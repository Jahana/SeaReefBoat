using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using XI.Common;

// XI.Verify.Settings -- Settings reader vs. the shipped settings/default/*.lua (independent scalar-line count) + override rules.
internal static class Program
{
    private static int _pass, _fail;
    private static void Check(string n, object e, object a)
    {
        bool ok = Equals(e?.ToString(), a?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {n}" + (ok ? "" : $"  expected=[{e}] actual=[{a}]"));
    }

    // the csharp folder (settings/, scripts/, data/ ... copied from the C++ server), found above the test's bin folder
    private static string FindServerRoot()
    {
        for (var d = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory); d != null; d = d.Parent)
            if (System.IO.File.Exists(System.IO.Path.Combine(d.FullName, "settings", "default", "main.lua"))) return d.FullName;
        throw new System.IO.DirectoryNotFoundException("settings/default/main.lua not found above " + System.AppContext.BaseDirectory);
    }

    // lane engine-small-2: the logging.* settings (LogSettings, ServerLog) and main.GENERATE_CORE_DUMP's minidump
    private static void RunServerLog(Settings s)
    {
        var d = LogSettings.From(s);
        Check("logging.PATTERN: the shipped classic pattern", "[%D %T:%e][%&]%^[%n]%$ %v (%!:%#)", d.Pattern);
        Check("logging.*: the shipped switches", "False/True/True/True/True/True/100/250",
              $"{d.Json}/{d.LogDebug}/{d.LogInfo}/{d.LogWarning}/{d.LogLua}/{d.SqlSlowQueryLog}/" +
              $"{d.SqlSlowQueryWarningMs}/{d.SqlSlowQueryErrorMs}");
        Check("logging.DEBUG_*: all off as shipped", "", string.Join(",", d.DebugCategories));
        var o = Settings.Parse("xi = xi or {}\nxi.settings = xi.settings or {}\nxi.settings.logging =\n{\n" +
                               " LOG_INFO = false,\n LOG_LUA = false,\n DEBUG_SQL = true,\n JSON_ENABLED = true,\n" +
                               " SQL_SLOW_QUERY_WARNING_TIME = 7,\n}\n");
        var od = LogSettings.From(o);
        Check("logging.* read from a settings file", "False/False/SQL/True/7",
              $"{od.LogInfo}/{od.LogLua}/{string.Join(",", od.DebugCategories)}/{od.Json}/" +
              $"{od.SqlSlowQueryWarningMs}");

        var at = new DateTimeOffset(2022, 12, 3, 19, 25, 12, 812, TimeSpan.Zero);
        var console = new StringWriter();
        string dir = Path.Combine(Path.GetTempPath(), $"xi-serverlog-{Environment.ProcessId}");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        string file = Path.Combine(dir, "log", "map-server.log");
        using (var log = new ServerLog("map", new LogSettings(), console, file, false, () => at))
        {
            log.Write(ServerLog.Info, "do_init: connecting to database");
            log.Write(ServerLog.Warn, "a warning");
            log.Write(ServerLog.Lua, "from print");
            log.Write(ServerLog.DebugLogger, "a debug line");
            log.Debug("SQL", "preparedStmt: SELECT 1");
        }
        var lines = console.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Check("ServerLog: the classic pattern (no source location: the empty \"(%!:%#)\" dropped)",
              "[12/03/22 19:25:12:812][map][info] do_init: connecting to database", lines.FirstOrDefault());
        Check("  the logger names: warn, lua, debug; DEBUG_SQL off drops its line",
              "[warn] a warning|[lua] from print|[debug] a debug line",
              string.Join("|", lines.Skip(1)
                  .Select(l => l.Substring(l.IndexOf("][map]", StringComparison.Ordinal) + 6))));
        Check("  the file log/map-server.log holds the same lines", console.ToString(),
              File.Exists(file) ? File.ReadAllText(file) : "no file");

        var off = new LogSettings { LogDebug = false, LogInfo = false, LogWarning = false, LogLua = false };
        off.DebugCategories.Add("SQL");
        var c2 = new StringWriter();
        using (var log = new ServerLog("map", off, c2, null, false, () => at))
        {
            log.Write(ServerLog.Info, "i");
            log.Write(ServerLog.Warn, "w");
            log.Write(ServerLog.Lua, "l");
            log.Write(ServerLog.DebugLogger, "d");
            log.Write(ServerLog.Error, "e");
            log.Write(ServerLog.Critical, "c");
            log.Debug("SQL", "q");
        }
        Check("LOG_DEBUG / INFO / WARNING / LUA off: only error, critical and the DEBUG_SQL line", "e|c|q",
              string.Join("|", c2.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                  .Select(l => l.Substring(l.IndexOf("] ", StringComparison.Ordinal) + 2))));

        var nl = new ServerLog("map", new LogSettings(), null, null, false, () => at);
        Check("the 'New' pattern: %Y/%m/%d %T:%e | %_ | %^%-4!l%$ | %* | %v",
              "2022/12/03 19:25:12:812 | M | info | " + new string(' ', 32) + " | hello",
              nl.Format("%Y/%m/%d %T:%e | %_ | %^%-4!l%$ | %* | %v", ServerLog.Info, "hello", at));
        Check("  %-4!l truncates warning to 'warn'", "warn|", nl.Format("%-4!l|", ServerLog.Warn, "", at));
        Check("  %8n right-aligned", "    info", nl.Format("%8n", ServerLog.Info, "", at));
        Check("  %c: spdlog's c_formatter (the day unpadded)", "Sat Dec 3 19:25:12 2022",
              nl.Format("%c", ServerLog.Info, "", at));
        Check("  a flag spdlog does not know printed as written, unpadded", "%k|%k|",
              nl.Format("%k|%5k|", ServerLog.Info, "", at));
        Check("  a source location group with text around it: only the empty group goes", "[x] msg (in)",
              nl.Format("[x] %v (%!:%#) (in)", ServerLog.Info, "msg", at));

        // the host's untyped lines: the logger of the C++ Show* call each ports
        Check("ServerLog.LevelOf: ShowWarning's and ShowError's messages, else info",
              "warn|warn|warn|warn|error|error|error|error|info|info",
              string.Join("|", new[]
              {
                  "Invalid entity type calling function (x).", "lua: Invalid entity type calling function (x).",
                  "Attempting to set variable", "Attempt to save Char XP", "luautils::callGlobalFunction: x",
                  "Failed to load x", "persist: char 1: writing its char_effects rows failed (rolled back)",
                  "Query Failed: SELECT 1", "map: UDP 54230", "persist: char 1 retried its char_effects write",
              }.Select(ServerLog.LevelOf)));
        var lc = new StringWriter();
        using (var log = new ServerLog("map", new LogSettings { LogInfo = false }, lc, null, false, () => at))
        {
            log.Line("map: UDP 54230");
            log.Line("Failed to load x");
        }
        Check("  LOG_INFO off: an error-class host line still printed",
              "[12/03/22 19:25:12:812][map][error] Failed to load x", lc.ToString().Trim());
        Check("  the ShowDebug ports a broader prefix would catch stay info", "info|info|info|info",
              string.Join("|", new[]
              {
                  "Attempt to adjust job points for an invalid job for (x).",
                  "Attempt to reduce job points below 0 for (x).",
                  "Unable to remove award on fishing contest for player x.",
                  "luautils::LoadLuaObjectFromFile: x.lua: y",
              }.Select(ServerLog.LevelOf)));

        // two processes of one kind (two map servers) on one log file: each line at the end, none written over
        string shared = Path.Combine(dir, "log", "shared-server.log");
        using (var a = new ServerLog("map", new LogSettings { Pattern = "%v" }, null, shared, false, () => at))
        using (var b = new ServerLog("map", new LogSettings { Pattern = "%v" }, null, shared, false, () => at))
        {
            for (int i = 0; i < 3; i++)
            {
                a.Write(ServerLog.Info, $"a{i}");
                b.Write(ServerLog.Info, $"b{i}");
            }
        }
        Check("two writers on one file: every line kept, in order", "a0|b0|a1|b1|a2|b2",
              string.Join("|", File.ReadAllLines(shared)));

        // the file's mutex held elsewhere (a stuck process): one wait times out, said once, then the lines go out
        // without it for the back-off, not each after a wait of its own
        string stuck = Path.Combine(dir, "log", "stuck-server.log");
        var (waitMs, backOffMs) = (ServerLog.MutexWaitMs, ServerLog.MutexBackOffMs);
        (ServerLog.MutexWaitMs, ServerLog.MutexBackOffMs) = (200, 30000);
        using (var holding = new System.Threading.ManualResetEventSlim())
        using (var release = new System.Threading.ManualResetEventSlim())
        {
            var holder = new System.Threading.Thread(() =>
            {
                using var m = new System.Threading.Mutex(false, ServerLog.MutexName(stuck));
                m.WaitOne();
                holding.Set();
                release.Wait();
                m.ReleaseMutex();
            });
            holder.Start();
            holding.Wait();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            using (var log = new ServerLog("map", new LogSettings { Pattern = "%v" }, null, stuck, false, () => at))
                for (int i = 0; i < 20; i++) log.Write(ServerLog.Info, $"s{i}");
            timer.Stop();
            release.Set();
            holder.Join();
            (ServerLog.MutexWaitMs, ServerLog.MutexBackOffMs) = (waitMs, backOffMs);
            var stuckLines = File.ReadAllLines(stuck);
            Check("the file's mutex held elsewhere: one wait, not one per line (20 lines, 200 ms wait)", "True",
                  (timer.ElapsedMilliseconds < 1500).ToString());
            int said = stuckLines.Count(l => l.Contains("writing without it"));
            Check("  said once, then every line written", "1/20", $"{said}/{stuckLines.Count(l => l.StartsWith("s"))}");
        }

        // two processes writing one file at once (two map servers): every line whole, none lost
        string together = Path.Combine(dir, "log", "together-server.log");
        string go = Path.Combine(dir, "go");
        const int Lines = 3000;
        var writers = new[] { "p", "q" }.Select(tag => System.Diagnostics.Process.Start(
            Child("--log-writer", together, tag, Lines.ToString(), go))).ToList();
        System.Threading.Thread.Sleep(1500);
        File.WriteAllText(go, "");
        foreach (var w in writers)
        {
            w.WaitForExit(120000);
            w.Dispose();
        }
        var written = File.Exists(together) ? File.ReadAllLines(together) : Array.Empty<string>();
        var whole = new HashSet<string>(Enumerable.Range(0, Lines).SelectMany(i => new[] { $"p {i}", $"q {i}" }));
        Check("two processes on one log file at once: every line there, whole", $"{2 * Lines}/0",
              $"{written.Count(whole.Contains)}/{written.Count(l => !whole.Contains(l))}");
        Check("JSON_ENABLED: renderJsonLine's object",
              "{\"ts\":\"2022-12-03T19:25:12.812Z\",\"lvl\":\"error\",\"source\":{\"file\":\"\",\"line\":0," +
              "\"function\":\"\"},\"msg\":\"a \\\"q\\\"\\n\"}",
              ServerLog.JsonLine(ServerLog.Error, "a \"q\"\n", at));
        var jc = new StringWriter();
        using (var log = new ServerLog("map", new LogSettings { Json = true }, jc, null, false, () => at))
            log.Write(ServerLog.Info, "x");
        Check("  the sink writes it when on", ServerLog.JsonLine(ServerLog.Info, "x", at), jc.ToString().Trim());
        Check("--append-date: daily_file_sink's name", Path.Combine("log", "map-server_2022-12-03.log"),
              ServerLog.DailyName(Path.Combine("log", "map-server.log"), at));

        // main.GENERATE_CORE_DUMP: the minidump of this process under dmp/
        if (OperatingSystem.IsWindows())
        {
            string dump = CrashDump.WriteMiniDump(dir);
            Check("CrashDump.WriteMiniDump: dmp/tombstone_<pid>_<time>.dmp written", "True",
                  (dump != null && File.Exists(dump) && new FileInfo(dump).Length > 0 &&
                   Path.GetFileName(dump).StartsWith($"tombstone_{Environment.ProcessId}_") &&
                   Path.GetFileName(Path.GetDirectoryName(dump)) == "dmp").ToString());
        }
        try
        {
            Directory.Delete(dir, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }

        // ForceCrash: the crash reported, then TerminateProcess with EXCEPTION_ACCESS_VIOLATION (no Windows Error
        // Reporting, no FailFast); a child of this suite (--force-crash) crashes so
        if (OperatingSystem.IsWindows())
        {
            var psi = Child("--force-crash");
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            using var child = System.Diagnostics.Process.Start(psi);
            string output = child.StandardOutput.ReadToEnd() + child.StandardError.ReadToEnd();
            child.WaitForExit(60000);
            Check("CrashDump.Force: the process ends with 0xC0000005", "C0000005",
                  ((uint)child.ExitCode).ToString("X8"));
            Check("  the crash line written first, and no FailFast message", "True",
                  (output.Contains("crash: EXCEPTION_ACCESS_VIOLATION: ForceCrash") &&
                   !output.Contains("FailFast")).ToString());
        }
    }

    // a child process of this suite: this exe again, or under "dotnet XI.Verify.Settings.dll" dotnet with the dll
    private static System.Diagnostics.ProcessStartInfo Child(params string[] args)
    {
        string exe = Environment.ProcessPath;
        var psi = new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = false };
        if (string.Equals(Path.GetFileNameWithoutExtension(exe), "dotnet", StringComparison.OrdinalIgnoreCase))
            psi.ArgumentList.Add(System.Reflection.Assembly.GetEntryAssembly().Location);
        foreach (var a in args) psi.ArgumentList.Add(a);
        return psi;
    }

    private static int Main(string[] args)
    {
        // a child of RunServerLog's: <n> lines "<tag> <i>" to the file once the go file is there (given up after
        // 60 s, so a parent that died does not leave it waiting)
        if (args.Length == 5 && args[0] == "--log-writer")
        {
            var waited = System.Diagnostics.Stopwatch.StartNew();
            while (!File.Exists(args[4]))
            {
                if (waited.ElapsedMilliseconds > 60000) return 3;
                System.Threading.Thread.Sleep(10);
            }
            using var log = new ServerLog("map", new LogSettings { Pattern = "%v" }, null, args[1]);
            for (int i = 0; i < int.Parse(args[3]); i++) log.Write(ServerLog.Info, $"{args[2]} {i}");
            return 0;
        }
        // the child RunServerLog crashes: CrashDump.Force as the ForceCrash binding calls it
        if (args.Length > 0 && args[0] == "--force-crash")
        {
            CrashDump.Log = s => Console.WriteLine(s);
            CrashDump.Force();
            return 2;
        }
        string root = args.Length > 0 ? args[0] : FindServerRoot();
        var s = Settings.Load(root, _ => null);

        // independent expectation: top-level scalar KEY = value lines inside each default block (4-space indent)
        int expected = 0;
        foreach (var f in Directory.GetFiles(Path.Combine(root, "settings", "default"), "*.lua"))
            foreach (var l in File.ReadAllLines(f))
                if (Regex.IsMatch(l, @"^    [A-Z_0-9]+\s*=\s*(-?[0-9][0-9.xA-Fa-f]*|true|false|'[^']*'|""[^""]*"")\s*(,|(--.*)?$)"))
                    expected++;
        Check("scalar count == independent line count", expected, s.All.Count);

        Check("network.SQL_HOST", "127.0.0.1", s.GetString("network.SQL_HOST"));
        Check("network.SQL_PORT", 3306, s.GetInt("network.SQL_PORT"));
        Check("network.LOGIN_AUTH_PORT", 54231, s.GetInt("network.LOGIN_AUTH_PORT"));
        Check("nested table skipped", false, s.Has("main.START"));
        Check("bool parsed", true, s.All.Values.OfType<bool>().Any());

        var o = Settings.Parse("xi = xi or {}\nxi.settings = xi.settings or {}\nxi.settings.network =\n{\n SQL_HOST = 'db', -- c\n}\nxi.settings.main.X = 5\nxi.settings.main.Y = 'a--b'\n");
        Check("table override", "db", o.GetString("network.SQL_HOST"));
        Check("dotted override", 5, o.GetInt("main.X"));
        Check("comment marker in string kept", "a--b", o.GetString("main.Y"));

        var e = Settings.Load(root, k => k == "XI_NETWORK_SQL_PORT" ? " 3307 " : null);
        Check("env override numeric", 3307, e.GetInt("network.SQL_PORT"));

        // settings.cpp isNumber: only an all-digit override is a number; settings::get<T> reads the rest as a string
        var ev = Settings.Load(root, k => k switch { "XI_MAP_EXP_RATE" => "1.5", "XI_LOGIN_LOG_USER_IP" => "true", "XI_LOGIN_VER_LOCK" => "-1", _ => null });
        Check("env override with a dot: a string", "1.5", ev.GetString("map.EXP_RATE"));
        Check("get<float> of a truthy string: 1", 1f, ev.Get<float>("map.EXP_RATE"));
        Check("get<bool> of env 'true'", true, ev.Get<bool>("login.LOG_USER_IP"));
        Check("get<uint8> of env '-1' (a truthy string)", (byte)1, ev.Get<byte>("login.VER_LOCK"));
        Check("get<uint16> of a number", (ushort)54231, s.Get<ushort>("network.LOGIN_AUTH_PORT"));
        Check("get<std::string> of a number", "54231", s.Get<string>("network.LOGIN_AUTH_PORT"));
        Check("get<bool> of a missing key: false", false, s.Get<bool>("login.NO_SUCH_KEY"));
        Check("login.BANNED_WORDS_LIST from the shipped file", "badword", string.Join(",", s.GetStringList("login.BANNED_WORDS_LIST") ?? new List<string>()));

        RunServerLog(s);

        Console.WriteLine($"{_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }
}
