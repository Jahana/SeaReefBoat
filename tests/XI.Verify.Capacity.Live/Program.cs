using System;
using System.Collections.Generic;
using System.IO;
using XI.Common;
using XI.Map;
using XI.Ipc;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Capacity.Live -- live gate for capacity points from kills (charutils::DistributeCapacityPoints,
// AddCapacityBonus, AddCapacityPoints: the lv100+ curve, the capacity chain, Commitment, the Job Breaker / level 99
// gates) and the RoE event sources around them (DEFEAT_MOB's mob and atkType, GAIN_EXPERIENCE's capacity,
// COMPLETE_QUEST, COMPLETE_MISSION). A WAR 99 and a Wild Rabbit of West Ronfaure made level 110, with the real
// MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 993420;
    private const ushort WestRonfaure = 100;
    private const uint MemberId = 993421;
    private const uint Rabbit = 17186822;

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
        foreach (uint id in new[] { CharId, MemberId })
        {
            foreach (var t in CharidTables(db))
                Sql(db, $"DELETE FROM `{t}` WHERE charid = ?", BoundValue.Of(id));
            Sql(db, "DELETE FROM chars WHERE charid = ?", BoundValue.Of(id));
            Sql(db, "DELETE FROM accounts WHERE id = ?", BoundValue.Of(id));
        }
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
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("capy" + CharId), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Cappy"), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(0.0f), BoundValue.Of(0.0f), BoundValue.Of(0.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 3000, 50, 1, 99)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 99)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
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
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);

        var info = zone.ZoneInfoOf(WestRonfaure);
        var rabbit = info.MobById[Rabbit];

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
        Check("the rabbit spawned", true, rabbit.IsSpawned);

        List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);
        // 0x02D as msg:param:value
        List<string> Msgs2() => Of(0x02D).ConvertAll(p =>
            $"{BitConverter.ToUInt16(p.Buffer, 24)}:{BitConverter.ToUInt32(p.Buffer, 16)}:{BitConverter.ToUInt32(p.Buffer, 20)}");
        string Capacity() => string.Join(",", Msgs2().FindAll(m => m.StartsWith("718:") || m.StartsWith("735:")));

        var reports = new List<string>();
        scripts.Lua.SetGlobal("capPlayer", l => { l.PushObject(c); return 1; });
        scripts.Lua.SetGlobal("report", l => { reports.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            reports.Clear();
            scripts.Lua.DoString("local player = capPlayer()\n" + code, "=verify_capacity");
            return string.Join("|", reports);
        }

        // a level 110 rabbit the char owns dies; the defeat message a second later hands out the rewards
        void Kill(byte level = 110)
        {
            // back up from the last kill (the body would despawn after 15 s)
            rabbit.DeathAtMs = rabbit.DespawnAtMs = rabbit.DefeatMsgAtMs = 0;
            rabbit.Animation = Anim.None;
            rabbit.MLvl = level;
            rabbit.OwnerId = c.Id;
            rabbit.Hp = 0;
            s.PacketList.Clear();
            now += 100;
            zone.Tick();
            now += 1000;
            zone.Tick();
        }

        Console.WriteLine("the gates: no Job Breaker, a level 99 mob, a level 98 char");
        Kill();
        Check("no Job Breaker: no capacity points", "", Capacity());
        Lua("player:addKeyItem(xi.keyItem.JOB_BREAKER)");
        Kill(99);
        Check("a level 99 mob: none", "", Capacity());
        c.MLvl = 98;
        Kill();
        Check("a level 98 char: none", "", Capacity());
        c.MLvl = 99;

        Console.WriteLine("DistributeCapacityPoints: the lv100+ curve, then the chain");
        // levelDiff 11: 0.0089 * 11^3 + 0.0533 * 11^2 + 3.7439 * 11 + 89.7 = 149.18
        now += 60_000;
        Kill();
        Check("the first kill: CAPACITY_POINTS_GAINED (718) 149", "718:149:0", Capacity());
        Check("the char's capacity points", 149, (int)c.JobPoints[1].Capacity);
        Check("JOB_POINTS (0x063) sent", true, Of(0x063).Count > 0);
        // within 30 s: chain 1, x1.05 = 156.6
        now += 10_000;
        Kill();
        Check("a kill 10 s later: CAPACITY_CHAIN (735) 156, chain 1", "735:156:1", Capacity());
        now += 10_000;
        Kill();
        // chain 2: x1.10 = 164.1
        Check("the next: chain 2, 164", "735:164:2", Capacity());
        Check("the points add up", 149 + 156 + 164, (int)c.JobPoints[1].Capacity);
        now += 31_000;
        Kill();
        Check("31 s later: the chain is over, 718 149 again", "718:149:0", Capacity());

        Console.WriteLine("AddCapacityBonus: Commitment, CAPACITY_BONUS, the RoV key items");
        now += 60_000;
        Lua("player:addStatusEffect(xi.effect.COMMITMENT, { power = 50, subPower = 30, duration = 600, origin = player })");
        Kill();
        // 50% of 149 is 74, held to the 30 left: 149 * 1.3 = 193.7, and the commitment used up
        Check("commitment: +30 (its cap), then gone", "718:193:0/False",
              $"{Capacity()}/{c.StatusEffects.HasStatusEffect(579)}");
        now += 60_000;
        Lua("player:addMod(xi.mod.CAPACITY_BONUS, 10) player:addKeyItem(xi.keyItem.RHAPSODY_IN_FUCHSIA)");
        Kill();
        // +10% and +30%: 149 * 1.4 = 208.6
        Check("CAPACITY_BONUS 10 and Rhapsody in Fuchsia: 208", "718:208:0", Capacity());
        Lua("player:delMod(xi.mod.CAPACITY_BONUS, 10) player:delKeyItem(xi.keyItem.RHAPSODY_IN_FUCHSIA)");

        Console.WriteLine("RoE: the payloads reach the records");
        string roeKill = null;
        long? roeCapacity = null;
        int roeQuest = 0, roeMission = 0;
        zone.AddRoeRecordForTest(new XI.Quests.RoeRecord(3990)
        {
            Trigger = 1, Goal = 100,
            TestCheck = (player, p) =>
            {
                roeKill = $"{p.Mob.Name}:{p.AtkType}";
                return false;
            },
        });
        zone.AddRoeRecordForTest(new XI.Quests.RoeRecord(3991)
        {
            Trigger = 7, Goal = 100,
            TestCheck = (player, p) =>
            {
                if (p.Number("capacity") is long capacity) roeCapacity = capacity;
                return false;
            },
        });
        zone.AddRoeRecordForTest(new XI.Quests.RoeRecord(3992)
        {
            Trigger = 11, Goal = 100,
            TestCheck = (player, p) =>
            {
                roeQuest++;
                return false;
            },
        });
        zone.AddRoeRecordForTest(new XI.Quests.RoeRecord(3993)
        {
            Trigger = 12, Goal = 100,
            TestCheck = (player, p) =>
            {
                roeMission++;
                return false;
            },
        });
        foreach (ushort id in new ushort[] { 3990, 3991, 3992, 3993 })
            Send(0x10C, 8, buf => BitConverter.GetBytes(id).CopyTo(buf, 4));
        Check("the four records active", "true|true|true|true",
              Lua("for _, id in ipairs({ 3990, 3991, 3992, 3993 }) do report(tostring(player:hasEminenceRecord(id))) end"));
        log.Clear();
        now += 60_000;
        Kill();
        Check("DEFEAT_MOB: the mob and the last hit's attack type", "Wild_Rabbit:0", roeKill ?? "nil");
        Check("GAIN_EXPERIENCE: the capacity points", "149", roeCapacity?.ToString() ?? "nil");
        Check("no unhandled payload", false, log.Exists(l => l.Contains("Unhandled payload type")));
        Lua("player:addQuest(xi.questLog.SANDORIA, 1) player:completeQuest(xi.questLog.SANDORIA, 1) player:completeQuest(xi.questLog.SANDORIA, 1)");
        Check("COMPLETE_QUEST once (the second completion is a no-op)", "1", roeQuest.ToString());
        Lua("player:addMission(xi.mission.log_id.SANDORIA, 0) player:completeMission(xi.mission.log_id.SANDORIA, 0)");
        Check("COMPLETE_MISSION", "1", roeMission.ToString());
        Lua("player:addCapacityPoints(50)");
        Check("addCapacityPoints from a script: not a kill, no GAIN_EXPERIENCE", "149", roeCapacity?.ToString() ?? "nil");
        Console.WriteLine("RoE kill credit: the zone decides, not the instance (mob_entity.cpp:891)");
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(MemberId), BoundValue.Of("capy" + MemberId), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(MemberId), BoundValue.Of(MemberId), BoundValue.Of("Cappa"), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(0.0f), BoundValue.Of(0.0f), BoundValue.Of(0.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(MemberId), BoundValue.Of(MemberId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        // xi.player.onGameIn runs charCreate on this first login, and charCreate needs a race
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(MemberId));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 3000, 50, 1, 99)", BoundValue.Of(MemberId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 99)", BoundValue.Of(MemberId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(MemberId));
        var s2 = new MapSession { CharId = MemberId, AccountId = MemberId };
        zone.OnLogin(s2);
        var c2 = (CharData)s2.Char;
        void Send2(ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(s2, opcode, new BasicPacket(buf));
            zone.AfterParse(s2);
        }
        Send2(0x00A, 92, buf => BitConverter.GetBytes(MemberId).CopyTo(buf, 12));
        Send2(0x00C, 12);
        Pump();
        Send(0x06E, 12, b =>
        {
            BitConverter.GetBytes(MemberId).CopyTo(b, 4);
            BitConverter.GetBytes(c2.TargId).CopyTo(b, 8);
        });
        Pump();
        Send2(0x074, 8, b => b[4] = 1);
        Pump();
        int memberKills = 0;
        zone.AddRoeRecordForTest(new XI.Quests.RoeRecord(3994)
        {
            Trigger = 1, Goal = 100,
            TestCheck = (player, p) =>
            {
                memberKills++;
                return false;
            },
        });
        Send2(0x10C, 8, buf => BitConverter.GetBytes((ushort)3994).CopyTo(buf, 4));
        now += 60_000;
        Kill();
        Check("the member beside the owner: credited", 1, memberKills);
        c2.Instance = new Instance { Data = new InstanceData { Zone = WestRonfaure }, Info = info };
        now += 60_000;
        Kill();
        Check("the member in another instance of the zone: credited, as the C++", 2, memberKills);
        c2.Instance = null;
        // caskets (spawn_casket on DEATH) need queryEntitiesByName, an open AI gap
        Check("no script errors", false, log.Exists(l => l.Contains("lua:") && !l.Contains("0 load errors") && true));
        foreach (var l in log) if (l.Contains("lua:") && true) Console.WriteLine("  log: " + l);
    }
}
