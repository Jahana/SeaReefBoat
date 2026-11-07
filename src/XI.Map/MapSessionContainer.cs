// Port of src/map/map_session_container.h/.cpp: live sessions keyed by client IPP, pending sessions keyed by charid.
//
// MapEngine runs cleanupSessions on the main thread every kSessionCleanupInterval (5 s): CleanupSessionsIfDue.
// Its character steps belong to the entity layer (ZoneHost.OnLinkDead / OnCharTimeout).
// At each cleanup a session with a Char is handed to OnLinkDead: stale after 5 s without an update (the char goes
// link dead), fresh otherwise (the flag is cleared again).
// At the timeout it is handed to OnCharTimeout (persist::flush, removeCharFromZone, the pet's despawn).
// The session DB statements go through IMapSessionStore.

using System;
using System.Collections.Generic;
using System.Linq;

namespace XI.Map
{
    public interface IMapSessionStore
    {
        // SELECT c.accid, s.session_key FROM chars c LEFT JOIN accounts_sessions s ... WHERE c.charid = ?
        // false: no such char. key5 null: session_key is NULL.
        bool TryLoadLogin(uint charId, out uint accountId, out uint[] key5);

        // SELECT server_addr, server_port FROM accounts_sessions WHERE charid = ?
        bool TryGetSessionServer(uint charId, out uint serverAddr, out uint serverPort);

        // DELETE FROM accounts_sessions WHERE charid = ?
        void DeleteSession(uint charId);

        // UPDATE accounts_sessions SET session_key = ? WHERE charid = ? LIMIT 1
        void UpdateSessionKey(uint charId, uint[] key5);

        // UPDATE accounts_sessions SET server_addr/server_port/client_port = 0/last_zoneout_time (0x00B sent)
        void MarkZoneOut(uint charId, LogoutState state, ulong zoneIpp);

        // C# only (network.ZONE_IP_FROM_CLIENT): SELECT client_server_addr FROM accounts_sessions WHERE charid = ?;
        // 0 when there is no row or no address
        uint GetClientServerAddr(uint charId) => 0;
    }

    public sealed class MapSessionContainer
    {
        private readonly Dictionary<ulong, MapSession> _sessions = new Dictionary<ulong, MapSession>();
        private readonly Dictionary<uint, MapSession> _pending = new Dictionary<uint, MapSession>();
        private readonly IMapSessionStore _store;
        private readonly Func<DateTime> _now;
        private readonly Action<string> _log;

        public MapSessionContainer(IMapSessionStore store, Func<DateTime> now = null, Action<string> log = null)
        {
            _store = store;
            _now = now ?? (() => DateTime.UtcNow);
            _log = log ?? (_ => { });
            _nextCleanup = _now() + CleanupInterval;
        }

        // kSessionCleanupInterval: the main thread's interval for cleanupSessions
        public static readonly TimeSpan CleanupInterval = TimeSpan.FromSeconds(5);
        private DateTime _nextCleanup;

        // the cleanup on its 5 s interval, from the main loop: run (true) once the interval has passed since the last
        // one, else nothing
        public bool CleanupSessionsIfDue(uint mapIp, ushort mapPort, int timeoutSeconds)
        {
            var now = _now();
            if (now < _nextCleanup) return false;
            _nextCleanup = now + CleanupInterval;
            CleanupSessions(mapIp, mapPort, timeoutSeconds);
            return true;
        }

        public IReadOnlyCollection<MapSession> Sessions => _sessions.Values;

        public IReadOnlyCollection<MapSession> PendingSessions => _pending.Values;

        public DateTime Now => _now();

        // Character-bearing session timed out; the entity layer persists/removes the char. Return value is ignored.
        public Action<MapSession, bool> OnCharTimeout;

        // Character-bearing session at each cleanup: true when its last update is more than 5 s old (the char goes
        // link dead, once), false otherwise (a link-dead char comes back)
        public Action<MapSession, bool> OnLinkDead;

        public MapSession CreateSession(ulong ipp)
        {
            _log($"Creating session for {MapIpp.ToString(ipp)}");
            var s = new MapSession { ClientIpp = ipp };
            s.TapLastUpdate(_now());
            _sessions[ipp] = s;
            return s;
        }

        public MapSession CreatePendingSession(uint charId)
        {
            _log($"Creating pending session for character id {charId}");
            var s = new MapSession { CharId = charId };
            s.TapLastUpdate(_now());
            _pending[charId] = s;
            return s;
        }

        public void MoveSession(MapSession s, ulong ipp)
        {
            if (!_sessions.Remove(s.ClientIpp)) return;
            s.ClientIpp = ipp;
            _sessions[ipp] = s;
        }

        public MapSession GetSessionByIpp(ulong ipp) => _sessions.TryGetValue(ipp, out var s) ? s : null;

        public MapSession GetSessionByCharId(uint charId) => _sessions.Values.FirstOrDefault(s => s.CharId == charId);

        public MapSession GetPendingSessionByCharId(uint charId) => _pending.TryGetValue(charId, out var s) ? s : null;

        public MapSession GetSessionByAccountId(uint accountId) => _sessions.Values.FirstOrDefault(s => s.AccountId == accountId);

        // mapIp/mapPort: this process's IPP; 0 means "not set explicitly" exactly as in C++.
        public void CleanupSessions(uint mapIp, ushort mapPort, int timeoutSeconds)
        {
            var now = _now();
            foreach (var s in _sessions.Values.ToList())
            {
                if (now <= s.LastUpdate.AddSeconds(5))
                {
                    if (s.Char != null) OnLinkDead?.Invoke(s, false);
                    continue;
                }
                if (s.Char != null) OnLinkDead?.Invoke(s, true);
                if (now <= s.LastUpdate.AddSeconds(timeoutSeconds)) continue;

                bool otherMap = false;
                if (_store.TryGetSessionServer(s.CharId, out uint addr, out uint port))
                    otherMap = (mapIp != 0 && addr != mapIp) || (mapPort != 0 && port != mapPort);

                if (s.Char != null)
                {
                    OnCharTimeout?.Invoke(s, otherMap);
                    if (!otherMap) _store.DeleteSession(s.CharId);
                }
                else
                {
                    _log($"map_cleanup: session {MapIpp.ToString(s.ClientIpp)} (charid {s.CharId}) timed out without a character, closing");
                    if (!otherMap) _store.DeleteSession(s.CharId);
                }
                s.Char = null;
                _sessions.Remove(s.ClientIpp);
            }

            foreach (var s in _pending.Values.ToList())
            {
                if (now <= s.LastUpdate.AddSeconds(timeoutSeconds)) continue;
                _log($"map_cleanup: pending session for charid {s.CharId} expired, client did not connect");
                _store.DeleteSession(s.CharId);
                _pending.Remove(s.CharId);
            }
        }

        public void DestroySession(ulong ipp)
        {
            var s = GetSessionByIpp(ipp);
            if (s != null) DestroySession(s);
        }

        public void DestroySession(MapSession s)
        {
            if (s == null) return;
            _log($"Closing session for {MapIpp.ToString(s.ClientIpp)}");

            // clear accounts_sessions if character is logging out (not when zoning)
            if (s.ShuttingDown == 1) _store.DeleteSession(s.CharId);

            s.Char = null;
            s.InZone = false;
            _sessions.Remove(s.ClientIpp);
        }

        public void DestroyPendingSession(MapSession s)
        {
            if (s == null) return;
            _log($"Closing pending session for character id {s.CharId}");
            _pending.Remove(s.CharId);
        }

        public void DestroyPendingSession(uint charId) => DestroyPendingSession(GetPendingSessionByCharId(charId));

        public void Shutdown()
        {
            foreach (var s in _sessions.Values.ToList()) DestroySession(s);
            foreach (var s in _pending.Values) s.Char = null;
            _pending.Clear();
        }
    }
}
