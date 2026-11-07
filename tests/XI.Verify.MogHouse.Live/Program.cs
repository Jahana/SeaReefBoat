using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Ipc;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.MogHouse.Live -- live gate for the mog house handlers (ZoneHost.MogHouse): c2s 0x0CB MYROOM_IS (open / close
// to the party, remodel the 2F), 0x0FA MYROOM_LAYOUT (placing furnishings: the footprint, collision, OnTable parent and
// wall checks, the storage they add, s2c 0x0FA / 0x01C / 0x020 / 0x01D; the done-placing moghancement update with its
// key item, mods and save) and 0x0FB MYROOM_BANKIN (the storage shrink and the items moved out of it, cleanPot, the
// mannequin's gear released) and the gardening handlers 0x0FC-0x0FF (ZoneHost.Garden: planting, feeding, examining,
// harvesting, uprooting, drying, the grow / wilt tick) against the real MariaDB (xidb).
//
// Every login of the char goes the way XI.Host's do (ZoneHost.BeginLoginReads): its reads on a worker first, then the
// load and the 0x00A answered from them (PrefetchDatabase); each read answers as the database does, and the login makes
// no read of the char the read-ahead lacks.
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 990922, VisitorId = 990923;
    private const string CharName = "Moghs";

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
        foreach (var id in new[] { CharId, VisitorId })
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

    private static void Run(IDatabase direct)
    {
        // every query through the read-ahead's database, as in XI.Host
        var prefetch = new PrefetchDatabase(direct);
        IDatabase db = prefetch;
        var dataDir = FindDataDir();
        var cfg = new StatSettings();
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), cfg);
        var loader = new CharLoader(db, Grades.Load(dataDir), AbilityTable.Load(db, _ => true), stats);
        var log = new List<string>();
        var zone = new ZoneHost(db, loader, dataDir, true, log.Add);
        var sent = new List<IIpcMessage>();
        zone.SendIpc = sent.Add;
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);
        zone.LoginPrefetch = prefetch;
        PrefetchDatabase.ReadSet lastReads = null;
        prefetch.Closed += set => lastReads = set;

        // a Windurstian WAR75 in its own mog house in Windurst Waters
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("mog" + CharName), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,moghouse,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(CharName), BoundValue.Of((ushort)238), BoundValue.Of((ushort)0),
            BoundValue.Of(CharId), BoundValue.Of((byte)2), BoundValue.Of((byte)0));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl, sjob, slvl, mhflag) VALUES (?, 300, 50, 1, 75, 0, 1, 0)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 75)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));
        void Put(byte loc, byte slot, ushort id, byte[] extra = null) =>
            Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?,?,?,?,1,?)",
                BoundValue.Of(CharId), BoundValue.Of(loc), BoundValue.Of(slot), BoundValue.Of(id), BoundValue.Of(extra ?? new byte[24]));
        var pot = new byte[24];
        pot[0] = 5; pot[1] = 0x80; pot[2] = 1; pot[3] = 2; pot[4] = 3;
        for (int i = 12; i < 20; i++) pot[i] = 9;
        var mann = new byte[24];
        mann[1] = 0x40; mann[6] = 19; mann[10] = 1; mann[11] = 2; mann[18] = 1; mann[19] = 2;
        Put(1, 1, 2);       // simple bed: Floor 4x6, storage 1, element 6 aura 3
        Put(1, 2, 57);      // cupboard: Floor 3x2, storage 6, element 1 aura 6 (512)
        Put(1, 3, 24);      // oak table: Surface 3x4 height 100, storage 1, element 4 aura 9 (521)
        Put(1, 4, 88);      // timepiece: OnTable 1x1, storage 1, element 8 aura 5
        Put(1, 5, 129);     // imperial standard: Wall, storage 1, element 1 aura 9 (532)
        Put(1, 6, 185);     // rook banner: Wall, storage 1, element 1 aura 7 (532)
        Put(1, 7, 216, pot); // porcelain flowerpot: Floor 1x1, storage 1, element 4 aura 2, with a plant
        Put(1, 8, 256, mann); // an installed hume mannequin at col 19 (storage 28, element 7 aura 6) wearing storage 1 and 2
        Put(1, 9, 1);       // chocobo bedding: Floor 5x5, storage 1, element 8 aura 2
        Put(1, 10, 2);      // another simple bed, never installed
        Put(2, 1, 12511);   // storage: a head
        Put(2, 2, 12544);   // a body
        Put(2, 3, 16482);   // a dagger
        Put(2, 40, 16482);  // a dagger beyond the storage the furnishings give at first
        Put(1, 11, 572);    // herb seeds (3)
        Put(1, 12, 4098);   // wind crystals (3)
        Sql(db, "UPDATE char_inventory SET quantity = 3 WHERE charid = ? AND location = 1 AND slot IN (11, 12)", BoundValue.Of(CharId));

        MapSession s = null;
        CharData c = null;
        List<BasicPacket> loginPackets = null;
        void Send(ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(s, opcode, new BasicPacket(buf));
            zone.AfterParse(s);
        }
        int logins = 0;
        void Login()
        {
            if (s != null) zone.OnCharTimeout(s, false);
            s = new MapSession { CharId = CharId, AccountId = CharId };
            // MapNetwork.PrefetchLogin, then PumpDeferred once the reads are in
            var ahead = zone.BeginLoginReads(CharId);
            var until = DateTime.UtcNow.AddSeconds(30);
            while (!ahead.Ready && DateTime.UtcNow < until) System.Threading.Thread.Sleep(1);
            using (ahead.Serve())
            {
                zone.OnLogin(s);
                c = (CharData)s.Char;
                Send(0x00A, 92, buf => BitConverter.GetBytes(CharId).CopyTo(buf, 12));
            }
            ahead.Dispose();
            logins++;
            Check($"read-ahead login {logins}: its reads of the char all read ahead, most answered from them", "/True",
                  $"{string.Join(" | ", lastReads?.MissedCharReads ?? new List<string> { "no read set" })}/{lastReads?.Hits >= 20}");
            loginPackets = new List<BasicPacket>(s.PacketList);
            s.PacketList.Clear();
        }

        // each read of the read-ahead answers as the database does: the same columns, rows and cells
        {
            var reads = ZoneHost.LoginReads(CharId, loader.SpellExpansions);
            var bad = new List<string>();
            int rows = 0;
            using (var set = prefetch.Open(CharId))
            {
                foreach (var (sql, args) in reads) prefetch.Read(set, sql, args);
                using (prefetch.Serve(set))
                    foreach (var (sql, args) in reads)
                    {
                        string Dump(ResultSet r)
                        {
                            if (r == null) return "null";
                            var sb = new System.Text.StringBuilder();
                            var cols = Enumerable.Range(0, (int)r.ColumnCount()).Select(i => r.ColumnName((uint)i)).ToList();
                            sb.Append(string.Join(",", cols)).Append(';');
                            while (r.Next())
                            {
                                rows++;
                                foreach (var col in cols)
                                    sb.Append(r.IsNull(col) ? "NULL" : r.Get<string>(col) + "=" + Convert.ToHexString(r.GetBlobBytes(col))).Append(',');
                                sb.Append(';');
                            }
                            return sb.ToString();
                        }
                        string served = Dump(db.Execute(sql, args)), fresh = Dump(direct.Execute(sql, args));
                        if (served != fresh) bad.Add(sql);
                    }
                Check("read-ahead: every read answers as the database does", "/True", $"{string.Join(" | ", bad)}/{set.Hits == reads.Count && rows > 20}");
            }
        }
        List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);
        string Ops() => string.Join(",", new List<BasicPacket>(s.PacketList).ConvertAll(p => $"{Opcode(p):X3}"));
        void Clear() { s.PacketList.Clear(); log.Clear(); sent.Clear(); }
        bool Logged(string text) => log.Exists(l => l.Contains(text));
        void Is(MyroomIsKind kind, ushort param2 = 0) => Send(0x0CB, 8, buf => { buf[4] = (byte)kind; BitConverter.GetBytes(param2).CopyTo(buf, 6); });
        void Layout(ushort itemNo, byte idx, byte cat, byte floor, byte x, byte y, byte z, byte v) => Send(0x0FA, 16, buf =>
        {
            BitConverter.GetBytes(itemNo).CopyTo(buf, 4);
            buf[6] = idx; buf[7] = cat; buf[8] = floor; buf[9] = x; buf[10] = y; buf[11] = z; buf[12] = v;
        });
        void Bankin(ushort itemNo, byte idx, byte cat = 1) => Send(0x0FB, 8, buf =>
        {
            BitConverter.GetBytes(itemNo).CopyTo(buf, 4);
            buf[6] = idx; buf[7] = cat;
        });
        Item Safe(byte slot) => c.FindItem(Container.MogSafe, slot);
        string Hex(byte[] b, int from = 0, int count = 24) => BitConverter.ToString(b, from, count);
        // byte 1 / col / level / row / rotation
        string Furn(Item i) => $"{i.Extra[1]:X2}/{i.Extra[6]}/{i.Extra[7]}/{i.Extra[8]}/{i.Extra[9]}";
        byte[] DbExtra(byte loc, byte slot)
        {
            var r = Sql(db, "SELECT extra FROM char_inventory WHERE charid = ? AND location = ? AND slot = ?", BoundValue.Of(CharId), BoundValue.Of(loc), BoundValue.Of(slot));
            return r != null && r.Next() ? r.GetBlobBytes("extra") : null;
        }
        string DbScalar(string q)
        {
            var r = Sql(db, q, BoundValue.Of(CharId));
            return r != null && r.Next() ? r.Get<string>(0) : "-";
        }
        int StorageSize() => c.StorageSize[(int)Container.Storage];
        bool HasKi(int id) => (c.KeyItems[id / 512 * 128 + id % 512 / 8] & 1 << id % 8) != 0;
        bool DbHasKi(int id)
        {
            var r = Sql(db, "SELECT keyitems FROM chars WHERE charid = ?", BoundValue.Of(CharId));
            if (r == null || !r.Next()) return false;
            var b = r.GetBlobBytes("keyitems");
            int off = id / 512 * 128 + id % 512 / 8;
            return b != null && off < b.Length && (b[off] & 1 << id % 8) != 0;
        }
        short ModOf(Mod m) => c.ModStat[(int)m];

        Login();
        Console.WriteLine("login in the own mog house:");
        var login = loginPackets.Find(p => Opcode(p) == 0x00A);
        Check("0x00A LoginState MYROOM, the Windurstian's own model, no exit unlocked", "1/291/0",
              $"{BitConverter.ToUInt32(login.Buffer, 128)}/{BitConverter.ToUInt16(login.Buffer, 170)}/{login.Buffer[174]}");
        Check("storage: the installed mannequin's 28", 28, StorageSize());
        Check("a residential area: the 300 HP saved refilled to the maximum (LoadChar's IsResidentialArea)", "True/True/True",
              $"{c.MaxHp > 300}/{c.Hp == c.MaxHp}/{c.Mp == c.MaxMp}");
        var loginMannequins = loginPackets.FindAll(p => Opcode(p) == 0x026);
        Check("updateMannequins: the installed mannequin's 0x026 at the login (safe 1, slot 8)", "1/1.8",
              $"{loginMannequins.Count}/{(loginMannequins.Count > 0 ? $"{loginMannequins[0].Buffer[5]}.{loginMannequins[0].Buffer[6]}" : "-")}");
        Check("the dagger beyond the storage is out of reach", "True/False",
              $"{c.FindItem(Container.Storage, 40) != null}/{c.StorageSize[(int)Container.Storage] >= 40}");

        Console.WriteLine("0x0CB MYROOM_IS:");
        c.MoghouseId = 0;
        Clear();
        Is(MyroomIsKind.Open);
        Check("outside the mog house", "True/False", $"{Logged("Character not in their mog house")}/{c.MogHouseOpen}");
        c.MoghouseId = CharId;
        Clear();
        Is((MyroomIsKind)3);
        Check("Kind 3", true, Logged("not a valid MyroomIsKind"));
        Clear();
        Is(MyroomIsKind.Remodel, 600);
        Check("Param2 600", true, Logged("not a valid MyroomIsParam2"));
        Clear();
        Is(MyroomIsKind.Open);
        var sync = Of(0x067);
        Check("open: CharSync's MogHouseFlag", "True/1/1", $"{c.MogHouseOpen}/{sync.Count}/{(sync.Count > 0 ? sync[0].Buffer[0x24] : -1)}");
        Check("not a rent-a-room", false, Logged("Rent-a-Room"));
        Clear();
        Layout(2, 1, 1, 0, 0, 0, 0, 0);
        Check("layout while open", "True/0", $"{Logged("Mog House is open to visitors")}/{s.PacketList.Count}");
        Clear();
        Bankin(256, 8);
        Check("bankin while open", true, Logged("Mog House is open to visitors"));
        Clear();
        Is(MyroomIsKind.Remodel, 616);
        Check("remodel while open", "True/0", $"{Logged("Mog House is open to visitors")}/{c.MhFlag}");
        Clear();
        Is(MyroomIsKind.Close);
        sync = Of(0x067);
        Check("close: CharSync's MogHouseFlag", "False/1/0", $"{c.MogHouseOpen}/{sync.Count}/{(sync.Count > 0 ? sync[0].Buffer[0x24] : -1)}");
        Clear();
        Is(MyroomIsKind.Close);
        Check("close when closed: nothing", 0, s.PacketList.Count);
        Clear();
        Is(MyroomIsKind.Remodel, 616);
        Check("remodel to Bastokan without the 2F: logged, mhflag 0x80, message 293", "True/128/293/128",
              $"{Logged("remodeling MH2F without it unlocked")}/{c.MhFlag}/{(Of(0x009).Count > 0 ? BitConverter.ToUInt16(Of(0x009)[0].Buffer, 10) : -1)}/" +
              DbScalar("SELECT mhflag FROM char_stats WHERE charid = ?"));
        Clear();
        Is(MyroomIsKind.Remodel, 618);
        Check("Patio without the KI: the nation's style", "True/256", $"{Logged("Patio without owning the KI")}/{c.MhFlag}");
        Clear();
        Is(MyroomIsKind.Remodel, 615);
        Check("Sandorian: no style bits", 0, c.MhFlag);

        Console.WriteLine("0x0FA validation:");
        c.MoghouseId = 0;
        Clear();
        Layout(2, 1, 1, 0, 0, 0, 0, 0);
        Check("outside the mog house", "True/0", $"{Logged("Character is not in Mog House.")}/{s.PacketList.Count}");
        c.MoghouseId = CharId + 1;
        Clear();
        Layout(2, 1, 1, 0, 0, 0, 0, 0);
        Check("in someone else's", true, Logged("Character is not in Mog House."));
        c.MoghouseId = CharId;
        Clear();
        Layout(2, 1, 1, 2, 0, 0, 0, 0);
        Check("MyroomFloorFlg 2", true, Logged("MyroomFloorFlg out of range"));
        Clear();
        Layout(2, 1, 1, 0, 0, 0, 0, 4);
        Check("v 4", true, Logged("v out of range"));
        Clear();
        Layout(2, 1, 1, 0, 0, 26, 0, 0);
        Check("y 26", true, Logged("y out of range"));

        Console.WriteLine("0x0FA process refusals:");
        Clear();
        Layout(2, 1, 0, 0, 0, 0, 0, 0);
        Check("the inventory", "True/0", $"{Logged("Invalid container requested")}/{s.PacketList.Count}");
        Clear();
        Layout(2, 200, 1, 0, 0, 0, 0, 0);
        Check("slot 200", true, Logged("Invalid slot requested"));
        Clear();
        Layout(2, 1, 1, 1, 0, 0, 0, 0);
        Check("the 2F while locked", true, Logged("MH2F placement without 2F unlocked"));
        Clear();
        Layout(3, 1, 1, 0, 0, 0, 0, 0);
        Check("item id mismatch", true, Logged("Layout item id mismatch"));
        Clear();
        Layout(2, 20, 1, 0, 0, 0, 0, 0);
        Check("an empty slot: silent", "0/0", $"{log.Count}/{s.PacketList.Count}");
        Clear();
        Layout(2, 1, 1, 0, 6, 0, 0, 0);
        Check("the bed over the 1F's hole", "True/False", $"{Logged("Invalid placement footprint")}/{Safe(1).Installed}");
        Clear();
        Layout(2, 1, 1, 0, 0, 0, 20, 0);
        Check("the bed past the 1F's back wall (z 20..25)", true, Logged("Invalid placement footprint"));

        Console.WriteLine("0x0FA placing:");
        // item script stand-ins: the bed's onFurniturePlaced, the standard's onFurnitureRemoved
        scripts.Lua.DoString("xi.items = xi.items or {} " +
            "xi.items.simple_bed = { onFurniturePlaced = function(p) p:setLocalVar('bedPlaced', 1) end } " +
            "xi.items.imperial_standard = { onFurnitureRemoved = function(p) p:setLocalVar('standardRemoved', 1) end }", "=verify_moghouse");
        Clear();
        Layout(2, 1, 1, 0, 0, 0, 0, 0);
        var bed = Safe(1);
        var op = Of(0x0FA);
        Check("the bed: 0x0FA, 0x01C, the conditional NPCs (0x00E), 0x020, 0x01D", "0FA,01C,00E,00E,020,01D", Ops());
        Check("onFurniturePlaced: the bed's script hears the char", 1u, c.LocalVars.GetValueOrDefault("bedPlaced"));
        var mhNpcs = zone.ZoneInfoOf(238).Npcs;
        string CondNpc(BasicPacket p) => mhNpcs.Find(n => n.Id == BitConverter.ToUInt32(p.Buffer, 4))?.Name ?? "?";
        Check("SpawnConditionalNPCs: the Moogle and the Symphonic Curator sent to the char, both still hidden for the zone",
              "Moogle,Symphonic_Curator/True", $"{string.Join(",", Of(0x00E).ConvertAll(CondNpc))}/" +
              Of(0x00E).TrueForAll(p => mhNpcs.Find(n => n.Id == BitConverter.ToUInt32(p.Buffer, 4))?.Status == NpcEntity.StatusDisappear));
        Check("0x0FA: item / result / index / category / size", "2/0/1/1/16",
              op.Count == 0 ? "-" : $"{BitConverter.ToUInt16(op[0].Buffer, 4)}/{op[0].Buffer[6]}/{op[0].Buffer[10]}/{op[0].Buffer[11]}/{op[0].GetSize()}");
        Check("installed at 0,0, placed, its exdata saved", "40/0/0/0/0/True/40",
              $"{Furn(bed)}/{bed.PlacedFurniture}/{DbExtra(1, 1)[1]:X2}");
        Check("storage +1, the 0x01C shows it (1 + size)", "29/30", $"{StorageSize()}/{Of(0x01C)[0].Buffer[4 + 2]}");
        Check("0x020: NoDrop, its exdata", "5/40", $"{Of(0x020)[0].Buffer[16]}/{Of(0x020)[0].Buffer[18]:X2}");
        Clear();
        Layout(57, 2, 1, 0, 2, 0, 4, 0);
        Check("the cupboard on the bed", "True/False", $"{Logged("Placement collides with installed furniture")}/{Safe(2).Installed}");
        Clear();
        Layout(57, 2, 1, 0, 4, 0, 0, 0);
        Check("the cupboard 3 wide at x 4 reaches the hole", true, Logged("Invalid placement footprint"));
        Clear();
        Layout(57, 2, 1, 0, 4, 0, 0, 1);
        Check("rotated, 2 wide, it fits beside the bed", "40/4/0/0/1/35", $"{Furn(Safe(2))}/{StorageSize()}");
        Clear();
        Layout(24, 3, 1, 0, 14, 0, 0, 0);
        Check("the oak table", "40/14/0/0/0/36", $"{Furn(Safe(3))}/{StorageSize()}");
        Clear();
        Layout(88, 4, 1, 0, 15, 5, 1, 0);
        Check("the timepiece at a height no surface has", "True/False", $"{Logged("OnTable placement with no matching Surface parent")}/{Safe(4).Installed}");
        Clear();
        Layout(88, 4, 1, 0, 18, 10, 1, 0);
        Check("the timepiece off the table", true, Logged("OnTable placement with no matching Surface parent"));
        Clear();
        Layout(88, 4, 1, 0, 15, 0, 1, 0);
        Check("the timepiece on the floor under the table", true, Logged("Placement collides with installed furniture"));
        Clear();
        Layout(88, 4, 1, 0, 15, 10, 1, 0);
        Check("the timepiece on the table", "40/15/10/1/0/37", $"{Furn(Safe(4))}/{StorageSize()}");
        Clear();
        Layout(2, 1, 1, 0, 15, 0, 0, 0);
        Check("the bed onto the table's cells", true, Logged("Placement collides with installed furniture"));
        Clear();
        Layout(129, 5, 1, 0, 2, 0, 1, 0);
        Check("the standard with a z", true, Logged("Wall placement with non-zero y/z"));
        Clear();
        Layout(129, 5, 1, 0, 5, 0, 0, 0);
        Check("wall slot 5 on the 1F", true, Logged("Invalid wall slot requested"));
        Clear();
        Layout(129, 5, 1, 0, 2, 0, 0, 0);
        Check("the standard on wall slot 2", "40/2/0/0/0/38", $"{Furn(Safe(5))}/{StorageSize()}");
        Clear();
        Layout(185, 6, 1, 0, 2, 0, 0, 0);
        Check("the banner on the standard's slot", true, Logged("Wall slot already occupied"));
        Clear();
        Layout(185, 6, 1, 0, 3, 0, 0, 0);
        Check("the banner on slot 3", "40/3/39", $"{Safe(6).Extra[1]:X2}/{Safe(6).Extra[6]}/{StorageSize()}");
        c.MhFlag |= 0x20;
        Clear();
        Layout(216, 7, 1, 1, 0, 0, 0, 0);
        Check("the flowerpot on the 2F", "True/False", $"{Logged("has tried to gardening pot 216 on 2nd floor")}/{Safe(7).Installed}");
        Clear();
        Layout(216, 7, 1, 0, 19, 0, 23, 0);
        Check("the flowerpot in the 1F's corner, its plant kept", "C0/19/0/23/0/05/40", $"{Furn(Safe(7))}/{Safe(7).Extra[0]:X2}/{StorageSize()}");
        Check("the dagger now in reach", true, c.StorageSize[(int)Container.Storage] >= 40);
        Clear();
        Layout(1, 9, 1, 1, 15, 0, 21, 0);
        Check("the bedding on the 2F: no storage (a fresh install still sends 0x01C)", "41/15/0/21/0/40/0FA,01C,00E,00E,020,01D", $"{Furn(Safe(9))}/{StorageSize()}/{Ops()}");
        Clear();
        Layout(2, 1, 1, 0, 0, 0, 10, 2);
        Check("moving the bed: no storage, no 0x01C", "40/0/0/10/2/40/0FA,020,01D", $"{Furn(bed)}/{StorageSize()}/{Ops()}");
        Clear();
        Layout(2, 1, 1, 1, 0, 0, 0, 0);
        Check("the bed to the 2F", "True/40/0/0/10/2", $"{Logged("Floor change on installed furnishing")}/{Furn(bed)}");
        Check("the DB has the moved bed", "40-00-00-0A-02", Hex(DbExtra(1, 1), 1, 1) + "-" + Hex(DbExtra(1, 1), 6, 4));

        Console.WriteLine("0x0FA done placing (UpdateMoghancement):");
        Clear();
        Layout(0, 0, 0, 0, 0, 0, 0, 0);
        // 1F: element 1 = cupboard 6 + standard 9 + banner 7 = 22, element 4 = table 9 + pot 2 = 11, ...; the 2F bedding not counted
        var talk = Of(0x02A);
        Check("element 1's best: the standard's Conquest", "532/True/True", $"{c.Moghancement}/{HasKi(532)}/{DbHasKi(532)}");
        Check("shown as obtained (KEYITEM_OBTAINED 6557, no name)", "1/532/39325",
              talk.Count == 0 ? "-" : $"{talk.Count}/{BitConverter.ToUInt32(talk[0].Buffer, 8)}/{BitConverter.ToUInt16(talk[0].Buffer, 26)}");
        var sc = Of(0x055);
        Check("one 0x055 for table 1", "1/1", sc.Count == 0 ? "-" : $"{sc.Count}/{BitConverter.ToUInt16(sc[0].Buffer, 132)}");
        Check("CONQUEST_BONUS 6, saved", "6/532", $"{ModOf(Mod.CONQUEST_BONUS)}/{DbScalar("SELECT moghancement FROM chars WHERE charid = ?")}");
        Clear();
        Layout(0, 0, 0, 0, 0, 0, 0, 0);
        Check("again: only the reminder", "02A", Ops());

        Console.WriteLine("0x0FC-0x0FF gardening:");
        long now = 1_800_000_000;
        zone.UnixNow = () => now;
        zone.RandomBelow = _ => 20;
        uint Vana() => (uint)(now - 1009810800L);
        const long Day = 3456;
        var potItem = Safe(7);
        void PlantAdd(ushort addNo, byte plantIdx, byte addIdx, byte plantCat = 1, byte addCat = 1) => Send(0x0FC, 12, buf =>
        {
            BitConverter.GetBytes((ushort)216).CopyTo(buf, 4);
            BitConverter.GetBytes(addNo).CopyTo(buf, 6);
            buf[8] = plantIdx; buf[9] = addIdx; buf[10] = plantCat; buf[11] = addCat;
        });
        void PlantOp(ushort opcode, byte idx = 7, byte cat = 1, byte cancel = 0) => Send(opcode, opcode == 0x0FE ? 12 : 8, buf =>
        {
            BitConverter.GetBytes((ushort)216).CopyTo(buf, 4);
            buf[6] = idx; buf[7] = cat;
            if (opcode == 0x0FE) buf[8] = cancel;
        });
        void Advance(long days) { now += days * Day; zone.Tick(); }
        // stage / byte 1 / crystal1 / crystal2 / plant / byte 5
        string PotState(byte[] e) => $"{e[0]}/{e[1]:X2}/{e[2]}/{e[3]}/{e[4]}/{e[5]:X2}";
        uint NextStep() => BitConverter.ToUInt32(potItem.Extra, 16);
        string Para(BasicPacket p) => System.Text.Encoding.ASCII.GetString(p.Buffer, 13, 23).TrimEnd('\0').Trim();
        // 0x029 battle message: param @12, message @24
        string Battle(BasicPacket p) => $"{BitConverter.ToUInt32(p.Buffer, 12)}:{BitConverter.ToUInt16(p.Buffer, 24)}";
        uint Qty(byte slot) => Safe(slot)?.Quantity ?? 0;
        string Msg(BasicPacket p) => $"{BitConverter.ToUInt16(p.Buffer, 10)}:{Para(p)}";
        string FirstMsg() => Of(0x009).Count > 0 ? Msg(Of(0x009)[0]) : "-";

        Clear();
        PlantAdd(0, 7, 11);
        Check("AddItemNo 0", true, Logged("MyroomAddItemNo must not be 0"));
        Clear();
        PlantAdd(572, 7, 11, 1, 0);
        Check("an inventory seed", true, Logged("MyroomAddCategory value 0 is not allowed"));
        Clear();
        PlantOp(0x0FD, 7, 2);
        Check("a storage pot", true, Logged("MyroomPlantCategory value 2 is not allowed"));
        Clear();
        PlantAdd(572, 7, 7);
        Check("the pot into itself", true, Logged("trying to plant a pot into itself"));
        Clear();
        PlantAdd(573, 7, 11);
        Check("vegetable seeds not in slot 11", true, Logged("trying to plant item 573 that is not in slot 11"));
        Clear();
        PlantAdd(572, 1, 11);
        Check("the bed as a pot", "True/0", $"{Logged("GP_CLI_COMMAND_MYROOM_PLANT_ADD::process: Moghs has tried to use invalid gardening pot 2")}/{s.PacketList.Count}");
        Clear();
        PlantOp(0x0FE, 20);
        Check("crop of an empty slot", true, Logged("GP_CLI_COMMAND_MYROOM_PLANT_CROP::process(: Moghs has tried to use invalid gardening pot item (MyroomPlantCategory = 1, MyroomPlantItemIndex = 20)"));
        Clear();
        PlantOp(0x0FF, 10);
        Check("stop on the uninstalled bed: silent", "False/0", $"{Logged("PLANT_STOP")}/{s.PacketList.Count}");

        Clear();
        PlantAdd(572, 7, 11);
        Check("herb seeds sown: the seed spent, 'plants the seeds', the pot's operation and attr", "01E,009,0FA,020,01D/2/132:Para0 572",
              $"{Ops()}/{Qty(11)}/{FirstMsg()}");
        Check("the fresh plant: stage 1, the dried bit cleaned, herb, strength 20 (not examined)", "1/40/0/0/2/28", PotState(potItem.Extra));
        Check("planted now, the next stage in 9 days", $"{Vana()}/{Vana() + 9 * Day}", $"{BitConverter.ToUInt32(potItem.Extra, 12)}/{NextStep()}");
        Check("the DB: the pot and the seed", $"{Hex(potItem.Extra)}/2", $"{Hex(DbExtra(1, 7))}/{DbScalar("SELECT quantity FROM char_inventory WHERE charid = ? AND location = 1 AND slot = 11")}");
        Clear();
        PlantAdd(4098, 7, 12);
        Check("a crystal at stage 1: 'uses the item', the crystal given back, the pot untouched", "136:Para0 4098/3/3/1/40",
              $"{FirstMsg()}/{Qty(12)}/{DbScalar("SELECT quantity FROM char_inventory WHERE charid = ? AND location = 1 AND slot = 12")}/{PotState(potItem.Extra).Substring(0, 4)}");
        Check("no MYROOM_OPERATION", 0, Of(0x0FA).Count);

        Clear();
        PlantOp(0x0FD);
        Check("check: the seed sown, no crystals yet, then the operation", "572:256/0FA",
              $"{string.Join(",", Of(0x029).ConvertAll(Battle))}/{Opcode(new List<BasicPacket>(s.PacketList)[^1]):X3}");
        Check("examined, saved", "29/29", $"{potItem.Extra[5]:X2}/{DbExtra(1, 7)[5]:X2}");

        Clear();
        Advance(8);
        Check("8 days on: not grown", "1/0", $"{potItem.Extra[0]}/{Of(0x020).Count}");
        Advance(1);
        Check("the 9th day: first sprouts, unexamined, 4 more days, an attr", $"2/28/{Vana() + 4 * Day}/1",
              $"{potItem.Extra[0]}/{potItem.Extra[5]:X2}/{NextStep()}/{Of(0x020).Count}");
        Check("saved", 2, (int)DbExtra(1, 7)[0]);
        Advance(4);
        Check("not a tree: on to the second sprouts (6), 12 days", $"6/{Vana() + 12 * Day}", $"{potItem.Extra[0]}/{NextStep()}");
        Advance(12);
        Check("the crystal stage (7), 50 days", $"7/{Vana() + 50 * Day}", $"{potItem.Extra[0]}/{NextStep()}");

        Clear();
        PlantAdd(4098, 7, 12);
        Check("the wind crystal fed: the common feed, stage 8 for the fed 24 days, examined", $"8/40/0/3/2/29/{Vana() + 24 * Day}/2",
              $"{PotState(potItem.Extra)}/{NextStep()}/{Qty(12)}");
        Check("'uses the item', then 0x0FA, 0x020, 0x01D", "136:Para0 4098/0FA,020,01D", $"{FirstMsg()}/{string.Join(",", Ops().Split(',')[^3..])}");
        Clear();
        PlantOp(0x0FD);
        Check("check: the seed and the wind crystal", "572:256,4098:258", string.Join(",", Of(0x029).ConvertAll(Battle)));

        Advance(24);
        Advance(30);
        Check("third sprouts then mature", 10, (int)potItem.Extra[0]);
        Advance(200);
        Check("a mature plant does not grow or wilt", 10, (int)potItem.Extra[0]);

        Console.WriteLine("crop:");
        Clear();
        PlantOp(0x0FE);
        // wind 10 (fed) + 10 (herb) = 20, + (100 - 20) * 20 / 32 = 70: the cumulative 30, 50, 80 picks 638 (4-8), 20 of its 30 in: 7
        var harvest = Of(0x009);
        Check("harvested: 7 of 638 in safe slot 13", "638/7", $"{Safe(13)?.ItemId}/{Qty(13)}");
        Check("'Your moogle ... from the plant!' (0x48)", "134:Para0 638 Para1 7/72",
              harvest.Count == 0 ? "-" : $"{Msg(harvest[^1])}/{harvest[^1].Buffer[1] >> 1 << 2}");
        Check("the pot cleaned, the strength kept", "0/40/0/0/0/28", PotState(potItem.Extra));
        Check("the DB: the crop and the pot", "7/0", $"{DbScalar("SELECT quantity FROM char_inventory WHERE charid = ? AND location = 1 AND slot = 13 AND itemId = 638")}/{DbExtra(1, 7)[0]}");
        Clear();
        PlantOp(0x0FE);
        Check("crop of an empty pot: nothing", 0, s.PacketList.Count);

        Console.WriteLine("wilting, uprooting and drying:");
        Clear();
        PlantAdd(572, 7, 11);
        Advance(9 + 28);
        // stage 1's 9 days + 28 since, unexamined: 37 > 36
        Check("unexamined past the wilt time: wilted for good", $"11/{Vana() + 65535 * Day}", $"{potItem.Extra[0]}/{NextStep()}");
        Clear();
        PlantOp(0x0FF);
        Check("a wilted plant cannot be dried", 0, s.PacketList.Count);
        Clear();
        PlantOp(0x0FE);
        Check("cropping the wilted plant uproots it", "0FA,020,01D/0/40", $"{Ops()}/{potItem.Extra[0]}/{potItem.Extra[1]:X2}");
        zone.RandomBelow = _ => 0;
        Clear();
        PlantAdd(572, 7, 11);
        Check("the last seed", "0/0", $"{Qty(11)}/{DbScalar("SELECT COUNT(*) FROM char_inventory WHERE charid = ? AND location = 1 AND slot = 11")}");
        Clear();
        PlantOp(0x0FF);
        Check("stage 1 cannot be dried", 0, s.PacketList.Count);
        PlantOp(0x0FD);
        Advance(9);
        Clear();
        PlantOp(0x0FF);
        Check("dried: 'dries the plant', 0x0FA, 0x020, 0x01D", "009,0FA,020,01D/133:Para0 216/C0/C0",
              $"{Ops()}/{FirstMsg()}/{potItem.Extra[1]:X2}/{DbExtra(1, 7)[1]:X2}");
        Advance(100);
        Check("a dried plant stops growing", 2, (int)potItem.Extra[0]);
        Clear();
        PlantOp(0x0FE, 7, 1, 1);
        Check("cancelled crop: uprooted", "0FA,020,01D/0/40", $"{Ops()}/{potItem.Extra[0]}/{potItem.Extra[1]:X2}");

        Console.WriteLine("0x0FB validation and refusals:");
        Clear();
        Bankin(0, 1);
        Check("ItemNo 0", true, Logged("MyroomItemNo must not equal 0"));
        Clear();
        Bankin(2, 1, 0);
        Check("the inventory", true, Logged("MyroomCategory value 0 is not allowed"));
        c.MoghouseId = 0;
        Clear();
        Bankin(2, 1);
        Check("outside the mog house", true, Logged("Character is not in Mog House."));
        c.MoghouseId = CharId;
        Clear();
        Bankin(2, 200);
        Check("slot 200", true, Logged("Invalid slot requested"));
        Clear();
        Bankin(3, 1);
        Check("item id mismatch", true, Logged("Bankin item id mismatch"));
        Clear();
        Bankin(2, 10);
        Check("an uninstalled one", "True/0", $"{Logged("Bankin requested for an uninstalled item")}/{s.PacketList.Count}");

        Console.WriteLine("0x0FB bankin:");
        var dagger = c.FindItem(Container.Storage, 40);
        Clear();
        Bankin(129, 5);
        Check("the standard: its slot shrinks the storage, the dagger moved to slot 4", "00/0/0/0/0/False/39/4",
              $"{Furn(Safe(5))}/{Safe(5).PlacedFurniture}/{StorageSize()}/{dagger.Slot}");
        Check("0x020 empty 40 (the leaked exdata), 0x020 at 4, 0x01C, the conditional NPCs, 0x020, 0x01D", "020,020,01C,00E,00E,020,01D", Ops());
        Check("onFurnitureRemoved: the standard's script hears the char", 1u, c.LocalVars.GetValueOrDefault("standardRemoved"));
        var attrs = Of(0x020);
        Check("the empty 40 then the dagger at 4", "2.40:0/2.4:16482", $"{attrs[0].Buffer[14]}.{attrs[0].Buffer[15]}:{BitConverter.ToUInt16(attrs[0].Buffer, 12)}/" +
                                                                     $"{attrs[1].Buffer[14]}.{attrs[1].Buffer[15]}:{BitConverter.ToUInt16(attrs[1].Buffer, 12)}");
        Check("the DB: the dagger's slot, the standard's exdata", "1/00", $"{DbScalar("SELECT COUNT(*) FROM char_inventory WHERE charid = ? AND location = 2 AND slot = 4")}/{DbExtra(1, 5)[1]:X2}");
        Clear();
        Bankin(185, 6);
        Clear();
        Layout(0, 0, 0, 0, 0, 0, 0, 0);
        // element 1 = 6, element 4 = 11: the oak table's Gardening
        Check("element 4's best now: the table's Gardening", "521/False/True/0/36", $"{c.Moghancement}/{HasKi(532)}/{HasKi(521)}/{ModOf(Mod.CONQUEST_BONUS)}/{ModOf(Mod.GARDENING_WILT_BONUS)}");
        Check("both in table 1: one 0x055, the DB", "1/False/True/521", $"{Of(0x055).Count}/{DbHasKi(532)}/{DbHasKi(521)}/{DbScalar("SELECT moghancement FROM chars WHERE charid = ?")}");
        Clear();
        Bankin(216, 7);
        Check("the flowerpot: cleanPot", "00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00", Hex(Safe(7).Extra));
        Check("the DB too", "00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00", Hex(DbExtra(1, 7)));
        Clear();
        Bankin(1, 9);
        Check("the 2F bedding: no storage change", "01/37", $"{Safe(9).Extra[1]:X2}/{StorageSize()}");
        Clear();
        Bankin(256, 8);
        var mq = Of(0x026);
        Check("the mannequin: a zeroed 0x026, its gear released Normal", "1/1.8/2.1:00,2.2:00",
              $"{mq.Count}/{(mq.Count > 0 ? $"{mq[0].Buffer[5]}.{mq[0].Buffer[6]}" : "-")}/" +
              string.Join(",", Of(0x01F).ConvertAll(p => $"{p.Buffer[10]}.{p.Buffer[11]}:{p.Buffer[12]:X2}")));
        Check("its slots cleared, the storage 37 - 28", "0 0 0 0 0 0 0 0/9/0 0 0 0 0 0 0 0",
              $"{string.Join(" ", new ArraySegment<byte>(Safe(8).Extra, 10, 8))}/{StorageSize()}/{string.Join(" ", new ArraySegment<byte>(DbExtra(1, 8), 10, 8))}");
        Clear();
        Bankin(57, 2);
        // 9 storage, 4 items: 5 free, the cupboard's 6 can't go
        Check("the cupboard with too few free slots", "True/True/9", $"{Logged("furnishing can't be removed")}/{Safe(2).Installed}/{StorageSize()}");

        Console.WriteLine("relogin:");
        Login();
        Check("the moghancement and its mod back, the storage of the bed, cupboard, table and timepiece", "521/36/9/True",
              $"{c.Moghancement}/{ModOf(Mod.GARDENING_WILT_BONUS)}/{StorageSize()}/{HasKi(521)}");

        // ---- 0x05E: the mog house exit (zmrq) and entrances (zmr*), floors, visiting ----
        void MapRect(MapSession ms, string rect, byte exitBit = 0, byte exitMode = 0)
        {
            var buf = new byte[24];
            buf[0] = 0x5E; buf[1] = (byte)(buf.Length / 4 << 1);
            BitConverter.GetBytes(ZoneLine.PackId(rect)).CopyTo(buf, 4);
            buf[22] = exitBit; buf[23] = exitMode;
            zone.Dispatch(ms, 0x05E, new BasicPacket(buf));
            zone.AfterParse(ms);
        }
        string DbPos(uint id)
        {
            var r = Sql(db, "SELECT pos_zone, moghouse FROM chars WHERE charid = ?", BoundValue.Of(id));
            return r != null && r.Next() ? $"{r.Get<ushort>("pos_zone")}:{r.Get<uint>("moghouse")}" : "-";
        }
        Console.WriteLine("0x05E mog house exits and entrance:");
        c.MhFlag = 0;
        Clear();
        MapRect(s, "zmrq", 3, 3);
        Check("a city exit without its quest (mhflag 0x04): refused, still home", $"True/{CharId}", $"{Logged("Moghouse zoneline abuse")}/{c.MoghouseId}");
        Clear();
        MapRect(s, "zmrq", 0, 125);
        Check("2F without it unlocked: refused", $"True/{CharId}/0", $"{Logged("2F requested without it being unlocked")}/{c.MoghouseId}/{c.MhFlag}");
        c.MhFlag = 0x20;
        Clear();
        MapRect(s, "zmrq", 0, 125);
        zone.Tick();
        Check("2F unlocked: the 2F flag on, rezoned into the same mog house", $"{0x60}/{CharId}/238:{CharId}", $"{c.MhFlag}/{c.MoghouseId}/{DbPos(CharId)}");
        Login();
        EndEvents(s, c);
        c.MhFlag = 0x60;
        Clear();
        MapRect(s, "zmrq", 0, 0);
        Check("the regular exit: out of the mog house, the 2F flag off, at 0,0,0", "0/32/0", $"{c.MoghouseId}/{c.MhFlag}/{c.X}");
        zone.Tick();
        Check("the zone change: Windurst Waters, no mog house", "238:0", DbPos(CharId));
        Login();
        EndEvents(s, c);
        var entrance = zone.ZoneInfoOf(238).ZoneLines.Values.FirstOrDefault(l => ZoneLine.IdText(l.Id).StartsWith("zmr") && ZoneLine.IdText(l.Id) != "zmrq");
        Check("Windurst Waters has a mog house entrance", true, entrance != null);
        if (entrance != null)
        {
            (c.X, c.Y, c.Z) = (entrance.OriginX, entrance.OriginY, entrance.OriginZ);
            Clear();
            MapRect(s, ZoneLine.IdText(entrance.Id));
            Check("the entrance: the own mog house in the same zone, at the line's destination", $"{CharId}/238/{entrance.DestX}",
                  $"{c.MoghouseId}/{c.PrevZone}/{c.X}");
            zone.Tick();
            Check("the zone change into it", $"238:{CharId}", DbPos(CharId));
        }
        Login();
        EndEvents(s, c);

        Console.WriteLine("visiting (MogHouseContainer):");
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(VisitorId), BoundValue.Of("mogvisit"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,moghouse,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?)",
            BoundValue.Of(VisitorId), BoundValue.Of(VisitorId), BoundValue.Of("Mogvisit"), BoundValue.Of((ushort)238), BoundValue.Of((ushort)0),
            BoundValue.Of(0u), BoundValue.Of((byte)2), BoundValue.Of((byte)0));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(VisitorId), BoundValue.Of(VisitorId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 300, 50, 1, 75)", BoundValue.Of(VisitorId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 75)", BoundValue.Of(VisitorId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(VisitorId));
        MapSession vs = null;
        CharData v = null;
        List<BasicPacket> visitorLogin = null;
        void SendAs(MapSession ms, ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(ms, opcode, new BasicPacket(buf));
            zone.AfterParse(ms);
        }
        // a zone-in cutscene ended (0x05B) so the char can act
        void EndEvents(MapSession ms, CharData ch)
        {
            for (int i = 0; i < 4 && ch.IsInEvent; i++)
            {
                var ev = (ushort)ch.CurrentEvent.EventId;
                SendAs(ms, 0x05B, 20, b => BitConverter.GetBytes(ev).CopyTo(b, 0x12));
            }
            ms.PacketList.Clear();
        }
        void LoginVisitor()
        {
            if (vs != null) zone.OnCharTimeout(vs, false);
            vs = new MapSession { CharId = VisitorId, AccountId = VisitorId };
            zone.OnLogin(vs);
            v = (CharData)vs.Char;
            SendAs(vs, 0x00A, 92, buf => BitConverter.GetBytes(VisitorId).CopyTo(buf, 12));
            SendAs(vs, 0x00C, 12);
            visitorLogin = new List<BasicPacket>(vs.PacketList);
            vs.PacketList.Clear();
            EndEvents(vs, v);
        }
        LoginVisitor();
        var lua = scripts.Lua;
        var heard = new List<string>();
        lua.SetGlobal("report", l => { heard.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            heard.Clear();
            lua.DoString($"local host = GetPlayerByID({CharId}) local visitor = GetPlayerByID({VisitorId})\n" + code, "=verify_moghouse");
            return string.Join(",", heard);
        }
        var npc = zone.ZoneInfoOf(238).Npcs.First(n => (n.Id >> 12 & 0xFFF) == 238);
        Check("visitMogHouse without a party: refused", "false", Lua($"report(tostring(visitor:visitMogHouse({CharId}, GetNPCByID({npc.Id}))))"));
        // the host invites the visitor (0x06E), who accepts (0x074)
        SendAs(s, 0x06E, 12, buf => { BitConverter.GetBytes(VisitorId).CopyTo(buf, 4); BitConverter.GetBytes(v.TargId).CopyTo(buf, 8); });
        SendAs(vs, 0x074, 8, buf => buf[4] = 1);
        Check("the party formed", $"{CharId}",
              Sql(db, "SELECT partyid FROM accounts_parties WHERE charid = ?", BoundValue.Of(VisitorId)) is var pr && pr != null && pr.Next() ? pr.Get<uint>("partyid").ToString() : "-");
        Check("visitMogHouse to a closed mog house: refused", "false", Lua($"report(tostring(visitor:visitMogHouse({CharId}, GetNPCByID({npc.Id}))))"));
        Is(MyroomIsKind.Open);
        Check("visitMogHouse to itself: refused", "false", Lua($"report(tostring(visitor:visitMogHouse({VisitorId}, GetNPCByID({npc.Id}))))"));
        Check("visitMogHouse: true, into the host's mog house, the NPC kept", $"true/{CharId}/{npc.Id}",
              Lua($"report(tostring(visitor:visitMogHouse({CharId}, GetNPCByID({npc.Id}))))") + $"/{v.MoghouseId}/" +
              DbScalar($"SELECT value FROM char_vars WHERE charid = {VisitorId} AND varname = 'mh-visit-npc' AND ? > 0"));
        zone.Tick();
        Check("the visitor's zone change", $"238:{CharId}", DbPos(VisitorId));
        Safe(8).Extra[1] |= 0x40; // the mannequin installed again (in memory), for the visitor's updateMannequins
        Safe(8).Extra[18] = 1;
        LoginVisitor();
        int installed = c.Items.Count(i => (i.Location == Container.MogSafe || i.Location == Container.MogSafe2) && i.Installed && i.Slot <= c.StorageSize[(int)i.Location]);
        var safeAttrs = visitorLogin.FindAll(p => Opcode(p) == 0x020 && (p.Buffer[14] == 1 || p.Buffer[14] == 9));
        Check("the visitor in: in the host's mog house, among its visitors",$"{CharId}/{VisitorId}",
              $"{v.MoghouseId}/" + Lua("for _, p in ipairs(host:getMogHouseVisitors()) do report(p:getID()) end"));
        Check("the visitor's safes: the host's installed furniture, slots 1..N", $"{installed}/True",
              $"{safeAttrs.Count}/{safeAttrs.Select((p, i) => p.Buffer[15] == i + 1 || p.Buffer[14] == 9).All(x => x)}");
        // updateMannequins for a visitor: each installed mannequin at its renumbered slot among the host's furniture
        string MannequinSlots(IEnumerable<string> slots) => string.Join(",", slots);
        var hostFurniture = new[] { Container.MogSafe, Container.MogSafe2 }.SelectMany(loc =>
            c.Items.Where(i => i.Location == loc && i.Installed && i.Slot <= c.StorageSize[(int)loc]).OrderBy(i => i.Slot)
             .Select((i, n) => (i, n))).Where(t => ZoneHost.IsMannequin(t.i)).Select(t => $"{(byte)t.i.Location}.{t.n + 1}").ToList();
        Check("updateMannequins: the visitor gets the host's installed mannequins at their renumbered slots",
              MannequinSlots(hostFurniture),
              MannequinSlots(visitorLogin.FindAll(p => Opcode(p) == 0x026).Select(p => $"{p.Buffer[5]}.{p.Buffer[6]}")));
        Check("updateMannequins: the visitor sees at least one mannequin", true, hostFurniture.Count > 0);
        // SpawnConditionalNPCs for a visitor reads the host (moghouse().host()): the 1F Moogle is shown while the
        // host's mhflag has no 2F bit, hidden once it has, whatever the visitor's own mhflag
        ushort hostFlag = c.MhFlag;
        string VisitorMoogle()
        {
            vs.PacketList.Clear();
            SendAs(vs, 0x01A, 28, b =>
            {
                BitConverter.GetBytes(VisitorId).CopyTo(b, C2sAction.OffUniqueNo);
                BitConverter.GetBytes(v.TargId).CopyTo(b, C2sAction.OffActIndex);
                BitConverter.GetBytes((ushort)ActionId.SendResRdy).CopyTo(b, C2sAction.OffActionID);
            });
            var moogle = vs.PacketList.Where(p => Opcode(p) == 0x00E && CondNpc(p) == "Moogle").ToList();
            return moogle.Count == 1 ? moogle[0].Buffer[0x20].ToString() : $"{moogle.Count} packets";
        }
        c.MhFlag = (ushort)(hostFlag & ~0x40);
        v.MhFlag = 0x40;
        Check("a visitor's conditional Moogle: the host on 1F (the visitor's own 2F bit set): shown", "0",
              VisitorMoogle());
        c.MhFlag = (ushort)(hostFlag | 0x40);
        v.MhFlag = 0;
        Check("a visitor's conditional Moogle: the host on 2F: hidden", $"{NpcEntity.StatusDisappear}",
              VisitorMoogle());
        c.MhFlag = hostFlag;
        vs.PacketList.Clear();
        log.Clear();
        MapRect(vs, "zmrq", 0, 125);
        Check("a visitor cannot change floors", $"True/{CharId}", $"{log.Exists(l => l.Contains("Mog House visitor Mogvisit requested exit mode 125"))}/{v.MoghouseId}");
        Clear();
        Is(MyroomIsKind.Close);
        Check("the host closes: the visitor expelled back to its NPC's zone, the visitors cleared", "0/0/",
              $"{v.MoghouseId}/{v.X}/" + Lua("for _, p in ipairs(host:getMogHouseVisitors()) do report(p:getID()) end"));
        zone.Tick();
        Check("the expelled visitor's zone change", "238:0", DbPos(VisitorId));
        zone.OnCharTimeout(vs, false);

        Console.WriteLine("0x05E a city exit with its quest:");
        c.MhFlag = 0x04;
        Clear();
        MapRect(s, "zmrq", 3, 3);
        zone.Tick();
        Check("Windurst's exit 3: Port Windurst, out of the mog house", "0/240:0", $"{c.MoghouseId}/{DbPos(CharId)}");
    }
}
