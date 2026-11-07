using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.PlayerBindings.Live -- live gate for the CLuaBaseEntity methods in ZoneHost.PlayerBindings.cs: a spell
// scroll used through 0x037 (onItemCheck's canLearnSpell, onItemUse's addSpell: the spell list, MAGIC_DATA, "learns a
// new spell!", the char_spells row), canLearnSpell's 96 / 95, addSpell's silentLog, delSpell, openDoor and the door
// closing again, setCurrency / delCurrency, hasEquipped, getCursorTarget, getGender, hasVisitedZone, delItem,
// getPartySize, getContainerSize / changeContainerSize (ITEM_MAX, char_storage), canUseMisc and changeMusic; the
// learned abilities (canLearnAbility's 443 / 444, addLearnedAbility, delLearnedAbility), canEquipItem, unequipItem,
// getParty and engage; the other teleport types (outposts, runic portals, waypoints, eschan portals, the abyssea
// conflux: getTeleport, add / hasTeleport, char_unlocks); batch 3 (openSendBox, injectActionPacket, copyStatusEffect,
// entityVisualPacket, the unique events, setMaxHP, forMembersInRange, isMobType, delContainerItems, updateNPCHideTime,
// closeDoor, warp, getStorageItem, setNation, the GM levels, getEntity, getModelId, the costume, canUseAbilities,
// getSeals, sendEntityUpdateToPlayer, isPlayerInTriggerArea, setDamage); batch 4 (the merit points, updateEventString,
// seenKeyItem, independentAnimation, addUsedItem, checkSoloPartyAlliance, getMaxGearMod, setCampaignAllegiance);
// batch 5's char half (customMenu answered through a 0x0B6 tell to "_CUSTOM_MENU", checkImbuedItems,
// hasRaiseTractorMenu); batch 6 (setGMHidden / setWallhack / setFreezeFlag in the 0x037 and 0x00D, the equipment
// locks and encumbrance, getWornUses, findItems, the look getters, delSeals, getCharVarsWithPrefix, Bust and the runes,
// messagePublic / messageCombat, sendEmote, showPosition, enableEntities, showNPC, teleport, getData / resetData);
// batch 7's char and NPC half (renameEntity, setAllegiance / setPriorityRender in the packets, setNpcFlags, the flag
// getters, addSeals, the assault points and missions, delTitle, unseenKeyItem, hasLearnedAbility, setVolatileCharVar,
// setCostume2, delItemAt, the gear getters, restoreMP, the rune removals, hasBustEffect, getWorldAngle and the sides,
// getPartyMember, messageStandard, sendEmptyEntityUpdateToPlayer, hasListener, clearLocalVarsWithPrefix, die); batch 8
// (traverser stones, getCurrentGPItem, the storage slip getters, claimed deeds, sendPartialMissionLog, the maze, the
// contest getters, restoreFromChest); batch 9 (the objective packet, getItems, pet names, delCurrentQuest, getHistory); batch 10
// (addLinkpearl, the confrontation getters, the party effects, reloadParty, doWildCard, doRandomDeal, healingWaltz,
// takeSwipeLungeDamage, addDamageFromMultipliers, forceRezone); batch 11 (the job points, setUnityLeader, gotoPlayer /
// gotoEntity / bringPlayer / resetPlayer and the IPC they ride). A WHM 30 in Southern San d'Oria, with the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 990995;
    private const ushort SouthernSandoria = 230;
    private const ushort SpellCure = 1, SpellStone = 159, ScrollOfCure = 4609, Potion = 4112, AnniversaryRing = 15793,
                         FightersRoll = 98, BronzeCap = 12448, SalutaryRobe = 11340, WarpCudgel = 17040,
                         AshClub = 17024;

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
        Sql(db, "DELETE FROM linkshells WHERE name = 'VerifyBten'");
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

        try
        {
            CleanupInspect2(db);
            RunInspect2(db);
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FAIL exception: {ex}");
        }
        finally
        {
            CleanupInspect2(db);
        }

        try
        {
            CleanupItems2(db);
            RunItems2(db);
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FAIL exception: {ex}");
        }
        finally
        {
            CleanupItems2(db);
        }

        Console.WriteLine($"{_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    private static int Opcode(BasicPacket p) => p.Buffer[0] | (p.Buffer[1] & 1) << 8;

    // a char in Southern San d'Oria past its first login (the NEW_ADVENTURER title: no charCreate) that has seen the
    // RoV opening (Rhapsodies of Vana'diel, a zone-in cutscene in the starting cities: Resonance current, the other logs
    // as a new char has them), standing at the mog house exit (a char at 0,0,0 is moved there by the zone's onZoneIn)
    private static void InsertChar(IDatabase db, uint id, string name)
    {
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        var missions = new byte[15 * 70];
        for (int i = 0; i <= 3; i++) BitConverter.GetBytes((ushort)0xFFFF).CopyTo(missions, i * 70);
        BitConverter.GetBytes((ushort)101).CopyTo(missions, 6 * 70);
        BitConverter.GetBytes((ushort)1).CopyTo(missions, 13 * 70);
        missions[13 * 70 + 6] = 1;
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,moghouse,nation,gmlevel,titles,missions) " +
                "VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(name), BoundValue.Of(SouthernSandoria), BoundValue.Of((ushort)0),
            BoundValue.Of(159.5f), BoundValue.Of(-2.0f), BoundValue.Of(160.0f),
            BoundValue.Of(0u), BoundValue.Of((byte)0), BoundValue.Of((byte)0), BoundValue.Of(titles), BoundValue.Of(missions));
    }

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
        var loader = new CharLoader(db, Grades.Load(dataDir), AbilityTable.Load(db, _ => true), stats);
        var log = new List<string>();
        var zone = new ZoneHost(db, loader, dataDir, true, log.Add);
        var ipcSent = new List<XI.Ipc.IIpcMessage>();
        zone.SendIpc = ipcSent.Add;
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
            lua.DoString($"local player = GetPlayerByID({CharId})\n" + code, "=verify_playerbindings");
            return string.Join(",", got);
        }

        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("pb" + CharId), BoundValue.Of("x"), BoundValue.Of((byte)1));
        InsertChar(db, CharId, "Binder");
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl, sjob, slvl) VALUES (?, 300, 50, 3, 30, 0, 0)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs SET charid = ?, unlocked = 8, whm = 30", BoundValue.Of(CharId));
        Sql(db, "INSERT INTO char_storage(charid, inventory) VALUES (?, 30) ON DUPLICATE KEY UPDATE inventory = 30", BoundValue.Of(CharId));
        // a Hume female: getGender 0 (race % 2 ^ (race > 6): 1 for the male races)
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 2)", BoundValue.Of(CharId));
        void Give(byte slot, ushort itemId, uint qty) =>
            Sql(db, "INSERT INTO char_inventory(charid,location,slot,itemId,quantity,extra) VALUES (?,0,?,?,?,?)",
                BoundValue.Of(CharId), BoundValue.Of(slot), BoundValue.Of(itemId), BoundValue.Of(qty), BoundValue.Of(new byte[24]));
        Give(1, ScrollOfCure, 1);
        Give(2, ScrollOfCure, 1);
        Give(3, Potion, 1);
        Give(4, AnniversaryRing, 1);
        // a temporary item, for delContainerItems
        Sql(db, "INSERT INTO char_inventory(charid,location,slot,itemId,quantity,extra) VALUES (?,3,1,?,1,?)",
            BoundValue.Of(CharId), BoundValue.Of(Potion), BoundValue.Of(new byte[24]));
        Sql(db, "INSERT IGNORE INTO char_unlocks(charid) VALUES (?)", BoundValue.Of(CharId));
        Sql(db, "INSERT INTO char_equip(charid,equipslotid,slotid,containerid) VALUES (?,13,4,0)", BoundValue.Of(CharId));

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
        List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);
        string Messages() => string.Join(",", Of(0x029).ConvertAll(p => $"{BitConverter.ToInt32(p.Buffer, 12)}/{BitConverter.ToUInt16(p.Buffer, 24)}"));
        void Advance(long ms)
        {
            nowMs += ms;
            zone.Tick();
        }
        void Use(byte slot) => Send(0x037, 20, buf =>
        {
            BitConverter.GetBytes(c.TargId).CopyTo(buf, 12);
            buf[14] = slot;
        });
        bool Knows(ushort spell) => (c.SpellList[spell / 8] & 1 << spell % 8) != 0;
        string SpellRow(ushort spell) => Scalar(db, "SELECT COUNT(*) FROM char_spells WHERE charid = ? AND spellid = ?", BoundValue.Of(CharId), BoundValue.Of(spell));

        Console.WriteLine("a spell scroll: canLearnSpell in onItemCheck, addSpell in onItemUse");
        Check("cure not known yet", "False/0", $"{Knows(SpellCure)}/{SpellRow(SpellCure)}");
        Check("canLearnSpell: 0 for a WHM", "0", Lua($"report(player:canLearnSpell({SpellCure}))"));
        Check("canLearnSpell: 95 for stone (no job of the char's)", "95", Lua($"report(player:canLearnSpell({SpellStone}))"));
        s.PacketList.Clear();
        Use(1);
        Check("the scroll in use", true, c.UsingItem);
        s.PacketList.Clear();
        Advance(1100);
        Check("learned: the list, the row", "True/1", $"{Knows(SpellCure)}/{SpellRow(SpellCure)}");
        Check("learns a new spell! (23) and the MAGIC_DATA", "True/1", $"{Messages().Contains("0/23")}/{Of(0x0AA).Count}");
        Check("the scroll used up", null, Scalar(db, "SELECT quantity FROM char_inventory WHERE charid = ? AND location = 0 AND slot = 1", BoundValue.Of(CharId)));
        Advance(6000);
        s.PacketList.Clear();
        Use(2);
        Check("a second scroll: refused with 96, the spell already known", true, Messages().EndsWith("/96"));
        Check("not in use", false, c.UsingItem);
        Check("canLearnSpell: 96 once known", "96", Lua($"report(player:canLearnSpell({SpellCure}))"));

        Console.WriteLine("delSpell, then addSpell with silentLog");
        Lua($"player:delSpell({SpellCure})");
        Check("forgotten: the list, the row", "False/0", $"{Knows(SpellCure)}/{SpellRow(SpellCure)}");
        s.PacketList.Clear();
        Lua($"player:addSpell({SpellCure}, {{ silentLog = true }})");
        Check("silent: learned, no message, the MAGIC_DATA", "True/1//1", $"{Knows(SpellCure)}/{SpellRow(SpellCure)}/{Messages()}/{Of(0x0AA).Count}");
        Lua($"player:addSpell({SpellCure}, {{ saveToDB = false }})");
        Check("adding it again: nothing", "True/1", $"{Knows(SpellCure)}/{SpellRow(SpellCure)}");

        Console.WriteLine("openDoor: open, then shut again");
        var door = zone.ZoneInfoOf(SouthernSandoria).Npcs[0];
        door.Animation = 9; // closed
        Lua($"GetNPCByID({door.Id}):openDoor(3)");
        Check("open (animation 8)", 8, door.Animation);
        Advance(2000);
        Check("still open at 2 s", 8, door.Animation);
        Advance(1500);
        Check("closed after 3 s", 9, door.Animation);
        door.Animation = 0;
        Lua($"GetNPCByID({door.Id}):openDoor()");
        Check("not a closed door: left alone", 0, door.Animation);

        Console.WriteLine("currency, equipment, the cursor target, gender, zones visited");
        Check("setCurrency / delCurrency", "500/380",
            Lua("player:setCurrency('sandoria_cp', 500) report(player:getCurrency('sandoria_cp')) player:delCurrency('sandoria_cp', 120) report(player:getCurrency('sandoria_cp'))").Replace(",", "/"));
        Check("hasEquipped: the ring, not the potion", "true/false",
            Lua($"report(tostring(player:hasEquipped({AnniversaryRing}))) report(tostring(player:hasEquipped({Potion})))").Replace(",", "/"));
        Check("getCursorTarget: nothing targeted", "nil", Lua("report(tostring(player:getCursorTarget()))"));
        Send(0x015, 32, b => BitConverter.GetBytes(door.TargId).CopyTo(b, C2sPos.OffFacetarget));
        Check("getCursorTarget: the NPC faced", $"{door.Id}", Lua("report(player:getCursorTarget():getID())"));
        Check("getGender: a Hume female", "0", Lua("report(player:getGender())"));
        Check("hasVisitedZone: here, not West Ronfaure", "true/false",
            Lua($"report(tostring(player:hasVisitedZone({SouthernSandoria}))) report(tostring(player:hasVisitedZone(100)))").Replace(",", "/"));

        Console.WriteLine("delItem, getPartySize, the container size, canUseMisc, changeMusic");
        Check("delItem: the potion", "true/", Lua($"report(tostring(player:delItem({Potion}, 1)))") + "/" +
            Scalar(db, "SELECT quantity FROM char_inventory WHERE charid = ? AND location = 0 AND slot = 3", BoundValue.Of(CharId)));
        Check("delItem: none left", "false", Lua($"report(tostring(player:delItem({Potion}, 1)))"));
        Check("getPartySize alone", "1", Lua("report(player:getPartySize())"));
        Check("getContainerSize: inventory", "30", Lua("report(player:getContainerSize(0))"));
        s.PacketList.Clear();
        Check("changeContainerSize: +5", "35", Lua("player:changeContainerSize(0, 5) report(player:getContainerSize(0))"));
        Check("ITEM_MAX sent, char_storage saved", "1/35",
            $"{Of(0x01C).Count}/{Scalar(db, "SELECT inventory FROM char_storage WHERE charid = ?", BoundValue.Of(CharId))}");
        var info = zone.ZoneInfoOf(SouthernSandoria);
        Check("canUseMisc follows the zone's misc", $"{info.CanUseMisc(0x0200).ToString().ToLower()}/{info.CanUseMisc(0x0004).ToString().ToLower()}",
            Lua("report(tostring(player:canUseMisc(xi.zoneMisc.AUCTION_HOUSE))) report(tostring(player:canUseMisc(xi.zoneMisc.MOUNT)))").Replace(",", "/"));
        s.PacketList.Clear();
        Lua("player:changeMusic(0, 113)");
        var music = Of(0x05F);
        Check("changeMusic: GP_SERV_COMMAND_MUSIC slot 0, track 113", "1/0/113",
            $"{music.Count}/{(music.Count == 1 ? BitConverter.ToUInt16(music[0].Buffer, 4) : -1)}/{(music.Count == 1 ? BitConverter.ToUInt16(music[0].Buffer, 6) : -1)}");

        Console.WriteLine("the learned abilities (a corsair roll), canEquipItem, unequipItem, getParty, engage");
        string Learned()
        {
            var r = db.Execute("SELECT abilities FROM chars WHERE charid = ?", new[] { BoundValue.Of(CharId) });
            if (r == null || !r.Next() || r.IsNull("abilities")) return "False";
            var bits = r.GetBlobBytes("abilities");
            return (bits.Length > FightersRoll / 8 && (bits[FightersRoll / 8] & 1 << FightersRoll % 8) != 0).ToString();
        }
        Check("canLearnAbility: 443, Fighter's Roll is a COR 49's", "443", Lua($"report(player:canLearnAbility({FightersRoll}))"));
        s.PacketList.Clear();
        Lua($"player:addLearnedAbility({FightersRoll})");
        Check("learned: 444 now, the row, COMMAND_DATA and 442", "444/True/1/True",
            $"{Lua($"report(player:canLearnAbility({FightersRoll}))")}/{Learned()}/{Of(0x0AC).Count}/{Messages().Contains("0/442")}");
        s.PacketList.Clear();
        Lua($"player:addLearnedAbility({FightersRoll})");
        Check("learning it again: nothing sent", 0, Of(0x0AC).Count);
        Lua($"player:delLearnedAbility({FightersRoll})");
        Check("delLearnedAbility: 443 again, the row", "443/False", $"{Lua($"report(player:canLearnAbility({FightersRoll}))")}/{Learned()}");
        Check("canEquipItem: the ring yes, the bronze cap (no WHM) no", "true/false",
            Lua($"report(tostring(player:canEquipItem({AnniversaryRing}))) report(tostring(player:canEquipItem({BronzeCap})))").Replace(",", "/"));
        Check("canEquipItem: a level 40 robe, with and without the level check", "true/false",
            Lua($"report(tostring(player:canEquipItem({SalutaryRobe}))) report(tostring(player:canEquipItem({SalutaryRobe}, true)))").Replace(",", "/"));
        Lua("player:unequipItem(13)");
        Check("unequipItem: the ring off", "false", Lua($"report(tostring(player:hasEquipped({AnniversaryRing})))"));
        Check("getParty alone: itself", $"1/{CharId}", Lua("local p = player:getParty() report(#p .. '/' .. p[1]:getID())"));
        Check("engage on a target that is not there: nothing", "False", Lua("player:engage(1)") + $"{c.AttackTarget != null}");

        Console.WriteLine("the other teleport types");
        string Unlock(string col) => Scalar(db, $"SELECT {col} FROM char_unlocks WHERE charid = ?", BoundValue.Of(CharId));
        string UnlockHex(string col) => Scalar(db, $"SELECT HEX({col}) FROM char_unlocks WHERE charid = ?", BoundValue.Of(CharId));
        Lua("player:addTeleport(xi.teleport.type.OUTPOST_SANDORIA, 3) player:addTeleport(xi.teleport.type.OUTPOST_SANDORIA, 5)");
        Check("an outpost: the mask, hasTeleport, the row", "40/true/false/40",
            $"{Lua("report(player:getTeleport(xi.teleport.type.OUTPOST_SANDORIA))")}/{Lua("report(tostring(player:hasTeleport(xi.teleport.type.OUTPOST_SANDORIA, 3)))")}/" +
            $"{Lua("report(tostring(player:hasTeleport(xi.teleport.type.OUTPOST_BASTOK, 3)))")}/{Unlock("outpost_sandy")}");
        Lua("player:addTeleport(xi.teleport.type.RUNIC_PORTAL, 2) player:addTeleport(xi.teleport.type.PAST_MAW, 0)");
        Check("a runic portal, a maw", "4/true/1", $"{Unlock("runic_portal")}/{Lua("report(tostring(player:hasTeleport(xi.teleport.type.RUNIC_PORTAL, 2)))")}/{Unlock("maw")}");
        Lua("player:addTeleport(xi.teleport.type.WAYPOINT, 40)");
        Check("a waypoint past the first word: access[1] bit 8, the 12-byte blob", "true/false/000000000001000000000000",
            $"{Lua("report(tostring(player:hasTeleport(xi.teleport.type.WAYPOINT, 40)))")}/{Lua("report(tostring(player:hasTeleport(xi.teleport.type.WAYPOINT, 8)))")}/{UnlockHex("waypoints")}");
        // getTeleportTable / getTeleportMenu / setTeleportMenu for the waypoints: the two access words, the
        // { confirmation } menu (waypoint.lua's destConfirmation) and its byte in the blob after the access words
        Check("getTeleportTable(WAYPOINT): the two access words", "2/0/256",
            Lua("local t = player:getTeleportTable(xi.teleport.type.WAYPOINT) " +
                "report(#t .. '/' .. t[1] .. '/' .. t[2])"));
        const string WaypointMenu = "local m = player:getTeleportMenu(xi.teleport.type.WAYPOINT) " +
                                    "report(#m .. '/' .. tostring(m[1]) .. '/' .. (m[1] and 1 or 0))";
        Check("getTeleportMenu(WAYPOINT): { false }", "1/false/0", Lua(WaypointMenu));
        Lua("player:setTeleportMenu(xi.teleport.type.WAYPOINT, true)");
        Check("setTeleportMenu(WAYPOINT, true): the confirmation and the row", "1/true/1/000000000001000001000000",
            $"{Lua(WaypointMenu)}/{UnlockHex("waypoints")}");
        Lua("player:setTeleportMenu(xi.teleport.type.WAYPOINT, false)");
        Check("setTeleportMenu(WAYPOINT, false): cleared", "1/false/0/000000000001000000000000",
            $"{Lua(WaypointMenu)}/{UnlockHex("waypoints")}");
        const string OutOfBounds = "LuaBaseEntity::getteleport : Parameter 1 out of bounds.";
        const string MenuInvalid = "LuaBaseEntity::getTeleportMenu : Invalid Teleport Type passed to function.";
        const string SetMenuInvalid = "LuaBaseEntity::setteleportMenu : Invalid Teleport Type passed to function.";
        int outOfBounds = log.Count(l => l.Contains(OutOfBounds));
        Check("getTeleportTable / getTeleportMenu of an outpost: nil", "nil/nil",
            Lua("report(tostring(player:getTeleportTable(xi.teleport.type.OUTPOST_SANDORIA)) .. '/' .. " +
                "tostring(player:getTeleportMenu(xi.teleport.type.OUTPOST_SANDORIA)))"));
        Lua("player:setTeleportMenu(xi.teleport.type.OUTPOST_SANDORIA, {})");
        Check("the three logged as the C++ does", "1/True/True",
            $"{log.Count(l => l.Contains(OutOfBounds)) - outOfBounds}/{log.Exists(l => l.Contains(MenuInvalid))}/" +
            $"{log.Exists(l => l.Contains(SetMenuInvalid))}");
        Lua("player:addTeleport(xi.teleport.type.ESCHAN_PORTAL, 2)");
        Check("an eschan portal: getTeleport, the row; hasTeleport does not ask it", "4/4/false",
            $"{Lua("report(player:getTeleport(xi.teleport.type.ESCHAN_PORTAL))")}/{Unlock("eschan_portals")}/{Lua("report(tostring(player:hasTeleport(xi.teleport.type.ESCHAN_PORTAL, 2)))")}");
        Lua("player:addTeleport(xi.teleport.type.ABYSSEA_CONFLUX, 2, 1) player:addTeleport(xi.teleport.type.ABYSSEA_CONFLUX, 3)");
        Check("an abyssea conflux by region; none without a region", "4/0/0/000400000000000000",
            $"{Lua("report(player:getTeleport(xi.teleport.type.ABYSSEA_CONFLUX, 1))")}/{Lua("report(player:getTeleport(xi.teleport.type.ABYSSEA_CONFLUX, 0))")}/" +
            $"{Lua("report(player:getTeleport(xi.teleport.type.ABYSSEA_CONFLUX))")}/{UnlockHex("abyssea_conflux")}");
        Check("the conflux without a region logged", true, log.Exists(l => l.Contains("Attempt to add Abyssea Conflux")));

        Console.WriteLine("batch 3: delivery box, action / visual packets, status effects, unique events, health, party");
        s.PacketList.Clear();
        Lua("player:openSendBox()");
        Check("openSendBox: the send box open, the PBX result", $"{UContainerType.SendDeliveryBox}/1", $"{c.UContainer.Type}/{Of(0x04B).Count}");
        c.UContainer.Clean();
        s.PacketList.Clear();
        Lua($"player:injectActionPacket({CharId}, 4, 0, 0, 0, 0, 1, 0)");
        Check("injectActionPacket: a BATTLE2 to the char itself", 1, Of(0x028).Count);
        s.PacketList.Clear();
        Lua($"player:entityVisualPacket('chm1', GetNPCByID({door.Id}))");
        var visual = Of(0x039);
        Check("entityVisualPacket: 0x039 with the NPC and the id", $"1/{door.Id}/{door.TargId}/chm1",
            visual.Count == 1 ? $"1/{BitConverter.ToUInt32(visual[0].Buffer, 4)}/{BitConverter.ToUInt16(visual[0].Buffer, 16)}/{System.Text.Encoding.ASCII.GetString(visual[0].Buffer, 12, 4)}" : $"{visual.Count}");
        Lua("player:addStatusEffect(xi.effect.PROTECT, { power = 10, duration = 600, origin = player })");
        Advance(10000);
        Lua("player:copyStatusEffect(player:getStatusEffect(xi.effect.PROTECT))");
        var protect = c.StatusEffects.GetStatusEffect(40);
        Check("copyStatusEffect: the effect again, for the 590 s left", "590000/10/True", $"{protect?.DurationMs}/{protect?.Power}/{protect?.StartMs == nowMs}");
        Lua("player:setUniqueEvent(5)");
        Check("setUniqueEvent: completed, the blob", "true/20", $"{Lua("report(tostring(player:hasCompletedUniqueEvent(5)))")}/{UnlockHex("unique_event")?.Substring(0, 2)}");
        Lua("player:delUniqueEvent(5)");
        Check("delUniqueEvent", "false", Lua("report(tostring(player:hasCompletedUniqueEvent(5)))"));
        Lua("player:setMaxHP(777)");
        Check("setMaxHP: health.maxhp, UpdateHealth", "777/True", $"{c.BaseMaxHp}/{c.MaxHp >= 777}");
        Check("forMembersInRange alone: itself", $"{CharId}", Lua("player:forMembersInRange(10, function(m) report(m:getID()) end)"));
        Check("isMobType on a char: false", "false", Lua("report(tostring(player:isMobType(xi.mobType.NORMAL)))"));
        Check("canUseAbilities; not while stunned", "true/false",
            $"{Lua("report(tostring(player:canUseAbilities()))")}/{Lua("player:addStatusEffect(xi.effect.STUN, { power = 1, duration = 5, origin = player }) report(tostring(player:canUseAbilities()))")}");
        Lua("player:delStatusEffect(xi.effect.STUN)");

        Console.WriteLine("batch 3: containers, NPCs, the char's settings");
        Check("delContainerItems: the temporary item gone", "true/",
            Lua("report(tostring(player:delContainerItems(xi.inv.TEMPITEMS)))") + "/" +
            Scalar(db, "SELECT itemId FROM char_inventory WHERE charid = ? AND location = 3", BoundValue.Of(CharId)));
        Check("getStorageItem: the ring in inventory slot 4, an emptied slot", $"{AnniversaryRing}/nil",
            Lua("report(player:getStorageItem(0, 4, 255):getID()) report(tostring(player:getStorageItem(3, 1, 255)))").Replace(",", "/"));
        var hidden = zone.ZoneInfoOf(SouthernSandoria).Npcs[1];
        Lua($"GetNPCByID({hidden.Id}):hideNPC(100) GetNPCByID({hidden.Id}):updateNPCHideTime(2)");
        Check("updateNPCHideTime: hidden", NpcEntity.StatusDisappear, hidden.Status);
        Advance(2500);
        Check("back after 2 s, not 100", NpcEntity.StatusNormal, hidden.Status);
        door.Animation = 8;
        Lua($"GetNPCByID({door.Id}):closeDoor(2)");
        Check("closeDoor: shut", 9, door.Animation);
        Advance(2500);
        Check("open again after 2 s", 8, door.Animation);
        Lua("player:setNation(2)");
        Check("setNation: the nation, the row", "2/2", $"{Lua("report(player:getNation())")}/{Scalar(db, "SELECT nation FROM chars WHERE charid = ?", BoundValue.Of(CharId))}");
        Lua("player:setGMLevel(3) player:setVisibleGMLevel(4)");
        Check("the GM levels: the account's, the visible one, the row", "3/4/3",
            $"{Lua("report(player:getGMLevel())")}/{Lua("report(player:getVisibleGMLevel())")}/{Scalar(db, "SELECT gmlevel FROM chars WHERE charid = ?", BoundValue.Of(CharId))}");
        Lua("player:setGMLevel(0) player:setVisibleGMLevel(0)");
        Check("getEntity: the NPC at its act index, nothing at 0", $"{door.Id}/nil",
            $"{Lua($"report(player:getEntity({door.TargId}):getID())")}/{Lua("report(tostring(player:getEntity(0)))")}");
        Check("getModelId: the NPC's model, the char's face and race", $"{door.Look[1]}/{c.Look.ModelId}",
            $"{Lua($"report(GetNPCByID({door.Id}):getModelId())")}/{Lua("report(player:getModelId())")}");
        s.PacketList.Clear();
        Lua("player:setCostume(1500)");
        var status = Of(0x037);
        Check("setCostume: the costume, the 0x037 carrying it", "1500/1/1500",
            $"{Lua("report(player:getCostume())")}/{status.Count}/{(status.Count > 0 ? BitConverter.ToUInt16(status[^1].Buffer, 68) : -1)}");
        s.PacketList.Clear();
        Send(0x01A, 28, b =>
        {
            BitConverter.GetBytes(door.Id).CopyTo(b, 4);
            BitConverter.GetBytes(door.TargId).CopyTo(b, C2sAction.OffActIndex);
            BitConverter.GetBytes((ushort)ActionId.Talk).CopyTo(b, C2sAction.OffActionID);
        });
        Check("talking in a costume: released, no event", "1/False", $"{Of(0x052).Count}/{c.IsInEvent}");
        Lua("player:setCostume(0)");
        Lua("player:setCurrency('beastman_seal', 12)");
        Check("getSeals: beastman seals", "12", Lua("report(player:getSeals(0))"));
        s.PacketList.Clear();
        Lua($"player:sendEntityUpdateToPlayer(GetNPCByID({door.Id}), xi.entityUpdate.ENTITY_UPDATE, xi.updateType.UPDATE_POS)");
        Check("sendEntityUpdateToPlayer: the NPC's 0x00E", 1, Of(0x00E).FindAll(p => BitConverter.ToUInt32(p.Buffer, 4) == door.Id).Count);
        Check("isPlayerInTriggerArea: not in one", "false", Lua("report(tostring(player:isPlayerInTriggerArea(1)))"));
        Lua("player:setDamage(10, 0)");
        Check("setDamage on a char: refused", true, log.Exists(l => l.Contains("function call on invalid entity! (name: Binder)")));
        Lua("player:warp()");
        Check("warp: the request the zone tick serves", WarpRequest.Warp, c.RequestedWarp);
        c.RequestedWarp = WarpRequest.None;

        Console.WriteLine("batch 4: merits, event strings, key items seen, animations, a used item, the party, gear mods");
        s.PacketList.Clear();
        Lua("player:setMerits(5)");
        Check("setMerits: the points, the merit and monstrosity MISCDATA, char_exp", "5/3/5",
            $"{Lua("report(player:getMeritCount())")}/{Of(0x063).Count}/{Scalar(db, "SELECT merits FROM char_exp WHERE charid = ?", BoundValue.Of(CharId))}");
        Lua("player:setMerits(200)");
        Check("setMerits: no more than the bank holds", "30", Lua("report(player:getMeritCount())"));
        s.PacketList.Clear();
        Lua("player:updateEventString('Binder', 'a longer name than fits', nil, nil, 7, 8)");
        var pending = Of(0x05D);
        Check("updateEventString: 0x05D, the numbers, the strings cut at 15", "1/7/8/Binder/a longer name t",
            pending.Count == 1 ? $"1/{BitConverter.ToInt32(pending[0].Buffer, 4)}/{BitConverter.ToInt32(pending[0].Buffer, 8)}/" +
                                 $"{System.Text.Encoding.ASCII.GetString(pending[0].Buffer, 40, 16).TrimEnd('\0')}/{System.Text.Encoding.ASCII.GetString(pending[0].Buffer, 56, 16).TrimEnd('\0')}" : $"{pending.Count}");
        Lua("player:addKeyItem(xi.keyItem.ZERUHN_REPORT)");
        Check("seenKeyItem: held, not seen yet", "true/false",
            $"{Lua("report(tostring(player:hasKeyItem(xi.keyItem.ZERUHN_REPORT)))")}/{Lua("report(tostring(player:seenKeyItem(xi.keyItem.ZERUHN_REPORT)))")}");
        int zeruhn = int.Parse(Lua("report(xi.keyItem.ZERUHN_REPORT)"));
        c.KeyItems[zeruhn / 512 * 128 + 64 + zeruhn % 512 / 8] |= (byte)(1 << zeruhn % 8);
        Check("seenKeyItem: once seen", "true", Lua("report(tostring(player:seenKeyItem(xi.keyItem.ZERUHN_REPORT)))"));
        s.PacketList.Clear();
        Lua($"player:independentAnimation(GetNPCByID({door.Id}), 251, 4)");
        var magic = Of(0x03A);
        Check("independentAnimation: 0x03A to itself, the target, the file and type", $"1/{door.Id}/251/4",
            magic.Count == 1 ? $"1/{BitConverter.ToUInt32(magic[0].Buffer, 8)}/{BitConverter.ToUInt16(magic[0].Buffer, 16)}/{magic[0].Buffer[18]}" : $"{magic.Count}");
        uint usedBefore = LoginPackets.VanadielTimestamp(zone.UnixNow());
        string usedAdded = Lua($"report(tostring(player:addUsedItem({WarpCudgel})))");
        uint usedAt = c.Items.Find(i => i.ItemId == WarpCudgel) is Item cudgel ? BitConverter.ToUInt32(cudgel.Extra, 4) : 0;
        // the clock may tick over between the call and the reads around it
        Check("addUsedItem: a warp cudgel, used just now", "true/True",
            $"{usedAdded}/{usedAt >= usedBefore && usedAt <= LoginPackets.VanadielTimestamp(zone.UnixNow())}");
        Check("addUsedItem: not a charged item", "false/True",
            $"{Lua($"report(tostring(player:addUsedItem({Potion})))")}/{log.Exists(l => l.Contains("is not type ITEM_CHARGED"))}");
        Check("checkSoloPartyAlliance alone", "0", Lua("report(player:checkSoloPartyAlliance())"));
        Check("getMaxGearMod: nothing worn carries it", "0", Lua("report(player:getMaxGearMod(xi.mod.HP))"));
        Lua("player:setCampaignAllegiance(2)");
        Check("setCampaignAllegiance: the allegiance, the row", "2/2",
            $"{Lua("report(player:getCampaignAllegiance())")}/{Scalar(db, "SELECT campaign_allegiance FROM chars WHERE charid = ?", BoundValue.Of(CharId))}");

        Console.WriteLine("batch 5: the custom menu, imbued items, the raise / tractor menu");
        void Tell(string recipient, byte[] message)
        {
            int size = (0x15 + message.Length + 3) & ~3;
            Send(0x0B6, size, b =>
            {
                b[4] = 3;
                var name = System.Text.Encoding.ASCII.GetBytes(recipient);
                Array.Copy(name, 0, b, 6, Math.Min(name.Length, 15));
                Array.Copy(message, 0, b, 0x15, message.Length);
            });
        }
        s.PacketList.Clear();
        const string menu = "menuLog = '' player:customMenu({ title = 'Pick', " +
            "onStart = function(p) menuLog = menuLog .. 'start;' end, " +
            "onCancelled = function(p, byEvent) menuLog = menuLog .. 'cancelled ' .. tostring(byEvent) .. ';' end, " +
            "onEnd = function(p) menuLog = menuLog .. 'end;' end, " +
            "options = { { 'Red', function(p) menuLog = menuLog .. 'red;' end }, { 'Blue', function(p) menuLog = menuLog .. 'blue;' end } } })";
        Lua(menu);
        var prompt = Of(0x017);
        Check("customMenu: onStart, the GM prompt from _CUSTOM_MENU with the title and options quoted", "start;/1/12/_CUSTOM_MENU/\"Pick\"\"Red\"\"Blue\"",
            $"{Lua("report(menuLog)")}/{prompt.Count}/" + (prompt.Count == 1
                ? $"{prompt[0].Buffer[4]}/{System.Text.Encoding.ASCII.GetString(prompt[0].Buffer, 8, 15).TrimEnd('\0')}/{System.Text.Encoding.ASCII.GetString(prompt[0].Buffer, 23, prompt[0].Buffer.Length - 23).TrimEnd('\0')}"
                : ""));
        Tell("_CUSTOM_MENU", System.Text.Encoding.ASCII.GetBytes("GMTELL(Binder): Question(Pick): Result (Blue)"));
        Check("the answer: the option picked, onEnd", "start;blue;end;", Lua("report(menuLog)"));
        Tell("_CUSTOM_MENU", System.Text.Encoding.ASCII.GetBytes("GMTELL(Binder): Question(Pick): Result (Red)"));
        Check("answered once: a second tell to it goes out as a tell", "start;blue;end;", Lua("report(menuLog)"));
        Lua(menu);
        Tell("_CUSTOM_MENU", System.Text.Encoding.ASCII.GetBytes("GMTELL(Binder): Question(Pick): Result (Canceled.)"));
        Check("cancelled by the player: onCancelled(false), onEnd", "start;cancelled false;end;", Lua("report(menuLog)"));
        Lua(menu);
        Tell("_CUSTOM_MENU", System.Text.Encoding.ASCII.GetBytes("Currently in an event. This GMTELL was invalidated."));
        Check("cancelled by an event: onCancelled(true)", "start;cancelled true;end;", Lua("report(menuLog)"));
        Check("checkImbuedItems: none", "false", Lua("report(tostring(player:checkImbuedItems()))"));
        c.Items.Add(new Item { ItemId = 5370, Quantity = 1, Location = Container.MogSafe, Slot = 9 });
        Check("checkImbuedItems: one in the safe", "true", Lua("report(tostring(player:checkImbuedItems()))"));
        c.Items.RemoveAll(i => i.ItemId == 5370);
        Check("hasRaiseTractorMenu: none open, then a raise offered", "false/true",
            $"{Lua("report(tostring(player:hasRaiseTractorMenu()))")}/{(c.HasRaise = 1) != 0 && Lua("report(tostring(player:hasRaiseTractorMenu()))") == "true"}".Replace("True", "true"));
        c.HasRaise = 0;

        Console.WriteLine("batch 6: the GM flags, equipment locks, items, points and vars, messages, emotes, NPCs, teleport, data");
        Sql(db, "INSERT IGNORE INTO char_flags(charid) VALUES (?)", BoundValue.Of(CharId));
        s.PacketList.Clear();
        Lua("player:setGMHidden(true) player:setWallhack(true) player:setFreezeFlag(true)");
        zone.AfterParse(s);
        string Bits(uint v, params int[] bits) => string.Join("", Array.ConvertAll(bits, b => (v >> b & 1).ToString()));
        string StatusFlags()
        {
            var st = Of(0x037);
            return st.Count == 0 ? "none" : $"{Bits(BitConverter.ToUInt32(st[^1].Buffer, 44), 12, 13, 15)}{Bits(BitConverter.ToUInt32(st[^1].Buffer, 56), 2)}";
        }
        string UpdateFlags()
        {
            var p = LoginPackets.CharPc(c, zone.ZoneInfoOf(SouthernSandoria), EntityUpdate.Update, 0x05);
            return $"{Bits(BitConverter.ToUInt32(p.Buffer, 24), 15)}{Bits(BitConverter.ToUInt32(p.Buffer, 32), 27, 29)}{Bits(BitConverter.ToUInt32(p.Buffer, 40), 21)}";
        }
        string HiddenRow() => Scalar(db, "SELECT gmHiddenEnabled FROM char_flags WHERE charid = ?", BoundValue.Of(CharId));
        Check("on: the 0x037's Hackmove, FreezeFlag, InvisFlag, SilenceFlag; the 0x00D's GroundFlag, HackMove, InvisFlag, SilenceFlag; the row",
            "1111/1111/1", $"{StatusFlags()}/{UpdateFlags()}/{HiddenRow()}");
        s.PacketList.Clear();
        Lua("player:setGMHidden(false) player:setWallhack(false) player:setFreezeFlag(false)");
        zone.AfterParse(s);
        Check("off again", "0000/0000/0", $"{StatusFlags()}/{UpdateFlags()}/{HiddenRow()}");
        Lua("player:addStatusEffect(xi.effect.SNEAK, { power = 1, duration = 60, origin = player })");
        Check("sneak: silent, not invisible", "0001", UpdateFlags());
        Lua("player:delStatusEffect(xi.effect.SNEAK)");

        void Equip(byte invSlot, byte equipSlot) => Send(0x050, 8, b =>
        {
            b[4] = invSlot;
            b[5] = equipSlot;
            b[6] = 0;
        });
        uint Encumbrance() => Of(0x01B) is { Count: > 0 } ji ? BitConverter.ToUInt32(ji[^1].Buffer, 96) : 0xFFFFFFFF;
        Equip(4, 13);
        Check("hasSlotEquipped: the ring, nothing in the main hand", "true/false",
            Lua("report(tostring(player:hasSlotEquipped(xi.slot.RING1))) report(tostring(player:hasSlotEquipped(xi.slot.MAIN)))").Replace(",", "/"));
        s.PacketList.Clear();
        Lua("player:lockEquipSlot(xi.slot.RING1)");
        Check("lockEquipSlot: the ring off, the look, the slot, the encumbrance", $"false/1/1/{1 << 13}",
            $"{Lua("report(tostring(player:hasSlotEquipped(xi.slot.RING1)))")}/{Of(0x051).Count}/{Of(0x050).Count}/{Encumbrance()}");
        Equip(4, 13);
        Check("a locked slot cannot be equipped", "false", Lua("report(tostring(player:hasSlotEquipped(xi.slot.RING1)))"));
        s.PacketList.Clear();
        Lua("player:unlockEquipSlot(xi.slot.RING1)");
        Equip(4, 13);
        Check("unlocked: it goes on again", "0/true", $"{Encumbrance()}/{Lua("report(tostring(player:hasSlotEquipped(xi.slot.RING1)))")}");
        Lua("player:setEquipBlock(0x2000)");
        Equip(0, 13);
        Check("setEquipBlock: the slot locked against taking the ring off too", "true", Lua("report(tostring(player:hasSlotEquipped(xi.slot.RING1)))"));
        s.PacketList.Clear();
        Lua("player:setStatDebilitation(5)");
        Check("setStatDebilitation: the encumbrance's high half", $"{0x2000 | 5 << 16}", $"{Encumbrance()}");
        Lua("player:setStatDebilitation(0) player:setEquipBlock(0)");

        c.Items.Find(i => i.ItemId == AnniversaryRing).Extra[0] = 2;
        Check("getWornUses: the ring's use count; none held", "2/0", $"{Lua($"report(player:getWornUses({AnniversaryRing}))")}/{Lua($"report(player:getWornUses({Potion}))")}");
        c.Items.Find(i => i.ItemId == AnniversaryRing).Extra[0] = 0;
        Check("findItems: the ring anywhere, none in the safe", $"1/{AnniversaryRing}/0",
            Lua($"local t = player:findItems({AnniversaryRing}) report(#t .. '/' .. t[1]:getID() .. '/' .. #player:findItems({AnniversaryRing}, xi.inv.MOGSAFE))"));
        Check("getEquipmentModelIds", $"{c.Look.Head}/{c.Look.Body}/{c.Look.Hands}/{c.Look.Main}/{c.Look.Sub}",
            Lua("local t = player:getEquipmentModelIds() report(t.head .. '/' .. t.body .. '/' .. t.hands .. '/' .. t.main .. '/' .. t.sub)"));
        Check("getSize, getAverageItemLevel, getAllianceSize alone, getBaseSpeed, isAlly", $"{c.Look.Size}/{CharStats.ItemLevelDifference(c) + 99}/1/50/false",
            Lua("report(player:getSize() .. '/' .. player:getAverageItemLevel() .. '/' .. player:getAllianceSize() .. '/' .. player:getBaseSpeed() .. '/' .. tostring(player:isAlly()))"));
        Lua("player:delSeals(5, 0)");
        Check("delSeals: 12 beastman seals less 5", "7", Lua("report(player:getSeals(0))"));
        Lua("player:setCharVar('b6Test_a', 3) player:setCharVar('b6Test_b', 4) player:setCharVar('otherB6', 1)");
        Check("getCharVarsWithPrefix: the two, not the other", "3/4/nil",
            Lua("local t = player:getCharVarsWithPrefix('b6Test_') report(t.b6Test_a .. '/' .. t.b6Test_b .. '/' .. tostring(t.otherB6))"));
        Check("getMogHouseOwner: not in one", "nil", Lua("report(tostring(player:getMogHouseOwner()))"));
        Check("numBustEffects: none, then one", "0/1",
            $"{Lua("report(player:numBustEffects())")}/{Lua("player:addStatusEffect(xi.effect.BUST, { power = 1, duration = 60, origin = player }) report(player:numBustEffects())")}");
        Lua("player:delStatusEffect(xi.effect.BUST)");
        Check("no runes: none counted, effect 0, an empty list", "0/0/0",
            Lua("report(player:getActiveRuneCount() .. '/' .. player:getHighestRuneEffect() .. '/' .. #player:getAllRuneEffects())"));
        Lua("player:addStatusEffect(xi.effect.GELUS, { power = 1, duration = 60, origin = player }) " +
            "player:addStatusEffect(xi.effect.IGNIS, { power = 1, duration = 60, origin = player })");
        Check("two runes: counted, both listed", "2/2", Lua("report(player:getActiveRuneCount() .. '/' .. #player:getAllRuneEffects())"));
        Lua("player:delStatusEffect(xi.effect.GELUS) player:delStatusEffect(xi.effect.IGNIS)");

        s.PacketList.Clear();
        Lua("player:messagePublic(10, player, 5, 6)");
        Check("messagePublic: the BATTLE_MESSAGE to the char too", "5/10", Messages());
        s.PacketList.Clear();
        Lua($"player:messageCombat(nil, 7, 8, 11) player:messageCombat(GetNPCByID({door.Id}), 1, 2, 12)");
        var combat = Of(0x02D).ConvertAll(p => $"{BitConverter.ToUInt32(p.Buffer, 4)}:{BitConverter.ToInt32(p.Buffer, 16)}:{BitConverter.ToUInt16(p.Buffer, 24)}");
        Check("messageCombat: from itself, from the NPC", $"{CharId}:7:11,{door.Id}:1:12", string.Join(",", combat));
        s.PacketList.Clear();
        Lua($"player:sendEmote(GetNPCByID({door.Id}), 8, 0, false)");
        var emote = Of(0x05A);
        Check("sendEmote: the MOTIONMES to itself, at the NPC", $"1/{CharId}/{door.Id}/8",
            emote.Count == 1 ? $"1/{BitConverter.ToUInt32(emote[0].Buffer, 4)}/{BitConverter.ToUInt32(emote[0].Buffer, 8)}/{BitConverter.ToUInt16(emote[0].Buffer, 16)}" : $"{emote.Count}");
        s.PacketList.Clear();
        Lua($"player:sendEmote(nil, 8, 0, true)");
        Check("sendEmote othersOnly: not to itself", 0, Of(0x05A).Count);
        s.PacketList.Clear();
        Lua("player:showPosition()");
        var compass = Of(0x009);
        Check("showPosition: the compass message with the position", $"1/239/Para0 {(uint)(int)c.X} ",
            compass.Count == 1 ? $"1/{BitConverter.ToUInt16(compass[0].Buffer, 10)}/{System.Text.Encoding.ASCII.GetString(compass[0].Buffer, 13, $"Para0 {(uint)(int)c.X} ".Length)}" : $"{compass.Count}");
        s.PacketList.Clear();
        Lua($"player:enableEntities({{ {door.Id}, 17 }})");
        var vis = Of(0x077);
        Check("enableEntities: 0x077 with the ids", $"1/1/{door.Id}/17/0",
            vis.Count == 1 ? $"1/{vis[0].Buffer[4]}/{BitConverter.ToUInt32(vis[0].Buffer, 8)}/{BitConverter.ToUInt32(vis[0].Buffer, 12)}/{BitConverter.ToUInt32(vis[0].Buffer, 16)}" : $"{vis.Count}");
        hidden.Status = NpcEntity.StatusDisappear;
        Lua($"GetNPCByID({hidden.Id}):showNPC(2)");
        Check("showNPC: shown", NpcEntity.StatusNormal, hidden.Status);
        Advance(2500);
        Check("gone again after 2 s", NpcEntity.StatusDisappear, hidden.Status);
        hidden.Status = NpcEntity.StatusNormal;
        s.PacketList.Clear();
        Lua("player:teleport({ x = 10, y = -2, z = 30 }, 64)");
        var wpos = Of(0x05B);
        Check("teleport: moved and turned, the WPOS", "10/-2/30/64/1/10/64",
            $"{c.X}/{c.Y}/{c.Z}/{c.Rotation}/{wpos.Count}/{(wpos.Count > 0 ? BitConverter.ToSingle(wpos[0].Buffer, 4) : -1)}/{(wpos.Count > 0 ? wpos[0].Buffer[4 + 19] : -1)}");
        Lua("player:getData().foo = 5");
        Check("getData: kept between calls, apart from the NPC's", "5/nil",
            $"{Lua("report(player:getData().foo)")}/{Lua($"report(tostring(GetNPCByID({door.Id}):getData().foo))")}");
        Lua("player:resetData()");
        Check("resetData: a fresh table", "nil", Lua("report(tostring(player:getData().foo))"));

        Console.WriteLine("batch 7: renaming, allegiance, flags, seals and assaults, titles, key items, items, runes, angles, the party, vars");
        Lua($"GetNPCByID({door.Id}):renameEntity('Gate', true)");
        var renamed = EntityUpdatePacket.Npc(door, EntityUpdate.Update, EntityUpdatePacket.UpdatePos);
        Check("renameEntity: the packet name, sent with every update after the 0x01 marker", "Gate/8/1/Gate",
            $"{Lua($"report(GetNPCByID({door.Id}):getPacketName())")}/{renamed.Buffer[0x0A] & 0x08}/{BitConverter.ToUInt16(renamed.Buffer, 0x34) & 0xFF}/" +
            System.Text.Encoding.ASCII.GetString(renamed.Buffer, 0x35, 4));
        door.IsRenamed = false;
        door.PacketName = "";
        Lua("player:renameEntity('Nope')");
        Check("renameEntity on a char: refused", true, log.Exists(l => l.Contains("Renaming player character entities isn't supported.")));
        s.PacketList.Clear();
        Lua("player:setAllegiance(2) player:setPriorityRender(true)");
        zone.AfterParse(s);
        var st7 = Of(0x037);
        var up7 = LoginPackets.CharPc(c, zone.ZoneInfoOf(SouthernSandoria), EntityUpdate.Update, 0x04);
        Check("setAllegiance / setPriorityRender: the 0x037's BallistaFlg and CliPriorityFlag, the 0x00D's", "2/1/2/1",
            st7.Count == 0 ? "none" : $"{BitConverter.ToUInt32(st7[^1].Buffer, 52) >> 21 & 0xFF}/{BitConverter.ToUInt32(st7[^1].Buffer, 52) >> 20 & 1}/" +
                                      $"{BitConverter.ToUInt32(up7.Buffer, 40) >> 8 & 0xFF}/{BitConverter.ToUInt32(up7.Buffer, 40) >> 5 & 1}");
        Lua("player:setAllegiance(1) player:setPriorityRender(false)");
        uint doorFlags = door.EntityFlags;
        Lua($"GetNPCByID({door.Id}):setNpcFlags(0x123)");
        Check("setNpcFlags", 0x123u, door.EntityFlags);
        door.EntityFlags = doorFlags;
        Check("getWallhack, getGMHidden, isJailed, isSeekingParty, getContinentID", $"false/false/false/{c.InviteFlg.ToString().ToLower()}/1",
            Lua("report(tostring(player:getWallhack()) .. '/' .. tostring(player:getGMHidden()) .. '/' .. tostring(player:isJailed()) .. '/' .. " +
                "tostring(player:isSeekingParty()) .. '/' .. player:getContinentID())"));
        Check("getLastOnline, getTimeCreated", $"{(uint)c.LastOnline}/{Scalar(db, "SELECT UNIX_TIMESTAMP(timecreated) FROM chars WHERE charid = ?", BoundValue.Of(CharId))}",
            Lua("report(player:getLastOnline() .. '/' .. player:getTimeCreated())"));
        Lua("player:addSeals(3, 0)");
        Check("addSeals: 7 beastman seals and 3", "10", Lua("report(player:getSeals(0))"));
        Lua("player:addAssaultPoint(0, 50) player:delAssaultPoint(0, 20)");
        Check("assault points: 50 less 20 in Leujaoam, none in Ilrusi", "30/0", Lua("report(player:getAssaultPoint(0) .. '/' .. player:getAssaultPoint(4))"));
        s.PacketList.Clear();
        Lua("player:addAssault(5)");
        int Port(int port) => Of(0x056).FindAll(p => BitConverter.ToUInt16(p.Buffer, 36) == port).Count;
        Check("addAssault: current, the ToAU offer page", "5/false/1", $"{Lua("report(player:getCurrentAssault() .. '/' .. tostring(player:hasCompletedAssault(5)))")}/{Port(0x80)}");
        s.PacketList.Clear();
        Lua("player:completeAssault(5)");
        Check("completeAssault: none current, completed, both pages, the blob", "0/true/1/1/1",
            $"{Lua("report(player:getCurrentAssault() .. '/' .. tostring(player:hasCompletedAssault(5)))")}/{Port(0x80)}/{Port(0xC0)}/" +
            Scalar(db, "SELECT ASCII(SUBSTRING(assault, 8, 1)) FROM chars WHERE charid = ?", BoundValue.Of(CharId)));
        Lua("player:addAssault(6) player:delAssault(6)");
        Check("delAssault: none current", "0", Lua("report(player:getCurrentAssault())"));
        s.PacketList.Clear();
        Lua("player:addTitle(7)");
        Check("addTitle: the current title, its bit and a CLISTATUS", "7/true/1",
            $"{c.Title}/{Lua("report(tostring(player:hasTitle(7)))")}/{Of(0x061).Count}");
        Lua("player:delTitle(7)");
        Check("delTitle: the bit and the current title gone", "false/0", $"{Lua("report(tostring(player:hasTitle(7)))")}/{c.Title}");
        Lua("player:unseenKeyItem(xi.keyItem.ZERUHN_REPORT)");
        Check("unseenKeyItem: held, no longer seen", "true/false",
            $"{Lua("report(tostring(player:hasKeyItem(xi.keyItem.ZERUHN_REPORT)))")}/{Lua("report(tostring(player:seenKeyItem(xi.keyItem.ZERUHN_REPORT)))")}");
        Check("hasLearnedAbility: learned, then not", "true/false",
            $"{Lua($"player:addLearnedAbility({FightersRoll}) report(tostring(player:hasLearnedAbility({FightersRoll})))")}/" +
            $"{Lua($"player:delLearnedAbility({FightersRoll}) report(tostring(player:hasLearnedAbility({FightersRoll})))")}");
        Lua("player:setVolatileCharVar('b7Volatile', 9)");
        Check("setVolatileCharVar: read back, never saved", "9/0",
            $"{Lua("report(player:getCharVar('b7Volatile'))")}/{Scalar(db, "SELECT COUNT(*) FROM char_vars WHERE charid = ? AND varname = 'b7Volatile'", BoundValue.Of(CharId))}");
        s.PacketList.Clear();
        Lua("player:setCostume2(100)");
        var grap = Of(0x051);
        Check("setCostume2: the look list with the costume first and no ranged model", "100/1/100/65535",
            $"{Lua("report(player:getCostume2())")}/{grap.Count}/{(grap.Count > 0 ? BitConverter.ToUInt16(grap[0].Buffer, 4) : -1)}/{(grap.Count > 0 ? BitConverter.ToUInt16(grap[0].Buffer, 20) : -1)}");
        Lua("player:setCostume2(0)");
        const ushort DistilledWater = 4509; // stacks to 12
        Lua($"player:addItem({DistilledWater}, 3)");
        var water = c.Items.Find(i => i.ItemId == DistilledWater && i.Location == Container.Inventory);
        Check("delItemAt: two of the three; the wrong item there: nothing", "true/1/false",
            $"{Lua($"report(tostring(player:delItemAt({DistilledWater}, 2, 0, {water?.Slot ?? 0})))")}/{c.FindItem(Container.Inventory, water?.Slot ?? 0)?.Quantity}/" +
            $"{Lua($"report(tostring(player:delItemAt({AnniversaryRing}, 1, 0, {water?.Slot ?? 0})))")}");
        var ringMods = stats.Items.Equipment(AnniversaryRing).Mods;
        int ringMod = ringMods.Count > 0 ? (int)ringMods[0].Mod : (int)Mod.HP;
        Check("getGearModFromSlot: the ring's own mod", $"{ringMods.Where(x => (int)x.Mod == ringMod).Sum(x => x.Value)}", Lua($"report(player:getGearModFromSlot(xi.slot.RING1, {ringMod}))"));
        Check("getAmmoDmg, getWeaponSkillLevel: nothing held; getRATT; getBaseHP / MP", $"0/0/{(ushort)stats.Ratt(c)}/{c.BaseMaxHp}/{c.BaseMaxMp}",
            Lua("report(player:getAmmoDmg() .. '/' .. player:getWeaponSkillLevel(0) .. '/' .. player:getRATT() .. '/' .. player:getBaseHP() .. '/' .. player:getBaseMP())"));
        c.Mp = 0;
        Check("restoreMP", $"10/10", $"{Lua("report(player:restoreMP(10))")}/{c.Mp}");
        Lua("player:addStatusEffect(xi.effect.IGNIS, { power = 1, duration = 60, origin = player })");
        Advance(1000);
        Lua("player:addStatusEffect(xi.effect.GELUS, { power = 1, duration = 60, origin = player })");
        Check("getNewestRuneEffect: the one added last", "524", Lua("report(player:getNewestRuneEffect())"));
        Lua("player:removeOldestRune()");
        Check("removeOldestRune: Ignis gone", "1/524", Lua("report(player:getActiveRuneCount() .. '/' .. player:getHighestRuneEffect())"));
        Lua("player:addStatusEffect(xi.effect.IGNIS, { power = 1, duration = 60, origin = player }) player:removeNewestRune()");
        Check("removeNewestRune: the new Ignis gone", "1/524", Lua("report(player:getActiveRuneCount() .. '/' .. player:getHighestRuneEffect())"));
        Lua("player:removeAllRunes()");
        Check("removeAllRunes", "0", Lua("report(player:getActiveRuneCount())"));
        Lua("player:addStatusEffect(xi.effect.BUST, { power = 1, duration = 60, origin = player, subPower = 98 })");
        Check("hasBustEffect: the roll busted, not another", "true/false", Lua("report(tostring(player:hasBustEffect(98)) .. '/' .. tostring(player:hasBustEffect(99)))"));
        Lua("player:delStatusEffect(xi.effect.BUST)");
        Check("getWorldAngle(x, y, z): a point due +z is 192", "192", Lua($"report(player:getWorldAngle({c.X}, {c.Y}, {c.Z + 10}))"));
        Lua($"GetNPCByID({door.Id}):teleport({{ x = {c.X}, y = {c.Y}, z = {c.Z + 10} }}, 0)");
        Check("getWorldAngle(entity, 360): 270 degrees; the char to the door's right", "270/false/true",
            Lua($"local d = GetNPCByID({door.Id}) report(player:getWorldAngle(d, 360) .. '/' .. tostring(player:isToEntitysLeft(d)) .. '/' .. tostring(player:isToEntitysRight(d)))"));
        Check("getTrickAttackChar alone: nobody", "nil", Lua($"report(tostring(player:getTrickAttackChar(player)))"));
        Check("getPartyMember: itself at 0; nobody at 1", $"{CharId}/nil", Lua("report(player:getPartyMember(0, 0):getID() .. '/' .. tostring(player:getPartyMember(1, 0)))"));
        s.PacketList.Clear();
        Lua($"player:messageStandard(5) player:sendEmptyEntityUpdateToPlayer(GetNPCByID({door.Id}))");
        var standard = Of(0x009);
        var empty = Of(0x00E).FindAll(p => BitConverter.ToUInt32(p.Buffer, 4) == door.Id);
        Check("messageStandard; sendEmptyEntityUpdateToPlayer", "1/5/1/32/1",
            $"{standard.Count}/{(standard.Count > 0 ? BitConverter.ToUInt16(standard[0].Buffer, 10) : -1)}/{empty.Count}/{(empty.Count > 0 ? empty[0].Buffer[0x0A] : -1)}/{(empty.Count > 0 ? empty[0].Buffer[0x30] : -1)}");
        Check("hasListener", "false/true",
            Lua("report(tostring(player:hasListener('B7_EVENT'))) player:addListener('B7_EVENT', 'B7_KEY', function() end) report(tostring(player:hasListener('B7_EVENT')))").Replace(",", "/"));
        Lua("player:setLocalVar('b7_a', 1) player:setLocalVar('b7_b', 2) player:setLocalVar('other7', 3) player:clearLocalVarsWithPrefix('b7_')");
        Check("clearLocalVarsWithPrefix", "0/0/3", Lua("report(player:getLocalVar('b7_a') .. '/' .. player:getLocalVar('b7_b') .. '/' .. player:getLocalVar('other7'))"));

        Console.WriteLine("batch 8: traverser stones, guild points, slips, deeds, the mission log, the maze, the contest, chest restores");
        Lua("player:setClaimedTraverserStones(4) player:addClaimedTraverserStones(2)");
        Check("traverser stones: 4 claimed and 2; none available before the epoch", "6/0",
            Lua("report(player:getClaimedTraverserStones() .. '/' .. player:getAvailableTraverserStones())"));
        var gp = Lua("local item, left = player:getCurrentGPItem(0) report(item) report(left)").Split(',');
        string gpPattern = Scalar(db, "SELECT value FROM server_variables WHERE name = '[GUILD]pattern'");
        Check("getCurrentGPItem: today's fishing item and its whole cap still to earn", "True/True",
            $"{gp.Length == 2 && gp[0] != "0"}/{gp.Length == 2 && Scalar(db, "SELECT COUNT(*) FROM guild_item_points WHERE guildid = 0 AND pattern = ? AND itemid = ? AND max_points = ?", BoundValue.Of(int.Parse(gpPattern ?? "0")), BoundValue.Of(ushort.Parse(gp[0])), BoundValue.Of(ushort.Parse(gp[1]))) != "0"}");
        Lua("player:setCharVar('[GUILD]daily_points', 1)");
        Check("getCurrentGPItem: nothing left once the day's cap is marked", "0", Lua("local item, left = player:getCurrentGPItem(0) report(left)"));
        Lua("player:setCharVar('[GUILD]daily_points', 0)");
        const ushort StorageSlip01 = 29312;
        Lua($"player:addItem({StorageSlip01})");
        var slip = c.Items.Find(i => i.ItemId == StorageSlip01);
        Check("getRetrievableItemsForSlip: the slip's 24 bytes; nil without the slip", "24/0/nil",
            $"{Lua($"local t = player:getRetrievableItemsForSlip({StorageSlip01}) report(#t .. '/' .. t[1])")}/{Lua("report(tostring(player:getRetrievableItemsForSlip(29313)))")}");
        Check("storeWithPorterMoogle without a trade: 3", "3", Lua($"report(player:storeWithPorterMoogle({StorageSlip01}, {{ 1 }}, {{ {BronzeCap} }}))"));
        slip.Extra[0] = 0x03;
        Lua($"player:retrieveItemFromSlip({StorageSlip01}, {BronzeCap}, 0, 0xFE)");
        Check("retrieveItemFromSlip: the cap back, its bit off the slip and the row", "True/2/02",
            $"{c.Items.Exists(i => i.ItemId == BronzeCap && i.Location == Container.Inventory)}/{slip.Extra[0]}/" +
            Scalar(db, "SELECT HEX(SUBSTRING(extra, 1, 1)) FROM char_inventory WHERE charid = ? AND itemId = ?", BoundValue.Of(CharId), BoundValue.Of(StorageSlip01)));
        Lua("player:setClaimedDeed(33) player:toggleReceivedDeedRewards()");
        Check("setClaimedDeed(33), toggleReceivedDeedRewards", "1/2/0/0/0",
            Lua("local t = player:getClaimedDeedMask() report(t[1] .. '/' .. t[2] .. '/' .. t[3] .. '/' .. t[4] .. '/' .. t[5])"));
        Lua("player:setClaimedDeed(96) player:setClaimedDeed(100) player:resetClaimedDeeds()");
        Check("resetClaimedDeeds: the fourth word's first two bits kept, a reset counted; the row", $"1/1/{1 << 18}/0100000000000400",
            Lua("local t = player:getClaimedDeedMask() report(t[1] .. '/' .. t[4] .. '/' .. t[5])") + "/" +
            Scalar(db, "SELECT HEX(SUBSTRING(claimed_deeds, 13, 8)) FROM char_unlocks WHERE charid = ?", BoundValue.Of(CharId)));
        s.PacketList.Clear();
        Lua("player:sendPartialMissionLog(6, false) player:sendPartialMissionLog(14, false) player:sendPartialMissionLog(7, true)");
        Check("sendPartialMissionLog: CoP's MISSION, nothing for 14, the completed assaults", "2/1/1",
            $"{Of(0x056).Count}/{Port(0xFFFF)}/{Port(0xC0)}");
        Lua("player:learnMazeVoucher(3) player:learnMazeRune(500)");
        Check("the maze: voucher 3 and rune 500 learned, not their neighbours; the row", "true/false/true/false/04",
            Lua("report(tostring(player:hasMazeVoucher(3)) .. '/' .. tostring(player:hasMazeVoucher(4)) .. '/' .. tostring(player:hasMazeRune(500)) .. '/' .. tostring(player:hasMazeRune(501)))") + "/" +
            Scalar(db, "SELECT HEX(SUBSTRING(maze_vouchers, 1, 1)) FROM char_unlocks WHERE charid = ?", BoundValue.Of(CharId)));
        Check("the contest: no entry", "0/0/0/0",
            Lua("local r = player:getContestRewardStatus() report(player:getContestScore() .. '/' .. player:getContestRank() .. '/' .. r.rank .. '/' .. r.share)"));
        c.Hp = c.MaxHp - 30;
        s.PacketList.Clear();
        Lua($"player:restoreFromChest(GetNPCByID({door.Id}), 1)");
        Check("restoreFromChest: the chest's BATTLE2 to those near it", 1, Of(0x028).Count);
        c.Hp = c.MaxHp;
        Lua("player:timer(500, function(p) p:setLocalVar('b9Timer', 1) end)");
        Advance(100);
        c.Animation = Anim.Attack;
        c.UpdateMask = 0;
        Lua("player:resetAI()");
        bool hpUpdate = (c.UpdateMask & CharStats.UpdateHp) != 0;
        Advance(1000);
        Check("resetAI on a char: the weapon away, the HP update, the timer dropped", "0/True/0",
            $"{c.Animation}/{hpUpdate}/{Lua("report(player:getLocalVar('b9Timer'))")}");
        Lua("player:timer(500, function(p) p:setLocalVar('b9Timer', 2) end)");
        Advance(1000);
        Check("a timer after resetAI still fires", "2", Lua("report(player:getLocalVar('b9Timer'))"));

        Console.WriteLine("batch 10: linkpearls, confrontation, party effects, Wild Card, Random Deal, Healing Waltz, swipe damage, multipliers");
        Sql(db, "DELETE FROM linkshells WHERE name = 'VerifyBten'");
        Sql(db, "INSERT INTO linkshells(name, color) VALUES ('VerifyBten', 4660)");
        string lsId = Scalar(db, "SELECT linkshellid FROM linkshells WHERE name = 'VerifyBten'");
        Check("addLinkpearl of no such linkshell: false", "false", Lua("report(tostring(player:addLinkpearl('NoSuchShellX', false)))"));
        Check("addLinkpearl: true", "true", Lua("report(tostring(player:addLinkpearl('VerifyBten', false)))"));
        string PearlOf(Item i) => i == null ? "none" : $"{BitConverter.ToUInt32(i.Extra, 0)}/{BitConverter.ToUInt16(i.Extra, 6)}/{i.Extra[8]}/{i.Signature}/{i.Quantity}";
        var newPearl = c.Items.Find(i => i.ItemId == 515 && i.Location == Container.Inventory);
        Check("addLinkpearl: a linkpearl of that shell, its colour, the linkpearl type, the signature", $"{lsId}/4660/3/VerifyBten/1", PearlOf(newPearl));
        Check("addLinkpearl: the char_inventory row", "1",
            Scalar(db, "SELECT COUNT(*) FROM char_inventory WHERE charid = ? AND itemId = 515 AND signature = 'VerifyBten'", BoundValue.Of(CharId)));
        s.PacketList.Clear();
        Check("addLinkpearl equipped: true", "true", Lua("report(tostring(player:addLinkpearl('VerifyBten', true)))"));
        int ls2 = c.Equip.FindIndex(e => e.EquipSlot == 17);
        var worn = ls2 < 0 ? null : c.Items.Find(i => i.Location == c.Equip[ls2].Container && i.Slot == c.Equip[ls2].InvSlot);
        Check("addLinkpearl equipped: a second pearl worn as the second shell, GROUP_COMLINK sent, saved to char_equip",
            $"{lsId}/{newPearl?.Slot != worn?.Slot}/1/1",
            $"{(worn == null ? "none" : BitConverter.ToUInt32(worn.Extra, 0).ToString())}/{newPearl?.Slot != worn?.Slot}/{Of(0x0E0).Count}/" +
            Scalar(db, "SELECT COUNT(*) FROM char_equip WHERE charid = ? AND equipslotid = 17", BoundValue.Of(CharId)));

        Check("getConfrontationEffect without one: 0", "0", Lua("report(player:getConfrontationEffect())"));
        Check("copyConfrontationEffect onto an entity without one: 0", "0", Lua("report(player:copyConfrontationEffect(player:getTargID()))"));
        Lua("player:addStatusEffect(xi.effect.CONFRONTATION, { power = 7, duration = 60, origin = player, flag = xi.effectFlag.CONFRONTATION })");
        Check("getConfrontationEffect: its power", "7", Lua("report(player:getConfrontationEffect())"));
        Check("copyConfrontationEffect: the target's confrontation power", "7", Lua("report(player:copyConfrontationEffect(player:getTargID()))"));
        Check("copyConfrontationEffect of a bad index: 0", "0", Lua("report(player:copyConfrontationEffect(0))"));
        Lua("for _ = 1, 3 do player:delStatusEffectSilent(xi.effect.CONFRONTATION) end");
        Advance(100); // a deleted effect stays listed until the tick
        Check("the confrontation effects gone", "0", Lua("report(player:getConfrontationEffect())"));

        Lua("player:addPartyEffect(xi.effect.PROTECT, xi.effect.PROTECT, 10, 0, 60)");
        Check("addPartyEffect alone: on the char; hasPartyEffect", "10/60000/true",
            Lua("local e = player:getStatusEffect(xi.effect.PROTECT) report(e:getPower() .. '/' .. e:getDuration() .. '/' .. tostring(player:hasPartyEffect(xi.effect.PROTECT)))"));
        Check("hasPartyEffect alone, of an effect not had: true (no party to check)", "true", Lua("report(tostring(player:hasPartyEffect(xi.effect.SHELL)))"));
        Lua("player:removePartyEffect(xi.effect.PROTECT)");
        Check("removePartyEffect without a party: nothing", "true", Lua("report(tostring(player:hasStatusEffect(xi.effect.PROTECT)))"));
        Lua("player:delStatusEffectSilent(xi.effect.PROTECT)");
        c.ReloadParty = false;
        Lua("player:reloadParty()");
        Check("reloadParty: the reload flagged", true, c.ReloadParty);

        string RecastRow(int id) => Scalar(db, "SELECT COUNT(*) FROM char_recast WHERE charid = ? AND id = ?", BoundValue.Of(CharId), BoundValue.Of((ushort)id));
        string HasRecasts(params int[] ids) => Lua(string.Join(" .. '/' .. ", Array.ConvertAll(ids, id => $"tostring(player:hasRecast(xi.recast.ABILITY, {id}))")).Insert(0, "report(") + ")");
        Lua("player:addRecast(xi.recast.ABILITY, 10, 60) player:addRecast(xi.recast.ABILITY, 0, 3600) player:addRecast(xi.recast.ABILITY, 254, 3600)");
        c.Tp = 0;
        c.Mp = 0;
        s.PacketList.Clear();
        Lua("player:doWildCard(player, 4)");
        Check("doWildCard 4: the abilities reset, both two-hours kept, 3000 TP, the recasts sent", "false/true/true/3000/1",
            $"{HasRecasts(10, 0, 254)}/{c.Tp}/{Of(0x119).Count}");
        Lua("player:doWildCard(player, 5)");
        Check("doWildCard 5: a WHM's first two-hour reset, half its MP back", $"false/true/{c.MaxMp / 2}", $"{HasRecasts(0, 254)}/{c.Mp}");
        Lua("player:addRecast(xi.recast.ABILITY, 0, 3600)");
        Lua("player:doWildCard(player, 6)");
        Check("doWildCard 6: both two-hours reset, the MP full", $"false/false/{c.MaxMp}/0", $"{HasRecasts(0, 254)}/{c.Mp}/{RecastRow(254)}");

        Check("doRandomDeal with nothing running: false", "false", Lua("report(tostring(player:doRandomDeal(player)))"));
        // the reset picks among every listed ability recast but the excluded ones, running or not: only 10 left to pick
        c.Recasts.GetRecastList(RecastType.Ability).RemoveAll(r => r.Id != 10 && r.Id != 196);
        Lua("player:addRecast(xi.recast.ABILITY, 10, 60) player:addRecast(xi.recast.ABILITY, 196, 60)");
        Check("the recasts running, char_recast rows", "true/true/1", $"{HasRecasts(10, 196)}/{RecastRow(10)}");
        Check("doRandomDeal: the one running recast reset (its char_recast row gone), Random Deal's own kept", "true/false/true/0",
            Lua("report(tostring(player:doRandomDeal(player)))") + "/" + HasRecasts(10, 196) + "/" + RecastRow(10));
        Check("doRandomDeal on a non-entity: false, with the C++ warning", "false/1",
            Lua("report(tostring(player:doRandomDeal(nil)))") + "/" + log.RemoveAll(l => l == "Invalid entity type passed as target (Binder)."));
        Lua("player:resetRecast(xi.recast.ABILITY, 196)");

        Lua("player:addStatusEffect(xi.effect.POISON, { power = 1, duration = 60, tick = 3, origin = player })");
        Check("healingWaltz: the poison erased, its id", "3/false", Lua("report(player:healingWaltz() .. '/' .. tostring(player:hasStatusEffect(xi.effect.POISON)))"));
        Check("healingWaltz with nothing to erase: xi.effect.NONE", "255", Lua("report(player:healingWaltz())"));

        c.Hp = c.MaxHp;
        Check("takeSwipeLungeDamage: the damage taken and returned", $"20/{c.MaxHp - 20}",
            Lua("local d = player:takeSwipeLungeDamage(player, 20, xi.attackType.MAGICAL, xi.damageType.FIRE) report(d .. '/' .. player:getHP())"));
        c.Hp = c.MaxHp;
        // BindBreakCheck: 95% a hit, so ten tries all but surely break it
        bool bindKept = true;
        for (int i = 0; i < 10 && bindKept; i++)
        {
            Lua("player:addStatusEffect(xi.effect.BIND, { power = 1, duration = 60, origin = player })");
            Lua("player:takeSwipeLungeDamage(player, 1, xi.attackType.MAGICAL, xi.damageType.FIRE)");
            bindKept = Lua("report(tostring(player:hasStatusEffect(xi.effect.BIND)))") == "true";
        }
        Check("takeSwipeLungeDamage: the hit may break bind (BindBreakCheck)", false, bindKept);
        Lua("player:addStatusEffect(xi.effect.BIND, { power = 1, duration = 60, origin = player })");
        Lua("player:takeSwipeLungeDamage(player, 0, xi.attackType.MAGICAL, xi.damageType.FIRE)");
        Check("takeSwipeLungeDamage for 0: bind kept", "true", Lua("report(tostring(player:hasStatusEffect(xi.effect.BIND)))"));
        Lua("player:delStatusEffectSilent(xi.effect.BIND)");
        c.Hp = c.MaxHp;

        Console.WriteLine("hideName / setStatus / setAnimationSub on a char");
        s.PacketList.Clear();
        Lua("player:hideName(true)");
        Check("hideName: namevis's hide bit, UPDATE_HP pending; isNameHidden", $"8/{CharStats.UpdateHp}/true",
              $"{c.NameVis}/{c.UpdateMask & CharStats.UpdateHp}/" + Lua("report(tostring(player:isNameHidden()))"));
        Lua("player:hideName(false)");
        Check("hideName(false)", "0/false", $"{c.NameVis}/" + Lua("report(tostring(player:isNameHidden()))"));
        Lua("player:setStatus(xi.status.UPDATE)");
        Check("setStatus / getStatus on a char", "1/1", $"{c.Status}/" + Lua("report(player:getStatus())"));
        Lua("player:setStatus(xi.status.NORMAL)");
        c.UpdateMask = 0;
        s.PacketList.Clear();
        Lua("player:setAnimationSub(3)");
        int subSent = Of(0x037).Count;
        s.PacketList.Clear();
        Lua("player:setAnimationSub(3) player:setAnimationSub(0, false)");
        Check("setAnimationSub on a char: its 0x037, none for the same or with sendUpdate false; getAnimationSub", "1/0/0/0",
              $"{subSent}/{Of(0x037).Count}/{c.AnimationSub}/" + Lua("report(player:getAnimationSub())"));

        Console.WriteLine("CLuaItem exdata: getExData / setExData (Exdata::toTable / fromTable), the raw pair, the dirty flush");
        const ushort BonanzaPearl = 4089;
        Lua($"player:addItem({BonanzaPearl})");
        var pearl = c.Items.Find(i => i.ItemId == BonanzaPearl && i.Location == Container.Inventory);
        Check("getExData of a lottery ticket: its keys", "0/0",
              Lua($"local t = player:findItem({BonanzaPearl}):getExData() report(t.number .. '/' .. t.title)"));
        s.PacketList.Clear();
        Lua($"player:findItem({BonanzaPearl}):setExData({{ number = 123456, title = 7 }})");
        Check("setExData: the layout written, the item dirty, nothing sent yet", "40E20107/True/0",
              $"{Convert.ToHexString(pearl.Extra, 0, 4)}/{pearl.Dirty}/{Of(0x020).Count}");
        Check("getExData reads it back", "123456/7", Lua($"local t = player:findItem({BonanzaPearl}):getExData() report(t.number .. '/' .. t.title)"));
        Advance(100);
        string PearlExtra() => Scalar(db, "SELECT HEX(SUBSTRING(extra, 1, 4)) FROM char_inventory WHERE charid = ? AND location = 0 AND slot = ?",
                                      BoundValue.Of(CharId), BoundValue.Of(pearl.Slot));
        Check("the tick's flush: saved, ITEM_ATTR sent, clean", "40E20107/1/False", $"{PearlExtra()}/{Of(0x020).Count}/{pearl.Dirty}");
        Check("getExDataRaw: 0-indexed bytes", "64/7/24",
              Lua($"local t = player:findItem({BonanzaPearl}):getExDataRaw() local n = 0 for _ in pairs(t) do n = n + 1 end report(t[0] .. '/' .. t[3] .. '/' .. n)"));
        Lua($"player:findItem({BonanzaPearl}):setExDataRaw({{ [0] = 5, [30] = 1 }})");
        Check("setExDataRaw: the byte set, a key past the exdata refused with the warning", "5/1/True",
              $"{pearl.Extra[0]}/{log.RemoveAll(l => l == "setExDataRaw: key too large for exdata array: bonanza_pearl[30]")}/{pearl.Dirty}");
        Advance(100);
        Lua($"player:addItem({Potion})");
        var potionItem = c.Items.Find(i => i.ItemId == Potion && i.Location == Container.Inventory);
        Check("getExData of an untyped item: the raw bytes", "0/24",
              Lua($"local t = player:findItem({Potion}):getExData() local n = 0 for _ in pairs(t) do n = n + 1 end report(t[0] .. '/' .. n)"));
        Lua($"player:findItem({Potion}):setExData({{ [2] = 9 }})");
        Check("setExData of an untyped item: the table's keys as bytes", 9, potionItem.Extra[2]);
        Advance(100);
        potionItem.Extra[2] = 0;
        Sql(db, "UPDATE char_inventory SET extra = ? WHERE charid = ? AND location = 0 AND slot = ?",
            BoundValue.Of(new byte[24]), BoundValue.Of(CharId), BoundValue.Of(potionItem.Slot));
        Check("a lookup item: its type's keys over blank exdata", "0", Lua($"report(GetReadOnlyItem({BonanzaPearl}):getExData().number)"));
        Lua($"player:delItem({BonanzaPearl}, 1) player:delItem({Potion}, 1)");

        c.AddModifier(Mod.DA_DOUBLE_DMG_RATE, 100);
        Check("addDamageFromMultipliers without a weapon: the damage kept", "100",
            Lua("report(player:addDamageFromMultipliers(100, xi.physicalAttackType.DOUBLE, xi.slot.MAIN, false))"));
        Lua($"player:addItem({AshClub})");
        var club = c.Items.Find(i => i.ItemId == AshClub && i.Location == Container.Inventory);
        Send(0x050, 8, b => { b[4] = club.Slot; b[5] = 0; b[6] = 0; });
        Check("the club equipped", "true", Lua($"report(tostring(player:hasEquipped({AshClub})))"));
        Check("addDamageFromMultipliers: a double attack's DA_DOUBLE_DMG_RATE, a normal swing kept, a non-char kept", "200/100/100",
            Lua("report(player:addDamageFromMultipliers(100, xi.physicalAttackType.DOUBLE, xi.slot.MAIN, false) .. '/' .. " +
                "player:addDamageFromMultipliers(100, xi.physicalAttackType.NORMAL, xi.slot.MAIN, true))") +
            Lua($"report('/' .. GetNPCByID({door.Id}):addDamageFromMultipliers(100, 1, 0, false))"));
        c.AddModifier(Mod.DA_DOUBLE_DMG_RATE, -100);
        Lua("player:unequipItem(xi.slot.MAIN)");

        Console.WriteLine("batch 9: the objective packet, items, pet names, the party's jobs, the shield, blue magic, NPC looks, quests, history");
        uint U32(BasicPacket p, int off) => BitConverter.ToUInt32(p.Buffer, off);
        s.PacketList.Clear();
        Lua("player:countdown(30)");
        var cd = Of(0x075);
        Check("countdown(30): GP_SERV_COMMAND_BATTLEFIELD with the countdown flag, mode 1, 30 s, no warning", "172/1/1/30/0",
            cd.Count == 1 ? $"{cd[0].GetSize()}/{cd[0].Buffer[36]}/{U32(cd[0], 4)}/{U32(cd[0], 12)}/{U32(cd[0], 16)}" : "none");
        s.PacketList.Clear();
        Lua("player:countdown()");
        cd = Of(0x075);
        Check("countdown(): the cleared packet", "0/0/0", cd.Count == 1 ? $"{cd[0].Buffer[36]}/{U32(cd[0], 4)}/{U32(cd[0], 12)}" : "none");
        s.PacketList.Clear();
        Lua("player:objectiveUtility({ countdown = { duration = 60, warning = 10 }, bars = { { title = 'Kills', value = 3 } }, " +
            "fence = { pos = { x = 10, z = -20 }, radius = 25, blue = true }, help = { title = 5, description = 40 } })");
        cd = Of(0x075);
        Check("objectiveUtility: every flag, the bars' mode, the countdown, the first bar, the fence, the help text",
            "15/65535/60/10/3/Kills/0/10000/-20000/25000/25000/1/0/5/21",
            cd.Count == 1 ? $"{cd[0].Buffer[36]}/{U32(cd[0], 4)}/{U32(cd[0], 12)}/{U32(cd[0], 16)}/{U32(cd[0], 40)}/" +
                            $"{System.Text.Encoding.ASCII.GetString(cd[0].Buffer, 44, 16).TrimEnd('\0')}/{U32(cd[0], 60)}/" +
                            $"{BitConverter.ToInt32(cd[0].Buffer, 20)}/{BitConverter.ToInt32(cd[0].Buffer, 24)}/{U32(cd[0], 28)}/{U32(cd[0], 32)}/" +
                            $"{cd[0].Buffer[37]}/{cd[0].Buffer[38]}/{BitConverter.ToUInt16(cd[0].Buffer, 168)}/{BitConverter.ToUInt16(cd[0].Buffer, 170)}" : "none");
        s.PacketList.Clear();
        Lua("player:objectiveUtility({ scoreboard = { marchlandScore = 5, strongholdProgressMax = 9 } })");
        cd = Of(0x075);
        Check("objectiveUtility's scoreboard: the progress flag, mode 0x1000, the scores", "2/4096/5/9",
            cd.Count == 1 ? $"{cd[0].Buffer[36]}/{U32(cd[0], 4)}/{U32(cd[0], 40)}/{U32(cd[0], 60)}" : "none");

        int invCount = c.Items.FindAll(i => i.Location == Container.Inventory).Count, tempCount = c.Items.FindAll(i => i.Location == Container.TempItems).Count;
        Check("getItems: the inventory by default and out of range, the temporary items", $"{invCount}/{invCount}/{tempCount}",
            Lua("report(#player:getItems() .. '/' .. #player:getItems(200) .. '/' .. #player:getItems(3))"));
        Check("getItems in slot order", "True", Lua("local t = player:getItems() local ok = true for i = 2, #t do if t[i]:getSlotID() < t[i - 1]:getSlotID() then ok = false end end report(tostring(ok):gsub('^%l', string.upper))"));
        Lua("player:setPetName(1, 5) player:setPetName(6, 3, 2)");
        var petRow = Sql(db, "SELECT wyvernid, chocoboid FROM char_pet WHERE charid = ?", BoundValue.Of(CharId));
        Check("setPetName: the wyvern's name, the chocobo's two parts", "5/131075",
            petRow != null && petRow.Next() ? $"{petRow.Get<uint>("wyvernid")}/{petRow.Get<uint>("chocoboid")}" : "none");
        Check("hasPartyJob alone: false", "false", Lua("report(tostring(player:hasPartyJob(xi.job.WHM)))"));
        Check("getShieldDefense without a shield: 0", "0", Lua("report(player:getShieldDefense())"));
        c.SetBlueSpells[0] = 1;
        c.SetBlueSpells[3] = 0x10;
        Check("getSetBlueSpells: the set spells' ids", "513,528", Lua("for _, id in ipairs(player:getSetBlueSpells()) do report(id) end"));
        Array.Clear(c.SetBlueSpells);
        Lua($"GetNPCByID({door.Id}):setLook({{ face = 3, race = 2 }})");
        Check("setLook: an equipped model of that face and race", $"1/{0x0203}", $"{door.Look[0]}/{door.Look[1]}");
        Check("addWeaponSkillPoints without an unlockable weapon: false", "false", Lua("report(tostring(player:addWeaponSkillPoints(0, 5)))"));
        Lua("player:addQuest(0, 5)");
        s.PacketList.Clear();
        Lua("player:delCurrentQuest(0, 5)");
        Check("delCurrentQuest: no longer current, both quest logs sent, saved", "0/2/0",
            $"{Lua("report(player:getQuestStatus(0, 5))")}/{Of(0x056).Count}/" +
            Scalar(db, "SELECT ASCII(SUBSTRING(quests, 1, 1)) & 32 FROM chars WHERE charid = ?", BoundValue.Of(CharId)));
        s.PacketList.Clear();
        Lua("player:delCurrentQuest(0, 5)");
        Check("delCurrentQuest on a quest not current: nothing", 0, Of(0x056).Count);
        string knockedOut = Lua("report(player:getHistory(xi.history.TIMES_KNOCKED_OUT))");
        Check("getHistory out of range: 0", "0", Lua("report(player:getHistory(99))"));
        Lua("player:die()");
        Check("die: the char falls", 0, c.Hp);
        Advance(100); // the death runs on the next tick
        Check("getHistory: the knock-out counted", (int.Parse(knockedOut) + 1).ToString(), Lua("report(player:getHistory(xi.history.TIMES_KNOCKED_OUT))"));

        Console.WriteLine("batch 11: job points, the unity leader, the GM moves and their IPC");
        string JpRow(string col) => Scalar(db, $"SELECT {col} FROM char_job_points WHERE charid = ? AND jobid = 3", BoundValue.Of(CharId));
        Check("getJobPoints without a row: 0", "0", Lua("report(player:getJobPoints(xi.job.WHM))"));
        s.PacketList.Clear();
        Lua("player:addJobPoints(xi.job.WHM, 100)");
        Check("addJobPoints: the row, the loaded points, the job points packet", "100/100/100/1",
            $"{Lua("report(player:getJobPoints(xi.job.WHM))")}/{JpRow("job_points")}/{c.JobPoints[3].Current}/{Of(0x063).Count}");
        Lua("player:addJobPoints(0, 5)");
        Check("addJobPoints of no job: refused with the debug line", 1, log.RemoveAll(l => l == "Attempt to adjust job points for an invalid job for (Binder)."));
        Lua("player:delJobPoints(xi.job.WHM, 30)");
        Check("delJobPoints", "70/70", $"{JpRow("job_points")}/{c.JobPoints[3].Current}");
        Lua("player:delJobPoints(xi.job.WHM, 100)");
        Check("delJobPoints below 0: refused", "70/1", $"{JpRow("job_points")}/" + log.RemoveAll(l => l == "Attempt to reduce job points below 0 for (Binder)."));
        Lua("player:setJobPoints(900)");
        Check("setJobPoints: the main job's, capped at 500", "500/500", $"{JpRow("job_points")}/{c.JobPoints[3].Current}");
        Lua("player:setJobPoints(250) player:setCapacityPoints(1234)");
        Check("setCapacityPoints: the main job's", "1234/1234/250", $"{JpRow("capacity_points")}/{c.JobPoints[3].Capacity}/{c.JobPoints[3].Current}");
        Lua("player:masterJob()");
        Check("masterJob: every type of the main job at 20, the points kept, 10 x (1 + .. + 20) spent", "20/20/250/2100/2100/20",
            $"{c.JobPointLevels[3, 0]}/{c.JobPointLevels[3, 9]}/{c.JobPoints[3].Current}/{c.JobPoints[3].Spent}/{JpRow("job_points_spent")}/{JpRow("jptype5")}");

        Lua("player:setUnityLeader(3)");
        Check("setUnityLeader: the leader and its row", "3/3",
            $"{c.UnityLeader}/" + Scalar(db, "SELECT unity_leader FROM char_profile WHERE charid = ?", BoundValue.Of(CharId)));
        Lua("player:setUnityLeader(12)");
        Check("setUnityLeader out of range: kept", 3, c.UnityLeader);

        ipcSent.Clear();
        Check("gotoPlayer of no such char: false, nothing sent", "false/0", Lua("report(tostring(player:gotoPlayer('NoSuchCharX')))") + $"/{ipcSent.Count}");
        Check("gotoPlayer: true, the world asked where the char is", $"true/{CharId}/{CharId}/1/True",
            Lua("report(tostring(player:gotoPlayer('Binder')))") +
            (ipcSent.Count == 1 && ipcSent[0] is XI.Ipc.EntityInformationRequest rq ? $"/{rq.RequesterId}/{rq.TargetId}/{rq.EntityType}/{rq.Warp}" : "/none"));
        ipcSent.Clear();
        zone.OnIpc(new XI.Ipc.EntityInformationRequest { RequesterId = 1234, TargetId = CharId, EntityType = 1, Warp = true });
        Check("EntityInformationRequest for a char here: its zone and position, the warp", $"1234/{SouthernSandoria}/{c.X}/{c.Z}/True",
            ipcSent.Count == 1 && ipcSent[0] is XI.Ipc.EntityInformationResponse rs ? $"{rs.RequesterId}/{(ushort)rs.ZoneId}/{rs.X}/{rs.Z}/{rs.Warp}" : "none");
        ipcSent.Clear();
        Lua($"player:gotoEntity({door.Id})");
        var entityRq = ipcSent.Count == 1 ? ipcSent[0] as XI.Ipc.EntityInformationRequest : null;
        Check("gotoEntity: the world asked about the NPC or mob", $"{CharId}/{door.Id}/6", entityRq == null ? "none" : $"{entityRq.RequesterId}/{entityRq.TargetId}/{entityRq.EntityType}");
        ipcSent.Clear();
        zone.OnIpc(entityRq);
        Check("EntityInformationRequest for an NPC: its position", $"{door.X}/{door.Z}/{door.Status != 2}",
            ipcSent.Count == 1 && ipcSent[0] is XI.Ipc.EntityInformationResponse nrs ? $"{nrs.X}/{nrs.Z}/{nrs.Warp}" : "none");
        ipcSent.Clear();
        zone.OnIpc(new XI.Ipc.EntityInformationRequest { RequesterId = CharId, TargetId = 0x1FFFFFF, EntityType = 6, Warp = true });
        Check("EntityInformationRequest for no such entity: no reply, the warning", "0/1", $"{ipcSent.Count}/" + log.RemoveAll(l => l == $"EntityInformationRequest for entity {0x1FFFFFF} failed"));
        Check("bringPlayer: the char sent to the caller's position", $"true/{CharId}/{SouthernSandoria}/{c.X}/{c.Z}",
            Lua("report(tostring(player:bringPlayer('Binder')))") +
            (ipcSent.Count == 1 && ipcSent[0] is XI.Ipc.SendPlayerToLocation sp ? $"/{sp.TargetId}/{(ushort)sp.ZoneId}/{sp.X}/{sp.Z}" : "/none"));
        Lua("player:resetPlayer('NoSuchCharX')");
        Check("resetPlayer of no such char: the debug line", 1, log.RemoveAll(l => l == "Could not get the character NoSuchCharX from database"));
        Lua("player:resetPlayer('Binder')");
        Check("resetPlayer: the session gone, the char in Lower Jeuno", "0/245/245/86",
            Scalar(db, "SELECT COUNT(*) FROM accounts_sessions WHERE charid = ?", BoundValue.Of(CharId)) + "/" +
            Scalar(db, "SELECT CONCAT(pos_zone, '/', pos_prevzone, '/', pos_rot) FROM chars WHERE charid = ?", BoundValue.Of(CharId)));
        c.Boundary = 2;
        zone.OnIpc(new XI.Ipc.EntityInformationResponse { RequesterId = CharId, TargetId = 1, Warp = true, ZoneId = (XI.Ipc.ZoneId)231, X = 7.5f, Y = 1, Z = -3.5f, Rot = 9 });
        Check("EntityInformationResponse with a warp: the char at the position, the boundary and packets dropped", "7.5/1/-3.5/9/0/0",
            $"{c.X}/{c.Y}/{c.Z}/{c.Rotation}/{c.Boundary}/{s.PacketList.Count}");
        zone.OnIpc(new XI.Ipc.SendPlayerToLocation { TargetId = CharId, ZoneId = (XI.Ipc.ZoneId)231, X = 2.5f, Y = 0, Z = 4.5f, Rot = 3 });
        Check("SendPlayerToLocation: the char at the position", "2.5/4.5/3", $"{c.X}/{c.Z}/{c.Rotation}");

        // last: the zone change it queues refuses the char's position reports until it goes
        c.Boundary = 3;
        float rezoneX = c.X;
        Lua("player:forceRezone()");
        Check("forceRezone: the boundary and the packets dropped", "0/0", $"{c.Boundary}/{s.PacketList.Count}");
        Send(0x015, 32, b => BitConverter.GetBytes(rezoneX + 5).CopyTo(b, C2sPos.OffX));
        Check("forceRezone: a zone change pending, the position report refused", rezoneX, c.X);

        Check("no script errors", "", string.Join(" | ", log.FindAll(l => (l.StartsWith("lua:") && !l.Contains("global scripts")) || (l.Contains("Invalid entity") && !l.Contains("function call on invalid entity")))));
    }

    // ---- Inspect 2: item signatures (charutils::LoadInventory's setSignature for a linkshell or an inscribable item,
    // getSignature from the exdata for anything else, addItem's signature key, the ITEM_ATTR that carries it) ----
    private const uint SignerId = 995811;
    private const ushort SimpleBed = 2;

    private static void CleanupInspect2(IDatabase db)
    {
        foreach (var t in CharidTables(db))
            Sql(db, $"DELETE FROM `{t}` WHERE charid = ?", BoundValue.Of(SignerId));
        Sql(db, "DELETE FROM chars WHERE charid = ?", BoundValue.Of(SignerId));
        Sql(db, "DELETE FROM accounts WHERE id = ?", BoundValue.Of(SignerId));
    }

    private static void RunInspect2(IDatabase db)
    {
        Console.WriteLine("inspect 2: item signatures on load, addItem's signature, ITEM_ATTR");
        var dataDir = FindDataDir();
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
        var loader = new CharLoader(db, Grades.Load(dataDir), AbilityTable.Load(db, _ => true), stats);
        var log = new List<string>();
        var zone = new ZoneHost(db, loader, dataDir, true, log.Add);
        zone.SendIpc = _ => { };
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);
        string Hex(byte[] b, int at, int n) => Convert.ToHexString(b, at, n);
        string Zeros(int bytes) => new string('0', bytes * 2);

        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(SignerId), BoundValue.Of("pb" + SignerId), BoundValue.Of("x"), BoundValue.Of((byte)1));
        InsertChar(db, SignerId, "Signer");
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(SignerId), BoundValue.Of(SignerId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl, sjob, slvl) VALUES (?, 300, 50, 3, 30, 0, 0)", BoundValue.Of(SignerId));
        Sql(db, "REPLACE INTO char_jobs SET charid = ?, unlocked = 8, whm = 30", BoundValue.Of(SignerId));
        Sql(db, "INSERT INTO char_storage(charid, inventory) VALUES (?, 30) ON DUPLICATE KEY UPDATE inventory = 30", BoundValue.Of(SignerId));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 2)", BoundValue.Of(SignerId));
        void Give(byte slot, ushort itemId, string signature, byte[] extra) =>
            Sql(db, "INSERT INTO char_inventory(charid,location,slot,itemId,quantity,signature,extra) VALUES (?,0,?,?,1,?,?)",
                BoundValue.Of(SignerId), BoundValue.Of(slot), BoundValue.Of(itemId), BoundValue.Of(signature), BoundValue.Of(extra));
        var signedExtra = new byte[24];
        signedExtra[12] = 0x57; signedExtra[13] = 0x39; signedExtra[14] = 0xB3; // "Kobo" packed
        Give(1, BronzeCap, "Kobo", new byte[24]);       // inscribable: the column is packed
        Give(2, Potion, "Ghost", new byte[24]);         // not inscribable: the column is ignored
        Give(3, AnniversaryRing, "", signedExtra);      // not inscribable: getSignature reads the exdata
        Give(4, SimpleBed, "Kobo", new byte[24]);       // an inscribable furnishing: at Furniture.Signature
        Give(5, AshClub, "AF0x", new byte[24]);         // strncpy stops at the zero byte "AF0" packs to

        var s = new MapSession { CharId = SignerId, AccountId = SignerId };
        zone.OnLogin(s);
        var c = (CharData)s.Char;
        Check("the signer logs in", true, c != null);
        if (c == null) return;
        Item At(byte slot) => c.Items.Find(i => i.Location == Container.Inventory && i.Slot == slot);
        string Of(Item i) => i == null ? "none" : $"{Hex(i.Extra, 0, 24)}/{i.Signature}";
        Check("an inscribable item: the column packed at 0x0C", Zeros(12) + "5739B3" + Zeros(9) + "/Kobo", Of(At(1)));
        Check("not inscribable: the column ignored, the exdata untouched", Zeros(24) + "/", Of(At(2)));
        Check("not inscribable: getSignature reads the exdata", Hex(signedExtra, 0, 24) + "/Kobo", Of(At(3)));
        Check("an inscribable furnishing: at 0x0A", Zeros(10) + "5739B3" + Zeros(11) + "/Kobo", Of(At(4)));
        Check("the packing cut at its zero byte", Zeros(12) + "2D" + Zeros(11) + "/AF", Of(At(5)));

        void Send(ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(s, opcode, new BasicPacket(buf));
            zone.AfterParse(s);
        }
        Send(0x00A, 92, buf => BitConverter.GetBytes(SignerId).CopyTo(buf, 12));
        Send(0x00C, 12);
        var attr = s.PacketList.FirstOrDefault(p => Opcode(p) == 0x020 && p.Buffer[14] == 0 && p.Buffer[15] == 1);
        Check("ITEM_ATTR carries the packed signature", "5739B3" + Zeros(9), attr == null ? "none" : Hex(attr.Buffer, 17 + 12, 12));

        // addItem({id, signature}): setSignature on the new item, which its char_inventory row and ITEM_ATTR carry
        s.PacketList.Clear();
        scripts.Lua.DoString($"GetPlayerByID({SignerId}):addItem({{ id = {AshClub}, signature = 'Lua' }})", "=verify_inspect2");
        var club = c.Items.Find(i => i.ItemId == AshClub && i.Slot != 5);
        Check("addItem's signature: packed at 0x0C", Zeros(12) + "5B9940" + Zeros(9) + "/Lua", Of(club));
        if (club != null)
        {
            var r = db.Execute("SELECT signature, extra FROM char_inventory WHERE charid = ? AND location = 0 AND slot = ?",
                               new[] { BoundValue.Of(SignerId), BoundValue.Of(club.Slot) });
            Check("addItem's signature: the row's signature and exdata", "Lua/5B9940",
                  r != null && r.Next() ? $"{r.Get<string>("signature")}/{Hex(r.GetBlobBytes("extra"), 12, 3)}" : "none");
            var clubAttr = s.PacketList.FirstOrDefault(p => Opcode(p) == 0x020 && p.Buffer[15] == club.Slot);
            Check("addItem's signature: in its ITEM_ATTR", "5B9940", clubAttr == null ? "none" : Hex(clubAttr.Buffer, 17 + 12, 3));
        }
        scripts.Lua.DoString($"GetPlayerByID({SignerId}):addItem({{ id = {AshClub} }})", "=verify_inspect2");
        Check("addItem without one: unsigned", Zeros(24) + "/", Of(c.Items.FindLast(i => i.ItemId == AshClub)));
        Check("no script errors", "", string.Join(" | ", log.FindAll(l => l.StartsWith("lua:") && !l.Contains("global scripts"))));
    }

    // ---- Items 2: augment mods per item on load and on the wearer, addItem's exdata / appraisal keys ----
    private const uint AugmenterId = 995861;
    private const ushort WindurstianRing = 13496, AsceticsRing = 13440, BonanzaPearl = 4089; // the Windurstian ring is rare

    private static void CleanupItems2(IDatabase db)
    {
        foreach (var t in CharidTables(db))
            Sql(db, $"DELETE FROM `{t}` WHERE charid = ?", BoundValue.Of(AugmenterId));
        Sql(db, "DELETE FROM chars WHERE charid = ?", BoundValue.Of(AugmenterId));
        Sql(db, "DELETE FROM accounts WHERE id = ?", BoundValue.Of(AugmenterId));
    }

    private static void RunItems2(IDatabase db)
    {
        Console.WriteLine("items 2: augment mods on load and on the wearer, addItem's exdata and appraisal");
        var dataDir = FindDataDir();
        var items = new ItemData(db);
        var stats = new CharStats(GrowthTables.Load(db), items, new StatSettings());
        var loader = new CharLoader(db, Grades.Load(dataDir), AbilityTable.Load(db, _ => true), stats);
        var log = new List<string>();
        var zone = new ZoneHost(db, loader, dataDir, true, log.Add);
        zone.SendIpc = _ => { };
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);
        string Hex(byte[] b, int at, int n) => Convert.ToHexString(b, at, n);
        var template = items.Equipment(WindurstianRing);
        var plain = items.Equipment(AsceticsRing);

        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(AugmenterId), BoundValue.Of("pb" + AugmenterId), BoundValue.Of("x"), BoundValue.Of((byte)1));
        InsertChar(db, AugmenterId, "Augmenter");
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(AugmenterId), BoundValue.Of(AugmenterId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl, sjob, slvl) VALUES (?, 300, 50, 3, 30, 0, 0)", BoundValue.Of(AugmenterId));
        Sql(db, "REPLACE INTO char_jobs SET charid = ?, unlocked = 8, whm = 30", BoundValue.Of(AugmenterId));
        Sql(db, "INSERT INTO char_storage(charid, inventory) VALUES (?, 30) ON DUPLICATE KEY UPDATE inventory = 30", BoundValue.Of(AugmenterId));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 2)", BoundValue.Of(AugmenterId));
        // a ring on the right ring slot with Accuracy+1 at 4 and a pet Accuracy / Rng.Acc. augment, kind bytes not yet set
        var extra = new byte[24];
        BitConverter.TryWriteBytes(extra.AsSpan(2, 2), (ushort)(23 | 4 << 11));
        BitConverter.TryWriteBytes(extra.AsSpan(4, 2), (ushort)96);
        Sql(db, "INSERT INTO char_inventory(charid,location,slot,itemId,quantity,signature,extra) VALUES (?,0,1,?,1,'',?)",
            BoundValue.Of(AugmenterId), BoundValue.Of(WindurstianRing), BoundValue.Of(extra));
        Sql(db, "INSERT INTO char_equip(charid,slotid,equipslotid,containerid) VALUES (?,1,13,0)", BoundValue.Of(AugmenterId));

        var s = new MapSession { CharId = AugmenterId, AccountId = AugmenterId };
        zone.OnLogin(s);
        var c = (CharData)s.Char;
        Check("the augmenter logs in", true, c != null);
        if (c == null) return;
        Item At(byte slot) => c.Items.Find(i => i.Location == Container.Inventory && i.Slot == slot);
        var ring = At(1);
        Check("LoadInventory's augment pass: ITEM_AUGMENTED, HasAugments / Standard", "True/0203",
              ring == null ? "none" : $"{ring.Augmented}/{Hex(ring.Extra, 0, 2)}");
        Check("the worn ring is the item's own CItemEquipment", true, ring != null && c.Equipped[13] == ring.Equip);
        Check("the wearer's ACC: the ring's own mods plus Accuracy+1 at 4", template.GetModifier(Mod.ACC) + 5, c.GetMod(Mod.ACC));
        c.PetMods.TryGetValue((0, Mod.ACC), out short petAcc);
        c.PetMods.TryGetValue((0, Mod.RACC), out short petRacc);
        Check("the wearer's pet mods: the pet augment", "1/1", $"{petAcc}/{petRacc}");

        void Send(ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(s, opcode, new BasicPacket(buf));
            zone.AfterParse(s);
        }
        Send(0x00A, 92, buf => BitConverter.GetBytes(AugmenterId).CopyTo(buf, 12));
        Send(0x00C, 12);

        string Row(Item item)
        {
            if (item == null) return "none";
            var r = db.Execute("SELECT extra FROM char_inventory WHERE charid = ? AND location = 0 AND slot = ?",
                               new[] { BoundValue.Of(AugmenterId), BoundValue.Of(item.Slot) });
            return r != null && r.Next() ? Hex(r.GetBlobBytes("extra"), 0, 24) : "no row";
        }
        void Lua(string call) => scripts.Lua.DoString($"GetPlayerByID({AugmenterId}):{call}", "=verify_items2");

        // addItem's exdata: Exdata::fromTable writes the augments and applies them
        Lua($"addItem({{ id = {AsceticsRing}, exdata = {{ augmentKind = 2, augmentSubKind = 3, augments = {{ {{ id = 23, value = 1 }} }} }} }})");
        var added = c.Items.Find(i => i.ItemId == AsceticsRing);
        Check("addItem's exdata: the row's augment exdata", "020317080000" + new string('0', 36), Row(added));
        Check("addItem's exdata: the augment applied to the new item", "True/2",
              added == null ? "none" : $"{added.Augmented}/{added.Equip?.GetModifier(Mod.ACC) - plain.GetModifier(Mod.ACC)}");
        if (added != null)
        {
            // worn on the left ring slot through 0x050: its Accuracy+1 at 1 joins the other's
            int before = c.GetMod(Mod.ACC);
            Send(0x050, 8, buf => { buf[4] = added.Slot; buf[5] = 14; });
            Check("equipping the added ring adds its augment", plain.GetModifier(Mod.ACC) + 2, c.GetMod(Mod.ACC) - before);
        }

        // appraisal: Exdata::Appraisable's AppraisalId
        Lua($"addItem({{ id = {AsceticsRing}, appraisal = 5 }})");
        var appraised = c.Items.FindLast(i => i.ItemId == AsceticsRing);
        Check("addItem's appraisal: byte 22", "05", appraised == null ? "none" : Row(appraised).Substring(44, 2));

        // a typed non-equipment exdata, and the raw fallback for an item with none (an index past 24 warned)
        Lua($"addItem({{ id = {BonanzaPearl}, exdata = {{ number = 123, title = 7 }} }})");
        Check("addItem's exdata: a LotteryTicket", "7B000007", Row(c.Items.FindLast(i => i.ItemId == BonanzaPearl)).Substring(0, 8));
        Lua($"addItem({{ id = {Potion}, exdata = {{ [0] = 0x42, [5] = 0xCD, [30] = 1 }} }})");
        Check("addItem's exdata: raw byte indices for an untyped item", "4200000000CD", Row(c.Items.FindLast(i => i.ItemId == Potion)).Substring(0, 12));
        Check("an index past the exdata is warned", true, log.Exists(l => l.Contains("AddItem: Trying to write to invalid exdata index: <30>")));
        Check("no script errors", "", string.Join(" | ", log.FindAll(l => l.StartsWith("lua:") && !l.Contains("global scripts"))));
    }
}
