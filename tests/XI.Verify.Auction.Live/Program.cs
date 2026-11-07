using System;
using System.Collections.Generic;
using System.IO;
using XI.Ipc;
using XI.Map;
using XI.Persistence;
using XI.Zone;

// XI.Verify.Auction.Live -- live gate for the auction house (ZoneHost.Auction: c2s 0x04E AUC, auctionutils, s2c 0x04C
// AUC) against the real MariaDB (xidb) and its auction_house_buy trigger, which pays the seller through the delivery box.
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    // a (the seller) and b (the buyer) at Port Jeuno (an auction house), c at Port Windurst (none)
    private static readonly (uint Id, uint Acc, string Name, ushort Zone)[] Chars =
    {
        (990961, 990961, "Ahaa", 246), (990962, 990962, "Ahbb", 246), (990963, 990963, "Ahcc", 240),
    };

    // the gold bed is NoAuction, the holiday tree rare, the skeleton robe charged (30 charges)
    private const ushort Crystal = 4096, GoldBed = 7, Tree = 86, Robe = 11320, Gil = 0xFFFF;

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
            Sql(db, "DELETE FROM auction_house WHERE seller = ? OR buyer = ?", BoundValue.Of(id), BoundValue.Of(id));
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
                BoundValue.Of(id), BoundValue.Of("ah" + name), BoundValue.Of("x"), BoundValue.Of((byte)1));
            Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,nation,gmlevel) VALUES (?,?,?,?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of(acc), BoundValue.Of(name), BoundValue.Of(zoneId), BoundValue.Of((ushort)0),
                BoundValue.Of((byte)2), BoundValue.Of((byte)0));
            Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(0u), BoundValue.Of((ushort)0));
            Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 300, 50, 1, 30)", BoundValue.Of(id));
            Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 30)", BoundValue.Of(id));
            Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(id));
            Sql(db, "REPLACE INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?,0,0,?,?,?)",
                BoundValue.Of(id), BoundValue.Of(Gil), BoundValue.Of(id == Chars[1].Id ? 3000u : 1000u), BoundValue.Of(new byte[24]));
        }
        void Give(int i, byte slot, ushort item, uint qty, byte charges = 0)
        {
            var extra = new byte[24];
            extra[1] = charges;
            Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?,0,?,?,?,?)",
                BoundValue.Of(Chars[i].Id), BoundValue.Of(slot), BoundValue.Of(item), BoundValue.Of(qty), BoundValue.Of(extra));
        }
        Give(0, 1, Crystal, 12);
        Give(0, 2, Crystal, 5);
        Give(0, 3, GoldBed, 1);
        Give(0, 4, Tree, 1);
        Give(0, 5, Robe, 1, 5);  // charges spent
        Give(0, 6, Robe, 1, 30); // full
        Give(1, 1, Tree, 1);     // b already owns the holiday tree

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        zone.UnixNow = () => 1_700_000_000;
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
        int Count(CharData c, ushort id) { int n = 0; foreach (var i in c.Items) if (i.Location == 0 && i.ItemId == id) n += (int)i.Quantity; return n; }
        uint GilOf(CharData c) => c.Items.Find(i => i.Location == 0 && i.Slot == 0).Quantity;

        // 0x04C: cmd:AucWorkIndex:Result:ResultStatus:Param(u32@8:u16@12:u16@14:u32@16), then the parcel when it is set
        // (|Stat:ItemNo:ItemQuantity:Price:MarketNo:Name) or just its MarketNo (|m4)
        string Auc(BasicPacket p)
        {
            var b = p.Buffer;
            string s = $"{b[4]}:{(sbyte)b[5]}:r{b[6]:X2}:{b[7]}:{BitConverter.ToUInt32(b, 8)}:{BitConverter.ToUInt16(b, 12)}:{BitConverter.ToUInt16(b, 14)}:{BitConverter.ToUInt32(b, 16)}";
            if (b[20] != 0)
                s += $"|{b[20]}:{BitConverter.ToUInt16(b, 40)}:{b[42]}:{BitConverter.ToUInt32(b, 44)}:{BitConverter.ToUInt32(b, 48)}:" +
                     System.Text.Encoding.ASCII.GetString(b, 24, 16).TrimEnd('\0');
            else if (BitConverter.ToUInt32(b, 48) != 0) s += $"|m{BitConverter.ToUInt32(b, 48)}";
            return s;
        }
        string Results(MapSession s) => string.Join(" ", Of(s, 0x04C).ConvertAll(Auc));
        void Cmd(int i, byte cmd, sbyte work = 0, uint p8 = 0, ushort p12 = 0, ushort p14 = 0, uint p16 = 0, byte result = 0) =>
            Send(ss[i], 0x04E, 60, buf =>
            {
                buf[4] = cmd; buf[5] = (byte)work; buf[6] = result;
                BitConverter.GetBytes(p8).CopyTo(buf, 8);
                BitConverter.GetBytes(p12).CopyTo(buf, 12);
                BitConverter.GetBytes(p14).CopyTo(buf, 14);
                BitConverter.GetBytes(p16).CopyTo(buf, 16);
            });
        // AskCommit: Commission, ItemWorkIndex, ItemNo, ItemStacks; LotIn: LimitPrice, ItemWorkIndex, ItemStacks;
        // Bid: BidPrice, ItemNo, ItemStacks (0 a stack, 1 a single)
        void Ask(int i, uint price, byte slot, ushort item, uint stacks) => Cmd(i, 0x04, -1, price, slot, item, stacks);
        void Lot(int i, uint price, byte slot, uint stacks) => Cmd(i, 0x0B, 0, price, slot, 0, stacks);
        void Bid(int i, uint price, ushort item, uint stacks) => Cmd(i, 0x0E, 0, price, item, 0, stacks);
        string Listings(uint seller) =>
            Scalar(db, "SELECT IFNULL(GROUP_CONCAT(CONCAT(itemid, 'x', stack, '@', price, ':', seller_name, ':', IFNULL(buyer_name, '-'), ':', sale, ':', date, '/', sell_date) " +
                       "ORDER BY id SEPARATOR ' '), '') FROM auction_house WHERE seller = ?", BoundValue.Of(seller));
        string Dbox(uint charId) =>
            Scalar(db, "SELECT IFNULL(GROUP_CONCAT(CONCAT(itemid, 'x', quantity, ':', sender) ORDER BY slot SEPARATOR ' '), '') FROM delivery_box WHERE charid = ? AND box = 1",
                   BoundValue.Of(charId));
        int FullSize(CharData c) => c.Items.FindAll(i => i.Location == 0 && i.Slot >= 1 && i.Slot <= c.StorageSize[0]).Count;

        uint A = Chars[0].Id, B = Chars[1].Id;
        Login(0);
        Login(1);
        Login(2);
        var (a, b) = (cs[0], cs[1]);

        Console.WriteLine("validation:");
        Clear();
        Cmd(2, 0x05, -1);
        Check("a zone without an auction house: refused", "", Results(ss[2]));
        Cmd(0, 0x05, -1, result: 1);
        Check("Result not 0: refused", "", Results(ss[0]));
        Cmd(0, 0x0A, 0);
        Check("WorkCheck with AucWorkIndex 0: refused", "", Results(ss[0]));
        Cmd(0, 0x0B, 7, 100, 1, 0, 1);
        Check("LotIn at AucWorkIndex 7: refused", "", Results(ss[0]));
        Bid(1, 0, Crystal, 1);
        Check("a bid of 0: refused", "", Results(ss[1]));
        Ask(0, 1000, 1, Crystal, 2);
        Check("AskCommit ItemStacks 2: refused", "", Results(ss[0]));
        Cmd(0, 0x03, -1);
        Check("command 3: refused", "", Results(ss[0]));
        Check("a's gil untouched", 1000, GilOf(a));

        Console.WriteLine("AskCommit quotes the fee:");
        Clear();
        Ask(0, 1000, 1, Crystal, 0);
        Check("a stack: 4 + 0.5% of 1000", "4:-1:r01:2:9:1:4096:0|m4", Results(ss[0]));
        Clear();
        Ask(0, 1000, 1, Crystal, 1);
        Check("a single: 1 + 1% of 1000", "4:-1:r01:2:11:1:4096:1|m4", Results(ss[0]));
        Clear();
        Ask(0, 999999999, 1, Crystal, 1);
        Check("capped at AH_MAX_FEE", "4:-1:r01:2:10000:1:4096:1|m4", Results(ss[0]));
        Clear();
        Ask(0, 1000, 1, Tree, 1);
        Check("another item than the slot holds: nothing", "", Results(ss[0]));
        Ask(0, 1000, 3, GoldBed, 1);
        Check("NoAuction: nothing", "", Results(ss[0]));
        Ask(0, 1000, 9, Crystal, 1);
        Check("an empty slot: nothing", "", Results(ss[0]));
        Ask(0, 1000, 5, Robe, 1);
        Check("charges spent: 197", "4:0:rC5:0:0:0:0:0", Results(ss[0]));
        Clear();
        Ask(0, 1000, 6, Robe, 1);
        Check("full charges: the quote", "4:-1:r01:2:11:6:11320:1|m4", Results(ss[0]));
        Clear();
        var fullRobe = a.Items.Find(i => i.Slot == 6 && i.Location == 0);
        fullRobe.InBazaar = true;
        Ask(0, 1000, 6, Robe, 1);
        Check("a busy item: nothing", "", Results(ss[0]));
        fullRobe.InBazaar = false;

        Console.WriteLine("Info opens the sales status window, at most every 5 s:");
        Clear();
        Cmd(0, 0x05, -1);
        Check("open, no listings", "5:-1:r01:0:0:0:0:0", Results(ss[0]));
        Clear();
        now += 5000;
        Cmd(0, 0x05, -1);
        Check("5 s later: try again in a little while", "5:0:rF6:0:0:0:0:0", Results(ss[0]));

        Console.WriteLine("LotIn pays the fee, takes the item and lists it:");
        Clear();
        Lot(0, 1000, 2, 0);
        Check("a stack that is not full: 197", "11:0:rC5:0:0:0:0:0/1000/17", $"{Results(ss[0])}/{GilOf(a)}/{Count(a, Crystal)}");
        Clear();
        Lot(0, 1000, 1, 0);
        Check("the stack of 12 at 1000", "11:0:r01:0:0:0:0:0 12:0:r01:0:0:0:0:0", Results(ss[0]));
        Check("the fee and the stack are gone", "991/5", $"{GilOf(a)}/{Count(a, Crystal)}");
        Check("the listing", "4096x1@1000:Ahaa:-:0:1700000000/0", Listings(A));
        Clear();
        Lot(0, 100, 2, 1);
        Check("one of the 5 at 100", "11:0:r01:0:0:0:0:0 12:1:r01:0:0:0:0:0", Results(ss[0]));
        Check("2 gil and 1 crystal", "989/4", $"{GilOf(a)}/{Count(a, Crystal)}");
        Clear();
        Lot(0, 1000, 3, 1);
        Check("NoAuction: nothing", "/989/1", $"{Results(ss[0])}/{GilOf(a)}/{Count(a, GoldBed)}");
        Lot(0, 1000, 5, 1);
        Check("charges spent: 197", "11:0:rC5:0:0:0:0:0/989/2", $"{Results(ss[0])}/{GilOf(a)}/{Count(a, Robe)}");
        Clear();
        Lot(0, 999999999, 4, 1);
        Check("a fee a cannot pay: 197", "11:0:rC5:0:0:0:0:0/989/1", $"{Results(ss[0])}/{GilOf(a)}/{Count(a, Tree)}");
        Clear();
        statSettings.AhListLimit = 2;
        Lot(0, 500, 4, 1);
        Check("at the listing limit: 197, the fee refunded", "11:0:rC5:0:0:0:0:0/989/1", $"{Results(ss[0])}/{GilOf(a)}/{Count(a, Tree)}");
        statSettings.AhListLimit = 7;
        Check("still 2 listings", 2, Listings(A).Split(' ').Length);

        Console.WriteLine("the window reloads the listings:");
        Clear();
        now += 1;
        Cmd(0, 0x05, -1);
        Check("open, then the two slots", "5:-1:r01:0:0:0:0:0 12:0:r01:0:0:0:0:0|3:4096:0:1000:4: 12:1:r01:0:0:0:0:0|3:4096:1:100:4:",
              Results(ss[0]));
        Clear();
        Cmd(0, 0x0A, -1);
        Check("WorkCheck: the two slots", "12:0:r01:0:0:0:0:0|3:4096:0:1000:4: 12:1:r01:0:0:0:0:0|3:4096:1:100:4:", Results(ss[0]));
        Clear();
        Cmd(0, 0x0D, 1);
        Check("LotCheck slot 1", "13:1:r01:0:0:0:0:0|3:4096:1:100:4:", Results(ss[0]));
        Clear();
        Cmd(0, 0x0D, 4);
        Check("LotCheck an empty slot", "13:4:r01:0:0:0:0:0", Results(ss[0]));

        Console.WriteLine("CancelSale takes a listing down:");
        Clear();
        Cmd(0, 0x0C, 0);
        Check("cancel the stack", "12:0:r00:0:0:0:0:0", Results(ss[0]));
        Check("ItemSame follows the give's own", 2, Of(ss[0], 0x01D).Count);
        Check("the stack is back, no fee refund", "16/989", $"{Count(a, Crystal)}/{GilOf(a)}");
        Check("one listing left", "4096x0@100:Ahaa:-:0:1700000000/0", Listings(A));
        Clear();
        Cmd(0, 0x0C, 0);
        Check("cancel it again: kept in the window", "12:0:rE5:0:0:0:0:0|3:4096:0:1000:4:Ahaa/16", $"{Results(ss[0])}/{Count(a, Crystal)}");
        Clear();
        Cmd(0, 0x0C, 5);
        Check("a slot past the history", "12:5:rE5:0:0:0:0:0", Results(ss[0]));
        Clear();
        byte aSize = a.StorageSize[0];
        a.StorageSize[0] = (byte)FullSize(a);
        Cmd(0, 0x0C, 1);
        Check("a full inventory: kept", "12:1:rE5:0:0:0:0:0|3:4096:1:100:4:Ahaa", Results(ss[0]));
        a.StorageSize[0] = aSize;
        Check("the listing stays", "4096x0@100:Ahaa:-:0:1700000000/0", Listings(A));

        Console.WriteLine("Bid buys the cheapest listing at or under the bid:");
        Clear();
        Bid(1, 50, Crystal, 1);
        Check("nothing at 50: unable to buy", "14:0:rC5:0:50:4096:0:1/3000", $"{Results(ss[1])}/{GilOf(b)}");
        Clear();
        Bid(1, 500, Crystal, 0);
        Check("no stack listed", "14:0:rC5:0:500:4096:0:12/3000", $"{Results(ss[1])}/{GilOf(b)}");
        Clear();
        Bid(1, 5000, Crystal, 0);
        Check("short of gil", "14:0:rC5:0:5000:4096:0:12/3000", $"{Results(ss[1])}/{GilOf(b)}");
        Clear();
        Bid(1, 500, Tree, 1);
        Check("a rare item b holds", "14:0:rE5:0:0:0:0:0", Results(ss[1]));
        Clear();
        Bid(1, 500, 65000, 1);
        Check("an unknown item", "14:0:rC5:0:500:65000:0:1", Results(ss[1]));
        Clear();
        byte bSize = b.StorageSize[0];
        b.StorageSize[0] = (byte)FullSize(b);
        Bid(1, 150, Crystal, 1);
        Check("a full inventory", "14:0:rE5:0:0:0:0:0/3000", $"{Results(ss[1])}/{GilOf(b)}");
        b.StorageSize[0] = bSize;

        Console.WriteLine("a give that fails after the listing is marked sold rolls the sale back:");
        Clear();
        Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?,0,2,?,1,?)",
            BoundValue.Of(B), BoundValue.Of(GoldBed), BoundValue.Of(new byte[24])); // b's next free slot, behind the zone's back
        Bid(1, 150, Crystal, 1);
        Check("unable to buy", "14:0:rC5:0:150:4096:0:1", Results(ss[1]));
        Check("b keeps the gil, gets nothing", "3000/0", $"{GilOf(b)}/{Count(b, Crystal)}");
        Check("the listing is still open", "4096x0@100:Ahaa:-:0:1700000000/0", Listings(A));
        Check("the trigger's payment is undone", "", Dbox(A));
        Sql(db, "DELETE FROM char_inventory WHERE charid = ? AND location = 0 AND slot = 2", BoundValue.Of(B));

        Clear();
        Bid(1, 150, Crystal, 1);
        Check("bought at the bid", "14:0:r01:0:150:4096:0:1", Results(ss[1]));
        Check("ItemSame follows the give's own", 2, Of(ss[1], 0x01D).Count);
        Check("b paid the bid and has the crystal", "2850/1", $"{GilOf(b)}/{Count(b, Crystal)}");
        Check("the listing is sold at the bid", "4096x0@100:Ahaa:Ahbb:150:1700000000/1700000000", Listings(A));
        Check("the trigger sends a the gil", "65535x150:AH-Jeuno", Dbox(A));
        Check("in the db too", "2850", Scalar(db, "SELECT quantity FROM char_inventory WHERE charid = ? AND location = 0 AND slot = 0", BoundValue.Of(B)));
        Clear();
        Bid(1, 150, Crystal, 1);
        Check("sold out", "14:0:rC5:0:150:4096:0:1/2850", $"{Results(ss[1])}/{GilOf(b)}");

        Console.WriteLine("a stack, and a rare listing:");
        Clear();
        Lot(0, 800, 1, 0);
        Check("a lists 12 crystals again", "11:0:r01:0:0:0:0:0 12:0:r01:0:0:0:0:0|3:4096:0:1000:4:/981",
              $"{Results(ss[0])}/{GilOf(a)}");
        Clear();
        Bid(1, 900, Crystal, 0);
        Check("b buys the stack", "14:0:r01:0:900:4096:0:12/1950/13", $"{Results(ss[1])}/{GilOf(b)}/{Count(b, Crystal)}");
        Check("a's delivery box: the stack's gil too", "65535x150:AH-Jeuno 65535x900:AH-Jeuno", Dbox(A));
        Clear();
        Lot(0, 500, 4, 1);
        Check("a lists the tree", "11:0:r01:0:0:0:0:0/975", $"{Results(ss[0]).Split(' ')[0]}/{GilOf(a)}");
        now += 6000;
        Cmd(0, 0x05, -1);
        Check("a's window holds only it", "86:0:500", a.AhHistory.Count == 1 ? $"{a.AhHistory[0].ItemId}:{a.AhHistory[0].Stack}:{a.AhHistory[0].Price}" : $"{a.AhHistory.Count}");
        var spare = new Item { Location = 0, Slot = 30, ItemId = Tree, Quantity = 1 };
        a.Items.Add(spare);
        Clear();
        Cmd(0, 0x0C, 0);
        Check("cancel while holding another: kept", "12:0:rE5:0:0:0:0:0|3:86:1:500:4:Ahaa", Results(ss[0]));
        a.Items.Remove(spare);
        Clear();
        Cmd(0, 0x0C, 0);
        Check("cancel: the tree comes back", "12:0:r00:0:0:0:0:0/1", $"{Results(ss[0])}/{Count(a, Tree)}");
        Check("no open listing left", 0, (int)uint.Parse(Scalar(db, "SELECT COUNT(*) FROM auction_house WHERE seller = ? AND sale = 0", BoundValue.Of(A))));
    }
}
