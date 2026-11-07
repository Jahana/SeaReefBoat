using System;
using System.Collections.Generic;
using System.IO;
using XI.Common;
using XI.Map;
using XI.Ipc;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Latent.Live -- live gate for the latent effects (CLatentEffectContainer: AddLatentEffects / DelLatentEffects
// from item_latents on equip and unequip, CheckLatentsHP / TP / WeaponDraw / WeekDay / Day, CLatentEffect::Activate /
// Deactivate, the addLatent / delLatent / hasAllLatentsActive bindings) with real item_latents gear on a DRK75 in West
// Ronfaure against the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 991150;
    private const ushort WestRonfaure = 100;
    private const uint Rabbit = 17186822;
    private const ushort RabbitTargId = 6;
    // item_latents: carapace helm HP <= 50% DEF+23 ATT+10; perdu sword TP < 1000 ATT+12 ACC+5; frenzy sallet weapon
    // drawn ACC+12; poroggo cassock +1 watersday HASTE_GEAR+2500 (and more); dinner jacket night CHR+1, HP <= 75% ENMITY-1
    private const ushort CarapaceHelm = 13878, PerduSword = 16602, FrenzySallet = 15174, PoroggoCassock = 23804, DinnerJacket = 11355;
    private const byte SlotHead = 4, SlotBody = 5;

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

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("latentverify"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        // a Hume with the NEW_ADVENTURER title (not a first login: no charCreate), standing at the zone's entrance (a
        // char at 0,0,0 is moved there by the zone's onZoneIn)
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,titles) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Latenty"), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(-126.0f), BoundValue.Of(-62.0f), BoundValue.Of(273.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0), BoundValue.Of(titles));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(CharId));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 300, 50, 8, 75)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, drk) VALUES (?, 75)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));
        void Item(byte slot, ushort id) =>
            Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?, 0, ?, ?, 1, ?)",
                BoundValue.Of(CharId), BoundValue.Of(slot), BoundValue.Of(id), BoundValue.Of(new byte[24]));
        Item(1, CarapaceHelm);
        Item(2, PerduSword);
        Item(3, FrenzySallet);
        Item(4, PoroggoCassock);
        Item(5, DinnerJacket);

        // a watersday, 23:00
        long vana = 8002 * VanaTime.DAY + 23 * VanaTime.HOUR;
        Check("the clock: watersday 23:00", "2/23", $"{VanaTime.GetWeekDay(vana)}/{VanaTime.GetHour(vana)}");

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        zone.VanaTicksNow = () => vana;
        zone.MobRng = new Random(7);
        zone.SendIpc = _ => { };
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);
        var rabbit = zone.ZoneInfoOf(WestRonfaure).MobById[Rabbit];

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
        void Equip(byte invSlot, byte equipSlot) => Send(0x050, 8, b => { b[4] = invSlot; b[5] = equipSlot; b[6] = 0; });
        void Tick(long ms)
        {
            now += ms;
            zone.Tick();
        }

        var lua = scripts.Lua;
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local player = GetPlayerByID({CharId})\n" + code, "=verify_latent");
            return string.Join(",", got);
        }
        List<LatentEffect> In(byte slot) => new List<LatentEffect>(c.Latents.All).FindAll(l => l.Slot == slot);
        string Active(byte slot) => string.Join(" ", In(slot).ConvertAll(l => $"{(ushort)l.ModValue}:{(l.Activated ? 1 : 0)}"));
        void FullHp() => Lua("player:addHP(99999)");
        void HpTo(int percent) => Lua($"player:addHP({c.MaxHp * percent / 100 - c.Hp})");

        Console.WriteLine("DRK75, nothing on");
        Check("logged in DRK75", "8/75", $"{c.MJob}/{c.MLvl}");
        Check("no latents", 0, c.Latents.All.Count);
        FullHp();
        Check("full HP", c.MaxHp, c.Hp);

        Console.WriteLine("HP_UNDER_PERCENT: the carapace helm (HP <= 50%: DEF+23, ATT+10)");
        short att0 = c.GetMod(Mod.ATT), def0 = c.GetMod(Mod.DEF);
        Equip(1, SlotHead);
        Check("the helm on", CarapaceHelm, c.Equipped[SlotHead]?.ItemId ?? 0);
        Check("equip adds its two latents, both off at full HP", "1:0 23:0", Active(SlotHead));
        short attHelm = c.GetMod(Mod.ATT), defHelm = c.GetMod(Mod.DEF);
        HpTo(60);
        Check("HP 60%: still off", "1:0 23:0", Active(SlotHead));
        HpTo(40);
        Check("HP 40% through addHP: both on", "1:1 23:1", Active(SlotHead));
        Check("the mods applied: ATT+10, DEF+23", "10/23", $"{c.GetMod(Mod.ATT) - attHelm}/{c.GetMod(Mod.DEF) - defHelm}");
        HpTo(80);
        Check("HP 80%: both off again", "1:0 23:0", Active(SlotHead));
        Check("the mods removed", "0/0", $"{c.GetMod(Mod.ATT) - attHelm}/{c.GetMod(Mod.DEF) - defHelm}");
        HpTo(30);
        Check("HP 30%: on again", "1:1 23:1", Active(SlotHead));
        Equip(0, SlotHead);
        Check("unequipped while on: the latents gone", "0/", $"{c.Equipped[SlotHead]?.ItemId ?? 0}/{Active(SlotHead)}");
        Check("ATT and DEF back where they were bare headed", $"{att0}/{def0}", $"{c.GetMod(Mod.ATT)}/{c.GetMod(Mod.DEF)}");
        Check("hasAllLatentsActive on an empty slot: true", "true", Lua($"report(tostring(player:hasAllLatentsActive({SlotHead})))"));
        Equip(1, SlotHead);
        Check("equipped at 30% HP: on at once (CheckLatentsEquip)", "1:1 23:1", Active(SlotHead));
        Check("hasAllLatentsActive: true", "true", Lua($"report(tostring(player:hasAllLatentsActive({SlotHead})))"));
        FullHp();
        Check("hasAllLatentsActive at full HP: false", "false", Lua($"report(tostring(player:hasAllLatentsActive({SlotHead})))"));
        Equip(0, SlotHead);

        Console.WriteLine("TP_UNDER: the perdu sword (TP < 1000: ATT+12, ACC+5)");
        Lua("player:setTP(0)");
        short attBare = c.GetMod(Mod.ATT), accBare = c.GetMod(Mod.ACC);
        Equip(2, CharStats.SlotMain);
        Check("the sword on", PerduSword, c.Equipped[CharStats.SlotMain]?.ItemId ?? 0);
        var sword = In(CharStats.SlotMain);
        Check("its three latents, on at 0 TP", "23:1 25:1 287:1", Active(CharStats.SlotMain));
        short attOn = c.GetMod(Mod.ATT), accOn = c.GetMod(Mod.ACC);
        Lua("player:addTP(1500)");
        Check("1500 TP through addTP: off", "1500/23:0 25:0 287:0", $"{c.Tp}/{Active(CharStats.SlotMain)}");
        Check("the mods removed: ATT-12, ACC-5", "-12/-5", $"{c.GetMod(Mod.ATT) - attOn}/{c.GetMod(Mod.ACC) - accOn}");
        Lua("player:addTP(-600)");
        Check("900 TP: on again", "900/23:1 25:1 287:1", $"{c.Tp}/{Active(CharStats.SlotMain)}");
        Check("the mods back", "0/0", $"{c.GetMod(Mod.ATT) - attOn}/{c.GetMod(Mod.ACC) - accOn}");
        Lua("player:addTP(100)");
        Check("1000 TP exactly: off (TP < 1000)", "23:0 25:0 287:0", Active(CharStats.SlotMain));
        Lua("player:setTP(0)");
        Lua("player:addTP(0)");
        Check("back to 0, rechecked by addTP: on", "23:1 25:1 287:1", Active(CharStats.SlotMain));

        Console.WriteLine("WEAPON_DRAWN: the frenzy sallet (ACC+12) on engage and disengage");
        Equip(3, SlotHead);
        Check("the sallet on, its three latents off sheathed", $"{FrenzySallet}/25:0 384:0 404:0", $"{c.Equipped[SlotHead]?.ItemId ?? 0}/{Active(SlotHead)}");
        short accSheathed = c.GetMod(Mod.ACC);
        Send(0x015, 32, b =>
        {
            BitConverter.GetBytes(rabbit.X - 1).CopyTo(b, C2sPos.OffX);
            BitConverter.GetBytes(rabbit.Y).CopyTo(b, C2sPos.OffZ);
            BitConverter.GetBytes(rabbit.Z).CopyTo(b, C2sPos.OffY);
            b[C2sPos.OffDir] = 0;
        });
        void Action(ActionId id, ushort targid) => Send(0x01A, 28, b =>
        {
            BitConverter.GetBytes(Rabbit).CopyTo(b, 4);
            BitConverter.GetBytes(targid).CopyTo(b, C2sAction.OffActIndex);
            BitConverter.GetBytes((ushort)id).CopyTo(b, C2sAction.OffActionID);
        });
        Action(ActionId.Attack, RabbitTargId);
        Check("engaged, weapon out", "True/1", $"{c.AttackTarget == rabbit}/{c.Animation}");
        Check("drawn: on, ACC+12", "25:1 384:1 404:1/12", $"{Active(SlotHead)}/{c.GetMod(Mod.ACC) - accSheathed}");
        Action(ActionId.AttackOff, 0);
        Tick(10);
        Check("disengaged", "True/0", $"{c.AttackTarget == null}/{c.Animation}");
        Check("sheathed: off, ACC back", "25:0 384:0 404:0/0", $"{Active(SlotHead)}/{c.GetMod(Mod.ACC) - accSheathed}");
        Equip(0, SlotHead);
        Equip(0, CharStats.SlotMain);
        Check("both off: no latents left", 0, c.Latents.All.Count);
        Check("ATT and ACC back where they were", $"{attBare}/{accBare}", $"{c.GetMod(Mod.ATT)}/{c.GetMod(Mod.ACC)}");

        Console.WriteLine("WATERSDAY: the poroggo cassock +1 (HASTE_GEAR+2500 and four more) on the day's change");
        short haste0 = c.GetMod(Mod.HASTE_GEAR);
        Equip(4, SlotBody);
        Check("the cassock on, on a watersday: all on", $"{PoroggoCassock}/239:1 384:1 416:1 476:1 958:1",
              $"{c.Equipped[SlotBody]?.ItemId ?? 0}/{Active(SlotBody)}");
        Check("HASTE_GEAR+2500", 2500, c.GetMod(Mod.HASTE_GEAR) - haste0);
        Tick(10);
        vana += VanaTime.HOUR;
        Tick(10);
        Check("windsday 00:00 through the hour tick: all off, HASTE_GEAR back", $"3/0/239:0 384:0 416:0 476:0 958:0/{haste0}",
              $"{VanaTime.GetWeekDay(vana)}/{VanaTime.GetHour(vana)}/{Active(SlotBody)}/{c.GetMod(Mod.HASTE_GEAR)}");
        vana += 7 * VanaTime.DAY;
        Tick(10);
        Check("the next watersday: on again", $"2/239:1 384:1 416:1 476:1 958:1/{haste0 + 2500}",
              $"{VanaTime.GetWeekDay(vana)}/{Active(SlotBody)}/{c.GetMod(Mod.HASTE_GEAR)}");
        Equip(0, SlotBody);
        Check("unequipped: the latents and HASTE_GEAR gone", $"/{haste0}", $"{Active(SlotBody)}/{c.GetMod(Mod.HASTE_GEAR)}");

        Console.WriteLine("TIME_OF_DAY night: the dinner jacket (CHR+1) on the time of day's change");
        FullHp();
        short chr0 = c.GetMod(Mod.CHR);
        Equip(5, SlotBody);
        Check("the jacket on at 00:00: CHR on, the HP one off", $"{DinnerJacket}/14:1 27:0/1",
              $"{c.Equipped[SlotBody]?.ItemId ?? 0}/{Active(SlotBody)}/{c.GetMod(Mod.CHR) - chr0}");
        vana += 6 * VanaTime.HOUR;
        Tick(10);
        Check("06:00, daytime: off, CHR back", "6/14:0 27:0/0", $"{VanaTime.GetHour(vana)}/{Active(SlotBody)}/{c.GetMod(Mod.CHR) - chr0}");
        vana += 12 * VanaTime.HOUR;
        Tick(10);
        Check("18:00, night again: on", "18/14:1 27:0/1", $"{VanaTime.GetHour(vana)}/{Active(SlotBody)}/{c.GetMod(Mod.CHR) - chr0}");
        Equip(0, SlotBody);
        Check("unequipped", $"/{chr0}", $"{Active(SlotBody)}/{c.GetMod(Mod.CHR)}");

        Console.WriteLine("addLatent / delLatent: a script's TP_OVER 500 STR+7");
        short str0 = c.GetMod(Mod.STR);
        Lua("player:setTP(0)");
        Lua("player:addLatent(xi.latent.TP_OVER, 500, xi.mod.STR, 7)");
        Check("added off, in no slot (MAX_SLOTTYPE)", "1/8:0/0", $"{c.Latents.All.Count}/{Active(18)}/{c.GetMod(Mod.STR) - str0}");
        Lua("player:addTP(800)");
        Check("800 TP: on, STR+7", "8:1/7", $"{Active(18)}/{c.GetMod(Mod.STR) - str0}");
        Check("delLatent with another value: false", "false", Lua("report(tostring(player:delLatent(xi.latent.TP_OVER, 500, xi.mod.STR, 6)))"));
        Check("delLatent: true, removed and STR back", "true/0/0", $"{Lua("report(tostring(player:delLatent(xi.latent.TP_OVER, 500, xi.mod.STR, 7)))")}/{c.Latents.All.Count}/{c.GetMod(Mod.STR) - str0}");
        Check("delLatent again: false", "false", Lua("report(tostring(player:delLatent(xi.latent.TP_OVER, 500, xi.mod.STR, 7)))"));

        Check("no script errors", "", string.Join("\n", log.FindAll(l => !l.Contains("global scripts") && (l.Contains("lua:") || l.Contains("luautils::") || l.Contains("rror")))));
    }
}
