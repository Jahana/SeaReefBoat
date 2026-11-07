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

// XI.Verify.Trusts.Live -- live gate for trusts (ZoneHost.Trusts.cs, TrustEntity.cs): spawnTrust (Kupipi, then Naji)
// under a dynamic targid in the zone's trust list, its stats from mob_pools (spell id + 5000) and the master's level
// (the halved sub level, the scaled weapon damage, the skills, full HP and MP), the script's onMobSpawn (its spawn
// message to the party as 0x02D 711, its gambits, TRUST_DISTANCE), the party formed by trusts (accounts_parties, the
// trusts in 0x0C8 and 0x0DD, getPartyWithTrusts, 0x0DF on an HP change), the client view (0x00E with the master bit
// and 0x45 at 0x28, 0x067 to the master), xi.trust.canCast refusing the same trust again, following in formation (the
// first trust behind the master, the second behind the first, a warp from afar), the rest between fights, the
// release from the trust's menu (0x01A: the despawn message, gone 2 s later and faded 3 s after that), clearTrusts
// (the party formed by trusts disbanding), the fight (Kupipi and Naji engaging after the master's swing, Naji closing
// in and hitting, its TP skill list and weapon skill at 1000 TP, its Provoke gambit, Kupipi's gambits casting
// Protectra / Shellra on the party with its trusts and curing the hurt master,
// the rabbit turning on Naji, both disengaging after the kill), status effects on a trust (a POISON ticking it down,
// REGAIN while idle, CHARM_I refused, the master's Benediction reaching it as an AoE target with the POISON's wear-off
// to its origin, its death wearing its death-flagged effects silently and running the DEATH listeners), the master's
// death and zone-out sending them away. One
// WHM 75 in West Ronfaure, with the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 993730;
    private const ushort WestRonfaure = 100;
    private const uint Kupipi = 898, Naji = 897;
    private const uint Rabbit = 17186822;

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static ResultSet Sql(IDatabase db, string q, params BoundValue[] a) => db.Execute(q, a);

    private static ulong U(BasicPacket p, int bit, int len) => Battle2Packets.Unpack(p.Buffer, bit, len);

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
            BoundValue.Of(CharId), BoundValue.Of("trust" + CharId), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Caller"), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(-200.0f), BoundValue.Of(0.0f), BoundValue.Of(-200.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 1500, 500, 3, 75)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, whm) VALUES (?, 75)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));
        // the Kupipi cipher's spell
        Sql(db, "INSERT INTO char_spells(charid, spellid) VALUES (?, ?)", BoundValue.Of(CharId), BoundValue.Of((ushort)Kupipi));

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, AbilityTable.Load(db, _ => true, 1.0), stats), dataDir, true, log.Add);
        // this suite's scenes are set at places off the zone's navmesh, so its paths are straight lines
        // (XI.Verify.NavMesh.Live walks the mesh)
        zone.NavMeshDir = null;
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
            lua.DoString($"local player = GetPlayerByID({CharId})\n" + code, "=verify_trusts");
            return string.Join(",", got);
        }

        var info = zone.ZoneInfoOf(WestRonfaure);
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
        string Solo() => Lua("report(player:checkSoloPartyAlliance())");
        Check("the WHM 75 in West Ronfaure, solo", "3/75/100/0", $"{c.MJob}/{c.MLvl}/{c.Zone}/{Solo()}");

        List<BasicPacket> Sent(ushort opcode) => s.PacketList.Where(p => p.GetOpcode() == opcode).ToList();
        List<string> LuaErrors() => log.Where(l => l.Contains("lua") && (l.Contains("rror") || l.Contains("not ported"))).ToList();
        string Name(BasicPacket p, int offset, int len) => System.Text.Encoding.ASCII.GetString(p.Buffer, offset, len);

        Console.WriteLine("spawnTrust: Kupipi behind the WHM");
        c.Rotation = 0; // facing east: behind is west
        s.PacketList.Clear();
        log.Clear();
        Lua($"player:spawnTrust({Kupipi})");
        var kupipi = c.PTrusts.FirstOrDefault();
        Check("a TrustEntity in the master's list and the zone's", "True/True/True/True",
            $"{kupipi != null}/{kupipi?.PMaster == c}/{info.Trusts.Contains(kupipi)}/{c.PTrusts.Count == 1}");
        Check("a dynamic targid from 0x700 and its id", $"True/{0x01000000u | (uint)WestRonfaure << 12 | (uint)(kupipi.TargId + 0x100)}",
            $"{kupipi.TargId >= 0x700 && kupipi.TargId <= 0x8FF}/{kupipi.Id}");
        string trustLua = $"local t = player:getPartyWithTrusts()[2]\n";
        Check("getTrustID, isTrust, isMob, getObjType, getPool, getMaster", $"{Kupipi}/true/false/32/5898/Caller",
            Lua(trustLua + "report(t:getTrustID() .. '/' .. tostring(t:isTrust()) .. '/' .. tostring(t:isMob()) .. '/' .. t:getObjType() .. " +
                "'/' .. t:getPool() .. '/' .. t:getMaster():getName())"));
        float dx = kupipi.X - c.X, dz = kupipi.Z - c.Z;
        Check("3 yalms behind the master (west), facing its way", "3.0/True/0", $"{MathF.Sqrt(dx * dx + dz * dz):0.0}/{dx < -2.9f}/{kupipi.Rotation}");
        Check("the pool's name and look, a WHM 75 with half the level as its sub", "kupipi/Kupipi/3/75/0/37",
            $"{kupipi.Name}/{kupipi.PacketName}/{kupipi.MJob}/{kupipi.MLvl}/{kupipi.SJob}/{kupipi.SLvl}");
        // 75 halved, then cmbDmgMult 40%; cmbDelay 230 and cmbSkill 11 (club)
        Check("the scaled weapon: damage 15, delay 230, club", "15/230/11", $"{kupipi.MainDamage}/{kupipi.MainDelay}/{kupipi.WeaponSkill}");
        Check("full HP and MP, the stats, healing magic and MEVA set", "True/True/True/True/True",
            $"{kupipi.Hp == kupipi.MaxHp && kupipi.MaxHp > 800}/{kupipi.Mp == kupipi.MaxMp && kupipi.MaxMp > 200}/{kupipi.Stats.All(v => v > 20)}/" +
            $"{kupipi.WorkingSkills[33] > 200}/{kupipi.GetMod(Mod.MEVA) > 100}");
        Check("its spell list (310) and traits", "True/True", $"{kupipi.SpellList != null}/{kupipi.TraitList.Count > 0}");
        Check("onMobSpawn ran: its gambits, NO_MOVE", "True/-1", $"{kupipi.Gambits.Count >= 10}/{kupipi.GetMobMod(MobMod.TrustDistance)}");
        var msg = Sent(0x02D).FirstOrDefault();
        Check("its spawn message to the party: 0x02D 711, page 2 offset SPAWN", $"{kupipi.Id}/{c.Id}/201/711",
            msg == null ? "none" : $"{BitConverter.ToUInt32(msg.Buffer, 4)}/{BitConverter.ToUInt32(msg.Buffer, 8)}/" +
                                   $"{BitConverter.ToUInt32(msg.Buffer, 16)}/{BitConverter.ToUInt16(msg.Buffer, 24)}");
        Check("the char sees it", true, c.SpawnTrustList.Contains(kupipi.Id));
        var spawn = Sent(0x00E).FirstOrDefault(p => BitConverter.ToUInt32(p.Buffer, 4) == kupipi.Id);
        Check("0x00E: the master bit at 0x27, 0x45 at 0x28, its packet name", "8/69/Kupipi",
            spawn == null ? "none" : $"{spawn.Buffer[0x27] & 0x08}/{spawn.Buffer[0x28] & 0x45}/" + Name(spawn, 0x34, 6));
        Check("the special spawn animation only the once", 0, kupipi.SpawnAnimation);
        var setName = Sent(0x067).FirstOrDefault();
        Check("0x067 to the master: the trust's targid and id, the master's targid, its name", $"{kupipi.TargId}/{kupipi.Id}/{c.TargId}/Kupipi",
            setName == null ? "none" : $"{BitConverter.ToUInt16(setName.Buffer, 6)}/{BitConverter.ToUInt32(setName.Buffer, 8)}/" +
                                       $"{BitConverter.ToUInt16(setName.Buffer, 12)}/" + Name(setName, 0x18, 6));
        Check("no script errors", "", string.Join(" | ", LuaErrors()));

        Console.WriteLine("The party formed by trusts");
        Check("a party, the char its leader", "1/Caller", $"{Solo()}/" + Lua("report(player:getPartyLeader():getName())"));
        var row = Sql(db, "SELECT COUNT(*) AS n FROM accounts_parties WHERE charid = ? AND partyid = ?", BoundValue.Of(CharId), BoundValue.Of(CharId));
        Check("the char's accounts_parties row", 1, row != null && row.Next() ? row.Get<int>("n") : -1);
        var tbl = Sent(0x0C8).LastOrDefault();
        Check("0x0C8: the char, then Kupipi with its targid", $"{c.Id}/{kupipi.Id}/{kupipi.TargId}",
            tbl == null ? "none" : $"{BitConverter.ToUInt32(tbl.Buffer, 8)}/{BitConverter.ToUInt32(tbl.Buffer, 20)}/{BitConverter.ToUInt16(tbl.Buffer, 24)}");
        var list = Sent(0x0DD).LastOrDefault(p => BitConverter.ToUInt32(p.Buffer, 4) == kupipi.Id);
        Check("0x0DD for Kupipi: member 1, its HP, jobs and name", $"1/{kupipi.Hp}/3/75/37/Kupipi",
            list == null ? "none" : $"{list.Buffer[26]}/{BitConverter.ToInt32(list.Buffer, 8)}/{list.Buffer[34]}/{list.Buffer[35]}/" +
                                    $"{list.Buffer[37]}/" + Name(list, 40, 6));
        Check("getPartyWithTrusts: the char, then Kupipi", "Caller/kupipi",
            Lua("local p = player:getPartyWithTrusts() report(p[1]:getName() .. '/' .. p[2]:getName())"));
        Check("xi.trust.canCast refuses Kupipi again (TRUST_ALREADY_CALLED)", "-1",
            Lua($"report(xi.trust.canCast(player, GetSpell({Kupipi})))"));
        Check("but allows Naji", "0", Lua($"report(xi.trust.canCast(player, GetSpell({Naji})))"));
        s.PacketList.Clear();
        kupipi.Hp -= 100;
        kupipi.UpdateMask |= EntityUpdatePacket.UpdateHp;
        Tick();
        var attr = Sent(0x0DF).LastOrDefault(p => BitConverter.ToUInt32(p.Buffer, 4) == kupipi.Id);
        Check("an HP change goes to the party as 0x0DF", $"{kupipi.Hp}/{kupipi.Hpp}",
            attr == null ? "none" : $"{BitConverter.ToInt32(attr.Buffer, 8)}/{attr.Buffer[22]}");

        Console.WriteLine("A second trust: Naji further back, following Kupipi");
        Lua($"player:spawnTrust({Naji})");
        var naji = c.PTrusts.Count > 1 ? c.PTrusts[1] : null;
        dx = naji.X - c.X;
        dz = naji.Z - c.Z;
        Check("Naji 6 yalms behind, a WAR", "6.0/1", $"{MathF.Sqrt(dx * dx + dz * dz):0.0}/{naji.MJob}");
        Check("both in the party list", "2/True", $"{c.PTrusts.Count}/{Sent(0x0DD).Any(p => BitConverter.ToUInt32(p.Buffer, 4) == naji.Id)}");
        c.X += 12;
        Tick(20);
        float Dist(BattleEntity a, BattleEntity b)
        {
            float x = a.X - b.X, y = a.Y - b.Y, z = a.Z - b.Z;
            return MathF.Sqrt(x * x + y * y + z * z);
        }
        Check("Kupipi caught up to the master, Naji to Kupipi", "True/True", $"{Dist(kupipi, c) <= 2.0f}/{Dist(naji, kupipi) <= 3.5f}");
        c.X += 60;
        Tick(2);
        Check("a warp to the master from beyond 30 yalms", true, Dist(kupipi, c) <= 2.1f);
        Tick(10);

        Console.WriteLine("The rest between fights");
        kupipi.Hp = kupipi.MaxHp / 2;
        int restHp = kupipi.Hp;
        // the rest's delays start over (15 s first); the pool's REGEN 1 ticks on top
        kupipi.NumHealingTicks = 0;
        kupipi.LastHealTickMs = now;
        Tick(10);
        Check("nothing but the REGEN within the first 15 s", true, kupipi.Hp - restHp <= 2);
        Tick(30);
        int restGain = kupipi.Hp - restHp, fivePercent = (int)(kupipi.MaxHp * 0.05);
        Check("5% back once after 15 s, the delay down to 10 s", "True/1",
            $"{restGain >= fivePercent && restGain <= fivePercent + 6}/{kupipi.NumHealingTicks}");

        Console.WriteLine("Release from the trust's menu");
        s.PacketList.Clear();
        log.Clear();
        Send(0x01A, 28, b =>
        {
            BitConverter.GetBytes(kupipi.Id).CopyTo(b, 4);
            BitConverter.GetBytes(kupipi.TargId).CopyTo(b, C2sAction.OffActIndex);
            BitConverter.GetBytes((ushort)ActionId.Talk).CopyTo(b, C2sAction.OffActionID);
        });
        msg = Sent(0x02D).LastOrDefault();
        Check("released: the despawn message (page 2 offset DESPAWN) at once, still listed", "True/211/True",
            $"{kupipi.Released}/{(msg == null ? -1 : BitConverter.ToUInt32(msg.Buffer, 16))}/{c.PTrusts.Contains(kupipi)}");
        Tick(6);
        Check("2 s later: out of the master's list, no master, despawning", "False/True/True",
            $"{c.PTrusts.Contains(kupipi)}/{kupipi.PMaster == null}/{kupipi.FadeAtMs != 0}");
        Tick(9);
        Check("faded after the despawn's 3 s: out of the zone, the char told", "False/False/True",
            $"{info.Trusts.Contains(kupipi)}/{c.SpawnTrustList.Contains(kupipi.Id)}/" +
            Sent(0x00E).Any(p => BitConverter.ToUInt32(p.Buffer, 4) == kupipi.Id && p.Buffer[0x0A] == 0x30));
        Check("Naji first in line now; the party still up", "True/1", $"{c.PTrusts.FirstOrDefault() == naji}/{Solo()}");
        Check("one despawn message (the second onMobDespawn has no master)", 1,
            s.PacketList.Count(p => p.GetOpcode() == 0x02D && BitConverter.ToUInt32(p.Buffer, 16) == 211));

        Console.WriteLine("clearTrusts: the party formed by trusts disbands");
        Lua("player:clearTrusts()");
        Tick(2);
        row = Sql(db, "SELECT COUNT(*) AS n FROM accounts_parties WHERE charid = ?", BoundValue.Of(CharId));
        Check("no trusts, no party, no accounts_parties row", "0/0/0",
            $"{c.PTrusts.Count}/{Solo()}/{(row != null && row.Next() ? row.Get<int>("n") : -1)}");
        Tick(10);
        Check("Naji faded out of the zone", false, info.Trusts.Contains(naji));

        Console.WriteLine("Casting the trust spell: onSpellCast's xi.trust.spawn");
        log.Clear();
        Send(0x01A, 28, b =>
        {
            BitConverter.GetBytes(CharId).CopyTo(b, 4);
            BitConverter.GetBytes(c.TargId).CopyTo(b, C2sAction.OffActIndex);
            BitConverter.GetBytes((ushort)ActionId.CastMagic).CopyTo(b, C2sAction.OffActionID);
            C2sAction.SetSpellId(b, (ushort)Kupipi);
        });
        Check("casting", true, c.MagicState != null);
        for (int i = 0; i < 40 && c.PTrusts.Count == 0; i++) Tick();
        var again = c.PTrusts.FirstOrDefault();
        Check("Kupipi called by the spell, in a new party", "True/kupipi/1",
            $"{again != null && again != kupipi && info.Trusts.Contains(again)}/{again?.Name}/{Solo()}");
        Check("no script errors (the cast)", "", string.Join(" | ", LuaErrors()));
        Tick(5);

        Console.WriteLine("Fighting with the master: Kupipi and Naji engage once the master swings");
        Lua($"player:spawnTrust({Naji})");
        naji = c.PTrusts.Count > 1 ? c.PTrusts[1] : null;
        Tick(10);
        var rabbit = info.MobById[Rabbit];
        Lua($"SpawnMob({Rabbit})");
        rabbit.X = c.X + 2; rabbit.Y = c.Y; rabbit.Z = c.Z;
        rabbit.Unkillable = true;
        c.Hp = c.MaxHp;
        log.Clear();
        Send(0x01A, 28, b =>
        {
            BitConverter.GetBytes(Rabbit).CopyTo(b, 4);
            BitConverter.GetBytes(rabbit.TargId).CopyTo(b, C2sAction.OffActIndex);
            BitConverter.GetBytes((ushort)ActionId.Attack).CopyTo(b, C2sAction.OffActionID);
        });
        Check("the master engaged, no trust yet (no swing)", "True/False/False", $"{c.AttackTarget == rabbit}/{again.Engaged}/{naji.Engaged}");
        for (int i = 0; i < 30 && !(again.Engaged && naji.Engaged); i++) Tick();
        Check("after the master's first swing both engage the rabbit", "True/True/True/True",
            $"{c.LastAttackMs != 0}/{again.Engaged && again.BattleTarget == rabbit}/{naji.Engaged && naji.BattleTarget == rabbit}/{again.Animation == 1}");
        int rabbitHp = rabbit.Hp;
        s.PacketList.Clear();
        Tick(25);
        Check("Naji (melee) closed in and hit: its swings to the master, the rabbit hating it", "True/True/True",
            $"{Dist(naji, rabbit) <= 4.0f}/{Sent(0x028).Any(p => BitConverter.ToUInt32(p.Buffer, 5) == naji.Id)}/{rabbit.Enmity.CE(naji) > 0}");
        Check("Kupipi (NO_MOVE) stayed in casting range", true, Dist(again, c) <= 15.0f);
        Check("the TP skills: Naji's three weapon skills, Kupipi's Starlight and Moonlight", "33,34,40/163,164",
            string.Join(",", naji.TpSkills.Select(t => t.SkillId)) + "/" + string.Join(",", again.TpSkills.Select(t => t.SkillId)));

        Check("Naji's gambit (NOT_HAS_TOP_ENMITY): Provoke on the rabbit, its recast, the rabbit's hate", "True/True",
            $"{naji.Recasts?.Has(RecastType.Ability, 35) == true}/{rabbit.Enmity.VE(naji) > 0}");
        Check("the JA went out as Naji's (0x028 category 6, Provoke)", true,
            Sent(0x028).Any(p => U(p, 40, 32) == naji.Id && U(p, 82, 4) == 6 && U(p, 86, 32) == 35));

        Console.WriteLine("Naji's weapon skill at 1000 TP (TryTrustSkill: ASAP, a random one as the opener)");
        naji.Tp = 1500;
        s.PacketList.Clear();
        log.Clear();
        int beforeWs = rabbit.Hp;
        ushort usedWs = 0;
        for (int i = 0; i < 20 && usedWs == 0; i++)
        {
            Tick();
            if (naji.WeaponSkillInUse != 0) usedWs = naji.WeaponSkillInUse;
        }
        Check("readied one of its weapon skills on the rabbit", "True/True",
            $"{usedWs is 33 or 34 or 40}/{Sent(0x028).Any(p => U(p, 40, 32) == naji.Id && U(p, 82, 4) == 7)}");
        for (int i = 0; i < 20 && naji.WeaponSkillInUse != 0; i++) Tick();
        var finish = Sent(0x028).FirstOrDefault(p => U(p, 40, 32) == naji.Id && U(p, 82, 4) == 3);
        Check("the finish: category 3 under the skill, on the rabbit, its TP spent", $"{usedWs}/{rabbit.Id}/True",
            finish == null ? "none" : $"{U(finish, 86, 32)}/{U(finish, 150, 32)}/{naji.Tp < 1000}");
        Check("the rabbit took it, no script errors", "True/", $"{rabbit.Hp < beforeWs || rabbit.Hp == 1}/" + string.Join(" | ", LuaErrors()));
        for (int i = 0; i < 40 && !(c.StatusEffects.HasStatusEffect(41) && naji.StatusEffects.HasStatusEffect(41)); i++) Tick();
        Check("Kupipi's gambits: Protectra and Shellra on the party", "True/True/True",
            $"{c.StatusEffects.HasStatusEffect((ushort)40)}/{c.StatusEffects.HasStatusEffect((ushort)41)}/" +
            $"{naji.StatusEffects.HasStatusEffect((ushort)40)}");
        Check("the casts went out as Kupipi's (0x028 category 4)", true,
            Sent(0x028).Any(p => BitConverter.ToUInt32(p.Buffer, 5) == again.Id));

        Console.WriteLine("Kupipi cures the hurt master (HPP_LT 75: the best cure it has)");
        for (int i = 0; i < 20 && again.MagicState != null; i++) Tick();
        c.Hp = c.MaxHp / 2;
        int hurt = c.Hp;
        ushort cureSpell = 0;
        for (int i = 0; i < 40 && !(cureSpell != 0 && again.MagicState == null); i++)
        {
            Tick();
            if (again.MagicState is { } st && st.Target == c) cureSpell = st.Spell.Id;
        }
        Check("a cure on the master (Cure I-VI) and the HP back up", "True/True", $"{cureSpell is >= 1 and <= 6}/{c.Hp > hurt + 100}");
        Check("its magic recast set", true, cureSpell != 0 && again.Recasts?.Has(RecastType.Magic, cureSpell) == true);

        Console.WriteLine("The rabbit fights a trust");
        rabbit.Enmity.UpdateEnmity(naji, 10000, 10000);
        for (int i = 0; i < 20 && naji.LastAttackedMs == 0; i++) Tick();
        Check("the rabbit turned on Naji and swung at it", "True/True", $"{rabbit.BattleTarget == naji}/{naji.LastAttackedMs != 0}");

        Console.WriteLine("The rabbit shoots the trust: CBattleEntity::OnRangedAttack marks the target attacked");
        // its swing held off, so only the shot can set Naji's LastAttacked (what the trust's rest gate reads)
        rabbit.SwingAtMs = now + 600_000;
        naji.LastAttackedMs = 0;
        rabbit.LastRangedAttackMs = 0;
        rabbit.RangedAttackEnabled = true;
        bool aimed = false;
        for (int i = 0; i < 40 && naji.LastAttackedMs == 0; i++)
        {
            Tick();
            aimed |= rabbit.InRangedAttack;
        }
        Check("a shot at Naji, the swing still held: Naji's LastAttacked set by the shot", "True/True/True/True",
            $"{aimed}/{rabbit.BattleTarget == naji}/{rabbit.SwingAtMs > now}/{naji.LastAttackedMs != 0}");
        rabbit.RangedAttackEnabled = false;
        for (int i = 0; i < 20 && rabbit.InRangedAttack; i++) Tick();
        rabbit.SwingAtMs = now;

        Console.WriteLine("The kill: the trusts put their weapons away");
        rabbit.Unkillable = false;
        rabbit.Hp = 1;
        for (int i = 0; i < 40 && rabbit.IsAlive; i++) Tick();
        Tick(4);
        Check("the rabbit died, the trusts disengaged", "False/False/False", $"{rabbit.IsAlive}/{again.Engaged}/{naji.Engaged}");
        Check("no script errors (the fight)", "", string.Join(" | ", LuaErrors()));
        Lua($"player:clearTrusts()");
        Tick(12);
        Lua($"player:spawnTrust({Kupipi})");
        again = c.PTrusts.FirstOrDefault();
        Tick(5);

        Console.WriteLine("Status effects on a trust: ticks, an AoE ability, the wear-off to its origin, no charm, its death");
        log.Clear();
        string kupipiLua = "local t = player:getPartyWithTrusts()[2]\n";
        Lua(kupipiLua + "t:addStatusEffect(xi.effect.POISON, { power = 20, duration = 60, tick = 3, origin = player })");
        int poisonedHp = again.Hp;
        Tick(8);
        Check("POISON on Kupipi: its REGEN_DOWN 20 ticks off its HP (the trustTick regen)", "True/20/True",
            $"{again.StatusEffects.HasStatusEffect((ushort)3)}/{again.GetMod(Mod.REGEN_DOWN)}/{again.Hp <= poisonedHp - 18}");
        Lua(kupipiLua + "t:addMod(xi.mod.REGAIN, 50)");
        int idleTp = again.Tp;
        Tick(8);
        Check("REGAIN on an idle trust (not a mob: no engagement needed)", true, again.Tp > idleTp);
        Lua(kupipiLua + "t:delMod(xi.mod.REGAIN, 50)");
        Check("CHARM_I refused on a trust (it has a master)", "false",
            Lua(kupipiLua + "report(tostring(t:addStatusEffect(xi.effect.CHARM_I, { duration = 30, origin = player })))"));
        // Benediction: a self-centered AoE (radius 14) over the party with its trusts (ForPartyWithTrusts)
        c.Hp = c.MaxHp / 2;
        again.Hp = again.MaxHp / 2;
        c.X = again.X + 2; c.Z = again.Z;
        s.PacketList.Clear();
        Send(0x01A, 28, b =>
        {
            BitConverter.GetBytes(c.Id).CopyTo(b, 4);
            BitConverter.GetBytes(c.TargId).CopyTo(b, C2sAction.OffActIndex);
            BitConverter.GetBytes((ushort)ActionId.JobAbility).CopyTo(b, C2sAction.OffActionID);
            C2sAction.SetSkillId(b, 18);
        });
        Tick(3);
        var bene = Sent(0x028).FirstOrDefault(p => U(p, 40, 32) == c.Id && U(p, 82, 4) == 6 && U(p, 86, 32) == 18);
        // one result per target (no added or spikes effect): 123 bits each from bit 150
        Check("Benediction's action: two targets, the master then Kupipi", $"2/{c.Id}/{again.Id}",
            bene == null ? "none" : $"{U(bene, 72, 6)}/{U(bene, 150, 32)}/{U(bene, 273, 32)}");
        Check("Kupipi healed in full, its POISON gone, the master healed too", "True/False/True",
            $"{again.Hp == again.MaxHp}/{again.StatusEffects.HasStatusEffect((ushort)3)}/{c.Hp == c.MaxHp}");
        var worn = Sent(0x029).FirstOrDefault(p => BitConverter.ToUInt32(p.Buffer, 8) == again.Id);
        Check("the wear-off to the POISON's origin (the master has Kupipi spawned): 0x029 204 on Kupipi", $"{c.Id}/3/204",
            worn == null ? "none" : $"{BitConverter.ToUInt32(worn.Buffer, 4)}/{BitConverter.ToUInt32(worn.Buffer, 12)}/{BitConverter.ToUInt16(worn.Buffer, 24)}");
        // its death: CDeathState wears the death-flagged effects silently; CBattleEntity::Die's DEATH listeners
        Lua(kupipiLua + "t:addStatusEffect(xi.effect.POISON, { power = 1, duration = 60, tick = 3, origin = player })\n" +
            "t:addListener('DEATH', 'VERIFY_TRUST_DEATH', function(mob) report('died ' .. mob:getName()) end)");
        Check("poisoned again", true, again.StatusEffects.HasStatusEffect((ushort)3));
        s.PacketList.Clear();
        got.Clear();
        again.Hp = 0;
        Tick();
        Check("Kupipi died: dying, out of the master's list, its POISON worn silently, the DEATH listener ran",
            "True/False/False/False/died kupipi",
            $"{again.DeathAtMs != 0}/{c.PTrusts.Contains(again)}/{again.StatusEffects.HasStatusEffect((ushort)3)}/" +
            $"{Sent(0x029).Any(p => BitConverter.ToUInt32(p.Buffer, 8) == again.Id)}/{string.Join(",", got)}");
        Check("no script errors (trust effects)", "", string.Join(" | ", LuaErrors()));
        Tick(12);
        Lua($"player:spawnTrust({Kupipi})");
        again = c.PTrusts.FirstOrDefault();
        Tick(5);

        Console.WriteLine("The master's death sends them away");
        Lua("player:setHP(0)");
        Tick(2);
        Check("dead master: no trusts, Kupipi despawning", "0/True", $"{c.PTrusts.Count}/{again?.FadeAtMs != 0 || !info.Trusts.Contains(again)}");
        Tick(10);
        Lua("player:setHP(1000)");
        Tick(2);

        Console.WriteLine("The master's zone-out sends them away");
        Lua($"player:spawnTrust({Naji})");
        var last = c.PTrusts.FirstOrDefault();
        Check("Naji out", true, last != null && info.Trusts.Contains(last));
        zone.OnCharTimeout(s, false);
        Tick();
        Check("the master gone: no trusts, Naji despawning", "0/True", $"{c.PTrusts.Count}/{last.PMaster == null}");
        Tick(10);
        Check("and out of the zone", false, info.Trusts.Contains(last));
        Check("no Lua errors", "", string.Join(" | ", LuaErrors()));
    }
}
