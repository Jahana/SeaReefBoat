// Port of world/http_server.{h,cpp}: the world's read-only HTTP API (/api, /api/sessions, /api/ips, /api/zones,
// /api/zones/<id>, /api/settings). HttpApi is the transport-free request handler (what the httplib lambdas do);
// HttpApiHost serves it over System.Net.HttpListener.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using XI.Login;
using XI.Persistence;

namespace XI.World
{
    public readonly struct HttpResponse
    {
        public readonly int Status;
        public readonly string ContentType;
        public readonly string Body;
        public HttpResponse(int status, string contentType, string body) { Status = status; ContentType = contentType; Body = body; }
    }

    public sealed class HttpApi
    {
        public const int MaxZoneId = 300; // MAX_ZONEID
        public static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        private static readonly Regex ZoneRoute = new(@"^/api/zones/(\d+)$", RegexOptions.Compiled);
        private static readonly string[] OmitText = { "logging.", "network.", "password" };
        private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        private readonly IDatabase _db;
        private readonly Func<IReadOnlyDictionary<string, object>> _settings; // settings::visit: bool / double / string values
        private readonly Func<long> _nowMs;
        private readonly Action<string> _log;
        private readonly object _lock = new();

        private long _lastUpdateMs = long.MinValue / 2;
        private uint _activeSessionCount, _activeUniqueIpCount;
        private readonly uint[] _zonePlayerCounts = new uint[MaxZoneId];

        public HttpApi(IDatabase db, Func<IReadOnlyDictionary<string, object>> settings, Func<long> nowMs = null, Action<string> log = null)
        {
            _db = db;
            _settings = settings;
            _nowMs = nowMs ?? (() => Environment.TickCount64);
            _log = log ?? (_ => { });
            LockingUpdate();
        }

        // HTTPServer::LockingUpdate: refresh the cache at most once per minute.
        public void LockingUpdate()
        {
            lock (_lock)
            {
                long now = _nowMs();
                if (now < _lastUpdateMs + (long)CacheTtl.TotalMilliseconds) return;

                _log("API data is stale. Updating...");

                var rset = LoginDb.PreparedStmt(_db, "SELECT COUNT(*) AS `count` FROM accounts_sessions");
                if (rset != null && rset.Next()) _activeSessionCount = rset.Get<uint>("count");

                rset = LoginDb.PreparedStmt(_db, "SELECT COUNT(DISTINCT client_addr) AS `count` FROM accounts_sessions");
                if (rset != null && rset.Next()) _activeUniqueIpCount = rset.Get<uint>("count");

                rset = LoginDb.PreparedStmt(_db, "SELECT chars.pos_zone, COUNT(*) AS `count` FROM chars " +
                                                 "INNER JOIN accounts_sessions ON chars.charid = accounts_sessions.charid GROUP BY pos_zone");
                if (rset != null && rset.RowsCount() != 0)
                {
                    while (rset.Next())
                    {
                        ushort zoneId = rset.Get<ushort>("pos_zone");
                        uint count = rset.Get<uint>("count");
                        // std::array::operator[] is unchecked in C++; ignore out-of-range zones instead of corrupting memory.
                        if (zoneId < MaxZoneId) _zonePlayerCounts[zoneId] = count;
                    }
                }

                _lastUpdateMs = now;
            }
        }

        public HttpResponse Get(string path)
        {
            switch (path)
            {
                case "/api":
                    return new HttpResponse(200, "text/plain", "Hello LSB API");
                case "/api/sessions":
                    LockingUpdate();
                    lock (_lock) return Json(_activeSessionCount.ToString());
                case "/api/ips":
                    LockingUpdate();
                    lock (_lock) return Json(_activeUniqueIpCount.ToString());
                case "/api/zones":
                    LockingUpdate();
                    lock (_lock) return Json("[" + string.Join(",", _zonePlayerCounts) + "]");
                case "/api/settings":
                    return Json(SettingsJson());
            }

            var m = ZoneRoute.Match(path);
            if (m.Success)
            {
                // std::strtol (saturating) then truncating cast to uint16
                long parsed = long.TryParse(m.Groups[1].Value, out var v) ? v : long.MaxValue;
                ushort zoneId = unchecked((ushort)parsed);
                if (zoneId != 0 && zoneId < MaxZoneId)
                {
                    LockingUpdate();
                    lock (_lock) return Json(_zonePlayerCounts[zoneId].ToString());
                }
                return ErrorPage(404);
            }

            return ErrorPage(404);
        }

        private static HttpResponse Json(string body) => new(200, "application/json", body);

        // set_error_handler body (no extra response headers are set on these routes)
        private static HttpResponse ErrorPage(int status) =>
            new(status, "text/html", $"<p>Error Status: <span style='color:red;'>{status} ({StatusMessage(status)})</span></p>");

        private static string StatusMessage(int status) => status switch
        {
            400 => "Bad Request",
            404 => "Not Found",
            405 => "Method Not Allowed",
            500 => "Internal Server Error",
            _ => "Unknown",
        };

        // utils::toASCII(arg, '?'): every non-printable-ASCII byte becomes '?'
        public static string ToAscii(string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            var sb = new StringBuilder(bytes.Length);
            foreach (var b in bytes) sb.Append(b >= 0x20 && b <= 0x7E ? (char)b : '?');
            return sb.ToString();
        }

        private string SettingsJson()
        {
            // nlohmann::json objects are ordered maps (ordinal key order)
            var j = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in _settings())
            {
                string lowerKey = kv.Key.ToLowerInvariant();
                bool omit = false;
                foreach (var text in OmitText) if (lowerKey.Contains(text, StringComparison.Ordinal)) { omit = true; break; }
                if (omit) continue;

                switch (kv.Value)
                {
                    case bool b: j[kv.Key] = b ? "true" : "false"; break;
                    case double d: j[kv.Key] = DoubleJson(d); break;
                    case string s: j[kv.Key] = JsonSerializer.Serialize(ToAscii(s), JsonOpts); break;
                }
            }

            var sb = new StringBuilder("{");
            bool first = true;
            foreach (var kv in j)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(JsonSerializer.Serialize(kv.Key, JsonOpts)).Append(':').Append(kv.Value);
            }
            return sb.Append('}').ToString();
        }

        // nlohmann dumps floating-point numbers with a trailing ".0" when integral
        private static string DoubleJson(double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) return "null";
            var s = d.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            return (s.Contains('.') || s.Contains('E') || s.Contains('e')) ? s : s + ".0";
        }
    }

    // httplib::Server::listen(host, port) analogue.
    public sealed class HttpApiHost : IDisposable
    {
        private readonly HttpApi _api;
        private readonly HttpListener _listener = new();
        private readonly Action<string> _log;
        private readonly Task _loop;

        public HttpApiHost(HttpApi api, string host, ushort port, Action<string> log = null)
        {
            _api = api;
            _log = log ?? (_ => { });
            _log($"Starting HTTP Server on http://{host}:{port}/api");
            _listener.Prefixes.Add($"http://{host}:{port}/");
            _listener.Start();
            _loop = Task.Run(Serve);
        }

        private async Task Serve()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = await _listener.GetContextAsync(); }
                catch (Exception e) when (e is HttpListenerException || e is ObjectDisposedException || e is InvalidOperationException) { return; }

                try
                {
                    var res = ctx.Request.HttpMethod == "GET" ? _api.Get(ctx.Request.Url.AbsolutePath) : new HttpResponse(404, "text/html", "<p>Error Status: <span style='color:red;'>404 (Not Found)</span></p>");
                    if (res.Status >= 500) _log($"Server Error: {res.Status}");
                    else if (res.Status >= 400) _log($"Client Error: {res.Status}");
                    var bytes = Encoding.UTF8.GetBytes(res.Body);
                    ctx.Response.StatusCode = res.Status;
                    ctx.Response.ContentType = res.ContentType;
                    ctx.Response.ContentLength64 = bytes.Length;
                    await ctx.Response.OutputStream.WriteAsync(bytes);
                }
                catch (Exception e)
                {
                    _log($"HTTP handler error: {e.Message}");
                }
                finally
                {
                    try { ctx.Response.Close(); } catch { }
                }
            }
        }

        public void Dispose()
        {
            try { _listener.Stop(); _listener.Close(); } catch { }
            try { _loop.Wait(1000); } catch { }
        }
    }
}
