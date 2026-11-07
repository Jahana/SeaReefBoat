using System;
using System.Collections.Generic;
using System.Linq;
using XI.Common;
using XI.Map;
using XI.Persistence;
using XI.Quests;
using XI.Script;
using XI.Zone;

// The zone side of XI.Verify.QuestState.Live: a zone host on the real database with its script engine, the char's
// login (0x00A / 0x00C) and logout, Lua run as the char, the host's log, and reads of the char's rows and legacy columns.
internal static partial class Program
{
    private static IDatabase db;
    private static ZoneHost zone;
    private static CharLoader loader;
    private static ScriptEngine scripts;
    private static LuaState lua;
    private static readonly List<string> log = new();
    private static readonly List<string> got = new();
    private static MapSession s;
    private static CharData c;
    private static uint CharId;
    private static long unixOffset;

    private static System.IO.DirectoryInfo Root()
    {
        for (var d = new System.IO.DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (System.IO.File.Exists(System.IO.Path.Combine(d.FullName, "data", "grades.yaml"))) return d;
        throw new System.IO.DirectoryNotFoundException("data (grades.yaml) not found above " + AppContext.BaseDirectory);
    }

    private static void Setup(IDatabase database)
    {
        db = database;
        string dataDir = System.IO.Path.Combine(Root().FullName, "data");
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
        loader = new CharLoader(db, Grades.Load(dataDir), AbilityTable.Load(db, _ => true, 1.0), stats);
        zone = new ZoneHost(db, loader, dataDir, true, log.Add);
        zone.UnixNow = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds() + unixOffset;
        zone.SendIpc = _ => { };
        scripts = new ScriptEngine(Root().FullName, log.Add, log.Add);
        zone.InitScripts(scripts);
        lua = scripts.Lua;
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds() + unixOffset;

    private static string Lua(string code)
    {
        got.Clear();
        lua.DoString($"local player = GetPlayerByID({CharId})\n" + code, "=verify_queststate");
        return string.Join(",", got);
    }

    private static void Send(ushort opcode, int size, Action<byte[]> fill = null)
    {
        var buf = new byte[size];
        buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
        fill?.Invoke(buf);
        zone.Dispatch(s, opcode, new BasicPacket(buf));
        zone.AfterParse(s);
    }

    private static void Login()
    {
        Logout();
        s = new MapSession { CharId = CharId, AccountId = CharId };
        zone.OnLogin(s);
        c = (CharData)s.Char;
        Send(0x00A, 92, buf => BitConverter.GetBytes(CharId).CopyTo(buf, 12));
        Send(0x00C, 12);
        s.PacketList.Clear();
    }

    private static void Logout()
    {
        if (s != null) zone.OnCharTimeout(s, false);
        s = null;
        c = null;
    }

    // the host's "progress:" lines since the mark
    private static int mark_;
    private static void Mark() => mark_ = log.Count;
    private static int CountLines(string prefix) => log.Skip(mark_).Count(l => l.StartsWith(prefix));
    private static string ProgressLines() => string.Join(" | ", log.Skip(mark_).Where(l => l.StartsWith("progress:")));

    // a Hume Bastokan WAR 10 in Port Bastok past its first login, with these legacy columns and char_vars
    private static void Fixture(uint charId, byte[] quests, byte[] missions, params (string Name, int Value, uint Expiry)[] vars)
    {
        CharId = charId;
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7); // NEW_ADVENTURER: no first-login cutscene
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(charId), BoundValue.Of($"qstate{charId % 1000}"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,titles) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(charId), BoundValue.Of(charId), BoundValue.Of($"Qstate{charId % 1000}"), BoundValue.Of((ushort)236), BoundValue.Of((ushort)236),
            BoundValue.Of(90.0f), BoundValue.Of(-8.772f), BoundValue.Of(29.0f), BoundValue.Of((byte)1), BoundValue.Of((byte)0), BoundValue.Of(titles));
        if (quests != null) Sql(db, "UPDATE chars SET quests = ? WHERE charid = ?", BoundValue.Of(quests), BoundValue.Of(charId));
        if (missions != null) Sql(db, "UPDATE chars SET missions = ? WHERE charid = ?", BoundValue.Of(missions), BoundValue.Of(charId));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(charId), BoundValue.Of(charId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 300, 50, 1, 10)", BoundValue.Of(charId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 10)", BoundValue.Of(charId));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(charId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(charId));
        Sql(db, "INSERT IGNORE INTO char_unlocks(charid) VALUES (?)", BoundValue.Of(charId));
        foreach (var v in vars)
            Sql(db, "REPLACE INTO char_vars(charid, varname, value, expiry) VALUES (?, ?, ?, ?)",
                BoundValue.Of(charId), BoundValue.Of(v.Name), BoundValue.Of(v.Value), BoundValue.Of(v.Expiry));
    }

    private static byte[] DbBlob(string col)
    {
        var r = Sql(db, $"SELECT {col} FROM chars WHERE charid = ?", BoundValue.Of(CharId));
        return r != null && r.Next() && !r.IsNull(col) ? r.GetBlobBytes(col) : Array.Empty<byte>();
    }

    private static string Hex(byte[] b, int n) => Convert.ToHexString(b, 0, Math.Min(n, b.Length));

    // the stored rows' projection == the legacy columns, byte for byte (704 / 1050)
    private static string RowsMatchLegacy()
    {
        var p = CharProgress.FromRows(CharId, QuestStateStore.LoadQuests(db, CharId), QuestStateStore.LoadMissions(db, CharId));
        bool q = p.ProjectQuests().ToHex() == Hex(DbBlob("quests"), 704);
        var mb = DbBlob("missions");
        // a NULL missions column is the loader's default for the char (the char's own log, taken from the rows)
        bool m = p.ProjectMissions().ToHex() == (mb.Length == 0 ? Convert.ToHexString(c.MissionLog, 0, 1050) : Hex(mb, 1050));
        return $"{q}/{m}";
    }

    private static string Var(string name)
    {
        var r = Sql(db, "SELECT value FROM char_vars WHERE charid = ? AND varname = ?", BoundValue.Of(CharId), BoundValue.Of(name));
        return r != null && r.Next() ? r.Get<int>("value").ToString() : "-";
    }

    private static string QuestRow(int area, int id)
    {
        var r = Sql(db, "SELECT status, step, progress FROM char_quest_state WHERE charid = ? AND area = ? AND quest_id = ?",
                    BoundValue.Of(CharId), BoundValue.Of((byte)area), BoundValue.Of((ushort)id));
        if (r == null || !r.Next()) return "-";
        return $"{r.Get<byte>("status")}|{r.Get<int>("step")}|{(r.IsNull("progress") ? "-" : r.Get<string>("progress"))}";
    }

    private static string Key(int area, int id)
    {
        var r = Sql(db, "SELECT quest_key FROM char_quest_state WHERE charid = ? AND area = ? AND quest_id = ?",
                    BoundValue.Of(CharId), BoundValue.Of((byte)area), BoundValue.Of((ushort)id));
        return r != null && r.Next() ? r.Get<string>("quest_key") : "-";
    }

    private static string MissionRow(int area)
    {
        var r = Sql(db, "SELECT current, status, completed_mask, progress FROM char_mission_state WHERE charid = ? AND area = ?",
                    BoundValue.Of(CharId), BoundValue.Of((byte)area));
        if (r == null || !r.Next()) return "-";
        return $"{r.Get<ushort>("current")}|{r.Get<uint>("status")}|{r.Get<ulong>("completed_mask")}|{(r.IsNull("progress") ? "-" : r.Get<string>("progress"))}";
    }

    private static long Count(string table) =>
        Sql(db, $"SELECT COUNT(*) AS n FROM {table} WHERE charid = ?", BoundValue.Of(CharId)) is { } r && r.Next() ? r.Get<long>("n") : -1;
}
