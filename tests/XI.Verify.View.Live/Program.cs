using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using XI.Login;
using XI.Persistence;

// XI.Verify.View.Live -- live gate for the ported view_session (lobby port 54001: 0x07 select,
// 0x14 delete, 0x21 register, 0x22 check name, 0x28 rename, 0x26 version, 0x1F, 0x24 world list)
// against the real MariaDB (docker xi-live-db, host:3307, xidb). Sockets are recording fakes.
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint Acc = 990201, Acc2 = 990202;
    private const uint Char1 = 990301, Char2 = 990302;
    private const string Ip = "127.0.0.1";

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static void CheckTrue(string name, bool cond) => Check(name, true, cond);

    private sealed class FakeChannel : ILobbyChannel
    {
        public readonly List<byte[]> Writes = new();
        public bool Closed;
        public void Write(byte[] data) => Writes.Add(data);
        public void Close() => Closed = true;
        public byte[] Last => Writes.Count == 0 ? null : Writes[^1];
    }

    private sealed class RecSink : ICharZoneSink
    {
        public void CharZone(uint charId, ushort zone) { }
    }

    private static void Sql(IDatabase db, string q, params object[] a) => LoginDb.PreparedStmt(db, q, a);

    // every table with a charid column, read from the schema at run time: chars' char_insert trigger fills char_equip, char_exp,
    // char_history, char_inventory, char_jobs, char_pet, char_points, char_profile, char_storage and char_unlocks and only
    // char_delete clears them, so a run aborted with one of them left behind would make the next run's INSERT INTO chars collide
    private static List<string> CharidTables(IDatabase db)
    {
        var tables = new List<string>();
        var r = LoginDb.PreparedStmt(db, "SELECT table_name AS t FROM information_schema.columns WHERE table_schema = DATABASE() AND column_name = 'charid' AND table_name <> 'chars'");
        while (r != null && r.Next()) tables.Add(r.Get<string>("t"));
        foreach (var t in new[] { "char_equip", "char_exp", "char_history", "char_inventory", "char_jobs", "char_pet", "char_points", "char_profile",
                                  "char_storage", "char_unlocks" })
            if (!tables.Contains(t)) tables.Add(t);
        return tables;
    }

    private static void Cleanup(IDatabase db)
    {
        var ids = new List<uint> { Char1, Char2 };
        var rs = LoginDb.PreparedStmt(db, "SELECT charid FROM chars WHERE charname LIKE 'Vw%' OR accid IN (?, ?) OR original_accid IN (?, ?)", Acc, Acc2, Acc, Acc2);
        while (rs != null && rs.Next()) { var id = rs.Get<uint>("charid"); if (!ids.Contains(id)) ids.Add(id); }
        foreach (var c in ids)
        {
            foreach (var t in CharidTables(db)) Sql(db, $"DELETE FROM `{t}` WHERE charid = ?", c);
            Sql(db, "DELETE FROM chars WHERE charid = ?", c);
        }
        foreach (var a in new[] { Acc, Acc2 })
        {
            Sql(db, "DELETE FROM accounts_sessions WHERE accid = ?", a);
            Sql(db, "DELETE FROM accounts_profile WHERE accid = ?", a);
            Sql(db, "DELETE FROM accounts WHERE id = ?", a);
        }
    }

    private static void Seed(IDatabase db)
    {
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)", Acc, "vwgate1", "x", (byte)3);
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)", Acc2, "vwgate2", "x", (byte)1);
        foreach (var (c, a, n) in new[] { (Char1, Acc, "Vwgatea"), (Char2, Acc2, "Vwgateb") })
        {
            Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,nation,gmlevel) VALUES (?,?,?,?,?,?,?)", c, a, n, (ushort)230, (ushort)230, (byte)1, (byte)0);
            Sql(db, "INSERT INTO char_stats(charid,mjob,sjob) VALUES (?,?,?)", c, (byte)1, (byte)0);
            Sql(db, "INSERT INTO char_look(charid) VALUES (?)", c);
            Sql(db, "INSERT INTO char_flags(charid,`rename`) VALUES (?,?)", c, c == Char1 ? (byte)1 : (byte)0);
        }
    }

    // the csharp folder (settings/, scripts/, data/ ... copied from the C++ server), found above the test's bin folder
    private static string FindServerRoot()
    {
        for (var d = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory); d != null; d = d.Parent)
            if (System.IO.File.Exists(System.IO.Path.Combine(d.FullName, "settings", "default", "main.lua"))) return d.FullName;
        throw new System.IO.DirectoryNotFoundException("settings/default/main.lua not found above " + System.AppContext.BaseDirectory);
    }

    private static int Main(string[] argv)
    {
        int dbPort = argv.Length > 0 ? int.Parse(argv[0]) : 3307;
        var settings = new NetworkSettings { SqlHost = "127.0.0.1", SqlPort = dbPort, SqlLogin = "root", SqlPassword = "root", SqlDatabase = "xidb" };
        using var db = new MySqlDatabase(settings);

        try
        {
            var probe = db.Execute("SELECT 1", Array.Empty<BoundValue>());
            if (probe == null || !probe.Next()) { Console.WriteLine("DB not reachable"); return 3; }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DB not reachable ({ex.GetType().Name}: {ex.Message})");
            return 3;
        }

        try
        {
            Cleanup(db);
            Seed(db);
            Run(db);
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FAIL unhandled {ex}");
        }
        finally
        {
            Cleanup(db);
        }

        Console.WriteLine($"RESULT: {_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    private static uint U32(byte[] b, int o) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o));
    private static ushort U16(byte[] b, int o) => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(o));
    private static string Str(byte[] b, int o, int len) { var s = b.AsSpan(o, len); int z = s.IndexOf((byte)0); return Encoding.UTF8.GetString(z < 0 ? s : s[..z]); }

    private static bool HashOk(byte[] pkt, int len)
    {
        var copy = (byte[])pkt.Clone();
        Array.Clear(copy, 12, 16);
        return MD5.HashData(copy.AsSpan(0, len)).AsSpan().SequenceEqual(pkt.AsSpan(12, 16));
    }

    private sealed class Ctx
    {
        public LoginSessions Sessions;
        public LoginSession S;
        public ViewSession V;
        public FakeChannel Self = new(), Data = new();
        public DataSession D;
        public string Key;
        public ViewSettings Settings;
    }

    private static Ctx NewCtx(IDatabase db, ViewSettings vs, uint acc = Acc, bool withData = true)
    {
        var c = new Ctx { Settings = vs, Sessions = new LoginSessions() };
        var hash = new byte[16]; RandomNumberGenerator.Fill(hash);
        c.Key = LoginSessions.HashKey(hash);
        c.S = c.Sessions.GetAuthenticatedSession(Ip, c.Key);
        c.S.AccountId = acc; c.S.KeyCounter = 0x100;
        c.V = new ViewSession(Ip, c.Self, db, c.Sessions, new OtpStore(db), vs);
        if (withData)
        {
            c.D = new DataSession(Ip, c.Data, db, c.Sessions, new RecSink(), new LobbySettings());
            c.S.Data = c.D;
        }
        return c;
    }

    private static byte[] Req(Ctx c, byte cmd, int len = 256)
    {
        var b = new byte[len];
        b[0] = (byte)len;
        b[8] = cmd;
        Encoding.Latin1.GetBytes(c.Key).CopyTo(b, 12);
        return b;
    }

    private static void PutName(byte[] b, int off, string name) => Encoding.Latin1.GetBytes(name).CopyTo(b, off);

    private static uint Count(IDatabase db, string q, params object[] a)
    {
        var rs = LoginDb.PreparedStmt(db, q, a);
        return rs != null && rs.Next() ? rs.Get<uint>("c") : uint.MaxValue;
    }

    private static ViewSettings Vs() => new ViewSettings
    {
        ClientVer = "30251001_0",
        ServerName = "TestWorld",
        BadNamesPath = System.IO.Path.Combine(FindServerRoot(), "res", "badnames.bin"),
        Random = n => 1,
    };

    private static void Run(IDatabase db)
    {
        // ---- unknown hash: ignored
        {
            var c = NewCtx(db, Vs());
            var r = Req(c, 0x24); r[12] ^= 0xFF;
            c.V.Handle(r);
            Check("unknown hash: no write", 0, c.Self.Writes.Count);
            Check("unknown hash: counter untouched", 0x100u, c.S.KeyCounter);
        }

        // ---- 0x24 world list + key counter (request +1, reply 0x23 +1)
        {
            var c = NewCtx(db, Vs());
            c.V.Handle(Req(c, 0x24));
            var w = c.Self.Last;
            Check("0x24: len", 52, w.Length);
            Check("0x24: size field", 52u, U32(w, 0));
            Check("0x24: terminator", 0x46465849u, U32(w, 4));
            Check("0x24: command", 0x23u, U32(w, 8));
            Check("0x24: sumofworld", 1u, U32(w, 28));
            Check("0x24: world no", 0x20u, U32(w, 32));
            Check("0x24: world name", "TestWorld", Str(w, 36, 16));
            CheckTrue("0x24: md5", HashOk(w, 52));
            Check("0x24: counter +2 (request + reply)", 0x102u, c.S.KeyCounter);
            Check("0x24: keyRequest", (byte)0x24, c.S.KeyRequest);
            CheckTrue("view bound into session", ReferenceEquals(c.S.View, c.V));
        }

        // ---- 0x26 version
        {
            var vs = Vs();
            var c = NewCtx(db, vs);
            var r = Req(c, 0x26); PutName(r, 0x74, "30251001_0");
            c.V.Handle(r);
            var w = c.Self.Last;
            Check("0x26 match: len", 0x28, w.Length);
            Check("0x26 match: command", 5u, U32(w, 8));
            Check("0x26 match: key", 0xAD5DE04Fu, U32(w, 28));
            Check("0x26 match: expansions", (uint)LoginHelpers.GenerateExpansionBitmask(false, false, false, false, false, false, false, false, false, false, false), U32(w, 32));
            Check("0x26 match: features", 0u, U32(w, 36));
            CheckTrue("0x26 match: md5", HashOk(w, 0x28));
            Check("0x26 match: counter reset to key+1", 0xAD5DE050u, c.S.KeyCounter);

            vs = Vs(); vs.RiseOfZilart = true; vs.MogWardrobe3 = true;
            c = NewCtx(db, vs);
            r = Req(c, 0x26); PutName(r, 0x74, "30251001_0");
            c.V.Handle(r);
            Check("0x26 expansion bits", (uint)LoginHelpers.GenerateExpansionBitmask(true, false, false, false, false, false, false, false, false, false, false), U32(c.Self.Last, 32));
            Check("0x26 feature bits", (uint)LoginHelpers.GenerateFeatureBitmask(true, false, false, false, false, false, false), U32(c.Self.Last, 36));

            // mismatch, VER_LOCK 0: continues
            c = NewCtx(db, Vs());
            r = Req(c, 0x26); PutName(r, 0x74, "99999999_0");
            c.V.Handle(r);
            Check("0x26 mismatch verlock0: still key packet", 5u, U32(c.Self.Last, 8));

            // VER_LOCK 1: fatal 331
            vs = Vs(); vs.VerLock = 1;
            c = NewCtx(db, vs);
            c.V.Handle(r = Req(c, 0x26));
            PutName(r, 0x74, "99999999_0"); c.V.Handle(r);
            Check("0x26 verlock1: error 331", (ushort)331, U16(c.Self.Last, 32));
            Check("0x26 verlock1: command 4", 4u, U32(c.Self.Last, 8));

            // VER_LOCK 2: client newer -> ok, client older -> fatal
            vs = Vs(); vs.VerLock = 2;
            c = NewCtx(db, vs);
            r = Req(c, 0x26); PutName(r, 0x74, "40000000_0"); c.V.Handle(r);
            Check("0x26 verlock2 newer client: ok", 5u, U32(c.Self.Last, 8));
            c = NewCtx(db, vs);
            r = Req(c, 0x26); PutName(r, 0x74, "20000000_0"); c.V.Handle(r);
            Check("0x26 verlock2 older client: 331", (ushort)331, U16(c.Self.Last, 32));
        }

        // ---- 0x1F
        {
            var c = NewCtx(db, Vs());
            c.V.Handle(Req(c, 0x1F));
            CheckTrue("0x1F: data session gets {01,0,0,0,0}", c.Data.Last != null && c.Data.Last.AsSpan().SequenceEqual(new byte[] { 1, 0, 0, 0, 0 }));
            Check("0x1F: counter +1", 0x101u, c.S.KeyCounter);

            c = NewCtx(db, Vs(), withData: false);
            c.V.Handle(Req(c, 0x1F));
            Check("0x1F no data: error 332", (ushort)332, U16(c.Self.Last, 32));
            Check("0x1F no data: counter +1 (request only)", 0x101u, c.S.KeyCounter);
        }

        // ---- 0x07 select
        {
            var c = NewCtx(db, Vs());
            var r = Req(c, 0x07);
            BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(28), Char1); PutName(r, 36, "Vwgatea");
            c.V.Handle(r);
            Check("0x07: requestedCharacterId", Char1, c.S.RequestedCharacterId);
            CheckTrue("0x07: data gets {02,0,0,0,0}", c.Data.Last != null && c.Data.Last.AsSpan().SequenceEqual(new byte[] { 2, 0, 0, 0, 0 }));
            CheckTrue("0x07: not closed", !c.Self.Closed);

            c = NewCtx(db, Vs());
            r = Req(c, 0x07);
            BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(28), Char1); PutName(r, 36, "Wrongname");
            c.V.Handle(r);
            CheckTrue("0x07 wrong name: closed", c.Self.Closed);
            Check("0x07 wrong name: no data write", 0, c.Data.Writes.Count);

            c = NewCtx(db, Vs());
            r = Req(c, 0x07);
            BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(28), Char2); PutName(r, 36, "Vwgateb");
            c.V.Handle(r);
            CheckTrue("0x07 other account's char: closed", c.Self.Closed);
            Check("0x07 other account's char: no data write", 0, c.Data.Writes.Count);
        }

        // ---- 0x22 check name
        {
            var c = NewCtx(db, Vs());
            var r = Req(c, 0x22); PutName(r, 32, "Vwnewone");
            c.V.Handle(r);
            var w = c.Self.Last;
            Check("0x22 ok: len", 0x20, w.Length);
            Check("0x22 ok: result 3", (byte)3, w[8]);
            CheckTrue("0x22 ok: md5", HashOk(w, 0x20));
            Check("0x22 ok: name remembered", "Vwnewone", c.S.RequestedNewCharacterName);
            Check("0x22 ok: counter +2", 0x102u, c.S.KeyCounter);

            foreach (var (label, name) in new[] { ("digits", "Vw123abc"), ("too short", "Vw"), ("in use", "Vwgatea"), ("in use (case)", "vwGATEA") })
            {
                c = NewCtx(db, Vs());
                r = Req(c, 0x22); PutName(r, 32, name);
                c.V.Handle(r);
                Check($"0x22 {label}: error 313", (ushort)313, U16(c.Self.Last, 32));
                Check($"0x22 {label}: command 4", 4u, U32(c.Self.Last, 8));
                Check($"0x22 {label}: counter +2 (error answering 0x22)", 0x102u, c.S.KeyCounter);
                Check($"0x22 {label}: name not remembered", "", c.S.RequestedNewCharacterName);
            }

            var vs = Vs(); vs.MaintMode = 1;
            c = NewCtx(db, vs); r = Req(c, 0x22); PutName(r, 32, "Vwnewtwo"); c.V.Handle(r);
            Check("0x22 maintenance: 314", (ushort)314, U16(c.Self.Last, 32));
            vs = Vs(); vs.CharacterCreation = false;
            c = NewCtx(db, vs); r = Req(c, 0x22); PutName(r, 32, "Vwnewtwo"); c.V.Handle(r);
            Check("0x22 creation disabled: 314", (ushort)314, U16(c.Self.Last, 32));

            c = NewCtx(db, Vs(), Acc2);
            r = Req(c, 0x22); PutName(r, 32, "Vwnewtwo"); c.V.Handle(r);
            Check("0x22 account full (content_ids=1): 314", (ushort)314, U16(c.Self.Last, 32));

            vs = Vs(); vs.BannedWords = new[] { "gor" };
            c = NewCtx(db, vs); r = Req(c, 0x22); PutName(r, 32, "Vwgorilla"); c.V.Handle(r);
            Check("0x22 banned word (case-insens): 313", (ushort)313, U16(c.Self.Last, 32));

            vs = Vs(); vs.DisableMobNpcCharNames = true; vs.IsEntityName = n => n == "VWENTITY";
            c = NewCtx(db, vs); r = Req(c, 0x22); PutName(r, 32, "Vw_entity"); c.V.Handle(r);
            Check("0x22 matches mob/npc name: 313", (ushort)313, U16(c.Self.Last, 32));
        }

        // ---- 0x21 register / createCharacter
        {
            var c = NewCtx(db, Vs());
            // nation 1 (Bastok) via random index 1 -> 235; race 3, size 1, face 7, mjob 9 -> clamped 6
            c.S.RequestedNewCharacterName = "Vwnewone";
            var r = Req(c, 0x21);
            r[48] = 3; r[57] = 1; r[60] = 7; r[50] = 9; r[54] = 1;
            c.V.Handle(r);
            var w = c.Self.Last;
            CheckTrue("0x21: success packet", w != null && w[8] == 3 && HashOk(w, 0x20));
            Check("0x21: counter +2 (request + reply 0x20)", 0x102u, c.S.KeyCounter);
            Check("0x21: name cleared", "", c.S.RequestedNewCharacterName);

            var rs = LoginDb.PreparedStmt(db, "SELECT charid, accid, pos_zone, nation FROM chars WHERE charname = ?", "Vwnewone");
            CheckTrue("0x21: chars row", rs != null && rs.Next());
            uint newId = rs.Get<uint>("charid");
            Check("0x21: accid", Acc, rs.Get<uint>("accid"));
            Check("0x21: pos_zone (bastok idx1 = Bastok Markets)", (ushort)235, rs.Get<ushort>("pos_zone"));
            Check("0x21: nation", (byte)1, rs.Get<byte>("nation"));
            Check("0x21: char_look", 1u, Count(db, "SELECT COUNT(*) AS c FROM char_look WHERE charid = ? AND race = 3 AND size = 1 AND face = 7", newId));
            Check("0x21: char_stats mjob clamped to 6", 1u, Count(db, "SELECT COUNT(*) AS c FROM char_stats WHERE charid = ? AND mjob = 6", newId));
            foreach (var t in new[] { "char_exp", "char_flags", "char_jobs", "char_points", "char_unlocks", "char_profile", "char_storage", "char_inventory" })
                Check($"0x21: {t} row", 1u, Count(db, $"SELECT COUNT(*) AS c FROM {t} WHERE charid = ?", newId));
            Check("0x21: new-character cutscene var", 1u, Count(db, "SELECT COUNT(*) AS c FROM char_vars WHERE charid = ? AND varname = 'HQuest[newCharacterCS]notSeen'", newId));

            // invalid race
            c = NewCtx(db, Vs());
            c.S.RequestedNewCharacterName = "Vwnewtwo";
            r = Req(c, 0x21); r[48] = 9; r[54] = 0;
            c.V.Handle(r);
            CheckTrue("0x21 invalid race: closed", c.Self.Closed);
            Check("0x21 invalid race: nothing created", 0u, Count(db, "SELECT COUNT(*) AS c FROM chars WHERE charname = 'Vwnewtwo'"));

            // name taken between 0x22 and 0x21
            c = NewCtx(db, Vs());
            c.S.RequestedNewCharacterName = "Vwgatea";
            r = Req(c, 0x21); r[48] = 3; r[54] = 0;
            c.V.Handle(r);
            CheckTrue("0x21 taken name: closed", c.Self.Closed);

            // nations pick the right zone triple
            foreach (var (nation, zone) in new[] { ((byte)0, (ushort)231), ((byte)2, (ushort)240) })
            {
                var vs = Vs();
                c = NewCtx(db, vs, Acc);
                Sql(db, "DELETE FROM chars WHERE charname = 'Vwnewone'"); // keep the account under its content_ids cap
                c.S.RequestedNewCharacterName = "Vwnation" + (char)('a' + nation);
                r = Req(c, 0x21); r[48] = 1; r[54] = nation;
                c.V.Handle(r);
                var z = LoginDb.PreparedStmt(db, "SELECT pos_zone FROM chars WHERE charname = ?", c.S.RequestedNewCharacterName == "" ? "Vwnation" + (char)('a' + nation) : c.S.RequestedNewCharacterName);
                CheckTrue($"0x21 nation {nation}: row", z != null && z.Next());
                Check($"0x21 nation {nation}: zone", zone, z.Get<ushort>("pos_zone"));
            }
        }

        // ---- 0x14 delete
        {
            var vs = Vs(); vs.CharacterDeletion = false;
            var c = NewCtx(db, vs);
            var r = Req(c, 0x14); BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(28), Char1);
            c.V.Handle(r);
            Check("0x14 disabled: error 332", (ushort)332, U16(c.Self.Last, 32));
            Check("0x14 disabled: counter +1 (error does not answer 0x14)", 0x101u, c.S.KeyCounter);
            Check("0x14 disabled: char untouched", Acc, Count(db, "SELECT accid AS c FROM chars WHERE charid = ?", Char1));

            // other account's char: success reply goes out, then close, no DB change
            c = NewCtx(db, Vs());
            r = Req(c, 0x14); BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(28), Char2);
            c.V.Handle(r);
            Check("0x14 foreign: reply still sent", (byte)3, c.Self.Last[8]);
            CheckTrue("0x14 foreign: closed", c.Self.Closed);
            Check("0x14 foreign: char untouched", Acc2, Count(db, "SELECT accid AS c FROM chars WHERE charid = ?", Char2));

            c = NewCtx(db, Vs());
            r = Req(c, 0x14); BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(28), Char1);
            c.V.Handle(r);
            CheckTrue("0x14 ok: success packet", c.Self.Last[8] == 3 && HashOk(c.Self.Last, 0x20));
            Check("0x14 ok: counter +2", 0x102u, c.S.KeyCounter);
            CheckTrue("0x14 ok: not closed", !c.Self.Closed);
            Check("0x14 ok: accid zeroed", 0u, Count(db, "SELECT accid AS c FROM chars WHERE charid = ?", Char1));
            Check("0x14 ok: original_accid kept", Acc, Count(db, "SELECT original_accid AS c FROM chars WHERE charid = ?", Char1));
        }

        // ---- 0x28 rename (Char1 was soft-deleted above: give it back)
        {
            Sql(db, "UPDATE chars SET accid = ? WHERE charid = ?", Acc, Char1);
            Sql(db, "UPDATE char_flags SET `rename` = 1 WHERE charid = ?", Char1);

            var c = NewCtx(db, Vs());
            var r = Req(c, 0x28);
            BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(28), Char1); PutName(r, 36, "Vwgatea"); // already in use
            c.V.Handle(r);
            Check("0x28 name in use: 313", (ushort)313, U16(c.Self.Last, 32));
            Check("0x28 name in use: counter +2", 0x102u, c.S.KeyCounter);
            Check("0x28 name in use: flag kept", 1u, Count(db, "SELECT `rename` AS c FROM char_flags WHERE charid = ?", Char1));

            c = NewCtx(db, Vs());
            r = Req(c, 0x28);
            BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(28), Char1); PutName(r, 36, "Vwrenamed");
            c.V.Handle(r);
            CheckTrue("0x28 ok: success packet", c.Self.Last[8] == 3 && HashOk(c.Self.Last, 0x20));
            Check("0x28 ok: counter +2", 0x102u, c.S.KeyCounter);
            Check("0x28 ok: requestedCharacterId", Char1, c.S.RequestedCharacterId);
            Check("0x28 ok: name updated", 1u, Count(db, "SELECT COUNT(*) AS c FROM chars WHERE charid = ? AND charname = 'Vwrenamed'", Char1));
            Check("0x28 ok: flag cleared", 0u, Count(db, "SELECT `rename` AS c FROM char_flags WHERE charid = ?", Char1));

            // flag now cleared: a second rename is illegitimate
            c = NewCtx(db, Vs());
            r = Req(c, 0x28);
            BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(28), Char1); PutName(r, 36, "Vwagain");
            c.V.Handle(r);
            CheckTrue("0x28 not flagged: closed", c.Self.Closed);
            Check("0x28 not flagged: name unchanged", 1u, Count(db, "SELECT COUNT(*) AS c FROM chars WHERE charid = ? AND charname = 'Vwrenamed'", Char1));

            c = NewCtx(db, Vs());
            r = Req(c, 0x28);
            BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(28), Char2); PutName(r, 36, "Vwstolen");
            c.V.Handle(r);
            CheckTrue("0x28 foreign char: closed", c.Self.Closed);
        }

        // ---- data-session writes to the view step the counter (do_write is virtual)
        {
            var c = NewCtx(db, Vs());
            c.V.Handle(Req(c, 0x24)); // binds View = ViewSession
            uint before = c.S.KeyCounter;
            var pkt = new byte[16]; pkt[8] = 0x03;
            c.S.View.Write(pkt);
            Check("reply 0x03 via session.View steps counter", before + 1, c.S.KeyCounter);
            pkt[8] = 0x0B; c.S.View.Write(pkt);
            Check("reply 0x0B steps counter", before + 2, c.S.KeyCounter);
            pkt[8] = 0x99; c.S.View.Write(pkt);
            Check("unknown reply does not step", before + 2, c.S.KeyCounter);
            c.S.KeyRequest = 0x14;
            pkt[8] = 0x04; c.S.View.Write(pkt);
            Check("error answering 0x14 does not step", before + 2, c.S.KeyCounter);
            c.S.KeyRequest = 0x2B;
            c.S.View.Write(pkt);
            Check("error answering 0x2B steps", before + 3, c.S.KeyCounter);
        }


        // ---- end to end over real loopback sockets (LobbyServer / LobbyConnection)
        {
            var sessions = new LoginSessions();
            var vs = Vs();
            var otp = new OtpStore(db);
            var hash = new byte[16]; RandomNumberGenerator.Fill(hash);
            var key = LoginSessions.HashKey(hash);
            var s = sessions.GetAuthenticatedSession(Ip, key);
            s.AccountId = Acc; s.KeyCounter = 0x100;

            using var viewSrv = new LobbyServer(System.Net.IPAddress.Loopback, 0, (ip, ch) => new ViewSession(ip, ch, db, sessions, otp, vs));
            using var dataSrv = new LobbyServer(System.Net.IPAddress.Loopback, 0, (ip, ch) => new DataSession(ip, ch, db, sessions, new RecSink(), new LobbySettings()));
            viewSrv.Start(); dataSrv.Start();

            using var viewC = new System.Net.Sockets.TcpClient(); viewC.Connect(System.Net.IPAddress.Loopback, viewSrv.Port);
            using var dataC = new System.Net.Sockets.TcpClient(); dataC.Connect(System.Net.IPAddress.Loopback, dataSrv.Port);
            var vstream = viewC.GetStream(); var dstream = dataC.GetStream();

            byte[] ReadExact(System.Net.Sockets.NetworkStream st, int len)
            {
                var b = new byte[len]; int got = 0;
                using var cts = new System.Threading.CancellationTokenSource(5000);
                try { while (got < len) { int n = st.ReadAsync(b, got, len - got, cts.Token).GetAwaiter().GetResult(); if (n <= 0) return null; got += n; } }
                catch (OperationCanceledException) { return null; }
                return b;
            }

            byte[] Request(byte cmd, int len = 256)
            {
                var r = new byte[len]; r[0] = (byte)len; r[8] = cmd;
                Encoding.Latin1.GetBytes(key).CopyTo(r, 12);
                return r;
            }

            vstream.Write(Request(0x24));
            var wl = ReadExact(vstream, 52);
            CheckTrue("e2e view: world list over tcp", wl != null && U32(wl, 8) == 0x23 && HashOk(wl, 52));
            CheckTrue("e2e view: bound into session", ReferenceEquals(s.View is ViewSession ? s.View : null, s.View) && s.View != null);

            var a1 = new byte[64]; a1[0] = 0xA1;
            BinaryPrimitives.WriteUInt32LittleEndian(a1.AsSpan(1), Acc);
            BinaryPrimitives.WriteUInt32LittleEndian(a1.AsSpan(5), 0x0100007Fu);
            Encoding.Latin1.GetBytes(key).CopyTo(a1, 12);
            dstream.Write(a1);
            var ulist = ReadExact(dstream, 0x148);
            CheckTrue("e2e data: 0x148 uList over tcp", ulist != null);
            int chrLen = 32 + 140 * ulist[1];
            var chr = ReadExact(vstream, chrLen);
            CheckTrue("e2e view: chr_info2 relayed, md5 ok", chr != null && U32(chr, 0) == (uint)chrLen && HashOk(chr, chrLen));
            CheckTrue("e2e: data bound", s.Data != null);

            vstream.Write(Request(0x1F));
            var ap = ReadExact(dstream, 5);
            CheckTrue("e2e 0x1F: data socket gets {1,0,0,0,0}", ap != null && ap[0] == 1);

            var sel = Request(0x07);
            BinaryPrimitives.WriteUInt32LittleEndian(sel.AsSpan(28), Char1); PutName(sel, 36, "Vwrenamed");
            dstream.Write(a1); // data re-armed by its previous write; request another list (cached)
            ReadExact(dstream, 0x148); ReadExact(vstream, chrLen);
            vstream.Write(sel);
            var sp = ReadExact(dstream, 5);
            CheckTrue("e2e 0x07: data socket gets {2,0,0,0,0}", sp != null && sp[0] == 2);
            Check("e2e 0x07: requestedCharacterId", Char1, s.RequestedCharacterId);

            // closing both sockets tears the session entry down
            viewC.Close(); dataC.Close();
            for (int i = 0; i < 50 && sessions.GetHashFromPacket(Ip, Request(0x24)) != ""; i++) System.Threading.Thread.Sleep(100);
            Check("e2e: session erased after both sockets close", "", sessions.GetHashFromPacket(Ip, Request(0x24)));
        }

        // ---- ConnectEngine: periodic cleanup + listener wiring
        {
            var clock = new ManualTime(DateTimeOffset.UtcNow);
            var sessions = new LoginSessions(clock);
            var stale = sessions.GetAuthenticatedSession("10.0.0.1", "stale");
            var withView = sessions.GetAuthenticatedSession("10.0.0.1", "withview"); withView.View = new FakeChannel();
            var other = sessions.GetAuthenticatedSession("10.0.0.2", "other");
            sessions.PeriodicCleanup();
            CheckTrue("cleanup: nothing erased before 15 min", sessions.TryGetSession("10.0.0.1", "stale", out _) && sessions.TryGetSession("10.0.0.2", "other", out _));
            clock.Now = clock.Now.AddMinutes(14);
            sessions.PeriodicCleanup();
            CheckTrue("cleanup: nothing erased at 14 min", sessions.TryGetSession("10.0.0.1", "stale", out _));
            clock.Now = clock.Now.AddMinutes(2);
            sessions.PeriodicCleanup();
            CheckTrue("cleanup: socketless entry erased after 15 min", !sessions.TryGetSession("10.0.0.1", "stale", out _));
            CheckTrue("cleanup: entry with a view socket kept", sessions.TryGetSession("10.0.0.1", "withview", out _));
            CheckTrue("cleanup: empty ip bucket removed", !sessions.TryGetSession("10.0.0.2", "other", out _));

            var cfg = new ConnectConfig { Bind = System.Net.IPAddress.Loopback, AuthPort = 0, DataPort = 0, ViewPort = 0 };
            using var engine = new ConnectEngine(cfg, db, sessions, SelfSignedCert.Create(), null, new RecSink(), new AuthSettings(), new LobbySettings(), Vs());
            engine.Start();
            CheckTrue("engine: three distinct listening ports", engine.AuthPort > 0 && engine.DataPort > 0 && engine.ViewPort > 0 &&
                new HashSet<int> { engine.AuthPort, engine.DataPort, engine.ViewPort }.Count == 3);
            foreach (var (label, port) in new[] { ("auth", engine.AuthPort), ("data", engine.DataPort), ("view", engine.ViewPort) })
            {
                using var tc = new System.Net.Sockets.TcpClient();
                bool ok;
                try { tc.Connect(System.Net.IPAddress.Loopback, port); ok = tc.Connected; } catch (Exception) { ok = false; }
                CheckTrue($"engine: {label} port accepts", ok);
            }
        }
        // ---- HandleError cleanup
        {
            var c = NewCtx(db, Vs());
            c.V.Handle(Req(c, 0x24));
            c.V.HandleError();
            CheckTrue("HandleError: view cleared", c.S.View == null);
            CheckTrue("HandleError: entry kept while data remains", c.Sessions.GetHashFromPacket(Ip, Req(c, 0x24)) == c.Key);
            c.D.HandleError();
            // data's own session hash is only set once it handled a packet; bind it explicitly
            c.Sessions.OnDataSessionClosed(Ip, c.Key);
            Check("HandleError: entry erased once both halves gone", "", c.Sessions.GetHashFromPacket(Ip, Req(c, 0x24)));
        }
    }
}

internal sealed class ManualTime : TimeProvider
{
    public DateTimeOffset Now;
    public ManualTime(DateTimeOffset now) { Now = now; }
    public override DateTimeOffset GetUtcNow() => Now;
}
