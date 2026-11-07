using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Mounts.Live -- live gate for ZoneHost.Mounts.cs and MountUtils: GP_CLI_COMMAND_ACTION's Mount refused
// below level 20, without the key item, in a town and with a mob hating the char; a rented chocobo called (the
// Mounted effect for 30 minutes and its script's chocobo animation, the 60 s recast and 0x119, the mount's speed and
// ChocoboIndex in 0x037 / 0x00D / 0x00A), Dismount, the recast refusing a second call; the char's speed through its
// modifiers on foot; a registered chocobo (registerChocobo, getFieldChocobo, the char_pet row read back, its minutes,
// speed, silks and MOUNT_MOVE, ChocoboIndex 2 and its CustomProperties in 0x00D / 0x067); ChocoboDig spending a Gysahl
// Green for xi.chocoboDig.start with GP_SERV_COMMAND_DIG; get / setChocoboUserData; the raised chocobo's row through
// set / getChocoboRaisingInfo and deleteRaisedChocobo. A WAR 30 in East Ronfaure, with the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 993770;
    private const ushort EastRonfaure = 101, SouthernSandoria = 230;
    private const ushort GysahlGreens = 4545;

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
            lua.DoString($"local player = GetPlayerByID({CharId})\n" + code, "=verify_mounts");
            return string.Join(",", got);
        }

        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("mt" + CharId), BoundValue.Of("x"), BoundValue.Of((byte)1));
        // the NEW_ADVENTURER title: not a first login, no charCreate
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,moghouse,nation,gmlevel,titles) " +
                "VALUES (?,?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Rider"), BoundValue.Of(EastRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(200.0f), BoundValue.Of(-60.0f), BoundValue.Of(-300.0f), BoundValue.Of(0u), BoundValue.Of((byte)0), BoundValue.Of((byte)0),
            BoundValue.Of(titles));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl, sjob, slvl) VALUES (?, 500, 0, 1, 30, 0, 0)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs SET charid = ?, unlocked = 2, war = 30", BoundValue.Of(CharId));
        Sql(db, "INSERT INTO char_storage(charid, inventory) VALUES (?, 30) ON DUPLICATE KEY UPDATE inventory = 30", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(CharId));
        Sql(db, "INSERT INTO char_inventory(charid,location,slot,itemId,quantity,extra) VALUES (?,0,1,?,2,?)",
            BoundValue.Of(CharId), BoundValue.Of(GysahlGreens), BoundValue.Of(new byte[24]));
        Sql(db, "INSERT IGNORE INTO char_unlocks(charid) VALUES (?)", BoundValue.Of(CharId));

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
        var info = zone.ZoneInfoOf(EastRonfaure);
        List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);
        string Messages() => string.Join(",", Of(0x029).ConvertAll(p => $"{BitConverter.ToInt32(p.Buffer, 12)}/{BitConverter.ToUInt16(p.Buffer, 24)}"));
        void Advance(long ms)
        {
            nowMs += ms;
            zone.Tick();
        }
        void Act(ActionId id, uint mountId = 0)
        {
            s.PacketList.Clear();
            Send(0x01A, 28, b =>
            {
                BitConverter.GetBytes(c.Id).CopyTo(b, 4);
                BitConverter.GetBytes(c.TargId).CopyTo(b, C2sAction.OffActIndex);
                BitConverter.GetBytes((ushort)id).CopyTo(b, C2sAction.OffActionID);
                BitConverter.GetBytes(mountId).CopyTo(b, C2sAction.OffMount);
            });
        }
        var mounted = () => c.StatusEffects.GetStatusEffect(EffectId.Mounted);
        BasicPacket Status() => LoginPackets.ServerStatus(c, info, zone.UnixNow());
        uint U32(BasicPacket p, int off) => BitConverter.ToUInt32(p.Buffer, off);
        // 0x037: the speed (Flags1's low 12 bits), Chocobo_Index (Flags0 bits 13..15), mount_id
        string StatusMount(BasicPacket p) => $"{U32(p, 44) & 0xFFF}/{U32(p, 40) >> 13 & 7}/{p.Buffer[91]}";
        // 0x00D's general block: ChocoboIndex (Flags1 bits 5..7), CustomProperties, Flags6.MountIndex
        string UpdateMount()
        {
            var p = LoginPackets.CharPc(c, info, EntityUpdate.Update, (byte)(CharStats.UpdatePos | CharStats.UpdateHp));
            return $"{p.Buffer[28]}/{U32(p, 32) >> 5 & 7}/{U32(p, 52)}/{U32(p, 56)}/{U32(p, 68) >> 4 & 0xFF}";
        }

        Console.WriteLine("Mount refused: below level 20, without the key item, in a town, with a mob hating the char");
        c.MLvl = 15;
        Act(ActionId.Mount);
        Check("level 15: MountRequiredLevel with 20", "20/773", Messages());
        c.MLvl = 30;
        Act(ActionId.Mount);
        Check("no Chocobo Companion: nothing", "/False", $"{Messages()}/{mounted() != null}");
        Lua("player:addKeyItem(xi.keyItem.CHOCOBO_COMPANION)");
        var mob = info.MobById.Values.First(m => m.IsAlive);
        mob.Enmity.UpdateEnmity(c, 0, 1);
        Act(ActionId.Mount);
        Check("a mob hating the char: the mount refuses", "0/339/False", $"{Messages()}/{mounted() != null}");
        mob.Enmity.Clear();
        ushort zoneWas = c.Zone;
        c.Zone = SouthernSandoria;
        Act(ActionId.Mount);
        c.Zone = zoneWas;
        Check("in a town: cannot use in this area", "0/316", Messages());
        Check("no recast from the refusals", false, c.Recasts?.HasRecast(RecastType.Ability, Recast.Mount, 60_000) ?? false);

        Console.WriteLine("A rented chocobo: Mount, the effect and its animation, the recast, the packets, Dismount");
        log.Clear();
        Act(ActionId.Mount);
        var e = mounted();
        Check("Mounted: the chocobo, the personal flag, 30 minutes", "0/64/1800000", e == null ? "none" : $"{e.Power}/{e.SubPower}/{e.DurationMs}");
        Check("its script: the chocobo animation; the mount id", $"{Anim.Chocobo}/0", $"{c.Animation}/{c.MountId}");
        Check("the 60 s recast and 0x119", "True/1", $"{c.Recasts.HasRecast(RecastType.Ability, Recast.Mount, 60_000)}/{Of(0x119).Count}");
        Check("0x037: the mount's speed (80 / 2), ChocoboIndex 1 (no registered chocobo), mount_id 0", "40/1/0", StatusMount(Status()));
        Check("0x00D: the speed, ChocoboIndex 1, no CustomProperties, MountIndex 0", "40/1/0/0/0", UpdateMount());
        var login = LoginPackets.Login(c, info, zone.UnixNow());
        Check("0x00A: the speed, the Mounted sub power in flags2's low byte", "40/64", $"{login.Buffer[28]}/{login.Buffer[32]}");
        Act(ActionId.Dismount);
        Check("Dismount: on foot, no effect, the base speed", "0/False/50", $"{c.Animation}/{mounted() != null}/{MountUtils.UpdateSpeed(c)}");
        Act(ActionId.Mount);
        Check("called again within the minute: wait longer", "0/94/False", $"{Messages()}/{mounted() != null}");
        Check("no Lua errors", "", string.Join(" | ", log.FindAll(l => l.StartsWith("lua") || l.Contains("luautils"))));

        Console.WriteLine("The char's speed on foot");
        c.AddModifier(Mod.MOVE_SPEED_FLEE, 2500);
        Check("Flee +25%", 62, MountUtils.UpdateSpeed(c));
        c.DelModifier(Mod.MOVE_SPEED_FLEE, 2500);
        c.AddModifier(Mod.MOVE_SPEED_WEIGHT_PENALTY, 50);
        Check("gravity -50%", 25, MountUtils.UpdateSpeed(c));
        c.DelModifier(Mod.MOVE_SPEED_WEIGHT_PENALTY, 50);
        c.AddModifier(Mod.MOVE_SPEED_QUICKENING, 15);
        c.AddModifier(Mod.MOVE_SPEED_STACKABLE, 40);
        Check("+40 and Quickening's 10 at most, capped at SPEED_LIMIT", 80, MountUtils.UpdateSpeed(c));
        c.DelModifier(Mod.MOVE_SPEED_QUICKENING, 15);
        c.DelModifier(Mod.MOVE_SPEED_STACKABLE, 40);
        c.AddModifier(Mod.MOVE_SPEED_OVERRIDE, 120);
        Check("MOVE_SPEED_OVERRIDE: past the cap", 120, MountUtils.UpdateSpeed(c));
        c.DelModifier(Mod.MOVE_SPEED_OVERRIDE, 120);
        Check("0x037 carries it", "50", (U32(Status(), 44) & 0xFFF).ToString());

        Console.WriteLine("A registered chocobo");
        Check("getFieldChocobo before registering: nil", "nil", Lua("report(tostring(player:getFieldChocobo()))"));
        Lua("player:registerChocobo({ color = 2, largeBeak = true, speed = 100, minutes = 20, endurance = 30, silksSpeedBonus = 4, ability1 = 7 })");
        uint properties = 1 | 2u << 9 | 100u << 12 | 20u << 19;
        Check("getFieldChocobo: its look, speed, minutes and properties", $"2/true/false/false/100/20/{properties}",
            Lua("local t = player:getFieldChocobo() report(t.color .. '/' .. tostring(t.largeBeak) .. '/' .. tostring(t.fullTail) .. '/' .. " +
                "tostring(t.largeTalons) .. '/' .. t.speed .. '/' .. t.minutes .. '/' .. t.properties)"));
        Check("getChocoboUserData: the registered stats", "30/7/4/0",
            Lua("local d = player:getChocoboUserData() report(d.registeredEndurance .. '/' .. d.registeredAbility1 .. '/' .. d.silksSpeedBonus .. '/' .. d.chocobosRaised)"));
        Check("char_pet.chocobo_user_data: the 32 bytes, the properties first", $"32/{BitConverter.ToString(BitConverter.GetBytes(properties)).Replace("-", "")}",
            Scalar(db, "SELECT CONCAT(LENGTH(chocobo_user_data), '/', HEX(SUBSTRING(chocobo_user_data, 1, 4))) FROM char_pet WHERE charid = ?", BoundValue.Of(CharId)));
        Lua("player:setChocoboUserData({ flags = 5 }) player:setChocoboUserData({ chocobosRaised = 3 })");
        Check("setChocoboUserData: each field kept when left out", "5/3", Lua("local d = player:getChocoboUserData() report(d.flags .. '/' .. d.chocobosRaised)"));
        var fresh = loader.Load(CharId);
        Check("the user data loaded back from char_pet", $"{properties}/5/3/30",
            fresh == null ? "none" : $"{MountUtils.FieldChocobo(fresh)}/{MountUtils.Flags(fresh)}/{MountUtils.ChocobosRaised(fresh)}/{fresh.ChocoboUserData[MountUtils.OffEndurance]}");

        Advance(61_000);
        Act(ActionId.Mount);
        e = mounted();
        Check("Mount after the recast: the registered chocobo for its 20 minutes", "True/1200000/True",
            $"{e != null}/{e?.DurationMs}/{MountUtils.IsPersonalChocobo(c)}");
        Check("0x037: its speed (100 / 2), ChocoboIndex 2", "50/2/0", StatusMount(Status()));
        Check("0x00D: ChocoboIndex 2 and its properties in CustomProperties", $"50/2/{properties}/0/0", UpdateMount());
        var sync = GameOkPackets.CharSync(c);
        Check("0x067: the sub power and the model", $"64/{properties}/0", $"{BitConverter.ToUInt16(sync.Buffer, 0x13)}/{U32(sync, 0x18)}/{U32(sync, 0x1C)}");
        c.AddModifier(Mod.PERSONAL_CHOCOBO_SPEED, 1);
        Check("the silks worn: their 4 speed added", 52, MountUtils.UpdateSpeed(c));
        c.AddModifier(Mod.MOUNT_MOVE, 10);
        Check("MOUNT_MOVE +10%", 57, MountUtils.UpdateSpeed(c));
        c.DelModifier(Mod.MOUNT_MOVE, 10);
        c.DelModifier(Mod.PERSONAL_CHOCOBO_SPEED, 1);

        Console.WriteLine("ChocoboDig: a Gysahl Green for xi.chocoboDig.start, the dig shown");
        log.Clear();
        // the zone-in's digging cooldown (60 s at rank 0 from xi.player.onGameIn's ZoneInTime, a GetSystemTime on the
        // wall clock) gone by
        c.LocalVars["ZoneInTime"] -= 60;
        Act(ActionId.ChocoboDig);
        var greens = c.Items.Find(i => i.ItemId == GysahlGreens && i.Location == Container.Inventory);
        // the dig's find is random (the moon and math.randomInt): an item found syncs the inventory once more
        Check("a green spent, the dig shown to the char, the inventory synced", "1/1/True",
            $"{greens?.Quantity ?? 0}/{Of(0x02F).Count(p => U32(p, 4) == c.Id && p.Buffer[10] == 1)}/{Of(0x01D).Count >= 1}");
        Check("the dig's time kept by the script", true, Lua("report(player:getLocalVar('[DIG]LastDigTime'))") != "0");
        Act(ActionId.ChocoboDig);
        Check("again at once: the cooldown, no dig, the green given back", "1/0", $"{greens?.Quantity ?? 0}/{Of(0x02F).Count}");
        Check("no Lua errors", "", string.Join(" | ", log.FindAll(l => l.StartsWith("lua") || l.Contains("luautils"))));
        Act(ActionId.Dismount);

        Console.WriteLine("The raised chocobo: set / getChocoboRaisingInfo, deleteRaisedChocobo");
        Check("no raised chocobo: nil", "nil", Lua("report(tostring(player:getChocoboRaisingInfo()))"));
        Check("setChocoboRaisingInfo", "true",
            Lua("report(tostring(player:setChocoboRaisingInfo({ first_name = 'Pip', sex = 1, created = 1700000000, stage = 2, color = 3, strength = 40, care_plan = 70000 })))"));
        Check("getChocoboRaisingInfo: the row back, the defaults for the rest", $"{CharId}/Pip/Chocobo/1/1700000000/2/3/40/70000/0",
            Lua("local t = player:getChocoboRaisingInfo() report(t.charid .. '/' .. t.first_name .. '/' .. t.last_name .. '/' .. t.sex .. '/' .. " +
                "t.created .. '/' .. t.stage .. '/' .. t.color .. '/' .. t.strength .. '/' .. t.care_plan .. '/' .. t.hunger)"));
        // created defaults to 0, which FROM_UNIXTIME turns into a value strict mode refuses, as it does for the C++
        Check("an update without created: refused by the database, the row as it was", "false/2",
            Lua("report(tostring(player:setChocoboRaisingInfo({ first_name = 'Pip', stage = 3 }))) report(player:getChocoboRaisingInfo().stage)").Replace(",", "/"));
        Lua("player:setChocoboRaisingInfo({ first_name = 'Pip', created = 1700000000, stage = 3 })");
        Check("an update replaces the row", "3/0", Lua("local t = player:getChocoboRaisingInfo() report(t.stage .. '/' .. t.color)"));
        Check("deleteRaisedChocobo", "true/nil", Lua("report(tostring(player:deleteRaisedChocobo())) report(tostring(player:getChocoboRaisingInfo()))").Replace(",", "/"));
        Check("no script errors", "", string.Join(" | ", log.FindAll(l => l.StartsWith("lua") || l.Contains("Invalid entity"))));
    }
}
