using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XI.Crypto;
using XI.Persistence;
using XI.Search;

// XI.Verify.Search.Live -- end-to-end gate for the ported xi_search: a real SearchEngine (TCP on an ephemeral port) in
// front of a real MariaDB (docker xi-live-db, host:3307, xidb schema), driven by a client that encrypts requests the way
// the game does and decrypts and checks the replies.
//
//   XI.Verify.Search.Live [dbPort]                          the gate
//   XI.Verify.Search.Live [dbPort] golden <cppSearchPort>   also sends the requests to a running C++ xi_search (run it
//                                                           with XI_SEARCH_EXPIRE_AUCTIONS=0) and compares the replies
//                                                           byte for byte
//
// Accounts and characters 996046-996049 only (names "PrfSea*"). Exit codes: 0 = all checks passed; 1 = a check failed;
// 3 = DB not reachable.
internal static class Program
{
    private static int _pass, _fail;

    private const uint A = 996046, B = 996047, C = 996048, D = 996049;
    private static readonly uint[] Chars = { A, B, C, D };
    private const uint Linkshell = 0x7F3E9601;
    private static ushort _item;     // an auction house item nobody else lists
    private static byte _category;

    private static readonly byte[] KeyBase = { 0x30, 0x73, 0x3D, 0x6D, 0x3C, 0x31, 0x49, 0x5A, 0x32, 0x7A, 0x42, 0x43, 0x63, 0x38, 0x7B, 0x7E };

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static void CheckTrue(string name, bool cond) => Check(name, true, cond);

    private static ResultSet Sql(IDatabase db, string query, params object[] args)
    {
        var bound = args.Select(a => a switch
        {
            uint v => BoundValue.Of(v),
            int v => BoundValue.Of(v),
            ushort v => BoundValue.Of(v),
            byte v => BoundValue.Of(v),
            string v => BoundValue.Of(v),
            byte[] v => BoundValue.Of(v),
            _ => throw new NotSupportedException(),
        }).ToArray();
        return db.Execute(query, bound);
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
        foreach (var id in Chars)
        {
            Sql(db, "DELETE FROM auction_house WHERE seller = ?", id);
            Sql(db, "DELETE FROM delivery_box WHERE charid = ?", id);
            foreach (var t in CharidTables(db)) Sql(db, $"DELETE FROM `{t}` WHERE charid = ?", id);
            Sql(db, "DELETE FROM chars WHERE charid = ?", id);
            Sql(db, "DELETE FROM accounts WHERE id = ?", id);
        }
    }

    private static void Setup(IDatabase db)
    {
        // name, zone, prevzone, nation, race, mjob, mlvl, sjob, slvl, rank, settings, seacom, ls rank
        var rows = new (uint Id, string Name, ushort Zone, ushort Prev, byte Nation, byte Race, byte Mjob, byte Mlvl, byte Sjob, byte Slvl, byte Rank, uint Settings, byte Seacom, byte LsRank)[]
        {
            (A, "PrfSeaAlpha", 230, 0, 0, 1, 1, 75, 2, 37, 6, 0x0001 | 1u << 25, 0x21, 1), // invite, mentor; a comment
            (B, "PrfSeaBeta", 231, 0, 1, 7, 3, 50, 5, 25, 4, 0x0002 | 1u << 29, 0, 2),      // away, recruiting
            (C, "PrfSeaGamma", 0, 232, 2, 8, 23, 30, 0, 0, 3, 0, 0, 3),                     // in transit (zone 0), monstrosity
            (D, "PrfSeaDelta", 230, 0, 0, 3, 1, 60, 0, 0, 5, 0x0004, 0, 0),                 // anonymous
        };
        foreach (var r in rows)
        {
            Sql(db, "INSERT INTO accounts(id,login,password) VALUES (?,?,?)", r.Id, $"prf{r.Id}", "x");
            Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,settings,languages) VALUES (?,?,?,?,?,0,0,0,?,0,?,1)",
                r.Id, r.Id, r.Name, r.Zone, r.Prev, r.Nation, r.Settings);
            Sql(db, "INSERT INTO char_look(charid, race) VALUES (?, ?)", r.Id, r.Race);
            Sql(db, "INSERT INTO char_stats(charid, mjob, mlvl, sjob, slvl) VALUES (?, ?, ?, ?, ?)", r.Id, r.Mjob, r.Mlvl, r.Sjob, r.Slvl);
            // char_insert made the char_profile row
            Sql(db, "UPDATE char_profile SET rank_sandoria = ?, rank_bastok = ?, rank_windurst = ? WHERE charid = ?", r.Rank, r.Rank, r.Rank, r.Id);
            Sql(db, "INSERT INTO char_flags(charid) VALUES (?)", r.Id);
            Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port,linkshellid1,linkshellrank1,seacom_type,seacom_message) VALUES (?,?,0,0,?,?,?,?)",
                r.Id, r.Id, r.LsRank != 0 ? Linkshell : 0u, (ushort)r.LsRank, r.Seacom, r.Seacom != 0 ? Encoding.ASCII.GetBytes("Looking for a party") : Array.Empty<byte>());
        }
        Sql(db, "UPDATE accounts_sessions SET seacom_message = NULL WHERE charid = ?", B);
        // A leads a party with B
        Sql(db, "INSERT INTO accounts_parties(charid, partyid, partyflag, allianceid) VALUES (?, ?, 1, 0), (?, ?, 0, 0)", A, A, B, A);

        var item = Sql(db, "SELECT b.itemid, b.aH FROM item_basic b WHERE b.aH > 0 AND b.stackSize = 12 AND NOT EXISTS (SELECT 1 FROM auction_house a WHERE a.itemid = b.itemid) ORDER BY b.itemid LIMIT 1");
        item.Next();
        _item = item.Get<ushort>("itemid");
        _category = item.Get<byte>("aH");
        // 2 single and 1 stack listing, 3 sales
        Sql(db, "INSERT INTO auction_house(itemid, stack, seller, seller_name, date, price) VALUES (?, 0, ?, 'PrfSeaAlpha', 2000000000, 100), (?, 0, ?, 'PrfSeaAlpha', 2000000000, 110), (?, 1, ?, 'PrfSeaBeta', 2000000000, 900)",
            _item, A, _item, A, _item, B);
        for (uint i = 0; i < 3; i++)
        {
            Sql(db, "INSERT INTO auction_house(itemid, stack, seller, seller_name, date, price, buyer, buyer_name, sale, sell_date) VALUES (?, 0, ?, 'PrfSeaBeta', 1000, 50, ?, 'PrfSeaGamma', ?, ?)",
                _item, B, C, 40 + i, 1700000000 + i);
        }
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
            Setup(db);
            if (argv.Length >= 3 && argv[1] == "golden") Golden(db, int.Parse(argv[2])).GetAwaiter().GetResult();
            else RunAsync(db).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FAIL unhandled {ex}");
        }
        finally
        {
            Cleanup(db);
        }

        Console.WriteLine($"RESULT: {_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    // ---------------------------------------------------------------- client side

    // a request: payload from 0x10, the 4 bytes before the hash folded into the reply key
    private static byte[] Request(byte type, byte[] payload, uint key)
    {
        int length = (0x10 + payload.Length + 4 + 20 + 3) / 4 * 4;
        length = Math.Max(length, 28);
        var b = new byte[length];
        BinaryPrimitives.WriteUInt16LittleEndian(b, (ushort)length);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4), 0x46465849);
        b[0x0B] = type;
        payload.CopyTo(b, 0x10);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(length - 0x18), 0xA5A5A5A5);
        MD5.HashData(b.AsSpan(8, length - 28)).CopyTo(b, length - 20);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(length - 4), key);
        return b;
    }

    private static Blowfish Cipher(byte[] key)
    {
        var bf = new Blowfish();
        bf.Init(MD5.HashData(key));
        return bf;
    }

    private static byte[] Encrypt(byte[] plain)
    {
        var b = (byte[])plain.Clone();
        int length = b.Length;
        var bf = Cipher(KeyBase.Concat(b.Skip(length - 4).Take(4)).ToArray());
        int tmp = (length - 12) / 4;
        tmp -= tmp % 2;
        for (int i = 0; i < tmp; i += 2)
        {
            uint l = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan((i + 2) * 4)), r = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan((i + 3) * 4));
            bf.Encipher(ref l, ref r);
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan((i + 2) * 4), l);
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan((i + 3) * 4), r);
        }
        return b;
    }

    private sealed class Reply
    {
        public byte[] Raw;   // as received
        public byte[] Plain; // decrypted
        public bool HashOk;
        public bool KeyEchoed;
    }

    // sends one request; the replies until the server stays quiet for `quietMs`
    private static async Task<List<Reply>> Send(int port, byte[] plain, int quietMs = 700)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, port);
        var s = tcp.GetStream();
        try { await s.WriteAsync(Encrypt(plain)); }
        catch (System.IO.IOException) { return new List<Reply>(); }
        var replyKey = KeyBase.Concat(plain.Skip(plain.Length - 4).Take(4)).Concat(plain.Skip(plain.Length - 0x18).Take(4)).ToArray();
        var replies = new List<Reply>();
        var acc = new List<byte>();
        var buf = new byte[8192];
        while (true)
        {
            using var cts = new CancellationTokenSource(quietMs);
            int n;
            try { n = await s.ReadAsync(buf, cts.Token); }
            catch (OperationCanceledException) { break; }
            catch (System.IO.IOException) { break; } // closed by the server
            if (n <= 0) break;
            acc.AddRange(buf.Take(n));
            while (acc.Count >= 2)
            {
                int size = BinaryPrimitives.ReadUInt16LittleEndian(acc.ToArray());
                if (size < 28 || acc.Count < size) break;
                var raw = acc.Take(size).ToArray();
                acc.RemoveRange(0, size);
                var p = (byte[])raw.Clone();
                var bf = Cipher(replyKey);
                int tmp = (byte)((size - 12) / 4);
                tmp -= tmp % 2;
                for (int i = 0; i < tmp; i += 2)
                {
                    uint l = BinaryPrimitives.ReadUInt32LittleEndian(p.AsSpan((i + 2) * 4)), r = BinaryPrimitives.ReadUInt32LittleEndian(p.AsSpan((i + 3) * 4));
                    bf.Decipher(ref l, ref r);
                    BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan((i + 2) * 4), l);
                    BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan((i + 3) * 4), r);
                }
                replies.Add(new Reply
                {
                    Raw = raw,
                    Plain = p,
                    HashOk = MD5.HashData(p.AsSpan(8, size - 28)).AsSpan().SequenceEqual(p.AsSpan(size - 20, 16)),
                    KeyEchoed = p.AsSpan(size - 4, 4).SequenceEqual(plain.AsSpan(plain.Length - 4, 4)),
                });
            }
        }
        return replies;
    }

    // the query block of a /sea request
    private sealed class Query
    {
        private readonly byte[] _bits = new byte[256];
        private int _at;
        public readonly List<byte> Trailer = new();

        private Query Put(ulong v, int n)
        {
            _at = Bits.Pack(_bits, v, _at, n);
            return this;
        }

        private Query Entry(SearchType type, bool present = true) => Put((ulong)type, 5).Put(0, 1).Put(present ? 1u : 0u, 1);

        public Query Name(string name)
        {
            Entry(SearchType.Name).Put((ulong)name.Length, 5);
            foreach (var c in name) Put(c, 7);
            return this;
        }

        public Query Area(ushort zone) => Entry(SearchType.Area).Put(zone, 10);
        public Query Nation(byte n) => Entry(SearchType.Nation).Put(n, 2);
        public Query Job(byte j) => Entry(SearchType.Job).Put(j, 5);
        public Query Level(byte lo, byte hi) => Entry(SearchType.Level).Put(lo, 8).Put(hi, 8);
        public Query Race(byte r) => Entry(SearchType.Race).Put(r, 4);
        public Query Rank(byte lo, byte hi) => Entry(SearchType.Rank).Put(lo, 8).Put(hi, 8);
        public Query Flags1(ushort f) => Entry(SearchType.Flags1).Put(f, 16);
        public Query Flags2(uint f) => Put((ulong)SearchType.Flags2, 5).Put(f, 32);
        public Query Comment(uint t) => Put((ulong)SearchType.Comment, 5).Put(t, 32);
        public Query Ls(uint id) => Put((ulong)SearchType.Linkshell, 5).Put(id, 32);
        public Query Friends(params uint[] ids)
        {
            Put((ulong)SearchType.Friend, 5);
            Trailer.AddRange(BitConverter.GetBytes((ushort)ids.Length));
            foreach (var id in ids) Trailer.AddRange(BitConverter.GetBytes(id));
            return this;
        }
        public Query RequestList(params string[] names)
        {
            Put((ulong)SearchType.RequestList, 5);
            Trailer.AddRange(BitConverter.GetBytes((ushort)names.Length));
            foreach (var n in names) Trailer.AddRange(Encoding.ASCII.GetBytes(n.PadRight(16, '\0')));
            return this;
        }

        // size, the bit stream with a spare zero byte, then any trailer
        public byte[] Payload()
        {
            int size = (_at + 7) / 8 + 1;
            return new[] { (byte)size }.Concat(_bits.Take(size)).Concat(Trailer).ToArray();
        }
    }

    private static byte[] Sea(Query q, uint key = 0x11223344) => Request(TcpRequestType.Search, q.Payload(), key);

    // a search list entry's fields by search type
    private sealed class Entity
    {
        public string Name;
        public int Zone = -1, Nation = -1, Mjob = -1, Sjob = -1, Mlvl = -1, Slvl = -1, Race = -1, Rank = -1, Flags1 = -1, Id = -1, Language = -1;
        public long Flags2 = -1, Comment = -1, Ls1 = -1, LsRank1 = -1;
    }

    private static List<Entity> Entities(byte[] p)
    {
        var list = new List<Entity>();
        int end = BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(8));
        int at = 24;
        while (at < end)
        {
            int size = p[at];
            int bit = (at + 1) * 8, stop = (at + 1 + size) * 8;
            var e = new Entity();
            ulong U(int n)
            {
                ulong v = Bits.Unpack(p, 0, bit, n);
                bit += n;
                return v;
            }
            while (bit + 5 <= stop)
            {
                var type = (SearchType)U(5);
                switch (type)
                {
                    case SearchType.Name:
                        int len = (int)U(4);
                        var sb = new StringBuilder();
                        for (int i = 0; i < len; i++) sb.Append((char)U(7));
                        e.Name = sb.ToString();
                        break;
                    case SearchType.Area: e.Zone = (int)U(10); break;
                    case SearchType.Nation: e.Nation = (int)U(2); break;
                    case SearchType.Job: e.Mjob = (int)U(5); e.Sjob = (int)U(5); break;
                    case SearchType.Level: e.Mlvl = (int)U(8); e.Slvl = (int)U(8); break;
                    case SearchType.Race: e.Race = (int)U(4); break;
                    case SearchType.Rank: e.Rank = (int)U(8); break;
                    case SearchType.Flags1: e.Flags1 = (int)U(16); break;
                    case SearchType.Id: e.Id = (int)U(20); break;
                    case SearchType.LinkshellRank: e.LsRank1 = (long)U(8); U(16); e.Ls1 = (long)U(32); U(64); break;
                    case SearchType.Unknown0E: U(32); break;
                    case SearchType.Comment: e.Comment = (long)U(32); break;
                    case SearchType.Flags2: e.Flags2 = (long)U(32); break;
                    case SearchType.Language: e.Language = (int)U(16); bit = stop; break; // the last field, padding follows
                    default: bit = stop; break;
                }
            }
            list.Add(e);
            at += 1 + size;
        }
        return list;
    }

    private static string Names(List<Reply> replies) => string.Join(",", replies.SelectMany(r => Entities(r.Plain)).Select(e => e.Name));

    // ---------------------------------------------------------------- the gate

    private static async Task RunAsync(IDatabase db)
    {
        var log = new List<string>();
        using var engine = new SearchEngine(db, new SearchSettings { ExpireAuctions = false }, IPAddress.Loopback, 0, s => { lock (log) log.Add(s); });
        engine.Start();
        int port = engine.Port;
        CheckTrue("listening on an ephemeral port", port != 0 && port != 54002);

        // ---- ID list (the friend list refresh): sorted by name, the transit char shows its previous zone
        var r = await Send(port, Request(TcpRequestType.IdList, new byte[] { 4, 0 }.Concat(Chars.SelectMany(BitConverter.GetBytes)).ToArray(), 0x01020304));
        Check("id list: one reply", 1, r.Count);
        CheckTrue("reply md5 valid and key echoed", r[0].HashOk && r[0].KeyEchoed);
        Check("reply header: size, IXFF", $"{r[0].Raw.Length}/46465849", $"{BinaryPrimitives.ReadUInt16LittleEndian(r[0].Plain)}/{BinaryPrimitives.ReadUInt32LittleEndian(r[0].Plain.AsSpan(4)):X8}");
        Check("search list: type 0x80, final, total 4", "128/128/4", $"{r[0].Plain[0x0B]}/{r[0].Plain[0x0A]}/{BinaryPrimitives.ReadUInt16LittleEndian(r[0].Plain.AsSpan(0x0E))}");
        var es = Entities(r[0].Plain);
        Check("names in order", "PrfSeaAlpha,PrfSeaBeta,PrfSeaDelta,PrfSeaGamma", string.Join(",", es.Select(e => e.Name)));
        var a = es[0];
        Check("A: zone/nation/jobs/levels/race/rank", "230/0/1/2/75/37/1/6", $"{a.Zone}/{a.Nation}/{a.Mjob}/{a.Sjob}/{a.Mlvl}/{a.Slvl}/{a.Race}/{a.Rank}");
        Check("A: flags mentor|leader|comment|party|invite", 0x0001 | 0x0008 | 0x0010 | 0x2000 | 0x8000, a.Flags1);
        Check("A: id, comment type, language", $"{A}/{0x21}/1", $"{a.Id}/{a.Comment}/{a.Language}");
        var b = es[1];
        Check("B: away | party | recruiting (flags2 32 bits)", $"{0x0100 | 0x2000}/{0x0100 | 0x2000 | 0x10000000}", $"{b.Flags1}/{b.Flags2}");
        var dd = es[2];
        Check("D: anonymous hides nation/job/level/race/rank", "-1/-1/-1/-1/-1/16384", $"{dd.Nation}/{dd.Mjob}/{dd.Mlvl}/{dd.Race}/{dd.Rank}/{dd.Flags1}");
        var c = es[3];
        Check("C: zone 0 shows the previous zone; monstrosity jobs show 0", "232/0/0", $"{c.Zone}/{c.Mjob}/{c.Sjob}");

        // ---- /sea filters (all scoped by name to these characters)
        r = await Send(port, Sea(new Query().Name("PrfSea")));
        Check("name prefix (case-insensitive)", "PrfSeaAlpha,PrfSeaBeta,PrfSeaDelta,PrfSeaGamma", Names(r));
        r = await Send(port, Sea(new Query().Name("prfseab")));
        Check("name prefix, lower case", "PrfSeaBeta", Names(r));
        r = await Send(port, Sea(new Query().Name("PrfSea").Job(1)));
        Check("job WAR: anon D filtered out", "PrfSeaAlpha", Names(r));
        r = await Send(port, Sea(new Query().Name("PrfSea").Level(40, 60)));
        Check("level 40-60", "PrfSeaBeta", Names(r));
        r = await Send(port, Sea(new Query().Name("PrfSea").Area(230)));
        Check("area 230", "PrfSeaAlpha,PrfSeaDelta", Names(r));
        r = await Send(port, Sea(new Query().Name("PrfSea").Area(232)));
        Check("area matches pos_prevzone when in transit", "PrfSeaGamma", Names(r));
        r = await Send(port, Sea(new Query().Name("PrfSea").Race(3)));
        Check("race mithra", "PrfSeaBeta", Names(r));
        r = await Send(port, Sea(new Query().Name("PrfSea").Nation(2)));
        Check("nation windurst", "PrfSeaGamma", Names(r));
        r = await Send(port, Sea(new Query().Name("PrfSea").Rank(5, 6)));
        Check("rank 5-6 (anon excluded)", "PrfSeaAlpha", Names(r));
        r = await Send(port, Sea(new Query().Name("PrfSea").Flags1(0x0100)));
        Check("away flag", "PrfSeaBeta", Names(r));
        r = await Send(port, Sea(new Query().Name("PrfSea").Flags2(0x10000000)));
        Check("accepting requests flag", "PrfSeaBeta", Names(r));
        r = await Send(port, Sea(new Query().Name("PrfSea").Comment(0x20)));
        Check("comment type (& 0xF0)", "PrfSeaAlpha", Names(r));
        r = await Send(port, Sea(new Query().Name("PrfSea").Ls(Linkshell)));
        Check("linkshell", "PrfSeaAlpha,PrfSeaBeta,PrfSeaGamma", Names(r));
        r = await Send(port, Sea(new Query().Name("PrfSea").Ls(0)));
        Check("linkshell 0 matches nobody: an empty final list", "1//128/0", $"{r.Count}/{Names(r)}/{r[0].Plain[0x0A]}/{BinaryPrimitives.ReadUInt16LittleEndian(r[0].Plain.AsSpan(0x0E))}");
        r = await Send(port, Sea(new Query().Friends(B, D)));
        Check("friends search: the trailing ids", "PrfSeaBeta,PrfSeaDelta", Names(r));
        r = await Send(port, Sea(new Query().RequestList("PrfSeaGamma", "Nobody")));
        Check("request list: the trailing names", "PrfSeaGamma", Names(r));
        Sql(db, "UPDATE char_flags SET gmHiddenEnabled = 1 WHERE charid = ?", C);
        r = await Send(port, Sea(new Query().Name("PrfSea")));
        Check("a hidden GM is not listed", "PrfSeaAlpha,PrfSeaBeta,PrfSeaDelta", Names(r));
        Sql(db, "UPDATE char_flags SET gmHiddenEnabled = 0 WHERE charid = ?", C);

        // ---- party and linkshell lists, search comment
        var group = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(group, A);
        r = await Send(port, Request(TcpRequestType.GroupList, group, 7));
        Check("party list: type 0x82, count 2", "130/2", $"{r[0].Plain[0x0B]}/{r[0].Plain[0x0E]}");
        es = Entities(r[0].Plain);
        Check("party members", "PrfSeaAlpha,PrfSeaBeta", string.Join(",", es.Select(e => e.Name)));
        Check("party: leader flag on the party id's char", "8/0", $"{es[0].Flags1 & 8}/{es[1].Flags1 & 8}");
        group = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(group.AsSpan(8), Linkshell);
        r = await Send(port, Request(TcpRequestType.GroupList, group, 8));
        Check("linkshell list: final, total 3", "128/3", $"{r[0].Plain[0x0A]}/{BinaryPrimitives.ReadUInt16LittleEndian(r[0].Plain.AsSpan(0x0E))}");
        es = Entities(r[0].Plain);
        Check("linkshell members and ranks", "PrfSeaAlpha:1,PrfSeaBeta:2,PrfSeaGamma:3", string.Join(",", es.Select(e => $"{e.Name}:{e.LsRank1}")));
        Check("linkshell id in the entry", Linkshell, es[0].Ls1);
        r = await Send(port, Request(TcpRequestType.GroupList, new byte[16], 9));
        Check("no party and no linkshell: no reply", 0, r.Count);

        r = await Send(port, Request(TcpRequestType.SearchComment, BitConverter.GetBytes(A), 10));
        Check("search comment: 204 bytes, type 0x88", "204/136", $"{r[0].Raw.Length}/{r[0].Plain[0x0B]}");
        Check("comment text, space padded", "Looking for a party" + new string(' ', 104), Encoding.ASCII.GetString(r[0].Plain, 0x1E, 123));
        r = await Send(port, Request(TcpRequestType.SearchComment, BitConverter.GetBytes(B), 11));
        Check("no comment: no reply", 0, r.Count);

        // ---- auction house
        var ah = new byte[0x30];
        ah[0x06] = _category;
        r = await Send(port, Request(TcpRequestType.AhRequest, ah, 12));
        CheckTrue("ah list: type 0x95 replies", r.Count > 0 && r.All(x => x.Plain[0x0B] == 0x95 && x.HashOk));
        int total = BinaryPrimitives.ReadUInt16LittleEndian(r[0].Plain.AsSpan(0x0E));
        Check("ah list: one reply per 20 items", (total + 19) / 20 == 0 ? 1 : (total + 19) / 20, r.Count);
        var entries = r.SelectMany(x => Enumerable.Range(0, (BinaryPrimitives.ReadUInt16LittleEndian(x.Plain.AsSpan(8)) - 0x18) / 10)
                                                  .Select(i => x.Plain.AsSpan(0x18 + 10 * i, 10).ToArray())).ToList();
        Check("ah list: every item listed once", total, entries.Count);
        var mine = entries.First(e => BinaryPrimitives.ReadUInt16LittleEndian(e) == _item);
        Check("our item: 2 single, 1 stack", "2/1", $"{BinaryPrimitives.ReadUInt32LittleEndian(mine.AsSpan(2))}/{BinaryPrimitives.ReadUInt32LittleEndian(mine.AsSpan(6))}");
        CheckTrue("the last reply is final", r[^1].Plain[0x0A] == 0x80);

        var hist = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(hist.AsSpan(2), _item);
        r = await Send(port, Request(TcpRequestType.AhHistorySingle, hist, 13));
        Check("history: type 0x85, size", $"133/{0x20 + 3 * 40 + 28}", $"{r[0].Plain[0x0B]}/{r[0].Raw.Length}");
        var p = r[0].Plain;
        Check("history: item, single amount, category", $"{_item}/2/{_category}",
              $"{BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(0x18))}/{BinaryPrimitives.ReadUInt32LittleEndian(p.AsSpan(0x1A))}/{BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(0x1E))}");
        Check("history: oldest first, prices", "40,41,42", string.Join(",", Enumerable.Range(0, 3).Select(i => BinaryPrimitives.ReadUInt32LittleEndian(p.AsSpan(0x20 + 40 * i)))));
        Check("history: seller and buyer names", "PrfSeaBeta/PrfSeaGamma", $"{Encoding.ASCII.GetString(p, 0x28, 10)}/{Encoding.ASCII.GetString(p, 0x38, 11)}");
        hist[5] = 1;
        r = await Send(port, Request(TcpRequestType.AhHistoryStack, hist, 14));
        Check("stack history: no sales, the stack amount", "1/60", $"{BinaryPrimitives.ReadUInt32LittleEndian(r[0].Plain.AsSpan(0x1A))}/{r[0].Raw.Length}");

        // ---- malformed requests: no reply
        var bad = Sea(new Query().Name("PrfSea"));
        bad[0x14] ^= 1;
        r = await Send(port, bad);
        Check("a request whose md5 fails: no reply", 0, r.Count);
        var shortReq = Request(TcpRequestType.Search, Array.Empty<byte>(), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(shortReq, (ushort)(shortReq.Length + 4));
        r = await Send(port, shortReq);
        Check("a size that disagrees with the read: no reply", 0, r.Count);
        r = await Send(port, Request(0x1F, new byte[8], 2));
        Check("an unknown type: no reply", 0, r.Count);
        lock (log) CheckTrue("logged the requests", log.Any(l => l.StartsWith("Search Request: ID_LIST (1)")));

        // ---- AH expiry, scoped to these sellers (ScopedDb) so nobody else's listings move
        var loader = new SearchDataLoader(new ScopedDb(db), s => { lock (log) log.Add(s); });
        loader.Now = () => 2000000000u + 2 * 86400u;
        loader.ExpireAHItems(3);
        var left = Sql(db, "SELECT COUNT(*) AS n FROM auction_house WHERE seller IN (?, ?) AND buyer_name IS NULL", A, B);
        CheckTrue("expiry: listings younger than the cutoff stay", left.Next() && left.Get<uint>("n") == 3);
        loader.Now = () => 2000000000u + 4 * 86400u;
        loader.ExpireAHItems(3);
        left = Sql(db, "SELECT COUNT(*) AS n FROM auction_house WHERE seller IN (?, ?) AND buyer_name IS NULL", A, B);
        CheckTrue("expiry: the 3 open listings are gone", left.Next() && left.Get<uint>("n") == 0);
        var sold = Sql(db, "SELECT COUNT(*) AS n FROM auction_house WHERE seller = ? AND buyer_name IS NOT NULL", B);
        CheckTrue("expiry: sales stay", sold.Next() && sold.Get<uint>("n") == 3);
        var box = Sql(db, "SELECT charid, charname, box, slot, itemid, quantity, sender FROM delivery_box WHERE charid IN (?, ?) ORDER BY charid, slot", A, B);
        var got = new List<string>();
        while (box.Next()) got.Add($"{box.Get<uint>("charid") - A}:{box.Get<string>("charname")}:{box.Get<byte>("box")}:{box.Get<byte>("slot")}:{box.Get<ushort>("itemid") == _item}:{box.Get<uint>("quantity")}:{box.Get<string>("sender")}");
        Check("expiry: items back in the sellers' delivery boxes (a stack is 12)", "0:PrfSeaAlpha:1:8:True:1:AH-Jeuno,0:PrfSeaAlpha:1:9:True:1:AH-Jeuno,1:PrfSeaBeta:1:8:True:12:AH-Jeuno", string.Join(",", got));
        lock (log) CheckTrue("expiry logged", log.Contains("Sent 3 expired auction house listings back to sellers"));

        // ---- connection limit: whitelisted loopback is not counted; otherwise the 6th from one address is closed
        using (var limited = new SearchEngine(db, new SearchSettings { ExpireAuctions = false }, IPAddress.Loopback, 0, _ => { }))
        {
            limited.Start();
            var held = new List<TcpClient>();
            for (int i = 0; i < 5; i++)
            {
                var t = new TcpClient();
                await t.ConnectAsync(IPAddress.Loopback, limited.Port);
                held.Add(t);
            }
            await Task.Delay(200);
            r = await Send(limited.Port, Sea(new Query().Name("PrfSeaA")));
            Check("6th connection from one address: closed without a reply", 0, r.Count);
            foreach (var t in held) t.Dispose();
            await Task.Delay(200);
            r = await Send(limited.Port, Sea(new Query().Name("PrfSeaA")));
            Check("after they close: served", "PrfSeaAlpha", Names(r));
        }
        using (var open = new SearchEngine(db, new SearchSettings { ExpireAuctions = false, AccessWhitelist = new HashSet<string> { "127.0.0.1" } }, IPAddress.Loopback, 0, _ => { }))
        {
            open.Start();
            var held = new List<TcpClient>();
            for (int i = 0; i < 6; i++)
            {
                var t = new TcpClient();
                await t.ConnectAsync(IPAddress.Loopback, open.Port);
                held.Add(t);
            }
            r = await Send(open.Port, Sea(new Query().Name("PrfSeaA")));
            Check("whitelisted address: not limited", "PrfSeaAlpha", Names(r));
            foreach (var t in held) t.Dispose();
        }
        Check("whitelist from settings/default/search.lua", true, SearchSettings.ReadWhitelist(FindRoot()).Contains("127.0.0.1"));
    }

    // the expiry's listing query limited to this test's sellers; everything else passes through
    private sealed class ScopedDb : IDatabase
    {
        private readonly IDatabase _db;
        public ScopedDb(IDatabase db) => _db = db;
        public ResultSet Execute(string query, IReadOnlyList<BoundValue> parameters) =>
            _db.Execute(query.StartsWith("SELECT T0.id,T0.itemid") ? query + $" AND T0.seller IN ({string.Join(", ", Chars)})" : query, parameters);
        public ResultSet ExecuteBulk(string query, IReadOnlyList<BoundValue> parameters) => _db.ExecuteBulk(query, parameters);
        public string Schema => _db.Schema;
        public string Version => _db.Version;
        public string DriverVersion => _db.DriverVersion;
        public bool Transaction(Fn transactionFn) => _db.Transaction(transactionFn);
    }

    private static string FindRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir, "settings", "default", "search.lua")))
        {
            dir = System.IO.Path.GetDirectoryName(dir);
        }
        return dir ?? throw new System.IO.DirectoryNotFoundException("settings/default/search.lua not found above " + AppContext.BaseDirectory);
    }

    // ---------------------------------------------------------------- golden: the same requests to the C++ server

    private static async Task Golden(IDatabase db, int cppPort)
    {
        using var engine = new SearchEngine(db, new SearchSettings { ExpireAuctions = false }, IPAddress.Loopback, 0, _ => { });
        engine.Start();

        async Task Same(string name, byte[] request)
        {
            var cs = await Send(engine.Port, request);
            var cpp = await Send(cppPort, request);
            Check($"golden {name}", string.Join("|", cpp.Select(x => Convert.ToHexString(x.Raw))), string.Join("|", cs.Select(x => Convert.ToHexString(x.Raw))));
            CheckTrue($"golden {name}: replied", cpp.Count == 0 || cs.Count > 0);
        }

        await Same("id list", Request(TcpRequestType.IdList, new byte[] { 4, 0 }.Concat(Chars.SelectMany(BitConverter.GetBytes)).ToArray(), 0x01020304));
        await Same("name", Sea(new Query().Name("PrfSea")));
        await Same("name lower", Sea(new Query().Name("prfseab")));
        await Same("job", Sea(new Query().Name("PrfSea").Job(1)));
        await Same("level", Sea(new Query().Name("PrfSea").Level(40, 60)));
        await Same("area", Sea(new Query().Name("PrfSea").Area(230)));
        await Same("area in transit", Sea(new Query().Name("PrfSea").Area(232)));
        await Same("race", Sea(new Query().Name("PrfSea").Race(3)));
        await Same("nation", Sea(new Query().Name("PrfSea").Nation(2)));
        await Same("rank", Sea(new Query().Name("PrfSea").Rank(5, 6)));
        await Same("flags1", Sea(new Query().Name("PrfSea").Flags1(0x0100)));
        await Same("flags2", Sea(new Query().Name("PrfSea").Flags2(0x10000000)));
        await Same("comment", Sea(new Query().Name("PrfSea").Comment(0x20)));
        await Same("linkshell", Sea(new Query().Name("PrfSea").Ls(Linkshell)));
        await Same("linkshell 0", Sea(new Query().Name("PrfSea").Ls(0)));
        await Same("friends", Sea(new Query().Friends(B, D)));
        await Same("request list", Sea(new Query().RequestList("PrfSeaGamma", "Nobody")));
        var group = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(group, A);
        await Same("party list", Request(TcpRequestType.GroupList, group, 7));
        group = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(group.AsSpan(8), Linkshell);
        await Same("linkshell list", Request(TcpRequestType.GroupList, group, 8));
        await Same("search comment", Request(TcpRequestType.SearchComment, BitConverter.GetBytes(A), 10));
        await Same("no comment", Request(TcpRequestType.SearchComment, BitConverter.GetBytes(B), 11));
        var hist = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(hist.AsSpan(2), _item);
        await Same("ah history", Request(TcpRequestType.AhHistorySingle, hist, 13));
        hist[5] = 1;
        await Same("ah stack history", Request(TcpRequestType.AhHistoryStack, hist, 14));
        // a category list and its sort keys (level, damage, delay, name); other listings may come and go meanwhile
        foreach (var (cat, keys) in new (byte, byte[])[] { (_category, new byte[0]), (1, new byte[] { 2, 9 }), (1, new byte[] { 5, 6 }), (2, new byte[] { 9, 9, 6 }) })
        {
            var ah = new byte[0x30];
            ah[0x02] = (byte)keys.Length;
            ah[0x06] = cat;
            for (int i = 0; i < keys.Length; i++) ah[0x08 + 8 * i] = keys[i];
            await Same($"ah category {cat} keys [{string.Join(",", keys)}]", Request(TcpRequestType.AhRequest, ah, 12));
        }
        var bad = Sea(new Query().Name("PrfSea"));
        bad[0x14] ^= 1;
        await Same("bad md5", bad);
        await Same("unknown type", Request(0x1F, new byte[8], 2));
    }
}
