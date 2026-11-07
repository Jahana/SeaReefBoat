using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Monstrosity.Live -- live gate for Monstrosity.cs / ZoneHost.Monstrosity.cs (batch M1): the dataset
// (data/monstrosity.yaml: the shop, the exits, the passage zones), the saved progress outside MON (getMonstrosityData of
// a char with no row, setMonstrosityEntryData and setMonstrosityData into char_monstrosity, the level instincts), changeJob
// into MON (the data, MON on both jobs at the species level, the species' ecosystem, the species HP formula), zoning in
// as a Monipulator (Gestation, the species' look, speed and hitbox, the login / look / status packets, the full update
// after GAMEOK with both MONSTROSITY pages), a species change (refused for one not owned; the family's level and jobs,
// the exp reset, the row), the Belligerency flag and the rank infamy gives (the SetPoints hook), and changeJob back out.
// Batch M2a: a Monipulator's abilities (its species' main job and MON, no Provoke), traits and magic skills, its combat
// (the A+ combat skill in ATT / ACC / DEF, the species' evasion, fists, delay and damage), the 0x044 MON, 0x0DF and 0x0AA
// packets, exp on monstrosity's table (infamy from kills, the level up into the family, no loss on death) and a
// gladiator's prestige. Batch M2b: the Feretory equipment change (0x102: instincts, a name descriptor, a species).
// Batch M2c: a monster skill (0x01A MonsterSkill: a char in the mob skill state, the move's ready time and TP cost)
// and Fighting Stance's form. Batch M3: the Monipulator's restrictions (no party invite or request), the death menu
// and its timeout, the idle regen, /check and /checkname on a Monipulator, Belligerency, and the gladiator (its team in
// the status packets, the gear encumbrance).
// West Ronfaure, with the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 994120;
    private const ushort WestRonfaure = 100;
    private const ushort SpeciesRabbit = 1, SpeciesMandragora = 18, SpeciesBehemoth = 2;

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
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
        var loader = new CharLoader(db, grades, AbilityTable.Load(db, _ => true), stats);
        var log = new List<string>();
        var zone = new ZoneHost(db, loader, dataDir, true, log.Add);
        var ipc = new List<object>();
        zone.SendIpc = m => ipc.Add(m);
        long nowMs = 1_000_000;
        zone.NowMs = () => nowMs;
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);
        var lua = scripts.Lua;
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local player = GetPlayerByID({CharId})\n" + code, "=verify_monstrosity");
            return string.Join(",", got);
        }

        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("mo" + CharId), BoundValue.Of("x"), BoundValue.Of((byte)1));
        // the NEW_ADVENTURER title: not a first login, no charCreate
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,moghouse,nation,gmlevel,titles) " +
                "VALUES (?,?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Lapin"), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(-120.0f), BoundValue.Of(-20.0f), BoundValue.Of(-120.0f), BoundValue.Of(0u), BoundValue.Of((byte)0), BoundValue.Of((byte)0),
            BoundValue.Of(titles));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl, sjob, slvl) VALUES (?, 500, 0, 1, 30, 0, 0)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs SET charid = ?, unlocked = 2, war = 30", BoundValue.Of(CharId));
        Sql(db, "INSERT INTO char_storage(charid, inventory) VALUES (?, 30) ON DUPLICATE KEY UPDATE inventory = 30", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_unlocks(charid) VALUES (?)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_points(charid) VALUES (?)", BoundValue.Of(CharId));

        MapSession s = null;
        var loginPackets = new List<BasicPacket>();
        CharData c = null;
        void Send(ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(s, opcode, new BasicPacket(buf));
            zone.AfterParse(s);
        }
        var gameOkPackets = new List<BasicPacket>();
        void Login()
        {
            s = new MapSession { CharId = CharId, AccountId = CharId };
            zone.OnLogin(s);
            c = (CharData)s.Char;
            Send(0x00A, 92, buf => BitConverter.GetBytes(CharId).CopyTo(buf, 12));
            loginPackets = new List<BasicPacket>(s.PacketList);
            s.PacketList.Clear();
            Send(0x00C, 12);
            gameOkPackets = new List<BasicPacket>(s.PacketList);
        }
        void Leave()
        {
            zone.OnCharTimeout(s, false);
            zone.OnZoneOut(s);
        }
        List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);
        int Msgs(int msg) => Of(0x029).Count(p => BitConverter.ToUInt16(p.Buffer, 24) == msg);
        bool Has(byte[] bits, int id) => (bits[id / 8] & 1 << (id % 8)) != 0;
        bool Listed(BasicPacket p, int id) => (p.Buffer[4 + id / 8] & 1 << (id % 8)) != 0;
        string Points(string type) => Lua($"report(player:getCurrency('{type}'))");
        var addExp = typeof(ZoneHost).GetMethod("AddExperiencePoints", BindingFlags.NonPublic | BindingFlags.Instance, null,
            new[] { typeof(bool), typeof(CharData), typeof(BattleEntity), typeof(uint), typeof(byte), typeof(bool), typeof(bool), typeof(bool) }, null);
        var delExp = typeof(ZoneHost).GetMethod("DelExperiencePoints", BindingFlags.NonPublic | BindingFlags.Instance);
        void KillExp(uint exp) => addExp.Invoke(zone, new object[] { false, c, c, exp, (byte)0, false, true, false });
        void MonsterSkill(ushort targId, uint datSkillId) => Send(0x01A, 28, b =>
        {
            BitConverter.GetBytes(CharId).CopyTo(b, 4);
            BitConverter.GetBytes(targId).CopyTo(b, C2sAction.OffActIndex);
            BitConverter.GetBytes((ushort)ActionId.MonsterSkill).CopyTo(b, C2sAction.OffActionID);
            C2sAction.SetSkillId(b, datSkillId);
        });
        BasicPacket Page(List<BasicPacket> list, int type) => list.LastOrDefault(p => Opcode(p) == 0x063 && BitConverter.ToUInt16(p.Buffer, 4) == type);
        Login();

        Console.WriteLine("the dataset: the shop, the Feretory exits, the passage zones");
        Check("getMonstrosityShop: Beasts page slot 0 is the lapinion variant for 7500, rabbit 90", "7500/nil/1/90",
            Lua("local s = player:getMonstrosityShop()[0][0] report(s.infamyCost .. '/' .. tostring(s.monSpecies) .. '/' .. s.requirements[1][1] .. '/' .. s.requirements[1][2])"));
        Check("getMonstrosityExits: West Ronfaure's two", "2/-600/-60/400/32",
            Lua($"local e = player:getMonstrosityExits({WestRonfaure}) report(#e .. '/' .. e[1][1] .. '/' .. e[1][2] .. '/' .. e[1][3] .. '/' .. e[1][4])"));
        Check("isMonstrosityPassageZone: West Ronfaure, not Lower Jeuno", "true/false",
            Lua($"report(tostring(player:isMonstrosityPassageZone({WestRonfaure})) .. '/' .. tostring(player:isMonstrosityPassageZone(245)))"));

        Console.WriteLine("outside MON: the saved progress");
        Check("getMonstrosityData without a row: a fresh Monipulator (the three starters, the race I instincts)", "1/1/1/1/1/31/0",
            Lua("local d = player:getMonstrosityData() report(d.monstrosityId .. '/' .. d.species .. '/' .. d.levels[1] .. '/' .. d.levels[18] .. '/' .. " +
                "d.levels[43] .. '/' .. d.instincts[20] .. '/' .. d.levels[2])"));
        Check("not a Monipulator: no size, no flag", "0/false", Lua("report(player:getMonstrositySize() .. '/' .. tostring(player:getBelligerencyFlag()))"));
        Lua($"player:setMonstrosityEntryData(-120, -20, -120, 64, {WestRonfaure}, 1, 0)");
        Check("setMonstrosityEntryData: the row written with the entry", $"-120.000/64/{WestRonfaure}/1/0",
            Scalar(db, "SELECT CONCAT(entry_x, '/', entry_rot, '/', entry_zone_id, '/', entry_mjob, '/', entry_sjob) FROM char_monstrosity WHERE charid = ?", BoundValue.Of(CharId)));
        Lua("player:setMonstrosityData({ levels = { [1] = 30 }, instincts = { [21] = 1 } })");
        Check("setMonstrosityData outside MON: the levels set, the instincts OR-ed in, saved", "30/1",
            Lua("local d = player:getMonstrosityData() report(d.levels[1] .. '/' .. d.instincts[21])"));
        Check("loaded back: the rabbit's first level instinct (30) counted in its 2-bit slot", 1 << 2,
            Lua("report(player:getMonstrosityData().instincts[0])"));
        Check("not a Monipulator yet", "False", $"{c.Monstrosity != null}");
        Lua("player:addSpell(1, { silentLog = true }) player:addSpell(159, { silentLog = true }) player:addSpell(898, { silentLog = true })");

        Console.WriteLine("changeJob(MON): the data, MON on both jobs at the species level, the ecosystem, the species HP");
        Lua("player:changeJob(xi.job.MON)");
        int expectedHp = 220 * (int)MobStats.MonipulatorBaseHP(grades, 1, 1, 30) / 100;
        Check("a Monipulator: the rabbit, MON/MON at 30, the job level, Beast", $"True/{SpeciesRabbit}/23/23/30/30/30/{Ecosystem.Beast}",
            $"{c.Monstrosity != null}/{c.Monstrosity?.Species}/{c.MJob}/{c.SJob}/{c.MLvl}/{c.SLvl}/{c.JobLevels[23]}/{c.EcoSystem}");
        Check("the HP: the rabbit's 220% of the WAR/WAR mob formula at 30 and its WAR trait (Max HP Boost +30), no MP", $"{expectedHp + 30}/0", $"{c.MaxHp}/{c.MaxMp}");
        Check("the row saved (TryPopulate)", $"{SpeciesRabbit}/30",
            Scalar(db, "SELECT CONCAT(current_monstrosity_species, '/', ORD(SUBSTRING(levels, 2, 1))) FROM char_monstrosity WHERE charid = ?", BoundValue.Of(CharId)));
        Check("getMonstrositySize: small", "0", Lua("report(player:getMonstrositySize())"));

        Console.WriteLine("zoning in as a Monipulator: Gestation, the look, the speed, the packets");
        Leave();
        Login();
        Check("loaded as a Monipulator: the data, the species state", $"True/30/{Ecosystem.Beast}", $"{c.Monstrosity != null}/{c.MLvl}/{c.EcoSystem}");
        Check("Gestation outside the Feretory, for 18 hours", $"True/{18 * 3600_000L}",
            $"{c.StatusEffects.HasStatusEffect(575)}/{c.StatusEffects.GetStatusEffect(575)?.DurationMs}");
        Check("the rabbit's walk (twice the base speed) and hitbox (1.2)", "100/12", $"{c.AnimationSpeed}/{c.ModelHitboxSize10}");
        var login = loginPackets.Find(p => Opcode(p) == 0x00A);
        Check("GP_SERV_COMMAND_LOGIN: the species model, IsMonstrosity", $"{0x010C}/{0xFFFF}/{0x1F}",
            login == null ? "none" : $"{BitConverter.ToUInt16(login.Buffer, 68)}/{BitConverter.ToUInt16(login.Buffer, 84)}/{BitConverter.ToUInt16(login.Buffer, 126)}");
        var grap = loginPackets.Find(p => Opcode(p) == 0x051);
        Check("0x051 GRAP_LIST: the species model", $"{0x010C}/{0xFFFF}", grap == null ? "none" : $"{BitConverter.ToUInt16(grap.Buffer, 4)}/{BitConverter.ToUInt16(grap.Buffer, 20)}");
        var spawn = loginPackets.Find(p => Opcode(p) == 0x00D);
        Check("0x00D: MonstrosityFlags (0x8000 | the species), the model", $"{0x8001}/{0x010C}",
            spawn == null ? "none" : $"{BitConverter.ToUInt16(spawn.Buffer, 62)}/{BitConverter.ToUInt16(spawn.Buffer, 72)}");
        var page1 = Page(gameOkPackets, 3);
        Check("GAMEOK: MONSTROSITY1 with the species, the flags, the levels", $"{SpeciesRabbit}/{0x0B44}/30/1/1",
            page1 == null ? "none" : $"{BitConverter.ToUInt16(page1.Buffer, 8)}/{BitConverter.ToUInt16(page1.Buffer, 10)}/{page1.Buffer[92 + 1]}/{page1.Buffer[92 + 18]}/{page1.Buffer[92 + 43]}");
        Check("MONSTROSITY1 leaves the purchased instincts out", "0", page1 == null ? "none" : $"{page1.Buffer[28 + 20]}");
        var page2 = Page(gameOkPackets, 4);
        Check("MONSTROSITY2: the purchased instincts (race I, and the bit set above)", $"{0x1F}/1",
            page2 == null ? "none" : $"{page2.Buffer[0x88]}/{page2.Buffer[0x89]}");
        Check("the full update after GAMEOK: both pages twice (the GAMEOK set's and SendFullMonstrosityUpdate's)", "2/2",
            $"{gameOkPackets.Count(p => Opcode(p) == 0x063 && BitConverter.ToUInt16(p.Buffer, 4) == 3)}/{gameOkPackets.Count(p => Opcode(p) == 0x063 && BitConverter.ToUInt16(p.Buffer, 4) == 4)}");
        var status = gameOkPackets.LastOrDefault(p => Opcode(p) == 0x037);
        Check("0x037: monstrosity_info, rank Mon", $"{0x8001}/0", status == null ? "none" : $"{BitConverter.ToUInt16(status.Buffer, 84)}/{status.Buffer[90] & 3}");

        Console.WriteLine("M2a: the rabbit's abilities, traits, skills and combat (WAR/WAR at 30)");
        Check("abilities: its WAR ones but Provoke (Mighty Strikes, Berserk, Defender), and Relinquish", "True/True/True/False/True",
            $"{Has(c.Abilities, 16)}/{Has(c.Abilities, 31)}/{Has(c.Abilities, 33)}/{Has(c.Abilities, 35)}/{Has(c.Abilities, 382)}");
        Check("traits: WAR's at 30 (Double Attack)", "10", $"{c.GetMod(Mod.DOUBLE_ATTACK)}");
        int combat = stats.Growth.GetMaxSkillByRank(1, 30), evasionSkill = stats.Growth.GetMaxSkill(Skill.Evasion, 1, 30);
        Check("the A+ combat skill and WAR's evasion at 30; no divine magic for a WAR", $"{combat}/{evasionSkill}/0",
            $"{c.Monstrosity.CombatSkill}/{c.Monstrosity.EvasionSkill}/{c.WorkingSkills[Skill.DivineMagic]}");
        int att = (int)(8 + c.GetMod(Mod.ATT) + CharStats.Str(c) * 0.75f) + combat;
        int acc = combat + (int)MathF.Floor(CharStats.Dex(c) * 0.75f) + c.GetMod(Mod.ACC);
        int def = 8 + (int)MathF.Floor(CharStats.Vit(c) * stats.Settings.VitDefMultiplier) + 30 + combat + c.GetMod(Mod.DEF);
        int eva = evasionSkill + c.GetMod(Mod.EVA) + CharStats.Agi(c) / 2;
        Check("ATT (STR x 0.75, the combat skill), ACC (DEX x 0.75), DEF, EVA (the species' evasion)",
            $"{att + att * c.GetMod(Mod.ATTP) / 100}/{acc}/{def + def * c.GetMod(Mod.DEFP) / 100}/{eva}",
            $"{stats.AttMain(c)}/{stats.Acc(c, 0, 0)}/{stats.Def(c)}/{CharStats.Eva(c)}");
        Check("no fists (a WAR species), 240 delay a hit, 4000 ms a round, the species damage (30 x 1.5 + 10)", "True/240/4000/55",
            $"{c.MainWeapon == WeaponInfo.Unarmed}/{Melee.BaseDelay(c)}/{Monstrosity.WeaponDelay(c.Monstrosity)}/{Melee.MainWeaponDmg(c)}");
        var mon044 = gameOkPackets.FindAll(p => Opcode(p) == 0x044);
        Check("GAMEOK: 0x044 MON for both jobs with the species (twice: the GAMEOK set's and the full update's)", $"4/23/0/1/{SpeciesRabbit}",
            mon044.Count < 2 ? $"{mon044.Count}" : $"{mon044.Count}/{mon044[0].Buffer[4]}/{mon044[0].Buffer[5]}/{mon044[1].Buffer[5]}/{BitConverter.ToUInt16(mon044[0].Buffer, 8)}");
        var magic = gameOkPackets.LastOrDefault(p => Opcode(p) == 0x0AA);
        Check("0x0AA: a WAR species lists neither Cure nor Stone, the Trust still", "False/False/True",
            magic == null ? "none" : $"{Listed(magic, 1)}/{Listed(magic, 159)}/{Listed(magic, 898)}");
        var cli = gameOkPackets.Concat(loginPackets).LastOrDefault(p => Opcode(p) == 0x061);
        Check("the exp curve: monstrosity's table (3800 at 30), in CLISTATUS", "3800/3800",
            $"{stats.ExpNextLevelFor(c, 30)}/" + (cli == null ? "none" : $"{BitConverter.ToUInt16(cli.Buffer, 18)}"));
        c.JobExp[23] = 500;
        delExp.Invoke(zone, new object[] { c, 0f, (ushort)100 });
        Check("no exp lost on death, even a forced loss", "500", $"{c.JobExp[23]}");
        c.JobExp[23] = 0;

        Console.WriteLine("M2c: the rabbit's Wild Carrot (0x01A MonsterSkill)");
        c.StatusEffects.DelStatusEffect(575); // Gestation blocks every action
        c.Tp = 500;
        s.PacketList.Clear();
        MonsterSkill(c.TargId, 314);
        Check("short of its 1000 TP: unable to use (88), no state", "1/False", $"{Msgs(88)}/{c.SkillState != null}");
        c.Tp = 1500;
        s.PacketList.Clear();
        MonsterSkill(c.TargId, 999);
        Check("a move the species does not have: unable to use", "1/False", $"{Msgs(88)}/{c.SkillState != null}");
        c.Hp = c.MaxHp - 300;
        int hpBefore = c.Hp;
        s.PacketList.Clear();
        MonsterSkill(c.TargId, 314);
        Check("Wild Carrot readies (mob skill 323, its own 3.2 s), no TP spent yet, the readies packet", "True/323/3200/1500/1",
            $"{c.SkillState != null}/{c.SkillState?.Skill.Id}/{c.SkillState?.CastMs}/{c.Tp}/{Of(0x028).Count}");
        MonsterSkill(c.TargId, 314);
        Check("a second move while one readies: refused (CanChangeState)", "3200", $"{c.SkillState?.CastMs}");
        nowMs += 3300;
        s.PacketList.Clear();
        zone.Tick();
        Check("it lands: the 1000 TP paid, HP healed, the finish packet", $"500/True/1",
            $"{c.Tp}/{c.Hp > hpBefore}/{Of(0x028).Count}");
        nowMs += 5100;
        zone.Tick();
        Check("the state over after the animation", "False", $"{c.SkillState != null}");

        Console.WriteLine("changeMonstrositySpecies: refused for one not owned, a new family's level and jobs");
        s.PacketList.Clear();
        Lua($"player:changeMonstrositySpecies({SpeciesBehemoth})");
        Check("a behemoth (not owned): nothing", $"{SpeciesRabbit}/0", $"{c.Monstrosity.Species}/{s.PacketList.Count}");
        c.JobExp[23] = 120;
        Lua($"player:changeMonstrositySpecies({SpeciesMandragora})");
        Check("the mandragora: its family, MNK/MNK at its level 1, the exp reset, Plantoid", $"{SpeciesMandragora}/18/2/2/1/1/0/{Ecosystem.Plantoid}",
            $"{c.Monstrosity.Species}/{c.Monstrosity.MonstrosityId}/{c.Monstrosity.MainJob}/{c.Monstrosity.SubJob}/{c.MLvl}/{c.JobLevels[23]}/{c.JobExp[23]}/{c.EcoSystem}");
        Check("its look and hitbox (0.8)", $"{0x012C}/8", $"{c.Monstrosity.Look}/{c.ModelHitboxSize10}");
        Check("the species change packets: the job table (a new family), both pages, the look", "1/1/1/1",
            $"{Of(0x01B).Count}/{Of(0x063).Count(p => BitConverter.ToUInt16(p.Buffer, 4) == 3)}/{Of(0x063).Count(p => BitConverter.ToUInt16(p.Buffer, 4) == 4)}/{Of(0x051).Count}");
        Check("the row: the mandragora", $"{SpeciesMandragora}/18",
            Scalar(db, "SELECT CONCAT(current_monstrosity_species, '/', current_monstrosity_id) FROM char_monstrosity WHERE charid = ?", BoundValue.Of(CharId)));
        var job044 = Of(0x044);
        var attr = Of(0x0DF).LastOrDefault();
        Check("0x044 MON for both jobs with the mandragora, 0x0DF its species and name", $"2/{SpeciesMandragora}/{0x8012}/{0x8012}",
            $"{job044.Count}/{(job044.Count > 0 ? BitConverter.ToUInt16(job044[0].Buffer, 8) : -1)}/" +
            (attr == null ? "none" : $"{BitConverter.ToUInt16(attr.Buffer, 28)}/{BitConverter.ToUInt16(attr.Buffer, 30)}"));
        s.PacketList.Clear();
        Lua($"player:changeMonstrositySpecies({SpeciesMandragora})");
        Check("the current species again: nothing", "0", $"{s.PacketList.Count}");

        Console.WriteLine("M2a: the mandragora (MNK/MNK at 1): fists, abilities, exp and infamy, the level up");
        Check("fists, 180 delay a hit over two swings (6000 ms), the damage split ((1 x 1.5 + 10) / 2)", "True/180/6000/5",
            $"{c.MainWeapon == WeaponInfo.UnarmedH2H}/{Melee.BaseDelay(c)}/{Monstrosity.WeaponDelay(c.Monstrosity)}/{Melee.MainWeaponDmg(c)}");
        Check("abilities: Hundred Fists and Relinquish, no WAR ones; no Double Attack", "True/True/False/0",
            $"{Has(c.Abilities, 17)}/{Has(c.Abilities, 382)}/{Has(c.Abilities, 31)}/{c.GetMod(Mod.DOUBLE_ATTACK)}");
        string infamyBefore = Points("infamy");
        s.PacketList.Clear();
        KillExp(200);
        Check("a kill's 200 exp: a tenth in infamy, the exp, no level", $"{int.Parse(infamyBefore) + 20}/200/1",
            Points("infamy") + $"/{c.JobExp[23]}/{c.JobLevels[23]}");
        Check("the gain's packets: CLISTATUS, CLISTATUS2, MERITS, MONSTROSITY1, JOB_POINTS", "1/1/1/1/1",
            $"{Of(0x061).Count}/{Of(0x062).Count}/{Of(0x063).Count(p => BitConverter.ToUInt16(p.Buffer, 4) == 2)}/" +
            $"{Of(0x063).Count(p => BitConverter.ToUInt16(p.Buffer, 4) == 3)}/{Of(0x063).Count(p => BitConverter.ToUInt16(p.Buffer, 4) == 5)}");
        s.PacketList.Clear();
        Lua("player:addExp(100)");
        Check("300 exp in all: level 2 for MON, the family, both jobs and the row", "2/2/2/2/2/0",
            $"{c.JobLevels[23]}/{c.Monstrosity.Levels[18]}/{c.MLvl}/{c.SLvl}/" +
            Scalar(db, "SELECT ORD(SUBSTRING(levels, 19, 1)) FROM char_monstrosity WHERE charid = ?", BoundValue.Of(CharId)) + $"/{c.JobExp[23]}");
        Check("no infamy from a script's exp; the level up's message, 0x044 pair, no new instinct", $"{int.Parse(infamyBefore) + 20}/1/2/0",
            Points("infamy") + $"/{Of(0x02D).Count(p => BitConverter.ToUInt16(p.Buffer, 24) == 9)}/{Of(0x044).Count}/{Msgs(678)}");

        Console.WriteLine("M2a: the panopt (BLM/BLM at 20): its magic skills and spells");
        Lua("player:setMonstrosityData({ levels = { [23] = 20 } })");
        Lua("player:changeMonstrositySpecies(23)");
        var panoptMagic = LoginPackets.MagicData(c);
        Check("BLM's elemental magic at 20, Stone listed and Cure not", $"23/20/{stats.Growth.GetMaxSkill(36, 4, 20)}/True/False/True",
            $"{c.Monstrosity.Species}/{c.MLvl}/{c.WorkingSkills[36]}/{Listed(panoptMagic, 159)}/{Listed(panoptMagic, 1)}/{c.WorkingSkills[36] > 0}");

        Console.WriteLine("M2b: the Feretory equipment change (0x102)");
        void Equip(byte flags, Action<byte[]> fill) => Send(0x102, 164, buf => { buf[0x0A] = flags; fill(buf); });
        void Slots(byte[] buf, params ushort[] slots) { for (int i = 0; i < slots.Length; i++) BitConverter.GetBytes(slots[i]).CopyTo(buf, 0x10 + i * 2); }
        string Instincts() => $"{c.Monstrosity.EquippedInstincts[0]}/{c.Monstrosity.EquippedInstincts[1]}";
        int strBefore = c.GetMod(Mod.STR);
        Equip(0x04, b => Slots(b, 768, 769));
        Check("outside the Feretory: refused", "0/0", Instincts());
        c.Zone = 285; // the Feretory
        s.PacketList.Clear();
        Equip(0x04, b => Slots(b, 768, 769));
        Check("in the Feretory: Hume's and Elvaan's instinct I, their STR, the row, the packets", $"768/769/{strBefore + 5}/768/True/1",
            Instincts() + $"/{c.GetMod(Mod.STR)}/" +
            Scalar(db, "SELECT ORD(SUBSTRING(equip, 1, 1)) + 256 * ORD(SUBSTRING(equip, 2, 1)) FROM char_monstrosity WHERE charid = ?", BoundValue.Of(CharId)) +
            $"/{Of(0x044).Count > 0}/{Of(0x063).Count(p => BitConverter.ToUInt16(p.Buffer, 4) == 3)}");
        Equip(0x04, b => Slots(b, 0xFFFF));
        Check("a second change within 250 ms: ignored", "768/769", Instincts());
        nowMs += 300;
        Equip(0x04, b => Slots(b, 770, 770));
        Check("a duplicate: refused", "768/769", Instincts());
        nowMs += 300;
        Equip(0x04, b => Slots(b, 790));
        Check("an instinct not owned: refused", "768/769", Instincts());
        nowMs += 300;
        Equip(0x04, b => Slots(b, 0, 0xFFFF));
        Check("0xFFFF unequips its slot, 0 leaves one alone; the mods follow", $"768/0/{strBefore + 2}", Instincts() + $"/{c.GetMod(Mod.STR)}");
        nowMs += 300;
        Equip(0x08, b => b[0x28] = 5);
        Check("a name descriptor, saved", "5/5", $"{c.Monstrosity.NamePrefix1}/" +
            Scalar(db, "SELECT current_monstrosity_name_prefix_1 FROM char_monstrosity WHERE charid = ?", BoundValue.Of(CharId)));
        nowMs += 300;
        Equip(0x01, b => BitConverter.GetBytes(SpeciesMandragora).CopyTo(b, 0x0C));
        Check("a species change: the mandragora again", $"{SpeciesMandragora}/2", $"{c.Monstrosity.Species}/{c.MLvl}");
        c.Zone = WestRonfaure;

        Console.WriteLine("M2c: Fighting Stance (the wamouracampa)");
        nowMs += 300;
        Lua("player:setMonstrosityData({ levels = { [36] = 10 } })");
        Lua("player:changeMonstrositySpecies(36)");
        c.Tp = 1000;
        s.PacketList.Clear();
        MonsterSkill(c.TargId, 684);
        Check("an instant move: the stance species (306), its 300 TP, the look packets", "36/306/700/1/True",
            $"{c.Monstrosity.Species}/{c.Monstrosity.StanceSpecies}/{c.Tp}/{Of(0x051).Count}/{Of(0x037).Count > 0}");
        nowMs += 2000;
        zone.Tick();
        c.Tp = 1000;
        MonsterSkill(c.TargId, 684);
        Check("again: back to its own form", "0", $"{c.Monstrosity.StanceSpecies}");
        nowMs += 2000;
        zone.Tick();
        Console.WriteLine("the Belligerency flag and the rank infamy gives");
        Lua("player:setCurrency('infamy', 15000)");
        Check("infamy outside Belligerency: rank Mon", "0", $"{c.Monstrosity.Rank}");
        s.PacketList.Clear();
        Lua("player:setBelligerencyFlag(true)");
        Check("flagged: the flag, rank NM at 15000 infamy, the status packet, the row", "true/1/1/1",
            Lua("report(tostring(player:getBelligerencyFlag()))") + $"/{c.Monstrosity.Rank}/{Of(0x037).Count}/" +
            Scalar(db, "SELECT belligerency FROM char_monstrosity WHERE charid = ?", BoundValue.Of(CharId)));
        Lua("player:setCurrency('infamy', 25000)");
        Check("spending or gaining infamy moves the rank (SetPoints): HNM at 25000", "2", $"{c.Monstrosity.Rank}");
        Lua("player:setCurrency('infamy', 500) player:setBelligerencyFlag(false)");
        Check("unflagged: rank Mon", "0/false", $"{c.Monstrosity.Rank}/" + Lua("report(tostring(player:getBelligerencyFlag()))"));

        Console.WriteLine("setMonstrosityData as a Monipulator: saved and sent in full");
        s.PacketList.Clear();
        Lua("player:setMonstrosityData({ levels = { [18] = 12 } })");
        Check("the level in the live data and the row, the full update's pages", "12/12/1",
            $"{c.Monstrosity.Levels[18]}/" + Scalar(db, "SELECT ORD(SUBSTRING(levels, 19, 1)) FROM char_monstrosity WHERE charid = ?", BoundValue.Of(CharId)) +
            $"/{Of(0x063).Count(p => BitConverter.ToUInt16(p.Buffer, 4) == 3)}");

        Console.WriteLine("M3: no party, the idle regen, /check, the death menu, Belligerency");
        ipc.Clear();
        s.PacketList.Clear();
        Send(0x06E, 12, b => { BitConverter.GetBytes(994999u).CopyTo(b, 4); BitConverter.GetBytes((ushort)0x500).CopyTo(b, 8); });
        int invites = ipc.Count(m => m.GetType().Name == "PartyInvite");
        Check("a party invite: refused (CannotInvite), nothing sent on", "0/1", $"{invites}/{Of(0x009).Count}");
        Send(0x11C, 12, b => BitConverter.GetBytes(994999u).CopyTo(b, 4));
        int requests = ipc.Count(m => m.GetType().Name == "PartyRequest");
        bool refusedForMon = log.Exists(l => l.Contains("Cannot request while in Monstrosity"));
        Check("a party request: refused for being in Monstrosity", "0/True", $"{requests}/{refusedForMon}");
        c.Hp = c.MaxHp / 2;
        int regenFrom = c.Hp;
        nowMs += 3100;
        zone.Tick();
        Check("the idle regen: 5% of the HP a tick out of combat", "True", $"{c.Hp >= regenFrom + c.MaxHp * 5 / 100}");
        s.PacketList.Clear();
        Send(0x0DD, 16, b => { BitConverter.GetBytes(CharId).CopyTo(b, 4); BitConverter.GetBytes((uint)c.TargId).CopyTo(b, 8); b[12] = 0; });
        var gauge = Of(0x029).FirstOrDefault();
        Check("/check on a Monipulator: checked like a monster (its level, a check message), the examine message", $"{c.MLvl}/True/1",
            gauge == null ? "none" : $"{BitConverter.ToUInt32(gauge.Buffer, 12)}/{BitConverter.ToUInt16(gauge.Buffer, 24) is >= 170 and <= 178}/{Of(0x009).Count}");
        s.PacketList.Clear();
        Send(0x0DD, 16, b => { BitConverter.GetBytes(CharId).CopyTo(b, 4); BitConverter.GetBytes((uint)c.TargId).CopyTo(b, 8); b[12] = 1; });
        Check("/checkname on a Monipulator: the possessed and the stares messages", "2", $"{Of(0x009).Count}");
        Lua("player:setBelligerencyFlag(true)");
        c.Zone = 118; // Buburimu Peninsula
        Check("flagged in Buburimu: in Belligerency, no gladiator, no belligerent of itself", "True/False/False",
            $"{Monstrosity.IsMonipulatorInBelligerency(c)}/{Monstrosity.IsGladiatorInBelligerency(c)}/{Monstrosity.AreBelligerents(c, c)}");
        c.Zone = WestRonfaure;
        Lua("player:setBelligerencyFlag(false)");
        lua.DoString("xi.monstrosity = xi.monstrosity or {} xi.monstrosity.onMonstrosityReturnToEntrance = function(p) report('entrance') end", "=verify_monstrosity");
        c.Hp = 0;
        zone.Tick();
        got.Clear();
        Send(0x01A, 28, b =>
        {
            BitConverter.GetBytes(CharId).CopyTo(b, 4);
            BitConverter.GetBytes(c.TargId).CopyTo(b, C2sAction.OffActIndex);
            BitConverter.GetBytes((ushort)ActionId.HomepointMenu).CopyTo(b, C2sAction.OffActionID);
            C2sAction.SetHomepointMenuStatus(b, HomepointMenuStatus.MonstrosityCancel);
        });
        Check("the death menu's Cancel: back on its feet, to the Feretory's entrance (the script), no home point", $"entrance/{c.MaxHp}/0/None",
            string.Join(",", got) + $"/{c.Hp}/{c.Animation}/{c.RequestedWarp}");
        c.Hp = 0;
        zone.Tick();
        got.Clear();
        nowMs += 3600_000 + 1000;
        zone.Tick(); // the 16 s death sync goes first
        zone.Tick();
        Check("the death timer running out: Cancel too, not the home point", $"entrance/{c.MaxHp}/None",
            string.Join(",", got) + $"/{c.Hp}/{c.RequestedWarp}");

        Console.WriteLine("changeJob back out: the data dropped, a humanoid again");
        Lua("player:changeJob(xi.job.WAR)");
        Check("out of MON: no data, WAR, Humanoid, the base walk and the race hitbox", $"False/1/{Ecosystem.Humanoid}/50/4",
            $"{c.Monstrosity != null}/{c.MJob}/{c.EcoSystem}/{c.AnimationSpeed}/{c.ModelHitboxSize10}");
        Check("the progress stays saved", "12", Lua("report(player:getMonstrosityData().levels[18])"));

        Console.WriteLine("M2a: a gladiator's prestige");
        Lua("player:addKeyItem(2351)"); // xi.ki.GLADIATORIAL_WRIT_OF_SUMMONS
        c.Zone = 118; // Buburimu Peninsula, a Belligerency zone
        KillExp(1000);
        var info = zone.ZoneInfoOf(WestRonfaure);
        var pc = LoginPackets.CharPc(c, info, EntityUpdate.Spawn, 0);
        var st = LoginPackets.ServerStatus(c, info, 0);
        Check("a gladiator in Buburimu: team 0 (0x08) in 0x00D's BallistaTeam and 0x037's BallistaFlg", "8/8",
            $"{BitConverter.ToUInt32(pc.Buffer, 40) >> 8 & 0xFF}/{BitConverter.ToUInt32(st.Buffer, 52) >> 21 & 0xFF}");
        Lua("player:addItem(16535)"); // a bronze sword
        var sword = c.Items.Find(i => i.ItemId == 16535 && i.Location == Container.Inventory);
        Send(0x050, 8, b => { b[4] = sword?.Slot ?? 0; b[5] = 0; b[6] = 0; });
        Check("its gear change: every slot encumbered for 10 s (Encumbrance II)", $"True/True/{10_000}",
            $"{c.Equipped[0] != null}/{c.StatusEffects.HasStatusEffect(177)}/{c.StatusEffects.GetStatusEffect(177)?.DurationMs}");
        c.Zone = WestRonfaure;
        KillExp(1000);
        Check("a kill with the writ in Buburimu: a tenth in prestige (none in West Ronfaure), no infamy", "100/500",
            Points("prestige") + "/" + Points("infamy"));

        Check("no Lua errors", "", string.Join(" | ", log.FindAll(l => (l.StartsWith("lua") || l.Contains("luautils") || l.Contains("rror")) && !l.Contains("global scripts"))));
    }
}
