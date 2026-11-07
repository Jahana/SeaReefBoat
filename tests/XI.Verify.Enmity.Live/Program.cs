using System;
using System.Collections.Generic;
using System.IO;
using XI.Common;
using XI.Map;
using XI.Ipc;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Enmity.Live -- live gate for the enmity sources outside the melee path: updateEnmityFromCure through
// battleutils::GenerateCureEnmity and CEnmityContainer::UpdateEnmityFromCure (GetEnmityModCure, fixed CE / VE, Tranquil
// Heart, the Enmity Increase merit in CalculateEnmityBonus), the getCE / getVE / setCE / setVE / addBaseEnmity /
// lowerEnmity / setEnmityActive / clearEnmityForEntity / getEnmityList / transferEnmity / hasClaim / hasEnmity /
// getNotorietyList bindings, MOBMOD_ALLI_HATE on engage and handleKillshotEnmity after a mob's killing swing; the
// notoriety containers kept in step with the enmity lists (CNotorietyContainer), a trust as a curer and a trust on top
// of the list claiming for its master, CMobEntity::FadeOut's clear. A WHM 75 and a WAR partner on a Wild Rabbit in
// West Ronfaure, with the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint HealerId = 993400, FighterId = 993401, LoggerId = 995921;
    private const ushort WestRonfaure = 100;
    private const uint Rabbit = 17186822;
    private const uint Kupipi = 898;
    private const ushort MeritEnmityIncrease = 320;
    private const long Never = long.MaxValue / 4;

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
        foreach (var id in new[] { HealerId, FighterId, LoggerId })
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

    private static void AddChar(IDatabase db, uint id, string name, byte job, string jobCol)
    {
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of("enmv" + id), BoundValue.Of("x"), BoundValue.Of((byte)1));
        // a Hume with the NEW_ADVENTURER title (not a first login: no charCreate), standing at the zone's entrance (a
        // char at 0,0,0 is moved there by the zone's onZoneIn)
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,titles) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(name), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(-126.0f), BoundValue.Of(-62.0f), BoundValue.Of(273.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0), BoundValue.Of(titles));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(id));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 1500, 50, ?, 75)", BoundValue.Of(id), BoundValue.Of(job));
        Sql(db, $"REPLACE INTO char_jobs(charid, {jobCol}) VALUES (?, 75)", BoundValue.Of(id));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(id));
    }

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        AddChar(db, HealerId, "Healy", 3, "whm");
        AddChar(db, FighterId, "Fighty", 1, "war");
        AddChar(db, LoggerId, "Loggy", 1, "war");
        // healing magic 200
        Sql(db, "INSERT INTO char_skills(charid, skillid, value, rank) VALUES (?, 33, 2000, 0)", BoundValue.Of(HealerId));

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        zone.VanaTicksNow = () => 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        zone.MobRng = new Random(5);
        var queue = new Queue<IIpcMessage>();
        zone.SendIpc = m => queue.Enqueue(m);
        void Pump()
        {
            for (int round = 0; round < 8; round++)
            {
                while (queue.Count > 0) zone.OnIpc(IpcFraming.Decode(IpcFraming.ToBytesWithHeader(queue.Dequeue())));
                zone.Tick();
                if (queue.Count == 0) return;
            }
        }
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);

        var info = zone.ZoneInfoOf(WestRonfaure);
        var rabbit = info.MobById[Rabbit];
        Check("rabbit up", true, rabbit.IsSpawned);

        var sh = new MapSession { CharId = HealerId, AccountId = HealerId };
        var sf = new MapSession { CharId = FighterId, AccountId = FighterId };
        zone.OnLogin(sh);
        zone.OnLogin(sf);
        var h = (CharData)sh.Char;
        var f = (CharData)sf.Char;
        void Send(MapSession ss, ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(ss, opcode, new BasicPacket(buf));
            zone.AfterParse(ss);
        }
        foreach (var (ss, id) in new[] { (sh, HealerId), (sf, FighterId) })
        {
            Send(ss, 0x00A, 92, buf => BitConverter.GetBytes(id).CopyTo(buf, 12));
            Send(ss, 0x00C, 12);
        }
        Pump();
        void Pos(MapSession ss, float dx, float dz)
        {
            Send(ss, 0x015, 32, p =>
            {
                BitConverter.GetBytes(rabbit.X + dx).CopyTo(p, C2sPos.OffX);
                BitConverter.GetBytes(rabbit.Y).CopyTo(p, C2sPos.OffZ);
                BitConverter.GetBytes(rabbit.Z + dz).CopyTo(p, C2sPos.OffY);
            });
            ss.PacketList.Clear();
        }
        Pos(sh, -4, 0);
        Pos(sf, -2, 0);
        Pump();

        var lua = scripts.Lua;
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local healer = GetPlayerByID({HealerId})\nlocal fighter = GetPlayerByID({FighterId})\nlocal mob = GetMobByID({Rabbit})\n" + code, "=verify_enmity");
            return string.Join(",", got);
        }

        Console.WriteLine("Setup: the fighter on the rabbit's list, the two in a party");
        Send(sh, 0x06E, 12, p =>
        {
            BitConverter.GetBytes(f.Id).CopyTo(p, 4);
            BitConverter.GetBytes(f.TargId).CopyTo(p, 8);
        });
        Send(sf, 0x074, 8, p => p[4] = 1);
        Pump();
        var party = Sql(db, "SELECT COUNT(*) AS n FROM accounts_parties WHERE partyid = ? AND charid IN (?, ?)", BoundValue.Of(HealerId), BoundValue.Of(HealerId), BoundValue.Of(FighterId));
        Check("in one party", 2L, party != null && party.Next() ? party.Get<long>("n") : -1);
        rabbit.Enmity.UpdateEnmityFromDamage(f, 10);
        Check("the rabbit has fought a PC (m_HiPCLvl)", (byte)75, rabbit.HiPcLvl);
        Check("the healer not on the list yet", false, rabbit.Enmity.HasId(h.Id));
        Check("WHM 75 has Tranquil Heart (trait 114)", true, CharStats.HasTrait(h, 114));
        float th = EnmityContainer.TranquilHeart(h);
        Check("Tranquil Heart: 0.5% per 10 healing skill, 25% at most", Math.Min(CharStats.GetSkill(h, 33) / 10.0f * 0.5f, 25.0f) / 100.0f, th);
        Check("Tranquil Heart cuts something", true, th > 0);

        Console.WriteLine("battleutils::GetEnmityModCure");
        Check("level 5", 15, EnmityContainer.EnmityModCure(5));
        Check("level 10", 20, EnmityContainer.EnmityModCure(10));
        Check("level 30", 30, EnmityContainer.EnmityModCure(30));
        Check("level 50", 40, EnmityContainer.EnmityModCure(50));
        Check("level 75", 55, EnmityContainer.EnmityModCure(75));
        Check("level 99", 69, EnmityContainer.EnmityModCure(99));

        Console.WriteLine("updateEnmityFromCure: the curer hated by the mobs fighting the target");
        Lua("healer:updateEnmityFromCure(healer, 100)");
        Check("curing someone the rabbit does not hate: nothing", false, rabbit.Enmity.HasId(h.Id));
        Lua("healer:updateEnmityFromCure(fighter, 100)");
        int ce = (int)(40.0f / 55 * 100 * 1.0f * (1.0f - th)), ve = (int)(240.0f / 55 * 100 * 1.0f * (1.0f - th));
        Check("cure 100 at level 75: CE", ce, rabbit.Enmity.CE(h));
        Check("cure 100 at level 75: VE", ve, rabbit.Enmity.VE(h));
        Check("the entry is active", true, rabbit.Enmity.Get(h.Id)?.Active);
        Lua("healer:updateEnmityFromCure(fighter, 0)");
        Check("a 0 cure counts as 1", ce + (int)(40.0f / 55 * 1 * (1.0f - th)), rabbit.Enmity.CE(h));
        int ce1 = rabbit.Enmity.CE(h), ve1 = rabbit.Enmity.VE(h);
        Lua("healer:updateEnmityFromCure(fighter, 500, 100, 300)");
        Check("fixed CE", ce1 + (int)(100 * (1.0f - th)), rabbit.Enmity.CE(h));
        Check("fixed VE", ve1 + (int)(300 * (1.0f - th)), rabbit.Enmity.VE(h));
        ce1 = rabbit.Enmity.CE(h);
        Lua("healer:updateEnmityFromCure(fighter, -5)");
        Check("a negative cure does nothing", ce1, rabbit.Enmity.CE(h));
        Check("a negative cure warns", true, log.Contains("Received negative cure amount."));
        rabbit.HiPcLvl = 0;
        Lua("healer:updateEnmityFromCure(fighter, 100)");
        Check("a mob that never fought a PC: nothing", ce1, rabbit.Enmity.CE(h));
        rabbit.HiPcLvl = 75;
        Lua("mob:updateEnmityFromCure(fighter, 100)");
        Check("a mob curer: nothing", false, rabbit.Enmity.HasId(rabbit.Id));
        Pos(sh, -40, 0);
        Lua("healer:updateEnmityFromCure(fighter, 100)");
        Check("out of enmity range (25): nothing", ce1, rabbit.Enmity.CE(h));
        Pos(sh, -4, 0);

        Console.WriteLine("CalculateEnmityBonus: the Enmity Increase merit");
        Sql(db, "INSERT INTO char_merit(charid, meritid, upgrades) VALUES (?, ?, 3)", BoundValue.Of(HealerId), BoundValue.Of(MeritEnmityIncrease));
        h.Merits.Counts[h.Merits.Table.IndexOf(MeritEnmityIncrease)] = 3;
        int merit = h.Merits.Value(h, MeritEnmityIncrease);
        Check("Enmity Increase 3 upgrades counted", true, merit > 0);
        ce1 = rabbit.Enmity.CE(h);
        Lua("healer:updateEnmityFromCure(fighter, 0, 100, 0)");
        Check("fixed CE 100 with the merit's bonus", ce1 + (int)(100 * ((100.0f + merit) / 100.0f) * (1.0f - th)), rabbit.Enmity.CE(h));
        h.Merits.Counts[h.Merits.Table.IndexOf(MeritEnmityIncrease)] = 0;

        Console.WriteLine("getCE / getVE / setCE / setVE / addBaseEnmity");
        Check("getCE", rabbit.Enmity.CE(h).ToString(), Lua("report(mob:getCE(healer))"));
        Check("getVE", rabbit.Enmity.VE(h).ToString(), Lua("report(mob:getVE(healer))"));
        Lua("mob:setCE(healer, 1234) mob:setVE(healer, 40000)");
        Check("setCE", 1234, rabbit.Enmity.CE(h));
        Check("setVE: capped at the enmity cap", 30000, rabbit.Enmity.VE(h));
        Check("getCE on a char: 0 with a warning", "0", Lua("report(healer:getCE(fighter))"));
        Check("the getCE warning", true, log.Exists(x => x.StartsWith("Attempting to get CE for invalid entity type")));
        Lua("mob:clearEnmityForEntity(healer)");
        Check("clearEnmityForEntity", false, rabbit.Enmity.HasId(h.Id));
        Lua("mob:setCE(healer, 500)");
        Check("setCE on someone not listed: a base entry first", "500/0", $"{rabbit.Enmity.CE(h)}/{rabbit.Enmity.VE(h)}");
        Lua("mob:clearEnmityForEntity(healer) mob:addBaseEnmity(healer)");
        Check("addBaseEnmity: 0 / 0, active", "0/0/True", $"{rabbit.Enmity.CE(h)}/{rabbit.Enmity.VE(h)}/{rabbit.Enmity.Get(h.Id)?.Active}");

        Console.WriteLine("lowerEnmity / setEnmityActive / getEnmityList");
        Lua("mob:setCE(healer, 1000) mob:setVE(healer, 2001)");
        Lua("mob:lowerEnmity(healer, 30)");
        Check("lowerEnmity 30%", "700/1401", $"{rabbit.Enmity.CE(h)}/{rabbit.Enmity.VE(h)}");
        Lua("mob:setEnmityActive(healer, false)");
        Check("setEnmityActive false", false, rabbit.Enmity.Get(h.Id)?.Active);
        var listed = Lua("for _, e in ipairs(mob:getEnmityList()) do report(e.entity:getName() .. ':' .. e.ce .. ':' .. e.ve .. ':' .. tostring(e.active) .. ':' .. tostring(e.tameable)) end");
        Check("getEnmityList has the healer", true, listed.Contains($"Healy:700:1401:false:true"));
        Check("getEnmityList has the fighter", true, listed.Contains($"Fighty:{rabbit.Enmity.CE(f)}:{rabbit.Enmity.VE(f)}:true:true"));
        Lua("mob:setEnmityActive(healer, true)");
        Check("setEnmityActive true", true, rabbit.Enmity.Get(h.Id)?.Active);

        Console.WriteLine("tameable: other enmity than a claim ends it, taking one off the list restores it");
        string Tameable() => Lua("report(tostring(mob:getEnmityList()[1].tameable))");
        Check("since the healer came off the list (setCE / addBaseEnmity add none): tameable", "true", Tameable());
        Lua("mob:addEnmity(healer, 1, 1)");
        Check("addEnmity (UpdateEnmity): not tameable", "false", Tameable());
        Lua("mob:clearEnmityForEntity(healer) mob:setCE(healer, 700) mob:setVE(healer, 1401)");
        Check("clearEnmityForEntity: tameable again", "true", Tameable());
        rabbit.Enmity.UpdateEnmity(h, 0, 0, withMaster: true, tameable: true);
        Check("a claim's UpdateEnmity (tameable): still tameable", "true", Tameable());

        Console.WriteLine("transferEnmity: the fighter hands half its enmity to the healer");
        Lua("mob:setCE(fighter, 1000) mob:setVE(fighter, 600) mob:setCE(healer, 0) mob:setVE(healer, 0)");
        Check("the rabbit is spawned for the fighter", true, f.SpawnMobList.Contains(Rabbit));
        Lua("fighter:transferEnmity(healer, 50, 20)");
        Check("the fighter lost half", "500/300", $"{rabbit.Enmity.CE(f)}/{rabbit.Enmity.VE(f)}");
        Check("the healer gained it (UpdateEnmity, no bonus)", "500/300", $"{rabbit.Enmity.CE(h)}/{rabbit.Enmity.VE(h)}");
        Lua("fighter:transferEnmity(healer, 50, 1)");
        Check("the rabbit farther than the range from the healer: nothing", "500/300", $"{rabbit.Enmity.CE(f)}/{rabbit.Enmity.VE(f)}");

        Console.WriteLine("hasClaim / hasEnmity / getNotorietyList");
        rabbit.OwnerId = f.Id;
        Check("hasClaim: the claimer", "true", Lua("report(tostring(fighter:hasClaim(mob)))"));
        Check("hasClaim: a party member", "true", Lua("report(tostring(healer:hasClaim(mob)))"));
        rabbit.OwnerId = 0;
        Check("hasClaim: unclaimed", "false", Lua("report(tostring(healer:hasClaim(mob)))"));
        Check("hasEnmity: on the list", "true", Lua("report(tostring(healer:hasEnmity()))"));
        Check("getNotorietyList: the rabbit", Rabbit.ToString(), Lua("for _, m in ipairs(healer:getNotorietyList()) do report(m:getID()) end"));
        Lua("mob:clearEnmityForEntity(healer)");
        Check("hasEnmity: off the list", "false", Lua("report(tostring(healer:hasEnmity()))"));
        Check("getNotorietyList: empty", "", Lua("for _, m in ipairs(healer:getNotorietyList()) do report(m:getID()) end"));

        Console.WriteLine("CNotorietyContainer: kept in step with the enmity lists");
        Check("off the rabbit's list: out of the healer's notoriety", false, h.Notoriety.Contains(rabbit));
        Check("the fighter's notoriety: the rabbit alone", "True/1", $"{f.Notoriety.Contains(rabbit)}/{f.Notoriety.Count}");
        Lua("mob:addBaseEnmity(healer)");
        Check("addBaseEnmity: into the healer's notoriety", true, h.Notoriety.Contains(rabbit));
        Lua("mob:clearEnmityForEntity(healer) healer:updateEnmityFromCure(fighter, 100)");
        Check("a cure's new entry: into the healer's notoriety", "True/True", $"{rabbit.Enmity.HasId(h.Id)}/{h.Notoriety.Contains(rabbit)}");
        MobEntity other = null;
        foreach (var mm in info.MobById.Values)
            if (mm != rabbit && mm.IsSpawned && mm.Allegiance == rabbit.Allegiance)
            {
                other = mm;
                break;
            }
        rabbit.Enmity.AddBaseEnmity(other);
        Check("a mob of the rabbit's allegiance on its list stays out of the notoriety", "True/0", $"{rabbit.Enmity.HasId(other.Id)}/{other.Notoriety.Count}");
        rabbit.Enmity.Clear(other.Id);
        Lua("mob:setCE(healer, 20000)");
        rabbit.Enmity.GetHighestEnmity(e => e != h);
        Check("GetHighestEnmity striking the healer off leaves the notoriety stale (as C++)", "False/True", $"{rabbit.Enmity.HasId(h.Id)}/{h.Notoriety.Contains(rabbit)}");
        Check("hasEnmity weeds the stale entry out", "false/0", Lua("report(tostring(healer:hasEnmity()))") + "/" + h.Notoriety.Count);

        Console.WriteLine("MOBMOD_ALLI_HATE: the alliance near the target on engage");
        rabbit.SetMobMod(MobMod.AlliHate, 10);
        Lua("mob:updateEnmity(fighter)");
        Check("the rabbit engaged the fighter", true, rabbit.Engaged && rabbit.BattleTarget == f);
        Check("the healer (2 from the fighter) got base enmity", "True/0/0", $"{rabbit.Enmity.HasId(h.Id)}/{rabbit.Enmity.CE(h)}/{rabbit.Enmity.VE(h)}");
        Check("hasEnmity: the battle target", "true", Lua("report(tostring(fighter:hasEnmity()))"));

        Console.WriteLine("Cover: the healer steps in front of the fighter");
        rabbit.SetMobMod(MobMod.AlliHate, 0);
        rabbit.SetModifier(Mod.ACC, 5000);
        Pos(sf, -2.2f, 0);
        Pos(sh, -1.0f, 0);
        h.LocalVars["COVER_ABILITY_TARGET"] = f.Id;
        h.StatusEffects.AddStatusEffect(new StatusEffect(114, 114, 0, 0, 0)); // EFFECT_COVER
        h.SetModifier(Mod.COVER_TO_MP, 100);
        h.Mp = 0;
        f.Hp = f.MaxHp;
        int healerHp = h.Hp;
        Lua("mob:setCE(fighter, 5000) mob:setVE(fighter, 0) mob:setCE(healer, 0) mob:setVE(healer, 0)");
        void Swing()
        {
            rabbit.SwingAtMs = now;
            now += 400;
            zone.Tick();
        }
        Swing();
        int covered = rabbit.Enmity.CE(h) / 200;
        Check("in line and nearer: the healer takes the swings, the fighter none", $"True/{f.MaxHp}", $"{h.Hp < healerHp}/{f.Hp}");
        Check("200 CE to the cover user a covered swing", true, covered > 0 && rabbit.Enmity.CE(h) == covered * 200);
        Check("10% off the covered fighter a covered swing", true, Math.Abs(5000 * Math.Pow(0.9, covered) - rabbit.Enmity.CE(f)) <= 2);
        Check("COVER_TO_MP 100: the damage back as MP", true, h.Mp > 0);
        Check("the rabbit still fights the fighter", true, rabbit.BattleTarget == f);

        Pos(sh, -1.0f, 2.0f);
        Lua("mob:setCE(healer, 0) mob:setVE(healer, 0)");
        healerHp = h.Hp;
        Swing();
        Check("off the line: the fighter takes it, the healer untouched", $"0/{healerHp}", $"{rabbit.Enmity.CE(h)}/{h.Hp}");
        Pos(sh, -1.0f, 0);
        h.StatusEffects.DelStatusEffectSilent(114);
        f.Hp = f.MaxHp;
        healerHp = h.Hp;
        Swing();
        Check("Cover worn off: the fighter takes it", healerHp, h.Hp);

        Console.WriteLine("Cover: a single-target mob skill");
        h.StatusEffects.AddStatusEffect(new StatusEffect(114, 114, 0, 0, 0));
        h.LocalVars["COVER_ABILITY_TARGET"] = f.Id; // the removal above zeroed it (effects/cover.lua onEffectLose)
        f.Hp = f.MaxHp;
        healerHp = h.Hp;
        rabbit.Tp = 0;
        rabbit.SwingAtMs = now + 60_000; // no melee meanwhile: only the skill can land
        sh.PacketList.Clear();
        Lua("mob:useMobAbility(257, fighter)"); // Foot Kick
        for (int t = 0; t < 15 && rabbit.SkillState == null; t++) { now += 400; zone.Tick(); }
        for (int t = 0; t < 15 && rabbit.SkillState != null; t++) { now += 400; zone.Tick(); }
        // the finish (BATTLE2 category 11, the mob skill finish): its one target, whether it hit or missed
        var finish = new List<BasicPacket>(sh.PacketList).FindAll(p => (p.Buffer[0] | (p.Buffer[1] & 1) << 8) == 0x028 &&
                                                                       Battle2Packets.Unpack(p.Buffer, 82, 4) == 11);
        Check("Foot Kick at the fighter lands on the healer: its one target, the fighter untouched", $"1/1/{h.Id}/{f.MaxHp}",
            $"{finish.Count}/{(finish.Count == 1 ? Battle2Packets.Unpack(finish[0].Buffer, 72, 6) : 0)}/{(finish.Count == 1 ? Battle2Packets.Unpack(finish[0].Buffer, 150, 32) : 0)}/{f.Hp}");
        h.StatusEffects.DelStatusEffectSilent(114);
        h.LocalVars.Remove("COVER_ABILITY_TARGET");
        h.SetModifier(Mod.COVER_TO_MP, 0);
        h.Hp = h.MaxHp;
        Pos(sh, -4, 0);
        Pos(sf, -2, 0);

        Console.WriteLine("handleKillshotEnmity: the killing swing takes the dead top of the list off");
        Lua("mob:setCE(fighter, 5000) mob:setCE(healer, 10)");
        f.Hp = 1;
        rabbit.SetModifier(Mod.ACC, 5000);
        rabbit.SwingAtMs = now;
        bool dead = false;
        for (int t = 0; t < 20 && !dead; t++)
        {
            now += 400;
            zone.Tick();
            dead = f.Hp == 0;
            if (!dead) rabbit.SwingAtMs = Math.Min(rabbit.SwingAtMs, now);
        }
        Check("the fighter died", true, dead);
        Check("the fighter came off the list", false, rabbit.Enmity.HasId(f.Id));
        Check("the healer stayed", true, rabbit.Enmity.HasId(h.Id));

        Console.WriteLine("Trusts: a trust curer, a trust on top of the list claims for its master");
        Lua($"kupipi = healer:spawnTrust({Kupipi})");
        var kupipi = h.PTrusts.Count > 0 ? h.PTrusts[0] : null;
        Check("Kupipi out for the healer", true, kupipi != null);
        Lua("kupipi:updateEnmityFromCure(healer, 100)");
        float trustBonus = (100.0f + Math.Clamp((int)kupipi.GetMod(Mod.ENMITY), -50, 100)) / 100.0f;
        Check("a trust curer: hated by the rabbit fighting the healer (no Tranquil Heart)",
            $"{(int)(40.0f / 55 * 100 * trustBonus * 1.0f)}/{(int)(240.0f / 55 * 100 * trustBonus * 1.0f)}", $"{rabbit.Enmity.CE(kupipi)}/{rabbit.Enmity.VE(kupipi)}");
        Check("the trust's notoriety: the rabbit", true, kupipi.Notoriety.Contains(rabbit));
        rabbit.OwnerId = 0;
        h.ClaimedMob = null;
        Lua("mob:setCE(kupipi, 30000) mob:updateClaim(healer)");
        Check("the trust on top: the master's claim", h.Id, rabbit.OwnerId);
        Lua("mob:clearEnmityForEntity(kupipi)");
        Check("off the list: out of the trust's notoriety", 0, kupipi.Notoriety.Count);
        kupipi.Enmity.UpdateEnmity(rabbit, 100, 100);
        Check("a trust holds no enmity of its own (UpdateEnmity on its list does nothing)", 0, kupipi.Enmity.Count);
        Lua("healer:clearTrusts()");

        Console.WriteLine("LogoutReset: a char's zone-out leaves its entry without an owner; back in, the mob finds it by id");
        (MapSession, CharData) LogIn()
        {
            var sl = new MapSession { CharId = LoggerId, AccountId = LoggerId };
            zone.OnLogin(sl);
            Send(sl, 0x00A, 92, buf => BitConverter.GetBytes(LoggerId).CopyTo(buf, 12));
            Send(sl, 0x00C, 12);
            Pos(sl, -3, 1);
            return (sl, (CharData)sl.Char);
        }
        var (sl1, lg) = LogIn();
        Lua($"local l = GetPlayerByID({LoggerId}) mob:updateEnmity(l) mob:setCE(l, 20000)");
        rabbit.OwnerId = lg.Id;
        now += 400;
        zone.Tick();
        Check("the rabbit on the char: its target, its claim, its list", "True/True/True",
            $"{rabbit.BattleTarget == lg}/{rabbit.OwnerId == lg.Id}/{rabbit.Enmity.Get(LoggerId)?.Owner == lg}");
        zone.OnCharTimeout(sl1, false);
        var logged = rabbit.Enmity.Get(LoggerId);
        Check("logged out: the entry kept without its owner, the claim and the target let go", "True/True/0/False",
            $"{logged != null}/{logged?.Owner == null}/{rabbit.OwnerId}/{rabbit.BattleTarget == lg}");
        var (sl2, lg2) = LogIn();
        now += 400;
        zone.Tick();
        Check("back in: the rabbit turns on the char of that id (a new entity)", "True/False/20000",
            $"{rabbit.BattleTarget == lg2}/{lg2 == lg}/{rabbit.Enmity.CE(lg2)}");
        rabbit.Enmity.UpdateEnmity(lg2, 0, 0);
        Check("UpdateEnmity takes the owner back", true, rabbit.Enmity.Get(LoggerId)?.Owner == lg2);
        zone.OnCharTimeout(sl2, false);
        now += 400;
        zone.Tick();
        Check("gone for good: GetHighestEnmity strikes the ownerless entry off", "False/False",
            $"{rabbit.Enmity.HasId(LoggerId)}/{rabbit.BattleTarget == lg2}");

        Console.WriteLine("CMobEntity::FadeOut: the list goes, and the mob from its targets' notoriety");
        Check("before: the rabbit in the healer's notoriety", true, h.Notoriety.Contains(rabbit));
        rabbit.FadeOut();
        Check("after the fade: the rabbit's list and the healer's notoriety empty", "0/0", $"{rabbit.Enmity.Count}/{h.Notoriety.Count}");
    }
}
