using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Common;
using XI.Map;
using XI.Ipc;
using XI.Persistence;
using XI.Script;
using XI.World;
using XI.Zone;

// XI.Verify.ZoneConquest.Live -- live gate for the map side of conquest and the zone Lua by default: the luautils
// conquest globals (GetRegionOwner, GetRegionInfluence, GetNationRank, GetConquestBalance, IsConquestAlliance,
// WeekUpdateConquest -> conquest::UpdateConquestGM) and the JST time globals, the CLuaBaseEntity conquest methods
// (getCP / addCP / delCP with GP_SERV_COMMAND_CONQUEST, gainConquestInfluence, addConquestMobKills), conquest::
// HandleMessage on the world's W2M broadcasts (luautils::OnConquestUpdate, SetRegionalConquestOverseers), and
// xi.player.onGameIn's charCreate on a first login (equipItem, setGil, setNewPlayer) with ZoneLuaEnabled left at its
// default; the map's ConquestData cache (conquest::GetConquestData) kept in step by XI.World.ConquestSystem's broadcasts;
// xi.conquest's guards driven through their events (a city guard's signet, CP shop and supply run, an outpost
// teleporter, the outpost guard taking the supplies, an outpost vendor's shop and home warp) and CLuaItem isType /
// getCurrentCharges. West Ronfaure (Ronfaure, region 0) with a Hume Bastokan, Northern San d'Oria with a San d'Orian;
// conquest_system is set for the run and restored.
// (XI.Verify.Conquest.Live is the world side's suite.)
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 993500, GuardCharId = 995831;
    private const ushort WestRonfaure = 100, ValkurmDunes = 103, NorthernSandoria = 231, BastokMines = 234;

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static ResultSet Sql(IDatabase db, string q, params BoundValue[] a) => db.Execute(q, a);

    private static string DbRow(IDatabase db, string cols, string from, uint charId = CharId)
    {
        var r = Sql(db, $"SELECT {cols} {from}", BoundValue.Of(charId));
        if (r == null || !r.Next()) return "-";
        var parts = new List<string>();
        for (uint i = 0; i < r.ColumnCount(); i++) parts.Add(r.Get<string>(i) ?? "null");
        return string.Join(" ", parts);
    }

    // every char table the login, charCreate and the bindings write
    private static void Cleanup(IDatabase db)
    {
        var tables = new List<string>();
        var r = Sql(db, "SELECT table_name AS t FROM information_schema.columns WHERE table_schema = DATABASE() AND column_name = 'charid' AND table_name <> 'chars'");
        while (r != null && r.Next()) tables.Add(r.Get<string>("t"));
        foreach (uint id in new[] { CharId, GuardCharId })
        {
            foreach (var t in tables) Sql(db, $"DELETE FROM `{t}` WHERE charid = ?", BoundValue.Of(id));
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

    private const string ConquestCols = "region_id, region_control, region_control_prev, sandoria_influence, bastok_influence, windurst_influence, mob_kills, player_homepoints";

    private static List<int[]> SnapshotConquest(IDatabase db)
    {
        var rows = new List<int[]>();
        var r = Sql(db, $"SELECT {ConquestCols} FROM conquest_system");
        while (r != null && r.Next())
        {
            var row = new int[8];
            for (uint i = 0; i < 8; i++) row[i] = r.Get<int>(i);
            rows.Add(row);
        }
        return rows;
    }

    private static void RestoreConquest(IDatabase db, List<int[]> rows)
    {
        if (rows == null) return;
        Sql(db, "DELETE FROM conquest_system");
        foreach (var row in rows)
            Sql(db, $"INSERT INTO conquest_system({ConquestCols}) VALUES (?,?,?,?,?,?,?,?)", row.Select(v => BoundValue.Of(v)).ToArray());
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

        List<int[]> conquest = null;
        try
        {
            Cleanup(db);
            conquest = SnapshotConquest(db);
            Run(db);
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FAIL exception: {ex}");
        }
        finally
        {
            RestoreConquest(db, conquest);
            Cleanup(db);
        }

        Console.WriteLine($"{_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    private static int Opcode(BasicPacket p) => p.Buffer[0] | (p.Buffer[1] & 1) << 8;

    // Ronfaure (0) Bastok's with 6000 / 2000 / 2000 influence and no homepoints (no beastmen influence); the other
    // conquest regions 1..10 San d'Oria's, 11..15 Bastok's, 16..18 Windurst's: 10 / 6 / 3 regions
    private static void SetConquest(IDatabase db)
    {
        Sql(db, "DELETE FROM conquest_system");
        for (int id = 0; id <= 18; id++)
        {
            int owner = id == 0 ? 1 : id <= 10 ? 0 : id <= 15 ? 1 : 2;
            int san = id == 0 ? 6000 : 0, bas = id == 0 ? 2000 : 0, win = id == 0 ? 2000 : 0;
            Sql(db, $"INSERT INTO conquest_system({ConquestCols}) VALUES (?,?,?,?,?,?,0,0)", BoundValue.Of(id), BoundValue.Of(owner),
                BoundValue.Of(owner), BoundValue.Of(san), BoundValue.Of(bas), BoundValue.Of(win));
        }
    }

    // a first login: a Hume male (race 1) Bastokan WAR, no NEW_ADVENTURER title, with only the gil slot
    private static void Fixture(IDatabase db)
    {
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("zoneconquest"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Zoneconq"), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(0.0f), BoundValue.Of(0.0f), BoundValue.Of(0.0f), BoundValue.Of((byte)1), BoundValue.Of((byte)0));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl, zoning) VALUES (?, 300, 50, 1, 1, 2)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 1)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_unlocks(charid) VALUES (?)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_inventory(charid,location,slot,itemId,quantity) VALUES (?,0,0,65535,0)", BoundValue.Of(CharId));
    }

    // a San d'Orian WAR 30 of rank 3, not on a first login (NEW_ADVENTURER), by Achantere, T.K. in Northern San d'Oria,
    // with 10000 gil and 5000 conquest points
    private static void GuardFixture(IDatabase db)
    {
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(GuardCharId), BoundValue.Of("conqguard"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,titles) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(GuardCharId), BoundValue.Of(GuardCharId), BoundValue.Of("Conqguard"), BoundValue.Of(NorthernSandoria), BoundValue.Of(NorthernSandoria),
            BoundValue.Of(-246.0f), BoundValue.Of(7.0f), BoundValue.Of(40.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0), BoundValue.Of(titles));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(GuardCharId), BoundValue.Of(GuardCharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl, zoning) VALUES (?, 300, 50, 1, 30, 0)", BoundValue.Of(GuardCharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 30)", BoundValue.Of(GuardCharId));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(GuardCharId));
        Sql(db, "REPLACE INTO char_profile(charid, rank_sandoria) VALUES (?, 3)", BoundValue.Of(GuardCharId));
        Sql(db, "REPLACE INTO char_points(charid, sandoria_cp) VALUES (?, 5000)", BoundValue.Of(GuardCharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(GuardCharId));
        Sql(db, "INSERT IGNORE INTO char_unlocks(charid) VALUES (?)", BoundValue.Of(GuardCharId));
        Sql(db, "REPLACE INTO char_inventory(charid,location,slot,itemId,quantity) VALUES (?,0,0,65535,10000)", BoundValue.Of(GuardCharId));
    }

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        SetConquest(db);
        Fixture(db);

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
        Check("ZoneLuaEnabled is on by default", true, zone.ZoneLuaEnabled);
        long now = 1_000_000;
        zone.NowMs = () => now;
        var ipc = new List<IIpcMessage>();
        zone.SendIpc = m => ipc.Add(m);
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);
        var lua = scripts.Lua;
        var got = new List<string>();
        lua.SetGlobal("verifyReport", l => { got.Add(l.ToStringValue(1)); return 0; });
        lua.DoString("report = function(v) verifyReport(tostring(v)) end", "=verify_zoneconquest");
        string Lua(string code)
        {
            got.Clear();
            lua.DoString(code, "=verify_zoneconquest");
            return string.Join(",", got);
        }
        List<string> ScriptErrors() => log.Where(l => l.StartsWith("lua:") || l.Contains("luautils::")).Distinct().ToList();

        Console.WriteLine("the luautils conquest globals, read from conquest_system");
        Check("GetRegionOwner: Ronfaure Bastok's, Zulkheim San d'Oria's, a city (19) and Limbus (27) the beastmen's",
              "1,0,3,3", Lua("report(GetRegionOwner(0)) report(GetRegionOwner(1)) report(GetRegionOwner(19)) report(GetRegionOwner(27))"));
        log.Clear();
        Check("GetRegionOwner(28): 0, with the invalid region error", "0", Lua("report(GetRegionOwner(28))"));
        Check("  the error logged", true, log.Contains("Invalid conquest region passed to function (28)"));
        Check("GetRegionInfluence: 6000 / 2000 / 2000 reads San d'Oria at half (2); an even region 0",
              "2,0", Lua("report(GetRegionInfluence(0)) report(GetRegionInfluence(1))"));
        Check("GetConquestBalance: 10 / 6 / 3 regions rank 1 / 2 / 3 (1 + 2 << 2 + 3 << 4)", "57", Lua("report(GetConquestBalance())"));
        Check("GetNationRank: San d'Oria 1, Bastok 2, Windurst 3, the beastmen 0", "1,2,3,0",
              Lua("for n = 0, 3 do report(GetNationRank(n)) end"));
        Check("IsConquestAlliance: San d'Oria's 10 regions outnumber 6 + 3", "true", Lua("report(IsConquestAlliance())"));

        Console.WriteLine("the JST globals");
        long unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var jst = DateTimeOffset.UtcNow.AddHours(9);
        Check("JstYear / JstMonth / JstDayOfTheMonth / JstDayOfTheWeek (Sunday 0)", $"{jst.Year},{jst.Month},{jst.Day},{(int)jst.DayOfWeek}",
              Lua("report(JstYear()) report(JstMonth()) report(JstDayOfTheMonth()) report(JstDayOfTheWeek())"));
        var mid = Lua("report(JstMidnight())");
        long midnight = long.Parse(mid);
        Check("JstMidnight: the next 00:00 JST, within a day", "True 0", $"{midnight > unix && midnight - unix <= 86400} {(midnight + 9 * 3600) % 86400}");
        Check("NextJstDay is JstMidnight", mid, Lua("report(NextJstDay())"));
        long week = long.Parse(Lua("report(NextJstWeek())"));
        Check("NextJstWeek: a Monday 00:00 JST within a week", "True 0 1",
              $"{week > unix && week - unix <= 7 * 86400} {(week + 9 * 3600) % 86400} {((week + 9 * 3600) / 86400 + 4) % 7}");
        Check("NextConquestTally is NextJstWeek", week.ToString(), Lua("report(NextConquestTally())"));
        Check("JstHour", jst.Hour.ToString(), Lua("report(JstHour())"));

        Console.WriteLine("the first login: xi.player.onGameIn's charCreate");
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
        int Count(int opcode) => s.PacketList.Count(p => Opcode(p) == opcode);
        string Me = $"local p = GetPlayerByID({CharId})";
        Check("the Hume male gear in inventory and equipped (body, hands, legs, feet)", "true,true,true,true",
              Lua($"{Me} local g = {{ {{ xi.item.HUME_TUNIC, xi.slot.BODY }}, {{ xi.item.HUME_M_GLOVES, xi.slot.HANDS }}, {{ xi.item.HUME_SLACKS, xi.slot.LEGS }}, {{ xi.item.HUME_M_BOOTS, xi.slot.FEET }} }}" +
                  " for _, v in ipairs(g) do report(p:hasItem(v[1]) and p:getEquipID(v[2]) == v[1]) end"));
        Check("the WAR's Onion Sword in inventory, not equipped", "true,false",
              Lua($"{Me} report(p:hasItem(xi.item.ONION_SWORD)) report(p:getEquipID(xi.slot.MAIN) == xi.item.ONION_SWORD)"));
        Check("setGil: START_GIL", Lua("report(xi.settings.main.START_GIL)"), Lua($"{Me} report(p:getGil())"));
        Check("  in the gil slot", Lua("report(xi.settings.main.START_GIL)"), DbRow(db, "quantity", "FROM char_inventory WHERE charid = ? AND location = 0 AND slot = 0"));
        Check("setNewPlayer(true): getNewPlayer, chars.settings bit 26 clear", "true 0",
              $"{Lua($"{Me} report(p:getNewPlayer())")} {(long.Parse(DbRow(db, "settings", "FROM chars WHERE charid = ?")) >> 26) & 1}");
        Check("the NEW_ADVENTURER title, the Bastok map, the job gestures", "true,true,true",
              Lua($"{Me} report(p:hasTitle(xi.title.NEW_ADVENTURER)) report(p:hasKeyItem(xi.keyItem.MAP_OF_THE_BASTOK_AREA)) report(p:hasKeyItem(xi.keyItem.JOB_GESTURE_THIEF))"));
        Check("the starting fame: Bastok 70 + 30", "100", Lua($"{Me} report(p:getFame(xi.fameArea.BASTOK))"));
        Lua($"{Me} p:setNewPlayer(false)");
        Check("setNewPlayer(false): getNewPlayer false, bit 26 set", "false 1",
              $"{Lua($"{Me} report(p:getNewPlayer())")} {(long.Parse(DbRow(db, "settings", "FROM chars WHERE charid = ?")) >> 26) & 1}");
        Check("no script errors on the login", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("getCP / addCP / delCP");
        int conquestPackets = Count(0x05E);
        Check("getCP: none yet", "0", Lua($"{Me} report(p:getCP())"));
        Lua($"{Me} p:addCP(100)");
        Check("addCP(100): 100, in char_points.bastok_cp", "100 100", $"{Lua($"{Me} report(p:getCP())")} {DbRow(db, "bastok_cp", "FROM char_points WHERE charid = ?")}");
        Check("  a GP_SERV_COMMAND_CONQUEST", conquestPackets + 1, Count(0x05E));
        Lua($"{Me} p:delCP(30)");
        Check("delCP(30): 70, another 0x05E", $"70 {conquestPackets + 2}", $"{Lua($"{Me} report(p:getCP())")} {Count(0x05E)}");

        Console.WriteLine("the messages to the world");
        ipc.Clear();
        Lua($"{Me} p:gainConquestInfluence(50)");
        var add = ipc.OfType<ConquestEvent>().ToList();
        var inf = add.Count == 1 ? ConquestAddInfluencePoints.FromBytes(add[0].Payload) : null;
        Check("gainConquestInfluence(50): M2W_AddInfluencePoints 50, Bastok, Ronfaure", "6 50 1 0",
              inf == null ? $"{add.Count} messages" : $"{add[0].TypeField} {inf.Points} {inf.Nation} {inf.Region}");
        ipc.Clear();
        log.Clear();
        Lua($"{Me} p:gainConquestInfluence(0)");
        Check("gainConquestInfluence(0): nothing sent, a warning", "0 True",
              $"{ipc.Count} {log.Contains("gainConquestInfluence: non-positive amount (0) ignored.")}");
        Lua($"{Me} p:addConquestMobKills(3)");
        var kills = ipc.OfType<ConquestEvent>().ToList();
        var counter = kills.Count == 1 ? ConquestAddCounter.FromBytes(kills[0].Payload) : null;
        Check("addConquestMobKills(3): M2W_AddMobKills 3 in Ronfaure", "7 3 0",
              counter == null ? $"{kills.Count} messages" : $"{kills[0].TypeField} {counter.Count} {counter.Region}");
        ipc.Clear();
        Lua("WeekUpdateConquest(0) WeekUpdateConquest(2)");
        Check("WeekUpdateConquest(0) / (2): M2W_GM_WeeklyUpdate, M2W_GM_ConquestUpdate", "4,5",
              string.Join(",", ipc.OfType<ConquestEvent>().Select(m => m.TypeField)));

        Console.WriteLine("the map's ConquestData cache, kept in step by the world's broadcasts");
        // the world's conquest system, broadcasting to this zone process
        var world = new ConquestSystem(db, m => zone.OnIpc(m));
        Sql(db, "UPDATE conquest_system SET region_control = 2, sandoria_influence = 0, bastok_influence = 0, windurst_influence = 9000 WHERE region_id = 0");
        Check("conquest_system changed without a broadcast: the cache still reads Ronfaure Bastok's, San d'Oria at half", "1,2",
              Lua("report(GetRegionOwner(0)) report(GetRegionInfluence(0))"));
        world.UpdateVanaHourlyConquest();
        Check("the world's Vana'diel hour (W2M_BroadcastInfluencePoints): the new influence (Windurst 65%+ = 48), the owner kept", "1,48",
              Lua("report(GetRegionOwner(0)) report(GetRegionInfluence(0))"));
        zone.OnIpc(new ConquestEvent { TypeField = (byte)ConquestMessage.W2M_BroadcastRegionControls,
                                       Payload = new ConquestRegionControlUpdate { RegionControls = world.GetRegionControls() }.ToBytes() });
        Check("W2M_BroadcastRegionControls: Ronfaure Windurst's; 10 / 5 / 4 regions keep the balance and the alliance", "2,57,true",
              Lua("report(GetRegionOwner(0)) report(GetConquestBalance()) report(IsConquestAlliance())"));
        int conquestReplies = Count(0x05E);
        Send(0x05A, 4);
        var reply = s.PacketList.LastOrDefault(p => Opcode(p) == 0x05E);
        Check("the 0x05A request answers from the cache: Ronfaure's owner Windurst (3), Windurst's share 100%", $"{conquestReplies + 1} 3 100",
              reply == null ? "-" : $"{Count(0x05E)} {reply.Buffer[4 + 22 + 3]} {reply.Buffer[4 + 0x84]}");
        // back to the run's state, through the world as well
        SetConquest(db);
        world.UpdateVanaHourlyConquest();
        zone.OnIpc(new ConquestEvent { TypeField = (byte)ConquestMessage.W2M_BroadcastRegionControls,
                                       Payload = new ConquestRegionControlUpdate { RegionControls = world.GetRegionControls() }.ToBytes() });
        Check("  restored: Ronfaure Bastok's, San d'Oria at half", "1,2", Lua("report(GetRegionOwner(0)) report(GetRegionInfluence(0))"));
        byte[] Influences(bool update) => new ConquestInfluenceUpdate { ShouldUpdateZones = update, Influences = world.GetRegionalInfluences() }.ToBytes();
        byte[] Controls() => new ConquestRegionControlUpdate { RegionControls = world.GetRegionControls() }.ToBytes();

        Console.WriteLine("conquest::HandleMessage: the world's broadcasts");
        Lua("local zt = xi.zones.West_Ronfaure.Zone\n" +
            "verifyRealHook = zt.onConquestUpdate\n" +
            "zt.onConquestUpdate = function(z, t, i, o, r, a) report(z:getID() .. ' ' .. t .. ' ' .. i .. ' ' .. o .. ' ' .. r .. ' ' .. tostring(a)) end");
        string Broadcast(ConquestMessage type, byte[] payload)
        {
            got.Clear();
            zone.OnIpc(new ConquestEvent { TypeField = (byte)type, Payload = payload ?? Array.Empty<byte>() });
            return string.Join(",", got);
        }
        Check("W2M_WeeklyUpdateStart: onConquestUpdate(zone, TALLY_START, influence 2, owner Bastok, ranking 57, alliance)",
              $"{WestRonfaure} 0 2 1 57 true", Broadcast(ConquestMessage.W2M_WeeklyUpdateStart, null));
        Check("W2M_BroadcastInfluencePoints with shouldUpdateZones: UPDATE", $"{WestRonfaure} 2 2 1 57 true",
              Broadcast(ConquestMessage.W2M_BroadcastInfluencePoints, Influences(true)));
        Check("  without it: no update", "", Broadcast(ConquestMessage.W2M_BroadcastInfluencePoints, Influences(false)));
        Check("  a payload that does not decode: dropped", "", Broadcast(ConquestMessage.W2M_BroadcastInfluencePoints, new byte[] { 0xFF, 0x00 }));
        Check("W2M_BroadcastRegionControls: no zone callback", "", Broadcast(ConquestMessage.W2M_BroadcastRegionControls, Controls()));
        log.Clear();
        Broadcast((ConquestMessage)9, null);
        Check("an unknown conquest message logged", true, log.Contains("Message: unhandled conquest type message received: 9"));

        // the overseers of Ronfaure set the other way first: the tally end's SetRegionalConquestOverseers shows Bastok's
        string Overseers() => Lua("local base = zones[xi.zone.WEST_RONFAURE].npc.OVERSEER_BASE report(GetNPCByID(base):getStatus()) report(GetNPCByID(base + 1):getStatus())");
        Lua("local base = zones[xi.zone.WEST_RONFAURE].npc.OVERSEER_BASE GetNPCByID(base):setStatus(xi.status.NORMAL) GetNPCByID(base + 1):setStatus(xi.status.DISAPPEAR)");
        Check("before the tally end: Doladepaiton (San d'Oria) shown, Yoshihiro (Bastok) hidden (DISAPPEAR 2)", "0,2", Overseers());
        conquestPackets = Count(0x05E);
        log.Clear();
        Check("W2M_WeeklyUpdateEnd: onConquestUpdate(zone, TALLY_END, ...)", $"{WestRonfaure} 1 2 1 57 true",
              Broadcast(ConquestMessage.W2M_WeeklyUpdateEnd, Controls()));
        Check("  the overseers: Doladepaiton hidden, Yoshihiro shown", "2,0", Overseers());
        Check("  a 0x05E to the char in the zone, the finished log", $"{conquestPackets + 1} True",
              $"{Count(0x05E)} {log.Contains("Conquest Weekly Update is finished")}");
        Check("  a payload that does not decode: dropped", "", Broadcast(ConquestMessage.W2M_WeeklyUpdateEnd, new byte[] { 0x01 }));
        ipc.Clear();
        Check("WeekUpdateConquest(1): the tally end here, nothing sent", $"{WestRonfaure} 1 2 1 57 true 0",
              $"{Lua("WeekUpdateConquest(1)")} {ipc.Count}");

        // the zone's own onConquestUpdate (xi.conquest.onConquestUpdate) on each broadcast type
        Lua("xi.zones.West_Ronfaure.Zone.onConquestUpdate = verifyRealHook");
        log.Clear();
        int before = s.PacketList.Count;
        Broadcast(ConquestMessage.W2M_WeeklyUpdateStart, null);
        Broadcast(ConquestMessage.W2M_BroadcastInfluencePoints, Influences(true));
        Broadcast(ConquestMessage.W2M_WeeklyUpdateEnd, Controls());
        Check("the real xi.conquest.onConquestUpdate messages the char", true, s.PacketList.Count > before);
        Check("no script errors in the conquest hooks", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("the campaign: campaign_system and campaign_handler");
        string Scalar(string q, params BoundValue[] a)
        {
            var r = Sql(db, q, a);
            return r != null && r.Next() ? r.Get<string>(0u) : "";
        }
        const string NationCols = "reconnaissance, morale, prosperity";
        const string MapCols = "heroism, influence_sandoria, influence_bastok, current_fortifications, max_fortifications";
        string nationWas = Scalar($"SELECT CONCAT_WS(',', {NationCols}) FROM campaign_nation WHERE id = 1");
        string mapWas = Scalar($"SELECT CONCAT_WS(',', {MapCols}) FROM campaign_map WHERE zoneid = 80");
        try
        {
            Sql(db, "UPDATE campaign_nation SET reconnaissance = 3, morale = 40, prosperity = 55 WHERE id = 1");
            Sql(db, "UPDATE campaign_map SET heroism = 17, influence_bastok = 30, current_fortifications = 100, max_fortifications = 500 WHERE zoneid = 80");
            zone.LoadCampaign();
            var state = zone.CampaignState;
            int zones = int.Parse(Scalar("SELECT COUNT(*) FROM zone_settings"));
            Check("LoadNations: the seven armies, Bastok's standing", "7/3/40/55",
                $"{state.Nations.Count}/{zone.CampaignReconnaissance(CampaignArmy.Bastok)}/{zone.CampaignMorale(CampaignArmy.Bastok)}/{zone.CampaignProsperity(CampaignArmy.Bastok)}");
            // the C++ counts in uint8s: San d'Oria's (every zone without a campaign_map row is nation 0) wraps past 255
            string Tally(int mask) => ((byte)state.Regions.Count(x => mask == 8 ? x.NationControl is not (2 or 4 or 6) : x.NationControl == mask)).ToString();
            Check("LoadState: a region for every zone served, each counted to its side in a byte", $"{zones}/{Tally(2)}/{Tally(4)}/{Tally(6)}/{Tally(8)}",
                $"{state.Regions.Count}/{state.ControlSandoria}/{state.ControlBastok}/{state.ControlWindurst}/{state.ControlBeastman}");
            var southern = zone.CampaignHandlerOf(80);
            Check("Southern San d'Oria [S]'s handler: campaign id 0, nation 2 (Windurst's mask 6 in the state), heroism 17",
                "0/2/17/30/100/500", $"{southern.CampaignId}/{southern.ZoneControl}/{southern.Heroism}/{southern.GetInfluence(CampaignArmy.Bastok)}/{southern.Fortification}/{southern.MaxFortification}");
            Check("the regions in campaign id order, the last map region 25", "True/25",
                $"{state.Regions.Zip(state.Regions.Skip(1)).All(p => p.First.CampaignId <= p.Second.CampaignId)}/{state.Regions[^1].CampaignId}");
            Check("a beastmen influence read for any of the four", "True",
                $"{southern.GetInfluence(CampaignArmy.Orcish) == southern.GetInfluence(CampaignArmy.Kindred)}");

            zone.SetCampaignAlliedNotes(c, 1234);
            Check("allied notes: char_points.allied_notes", "1234/1234",
                $"{zone.CampaignAlliedNotes(c)}/{DbRow(db, "allied_notes", "FROM char_points WHERE charid = ?")}");
            s.PacketList.Clear();
            zone.SendCampaignUpdate(c);
            var halves = s.PacketList.Where(p => Opcode(p) == 0x071).ToList();
            Check("SendUpdate: two 0x071 INFLUENCE::CAMPAIGN halves", 2, halves.Count);
            if (halves.Count == 2)
            {
                byte[] b0 = halves[0].Buffer.AsSpan(4).ToArray(), b1 = halves[1].Buffer.AsSpan(4).ToArray();
                Check("mode 2, length 0xC4, regions from 0 and 13, the allied notes", "Campaign/196/0/13/1234",
                    $"{S2cInfluenceCampaign.GetMode(b0)}/{S2cInfluenceCampaign.GetLength(b0)}/{S2cInfluenceCampaign.GetZoneOffset(b0)}/{S2cInfluenceCampaign.GetZoneOffset(b1)}/{S2cInfluenceCampaign.GetAlliedNotes(b0)}");
                Check("Bastok's standing and the areas each side holds", $"3/40/55/{state.ControlSandoria}/{state.ControlBeastman}",
                    $"{S2cInfluenceCampaign.GetNationReconnaissance(b0, 1)}/{S2cInfluenceCampaign.GetNationMorale(b0, 1)}/{S2cInfluenceCampaign.GetNationProsperity(b0, 1)}/{S2cInfluenceCampaign.GetCASandoria(b0)}/{S2cInfluenceCampaign.GetCABeastman(b0)}");
            }

            zone.SetCampaignMorale(CampaignArmy.Bastok, 120);
            zone.SetCampaignReconnaissance(CampaignArmy.Bastok, 50);
            Check("SetMorale capped at 100, SetReconnaissance at 10, both written", "100/10/10,100,55",
                $"{zone.CampaignMorale(CampaignArmy.Bastok)}/{zone.CampaignReconnaissance(CampaignArmy.Bastok)}/{Scalar($"SELECT CONCAT_WS(',', {NationCols}) FROM campaign_nation WHERE id = 1")}");
            southern.SetHeroism(300);
            southern.SetFortification(800);
            southern.SetInfluence(CampaignArmy.Bastok, 999);
            Check("the handler's setters: heroism capped at 200, fortifications at the max, influence at 250, written",
                "200/500/250/200,0,250,500,500", $"{southern.Heroism}/{southern.Fortification}/{southern.InfluenceBastok}/{Scalar($"SELECT CONCAT_WS(',', {MapCols}) FROM campaign_map WHERE zoneid = 80")}");
            log.Clear();
            southern.SetInfluence(CampaignArmy.Sandoria, 5);
            Check("San d'Oria's influence never changes (the C++'s statement fails), the error logged", "0/True",
                $"{southern.InfluenceSandoria}/{log.Contains("Unable to update influence.")}");
        }
        finally
        {
            var n = nationWas.Split(',');
            Sql(db, "UPDATE campaign_nation SET reconnaissance = ?, morale = ?, prosperity = ? WHERE id = 1",
                BoundValue.Of(int.Parse(n[0])), BoundValue.Of(int.Parse(n[1])), BoundValue.Of(int.Parse(n[2])));
            var m = mapWas.Split(',');
            Sql(db, "UPDATE campaign_map SET heroism = ?, influence_sandoria = ?, influence_bastok = ?, current_fortifications = ?, max_fortifications = ? WHERE zoneid = 80",
                BoundValue.Of(int.Parse(m[0])), BoundValue.Of(int.Parse(m[1])), BoundValue.Of(int.Parse(m[2])), BoundValue.Of(int.Parse(m[3])), BoundValue.Of(int.Parse(m[4])));
        }

        Guards(db, zone, s, log, Lua, () => now += 1000);
    }

    // xi.conquest's NPCs driven through their events: a city guard's signet and conquest point shop (overseerOnTrigger /
    // onEventUpdate / onEventFinish), an outpost teleporter (teleporterOn*) and an outpost vendor's home nation warp
    // (vendorOn*), each teleport through the TELEPORT effect's onEffectLose
    private static void Guards(IDatabase db, ZoneHost zone, MapSession bastokan, List<string> log, Func<string, string> Lua, Action advance)
    {
        Console.WriteLine("the conquest guards: Achantere, T.K. (signet, CP shop), Jeanvirgaud (outpost teleport), Harvetour (home warp)");
        List<string> ScriptErrors() => log.Where(l => l.StartsWith("lua:") || l.Contains("luautils::")).Distinct().ToList();
        void Send(MapSession ss, ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(ss, opcode, new BasicPacket(buf));
            zone.AfterParse(ss);
        }
        void Talk(MapSession ss, NpcEntity npc)
        {
            var ch = (CharData)ss.Char;
            ch.X = npc.X; ch.Y = npc.Y; ch.Z = npc.Z;
            Send(ss, 0x01A, 28, b =>
            {
                BitConverter.GetBytes(npc.Id).CopyTo(b, 4);
                BitConverter.GetBytes(npc.TargId).CopyTo(b, C2sAction.OffActIndex);
                BitConverter.GetBytes((ushort)ActionId.Talk).CopyTo(b, C2sAction.OffActionID);
            });
        }
        // c2s 0x05B: mode 1 an update, 0 the end
        void Event(MapSession ss, ushort eventId, uint option, ushort mode) =>
            Send(ss, 0x05B, 20, b =>
            {
                BitConverter.GetBytes(option).CopyTo(b, 0x08);
                BitConverter.GetBytes(mode).CopyTo(b, 0x0E);
                BitConverter.GetBytes(eventId).CopyTo(b, 0x12);
            });
        uint Param(CharData ch, int i) => ch.CurrentEvent.Params.TryGetValue(i, out var v) ? v : 0;
        string Params(CharData ch) => string.Join(" ", Enumerable.Range(0, 8).Select(i => Param(ch, i)));
        string Pending(MapSession ss)
        {
            var p = ss.PacketList.LastOrDefault(x => Opcode(x) == 0x05C);
            return p == null ? "-" : string.Join(" ", Enumerable.Range(0, 5).Select(i => BitConverter.ToInt32(p.Buffer, 4 + i * 4)));
        }
        void Tick(int ms)
        {
            for (int t = 0; t < ms; t += 1000)
            {
                advance();
                zone.Tick();
            }
        }

        GuardFixture(db);
        var s = new MapSession { CharId = GuardCharId, AccountId = GuardCharId };
        zone.OnLogin(s);
        var c = (CharData)s.Char;
        Send(s, 0x00A, 92, buf => BitConverter.GetBytes(GuardCharId).CopyTo(buf, 12));
        Send(s, 0x00C, 12);
        if (c.IsInEvent) Event(s, (ushort)c.CurrentEvent.EventId, 0, 0);
        Check("the guard char in Northern San d'Oria, out of any event", $"{NorthernSandoria} -1", $"{c.Zone} {c.CurrentEvent.EventId}");
        var info = zone.ZoneInfoOf(NorthernSandoria);
        var achantere = info.Npcs.First(n => n.Name == "Achantere_TK");
        var jeanvirgaud = info.Npcs.First(n => n.Name == "Jeanvirgaud");
        string Me = $"local p = GetPlayerByID({GuardCharId})";
        log.Clear();

        // San d'Oria first (10 regions), Bastok second, Windurst third: a3 = 1 + 2 << 2 + 3 << 4; a7 the conquest points
        Talk(s, achantere);
        Check("Achantere: overseerOnTrigger's event 32762, a3 the nation ranks 57, a7 the CP 5000", "32762 57 5000",
              $"{c.CurrentEvent.EventId} {Param(c, 2)} {Param(c, 6)}");
        int talk = s.PacketList.Count(p => Opcode(p) == 0x02A);
        Event(s, 32762, 1, 0);
        Check("option 1, the signet: SIGNET for (rank 3 + nation rank 1 + 3) hours, 'You've received your nation's Signet!'",
              $"true,{(3 + 1 + 3) * 3600},{talk + 1},-1",
              Lua($"{Me} local e = p:getStatusEffect(xi.effect.SIGNET) report(e ~= nil) report(e and math.floor(e:getDuration() / 1000) or 0)") +
              $",{s.PacketList.Count(p => Opcode(p) == 0x02A)},{c.CurrentEvent.EventId}");

        // the CP shop: option 32929 a Scroll of Instant Warp for 10 CP; the update answers (u1, u2 0 = enough CP, the item)
        Talk(s, achantere);
        Event(s, 32762, 32929, 1);
        Check("CP shop update (32929): enough CP (u2 0), the item", $"0 {Lua("report(xi.item.SCROLL_OF_INSTANT_WARP)")}",
              string.Join(" ", Pending(s).Split(' ').Skip(1).Take(2)));
        Event(s, 32762, 32929, 0);
        Check("CP shop finish: the scroll given, 10 CP spent", "true,4990",
              Lua($"{Me} report(p:hasItem(xi.item.SCROLL_OF_INSTANT_WARP)) report(p:getCP())"));

        // a supply run to Zulkheim (option 65541 + region): the supplies key item and the run's vars
        Talk(s, achantere);
        Event(s, 32762, 65541 + 1, 0);
        Check("supply run begun: ZULKHEIM_SUPPLIES, supplyQuest_region 1", "true,1",
              Lua($"{Me} report(p:hasKeyItem(xi.keyItem.ZULKHEIM_SUPPLIES)) report(p:getCharVar('supplyQuest_region'))"));

        // Jeanvirgaud: the outposts of Zulkheim (San d'Oria's, fee 100) unlocked for the char's nation
        Lua($"{Me} p:addTeleport(xi.nation.SANDORIA, xi.region.ZULKHEIM + 5)");
        Talk(s, jeanvirgaud);
        Check("Jeanvirgaud: teleporterOnTrigger's event 716 with the region masks of each nation, the level, Zulkheim allowed",
              $"716 {0xFFC0} {0x1F0020} {0xE00000} 0 0 0 30 {0x3FFFFFBF}", $"{c.CurrentEvent.EventId} {Params(c)}");
        Event(s, 716, 1073741829 + 1, 1);
        Check("teleporterOnEventUpdate (Zulkheim): gil, fee 100, 0, the CP fee 10, the CP", "10000 100 0 10 4990", Pending(s));
        Event(s, 716, 5 + 1, 0);
        Check("teleporterOnEventFinish (gil): 100 gil paid, TELEPORT (OUTPOST, Zulkheim) on", $"9900,true,{Lua("report(xi.teleport.id.OUTPOST)")},1",
              Lua($"{Me} report(p:getGil()) local e = p:getStatusEffect(xi.effect.TELEPORT) report(e ~= nil) report(e and e:getPower()) report(e and e:getSubPower())"));
        Tick(3000);
        Check("TELEPORT's onEffectLose: xi.teleport.toOutpost to Valkurm Dunes", $"{ValkurmDunes} 1",
              DbRow(db, "pos_zone, (SELECT zoning FROM char_stats WHERE charid = chars.charid)", "FROM chars WHERE charid = ?", GuardCharId));

        // in at the Zulkheim outpost: Quanteilleron, R.K. takes the supplies (event 32763, 16), option 2 pays 30 CP
        s = new MapSession { CharId = GuardCharId, AccountId = GuardCharId };
        zone.OnLogin(s);
        c = (CharData)s.Char;
        Send(s, 0x00A, 92, buf => BitConverter.GetBytes(GuardCharId).CopyTo(buf, 12));
        Send(s, 0x00C, 12);
        if (c.IsInEvent) Event(s, (ushort)c.CurrentEvent.EventId, 0, 0);
        var quanteilleron = zone.ZoneInfoOf(ValkurmDunes).Npcs.First(n => n.Name == "Quanteilleron_RK");
        Talk(s, quanteilleron);
        Check("Quanteilleron: the supplies delivered (event 32763, 16)", "32763 16", $"{c.CurrentEvent.EventId} {Param(c, 0)}");
        Event(s, 32763, 2, 0);
        Check("option 2: the key item taken, 30 CP, the run's vars cleared", "false,5020,0",
              Lua($"{Me} report(p:hasKeyItem(xi.keyItem.ZULKHEIM_SUPPLIES)) report(p:getCP()) report(p:getCharVar('supplyQuest_region'))"));

        // Harvetour (Ronfaure's outpost vendor) with the Bastokan in West Ronfaure: Bastok holds Ronfaure (fee 100)
        var c1 = (CharData)bastokan.Char;
        var harvetour = zone.ZoneInfoOf(WestRonfaure).Npcs.First(n => n.Name == "Harvetour");
        string Bastokan = $"local p = GetPlayerByID({CharId})";
        Lua($"{Bastokan} p:addTeleport(xi.nation.BASTOK, xi.region.RONFAURE + 5) p:setGil(1000)");
        Talk(bastokan, harvetour);
        Check("Harvetour: vendorOnTrigger's event 32756, the region its nation's (1), fee 100, CP fee 10, the CP 70",
              "32756 1 100 0 10 70 0 0 0", $"{c1.CurrentEvent.EventId} {Params(c1)}");
        Event(bastokan, 32756, 0, 1);
        Check("vendorOnEventUpdate: gil, fee, 0, CP fee, CP", "1000 100 0 10 70", Pending(bastokan));
        Event(bastokan, 32756, 1, 0);
        Check("vendorOnEventFinish (1): xi.shop.outpost's five items (SHOP_OPEN, SHOP_LIST)", "5 1",
              $"{(bastokan.PacketList.LastOrDefault(p => Opcode(p) == 0x03E) is { } open ? BitConverter.ToUInt16(open.Buffer, 4) : -1)} " +
              $"{(bastokan.PacketList.Any(p => Opcode(p) == 0x03C) ? 1 : 0)}");
        Talk(bastokan, harvetour);
        Event(bastokan, 32756, 2, 0);
        Tick(3000);
        Check("vendorOnEventFinish (2): 100 gil paid, xi.teleport.toHomeNation to Bastok Mines", $"900 {BastokMines}",
              $"{DbRow(db, "quantity", "FROM char_inventory WHERE charid = ? AND location = 0 AND slot = 0")} {DbRow(db, "pos_zone", "FROM chars WHERE charid = ?")}");

        // xi.conquest.vendorOnTrade's item checks: CLuaItem::isType (weapons and armor are ITEM_EQUIPMENT) and the
        // exp rings' getCurrentCharges
        Check("CLuaItem::isType: a sword is ARMOR (equipment) and WEAPON, a scroll neither but USABLE; getCurrentCharges 0 on a lookup",
              "true,true,false,false,true,0",
              Lua("local sword = GetReadOnlyItem(xi.item.BRONZE_SWORD) local scroll = GetReadOnlyItem(xi.item.SCROLL_OF_INSTANT_WARP)" +
                  " report(sword:isType(xi.itemType.ARMOR)) report(sword:isType(xi.itemType.WEAPON))" +
                  " report(scroll:isType(xi.itemType.ARMOR)) report(scroll:isType(xi.itemType.WEAPON)) report(scroll:isType(xi.itemType.USABLE))" +
                  " report(GetReadOnlyItem(xi.item.CHARIOT_BAND):getCurrentCharges())"));
        Check("no script errors driving the guards", "", string.Join(" | ", ScriptErrors()));
    }
}
