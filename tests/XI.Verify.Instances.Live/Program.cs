using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Instances.Live -- live gate for ZoneHost.Instances.cs: createInstance refused for an unknown id and queued
// for a known one, loaded at the end of the tick (the run's own mobs and NPCs from the instanced zone's yaml for the
// ids instance_entities lists, the base zone left empty), the assault's onInstanceCreatedCallback (the level cap, the
// armband, setInstance) and onInstanceCreated (the worms spawned in the run, the Rune of Release moved); the CInstance
// getters and setters; GetMobByID / GetNPCByID / GetEntityByID by instance; the char zoning into the instanced zone back
// into its run (the entry position, the level restriction, afterInstanceRegister 400 ms on), seeing the run's mobs;
// setProgress completing the assault; the emptied, completed run cleaned up; a char coming back with no run sent to
// onInstanceLoadFailed's zone; a run nobody entered wiping and failing on its time updates. Leujaoam Cleansing (6900),
// entered from Caedarva Mire, with the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 994110;
    private const ushort CaedarvaMire = 79, LeujaoamSanctum = 69;
    private const ushort LeujaoamCleansing = 6900;
    private const uint FirstWorm = 17059841, RuneOfRelease = 17060015;

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static ResultSet Sql(IDatabase db, string q, params BoundValue[] a) => db.Execute(q, a);

    private static string Scalar(IDatabase db, string q, params BoundValue[] a)
    {
        var r = db.Execute(q, a);
        return r != null && r.Next() ? r.Get<string>(0u) : null;
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
        // the day music override the run is given here (Leujaoam Cleansing has none)
        Sql(db, "UPDATE instance_list SET music_day = NULL WHERE instanceid = ?", BoundValue.Of(LeujaoamCleansing));
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
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
        var loader = new CharLoader(db, Grades.Load(dataDir), AbilityTable.Load(db, _ => true), stats);
        var log = new List<string>();
        var zone = new ZoneHost(db, loader, dataDir, true, log.Add);
        zone.SendIpc = _ => { };
        long nowMs = 1_000_000;
        zone.NowMs = () => nowMs;
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);
        var lua = scripts.Lua;
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local player = GetPlayerByID({CharId})\n" + code, "=verify_instances");
            return string.Join(",", got);
        }

        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("in" + CharId), BoundValue.Of("x"), BoundValue.Of((byte)1));
        // the NEW_ADVENTURER title: not a first login, no charCreate
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,moghouse,nation,gmlevel,titles) " +
                "VALUES (?,?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Delver"), BoundValue.Of(CaedarvaMire), BoundValue.Of((ushort)0),
            BoundValue.Of(-440.0f), BoundValue.Of(-10.0f), BoundValue.Of(-600.0f), BoundValue.Of(0u), BoundValue.Of((byte)0), BoundValue.Of((byte)0),
            BoundValue.Of(titles));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl, sjob, slvl) VALUES (?, 900, 0, 1, 75, 0, 0)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs SET charid = ?, unlocked = 2, war = 75", BoundValue.Of(CharId));
        Sql(db, "INSERT INTO char_storage(charid, inventory) VALUES (?, 30) ON DUPLICATE KEY UPDATE inventory = 30", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_unlocks(charid) VALUES (?)", BoundValue.Of(CharId));
        Sql(db, "UPDATE instance_list SET music_day = 99 WHERE instanceid = ?", BoundValue.Of(LeujaoamCleansing));

        MapSession s = null;
        var loginPackets = new List<BasicPacket>();
        CharData c = null;
        void Send(ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(s, opcode, new BasicPacket(buf));
            zone.AfterParse(s);
        }
        void Login()
        {
            s = new MapSession { CharId = CharId, AccountId = CharId };
            zone.OnLogin(s);
            c = (CharData)s.Char;
            Send(0x00A, 92, buf => BitConverter.GetBytes(CharId).CopyTo(buf, 12));
            loginPackets = new List<BasicPacket>(s.PacketList);
            Send(0x00C, 12);
        }
        void Leave()
        {
            zone.OnCharTimeout(s, false);
            zone.OnZoneOut(s);
        }
        void Tick(long ms = 400)
        {
            nowMs += ms;
            zone.Tick();
        }
        int LogCount(string line) => log.Count(l => l == line);
        Login();

        Console.WriteLine("createInstance: refused for an unknown id, queued for a known one, loaded at the end of the tick");
        Lua("player:createInstance(9999)");
        Check("an unknown instance id: refused with the error", 1, LogCount("CLuaBaseEntity::createInstance: Invalid instanceID: 9999"));
        Lua("player:setLocalVar('AssaultCap', 50)");
        Lua($"player:createInstance({LeujaoamCleansing})");
        Check("queued: no instance yet", "nil", Lua("report(tostring(player:getInstance()))"));
        Tick();
        Check("loaded: the char's instance (the callback's setInstance)", $"{LeujaoamCleansing}/leujaoam_cleansing/{CaedarvaMire}/{LeujaoamSanctum}",
            Lua("local i = player:getInstance() report(i:getID() .. '/' .. i:getName() .. '/' .. i:getEntranceZoneID() .. '/' .. i:getZone():getID())"));
        Check("the callback: the level cap from AssaultCap, the armband var", "50/1/0",
            Lua("report(player:getInstance():getLevelCap() .. '/' .. player:getCharVar('Assault_Armband') .. '/' .. player:getLocalVar('AssaultCap'))"));
        Check("the time limit (30 minutes) and the entry position", "1800/280/-7.5/35/195",
            Lua("local i = player:getInstance() local p = i:getEntryPos() report(i:getTimeLimit() .. '/' .. p.x .. '/' .. p.y .. '/' .. p.z .. '/' .. p.rot)"));
        Check("the run's mobs: the 15 worms instance_entities lists, all spawned by onInstanceCreated", "15/15",
            Lua("local n, up = 0, 0 for _, m in ipairs(player:getInstance():getMobs()) do n = n + 1 if m:isSpawned() then up = up + 1 end end report(n .. '/' .. up)"));
        Check("the run's NPCs: the lockbox, the rune and the rest instance_entities lists", "6",
            Lua("report(#player:getInstance():getNpcs())"));
        Check("onInstanceCreated moved the Rune of Release", "476/40",
            Lua($"local n = GetNPCByID({RuneOfRelease}, player:getInstance()) report(n:getXPos() .. '/' .. n:getZPos())"));
        Check("the instanced zone's own lists stay empty", "0/0", $"{zone.ZoneInfoOf(LeujaoamSanctum).Mobs.Count}/{zone.ZoneInfoOf(LeujaoamSanctum).Npcs.Count}");
        Check("GetMobByID without the instance: nothing (the zone has no mobs), with it: the run's worm", $"nil/{FirstWorm}",
            Lua($"report(tostring(GetMobByID({FirstWorm})) .. '/' .. GetMobByID({FirstWorm}, player:getInstance()):getID())"));
        Check("GetEntityByID and instance:getEntity by targid", $"{RuneOfRelease}/{FirstWorm}",
            Lua($"local i = player:getInstance() report(GetEntityByID({RuneOfRelease}, i):getID() .. '/' .. i:getEntity({FirstWorm & 0xFFF}):getID())"));
        Check("a worm's instance", LeujaoamCleansing.ToString(), Lua($"report(GetMobByID({FirstWorm}, player:getInstance()):getInstance():getID())"));
        Check("the CInstance setters", "7/3/1200/9/120",
            Lua("local i = player:getInstance() i:setStage(7) i:setLocalVar('x', 3) i:setTimeLimit(1200) i:setLastTimeUpdate(9) i:setWipeTime(120) " +
                "report(i:getStage() .. '/' .. i:getLocalVar('x') .. '/' .. i:getTimeLimit() .. '/' .. i:getLastTimeUpdate() .. '/' .. i:getWipeTime()) " +
                "i:setTimeLimit(1800) i:setWipeTime(0) i:setLastTimeUpdate(0)"));
        Check("GetCachedInstanceScript: the assault's object", "function",
            Lua($"report(type(GetCachedInstanceScript({LeujaoamCleansing}).onInstanceCreatedCallback))"));
        Check("no chars in the run yet", "0", Lua("report(#player:getInstance():getChars())"));
        s.PacketList.Clear();
        Lua($"player:instanceEntry(GetNPCByID({RuneOfRelease}, player:getInstance()), 4)");
        var reg = s.PacketList.FirstOrDefault(p => (p.Buffer[0] | (p.Buffer[1] & 1) << 8) == 0x0BF);
        Check("instanceEntry: GP_SERV_COMMAND_REGISTRATION with the response and the NPC's act index", $"28/4/{RuneOfRelease & 0xFFF}",
            reg == null ? "none" : $"{reg.GetSize()}/{BitConverter.ToUInt16(reg.Buffer, 6)}/{BitConverter.ToUInt32(reg.Buffer, 12)}");

        Console.WriteLine("zoning in: the char back in its run, at the entry, under the level cap");
        Leave();
        Sql(db, "UPDATE chars SET pos_zone = ?, pos_prevzone = ? WHERE charid = ?", BoundValue.Of(LeujaoamSanctum), BoundValue.Of(CaedarvaMire), BoundValue.Of(CharId));
        Login();
        var loginPacket = loginPackets.Find(p => (p.Buffer[0] | (p.Buffer[1] & 1) << 8) == 0x00A);
        var baseZone = zone.ZoneInfoOf(LeujaoamSanctum);
        Check("GP_SERV_COMMAND_LOGIN: the run's day music override, the zone's other music", $"99/{baseZone.MusicNight}/{baseZone.BattleSolo}",
            loginPacket == null ? "none" : $"{BitConverter.ToUInt16(loginPacket.Buffer, 86)}/{BitConverter.ToUInt16(loginPacket.Buffer, 88)}/{BitConverter.ToUInt16(loginPacket.Buffer, 90)}");
        Check("in the run (registered by setInstance)", $"{LeujaoamCleansing}/1",
            Lua("local i = player:getInstance() report(i:getID() .. '/' .. #i:getChars())"));
        Check("its first entry: the entry position", "280/-7.5/35/195", $"{c.X}/{c.Y}/{c.Z}/{c.Rotation}");
        var restriction = c.StatusEffects.GetStatusEffect(143);
        Check("the level restriction at the cap", "50", restriction?.Power);
        Tick(500);
        Check("afterInstanceRegister 400 ms on: the assault entered", "true",
            Lua("report(tostring(player:getCharVar('assaultEntered') == xi.assault.mission.LEUJAOAM_CLEANSING))"));
        // the position report brings the entities nearby into view
        Send(0x015, 32, b =>
        {
            BitConverter.GetBytes(284.0f).CopyTo(b, C2sPos.OffX);
            BitConverter.GetBytes(-8.5f).CopyTo(b, C2sPos.OffZ);
            BitConverter.GetBytes(115.0f).CopyTo(b, C2sPos.OffY);
        });
        Check("the run's worm near the char comes into view", true, c.SpawnMobList.Contains(FirstWorm));
        Check("the char shares its targid space with the run, not the empty zone", true, c.TargId >= 0x400 && c.TargId < 0x700);

        Console.WriteLine("a trigger area in the run: the instance's script runs, with the instance");
        // luautils::OnTriggerAreaEnter / Leave read xi.zones[zone].instances[name] for a char in an instance. The
        // assault's own script lives under xi.assaults, so the test puts the functions where the C++ looks; the
        // zone's are not run.
        void Move(float x, float y, float z) => Send(0x015, 32, b =>
        {
            BitConverter.GetBytes(x).CopyTo(b, C2sPos.OffX);
            BitConverter.GetBytes(y).CopyTo(b, C2sPos.OffZ);
            BitConverter.GetBytes(z).CopyTo(b, C2sPos.OffY);
        });
        float atX = c.X, atY = c.Y, atZ = c.Z;
        static string Inv(float f) => f.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Lua("player:setLocalVar('ZoningIn', 0) areaHits = '' " +
            "local function hit(kind) return function(p, area, instance) " +
            "areaHits = areaHits .. kind .. ':' .. area:getTriggerAreaID() .. ':' .. " +
            "(instance and instance:getID() or 'nil') .. ' ' end end " +
            "local zone = xi.zones.Leujaoam_Sanctum " +
            "zone.instances = zone.instances or {} " +
            "zone.instances.leujaoam_cleansing = " +
            "{ onTriggerAreaEnter = hit('enter'), onTriggerAreaLeave = hit('leave') } " +
            "savedZoneEnter, savedZoneLeave = zone.Zone.onTriggerAreaEnter, zone.Zone.onTriggerAreaLeave " +
            "zone.Zone.onTriggerAreaEnter, zone.Zone.onTriggerAreaLeave = hit('zone-enter'), hit('zone-leave') " +
            $"player:getZone():registerCylindricalTriggerArea(901, {Inv(atX)}, {Inv(atZ)}, 5)");
        Tick();
        Check("entering: the instance's onTriggerAreaEnter with the run", $"enter:901:{LeujaoamCleansing} ",
            Lua("report(areaHits)"));
        Check("entering: the instance's script is the event file",
            "./scripts/zones/Leujaoam_Sanctum/instances/leujaoam_cleansing.lua", c.EventPreparation.ScriptFile);
        Lua("areaHits = ''");
        Move(atX + 20, atY, atZ);
        Tick();
        Check("leaving: the instance's onTriggerAreaLeave with the run", $"leave:901:{LeujaoamCleansing} ",
            Lua("report(areaHits)"));
        Check("leaving: the unprefixed file name", "scripts/zones/Leujaoam_Sanctum/instances/leujaoam_cleansing.lua",
            c.EventPreparation.ScriptFile);
        Move(atX, atY, atZ);
        Tick();
        // a zone change from inside the area (charutils::SendToZone): the leave runs before the char leaves the run.
        // The destination's position is the area's, so the tick's area check sees no move out of it first.
        Lua("areaHits = ''");
        Lua($"player:setPos({Inv(atX)}, {Inv(atY)}, {Inv(atZ)}, 0, {CaedarvaMire})");
        Tick();
        Check("a zone change inside the area: the leave runs while the char is still in the run",
            $"leave:901:{LeujaoamCleansing} ", Lua("report(areaHits)"));
        // back into the run (already entered: its own position kept), into the area again
        Lua("areaHits = ''");
        Leave();
        Sql(db, "UPDATE chars SET pos_zone = ?, pos_prevzone = ? WHERE charid = ?", BoundValue.Of(LeujaoamSanctum),
            BoundValue.Of(CaedarvaMire), BoundValue.Of(CharId));
        Login();
        Lua("player:setLocalVar('ZoningIn', 0)");
        Move(atX, atY, atZ);
        Tick();
        Check("back in the run and in the area", $"{LeujaoamCleansing}/enter:901:{LeujaoamCleansing} ",
            Lua("report(player:getInstance():getID() .. '/' .. areaHits)"));
        Lua("areaHits = ''");

        Console.WriteLine("progress: the assault completes, the emptied run is cleaned up");
        Lua("player:getInstance():setProgress(15)");
        Check("setProgress(15): completed by onInstanceProgressUpdate", "15/true",
            Lua("local i = player:getInstance() report(i:getProgress() .. '/' .. tostring(i:completed()))"));
        Lua("completedRun = player:getInstance()");
        Tick();
        Check("completed but not empty: kept", 0, LogCount("[CZoneInstance] ZoneServer cleaned up Instance leujaoam_cleansing"));
        Leave();
        Check("timing out inside the area: the leave runs while the char is still in the run",
            $"leave:901:{LeujaoamCleansing} ", Lua("report(areaHits)"));
        Lua("local zone = xi.zones.Leujaoam_Sanctum zone.instances.leujaoam_cleansing = nil " +
            "zone.Zone.onTriggerAreaEnter, zone.Zone.onTriggerAreaLeave = savedZoneEnter, savedZoneLeave");
        // trigger area 901 off Leujaoam Sanctum again (no binding unregisters one; CZone's list, by reflection)
        const System.Reflection.BindingFlags NonPublic = System.Reflection.BindingFlags.NonPublic |
                                                         System.Reflection.BindingFlags.Instance;
        var zoneLuaZones = (System.Collections.IDictionary)typeof(ZoneHost).GetField("zoneLuaZones_", NonPublic)
            .GetValue(zone);
        var sanctumState = zoneLuaZones[LeujaoamSanctum];
        var areas = (List<TriggerArea>)sanctumState.GetType().GetField("TriggerAreas").GetValue(sanctumState);
        int removed = areas.RemoveAll(a => a.Id == 901);
        Check("trigger area 901 unregistered", "1/0", $"{removed}/{areas.Count(a => a.Id == 901)}");
        Tick();
        Check("completed and empty: cleaned up", 1, LogCount("[CZoneInstance] ZoneServer cleaned up Instance leujaoam_cleansing"));
        Check("the level restriction was not saved with the effects", "0",
            Scalar(db, "SELECT COUNT(*) FROM char_effects WHERE charid = ? AND effectid = 143", BoundValue.Of(CharId)));

        Console.WriteLine("coming back with no run: out to onInstanceLoadFailed's zone");
        Login();
        Check("placed in Caedarva Mire instead, the failure logged", $"{CaedarvaMire}/1",
            $"{c.Zone}/{LogCount("Failed to place Delver in Leujaoam_Sanctum (69). Placing them in that zone's instance exit area.")}");
        Check("no instance", "nil", Lua("report(tostring(player:getInstance()))"));

        Console.WriteLine("a run nobody enters: wiped on its time update, failed 180 s later");
        Lua("player:setLocalVar('AssaultCap', 0)");
        Lua($"player:createInstance({LeujaoamCleansing})");
        Tick();
        Lua("wipedRun = player:getInstance()");
        Check("a second run", LeujaoamCleansing.ToString(), Lua("report(wipedRun:getID())"));
        Tick(39_000);
        Check("before 40 s: no time update yet (nobody ever entered)", "0", Lua("report(wipedRun:getWipeTime())"));
        Tick(1_200);
        Check("the first time update at 40 s: the party counts as fallen", "40", Lua("report(wipedRun:getWipeTime())"));
        for (int i = 0; i < 190; i++) Tick(1_000);
        Check("180 s after the wipe: failed and cleaned up", "true/2",
            Lua("report(tostring(wipedRun:failed()))") + "/" + LogCount("[CZoneInstance] ZoneServer cleaned up Instance leujaoam_cleansing"));

        Console.WriteLine("instanceutils: the per-map assignment and the lazy load");
        int CountRows(string where, params BoundValue[] args)
        {
            var r = Sql(db, "SELECT COUNT(*) AS n FROM instance_list INNER JOIN zone_settings ON instance_zone = zone_settings.zoneid " + where, args);
            return r != null && r.Next() ? (int)r.Get<long>("n") : -1;
        }
        int allRows = CountRows("");
        Check("this process (no address): every instance_list row loaded", $"True/{allRows}/{allRows}",
            $"{allRows > 0}/{zone.LoadedInstanceCount}/{zone.ManagedInstanceCount}");
        T Private<T>(ZoneHost z, string name, params object[] args) =>
            (T)typeof(ZoneHost).GetMethod(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(z, args);
        ulong Address(string ip, ushort port)
        {
            var o = ip.Split('.');
            uint n = uint.Parse(o[0]) | uint.Parse(o[1]) << 8 | uint.Parse(o[2]) << 16 | uint.Parse(o[3]) << 24;
            return MapIpp.Make(n, port);
        }
        var other = new ZoneHost(db, loader, dataDir, true, _ => { }) { ProcessIpp = Address("10.9.9.9", 1) };
        other.InitializeInstances();
        Check("a process at an address no zone has: none of them, Leujaoam Cleansing not valid", "0/0/False",
            $"{other.LoadedInstanceCount}/{other.ManagedInstanceCount}/{Private<bool>(other, "IsValidInstanceId", LeujaoamCleansing)}");
        var zr = Sql(db, "SELECT zoneip, zoneport FROM zone_settings WHERE zoneid = ?", BoundValue.Of(LeujaoamSanctum));
        zr.Next();
        string sanctumIp = zr.Get<string>("zoneip");
        ushort sanctumPort = zr.Get<ushort>("zoneport");
        int sanctumRows = CountRows("WHERE zoneip = ? AND zoneport = ?", BoundValue.Of(sanctumIp), BoundValue.Of(sanctumPort));
        var assigned = new ZoneHost(db, loader, dataDir, true, _ => { }) { ProcessIpp = Address(sanctumIp, sanctumPort) };
        assigned.InitializeInstances();
        Check("the process at Leujaoam Sanctum's address: the instances of the zones it serves, the cleansing among them",
            $"{sanctumRows}/True", $"{assigned.LoadedInstanceCount}/{Private<bool>(assigned, "IsValidInstanceId", LeujaoamCleansing)}");
        var lazyLog = new List<string>();
        var lazy = new ZoneHost(db, loader, dataDir, true, lazyLog.Add) { LazyZones = true };
        lazy.InitializeInstances();
        Check("lazy zones: nothing loaded, every row managed", $"0/{allRows}", $"{lazy.LoadedInstanceCount}/{lazy.ManagedInstanceCount}");
        Check("a managed instance is valid before it loads, an unknown one is not", "True/False/0",
            $"{Private<bool>(lazy, "IsValidInstanceId", LeujaoamCleansing)}/{Private<bool>(lazy, "IsValidInstanceId", (ushort)9999)}/{lazy.LoadedInstanceCount}");
        var lazyData = Private<object>(lazy, "InstanceDataOf", LeujaoamCleansing);
        string Field(object o, string name) => o.GetType().GetField(name).GetValue(o)?.ToString();
        Check("its first lookup loads it, alone", $"leujaoam_cleansing/{LeujaoamSanctum}/1",
            $"{Field(lazyData, "Name")}/{Field(lazyData, "Zone")}/{lazy.LoadedInstanceCount}");
        Check("an unknown id: an empty one, nothing more loaded", "/1", $"{Field(Private<object>(lazy, "InstanceDataOf", (ushort)9999), "Name")}/{lazy.LoadedInstanceCount}");

        Check("GetMobByID without the instance warned once", 1, log.RemoveAll(l => l == $"luautils::GetMobByID Mob doesn't exist ({FirstWorm})"));
        var errors = log.FindAll(l => (l.StartsWith("lua:") && !l.Contains("global scripts")) || l.Contains("luautils::") || l.Contains("[lua]") && l.Contains("error"));
        Check("no script errors", "", string.Join(" | ", errors));
    }
}
