using System;
using System.Collections.Generic;
using System.IO;
using XI.Common;
using XI.Map;
using XI.Ipc;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Traits.Live -- live gate for job traits (ZoneHost.Traits: traits::LoadTraitsList, battleutils::AddTraits,
// charutils::BuildingCharTraitsTable, blueutils::CalculateTraits, CBattleEntity::addTrait / delTrait / hasTrait, the
// m_TraitList bits in COMMAND_DATA, mobutils' traits and the hasTrait binding) against the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    // a WAR30/THF15 in West Ronfaure, a WAR75 and a BLU75 in their mog houses
    private const uint War = 991100, Merit = 991101, Blu = 991102;
    private static readonly uint[] Ids = { War, Merit, Blu };
    private const ushort WestRonfaure = 100, BastokMines = 234;
    private const uint Rabbit = 17186822;
    private const int DoubleAttack = 15, AttackBonus = 3, DefenseBonus = 4, EvasionBonus = 2, TreasureHunter = 19,
                      ClearMind = 24, Savagery = 71;
    // WAR30 (resist virus 54 at 5, defense bonus, double attack, attack bonus, the SOA 7) and THF15 (gilfinder 20,
    // evasion bonus, treasure hunter)
    private const string WarThf = "2 3 4 7 15 19 20 54";
    private const ushort SavageryMerit = 0x804;
    private const ushort Soporific = 598, PoisonBreath = 536;

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static ResultSet Sql(IDatabase db, string q, params BoundValue[] a) => db.Execute(q, a);

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
        foreach (var id in Ids)
        {
            foreach (var t in CharidTables(db))
                Sql(db, $"DELETE FROM `{t}` WHERE charid = ?", BoundValue.Of(id));
            Sql(db, "DELETE FROM chars WHERE charid = ?", BoundValue.Of(id));
            Sql(db, "DELETE FROM accounts WHERE id = ?", BoundValue.Of(id));
        }
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

    private static bool Bit(byte[] bits, int offset, int id) => (bits[offset + id / 8] & 1 << id % 8) != 0;

    private static string Ids_(BattleEntity e)
    {
        var ids = new SortedSet<int>();
        foreach (var t in e.TraitList) ids.Add(t.Id);
        return string.Join(" ", ids);
    }

    private static void AddChar(IDatabase db, uint id, string name, ushort zone, bool moghouse, string statsRow, string jobs)
    {
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of("trt" + name), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,moghouse,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(name), BoundValue.Of(zone), BoundValue.Of((ushort)0),
            BoundValue.Of(0.0f), BoundValue.Of(0.0f), BoundValue.Of(0.0f), BoundValue.Of(moghouse ? id : 0u), BoundValue.Of((byte)1), BoundValue.Of((byte)0));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, $"REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl, sjob, slvl) VALUES (?, 300, 50, {statsRow})", BoundValue.Of(id));
        Sql(db, $"REPLACE INTO char_jobs SET charid = ?, unlocked = ?, {jobs}", BoundValue.Of(id), BoundValue.Of(0x3FFFFEu));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(id));
    }

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var growth = GrowthTables.Load(db);
        var stats = new CharStats(growth, new ItemData(db), new StatSettings());

        // ---- traits::LoadTraitsList ----
        var table = TraitTable.Load(db, _ => true);
        var war = table.ForJob(1);
        var da = war == null ? null : new List<Trait>(war).Find(t => t.Id == DoubleAttack && t.Rank == 1);
        Check("WAR's double attack I: level 25, DOUBLE_ATTACK +10", "25/DOUBLE_ATTACK/10/0", da == null ? "-" : $"{da.Level}/{da.Mod}/{da.Value}/{da.MeritId}");
        var sav = war == null ? null : new List<Trait>(war).Find(t => t.Id == Savagery);
        Check("WAR's savagery: a merit trait (2052) at 75", "75/2052", sav == null ? "-" : $"{sav.Level}/{sav.MeritId}");
        var blu = new List<Trait>(table.ForJob(16) ?? new List<Trait>());
        var clear = blu.Find(t => t.Id == ClearMind && t.Level == 0 && t.Rank == 1);
        Check("BLU's list carries the blue traits: clear mind I, category 4 at 2 points", "4/2/MPHEAL/3",
              clear == null ? "-" : $"{clear.Category}/{clear.Points}/{clear.Mod}/{clear.Value}");
        var noTagTable = TraitTable.Load(db, _ => false);
        Check("a content tag left off drops its rows: no WAR double attack III (ABYSSEA)", "True/False",
              $"{new List<Trait>(war).Exists(t => t.Id == DoubleAttack && t.Rank == 3)}/{new List<Trait>(noTagTable.ForJob(1)).Exists(t => t.Id == DoubleAttack && t.Rank == 3)}");
        var outOfRange = new List<string>();
        Check("MON (23) has a list; a job past MAX_JOBTYPE (24) none, logged", "False/True/1",
              $"{table.ForJob(23, outOfRange.Add) == null}/{table.ForJob(24, outOfRange.Add) == null}/{outOfRange.Count}");

        AddChar(db, War, "Traity", WestRonfaure, false, "1, 30, 6, 15", "war = 30, thf = 30, mnk = 30");
        AddChar(db, Merit, "Meritty", BastokMines, true, "1, 75, 0, 1", "war = 75");
        Sql(db, "REPLACE INTO char_exp(charid, war, merits, limits, mode) VALUES (?, 0, 30, 0, 0)", BoundValue.Of(Merit));
        Sql(db, "INSERT INTO char_merit(charid, meritid, upgrades) VALUES (?, ?, 1)", BoundValue.Of(Merit), BoundValue.Of(SavageryMerit));
        AddChar(db, Blu, "Bluey", BastokMines, true, "16, 75, 0, 1", "blm = 1, blu = 75");
        foreach (ushort spell in new[] { Soporific, PoisonBreath })
            Sql(db, "INSERT INTO char_spells(charid, spellid) VALUES (?, ?)", BoundValue.Of(Blu), BoundValue.Of(spell));
        var saved = new byte[20];
        saved[0] = (byte)(Soporific - 0x200); saved[1] = (byte)(PoisonBreath - 0x200);
        Sql(db, "UPDATE chars SET set_blue_spells = ? WHERE charid = ?", BoundValue.Of(saved), BoundValue.Of(Blu));

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, AbilityTable.Load(db, _ => true), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        zone.VanaTicksNow = () => 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        zone.MobRng = new Random(7);
        zone.SendIpc = _ => { };
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);

        var sessions = new Dictionary<uint, MapSession>();
        void Send(uint id, ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(sessions[id], opcode, new BasicPacket(buf));
            zone.AfterParse(sessions[id]);
        }
        CharData Login(uint id)
        {
            var s = sessions[id] = new MapSession { CharId = id, AccountId = id };
            zone.OnLogin(s);
            Send(id, 0x00A, 92, buf => BitConverter.GetBytes(id).CopyTo(buf, 12));
            Send(id, 0x00C, 12);
            return (CharData)s.Char;
        }
        List<BasicPacket> Of(uint id, int opcode) => new List<BasicPacket>(sessions[id].PacketList).FindAll(p => Opcode(p) == opcode);
        var lua = scripts.Lua;
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(uint id, string code)
        {
            got.Clear();
            sessions[id].PacketList.Clear();
            lua.DoString($"local player = GetPlayerByID({id})\nlocal mob = GetMobByID({Rabbit})\n" + code, "=verify_traits");
            return string.Join(",", got);
        }
        bool LastCommandBit(uint id, int traitId)
        {
            var p = Of(id, 0x0AC);
            return p.Count > 0 && Bit(p[^1].Buffer, 196, traitId);
        }

        // ---- the WAR30/THF15 ----
        Console.WriteLine("WAR30/THF15: BuildingCharTraitsTable at load");
        var c = Login(War);
        Check("logged in WAR30/THF15", "1/30/6/15", $"{c.MJob}/{c.MLvl}/{c.SJob}/{c.SLvl}");
        Check("the traits: WAR30's, and THF15's at the half level", WarThf, Ids_(c));
        Check("double attack's mod in: DOUBLE_ATTACK 10", 10, c.GetMod(Mod.DOUBLE_ATTACK));
        Check("treasure hunter 1, attack +10, defense +10, evasion +10", "1/10/10/10",
              $"{c.GetMod(Mod.TREASURE_HUNTER)}/{c.GetMod(Mod.ATT)}/{c.GetMod(Mod.DEF)}/{c.GetMod(Mod.EVA)}");
        int meva30 = growth.GetMaxSkillByRank(12, 30);
        Check("MEVA: rank G at 30 (m_magicEvasion)", $"{meva30}/{meva30}", $"{c.TraitMagicEvasion}/{c.GetMod(Mod.MEVA)}");
        Check("the m_TraitList bits: 2, 3, 4, 15, 19 set, 16 not", "True/True/True/True/True/False",
              $"{Bit(c.Traits, 0, 2)}/{Bit(c.Traits, 0, 3)}/{Bit(c.Traits, 0, 4)}/{Bit(c.Traits, 0, 15)}/{Bit(c.Traits, 0, 19)}/{Bit(c.Traits, 0, 16)}");
        var cmd = LoginPackets.CommandData(c);
        Check("COMMAND_DATA (0x0AC) carries the bits at 196", "True/True/False",
              $"{Bit(cmd.Buffer, 196, DoubleAttack)}/{Bit(cmd.Buffer, 196, TreasureHunter)}/{Bit(cmd.Buffer, 196, 16)}");
        Check("hasTrait from Lua: double attack, treasure hunter, not triple attack", "true,true,false",
              Lua(War, "report(tostring(player:hasTrait(xi.trait.DOUBLE_ATTACK))) report(tostring(player:hasTrait(19))) report(tostring(player:hasTrait(xi.trait.TRIPLE_ATTACK)))"));
        Check("no canApplyTrait failure logged", false, log.Exists(l => l.Contains("canApplyTrait")));

        Console.WriteLine("setLevel / changeJob / changesJob rebuild them");
        Lua(War, "player:setLevel(28)");
        Check("WAR28/THF14: treasure hunter (15) gone at the half level, double attack kept", "28/14/False/True/10/0",
              $"{c.MLvl}/{c.SLvl}/{c.HasTrait(TreasureHunter)}/{c.HasTrait(DoubleAttack)}/{c.GetMod(Mod.DOUBLE_ATTACK)}/{c.GetMod(Mod.TREASURE_HUNTER)}");
        Check("its COMMAND_DATA: 15 set, 19 clear", "True/False", $"{LastCommandBit(War, DoubleAttack)}/{LastCommandBit(War, TreasureHunter)}");
        Lua(War, "player:setLevel(20)");
        Check("WAR20: no double attack, its mod and bit out, attack bonus out", "False/0/False/False/0",
              $"{c.HasTrait(DoubleAttack)}/{c.GetMod(Mod.DOUBLE_ATTACK)}/{Bit(c.Traits, 0, DoubleAttack)}/{c.HasTrait(AttackBonus)}/{c.GetMod(Mod.ATT)}");
        Check("MEVA follows the level (no stacking)", growth.GetMaxSkillByRank(12, 20), c.GetMod(Mod.MEVA));
        Check("hasTrait from Lua: false", "false", Lua(War, "report(tostring(player:hasTrait(xi.trait.DOUBLE_ATTACK)))"));
        Lua(War, "player:setLevel(30)");
        Check("WAR30 again: double attack and treasure hunter back, the mods once", "True/True/10/1",
              $"{c.HasTrait(DoubleAttack)}/{c.HasTrait(TreasureHunter)}/{c.GetMod(Mod.DOUBLE_ATTACK)}/{c.GetMod(Mod.TREASURE_HUNTER)}");
        Lua(War, "player:changeJob(xi.job.MNK)");
        Check("MNK30/THF15: no WAR traits, treasure hunter kept", "2/False/False/True/0",
              $"{c.MJob}/{c.HasTrait(DoubleAttack)}/{c.HasTrait(DefenseBonus)}/{c.HasTrait(TreasureHunter)}/{c.GetMod(Mod.DOUBLE_ATTACK)}");
        Check("its COMMAND_DATA: 15 clear", false, LastCommandBit(War, DoubleAttack));
        Lua(War, "player:changesJob(xi.job.WAR)");
        Check("MNK30/WAR15: defense bonus (10) in, double attack (25) not, treasure hunter out", "1/15/True/False/False",
              $"{c.SJob}/{c.SLvl}/{c.HasTrait(DefenseBonus)}/{c.HasTrait(DoubleAttack)}/{c.HasTrait(TreasureHunter)}");
        Lua(War, "player:changeJob(xi.job.WAR) player:changesJob(xi.job.THF)");
        Check("back to WAR30/THF15", $"{WarThf}/10", $"{Ids_(c)}/{c.GetMod(Mod.DOUBLE_ATTACK)}");
        Lua(War, "player:recalculateAbilitiesTable()");
        Check("recalculateAbilitiesTable: the same traits, the mods not stacked", $"{WarThf}/10/{meva30}",
              $"{Ids_(c)}/{c.GetMod(Mod.DOUBLE_ATTACK)}/{c.GetMod(Mod.MEVA)}");

        // ---- a mob: mobutils::CalculateMobStats' traits ----
        Console.WriteLine("the Wild Rabbit (mobutils)");
        var rabbit = zone.ZoneInfoOf(WestRonfaure).MobById[Rabbit];
        var expected = new SortedSet<int>();
        foreach (var (job, lvl) in new[] { (rabbit.MJob, rabbit.MLvl), (rabbit.SJob, rabbit.SLvl) })
            foreach (var t in table.ForJob(job) ?? new List<Trait>())
                if (t.Level > 0 && lvl >= t.Level) expected.Add(t.Id);
        Check($"spawned WAR{rabbit.MLvl}/WAR{rabbit.SLvl}: its jobs' traits (none under 5)", $"1/1/{string.Join(" ", expected)}",
              $"{rabbit.MJob}/{rabbit.SJob}/{Ids_(rabbit)}");
        int before = rabbit.GetMod(Mod.DOUBLE_ATTACK);
        Check("setMobLevel(30): double attack, hasTrait from Lua", $"true,{before + 10}",
              Lua(War, "mob:setMobLevel(30) report(tostring(mob:hasTrait(xi.trait.DOUBLE_ATTACK))) report(mob:getMod(xi.mod.DOUBLE_ATTACK))"));
        Check("its traits: WAR30's, less resist virus (54: canApplyTrait keeps resist traits to beastmen)", "3 4 7 15", Ids_(rabbit));
        Check("the mob's traits list: no bit field", "True", rabbit.HasTrait(DoubleAttack));

        // ---- merit traits ----
        Console.WriteLine("WAR75 with one savagery merit (in its mog house)");
        var m = Login(Merit);
        Check("savagery (merit 2052, 1 upgrade) in", "1/True/True", $"{m.Merits.Count(SavageryMerit)}/{m.HasTrait(Savagery)}/{Bit(m.Traits, 0, Savagery)}");
        Check("hasTrait(71) from Lua", "true", Lua(Merit, "report(tostring(player:hasTrait(71)))"));
        void MeritPacket(byte param1) => Send(Merit, 0x0BE, 12, buf =>
        {
            buf[4] = 3; buf[5] = param1;
            BitConverter.GetBytes((ushort)(SavageryMerit >> 1)).CopyTo(buf, 6);
        });
        sessions[Merit].PacketList.Clear();
        MeritPacket(0);
        Check("0x0BE lowers it: savagery out, the bit cleared", "0/False/False/False", $"{m.Merits.Count(SavageryMerit)}/{m.HasTrait(Savagery)}/{Bit(m.Traits, 0, Savagery)}/{LastCommandBit(Merit, Savagery)}");
        Check("the other WAR75 traits kept", "True/True", $"{m.HasTrait(DoubleAttack)}/{m.HasTrait(DefenseBonus)}");
        sessions[Merit].PacketList.Clear();
        MeritPacket(1);
        Check("0x0BE raises it again: savagery in", "1/True/True", $"{m.Merits.Count(SavageryMerit)}/{m.HasTrait(Savagery)}/{LastCommandBit(Merit, Savagery)}");
        Lua(Merit, "player:setLevel(74)");
        Check("WAR74: savagery (75) out with the merit kept", "1/False", $"{m.Merits.Count(SavageryMerit)}/{m.HasTrait(Savagery)}");

        // ---- blue magic traits ----
        Console.WriteLine("BLU75 with soporific and poison breath set (clear mind, category 4)");
        var b = Login(Blu);
        Check("both spells set", $"{Soporific - 0x200}/{PoisonBreath - 0x200}", $"{b.SetBlueSpells[0]}/{b.SetBlueSpells[1]}");
        Check("2 points of category 4: clear mind I, MPHEAL +3", "True/3/True", $"{b.HasTrait(ClearMind)}/{b.GetMod(Mod.MPHEAL)}/{Bit(b.Traits, 0, ClearMind)}");
        Check("hasTrait(24) from Lua", "true", Lua(Blu, "report(tostring(player:hasTrait(24)))"));
        void Ext(byte first, int slot, byte value) => Send(Blu, 0x102, 164, buf => { buf[4] = first; buf[8] = 16; buf[12 + slot] = value; });
        sessions[Blu].PacketList.Clear();
        Ext(0, 1, (byte)(PoisonBreath - 0x200));
        Check("0x102 takes poison breath off: 1 point, clear mind out, MPHEAL 0", "0/False/0",
              $"{b.SetBlueSpells[1]}/{b.HasTrait(ClearMind)}/{b.GetMod(Mod.MPHEAL)}");
        sessions[Blu].PacketList.Clear();
        Ext((byte)(PoisonBreath - 0x200), 1, (byte)(PoisonBreath - 0x200));
        Check("0x102 sets it again: clear mind back, MPHEAL +3 once", $"{PoisonBreath - 0x200}/True/3",
              $"{b.SetBlueSpells[1]}/{b.HasTrait(ClearMind)}/{b.GetMod(Mod.MPHEAL)}");
        Check("the set's COMMAND_DATA follows the rebuild", "True/True", $"{Of(Blu, 0x0AC).Count == 1}/{LastCommandBit(Blu, ClearMind)}");
        Check("nothing failed in Lua", "", string.Join(" | ", log.FindAll(l => (l.Contains("rror") && !l.Contains(" 0 load errors")) || l.Contains("canApplyTrait"))));
    }
}
