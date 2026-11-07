// src/profile/data/friends.cpp: friend and block lists (accounts_friends), and who sees whose presence.

using System;
using System.Collections.Generic;
using System.Linq;
using XI.Persistence;

namespace XI.Profile
{
    public sealed class ProfileFriends
    {
        public const byte FriendListSize = StoreFriendListHead.FriendOrderSize;
        public const byte BlockListSize = StoreFriendListHead.BlackOrderSize;

        // visibleFriends entry: both list each other, neither pending, neither blocked
        public sealed class Friend
        {
            public uint AccountId;
            public byte ListIndex;  // where this account lists the friend
            public byte TheirIndex; // where the friend lists this account
            public uint? Playing;
        }

        public sealed class StoreResult
        {
            public readonly List<uint> Accepted = new();
            public readonly List<uint> Lost = new();
        }

        private readonly IDatabase _db;
        private readonly ProfileAccounts _accounts;

        public ProfileFriends(IDatabase db, ProfileAccounts accounts)
        {
            _db = db;
            _accounts = accounts;
        }

        // run: throws so the transaction rolls back
        private ResultSet Run(string query, params object[] args)
        {
            var rset = ProfileDb.Stmt(_db, query, args);
            if (rset == null) throw new InvalidOperationException("friend list statement failed");
            return rset;
        }

        private bool? PendingEntry(uint accountId, uint otherAccountId)
        {
            var rset = Run("SELECT pending FROM accounts_friends WHERE accid = ? AND blacklist = 0 AND friend_accid = ?", accountId, otherAccountId);
            if (!rset.Next()) return null;
            return rset.Get<bool>("pending");
        }

        private bool IsPending(uint accountId, uint friendAccountId) => PendingEntry(accountId, friendAccountId) ?? true;

        private bool Apply(uint accountId, FriendInfo entry)
        {
            bool isBlack = entry.IsBlack != 0;
            // one bad index makes the client reject the whole list
            byte listSize = isBlack ? BlockListSize : FriendListSize;
            if (entry.Num >= listSize) return false;

            if (entry.Op == FriendInfo.OpDelete)
            {
                Run("DELETE FROM accounts_friends WHERE accid = ? AND blacklist = ? AND list_index = ?", accountId, isBlack, entry.Num);
                return true;
            }

            // POL ids and handle ids are account ids
            uint friendAccountId = (uint)entry.PolId;
            if (entry.Op != FriendInfo.OpSet || entry.PolId != friendAccountId || friendAccountId == accountId || !_accounts.Exists(friendAccountId)) return false;

            bool pending = !isBlack && IsPending(accountId, friendAccountId);
            entry.HandleId = friendAccountId;
            entry.HandleNumber = 0;
            entry.BTemporary = (byte)(pending ? 1 : 0);
            // also moves the friend if listed at another index
            Run("REPLACE INTO accounts_friends(accid, blacklist, list_index, friend_accid, display_order, name, pending) VALUES(?, ?, ?, ?, ?, ?, ?)",
                accountId, isBlack, entry.Num, friendAccountId, entry.Order, ProfileCodec.CString(entry.HandleName), pending);
            return true;
        }

        private void ApplyOrder(uint accountId, StoreFriendListHead head)
        {
            var rset = Run("SELECT blacklist, list_index, display_order FROM accounts_friends WHERE accid = ?", accountId);
            while (rset.Next())
            {
                bool isBlack = rset.Get<bool>("blacklist");
                byte listIndex = rset.Get<byte>("list_index");
                byte order = isBlack ? head.BlackOrder(listIndex) : head.FriendOrder(listIndex);
                if (rset.Get<byte>("display_order") == order) continue;
                Run("UPDATE accounts_friends SET display_order = ? WHERE accid = ? AND blacklist = ? AND list_index = ?", order, accountId, isBlack, listIndex);
            }
        }

        // visibleIds: throws so the transaction rolls back
        private SortedSet<uint> VisibleIds(uint accountId)
        {
            var visible = VisibleFriends(accountId);
            if (visible == null) throw new InvalidOperationException("friend visibility query failed");
            return new SortedSet<uint>(visible.Select(f => f.AccountId));
        }

        // load: null when the query failed
        public List<FriendInfo> Load(uint accountId)
        {
            var rset = ProfileDb.Stmt(_db, "SELECT blacklist, list_index, friend_accid, display_order, name, pending FROM accounts_friends WHERE accid = ? ORDER BY blacklist, list_index",
                                      accountId);
            if (rset == null) return null;

            var entries = new List<FriendInfo>();
            while (rset.Next())
            {
                uint friendAccountId = rset.Get<uint>("friend_accid");
                // same as retail: kind 1, Group 0xE, no characters
                var entry = new FriendInfo
                {
                    Op = FriendInfo.OpSet,
                    IsBlack = (byte)(rset.Get<bool>("blacklist") ? 1 : 0),
                    Kind = 1,
                    HandleId = friendAccountId,
                    Group = 0xE,
                    BTemporary = (byte)(rset.Get<bool>("pending") ? 1 : 0),
                    Num = rset.Get<byte>("list_index"),
                    Order = rset.Get<byte>("display_order"),
                    PolId = friendAccountId,
                };
                ProfileCodec.PutName(entry.HandleName, rset.GetBlobBytes("name"));
                entries.Add(entry);
            }
            return entries;
        }

        // store: sets each entry's result, 0 = applied; null when the transaction did not commit
        public StoreResult Store(uint accountId, StoreFriendListHead head, List<FriendInfo> entries)
        {
            SortedSet<uint> before = new(), after = new();
            bool committed = _db.Transaction(() =>
            {
                before = VisibleIds(accountId);
                foreach (var entry in entries)
                {
                    entry.Result = 1;
                    if (Apply(accountId, entry)) entry.Result = 0;
                }
                ApplyOrder(accountId, head);
                after = VisibleIds(accountId);
            });
            if (!committed) return null;

            var result = new StoreResult();
            result.Accepted.AddRange(after.Where(id => !before.Contains(id)));
            result.Lost.AddRange(before.Where(id => !after.Contains(id)));
            return result;
        }

        public bool? Blocks(uint accountId, uint otherAccountId)
        {
            var rset = ProfileDb.Stmt(_db, "SELECT list_index FROM accounts_friends WHERE accid = ? AND blacklist = 1 AND friend_accid = ?", accountId, otherAccountId);
            if (rset == null) return null;
            return rset.Next();
        }

        // accept: throws on failure, call inside a transaction
        public bool Accept(uint accountId, uint otherAccountId)
        {
            var rset = Run("UPDATE accounts_friends SET pending = 0 " +
                           "WHERE blacklist = 0 AND pending = 1 " +
                           "AND ((accid = ? AND friend_accid = ?) OR (accid = ? AND friend_accid = ?))",
                           accountId, otherAccountId, otherAccountId, accountId);
            return rset.RowsAffected() > 0;
        }

        // dropPending: throws on failure, call inside a transaction
        public void DropPending(uint accountId, uint otherAccountId)
        {
            Run("DELETE FROM accounts_friends WHERE accid = ? AND blacklist = 0 AND friend_accid = ? AND pending = 1", accountId, otherAccountId);
        }

        // visibleFriends: null when the query failed
        public List<Friend> VisibleFriends(uint accountId)
        {
            var rset = ProfileDb.Stmt(_db, "SELECT mine.friend_accid, mine.list_index AS my_index, theirs.list_index AS their_index, sessions.charid AS playing " +
                                           "FROM accounts_friends mine " +
                                           "JOIN accounts_friends theirs ON theirs.accid = mine.friend_accid AND theirs.blacklist = 0 AND theirs.friend_accid = mine.accid AND theirs.pending = 0 " +
                                           "LEFT JOIN accounts_sessions sessions ON sessions.accid = mine.friend_accid " +
                                           "WHERE mine.accid = ? AND mine.blacklist = 0 AND mine.pending = 0 " +
                                           "AND NOT EXISTS (SELECT 1 FROM accounts_friends blocked WHERE blocked.accid = mine.accid AND blocked.blacklist = 1 AND blocked.friend_accid = mine.friend_accid) " +
                                           "AND NOT EXISTS (SELECT 1 FROM accounts_friends blocked WHERE blocked.accid = mine.friend_accid AND blocked.blacklist = 1 AND blocked.friend_accid = mine.accid)",
                                      accountId);
            if (rset == null) return null;

            var result = new List<Friend>();
            while (rset.Next())
            {
                var visible = new Friend
                {
                    AccountId = rset.Get<uint>("friend_accid"),
                    ListIndex = rset.Get<byte>("my_index"),
                    TheirIndex = rset.Get<byte>("their_index"),
                };
                if (!rset.IsNull("playing")) visible.Playing = rset.Get<uint>("playing");
                result.Add(visible);
            }
            return result;
        }
    }
}
