using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace XI.Bots
{
    // The lobby as xiloader + the retail client drive it (src/login/data_session.cpp, view_session.cpp):
    //   data (plain TCP): the loader's socket. It opens with 0xA1 (account id, the lobby's address, the session hash),
    //     then answers the server's pushes: 0x01 (send the list again: 0xA1) and 0x02 (char picked: 0xA2 + the key).
    //   view (plain TCP): the client's socket. 0x26 version, 0x24 world list, 0x1F char list (the list comes back via
    //     the data socket's 0xA1), 0x22 / 0x21 name check and create, 0x07 select (the 0x0B with the map address).
    // Both sockets only re-arm their read once the server wrote (handler_session::write_func), so every request waits
    // for its answer before the next one goes out.
    public sealed class LobbyClient : IDisposable
    {
        public sealed class Character
        {
            public uint Id;
            public string Name;
        }

        // lpkt_next_login (0x0B): where the char plays, and the search server
        public sealed class Selected
        {
            public uint CharId;
            public string Name;
            public uint MapIp, SearchIp;
            public ushort MapPort, SearchPort;
            public uint[] Key = new uint[5];
        }

        private readonly Endpoints _ep;
        private readonly uint _accountId;
        private readonly byte[] _hash;
        private TcpClient _data, _view;
        private NetworkStream _ds, _vs;
        private readonly Channel<byte[]> _dataIn = Channel.CreateUnbounded<byte[]>();
        private readonly Channel<byte[]> _viewIn = Channel.CreateUnbounded<byte[]>();
        private byte _lastRequest;
        private Task _dataPump;

        // the client's lobby key counter: what the server follows in view_session::do_write (the 0x05 seeds it, the
        // requests and replies that step it)
        public uint KeyCounter { get; private set; }

        public List<Character> Characters { get; } = new List<Character>();
        // the last 0x20's slot count: the chars and, under login.CHARACTER_CREATION, the empty slots up to content_ids
        public int ListSlots { get; private set; }
        public TimeSpan Timeout = TimeSpan.FromSeconds(15);
        // the 0x26's client version (view_session.cpp compares its first 6 characters with login.CLIENT_VER's): not older
        // than settings/default/login.lua's CLIENT_VER, which VER_LOCK 2 refuses
        public string ClientVersion = "30261006_0";

        public LobbyClient(Endpoints ep, uint accountId, byte[] sessionHash)
        {
            _ep = ep;
            _accountId = accountId;
            _hash = sessionHash;
        }

        public async Task Connect(CancellationToken ct)
        {
            _data = _ep.NewTcpClient();
            _data.NoDelay = true;
            await _data.ConnectAsync(Endpoints.Checked(_ep.Host, _ep.Data), ct);
            _ds = _data.GetStream();
            _ = ReadData(ct);

            // the loader binds its data socket to the session first: the char list (0x03) it gets back proves it
            await _ds.WriteAsync(CharListRequest(), ct);
            var list = await Next(_dataIn, "data 0x03 after the first 0xA1", ct);
            if (list[0] != 0x03) throw new InvalidDataException($"data: expected 0x03, got 0x{list[0]:X2}");

            _view = _ep.NewTcpClient();
            _view.NoDelay = true;
            await _view.ConnectAsync(Endpoints.Checked(_ep.Host, _ep.View), ct);
            _vs = _view.GetStream();
            _ = ReadView(ct);
            _dataPump = PumpData(ct);
        }

        private byte[] CharListRequest()
        {
            var b = new byte[0x40];
            b[0] = 0xA1;
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(1), _accountId);
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(5), Ip(_ep.Host)); // the lobby address = the search server's
            _hash.CopyTo(b, 12);
            return b;
        }

        public static uint Ip(string dotted)
        {
            var p = dotted.Split('.');
            return (uint)(byte.Parse(p[0]) | byte.Parse(p[1]) << 8 | byte.Parse(p[2]) << 16 | byte.Parse(p[3]) << 24);
        }

        private Selected _pendingSelect;
        private readonly TaskCompletionSource<bool> _selectKeySent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // the loader's answers to the server's pushes on the data socket
        private async Task PumpData(CancellationToken ct)
        {
            try
            {
                while (true)
                {
                    var m = await _dataIn.Reader.ReadAsync(ct);
                    switch (m[0])
                    {
                        case 0x01: await _ds.WriteAsync(CharListRequest(), ct); break;
                        case 0x02:
                        {
                            // 0xA2: the key the client hashes its map traffic with; the server takes md5(session hash)
                            // and the lobby counter + 3 in place of the loader's (data_session.cpp 0xA2)
                            var key = new byte[20];
                            MD5.HashData(_hash).CopyTo(key, 0);
                            BinaryPrimitives.WriteUInt32LittleEndian(key.AsSpan(16), KeyCounter + 3);
                            if (_pendingSelect != null)
                                for (int i = 0; i < 5; i++) _pendingSelect.Key[i] = BinaryPrimitives.ReadUInt32LittleEndian(key.AsSpan(4 * i));
                            var b = new byte[0x40];
                            b[0] = 0xA2;
                            key.CopyTo(b, 1);
                            await _ds.WriteAsync(b, ct);
                            _selectKeySent.TrySetResult(true);
                            break;
                        }
                        case 0x03: break; // the list again
                    }
                }
            }
            catch (Exception) { }
        }

        private async Task ReadData(CancellationToken ct)
        {
            var acc = new List<byte>();
            var buf = new byte[4096];
            try
            {
                while (true)
                {
                    int n = await _ds.ReadAsync(buf, ct);
                    if (n <= 0) break;
                    acc.AddRange(new ArraySegment<byte>(buf, 0, n));
                    while (acc.Count > 0)
                    {
                        int len = acc[0] switch { 0x01 => 5, 0x02 => 5, 0x03 => 0x148, _ => acc.Count };
                        if (acc.Count < len) break;
                        var m = acc.GetRange(0, len).ToArray();
                        acc.RemoveRange(0, len);
                        _dataIn.Writer.TryWrite(m);
                    }
                }
            }
            catch (Exception) { }
            _dataIn.Writer.TryComplete();
        }

        private async Task ReadView(CancellationToken ct)
        {
            var acc = new List<byte>();
            var buf = new byte[8192];
            try
            {
                while (true)
                {
                    int n = await _vs.ReadAsync(buf, ct);
                    if (n <= 0) break;
                    acc.AddRange(new ArraySegment<byte>(buf, 0, n));
                    while (acc.Count >= 8)
                    {
                        int len = (int)BinaryPrimitives.ReadUInt32LittleEndian(acc.GetRange(0, 4).ToArray());
                        if (len < 8 || len > 0x10000) throw new InvalidDataException($"view: bad packet size {len}");
                        if (acc.Count < len) break;
                        var m = acc.GetRange(0, len).ToArray();
                        acc.RemoveRange(0, len);
                        Track(m);
                        _viewIn.Writer.TryWrite(m);
                    }
                }
            }
            catch (Exception) { }
            _viewIn.Writer.TryComplete();
        }

        // view_session's key counter rules, from the client's side of the socket
        private static bool StepsOnRequest(byte c) => c is 0x07 or 0x14 or 0x1F or 0x21 or 0x22 or 0x24 or 0x28 or 0x2B;
        private static bool StepsOnReply(byte c) => c is 0x03 or 0x0B or 0x20 or 0x23;
        private static bool StepsOnError(byte c) => c is 0x22 or 0x24 or 0x28 or 0x2B;

        private void Track(byte[] reply)
        {
            byte command = reply[8];
            if (command == 0x05) KeyCounter = BinaryPrimitives.ReadUInt32LittleEndian(reply.AsSpan(0x1C)) + 1;
            else if (StepsOnReply(command) || (command == 0x04 && StepsOnError(_lastRequest))) KeyCounter++;
        }

        private async Task<byte[]> Next(Channel<byte[]> ch, string what, CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(Timeout);
            try { return await ch.Reader.ReadAsync(cts.Token); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new TimeoutException($"lobby: no {what}"); }
            catch (ChannelClosedException) { throw new IOException($"lobby: socket closed waiting for {what}"); }
        }

        private byte[] Request(byte code, int len = 0x98)
        {
            var b = new byte[len];
            BinaryPrimitives.WriteUInt32LittleEndian(b, (uint)len);
            b[4] = 0x49; b[5] = 0x58; b[6] = 0x46; b[7] = 0x46; // "IXFF"
            b[8] = code;
            _hash.CopyTo(b, 12);
            return b;
        }

        private async Task<byte[]> View(byte[] request, string what, CancellationToken ct)
        {
            _lastRequest = request[8];
            if (StepsOnRequest(request[8])) KeyCounter++;
            await _vs.WriteAsync(request, ct);
            return await Next(_viewIn, what, ct);
        }

        private static byte[] Expect(byte[] reply, byte command, string what)
        {
            if (reply[8] == command) return reply;
            if (reply[8] == 0x04) throw new InvalidDataException($"lobby: {what}: error {BinaryPrimitives.ReadUInt16LittleEndian(reply.AsSpan(32))}");
            throw new InvalidDataException($"lobby: {what}: reply 0x{reply[8]:X2}");
        }

        public async Task Version(CancellationToken ct)
        {
            var r = Request(0x26);
            Encoding.ASCII.GetBytes(ClientVersion).CopyTo(r, 0x74);
            Expect(await View(r, "0x05 version reply", ct), 0x05, "0x26 version");
        }

        public async Task<string> WorldList(CancellationToken ct)
        {
            var w = Expect(await View(Request(0x24), "0x23 world list", ct), 0x23, "0x24 world list");
            int n = 0;
            while (n < 15 && w[36 + n] != 0) n++;
            return Encoding.ASCII.GetString(w, 36, n);
        }

        // 0x1F: the data socket gets 0x01, answers 0xA1, and the view socket the list (lpkt_chr_info2, 0x20)
        public async Task<List<Character>> CharList(CancellationToken ct)
        {
            var list = Expect(await View(Request(0x1F), "0x20 char list", ct), 0x20, "0x1F char list");
            Characters.Clear();
            uint count = BinaryPrimitives.ReadUInt32LittleEndian(list.AsSpan(28));
            ListSlots = (int)count;
            for (int i = 0; i < count && 32 + (i + 1) * 140 <= list.Length; i++)
            {
                int at = 32 + i * 140;
                if (list[at + 12] == 0x20 || list[at + 12] == 0) continue; // an empty slot
                int n = 0;
                while (n < 15 && list[at + 12 + n] != 0) n++;
                Characters.Add(new Character { Id = BinaryPrimitives.ReadUInt32LittleEndian(list.AsSpan(at)), Name = Encoding.ASCII.GetString(list, at + 12, n) });
            }
            return Characters;
        }

        // 0x22 alone: 0 when the lobby would register the name, else its error code (the 0x04's)
        public async Task<ushort> CheckName(string name, CancellationToken ct)
        {
            var check = Request(0x22);
            Encoding.ASCII.GetBytes(name).CopyTo(check, 32);
            var r = await View(check, "0x03 name check", ct);
            if (r[8] == 0x03) return 0;
            if (r[8] == 0x04) return BinaryPrimitives.ReadUInt16LittleEndian(r.AsSpan(32));
            throw new InvalidDataException($"lobby: 0x22 check name {name}: reply 0x{r[8]:X2}");
        }

        // 0x22 then 0x21 (lpkt_chr_create): a Galka (race 8) warrior of Bastok
        public async Task Create(string name, CancellationToken ct)
        {
            var check = Request(0x22);
            Encoding.ASCII.GetBytes(name).CopyTo(check, 32);
            Expect(await View(check, "0x03 name check", ct), 0x03, "0x22 check name " + name);
            var reg = Request(0x21);
            reg[48] = 8;  // race
            reg[50] = 1;  // main job
            reg[54] = 1;  // nation
            reg[57] = 1;  // size
            reg[60] = 3;  // face
            Expect(await View(reg, "0x03 create", ct), 0x03, "0x21 create " + name);
        }

        // 0x07: the data socket gets 0x02 and sends 0xA2 with the key; the view socket the 0x0B, then the server
        // closes it
        public async Task<Selected> Select(Character c, CancellationToken ct)
        {
            var sel = new Selected { CharId = c.Id, Name = c.Name };
            _pendingSelect = sel;
            var r = Request(0x07);
            BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(28), c.Id);
            Encoding.ASCII.GetBytes(c.Name).CopyTo(r, 36);
            var next = Expect(await View(r, "0x0B char select", ct), 0x0B, "0x07 select " + c.Name);
            if (!_selectKeySent.Task.IsCompleted) throw new InvalidDataException("lobby: 0x0B before the 0xA2");
            sel.MapIp = BinaryPrimitives.ReadUInt32LittleEndian(next.AsSpan(56));
            sel.MapPort = (ushort)BinaryPrimitives.ReadUInt32LittleEndian(next.AsSpan(60));
            sel.SearchIp = BinaryPrimitives.ReadUInt32LittleEndian(next.AsSpan(64));
            sel.SearchPort = (ushort)BinaryPrimitives.ReadUInt32LittleEndian(next.AsSpan(68));
            var hash = MD5.HashData(Zeroed(next, 12, 16));
            if (!hash.AsSpan().SequenceEqual(next.AsSpan(12, 16))) throw new InvalidDataException("lobby: 0x0B hash mismatch");
            return sel;
        }

        private static byte[] Zeroed(byte[] p, int off, int len)
        {
            var c = (byte[])p.Clone();
            Array.Clear(c, off, len);
            return c;
        }

        public void Dispose()
        {
            try { _view?.Close(); } catch (Exception) { }
            try { _data?.Close(); } catch (Exception) { }
        }
    }
}
