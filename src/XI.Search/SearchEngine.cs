// src/search/search_engine.cpp + search_listener.h: the search server's listener (one SearchHandler per connection, at
// most 5 per address outside ACCESS_WHITELIST) and the auction house expiry task.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using XI.Persistence;

namespace XI.Search
{
    public sealed class SearchSettings
    {
        public bool ExpireAuctions = true;   // search.EXPIRE_AUCTIONS
        public ushort ExpireDays = 3;        // search.EXPIRE_DAYS
        public uint ExpireInterval = 3600;   // search.EXPIRE_INTERVAL (seconds)
        public bool OmitNoHistory;           // search.OMIT_NO_HISTORY
        public HashSet<string> AccessWhitelist = new(); // search.ACCESS_WHITELIST

        // ACCESS_WHITELIST is a table, which the scalar settings scrape skips: the quoted strings of the block in
        // settings/search.lua, else settings/default/search.lua (the user file replaces the whole table, as in Lua)
        public static HashSet<string> ReadWhitelist(string root)
        {
            bool defaultsOnly = Environment.GetEnvironmentVariable("XI_SETTINGS_DEFAULTS_ONLY") == "1"; // Settings.DefaultsOnly
            foreach (var file in defaultsOnly ? new[] { Path.Combine(root, "settings", "default", "search.lua") }
                                              : new[] { Path.Combine(root, "settings", "search.lua"), Path.Combine(root, "settings", "default", "search.lua") })
            {
                if (!File.Exists(file)) continue;
                var text = Regex.Replace(File.ReadAllText(file), "--[^\n]*", "");
                var block = Regex.Match(text, @"ACCESS_WHITELIST\s*=\s*\{(?<body>[^}]*)\}");
                if (!block.Success) continue;
                var set = new HashSet<string>();
                foreach (Match m in Regex.Matches(block.Groups["body"].Value, "'([^']*)'|\"([^\"]*)\""))
                {
                    set.Add(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value);
                }
                return set;
            }
            return new HashSet<string>();
        }
    }

    public sealed class SearchEngine : IDisposable
    {
        private const int MaxSessionsPerAddress = 5;

        private readonly SearchDataLoader _data;
        private readonly SearchSettings _settings;
        private readonly Action<string> _log;
        private readonly Action<string> _debug;
        private readonly TcpListener _listener;
        private readonly Dictionary<string, ushort> _ipInFlight = new();
        private readonly CancellationTokenSource _cts = new();
        private Timer _cleanup;

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public SearchDataLoader Data => _data;

        public SearchEngine(IDatabase db, SearchSettings settings, IPAddress bind, int port, Action<string> log, Action<string> debug = null)
        {
            _settings = settings;
            _log = log ?? (_ => { });
            _debug = debug;
            _data = new SearchDataLoader(db, _log) { OmitNoHistory = settings.OmitNoHistory };
            _listener = new TcpListener(bind, port);
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        }

        public void Start()
        {
            _listener.Start();
            _ = AcceptLoopAsync();
            // SearchEngine::SearchEngine + onInitialize: the expiry runs once at startup (on a worker) and then every
            // EXPIRE_INTERVAL seconds
            if (_settings.ExpireAuctions)
            {
                _log($"AH task to return items older than {_settings.ExpireDays} days is running");
                var interval = TimeSpan.FromSeconds(_settings.ExpireInterval);
                _ = Task.Run(() => ExpireAH(_settings.ExpireDays));
                _cleanup = new Timer(_ => ExpireAH(_settings.ExpireDays), null, interval, interval);
            }
        }

        // onAHCleanup / onExpireAll (the console's ah_cleanup and expire_all): null days expires everything
        public void ExpireAH(ushort? days)
        {
            try { _data.ExpireAHItems(days ?? 0); }
            catch (Exception e) { _log($"AH expiry failed: {e.Message}"); }
        }

        private bool Whitelisted(string ip) => _settings.AccessWhitelist.Contains(ip);

        private async Task AcceptLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await _listener.AcceptTcpClientAsync(_cts.Token); }
                catch (OperationCanceledException) { return; }
                catch (ObjectDisposedException) { return; }
                catch (Exception e)
                {
                    _log($"Failed to accept connection: {e.Message}");
                    continue;
                }
                _ = Task.Run(() => ServeAsync(client));
            }
        }

        private async Task ServeAsync(TcpClient client)
        {
            using (client)
            {
                string ip;
                try { ip = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString(); }
                catch (Exception) { return; }

                // SearchHandler::SearchHandler: addToUsedIPAddresses, then more than 5 closes this one
                bool counted = !Whitelisted(ip);
                if (counted)
                {
                    lock (_ipInFlight)
                    {
                        _ipInFlight.TryGetValue(ip, out var n);
                        _ipInFlight[ip] = (ushort)(n + 1);
                    }
                }
                try
                {
                    ushort inUse;
                    lock (_ipInFlight) inUse = counted && _ipInFlight.TryGetValue(ip, out var n) ? n : (ushort)0;
                    if (inUse > MaxSessionsPerAddress)
                    {
                        _log($"More than {MaxSessionsPerAddress} simultaneous connections from {ip}. Closing socket.");
                        return;
                    }
                    await new SearchHandler(client.GetStream(), ip, _data, _log, _debug).RunAsync();
                }
                finally
                {
                    // ~SearchHandler: removeFromUsedIPAddresses
                    if (counted)
                    {
                        lock (_ipInFlight)
                        {
                            if (_ipInFlight.TryGetValue(ip, out var n))
                            {
                                if (n <= 1) _ipInFlight.Remove(ip);
                                else _ipInFlight[ip] = (ushort)(n - 1);
                            }
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cleanup?.Dispose();
            try { _listener.Stop(); } catch { }
            _cts.Dispose();
        }
    }
}
