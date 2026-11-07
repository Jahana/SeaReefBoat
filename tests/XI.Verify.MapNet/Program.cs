using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using XI.Crypto;
using XI.Map;

// XI.Verify.MapNet -- map UDP framing + session pipeline (map_networking.cpp / map_session*.cpp port).
//   1. GP_CLI_COMMAND_LOGIN constants vs the MSVC oracle (oracle/map_login_layout_golden.txt)
//   2. framing round trip (compress -> finalize -> decipher -> decompress)
//   3. MapNetwork driven by a scripted client: 0x00A login, encrypted traffic, resend, 0x00B key roll, prev-key
//      0x00B resend, address move, cleanup
//   4. the read-ahead login (MapNetwork.PrefetchLogin / PumpDeferred): the same datagrams give the same replies and
//      dispatches as the in-tick load, held in arrival order until the reads are in; a re-log, a pending session gone
//      and a zone change mid-load; the per-tick budget
//   5. live: signed plaintext 0x00A datagrams to the running C++ xi_map; its log must show the same gate decisions
//      (args: [cppMapPort] [cppMapLog]; skipped when the port is not open)
internal static class Program
{
    private static int _pass, _fail;

    private static void Check(string n, object e, object a)
    {
        bool ok = Equals(e?.ToString(), a?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {n}" + (ok ? "" : $"  expected=[{e}] actual=[{a}]"));
    }

    private sealed class FakeStore : IMapSessionStore
    {
        public readonly Dictionary<uint, (uint Acc, uint[] Key)> Chars = new Dictionary<uint, (uint, uint[])>();
        public readonly List<string> Calls = new List<string>();
        public uint ClientServerAddr;
        public uint GetClientServerAddr(uint charId) => ClientServerAddr;

        public bool TryLoadLogin(uint charId, out uint accountId, out uint[] key5)
        {
            accountId = 0;
            key5 = null;
            if (!Chars.TryGetValue(charId, out var c)) return false;
            accountId = c.Acc;
            key5 = c.Key == null ? null : (uint[])c.Key.Clone();
            return true;
        }

        public bool TryGetSessionServer(uint charId, out uint serverAddr, out uint serverPort)
        {
            serverAddr = 0;
            serverPort = 0;
            return false;
        }

        public void DeleteSession(uint charId) => Calls.Add($"delete {charId}");
        public void UpdateSessionKey(uint charId, uint[] key5) => Calls.Add($"key {charId} {key5[4]:X8}");
        public void MarkZoneOut(uint charId, LogoutState state, ulong zoneIpp) => Calls.Add($"zoneout {charId} {state} {MapIpp.ToString(zoneIpp)}");
    }

    // The retail client's side of the framing.
    private sealed class Client
    {
        public uint[] Key;
        public ushort Seq;       // client packet id
        public ushort ServerAck; // last server packet id seen

        public Blowfish Cipher() => SessionCipher.Create(Key);

        public byte[] Login(uint charId, ushort retry = 0, bool badCheck = false)
        {
            var body = new byte[MapFraming.LoginSize];
            body[0] = 0x0A;
            body[1] = (byte)(MapFraming.LoginSize / 2); // size in words, bit0 = opcode bit 8 (0)
            BitConverter.TryWriteBytes(new Span<byte>(body, 2, 2), (ushort)1);
            BitConverter.TryWriteBytes(new Span<byte>(body, MapFraming.LoginRetryOffset, 2), retry);
            BitConverter.TryWriteBytes(new Span<byte>(body, MapFraming.LoginCharIdOffset, 4), charId);
            Encoding.ASCII.GetBytes("Tester").CopyTo(body, 34);
            BitConverter.TryWriteBytes(new Span<byte>(body, MapFraming.LoginLangOffset, 2), (ushort)2);
            MapFraming.SignLogin(body);
            if (badCheck) body[MapFraming.LoginCheckOffset]++;
            return MapFraming.BuildPlainDatagram(1, 0, body);
        }

        public byte[] Encrypted(params byte[][] subs)
        {
            Seq++;
            var stream = subs.SelectMany(x => x).ToArray();
            var header = new byte[MapFraming.HeaderSize];
            MapFraming.PrepareHeader(header, Seq, ServerAck, 0);
            return MapFraming.Finalize(header, MapFraming.Compress(stream), Cipher());
        }

        // Decrypts a server datagram; null on checksum failure.
        public List<SubPacket> Read(byte[] d, out byte[] plain, Blowfish bf = null)
        {
            plain = (byte[])d.Clone();
            Array.Resize(ref plain, MapFraming.MaxBufferSize);
            if (!MapFraming.Decipher(plain, d.Length, bf ?? Cipher())) return null;
            int n = MapFraming.Decompress(plain, d.Length);
            if (n < 0) return null;
            ServerAck = BitConverter.ToUInt16(plain, 0);
            return MapFraming.SubPackets(plain, n).ToList();
        }
    }

    private static byte[] Sub(ushort type, ushort sync, int size)
    {
        var b = new byte[size];
        var p = new BasicPacket();
        p.SetOpcode(type);
        p.SetSize((uint)size);
        p.SetSequence(sync);
        Array.Copy(p.Buffer, b, 4);
        for (int i = 4; i < size; i++) b[i] = (byte)(i * 7);
        return b;
    }

    // ClientAddress.ClientFacingIp: the map address a client is told (network.ZONE_IP_FROM_CLIENT)
    private static void ClientAddressRule()
    {
        uint lo = 0x0100007F;          // 127.0.0.1
        uint lo2 = 0x0500007F;         // 127.0.0.5
        uint lan = 0x3201A8C0;         // 192.168.1.50
        uint client = 0x0D0C0B0A;      // 10.11.12.13
        Check("rule: off sends zoneip", lo, XI.Common.ClientAddress.ClientFacingIp(lo, client, false));
        Check("rule: on, loopback zone -> client address", client, XI.Common.ClientAddress.ClientFacingIp(lo, client, true));
        Check("rule: on, any 127/8 zone -> client address", client, XI.Common.ClientAddress.ClientFacingIp(lo2, client, true));
        Check("rule: on, routable zone unchanged", lan, XI.Common.ClientAddress.ClientFacingIp(lan, client, true));
        Check("rule: on, no client address -> zoneip", lo, XI.Common.ClientAddress.ClientFacingIp(lo, 0, true));
        Check("rule: IsLoopback 127.0.0.1", true, XI.Common.ClientAddress.IsLoopback(lo));
        Check("rule: IsLoopback 192.168.1.50", false, XI.Common.ClientAddress.IsLoopback(lan));
    }

    private static int Main(string[] args)
    {
        Golden();
        RoundTrip();
        Pipeline();
        ZoneIpFromClient();
        Deferred();
        Socket();
        LinkDead();
        Live(args.Length > 0 ? int.Parse(args[0]) : 54230,
             args.Length > 1 ? args[1] : null);

        Console.WriteLine($"{_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    private static void Golden()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "oracle", "map_login_layout_golden.txt");
        var g = File.ReadAllLines(path).Select(l => l.Split('=')).Where(p => p.Length == 2).ToDictionary(p => p[0], p => int.Parse(p[1]));
        Check("golden sizeof(GP_CLI_COMMAND_LOGIN)", g["sizeof"], MapFraming.LoginSize);
        Check("golden LoginPacketCheck", g["LoginPacketCheck"], MapFraming.LoginCheckOffset);
        Check("golden unknown00 (retry)", g["unknown00"], MapFraming.LoginRetryOffset);
        Check("golden offsetof(unknown01) (sum start)", g["unknown01"], MapFraming.LoginSumOffset);
        Check("golden UniqueNo", g["UniqueNo"], MapFraming.LoginCharIdOffset);
        Check("golden uCliLang", g["uCliLang"], MapFraming.LoginLangOffset);
        Check("golden 0x00B body offset+size", $"{g["LOGOUT.LogoutState"]}/{g["LOGOUT.size"]}", $"4/{4 + S2cLogout.SizeOf}");
        Check("golden 0x00B ip/port", $"{g["LOGOUT.ip"]}/{g["LOGOUT.port"]}", $"{4 + S2cLogout.IpOffset()}/{4 + S2cLogout.PortOffset()}");
    }

    private static void RoundTrip()
    {
        var key = new uint[] { 0x11111111, 0x22222222, 0x33333333, 0x44444444, 0x55555555 };
        var stream = Sub(0x015, 3, 32).Concat(Sub(0x0B5, 3, 20)).ToArray();
        var header = new byte[MapFraming.HeaderSize];
        MapFraming.PrepareHeader(header, 7, 9, 0xAABBCCDD);
        var d = MapFraming.Finalize(header, MapFraming.Compress(stream), SessionCipher.Create(key));

        var buf = new byte[MapFraming.MaxBufferSize];
        d.CopyTo(buf, 0);
        Check("roundtrip header untouched", "7/9/AABBCCDD", $"{BitConverter.ToUInt16(buf, 0)}/{BitConverter.ToUInt16(buf, 2)}/{BitConverter.ToUInt32(buf, 8):X8}");
        Check("roundtrip plaintext checksum fails before decipher", false, MapFraming.ChecksumMatches(buf, d.Length));
        Check("roundtrip decipher ok", true, MapFraming.Decipher(buf, d.Length, SessionCipher.Create(key)));
        int n = MapFraming.Decompress(buf, d.Length);
        // zlib_decompress runs until in_sz bits, which include the 8 flag bits, so it emits trailing junk (C++ does too)
        Check("roundtrip decompressed size covers payload", true, n >= MapFraming.HeaderSize + stream.Length);
        Check("roundtrip payload", Convert.ToHexString(stream), Convert.ToHexString(buf, MapFraming.HeaderSize, stream.Length));
        var subs = MapFraming.SubPackets(buf, n).ToList();
        Check("roundtrip sub-packets", "015:32:3,0B5:20:3", string.Join(",", subs.Select(s => $"{s.Type:X3}:{s.Size}:{s.Sync}")));

        var wrong = new byte[MapFraming.MaxBufferSize];
        d.CopyTo(wrong, 0);
        key[4] += 2;
        Check("roundtrip wrong key rejected", false, MapFraming.Decipher(wrong, d.Length, SessionCipher.Create(key)));
    }

    private static void Pipeline()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var store = new FakeStore();
        var key = new uint[] { 0x0100007F, 0x2B, 0, 0, 0x10 };
        store.Chars[1234] = (77, key);
        var log = new List<string>();
        var sessions = new MapSessionContainer(store, () => now, log.Add);
        var net = new MapNetwork(sessions, store, log.Add) { Timestamp = () => 0x01020304 };
        var dispatched = new List<string>();
        net.Dispatch = (s, t, p) =>
        {
            dispatched.Add($"{t:X3}@{p.GetSequence()}");
            if (t == 0x00A) { s.InZone = true; s.Blowfish.Status = BlowfishStatus.Accepted; } // what the 0x00A handler does
        };

        ulong ipp = MapIpp.Make(0x0100007F, 50000);
        var c = new Client { Key = key };

        Check("0x00A without pending session ignored", null, net.HandleIncoming(c.Login(1234), ipp));
        Check("  logged like C++", true, log.Any(l => l.Contains("no session or pending session")));

        sessions.CreatePendingSession(1234);
        Check("0x00A bad LoginPacketCheck dropped", null, net.HandleIncoming(c.Login(1234, badCheck: true), ipp));
        Check("  pending session kept", 1, sessions.PendingSessions.Count);

        var r1 = net.HandleIncoming(c.Login(1234), ipp);
        var s = sessions.GetSessionByIpp(ipp);
        Check("0x00A promotes pending -> session", "1/0", $"{sessions.Sessions.Count}/{sessions.PendingSessions.Count}");
        Check("  session char/account", "1234/77", $"{s?.CharId}/{s?.AccountId}");
        Check("  0x00A dispatched", "00A@1", string.Join(",", dispatched));
        Check("  reply returned", true, r1 != null);
        var subs = c.Read(r1, out var plain);
        Check("  reply deciphers with session key", true, subs != null);
        Check("  reply header server id / ack / time", "1/1/01020304", $"{BitConverter.ToUInt16(plain, 0)}/{BitConverter.ToUInt16(plain, 2)}/{BitConverter.ToUInt32(plain, 8):X8}");
        Check("  server id advanced", 1, s.ServerPacketId);
        Check("  reply cached for resend", r1.Length, s.ServerPacketSize);

        // queued game packets ride the next reply
        c.Seq = 1; // the 0x00A datagram was client packet 1
        var p = new BasicPacket();
        p.SetOpcode(0x037);
        p.SetSize(0x60);
        s.PacketList.AddLast(p);
        c.ServerAck = 1;
        var r2 = net.HandleIncoming(c.Encrypted(Sub(0x015, 2, 32), Sub(0x0B5, 2, 8)), ipp);
        Check("encrypted datagram dispatched", "00A@1,015@2,0B5@2", string.Join(",", dispatched));
        Check("  hasDecryptedPacket set", true, s.HasDecryptedPacket);
        subs = c.Read(r2, out plain);
        Check("  reply carries queued 0x037 with server seq", "037:96:2", subs == null ? "null" : string.Join(",", subs.Select(x => $"{x.Type:X3}:{x.Size}:{x.Sync}")));
        Check("  packet list drained", 0, s.PacketList.Count);

        // stale sync numbers are ignored
        dispatched.Clear();
        c.ServerAck = 2;
        net.HandleIncoming(c.Encrypted(Sub(0x015, 2, 32), Sub(0x015, 3, 32)), ipp);
        Check("sync <= client_packet_id skipped", "015@3", string.Join(",", dispatched));

        // client missed our last datagram: it acks an old id -> previous datagram resent, ack/time patched
        var before = (byte[])s.ServerPacketData.Clone();
        int beforeLen = s.ServerPacketSize;
        c.ServerAck = 1;
        var r3 = net.HandleIncoming(c.Encrypted(Sub(0x015, 4, 32)), ipp);
        Check("resend: same cipher region", Convert.ToHexString(before, 28, beforeLen - 28), Convert.ToHexString(r3, 28, r3.Length - 28));
        Check("resend: ack patched to client code", c.Seq, BitConverter.ToUInt16(r3, 2));
        Check("resend: u16 time patched", 0x0304, BitConverter.ToUInt16(r3, 8));
        Check("resend: server id not advanced", 3, s.ServerPacketId);

        ClientAddressRule();

        // 0x00B: sent, then key increments and session goes pending zone
        c.ServerAck = 3;
        s.PacketList.AddLast(LogoutPacket.Create(LogoutState.Zonechange, MapIpp.Make(0x0100007F, 54230)));
        var r4 = net.HandleIncoming(c.Encrypted(Sub(0x015, 5, 32)), ipp);
        subs = c.Read(r4, out plain);
        Check("0x00B sent under the old key", "00B", subs == null ? "null" : string.Join(",", subs.Select(x => $"{x.Type:X3}")));
        Check("  key counter +2, pending zone", "00000012/PendingZone", $"{s.Blowfish.Key[4]:X8}/{s.Blowfish.Status}");
        Check("  store saw zoneout + key", "zoneout 1234 Zonechange 127.0.0.1:54230|key 1234 00000012", string.Join("|", store.Calls));
        Check("  char dropped", true, s.Char == null);

        // client never got 0x00B: still on the old key -> 0x00B rebuilt with prev key
        var r5 = net.HandleIncoming(c.Encrypted(Sub(0x015, 6, 32)), ipp);
        subs = r5 == null ? null : c.Read(r5, out plain);
        Check("prev-key datagram -> 0x00B resent with prev key", "00B", subs == null ? "null" : string.Join(",", subs.Select(x => $"{x.Type:X3}")));
        Check("  zone ipp in resent 0x00B", "127.0.0.1:54230", subs == null ? "" : MapIpp.ToString(LogoutPacket.ZoneIpp(new BasicPacket(plain.Skip(subs[0].Offset).Take(0x1FF).ToArray()))));
        Check("  client's 0x00D reply ends the resend", null, net.HandleIncoming(c.Encrypted(Sub(0x00D, 7, 8)), ipp));

        // zone back in from a new port: 0x00A finds the session by char and moves it (key now the incremented one)
        c.Key = (uint[])s.Blowfish.Key.Clone();
        store.Chars[1234] = (77, c.Key);
        ulong ipp2 = MapIpp.Make(0x0100007F, 50001);
        dispatched.Clear();
        var r6 = net.HandleIncoming(c.Login(1234, retry: 1), ipp2);
        Check("0x00A from new address moves the session", "False/True", $"{sessions.GetSessionByIpp(ipp) != null}/{sessions.GetSessionByIpp(ipp2) == s}");
        Check("  ids reset, reply under new key", "True", (c.Read(r6, out _) != null).ToString());
        Check("  second move inside 1s throttled", null, net.HandleIncoming(c.Login(1234, retry: 2), MapIpp.Make(0x0100007F, 50002)));

        // once accepted + decrypted, further 0x00A are refused
        c.ServerAck = 1;
        c.Seq = 1;
        net.HandleIncoming(c.Encrypted(Sub(0x015, 2, 32)), ipp2);
        now = now.AddSeconds(2);
        Check("bound session refuses 0x00A", null, net.HandleIncoming(c.Login(1234, retry: 3), MapIpp.Make(0x0100007F, 50003)));

        // cleanup: session without char past MAX_TIME_LASTUPDATE is closed and accounts_sessions cleared
        s.Char = null;
        store.Calls.Clear();
        sessions.CreatePendingSession(999);
        now = now.AddSeconds(61);
        sessions.CleanupSessions(0, 0, 60);
        Check("cleanup closes stale session + pending", "0/0", $"{sessions.Sessions.Count}/{sessions.PendingSessions.Count}");
        Check("  accounts_sessions deleted", "delete 1234|delete 999", string.Join("|", store.Calls));
    }

    // the reads of a held login: ready when the test says so
    private sealed class FakeReads : IDeferredLogin
    {
        public bool Ready { get; set; }
        public bool Serving;
        public int Served, Disposed;

        public IDisposable Serve()
        {
            Served++;
            Serving = true;
            return new End(() => Serving = false);
        }

        public void Dispose() => Disposed++;

        private sealed class End : IDisposable
        {
            private readonly Action a_;
            public End(Action a) => a_ = a;
            public void Dispose() => a_();
        }
    }

    // one map: what it dispatched and loaded, the replies it sent (to whom, the bytes), the reads it started
    private sealed class Rig
    {
        public readonly MapNetwork Net;
        public readonly MapSessionContainer Sessions;
        public readonly List<string> Events = new List<string>();
        public readonly List<string> Replies = new List<string>();
        public readonly List<FakeReads> Reads = new List<FakeReads>();
        public readonly List<string> Log = new List<string>();
        public readonly FakeStore Store = new FakeStore();

        public Rig(bool deferred, uint[] key, Func<DateTime> now)
        {
            var store = Store;
            store.Chars[1234] = (77, key);
            store.Chars[1235] = (78, key);
            Sessions = new MapSessionContainer(store, now, Log.Add);
            Net = new MapNetwork(Sessions, store, Log.Add) { Timestamp = () => 0x01020304 };
            Net.Dispatch = (s, t, p) =>
            {
                Events.Add($"{s.CharId}:{t:X3}@{p.GetSequence()}");
                if (t == 0x00A) { s.InZone = true; s.Blowfish.Status = BlowfishStatus.Accepted; }
            };
            Net.OnLogin = s =>
            {
                Events.Add($"load {s.CharId}");
                if (Reads.Any(r => r.Serving)) Served.Add(s.CharId);
                s.Char = new object();
            };
            if (deferred)
                Net.PrefetchLogin = id =>
                {
                    var r = new FakeReads();
                    Reads.Add(r);
                    return r;
                };
        }

        public readonly List<uint> Served = new List<uint>();

        public void In(byte[] d, ulong ipp)
        {
            var reply = Net.HandleIncoming(d, ipp);
            if (reply != null) Replies.Add($"{MapIpp.ToString(ipp)} {Convert.ToHexString(reply)}");
        }

        public void Pump(double budget = double.PositiveInfinity) =>
            Net.PumpDeferred((to, reply) => Replies.Add($"{MapIpp.ToString(to)} {Convert.ToHexString(reply)}"), budget);

        public void ReadsIn()
        {
            foreach (var r in Reads) r.Ready = true;
        }
    }

    // network.ZONE_IP_FROM_CLIENT on the map: the 0x00B on the wire carries the client's address, the store and
    // session.ZoneIpp keep the internal one, on the first send and on the prev-key resend
    private static void ZoneIpFromClient()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var store = new FakeStore { ClientServerAddr = 0x0D0C0B0A };
        var key = new uint[] { 0x0100007F, 0x2B, 0, 0, 0x10 };
        store.Chars[4321] = (77, key);
        var log = new List<string>();
        var sessions = new MapSessionContainer(store, () => now, log.Add);
        var net = new MapNetwork(sessions, store, log.Add) { Timestamp = () => 0x01020304, ZoneIpFromClient = true };
        net.Dispatch = (sess, t, pk) =>
        {
            if (t == 0x00A) { sess.InZone = true; sess.Blowfish.Status = BlowfishStatus.Accepted; } // what the 0x00A handler does
        };
        ulong ipp = MapIpp.Make(0x0100007F, 50001);
        var c = new Client { Key = key };

        // 0x00A on a pending session (server packet 1), then one encrypted datagram (server packet 2), so that
        // hasDecryptedPacket is set and a queued 0x00B is no longer held
        sessions.CreatePendingSession(4321);
        net.HandleIncoming(c.Login(4321), ipp);
        var s = sessions.GetSessionByIpp(ipp);
        Check("zone ip: 0x00A loaded the client address", 0x0D0C0B0Au, s.ClientServerIp);
        c.Seq = 1;
        c.ServerAck = 1;
        net.HandleIncoming(c.Encrypted(Sub(0x015, 2, 32)), ipp);

        c.ServerAck = 2;
        s.PacketList.AddLast(LogoutPacket.Create(LogoutState.Zonechange, MapIpp.Make(0x0100007F, 54231)));
        var r4 = net.HandleIncoming(c.Encrypted(Sub(0x015, 3, 32)), ipp);
        var subs = c.Read(r4, out var plain);
        Check("zone ip: 0x00B sent", "00B", subs == null ? "null" : string.Join(",", subs.Select(x => $"{x.Type:X3}")));
        Check("zone ip: 0x00B on the wire", "10.11.12.13:54231",
              MapIpp.ToString(LogoutPacket.ZoneIpp(new BasicPacket(plain.Skip(subs[0].Offset).Take(0x1FF).ToArray()))));
        Check("zone ip: session.ZoneIpp internal", "127.0.0.1:54231", MapIpp.ToString(s.ZoneIpp));
        Check("zone ip: store saw the internal address", true, store.Calls.Any(x => x.StartsWith("zoneout 4321 Zonechange 127.0.0.1:54231")));

        // the client never got it: still on the old key -> 0x00B rebuilt and resent with the previous key
        var r5 = net.HandleIncoming(c.Encrypted(Sub(0x015, 4, 32)), ipp);
        subs = c.Read(r5, out plain);
        Check("zone ip: resent 0x00B on the wire", "10.11.12.13:54231",
              MapIpp.ToString(LogoutPacket.ZoneIpp(new BasicPacket(plain.Skip(subs[0].Offset).Take(0x1FF).ToArray()))));

        // a routable zone ip is sent unchanged
        Check("zone ip: routable zone untouched", "192.168.1.50:54230", ApplyTo(MapIpp.Make(0x3201A8C0, 54230), 0x0D0C0B0A, true));
        // setting off: unchanged
        Check("zone ip: off untouched", "127.0.0.1:54230", ApplyTo(MapIpp.Make(0x0100007F, 54230), 0x0D0C0B0A, false));
    }

    private static string ApplyTo(ulong ipp, uint client, bool on)
    {
        var p = LogoutPacket.Create(LogoutState.Zonechange, ipp);
        LogoutPacket.ApplyClientAddress(new Span<byte>(p.Buffer, 0, (int)p.GetSize()), client, on);
        return MapIpp.ToString(LogoutPacket.ZoneIpp(p));
    }

    private static void Deferred()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var key = new uint[] { 0x0100007F, 0x2B, 0, 0, 0x10 };
        ulong ipp = MapIpp.Make(0x0100007F, 50000), ipp2 = MapIpp.Make(0x0100007F, 50001), ipp3 = MapIpp.Make(0x0100007F, 50002);

        // a login, its retry and the first encrypted datagram, all before the reads are in
        var c = new Client { Key = key };
        var login = c.Login(1234);
        var retry = c.Login(1234, retry: 1);
        c.Seq = 1;
        c.ServerAck = 1;
        var walk = c.Encrypted(Sub(0x015, 2, 32));
        var sync = new Rig(false, key, () => now);
        var held = new Rig(true, key, () => now);
        foreach (var r in new[] { sync, held }) r.Sessions.CreatePendingSession(1234);
        foreach (var d in new[] { login, retry, walk }) { sync.In(d, ipp); held.In(d, ipp); }
        Check("held: nothing answered while the reads run", "0/3/0", $"{held.Replies.Count}/{held.Net.DeferredCount}/{held.Events.Count}");
        Check("  one read-ahead for the 0x00A, none for what came behind it", 1, held.Reads.Count);
        Check("  the pending session is untouched", "1/0", $"{held.Sessions.PendingSessions.Count}/{held.Sessions.Sessions.Count}");
        held.Pump();
        Check("  not handled before the reads are in", 3, held.Net.DeferredCount);
        held.ReadsIn();
        held.Pump();
        Check("  then handled in order: the same dispatches and loads", string.Join(",", sync.Events), string.Join(",", held.Events));
        Check("  the same replies, byte for byte", string.Join("|", sync.Replies), string.Join("|", held.Replies));
        Check("  the load ran inside the reads' Serve, which were let go", "1234/1/1", $"{string.Join(",", held.Served)}/{held.Reads[0].Served}/{held.Reads[0].Disposed}");
        Check("  nothing left held", 0, held.Net.DeferredCount);
        var walk2 = c.Encrypted(Sub(0x015, 3, 32));
        sync.In(walk2, ipp);
        held.In(walk2, ipp);
        Check("  later datagrams go straight through", string.Join("|", sync.Replies), string.Join("|", held.Replies));

        // a re-log from another address while the first login's reads run: each 0x00A its own read-ahead, handled in
        // the order they came (the second moves the session the first made)
        c = new Client { Key = key };
        var a1 = c.Login(1235);
        var a2 = c.Login(1235, retry: 1);
        sync = new Rig(false, key, () => now);
        held = new Rig(true, key, () => now);
        foreach (var r in new[] { sync, held })
        {
            r.Sessions.CreatePendingSession(1235);
            r.In(a1, ipp);
            r.In(a2, ipp2);
        }
        Check("re-log: two read-aheads", 2, held.Reads.Count);
        held.Reads[1].Ready = true;
        held.Pump();
        Check("  the second waits for the first", "2/0", $"{held.Net.DeferredCount}/{held.Replies.Count}");
        held.Reads[0].Ready = true;
        held.Pump();
        Check("  then the same as in the tick", string.Join(",", sync.Events) + "|" + string.Join("|", sync.Replies), string.Join(",", held.Events) + "|" + string.Join("|", held.Replies));
        Check("  the session at the second address", "False/True", $"{held.Sessions.GetSessionByIpp(ipp) != null}/{held.Sessions.GetSessionByIpp(ipp2) != null}");
        Check("  both reads let go", "1/1", $"{held.Reads[0].Disposed}/{held.Reads[1].Disposed}");

        // the client goes away and its pending session with it while the reads run: handled as a 0x00A that comes late
        c = new Client { Key = key };
        var late = c.Login(1234);
        sync = new Rig(false, key, () => now);
        held = new Rig(true, key, () => now);
        held.Sessions.CreatePendingSession(1234);
        held.In(late, ipp);
        held.Sessions.DestroyPendingSession(1234);
        held.ReadsIn();
        held.Pump();
        sync.In(late, ipp);
        Check("gone mid-load: no session, no load, no reply", "0/0/0", $"{held.Sessions.Sessions.Count}/{held.Events.Count}/{held.Replies.Count}");
        Check("  logged as the tick would", true, held.Log.Any(l => l.Contains("no session or pending session")) && sync.Log.Any(l => l.Contains("no session or pending session")));
        Check("  the reads let go", 1, held.Reads[0].Disposed);

        // a 0x00A that would not load a char (no session for it) is not held
        held.In(late, ipp3);
        Check("  a 0x00A without a session is not held", "0/1", $"{held.Net.DeferredCount}/{held.Reads.Count}");

        // a zone change in the same process: the 0x00B goes, the client comes back with 0x00A on the same session
        c = new Client { Key = key };
        var zin = c.Login(1234);
        c.Seq = 1;
        c.ServerAck = 1;
        var step = c.Encrypted(Sub(0x015, 2, 32));
        sync = new Rig(false, key, () => now);
        held = new Rig(true, key, () => now);
        foreach (var r in new[] { sync, held })
        {
            r.Sessions.CreatePendingSession(1234);
            r.In(zin, ipp);
            r.ReadsIn();
            r.Pump();
            r.In(step, ipp);
            var s = r.Sessions.GetSessionByIpp(ipp);
            s.PacketList.AddLast(LogoutPacket.Create(LogoutState.Zonechange, MapIpp.Make(0x0100007F, 54230)));
        }
        c.ServerAck = 2;
        var out1 = c.Encrypted(Sub(0x015, 3, 32));
        foreach (var r in new[] { sync, held }) r.In(out1, ipp);
        var back = new Client { Key = (uint[])sync.Sessions.GetSessionByIpp(ipp).Blowfish.Key.Clone() };
        foreach (var r in new[] { sync, held })
        {
            r.Store.Chars[1234] = (77, back.Key);
            r.In(back.Login(1234), ipp);
        }
        Check("zone change: the 0x00A on the pending-zone session is held", "1/2", $"{held.Net.DeferredCount}/{held.Reads.Count}");
        held.ReadsIn();
        held.Pump();
        Check("  then the same as in the tick", string.Join(",", sync.Events) + "|" + string.Join("|", sync.Replies), string.Join(",", held.Events) + "|" + string.Join("|", held.Replies));
        Check("  the char loaded twice, the second inside its reads", "1234,1234", string.Join(",", held.Served));

        // the budget: past it the next held login waits for the next pump (one is always handled)
        held = new Rig(true, key, () => now);
        held.Sessions.CreatePendingSession(1234);
        held.Sessions.CreatePendingSession(1235);
        held.In(new Client { Key = key }.Login(1234), ipp);
        held.In(new Client { Key = key }.Login(1235), ipp2);
        held.ReadsIn();
        held.Pump(budget: 0);
        Check("budget: one login per pump at 0 ms", "1/1", $"{held.Net.DeferredCount}/{held.Replies.Count}");
        held.Pump(budget: 0);
        Check("  the next one on the next", "0/2", $"{held.Net.DeferredCount}/{held.Replies.Count}");
    }

    // cleanupSessions' link-dead half: a session with a char is handed to OnLinkDead at each cleanup, stale once its
    // last update is more than 5 s old (the char flagged), fresh otherwise (the flag cleared); a session without a char
    // is not; at the timeout the char goes to OnCharTimeout after its last stale call
    private static void LinkDead()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var store = new FakeStore();
        var sessions = new MapSessionContainer(store, () => now);
        var calls = new List<string>();
        sessions.OnLinkDead = (s, stale) => calls.Add($"{s.CharId}:{(stale ? "stale" : "fresh")}");
        sessions.OnCharTimeout = (s, otherMap) => calls.Add($"{s.CharId}:timeout");
        var withChar = sessions.CreateSession(MapIpp.Make(0x0100007F, 50100));
        withChar.CharId = 501;
        withChar.Char = new object();
        var noChar = sessions.CreateSession(MapIpp.Make(0x0100007F, 50101));
        noChar.CharId = 502;

        sessions.CleanupSessions(0, 0, 60);
        Check("link dead: an updated session with a char is fresh", "501:fresh", string.Join(",", calls));
        calls.Clear();
        now = now.AddSeconds(5);
        sessions.CleanupSessions(0, 0, 60);
        Check("link dead: 5 s is not yet stale", "501:fresh", string.Join(",", calls));
        calls.Clear();
        now = now.AddSeconds(1);
        sessions.CleanupSessions(0, 0, 60);
        Check("link dead: past 5 s the char's session is stale, the char-less one is not asked", "501:stale",
              string.Join(",", calls));
        calls.Clear();
        withChar.TapLastUpdate(now);
        sessions.CleanupSessions(0, 0, 60);
        Check("link dead: the next update makes it fresh again", "501:fresh", string.Join(",", calls));
        calls.Clear();
        now = now.AddSeconds(61);
        sessions.CleanupSessions(0, 0, 60);
        Check("link dead: at the timeout the stale call comes first", "501:stale,501:timeout", string.Join(",", calls));

        // MapEngine runs cleanupSessions every kSessionCleanupInterval (5 s) from its start: CleanupSessionsIfDue runs
        // it once 5 s have passed since the last run (or the container's creation), and once only after a stall
        var clock = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var paced = new MapSessionContainer(new FakeStore(), () => clock);
        var pacedCalls = new List<string>();
        paced.OnLinkDead = (s, stale) => pacedCalls.Add(stale ? "stale" : "fresh");
        var ps = paced.CreateSession(MapIpp.Make(0x0100007F, 50102));
        ps.CharId = 601;
        ps.Char = new object();
        string Due(int seconds)
        {
            clock = clock.AddSeconds(seconds);
            pacedCalls.Clear();
            bool ran = paced.CleanupSessionsIfDue(0, 0, 60);
            return $"{ran}:{string.Join(",", pacedCalls)}";
        }
        Check("cleanup interval: 4 s after the start, not yet", "False:", Due(4));
        Check("cleanup interval: at 5 s, a cleanup (the session fresh)", "True:fresh", Due(1));
        Check("cleanup interval: 1 s later, none", "False:", Due(1));
        Check("cleanup interval: at 10 s, the next (the session stale)", "True:stale", Due(4));
        Check("cleanup interval: after a 20 s stall, one cleanup", "True:stale", Due(20));
        Check("cleanup interval: and none right after it", "False:", Due(0));
    }

    private static void Socket()
    {
        using var sock = new MapSocket(0, null, IPAddress.Loopback);
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int localPort = ((IPEndPoint)udp.Client.LocalEndPoint).Port;
        udp.Send(new byte[] { 1, 2, 3 }, new IPEndPoint(IPAddress.Loopback, sock.Port));

        ulong from = 0;
        byte[] got = null;
        for (int i = 0; i < 40 && got == null; i++)
        {
            Thread.Sleep(25);
            sock.Drain((d, ipp) => { got = d; from = ipp; });
        }
        Check("socket: datagram drained", "010203", got == null ? "" : Convert.ToHexString(got));
        Check("socket: IPP is network-order ip + port", $"127.0.0.1:{localPort}/{0x0100007F:X8}", $"{MapIpp.ToString(from)}/{MapIpp.Ip(from):X8}");

        sock.Send(from, new byte[] { 9, 8 });
        udp.Client.ReceiveTimeout = 1000;
        var ep = new IPEndPoint(IPAddress.Any, 0);
        Check("socket: send reaches client", "0908", Convert.ToHexString(udp.Receive(ref ep)));
    }

    private static void Live(int port, string cppLog)
    {
        bool open = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners().Any(e => e.Port == port);
        if (!open || cppLog == null || !File.Exists(cppLog))
        {
            Console.WriteLine($"SKIP live C++ xi_map gate (udp {port} open={open}, log={cppLog})");
            return;
        }

        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int localPort = ((IPEndPoint)udp.Client.LocalEndPoint).Port;
        uint charId = 0x7FFF0000u + (uint)Environment.ProcessId % 0xFFFF;
        var c = new Client { Key = new uint[5] };
        var target = new IPEndPoint(IPAddress.Loopback, port);

        long start = new FileInfo(cppLog).Length;
        udp.Send(c.Login(charId, retry: 41, badCheck: true), target); // must be dropped silently
        udp.Send(c.Login(charId, retry: 42), target);                 // passes md5 + size + LoginPacketCheck
        udp.Client.ReceiveTimeout = 1500;
        bool replied;
        try { udp.Receive(ref target); replied = true; } catch (SocketException) { replied = false; }

        string tail = "";
        for (int i = 0; i < 20 && !tail.Contains($"retry 42"); i++)
        {
            Thread.Sleep(250);
            using var fs = new FileStream(cppLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(start, SeekOrigin.Begin);
            tail = new StreamReader(fs).ReadToEnd();
        }

        string expect = $"0x00A for charid {charId} from 127.0.0.1:{localPort} ignored, no session or pending session (retry 42)";
        Check("live C++: signed 0x00A reaches the session lookup (layout/check/md5 agree)", true, tail.Contains(expect));
        Check("live C++: bad LoginPacketCheck rejected before lookup", false, tail.Contains("(retry 41)"));
        Check("live C++: no reply for unknown char", false, replied);

        // the C# port must log the identical decision for the identical bytes
        var log = new List<string>();
        var net = new MapNetwork(new MapSessionContainer(new FakeStore(), null, log.Add), new FakeStore(), log.Add);
        ulong ipp = MapIpp.Make(0x0100007F, (ushort)localPort);
        net.HandleIncoming(c.Login(charId, retry: 41, badCheck: true), ipp);
        net.HandleIncoming(c.Login(charId, retry: 42), ipp);
        Check("live C#: same decision text", "recv_parse: " + expect, string.Join("|", log));
    }
}
