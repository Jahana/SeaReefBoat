using System;
using System.Collections.Generic;
using System.IO;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Roe.Live -- live gate for Records of Eminence (ZoneHost.Roe: c2s 0x10C ROE_START, 0x10D ROE_REMOVE,
// 0x10E ROE_CLAIM; roeutils' parse, onCharLoad resets, the JST hourly cycles, onRecordTrigger; the RoE / currency /
// exp bindings) against the real MariaDB (xidb) with the real xi.roe global (C#) and the C# records.
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 990701;
    private const string CharName = "Roea";
    private static readonly string[] UnityColumns = { "members_current", "points_current", "members_prev", "points_prev" };

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static ResultSet Sql(IDatabase db, string q, params BoundValue[] a) => db.Execute(q, a);

    private static string Q1(IDatabase db, string q)
    {
        var r = Sql(db, q, BoundValue.Of(CharId));
        return r != null && r.Next() ? r.Get<string>(0u) : "-";
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

        // unity_system is shared: put it back as it was
        var unity = new List<string[]>();
        var ur = Sql(db, "SELECT leader, members_current, points_current, members_prev, points_prev FROM unity_system");
        while (ur != null && ur.Next())
            unity.Add(new[] { ur.Get<string>("leader"), ur.Get<string>("members_current"), ur.Get<string>("points_current"), ur.Get<string>("members_prev"), ur.Get<string>("points_prev") });

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
            foreach (var u in unity)
                Sql(db, "UPDATE unity_system SET members_current = ?, points_current = ?, members_prev = ?, points_prev = ? WHERE leader = ?",
                    BoundValue.Of(u[1]), BoundValue.Of(u[2]), BoundValue.Of(u[3]), BoundValue.Of(u[4]), BoundValue.Of(u[0]));
        }

        Console.WriteLine($"{_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    private static int Opcode(BasicPacket p) => p.Buffer[0] | (p.Buffer[1] & 1) << 8;

    private static ushort Active(byte[] log, int slot) => BitConverter.ToUInt16(log, 2 * slot);
    private static uint Progress(byte[] log, int slot) => BitConverter.ToUInt32(log, 64 + 4 * slot);
    private static bool Complete(byte[] log, int id) => (log[188 + id / 8] & 1 << id % 8) != 0;

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
        uint next29 = stats.Growth.GetExpNextLevel(29);

        // Wednesday 2026-10-14 10:00:30 JST: timed block 3 (08-12), record 4019
        long now = new DateTimeOffset(2026, 10, 14, 1, 0, 30, TimeSpan.Zero).ToUnixTimeSeconds();

        // the log: 4082 (daily) active with progress, First Step Forward and 4082 complete; offline for twenty days
        var blob = new byte[700];
        BitConverter.GetBytes((ushort)4082).CopyTo(blob, 0);
        BitConverter.GetBytes(7u).CopyTo(blob, 64);
        blob[188 + 1 / 8] |= 1 << 1;
        blob[188 + 4082 / 8] |= 1 << 4082 % 8;
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("roe" + CharName), BoundValue.Of("x"), BoundValue.Of((byte)1));
        // a Hume past its first login (the NEW_ADVENTURER title: no charCreate) that has seen the RoV opening
        // (Rhapsodies of Vana'diel, a zone-in cutscene in the starting cities: Resonance current, the other logs as a
        // new char has them), standing at Port Windurst's mog house exit (a char at 0,0,0 is moved there by the zone's
        // onZoneIn)
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        var missions = new byte[15 * 70];
        for (int i = 0; i <= 3; i++) BitConverter.GetBytes((ushort)0xFFFF).CopyTo(missions, i * 70);
        BitConverter.GetBytes((ushort)101).CopyTo(missions, 6 * 70);
        BitConverter.GetBytes((ushort)1).CopyTo(missions, 13 * 70);
        missions[13 * 70 + 6] = 1;
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,eminence,lastupdate,titles,missions) " +
                "VALUES (?,?,?,?,?,?,?,?,?,?,?,FROM_UNIXTIME(?),?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(CharName), BoundValue.Of((ushort)240), BoundValue.Of((ushort)0),
            BoundValue.Of(198.0f), BoundValue.Of(-15.65f), BoundValue.Of(258.0f),
            BoundValue.Of((byte)2), BoundValue.Of((byte)0), BoundValue.Of(blob), BoundValue.Of((uint)(now - 20 * 86400)),
            BoundValue.Of(titles), BoundValue.Of(missions));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(CharId));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl, sjob, slvl) VALUES (?, 300, 50, 1, 29, 0, 1)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 29)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_exp(charid, war) VALUES (?, ?)", BoundValue.Of(CharId), BoundValue.Of((ushort)(next29 - 100)));
        Sql(db, "REPLACE INTO char_profile(charid, unity_leader) VALUES (?, 1)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_points(charid, spark_of_eminence, current_accolades, prev_accolades) VALUES (?, 50, 6000, 0)", BoundValue.Of(CharId));

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
        zone.UnixNow = () => now;
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);

        Console.WriteLine("parse (the C# records):");
        Check("no unknown trigger / flag", false, log.Exists(l => l.Contains("ROEUtils: Unknown")));

        Console.WriteLine("onCharLoad:");
        var s = new MapSession { CharId = CharId, AccountId = CharId };
        zone.OnLogin(s);
        var c = (CharData)s.Char;
        Check("loaded, offline twenty days", now - 20 * 86400, c.LastOnline);
        Check("daily: progress cleared, the record kept", "4082/0", $"{Active(c.EminenceLog, 0)}/{Progress(c.EminenceLog, 0)}");
        Check("daily: 4082's completion cleared, 1 kept", "False/True", $"{Complete(c.EminenceLog, 4082)}/{Complete(c.EminenceLog, 1)}");
        Check("timed: Wednesday 08-12 is 4019", 4019, Active(c.EminenceLog, 30));
        Check("the log saved", Convert.ToHexString(c.EminenceLog), Q1(db, "SELECT HEX(eminence) FROM chars WHERE charid = ?"));
        Check("weekly: accolades rolled, then the stale previous week zeroed", "0/0",
              Q1(db, "SELECT CONCAT(current_accolades, '/', prev_accolades) FROM char_points WHERE charid = ?"));
        Check("weekly: 6000 accolades grant Pieuje's trust (953)", "953", Q1(db, "SELECT spellid FROM char_spells WHERE charid = ?"));
        Check("the trust known", true, (c.SpellList[953 / 8] & 1 << 953 % 8) != 0);

        void Send(ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(s, opcode, new BasicPacket(buf));
            zone.AfterParse(s);
        }
        List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);
        // 0x029 messages as msg:param:value
        List<string> Msgs() => Of(0x029).ConvertAll(p => $"{BitConverter.ToUInt16(p.Buffer, 24)}:{BitConverter.ToUInt32(p.Buffer, 12)}:{BitConverter.ToUInt32(p.Buffer, 16)}");
        // 0x02D messages as msg:param
        List<string> Msgs2() => Of(0x02D).ConvertAll(p => $"{BitConverter.ToUInt16(p.Buffer, 24)}:{BitConverter.ToUInt32(p.Buffer, 16)}");
        void Clear() { s.PacketList.Clear(); log.Clear(); }
        void Roe(ushort opcode, int id) => Send(opcode, 8, buf => BitConverter.GetBytes((ushort)id).CopyTo(buf, 4));

        var reports = new List<string>();
        scripts.Lua.SetGlobal("roePlayer", l => { l.PushObject(c); return 1; });
        scripts.Lua.SetGlobal("report", l => { reports.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            reports.Clear();
            scripts.Lua.DoString("local player = roePlayer()\n" + code, "=verify_roe");
            return string.Join("|", reports);
        }
        string M(string name) => Lua($"report(xi.msg.basic.{name})");

        Send(0x00A, 92, buf => BitConverter.GetBytes(CharId).CopyTo(buf, 12));
        Clear();
        Send(0x00C, 12);
        var ok = new List<BasicPacket>(s.PacketList);
        int active = ok.FindIndex(p => Opcode(p) == 0x111), timed = ok.FindIndex(p => Opcode(p) == 0x029 && BitConverter.ToUInt16(p.Buffer, 24) == 705),
            roeLog = ok.FindIndex(p => Opcode(p) == 0x112);
        Check("game OK: ROE_TIMED (705) for 4019 between the active log and the log", "4019/True",
              timed < 0 ? "none" : $"{BitConverter.ToUInt32(ok[timed].Buffer, 12)}/{active < timed && timed < roeLog}");
        Clear();
        Send(0x00C, 12);
        Check("game OK again: no timed message", false, Msgs().Exists(m => m.StartsWith("705:")));

        Console.WriteLine("0x10C ROE_START:");
        Clear();
        Roe(0x10C, 1200);
        Check("1200 taken into slot 1", 1200, Active(c.EminenceLog, 1));
        Check("ROE_START (704)", true, Msgs().Contains("704:1200:0"));
        Check("ACTIVE_LOG and UNITY sent", "1/1", $"{Of(0x111).Count}/{Of(0x110).Count}");
        Check("retro: the level check fails at 29, nothing completes", false, Complete(c.EminenceLog, 1200));
        Check("saved", Convert.ToHexString(c.EminenceLog), Q1(db, "SELECT HEX(eminence) FROM chars WHERE charid = ?"));
        Clear();
        Roe(0x10C, 1200);
        Check("taken twice: ROE_UNABLE (742)", "742:1200:0", string.Join(",", Msgs()));
        Clear();
        Roe(0x10C, 4096);
        Check("4096: refused", true, log.Exists(l => l.Contains("ObjectiveId out of range")));
        Clear();
        Roe(0x10C, 4022);
        Check("not implemented: told, and ROE_UNABLE", "True/742:4022:0",
              $"{Of(0x017).Exists(p => System.Text.Encoding.ASCII.GetString(p.Buffer).Contains("The record #4022 is not implemented at this time."))}/{string.Join(",", Msgs())}");
        Clear();
        Roe(0x10C, 1);
        Check("a completed record that is not repeatable: refused", true, log.Exists(l => l.Contains("Cannot start a completed record")));

        Console.WriteLine("bindings:");
        Roe(0x10C, 2);
        Check("2 taken", 2, Active(c.EminenceLog, 2));
        Check("hasEminenceRecord / getEminenceProgress / nil off the log", "true|0|nil",
              Lua("report(tostring(player:hasEminenceRecord(2))) report(tostring(player:getEminenceProgress(2))) report(tostring(player:getEminenceProgress(3)))"));
        Clear();
        Check("setEminenceProgress returns true", "true", Lua("report(tostring(player:setEminenceProgress(2, 1, 1)))"));
        Check("ROE_RECORD (697) and ROE_PROGRESS (698)", "697:2:0,698:1:1", string.Join(",", Msgs()));
        Check("setEminenceProgress off the log: false", "false", Lua("report(tostring(player:setEminenceProgress(3, 1, 1)))"));
        Check("getUnityLeader / getUnityRank / getJobLevel / getMainJob", "1|true|29|1",
              Lua("report(player:getUnityLeader()) local r = player:getUnityRank() report(tostring(r >= 1 and r <= 11)) report(player:getJobLevel(xi.job.WAR)) report(player:getMainJob())"));
        Check("getCurrency", "50", Lua("report(player:getCurrency('spark_of_eminence'))"));
        log.Clear();
        Check("getCurrency charid: 0, logged", "0/True", Lua("report(player:getCurrency('charid'))") + "/" + log.Exists(l => l.Contains("charutils::GetPoints: Invalid type charid for Roea")));

        Console.WriteLine("0x10E ROE_CLAIM -> completeRecord -> addExp level up -> ACHIEVE_LEVEL:");
        Clear();
        Roe(0x10E, 2);
        Check("ROE_COMPLETE for 2", true, Msgs().Contains($"{M("ROE_COMPLETE")}:2:0"));
        Check("first time: triple sparks, for 2 and for 1200", "650", Q1(db, "SELECT spark_of_eminence FROM char_points WHERE charid = ?"));
        Check("EXPERIENCE_GAINED 500 and LEVEL_UP 30 (0x02D)", true, Msgs2().Contains("8:500") && Msgs2().Contains("9:30"));
        Check("level 30: char_jobs / char_stats / char_exp", $"30/30/{500 - 100 + 500}",
              Q1(db, "SELECT CONCAT(j.war, '/', s.mlvl, '/', e.war) FROM char_jobs j JOIN char_stats s USING (charid) JOIN char_exp e USING (charid) WHERE charid = ?"));
        Check("the char at 30, HP full", $"30/30/True", $"{c.MLvl}/{c.JobLevels[1]}/{c.Hp == c.MaxHp}");
        Check("level-up packets: JOB_INFO, CLISTATUS", true, Of(0x01B).Count > 0 && Of(0x061).Count > 0);
        Check("2 completed and off the log", "True/False", $"{Complete(c.EminenceLog, 2)}/{Lua("report(tostring(player:hasEminenceRecord(2)))") == "true"}");
        Check("ACHIEVE_LEVEL ran 1200's check: ROE_COMPLETE for 1200", true, Msgs().Contains($"{M("ROE_COMPLETE")}:1200:0"));
        Check("1200's item reward", "12", Lua("report(xi.item.COPY_OF_DEATH_FOR_DIMWITS)") is var dimwits &&
              c.Items.Find(i => i.ItemId.ToString() == dimwits) is Item given ? given.Quantity.ToString() : "none");
        Check("CURRENCIES_1 after the claim", 1, Of(0x113).Count);
        int completedShown = 0;
        for (int i = 0; i < 4096; i++)
            if (Complete(c.EminenceLog, i) && (zone.RoeRecordForTest(i)?.Flags & XI.Quests.RoeFlags.Hidden) is null or XI.Quests.RoeFlags.None)
                completedShown++;
        Check("getNumEminenceCompleted", completedShown.ToString(), Lua("report(player:getNumEminenceCompleted())"));

        Console.WriteLine("0x10D ROE_REMOVE:");
        Roe(0x10C, 3);
        Roe(0x10C, 4);
        Lua("player:setEminenceProgress(4, 5)");
        string Slots() { var l = new List<string>(); for (int i = 0; i < 30 && Active(c.EminenceLog, i) != 0; i++) l.Add($"{Active(c.EminenceLog, i)}:{Progress(c.EminenceLog, i)}"); return string.Join(",", l); }
        string before = Slots();
        Clear();
        Roe(0x10D, 3);
        Check("3 removed, 4 moved up with its progress", before.Replace("3:0,", ""), Slots());
        Check("ACTIVE_LOG and UNITY sent", "1/1", $"{Of(0x111).Count}/{Of(0x110).Count}");
        Check("the activemap", "false", Lua("report(tostring(player:hasEminenceRecord(3)))"));

        Console.WriteLine("triggerRoeEvent / onRecordTrigger:");
        string roeSeen = null;
        zone.AddRoeRecordForTest(new XI.Quests.RoeRecord(3999)
        {
            Trigger = 18, // TRIGGER_NPC
            TestCheck = (player, p) =>
            {
                roeSeen = $"{p.Text("tag")}:{p.Number("count")}:{p.Progress}";
                return false;
            },
        });
        Roe(0x10C, 3999);
        Lua("player:triggerRoeEvent(xi.roeTrigger.TRIGGER_NPC, { tag = 'x', count = 3 })");
        Check("the record's check sees the datagrams and its progress", "x:3:0", roeSeen ?? "nil");
        roeSeen = null;
        Lua("player:triggerRoeEvent(xi.roeTrigger.HEAL_ALLY, { tag = 'y', count = 1 })");
        Check("another trigger: not called", "nil", roeSeen ?? "nil");

        Console.WriteLine("currencies:");
        Clear();
        double points0 = double.Parse(Q1(db, "SELECT points_current FROM unity_system WHERE leader = 1 AND ? > 0"));
        Lua("player:addCurrency('unity_accolades', 1500)");
        Check("unity accolades: the total, the week", "1500/1500", Q1(db, "SELECT CONCAT(unity_accolades, '/', current_accolades) FROM char_points WHERE charid = ?"));
        Check("unity_system points for leader 1", "1.5",
              (double.Parse(Q1(db, "SELECT points_current FROM unity_system WHERE leader = 1 AND ? > 0")) - points0).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Check("MAGIC_DATA and CLISTATUS sent", "1/1", $"{Of(0x0AA).Count}/{Of(0x061).Count}");
        Lua("player:addCurrency('spark_of_eminence', -1000)");
        Check("clamped at 0", "0", Q1(db, "SELECT spark_of_eminence FROM char_points WHERE charid = ?"));
        Lua("player:addCurrency('spark_of_eminence', 500, 300)");
        Check("clamped at the max", "300", Q1(db, "SELECT spark_of_eminence FROM char_points WHERE charid = ?"));

        Console.WriteLine("0x116 UNITY_MENU / 0x117 UNITY_QUEST:");
        void Unity(ushort opcode, uint kind) => Send(opcode, 8, buf => BitConverter.GetBytes(kind).CopyTo(buf, 4));
        Clear();
        Unity(0x116, 0);
        var unity = new List<BasicPacket>(s.PacketList);
        Check("UNITY, then 64 MISCDATA unity pages", "110/64/64", $"{Opcode(unity[0]):X3}/{Of(0x063).Count}/{unity.Count - 1}");
        Check("UNITY's sparks: the clamped 300", 300u, BitConverter.ToUInt32(unity[0].Buffer, 4) & 0xFFFFFF);
        // page 2 is the previous week's points, page 34 the current week's (type 7, result set, data type 2)
        Check("the pages: previous then current week's points", "7:0:2/7:1:2", $"{BitConverter.ToUInt16(unity[3].Buffer, 4)}:{unity[3].Buffer[8]}:{unity[3].Buffer[9]}/" +
              $"{BitConverter.ToUInt16(unity[35].Buffer, 4)}:{unity[35].Buffer[8]}:{unity[35].Buffer[9]}");
        Check("leader 1's current points", (uint)double.Parse(Q1(db, "SELECT points_current FROM unity_system WHERE leader = 1 AND ? > 0")),
              BitConverter.ToUInt32(unity[35].Buffer, 20));
        Clear();
        Unity(0x116, 1);
        Check("Kind 1: the same", 65, s.PacketList.Count);
        Clear();
        Unity(0x116, 2);
        Check("Kind 2 refused", "True/0", $"{log.Exists(l => l.Contains("Kind out of range: 2 not in [0, 1]"))}/{s.PacketList.Count}");
        Clear();
        Unity(0x117, 0);
        Check("0x117: the same", "110/65", $"{Opcode(s.PacketList.First.Value):X3}/{s.PacketList.Count}");
        Clear();
        Unity(0x117, 1);
        Check("0x117 Kind 1 refused", "True/0", $"{log.Exists(l => l.Contains("Kind not 0x0"))}/{s.PacketList.Count}");

        Console.WriteLine("addExp limit mode / addCapacityPoints:");
        c.JobLevels[1] = 75; c.MLvl = 75; c.MeritMode = true; c.LimitPoints = 9900; c.MeritPoints = 0;
        Clear();
        Lua("player:addExp(200)");
        Check("LIMIT_POINTS_GAINED (371) and MERIT_POINT_GAINED (50)", true, Msgs2().Contains("371:200") && Msgs2().Contains("50:1"));
        Check("char_exp merits / limits", "1/100", Q1(db, "SELECT CONCAT(merits, '/', limits) FROM char_exp WHERE charid = ?"));
        Check("MERITS sent", true, Of(0x063).Count > 0);
        Clear();
        Lua("player:addCapacityPoints(100)");
        Check("CAPACITY_POINTS_GAINED (718)", true, Msgs2().Contains("718:100"));
        Check("char_job_points capacity", "100/0", Q1(db, "SELECT CONCAT(capacity_points, '/', job_points) FROM char_job_points WHERE charid = ? AND jobid = 1"));
        Clear();
        Lua("player:addCapacityPoints(30000)");
        Check("a job point: JOB_POINT_GAINED (719)", true, Msgs2().Contains("719:1"));
        Check("char_job_points: the remainder and the point", "100/1", Q1(db, "SELECT CONCAT(capacity_points, '/', job_points) FROM char_job_points WHERE charid = ? AND jobid = 1"));

        Console.WriteLine("hourly cycles:");
        zone.Tick(); // the first tick only schedules the next JST hour
        Check("10:00 JST: no cycle yet", 4019, Active(c.EminenceLog, 30));
        Clear();
        now = new DateTimeOffset(2026, 10, 14, 3, 0, 5, TimeSpan.Zero).ToUnixTimeSeconds(); // 12:00 JST
        zone.Tick();
        Check("12:00 JST: the timed record is 4008", 4008, Active(c.EminenceLog, 30));
        Check("ROE_TIMED (705) for it", true, Msgs().Contains("705:4008:0"));
        Check("members counted", Q1(db, "SELECT COUNT(*) FROM char_profile WHERE unity_leader = 1 AND ? > 0"),
              Q1(db, "SELECT members_current FROM unity_system WHERE leader = 1 AND ? > 0"));
        string pointsBefore = Q1(db, "SELECT points_current FROM unity_system WHERE leader = 1 AND ? > 0");
        Lua("player:setEminenceProgress(4082, 9)");
        now = new DateTimeOffset(2026, 10, 18, 15, 0, 5, TimeSpan.Zero).ToUnixTimeSeconds(); // Monday 00:00 JST
        zone.Tick();
        Check("Monday: the week rolled", $"{pointsBefore}/0", Q1(db, "SELECT CONCAT(points_prev, '/', points_current) FROM unity_system WHERE leader = 1 AND ? > 0"));
        Check("Monday: accolades rolled", "1500/0", Q1(db, "SELECT CONCAT(prev_accolades, '/', current_accolades) FROM char_points WHERE charid = ?"));
        Check("Monday: under 5000 both weeks, the trust removed", "-", Q1(db, "SELECT spellid FROM char_spells WHERE charid = ?"));
        Check("midnight: the daily progress cleared", 0u, Progress(c.EminenceLog, 0));
        Check("Monday 00-04: the timed record is 4015", 4015, Active(c.EminenceLog, 30));

        Console.WriteLine("accolades: the unity rank's bonus");
        // leader 1 ranks 3rd: 1.1 * 300 is 330
        Sql(db, "UPDATE unity_system SET members_prev = 0, points_prev = 0");
        foreach (var (leader, prev) in new[] { (2, 90), (3, 80), (1, 70) })
            Sql(db, "UPDATE unity_system SET members_prev = 1, points_prev = ? WHERE leader = ?",
                BoundValue.Of(prev), BoundValue.Of(leader));
        zone.AddRoeRecordForTest(new XI.Quests.RoeRecord(3998)
        {
            Reward = new XI.Quests.RoeReward { Accolades = 300 },
        });
        Roe(0x10C, 3998);
        Lua("player:setEminenceProgress(3998, 1)");
        Clear();
        Roe(0x10E, 3998);
        Check("rank 3: 5% a rank", true, Msgs().Exists(m => m.StartsWith($"{M("ROE_RECEIVED_ACCOLADES")}:330:")));
    }
}
