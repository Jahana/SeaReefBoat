// src/profile/profile/profile_session.cpp: one profile connection, one request (polcore opens one connection per
// request).

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace XI.Profile
{
    public sealed class ProfileSession
    {
        private static readonly TimeSpan RequestDeadline = TimeSpan.FromSeconds(30);
        private const byte RequestKind = 2;
        private const int FriendStoreLimit = ProfileFriends.FriendListSize + ProfileFriends.BlockListSize;

        // across all connections
        private const long BodyBudget = 64L * 1024 * 1024;
        private static long _bodyBytesInFlight;

        private readonly Stream _stream;
        private readonly ProfileContext _context;
        private readonly ProfileRequests _requests;
        private readonly Action<string> _log;
        private readonly Action<string> _debug;
        private readonly uint _sessionValue;

        public ProfileSession(Stream stream, string peer, uint accountId, PresenceManager presence, ProfileRequests requests, Action<string> log, Action<string> debug)
        {
            _stream = stream;
            _context = new ProfileContext { Peer = peer, Presence = presence, AccountId = accountId };
            _requests = requests;
            _log = log ?? (_ => { });
            _debug = debug ?? (_ => { });
            // std::random_device{}()
            _sessionValue = (uint)RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);
        }

        private static long BodyLimit(ProfRequest request) => request switch
        {
            ProfRequest.WriteFile => ProfFileHead.Size + ProfileFiles.FileSizeLimit + 4,
            ProfRequest.StoreFriendList => StoreFriendListHead.Size + FriendStoreLimit * FriendInfo.Size + ProfTrailer.Size,
            _ => ProfFileRequest.Size,
        };

        public async Task RunAsync()
        {
            if (await ReadAsync(ProfOpenData.Size) == null) return;

            var openAnswer = ProfOpenAns.Make(ProfileAnswer.OpenAnswerCode, _sessionValue);
            if (!await WriteAsync(openAnswer)) return;

            // polcore opens one connection per request
            var headerBytes = await ReadAsync(ProfSendReqData.Size);
            if (headerBytes == null) return;

            var header = new ProfSendReqData(headerBytes);
            if (header.Kind != RequestKind || header.BodySize > BodyLimit(header.Request))
            {
                _log($"{_context.Peer} refused a malformed or oversized request");
                return;
            }
            // shared by every connection
            if (Interlocked.Add(ref _bodyBytesInFlight, header.BodySize) > BodyBudget)
            {
                Interlocked.Add(ref _bodyBytesInFlight, -header.BodySize);
                _log($"{_context.Peer} refused a request, too many bodies in flight");
                return;
            }
            try
            {
                var body = await ReadAsync((int)header.BodySize);
                if (body == null) return;

                _debug($"{_context.Peer} account {_context.AccountId} request {header.Command}/{header.SubCommand} size 0x{header.BodySize:x}");
                ProfileAnswer answer;
                lock (_context.Presence.Sync) answer = Handle(header.Request, body);
                if (answer != null)
                {
                    var head = answer.Head();
                    var bodyBytes = answer.Body();
                    var all = new byte[head.Length + bodyBytes.Length];
                    head.CopyTo(all, 0);
                    bodyBytes.CopyTo(all, head.Length);
                    await WriteAsync(all);
                }
                // runs even if the write failed
                if (_context.AfterAnswer != null)
                {
                    lock (_context.Presence.Sync) _context.AfterAnswer();
                }
            }
            finally
            {
                Interlocked.Add(ref _bodyBytesInFlight, -header.BodySize);
            }
        }

        // handle: null closes the connection
        private ProfileAnswer Handle(ProfRequest request, byte[] body)
        {
            switch (request)
            {
                case ProfRequest.LoadHandleNameList: return _requests.LoadHandleNameList(_context);
                case ProfRequest.LoadCharacterList: return _requests.LoadCharacterList(_context);
                case ProfRequest.SearchPolId: return _requests.SearchPolId(body);
                case ProfRequest.LoadGroupList: return new ProfileAnswer().Add(ProfCount.Make(0));
                case ProfRequest.LoadFriendList: return _requests.LoadFriendList(_context);
                case ProfRequest.StoreFriendList: return _requests.StoreFriendList(_context, body);
                case ProfRequest.LoadMyStatus: return _requests.LoadMyStatus(_context);
                case ProfRequest.ChangeMyStatus: return _requests.ChangeMyStatus(_context, body);
                case ProfRequest.SecurityToken: return new ProfileAnswer();
                case ProfRequest.ReadFile: return _requests.ReadFile(_context, body);
                case ProfRequest.WriteFile: return _requests.WriteFile(_context, body);
                case ProfRequest.RemoveFile: return _requests.DeleteFile(_context, body);
                case ProfRequest.GetFileList: return _requests.GetFileList(_context, body);
                // adds, renames, deletes and reorders handles
                case ProfRequest.StoreHandleNameList:
                // attaches characters to handles and reorders them
                case ProfRequest.StoreCharacterList:
                // one message to 1-20 recipients
                case ProfRequest.WriteFileMulti:
                // shown to friends
                case ProfRequest.SetHandleComment:
                case ProfRequest.PolbesUpdateRecord:
                case ProfRequest.CreateGroup:
                // owner only
                case ProfRequest.DeleteGroup:
                // invites, accepts, promotes, kicks or leaves a group member
                case ProfRequest.ChangeGroupMember:
                // updates the requester's entry in one or every group
                case ProfRequest.UpdateMyGroupEntry:
                    return Unimplemented(request, ProfError.Refused);
                // every row of a POLBES table, used by the help desk FAQ
                case ProfRequest.PolbesSearchDataAll:
                // searches a POLBES table, used by the GM call
                case ProfRequest.PolbesSearchData:
                    return Unimplemented(request, ProfError.NotFound);
                default:
                    _log($"{_context.Peer} unhandled request 0x{(ushort)request:x4}");
                    return null;
            }
        }

        private ProfileAnswer Unimplemented(ProfRequest request, ProfError error)
        {
            _debug($"{_context.Peer} unimplemented request 0x{(ushort)request:x4}");
            return new ProfileAnswer(error);
        }

        private async Task<byte[]> ReadAsync(int size)
        {
            var buffer = new byte[size];
            using var cts = new CancellationTokenSource(RequestDeadline);
            try
            {
                await _stream.ReadExactlyAsync(buffer, cts.Token);
                return buffer;
            }
            catch (Exception) { return null; }
        }

        private async Task<bool> WriteAsync(byte[] bytes)
        {
            using var cts = new CancellationTokenSource(RequestDeadline);
            try
            {
                await _stream.WriteAsync(bytes, cts.Token);
                await _stream.FlushAsync(cts.Token);
                return true;
            }
            catch (Exception) { return false; }
        }
    }
}
