using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace XI.Common
{
    // The logging.* settings (settings/default/logging.lua) the C++ common layer reads: the line pattern or JSON, the
    // switches of the debug / info / warning / lua lines, the DEBUG_* categories and the slow query thresholds
    public sealed class LogSettings
    {
        public string Pattern = "[%D %T:%e][%&]%^[%n]%$ %v (%!:%#)";
        public bool Json;
        public bool LogDebug = true, LogInfo = true, LogWarning = true, LogLua = true;
        public bool SqlSlowQueryLog = true;
        public uint SqlSlowQueryWarningMs = 100, SqlSlowQueryErrorMs = 250;

        // the DEBUG_<NAME> categories that are on, by NAME (SQL, BAZAARS, ...)
        public readonly HashSet<string> DebugCategories = new HashSet<string>(StringComparer.Ordinal);

        public static LogSettings From(Settings s)
        {
            var d = new LogSettings();
            if (s == null) return d;
            d.Pattern = s.GetString("logging.PATTERN", d.Pattern);
            d.Json = s.GetBool("logging.JSON_ENABLED", d.Json);
            d.LogDebug = s.GetBool("logging.LOG_DEBUG", d.LogDebug);
            d.LogInfo = s.GetBool("logging.LOG_INFO", d.LogInfo);
            d.LogWarning = s.GetBool("logging.LOG_WARNING", d.LogWarning);
            d.LogLua = s.GetBool("logging.LOG_LUA", d.LogLua);
            d.SqlSlowQueryLog = s.GetBool("logging.SQL_SLOW_QUERY_LOG_ENABLE", d.SqlSlowQueryLog);
            d.SqlSlowQueryWarningMs = (uint)s.GetDouble("logging.SQL_SLOW_QUERY_WARNING_TIME", d.SqlSlowQueryWarningMs);
            d.SqlSlowQueryErrorMs = (uint)s.GetDouble("logging.SQL_SLOW_QUERY_ERROR_TIME", d.SqlSlowQueryErrorMs);
            foreach (var (key, value) in s.All)
                if (key.StartsWith("logging.DEBUG_", StringComparison.Ordinal) && value is true)
                    d.DebugCategories.Add(key.Substring("logging.DEBUG_".Length));
            return d;
        }
    }

    // logging::InitializeLog / SetPattern and the Show* macros: one line per message to the console and to the log
    // file (log/<server>-server.log, or with appendDate a file per day, spdlog's daily_file_sink), formatted with
    // logging.PATTERN or as logging.JSON_ENABLED's JSON object. The loggers are the C++ ones: critical, error, lua,
    // warn, info and debug; debug, info, warn and lua lines are dropped while LOG_DEBUG / LOG_INFO / LOG_WARNING /
    // LOG_LUA is off, and a Debug(category) line unless logging.DEBUG_<category> is on.
    // The host's untyped lines (Line) go to the logger their start names (LevelOf): the common prefixes of the C++
    // ShowWarning / ShowError messages are routed to warn / error, every other line stays info.
    // Deliberate differences: a C# line has no source location, so %!, %#, %s, %g and %@ print nothing and %* / %q
    // their 32 blanks (JSON: an empty file and function, line 0), and a "(%!:%#)" group left empty by that, with the
    // blank before it, is dropped from the line; %^ / %$ (spdlog's colour range) print nothing; each line is written
    // at once, not by spdlog's worker thread.
    public sealed class ServerLog : IDisposable
    {
        public const string Critical = "critical", Error = "error", Lua = "lua", Warn = "warn", Info = "info",
                            DebugLogger = "debug";

        private readonly string serverName_;
        private readonly LogSettings settings_;
        private readonly TextWriter console_;
        private readonly string logFile_;
        private readonly bool appendDate_;
        private readonly Func<DateTimeOffset> now_;
        private readonly object lock_ = new object();
        private FileStream file_;
        private Mutex fileMutex_;
        private string openPath_;
        private long noMutexUntil_;

        // how long a line waits for the file's mutex, and how long after a wait that timed out lines are written
        // without it; settable for the tests
        public static int MutexWaitMs = 5000, MutexBackOffMs = 30000;

        public LogSettings Settings => settings_;

        // logFile null: the console only
        public ServerLog(string serverName, LogSettings settings, TextWriter console, string logFile,
                         bool appendDate = false, Func<DateTimeOffset> now = null)
        {
            serverName_ = serverName ?? "";
            settings_ = settings ?? new LogSettings();
            console_ = console;
            logFile_ = logFile;
            appendDate_ = appendDate;
            now_ = now ?? (() => DateTimeOffset.Now);
        }

        // the switch of a logger's lines (critical and error always print)
        public bool Enabled(string logger) => logger switch
        {
            DebugLogger => settings_.LogDebug,
            Info => settings_.LogInfo,
            Warn => settings_.LogWarning,
            Lua => settings_.LogLua,
            _ => true,
        };

        public bool DebugOn(string category) => settings_.DebugCategories.Contains(category);

        // the start of a host line and the logger of the C++ Show* calls it is common to; matched after a "lua: "
        // prefix, the first match wins. The first rows are ShowDebug ports (or a C#-only line) a broader prefix below
        // would catch: they stay info
        private static readonly (string Prefix, string Logger)[] Levels =
        {
            ("Attempt to adjust job points", Info), ("Attempt to reduce job points", Info),
            ("Unable to remove award", Info), ("luautils::LoadLuaObjectFromFile", Info),
            ("Invalid entity", Warn), ("Invalid Entity", Warn), ("Attempting to", Warn), ("Attempt to", Warn),
            ("luautils::", Error), ("Failed to", Error), ("Unable to", Error), ("Query Failed", Error),
        };

        // the logger of an untyped host line: a common ShowWarning / ShowError prefix, a persist write that failed
        // (an error), else info
        public static string LevelOf(string message)
        {
            string m = message.StartsWith("lua: ", StringComparison.Ordinal) ? message.Substring(5) : message;
            foreach (var (prefix, logger) in Levels)
                if (m.StartsWith(prefix, StringComparison.Ordinal)) return logger;
            if (m.StartsWith("persist: ", StringComparison.Ordinal) && m.Contains(" failed")) return Error;
            return Info;
        }

        // a host line, on the logger LevelOf names
        public void Line(string message) => Write(LevelOf(message), message);

        public void Write(string logger, string message)
        {
            if (!Enabled(logger)) return;
            Emit(logger, message);
        }

        // Debug<Category>(...): a debug line while logging.DEBUG_<CATEGORY> is on (LOG_DEBUG does not gate it)
        public void Debug(string category, string message)
        {
            if (DebugOn(category)) Emit(DebugLogger, message);
        }

        private void Emit(string logger, string message)
        {
            var now = now_();
            string line = settings_.Json
                ? JsonLine(logger, message, now)
                : Format(settings_.Pattern, logger, message, now);
            lock (lock_)
            {
                console_?.WriteLine(line);
                var file = File(now);
                if (file != null)
                {
                    // spdlog's "ab": each line at the file's end, so two processes of one kind (two map servers)
                    // appending to the same file do not write over each other's lines. A FileStream keeps its own
                    // position, and a seek then a write is not one step between two processes, so the file's named
                    // mutex holds the two together. A wait that times out (a process stuck holding it) writes
                    // without it for MutexBackOffMs, said once, so each line does not stall that long
                    bool held = false;
                    try
                    {
                        string notice = null;
                        if (Environment.TickCount64 >= noMutexUntil_)
                        {
                            try
                            {
                                held = fileMutex_.WaitOne(MutexWaitMs);
                            }
                            catch (AbandonedMutexException)
                            {
                                held = true; // a process that ended while it held the mutex: it is this one's now
                            }
                            if (!held)
                            {
                                noMutexUntil_ = Environment.TickCount64 + MutexBackOffMs;
                                string text = $"ServerLog: the lock of {openPath_} not taken in {MutexWaitMs} " +
                                              $"ms: writing without it for {MutexBackOffMs} ms";
                                notice = settings_.Json ? JsonLine(Warn, text, now)
                                                        : Format(settings_.Pattern, Warn, text, now);
                                console_?.WriteLine(notice);
                            }
                        }
                        file.Seek(0, SeekOrigin.End);
                        file.Write(Utf8.GetBytes((notice != null ? notice + Environment.NewLine : "") +
                                                 line + Environment.NewLine));
                        file.Flush();
                    }
                    catch (IOException) { }
                    finally
                    {
                        if (held) fileMutex_.ReleaseMutex();
                    }
                }
            }
        }

        // the sink's file: logFile_, or under appendDate spdlog's daily name (<base>_YYYY-MM-DD<ext>), opened again
        // when the day changes
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        private FileStream File(DateTimeOffset now)
        {
            if (logFile_ == null) return null;
            string path = appendDate_ ? DailyName(logFile_, now) : logFile_;
            if (path == openPath_) return file_;
            file_?.Dispose();
            file_ = null;
            fileMutex_?.Dispose();
            fileMutex_ = null;
            openPath_ = path;
            try
            {
                fileMutex_ = new Mutex(false, MutexName(path));
                string dir = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                // several processes of one kind (two map servers) share the file, as spdlog's sinks do
                file_ = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write,
                                       FileShare.ReadWrite | FileShare.Delete);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException
                                          or WaitHandleCannotBeOpenedException)
            {
                file_?.Dispose();
                file_ = null;
                console_?.WriteLine($"log: cannot open {path}: {e.Message}");
            }
            return file_;
        }

        // the named mutex of a log file, one per full path (case folded), across the processes
        public static string MutexName(string path)
        {
            string full = Path.GetFullPath(path).ToUpperInvariant();
            byte[] hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(full));
            return "xi-log-" + Convert.ToHexString(hash, 0, 16);
        }

        public static string DailyName(string path, DateTimeOffset now)
        {
            string ext = Path.GetExtension(path);
            string stem = ext.Length > 0 ? path.Substring(0, path.Length - ext.Length) : path;
            return $"{stem}_{now:yyyy-MM-dd}{ext}";
        }

        // spdlog's level name of each logger (%l, %L): lua logs at info
        private static string LevelName(string logger) => logger switch
        {
            Critical => "critical",
            Error => "error",
            Warn => "warning",
            DebugLogger => "debug",
            _ => "info",
        };

        private static string ShortLevel(string logger) => logger switch
        {
            Critical => "C",
            Error => "E",
            Warn => "W",
            DebugLogger => "D",
            _ => "I",
        };

        // spdlog's pattern_formatter over the flags the C++ patterns use, with spdlog's padding (%8l right-aligned,
        // %-8l left, %=8l centred, a ! after the width truncating)
        public string Format(string pattern, string logger, string message, DateTimeOffset now)
        {
            // the source location group a C# line leaves empty, with its blank
            pattern = pattern.Replace(" (%!:%#)", "").Replace("(%!:%#)", "");
            var sb = new StringBuilder();
            for (int i = 0; i < pattern.Length; i++)
            {
                char c = pattern[i];
                if (c != '%' || i + 1 >= pattern.Length)
                {
                    sb.Append(c);
                    continue;
                }
                i++;
                char align = 'r';
                if (pattern[i] is '-' or '=')
                {
                    align = pattern[i] == '-' ? 'l' : 'c';
                    i++;
                }
                int width = 0;
                while (i < pattern.Length && char.IsDigit(pattern[i])) width = width * 10 + (pattern[i++] - '0');
                bool truncate = false;
                if (i < pattern.Length && pattern[i] == '!' && width > 0)
                {
                    truncate = true;
                    i++;
                }
                if (i >= pattern.Length) break;
                string field = Flag(pattern[i], logger, message, now);
                // spdlog prints a flag it does not know as written, without padding
                if (field == null) sb.Append('%').Append(pattern[i]);
                else sb.Append(width > 0 ? Pad(field, width, align, truncate) : field);
            }
            return sb.ToString();
        }

        private static string Pad(string s, int width, char align, bool truncate)
        {
            if (s.Length >= width) return truncate ? s.Substring(0, width) : s;
            int pad = width - s.Length;
            return align switch
            {
                'l' => s + new string(' ', pad),
                'c' => new string(' ', pad / 2) + s + new string(' ', pad - pad / 2),
                _ => new string(' ', pad) + s,
            };
        }

        private string Flag(char flag, string logger, string message, DateTimeOffset now)
        {
            var inv = CultureInfo.InvariantCulture;
            return flag switch
            {
                'v' => message,
                'n' => logger,
                'l' => LevelName(logger),
                'L' => ShortLevel(logger),
                '&' => serverName_,
                '_' => serverName_.Length > 0 ? char.ToUpperInvariant(serverName_[0]).ToString() : "",
                't' => Environment.CurrentManagedThreadId.ToString(inv),
                'P' => Environment.ProcessId.ToString(inv),
                'Y' => now.ToString("yyyy", inv),
                'y' => now.ToString("yy", inv),
                'm' => now.ToString("MM", inv),
                'd' => now.ToString("dd", inv),
                'H' => now.ToString("HH", inv),
                'I' => now.ToString("hh", inv),
                'M' => now.ToString("mm", inv),
                'S' => now.ToString("ss", inv),
                'p' => now.ToString("tt", inv),
                'e' => now.ToString("fff", inv),
                'f' => now.ToString("ffffff", inv),
                'F' => now.ToString("fffffff", inv) + "00",
                'D' => now.ToString("MM/dd/yy", inv),
                'T' or 'X' => now.ToString("HH:mm:ss", inv),
                'R' => now.ToString("HH:mm", inv),
                'r' => now.ToString("hh:mm:ss tt", inv),
                'a' => now.ToString("ddd", inv),
                'A' => now.ToString("dddd", inv),
                'b' or 'h' => now.ToString("MMM", inv),
                'B' => now.ToString("MMMM", inv),
                'c' => now.ToString("ddd MMM d HH:mm:ss yyyy", inv),
                'C' => now.ToString("yy", inv),
                'x' => now.ToString("MM/dd/yy", inv),
                'z' => now.ToString("zzz", inv),
                'E' => now.ToUnixTimeSeconds().ToString(inv),
                '*' or 'q' => new string(' ', 32),
                '%' => "%",
                // %^ %$ (the colour range) and the source location (%! %# %s %g %@): nothing
                '^' or '$' or '!' or '#' or 's' or 'g' or '@' => "",
                // a flag spdlog does not know
                _ => null,
            };
        }

        // logging::detail::renderJsonLine: {"ts":"<UTC>.mmmZ","lvl":"<logger>","source":{...},"msg":"..."}
        public static string JsonLine(string logger, string message, DateTimeOffset now)
        {
            var utc = now.UtcDateTime;
            var inv = CultureInfo.InvariantCulture;
            string ts = utc.ToString("yyyy-MM-ddTHH:mm:ss", inv) + "." + utc.Millisecond.ToString("000", inv) + "Z";
            return "{\"ts\":\"" + ts + "\",\"lvl\":\"" + JsonEscape(logger) +
                   "\",\"source\":{\"file\":\"\",\"line\":0,\"function\":\"\"},\"msg\":\"" +
                   JsonEscape(message) + "\"}";
        }

        private static string JsonEscape(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        public void Dispose()
        {
            lock (lock_)
            {
                file_?.Dispose();
                file_ = null;
                fileMutex_?.Dispose();
                fileMutex_ = null;
                openPath_ = null;
            }
        }
    }
}
