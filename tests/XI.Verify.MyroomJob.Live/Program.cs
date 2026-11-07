using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Ipc;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.MyroomJob.Live -- live gate for c2s 0x100 MYROOM_JOB (ZoneHost.MyroomJob: the job change at a moogle,
// char_equip_saved per job, the levels, merit mode and style lock it resets, and what it sends) against the real
// MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 990925;
    private const string CharName = "Jobch";

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

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var cfg = new StatSettings();
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), cfg);
        var loader = new CharLoader(db, Grades.Load(dataDir), AbilityTable.Load(db, _ => true), stats);
        var log = new List<string>();
        var zone = new ZoneHost(db, loader, dataDir, true, log.Add);
        var sent = new List<IIpcMessage>();
        zone.SendIpc = sent.Add;
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);

        // WAR75/NIN60 in its own mog house in Windurst Waters, in merit mode and style locked; MNK50, WHM30, THF40 and
        // (for the refusal) MON unlocked too
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("job" + CharName), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,moghouse,nation,gmlevel,isstylelocked) VALUES (?,?,?,?,?,?,?,?,1)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(CharName), BoundValue.Of((ushort)238), BoundValue.Of((ushort)0),
            BoundValue.Of(CharId), BoundValue.Of((byte)2), BoundValue.Of((byte)0));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl, sjob, slvl) VALUES (?, 300, 50, 1, 75, 13, 37)", BoundValue.Of(CharId));
        uint unlocked = 1u << 1 | 1u << 2 | 1u << 3 | 1u << 6 | 1u << 13 | 1u << 23;
        Sql(db, "REPLACE INTO char_jobs(charid, unlocked, war, mnk, whm, thf, nin, mon) VALUES (?, ?, 75, 50, 30, 40, 60, 1)",
            BoundValue.Of(CharId), BoundValue.Of(unlocked));
        Sql(db, "REPLACE INTO char_exp(charid, mode) VALUES (?, 1)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));
        void Put(byte slot, ushort id) =>
            Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?,0,?,?,1,?)",
                BoundValue.Of(CharId), BoundValue.Of(slot), BoundValue.Of(id), BoundValue.Of(new byte[24]));
        void Wear(byte equipSlot, byte slot) =>
            Sql(db, "INSERT INTO char_equip(charid, equipslotid, slotid, containerid) VALUES (?,?,?,0)",
                BoundValue.Of(CharId), BoundValue.Of(equipSlot), BoundValue.Of(slot));
        Sql(db, "DELETE FROM char_equip WHERE charid = ?", BoundValue.Of(CharId)); // char_insert's empty row
        Put(1, 12448); Wear(4, 1);   // bronze cap (all jobs)
        Put(2, 16482); Wear(0, 2);   // onion dagger (WAR)
        Put(3, 16465); Wear(1, 3);   // bronze knife in the sub slot (dual wield is assumed while traits are not built)
        Put(4, 16405);               // cat baghnakhs (MNK)
        Put(5, 13505); Wear(13, 5);  // judge's ring (all jobs)
        // MNK's gear: the baghnakhs, the cap and the ring in both ring slots (only one is owned)
        Sql(db, "INSERT INTO char_equip_saved SET charid = ?, jobid = 2, main = 16405, head = 12448, ring1 = 13505, ring2 = 13505", BoundValue.Of(CharId));

        MapSession s = null;
        CharData c = null;
        void Send(ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(s, opcode, new BasicPacket(buf));
            zone.AfterParse(s);
        }
        void Login()
        {
            if (s != null) zone.OnCharTimeout(s, false);
            s = new MapSession { CharId = CharId, AccountId = CharId };
            zone.OnLogin(s);
            c = (CharData)s.Char;
            Send(0x00A, 92, buf => BitConverter.GetBytes(CharId).CopyTo(buf, 12));
            s.PacketList.Clear();
        }
        List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);
        string Ops() => string.Join(",", new List<BasicPacket>(s.PacketList).ConvertAll(p => $"{Opcode(p):X3}"));
        void Clear() { s.PacketList.Clear(); log.Clear(); sent.Clear(); }
        bool Logged(string text) => log.Exists(l => l.Contains(text));
        void Job(byte main, byte sub) => Send(0x100, 8, buf => { buf[4] = main; buf[5] = sub; });
        string Jobs() => $"{c.MJob}.{c.MLvl}/{c.SJob}.{c.SLvl}";
        // the first row's columns as one string: the query's SELECT list goes into CONCAT_WS(' ', ...)
        string DbRow(string cols, string rest)
        {
            var r = Sql(db, $"SELECT CONCAT_WS(' ', {cols}) AS v {rest}", BoundValue.Of(CharId));
            return r != null && r.Next() ? r.Get<string>("v") : "-";
        }
        string DbJobs() => DbRow("mjob, mlvl, sjob, slvl", "FROM char_stats WHERE charid = ?");
        // the equipped item per slot that has one: slot:item id
        string Worn()
        {
            var worn = new List<string>();
            for (int slot = 0; slot < 16; slot++)
                if (CharStats.EquippedItem(c, slot) is Item item) worn.Add($"{slot}:{item.ItemId}");
            return string.Join(" ", worn);
        }
        string DbWorn() => DbRow("IFNULL(GROUP_CONCAT(CONCAT(equipslotid, ':', slotid) ORDER BY equipslotid SEPARATOR ' '), '')", "FROM char_equip WHERE charid = ?");
        string Saved(int job) => DbRow("main, sub, ranged, ammo, head, body, hands, legs, feet, neck, waist, ear1, ear2, ring1, ring2, back",
                                       $"FROM char_equip_saved WHERE charid = ? AND jobid = {job}");

        Login();
        Check("logged in as WAR75/NIN37 wearing the cap, the dagger, the knife and the ring", "1.75/13.37/0:16482 1:16465 4:12448 13:13505/True/True",
              $"{Jobs()}/{Worn()}/{c.MeritMode}/{c.StyleLocked}");

        Console.WriteLine("validation:");
        Clear();
        Job(4, 0);
        Check("BLM is locked", "True/0", $"{Logged("Main job not unlocked")}/{s.PacketList.Count}");
        Clear();
        Job(23, 0);
        Check("MON as the main job", true, Logged("MON is only entered through the Feretory"));
        Clear();
        Job(24, 0);
        Check("main job 24", true, Logged("MainJobIndex out of range: 24 not in [1, 23]"));
        Clear();
        Job(0, 4);
        Check("BLM support", true, Logged("Support job not unlocked"));
        Clear();
        Job(0, 23);
        Check("MON support", true, Logged("MON is never a support job"));
        c.MoghouseId = 0;
        Clear();
        Job(2, 0);
        Check("outside the mog house in Windurst Waters", "True/1.75", $"{Logged("Player not in MH or zone with Moogle.")}/{c.MJob}.{c.MLvl}");
        c.MoghouseId = CharId;

        Console.WriteLine("support job WHM:");
        Clear();
        Job(0, 3);
        Check("WAR75/WHM30, saved", "1.75/3.30/1 75 3 30", $"{Jobs()}/{DbJobs()}");
        Check("the knife leaves the sub slot, the rest stays", "0:16482 4:12448 13:13505/0:2 4:1 13:5", $"{Worn()}/{DbWorn()}");
        Check("the style lock off, saved", "False/0", $"{c.StyleLocked}/{DbRow("isstylelocked", "FROM chars WHERE charid = ?")}");
        var ops = Ops();
        Check("009 style off, then JOB_INFO, CHAR_STATUS, CLISTATUS(2), ABIL_RECAST, COMMAND_DATA, MERITS, MONSTROSITY1/2, CHAR_SYNC",
              "009,01B,037,061,062,119,0AC,063,063,063,067", string.Join(",", ops.Split(',')[..11]));
        Check("a sub change keeps merit mode", "True/1", $"{c.MeritMode}/{DbRow("mode", "FROM char_exp WHERE charid = ?")}");
        Check("HP and MP full, saved", $"{c.MaxHp} {c.MaxMp}", DbRow("hp, mp", "FROM char_stats WHERE charid = ?"));
        Check("no saved gear for WAR yet", "-", Saved(1));

        Console.WriteLine("main job MNK:");
        Clear();
        Job(2, 0);
        Check("MNK50/WHM25, saved", "2.50/3.25/2 50 3 25", $"{Jobs()}/{DbJobs()}");
        Check("WAR's gear saved", "16482 0 0 0 12448 0 0 0 0 0 0 0 0 13505 0 0", Saved(1));
        Check("MNK's gear on: the baghnakhs, the cap, one ring", "0:16405 4:12448 13:13505/0:4 4:1 13:5", $"{Worn()}/{DbWorn()}");
        Check("merit mode off below 75 without Limit Breaker, saved", "False/0", $"{c.MeritMode}/{DbRow("mode", "FROM char_exp WHERE charid = ?")}");
        Check("no style lock message the second time", "01B", Ops().Split(',')[0]);
        Check("HP full", c.MaxHp, c.Hp);

        Console.WriteLine("main job WHM (the sub): swapped");
        Clear();
        Job(3, 0);
        Check("WHM30/MNK15", "3.30/2.15/3 30 2 15", $"{Jobs()}/{DbJobs()}");
        Check("MNK's gear saved", "16405 0 0 0 12448 0 0 0 0 0 0 0 0 13505 0 0", Saved(2));
        Check("WHM has none: all off", "/", $"{Worn()}/{DbWorn()}");
        Check("the unequips sent (EQUIP_LIST)", 3, Of(0x050).Count);

        Console.WriteLine("support job the main's:");
        Clear();
        Job(0, 3);
        Check("refused, but the job packets still go", "3.30/2.15/01B", $"{Jobs()}/{Ops().Split(',')[0]}");

        Console.WriteLine("main job WAR, support THF:");
        Clear();
        Job(1, 6);
        Check("WAR75/THF37", "1.75/6.37/1 75 6 37", $"{Jobs()}/{DbJobs()}");
        Check("WAR's gear back on (the knife was not saved)", "0:16482 4:12448 13:13505/0:2 4:1 13:5", $"{Worn()}/{DbWorn()}");
        Check("WHM's (nothing) saved", "0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0", Saved(3));

        Console.WriteLine("relogin:");
        Login();
        Check("the jobs and the gear as left", "1.75/6.37/0:16482 4:12448 13:13505/False/False", $"{Jobs()}/{Worn()}/{c.MeritMode}/{c.StyleLocked}");

        Console.WriteLine("status effects on a job change:");
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectId.Haste, EffectId.Haste, 100, 0, 180_000));
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectId.Food, EffectId.Food, 0, 0, 1_800_000));
        var teleport = new StatusEffect(EffectId.Teleport, 0, 5, 0, 10_000);
        c.StatusEffects.AddStatusEffect(teleport);
        Clear();
        Job(0, 6);
        // the teleport (on_jobchange) goes too, its power zeroed first so its lose script does not warp
        Check("haste (dispelable) gone with its wear-off, food kept, teleport gone with power 0", $"False/True/False:0/{CharId}:33:206",
              $"{c.StatusEffects.HasStatusEffect(EffectId.Haste)}/{c.StatusEffects.HasStatusEffect(EffectId.Food)}/" +
              $"{c.StatusEffects.HasStatusEffect(EffectId.Teleport)}:{teleport.Power}/" +
              string.Join(",", Of(0x029).ConvertAll(p => $"{BitConverter.ToUInt32(p.Buffer, 8)}:{BitConverter.ToInt32(p.Buffer, 12)}:{BitConverter.ToUInt16(p.Buffer, 24)}")));

        Console.WriteLine("the job and level lua methods:");
        var lua = scripts.Lua;
        lua.SetGlobal("me", l => { l.PushObject(c); return 1; });
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString("local p = me()\n" + code, "=verify_jobs");
            return string.Join(",", got);
        }
        string Hp() => $"{c.Hp == c.MaxHp && c.MaxHp > 0}";
        int def = c.GetMod(Mod.DEF), str = c.GetMod(Mod.STR);
        Clear();
        Lua("p:setLevel(50)");
        Check("setLevel(50): WAR50/THF25, exp one short of 51, full HP, saved", $"1.50/6.25/{stats.Growth.GetExpNextLevel(50) - 1}/True/1 50 6 25/50",
              $"{Jobs()}/{c.JobExp[1]}/{Hp()}/{DbJobs()}/{DbRow("war", "FROM char_jobs WHERE charid = ?")}");
        Check("the gear's mods taken out and put back once", $"{def}/{str}", $"{c.GetMod(Mod.DEF)}/{c.GetMod(Mod.STR)}");
        Check("its packets", "01B,061,062,119,0AC,037,063,063,063,067",
              string.Join(",", Ops().Split(',').ToList().FindAll(o => o != "053" && o != "029" && o != "0AA")));

        Console.WriteLine("levelRestriction:");
        Clear();
        Check("levelRestriction(30) returns 30: WAR30/THF15, the job still 50, saved mlvl 30", "30/1.30/6.15/50/1 30 6 15",
              $"{Lua("report(p:levelRestriction(30))")}/{Jobs()}/{c.JobLevels[1]}/{DbJobs()}");
        Check("MAGIC_DATA and CHAR_SYNC sent, no level sync flag without the effect", "True/0/0",
              $"{Of(0x0AA).Count > 0}/{(Of(0x067).Count == 1 ? Of(0x067)[0].Buffer[0x10] : -1)}/{(Of(0x067).Count == 1 ? Of(0x067)[0].Buffer[0x26] : -1)}");
        Check("no argument: the restriction read back", "30/1.30/6.15", $"{Lua("report(p:levelRestriction())")}/{Jobs()}");
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectId.LevelSync, EffectId.LevelSync, 30, 0, 0), EffectNotice.Silent);
        Check("level sync: CHAR_SYNC flag 0x04 and level 30", "4/30",
              $"{GameOkPackets.CharSync(c).Buffer[0x10]}/{GameOkPackets.CharSync(c).Buffer[0x26]}");
        c.StatusEffects.DelStatusEffectSilent(EffectId.LevelSync);
        Check("a restriction above the job's level: the job's own level", "60/1.50/6.25", $"{Lua("report(p:levelRestriction(60))")}/{Jobs()}");
        Clear();
        Check("levelRestriction(0): back to WAR50", "0/1.50/6.25/1 50 6 25", $"{Lua("report(p:levelRestriction(0))")}/{Jobs()}/{DbJobs()}");

        Console.WriteLine("changeJob, changesJob, unlockJob, hasJob:");
        Check("hasJob: WAR yes, BLM no", "true,false", Lua("report(tostring(p:hasJob(1))) report(tostring(p:hasJob(4)))"));
        Clear();
        Lua("p:unlockJob(4)");
        Check("unlockJob(BLM): unlocked, level 1, saved, JOB_INFO", "true/1/1/1",
              $"{Lua("report(tostring(p:hasJob(4)))")}/{c.JobLevels[4]}/{DbRow("blm", "FROM char_jobs WHERE charid = ?")}/{Of(0x01B).Count}");
        Clear();
        Lua("p:changeJob(2)");
        Check("changeJob(MNK): the main job only, the level as it was, saved", "2.50/6.25/2 50 6 25/True",
              $"{Jobs()}/{DbJobs()}/{Hp()}");
        Lua("p:changesJob(3)");
        Check("changesJob(WHM): the sub job only, its level as it was, saved", "2.50/3.25/2 50 3 25", $"{Jobs()}/{DbJobs()}");
        Lua("p:setsLevel(20)");
        Check("setsLevel(20): WHM20, its exp one short of 21, saved", $"2.50/3.20/{stats.Growth.GetExpNextLevel(20) - 1}/20",
              $"{Jobs()}/{c.JobExp[3]}/{DbRow("whm", "FROM char_jobs WHERE charid = ?")}");
        Lua("p:setLevelCap(60)");
        Check("setLevelCap(60) saved", "60/60", $"{Lua("report(p:getLevelCap())")}/{DbRow("genkai", "FROM char_jobs WHERE charid = ?")}");

        Console.WriteLine("skills:");
        Clear();
        Lua("p:setSkillLevel(1, 800)");
        Check("setSkillLevel(H2H, 800): real 800, saved, CLISTATUS2, working 80 (under the cap)", $"800/800/1/{Math.Min(80, (int)stats.Growth.GetMaxSkill(1, 2, 50))}",
              $"{Lua("report(p:getCharSkillLevel(1))")}/{DbRow("value", "FROM char_skills WHERE charid = ? AND skillid = 1")}/{Of(0x062).Count}/" +
              $"{Lua("report(p:getSkillLevel(1))")}");
        Lua("p:setSkillRank(48, 3)");
        Check("setSkillRank(fishing, 3): rank 3, saved", "3/3", $"{Lua("report(p:getSkillRank(48))")}/{DbRow("rank", "FROM char_skills WHERE charid = ? AND skillid = 48")}");
        Check("getMaxSkillLevel(50, MNK, H2H)", $"{stats.Growth.GetMaxSkill(1, 2, 50)}", Lua("report(p:getMaxSkillLevel(50, 2, 1))"));
        Clear();
        Lua("p:recalculateStats()");
        Check("recalculateStats: the stat packets", "01B,061,062,119,0AC,037", string.Join(",", Ops().Split(',').ToList().GetRange(0, 6)));
        Check("no lua errors", "", string.Join(" | ", log.FindAll(l => l.Contains("error") || l.Contains("attempt"))));
    }
}
