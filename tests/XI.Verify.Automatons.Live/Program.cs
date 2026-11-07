using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Common;
using XI.Map;
using XI.Ipc;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Automatons.Live -- live gate for a PUP's automaton (ZoneHost.Automatons.cs, AutomatonEntity.cs): Activate
// (xi.job_utils.puppetmaster: the check, isExceedingElementalCapacity, spawnPet(AUTOMATON)) making an AutomatonEntity
// with the master's frame, head and attachments, its name, look and the PUP level, LoadAutomatonStats (the frame stats,
// the skills capped, the melee weapon from the skill, the hard-to-interrupt 85%, the frame's evasion rank),
// onPetStatCalculate (CAN_PARRY, the special spawn animation), onMobSpawn's 35 burden in every element, the burden's
// decay on the master's regen tick, getOverloadChance, reduceBurden; the attachment scripts (the strobe's and tension
// spring's onEquip mods, onManeuverGain / onManeuverLose with a Fire Maneuver on the master); hasAttachmentSet,
// getAttachments, isAutomaton, getAutomatonFrame / Head on both; the frame's pet mods; the automaton sitting with its
// master; the PUP job packet with the automaton's HP; the equipment locked while it is out; the controller
// (ZoneHost.AutomatonAi.cs: the Harlequin head's magic cooldowns, Dia II first without a Light maneuver, Cure III on its
// hurt master with one, a frame move at 1000 TP, the master's base enmity); its death taking the master's maneuvers;
// a Valoredge frame's Shield Bash on a rabbit readying Lowing. One PUP 75 in West Ronfaure, with the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 993740;
    private const ushort WestRonfaure = 100;
    private const ushort AnimatorPlus1 = 17857, PantinTobe = 11298;
    private const uint Rabbit = 17186822;

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

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("auto" + CharId), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Puppeteer"), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(-200.0f), BoundValue.Of(0.0f), BoundValue.Of(-200.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 1500, 100, 18, 75)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, pup) VALUES (?, 75)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));
        // the automaton skills: melee 300 (past the Harlequin's cap at 75), ranged 150, magic 100
        foreach (var (skill, value) in new[] { (22, 3000), (23, 1500), (24, 1000) })
            Sql(db, "INSERT INTO char_skills(charid, skillid, value, rank) VALUES (?, ?, ?, 0)", BoundValue.Of(CharId), BoundValue.Of(skill), BoundValue.Of(value));
        // "Azure" (pet_name 1): Harlequin head and frame with a strobe and a tension spring (both fire)
        var unlockBlob = new byte[36];
        unlockBlob[1] = 1 << 1; // frames: Valoredge (0x2021 & 0x0F)
        foreach (int att in new[] { 1, 2 }) unlockBlob[4 + att / 8] |= (byte)(1 << att % 8);
        var equipBlob = new byte[14];
        equipBlob[0] = 0x20; equipBlob[1] = 1; equipBlob[2] = 1; equipBlob[3] = 2;
        Sql(db, "REPLACE INTO char_pet(charid, automatonid, unlocked_attachments, equipped_attachments) VALUES (?, 1, ?, ?)",
            BoundValue.Of(CharId), BoundValue.Of(unlockBlob), BoundValue.Of(equipBlob));

        // Animator +1 (HP +50 / +60 / +45 / +40 by frame) and Pantin Tobe (the automaton's ACC +10)
        foreach (var (slot, item) in new[] { (1, AnimatorPlus1), (2, PantinTobe) })
            Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity) VALUES (?, 0, ?, ?, 1)",
                BoundValue.Of(CharId), BoundValue.Of(slot), BoundValue.Of(item));

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, AbilityTable.Load(db, _ => true, 1.0), stats), dataDir, true, log.Add);
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
        void Tick(int ticks = 1)
        {
            for (int t = 0; t < ticks; t++)
            {
                now += 400;
                zone.Tick();
            }
        }
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);
        var lua = scripts.Lua;
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local player = GetPlayerByID({CharId})\nlocal pet = player and player:getPet()\n" + code, "=verify_automatons");
            return string.Join(",", got);
        }

        var info = zone.ZoneInfoOf(WestRonfaure);
        var s = new MapSession { CharId = CharId, AccountId = CharId };
        zone.OnLogin(s);
        var c = (CharData)s.Char;
        void Send(ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(s, opcode, new BasicPacket(buf));
            zone.AfterParse(s);
        }
        Send(0x00A, 92, buf => BitConverter.GetBytes(CharId).CopyTo(buf, 12));
        Send(0x00C, 12);
        Pump();
        Check("the PUP 75 in West Ronfaure, Harlequin with a strobe and a tension spring", "18/75/100/32/1/1/2",
            $"{c.MJob}/{c.MLvl}/{c.Zone}/{c.Automaton.Frame}/{c.Automaton.Head}/{c.Automaton.Attachments[0]}/{c.Automaton.Attachments[1]}");

        List<BasicPacket> Sent(ushort opcode) => s.PacketList.Where(p => p.GetOpcode() == opcode).ToList();
        List<string> LuaErrors() => log.Where(l => l.Contains("lua") && (l.Contains("rror") || l.Contains("not ported"))).ToList();

        Console.WriteLine("Activate: the automaton out");
        log.Clear();
        Check("onAbilityCheckActivate: nothing out, the capacity not exceeded", "0,0,false",
            Lua("local a, b = xi.job_utils.puppetmaster.onAbilityCheckActivate(player, player, nil) report(a) report(b) " +
                "report(tostring(player:isExceedingElementalCapacity()))"));
        Lua("xi.job_utils.puppetmaster.onAbilityUseActivate(player, player, nil)");
        var pet = c.PPet as AutomatonEntity;
        Check("an AutomatonEntity, the Harlequin frame's pet id, in the zone's pets", "True/69/True/True",
            $"{pet != null}/{pet?.PetId}/{pet != null && info.Pets.Contains(pet)}/{pet?.PMaster == c}");
        if (pet == null) return;
        Check("the master's equipment on it", "32/1/1/2/0", $"{pet.Frame}/{pet.Head}/{pet.Attachments[0]}/{pet.Attachments[1]}/{pet.Attachments[2]}");
        Check("the automaton's name and look (the automaton model, the frame's model id)", $"Azure/6/{c.Automaton.ModelId}",
            $"{pet.Name}/{pet.Look[0]}/{pet.Look[1]}");
        Check("the PUP level and half of it", "75/37", $"{pet.MLvl}/{pet.SLvl}");
        Check("the frame's health and stats, full HP and MP", $"{c.Automaton.MaxHp}/{c.Automaton.MaxMp}/True/True",
            $"{pet.MaxHp}/{pet.MaxMp}/{pet.Hp == pet.MaxHp}/{pet.Mp == pet.MaxMp}");
        Check("the frame's stats", string.Join(",", c.Automaton.Stats), string.Join(",", pet.Stats));
        int cap = pet.WorkingSkills[22] & 0x7FFF;
        Check("the melee skill capped at the frame's cap (the capped bit set), the ranged and magic skills", "True/True/True/True",
            $"{cap < 300}/{(pet.WorkingSkills[22] & 0x8000) != 0}/{pet.WorkingSkills[23] > 0}/{pet.WorkingSkills[24] == c.Automaton.Skills[24]}");
        Check("the magic skill copied into the healing, enhancing and elemental skills, not divine", "True/True/True/0",
            $"{pet.WorkingSkills[33] == pet.WorkingSkills[24]}/{pet.WorkingSkills[34] == pet.WorkingSkills[24]}/{pet.WorkingSkills[36] == pet.WorkingSkills[24]}/{pet.WorkingSkills[32]}");
        Check("the melee weapon from the skill, automaton melee", $"{(int)Math.Floor(pet.WorkingSkills[22] / 8.7f * 2.0f + 3.0f)}/22",
            $"{pet.MainDamage}/{pet.WeaponSkill}");
        Check("hard to interrupt, an evasion rank, DEF", "85/True/True",
            $"{pet.GetMod(Mod.SPELLINTERRUPT)}/{pet.WorkingSkills[Skill.Evasion] > 100}/{pet.GetMod(Mod.DEF) > 50}");
        Check("onPetStatCalculate: CAN_PARRY", 1, pet.GetMobMod(MobMod.CanParry));
        Check("isAutomaton, getAutomatonFrame / Head on the pet and the master", "true,32,1,32,1,false",
            Lua("report(tostring(pet:isAutomaton())) report(pet:getAutomatonFrame()) report(pet:getAutomatonHead()) " +
                "report(player:getAutomatonFrame()) report(player:getAutomatonHead()) report(tostring(player:isAutomaton()))"));
        Check("hasAttachmentSet, getAttachments, hasAttachment", "true,false,strobe/tension_spring/,true,false",
            Lua("report(tostring(pet:hasAttachmentSet(0x2101))) report(tostring(pet:hasAttachmentSet(0x2103))) " +
                "local t = pet:getAttachments() report(t[0] .. '/' .. t[1] .. '/' .. t[2]) " +
                "report(tostring(player:hasAttachment(0x2101))) report(tostring(player:hasAttachment(0x2103)))"));

        Console.WriteLine("The attachments: their mods by the master's fire maneuvers");
        Check("onEquip: the strobe's enmity and the tension spring's attack at no maneuver", "10/3/3",
            $"{pet.GetMod(Mod.ENMITY)}/{pet.GetMod(Mod.ATTP)}/{pet.GetMod(Mod.RATTP)}");
        Lua("player:addStatusEffect(xi.effect.FIRE_MANEUVER, { power = 1, duration = 60, origin = player })");
        Check("a Fire Maneuver: onManeuverGain with one maneuver", "1/25/6",
            $"{c.StatusEffects.GetEffectsCount(EffectId.FireManeuver)}/{pet.GetMod(Mod.ENMITY)}/{pet.GetMod(Mod.ATTP)}");
        Lua("player:addStatusEffect(xi.effect.FIRE_MANEUVER, { power = 1, duration = 60, origin = player })");
        Check("a second: two maneuvers", "2/40/9", $"{c.StatusEffects.GetEffectsCount(EffectId.FireManeuver)}/{pet.GetMod(Mod.ENMITY)}/{pet.GetMod(Mod.ATTP)}");
        Lua("player:delStatusEffect(xi.effect.FIRE_MANEUVER)");
        Check("one lost: onManeuverLose with the one left", "1/25/6",
            $"{c.StatusEffects.GetEffectsCount(EffectId.FireManeuver)}/{pet.GetMod(Mod.ENMITY)}/{pet.GetMod(Mod.ATTP)}");
        Lua("player:addStatusEffect(xi.effect.ICE_MANEUVER, { power = 1, duration = 60, origin = player })");
        Check("an Ice Maneuver leaves the fire attachments alone", "25/6", $"{pet.GetMod(Mod.ENMITY)}/{pet.GetMod(Mod.ATTP)}");
        Lua("player:updateAttachments()");
        Check("updateAttachments: the same mods", "25/6", $"{pet.GetMod(Mod.ENMITY)}/{pet.GetMod(Mod.ATTP)}");

        Console.WriteLine("The burden");
        Check("onMobSpawn: 35 in each element", "35,35,35,35,35,35,35,35", string.Join(",", pet.Burden));
        Check("getOverloadChance: 35 - 30 + 5", "10", Lua("report(player:getOverloadChance(0))"));
        Lua("player:reduceBurden(50)");
        Check("reduceBurden(50): half", "17,17,17,17,17,17,17,17", string.Join(",", pet.Burden));
        Lua("player:addBurden(2, 10)");
        Check("addBurden(wind, 10)", 27, pet.Burden[2]);
        Tick(8); // past the next regen tick
        Check("the master's regen tick: one less each", "16/26", $"{pet.Burden[0]}/{pet.Burden[2]}");

        Console.WriteLine("The frame's pet mods, sitting, the job packet, the locked equipment");
        int hp = pet.MaxHp, mp = pet.MaxMp, acc = pet.GetMod(Mod.ACC);
        Send(0x050, 8, b => { b[4] = 1; b[5] = CharStats.SlotRanged; b[6] = 0; });
        Send(0x050, 8, b => { b[4] = 2; b[5] = 5; b[6] = 0; });
        Check("Animator +1 and Pantin Tobe on", $"{AnimatorPlus1}/{PantinTobe}",
            $"{c.Equipped[CharStats.SlotRanged]?.ItemId ?? 0}/{c.Equipped[5]?.ItemId ?? 0}");
        Check("the Harlequin HP and MP mods reach it, not the other frames', and the Automaton ACC mod", "50/50/10",
            $"{pet.MaxHp - hp}/{pet.MaxMp - mp}/{pet.GetMod(Mod.ACC) - acc}");
        Send(0x0EA, 8, buf => buf[4] = 1);
        Check("the master sits: the automaton sits along", $"{c.Animation}/{c.Animation}", $"{c.Animation}/{pet.Animation}");
        Send(0x0EA, 8, buf => buf[4] = 2);
        s.PacketList.Clear();
        pet.Hp = 100;
        pet.UpdateMask |= EntityUpdatePacket.UpdateHp;
        Tick(2);
        var job = Sent(0x044).LastOrDefault(p => p.Buffer[4] == 18);
        Check("the PUP job packet on its update: its HP and max, the STR bonus", $"100/{pet.MaxHp}/{pet.GetMod(Mod.STR)}",
            job == null ? "none" : $"{BitConverter.ToUInt16(job.Buffer, 104)}/{BitConverter.ToUInt16(job.Buffer, 106)}/{BitConverter.ToUInt16(job.Buffer, 130)}");
        // 0x102 pup_data_t: ItemId @4, JobIndex @8, Slots[14] @12 -- the Valoredge frame in
        Send(0x102, 164, buf => { buf[4] = 0x21; buf[8] = 18; buf[12 + 1] = 0x21; });
        Check("the equipment stays while it is out", 32, c.Automaton.Frame);

        Console.WriteLine("The controller: the Harlequin head's cooldowns, a fight on a rabbit");
        Check("magicCooldowns for the Harlequin head: global 10, healing 12, enfeebling 12; no ranged attack or Shield Bash",
            "10000/12000/12000/0/0/0", $"{pet.MagicCooldownMs}/{pet.HealCooldownMs}/{pet.EnfeebleCooldownMs}/{pet.ElementalCooldownMs}/" +
                                       $"{pet.RangedCooldownMs}/{pet.ShieldBashCooldownMs}");
        Lua("player:removeAllManeuvers()");
        var rabbit = info.MobById[Rabbit];
        Lua($"SpawnMob({Rabbit})");
        Tick();
        rabbit.X = pet.X + 3; rabbit.Y = pet.Y; rabbit.Z = pet.Z;
        rabbit.Unkillable = true;
        pet.Hp = pet.MaxHp;
        pet.Mp = pet.MaxMp;
        c.Hp = c.MaxHp;
        log.Clear();
        Lua($"player:petAttack(GetMobByID({Rabbit}))");
        Check("petAttack: the automaton engaged on the rabbit", "True/True", $"{pet.Engaged}/{pet.BattleTarget == rabbit}");
        // no Light maneuver: an enfeeble first, the first its head casts at magic skill 100 (Bio II needs 111)
        for (int i = 0; i < 20 && pet.MagicState == null; i++) Tick();
        Check("its first decision: Dia II on the rabbit", "24/True", $"{pet.MagicState?.Spell.Id}/{pet.MagicState?.Target == rabbit}");
        for (int i = 0; i < 20 && !rabbit.StatusEffects.HasStatusEffect(134); i++) Tick();
        Check("the rabbit has Dia; the master's enmity on it", "True/True", $"{rabbit.StatusEffects.HasStatusEffect(134)}/{rabbit.Enmity.Get(c.Id) != null}");
        Check("the enfeebling recast", true, ((Func<bool>)(() => Lua("report(tostring(pet:hasRecast(xi.recast.MAGIC, 24)))") == "true"))());
        // the master hurt and a Light Maneuver up: healing before enfeebling (threshold 40%), once the global magic
        // cooldown is up
        Lua("player:addStatusEffect(xi.effect.LIGHT_MANEUVER, { power = 1, duration = 60, origin = player })");
        c.Hp = c.MaxHp / 4;
        int hurt = c.Hp;
        for (int i = 0; i < 60 && pet.MagicState?.Spell.Id is not (>= 1 and <= 6); i++)
        {
            c.Hp = hurt;
            Tick();
        }
        Check("Light: it cures its master, Cure III for more than 190 HP missing", "3/True", $"{pet.MagicState?.Spell.Id}/{pet.MagicState?.Target == c}");
        for (int i = 0; i < 20 && pet.MagicState != null; i++) Tick();
        Check("the master's HP up", true, c.Hp > hurt);
        // a TP move: the frame's list, by matching maneuvers then skill
        pet.Tp = 1000;
        for (int i = 0; i < 40 && pet.SkillState == null; i++) Tick();
        var move = pet.SkillState?.Skill.Id ?? 0;
        Check("at 1000 TP a Harlequin frame move", true, move is 1943 or 2067 or 2301);
        for (int i = 0; i < 20 && pet.SkillState != null; i++) Tick();
        Check("its TP spent", true, pet.Tp < 1000);
        Check("no script errors (the fight)", "", string.Join(" | ", LuaErrors()));
        Lua("pet:disengage()");
        rabbit.Unkillable = false;
        Tick(2);

        Console.WriteLine("Its death takes the master's maneuvers");
        Lua("player:addStatusEffect(xi.effect.FIRE_MANEUVER, { power = 1, duration = 60, origin = player }) " +
            "player:addStatusEffect(xi.effect.ICE_MANEUVER, { power = 1, duration = 60, origin = player })");
        Check("maneuvers up", "1/1", $"{c.StatusEffects.GetEffectsCount(EffectId.FireManeuver)}/{c.StatusEffects.GetEffectsCount((ushort)301)}");
        Lua("pet:setHP(0)");
        Tick(2);
        Check("the automaton gone from the master, its maneuvers with it", "True/0/0",
            $"{c.PPet == null}/{c.StatusEffects.GetEffectsCount(EffectId.FireManeuver)}/{c.StatusEffects.GetEffectsCount((ushort)301)}");
        Tick(20);
        Check("the equipment can change again", 33,
            ((Func<byte>)(() => { Send(0x102, 164, buf => { buf[4] = 0x21; buf[8] = 18; buf[12 + 1] = 0x21; }); return c.Automaton.Frame; }))());

        Console.WriteLine("A Valoredge frame: Shield Bash on a target readying a move");
        Lua("xi.job_utils.puppetmaster.onAbilityUseActivate(player, player, nil)");
        var valor = c.PPet as AutomatonEntity;
        Check("the Valoredge frame out: Shield Bash every 3 minutes, no spells", "33/180000/0",
            $"{valor?.Frame}/{valor?.ShieldBashCooldownMs}/{valor?.MagicCooldownMs}");
        if (valor == null) return;
        // a fresh rabbit
        Lua($"DespawnMob({Rabbit})");
        for (int i = 0; i < 30 && rabbit.IsSpawned; i++) Tick();
        Lua($"SpawnMob({Rabbit})");
        Tick();
        rabbit.X = valor.X + 3; rabbit.Y = valor.Y; rabbit.Z = valor.Z;
        rabbit.Unkillable = true;
        log.Clear();
        Lua($"player:petAttack(GetMobByID({Rabbit}))");
        Tick(8); // past its first decision
        Check("no Shield Bash while the rabbit only swings", true, valor.SkillState == null && valor.LastShieldBashMs == 0);
        // Lowing: a 3 s readying, an interruptible state that outlasts a decision
        Lua($"GetMobByID({Rabbit}):useMobAbility(497)");
        valor.LastActionMs = now; // the next decision 3 s on, inside the readying
        Tick();
        Check("the rabbit readies Lowing", 497, rabbit.SkillState?.Skill.Id ?? 0);
        bool bashed = false;
        for (int i = 0; i < 12 && !bashed; i++)
        {
            Tick();
            bashed = valor.SkillState?.Skill.Id == 1944;
        }
        Check("Shield Bash on the readying rabbit", true, bashed);
        for (int i = 0; i < 20 && valor.SkillState != null; i++) Tick();
        Check("its last Shield Bash time set", true, valor.LastShieldBashMs != 0);
        Check("no script errors", "", string.Join(" | ", LuaErrors()));
    }
}
