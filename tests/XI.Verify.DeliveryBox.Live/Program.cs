using System;
using System.Collections.Generic;
using System.IO;
using XI.Ipc;
using XI.Map;
using XI.Persistence;
using XI.Zone;

// XI.Verify.DeliveryBox.Live -- live gate for the delivery box (ZoneHost.DeliveryBox: c2s 0x04D PBX, dboxutils, s2c 0x04B
// PBX_RESULT, the universal container it fills, the 0x096 synth refusal while it is open) against the real MariaDB (xidb)
// and its delivery_box_insert trigger.
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    // a and b at Port Jeuno (an auction house), c at Port Windurst (none) on a's account
    private static readonly (uint Id, uint Acc, string Name, ushort Zone)[] Chars =
    {
        (990951, 990951, "Dbaa", 246), (990952, 990952, "Dbbb", 246), (990953, 990951, "Dbcc", 240),
    };

    // the cupboard is NoDelivery but CanSendAccount, the gold bed only NoDelivery, the holiday tree rare
    private const ushort Crystal = 4096, Cupboard = 57, GoldBed = 7, Tree = 86, Gil = 0xFFFF;

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static ResultSet Sql(IDatabase db, string q, params BoundValue[] a) => db.Execute(q, a);

    private static string Scalar(IDatabase db, string q, params BoundValue[] a)
    {
        var r = db.Execute(q, a);
        return r != null && r.Next() ? r.Get<string>(0u) : null;
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
        foreach (var (id, _, _, _) in Chars)
        {
            foreach (var t in CharidTables(db))
                Sql(db, $"DELETE FROM `{t}` WHERE charid = ?", BoundValue.Of(id));
            Sql(db, "DELETE FROM delivery_box WHERE charid = ? OR senderid = ?", BoundValue.Of(id), BoundValue.Of(id));
            Sql(db, "DELETE FROM audit_dbox WHERE receiver = ?", BoundValue.Of(id));
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
        var statSettings = new StatSettings();
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), statSettings);

        foreach (var (id, acc, name, zoneId) in Chars)
        {
            Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of("db" + name), BoundValue.Of("x"), BoundValue.Of((byte)1));
            Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,nation,gmlevel) VALUES (?,?,?,?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of(acc), BoundValue.Of(name), BoundValue.Of(zoneId), BoundValue.Of((ushort)0),
                BoundValue.Of((byte)2), BoundValue.Of((byte)0));
            Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(0u), BoundValue.Of((ushort)0));
            Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 300, 50, 1, 30)", BoundValue.Of(id));
            Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 30)", BoundValue.Of(id));
            Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(id));
            Sql(db, "REPLACE INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?,0,0,?,1000,?)",
                BoundValue.Of(id), BoundValue.Of(Gil), BoundValue.Of(new byte[24]));
        }
        void Give(int i, byte slot, ushort item, uint qty) =>
            Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?,0,?,?,?,?)",
                BoundValue.Of(Chars[i].Id), BoundValue.Of(slot), BoundValue.Of(item), BoundValue.Of(qty), BoundValue.Of(new byte[24]));
        Give(0, 1, Crystal, 12);
        Give(0, 2, Cupboard, 1);
        Give(0, 3, GoldBed, 1);
        Give(0, 4, Tree, 1);
        Give(1, 1, Tree, 1); // b already owns the holiday tree

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        var queue = new Queue<IIpcMessage>();
        zone.SendIpc = m => queue.Enqueue(m);

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
            Send(ss[i], 0x00C, 12);
        }
        List<BasicPacket> Of(MapSession s, int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);
        void Clear() { foreach (var s in ss) s?.PacketList.Clear(); log.Clear(); queue.Clear(); }
        bool Logged(string text) => log.Exists(l => l.Contains(text));
        int Count(CharData c, ushort id) { int n = 0; foreach (var i in c.Items) if (i.Location == 0 && i.ItemId == id) n += (int)i.Quantity; return n; }

        // 0x04B: the short form cmd:box:result:ResParam1-3, the long cmd:box:PostWorkNo:result:count:stat:item x stack:name
        string Pbx(BasicPacket p)
        {
            var b = p.Buffer;
            int size = (b[1] & 0xFE) << 1;
            if (size == 0x14) return $"{b[4]}:{(sbyte)b[5]}:r{b[12]:X2}:{b[13]}:{b[14]}:{b[15]}";
            string name = System.Text.Encoding.ASCII.GetString(b, 20, 16).TrimEnd('\0');
            return $"{b[4]}:{(sbyte)b[5]}:{b[6]}:r{b[12]:X2}:n{b[13]}:s{BitConverter.ToUInt32(b, 16)}:{BitConverter.ToUInt16(b, 48)}x{BitConverter.ToUInt32(b, 56)}:{name}";
        }
        string Results(MapSession s) => string.Join(" ", Of(s, 0x04B).ConvertAll(Pbx));
        void Cmd(int i, byte cmd, sbyte box = -1, sbyte post = -1, sbyte itemWork = -1, int stacks = -1, string name = null, byte result = 0) =>
            Send(ss[i], 0x04D, 32, buf =>
            {
                buf[4] = cmd; buf[5] = (byte)box; buf[6] = (byte)post; buf[7] = (byte)itemWork;
                BitConverter.GetBytes(stacks).CopyTo(buf, 8);
                buf[12] = result;
                if (name != null) System.Text.Encoding.ASCII.GetBytes(name).CopyTo(buf, 16);
            });
        string Rows(uint charId, int box) =>
            Scalar(db, "SELECT IFNULL(GROUP_CONCAT(CONCAT(slot, ':', itemid, 'x', quantity, ':', sent, received, ':', sender) ORDER BY slot SEPARATOR ' '), '') " +
                       "FROM delivery_box WHERE charid = ? AND box = ?", BoundValue.Of(charId), BoundValue.Of(box));

        uint A = Chars[0].Id, B = Chars[1].Id;
        Login(0);
        Login(1);
        Login(2);
        var (a, b) = (cs[0], cs[1]);
        Check("the char's accid is loaded", $"{Chars[2].Acc}", cs[2].AccId);

        Console.WriteLine("the zone gate: no auction house, mog house or GM level:");
        Clear();
        Cmd(2, 0x0D);
        Check("refused, nothing sent", "True/", $"{Logged("disallowed zone")}/{Results(ss[2])}");
        Check("the box stays closed", UContainerType.Empty, cs[2].UContainer.Type);

        Console.WriteLine("validation:");
        Clear();
        Cmd(0, 0x0D, result: 1);
        Check("Result not 0: refused", "", Results(ss[0]));
        Cmd(0, 0x02, box: 1, post: 0, itemWork: 1, stacks: 1, name: "Dbbb");
        Check("Set into the incoming box: refused", "", Results(ss[0]));
        Cmd(0, 0x10);
        Check("command 0x10: refused", "", Results(ss[0]));

        Console.WriteLine("a opens the send box and checks names:");
        Clear();
        Cmd(0, 0x0D);
        Check("DeliOpen", "13:2:r01:255:255:255", Results(ss[0]));
        Check("the container is the send box", UContainerType.SendDeliveryBox, a.UContainer.Type);
        Clear();
        Cmd(0, 0x0C, name: "Dbbb");
        Check("another account", "12:-1:r02:255:255:255 12:-1:r01:0:255:255", Results(ss[0]));
        Clear();
        Cmd(0, 0x0C, name: "Dbcc");
        Check("the same account", "12:-1:r02:255:255:255 12:-1:r01:1:255:255", Results(ss[0]));
        Clear();
        Cmd(0, 0x0C, name: "Nobody");
        Check("no such char", "12:-1:r02:255:255:255 12:-1:rFB:0:255:255", Results(ss[0]));
        Clear();
        Cmd(0, 0x01, box: 2);
        var work = Of(ss[0], 0x04B);
        Check("Work: the 8 empty slots", "8/1:2:0:r01:n0:s0:0x0:/1:2:7:r01:n0:s0:0x0:",
              $"{work.Count}/{(work.Count > 0 ? Pbx(work[0]) : "")}/{(work.Count > 7 ? Pbx(work[7]) : "")}");

        Console.WriteLine("Set: the stack moves from the inventory into the outgoing row:");
        Clear();
        Cmd(0, 0x02, box: 2, post: 0, itemWork: 1, stacks: 5, name: "Dbbb");
        Check("Set", "2:2:0:r01:n1:s1:4096x5:Dbbb", Results(ss[0]));
        Check("ItemSame follows", 1, Of(ss[0], 0x01D).Count);
        Check("7 crystals left", 7, Count(a, Crystal));
        Check("the outgoing row", "0:4096x5:00:Dbbb", Rows(A, 2));
        Clear();
        Cmd(0, 0x02, box: 2, post: 0, itemWork: 1, stacks: 1, name: "Dbbb");
        Check("a taken slot: nothing", "/7", $"{Results(ss[0])}/{Count(a, Crystal)}");
        Cmd(0, 0x02, box: 2, post: 1, itemWork: 1, stacks: 8, name: "Dbbb");
        Check("more than the stack: nothing", "True//7", $"{Logged("insufficient")}/{Results(ss[0])}/{Count(a, Crystal)}");
        Cmd(0, 0x02, box: 2, post: 1, itemWork: 1, stacks: 1, name: "Nobody");
        Check("no such receiver: nothing", "/0:4096x5:00:Dbbb", $"{Results(ss[0])}/{Rows(A, 2)}");
        Cmd(0, 0x02, box: 2, post: 1, itemWork: 2, stacks: 1, name: "Dbbb");
        Check("NoDelivery to another account: nothing", "/1", $"{Results(ss[0])}/{Count(a, Cupboard)}");
        Cmd(0, 0x02, box: 2, post: 1, itemWork: 3, stacks: 1, name: "Dbcc");
        Check("NoDelivery without CanSendAccount: nothing", "/1", $"{Results(ss[0])}/{Count(a, GoldBed)}");
        Cmd(0, 0x02, box: 2, post: 1, itemWork: 2, stacks: 1, name: "Dbcc");
        Check("CanSendAccount to the same account", "2:2:1:r01:n2:s1:57x1:Dbcc/0", $"{Results(ss[0])}/{Count(a, Cupboard)}");
        Clear();
        Cmd(0, 0x0A, box: 2, post: 1);
        Check("Get from the outgoing box before it is sent", "10:2:1:r01:n2:s0:57x1:/1", $"{Results(ss[0])}/{Count(a, Cupboard)}");
        Check("the row is gone", "0:4096x5:00:Dbbb", Rows(A, 2));

        Console.WriteLine("Send queues a copy in b's incoming box; Cancel takes it back:");
        Clear();
        Cmd(0, 0x03, box: 2, post: 0);
        Check("Send", "3:2:0:r02:n1:s3:4096x5:Dbbb 3:2:0:r01:n1:s3:4096x5:Dbbb", Results(ss[0]));
        Check("marked sent", "0:4096x5:10:Dbbb", Rows(A, 2));
        Check("b's queue (the trigger numbers it from 8)", "8:4096x5:00:Dbaa", Rows(B, 1));
        Clear();
        Cmd(0, 0x03, box: 2, post: 0);
        Check("sending it again: nothing", "", Results(ss[0]));
        Clear();
        Cmd(0, 0x0A, box: 2, post: 0);
        Check("Get while it is on its way: refused", "10:2:0:rBA:n1:s3:4096x5:Dbbb/7", $"{Results(ss[0])}/{Count(a, Crystal)}");
        Clear();
        Cmd(0, 0x04, box: 2, post: 0);
        Check("Cancel", "4:2:0:r02:n1:s4:4096x5:Dbbb 4:2:0:r01:n1:s5:4096x5:Dbbb", Results(ss[0]));
        Check("unsent", "0:4096x5:00:Dbbb", Rows(A, 2));
        Check("b's queue is empty", "", Rows(B, 1));
        Clear();
        Cmd(0, 0x04, box: 2, post: 0);
        Check("cancelling an unsent item: the orphan row goes, backlogged", "15:2:r01:255:255:255 4:2:rFF:255:255:255/", $"{Results(ss[0])}/{Rows(A, 2)}");

        Console.WriteLine("the inflight cap:");
        Cmd(0, 0x0F); // the client closes the box on the 0x0F
        Cmd(0, 0x0D);
        Clear();
        Cmd(0, 0x02, box: 2, post: 0, itemWork: 1, stacks: 5, name: "Dbbb");
        Cmd(0, 0x02, box: 2, post: 1, itemWork: 1, stacks: 1, name: "Dbbb");
        Cmd(0, 0x03, box: 2, post: 0);
        statSettings.DeliveryBoxMaxInflight = 1;
        Clear();
        Cmd(0, 0x03, box: 2, post: 1);
        Check("at capacity: 0xFE", "3:2:1:r02:n1:s5:4096x1:Dbbb 3:2:1:r01:n1:s5:4096x1:Dbbb".Replace("r01", "rFE"), Results(ss[0]));
        Check("not queued", "8:4096x5:00:Dbaa", Rows(B, 1));
        statSettings.DeliveryBoxMaxInflight = 128;
        Cmd(0, 0x0A, box: 2, post: 1);
        Check("taken back", 2, Count(a, Crystal));

        Console.WriteLine("b receives:");
        Clear();
        Cmd(1, 0x0E);
        Check("PostOpen", "14:1:r01:255:255:255", Results(ss[1]));
        Clear();
        Cmd(1, 0x05, box: 1);
        Check("Check: 1 new", "5:1:r02:255:255:255 5:1:r01:255:1:255", Results(ss[1]));
        statSettings.AuditPlayerDbox = true;
        Clear();
        Cmd(1, 0x06, box: 1, post: 0, itemWork: 1);
        Check("Recv", "6:1:0:r02:n1:s6:0x0: 6:1:0:r01:n1:s7:4096x5:Dbaa", Results(ss[1]));
        Check("moved to slot 0", "0:4096x5:00:Dbaa", Rows(B, 1));
        Check("the sender's row is received", "0:4096x5:11:Dbbb", Rows(A, 2));
        Check("audited", "1", Scalar(db, "SELECT COUNT(*) FROM audit_dbox WHERE receiver = ? AND sender = ? AND itemid = ?",
                                       BoundValue.Of(B), BoundValue.Of(A), BoundValue.Of(Crystal)));
        statSettings.AuditPlayerDbox = false;
        Clear();
        Cmd(1, 0x06, box: 1, post: 1, itemWork: 1);
        Check("Recv with nothing queued: rolled back, 0xEB", "6:1:rEB:255:255:255", Results(ss[1]));
        Clear();
        Cmd(1, 0x08, box: 1, post: 0);
        Check("Accept", "8:1:0:r01:n1:s7:4096x5:Dbaa", Results(ss[1]));
        Clear();
        Cmd(1, 0x0A, box: 1, post: 0);
        Check("Get", "10:1:0:r01:n1:s0:4096x5:/5", $"{Results(ss[1])}/{Count(b, Crystal)}");
        Check("b's box is empty", "", Rows(B, 1));
        Check("the crystals are saved", "5", Scalar(db, "SELECT SUM(quantity) FROM char_inventory WHERE charid = ? AND itemId = ?",
                                                     BoundValue.Of(B), BoundValue.Of(Crystal)));

        Console.WriteLine("a sees the delivery and confirms it:");
        Clear();
        Cmd(0, 0x05, box: 2);
        Check("Check: 1 delivered", "5:2:r02:255:255:255 5:2:r01:255:255:1", Results(ss[0]));
        Clear();
        Cmd(0, 0x07);
        Check("Confirm", "7:-1:r02:255:255:255 7:-1:0:r01:n1:s3:4096x5:Dbbb", Results(ss[0]));
        Check("the outgoing row is gone", "", Rows(A, 2));

        Console.WriteLine("b returns an item to a; a clears it:");
        Clear();
        Check("the stack taken back went to a slot of its own", "1/1", $"{a.Items.FindAll(i => i.Location == 0 && i.ItemId == Crystal).Count - 1}/{Count(a, Crystal) - 1}");
        Cmd(0, 0x02, box: 2, post: 0, itemWork: 1, stacks: 1, name: "Dbbb");
        Cmd(0, 0x03, box: 2, post: 0);
        Cmd(1, 0x06, box: 1, post: 0, itemWork: 1);
        Clear();
        Cmd(1, 0x09, box: 1, post: 0);
        Check("Reject", "9:1:0:r02:n0:s7:4096x1:Dbaa 9:1:0:r01:n0:s0:4096x1:", Results(ss[1]));
        Check("queued for a", "8:4096x1:00:Dbbb", Rows(A, 1));
        Check("gone from b", "", Rows(B, 1));
        Cmd(0, 0x0F);
        Check("PostClose cleans the container", UContainerType.Empty, a.UContainer.Type);
        Cmd(0, 0x0E);
        Cmd(0, 0x06, box: 1, post: 0, itemWork: 1);
        Clear();
        Cmd(0, 0x0B, box: 1, post: 0);
        Check("Clear", "11:1:0:r01:n0:s0:4096x1:", Results(ss[0]));
        Check("a's box is empty", "", Rows(A, 1));

        Console.WriteLine("a rare item b already owns:");
        Cmd(0, 0x0F);
        Cmd(0, 0x0D);
        Clear();
        Cmd(0, 0x02, box: 2, post: 0, itemWork: 4, stacks: 1, name: "Dbbb");
        Check("a slot whose row is still in the table (the delivered, unconfirmed crystal): the insert fails, nothing taken",
              "/1/0:4096x1:11:Dbbb", $"{Results(ss[0])}/{Count(a, Tree)}/{Rows(A, 2)}");
        Cmd(0, 0x02, box: 2, post: 1, itemWork: 4, stacks: 1, name: "Dbbb");
        Cmd(0, 0x03, box: 2, post: 1);
        Cmd(1, 0x06, box: 1, post: 0, itemWork: 1);
        Clear();
        Cmd(1, 0x0A, box: 1, post: 0);
        Check("ItemEx, then refused", "1/10:1:0:rBA:n1:s7:86x1:Dbaa/1", $"{Of(ss[1], 0x029).Count + Of(ss[1], 0x009).Count}/{Results(ss[1])}/{Count(b, Tree)}");
        Check("still in the box", "0:86x1:00:Dbaa", Rows(B, 1));

        Console.WriteLine("Work reloads the box after it is reopened:");
        Cmd(1, 0x0F);
        Cmd(1, 0x0E);
        Clear();
        Cmd(1, 0x01, box: 1);
        work = Of(ss[1], 0x04B);
        Check("slot 0 holds the tree", "1:1:0:r01:n1:s7:86x1:Dbaa", work.Count > 0 ? Pbx(work[0]) : "");
        Check("in the container", Tree, b.UContainer.GetItem(0)?.ItemId);

        Console.WriteLine("synthesis while the box is open:");
        Clear();
        byte crystalSlot = b.Items.Find(i => i.Location == 0 && i.ItemId == Crystal).Slot;
        Send(ss[1], 0x096, 36, buf =>
        {
            BitConverter.GetBytes(Crystal).CopyTo(buf, 6);
            buf[8] = crystalSlot;
            buf[9] = 1;
            BitConverter.GetBytes(Crystal).CopyTo(buf, 10);
            buf[26] = crystalSlot;
        });
        var msg = Of(ss[1], 0x009);
        Check("CannotBeProcessed", "14", msg.Count > 0 ? BitConverter.ToUInt16(msg[0].Buffer, 10).ToString() : "");
        Check("the container is cleaned", UContainerType.Empty, b.UContainer.Type);
        Clear();
        Cmd(1, 0x01, box: 1);
        Check("Work on the cleaned container: refused", "True/", $"{Logged("invalid state")}/{Results(ss[1])}");

        Console.WriteLine("gil into slot 0: the second Recv packet waits for the next tick:");
        Cmd(1, 0x0E);
        Cmd(1, 0x01, box: 1);
        Cmd(1, 0x0B, box: 1, post: 0);
        Cmd(0, 0x02, box: 2, post: 2, itemWork: 0, stacks: 300, name: "Dbbb");
        Check("a pays the gil", 700, Count(a, Gil));
        Cmd(0, 0x03, box: 2, post: 2);
        Clear();
        Cmd(1, 0x06, box: 1, post: 0, itemWork: 1);
        Check("only the first", "6:1:0:r02:n1:s6:0x0:", Results(ss[1]));
        zone.Tick();
        Check("the second on the tick", "6:1:0:r02:n1:s6:0x0: 6:1:0:r01:n1:s7:65535x300:Dbaa", Results(ss[1]));
        Clear();
        Cmd(1, 0x0A, box: 1, post: 0);
        Check("Get gil", "10:1:0:r01:n1:s0:65535x300:/1300", $"{Results(ss[1])}/{Count(b, Gil)}");
    }
}
