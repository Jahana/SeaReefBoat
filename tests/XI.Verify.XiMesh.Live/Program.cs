using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using XI.Common;
using XI.Ipc;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.XiMesh.Live -- live gate for the collision mesh (XiMesh.cs: ximesh_impl.cpp's loader, query /
// getTerrainAt / getFloorId, rayIntersect, getPositionInfo; los_cache.h) on server/ximeshes, and what stands on it
// (ZoneHost.XiMesh.cs: CZone::LoadXiMesh's names, CBaseEntity::CanSeeTarget, inWater) in West Ronfaure: the canSee /
// inWater bindings, zone getTerrainType / getFloorId, wide scan's mobs on the char's floor, the scent switch on a
// weather change, with the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 993790;
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

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        string meshDir = Path.GetFullPath(Path.Combine(dataDir, "..", "ximeshes"));

        Console.WriteLine("XiMesh: the West Ronfaure mesh");
        var mesh = XiMesh.Load(Path.Combine(meshDir, "West_Ronfaure.ximesh"));
        Check("a grid, blocks and placements", true, mesh.GridWidth > 0 && mesh.GridHeight > 0 && mesh.BlockCount > 0 && mesh.PlacementCount > 0);
        var floorA = mesh.Query(A.X, A.Y - 1, A.Z);
        Check("query a yalm above the first spawn (spawn points sit 1-2 yalms up): the ground under it", true, floorA is { } fa && fa.Y > A.Y && fa.Y - A.Y < 2.0f);
        Console.WriteLine($"  floor at A: {floorA?.Y} type {floorA?.Type} map {floorA?.MapId} roofed {floorA?.Roofed}");
        Check("its terrain is ground, not none or water", true,
            mesh.GetTerrainAt(A.X, A.Y - 1, A.Z) is not (TerrainType.None or TerrainType.ShallowWater or TerrainType.DeepWater));
        Check("query far off the grid: nothing", true, mesh.Query(A.X + 100000, A.Y, A.Z) == null);
        Check("getFloorId off the grid: 0", (byte)0, mesh.GetFloorId(A.X + 100000, A.Y, A.Z));
        var up = new Vector3(A.X, A.Y - 5, A.Z);
        var down = new Vector3(A.X, A.Y + 5, A.Z);
        Check("a ray from 5 up to 5 under the ground meets it", true, mesh.RayIntersect(up, down));
        Check("a ray 3 yalms long, 60 yalms up in the air, meets nothing", false,
            mesh.RayIntersect(new Vector3(A.X, A.Y - 60, A.Z), new Vector3(A.X + 3, A.Y - 60, A.Z)));
        var info = mesh.GetPositionInfo(new Vector3(A.X, A.Y, A.Z), -5, 10);
        Check("getPositionInfo straight down from 5 up: a hit", true, info != null);
        Check("a degenerate ray (start = end): nothing", false, mesh.RayIntersect(up, up));

        Console.WriteLine("XiMesh: every mesh of server/ximeshes loads");
        var sw = Stopwatch.StartNew();
        var failures = new List<string>();
        int count = 0;
        foreach (var file in Directory.GetFiles(meshDir, "*.ximesh"))
        {
            count++;
            try { XiMesh.Load(file); }
            catch (Exception e) { failures.Add($"{Path.GetFileName(file)}: {e.Message}"); }
        }
        Console.WriteLine($"  {count} meshes in {sw.ElapsedMilliseconds} ms");
        Check("all of them parse", "", string.Join(" | ", failures));

        Console.WriteLine("XiMeshName: the C++ name fix-ups");
        Check("Rala_Waterways_U", "Rala_Waterways_[U]", ZoneHost.XiMeshName("Rala_Waterways_U"));
        Check("Escha_ZiTah", "Escha-ZiTah", ZoneHost.XiMeshName("Escha_ZiTah"));
        Check("Desuetia_Empyreal_Paradox", "Desuetia-Empyreal_Paradox", ZoneHost.XiMeshName("Desuetia_Empyreal_Paradox"));
        Check("the Selbina pirates' ship", "Ship_bound_for_Selbina_ID-227", ZoneHost.XiMeshName("Ship_bound_for_Selbina_Pirates"));
        Check("Maquette_Abdhaljs-Legion_A", "Maquette_Abdhaljs-LegionA", ZoneHost.XiMeshName("Maquette_Abdhaljs-Legion_A"));
        Check("West_Ronfaure unchanged", "West_Ronfaure", ZoneHost.XiMeshName("West_Ronfaure"));
        int named = 0;
        var missing = new List<string>();
        var zr = Sql(db, "SELECT name FROM zone_settings");
        while (zr.Next())
        {
            string name = zr.Get<string>("name");
            named++;
            if (!File.Exists(Path.Combine(meshDir, ZoneHost.XiMeshName(name) + ".ximesh"))) missing.Add(name);
        }
        Console.WriteLine($"  {named} zones, {missing.Count} without a mesh");
        Check("most zones find their mesh", true, named > 0 && missing.Count * 4 < named);

        Console.WriteLine("the zone's mesh");
        var log = new List<string>();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("xim" + CharId), BoundValue.Of("x"), BoundValue.Of((byte)1));
        // the NEW_ADVENTURER title: not a first login, no charCreate
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,titles) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Meshy"), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(A.X), BoundValue.Of(A.Y), BoundValue.Of(A.Z), BoundValue.Of((byte)0), BoundValue.Of((byte)0), BoundValue.Of(titles));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        // a RNG 75: wide scan reaches 300 yalms
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 1500, 50, 11, 75)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, rng) VALUES (?, 75)", BoundValue.Of(CharId));
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

        var zoneMesh = zone.XiMeshOf(WestRonfaure);
        Check("XiMeshOf: loaded on first use, then the same one", true, zoneMesh != null && ReferenceEquals(zoneMesh, zone.XiMeshOf(WestRonfaure)));
        string dir = zone.XiMeshDir;
        zone.XiMeshDir = null;
        Check("XiMeshDir off: none", true, zone.XiMeshOf(WestRonfaure) == null);
        zone.XiMeshDir = dir;

        var rabbit = zone.ZoneInfoOf(WestRonfaure).MobById[Rabbit];
        Check("the rabbit is up", true, rabbit.IsSpawned);

        var s = new MapSession { CharId = CharId, AccountId = CharId };
        zone.OnLogin(s);
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

        var lua = scripts.Lua;
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local player = GetPlayerByID({CharId})\nlocal mob = GetMobByID({Rabbit})\nlocal zone = mob:getZone()\n" + code, "=verify_ximesh");
            return string.Join(",", got);
        }

        Console.WriteLine("Lua: canSee, inWater");
        Pos(rabbit.X + 3, rabbit.Y, rabbit.Z);
        Check("3 yalms from the rabbit, on its ground: each sees the other", "true/true",
            Lua("report(tostring(mob:canSee(player)) .. '/' .. tostring(player:canSee(mob)))"));
        Check("with the transparent barriers counted too: still seen", "true", Lua("report(tostring(mob:canSee(player, false)))"));
        Pos(rabbit.X + 3, rabbit.Y + 15, rabbit.Z);
        Check("15 yalms under the ground: the ground is in the way", "false/false",
            Lua("report(tostring(mob:canSee(player)) .. '/' .. tostring(player:canSee(mob)))"));
        Pos(rabbit.X + 3, rabbit.Y, rabbit.Z);
        Check("on dry land: not in the water", "false", Lua("report(tostring(player:inWater()))"));

        Console.WriteLine("Lua: zone getTerrainType, getFloorId");
        Check("getTerrainType at the rabbit: the mesh's", ((int)zoneMesh.GetTerrainAt(rabbit.X, rabbit.Y - 1, rabbit.Z)).ToString(),
            Lua($"report(tostring(zone:getTerrainType({{ x = {rabbit.X}, y = {rabbit.Y - 1}, z = {rabbit.Z} }})))"));
        Check("getFloorId at the rabbit: the mesh's", zoneMesh.GetFloorId(rabbit.X, rabbit.Y - 1, rabbit.Z).ToString(),
            Lua($"report(tostring(zone:getFloorId({{ x = {rabbit.X}, y = {rabbit.Y - 1}, z = {rabbit.Z} }})))"));
        Check("off the grid: none (0xFF) and floor 0", "255/0",
            Lua($"report(zone:getTerrainType({{ x = 100000, y = 0, z = 0 }}) .. '/' .. zone:getFloorId({{ x = 100000, y = 0, z = 0 }}))"));

        // a water surface somewhere in the zone, from high above
        (float X, float Y, float Z)? water = null;
        for (int gx = -800; gx <= 800 && water == null; gx += 8)
            for (int gz = -800; gz <= 800 && water == null; gz += 8)
                if (zoneMesh.Query(gx, -300, gz) is { Type: TerrainType.ShallowWater or TerrainType.DeepWater } w) water = (gx, w.Y, gz);
        if (water is { } wp)
        {
            Console.WriteLine($"  water at {wp.X},{wp.Y},{wp.Z}");
            Pos(wp.X, wp.Y - 0.5f, wp.Z);
            Check("standing in the water: inWater", "true", Lua("report(tostring(player:inWater()))"));
            Pos(rabbit.X + 3, rabbit.Y, rabbit.Z);
        }
        else Console.WriteLine("  (no water surface in the zone: inWater's true case not checked)");

        Console.WriteLine("wide scan: mobs, on the char's floor");
        List<BasicPacket> Scan()
        {
            s.PacketList.Clear();
            Send(0x0F4, 8, b => b[4] = 1);
            var list = new List<BasicPacket>(s.PacketList);
            return list.FindAll(p => (p.Buffer[0] | (p.Buffer[1] & 1) << 8) == 0x0F4);
        }
        uint? RabbitDot(List<BasicPacket> dots)
        {
            foreach (var p in dots)
            {
                uint bits = BitConverter.ToUInt32(p.Buffer, 4);
                if ((ushort)bits == rabbit.TargId) return bits;
            }
            return null;
        }
        var scan = Scan();
        var dot = RabbitDot(scan);
        Check("the rabbit is on the list: a mob (type 2) with its level", $"2/{rabbit.MLvl}",
            dot is { } d ? $"{d >> 24 & 7}/{d >> 16 & 0xFF}" : "missing");
        int npcDots = scan.FindAll(p => (BitConverter.ToUInt32(p.Buffer, 4) >> 24 & 7) == 1).Count;
        int lastNpc = scan.FindLastIndex(p => (BitConverter.ToUInt32(p.Buffer, 4) >> 24 & 7) == 1);
        int firstMob = scan.FindIndex(p => (BitConverter.ToUInt32(p.Buffer, 4) >> 24 & 7) == 2);
        Check("NPCs first, then the mobs", true, firstMob > lastNpc);
        Console.WriteLine($"  {npcDots} NPCs, {scan.Count - npcDots} mobs");
        rabbit.Untargetable = true;
        Check("an untargetable mob: off the list", false, RabbitDot(Scan()) != null);
        rabbit.Untargetable = false;
        rabbit.SetMobMod(MobMod.NoWidescan, 1);
        Check("NO_WIDESCAN: off the list", false, RabbitDot(Scan()) != null);
        rabbit.SetMobMod(MobMod.NoWidescan, 0);
        Check("back on the list", true, RabbitDot(Scan()) != null);

        Console.WriteLine("scent: rain washes out the trail");
        short detection = rabbit.GetMobMod(MobMod.Detection);
        Console.WriteLine($"  the rabbit's detection: 0x{detection:X}");
        rabbit.SetMobMod(MobMod.Detection, (short)(detection | 0x100));
        zone.SetWeather(WestRonfaure, 6);
        Check("rain: a scent tracker loses the scent", true, rabbit.DisableScent);
        zone.SetWeather(WestRonfaure, 0);
        Check("clear skies: back", false, rabbit.DisableScent);
        rabbit.SetMobMod(MobMod.Detection, (short)(detection & ~0x100));
        zone.SetWeather(WestRonfaure, 6);
        Check("rain on a mob without scent detection: untouched", false, rabbit.DisableScent);
        zone.SetWeather(WestRonfaure, 0);
        rabbit.SetMobMod(MobMod.Detection, detection);
    }
}
