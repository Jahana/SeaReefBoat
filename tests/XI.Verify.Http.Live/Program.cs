using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using XI.Login;
using XI.Persistence;
using XI.World;

// XI.Verify.Http.Live -- live gate for the ported world HTTP API against the real MariaDB (docker xi-live-db,
// host:3307, xidb). Exit codes: 0 = pass; 1 = a check failed; 3 = DB not reachable.
internal static class Program
{
    private static int _pass, _fail;

    private const uint C1 = 990601, C2 = 990602, C3 = 990603;

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static void Sql(IDatabase db, string q, params object[] a) => LoginDb.PreparedStmt(db, q, a);

    // every table with a charid column, read from the schema at run time: chars' char_insert trigger fills char_equip, char_exp,
    // char_history, char_inventory, char_jobs, char_pet, char_points, char_profile, char_storage and char_unlocks and only
    // char_delete clears them, so a run aborted with one of them left behind would make the next run's INSERT INTO chars collide
    private static List<string> CharidTables(IDatabase db)
    {
        var tables = new List<string>();
        var r = LoginDb.PreparedStmt(db, "SELECT table_name AS t FROM information_schema.columns WHERE table_schema = DATABASE() AND column_name = 'charid' AND table_name <> 'chars'");
        while (r != null && r.Next()) tables.Add(r.Get<string>("t"));
        foreach (var t in new[] { "char_equip", "char_exp", "char_history", "char_inventory", "char_jobs", "char_pet", "char_points", "char_profile",
                                  "char_storage", "char_unlocks" })
            if (!tables.Contains(t)) tables.Add(t);
        return tables;
    }

    private static void Cleanup(IDatabase db)
    {
        foreach (var c in new[] { C1, C2, C3 })
        {
            foreach (var t in CharidTables(db)) Sql(db, $"DELETE FROM `{t}` WHERE charid = ?", c);
            Sql(db, "DELETE FROM chars WHERE charid = ?", c);
            Sql(db, "DELETE FROM accounts WHERE id = ?", c);
        }
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
        catch (Exception ex) { Console.WriteLine($"DB not reachable ({ex.GetType().Name}: {ex.Message})"); return 3; }

        try
        {
            Cleanup(db);
            Run(db);
        }
        catch (Exception ex) { _fail++; Console.WriteLine($"FAIL exception {ex}"); }
        finally { Cleanup(db); }

        Console.WriteLine($"{_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    private static void Run(IDatabase db)
    {
        var cfg = new Dictionary<string, object>
        {
            ["main.SERVER_NAME"] = "LandSandBoat",
            ["main.EXP_RATE"] = 1.0,
            ["main.HALF"] = 0.5,
            ["main.ENABLE_THING"] = true,
            ["main.MOTD"] = "café <b>",
            ["network.SQL_PASSWORD"] = "secret",
            ["logging.LEVEL"] = "debug",
            ["main.ADMIN_PASSWORD_HASH"] = "hidden",
            ["main.DB_PASSWORD"] = "hidden",
            ["Main.Zeta"] = "z",
        };

        long now = 1_000_000;
        var logs = new List<string>();
        var api = new HttpApi(db, () => cfg, () => now, logs.Add);
        Check("constructor primes cache (logged)", true, logs.Contains("API data is stale. Updating..."));

        Check("/api", "200 text/plain Hello LSB API", Fmt(api.Get("/api")));
        var baseSessions = uint.Parse(api.Get("/api/sessions").Body);
        var baseIps = uint.Parse(api.Get("/api/ips").Body);
        var baseZones = JsonSerializer.Deserialize<uint[]>(api.Get("/api/zones").Body);
        Check("/api/zones length", 300, baseZones.Length);
        Check("/api/sessions content type", "application/json", api.Get("/api/sessions").ContentType);

        // seed three sessions: two share client_addr 777001, one differs; zones 230, 230, 231
        var rows = new (uint c, string n, uint ip, ushort zone)[] { (C1, "Htaa", 777001, 230), (C2, "Htbb", 777001, 230), (C3, "Htcc", 777002, 231) };
        foreach (var r in rows)
        {
            Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)", r.c, "ht" + r.n, "x", (byte)1);
            Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,nation,gmlevel) VALUES (?,?,?,?,?,?,?)", r.c, r.c, r.n, r.zone, r.zone, (byte)1, (byte)0);
            Sql(db, "INSERT INTO accounts_sessions(accid,charid,client_addr) VALUES (?,?,?)", r.c, r.c, r.ip);
        }

        now += 59_000;
        Check("cache not refreshed inside 60s", baseSessions, uint.Parse(api.Get("/api/sessions").Body));

        now += 2_000; // 61s after the last update
        Check("sessions refreshed after 60s", baseSessions + 3, uint.Parse(api.Get("/api/sessions").Body));
        Check("unique ips +2", baseIps + 2, uint.Parse(api.Get("/api/ips").Body));
        var zones = JsonSerializer.Deserialize<uint[]>(api.Get("/api/zones").Body);
        Check("zone 230 +2", baseZones[230] + 2, zones[230]);
        Check("zone 231 +1", baseZones[231] + 1, zones[231]);
        Check("/api/zones/230", zones[230].ToString(), api.Get("/api/zones/230").Body);
        Check("/api/zones/231", zones[231].ToString(), api.Get("/api/zones/231").Body);

        Check("zone 0 -> 404", 404, api.Get("/api/zones/0").Status);
        Check("zone 300 -> 404", 404, api.Get("/api/zones/300").Status);
        Check("zone 65536 wraps to 0 -> 404", 404, api.Get("/api/zones/65536").Status);
        Check("zone 299 ok", 200, api.Get("/api/zones/299").Status);
        Check("unknown route 404 page", "404 text/html <p>Error Status: <span style='color:red;'>404 (Not Found)</span></p>", Fmt(api.Get("/api/nope")));

        var settingsBody = api.Get("/api/settings").Body;
        Check("settings filtered + sorted + typed",
            "{\"Main.Zeta\":\"z\",\"main.ENABLE_THING\":true,\"main.EXP_RATE\":1.0,\"main.HALF\":0.5,\"main.MOTD\":\"caf?? <b>\",\"main.SERVER_NAME\":\"LandSandBoat\"}",
            settingsBody);

        // real HTTP over HttpListener
        ushort port = 38081;
        using (var host = new HttpApiHost(api, "127.0.0.1", port))
        using (var http = new HttpClient())
        {
            var r1 = http.GetAsync($"http://127.0.0.1:{port}/api").Result;
            Check("http /api status", 200, (int)r1.StatusCode);
            Check("http /api body", "Hello LSB API", r1.Content.ReadAsStringAsync().Result);
            var r2 = http.GetAsync($"http://127.0.0.1:{port}/api/zones/230").Result;
            Check("http zone 230", zones[230].ToString(), r2.Content.ReadAsStringAsync().Result);
            Check("http zone content-type", "application/json", r2.Content.Headers.ContentType.MediaType);
            var r3 = http.GetAsync($"http://127.0.0.1:{port}/api/zones/0").Result;
            Check("http zone 0 status", 404, (int)r3.StatusCode);
        }
    }

    private static string Fmt(HttpResponse r) => $"{r.Status} {r.ContentType} {r.Body}";
}
