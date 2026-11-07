using System;
using System.Collections.Generic;
using System.IO;
using XI.Ipc;
using XI.Map;
using XI.Persistence;
using XI.Zone;

// XI.Verify.Subcontainer.Live -- live gate for c2s 0x03B SUBCONTAINER (ZoneHost.Subcontainer: dressing a mannequin
// with gear from the storage, s2c 0x01F / 0x020 / 0x026 / 0x01D) and the installed furnishings it rests on (the
// storage size they add, ItemState PlacedFurniture, lockFlagFor's NoDrop) against the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 990921;
    private const string CharName = "Subca";

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

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var cfg = new StatSettings();
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), cfg);
        var loader = new CharLoader(db, Grades.Load(dataDir), AbilityTable.Load(db, _ => true), stats);
        var log = new List<string>();
        var zone = new ZoneHost(db, loader, dataDir, true, log.Add);
        var sent = new List<IIpcMessage>();
        zone.SendIpc = sent.Add;

        // WAR75 in its own mog house in Windurst Waters
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("sub" + CharName), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,moghouse,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(CharName), BoundValue.Of((ushort)238), BoundValue.Of((ushort)0),
            BoundValue.Of(CharId), BoundValue.Of((byte)2), BoundValue.Of((byte)0));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl, sjob, slvl) VALUES (?, 300, 50, 1, 75, 0, 1)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 75)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));
        void Put(byte loc, byte slot, ushort id, byte[] extra = null) =>
            Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?,?,?,?,1,?)",
                BoundValue.Of(CharId), BoundValue.Of(loc), BoundValue.Of(slot), BoundValue.Of(id), BoundValue.Of(extra ?? new byte[24]));
        byte[] Installed(byte race = 0) { var e = new byte[24]; e[1] = 0x40; e[18] = race; e[19] = 2; return e; }
        Put(1, 1, 256, Installed(1));   // an installed hume mannequin (storage 28), race 1, pose 2
        Put(1, 2, 2, Installed());      // an installed simple bed (storage 1)
        Put(1, 3, 1);                   // chocobo bedding, not installed (storage 1, not counted)
        Put(9, 1, 262, Installed(7));   // an installed galka mannequin on the second floor
        Put(2, 1, 12511);               // storage: a head (model 64)
        Put(2, 2, 12544);               // a body (model 2)
        Put(2, 3, 16482);               // a dagger (model 164)

        MapSession s = null;
        CharData c = null;
        void Send(ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(s, opcode, new BasicPacket(buf));
            zone.AfterParse(s);
        }
        void Login()
        {
            if (s != null) zone.OnCharTimeout(s, false);
            s = new MapSession { CharId = CharId, AccountId = CharId };
            zone.OnLogin(s);
            c = (CharData)s.Char;
            Send(0x00A, 92, buf => BitConverter.GetBytes(CharId).CopyTo(buf, 12));
            s.PacketList.Clear();
        }
        List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);
        string Ops() => string.Join(",", new List<BasicPacket>(s.PacketList).ConvertAll(p => $"{Opcode(p):X3}"));
        void Clear() { s.PacketList.Clear(); log.Clear(); sent.Clear(); }
        void Sub(uint kind, uint cat1, byte idx1, byte cidx, uint cat2, byte idx2) => Send(0x03B, 32, buf =>
        {
            BitConverter.GetBytes(kind).CopyTo(buf, 4);
            BitConverter.GetBytes(cat1).CopyTo(buf, 8);
            buf[12] = idx1;
            buf[13] = cidx;
            BitConverter.GetBytes(cat2).CopyTo(buf, 16);
            buf[20] = idx2;
        });
        string Lists() => string.Join(",", Of(0x01F).ConvertAll(p => $"{p.Buffer[10]}.{p.Buffer[11]}:{p.Buffer[12]:X2}"));
        string Slots(Item m) => string.Join(" ", new ArraySegment<byte>(m.Extra, 10, 8));
        string DbSlots(byte loc, byte slot)
        {
            var r = Sql(db, "SELECT extra FROM char_inventory WHERE charid = ? AND location = ? AND slot = ?", BoundValue.Of(CharId), BoundValue.Of(loc), BoundValue.Of(slot));
            return r != null && r.Next() ? string.Join(" ", new ArraySegment<byte>(r.GetBlobBytes("extra"), 10, 8)) : "-";
        }
        // 0x026: used / container / index, then the nine model ids, race and pose
        string Sc(BasicPacket p)
        {
            var b = p.Buffer;
            var models = new List<string>();
            for (int i = 0; i < 9; i++) models.Add($"{BitConverter.ToUInt16(b, 10 + 2 * i):X4}");
            return $"{b[4]}/{b[5]}/{b[6]}/{string.Join(" ", models)}/{b[28]}/{b[29]}/{p.GetSize()}";
        }

        Login();
        var mannequin = c.FindItem(Container.MogSafe, 1);
        Console.WriteLine("installed furnishings at login:");
        Check("storage: the installed mannequins and bed (28 + 1 + 28), not the bedding", 57, c.StorageSize[(int)Container.Storage]);
        Check("placed: the installed ones in the safes", "True/True/False/True",
              $"{mannequin.PlacedFurniture}/{c.FindItem(Container.MogSafe, 2).PlacedFurniture}/{c.FindItem(Container.MogSafe, 3).PlacedFurniture}/" +
              $"{c.FindItem(Container.MogSafe2, 1).PlacedFurniture}");
        Check("lockFlagFor: NoDrop for an installed furnishing, Normal otherwise", "5/0/0",
              $"{GameOkPackets.LockFlagFor(c, mannequin)}/{GameOkPackets.LockFlagFor(c, c.FindItem(Container.MogSafe, 3))}/" +
              $"{GameOkPackets.LockFlagFor(c, c.FindItem(Container.Storage, 1))}");

        Console.WriteLine("validation:");
        c.MoghouseId = 0;
        Clear();
        Sub(1, 1, 1, 3, 2, 1);
        Check("outside its own mog house", "True/0", $"{log.Exists(l => l.Contains("Character not in their mog house"))}/{s.PacketList.Count}");
        c.MoghouseId = CharId + 1;
        Clear();
        Sub(1, 1, 1, 3, 2, 1);
        Check("in someone else's", true, log.Exists(l => l.Contains("Character not in their mog house")));
        c.MoghouseId = CharId;
        Clear();
        Sub(3, 1, 1, 3, 2, 1);
        Check("Kind 3", "True/0", $"{log.Exists(l => l.Contains("not a valid SubcontainerKind"))}/{s.PacketList.Count}");
        Clear();
        Sub(1, 1, 1, 8, 2, 1);
        Check("ContainerIndex 8", true, log.Exists(l => l.Contains("not a valid SubcontainerIndex")));
        Clear();
        Sub(1, 0, 1, 3, 2, 1);
        Check("Category1 the inventory", true, log.Exists(l => l.Contains("Category1 value 0 is not allowed")));
        Clear();
        Sub(1, 9, 1, 3, 2, 1);
        Check("the second floor while locked", "True/0", $"{log.Exists(l => l.Contains("Category1 value 9 is not allowed"))}/{s.PacketList.Count}");

        Console.WriteLine("equip:");
        Clear();
        Sub(1, 1, 1, 3, 2, 1);
        Check("head from storage 1: 0x01F (Mannequin lock), 0x020, 0x026, 0x01D", "01F,020,026,01D", Ops());
        Check("0x01F: storage 1 locked 0x1B", "2.1:1B", Lists());
        Check("the exdata's head slot", "0 0 0 1 0 0 0 0", Slots(mannequin));
        Check("saved", "0 0 0 1 0 0 0 0", DbSlots(1, 1));
        var attr = Of(0x020)[0].Buffer;
        Check("0x020: the mannequin, NoDrop, its exdata", "256/1/1/5/1", $"{BitConverter.ToUInt16(attr, 12)}/{attr[14]}/{attr[15]}/{attr[16]}/{attr[17 + 13]}");
        Check("0x026: models + slot bases, race 1 pose 2", "1/1/1/0001 1040 2000 3000 4000 5000 6000 7000 8000/1/2/32", Sc(Of(0x026)[0]));
        Check("0x01D: all loaded", "1/18", $"{Of(0x01D)[0].Buffer[4]}/{Of(0x01D)[0].Buffer[5]}");
        Clear();
        Sub(1, 1, 1, 0, 2, 3);
        Sub(1, 1, 1, 4, 2, 2);
        Check("main and body", "3 0 0 1 2 0 0 0/1/1/1/0001 1040 2002 3000 4000 5000 60A4 7000 8000/1/2/32",
              $"{Slots(mannequin)}/{Sc(Of(0x026)[^1])}");
        Clear();
        Sub(1, 1, 1, 3, 2, 1);
        Check("the same item again takes it off: Normal", "2.1:00/3 0 0 0 2 0 0 0", $"{Lists()}/{Slots(mannequin)}");
        Clear();
        Sub(1, 1, 1, 5, 0, 1);
        Check("equip from outside the storage: logged, nothing", "True/", $"{log.Exists(l => l == "GP_CLI_COMMAND_SUBCONTAINER: Invalid item location passed to Mannequin Equip packet 0 by Subca")}/{Ops()}");
        Clear();
        Sub(1, 1, 1, 5, 2, 9);
        Check("an empty storage slot: no 0x01F, slot kept, no model", "01F?False/3 0 0 0 2 9 0 0/3000",
              $"01F?{Of(0x01F).Count > 0}/{Slots(mannequin)}/{BitConverter.ToUInt16(Of(0x026)[0].Buffer, 16):X4}");

        Console.WriteLine("unequip:");
        Clear();
        Sub(2, 1, 1, 0, 0, 3);
        Check("main: storage 3 Normal", "2.3:00/0 0 0 0 2 9 0 0", $"{Lists()}/{Slots(mannequin)}");
        Clear();
        Sub(5, 1, 1, 0, 0, 0);
        Check("all: each set slot Normal (the empty storage 9 none)", "2.2:00/0 0 0 0 0 0 0 0/0 0 0 0 0 0 0 0", $"{Lists()}/{Slots(mannequin)}/{DbSlots(1, 1)}");

        Console.WriteLine("not a mannequin:");
        Clear();
        Sub(1, 1, 2, 3, 2, 1);
        Check("the bed: logged, nothing", "True/",
              $"{log.Exists(l => l == "GP_CLI_COMMAND_SUBCONTAINER: Unable to load mannequin from slot 2 in location 1 by Subca")}/{Ops()}");
        Clear();
        Sub(1, 1, 7, 3, 2, 1);
        Check("an empty safe slot: logged", true, log.Exists(l => l.Contains("Unable to load mannequin from slot 7 in location 1")));

        Console.WriteLine("the second floor, unlocked:");
        c.MhFlag = 0x20;
        Clear();
        Sub(1, 9, 1, 7, 2, 2);
        Check("feet from storage 2 on the galka", "2.2:1B/0 0 0 0 0 0 0 2/1/9/1/0001 1000 2000 3000 4000 5002 6000 7000 8000/7/2/32",
              $"{Lists()}/{Slots(c.FindItem(Container.MogSafe2, 1))}/{Sc(Of(0x026)[0])}");
        cfg.MogHouse2F = false;
        Clear();
        Sub(1, 9, 1, 7, 2, 2);
        Check("ENABLE_MOG_HOUSE_2F off", true, log.Exists(l => l.Contains("Category1 value 9 is not allowed")));
        cfg.MogHouse2F = true;

        Console.WriteLine("a placed furnishing is busy:");
        Clear();
        // 0x029 ITEM_MOVE: quantity, from container, to container, from slot, to slot
        Send(0x029, 12, buf =>
        {
            BitConverter.GetBytes(1u).CopyTo(buf, 4);
            buf[8] = 1; buf[9] = 0; buf[10] = 1; buf[11] = 0xFF;
        });
        Check("the installed mannequin does not move", "True/False", $"{c.FindItem(Container.MogSafe, 1) != null}/{c.FindItem(Container.Inventory, 1) != null}");

        Console.WriteLine("reload:");
        Sub(1, 1, 1, 6, 2, 1);
        Login();
        Check("the exdata kept, the gear loads Normal", "0 0 0 0 0 0 1 0/0", $"{Slots(c.FindItem(Container.MogSafe, 1))}/{GameOkPackets.LockFlagFor(c, c.FindItem(Container.Storage, 1))}");
    }
}
