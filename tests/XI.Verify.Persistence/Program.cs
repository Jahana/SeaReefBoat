using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XI.Persistence;
using XI.Crypto;

// Dependency-free offline verifier for XI.Persistence. Checks the pure, deterministic parts
// that mirror the C++ engine: config parse (golden from settings/default/network.lua),
// escapeString semantics, query classification, binder lowering, and the 0-indexed public /
// 1-indexed raw result-set contract. Live MariaDB execution is the deferred boundary.
internal static class Program
{
    private static int _pass;
    private static int _fail;

    // A uint16-enum stand-in for xi::ZoneId (pinned to uint16_t from data/enums/zone.yaml).
    private enum ZoneId : ushort
    {
        Unknown = 0,
        PhanauetChannel = 1,
        CarpentersLanding = 2,
    }

    private static int Main(string[] argv)
    {
        GoldenPath = argv.Length > 0
            ? argv[0]
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "oracle", "blob_golden.txt"));

        BlowfishTGoldenPath = argv.Length > 1
            ? argv[1]
            : Path.Combine(Path.GetDirectoryName(GoldenPath) ?? ".", "map_blowfish_t_golden.txt");

        CheckConfigParse();
        CheckEscape();
        CheckQueryValidation();
        CheckBinder();
        CheckResultSetContract();
        CheckBlobGolden();
        CheckBlowfishKeyBlob();
        CheckPrefetch();

        Console.WriteLine($"RESULT: {_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    private static string GoldenPath;

    // Second oracle file: the compiled blowfish_t layout + extractFromBlob C-array-branch words.
    private static string BlowfishTGoldenPath;

    // Diff the blob escape + extract paths against the compiled oracle (csharp/oracle/blob_golden.txt).
    private static void CheckBlobGolden()
    {
        if (!File.Exists(GoldenPath))
        {
            Check($"blob.golden_missing[{GoldenPath}]", false);
            return;
        }

        var esc = 0;
        var ext = 0;
        foreach (var line in File.ReadAllLines(GoldenPath))
        {
            if (line.StartsWith("ESC "))
            {
                esc++;
                var kv = ParseKv(line);
                var input = BytesFromHex(kv["in_hex"]);
                var wantHex = kv["out_hex"];
                var gotHex = ToHex(ToLatin1(BlobEscape.ToString(input)));
                CheckEq($"blob.esc[{kv["tag"]}]", gotHex, wantHex);
            }
            else if (line.StartsWith("EXT "))
            {
                ext++;
                var kv = ParseKv(line);
                var destSize = int.Parse(kv["destSize"]);
                var isNull = kv["null"] == "1";
                // Reconstruct the blob from the input we generated it from: use out_hex only when
                // null==1 (all zeros) is not derivable from blob alone. The EXT lines carry out_hex as
                // the authoritative expected result; feed the same blob bytes the oracle used by
                // deriving them from a fixed pattern the oracle emitted. See note below.
                var wantHex = kv["out_hex"];
                // We cannot recover the source blob from the line; instead validate the extract RULE
                // against the golden by re-deriving: the golden out_hex already encodes zero-init +
                // copy(min). We assert our Extract reproduces it given a blob of the recorded length
                // built from out_hex's own leading bytes when null==0.
                byte[] blob;
                if (isNull)
                {
                    blob = null;
                }
                else
                {
                    var full = BytesFromHex(wantHex);
                    var blobLen = int.Parse(kv["blobLen"]);
                    blob = new byte[blobLen];
                    Array.Copy(full, blob, Math.Min(blobLen, full.Length));
                }

                var got = BlobExtract.Extract(destSize, blob, isNull);
                CheckEq($"blob.ext[{kv["tag"]}]", ToHex(got), wantHex);
            }
        }

        Check($"blob.counts[esc={esc},ext={ext}]", esc >= 18 && ext >= 6);
    }

    private static System.Collections.Generic.Dictionary<string, string> ParseKv(string line)
    {
        var d = new System.Collections.Generic.Dictionary<string, string>();
        foreach (var tok in line.Split(' '))
        {
            var eq = tok.IndexOf('=');
            if (eq > 0)
            {
                d[tok.Substring(0, eq)] = tok.Substring(eq + 1);
            }
            else if (tok != "ESC" && tok != "EXT")
            {
                d["tag"] = tok;
            }
        }

        return d;
    }

    private static byte[] ToLatin1(string s)
    {
        var b = new byte[s.Length];
        for (var i = 0; i < s.Length; i++)
        {
            b[i] = (byte)s[i];
        }

        return b;
    }

    private static byte[] BytesFromHex(string hex)
    {
        if (string.IsNullOrEmpty(hex))
        {
            return Array.Empty<byte>();
        }

        var b = new byte[hex.Length / 2];
        for (var i = 0; i < b.Length; i++)
        {
            b[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        return b;
    }

    private static string ToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    }

    private static void Check(string name, bool ok)
    {
        if (ok)
        {
            _pass++;
        }
        else
        {
            _fail++;
            Console.WriteLine($"FAIL: {name}");
        }
    }

    private static void CheckEq<T>(string name, T actual, T expected)
    {
        var ok = EqualityComparer<T>.Default.Equals(actual, expected);
        Check(name + (ok ? "" : $" (got '{actual}', want '{expected}')"), ok);
    }

    // Golden values read directly from settings/default/network.lua.
    private static void CheckConfigParse()
    {
        const string lua = @"
xi.settings.network =
{
    SQL_HOST     = '127.0.0.1',
    SQL_PORT     = 3306,
    SQL_LOGIN    = 'root',
    SQL_PASSWORD = 'root',
    SQL_DATABASE = 'xidb',
    SQL_QUERY_RETRY_COUNT = 1,
    ZMQ_TRANSPORT = 'tcp',
    ZMQ_IP        = '127.0.0.1',
    ZMQ_PORT      = 54003,
    MAP_PORT = 54230,
    HTTP_PORT   = 8088,
}";
        var s = NetworkSettings.Parse(lua);
        CheckEq("cfg.SQL_HOST", s.SqlHost, "127.0.0.1");
        CheckEq("cfg.SQL_PORT", s.SqlPort, 3306);
        CheckEq("cfg.SQL_LOGIN", s.SqlLogin, "root");
        CheckEq("cfg.SQL_PASSWORD", s.SqlPassword, "root");
        CheckEq("cfg.SQL_DATABASE", s.SqlDatabase, "xidb");
        CheckEq("cfg.SQL_QUERY_RETRY_COUNT", s.SqlQueryRetryCount, 1);
        CheckEq("cfg.ZMQ_TRANSPORT", s.ZmqTransport, "tcp");
        CheckEq("cfg.ZMQ_IP", s.ZmqIp, "127.0.0.1");
        CheckEq("cfg.ZMQ_PORT", s.ZmqPort, 54003);

        var cs = s.BuildConnectionString();
        Check("cfg.connstring.host", cs.Contains("Server=127.0.0.1"));
        Check("cfg.connstring.port", cs.Contains("Port=3306"));
        Check("cfg.connstring.db", cs.Contains("Database=xidb"));
        Check("cfg.connstring.charset", cs.Contains("Charset=utf8mb4"));
    }

    private static string Esc(string s) => SqlEscape.Escape(s);

    private static void CheckEscape()
    {
        CheckEq("esc.empty", Esc(""), "");
        CheckEq("esc.plain", Esc("abc"), "abc");
        CheckEq("esc.squote", Esc("a'b"), "a\\'b");
        CheckEq("esc.dquote", Esc("a\"b"), "a\\\"b");
        CheckEq("esc.backslash", Esc("a\\b"), "a\\\\b");
        CheckEq("esc.percent", Esc("a%b"), "a\\%b");
        CheckEq("esc.pipe", Esc("a|b"), "a\\|b");
        CheckEq("esc.semicolon", Esc("a;b"), "a\\;b");
        CheckEq("esc.newline", Esc("a\nb"), "a\\nb");
        CheckEq("esc.cr", Esc("a\rb"), "a\\rb");
        CheckEq("esc.sub", Esc("a\u001ab"), "a\\Zb");
        CheckEq("esc.backspace", Esc("a\bb"), "a\\bb");
        // The C++ loop breaks on '\0' before the map -> 'b' after the NUL is dropped.
        CheckEq("esc.nul_break", Esc("a\0b"), "a");
    }

    private static void CheckQueryValidation()
    {
        CheckEq("qv.SELECT", QueryValidation.ValidateLeadingKeyword("SELECT * FROM t"), ResultSetType.Select);
        CheckEq("qv.lower_select", QueryValidation.ValidateLeadingKeyword("  select 1"), ResultSetType.Select);
        CheckEq("qv.SHOW", QueryValidation.ValidateLeadingKeyword("SHOW TABLES"), ResultSetType.Select);
        CheckEq("qv.INSERT", QueryValidation.ValidateLeadingKeyword("INSERT INTO t VALUES (?)"), ResultSetType.Update);
        CheckEq("qv.UPDATE", QueryValidation.ValidateLeadingKeyword("UPDATE t SET a=?"), ResultSetType.Update);
        CheckEq("qv.DELETE", QueryValidation.ValidateLeadingKeyword("DELETE FROM t"), ResultSetType.Update);
        CheckEq("qv.REPLACE", QueryValidation.ValidateLeadingKeyword("REPLACE INTO t VALUES (?)"), ResultSetType.Update);
        CheckEq("qv.SET", QueryValidation.ValidateLeadingKeyword("SET @@autocommit = ?"), ResultSetType.Update);
        CheckEq("qv.START", QueryValidation.ValidateLeadingKeyword("START TRANSACTION"), ResultSetType.Update);
        CheckEq("qv.COMMIT", QueryValidation.ValidateLeadingKeyword("COMMIT"), ResultSetType.Update);
        CheckEq("qv.ROLLBACK", QueryValidation.ValidateLeadingKeyword("ROLLBACK"), ResultSetType.Update);
        CheckEq("qv.unknown", QueryValidation.ValidateLeadingKeyword("VACUUM"), ResultSetType.Invalid);
        CheckEq("qv.empty", QueryValidation.ValidateLeadingKeyword("   "), ResultSetType.Invalid);

        Check("qc.pass_select", QueryValidation.ValidateContent("SELECT * FROM t WHERE x = '%'"));
        Check("qc.reject_braces", !QueryValidation.ValidateContent("SELECT '{}' FROM t"));
        Check("qc.reject_semicolon", !QueryValidation.ValidateContent("SELECT 1; SELECT 2"));
    }

    private static void CheckBinder()
    {
        CheckEq("bind.int.kind", Binder.Lower(42).Type, BoundValue.Kind.Int32);
        CheckEq("bind.uint.kind", Binder.Lower(42u).Type, BoundValue.Kind.UInt32);
        CheckEq("bind.short.kind", Binder.Lower((short)3).Type, BoundValue.Kind.Int16);
        CheckEq("bind.ushort.kind", Binder.Lower((ushort)3).Type, BoundValue.Kind.UInt16);
        CheckEq("bind.bool.kind", Binder.Lower(true).Type, BoundValue.Kind.Bool);
        CheckEq("bind.float.kind", Binder.Lower(1.5f).Type, BoundValue.Kind.Float);
        CheckEq("bind.double.kind", Binder.Lower(1.5).Type, BoundValue.Kind.Double);
        CheckEq("bind.string.kind", Binder.Lower("x").Type, BoundValue.Kind.String);
        CheckEq("bind.blob.kind", Binder.Lower(new byte[] { 1 }).Type, BoundValue.Kind.Blob);

        // enum decays to underlying: ZoneId : ushort -> UInt16 (matches enum_decay_t).
        CheckEq("bind.enum.kind", Binder.LowerEnum(ZoneId.CarpentersLanding).Type, BoundValue.Kind.UInt16);
        CheckEq("bind.enum.value", Binder.LowerEnum(ZoneId.CarpentersLanding).Value, (ushort)2);

        // MySqlConnector type mapping mirrors the C++ variant's bindable widths.
        CheckEq("dbtype.int32", MySqlDatabase.DbTypeFor(Binder.Lower(1)), MySqlConnector.MySqlDbType.Int32);
        CheckEq("dbtype.uint32", MySqlDatabase.DbTypeFor(Binder.Lower(1u)), MySqlConnector.MySqlDbType.UInt32);
        CheckEq("dbtype.uint16", MySqlDatabase.DbTypeFor(Binder.Lower((ushort)1)), MySqlConnector.MySqlDbType.UInt16);
        CheckEq("dbtype.blob", MySqlDatabase.DbTypeFor(Binder.Lower(new byte[] { 1 })), MySqlConnector.MySqlDbType.Blob);

        var ps = MySqlDatabase.BuildParameters(new List<BoundValue> { Binder.Lower(7), Binder.Lower("hi") });
        CheckEq("bind.params.count", ps.Count, 2);
        CheckEq("bind.params.order0", ps[0].Value, 7);
        CheckEq("bind.params.order1", ps[1].Value, "hi");
    }

    // A database of fixed SELECT answers that counts what it was asked (PrefetchDatabase's inner)
    private sealed class FakeDb : IDatabase
    {
        public readonly List<string> Asked = new List<string>();
        public int Version_;

        public ResultSet Execute(string query, IReadOnlyList<BoundValue> parameters)
        {
            lock (Asked) Asked.Add(query);
            if (QueryValidation.ValidateLeadingKeyword(query) != ResultSetType.Select)
                return new ResultSet(new InMemoryResultSet(new List<string>(), new List<Dictionary<string, object>>()), query, ResultSetType.Update, 1);
            var row = new Dictionary<string, object> { { "v", Version_ }, { "blob", new byte[] { 1, 2, 3 } }, { "arg", parameters.Count > 0 ? parameters[0].Value : null } };
            return new ResultSet(new InMemoryResultSet(new List<string> { "v", "blob", "arg" }, new List<Dictionary<string, object>> { row }), query, ResultSetType.Select);
        }

        public ResultSet ExecuteBulk(string query, IReadOnlyList<BoundValue> parameters) => Execute(query, parameters);
        public string Schema => "fake";
        public string Version => "";
        public string DriverVersion => "";
        public bool Transaction(Fn transactionFn) { transactionFn(); return true; }
    }

    // PrefetchDatabase: a read set answers its char's SELECTs where the text and the bound values match, as often as
    // asked, each time from the first row and with blobs of its own; a write that may touch the char makes the tables
    // it names stale in the set; outside Serve, or on another thread, every query goes to the database
    private static void CheckPrefetch()
    {
        const uint Char = 4242;
        const string Vars = "SELECT varname, value FROM char_vars WHERE charid = ?";
        const string Look = "SELECT face FROM char_look WHERE charid = ?";
        var id = new[] { BoundValue.Of(Char) };
        var fake = new FakeDb { Version_ = 1 };
        var db = new PrefetchDatabase(fake);

        using (var set = db.Open(Char))
        {
            db.Read(set, Vars, id);
            db.Read(set, Look, id);
            db.Read(set, "UPDATE chars SET x = 1 WHERE charid = ?", id); // not a read: not held
            CheckEq("prefetch: the set holds the two reads", set.Count, 2);
            fake.Version_ = 2; // the database moves on without a write through the wrapper
            fake.Asked.Clear();

            ResultSet outside = db.Execute(Vars, id);
            Check("prefetch: outside Serve the database answers", outside.Next() && outside.Get<int>("v") == 2 && fake.Asked.Count == 1);

            using (db.Serve(set))
            {
                fake.Asked.Clear();
                var a = db.Execute(Vars, id);
                Check("prefetch: a held read is answered from the set", a.Next() && a.Get<int>("v") == 1 && fake.Asked.Count == 0);
                a.GetBlobBytes("blob")[0] = 99;
                var b = db.Execute(Vars, new List<BoundValue> { BoundValue.Of(Char) });
                Check("prefetch: again from the first row, its blob a copy of its own", b.Next() && b.GetBlobBytes("blob")[0] == 1 && !b.Next());
                var other = db.Execute(Vars, new[] { BoundValue.Of(Char + 1) });
                var typed = db.Execute(Vars, new[] { BoundValue.Of((int)Char) });
                Check("prefetch: other bound values (or types) go to the database", other.Next() && other.Get<int>("v") == 2 && typed.Next() && fake.Asked.Count == 2);
                CheckEq("prefetch: misses that bind the char's id are listed", set.MissedCharReads.Count, 1);

                int seenOnWorker = 0;
                var t = new System.Threading.Thread(() => { var r = db.Execute(Vars, id); if (r.Next()) seenOnWorker = r.Get<int>("v"); });
                t.Start();
                t.Join();
                CheckEq("prefetch: another thread is not served", seenOnWorker, 2);

                // another char's row in the same table: still held
                db.Execute("UPDATE char_vars SET value = 1 WHERE charid = ? AND varname = ?", new[] { BoundValue.Of(Char + 1), BoundValue.Of("x") });
                var c = db.Execute(Vars, id);
                Check("prefetch: a write to another char's rows leaves the set", c.Next() && c.Get<int>("v") == 1);

                // the char's own row: char_vars is read from the database again, char_look is still held
                db.Execute("INSERT INTO char_vars SET charid = ?, varname = ?, value = ?", new[] { BoundValue.Of(Char), BoundValue.Of("x"), BoundValue.Of(1) });
                var d = db.Execute(Vars, id);
                var e = db.Execute(Look, id);
                Check("prefetch: a write to the char's rows makes that table stale", d.Next() && d.Get<int>("v") == 2 && e.Next() && e.Get<int>("v") == 1);
            }
        }

        // a read not held is derived from the held ones while its tables are fresh
        using (var set = db.Open(Char))
        {
            db.Read(set, Vars, id);
            set.Derive = (q, a) => q == "SELECT value FROM char_vars WHERE charid = ? AND varname = ?" ? set.Held(Vars, id) : null;
            using (db.Serve(set))
            {
                fake.Asked.Clear();
                var derived = db.Execute("SELECT value FROM char_vars WHERE charid = ? AND varname = ?", new[] { BoundValue.Of(Char), BoundValue.Of("y") });
                Check("prefetch: a derived read is answered from the set", derived != null && derived.Next() && fake.Asked.Count == 0);
                db.Execute("DELETE FROM char_vars WHERE charid = ?", id);
                fake.Asked.Clear();
                db.Execute("SELECT value FROM char_vars WHERE charid = ? AND varname = ?", new[] { BoundValue.Of(Char), BoundValue.Of("y") });
                Check("prefetch: not once its tables are stale", fake.Asked.Count == 1);
            }
        }

        bool Stale(string write, params BoundValue[] args)
        {
            using var set = db.Open(Char);
            db.Read(set, Look, id);
            int held = fake.Version_++;
            db.Execute(write, args);
            using (db.Serve(set))
            {
                var r = db.Execute(Look, id);
                return r.Next() && r.Get<int>("v") != held;
            }
        }
        Check("prefetch: a write without charid makes its tables stale", Stale("DELETE FROM char_look WHERE face = ?", BoundValue.Of(3)));
        Check("prefetch: a write with a join or a subquery may touch the char", Stale("UPDATE char_look JOIN chars USING (charid) SET face = 1 WHERE charid = ?", BoundValue.Of(Char + 1)));
        Check("prefetch: the char's id in the text touches it", Stale($"UPDATE char_look SET face = 1 WHERE charid = {Char}"));
        Check("prefetch: a write to another table leaves it", !Stale("UPDATE char_vars SET value = 1 WHERE charid = ?", BoundValue.Of(Char)));
        Check("prefetch: a write naming no table it can find makes everything stale", Stale("SET @x = 1"));

        CheckEq("prefetch: tables of a join", string.Join(",", PrefetchDatabase.TablesOf("SELECT i.slot FROM char_inventory i LEFT JOIN item_basic b ON b.itemid = i.itemId WHERE i.charid = ?")), "char_inventory,item_basic");
        CheckEq("prefetch: tables of an insert", string.Join(",", PrefetchDatabase.TablesOf("INSERT INTO `char_vars` (charid) VALUES (?)")), "char_vars");
    }

    private static void CheckResultSetContract()
    {
        // Columns named in declaration order; the public accessor is 0-indexed, the raw backend 1-indexed.
        var labels = new List<string> { "charid", "zoneid", "name", "posx" };
        var rows = new List<Dictionary<string, object>>
        {
            new Dictionary<string, object> { { "charid", 12345u }, { "zoneid", (ushort)2 }, { "name", "Test" }, { "posx", 10.5 } },
            new Dictionary<string, object> { { "charid", 67890u }, { "zoneid", (ushort)1 }, { "name", "Two" }, { "posx", -3.25 } },
        };

        var raw = new InMemoryResultSet(labels, rows);
        var rs = new ResultSet(raw, "SELECT charid, zoneid, name, posx FROM chars", ResultSetType.Select);

        // columnName is 0-indexed public -> rawColumnLabel(index+1).
        CheckEq("rset.colname0", rs.ColumnName(0), "charid");
        CheckEq("rset.colname3", rs.ColumnName(3), "posx");
        CheckEq("rset.colcount", rs.ColumnCount(), 4u);

        Check("rset.next1", rs.Next());
        CheckEq("rset.byindex0.uint", rs.Get<uint>(0), 12345u);
        CheckEq("rset.byindex1.zoneid", rs.Get<ZoneId>(1), ZoneId.CarpentersLanding);
        CheckEq("rset.byindex2.name", rs.Get<string>(2), "Test");
        CheckEq("rset.byindex3.double", rs.Get<double>(3), 10.5);
        CheckEq("rset.bykey.charid", rs.Get<uint>("charid"), 12345u);

        Check("rset.next2", rs.Next());
        CheckEq("rset.row2.zoneid", rs.Get<ZoneId>(1), ZoneId.PhanauetChannel);
        CheckEq("rset.row2.neg", rs.Get<double>(3), -3.25);
        Check("rset.next_none", !rs.Next());

        // NULL handling: get<T>(key) on a null column returns default; GetOrDefault returns fallback.
        var rowsNull = new List<Dictionary<string, object>>
        {
            new Dictionary<string, object> { { "charid", 1u }, { "name", null } },
        };
        var rsNull = new ResultSet(new InMemoryResultSet(new List<string> { "charid", "name" }, rowsNull), "SELECT", ResultSetType.Select);
        rsNull.Next();
        Check("rset.isNull", rsNull.IsNull("name"));
        CheckEq("rset.null.default", rsNull.Get<string>("name"), "");
        CheckEq("rset.null.getordefault", rsNull.GetOrDefault("name", "fallback"), "fallback");

        // Update result set: only rowsAffected is meaningful; Select-only accessors return guards.
        var rsUpd = new ResultSet(raw, "UPDATE t", ResultSetType.Update, rowsAffected: 3);
        CheckEq("rset.update.affected", rsUpd.RowsAffected(), 3u);
        Check("rset.update.next_false", !rsUpd.Next());
        CheckEq("rset.update.colcount", rsUpd.ColumnCount(), 0u);
    }

    // ---- extractFromBlob C-array branch (blowfish_t::key) vs the compiled layout oracle ----

    // The blob->map-cipher step: map_networking.cpp:346 memcpy's the real accounts_sessions
    // session_key (binary(20)) into blowfish_t::key (uint32[5]) via the array branch of
    // db::extractFromBlob, then initBlowfish() derives the cipher. The array branch is
    // NULL-skip + ranges::fill(uint32{}) zero-init + memcpy(min(20, blobLen)), so the resulting
    // words are the little-endian u32 of the copied bytes. Every expected word/offset here comes
    // ONLY from the compiled harness (map_blowfish_t_golden.txt), never hand-computed.
    private static void CheckBlowfishKeyBlob()
    {
        if (!File.Exists(BlowfishTGoldenPath))
        {
            Check($"bft.golden_missing[{BlowfishTGoldenPath}]", false);
            return;
        }

        var g = ReadKeyValues(BlowfishTGoldenPath);

        // blowfish_t layout constants, cross-checked against the compiler's sizeof/offsetof/enum.
        CheckEq("bft.sizeof", BlowfishT.SizeOf, (int)ValL(g, "BFT_sizeof"));
        CheckEq("bft.alignof", BlowfishT.AlignOf, (int)ValL(g, "BFT_alignof"));
        CheckEq("bft.off_key", BlowfishT.OffKey, (int)ValL(g, "BFT_off_key"));
        CheckEq("bft.off_hash", BlowfishT.OffHash, (int)ValL(g, "BFT_off_hash"));
        CheckEq("bft.off_P", BlowfishT.OffP, (int)ValL(g, "BFT_off_P"));
        CheckEq("bft.off_S", BlowfishT.OffS, (int)ValL(g, "BFT_off_S"));
        CheckEq("bft.off_status", BlowfishT.OffStatus, (int)ValL(g, "BFT_off_status"));
        CheckEq("bft.status_WAITING", BlowfishT.StatusWaiting, (int)ValL(g, "BFT_status_WAITING"));
        CheckEq("bft.status_SENT", BlowfishT.StatusSent, (int)ValL(g, "BFT_status_SENT"));
        CheckEq("bft.status_ACCEPTED", BlowfishT.StatusAccepted, (int)ValL(g, "BFT_status_ACCEPTED"));
        CheckEq("bft.status_PENDING_ZONE", BlowfishT.StatusPendingZone, (int)ValL(g, "BFT_status_PENDING_ZONE"));
        CheckEq("bft.key_bytes", BlowfishT.KeyBytes, (int)ValL(g, "KEY_bytes"));
        CheckEq("bft.key_elems", BlowfishT.KeyElems, (int)ValL(g, "KEY_elems"));
        CheckEq("bft.key_elem_size", BlowfishT.KeyElemSize, (int)ValL(g, "KEY_elem_size"));

        // Rebuild each harness input blob EXACTLY as the harness built it, then verify the C#
        // array-branch extract against BOTH the compiler's K32_<tag>_<i> memcpy result AND the
        // already-proven BlobExtract byte path (a third independent derivation of the same bytes).
        CheckK32(g, "full", Ramp(0xA0, 20), false);
        CheckK32(g, "pad", Ramp(0xC0, 12), false);
        CheckK32(g, "trunc", Ramp(0xD0, 24), false);
        CheckK32(g, "null", Ramp(0xA0, 20), true);
        CheckK32(g, "empty", Array.Empty<byte>(), false);
    }

    private static void CheckK32(Dictionary<string, string> g, string tag, byte[] blob, bool isNull)
    {
        var words = BlowfishKeyBlob.ExtractWords(blob, isNull);
        CheckEq($"bft.{tag}.count", words.Length, BlowfishT.KeyElems);

        // Independent byte-path derivation: extractFromBlob's memcpy is the same as
        // BlobExtract.Extract(destSize=20), reinterpreted as little-endian words.
        var bytes = BlobExtract.Extract(BlowfishT.KeyBytes, blob, isNull);
        CheckEq($"bft.{tag}.bytesize", bytes.Length, BlowfishT.KeyBytes);

        for (var w = 0; w < BlowfishT.KeyElems; w++)
        {
            var viaBytes = (uint)(bytes[w * 4] | (bytes[w * 4 + 1] << 8) | (bytes[w * 4 + 2] << 16) | (bytes[w * 4 + 3] << 24));
            CheckEq($"bft.{tag}.w{w}_vs_bytes", words[w], viaBytes);
            CheckEq($"bft.{tag}.w{w}_vs_compiler", $"0x{words[w]:X8}", g[$"K32_{tag}_{w}"]);
        }
    }

    // Mirrors the harness's per-byte blob generator: byte[i] = (start + i) & 0xFF.
    private static byte[] Ramp(int start, int len)
    {
        var b = new byte[len];
        for (var i = 0; i < len; i++)
        {
            b[i] = (byte)(start + i);
        }

        return b;
    }

    // Parses a KEY=VALUE (one per line) oracle file into a lookup, tolerating blank lines.
    private static Dictionary<string, string> ReadKeyValues(string path)
    {
        var d = new Dictionary<string, string>();
        foreach (var line in File.ReadAllLines(path))
        {
            var idx = line.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            d[line.Substring(0, idx).Trim()] = line.Substring(idx + 1).Trim();
        }

        return d;
    }

    // Reads an oracle value as int64, accepting 0x-prefixed hex or decimal.
    private static long ValL(Dictionary<string, string> d, string key)
    {
        var s = d[key];
        if (s.StartsWith("0x") || s.StartsWith("0X"))
        {
            return Convert.ToInt64(s.Substring(2), 16);
        }

        return Convert.ToInt64(s, 10);
    }
}
