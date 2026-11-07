using System;
using System.Collections.Generic;
using System.IO;
using XI.Ipc;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Treasure.Live -- live gate for the treasure pool (ZoneHost.TreasurePool: CTreasurePool, c2s 0x041
// TROPHY_ENTRY / 0x042 TROPHY_ABSENCE, s2c 0x0D2 / 0x0D3, the pool's assignment at zone in / out and through the party,
// the addTreasure / getTreasurePool / CTreasurePool lua) against the real MariaDB (xidb) with the real scripts loaded.
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    // two at Port Windurst, one in Dynamis - Bastok [D] (a zone with its own pool)
    private static readonly (uint Id, string Name, ushort Zone)[] Chars =
    {
        (990941, "Tpaa", 240), (990942, "Tpbb", 240), (990943, "Tpcc", 295),
    };

    private const ushort Crystal = 4096, Potion = 4112, Kraken = 17440, Tree = 86; // the holiday tree: rare, not Ex

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
        foreach (var (id, _, _) in Chars)
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

        // Humes past their first login (the NEW_ADVENTURER title: no charCreate) that have seen the RoV opening
        // (Rhapsodies of Vana'diel, a zone-in cutscene in the starting cities: Resonance current, the other logs as a
        // new char has them); in Port Windurst at the mog house exit (a char at 0,0,0 is moved there by the zone's
        // onZoneIn)
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        var missions = new byte[15 * 70];
        for (int i = 0; i <= 3; i++) BitConverter.GetBytes((ushort)0xFFFF).CopyTo(missions, i * 70);
        BitConverter.GetBytes((ushort)101).CopyTo(missions, 6 * 70);
        BitConverter.GetBytes((ushort)1).CopyTo(missions, 13 * 70);
        missions[13 * 70 + 6] = 1;
        foreach (var (id, name, zoneId) in Chars)
        {
            Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of("tp" + name), BoundValue.Of("x"), BoundValue.Of((byte)1));
            var (x, y, z) = zoneId == 240 ? (198.0f, -15.65f, 258.0f) : (0.0f, 0.0f, 0.0f);
            Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,titles,missions) " +
                    "VALUES (?,?,?,?,?,?,?,?,?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(name), BoundValue.Of(zoneId), BoundValue.Of((ushort)0),
                BoundValue.Of(x), BoundValue.Of(y), BoundValue.Of(z), BoundValue.Of((byte)2), BoundValue.Of((byte)0),
                BoundValue.Of(titles), BoundValue.Of(missions));
            Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(id));
            Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(0u), BoundValue.Of((ushort)0));
            Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 300, 50, 1, 30)", BoundValue.Of(id));
            Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 30)", BoundValue.Of(id));
            Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(id));
        }
        // b already owns the kraken club
        Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?,0,1,?,1,?)",
            BoundValue.Of(Chars[1].Id), BoundValue.Of(Kraken), BoundValue.Of(new byte[24]));

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        var rolls = new Queue<int>();
        zone.RandomBelow = n => rolls.Count > 0 ? Math.Min(rolls.Dequeue(), n - 1) : 0;
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);

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

        var ss = new MapSession[Chars.Length];
        var cs = new CharData[Chars.Length];
        void Send(MapSession s, ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(s, opcode, new BasicPacket(buf));
            zone.AfterParse(s);
        }
        void Login(int i)
        {
            ss[i] = new MapSession { CharId = Chars[i].Id, AccountId = Chars[i].Id };
            zone.OnLogin(ss[i]);
            cs[i] = (CharData)ss[i].Char;
            Send(ss[i], 0x00A, 92, buf => BitConverter.GetBytes(Chars[i].Id).CopyTo(buf, 12));
        }
        List<BasicPacket> Of(MapSession s, int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);
        void Clear() { foreach (var s in ss) s?.PacketList.Clear(); log.Clear(); }
        bool Logged(string text) => log.Exists(l => l.Contains(text));
        int Count(CharData c, ushort id) { int n = 0; foreach (var i in c.Items) if (i.Location == 0 && i.ItemId == id) n += (int)i.Quantity; return n; }
        // 0x0D2: item@slot entry(old) dropper
        string Lists(MapSession s) => string.Join(" ", Of(s, 0x0D2).ConvertAll(p =>
            $"{BitConverter.ToUInt16(p.Buffer, 16)}@{p.Buffer[20]}:{p.Buffer[21]}:{BitConverter.ToUInt32(p.Buffer, 8)}"));
        // 0x0D3: slot:judge highest(id-990940)/lot entry(id-990940)/lot name2
        string Solutions(MapSession s) => string.Join(" ", Of(s, 0x0D3).ConvertAll(Solution));
        string Solution(BasicPacket p)
        {
            uint loot = BitConverter.ToUInt32(p.Buffer, 4), entry = BitConverter.ToUInt32(p.Buffer, 8);
            string name2 = System.Text.Encoding.ASCII.GetString(p.Buffer, 38, 16).TrimEnd('\0');
            return $"{p.Buffer[20]}:{p.Buffer[21]} {(loot == 0 ? 0 : loot - 990940)}/{BitConverter.ToUInt16(p.Buffer, 14)} " +
                   $"{(entry == 0 ? 0 : entry - 990940)}/{BitConverter.ToUInt16(p.Buffer, 18)}{(name2.Length > 0 ? " " + name2 : "")}";
        }
        // the last 0x0D3, as Solutions shows it
        string Last(MapSession s) => Of(s, 0x0D3) is var l && l.Count > 0 ? Solution(l[l.Count - 1]) : "";
        void Lot(int i, byte slot) => Send(ss[i], 0x041, 8, buf => buf[4] = slot);
        void Pass(int i, byte slot) => Send(ss[i], 0x042, 8, buf => buf[4] = slot);

        var reports = new List<string>();
        int luaChar = 0;
        scripts.Lua.SetGlobal("tpPlayer", l => { l.PushObject(cs[luaChar]); return 1; });
        scripts.Lua.SetGlobal("report", l => { reports.Add(l.ToStringValue(1)); return 0; });
        string Lua(int i, string code)
        {
            luaChar = i;
            reports.Clear();
            scripts.Lua.DoString("local player = tpPlayer()\n" + code, "=verify_treasure");
            return string.Join("|", reports);
        }

        Console.WriteLine("zone in: a pool of its own, game OK sends its 10 slots:");
        Login(0);
        var (a, sa) = (cs[0], ss[0]);
        Check("a solo pool", "1/1", Lua(0, "local p = player:getTreasurePool() report(p:getType()) report(p:memberCount())").Replace("|", "/"));
        Check("joining sent nothing (still Disappear)", "", Lists(sa));
        Clear();
        Send(sa, 0x00C, 12);
        Check("game OK: 10 empty old slots", "0@0:1:0 0@1:1:0 0@2:1:0 0@3:1:0 0@4:1:0 0@5:1:0 0@6:1:0 0@7:1:0 0@8:1:0 0@9:1:0", Lists(sa));
        var ops = new List<BasicPacket>(sa.PacketList).ConvertAll(p => Opcode(p));
        Check("the slots come last", 0x0D2, ops.Count > 0 ? ops[ops.Count - 1] : -1);

        Console.WriteLine("solo: addTreasure lands at once:");
        Clear();
        Lua(0, $"player:addTreasure({Crystal})");
        Check("the new slot, then the win", $"{Crystal}@0:0:0", Lists(sa));
        Check("won by a", "0:1 1/0 0/0", Solutions(sa));
        Check("in the inventory", 1, Count(a, Crystal));
        Check("the pool is empty again", "0", Lua(0, "local n = 0 for _, i in ipairs(player:getTreasurePool():getItems()) do n = n + i.id end report(n)"));
        Clear();
        Lua(0, $"player:addTreasure({Potion}, 0)");
        Check("drop rate 0: nothing", "/0", $"{Lists(sa)}/{Count(a, Potion)}");
        Clear();
        Lua(0, $"player:addTreasure({Potion}, player, 0)");
        Check("drop rate 0 with a dropper: nothing", "", Lists(sa));
        Clear();
        Lua(0, "player:addTreasure(0)");
        Check("item 0: nothing", "", Lists(sa));

        Console.WriteLine("solo with a full inventory: kept until its time is up, then lost:");
        byte invSize = a.StorageSize[0];
        a.StorageSize[0] = (byte)a.Items.FindAll(i => i.Location == 0 && i.Slot >= 1).Count;
        Clear();
        Lua(0, $"player:addTreasure({Potion}, player)");
        Check("listed with the dropper, not won", $"{Potion}@0:0:{Chars[0].Id}/", $"{Lists(sa)}/{Solutions(sa)}");
        Check("getItems: id, slot, a unix timestamp", $"{Potion}/0/true",
              Lua(0, "local i = player:getTreasurePool():getItems()[1] report(i.id) report(i.slotId) report(tostring(i.timestamp > 0))").Replace("|", "/"));
        Clear();
        Lot(0, 0);
        Check("no room: the lot is refused", "", Solutions(sa));
        now += 4000;
        zone.Tick();
        Check("not yet", "", Solutions(sa));
        now += 300_000;
        zone.Tick();
        Check("past 5 minutes: lost (WinError)", "0:2 0/0 0/0", Solutions(sa));
        a.StorageSize[0] = invSize;

        Console.WriteLine("validation:");
        Clear();
        Lot(0, 10);
        Check("slot 10 refused", "True/", $"{log.Count > 0}/{Solutions(sa)}");
        Clear();
        Lot(0, 3);
        Check("an empty slot: logged", true, Logged("is trying to lot on an item that doesn't exist"));

        Console.WriteLine("a party: one pool, lots and passes:");
        Login(1);
        var (b, sb) = (cs[1], ss[1]);
        Send(sb, 0x00C, 12);
        Send(sa, 0x06E, 12, buf => { BitConverter.GetBytes(b.Id).CopyTo(buf, 4); BitConverter.GetBytes(b.TargId).CopyTo(buf, 8); });
        Send(sb, 0x074, 8, buf => buf[4] = 1);
        Pump();
        Check("b in a's party", "Tpaa", Lua(1, "local l = player:getPartyLeader() report(l and l:getName() or 'none')"));
        Check("one shared Party pool", "6/2/True",
              Lua(0, "local p = player:getTreasurePool() report(p:getType()) report(p:memberCount())").Replace("|", "/") + "/" +
              (Lua(1, "report(player:getTreasurePool():memberCount())") == "2"));
        Clear();
        Lua(0, $"player:addTreasure({Potion})");
        Check("both see the item; nobody wins yet", $"{Potion}@0:0:0/{Potion}@0:0:0/",
              $"{Lists(sa)}/{Lists(sb)}/{Solutions(sa)}");
        Clear();
        rolls.Enqueue(499);
        Lot(1, 0);
        Check("b lots 500: both see it", "0:0 2/500 2/500 Tpbb|0:0 2/500 2/500 Tpbb", $"{Solutions(sa)}|{Solutions(sb)}");
        Clear();
        Lot(1, 0);
        Check("a second lot is ignored", "", Solutions(sb));
        Clear();
        Pass(0, 0);
        Check("a passes (65535 to the others), then b wins", "0:0 2/500 1/65535 Tpaa 0:1 2/0 0/0", Solutions(sb));
        Check("in b's inventory", 1, Count(b, Potion));

        Clear();
        Lua(0, $"player:addTreasure({Potion})");
        Pass(0, 0);
        Pass(1, 0);
        Check("both pass: lost", "0:2 0/0 0/0", Last(sa));

        Clear();
        Lua(0, $"player:addTreasure({Crystal})");
        Lot(0, 0);
        now += 301_000;
        zone.Tick();
        Check("time up with a lot: the lotter wins", "0:1 1/0 0/0", Last(sa));
        Check("a has both crystals", 2, Count(a, Crystal));

        Clear();
        Lua(0, $"player:addTreasure({Potion})");
        rolls.Enqueue(1);
        now += 301_000;
        zone.Tick();
        Check("time up without lots: a random member (the second roll: b)", "0:1 2/0 0/0", Solutions(sa));
        Check("b has two potions", 2, Count(b, Potion));

        Console.WriteLine("rare items:");
        Clear();
        Lua(0, $"player:addTreasure({Kraken})");
        Check("a lacks it: listed", $"{Kraken}@0:0:0", Lists(sa));
        Clear();
        Lot(1, 0);
        Check("b owns one: its lot is refused", "", Solutions(sb));
        Lot(0, 0);
        Check("a lots; not everyone has, so the item waits", 1, Of(sa, 0x0D3).Count);
        now += 301_000;
        zone.Tick();
        Check("a wins it", 1, Count(a, Kraken));
        Clear();
        Check("both own it: addItem returns the count unchanged", "0/",
              Lua(0, $"report(player:getTreasurePool():addItem({Kraken}))") + "/" + Lists(sa));

        Console.WriteLine("a full pool: the oldest plain item is decided to make room:");
        Clear();
        for (int i = 0; i < 10; i++) { now += 10; Lua(0, $"player:getTreasurePool():addItem({(i == 0 ? Tree : Potion)})"); }
        Check("ten items", "10", Lua(0, "local n = 0 for _, i in ipairs(player:getTreasurePool():getItems()) do if i.id ~= 0 then n = n + 1 end end report(n)"));
        Clear();
        Check("an 11th: the oldest plain slot (1, not the rare 0) is decided first (won)", "10/1:1",
              Lua(0, $"report(player:getTreasurePool():addItem({Crystal}))") + "/" +
              (Of(sa, 0x0D3) is var d && d.Count > 0 ? $"{d[0].Buffer[20]}:{d[0].Buffer[21]}" : "none"));
        Check("slot 1 now holds the crystal", $"{Crystal}@1:0:0", Lists(sa).Split(' ')[0]);
        Clear();
        string Flush() => Lua(0, "local p = player:getTreasurePool() p:flush() local n = 0 for _, i in ipairs(p:getItems()) do if i.id ~= 0 then n = n + 1 end end report(n)");
        Check("flush: nothing due yet (4 s past now only)", "10", Flush());
        now += 297_000;
        Check("flush 297 s on: everything decided", "0", Flush());

        Console.WriteLine("leaving the party: a pool of one's own again:");
        Clear();
        Send(sb, 0x06F, 8, buf => buf[4] = 0);
        Pump();
        Check("b out of the party", "none", Lua(1, "local l = player:getPartyLeader() report(l and l:getName() or 'none')"));
        Check("b's own pool (made while still in the party: Party, as the C++), a's Solo again", "6/1|1/1",
              Lua(1, "local p = player:getTreasurePool() report(p:getType()) report(p:memberCount())").Replace("|", "/") + "|" +
              Lua(0, "local p = player:getTreasurePool() report(p:getType()) report(p:memberCount())").Replace("|", "/"));
        Check("b's slots resent twice (addMember's updatePool, then the party's, as the C++)", 20, Of(sb, 0x0D2).Count);

        Console.WriteLine("a zone with its own pool:");
        Login(2);
        Check("c in Dynamis' Zone pool", "128/1", Lua(2, "local p = player:getTreasurePool() report(p:getType()) report(p:memberCount())").Replace("|", "/"));
        Lua(2, $"player:getTreasurePool():addItem({Potion})");
        Check("a lone member with room in a zone pool still wins at once", 1, Count(cs[2], Potion));
        Check("CTreasurePool:getMembers", "Tpcc", Lua(2, "for _, m in ipairs(player:getTreasurePool():getMembers()) do report(m:getName()) end"));
        Check("delMember / addMember from lua", "0/1",
              Lua(2, "local p = player:getTreasurePool() p:delMember(player) report(p:memberCount())") + "/" +
              Lua(2, "report(player:getTreasurePool() == nil and 1 or 0)"));
        zone.OnCharTimeout(ss[2], false);
        Check("logged out: out of any pool", "1", Lua(2, "report(player:getTreasurePool() == nil and 1 or 0)"));

        Console.WriteLine("logout:");
        var poolA = Lua(0, "report(player:getTreasurePool():memberCount())");
        zone.OnCharTimeout(sa, false);
        Check("a out of its pool", "1/1", poolA + "/" + Lua(0, "report(player:getTreasurePool() == nil and 1 or 0)"));
    }
}
