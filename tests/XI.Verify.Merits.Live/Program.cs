using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Map;
using XI.Persistence;
using XI.Zone;

// XI.Verify.Merits.Live -- live gate for merits and job points (ZoneHost.Merits: c2s 0x0BE MERITS, 0x0BF
// JOB_POINTS_SPEND, 0x0C0 JOB_POINTS_REQ, 0x0C1 ALTER_EGO_POINTS; the merit table from data/merits.yaml, the char's
// upgrades and job point levels, merit stats / skills / abilities, the job point gifts) against the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 990801;
    private const string CharName = "Merita";

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static ResultSet Sql(IDatabase db, string q, params BoundValue[] a) => db.Execute(q, a);

    private static string Q1(IDatabase db, string q)
    {
        var r = Sql(db, q, BoundValue.Of(CharId));
        return r != null && r.Next() ? r.Get<string>(0u) : "-";
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
        foreach (var t in CharidTables(db))
            Sql(db, $"DELETE FROM `{t}` WHERE charid = ?", BoundValue.Of(CharId));
        Sql(db, "DELETE FROM chars WHERE charid = ?", BoundValue.Of(CharId));
        Sql(db, "DELETE FROM accounts WHERE id = ?", BoundValue.Of(CharId));
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
    private static bool Bit(byte[] bits, int id) => (bits[id / 8] & 1 << id % 8) != 0;
    private static void AddKeyItem(CharData c, int id) => c.KeyItems[id / 512 * 128 + id % 512 / 8] |= (byte)(1 << id % 512 % 8);

    // a 0x08C slot as id/next/count
    private static string MeritSlot(BasicPacket p, int slot) =>
        $"0x{BitConverter.ToUInt16(p.Buffer, 8 + 4 * slot):X}/{p.Buffer[10 + 4 * slot]}/{p.Buffer[11 + 4 * slot]}";

    // a 0x08D entry as index/job/next/level
    private static string JobPointSlot(BasicPacket p, int slot)
    {
        uint v = BitConverter.ToUInt32(p.Buffer, 4 + 4 * slot);
        return $"{v & 0x1F}/{v >> 5 & 0x7FF}/{v >> 16 & 0x3FF}/{v >> 26}";
    }

    private static int GiftSum(IDatabase db, int job, int mod, int spent)
    {
        var r = Sql(db, "SELECT COALESCE(SUM(value), 0) FROM job_point_gifts WHERE jobid = ? AND modid = ? AND jp_needed <= ?",
                    BoundValue.Of((byte)job), BoundValue.Of((ushort)mod), BoundValue.Of((ushort)spent));
        return r != null && r.Next() ? int.Parse(r.Get<string>(0u)) : -1;
    }

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
        var abilities = AbilityTable.Load(db, _ => true);
        var table = MeritTable.Load(dataDir, new DataEnums(dataDir));

        Console.WriteLine("merit table (data/merits.yaml):");
        Check("merits sorted by id, MaxHp first", "0x40", $"0x{table.Entries[0].Id:X}");
        Check("max_hp: 15 upgrades of 10", "15/10", $"{table.Entries[0].Upgrade}/{table.Entries[0].Value}");
        var whereCharge = table.Entries[table.IndexOf(0x800)];
        Check("warriors_charge: WAR only, job_group_2 costs", "1/3,4,5,5,5", $"{whereCharge.Jobs}/{string.Join(",", whereCharge.Costs)}");
        Check("max_hp's category: hp_mp, 75 upgrades", "75", table.CategoryOf(0x40).MaxUpgrades);
        Check("level caps: 9 at 74, 10 at 75, 15 at 99", "9/10/15", $"{table.LevelCaps[74]}/{table.LevelCaps[75]}/{table.LevelCaps[99]}");
        Check("hand-to-hand skill takes its bonus from 0xC0", "192", table.MeritBySkill[1]);
        Check("an unknown merit has no slot", -1, table.IndexOf(0x7FFE));

        // WAR99/--- in its own mog house: 40 merit points (over the 32 cap), HP x3, STR x2, H2H x1, MaxMerit x2,
        // shijin spiral x9 (cut to 5), an unknown merit; WAR's job points: 50 to spend, 30 spent, types 0 and 1 levelled
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("mrt" + CharName), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,nation,gmlevel,moghouse) VALUES (?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(CharName), BoundValue.Of((ushort)240), BoundValue.Of((ushort)0),
            BoundValue.Of((byte)2), BoundValue.Of((byte)0), BoundValue.Of(CharId));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl, sjob, slvl) VALUES (?, 300, 50, 1, 99, 0, 1)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 99)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_exp(charid, war, merits, limits, mode) VALUES (?, 0, 40, 0, 0)", BoundValue.Of(CharId));
        Sql(db, "INSERT INTO char_skills(charid, skillid, value, rank) VALUES (?, 1, 500, 0)", BoundValue.Of(CharId));
        // lizard cesti (WAR, level 12) for the equipment check after a merit edit
        Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?, 0, 1, 16386, 1, ?)",
            BoundValue.Of(CharId), BoundValue.Of(new byte[24]));
        foreach (var (id, n) in new[] { (0x40, 3), (0x80, 2), (0xC0, 1), (0x44, 2), (0x680, 9), (0x7FFE, 4) })
            Sql(db, "INSERT INTO char_merit(charid, meritid, upgrades) VALUES (?, ?, ?)", BoundValue.Of(CharId), BoundValue.Of((ushort)id), BoundValue.Of((ushort)n));
        Sql(db, "INSERT INTO char_job_points(charid, jobid, capacity_points, job_points, job_points_spent, jptype0, jptype1) VALUES (?, 1, 7, 50, 30, 3, 1)",
            BoundValue.Of(CharId));

        // the same char without a merit table: no upgrades count
        var bare = new CharLoader(db, grades, abilities, stats).Load(CharId);
        stats.BuildSkillsTable(bare);

        var log = new List<string>();
        var loader = new CharLoader(db, grades, abilities, stats);
        var zone = new ZoneHost(db, loader, dataDir, true, log.Add);
        Check("ZoneHost gives the loader its merit table", true, loader.MeritTable != null);

        Console.WriteLine("load (CMeritPoints / CJobPoints / RefreshGiftMods):");
        var s = new MapSession { CharId = CharId, AccountId = CharId };
        zone.OnLogin(s);
        var c = (CharData)s.Char;
        var m = c.Merits;
        Check("upgrades loaded: HP 3, STR 2, H2H 1, MaxMerit 2", "3/2/1/2", $"{m.Count(0x40)}/{m.Count(0x80)}/{m.Count(0xC0)}/{m.Count(0x44)}");
        Check("shijin spiral's 9 cut to its 5", 5, m.Count(0x680));
        Check("merit points capped at MAX_MERIT_POINTS 30 + MaxMerit 2", 32, c.MeritPoints);
        // the bare char has no traits (BuildingCharTraitsTable is the zone host's): WAR99's max HP boost IV comes off
        int traitHp = c.TraitList.FindAll(t => t.Mod == Mod.BASE_HP).ConvertAll(t => (int)t.Value).Sum();
        Check("max HP boost IV: BASE_HP +180", 180, traitHp);
        Check("max HP +30 (3 x 10)", 30, c.MaxHp - bare.MaxHp - traitHp);
        Check("STR +2, DEX +0", "2/0", $"{c.BaseStats[0] - bare.BaseStats[0]}/{c.BaseStats[1] - bare.BaseStats[1]}");
        Check("hand-to-hand skill +2", 2, (c.WorkingSkills[1] & 0x7FFF) - (bare.WorkingSkills[1] & 0x7FFF));
        Check("job points: WAR's row with its levels", "True/50/30/3/1", $"{c.JobPointRows[1]}/{c.JobPoints[1].Current}/{c.JobPoints[1].Spent}/{c.JobPointLevels[1, 0]}/{c.JobPointLevels[1, 1]}");
        Check("no row for MNK", false, c.JobPointRows[2]);
        int gift915 = GiftSum(db, 1, 915, 30);
        Check("gifts up to 30 spent: capacity point bonus (915) summed", gift915, c.ModStat[915]);
        Check("gifts at 99 only: the char without the merit table has them too", gift915, bare.ModStat[915]);
        Check("warriors_charge (merit ability) not available without the merit", false, Bit(c.Abilities, 149));

        void Send(ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(s, opcode, new BasicPacket(buf));
            zone.AfterParse(s);
        }
        List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);
        List<string> Msgs() => Of(0x029).ConvertAll(p => $"{BitConverter.ToUInt16(p.Buffer, 24)}:{BitConverter.ToUInt32(p.Buffer, 12)}:{BitConverter.ToUInt32(p.Buffer, 16)}");
        List<BasicPacket> Misc(int type) => Of(0x063).FindAll(p => p.Buffer[4] == type);
        void Clear() => s.PacketList.Clear();
        void Merit(byte kind, byte param1, ushort param2) => Send(0x0BE, 12, buf =>
        {
            buf[4] = kind; buf[5] = param1;
            BitConverter.GetBytes(param2).CopyTo(buf, 6);
        });

        Console.WriteLine("game OK (0x08C x5, in the mog house):");
        Send(0x00A, 92, buf => BitConverter.GetBytes(CharId).CopyTo(buf, 12));
        Clear();
        Send(0x00C, 12);
        var full = Of(0x08C);
        Check("five merit packets of 61", "5/61/256", $"{full.Count}/{BitConverter.ToUInt16(full[0].Buffer, 4)}/{full[0].GetSize()}");
        Check("slot 0: max_hp, next 4 (its 4th cost), 3 upgrades", "0x40/4/3", MeritSlot(full[0], 0));
        int strIndex = table.IndexOf(0x80);
        Check("STR's slot: next 9, 2 upgrades", "0x80/9/2", MeritSlot(full[strIndex / 61], strIndex % 61));
        int last = table.Entries.Count;
        Check("the slot past the merits is empty", "0x0/0/0", MeritSlot(full[last / 61], last % 61));

        Console.WriteLine("0x063 MERITS:");
        var misc = SelfPackets.Merits(c, stats);
        Check("merit points 32, no BLU bonus, max 32", "32/0/32", $"{BitConverter.ToUInt16(misc.Buffer, 10) & 0x7F}/{BitConverter.ToUInt16(misc.Buffer, 10) >> 7 & 0x3F}/{misc.Buffer[12]}");

        Console.WriteLine("0x0BE MERITS ChangeMode:");
        Clear();
        Merit(2, 1, 0);
        Check("mode saved, merit mode on", "1/True", $"{Q1(db, "SELECT mode FROM char_exp WHERE charid = ?")}/{c.MeritMode}");
        Check("MERITS, MONSTROSITY1, MONSTROSITY2", "2,3,4", string.Join(",", Of(0x063).ConvertAll(p => p.Buffer[4])));
        Clear();
        Merit(2, 2, 0);
        Check("Param1 2 is rejected", "0/True", $"{s.PacketList.Count}/{c.MeritMode}");
        Clear();
        Merit(1, 0, 0);
        Check("Kind 1 is rejected", 0, s.PacketList.Count);
        Merit(2, 0, 0);
        Check("back to EXP mode", "0/False", $"{Q1(db, "SELECT mode FROM char_exp WHERE charid = ?")}/{c.MeritMode}");

        Console.WriteLine("0x0BE MERITS EditMode raise:");
        Clear();
        c.Hp = 1;
        Merit(3, 1, 0x800 >> 1);
        Check("warriors_charge: 1 upgrade for 3 points", "1/29", $"{m.Count(0x800)}/{c.MeritPoints}");
        Check("MERIT_INCREASE (380) for 0x400 at 1", "380:1024:1", string.Join("|", Msgs()));
        var single = Of(0x08C);
        Check("one 0x08C: count 1, warriors_charge next 4 count 1, 16 bytes", "1/1/0x800/4/1/16",
              single.Count == 1 ? $"{single.Count}/{BitConverter.ToUInt16(single[0].Buffer, 4)}/{MeritSlot(single[0], 0)}/{single[0].GetSize()}" : single.Count.ToString());
        Check("the upgrade saved", "1", Q1(db, "SELECT upgrades FROM char_merit WHERE charid = ? AND meritid = 2048"));
        Check("merit points saved", "29", Q1(db, "SELECT merits FROM char_exp WHERE charid = ?"));
        Check("an upgrade in its merit makes warriors_charge available", true, Bit(c.Abilities, 149));
        Check("restored to full HP", c.MaxHp, c.Hp);
        Check("the stat refresh: CHAR_STATUS, CLISTATUS, CLISTATUS2, ABIL_RECAST, COMMAND_DATA, CHAR_SYNC", "1/1/1/1/1/1",
              $"{Of(0x037).Count}/{Of(0x061).Count}/{Of(0x062).Count}/{Of(0x119).Count}/{Of(0x0AC).Count}/{Of(0x067).Count}");

        Clear();
        Merit(3, 1, 0x40 >> 1);
        Check("max_hp: 4th upgrade for 4 points, +40 HP", "4/25/40", $"{m.Count(0x40)}/{c.MeritPoints}/{c.MaxHp - bare.MaxHp - traitHp}");

        Console.WriteLine("0x0BE weapon skill merit:");
        Clear();
        Merit(3, 1, 0x682 >> 1);
        Check("exenterator: 1 upgrade for 20 points", "1/5", $"{m.Count(0x682)}/{c.MeritPoints}");
        Check("exenterator learned (unlock 53) and saved", "True/True",
              $"{Bit(c.LearnedWeaponskills, 53)}/{Q1(db, "SELECT HEX(weaponskills) FROM chars WHERE charid = ?") == "0000000000002000"}");
        Check("COMMAND_DATA for the weapon skill, then the refresh", 2, Of(0x0AC).Count);
        Check("the abilities blob saved at its 49 bytes", "49", Q1(db, "SELECT LENGTH(abilities) FROM chars WHERE charid = ?"));
        Clear();
        Merit(3, 1, 0x682 >> 1);
        Check("too few points (5 of 22): no upgrade, still the reply", "1/5/380:833:1", $"{m.Count(0x682)}/{c.MeritPoints}/{string.Join("|", Msgs())}");
        Clear();
        Merit(3, 0, 0x682 >> 1);
        Check("lowered: no refund, the weapon skill forgotten", "0/5/False", $"{m.Count(0x682)}/{c.MeritPoints}/{Bit(c.LearnedWeaponskills, 53)}");
        Check("MERIT_DECREASE (381) at 0, the row deleted", "381:833:0/-", $"{string.Join("|", Msgs())}/{Q1(db, "SELECT upgrades FROM char_merit WHERE charid = ? AND meritid = 1666")}");
        Check("weapon skills saved cleared", "0000000000000000", Q1(db, "SELECT HEX(weaponskills) FROM chars WHERE charid = ?"));

        Console.WriteLine("0x0BE lower:");
        Clear();
        Merit(3, 0, 0x800 >> 1);
        Check("warriors_charge lowered: ability gone, row deleted", "0/False/-",
              $"{m.Count(0x800)}/{Bit(c.Abilities, 149)}/{Q1(db, "SELECT upgrades FROM char_merit WHERE charid = ? AND meritid = 2048")}");
        Clear();
        Merit(3, 0, 0x800 >> 1);
        Check("lowering at 0 stays 0 (and still replies)", "0/381:1024:0", $"{m.Count(0x800)}/{string.Join("|", Msgs())}");

        Console.WriteLine("0x0BE equipment checked after the edit (CheckValidEquipment):");
        Send(0x050, 8, b => { b[4] = 1; b[5] = 0; b[6] = 0; });
        Check("lizard cesti on", 16386, c.Equipped[0]?.ItemId ?? 0);
        // a WAR level the cesti's 12 no longer fits (the edit itself changes nothing: lowering at 0)
        c.JobLevels[1] = 5;
        Clear();
        Merit(3, 0, 0x800 >> 1);
        Check("the cesti came off, the char_equip row gone", "0/-",
              $"{c.Equipped[0]?.ItemId ?? 0}/{Q1(db, "SELECT slotid FROM char_equip WHERE charid = ? AND equipslotid = 0")}");
        c.JobLevels[1] = 99;

        Console.WriteLine("0x0BE outside the merits / the mog house:");
        Clear();
        Merit(3, 1, 0x7FFE >> 1);
        Check("unknown merit: nothing", 0, s.PacketList.Count);
        c.MoghouseId = 0;
        c.MeritPoints = 30;
        Merit(3, 1, 0x40 >> 1);
        Check("outside the mog house: nothing", "0/4/30", $"{s.PacketList.Count}/{m.Count(0x40)}/{c.MeritPoints}");

        Console.WriteLine("0x0BF JOB_POINTS_SPEND:");
        void Jp(ushort index) => Send(0x0BF, 8, buf => BitConverter.GetBytes(index).CopyTo(buf, 4));
        Clear();
        Jp(0x20);
        Check("outside the mog house: rejected", "0/3", $"{s.PacketList.Count}/{c.JobPointLevels[1, 0]}");
        c.MoghouseId = CharId;
        Jp(0x2A);
        Jp(0x1F);
        Check("type 10 / below WAR: rejected", 0, s.PacketList.Count);
        Jp(0x20);
        Check("WAR type 0 raised for 4: level 4, 46 left, 34 spent", "4/46/34", $"{c.JobPointLevels[1, 0]}/{c.JobPoints[1].Current}/{c.JobPoints[1].Spent}");
        Check("saved", "4/1/46/34", Q1(db, "SELECT CONCAT(jptype0, '/', jptype1, '/', job_points, '/', job_points_spent) FROM char_job_points WHERE charid = ? AND jobid = 1"));
        Check("JOB_POINTS (0x063 type 5): WAR 46 / 34", "46/34", $"{BitConverter.ToUInt16(Misc(5)[0].Buffer, 20)}/{BitConverter.ToUInt16(Misc(5)[0].Buffer, 22)}");
        var jpOne = Of(0x08D);
        Check("one 0x08D: type 0 of WAR, next 5, level 4, full size", "1/0/1/5/4/260", jpOne.Count == 1 ? $"1/{JobPointSlot(jpOne[0], 0)}/{jpOne[0].GetSize()}" : jpOne.Count.ToString());
        Check("JOB_POINTS_INCREASE (720) for 0x20 at 4", "720:32:4", string.Join("|", Msgs()));
        Check("the gifts follow the 34 spent", GiftSum(db, 1, 915, 34), c.ModStat[915]);
        Clear();
        Jp(0x40);
        Check("MNK (no row): nothing spent, the entry's id 0", "0/0/0/1/0", $"{c.JobPointLevels[2, 0]}/{JobPointSlot(Of(0x08D)[0], 0)}");

        Console.WriteLine("0x0C0 JOB_POINTS_REQ:");
        Clear();
        Send(0x0C0, 4);
        Check("without Job Breaker: nothing", 0, s.PacketList.Count);
        AddKeyItem(c, SelfPackets.KeyItemJobBreaker);
        Send(0x0C0, 4);
        var jps = Of(0x08D);
        Check("13 packets (WAR, then two jobs each, then an empty one)", 13, jps.Count);
        Check("WAR's ten types in the first", "0/1/5/4|1/1/2/1|9/1/1/0|0/0/0/0",
              jps.Count == 13 ? $"{JobPointSlot(jps[0], 0)}|{JobPointSlot(jps[0], 1)}|{JobPointSlot(jps[0], 9)}|{JobPointSlot(jps[0], 10)}" : "-");
        Check("the others empty", "0/0/0/0", jps.Count == 13 ? JobPointSlot(jps[1], 0) : "-");

        Console.WriteLine("0x0C1 ALTER_EGO_POINTS:");
        void AlterEgo(ushort cat) => Send(0x0C1, 8, buf => BitConverter.GetBytes(cat).CopyTo(buf, 4));
        Clear();
        AlterEgo(8);
        Check("without the Cipher Bracelet: nothing", 0, s.PacketList.Count);
        AddKeyItem(c, 3361);
        AlterEgo(7);
        Check("category 7: nothing", 0, s.PacketList.Count);
        AlterEgo(8);
        Check("HP: the alter ego points (0x08E)", 1, Of(0x08E).Count);
        Clear();
        c.MLvl = 98;
        AlterEgo(18);
        Check("below 99: nothing", 0, s.PacketList.Count);
        c.MLvl = 99;

        Console.WriteLine("the BLU bonus and the job point spells:");
        c.MJob = 16;
        c.Merits.Counts[table.IndexOf(MeritTable.Assimilation)] = 3;
        c.JobPointLevels[16, 1] = 2;
        misc = SelfPackets.Merits(c, stats);
        Check("BLU 99: Assimilation 3 + blue magic point bonus 2", 5, BitConverter.ToUInt16(misc.Buffer, 10) >> 7 & 0x3F);
        c.MLvl = 98;
        misc = SelfPackets.Merits(c, stats);
        Check("BLU 98: Assimilation only", 3, BitConverter.ToUInt16(misc.Buffer, 10) >> 7 & 0x3F);
        c.MLvl = 99;
        c.MJob = 3;
        c.JobPoints[3] = (0, 0, 1200);
        Check("WHM 1200 spent: the spells sent", true, loader.RefreshGiftMods(c));
        Check("Reraise IV (848) and Full Cure (893) learned and saved", "True/True/848,893",
              $"{Bit(c.SpellList, 848)}/{Bit(c.SpellList, 893)}/{Q1(db, "SELECT GROUP_CONCAT(spellid ORDER BY spellid) FROM char_spells WHERE charid = ?")}");
        Check("known already: nothing to send", false, loader.RefreshGiftMods(c));
        Check("WHM's capacity point bonus replaces WAR's", GiftSum(db, 3, 915, 1200), c.ModStat[915]);

        Console.WriteLine("the melee merits (Melee: GetCritHitRate, CAttackRound, the TP return, CBattleEntity::ACC):");
        var rng = new Random(5);
        var melee = new Melee(stats, new StatSettings(), () => rng);
        void SetMerit(ushort id, byte n) => c.Merits.Counts[table.IndexOf(id)] = n;
        stats.BuildSkillsTable(bare);
        c.MJob = 1;
        int crit0 = melee.CritHitRate(c, bare, Melee.SlotMain), enemy0 = melee.CritHitRate(bare, c, Melee.SlotMain);
        SetMerit(324, 3); // Critical Hit Rate
        SetMerit(326, 2); // Enemy Critical Hit Rate
        Check("Critical Hit Rate x3: +3%; Enemy Critical Hit Rate x2: -2% against the char", "3/-2",
              $"{melee.CritHitRate(c, bare, Melee.SlotMain) - crit0}/{melee.CritHitRate(bare, c, Melee.SlotMain) - enemy0}");
        SetMerit(324, 0);
        SetMerit(326, 0);

        // unarmed: each fist's swing doubles at 100%
        Check("WAR 99 has the Double Attack trait", true, CharStats.HasTrait(c, 15));
        c.SetModifier(Mod.DOUBLE_ATTACK, 95);
        bool allDoubled(int rounds)
        {
            for (int i = 0; i < rounds; i++) if (melee.BuildRound(c).Count != 4) return false;
            return true;
        }
        Check("DOUBLE_ATTACK 95 alone: some round without the double", false, allDoubled(200));
        SetMerit(392, 5); // Double Attack Rate
        Check("with Double Attack Rate x5 (100%): every round doubled, two a fist", true, allDoubled(200));
        SetMerit(392, 0);
        c.SetModifier(Mod.DOUBLE_ATTACK, 0);

        c.MJob = 12; // SAM
        int tp0 = Melee.SingleMeleeHitTpReturn(c);
        SetMerit(1092, 5); // Store TP Effect
        int storeTp = c.Merits.Value(c, 1092);
        Check("SAM's Store TP Effect x5 in the TP return", (int)Math.Floor(Melee.TpReturn(c, Melee.ModifiedDelay(c, Melee.BaseDelay(c))) * (1 + storeTp / 100.0)) + "/True",
              $"{Melee.SingleMeleeHitTpReturn(c)}/{storeTp > 0 && Melee.SingleMeleeHitTpReturn(c) > tp0}");
        SetMerit(1092, 0);

        c.MJob = 5; // RDM
        int acc0 = melee.Acc(c, 0);
        SetMerit(2326, 3); // Accuracy
        Check("RDM's Accuracy x3 in ACC", c.Merits.Value(c, 2326), melee.Acc(c, 0) - acc0);
        SetMerit(2326, 0);
        c.StatusEffects.AddStatusEffect(new StatusEffect(274, 274, 9, 0, 60_000));
        Check("Enlight's potency in ACC", c.GetMod(Mod.ENSPELL_DMG), melee.Acc(c, 0) - acc0);
        c.StatusEffects.DelStatusEffectSilent(274);
        c.MJob = 1;
    }
}
