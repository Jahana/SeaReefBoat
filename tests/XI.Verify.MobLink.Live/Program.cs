using System;
using System.Collections.Generic;
using System.IO;
using XI.Common;
using XI.Map;
using XI.Ipc;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.MobLink.Live -- live gate for mob linking: CZoneEntities::FindPartyForMob's link parties at zone load (by
// family for linking mobs, none for a mob that does not link) and CMobController::TryLink calling an idle party member
// onto the target when CMobEntity::CanLink allows it (link radius, sight detection facing, neutral, NOLINK,
// ONE_WAY_LINKING, a buried worm, the every-other-tick scan). Two Forest Funguars of West Ronfaure, a Fungus Beetle
// and a Wild Rabbit, one WAR 75, with the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 993410;
    private const ushort WestRonfaure = 100;
    private const uint FunguarA = 17186979, FunguarB = 17186980, Beetle = 17187047, Rabbit = 17186822;

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
            BoundValue.Of(CharId), BoundValue.Of("mlnk" + CharId), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Linky"), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(0.0f), BoundValue.Of(0.0f), BoundValue.Of(0.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 1500, 50, 1, 75)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 75)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));

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
        var a = info.MobById[FunguarA];
        var b = info.MobById[FunguarB];
        var beetle = info.MobById[Beetle];
        var rabbit = info.MobById[Rabbit];

        Console.WriteLine("FindPartyForMob: link parties at zone load");
        Check("Forest Funguars link (links: true)", "True/True", $"{a.Link}/{b.Link}");
        Check("the two funguars share a party", true, a.LinkParty != null && ReferenceEquals(a.LinkParty, b.LinkParty));
        Check("the funguar party is all one family", true, a.LinkParty.TrueForAll(x => x.Family == a.Family));
        Check("every linking funguar of the zone is in it", true,
              new List<MobEntity>(info.Mobs).TrueForAll(x => !(x.Link && x.Family == a.Family && x.GetMobMod(MobMod.Sublink) == 0 && x.GetMobMod(MobMod.Superlink) == 0) || ReferenceEquals(x.LinkParty, a.LinkParty)));
        Check("the beetle (another family) has its own party", true, beetle.LinkParty != null && !ReferenceEquals(beetle.LinkParty, a.LinkParty));
        Check("a Wild Rabbit does not link: no party", true, rabbit.LinkParty == null);

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
        Check("a and b spawned", "True/True", $"{a.IsSpawned}/{b.IsSpawned}");

        // a stands 3 east of the char, b `gap` farther east, facing a (west, 128) or away (east, 0)
        void Place(float gap, byte bRotation)
        {
            foreach (var m in new[] { a, b, beetle })
            {
                m.Engaged = false;
                m.BattleTarget = null;
                m.Animation = Anim.None;
                m.Neutral = false;
                m.NeutralAtMs = now - 60000;
                m.Enmity.Clear();
                m.OwnerId = 0;
                m.Hp = m.MaxHp;
                // held still: no roam path, the next idle action (roam or walk home) RoamCool away
                m.PathFind?.Clear();
                m.LastActionMs = now;
            }
            a.X = c.X + 3; a.Y = c.Y; a.Z = c.Z; a.Rotation = 128;
            b.X = a.X + gap; b.Y = a.Y; b.Z = a.Z; b.Rotation = bRotation;
            beetle.X = 500; beetle.Z = 500;
        }
        bool Pull(int ticks = 4)
        {
            a.Enmity.UpdateEnmityFromDamage(c, 10);
            for (int t = 0; t < ticks; t++)
            {
                now += 400;
                zone.Tick();
            }
            return b.Engaged && b.BattleTarget == c;
        }

        Console.WriteLine("TryLink: an idle party member within the link radius joins in");
        Check("the funguar link radius is 10", (short)10, a.GetMobMod(MobMod.LinkRadius));
        b.SetMobMod(MobMod.Detection, 0x002);
        Place(5, 0);
        Check("b links on a's target (hearing, 5 away)", true, Pull());
        Check("a engaged the char", true, a.Engaged && a.BattleTarget == c);
        Check("b holds no enmity of its own yet", false, b.Enmity.HasId(c.Id));

        Place(11, 128);
        Check("11 away (past the link radius): no link", false, Pull());

        b.SetMobMod(MobMod.Detection, 0x001);
        Place(5, 0);
        Check("sight only, facing away: no link", false, Pull());
        Place(5, 128);
        Check("sight only, facing a: link", true, Pull());
        b.SetMobMod(MobMod.Detection, 0x003);
        Place(5, 0);
        Check("sight and hearing, facing away: link", true, Pull());

        Place(5, 128);
        b.NeutralAtMs = now;
        Check("b neutral (15 s after its last fight): no link", false, Pull());

        Place(5, 128);
        b.SetMobMod(MobMod.NoLink, 1);
        Check("b NOLINK: no link", false, Pull());
        b.SetMobMod(MobMod.NoLink, 0);

        Place(5, 128);
        a.SetMobMod(MobMod.OneWayLinking, 1);
        Check("a ONE_WAY_LINKING: it calls no one", false, Pull());
        a.SetMobMod(MobMod.OneWayLinking, 0);

        Place(5, 128);
        b.Animation = Anim.Healing;
        Check("b resting (not roaming): no link", false, Pull());

        Place(5, 128);
        ushort flags = b.RoamFlags;
        b.RoamFlags |= RoamFlag.Worm;
        b.NameVis |= MobEntity.NameVisHideName;
        Check("b a buried worm: no link", false, Pull());
        b.RoamFlags = flags;
        b.NameVis &= unchecked((byte)~MobEntity.NameVisHideName);

        Console.WriteLine("The scan runs every other combat tick");
        Place(5, 128);
        a.LinkScanThisTick = true;
        a.Enmity.UpdateEnmityFromDamage(c, 10);
        int linkedAt = -1;
        for (int t = 0; t < 6 && linkedAt < 0; t++)
        {
            now += 400;
            zone.Tick();
            if (b.Engaged) linkedAt = t;
        }
        // tick 0 engages a (the roam tick), tick 1 is its first combat tick: the scan is skipped, tick 2 runs it
        Check("b joins on the second combat tick", 2, linkedAt);

        Console.WriteLine("Another family near by does not join");
        Place(50, 128);
        beetle.X = a.X + 3; beetle.Z = a.Z; beetle.Rotation = 128;
        Pull();
        Check("the beetle stays out", false, beetle.Engaged);
        Console.WriteLine("A dead member does not join");
        Place(5, 128);
        b.Hp = 0;
        Check("b dead: no link", false, Pull());

        Console.WriteLine("OnMobSpawnCheck: a script's non-zero answer holds the respawn back");
        var lua = scripts.Lua;
        lua.DoString("xi.zones.West_Ronfaure.mobs.Wild_Rabbit = xi.zones.West_Ronfaure.mobs.Wild_Rabbit or {} " +
                     "verifySpawnHold = 1 verifySpawnAsked = 0 " +
                     "xi.zones.West_Ronfaure.mobs.Wild_Rabbit.onMobSpawnCheck = function(mob) verifySpawnAsked = verifySpawnAsked + 1 return verifySpawnHold end",
                     "=verify_spawncheck");
        rabbit.DespawnAtMs = now + 1;
        now += 10;
        zone.Tick();
        now += 3100;
        zone.Tick();
        Check("the rabbit faded out", false, rabbit.IsSpawned);
        for (int t = 0; t < 12; t++)
        {
            now += 10_000;
            zone.Tick();
        }
        lua.DoString("verifyReport = verifySpawnAsked", "=verify_spawncheck");
        lua.GetGlobal("verifyReport");
        int asked = (int)lua.ToNumber(-1);
        lua.Pop(1);
        Check("past its respawn time: asked, held back (1)", "True/False", $"{asked > 0}/{rabbit.IsSpawned}");
        lua.DoString("verifySpawnHold = 0", "=verify_spawncheck");
        for (int t = 0; t < 4 && !rabbit.IsSpawned; t++)
        {
            now += 10_000;
            zone.Tick();
        }
        Check("0: it spawns on the next wave", true, rabbit.IsSpawned);
        lua.DoString("xi.zones.West_Ronfaure.mobs.Wild_Rabbit.onMobSpawnCheck = nil", "=verify_spawncheck");

        Check("no script errors", 0, log.FindAll(x => x.Contains("rror") && !x.Contains("0 load errors")).Count);
        foreach (var x in log.FindAll(x => x.Contains("rror") && !x.Contains("0 load errors"))) Console.WriteLine("  " + x);
    }
}
