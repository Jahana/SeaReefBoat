using System;
using System.Collections.Generic;
using System.IO;
using XI.Common;
using XI.Map;
using XI.Ipc;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Ranged.Live -- live gate for a char's ranged attack (ZoneHost.Ranged: CPlayerController::RangedAttack,
// CRangeState, CBattleEntity::OnRangedAttack and the ranged battleutils) with a shortbow and wooden arrows against a
// Wild Rabbit in West Ronfaure and the real MariaDB (xidb); poison arrows for the ammo's additional effect.
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 991300;
    private const ushort WestRonfaure = 100;
    private const uint Rabbit = 17186822;
    private const ushort RabbitTargId = 6;
    private const ushort Shortbow = 17152, WoodenArrow = 17318, PoisonArrow = 18157;
    private const uint RangedStart = 0x676C6163, RangedInterrupt = 0x676C7073, RangedFinish = 0x676C6873;

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
    private static string Battle2Head(BasicPacket p) => $"{U(p, 40, 32)}/{U(p, 82, 4)}/{U(p, 86, 32)}/{U(p, 72, 6)}/{U(p, 150, 32)}";

    // the first target's results: resolution, animation, param and message of each
    private static List<(int Res, int Anim, int Param, int Msg)> Battle2Results(BasicPacket p)
    {
        var list = new List<(int, int, int, int)>();
        int n = (int)U(p, 182, 4);
        for (int i = 0; i < n; i++)
        {
            int at = 186 + i * 87;
            list.Add(((int)U(p, at, 3), (int)U(p, at + 5, 12), (int)U(p, at + 27, 17), (int)U(p, at + 44, 10)));
        }
        return list;
    }

    // the first result's additional effect block: kind / param / message
    private static (int Add, int AddParam, int AddMsg) FirstAddEffect(BasicPacket p)
    {
        int at = 186 + 85;
        if (U(p, at, 1) == 0) return (0, 0, 0);
        return ((int)U(p, at + 1, 6), (int)U(p, at + 11, 17), (int)U(p, at + 28, 10));
    }

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("rangedverify"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        // a Hume with the NEW_ADVENTURER title (not a first login: no charCreate), standing at the zone's entrance (a
        // char at 0,0,0 is moved there by the zone's onZoneIn)
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,titles) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Archy"), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(-126.0f), BoundValue.Of(-62.0f), BoundValue.Of(273.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0), BoundValue.Of(titles));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(CharId));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        // a warrior: no Recycle, Snapshot or Rapid Shot traits, so every shot uses an arrow and aims the bow's 3 s
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 300, 50, 1, 30)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 30)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));
        Sql(db, "INSERT INTO char_skills(charid, skillid, value, rank) VALUES (?, 25, 100, 0)", BoundValue.Of(CharId));
        Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?, 0, 1, ?, 1, ?)",
            BoundValue.Of(CharId), BoundValue.Of(Shortbow), BoundValue.Of(new byte[24]));
        Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?, 0, 2, ?, 2, ?)",
            BoundValue.Of(CharId), BoundValue.Of(WoodenArrow), BoundValue.Of(new byte[24]));
        Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?, 0, 3, ?, 5, ?)",
            BoundValue.Of(CharId), BoundValue.Of(WoodenArrow), BoundValue.Of(new byte[24]));
        Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?, 0, 4, ?, 12, ?)",
            BoundValue.Of(CharId), BoundValue.Of(PoisonArrow), BoundValue.Of(new byte[24]));

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        zone.VanaTicksNow = () => 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        zone.MobRng = new Random(7);
        zone.SendIpc = _ => { };
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);

        var info = zone.ZoneInfoOf(WestRonfaure);
        var rabbit = info.MobById[Rabbit];
        Check("rabbit up", true, rabbit.IsSpawned);
        rabbit.MaxHp = rabbit.Hp = 5000;

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
        void Pos(float dx, byte dir, float dy = 0)
        {
            Send(0x015, 32, b =>
            {
                BitConverter.GetBytes(rabbit.X + dx).CopyTo(b, C2sPos.OffX);
                BitConverter.GetBytes(rabbit.Y + dy).CopyTo(b, C2sPos.OffZ);
                BitConverter.GetBytes(rabbit.Z).CopyTo(b, C2sPos.OffY);
                b[C2sPos.OffDir] = dir;
            });
            s.PacketList.Clear();
        }
        void Shoot(ushort targid)
        {
            s.PacketList.Clear();
            Send(0x01A, 28, b =>
            {
                BitConverter.GetBytes(Rabbit).CopyTo(b, 4);
                BitConverter.GetBytes(targid).CopyTo(b, C2sAction.OffActIndex);
                BitConverter.GetBytes((ushort)ActionId.Shoot).CopyTo(b, C2sAction.OffActionID);
            });
        }
        void Equip(byte invSlot, byte slot)
        {
            s.PacketList.Clear();
            Send(0x050, 8, b => { b[4] = invSlot; b[5] = slot; b[6] = 0; });
        }
        List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);
        // the char's BATTLE2s (the claimed rabbit fights back with its own)
        List<BasicPacket> Mine() => Of(0x028).FindAll(p => U(p, 40, 32) == CharId);
        string Msg(BasicPacket p) => p == null ? "" : $"{BitConverter.ToUInt32(p.Buffer, 4)}>{BitConverter.ToUInt32(p.Buffer, 8)}:{BitConverter.ToUInt16(p.Buffer, 24)}";
        string LastMsg() { var l = Of(0x029); return l.Count == 0 ? "" : Msg(l[^1]); }
        void Tick(long ms)
        {
            s.PacketList.Clear();
            now += ms;
            zone.Tick();
        }
        uint Qty(byte slot) => c.FindItem(Container.Inventory, slot)?.Quantity ?? 0;

        Console.WriteLine("The refusals");
        Pos(-5, 0);
        Shoot(RabbitTargId);
        Check("no ranged weapon: 216", $"{CharId}>{CharId}:216", LastMsg());
        Equip(1, CharStats.SlotRanged);
        Check("the shortbow on", Shortbow, c.Equipped[CharStats.SlotRanged]?.ItemId ?? 0);
        Shoot(RabbitTargId);
        Check("a bow without arrows: 216", $"{CharId}>{CharId}:216", LastMsg());
        Equip(2, CharStats.SlotAmmo);
        Check("the arrows on", WoodenArrow, c.Equipped[CharStats.SlotAmmo]?.ItemId ?? 0);
        Shoot(c.TargId);
        Check("at oneself: 446", $"{CharId}>{CharId}:446", LastMsg());
        Pos(-5, 128);
        Shoot(RabbitTargId);
        Check("facing away: 217", $"{CharId}>{Rabbit}:217", LastMsg());
        Pos(-5, 0, 15);
        Shoot(RabbitTargId);
        Check("15 yalms under the ground: no line of sight, 71", $"{CharId}>{Rabbit}:71", LastMsg());
        // 30 yalms along x at the rabbit's height is inside the hillside, and the line of sight goes first (as in
        // C++): the distance check alone, without the collision mesh
        string meshDir = zone.XiMeshDir;
        zone.XiMeshDir = null;
        Pos(-30, 0);
        Shoot(RabbitTargId);
        Check("30' away: 78", $"{CharId}>{Rabbit}:78", LastMsg());
        zone.XiMeshDir = meshDir;
        Check("no state, no BATTLE2", "False/0", $"{c.InRangedAttack}/{Mine().Count}");
        c.SetModifier(Mod.RACC, 300); // the rabbit is hit

        Console.WriteLine("A shot");
        Pos(-5, 0);
        Shoot(RabbitTargId);
        var start = Mine();
        Check("the start BATTLE2", 1, start.Count);
        if (start.Count == 1)
        {
            Check("actor / ranged start / FourCC / 1 target / self", $"{CharId}/12/{RangedStart}/1/{CharId}", Battle2Head(start[0]));
            Check("one empty result", "1/0/0", $"{Battle2Results(start[0]).Count}/{Battle2Results(start[0])[0].Param}/{Battle2Results(start[0])[0].Msg}");
        }
        Check("in the state", true, c.InRangedAttack);
        Shoot(RabbitTargId);
        Check("a second one meanwhile: 94", $"{CharId}>{CharId}:94", LastMsg());
        Equip(0, CharStats.SlotRanged);
        Check("the bow cannot come off while aiming", Shortbow, c.Equipped[CharStats.SlotRanged]?.ItemId ?? 0);
        Tick(2999);
        Check("no finish before the bow's 360 delay: 3000 ms of aim", 0, Mine().Count);
        Tick(2);
        var finish = Mine();
        Check("the finish BATTLE2", 1, finish.Count);
        int dealt = -1;
        if (finish.Count == 1)
        {
            Check("actor / ranged finish / FourCC / 1 target / rabbit", $"{CharId}/2/{RangedFinish}/1/{Rabbit}", Battle2Head(finish[0]));
            var r = Battle2Results(finish[0]);
            Check("one result", 1, r.Count);
            dealt = r[0].Param;
            Check("a hit: a ranged hit message (352/353/576/577) and damage", true,
                r[0].Res == 0 && r[0].Param > 0 && (r[0].Msg == 352 || r[0].Msg == 353 || r[0].Msg == 576 || r[0].Msg == 577));
        }
        Check("the rabbit's HP down by the damage", 5000 - dealt, rabbit.Hp);
        Check("claimed, on the enmity list", $"{CharId}/True", $"{rabbit.OwnerId}/{rabbit.Enmity.HasId(CharId)}");
        Check("an arrow used: 1 left, still on", $"1/{WoodenArrow}", $"{Qty(2)}/{c.Equipped[CharStats.SlotAmmo]?.ItemId ?? 0}");
        Check("TP from the hit", true, c.Tp > 0);
        Check("still in the state (returning the weapon)", true, c.InRangedAttack);
        Tick(800);
        Check("the state over 800 ms after the shot", true, !c.InRangedAttack);

        Console.WriteLine("The free phase and the last arrow");
        Shoot(RabbitTargId);
        Check("too soon after the last shot: 94", $"{CharId}>{Rabbit}:94", LastMsg());
        Tick(500);
        Shoot(RabbitTargId);
        Check("a shot after the free phase", 1, Mine().Count);
        int hp = rabbit.Hp;
        Tick(3001);
        finish = Mine();
        Check("the finish BATTLE2", 1, finish.Count);
        if (finish.Count == 1) Check("the rabbit's HP down by the damage", hp - Battle2Results(finish[0])[0].Param, rabbit.Hp);
        Check("the last arrow used: gone, the ammo slot empty", "0/True", $"{Qty(2)}/{c.Equipped[CharStats.SlotAmmo] == null}");
        Tick(801);
        Check("the state over", true, !c.InRangedAttack);

        Console.WriteLine("Moving interrupts the aim");
        Equip(3, CharStats.SlotAmmo);
        Check("the other arrows on", $"{WoodenArrow}/5", $"{c.Equipped[CharStats.SlotAmmo]?.ItemId ?? 0}/{Qty(3)}");
        Tick(500);
        Shoot(RabbitTargId);
        Check("aiming", true, c.InRangedAttack);
        Pos(-4, 0);
        hp = rabbit.Hp;
        Tick(3001);
        // (the rabbit's misses can skill up the char's evasion in the same tick)
        Check("you move and interrupt your aim: 218", true, Of(0x029).Exists(p => Msg(p) == $"{CharId}>{CharId}:218"));
        var interrupt = Mine();
        Check("the interrupt BATTLE2", 1, interrupt.Count);
        if (interrupt.Count == 1)
        {
            Check("actor / ranged start / interrupt FourCC / 1 target / self", $"{CharId}/12/{RangedInterrupt}/1/{CharId}", Battle2Head(interrupt[0]));
            Check("animation 508", 508, Battle2Results(interrupt[0])[0].Anim);
        }
        Check("no damage, no arrow used", $"{hp}/5", $"{rabbit.Hp}/{Qty(3)}");
        Check("the state over (no aim time left)", true, !c.InRangedAttack);
        Check("the last shot at the start + 800 ms (no aim time left)", now - 3001 + 800, c.LastRangedAttackMs);

        Console.WriteLine("A shot into the rabbit's shadow (battleutils::IsAbsorbByShadow)");
        Pos(-5, 0);
        Tick(500);
        rabbit.SetModifier(Mod.UTSUSEMI, 1);
        Shoot(RabbitTargId);
        Check("aiming", true, c.InRangedAttack);
        hp = rabbit.Hp;
        Tick(3001);
        finish = Mine();
        Check("the finish BATTLE2", 1, finish.Count);
        if (finish.Count == 1)
        {
            var r = Battle2Results(finish[0]);
            Check("absorbed: miss / 31 / one shadow", "1/31/1", $"{r[0].Res}/{r[0].Msg}/{r[0].Param}");
        }
        Check("no HP lost, the shadow gone, an arrow used", $"{hp}/0/4", $"{rabbit.Hp}/{rabbit.GetMod(Mod.UTSUSEMI)}/{Qty(3)}");
        Tick(801);
        c.LocalVars.TryGetValue("ArrowsUsed", out uint arrowsUsed);
        Check("Scavenge's count: the wooden arrow's id x 10000 + the arrows used", $"{WoodenArrow}/True",
              $"{arrowsUsed / 10000}/{arrowsUsed % 10000 >= 2}");

        Console.WriteLine("The ammo's additional effect (poison arrows: xi.additionalEffect.attack)");
        Equip(4, CharStats.SlotAmmo);
        Check("the poison arrows on", PoisonArrow, c.Equipped[CharStats.SlotAmmo]?.ItemId ?? 0);
        (int Add, int AddParam, int AddMsg) ae = (0, 0, 0);
        int shots = 0;
        for (int i = 0; i < 8 && ae.Add == 0; i++)
        {
            rabbit.Hp = rabbit.MaxHp;
            rabbit.StatusEffects.DelStatusEffectSilent(EffectId.Poison);
            Tick(500);
            Shoot(RabbitTargId);
            Tick(3001);
            var f = Mine();
            if (f.Count == 1 && Battle2Results(f[0])[0].Res == 0)
            {
                shots++;
                ae = FirstAddEffect(f[0]);
            }
            Tick(801);
        }
        Check("a hit's poison proc (10, poisoned)", "10/True", $"{ae.Add}/{rabbit.StatusEffects.HasStatusEffect(EffectId.Poison)}");
        c.LocalVars.TryGetValue("ArrowsUsed", out arrowsUsed);
        Check("Scavenge's count started again for the poison arrows", PoisonArrow, arrowsUsed / 10000);

        Console.WriteLine("Barrage's accuracy, Velocity Shot's attack (GetRangedAccuracyBonuses / GetRangedAttackBonuses), Endark");
        var got = new List<string>();
        scripts.Lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        int Stat(string code)
        {
            got.Clear();
            scripts.Lua.DoString($"local player = GetPlayerByID({CharId})\nlocal rabbit = GetMobByID({Rabbit})\n" + code, "=verify_ranged");
            return int.Parse(got[0]);
        }
        int racc0 = Stat("report(player:getRACC())");
        c.SetModifier(Mod.BARRAGE_ACC, 30);
        Check("BARRAGE_ACC without Barrage: no accuracy", racc0, Stat("report(player:getRACC())"));
        Stat("player:addStatusEffect(xi.effect.BARRAGE, { origin = player, power = 1, duration = 60 }) report(1)");
        Check("under Barrage: RACC +30", racc0 + 30, Stat("report(player:getRACC())"));
        Stat("player:delStatusEffect(xi.effect.BARRAGE) report(1)");
        c.SetModifier(Mod.BARRAGE_ACC, 0);
        int ratt0 = Stat("report(player:getRATT())");
        c.SetModifier(Mod.VELOCITY_RATT_BONUS, 40);
        Check("VELOCITY_RATT_BONUS without Velocity Shot: no attack", ratt0, Stat("report(player:getRATT())"));
        Stat("player:addStatusEffect(xi.effect.VELOCITY_SHOT, { origin = player, power = 1, duration = 60 }) report(1)");
        int rattVel = Stat("report(player:getRATT())");
        c.SetModifier(Mod.VELOCITY_RATT_BONUS, 0);
        int rattVel0 = Stat("report(player:getRATT())");
        Check("under Velocity Shot: the 40 bonus and its 15% RATTP", true, rattVel - rattVel0 is 45 or 46);
        Stat("player:delStatusEffect(xi.effect.VELOCITY_SHOT) report(1)");
        int att0 = Stat("report(player:getStat(xi.mod.ATT))");
        int ratk0 = Stat("report(rabbit:getStat(xi.mod.ATT))");
        Stat("player:addStatusEffect(xi.effect.ENDARK, { origin = player, power = 12, duration = 60 }) rabbit:addStatusEffect(xi.effect.ENDARK, { origin = rabbit, power = 12, duration = 60 }) report(1)");
        Check("Endark's potency on the char's and the mob's ATT", "12/12",
              $"{Stat("report(player:getStat(xi.mod.ATT))") - att0}/{Stat("report(rabbit:getStat(xi.mod.ATT))") - ratk0}");
        Stat("player:delStatusEffect(xi.effect.ENDARK) rabbit:delStatusEffect(xi.effect.ENDARK) report(1)");

        Check("no script errors", "", string.Join("\n", log.FindAll(l => !l.Contains("global scripts") && (l.Contains("lua:") || l.Contains("luautils::") || l.Contains("rror") || l.Contains("did not spend")))));
    }
}
