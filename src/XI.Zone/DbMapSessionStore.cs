using System;
using System.Collections.Generic;
using XI.Map;
using XI.Persistence;

namespace XI.Zone
{
    // IMapSessionStore over the accounts_sessions / chars tables, with the statements map_networking.cpp and
    // map_session_container.cpp run.
    public sealed class DbMapSessionStore : IMapSessionStore
    {
        private readonly IDatabase db_;

        public DbMapSessionStore(IDatabase db) { db_ = db; }

        private ResultSet Exec(string sql, params BoundValue[] args) => db_.Execute(sql, args);

        // recv_parse's read of the char's account and session key (LoginReads lists it)
        internal const string SqlLogin = "SELECT c.accid, s.session_key FROM chars c LEFT JOIN accounts_sessions s ON s.charid = c.charid WHERE c.charid = ? LIMIT 1";

        public bool TryLoadLogin(uint charId, out uint accountId, out uint[] key5)
        {
            accountId = 0;
            key5 = null;
            var r = Exec(SqlLogin, BoundValue.Of(charId));
            if (r == null || !r.Next()) return false;
            accountId = r.Get<uint>("accid");
            if (r.IsNull("session_key")) return true;

            // db::extractFromBlob into uint32 key[5]
            var raw = new byte[20];
            var blob = r.GetBlobBytes("session_key");
            Array.Copy(blob, raw, Math.Min(blob.Length, raw.Length));
            key5 = new uint[5];
            for (int i = 0; i < 5; i++) key5[i] = BitConverter.ToUInt32(raw, 4 * i);
            return true;
        }

        public bool TryGetSessionServer(uint charId, out uint serverAddr, out uint serverPort)
        {
            serverAddr = serverPort = 0;
            var r = Exec("SELECT server_addr, server_port FROM accounts_sessions WHERE charid = ?", BoundValue.Of(charId));
            if (r == null || !r.Next()) return false;
            serverAddr = r.Get<uint>("server_addr");
            serverPort = r.Get<uint>("server_port");
            return true;
        }

        public uint GetClientServerAddr(uint charId)
        {
            var r = Exec("SELECT client_server_addr FROM accounts_sessions WHERE charid = ?", BoundValue.Of(charId));
            return r != null && r.Next() ? r.Get<uint>("client_server_addr") : 0;
        }

        public void DeleteSession(uint charId) => Exec("DELETE FROM accounts_sessions WHERE charid = ?", BoundValue.Of(charId));

        public void UpdateSessionKey(uint charId, uint[] key5)
        {
            var raw = new byte[20];
            for (int i = 0; i < 5; i++) BitConverter.TryWriteBytes(raw.AsSpan(4 * i), key5[i]);
            Exec("UPDATE accounts_sessions SET session_key = ? WHERE charid = ? LIMIT 1", BoundValue.Of(raw), BoundValue.Of(charId));
        }

        public void MarkZoneOut(uint charId, LogoutState state, ulong zoneIpp)
        {
            if (state != LogoutState.Logout)
            {
                // the client no longer has a port until its next 0x00A
                Exec("UPDATE accounts_sessions SET server_addr = ?, server_port = ?, client_port = 0, last_zoneout_time = NOW() WHERE charid = ?",
                     BoundValue.Of(MapIpp.Ip(zoneIpp)), BoundValue.Of(MapIpp.Port(zoneIpp)), BoundValue.Of(charId));
            }
            else
            {
                Exec("UPDATE accounts_sessions SET client_port = 0, last_zoneout_time = NOW() WHERE charid = ?", BoundValue.Of(charId));
            }
        }
    }
}
