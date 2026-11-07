// src/profile/irc/irc_session.cpp: one polcore IRC connection (presence, friend status and message notices).
//
// The C++ reads with async_read_until under a timeout and writes from an outbox in a second coroutine; here a reader
// task splits the stream into lines for the read loop, which applies the same deadlines, size and checksum rules.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace XI.Profile
{
    public sealed class IrcSession : PresenceManager.ISession
    {
        // polcore times out after 400s of silence
        private static readonly TimeSpan KeepaliveInterval = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan LoginDeadline = TimeSpan.FromSeconds(30);
        private const int LineLimit = 0x1000;
        private const int OutboxLimit = 1024; // lines

        private readonly Stream _stream;
        private readonly IDisposable _socket;
        private readonly IPEndPoint _remote;
        private readonly IPEndPoint _local;
        private readonly string _peer;
        private readonly Credential _credential;
        private readonly PresenceManager _presence;
        private readonly ProfileAccounts _accounts;
        private readonly Action<string> _log;
        private readonly Action<string> _debug;

        private readonly Channel<string> _lines = Channel.CreateUnbounded<string>(); // completed on EOF or a read error
        private readonly ConcurrentQueue<string> _outbox = new();
        private readonly SemaphoreSlim _wake = new(0); // released by Send() to wake the write loop
        private readonly object _outLock = new();
        private volatile bool _closed;
        private bool _keyed;
        private ulong _signOnId;

        public IrcSession(Stream stream, IDisposable socket, IPEndPoint remote, IPEndPoint local, string peer, Credential credential,
                          PresenceManager presence, ProfileAccounts accounts, Action<string> log, Action<string> debug)
        {
            _stream = stream;
            _socket = socket;
            _remote = remote;
            _local = local;
            _peer = peer;
            _credential = credential;
            _presence = presence;
            _accounts = accounts;
            _log = log ?? (_ => { });
            _debug = debug ?? (_ => { });
        }

        // tokens: splits a line into words, skipping the :prefix
        private static List<string> Tokens(string line)
        {
            if (line.StartsWith(':'))
            {
                int space = line.IndexOf(' ');
                line = space < 0 ? string.Empty : line.Substring(space);
            }
            var result = new List<string>();
            foreach (var token in line.Split(' '))
            {
                if (token.Length > 0) result.Add(token);
            }
            return result;
        }

        // challenge: login time, client IP and port, base32 encoded
        private string Challenge()
        {
            var blob = new byte[25];
            uint now = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            blob[0] = (byte)(now >> 24);
            blob[1] = (byte)(now >> 16);
            blob[2] = (byte)(now >> 8);
            blob[3] = (byte)now;
            var address = _remote.Address.IsIPv4MappedToIPv6 ? _remote.Address.MapToIPv4() : _remote.Address;
            address.GetAddressBytes().AsSpan(0, 4).CopyTo(blob.AsSpan(4));
            blob[0x14] = (byte)(_local.Port >> 8);
            blob[0x15] = (byte)_local.Port;
            return ProfileCodec.Base32Encode(blob);
        }

        // signOff: not in a destructor, presence may already be gone at shutdown
        public void SignOff()
        {
            if (_signOnId == 0) return;
            try
            {
                lock (_presence.Sync)
                {
                    _presence.SignOff(_credential.AccountId, _signOnId);
                    // gives the client two hours to reconnect
                    _accounts.Refresh(_credential.AccountId, _credential.SessionHash);
                }
                _log($"{_peer} account {_credential.AccountId} signed off");
            }
            catch (Exception e)
            {
                _log($"account {_credential.AccountId} sign off failed: {e.Message}");
            }
        }

        public async Task RunAsync()
        {
            Send(":srv NOTICE AUTH " + Challenge());
            var reader = ReaderAsync();
            var readLoop = ReadLoopAsync();
            var writeLoop = WriteLoopAsync();
            await Task.WhenAny(readLoop, writeLoop);
            Close();
            try { await Task.WhenAll(readLoop, writeLoop, reader); } catch { }
        }

        // the async_read_until half of readLine: lines up to and including '\n', at most LineLimit buffered
        private async Task ReaderAsync()
        {
            var input = new List<byte>();
            var buffer = new byte[LineLimit];
            try
            {
                while (true)
                {
                    int n = await _stream.ReadAsync(buffer);
                    if (n <= 0) break;
                    input.AddRange(buffer.AsSpan(0, n).ToArray());
                    int newline;
                    while ((newline = input.IndexOf((byte)'\n')) >= 0)
                    {
                        // read_until stops at max_size, so a line must fit the buffer
                        if (newline + 1 > LineLimit) return;
                        var line = input.GetRange(0, newline + 1).ToArray();
                        input.RemoveRange(0, newline + 1);
                        _lines.Writer.TryWrite(ProfileCodec.Latin1(line));
                    }
                    if (input.Count >= LineLimit) return;
                }
            }
            catch (Exception) { }
            finally { _lines.Writer.TryComplete(); }
        }

        private async Task ReadLoopAsync()
        {
            var loginBy = DateTime.UtcNow + LoginDeadline;
            bool idle = false;
            while (!_closed)
            {
                var deadline = KeepaliveInterval;
                if (_signOnId == 0) deadline = loginBy - DateTime.UtcNow;
                var (timedOut, line) = await ReadLineAsync(deadline);
                if (timedOut)
                {
                    // never signed in, or didn't answer the last PING
                    if (_signOnId == 0 || idle) break;
                    idle = true;
                    Send("PING :x");
                    continue;
                }
                idle = false;
                if (line == null || !Handle(line)) break;
            }
        }

        private async Task WriteLoopAsync()
        {
            while (true)
            {
                await _wake.WaitAsync();
                if (_closed) return;
                if (!_outbox.TryDequeue(out var line)) continue;
                using var cts = new CancellationTokenSource(KeepaliveInterval);
                try
                {
                    await _stream.WriteAsync(ProfileCodec.Latin1(line), cts.Token);
                    await _stream.FlushAsync(cts.Token);
                }
                catch (Exception) { return; }
            }
        }

        // send: drops the client if too much is queued
        public void Send(string message)
        {
            _debug($"{_peer} account {_credential.AccountId} -> {message}");
            string line = message + ProfileCodec.LineChecksum(message) + "\r\n";
            lock (_outLock)
            {
                if (_closed) return;
                if (_outbox.Count >= OutboxLimit)
                {
                    _log($"{_peer} stopped reading, closing");
                    Close();
                    return;
                }
                _outbox.Enqueue(line);
            }
            _wake.Release();
        }

        public void Close()
        {
            lock (_outLock)
            {
                if (_closed) return;
                _closed = true;
                _outbox.Clear();
            }
            try { _stream.Dispose(); } catch { }
            try { _socket?.Dispose(); } catch { }
            _wake.Release();
        }

        private bool Handle(string line)
        {
            lock (_presence.Sync)
            {
                _debug($"{_peer} account {_credential.AccountId} <- {line}");
                var command = Tokens(line);
                if (command.Count == 0) return true;

                if (command[0] == "USER")
                {
                    // the key in 300 is unused since xiloader disables the cipher
                    if (!_keyed)
                    {
                        Send(":srv 300 * " + ProfileCodec.Base64Encode(new byte[32]));
                        _keyed = true;
                    }
                    return true;
                }
                if (command[0] == "NICK")
                {
                    // only once, after USER
                    if (!_keyed || _signOnId != 0 || command.Count < 2) return true;
                    Login(command[1]);
                    return true;
                }
                // polcore ignores the PONG argument
                if (command[0] == "PING")
                {
                    Send("PONG a");
                    return true;
                }
                if (command[0] == "QUIT") return false;
                return true;
            }
        }

        // login: NICK U<scrambled pol id>:<digest>:<client info>; the relay already vouched for the account
        private void Login(string argument)
        {
            int colon = argument.IndexOf(':');
            string nick = colon < 0 ? argument : argument.Substring(0, colon);
            _accounts.Refresh(_credential.AccountId, _credential.SessionHash);
            ushort udpPortSlot = _accounts.UdpPortSlot(_credential);
            _log($"{_peer} account {_credential.AccountId} signed on, udp port slot {udpPortSlot}");
            Send($":srv 422 {nick} :no motd");
            Send($":{ProfileCodec.ScrambleNick(1)}!p@pol NOTICE {nick} :{ProfileNotices.ProfileAvailable(udpPortSlot)}");
            _signOnId = _presence.SignOn(_credential.AccountId, _credential.SessionHash, this);
        }

        // readLine: a line without its checksum, dropping lines that fail it; timedOut when the deadline passed first,
        // a null line when the stream ended
        private async Task<(bool TimedOut, string Line)> ReadLineAsync(TimeSpan deadline)
        {
            using var cts = new CancellationTokenSource(deadline > TimeSpan.Zero ? deadline : TimeSpan.Zero);
            while (true)
            {
                string line;
                try { line = await _lines.Reader.ReadAsync(cts.Token); }
                catch (OperationCanceledException) { return (true, null); }
                catch (ChannelClosedException) { return (false, null); }

                line = line.TrimEnd('\r', '\n');
                if (line.Length < 4) continue;
                string message = line.Substring(0, line.Length - 4);
                if (_keyed && ProfileCodec.LineChecksum(message) != line.Substring(line.Length - 4))
                {
                    _debug($"{_peer} dropped a line with a bad checksum: {line}");
                    continue;
                }
                return (false, message);
            }
        }
    }
}
