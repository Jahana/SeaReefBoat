using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using MySqlConnector;
using XI.Bots;

// XI.Verify.Bots.Live -- a load / soak run of the whole C# stack against bots that speak the real protocol
// (tools/XI.Bots): XI.Host world, search, profile, login and two map processes as child processes, on a port block of
// their own (55xxx) and a database of their own (xidb_bots, made fresh from xidb's schema and static data each run).
//
//   1. xidb_bots: every xidb table (CREATE TABLE LIKE), the static rows (accounts, chars and the other player tables
//      left empty), the triggers; zone_settings: West Ronfaure (100) on map A 127.0.0.1:55232, Northern San d'Oria
//      (231) on map B 127.0.0.1:55233, every other zone on 55299 (nothing listens there)
//   2. the stack: world (router 55003, no HTTP), search 55002, profile 55220 / IRC 55240, map A, map B, login (auth
//      55231, data 55230, view 55001, its 0x0B naming search 55002); every port checked free first
//   3. N bots (default 20) at once: auth create + login, the lobby's name check and char create
//   4. their chars moved to West Ronfaure (pos 0,0,0: the zone's onZoneIn places them)
//   5. N bots at once: auth login, lobby select, the map login; seeing each other (0x00D), say / shout / a tell round
//      the ring, an emote each, /sea; a soak of walking and chatter; the zone line to Northern San d'Oria (map B) and
//      back; /logout
//   6. afterwards: no accounts_sessions row left, no exception or error line in any server log, the map tick and the
//      memory within bounds; the round trip and tick numbers
//
//   XI.Verify.Bots.Live [bots=20] [soak=60] [laps=1] [tick=300] [keepdb=1] [prefetch=0: the maps load chars in the tick]
// The last line is "N passed, M failed"; exit 0 only when nothing failed (3 = the database is not reachable).
internal static class Program
{
    private static int _pass, _fail;

    private const string Db = "xidb_bots", Source = "xidb";
    private const int DbPort = 3307;
    private const int World = 55003, Search = 55002, Profile = 55220, Irc = 55240, MapA = 55232, MapB = 55233,
                      Auth = 55231, Data = 55230, View = 55001, Nowhere = 55299;
    private const ushort WestRonfaure = 100, NorthernSandoria = 231;

    private static void Check(string name, bool ok, string detail = "")
    {
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (detail.Length > 0 ? $"  ({detail})" : ""));
    }

    private static string FindRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var cand = d.FullName;
            if (File.Exists(Path.Combine(cand, "data", "grades.yaml"))) return cand;
        }
        throw new DirectoryNotFoundException("data (grades.yaml) not found above " + AppContext.BaseDirectory);
    }

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

    private static int Main(string[] argv)
    {
        var o = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in argv)
        {
            int eq = a.IndexOf('=');
            if (eq > 0) o[a.Substring(0, eq)] = a.Substring(eq + 1);
        }
        int Arg(string k, int d) => o.TryGetValue(k, out var v) ? int.Parse(v, CultureInfo.InvariantCulture) : d;
        int bots = Arg("bots", 20), soak = Arg("soak", 60), laps = Arg("laps", 1), tick = Arg("tick", 300), prefetch = Arg("prefetch", 1);

        string admin = $"Server=127.0.0.1;Port={DbPort};User ID=root;Password=root;AllowUserVariables=true;Default Command Timeout=120";
        try
        {
            using var c = new MySqlConnection(admin);
            c.Open();
        }
        catch (Exception e)
        {
            Console.WriteLine($"DB not reachable ({e.GetType().Name}: {e.Message})");
            return 3;
        }

        var procs = new List<HostProc>();
        var sw = Stopwatch.StartNew();
        try
        {
            Console.WriteLine($"1. {Db} from {Source}'s schema and static data");
            MakeDatabase(admin);

            Console.WriteLine("2. the stack on its own ports");
            var host = FindHost();
            Check("XI.Host.dll built beside the suite", host != null);
            if (host == null) return Finish();
            string root = FindRoot();
            var ports = new[] { World, Search, Profile, Irc, MapA, MapB, Auth, Data, View };
            Check("no port of the run is a live one", ports.All(p => !Endpoints.Forbidden.Contains(p)));
            var busy = ports.Where(p => !PortFree(p)).ToList();
            Check("every port of the run is free", busy.Count == 0, busy.Count == 0 ? "" : "busy: " + string.Join(",", busy));
            if (busy.Count > 0) return Finish();

            string logDir = Path.Combine(AppContext.BaseDirectory, "bots-logs");
            Directory.CreateDirectory(logDir);
            string common = $"root=\"{root}\" db.port={DbPort} db.name={Db} ipc=tcp://127.0.0.1:{World} bind=127.0.0.1 roe=1";
            HostProc Start(string name, string args)
            {
                var p = new HostProc(name, host, root, args, Path.Combine(logDir, name + ".log"));
                procs.Add(p);
                return p;
            }
            var world = Start("world", $"world {common} http=0 dailytally=0");
            var search = Start("search", $"search {common} search={Search} expire=0");
            var profile = Start("profile", $"profile {common} profile={Profile} irc={Irc} auth.ip=127.0.0.1");
            var mapA = Start("mapA", $"map {common} ip=127.0.0.1 port={MapA} stats=5 prefetch={prefetch}");
            var mapB = Start("mapB", $"map {common} ip=127.0.0.1 port={MapB} stats=5 prefetch={prefetch}");
            var login = Start("login", $"login {common} auth={Auth} data={Data} view={View} self=127.0.0.1:{Auth} search={Search}");
            bool up = WaitFor(() => world.Saw("IPC router bound") && search.Saw($"listening on {Search}") && profile.Saw($"listening on {Profile}") &&
                                    mapA.Saw($"map: UDP {MapA}") && mapB.Saw($"map: UDP {MapB}") && login.Saw($"auth {Auth}"), 180_000);
            Check("world, search, profile, two maps and login up", up, $"{sw.Elapsed.TotalSeconds:0} s");
            Check("each map holds its one zone", mapA.Saw("Zones assigned to this process: 1") && mapB.Saw("Zones assigned to this process: 1"));
            if (!up)
            {
                foreach (var p in procs)
                    foreach (var l in p.Lines.TakeLast(8)) Console.WriteLine($"    {p.Name} | {l}");
                return Finish();
            }
            foreach (var p in procs) p.MarkBaseline();

            var cfg = new SwarmConfig
            {
                Endpoints = new Endpoints { Host = "127.0.0.1", Auth = Auth, Data = Data, View = View },
                Bots = bots, SoakSeconds = soak, Laps = laps, TickMs = tick,
                Log = s => { if (o.ContainsKey("verbose")) Console.WriteLine("    " + s); },
            };
            var swarm = new Swarm(cfg);
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(20));

            Console.WriteLine($"3. {bots} bots: accounts and chars through auth and the lobby");
            var t0 = sw.Elapsed;
            swarm.Create(cts.Token).GetAwaiter().GetResult();
            int created = swarm.Bots.Count(b => b.CharId != 0);
            Check("every bot made its account and char", created == bots, $"{created}/{bots} in {(sw.Elapsed - t0).TotalSeconds:0.0} s");
            Check("the chars are in xidb_bots", Count(admin, $"SELECT COUNT(*) FROM {Db}.chars WHERE charname LIKE 'Xibot%'") == bots);

            Console.WriteLine("4. the chars to West Ronfaure");
            Exec(admin, $"UPDATE {Db}.chars SET pos_zone = {WestRonfaure}, pos_prevzone = {WestRonfaure}, pos_x = 0, pos_y = 0, pos_z = 0, pos_rot = 0 " +
                        "WHERE charname LIKE 'Xibot%'");

            Console.WriteLine($"5. {bots} bots in game (soak {soak} s, {laps} zone lap(s))");
            var sampler = new MemorySampler(procs);
            t0 = sw.Elapsed;
            swarm.Play(cts.Token).GetAwaiter().GetResult();
            var played = sw.Elapsed - t0;
            sampler.Stop();

            // every bot check, by name across the bots
            var names = swarm.Bots.SelectMany(b => b.Checks.Keys).Distinct().ToList();
            foreach (var name in names)
            {
                var failed = swarm.Bots.Where(b => b.Checks.TryGetValue(name, out var r) && !r.Ok).ToList();
                int had = swarm.Bots.Count(b => b.Checks.ContainsKey(name));
                Check($"bots: {name}", failed.Count == 0 && had == bots,
                      $"{had - failed.Count}/{bots}" + string.Concat(failed.Take(3).Select(b => $"; {b.CharName}: {b.Checks[name].Detail}")));
            }
            var stopped = swarm.Bots.Where(b => b.Failure != null).ToList();
            Check("no bot stopped early", stopped.Count == 0, string.Join("; ", stopped.Take(5).Select(b => $"{b.CharName}: {b.Failure}")));
            Check("every bot zoned out and back", swarm.Bots.All(b => b.ZoneIns >= 1 + 2 * laps), string.Join(",", swarm.Bots.Select(b => b.ZoneIns).Distinct()));

            Console.WriteLine("6. afterwards");
            Thread.Sleep(3000);
            long sessionsLeft = Count(admin, $"SELECT COUNT(*) FROM {Db}.accounts_sessions");
            Check("no accounts_sessions row left", sessionsLeft == 0, $"{sessionsLeft} rows");
            Check("no char left zoning (char_stats.zoning)", Count(admin, $"SELECT COUNT(*) FROM {Db}.char_stats WHERE zoning <> 0") == 0);
            Check("every process still running", procs.All(p => !p.P.HasExited), string.Join(",", procs.Where(p => p.P.HasExited).Select(p => p.Name)));
            foreach (var p in procs)
            {
                var bad = p.ErrorLines();
                Check($"{p.Name}: no exception or error line", bad.Count == 0,
                      bad.Count == 0 ? $"{p.Lines.Count} lines" : $"{bad.Count}: " + string.Join(" | ", bad.Take(4)));
            }

            // the map tick and memory: what MapStatistics would report, from the "map: stats" lines of the play phase
            foreach (var m in new[] { mapA, mapB })
            {
                var stats = m.Stats();
                if (stats.Count == 0)
                {
                    Check($"{m.Name}: tick statistics", false, "no map: stats line");
                    continue;
                }
                double p50 = stats.Average(s => s.P50), p99 = stats.Max(s => s.P99), max = stats.Max(s => s.Max);
                long heapFirst = stats[0].HeapMb, heapLast = stats[^1].HeapMb, wsPeak = stats.Max(s => s.WsMb);
                // a steady window: nobody logging in, out or zoning (every session has its char, none pending, the same
                // count as the two windows before and the one after: a logout's session closes after the line that counted
                // it, and a herd's zone-ins still have their game-OK packets and zoneutils::AfterZoneIn, 4 s on, in the
                // window after its last login); the others hold a login or logout herd, whose ticks wait on the database
                // (the saves are synchronous queries in the C++ too; the loads read ahead on a worker unless prefetch=0)
                var steady = stats.Where((s, i) => i > 1 && i < stats.Count - 1 && s.Pending == 0 && s.Sessions == s.Chars &&
                                                   s.Sessions == stats[i - 1].Sessions && s.Sessions == stats[i - 2].Sessions &&
                                                   s.Sessions == stats[i + 1].Sessions).ToList();
                var herd = stats.Except(steady).ToList();
                double steadyP99 = steady.Count == 0 ? 0 : steady.Max(s => s.P99), steadyMax = steady.Count == 0 ? 0 : steady.Max(s => s.Max);
                double herdMax = herd.Count == 0 ? 0 : herd.Max(s => s.Max);
                Check($"{m.Name}: map tick bounded while steady", steady.Count > 0 && steadyP99 < 25 && steadyMax < 250,
                      FormattableString.Invariant($"tick p50 {p50:0.00} ms; {steady.Count} steady windows: p99 {steadyP99:0.00} ms, max {steadyMax:0.0} ms; {stats.Sum(s => s.Ticks)} ticks"));
                // the C++ watchdog (INACTIVITY_WATCHDOG_PERIOD, 2000 ms) would kill a map stalled that long, but only
                // between ticks: a herd of N logins in one tick is N loads long there as well (about 200 ms each here, on
                // docker). With the loads' reads ahead (MapNetwork.PrefetchLogin) the held logins go a budget's worth per
                // tick; what is left is the first login's zone (loaded on first use) and the zone-out and logout saves
                double herdBound = prefetch != 0 ? Math.Max(4000, bots * 60) : Math.Max(10000, bots * 300);
                Check($"{m.Name}: map tick bounded in a herd", herdMax < herdBound,
                      FormattableString.Invariant($"{herd.Count} herd windows: worst p99 {(herd.Count == 0 ? 0 : herd.Max(s => s.P99)):0.00} ms, max {herdMax:0.0} ms"));
                Check($"{m.Name}: memory bounded", wsPeak < 3000 && heapLast < Math.Max(2 * heapFirst, heapFirst + 300),
                      $"heap {heapFirst} -> {heapLast} MB, working set peak {wsPeak} MB, dropped datagrams {stats[^1].Dropped}");
            }
            foreach (var p in procs)
                Console.WriteLine($"     {p.Name,-7} working set {sampler.First(p)} -> {sampler.Last(p)} MB (peak {sampler.Peak(p)} MB)");

            var rt = swarm.RoundTrips.ToList();
            Check("round trips measured", rt.Count > bots * 100, $"{rt.Count}");
            Console.WriteLine(FormattableString.Invariant(
                $"     {bots} bots, play {played.TotalSeconds:0} s (whole run {sw.Elapsed.TotalSeconds:0} s): {swarm.Summary()}"));
        }
        catch (Exception e)
        {
            Check("the run", false, e.ToString());
        }
        finally
        {
            foreach (var p in procs) p.Dispose();
            if (!o.ContainsKey("keepdb")) { /* xidb_bots stays for a look; the next run makes it afresh */ }
        }
        return Finish();
    }

    private static int Finish()
    {
        Console.WriteLine($"{_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    private static bool PortFree(int port)
    {
        try
        {
            var t = new TcpListener(IPAddress.Loopback, port);
            t.Start();
            t.Stop();
            using var u = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            return true;
        }
        catch (SocketException) { return false; }
    }

    private static bool WaitFor(Func<bool> cond, int ms)
    {
        var until = DateTime.UtcNow.AddMilliseconds(ms);
        while (DateTime.UtcNow < until)
        {
            if (cond()) return true;
            Thread.Sleep(100);
        }
        return cond();
    }

    // ---------------------------------------------------------------- the database

    private static void Exec(string cs, string sql)
    {
        using var c = new MySqlConnection(cs);
        c.Open();
        using var cmd = new MySqlCommand(sql, c);
        cmd.ExecuteNonQuery();
    }

    private static long Count(string cs, string sql)
    {
        using var c = new MySqlConnection(cs);
        c.Open();
        using var cmd = new MySqlCommand(sql, c);
        return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    // player tables: the schema only
    private static bool PlayerTable(string t) =>
        t.StartsWith("accounts") || t.StartsWith("account_") || t.StartsWith("char_") || t == "chars" || t.StartsWith("audit_") ||
        t is "auction_house" or "delivery_box" or "linkshells" or "linkshell_concierge" or "help_desk" or "ip_exceptions" or
             "fishing_contest_entries" or "chocobo_race_quills" or "bcnm_records";

    // xidb is only read, at READ COMMITTED so the copy takes no row locks the live server could wait on
    private static void MakeDatabase(string admin)
    {
        var watch = Stopwatch.StartNew();
        using var c = new MySqlConnection(admin);
        c.Open();
        void Run(string sql)
        {
            using var cmd = new MySqlCommand(sql, c);
            cmd.ExecuteNonQuery();
        }
        List<string> Column(string sql)
        {
            var list = new List<string>();
            using var cmd = new MySqlCommand(sql, c);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(r.GetString(0));
            return list;
        }
        if (Db == Source) throw new InvalidOperationException("refusing to write the source database");
        Run("SET SESSION TRANSACTION ISOLATION LEVEL READ COMMITTED");
        Run($"DROP DATABASE IF EXISTS `{Db}`");
        Run($"CREATE DATABASE `{Db}`");
        var tables = Column($"SELECT table_name FROM information_schema.tables WHERE table_schema = '{Source}' AND table_type = 'BASE TABLE' ORDER BY table_name");
        int copied = 0;
        foreach (var t in tables)
        {
            Run($"CREATE TABLE `{Db}`.`{t}` LIKE `{Source}`.`{t}`");
            if (PlayerTable(t)) continue;
            Run($"INSERT INTO `{Db}`.`{t}` SELECT * FROM `{Source}`.`{t}`");
            copied++;
        }
        // the triggers (char_insert's rows, session_delete's zoning reset, ...), created in xidb_bots
        var triggers = Column($"SELECT trigger_name FROM information_schema.triggers WHERE trigger_schema = '{Source}'");
        var bodies = new List<string>();
        foreach (var tr in triggers)
        {
            using var cmd = new MySqlCommand($"SHOW CREATE TRIGGER `{Source}`.`{tr}`", c);
            using var r = cmd.ExecuteReader();
            if (r.Read()) bodies.Add(r.GetString(r.GetOrdinal("SQL Original Statement")));
        }
        Run($"USE `{Db}`");
        foreach (var body in bodies) Run(body);

        // the zones: the two of the run on the two maps, every other one where nothing listens
        Run($"UPDATE zone_settings SET zoneip = '127.0.0.1', zoneport = {Nowhere} WHERE zoneport <> 0");
        Run($"UPDATE zone_settings SET zoneport = {MapA} WHERE zoneid = {WestRonfaure}");
        Run($"UPDATE zone_settings SET zoneport = {MapB} WHERE zoneid = {NorthernSandoria}");
        long live = Count(admin, $"SELECT COUNT(*) FROM `{Db}`.zone_settings WHERE zoneport IN (54230, 54001, 54002, 54003, 54231)");
        Check($"{Db}: {tables.Count} tables ({copied} with rows), {bodies.Count} triggers", tables.Count > 100 && bodies.Count == triggers.Count,
              $"{watch.ElapsedMilliseconds} ms");
        Check($"{Db}: no zone points at a live port", live == 0);
    }

    // ---------------------------------------------------------------- the processes

    private sealed class HostProc : IDisposable
    {
        public readonly string Name;
        public readonly Process P;
        public readonly ConcurrentQueue<string> Lines = new ConcurrentQueue<string>();
        private readonly StreamWriter _file;
        private int _baseline;

        public HostProc(string name, string dll, string root, string args, string logFile)
        {
            Name = name;
            _file = new StreamWriter(logFile, false) { AutoFlush = true };
            P = new Process
            {
                StartInfo = new ProcessStartInfo("dotnet", $"\"{dll}\" {args}")
                {
                    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
                    WorkingDirectory = root, // res/badnames.bin and the other root-relative paths
                },
            };
            // no XI_* setting of the caller's environment reaches the stack, but the tests' defaults-only switch
            foreach (var k in P.StartInfo.Environment.Keys.Where(k => k.StartsWith("XI_", StringComparison.OrdinalIgnoreCase) &&
                     k != "XI_SETTINGS_DEFAULTS_ONLY").ToList())
                P.StartInfo.Environment.Remove(k);
            P.OutputDataReceived += (_, e) => Add(e.Data, "");
            P.ErrorDataReceived += (_, e) => Add(e.Data, "[stderr] ");
            P.Start();
            P.BeginOutputReadLine();
            P.BeginErrorReadLine();
        }

        private void Add(string line, string tag)
        {
            if (line == null) return;
            Lines.Enqueue(tag + line);
            lock (_file) _file.WriteLine(tag + line);
        }

        public bool Saw(string text) => Lines.Any(l => l.Contains(text));

        // the start-up lines are not the run's
        public void MarkBaseline() => _baseline = Lines.Count;

        private static readonly Regex Bad = new Regex(
            @"exception|error|query failed|\bfail|unhandled|invalid|bad packet|traceback|stack overflow|nullreference|lua:|\[stderr\]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // lines that look bad but are the protocol working as it should:
        //   a lobby socket the client closed, or the server did after the 0x0B (handler_session::do_read's read error:
        //   DebugSockets, not an error)
        private static readonly Regex Benign = new Regex(
            @"login: (view|data) [\d.]+: ended: (IOException|ObjectDisposedException)",
            RegexOptions.Compiled);

        public List<string> ErrorLines() =>
            Lines.Skip(_baseline).Where(l => Bad.IsMatch(l) && !Benign.IsMatch(l)).Distinct().ToList();

        private static readonly Regex StatsLine = new Regex(
            @"map: stats ticks (\d+) tick_ms p50 ([\d.]+) p99 ([\d.]+) max ([\d.]+) datagrams_in (\d+) dropped (\d+) sessions (\d+) pending (\d+) chars (\d+) heap_mb (\d+) ws_mb (\d+)",
            RegexOptions.Compiled);

        public List<(long Ticks, double P50, double P99, double Max, long Dropped, long Sessions, long Pending, long Chars, long HeapMb, long WsMb)> Stats()
        {
            var list = new List<(long, double, double, double, long, long, long, long, long, long)>();
            foreach (var l in Lines.Skip(_baseline))
            {
                var m = StatsLine.Match(l);
                if (!m.Success) continue;
                double D(int i) => double.Parse(m.Groups[i].Value, CultureInfo.InvariantCulture);
                long L(int i) => long.Parse(m.Groups[i].Value, CultureInfo.InvariantCulture);
                list.Add((L(1), D(2), D(3), D(4), L(6), L(7), L(8), L(9), L(10), L(11)));
            }
            return list;
        }

        public void Dispose()
        {
            try { if (!P.HasExited) P.Kill(true); } catch (InvalidOperationException) { }
            P.WaitForExit(5000);
            P.Dispose();
            lock (_file) _file.Dispose();
        }
    }

    // each process's working set every second through the play phase
    private sealed class MemorySampler
    {
        private readonly Dictionary<HostProc, List<long>> _mb = new Dictionary<HostProc, List<long>>();
        private readonly Thread _thread;
        private volatile bool _stop;

        public MemorySampler(List<HostProc> procs)
        {
            foreach (var p in procs) _mb[p] = new List<long>();
            _thread = new Thread(() =>
            {
                while (!_stop)
                {
                    foreach (var (p, list) in _mb)
                    {
                        try
                        {
                            p.P.Refresh();
                            lock (list) list.Add(p.P.WorkingSet64 / (1024 * 1024));
                        }
                        catch (InvalidOperationException) { }
                    }
                    Thread.Sleep(1000);
                }
            }) { IsBackground = true };
            _thread.Start();
        }

        public void Stop()
        {
            _stop = true;
            _thread.Join(3000);
        }

        private long Pick(HostProc p, Func<List<long>, long> f)
        {
            var l = _mb[p];
            lock (l) return l.Count == 0 ? -1 : f(l);
        }

        public long First(HostProc p) => Pick(p, l => l[0]);
        public long Last(HostProc p) => Pick(p, l => l[^1]);
        public long Peak(HostProc p) => Pick(p, l => l.Max());
    }
}
