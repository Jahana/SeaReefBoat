using System;
using System.Collections.Generic;
using System.Linq;
using XI.Ipc;
using XI.Login;
using XI.Persistence;
using XI.World;

// XI.Verify.Conquest.Live -- live gate for ConquestSystem / ConquestData / DailyTally against the real MariaDB
// (docker xi-live-db, host:3307, xidb). conquest_system is snapshotted and restored.
// Exit codes: 0 = pass; 1 = a check failed; 3 = DB not reachable.
internal static class Program
{
    private static int _pass, _fail;

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static void Sql(IDatabase db, string q, params object[] a) => LoginDb.PreparedStmt(db, q, a);

    private static readonly string[] Cols =
        { "sandoria_influence", "bastok_influence", "windurst_influence", "mob_kills", "player_homepoints", "region_control", "region_control_prev" };

    private static string Row(IDatabase db, int region)
    {
        var r = LoginDb.PreparedStmt(db, "SELECT * FROM conquest_system WHERE region_id = ?", region);
        r.Next();
        return string.Join(",", Cols.Select(c => r.Get<int>(c)));
    }

    private static int Count(IDatabase db, uint charId)
    {
        var r = LoginDb.PreparedStmt(db, "SELECT COUNT(*) AS n FROM char_vars WHERE charid = ? AND varname = 'gobbieBoxUsed'", charId);
        r.Next();
        return r.Get<int>("n");
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
        catch (Exception ex) { Console.WriteLine($"DB not reachable ({ex.GetType().Name}: {ex.Message})"); return 3; }

        var rs = LoginDb.PreparedStmt(db, "SELECT region_id FROM conquest_system ORDER BY region_id");
        var ids = new List<int>();
        while (rs.Next()) ids.Add(rs.Get<int>("region_id"));
        var snap = ids.Select(id => Row(db, id)).ToList();

        try { Run(db, ids.Count); }
        catch (Exception ex) { _fail++; Console.WriteLine($"FAIL exception {ex}"); }
        finally
        {
            for (int i = 0; i < ids.Count; i++)
            {
                var v = snap[i].Split(',').Select(int.Parse).ToArray();
                Sql(db, "UPDATE conquest_system SET sandoria_influence=?, bastok_influence=?, windurst_influence=?, mob_kills=?, player_homepoints=?, region_control=?, region_control_prev=? WHERE region_id=?",
                    v[0], v[1], v[2], v[3], v[4], v[5], v[6], ids[i]);
            }
            Sql(db, "DELETE FROM char_vars WHERE charid = ?", 990450u);
        }

        Console.WriteLine($"{_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    private static void Run(IDatabase db, int regions)
    {
        Check("conquest_system region rows", 19, regions);

        // --- CalculateBeastmenInfluence ---
        var inf = new Influence { SandoriaInfluence = 100, BastokInfluence = 200, WindurstInfluence = 300, MobKills = 600, PlayerHomepoints = 0 };
        Check("beast: no homepoints", 0, ConquestData.CalculateBeastmenInfluence(Region.Ronfaure, inf));
        inf.PlayerHomepoints = 3;
        Check("beast: general weight 10", 30, ConquestData.CalculateBeastmenInfluence(Region.Ronfaure, inf));
        Check("beast: semi weight 25", 75, ConquestData.CalculateBeastmenInfluence(Region.Fauregandi, inf));
        Check("beast: stronghold weight 50", 150, ConquestData.CalculateBeastmenInfluence(Region.Norvallen, inf));
        Check("beast: unknown region weight 10", 30, ConquestData.CalculateBeastmenInfluence(Region.Unknown, inf));
        inf.MobKills = 0;
        Check("beast: no kills -> max", ConquestData.MaxBeastmenInfluence, ConquestData.CalculateBeastmenInfluence(Region.Ronfaure, inf));
        inf.MobKills = 1; inf.PlayerHomepoints = 1_000_000;
        Check("beast: capped", ConquestData.MaxBeastmenInfluence, ConquestData.CalculateBeastmenInfluence(Region.Norvallen, inf));

        // --- payload codecs ---
        var a = ConquestAddInfluencePoints.FromBytes(new ConquestAddInfluencePoints { Points = -5, Nation = 2, Region = 7 }.ToBytes());
        Check("codec addInfluence", "-5,2,7", $"{a.Points},{a.Nation},{a.Region}");
        var c = ConquestAddCounter.FromBytes(new ConquestAddCounter { Count = 9, Region = 4 }.ToBytes());
        Check("codec addCounter", "9,4", $"{c.Count},{c.Region}");
        var iu = ConquestInfluenceUpdate.FromBytes(new ConquestInfluenceUpdate
        {
            ShouldUpdateZones = true,
            Influences = { new Influence { SandoriaInfluence = 1, BastokInfluence = 2, WindurstInfluence = 3, MobKills = 4, PlayerHomepoints = 5 } },
        }.ToBytes());
        var i0 = iu.Influences[0];
        Check("codec influenceUpdate", "True,1,1,2,3,4,5",
            $"{iu.ShouldUpdateZones},{iu.Influences.Count},{i0.SandoriaInfluence},{i0.BastokInfluence},{i0.WindurstInfluence},{i0.MobKills},{i0.PlayerHomepoints}");
        var rc = ConquestRegionControlUpdate.FromBytes(new ConquestRegionControlUpdate { RegionControls = { new RegionControl { Current = 3, Prev = 5 } } }.ToBytes());
        Check("codec regionControl", "1,3,5", $"{rc.RegionControls.Count},{rc.RegionControls[0].Current},{rc.RegionControls[0].Prev}");
        Check("codec garbage -> null", true, ConquestAddCounter.FromBytes(new byte[] { 0xFF, 0x00 }) == null);

        // --- DB operations ---
        Sql(db, "UPDATE conquest_system SET sandoria_influence=0, bastok_influence=0, windurst_influence=0, mob_kills=0, player_homepoints=0, region_control=5, region_control_prev=5");
        var sent = new List<IIpcMessage>();
        var cs = new ConquestSystem(db, sent.Add);

        cs.AddMobKills(7, 2); cs.AddMobKills(3, 2);
        cs.AddPlayerHomepoints(4, 2);
        Check("mob kills/homepoints accumulate", "0,0,0,10,4,5,5", Row(db, 2));

        cs.UpdateInfluencePoints(50, Nation.Bastok, 2);       // all zero -> 1x; 50/10 = 5
        Check("influence 1x, /10", "0,5,0,10,4,5,5", Row(db, 2));
        cs.UpdateInfluencePoints(20, Nation.Sandoria, 2);     // 0 < 5/2 -> 3x: 60/10 = 6
        Check("influence 3x when < half of first", "6,5,0,10,4,5,5", Row(db, 2));
        cs.UpdateInfluencePoints(1, Nation.Windurst, 2);      // 3x: 3/10 = 0 -> min 1
        Check("influence min gain 1", "6,5,1,10,4,5,5", Row(db, 2));
        cs.UpdateInfluencePoints(100, Nation.Bastok, 2);      // 5 >= 6/2 but < 6 -> 2x: 200/10 = 20
        Check("influence 2x when < first", "6,25,1,10,4,5,5", Row(db, 2));
        Check("invalid region ignored", false, cs.UpdateInfluencePoints(10, Nation.Bastok, Region.Unknown));
        Check("invalid nation ignored", false, cs.UpdateInfluencePoints(10, Nation.Beastmen, 2));
        Check("row unchanged after ignored", "6,25,1,10,4,5,5", Row(db, 2));

        Sql(db, "UPDATE conquest_system SET sandoria_influence=?, bastok_influence=100, windurst_influence=50 WHERE region_id=3", int.MaxValue / 100 - 5);
        cs.UpdateInfluencePoints(1000, Nation.Sandoria, 3);   // 1x -> 100; room 5; overflow 95
        Check("overflow drains others", $"{int.MaxValue / 100},5,0,0,0,5,5", Row(db, 3));

        sent.Clear();
        cs.UpdateHourlyConquest();
        var ev = (ConquestEvent)sent.Single();
        Check("hourly type", (byte)ConquestMessage.W2M_BroadcastInfluencePoints, ev.TypeField);
        var upd = ConquestInfluenceUpdate.FromBytes(ev.Payload);
        Check("hourly zones+rows", $"True,{regions}", $"{upd.ShouldUpdateZones},{upd.Influences.Count}");
        var u2 = upd.Influences[2];
        Check("hourly row 2", "6,25,1,10,4", $"{u2.SandoriaInfluence},{u2.BastokInfluence},{u2.WindurstInfluence},{u2.MobKills},{u2.PlayerHomepoints}");
        sent.Clear();
        cs.UpdateVanaHourlyConquest();
        Check("vana hourly no zones", false, ConquestInfluenceUpdate.FromBytes(((ConquestEvent)sent.Single()).Payload).ShouldUpdateZones);

        var ipp = new Ipp(1, 2);
        cs.HandleMessage((byte)ConquestMessage.M2W_AddMobKills, new IppPayload(ipp, new ConquestAddCounter { Count = 5, Region = 2 }.ToBytes()));
        cs.HandleMessage((byte)ConquestMessage.M2W_AddPlayerHomepoints, new IppPayload(ipp, new ConquestAddCounter { Count = 1, Region = 2 }.ToBytes()));
        cs.HandleMessage((byte)ConquestMessage.M2W_AddInfluencePoints, new IppPayload(ipp, new ConquestAddInfluencePoints { Points = 10, Nation = Nation.Windurst, Region = 2 }.ToBytes()));
        // windurst 1 < 25/2 -> 3x: 30/10 = 3 -> 4
        Check("handler dispatch", "6,25,4,15,5,5,5", Row(db, 2));
        Check("handler garbage tolerated", true, cs.HandleMessage((byte)ConquestMessage.M2W_AddMobKills, new IppPayload(ipp, new byte[] { 0xFF })));
        Check("handler unknown type", false, cs.HandleMessage(99, new IppPayload(ipp, Array.Empty<byte>())));
        sent.Clear();
        cs.HandleMessage((byte)ConquestMessage.M2W_GM_ConquestUpdate, new IppPayload(ipp, Array.Empty<byte>()));
        Check("GM conquest update broadcasts zones", true, ConquestInfluenceUpdate.FromBytes(((ConquestEvent)sent.Single()).Payload).ShouldUpdateZones);

        // weekly: region 2 -> beast = 10*5*(6+25+4)/15 = 116 > 25 -> beastmen. region 3 -> sandoria.
        sent.Clear();
        cs.UpdateWeekConquest();
        Check("weekly: first msg start", (byte)ConquestMessage.W2M_WeeklyUpdateStart, ((ConquestEvent)sent[0]).TypeField);
        Check("weekly: msg count", 3, sent.Count);
        Check("weekly: influence msg no zones", false, ConquestInfluenceUpdate.FromBytes(((ConquestEvent)sent[1]).Payload).ShouldUpdateZones);
        Check("weekly: end msg type", (byte)ConquestMessage.W2M_WeeklyUpdateEnd, ((ConquestEvent)sent[2]).TypeField);
        Check("weekly region 2 reset + beastmen control", "0,0,0,0,0,3,5", Row(db, 2));
        Check("weekly region 3 reset + sandoria control", "0,0,0,0,0,0,5", Row(db, 3));
        var ctl = ConquestRegionControlUpdate.FromBytes(((ConquestEvent)sent[2]).Payload);
        Check("weekly controls payload", $"{regions},3,0", $"{ctl.RegionControls.Count},{ctl.RegionControls[2].Current},{ctl.RegionControls[3].Current}");
        cs.UpdateWeekConquest();
        Check("second weekly: all zero -> neutral, prev kept", "0,0,0,0,0,5,3", Row(db, 2));

        // --- daily tally ---
        const uint charId = 990450;
        Sql(db, "DELETE FROM char_vars WHERE charid = ?", charId);
        Sql(db, "INSERT INTO char_vars (charid, varname, value) VALUES (?, 'gobbieBoxUsed', 1)", charId);
        var logs = new List<string>();
        DailyTally.UpdateDailyTallyPoints(db, new DailyTallySettings { Enable = false }, logs.Add);
        Check("daily tally disabled: no-op", 1, Count(db, charId));
        DailyTally.UpdateDailyTallyPoints(db, new DailyTallySettings { Enable = true, Limit = 100000, Amount = 10 }, logs.Add);
        Check("daily tally enabled: gobbieBoxUsed cleared", 0, Count(db, charId));
        Check("daily tally logs", true, logs.Contains("Distributed daily tally points"));

        RunTimeServer(db, regions);
    }

    private static long Utc(int y, int mo, int d, int h, int mi, int sec, int ms = 0) =>
        new DateTimeOffset(y, mo, d, h, mi, sec, ms, TimeSpan.Zero).ToUnixTimeMilliseconds();

    private static void RunTimeServer(IDatabase db, int regions)
    {
        // JST helpers: 2026-10-18 15:00 UTC == Monday 2026-10-19 00:00 JST
        Check("jst hour midnight", 0u, TimeServer.JstHour(Utc(2026, 10, 18, 15, 0, 0)));
        Check("jst weekday monday", 1u, TimeServer.JstWeekday(Utc(2026, 10, 18, 15, 0, 0)));
        Check("jst weekday sunday before midnight", 0u, TimeServer.JstWeekday(Utc(2026, 10, 18, 14, 59, 59)));
        Check("jst hour 23", 23u, TimeServer.JstHour(Utc(2026, 10, 18, 14, 59, 59)));
        Check("jst weekday saturday", 6u, TimeServer.JstWeekday(Utc(2026, 10, 17, 3, 0, 0)));

        var sent = new List<IIpcMessage>();
        var cs = new ConquestSystem(db, sent.Add);
        var logs = new List<string>();
        long now = Utc(2026, 10, 18, 14, 59, 59);
        var ts = new TimeServer(cs, db, new DailyTallySettings { Enable = false }, () => now, logs.Add);

        int Weekly() => sent.Count(m => ((ConquestEvent)m).TypeField == (byte)ConquestMessage.W2M_WeeklyUpdateStart);
        int Hourly() => sent.Count(m =>
        {
            var e = (ConquestEvent)m;
            return e.TypeField == (byte)ConquestMessage.W2M_BroadcastInfluencePoints && ConquestInfluenceUpdate.FromBytes(e.Payload).ShouldUpdateZones;
        });

        ts.Tick(); // 23:59:59 JST Sunday, first tick only arms the markers
        Check("first tick: no hourly/weekly", "0,0", $"{Hourly()},{Weekly()}");

        now = Utc(2026, 10, 18, 15, 0, 0, 500); // Monday 00:00 JST -> weekly + daily
        ts.Tick();
        Check("monday midnight: weekly once, no plain hourly", "0,1", $"{Hourly()},{Weekly()}");
        Check("monday midnight: daily logged", true, logs.Any(l => l.StartsWith("Daily tick")) && logs.Any(l => l.StartsWith("Weekly tick")));
        now += 1000; ts.Tick();
        Check("same hour again: nothing", "0,1", $"{Hourly()},{Weekly()}");

        logs.Clear();
        now = Utc(2026, 10, 19, 15, 0, 0, 100); // Tuesday 00:00 JST -> daily + plain hourly
        ts.Tick();
        Check("tuesday midnight: hourly, no weekly", "1,1", $"{Hourly()},{Weekly()}");
        Check("tuesday midnight: daily but not weekly log", "True,False", $"{logs.Any(l => l.StartsWith("Daily tick"))},{logs.Any(l => l.StartsWith("Weekly tick"))}");

        logs.Clear();
        now = Utc(2026, 10, 19, 16, 0, 0, 100); // 01:00 JST
        ts.Tick();
        Check("01:00 JST: hourly only", "2,False", $"{Hourly()},{logs.Any(l => l.StartsWith("Daily tick"))}");
        Check("01:00 JST: no 2-hour tick", false, logs.Any(l => l.StartsWith("2-hour tick")));
        now = Utc(2026, 10, 19, 17, 0, 0, 100); // 02:00 JST
        ts.Tick();
        Check("02:00 JST: 2-hour but not 4-hour", "True,False", $"{logs.Any(l => l.StartsWith("2-hour tick"))},{logs.Any(l => l.StartsWith("4-hour tick"))}");
        now = Utc(2026, 10, 19, 19, 0, 0, 100); // 04:00 JST
        ts.Tick();
        Check("04:00 JST: 4-hour tick", true, logs.Any(l => l.StartsWith("4-hour tick")));

        // Vana'diel hourly: 10 minutes of 2400ms ticks inside one earth hour (no earth-hourly tick fires)
        sent.Clear();
        var ts2 = new TimeServer(cs, db, new DailyTallySettings(), () => now, null);
        long t0 = Utc(2026, 10, 19, 17, 0, 5);
        now = t0;
        ts2.Tick();
        long last = t0;
        for (int i = 1; i <= 250; i++) { now = t0 + i * 2400L; last = now; ts2.Tick(); }
        long hourTicks = XI.Common.VanaTime.HOUR;
        long expected = (TimeServer.VanaTicks(last) / hourTicks) - (TimeServer.VanaTicks(t0) / hourTicks);
        int vana = sent.Count(m =>
        {
            var e = (ConquestEvent)m;
            return e.TypeField == (byte)ConquestMessage.W2M_BroadcastInfluencePoints && !ConquestInfluenceUpdate.FromBytes(e.Payload).ShouldUpdateZones;
        });
        Check("vana hourly count over 10 min", expected, vana);
        Check("vana hourly between 4 and 5", true, vana >= 4 && vana <= 5);

        // engine wiring: conquest events routed to ConquestSystem, tick/pump callable
        var chan = new QueueWorldChannel();
        var eng = new WorldEngine(db, chan, new ZoneSettings(), new DailyTallySettings(), () => now);
        var framed = IpcFraming.ToBytesWithHeader(new ConquestEvent
        {
            TypeField = (byte)ConquestMessage.M2W_AddMobKills,
            Payload = new ConquestAddCounter { Count = 2, Region = 5 }.ToBytes(),
        });
        chan.Inject(new Ipp(1, 2), framed);
        var before = Row(db, 5);
        eng.Pump();
        Check("engine routes conquest event", before.Split(',')[3] != Row(db, 5).Split(',')[3], true);
        eng.Tick();
        Check("engine intervals", "2400,100", $"{(int)WorldEngine.TimeServerTickInterval.TotalMilliseconds},{(int)WorldEngine.IpcPumpInterval.TotalMilliseconds}");
    }
}
