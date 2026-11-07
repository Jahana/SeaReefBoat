using System;
using System.Collections.Generic;
using System.IO;
using XI.Ipc;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.ChocoboRace.Live -- live gate for c2s 0x09B CHOCOBO_RACE_REQ (ZoneHost.ChocoboRace): the toteboard odds
// (s2c 0x073) and a race's field (s2c 0x074 modes 1-4) from xi.chocoboRacing.onRaceRequest, and the race globals the
// scripts use (Save/LoadChocoboRaceResult, Add/GetChocoboRaceQuills) against the real MariaDB (xidb) race tables.
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    // a racegoer in Chocobo Circuit, one in Phanauet Channel
    private static readonly (uint Id, string Name, ushort Zone)[] Chars = { (990991, "Racegoer", 70), (990992, "Elsewhere", 1) };

    private const long Now = 1_800_000_000;
    private static readonly int[] GradeCycle = { 15, 14, 14, 13, 15, 14, 13, 12 };

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static ResultSet Sql(IDatabase db, string q, params BoundValue[] a) => db.Execute(q, a);

    private static string Scalar(IDatabase db, string q, params BoundValue[] a)
    {
        var r = db.Execute(q, a);
        return r != null && r.Next() ? r.Get<string>(0u) : null;
    }

    // xi.chocoboRacing.raceNoForColor at Now: the newest race on the board less the colors back to this one
    private static long RaceNoForColor(int color)
    {
        long newest = (Now - 47) / 900 - 1848287 + 2;
        return newest - ((newest % 4 - color) % 4 + 4) % 4;
    }

    // every table with a charid column, read from the schema at run time: chars' char_insert trigger fills char_equip, char_exp,
    // char_history, char_inventory, char_jobs, char_pet, char_points, char_profile, char_storage and char_unlocks and only
    // char_delete clears them, so a run aborted with one of them left behind would make the next run's INSERT INTO chars collide
    private static List<string> CharidTables(IDatabase db)
    {
        var tables = new List<string>();
        var r = Sql(db, "SELECT table_name AS t FROM information_schema.columns WHERE table_schema = DATABASE() AND column_name = 'charid' AND table_name <> 'chars'");
        while (r != null && r.Next()) tables.Add(r.Get<string>("t"));
        foreach (var t in new[] { "char_equip", "char_exp", "char_history", "char_inventory", "char_jobs", "char_pet", "char_points", "char_profile",
                                  "char_storage", "char_unlocks" })
            if (!tables.Contains(t)) tables.Add(t);
        return tables;
    }

    private static void Cleanup(IDatabase db)
    {
        foreach (var (id, _, _) in Chars)
        {
            foreach (var t in CharidTables(db))
                Sql(db, $"DELETE FROM `{t}` WHERE charid = ?", BoundValue.Of(id));
            Sql(db, "DELETE FROM chars WHERE charid = ?", BoundValue.Of(id));
            Sql(db, "DELETE FROM accounts WHERE id = ?", BoundValue.Of(id));
        }
        for (int color = 0; color < 4; color++)
            Sql(db, "DELETE FROM chocobo_race_results WHERE race_no = ?", BoundValue.Of((uint)RaceNoForColor(color)));
    }

    private static string FindDataDir()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var cand = Path.Combine(d.FullName, "data");
            if (File.Exists(Path.Combine(cand, "grades.yaml"))) return cand;
        }
        throw new DirectoryNotFoundException("data (grades.yaml) not found above " + AppContext.BaseDirectory);
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
            Run(db);
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FAIL exception: {ex}");
        }
        finally
        {
            Cleanup(db);
        }

        Console.WriteLine($"{_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    private static int Opcode(BasicPacket p) => p.Buffer[0] | (p.Buffer[1] & 1) << 8;

    // lane i's ChocoboParam as ZoneHost packs it
    private static string LaneHex(int i)
    {
        var b = new byte[12];
        b[0] = (byte)(i & 0xF | (9 - i) << 4);
        b[4] = (byte)(16 * i); b[5] = (byte)(16 * i + 1); b[6] = (byte)(16 * i + 2); b[7] = (byte)(16 * i + 3);
        uint dna = (uint)i << 9 | (uint)(15 - i) << 13 | (uint)((i + 1) % 8) << 17 | (uint)i << 20 | (uint)(i % 2) << 24
                 | (uint)((i + 2) % 8) << 25 | (uint)(i % 8) << 28;
        BitConverter.GetBytes(dna).CopyTo(b, 8);
        return Convert.ToHexString(b);
    }

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
        var loader = new CharLoader(db, Grades.Load(dataDir), AbilityTable.Load(db, _ => true), stats);
        var log = new List<string>();
        var zone = new ZoneHost(db, loader, dataDir, true, log.Add);
        zone.SendIpc = _ => { };
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);
        var lua = scripts.Lua;
        lua.SetGlobal("GetSystemTime", l => { l.Push((double)Now); return 1; });
        string LuaStr(string code)
        {
            int top = lua.Top;
            lua.DoString(code, "=test", 1);
            string v = lua.ToStringValue(-1);
            lua.SetTop(top);
            return v;
        }

        foreach (var (id, name, z) in Chars)
        {
            Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of("cr" + name), BoundValue.Of("x"), BoundValue.Of((byte)1));
            Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,moghouse,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(name), BoundValue.Of(z), BoundValue.Of((ushort)0),
                BoundValue.Of(0u), BoundValue.Of((byte)0), BoundValue.Of((byte)0));
            Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(0u), BoundValue.Of((ushort)0));
            Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl, sjob, slvl) VALUES (?, 300, 50, 1, 30, 0, 0)", BoundValue.Of(id));
            Sql(db, "REPLACE INTO char_jobs SET charid = ?, unlocked = 2, war = 30", BoundValue.Of(id));
            Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(id));
        }

        var ss = new MapSession[Chars.Length];
        var cs = new CharData[Chars.Length];
        void Send(int i, ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(ss[i], opcode, new BasicPacket(buf));
            zone.AfterParse(ss[i]);
        }
        List<BasicPacket> Of(int i, int opcode) => new List<BasicPacket>(ss[i].PacketList).FindAll(p => Opcode(p) == opcode);
        void Clear() { foreach (var s in ss) s?.PacketList.Clear(); log.Clear(); }
        bool Logged(string text) => log.Exists(l => l.Contains(text));
        void Login(int i)
        {
            ss[i] = new MapSession { CharId = Chars[i].Id, AccountId = Chars[i].Id };
            zone.OnLogin(ss[i]);
            cs[i] = (CharData)ss[i].Char;
            Send(i, 0x00A, 92, buf => BitConverter.GetBytes(Chars[i].Id).CopyTo(buf, 12));
            Send(i, 0x00C, 12);
        }
        // GP_CLI_COMMAND_CHOCOBO_RACE_REQ: Param u32 @4, Kind u32 @8
        void Req(int i, uint param, uint kind) => Send(i, 0x09B, 12, buf =>
        {
            BitConverter.GetBytes(param).CopyTo(buf, 4);
            BitConverter.GetBytes(kind).CopyTo(buf, 8);
        });
        string Modes(int i) => string.Join(",", Of(i, 0x074).ConvertAll(p => BitConverter.ToUInt16(p.Buffer, 16).ToString()));
        BasicPacket Mode(int i, int mode) => Of(i, 0x074).Find(p => BitConverter.ToUInt16(p.Buffer, 16) == mode);

        Login(0);
        Login(1);
        Check("login: zones", "70/1", $"{cs[0].Zone}/{cs[1].Zone}");
        Check("race scripts loaded", "function", LuaStr("return type(xi.chocoboRacing.onRaceRequest)"));

        // ---- SaveChocoboRaceResult / LoadChocoboRaceResult ----
        long race1 = RaceNoForColor(1);
        lua.DoString($@"
            local chocobos = {{}}
            for i = 1, 8 do
                chocobos[i] = {{
                    name = 'Bird' .. i, orders = i, item = 9 - i, size = i % 8, color = (i + 2) % 8, gender = i % 2,
                    weather = i, temperament = (i + 1) % 8, ability1 = i, ability2 = 15 - i,
                    stats = {{ str = 16 * i, ['end'] = 16 * i + 1, dsc = 16 * i + 2, rcp = 16 * i + 3 }},
                }}
            end
            chocobos[1].name = 'Abcdefghijklmnopq'
            local sections = {{}}
            for s = 1, 32 do
                sections[s] = {{ from = {{ 1, 2, 3, 4, 5, 6, 7, s % 16 }}, to = {{ 8, 7, 6, 5, 4, 3, 2, 1 }},
                                 trigger = {{ user = s, targets = 2, param = 3, type = 0x20 }} }}
            end
            SaveChocoboRaceResult({{ raceNo = {race1}, weather = 6, runAt = 1234, places = {{ 7, 6, 5, 4, 3, 2, 1, 0 }},
                                     intro = {{ 65, 9 }}, chocobos = chocobos, sections = sections }})", "=save");
        Check("save: a row", "6/1234/65/9", Scalar(db, "SELECT CONCAT(weather,'/',run_at,'/',intro_first,'/',intro_second) FROM chocobo_race_results WHERE race_no = ?", BoundValue.Of((uint)race1)));
        Check("save: places packed a nibble each", 0x01234567u.ToString(), Scalar(db, "SELECT places FROM chocobo_race_results WHERE race_no = ?", BoundValue.Of((uint)race1)));
        Check("save: blob sizes", "96/128/384", Scalar(db, "SELECT CONCAT(LENGTH(chocobos),'/',LENGTH(names),'/',LENGTH(sections)) FROM chocobo_race_results WHERE race_no = ?", BoundValue.Of((uint)race1)));
        Check("save: lane 1 ChocoboParam", "8100000010111213" + "00C21517", Scalar(db, "SELECT HEX(SUBSTR(chocobos,1,12)) FROM chocobo_race_results WHERE race_no = ?", BoundValue.Of((uint)race1)));
        Check("save: lane 1 ChocoboParam (formula)", LaneHex(1), "8100000010111213" + "00C21517");
        Check("save: section 2 nibbles and trigger", "2143652778563412" + "02020320", Scalar(db, "SELECT HEX(SUBSTR(sections,13,12)) FROM chocobo_race_results WHERE race_no = ?", BoundValue.Of((uint)race1)));
        Check("load: the race read back", "Abcdefghijklmno,49,7,3,2,32,9,10,6,1234",
            LuaStr($@"local r = LoadChocoboRaceResult({race1})
                return table.concat({{ r.chocobos[1].name, r.chocobos[3].stats['end'], r.places[1], r.sections[2].from[3], r.sections[2].from[8],
                                     r.sections[2].trigger.type, r.intro[2], r.chocobos[5].ability2, r.weather, r.runAt }}, ',')"));
        Check("load: a race not posted is nil", "nil", LuaStr($"return tostring(LoadChocoboRaceResult({RaceNoForColor(3)}))"));

        // ---- ChocoboList: the posted race of color 1 ----
        Clear();
        Req(0, 1, 2);
        Check("list: modes 1-4", "1,2,3,4", Modes(0));
        var race = Mode(0, 1);
        Check("list: 0x074 size 0xB4", 0xB4, (race.Buffer[1] >> 1) * 4);
        Check("list RACE: weather packed, ParamSize 8", $"{(6 << 5) + 8}/{0xC0000000}/8",
            $"{BitConverter.ToUInt32(race.Buffer, 20)}/{BitConverter.ToUInt32(race.Buffer, 24)}/{BitConverter.ToUInt16(race.Buffer, 18)}");
        var chocos = Mode(0, 2);
        Check("list CHOCOBOS: ParamSize 96", 96, BitConverter.ToUInt16(chocos.Buffer, 18));
        bool lanesOk = true;
        for (int i = 1; i <= 8; i++) lanesOk &= Convert.ToHexString(chocos.Buffer, 20 + (i - 1) * 12, 12) == LaneHex(i);
        Check("list CHOCOBOS: all 8 lanes", true, lanesOk);
        var names = Mode(0, 3);
        string Name(int i) => System.Text.Encoding.ASCII.GetString(names.Buffer, 20 + i * 20 + 4, 16).TrimEnd('\0');
        Check("list NAMES: ParamSize 160, 15 chars at most", "160/Abcdefghijklmno/Bird8", $"{BitConverter.ToUInt16(names.Buffer, 18)}/{Name(0)}/{Name(7)}");
        Check("list END: ParamSize 0, SlotIndex 0", "0/0", $"{BitConverter.ToUInt16(Mode(0, 4).Buffer, 18)}/{BitConverter.ToUInt32(Mode(0, 4).Buffer, 4)}");

        // ---- ChocoboList: a race not posted yet sends sunshine and no field ----
        Clear();
        Req(0, 3, 2);
        Check("list unposted: RACE and END only", "1,4", Modes(0));
        Check("list unposted: sunshine", (1 << 5) + 8, BitConverter.ToUInt32(Mode(0, 1).Buffer, 20));

        // ---- Toteboard: the odds from the quills bet ----
        lua.DoString($"AddChocoboRaceQuills({race1}, 0, 20) AddChocoboRaceQuills({race1}, 0, 10) AddChocoboRaceQuills({race1}, 27, 10) AddChocoboRaceQuills({race1}, 28, 5)", "=bet");
        Check("quills: added up, pair 28 refused", "30/10/2", Scalar(db, "SELECT CONCAT(SUM(IF(pair=0,quills,0)),'/',SUM(IF(pair=27,quills,0)),'/',COUNT(*)) FROM chocobo_race_quills WHERE race_no = ?", BoundValue.Of((uint)race1)));
        Check("quills: the bad pair warned", true, Logged("AddChocoboRaceQuills: pair 28 is not on the toteboard"));
        Check("quills: read back", "30,0,10", LuaStr($"local q = GetChocoboRaceQuills({race1}) return q[1] .. ',' .. q[2] .. ',' .. q[28]"));
        Clear();
        Req(0, 1, 1);
        var board = Of(0, 0x073);
        Check("toteboard: one 0x073 of size 0x48", "1/72", $"{board.Count}/{(board.Count > 0 ? (board[0].Buffer[1] >> 1) * 4 : 0)}");
        var b = board[0];
        uint ident = (uint)GradeCycle[race1 % 8] << 18 | (uint)(race1 % 0x40000);
        Check("toteboard: SlotIndex is Param, Ident grade and race", $"1/{ident}", $"{BitConverter.ToUInt32(b.Buffer, 4)}/{BitConverter.ToUInt32(b.Buffer, 8)}");
        Check("toteboard: odds 75% of the pool over the pair, 50000 unbet", "100/50000/300",
            $"{BitConverter.ToUInt16(b.Buffer, 16)}/{BitConverter.ToUInt16(b.Buffer, 18)}/{BitConverter.ToUInt16(b.Buffer, 16 + 27 * 2)}");

        // ---- an empty board sends nothing; the validation ----
        Clear();
        Req(0, 4, 1);
        Req(0, 4, 2);
        Check("param past the colors: an empty board, nothing sent", "0/0", $"{Of(0, 0x073).Count}/{Of(0, 0x074).Count}");
        Clear();
        Req(1, 1, 1);
        Check("outside Chocobo Circuit refused", "True/0", $"{Logged("Not in Chocobo Circuit")}/{Of(1, 0x073).Count}");
        Clear();
        Req(0, 1, 3);
        Check("a bad kind refused", "True/0/0", $"{Logged("3 not a valid ChocoboRaceKind value")}/{Of(0, 0x073).Count}/{Of(0, 0x074).Count}");

        // ---- postRace: the script draws a field and saves it through SaveChocoboRaceResult ----
        long race2 = RaceNoForColor(2);
        lua.DoString($"xi.chocoboRacing.postRace({race2})", "=post");
        Check("postRace: saved", "1", Scalar(db, "SELECT COUNT(*) FROM chocobo_race_results WHERE race_no = ?", BoundValue.Of((uint)race2)));
        Clear();
        Req(0, 2, 2);
        Check("posted field: modes 1-4", "1,2,3,4", Modes(0));
        bool named = true;
        names = Mode(0, 3);
        for (int i = 0; i < 8; i++) named &= names != null && Name(i).Length > 0;
        Check("posted field: 8 named entrants", true, named);

        // ---- sendChocoboRace: the broadcast's 0x069 modes ----
        Clear();
        lua.DoString($"local p = GetPlayerByID({Chars[0].Id}) " +
            "local sections = {} for i = 1, 18 do sections[i] = { from = { i % 16, 1 }, to = { 2, 3 }, trigger = { user = 1, targets = 2, param = 3, type = 0x20 } } end " +
            "p:sendChocoboRace({ counter = 6, weather = 4, chocobos = { { orders = 2, item = 3, stats = { str = 9 } }, { size = 5 } }, sections = sections, places = { 1, 0 } })", "=race");
        var racing = Of(0, 0x069);
        string R(int i, int off) => racing[i].Buffer[off].ToString();
        Check("0x069: modes 1, 2, 3 twice, 4, 5, each 0xC8", "1,2,3,3,4,5/True",
            $"{string.Join(",", racing.ConvertAll(p => p.Buffer[4]))}/{racing.TrueForAll(p => (p.Buffer[1] >> 1) * 4 == 0xC8)}");
        if (racing.Count == 6)
        {
            Check("mode 1: weather 4 << 5 | 2 entrants, the high bit and counter 6 & 3", $"8/{4 << 5 | 2}/{0x80000002:X8}",
                $"{R(0, 6)}/{BitConverter.ToUInt32(racing[0].Buffer, 8)}/{BitConverter.ToUInt32(racing[0].Buffer, 12):X8}");
            Check("mode 2: 24 bytes, orders | item << 4, str, the size bits", $"24/{2 | 3 << 4}/9/{5u << 28}",
                $"{R(1, 6)}/{R(1, 8)}/{R(1, 12)}/{BitConverter.ToUInt32(racing[1].Buffer, 8 + 12 + 8)}");
            Check("mode 3: 16 sections at 0, 2 at 16; from packed, the trigger", $"0/192/16/24/{1 << 4 | 1}/1,2,3,32",
                $"{R(2, 5)}/{R(2, 6)}/{R(3, 5)}/{R(3, 6)}/{R(2, 8)}/{R(2, 16)},{R(2, 17)},{R(2, 18)},{R(2, 19)}");
            Check("mode 4: the places packed", $"4/{0 << 4 | 1}", $"{R(4, 6)}/{R(4, 8)}");
        }
        Check("no script errors", "", string.Join(" | ", log.FindAll(l => l.StartsWith("lua:"))));
    }
}
