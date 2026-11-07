using System;
using System.Collections.Generic;
using System.IO;
using XI.Common;
using XI.Map;
using XI.Ipc;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.ZoneInit.Live -- live gate for every zone's onInitialize (luautils::OnZoneInitialize, zoneutils::LoadZoneList)
// with the zone Lua on: each zone this server runs (zone_settings with an IP) loads, its NPCs and mobs are in, and its
// onInitialize (and the mob / NPC scripts it reaches) run with no script error. With the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    // the bindings whose systems are still being ported. A zone that fails only on one of these is listed, not failed;
    // drop each entry once its system lands. None are left: follow, setLink, isInDynamis and getTargID landed with
    // the mob follow port (ZoneHost.MobFollow.cs).
    private static readonly string[] KnownGaps = { };

    private static ResultSet Sql(IDatabase db, string q, params BoundValue[] a) => db.Execute(q, a);

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

        try
        {
            Run(db);
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FAIL exception: {ex}");
        }

        Console.WriteLine($"{_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        zone.VanaTicksNow = () => 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        zone.MobRng = new Random(5);
        zone.SendIpc = _ => { };
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);
        Check("the scripts load", false, log.Exists(l => l.StartsWith("lua: ") && !l.Contains(" 0 load errors")));

        var zones = new List<(ushort Id, string Name)>();
        var r = Sql(db, "SELECT zoneid, name FROM zone_settings WHERE zoneip <> '' AND zoneip <> '0' ORDER BY zoneid");
        while (r != null && r.Next()) zones.Add((r.Get<ushort>("zoneid"), r.Get<string>("name")));
        Check("zones to load", true, zones.Count > 200);

        var tally = new SortedDictionary<string, int>();
        int known = 0;
        foreach (var (id, name) in zones)
        {
            int before = log.Count;
            string error = null;
            try
            {
                zone.ZoneInfoOf(id);
            }
            catch (Exception ex)
            {
                error = $"{ex.GetType().Name}: {ex.Message}";
            }
            var errors = log.GetRange(before, log.Count - before).FindAll(l => l.StartsWith("lua: "));
            if (error == null && errors.Count > 0) error = errors[0].Split('\n')[0];
            if (error != null)
            {
                string key = System.Text.RegularExpressions.Regex.Replace(error, @"^lua: [^:]*: |\./scripts/zones/[^/]*/", "");
                tally[key] = tally.GetValueOrDefault(key) + 1;
            }
            if (error != null && Array.Exists(KnownGaps, g => error.EndsWith("binding not ported: " + g)))
            {
                known++;
                continue;
            }
            Check($"{id} {name}: onInitialize with no script error", "", error ?? "");
        }
        foreach (var (k, v) in tally) Console.WriteLine($"  {v,3}  {k}");
        Console.WriteLine($"  {known} zones stop on a known gap ({string.Join(", ", KnownGaps)})");
    }
}
