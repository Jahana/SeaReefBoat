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

// XI.Verify.PlayerMagic.Live -- live gate for a char casting (ZoneHost.PlayerMagic: the 0x01A CastMagic action and
// CPlayerController::Cast; ZoneHost.Magic: CMagicState with a char caster, CCharEntity::OnCastFinished /
// OnCastInterrupted with its Immanence and Chain Affinity skillchains, spell::CanUseSpell for a PC) with a WHM 30 /
// BLM 15 (and a BLU 30 for foot kick) against a rabbit in West Ronfaure and the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 990954;
    private const ushort WestRonfaure = 100;
    private const uint Rabbit = 17186822;
    private const ushort Cure = 1, CureII = 2, Dia = 23, Protect = 43, Stone = 159, Utsusemi = 338, FootKick = 577, Helldive = 567;

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

    private static ulong U(BasicPacket p, int bit, int len) => Battle2Packets.Unpack(p.Buffer, bit, len);

    // a BATTLE2's actor / category / action id / target count / first target
    private static string Head(BasicPacket p) => $"{U(p, 40, 32)}/{U(p, 82, 4)}/{U(p, 86, 32):X}/{U(p, 72, 6)}/{U(p, 150, 32)}";

    private static uint Recast(BasicPacket p) => (uint)U(p, 118, 32);

    // the first target's first result: (res, anim, param, msg)
    private static (int Res, int Anim, int Param, int Msg) First(BasicPacket p)
    {
        int r = 150 + 36;
        return ((int)U(p, r, 3), (int)U(p, r + 5, 12), (int)U(p, r + 27, 17), (int)U(p, r + 44, 10));
    }

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("pcmagicverify"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Chanty"), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(0.0f), BoundValue.Of(0.0f), BoundValue.Of(0.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)3));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        // WHM 30 / BLM 15
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, sjob, mlvl, slvl) VALUES (?, 300, 200, 3, 4, 30, 15)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, whm, blm) VALUES (?, 30, 15)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));
        foreach (var spell in new[] { Cure, Dia, Protect, Stone, Utsusemi, FootKick, Helldive })
            Sql(db, "INSERT INTO char_spells(charid, spellid) VALUES (?, ?)", BoundValue.Of(CharId), BoundValue.Of(spell));
        // healing / elemental magic at 50
        Sql(db, "INSERT INTO char_skills(charid, skillid, value, rank) VALUES (?, 33, 500, 0), (?, 36, 500, 0)", BoundValue.Of(CharId), BoundValue.Of(CharId));

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        zone.VanaTicksNow = () => 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        zone.MobRng = new Random(5);
        zone.SendIpc = _ => { };
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);

        var info = zone.ZoneInfoOf(WestRonfaure);
        var rabbit = info.MobById[Rabbit];
        Check("rabbit up", true, rabbit.IsSpawned);
        rabbit.BaseMaxHp = rabbit.MaxHp = rabbit.Hp = 5000; // it lives through the spells

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
        void Pos(float dx)
        {
            s.PacketList.Clear(); // a pending 0x05B (the login's position set) would hold the update back
            Send(0x015, 32, b =>
            {
                BitConverter.GetBytes(rabbit.X + dx).CopyTo(b, C2sPos.OffX);
                BitConverter.GetBytes(rabbit.Y).CopyTo(b, C2sPos.OffZ);
                BitConverter.GetBytes(rabbit.Z).CopyTo(b, C2sPos.OffY);
            });
        }
        // a client standing still keeps sending its position: the last move distance goes back to 0
        void Stand(float dx)
        {
            Pos(dx);
            Pos(dx);
        }
        void Cast(ushort spell, ushort targid)
        {
            s.PacketList.Clear();
            Send(0x01A, 28, b =>
            {
                BitConverter.GetBytes(CharId).CopyTo(b, 4);
                BitConverter.GetBytes(targid).CopyTo(b, C2sAction.OffActIndex);
                BitConverter.GetBytes((ushort)ActionId.CastMagic).CopyTo(b, C2sAction.OffActionID);
                C2sAction.SetSpellId(b, spell);
            });
        }
        List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);
        List<BasicPacket> Actions(int category) => Of(0x028).FindAll(p => (int)U(p, 82, 4) == category);
        string Msgs() => string.Join(",", Of(0x029).ConvertAll(p => $"{BitConverter.ToUInt32(p.Buffer, 12)}:{BitConverter.ToUInt16(p.Buffer, 24)}"));
        void Tick(long ms)
        {
            s.PacketList.Clear();
            now += ms;
            zone.Tick();
        }
        BasicPacket TickUntil(int category, Func<BasicPacket, bool> match = null, int maxTicks = 60)
        {
            for (int i = 0; i < maxTicks; i++)
            {
                Tick(100);
                if (Actions(category).Find(p => match == null || match(p)) is { } found) return found;
            }
            return null;
        }
        void TickUntilIdle()
        {
            for (int i = 0; c.MagicState != null && i < 100; i++) Tick(100);
        }

        var lua = scripts.Lua;
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local pc = GetEntityByID({CharId})\nlocal mob = GetMobByID({Rabbit})\n" + code, "=verify_pcmagic");
            return string.Join(",", got);
        }

        Stand(3);
        Tick(1);
        log.Clear(); // the login's own noise (a test char has no race row)
        c.Mp = c.MaxMp;
        Check("a WHM 30 / BLM 15 with MP", true, c.MJob == 3 && c.SJob == 4 && c.MaxMp > 50);

        Console.WriteLine("the refusals: CanUseSpell, the cost, silence, the range");
        Cast(CureII, c.TargId);
        Check("cure ii not learned: cannot cast (47)", $"{CureII}:47/False", $"{Msgs()}/{c.MagicState != null}");
        Cast(Utsusemi, c.TargId);
        Check("utsusemi learned, no NIN level: cannot cast (47)", $"{Utsusemi}:47", Msgs());
        c.Mp = 2;
        Cast(Cure, c.TargId);
        Check("2 MP: not enough MP (34)", $"{Cure}:34", Msgs());
        c.Mp = c.MaxMp;
        Lua("pc:addStatusEffect(xi.effect.SILENCE, { power = 1, duration = 60, origin = pc })");
        Cast(Cure, c.TargId);
        Check("silenced: unable to cast spells (49)", $"{Cure}:49", Msgs());
        Lua("pc:delStatusEffect(xi.effect.SILENCE)");
        Pos(23);
        Cast(Stone, rabbit.TargId);
        Check("23 away, stone's range 20: out of range (313)", $"{Stone}:313/False", $"{Msgs()}/{c.MagicState != null}");
        Stand(3);

        Console.WriteLine("cure on itself: the start, the cast time, the finish, the recast, canAct");
        c.Hp = 50;
        int mp0 = c.Mp;
        Cast(Cure, c.TargId);
        var start = Actions(8).Count > 0 ? Actions(8)[0] : null;
        Check("starts casting: cat 8, white magic, msg 327 on itself", $"{CharId}/8/{(uint)FourCC.WhiteMagicCast:X}/1/{CharId}/{Cure}/327",
              start == null ? "none" : $"{Head(start)}/{First(start).Param}/{First(start).Msg}");
        Check("in the state: canChangeState false, getCurrentAction 30", "false,30",
              Lua("report(tostring(pc:canChangeState())) report(pc:getCurrentAction())"));
        Tick(1900);
        Check("not done at 1.9 s (cure casts 2 s)", 0, Actions(4).Count);
        var finish = TickUntil(4);
        Check("finishes: cat 4, the spell id, on itself", $"{CharId}/4/{Cure:X}/1/{CharId}", finish == null ? "none" : Head(finish));
        Check("recovers HP (msg 7), the amount healed", $"7/{c.Hp - 50}", finish == null ? "none" : $"{First(finish).Msg}/{First(finish).Param}");
        Check("healed some", true, c.Hp > 50);
        Check("8 MP spent", mp0 - 8, c.Mp);
        Check("the 5 s recast in the finish", 5u, finish == null ? 0u : Recast(finish));
        Cast(Cure, c.TargId);
        Check("again at once: unable to cast (18; canAct and the recast)", "0:18", Msgs());
        Tick(2600);
        Cast(Cure, c.TargId);
        Check("2.6 s on, the recast still up: 18", "0:18", Msgs());
        TickUntilIdle();
        Check("the state ends after the animation", false, c.MagicState != null);

        Tick(6000);
        Cast(Protect, c.TargId);
        var protect = TickUntil(4);
        Check("protect on itself: gains the effect (230), protect (40) on", "230/40/True",
              protect == null ? "none" : $"{First(protect).Msg}/{First(protect).Param}/{c.StatusEffects.HasStatusEffect(40)}");
        TickUntilIdle();

        Console.WriteLine("interrupted: moving, a hit, death");
        Tick(6000);
        mp0 = c.Mp;
        Cast(Cure, c.TargId);
        Check("casting", true, c.MagicState != null);
        Pos(4);
        BasicPacket moved = null;
        string movedMsgs = "";
        for (int i = 0; i < 40 && moved == null; i++)
        {
            Tick(100);
            moved = Actions(8).Find(p => U(p, 86, 32) == (uint)FourCC.WhiteMagicInterrupt);
            if (moved != null) movedMsgs = Msgs();
        }
        Check("moved 1: the interrupt packet and 'is interrupted' (16)", "True/0:16", $"{moved != null}/{movedMsgs}");
        Check("no MP spent", mp0, c.Mp);
        TickUntilIdle();
        Stand(4);
        Tick(6000);

        Lua("pc:setMod(xi.mod.SPELLINTERRUPT, -10000)");
        Cast(Cure, c.TargId);
        Lua("pc:tryHitInterrupt(mob)");
        BasicPacket hit = null;
        for (int i = 0; i < 40 && hit == null; i++)
        {
            Tick(100);
            hit = Actions(8).Find(p => U(p, 86, 32) == (uint)FourCC.WhiteMagicInterrupt);
        }
        Check("a hit with SIRD below zero: interrupted at the end", true, hit != null);
        Lua("pc:setMod(xi.mod.SPELLINTERRUPT, 0)");
        TickUntilIdle();
        Tick(6000);

        Console.WriteLine("castSpell on a char, then stone on the rabbit: the damage, the claim, the enmity");
        Lua($"pc:castSpell({Cure}, pc)");
        Tick(1);
        Check("castSpell queues a char's cast", true, c.MagicState != null);
        TickUntilIdle();
        Tick(6000);
        int rabbitHp = rabbit.Hp;
        Cast(Stone, rabbit.TargId);
        var stoneStart = Actions(8).Count > 0 ? Actions(8)[0] : null;
        Check("stone starts on the rabbit: black magic", $"{CharId}/8/{(uint)FourCC.BlackMagicCast:X}/1/{Rabbit}",
              stoneStart == null ? "none" : Head(stoneStart));
        var stoneDone = TickUntil(4);
        Check("lands: damage (msg 2) equal to the HP lost", $"2/{rabbitHp - rabbit.Hp}",
              stoneDone == null ? "none" : $"{First(stoneDone).Msg}/{First(stoneDone).Param}");
        Check("claimed and on its list", $"{CharId}/True", $"{rabbit.OwnerId}/{rabbit.Enmity.HasId(CharId)}");
        Tick(500);
        Check("its next AI tick: fighting the char", $"True/{CharId}", $"{rabbit.Engaged}/{rabbit.BattleTarget?.Id}");
        TickUntilIdle();

        Console.WriteLine("ninjutsu: the tool");
        Lua("pc:changeJob(xi.job.NIN) pc:setLevel(30)");
        c.Mp = c.MaxMp;
        Tick(3000);
        Cast(Utsusemi, c.TargId);
        Check("a NIN 30 without shihei: lacks the ninja tools (35)", $"{Utsusemi}:35/False", $"{Msgs()}/{c.MagicState != null}");
        Lua("pc:addItem(1179, 3)");
        Lua("pc:setMod(xi.mod.SPELLINTERRUPT, 102)"); // the rabbit swinging at it: 102% SIRD and it cannot be interrupted
        Cast(Utsusemi, c.TargId);
        Check("with shihei: casting", true, c.MagicState != null);
        var ichi = TickUntil(4);
        Check("utsusemi: ichi lands, one shihei used", $"True/2",
              $"{ichi != null}/{c.Items.FindAll(i => i.ItemId == 1179 && i.Location == Container.Inventory).ConvertAll(i => (int)i.Quantity).Sum()}");
        TickUntilIdle();
        Lua("pc:setMod(xi.mod.SPELLINTERRUPT, 0) pc:changeJob(xi.job.WHM) pc:setLevel(30)");

        Console.WriteLine("the scholar's arts: Light Arts lifts healing magic to the level's baseline");
        // healing magic is 50 (500 in tenths; the cures above may have skilled it up a point), under level 30's E cap:
        // raised to the baseline 5 + 2.7 * 29 = 83 (elemental too, from its cap of 48)
        int healing = c.WorkingSkills[33] & 0x7FFF;
        Check("healing / elemental before (elemental at the WHM 30 cap)", "True/48", $"{healing is 50 or 51}/{c.WorkingSkills[36] & 0x7FFF}");
        Check("the real skill under the E cap (RDM's dark magic)", true, 50 < stats.Growth.GetMaxSkill(37, 5, 30));
        Lua("pc:addStatusEffect(xi.effect.LIGHT_ARTS, { power = 1, duration = 600, origin = pc })");
        Check("light arts: healing to 83, elemental untouched", "83/48", $"{c.WorkingSkills[33] & 0x7FFF}/{c.WorkingSkills[36] & 0x7FFF}");
        Lua("pc:delStatusEffect(xi.effect.LIGHT_ARTS)");
        Check("arts gone: healing back", healing, c.WorkingSkills[33] & 0x7FFF);
        Lua("pc:addStatusEffect(xi.effect.DARK_ARTS, { power = 1, duration = 600, origin = pc })");
        Check("dark arts: elemental to 83, healing untouched", $"{healing}/83", $"{c.WorkingSkills[33] & 0x7FFF}/{c.WorkingSkills[36] & 0x7FFF}");
        Lua("pc:delStatusEffect(xi.effect.DARK_ARTS)");
        Check("arts gone: elemental back to its cap", 48, c.WorkingSkills[36] & 0x7FFF);

        Console.WriteLine("Immanence and Chain Affinity: a spell closing the rabbit's resonance");
        Lua("pc:setMod(xi.mod.SPELLINTERRUPT, 102)");
        rabbit.Unkillable = true;
        // a Detonation resonance 3 s old: stone's Scission closes Scission
        rabbit.StatusEffects.AddStatusEffect(new StatusEffect(799, 0, 6, 0, 10000));
        Lua("pc:addStatusEffect(xi.effect.IMMANENCE, { power = 1, duration = 60, origin = pc })");
        c.Mp = c.MaxMp;
        Tick(3100);
        Cast(Stone, rabbit.TargId);
        TickUntil(4);
        var resonance = rabbit.StatusEffects.GetStatusEffect(799, 0);
        Check("immanence stone: used up, the resonance now Scission, linked once", "False/4/1",
              $"{c.StatusEffects.HasStatusEffect(470)}/{resonance?.Power}/{resonance?.SubPower}");
        TickUntilIdle();
        rabbit.StatusEffects.DelStatusEffectSilent(799);
        Tick(6000);

        Lua("pc:changeJob(xi.job.BLU) pc:setLevel(30)");
        c.SetBlueSpells[0] = (byte)(FootKick - 0x200);
        // foot kick is physical and can miss: a few tries for a hit
        string blueResult = "no hit";
        for (int attempt = 0; attempt < 6 && blueResult == "no hit"; attempt++)
        {
            Tick(10000);
            c.Mp = c.MaxMp;
            c.Tp = 1500;
            rabbit.StatusEffects.DelStatusEffectSilent(799);
            // a Scission resonance: foot kick's Detonation closes Detonation
            rabbit.StatusEffects.AddStatusEffect(new StatusEffect(799, 0, 4, 0, 10000));
            Lua("pc:addStatusEffect(xi.effect.CHAIN_AFFINITY, { power = 1, duration = 30, origin = pc })");
            Tick(3100);
            Cast(FootKick, rabbit.TargId);
            var kick = TickUntil(4);
            if (kick == null) { blueResult = "no cast: " + Msgs(); break; }
            if (First(kick).Param <= 0) continue;
            var sc = rabbit.StatusEffects.GetStatusEffect(799, 0);
            blueResult = $"{c.StatusEffects.HasStatusEffect(164)}/{c.Tp}/{sc?.Power}/{sc?.SubPower}";
        }
        Check("chain affinity foot kick: worn off, the TP taken, the resonance Detonation, linked once", "False/0/6/1", blueResult);
        Check("getKnockback: helldive's 1, foot kick's none, stone's none", "1/0/0",
              Lua($"report(GetSpell({Helldive}):getKnockback() .. '/' .. GetSpell({FootKick}):getKnockback() .. '/' .. GetSpell({Stone}):getKnockback())"));
        c.SetBlueSpells[1] = (byte)(Helldive - 0x200);
        string knock = "no hit";
        for (int attempt = 0; attempt < 6 && knock == "no hit"; attempt++)
        {
            TickUntilIdle();
            Tick(30000);
            c.Mp = c.MaxMp;
            Cast(Helldive, rabbit.TargId);
            var dive = TickUntil(4);
            if (dive == null) { knock = "no cast: " + Msgs(); break; }
            if (First(dive).Param > 0) knock = $"{U(dive, 150 + 36 + 24, 3)}";
        }
        Check("helldive lands: knockback 1 in the result", "1", knock);
        TickUntilIdle();
        rabbit.StatusEffects.DelStatusEffectSilent(799);
        rabbit.Unkillable = false;
        rabbit.Hp = rabbit.MaxHp;
        Lua("pc:setMod(xi.mod.SPELLINTERRUPT, 0) pc:changeJob(xi.job.WHM) pc:setLevel(30)");
        c.Mp = c.MaxMp;

        Console.WriteLine("takeSpellDamage: the bind breaks (BindBreakCheck), Scarlet Delirium takes the damage up");
        int bindsBroken = 0;
        for (int i = 0; i < 10; i++)
        {
            rabbit.Hp = rabbit.MaxHp;
            Lua("mob:addStatusEffect(xi.effect.BIND, { power = 1, duration = 60, origin = pc })");
            bool bound = rabbit.StatusEffects.HasStatusEffect(11);
            Lua($"mob:takeSpellDamage(pc, GetSpell({Stone}), 1, xi.attackType.MAGICAL, xi.damageType.EARTH)");
            if (bound && !rabbit.StatusEffects.HasStatusEffect(11)) bindsBroken++;
        }
        Check("a damaging hostile spell breaks bind (95%): most of 10 tries", true, bindsBroken >= 7);
        Lua("mob:delStatusEffect(xi.effect.BIND)");
        rabbit.Hp = rabbit.MaxHp;
        c.Hp = c.MaxHp;
        Lua("pc:addStatusEffect(xi.effect.SCARLET_DELIRIUM, { power = 0, duration = 60, origin = pc })");
        int delirium = c.MaxHp / 5;
        Lua($"pc:takeSpellDamage(mob, GetSpell({Stone}), {delirium}, xi.attackType.MAGICAL, xi.damageType.EARTH)");
        Check("Scarlet Delirium: the first damage turns it into SCARLET_DELIRIUM_1, power from the HP share", $"False/True/{(ushort)MathF.Floor(Math.Clamp((float)delirium / c.MaxHp / 2.0f, 0.0f, 0.5f) * 1000)}",
              $"{c.StatusEffects.HasStatusEffect(479)}/{c.StatusEffects.HasStatusEffect(480)}/{c.StatusEffects.GetStatusEffect(480)?.Power}");
        Lua("pc:delStatusEffect(xi.effect.SCARLET_DELIRIUM_1)");
        c.Hp = c.MaxHp;

        Console.WriteLine("a spell's kill: the defeat message waits the spell's animation time (m_DropItemTime)");
        Check("a spawned mob's m_DropItemTime: 1 s", 1000, rabbit.DropItemTimeMs);
        Tick(6000);
        c.Mp = c.MaxMp;
        rabbit.Hp = 1;
        var (rabbitX, rabbitY, rabbitZ) = (rabbit.X, rabbit.Y, rabbit.Z);
        Cast(Stone, rabbit.TargId);
        TickUntil(4);
        for (int t = 0; t < 5 && !rabbit.IsDead; t++) Tick(100);
        Check("stone kills: the death, the defeat message stone's 2 s on", "True/2000/2000",
              $"{rabbit.IsDead}/{rabbit.DropItemTimeMs}/{rabbit.DefeatMsgAtMs - rabbit.DeathAtMs}");
        for (int t = 0; t < 10 && rabbit.IsSpawned; t++) Tick(4000); // the body despawns, then fades out
        Lua($"SpawnMob({Rabbit})");
        Check("respawned: m_DropItemTime back to 1 s", "True/1000", $"{rabbit.IsAlive && !rabbit.IsDead}/{rabbit.DropItemTimeMs}");
        rabbit.Hp = rabbit.MaxHp;
        rabbit.PathFind?.Clear();
        (rabbit.X, rabbit.Y, rabbit.Z) = (rabbitX, rabbitY, rabbitZ); // back by the char

        Tick(3000);
        Cast(Dia, rabbit.TargId);
        Check("dia casting", true, c.MagicState != null);
        Lua("pc:setHP(0)");
        Tick(100);
        Check("dead mid cast: the state gone", false, c.MagicState != null);

        Check("no script errors along the way", "", string.Join("\n", log.FindAll(l => l.Contains("lua:") || l.Contains("luautils::") || l.Contains("rror") || l.Contains("Invalid entity"))));
    }
}
