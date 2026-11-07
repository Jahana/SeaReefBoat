using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using XI.Common;
using XI.Crypto;
using XI.Ipc;
using XI.Login;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.World;
using XI.Zone;

// XI.Verify.CrossZone.Live -- live gate for cross-process zoning against the real MariaDB (xidb): two map processes
// (ZoneHost + MapSessionContainer + MapNetwork + MapIpcClient each, as XI.Host map composes them) with different
// --ip/--port, the world's IPC router between them (IpcServer on an in-memory channel), and a scripted client speaking
// the encrypted map protocol (MapFraming + the session key).
//
// For the run, Valley of Sorrows (128) moves to 127.0.0.2:54299 (process B); Cape Teriggan (113) stays on
// 127.0.0.1:54230 (process A). The zone_settings row is restored at the end.
//   1. A: the pending session from the world's CharZone, the login (accounts_sessions updated, no KillSession)
//   2. A: the zone line z352 to Valley of Sorrows: the 0x00B names B, chars / accounts_sessions / the key, the
//      CharZone the world routes to B (and its character cache), B's pending session; A never loads zone 128
//   3. B: the 0x00A with the incremented key, the KillSession routed to A, A's session closed
//   4. back: B -> A by z3k2; B's leftover session timing out leaves accounts_sessions alone (another map owns it)
//   5. a char sent to a process that does not hold its zone: the home point demand, no login
//   6. chars on both processes: Xzonea / Xzonee on A, Xzonec / Xzoned on B (Xzonee holds a linkshell, Xzoned a pearl)
//   7. tells through the world (the recipient's accounts_sessions; "not online" from the world to the sender's process)
//   8. a party across the processes: invite / answer routed by char id, PartyReload to every member's process, the
//      lists each process builds (its own members' numbers, the others' from the database), party chat
//   9. an alliance across them: the trust refusal, AllianceReload, alliance chat
//  10. linkshell chat, message, rank change and kick routed to the members' / the member's process
//  11. unity chat by accounts_sessions.unitychat; yells to the processes holding a yell zone (zone.yaml misc)
//  12. SendToZone's own player cap check (the zone filled up after the zone line), then Xzonea zoning to B: its party
//      and alliance picked up there
//  13. the alliance dissolved and a party kick across the processes (AllianceDissolve, PlayerKick)
//  14. real processes: XI.Host world and two XI.Host map processes (Cape Teriggan on 127.0.0.3:54298, Valley of Sorrows
//      on 127.0.0.2:54299 under --lazy, both rows restored) over ZeroMQ, the test as the lobby's dealer handing a char to
//      A and as its client over UDP: the login on A, the zone line to B (which loads its zone for it), the KillSession
//      back to A
//  15. a --lazy map process at A's address (MapConfig::lazyZones): nothing loaded at the start, the login's zone loaded
//      by EnsureZoneLoaded, a zone line's managed destination by IsZoneReady (the 0x00B naming the process itself), B's
//      zone left alone by a zone line but loaded for a char sent there anyway (no wrong-process check under --lazy)
// 16-18 with zone Lua on both processes, the airship's crossing (224), Lower Jeuno (245) and Leujaoam Sanctum (69) moved
//      to B as well (restored at the end):
//  16. instances: Leujaoam Cleansing B's (instanceutils by its zone's address), refused on A; created on B for a char
//      in Lower Jeuno, which goes in, out to Caedarva Mire on A and back into its run (registered on B); a char sent in
//      from A with no run is put out to Caedarva Mire, which B does not hold (a null zone in the C++): the home point
//  17. the Bastok - Jeuno airship: boarding in Port Bastok on A (TransportDepart, the cutscene, setPos to the crossing),
//      the crossing on B (its voyage there, DisembarkAll at the arrival), ashore in Port Jeuno on A
//  18. the Assist Channel: AssistE / AssistJ to both processes' eligible chars, a thumbs up from A to a mentor on B
//      (AssistChannelEvent, MessageStandard and CharVarUpdate back)
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 995941, StrayId = 995942, QId = 995951, RId = 995952, SId = 995953, FId = 995954;
    private const uint LazyId = 995961, LazyLineId = 995962, LazyStrayId = 995963, BoardId = 995964, InstId = 995965, InstStrayId = 995966,
                       AssistAId = 995967, AssistBId = 995968;
    private const string LsName = "Xzonels";
    private const ushort Teriggan = 113, Valley = 128, Kuftal = 174;
    private const ushort PortBastok = 236, BastokJeunoAirship = 224, LowerJeuno = 245, CaedarvaMire = 79, LeujaoamSanctum = 69;
    private const ushort LeujaoamCleansing = 6900;
    private const string BIp = "127.0.0.2";
    private const ushort BPort = 54299;

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static ResultSet Sql(IDatabase db, string q, params BoundValue[] a) => db.Execute(q, a);

    private static string Q1(IDatabase db, string q, uint charId = CharId)
    {
        var r = Sql(db, q, BoundValue.Of(charId));
        return r != null && r.Next() ? r.Get<string>(0u) : "-";
    }

    // every table with a charid column, then the char and its account
    private static void Cleanup(IDatabase db)
    {
        var tables = new List<string>();
        var r = Sql(db, "SELECT DISTINCT table_name FROM information_schema.columns WHERE table_schema = DATABASE() AND column_name = 'charid' " +
                        "AND table_name <> 'chars'");
        while (r != null && r.Next()) tables.Add(r.Get<string>(0u));
        foreach (var id in new[] { CharId, StrayId, QId, RId, SId, FId, LazyId, LazyLineId, LazyStrayId, BoardId, InstId, InstStrayId,
                                   AssistAId, AssistBId })
        {
            foreach (var t in tables) Sql(db, $"DELETE FROM `{t}` WHERE charid = ?", BoundValue.Of(id));
            Sql(db, "DELETE FROM chars WHERE charid = ?", BoundValue.Of(id));
            Sql(db, "DELETE FROM accounts WHERE id = ?", BoundValue.Of(id));
        }
        Sql(db, "DELETE FROM linkshells WHERE name = ?", BoundValue.Of(LsName));
    }

    private static string FindDataDir()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var cand = Path.Combine(d.FullName, "data");
            if (File.Exists(Path.Combine(cand, "grades.yaml"))) return cand;
        }
        throw new DirectoryNotFoundException("data (grades.yaml) not found above " + AppContext.BaseDirectory);
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

        // the zone_settings row the run moves, restored whatever happens
        var row = Sql(db, "SELECT zoneip, zoneport FROM zone_settings WHERE zoneid = ?", BoundValue.Of(Valley));
        if (row == null || !row.Next()) { Console.WriteLine("zone_settings has no Valley of Sorrows"); return 3; }
        string valleyIp = row.Get<string>("zoneip");
        ushort valleyPort = row.Get<ushort>("zoneport");
        try
        {
            Cleanup(db);
            Sql(db, "UPDATE zone_settings SET zoneip = ?, zoneport = ? WHERE zoneid = ?", BoundValue.Of(BIp), BoundValue.Of(BPort), BoundValue.Of(Valley));
            Run(db);
            RunProcesses(db);
            RunLazy(db);
            RunScripted(db);
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FAIL exception: {ex}");
        }
        finally
        {
            Sql(db, "UPDATE zone_settings SET zoneip = ?, zoneport = ? WHERE zoneid = ?", BoundValue.Of(valleyIp), BoundValue.Of(valleyPort), BoundValue.Of(Valley));
            Cleanup(db);
        }

        Console.WriteLine($"{_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    // The retail client's side of the framing (XI.Verify.MapNet's client): a plaintext 0x00A, then compressed and
    // enciphered datagrams with the session key.
    private sealed class Client
    {
        public uint[] Key;
        public ushort Seq;       // client packet id
        public ushort ServerAck; // last server packet id seen

        private Blowfish Cipher() => SessionCipher.Create(Key);

        public byte[] Login(uint charId)
        {
            Seq = 1;
            ServerAck = 0;
            var body = new byte[MapFraming.LoginSize];
            body[0] = 0x0A;
            body[1] = (byte)(MapFraming.LoginSize / 2);
            BitConverter.TryWriteBytes(new Span<byte>(body, 2, 2), (ushort)1);
            BitConverter.TryWriteBytes(new Span<byte>(body, MapFraming.LoginCharIdOffset, 4), charId);
            Encoding.ASCII.GetBytes("Xzone").CopyTo(body, 34);
            BitConverter.TryWriteBytes(new Span<byte>(body, MapFraming.LoginLangOffset, 2), (ushort)2);
            MapFraming.SignLogin(body);
            return MapFraming.BuildPlainDatagram(1, 0, body);
        }

        // sub-packets (opcode, size, filler) under the next client packet id
        public byte[] Encrypted(params (ushort Type, int Size, Action<byte[]> Fill)[] subs)
        {
            Seq++;
            var stream = new List<byte>();
            foreach (var (type, size, fill) in subs)
            {
                var p = new BasicPacket();
                p.SetOpcode(type);
                p.SetSize((uint)size);
                p.SetSequence(Seq);
                var b = new byte[size];
                Array.Copy(p.Buffer, b, 4);
                fill?.Invoke(b);
                stream.AddRange(b);
            }
            var header = new byte[MapFraming.HeaderSize];
            MapFraming.PrepareHeader(header, Seq, ServerAck, 0);
            return MapFraming.Finalize(header, MapFraming.Compress(stream.ToArray()), Cipher());
        }

        // a server datagram's sub-packets (whole packets), null when it does not decipher with the key
        public List<byte[]> Read(byte[] d)
        {
            if (d == null) return null;
            var plain = (byte[])d.Clone();
            Array.Resize(ref plain, MapFraming.MaxBufferSize);
            if (!MapFraming.Decipher(plain, d.Length, Cipher())) return null;
            int n = MapFraming.Decompress(plain, d.Length);
            if (n < 0) return null;
            ServerAck = BitConverter.ToUInt16(plain, 0);
            return MapFraming.SubPackets(plain, n).Select(s => plain.AsSpan(s.Offset, s.Size).ToArray()).ToList();
        }
    }

    private static int Opcode(byte[] p) => p[0] | (p[1] & 1) << 8;

    // One map process as XI.Host map composes it, its clock under the test's control
    private sealed class MapProc
    {
        public string Name;
        public ulong Ipp;
        public ZoneHost Zone;
        public MapSessionContainer Sessions;
        public MapNetwork Net;
        public MapIpcClient Ipc;
        public StatSettings Cfg;
        public DateTime Now = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        public readonly List<string> Log = new List<string>();

        public Func<string, string> Lua; // a chunk on the process's scripts, what it report()s

        public Ipp WorldIpp => new Ipp(MapIpp.Ip(Ipp), MapIpp.Port(Ipp));
    }

    // A char's client on one of the map processes: its session found by the client's address
    private sealed class Peer
    {
        public uint Id;
        public string Name;
        public MapProc M;
        public Client Cl;
        public ulong From;

        public MapSession S => M.Sessions.GetSessionByIpp(From);
        public CharData C => S?.Char as CharData;
    }

    private static MapProc NewMap(IDatabase db, string dataDir, string name, string ip, ushort port, QueueWorldChannel world)
    {
        var m = new MapProc { Name = name, Ipp = MapIpp.Make(IpText.Str2Ip(ip), port) };
        m.Cfg = new StatSettings();
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), m.Cfg);
        var loader = new CharLoader(db, Grades.Load(dataDir), AbilityTable.Load(db, _ => true), stats);
        m.Zone = new ZoneHost(db, loader, dataDir, true, m.Log.Add) { ProcessIpp = m.Ipp };
        var store = new DbMapSessionStore(db);
        m.Sessions = new MapSessionContainer(store, () => m.Now, m.Log.Add);
        m.Net = new MapNetwork(m.Sessions, store, m.Log.Add)
        {
            OnLogin = m.Zone.OnLogin, Dispatch = m.Zone.Dispatch, AfterParse = m.Zone.AfterParse, OnZoneOut = m.Zone.OnZoneOut,
        };
        m.Sessions.OnCharTimeout = m.Zone.OnCharTimeout;
        m.Ipc = new MapIpcClient(m.Sessions, m.Zone, m.Log.Add);
        // message::send: the dealer to the world router, which knows the process by its routing id
        m.Zone.SendIpc = msg => world.Inject(m.WorldIpp, IpcFraming.ToBytesWithHeader(msg));
        return m;
    }

    // WAR50 logged in through the lobby: its account, char, the session row (and key) the lobby writes
    private static void SeedChar(IDatabase db, byte[] keyBytes, uint id, string name, ushort zone, float x, float y, float z)
    {
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of("xz" + name), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,pos_rot,home_zone,nation,gmlevel) " +
                "VALUES (?,?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(name), BoundValue.Of(zone), BoundValue.Of(zone),
            BoundValue.Of(x), BoundValue.Of(y), BoundValue.Of(z), BoundValue.Of((byte)0), BoundValue.Of((ushort)230),
            BoundValue.Of((byte)0), BoundValue.Of((byte)0));
        // the lobby's row: the address of the char's zone (data_session's zoneip / zoneport)
        var zs = Sql(db, "SELECT zoneip, zoneport FROM zone_settings WHERE zoneid = ?", BoundValue.Of(zone));
        zs.Next();
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,session_key,server_addr,server_port,client_addr) VALUES (?,?,?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(keyBytes), BoundValue.Of(IpText.Str2Ip(zs.Get<string>("zoneip"))),
            BoundValue.Of(zs.Get<ushort>("zoneport")), BoundValue.Of(IpText.Str2Ip("127.0.0.1")));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl, sjob, slvl) VALUES (?, 600, 0, 1, 50, 0, 1)", BoundValue.Of(id));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 50)", BoundValue.Of(id));
    }

    // ---- 14. real processes over ZeroMQ ----------------------------------------------------------------------------------

    // XI.Host.dll as the build left it beside this suite (the same configuration)
    private static string FindHost()
    {
        var baseDir = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        string tfm = baseDir.Name, config = baseDir.Parent?.Name ?? "Debug";
        for (var d = baseDir; d != null; d = d.Parent)
        {
            var cand = Path.Combine(d.FullName, "src", "XI.Host", "bin", config, tfm, "XI.Host.dll");
            if (File.Exists(cand)) return cand;
        }
        return null;
    }

    // an XI.Host process, its output lines as they come
    private sealed class HostProc : IDisposable
    {
        public readonly Process P;
        public readonly ConcurrentQueue<string> Lines = new ConcurrentQueue<string>();

        public HostProc(string dll, string args)
        {
            P = new Process
            {
                StartInfo = new ProcessStartInfo("dotnet", $"\"{dll}\" {args}")
                {
                    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
                },
            };
            P.OutputDataReceived += (_, e) => { if (e.Data != null) Lines.Enqueue(e.Data); };
            P.ErrorDataReceived += (_, e) => { if (e.Data != null) Lines.Enqueue(e.Data); };
            P.Start();
            P.BeginOutputReadLine();
            P.BeginErrorReadLine();
        }

        public bool Saw(string text) => Lines.Any(l => l.Contains(text));

        public void Dispose()
        {
            try { if (!P.HasExited) P.Kill(true); } catch (InvalidOperationException) { }
            P.WaitForExit(5000);
            P.Dispose();
        }
    }

    private static bool WaitFor(Func<bool> cond, int ms)
    {
        var until = DateTime.UtcNow.AddMilliseconds(ms);
        while (DateTime.UtcNow < until)
        {
            if (cond()) return true;
            Thread.Sleep(50);
        }
        return cond();
    }

    private static void RunProcesses(IDatabase db)
    {
        Console.WriteLine("14. XI.Host world + two XI.Host map processes over ZeroMQ, the client over UDP:");
        var host = FindHost();
        Check("XI.Host.dll built beside the suite", true, host != null);
        if (host == null) return;
        string root = Directory.GetParent(FindDataDir()).FullName;
        const string Endpoint = "tcp://127.0.0.1:54913", AIp = "127.0.0.3";
        const ushort APort = 54298;

        // both zones on addresses of their own, so each map process's start (which drops its sessions) touches only these
        var saved = new Dictionary<ushort, (string Ip, ushort Port)>();
        foreach (var z in new[] { Teriggan, Valley })
        {
            var row = Sql(db, "SELECT zoneip, zoneport FROM zone_settings WHERE zoneid = ?", BoundValue.Of(z));
            row.Next();
            saved[z] = (row.Get<string>("zoneip"), row.Get<ushort>("zoneport"));
        }
        var procs = new List<HostProc>();
        UdpClient udp = null;
        ZmqDealerChannel lobby = null;
        try
        {
            Sql(db, "UPDATE zone_settings SET zoneip = ?, zoneport = ? WHERE zoneid = ?", BoundValue.Of(AIp), BoundValue.Of(APort), BoundValue.Of(Teriggan));
            Sql(db, "UPDATE zone_settings SET zoneip = ?, zoneport = ? WHERE zoneid = ?", BoundValue.Of(BIp), BoundValue.Of(BPort), BoundValue.Of(Valley));
            var key = new uint[] { 0x2B7E1516, 0x28AED2A6, 0xABF71588, 0x09CF4F3C, 0x10 };
            var keyBytes = new byte[20];
            for (int i = 0; i < 5; i++) BitConverter.TryWriteBytes(keyBytes.AsSpan(4 * i), key[i]);

            string common = $"root=\"{root}\" db.port=3307 ipc={Endpoint} roe=0";
            var world = new HostProc(host, $"world {common} http=0 dailytally=0");
            procs.Add(world);
            var mapA = new HostProc(host, $"map {common} ip={AIp} port={APort}");
            procs.Add(mapA);
            // B with --lazy: Valley of Sorrows loads when the char logs into it
            var mapB = new HostProc(host, $"map {common} ip={BIp} port={BPort} lazy=1");
            procs.Add(mapB);
            bool up = WaitFor(() => world.Saw("IPC router bound") && mapA.Saw($"map: UDP {APort}") && mapB.Saw($"map: UDP {BPort}"), 90_000);
            Check("the world binds, each map holds its one zone and listens", "True/True/True",
                  $"{up}/{mapA.Saw("Zones assigned to this process: 1")}/{mapB.Saw("Zones assigned to this process: 1")}");
            if (!up)
            {
                foreach (var hp in procs)
                    foreach (var l in hp.Lines.TakeLast(5)) Console.WriteLine("    | " + l);
                return;
            }
            Check("B under --lazy: its zone managed, no ships", "True/False",
                  $"{mapB.Saw("map: lazy zones: 1 managed, loaded on first access")}/{mapB.Saw("transport runs")}");

            // the lobby (data_session) writes the session row (after the maps' start dropped theirs) and hands the char over:
            // CharZone through its dealer
            SeedChar(db, keyBytes, FId, "Xzonef", Teriggan, 239.925f, -3.197f, -180.056f);
            lobby = new ZmqDealerChannel(Endpoint, new Ipp(IpText.Str2Ip("127.0.0.1"), 54229));
            lobby.Send(IpcFraming.ToBytesWithHeader(new CharZone(FId, (ZoneId)Teriggan)));

            udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            udp.Client.ReceiveTimeout = 500;
            byte[] Exchange(string ip, ushort port, byte[] datagram)
            {
                udp.Send(datagram, datagram.Length, new IPEndPoint(IPAddress.Parse(ip), port));
                try
                {
                    var from = new IPEndPoint(IPAddress.Any, 0);
                    return udp.Receive(ref from);
                }
                catch (SocketException) { return null; }
            }
            var client = new Client { Key = (uint[])key.Clone() };
            // the 0x00A until the pending session is there (the CharZone on its way through the world)
            List<byte[]> LogIn(string ip, ushort port)
            {
                List<byte[]> got = null;
                WaitFor(() => (got = client.Read(Exchange(ip, port, client.Login(FId)))) != null, 15_000);
                return got;
            }
            var r = LogIn(AIp, APort);
            Check("A: the 0x00A answered after the lobby's CharZone over ZeroMQ", true, r?.Exists(pk => Opcode(pk) == 0x00A) == true);
            string Session() =>
                Q1(db, "SELECT CONCAT_WS('/', INET_NTOA(CONV(HEX(REVERSE(UNHEX(LPAD(HEX(server_addr), 8, '0')))), 16, 10)), server_port, " +
                       "client_port > 0) FROM accounts_sessions WHERE charid = ?", FId);
            Check("accounts_sessions: A's address, the client's port", $"{AIp}/{APort}/1", Session());
            for (int i = 0; i < 8; i++) client.Read(Exchange(AIp, APort, client.Encrypted((0x00C, 4, null))));

            // the zone line z352: A's loop ticks the zone, which queues the 0x00B; a later datagram's answer carries it
            client.Read(Exchange(AIp, APort, client.Encrypted(((ushort)0x05E, 24, buf => BitConverter.GetBytes(ZoneLine.PackId("z352")).CopyTo(buf, 4)))));
            byte[] logout = null;
            WaitFor(() => (logout = client.Read(Exchange(AIp, APort, client.Encrypted((0x015, 32, null))))?.Find(pk => Opcode(pk) == 0x00B)) != null,
                    10_000);
            Check("A: the 0x00B names B", $"2/{BIp}/{BPort}",
                  logout == null ? "none" : $"{logout[4]}/{IpText.Ip2Str(BitConverter.ToUInt32(logout, 4 + S2cLogout.IpOffset()))}/" +
                                            $"{BitConverter.ToUInt16(logout, 4 + S2cLogout.PortOffset())}");
            client.Key[4] += 2;
            r = LogIn(BIp, BPort);
            Check("B: the 0x00A answered under the new key (A's CharZone routed by the world)", true, r?.Exists(pk => Opcode(pk) == 0x00A) == true);
            Check("chars: Valley of Sorrows from Cape Teriggan; accounts_sessions: B's address", $"128/113 {BIp}/{BPort}/1",
                  $"{Q1(db, "SELECT CONCAT_WS('/', pos_zone, pos_prevzone) FROM chars WHERE charid = ?", FId)} {Session()}");
            Check("A closes its session on B's KillSession", true,
                  WaitFor(() => mapA.Saw($"Closing session of charid {FId} on request of other process"), 10_000));
            Check("B loaded Valley of Sorrows for the login (EnsureZoneLoaded)", true, mapB.Saw("zone: Valley_of_Sorrows loaded"));
        }
        finally
        {
            lobby?.Dispose();
            udp?.Dispose();
            foreach (var hp in procs) hp.Dispose();
            foreach (var (z, (ip, port)) in saved)
                Sql(db, "UPDATE zone_settings SET zoneip = ?, zoneport = ? WHERE zoneid = ?", BoundValue.Of(ip), BoundValue.Of(port), BoundValue.Of(z));
        }
    }

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var chan = new QueueWorldChannel();
        var worldLog = new List<string>();
        var a = NewMap(db, dataDir, "A", "127.0.0.1", 54230, chan);
        var b = NewMap(db, dataDir, "B", BIp, BPort, chan);
        // the world reads zone_settings at its start, with Valley of Sorrows already on B
        var sys = new WorldSystems();
        // (each zone's misc from its zone.yaml, as XI.Host world builds it: the yell / assist endpoints)
        var world = new IpcServer(db, chan, new ZoneSettings(db, ZoneInfo.MiscFlagsLookup(db, Path.Combine(dataDir, "zones"))), sys,
                                  worldLog.Add);
        var loginIpp = new Ipp(IpText.Str2Ip("127.0.0.1"), 54230 - 1);

        // the world's routing: each message to the map process it was sent to; what went where, by message type
        var routed = new List<string>();
        void Pump()
        {
            world.HandleIncomingMessages();
            foreach (var m in chan.Sent)
            {
                var decoded = IpcFraming.Decode(m.Payload);
                var to = m.Ipp.Raw == a.Ipp ? a : m.Ipp.Raw == b.Ipp ? b : null;
                routed.Add($"{decoded.Type}->{to?.Name ?? m.Ipp.ToString()}");
                to?.Ipc.HandleFrame(m.Payload);
            }
            chan.Sent.Clear();
        }
        string Routed()
        {
            var s = string.Join(",", routed);
            routed.Clear();
            return s;
        }

        Console.WriteLine("zone assignment:");
        Check("A holds Cape Teriggan, not Valley of Sorrows", "True/False",
              $"{a.Zone.ZonesAssignedToThisProcess().Contains(Teriggan)}/{a.Zone.ZonesAssignedToThisProcess().Contains(Valley)}");
        Check("B holds Valley of Sorrows only", $"1/True", $"{b.Zone.ZonesAssignedToThisProcess().Count}/{b.Zone.ZonesAssignedToThisProcess().Contains(Valley)}");
        Check("the world routes zone 128 to B, 113 to A", $"{BIp}:{BPort}/127.0.0.1:54230",
              $"{world.GetIppForZoneId(Valley)}/{world.GetIppForZoneId(Teriggan)}");

        // WAR50 at Cape Teriggan's zone line to Valley of Sorrows (z352), logged in through the lobby: its session
        // row and key, the CharZone the lobby sends
        var key = new uint[] { 0x2B7E1516, 0x28AED2A6, 0xABF71588, 0x09CF4F3C, 0x10 };
        var keyBytes = new byte[20];
        for (int i = 0; i < 5; i++) BitConverter.TryWriteBytes(keyBytes.AsSpan(4 * i), key[i]);
        void Seed(uint id, string name, ushort zone, float x, float y, float z) => SeedChar(db, keyBytes, id, name, zone, x, y, z);
        Seed(CharId, "Xzonea", Teriggan, 239.925f, -3.197f, -180.056f);
        string Session(uint id = CharId) =>
            Q1(db, "SELECT CONCAT_WS('/', INET_NTOA(CONV(HEX(REVERSE(UNHEX(LPAD(HEX(server_addr), 8, '0')))), 16, 10)), server_port, client_port, " +
                   "HEX(REVERSE(SUBSTRING(session_key, 17, 4)))) FROM accounts_sessions WHERE charid = ?", id);
        string Pos(uint id = CharId) => Q1(db, "SELECT CONCAT_WS('/', pos_zone, pos_prevzone, ROUND(pos_x, 2), ROUND(pos_y, 3)) FROM chars WHERE charid = ?", id);

        // ---- 1. login on A --------------------------------------------------------------------------------------
        Console.WriteLine("1. the login on A:");
        chan.Inject(loginIpp, IpcFraming.ToBytesWithHeader(new CharZone(CharId, (ZoneId)Teriggan)));
        Pump();
        Check("the lobby's CharZone goes to A", "CharZone->A", Routed());
        Check("A: a pending session; B: none", "1/0", $"{a.Sessions.PendingSessions.Count(s => s.CharId == CharId)}/{b.Sessions.PendingSessions.Count}");

        var client = new Client { Key = (uint[])key.Clone() };
        ulong clientA = MapIpp.Make(IpText.Str2Ip("127.0.0.1"), 50001), clientB = MapIpp.Make(IpText.Str2Ip("127.0.0.1"), 50002);
        var r = client.Read(a.Net.HandleIncoming(client.Login(CharId), clientA));
        var sa = a.Sessions.GetSessionByIpp(clientA);
        var ca = sa?.Char as CharData;
        Check("A: the 0x00A answered (the S2C LOGIN in the key's datagram), in zone 113", "True/True/113",
              $"{r?.Exists(p => Opcode(p) == 0x00A)}/{sa?.InZone}/{ca?.Zone}");
        Check("A: accounts_sessions server_addr from the zone, client port", "127.0.0.1/54230/50001/00000010", Session());
        Pump();
        Check("A: no KillSession (the char's zone did not change)", "", Routed());

        // the client loads (0x00C), so its datagrams decipher with the current key and a 0x00B can go
        void Drain(MapProc m, ulong from, Client cl = null)
        {
            cl ??= client;
            for (int i = 0; i < 8; i++) cl.Read(m.Net.HandleIncoming(cl.Encrypted((0x00C, 4, null)), from));
        }
        Drain(a, clientA);

        // network.ZONE_IP_FROM_CLIENT on both maps for the zone change to B: the client reached the login on
        // 10.11.12.13 (A's 0x00A ran with the setting off, so its session gets the address directly; B loads it)
        db.Execute("UPDATE accounts_sessions SET client_server_addr = ? WHERE charid = ?",
                   new[] { BoundValue.Of(0x0D0C0B0Au), BoundValue.Of(CharId) });
        a.Net.ZoneIpFromClient = true;
        b.Net.ZoneIpFromClient = true;
        sa.ClientServerIp = 0x0D0C0B0A;

        // ---- 2. the zone line to B --------------------------------------------------------------------------------
        Console.WriteLine("2. the zone line z352 to Valley of Sorrows (B):");
        var mapRect = ((ushort)0x05E, 24, (Action<byte[]>)(buf => BitConverter.GetBytes(ZoneLine.PackId("z352")).CopyTo(buf, 4)));
        client.Read(a.Net.HandleIncoming(client.Encrypted(mapRect), clientA));
        a.Zone.Tick();
        Check("A: the zone change queued the 0x00B, the char out of the zone", "True/False",
              $"{sa.PacketList.Any(p => p.GetOpcode() == 0x00B)}/{sa.InZone}");
        Check("A never loaded Valley of Sorrows (GetZoneIPP / loadZoneFile only)", false, a.Zone.IsZoneLoaded(Valley));
        var datagram = a.Net.HandleIncoming(client.Encrypted((0x015, 32, null)), clientA);
        r = client.Read(datagram);
        var logout = r?.Find(p => Opcode(p) == 0x00B);
        Check("the 0x00B: ZONECHANGE (2) to B's address", $"2/10.11.12.13/{BPort}",
              logout == null ? "none" : $"{logout[4]}/{IpText.Ip2Str(BitConverter.ToUInt32(logout, 4 + S2cLogout.IpOffset()))}/" +
                                        $"{BitConverter.ToUInt16(logout, 4 + S2cLogout.PortOffset())}");
        Check("A: the session pending zone, the key moved on, its char let go", "PendingZone/00000012/False",
              $"{sa.Blowfish.Status}/{sa.Blowfish.Key[4]:X8}/{sa.Char != null}");
        Check("chars: Valley of Sorrows from Cape Teriggan, at the line's destination", "128/113/45.25/-2.115", Pos());
        Check("accounts_sessions: B's address, no client port, the incremented key", $"{BIp}/{BPort}/0/00000012", Session());
        Check("char_stats.zoning 1", "1", Q1(db, "SELECT zoning FROM char_stats WHERE charid = ?"));
        Pump();
        Check("A's CharZone (send_parse) is routed to B", "CharZone->B", Routed());
        Check("the world's character cache: the char on B", $"{BIp}:{BPort}", world.CharacterCache.GetCharacterIpp(CharId)?.ToString());
        Check("B: a pending session for the char", "1", b.Sessions.PendingSessions.Count(s => s.CharId == CharId));
        Check("the client's reply to the 0x00B (0x00D, old key): A answers nothing", null,
              a.Net.HandleIncoming(client.Encrypted((0x00D, 8, null)), clientA));

        // ---- 3. B picks the char up ------------------------------------------------------------------------------
        Console.WriteLine("3. the login on B:");
        client.Key[4] += 2; // the client's key after the 0x00B
        r = client.Read(b.Net.HandleIncoming(client.Login(CharId), clientB));
        var sb = b.Sessions.GetSessionByIpp(clientB);
        Check("B: the 0x00A loaded the client's address (ZONE_IP_FROM_CLIENT)", 0x0D0C0B0Au, sb?.ClientServerIp ?? 0u);
        var cb = sb?.Char as CharData;
        Check("B: the 0x00A answered under the new key, the char in Valley of Sorrows", "True/True/Accepted/128",
              $"{r?.Exists(p => Opcode(p) == 0x00A)}/{sb?.InZone}/{sb?.Blowfish.Status}/{cb?.Zone}");
        Check("B: the pending session taken up", "0", b.Sessions.PendingSessions.Count(s => s.CharId == CharId));
        Check("B: entered at the zone line's destination", "45.25/-2.115", cb == null ? "-" : $"{Math.Round(cb.X, 2)}/{Math.Round(cb.Y, 3)}");
        Check("accounts_sessions: B's address, the new client port, the targid", $"{BIp}/{BPort}/50002/00000012/{cb?.TargId}",
              Session() + "/" + Q1(db, "SELECT targid FROM accounts_sessions WHERE charid = ?"));
        Check("B loaded Valley of Sorrows, A did not", "True/False", $"{b.Zone.IsZoneLoaded(Valley)}/{a.Zone.IsZoneLoaded(Valley)}");
        Check("A still keeps its pending-zone session", "1", a.Sessions.Sessions.Count(s => s.CharId == CharId));
        Pump();
        Check("B's KillSession (a char new to B, its zone changed) goes to the previous zone's process", "KillSession->A", Routed());
        Check("A: its session closed on the other process's request", "0/True",
              $"{a.Sessions.Sessions.Count(s => s.CharId == CharId)}/{a.Log.Contains($"Closing session of charid {CharId} on request of other process")}");
        Check("accounts_sessions kept (a zone change, not a logout)", $"{BIp}/{BPort}/50002/00000012", Session());
        Drain(b, clientB);
        r = client.Read(b.Net.HandleIncoming(client.Encrypted((0x015, 32, null)), clientB));
        Check("B: the traffic after the login deciphers with the new key both ways", true, r != null && sb.HasDecryptedPacket);

        a.Net.ZoneIpFromClient = false;
        b.Net.ZoneIpFromClient = false;

        // ---- 4. back to A; B's leftover session times out ---------------------------------------------------------
        Console.WriteLine("4. back to Cape Teriggan (A) by z3k2:");
        cb.X = 39.951f; cb.Y = -3.102f; cb.Z = -140.068f;
        var back = ((ushort)0x05E, 24, (Action<byte[]>)(buf => BitConverter.GetBytes(ZoneLine.PackId("z3k2")).CopyTo(buf, 4)));
        client.Read(b.Net.HandleIncoming(client.Encrypted(back), clientB));
        b.Zone.Tick();
        r = client.Read(b.Net.HandleIncoming(client.Encrypted((0x015, 32, null)), clientB));
        logout = r?.Find(p => Opcode(p) == 0x00B);
        Check("the 0x00B names A", "2/127.0.0.1/54230",
              logout == null ? "none" : $"{logout[4]}/{IpText.Ip2Str(BitConverter.ToUInt32(logout, 4 + S2cLogout.IpOffset()))}/" +
                                        $"{BitConverter.ToUInt16(logout, 4 + S2cLogout.PortOffset())}");
        Check("chars: Cape Teriggan from Valley of Sorrows", "113/128", string.Join("/", Pos().Split('/').Take(2)));
        Pump();
        Check("B's CharZone is routed to A", "CharZone->A", Routed());
        client.Key[4] += 2;
        r = client.Read(a.Net.HandleIncoming(client.Login(CharId), clientA));
        sa = a.Sessions.GetSessionByIpp(clientA);
        Check("A: the char back in Cape Teriggan under the next key", "True/True/113/00000014",
              $"{r?.Exists(p => Opcode(p) == 0x00A)}/{sa?.InZone}/{(sa?.Char as CharData)?.Zone}/{sa?.Blowfish.Key[4]:X8}");
        Check("accounts_sessions: A's address", "127.0.0.1/54230/50001/00000014", Session());
        // the KillSession for B is lost: B's session times out instead, and the row belongs to A now
        world.HandleIncomingMessages();
        chan.Sent.Clear();
        b.Now = b.Now.AddSeconds(120);
        b.Sessions.CleanupSessions(MapIpp.Ip(b.Ipp), MapIpp.Port(b.Ipp), 60);
        Check("B: its timed-out session dropped, accounts_sessions left for A (otherMap)", "0/127.0.0.1/54230/50001/00000014",
              $"{b.Sessions.Sessions.Count(s => s.CharId == CharId)}/{Session()}");

        // ---- 5. a char sent to a process that does not hold its zone -------------------------------------------------
        Console.WriteLine("5. a char in Valley of Sorrows logging in on A:");
        Seed(StrayId, "Xzoneb", Valley, 45.25f, -2.115f, -140.562f);
        a.Ipc.HandleMessage(new CharZone(StrayId, (ZoneId)Teriggan)); // misrouted
        a.Log.Clear();
        var stray = new Client { Key = (uint[])key.Clone() };
        ulong strayIpp = MapIpp.Make(IpText.Str2Ip("127.0.0.1"), 50003);
        r = stray.Read(a.Net.HandleIncoming(stray.Login(StrayId), strayIpp));
        var ss = a.Sessions.GetSessionByIpp(strayIpp);
        var cs = ss?.Char as CharData;
        Check("A: no S2C LOGIN, not in a zone, the home point demanded", "False/False/HomePoint/Accepted",
              $"{r?.Exists(p => Opcode(p) == 0x00A)}/{ss?.InZone}/{cs?.RequestedWarp}/{ss?.Blowfish.Status}");
        Check("  logged as GP_CLI_COMMAND_LOGIN does", true,
              a.Log.Contains("GP_CLI_COMMAND_LOGIN: player tried to enter zone that was invalid or out of range") &&
              a.Log.Contains("GP_CLI_COMMAND_LOGIN: dumping player `Xzoneb` to homepoint!"));
        Check("A still has not loaded Valley of Sorrows", false, a.Zone.IsZoneLoaded(Valley));

        // ---- what one process's char does reaching the chars on the other, through the world ----------------------------
        var names = new Dictionary<uint, string> { [CharId] = "Xzonea", [QId] = "Xzonec", [RId] = "Xzoned", [SId] = "Xzonee" };
        // the routed messages of these types (others the ticks send are left out); the list is taken
        string RoutedOf(params string[] types)
        {
            var s = string.Join(",", routed.Where(x => types.Contains(x.Split("->")[0])));
            routed.Clear();
            return s;
        }
        // the world's routing and both processes' ticks until the messages have run their course
        void Settle()
        {
            for (int i = 0; i < 6; i++)
            {
                Pump();
                a.Zone.Tick();
                b.Zone.Tick();
            }
        }
        List<byte[]> Act(Peer x, (ushort Type, int Size, Action<byte[]> Fill) sub) =>
            x.Cl.Read(x.M.Net.HandleIncoming(x.Cl.Encrypted(sub), x.From)) ?? new List<byte[]>();
        // what the char was sent meanwhile (by an IPC message or another char), taken off its packet list
        List<byte[]> Inbox(Peer x)
        {
            var l = x.S.PacketList.Select(pk => pk.Buffer.AsSpan(0, (int)pk.GetSize()).ToArray()).ToList();
            x.S.PacketList.Clear();
            return l;
        }
        // a fresh start: the chars' packets and the routed list
        void ClearAll(params Peer[] xs)
        {
            foreach (var x in xs) Inbox(x);
            routed.Clear();
        }
        string Text(byte[] buf, int off, int len) =>
            off >= buf.Length ? "" : Encoding.ASCII.GetString(buf, off, Math.Min(len, buf.Length - off)).Split('\0')[0];
        // 0x017: kind / sender / message, the last one of the kind
        string Chat(List<byte[]> l, int kind) => l.LastOrDefault(pk => Opcode(pk) == 0x017 && pk[4] == kind) is byte[] c
            ? $"{c[4]}/{Text(c, 8, 15)}/{Text(c, 23, 150)}" : "none";
        int Chats(List<byte[]> l) => l.Count(pk => Opcode(pk) == 0x017);
        // 0x009: the message ids
        string Msgs(List<byte[]> l) => string.Join(",", l.Where(pk => Opcode(pk) == 0x009).Select(pk => BitConverter.ToUInt16(pk, 10)));
        // the last GROUP_TBL: kind and the members by name (sorted)
        string Tbl(List<byte[]> l)
        {
            if (l.LastOrDefault(pk => Opcode(pk) == 0x0C8) is not byte[] t) return "none";
            var ids = new List<string>();
            for (int i = 0; i < 20; i++)
            {
                uint id = BitConverter.ToUInt32(t, 8 + i * 12);
                if (id != 0) ids.Add(names.TryGetValue(id, out var n) ? n : id.ToString());
            }
            ids.Sort(StringComparer.Ordinal);
            return $"{t[4]}:{string.Join(",", ids)}";
        }
        // the GROUP_LISTs after the last GROUP_TBL: name:zone (0 for a member in the receiver's zone, which carries its
        // numbers), sorted
        string Lists(List<byte[]> l)
        {
            int from = l.FindLastIndex(pk => Opcode(pk) == 0x0C8);
            var e = l.Skip(from + 1).Where(pk => Opcode(pk) == 0x0DD).Select(pk => $"{Text(pk, 40, 16)}:{BitConverter.ToUInt16(pk, 32)}").ToList();
            e.Sort(StringComparer.Ordinal);
            return string.Join(",", e);
        }
        string Party(uint id) => Q1(db, "SELECT CONCAT_WS('/', partyid, allianceid, partyflag) FROM accounts_parties WHERE charid = ?", id);
        string Parties(params uint[] ids) => string.Join(" ", ids.Select(Party));
        uint PartyId(List<byte[]> l) => l.LastOrDefault(pk => Opcode(pk) == 0x0E1) is byte[] ck ? BitConverter.ToUInt32(ck, 4) : 0xFFFFFFFF;

        (ushort, int, Action<byte[]>) Tell(string to, string text) => ((ushort)0x0B6, (0x15 + text.Length + 1 + 3) & ~3, buf =>
        {
            buf[4] = 3;
            Encoding.ASCII.GetBytes(to).CopyTo(buf, 6);
            Encoding.ASCII.GetBytes(text).CopyTo(buf, 0x15);
        });
        (ushort, int, Action<byte[]>) Say(byte kind, string text) => ((ushort)0x0B5, (6 + text.Length + 1 + 3) & ~3, buf =>
        {
            buf[4] = kind;
            Encoding.ASCII.GetBytes(text).CopyTo(buf, 6);
        });
        (ushort, int, Action<byte[]>) Invite(uint id, byte kind) => ((ushort)0x06E, 12, buf =>
        {
            BitConverter.GetBytes(id).CopyTo(buf, 4);
            buf[10] = kind;
        });
        (ushort, int, Action<byte[]>) Answer(byte res) => ((ushort)0x074, 8, buf => buf[4] = res);
        (ushort, int, Action<byte[]>) Strike(string name, byte kind) => ((ushort)0x071, 28, buf =>
        {
            buf[10] = kind;
            Encoding.ASCII.GetBytes(name).CopyTo(buf, 12);
        });
        (ushort, int, Action<byte[]>) CheckId() => ((ushort)0x078, 4, null);

        // ---- 6. chars on both processes ------------------------------------------------------------------------------
        Console.WriteLine("6. Xzonea and Xzonee on A, Xzonec and Xzoned on B:");
        Sql(db, "INSERT INTO linkshells (name, color, postrights) VALUES (?, ?, 0)", BoundValue.Of(LsName), BoundValue.Of((ushort)0xF321));
        var lsRow = Sql(db, "SELECT linkshellid FROM linkshells WHERE name = ?", BoundValue.Of(LsName));
        uint lsid = lsRow != null && lsRow.Next() ? lsRow.Get<uint>(0u) : 0;
        // Xzonee holds the linkshell (513), Xzoned a pearl (515), each in LS1 at its login
        void GiveLinkshell(uint id, ushort itemId, byte type)
        {
            var extra = new byte[24];
            BitConverter.TryWriteBytes(extra.AsSpan(0, 4), lsid);
            BitConverter.TryWriteBytes(extra.AsSpan(6, 2), (ushort)0xF321);
            extra[8] = type;
            Sql(db, "INSERT INTO char_inventory (charid, location, slot, itemId, quantity, signature, extra) VALUES (?, 0, 1, ?, 1, ?, ?)",
                BoundValue.Of(id), BoundValue.Of(itemId), BoundValue.Of(LsName), BoundValue.Of(extra));
            Sql(db, "INSERT INTO char_equip (charid, slotid, equipslotid, containerid) VALUES (?, 1, 16, 0)", BoundValue.Of(id));
        }
        Seed(QId, "Xzonec", Valley, 45.25f, -2.115f, -140.562f);
        Seed(RId, "Xzoned", Valley, 46.25f, -2.115f, -140.562f);
        Seed(SId, "Xzonee", Teriggan, 238.925f, -3.197f, -180.056f);
        GiveLinkshell(SId, 513, 1);
        GiveLinkshell(RId, 515, 3);
        foreach (var id in new[] { QId, SId }) Sql(db, "REPLACE INTO char_profile (charid, unity_leader) VALUES (?, 1)", BoundValue.Of(id));

        // the lobby's CharZone, the 0x00A, the 0x00C
        Peer LogIn(MapProc m, uint id, ushort zone, ushort clientPort)
        {
            chan.Inject(loginIpp, IpcFraming.ToBytesWithHeader(new CharZone(id, (ZoneId)zone)));
            Pump();
            var x = new Peer { Name = names[id], M = m, Cl = new Client { Key = (uint[])key.Clone() }, From = MapIpp.Make(IpText.Str2Ip("127.0.0.1"), clientPort) };
            x.Cl.Read(m.Net.HandleIncoming(x.Cl.Login(id), x.From));
            Drain(m, x.From, x.Cl);
            return x;
        }
        var p = new Peer { Name = "Xzonea", M = a, Cl = client, From = clientA };
        var q = LogIn(b, QId, Valley, 50011);
        var r2 = LogIn(b, RId, Valley, 50012);
        var e = LogIn(a, SId, Teriggan, 50013);
        Settle();
        Check("the four in their zones", "113/128/128/113", $"{p.C?.Zone}/{q.C?.Zone}/{r2.C?.Zone}/{e.C?.Zone}");
        Check("accounts_sessions: B's address for Xzonec / Xzoned, A's for Xzonee", $"{BIp}/{BIp}/127.0.0.1",
              $"{Session(QId).Split('/')[0]}/{Session(RId).Split('/')[0]}/{Session(SId).Split('/')[0]}");
        string LsSession(uint id) => Q1(db, "SELECT CONCAT_WS('/', linkshellid1, linkshellrank1) FROM accounts_sessions WHERE charid = ?", id);
        Check("the linkshell joined on each process: accounts_sessions linkshellid1 / rank", $"{lsid}/1 {lsid}/3", $"{LsSession(SId)} {LsSession(RId)}");

        // ---- 7. tells ------------------------------------------------------------------------------------------------
        Console.WriteLine("7. tells (0x0B6, ChatMessageTell):");
        ClearAll(p, q, r2, e);
        Act(p, Tell("Xzonec", "hello B"));
        Settle();
        Check("A's tell is routed to the recipient's process", "ChatMessageTell->B", RoutedOf("ChatMessageTell", "MessageStandard"));
        Check("Xzonec on B gets it: MESSAGE_TELL from Xzonea", "3/Xzonea/hello B", Chat(Inbox(q), 3));
        Act(q, Tell("xzonee", "hi A"));
        Settle();
        Check("B's tell to a char on A (the name in another case)", "ChatMessageTell->A", RoutedOf("ChatMessageTell", "MessageStandard"));
        Check("Xzonee on A gets it", "3/Xzonec/hi A", Chat(Inbox(e), 3));
        Act(q, Tell("Xzonenobody", "anyone?"));
        Settle();
        Check("a tell to a char not online: the world answers the sender's process", "MessageStandard->B",
              RoutedOf("ChatMessageTell", "MessageStandard"));
        Check("  TellNotReceivedOffline (125) to the sender", "125", Msgs(Inbox(q)));

        // ---- 8. a party across the processes ------------------------------------------------------------------------
        Console.WriteLine("8. a party: Xzonea (A) invites Xzonec (B):");
        ClearAll(p, q, r2, e);
        Act(p, Invite(QId, 0));
        Settle();
        Check("the invite is routed to the invitee's process", "PartyInvite->B", RoutedOf("PartyInvite"));
        var inv = Inbox(q).LastOrDefault(pk => Opcode(pk) == 0x0DC);
        Check("Xzonec: GROUP_SOLICIT_REQ naming Xzonea, a party invite, pending", $"Xzonea/0/{CharId}",
              inv == null ? "none" : $"{Text(inv, 12, 16)}/{inv[11]}/{q.C.InvitePending.UniqueNo}");
        Act(q, Answer(1));
        Settle();
        var rt = RoutedOf("PartyInviteResponse", "PartyReload");
        Check("the answer is routed to the inviter's process, PartyReload to both", "True/True/True",
              $"{rt.StartsWith("PartyInviteResponse->A")}/{rt.Contains("PartyReload->A")}/{rt.Contains("PartyReload->B")}");
        Check("accounts_parties: Xzonea leads, Xzonec a member", $"{CharId}/0/4 {CharId}/0/0", Parties(CharId, QId));
        var pIn = Inbox(p);
        var qIn = Inbox(q);
        Check("A's lists: both, Xzonec from the database (in Valley of Sorrows)", "0:Xzonea,Xzonec Xzonea:0,Xzonec:128", $"{Tbl(pIn)} {Lists(pIn)}");
        Check("B's lists: both, Xzonea from the database (in Cape Teriggan)", "0:Xzonea,Xzonec Xzonea:113,Xzonec:0", $"{Tbl(qIn)} {Lists(qIn)}");
        Check("B: GROUP_CHECKID gives Xzonea's party", CharId, PartyId(Act(q, CheckId())));
        Act(q, Say(4, "party hi"));
        Settle();
        Check("party chat: routed to both processes", "ChatMessageParty->A,ChatMessageParty->B", RoutedOf("ChatMessageParty"));
        Check("Xzonea on A hears it, Xzonec does not hear itself", "4/Xzonec/party hi/0", $"{Chat(Inbox(p), 4)}/{Chats(Inbox(q))}");

        // ---- 9. an alliance across the processes ----------------------------------------------------------------------
        Console.WriteLine("9. an alliance: Xzoned (B) and Xzonee (A) form a party, Xzonea invites Xzoned's party:");
        Act(r2, Invite(SId, 0));
        Settle();
        Act(e, Answer(1));
        Settle();
        Check("accounts_parties: Xzoned leads, Xzonee a member", $"{RId}/0/4 {RId}/0/0", Parties(RId, SId));
        ClearAll(p, q, r2, e);
        Act(p, Invite(RId, 5));
        Settle();
        Check("the alliance invite is routed to B", "PartyInvite->B", RoutedOf("PartyInvite"));
        Check("Xzoned: GROUP_SOLICIT_REQ for an alliance", 5, Inbox(r2).LastOrDefault(pk => Opcode(pk) == 0x0DC)?[11] ?? -1);
        // with a trust out in the inviter's party, the answer is refused on A, the invitee told on B
        var trust = new TrustEntity(p.C, 1, false);
        p.C.PTrusts.Add(trust);
        Act(r2, Answer(1));
        Pump();
        Pump();
        p.C.PTrusts.Remove(trust);
        Check("the inviter's party has a trust: refused on A, routed back", "PartyInviteResponse->A,MessageStandard->B",
              RoutedOf("PartyInviteResponse", "MessageStandard", "AllianceReload"));
        Check("  Xzoned told TrustCannotJoinAlliance (266), no alliance", $"266/{RId}/0/4", $"{Msgs(Inbox(r2))}/{Party(RId)}");
        Act(p, Invite(RId, 5));
        Settle();
        Act(r2, Answer(1));
        Settle();
        Check("AllianceReload routed to both processes", "True/True",
              $"{RoutedOf("AllianceReload").Contains("AllianceReload->A")}/{routed.Count == 0}");
        Check("accounts_parties: Xzonea's alliance, Xzoned's party the second",
              $"{CharId}/{CharId}/12 {CharId}/{CharId}/0 {RId}/{CharId}/5 {RId}/{CharId}/1", Parties(CharId, QId, RId, SId));
        var all4 = "5:Xzonea,Xzonec,Xzoned,Xzonee";
        var eIn = Inbox(e);
        Check("the alliance lists on each process", $"{all4} {all4} {all4} {all4}",
              $"{Tbl(Inbox(p))} {Tbl(Inbox(q))} {Tbl(Inbox(r2))} {Tbl(eIn)}");
        Check("A's GROUP_LIST for Xzonee: Xzonea here, the B pair from the database", "Xzonea:0,Xzonec:128,Xzoned:128,Xzonee:0", Lists(eIn));
        Act(e, Say(4, "all hi"));
        Settle();
        Check("alliance chat: routed to both processes", "ChatMessageAlliance->A,ChatMessageAlliance->B", RoutedOf("ChatMessageAlliance"));
        Check("Xzonea, Xzonec and Xzoned hear Xzonee, not Xzonee", "4/Xzonee/all hi 4/Xzonee/all hi 4/Xzonee/all hi 0",
              $"{Chat(Inbox(p), 4)} {Chat(Inbox(q), 4)} {Chat(Inbox(r2), 4)} {Chats(Inbox(e))}");

        // ---- 10. linkshell across the processes -------------------------------------------------------------------------
        Console.WriteLine("10. the linkshell: Xzonee (A) holds it, Xzoned (B) a pearl:");
        ClearAll(p, q, r2, e);
        Act(r2, Say(5, "ls hi"));
        Settle();
        Check("linkshell chat: routed to the members' processes (accounts_sessions linkshellid1)",
              "ChatMessageLinkshell->A,ChatMessageLinkshell->B", RoutedOf("ChatMessageLinkshell"));
        Check("Xzonee hears Xzoned, the others nothing", "5/Xzoned/ls hi/0/0", $"{Chat(Inbox(e), 5)}/{Chats(Inbox(p))}/{Chats(Inbox(q))}");
        Act(e, ((ushort)0x0E2, 144, buf =>
        {
            buf[4] = 0x40;
            Encoding.ASCII.GetBytes("Shell news").CopyTo(buf, 16);
        }));
        Settle();
        Check("a linkshell message: routed to both", "LinkshellSetMessage->A,LinkshellSetMessage->B", RoutedOf("LinkshellSetMessage"));
        var lsMsg = Inbox(r2).LastOrDefault(pk => Opcode(pk) == 0x0CC);
        Check("Xzoned gets the 0x0CC post: message, poster", "Shell news/Xzonee",
              lsMsg == null ? "none" : $"{Text(lsMsg, 8, 128)}/{Text(lsMsg, 140, 16)}");
        Act(e, ((ushort)0x077, 24, buf =>
        {
            Encoding.ASCII.GetBytes("Xzoned").CopyTo(buf, 4);
            buf[20] = 1;
            buf[21] = 2; // pearl to sack
        }));
        Settle();
        Check("the rank change: routed to Xzoned's process by name", "LinkshellRankChange->B", RoutedOf("LinkshellRankChange"));
        string LsItem(uint id) => Q1(db, "SELECT CONCAT_WS('/', itemId, ORD(SUBSTRING(extra, 9, 1))) FROM char_inventory WHERE charid = ? AND slot = 1", id);
        Check("  Xzoned's pearl a sack: the item, accounts_sessions", $"514/2 {lsid}/2", $"{LsItem(RId)} {LsSession(RId)}");
        ClearAll(r2);
        Act(e, Strike("Xzoned", 1));
        Settle();
        Check("the kick: routed to Xzoned's process by name", "LinkshellRemove->B", RoutedOf("LinkshellRemove"));
        Check("  Xzoned told LinkshellKicked (109), the sack broken and unequipped, out of accounts_sessions", "109 514/4 0 0/0",
              $"{Msgs(Inbox(r2))} {LsItem(RId)} {Q1(db, "SELECT COUNT(*) FROM char_equip WHERE charid = ? AND equipslotid = 16", RId)} {LsSession(RId)}");
        Act(e, Say(5, "anyone?"));
        Settle();
        Check("linkshell chat now goes to A only", "ChatMessageLinkshell->A", RoutedOf("ChatMessageLinkshell"));

        // ---- 11. unity chat and yells -----------------------------------------------------------------------------------
        Console.WriteLine("11. unity chat (accounts_sessions.unitychat) and yells (the yell zones' processes):");
        Act(q, ((ushort)0x118, 8, buf => buf[4] = 1));
        Act(e, ((ushort)0x118, 8, buf => buf[4] = 1));
        Check("Xzonec and Xzonee in unity 1's chat", "1 1",
              $"{Q1(db, "SELECT unitychat FROM accounts_sessions WHERE charid = ?", QId)} {Q1(db, "SELECT unitychat FROM accounts_sessions WHERE charid = ?", SId)}");
        ClearAll(p, q, r2, e);
        Act(q, Say(0x21, "unity hi"));
        Settle();
        // other chars of unity 1 may sit on other addresses: just these two processes
        var unity = routed.Where(x => x == "ChatMessageUnity->A" || x == "ChatMessageUnity->B").ToList();
        routed.Clear();
        Check("unity chat: routed to both processes", "ChatMessageUnity->A,ChatMessageUnity->B", string.Join(",", unity));
        Check("Xzonee hears Xzonec, the others nothing", $"{(byte)ChatMessageType.Unity}/Xzonec/unity hi/0/0",
              $"{Chat(Inbox(e), (byte)ChatMessageType.Unity)}/{Chats(Inbox(p))}/{Chats(Inbox(r2))}");
        Check("the world's yell endpoints: A (it holds the cities), not B (Valley of Sorrows has no yell)", "True/False",
              $"{world.ZoneSettings.YellMapEndpoints.Any(x => x.Raw == a.Ipp)}/{world.ZoneSettings.YellMapEndpoints.Any(x => x.Raw == b.Ipp)}");
        chan.Inject(a.WorldIpp, IpcFraming.ToBytesWithHeader(new ChatMessageYell { SenderId = SId, SenderName = "Xzonee", Message = "yell" }));
        Pump();
        Check("a yell is routed to A only", "ChatMessageYell->A", RoutedOf("ChatMessageYell"));

        // ---- 12. SendToZone's player cap; Xzonea to B -----------------------------------------------------------------
        Console.WriteLine("12. Xzonea's zone line to Valley of Sorrows while it fills up (charutils::SendToZone's cap check):");
        (p.C.X, p.C.Y, p.C.Z) = (239.925f, -3.197f, -180.056f);
        ClearAll(p);
        Act(p, mapRect);
        // the zone line found room (map.ZONE_PLAYER_CAP 700); Valley of Sorrows fills up before the zone tick
        int cap = a.Cfg.ZonePlayerCap, reserved = a.Cfg.ZonePlayerGmReserved;
        int pop = int.Parse(Q1(db, "SELECT COUNT(*) FROM accounts_sessions s JOIN chars c ON c.charid = s.charid WHERE c.pos_zone = ?", Valley));
        a.Cfg.ZonePlayerCap = pop;
        a.Cfg.ZonePlayerGmReserved = 0;
        a.Log.Clear();
        a.Zone.Tick();
        Check("A: no 0x00B, the session kept, SendToZone's refusal logged", "False/Accepted/True",
              $"{p.S.PacketList.Any(pk => pk.GetOpcode() == 0x00B)}/{p.S.Blowfish.Status}/" +
              $"{a.Log.Contains("charutils::SendToZone : zone 128 at player cap, denying Xzonea (gm=0)")}");
        Check("chars: still in Cape Teriggan", "113", Pos().Split('/')[0]);
        // still zoning: a tell does not reach it
        Act(q, Tell("Xzonea", "there?"));
        Settle();
        Check("a tell to the char waiting to zone: TellNotReceivedOffline", "125", Msgs(Inbox(q)));
        Check("the next ticks try again", true, a.Log.Count(l => l.Contains("at player cap, denying Xzonea")) > 1);
        a.Cfg.ZonePlayerCap = cap;
        a.Cfg.ZonePlayerGmReserved = reserved;
        a.Zone.Tick();
        r = Act(p, (0x015, 32, null));
        logout = r.Find(pk => Opcode(pk) == 0x00B);
        Check("room again: the 0x00B names B", $"2/{BIp}/{BPort}",
              logout == null ? "none" : $"{logout[4]}/{IpText.Ip2Str(BitConverter.ToUInt32(logout, 4 + S2cLogout.IpOffset()))}/" +
                                        $"{BitConverter.ToUInt16(logout, 4 + S2cLogout.PortOffset())}");
        Pump();
        Check("CharZone to B, and A's members told to reload (AllianceReload)", "True/True", RoutedOf("CharZone", "AllianceReload") is var cz
            ? $"{cz.Contains("CharZone->B")}/{cz.Contains("AllianceReload->")}" : "");
        client.Key[4] += 2;
        p.M = b;
        p.From = MapIpp.Make(IpText.Str2Ip("127.0.0.1"), 50021);
        client.Read(b.Net.HandleIncoming(client.Login(CharId), p.From));
        Pump();
        Check("B: Xzonea in Valley of Sorrows, A's session closed", "128/0", $"{p.C?.Zone}/{a.Sessions.Sessions.Count(x => x.CharId == CharId)}");
        Drain(b, p.From, client);
        Settle();
        var pB = Inbox(p);
        Check("B picks up Xzonea's party and alliance (GameOk's ReloadParty)", $"{all4} Xzonea:0,Xzonec:0,Xzoned:0,Xzonee:113",
              $"{Tbl(pB)} {Lists(pB)}");
        Check("B: GROUP_CHECKID gives Xzonea's party", CharId, PartyId(Act(p, CheckId())));
        Check("accounts_parties unchanged by the zoning", $"{CharId}/{CharId}/12 {CharId}/{CharId}/0", Parties(CharId, QId));
        ClearAll(p, q, r2, e);
        Act(p, Say(4, "from B"));
        Settle();
        Check("alliance chat from B reaches Xzonee on A and Xzonec / Xzoned on B", "4/Xzonea/from B 4/Xzonea/from B 4/Xzonea/from B",
              $"{Chat(Inbox(e), 4)} {Chat(Inbox(q), 4)} {Chat(Inbox(r2), 4)}");

        // ---- 13. the alliance dissolved, a kick across the processes -----------------------------------------------------
        Console.WriteLine("13. Xzonea dissolves the alliance; Xzoned (B) kicks Xzonee (A) from its party:");
        ClearAll(p, q, r2, e);
        Act(p, ((ushort)0x070, 8, buf => buf[4] = 5));
        Settle();
        Check("AllianceDissolve routed to both processes", "AllianceDissolve->A,AllianceDissolve->B", RoutedOf("AllianceDissolve"));
        Check("accounts_parties: two parties again", $"{CharId}/0/4 {CharId}/0/0 {RId}/0/4 {RId}/0/0", Parties(CharId, QId, RId, SId));
        Check("Xzonee's list: its party only", "0:Xzoned,Xzonee", Tbl(Inbox(e)));
        Act(r2, Strike("Xzonee", 0));
        Settle();
        Check("the kick: PartyReload to B, PlayerKick to Xzonee's process", "PartyReload->B,PlayerKick->A", RoutedOf("PartyReload", "PlayerKick"));
        Check("Xzonee out of the party: no row, GROUP_CHECKID 0", "-/0", $"{Party(SId)}/{PartyId(Act(e, CheckId()))}");
    }

    // ---- 15-18: the world's router and map processes of their own --------------------------------------------------------

    private static readonly uint[] Key0 = { 0x2B7E1516, 0x28AED2A6, 0xABF71588, 0x09CF4F3C, 0x10 };

    private static byte[] KeyBytes()
    {
        var b = new byte[20];
        for (int i = 0; i < 5; i++) BitConverter.TryWriteBytes(b.AsSpan(4 * i), Key0[i]);
        return b;
    }

    // the world (an IpcServer on the in-memory channel, reading zone_settings now) and the map processes behind it, on
    // one clock
    private sealed class Hub
    {
        public readonly QueueWorldChannel Chan = new QueueWorldChannel();
        public IpcServer World;
        public readonly List<MapProc> Maps = new List<MapProc>();
        public readonly List<string> Routed = new List<string>();
        public long NowMs = 1_000_000;
        public long Unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public readonly Ipp LoginIpp = new Ipp(IpText.Str2Ip("127.0.0.1"), 54229);

        public Hub(IDatabase db, string dataDir) =>
            World = new IpcServer(db, Chan, new ZoneSettings(db, ZoneInfo.MiscFlagsLookup(db, Path.Combine(dataDir, "zones"))), new WorldSystems(),
                                  _ => { });

        public MapProc Add(MapProc m)
        {
            m.Zone.NowMs = () => NowMs;
            m.Zone.UnixNow = () => Unix;
            Maps.Add(m);
            return m;
        }

        // each message the world sends goes to the map process it names
        public void Pump()
        {
            World.HandleIncomingMessages();
            foreach (var m in Chan.Sent)
            {
                var decoded = IpcFraming.Decode(m.Payload);
                var to = Maps.Find(x => x.Ipp == m.Ipp.Raw);
                Routed.Add($"{decoded.Type}->{to?.Name ?? m.Ipp.ToString()}");
                to?.Ipc.HandleFrame(m.Payload);
            }
            Chan.Sent.Clear();
        }

        // the routed messages of these types, the list taken
        public string Take(params string[] types)
        {
            var s = string.Join(",", Routed.Where(x => types.Contains(x.Split("->")[0])));
            Routed.Clear();
            return s;
        }

        // the routing and every process's zone tick, 400 ms apart
        public void Settle(int rounds = 6)
        {
            for (int i = 0; i < rounds; i++)
            {
                Pump();
                NowMs += 400;
                foreach (var m in Maps) m.Zone.Tick();
            }
            Pump();
        }

        // the lobby hands the char to the process holding its zone (CharZone), the client logs in and loads
        public Peer Enter(MapProc m, uint id, string name, ushort zone, ushort clientPort, out List<byte[]> login)
        {
            Chan.Inject(LoginIpp, IpcFraming.ToBytesWithHeader(new CharZone(id, (ZoneId)zone)));
            Pump();
            var x = new Peer
            {
                Id = id, Name = name, M = m, Cl = new Client { Key = (uint[])Key0.Clone() }, From = MapIpp.Make(IpText.Str2Ip("127.0.0.1"), clientPort),
            };
            login = x.Cl.Read(m.Net.HandleIncoming(x.Cl.Login(id), x.From));
            Load(x);
            return x;
        }

        // the 0x00C until the traffic settles, so the client's datagrams decipher with the current key
        public static void Load(Peer x)
        {
            for (int i = 0; i < 8; i++) x.Cl.Read(x.M.Net.HandleIncoming(x.Cl.Encrypted((0x00C, 4, null)), x.From));
        }

        // the 0x00B a queued zone change sends with the answer to the client's next datagram: kind / ip / port
        public string Leave(Peer x)
        {
            Routed.Clear();
            var r = Act(x, (0x015, 32, null));
            var logout = r.Find(pk => Opcode(pk) == 0x00B);
            Pump();
            return logout == null ? "none" : $"{logout[4]}/{IpText.Ip2Str(BitConverter.ToUInt32(logout, 4 + S2cLogout.IpOffset()))}/" +
                                             $"{BitConverter.ToUInt16(logout, 4 + S2cLogout.PortOffset())}";
        }

        // the client after the 0x00B: the next key, the 0x00A to the process named (from another port when it is another)
        public List<byte[]> Arrive(Peer x, MapProc m, ushort clientPort)
        {
            x.Cl.Key[4] += 2;
            x.M = m;
            x.From = MapIpp.Make(IpText.Str2Ip("127.0.0.1"), clientPort);
            var r = x.Cl.Read(m.Net.HandleIncoming(x.Cl.Login(x.Id), x.From));
            Load(x);
            Pump();
            return r ?? new List<byte[]>();
        }
    }

    private static List<byte[]> Act(Peer x, (ushort Type, int Size, Action<byte[]> Fill) sub) =>
        x.Cl.Read(x.M.Net.HandleIncoming(x.Cl.Encrypted(sub), x.From)) ?? new List<byte[]>();

    // what the char was sent meanwhile, taken off its packet list
    private static List<byte[]> Inbox(Peer x)
    {
        if (x.S == null) return new List<byte[]>();
        var l = x.S.PacketList.Select(pk => pk.Buffer.AsSpan(0, (int)pk.GetSize()).ToArray()).ToList();
        x.S.PacketList.Clear();
        return l;
    }

    private static string Text(byte[] buf, int off, int len) =>
        off >= buf.Length ? "" : Encoding.ASCII.GetString(buf, off, Math.Min(len, buf.Length - off)).Split('\0')[0];

    // 0x017: kind / Data / sender / message, the last one of the kind
    private static string ChatOf(List<byte[]> l, int kind) => l.LastOrDefault(pk => Opcode(pk) == 0x017 && pk[4] == kind) is byte[] c
        ? $"{c[4]}/{BitConverter.ToUInt16(c, 6)}/{Text(c, 8, 15)}/{Text(c, 23, 150)}" : "none";

    // 0x009: MesNo:the Data text
    private static string MsgsOf(List<byte[]> l) =>
        string.Join(",", l.Where(pk => Opcode(pk) == 0x009).Select(pk => $"{BitConverter.ToUInt16(pk, 10)}:{Text(pk, 13, 40)}"));

    private static (ushort, int, Action<byte[]>) Say(byte kind, string text) => ((ushort)0x0B5, (6 + text.Length + 1 + 3) & ~3, buf =>
    {
        buf[4] = kind;
        Encoding.ASCII.GetBytes(text).CopyTo(buf, 6);
    });

    // GP_CLI_COMMAND_EVENTEND: the event's end (mode 0), result 0
    private static (ushort, int, Action<byte[]>) EventEnd(uint charId, ushort eventId) => ((ushort)0x05B, 20, buf =>
    {
        BitConverter.GetBytes(charId).CopyTo(buf, 4);
        BitConverter.GetBytes(eventId).CopyTo(buf, 0x12);
    });

    private static T Private<T>(ZoneHost z, string name, params object[] args) =>
        (T)typeof(ZoneHost).GetMethod(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(z, args);

    // ---- 15. a --lazy map process -----------------------------------------------------------------------------------------

    private static void RunLazy(IDatabase db)
    {
        Console.WriteLine("15. a --lazy map process at A's address (zoneutils::EnsureZoneLoaded / IsZoneReady), B as before:");
        var dataDir = FindDataDir();
        var h = new Hub(db, dataDir);
        var l = h.Add(NewMap(db, dataDir, "L", "127.0.0.1", 54230, h.Chan));
        l.Zone.LazyZones = true;
        var b = h.Add(NewMap(db, dataDir, "B", BIp, BPort, h.Chan));
        var keyBytes = KeyBytes();
        int managed = l.Zone.ZonesAssignedToThisProcess().Count;
        Check("L manages A's zones, none loaded", "True/True/False/False/False",
              $"{managed > 1}/{l.Zone.ZonesAssignedToThisProcess().Contains(Teriggan)}/{l.Zone.IsZoneLoaded(Teriggan)}/" +
              $"{l.Zone.IsZoneLoaded(Kuftal)}/{l.Zone.ZonesAssignedToThisProcess().Contains(Valley)}");

        // Xzlazy at Cape Teriggan's line to Kuftal Tunnel (z350), which L manages too
        SeedChar(db, keyBytes, LazyId, "Xzlazy", Teriggan, -218.710f, -3.046f, -328.023f);
        var x = h.Enter(l, LazyId, "Xzlazy", Teriggan, 50101, out var login);
        Check("the login loads Cape Teriggan (EnsureZoneLoaded in recv_parse), the char in it: S2C LOGIN, the zone",
              "True/True/113/True/False", $"{login?.Exists(pk => Opcode(pk) == 0x00A)}/{x.S?.InZone}/{x.C?.Zone}/" +
                                          $"{l.Zone.IsZoneLoaded(Teriggan)}/{l.Zone.IsZoneLoaded(Kuftal)}");
        Act(x, ((ushort)0x05E, 24, buf => BitConverter.GetBytes(ZoneLine.PackId("z350")).CopyTo(buf, 4)));
        Check("the zone line to Kuftal Tunnel does not load it (GetZoneIPP)", false, l.Zone.IsZoneLoaded(Kuftal));
        l.Zone.Tick();
        Check("the zone tick does (IsZoneReady: managed): the 0x00B names L itself", "True/2/127.0.0.1/54230",
              $"{l.Zone.IsZoneLoaded(Kuftal)}/{h.Leave(x)}");
        var r = h.Arrive(x, l, 50101);
        Check("L: the char in Kuftal Tunnel under the next key", "True/True/174",
              $"{r.Exists(pk => Opcode(pk) == 0x00A)}/{x.S?.InZone}/{x.C?.Zone}");

        // Xzlline at the line to Valley of Sorrows (z352), B's
        SeedChar(db, keyBytes, LazyLineId, "Xzlline", Teriggan, 239.925f, -3.197f, -180.056f);
        var y = h.Enter(l, LazyLineId, "Xzlline", Teriggan, 50102, out _);
        Act(y, ((ushort)0x05E, 24, buf => BitConverter.GetBytes(ZoneLine.PackId("z352")).CopyTo(buf, 4)));
        l.Zone.Tick();
        Check("the line to Valley of Sorrows: the 0x00B names B, L leaves it unloaded (IsZoneReady: not managed)",
              $"2/{BIp}/{BPort}/False", $"{h.Leave(y)}/{l.Zone.IsZoneLoaded(Valley)}");
        Check("the CharZone goes to B", "CharZone->B", h.Take("CharZone"));
        r = h.Arrive(y, b, 50103);
        Check("B: the char in Valley of Sorrows", "True/128", $"{r.Exists(pk => Opcode(pk) == 0x00A)}/{y.C?.Zone}");

        // Xzlstray, in Valley of Sorrows, sent to L: EnsureZoneLoaded loads whatever zone the char is in
        SeedChar(db, keyBytes, LazyStrayId, "Xzlstray", Valley, 45.25f, -2.115f, -140.562f);
        l.Ipc.HandleMessage(new CharZone(LazyStrayId, (ZoneId)Teriggan)); // misrouted
        l.Log.Clear();
        var z = new Peer
        {
            Id = LazyStrayId, Name = "Xzlstray", M = l, Cl = new Client { Key = (uint[])Key0.Clone() }, From = MapIpp.Make(IpText.Str2Ip("127.0.0.1"), 50104),
        };
        r = z.Cl.Read(l.Net.HandleIncoming(z.Cl.Login(LazyStrayId), z.From));
        Check("a char in B's zone logging in on L: Valley of Sorrows loaded for it, no home point (no wrong-process check)",
              "True/True/128/True/None/False", $"{r?.Exists(pk => Opcode(pk) == 0x00A)}/{z.S?.InZone}/{z.C?.Zone}/" +
                                                  $"{l.Zone.IsZoneLoaded(Valley)}/{z.C?.RequestedWarp}/{l.Log.Exists(t => t.Contains("dumping player"))}");
        Check("L has loaded these three zones only", 3,
              l.Zone.ZonesAssignedToThisProcess().Count(l.Zone.IsZoneLoaded) + (l.Zone.IsZoneLoaded(Valley) ? 1 : 0));
    }

    // ---- 16-18. instances, ships and the assist channel ---------------------------------------------------------------------

    // a seeded char's fixtures: a Hume past its first login and the RoV opening (no zone-in cutscenes), the airship pass,
    // the Assist Channel record of eminence, a mentor
    private static void Fixtures(IDatabase db, uint id, byte nation, bool airshipPass = false, bool assist = false, bool mentor = false)
    {
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        var missions = new byte[15 * 70];
        for (int i = 0; i <= 3; i++) BitConverter.GetBytes((ushort)0xFFFF).CopyTo(missions, i * 70);
        BitConverter.GetBytes((ushort)101).CopyTo(missions, 6 * 70);
        BitConverter.GetBytes((ushort)1).CopyTo(missions, 13 * 70);
        missions[13 * 70 + 6] = 1;
        var keyItems = new byte[8 * 128];
        if (airshipPass) keyItems[8 / 8] |= 1 << 8 % 8;
        var eminence = new byte[700];
        if (assist) eminence[64 + 31 * 4 + 1448 / 8] |= 1 << 1448 % 8;
        Sql(db, "UPDATE chars SET nation = ?, titles = ?, missions = ?, keyitems = ?, eminence = ?, mentor = ?, settings = ?, " +
                "playtime = ?, last_logout = NOW() - INTERVAL 1 DAY WHERE charid = ?",
            BoundValue.Of(nation), BoundValue.Of(titles), BoundValue.Of(missions), BoundValue.Of(keyItems), BoundValue.Of(eminence),
            BoundValue.Of(mentor ? 1 : 0), BoundValue.Of(mentor ? 1u << 25 : 0u), BoundValue.Of(mentor ? 5_000_000u : 0u), BoundValue.Of(id));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(id));
        Sql(db, "INSERT IGNORE INTO char_flags(charid) VALUES (?)", BoundValue.Of(id));
    }

    private static void RunScripted(IDatabase db)
    {
        var dataDir = FindDataDir();
        // the airship's crossing, Lower Jeuno and Leujaoam Sanctum join Valley of Sorrows on B for the run
        var moved = new[] { BastokJeunoAirship, LowerJeuno, LeujaoamSanctum };
        var saved = new Dictionary<ushort, (string Ip, ushort Port)>();
        foreach (var zid in moved)
        {
            var row = Sql(db, "SELECT zoneip, zoneport FROM zone_settings WHERE zoneid = ?", BoundValue.Of(zid));
            row.Next();
            saved[zid] = (row.Get<string>("zoneip"), row.Get<ushort>("zoneport"));
        }
        var engines = new List<ScriptEngine>();
        try
        {
            foreach (var zid in moved)
                Sql(db, "UPDATE zone_settings SET zoneip = ?, zoneport = ? WHERE zoneid = ?", BoundValue.Of(BIp), BoundValue.Of(BPort), BoundValue.Of(zid));
            var h = new Hub(db, dataDir);
            // the Bastok - Jeuno airship's cycle (offset 2, every 864) starts at Vana'diel second cycleStart; t is how far
            // into it (the hub's unix clock follows it)
            long cycleStart = (h.Unix - VanaTime.EpochUnixSec) / 864 * 864 + 2;
            void At(long t) => h.Unix = VanaTime.EpochUnixSec + cycleStart + t;
            At(50);
            var got = new Dictionary<MapProc, List<string>>();
            MapProc Scripted(string name, string ip, ushort port)
            {
                var m = h.Add(NewMap(db, dataDir, name, ip, port, h.Chan));
                var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), m.Log.Add, m.Log.Add);
                engines.Add(scripts);
                m.Zone.InitScripts(scripts);
                var list = got[m] = new List<string>();
                scripts.Lua.SetGlobal("report", s => { list.Add(s.ToStringValue(1)); return 0; });
                m.Lua = code =>
                {
                    list.Clear();
                    scripts.Lua.DoString(code, "=verify_crosszone");
                    return string.Join(",", list);
                };
                // instanceutils::Initialize, ShipHandler::InitializeShips (XI.Host map's order)
                m.Zone.InitializeInstances();
                m.Zone.InitializeShips();
                return m;
            }
            Console.WriteLine("16. instances across processes (Leujaoam Sanctum on B, Caedarva Mire on A):");
            var a = Scripted("A", "127.0.0.1", 54230);
            var b = Scripted("B", BIp, BPort);
            RunInstances(db, h, a, b);
            RunShips(db, h, a, b, At);
            RunAssist(db, h, a, b);
            foreach (var m in h.Maps)
            {
                var errors = m.Log.FindAll(t => t.StartsWith("lua:") && !t.Contains("global scripts") || t.Contains("luautils::") && !t.Contains("GetMobByID"));
                Check($"{m.Name}: no script errors", "", string.Join(" | ", errors.Take(5)));
            }
        }
        finally
        {
            foreach (var e in engines) e.Dispose();
            foreach (var (zid, (ip, port)) in saved)
                Sql(db, "UPDATE zone_settings SET zoneip = ?, zoneport = ? WHERE zoneid = ?", BoundValue.Of(ip), BoundValue.Of(port), BoundValue.Of(zid));
        }
    }

    private static void RunInstances(IDatabase db, Hub h, MapProc a, MapProc b)
    {
        var keyBytes = KeyBytes();
        Check("instanceutils: Leujaoam Cleansing is B's (its zone's address), not A's", "False/True",
              $"{Private<bool>(a.Zone, "IsValidInstanceId", LeujaoamCleansing)}/{Private<bool>(b.Zone, "IsValidInstanceId", LeujaoamCleansing)}");

        // Xzistray in Caedarva Mire (A), the entrance: createInstance is refused there
        SeedChar(db, keyBytes, InstStrayId, "Xzistray", CaedarvaMire, -440.0f, -10.0f, -600.0f);
        Fixtures(db, InstStrayId, 1);
        var st = h.Enter(a, InstStrayId, "Xzistray", CaedarvaMire, 50201, out _);
        a.Log.Clear();
        a.Lua($"GetPlayerByID({InstStrayId}):createInstance({LeujaoamCleansing})");
        h.Settle(2);
        Check("A: createInstance refused (CLuaBaseEntity::createInstance: Invalid instanceID), no run", "True/nil",
              $"{a.Log.Contains($"CLuaBaseEntity::createInstance: Invalid instanceID: {LeujaoamCleansing}")}/" +
              a.Lua($"report(tostring(GetPlayerByID({InstStrayId}):getInstance()))"));

        // Xzinst in Lower Jeuno (B) creates the run there: the callback (not in the entrance zone) sends it straight in
        SeedChar(db, keyBytes, InstId, "Xzinst", LowerJeuno, -30.0f, 0.0f, 65.0f);
        Fixtures(db, InstId, 1);
        var x = h.Enter(b, InstId, "Xzinst", LowerJeuno, 50202, out var login);
        Check("B: Xzinst in Lower Jeuno", "True/245", $"{login?.Exists(pk => Opcode(pk) == 0x00A)}/{x.C?.Zone}");
        h.Settle(12);
        b.Lua($"local p = GetPlayerByID({InstId}) p:setLocalVar('AssaultCap', 50) p:createInstance({LeujaoamCleansing})");
        h.Settle(2);
        Check("B: the run loaded, the char sent into Leujaoam Sanctum (B itself)", $"2/{BIp}/{BPort}", h.Leave(x));
        var r = h.Arrive(x, b, 50202);
        Check("B: in its run at the entry, under the level cap", $"True/69/{LeujaoamCleansing}/1/280/-7.5/35",
              $"{r.Exists(pk => Opcode(pk) == 0x00A)}/{x.C?.Zone}/" +
              b.Lua($"local i = GetPlayerByID({InstId}):getInstance() report(i:getID() .. '/' .. #i:getChars())") + $"/{x.C?.X}/{x.C?.Y}/{x.C?.Z}");
        h.Settle(4);

        // out to Caedarva Mire (A) and back: the run stays on B, registered to the char
        b.Lua($"GetPlayerByID({InstId}):setPos(0, 0, 0, 0, {CaedarvaMire})");
        h.Settle(2);
        Check("leaving the run for Caedarva Mire: the 0x00B names A", "2/127.0.0.1/54230", h.Leave(x));
        Check("the CharZone goes to A", "CharZone->A", h.Take("CharZone"));
        r = h.Arrive(x, a, 50203);
        Check("A: in Caedarva Mire from Leujaoam Sanctum (onZoneIn's spot); B's session closed", "True/79/495.45/0",
              $"{r.Exists(pk => Opcode(pk) == 0x00A)}/{x.C?.Zone}/{x.C?.X}/{b.Sessions.Sessions.Count(s => s.CharId == InstId)}");
        h.Settle(4);
        a.Lua($"GetPlayerByID({InstId}):setPos(0, 0, 0, 0, {LeujaoamSanctum})");
        h.Settle(2);
        Check("A: setPos into Leujaoam Sanctum: the 0x00B names B", $"2/{BIp}/{BPort}", h.Leave(x));
        b.Log.Clear();
        r = h.Arrive(x, b, 50204);
        Check("B: registered to the run, the char finds it again (CZoneInstance::IncreaseZoneCounter)", $"True/69/{LeujaoamCleansing}/1/False",
              $"{r.Exists(pk => Opcode(pk) == 0x00A)}/{x.C?.Zone}/" +
              b.Lua($"local i = GetPlayerByID({InstId}):getInstance() report(i:getID() .. '/' .. #i:getChars())") +
              $"/{b.Log.Exists(t => t.StartsWith("Failed to place Xzinst"))}");

        // Xzistray from A into Leujaoam Sanctum: no run on B; onInstanceLoadFailed's Caedarva Mire and the previous zone
        // are A's, a null zone the C++ dereferences
        a.Lua($"GetPlayerByID({InstStrayId}):setPos(0, 0, 0, 0, {LeujaoamSanctum})");
        h.Settle(2);
        Check("A: Xzistray into Leujaoam Sanctum: the 0x00B names B", $"2/{BIp}/{BPort}", h.Leave(st));
        b.Log.Clear();
        r = h.Arrive(st, b, 50205);
        Check("B: no run for it, put out to Caedarva Mire, which B does not hold: no S2C LOGIN, the home point demanded",
              "False/False/79/HomePoint", $"{r.Exists(pk => Opcode(pk) == 0x00A)}/{st.S?.InZone}/{st.C?.Zone}/{st.C?.RequestedWarp}");
        Check("  logged: the failed placement, the zone B does not hold, the home point", "True/True/True",
              $"{b.Log.Contains("Failed to place Xzistray in Leujaoam_Sanctum (69). Placing them in that zone's instance exit area.")}/" +
              $"{b.Log.Contains("GP_CLI_COMMAND_LOGIN: neither Xzistray's instance exit zone nor its previous zone 79 is on this process (a null zone in the C++)")}/" +
              $"{b.Log.Contains("GP_CLI_COMMAND_LOGIN: dumping player `Xzistray` to homepoint!")}");
        Check("  chars: still Leujaoam Sanctum from Caedarva Mire (SaveCharPosition writes no zone)", "69/79",
              Q1(db, "SELECT CONCAT_WS('/', pos_zone, pos_prevzone) FROM chars WHERE charid = ?", InstStrayId));
        Check("B never loaded A's Caedarva Mire", false, b.Zone.IsZoneLoaded(CaedarvaMire));
    }

    private static void RunShips(IDatabase db, Hub h, MapProc a, MapProc b, Action<long> at)
    {
        Console.WriteLine("17. the Bastok - Jeuno airship: Port Bastok and Port Jeuno on A, the airship's crossing on B:");
        var keyBytes = KeyBytes();
        Check("the run is A's (its dock), B docks nothing (ship_handler's ownsZone)", "True/0",
              $"{a.Zone.TransportNames.Contains("bastok_jeuno_airship")}/{b.Zone.TransportNames.Count}");
        // inside Port Bastok's boarding area 315, with the airship pass
        SeedChar(db, keyBytes, BoardId, "Xzboard", PortBastok, -26.0f, 5.0f, -77.0f);
        Fixtures(db, BoardId, 1, airshipPass: true);
        // under level 30: Abyssea's A Journey Begins would take Port Jeuno's zone-in cutscene
        Sql(db, "UPDATE char_stats SET mlvl = 20 WHERE charid = ?", BoundValue.Of(BoardId));
        Sql(db, "UPDATE char_jobs SET war = 20 WHERE charid = ?", BoundValue.Of(BoardId));
        at(50);
        var x = h.Enter(a, BoardId, "Xzboard", PortBastok, 50301, out var login);
        Check("A: Xzboard in Port Bastok, no event", "True/236/False", $"{login?.Exists(pk => Opcode(pk) == 0x00A)}/{x.C?.Zone}/{x.C?.IsInEvent}");
        // past xi.player.onGameIn's 2.5 s ZoningIn, which keeps a char out of trigger areas
        h.Settle(10);
        Check("in boarding area 315", "true", a.Lua($"report(tostring(GetPlayerByID({BoardId}):isPlayerInTriggerArea(315)))"));
        at(180);
        h.Settle(1);
        Check("departing (t = 180): A's TransportDepart, Port Bastok's onTransportEvent: the boarding cutscene", "71",
              x.C?.IsInEvent == true ? x.C.CurrentEvent.EventId : -1);
        Act(x, EventEnd(BoardId, 71));
        h.Settle(2);
        Check("its end: setPos to the airship's crossing, the 0x00B names B", $"2/{BIp}/{BPort}", h.Leave(x));
        Check("chars: the crossing from Port Bastok; the CharZone to B", "224/236 CharZone->B",
              $"{Q1(db, "SELECT CONCAT_WS('/', pos_zone, pos_prevzone) FROM chars WHERE charid = ?", BoardId)} {h.Take("CharZone")}");
        at(185);
        var r = h.Arrive(x, b, 50302);
        Check("B: aboard (the crossing's onZoneIn spot), A's session closed", "True/224/1/0",
              $"{r.Exists(pk => Opcode(pk) == 0x00A)}/{x.C?.Zone}/{x.C?.Y}/{a.Sessions.Sessions.Count(s => s.CharId == BoardId)}");
        at(300);
        h.Settle(10);
        Check("mid-flight (t = 300): the Bastok leg carries, nobody put ashore", false, x.C?.IsInEvent);
        at(441);
        h.Settle(1);
        Check("arrival (t = 441): B's DisembarkAll, the crossing's onTransportEvent: the arrival cutscene", "100",
              x.C?.IsInEvent == true ? x.C.CurrentEvent.EventId : -1);
        Act(x, EventEnd(BoardId, 100));
        h.Settle(2);
        Check("its end: setPos to Port Jeuno, the 0x00B names A", "2/127.0.0.1/54230", h.Leave(x));
        Check("chars: Port Jeuno from the crossing; the CharZone to A", "246/224 CharZone->A",
              $"{Q1(db, "SELECT CONCAT_WS('/', pos_zone, pos_prevzone) FROM chars WHERE charid = ?", BoardId)} {h.Take("CharZone")}");
        r = h.Arrive(x, a, 50303);
        Check("A: in Port Jeuno at the Bastok berth, Port Jeuno's arrival cutscene (onZoneIn)", "True/246/-50/12/-116/10020",
              $"{r.Exists(pk => Opcode(pk) == 0x00A)}/{x.C?.Zone}/{x.C?.X}/{x.C?.Y}/{x.C?.Z}/" +
              $"{(x.C?.IsInEvent == true ? x.C.CurrentEvent.EventId : -1)}");
        Check("B's session closed (KillSession)", 0, b.Sessions.Sessions.Count(s => s.CharId == BoardId));
    }

    private static void RunAssist(IDatabase db, Hub h, MapProc a, MapProc b)
    {
        Console.WriteLine("18. the Assist Channel across processes (Port Bastok on A, Lower Jeuno on B):");
        var keyBytes = KeyBytes();
        Check("the world's assist endpoints: A and B (each holds an Assist zone)", "True/True",
              $"{h.World.ZoneSettings.AssistMapEndpoints.Any(e => e.Raw == a.Ipp)}/{h.World.ZoneSettings.AssistMapEndpoints.Any(e => e.Raw == b.Ipp)}");
        // a new char on A, a mentor on B
        SeedChar(db, keyBytes, AssistAId, "Xzassa", PortBastok, -26.0f, 5.0f, -60.0f);
        Fixtures(db, AssistAId, 1, assist: true);
        SeedChar(db, keyBytes, AssistBId, "Xzassb", LowerJeuno, -30.0f, 0.0f, 65.0f);
        Fixtures(db, AssistBId, 1, assist: true, mentor: true);
        var x = h.Enter(a, AssistAId, "Xzassa", PortBastok, 50401, out _);
        var y = h.Enter(b, AssistBId, "Xzassb", LowerJeuno, 50402, out _);
        h.Settle(12);
        Inbox(x);
        Inbox(y);
        h.Routed.Clear();
        Act(x, Say(0x23, "assist hi"));
        h.Settle(2);
        Check("A's AssistE: ChatMessageAssist to both processes", "ChatMessageAssist->A,ChatMessageAssist->B", h.Take("ChatMessageAssist"));
        Check("Xzassb on B hears Xzassa (no mentor rank), Xzassa does not hear itself", "35/0/Xzassa/assist hi/none",
              $"{ChatOf(Inbox(y), 0x23)}/{ChatOf(Inbox(x), 0x23)}");
        Act(y, Say(0x22, "mentor hi"));
        h.Settle(2);
        Check("B's AssistJ from a mentor: routed to both", "ChatMessageAssist->A,ChatMessageAssist->B", h.Take("ChatMessageAssist"));
        Check("Xzassa on A hears it: mastery 1 | mentor rank 1 << 8", "34/257/Xzassb/mentor hi", ChatOf(Inbox(x), 0x22));
        // Xzassa's thumbs up for Xzassb, who spoke within 10 minutes
        Act(x, ((ushort)0x0B7, 24, buf =>
        {
            buf[4] = 0x24;
            Encoding.ASCII.GetBytes("Xzassb").CopyTo(buf, 6);
            buf[21] = (byte)' ';
        }));
        h.Settle(2);
        var routed = h.Take("AssistChannelEvent", "MessageStandard", "CharVarUpdate");
        Check("the thumbs up: AssistChannelEvent to B, the answer and the giver's cooldown back to A",
              "AssistChannelEvent->B/True/True", $"{routed.Split(',')[0]}/{routed.Contains("MessageStandard->A")}/{routed.Contains("CharVarUpdate->A")}");
        Check("Xzassb told ReceivedThumbsUp (314), Xzassa GivenThumbsUp (306) naming it", "314:/306:string2 Xzassb",
              $"{MsgsOf(Inbox(y))}/{MsgsOf(Inbox(x))}");
        Check("the giver's cooldown on A (char_vars and the cache), the receiver's count", "1/1/1",
              $"{Q1(db, "SELECT value FROM char_vars WHERE charid = ? AND varname = '[ASSIST][ThumbsUp]Cooldown'", AssistAId)}/" +
              $"{(x.C != null && x.C.CharVars.TryGetValue("[ASSIST][ThumbsUp]Cooldown", out var cv) ? cv.Value : 0)}/" +
              Q1(db, "SELECT value FROM char_vars WHERE charid = ? AND varname = '[ASSIST][Evaluations]Count'", AssistBId));
    }
}
