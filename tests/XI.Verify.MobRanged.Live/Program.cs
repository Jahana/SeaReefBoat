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

// XI.Verify.MobRanged.Live -- live gate for a mob's ranged attack (ZoneHost.MobRanged: SetupRangedAttack, the
// controller's shot, CRangeState for a mob, CBattleEntity::OnRangedAttack with a mob shooting, the free phases, the
// held swing, Barrage, paralysis, a stun dropping the aim, setRangedAttackEnabled / isRangedAttackEnabled, with the
// chase's HP_STANDBACK holding it back once ranged attacks are off) against a
// Fomor Ranger in Phomiuna Aqueducts and the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 990997;
    private const ushort Phomiuna = 27;
    private const uint Ranger = 16887904;
    private const uint RangedStart = 0x676C6163, RangedInterrupt = 0x676C7073, RangedFinish = 0x676C6873;
    private const ushort Barrage = 73;

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
    private static string Head(BasicPacket p) => p == null ? "none" : $"{U(p, 40, 32)}/{U(p, 82, 4)}/{U(p, 86, 32):X}/{U(p, 72, 6)}/{U(p, 150, 32)}";

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
            BoundValue.Of(CharId), BoundValue.Of("rangedverify"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Shooty"), BoundValue.Of(Phomiuna), BoundValue.Of((ushort)0),
            BoundValue.Of(-166.0f), BoundValue.Of(0.0f), BoundValue.Of(170.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)3));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 3000, 50, 1, 75)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 75)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
        // the char stands dx yalms along x from the ranger, through the aqueduct's walls: no collision mesh
        // (XI.Verify.XiMesh.Live checks the line of sight)
        zone.XiMeshDir = null;
        long now = 1_000_000;
        zone.NowMs = () => now;
        zone.VanaTicksNow = () => 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        zone.MobRng = new Random(7);
        zone.SendIpc = _ => { };
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);

        var info = zone.ZoneInfoOf(Phomiuna);
        var ranger = info.MobById[Ranger];
        Check("the fomor ranger up", true, ranger.IsSpawned);

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
        // the char dx yalms along x from the ranger
        void Pos(float dx)
        {
            s.PacketList.Clear(); // a pending 0x05B (the login's position set) would hold the update back
            Send(0x015, 32, b =>
            {
                BitConverter.GetBytes(ranger.X + dx).CopyTo(b, C2sPos.OffX);
                BitConverter.GetBytes(ranger.Y).CopyTo(b, C2sPos.OffZ);
                BitConverter.GetBytes(ranger.Z).CopyTo(b, C2sPos.OffY);
            });
            s.PacketList.Clear();
        }
        float Dist()
        {
            float dx = ranger.X - c.X, dy = ranger.Y - c.Y, dz = ranger.Z - c.Z;
            return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        }
        List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);
        List<BasicPacket> Actions(int category) => Of(0x028).FindAll(p => (int)U(p, 82, 4) == category && U(p, 40, 32) == Ranger);
        BasicPacket Action(int category, uint fourCc) => Actions(category).Find(p => U(p, 86, 32) == fourCc);
        int swings = 0, swingsWhileAiming = 0;
        void Tick(long ms)
        {
            s.PacketList.Clear();
            now += ms;
            zone.Tick();
            int n = Actions(1).Count;
            swings += n;
            if (ranger.InRangedAttack) swingsWhileAiming += n;
        }
        // ticks of 100 ms until the ranger's BATTLE2 of that category (and FourCC) shows: its time, or -1
        BasicPacket found = null;
        long TickUntil(int category, uint fourCc = 0, int maxTicks = 120)
        {
            found = null;
            for (int i = 0; i < maxTicks; i++)
            {
                Tick(100);
                found = Actions(category).Find(p => fourCc == 0 || U(p, 86, 32) == fourCc);
                if (found != null) return now;
            }
            return -1;
        }
        void TickUntilIdle()
        {
            for (int i = 0; ranger.InRangedAttack && i < 100; i++) Tick(100);
        }

        var lua = scripts.Lua;
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local mob = GetMobByID({Ranger})\nlocal pc = GetEntityByID({CharId})\n" + code, "=verify_mobranged");
            return string.Join(",", got);
        }
        uint Var(string k) => ranger.LocalVars.TryGetValue(k, out var v) ? v : 0;
        void FullHp() => c.Hp = c.MaxHp;

        log.Clear(); // the login's own noise (a test char has no race row)
        Console.WriteLine("SetupRangedAttack: a fomor ranger");
        Check("ranged attacks on (isRangedAttackEnabled), range 14, base delay 300, no special skill", "true/14/300/0",
              $"{Lua("report(tostring(mob:isRangedAttackEnabled()))")}/{ranger.GetMobMod(MobMod.RangedAttackRange)}/{ranger.RangedBaseDelay}/{ranger.GetMobMod(MobMod.SpecialSkill)}");
        // keep the party's links, the TP moves and the roaming out of it
        ranger.SetMobMod(MobMod.OneWayLinking, 1);
        ranger.MobAbilityEnabled = false;
        Lua(@"mob:addListener('RANGE_START', 'VERIFY_RS', function(m, action) m:setLocalVar('rangeStart', m:getLocalVar('rangeStart') + 1) end)
              mob:addListener('RANGE_STATE_EXIT', 'VERIFY_RX', function(m, t, action) m:setLocalVar('rangeExit', m:getLocalVar('rangeExit') + 1) m:setLocalVar('exitTarget', t and t:getID() or 0) end)");

        Console.WriteLine("engaged from 20': closing to 14' and aiming");
        FullHp();
        Pos(-20);
        Lua("mob:updateEnmity(pc)");
        long start1 = TickUntil(12, RangedStart, 60);
        var start1Pkt = found;
        Check("closes to the ranged range and starts aiming: actor / 12 / RangedStart / 1 target / itself", $"{Ranger}/12/{RangedStart:X}/1/{Ranger}", Head(start1Pkt));
        Check("stopped 0.4 inside the ranged range, out of melee range", "13.6", $"{Dist():0.0}");
        Check("RANGE_START; the state up for the 2.5 s aim (300 delay); canChangeState false; TapDeaggroTime", $"1/True/2500/false/{start1}",
              $"{Var("rangeStart")}/{ranger.InRangedAttack}/{ranger.RangedAimMs}/{Lua("report(tostring(mob:canChangeState()))")}/{ranger.DeaggroTimeMs}");
        int hpBefore = c.Hp;
        long finish1 = TickUntil(2, RangedFinish, 40);
        var shot = found;
        Check("the shot after the aim (the next AI tick past 2.5 s): actor / 2 / RangedFinish / 1 target / the char", $"2800/{Ranger}/2/{RangedFinish:X}/1/{CharId}",
              $"{finish1 - start1}/{Head(shot)}");
        var r1 = shot == null ? default : First(shot);
        Check("a hit (352 / 353) takes its damage from the char, a miss (354) nothing", true,
              shot != null && ((r1.Msg is 352 or 353 && r1.Param == hpBefore - c.Hp && r1.Param > 0) || (r1.Msg == 354 && c.Hp == hpBefore)));
        Check("RANGE_STATE_EXIT with the char", $"1/{CharId}", $"{Var("rangeExit")}/{Var("exitTarget")}");
        Check("the mob's enmity toward the char kept, still engaged", "True/True", $"{ranger.Engaged}/{ranger.BattleTarget == c}");
        Check("aim done, the weapon not yet away", true, ranger.RangedAimDone);
        for (int i = 0; ranger.InRangedAttack && i < 60; i++) Tick(100);
        Check("the state ends 2850 ms after the aim (next tick: 5600 ms); the next shot 1.1 s after the aim's end", $"{start1 + 5600}/{start1 + 3600}",
              $"{now}/{ranger.LastRangedAttackMs}");
        long start2 = TickUntil(12, RangedStart, 20);
        Check("the free phase long over: the next aim on the next AI tick", 6000L, start2 - start1);
        Check("held at 13.6' and no swings: out of melee range", "13.6/0", $"{Dist():0.0}/{swings}");
        TickUntilIdle();

        Console.WriteLine("a shot from melee range: the 6.5-8 s free phase, the swing held");
        Pos(-1.5f);
        swings = swingsWhileAiming = 0;
        long start3 = TickUntil(12, RangedStart, 40);
        var entry3 = ranger.RangedEntryMs;
        TickUntilIdle();
        long phase = ranger.LastRangedAttackMs - (entry3 + 2500);
        Check("in melee range when it started: the next shot 6.5 to 8 s after the aim", true, start3 > 0 && phase >= 6500 && phase < 8000);
        for (int i = 0; i < 60; i++) Tick(100);
        Check("in between, the attack state swings, never while aiming", "True/0", $"{swings > 0}/{swingsWhileAiming}");
        TickUntilIdle();

        Console.WriteLine("setRangedAttackEnabled(false): closing to melee instead");
        Lua("mob:setRangedAttackEnabled(false)");
        Check("isRangedAttackEnabled", "false", Lua("report(tostring(mob:isRangedAttackEnabled()))"));
        FullHp();
        Pos(-10);
        swings = 0;
        int aims = 0;
        for (int i = 0; i < 30; i++)
        {
            Tick(100);
            if (Action(12, RangedStart) != null) aims++;
        }
        // ShouldCloseToTarget: a fomor RNG has HP_STANDBACK 70, and at full HP with nothing to cast it stands back
        Check("HP_STANDBACK: healthy with no spells, it holds back at 10 yalms", "0/True/False",
              $"{aims}/{Dist() > 9.5f}/{swings > 0}");
        ranger.SetMobMod(MobMod.NoStandback, 1);
        for (int i = 0; i < 80; i++)
        {
            Tick(100);
            if (Action(12, RangedStart) != null) aims++;
        }
        Check("NO_STANDBACK: no aims; closed to melee range and swinging", "0/True/True",
              $"{aims}/{Dist() <= ranger.ModelHitboxSize + 2.0f + c.ModelHitboxSize10 / 10.0f}/{swings > 0}");
        ranger.SetMobMod(MobMod.NoStandback, 0);
        Lua("mob:setRangedAttackEnabled(true)");

        Console.WriteLine("paralyzed: the interrupt and 'is paralyzed', no shot");
        FullHp();
        Pos(-10);
        for (int i = 0; !ranger.InRangedAttack && i < 120; i++) Tick(100);
        ranger.SetModifier(Mod.PARALYZE, 100);
        int finishes = 0;
        BasicPacket para = null, interrupt = null;
        for (int i = 0; i < 40 && para == null; i++)
        {
            Tick(100);
            finishes += Actions(2).Count;
            para ??= Actions(4).Find(_ => true);
            interrupt ??= Action(12, RangedInterrupt);
        }
        Check("a magic finish on itself with 'is paralyzed' (84)", $"{Ranger}/4/0/1/{Ranger}/84", para == null ? "none" : $"{Head(para)}/{First(para).Msg}");
        Check("the ranged interrupt on itself; no shot", $"{Ranger}/12/{RangedInterrupt:X}/1/{Ranger}/0", $"{Head(interrupt)}/{finishes}");
        ranger.SetModifier(Mod.PARALYZE, 0);
        TickUntilIdle();

        Console.WriteLine("Barrage: the extra shots in one result, the effect spent");
        FullHp();
        for (int i = 0; !ranger.InRangedAttack && i < 120; i++) Tick(100);
        TickUntilIdle();
        Lua($"mob:addStatusEffect({Barrage}, {{ origin = mob, power = 0, duration = 60 }})");
        Check("barrage up", true, ranger.StatusEffects.HasStatusEffectWithSubId(Barrage, 0u));
        hpBefore = c.Hp;
        TickUntil(2, RangedFinish, 120);
        var barrage = found == null ? default : First(found);
        Check("'uses Barrage' (157) with the damage the char took, or a full miss (354)", true,
              found != null && ((barrage.Msg == 157 && barrage.Param == hpBefore - c.Hp) || (barrage.Msg == 354 && c.Hp == hpBefore)));
        Check("barrage gone", false, ranger.StatusEffects.HasStatusEffectWithSubId(Barrage, 0u));
        TickUntilIdle();

        Console.WriteLine("a stun mid-aim drops the shot");
        FullHp();
        for (int i = 0; !ranger.InRangedAttack && i < 120; i++) Tick(100);
        Lua("mob:stun(3000)");
        Check("the state gone at once; inactive", "False/True", $"{ranger.InRangedAttack}/{ranger.InactiveState != null}");
        finishes = 0;
        for (int i = 0; i < 25; i++)
        {
            Tick(100);
            finishes += Actions(2).Count + (Action(12, RangedStart) != null ? 1 : 0);
        }
        Check("no shot and no aim while stunned", 0, finishes);
        long after = TickUntil(12, RangedStart, 40);
        Check("aiming again once the stun ends", true, after > 0);
        TickUntilIdle();

        Console.WriteLine("addBardSong (the party's bard sings): two songs a singer, the one ending first replaced");
        const ushort Paeon = 195, Ballad = 196, Minuet = 198;
        string Songs() => string.Join(",", new[] { Paeon, Ballad, Minuet }.Where(id => ranger.StatusEffects.HasStatusEffect(id)));
        Check("paeon (120 s), ballad (60 s): both", "true,true/195,196",
              Lua($"report(tostring(mob:addBardSong(mob, {Paeon}, 1, 0, 120, mob:getID(), 0, 1))) report(tostring(mob:addBardSong(mob, {Ballad}, 1, 0, 60, mob:getID(), 0, 1)))") + "/" + Songs());
        Tick(100); // a deleted song leaves the container on the next tick
        byte balladSlot = ranger.StatusEffects.GetStatusEffect(Ballad)?.Slot ?? 0;
        Check("a third: the ballad (ending first) gives up its slot", $"true/195,198/{balladSlot}",
              Lua($"report(tostring(mob:addBardSong(mob, {Minuet}, 1, 0, 90, mob:getID(), 0, 1)))") + $"/{Songs()}/{ranger.StatusEffects.GetStatusEffect(Minuet)?.Slot}");
        Tick(100);
        // the paeon overwritten in place still counts as one of the two, so (as in C++) the minuet ending first goes too
        Check("the paeon again (same tier): overwritten, and the minuet with it", "true/195/5",
              Lua($"report(tostring(mob:addBardSong(mob, {Paeon}, 5, 0, 120, mob:getID(), 0, 1)))") + $"/{Songs()}/{ranger.StatusEffects.GetStatusEffect(Paeon)?.Power}");
        Tick(100);
        Check("a char without an instrument: one song of its own, the second replacing the first", "195,198",
              Lua($"mob:addBardSong(pc, {Ballad}, 1, 0, 60, pc:getID(), 0, 1) mob:addBardSong(pc, {Minuet}, 1, 0, 60, pc:getID(), 0, 1)") + Songs());
        foreach (var id in new[] { Paeon, Ballad, Minuet }) ranger.StatusEffects.DelStatusEffectSilent(id);

        // the party's beastmaster calls its pet: pets are not ported
        Check("no script errors along the way", "", string.Join("\n", log.FindAll(l => (l.Contains("lua:") || l.Contains("luautils::") || l.Contains("rror") ||
              l.Contains("Invalid entity") || l.Contains("invalid entity")) && !l.Contains("CBaseEntity:spawnPet"))));
        log.Clear();
        Lua("pc:setRangedAttackEnabled(true)");
        Check("setRangedAttackEnabled on a char: refused with the error", true, log.Exists(l => l.Contains("function call on invalid entity")));
    }
}
