using System;
using System.Collections.Generic;
using System.IO;
using XI.Common;
using XI.Map;
using XI.Ipc;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.NavMesh.Live -- live gate for the navmesh (NavMesh.cs, PathInset.cs, ZoneHost.NavMesh.cs and its users):
// DetourNavMesh loading navmeshes/West_Ronfaure.nav (and refusing a missing file or one that is not a navmesh set),
// findPath between two spawn points with and without a body's clearance (the inset string pull), validPosition,
// findClosestValidPoint, snapToValidPosition, moveAlongSurface, findRandomPosition and findFurthestValidPoint;
// the zone's navmesh made once, none with NavMeshDir off; a region roam on the mesh; the chase on a navmesh path
// (IsPathDirect, ChunkCount), WarpTo, the idle mob off the mesh despawned, zone:isNavigablePoint,
// GetFurthestValidPosition and DrawIn snapping onto the mesh. A Wild Rabbit of West Ronfaure and one WAR 75, with the
// real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 993700;
    private const ushort WestRonfaure = 100;
    private const uint Rabbit = 17186822;

    // two Wild Rabbit spawn points of West Ronfaure's mobs.yaml
    private static readonly (float X, float Y, float Z) A = (-442.939f, -22.108f, -92.488f);
    private static readonly (float X, float Y, float Z) B = (-429.382f, -18.441f, -204.904f);

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
            Sql(db, $"DELETE FROM `{t}` WHERE charid BETWEEN 993700 AND 993799");
        Sql(db, "DELETE FROM chars WHERE charid BETWEEN 993700 AND 993799");
        Sql(db, "DELETE FROM accounts WHERE id BETWEEN 993700 AND 993799");
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

    // worldAngle: the facing from a to b
    private static byte Facing(float ax, float az, float bx, float bz)
    {
        short raw = (short)(MathF.Atan2(bz - az, bx - ax) * -(128.0f / MathF.PI));
        return (byte)((raw % 256 + 256) % 256);
    }

    private static float Dist((float X, float Y, float Z) a, (float X, float Y, float Z) b) =>
        MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        string navDir = Path.GetFullPath(Path.Combine(dataDir, "..", "navmeshes"));

        Console.WriteLine("DetourNavMesh: loading");
        var log = new List<string>();
        var nav = DetourNavMesh.Load(Path.Combine(navDir, "West_Ronfaure.nav"), WestRonfaure, log.Add);
        Check("West_Ronfaure.nav loads", true, nav != null);
        Check("a missing file: none", true, DetourNavMesh.Load(Path.Combine(navDir, "Nowhere.nav"), 0, log.Add) == null);
        Check("not a navmesh set (grades.yaml): none", true, DetourNavMesh.Load(Path.Combine(dataDir, "grades.yaml"), 0, log.Add) == null);

        Console.WriteLine("DetourNavMesh: queries");
        Check("validPosition at a spawn point", true, nav.ValidPosition(A.X, A.Y + 1.2f, A.Z));
        Check("validPosition 50 yalms up in the air: no", false, nav.ValidPosition(A.X, A.Y - 50, A.Z));
        var closest = nav.FindClosestValidPoint(A.X, A.Y - 20, A.Z);
        // the nearest point in 3D: the ground straight below (about 19 down) or a slope nearer still
        Check("findClosestValidPoint from 20 up: a point on the mesh no farther than the ground below", true,
            closest is { } cp && nav.ValidPosition(cp.X, cp.Y, cp.Z) && Dist(cp, (A.X, A.Y - 20, A.Z)) <= 20.0f);
        Check("findClosestValidPoint 100 up: nothing within 60", true, nav.FindClosestValidPoint(A.X, A.Y - 100, A.Z) == null);
        float sx = A.X, sy = A.Y - 3, sz = A.Z;
        nav.SnapToValidPosition(ref sx, ref sy, ref sz);
        Check("snapToValidPosition: onto the mesh", true, nav.ValidPosition(sx, sy, sz) && Math.Abs(sy - A.Y) < 3);
        float ux = A.X, uy = A.Y - 100, uz = A.Z;
        nav.SnapToValidPosition(ref ux, ref uy, ref uz);
        Check("snapToValidPosition with no mesh in reach: unchanged", true, uy == A.Y - 100);

        var path = nav.FindPath(A.X, A.Y, A.Z, B.X, B.Y, B.Z, 0, false);
        Check("findPath between the spawns: a complete path of several waypoints", true, path is { IsPartial: false } && path.Points.Count >= 2);
        var end = path.Points[^1];
        Check("ending at the second spawn", true, Flat(end.X, end.Z, B.X, B.Z) < 0.5f);
        bool allValid = true;
        float length = 0;
        var prev = A;
        foreach (var p in path.Points)
        {
            allValid &= nav.ValidPosition(p.X, p.Y, p.Z);
            length += Dist(prev, (p.X, p.Y, p.Z));
            prev = (p.X, p.Y, p.Z);
        }
        Check("every waypoint on the mesh", true, allValid);
        Check("longer than the straight line (it goes round)", true, length > Dist(A, B) + 0.5f);
        var inset = nav.FindPath(A.X, A.Y, A.Z, B.X, B.Y, B.Z, 1.0f, false);
        var insetEnd = inset?.Points[^1];
        Check("with a body's clearance: still complete, still at the second spawn", true,
            inset is { IsPartial: false } && Flat(insetEnd.Value.X, insetEnd.Value.Z, B.X, B.Z) < 0.5f);
        bool differs = inset.Points.Count != path.Points.Count;
        for (int i = 0; !differs && i < path.Points.Count; i++)
            differs = Flat(inset.Points[i].X, inset.Points[i].Z, path.Points[i].X, path.Points[i].Z) > 0.01f;
        Check("the clearance keeps the corners off the walls (another route)", true, differs);
        Check("findPath from up in the air: none", true, nav.FindPath(A.X, A.Y - 100, A.Z, B.X, B.Y, B.Z, 0, false) == null);

        Check("moveAlongSurface 5 yalms east", true,
            nav.MoveAlongSurface(A.X, A.Y, A.Z, A.X + 5, A.Y, A.Z, out var reached) && Flat(reached.X, reached.Z, A.X, A.Z) > 1 && nav.ValidPosition(reached.X, reached.Y, reached.Z));
        Check("moveAlongSurface from up in the air: false", false, nav.MoveAlongSurface(A.X, A.Y - 100, A.Z, A.X + 5, A.Y, A.Z, out _));
        var rng = new Random(5);
        var random = nav.FindRandomPosition(A.X, A.Y, A.Z, 5, rng);
        Check("findRandomPosition: a point on the mesh", true, random is { } rp && nav.ValidPosition(rp.X, rp.Y, rp.Z));
        var furthest = nav.FindFurthestValidPoint(A.X, A.Y, A.Z, A.X + 3, A.Y, A.Z);
        Check("findFurthestValidPoint 3 east: somewhere along the way", true, furthest is { } fp && Flat(fp.X, fp.Z, A.X, A.Z) <= 3.01f);

        Console.WriteLine("the zone's navmesh");
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("nav" + CharId), BoundValue.Of("x"), BoundValue.Of((byte)1));
        // the NEW_ADVENTURER title: not a first login, no charCreate
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,titles) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Navvy"), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(A.X), BoundValue.Of(A.Y), BoundValue.Of(A.Z), BoundValue.Of((byte)0), BoundValue.Of((byte)0), BoundValue.Of(titles));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 1500, 50, 1, 75)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 75)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(CharId));

        var zone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
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

        var zoneNav = zone.NavMeshOf(WestRonfaure);
        Check("NavMeshOf: loaded on first use, then the same one", true, zoneNav != null && ReferenceEquals(zoneNav, zone.NavMeshOf(WestRonfaure)));
        string dir = zone.NavMeshDir;
        zone.NavMeshDir = null;
        Check("NavMeshDir off: none", true, zone.NavMeshOf(WestRonfaure) == null);
        zone.NavMeshDir = dir;

        var info = zone.ZoneInfoOf(WestRonfaure);
        var rabbit = info.MobById[Rabbit];
        Check("the rabbit spawned on the mesh (a region point snapped)", true, rabbit.IsSpawned && zoneNav.ValidPosition(rabbit.X, rabbit.Y, rabbit.Z));

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
        void Due()
        {
            rabbit.PathFind?.Clear();
            rabbit.LastActionMs = now - rabbit.GetMobMod(MobMod.RoamCool) * 1000L - 1000;
        }

        var lua = scripts.Lua;
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local player = GetPlayerByID({CharId})\nlocal mob = GetMobByID({Rabbit})\nlocal zone = mob:getZone()\n" + code, "=verify_navmesh");
            return string.Join(",", got);
        }

        Console.WriteLine("a region roam on the mesh");
        Pos(rabbit.X - 40, rabbit.Y, rabbit.Z);
        Pump();
        Due();
        Tick();
        Check("the idle action set a roam path", true, rabbit.PathFind is { IsFollowingPath: true });
        bool onMesh = true;
        int legs = 0;
        bool wasFollowing = true;
        for (int t = 0; t < 1500; t++)
        {
            Tick();
            bool following = rabbit.PathFind.IsFollowingPath;
            if (wasFollowing && !following)
            {
                legs++;
                onMesh &= zoneNav.FindClosestValidPoint(rabbit.X, rabbit.Y, rabbit.Z) is { } at && Flat(at.X, at.Z, rabbit.X, rabbit.Z) < 0.5f;
            }
            wasFollowing = following;
        }
        Check("it roamed, and every rest is on the mesh", true, legs >= 5 && onMesh);
        Check("still spawned (never judged off the mesh)", true, rabbit.IsSpawned && rabbit.FadeAtMs == 0);

        Console.WriteLine("the chase on a navmesh path");
        rabbit.PathFind.Clear();
        var target = zoneNav.FindRandomPosition(rabbit.X, rabbit.Y, rabbit.Z, 2.0f, new Random(3));
        var away = zoneNav.FindFurthestValidPoint(rabbit.X, rabbit.Y, rabbit.Z, rabbit.X - 12, rabbit.Y, rabbit.Z).Value;
        Pos(away.X, away.Y, away.Z);
        float startGap = Flat(rabbit.X, rabbit.Z, c.X, c.Z);
        rabbit.Enmity.UpdateEnmityFromDamage(c, 10);
        Tick(4);
        Check("engaged the char", true, rabbit.Engaged && rabbit.BattleTarget == c);
        bool chaseOnMesh = true;
        int ticks = 0;
        float meleeRange = rabbit.ModelHitboxSize + 2.0f + c.ModelHitboxSize10 / 10.0f;
        for (; ticks < 60 && Flat(rabbit.X, rabbit.Z, c.X, c.Z) > meleeRange; ticks++)
        {
            Tick();
            chaseOnMesh &= zoneNav.FindClosestValidPoint(rabbit.X, rabbit.Y, rabbit.Z) is { } at && Flat(at.X, at.Z, rabbit.X, rabbit.Z) < 1.0f;
        }
        Check($"in melee range ({startGap:F1} yalms away at first)", true, Flat(rabbit.X, rabbit.Z, c.X, c.Z) <= meleeRange);
        Check("on the mesh all the way", true, chaseOnMesh);
        Check("one navmesh leg for the PathTo", true, rabbit.PathFind.ChunkCount <= 1);
        Tick(3);
        Check("settled: a direct route needs no path", false, rabbit.PathFind.IsFollowingPath);
        _ = target;

        Console.WriteLine("IsPathDirect / WarpTo");
        var pf = rabbit.PathFind;
        pf.PathThrough(new List<PathPoint> { new PathPoint(rabbit.X + 3, rabbit.Y, rabbit.Z) });
        Check("a straight path is direct", true, pf.IsPathDirect(1.1f));
        pf.PathThrough(new List<PathPoint> { new PathPoint(rabbit.X + 3, rabbit.Y, rabbit.Z + 3), new PathPoint(rabbit.X, rabbit.Y, rabbit.Z + 0.5f) });
        Check("a detour is not", false, pf.IsPathDirect(1.1f));
        Check("BuildDirectPath: one waypoint", true, pf.BuildDirectPath(rabbit.X + 2, rabbit.Y, rabbit.Z) && pf.IsFollowingPath && pf.IsPathDirect());
        pf.WarpTo(c.X, c.Y, c.Z, c.Rotation, 2.0f);
        Check("WarpTo: 2 yalms from the target, facing it, no path", true,
            Math.Abs(Flat(rabbit.X, rabbit.Z, c.X, c.Z) - 2.0f) < 0.01f && !pf.IsFollowingPath &&
            rabbit.Rotation == Facing(rabbit.X, rabbit.Z, c.X, c.Z));

        Console.WriteLine("the teleport mob mods");
        // Eald'Narche's: warp_in_ealdnarche (988, on itself) uses TELEPORT_END, warp_out_ealdnarche (989), which
        // teleports it 1.5 yalms from its battle target
        rabbit.SetMobMod(MobMod.TeleportType, 1);
        rabbit.SetMobMod(MobMod.TeleportStart, 988);
        rabbit.SetMobMod(MobMod.TeleportEnd, 989);
        rabbit.SetMobMod(MobMod.TeleportCd, 30);
        var far = zoneNav.FindFurthestValidPoint(rabbit.X, rabbit.Y, rabbit.Z, rabbit.X + 15, rabbit.Y, rabbit.Z).Value;
        Pos(far.X, far.Y, far.Z);
        rabbit.PathFind.Clear();
        rabbit.LastSpecialMs = now - 31_000;
        float gap = Flat(rabbit.X, rabbit.Z, c.X, c.Z);
        Tick();
        Check($"TELEPORT_TYPE 1, {gap:F1} yalms off: the warp-in skill on itself", "988/True",
            $"{rabbit.SkillState?.Skill.Id}/{rabbit.SkillState?.Target == rabbit}");
        for (int t = 0; t < 10 && Flat(rabbit.X, rabbit.Z, c.X, c.Z) > 2.0f; t++) Tick();
        Check("then the warp-out beside the target (NearLocation 1.5 yalms off)", true, Flat(rabbit.X, rabbit.Z, c.X, c.Z) <= 2.0f);
        Check("NearLocation({ x = 10, y = 3, z = -4, rot = 0 }, 2, 0): 2 yalms along x", "12/3/-4",
            Lua("local p = NearLocation({ x = 10, y = 3, z = -4, rot = 0 }, 2, 0)\nreport(string.format('%g/%g/%g', p.x, p.y, math.floor(p.z * 1000 + 0.5) / 1000))"));
        Tick(3);
        Pos(far.X, far.Y, far.Z);
        (rabbit.X, rabbit.Y, rabbit.Z) = (far.X - 12, far.Y, far.Z);
        rabbit.PathFind.Clear();
        Tick();
        Check("within the 30 s cooldown: no jump", true, rabbit.SkillState == null);
        rabbit.SetMobMod(MobMod.TeleportType, 2);
        rabbit.LastSpecialMs = now - 1;
        Tick();
        Check("TELEPORT_TYPE 2, inside the skill's 22 yalms: the warp-in at once", 988, (int?)rabbit.SkillState?.Skill.Id ?? 0);
        for (int t = 0; t < 10 && rabbit.SkillState != null; t++) Tick();
        (rabbit.X, rabbit.Y, rabbit.Z) = (far.X - 25, far.Y, far.Z);
        rabbit.PathFind.Clear();
        rabbit.LastSpecialMs = now - 1;
        float before = rabbit.X;
        Tick(3);
        Check("TELEPORT_TYPE 2, beyond it: neither warps nor walks", true, rabbit.SkillState == null && rabbit.X == before && !rabbit.PathFind.IsFollowingPath);
        rabbit.SetMobMod(MobMod.TeleportType, 0);
        rabbit.SetMobMod(MobMod.TeleportStart, 0);
        rabbit.SetMobMod(MobMod.TeleportEnd, 0);
        rabbit.SetMobMod(MobMod.TeleportCd, 0);
        Pos(rabbit.X + 2, rabbit.Y, rabbit.Z);
        Tick(3);

        Console.WriteLine("Lua: isNavigablePoint, GetFurthestValidPosition, DrawIn");
        Check("zone:isNavigablePoint at the rabbit / 50 up",
            "true/false", Lua($"report(tostring(zone:isNavigablePoint({{ x = {A.X}, y = {A.Y + 1.2f}, z = {A.Z} }})) .. '/' .. tostring(zone:isNavigablePoint({{ x = {A.X}, y = {A.Y - 50}, z = {A.Z} }})))"));
        Check("GetFurthestValidPosition(mob, 2, pi): a point about 2 yalms off",
            "true", Lua("local p = GetFurthestValidPosition(mob, 2, math.pi)\nreport(tostring(p ~= nil and mob:checkDistance(p) <= 2.1))"));
        float gx = rabbit.X, gz = rabbit.Z, gy = rabbit.Y;
        Lua($"DrawIn(player, {{ x = {gx}, y = {gy - 6}, z = {gz}, rot = 0 }}, 0, 0)");
        var below = zoneNav.FindClosestValidPoint(gx, gy - 6, gz).Value;
        Check("DrawIn 6 up in the air: set down on the mesh, a yalm higher", true, Math.Abs(c.Y - (below.Y - 1.0f)) < 0.01f);

        Console.WriteLine("an idle mob off the mesh despawns");
        rabbit.Enmity.Clear();
        rabbit.BattleTarget = null;
        Lua("mob:disengage()");
        Tick(2);
        Check("out of combat", false, rabbit.Engaged);
        rabbit.PathFind?.Clear();
        (rabbit.X, rabbit.Y, rabbit.Z) = (rabbit.X, rabbit.Y - 150, rabbit.Z);
        Tick(3);
        Check("150 yalms up, nothing walkable near: despawned", true, rabbit.FadeAtMs != 0 || !rabbit.IsSpawned);

        Console.WriteLine("a pet no path reaches its master from (the rabbit gone): warped beside it (CPetController::DoRoamTick's WarpTo)");
        Lua("player:changeJob(xi.job.SMN) player:setLevel(75)");
        c.Mp = c.MaxMp;
        Lua("player:spawnPet(xi.petId.CARBUNCLE)");
        var pet = c.PPet as PetEntity;
        Check("Carbuncle out beside the SMN", true, pet != null);
        if (pet != null)
        {
            Tick(2);
            float masterY = c.Y;
            c.Y -= 100; // up in the air: nothing walkable near
            Tick(2);
            Check("warped 2.1 yalms behind the master's facing, at its height, no path", true,
                Math.Abs(Flat(pet.X, pet.Z, c.X, c.Z) - 2.1f) < 0.01f && pet.Y == c.Y && pet.PathFind?.IsFollowingPath != true);
            c.Y = masterY;
            Lua("player:despawnPet() player:changeJob(xi.job.WAR) player:setLevel(75)");
            Tick(20);
        }
    }
}
