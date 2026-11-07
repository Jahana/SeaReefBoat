// XI.Host -- runnable composition of the ported servers.
//
//   XI.Host login [options]   auth/data/view listeners + ZMQ dealer to the world router (== xi_connect)
//   XI.Host world [options]   ZMQ router + IPC hub + conquest/time server + optional HTTP API (== xi_world)
//   XI.Host map [options]     the zones assigned to ip:port (every zone without them), loaded on first use (== xi_map)
//   XI.Host profile [options] PlayOnline profile (friends, status, files) + IRC presence listeners (== xi_profile)
//   XI.Host search [options]  /sea, party / linkshell lists, auction house lists and history, AH expiry (== xi_search)
//
// Options (key=value, all optional):
//   db.host=127.0.0.1 db.port=3306 db.user=root db.password=root db.name=xidb
//   db.retry=1                           network.SQL_QUERY_RETRY_COUNT: a query that lost its connection is run again
//                                         after a reconnect this many times, then the process ends (exit code 6)
//   ipc=tcp://127.0.0.1:54003            world router endpoint (world binds, login connects)
//   self=0.0.0.0:54231                   login's own IPP (dealer routing id; default LOGIN_AUTH_IP:LOGIN_AUTH_PORT)
//   bind=0.0.0.0 auth=54231 data=54230 view=54001
//   http=0 http.host=127.0.0.1 http.port=8088
//   dailytally=0 dailytally.amount=10 dailytally.limit=10000
//   root=<dir>                           directory holding settings/, scripts/, data/, res/, navmeshes/, ximeshes/ and
//                                         libluajit_64.dll; default: the first of the working directory and the folders
//                                         above the exe that has settings/default/main.lua (the csharp folder)
//   server.name=Nameless badnames=res/badnames.bin
//   map.port=54230 map.timeout=60 roe=1   (map) UDP port, MAX_TIME_LASTUPDATE, ENABLE_ROE
//   ip=127.0.0.1 port=54230               (map) --ip / --port: the zone_settings zoneip / zoneport this process serves;
//                                         port is also the UDP port (map.port without it)
//   lazy=0                                (map) --lazy: a zone loads when a char logs into it or zones to it, not at
//                                         startup; no ships (development only, as in the C++)
//   watch=0                               (map) the file watcher (luautils.cpp:578, opt-in here): a .lua file added or
//                                         changed under scripts/, modules/ or settings/ is reloaded on the next
//                                         2400 ms time server tick, as the C++ TryReloadFilewatchList does; an
//                                         editor that saves through a temp file and a rename makes only a move,
//                                         which neither server reloads
//   log=log/<name>-server.log             --log: the log file (logging::InitializeLog's file sink); <name> is connect,
//                                         world, map, profile or search, the C++ executables' server names, and the
//                                         path is under root. append-date=1 (--append-date): a file per day instead
//                                         (<file>_YYYY-MM-DD.log). The lines follow logging.PATTERN / JSON_ENABLED and
//                                         the LOG_* / DEBUG_* switches (XI.Common.ServerLog)
//   stats=0                               (map) every n seconds a "map: stats" line (MapStatistics::print): the tick
//                                         times, sessions, chars, memory; 0 = off
//   search=54002                          (login) the search port the char select (0x0B) names
//                                         (login) every other setting src/login reads (login.*, main.SERVER_NAME /
//                                         NEW_CHARACTER_CUTSCENE, map.ZONE_PLAYER_CAP / GM_RESERVED, the network ports):
//                                         XI.Login.LoginConfig, from the settings and XI_* env vars only
//   profile=51220 irc=51240               (profile) PROFILE_PORT, and the IRC port (a polcore constant in the C++; only
//                                         movable here for development); bind as above; profile.cert / profile.key
//                                         in root (made on first start, as the C++)
//   search=54002 expire=1 expire.days=3 expire.interval=3600 omitnohistory=0
//                                         (search) SEARCH_PORT and search.EXPIRE_AUCTIONS / EXPIRE_DAYS / EXPIRE_INTERVAL /
//                                         OMIT_NO_HISTORY; whitelist=a,b overrides search.ACCESS_WHITELIST; bind as above
//
// Defaults come from <root>/settings/default/*.lua + <root>/settings/*.lua + XI_* env vars (the C++ layout);
// key=value flags override them.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using XI.Login;
using XI.Persistence;
using XI.World;

internal static class Program
{
    // the exit code of a process the database layer ended (MySqlDatabase.Terminate)
    private const int DatabaseTerminatedExit = 6;

    // the process's log sink (logging::InitializeLog) and its database (db::enableTimers, map only)
    private static XI.Common.ServerLog serverLog_;
    private static MySqlDatabase mysql_;

    private static bool HasColumn(IDatabase db, string table, string column)
    {
        var rs = db.Execute($"SHOW COLUMNS FROM `{table}` LIKE '{column}'", Array.Empty<BoundValue>());
        return rs != null && rs.Next();
    }

    private static string FindRoot()
    {
        static bool IsRoot(string dir) => File.Exists(Path.Combine(dir, "settings", "default", "main.lua"));
        if (IsRoot(Directory.GetCurrentDirectory())) return Directory.GetCurrentDirectory();
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (IsRoot(d.FullName)) return d.FullName;
        return ".";
    }

    private static int Main(string[] args)
    {
        if (args.Length == 0 || (args[0] != "login" && args[0] != "world" && args[0] != "map" && args[0] != "profile" && args[0] != "search"))
        {
            Console.WriteLine("usage: XI.Host <login|world|map|profile|search> [key=value ...]");
            return 2;
        }

        var o = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < args.Length; i++)
        {
            int eq = args[i].IndexOf('=');
            if (eq > 0) o[args[i].Substring(0, eq)] = args[i].Substring(eq + 1);
        }
        string root = o.TryGetValue("root", out var r) ? r : FindRoot();
        var settings = XI.Common.Settings.Load(root);
        var fromSettings = new Dictionary<string, string>
        {
            ["db.host"] = "network.SQL_HOST", ["db.port"] = "network.SQL_PORT", ["db.user"] = "network.SQL_LOGIN",
            ["db.password"] = "network.SQL_PASSWORD", ["db.name"] = "network.SQL_DATABASE",
            ["db.retry"] = "network.SQL_QUERY_RETRY_COUNT",
            ["auth"] = "network.LOGIN_AUTH_PORT", ["data"] = "network.LOGIN_DATA_PORT", ["view"] = "network.LOGIN_VIEW_PORT",
            ["http"] = "network.ENABLE_HTTP", ["http.host"] = "network.HTTP_HOST", ["http.port"] = "network.HTTP_PORT",
            ["dailytally"] = "main.ENABLE_DAILY_TALLY", ["dailytally.amount"] = "main.DAILY_TALLY_AMOUNT", ["dailytally.limit"] = "main.DAILY_TALLY_LIMIT", ["server.name"] = "main.SERVER_NAME",
            ["map.port"] = "network.MAP_PORT", ["map.timeout"] = "map.MAX_TIME_LASTUPDATE", ["roe"] = "main.ENABLE_ROE",
            ["profile"] = "network.PROFILE_PORT", ["auth.ip"] = "network.LOGIN_AUTH_IP",
            ["search"] = "network.SEARCH_PORT", ["expire"] = "search.EXPIRE_AUCTIONS", ["expire.days"] = "search.EXPIRE_DAYS",
            ["expire.interval"] = "search.EXPIRE_INTERVAL", ["omitnohistory"] = "search.OMIT_NO_HISTORY",
        };
        string Get(string k, string d)
        {
            if (o.TryGetValue(k, out var v)) return v;
            if (fromSettings.TryGetValue(k, out var sk) && settings.All.TryGetValue(sk, out var sv))
                return sv is bool b ? (b ? "1" : "0") : sv is double dd ? ((long)dd).ToString() : sv.ToString();
            return d;
        }
        if (!o.ContainsKey("ipc") && settings.Has("network.ZMQ_IP"))
            o["ipc"] = $"{settings.GetString("network.ZMQ_TRANSPORT", "tcp")}://{settings.GetString("network.ZMQ_IP")}:{settings.GetInt("network.ZMQ_PORT", 54003)}";

        var dbs = new NetworkSettings
        {
            SqlHost = Get("db.host", "127.0.0.1"), SqlPort = int.Parse(Get("db.port", "3306")),
            SqlLogin = Get("db.user", "root"), SqlPassword = Get("db.password", "root"), SqlDatabase = Get("db.name", "xidb"),
            SqlQueryRetryCount = int.Parse(Get("db.retry", "1")),
        };
        // Application::prepareLogging: log/<server name>-server.log (--log, --append-date), the lines as the
        // logging.* settings say
        string serverName = args[0] == "login" ? "connect" : args[0];
        string logFile = o.TryGetValue("log", out var lf) ? lf : Path.Combine("log", $"{serverName}-server.log");
        if (!Path.IsPathRooted(logFile)) logFile = Path.Combine(root, logFile);
        var logSettings = XI.Common.LogSettings.From(settings);
        using var serverLog = new XI.Common.ServerLog(serverName, logSettings, Console.Out, logFile,
            o.TryGetValue("append-date", out var ad) && ad != "0");
        serverLog_ = serverLog;
        // debug::init / setCoreDumpsEnabled (main.GENERATE_CORE_DUMP): the crash report and its minidump
        XI.Common.CrashDump.Log = s => serverLog.Write(XI.Common.ServerLog.Critical, s);
        XI.Common.CrashDump.Install(root);
        XI.Common.CrashDump.Enabled = settings.GetBool("main.GENERATE_CORE_DUMP");

        using var db = new MySqlDatabase(dbs);
        mysql_ = db;
        try
        {
            var probe = db.Execute("SELECT 1", Array.Empty<BoundValue>());
            if (probe == null || !probe.Next()) throw new Exception("no result");
        }
        catch (Exception e)
        {
            serverLog.Write(XI.Common.ServerLog.Critical, $"database not reachable: {e.Message}");
            return 3;
        }
        // CachingDatabase::runWithRetry / LibMariaDBConnection::connect: once running, a connection that stays lost
        // through the retries, or a reconnect that fails, ends the process as the C++ std::terminate does (an exit
        // code, not an abort: no crash dialog or dump for a watchdog to wait on)
        db.Log = s => serverLog.Write(XI.Common.ServerLog.Error, s);
        db.LogError = s => serverLog.Write(XI.Common.ServerLog.Error, s);
        db.LogWarning = s => serverLog.Write(XI.Common.ServerLog.Warn, s);
        db.LogInfo = s => serverLog.Write(XI.Common.ServerLog.Info, s);
        db.LogCritical = s => serverLog.Write(XI.Common.ServerLog.Critical, s);
        db.SlowQueryLog = logSettings.SqlSlowQueryLog;
        db.SlowQueryWarningMs = logSettings.SqlSlowQueryWarningMs;
        db.SlowQueryErrorMs = logSettings.SqlSlowQueryErrorMs;
        if (serverLog.DebugOn("SQL")) db.DebugSql = s => serverLog.Debug("SQL", s);
        db.Terminate = message =>
        {
            serverLog.Write(XI.Common.ServerLog.Critical, $"terminating: {message}");
            Environment.Exit(DatabaseTerminatedExit);
        };

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
        // the host's lines: info, or the warning / error logger of the C++ Show* call a line ports (ServerLog.LevelOf)
        Action<string> log = serverLog.Line;
        string endpoint = Get("ipc", "tcp://127.0.0.1:54003");

        return args[0] switch
        {
            "world" => RunWorld(db, settings, root, Get, endpoint, log, cts.Token),
            "map" => RunMap(db, settings, root, Get, endpoint, log, cts.Token),
            "profile" => RunProfile(db, root, Get, log, cts.Token),
            "search" => RunSearch(db, root, Get, log, cts.Token),
            _ => RunLogin(db, settings, root, k => o.TryGetValue(k, out var f) ? f : null, endpoint, log, cts.Token),
        };
    }

    private static int RunWorld(IDatabase db, XI.Common.Settings settings, string root, Func<string, string, string> get, string endpoint, Action<string> log, CancellationToken ct)
    {
        using var router = new ZmqRouterChannel(endpoint, log);
        if (!router.Opened) return 4;

        // ZoneSettings: each zone's misc flags from data/zones/<name>/zone.yaml (the yell and assist endpoints)
        var zoneSettings = new ZoneSettings(db, XI.Zone.ZoneInfo.MiscFlagsLookup(db, System.IO.Path.Combine(root, "data", "zones")));
        var engine = new WorldEngine(db, router, zoneSettings, new DailyTallySettings
        {
            Enable = get("dailytally", "0") != "0",
            Amount = int.Parse(get("dailytally.amount", "10")), Limit = int.Parse(get("dailytally.limit", "10000")),
        }, null, log);
        log($"world: IPC router bound at {endpoint}");

        HttpApiHost http = null;
        if (get("http", "0") == "1")
        {
            var api = new HttpApi(db, () => settings.All, null, log);
            http = new HttpApiHost(api, get("http.host", "127.0.0.1"), ushort.Parse(get("http.port", "8088")), log);
        }

        try { engine.Run(ct); }
        finally { http?.Dispose(); }
        return 0;
    }

    private static int RunLogin(IDatabase db, XI.Common.Settings settings, string root, Func<string, string> flag, string endpoint, Action<string> log, CancellationToken ct)
    {
        // every setting src/login reads, as it reads it (LoginConfig: the key table); a key=value flag still wins
        var lc = LoginConfig.From(settings);
        var cfg = lc.Connect;
        cfg.Bind = IPAddress.Parse(flag("bind") ?? "0.0.0.0");
        cfg.Log = log;
        if (flag("auth") is string auth) cfg.AuthPort = lc.RoutingPort = ushort.Parse(auth);
        if (flag("data") is string data) cfg.DataPort = ushort.Parse(data);
        if (flag("view") is string view) cfg.ViewPort = ushort.Parse(view);
        if (flag("search") is string search) lc.Lobby.SearchPort = ushort.Parse(search);
        if (flag("server.name") is string name) lc.Auth.ServerName = lc.Lobby.ServerName = lc.View.ServerName = name;
        lc.Auth.Log = log;
        lc.View.Log = log;
        // login_helpers.cpp isVulgarName: res/badnames.bin under the server root (the C++ working directory)
        lc.View.BadNamesPath = flag("badnames") ?? System.IO.Path.Combine(root, "res", "badnames.bin");
        if (lc.Lobby.ZoneIpFromClient && !HasColumn(db, "accounts_sessions", "client_server_addr"))
        {
            log("login: network.ZONE_IP_FROM_CLIENT needs accounts_sessions.client_server_addr (python tools/dbtool.py migrates); left off");
            lc.Lobby.ZoneIpFromClient = false;
        }

        // ConnectApplication::createEngine: loginHelpers::loadZoneLookups, the instanced zones always and the npc / mob
        // names only under DISABLE_MOB_NPC_CHAR_NAMES
        string zoneDir = System.IO.Path.Combine(root, "data", "zones");
        var instanced = XI.Zone.LoginZoneLookups.InstancedZones(db, zoneDir);
        lc.Lobby.IsInstancedZone = instanced.Contains;
        if (lc.View.DisableMobNpcCharNames)
        {
            var names = XI.Zone.LoginZoneLookups.EntityNames(db, zoneDir);
            lc.View.IsEntityName = names.Contains;
            log($"login: {names.Count} npc / mob names refused as char names");
        }

        // connect_engine.cpp getZMQRoutingId: LOGIN_AUTH_IP:LOGIN_AUTH_PORT (one login server), never a map process's IPP
        var self = flag("self") is string selfFlag ? selfFlag.Split(':') : new[] { lc.RoutingIp, lc.RoutingPort.ToString() };
        var ipp = new Ipp(IpText.Str2Ip(self[0]), ushort.Parse(self[1]));
        using var dealer = new ZmqDealerChannel(endpoint, ipp);

        // connect_application.cpp: certificateHelpers::generateSelfSignedCert() makes login.key / login.cert in the server
        // root when neither exists; handler.h then loads them
        using var cert = LoadOrCreateLoginCertificate(root, lc.RoutingIp, log);

        using var engine = new ConnectEngine(cfg, db, new LoginSessions(), cert,
            new IpcAccountLoginSink(dealer.Send), new IpcCharZoneSink(dealer.Send), lc.Auth, lc.Lobby, lc.View);

        var run = engine.Start();
        log($"login: auth {cfg.AuthPort}, data {cfg.DataPort}, view {cfg.ViewPort}; IPC dealer -> {endpoint} as {ipp}");
        log($"login: MAINT_MODE {lc.Lobby.MaintMode}, CHARACTER_CREATION {(lc.View.CharacterCreation ? 1 : 0)}, " +
            $"ACCOUNT_CREATION {(lc.Auth.AccountCreation ? 1 : 0)}, CHARACTER_DELETION {(lc.View.CharacterDeletion ? 1 : 0)}, " +
            $"LOGIN_LIMIT {lc.Lobby.LoginLimit}, LOG_USER_IP {(lc.Lobby.LogUserIp ? 1 : 0)}, CLIENT_VER {lc.View.ClientVer}, VER_LOCK {lc.View.VerLock}, " +
            $"SEARCH_PORT {lc.Lobby.SearchPort}, ZONE_IP_FROM_CLIENT {(lc.Lobby.ZoneIpFromClient ? 1 : 0)}, {instanced.Count} instanced zones");
        try { run.Wait(ct); } catch (OperationCanceledException) { }
        return 0;
    }

    // cert_helpers.h generateSelfSignedCert("login", false): when neither file exists, an RSA 4096 key and a certificate
    // (O "LSB self-signed certificate for login server", CN the LOGIN_AUTH_IP, SHA-384, 20 years) written to the root;
    // then the files as they are
    private static X509Certificate2 LoadOrCreateLoginCertificate(string root, string authIp, Action<string> log)
    {
        string certPath = System.IO.Path.Combine(root, "login.cert"), keyPath = System.IO.Path.Combine(root, "login.key");
        if (!System.IO.File.Exists(keyPath) && !System.IO.File.Exists(certPath))
        {
            log("Generating self-signed certificate");
            using var rsa = RSA.Create(4096);
            var cn = authIp ?? string.Empty;
            if (cn.Length > 16) cn = cn.Substring(0, 16);
            var subject = new X500DistinguishedNameBuilder();
            // the builder encodes in reverse: O then CN on the wire, as X509_NAME_add_entry_by_txt does
            subject.AddCommonName(cn.Length > 0 ? cn : "0.0.0.0");
            subject.AddOrganizationName("LSB self-signed certificate for login server");
            var req = new CertificateRequest(subject.Build(), rsa, HashAlgorithmName.SHA384, RSASignaturePadding.Pkcs1);
            var now = DateTimeOffset.UtcNow;
            using var created = req.CreateSelfSigned(now, now.AddSeconds(31536000L * 20));
            System.IO.File.WriteAllText(keyPath, rsa.ExportPkcs8PrivateKeyPem());
            System.IO.File.WriteAllText(certPath, created.ExportCertificatePem());
        }
        using var pem = X509Certificate2.CreateFromPemFile(certPath, keyPath);
        log($"login: TLS certificate {certPath} ({pem.Subject})");
        if (pem.NotAfter < DateTime.Now || pem.NotBefore > DateTime.Now)
            log($"Existing {certPath} is not valid for the current time. Please regenerate or obtain a new certificate.");
        // round-tripped through PFX so SslStream on Windows can use the private key
        return new X509Certificate2(pem.Export(X509ContentType.Pfx));
    }

    private static int RunProfile(IDatabase db, string root, Func<string, string, string> get, Action<string> log, CancellationToken ct)
    {
        // ProfileApplication::createEngine: generateSelfSignedCert("profile", true), then ProfileEngine loads
        // profile.cert / profile.key
        using var cert = XI.Profile.ProfileEngine.LoadOrCreateCertificate(root, get("auth.ip", "0.0.0.0"), log);
        using var engine = new XI.Profile.ProfileEngine(db, cert, IPAddress.Parse(get("bind", "0.0.0.0")),
            int.Parse(get("profile", "51220")), int.Parse(get("irc", XI.Profile.ProfileEngine.DefaultIrcPort.ToString())),
            s => log("profile: " + s));
        engine.Start();
        try { ct.WaitHandle.WaitOne(); } catch (OperationCanceledException) { }
        return 0;
    }

    private static int RunSearch(IDatabase db, string root, Func<string, string, string> get, Action<string> log, CancellationToken ct)
    {
        var settings = new XI.Search.SearchSettings
        {
            ExpireAuctions = get("expire", "1") != "0", ExpireDays = ushort.Parse(get("expire.days", "3")),
            ExpireInterval = uint.Parse(get("expire.interval", "3600")), OmitNoHistory = get("omitnohistory", "0") != "0",
            AccessWhitelist = XI.Search.SearchSettings.ReadWhitelist(root),
        };
        var whitelist = get("whitelist", null);
        if (whitelist != null) settings.AccessWhitelist = new HashSet<string>(whitelist.Split(',', StringSplitOptions.RemoveEmptyEntries));
        using var engine = new XI.Search.SearchEngine(db, settings, IPAddress.Parse(get("bind", "0.0.0.0")), int.Parse(get("search", "54002")),
            s => log("search: " + s));
        engine.Start();
        log($"search: listening on {engine.Port}");
        try { ct.WaitHandle.WaitOne(); } catch (OperationCanceledException) { }
        return 0;
    }

    private static int RunMap(IDatabase db, XI.Common.Settings settings, string root, Func<string, string, string> get, string endpoint, Action<string> log, CancellationToken ct)
    {
        // MapNetwork.PrefetchLogin: a char's login reads run on a worker before the tick loads it (prefetch=0: in the tick,
        // the C++ way); every query of the process goes through it, so the writes that may touch a read are seen
        var prefetch = new PrefetchDatabase(db);
        db = prefetch;
        string dataDir = System.IO.Path.Combine(root, "data");
        var stats = new XI.Zone.CharStats(XI.Zone.GrowthTables.Load(db), new XI.Zone.ItemData(db), XI.Zone.StatSettings.From(settings));
        var loader = new XI.Zone.CharLoader(db, XI.Zone.Grades.Load(dataDir), XI.Zone.AbilityTable.Load(db, _ => true, stats.Settings.AbilityRecastMultiplier), stats);
        // quest rework piece A and C1: the char_quest_state, char_mission_state, char_assault_state, char_campaign_state and
        // char_eminence_state rows (map.QUEST_STATE_ROWS, C# only)
        loader.Progress.Enabled = settings.Get<bool>("map.QUEST_STATE_ROWS");
        if (loader.Progress.Enabled && !loader.Progress.TablesPresent)
            log("map: map.QUEST_STATE_ROWS needs the char_quest_state, char_mission_state, char_assault_state, char_campaign_state and char_eminence_state tables (python tools/dbtool.py migrates); left off");
        log($"map: QUEST_STATE_ROWS {(loader.Progress.On ? 1 : 0)}");

        // luautils::IsContentEnabled: an ENABLE_<SLUG> that is off only gates content under RESTRICT_CONTENT
        bool On(string key) => settings.All.TryGetValue(key, out var v) && (v is bool b ? b : v is double d && d != 0);
        bool ContentEnabled(string slug) => On("main.ENABLE_" + slug.ToUpperInvariant()) || !On("main.RESTRICT_CONTENT");
        var zone = new XI.Zone.ZoneHost(db, loader, dataDir, get("roe", "1") != "0", log, ContentEnabled);
        // quest rework B2: the C# quests in place of their Lua files (map.CSHARP_QUESTS), less the quest keys left to
        // Lua (map.CSHARP_QUESTS_OFF, a comma list), C# only
        zone.CSharpQuestsEnabled = settings.Get<bool>("map.CSHARP_QUESTS");
        foreach (var key in settings.Get<string>("map.CSHARP_QUESTS_OFF").Split(','))
            if (key.Trim().Length > 0) zone.CSharpQuestsOff.Add(key.Trim());
        log($"map: CSHARP_QUESTS {(zone.CSharpQuestsEnabled ? 1 : 0)}");
        // MapApplication: --ip / --port, the address whose zones (zone_settings zoneip / zoneport) this process serves;
        // 0 / 0 serves every zone
        uint mapIp = IpText.Str2Ip(get("ip", ""));
        ushort mapPort = ushort.Parse(get("port", "0"));
        zone.ProcessIpp = XI.Map.MapIpp.Make(mapIp, mapPort);
        // MapApplication: --lazy (MapConfig::lazyZones)
        zone.LazyZones = get("lazy", "0") != "0";
        // MapEngine::persistSweep: the batch is written on a worker thread
        zone.PersistOnWorker = true;
        log($"map: map_ip {IpText.Ip2Str(mapIp)}, map_port {mapPort}");
        log($"map: Zones assigned to this process: {zone.ZonesAssignedToThisProcess().Count}");
        // zoneutils::Initialize: LoadZoneList, which needs a zone; under --lazy the zones are only managed
        if (zone.LazyZones) log($"map: lazy zones: {zone.ZonesAssignedToThisProcess().Count} managed, loaded on first access");
        else if (zone.ZonesAssignedToThisProcess().Count == 0)
        {
            log("map: Unable to load any zones! Check IP and port params");
            return 1;
        }
        // luautils::init: the scripts/ tree on the embedded LuaJIT
        using var scripts = new XI.Script.ScriptEngine(root, log, s => serverLog_.Write(XI.Common.ServerLog.Error, s));
        // ShowLua: print() on the lua logger (logging.LOG_LUA)
        scripts.LuaPrint = s => serverLog_.Write(XI.Common.ServerLog.Lua, s);
        zone.InitScripts(scripts);
        // MapEngine::do_init: the synth and synergy recipes
        zone.LoadRecipes();
        // luautils::Terminate's std::exit(1), once the chars are written; ForceCrash's crash()
        zone.ExitProcess = code =>
        {
            zone.WaitForPersist();
            serverLog_.Dispose();
            Environment.Exit(code);
        };
        zone.ForceCrash = XI.Common.CrashDump.Force;
        // luautils::init's end (luautils.cpp:578): the file watcher, only with watch=1 (USER RULING 2026-11-05)
        if (get("watch", "0") != "0")
        {
            zone.EnableFileWatcher();
            log($"map: file watcher on: {zone.FileWatcher.Watching} folders (scripts/, modules/, settings/)");
        }
        // instanceutils::Initialize: without --ip/--port this process serves every instance, loaded now (under --lazy on
        // first access)
        zone.InitializeInstances();
        log($"map: {zone.LoadedInstanceCount} instances");
        // ShipHandler::InitializeShips: every zone a ship docks at or crosses is loaded now; not under --lazy
        if (!zone.LazyZones)
        {
            zone.InitializeShips();
            log($"map: {zone.TransportNames.Count} transport runs");
        }
        // the zone loader's campaign::LoadState / LoadNations
        zone.LoadCampaign();
        log($"map: {zone.CampaignState.Regions.Count} campaign regions");
        // MapEngine::do_init's tail: the time-of-day spawns, the expired variables, xi.server.onServerStart
        zone.ServerStart();
        // db::enableTimers: the slow query lines from here on (logging.SQL_SLOW_QUERY_*)
        if (mysql_ != null) mysql_.QueryTimers = true;

        // IPCClient::getZMQRoutingId: --ip/--port, or without them the zone_settings address most zones use
        var ipp = new Ipp(mapIp, mapPort);
        if (mapIp == 0 && mapPort == 0)
        {
            var rs = db.Execute("SELECT zoneip, zoneport FROM zone_settings GROUP BY zoneip, zoneport ORDER BY COUNT(*) DESC", Array.Empty<BoundValue>());
            if (rs != null && rs.Next()) ipp = new Ipp(IpText.Str2Ip(rs.Get<string>("zoneip")), rs.Get<ushort>("zoneport"));
        }
        if (ipp.Raw == 0)
        {
            log("ZMQ Routing ID IPP calculated as 0 - setting to 1. Check your zone_settings!");
            ipp = Ipp.FromRaw(1);
        }

        // MapEngine::do_init: drop the sessions associated with this map process (a crash or kill leaves the char
        // "logged in" and the lobby refuses it with FFXI-3201), but leave other processes' alone; without --ip/--port
        // that is every session
        db.Execute("DELETE FROM accounts_sessions WHERE IF(? = 0 AND ? = 0, true, server_addr = ? AND server_port = ?)",
            new[] { BoundValue.Of(mapIp), BoundValue.Of(mapPort), BoundValue.Of(mapIp), BoundValue.Of(mapPort) });
        using var dealer = new ZmqDealerChannel(endpoint, ipp);

        var store = new XI.Zone.DbMapSessionStore(db);
        var sessions = new XI.Map.MapSessionContainer(store, log: log);
        bool zoneIpFromClient = settings.Get<bool>("network.ZONE_IP_FROM_CLIENT");
        if (zoneIpFromClient && !HasColumn(db, "accounts_sessions", "client_server_addr"))
        {
            log("map: network.ZONE_IP_FROM_CLIENT needs accounts_sessions.client_server_addr (python tools/dbtool.py migrates); left off");
            zoneIpFromClient = false;
        }
        if (zoneIpFromClient) log("map: ZONE_IP_FROM_CLIENT on: a 127.x zone's 0x00B carries the client's own server address");
        var net = new XI.Map.MapNetwork(sessions, store, log) { OnLogin = zone.OnLogin, Dispatch = zone.Dispatch, AfterParse = zone.AfterParse, OnZoneOut = zone.OnZoneOut, ZoneIpFromClient = zoneIpFromClient };
        sessions.OnCharTimeout = zone.OnCharTimeout;
        sessions.OnLinkDead = zone.OnLinkDead;
        if (get("prefetch", "1") != "0")
        {
            zone.LoginPrefetch = prefetch;
            zone.LoginReadWorkers = int.Parse(get("prefetch.workers", "4"));
            net.PrefetchLogin = zone.BeginLoginReads;
        }
        // the tick time the held logins may take once their reads are in (one at least); the others wait a tick
        double loginBudgetMs = double.Parse(get("prefetch.budget", "20"), System.Globalization.CultureInfo.InvariantCulture);
        var ipc = new XI.Zone.MapIpcClient(sessions, zone, log);
        // MapNetworking: the UDP port is --port, else MAP_PORT
        ushort port = mapPort != 0 ? mapPort : ushort.Parse(get("map.port", "54230"));
        int timeout = int.Parse(get("map.timeout", "60"));
        using var socket = new XI.Map.MapSocket(port, log);
        log($"map: UDP {port}; IPC dealer -> {endpoint} as {ipp}");
        // message::send: the chat this map hands the world to route
        zone.SendIpc = m => dealer.Send(XI.Ipc.IpcFraming.ToBytesWithHeader(m));

        // MapStatistics (TotalTickTime, ConnectedPlayers, ...): flushed every stats seconds as one line
        int statsEvery = int.Parse(get("stats", "0"));
        var tickMs = new List<double>();
        var lastStats = DateTime.UtcNow;
        long datagramsIn = 0;
        var tickClock = new System.Diagnostics.Stopwatch();
        while (!ct.IsCancellationRequested)
        {
            tickClock.Restart();
            // IPCClient: CharZone opens a pending session for the char about to arrive, KillSession closes the one a
            // char left here; the chat goes to the zone
            while (dealer.TryReceive(out var frame)) ipc.HandleFrame(frame);

            // the logins whose reads are in, in the order they came
            net.PumpDeferred((to, reply) => socket.Send(to, reply), loginBudgetMs);

            int n = socket.Drain((data, from) =>
            {
                var reply = net.HandleIncoming(data, from);
                if (reply != null) socket.Send(from, reply);
            });
            zone.Tick();

            // MapEngine::sessionCleanup every kSessionCleanupInterval
            sessions.CleanupSessionsIfDue(mapIp, mapPort, timeout);
            if (statsEvery > 0)
            {
                datagramsIn += n;
                tickMs.Add(tickClock.Elapsed.TotalMilliseconds);
                if (DateTime.UtcNow - lastStats >= TimeSpan.FromSeconds(statsEvery))
                {
                    tickMs.Sort();
                    double P(double q) => tickMs[Math.Clamp((int)Math.Ceiling(q * tickMs.Count) - 1, 0, tickMs.Count - 1)];
                    using var self = System.Diagnostics.Process.GetCurrentProcess();
                    log(FormattableString.Invariant(
                        $"map: stats ticks {tickMs.Count} tick_ms p50 {P(0.50):0.00} p99 {P(0.99):0.00} max {tickMs[^1]:0.00} ") +
                        $"datagrams_in {datagramsIn} dropped {socket.IngressDropped} sessions {sessions.Sessions.Count} " +
                        $"pending {sessions.PendingSessions.Count} chars {sessions.Sessions.Count(x => x.Char != null)} " +
                        $"heap_mb {GC.GetTotalMemory(false) / (1024 * 1024)} ws_mb {self.WorkingSet64 / (1024 * 1024)}");
                    tickMs.Clear();
                    datagramsIn = 0;
                    lastStats = DateTime.UtcNow;
                }
            }
            // map_socket.cpp scheduleMainDrain: an idle loop wakes for the next datagram, not a fixed sleep
            if (n == 0) socket.WaitForIngress(2);
        }
        net.DropDeferred();
        sessions.Shutdown();
        zone.WaitForPersist();
        zone.DisableFileWatcher();
        return 0;
    }
}
