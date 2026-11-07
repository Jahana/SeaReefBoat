// Port of the in-memory state in login_helpers.cpp that the auth session touches:
// per-IP login-failure lockout (5 failures / 60s window) and the authenticated-session table
// keyed [ip][sessionHash] (session_t, session.h).

using System;
using System.Collections.Generic;
using System.Text;

namespace XI.Login
{
    public sealed class LoginSession
    {
        public uint AccountId;
        public uint ServerIp;
        public uint RequestedCharacterId;
        public string RequestedNewCharacterName = "";
        public bool VersionMismatch;
        public uint KeyCounter;
        public byte KeyRequest;
        public DateTimeOffset AuthorizedTime;
        public ILobbyChannel View;   // session_t::view_session (port 54001), null once handed off
        public DataSession Data;     // session_t::data_session (port 54230)
    }

    // The slice of handler_session the data/view sessions use on each other: an async do_write and
    // closing the socket (shutdown + close). Implemented by the TLS loops; faked in tests.
    public interface ILobbyChannel
    {
        void Write(byte[] data);
        void Close();
    }

    public sealed class LoginSessions
    {
        public static readonly TimeSpan LoginFailureWindow = TimeSpan.FromSeconds(60);
        public const int LoginFailureLimit = 5;

        private readonly TimeProvider _time;
        private readonly object _lock = new object();
        private readonly Dictionary<string, (byte Count, DateTimeOffset Last)> _failures = new();
        private readonly Dictionary<string, Dictionary<string, LoginSession>> _sessions = new();

        public LoginSessions(TimeProvider time = null) { _time = time ?? TimeProvider.System; }

        public bool IsLoginLockedOut(string ip)
        {
            lock (_lock)
            {
                if (!_failures.TryGetValue(ip, out var f)) return false;
                if (_time.GetUtcNow() > f.Last + LoginFailureWindow)
                {
                    _failures.Remove(ip);
                    return false;
                }

                return f.Count >= LoginFailureLimit;
            }
        }

        public void RecordLoginFailure(string ip)
        {
            lock (_lock)
            {
                _failures.TryGetValue(ip, out var f); // default: count 0, Last = MinValue (always expired)
                var now = _time.GetUtcNow();
                byte count = now > f.Last + LoginFailureWindow ? (byte)0 : f.Count;
                _failures[ip] = (unchecked((byte)(count + 1)), now);
            }
        }

        public void ClearLoginFailures(string ip)
        {
            lock (_lock) { _failures.Remove(ip); }
        }

        // get_authenticated_session: constructs the entry if it does not exist.
        public LoginSession GetAuthenticatedSession(string ip, string sessionHash)
        {
            lock (_lock)
            {
                if (!_sessions.TryGetValue(ip, out var byHash)) _sessions[ip] = byHash = new();
                if (!byHash.TryGetValue(sessionHash, out var s)) byHash[sessionHash] = s = new LoginSession { AuthorizedTime = _time.GetUtcNow() };
                return s;
            }
        }

        // Non-constructing lookup (view_session::do_write only touches an existing entry).
        public bool TryGetSession(string ip, string sessionHash, out LoginSession session)
        {
            lock (_lock)
            {
                session = null;
                return _sessions.TryGetValue(ip, out var byHash) && byHash.TryGetValue(sessionHash, out session);
            }
        }

        // getHashFromPacket: the 16 bytes at packet+12 if they name a known session for this ip, else "".
        public string GetHashFromPacket(string ip, ReadOnlySpan<byte> packet)
        {
            if (packet.Length < 28) return "";
            var hash = HashKey(packet.Slice(12, 16).ToArray());
            lock (_lock)
            {
                return _sessions.TryGetValue(ip, out var byHash) && byHash.ContainsKey(hash) ? hash : "";
            }
        }

        // data_session::handle_error: drop the data half; erase the entry (and the ip bucket) once both halves are gone.
        public void OnDataSessionClosed(string ip, string sessionHash)
        {
            if (sessionHash == "") return;
            lock (_lock)
            {
                if (!_sessions.TryGetValue(ip, out var byHash) || !byHash.TryGetValue(sessionHash, out var s)) return;
                s.Data = null;
                if (s.View == null)
                {
                    byHash.Remove(sessionHash);
                    if (byHash.Count == 0) _sessions.Remove(ip);
                }
            }
        }

        public static readonly TimeSpan SessionCleanTime = TimeSpan.FromMinutes(15);

        // ConnectEngine::periodicCleanup: drop sessions that never got a socket (or lost both) after 15 minutes,
        // then any ip bucket left empty.
        public void PeriodicCleanup()
        {
            lock (_lock)
            {
                var now = _time.GetUtcNow();
                foreach (var byHash in _sessions.Values)
                {
                    var stale = new List<string>();
                    foreach (var (hash, s) in byHash)
                        if (s.Data == null && s.View == null && now > s.AuthorizedTime + SessionCleanTime) stale.Add(hash);
                    foreach (var hash in stale) byHash.Remove(hash);
                }

                var empty = new List<string>();
                foreach (var (ip, byHash) in _sessions) if (byHash.Count == 0) empty.Add(ip);
                foreach (var ip in empty) _sessions.Remove(ip);
            }
        }

        // ~ConnectEngine: authenticatedSessions_.clear()
        public void Clear()
        {
            lock (_lock) { _sessions.Clear(); }
        }

        // view_session::handle_error: mirror of OnDataSessionClosed for the lobby view half.
        public void OnViewSessionClosed(string ip, string sessionHash)
        {
            if (sessionHash == "") return;
            lock (_lock)
            {
                if (!_sessions.TryGetValue(ip, out var byHash) || !byHash.TryGetValue(sessionHash, out var s)) return;
                s.View = null;
                if (s.Data == null)
                {
                    byHash.Remove(sessionHash);
                    if (byHash.Count == 0) _sessions.Remove(ip);
                }
            }
        }

        // asStringFromUntrustedSource: raw bytes as a string, one char per byte.
        public static string HashKey(byte[] hash) => Encoding.Latin1.GetString(hash);
    }
}
