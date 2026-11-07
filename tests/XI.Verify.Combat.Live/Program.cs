using System;
using System.Collections.Generic;
using System.IO;
using XI.Common;
using XI.Map;
using XI.Ipc;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Combat.Live -- live gate for melee (ZoneHost.Combat: CPlayerController::Engage / Disengage / ChangeTarget,
// CAttackState, CCharEntity::CanAttack, CBattleEntity::OnAttack, TakePhysicalDamage, ClaimMob / RelinquishClaim, the
// mob's death and defeat message, call for help) against a Wild Rabbit in West Ronfaure and the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 990952;
    private const ushort WestRonfaure = 100;
    private const uint Rabbit = 17186822;
    private const ushort RabbitTargId = 6;

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static ResultSet Sql(IDatabase db, string q, params BoundValue[] a) => db.Execute(q, a);

    private static string DbExp(IDatabase db)
    {
        var r = Sql(db, "SELECT war FROM char_exp WHERE charid = ?", BoundValue.Of(CharId));
        return r != null && r.Next() ? r.Get<string>(0u) : "";
    }

    private static string DbGil(IDatabase db)
    {
        var r = Sql(db, "SELECT quantity FROM char_inventory WHERE charid = ? AND location = 0 AND slot = 0", BoundValue.Of(CharId));
        return r != null && r.Next() ? r.Get<string>(0u) : "";
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

    private static ulong U(BasicPacket p, int bit, int len) => Battle2Packets.Unpack(p.Buffer, bit, len);

    // a BATTLE2's actor / type / action id / target count / first target
    private static string Battle2Head(BasicPacket p) => $"{U(p, 40, 32)}/{U(p, 82, 4)}/{U(p, 86, 32):X}/{U(p, 72, 6)}/{U(p, 150, 32)}";

    // the first target's results: resolution, param and message of each
    private static List<(int Res, int Param, int Msg, int Info)> Battle2Results(BasicPacket p)
    {
        var list = new List<(int, int, int, int)>();
        int n = (int)U(p, 182, 4);
        for (int i = 0; i < n; i++)
        {
            int at = 186 + i * 87;
            list.Add(((int)U(p, at, 3), (int)U(p, at + 27, 17), (int)U(p, at + 44, 10), (int)U(p, at + 17, 5)));
        }
        return list;
    }

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("combatverify"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Fighty"), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(0.0f), BoundValue.Of(0.0f), BoundValue.Of(0.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 300, 50, 1, 30)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 30)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, AbilityTable.Load(db, _ => true, 1.0), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        zone.VanaTicksNow = () => 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        zone.MobRng = new Random(7);
        var sent = new List<IIpcMessage>();
        zone.SendIpc = sent.Add;
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);
        Check("EXP tables load", false, log.Exists(l => l.Contains("experience points will not be calculated") || l.Contains("loadExpDifficultyCurve")));

        var info = zone.ZoneInfoOf(WestRonfaure);
        var rabbit = info.MobById[Rabbit];
        Check("rabbit up", true, rabbit.IsSpawned);
        Check("its onMobSpawn ran: ATT 1, EXP_LVL_MOD -2", "1/-2", $"{rabbit.GetMod(Mod.ATT)}/{rabbit.GetMod(Mod.EXP_LVL_MOD)}");
        // the Wild Rabbit template's loot: hare meat (common) and a rabbit hide (uncommon)
        var loot = rabbit.Template.Loot;
        Check("the template's loot", "True/slice_of_hare_meat:150,rabbit_hide:100",
              $"{rabbit.TemplateLoot}/{string.Join(",", loot.Drops.ConvertAll(d => $"{d.Item}:{d.Chance}"))}");

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
        void Pos(float dx, byte dir)
        {
            Send(0x015, 32, b =>
            {
                BitConverter.GetBytes(rabbit.X + dx).CopyTo(b, C2sPos.OffX);
                BitConverter.GetBytes(rabbit.Y).CopyTo(b, C2sPos.OffZ);
                BitConverter.GetBytes(rabbit.Z).CopyTo(b, C2sPos.OffY);
                b[C2sPos.OffDir] = dir;
            });
            s.PacketList.Clear();
        }
        void Action(ActionId id, ushort targid)
        {
            s.PacketList.Clear();
            Send(0x01A, 28, b =>
            {
                BitConverter.GetBytes(Rabbit).CopyTo(b, 4);
                BitConverter.GetBytes(targid).CopyTo(b, C2sAction.OffActIndex);
                BitConverter.GetBytes((ushort)id).CopyTo(b, C2sAction.OffActionID);
            });
        }
        List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);
        string Msg(BasicPacket p) => p == null ? "" : $"{BitConverter.ToUInt32(p.Buffer, 4)}>{BitConverter.ToUInt32(p.Buffer, 8)}:{BitConverter.ToUInt16(p.Buffer, 24)}";
        // the last message but a skill gain (38) or level up (53): the chasing rabbit runs into reach and swings, and
        // the char's evasion may rise at random
        string LastMsg()
        {
            var l = Of(0x029).FindAll(p => BitConverter.ToUInt16(p.Buffer, 24) is not (38 or 53));
            return l.Count == 0 ? "" : Msg(l[^1]);
        }
        void Tick(long ms)
        {
            s.PacketList.Clear();
            now += ms;
            zone.Tick();
        }

        Console.WriteLine("Engage: too far, not an enemy");
        Pos(-31, 0);
        Action(ActionId.Attack, RabbitTargId);
        Check("too far away (78)", $"{CharId}>{Rabbit}:78", LastMsg());
        Check("not engaged", null, c.AttackTarget);
        Action(ActionId.Attack, 0x3FF);
        Check("no target: silence", 0, Of(0x029).Count + Of(0x058).Count);

        Console.WriteLine("Engage: the attack state, the assist packet, the weapon drawn");
        Pos(-1, 0);
        Check("facing east", 0, c.Rotation);
        Action(ActionId.Attack, RabbitTargId);
        var assist = Of(0x058);
        Check("0x058 assist", true, assist.Count == 1);
        if (assist.Count == 1)
            Check("assist char / target / targid", $"{CharId}/{Rabbit}/{c.TargId}/16",
                $"{BitConverter.ToUInt32(assist[0].Buffer, 4)}/{BitConverter.ToUInt32(assist[0].Buffer, 8)}/{BitConverter.ToUInt16(assist[0].Buffer, 12)}/{(assist[0].Buffer[1] >> 1) * 4}");
        Check("engaged on the rabbit", true, c.AttackTarget == rabbit && c.BattleTarget == rabbit);
        Check("animation attack", 1, c.Animation);
        Check("first swing 3 s off", now + 3000, c.SwingAtMs);
        Check("swing delay (unarmed WAR)", true, c.SwingDelayMs > 0);

        Console.WriteLine("the first round at 3 s");
        // keep the rabbit up until the kill
        rabbit.MaxHp = rabbit.Hp = 5000;
        Tick(2999);
        Check("nothing at 2999", 0, Of(0x028).Count);
        Tick(1);
        var rounds = Of(0x028);
        Check("a 0x028", 1, rounds.Count);
        int dealt = 0;
        if (rounds.Count == 1)
        {
            Check("actor / basic attack / FourCC / 1 target / rabbit", $"{CharId}/1/306B7461/1/{Rabbit}", Battle2Head(rounds[0]));
            var results = Battle2Results(rounds[0]);
            Check("1-8 results", true, results.Count >= 1 && results.Count <= 8);
            bool shaped = true;
            foreach (var r in results)
            {
                shaped &= r.Res == 0 ? r.Msg == 1 || r.Msg == 67 : r.Res == 1 && r.Msg == 15 && r.Param == 0;
                if (r.Res == 0) dealt += r.Param;
            }
            Check("hits 1/67, misses 15 with 0", true, shaped);
            Check("hp down by the hits", rabbit.MaxHp - Math.Min(dealt, rabbit.MaxHp), rabbit.Hp);
        }
        Check("claimed", CharId, rabbit.OwnerId);
        Check("char's claimed mob", true, c.ClaimedMob == rabbit);
        Check("on the enmity list", true, rabbit.Enmity.HasId(CharId));
        Check("next swing a delay later", now + c.SwingDelayMs, c.SwingAtMs);
        Check("tp from the hits", dealt > 0, c.Tp > 0);

        Console.WriteLine("CanAttack: facing away, out of reach");
        Pos(-1, 128);
        Tick(c.SwingDelayMs);
        Check("unable to see (5)", $"{CharId}>{Rabbit}:5", LastMsg());
        Check("no round", 0, Of(0x028).Count);
        Check("still engaged", true, c.AttackTarget == rabbit);
        Tick(1);
        Check("the error waits a delay", "", LastMsg());
        Pos(-5, 0);
        Tick(c.SwingDelayMs);
        Check("out of range (4)", $"{CharId}>{Rabbit}:4", LastMsg());

        Console.WriteLine("call for help");
        Pos(-1, 0);
        Action(ActionId.Help, RabbitTargId);
        Check("calls for help (19)", $"{CharId}>{CharId}:19", LastMsg());
        Check("flag set, claim lifted", "True/0", $"{rabbit.CallForHelp}/{rabbit.OwnerId}");
        Action(ActionId.Help, RabbitTargId);
        Check("once only (22)", $"{CharId}>{CharId}:22", LastMsg());
        rabbit.EntityFlags &= ~MobEntity.EntityFlagCallForHelp;

        Console.WriteLine("AttackOff: the state ends, the lockout, the declaim");
        Tick(c.SwingDelayMs);
        Check("claimed again by the round", CharId, rabbit.OwnerId);
        Action(ActionId.AttackOff, 0);
        Tick(10);
        Check("disengaged", "True/0", $"{c.AttackTarget == null}/{c.Animation}");
        Check("lockout 1.5 s or more", true, c.EngageLockedUntilMs >= now + 1500);
        Check("claim relinquished, timer started", "True/True", $"{c.ClaimedMob == null}/{rabbit.DeclaimTimeMs == now}");
        Action(ActionId.Attack, RabbitTargId);
        Check("wait longer (94)", $"{CharId}>{Rabbit}:94", LastMsg());
        Tick(2990);
        Check("still claimed at 3 s", CharId, rabbit.OwnerId);
        Tick(10);
        Check("declaimed", 0u, rabbit.OwnerId);
        long locked = c.EngageLockedUntilMs;
        if (now < locked) Tick(locked - now);
        Action(ActionId.Attack, RabbitTargId);
        Check("engages again", true, c.AttackTarget == rabbit && Of(0x058).Count == 1);

        Console.WriteLine("ChangeTarget to nothing ends the state");
        Action(ActionId.ChangeTarget, 0x3FF);
        Tick(10);
        Check("disengaged", true, c.AttackTarget == null && c.BattleTarget == null);
        if (now < c.EngageLockedUntilMs) Tick(c.EngageLockedUntilMs - now);

        Console.WriteLine("the kill");
        rabbit.MaxHp = 33;
        rabbit.Hp = 33;
        Action(ActionId.Attack, RabbitTargId);
        Tick(3000);
        int roundsFought = 0;
        bool killFlag = false;
        while (rabbit.Hp > 0 && roundsFought < 60)
        {
            foreach (var r in Of(0x028))
                foreach (var res in Battle2Results(r)) killFlag |= (res.Info & 1) != 0;
            if (rabbit.Hp > 0) Tick(c.SwingDelayMs);
            roundsFought++;
        }
        foreach (var r in Of(0x028))
            foreach (var res in Battle2Results(r)) killFlag |= (res.Info & 1) != 0;
        Check("rabbit dead", "0/3/True", $"{rabbit.Hp}/{rabbit.Animation}/{rabbit.IsDead}");
        Check("the killing hit flagged", true, killFlag);
        Check("last attacker", CharId, rabbit.LastAttackerId);
        Check("enmity cleared, claim let go", "0/True", $"{rabbit.Enmity.Count}/{c.ClaimedMob == null}");
        Check("despawn in 15 s", now + 15000, rabbit.DespawnAtMs);
        Tick(10);
        Check("the attack state ends", "True/0", $"{c.AttackTarget == null}/{c.Animation}");
        Check("still owned until the message", CharId, rabbit.OwnerId);
        // the EXP is graded when the message goes out: an even match for the level 30 WAR, one point short of 31
        rabbit.MLvl = 30;
        uint tnl = stats.Growth.GetExpNextLevel(30);
        c.JobExp[1] = (ushort)(tnl - 1);
        // a sure one_of (a single hare meat), a sure rabbit hide (added by the ITEM_DROPS listener below), and exactly
        // 10 gil
        rabbit.Template = new MobTemplate { Name = "Sure_Rabbit" };
        var group = new LootRoll { Chance = 1000 };
        group.OneOf.Add(("slice_of_hare_meat", 1));
        rabbit.Template.Loot.Drops.Add(group);
        rabbit.SetMobMod(MobMod.GilMin, 10);
        rabbit.SetMobMod(MobMod.GilMax, 10);
        string gilBefore = DbGil(db);
        // the listeners: ITEM_DROPS adds the sure rabbit hide (xi.loot.rate.GUARANTEED, the roll the template's would
        // have made), TREASUREPOOL hears each pooled item, the char its EXP
        scripts.Lua.DoString($"local mob = GetMobByID({Rabbit}) local player = GetPlayerByID({CharId}) " +
            "mob:addListener('ITEM_DROPS', 'T_DROPS', function(m, loot) loot:addItem(856, 8) end) " +
            "mob:addListener('TREASUREPOOL', 'T_POOL', function(m, target, itemId) m:setLocalVar('tPool', m:getLocalVar('tPool') + 1) " +
            "  if itemId == 856 then m:setLocalVar('tOre', target:getID() == player:getID() and 1 or 2) end end) " +
            "player:addListener('EXPERIENCE_POINTS', 'T_EXP', function(p, m, exp) p:setLocalVar('tExp', exp) p:setLocalVar('tExpMob', m:getID()) end)",
            "=verify_combat_listeners");
        Tick(990);
        Check("defeats the rabbit (6)", $"{CharId}>{Rabbit}:6", LastMsg());
        Check("claim lifts", 0u, rabbit.OwnerId);
        // 0x02D as sender>target:msg:param
        List<string> Msgs2() => Of(0x02D).ConvertAll(p =>
            $"{BitConverter.ToUInt32(p.Buffer, 4)}>{BitConverter.ToUInt32(p.Buffer, 8)}:{BitConverter.ToUInt16(p.Buffer, 24)}:{BitConverter.ToUInt32(p.Buffer, 16)}");
        var gain = Msgs2().Find(m => m.StartsWith($"{CharId}>{CharId}:8:"));
        uint gained = gain == null ? 0 : uint.Parse(gain.Split(':')[2]);
        Console.WriteLine($"  (exp {gained}, tnl {tnl})");
        Check("experience points gained (8)", true, gained > 0);
        Check("levels up to 31 (9), the rabbit the target", true, Msgs2().Contains($"{CharId}>{Rabbit}:9:31"));
        Check("level 31, the rest carried", $"31/31/{Math.Min(gained - 1, stats.Growth.GetExpNextLevel(31) - 1)}",
              $"{c.JobLevels[1]}/{c.MLvl}/{c.JobExp[1]}");
        Check("the char's EXP saved", $"{c.JobExp[1]}", DbExp(db));
        Check("the mob's shared grade", "30/1", $"{rabbit.HiPcLvl}/{rabbit.HiPartySize}");
        // (the login's KillSession aside: the char's pos_prevzone 0 is not its zone)
        Check("no conquest without Signet", 0, sent.FindAll(m => m is ConquestEvent).Count);
        // 0x0D2 as item@dropper; the group rolls before the items
        var pool = Of(0x0D2).ConvertAll(p => $"{BitConverter.ToUInt16(p.Buffer, 16)}@{BitConverter.ToUInt32(p.Buffer, 8)}");
        Console.WriteLine($"  (pool {string.Join(" ", pool)})");
        Check("the drops reach the pool, the rabbit the dropper", $"4358@{Rabbit},856@{Rabbit}", string.Join(",", pool.GetRange(0, Math.Min(2, pool.Count))));
        Check("TREASUREPOOL: each pooled item heard, the hide for the char", $"{pool.Count}/1",
              $"{rabbit.LocalVars.GetValueOrDefault("tPool")}/{rabbit.LocalVars.GetValueOrDefault("tOre")}");
        Check("EXPERIENCE_POINTS: the char hears the gain and the mob", $"{gained}/{Rabbit}",
              $"{c.LocalVars.GetValueOrDefault("tExp")}/{c.LocalVars.GetValueOrDefault("tExpMob")}");
        scripts.Lua.DoString($"GetMobByID({Rabbit}):removeListener('T_DROPS') GetMobByID({Rabbit}):removeListener('T_POOL') " +
            $"GetPlayerByID({CharId}):removeListener('T_EXP')", "=verify_combat_listeners");
        Check("obtains 10 gil (565)", true, Msgs2().Contains($"{CharId}>{CharId}:565:10"));
        Check("the gil saved", $"{(gilBefore == "" ? 0 : long.Parse(gilBefore)) + 10}", DbGil(db));
        Check("TH and gilfinder reset", "0/0", $"{rabbit.ThLvl}/{rabbit.GilfinderLevel}");
        if (now < c.EngageLockedUntilMs) Tick(c.EngageLockedUntilMs - now);
        Action(ActionId.Attack, RabbitTargId);
        Check("a dead mob: silence", "0/True", $"{Of(0x029).Count + Of(0x058).Count}/{c.AttackTarget == null}");

        Console.WriteLine("despawn and respawn clear the combat state");
        Tick(14000);
        Tick(3100);
        Check("gone", false, rabbit.IsSpawned);
        for (int t = 0; !rabbit.IsSpawned && t < 10; t++) Tick(10_000);
        Check("respawned", true, rabbit.IsSpawned);
        Check("alive, fresh", "33/0/False/0", $"{rabbit.Hp}/{rabbit.Animation}/{rabbit.IsDead}/{rabbit.LastAttackerId}");
        Console.WriteLine("the rabbit fights back: engage, the swings, enmity");
        rabbit.MaxHp = rabbit.Hp = 5000;
        Pos(-1, 0);
        if (now < c.EngageLockedUntilMs) Tick(c.EngageLockedUntilMs - now);
        Action(ActionId.Attack, RabbitTargId);
        Tick(3000);
        Check("the char's round claims it", CharId, rabbit.OwnerId);
        Tick(400);
        Check("rabbit engaged on the char", "True/1/True", $"{rabbit.Engaged}/{rabbit.Animation}/{rabbit.BattleTarget == c}");
        long mobSwing = rabbit.SwingAtMs;
        Check("its first swing 3 s after the engage", true, mobSwing > now && mobSwing <= now + 3000);
        Action(ActionId.AttackOff, 0);
        int hpBefore = c.Hp;
        var entry = rabbit.Enmity.Get(CharId);
        int ve0 = entry?.VE ?? -1;
        Check("damage enmity on the list", true, entry != null && entry.CE > 0 && entry.VE > 0);
        BasicPacket mobRound = null;
        for (int t = 0; mobRound == null && t < 20; t++)
        {
            Tick(400);
            mobRound = Of(0x028).Find(p => U(p, 40, 32) == Rabbit);
        }
        Check("a 0x028 from the rabbit", true, mobRound != null);
        if (mobRound != null)
        {
            Check("rabbit / basic attack / 1 target / the char", $"{Rabbit}/1/1/{CharId}",
                $"{U(mobRound, 40, 32)}/{U(mobRound, 82, 4)}/{U(mobRound, 72, 6)}/{U(mobRound, 150, 32)}");
            int taken = 0;
            foreach (var r in Battle2Results(mobRound)) if (r.Res == 0) taken += r.Param;
            Check("char hp down by the hits", hpBefore - taken, c.Hp);
        }
        Check("the swing at its time, the next a delay later", true, rabbit.SwingAtMs == mobSwing + rabbit.SwingDelayMs);
        int ve1 = rabbit.Enmity.Get(CharId).VE;
        Check("volatile enmity decays 24 a tick", true, ve1 < ve0 && (ve0 - ve1) % 24 == 0);

        Console.WriteLine("the chase");
        Pos(-8, 0);
        float far = (float)Math.Sqrt(Math.Pow(rabbit.X - c.X, 2) + Math.Pow(rabbit.Z - c.Z, 2));
        Tick(400);
        float nearer = (float)Math.Sqrt(Math.Pow(rabbit.X - c.X, 2) + Math.Pow(rabbit.Z - c.Z, 2));
        Check("steps toward the char", true, nearer < far - 0.5f);
        Check("faces west, at the char", 128, rabbit.Rotation);
        for (int t = 0; t < 20; t++) Tick(400);
        float close = (float)Math.Sqrt(Math.Pow(rabbit.X - c.X, 2) + Math.Pow(rabbit.Z - c.Z, 2));
        Check("stops in melee reach", true, close <= rabbit.ModelHitboxSize + 2.4f && close > 1.0f);

        Console.WriteLine("the char dies");
        c.Hp = 1;
        // Reraise II on: dying wears it (a death effect), and its onEffectLose hands the raise level over
        c.StatusEffects.AddStatusEffect(new StatusEffect(113, 113, 2, 0, 3600_000));
        // level 31 (at EXP_LOSS_LEVEL) loses 8% of its next level; short of it, it falls to 30
        int deathLevel = c.JobLevels[1], deathExp = c.JobExp[1];
        int loss = (int)stats.Growth.GetExpNextLevel(deathLevel) * 8 / 100;
        bool died = false;
        for (int t = 0; !died && t < 40; t++)
        {
            Tick(400);
            died = Of(0x029).Exists(p => Msg(p) == $"{Rabbit}>{CharId}:97");
        }
        Check("defeated by the rabbit (97)", true, died);
        Check("dead: 0 hp, animation death, the death timer", $"0/3/{now}", $"{c.Hp}/{c.Animation}/{c.DeathAtMs}");
        Console.WriteLine($"  (level {deathLevel}, exp {deathExp}, loss {loss})");
        Check("the loss outweighs the EXP", true, deathExp < loss);
        Check("falls to level 30 (11)", true, Msgs2().Contains($"{CharId}>{CharId}:11:30"));
        Check("level 30, the rest taken from its top", $"30/30/{Math.Max(0, (int)stats.Growth.GetExpNextLevel(30) - (loss - deathExp))}",
              $"{c.JobLevels[1]}/{c.MLvl}/{c.JobExp[1]}");
        Check("the EXP saved", $"{c.JobExp[1]}", DbExp(db));
        var lost = Sql(db, "SELECT value FROM char_vars WHERE charid = ? AND varname = 'expLost'", BoundValue.Of(CharId));
        Check("expLost kept for a raise", $"{loss}", lost != null && lost.Next() ? lost.Get<string>(0u) : "");
        Check("reraise worn off into the raise level", "2/False", $"{c.HasRaise}/{c.StatusEffects.HasStatusEffect(113)}");
        long diedAt = c.DeathAtMs;
        Tick(400);
        Check("rabbit disengages", "False/0/0/0", $"{rabbit.Engaged}/{rabbit.Animation}/{rabbit.Enmity.Count}/{rabbit.OwnerId}");
        Tick(400);
        Check("no more swings", 0, Of(0x028).Count);

        Console.WriteLine("raise");
        string Res() => Of(0x0F9).Count == 0 ? "" : $"{BitConverter.ToUInt32(Of(0x0F9)[0].Buffer, 4)}/{BitConverter.ToUInt16(Of(0x0F9)[0].Buffer, 10)}";
        bool menuEarly = false;
        while (now < diedAt + 8000) { Tick(400); menuEarly |= Res() != ""; }
        Check("no raise menu before 8 s", false, menuEarly);
        Tick(400);
        Check("the raise menu (0x0F9 raise)", $"{CharId}/1", Res());
        Tick(400);
        Check("sent once", "", Res());
        int expDown = c.JobExp[1];
        s.PacketList.Clear();
        Send(0x01A, 28, b =>
        {
            BitConverter.GetBytes(CharId).CopyTo(b, 4);
            BitConverter.GetBytes(c.TargId).CopyTo(b, C2sAction.OffActIndex);
            BitConverter.GetBytes((ushort)ActionId.RaiseMenu).CopyTo(b, C2sAction.OffActionID);
        });
        var raised = Of(0x028).Find(p => U(p, 40, 32) == CharId);
        Check("the raise II animation (category 4, 512)", $"{CharId}/4/{CharId}/512",
              raised == null ? "" : $"{U(raised, 40, 32)}/{U(raised, 82, 4)}/{U(raised, 150, 32)}/{U(raised, 186 + 5, 12)}");
        Check("a quarter of the HP back", (int)(c.MaxHp * 0.25), c.Hp);
        // level 30 gets half the loss back (to 50), quietly
        Check("half the EXP lost returned", expDown + (int)Math.Ceiling(loss * 0.5), c.JobExp[1]);
        Check("no EXP message for it", false, Msgs2().Exists(m => m.StartsWith($"{CharId}>{CharId}:8:")));
        Check("weakness 1 for 5 minutes, the raise spent", "True/1/0",
              $"{c.StatusEffects.HasStatusEffect(1)}/{c.WeaknessLvl}/{c.HasRaise}");
        Check("still lying down", Anim.Death, c.Animation);
        Tick(2400);
        Check("up 2 s on", $"{Anim.None}/0", $"{c.Animation}/{c.DeathAtMs}");
        var lost2 = Sql(db, "SELECT value FROM char_vars WHERE charid = ? AND varname = 'expLost'", BoundValue.Of(CharId));
        Check("expLost spent", false, lost2 != null && lost2.Next());

        Console.WriteLine("aggro (tapMobAggro on the position update): the rabbit made aggressive, by sound");
        Console.WriteLine($"  (zone {c.Zone}, rabbit engaged {rabbit.Engaged}, neutral {rabbit.Neutral}, aggro {rabbit.Aggro}, detects {rabbit.GetMobMod(MobMod.Detection)}, sound {rabbit.GetMobMod(MobMod.SoundRange)})");
        rabbit.SetMobMod(MobMod.Detection, 2);
        if (rabbit.GetMobMod(MobMod.SoundRange) == 0) rabbit.SetMobMod(MobMod.SoundRange, 8);
        string Aggroed() => $"{rabbit.Engaged}/{(rabbit.BattleTarget as CharData)?.Id ?? 0}";
        for (int i = 0; i < 40; i++) Tick(400);
        Check("the rabbit out of neutral 15 s after it let the dead char go", false, rabbit.Neutral);
        Pos(-3, 0);
        c.Animation = Anim.Sit;
        Pos(-3, 0);
        Check("a passive mob ignores a sitting char", "False/0", Aggroed());
        rabbit.Aggro = true;
        c.Animation = Anim.None;
        Pos(-3, 0);
        Check("too weak for the char's level 30: no aggro standing", "False/0", Aggroed());
        c.Animation = Anim.Sit;
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectId.Sneak, EffectId.Sneak, 0, 0, 60_000));
        Pos(-3, 0);
        Check("sneak hides a sitting char from hearing", "False/0", Aggroed());
        c.StatusEffects.DelStatusEffectSilent(EffectId.Sneak);
        Pos(-12, 0);
        Check("out of sound range", "False/0", Aggroed());
        c.GmLevel = 3;
        Pos(-3, 0);
        Check("a GM (visible level 3) is ignored", "False/0", Aggroed());
        c.GmLevel = 0;
        rabbit.SetMobMod(MobMod.NoAggro, 1);
        Pos(-3, 0);
        Check("NO_AGGRO", "False/0", Aggroed());
        rabbit.SetMobMod(MobMod.NoAggro, 0);
        // a Monipulator whose ecosystem preys on the rabbit's (plantoid > beast) is ignored
        c.EcoSystem = Ecosystem.Plantoid;
        Pos(-3, 0);
        Check("a Monipulator preying on beasts (plantoid) is ignored", "False/0", Aggroed());
        c.EcoSystem = Ecosystem.Lizard;
        Pos(-3, 0);
        Check("one the beasts prey on (lizard) is not: aggro", $"True/{CharId}", Aggroed());
        rabbit.Engaged = false;
        rabbit.BattleTarget = null;
        rabbit.Animation = Anim.None;
        c.EcoSystem = Ecosystem.Humanoid;
        Pos(-3, 0);
        Check("sitting within sound range: aggro", $"True/{CharId}", Aggroed());
        Check("engaged unclaimed, weapon out, no enmity yet", $"0/{Anim.Attack}/0", $"{rabbit.OwnerId}/{rabbit.Animation}/{rabbit.Enmity.Count}");

        Console.WriteLine("mounted: lost, the mob disengages and is neutral for 15 s");
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectId.Mounted, EffectId.Mounted, 0, 0, 0));
        Tick(400);
        Check("disengaged, neutral", "False/True", $"{rabbit.Engaged}/{rabbit.Neutral}");
        c.StatusEffects.DelStatusEffectSilent(EffectId.Mounted);
        c.Animation = Anim.Sit;
        long disengagedAt = now;
        Pos(-3, 0);
        Tick(400);
        Pos(-3, 0);
        Check("no aggro while neutral", "False/0", Aggroed());
        Tick(disengagedAt + 15000 - now);
        Pos(-3, 0);
        Check("still neutral at 15 s", "False/0", Aggroed());
        Tick(400);
        Pos(-3, 0);
        Check("aggro again past 15 s", $"True/{CharId}", Aggroed());

        Console.WriteLine("hidden or locked: lost (CheckHide, CheckLock)");
        short rabbitDetects = rabbit.GetMobMod(MobMod.Detection);
        byte charJob = c.MJob;
        c.MJob = 6;
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectId.Hide, EffectId.Hide, 0, 0, 0));
        Tick(400);
        Check("a THF under Hide, the rabbit hears: still on it", $"True/{CharId}", $"{rabbit.Engaged}/{rabbit.BattleTarget?.Id}");
        rabbit.SetMobMod(MobMod.Detection, 0x001); // sight only
        Tick(400);
        Check("a mob that only sees: loses the hidden THF", "False/0", Aggroed());
        c.StatusEffects.DelStatusEffectSilent(EffectId.Hide);
        c.MJob = charJob;
        rabbit.SetMobMod(MobMod.Detection, rabbitDetects);
        // the 15 s neutral cut short, so the clock (and the raise's weakness below) stays where it was
        rabbit.NeutralAtMs = now - 16000;
        Tick(400);
        Pos(-3, 0);
        Check("aggro again once neutral is over", $"True/{CharId}", Aggroed());
        c.Locked = true;
        Tick(400);
        Check("a locked char: lost", "False/0", Aggroed());
        c.Locked = false;
        rabbit.NeutralAtMs = now - 16000;
        Tick(400);
        Pos(-3, 0);
        Check("aggro again once unlocked", $"True/{CharId}", Aggroed());

        Console.WriteLine("the idle rest: 10% HP every 10 s");
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectId.Mounted, EffectId.Mounted, 0, 0, 0));
        Tick(400);
        c.StatusEffects.DelStatusEffectSilent(EffectId.Mounted);
        rabbit.Hp = 1;
        rabbit.Tp = 100;
        rabbit.HiPcLvl = 30;
        rabbit.MobHealAtMs = now;
        Tick(9600);
        Check("nothing before 10 s", "1/100", $"{rabbit.Hp}/{rabbit.Tp}");
        Tick(400);
        Check("10% back, 50 TP off", $"{1 + (int)(rabbit.MaxHp * 0.1f)}/50", $"{rabbit.Hp}/{rabbit.Tp}");
        for (int i = 0; i < 250; i++) Tick(400);
        Check("full at last, the EXP tracking reset", $"{rabbit.MaxHp}/0/0", $"{rabbit.Hp}/{rabbit.Tp}/{rabbit.HiPcLvl}");

        Console.WriteLine("mob scripts: GetMobByID, the bindings, the listeners and the hooks");
        var lua = scripts.Lua;
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local mob = GetMobByID({Rabbit})\n" + code, "=verify_mobscripts");
            return string.Join(",", got);
        }
        uint missing = (Rabbit & ~0xFFFu) | 0xFFE;
        Check("GetMobByID: the rabbit; a missing one nil, warned", $"{Rabbit},true,True",
              Lua($"report(mob:getID()) report(tostring(GetMobByID({missing}) == nil))") + "," +
              log.Exists(l => l.Contains($"GetMobByID Mob doesn't exist ({missing})")));
        Check("GetEntityByID: the char, the rabbit", $"{CharId},{Rabbit}",
              Lua($"report(GetEntityByID({CharId}):getID()) report(GetEntityByID({Rabbit}):getID())"));
        Check("isAlive / isDead / isSpawned / isEngaged", "true,false,true,false",
              Lua("for _, f in ipairs({ 'isAlive', 'isDead', 'isSpawned', 'isEngaged' }) do report(tostring(mob[f](mob))) end"));
        Check("getPos: x, y, z, rot", $"{Math.Floor(rabbit.X * 100.0)}/{Math.Floor(rabbit.Y * 100.0)}/{Math.Floor(rabbit.Z * 100.0)}/{rabbit.Rotation}",
              Lua("local p = mob:getPos() report(string.format('%d/%d/%d/%d', math.floor(p.x * 100), math.floor(p.y * 100), math.floor(p.z * 100), p.rot))"));
        Check("getSpawnPos", $"{Math.Floor(rabbit.SpawnX * 100.0)}/{rabbit.SpawnRotation}",
              Lua("local p = mob:getSpawnPos() report(string.format('%d/%d', math.floor(p.x * 100), p.rot))"));
        Lua("mob:addListener('TICK', 'T_TICK', function(m, elapsed) m:setLocalVar('tTicks', m:getLocalVar('tTicks') + 1) m:setLocalVar('tElapsed', elapsed) end)");
        Tick(400);
        Tick(400);
        Lua("mob:removeListener('T_TICK')");
        Check("TICK: each zone tick, the milliseconds since the last", "2/400",
              $"{rabbit.LocalVars.GetValueOrDefault("tTicks")}/{rabbit.LocalVars.GetValueOrDefault("tElapsed")}");
        uint immunity = rabbit.Immunity;
        rabbit.Immunity = 0;
        Check("addImmunity / delImmunity / hasImmunity", "false,true,4",
              Lua("mob:addImmunity(1) mob:addImmunity(4) mob:delImmunity(1) report(tostring(mob:hasImmunity(1))) report(tostring(mob:hasImmunity(4)))") +
              "," + rabbit.Immunity);
        rabbit.Immunity = immunity;
        uint respawn = rabbit.RespawnTimeSec;
        Check("setRespawnTime while up: kept, nothing pending", "0,600/True",
              Lua("mob:setRespawnTime(600) report(mob:getRespawnTime())") + $",{rabbit.RespawnTimeSec}/{rabbit.AllowRespawn}");
        s.PacketList.Clear();
        int RabbitUpdates() => Of(0x00E).FindAll(p => BitConverter.ToUInt32(p.Buffer, 4) == Rabbit).Count;
        Lua("mob:setAnimationSub(2)");
        int sentFirst = RabbitUpdates();
        s.PacketList.Clear();
        Lua("mob:setAnimationSub(2) mob:setAnimationSub(0, false)");
        Check("setAnimationSub: an update, none for the same or with sendUpdate false; getAnimationSub", "1/0/0",
              $"{sentFirst}/{RabbitUpdates()}/{Lua("report(mob:getAnimationSub())")}");
        Lua("mob:setAutoAttackEnabled(false) mob:setMagicCastingEnabled(false) mob:setMobAbilityEnabled(false)");
        Check("the controller flags off", "False/False/False", $"{rabbit.AutoAttackEnabled}/{rabbit.MagicCastingEnabled}/{rabbit.MobAbilityEnabled}");

        // listeners, and the script's hooks set on its cached object
        Lua(@"
            local vars = function(m, k) m:setLocalVar(k, m:getLocalVar(k) + 1) end
            mob:addListener('ENGAGE', 'VERIFY_ENGAGE', function(m, target) m:setLocalVar('engagedBy', target:getID()) end)
            mob:addListener('COMBAT_TICK', 'VERIFY_TICK', function(m) vars(m, 'combatTicks') end)
            mob:addListener('ROAM_TICK', 'VERIFY_ROAM', function(m) vars(m, 'wrong') end)
            mob:addListener('ROAM_TICK', 'VERIFY_ROAM', function(m) vars(m, 'roamTicks') end)
            mob:addListener('ROAM_TICK', 'VERIFY_ONCE', function(m) vars(m, 'once') m:removeListener('VERIFY_ONCE') end)
            mob:addListener('ROAM_TICK', 'VERIFY_ERROR', function(m) error('listener boom') end)
            mob:addListener('DISENGAGE', 'VERIFY_DISENGAGE', function(m) vars(m, 'disengaged') end)
            mob:addListener('DEATH', 'VERIFY_DEATH', function(m, killer) m:setLocalVar('killedBy', killer and killer:getID() or 1) end)
            mob:addListener('DESPAWN', 'VERIFY_DESPAWN', function(m) vars(m, 'despawned') end)
            mob:addListener('SPAWN', 'VERIFY_SPAWN', function(m) vars(m, 'spawned') end)
            mob:addListener('CUSTOM', 'VERIFY_CUSTOM', function(m, a, b) m:setLocalVar('custom', a + b) end)
            mob:triggerListener('CUSTOM', mob, 3, 4)
            local script = xi.zones.West_Ronfaure.mobs.Wild_Rabbit
            script.onMobEngage = function(m, target) m:setLocalVar('scriptEngaged', target:getID()) end
            script.onMobFight = function(m, target) vars(m, 'fights') end
            script.onMobRoam = function(m) vars(m, 'roams') end
            script.onMobDisengage = function(m, weather) m:setLocalVar('weather', weather + 1) end
            script.onMobDeath = function(m, player, optParams)
                m:setLocalVar('deathPlayer', player and player:getID() or 1)
                m:setLocalVar('deathFlags', (optParams.isKiller and 1 or 0) + (optParams.noKiller and 2 or 0))
            end
            script.onMobDespawn = function(m) vars(m, 'scriptDespawned') end");
        uint Var(string k) => rabbit.LocalVars.TryGetValue(k, out var v) ? v : 0;
        log.Clear();
        Check("triggerListener: the arguments past the event name", 7u, Var("custom"));
        rabbit.LastRoamScriptMs = now;
        for (int i = 0; i < 7; i++) Tick(400);
        Check("no roam script before 3 s", "0/0", $"{Var("roamTicks")}/{Var("roams")}");
        Tick(400);
        Check("ROAM_TICK (the listener replaced by identifier) and onMobRoam at 3 s; a removed listener once",
              "1/1/0/1", $"{Var("roamTicks")}/{Var("roams")}/{Var("wrong")}/{Var("once")}");
        for (int i = 0; i < 8; i++) Tick(400);
        Check("again 3 s on; the removed one not; a failing listener logged", "2/2/1/True",
              $"{Var("roamTicks")}/{Var("roams")}/{Var("once")}/{log.Exists(l => l.Contains("Error in listener event ROAM_TICK") && l.Contains("listener boom"))}");

        // too weak for a standing level 30: it hears the char sitting
        c.Animation = Anim.Sit;
        Pos(-3, 0);
        c.Animation = Anim.None;
        Pos(-1, 0);
        Check("aggro: ENGAGE with the target, onMobEngage, the char's next event from the mob", $"{CharId}/{CharId}/True/True",
              $"{Var("engagedBy")}/{Var("scriptEngaged")}/{c.EventPreparation.TargetEntity == rabbit}/{c.EventPreparation.ScriptFile.EndsWith("/mobs/Wild_Rabbit.lua")}");
        int hpUnhit = c.Hp;
        for (int i = 0; i < 20; i++) Tick(400);
        Check("COMBAT_TICK and onMobFight each AI tick", true, Var("combatTicks") >= 15 && Var("fights") == Var("combatTicks"));
        Check("auto-attack off: no swings in 8 s", hpUnhit, c.Hp);
        Lua("mob:setAutoAttackEnabled(true) mob:setMagicCastingEnabled(true) mob:setMobAbilityEnabled(true)");

        rabbit.Hp = 1;
        Action(ActionId.Attack, RabbitTargId);
        for (int i = 0; rabbit.Hp > 0 && i < 60; i++) Tick(c.SwingDelayMs > 0 ? c.SwingDelayMs : 1000);
        Check("the rabbit killed", "0/True", $"{rabbit.Hp}/{rabbit.IsDead}");
        Check("DEATH with the owner as the killer", CharId, Var("killedBy"));
        Tick(1000);
        Check("onMobDeath through InteractionGlobal: the killer, isKiller", $"{CharId}/1", $"{Var("deathPlayer")}/{Var("deathFlags")}");
        Tick(14100);
        Tick(3100);
        Check("gone: DESPAWN and onMobDespawn", "False/1/1", $"{rabbit.IsSpawned}/{Var("despawned")}/{Var("scriptDespawned")}");
        long? pending = zone.ZoneInfoOf(WestRonfaure).Spawner.RemainingRespawnMs(rabbit, now);
        Check("setRespawnTime's 600 s: the respawn pending", true, pending is long ms && ms > 500_000 && ms <= 600_000);
        Check("getRespawnTime", true, int.Parse(Lua("report(mob:getRespawnTime())")) > 500);
        Lua("mob:setRespawnTime(0)");
        Check("setRespawnTime(0): no respawn", "False/False/0", $"{zone.ZoneInfoOf(WestRonfaure).Spawner.IsRegistered(rabbit)}/{rabbit.AllowRespawn}/{Lua("report(mob:getRespawnTime())")}");
        Lua("mob:setRespawnTime(5)");
        for (int t = 0; !rabbit.IsSpawned && t < 10; t++) Tick(10_000);
        Check("setRespawnTime(5) while down: respawned; SPAWN, onMobSpawn's mods again", "True/1/1", $"{rabbit.IsSpawned}/{Var("spawned")}/{rabbit.GetMod(Mod.ATT)}");
        rabbit.RespawnTimeSec = respawn;
        Lua(@"
            for _, id in ipairs({ 'VERIFY_ENGAGE', 'VERIFY_TICK', 'VERIFY_ROAM', 'VERIFY_ERROR', 'VERIFY_DISENGAGE', 'VERIFY_DEATH', 'VERIFY_DESPAWN', 'VERIFY_SPAWN', 'VERIFY_CUSTOM' }) do
                mob:removeListener(id)
            end
            local script = xi.zones.West_Ronfaure.mobs.Wild_Rabbit
            script.onMobEngage, script.onMobFight, script.onMobRoam, script.onMobDisengage, script.onMobDeath, script.onMobDespawn = nil, nil, nil, nil, nil, nil");

        Console.WriteLine("mob bindings: timer / queue, TP / HP / MP, flags, status, enmity, spawning");
        log.Clear();
        Lua(@"
            order = {}
            mob:timer(1000, function(m) table.insert(order, 'timer') end)
            mob:queue(500, function(m) table.insert(order, 'queue') end)
            mob:timer(500, function(m) table.insert(order, 'early') end)
            mob:timer(800, function(m) error('timer boom') end)
            mob:timer(900, function(m) table.insert(order, 'cleared') end)");
        Tick(400);
        Check("nothing due at 400 ms", "", Lua("report(table.concat(order, ' '))"));
        Tick(200);
        Check("past 500 ms: the timer first, then the queued action", "early queue", Lua("report(table.concat(order, ' '))"));
        Lua("mob:clearTimerQueue() mob:timer(400, function(m) table.insert(order, 'timer') end)");
        Tick(400);
        Check("clearTimerQueue: the pending ones gone; a deadline is not yet due at it", "early queue", Lua("report(table.concat(order, ' '))"));
        Tick(1);
        Check("then due; the cleared failing timer never ran", "early queue timer/False",
              Lua("report(table.concat(order, ' '))") + "/" + log.Exists(l => l.Contains("timer boom")));
        Lua("mob:timer(0, function(m) error('timer boom') end)");
        Tick(1);
        Check("a failing timer logged", true, log.Exists(l => l.Contains($"CAIActionQueue Lua action for {rabbit.Name} ({Rabbit})") && l.Contains("timer boom")));

        Check("setTP / getTP", "1500", Lua("mob:setTP(1500) report(mob:getTP())"));
        Check("setHP: clamped to max; setMP", $"{rabbit.MaxHp}/{rabbit.MaxHp}/0",
              Lua("mob:setHP(99999) report(mob:getHP()) mob:setMP(5)") + $"/{rabbit.Hp}/{rabbit.Mp}");
        Check("setUnkillable: setHP(0) leaves 1; getUnkillable", "1,true",
              Lua("mob:setUnkillable(true) mob:setHP(0) report(mob:getHP()) report(tostring(mob:getUnkillable()))"));
        Check("unkillable: damage leaves 1", "1", Lua($"mob:takeDamage(500) report(mob:getHP())"));
        Lua("mob:setUnkillable(false) mob:setHP(99999) mob:setTP(0)");
        Lua($@"mob:addListener('TAKE_DAMAGE', 'VERIFY_TAKE', function(m, amount, attacker, atkType, dmgType)
                   m:setLocalVar('took', amount * 1000 + atkType * 10 + dmgType) m:setLocalVar('tookFrom', attacker and attacker:getID() or 1) end)
               mob:takeDamage(10, GetEntityByID({CharId}), 2, 3)");
        Check("takeDamage: TAKE_DAMAGE with the amount, attacker, types; the last attacker", $"10023/{CharId}/{CharId}/{rabbit.MaxHp - 10}",
              $"{Var("took")}/{Var("tookFrom")}/{rabbit.LastAttackerId}/{rabbit.Hp}");
        Check("delHP: no attacker, the last one cleared; addHP's amount; getHPP / getMaxHP", $"1,0,20,100,{rabbit.MaxHp}/0",
              Lua("mob:delHP(10) report(mob:getLocalVar('tookFrom')) report(mob:addHP(0)) report(mob:addHP(50)) report(mob:getHPP()) report(mob:getMaxHP())") + $"/{rabbit.LastAttackerId}");
        Lua("mob:removeListener('VERIFY_TAKE')");
        // the wake-up and bind break reach a mob too (battleutils::BindBreakCheck: 95%)
        Check("takeDamage on a sleeping mob: woken", "true/false",
              Lua($"mob:addStatusEffect(xi.effect.SLEEP_I, {{ power = 1, duration = 60, origin = mob }}) report(tostring(mob:hasStatusEffect(xi.effect.SLEEP_I))) " +
                  $"mob:takeDamage(1, GetEntityByID({CharId})) report(tostring(mob:hasStatusEffect(xi.effect.SLEEP_I)))").Replace(",", "/"));
        Check("takeDamage { wakeUp = false }: still asleep", "true",
              Lua($"mob:addStatusEffect(xi.effect.SLEEP_I, {{ power = 1, duration = 60, origin = mob }}) mob:takeDamage(1, GetEntityByID({CharId}), 0, 0, {{ wakeUp = false }}) " +
                  "report(tostring(mob:hasStatusEffect(xi.effect.SLEEP_I)))"));
        Lua("mob:delStatusEffect(xi.effect.SLEEP_I)");
        int bindBreaks = 0;
        for (int i = 0; i < 20; i++)
            if (Lua($"mob:addStatusEffect(xi.effect.BIND, {{ power = 1, duration = 60, origin = mob }}) mob:takeDamage(1, GetEntityByID({CharId})) " +
                    "report(tostring(mob:hasStatusEffect(xi.effect.BIND))) mob:delStatusEffect(xi.effect.BIND)") == "false") bindBreaks++;
        Check("takeDamage with an attacker: bind breaks (95%)", true, bindBreaks >= 15);
        Check("no attacker, or { breakBind = false }: bind holds", "true/true",
              Lua($"mob:addStatusEffect(xi.effect.BIND, {{ power = 1, duration = 60, origin = mob }}) mob:takeDamage(1) report(tostring(mob:hasStatusEffect(xi.effect.BIND))) " +
                  $"mob:takeDamage(1, GetEntityByID({CharId}), 0, 0, {{ wakeUp = true }}) report(tostring(mob:hasStatusEffect(xi.effect.BIND))) " +
                  "mob:delStatusEffect(xi.effect.BIND)").Replace(",", "/"));
        Lua("mob:setHP(99999)");
        // bind on a char: the effect's setBaseSpeed(0), then its power (the base speed) back
        Check("BIND on a char: base speed and speed 0, then 50 again", "0/0/50/50",
              Lua($"local p = GetPlayerByID({CharId}) p:addStatusEffect(xi.effect.BIND, {{ power = 1, duration = 60, origin = mob }}) report(p:getBaseSpeed())") + $"/{c.Speed}/" +
              Lua($"local p = GetPlayerByID({CharId}) p:delStatusEffect(xi.effect.BIND) report(p:getBaseSpeed())") + $"/{c.Speed}");
        Check("setBehavior / getBehavior; setDelay the main weapon's", "4/240", Lua("mob:setBehavior(4) report(mob:getBehavior())") + $"/{(Lua("mob:setDelay(240)") == "" ? rabbit.MainDelay : 0)}");
        Lua("mob:setBehavior(0)");
        Check("setUntargetable: the 0x800 flag; hideName", "true/2048/true/true",
              Lua("mob:setUntargetable(true) report(tostring(mob:getUntargetable())) mob:hideName(true) report(tostring(mob:isNameHidden()))").Replace(",", $"/{rabbit.EntityFlags & 0x800}/") + $"/{rabbit.Untargetable.ToString().ToLower()}");
        Lua("mob:setUntargetable(false) mob:hideName(false)");
        Check("isNM, isUndead: a rabbit neither", "false,false", Lua("report(tostring(mob:isNM())) report(tostring(mob:isUndead()))"));
        Check("setRotation / getRotPos", "64", Lua("mob:setRotation(64) report(mob:getRotPos())"));
        Check("lookAt: east 0, west 128", "0,128",
              Lua("local p = mob:getPos() mob:lookAt(p.x + 5, p.y, p.z) report(mob:getRotPos()) mob:lookAt({ x = p.x - 5, y = p.y, z = p.z }) report(mob:getRotPos())"));
        Pos(-1, 0);
        s.PacketList.Clear();
        Lua("mob:entityAnimationPacket('atk0')");
        var anim = Of(0x038);
        Check("entityAnimationPacket: 0x038 to the char in range", $"1/{Rabbit}/{Rabbit}/{(uint)FourCC.BasicAttack}/{RabbitTargId}",
              anim.Count == 1 ? $"1/{BitConverter.ToUInt32(anim[0].Buffer, 4)}/{BitConverter.ToUInt32(anim[0].Buffer, 8)}/{BitConverter.ToUInt32(anim[0].Buffer, 12)}/{BitConverter.ToUInt16(anim[0].Buffer, 16)}" : $"{anim.Count}");
        Check("getBattlefield / getInstance nil", "true,true", Lua("report(tostring(mob:getBattlefield() == nil)) report(tostring(mob:getInstance() == nil))"));

        Check("idle: action 16, no target", "16,true", Lua("report(mob:getCurrentAction()) report(tostring(mob:getTarget() == nil))"));
        Lua($"mob:updateClaim(GetEntityByID({CharId}))");
        Check("updateClaim(player): claimed", CharId, rabbit.OwnerId);
        Lua("mob:updateClaim()");
        Check("updateClaim(): unclaimed", 0u, rabbit.OwnerId);
        Lua($"mob:updateEnmity(GetEntityByID({CharId}))");
        Check("updateEnmity: engaged on the char, action 1", $"1,{CharId},0", Lua("report(mob:getCurrentAction()) report(mob:getTarget():getID()) report(mob:getBattleTime())"));
        Tick(2000);
        Check("getBattleTime: 2 s on", "2", Lua("report(mob:getBattleTime())"));
        rabbit.Enmity.UpdateEnmity(c, 100, 200);
        Lua($"mob:resetEnmity(GetEntityByID({CharId}))");
        var enmityEntry = rabbit.Enmity.Get(c.Id);
        Check("resetEnmity: CE / VE zeroed", "0/0", enmityEntry == null ? "none" : $"{enmityEntry.CE}/{enmityEntry.VE}");

        Lua("mob:setLocalVar('kept', 1)");
        s.PacketList.Clear();
        Lua($"DespawnMob({Rabbit})");
        Check("DespawnMob: fading, action 24", "True/24", $"{rabbit.FadeAtMs != 0}/{Lua("report(mob:getCurrentAction())")}");
        var fade = Of(0x038);
        Check("CDespawnState: the fade-out (kesu) 0x038 to the char in range", $"1/{Rabbit}/{(uint)FourCC.FadeOut}",
              fade.Count == 1 ? $"1/{BitConverter.ToUInt32(fade[0].Buffer, 4)}/{BitConverter.ToUInt32(fade[0].Buffer, 12)}" : $"{fade.Count}");
        Tick(3100);
        Check("then gone", false, rabbit.IsSpawned);
        Lua("mob:timer(5000, function(m) table.insert(order, 'stale') end)");
        Check("SpawnMob: returns the mob, spawned; the local vars and queues reset", $"{Rabbit}/True/0",
              Lua($"report(SpawnMob({Rabbit}):getID())") + $"/{rabbit.IsSpawned}/{Var("kept")}");
        Tick(5100);
        Check("the timer from before the spawn never ran", "early queue timer", Lua("report(table.concat(order, ' '))"));
        log.Clear();
        Lua($"SpawnMob({Rabbit}) SpawnMob({missing})");
        Check("SpawnMob: already spawned and missing logged", "True/True",
              $"{log.Exists(l => l.Contains($"SpawnMob: {Rabbit} <{rabbit.Name}> is already spawned"))}/{log.Exists(l => l.Contains($"SpawnMob: mob <{missing}> not found"))}");
        uint respawnBefore = rabbit.RespawnTimeSec;
        Lua("mob:spawn(nil, 77)");
        Check("spawn(nil, respawn): only the respawn time", "77/True/True", $"{rabbit.RespawnTimeSec}/{rabbit.AllowRespawn}/{rabbit.IsSpawned}");
        rabbit.RespawnTimeSec = respawnBefore;
        Lua($"DespawnMob({Rabbit}, 5)");
        Check("DespawnMob(id, 5): a despawn timer", now + 5000, rabbit.DespawnAtMs);
        Lua($"DespawnMob({Rabbit}, 0)");
        Check("DespawnMob(id, 0): the timer off", 0L, rabbit.DespawnAtMs);
        var spawnPoint = (rabbit.SpawnX, rabbit.SpawnY, rabbit.SpawnZ, rabbit.SpawnRotation);
        Check("setSpawn", "10/20/30/7", Lua("mob:setSpawn(10, 20, 30, 7)") + $"{rabbit.SpawnX}/{rabbit.SpawnY}/{rabbit.SpawnZ}/{rabbit.SpawnRotation}");
        (rabbit.SpawnX, rabbit.SpawnY, rabbit.SpawnZ, rabbit.SpawnRotation) = spawnPoint;

        Console.WriteLine("mob skills: useMobAbility, the ready, the finish, the target search, the controller's TP move");
        log.Clear();
        // a 0x028's targets, each with one result: (id, res, kind, anim, info, param, msg)
        List<(uint Id, int Res, int Kind, int Anim, int Info, int Param, int Msg)> Targets(BasicPacket p)
        {
            var list = new List<(uint, int, int, int, int, int, int)>();
            int n = (int)U(p, 72, 6), at = 150;
            for (int i = 0; i < n; i++)
            {
                int r = at + 36;
                list.Add(((uint)U(p, at, 32), (int)U(p, r, 3), (int)U(p, r + 3, 2), (int)U(p, r + 5, 12), (int)U(p, r + 17, 5),
                          (int)U(p, r + 27, 17), (int)U(p, r + 44, 10)));
                at += 36 + 87 * (int)U(p, at + 32, 4);
            }
            return list;
        }
        List<BasicPacket> Actions(int category) => Of(0x028).FindAll(p => (int)U(p, 82, 4) == category);
        BasicPacket TickUntil(int category, int maxTicks = 12)
        {
            for (int i = 0; i < maxTicks; i++)
            {
                Tick(100);
                if (Actions(category) is { Count: > 0 } l) return l[0];
            }
            return null;
        }
        void TickUntilIdle()
        {
            for (int i = 0; rabbit.SkillState != null && i < 100; i++) Tick(100);
        }
        Pos(-1, 0);
        c.Hp = c.MaxHp;
        Lua($@"mob:updateEnmity(GetEntityByID({CharId}))
               mob:setTP(900)
               mob:addListener('WEAPONSKILL_BEFORE_USE', 'VERIFY_WS_BEFORE', function(m, id) m:setLocalVar('wsBefore', id) end)
               mob:addListener('WEAPONSKILL_STATE_ENTER', 'VERIFY_WS_ENTER', function(m, id) m:setLocalVar('wsEnter', id) end)
               mob:addListener('WEAPONSKILL_STATE_EXIT', 'VERIFY_WS_EXIT', function(m, id, done) m:setLocalVar('wsExit', id * 10 + (done and 1 or 0)) end)
               mob:addListener('WEAPONSKILL_USE', 'VERIFY_WS_USE', function(m, t, skill, tp, action, dmg)
                   m:setLocalVar('wsUse', skill:getID()) m:setLocalVar('wsUseTp', tp) m:setLocalVar('wsUseTarget', t:getID())
                   m:setLocalVar('wsUseSkillTp', skill:getTP()) m:setLocalVar('wsTotal', skill:getTotalTargets())
                   m:setLocalVar('wsPrimary', action:getPrimaryTargetID()) m:setLocalVar('wsCategory', action:getCategory())
                   m:setLocalVar('wsDmg', dmg + 1) end)");
        Check("the rabbit's skill list: 206 (foot kick, dust cloud, whirl claws)", 206, rabbit.GetMobMod(MobMod.SkillList));
        Lua($"mob:useMobAbility(257, GetEntityByID({CharId}))");
        Check("useMobAbility: queued, nothing yet", "0/0", $"{Of(0x028).Count}/{Var("wsBefore")}");
        Tick(1);
        var readies = Actions(7);
        Check("the ready: actor / category 7 / SkillUse / 1 target / the char", $"{Rabbit}/7/{(uint)FourCC.SkillUse:X}/1/{CharId}",
              readies.Count == 1 ? Battle2Head(readies[0]) : $"{readies.Count}");
        Check("its result: the skill id, 'readies' (43)", "257/43",
              readies.Count == 1 ? $"{Targets(readies[0])[0].Param}/{Targets(readies[0])[0].Msg}" : "");
        Check("BEFORE_USE and STATE_ENTER with the id; the TP spent", "257/257/0/True", $"{Var("wsBefore")}/{Var("wsEnter")}/{rabbit.Tp}/{rabbit.SkillState != null}");
        int skillHpBefore = c.Hp;
        var finish = TickUntil(11);
        Check("the finish: actor / category 11 / the skill id / 1 target / the char", $"{Rabbit}/11/101/1/{CharId}", finish == null ? "none" : Battle2Head(finish));
        var hit = finish == null ? default : Targets(finish)[0];
        Check("its animation 1; a hit (185, the HP gone) or a miss (188)", true,
              finish != null && hit.Anim == 1 && ((hit.Msg == 185 && hit.Param == skillHpBefore - c.Hp && hit.Param > 0 && hit.Kind == 3) || (hit.Msg == 188 && hit.Res == 1)));
        Check("WEAPONSKILL_USE: the skill, spent TP, the target, the totals, the action", $"257/900/{CharId}/900/1/{CharId}/11/{hit.Param + 1}",
              $"{Var("wsUse")}/{Var("wsUseTp")}/{Var("wsUseTarget")}/{Var("wsUseSkillTp")}/{Var("wsTotal")}/{Var("wsPrimary")}/{Var("wsCategory")}/{Var("wsDmg")}");
        Check("no swing while the animation plays out", 0, Actions(1).Count);
        TickUntilIdle();
        Check("done after the animation time: STATE_EXIT completed", "2571/False", $"{Var("wsExit")}/{rabbit.SkillState != null}");
        Check("m_UsedSkillIds: Foot Kick at the rabbit's level", $"True/{rabbit.MLvl}",
              $"{rabbit.UsedSkillIds.ContainsKey(257)}/{(rabbit.UsedSkillIds.TryGetValue(257, out var usedAt) ? usedAt : 0)}");

        Console.WriteLine("mob skills: AoE, conal, self, out of range, refused, the TP move, interrupted");
        c.Hp = c.MaxHp;
        Lua("mob:useMobAbility(259)");
        Tick(1);
        readies = Actions(7);
        Check("whirl claws (self-centered): readies on the battle target", $"{Rabbit}/7/{(uint)FourCC.SkillUse:X}/1/{CharId}", readies.Count == 1 ? Battle2Head(readies[0]) : $"{readies.Count}");
        finish = TickUntil(11);
        Check("lands on the char in the radius, not on the rabbit; primary the char (targets[0])", $"{CharId}/1/{CharId}",
              finish == null ? "none" : $"{string.Join(",", Targets(finish).ConvertAll(t => t.Id))}/{Var("wsTotal")}/{Var("wsPrimary")}");
        TickUntilIdle();
        c.Hp = c.MaxHp;
        Lua($"mob:useMobAbility(258, GetEntityByID({CharId}))");
        Tick(1);
        finish = TickUntil(11);
        Check("dust cloud: the char in the cone in front", $"{CharId}", finish == null ? "none" : string.Join(",", Targets(finish).ConvertAll(t => t.Id)));
        TickUntilIdle();
        Lua($"mob:setBehavior(0x400) mob:useMobAbility(258, GetEntityByID({CharId}))");
        Tick(1);
        byte turned = (byte)(rabbit.Rotation + 128);
        rabbit.Rotation = turned;
        finish = TickUntil(11);
        Check("turned away (NO_TURN): it stays turned, and the cone still takes its main target", $"{turned}/{CharId}",
              finish == null ? "none" : $"{rabbit.Rotation}/{string.Join(",", Targets(finish).ConvertAll(t => t.Id))}");
        TickUntilIdle();
        c.Hp = c.MaxHp;
        Lua($"mob:setLocalVar('wsExit', 0) mob:useMobAbility(258, GetEntityByID({CharId}))");
        Tick(1);
        c.Y = rabbit.Y + 9; // in range (10), past the 8.5 vertical reach
        finish = TickUntil(4);
        Check("the target above the vertical reach: no one, 76 with animation 508", $"{Rabbit}/4/0/1/{Rabbit}/508/76",
              finish == null ? "none" : $"{Battle2Head(finish)}/{Targets(finish)[0].Anim}/{Targets(finish)[0].Msg}");
        TickUntilIdle();
        Check("no targets: STATE_EXIT still completed", 2581u, Var("wsExit"));
        c.Y = rabbit.Y;
        Lua("mob:setBehavior(0)");

        Console.WriteLine("useJobAbility: a mob's job ability (CAbilityState), queued, its target, the recast");
        s.PacketList.Clear();
        Lua("mob:addListener('ABILITY_USE', 'VERIFY_JA_USE', function(m, t, ability) m:setLocalVar('jaUse', ability:getID()) end)");
        Lua("mob:useJobAbility(xi.ja.BERSERK, mob)");
        Check("queued: nothing yet", "0/0", $"{Of(0x028).Count}/{rabbit.AbilityInUse}");
        Tick(1);
        Check("the state on: berserk in use", 31, (int)rabbit.AbilityInUse);
        var ja = TickUntil(6);
        Check("the finish: actor / category 6 / berserk / 1 target / itself", $"{Rabbit}/6/1F/1/{Rabbit}", ja == null ? "none" : Battle2Head(ja));
        Check("the effect (56) on the rabbit, ABILITY_USE", "True/31", $"{rabbit.StatusEffects.HasStatusEffect(56)}/{Var("jaUse")}");
        Tick(500);
        Check("still in the state for the 2 s animation", 31, (int)rabbit.AbilityInUse);
        Tick(1600);
        Check("done after the animation", 0, (int)rabbit.AbilityInUse);
        s.PacketList.Clear();
        Lua("mob:delStatusEffect(xi.effect.BERSERK) mob:useJobAbility(xi.ja.BERSERK, mob)");
        Tick(1);
        Tick(100);
        Check("again at once: on its recast, nothing", "0/False/0", $"{Actions(6).Count}/{rabbit.StatusEffects.HasStatusEffect(56)}/{rabbit.AbilityInUse}");
        Lua("mob:resetRecasts() mob:useJobAbility(xi.ja.BERSERK)");
        Tick(1);
        Tick(100);
        Check("no target: the battle target (the char), not a self ability's, refused", "0/0", $"{Actions(6).Count}/{rabbit.AbilityInUse}");

        Console.WriteLine("setBattleID: entities of another battle id do not interact");
        Check("getBattleID: 0, then what setBattleID set", "0/2", Lua("local was = mob:getBattleID() mob:setBattleID(2) report(was .. '/' .. mob:getBattleID())"));
        Lua("mob:setBattleID(0)");
        c.Hp = c.MaxHp;
        s.PacketList.Clear();
        Lua("mob:useMobAbility(259)");
        Tick(1);
        Check("whirl claws readied", 1, Actions(7).Count);
        c.BattleId = 1;
        BasicPacket noOne = null;
        for (int i = 0; i < 12 && noOne == null; i++)
        {
            Tick(100);
            noOne = Actions(11).Count > 0 ? Actions(11)[0] : Actions(4).Count > 0 ? Actions(4)[0] : null;
        }
        Check("whirl claws with the char of another battle id: no one in its AoE (76)", $"{Rabbit}/4/0/1/{Rabbit}/76",
              noOne == null ? "none" : $"{Battle2Head(noOne)}/{Targets(noOne)[0].Msg}");
        TickUntilIdle();
        Tick(500);
        Check("the char off the rabbit's enmity list (TryDeaggro)", "False", $"{rabbit.Enmity.HasId(CharId)}");
        c.BattleId = 0;
        Lua($"mob:updateEnmity(GetEntityByID({CharId}))");
        Tick(500);
        Check("one battle id again: engaged on the char", $"True/{CharId}", $"{rabbit.Engaged}/{rabbit.BattleTarget?.Id}");

        Console.WriteLine("trySkillUp: the forced roll, a level gained, the save, the blue flag, a mob");
        ushort evaReal = c.RealSkills[Skill.Evasion], evaWork = c.WorkingSkills[Skill.Evasion];
        c.RealSkills[Skill.Evasion] = 99;
        c.WorkingSkills[Skill.Evasion] = 9;
        s.PacketList.Clear();
        Lua($"GetEntityByID({CharId}):trySkillUp(xi.skill.EVASION, {c.MLvl}, true)");
        var gains = Of(0x029).ConvertAll(Msg);
        Check("a gain (38) and, past 10.0, the level up (53) with the skills menu", $"{CharId}>{CharId}:38,{CharId}>{CharId}:53/1/True",
              $"{string.Join(",", gains)}/{Of(0x062).Count}/{c.RealSkills[Skill.Evasion] > 99 && c.WorkingSkills[Skill.Evasion] == 10}");
        var evaRow = Sql(db, "SELECT value FROM char_skills WHERE charid = ? AND skillid = ?", BoundValue.Of(CharId), BoundValue.Of(Skill.Evasion));
        Check("char_skills saved", $"{c.RealSkills[Skill.Evasion]}", evaRow != null && evaRow.Next() ? evaRow.Get<string>(0u) : "");
        c.WorkingSkills[Skill.Evasion] |= 0x8000;
        s.PacketList.Clear();
        ushort capped = c.RealSkills[Skill.Evasion];
        Lua($"GetEntityByID({CharId}):trySkillUp(xi.skill.EVASION, {c.MLvl}, true)");
        Check("blue (capped): no roll", $"0/{capped}", $"{Of(0x029).Count}/{c.RealSkills[Skill.Evasion]}");
        Lua("mob:trySkillUp(xi.skill.EVASION, 1, true)");
        Check("on a mob: nothing", 0, Of(0x029).Count);
        c.RealSkills[Skill.Evasion] = evaReal;
        c.WorkingSkills[Skill.Evasion] = evaWork;
        Sql(db, "UPDATE char_skills SET value = ? WHERE charid = ? AND skillid = ?", BoundValue.Of(evaReal), BoundValue.Of(CharId), BoundValue.Of(Skill.Evasion));

        Console.WriteLine("mob skills and the mob's effects: amnesia, sekkanoki, stunned at the finish");
        c.Hp = c.MaxHp;
        Lua($"mob:addStatusEffect(xi.effect.AMNESIA, {{ origin = mob, power = 1, duration = 60 }}) mob:setTP(1000) mob:useMobAbility(257, GetEntityByID({CharId}))");
        Tick(1);
        Check("amnesia: refused silently, the TP kept", "0/False/1000", $"{Actions(7).Count}/{rabbit.SkillState != null}/{rabbit.Tp}");
        Lua($"mob:delStatusEffect(xi.effect.AMNESIA) mob:addStatusEffect(xi.effect.SEKKANOKI, {{ origin = mob, power = 1, duration = 60 }}) mob:setTP(2500) mob:useMobAbility(257, GetEntityByID({CharId}))");
        Tick(1);
        Check("sekkanoki: 1000 spent of 2500, then gone", "1/1500/False", $"{Actions(7).Count}/{rabbit.Tp}/{rabbit.StatusEffects.HasStatusEffect(408)}");
        TickUntilIdle();
        c.Hp = c.MaxHp;
        Lua($"mob:setLocalVar('wsExit', 0) mob:setTP(3000) mob:useMobAbility(257, GetEntityByID({CharId}))");
        Tick(1);
        Lua("mob:addStatusEffect(xi.effect.STUN, { origin = mob, power = 1, duration = 10 })");
        List<BasicPacket> stunInterrupt = new();
        for (int i = 0; i < 15 && stunInterrupt.Count == 0; i++)
        {
            Tick(100);
            stunInterrupt = Actions(7).FindAll(p => U(p, 86, 32) == (uint)FourCC.SkillInterrupt);
        }
        Check("stunned at the finish: no finish, the interrupt, a third of 3000 back, STATE_EXIT not completed", $"1/0/1000/2570/False",
              $"{stunInterrupt.Count}/{Actions(11).Count}/{rabbit.Tp}/{Var("wsExit")}/{rabbit.SkillState != null}");
        Lua("mob:delStatusEffect(xi.effect.STUN) mob:setTP(0)");
        // the stun left it inactive (CAIContainer::Tick's prevent-action check); it wakes on its next AI tick
        Tick(400);
        Check("awake again", false, rabbit.InactiveState != null);

        rabbit.Hp = rabbit.MaxHp / 2;
        Lua("mob:useMobAbility(323)");
        Tick(1);
        readies = Actions(7);
        Check("wild carrot (self): readies on the rabbit", $"{Rabbit}/7/{(uint)FourCC.SkillUse:X}/1/{Rabbit}", readies.Count == 1 ? Battle2Head(readies[0]) : $"{readies.Count}");
        Tick(2500);
        Check("its 3 s ready: not yet", 0, Actions(11).Count + Actions(3).Count);
        int mobHpBefore = rabbit.Hp;
        finish = TickUntil(11);
        var heal = finish == null ? default : Targets(finish)[0];
        Check("then heals the rabbit: the amount it got back", $"{Rabbit}/True/{rabbit.Hp - mobHpBefore}",
              finish == null ? "none" : $"{heal.Id}/{heal.Param > 0}/{heal.Param}");
        TickUntilIdle();

        c.Hp = c.MaxHp;
        Lua($"mob:useMobAbility(257, GetEntityByID({CharId}), 2000)");
        Tick(1);
        Check("a cast time override: readies", 1, Actions(7).Count);
        Pos(-15, 0);
        finish = TickUntil(4, 25);
        Check("the char left the range during the ready: 78 with animation 508", $"{Rabbit}/4/0/1/{CharId}/508/78",
              finish == null ? "none" : $"{Battle2Head(finish)}/{Targets(finish)[0].Anim}/{Targets(finish)[0].Msg}");
        TickUntilIdle();
        Lua($"mob:useMobAbility(257, GetEntityByID({CharId}))");
        Tick(1);
        Check("useMobAbility out of range: nothing", "0/False", $"{Actions(7).Count}/{rabbit.SkillState != null}");
        Lua($"mob:useMobAbility(257, GetEntityByID({CharId}), nil, true)");
        Tick(1);
        Check("ignoreDistance: readies anyway", 1, Actions(7).Count);
        Pos(-1, 0);
        TickUntilIdle();

        c.Hp = c.MaxHp;
        Lua(@"saved = {}
              for _, n in ipairs({ 'foot_kick', 'dust_cloud', 'whirl_claws' }) do
                  saved[n] = xi.actions.mobskills[n].onMobSkillCheck
                  xi.actions.mobskills[n].onMobSkillCheck = function() return 1 end
              end
              mob:setTP(3000)");
        for (int i = 0; i < 3; i++) Tick(400);
        Check("3000 TP, every skill's onMobSkillCheck refusing: no move, the TP kept", "0/3000", $"{Actions(7).Count}/{rabbit.Tp}");
        Lua(@"for n, f in pairs(saved) do xi.actions.mobskills[n].onMobSkillCheck = f end
              xi.zones.West_Ronfaure.mobs.Wild_Rabbit.onMobMobskillChoose = function(m, t, chosen) return 259 end");
        readies = null;
        for (int i = 0; i < 3 && readies == null; i++)
        {
            Tick(400);
            if (Actions(7).Count > 0) readies = Actions(7);
        }
        Check("the TP move: onMobMobskillChoose's pick readies; the TP spent", "259/0",
              readies == null ? "none" : $"{Targets(readies[0])[0].Param}/{rabbit.Tp}");
        Check("a new threshold rolled", true, rabbit.TpThreshold >= 1000 && rabbit.TpThreshold <= 3000);
        Lua("xi.zones.West_Ronfaure.mobs.Wild_Rabbit.onMobMobskillChoose = nil");
        TickUntilIdle();
        Lua("mob:setTP(3000)");
        readies = null;
        for (int i = 0; i < 3 && readies == null; i++)
        {
            Tick(400);
            if (Actions(7).Count > 0) readies = Actions(7);
        }
        Check("the TP move from the list", true, readies != null && Targets(readies[0])[0].Param is 257 or 258 or 259);
        TickUntilIdle();

        Lua("mob:setTP(0) mob:useMobAbility()");
        Tick(1);
        Check("useMobAbility() with no TP: the list still picks", 1, Actions(7).Count);
        TickUntilIdle();

        Lua("mob:useMobAbility(323)");
        Tick(1);
        s.PacketList.Clear();
        Lua($"DespawnMob({Rabbit})");
        var interrupt = Actions(7).FindAll(p => U(p, 86, 32) == (uint)FourCC.SkillInterrupt);
        Check("despawned mid-ready: the interrupt packet, STATE_EXIT not completed", $"1/{Rabbit}/7/{(uint)FourCC.SkillInterrupt:X}/1/{Rabbit}/3230/False",
              $"{interrupt.Count}/{(interrupt.Count == 1 ? Battle2Head(interrupt[0]) : "")}/{Var("wsExit")}/{rabbit.SkillState != null}");
        Tick(3100);
        Console.WriteLine("Assist: lock on to what the mob or char at that index fights");
        Action(ActionId.Assist, RabbitTargId);
        var assisted = Of(0x058);
        Check("assisting the rabbit: its foe, the char", $"1/{CharId}", $"{assisted.Count}/{(assisted.Count == 1 ? BitConverter.ToUInt32(assisted[0].Buffer, 8) : 0)}");
        Action(ActionId.Assist, c.TargId);
        Check("assisting itself, not engaged: nothing", 0, Of(0x058).Count);
        Action(ActionId.Assist, 0x3FF);
        Check("nobody there: nothing", 0, Of(0x058).Count);

        Lua($"SpawnMob({Rabbit})");
        Check("respawned: no skill state", "True/False", $"{rabbit.IsSpawned}/{rabbit.SkillState != null}");

        Console.WriteLine("blue magic: a BLU learns Foot Kick from the kill (blueutils::TryLearningSpells)");
        const int FootKickSpell = 577;
        byte jobWas = c.MJob;
        c.MJob = 16;
        c.SetModifier(Mod.BLUE_LEARN_CHANCE, 100);
        c.SpellList[FootKickSpell / 8] &= unchecked((byte)~(1 << FootKickSpell % 8));
        // another mob dies, so the rabbit stays up for what follows
        var learnFrom = info.Mobs.Find(m => m.Id != Rabbit && m.IsSpawned && m.IsAlive && m.CallForHelp == false);
        learnFrom.UsedSkillIds[257] = learnFrom.MLvl;
        learnFrom.GiveExp = false;
        learnFrom.SetMobMod(MobMod.NoDrops, 1);
        learnFrom.OwnerId = CharId;
        Pos(-1, 0);
        s.PacketList.Clear();
        Lua($"local victim = GetMobByID({learnFrom.Id}) victim:takeDamage(victim:getHP(), GetEntityByID({CharId}))");
        Tick(1);    // the death
        var learnPackets = new List<BasicPacket>();
        Tick(1000); // the defeat message and the rewards
        learnPackets.AddRange(Of(0x029));
        learnPackets.AddRange(Of(0x0AA));
        var learnRow = Sql(db, "SELECT spellid FROM char_spells WHERE charid = ? AND spellid = ?", BoundValue.Of(CharId), BoundValue.Of((ushort)FootKickSpell));
        Check("learned: the spell bit, 'learns' (419) with Foot Kick, the MAGIC_DATA, the char_spells row", "True/True/True/True",
              $"{(c.SpellList[FootKickSpell / 8] & 1 << FootKickSpell % 8) != 0}/" +
              $"{learnPackets.Exists(p => Opcode(p) == 0x029 && BitConverter.ToUInt16(p.Buffer, 24) == 419 && BitConverter.ToUInt32(p.Buffer, 12) == FootKickSpell)}/" +
              $"{learnPackets.Exists(p => Opcode(p) == 0x0AA)}/{learnRow != null && learnRow.Next()}");
        Check("the used skills cleared at the kill", 0, learnFrom.UsedSkillIds.Count);
        c.MJob = jobWas;
        c.SetModifier(Mod.BLUE_LEARN_CHANCE, 0);
        Sql(db, "DELETE FROM char_spells WHERE charid = ?", BoundValue.Of(CharId));
        Check("no script errors along the way", "", string.Join("\n", log.FindAll(l => l.Contains("lua:") || l.Contains("luautils::") || l.Contains("rror"))));
        c.Hp = c.MaxHp;

        // changeJob on a mob: the job, a fresh weapon of its skill and delay, the stats recalculated
        {
            byte job = rabbit.MJob, skill = rabbit.WeaponSkill;
            ushort delay = rabbit.MainDelay, baseDelay = rabbit.BaseDelay;
            Lua("mob:changeJob(xi.job.DRK)");
            Check("mob:changeJob(DRK): the job, a scythe at 480, full HP again", $"8/7/480/480/{rabbit.MaxHp}",
                  $"{rabbit.MJob}/{rabbit.WeaponSkill}/{rabbit.MainDelay}/{rabbit.BaseDelay}/{rabbit.Hp}");
            Lua("mob:changeJob(xi.job.THF)");
            Check("mob:changeJob(THF): a dagger at 240", "6/2/240", $"{rabbit.MJob}/{rabbit.WeaponSkill}/{rabbit.MainDelay}");
            Lua($"mob:changeJob({job})");
            (rabbit.WeaponSkill, rabbit.MainDelay, rabbit.BaseDelay) = (skill, delay, baseDelay);
        }
        // facePlayer on a mob: turned towards the char for that client only, unless nonGlobal is false
        {
            byte rot = rabbit.Rotation;
            byte toward = (byte)(((short)(MathF.Atan2(c.Z - rabbit.Z, c.X - rabbit.X) * -(128.0f / MathF.PI)) % 256 + 256) % 256); // worldAngle
            s.PacketList.Clear();
            Lua($"mob:facePlayer(GetPlayerByID({CharId}))");
            var faced = Of(0x00E).Find(p => BitConverter.ToUInt32(p.Buffer, 4) == Rabbit);
            Check("facePlayer: an update to the char facing it, the mob's own rotation kept", $"True/{toward}/{rot}",
                  $"{faced != null}/{(faced == null ? -1 : faced.Buffer[0x0B])}/{rabbit.Rotation}");
            Lua($"mob:facePlayer(GetPlayerByID({CharId}), false)");
            Check("facePlayer(player, false): the mob turned for all", toward, rabbit.Rotation);
            rabbit.Rotation = rot;
        }

        Console.WriteLine("auto-target: a lost target hands over to a fighting mob in front (CAttackState::UpdateTarget)");
        {
            var other = info.Mobs.Find(m => m.Id != Rabbit && m.IsSpawned && m.IsAlive);
            rabbit.MaxHp = rabbit.Hp = 5000;
            rabbit.OwnerId = 0;
            rabbit.Enmity.Clear();
            Pos(-1, 0);
            if (now < c.EngageLockedUntilMs) Tick(c.EngageLockedUntilMs - now);
            Action(ActionId.Attack, RabbitTargId);
            Tick(10);
            Check("engaged on the rabbit", true, c.AttackTarget == rabbit);
            // another mob, fighting, 2 yalms in front of the char
            other.PathFind?.Clear();
            other.RoamRegion = null;
            other.OwnerId = 0;
            (other.X, other.Y, other.Z) = (c.X + 2, c.Y, c.Z + 0.3f);
            other.Animation = Anim.Attack;
            Pos(-1, 0); // the char's spawn list takes the other mob in
            Lua($"DespawnMob({Rabbit})");
            Tick(3500); // the fade-out
            var assists = Of(0x058).ConvertAll(p => BitConverter.ToUInt32(p.Buffer, 8));
            Check("the rabbit gone: on to the other mob, the client told (ASSIST)", "True/True/True",
                $"{c.BattleTarget == other}/{c.AttackTarget == other}/{assists.Contains(other.Id)}");

            // /autotarget off: the next lost target ends the fight
            Lua($"SpawnMob({Rabbit})");
            rabbit.OwnerId = 0;
            (rabbit.X, rabbit.Y, rabbit.Z) = (c.X + 1, c.Y, c.Z);
            rabbit.Animation = Anim.Attack;
            Pos(-1, 0);
            c.Settings |= 1u << 14;
            Lua($"DespawnMob({other.Id})");
            Tick(3500);
            Check("/autotarget off: the other mob gone, the fight over", "True/True", $"{c.AttackTarget == null}/{c.BattleTarget == null}");
            c.Settings &= ~(1u << 14);
            rabbit.Animation = Anim.None;
            Lua($"SpawnMob({other.Id})");
            Tick(10);
        }

        Console.WriteLine("dies again: weakness counted, no raise, home at the hour");
        c.Hp = 0;
        Tick(400);
        diedAt = c.DeathAtMs;
        Check("dead again, weakness off and counted", $"{now}/False/2", $"{diedAt}/{c.StatusEffects.HasStatusEffect(1)}/{c.WeaknessLvl}");
        Check("level 30 loses nothing", expDown + (int)Math.Ceiling(loss * 0.5), c.JobExp[1]);
        Tick(9000);
        Check("no raise menu without a raise", "", Res());

        Console.WriteLine("the death counter, its 16 s sync, and a relogin that takes the death up again");
        Check("the death counter counts down (66 min less the time dead)", (uint)((66 * 60 * 1000L - (now - diedAt)) / 1000), c.DeadRemainingSec);
        Tick(diedAt + 16000 - now);
        var sync = Of(0x037);
        Check("16 s on: the status goes out with the counter", $"1/{60 * c.DeadRemainingSec}",
              $"{sync.Count}/{(sync.Count == 1 ? BitConverter.ToUInt32(sync[0].Buffer, 60) : 0)}");
        Tick(400);
        Check("not again until 16 s more", 0, Of(0x037).Count);
        int expBefore = c.JobExp[1];
        long deadMs = now - diedAt;
        zone.OnCharTimeout(s, false);
        var deathRow = Sql(db, "SELECT death, hp FROM char_stats WHERE charid = ?", BoundValue.Of(CharId));
        Check("the seconds dead and the 0 hp saved on the way out", $"{deadMs / 1000}/0",
              deathRow != null && deathRow.Next() ? $"{deathRow.Get<uint>("death")}/{deathRow.Get<uint>("hp")}" : "");
        // time logged out does not count: the next login takes the death up from the seconds saved
        now += 60_000;
        s = new MapSession { CharId = CharId, AccountId = CharId };
        zone.OnLogin(s);
        c = (CharData)s.Char;
        Send(0x00A, 92, buf => BitConverter.GetBytes(CharId).CopyTo(buf, 12));
        Send(0x00C, 12);
        Check("logged in dead: the death taken up where it was", $"0/{Anim.Death}/{now - deadMs / 1000 * 1000}", $"{c.Hp}/{c.Animation}/{c.DeathAtMs}");
        Tick(400);
        Check("no second defeat: no message, no EXP lost", $"0/{expBefore}",
              $"{Of(0x029).FindAll(p => Msg(p).EndsWith(":97") || Msg(p).EndsWith(":20")).Count}/{c.JobExp[1]}");
        diedAt = c.DeathAtMs;
        Tick(diedAt + 3600_000 - 400 - now);
        Check("still down short of the hour", "0/None", $"{c.Hp}/{c.RequestedWarp}");
        Tick(400);
        Check("an hour on: sent home, hp back, the timer off", $"{c.MaxHp}/0/None", $"{c.Hp}/{c.DeathAtMs}/{c.RequestedWarp}");

        Console.WriteLine("player:die's staged params: Mijin Gakure's death (no EXP lost, no weakness, half the HP back), then a plain one");
        stats.Settings.ExpLossLevel = 1; // level 30 loses EXP here
        string ExpLostVar()
        {
            var r = Sql(db, "SELECT value FROM char_vars WHERE charid = ? AND varname = 'expLost'", BoundValue.Of(CharId));
            return r != null && r.Next() ? r.Get<string>(0u) : "0";
        }
        void AcceptRaise() => Send(0x01A, 28, b =>
        {
            BitConverter.GetBytes(CharId).CopyTo(b, 4);
            BitConverter.GetBytes(c.TargId).CopyTo(b, C2sAction.OffActIndex);
            BitConverter.GetBytes((ushort)ActionId.RaiseMenu).CopyTo(b, C2sAction.OffActionID);
        });
        int expMijin = c.JobExp[1];
        Lua($"local p = GetPlayerByID({CharId}) p:die({{ expLoss = false }}) p:die({{ mijin = true }})");
        Check("die: only the keys given change the staged params; the char at 0 hp", "False/True/0",
              $"{c.NextDeath?.LosesExp}/{c.NextDeath?.Mijin}/{c.Hp}");
        Tick(400);
        Check("the death takes them up: no EXP lost, the death state's mijin, nothing staged", $"{now}/{expMijin}/0/True/False",
              $"{c.DeathAtMs}/{c.JobExp[1]}/{ExpLostVar()}/{c.DeathState.Mijin}/{c.NextDeath.HasValue}");
        Lua($"GetPlayerByID({CharId}):sendRaise(1)");
        s.PacketList.Clear();
        AcceptRaise();
        var mijinRaise = Of(0x028).Find(p => U(p, 40, 32) == CharId);
        Check("raised after Mijin Gakure: Raise's animation, half the HP, no weakness", $"511/{(int)(c.MaxHp * 0.5)}/False",
              $"{(mijinRaise == null ? 0 : U(mijinRaise, 186 + 5, 12))}/{c.Hp}/{c.StatusEffects.HasStatusEffect(1)}");
        Tick(2400);
        c.Hp = c.MaxHp;
        Lua($"GetPlayerByID({CharId}):die()");
        Tick(400);
        Check("a plain die(): EXP lost, no mijin", "True/False", $"{int.Parse(ExpLostVar()) > 0}/{c.DeathState.Mijin}");
        Lua($"GetPlayerByID({CharId}):sendRaise(1)");
        s.PacketList.Clear();
        AcceptRaise();
        Check("raised: a tenth of the HP, weakness", $"{(int)(c.MaxHp * 0.1)}/True", $"{c.Hp}/{c.StatusEffects.HasStatusEffect(1)}");
        Tick(2400);
        c.StatusEffects.DelStatusEffectSilent(1);
        c.Hp = c.MaxHp;
        stats.Settings.ExpLossLevel = 31;

        Console.WriteLine("tractor: the offer, a raise refused meanwhile, declined, then accepted");
        void TractorMenu(uint status) => Send(0x01A, 28, b =>
        {
            BitConverter.GetBytes(CharId).CopyTo(b, 4);
            BitConverter.GetBytes(c.TargId).CopyTo(b, C2sAction.OffActIndex);
            BitConverter.GetBytes((ushort)ActionId.TractorMenu).CopyTo(b, C2sAction.OffActionID);
            BitConverter.GetBytes(status).CopyTo(b, C2sAction.OffTractorMenu);
        });
        c.Hp = 0;
        s.PacketList.Clear();
        Lua($"GetPlayerByID({CharId}):sendTractor(4.5, 0, -6.5, 64)");
        Check("sendTractor: the tractor menu (0x0F9 tractor)", $"{CharId}/2", Res());
        s.PacketList.Clear();
        Check("hasRaiseTractorMenu with it open; a raise refused meanwhile", "true/0/0",
            Lua($"local p = GetPlayerByID({CharId}) p:sendRaise(1) report(tostring(p:hasRaiseTractorMenu()))") + $"/{c.HasRaise}/{Of(0x0F9).Count}");
        float wasX = c.X;
        TractorMenu(1);
        Check("declined: the offer gone, the char where it was", "0/True/false",
            $"{c.HasTractor}/{c.X == wasX}/" + Lua($"report(tostring(GetPlayerByID({CharId}):hasRaiseTractorMenu()))"));
        Lua($"GetPlayerByID({CharId}):sendTractor(4.5, 0, -6.5, 64)");
        TractorMenu(0);
        Check("accepted: the char at the caster's position, the offer gone", "4.5/0/-6.5/64/0", $"{c.X}/{c.Y}/{c.Z}/{c.Rotation}/{c.HasTractor}");
        // the zone change is pending: the char's own position reports are refused until it goes
        Pos(-3, 0);
        Check("a zone change into the same zone pending: the position report refused", "4.5/-6.5", $"{c.X}/{c.Z}");
    }
}
