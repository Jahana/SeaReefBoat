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

// XI.Verify.WeaponSkill.Live -- live gate for the weapon skill lists (charutils::BuildingCharWeaponSkills,
// CheckWeaponSkill, CheckValidEquipment, battleutils::CanUseWeaponskill, the capSkill / capAllSkills / learned weapon
// skill / recalculate bindings) in West Ronfaure against the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 990953;
    private const ushort WestRonfaure = 100;
    private const ushort BronzeSword = 16535, BraveBlade = 17744;
    private const int FastBlade = 32, BurningBlade = 33, Atonement = 45;

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

    private static string Ws(CharData c)
    {
        var ids = new List<int>();
        for (int i = 0; i < c.WeaponSkills.Length * 8; i++)
            if (CharStats.HasWeaponSkill(c, i)) ids.Add(i);
        return string.Join(" ", ids);
    }

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        var table = stats.WeaponSkills;
        var fast = table.Get(FastBlade);
        Check("the table: fast blade's row", "fast_blade/3/5/0/False/2/0", $"{fast?.Name}/{fast?.Type}/{fast?.SkillLevel}/{fast?.UnlockId}/{fast?.MainOnly}/{fast?.GetJob(1)}/{fast?.GetJob(2)}");
        var swords = table.ForSkill(Skill.Sword);
        bool ordered = true;
        for (int i = 1; i < swords.Count; i++) ordered &= swords[i - 1].SkillLevel <= swords[i].SkillLevel;
        Check("the sword list in skill level order", "True/True", $"{swords.Count > 10}/{ordered}");
        Check("past MAX_WEAPONSKILL_ID: none", "True/True", $"{table.Get(256) == null}/{table.Get(-1) == null}");

        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("wsverify"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        // a Hume with the NEW_ADVENTURER title (not a first login: no charCreate), standing at the zone's entrance (a
        // char at 0,0,0 is moved there by the zone's onZoneIn)
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,titles) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Swordy"), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(-126.0f), BoundValue.Of(-62.0f), BoundValue.Of(273.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0), BoundValue.Of(titles));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(CharId));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 300, 50, 1, 30)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war, whm, pld) VALUES (?, 30, 30, 75)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));
        void Item(byte slot, ushort id, byte[] extra) =>
            Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?, 0, ?, ?, 1, ?)",
                BoundValue.Of(CharId), BoundValue.Of(slot), BoundValue.Of(id), BoundValue.Of(extra ?? new byte[24]));
        Item(1, BronzeSword, null);
        Item(2, BraveBlade, null);
        var broken = new byte[24];
        broken[0] = 250; // the unlock points, all 250
        Item(3, BraveBlade, broken);

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        zone.VanaTicksNow = () => 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        zone.SendIpc = _ => { };
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);

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
        void Equip(byte invSlot, byte equipSlot)
        {
            s.PacketList.Clear();
            Send(0x050, 8, b => { b[4] = invSlot; b[5] = equipSlot; b[6] = 0; });
        }
        List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);
        string Learned()
        {
            var l = Of(0x029).FindAll(p => BitConverter.ToUInt16(p.Buffer, 24) == 45);
            return string.Join(" ", l.ConvertAll(p => $"{BitConverter.ToUInt32(p.Buffer, 12)}:{BitConverter.ToUInt32(p.Buffer, 16)}"));
        }

        var lua = scripts.Lua;
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            s.PacketList.Clear();
            lua.DoString($"local player = GetPlayerByID({CharId})\n" + code, "=verify_ws");
            return string.Join(",", got);
        }

        Console.WriteLine("WAR30 bare handed, no hand-to-hand skill");
        Check("logged in WAR30", "1/30", $"{c.MJob}/{c.MLvl}");
        Check("no weapon skills: no hand-to-hand skill, the 0-level ones on no job", "", Ws(c));

        Console.WriteLine("sword skill 4.9, then the bronze sword");
        Lua("player:setSkillLevel(xi.skill.SWORD, 49)");
        Check("setSkillLevel bare handed: nothing learned", "", Learned());
        Equip(1, CharStats.SlotMain);
        Check("the sword on", BronzeSword, c.Equipped[CharStats.SlotMain]?.ItemId ?? 0);
        Check("skill 4: no fast blade yet", "", Ws(c));
        Check("COMMAND_DATA sent with the equip", 1, Of(0x0AC).Count);

        Console.WriteLine("a skill up to 5 learns fast blade");
        Lua("player:trySkillUp(xi.skill.SWORD, 30, true)");
        Check("skill 5 (the forced skill up adds at least one)", true, c.RealSkills[Skill.Sword] >= 50 && c.RealSkills[Skill.Sword] < 60);
        Check("learns fast blade: the message, COMMAND_DATA", $"{FastBlade}:{FastBlade}/1", $"{Learned()}/{Of(0x0AC).Count}");
        Check("the list: fast blade", $"{FastBlade}", Ws(c));

        Console.WriteLine("setSkillLevel to 30 learns burning blade");
        Lua("player:setSkillLevel(xi.skill.SWORD, 300)");
        Check("learns burning blade", $"{BurningBlade}:{BurningBlade}", Learned());
        Check("the list: fast and burning blade", $"{FastBlade} {BurningBlade}", Ws(c));
        Check("the row saved", "300", DbRow(db, "value", $"FROM char_skills WHERE charid = ? AND skillid = {Skill.Sword}"));
        Lua("player:setSkillLevel(xi.skill.SWORD, 310)");
        Check("31: nothing at exactly that level", "", Learned());

        Console.WriteLine("capSkill");
        int cap = int.Parse(Lua("report(player:getMaxSkillLevel(30, xi.job.WAR, xi.skill.SWORD))"));
        Lua("player:capSkill(xi.skill.SWORD)");
        Check("capped: real at the cap, working blue", $"{cap * 10}/{cap | 0x8000}", $"{c.RealSkills[Skill.Sword]}/{c.WorkingSkills[Skill.Sword]}");
        Check("capped: CLISTATUS2, the row", $"1/{cap * 10}", $"{Of(0x062).Count}/{DbRow(db, "value", $"FROM char_skills WHERE charid = ? AND skillid = {Skill.Sword}")}");
        var reachable = new List<int>();
        var atCap = new List<int> { FastBlade, BurningBlade };
        foreach (var ws in swords)
        {
            if (ws.SkillLevel == 0 || ws.SkillLevel > cap || ws.GetJob(1) == 0) continue;
            reachable.Add(ws.Id);
            if (ws.SkillLevel == cap) atCap.Add(ws.Id);
        }
        reachable.Sort();
        // CheckWeaponSkill only teaches the ones at exactly the new level; the rest wait for a rebuild
        Check("the list: what it had, plus the ones at exactly the cap", string.Join(" ", atCap), Ws(c));

        Console.WriteLine("the learned weapon skills");
        Check("hasLearnedWeaponskill(3): no", "false", Lua("report(tostring(player:hasLearnedWeaponskill(3)))"));
        Lua("player:addLearnedWeaponskill(3)");
        Check("added: yes, saved, COMMAND_DATA", "true/1", $"{Lua("report(tostring(player:hasLearnedWeaponskill(3)))")}/{(c.LearnedWeaponskills[0] >> 3) & 1}");
        Check("the weaponskills column", "08", DbRow(db, "LEFT(HEX(weaponskills), 2)", "FROM chars WHERE charid = ?"));
        Lua("player:delLearnedWeaponskill(3)");
        int commandData = Of(0x0AC).Count;
        Check("removed", "false/1/00", $"{Lua("report(tostring(player:hasLearnedWeaponskill(3)))")}/{commandData}/{DbRow(db, "LEFT(HEX(weaponskills), 2)", "FROM chars WHERE charid = ?")}");

        Console.WriteLine("recalculateSkillsTable / recalculateAbilitiesTable");
        Array.Clear(c.WeaponSkills);
        Lua("player:recalculateSkillsTable()");
        Check("rebuilt, CLISTATUS2, ABIL_RECAST, COMMAND_DATA", $"{string.Join(" ", reachable)}/1/1/1",
              $"{Ws(c)}/{Of(0x062).Count}/{Of(0x119).Count}/{Of(0x0AC).Count}");
        Array.Clear(c.WeaponSkills);
        Lua("player:recalculateAbilitiesTable()");
        Check("rebuilt, COMMAND_DATA", $"{string.Join(" ", reachable)}/1", $"{Ws(c)}/{Of(0x0AC).Count}");

        Console.WriteLine("unequipped: back to hand-to-hand");
        Equip(0, CharStats.SlotMain);
        Check("the sword off, no weapon skills", "0/", $"{c.Equipped[CharStats.SlotMain]?.ItemId ?? 0}/{Ws(c)}");

        Console.WriteLine("CheckValidEquipment: WHM can't hold the sword");
        Equip(1, CharStats.SlotMain);
        Check("on again", BronzeSword, c.Equipped[CharStats.SlotMain]?.ItemId ?? 0);
        Lua("player:changeJob(xi.job.WHM)");
        Check("WHM30: the sword comes off, the list empties", "3/30/0/", $"{c.MJob}/{c.MLvl}/{c.Equipped[CharStats.SlotMain]?.ItemId ?? 0}/{Ws(c)}");
        Check("the equipment flagged for the save at the end of the datagram", true, c.EquipDirty);

        Console.WriteLine("PLD75 and the brave blade: atonement only once broken");
        Lua("player:changeJob(xi.job.PLD)");
        Check("changeJob keeps the level", "7/30", $"{c.MJob}/{c.MLvl}");
        Lua("player:setLevel(75)");
        Check("PLD75", "7/75", $"{c.MJob}/{c.MLvl}");
        Lua("player:capSkill(xi.skill.SWORD)");
        Equip(2, CharStats.SlotMain);
        Check("the brave blade on, unbroken", $"{BraveBlade}/False/0", $"{c.Equipped[CharStats.SlotMain]?.ItemId ?? 0}/{c.MainWeapon.IsUnlocked(c.FindItem(Container.Inventory, 2))}/{WeaponInfo.CurrentUnlockPoints(c.FindItem(Container.Inventory, 2))}");
        Check("no atonement", false, CharStats.HasWeaponSkill(c, Atonement));
        Check("the PLD list has savage blade? (unlock 3 not learned)", false, CharStats.HasWeaponSkill(c, 42));
        Equip(3, CharStats.SlotMain);
        Check("the broken one: atonement from ADDS_WEAPONSKILL", "True/True", $"{c.MainWeapon.IsUnlocked(c.FindItem(Container.Inventory, 3))}/{CharStats.HasWeaponSkill(c, Atonement)}");
        Lua("player:addLearnedWeaponskill(3)");
        Check("savage blade with its unlock learned", true, CharStats.HasWeaponSkill(c, 42));
        Lua("player:delLearnedWeaponskill(3)");

        // CLuaItem::setWeaponskillPointsNeeded (unlocking_a_myth's era override of the Vigil weapons' onItemEquip):
        // the item's own total, read by getWeaponskillPointsNeeded and the weapon skill unlock path; the template and a
        // lookup item (GetItemByID: no m_writeItem) keep the item_weapon row's 250
        Console.WriteLine("setWeaponskillPointsNeeded: the unbroken brave blade's own total");
        Equip(2, CharStats.SlotMain);
        const string Main = "local i = player:getEquippedItem(xi.slot.MAIN) ";
        Check("set to 4000: the item reads it; the template and a lookup item 250 (the lookup not written)",
              "4000/250/250", Lua(Main + "i:setWeaponskillPointsNeeded(4000) report(i:getWeaponskillPointsNeeded()) " +
                                  $"local t = GetItemByID({BraveBlade}) t:setWeaponskillPointsNeeded(1) " +
                                  $"report(t:getWeaponskillPointsNeeded()) report(GetItemByID({BraveBlade}):" +
                                  "getWeaponskillPointsNeeded())").Replace(",", "/"));
        Lua(Main + "i:setWeaponskillPointsNeeded(0)");
        Lua("player:recalculateSkillsTable()");
        Check("set to 0: no longer unlockable, so atonement from ADDS_WEAPONSKILL (BuildingCharWeaponSkills)",
              true, CharStats.HasWeaponSkill(c, Atonement));
        Lua(Main + "i:setWeaponskillPointsNeeded(250)");
        Lua("player:recalculateSkillsTable()");
        Check("back to 250: unbroken, no atonement", false, CharStats.HasWeaponSkill(c, Atonement));
        // charutils::AddWeaponSkillPoints (player:addWeaponSkillPoints) up to the item's own total: one lowered below
        // the points so far leaves the weapon unbroken (isUnlocked is points == total) until the next points, which
        // break it at that total (CItemWeapon::addWsPoints)
        string Points(int n) => Lua($"report(tostring(player:addWeaponSkillPoints(xi.slot.MAIN, {n}))) " + Main +
                                    "report(i:getWeaponskillPoints())").Replace(",", "/") +
                                $"/{CharStats.HasWeaponSkill(c, Atonement)}";
        Check("100 points at the total 250: added, unbroken", "true/100/False", Points(100));
        Lua(Main + "i:setWeaponskillPointsNeeded(50)");
        Lua("player:recalculateSkillsTable()");
        Check("  the total lowered to 50: 100 is not 50, still unbroken", false,
              CharStats.HasWeaponSkill(c, Atonement));
        Check("  1 point more: broken at the item's 50 (the points set to it), atonement", "true/50/True", Points(1));
        Check("  broken: no more points (false)", "false/50/True", Points(1));
        // xi::items::clone (a transaction's undo, a trade, the bazaar) keeps the instance's total, as the C++ copy does
        Check("CloneItem keeps the item's own total", "50",
              ZoneHost.CloneItem(CharStats.EquippedItem(c, CharStats.SlotMain)).UnlockPointsNeeded?.ToString() ??
              "null");

        Console.WriteLine("capAllSkills");
        Lua("player:capAllSkills()");
        int cliStatus2 = Of(0x062).Count;
        Check("every combat and magic skill at 5000 in the rows", "45 5000 5000",
              DbRow(db, "COUNT(*), MIN(value), MAX(value)", $"FROM char_skills WHERE charid = ? AND skillid BETWEEN 1 AND 45"));
        int pldClub = int.Parse(Lua("report(player:getMaxSkillLevel(75, xi.job.PLD, xi.skill.CLUB))"));
        Check("the club capped in memory, blue", $"{pldClub * 10}/{pldClub | 0x8000}", $"{c.RealSkills[Skill.Club]}/{c.WorkingSkills[Skill.Club]}");
        Check("CLISTATUS2", 1, cliStatus2);

        // the era module unlocking_a_myth.lua (xi.pre(xi.expansion.ABYSSEA): RESTRICT_CONTENT 1, ENABLE_ABYSSEA 0),
        // loaded by the server's Lua module loader: a Vigil weapon's onItemEquip override sets its total from
        // xi.equipment.vigilWeaponRequiredWsPoints (NyzulFloorProgress 0: 16000, 100: 250)
        Console.WriteLine("unlocking_a_myth (era module): the brave blade's onItemEquip sets its own total");
        Lua("xi.settings.main.RESTRICT_CONTENT = 1 xi.settings.main.ENABLE_ABYSSEA = 0");
        zone.ModuleInitLines = new[] { "era/lua/globals/equipment/unlocking_a_myth.lua" };
        zone.LoadLuaModules();
        Check("the module enabled", "unlocking_a_myth:True",
              string.Join(",", zone.LuaModules.Modules.Where(m => m.Name == "unlocking_a_myth")
                                   .Select(m => $"{m.Name}:{m.Enabled}")));
        Equip(0, CharStats.SlotMain);
        Equip(2, CharStats.SlotMain);
        Check("equipped at NyzulFloorProgress 0: 16000 (no longer broken at its 50 points)", "16000/False",
              $"{Lua(Main + "report(i:getWeaponskillPointsNeeded())")}/{CharStats.HasWeaponSkill(c, Atonement)}");
        Lua("player:setCharVar('NyzulFloorProgress', 100)");
        Equip(0, CharStats.SlotMain);
        Equip(2, CharStats.SlotMain);
        Check("  again at 100: 250", "250", Lua(Main + "report(i:getWeaponskillPointsNeeded())"));
        Lua("player:setCharVar('NyzulFloorProgress', 0)");

        Check("no script errors", "", string.Join("\n", log.FindAll(l => !l.Contains("global scripts") && (l.Contains("lua:") || l.Contains("luautils::") || l.Contains("rror")))));
    }
}
