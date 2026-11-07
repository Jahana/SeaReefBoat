using System;
using System.Collections.Generic;
using System.IO;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.NpcPath.Live -- live gate for the NPC AI (ZoneHost.NpcAi.cs): every NPC's onSpawn as the zone loads
// (initNpcAi, setPos to the path's first point, pathThrough PATROL), the controller-less path tick (the step at the
// NPC's speed, a point's wait, the patrol looping back, the PATH listener, the update sent at most every 250 ms),
// pauseNPCPathing through clearPath(true) / continuePath and an event, wait, clearPath, pathTo, setBaseSpeed, onPath
// (Raminel's delivery), clearTargID and the refusals. Glenne and Raminel in Southern San d'Oria, with the real
// MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 990996;
    private const ushort SouthernSandoria = 230;
    private const uint Glenne = 17719328, Raminel = 17719326;

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
            lua.DoString($"local player = GetPlayerByID({CharId})\n" + code, "=verify_npcpath");
            return string.Join(",", got);
        }

        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("np" + CharId), BoundValue.Of("x"), BoundValue.Of((byte)1));
        // past its first login (the NEW_ADVENTURER title: no charCreate), having seen the RoV opening (Rhapsodies of
        // Vana'diel, a zone-in cutscene in the starting cities: Resonance current, the other logs as a new char has them),
        // standing by Glenne's beat, within talking distance of it
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        var missions = new byte[15 * 70];
        for (int i = 0; i <= 3; i++) BitConverter.GetBytes((ushort)0xFFFF).CopyTo(missions, i * 70);
        BitConverter.GetBytes((ushort)101).CopyTo(missions, 6 * 70);
        BitConverter.GetBytes((ushort)1).CopyTo(missions, 13 * 70);
        missions[13 * 70 + 6] = 1;
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,moghouse,nation,gmlevel,titles,missions) " +
                "VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Pather"), BoundValue.Of(SouthernSandoria), BoundValue.Of((ushort)0),
            BoundValue.Of(-124.0f), BoundValue.Of(-2.0f), BoundValue.Of(17.0f), BoundValue.Of(0u), BoundValue.Of((byte)0), BoundValue.Of((byte)0),
            BoundValue.Of(titles), BoundValue.Of(missions));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl, sjob, slvl) VALUES (?, 300, 50, 1, 30, 0, 0)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs SET charid = ?, unlocked = 2, war = 30", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(CharId));

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
        var info = zone.ZoneInfoOf(SouthernSandoria);
        var glenne = info.NpcById[Glenne];
        var raminel = info.NpcById[Raminel];
        List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);
        int UpdatesOf(NpcEntity n) => Of(0x00E).FindAll(p => BitConverter.ToUInt32(p.Buffer, 4) == n.Id).Count;
        void Advance(long ms)
        {
            for (long t = 0; t < ms; t += 100)
            {
                nowMs += 100;
                zone.Tick();
            }
        }
        string Pos(NpcEntity n) => $"{n.X:0.00}/{n.Y:0.00}/{n.Z:0.00}";
        // a position update where the char stands spawns the NPCs around it
        Send(0x015, 32, b =>
        {
            BitConverter.GetBytes(c.X).CopyTo(b, C2sPos.OffX);
            BitConverter.GetBytes(c.Y).CopyTo(b, C2sPos.OffZ);
            BitConverter.GetBytes(c.Z).CopyTo(b, C2sPos.OffY);
        });

        Console.WriteLine("onSpawn as the zone loads: Glenne's patrol between two points, 1 s at each");
        Check("at the first point, following its path", "-121.51/-2.00/14.49/True/True",
            $"{Pos(glenne)}/{glenne.PathFind != null}/{Lua($"report(tostring(GetNPCByID({Glenne}):isFollowingPath()))") == "true"}");
        Check("Raminel too (a 41-point beat)", "-138.44/-2.00/16.23/True", $"{Pos(raminel)}/{raminel.PathFind?.IsFollowingPath == true}");
        Check("Glenne is spawned for the char", true, c.SpawnNpcList.Contains(Glenne));
        Lua($"pathPoints = 0 GetNPCByID({Glenne}):addListener('PATH', 'VERIFY_PATH', function(npc) pathPoints = pathPoints + 1 end)");
        int PathPoints() => int.Parse(Lua("report(pathPoints)"));

        // the ms until Glenne's x changes (its logic ticks come every 400)
        long UntilMoved(long maxMs)
        {
            float x = glenne.X;
            for (long t = 100; t <= maxMs; t += 100)
            {
                nowMs += 100;
                zone.Tick();
                if (glenne.X != x) return t;
            }
            return -1;
        }

        // the first tick arrives (the wait set), two wait, the fourth moves on to the next point and the fifth steps;
        // OnPoint holds through the wait, so the PATH listener fires on each of the first four. Speed 13: 13 / 17 * 0.4
        // = 0.306 yalms a logic tick, 17 of them to the second point 5.19 off
        float x0 = glenne.X;
        long firstStep = UntilMoved(3000);
        Check("waits out the first point: a step on the fifth tick, PATH on the four before", "True/4",
            $"{firstStep > 1600 && firstStep <= 2000}/{PathPoints()}");
        Check("walks west, one step a logic tick", "0.31", $"{x0 - glenne.X:0.00}");
        s.PacketList.Clear();
        Advance(400);
        Check("the step sent as an entity update", 1, UpdatesOf(glenne));
        s.PacketList.Clear();
        Advance(200);
        Check("nothing between logic ticks", 0, UpdatesOf(glenne));
        Advance(6200);
        Check("at the second point: y carried over, PATH again", "-126.71/-2.00/14.49/True", $"{Pos(glenne)}/{PathPoints() > 4}");
        Advance(8000);
        Check("the patrol loops back to the first point", "-121.51/-2.00/14.49", Pos(glenne));

        Console.WriteLine("pauseNPCPathing: clearPath(true) / continuePath");
        // off the point again, a step west
        Advance(1600);
        Check("off the first point", true, glenne.X < -121.6f);
        Lua($"GetNPCByID({Glenne}):clearPath(true)");
        float paused = glenne.X;
        Advance(2000);
        Check("paused: not a step, still following", $"{paused:0.00}/true",
            $"{glenne.X:0.00}/{Lua($"report(tostring(GetNPCByID({Glenne}):isFollowingPath()))")}");
        Lua($"GetNPCByID({Glenne}):continuePath()");
        Advance(400);
        Check("continued: a step again", true, glenne.X < paused - 0.2f);

        Console.WriteLine("talking to it: the event pauses the path, its end lets it go on");
        Lua($"GetNPCByID({glenne.Id}):addListener('ON_TRIGGER', 'T_TRIG', function(p, npc) npc:setLocalVar('tTrig', p:getID()) end)");
        s.PacketList.Clear();
        Send(0x01A, 28, b =>
        {
            BitConverter.GetBytes(glenne.Id).CopyTo(b, 4);
            BitConverter.GetBytes(glenne.TargId).CopyTo(b, C2sAction.OffActIndex);
            BitConverter.GetBytes((ushort)ActionId.Talk).CopyTo(b, C2sAction.OffActionID);
        });
        float talked = glenne.X;
        Advance(2000);
        Check("in an event: held", $"True/{talked:0.00}", $"{c.IsInEvent}/{glenne.X:0.00}");
        Check("ON_TRIGGER: the NPC's listener hears the char", $"{CharId}", $"{glenne.LocalVars.GetValueOrDefault("tTrig")}");
        int eventId = c.IsInEvent ? c.CurrentEvent.EventId : 0;
        Send(0x05B, 20, b =>
        {
            BitConverter.GetBytes(glenne.Id).CopyTo(b, 4);
            BitConverter.GetBytes(glenne.TargId).CopyTo(b, 0x0C);
            BitConverter.GetBytes((ushort)eventId).CopyTo(b, 0x12);
        });
        Advance(400);
        Check("the event over: no pause left", "False/False", $"{c.IsInEvent}/{glenne.LocalVars.ContainsKey("pauseNPCPathing")}");
        Advance(800);
        Check("walking again", true, Math.Abs(glenne.X - talked) > 0.2f);

        Console.WriteLine("wait: the inactive state holds the path for its duration");
        Advance(400);
        Lua($"GetNPCByID({Glenne}):wait(1000)");
        float waited = glenne.X;
        Advance(800);
        Check("held", $"{waited:0.00}/True", $"{glenne.X:0.00}/{glenne.Inactive != null}");
        // the state ends on the first logic tick past the second, which still holds the path; the next one steps
        Advance(1200);
        Check("then the state ends and the path goes on", "True/True", $"{glenne.Inactive == null}/{Math.Abs(glenne.X - waited) > 0.2f}");

        Console.WriteLine("clearPath, pathTo, setBaseSpeed");
        Lua($"GetNPCByID({Glenne}):clearPath()");
        float cleared = glenne.X;
        Advance(1200);
        Check("cleared: no path, standing", $"false/{cleared:0.00}", $"{Lua($"report(tostring(GetNPCByID({Glenne}):isFollowingPath()))")}/{glenne.X:0.00}");
        Lua($"GetNPCByID({Glenne}):setBaseSpeed(85)");
        Check("setBaseSpeed: an NPC's speed", "85", Lua($"report(GetNPCByID({Glenne}):getSpeed())"));
        Lua($"GetNPCByID({Glenne}):pathTo(-124, -2, 20)");
        Advance(400);
        // 85 / 17 * 0.4 = 2 yalms a tick toward a point 5.6 off
        Check("pathTo: following, 2 yalms the first tick", "True/2.00", $"{glenne.PathFind.IsFollowingPath}/{Math.Sqrt(Math.Pow(glenne.X - cleared, 2) + Math.Pow(glenne.Z - 14.49, 2)):0.00}");
        Advance(1200);
        // Clear keeps the patrol flags (the C++'s too), so the finished path loops on its one point
        Check("there; the old patrol's flag keeps it looping on the point", "-124.00/20.00/True",
            $"{glenne.X:0.00}/{glenne.Z:0.00}/{glenne.PathFind.IsFollowingPath}");

        Console.WriteLine("onPath: Raminel's delivery at its 39th point, the reset at its last");
        Lua($"local r = GetNPCByID({Raminel}) r:setPos(-139.0, -2.0, 15.6) r:pathThrough({{ {{ x = -140.205811, y = -2.0, z = 15.668728 }} }}, xi.path.flag.COORDS)");
        Advance(1200);
        Check("onPath at the point: delivered", "1", Lua($"report(GetNPCByID({Raminel}):getLocalVar('delivered'))"));
        Lua($"local r = GetNPCByID({Raminel}) r:pathThrough({{ {{ x = -139.296539, y = -2.0, z = 16.786556 }} }}, xi.path.flag.COORDS)");
        Advance(1200);
        Check("onPath at the last point: the delivery reset", "0", Lua($"report(GetNPCByID({Raminel}):getLocalVar('delivered'))"));

        Console.WriteLine("clearTargID and the refusals");
        raminel.LookAt = 7;
        Lua($"GetNPCByID({Raminel}):clearTargID()");
        Check("clearTargID: the NPC faces nothing", 0, raminel.LookAt);
        var plain = info.Npcs.Find(n => n.PathFind == null && n.Status == NpcEntity.StatusNormal);
        Check("pathThrough without initNpcAi: false and a warning", "false/True",
            $"{Lua($"report(tostring(GetNPCByID({plain.Id}):pathThrough({{ -1, -2, 3 }})))")}/{log.Exists(l => l.Contains("Invalid entity type calling function"))}");
        Lua("player:initNpcAi()");
        Check("initNpcAi on a char: a warning", true, log.Exists(l => l.Contains("Attempting to init NPC AI for invalid entity type (Pather)")));
        Lua($"GetNPCByID({plain.Id}):initNpcAi()");
        Check("initNpcAi: a path, not following yet", "True/false",
            $"{plain.PathFind != null}/{Lua($"report(tostring(GetNPCByID({plain.Id}):isFollowingPath()))")}");

        Check("no script errors", "", string.Join(" | ", log.FindAll(l => l.StartsWith("lua:") && !l.Contains("global scripts"))));
    }
}
