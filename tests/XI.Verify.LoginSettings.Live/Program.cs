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
using System.Threading.Tasks;
using XI.Bots;
using XI.Common;
using XI.Ipc;
using XI.Login;
using XI.Persistence;
using XI.World;

// XI.Verify.LoginSettings.Live -- the login server's settings as src/login reads them (settings::get<T> over
// settings/default/*.lua, settings/*.lua and the XI_<FILE>_<KEY> environment variables).
//
//   1. offline: XI.Login.LoginConfig.From over the server's settings against an independent scrape of the default files,
//      key by key with the C++ call site's type; the env overrides as settings.cpp applies them (an all-digit value a
//      number, anything else a string read with settings::get's truthiness) and the get<T> conversions (uint8 narrowing,
//      a missing key's T()); login.BANNED_WORDS_LIST, the one table the C++ reads from Lua
//   2. live: XI.Host login launched by this suite on ports of its own (auth 57231, data 57230, view 57001, the world
//      router 57003 which this suite binds, search 57002), root = the server tree, the docker MariaDB on 3307, and only
//      XI_* environment overrides (never the live login): the defaults (the world list name, the free slots, the name
//      checks, VER_LOCK 2 refusing an older client, the routing id LOGIN_AUTH_IP:LOGIN_AUTH_PORT); MAINT_MODE refusing a
//      non-GM's lobby and the name server while a GM still selects; ACCOUNT_CREATION off; CHARACTER_CREATION off (no
//      free slot, the name server refuses); VER_LOCK 0; LOGIN_LIMIT from a client address of its own (127.0.0.77)
//      refusing a second session while a GM passes; LOG_USER_IP; DISABLE_MOB_NPC_CHAR_NAMES
//
// Accounts and chars 997701-997703 only (logins / names "lset*"). Exit codes: 0 = all checks passed; 1 = a check failed;
// 3 = DB not reachable.
internal static class Program
{
    private static int _pass, _fail;

    private const int DbPort = 3307;
    private const int Auth = 57231, Data = 57230, View = 57001, Router = 57003, Search = 57002;
    private const uint AccPlayer = 997701, AccOther = 997702, AccGm = 997703;
    private const uint CharPlayer = 997701, CharOther = 997702, CharGm = 997703;
    private const string Password = "lsetpass1";
    private const string OwnClient = "127.0.0.77";
    private const ushort Zone = 230; // Southern San d'Oria
    private static readonly int[] Forbidden = { 51220, 51240, 54001, 54002, 54003, 54230, 54231, 3306 };

    private static void Check(string name, bool ok, string detail = "")
    {
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (!ok && detail.Length > 0 ? $"  ({detail})" : ""));
    }

    private static void Eq(string name, object expected, object actual) =>
        Check(name, Equals(expected?.ToString(), actual?.ToString()), $"expected=[{expected}] actual=[{actual}]");

    private static string FindRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var cand = d.FullName;
            if (File.Exists(Path.Combine(cand, "settings", "default", "login.lua"))) return cand;
        }
        throw new DirectoryNotFoundException("settings/default/login.lua not found above " + AppContext.BaseDirectory);
    }

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
        int dbPort = argv.Length > 0 ? int.Parse(argv[0]) : DbPort;
        if (dbPort == 3306) { Console.WriteLine("refusing the host MySQL on 3306"); return 3; }
        string root = FindRoot();

        Offline(root);

        using var db = new MySqlDatabase(new NetworkSettings { SqlHost = "127.0.0.1", SqlPort = dbPort, SqlLogin = "root", SqlPassword = "root", SqlDatabase = "xidb" });
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

        Cleanup(db);
        try
        {
            Seed(db);
            Live(db, root, dbPort);
        }
        finally
        {
            Cleanup(db);
        }

        Console.WriteLine($"{_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    // ---------------------------------------------------------------- offline: the key table

    // an independent read of `KEY = value` in settings/default/<file>.lua
    private static object Scrape(string root, string key)
    {
        int dot = key.IndexOf('.');
        string file = Path.Combine(root, "settings", "default", key.Substring(0, dot) + ".lua"), name = key.Substring(dot + 1);
        foreach (var l in File.ReadAllLines(file))
        {
            var m = Regex.Match(l, @"^\s*" + name + @"\s*=\s*('([^']*)'|""([^""]*)""|true|false|-?[0-9.]+)\s*,?\s*(--.*)?$");
            if (!m.Success) continue;
            string v = m.Groups[1].Value;
            if (v == "true") return true;
            if (v == "false") return false;
            if (m.Groups[2].Success) return m.Groups[2].Value;
            if (m.Groups[3].Success) return m.Groups[3].Value;
            return double.Parse(v, CultureInfo.InvariantCulture);
        }
        return null;
    }

    // settings::get<T> of a scraped value
    private static bool B(object v) => v is bool b ? b : v is double d ? d != 0 : v is string s && s.Length > 0 && s != "false" && s != "0";
    private static uint U(object v) => v is bool b ? (b ? 1u : 0u) : v is double d ? unchecked((uint)(long)d) : B(v) ? 1u : 0u;
    private static string S(object v) => v is bool b ? (b ? "true" : "false") : v is double d ? d.ToString("R", CultureInfo.InvariantCulture) : v as string ?? "";

    // a root holding only settings/default, so the key table compares the shipped defaults whatever the local
    // settings/*.lua overrides (the server name, the cutscene ...) are
    private static string DefaultsOnly(string root)
    {
        string dir = Path.Combine(Path.GetTempPath(), "xi_loginsettings_defaults_" + Environment.ProcessId);
        string def = Path.Combine(dir, "settings", "default");
        Directory.CreateDirectory(def);
        foreach (var f in Directory.GetFiles(Path.Combine(root, "settings", "default"), "*.lua"))
            File.Copy(f, Path.Combine(def, Path.GetFileName(f)), true);
        return dir;
    }

    private static void Offline(string root)
    {
        string defRoot = DefaultsOnly(root);
        var s = Settings.Load(defRoot, _ => null);
        var c = LoginConfig.From(s);
        string[] expansions =
        {
            "RISE_OF_ZILART", "CHAINS_OF_PROMATHIA", "TREASURES_OF_AHT_URGHAN", "WINGS_OF_THE_GODDESS", "A_CRYSTALLINE_PROPHECY",
            "A_MOOGLE_KUPOD_ETAT", "A_SHANTOTTO_ASCENSION", "VISIONS_OF_ABYSSEA", "SCARS_OF_ABYSSEA", "HEROES_OF_ABYSSEA", "SEEKERS_OF_ADOULIN",
        };
        bool[] expansionFields =
        {
            c.View.RiseOfZilart, c.View.ChainsOfPromathia, c.View.TreasuresOfAhtUrghan, c.View.WingsOfTheGoddess, c.View.ACrystallineProphecy,
            c.View.AMoogleKupodEtat, c.View.AShantottoAscension, c.View.VisionsOfAbyssea, c.View.ScarsOfAbyssea, c.View.HeroesOfAbyssea, c.View.SeekersOfAdoulin,
        };
        bool[] wardrobes = { c.View.MogWardrobe3, c.View.MogWardrobe4, c.View.MogWardrobe5, c.View.MogWardrobe6, c.View.MogWardrobe7, c.View.MogWardrobe8 };

        // (key, the C++ conversion of the file's value, LoginConfig's)
        var table = new List<(string Key, object Expected, object Actual)>
        {
            ("network.LOGIN_AUTH_PORT", (ushort)U(Scrape(root, "network.LOGIN_AUTH_PORT")), c.Connect.AuthPort),
            ("network.LOGIN_AUTH_PORT (routing id)", (ushort)U(Scrape(root, "network.LOGIN_AUTH_PORT")), c.RoutingPort),
            ("network.LOGIN_DATA_PORT", (ushort)U(Scrape(root, "network.LOGIN_DATA_PORT")), c.Connect.DataPort),
            ("network.LOGIN_VIEW_PORT", (ushort)U(Scrape(root, "network.LOGIN_VIEW_PORT")), c.Connect.ViewPort),
            ("network.LOGIN_AUTH_IP", S(Scrape(root, "network.LOGIN_AUTH_IP")), c.RoutingIp),
            ("network.ZMQ_*", $"{S(Scrape(root, "network.ZMQ_TRANSPORT"))}://{S(Scrape(root, "network.ZMQ_IP"))}:{(ushort)U(Scrape(root, "network.ZMQ_PORT"))}", c.ZmqEndpoint),
            ("network.SEARCH_PORT", (ushort)U(Scrape(root, "network.SEARCH_PORT")), c.Lobby.SearchPort),
            ("main.SERVER_NAME (auth)", S(Scrape(root, "main.SERVER_NAME")), c.Auth.ServerName),
            ("main.SERVER_NAME (data)", S(Scrape(root, "main.SERVER_NAME")), c.Lobby.ServerName),
            ("main.SERVER_NAME (view)", S(Scrape(root, "main.SERVER_NAME")), c.View.ServerName),
            ("main.NEW_CHARACTER_CUTSCENE", B(Scrape(root, "main.NEW_CHARACTER_CUTSCENE")), c.View.NewCharacterCutscene),
            ("login.ACCOUNT_CREATION", B(Scrape(root, "login.ACCOUNT_CREATION")), c.Auth.AccountCreation),
            ("login.MAINT_MODE (data uint8)", (byte)U(Scrape(root, "login.MAINT_MODE")), c.Lobby.MaintMode),
            ("login.MAINT_MODE (data bool)", B(Scrape(root, "login.MAINT_MODE")), c.Lobby.MaintModeOn),
            ("login.MAINT_MODE (view uint8)", (byte)U(Scrape(root, "login.MAINT_MODE")), c.View.MaintMode),
            ("login.CHARACTER_CREATION (data uint8)", (byte)U(Scrape(root, "login.CHARACTER_CREATION")) != 0, c.Lobby.CharacterCreation),
            ("login.CHARACTER_CREATION (view bool)", B(Scrape(root, "login.CHARACTER_CREATION")), c.View.CharacterCreation),
            ("login.LOGIN_LIMIT", (byte)U(Scrape(root, "login.LOGIN_LIMIT")), c.Lobby.LoginLimit),
            ("login.LOG_USER_IP", B(Scrape(root, "login.LOG_USER_IP")), c.Lobby.LogUserIp),
            ("login.CHARACTER_DELETION", B(Scrape(root, "login.CHARACTER_DELETION")), c.View.CharacterDeletion),
            ("login.CLIENT_VER", S(Scrape(root, "login.CLIENT_VER")), c.View.ClientVer),
            ("login.VER_LOCK", (byte)U(Scrape(root, "login.VER_LOCK")), c.View.VerLock),
            ("login.DISABLE_MOB_NPC_CHAR_NAMES", B(Scrape(root, "login.DISABLE_MOB_NPC_CHAR_NAMES")), c.View.DisableMobNpcCharNames),
            ("map.ZONE_PLAYER_CAP", (ushort)U(Scrape(root, "map.ZONE_PLAYER_CAP")), c.Lobby.ZonePlayerCap),
            ("map.ZONE_PLAYER_GM_RESERVED", (ushort)U(Scrape(root, "map.ZONE_PLAYER_GM_RESERVED")), c.Lobby.ZonePlayerGmReserved),
        };
        for (int i = 0; i < expansions.Length; i++) table.Add(("login." + expansions[i], B(Scrape(root, "login." + expansions[i])), expansionFields[i]));
        for (int i = 0; i < wardrobes.Length; i++) table.Add(($"login.MOG_WARDROBE_{i + 3}", B(Scrape(root, $"login.MOG_WARDROBE_{i + 3}")), wardrobes[i]));
        foreach (var (key, expected, actual) in table) Eq("default " + key, expected, actual);
        Check("every scraped key exists in the files", table.All(t => t.Expected != null));
        Eq("default login.BANNED_WORDS_LIST", "badword", string.Join(",", c.View.BannedWords));

        // the env overrides (settings.cpp: XI_<FILE>_<KEY>, all digits a number, else a string)
        var env = new Dictionary<string, string>
        {
            ["XI_LOGIN_MAINT_MODE"] = "1",
            ["XI_LOGIN_CHARACTER_CREATION"] = "false",   // a string, get<bool>: falsy
            ["XI_LOGIN_LOG_USER_IP"] = "true",           // a string, get<bool>: truthy
            ["XI_LOGIN_LOGIN_LIMIT"] = "300",            // get<uint8>: 300 narrowed to 44
            ["XI_LOGIN_VER_LOCK"] = "1.5",               // not all digits: a string, get<uint8> of a truthy string is 1
            ["XI_LOGIN_ACCOUNT_CREATION"] = "0",
            ["XI_LOGIN_CLIENT_VER"] = "30990101_0",
            ["XI_NETWORK_LOGIN_AUTH_PORT"] = " 57231 ",  // trimmed
            ["XI_NETWORK_LOGIN_AUTH_IP"] = "127.0.0.9",
            ["XI_NETWORK_SEARCH_PORT"] = "57002",
            ["XI_MAIN_SERVER_NAME"] = "Lsetworld",
            ["XI_MAP_ZONE_PLAYER_CAP"] = "3",
            ["XI_LOGIN_NO_SUCH_KEY"] = "1",              // only keys the files define take an override
        };
        var e = LoginConfig.From(Settings.Load(defRoot, k => env.TryGetValue(k, out var v) ? v : null));
        Eq("env MAINT_MODE=1: data uint8", 1, e.Lobby.MaintMode);
        Eq("env MAINT_MODE=1: data bool", true, e.Lobby.MaintModeOn);
        Eq("env MAINT_MODE=1: view", 1, e.View.MaintMode);
        Eq("env CHARACTER_CREATION=false: data", false, e.Lobby.CharacterCreation);
        Eq("env CHARACTER_CREATION=false: view", false, e.View.CharacterCreation);
        Eq("env LOG_USER_IP=true", true, e.Lobby.LogUserIp);
        Eq("env LOGIN_LIMIT=300: uint8 44", 44, e.Lobby.LoginLimit);
        Eq("env VER_LOCK=1.5: a truthy string, 1", 1, e.View.VerLock);
        Eq("env ACCOUNT_CREATION=0", false, e.Auth.AccountCreation);
        Eq("env CLIENT_VER", "30990101_0", e.View.ClientVer);
        Eq("env LOGIN_AUTH_PORT: auth port", 57231, e.Connect.AuthPort);
        Eq("env LOGIN_AUTH_PORT: routing port", 57231, e.RoutingPort);
        Eq("env LOGIN_AUTH_IP: routing ip", "127.0.0.9", e.RoutingIp);
        Eq("env SEARCH_PORT", 57002, e.Lobby.SearchPort);
        Eq("env SERVER_NAME: auth / data / view", "Lsetworld/Lsetworld/Lsetworld", $"{e.Auth.ServerName}/{e.Lobby.ServerName}/{e.View.ServerName}");
        Eq("env ZONE_PLAYER_CAP", 3, e.Lobby.ZonePlayerCap);
        Eq("env of an undefined key: no key", false, Settings.Load(defRoot, k => env.TryGetValue(k, out var v) ? v : null).Has("login.NO_SUCH_KEY"));

        // settings::get<T> on a key no file has: T()
        var bare = LoginConfig.From(Settings.Parse("xi.settings.login = {\n    MAINT_MODE = 2,\n}\n"));
        Eq("missing CHARACTER_CREATION: false", false, bare.View.CharacterCreation);
        Eq("missing ACCOUNT_CREATION: false", false, bare.Auth.AccountCreation);
        Eq("missing LOGIN_AUTH_PORT: 0", 0, bare.Connect.AuthPort);
        Eq("missing SERVER_NAME: empty", "", bare.Lobby.ServerName);
        Eq("missing BANNED_WORDS_LIST: none", 0, bare.View.BannedWords.Count);
        Eq("MAINT_MODE = 2", 2, bare.Lobby.MaintMode);
        var narrow = LoginConfig.From(Settings.Parse("xi.settings.login = {\n    MAINT_MODE = 256,\n    LOGIN_LIMIT = 2.9,\n}\n"));
        Eq("MAINT_MODE = 256: get<uint8> 0", 0, narrow.Lobby.MaintMode);
        Eq("MAINT_MODE = 256: get<bool> true", true, narrow.Lobby.MaintModeOn);
        Eq("LOGIN_LIMIT = 2.9: truncated 2", 2, narrow.Lobby.LoginLimit);

        // login.BANNED_WORDS_LIST: a table of strings, a later file's replacing an earlier one whole
        var lists = Settings.Parse(
            "xi.settings.login =\n{\n    BANNED_WORDS_LIST =\n    {\n        'one', -- c\n        \"two\",\n    },\n    START = { DAY = 6, MONTH = 4 },\n" +
            "    EMPTY = {},\n    INLINE = { 'a', 'b' },\n}\n");
        Eq("list over lines", "one,two", string.Join(",", lists.GetStringList("login.BANNED_WORDS_LIST")));
        Eq("a keyed table is no list", true, lists.GetStringList("login.START") == null);
        Eq("an empty table: an empty list", 0, lists.GetStringList("login.EMPTY")?.Count ?? -1);
        Eq("a one-line list", "a,b", string.Join(",", lists.GetStringList("login.INLINE")));
        lists.ParseInto("xi.settings.login.BANNED_WORDS_LIST = { 'three' }\n");
        Eq("a later file's list replaces it", "three", string.Join(",", lists.GetStringList("login.BANNED_WORDS_LIST")));
        lists.ParseInto("xi.settings.login.BANNED_WORDS_LIST =\n{\n    'four',\n    'five',\n}\n");
        Eq("a dotted list over lines", "four,five", string.Join(",", lists.GetStringList("login.BANNED_WORDS_LIST")));
        Eq("scalars still read after the lists", 0, Settings.Parse("xi.settings.login =\n{\n    BANNED_WORDS_LIST = { 'x' },\n    LOGIN_LIMIT = 0,\n}\n").Get<byte>("login.LOGIN_LIMIT"));
        Eq("a key after a list", true, Settings.Parse("xi.settings.login =\n{\n    BANNED_WORDS_LIST =\n    {\n        'x',\n    },\n    LOG_USER_IP = true,\n}\n").Get<bool>("login.LOG_USER_IP"));
    }

    // ---------------------------------------------------------------- the fixtures

    private static void Sql(IDatabase db, string q, params object[] a) => LoginDb.PreparedStmt(db, q, a);

    private static long Count(IDatabase db, string q, params object[] a)
    {
        var r = LoginDb.PreparedStmt(db, q, a);
        return r != null && r.Next() ? r.Get<long>("c") : -1;
    }

    private static List<string> CharidTables(IDatabase db)
    {
        var tables = new List<string>();
        var r = LoginDb.PreparedStmt(db, "SELECT table_name AS t FROM information_schema.columns WHERE table_schema = DATABASE() AND column_name = 'charid' AND table_name <> 'chars'");
        while (r != null && r.Next()) tables.Add(r.Get<string>("t"));
        return tables;
    }

    private static void Cleanup(IDatabase db)
    {
        var tables = CharidTables(db);
        foreach (var c in new[] { CharPlayer, CharOther, CharGm })
        {
            foreach (var t in tables) Sql(db, $"DELETE FROM `{t}` WHERE charid = ?", c);
            Sql(db, "DELETE FROM chars WHERE charid = ?", c);
        }
        foreach (var a in new[] { AccPlayer, AccOther, AccGm })
        {
            Sql(db, "DELETE FROM accounts_sessions WHERE accid = ?", a);
            Sql(db, "DELETE FROM account_ip_record WHERE accid = ?", a);
            Sql(db, "DELETE FROM accounts_profile WHERE accid = ?", a);
            Sql(db, "DELETE FROM accounts WHERE id = ?", a);
        }
        // the ACCOUNT_CREATION check's account, should creation not have been refused
        Sql(db, "DELETE FROM accounts WHERE login = ?", "lsetnewacct");
    }

    private static void Seed(IDatabase db)
    {
        string hash = PasswordHasher.Generate(Password);
        foreach (var (acc, login) in new[] { (AccPlayer, "lsetplayer"), (AccOther, "lsetother"), (AccGm, "lsetgm") })
            Sql(db, "INSERT INTO accounts(id,login,password,content_ids,status,priv) VALUES (?,?,?,?,?,?)", acc, login, hash, (byte)3, (byte)1, (byte)1);
        foreach (var (charid, acc, name, gm) in new[] { (CharPlayer, AccPlayer, "Lsetplayer", (byte)0), (CharOther, AccOther, "Lsetother", (byte)0), (CharGm, AccGm, "Lsetgm", (byte)1) })
        {
            Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,nation,gmlevel) VALUES (?,?,?,?,?,?,?)", charid, acc, name, Zone, Zone, (byte)0, gm);
            Sql(db, "INSERT INTO char_stats(charid,mjob,sjob) VALUES (?,?,?)", charid, (byte)1, (byte)0);
            Sql(db, "INSERT INTO char_look(charid) VALUES (?)", charid);
        }
    }

    // ---------------------------------------------------------------- the login host

    private sealed class HostProc : IDisposable
    {
        public readonly Process P;
        public readonly ConcurrentQueue<string> Lines = new ConcurrentQueue<string>();

        public HostProc(string dll, string root, int dbPort, IDictionary<string, string> env)
        {
            P = new Process
            {
                StartInfo = new ProcessStartInfo("dotnet", $"\"{dll}\" login root=\"{root}\" db.port={dbPort}")
                {
                    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
                    WorkingDirectory = root,
                },
            };
            // only this run's XI_* settings reach the host
            foreach (var k in P.StartInfo.Environment.Keys.Where(k => k.StartsWith("XI_", StringComparison.OrdinalIgnoreCase) &&
                     k != "XI_SETTINGS_DEFAULTS_ONLY").ToList())
                P.StartInfo.Environment.Remove(k);
            foreach (var (k, v) in env) P.StartInfo.Environment[k] = v;
            P.OutputDataReceived += (_, e) => { if (e.Data != null) Lines.Enqueue(e.Data); };
            P.ErrorDataReceived += (_, e) => { if (e.Data != null) Lines.Enqueue("[stderr] " + e.Data); };
            P.Start();
            P.BeginOutputReadLine();
            P.BeginErrorReadLine();
        }

        public string Line(string text) => Lines.FirstOrDefault(l => l.Contains(text));

        public bool WaitFor(string text, TimeSpan within)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < within)
            {
                if (Line(text) != null) return true;
                if (P.HasExited) return Line(text) != null;
                Thread.Sleep(100);
            }
            return false;
        }

        public void Dispose()
        {
            try { if (!P.HasExited) P.Kill(true); } catch (InvalidOperationException) { }
            P.WaitForExit(5000);
            P.Dispose();
        }
    }

    private static bool PortFree(int port)
    {
        try
        {
            var l = new TcpListener(IPAddress.Loopback, port);
            l.Start();
            l.Stop();
            return true;
        }
        catch (SocketException) { return false; }
    }

    // the env every run has: the ports, the router, the database
    private static Dictionary<string, string> Env(int dbPort, params (string Key, string Value)[] extra)
    {
        var env = new Dictionary<string, string>
        {
            ["XI_NETWORK_LOGIN_AUTH_PORT"] = Auth.ToString(), ["XI_NETWORK_LOGIN_DATA_PORT"] = Data.ToString(),
            ["XI_NETWORK_LOGIN_VIEW_PORT"] = View.ToString(), ["XI_NETWORK_ZMQ_PORT"] = Router.ToString(),
            ["XI_NETWORK_SEARCH_PORT"] = Search.ToString(), ["XI_NETWORK_SQL_PORT"] = dbPort.ToString(),
        };
        foreach (var (k, v) in extra) env[k] = v;
        return env;
    }

    private static readonly List<(Ipp From, IIpcMessage Message)> Ipc = new List<(Ipp, IIpcMessage)>();

    private static void DrainIpc(ZmqRouterChannel router, TimeSpan within, Func<bool> done)
    {
        var sw = Stopwatch.StartNew();
        do
        {
            while (router.TryReceive(out var m))
            {
                try { Ipc.Add((m.Ipp, IpcFraming.Decode(m.Payload))); } catch (Exception) { }
            }
            if (done()) return;
            Thread.Sleep(50);
        } while (sw.Elapsed < within);
    }

    private static T Await<T>(Task<T> t) => t.GetAwaiter().GetResult();
    private static void Await(Task t) => t.GetAwaiter().GetResult();

    private static AuthClient.Reply Login(Endpoints ep, string user) =>
        Await(AuthClient.Send(ep, AuthClient.CmdLogin, user, Password, CancellationToken.None));

    // auth, then the lobby's data socket bound (the first 0x03) and the view socket; null when the lobby never answered
    private static (LobbyClient Lobby, string Error) Lobby(Endpoints ep, string user, uint acc, string version = null, double timeoutSeconds = 4)
    {
        var r = Login(ep, user);
        if (r.Result != AuthClient.ResultSuccess || r.AccountId != acc) return (null, $"auth result {r.Result} {r.Error}");
        var lobby = new LobbyClient(ep, acc, r.SessionHash) { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
        if (version != null) lobby.ClientVersion = version;
        try
        {
            Await(lobby.Connect(CancellationToken.None));
            return (lobby, null);
        }
        catch (Exception e) when (e is TimeoutException || e is IOException || e is InvalidDataException)
        {
            lobby.Dispose();
            return (null, e.Message);
        }
    }

    private static string Try(Action a)
    {
        try
        {
            a();
            return null;
        }
        catch (Exception e) when (e is TimeoutException || e is IOException || e is InvalidDataException) { return e.Message; }
    }

    private static void Live(IDatabase db, string root, int dbPort)
    {
        string dll = FindHost();
        Check("XI.Host.dll beside the suite", dll != null);
        if (dll == null) return;
        foreach (var p in new[] { Auth, Data, View, Router, Search })
        {
            if (Forbidden.Contains(p)) throw new InvalidOperationException($"port {p} is a live server's");
        }
        bool free = new[] { Auth, Data, View, Router }.All(PortFree);
        Check("the run's ports are free (57231 / 57230 / 57001 / 57003)", free);
        if (!free) return;

        using var router = new ZmqRouterChannel($"tcp://127.0.0.1:{Router}");
        Check("the run's world router bound", router.Opened);
        if (!router.Opened) return;

        var ep = new Endpoints { Host = "127.0.0.1", Auth = Auth, Data = Data, View = View };
        var own = new Endpoints { Host = "127.0.0.1", Auth = Auth, Data = Data, View = View, LocalHost = OwnClient };
        uint ownIp = IpText.Str2Ip(OwnClient);

        HostProc Start(string what, Dictionary<string, string> env)
        {
            var h = new HostProc(dll, root, dbPort, env);
            bool up = h.WaitFor("login: auth ", TimeSpan.FromSeconds(60));
            Check($"[{what}] login host up", up, string.Join(" | ", h.Lines.TakeLast(5)));
            return up ? h : null;
        }

        // ---- 1: the settings files as they are
        using (var h = Start("defaults", Env(dbPort, ("XI_MAIN_SERVER_NAME", "Lsetworld"))))
        {
            if (h != null)
            {
                string line = h.Line("login: MAINT_MODE") ?? "";
                Check("[defaults] the files' values: MAINT_MODE 0, CHARACTER_CREATION 1, LOGIN_LIMIT 0, CLIENT_VER 30261006_0, VER_LOCK 2",
                      line.Contains("MAINT_MODE 0,") && line.Contains("CHARACTER_CREATION 1,") && line.Contains("LOGIN_LIMIT 0,") &&
                      line.Contains("CLIENT_VER 30261006_0,") && line.Contains("VER_LOCK 2,") && line.Contains("SEARCH_PORT 57002"), line);
                Check("[defaults] instanced zones read from the zone files", Regex.IsMatch(line, @", [1-9]\d* instanced zones"), line);

                var r = Login(ep, "lsetplayer");
                Check("[defaults] auth login", r.Result == AuthClient.ResultSuccess && r.AccountId == AccPlayer, $"result {r.Result} {r.Error}");
                DrainIpc(router, TimeSpan.FromSeconds(5), () => Ipc.Any(x => x.Message is AccountLogin a && a.AccountId == AccPlayer));
                var hop = Ipc.FirstOrDefault(x => x.Message is AccountLogin a && a.AccountId == AccPlayer);
                Check("[defaults] AccountLogin reaches the world router (network.ZMQ_PORT)", hop.Message != null);
                Eq("[defaults] routing id LOGIN_AUTH_IP:LOGIN_AUTH_PORT (0.0.0.0:57231)", $"0.0.0.0:{Auth}", hop.Message != null ? hop.From.ToString() : "-");

                var (lobby, err) = Lobby(ep, "lsetplayer", AccPlayer);
                Check("[defaults] lobby: the data socket's list", lobby != null, err ?? "");
                if (lobby != null)
                {
                    using (lobby)
                    {
                        Check("[defaults] 0x26 version 30261006_0 accepted", Try(() => Await(lobby.Version(CancellationToken.None))) == null);
                        Eq("[defaults] 0x24 world name: main.SERVER_NAME from XI_MAIN_SERVER_NAME", "Lsetworld", Await(lobby.WorldList(CancellationToken.None)));
                        var chars = Await(lobby.CharList(CancellationToken.None));
                        Check("[defaults] the char list", chars.Any(c => c.Id == CharPlayer && c.Name == "Lsetplayer"), string.Join(",", chars.Select(c => c.Name)));
                        Eq("[defaults] CHARACTER_CREATION: free slots up to content_ids (3)", 3, lobby.ListSlots);
                        Eq("[defaults] 0x22 a free name", 0, Await(lobby.CheckName("Lsetfresh", CancellationToken.None)));
                        Eq("[defaults] 0x22 BANNED_WORDS_LIST 'badword': 313", 313, Await(lobby.CheckName("Xbadwordx", CancellationToken.None)));
                        Eq("[defaults] 0x22 a mob's name with DISABLE_MOB_NPC_CHAR_NAMES off", 0, Await(lobby.CheckName("Goblindigger", CancellationToken.None)));
                    }
                }

                var (old, err2) = Lobby(ep, "lsetplayer", AccPlayer, version: "30251001_0");
                Check("[defaults] lobby for an older client", old != null, err2 ?? "");
                if (old != null)
                {
                    using (old)
                    {
                        string refused = Try(() => Await(old.Version(CancellationToken.None)));
                        Check("[defaults] VER_LOCK 2: an older client refused, 331 GAMES_DATA_HAS_BEEN_UPDATED", refused != null && refused.Contains("error 331"), refused ?? "accepted");
                    }
                }
                Check("[defaults] the version mismatch logged", h.WaitFor("has incorrect client version: got 302510xx_x, expected 302610xx_x", TimeSpan.FromSeconds(3)));
            }
        }

        // ---- 2: MAINT_MODE 1, ACCOUNT_CREATION off
        using (var h = Start("maint", Env(dbPort, ("XI_LOGIN_MAINT_MODE", "1"), ("XI_LOGIN_ACCOUNT_CREATION", "false"))))
        {
            if (h != null)
            {
                var (lobby, err) = Lobby(ep, "lsetplayer", AccPlayer);
                Check("[maint] MAINT_MODE: a non-GM's lobby gets no char list (data_session 0xA1)", lobby == null, "the list came");
                lobby?.Dispose();

                var (gm, gmErr) = Lobby(ep, "lsetgm", AccGm);
                Check("[maint] MAINT_MODE: a GM's lobby", gm != null, gmErr ?? "");
                if (gm != null)
                {
                    using (gm)
                    {
                        Await(gm.Version(CancellationToken.None));
                        var chars = Await(gm.CharList(CancellationToken.None));
                        Check("[maint] the GM's char listed", chars.Any(c => c.Id == CharGm), string.Join(",", chars.Select(c => c.Name)));
                        Eq("[maint] MAINT_MODE: the name server refuses even a GM, 314", 314, Await(gm.CheckName("Lsetfresh", CancellationToken.None)));
                        LobbyClient.Selected sel = null;
                        string selErr = Try(() => sel = Await(gm.Select(chars.First(c => c.Id == CharGm), CancellationToken.None)));
                        Check("[maint] MAINT_MODE: a GM still selects (0x0B)", sel != null && sel.MapPort != 0, selErr ?? "");
                        DrainIpc(router, TimeSpan.FromSeconds(5), () => Ipc.Any(x => x.Message is CharZone z && z.CharId == CharGm));
                        Check("[maint] the GM's CharZone to the world", Ipc.Any(x => x.Message is CharZone z && z.CharId == CharGm && (ushort)z.DestinationZoneId == Zone));
                    }
                }
                Sql(db, "DELETE FROM accounts_sessions WHERE accid = ?", AccGm);

                var created = Await(AuthClient.Send(ep, AuthClient.CmdCreate, "lsetnewacct", Password, CancellationToken.None));
                Eq("[maint] ACCOUNT_CREATION false: LOGIN_ERROR_CREATE_DISABLED (8)", 8, created.Result);
                Eq("[maint] no account made", 0, Count(db, "SELECT COUNT(*) AS c FROM accounts WHERE login = ?", "lsetnewacct"));
            }
        }

        // ---- 3: CHARACTER_CREATION off (a string, as settings.cpp reads XI_LOGIN_CHARACTER_CREATION=false), VER_LOCK 0
        using (var h = Start("creation off", Env(dbPort, ("XI_LOGIN_CHARACTER_CREATION", "false"), ("XI_LOGIN_VER_LOCK", "0"))))
        {
            if (h != null)
            {
                var (lobby, err) = Lobby(ep, "lsetplayer", AccPlayer, version: "30251001_0");
                Check("[creation off] lobby", lobby != null, err ?? "");
                if (lobby != null)
                {
                    using (lobby)
                    {
                        Check("[creation off] VER_LOCK 0: an older client accepted", Try(() => Await(lobby.Version(CancellationToken.None))) == null);
                        Await(lobby.CharList(CancellationToken.None));
                        Eq("[creation off] CHARACTER_CREATION: no free slot, the one char only", 1, lobby.ListSlots);
                        Eq("[creation off] CHARACTER_CREATION: the name server refuses, 314", 314, Await(lobby.CheckName("Lsetfresh", CancellationToken.None)));
                    }
                }
                Eq("[creation off] no char made", 0, Count(db, "SELECT COUNT(*) AS c FROM chars WHERE charname = ?", "Lsetfresh"));
            }
        }

        // ---- 4: LOGIN_LIMIT 1 with a session from the same client address (127.0.0.77) already
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,client_addr) VALUES (?,?,?)", AccOther, CharOther, ownIp);
        using (var h = Start("login limit 1", Env(dbPort, ("XI_LOGIN_LOGIN_LIMIT", "1"))))
        {
            if (h != null)
            {
                var (lobby, err) = Lobby(own, "lsetplayer", AccPlayer);
                Check("[limit 1] lobby from 127.0.0.77", lobby != null, err ?? "");
                if (lobby != null)
                {
                    using (lobby)
                    {
                        Await(lobby.Version(CancellationToken.None));
                        var chars = Await(lobby.CharList(CancellationToken.None));
                        string refused = Try(() => Await(lobby.Select(chars.First(c => c.Id == CharPlayer), CancellationToken.None)));
                        Check("[limit 1] LOGIN_LIMIT: a second session from the address refused, 332", refused != null && refused.Contains("error 332"), refused ?? "selected");
                    }
                }
                Eq("[limit 1] no session row for the refused char", 0, Count(db, "SELECT COUNT(*) AS c FROM accounts_sessions WHERE accid = ?", AccPlayer));

                var (gm, gmErr) = Lobby(own, "lsetgm", AccGm);
                Check("[limit 1] a GM's lobby from 127.0.0.77", gm != null, gmErr ?? "");
                if (gm != null)
                {
                    using (gm)
                    {
                        Await(gm.Version(CancellationToken.None));
                        var chars = Await(gm.CharList(CancellationToken.None));
                        LobbyClient.Selected sel = null;
                        string selErr = Try(() => sel = Await(gm.Select(chars.First(c => c.Id == CharGm), CancellationToken.None)));
                        Check("[limit 1] LOGIN_LIMIT: a GM passes", sel != null, selErr ?? "");
                    }
                }
                Sql(db, "DELETE FROM accounts_sessions WHERE accid = ?", AccGm);
            }
        }

        // ---- 5: LOGIN_LIMIT 2 (the one session leaves room), LOG_USER_IP, DISABLE_MOB_NPC_CHAR_NAMES
        using (var h = Start("login limit 2", Env(dbPort, ("XI_LOGIN_LOGIN_LIMIT", "2"), ("XI_LOGIN_LOG_USER_IP", "true"), ("XI_LOGIN_DISABLE_MOB_NPC_CHAR_NAMES", "1"))))
        {
            if (h != null)
            {
                Check("[limit 2] DISABLE_MOB_NPC_CHAR_NAMES: the npc / mob names read", h.WaitFor("npc / mob names refused as char names", TimeSpan.FromSeconds(3)));
                var (lobby, err) = Lobby(own, "lsetplayer", AccPlayer);
                Check("[limit 2] lobby from 127.0.0.77", lobby != null, err ?? "");
                if (lobby != null)
                {
                    using (lobby)
                    {
                        Await(lobby.Version(CancellationToken.None));
                        var chars = Await(lobby.CharList(CancellationToken.None));
                        Eq("[limit 2] DISABLE_MOB_NPC_CHAR_NAMES: a mob's name (Goblin_Digger) refused, 313", 313, Await(lobby.CheckName("Goblindigger", CancellationToken.None)));
                        LobbyClient.Selected sel = null;
                        string selErr = Try(() => sel = Await(lobby.Select(chars.First(c => c.Id == CharPlayer), CancellationToken.None)));
                        Check("[limit 2] LOGIN_LIMIT: under the limit, selected (0x0B)", sel != null, selErr ?? "");
                        if (sel != null) Eq("[limit 2] the 0x0B's search port: network.SEARCH_PORT", Search, sel.SearchPort);
                    }
                }
                Eq("[limit 2] the session's client address", (long)ownIp, Count(db, "SELECT client_addr AS c FROM accounts_sessions WHERE accid = ?", AccPlayer));
                // written after the 0x0B went out (data_session.cpp: the 0xA2's tail)
                long logged = 0;
                for (var sw = Stopwatch.StartNew(); sw.Elapsed < TimeSpan.FromSeconds(5) && logged == 0; Thread.Sleep(100))
                    logged = Count(db, "SELECT COUNT(*) AS c FROM account_ip_record WHERE accid = ? AND charid = ? AND client_ip = ?", AccPlayer, CharPlayer, OwnClient);
                Eq("[limit 2] LOG_USER_IP=true: account_ip_record", 1, logged);
                DrainIpc(router, TimeSpan.FromSeconds(5), () => Ipc.Any(x => x.Message is CharZone z && z.CharId == CharPlayer));
                Check("[limit 2] the char's CharZone to the world", Ipc.Any(x => x.Message is CharZone z && z.CharId == CharPlayer && (ushort)z.DestinationZoneId == Zone));
            }
        }
        Eq("LOG_USER_IP false (the GM's selects in runs 2 and 4): no account_ip_record, the one of run 5 only", 1, Count(db, "SELECT COUNT(*) AS c FROM account_ip_record WHERE accid IN (?, ?)", AccPlayer, AccGm));
    }
}
