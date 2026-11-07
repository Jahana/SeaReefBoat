using System;
using System.Collections.Generic;
using XI.Persistence;
using XI.Crypto;

// XI.Verify.Persistence.Live — the deferred LIVE DB gate for XI.Persistence.
//
// The offline verifier (tests/XI.Verify.Persistence) proves the PURE logic (escape,
// classification, binder lowering, the 0/1-index translation, in-memory fake) against the
// compiled blob oracle. It cannot exercise MySqlRawResultSet + MySqlDatabase.Execute against a
// REAL MariaDB with the engine's 120-table xidb schema — that is this gate's job, and it is the
// one boundary the iron rule explicitly deferred (analogous to Phase-0's live-handshake gate).
//
// Run against the sanctioned container:
//   docker run -d --name xi-live-db -p 3307:3306 -e MARIADB_ROOT_PASSWORD=root \
//     -e MARIADB_DATABASE=xidb mariadb:12.3 ...   (port 3307 because a host mysqld.exe owns 3306)
// then load the engine's sql/*.sql (tools/dbtool.py order: sorted, triggers.sql last, FK off).
//
// Exit codes: 0 = all parity checks passed; 1 = a parity FAIL; 3 = DB not reachable (NOT a
// porting failure — the container/schema is a prerequisite the operator supplies).
internal static partial class Program
{
    private static int _pass;
    private static int _fail;

    // Sentinel keys so the gate never collides with real player data, and cleans up after itself.
    private const uint SentAccid = 990001u;
    private const uint SentCharid = 990002u;
    private const uint TxCharid = 990004u; // char_vars rows for the transaction checks

    // Separate sentinels for the DB->cipher check (its own accounts_sessions row), so it does not
    // collide with the row CheckSessionKeyBlobRoundTrip inserts under SentCharid.
    private const uint SentCipherAccid = 990003u;
    private const uint SentCipherCharid = 990003u;

    private static int Main(string[] argv)
    {
        // [<port>] [--db <name>]: the port (3307) and the database (xidb; a golden lane's copy, tools/golden_lane_db.sh, when
        // the servers run on xidb)
        var args = new List<string>(argv);
        string dbName = "xidb";
        int at = args.IndexOf("--db");
        if (at >= 0)
        {
            if (at + 1 >= args.Count || !System.Text.RegularExpressions.Regex.IsMatch(args[at + 1], "^[A-Za-z0-9_]{1,48}$"))
            {
                Console.WriteLine("--db takes a database name (letters, digits, _)");
                return 1;
            }
            dbName = args[at + 1];
            args.RemoveRange(at, 2);
        }
        int port = args.Count > 0 ? int.Parse(args[0]) : 3307;

        var settings = new NetworkSettings { SqlHost = "127.0.0.1", SqlPort = port, SqlLogin = "root", SqlPassword = "root", SqlDatabase = dbName };

        Console.WriteLine($"live DB gate: {settings.BuildConnectionString()}");

        using var db = new MySqlDatabase(settings);

        // Reachability probe (separate from parity): if the server/schema isn't there, exit 3 so
        // the checklist doesn't read it as a porting regression.
        try
        {
            var probe = db.Execute("SELECT 1", Array.Empty<BoundValue>());
            if (probe == null || !probe.Next())
            {
                Console.WriteLine("DB RESULT: not reachable (SELECT 1 returned no row)");
                Console.WriteLine("RESULT: 0 passed, 0 failed");
                return 3;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DB RESULT: not reachable ({ex.GetType().Name}: {ex.Message})");
            Console.WriteLine("RESULT: 0 passed, 0 failed");
            return 3;
        }

        try
        {
            Cleanup(db); // idempotent start
            CheckInvalidQueryNull(db);
            CheckSelectVersion(db);
            CheckShowTablesCount(db);
            CheckAccountsTypedRoundTrip(db);
            CheckSessionKeyBlobRoundTrip(db);
            CheckLiveDbToCipher(db);
            CheckNullStringSemantics(db);
            CheckTransactionControl(db);
            CheckReconnectRetry(settings);
        }
        catch (Exception ex)
        {
            // A crash mid-parity is itself a FAIL we surface with full context, not a silent pass.
            Bad("unhandled live exception", $"{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            try { Cleanup(db); } catch { /* best-effort teardown */ }
        }

        Console.WriteLine($"RESULT: {_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    // Execute on an Invalid leading keyword returns null (engine returns nullptr), no network touched.
    private static void CheckInvalidQueryNull(IDatabase db)
    {
        CheckEq("db.invalid_keyword_null", db.Execute("VACUUM xidb", Array.Empty<BoundValue>()) == null, true);
        // ValidateContent rejects {} and ';'; both must yield null (nullptr) before hitting the server.
        CheckEq("db.invalid_content_braces_null", db.Execute("SELECT '{}' FROM accounts", Array.Empty<BoundValue>()) == null, true);
        CheckEq("db.invalid_content_semicolon_null", db.Execute("SELECT 1; SELECT 2", Array.Empty<BoundValue>()) == null, true);
    }

    // SELECT VERSION() over the live reader: validates ExecuteReader + RawNext + the 0->1 index
    // translation (ColumnName/Get by 0-based index) against a real result set.
    private static void CheckSelectVersion(IDatabase db)
    {
        try
        {
            var rs = db.Execute("SELECT VERSION()", Array.Empty<BoundValue>());
            Check("select_version.nonnull", rs != null);
            if (rs == null) return;
            CheckEq("select_version.colname0", rs.ColumnName(0), "VERSION()");
            Check("select_version.next", rs.Next());
            var ver = rs.Get<string>(0); // 0-indexed public -> rawColumnLabel(1)
            Check("select_version.looks_like_12x", ver.Length > 0 && ver.StartsWith("12."));
            Console.WriteLine($"    MariaDB VERSION() = {ver}");
        }
        catch (Exception ex)
        {
            Bad("select_version", $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    // SHOW TABLES iteration against the live schema — proves reader streaming, not buffering, works.
    private static void CheckShowTablesCount(IDatabase db)
    {
        try
        {
            var rs = db.Execute("SHOW TABLES", Array.Empty<BoundValue>());
            int n = 0;
            string first = null;
            while (rs != null && rs.Next())
            {
                if (first == null) first = rs.Get<string>(0);
                n++;
            }
            Check("show_tables.count_at_least_119", n >= 119);
            Console.WriteLine($"    SHOW TABLES yielded {n} tables (expected ~120 loaded by dbtool order)");
        }
        catch (Exception ex)
        {
            Bad("show_tables", $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    // Insert one accounts row through the real '?' bind path, then SELECT it back and read every
    // integer width via MySqlRawResultSet — the widths the C++ variant supports (no 64-bit bind).
    private static void CheckAccountsTypedRoundTrip(IDatabase db)
    {
        try
        {
            var ins = db.Execute(
                "INSERT INTO accounts (id, login, password, current_email, registration_email, content_ids, expansions, features, status, priv) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
                new List<BoundValue>
                {
                    Binder.Lower(SentAccid),          // int(10) unsigned -> UInt32 bind
                    Binder.Lower("xigate"),           // varchar
                    Binder.Lower("deadbeef"),         // varchar
                    Binder.Lower("a@b.c"),            // varchar
                    Binder.Lower("a@b.c"),            // varchar
                    Binder.Lower((byte)16),           // content_ids tinyint(2) unsigned -> UInt8
                    Binder.Lower((ushort)4094),       // expansions  smallint(4) unsigned -> UInt16
                    Binder.Lower((byte)253),          // features    tinyint(2) unsigned -> UInt8
                    Binder.Lower((byte)1),            // status      tinyint(3) unsigned -> UInt8
                    Binder.Lower((byte)1),            // priv        tinyint(3) unsigned -> UInt8
                });
            Check("accounts_insert.nonnull", ins != null);
            Check("accounts_insert.rows_affected_ge1", ins != null && ins.RowsAffected() >= 1u);

            var rs = db.Execute("SELECT id, login, content_ids, expansions, features, priv FROM accounts WHERE id = ?", new List<BoundValue> { Binder.Lower(SentAccid) });
            Check("accounts_select.next", rs != null && rs.Next());
            if (rs == null) return;

            // Column labels via the 0-indexed public accessor (-> 1-indexed raw backend).
            CheckEq("accounts.col.name0", rs.ColumnName(0), "id");
            CheckEq("accounts.col.name2", rs.ColumnName(2), "content_ids");

            // Read every width through MySqlRawResultSet's GetFieldValue<T> against the real columns.
            CheckEq("accounts.read.uint_id", rs.Get<uint>("id"), SentAccid);
            CheckEq("accounts.read.string_login", rs.Get<string>("login"), "xigate");
            CheckEq("accounts.read.byte_contentids", rs.Get<byte>("content_ids"), (byte)16);
            CheckEq("accounts.read.ushort_expansions", rs.Get<ushort>("expansions"), (ushort)4094);
            CheckEq("accounts.read.byte_features", rs.Get<byte>("features"), (byte)253);
            CheckEq("accounts.read.byte_priv", rs.Get<byte>("priv"), (byte)1);

            // Same values via the 0-indexed positional accessor must agree with the by-name reads.
            CheckEq("accounts.read.pos0_eq_name_id", rs.Get<uint>(0), rs.Get<uint>("id"));
            CheckEq("accounts.read.pos2_eq_name_contentids", rs.Get<byte>(2), rs.Get<byte>("content_ids"));
        }
        catch (Exception ex)
        {
            Bad("accounts_typed_roundtrip", $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    // binary(20) blob = exactly the shape of the live accounts_sessions.session_key. Prove the
    // blob survives a real bind + a real read byte-for-byte through GetFieldValue<byte[]>.
    private static void CheckSessionKeyBlobRoundTrip(IDatabase db)
    {
        try
        {
            byte[] key = new byte[20];
            for (int i = 0; i < 20; i++) key[i] = (byte)(0xA0 + i);

            var ins = db.Execute(
                "INSERT INTO accounts_sessions (accid, charid, targid, unitychat, linkshellid1, linkshellrank1, linkshellid2, linkshellrank2, session_key, server_addr, server_port, client_addr, client_port, version_mismatch, seacom_type) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
                new List<BoundValue>
                {
                    Binder.Lower(SentAccid),
                    Binder.Lower(SentCharid),
                    Binder.Lower((ushort)7),          // targid smallint unsigned
                    Binder.Lower(0u), Binder.Lower(0u), Binder.Lower((ushort)0),
                    Binder.Lower(0u), Binder.Lower((ushort)0),
                    Binder.Lower(key),                // binary(20) blob
                    Binder.Lower(0x7F000001u),        // server_addr int unsigned (127.0.0.1)
                    Binder.Lower((ushort)54230),      // server_port smallint unsigned
                    Binder.Lower(0u),                 // client_addr
                    Binder.Lower((ushort)0),          // client_port
                    Binder.Lower((byte)0),            // version_mismatch tinyint(1) unsigned
                    Binder.Lower((byte)0),            // seacom_type tinyint(1) unsigned
                });
            Check("session_insert.nonnull", ins != null);
            Check("session_insert.affected_ge1", ins != null && ins.RowsAffected() >= 1u);

            var rs = db.Execute("SELECT charid, session_key, seacom_message FROM accounts_sessions WHERE charid = ?", new List<BoundValue> { Binder.Lower(SentCharid) });
            Check("session_select.next", rs != null && rs.Next());
            if (rs == null) return;

            CheckEq("session.read.uint_charid", rs.Get<uint>("charid"), SentCharid);
            var back = rs.GetBlobBytes("session_key");
            CheckEq("session.blob.length20", back.Length, 20);
            CheckEq("session.blob.bytes_equal", HexEq(back, key), true);
        }
        catch (Exception ex)
        {
            Bad("session_key_blob_roundtrip", $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    // A real NULL column (seacom_message TINYBLOB NULL) must read back as "" for string and report
    // IsNull/GetOrDefault exactly like the C++ value-initialised default — against a live reader.
    private static void CheckNullStringSemantics(IDatabase db)
    {
        try
        {
            var rs = db.Execute("SELECT seacom_message FROM accounts_sessions WHERE charid = ?", new List<BoundValue> { Binder.Lower(SentCharid) });
            Check("null_select.next", rs != null && rs.Next());
            if (rs == null) return;

            Check("null.isnull_true", rs.IsNull("seacom_message"));
            CheckEq("null.get_string_empty", rs.Get<string>("seacom_message"), "");
            CheckEq("null.getordefault_fallback", rs.GetOrDefault<string>("seacom_message", "FB"), "FB");
            CheckEq("null.get_blob_empty", rs.GetBlobBytes("seacom_message").Length, 0);
        }
        catch (Exception ex)
        {
            Bad("null_string_semantics", $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    // Transaction() control flow against a live connection: success path commits, throwing path
    // rolls back and returns false. Validates Begin/Commit/Rollback wire up correctly.
    private static void CheckTransactionControl(IDatabase db)
    {
        try
        {
            bool ok = db.Transaction(() => { /* no-op success */ });
            Check("tx.commit_true", ok);

            bool bad = db.Transaction(() => throw new InvalidOperationException("boom"));
            Check("tx.rollback_false", !bad);

            // the statements inside run on the transaction's connection: a throw undoes them, a nested transaction
            // joins the outer one, and a refused statement (duplicate key) comes back null instead of throwing
            var key = new List<BoundValue> { Binder.Lower(TxCharid) };
            int Rows()
            {
                var r = db.Execute("SELECT varname FROM char_vars WHERE charid = ?", key);
                return r == null ? -1 : (int)r.RowsCount();
            }
            const string insert = "INSERT INTO char_vars(charid, varname, value) VALUES (?, 'tx_a', 1)";
            bool undone = db.Transaction(() =>
            {
                db.Execute(insert, key);
                Check("tx.sees_own_insert", Rows() == 1);
                throw new InvalidOperationException("boom");
            });
            Check("tx.rollback_undoes_insert", !undone && Rows() == 0);

            bool nested = db.Transaction(() =>
            {
                db.Execute(insert, key);
                db.Transaction(() => db.Execute("INSERT INTO char_vars(charid, varname, value) VALUES (?, 'tx_b', 1)", key));
                throw new InvalidOperationException("boom");
            });
            Check("tx.nested_joins_outer", !nested && Rows() == 0);

            Check("tx.commit_keeps_insert", db.Transaction(() => db.Execute(insert, key)) && Rows() == 1);
            Check("refused_statement_is_null", db.Execute(insert, key) == null);
        }
        catch (Exception ex)
        {
            Bad("transaction_control", $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    // The live DB -> map-cipher half of the Phase-0 handshake. map_networking.cpp:346 reads the
    // real accounts_sessions.session_key (binary(20)) blob, memcpy's it into blowfish_t::key via
    // the extractFromBlob C-array branch, then initBlowfish() derives the map Blowfish cipher.
    // We store a known 20-byte key in a REAL MariaDB row, read it back through the now-fixed
    // Execute path, derive the cipher two independent ways (live-DB blob bytes vs a pure in-code
    // ramp of the same bytes), require them to converge AND to frame a packet. No hand-computed
    // expected cipher: the parity is that the DB-sourced derivation matches the pure one.
    private static void CheckLiveDbToCipher(IDatabase db)
    {
        try
        {
            // Same 20 bytes the live round-trip gate inserts (0xA0 + i), on its OWN sentinel row.
            byte[] key = new byte[20];
            for (int i = 0; i < 20; i++) key[i] = (byte)(0xA0 + i);

            var ins = db.Execute(
                "INSERT INTO accounts_sessions (accid, charid, targid, unitychat, linkshellid1, linkshellrank1, linkshellid2, linkshellrank2, session_key, server_addr, server_port, client_addr, client_port, version_mismatch, seacom_type) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
                new List<BoundValue>
                {
                    Binder.Lower(SentCipherAccid),
                    Binder.Lower(SentCipherCharid),
                    Binder.Lower((ushort)7),
                    Binder.Lower(0u), Binder.Lower(0u), Binder.Lower((ushort)0),
                    Binder.Lower(0u), Binder.Lower((ushort)0),
                    Binder.Lower(key),
                    Binder.Lower(0x7F000001u),
                    Binder.Lower((ushort)54230),
                    Binder.Lower(0u),
                    Binder.Lower((ushort)0),
                    Binder.Lower((byte)0),
                    Binder.Lower((byte)0),
                });
            Check("cipher_insert.affected_ge1", ins != null && ins.RowsAffected() >= 1u);

            var rs = db.Execute("SELECT session_key FROM accounts_sessions WHERE charid = ?",
                new List<BoundValue> { Binder.Lower(SentCipherCharid) });
            Check("cipher_select.next", rs != null && rs.Next());
            if (rs == null) return;

            byte[] liveBlob = rs.GetBlobBytes("session_key");
            CheckEq("cipher.liveblob_len20", liveBlob.Length, 20);
            Check("cipher.liveblob_equals_written", HexEq(liveBlob, key));

            // Live-DB blob -> key[5] via the extractFromBlob array branch.
            uint[] key5 = BlowfishKeyBlob.ExtractWords(liveBlob, isNull: false);
            CheckEq("cipher.key5_len5", key5.Length, 5);
            // Independent in-code derivation of the same 5 LE words from the raw bytes.
            uint[] pure5 = new uint[5];
            for (int i = 0; i < 5; i++)
                pure5[i] = (uint)(liveBlob[i * 4] | (liveBlob[i * 4 + 1] << 8) | (liveBlob[i * 4 + 2] << 16) | (liveBlob[i * 4 + 3] << 24));
            for (int i = 0; i < 5; i++)
                CheckEq($"cipher.key5[{i}]_converges", key5[i], pure5[i]);

            // initBlowfish: derive the cipher from the live blob AND from a pure ramp of key[i].
            byte[] hashLive = new byte[16];
            Blowfish bfLive = BlowfishKeyBlob.ExtractToCipher(liveBlob, isNull: false, hashLive);
            uint[] ramp5 = new uint[5];
            for (int i = 0; i < 5; i++) ramp5[i] = (uint)(0xA0 + i * 4) | (uint)((0xA1 + i * 4) << 8) | (uint)((0xA2 + i * 4) << 16) | (uint)((0xA3 + i * 4) << 24);
            byte[] hashPure = new byte[16];
            Blowfish bfPure = SessionCipher.Create(ramp5, hashPure);

            Check("cipher.live_derived_nonnull", bfLive != null && bfLive.P != null && bfLive.P.Length >= 18);
            if (bfLive?.P == null || bfLive.P.Length < 18) return;
            Check("cipher.P0_nonzero", bfLive.P[0] != 0);
            Check("cipher.live_and_pure_P0_agree", bfLive.P[0] == bfPure.P[0]);
            Check("cipher.live_and_pure_P17_agree", bfLive.P[17] == bfPure.P[17]);
            Check("cipher.live_and_pure_hash_agree", HexEq(hashLive, hashPure));
            Console.WriteLine($"    derived map cipher from live DB blob: P0=0x{bfLive.P[0]:X8} P17=0x{bfLive.P[17]:X8}");

            // The derived cipher is usable: encipher then decipher a probe payload under it.
            byte[] probe = { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03, 0x04 };
            byte[] region = PacketFraming.EncipherPayload(probe, bfLive);
            Check("cipher.payload_changed", !HexEq(region, probe));
            bool rt = PacketFraming.TryDecipher(region, bfLive, out byte[] back) && HexEq(back, probe);
            Check("cipher.framing_roundtrip_under_live_db_key", rt);
        }
        catch (Exception ex)
        {
            Bad("live_db_to_cipher", $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void Cleanup(IDatabase db)
    {
        db.Execute("DELETE FROM char_vars WHERE charid = ?", new List<BoundValue> { Binder.Lower(TxCharid) });
        db.Execute("DELETE FROM accounts_sessions WHERE charid = ?", new List<BoundValue> { Binder.Lower(SentCharid) });
        db.Execute("DELETE FROM accounts_sessions WHERE charid = ?", new List<BoundValue> { Binder.Lower(SentCipherCharid) });
        db.Execute("DELETE FROM accounts WHERE id = ?", new List<BoundValue> { Binder.Lower(SentAccid) });
    }

    // ---- tiny assert/hex helpers (dependency-free, matching the offline verifier's style) ----

    private static bool HexEq(byte[] a, byte[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    private static void Check(string name, bool ok)
    {
        if (ok) { _pass++; }
        else { _fail++; Console.WriteLine($"FAIL: {name}"); }
    }

    private static void CheckEq<T>(string name, T actual, T expected)
    {
        var ok = EqualityComparer<T>.Default.Equals(actual, expected);
        Check(name + (ok ? "" : $" (got '{actual}', want '{expected}')"), ok);
    }

    private static void Bad(string name, string detail)
    {
        _fail++;
        Console.WriteLine($"FAIL: {name} -> {detail}");
    }
}
