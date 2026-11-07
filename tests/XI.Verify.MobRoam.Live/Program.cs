using System;
using System.Collections.Generic;
using System.IO;
using XI.Common;
using XI.Map;
using XI.Ipc;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.MobRoam.Live -- live gate for mob roaming and paths (ZoneHost.MobRoam.cs, PathFind.cs; straight lines, no
// navmesh): CMobController::DoRoamTick's idle action (RoamAround inside the roam region, or within the roam distance
// of the spawn without one; walking home when far; despawning with a 60 s respawn when it cannot walk home; the worm's
// dive and surfacing), the path bindings (pathTo, pathThrough flat and PATROL, isFollowingPath, clearPath, atPoint,
// the player refusals), a scripted path walked in combat in place of the chase, and the chase itself; following
// (ZoneHost.MobFollow.cs: follow / unfollow / hasFollowTarget, a roam follower walking after its moving leader and
// idling by a still one, engage dropping it, a run-away follow ending in RUN_AWAY, a RoamFlag.Follow mob following a
// char instead of aggroing, letting the leader go), the inactive state (ZoneHost.Inactive.cs: stun, sleep, wait,
// untargetableAndUnactionable) and setLink / isInDynamis / getTargID / setMobFlags / getMobFlags / setTrueDetection;
// drawIn and the DrawIn global, resetAI (an engaged mob, and a dead one brought back as the aern mixin does), the fTP
// overrides and canSee; isAlly, getCrystalElement, getBaseSpeed, getAllianceSize, weaknessTrigger and the mob's
// getData (dropped when it spawns again); updateTarget, the call-for-help flags, set / isAggroable, getRoamFlags, getLink,
// getAbilityDistance, hasTPMoves, the stat ranks, the model and hitbox sizes, renameEntity and getPartyMember on a mob;
// getStealItem / getDespoilItem with itemStolen / itemDespoiled, getDespoilDebuff.
// A Wild Rabbit of West Ronfaure (region n_6), a second mob as its leader, and one WAR 75, with the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 993600;
    private const ushort WestRonfaure = 100;
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
            Sql(db, $"DELETE FROM `{t}` WHERE charid BETWEEN 993600 AND 993699");
        Sql(db, "DELETE FROM chars WHERE charid BETWEEN 993600 AND 993699");
        Sql(db, "DELETE FROM accounts WHERE id BETWEEN 993600 AND 993699");
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
        // the Lua snippets carry coordinates as text
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
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

    private static float Flat(float ax, float az, float bx, float bz) => MathF.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("roam" + CharId), BoundValue.Of("x"), BoundValue.Of((byte)1));
        // a Hume with the NEW_ADVENTURER title (not a first login: no charCreate), standing at the zone's entrance (a
        // char at 0,0,0 is moved there by the zone's onZoneIn)
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,titles) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Roamy"), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(-126.0f), BoundValue.Of(-62.0f), BoundValue.Of(273.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0), BoundValue.Of(titles));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(CharId));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 1500, 50, 1, 75)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 75)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
        // this suite's scenes are set at places off the zone's navmesh, so its paths are straight lines
        // (XI.Verify.NavMesh.Live walks the mesh)
        zone.NavMeshDir = null;
        long now = 1_000_000;
        zone.NowMs = () => now;
        zone.VanaTicksNow = () => 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        zone.MobRng = new Random(11);
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
        var n6 = info.Regions["n_6"];

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
        void Pos(float x, float y, float z)
        {
            Send(0x015, 32, b =>
            {
                BitConverter.GetBytes(x).CopyTo(b, C2sPos.OffX);
                BitConverter.GetBytes(y).CopyTo(b, C2sPos.OffZ);
                BitConverter.GetBytes(z).CopyTo(b, C2sPos.OffY);
            });
            s.PacketList.Clear();
        }
        void Tick(int n = 1)
        {
            for (int t = 0; t < n; t++)
            {
                now += 400;
                zone.Tick();
            }
        }
        // no path and the next idle action RoamCool away (Hold), or the idle action due on the next tick (Due)
        void Hold()
        {
            rabbit.PathFind?.Clear();
            rabbit.LastActionMs = now;
        }
        void Due()
        {
            rabbit.PathFind?.Clear();
            rabbit.LastActionMs = now - rabbit.GetMobMod(MobMod.RoamCool) * 1000L - 1000;
        }
        // the first point due east of where the rabbit stands that is `outside` yalms from n_6
        (float X, float Y, float Z) Outside(float outside)
        {
            float d = 0;
            while (n6.DistanceOutside(rabbit.X + d, rabbit.Y, rabbit.Z) < outside && d < 2000) d += 0.5f;
            return (rabbit.X + d, rabbit.Y, rabbit.Z);
        }

        var lua = scripts.Lua;
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local player = GetPlayerByID({CharId})\nlocal mob = GetMobByID({Rabbit})\n" + code, "=verify_mobroam");
            return string.Join(",", got);
        }

        Check("rabbit up, in region n_6", "True/n_6", $"{rabbit.IsSpawned}/{rabbit.RoamRegion?.Name}");
        Check("a region mob's max roam distance is 0", 0.0f, rabbit.MaxRoamDistance);
        Check("roam cool / turns / distance", "35/3/6",
            $"{rabbit.GetMobMod(MobMod.RoamCool)}/{rabbit.GetMobMod(MobMod.RoamTurns)}/{rabbit.GetMobMod(MobMod.RoamDistance)}");
        // the char stands out of the rabbit's way
        Pos(rabbit.X - 40, rabbit.Y, rabbit.Z);
        Pump();

        Console.WriteLine("RoamAround in a region: every step stays inside n_6");
        Due();
        Tick();
        Check("the idle action set a roam path", true, rabbit.PathFind is { IsFollowingPath: true });
        Check("not scripted (RoamAround's own flags)", 0, rabbit.PathFind.Flags & PathFlag.Script);
        bool allInside = true;
        float x0 = rabbit.X, z0 = rabbit.Z, farthest = 0;
        int paths = 0;
        bool wasFollowing = true;
        for (int t = 0; t < 1500; t++)
        {
            Tick();
            allInside &= n6.Contains(rabbit.X, rabbit.Z);
            farthest = Math.Max(farthest, Flat(rabbit.X, rabbit.Z, x0, z0));
            bool following = rabbit.PathFind.IsFollowingPath;
            if (wasFollowing && !following) paths++;
            wasFollowing = following;
        }
        Check("inside n_6 throughout (10 minutes)", true, allInside);
        Check("it walked somewhere", true, farthest > 1.0f);
        Check("several roams, a rest between (RoamCool 35, RoamRate 35)", true, paths >= 10 && paths <= 30);
        Check("walking speed (not run)", (byte)40, rabbit.Speed);

        Console.WriteLine("RoamAround without a region: within the roam distance (6) of the spawn point");
        var region = rabbit.RoamRegion;
        rabbit.RoamRegion = null;
        rabbit.MaxRoamDistance = 50.0f;
        (rabbit.SpawnX, rabbit.SpawnY, rabbit.SpawnZ) = (rabbit.X, rabbit.Y, rabbit.Z);
        Due();
        Tick();
        Check("a roam path", true, rabbit.PathFind.IsFollowingPath);
        farthest = 0;
        for (int t = 0; t < 1500; t++)
        {
            Tick();
            farthest = Math.Max(farthest, Flat(rabbit.X, rabbit.Z, rabbit.SpawnX, rabbit.SpawnZ));
        }
        Check("never past 6 from the spawn", true, farthest <= 6.01f);
        Check("it walked somewhere", true, farthest > 1.0f);
        rabbit.RoamRegion = region;
        rabbit.MaxRoamDistance = 0.0f;
        Hold();
        // back inside n_6 for what follows
        var inside = n6.SamplePoint(zone.MobRng);
        (rabbit.X, rabbit.Y, rabbit.Z) = inside;

        Console.WriteLine("pathTo / pathThrough / isFollowingPath / clearPath / atPoint");
        Hold();
        float tx = 0, tz = 0;
        for (int k = 0; k < 64; k++)
        {
            float ang = k * MathF.PI / 32;
            tx = rabbit.X + 3 * MathF.Cos(ang);
            tz = rabbit.Z + 3 * MathF.Sin(ang);
            if (n6.Contains(tx, tz)) break;
        }
        float ty = rabbit.Y;
        Check("isFollowingPath before", "false", Lua("report(tostring(mob:isFollowingPath()))"));
        Check("pathTo, then following", "true", Lua($"mob:pathTo({tx}, {ty}, {tz})\nreport(tostring(mob:isFollowingPath()))"));
        Check("pathTo defaults to RUN | SCRIPT", PathFlag.Run | PathFlag.Script, rabbit.PathFind.Flags);
        Tick();
        Check("running: faster than the walk", true, rabbit.Speed > 40);
        Tick(10);
        Check("arrived: no longer following", "false", Lua("report(tostring(mob:isFollowingPath()))"));
        Check("atPoint(x, y, z)", "true", Lua($"report(tostring(mob:atPoint({tx}, {ty}, {tz})))"));
        Check("atPoint({x, y, z})", "true", Lua($"report(tostring(mob:atPoint({{ {tx}, {ty}, {tz} }})))"));
        Check("atPoint elsewhere", "false", Lua($"report(tostring(mob:atPoint({tx + 1}, {ty}, {tz})))"));

        float bx = rabbit.X, bz = rabbit.Z;
        Check("pathThrough a flat list", "true/true",
            Lua($"report(tostring(mob:pathThrough({{ {bx + 0.5f}, {ty}, {bz}, {bx + 0.5f}, {ty}, {bz + 0.5f} }})))\nreport(tostring(mob:isFollowingPath()))").Replace(",", "/"));
        Tick(10);
        // a waypoint counts as reached within 0.1 (AtPoint), so the walk may end short of atPoint's 0.01
        Check("walked both points", "false/True",
            Lua("report(tostring(mob:isFollowingPath()))") + "/" + (Flat(rabbit.X, rabbit.Z, bx + 0.5f, bz + 0.5f) <= 0.1f));

        Check("pathThrough PATROL (y carried over, a rotation, a wait)", "true",
            Lua($"report(tostring(mob:pathThrough({{ {{ x = {bx}, z = {bz} }}, {{ x = {bx + 2}, rotation = 64, wait = 1000 }} }}, 0x20)))")); // xi.pathflag.PATROL
        Check("patrolling", "True/True", $"{rabbit.PathFind.IsPatrolling}/{rabbit.PathFind.IsFollowingPath}");
        bool reachedFar = false, cameBack = false;
        for (int t = 0; t < 40; t++)
        {
            Tick();
            if (Flat(rabbit.X, rabbit.Z, bx + 2, bz) <= 0.1f) reachedFar = true;
            // the near point has no wait: it is passed mid-tick, the tick ending at most a step's remainder past it
            if (reachedFar && Flat(rabbit.X, rabbit.Z, bx, bz) <= 0.95f && rabbit.Rotation == 0) cameBack = true;
        }
        Check("the patrol loops between its points", "True/True", $"{reachedFar}/{cameBack}");
        Check("still on the patrol", true, rabbit.PathFind.IsFollowingPath);
        Check("clearPath stops it", "false", Lua("mob:clearPath()\nreport(tostring(mob:isFollowingPath()))"));
        // Clear keeps the patrol: the next roam tick resumes it from its nearest waypoint
        Check("the patrol is kept", true, rabbit.PathFind.IsPatrolling);
        Tick();
        Check("ResumePatrol on the next roam tick", true, rabbit.PathFind.IsFollowingPath);
        // a path through no points without PATROL drops the patrol
        Check("pathThrough({}) ends the patrol", "false/False",
            Lua("mob:pathThrough({})\nreport(tostring(mob:isFollowingPath()))") + "/" + rabbit.PathFind.IsPatrolling);

        log.Clear();
        Lua($"player:pathTo({tx}, {ty}, {tz})");
        Check("pathTo on a player: refused", true, log.Exists(l => l.Contains("Invalid entity (Player: Roamy) calling function.")));
        log.Clear();
        Check("pathThrough on a player: false", "false", Lua($"report(tostring(player:pathThrough({{ {tx}, {ty}, {tz} }})))"));
        Check("logged invalid entity type", true, log.Exists(l => l.Contains("Invalid entity type calling function (")));
        Check("isFollowingPath on a player: false", "false", Lua("report(tostring(player:isFollowingPath()))"));
        Hold();

        Console.WriteLine("the chase: an engaged mob steps straight at its target");
        Pos(rabbit.X - 8, rabbit.Y, rabbit.Z);
        float startGap = Flat(rabbit.X, rabbit.Z, c.X, c.Z);
        rabbit.Enmity.UpdateEnmityFromDamage(c, 10);
        Tick(4);
        Check("engaged the char", true, rabbit.Engaged && rabbit.BattleTarget == c);
        Tick(10);
        float gap = Flat(rabbit.X, rabbit.Z, c.X, c.Z);
        Check("closed the distance", true, gap < startGap - 3);

        Console.WriteLine("Move: a scripted path in combat is walked instead of the chase");
        float sx = rabbit.X, sz = rabbit.Z;
        Lua($"mob:pathTo({sx + 4}, {rabbit.Y}, {sz})");
        Tick();
        Check("walking the script's path, away from the target", true, rabbit.X > sx + 0.5f);
        for (int t = 0; t < 10 && rabbit.PathFind.IsFollowingPath; t++) Tick();
        Check("at the scripted point", true, Flat(rabbit.X, rabbit.Z, sx + 4, sz) <= 0.1f);
        gap = Flat(rabbit.X, rabbit.Z, c.X, c.Z);
        Tick(2);
        Check("then back to the chase", true, Flat(rabbit.X, rabbit.Z, c.X, c.Z) < gap - 1);

        // out of combat again, back in the region
        rabbit.Enmity.Clear();
        rabbit.BattleTarget = null;
        rabbit.OwnerId = 0;
        Tick(3);
        Check("disengaged", false, rabbit.Engaged);
        Check("disengage cleared the path", false, rabbit.PathFind.IsFollowingPath);
        (rabbit.X, rabbit.Y, rabbit.Z) = n6.SamplePoint(zone.MobRng);
        rabbit.NeutralAtMs = now - 60000;
        rabbit.Hp = rabbit.MaxHp;
        Pos(rabbit.X - 40, rabbit.Y, rabbit.Z);
        Hold();

        Console.WriteLine("a worm dives, roams underground and surfaces");
        rabbit.RoamFlags |= RoamFlag.Worm;
        Due();
        Tick();
        Check("dived: animation sub 1, name hidden, untargetable", "1/True/True",
            $"{rabbit.AnimationSub}/{(rabbit.NameVis & MobEntity.NameVisHideName) != 0}/{rabbit.Untargetable}");
        Check("the 3 s wait", true, rabbit.WaitTimeMs > now && rabbit.WaitTimeMs <= now + 3000);
        Check("no path while sinking", false, rabbit.PathFind.IsFollowingPath);
        Tick(8);
        Check("invisible after 3 s", 3, rabbit.Status);
        bool roamedUnder = false, surfaced = false;
        for (int t = 0; t < 400 && !surfaced; t++)
        {
            Tick();
            if (rabbit.PathFind.IsFollowingPath && rabbit.Untargetable) roamedUnder = true;
            if (!rabbit.Untargetable) surfaced = true;
        }
        Check("roamed underground", true, roamedUnder);
        Check("surfaced: targetable, status Update", $"True/{NpcEntity.StatusUpdate}", $"{surfaced}/{rabbit.Status}");
        Check("still under the name for the 2 s rise", 1, rabbit.AnimationSub);
        Tick(6);
        Check("risen: animation sub 0, name shown", "0/False", $"{rabbit.AnimationSub}/{(rabbit.NameVis & MobEntity.NameVisHideName) != 0}");
        rabbit.RoamFlags &= unchecked((ushort)~RoamFlag.Worm);
        Hold();

        Console.WriteLine("far from home with NO_DESPAWN: it walks back, 10 yalms at a time");
        rabbit.SetMobMod(MobMod.NoDespawn, 1);
        (rabbit.X, rabbit.Y, rabbit.Z) = Outside(20);
        float outside0 = n6.DistanceOutside(rabbit.X, rabbit.Y, rabbit.Z);
        Check("20 outside n_6", true, outside0 >= 20);
        float hx = rabbit.X, hz = rabbit.Z;
        Due();
        Tick();
        Check("pathing home", "True/True", $"{rabbit.IsPathingHome}/{rabbit.PathFind.IsFollowingPath}");
        Check("heading for the region", true, n6.DistanceOutside(rabbit.X, rabbit.Y, rabbit.Z) < outside0);
        Tick(20);
        Check("a 10 yalm leg (LimitDistance)", true, Math.Abs(Flat(rabbit.X, rabbit.Z, hx, hz) - 10) < 0.5f);
        Check("leg done", false, rabbit.PathFind.IsFollowingPath);
        bool home = false;
        for (int t = 0; t < 1500 && !home; t++)
        {
            Tick();
            home = n6.DistanceOutside(rabbit.X, rabbit.Y, rabbit.Z) == 0;
        }
        Check("back at the region's edge", true, home);
        Check("still spawned", true, rabbit.IsSpawned && rabbit.FadeAtMs == 0);
        rabbit.SetMobMod(MobMod.NoDespawn, 0);
        Hold();

        Console.WriteLine("far from home in a region: it despawns, respawning 60 s on");
        (rabbit.X, rabbit.Y, rabbit.Z) = Outside(20);
        Due();
        Tick();
        Check("fading", true, rabbit.FadeAtMs > 0 && rabbit.IsSpawned);
        Check("not pathing home", false, rabbit.IsPathingHome);
        Check("respawn in 60 s", 60000L, info.Spawner.RemainingRespawnMs(rabbit, now));
        Tick(10);
        Check("faded out", false, rabbit.IsSpawned);
        int ticks = 0;
        while (!rabbit.IsSpawned && ticks < 400)
        {
            Tick();
            ticks++;
        }
        Check("respawned", true, rabbit.IsSpawned);
        Check("back inside n_6", true, n6.Contains(rabbit.X, rabbit.Z));
        Pos(rabbit.X - 40, rabbit.Y, rabbit.Z);
        Hold();
        rabbit.NeutralAtMs = now - 60000;
        Tick();

        Console.WriteLine("follow: a roam follower walks after its moving leader and idles by a still one");
        MobEntity leader = null;
        foreach (var other in info.Mobs)
            if (other != rabbit && other.IsSpawned && !other.IsDead && other.Hp > 0) { leader = other; break; }
        Check("a second mob to lead", true, leader != null);
        (leader.X, leader.Y, leader.Z) = (rabbit.X + 2, rabbit.Y, rabbit.Z);
        leader.PathFind?.Clear();
        leader.LastActionMs = now;
        Check("hasFollowTarget before", "false", Lua("report(tostring(mob:hasFollowTarget()))"));
        Check("follow(leader, ROAM)", "true",
            Lua($"mob:follow(GetMobByID({leader.Id}), 1)\nreport(tostring(mob:hasFollowTarget()))"));
        Check("its leader and the type", $"{leader.Id}/1", $"{rabbit.FollowTarget?.Id}/{rabbit.FollowType}");
        // the leader walks off; the follower runs after it once it is past the 4 yalm leash
        float lx = leader.X + 12, lz = leader.Z;
        Lua($"GetMobByID({leader.Id}):pathTo({lx}, {leader.Y}, {lz})");
        float rx0 = rabbit.X;
        for (int t = 0; t < 30; t++)
        {
            leader.LastActionMs = now;
            Tick();
        }
        // each tick's PathAround is walked in that same tick, so the path is never seen pending
        Check("ran after the moving leader", true, rabbit.X > rx0 + 8);
        Check("near the leader again (stop range 2 of a moving one)", true, Flat(rabbit.X, rabbit.Z, leader.X, leader.Z) < 6);
        // a still leader: the follower does not path, even with its idle action due
        float fx = rabbit.X, fz = rabbit.Z;
        Due();
        leader.LastActionMs = now;
        Tick(3);
        Check("idle by a still leader: no roam of its own", "False/True",
            $"{rabbit.PathFind.IsFollowingPath}/{Flat(rabbit.X, rabbit.Z, fx, fz) < 0.01f}");
        Check("unfollow", "false", Lua("mob:unfollow()\nreport(tostring(mob:hasFollowTarget()))"));
        Check("unfollow: no neutral (no hooks)", false, rabbit.Neutral);

        Console.WriteLine("follow(nil): letting a roam leader go makes the mob neutral, at full HP drops its claim");
        Lua($"mob:follow(GetMobByID({leader.Id}), 1)");
        rabbit.OwnerId = CharId;
        rabbit.Hp = rabbit.MaxHp;
        Lua("mob:follow(nil, 0)");
        Check("neutral, claim dropped, no follow", "True/0/False", $"{rabbit.Neutral}/{rabbit.OwnerId}/{rabbit.FollowTarget != null}");
        rabbit.NeutralAtMs = now - 60000;

        Console.WriteLine("engage drops a roam follow; a run-away follow ends in RUN_AWAY");
        Lua($"mob:follow(GetMobByID({leader.Id}), 1)");
        Pos(rabbit.X - 3, rabbit.Y, rabbit.Z);
        rabbit.Enmity.UpdateEnmityFromDamage(c, 10);
        Tick(2);
        Check("engaged, follow dropped", "True/False", $"{rabbit.Engaged}/{rabbit.FollowTarget != null}");
        leader.PathFind?.Clear();
        (leader.X, leader.Y, leader.Z) = (rabbit.X + 10, rabbit.Y, rabbit.Z);
        Lua("mob:addListener('RUN_AWAY', 'VERIFY_RUN_AWAY', function(m, t) report('ran to ' .. t:getID()) end)");
        Lua($"mob:follow(GetMobByID({leader.Id}), 2)");
        string ranTo = "";
        for (int t = 0; t < 30 && ranTo == ""; t++)
        {
            leader.LastActionMs = now;
            got.Clear();
            Tick();
            if (got.Count > 0) ranTo = got[0];
        }
        Check("ran to the target, then RUN_AWAY", $"ran to {leader.Id}", ranTo);
        Check("within 4 of it", true, Distance3(rabbit, leader) <= 4.0f);
        Check("the follow is cleared", false, rabbit.FollowTarget != null);
        Lua("mob:removeListener('VERIFY_RUN_AWAY')");

        Console.WriteLine("the inactive state: stun holds the swing, then the attack resumes");
        Pos(rabbit.X - 1.5f, rabbit.Y, rabbit.Z);
        Tick(2);
        Check("attacking", "1", Lua("report(mob:getCurrentAction())"));
        Check("stun(2000)", "27/false", Lua("mob:stun(2000)\nreport(mob:getCurrentAction() .. '/' .. tostring(mob:canChangeState()))"));
        int hp0 = c.Hp;
        float stunX = rabbit.X;
        Tick(4);
        Check("no swings and no chase while stunned", $"{hp0}/True", $"{c.Hp}/{rabbit.X == stunX}");
        Tick(2);
        Check("over after 2 s", "1/true", Lua("report(mob:getCurrentAction() .. '/' .. tostring(mob:canChangeState()))"));
        Check("sleep: inactive while the effect holds", "27",
            Lua("mob:addStatusEffect(xi.effect.SLEEP_I, { power = 1, duration = 60, origin = player })") + Tick1(() => Lua("report(mob:getCurrentAction())")));
        Check("woken: back to attacking", "1", Lua("mob:delStatusEffect(xi.effect.SLEEP_I)") + Tick1(() => Lua("report(mob:getCurrentAction())")));
        Check("untargetableAndUnactionable", "27/True",
            Lua("mob:untargetableAndUnactionable(1000)\nreport(mob:getCurrentAction())") + "/" + rabbit.InactiveState?.Untargetable);
        Tick(4);
        Check("targetable again", true, rabbit.InactiveState == null);
        rabbit.Enmity.Clear();
        rabbit.BattleTarget = null;
        rabbit.OwnerId = 0;
        Tick(3);
        Check("disengaged", false, rabbit.Engaged);
        rabbit.NeutralAtMs = now - 60000;
        Pos(rabbit.X - 40, rabbit.Y, rabbit.Z);

        Console.WriteLine("wait: no path is walked, but the state may still change");
        Hold();
        Lua($"mob:pathTo({rabbit.X + 4}, {rabbit.Y}, {rabbit.Z})");
        Check("wait(1200)", "27/true", Lua("mob:wait(1200)\nreport(mob:getCurrentAction() .. '/' .. tostring(mob:canChangeState()))"));
        float wx = rabbit.X;
        Tick(2);
        Check("not walking while waiting", true, rabbit.X == wx);
        Tick(3);
        Check("walking again after", true, rabbit.X > wx + 0.5f);
        log.Clear();
        Lua("player:wait()");
        Check("wait on a player: refused", true, log.Exists(l => l.Contains("Invalid entity (Player: Roamy) calling function.")));
        Hold();

        Console.WriteLine("RoamFlag.Follow: an aggressive mob follows the char it would aggro");
        rabbit.RoamFlags |= RoamFlag.Follow;
        rabbit.Aggro = true;
        rabbit.SetMobMod(MobMod.AlwaysAggro, 1);
        int detection0 = rabbit.GetMobMod(MobMod.Detection);
        rabbit.SetMobMod(MobMod.Detection, 0x002); // hearing
        Pos(rabbit.X - 3, rabbit.Y, rabbit.Z);
        for (int t = 0; t < 10 && rabbit.FollowTarget == null; t++) Tick();
        Check("following the char, not engaged", "True/False", $"{rabbit.FollowTarget == c}/{rabbit.Engaged}");
        Check("a char leader: no path (it has none to follow)", false, rabbit.PathFind.IsFollowingPath);
        rabbit.RoamFlags &= unchecked((ushort)~RoamFlag.Follow);
        rabbit.Aggro = false;
        rabbit.SetMobMod(MobMod.AlwaysAggro, 0);
        rabbit.SetMobMod(MobMod.Detection, (short)detection0);
        Lua("mob:unfollow()");
        Pos(rabbit.X - 40, rabbit.Y, rabbit.Z);

        Console.WriteLine("setLink / isInDynamis / getTargID / setMobFlags / getMobFlags / setTrueDetection");
        bool link0 = rabbit.Link;
        Lua("mob:setLink(0)");
        Check("setLink(0)", false, rabbit.Link);
        Lua("mob:setLink(1)");
        Check("setLink(1)", true, rabbit.Link);
        rabbit.Link = link0;
        Check("isInDynamis: West Ronfaure", "false/false", Lua("report(tostring(mob:isInDynamis()) .. '/' .. tostring(player:isInDynamis()))"));
        Check("getTargID", $"{rabbit.TargId}/{c.TargId}", Lua("report(mob:getTargID() .. '/' .. player:getTargID())"));
        uint flags0 = rabbit.EntityFlags;
        Check("setMobFlags / getMobFlags", "1155", Lua("mob:setMobFlags(1155)\nreport(mob:getMobFlags())"));
        Lua($"player:setMobFlags({flags0}, {Rabbit})");
        Check("setMobFlags by id from a player", flags0, rabbit.EntityFlags);
        bool td0 = rabbit.TrueDetection;
        Lua("mob:setTrueDetection(true)");
        Check("setTrueDetection", true, rabbit.TrueDetection);
        rabbit.TrueDetection = td0;

        Console.WriteLine("drawIn, resetAI, the fTP overrides, canSee");
        int Count(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => p.GetOpcode() == opcode).Count;
        int DrawnIn() => new List<BasicPacket>(s.PacketList).FindAll(p => p.GetOpcode() == 0x029 && BitConverter.ToUInt16(p.Buffer, 24) == 232).Count;
        Hold();
        Pos(rabbit.X - 20, rabbit.Y, rabbit.Z);
        rabbit.Rotation = 0;
        Lua("mob:drawIn()");
        Check("drawIn() without a battle target: nothing", "0", $"{Count(0x05B)}");
        Lua("mob:drawIn(player, 3, 0)");
        // facing 0: 3 yalms along +x, set down a yalm higher (y - 1)
        Check("drawIn(player, 3): 3 yalms off the mob's facing, a yalm up; WPOS and drawn in", $"{rabbit.X + 3:0.00}/{rabbit.Y - 1:0.00}/{rabbit.Z:0.00}/1/1",
            $"{c.X:0.00}/{c.Y:0.00}/{c.Z:0.00}/{Count(0x05B)}/{DrawnIn()}");
        float px = rabbit.X + 10, py = rabbit.Y, pz = rabbit.Z + 10;
        Lua($"DrawIn(player, {{ x = {px}, y = {py}, z = {pz}, rot = 64 }}, 2, 0)");
        // facing 64 (a quarter turn): 2 yalms toward -z
        Check("the DrawIn global: to the point, offset along its facing", $"{px:0.00}/{pz - 2:0.00}", $"{c.X:0.00}/{c.Z:0.00}");
        Pos(rabbit.X - 8, rabbit.Y, rabbit.Z);
        rabbit.Enmity.UpdateEnmityFromDamage(c, 10);
        Tick(4);
        Check("engaged before the reset", true, rabbit.Engaged);
        Lua("mob:resetAI()");
        Check("resetAI: the attack state gone, no target, no path", "False/False/0/False",
            $"{rabbit.Engaged}/{rabbit.BattleTarget != null}/{rabbit.Animation}/{rabbit.PathFind?.IsFollowingPath ?? false}");
        rabbit.Enmity.Clear();
        rabbit.OwnerId = 0;
        Tick(3);
        Console.WriteLine("resetAI on a dead mob, as the aern reraise does it");
        // at home: the idle action's walk home (or its far-from-home despawn) stays out of it
        rabbit.X = rabbit.SpawnX; rabbit.Y = rabbit.SpawnY; rabbit.Z = rabbit.SpawnZ;
        Lua("mob:setHP(0)");
        Tick();
        Check("dead, despawning", "True/True", $"{rabbit.IsDead}/{rabbit.DespawnAtMs != 0}");
        Lua("mob:setHP(mob:getMaxHP()) mob:resetAI()");
        Tick(45);
        Check("brought back: alive and still up 18 s on", "False/True/True", $"{rabbit.IsDead}/{rabbit.IsSpawned}/{rabbit.Hp == rabbit.MaxHp}");
        rabbit.Animation = 0;
        Lua("mob:setfTPModifierOverride(100, 1.5, 2, 2.5)");
        Check("set / getfTPModifierOverride", "1.5/2/2.5/nil",
            Lua("local t = mob:getfTPModifierOverride(100) report(t[1] .. '/' .. t[2] .. '/' .. t[3] .. '/' .. tostring(mob:getfTPModifierOverride(101)))"));
        Check("canSee: no mesh, so yes", "true", Lua("report(tostring(mob:canSee(player)))"));

        Console.WriteLine("isAlly, getCrystalElement, getBaseSpeed, getAllianceSize, weaknessTrigger, getData on a mob");
        Check("isAlly: a mob of the mobs' allegiance is not", "false", Lua("report(tostring(mob:isAlly()))"));
        byte allegiance0 = rabbit.Allegiance;
        rabbit.Allegiance = 1;
        Check("isAlly: on the players' side", "true", Lua("report(tostring(mob:isAlly()))"));
        rabbit.Allegiance = allegiance0;
        Check("getCrystalElement, getBaseSpeed, getAllianceSize", $"{rabbit.Element}/{rabbit.BaseSpeed}/{rabbit.LinkParty?.Count ?? 1}",
            Lua("report(mob:getCrystalElement() .. '/' .. mob:getBaseSpeed() .. '/' .. mob:getAllianceSize())"));
        Pos(rabbit.X - 5, rabbit.Y, rabbit.Z);
        Lua("mob:weaknessTrigger(2)");
        var trigger = new List<BasicPacket>(s.PacketList).FindAll(p => p.GetOpcode() == 0x028);
        Check("weaknessTrigger: a BATTLE2 to those in range", 1, trigger.Count);
        Lua("mob:getData().life = 1");
        Check("getData on a mob", "1", Lua("report(mob:getData().life)"));
        Lua($"DespawnMob({Rabbit})");
        Tick(10);
        Lua($"SpawnMob({Rabbit})");
        Check("a new life: spawned again, its data gone", "True/nil", $"{rabbit.IsSpawned}/{Lua("report(tostring(mob:getData().life))")}");

        Console.WriteLine("updateTarget, the call-for-help flags, aggroable, roam flags, link, skills, stat ranks, sizes, renaming on a mob");
        rabbit.Enmity.UpdateEnmityFromDamage(c, 10);
        rabbit.BattleTarget = null;
        Lua("mob:updateTarget()");
        Check("updateTarget: the char holding the enmity", true, rabbit.BattleTarget == c);
        rabbit.Enmity.Clear();
        rabbit.BattleTarget = null;
        rabbit.OwnerId = c.Id;
        Lua("mob:setCallForHelpFlag(true)");
        Check("setCallForHelpFlag(true): set, the claim lifted", "true/0", $"{Lua("report(tostring(mob:getCallForHelpFlag()))")}/{rabbit.OwnerId}");
        Lua("mob:setCallForHelpFlag(false) mob:setCallForHelpBlocked(true)");
        Check("cleared; setCallForHelpBlocked", "false/true", Lua("report(tostring(mob:getCallForHelpFlag()) .. '/' .. tostring(mob:getCallForHelpBlocked()))"));
        Lua("mob:setCallForHelpBlocked(false) mob:setIsAggroable(true)");
        Check("set / isAggroable, getRoamFlags, getLink", $"true/{rabbit.RoamFlags}/{(rabbit.Link ? 1 : 0)}",
            Lua("report(tostring(mob:isAggroable()) .. '/' .. mob:getRoamFlags() .. '/' .. mob:getLink())"));
        rabbit.IsAggroable = false;
        var skillRow = Sql(db, "SELECT mob_skill_id, mob_skill_distance FROM mob_skills ORDER BY mob_skill_id LIMIT 1");
        skillRow.Next();
        Check("getAbilityDistance: the skill's; hasTPMoves", $"{skillRow.Get<float>("mob_skill_distance")}/true",
            Lua($"report(mob:getAbilityDistance({skillRow.Get<int>("mob_skill_id")}) .. '/' .. tostring(mob:hasTPMoves()))"));
        byte str0 = rabbit.StrRank;
        Lua("mob:setStatRank(1, 5) mob:setStatRank(8, 7)");
        Check("setStatRank: STR to 5; ATT refuses 7", $"5/{rabbit.AttRank}", Lua("report(mob:getStatRank(1) .. '/' .. mob:getStatRank(8))"));
        rabbit.StrRank = str0;
        byte size0 = rabbit.ModelSize;
        float hitbox0 = rabbit.ModelHitboxSize;
        Lua("mob:setModelSize(5) mob:setHitboxSize(2.5)");
        Check("setModelSize clamped to 3, setHitboxSize", "3/2.5", $"{Lua("report(mob:getModelSize())")}/{rabbit.ModelHitboxSize}");
        rabbit.ModelSize = size0;
        rabbit.ModelHitboxSize = hitbox0;
        Lua("mob:renameEntity('Bunny', true)");
        var renamedMob = EntityUpdatePacket.Mob(rabbit, EntityUpdate.Update, EntityUpdatePacket.UpdatePos);
        Check("renameEntity on a mob: the name in each update", "Bunny/8/Bunny",
            $"{Lua("report(mob:getPacketName())")}/{renamedMob.Buffer[0x0A] & 0x08}/{System.Text.Encoding.ASCII.GetString(renamedMob.Buffer, 0x35, 5)}");
        rabbit.IsRenamed = false;
        Check("getPartyMember(0, 0) on a mob: itself", $"{Rabbit}", Lua("report(mob:getPartyMember(0, 0):getID())"));

        Console.WriteLine("steal and despoil");
        string stealItem = Lua("report(mob:getStealItem())");
        Lua("mob:itemStolen(true)");
        Check("itemStolen: nothing left to steal", "0", Lua("report(mob:getStealItem())"));
        Lua("mob:itemDespoiled(true)");
        Check("despoiled and stolen: nothing to despoil", "0", Lua("report(mob:getDespoilItem())"));
        Lua("mob:itemStolen(false) mob:itemDespoiled(false)");
        Check("the steal item back once the flag clears", stealItem, Lua("report(mob:getStealItem())"));
        Check("getDespoilDebuff: the item's effect, none for another", "149/0", Lua("report(mob:getDespoilDebuff(644) .. '/' .. mob:getDespoilDebuff(1))"));

        log.Clear();
        Lua("player:setTrueDetection(true)\nplayer:setLink(1)\nplayer:follow(mob, 1)");
        Check("the mob-only setters warn on a player", "True/True/True",
            $"{log.Exists(l => l.Contains("Attempting to set true detection for invalid entity type (Roamy)."))}/" +
            $"{log.Exists(l => l.Contains("Attempting to set link for invalid entity type (Roamy)."))}/" +
            $"{log.Exists(l => l.Contains("Invalid entity (Roamy) calling function."))}");

        string Tick1(Func<string> f)
        {
            Tick();
            return f();
        }
    }

    private static float Distance3(MobEntity a, MobEntity b) =>
        MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
}
