using System;
using System.Collections.Generic;
using System.IO;
using XI.Common;
using XI.Map;
using XI.Ipc;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Casket.Live -- live gate for the treasure caskets (xi.caskets through the spawn_casket mixin): the drop
// (PROWESS_CASKET_RATE), the chest's spawn and vars, the temp-item and item cutscenes, the lock's combination game, the
// party check and the 3 minute despawn; and the bindings they reach (queryEntitiesByName, getLeaderID, getAlliance,
// messageName, addTempItem, setModelId, getEventTarget). A WAR 99 and a Wild Rabbit of West Ronfaure, with the real
// MariaDB (xidb).
// MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 993430;
    private const ushort WestRonfaure = 100;
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
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("capy" + CharId), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Casky"), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(0.0f), BoundValue.Of(0.0f), BoundValue.Of(0.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 3000, 50, 1, 99)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 99)", BoundValue.Of(CharId));
        // a Hume, so xi.player.onGameIn's charCreate on this first login finds its race gear
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(CharId));
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

        List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);

        var reports = new List<string>();
        scripts.Lua.SetGlobal("casketPlayer", l => { l.PushObject(c); return 1; });
        scripts.Lua.SetGlobal("report", l => { reports.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            reports.Clear();
            scripts.Lua.DoString("local player = casketPlayer()\n" + code, "=verify_casket");
            return string.Join("|", reports);
        }

        var npcs = new List<NpcEntity>();
        foreach (var n in info.Npcs) if (n.Name == "Treasure_Casket") npcs.Add(n);
        npcs.Sort((x, y) => x.Id.CompareTo(y.Id));
        Check("West Ronfaure's 16 caskets, hidden", "16/True", $"{npcs.Count}/{npcs.TrueForAll(n => n.Status == NpcEntity.StatusDisappear)}");
        var chest = npcs[0];
        string V(string name) => Lua($"report(GetNPCByID({chest.Id}):getLocalVar('[caskets]{name}'))");
        string Has(string item, string loc) => Lua($"report(tostring(player:hasItem(xi.item.{item}, xi.inventoryLocation.{loc})))");
        int Item(string item) => int.Parse(Lua($"report(xi.item.{item})"));

        Console.WriteLine("queryEntitiesByName:");
        Check("the caskets by name (a full match), NPCs in id order", $"16/{chest.Id}",
              Lua("local r = player:getZone():queryEntitiesByName('Treasure_Casket') report(#r .. '/' .. r[1]:getID())"));
        Check("a regex: the caskets and the coffers", $"{16 + info.Npcs.FindAll(n => n.Name == "Treasure_Coffer").Count}",
              Lua("report(#player:getZone():queryEntitiesByName('Treasure_.*'))"));
        Check("a partial name matches nothing (with a warning)", "0/True",
              Lua("report(#player:getZone():queryEntitiesByName('Treasure'))") + "/" + log.Exists(l => l.Contains("Query for entity name: Treasure in zone")));
        Check("mobs too", true, int.Parse(Lua("report(#player:getZone():queryEntitiesByName('Wild_Rabbit'))")) > 0);

        Console.WriteLine("getLeaderID / getAlliance:");
        Check("solo: the char's own id, an alliance of one", $"{CharId}/1", Lua("report(player:getLeaderID() .. '/' .. #player:getAlliance())"));

        // the char next to the rabbit, which dies owned by it; the defeat message a second later hands out the rewards
        void Kill()
        {
            rabbit.DeathAtMs = rabbit.DespawnAtMs = rabbit.DefeatMsgAtMs = 0;
            rabbit.Animation = Anim.None;
            c.X = rabbit.X + 1; c.Y = rabbit.Y; c.Z = rabbit.Z;
            rabbit.OwnerId = c.Id;
            rabbit.Hp = 0;
            s.PacketList.Clear();
            now += 100;
            zone.Tick();
            now += 1000;
            zone.Tick();
            Pump();
        }
        // 0x02A messages as msg:param0, 0x027 as msg:param0
        List<string> Special() => Of(0x02A).ConvertAll(p => $"{BitConverter.ToUInt16(p.Buffer, 26) & 0x7FFF}:{BitConverter.ToUInt32(p.Buffer, 8)}");
        List<string> Named() => Of(0x027).ConvertAll(p => $"{BitConverter.ToUInt16(p.Buffer, 10)}:{BitConverter.ToUInt32(p.Buffer, 16)}");
        void Talk()
        {
            s.PacketList.Clear();
            Send(0x01A, 28, b =>
            {
                BitConverter.GetBytes(chest.Id).CopyTo(b, 4);
                BitConverter.GetBytes(chest.TargId).CopyTo(b, C2sAction.OffActIndex);
                BitConverter.GetBytes((ushort)ActionId.Talk).CopyTo(b, C2sAction.OffActionID);
            });
        }
        void EventEnd(int eventId, uint option)
        {
            s.PacketList.Clear();
            Send(0x05B, 20, b =>
            {
                BitConverter.GetBytes(chest.Id).CopyTo(b, 4);
                BitConverter.GetBytes(option).CopyTo(b, 0x08);
                BitConverter.GetBytes(chest.TargId).CopyTo(b, 0x0C);
                BitConverter.GetBytes((ushort)eventId).CopyTo(b, 0x12);
            });
        }
        string Event() => c.IsInEvent ? $"{c.CurrentEvent.EventId}:{c.CurrentEvent.Params.GetValueOrDefault(0)}" : "none";
        void Settle(long ms)
        {
            for (long t = 0; t < ms; t += 500)
            {
                now += 500;
                zone.Tick();
            }
        }

        const int TempMsg = 8077; // West Ronfaure's PLAYER_OBTAINS_TEMP_ITEM, the casket messages' base

        Console.WriteLine("spawnCasket: PROWESS_CASKET_RATE 100 makes the drop sure");
        Kill();
        Check("no prowess: 10% (seeded), no chest this time or a chest", true, true);
        foreach (var n in npcs)
            if (n.Status != NpcEntity.StatusDisappear) Lua($"GetNPCByID({n.Id}):setStatus(xi.status.DISAPPEAR) GetNPCByID({n.Id}):resetLocalVars()");
        Lua("player:addStatusEffect(xi.effect.PROWESS_CASKET_RATE, { power = 100, duration = 3600, origin = player })");
        Kill();
        Check("the first casket spawns where the rabbit died", $"0/{rabbit.X}/{rabbit.Z}", $"{chest.Status}/{chest.X}/{chest.Z}");
        Check("a blue (965) or brown (966) model", true, chest.Look[1] == 965 || chest.Look[1] == 966);
        Check("the others stay hidden", true, npcs.GetRange(1, 15).TrueForAll(n => n.Status == NpcEntity.StatusDisappear));
        Check("closed, owned by the char's id, the mob level kept", $"1/{CharId}/{rabbit.MLvl}", $"{V("SPAWNSTATUS")}/{V("PARTYID")}/{V("MOBLVL")}");
        Check("MONSTER_CONCEALED_CHEST to the alliance in the zone", true, Special().Contains($"{TempMsg + 21}:0"));

        Console.WriteLine("an unlocked chest of temporary items:");
        int potion = Item("POTION");
        Lua($@"local npc = GetNPCByID({chest.Id})
              npc:setLocalVar('[caskets]LOCKED', 0) npc:setLocalVar('[caskets]LOOT_TYPE', 1) npc:setLocalVar('[caskets]ITEMS_SET', 1)
              npc:setLocalVar('[caskets]TEMP1', {potion}) npc:setLocalVar('[caskets]TEMP2', 0) npc:setLocalVar('[caskets]TEMP3', 0)");
        Talk();
        Check("the temp cs (1001) with the temp items", $"1001:{potion}", Event());
        Check("the chest opens", "1/2", $"{chest.AnimationSub}/{V("SPAWNSTATUS")}");
        EventEnd(1001, 1u << 16 | 1);
        Check("the potion lands in the temporary items", "true", Has("POTION", "TEMPITEMS"));
        Check("PLAYER_OBTAINS_TEMP_ITEM by name, from the char (no sender shown)", true, Named().Contains($"{TempMsg | 0x8000}:{potion}"));
        Check("the slot emptied", "0", V("TEMP1"));
        Settle(2500);
        Check("empty: the chest fades out and its vars reset", "2/0/0", $"{chest.Status}/{V("SPAWNSTATUS")}/{V("PARTYID")}");

        Console.WriteLine("a locked chest: the combination");
        Kill();
        Check("the slot is reused", "0/1", $"{chest.Status}/{V("SPAWNSTATUS")}");
        int ring = Item("BLIND_RING");
        Lua($@"local npc = GetNPCByID({chest.Id})
              npc:setLocalVar('[caskets]LOCKED', 1) npc:setLocalVar('[caskets]LOOT_TYPE', 2) npc:setLocalVar('[caskets]ITEMS_SET', 1)
              npc:setLocalVar('[caskets]CORRECT_NUM', 42) npc:setLocalVar('[caskets]ATTEMPTS', 5) npc:setLocalVar('[caskets]FAILED_ATEMPTS', 0)
              npc:setLocalVar('[caskets]ITEM1', {ring}) npc:setLocalVar('[caskets]ITEM2', 0) npc:setLocalVar('[caskets]ITEM3', 0)
              npc:setLocalVar('[caskets]ITEM4', 0)");
        Talk();
        Check("the locked cs (1002) with 5 attempts", "1002:5", Event());
        EventEnd(1002, 30u << 16 | 1);
        Check("30: HUNCH_GREATER_LESS (greater), one attempt used", $"True/1",
              $"{Special().Contains($"{TempMsg + 7}:30")}/{V("FAILED_ATEMPTS")}");
        Talk();
        Check("4 attempts left", "1002:4", Event());
        EventEnd(1002, 42u << 16 | 1);
        Check("42: OPENED_LOCK by name, unlocked and open", $"True/0/1/2",
              $"{Named().Contains($"{(TempMsg + 10) | 0x8000}:0")}/{V("LOCKED")}/{chest.AnimationSub}/{V("SPAWNSTATUS")}");
        Talk();
        Check("the item cs (1000) with the items", $"1000:{ring}", Event());
        EventEnd(1000, 1u << 16 | 1);
        Check("the ring lands in the inventory", "true", Has("BLIND_RING", "INVENTORY"));
        Check("PLAYER_OBTAINS_ITEM (8075) by name", true, Named().Contains($"{8075 | 0x8000}:{ring}"));
        Settle(2500);
        Check("empty: gone", "2", chest.Status);

        Console.WriteLine("someone else's chest, and the 3 minute despawn");
        Kill();
        Lua($"GetNPCByID({chest.Id}):setLocalVar('[caskets]PARTYID', 1)");
        Talk();
        Check("another party's chest: no event", "none", Event());
        Settle(179_000);
        Check("still there at 2:59", "0/1", $"{chest.Status}/{V("SPAWNSTATUS")}");
        Settle(1_000);
        Check("at 3:00 it starts to fade out (untargetable), the vars kept", "1", V("SPAWNSTATUS"));
        Settle(2_000);
        Check("unopened, it fades out after 3 minutes", "2/0", $"{chest.Status}/{V("SPAWNSTATUS")}");

        Console.WriteLine("setModelId / messageName / getEventTarget:");
        Lua($"GetNPCByID({chest.Id}):setModelId(970)");
        Check("an NPC's model id", (ushort)970, chest.Look[1]);
        Lua($"GetNPCByID({chest.Id}):setModelId(77, xi.slot.HEAD)");
        Check("a slot sets the NPC's look slot", (ushort)77, chest.Look[2]);
        s.PacketList.Clear();
        Lua("player:setModelId(123, xi.slot.BODY)");
        Check("a char's body look, GRAP_LIST sent", "123/1", $"{c.Look.Body}/{Of(0x051).Count}");
        s.PacketList.Clear();
        Lua($"player:messageName(100, GetNPCByID({chest.Id}), 1, 2, 3, 4, 5, player, true)");
        var named = Of(0x027);
        Check("messageName with a shown sender: no 0x8000, the type, the numbers, both names", "100/5/1,2,3,4/Treasure_Casket/Casky",
              named.Count == 0 ? "none" : $"{BitConverter.ToUInt16(named[0].Buffer, 10)}/{BitConverter.ToUInt16(named[0].Buffer, 12)}/" +
              $"{BitConverter.ToUInt32(named[0].Buffer, 16)},{BitConverter.ToUInt32(named[0].Buffer, 20)},{BitConverter.ToUInt32(named[0].Buffer, 24)},{BitConverter.ToUInt32(named[0].Buffer, 28)}/" +
              $"{System.Text.Encoding.ASCII.GetString(named[0].Buffer, 32, 32).TrimEnd('\0')}/{System.Text.Encoding.ASCII.GetString(named[0].Buffer, 64, 16).TrimEnd('\0')}");
        Check("getEventTarget outside an event: nil, warned", "nil/True",
              Lua("report(tostring(player:getEventTarget()))") + "/" + log.Exists(l => l.Contains("EventTarget for event -1 is empty")));
        Check("addTempItem of a held temp item still adds (the script checks hasItem)", "true", Lua("report(tostring(player:addTempItem(xi.item.ETHER)))"));
        Check("no script errors", false, log.Exists(l => l.Contains("lua:") && !l.Contains("0 load errors")));
        foreach (var l in log) if (l.Contains("lua:")) Console.WriteLine("  log: " + l);
    }
}
