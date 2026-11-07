using System;
using System.Collections.Generic;
using System.IO;
using XI.Common;
using XI.Map;
using XI.Ipc;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Skillchain.Live -- live gate for skillchains (ZoneHost.Skillchains: battleutils::FormSkillchain,
// GetSkillChainEffect, TakeSkillchainDamage, action_result_t::recordSkillchain and the BATTLE2 additional effect
// block) with Fast Blade (scission) and Burning Blade (liquefaction) against a Wild Rabbit in West Ronfaure and the
// real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 990956;
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

    // the first target's first result's additional effect block: present / effect / param / message
    private static string AddEffect(BasicPacket p)
    {
        const int at = 186 + 85;
        if (U(p, at, 1) == 0) return "0";
        return $"1/{U(p, at + 1, 6)}/{U(p, at + 11, 17)}/{U(p, at + 28, 10)}";
    }

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("scverify"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        // a Hume with the NEW_ADVENTURER title (not a first login: no charCreate), standing at the zone's entrance (a
        // char at 0,0,0 is moved there by the zone's onZoneIn)
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,titles) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Chainy"), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(-126.0f), BoundValue.Of(-62.0f), BoundValue.Of(273.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0), BoundValue.Of(titles));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(CharId));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 300, 50, 1, 30)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 30)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));
        // sword skill 40: fast blade (5) and burning blade (30)
        Sql(db, "INSERT INTO char_skills(charid, skillid, value, rank) VALUES (?, 3, 400, 0)", BoundValue.Of(CharId));
        Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?, 0, 1, ?, 1, ?)",
            BoundValue.Of(CharId), BoundValue.Of(BronzeSword), BoundValue.Of(new byte[24]));

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
        rabbit.BaseMaxHp = rabbit.MaxHp = rabbit.Hp = 5000; // the base too: an effect on it recalculates the max

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
        void Pos(float dx, byte dir)
        {
            Send(0x015, 32, b =>
            {
                BitConverter.GetBytes(rabbit.X + dx).CopyTo(b, C2sPos.OffX);
                BitConverter.GetBytes(rabbit.Y).CopyTo(b, C2sPos.OffZ);
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
        Check("the sword on, fast blade and burning blade known", $"{BronzeSword}/True/True",
            $"{c.Equipped[CharStats.SlotMain]?.ItemId ?? 0}/{CharStats.HasWeaponSkill(c, FastBlade)}/{CharStats.HasWeaponSkill(c, BurningBlade)}");

        Console.WriteLine("The tables");
        Check("scission resonance, liquefaction skill: liquefaction", ZoneHost.ScLiquefaction,
            ZoneHost.FormSkillchain(new List<byte> { ZoneHost.ScScission }, new List<byte> { ZoneHost.ScLiquefaction, 0, 0 }));
        Check("liquefaction resonance, scission skill: scission", ZoneHost.ScScission,
            ZoneHost.FormSkillchain(new List<byte> { ZoneHost.ScLiquefaction }, new List<byte> { ZoneHost.ScScission, 0, 0 }));
        Check("light on light: light II", ZoneHost.ScLightII,
            ZoneHost.FormSkillchain(new List<byte> { ZoneHost.ScLight }, new List<byte> { ZoneHost.ScLight }));
        Check("scission on scission: none", ZoneHost.ScNone,
            ZoneHost.FormSkillchain(new List<byte> { ZoneHost.ScScission }, new List<byte> { ZoneHost.ScScission }));
        Check("the tiers of transfixion / gravitation / light / darkness II", "1/2/3/4",
            $"{ZoneHost.GetSkillchainTier(ZoneHost.ScTransfixion)}/{ZoneHost.GetSkillchainTier(ZoneHost.ScGravitation)}/" +
            $"{ZoneHost.GetSkillchainTier(ZoneHost.ScLight)}/{ZoneHost.GetSkillchainTier(ZoneHost.ScDarknessII)}");
        Check("the subeffects of liquefaction / light II", $"{ZoneHost.ProcScLiquefaction}/{ZoneHost.ProcScLight}",
            $"{ZoneHost.GetSkillchainSubeffect(ZoneHost.ScLiquefaction)}/{ZoneHost.GetSkillchainSubeffect(ZoneHost.ScLightII)}");
        Check("fusion's magic elements: light, fire", "7,1", string.Join(",", ZoneHost.GetSkillchainMagicElement(ZoneHost.ScFusion)));

        const ushort EffectSkillchain = 799;
        string Effect()
        {
            var e = rabbit.StatusEffects.GetStatusEffect(EffectSkillchain);
            return e == null ? "none" : $"{e.Power}/{e.Tier}/{e.SubPower}";
        }
        Pos(-1, 0);
        c.SetModifier(Mod.ACC, 300); // no misses

        // one weapon skill on the rabbit: the finish BATTLE2's damage and additional effect block, and the HP lost
        (int Dmg, string Add, int HpLost) Use(ushort wsid)
        {
            c.Tp = 1000;
            int hp = rabbit.Hp;
            Ws(wsid, RabbitTargId);
            Tick(1);
            var fin = Of(0x028);
            if (fin.Count != 1) return (-1, "no finish", 0);
            var r = Battle2Results(fin[0]);
            var res = (r[0].Param, AddEffect(fin[0]), hp - rabbit.Hp);
            Tick(2001); // the animation time
            return res;
        }

        Console.WriteLine("The opener");
        var open = Use(FastBlade);
        Check("fast blade hit", true, open.Dmg > 0);
        Check("no additional effect on an opener", "0", open.Add);
        Check("the resonance: fast blade's properties at tier 0", $"{ZoneHost.ScScission}/0/0", Effect());
        Check("the state over", true, !c.InWeaponSkill);

        Console.WriteLine("Too soon (within 3 s)");
        var soon = Use(BurningBlade);
        Check("no skillchain", "0", soon.Add);
        Check("the resonance reset to burning blade's properties", $"{ZoneHost.ScLiquefaction}/0/0", Effect());

        Console.WriteLine("Scission");
        Tick(1500); // past the 3 s window
        var sc1 = Use(FastBlade);
        Check("fast blade hit", true, sc1.Dmg > 0);
        string[] a1 = sc1.Add.Split('/');
        Check("the skillchain: scission, message 299 (or 396 absorbed)", true,
            a1.Length == 4 && a1[1] == ZoneHost.ProcScScission.ToString() && (a1[3] == "299" || a1[3] == "396"));
        int scDmg1 = a1.Length == 4 ? int.Parse(a1[2]) : -1;
        Check("skillchain damage dealt", true, scDmg1 > 0);
        Check("the HP lost: the weapon skill's and the skillchain's", sc1.Dmg + scDmg1, sc1.HpLost);
        Check("the resonance: scission at tier 1, one link", $"{ZoneHost.ScScission}/1/1", Effect());

        Console.WriteLine("Liquefaction (the second link)");
        Tick(1500);
        var sc2 = Use(BurningBlade);
        string[] a2 = sc2.Add.Split('/');
        Check("the skillchain: liquefaction, message 295", true,
            a2.Length == 4 && a2[1] == ZoneHost.ProcScLiquefaction.ToString() && a2[3] == "295");
        Check("the resonance: liquefaction at tier 1, two links", $"{ZoneHost.ScLiquefaction}/1/2", Effect());
        var eff = rabbit.StatusEffects.GetStatusEffect(EffectSkillchain);
        Check("the window shortened by 1 s a link", true, eff != null && eff.DurationMs == 8000);

        Console.WriteLine("The resonance wears off");
        Tick(15_000);
        Tick(1000);
        Check("no resonance left", "none", Effect());
        Check("rabbit still up", true, rabbit.Hp > 0);

        Check("no script errors", "", string.Join("\n", log.FindAll(l => !l.Contains("global scripts") && (l.Contains("lua:") || l.Contains("luautils::") || l.Contains("rror")))));
    }
}
