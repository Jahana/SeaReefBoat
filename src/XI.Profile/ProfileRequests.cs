// src/profile/profile/{characters,files,friends,messages,status}.cpp: the profile request handlers. A null answer
// closes the connection without one.

using System;
using System.Collections.Generic;
using System.Linq;

namespace XI.Profile
{
    // profile/context.h
    public sealed class ProfileContext
    {
        public string Peer;
        public PresenceManager Presence;
        public uint AccountId;
        public Action AfterAnswer; // runs after the answer is sent
    }

    public sealed class ProfileRequests
    {
        private readonly ProfileAccounts _accounts;
        private readonly ProfileFiles _files;
        private readonly ProfileFriends _friends;
        private readonly XI.Persistence.IDatabase _db;
        private readonly Action<string> _log;

        public ProfileRequests(XI.Persistence.IDatabase db, ProfileAccounts accounts, ProfileFiles files, ProfileFriends friends, Action<string> log)
        {
            _db = db;
            _accounts = accounts;
            _files = files;
            _friends = friends;
            _log = log ?? (_ => { });
        }

        // ---- characters.cpp ----

        // loadHandleNameList: we don't support multiple handles, this returns the first character as the handle name
        public ProfileAnswer LoadHandleNameList(ProfileContext context)
        {
            var characters = _accounts.Characters(context.AccountId);
            if (characters == null) return new ProfileAnswer(ProfError.Refused);

            string name = "Player";
            if (characters.Count > 0) name = characters[0].Name;
            var handle = HandleNameInfo.Make(context.AccountId, ProfileAccounts.NameBytes(name));
            return new ProfileAnswer().Add(ProfCount.Make(1)).Add(handle);
        }

        // loadCharacterList: the client matches its character by key, else it shows as PlayOnline only
        public ProfileAnswer LoadCharacterList(ProfileContext context)
        {
            const int handleSlots = 8;
            var characters = _accounts.Characters(context.AccountId);
            if (characters == null) return new ProfileAnswer(ProfError.Refused);

            var characterInfos = new List<byte[]>();
            foreach (var character in characters)
            {
                byte index = (byte)characterInfos.Count;
                bool attached = index < handleSlots;
                characterInfos.Add(CharacterInfo.Make(index, index, (byte)(attached ? 1 : 0), attached ? index : (byte)0, Content.FFXI,
                                                      ProfileCodec.CharacterKey(character.Id), character.Id, ProfileAccounts.NameBytes(character.Name)));
            }
            return new ProfileAnswer().Add(ProfCount.Make((uint)characterInfos.Count)).AddAll(characterInfos);
        }

        // searchPolId: FFXI passes the key from the search server
        public ProfileAnswer SearchPolId(ReadOnlySpan<byte> body)
        {
            if (!ProfileAnswer.Parse(body, SearchPolIdReq.Size, true)) return null;

            byte[] answer;
            var owner = _accounts.CharacterOwner(ProfileCodec.CharacterIdFromKey(SearchPolIdReq.WorldAndId(body)));
            if (owner != null) answer = SearchPolIdAns.Make(owner.AccountId, owner.AccountId, ProfileAccounts.NameBytes(owner.CharacterName), 1);
            else answer = SearchPolIdAns.Make(0, 0, ReadOnlySpan<byte>.Empty, 0);
            return new ProfileAnswer().Add(answer, true);
        }

        // ---- files.cpp ----

        private static bool IsOwnFile(ProfFileHead head, uint accountId) => head.Target == 0 || head.Target == accountId;

        public ProfileAnswer ReadFile(ProfileContext context, ReadOnlySpan<byte> body)
        {
            if (!ProfileAnswer.Parse(body, ProfFileRequest.Size, true)) return null;
            var head = new ProfFileHead(body);
            if (!IsOwnFile(head, context.AccountId)) return new ProfileAnswer(ProfError.NotFound);

            // a DB error must not look like a missing file
            var contents = _files.Read(context.AccountId, head.Path);
            if (!contents.Ok) return new ProfileAnswer(ProfError.Refused);
            if (contents.Data == null || head.Offset > (uint)contents.Data.Length) return new ProfileAnswer(ProfError.NotFound);
            if (head.Offset == 0) _log($"account {context.AccountId} loading {ProfileCodec.Latin1(head.Path)} ({contents.Data.Length} bytes)");

            var data = contents.Data.AsSpan((int)head.Offset);
            var answer = new ProfileAnswer();
            answer.AddBytes(data.Slice(0, (int)Math.Min((uint)data.Length, head.Length))).AddChecksum();
            return answer;
        }

        // writeFile: writes to another account are messages
        public ProfileAnswer WriteFile(ProfileContext context, ReadOnlySpan<byte> body)
        {
            if (body.Length < ProfFileHead.Size + 4 || !ProfileAnswer.HasTrailingChecksum(body)) return null;
            var head = new ProfFileHead(body.Slice(0, ProfFileHead.Size));
            var data = body.Slice(ProfFileHead.Size, body.Length - ProfFileHead.Size - 4).ToArray();
            if (!IsOwnFile(head, context.AccountId)) return PostMessage(context, head, head.Path, data);

            if (!_files.Write(context.AccountId, head.Path, head.Offset, data)) return new ProfileAnswer(ProfError.Refused);
            if (head.Offset == 0) _log($"account {context.AccountId} saving {ProfileCodec.Latin1(head.Path)}");
            return new ProfileAnswer();
        }

        // deleteFile: the checksum sits in the length field
        public ProfileAnswer DeleteFile(ProfileContext context, ReadOnlySpan<byte> body)
        {
            if (body.Length != ProfFileHead.Size || !ProfileAnswer.HasTrailingChecksum(body)) return null;
            var head = new ProfFileHead(body);
            if (!IsOwnFile(head, context.AccountId) || !_files.Erase(context.AccountId, head.Path)) return new ProfileAnswer(ProfError.Refused);
            return new ProfileAnswer();
        }

        // getFileList: file count, file list and message list share this request
        public ProfileAnswer GetFileList(ProfileContext context, ReadOnlySpan<byte> body)
        {
            if (!ProfileAnswer.Parse(body, ProfFileRequest.Size, true)) return null;
            var head = new ProfFileHead(body);
            if (!IsOwnFile(head, context.AccountId)) return new ProfileAnswer(ProfError.Refused);

            var directory = head.Path;
            bool isMessageList = directory.AsSpan().SequenceEqual(ProfileFiles.Mailbox);
            ushort maxEntries = (ushort)head.Offset;
            ushort limit = maxEntries == 0 ? ushort.MaxValue : maxEntries;
            var entries = _files.List(context.AccountId, directory, limit);
            if (entries == null) return new ProfileAnswer(ProfError.Refused);

            if (!isMessageList && maxEntries == 0) return new ProfileAnswer().Add(ProfCount.Make((uint)entries.Count));

            var answer = new ProfileAnswer();
            answer.Add(ProfCount.Make((uint)entries.Count)).AddAll(entries);
            // an empty file list has no checksum, the message list always does
            if (isMessageList || entries.Count > 0) answer.AddChecksum();
            return answer;
        }

        // ---- friends.cpp ----

        // loadFriendList: statuses follow as notices once the list is sent
        public ProfileAnswer LoadFriendList(ProfileContext context)
        {
            var entries = _friends.Load(context.AccountId);
            if (entries == null) return new ProfileAnswer(ProfError.Refused);

            var presence = context.Presence;
            uint accountId = context.AccountId;
            context.AfterAnswer = () => presence.SendFriendsTo(accountId);
            return new ProfileAnswer().Add(ProfCount.Make((uint)entries.Count)).AddAll(entries.Select(e => e.Bytes));
        }

        public ProfileAnswer StoreFriendList(ProfileContext context, ReadOnlySpan<byte> body)
        {
            const int fixedSize = StoreFriendListHead.Size + ProfTrailer.Size;
            if (body.Length < fixedSize || (body.Length - fixedSize) % FriendInfo.Size != 0) return null;

            var head = new StoreFriendListHead(body.Slice(0, StoreFriendListHead.Size));
            uint trailerUnused00 = Le.U32(body, body.Length - ProfTrailer.Size);
            int count = (body.Length - fixedSize) / FriendInfo.Size;
            if (head.Count != count || !ProfileAnswer.HasTrailingChecksum(body)) return null;

            var entries = new List<FriendInfo>(count);
            for (int i = 0; i < count; i++) entries.Add(new FriendInfo(body.Slice(StoreFriendListHead.Size + i * FriendInfo.Size, FriendInfo.Size)));

            uint accountId = context.AccountId;
            var result = _friends.Store(accountId, head, entries);
            if (result == null) return new ProfileAnswer(ProfError.Refused);

            foreach (var entry in entries)
            {
                if (entry.Result != 0) continue;
                string list = entry.IsBlack != 0 ? "block" : "friend";
                if (entry.Op == FriendInfo.OpDelete)
                {
                    _log($"account {accountId} removed entry {entry.Num} from its {list} list");
                    continue;
                }
                string pending = entry.BTemporary != 0 ? ", pending" : "";
                _log($"account {accountId} put account {entry.PolId} (\"{ProfileCodec.Latin1(ProfileCodec.CString(entry.HandleName))}\") at entry {entry.Num} of its {list} list{pending}");
            }
            foreach (var other in result.Accepted) _log($"accounts {accountId} and {other} are now friends");
            foreach (var other in result.Lost) _log($"account {accountId} no longer shares presence with account {other}");

            var presence = context.Presence;
            context.AfterAnswer = () =>
            {
                foreach (var other in result.Accepted) presence.Introduce(accountId, other);
                foreach (var other in result.Lost) presence.Hide(accountId, other);
            };
            return new ProfileAnswer().Add(ProfCount.Make((uint)count)).AddAll(entries.Select(e => e.Bytes)).Add(ProfTrailer.Make(trailerUnused00), true);
        }

        // ---- messages.cpp ----

        // postMessage
        public ProfileAnswer PostMessage(ProfileContext context, ProfFileHead head, byte[] path, byte[] data)
        {
            uint accountId = context.AccountId;
            if (!ProfileFiles.StartsWithMailbox(path))
            {
                _log($"{context.Peer} account {accountId} refused a write outside the mailbox of 0x{head.Target:x}");
                return new ProfileAnswer(ProfError.Refused);
            }

            uint recipient = (uint)head.Target;
            var messageName = path.AsSpan(ProfileFiles.Mailbox.Length).ToArray();
            string messageNameText = ProfileCodec.Latin1(messageName);
            var name = ProfileNotices.ParseMessageName(messageNameText);
            // sent in one chunk, and both sizes must agree
            if (head.Target != recipient || head.Offset != 0 || head.Length != (uint)data.Length || name == null || name.Length != (uint)data.Length ||
                name.Sender != accountId || name.Recipient != recipient || !_accounts.Exists(recipient))
            {
                _log($"{context.Peer} account {accountId} refused a message for 0x{head.Target:x}");
                return new ProfileAnswer(ProfError.Refused);
            }

            // no spoofing the sender name
            var characters = _accounts.Characters(accountId);
            if (characters == null || !characters.Any(c => ProfileAccounts.NameBytes(c.Name).AsSpan().SequenceEqual(name.SenderName)))
            {
                _log($"{context.Peer} account {accountId} refused a message signed as {ProfileCodec.Latin1(name.SenderName)}");
                return new ProfileAnswer(ProfError.Refused);
            }

            // retail tells blocked senders it went through
            var blocked = _friends.Blocks(recipient, accountId);
            if (blocked == null) return new ProfileAnswer(ProfError.Refused);
            if (blocked.Value)
            {
                _log($"account {accountId} sent a {name.Type} to account {recipient}, dropped by their block list");
                return new ProfileAnswer();
            }

            // friend request replies update the lists in the same transaction
            bool stored = false, accepted = false;
            bool committed = _db.Transaction(() =>
            {
                stored = _files.Post(accountId, recipient, messageName, name.Type, data);
                if (!stored) return;
                if (name.Type == MessageType.FriendAccepted) accepted = _friends.Accept(recipient, accountId);
                // retail keeps declined requests pending, we drop them
                if (name.Type == MessageType.FriendDeclined) _friends.DropPending(recipient, accountId);
            });
            if (!committed || !stored) return new ProfileAnswer(ProfError.Refused);

            _log($"account {accountId} sent a {name.Type} to account {recipient}");
            if (accepted) _log($"accounts {accountId} and {recipient} are now friends");

            var presence = context.Presence;
            context.AfterAnswer = () =>
            {
                if (accepted) presence.Introduce(accountId, recipient);
                presence.DeliverMessage(accountId, recipient, messageNameText);
            };
            return new ProfileAnswer();
        }

        // ---- status.cpp ----

        // openStatusFromWire: OpenStatus + 1 on the wire, 0 keeps the current one
        private static OpenStatus? OpenStatusFromWire(byte openStat)
        {
            var status = (OpenStatus)(byte)(openStat - 1);
            return status is OpenStatus.Online or OpenStatus.Away or OpenStatus.Invisible ? status : null;
        }

        public ProfileAnswer LoadMyStatus(ProfileContext context)
        {
            var status = context.Presence.GetStatus(context.AccountId) ?? new PresenceManager.Status();
            return new ProfileAnswer().Add(MyStatus.Make((byte)(status.InGame ? 1 : 0), status.CharacterSlot, (byte)(status.ReceiveMessages ? 1 : 0),
                                                         Content.FFXI, (byte)((byte)status.OpenStatus + 1)), true);
        }

        // changeMyStatus: the game hashes the answer's value with a counter into its map session key at lobby login
        public ProfileAnswer ChangeMyStatus(ProfileContext context, ReadOnlySpan<byte> body)
        {
            if (!ProfileAnswer.Parse(body, ChangeMyStatusReq.Size, true)) return null;
            var request = new ChangeMyStatusReq(body);

            var current = context.Presence.GetStatus(context.AccountId);
            if (current != null && request.BNoChange == 0)
            {
                var characters = _accounts.Characters(context.AccountId);
                if (characters == null) return new ProfileAnswer(ProfError.Refused);

                // only echoed back, but must be a real character
                bool inGame = request.BCharacterFound != 0 && request.ActiveCharacterSlot < characters.Count;
                bool saved = context.Presence.SetStatus(context.AccountId, new PresenceManager.Status
                {
                    OpenStatus = OpenStatusFromWire(request.OpenStat) ?? current.Value.OpenStatus,
                    InGame = inGame,
                    CharacterSlot = request.ActiveCharacterSlot,
                    ReceiveMessages = request.BReceiveOnlineMessage != 0,
                });
                if (!saved) return new ProfileAnswer(ProfError.Refused);
            }

            return new ProfileAnswer().Add(ChangeMyStatusAns.Make(_accounts.KeyValue(context.AccountId)), true);
        }
    }
}
