// src/profile/data/files.cpp: an account's profile files (accounts_files) and its mailbox (O/m/<name>, in
// accounts_messages). Paths and names are the client's bytes, bound as blobs so they store unchanged.

using System;
using System.Collections.Generic;
using XI.Persistence;

namespace XI.Profile
{
    public sealed class ProfileFiles
    {
        public static readonly byte[] Mailbox = { (byte)'O', (byte)'/', (byte)'m', (byte)'/' };
        public const uint FileSizeLimit = 0x400000;

        private const uint OwnFileLimit = 32;
        private const ulong OwnBytesLimit = 0x800000;
        private const uint MailboxLimit = 100;
        private const uint MailboxSenderLimit = 10;
        private const uint SenderUnreadLimit = 50;
        private const uint MessageSizeLimit = 1024;

        private readonly IDatabase _db;

        public ProfileFiles(IDatabase db) => _db = db;

        public static bool StartsWithMailbox(byte[] path) => path.AsSpan().StartsWith(Mailbox);

        // ownUsage: the account's files and bytes other than the one being written; a failed query counts as full
        private (uint Files, ulong Bytes) OwnUsage(uint accountId, byte[] exceptPath)
        {
            var rset = ProfileDb.Stmt(_db, "SELECT COUNT(*) AS files, COALESCE(SUM(LENGTH(data)), 0) AS bytes FROM accounts_files WHERE accid = ? AND path <> ?",
                                      accountId, exceptPath);
            if (rset == null || !rset.Next()) return (OwnFileLimit, OwnBytesLimit);
            return (rset.Get<uint>("files"), rset.Get<ulong>("bytes"));
        }

        // messageName: O/m/<name> lives in accounts_messages
        private static byte[] MessageName(byte[] path) => StartsWithMailbox(path) ? path.AsSpan(Mailbox.Length).ToArray() : null;

        // read: Ok == false on a DB error; Data null when there is no such file
        public (bool Ok, byte[] Data) Read(uint accountId, byte[] path)
        {
            var name = MessageName(path);
            var rset = name != null
                ? ProfileDb.Stmt(_db, "SELECT data FROM accounts_messages WHERE accid = ? AND name = ?", accountId, name)
                : ProfileDb.Stmt(_db, "SELECT data FROM accounts_files WHERE accid = ? AND path = ?", accountId, path);
            if (rset == null) return (false, null);
            if (!rset.Next()) return (true, null);
            return (true, rset.GetBlobBytes("data"));
        }

        // write: offset 0 starts over, anything else appends
        public bool Write(uint accountId, byte[] path, uint offset, byte[] data)
        {
            // names that don't fit a file list entry
            if (MessageName(path) != null || path.Length >= ProfFileInfo.NameSize || (ulong)offset + (ulong)data.Length > FileSizeLimit) return false;

            var usage = OwnUsage(accountId, path);
            if (usage.Files >= OwnFileLimit || usage.Bytes + offset + (ulong)data.Length > OwnBytesLimit) return false;

            if (offset == 0)
            {
                return ProfileDb.Stmt(_db, "REPLACE INTO accounts_files(accid, path, data) VALUES(?, ?, ?)", accountId, path, data) != null;
            }
            var rset = ProfileDb.Stmt(_db, "UPDATE accounts_files SET data = CONCAT(data, ?) WHERE accid = ? AND path = ? AND LENGTH(data) = ?",
                                      data, accountId, path, offset);
            return rset != null && rset.RowsAffected() == 1;
        }

        // post: false if it exists or a limit is hit
        public bool Post(uint senderAccountId, uint recipientAccountId, byte[] name, MessageType type, byte[] data)
        {
            if (name.Length >= ProfFileInfo.NameSize || data.Length > MessageSizeLimit) return false;

            var mailboxRset = ProfileDb.Stmt(_db, "SELECT COUNT(*) AS messages, COALESCE(SUM(sender_accid = ?), 0) AS sent FROM accounts_messages WHERE accid = ?",
                                             senderAccountId, recipientAccountId);
            if (mailboxRset == null || !mailboxRset.Next() || mailboxRset.Get<uint>("messages") >= MailboxLimit || mailboxRset.Get<uint>("sent") >= MailboxSenderLimit)
            {
                return false;
            }

            // read messages are deleted, so whatever is left is unread
            var unreadRset = ProfileDb.Stmt(_db, "SELECT COUNT(*) AS unread FROM accounts_messages WHERE sender_accid = ?", senderAccountId);
            if (unreadRset == null || !unreadRset.Next() || unreadRset.Get<uint>("unread") >= SenderUnreadLimit) return false;

            var rset = ProfileDb.Stmt(_db, "INSERT IGNORE INTO accounts_messages(accid, name, sender_accid, type, data) VALUES(?, ?, ?, ?, ?)",
                                      recipientAccountId, name, senderAccountId, (byte)type, data);
            return rset != null && rset.RowsAffected() == 1;
        }

        public bool Erase(uint accountId, byte[] path)
        {
            var name = MessageName(path);
            if (name != null) return ProfileDb.Stmt(_db, "DELETE FROM accounts_messages WHERE accid = ? AND name = ?", accountId, name) != null;
            return ProfileDb.Stmt(_db, "DELETE FROM accounts_files WHERE accid = ? AND path = ?", accountId, path) != null;
        }

        // list: oldest first, names relative to the directory; null when the query failed
        public List<byte[]> List(uint accountId, byte[] directory, ushort limit)
        {
            var rset = directory.AsSpan().SequenceEqual(Mailbox)
                ? ProfileDb.Stmt(_db, "SELECT name, LENGTH(data) AS size, UNIX_TIMESTAMP(sent) AS updated FROM accounts_messages " +
                                      "WHERE accid = ? ORDER BY sent, name LIMIT ?",
                                 accountId, (uint)limit)
                : ProfileDb.Stmt(_db, "SELECT SUBSTRING(path, ?) AS name, LENGTH(data) AS size, UNIX_TIMESTAMP(updated) AS updated FROM accounts_files " +
                                      "WHERE accid = ? AND LEFT(path, ?) = ? ORDER BY updated, path LIMIT ?",
                                 (uint)(directory.Length + 1), accountId, (uint)directory.Length, directory, (uint)limit);
            if (rset == null) return null;

            var entries = new List<byte[]>();
            while (rset.Next())
            {
                entries.Add(ProfFileInfo.Make(rset.Get<uint>("updated"), rset.Get<uint>("size"), rset.GetBlobBytes("name")));
            }
            return entries;
        }
    }
}
