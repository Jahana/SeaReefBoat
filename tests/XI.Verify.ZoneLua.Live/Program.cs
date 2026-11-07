using System;
using System.Collections.Generic;
using System.IO;
using XI.Common;
using XI.Map;
using XI.Ipc;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.ZoneLua.Live -- live gate for the zone Lua lifecycle (luautils::OnZoneInitialize, OnZoneIn and its
// cutscene in the 0x00A login, OnGameIn, AfterZoneIn, OnZoneOut, CZone::CheckTriggerAreas with OnTriggerAreaEnter /
// OnTriggerAreaLeave, CZone::CharZoneOut's leave, the CLuaZone and ITriggerArea bindings) in Lower Delkfutt's Tower,
// whose real Zone.lua registers two cuboid trigger areas and plays cs 14 to a char coming from Middle Delkfutt's Tower.
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 993000;
    private const ushort LowerDelkfutt = 184, MiddleDelkfutt = 157;

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static ResultSet Sql(IDatabase db, string q, params BoundValue[] a) => db.Execute(q, a);

    private static string DbRow(IDatabase db, string cols, string from)
    {
        var r = Sql(db, $"SELECT {cols} {from}", BoundValue.Of(CharId));
        if (r == null || !r.Next()) return "-";
        var parts = new List<string>();
        for (uint i = 0; i < r.ColumnCount(); i++) parts.Add(r.Get<string>(i) ?? "null");
        return string.Join(" ", parts);
    }

    // every char table the login, the scripts and the zone-out write
    private static void Cleanup(IDatabase db)
    {
        var tables = new List<string>();
        var r = Sql(db, "SELECT table_name AS t FROM information_schema.columns WHERE table_schema = DATABASE() AND column_name = 'charid' AND table_name <> 'chars'");
        while (r != null && r.Next()) tables.Add(r.Get<string>("t"));
        foreach (var t in tables) Sql(db, $"DELETE FROM `{t}` WHERE charid = ?", BoundValue.Of(CharId));
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

    private static void Fixture(IDatabase db, ushort zone, ushort prevZone, byte zoning)
    {
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7); // NEW_ADVENTURER: not a first login, no charCreate
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("zoneluaverify"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,titles) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Zonelua"), BoundValue.Of(zone), BoundValue.Of(prevZone),
            BoundValue.Of(0.0f), BoundValue.Of(0.0f), BoundValue.Of(0.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0), BoundValue.Of(titles));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl, zoning) VALUES (?, 300, 50, 1, 30, ?)", BoundValue.Of(CharId), BoundValue.Of(zoning));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 30)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_unlocks(charid) VALUES (?)", BoundValue.Of(CharId));
    }

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        // coming from Middle Delkfutt's Tower by a zone change (char_stats.zoning = 1), standing at 0,0,0
        Fixture(db, LowerDelkfutt, MiddleDelkfutt, 1);
        // a quest's ]mustZone var for another zone: onGameIn clears it through getCharVarsWithSuffix
        Sql(db, "INSERT INTO char_vars(charid, varname, value) VALUES (?, 'Quest[1][2]mustZone', ?)", BoundValue.Of(CharId), BoundValue.Of((int)MiddleDelkfutt));

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
        zone.ZoneLuaEnabled = true;
        long now = 1_000_000;
        zone.NowMs = () => now;
        long vanaNow = 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        zone.VanaTicksNow = () => vanaNow;
        long unixNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        zone.UnixNow = () => unixNow;
        zone.SendIpc = _ => { };
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);
        var lua = scripts.Lua;
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString(code, "=verify_zonelua");
            return string.Join(",", got);
        }

        Console.WriteLine("OnZoneInitialize: the zone's onInitialize as it loads");
        var areas = zone.TriggerAreasOf(LowerDelkfutt);
        Check("two cuboid trigger areas registered", "2 1 2 True", $"{areas.Count} {areas[0].Id} {areas[1].Id} {areas[0] is CuboidTriggerArea}");
        Check("area 1 holds (406, -33.5, 86), not (406, -32, 86)", "True False", $"{areas[0].IsPointInside(406, -33.5f, 86)} {areas[0].IsPointInside(406, -32, 86)}");
        Check("CLuaZone: the type mask (dungeon), the region", $"4,{ZoneRegions.Of(LowerDelkfutt)}",
              Lua($"local z = GetZone({LowerDelkfutt}) report(z:getTypeMask()) report(z:getRegionID())"));
        Check("CLuaZone local vars: unset 0, set, listed, reset",
              "0,7,v=7,0",
              Lua($"local z = GetZone({LowerDelkfutt}) report(z:getLocalVar('v')) z:setLocalVar('v', 7) report(z:getLocalVar('v'))" +
                  " for _, e in ipairs(z:getLocalVars()) do if e.varname == 'v' then report(e.varname .. '=' .. e.value) end end" +
                  " z:resetLocalVars() report(#z:getLocalVars())"));
        Check("setSoloBattleMusic / getSoloBattleMusic", "115", Lua($"local z = GetZone({LowerDelkfutt}) local old = z:getSoloBattleMusic() z:setSoloBattleMusic(115) report(z:getSoloBattleMusic()) z:setSoloBattleMusic(old)"));

        // hooks the zone script does not define, to see them run
        Lua("local zt = xi.zones.Lower_Delkfutts_Tower.Zone\n" +
            "zt.afterZoneIn = function(player) report('afterZoneIn ' .. player:getName()) end\n" +
            "zt.onZoneOut = function(player) report('onZoneOut ' .. player:getName()) end\n" +
            "zt.onTriggerAreaLeave = function(player, area) area:addCount(1) report('leave ' .. area:getTriggerAreaID() .. ' ' .. area:getCount()) end");

        Console.WriteLine("the 0x00A login from Middle Delkfutt's Tower: OnZoneIn's cs 14, OnGameIn");
        var s = new MapSession { CharId = CharId, AccountId = CharId };
        zone.OnLogin(s);
        var c = (CharData)s.Char;
        Check("charutils::LoadChar: char_stats.zoning read and cleared", "0", DbRow(db, "zoning", "FROM char_stats WHERE charid = ?"));
        void Send(ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(s, opcode, new BasicPacket(buf));
            zone.AfterParse(s);
        }
        void Tick(long ms)
        {
            got.Clear();
            now += ms;
            zone.Tick();
        }
        void EventEnd(ushort eventId, uint option) =>
            Send(0x05B, 20, b => { BitConverter.GetBytes(option).CopyTo(b, 0x08); BitConverter.GetBytes(eventId).CopyTo(b, 0x12); });
        List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);

        got.Clear();
        Send(0x00A, 92, buf => BitConverter.GetBytes(CharId).CopyTo(buf, 12));
        var login = Of(0x00A);
        Check("one 0x00A login", 1, login.Count);
        if (login.Count == 1)
        {
            var b = login[0].Buffer;
            Check("the 0x00A: EventNo (the zone), EventNum, EventPara 14, EventMode 0", $"{LowerDelkfutt} {LowerDelkfutt} 14 0",
                  $"{BitConverter.ToUInt16(b, 64)} {BitConverter.ToUInt16(b, 98)} {BitConverter.ToUInt16(b, 100)} {BitConverter.ToUInt16(b, 102)}");
            Check("the 0x00A: server_status Event, SendCount 1", "4 1", $"{b[31]} {BitConverter.ToUInt16(b, 172)}");
        }
        Check("the cutscene is the char's current event, locked", $"14 {EventType.Cutscene} True True",
              $"{c.CurrentEvent.EventId} {c.CurrentEvent.Type} {c.Locked} {c.IsInEvent}");
        Check("onZoneIn's setPos from 0,0,0", "460.0 188", $"{c.X:0.0} {c.Rotation}");
        // the char is Disappear while it loads in (charutils::LoadChar): setPos pushes no 0x05B, the login carries the
        // position, and the client's position reports are taken (no pendingPositionUpdate); then the char is Normal
        Check("no 0x05B for it, the char Normal after the login", "0 0", $"{Of(0x05B).Count} {c.Status}");
        Check("xi.player.onGameIn: ZoningIn set, ZoneInTime set", "1 True",
              $"{(c.LocalVars.TryGetValue("ZoningIn", out var zi) ? zi : 99)} {c.LocalVars.TryGetValue("ZoneInTime", out var zt) && zt > 0}");
        Check("onGameIn: the ]mustZone var of another zone cleared (getCharVarsWithSuffix)", "-", DbRow(db, "value", "FROM char_vars WHERE charid = ? AND varname = 'Quest[1][2]mustZone'"));
        Send(0x00C, 12);

        Console.WriteLine("the cutscene finishes through the event engine");
        EventEnd(14, 0);
        Check("cs 14 over, unlocked", "-1 False", $"{c.CurrentEvent.EventId} {c.Locked}");

        Console.WriteLine("AfterZoneIn, 4 s after the login");
        Tick(1000);
        Check("not yet", "", string.Join(",", got));
        Tick(3000);
        Check("the zone's afterZoneIn", "afterZoneIn Zonelua", string.Join(",", got));
        Check("onGameIn's 2.5 s timer cleared ZoningIn", "0", c.LocalVars.TryGetValue("ZoningIn", out var zi2) ? zi2 : 99);

        Console.WriteLine("the CLuaBaseEntity bindings of onGameIn");
        Check("getTraverserEpoch unset, setTraverserEpoch, then set", "0,true",
              Lua($"local p = GetPlayerByID({CharId}) report(p:getTraverserEpoch()) p:setTraverserEpoch() report(tostring(p:getTraverserEpoch() > 0))"));
        Sql(db, "INSERT INTO char_vars(charid, varname, value) VALUES (?, 'A]mustZone', 5), (?, 'B]mustZone', 6), (?, 'C]other', 7)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(CharId));
        Check("getCharVarsWithSuffix: the vars ending in the suffix", "A]mustZone=5,B]mustZone=6",
              Lua($"local t = GetPlayerByID({CharId}):getCharVarsWithSuffix(']mustZone') local k = {{}} for n, v in pairs(t) do k[#k + 1] = n .. '=' .. v end table.sort(k) for _, e in ipairs(k) do report(e) end"));
        Check("clearGearSetMods / addGearSetMod", "0,12,0",
              Lua($"local p = GetPlayerByID({CharId}) local before = p:getMod(xi.mod.STR) p:clearGearSetMods() report(p:getMod(xi.mod.STR) - before)" +
                  " p:addGearSetMod(1, xi.mod.STR, 12) report(p:getMod(xi.mod.STR) - before) p:clearGearSetMods() report(p:getMod(xi.mod.STR) - before)"));

        Console.WriteLine("luautils globals: items, players and char vars by id");
        Check("GetItemByID / GetItemFlagsByID / GetItemLevelRequirementsByID: the fire crystal, its flags 516; the cloak's level 34; nil, 0 and 0 for no such item",
              "fire_crystal,516,0,34,true,0,0",
              Lua("report(GetItemByID(4096):getName()) report(GetItemFlagsByID(4096)) report(GetItemLevelRequirementsByID(4096))" +
                  " report(GetItemLevelRequirementsByID(12610)) report(tostring(GetItemByID(65000) == nil)) report(GetItemFlagsByID(65000))" +
                  " report(GetItemLevelRequirementsByID(65000))"));
        Check("GetItemIDByName: one match its id, 55 matches 0xFFFF - 55 + 1, none 0", "4096,65481,0",
              Lua("report(GetItemIDByName('fire_crystal')) report(GetItemIDByName('%crystal')) report(GetItemIDByName('no such item'))"));
        Check("GetPlayerByName ignores case, nil for nobody; GetPlayerIDByName by chars.charname, 0 for nobody; PlayerHasValidSession false without a row",
              $"{CharId},true,{CharId},0,false",
              Lua($"report(GetPlayerByName('ZONELUA'):getID()) report(tostring(GetPlayerByName('Nobody') == nil)) report(GetPlayerIDByName('Zonelua'))" +
                  " report(GetPlayerIDByName('Nobody')) report(tostring(PlayerHasValidSession(1)))"));
        Sql(db, "DELETE FROM char_vars WHERE varname = 'LuGlob'");
        Check("SetCharVar on this map's char sets its own (cache and row); GetCharVar reads the row; another char's goes to the db",
              "5,5,5,3",
              Lua($"SetCharVar({CharId}, 'LuGlob', 5) report(GetPlayerByID({CharId}):getCharVar('LuGlob')) report(GetCharVar({CharId}, 'LuGlob'))" +
                  $" SetCharVar({CharId + 1}, 'LuGlob', 3)") + "," + DbRow(db, "value", "FROM char_vars WHERE charid = ? AND varname = 'LuGlob'") + "," +
              Lua($"report(GetCharVar({CharId + 1}, 'LuGlob'))"));
        Check("SetCharVar refuses an expiry already past", "5", Lua($"SetCharVar({CharId}, 'LuGlob', 9, 1) report(GetCharVar({CharId}, 'LuGlob'))"));
        Check("ClearCharVarFromAll: every row gone, the char's cache 0", "0,0,0",
              Lua($"ClearCharVarFromAll('LuGlob') report(GetPlayerByID({CharId}):getCharVar('LuGlob')) report(GetCharVar({CharId}, 'LuGlob'))" +
                  $" report(GetCharVar({CharId + 1}, 'LuGlob'))"));
        Check("SendEntityVisualPacket for no such NPC: nothing", "", Lua("SendEntityVisualPacket(1, 'kesu')"));

        Sql(db, "DELETE FROM server_variables WHERE name IN ('LuVol', 'LuExpired')");
        Sql(db, "INSERT INTO server_variables VALUES ('LuExpired', 4, 1)");
        Check("GetServerVariable: an expired row reads 0 and is deleted", "0/-",
              Lua("report(GetServerVariable('LuExpired'))") + "/" + DbRow(db, "value", "FROM server_variables WHERE name = 'LuExpired' AND ? > 0"));
        Check("SetVolatileServerVariable: the cache answers at once, no row until the write-back", "7/-",
              Lua("SetVolatileServerVariable('LuVol', 7) report(GetVolatileServerVariable('LuVol'))") + "/" +
              DbRow(db, "value", "FROM server_variables WHERE name = 'LuVol' AND ? > 0"));
        Tick(61000);
        string volRow = DbRow(db, "value", "FROM server_variables WHERE name = 'LuVol' AND ? > 0");
        Lua("SetVolatileServerVariable('LuVol', 0)");
        Tick(61000);
        Check("PersistVolatileServerVars a minute on: the row; 0 deletes it at the next one", "7/-",
              volRow + "/" + DbRow(db, "value", "FROM server_variables WHERE name = 'LuVol' AND ? > 0"));

        Sql(db, "DELETE FROM delivery_box WHERE charid = ? AND sender = 'LuGlob'", BoundValue.Of(CharId));
        Check("SendItemToDeliveryBox: 50 fire crystals limited to the stack (1, a row of 12), nobody 2, no such item 3, gil 0", "1,2,3,0/12,1000",
              Lua("report(SendItemToDeliveryBox('Zonelua', 4096, 50, 'LuGlob')) report(SendItemToDeliveryBox('Nobody', 4096, 1, 'LuGlob'))" +
                  " report(SendItemToDeliveryBox('Zonelua', 65000, 1, 'LuGlob')) report(SendItemToDeliveryBox('Zonelua', 65535, 1000, 'LuGlob'))") + "/" +
              DbRow(db, "GROUP_CONCAT(quantity ORDER BY itemid)", "FROM delivery_box WHERE charid = ? AND sender = 'LuGlob'"));
        Sql(db, "DELETE FROM delivery_box WHERE charid = ? AND sender = 'LuGlob'", BoundValue.Of(CharId));
        Check("SendToJailOffline: the inJail var of the char", "2",
              Lua($"SendToJailOffline({CharId + 1}, 2, 1, 2, 3, 4)") + DbRow(db, "value", "FROM char_vars WHERE charid = ? + 1 AND varname = 'inJail'"));
        Sql(db, "DELETE FROM char_vars WHERE charid = ? AND varname = 'inJail'", BoundValue.Of(CharId + 1));
        Sql(db, "DELETE FROM linkshell_concierge WHERE zone_id = 999");
        Sql(db, "REPLACE INTO linkshells (linkshellid, name) VALUES (995999, 'LuGlobLs')");
        string Slots() => Lua("for _, s in ipairs(LoadLinkshellConciergeSlots(999)) do report(s.slotIndex .. ':' .. s.name .. ':' .. s.color .. ':' .. s.membersGoal .. ':' .. s.ownerCharId .. ':' .. s.flag) end");
        Lua($"SetLinkshellConciergeSlot(999, 3, {{ linkshellid = 995999, ownerCharId = {CharId}, membersGoal = 2, flag = 1 }})");
        string set = Slots();
        Lua("DecrementLinkshellConciergeMembersGoal(999, 995999)");
        string once = Slots();
        Lua("DecrementLinkshellConciergeMembersGoal(999, 995999)");
        string twice = Slots();
        Lua($"SetLinkshellConciergeSlot(999, 3, {{ linkshellid = 995999, ownerCharId = {CharId}, membersGoal = 2 }}) DeleteLinkshellConciergeSlot(999, 3)");
        Check("the linkshell concierge: a slot set (with its linkshell's name and color), one member fewer, none left gone, deleted",
              $"3:LuGlobLs:61440:2:{CharId}:1/3:LuGlobLs:61440:1:{CharId}:1//", $"{set}/{once}/{twice}/{Slots()}");
        Sql(db, "DELETE FROM linkshells WHERE linkshellid = 995999");
        got.Clear();
        zone.OnIpc(new XI.Ipc.LuaFunction { FuncString = "report('ran ' .. 6 * 7)" });
        Check("an ipc LuaFunction (SendLuaFuncStringToZone's): the string run on this process", "ran 42", string.Join(",", got));
        Sql(db, "REPLACE INTO char_vars (charid, varname, value) VALUES (?, '[Fish]LastCastTime', UNIX_TIMESTAMP())", BoundValue.Of(CharId));
        Check("GetRecentFishers: a char cast in the last minutes, by char id (name, level, zone)", $"Zonelua:{c.MLvl}:Lower_Delkfutts_Tower",
              Lua($"local f = GetRecentFishers(5)[{CharId}] report(f.playerName .. ':' .. f.jobLevel .. ':' .. f.zoneName)"));
        Sql(db, "DELETE FROM char_vars WHERE charid = ? AND varname = '[Fish]LastCastTime'", BoundValue.Of(CharId));
        Check("CLuaItem lookups: getILvl, getWeaponskillPointsNeeded, isSubType(CHARGED) of an enchanted item, not of a crystal", "119,300,true,false",
              Lua("report(GetReadOnlyItem(18566):getILvl()) report(GetReadOnlyItem(16735):getWeaponskillPointsNeeded())" +
                  " report(tostring(GetReadOnlyItem(10250):isSubType(xi.itemSubType.CHARGED))) report(tostring(GetReadOnlyItem(4096):isSubType(xi.itemSubType.CHARGED)))"));
        Check("an owned weapon's exdata: getWeaponskillPoints, getAugment, getTrialNumber, set / getAppraisalID; setSubType(AUGMENTED)",
              "261,5,3,4097,9,true",
              Lua($"local p = GetPlayerByID({CharId}) p:addItem(16735) local it = p:findItem(16735)" +
                  " it:setExDataRaw({ [0] = 5, [1] = 1, [2] = 5, [3] = 0x18, [10] = 1, [11] = 0x90 }) it:setAppraisalID(9)" +
                  " report(it:getWeaponskillPoints()) local a = it:getAugment(0) report(a[1]) report(a[2]) report(it:getTrialNumber())" +
                  " report(it:getAppraisalID()) it:setSubType(xi.itemSubType.AUGMENTED) report(tostring(it:isSubType(xi.itemSubType.AUGMENTED)))" +
                  " p:delItem(16735, 1)"));
        Check("CLuaItem:state: a stack in the inventory free, a lookup item free", "0,0",
              Lua($"local p = GetPlayerByID({CharId}) p:addItem(4096) report(p:findItem(4096):state()) report(GetReadOnlyItem(4096):state()) p:delItem(4096, 1)"));
        Check("CLuaZone:getUptime: the process's seconds", "true", Lua($"report(tostring(GetZone({LowerDelkfutt}):getUptime() >= 0))"));
        int logFrom = log.Count;
        Check("GarbageCollectStep / GarbageCollectFull log what they did; BuildString names the build", "True/True/csharp-",
              $"{(Lua("GarbageCollectStep()") == "" && log.FindIndex(logFrom, l => l == "Garbage Collected (Step)") >= 0)}/" +
              $"{(Lua("GarbageCollectFull()") == "" && log.FindIndex(logFrom, l => l == "Garbage Collected (Full)") >= 0)}/" +
              Lua("report(BuildString():sub(1, 7))"));
        Check("SelectDailyItem: a Mystery Box item of the food dial, the goblin junk for an unknown dial", "true,true",
              Lua($"local p = GetPlayerByID({CharId}) local food = SelectDailyItem(p, 2) report(tostring(food > 0 and bit.band(GetItemFlagsByID(food), 4) ~= 0))" +
                  " local junk = { [2542] = true, [2543] = true, [4324] = true, [4325] = true, [4328] = true, [4458] = true, [4495] = true, [4539] = true }" +
                  " report(tostring(junk[SelectDailyItem(p, 9)] == true))"));

        // sol2 under LuaJIT (sol.hpp unqualified_getter): an integral argument is static_cast<T>(llround(n)), half away
        // from zero; an enum one compat-5.3's lua_tointegerx (a whole number, else 0); a float / double passes through
        // for the C++ body (a static_cast<int32> truncates)
        Console.WriteLine("sol2's number conversions: integral llround, enum lua_tointegerx, double as the body casts");
        Check("an integral argument rounds half away from zero: setLocalVar (uint32) 2.5, 2.49, 0.5, -0.49, -2.5 (wrapped)",
              "3,2,1,0,4294967293",
              Lua($"local p = GetPlayerByID({CharId}) for _, n in ipairs({{ 2.5, 2.49, 0.5, -0.49, -2.5 }}) do p:setLocalVar('numeric', n) report(p:getLocalVar('numeric')) end" +
                  " p:setLocalVar('numeric', 0)"));
        Check("a global's integral argument: GetItemByID(4095.5) and (4096.49) are the fire crystal", "fire_crystal,fire_crystal",
              Lua("report(GetItemByID(4095.5):getName()) report(GetItemByID(4096.49):getName())"));
        Check("an int32 argument: setCharVar -2.5 is -3, 7.5 is 8", "-3,8",
              Lua($"local p = GetPlayerByID({CharId}) p:setCharVar('NumericVar', -2.5) report(p:getCharVar('NumericVar'))" +
                  " p:setCharVar('NumericVar', 7.5) report(p:getCharVar('NumericVar')) p:setCharVar('NumericVar', 0)"));
        Check("a variadic va[3].as<uint8>(): setPos's rotation 10.5 is 11, 10.49 is 10", "11,10",
              Lua($"local p = GetPlayerByID({CharId}) p:setPos(1, 0, 0, 10.5) report(p:getRotPos()) p:setPos(1, 0, 0, 10.49) report(p:getRotPos())"));
        Check("an enum argument (xi::FameArea): getFame(1.5) is area 0's, not 1's (truncated) nor 2's (rounded); 2.0 is area 2's",
              "100,300,100",
              Lua($"local p = GetPlayerByID({CharId}) p:setFame(0, 100) p:setFame(1, 200) p:setFame(2, 300)" +
                  " report(p:getFame(1.5)) report(p:getFame(2.0)) report(p:getFame(0.25)) p:setFame(0, 0) p:setFame(1, 0) p:setFame(2, 0)"));
        Check("a double argument the C++ truncates (physicalDmgTaken's static_cast<int32>): 100.9 and 100.5 are 100", "100,100",
              Lua($"local p = GetPlayerByID({CharId}) report(p:physicalDmgTaken(100.9)) report(p:physicalDmgTaken(100.5))"));

        Console.WriteLine("CheckTriggerAreas");
        // sendMenu: the mog house menu (0x02E, header only) and the auction house (0x04C Open, result 1)
        s.PacketList.Clear();
        Lua($"GetPlayerByID({CharId}):sendMenu(xi.menuType.MOOGLE)");
        Lua($"GetPlayerByID({CharId}):sendMenu(xi.menuType.AUCTION)");
        var auc = Of(0x04C);
        Check("sendMenu: the mog menu, the auction house open", "1/4/1/2.255.1",
              $"{Of(0x02E).Count}/{(Of(0x02E).Count > 0 ? Of(0x02E)[0].Buffer[1] * 2 : 0)}/{auc.Count}/" +
              (auc.Count > 0 ? $"{auc[0].Buffer[4]}.{auc[0].Buffer[5]}.{auc[0].Buffer[6]}" : "-"));

        // CheckForGearSet: an equip change runs xi.gear_sets.checkForGearSet (a stand-in here), then the real one cleanly
        Lua("xi.gear_sets.verifyCheck = xi.gear_sets.checkForGearSet\n" +
            "xi.gear_sets.checkForGearSet = function(player) report('gear sets ' .. player:getID()) end");
        got.Clear();
        Send(0x050, 8);
        Check("CheckForGearSet: the 0x050 unequip runs xi.gear_sets.checkForGearSet", $"gear sets {CharId}", string.Join(",", got));
        Lua("xi.gear_sets.checkForGearSet = xi.gear_sets.verifyCheck");
        int logBefore = log.Count;
        Send(0x050, 8);
        Check("the real checkForGearSet: no script error", 0, log.GetRange(logBefore, log.Count - logBefore).FindAll(l => l.Contains("CheckForGearSet")).Count);

        // the zone-in's 2.5 s timer has run by now: back into the loading window to see its gate
        c.LocalVars["ZoningIn"] = 1;
        c.X = 406; c.Y = -33.5f; c.Z = 86;
        Tick(200);
        Check("ZoningIn: no trigger area entered while loading in", "False -1", $"{zone.IsInTriggerArea(c, 1)} {c.CurrentEvent.EventId}");
        c.LocalVars["ZoningIn"] = 0;
        s.PacketList.Clear();
        Tick(100);
        Check("inside the 200 ms interval: nothing yet", "False", zone.IsInTriggerArea(c, 1));
        Tick(100);
        Check("area 1 entered: the cache, the char var 'option'", "True 1", $"{zone.IsInTriggerArea(c, 1)} {DbRow(db, "value", "FROM char_vars WHERE charid = ? AND varname = 'option'")}");
        Check("onTriggerAreaEnter's optional cutscene 4", $"4 {EventType.OptionalCutscene} True",
              $"{c.CurrentEvent.EventId} {c.CurrentEvent.Type} {Of(0x032).Count + Of(0x033).Count + Of(0x034).Count > 0}");
        Tick(200);
        Check("still inside: no second enter", "4", c.CurrentEvent.EventId);
        EventEnd(4, 0);
        Check("cs 4 declined: option back to 0", "-1 -", $"{c.CurrentEvent.EventId} {DbRow(db, "value", "FROM char_vars WHERE charid = ? AND varname = 'option'")}");
        c.X = 0; c.Y = 0; c.Z = 0;
        Tick(200);
        Check("area 1 left: the leave hook, ITriggerArea addCount / getCount", "False leave 1 1", $"{zone.IsInTriggerArea(c, 1)} {string.Join(",", got)}");

        Console.WriteLine("CZone::CharZoneOut: a zone change from inside area 2");
        c.X = 393; c.Y = -33.5f; c.Z = -46;
        Tick(200);
        Check("area 2 entered: option 2, cs 4", "True 2 4", $"{zone.IsInTriggerArea(c, 2)} {DbRow(db, "value", "FROM char_vars WHERE charid = ? AND varname = 'option'")} {c.CurrentEvent.EventId}");
        // option 1 porters the char to Middle Delkfutt's Tower
        EventEnd(4, 1);
        Tick(0);
        Check("the zone change: the area 2 leave, then onZoneOut", "leave 2 1,onZoneOut Zonelua", string.Join(",", got));
        Check("zoning to Middle Delkfutt's Tower: char_stats.zoning, the zone", $"1 {MiddleDelkfutt}", DbRow(db, "zoning, (SELECT pos_zone FROM chars WHERE charid = char_stats.charid)", "FROM char_stats WHERE charid = ?"));

        Console.WriteLine("a login with no cutscene, then a timeout");
        Sql(db, "UPDATE chars SET pos_zone = ?, pos_prevzone = 0, pos_x = 1, pos_y = 0, pos_z = 0 WHERE charid = ?", BoundValue.Of(LowerDelkfutt), BoundValue.Of(CharId));
        var s2 = new MapSession { CharId = CharId, AccountId = CharId };
        zone.OnLogin(s2);
        var c2 = (CharData)s2.Char;
        s = s2;
        Send(0x00A, 92, buf => BitConverter.GetBytes(CharId).CopyTo(buf, 12));
        login = Of(0x00A);
        if (login.Count == 1)
            Check("the 0x00A: no event, SendCount 0", "0 0 0", $"{BitConverter.ToUInt16(login[0].Buffer, 100)} {login[0].Buffer[31]} {BitConverter.ToUInt16(login[0].Buffer, 172)}");
        else Check("one 0x00A login", 1, login.Count);
        Check("no event, not locked, not moved", "-1 False 1", $"{c2.CurrentEvent.EventId} {c2.Locked} {c2.X}");
        Check("a fresh char: no trigger area cached", "False False", $"{zone.IsInTriggerArea(c2, 1)} {zone.IsInTriggerArea(c2, 2)}");
        got.Clear();
        zone.OnCharTimeout(s2, false);
        Check("the timeout's zone-out: onZoneOut only", "onZoneOut Zonelua", string.Join(",", got));

        Console.WriteLine("time_server: onZoneTick, the zone onGameHour / onGameDay / onTOTDChange, the JST and 2400 ms hooks, ServerStart");
        const ushort WestRonfaure = 100;
        zone.ZoneInfoOf(WestRonfaure);
        Lua("local z = xi.zones.West_Ronfaure.Zone " +
            "z.onZoneTick = function(zone) zoneTicks = (zoneTicks or 0) + 1 lastTickZone = zone:getID() end " +
            "z.onGameHour = function(zone) report('hour ' .. zone:getID()) end " +
            "z.onGameDay = function() report('day') end " +
            "z.onTOTDChange = function(totd) report('totd ' .. totd) end " +
            "xi.server.onTimeServerTick = function() report('server tick') end " +
            "xi.server.onJSTMidnight = function() report('midnight') end " +
            "xi.server.onServerStart = function() report('start') end " +
            "xi.znm.UpdateSanrakusMobs = function() report('sanraku') end " +
            "xi.znm.ZNMPopPriceDecay = function() report('decay') end");
        Tick(2400);
        string first = string.Join(",", got);
        Tick(400);
        string second = string.Join(",", got);
        got.Clear();
        Check("onZoneTick each tick with the zone; onTimeServerTick once per 2400 ms", "server tick//2,100",
              $"{first}/{second}/{Lua("report(zoneTicks) report(lastTickZone)")}");
        vanaNow = 8000 * VanaTime.DAY + 23 * VanaTime.HOUR;
        Tick(0);
        Check("the Vana'diel hour 23: onGameHour, onTOTDChange to NIGHT (7)", "hour 100,totd 7", string.Join(",", got.FindAll(x => x.StartsWith("hour") || x.StartsWith("totd") || x == "day")));
        vanaNow = 8001 * VanaTime.DAY;
        Tick(0);
        Check("the new day: onGameHour, onGameDay, onTOTDChange to MIDNIGHT (1)", "hour 100,day,totd 1", string.Join(",", got.FindAll(x => x.StartsWith("hour") || x.StartsWith("totd") || x == "day")));
        long jstMidnight = ((unixNow + 9 * 3600) / 86400 + 1) * 86400 - 9 * 3600;
        unixNow = jstMidnight + 1;
        Tick(0);
        Check("JST midnight: onJSTMidnight, UpdateSanrakusMobs, ZNMPopPriceDecay", "midnight,sanraku,decay",
              string.Join(",", got.FindAll(x => x is "midnight" or "sanraku" or "decay")));
        unixNow += 3600;
        Tick(0);
        string oneAm = string.Join(",", got.FindAll(x => x is "midnight" or "sanraku" or "decay"));
        unixNow += 3600;
        Tick(0);
        Check("JST 1:00 nothing, 2:00 the pop price decay", "/decay", $"{oneAm}/{string.Join(",", got.FindAll(x => x is "midnight" or "sanraku" or "decay"))}");
        Sql(db, "INSERT INTO char_vars(charid, varname, value, expiry) VALUES (?, 'expiredVar', 1, ?), (?, 'liveVar', 1, ?)",
            BoundValue.Of(CharId), BoundValue.Of((int)(unixNow - 10)), BoundValue.Of(CharId), BoundValue.Of((int)(unixNow + 1000)));
        got.Clear();
        zone.ServerStart();
        Check("ServerStart: onTOTDChange (MIDNIGHT), the expired var gone, the live one kept, onServerStart", "totd 1,start/0/1",
              $"{string.Join(",", got.FindAll(x => x.StartsWith("totd") || x == "start"))}/" +
              $"{DbRow(db, "COUNT(*)", "FROM char_vars WHERE charid = ? AND varname = 'expiredVar'")}/{DbRow(db, "COUNT(*)", "FROM char_vars WHERE charid = ? AND varname = 'liveVar'")}");

        // the zone change loads Middle Delkfutt's Tower, whose mob scripts' onMobInitialize give their mobs pets
        // (setPet, not ported): those errors are the destination's mob scripts', not this suite's
        Check("no script errors", "", string.Join("\n", log.FindAll(l => !l.Contains("global scripts") && !l.Contains("setPet") &&
                                                                       (l.Contains("lua:") || l.Contains("luautils::") || l.Contains("rror")))));
    }
}
