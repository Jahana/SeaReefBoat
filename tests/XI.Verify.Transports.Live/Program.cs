using System;
using System.Collections.Generic;
using System.IO;
using XI.Common;
using XI.Map;
using XI.Ipc;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Transports.Live -- live gate for the ships (ZoneHost.Transports.cs: zone.yaml's transport schedules,
// ShipHandler::InitializeShips and tick, Ship's phases, the door, the departure collecting the riders in the boarding
// area, hiding, SpawnTransport on GAMEOK, the crossing zone's DisembarkAll and onTransportVoyageEnd) on the Bastok -
// Jeuno airship: Port Bastok's berth and boarding area 315, and the Bastok-Jeuno Airship it crosses, which the
// Port Jeuno leg crosses too. The lifts (ZoneHost.Elevators.cs: addElevator, the timed Metalworks lift's legs and doors,
// the Davoi lever lift run by RunElevator, GetElevatorState) and the transport name's timestamp and travel bytes.
// Two chars, with the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 993800, RiderId = 993801;
    private const ushort PortBastok = 236, BastokJeunoAirship = 224;
    private const uint Ship = 17743971;
    private const ushort Metalworks = 237, Davoi = 149;
    private const uint MetalworksLift = 17748035, DavoiLift = 17387996;

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
            Sql(db, $"DELETE FROM `{t}` WHERE charid BETWEEN 993800 AND 993899");
        Sql(db, "DELETE FROM chars WHERE charid BETWEEN 993800 AND 993899");
        Sql(db, "DELETE FROM accounts WHERE id BETWEEN 993800 AND 993899");
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
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
        void Char(uint id, string name, ushort zone, float x, float y, float z)
        {
            Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of("ship" + id), BoundValue.Of("x"), BoundValue.Of((byte)1));
            Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(name), BoundValue.Of(zone), BoundValue.Of(zone),
                BoundValue.Of(x), BoundValue.Of(y), BoundValue.Of(z), BoundValue.Of((byte)1), BoundValue.Of((byte)0));
            Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(0u), BoundValue.Of((ushort)0));
            Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 1500, 50, 1, 75)", BoundValue.Of(id));
            Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 75)", BoundValue.Of(id));
            Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(id));
            Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(id));
        }
        // inside Port Bastok's boarding area 315 (-40.3..-12.3, 3.1..8.9, -93.4..-61.2), and aboard the airship
        Char(CharId, "Boarder", PortBastok, -26.0f, 5.0f, -77.0f);
        Char(RiderId, "Rider", BastokJeunoAirship, 0.0f, -10.0f, 0.0f);

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        // the Port Bastok run's cycle (offset 2, every 864) starts at Vana'diel second C; t is how far into it
        long cycleStart = 864L * 50_000 + 2;
        long t = 50;
        zone.UnixNow = () => VanaTime.EpochUnixSec + cycleStart + t;
        // the lifts' millisecond clock, from a whole number of the Metalworks lift's 18.25 s legs
        long liftBase = 18_250L * 1_000_000, liftMs = 1000;
        zone.UnixNowMs = () => VanaTime.EpochUnixSec * 1000 + liftBase + liftMs;
        zone.VanaTicksNow = () => 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        var queue = new Queue<IIpcMessage>();
        zone.SendIpc = m => queue.Enqueue(m);
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);
        lua = scripts.Lua;
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        void Tick(int n = 1)
        {
            for (int i = 0; i < n; i++)
            {
                now += 400;
                zone.Tick();
            }
        }

        Console.WriteLine("InitializeShips: every zone's runs");
        zone.InitializeShips();
        var names = string.Join(",", zone.TransportNames);
        Check("the airships, ferries and barges registered", true,
            zone.TransportNames.Count >= 10 && names.Contains("bastok_jeuno_airship") && names.Contains("jeuno_bastok_airship"));
        bool threw = false;
        try { zone.InitializeShips(); } catch (InvalidOperationException) { threw = true; }
        Check("a second InitializeShips: refused", true, threw);

        var pb = zone.ZoneInfoOf(PortBastok);
        var ship = pb.NpcById[Ship];
        var door = pb.Npcs.Find(n => n.Name == "_6ku");
        Check("the ship: always relevant, at its berth, GroundFlag | MovTime 1 (it never leaves)", "True/-36.458/6.365/-77.322/128/32769",
            $"{ship.AlwaysRelevant}/{ship.X:F3}/{ship.Y:F3}/{ship.Z:F3}/{ship.Rotation}/{ship.Moving}");

        // the zone scripts' hooks report instead of starting the cutscenes
        Lua("xi.zones.Port_Bastok.Zone.onTransportEvent = function(player, prev, name) report('depart:' .. player:getName() .. ':' .. prev .. ':' .. name) end\n" +
            "xi.zones['Bastok-Jeuno_Airship'].Zone.onTransportEvent = function(player, prev, name) report('ashore:' .. player:getName() .. ':' .. prev .. ':' .. name) end\n" +
            "xi.zones['Bastok-Jeuno_Airship'].Zone.onTransportVoyageEnd = function(z) report('voyageEnd:' .. z:getID()) end");

        Console.WriteLine("docked (t = 50): playing its arrival from its start, the door open");
        Tick();
        Check("animation 18, stamped at the arrival's start, shown, door open", $"18/{cycleStart}/0/8",
            $"{ship.Animation}/{ship.LocalVars["TransportTimestamp"]}/{ship.Status}/{door.Animation}");

        Console.WriteLine("the boarder logs in inside area 315, the rider aboard the airship");
        var s = new MapSession { CharId = CharId, AccountId = CharId };
        zone.OnLogin(s);
        var c = (CharData)s.Char;
        var rs = new MapSession { CharId = RiderId, AccountId = RiderId };
        zone.OnLogin(rs);
        var rider = (CharData)rs.Char;
        void Send(MapSession session, ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(session, opcode, new BasicPacket(buf));
            zone.AfterParse(session);
        }
        foreach (var (session, id) in new[] { (s, CharId), (rs, RiderId) })
        {
            Send(session, 0x00A, 92, buf => BitConverter.GetBytes(id).CopyTo(buf, 12));
            session.PacketList.Clear();
            Send(session, 0x00C, 12);
        }
        BasicPacket shipSpawn = null;
        foreach (var p in s.PacketList)
            if (p.Buffer[0] == 0x0E && BitConverter.ToUInt32(p.Buffer, 4) == Ship) shipSpawn = p;
        Check("GAMEOK: the always-relevant ship spawned for the boarder (SpawnTransport)", true, shipSpawn != null);
        Check("its name carries the TransportTimestamp (the arrival's start)", $"{cycleStart}",
            shipSpawn == null ? "none" : $"{BitConverter.ToUInt32(shipSpawn.Buffer, 0x34 + 4)}");
        (c.X, c.Y, c.Z) = (-26.0f, 5.0f, -77.0f);
        // past xi.player.onGameIn's 2.5 s ZoningIn, which keeps a char out of trigger areas
        got.Clear();
        Tick(8);
        Check("while boarding neither leg carries anyone: the rider is put ashore every tick", 8,
            got.FindAll(g => g == $"ashore:Rider:{BastokJeunoAirship}:").Count);
        Check("no voyage end at startup (the baseline)", false, got.Exists(g => g.StartsWith("voyageEnd:")));
        Check("in boarding area 315", true, Lua($"report(tostring(GetPlayerByID({CharId}):isPlayerInTriggerArea(315)))") == "true");

        Console.WriteLine("closing (t = 174): the door swings shut");
        t = 174;
        s.PacketList.Clear();
        got.Clear();
        Tick();
        bool doorSent = false;
        foreach (var p in s.PacketList)
            if (p.Buffer[0] == 0x0E && BitConverter.ToUInt32(p.Buffer, 4) == door.Id) doorSent = true;
        Check("door closed, its update to the boarder", "9/True", $"{door.Animation}/{doorSent}");
        Check("boarding over (172): the Bastok leg carries, nobody put ashore", false, got.Exists(g => g.StartsWith("ashore:")));

        Console.WriteLine("departing (t = 180): the riders in area 315 collected");
        t = 180;
        got.Clear();
        Tick();
        Check("onTransportEvent(boarder, the airship zone, the run)", true, got.Contains($"depart:Boarder:{BastokJeunoAirship}:bastok_jeuno_airship"));
        Check("animation 19 from the departure's start", $"19/{cycleStart + 176}", $"{ship.Animation}/{ship.LocalVars["TransportTimestamp"]}");
        got.Clear();
        Tick();
        Check("once: no second pickup in the same phase", false, got.Exists(g => g.StartsWith("depart:")));
        Check("the crossing carries the Bastok leg: nobody put ashore", false, got.Exists(g => g.StartsWith("ashore:")));

        Console.WriteLine("out of sight (t = 213): hidden");
        t = 176 + 36;
        Tick();
        Check("still shown a second before the hide", (byte)0, ship.Status);
        t = 176 + 37;
        Tick();
        Check("hidden (Disappear)", (byte)2, ship.Status);

        Console.WriteLine("arrival in Jeuno (t = 441): the crossing empties, the voyage ends once");
        t = 441;
        got.Clear();
        Tick();
        Check("the rider put ashore", true, got.Contains($"ashore:Rider:{BastokJeunoAirship}:"));
        Check("onTransportVoyageEnd(the airship zone)", 1, got.FindAll(g => g == $"voyageEnd:{BastokJeunoAirship}").Count);
        got.Clear();
        Tick();
        Check("no second voyage end", 0, got.FindAll(g => g.StartsWith("voyageEnd:")).Count);

        Console.WriteLine("the next cycle (t = 864 + 10): arriving again, shown, the door shut");
        t = 864 + 10;
        Tick();
        Check("arriving: animation 18 from the new cycle, shown, door shut", $"18/{cycleStart + 864}/0/9",
            $"{ship.Animation}/{ship.LocalVars["TransportTimestamp"]}/{ship.Status}/{door.Animation}");

        Console.WriteLine("Metalworks' timed lift");
        var mw = zone.ZoneInfoOf(Metalworks);
        var mwLift = mw.NpcById[MetalworksLift];
        var mwLower = mw.Npcs.Find(n => n.Name == "_6lv");
        var mwUpper = mw.Npcs.Find(n => n.Name == "_6lu");
        Check("addElevator: platform and doors always relevant, travel 8, at the top (elevator_up), the upper door open",
            "True/True/True/8/10/9/8",
            $"{mwLift.AlwaysRelevant}/{mwLower.AlwaysRelevant}/{mwUpper.AlwaysRelevant}/{mwLift.LocalVars["TransportTravel"]}/{mwLift.Animation}/{mwLower.Animation}/{mwUpper.Animation}");
        // its first leg is lined up one period past the next boundary
        liftMs = 18_250 + 18_250 - 400;
        Tick();
        Check("still at the top before its leg", "10/8", $"{mwLift.Animation}/{mwUpper.Animation}");
        liftMs = 18_250 * 2;
        Tick();
        Check("the leg starts on the clock: the upper door shut, going down, stamped in whole seconds", $"11/9/{(liftBase + 36_500) / 1000}",
            $"{mwLift.Animation}/{mwUpper.Animation}/{mwLift.LocalVars["TransportTimestamp"]}");
        liftMs = 18_250 * 2 + 8_000;
        Tick();
        Check("8 s on: at the bottom, the lower door open", "8/9", $"{mwLower.Animation}/{mwUpper.Animation}");
        liftMs = 18_250 * 3;
        Tick();
        Check("the next leg: the lower door shut, going up", "10/9", $"{mwLift.Animation}/{mwLower.Animation}");

        Console.WriteLine("Davoi's lever lift");
        var dv = zone.ZoneInfoOf(Davoi);
        var dvLift = dv.NpcById[DavoiLift];
        var dvLower = dv.Npcs.Find(n => n.Name == "_453");
        var dvUpper = dv.Npcs.Find(n => n.Name == "_454");
        Check("reversed: elevator_down rests at the top, the upper door open", "1/9/8",
            Lua("report(GetElevatorState(xi.elevator.DAVOI_LIFT))") + $"/{dvLower.Animation}/{dvUpper.Animation}");
        Check("no such lift: -1", "-1", Lua("report(GetElevatorState(99))"));
        liftMs += 400;
        Tick(5);
        Check("a lever lift waits", "1", Lua("report(GetElevatorState(xi.elevator.DAVOI_LIFT))"));
        long pulled = liftMs;
        Lua("RunElevator(xi.elevator.DAVOI_LIFT)");
        Check("pulled: the standing door shuts at once", (byte)9, dvUpper.Animation);
        liftMs = pulled + 3_000;
        Tick();
        Check("the door delay (3.4 s): not yet moving", "1", Lua("report(GetElevatorState(xi.elevator.DAVOI_LIFT))"));
        liftMs = pulled + 3_400;
        Tick();
        Check("then descending, playing up (reversed)", "3/10", Lua("report(GetElevatorState(xi.elevator.DAVOI_LIFT))") + $"/{dvLift.Animation}");
        Lua("RunElevator(xi.elevator.DAVOI_LIFT)");
        Check("pulled again mid-leg: ignored", "3", Lua("report(GetElevatorState(xi.elevator.DAVOI_LIFT))"));
        liftMs = pulled + 3_400 + 4_000;
        Tick();
        Check("4 s on: at the bottom, the lower door open, waiting for the lever", "0/8",
            Lua("report(GetElevatorState(xi.elevator.DAVOI_LIFT))") + $"/{dvLower.Animation}");
        liftMs += 30_000;
        Tick();
        Check("and it stays there", "0", Lua("report(GetElevatorState(xi.elevator.DAVOI_LIFT))"));

        Check("no script errors", "", string.Join(" | ", log.FindAll(l => l.Contains("onTransport") || l.Contains("elevator"))));
    }

    private static readonly List<string> got = new List<string>();
    private static LuaState lua;

    private static string Lua(string code)
    {
        got.Clear();
        lua.DoString(code, "=verify_transports");
        return string.Join(",", got);
    }
}
