using System;
using System.Collections.Generic;
using System.IO;
using XI.Common;
using XI.Map;
using XI.Ipc;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.SneakAttack.Live -- live gate for Sneak Attack and Trick Attack on the melee path: GetHitRateEx's sure hit,
// GetCritHitRate's sure crit (from behind, under Hide, on a Doubt target; Trick Attack with Assassin and a partner),
// CAttack::ProcessDamage's DEX / AGI bonus with SNEAK_ATK_DEX / TRICK_ATK_AGI and the AUGMENTS_SA / AUGMENTS_TA
// multipliers, the SATA round (CAttackRound::SetSATA: every swing hits and skips shadows), the effects wearing on the
// first swing, battleutils::getAvailableTrickAttackChar (a party member in line, nearer the mob) and the Trick Attack
// hit's enmity going to that partner. A THF 75 and a WAR partner on a Wild Rabbit in West Ronfaure, with the real
// MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 993200, PartnerId = 993201;
    private const ushort WestRonfaure = 100;
    private const uint Rabbit = 17186822;
    private const ushort RabbitTargId = 6;
    private const ushort BronzeDagger = 16448;
    private const ushort EffectHide = 76, EffectDoubt = 576;
    private const ushort BehaviorNoTurn = 0x400;
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
        foreach (var id in new[] { CharId, PartnerId })
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

    private static int Opcode(BasicPacket p) => p.Buffer[0] | (p.Buffer[1] & 1) << 8;

    private static ulong U(BasicPacket p, int bit, int len) => Battle2Packets.Unpack(p.Buffer, bit, len);

    private record Result(int Res, int Param, int Msg);

    // the first target's results, walking each result's optional additional effect and reaction blocks
    private static List<Result> Results(BasicPacket p)
    {
        var list = new List<Result>();
        int n = (int)U(p, 182, 4);
        int at = 186;
        for (int i = 0; i < n; i++)
        {
            int res = (int)U(p, at, 3), param = (int)U(p, at + 27, 17), msg = (int)U(p, at + 44, 10);
            at += 85;
            if (U(p, at, 1) != 0) at += 1 + 37; else at += 1;
            if (U(p, at, 1) != 0) at += 1 + 34; else at += 1;
            list.Add(new Result(res, param, msg));
        }
        return list;
    }

    private static void AddChar(IDatabase db, uint id, string name, byte job)
    {
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of("sataverify" + id), BoundValue.Of("x"), BoundValue.Of((byte)1));
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
        Sql(db, job == 6 ? "REPLACE INTO char_jobs(charid, thf) VALUES (?, 75)" : "REPLACE INTO char_jobs(charid, war) VALUES (?, 75)", BoundValue.Of(id));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(id));
    }

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        AddChar(db, CharId, "Sneaky", 6);
        AddChar(db, PartnerId, "Tanky", 1);
        Sql(db, "INSERT INTO char_skills(charid, skillid, value, rank) VALUES (?, 2, 2000, 0)", BoundValue.Of(CharId));
        Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?, 0, 1, ?, 1, ?)",
            BoundValue.Of(CharId), BoundValue.Of(BronzeDagger), BoundValue.Of(new byte[24]));

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        zone.VanaTicksNow = () => 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        zone.MobRng = new Random(11);
        // the world: every message comes back to this process, through CBOR
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

        var s = new MapSession { CharId = CharId, AccountId = CharId };
        var sb = new MapSession { CharId = PartnerId, AccountId = PartnerId };
        zone.OnLogin(s);
        zone.OnLogin(sb);
        var c = (CharData)s.Char;
        var b = (CharData)sb.Char;
        void Send(MapSession ss, ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(ss, opcode, new BasicPacket(buf));
            zone.AfterParse(ss);
        }
        foreach (var (ss, id) in new[] { (s, CharId), (sb, PartnerId) })
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
        void Tick(long ms)
        {
            s.PacketList.Clear();
            now += ms;
            zone.Tick();
        }
        List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);

        // the char's next round, the rabbit frozen and turned to the given rotation each tick
        byte rabbitRot = 0;
        List<Result> Round()
        {
            for (int t = 0; t < 40; t++)
            {
                rabbit.SwingAtMs = Never;
                rabbit.Rotation = rabbitRot;
                if (c.SwingAtMs > now + 400) c.SwingAtMs = now + 400;
                Tick(400);
                var round = Of(0x028).Find(p => U(p, 40, 32) == CharId && U(p, 82, 4) == 1);
                if (round != null) return Results(round);
            }
            return new List<Result>();
        }
        void Fresh() => rabbit.BaseMaxHp = rabbit.MaxHp = rabbit.Hp = 50000;
        void Effect(ushort id) => c.StatusEffects.AddStatusEffect(new StatusEffect(id, id, 1, 0, 60_000));

        Console.WriteLine("Setup: a THF 75 with Assassin, a dagger, behind a rabbit that does not turn");
        Send(s, 0x050, 8, p => { p[4] = 1; p[5] = 0; p[6] = 0; });
        Check("dagger in the main hand", BronzeDagger, c.Equipped[0]?.ItemId ?? 0);
        Check("THF 75 has Assassin (trait 68)", true, CharStats.HasTrait(c, 68));
        rabbit.Behavior |= BehaviorNoTurn;
        Pos(s, -2, 0);
        Fresh();
        // the rotations with the char in the rabbit's back cone, then the middle of that run: the rabbit faces away
        var behindRots = new List<int>();
        for (int r = 0; r < 256; r++)
        {
            rabbit.Rotation = (byte)r;
            if (Melee.Behind(c, rabbit)) behindRots.Add(r);
        }
        Check("the back cone is 63 rotations wide (|diff| > 96)", 63, behindRots.Count);
        byte back = 0;
        for (int r = 0; r < 256; r++)
        {
            bool all = true;
            for (int k = -31; k <= 31; k++) all &= behindRots.Contains((r + k + 256) % 256);
            if (all) back = (byte)r;
        }
        byte front = (byte)(back + 128);
        rabbit.Rotation = back;
        Check("behind at the back rotation", true, Melee.Behind(c, rabbit));
        rabbit.Rotation = front;
        Check("not behind facing the char", false, Melee.Behind(c, rabbit));

        Console.WriteLine("The math (Melee): hit rate, crit rate, ProcessDamage's DEX bonus");
        var m = new Melee(stats, new StatSettings(), () => new Random(1));
        rabbit.SetModifier(Mod.EVA, 5000);
        Melee.Swing First() => new Melee.Swing { Type = Melee.AttackType.Normal, Direction = Melee.Direction.Right, First = true };
        rabbit.Rotation = back;
        Check("no SA: the floor hit rate (evasion 5000)", 20, m.HitRate(c, rabbit, 0));
        Check("no SA: crit rate under 100", true, m.CritHitRate(c, rabbit, Melee.SlotMain) < 100);
        int plain = m.ProcessDamage(c, rabbit, First(), 1.0f);
        Effect(EffectId.SneakAttack);
        Check("SA from behind: sure hit", 100, m.HitRate(c, rabbit, 0));
        Check("SA from behind: sure crit", 100, m.CritHitRate(c, rabbit, Melee.SlotMain));
        Check("SA: a later swing's crit ignores it", true, m.CritHitRate(c, rabbit, Melee.SlotMain, true) < 100);
        int dex = CharStats.Dex(c);
        var sw = First();
        Check("SA from behind: + DEX on the first swing", plain + dex, m.ProcessDamage(c, rabbit, sw, 1.0f));
        Check("the swing marked a sneak attack", true, sw.SneakAttack);
        var later = First(); later.First = false;
        Check("no bonus on a later swing", plain, m.ProcessDamage(c, rabbit, later, 1.0f));
        c.SetModifier(Mod.SNEAK_ATK_DEX, 100);
        Check("SNEAK_ATK_DEX 100: + 2 DEX", plain + 2 * dex, m.ProcessDamage(c, rabbit, First(), 1.0f));
        c.SetModifier(Mod.SNEAK_ATK_DEX, 0);
        c.SetModifier(Mod.AUGMENTS_SA, 50);
        Check("AUGMENTS_SA 50: x1.5 after", (int)MathF.Floor((plain + dex) * 1.5f), m.ProcessDamage(c, rabbit, First(), 1.0f));
        c.SetModifier(Mod.AUGMENTS_SA, 0);

        rabbit.Rotation = front;
        Check("SA in front: no sure hit", 20, m.HitRate(c, rabbit, 0));
        Check("SA in front: the flat 5 crit", 5, m.CritHitRate(c, rabbit, Melee.SlotMain));
        Check("SA in front: no bonus", plain, m.ProcessDamage(c, rabbit, First(), 1.0f));
        Effect(EffectHide);
        Check("SA under Hide in front: sure crit", 100, m.CritHitRate(c, rabbit, Melee.SlotMain));
        Check("SA under Hide in front: + DEX", plain + dex, m.ProcessDamage(c, rabbit, First(), 1.0f));
        Check("Hide is no sure hit", 20, m.HitRate(c, rabbit, 0));
        c.StatusEffects.DelStatusEffectSilent(EffectHide);
        rabbit.StatusEffects.AddStatusEffect(new StatusEffect(EffectDoubt, EffectDoubt, 1, 0, 60_000));
        Check("SA on a Doubt target in front: sure crit", 100, m.CritHitRate(c, rabbit, Melee.SlotMain));
        Check("SA on a Doubt target in front: + DEX", plain + dex, m.ProcessDamage(c, rabbit, First(), 1.0f));
        rabbit.StatusEffects.DelStatusEffectSilent(EffectDoubt);
        c.StatusEffects.DelStatusEffectSilent(EffectId.SneakAttack);

        Console.WriteLine("The math (Melee): Trick Attack's partner, AGI bonus");
        Effect(EffectId.TrickAttack);
        Check("TA without a partner: no sure hit", 20, m.HitRate(c, rabbit, 0));
        Check("TA without a partner: crit rate under 100", true, m.CritHitRate(c, rabbit, Melee.SlotMain) < 100);
        m.TrickAttackCharOf = (_, _) => b;
        Check("TA with a partner: sure hit", 100, m.HitRate(c, rabbit, 0));
        Check("TA with a partner: sure crit", 100, m.CritHitRate(c, rabbit, Melee.SlotMain));
        int agi = CharStats.Agi(c);
        var ts = First(); ts.Round = new Melee.Round { TaEntity = b };
        Check("TA: + AGI on the first swing", plain + agi, m.ProcessDamage(c, rabbit, ts, 1.0f));
        Check("the swing marked a trick attack", true, ts.TrickAttack);
        c.SetModifier(Mod.TRICK_ATK_AGI, 50);
        ts = First(); ts.Round = new Melee.Round { TaEntity = b };
        Check("TRICK_ATK_AGI 50: + 1.5 AGI", plain + (int)(agi * 1.5f), m.ProcessDamage(c, rabbit, ts, 1.0f));
        c.SetModifier(Mod.TRICK_ATK_AGI, 0);
        c.SetModifier(Mod.AUGMENTS_TA, 20);
        ts = First(); ts.Round = new Melee.Round { TaEntity = b };
        Check("AUGMENTS_TA 20: x1.2 after", (int)MathF.Floor((plain + agi) * 1.2f), m.ProcessDamage(c, rabbit, ts, 1.0f));
        c.SetModifier(Mod.AUGMENTS_TA, 0);
        c.StatusEffects.DelStatusEffectSilent(EffectId.TrickAttack);
        var rr = new Melee.Swing { Type = Melee.AttackType.Normal, Direction = Melee.Direction.Right, Round = new Melee.Round() };
        Effect(EffectId.SneakAttack);
        rabbit.Rotation = back;
        m.SwingHitRate(c, rabbit, rr);
        Check("a sure right-hand hit marks the round SATA", true, rr.Round.Sata);
        var lr = new Melee.Swing { Type = Melee.AttackType.Normal, Direction = Melee.Direction.Left, Round = new Melee.Round() };
        m.SwingHitRate(c, rabbit, lr);
        Check("a left-hand one does not", false, lr.Round.Sata);
        c.StatusEffects.DelStatusEffectSilent(EffectId.SneakAttack);

        Console.WriteLine("The fight: Sneak Attack from behind, through a shadow");
        Fresh();
        rabbitRot = back;
        s.PacketList.Clear();
        Send(s, 0x01A, 28, p =>
        {
            BitConverter.GetBytes(Rabbit).CopyTo(p, 4);
            BitConverter.GetBytes(RabbitTargId).CopyTo(p, C2sAction.OffActIndex);
            BitConverter.GetBytes((ushort)ActionId.Attack).CopyTo(p, C2sAction.OffActionID);
        });
        Check("engaged on the rabbit", true, c.BattleTarget == rabbit);
        var warm = Round();
        Check("a round without SA at evasion 5000 (some miss)", true, warm.Count > 0);
        Effect(EffectId.SneakAttack);
        rabbit.SetModifier(Mod.UTSUSEMI, 1);
        int hp = rabbit.Hp;
        var sa = Round();
        Check("the first swing a crit hit (res 0, msg 67)", "0/67", sa.Count > 0 ? $"{sa[0].Res}/{sa[0].Msg}" : "none");
        Check("every swing of the SATA round hit", true, sa.Count > 0 && sa.TrueForAll(x => x.Res == 0 && (x.Msg == 1 || x.Msg == 67)));
        Check("the shadow untouched", 1, rabbit.GetMod(Mod.UTSUSEMI));
        int dealt = 0; foreach (var x in sa) dealt += x.Param;
        Check("the HP lost is the damage shown", dealt, hp - rabbit.Hp);
        Check("Sneak Attack wore on the first swing", false, c.StatusEffects.HasStatusEffect(EffectId.SneakAttack));
        rabbit.SetModifier(Mod.UTSUSEMI, 0);

        Console.WriteLine("Sneak Attack in front: no sure hit, a miss possible");
        rabbitRot = front;
        bool missed = false;
        for (int i = 0; i < 6 && !missed; i++)
        {
            Effect(EffectId.SneakAttack);
            var f = Round();
            missed = f.Count > 0 && f[0].Res == 1;
        }
        Check("a front SA's first swing missed at evasion 5000", true, missed);
        c.StatusEffects.DelStatusEffectSilent(EffectId.SneakAttack);

        Console.WriteLine("Trick Attack: a party member in line, nearer the rabbit");
        s.PacketList.Clear();
        Send(s, 0x06E, 12, p =>
        {
            BitConverter.GetBytes(b.Id).CopyTo(p, 4);
            BitConverter.GetBytes(b.TargId).CopyTo(p, 8);
        });
        Send(sb, 0x074, 8, p => p[4] = 1);
        Pump();
        var party = Sql(db, "SELECT COUNT(*) AS n FROM accounts_parties WHERE partyid = ? AND charid IN (?, ?)", BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(PartnerId));
        Check("in one party", 2L, party != null && party.Next() ? party.Get<long>("n") : -1);
        Pos(sb, -1, 0);
        Fresh();
        Effect(EffectId.TrickAttack);
        int ceC = rabbit.Enmity.CE(c), ceB = rabbit.Enmity.CE(b);
        hp = rabbit.Hp;
        var ta = Round();
        Check("TA: the first swing a crit hit", "0/67", ta.Count > 0 ? $"{ta[0].Res}/{ta[0].Msg}" : "none");
        Check("TA: every swing of the round hit", true, ta.Count > 0 && ta.TrueForAll(x => x.Res == 0));
        Check("TA: the partner took the round's enmity", true, rabbit.Enmity.CE(b) > ceB);
        Check("TA: the THF none", ceC, rabbit.Enmity.CE(c));
        Check("Trick Attack wore on the first swing", false, c.StatusEffects.HasStatusEffect(EffectId.TrickAttack));

        Console.WriteLine("Trick Attack: the partner out of line, or farther than the THF");
        rabbit.SetModifier(Mod.EVA, 0);
        c.SetModifier(Mod.ACC, 2000);
        foreach (var (dx, dz, what) in new[] { (-1f, 1.5f, "out of line"), (-3f, 0f, "farther away"), (-0.3f, 0f, "within 0.5 of the rabbit") })
        {
            Pos(sb, dx, dz);
            Effect(EffectId.TrickAttack);
            ceC = rabbit.Enmity.CE(c); ceB = rabbit.Enmity.CE(b);
            var no = Round();
            Check($"{what}: the enmity is the THF's", true, no.Count > 0 && rabbit.Enmity.CE(c) > ceC && rabbit.Enmity.CE(b) == ceB);
        }
        c.StatusEffects.DelStatusEffectSilent(EffectId.TrickAttack);
    }
}
