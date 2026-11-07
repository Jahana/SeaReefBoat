using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using XI.Persistence;
using XI.Profile;

// XI.Verify.Profile.Live -- end-to-end gate for the ported xi_profile: a real ProfileEngine (TLS 1.3 profile and IRC
// listeners on ephemeral ports) in front of a real MariaDB (docker xi-live-db, host:3307, xidb schema), driven by a
// client speaking the xiloader relay's hello, the profile request protocol and polcore's IRC lines.
//
//   XI.Verify.Profile.Live [dbPort]                                 the gate
//   XI.Verify.Profile.Live [dbPort] golden <cppProfilePort> <cppIrcPort>
//                                                                   also sends the deterministic requests to a running
//                                                                   C++ xi_profile and compares the replies byte for byte
//
// Accounts and characters 996041-996049 only (names "Prf*"). Exit codes: 0 = all checks passed; 1 = a check failed;
// 3 = DB not reachable.
internal static class Program
{
    private static int _pass, _fail;

    private const uint A = 996041, B = 996042, C = 996043, Banned = 996044, Stale = 996045;
    private const uint CharA1 = 996041, CharA2 = 996042, CharB = 996043, CharC = 996044;
    private static readonly uint[] Accounts = { A, B, C, Banned, Stale };
    private static readonly Dictionary<uint, byte[]> Hash = new();

    private const ulong MessagePolIdMask = 0x1C273E4567891133ul;

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static void CheckTrue(string name, bool cond) => Check(name, true, cond);

    private static ResultSet Sql(IDatabase db, string query, params object[] args)
    {
        var bound = args.Select(a => a switch
        {
            uint v => BoundValue.Of(v),
            int v => BoundValue.Of(v),
            ushort v => BoundValue.Of(v),
            byte v => BoundValue.Of(v),
            string v => BoundValue.Of(v),
            byte[] v => BoundValue.Of(v),
            _ => throw new NotSupportedException(),
        }).ToArray();
        return db.Execute(query, bound);
    }

    // every table with a charid column, read from the schema at run time: chars' char_insert trigger fills char_equip, char_exp,
    // char_history, char_inventory, char_jobs, char_pet, char_points, char_profile, char_storage and char_unlocks and only
    // char_delete clears them, so a run aborted with one of them left behind would make the next run's INSERT INTO chars collide
    private static List<string> CharidTables(IDatabase db)
    {
        var tables = new List<string>();
        var r = Sql(db, "SELECT table_name AS t FROM information_schema.columns WHERE table_schema = DATABASE() AND column_name = 'charid' AND table_name <> 'chars'");
        while (r != null && r.Next()) tables.Add(r.Get<string>("t"));
        foreach (var t in new[] { "char_equip", "char_exp", "char_history", "char_inventory", "char_jobs", "char_pet", "char_points", "char_profile",
                                  "char_storage", "char_unlocks" })
            if (!tables.Contains(t)) tables.Add(t);
        return tables;
    }

    private static void Cleanup(IDatabase db)
    {
        foreach (var c in new[] { CharA1, CharA2, CharB, CharC })
            foreach (var t in CharidTables(db)) Sql(db, $"DELETE FROM `{t}` WHERE charid = ?", c);
        foreach (var id in Accounts)
        {
            Sql(db, "DELETE FROM accounts_friends WHERE accid = ? OR friend_accid = ?", id, id);
            Sql(db, "DELETE FROM accounts_messages WHERE accid = ? OR sender_accid = ?", id, id);
            Sql(db, "DELETE FROM accounts_files WHERE accid = ?", id);
            Sql(db, "DELETE FROM accounts_profile WHERE accid = ?", id);
            Sql(db, "DELETE FROM accounts_sessions WHERE accid = ?", id);
            Sql(db, "DELETE FROM chars WHERE accid = ?", id);
            Sql(db, "DELETE FROM accounts WHERE id = ?", id);
        }
    }

    private static void Setup(IDatabase db)
    {
        foreach (var id in Accounts)
        {
            Sql(db, "INSERT INTO accounts(id,login,password,status) VALUES (?,?,?,?)", id, $"prf{id}", "x", id == Banned ? (byte)0 : (byte)1);
            var hash = RandomNumberGenerator.GetBytes(16);
            Hash[id] = hash;
            Sql(db, "INSERT INTO accounts_profile(accid, session_hash, refreshed, client_addr, udp_port_slot) VALUES (?, ?, " +
                    (id == Stale ? "NOW() - INTERVAL 3 HOUR" : "NOW()") + ", '127.0.0.1', ?)", id, hash, id == A ? (ushort)3 : (ushort)0);
        }
        void Char(uint charId, uint accid, string name) =>
            Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,230,0,0,0,0,0,0)", charId, accid, name);
        Char(CharA1, A, "PrfAlpha");
        Char(CharA2, A, "PrfAlphb");
        Char(CharB, B, "PrfBeta");
        Char(CharC, C, "PrfGamma");
        // B is in game
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,0,0)", B, CharB);
    }

    private static int Main(string[] argv)
    {
        int dbPort = argv.Length > 0 ? int.Parse(argv[0]) : 3307;
        var settings = new NetworkSettings { SqlHost = "127.0.0.1", SqlPort = dbPort, SqlLogin = "root", SqlPassword = "root", SqlDatabase = "xidb" };
        using var db = new MySqlDatabase(settings);
        try
        {
            var probe = db.Execute("SELECT 1", Array.Empty<BoundValue>());
            if (probe == null || !probe.Next()) { Console.WriteLine("DB not reachable"); return 3; }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DB not reachable ({ex.GetType().Name}: {ex.Message})");
            return 3;
        }

        try
        {
            Cleanup(db);
            Setup(db);
            Codec();
            if (argv.Length >= 4 && argv[1] == "golden") Golden(db, int.Parse(argv[2]), int.Parse(argv[3])).GetAwaiter().GetResult();
            else RunAsync(db).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FAIL unhandled {ex}");
        }
        finally
        {
            Cleanup(db);
        }

        Console.WriteLine($"RESULT: {_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    // ---------------------------------------------------------------- client side of the protocol

    private static async Task<SslStream> Connect(int port, uint accid, byte[] hash)
    {
        var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, port);
        var tls = new SslStream(tcp.GetStream(), false);
        await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = "localhost",
            EnabledSslProtocols = SslProtocols.Tls13,
            RemoteCertificateValidationCallback = (_, _, _, _) => true,
        });
        var hello = new byte[20];
        BinaryPrimitives.WriteUInt32LittleEndian(hello, accid);
        hash.CopyTo(hello, 4);
        await tls.WriteAsync(hello);
        await tls.FlushAsync();
        return tls;
    }

    private static async Task<byte[]> ReadExactly(SslStream s, int n)
    {
        var buf = new byte[n];
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await s.ReadExactlyAsync(buf, cts.Token);
            return buf;
        }
        catch (Exception) { return null; }
    }

    private sealed class Reply
    {
        public byte[] Open;  // the 24-byte ProfOpenAns, null when the connection closed first
        public byte[] Head;  // the 24-byte ProfRecvAns, null when the connection closed without an answer
        public byte[] Body;
        public ProfError Error => (ProfError)Head[1];
        public uint Size => BinaryPrimitives.ReadUInt32LittleEndian(Head.AsSpan(4));
        public byte[] All => Head == null ? null : Head.Concat(Body).ToArray();
    }

    // one profile connection, one request
    private static async Task<Reply> Request(int port, uint accid, ProfRequest request, byte[] body, byte kind = 2, uint? sizeOverride = null)
    {
        body ??= Array.Empty<byte>();
        using var tls = await Connect(port, accid, Hash[accid]);
        var reply = new Reply();
        await tls.WriteAsync(new byte[ProfOpenData.Size]);
        reply.Open = await ReadExactly(tls, ProfOpenAns.Size);
        if (reply.Open == null) return reply;
        var header = new byte[ProfSendReqData.Size];
        header[0] = kind;
        header[1] = (byte)((ushort)request >> 8);
        header[2] = (byte)request;
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), sizeOverride ?? (uint)body.Length);
        await tls.WriteAsync(header);
        await tls.WriteAsync(body);
        await tls.FlushAsync();
        reply.Head = await ReadExactly(tls, ProfRecvAns.Size);
        if (reply.Head == null) return reply;
        reply.Body = reply.Size == 0 ? Array.Empty<byte>() : await ReadExactly(tls, (int)reply.Size);
        return reply;
    }

    // the last u32 is the checksum of the bytes before it
    private static byte[] Seal(byte[] obj)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(obj.AsSpan(obj.Length - 4), ProfileCodec.Checksum(obj.AsSpan(0, obj.Length - 4)));
        return obj;
    }

    private static byte[] FileHead(ulong target, string path, uint offset, uint length)
    {
        var b = new byte[ProfFileHead.Size];
        BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(8), target);
        Encoding.Latin1.GetBytes(path).CopyTo(b, 16);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(400), offset);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(404), length);
        return b;
    }

    private static byte[] FileRequest(ulong target, string path, uint offset, uint length) =>
        Seal(FileHead(target, path, offset, length).Concat(new byte[8]).ToArray());

    private static byte[] WriteBody(ulong target, string path, uint offset, byte[] data, uint? length = null) =>
        Seal(FileHead(target, path, offset, length ?? (uint)data.Length).Concat(data).Concat(new byte[4]).ToArray());

    private static byte[] SearchPolIdBody(uint charId)
    {
        var b = new byte[SearchPolIdReq.Size];
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(8), ProfileCodec.CharacterKey(charId));
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(12), 1);
        return Seal(b);
    }

    private static byte[] ChangeStatusBody(byte openStat, byte found, byte slot, byte noChange)
    {
        var b = new byte[ChangeMyStatusReq.Size];
        b[16] = 0;
        b[17] = found;
        b[18] = slot;
        b[19] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(20), 1);
        b[22] = openStat;
        b[24] = noChange;
        return Seal(b);
    }

    private static byte[] FriendEntry(byte op, bool black, byte num, byte order, ulong polId, string name)
    {
        var e = new FriendInfo { Op = op, IsBlack = (byte)(black ? 1 : 0), Kind = 1, Num = num, Order = order, PolId = polId };
        Encoding.Latin1.GetBytes(name).CopyTo(e.HandleName);
        return e.Bytes;
    }

    private static byte[] StoreBody(byte[][] entries, uint trailer = 0x12345678, int? countOverride = null)
    {
        var head = new byte[StoreFriendListHead.Size];
        for (int i = 0; i < 200; i++) head[i] = (byte)i;
        for (int i = 0; i < 100; i++) head[200 + i] = (byte)(i + 1);
        BinaryPrimitives.WriteUInt16LittleEndian(head.AsSpan(300), (ushort)(countOverride ?? entries.Length));
        var t = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(t, trailer);
        return Seal(head.Concat(entries.SelectMany(e => e)).Concat(t).ToArray());
    }

    // a message's file name: O/m/ + base64 of its sqPolMessageHeader
    private static string MessagePath(uint sender, uint recipient, string senderName, MessageType type, uint length, bool polpro = false)
    {
        var h = new byte[SqPolMessageHeader.Size];
        BinaryPrimitives.WriteUInt64LittleEndian(h.AsSpan(0), sender ^ MessagePolIdMask);
        BinaryPrimitives.WriteUInt64LittleEndian(h.AsSpan(8), recipient ^ MessagePolIdMask);
        Encoding.Latin1.GetBytes(senderName).CopyTo(h, 0x10);
        BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(0x30), 7); // Seq
        BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(0x38), length);
        BinaryPrimitives.WriteUInt16LittleEndian(h.AsSpan(0x3E), (ushort)((uint)type << 7 | 1 << 15));
        if (polpro) h[0x42] = 1;
        return "O/m/" + ProfileCodec.Base64Encode(h);
    }

    // a polcore IRC connection
    private sealed class Irc : IDisposable
    {
        public SslStream Tls;
        public readonly Channel<string> Lines = Channel.CreateUnbounded<string>();
        public readonly List<string> BadChecksums = new();

        public static async Task<Irc> Open(int port, uint accid, byte[] hash)
        {
            var irc = new Irc { Tls = await Connect(port, accid, hash) };
            _ = irc.Pump();
            return irc;
        }

        private async Task Pump()
        {
            var acc = new List<byte>();
            var buf = new byte[4096];
            try
            {
                while (true)
                {
                    int n = await Tls.ReadAsync(buf);
                    if (n <= 0) break;
                    acc.AddRange(buf.Take(n));
                    int nl;
                    while ((nl = acc.IndexOf((byte)'\n')) >= 0)
                    {
                        var line = Encoding.Latin1.GetString(acc.GetRange(0, nl + 1).ToArray()).TrimEnd('\r', '\n');
                        acc.RemoveRange(0, nl + 1);
                        var msg = line.Substring(0, line.Length - 4);
                        if (ProfileCodec.LineChecksum(msg) != line.Substring(line.Length - 4)) BadChecksums.Add(line);
                        Lines.Writer.TryWrite(msg);
                    }
                }
            }
            catch (Exception) { }
            Lines.Writer.TryComplete();
        }

        // null on timeout, "<closed>" when the server closed the connection
        public async Task<string> Next(int ms = 5000)
        {
            using var cts = new CancellationTokenSource(ms);
            try { return await Lines.Reader.ReadAsync(cts.Token); }
            catch (OperationCanceledException) { return null; }
            catch (ChannelClosedException) { return "<closed>"; }
        }

        public async Task Send(string message, bool goodChecksum = true)
        {
            string sum = goodChecksum ? ProfileCodec.LineChecksum(message) : "zzzz";
            await Tls.WriteAsync(Encoding.Latin1.GetBytes(message + sum + "\r\n"));
            await Tls.FlushAsync();
        }

        // NOTICE AUTH, USER -> 300, NICK -> 422 and the profile available notice
        public async Task<List<string>> SignOn(uint accid)
        {
            var got = new List<string> { await Next() };
            await Send("USER a b c :d");
            got.Add(await Next());
            await Send($"NICK {ProfileCodec.ScrambleNick(accid)}:digest:info");
            got.Add(await Next());
            got.Add(await Next());
            return got;
        }

        public void Dispose() => Tls.Dispose();
    }

    // a friend status notice's fields: (sender pol id, status, active, listIndex, connected, extended payload)
    private static (ulong Sender, byte Status, byte Active, byte ListIndex, byte Connected, byte[] Header, byte[] Payload) ParseNotice(string line)
    {
        int at = line.IndexOf(" :", StringComparison.Ordinal);
        var text = line.Substring(at + 2);
        var header = ProfileCodec.Base64Decode(text.Substring(0, 96));
        var rest = text.Substring(96);
        var payload = ProfileCodec.Base64Decode(rest.PadRight((rest.Length + 3) / 4 * 4, 'T'));
        return (BinaryPrimitives.ReadUInt64LittleEndian(header) ^ MessagePolIdMask, header[0x11], header[0x12], header[0x1C], header[0x10], header, payload);
    }

    // ---------------------------------------------------------------- codec

    private static void Codec()
    {
        Check("checksum: words + tail shifted from the top", 0x04030201u + 0x08070605u + 0x0B0A0900u, ProfileCodec.Checksum(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }));
        var bytes = Enumerable.Range(0, 72).Select(i => (byte)(i * 7)).ToArray();
        var text = ProfileCodec.Base64Encode(bytes);
        Check("base64: 72 bytes -> 96 chars", 96, text.Length);
        CheckTrue("base64: round trip", ProfileCodec.Base64Decode(text).SequenceEqual(bytes));
        Check("base64 padded: '=' becomes the zero digit", "STTT", ProfileCodec.Base64Encode(new byte[] { 4 }));
        Check("base64 unpadded drops it", 2, ProfileCodec.Base64EncodeUnpadded(new byte[] { 4 }).Length);
        CheckTrue("base64 decode: a partial group fails", ProfileCodec.Base64Decode("TST") == null);
        CheckTrue("base64 decode: '=' fails", ProfileCodec.Base64Decode("TS==") == null);
        Check("base32: 25 bytes -> 40 chars", 40, ProfileCodec.Base32Encode(new byte[25]).Length);
        Check("base32: zero bytes are the first digit", new string('N', 8), ProfileCodec.Base32Encode(new byte[5]));
        Check("characterKey 0x123456", 0x12003456u, ProfileCodec.CharacterKey(0x123456));
        Check("characterIdFromKey", 0x123456u, ProfileCodec.CharacterIdFromKey(0x12003456));
        var nick = ProfileCodec.ScrambleNick(1);
        CheckTrue("scrambleNick: U + 8 chars", nick.Length == 9 && nick[0] == 'U');
        Check("lineChecksum: 4 chars in 0x3F..0x7E", true, ProfileCodec.LineChecksum("PONG a").All(c => c >= 0x3F && c <= 0x7E) && ProfileCodec.LineChecksum("PONG a").Length == 4);
    }

    // ---------------------------------------------------------------- the gate

    private static async Task RunAsync(IDatabase db)
    {
        var log = new List<string>();
        using var cert = ProfileEngine.CreateEphemeralCertificate();
        using var engine = new ProfileEngine(db, cert, IPAddress.Loopback, 0, 0, s => { lock (log) log.Add(s); });
        engine.Start();
        int pp = engine.ProfilePort, ip = engine.IrcPort;
        CheckTrue("listening on ephemeral ports", pp != 0 && ip != 0 && pp != 51220 && ip != 51240);

        await Authentication(pp);
        await Characters(pp);
        await Misc(pp);
        await Files(db, pp);
        await IrcAndPresence(db, engine, pp, ip);
        lock (log) CheckTrue("logged sign ons", log.Any(l => l.Contains($"account {A} signed on, udp port slot 3")));
    }

    private static async Task Authentication(int pp)
    {
        // a wrong hash, a banned account and a stale (3h) session of an offline account: closed before the open answer
        var saved = Hash[A];
        Hash[A] = new byte[16];
        var r = await Request(pp, A, ProfRequest.LoadMyStatus, null);
        CheckTrue("wrong session hash: closed without an open answer", r.Open == null);
        Hash[A] = saved;
        r = await Request(pp, Banned, ProfRequest.LoadMyStatus, null);
        CheckTrue("banned account (status & 1 == 0): refused", r.Open == null);
        r = await Request(pp, Stale, ProfRequest.LoadMyStatus, null);
        CheckTrue("session older than 2 hours, not online: refused", r.Open == null);

        r = await Request(pp, A, ProfRequest.LoadMyStatus, null);
        CheckTrue("valid hello: open answer", r.Open != null);
        Check("open answer code 0x81", 0x81, r.Open[0]);
        Check("open answer error 0", 0, r.Open[1]);
        Check("answer code 0x83", 0x83, r.Head[0]);
        Check("answer realIp 0", 0u, BinaryPrimitives.ReadUInt32LittleEndian(r.Head.AsSpan(8)));
    }

    private static async Task Characters(int pp)
    {
        var r = await Request(pp, A, ProfRequest.LoadHandleNameList, null);
        Check("handle list: size count + 1 entry", 8 + 136, r.Body.Length);
        Check("handle list: count 1", 1u, BinaryPrimitives.ReadUInt32LittleEndian(r.Body));
        Check("handle list: id = account id", (ulong)A, BinaryPrimitives.ReadUInt64LittleEndian(r.Body.AsSpan(8 + 8)));
        Check("handle list: the first character's name", "PrfAlpha", Encoding.Latin1.GetString(r.Body, 8 + 16, 8));

        r = await Request(pp, A, ProfRequest.LoadCharacterList, null);
        Check("character list: count 2", 2u, BinaryPrimitives.ReadUInt32LittleEndian(r.Body));
        Check("character list: size", 8 + 2 * 104, r.Body.Length);
        var second = r.Body.AsSpan(8 + 104, 104).ToArray();
        Check("character 2: index/order 1", "1/1", $"{second[0]}/{second[1]}");
        Check("character 2: attached, handle slot 1", "1/1", $"{second[4]}/{second[6]}");
        Check("character 2: code \"00\"", "00", Encoding.Latin1.GetString(second, 10, 2));
        Check("character 2: key", ProfileCodec.CharacterKey(CharA2), BinaryPrimitives.ReadUInt32LittleEndian(second.AsSpan(12)));
        Check("character 2: id", (ulong)CharA2, BinaryPrimitives.ReadUInt64LittleEndian(second.AsSpan(16)));
        Check("character 2: name", "PrfAlphb", Encoding.Latin1.GetString(second, 24, 8));

        r = await Request(pp, A, ProfRequest.SearchPolId, SearchPolIdBody(CharB));
        Check("search pol id (in game): size", 48, r.Body.Length);
        Check("search pol id: found", 1, r.Body[32]);
        Check("search pol id: pol id = owner", (ulong)B, BinaryPrimitives.ReadUInt64LittleEndian(r.Body));
        Check("search pol id: name", "PrfBeta", Encoding.Latin1.GetString(r.Body, 16, 7));
        Check("search pol id: checksum", ProfileCodec.Checksum(r.Body.AsSpan(0, 44)), BinaryPrimitives.ReadUInt32LittleEndian(r.Body.AsSpan(44)));
        r = await Request(pp, A, ProfRequest.SearchPolId, SearchPolIdBody(CharC));
        Check("search pol id (not in game): not found", 0, r.Body[32]);
        Check("search pol id (not in game): error None", ProfError.None, r.Error);
        var bad = SearchPolIdBody(CharB);
        bad[20] ^= 1;
        r = await Request(pp, A, ProfRequest.SearchPolId, bad);
        CheckTrue("search pol id with a bad checksum: closed without an answer", r.Open != null && r.Head == null);
    }

    private static async Task Misc(int pp)
    {
        var r = await Request(pp, A, ProfRequest.LoadGroupList, null);
        Check("group list: an empty count", "8/0", $"{r.Body.Length}/{BinaryPrimitives.ReadUInt32LittleEndian(r.Body)}");
        r = await Request(pp, A, ProfRequest.SecurityToken, null);
        Check("security token: empty ok", "None/0", $"{r.Error}/{r.Size}");
        r = await Request(pp, A, ProfRequest.CreateGroup, null);
        Check("create group: refused", ProfError.Refused, r.Error);
        r = await Request(pp, A, ProfRequest.PolbesSearchData, null);
        Check("polbes search: not found", ProfError.NotFound, r.Error);
        r = await Request(pp, A, (ProfRequest)0x0999, null);
        CheckTrue("unknown request: closed without an answer", r.Open != null && r.Head == null);
        r = await Request(pp, A, ProfRequest.LoadMyStatus, null, kind: 3);
        CheckTrue("request kind != 2: closed", r.Head == null);
        r = await Request(pp, A, ProfRequest.ReadFile, new byte[ProfFileRequest.Size + 1], sizeOverride: ProfFileRequest.Size + 1);
        CheckTrue("oversized body: closed", r.Head == null);

        r = await Request(pp, A, ProfRequest.LoadMyStatus, null);
        Check("my status (offline): size", 128, r.Body.Length);
        Check("my status (offline): OpenStat online+1, receive 1, FFXI", "1/1/1", $"{r.Body[6]}/{r.Body[3]}/{BinaryPrimitives.ReadUInt16LittleEndian(r.Body.AsSpan(4))}");
        Check("my status: checksum", ProfileCodec.Checksum(r.Body.AsSpan(0, 124)), BinaryPrimitives.ReadUInt32LittleEndian(r.Body.AsSpan(124)));

        r = await Request(pp, A, ProfRequest.ChangeMyStatus, ChangeStatusBody(2, 1, 0, 0));
        Check("change status (offline): answer size", 32, r.Body.Length);
        CheckTrue("change status: the md5 of the session hash", r.Body.AsSpan(0, 16).SequenceEqual(MD5.HashData(Hash[A])));
        Check("change status: checksum", ProfileCodec.Checksum(r.Body.AsSpan(0, 28)), BinaryPrimitives.ReadUInt32LittleEndian(r.Body.AsSpan(28)));
    }

    private static async Task Files(IDatabase db, int pp)
    {
        var data = Enumerable.Range(0, 300).Select(i => (byte)i).ToArray();
        var r = await Request(pp, A, ProfRequest.WriteFile, WriteBody(0, "prf/one.dat", 0, data));
        Check("write file: ok", "None/0", $"{r.Error}/{r.Size}");
        r = await Request(pp, A, ProfRequest.WriteFile, WriteBody(A, "prf/one.dat", 300, new byte[] { 0xAA, 0xBB }));
        Check("append at the stored length (own pol id): ok", ProfError.None, r.Error);
        r = await Request(pp, A, ProfRequest.WriteFile, WriteBody(0, "prf/one.dat", 5, new byte[] { 1 }));
        Check("append at a wrong offset: refused", ProfError.Refused, r.Error);
        r = await Request(pp, A, ProfRequest.WriteFile, WriteBody(0, "O/m/x", 0, new byte[] { 1 }));
        Check("own write into the mailbox: refused", ProfError.Refused, r.Error);
        r = await Request(pp, A, ProfRequest.WriteFile, WriteBody(0, new string('p', 256), 0, new byte[] { 1 }));
        Check("a path that does not fit a list entry: refused", ProfError.Refused, r.Error);
        var badSum = WriteBody(0, "prf/two.dat", 0, data);
        badSum[^1] ^= 1;
        r = await Request(pp, A, ProfRequest.WriteFile, badSum);
        CheckTrue("write with a bad checksum: closed", r.Head == null);
        await Request(pp, A, ProfRequest.WriteFile, WriteBody(0, "prf/two.dat", 0, new byte[] { 9, 8, 7 }));

        var rs = Sql(db, "SELECT LENGTH(data) AS n FROM accounts_files WHERE accid = ? AND path = ?", A, Encoding.Latin1.GetBytes("prf/one.dat"));
        CheckTrue("stored length 302", rs.Next() && rs.Get<uint>("n") == 302);

        r = await Request(pp, A, ProfRequest.ReadFile, FileRequest(0, "prf/one.dat", 0, 0x10000));
        Check("read file: data + checksum", 306, r.Body.Length);
        CheckTrue("read file: the bytes", r.Body.Take(300).SequenceEqual(data) && r.Body[300] == 0xAA && r.Body[301] == 0xBB);
        Check("read file: trailing checksum", ProfileCodec.Checksum(r.Body.AsSpan(0, 302)), BinaryPrimitives.ReadUInt32LittleEndian(r.Body.AsSpan(302)));
        r = await Request(pp, A, ProfRequest.ReadFile, FileRequest(0, "prf/one.dat", 298, 3));
        CheckTrue("read file at an offset, a length", r.Body.Length == 7 && r.Body[0] == 298 % 256 && r.Body[2] == 0xAA);
        r = await Request(pp, A, ProfRequest.ReadFile, FileRequest(0, "prf/one.dat", 303, 3));
        Check("read past the end: not found", ProfError.NotFound, r.Error);
        r = await Request(pp, A, ProfRequest.ReadFile, FileRequest(0, "prf/none.dat", 0, 3));
        Check("read a missing file: not found", ProfError.NotFound, r.Error);
        r = await Request(pp, A, ProfRequest.ReadFile, FileRequest(B, "prf/one.dat", 0, 3));
        Check("read another account's file: not found", ProfError.NotFound, r.Error);

        r = await Request(pp, A, ProfRequest.GetFileList, FileRequest(0, "prf/", 0, 0));
        Check("file count (max entries 0): count only", "8/2", $"{r.Body.Length}/{BinaryPrimitives.ReadUInt32LittleEndian(r.Body)}");
        r = await Request(pp, A, ProfRequest.GetFileList, FileRequest(0, "prf/", 10, 0));
        Check("file list: count + 2 entries + checksum", 8 + 2 * 264 + 4, r.Body.Length);
        var names = new[] { 0, 1 }.Select(i => Encoding.Latin1.GetString(r.Body, 8 + i * 264 + 8, 7)).OrderBy(s => s).ToArray();
        Check("file list: names relative to the directory", "one.dat,two.dat", string.Join(",", names));
        r = await Request(pp, A, ProfRequest.GetFileList, FileRequest(0, "none/", 10, 0));
        Check("empty file list: no checksum", 8, r.Body.Length);
        r = await Request(pp, A, ProfRequest.GetFileList, FileRequest(0, "O/m/", 10, 0));
        Check("empty message list: count + checksum", 12, r.Body.Length);
        r = await Request(pp, A, ProfRequest.GetFileList, FileRequest(B, "prf/", 10, 0));
        Check("another account's list: refused", ProfError.Refused, r.Error);

        var del = FileHead(0, "prf/two.dat", 0, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(del.AsSpan(404), ProfileCodec.Checksum(del.AsSpan(0, 404)));
        r = await Request(pp, A, ProfRequest.RemoveFile, del);
        Check("delete file (checksum in the length field): ok", ProfError.None, r.Error);
        r = await Request(pp, A, ProfRequest.GetFileList, FileRequest(0, "prf/", 0, 0));
        Check("one file left", 1u, BinaryPrimitives.ReadUInt32LittleEndian(r.Body));
    }

    private static async Task IrcAndPresence(IDatabase db, ProfileEngine engine, int pp, int ip)
    {
        // ---- sign on
        using var ircA = await Irc.Open(ip, A, Hash[A]);
        var got = await ircA.SignOn(A);
        CheckTrue("irc: NOTICE AUTH challenge first", got[0] != null && got[0].StartsWith(":srv NOTICE AUTH ") && got[0].Length == ":srv NOTICE AUTH ".Length + 40);
        Check("irc: USER -> 300 with 32 zero bytes", ":srv 300 * " + ProfileCodec.Base64Encode(new byte[32]), got[1]);
        string nickA = ProfileCodec.ScrambleNick(A);
        Check("irc: NICK -> 422", $":srv 422 {nickA} :no motd", got[2]);
        Check("irc: the profile available notice (udp slot 3)", $":{ProfileCodec.ScrambleNick(1)}!p@pol NOTICE {nickA} :{ProfileNotices.ProfileAvailable(3)}", got[3]);
        var avail = ParseNotice(got[3]);
        Check("profile available: udp slot at 0x22", 3, BinaryPrimitives.ReadUInt16LittleEndian(avail.Header.AsSpan(0x22)));
        Check("profile available: connected 1, legacy block 1, list index 0xFF", "1/1/255", $"{avail.Connected}/{avail.Header[0x1B]}/{avail.ListIndex}");
        Check("irc lines carry good checksums", 0, ircA.BadChecksums.Count);
        lock (engine.Presence.Sync) CheckTrue("A online", engine.Presence.IsOnline(A));

        await ircA.Send("PING :abc");
        Check("PING -> PONG a", "PONG a", await ircA.Next());
        await ircA.Send("PING :abc", goodChecksum: false);
        Check("a line with a bad checksum is dropped", null, await ircA.Next(500));

        // ---- an online account's stale hash is accepted
        Sql(db, "UPDATE accounts_profile SET refreshed = NOW() - INTERVAL 3 HOUR WHERE accid = ?", A);
        var r = await Request(pp, A, ProfRequest.LoadMyStatus, null);
        CheckTrue("stale hash while online: accepted", r.Head != null);
        Check("my status while online: online", 1, r.Body[6]);

        // ---- friends: A lists B (pending), A asks, B lists A, B accepts
        r = await Request(pp, A, ProfRequest.StoreFriendList, StoreBody(new[]
        {
            FriendEntry(FriendInfo.OpSet, false, 0, 5, B, "PrfBeta"),
            FriendEntry(FriendInfo.OpSet, false, 200, 0, C, "PrfGamma"), // index past the friend list
            FriendEntry(FriendInfo.OpSet, false, 1, 0, A, "PrfAlpha"),   // itself
            FriendEntry(FriendInfo.OpSet, false, 2, 0, 0x1_0000_0000ul | C, "PrfGamma"), // pol id over 32 bits
            FriendEntry(FriendInfo.OpSet, false, 3, 0, 996048, "Nobody"), // no such account
        }));
        Check("store friend list: count + 5 entries + trailer", 8 + 5 * 168 + 8, r.Body.Length);
        var e0 = new FriendInfo(r.Body.AsSpan(8, 168));
        Check("entry 0 applied, pending, handle id = account", $"0/1/{B}/0", $"{e0.Result}/{e0.BTemporary}/{e0.HandleId}/{e0.HandleNumber}");
        Check("bad entries refused (result 1)", "1,1,1,1", string.Join(",", Enumerable.Range(1, 4).Select(i => r.Body[8 + i * 168 + 10])));
        Check("trailer echoes unused00", 0x12345678u, BinaryPrimitives.ReadUInt32LittleEndian(r.Body.AsSpan(r.Body.Length - 8)));
        Check("trailer checksum", ProfileCodec.Checksum(r.Body.AsSpan(0, r.Body.Length - 4)), BinaryPrimitives.ReadUInt32LittleEndian(r.Body.AsSpan(r.Body.Length - 4)));
        var row = Sql(db, "SELECT display_order, name, pending FROM accounts_friends WHERE accid = ? AND blacklist = 0 AND list_index = 0", A);
        CheckTrue("row stored with the head's display order (0) and pending", row.Next() && row.Get<byte>("display_order") == 0 && row.Get<byte>("pending") == 1 &&
                                                                              Encoding.Latin1.GetString(row.GetBlobBytes("name")) == "PrfBeta");
        r = await Request(pp, A, ProfRequest.StoreFriendList, StoreBody(new[] { FriendEntry(1, false, 0, 0, B, "x") }, countOverride: 2));
        CheckTrue("store with a wrong count: closed", r.Head == null);

        using var ircB = await Irc.Open(ip, B, Hash[B]);
        await ircB.SignOn(B);

        // A asks B
        var request = Encoding.Latin1.GetBytes("Be my friend");
        var path = MessagePath(A, B, "PrfAlpha", MessageType.FriendRequest, (uint)request.Length);
        r = await Request(pp, A, ProfRequest.WriteFile, WriteBody(B, path, 0, request));
        Check("friend request message: ok", ProfError.None, r.Error);
        var notice = await ircB.Next();
        Check("B notified of the message", $":{nickA}!p@pol NOTICE {ProfileCodec.ScrambleNick(B)} :{ProfileNotices.MessageNotice(path.Substring(4))}", notice);
        r = await Request(pp, B, ProfRequest.GetFileList, FileRequest(0, "O/m/", 10, 0));
        Check("B's message list: 1 message", 1u, BinaryPrimitives.ReadUInt32LittleEndian(r.Body));
        Check("B's message list: the name", path.Substring(4), Encoding.Latin1.GetString(r.Body, 16, 96));
        r = await Request(pp, B, ProfRequest.ReadFile, FileRequest(0, path, 0, 1024));
        CheckTrue("B reads the message", r.Body.Take(request.Length).SequenceEqual(request));

        // refused messages
        r = await Request(pp, A, ProfRequest.WriteFile, WriteBody(B, "x/y", 0, request));
        Check("a write to another account outside the mailbox: refused", ProfError.Refused, r.Error);
        r = await Request(pp, A, ProfRequest.WriteFile, WriteBody(B, MessagePath(A, B, "PrfBeta", MessageType.Plain, (uint)request.Length), 0, request));
        Check("a message signed with someone else's name: refused", ProfError.Refused, r.Error);
        r = await Request(pp, A, ProfRequest.WriteFile, WriteBody(B, MessagePath(A, B, "PrfAlpha", MessageType.Plain, 3), 0, request));
        Check("a message whose header length disagrees: refused", ProfError.Refused, r.Error);
        r = await Request(pp, A, ProfRequest.WriteFile, WriteBody(B, MessagePath(A, B, "PrfAlpha", MessageType.FriendStatus, (uint)request.Length), 0, request));
        Check("a friend status notice from a client: refused", ProfError.Refused, r.Error);
        r = await Request(pp, A, ProfRequest.WriteFile, WriteBody(B, MessagePath(A, B, "PrfAlpha", MessageType.Plain, (uint)request.Length, polpro: true), 0, request));
        Check("an online notice from a client: refused", ProfError.Refused, r.Error);
        r = await Request(pp, A, ProfRequest.WriteFile, WriteBody(C, MessagePath(A, B, "PrfAlpha", MessageType.Plain, (uint)request.Length), 0, request));
        Check("a message to an account other than the header's: refused", ProfError.Refused, r.Error);
        r = await Request(pp, A, ProfRequest.WriteFile, WriteBody(B, path, 0, request));
        Check("the same message twice: refused (INSERT IGNORE)", ProfError.Refused, r.Error);

        // B lists A, then accepts
        r = await Request(pp, B, ProfRequest.StoreFriendList, StoreBody(new[] { FriendEntry(FriendInfo.OpSet, false, 4, 0, A, "PrfAlpha") }));
        Check("B lists A: pending too", 1, new FriendInfo(r.Body.AsSpan(8, 168)).BTemporary);
        var accept = Encoding.Latin1.GetBytes("ok");
        var acceptPath = MessagePath(B, A, "PrfBeta", MessageType.FriendAccepted, (uint)accept.Length);
        r = await Request(pp, B, ProfRequest.WriteFile, WriteBody(A, acceptPath, 0, accept));
        Check("friend accepted message: ok", ProfError.None, r.Error);
        var pend = Sql(db, "SELECT SUM(pending) AS p FROM accounts_friends WHERE (accid = ? AND friend_accid = ?) OR (accid = ? AND friend_accid = ?)", A, B, B, A);
        CheckTrue("both rows no longer pending", pend.Next() && pend.Get<uint>("p") == 0);

        // introduce: A sees B (online, B not in game for presence), B sees A; then the message notice
        var lineA = await ircA.Next();
        var nA = ParseNotice(lineA);
        Check("A gets B's status: sender B, online (2), list index 0, connected", $"{B}/2/0/2", $"{nA.Sender}/{nA.Status}/{nA.ListIndex}/{nA.Connected}");
        var lineB = await ircB.Next();
        var nB = ParseNotice(lineB);
        Check("B gets A's status: sender A, online, B's index 4", $"{A}/2/4", $"{nB.Sender}/{nB.Status}/{nB.ListIndex}");
        Check("A then gets the accepted message notice", $":{ProfileCodec.ScrambleNick(B)}!p@pol NOTICE {nickA} :{ProfileNotices.MessageNotice(acceptPath.Substring(4))}", await ircA.Next());

        r = await Request(pp, A, ProfRequest.LoadFriendList, null);
        Check("A's friend list: one entry", 1u, BinaryPrimitives.ReadUInt32LittleEndian(r.Body));
        var le = new FriendInfo(r.Body.AsSpan(8, 168));
        Check("load: op 1, kind 1, group 0xE, not pending, pol id B", $"1/1/14/0/{B}/{B}", $"{le.Op}/{le.Kind}/{le.Group}/{le.BTemporary}/{le.PolId}/{le.HandleId}");
        var sent = ParseNotice(await ircA.Next());
        Check("after the list, A gets its friends' statuses", $"{B}/2", $"{sent.Sender}/{sent.Status}");

        // ---- status changes reach friends: B goes in game, then away
        r = await Request(pp, B, ProfRequest.ChangeMyStatus, ChangeStatusBody(0, 1, 0, 0));
        Check("B in game: ok", ProfError.None, r.Error);
        var ig = ParseNotice(await ircA.Next());
        Check("A sees B playing", "2/1", $"{ig.Status}/{ig.Active}");
        Check("the notice carries B's character", (ulong)CharB, BinaryPrimitives.ReadUInt64LittleEndian(ig.Payload.AsSpan(8 + 8)));
        Check("extended flags 0x09", 9, ig.Payload[0]);
        r = await Request(pp, B, ProfRequest.ChangeMyStatus, ChangeStatusBody(2, 1, 0, 0));
        var away = ParseNotice(await ircA.Next());
        Check("A sees B away (3)", 3, away.Status);
        var os = Sql(db, "SELECT open_status FROM accounts_profile WHERE accid = ?", B);
        CheckTrue("B's open status saved (away)", os.Next() && os.Get<byte>("open_status") == 1);
        r = await Request(pp, B, ProfRequest.LoadMyStatus, null);
        Check("B's status: away+1, in game", "2/1", $"{r.Body[6]}/{r.Body[1]}");
        r = await Request(pp, B, ProfRequest.ChangeMyStatus, ChangeStatusBody(2, 1, 0, 1));
        Check("keep-alive (no change): no notice", null, await ircA.Next(500));
        r = await Request(pp, B, ProfRequest.ChangeMyStatus, ChangeStatusBody(5, 1, 0, 0));
        Check("invisible: A sees B offline", 1, ParseNotice(await ircA.Next()).Status);
        r = await Request(pp, B, ProfRequest.ChangeMyStatus, ChangeStatusBody(1, 0, 0, 0));
        Check("back online", 2, ParseNotice(await ircA.Next()).Status);

        // ---- B blocks A: A's messages are dropped silently, and A loses B's presence
        r = await Request(pp, B, ProfRequest.StoreFriendList, StoreBody(new[] { FriendEntry(FriendInfo.OpSet, true, 0, 0, A, "PrfAlpha") }));
        Check("B blocks A: ok, not pending", "0/0", $"{r.Body[8 + 10]}/{new FriendInfo(r.Body.AsSpan(8, 168)).BTemporary}");
        var hidden = ParseNotice(await ircA.Next());
        Check("A sees B offline (hide)", $"{B}/1/0", $"{hidden.Sender}/{hidden.Status}/{hidden.Connected}");
        var plain = Encoding.Latin1.GetBytes("hello");
        r = await Request(pp, A, ProfRequest.WriteFile, WriteBody(B, MessagePath(A, B, "PrfAlpha", MessageType.Plain, (uint)plain.Length), 0, plain));
        Check("a blocked sender is told it went through", ProfError.None, r.Error);
        var count = Sql(db, "SELECT COUNT(*) AS n FROM accounts_messages WHERE accid = ? AND sender_accid = ?", B, A);
        CheckTrue("but only the friend request is stored", count.Next() && count.Get<uint>("n") == 1);
        r = await Request(pp, B, ProfRequest.StoreFriendList, StoreBody(new[] { FriendEntry(FriendInfo.OpDelete, true, 0, 0, 0, "") }));
        Check("B unblocks A (delete op)", 0, r.Body[8 + 10]);
        var back = ParseNotice(await ircA.Next());
        Check("A sees B again", $"{B}/2", $"{back.Sender}/{back.Status}");
        var backB = ParseNotice(await ircB.Next());
        Check("and B sees A again", $"{A}/2/4", $"{backB.Sender}/{backB.Status}/{backB.ListIndex}");

        // ---- a second IRC connection for A kicks the first; signing off tells B
        using (var ircA2 = await Irc.Open(ip, A, Hash[A]))
        {
            await ircA2.SignOn(A);
            string rest;
            do rest = await ircA.Next(); while (rest != null && rest != "<closed>");
            Check("the first connection is closed", "<closed>", rest);
            var again = ParseNotice(await ircB.Next());
            Check("B sees A sign on again", $"{A}/2", $"{again.Sender}/{again.Status}");
            await ircA2.Send("QUIT :bye");
            Check("QUIT closes", "<closed>", await ircA2.Next());
        }
        var off = ParseNotice(await ircB.Next());
        Check("B sees A sign off (offline, not connected)", $"{A}/1/0", $"{off.Sender}/{off.Status}/{off.Connected}");
        lock (engine.Presence.Sync) CheckTrue("A offline", !engine.Presence.IsOnline(A));
        var fresh = Sql(db, "SELECT refreshed > NOW() - INTERVAL 1 MINUTE AS f FROM accounts_profile WHERE accid = ?", A);
        CheckTrue("sign off refreshed A's credential", fresh.Next() && fresh.Get<bool>("f"));
        CheckTrue("no bad checksums from the server", ircA.BadChecksums.Count == 0 && ircB.BadChecksums.Count == 0);

        // ---- an IRC connection that never signs on is dropped at the login deadline (30s): only the NICK-less path
        using var idle = await Irc.Open(ip, C, Hash[C]);
        CheckTrue("C: NOTICE AUTH", (await idle.Next())?.StartsWith(":srv NOTICE AUTH") == true);
        await idle.Send("NICK Uxxxxxxxx:a:b");
        Check("NICK before USER is ignored", null, await idle.Next(500));
    }

    // ---------------------------------------------------------------- golden: the same requests to the C++ server

    private static async Task Golden(IDatabase db, int cppProfile, int cppIrc)
    {
        using var cert = ProfileEngine.CreateEphemeralCertificate();
        using var engine = new ProfileEngine(db, cert, IPAddress.Loopback, 0, 0, _ => { });
        engine.Start();
        int pp = engine.ProfilePort;

        async Task Same(string name, uint accid, ProfRequest request, byte[] body)
        {
            var cs = await Request(pp, accid, request, body);
            var cpp = await Request(cppProfile, accid, request, body);
            string Show(Reply r) => r.Open == null ? "<no open>" : r.Head == null ? "<closed>" : Convert.ToHexString(r.All);
            Check($"golden {name}", Show(cpp), Show(cs));
        }

        // seed the files and friends both servers read
        await Request(pp, A, ProfRequest.WriteFile, WriteBody(0, "prf/one.dat", 0, Enumerable.Range(0, 300).Select(i => (byte)i).ToArray()));
        await Request(pp, A, ProfRequest.WriteFile, WriteBody(0, "prf/two.dat", 0, new byte[] { 1, 2, 3 }));
        Sql(db, "UPDATE accounts_files SET updated = '2026-01-02 03:04:05' WHERE accid = ?", A);
        Sql(db, "INSERT INTO accounts_friends(accid, blacklist, list_index, friend_accid, display_order, name, pending) VALUES (?, 0, 3, ?, 7, ?, 0), (?, 1, 2, ?, 1, ?, 1)",
            A, B, Encoding.Latin1.GetBytes("PrfBeta"), A, C, Encoding.Latin1.GetBytes("PrfGamma"));

        await Same("handle name list", A, ProfRequest.LoadHandleNameList, null);
        await Same("character list", A, ProfRequest.LoadCharacterList, null);
        await Same("character list (one char)", B, ProfRequest.LoadCharacterList, null);
        await Same("search pol id (in game)", A, ProfRequest.SearchPolId, SearchPolIdBody(CharB));
        await Same("search pol id (not in game)", A, ProfRequest.SearchPolId, SearchPolIdBody(CharC));
        var bad = SearchPolIdBody(CharB);
        bad[20] ^= 1;
        await Same("search pol id (bad checksum)", A, ProfRequest.SearchPolId, bad);
        await Same("group list", A, ProfRequest.LoadGroupList, null);
        await Same("security token", A, ProfRequest.SecurityToken, null);
        await Same("create group", A, ProfRequest.CreateGroup, null);
        await Same("polbes search all", A, ProfRequest.PolbesSearchDataAll, null);
        await Same("unknown request", A, (ProfRequest)0x0999, null);
        await Same("my status (offline)", C, ProfRequest.LoadMyStatus, null);
        await Same("change status keep-alive (offline)", A, ProfRequest.ChangeMyStatus, ChangeStatusBody(2, 1, 0, 1));
        await Same("change status (offline)", A, ProfRequest.ChangeMyStatus, ChangeStatusBody(2, 1, 0, 0));
        await Same("read file", A, ProfRequest.ReadFile, FileRequest(0, "prf/one.dat", 0, 0x10000));
        await Same("read file at an offset", A, ProfRequest.ReadFile, FileRequest(0, "prf/one.dat", 290, 4));
        await Same("read file past the end", A, ProfRequest.ReadFile, FileRequest(0, "prf/one.dat", 301, 4));
        await Same("read missing file", A, ProfRequest.ReadFile, FileRequest(0, "prf/no.dat", 0, 4));
        await Same("read another account's file", A, ProfRequest.ReadFile, FileRequest(B, "prf/one.dat", 0, 4));
        await Same("file count", A, ProfRequest.GetFileList, FileRequest(0, "prf/", 0, 0));
        await Same("file list", A, ProfRequest.GetFileList, FileRequest(0, "prf/", 10, 0));
        await Same("file list limit 1", A, ProfRequest.GetFileList, FileRequest(0, "prf/", 1, 0));
        await Same("empty file list", A, ProfRequest.GetFileList, FileRequest(0, "none/", 10, 0));
        await Same("empty message list", A, ProfRequest.GetFileList, FileRequest(0, "O/m/", 10, 0));
        await Same("another account's list", A, ProfRequest.GetFileList, FileRequest(B, "prf/", 10, 0));
        await Same("friend list", A, ProfRequest.LoadFriendList, null);
        await Same("write at a wrong offset", A, ProfRequest.WriteFile, WriteBody(0, "prf/one.dat", 7, new byte[] { 1 }));
        await Same("write into the mailbox", A, ProfRequest.WriteFile, WriteBody(0, "O/m/x", 0, new byte[] { 1 }));
        await Same("write to another account outside the mailbox", A, ProfRequest.WriteFile, WriteBody(B, "x/y", 0, new byte[] { 1 }));
        await Same("message with a forged name", A, ProfRequest.WriteFile,
                   WriteBody(B, MessagePath(A, B, "PrfBeta", MessageType.Plain, 1), 0, new byte[] { 1 }));
        await Same("rewrite (same bytes)", A, ProfRequest.WriteFile, WriteBody(0, "prf/two.dat", 0, new byte[] { 1, 2, 3 }));
        var del = FileHead(0, "prf/none.dat", 0, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(del.AsSpan(404), ProfileCodec.Checksum(del.AsSpan(0, 404)));
        await Same("delete a missing file", A, ProfRequest.RemoveFile, del);
        // the same store twice leaves the same rows, so both answers see the same state
        var store = StoreBody(new[]
        {
            FriendEntry(FriendInfo.OpSet, false, 3, 2, B, "PrfBeta"),
            FriendEntry(FriendInfo.OpSet, false, 250, 0, C, "PrfGamma"),
            FriendEntry(FriendInfo.OpSet, true, 2, 0, C, "PrfGamma"),
            FriendEntry(FriendInfo.OpSet, false, 5, 0, A, "PrfAlpha"),
            FriendEntry(9, false, 6, 0, C, "PrfGamma"),
        });
        await Request(pp, A, ProfRequest.StoreFriendList, store);
        await Same("store friend list", A, ProfRequest.StoreFriendList, store);
        await Same("friend list after the store", A, ProfRequest.LoadFriendList, null);

        // IRC: the lines that don't carry a clock
        async Task<List<string>> IrcLines(int port)
        {
            using var irc = await Irc.Open(port, A, Hash[A]);
            var lines = await irc.SignOn(A);
            await irc.Send("PING :x");
            lines.Add(await irc.Next());
            await irc.Send("QUIT");
            lines.Add(await irc.Next());
            // a time, the address and the port follow: compare the length
            lines[0] = lines[0] == null ? null : lines[0].Substring(0, Math.Min(lines[0].Length, ":srv NOTICE AUTH ".Length)) + lines[0].Length;
            return lines;
        }
        var csLines = await IrcLines(engine.IrcPort);
        var cppLines = await IrcLines(cppIrc);
        for (int i = 0; i < csLines.Count; i++) Check($"golden irc line {i}", cppLines[i], csLines[i]);

        // presence: mutual friends A (B at 3) and B (A at 4); B watches A sign on, go away, go invisible, sign off.
        // The notices' clock (__SystemAgeOfData) is zeroed before comparing.
        Sql(db, "DELETE FROM accounts_friends WHERE accid IN (?, ?)", A, B);
        Sql(db, "INSERT INTO accounts_friends(accid, blacklist, list_index, friend_accid, display_order, name, pending) VALUES (?, 0, 3, ?, 0, ?, 0), (?, 0, 4, ?, 0, ?, 0)",
            A, B, Encoding.Latin1.GetBytes("PrfBeta"), B, A, Encoding.Latin1.GetBytes("PrfAlpha"));
        static string Unclock(string line)
        {
            if (line == null || !line.Contains("!p@pol NOTICE")) return line;
            int at = line.IndexOf(" :", StringComparison.Ordinal) + 2;
            var header = ProfileCodec.Base64Decode(line.Substring(at, 96));
            if (header == null) return line;
            Array.Clear(header, 0x30, 8);
            return line.Substring(0, at) + ProfileCodec.Base64Encode(header) + line.Substring(at + 96);
        }
        async Task<List<string>> PresenceLines(int profilePort, int ircPort)
        {
            var lines = new List<string>();
            using var ircB = await Irc.Open(ircPort, B, Hash[B]);
            await ircB.SignOn(B);
            using (var ircA = await Irc.Open(ircPort, A, Hash[A]))
            {
                await ircA.SignOn(A);
                lines.Add(Unclock(await ircB.Next()));
                var r = await Request(profilePort, A, ProfRequest.ChangeMyStatus, ChangeStatusBody(2, 1, 1, 0));
                lines.Add(r.Head == null ? "<closed>" : Convert.ToHexString(r.All));
                lines.Add(Unclock(await ircB.Next()));
                r = await Request(profilePort, A, ProfRequest.LoadMyStatus, null);
                lines.Add(r.Head == null ? "<closed>" : Convert.ToHexString(r.All));
                await Request(profilePort, A, ProfRequest.ChangeMyStatus, ChangeStatusBody(5, 0, 0, 0));
                lines.Add(Unclock(await ircB.Next()));
                await Request(profilePort, A, ProfRequest.ChangeMyStatus, ChangeStatusBody(1, 0, 0, 0));
                lines.Add(Unclock(await ircB.Next()));
                // a plain message from B to A, noticed to A
                var data = Encoding.Latin1.GetBytes("hi");
                var path = MessagePath(B, A, "PrfBeta", MessageType.Plain, (uint)data.Length);
                r = await Request(profilePort, B, ProfRequest.WriteFile, WriteBody(A, path, 0, data));
                lines.Add(r.Head == null ? "<closed>" : Convert.ToHexString(r.All));
                lines.Add(await ircA.Next());
                Sql(db, "DELETE FROM accounts_messages WHERE accid = ?", A);
                await ircA.Send("QUIT");
                lines.Add(await ircA.Next());
            }
            lines.Add(Unclock(await ircB.Next()));
            // B's friend list sends A's (now offline) status
            var fl = await Request(profilePort, B, ProfRequest.LoadFriendList, null);
            lines.Add(Convert.ToHexString(fl.All));
            lines.Add(Unclock(await ircB.Next(1000)));
            await ircB.Send("QUIT");
            lines.Add(await ircB.Next());
            return lines;
        }
        var csPresence = await PresenceLines(engine.ProfilePort, engine.IrcPort);
        var cppPresence = await PresenceLines(cppProfile, cppIrc);
        for (int i = 0; i < csPresence.Count; i++) Check($"golden presence line {i}", cppPresence[i], csPresence[i]);
        CheckTrue("golden presence: every line arrived", csPresence.All(l => l != null && l != "<closed>" || l == csPresence[^1] || l == csPresence[8]));
        Check("golden presence: A's notices name B's list index 4", 4, ParseNotice(csPresence[0]).ListIndex);
    }
}
