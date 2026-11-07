using System;
using System.Collections.Generic;
using System.IO;
using XI.Common;
using XI.Map;
using XI.Ipc;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.WsAction.Live -- live gate for the weapon skill action (ZoneHost.WeaponSkillAction:
// CPlayerController::WeaponSkill, CWeaponSkillState, CCharEntity::OnWeaponSkillFinished, battleutils::
// TakeWeaponskillDamage and the weapon skill script bindings) with Fast Blade against a Wild Rabbit in West Ronfaure
// and the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 990954;
    private const ushort WestRonfaure = 100;
    private const uint Rabbit = 17186822;
    private const ushort RabbitTargId = 6;
    private const ushort BronzeSword = 16535;
    private const ushort FastBlade = 32, BurningBlade = 33;

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

    private static ulong U(BasicPacket p, int bit, int len) => Battle2Packets.Unpack(p.Buffer, bit, len);

    // a BATTLE2's actor / category / action id / target count / first target
    private static string Battle2Head(BasicPacket p) => $"{U(p, 40, 32)}/{U(p, 82, 4)}/{U(p, 86, 32)}/{U(p, 72, 6)}/{U(p, 150, 32)}";

    // the first target's results: resolution, animation, param and message of each
    private static List<(int Res, int Anim, int Param, int Msg)> Battle2Results(BasicPacket p)
    {
        var list = new List<(int, int, int, int)>();
        int n = (int)U(p, 182, 4);
        for (int i = 0; i < n; i++)
        {
            int at = 186 + i * 87;
            list.Add(((int)U(p, at, 3), (int)U(p, at + 5, 12), (int)U(p, at + 27, 17), (int)U(p, at + 44, 10)));
        }
        return list;
    }

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("wsactverify"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        // a Hume with the NEW_ADVENTURER title (not a first login: no charCreate), standing at the zone's entrance (a
        // char at 0,0,0 is moved there by the zone's onZoneIn)
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,titles) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Blady"), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(-126.0f), BoundValue.Of(-62.0f), BoundValue.Of(273.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0), BoundValue.Of(titles));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(CharId));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 300, 50, 1, 30)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 30)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));
        // sword skill 10: fast blade (5), not burning blade (30)
        Sql(db, "INSERT INTO char_skills(charid, skillid, value, rank) VALUES (?, 3, 100, 0)", BoundValue.Of(CharId));
        Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?, 0, 1, ?, 1, ?)",
            BoundValue.Of(CharId), BoundValue.Of(BronzeSword), BoundValue.Of(new byte[24]));
        // a bottle of yellow liquid (a food for an enemy target) for the item use on a mob
        Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?, 0, 2, 5264, 1, ?)",
            BoundValue.Of(CharId), BoundValue.Of(new byte[24]));

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        zone.VanaTicksNow = () => 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        zone.MobRng = new Random(7);
        zone.SendIpc = _ => { };
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);

        var info = zone.ZoneInfoOf(WestRonfaure);
        var rabbit = info.MobById[Rabbit];
        Check("rabbit up", true, rabbit.IsSpawned);
        rabbit.BaseMaxHp = rabbit.MaxHp = rabbit.Hp = 5000; // the base too: an effect on it (the resonance) recalculates the max

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
        void Pos(float dx, byte dir, float dy = 0)
        {
            Send(0x015, 32, b =>
            {
                BitConverter.GetBytes(rabbit.X + dx).CopyTo(b, C2sPos.OffX);
                BitConverter.GetBytes(rabbit.Y + dy).CopyTo(b, C2sPos.OffZ);
                BitConverter.GetBytes(rabbit.Z).CopyTo(b, C2sPos.OffY);
                b[C2sPos.OffDir] = dir;
            });
            s.PacketList.Clear();
        }
        void Ws(ushort wsid, ushort targid)
        {
            s.PacketList.Clear();
            Send(0x01A, 28, b =>
            {
                BitConverter.GetBytes(Rabbit).CopyTo(b, 4);
                BitConverter.GetBytes(targid).CopyTo(b, C2sAction.OffActIndex);
                BitConverter.GetBytes((ushort)ActionId.Weaponskill).CopyTo(b, C2sAction.OffActionID);
                C2sAction.SetSkillId(b, wsid);
            });
        }
        List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);
        string Msg(BasicPacket p) => p == null ? "" : $"{BitConverter.ToUInt32(p.Buffer, 4)}>{BitConverter.ToUInt32(p.Buffer, 8)}:{BitConverter.ToUInt16(p.Buffer, 24)}";
        string LastMsg() { var l = Of(0x029); return l.Count == 0 ? "" : Msg(l[^1]); }
        void Tick(long ms)
        {
            s.PacketList.Clear();
            now += ms;
            zone.Tick();
        }

        s.PacketList.Clear();
        Send(0x050, 8, b => { b[4] = 1; b[5] = CharStats.SlotMain; b[6] = 0; });
        Check("the sword on, fast blade known, burning blade not", $"{BronzeSword}/True/False",
            $"{c.Equipped[CharStats.SlotMain]?.ItemId ?? 0}/{CharStats.HasWeaponSkill(c, FastBlade)}/{CharStats.HasWeaponSkill(c, BurningBlade)}");

        Console.WriteLine("The refusals");
        Pos(-1, 0);
        c.Tp = 0;
        Ws(BurningBlade, RabbitTargId);
        Check("burning blade not known: 190", $"{CharId}>{CharId}:190", LastMsg());
        Ws(300, RabbitTargId);
        Check("no such weapon skill: 190", $"{CharId}>{CharId}:190", LastMsg());
        Ws(FastBlade, RabbitTargId);
        Check("0 TP: 192", $"{CharId}>{CharId}:192", LastMsg());
        c.Tp = 1000;
        Ws(FastBlade, c.TargId);
        Check("at oneself (not a self target weapon skill): 446", $"{CharId}>{CharId}:446", LastMsg());
        Pos(-1, 128);
        Ws(FastBlade, RabbitTargId);
        Check("facing away: 217", $"{CharId}>{Rabbit}:217", LastMsg());
        Check("no state, TP kept", "True/1000", $"{!c.InWeaponSkill}/{c.Tp}");

        Pos(-1, 0, 15);
        Ws(FastBlade, RabbitTargId);
        Check("15 yalms under the ground: no line of sight, 71", $"{CharId}>{Rabbit}:71", LastMsg());
        Check("no state, TP kept", "True/1000", $"{!c.InWeaponSkill}/{c.Tp}");

        Console.WriteLine("Out of range at the finish");
        // 20 yalms along x at the rabbit's height is inside the hillside: this part without the collision mesh
        string meshDir = zone.XiMeshDir;
        zone.XiMeshDir = null;
        Pos(-20, 0);
        Ws(FastBlade, RabbitTargId);
        var readies = Of(0x028);
        Check("the readies BATTLE2", 1, readies.Count);
        if (readies.Count == 1)
        {
            Check("actor / skill start / FourCC / 1 target / rabbit", $"{CharId}/7/{0x65746163}/1/{Rabbit}", Battle2Head(readies[0]));
            var r = Battle2Results(readies[0]);
            Check("param fast blade, msg 43", $"1/{FastBlade}/43", $"{r.Count}/{r[0].Param}/{r[0].Msg}");
        }
        Check("in the state", true, c.InWeaponSkill);
        Send(0x050, 8, b => { b[4] = 0; b[5] = CharStats.SlotMain; b[6] = 0; });
        Check("EquipItem: the weapon stays on during the weapon skill", BronzeSword, c.Equipped[CharStats.SlotMain]?.ItemId ?? 0);
        Ws(FastBlade, RabbitTargId);
        Check("a second one meanwhile: 89", $"{CharId}>{CharId}:89", LastMsg());
        Tick(1);
        var oor = Of(0x028);
        Check("the interrupt BATTLE2", 1, oor.Count);
        if (oor.Count == 1)
        {
            Check("actor / magic finish / no id / 1 target / rabbit", $"{CharId}/4/0/1/{Rabbit}", Battle2Head(oor[0]));
            var r = Battle2Results(oor[0]);
            Check("animation 508, msg 78", "508/78", $"{r[0].Anim}/{r[0].Msg}");
        }
        Check("the TP spent anyway, the rabbit untouched", "0/5000", $"{c.Tp}/{rabbit.Hp}");
        Check("not claimed", 0, rabbit.OwnerId);
        Tick(10_000);
        Check("the state over after the animation", true, !c.InWeaponSkill);

        zone.XiMeshDir = meshDir;

        Console.WriteLine("Fast Blade");
        Pos(-1, 0);
        c.Tp = 1000;
        Ws(FastBlade, RabbitTargId);
        Check("the readies", 1, Of(0x028).Count);
        Tick(1);
        var finish = Of(0x028);
        Check("the finish BATTLE2", 1, finish.Count);
        int dealt = -1;
        if (finish.Count == 1)
        {
            Check("actor / skill finish / fast blade / 1 target / rabbit", $"{CharId}/3/{FastBlade}/1/{Rabbit}", Battle2Head(finish[0]));
            var r = Battle2Results(finish[0]);
            Check("one result", 1, r.Count);
            dealt = r[0].Param;
            Check("hit or miss: msg 185 with the damage, or 188 with 0", true,
                r[0].Res == 0 && r[0].Msg == 185 && r[0].Param > 0 || r[0].Msg == 188 && r[0].Param == 0);
            Check("the animation of fast blade", true, r[0].Anim > 0);
        }
        Check("the rabbit's HP down by the damage", 5000 - dealt, rabbit.Hp);
        Check("claimed", CharId, rabbit.OwnerId);
        Check("TP spent, the TP return back", true, c.Tp > 0 && c.Tp < 1000);
        Check("on the rabbit's enmity list", true, rabbit.Enmity.HasId(CharId));
        Check("still in the state (the animation)", true, c.InWeaponSkill);
        Tick(10_000);
        Check("the state over", true, !c.InWeaponSkill);

        Console.WriteLine("An item used on a mob (CItemState with a mob target)");
        s.PacketList.Clear();
        Send(0x037, 20, b =>
        {
            BitConverter.GetBytes(Rabbit).CopyTo(b, 4);
            BitConverter.GetBytes(RabbitTargId).CopyTo(b, 12);
            b[14] = 2;
        });
        var itemStart = Of(0x028);
        Check("the item start on the rabbit", $"1/{CharId}/9/{0x74696163}/1/{Rabbit}",
            $"{itemStart.Count}/" + (itemStart.Count > 0 ? Battle2Head(itemStart[0]) : "-"));
        Tick(1500);
        var itemFinish = Of(0x028).Find(p => U(p, 82, 4) == 5);
        Check("the item finish on the rabbit, the food on it", $"{CharId}/5/5264/1/{Rabbit}/True",
            (itemFinish != null ? Battle2Head(itemFinish) : "-") + $"/{rabbit.StatusEffects.HasStatusEffect(EffectId.Food)}");
        Check("the bottle used up", 0, c.Items.FindAll(i => i.ItemId == 5264).Count);

        Console.WriteLine("The bindings from Lua");
        var lua = scripts.Lua;
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local player = GetPlayerByID({CharId})\n" + code, "=verify_wsact");
            return string.Join(",", got);
        }
        Check("getWeaponDamageType(main): slashing", "2", Lua("report(player:getWeaponDamageType(xi.slot.MAIN))"));
        Check("getWeaponHitCount", "1", Lua("report(player:getWeaponHitCount(false))"));
        Check("getWSSkillchainProp outside a weapon skill", "0,0,0", Lua("local a, b, c = player:getWSSkillchainProp() report(a) report(b) report(c)"));
        Check("physicalDmgTaken: no mods", "100", Lua("report(player:physicalDmgTaken(100, xi.damageType.SLASHING))"));
        c.SetModifier(Mod.DMGPHYS, -2500);
        Check("physicalDmgTaken: -25% PDT", "75", Lua("report(player:physicalDmgTaken(100, xi.damageType.SLASHING))"));
        c.SetModifier(Mod.DMGPHYS, 0);
        Check("rangedDmgTaken: no mods", "100", Lua("report(player:rangedDmgTaken(100))"));

        Console.WriteLine("Blue magic learned from the kill (TryLearningSpells)");
        // a blue mage now, certain to learn: the rabbit used Foot Kick (mob skill 257, blue spell 577)
        c.MJob = 16;
        c.SetModifier(Mod.BLUE_LEARN_CHANCE, 100);
        rabbit.UsedSkillIds[257] = 1;
        rabbit.Hp = 1;
        var learned = new List<BasicPacket>();
        for (int i = 0; i < 5 && !rabbit.IsDead; i++)
        {
            Pos(-1, 0);
            c.Tp = 1000;
            Ws(FastBlade, RabbitTargId);
            Tick(1);
            learned.AddRange(Of(0x029));
            Tick(10_000);
            learned.AddRange(Of(0x029));
        }
        Check("the rabbit killed", true, rabbit.IsDead);
        Check("Foot Kick learned: the spell bit, char_spells, the LEARNS_SPELL message, the used skills cleared",
            $"True/1/True/0",
            $"{(c.SpellList[577 / 8] & 1 << 577 % 8) != 0}/{(Sql(db, "SELECT spellid FROM char_spells WHERE charid = ? AND spellid = 577", BoundValue.Of(CharId)) is { } sr && sr.Next() ? 1 : 0)}/" +
            $"{learned.Exists(p => BitConverter.ToUInt16(p.Buffer, 24) == 419 && BitConverter.ToUInt32(p.Buffer, 12) == 577)}/{rabbit.UsedSkillIds.Count}");

        // the food check's getRace on the rabbit warns, as CLuaBaseEntity::getRace does for a non-char
        Check("no script errors", "", string.Join("\n", log.FindAll(l => !l.Contains("global scripts") && !l.Contains("(getRace on Wild_Rabbit)") &&
            (l.Contains("lua:") || l.Contains("luautils::") || l.Contains("rror")))));
    }
}
