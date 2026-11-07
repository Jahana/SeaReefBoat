// src/profile/data/accounts.cpp: the profile server's account queries (credentials, open status, characters).

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using XI.Persistence;

namespace XI.Profile
{
    // db::preparedStmt over IDatabase: lowers C# scalars to BoundValue
    internal static class ProfileDb
    {
        public static ResultSet Stmt(IDatabase db, string query, params object[] args)
        {
            var bound = new BoundValue[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                bound[i] = args[i] switch
                {
                    uint v => BoundValue.Of(v),
                    int v => BoundValue.Of(v),
                    ushort v => BoundValue.Of(v),
                    byte v => BoundValue.Of(v),
                    bool v => BoundValue.Of(v),
                    string v => BoundValue.Of(v),
                    byte[] v => BoundValue.Of(v),
                    _ => throw new NotSupportedException($"unbindable argument type {args[i]?.GetType()}"),
                };
            }
            return db.Execute(query, bound);
        }
    }

    public struct Credential
    {
        public uint AccountId;
        public byte[] SessionHash; // 16 bytes
    }

    public sealed class ProfileAccounts
    {
        public sealed class Character
        {
            public uint Id;
            public string Name;
        }

        public sealed class Owner
        {
            public uint AccountId;
            public string CharacterName;
        }

        private readonly IDatabase _db;

        public ProfileAccounts(IDatabase db) => _db = db;

        // freshness: nothing if the hash isn't the account's current one, else whether it was refreshed in the last two
        // hours; banned accounts lose profile access too
        public bool? Freshness(Credential credential)
        {
            var rset = ProfileDb.Stmt(_db, "SELECT sessions.refreshed > NOW() - INTERVAL 2 HOUR AS fresh FROM accounts_profile sessions " +
                                           "JOIN accounts ON accounts.id = sessions.accid " +
                                           "WHERE sessions.accid = ? AND sessions.session_hash = ? AND accounts.status & 1",
                                      credential.AccountId, credential.SessionHash);
            if (rset == null || !rset.Next()) return null;
            return rset.Get<bool>("fresh");
        }

        // refresh: no-op if a newer login replaced the hash
        public void Refresh(uint accountId, byte[] sessionHash)
        {
            ProfileDb.Stmt(_db, "UPDATE accounts_profile SET refreshed = NOW() WHERE accid = ? AND session_hash = ?", accountId, sessionHash);
        }

        // udpPortSlot: 0 if the session is gone
        public ushort UdpPortSlot(Credential credential)
        {
            var rset = ProfileDb.Stmt(_db, "SELECT udp_port_slot FROM accounts_profile WHERE accid = ? AND session_hash = ?", credential.AccountId, credential.SessionHash);
            if (rset != null && rset.RowsCount() > 0 && rset.Next()) return rset.Get<ushort>("udp_port_slot");
            return 0;
        }

        // keyValue: md5 of the session hash, the lobby derives the same bytes into accounts_sessions.session_key at
        // character select; zeros if the session is gone
        public byte[] KeyValue(uint accountId)
        {
            var value = new byte[16];
            var rset = ProfileDb.Stmt(_db, "SELECT session_hash FROM accounts_profile WHERE accid = ?", accountId);
            if (rset != null && rset.RowsCount() > 0 && rset.Next())
            {
                var hash = new byte[16];
                var stored = rset.GetBlobBytes("session_hash");
                stored.AsSpan(0, Math.Min(stored.Length, 16)).CopyTo(hash);
                value = MD5.HashData(hash);
            }
            return value;
        }

        public bool Exists(uint accountId)
        {
            var rset = ProfileDb.Stmt(_db, "SELECT id FROM accounts WHERE id = ?", accountId);
            return rset != null && rset.Next();
        }

        // openStatus: online if never set
        public OpenStatus? GetOpenStatus(uint accountId)
        {
            var rset = ProfileDb.Stmt(_db, "SELECT open_status FROM accounts_profile WHERE accid = ?", accountId);
            if (rset == null) return null;
            if (!rset.Next()) return OpenStatus.Online;
            var stored = (OpenStatus)rset.Get<byte>("open_status");
            return stored switch
            {
                OpenStatus.Online or OpenStatus.Away or OpenStatus.Invisible => stored,
                _ => OpenStatus.Online,
            };
        }

        public bool SetOpenStatus(uint accountId, OpenStatus status) =>
            ProfileDb.Stmt(_db, "UPDATE accounts_profile SET open_status = ? WHERE accid = ?", (byte)status, accountId) != null;

        // characters: null when the query failed
        public List<Character> Characters(uint accountId)
        {
            var rset = ProfileDb.Stmt(_db, "SELECT charid, charname FROM chars WHERE accid = ? ORDER BY charid", accountId);
            if (rset == null) return null;
            var result = new List<Character>();
            while (rset.Next()) result.Add(new Character { Id = rset.Get<uint>("charid"), Name = rset.Get<string>("charname") });
            return result;
        }

        public uint? Playing(uint accountId)
        {
            var rset = ProfileDb.Stmt(_db, "SELECT charid FROM accounts_sessions WHERE accid = ?", accountId);
            if (rset == null || !rset.Next()) return null;
            return rset.Get<uint>("charid");
        }

        // characterOwner: in-game characters only
        public Owner CharacterOwner(uint characterId)
        {
            var rset = ProfileDb.Stmt(_db, "SELECT chars.accid, chars.charname FROM chars " +
                                           "JOIN accounts_sessions sessions ON sessions.charid = chars.charid " +
                                           "WHERE chars.charid = ?",
                                      characterId);
            if (rset == null || !rset.Next()) return null;
            return new Owner { AccountId = rset.Get<uint>("accid"), CharacterName = rset.Get<string>("charname") };
        }

        // a character name's bytes as the C++ std::string holds them
        public static byte[] NameBytes(string name) => Encoding.UTF8.GetBytes(name ?? string.Empty);
    }
}
