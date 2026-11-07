using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Common;
using XI.Map;
using XI.Ipc;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Battlefields.Live -- live gate for battlefields (ZoneHost.Battlefield.cs, Battlefield.cs): Ghelsba Outpost's
// handler and Wings of Fury (34) through the bindings its entry event uses: registerBattlefield loading the battlefield
// (bcnm_records' name and record, the time limit, the cap, onBattlefieldRegister / onBattlefieldInitialize with
// addGroups: Colo-colo and the Furies by name, one link party, superlinked, stationary and spawned, each with the
// Battlefield effect), the char inside (the clearance effect, the level restriction to 20, the timer packet), WAIT for
// a second registration, setEnteredBattlefield, getBattlefieldByInitiator, the tick (getTimeInside), a mob's enmity
// locking it (onBattlefieldStatusChange), the deaths reaching the group's allDeath (the armoury crate shown and in the
// battlefield), the win's cutscene timer, cleanup (onBattlefieldLeave's win event, the 10 s grace, the mobs despawned
// and released, the crate gone, the effects and the level restriction lifted, the timer cleared, bcnm_records' fastest
// time); then leaveBattlefield(EXIT) from an open battlefield, which is cleaned up with nobody inside. One WAR 75 in
// Ghelsba Outpost, with the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 993750;
    private const ushort GhelsbaOutpost = 140;
    private const ushort WingsOfFury = 34;
    private const uint ColoColo = 17350929, Fury1 = 17350930, Fury2 = 17350931, Crate = 17350932, HutDoor = 17350951;
    private const ushort CloudyOrb = 1551;
    private static (string Name, uint Time, uint Size)? _record;

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
        if (_record is { } r)
            Sql(db, "UPDATE bcnm_records SET fastestName = ?, fastestTime = ?, fastestPartySize = ? WHERE bcnmId = ?",
                BoundValue.Of(r.Name), BoundValue.Of(r.Time), BoundValue.Of(r.Size), BoundValue.Of(WingsOfFury));
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
            var r = Sql(db, "SELECT fastestName, fastestTime, fastestPartySize FROM bcnm_records WHERE bcnmId = ?", BoundValue.Of(WingsOfFury));
            if (r != null && r.Next()) _record = (r.Get<string>("fastestName"), r.Get<uint>("fastestTime"), r.Get<uint>("fastestPartySize"));
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
            BoundValue.Of(CharId), BoundValue.Of("bcnm" + CharId), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Challenger"), BoundValue.Of(GhelsbaOutpost), BoundValue.Of((ushort)0),
            BoundValue.Of(-164.0f), BoundValue.Of(-11.6f), BoundValue.Of(75.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 1500, 100, 1, 75)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 75)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));
        Sql(db, "UPDATE bcnm_records SET fastestName = 'Someone', fastestTime = 3600, fastestPartySize = 6 WHERE bcnmId = ?",
            BoundValue.Of(WingsOfFury));

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, AbilityTable.Load(db, _ => true, 1.0), stats), dataDir, true, log.Add);
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
        void Tick(int ticks = 1)
        {
            for (int t = 0; t < ticks; t++)
            {
                now += 400;
                zone.Tick();
            }
        }
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);
        var lua = scripts.Lua;
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local player = GetPlayerByID({CharId})\nlocal bf = player and player:getBattlefield()\n" +
                         $"local content = xi.battlefield.contents[{WingsOfFury}]\n" + code, "=verify_battlefields");
            return string.Join(",", got);
        }

        var info = zone.ZoneInfoOf(GhelsbaOutpost);
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
        Check("the WAR 75 in Ghelsba Outpost", "1/75/140", $"{c.MJob}/{c.MLvl}/{c.Zone}");

        List<BasicPacket> Sent(ushort opcode) => s.PacketList.Where(p => p.GetOpcode() == opcode).ToList();
        List<string> LuaErrors() => log.Where(l => l.Contains("lua") && (l.Contains("rror") || l.Contains("not ported"))).ToList();
        var mobs = new[] { ColoColo, Fury1, Fury2 }.Select(id => info.MobById[id]).ToArray();
        var crate = info.NpcById[Crate];

        Console.WriteLine("The handler");
        Check("Ghelsba Outpost holds battlefields, one at once (xi.battlefield.getMaxAreas)", "True/1", $"{info.BattlefieldHandler != null}/{info.BattlefieldHandler?.MaxBattlefields}");
        Check("Wings of Fury registered with xi.battlefield; no battlefield yet", "true,nil,-1,nil",
            Lua("report(tostring(content ~= nil)) report(tostring(player:getZone():getBattlefieldByInitiator(player:getID()))) " +
                "report(player:getBattlefieldID()) report(tostring(bf))"));

        Console.WriteLine("registerBattlefield: the battlefield loaded and the char inside");
        log.Clear();
        s.PacketList.Clear();
        Check("registerBattlefield answers CUTSCENE", "2", Lua("report(player:registerBattlefield(content.battlefieldId, 1, player:getID(), content))"));
        var bf = c.PBattlefield;
        Check("the char's battlefield: Wings of Fury in area 1, open", "True/34/1/0", $"{bf != null}/{bf?.Id}/{bf?.Area}/{bf?.Status}");
        if (bf == null)
        {
            foreach (var line in log) Console.WriteLine("  log: " + line);
            return;
        }
        Check("bcnm_records' name and record, the content's limits", "wings_of_fury/Someone/3600000/6/900000/3/20",
            $"{bf.Name}/{bf.RecordName}/{bf.RecordTimeMs}/{bf.RecordPartySize}/{bf.TimeLimitMs}/{bf.MaxParticipants}/{bf.LevelCap}");
        Check("the rules: subjobs allowed, EXP can be lost", Battlefield.RulesAllowSubjobs | Battlefield.RulesLoseExp, bf.Rules);
        Check("in the zone's handler, the char registered and entered", "1/True/True/1",
            $"{info.BattlefieldHandler.Battlefields.Count}/{bf.RegisteredPlayers.Contains(CharId)}/{bf.EnteredPlayers.Contains(CharId)}/{bf.PlayerCount}");
        var clearance = c.StatusEffects.GetStatusEffect(Battlefield.EffectBattlefield);
        Check("the clearance: the Battlefield effect for 34, area 1, the initiator, not yet entered", $"34/1/{CharId}/0",
            $"{clearance?.Power}/{clearance?.SubPower}/{clearance?.SubId}/{clearance?.Tier}");
        var cap = c.StatusEffects.GetStatusEffect(143);
        Check("the level restriction to 20, no subjob restriction", "20/20/False",
            $"{cap?.Power}/{c.LevelRestriction}/{c.StatusEffects.HasStatusEffect(157)}");
        var timer = Sent(0x075);
        Check("the timer packet: a countdown of 900 s", "1/1/900",
            $"{timer.Count}/{(timer.Count > 0 ? timer[0].Buffer[4 + S2cBattlefield.OffFlags] : -1)}/" +
            $"{(timer.Count > 0 ? BitConverter.ToUInt32(timer[0].Buffer, 4 + S2cBattlefield.OffDuration) : 0)}");
        Check("addGroups: Colo-colo and the two Furies, spawned and inside", "3/0/3/True/True",
            $"{bf.AdditionalEnemyList.Count + bf.RequiredEnemyList.Count}/{bf.RequiredEnemyList.Count}/{bf.AdditionalEnemyList.Count}/" +
            $"{mobs.All(m => m.IsSpawned && m.IsAlive)}/{mobs.All(m => m.PBattlefield == bf)}");
        Check("the mobs' Battlefield effect and bcnm ids", "True/True",
            $"{mobs.All(m => m.StatusEffects.GetStatusEffect(Battlefield.EffectBattlefield)?.Power == 34)}/{mobs.All(m => m.BcnmId == 34 && m.BattlefieldArea == 1)}");
        Check("the essential mobs: one link party, superlinked (1001), stationary", "True/3/1001,1001,1001/True",
            $"{mobs.All(m => m.LinkParty == mobs[0].LinkParty)}/{mobs[0].LinkParty?.Count}/" +
            $"{string.Join(",", mobs.Select(m => m.GetMobMod(MobMod.Superlink)))}/" +
            $"{mobs.All(m => m.MaxRoamDistance == 0.5f && (m.RoamFlags & RoamFlag.Scripted) != 0 && m.GetMobMod(MobMod.RoamResetFacing) == 1)}");
        Check("one group with an allDeath callback", "1/3/True", $"{bf.Groups.Count}/{bf.Groups[0].MobIds.Count}/{bf.Groups[0].AllDeathCallback != null}");
        Check("the CBattlefield methods", "34,140,1,900,3,1,3,Someone,3600,6,0," + CharId + ",Challenger",
            Lua("report(bf:getID()) report(bf:getZoneID()) report(bf:getArea()) report(bf:getTimeLimit()) report(bf:getMaxParticipants()) " +
                "report(bf:getPlayerCount()) report(#bf:getMobs(true, true)) local n, t, p = bf:getRecord() report(n) report(t) report(p) " +
                "report(bf:getStatus()) local id, name = bf:getInitiator() report(id) report(name)"));
        Check("getBattlefieldByInitiator, getRegisteredBattlefield, getBattlefieldID, a mob's battlefield", "34,34,34,34",
            Lua($"report(player:getZone():getBattlefieldByInitiator(player:getID()):getID()) report(player:getRegisteredBattlefield():getID()) " +
                $"report(player:getBattlefieldID()) report(GetMobByID({ColoColo}):getBattlefield():getID())"));
        Check("no Lua errors", "", string.Join(" | ", LuaErrors()));

        Console.WriteLine("Entering and a second registration");
        Lua("content:onEventFinishEnter(player, 32000, 0, nil)");
        Check("onEventFinishEnter: setEnteredBattlefield, hasEnteredBattlefield", "1,true",
            Lua("report(player:getStatusEffect(xi.effect.BATTLEFIELD):getTier()) report(tostring(player:hasEnteredBattlefield()))"));
        Check("a second registration while inside: WAIT", "1", Lua("report(player:registerBattlefield(content.battlefieldId, 2, player:getID(), content))"));
        Check("battlefieldAtCapacity: its one area taken", "true", Lua("report(tostring(player:battlefieldAtCapacity(34)))"));

        Console.WriteLine("The tick and the lock");
        Tick(3);
        Check("onTick: the time inside", "1", Lua("report(bf:getTimeInside())"));
        Lua($"GetMobByID({Fury1}):updateEnmity(player)");
        Tick();
        Check("a mob's enmity locks the battlefield: LOCKED, attacked, onBattlefieldStatusChange ran", "1/True/1",
            $"{bf.Status}/{bf.Attacked}/{bf.GetLocalVar("statusRemoval")}");

        Console.WriteLine("The deaths and the win");
        foreach (var m in mobs) m.Hp = 0;
        Tick();
        Check("the mobs died", "True", mobs.All(m => m.IsDead));
        Check("allDeath: the armoury crate shown inside the battlefield", "0/True/True",
            $"{crate.Status}/{crate.PBattlefield == bf}/{bf.NpcList.Contains(crate)}");
        Lua("bf:setStatus(xi.battlefield.status.WON) bf:setLocalVar('cutsceneTimer', 3)");
        Check("WON", "2", bf.Status);
        Tick(12);
        Check("the cutscene timer ran out: cleanup flagged, the players told they leave (the win event)", "True/True/34",
            $"{bf.CleanupFlag}/{bf.CleanedPlayers}/{c.LocalVars.GetValueOrDefault("battlefieldWin")}");
        Check("still inside for the 10 s grace", "True/True", $"{c.PBattlefield == bf}/{info.BattlefieldHandler.Battlefields.ContainsValue(bf)}");
        s.PacketList.Clear();
        Tick(30);
        Check("cleaned up: out of the handler, the char out", "0/True/0",
            $"{info.BattlefieldHandler.Battlefields.Count}/{c.PBattlefield == null}/{bf.EnteredPlayers.Count}");
        Check("the clearance and the level restriction lifted", "False/False/0",
            $"{c.StatusEffects.HasStatusEffect(Battlefield.EffectBattlefield)}/{c.StatusEffects.HasStatusEffect(143)}/{c.LevelRestriction}");
        timer = Sent(0x075);
        Check("the timer cleared", "True/0", $"{timer.Count > 0}/{(timer.Count > 0 ? timer[^1].Buffer[4 + S2cBattlefield.OffFlags] : -1)}");
        Check("the mobs released, the crate gone", "True/True/2",
            $"{mobs.All(m => m.PBattlefield == null)}/{crate.PBattlefield == null}/{crate.Status}");
        var rec = Sql(db, "SELECT fastestName, fastestTime, fastestPartySize FROM bcnm_records WHERE bcnmId = ?", BoundValue.Of(WingsOfFury));
        rec.Next();
        Check("bcnm_records: the faster win recorded", "Challenger/True/1",
            $"{rec.Get<string>("fastestName")}/{rec.Get<uint>("fastestTime") < 3600}/{rec.Get<uint>("fastestPartySize")}");
        Check("no Lua errors", "", string.Join(" | ", LuaErrors()));

        Console.WriteLine("leaveBattlefield from an open battlefield");
        Tick(40); // the mobs fade out
        Check("registerBattlefield again", "2", Lua("report(player:registerBattlefield(content.battlefieldId, 1, player:getID(), content))"));
        var bf2 = c.PBattlefield;
        Check("a new battlefield, open, the mobs back up", "True/0/True", $"{bf2 != null && bf2 != bf}/{bf2?.Status}/{mobs.All(m => m.IsSpawned && m.IsAlive)}");
        Lua("content:onEventFinishEnter(player, 32000, 0, nil)");
        // the char's timers are cleared on the way out (PAI->ClearTimerQueue); its queued actions are not
        Lua("leftTimer = false leftQueue = false player:timer(1000, function(p) leftTimer = true end) " +
            "player:queue(1000, function(p) leftQueue = true end)");
        Check("leaveBattlefield(EXIT): the char out, the clearance kept but no longer entered", "false/True/0/0",
            Lua("report(tostring(player:leaveBattlefield(xi.battlefield.leaveCode.EXIT)))") +
            $"/{c.StatusEffects.HasStatusEffect(Battlefield.EffectBattlefield)}/{c.StatusEffects.GetStatusEffect(Battlefield.EffectBattlefield)?.Tier}/{bf2?.PlayerCount}");
        Check("nobody inside: it can be cleaned up", "True/True", $"{c.PBattlefield == null}/{bf2?.CanCleanup()}");
        Tick(30);
        Check("RemoveEntity: the timer cleared, the queued action ran", "false/true",
            Lua("report(tostring(leftTimer)) report(tostring(leftQueue))").Replace(",", "/"));
        Check("cleaned up with nobody inside: the handler empty, the clearance gone, the mobs released", "0/False/True",
            $"{info.BattlefieldHandler.Battlefields.Count}/{c.StatusEffects.HasStatusEffect(Battlefield.EffectBattlefield)}/{mobs.All(m => m.PBattlefield == null)}");
        Check("no Lua errors", "", string.Join(" | ", LuaErrors()));

        Console.WriteLine("The entry NPC: the orb traded to the Hut Door, the arena asked for, the cutscene finished");
        Tick(40); // the mobs fade out
        var door = info.NpcById[HutDoor];
        // the first win's cutscene (32001) still open: finished, onEventFinishWin
        Check("the win cutscene still open from the first battlefield", "32001", c.IsInEvent ? c.CurrentEvent.EventId : 0);
        Send(0x05B, 20, b =>
        {
            BitConverter.GetBytes(door.Id).CopyTo(b, 4);
            BitConverter.GetBytes((ushort)32001).CopyTo(b, 0x12);
        });
        Check("finished", "False", c.IsInEvent);
        Check("a Cloudy Orb, not yet worn", "1,0", Lua($"report(player:addItem({CloudyOrb}) and 1 or 0) report(player:getWornUses({CloudyOrb}))"));
        var orbSlot = c.Items.First(i => i.Location == Container.Inventory && i.ItemId == CloudyOrb).Slot;
        log.Clear();
        s.PacketList.Clear();
        Send(0x036, 64, b =>
        {
            BitConverter.GetBytes(door.Id).CopyTo(b, 4);
            BitConverter.GetBytes(1u).CopyTo(b, 8);
            b[48] = (byte)orbSlot;
            BitConverter.GetBytes(door.TargId).CopyTo(b, 58);
            b[60] = 1;
        });
        Check("onEntryTrade: event 32000 with Wings of Fury (menu index 2) on offer", "32000/True",
            $"{(c.IsInEvent ? c.CurrentEvent.EventId : 0)}/{c.IsInEvent && (c.CurrentEvent.Params.GetValueOrDefault(3) & 1 << 2) != 0}");
        Check("the menu options kept", "4", Lua("report(player:getLocalVar('[BCNM]MenuOptions'))"));
        s.PacketList.Clear();
        // GP_CLI_COMMAND_EVENTENDXZY: the arena asked for, menu index 2 and area 1
        Send(0x05C, 32, b =>
        {
            BitConverter.GetBytes(-180.0f).CopyTo(b, 4);
            BitConverter.GetBytes(-10.0f).CopyTo(b, 8);
            BitConverter.GetBytes(60.0f).CopyTo(b, 12);
            BitConverter.GetBytes(door.Id).CopyTo(b, 16);
            BitConverter.GetBytes((uint)(2 << 4 | 1)).CopyTo(b, 20);
            BitConverter.GetBytes(door.TargId).CopyTo(b, 24);
            BitConverter.GetBytes((ushort)32000).CopyTo(b, 26);
            b[30] = 1; // Mode
            b[31] = 64;
        });
        var bf3 = c.PBattlefield;
        Check("onEntryEventUpdate: registered and inside Wings of Fury, the char moved into the arena", "True/34/1/-180",
            $"{bf3 != null}/{bf3?.Id}/{bf3?.Area}/{c.X}");
        var update = s.PacketList.LastOrDefault(x => x.GetOpcode() == 0x05C);
        Check("the event updated: CUTSCENE, menu index 2", "2/2",
            update == null ? "none" : $"{BitConverter.ToUInt32(update.Buffer, 4)}/{BitConverter.ToUInt32(update.Buffer, 8)}");
        Check("the orb worn once", "1", Lua($"report(player:getWornUses({CloudyOrb}))"));
        Send(0x05B, 20, b =>
        {
            BitConverter.GetBytes(door.Id).CopyTo(b, 4);
            BitConverter.GetBytes(0u).CopyTo(b, 0x08);
            BitConverter.GetBytes(door.TargId).CopyTo(b, 0x0C);
            BitConverter.GetBytes((ushort)32000).CopyTo(b, 0x12);
        });
        Check("onEventFinishEnter: the event over, entered", "False/True",
            $"{c.IsInEvent}/{Battlefield.HasPlayerEntered(c)}");
        Check("no Lua errors", "", string.Join(" | ", LuaErrors()));
    }
}
