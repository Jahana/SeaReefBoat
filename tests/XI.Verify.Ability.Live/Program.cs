using System;
using System.Collections.Generic;
using System.IO;
using XI.Common;
using XI.Map;
using XI.Ipc;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Ability.Live -- live gate for the job abilities (ZoneHost.AbilityAction: CPlayerController::Ability,
// CAbilityState, CCharEntity::OnAbility, charutils::ApplyAbilityRecast) and the recast container (CRecastContainer,
// CCharRecastContainer, LoadFromCharRecastSQL, GP_SERV_COMMAND_ABIL_RECAST, the recast bindings) with a WAR 30
// against a Wild Rabbit in West Ronfaure and the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 991250;
    private const ushort WestRonfaure = 100;
    private const uint Rabbit = 17186822;
    private const ushort RabbitTargId = 6;
    private const ushort Berserk = 31, Warcry = 32, Defender = 33, Provoke = 35;
    private const ushort RecastBerserk = 1, RecastDefender = 3, RecastProvoke = 5;

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

    // the 0x119's timer for a recast id: its seconds, or -1 when it is not listed
    private static int AbilRecastTimer(BasicPacket p, ushort recastId)
    {
        for (int i = 1; i < 31; i++)
        {
            int at = 4 + 8 * i;
            if (p.Buffer[at + 3] == recastId) return BitConverter.ToUInt16(p.Buffer, at);
        }
        return -1;
    }

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("abilverify"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        // a Hume with the NEW_ADVENTURER title (not a first login: no charCreate), standing at the zone's entrance (a
        // char at 0,0,0 is moved there by the zone's onZoneIn)
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,titles) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Abily"), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(-126.0f), BoundValue.Of(-62.0f), BoundValue.Of(273.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0), BoundValue.Of(titles));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(CharId));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 300, 50, 1, 30)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 30)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));

        var log = new List<string>();
        var loader = new CharLoader(db, grades, AbilityTable.Load(db, _ => true, 1.0), stats);
        var zone = new ZoneHost(db, loader, dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        zone.UnixNow = () => 1_700_000_000 + now / 1000;
        zone.VanaTicksNow = () => 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        zone.MobRng = new Random(7);
        zone.SendIpc = _ => { };
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);

        var info = zone.ZoneInfoOf(WestRonfaure);
        var rabbit = info.MobById[Rabbit];
        Check("rabbit up", true, rabbit.IsSpawned);
        rabbit.MaxHp = rabbit.Hp = 5000;

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
        void Ja(ushort id, ushort targid, uint targetId)
        {
            s.PacketList.Clear();
            Send(0x01A, 28, b =>
            {
                BitConverter.GetBytes(targetId).CopyTo(b, 4);
                BitConverter.GetBytes(targid).CopyTo(b, C2sAction.OffActIndex);
                BitConverter.GetBytes((ushort)ActionId.JobAbility).CopyTo(b, C2sAction.OffActionID);
                C2sAction.SetSkillId(b, id);
            });
        }
        List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);
        // the char's own BATTLE2s (the claimed rabbit fights back)
        List<BasicPacket> Ours() => Of(0x028).FindAll(p => U(p, 40, 32) == CharId);
        string Msg(BasicPacket p) => p == null ? "" : $"{BitConverter.ToUInt32(p.Buffer, 4)}>{BitConverter.ToUInt32(p.Buffer, 8)}:{BitConverter.ToUInt16(p.Buffer, 24)}";
        string Msgs() => string.Join(",", Of(0x029).ConvertAll(p => Msg(p)));
        void Tick(long ms)
        {
            s.PacketList.Clear();
            now += ms;
            zone.Tick();
        }
        int RecastRow(ushort id)
        {
            var r = Sql(db, "SELECT recast FROM char_recast WHERE charid = ? AND id = ?", BoundValue.Of(CharId), BoundValue.Of(id));
            return r != null && r.Next() ? r.Get<int>("recast") : -1;
        }

        Check("WAR 30: provoke, berserk and defender learned, warcry not", "True/True/True/False",
            $"{Has(c, Provoke)}/{Has(c, Berserk)}/{Has(c, Defender)}/{Has(c, Warcry)}");
        Check("the learned abilities listed with no recast", "True/False", $"{c.Recasts.Has(RecastType.Ability, RecastProvoke)}/" +
            $"{c.Recasts.HasRecast(RecastType.Ability, RecastProvoke, 0)}");

        Console.WriteLine("The refusals");
        Pos(-1, 0);
        Ja(1000, RabbitTargId, Rabbit);
        Check("no such ability: 87", $"{CharId}>{CharId}:87", Msgs());
        Ja(Warcry, c.TargId, CharId);
        Check("warcry not learned: the state taken", true, c.InAbility);
        Tick(1);
        Check("warcry not learned: 88 when it goes off", $"{CharId}>{CharId}:88", Msgs());
        Check("the state over at once", true, !c.InAbility);
        Check("no recast for warcry", false, c.Recasts.HasRecast(RecastType.Ability, 2, 0));
        Ja(Provoke, c.TargId, CharId);
        Check("provoke at oneself: 446", $"{CharId}>{CharId}:446", Msgs());

        Console.WriteLine("Provoke");
        // a direct action: the provoker's treasure hunter goes on the rabbit (UpdateEnmity's directAction)
        c.SetModifier(Mod.TREASURE_HUNTER, 2);
        short thBefore = rabbit.ThLvl;
        Ja(Provoke, RabbitTargId, Rabbit);
        Check("no BATTLE2 before it goes off (no cast time)", 0, Ours().Count);
        Tick(1);
        var finish = Ours();
        Check("the BATTLE2", 1, finish.Count);
        if (finish.Count == 1)
        {
            Check("actor / ability finish / provoke / 1 target / rabbit", $"{CharId}/6/{Provoke}/1/{Rabbit}", Battle2Head(finish[0]));
            var r = Battle2Results(finish[0]);
            Check("one result, the message of provoke (119), param 0", "1/119/0", $"{r.Count}/{r[0].Msg}/{r[0].Param}");
        }
        Check("on the rabbit's enmity list, CE and VE up", true,
            rabbit.Enmity.HasId(CharId) && rabbit.Enmity.CE(c) > 0 && rabbit.Enmity.VE(c) > 0);
        Check("claimed", CharId, rabbit.OwnerId);
        Check("the provoker's TH 2 on the rabbit (it had none)", "0/2", $"{thBefore}/{rabbit.ThLvl}");
        c.SetModifier(Mod.TREASURE_HUNTER, 0);
        var recastPacket = Of(0x119);
        Check("the 0x119", 1, recastPacket.Count);
        if (recastPacket.Count == 1) Check("provoke's 30 s in the 0x119", 30, AbilRecastTimer(recastPacket[0], RecastProvoke));
        Check("the recast running", true, c.Recasts.HasRecast(RecastType.Ability, RecastProvoke, 0));
        Check("char_recast row: 30 s", 30, RecastRow(RecastProvoke));
        Check("still in the state (the animation)", true, c.InAbility);
        Tick(2_001);
        Check("the state over after the animation", true, !c.InAbility);

        Console.WriteLine("Provoke again during the recast");
        now += 5_000;
        Ja(Provoke, RabbitTargId, Rabbit);
        Check("88, then 202 with the seconds left", $"{CharId}>{CharId}:88,{CharId}>{CharId}:202", Msgs());
        var timeLeft = Of(0x029);
        if (timeLeft.Count == 2) Check("23 s left", 23, BitConverter.ToInt32(timeLeft[1].Buffer, 12));
        Check("no state", true, !c.InAbility);

        Console.WriteLine("Berserk");
        // GenerateInRangeEnmity takes only the mobs a char has hit (m_HiPCLvl)
        rabbit.HiPcLvl = 30;
        int veBefore = rabbit.Enmity.VE(c);
        Ja(Berserk, c.TargId, CharId);
        Tick(1);
        var bers = Ours();
        Check("the BATTLE2", 1, bers.Count);
        if (bers.Count == 1)
        {
            Check("actor / ability finish / berserk / 1 target / self", $"{CharId}/6/{Berserk}/1/{CharId}", Battle2Head(bers[0]));
            var r = Battle2Results(bers[0]);
            Check("the message of berserk (115), param the effect (56)", "115/56", $"{r[0].Msg}/{r[0].Param}");
        }
        Check("berserk on", true, c.StatusEffects.HasStatusEffect(56));
        Check("the in-range enmity on the rabbit (GenerateInRangeEnmity)", true, rabbit.Enmity.VE(c) > veBefore);
        Check("berserk's recast: 300 s, saved", "True/300", $"{c.Recasts.HasRecast(RecastType.Ability, RecastBerserk, 0)}/{RecastRow(RecastBerserk)}");
        Tick(2_001);

        Console.WriteLine("Amnesia");
        c.StatusEffects.AddStatusEffect(new StatusEffect(16, 16, 0, 0, 60_000));
        Ja(Defender, c.TargId, CharId);
        Tick(1);
        Check("defender under amnesia: 88", $"{CharId}>{CharId}:88", Msgs());
        Check("no defender, no recast", "False/False", $"{c.StatusEffects.HasStatusEffect(57)}/{c.Recasts.HasRecast(RecastType.Ability, RecastDefender, 0)}");
        c.StatusEffects.DelStatusEffect(16);

        Console.WriteLine("A cast time (defender given 1 s)");
        var defender = loader.Abilities.Get(Defender);
        defender.CastTimeMs = 1000;
        Ja(Defender, c.TargId, CharId);
        var readies = Ours();
        Check("the readies BATTLE2", 1, readies.Count);
        if (readies.Count == 1)
        {
            Check("actor / ability start / no id / 1 target / self", $"{CharId}/10/0/1/{CharId}", Battle2Head(readies[0]));
            var r = Battle2Results(readies[0]);
            Check("animation 121, param defender, msg 326", $"121/{Defender}/326", $"{r[0].Anim}/{r[0].Param}/{r[0].Msg}");
        }
        Tick(500);
        Check("nothing before the cast time", "0/False", $"{Ours().Count}/{c.StatusEffects.HasStatusEffect(57)}");
        Tick(501);
        Check("defender goes off after it", "1/True", $"{Ours().Count}/{c.StatusEffects.HasStatusEffect(57)}");
        defender.CastTimeMs = 0;
        Tick(2_001);

        Console.WriteLine("The recasts reloaded (LoadFromCharRecastSQL)");
        c.Recasts = null;
        loader.AfterLevels(c);
        Check("provoke, berserk and defender back from char_recast", "True/True/True",
            $"{c.Recasts.HasRecast(RecastType.Ability, RecastProvoke, 0)}/{c.Recasts.HasRecast(RecastType.Ability, RecastBerserk, 0)}/" +
            $"{c.Recasts.HasRecast(RecastType.Ability, RecastDefender, 0)}");
        var reloaded = c.Recasts.GetRecast(RecastType.Ability, RecastProvoke);
        // used at unix second 1000, reloaded at 1012
        Check("provoke's time left: 30 - 12 s", 18_000, reloaded == null ? -1 : reloaded.TimeStampMs - now + reloaded.RecastTimeMs);
        now += 21_000;
        c.Recasts = null;
        loader.AfterLevels(c);
        Check("an expired row is not loaded", "False/True", $"{c.Recasts.Has(RecastType.Ability, RecastProvoke)}/" +
            $"{c.Recasts.HasRecast(RecastType.Ability, RecastBerserk, 0)}");
        Ja(Provoke, RabbitTargId, Rabbit);
        Tick(1);
        Check("provoke usable again", 1, Ours().Count);
        Tick(2_001);

        Console.WriteLine("The bindings from Lua");
        var lua = scripts.Lua;
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local player = GetPlayerByID({CharId})\n" + code, "=verify_ability");
            return string.Join(",", got);
        }
        Check("hasRecast(ABILITY, provoke)", "true", Lua("report(tostring(player:hasRecast(xi.recast.ABILITY, 5)))"));
        s.PacketList.Clear();
        Check("resetRecast(ABILITY, provoke)", "false", Lua("player:resetRecast(xi.recast.ABILITY, 5) report(tostring(player:hasRecast(xi.recast.ABILITY, 5)))"));
        Check("resetRecast sends 0x062 and 0x119", "1/1", $"{Of(0x062).Count}/{Of(0x119).Count}");
        Check("resetRecast: provoke's row back at 0 s (Del, then Add of 0)", 0, RecastRow(RecastProvoke));
        Check("addRecast(ABILITY, provoke, 60)", "true", Lua("player:addRecast(xi.recast.ABILITY, 5, 60) report(tostring(player:hasRecast(xi.recast.ABILITY, 5)))"));
        Check("addRecast saved", 60, RecastRow(RecastProvoke));
        Check("resetRecasts", "false,false", Lua("player:resetRecasts() report(tostring(player:hasRecast(xi.recast.ABILITY, 5))) report(tostring(player:hasRecast(xi.recast.ABILITY, 1)))"));
        Check("resetRecasts: the rows gone", "-1/-1", $"{RecastRow(RecastProvoke)}/{RecastRow(RecastBerserk)}");
        Check("GetAbility(provoke): the CAbility", "provoke,35,30,5,0,1800",
            Lua("local a = GetAbility(35) report(a:getName()) report(a:getID()) report(a:getRecast()) report(a:getRecastID()) report(a:getAOE()) report(a:getVE())"));
        Check("GetAbility(no such ability): nil", "nil", Lua("report(tostring(GetAbility(1000)))"));

        Check("no script errors", "", string.Join("\n", log.FindAll(l => !l.Contains("global scripts") && (l.Contains("lua:") || l.Contains("luautils::") || l.Contains("rror")))));
    }

    private static bool Has(CharData c, ushort id) => (c.Abilities[id / 8] & 1 << (id % 8)) != 0;
}
