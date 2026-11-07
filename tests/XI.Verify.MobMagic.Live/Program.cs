using System;
using System.Collections.Generic;
using System.IO;
using XI.Common;
using XI.Map;
using XI.Ipc;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.MobMagic.Live -- live gate for a mob casting (ZoneHost.MobMagic: CMagicState, CMobController's spell half,
// CMobEntity::OnCastFinished / OnCastInterrupted, the castSpell / tryHitInterrupt / hasSpellList / getSpellListId
// bindings) against a Goblin Weaver (spell list 3, Beastmen_RDM) in West Ronfaure and the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 990953;
    private const ushort WestRonfaure = 100;
    private const uint Weaver = 17187033;
    private const ushort Stone = 159, Protect = 43;

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
            BoundValue.Of(CharId), BoundValue.Of("magicverify"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Spelly"), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(0.0f), BoundValue.Of(0.0f), BoundValue.Of(0.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)3));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 300, 50, 1, 30)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 30)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        zone.VanaTicksNow = () => 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        zone.MobRng = new Random(11);
        zone.SendIpc = _ => { };
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);

        var info = zone.ZoneInfoOf(WestRonfaure);
        var weaver = info.MobById[Weaver];
        // the weaver shares a spawn slot whose roll draws on MobRng (as the spawns' gil purses do): the member the roll
        // put up makes way for it
        if (!weaver.IsSpawned && weaver.Slot?.Entries.Find(e => e.Mob.IsSpawned)?.Mob is { } slotUp)
        {
            scripts.Lua.DoString($"DespawnMob({slotUp.Id})", "=verify_mobmagic");
            for (int i = 0; i < 10 && slotUp.IsSpawned; i++)
            {
                now += 400;
                zone.Tick();
            }
            scripts.Lua.DoString($"SpawnMob({Weaver})", "=verify_mobmagic");
        }
        Check("weaver up", true, weaver.IsSpawned);

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
                BitConverter.GetBytes(weaver.X + dx).CopyTo(b, C2sPos.OffX);
                BitConverter.GetBytes(weaver.Y).CopyTo(b, C2sPos.OffZ);
                BitConverter.GetBytes(weaver.Z).CopyTo(b, C2sPos.OffY);
            });
            s.PacketList.Clear();
        }
        List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);
        List<BasicPacket> Actions(int category) => Of(0x028).FindAll(p => (int)U(p, 82, 4) == category);
        string Msgs() => string.Join(",", Of(0x029).ConvertAll(p => $"{BitConverter.ToUInt32(p.Buffer, 4)}>{BitConverter.ToUInt32(p.Buffer, 8)}:{BitConverter.ToUInt16(p.Buffer, 24)}"));
        void Tick(long ms)
        {
            s.PacketList.Clear();
            now += ms;
            zone.Tick();
        }
        BasicPacket TickUntil(int category, Func<BasicPacket, bool> match = null, int maxTicks = 40)
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
            for (int i = 0; weaver.MagicState != null && i < 100; i++) Tick(100);
        }

        var lua = scripts.Lua;
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local mob = GetMobByID({Weaver})\nlocal pc = GetEntityByID({CharId})\n" + code, "=verify_mobmagic");
            return string.Join(",", got);
        }
        uint Var(string k) => weaver.LocalVars.TryGetValue(k, out var v) ? v : 0;

        log.Clear(); // the login's own noise (a test char has no race row)
        Console.WriteLine("the spell list: hasSpellList, getSpellListId, the container at its level");
        Check("hasSpellList / getSpellListId", "true,3", Lua("report(tostring(mob:hasSpellList())) report(mob:getSpellListId())"));
        Check("stone in the damage list at level 4-5", true, weaver.SpellContainer.DamageList.Contains(Stone));
        Check("the magic cool defaulted to 35", 35, weaver.GetMobMod(MobMod.MagicCool));
        Check("a rabbit has none", "false,0", Lua("local r = GetMobByID(17186822) report(tostring(r:hasSpellList())) report(r:getSpellListId())"));

        Console.WriteLine("castSpell: the start, the cast time, the finish, the recast, the animation time");
        Pos(-3);
        weaver.MaxMp = weaver.Mp = 100;
        weaver.SetMobMod(MobMod.MagicCool, 600); // keep the controller's own casts out of the way
        c.Hp = c.MaxHp;
        Lua(@"mob:addListener('MAGIC_START', 'VERIFY_MS', function(m, t, spell, action) m:setLocalVar('start', spell:getID()) m:setLocalVar('startTarget', t:getID()) end)
              mob:addListener('MAGIC_USE', 'VERIFY_MU', function(m, t, spell, action) m:setLocalVar('use', spell:getID()) end)
              mob:addListener('MAGIC_INTERRUPTED', 'VERIFY_MI', function(m, t, spell, action) m:setLocalVar('interrupted', spell:getID()) end)
              mob:addListener('MAGIC_STATE_EXIT', 'VERIFY_MX', function(m, spell) m:setLocalVar('exit', spell:getID()) end)");
        Lua($"mob:castSpell({Stone}, pc)");
        Check("queued: nothing yet", "0/False", $"{Of(0x028).Count}/{weaver.MagicState != null}");
        Tick(1);
        var starts = Actions(8);
        Check("starts casting: actor / category 8 / BlackMagicCast / 1 target / the char", $"{Weaver}/8/{(uint)FourCC.BlackMagicCast:X}/1/{CharId}",
              starts.Count == 1 ? Head(starts[0]) : $"{starts.Count}");
        Check("its result: the spell id, 'starts casting' (3)", $"{Stone}/3", starts.Count == 1 ? $"{First(starts[0]).Param}/{First(starts[0]).Msg}" : "");
        Check("MAGIC_START with the spell and target; the state up; casting (30)", $"{Stone}/{CharId}/True/30",
              $"{Var("start")}/{Var("startTarget")}/{weaver.MagicState != null}/{Lua("report(mob:getCurrentAction())")}");
        Check("the cast time: stone's 500 ms", 500L, weaver.MagicState?.CastMs ?? -1);
        int hpBefore = c.Hp, mpBefore = weaver.Mp;
        Tick(400);
        Check("not done at 400 ms", 0, Actions(4).Count);
        var finish = TickUntil(4);
        Check("the finish: actor / category 4 / stone / 1 target / the char", $"{Weaver}/4/{Stone:X}/1/{CharId}", finish == null ? "none" : Head(finish));
        var hit = finish == null ? default : First(finish);
        Check("its result: animation 159, the damage (2 / 252) the char lost", $"159/True/{hpBefore - c.Hp}",
              finish == null ? "none" : $"{hit.Anim}/{hit.Msg is 2 or 252}/{hit.Param}");
        Check("the recast shown: 2 s", 2u, finish == null ? 0 : Recast(finish));
        Check("4 MP spent; MAGIC_USE", $"{mpBefore - 4}/{Stone}", $"{weaver.Mp}/{Var("use")}");
        Check("the state lingers for the animation time", true, weaver.MagicState != null);
        Check("the next spell a half to a whole magic cool away", true, weaver.NextMagicMs >= now + 300_000 - 200 && weaver.NextMagicMs <= now + 600_000);
        TickUntilIdle();
        Check("done: STATE_EXIT; roaming (16) again", $"{Stone}/False/16", $"{Var("exit")}/{weaver.MagicState != null}/{Lua("report(mob:getCurrentAction())")}");

        Console.WriteLine("the hit interrupt, silence, paralysis, the target out of range");
        Lua("mob:setMod(xi.mod.SPELLINTERRUPT, -300)"); // the interrupt rate past 100%
        Lua($"mob:castSpell({Stone}, pc)");
        Tick(1);
        Lua("mob:tryHitInterrupt(pc)");
        Check("hit mid-cast: marked interrupted", true, weaver.MagicState?.Interrupted ?? false);
        var interrupt = TickUntil(8, p => U(p, 86, 32) == (uint)FourCC.BlackMagicInterrupt);
        Check("at the end of the cast: the interrupt packet (on itself, the spell id)", $"{Weaver}/8/{(uint)FourCC.BlackMagicInterrupt:X}/1/{Weaver}/{Stone}",
              interrupt == null ? "none" : $"{Head(interrupt)}/{First(interrupt).Param}");
        Check("'is interrupted' (16); MAGIC_INTERRUPTED; no finish", $"{Weaver}>{CharId}:16/{Stone}/0", $"{Msgs()}/{Var("interrupted")}/{Actions(4).Count}");
        Check("the stopped cast lingers until cast and animation time", true, weaver.MagicState != null);
        TickUntilIdle();
        Check("then the state ends", false, weaver.MagicState != null);
        Lua("mob:setMod(xi.mod.SPELLINTERRUPT, 0)");

        Lua($"mob:addStatusEffect(xi.effect.SILENCE, {{ origin = mob, power = 1, duration = 60 }}) mob:castSpell({Stone}, pc)");
        Tick(1);
        Check("silenced: refused silently", "0/False", $"{Of(0x028).Count}/{weaver.MagicState != null}");
        Lua("mob:delStatusEffect(xi.effect.SILENCE)");

        Lua("mob:setMod(xi.mod.PARALYZE, 100)");
        Lua("mob:setLocalVar('use', 0)");
        Lua($"mob:castSpell({Stone}, pc)");
        Tick(1);
        var para = TickUntil(4);
        Check("paralyzed at the finish: category 4, the spell, recast 2, animation 508, 84 on the char",
              $"{Weaver}/4/{Stone:X}/1/{CharId}/2/508/84", para == null ? "none" : $"{Head(para)}/{Recast(para)}/{First(para).Anim}/{First(para).Msg}");
        var paraStop = Actions(8);
        Check("then the stop-casting packet; no MAGIC_USE; no MP spent", $"1/{(uint)FourCC.BlackMagicInterrupt:X}/508/0",
              paraStop.Count == 1 ? $"{paraStop.Count}/{U(paraStop[0], 86, 32):X}/{First(paraStop[0]).Anim}/{Var("use")}" : $"{paraStop.Count}");
        TickUntilIdle();
        Lua("mob:setMod(xi.mod.PARALYZE, 0)");

        Lua($"mob:castSpell({Stone}, pc)");
        Tick(1);
        Pos(-30);
        var quiet = TickUntil(8, p => U(p, 86, 32) == (uint)FourCC.BlackMagicInterrupt);
        Check("the char past 28.5 at the end: the interrupt quietly, no message, no finish", $"True//0",
              $"{quiet != null}/{Msgs()}/{Actions(4).Count}");
        TickUntilIdle();
        Pos(-3);

        Console.WriteLine("the controller: the engaged cast, onMobSpellChoose, the idle buff");
        weaver.SetMobMod(MobMod.MagicCool, 35);
        Lua("xi.zones.West_Ronfaure.mobs.Goblin_Weaver.onMobSpellChoose = function(m, t, chosen) m:setLocalVar('chosen', chosen or 0) return " + Stone + " end");
        Lua("mob:updateEnmity(pc)");
        weaver.NextMagicMs = now;
        BasicPacket aiStart = null;
        for (int i = 0; i < 10 && aiStart == null; i++)
        {
            Tick(400);
            if (Actions(8).Count > 0) aiStart = Actions(8)[0];
        }
        Check("engaged, the magic cool up: onMobSpellChoose's pick starts on the target", $"True/{Weaver}/8/{(uint)FourCC.BlackMagicCast:X}/1/{CharId}/{Stone}",
              aiStart == null ? $"{weaver.Engaged}/none" : $"{weaver.Engaged}/{Head(aiStart)}/{First(aiStart).Param}");
        Check("the aggro spell offered first: from the damage list", true, weaver.SpellContainer.DamageList.Contains((ushort)Var("chosen")));
        Check("FirstSpell spent", false, weaver.FirstSpell);
        var aiFinish = TickUntil(4);
        Check("and lands", $"{Weaver}/4/{Stone:X}/1/{CharId}", aiFinish == null ? "none" : Head(aiFinish));
        Check("the next one 17-35 s on", true, weaver.NextMagicMs >= now + 17_000 - 200 && weaver.NextMagicMs <= now + 35_000);
        TickUntilIdle();
        Lua("xi.zones.West_Ronfaure.mobs.Goblin_Weaver.onMobSpellChoose = nil");

        Lua("mob:disengage()");
        Check("disengage: the list and the target gone at once", "False/True/True",
              $"{weaver.Engaged}/{weaver.BattleTarget == null}/{weaver.Neutral}");
        weaver.MLvl = 20;
        Lua("mob:setSpellList(3)");
        Check("level 20: protect in the buff list", true, weaver.SpellContainer.BuffList.Contains(Protect));
        weaver.Mp = weaver.MaxMp;
        weaver.NextMagicMs = now;
        weaver.StatusEffects.DelStatusEffect(40);
        BasicPacket buffStart = null;
        for (int i = 0; i < 10 && buffStart == null; i++)
        {
            Tick(400);
            buffStart = Actions(8).Find(p => U(p, 86, 32) == (uint)FourCC.WhiteMagicCast);
        }
        Check("idle with the cool up: a buff starts (DoBuffTick)", true, buffStart != null);
        uint buffTarget = buffStart == null ? 0 : (uint)U(buffStart, 150, 32);
        var buffDone = TickUntil(4, null, 60); // a buff casts for up to 5 s
        Check("it lands on the mob it started on", $"{buffTarget}", buffDone == null ? "none" : $"{U(buffDone, 150, 32)}");
        var buffed = zone.ZoneInfoOf(WestRonfaure).MobById.TryGetValue(buffTarget, out var bm) ? bm : null;
        // the buff is a random pick from the buff list: the effect the finish names is what it carries
        uint buffMsg = buffDone == null ? 0 : (uint)U(buffDone, 186 + 44, 10), buffEffect = buffDone == null ? 0 : (uint)U(buffDone, 186 + 27, 17);
        Check("gains the effect (230) and carries it", $"230/True",
              $"{buffMsg}/{buffed != null && buffed.StatusEffects.HasStatusEffect((ushort)buffEffect)}");
        TickUntilIdle();

        Console.WriteLine("despawned mid-cast");
        Lua($"mob:castSpell({Stone}, pc)");
        Tick(1);
        Lua("mob:setLocalVar('ccs', mob:canChangeState() and 1 or 2)");
        Check("casting; canChangeState false", "True/2", $"{weaver.MagicState != null}/{Var("ccs")}");
        s.PacketList.Clear();
        Lua($"DespawnMob({Weaver})");
        Check("despawned: the interrupt packet, the state gone", $"1/False",
              $"{Actions(8).FindAll(p => U(p, 86, 32) == (uint)FourCC.BlackMagicInterrupt).Count}/{weaver.MagicState != null}");

        Check("no script errors along the way", "", string.Join("\n", log.FindAll(l => l.Contains("lua:") || l.Contains("luautils::") || l.Contains("rror") || l.Contains("Invalid entity"))));
    }
}
