// XI.Verify.ZoneIn: the zone-in S2C builders (XI.Zone.LoginPackets) against a real-client capture of the C++ xi_map
// (reference/zonein_ryoo.txt, recorded through XI.MapTap). The character fixture is the capture-time state of the
// test character "Ryoo" (the live DB has moved on since), so this needs no database. The growth tables and zone.yaml
// come from the server's data/ directory, found by walking up from the binary to <repo>/server/data.
//
// Compared per packet: every byte of the declared size except the sync counter (header bytes 2..3).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using XI.Map;
using XI.Zone;

namespace XI.Verify.ZoneIn
{
    internal static class Program
    {
        private static int pass_, fail_;

        private sealed class Datagram
        {
            public string Dir;
            public uint Time;
            public readonly List<byte[]> Packets = new List<byte[]>();
        }

        private static List<Datagram> ParseCapture(string path)
        {
            var list = new List<Datagram>();
            Datagram cur = null;
            foreach (var raw in File.ReadLines(path))
            {
                var t = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (t.Length >= 6 && (t[2] == "S>C" || t[2] == "C>S") && t[4] == "time")
                {
                    cur = new Datagram { Dir = t[2], Time = uint.Parse(t[5], NumberStyles.HexNumber) };
                    list.Add(cur);
                }
                else if (cur != null && t.Length >= 6 && t[1] == "sz" && t[3] == "sync")
                {
                    cur.Packets.Add(Convert.FromHexString(t[5]));
                }
            }
            return list;
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

        private static void Compare(string label, BasicPacket built, byte[] want)
        {
            int size = (int)built.GetSize();
            var got = built.Buffer;
            var diffs = new List<string>();
            if (size != want.Length) diffs.Add($"size {size} != {want.Length}");
            int n = Math.Min(size, want.Length);
            for (int i = 0; i < n; i++)
            {
                if (i == 2 || i == 3) continue;
                if (got[i] != want[i]) diffs.Add($"+{i}(0x{i:X2}) {got[i]:X2}!={want[i]:X2}");
            }
            if (diffs.Count == 0) { pass_++; Console.WriteLine($"  ok   {label}"); return; }
            fail_++;
            Console.WriteLine($"  FAIL {label}: {string.Join(", ", diffs.GetRange(0, Math.Min(diffs.Count, 24)))}{(diffs.Count > 24 ? $" (+{diffs.Count - 24} more)" : "")}");
        }

        // Ryoo at capture time: a fresh Mithra RDM1 in Port Windurst, wearing the starting gear
        private static CharData Fixture(Grades grades)
        {
            var c = new CharData
            {
                Id = 1, Name = "Ryoo", Nation = 2, Zone = 240, PrevZone = 0, Boundary = 0,
                X = -140f, Y = -7f, Z = 172f, Rotation = 32,
                MJob = 5, SJob = 0, UnlockedJobs = 126, PlayTimeSec = 234,
                Settings = 0, ChatFilters1 = 0, ChatFilters2 = 0, JobMasterDisplay = true,
            };
            c.Look.Face = 10; c.Look.Race = 7; c.Look.Size = 1;
            c.Look.Body = 8; c.Look.Hands = 8; c.Look.Legs = 8; c.Look.Feet = 8;
            c.JobLevels[1] = c.JobLevels[2] = c.JobLevels[3] = c.JobLevels[4] = c.JobLevels[5] = c.JobLevels[6] = 1;
            c.MLvl = 1; c.SLvl = 0;

            byte[] sizes = { 30, 50, 0, 50, 0, 0, 0, 80, 80, 50, 80, 80, 80, 80, 80, 80, 80, 10 };
            Array.Copy(sizes, c.StorageSize, sizes.Length);

            c.Items.Add(new Item { Location = Container.Inventory, Slot = 0, ItemId = 65535, Quantity = 10 });
            c.Items.Add(new Item { Location = Container.Inventory, Slot = 1, ItemId = 12757, Quantity = 1 });
            c.Items.Add(new Item { Location = Container.Inventory, Slot = 2, ItemId = 12887, Quantity = 1 });
            c.Items.Add(new Item { Location = Container.Inventory, Slot = 3, ItemId = 13008, Quantity = 1 });
            c.Items.Add(new Item { Location = Container.Inventory, Slot = 4, ItemId = 12636, Quantity = 1 });
            c.Items.Add(new Item { Location = Container.Inventory, Slot = 5, ItemId = 16482, Quantity = 1 });
            c.Items.Add(new Item { Location = Container.Inventory, Slot = 6, ItemId = 4606, Quantity = 1 });
            c.Items.Add(new Item { Location = Container.Inventory, Slot = 7, ItemId = 13496, Quantity = 1 });
            c.Items.Add(new Item { Location = Container.Inventory, Slot = 8, ItemId = 536, Quantity = 1 });
            c.Equip.Add(new EquipEntry { EquipSlot = 5, InvSlot = 4, Container = Container.Inventory });
            c.Equip.Add(new EquipEntry { EquipSlot = 6, InvSlot = 1, Container = Container.Inventory });
            c.Equip.Add(new EquipEntry { EquipSlot = 7, InvSlot = 2, Container = Container.Inventory });
            c.Equip.Add(new EquipEntry { EquipSlot = 8, InvSlot = 3, Container = Container.Inventory });

            // blobs as the capture shows them: Port Windurst visited, the starting key items, an Abyssea quest flag
            c.ZonesVisited[30] = 0x01;
            c.Languages = 2;
            c.Title = 206;
            c.Rank[0] = c.Rank[1] = c.Rank[2] = 1;
            c.HomeZone = 240;
            c.KeyItems[48] = 0x08;
            c.KeyItems[3 * 128 + 25] = 0xFC;
            c.QuestLog[8 * CharData.QuestLogSize + 20] = 0x01;
            c.HasPointsRow = true;

            var abilities = new AbilityTable();
            abilities.Add(5, new AbilityTable.Ability(20, 0, 0));                                    // chainspell
            abilities.Add(5, new AbilityTable.Ability(83, 40, 0));                                   // convert
            abilities.Add(5, new AbilityTable.Ability(247, 50, AbilityTable.AddTypeMainOnly));       // composure
            var loader = new CharLoader(null, grades, abilities);
            loader.CalculateStats(c);
            loader.BuildAbilityTable(c);
            c.Hp = c.MaxHp;
            c.Mp = c.MaxMp;
            return c;
        }

        private static int Main(string[] args)
        {
            var refPath = Path.Combine(AppContext.BaseDirectory, "reference", "zonein_ryoo.txt");
            var dataDir = args.Length > 0 ? args[0] : FindDataDir();
            var capture = ParseCapture(refPath);
            var s2c = capture.FindAll(d => d.Dir == "S>C");

            var grades = Grades.Load(dataDir);
            var c = Fixture(grades);
            Check("CalculateStats hp", c.MaxHp, 31);
            Check("CalculateStats mp", c.MaxMp, 24);

            var zone = ZoneInfo.Load(Path.Combine(dataDir, "zones"), 240, "Port_Windurst");
            Check("zone.yaml music day", zone.MusicDay, 151);
            zone.Weather = 1;
            zone.WeatherChangeTime = 782423398;

            const long loginUnix = 1792234285;

            Console.WriteLine("login datagram (reply to C2S 0x00A):");
            var built = LoginPackets.OnLogin(c, zone, loginUnix);
            var want = s2c[0].Packets;
            Check("login packet count", built.Count, want.Count);
            for (int i = 0; i < Math.Min(built.Count, want.Count); i++)
                Compare($"#{i} 0x{built[i].GetOpcode():X3}", built[i], want[i]);

            Console.WriteLine("zone-loaded datagram (reply to C2S 0x011):");
            var loaded = LoginPackets.OnZoneLoaded(c, zone, loginUnix);
            Compare("0x0DF", loaded[0], s2c[1].Packets[0]);
            Compare("0x037", loaded[1], s2c[1].Packets[1]);

            // the next datagram re-sends 0x037 a second later: only dead_counter2 moves with the clock
            var again = s2c[2].Packets.Find(p => (p[0] | (p[1] & 1) << 8) == 0x037);
            Compare("0x037 (+1s)", LoginPackets.ServerStatus(c, zone, loginUnix + 1), again);

            // the game-OK reply spans datagrams 2..9; compare up to the treasure pool (the transport spawn follows it)
            Console.WriteLine("game-OK datagrams (reply to C2S 0x00C):");
            var wantOk = new List<byte[]>();
            for (int k = 2; k <= 9; k++) wantOk.AddRange(s2c[k].Packets);
            int lastTrophy = wantOk.FindLastIndex(p => (p[0] | (p[1] & 1) << 8) == 0x0D2);
            wantOk = wantOk.GetRange(0, lastTrophy + 1);
            var merits = MeritTable.Load(dataDir, new DataEnums(dataDir));
            var builtOk = GameOkPackets.OnGameOk(c, zone, loginUnix + 1, new UnityStandings(), merits, enableRoe: true);
            Check("game-OK packet count", builtOk.Count, wantOk.Count);
            for (int i = 0; i < Math.Min(builtOk.Count, wantOk.Count); i++)
            {
                int wantId = wantOk[i][0] | (wantOk[i][1] & 1) << 8;
                if (builtOk[i].GetOpcode() != wantId) { Check($"#{i} opcode", builtOk[i].GetOpcode(), wantId); break; }
                Compare($"#{i} 0x{wantId:X3}", builtOk[i], wantOk[i]);
            }

            VerifyHomepointEvent(capture, dataDir, grades);
            VerifySelfStatePackets(capture, dataDir, grades, loginUnix);
            VerifyLocalPlayerPackets(capture, dataDir, grades);
            VerifyConquest(capture, dataDir, grades);
            VerifyLockstyle(dataDir, grades);
            VerifyFishRanking(dataDir, grades);
            VerifyEquipSet(dataDir, grades);
            VerifyItemMove(dataDir, grades);
            VerifyItemDump(dataDir, grades);
            VerifyItemStack(dataDir, grades);
            VerifyEquipsetSet(dataDir, grades);
            VerifyShop(dataDir, grades);
            VerifySynth(dataDir, grades);
            VerifyGuild(dataDir, grades);
            VerifyMotion(dataDir, grades);
            VerifyMenus(dataDir, grades);
            VerifyChars(dataDir, grades);
            VerifyChat(dataDir, grades);
            VerifyTrade(dataDir, grades);
            VerifyMisc(dataDir, grades);
            VerifyBazaar(dataDir, grades);
            VerifyInspect(dataDir, grades);
            VerifyInspect2(dataDir, grades);
            VerifyItems2(dataDir, grades);

            Console.WriteLine($"{pass_} passed, {fail_} failed");
            return fail_ == 0 ? 0 : 1;
        }

        // help_desk kept in memory over the stub: the rows a GM call adds, the answers the world writes, the reads
        private sealed class HelpDeskDatabase : XI.Persistence.IDatabase
        {
            public sealed class Row { public uint Id, CharId; public string Message, Response; public bool Deleted; }
            public readonly List<Row> Rows = new List<Row>();
            private readonly XI.Persistence.IDatabase inner_;
            public HelpDeskDatabase(XI.Persistence.IDatabase inner) { inner_ = inner; }

            public string Schema => inner_.Schema;
            public string Version => inner_.Version;
            public string DriverVersion => inner_.DriverVersion;
            public bool Transaction(XI.Persistence.Fn fn) { fn(); return true; }
            public XI.Persistence.ResultSet ExecuteBulk(string query, IReadOnlyList<XI.Persistence.BoundValue> p) => Execute(query, p);

            public XI.Persistence.ResultSet Execute(string query, IReadOnlyList<XI.Persistence.BoundValue> p)
            {
                if (!query.Contains("help_desk")) return inner_.Execute(query, p);
                var rows = new List<Dictionary<string, object>>();
                Row Pending(uint charId) => Rows.Find(r => r.CharId == charId && r.Response != null && !r.Deleted);
                uint id = Convert.ToUInt32(p[0].Value);
                if (query.StartsWith("INSERT"))
                {
                    var msg = p[1].Value is byte[] b ? System.Text.Encoding.Latin1.GetString(b) : (string)p[1].Value;
                    Rows.Add(new Row { Id = (uint)Rows.Count + 1, CharId = id, Message = msg });
                }
                else if (query.StartsWith("UPDATE"))
                {
                    var r = Pending(id);
                    if (r != null) r.Deleted = true;
                }
                else if (query.Contains("ORDER BY id DESC"))
                {
                    var r = Rows.FindLast(x => x.CharId == id);
                    if (r != null) rows.Add(new Dictionary<string, object> { ["id"] = r.Id });
                }
                else if (Pending(id) is Row r)
                    rows.Add(new Dictionary<string, object> { ["id"] = r.Id, ["response"] = r.Response });
                var type = query.StartsWith("SELECT") ? XI.Persistence.ResultSetType.Select : XI.Persistence.ResultSetType.Update;
                return new XI.Persistence.ResultSet(new XI.Persistence.InMemoryResultSet(new List<string> { "id", "response" }, rows), query, type, 1);
            }
        }

        // The world pass (0x01B), /translate and /itemsearch (0x02B 0x02C), map markers (0x0D2), the help desk (0x0D3 0x0D4
        // 0x0D5, IPC GMCallResponse) and wide scan (0x0F4 0x0F5 0x0F6, 0x015's TRACKING_POS)
        private static void VerifyMisc(string dataDir, Grades grades)
        {
            Console.WriteLine("misc (C2S 0x01B 0x02B 0x02C 0x063 0x0D2 0x0D3 0x0D4 0x0D5 0x0F4 0x0F5 0x0F6):");
            var db = new HelpDeskDatabase(new StubDatabase(Path.Combine(Path.GetDirectoryName(dataDir), "sql")));
            var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
            var log = new List<string>();
            var zone = new ZoneHost(db, new CharLoader(null, grades, new AbilityTable(), stats), dataDir, true, log.Add);
            long now = 1_000_000;
            zone.NowMs = () => now;
            zone.Random01 = () => 0.5;
            var ipc = new List<XI.Ipc.IIpcMessage>();
            zone.SendIpc = ipc.Add;
            var c = Fixture(grades);
            var s = new MapSession { Char = c, InZone = true, AccountId = 1000 };

            var sent = new List<BasicPacket>();
            List<int> Send(ushort opcode, int size, Action<byte[]> fill = null)
            {
                var buf = new byte[size];
                buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
                fill?.Invoke(buf);
                s.PacketList.Clear();
                zone.Dispatch(s, opcode, new BasicPacket(buf));
                zone.AfterParse(s);
                sent = new List<BasicPacket>(s.PacketList);
                return sent.ConvertAll(r => Opcode(r.Buffer));
            }
            void Opcodes(string label, List<int> got, params int[] want)
            {
                Check($"{label} replies", got.Count, want.Length);
                for (int i = 0; i < Math.Min(got.Count, want.Length); i++) Check($"{label} #{i} opcode", got[i], want[i]);
            }
            bool Rejected(Func<List<int>> send)
            {
                int before = log.Count;
                var got = send();
                return got.Count == 0 && log.Count > before && log[^1].StartsWith("zone: Invalid");
            }
            string Text(BasicPacket pk, int off, int len)
            {
                var span = pk.Buffer.AsSpan(off, len);
                int nul = span.IndexOf((byte)0);
                return System.Text.Encoding.Latin1.GetString(nul >= 0 ? span[..nul] : span);
            }

            // the world pass: a price only, then a 10-digit pass once a purchase is confirmed (0.5 of 9999999999, cut
            // to 32 bits)
            Opcodes("world pass begin", Send(0x01B, 8, b => b[4] = 0), 0x059);
            Check("world pass begin size", sent[0].GetSize(), 36);
            Check("world pass price", BitConverter.ToInt32(sent[0].Buffer, 4 + 8), 10000);
            Check("world pass begin type", sent[0].Buffer[4 + 28], 3);
            Check("world pass begin has no pass", sent[0].Buffer[4 + 12], 0);
            Opcodes("world pass confirm", Send(0x01B, 8, b => b[4] = 1), 0x059);
            Check("world pass type", sent[0].Buffer[4 + 28], 6);
            Check("world pass uses", BitConverter.ToInt32(sent[0].Buffer, 4), 1);
            Check("world pass hours", BitConverter.ToInt32(sent[0].Buffer, 8), 167);
            Check("world pass number", Text(sent[0], 4 + 12, 16) == "0705032703" ? 1 : 0, 1);
            Check("world pass para 4 refused", Rejected(() => Send(0x01B, 8, b => b[4] = 4)) ? 1 : 0, 1);

            // /translate: English names in any case to the Shift-JIS name_jp, and back to the sortname
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var sjis = System.Text.Encoding.GetEncoding(932).GetBytes("チョコボの寝ワラ");
            List<int> Translate(byte from, byte to, byte[] name) => Send(0x02B, 72, b => { b[4] = from; b[5] = to; name.CopyTo(b, 8); });
            Opcodes("translate en", Translate(1, 0, System.Text.Encoding.ASCII.GetBytes("Chocobo Bedding")), 0x047);
            Check("translate size", sent[0].GetSize(), 136);
            Check("translate en item", BitConverter.ToUInt16(sent[0].Buffer, 4), 1);
            Check("translate en from/to", sent[0].Buffer[6] << 8 | sent[0].Buffer[7], 0x0100);
            Check("translate en echoes the name", Text(sent[0], 8, 64) == "Chocobo Bedding" ? 1 : 0, 1);
            Check("translate en to Shift-JIS", sent[0].Buffer.AsSpan(4 + 68, sjis.Length).SequenceEqual(sjis) && sent[0].Buffer[4 + 68 + sjis.Length] == 0 ? 1 : 0, 1);
            Opcodes("translate jp", Translate(0, 1, sjis), 0x047);
            Check("translate jp item", BitConverter.ToUInt16(sent[0].Buffer, 4), 1);
            Check("translate jp to the sortname", Text(sent[0], 4 + 68, 64) == "chocobo bedding" ? 1 : 0, 1);
            Opcodes("translate unknown", Translate(1, 0, System.Text.Encoding.ASCII.GetBytes("No Such Thing")), 0x047);
            Check("translate unknown item", BitConverter.ToUInt16(sent[0].Buffer, 4), 0);
            Check("translate unknown is empty", sent[0].Buffer[4 + 68], 0);
            Check("translate index 2 refused", Rejected(() => Translate(2, 0, new byte[] { 0x41 })) ? 1 : 0, 1);
            Opcodes("item search", Send(0x02C, 72, b => { b[4] = 1; System.Text.Encoding.ASCII.GetBytes("SIMPLE BED").CopyTo(b, 8); }), 0x049);
            Check("item search size", sent[0].GetSize(), 72);
            Check("item search item", BitConverter.ToUInt16(sent[0].Buffer, 4), 2);
            Check("item search echoes the name", Text(sent[0], 8, 64) == "SIMPLE BED" ? 1 : 0, 1);
            Opcodes("dig is ignored", Send(0x063, 16));

            // map markers: the char alone, outside a party
            Opcodes("map group", Send(0x0D2, 8, b => BitConverter.GetBytes(240u).CopyTo(b, 4)), 0x0A0);
            Check("map group char", BitConverter.ToUInt32(sent[0].Buffer, 4), 1);
            Check("map group zone", BitConverter.ToInt16(sent[0].Buffer, 8), 240);
            Check("map group x", BitConverter.ToSingle(sent[0].Buffer, 12) == -140f ? 1 : 0, 1);
            Check("map group other zone refused", Rejected(() => Send(0x0D2, 8, b => BitConverter.GetBytes(241u).CopyTo(b, 4))) ? 1 : 0, 1);

            // the help desk menu
            Opcodes("gm param", Send(0x0D4, 8, b => BitConverter.GetBytes((ushort)0x1234).CopyTo(b, 4)), 0x0B5);
            Check("gm param size", sent[0].GetSize(), 32);
            Check("gm param params[0]", BitConverter.ToUInt32(sent[0].Buffer, 8), 1);
            Check("gm param id", BitConverter.ToUInt16(sent[0].Buffer, 24), 0x1234);
            Check("gm param rescue time", BitConverter.ToUInt16(sent[0].Buffer, 30), 1800);
            Check("gm param option refused", Rejected(() => Send(0x0D4, 8, b => b[6] = 1)) ? 1 : 0, 1);

            // a GM call in two packets, the last first: the text and an unknown key, then a version block
            List<int> GmCall(byte pktId, byte seq, bool eos, byte type, params byte[][] blocks) => Send(0x0D3, 264, b =>
            {
                b[4] = type; b[5] = pktId; b[6] = (byte)(seq | (eos ? 0x80 : 0)); b[7] = (byte)blocks.Length;
                int off = 8;
                foreach (var blk in blocks) { blk.CopyTo(b, off); off += blk.Length; }
            });
            byte[] StrBlock(string text)
            {
                var b = new byte[4 + text.Length + 1];
                b[0] = 3; b[1] = (byte)b.Length;
                System.Text.Encoding.ASCII.GetBytes(text).CopyTo(b, 4);
                return b;
            }
            var version = new byte[24];
            version[0] = 1; version[1] = 24;
            BitConverter.GetBytes(7u).CopyTo(version, 4);
            for (int i = 0; i < 4; i++) BitConverter.GetBytes((uint)(i + 1)).CopyTo(version, 8 + i * 4);
            GmCall(6, 1, false, 2, StrBlock("GMCALL.INPUT:I am stuck"), StrBlock("BOGUS.KEY:x"));
            Check("gm call not sent before eos", ipc.Count, 0);
            Opcodes("gm call", GmCall(6, 0, true, 2, version, StrBlock("GMREPORT.STUCK:1")));
            var call = ipc.Count == 1 ? ipc[0] as XI.Ipc.GMCallRequest : null;
            Check("gm call sent", call != null ? 1 : 0, 1);
            Check("gm call stored", db.Rows.Count == 1 && db.Rows[0].Message == "I am stuck" ? 1 : 0, 1);
            if (call != null)
            {
                Check("gm call id", call.CallId, 1);
                Check("gm call char/account", call.CharId == 1 && call.AccId == 1000 && call.CharName == "Ryoo" ? 1 : 0, 1);
                Check("gm call message", call.Message == "I am stuck" ? 1 : 0, 1);
                Check("gm call version", call.Parameters.GetValueOrDefault("VERSION.UNKNOWN00") == "7" &&
                                         call.Parameters.GetValueOrDefault("VERSION.UNKNOWN01") == "1,2,3,4" ? 1 : 0, 1);
                Check("gm call report flag", call.Parameters.GetValueOrDefault("GMREPORT.STUCK") == "1" ? 1 : 0, 1);
                Check("gm call drops the unknown key", call.Parameters.ContainsKey("BOGUS.KEY") ? 1 : 0, 0);
                Check("gm call parameters", call.Parameters.Count, 4);
            }
            GmCall(7, 0, true, 2, StrBlock("GMCALL.INPUT:again"));
            Check("a second call within 10s is dropped", ipc.Count, 1);
            now += 10000;
            GmCall(8, 0, true, 2, StrBlock("GMCALL.INPUT:again"));
            Check("a call 10s later goes", ipc.Count, 2);
            now += 10000;
            GmCall(9, 0, true, 1, StrBlock("GMCALL.INPUT:history"));
            Check("add history is ignored", ipc.Count, 2);
            Check("gm call vers refused", Rejected(() => GmCall(10, 0, true, 0x12)) ? 1 : 0, 1);
            Check("gm call type 4 refused", Rejected(() => GmCall(10, 0, true, 4)) ? 1 : 0, 1);

            // an answer: the world's GMCallResponse, 300 bytes in two packets; acknowledging it sends the next one
            db.Rows[0].Response = new string('a', 244) + new string('b', 56);
            db.Rows[1].Response = "second";
            s.PacketList.Clear();
            zone.OnIpc(new XI.Ipc.GMCallResponse { CallId = 1, CharId = 1, Message = "x" });
            var answer = new List<BasicPacket>(s.PacketList);
            Check("answer packets", answer.Count, 2);
            if (answer.Count == 2)
            {
                Check("answer opcode", Opcode(answer[0].Buffer), 0x0B6);
                Check("answer size", answer[0].GetSize(), 256);
                Check("answer id", BitConverter.ToUInt32(answer[0].Buffer, 4), 1);
                Check("answer #1 seq/pktNum", BitConverter.ToUInt16(answer[0].Buffer, 8) << 16 | BitConverter.ToUInt16(answer[0].Buffer, 10), 0x10001);
                Check("answer #2 seq/pktNum", BitConverter.ToUInt16(answer[1].Buffer, 8) << 16 | BitConverter.ToUInt16(answer[1].Buffer, 10), 0x20000);
                Check("answer #1 text", Text(answer[0], 12, 244) == new string('a', 244) ? 1 : 0, 1);
                Check("answer #2 text", Text(answer[1], 12, 244) == new string('b', 56) ? 1 : 0, 1);
            }
            Opcodes("acknowledge", Send(0x0D5, 12), 0x0B6);
            Check("acknowledged row", db.Rows[0].Deleted ? 1 : 0, 1);
            Check("next answer", sent.Count == 1 && BitConverter.ToUInt32(sent[0].Buffer, 4) == 2 && Text(sent[0], 12, 244) == "second" ? 1 : 0, 1);
            Opcodes("acknowledge the last", Send(0x0D5, 12));

            // wide scan: 150 for every job (ALL_JOBS_WIDESCAN)
            List<int> Scan() => Send(0x0F4, 8, b => b[4] = 1);
            var scan = Scan();
            Check("wide scan list start", scan.Count > 2 && scan[0] == 0x0F6 && sent[0].Buffer[4] == 1 ? 1 : 0, 1);
            Check("wide scan list end", scan.Count > 2 && scan[^1] == 0x0F6 && sent[^1].Buffer[4] == 2 ? 1 : 0, 1);
            int dots = 0, bad = 0;
            var targids = new List<ushort>();
            for (int i = 1; i < sent.Count - 1; i++)
            {
                var b = sent[i].Buffer;
                uint bits = BitConverter.ToUInt32(b, 4);
                short dx = BitConverter.ToInt16(b, 8), dz = BitConverter.ToInt16(b, 10);
                if (Opcode(b) != 0x0F4 || sent[i].GetSize() != 28 || (bits >> 24 & 7) != 1 || dx * dx + dz * dz > 151 * 151) bad++;
                targids.Add((ushort)bits);
                dots++;
            }
            Check("wide scan dots", dots > 0 ? 1 : 0, 1);
            Check("wide scan dots are NPCs in range", bad, 0);
            Check("wide scan skips the untargetable warp", targids.Contains(0x102) ? 1 : 0, 0);
            var sorted = new List<ushort>(targids);
            sorted.Sort();
            Check("wide scan in act index order", System.Linq.Enumerable.SequenceEqual(sorted, targids) ? 1 : 0, 1);
            c.MJob = 11; c.MLvl = 80;
            Scan();
            Check("RNG80 scans further", sent.Count - 2 > dots ? 1 : 0, 1);
            c.MJob = 5; c.MLvl = 1;
            Check("wide scan SendFlg 0 refused", Rejected(() => Send(0x0F4, 8)) ? 1 : 0, 1);

            // tracking: a scanned NPC's position on each 0x015 until 0x0F6; the warp can't be tracked
            List<int> Pos() => Send(0x015, 32, b =>
            {
                BitConverter.GetBytes(c.X).CopyTo(b, 4);
                BitConverter.GetBytes(c.Y).CopyTo(b, 8);
                BitConverter.GetBytes(c.Z).CopyTo(b, 12);
            });
            ushort tracked = targids.Count > 0 ? targids[0] : (ushort)0;
            Opcodes("track", Send(0x0F5, 8, b => BitConverter.GetBytes((uint)tracked).CopyTo(b, 4)));
            Check("tracking", c.WideScanTarget ?? 0, tracked);
            Pos();
            var pos = sent.FindAll(pk => Opcode(pk.Buffer) == 0x0F5);
            Check("tracking pos", pos.Count, 1);
            if (pos.Count == 1)
            {
                Check("tracking pos size", pos[0].GetSize(), 24);
                Check("tracking pos act index", BitConverter.ToUInt16(pos[0].Buffer, 4 + 14), tracked);
                Check("tracking pos level/state", pos[0].Buffer[4 + 12] << 8 | pos[0].Buffer[4 + 16], 0x0101);
            }
            Send(0x0F6, 8);
            Check("tracking ended", c.WideScanTarget.HasValue ? 1 : 0, 0);
            Pos();
            Check("no tracking pos after the end", sent.FindAll(pk => Opcode(pk.Buffer) == 0x0F5).Count, 0);
            Send(0x0F5, 8, b => BitConverter.GetBytes(0x102u).CopyTo(b, 4));
            Check("the warp can't be tracked", c.WideScanTarget.HasValue ? 1 : 0, 0);
            Send(0x0F5, 8, b => BitConverter.GetBytes((uint)tracked).CopyTo(b, 4));
            Send(0x0F5, 8, b => BitConverter.GetBytes(0xFFFu).CopyTo(b, 4));
            Check("an act index with no NPC clears it", c.WideScanTarget.HasValue ? 1 : 0, 0);
            Check("act index 0 refused", Rejected(() => Send(0x0F5, 8)) ? 1 : 0, 1);
            foreach (var line in log)
                if (line.StartsWith("zone: Bad")) { fail_++; Console.WriteLine("  FAIL " + line); }
        }

        // An empty database apart from zone_settings and the static item / growth tables (sql/*.sql): every char var,
        // server var, unlock and char_points row reads 0
        private sealed class StubDatabase : XI.Persistence.IDatabase
        {
            private readonly List<Dictionary<string, object>> zones_ = new List<Dictionary<string, object>>();

            private readonly string sqlDir_;
            private readonly Dictionary<string, (List<string> Cols, List<Dictionary<string, object>> Rows)> tables_ =
                new Dictionary<string, (List<string>, List<Dictionary<string, object>>)>();

            // a table of a sql/<name>.sql dump: the CREATE TABLE's columns and one row per INSERT, values kept as text
            private (List<string> Cols, List<Dictionary<string, object>> Rows) Table(string name)
            {
                if (tables_.TryGetValue(name, out var t)) return t;
                var cols = new List<string>();
                var rows = new List<Dictionary<string, object>>();
                var col = new System.Text.RegularExpressions.Regex(@"^\s+`(\w+)`");
                var setVar = new System.Text.RegularExpressions.Regex(@"^SET @(\w+)\s*=\s*(\d+);");
                var vars = new Dictionary<string, long>();
                // a cell of @NAME | @NAME ... (item_basic's flags and types) is the OR of the SET values
                string Resolve(string cell)
                {
                    if (!cell.Contains('@')) return cell;
                    long v = 0;
                    foreach (var part in cell.Split('|'))
                    {
                        var t = part.Trim();
                        if (t.StartsWith("@") && vars.TryGetValue(t.Substring(1), out long x)) v |= x;
                        else if (long.TryParse(t, out x)) v |= x;
                        else return cell;
                    }
                    return v.ToString();
                }
                bool inCreate = false;
                string prefix = $"INSERT INTO `{name}` VALUES (";
                foreach (var line in File.ReadLines(Path.Combine(sqlDir_, name + ".sql")))
                {
                    var sm = setVar.Match(line);
                    if (sm.Success) { vars[sm.Groups[1].Value] = long.Parse(sm.Groups[2].Value); continue; }
                    if (line.StartsWith("CREATE TABLE")) { inCreate = true; continue; }
                    if (inCreate)
                    {
                        var m = col.Match(line);
                        if (m.Success) cols.Add(m.Groups[1].Value);
                        else if (line.StartsWith(")")) inCreate = false;
                        continue;
                    }
                    if (!line.StartsWith(prefix)) continue;
                    var row = new Dictionary<string, object>();
                    var cell = new System.Text.StringBuilder();
                    bool quoted = false, wasQuoted = false;
                    int k = 0;
                    for (int i = prefix.Length; i < line.Length; i++)
                    {
                        char ch = line[i];
                        if (quoted)
                        {
                            if (ch == '\\') cell.Append(line[++i]);
                            else if (ch == '\'') quoted = false;
                            else cell.Append(ch);
                        }
                        else if (ch == '\'') quoted = wasQuoted = true;
                        else if (ch == ',' || ch == ')')
                        {
                            // an unquoted NULL is a SQL null
                            string text = cell.ToString();
                            if (k < cols.Count) row[cols[k++]] = !wasQuoted && text == "NULL" ? null : Resolve(text);
                            cell.Clear();
                            wasQuoted = false;
                            if (ch == ')') break;
                        }
                        else cell.Append(ch);
                    }
                    rows.Add(row);
                }
                return tables_[name] = (cols, rows);
            }

            public StubDatabase(string sqlDir)
            {
                sqlDir_ = sqlDir;
                var row = new System.Text.RegularExpressions.Regex(@"^INSERT INTO `zone_settings` VALUES \((\d+),'([^']*)',(\d+),'([^']*)'");
                foreach (var line in File.ReadLines(Path.Combine(sqlDir, "zone_settings.sql")))
                {
                    var m = row.Match(line);
                    if (!m.Success) continue;
                    zones_.Add(new Dictionary<string, object>
                    {
                        ["zoneid"] = int.Parse(m.Groups[1].Value), ["zoneip"] = m.Groups[2].Value,
                        ["zoneport"] = int.Parse(m.Groups[3].Value), ["name"] = m.Groups[4].Value,
                    });
                }
            }

            public string Schema => "xidb";
            public string Version => "stub";
            public string DriverVersion => "stub";
            public bool Transaction(XI.Persistence.Fn fn) { fn(); return true; }
            public XI.Persistence.ResultSet ExecuteBulk(string query, IReadOnlyList<XI.Persistence.BoundValue> p) => Execute(query, p);

            // every statement that is not a SELECT, with its arguments: "query|arg|arg"
            public readonly List<string> Writes = new List<string>();

            public XI.Persistence.ResultSet Execute(string query, IReadOnlyList<XI.Persistence.BoundValue> p)
            {
                if (!query.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = new List<string> { query };
                    foreach (var v in p) parts.Add(Convert.ToString(v.Value, CultureInfo.InvariantCulture));
                    Writes.Add(string.Join("|", parts));
                }
                foreach (var name in new[] { "item_equipment", "item_weapon", "item_mods", "item_basic", "item_usable", "skill_caps", "skill_ranks", "exp_base", "conquest_system", "synth_recipes", "augments" })
                {
                    if (!(query + " ").Contains("FROM " + name + " ")) continue;
                    var (tcols, trows) = Table(name);
                    if (query.Contains("WHERE itemId = ?") || query.Contains("WHERE itemid = ?"))
                        trows = trows.FindAll(r => Convert.ToInt32(r.TryGetValue("itemId", out var id) ? id : r["itemid"]) == Convert.ToInt32(p[0].Value));
                    return new XI.Persistence.ResultSet(new XI.Persistence.InMemoryResultSet(tcols, trows), query, XI.Persistence.ResultSetType.Select);
                }
                var rows = new List<Dictionary<string, object>>();
                if (query.Contains("FROM zone_settings WHERE zoneid = ?"))
                    rows.AddRange(zones_.FindAll(z => (int)z["zoneid"] == Convert.ToInt32(p[0].Value)));
                else if (query.Contains("FROM zone_settings"))
                    rows.AddRange(zones_);
                var cols = new List<string> { "zoneid", "name", "zoneip", "zoneport", "value", "expiry" };
                var type = query.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ? XI.Persistence.ResultSetType.Select : XI.Persistence.ResultSetType.Update;
                // every write lands on one row
                return new XI.Persistence.ResultSet(new XI.Persistence.InMemoryResultSet(cols, rows), query, type, 1);
            }
        }

        // Talking to the Port Windurst home point (C2S 0x01A) runs its script through InteractionGlobal on LuaJIT and
        // starts event 8701 (0x034); the client's 0x05B ends it (0x052 EventRecvPending). The fixture has no home
        // points unlocked, as at capture time.
        private static void VerifyHomepointEvent(List<Datagram> capture, string dataDir, Grades grades)
        {
            Console.WriteLine("home point event (C2S 0x01A, then 0x05B):");
            // the capture's packets in order from the Talk action (ActionID 0) on
            int at = 0;
            byte[] Find(string dir, int opcode, Func<byte[], bool> match = null)
            {
                for (; at < capture.Count; at++)
                {
                    if (capture[at].Dir != dir) continue;
                    foreach (var p in capture[at].Packets)
                        if ((p[0] | (p[1] & 1) << 8) == opcode && (match == null || match(p))) return p;
                }
                throw new InvalidDataException($"{dir} 0x{opcode:X3} not in the capture");
            }
            var action = Find("C>S", 0x01A, p => BitConverter.ToUInt16(p, C2sAction.OffActionID) == 0);
            var wantEventNum = Find("S>C", 0x034);
            var eventEnd = Find("C>S", 0x05B);
            var wantRelease = Find("S>C", 0x052);

            var c = Fixture(grades);
            // where the client stood when it sent the 0x01A
            (c.X, c.Y, c.Z, c.Rotation) = (-202.83849f, -8.2153f, 207.36887f, 0x16);
            // an earlier event's reward (the capture's 0x01E) took the gil from 10 to 60
            c.FindItem(Container.Inventory, 0).Quantity = 60;

            var log = new List<string>();
            var zone = new ZoneHost(new StubDatabase(Path.Combine(Path.GetDirectoryName(dataDir), "sql")),new CharLoader(null, grades, new AbilityTable()), dataDir, true, log.Add);
            using var scripts = new XI.Script.ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
            zone.InitScripts(scripts);
            var s = new MapSession { Char = c };

            zone.Dispatch(s, 0x01A, new BasicPacket(action));
            var sent = new List<BasicPacket>(s.PacketList);
            s.PacketList.Clear();
            var eventNum = sent.Find(p => p.GetOpcode() == 0x034);
            if (eventNum == null)
            {
                fail_++;
                Console.WriteLine($"  FAIL no 0x034; sent [{string.Join(", ", sent.ConvertAll(p => $"0x{p.GetOpcode():X3}"))}]");
                foreach (var line in log) if (!line.StartsWith("lua: 7")) Console.WriteLine("       " + line);
                return;
            }
            Compare("0x034 EVENTNUM", eventNum, wantEventNum);
            Check("event animation", c.Animation, 4);

            zone.Dispatch(s, 0x05B, new BasicPacket(eventEnd));
            var release = new List<BasicPacket>(s.PacketList).Find(p => p.GetOpcode() == 0x052);
            if (release == null) { fail_++; Console.WriteLine("  FAIL no 0x052 after 0x05B"); }
            else Compare("0x052 EVENTUCOFF", release, wantRelease);
            Check("event over", c.IsInEvent ? 1 : 0, 0);
            Check("animation back", c.Animation, 0);
            foreach (var line in log) if (line.StartsWith("lua:") && !line.StartsWith("lua: 7")) Console.WriteLine("       " + line);
        }

        private static int Opcode(byte[] p) => p[0] | (p[1] & 1) << 8;

        // The settings/state packets of the client's login batch (C2S datagrams 3 and 4) through ZoneHost.Dispatch:
        // the ones that changed nothing reply nothing, 0x04B gets the server message (0x04D) and each 0x0DB the config
        // and char status (0x0B4 + 0x037), as the C++ answered in S2C datagram 11.
        private static void VerifySelfStatePackets(List<Datagram> capture, string dataDir, Grades grades, long loginUnix)
        {
            Console.WriteLine("login-batch settings packets (C2S 0x11B 0x053 0x04B 0x0E0 0x00F 0x0DB 0x112):");
            var c = Fixture(grades);
            var log = new List<string>();
            var zone = new ZoneHost(new StubDatabase(Path.Combine(Path.GetDirectoryName(dataDir), "sql")), new CharLoader(null, grades, new AbilityTable()), dataDir, true, log.Add);
            long unixNow = 0;
            zone.UnixNow = () => unixNow;
            using var scripts = new XI.Script.ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
            zone.InitScripts(scripts);
            var s = new MapSession { Char = c };

            var cs = capture.FindAll(d => d.Dir == "C>S");
            var replies = new List<BasicPacket>();
            int[] ported = { 0x11B, 0x053, 0x04B, 0x0E0, 0x00F, 0x0DB, 0x112 };
            // the server clock at each datagram: 0x037's dead_counter2 shows datagram 4 (1.23s after 3) crossed two
            // second boundaries
            foreach (var (d, now) in new[] { (cs[2], loginUnix + 1), (cs[3], loginUnix + 3) })
                foreach (var p in d.Packets)
                {
                    int op = Opcode(p);
                    if (Array.IndexOf(ported, op) < 0) continue;
                    unixNow = now;
                    s.PacketList.Clear();
                    zone.Dispatch(s, (ushort)op, new BasicPacket(p));
                    replies.AddRange(s.PacketList);
                }

            var want = capture.FindAll(d => d.Dir == "S>C")[10].Packets.FindAll(p => Opcode(p) is 0x04D or 0x0B4 or 0x037);
            Check("reply count", replies.Count, want.Count);
            for (int i = 0; i < Math.Min(replies.Count, want.Count); i++)
                Compare($"#{i} 0x{Opcode(want[i]):X3}", replies[i], want[i]);
            foreach (var line in log)
                if (line.StartsWith("zone: Invalid") || line.StartsWith("zone: Bad") || line.StartsWith("lua: xi.server")) { fail_++; Console.WriteLine("  FAIL " + line); }
        }

        // C2S 0x061 CLISTATUS and 0x118 UNITY_TOGGLE of the login batch (C2S datagram 2) each get
        // SendLocalPlayerPackets' batch (0x0DF 0x061 0x062 0x119 0x063 x3), which spans S2C datagrams 9 and 10. The
        // equipment, skills and exp tables come from sql/; the character has no unity, merits or job points.
        private static void VerifyLocalPlayerPackets(List<Datagram> capture, string dataDir, Grades grades)
        {
            Console.WriteLine("status menu packets (C2S 0x061, 0x118):");
            var db = new StubDatabase(Path.Combine(Path.GetDirectoryName(dataDir), "sql"));
            var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
            var c = Fixture(grades);
            stats.EquipAtLogin(c);
            stats.BuildSkillsTable(c);
            CharStats.UpdateHealth(c);
            Check("UpdateHealth hp", c.MaxHp, 31);

            var log = new List<string>();
            var zone = new ZoneHost(db, new CharLoader(null, grades, new AbilityTable(), stats), dataDir, true, log.Add);
            var s = new MapSession { Char = c };
            var replies = new List<BasicPacket>();
            foreach (var p in capture.FindAll(d => d.Dir == "C>S")[2].Packets)
            {
                int op = Opcode(p);
                if (op != 0x061 && op != 0x118) continue;
                s.PacketList.Clear();
                zone.Dispatch(s, (ushort)op, new BasicPacket(p));
                replies.AddRange(s.PacketList);
            }

            var s2c = capture.FindAll(d => d.Dir == "S>C");
            var want = new List<byte[]>(s2c[9].Packets);
            want.AddRange(s2c[10].Packets);
            int first = want.FindIndex(p => Opcode(p) == 0x0DF);
            want = want.GetRange(first, 14);
            Check("reply count", replies.Count, want.Count);
            for (int i = 0; i < Math.Min(replies.Count, want.Count); i++)
                Compare($"#{i} 0x{Opcode(want[i]):X3}", replies[i], want[i]);
            foreach (var line in log)
                if (line.StartsWith("zone: Invalid") || line.StartsWith("zone: Bad") || line.Contains("needs the stat tables")) { fail_++; Console.WriteLine("  FAIL " + line); }
        }

        // C2S 0x05A REQCONQUEST of the login batch (C2S datagram 2) gets 0x05E CONQUEST and 0x071 INFLUENCE (S2C datagram
        // 10). conquest_system is sql/'s fresh table: every region neutral, no influence. The formulas the capture leaves
        // at their all-equal case are checked on their own.
        private static void VerifyConquest(List<Datagram> capture, string dataDir, Grades grades)
        {
            Console.WriteLine("conquest packets (C2S 0x05A):");
            Check("InfluenceGraphics San d'Oria 65%", ConquestPackets.InfluenceGraphics(65, 20, 15, 0), 3);
            Check("InfluenceGraphics Windurst 50%, Bastok 25%", ConquestPackets.InfluenceGraphics(10, 30, 60, 0), 4 * 1 + 16 * 2);
            Check("InfluenceGraphics beastmen ahead", ConquestPackets.InfluenceGraphics(1, 2, 3, 4), 64);
            Check("InfluenceRanking San d'Oria first", ConquestPackets.InfluenceRanking(10, 5, 1), 57);
            Check("Balance 1st/3rd/2nd", ConquestPackets.Balance(3, 1, 2), 45);
            Check("Alliance against San d'Oria", ConquestPackets.Alliance(10, 2, 3), 0b011);
            Check("ZoneRegions Port Windurst", ZoneRegions.Of(240), 21);

            var s2c = capture.FindAll(d => d.Dir == "S>C");
            var log = new List<string>();
            var zone = new ZoneHost(new StubDatabase(Path.Combine(Path.GetDirectoryName(dataDir), "sql")), new CharLoader(null, grades, new AbilityTable()), dataDir, true, log.Add);
            zone.UnixNow = () => s2c[10].Time;
            var s = new MapSession { Char = Fixture(grades) };
            foreach (var p in capture.FindAll(d => d.Dir == "C>S")[2].Packets)
                if (Opcode(p) == 0x05A) zone.Dispatch(s, 0x05A, new BasicPacket(p));

            var replies = new List<BasicPacket>(s.PacketList);
            var want = s2c[10].Packets.FindAll(p => Opcode(p) is 0x05E or 0x071);
            Check("reply count", replies.Count, want.Count);
            for (int i = 0; i < Math.Min(replies.Count, want.Count); i++)
                Compare($"#{i} 0x{Opcode(want[i]):X3}", replies[i], want[i]);
            foreach (var line in log)
                if (line.StartsWith("zone: Invalid") || line.StartsWith("zone: Bad")) { fail_++; Console.WriteLine("  FAIL " + line); }
        }

        // C2S 0x053 LOCKSTYLE Set / Disable / Enable on the fixture with its onion dagger in the main hand. Not in the
        // capture, so the expectations follow the C++ by hand.
        // The fish ranking menu (C2S 0x04B value1 2) while the results are presented: two real entries padded with the
        // placeholder fishers to MAX_FAKE_ENTRIES (15), five per page after the self block, the last block cut off by
        // the size, as fishingcontest + 0x04d_fragments_fishranking.cpp build them.
        private static void VerifyFishRanking(string dataDir, Grades grades)
        {
            Console.WriteLine("fish ranking (C2S 0x04B value1 2):");
            var c = Fixture(grades);
            c.MainLook.CopyFrom(c.Look);
            c.RealSkillRank[Skill.Fishing] = 3;
            var zone = new ZoneHost(new StubDatabase(Path.Combine(Path.GetDirectoryName(dataDir), "sql")),
                new CharLoader(null, grades, new AbilityTable()), dataDir, true, new List<string>().Add);
            const long now = 1792232771;
            zone.UnixNow = () => now;
            var contest = new FishingContest(15) { CurrentStatus = FishingContest.Status.Presenting };
            contest.Entries.Add(new FishingContest.Entry { Name = "Alpha", MJob = 1, MLvl = 75, Race = 2, FishRank = 9, Score = 120, ContestRank = 1 });
            contest.Entries.Add(new FishingContest.Entry { Name = "Beta", MJob = 2, MLvl = 70, Race = 3, FishRank = 8, Score = 100, ContestRank = 2 });
            contest.BuildPlaceholderEntries();
            zone.Contest = contest;
            Check("placeholder fishers (all 15 built)", contest.FakeEntries.Count, 15);

            var s = new MapSession { Char = c };
            byte[] Request(byte chunk, int offset, int dataSize)
            {
                var b = new byte[24];
                b[0] = 0x4B; b[1] = 24 / 4 << 1;
                b[4] = chunk; b[6] = 2; b[7] = 2;
                BitConverter.GetBytes(0x1234).CopyTo(b, 8);
                BitConverter.GetBytes(offset).CopyTo(b, 16);
                BitConverter.GetBytes(dataSize).CopyTo(b, 20);
                s.PacketList.Clear();
                zone.Dispatch(s, 0x04B, new BasicPacket(b));
                var sent = new List<BasicPacket>(s.PacketList);
                Check($"chunk {chunk} replies", sent.Count, 1);
                return sent.Count > 0 ? sent[0].Buffer : new byte[256];
            }
            static int Size(byte[] p) => (p[1] >> 1) * 4;
            static string Name(byte[] p, int o) => System.Text.Encoding.ASCII.GetString(p, o, 16).TrimEnd('\0');
            static int Block(int i) => 32 + i * 36;

            var p1 = Request(1, 0, 180);
            Check("page 1 opcode", Opcode(p1), 0x04D);
            Check("page 1 size (self + 4 shown blocks)", Size(p1), 212);
            Check("Command/Result/value1/value2", BitConverter.ToInt32(p1, 4), 1 | 1 << 8 | 2 << 16 | 2 << 24);
            Check("timestamp echoed", BitConverter.ToInt32(p1, 8), 0x1234);
            Check("size_total", BitConverter.ToInt32(p1, 12), 15 * 36);
            Check("data_size", BitConverter.ToInt32(p1, 20), 180);
            Check("totalEntries", BitConverter.ToInt32(p1, 24), 15);
            Check("self name", Name(p1, Block(0)) == "Ryoo" ? 1 : 0, 1);
            Check("self mjob/mlvl/race", p1[Block(0) + 16] | p1[Block(0) + 18] << 8 | p1[Block(0) + 20] << 16, 5 | 1 << 8 | 7 << 16);
            Check("self allegiance/fishRank", p1[Block(0) + 22] | p1[Block(0) + 23] << 8, 1 | 3 << 8);
            Check("self submitTime (vanadiel now)", BitConverter.ToUInt32(p1, Block(0) + 28), LoginPackets.VanadielTimestamp(now));
            Check("self resultCount", p1[Block(0) + 33], 15);
            Check("#1 Alpha", Name(p1, Block(1)) == "Alpha" ? 1 : 0, 1);
            Check("#1 score/rank/resultCount", BitConverter.ToInt32(p1, Block(1) + 24) | p1[Block(1) + 32] << 16 | p1[Block(1) + 33] << 24, 120 | 1 << 16 | 15 << 24);
            Check("#3 first placeholder", Name(p1, Block(3)) == " SmallFisher01 " ? 1 : 0, 1);
            Check("#3 mjob/mlvl/race/allegiance", p1[Block(3) + 16] | p1[Block(3) + 18] << 8 | p1[Block(3) + 20] << 16 | p1[Block(3) + 22] << 24, 1 | 75 << 8 | 1 << 16);
            // greatest: (needed + 1) - n with 13 needed
            Check("#3 score/rank", BitConverter.ToInt32(p1, Block(3) + 24) | p1[Block(3) + 32] << 16, 13 | 3 << 16);

            var p2 = Request(2, 180, 180);
            Check("page 2 size", Size(p2), 212);
            Check("page 2 self block empty", Name(p2, Block(0)).Length + p2[Block(0) + 33], 0);
            Check("page 2 #1 is the 4th placeholder", Name(p2, Block(1)) == " SmallFisher04 " ? 1 : 0, 1);
            Check("page 2 #1 score", BitConverter.ToInt32(p2, Block(1) + 24), 10);

            // 12/13 carry only the self block, which the size does not count
            var p12 = Request(12, 0, 0);
            Check("chunk 12 size", Size(p12), 32);
            Check("chunk 12 data_size", BitConverter.ToInt32(p12, 20), 0);

            // a fisher with an entry gets it back as the self block
            contest.Entries[1].Name = "Ryoo";
            var own = Request(1, 0, 180);
            Check("own entry as the self block (score/rank)", BitConverter.ToInt32(own, Block(0) + 24) | own[Block(0) + 32] << 16, 100 | 2 << 16);
        }

        private static void VerifyLockstyle(string dataDir, Grades grades)
        {
            Console.WriteLine("lockstyle (C2S 0x053):");
            var db = new StubDatabase(Path.Combine(Path.GetDirectoryName(dataDir), "sql"));
            var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
            var c = Fixture(grades);
            c.Equip.Insert(0, new EquipEntry { EquipSlot = 0, InvSlot = 5, Container = Container.Inventory });
            stats.EquipAtLogin(c);
            Check("look main from the equipped dagger", c.Look.Main, 164);
            Check("look body from the separates", c.Look.Body, 8);

            var log = new List<string>();
            var zone = new ZoneHost(db, new CharLoader(null, grades, new AbilityTable(), stats), dataDir, true, log.Add);
            var s = new MapSession { Char = c };

            static BasicPacket Lockstyle(byte mode, params (byte Kind, ushort Item)[] items)
            {
                var b = new byte[136];
                b[0] = 0x53; b[1] = 136 / 4 << 1;
                b[4] = (byte)items.Length; b[5] = mode;
                for (int i = 0; i < items.Length; i++)
                {
                    b[8 + i * 8 + 1] = items[i].Kind;
                    BitConverter.GetBytes(items[i].Item).CopyTo(b, 8 + i * 8 + 4);
                }
                return new BasicPacket(b);
            }
            var sent = new List<BasicPacket>();
            List<int> Send(BasicPacket p)
            {
                s.PacketList.Clear();
                zone.Dispatch(s, 0x053, p);
                sent = new List<BasicPacket>(s.PacketList);
                return sent.ConvertAll(r => Opcode(r.Buffer));
            }
            ushort Word(int i, int off) => i < sent.Count ? BitConverter.ToUInt16(sent[i].Buffer, off) : (ushort)0;

            // main: the dagger over itself (valid); sub: the dagger over an empty sub (not valid: reported); head: a
            // sword (not a head piece: dropped quietly); legs: the loincloth; a ring (not visible: skipped)
            var replies = Send(Lockstyle(3, (0, 16482), (1, 16482), (4, 16535), (7, 12887), (13, 13496)));
            Check("Set replies", replies.Count, 4);
            for (int i = 0; i < 4; i++)
                Check($"Set #{i} opcode", i < replies.Count ? replies[i] : 0, new[] { 0x009, 0x11C, 0x051, 0x067 }[i]);
            Check("StyleLockOn", Word(0, 10), SelfPackets.MsgStyleLockOn);
            Check("0x11C count", Word(1, 4) & 0xFF, 1);
            Check("0x11C item", Word(1, 8), 16482);
            Check("style main", c.StyleItems[0], 16482);
            Check("style sub", c.StyleItems[1], 0);
            Check("style head", c.StyleItems[4], 0);
            Check("style legs", c.StyleItems[7], 12887);
            Check("style ring slot from the (empty) gear slot", c.StyleItems[13], 0);
            Check("mainlook main", c.MainLook.Main, 164);
            Check("mainlook head", c.MainLook.Head, 0);
            Check("mainlook legs", c.MainLook.Legs, 8);
            Check("locked", c.StyleLocked ? 1 : 0, 1);

            replies = Send(Lockstyle(0));
            Check("Disable replies", replies.Count, 3);
            Check("StyleLockOff", Word(0, 10), SelfPackets.MsgStyleLockOff);
            Check("Disable clears the style", c.StyleItems[0] + c.StyleItems[7], 0);

            replies = Send(Lockstyle(4));
            Check("Enable replies", replies.Count, 3);
            Check("Enable takes the gear", c.StyleItems[0], 16482);
            foreach (var line in log)
                if (line.StartsWith("zone: Invalid") || line.StartsWith("zone: Bad") || line.Contains("needs the item tables")) { fail_++; Console.WriteLine("  FAIL " + line); }
        }

        // C2S 0x050 and what the end of the datagram sends: flushEquipChanges, then GROUP_ATTR and 0x037 for the updatemask
        private static void VerifyEquipSet(string dataDir, Grades grades)
        {
            Console.WriteLine("equip set (C2S 0x050):");
            var db = new StubDatabase(Path.Combine(Path.GetDirectoryName(dataDir), "sql"));
            var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
            var c = Fixture(grades);
            stats.EquipAtLogin(c);
            c.SyncedContainers = GameOkPackets.AllSyncedMask;
            c.Tp = 500;

            var log = new List<string>();
            var zone = new ZoneHost(db, new CharLoader(null, grades, new AbilityTable(), stats), dataDir, true, log.Add);
            var s = new MapSession { Char = c };

            var sent = new List<BasicPacket>();
            List<int> Send(byte invSlot, byte equipSlot, byte container)
            {
                var b = new byte[8];
                b[0] = 0x50; b[1] = 8 / 4 << 1;
                b[4] = invSlot; b[5] = equipSlot; b[6] = container;
                s.PacketList.Clear();
                zone.Dispatch(s, 0x050, new BasicPacket(b));
                zone.AfterParse(s);
                sent = new List<BasicPacket>(s.PacketList);
                return sent.ConvertAll(r => Opcode(r.Buffer));
            }
            byte Byte(int i, int off) => i < sent.Count ? sent[i].Buffer[off] : (byte)0xFF;
            void Opcodes(string label, List<int> got, params int[] want)
            {
                Check($"{label} replies", got.Count, want.Length);
                for (int i = 0; i < Math.Min(got.Count, want.Length); i++) Check($"{label} #{i} opcode", got[i], want[i]);
            }

            // the dagger (inventory 5) into the main hand
            Opcodes("equip main", Send(5, 0, 0), 0x050, 0x051, 0x01F, 0x01D, 0x01D, 0x0AA, 0x0AC, 0x0DF, 0x037);
            Check("EQUIP_LIST slot/kind/category", Byte(0, 4) << 16 | Byte(0, 5) << 8 | Byte(0, 6), 5 << 16);
            Check("ITEM_LIST item slot", Byte(2, 11), 5);
            Check("ITEM_LIST locked as equipped", Byte(2, 12), 5);
            Check("ITEM_SAME container, then all loaded", Byte(3, 4) << 8 | Byte(4, 4), 0 << 8 | 1);
            Check("look main", c.Look.Main, 164);
            Check("equip row", c.Equip.Exists(e => e.EquipSlot == 0 && e.InvSlot == 5) ? 1 : 0, 1);
            Check("TP lost", c.Tp, 0);

            // the ring into ring 1, then the same ring into ring 2: refused without a reply
            Opcodes("equip ring1", Send(7, 13, 0), 0x050, 0x051, 0x01F, 0x01D, 0x01D, 0x0AA, 0x0AC, 0x0DF, 0x037);
            Opcodes("same ring into ring2", Send(7, 14, 0));
            Check("ring stays in ring1", c.Equip.Exists(e => e.EquipSlot == 13) && !c.Equip.Exists(e => e.EquipSlot == 14) ? 1 : 0, 1);

            // index 0 unequips the main hand; the item goes back to normal
            Opcodes("unequip main", Send(0, 0, 0), 0x050, 0x051, 0x01F, 0x01D, 0x01D, 0x0AA, 0x0AC, 0x0DF, 0x037);
            Check("EQUIP_LIST unequip", Byte(0, 4) << 16 | Byte(0, 5) << 8 | Byte(0, 6), 0);
            Check("ITEM_LIST unlocked", Byte(2, 12), 0);
            Check("look main (RDM has no hand-to-hand)", c.Look.Main, 0);
            Check("main row gone", c.Equip.Exists(e => e.EquipSlot == 0) ? 1 : 0, 0);

            // a dagger in the sub slot with no main weapon: refused quietly
            Opcodes("sub weapon without a main", Send(5, 1, 0));
            Check("sub stays empty", c.Equipped[1] == null ? 1 : 0, 1);

            // the Mog Safe is not an equip container
            int before = log.Count;
            Opcodes("from the mog safe", Send(5, 0, 1));
            Check("mog safe rejected", log.Count > before && log[^1].StartsWith("zone: Invalid") ? 1 : 0, 1);

            // the dagger unequipped and thrown away in one datagram: its queued change is dropped and the equipment
            // resynced (resyncEquipment: EQUIP_CLEAR, then each equipped slot's 0x050), no flush for the main hand
            Send(5, 0, 0);
            stats.Settings.ItemRecycleBin = false;
            var unequip = new byte[8];
            unequip[0] = 0x50; unequip[1] = 8 / 4 << 1;
            var dump = new byte[12];
            dump[0] = 0x28; dump[1] = 12 / 4 << 1;
            dump[4] = 1; dump[9] = 5;
            s.PacketList.Clear();
            zone.Dispatch(s, 0x050, new BasicPacket(unequip));
            zone.Dispatch(s, 0x028, new BasicPacket(dump));
            Check("unequip + dump: the queued changes dropped", c.EquipChanges.Count, 0);
            zone.AfterParse(s);
            var resyncPackets = new List<BasicPacket>(s.PacketList);
            var resync = resyncPackets.ConvertAll(r => Opcode(r.Buffer));
            int clear = resync.IndexOf(0x04F);
            Check("unequip + dump: the dagger gone", c.Items.Exists(i => i.Location == Container.Inventory && i.Slot == 5) ? 1 : 0, 0);
            var lists = resyncPackets.FindAll(r => Opcode(r.Buffer) == 0x050);
            Check("unequip + dump: EQUIP_CLEAR, then each equipped slot's 0x050 (none for the main hand)",
                  string.Join(",", c.Equip.ConvertAll(e => e.EquipSlot)),
                  clear >= 0 && resync.IndexOf(0x050) > clear ? string.Join(",", lists.ConvertAll(r => r.Buffer[5])) : "-");
        }

        // C2S 0x051: an equipset puts the dagger and ring on in one packet; validate() refuses a bad count, slot or container
        private static void VerifyEquipsetSet(string dataDir, Grades grades)
        {
            Console.WriteLine("equipset set (C2S 0x051):");
            var db = new StubDatabase(Path.Combine(Path.GetDirectoryName(dataDir), "sql"));
            var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
            var c = Fixture(grades);
            stats.EquipAtLogin(c);
            c.SyncedContainers = GameOkPackets.AllSyncedMask;

            var log = new List<string>();
            var zone = new ZoneHost(db, new CharLoader(null, grades, new AbilityTable(), stats), dataDir, true, log.Add);
            var s = new MapSession { Char = c };

            List<int> Send(byte count, params (byte Index, byte Kind, byte Category)[] items)
            {
                var b = new byte[72];
                b[0] = 0x51; b[1] = 72 / 4 << 1;
                b[4] = count;
                for (int i = 0; i < items.Length; i++)
                {
                    b[8 + i * 4] = items[i].Index; b[9 + i * 4] = items[i].Kind; b[10 + i * 4] = items[i].Category;
                }
                s.PacketList.Clear();
                zone.Dispatch(s, 0x051, new BasicPacket(b));
                zone.AfterParse(s);
                return new List<BasicPacket>(s.PacketList).ConvertAll(r => Opcode(r.Buffer));
            }
            bool Rejected(Func<List<int>> send)
            {
                int before = log.Count;
                var got = send();
                return got.Count == 0 && log.Count > before && log[^1].StartsWith("zone: Invalid");
            }

            var got = Send(2, (5, 0, 0), (7, 13, 0));
            Check("an EQUIP_LIST per slot", got.FindAll(o => o == 0x050).Count, 2);
            Check("ends with GROUP_ATTR", got.Count > 0 ? got[^1] : 0, 0x037);
            Check("dagger in main, ring in ring1",
                  c.Equip.Exists(e => e.EquipSlot == 0 && e.InvSlot == 5) && c.Equip.Exists(e => e.EquipSlot == 13 && e.InvSlot == 7) ? 1 : 0, 1);

            Check("count 0 refused", Rejected(() => Send(0)) ? 1 : 0, 1);
            Check("count 17 refused", Rejected(() => Send(17)) ? 1 : 0, 1);
            Check("slot 18 refused", Rejected(() => Send(1, (5, 18, 0))) ? 1 : 0, 1);
            Check("the mog safe refused", Rejected(() => Send(1, (5, 0, 1))) ? 1 : 0, 1);
            foreach (var line in log)
                if (line.StartsWith("zone: Bad")) { fail_++; Console.WriteLine("  FAIL " + line); }
        }

        // C2S 0x083-0x085: xi.shop.general stocks and opens a Windurst shop (fame rank 6), then a purchase, the guild and job
        // restrictions, a slot past the stock, a sale offered and confirmed, a no-sale item, and what validate() refuses
        private static void VerifyShop(string dataDir, Grades grades)
        {
            Console.WriteLine("NPC shop (C2S 0x083 0x084 0x085):");
            var db = new StubDatabase(Path.Combine(Path.GetDirectoryName(dataDir), "sql"));
            var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
            var c = Fixture(grades);
            c.SyncedContainers = GameOkPackets.AllSyncedMask;
            c.FindItem(Container.Inventory, 0).Quantity = 1000;

            var log = new List<string>();
            var zone = new ZoneHost(db, new CharLoader(null, grades, new AbilityTable(), stats), dataDir, true, log.Add);
            using var scripts = new XI.Script.ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
            zone.InitScripts(scripts);
            var s = new MapSession { Char = c };

            var sent = new List<BasicPacket>();
            List<int> Send(ushort opcode, byte[] b)
            {
                b[0] = (byte)opcode; b[1] = (byte)(b.Length / 4 << 1 | opcode >> 8);
                s.PacketList.Clear();
                zone.Dispatch(s, opcode, new BasicPacket(b));
                sent = new List<BasicPacket>(s.PacketList);
                return sent.ConvertAll(r => Opcode(r.Buffer));
            }
            List<int> Buy(uint num, ushort index, byte property = 0)
            {
                var b = new byte[16];
                BitConverter.GetBytes(num).CopyTo(b, 4);
                BitConverter.GetBytes(index).CopyTo(b, 10);
                b[12] = property;
                return Send(0x083, b);
            }
            List<int> SellReq(uint num, ushort itemNo, byte index)
            {
                var b = new byte[12];
                BitConverter.GetBytes(num).CopyTo(b, 4);
                BitConverter.GetBytes(itemNo).CopyTo(b, 8);
                b[10] = index;
                return Send(0x084, b);
            }
            List<int> SellSet(ushort flag)
            {
                var b = new byte[8];
                BitConverter.GetBytes(flag).CopyTo(b, 4);
                return Send(0x085, b);
            }
            BasicPacket Last(int opcode) => sent.FindLast(p => Opcode(p.Buffer) == opcode);
            bool Rejected(Func<List<int>> send)
            {
                int before = log.Count;
                var got = send();
                return got.Count == 0 && log.Count > before && log[^1].StartsWith("zone: Invalid");
            }
            uint Gil() => c.FindItem(Container.Inventory, 0).Quantity;

            // (this also tells the zone the char's session, which the bindings push to)
            Check("PropertyItemIndex 1 refused", Rejected(() => Buy(1, 0, 1)) ? 1 : 0, 1);

            s.PacketList.Clear();
            try
            {
                scripts.Lua.DoString(@"
                    local player = ...
                    require('scripts/globals/shop')
                    xi.shop.general(player, {
                        { 4509, 10 },
                        { 4096, 100, { guild = 50, rank = 1 } },
                        { 16482, 500, { job = 1, level = 50 } },
                    }, xi.fameArea.WINDURST)", "=shop", 0, () => { scripts.Lua.PushObject(c); return 1; });
            }
            catch (XI.Script.LuaException e)
            {
                fail_++;
                Console.WriteLine("  FAIL xi.shop.general: " + e.Message);
                return;
            }
            sent = new List<BasicPacket>(s.PacketList);
            var opened = sent.ConvertAll(r => Opcode(r.Buffer));
            Check("SHOP_OPEN then SHOP_LIST", opened.Count == 2 && opened[0] == 0x03E && opened[1] == 0x03C ? 1 : 0, 1);
            if (opened.Count == 2)
            {
                var list = sent[1].Buffer;
                Check("ShopListNum", BitConverter.ToUInt16(sent[0].Buffer, 4), 3);
                Check("list size", (int)sent[1].GetSize(), 8 + 3 * 12);
                Check("list flags", list[6], 0x89);
                // price rank 6: buy prices x1.05, floored
                Check("water price", (int)BitConverter.ToUInt32(list, 8), 10);
                Check("water item", BitConverter.ToUInt16(list, 12), 4509);
                Check("crystal price", (int)BitConverter.ToUInt32(list, 20), 105);
                Check("crystal guild skill/info", BitConverter.ToUInt16(list, 28) << 16 | BitConverter.ToUInt16(list, 30), 50 << 16 | 200);
                Check("dagger index", list[8 + 24 + 6], 2);
                Check("dagger job/level", BitConverter.ToUInt16(list, 40) << 16 | BitConverter.ToUInt16(list, 42), 65 << 16 | 50);
            }

            // 13 water is more than a stack: 12 bought for 120 into the first free slot
            var got = Buy(13, 0);
            Check("buy ends with SHOP_BUY, ITEM_SAME", got.Count >= 2 && got[^2] == 0x03F && got[^1] == 0x01D ? 1 : 0, 1);
            Check("SHOP_BUY count", (int)(Last(0x03F) is BasicPacket bought ? BitConverter.ToUInt32(bought.Buffer, 8) : 0), 12);
            Check("gil after buying", (int)Gil(), 880);
            Check("water in slot 9", (int)(c.FindItem(Container.Inventory, 9) is Item water && water.ItemId == 4509 ? water.Quantity : 0), 12);

            Check("guild rank 0 cannot buy the crystals", Buy(1, 1).Count == 0 && log[^1].Contains("without meeting shop requirement") ? 1 : 0, 1);
            Check("WAR 1 cannot buy the dagger", Buy(1, 2).Count == 0 && log[^1].Contains("without meeting shop requirement") ? 1 : 0, 1);
            c.JobLevels[1] = 50;
            got = Buy(1, 2);
            Check("WAR 50 buys the dagger", got.Contains(0x03F) && (int)Gil() == 880 - 525 ? 1 : 0, 1);
            Check("past the stock", Buy(1, 3).Count == 0 && log[^1].Contains("invalid slot") ? 1 : 0, 1);

            Check("sell set needs a sell request first", Rejected(() => SellSet(1)) ? 1 : 0, 1);
            // 5 water offered: base price 3 at rank 6 is 2 each
            got = SellReq(5, 4509, 9);
            Check("SHOP_SELL", got.Count == 1 && got[0] == 0x03D ? 1 : 0, 1);
            if (got.Count == 1)
                Check("sell price/slot", (int)BitConverter.ToUInt32(sent[0].Buffer, 4) << 8 | sent[0].Buffer[8], 2 << 8 | 9);
            Check("SellFlag 0 refused", Rejected(() => SellSet(0)) ? 1 : 0, 1);
            SellReq(5, 4509, 9);
            got = SellSet(1);
            Check("sold: message then ITEM_SAME", got.Count >= 2 && got[^2] == 0x009 && got[^1] == 0x01D ? 1 : 0, 1);
            if (Last(0x009) is BasicPacket msg)
            {
                Check("sell message", BitConverter.ToUInt16(msg.Buffer, 10), 232);
                string text = System.Text.Encoding.ASCII.GetString(msg.Buffer, 13, (msg.Buffer[1] >> 1) * 4 - 13).TrimEnd('\0');
                Check($"sell text '{text}'", text == "Para0 4509 Para1 5" ? 1 : 0, 1);
            }
            Check("gil after selling", (int)Gil(), 355 + 10);
            Check("water left", (int)(c.FindItem(Container.Inventory, 9)?.Quantity ?? 0), 7);

            // the coupon cannot be sold, and nothing is put aside for a sell set
            Check("no-sale item", SellReq(1, 536, 8).Count, 0);
            Check("sell request for the wrong item", SellReq(1, 4096, 9).Count, 0);
            foreach (var line in log)
                if (line.StartsWith("zone: Bad") || line.StartsWith("lua:") && !line.Contains("global scripts")) { fail_++; Console.WriteLine("  FAIL " + line); }
        }

        // C2S 0x096: an unknown recipe and one past the char's skill are cancelled; Arrowwood Lumber (wind crystal + log,
        // Woodworking 2) greys out its stacks, uses the crystal and plays the effect, and the zone tick 16s later hands
        // out the lumber; a second synth breaks and loses the log
        private static void VerifySynth(string dataDir, Grades grades)
        {
            Console.WriteLine("synthesis (C2S 0x096):");
            var db = new StubDatabase(Path.Combine(Path.GetDirectoryName(dataDir), "sql"));
            var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
            var c = Fixture(grades);
            c.SyncedContainers = GameOkPackets.AllSyncedMask;
            c.Items.Add(new Item { Location = Container.Inventory, Slot = 10, ItemId = 4098, Quantity = 3 });
            c.Items.Add(new Item { Location = Container.Inventory, Slot = 11, ItemId = 688, Quantity = 2 });
            c.Items.Add(new Item { Location = Container.Inventory, Slot = 12, ItemId = 699, Quantity = 1 });

            var log = new List<string>();
            var zone = new ZoneHost(db, new CharLoader(null, grades, new AbilityTable(), stats), dataDir, true, log.Add);
            var s = new MapSession { Char = c };
            long now = 1_000_000;
            zone.NowMs = () => now;
            double roll = 0.0;
            zone.Random01 = () => roll;

            var sent = new List<BasicPacket>();
            List<int> Collect()
            {
                sent = new List<BasicPacket>(s.PacketList);
                return sent.ConvertAll(r => Opcode(r.Buffer));
            }
            List<int> Combine(ushort ingredient, byte slot)
            {
                var b = new byte[36];
                b[0] = 0x96; b[1] = 36 / 4 << 1;
                BitConverter.GetBytes((ushort)4098).CopyTo(b, 6);
                b[8] = 10;
                b[9] = 1;
                BitConverter.GetBytes(ingredient).CopyTo(b, 10);
                b[26] = slot;
                s.PacketList.Clear();
                zone.Dispatch(s, 0x096, new BasicPacket(b));
                return Collect();
            }
            List<int> Tick()
            {
                s.PacketList.Clear();
                zone.Tick();
                return Collect();
            }
            BasicPacket Last(int opcode) => sent.FindLast(p => Opcode(p.Buffer) == opcode);
            int AnsResult() => Last(0x06F) is BasicPacket ans ? ans.Buffer[4] : -1;
            uint Qty(byte slot) => c.FindItem(Container.Inventory, slot)?.Quantity ?? 0;

            Combine(536, 8);
            Check("unknown recipe cancelled (3)", AnsResult(), 3);
            Combine(699, 12);
            Check("Oak Lumber needs Woodworking 35 (6)", AnsResult(), 6);
            Check("nothing used by the cancels", (int)(Qty(10) << 8 | Qty(11)), 3 << 8 | 2);

            var got = Combine(688, 11);
            Check("both stacks greyed out", got.FindAll(o => o == 0x01F).Count, 2);
            Check("effect played", got.Contains(0x030) ? 1 : 0, 1);
            if (Last(0x030) is BasicPacket effect)
                Check("wind effect, success, synth animation", BitConverter.ToUInt16(effect.Buffer, 10) << 16 | effect.Buffer[12] << 8 | effect.Buffer[13], 0x11 << 16 | 0 << 8 | 44);
            Check("one crystal used", (int)Qty(10), 2);
            Check("synth open, animation 44", c.Synthesizing && c.Animation == 44 ? 1 : 0, 1);
            Check("a second synth is refused while crafting", Combine(688, 11).Count == 0 && log[^1].StartsWith("zone: Invalid") ? 1 : 0, 1);

            now += 15999;
            Check("not done before 16s", Tick().Contains(0x06F) ? 1 : 0, 0);
            now += 1;
            got = Tick();
            Check("COMBINE_ANS success", AnsResult(), 0);
            if (Last(0x06F) is BasicPacket done)
            {
                Check("result item/count", BitConverter.ToUInt16(done.Buffer, 8) << 8 | done.Buffer[6], 705 << 8 | 1);
                Check("UpKind woodworking, crystal, ingredient", done.Buffer[26] << 24 | BitConverter.ToUInt16(done.Buffer, 34) << 8 ^ BitConverter.ToUInt16(done.Buffer, 36),
                      49 << 24 | 4098 << 8 ^ 688);
            }
            Check("log used", (int)Qty(11), 1);
            Check("lumber in slot 9", c.FindItem(Container.Inventory, 9)?.ItemId ?? 0, 705);
            Check("woodworking 0.1 gained", c.RealSkills[49], 1);
            Check("skill-up message 38", sent.Exists(p => Opcode(p.Buffer) == 0x029 && BitConverter.ToUInt16(p.Buffer, 24) == 38) ? 1 : 0, 1);
            Check("stacks ungreyed, ITEM_SAME", got.Contains(0x01F) && got.Contains(0x01D) ? 1 : 0, 1);
            Check("synth closed, animation 0", !c.Synthesizing && c.Animation == 0 ? 1 : 0, 1);
            Check("nothing left claimed", c.Items.Exists(i => i.InTransaction) ? 1 : 0, 0);

            // a break roll: the second log is lost (a roll of 0 is under the 50% loss)
            // the 15s between synths is from the last start, so only a synth cut short can run into it
            c.LastSynthTimeMs = now - 14999;
            Combine(688, 11);
            Check("too soon after the last synth (94)", sent.Exists(p => Opcode(p.Buffer) == 0x029 && BitConverter.ToUInt16(p.Buffer, 24) == 94) ? 1 : 0, 1);
            c.LastSynthTimeMs = now - 15000;
            roll = 0.99;
            Combine(688, 11);
            if (Last(0x030) is BasicPacket failEffect) Check("effect shows the failure", failEffect.Buffer[12], 1);
            roll = 0.0;
            now += 16000;
            Tick();
            Check("COMBINE_ANS failed", AnsResult(), 1);
            if (Last(0x06F) is BasicPacket failed)
                Check("mangled mess, log broken", BitConverter.ToUInt16(failed.Buffer, 8) << 16 | BitConverter.ToUInt16(failed.Buffer, 10), 29695 << 16 | 688);
            Check("log gone", c.FindItem(Container.Inventory, 11) == null ? 1 : 0, 1);
            Check("crystals left", (int)Qty(10), 1);
            foreach (var line in log)
                if (line.StartsWith("zone: Bad") || line.Contains("Synth") && line.Contains("kept")) { fail_++; Console.WriteLine("  FAIL " + line); }
        }

        // C2S 0x0AA-0x0AD against Babubu (Port Windurst) with a stock the test sets: the lists, a purchase, a sale that
        // spans two stacks from the hinted one, a refusal that leaves the stack unclaimed, and the shop closing
        private static void VerifyGuild(string dataDir, Grades grades)
        {
            Console.WriteLine("guild shops (C2S 0x0AA-0x0AD):");
            var db = new StubDatabase(Path.Combine(Path.GetDirectoryName(dataDir), "sql"));
            var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
            var c = Fixture(grades);
            c.SyncedContainers = GameOkPackets.AllSyncedMask;
            c.FindItem(Container.Inventory, 0).Quantity = 1000;
            c.Items.Add(new Item { Location = Container.Inventory, Slot = 10, ItemId = 4509, Quantity = 2 });
            c.Items.Add(new Item { Location = Container.Inventory, Slot = 11, ItemId = 4509, Quantity = 12 });

            var log = new List<string>();
            var zone = new ZoneHost(db, new CharLoader(null, grades, new AbilityTable(), stats), dataDir, true, log.Add);
            using var scripts = new XI.Script.ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
            zone.InitScripts(scripts);
            var s = new MapSession { Char = c };

            var sent = new List<BasicPacket>();
            List<int> Collect()
            {
                sent = new List<BasicPacket>(s.PacketList);
                return sent.ConvertAll(r => Opcode(r.Buffer));
            }
            List<int> Send(ushort opcode, ushort itemNo = 0, byte index = 0, byte num = 0)
            {
                // the list requests are a bare header
                bool list = opcode == 0x0AB || opcode == 0x0AD;
                var b = new byte[list ? 4 : 8];
                b[0] = (byte)opcode; b[1] = (byte)(b.Length / 4 << 1 | opcode >> 8);
                if (!list)
                {
                    BitConverter.GetBytes(itemNo).CopyTo(b, 4);
                    b[6] = index;
                    b[7] = num;
                }
                s.PacketList.Clear();
                zone.Dispatch(s, opcode, new BasicPacket(b));
                return Collect();
            }
            bool Run(string lua)
            {
                s.PacketList.Clear();
                try
                {
                    scripts.Lua.DoString("local player = ...\nlocal npc = GetNPCByID(17760306)\n" + lua, "=guild", 0,
                                         () => { scripts.Lua.PushObject(c); return 1; });
                }
                catch (XI.Script.LuaException e)
                {
                    fail_++;
                    Console.WriteLine("  FAIL lua: " + e.Message);
                    return false;
                }
                Collect();
                return true;
            }
            bool Rejected(Func<List<int>> send)
            {
                int before = log.Count;
                var got = send();
                return got.Count == 0 && log.Count > before && log[^1].StartsWith("zone: Invalid");
            }
            BasicPacket Last(int opcode) => sent.FindLast(p => Opcode(p.Buffer) == opcode);
            uint Gil() => c.FindItem(Container.Inventory, 0).Quantity;
            int Water()
            {
                int n = 0;
                foreach (var item in c.Items)
                    if (item.Location == Container.Inventory && item.ItemId == 4509) n += (int)item.Quantity;
                return n;
            }
            int Trade(int opcode) => Last(opcode) is BasicPacket t ? BitConverter.ToUInt16(t.Buffer, 4) << 16 | t.Buffer[6] << 8 | t.Buffer[7] : -1;

            // (this also tells the zone the char's session, which the bindings push to)
            Check("buy list without a guild shop refused", Rejected(() => Send(0x0AB)) ? 1 : 0, 1);

            // water: buy price 60 at 5 of 20 in stock (below the knee), flat sell price 7; the hidden crystals only
            // show in the sell list, and with none in stock are not offered
            if (!Run(@"
                if not (xi.data and xi.data.guildShops) then require('scripts/data/guild_shops') end
                require('scripts/globals/guild_shops')
                local shop = xi.data.guildShops['Babubu']
                shop.hours = { 0, 24 }
                shop.holiday = nil
                shop.stock =
                {
                    { id = 4509, initial = 5, maxStock = 20, targetStock = 5, buyMax = 100, restockRate = 0, sellPrice = 7 },
                    { id = 4096, initial = 0, maxStock = 30, targetStock = 0, buyMax = 50, restockRate = 0, sellPrice = 5, hidden = true },
                }
                xi.guildShops.state = {}
                player:setLocalVar('guildOpen', xi.guildShops.onTrigger(player, npc) and 1 or 0)")) return;
            Check("onTrigger: open", (int)(c.LocalVars.TryGetValue("guildOpen", out var open) ? open : 9), 1);
            Check("guild shop NPC remembered", (int)(c.GuildShopNpc == 17760306 ? 1 : 0), 1);
            if (Last(0x086) is BasicPacket opened)
                Check("GUILD_OPEN open 0-24", (int)(opened.Buffer[4] << 24 | (int)(BitConverter.ToUInt32(opened.Buffer, 8) & 0xFFFFFF)), 0x00FFFFFF);
            else Check("GUILD_OPEN sent", 0, 1);

            var got = Send(0x0AB);
            Check("one GUILD_BUYLIST", got.Count == 1 && got[0] == 0x083 ? 1 : 0, 1);
            if (got.Count == 1)
            {
                var b = sent[0].Buffer;
                Check("buy list size", (int)sent[0].GetSize(), 248);
                Check("buy list Count/Stat", b[244] << 8 | b[245], 1 << 8 | 0x80);
                Check("water id/count/max", BitConverter.ToUInt16(b, 4) << 16 | b[6] << 8 | b[7], 4509 << 16 | 5 << 8 | 20);
                Check("water buy price", (int)BitConverter.ToUInt32(b, 8), 60);
            }
            got = Send(0x0AD);
            Check("one GUILD_SELLLIST", got.Count == 1 && got[0] == 0x085 ? 1 : 0, 1);
            if (got.Count == 1)
            {
                var b = sent[0].Buffer;
                Check("sell list Count", b[244], 2);
                Check("water sell price", (int)BitConverter.ToUInt32(b, 8), 7);
                Check("hidden crystal price", (long)BitConverter.ToUInt32(b, 16), 0x80000005L);
            }

            Check("ItemNum 0 refused", Rejected(() => Send(0x0AA, 4509, 0, 0)) ? 1 : 0, 1);
            Check("PropertyItemIndex 1 refused", Rejected(() => Send(0x0AA, 4509, 1, 1)) ? 1 : 0, 1);
            Send(0x0AA, 4509, 0, 13);
            Check("more than a stack: GUILD_BUY -1", Trade(0x082), 0xFF);

            Send(0x0AA, 4509, 0, 3);
            Check("GUILD_BUY water, 2 left, 3 bought", Trade(0x082), 4509 << 16 | 2 << 8 | 3);
            Check("gil after buying", (int)Gil(), 1000 - 180);
            Check("water after buying", Water(), 17);

            // 4 from slot 10 (only 2 there) and the rest from slot 9, where the purchase landed
            Send(0x0AC, 4509, 10, 4);
            Check("GUILD_SELL water, 6 in stock, 4 sold", Trade(0x084), 4509 << 16 | 6 << 8 | 4);
            Check("sale ITEM_SAME", sent.Exists(p => Opcode(p.Buffer) == 0x01D) ? 1 : 0, 1);
            Check("gil after selling", (int)Gil(), 820 + 28);
            Check("hinted stack sold out", c.FindItem(Container.Inventory, 10) == null ? 1 : 0, 1);
            Check("water after selling", Water(), 13);

            // a shop the script calls closed refuses the sale; the stack is left unclaimed
            Run("xi.data.guildShops['Babubu'].hours = { 25, 26 }");
            Send(0x0AC, 4509, 11, 5);
            Check("closed: GUILD_SELL -4", Trade(0x084), 0xFC);
            Check("refused stack not claimed", c.FindItem(Container.Inventory, 11)?.InTransaction == false ? 1 : 0, 1);
            Check("water kept", Water(), 13);

            Run("xi.guildShops.onShopClose(player, npc)");
            // the hours are 25-26 now: one bit at 25, and bit 31 for the passive close
            if (Last(0x086) is BasicPacket closed)
            {
                Check("GUILD_OPEN Close", closed.Buffer[4], 1);
                Check("GUILD_OPEN close Time", (long)BitConverter.ToUInt32(closed.Buffer, 8), 0x82000000L);
            }
            else Check("GUILD_OPEN close sent", 0, 1);
            Check("guild shop cleared", (int)c.GuildShopNpc, 0);
            Check("sell list after closing refused", Rejected(() => Send(0x0AD)) ? 1 : 0, 1);
            foreach (var line in log)
                if (line.StartsWith("zone: Bad") || line.StartsWith("lua:") && !line.Contains("global scripts") || line.Contains("GuildSell")) { fail_++; Console.WriteLine("  FAIL " + line); }
        }

        // C2S 0x05D, 0x0EA, 0x113, 0x119, 0x11D, 0x0DC, 0x0F2, 0x0EB: emotes and their refusals, sitting (and what it
        // does to resting), chairs with and without their key item, the emote list, /jump, the config flags, sub-maps
        private static void VerifyMotion(string dataDir, Grades grades)
        {
            Console.WriteLine("motions and config (C2S 0x05D 0x0EA 0x113 0x119 0x11D 0x0DC 0x0F2 0x0EB):");
            var db = new StubDatabase(Path.Combine(Path.GetDirectoryName(dataDir), "sql"));
            var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
            var c = Fixture(grades);
            c.SyncedContainers = GameOkPackets.AllSyncedMask;

            var log = new List<string>();
            var zone = new ZoneHost(db, new CharLoader(null, grades, new AbilityTable(), stats), dataDir, true, log.Add);
            using var scripts = new XI.Script.ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
            zone.InitScripts(scripts);
            var s = new MapSession { Char = c };

            var sent = new List<BasicPacket>();
            List<int> Send(ushort opcode, int size, Action<byte[]> fill = null)
            {
                var b = new byte[size];
                b[0] = (byte)opcode; b[1] = (byte)(b.Length / 4 << 1 | opcode >> 8);
                fill?.Invoke(b);
                s.PacketList.Clear();
                zone.Dispatch(s, opcode, new BasicPacket(b));
                zone.AfterParse(s);
                sent = new List<BasicPacket>(s.PacketList);
                return sent.ConvertAll(r => Opcode(r.Buffer));
            }
            bool Rejected(Func<List<int>> send)
            {
                int before = log.Count;
                var got = send();
                return got.Count == 0 && log.Count > before && log[^1].StartsWith("zone: Invalid");
            }
            BasicPacket Last(int opcode) => sent.FindLast(p => Opcode(p.Buffer) == opcode);
            List<int> Motion(byte number, byte mode = 0, ushort param = 0) => Send(0x05D, 16, b =>
            {
                BitConverter.GetBytes(c.Id).CopyTo(b, 4);
                BitConverter.GetBytes(c.TargId).CopyTo(b, 8);
                b[10] = number;
                b[11] = mode;
                BitConverter.GetBytes(param).CopyTo(b, 12);
            });
            int Mes() => Last(0x05A) is BasicPacket m ? BitConverter.ToUInt16(m.Buffer, 16) << 16 | BitConverter.ToUInt16(m.Buffer, 18) : -1;
            void GiveKeyItem(int id) => c.KeyItems[id / 512 * 128 + id % 512 / 8] |= (byte)(1 << id % 8);

            Motion(8);
            Check("/wave: MOTIONMES 8", Mes(), 8 << 16);
            Check("MOTIONMES caster", Last(0x05A) is BasicPacket wave ? (int)BitConverter.ToUInt32(wave.Buffer, 4) : -1, (int)c.Id);
            Motion(2, 1);
            Check("/salute: nation as Param", Mes(), 2 << 16 | 2);
            Check("/salute: Mode Text", Last(0x05A)?.Buffer[22] ?? -1, 1);
            Check("Number past Aim refused", Rejected(() => Motion(97)) ? 1 : 0, 1);
            Check("Mode 3 refused", Rejected(() => Motion(8, 3)) ? 1 : 0, 1);
            Check("/bell without a bell: nothing", Motion(73, 0, 0x10).Count, 0);
            Check("/jobemote war (unlocked): 74, job 0", Motion(74, 0, 0x1F).Contains(0x05A) ? Mes() : -1, 74 << 16);
            Check("/jobemote pld (locked): nothing", Motion(74, 0, 0x25).Count, 0);

            Send(0x0EA, 8, b => b[4] = 0);
            Check("/sit toggle on", c.Animation, 47);
            Send(0x0EA, 8, b => b[4] = 0);
            Check("/sit toggle off", c.Animation, 0);
            c.Animation = 33;
            Send(0x0EA, 8, b => b[4] = 1);
            Check("/sit on while resting", c.Animation, 47);
            Check("sit UPDATE_HP: GROUP_ATTR + CHAR_STATUS", sent.Exists(p => Opcode(p.Buffer) == 0x0DF) && sent.Exists(p => Opcode(p.Buffer) == 0x037) ? 1 : 0, 1);
            Check("Mode 3 sit refused", Rejected(() => Send(0x0EA, 8, b => b[4] = 3)) ? 1 : 0, 1);

            Send(0x113, 12, b => { b[4] = 1; b[8] = 1; });
            Check("chair 1 without its key item: chair 0", c.Animation, 63);
            GiveKeyItem(2826);
            Send(0x113, 12, b => { b[4] = 1; b[8] = 1; });
            Check("chair 1 with Imperial Chair", c.Animation, 64);
            Send(0x113, 12, b => { b[4] = 0; b[8] = 1; });
            Check("same chair again stands up", c.Animation, 0);
            Check("ChairId 21 refused", Rejected(() => Send(0x113, 12, b => { b[4] = 1; b[8] = 21; })) ? 1 : 0, 1);

            GiveKeyItem(1738);
            GiveKeyItem(2964);
            Send(0x119, 4);
            if (Last(0x11A) is BasicPacket list)
            {
                // (the fixture char already has some of the gestures)
                long owned = 0;
                int[] gestures = { 1738, 1739, 1740, 1741, 1742, 1743, 1744, 1745, 1746, 1747, 1748, 1749, 1750, 1751, 1752, 1753, 1754, 1755, 1756, 1757, 2963, 2964 };
                for (int i = 0; i < gestures.Length; i++)
                    if (c.HasKeyItem(gestures[i])) owned |= 1L << i;
                Check("EMOTE_LIST jobs: the owned gestures, WAR + RUN among them", (long)BitConverter.ToUInt32(list.Buffer, 4), owned | 1L | 1L << 21);
                Check("EMOTE_LIST chairs: Imperial Chair", BitConverter.ToUInt16(list.Buffer, 8), 1);
            }
            else Check("EMOTE_LIST sent", 0, 1);

            Check("/jump as someone else refused", Rejected(() => Send(0x11D, 12, b =>
            {
                BitConverter.GetBytes(c.Id + 1).CopyTo(b, 4);
                BitConverter.GetBytes(c.TargId).CopyTo(b, 8);
            })) ? 1 : 0, 1);
            Send(0x11D, 12, b =>
            {
                BitConverter.GetBytes(c.Id).CopyTo(b, 4);
                BitConverter.GetBytes(c.TargId).CopyTo(b, 8);
            });
            Check("/jump: JUMP with the ActIndex", Last(0x11E) is BasicPacket jump ? BitConverter.ToUInt16(jump.Buffer, 4) : -1, c.TargId);

            // CONFIG: the flags in byte 4 (and 7), SetFlg at 16
            List<int> Config(uint flags, byte setFlg) => Send(0x0DC, 20, b =>
            {
                BitConverter.GetBytes(flags).CopyTo(b, 4);
                b[16] = setFlg;
            });
            c.Settings = 0;
            var got = Config(1u << 2, 1);
            Check("/anon on", (int)(c.Settings & 4), 4);
            Check("/anon: SYSTEMMES 175", Last(0x053) is BasicPacket anon ? BitConverter.ToUInt16(anon.Buffer, 12) : -1, 175);
            Check("/anon: CONFIG, CHAR_STATUS, CHAR_SYNC", got.Contains(0x0B4) && got.Contains(0x037) && got.Contains(0x067) ? 1 : 0, 1);
            got = Config(1u << 2, 1);
            Check("unchanged: CONFIG + CHAR_STATUS only", got.Contains(0x0B4) && got.Contains(0x037) && !got.Contains(0x067) ? 1 : 0, 1);
            Config(1u << 0, 1);
            Config(1u << 29, 1);
            Check("/rec clears /invite", (int)(c.Settings & (1u | 1u << 29)), (int)(1u << 29));
            Config(1u << 25, 1);
            Check("/mentor before unlocking: off", (int)(c.Settings >> 25 & 1), 0);
            Config(1u << 26, 1);
            Config(1u << 26, 2);
            Check("new adventurer mark stays off", (int)(c.Settings >> 26 & 1), 1);
            Check("SetFlg 3 refused", Rejected(() => Config(1, 3)) ? 1 : 0, 1);

            // the C++ Event-state check is currentEvent != nullptr, and currentEvent is never null
            Send(0x0F2, 8, b => { b[4] = 2; b[6] = 5; });
            Check("SUBMAPCHANGE Event outside an event accepted", c.Boundary, 5);
            Check("SUBMAPCHANGE State 3 refused", Rejected(() => Send(0x0F2, 8, b => { b[4] = 3; b[6] = 4; })) ? 1 : 0, 1);
            Send(0x0F2, 8, b => { b[4] = 1; b[6] = 3; });
            Check("SUBMAPCHANGE boundary", c.Boundary, 3);
            Check("REQSUBMAPNUM outside an event: nothing", Send(0x0EB, 4).Count, 0);

            // GP_CLI_COMMAND_ACTION Blockaid / Help
            List<int> Action(ushort actionId, uint status = 0) => Send(0x01A, 28, b =>
            {
                BitConverter.GetBytes(c.Id).CopyTo(b, 4);
                BitConverter.GetBytes(actionId).CopyTo(b, 10);
                BitConverter.GetBytes(status).CopyTo(b, 12);
            });
            ushort StdMsg() => Last(0x053) is BasicPacket m ? BitConverter.ToUInt16(m.Buffer, 12) : (ushort)0;
            Action(0x18, 1);
            Check("/blockaid on: activated (221)", c.BlockingAid ? StdMsg() : 0, 221);
            Check("/blockaid on again: nothing", Action(0x18, 1).Count, 0);
            Action(0x18, 2);
            Check("/blockaid toggle: inactive (224)", c.BlockingAid ? 0 : StdMsg(), 224);
            Check("/blockaid off while off: nothing", Action(0x18, 0).Count, 0);
            Action(0x18, 2);
            Action(0x18, 0);
            Check("/blockaid off: canceled (222)", c.BlockingAid ? 0 : StdMsg(), 222);
            var tags = new StatusEffect(EffectId.AlliedTags, EffectId.AlliedTags, 0, 0, 0);
            Check("allied tags on", c.StatusEffects.AddStatusEffect(tags, EffectNotice.Silent) ? 1 : 0, 1);
            Action(0x18, 1);
            Check("/blockaid on under allied tags: cannot use (142), still off", c.BlockingAid ? 0 : StdMsg(), 142);
            c.StatusEffects.DelStatusEffectSilent(EffectId.AlliedTags);
            Action(0x05);
            Check("call for help without a mob: cannot (22)", Last(0x029) is BasicPacket bm ? BitConverter.ToUInt16(bm.Buffer, 24) : 0, 22);

            // GP_CLI_COMMAND_SET_USERMSG checks msgType as the whole u32
            Check("SET_USERMSG msgType 0x111 refused", Rejected(() => Send(0x0E0, 152, b => { b[4] = (byte)'h'; b[148] = 0x11; b[149] = 0x01; })) ? 1 : 0, 1);

            // GP_CLI_COMMAND_POS waits for a pushed 0x05B of the char itself to go out
            {
                var b = new byte[32];
                b[0] = 0x15; b[1] = 32 / 4 << 1;
                BitConverter.GetBytes(c.X + 3).CopyTo(b, C2sPos.OffX);
                BitConverter.GetBytes(c.Y).CopyTo(b, C2sPos.OffZ);
                BitConverter.GetBytes(c.Z).CopyTo(b, C2sPos.OffY);
                float x = c.X;
                var w = new byte[28];
                w[0] = 0x5B; w[1] = 28 / 4 << 1;
                BitConverter.GetBytes(c.Id).CopyTo(w, 0x10);
                s.PacketList.Clear();
                s.PacketList.AddLast(new BasicPacket(w));
                zone.Dispatch(s, 0x015, new BasicPacket(b));
                Check("POS ignored while a 0x05B is pending", c.X == x ? 1 : 0, 1);
                s.PacketList.Clear();
                zone.Dispatch(s, 0x015, new BasicPacket(b));
                Check("POS applied once it went out", c.X == x + 3 ? 1 : 0, 1);
            }

            // leavegame.lua onEffectGain: a GM leaves at once, without the countdown
            c.AccountGmLevel = 1;
            var gmLogout = Send(0x0E7, 8, b => { b[4] = 1; b[6] = 1; });
            Check("GM /logout: no countdown message, no healing", gmLogout.Contains(0x053) || c.Animation == 33 ? 1 : 0, 0);
            s.PacketList.Clear();
            zone.Tick();
            Check("GM /logout: logged out on the next tick", s.PacketList.Count > 0 && Opcode(s.PacketList.Last.Value.Buffer) == 0x00B ? 1 : 0, 1);
            foreach (var line in log)
                if (line.StartsWith("zone: Bad") || line.StartsWith("lua:") && !line.Contains("global scripts")) { fail_++; Console.WriteLine("  FAIL " + line); }
        }

        // the menus the client fills from the server: key items read, currencies, map markers, recipes, the equipset
        // editor's check, plus the event packets that need an event to be open
        private static void VerifyMenus(string dataDir, Grades grades)
        {
            Console.WriteLine("menus (C2S 0x064 0x10F 0x115 0x114 0x058 0x052 0x0D8 0x05C 0x060):");
            var db = new StubDatabase(Path.Combine(Path.GetDirectoryName(dataDir), "sql"));
            var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
            var c = Fixture(grades);
            c.SyncedContainers = GameOkPackets.AllSyncedMask;
            // a claymore in inventory 20, a lauan shield in 21
            c.Items.Add(new Item { Location = Container.Inventory, Slot = 20, ItemId = 16583, Quantity = 1 });
            c.Items.Add(new Item { Location = Container.Inventory, Slot = 21, ItemId = 12289, Quantity = 1 });

            var log = new List<string>();
            var zone = new ZoneHost(db, new CharLoader(null, grades, new AbilityTable(), stats), dataDir, true, log.Add);
            using var scripts = new XI.Script.ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
            zone.InitScripts(scripts);
            var s = new MapSession { Char = c };

            var sent = new List<BasicPacket>();
            List<int> Send(ushort opcode, int size, Action<byte[]> fill = null)
            {
                var b = new byte[size];
                b[0] = (byte)opcode; b[1] = (byte)(b.Length / 4 << 1 | opcode >> 8);
                fill?.Invoke(b);
                s.PacketList.Clear();
                zone.Dispatch(s, opcode, new BasicPacket(b));
                zone.AfterParse(s);
                sent = new List<BasicPacket>(s.PacketList);
                return sent.ConvertAll(r => Opcode(r.Buffer));
            }
            bool Rejected(Func<List<int>> send)
            {
                int before = log.Count;
                var got = send();
                return got.Count == 0 && log.Count > before && log[^1].StartsWith("zone: Invalid");
            }
            BasicPacket Last(int opcode) => sent.FindLast(p => Opcode(p.Buffer) == opcode);
            int Size(BasicPacket p) => p == null ? -1 : (p.Buffer[1] & 0xFE) * 2;

            // SCENARIOITEM: bit 3 of the first word marks key item 3 of table 0 as read
            List<int> Scenario(ushort table, uint id, ushort targ) => Send(0x064, 76, b =>
            {
                BitConverter.GetBytes(id).CopyTo(b, 4);
                BitConverter.GetBytes(1u << 3).CopyTo(b, 8);
                BitConverter.GetBytes(1u << 0).CopyTo(b, 12);
                BitConverter.GetBytes(targ).CopyTo(b, 72);
                BitConverter.GetBytes(table).CopyTo(b, 74);
            });
            c.KeyItems[64] = 0; c.KeyItems[2 * 128 + 64 + 4] = 0;
            Scenario(0, c.Id, c.TargId);
            Check("SCENARIOITEM: key item 3 seen", c.KeyItems[64] >> 3 & 1, 1);
            Scenario(2, c.Id, c.TargId);
            Check("SCENARIOITEM: table 2, key item 32 seen", c.KeyItems[2 * 128 + 64 + 4] & 1, 1);
            Check("TableIndex 8 refused", Rejected(() => Scenario(8, c.Id, c.TargId)) ? 1 : 0, 1);
            Check("someone else's id refused", Rejected(() => Scenario(0, c.Id + 1, c.TargId)) ? 1 : 0, 1);

            Send(0x10F, 4);
            Check("CURRENCIES_1: 0x113, 252 bytes", Size(Last(0x113)), 252);
            Send(0x115, 4);
            Check("CURRENCIES_2: 0x118, 160 bytes", Size(Last(0x118)), 160);
            Check("MAP_MARKERS: the homepoints", Send(0x114, 4).Contains(0x063) ? Last(0x063).Buffer[4] : -1, 6);

            // RECIPE: skill 4, level 6, Param0 8, Mode 10, Param1 12, Param3 16, Param4 18
            List<int> Recipe(ushort mode, ushort level, ushort param1 = 0, ushort param3 = 0, ushort param4 = 0) => Send(0x058, 20, b =>
            {
                BitConverter.GetBytes((ushort)1).CopyTo(b, 4);
                BitConverter.GetBytes(level).CopyTo(b, 6);
                BitConverter.GetBytes(mode).CopyTo(b, 10);
                BitConverter.GetBytes(param1).CopyTo(b, 12);
                BitConverter.GetBytes(param3).CopyTo(b, 16);
                BitConverter.GetBytes(param4).CopyTo(b, 18);
            });
            int RecipeType() => Last(0x031) is BasicPacket r && Size(r) == 52 ? BitConverter.ToUInt16(r.Buffer, 48) : -1;
            Recipe(1, 25);
            Check("RECIPE random: RecipeDetail1", RecipeType(), 1);
            Recipe(2, 25, 16, 0, 1);
            Check("RECIPE list: RecipeList", RecipeType(), 2);
            Check("RECIPE list: a full page", Last(0x031) is BasicPacket list && BitConverter.ToUInt16(list.Buffer, 16 + 15 * 2) != 0 ? 1 : 0, 1);
            Recipe(3, 25, 0, 4, 1);
            Check("RECIPE detail: RecipeDetail2", RecipeType(), 3);
            Check("list offset not a multiple of 16 refused", Rejected(() => Recipe(2, 25, 5, 0, 1)) ? 1 : 0, 1);
            Check("Mode 4 refused", Rejected(() => Recipe(4, 25)) ? 1 : 0, 1);
            Check("level 111 refused", Rejected(() => Recipe(1, 111)) ? 1 : 0, 1);

            // EQUIPSET_CHECK: EquipKind 4, ItemChange 8 (flags, index, item), the set's 16 items from 12
            List<int> Equipset(byte slot, byte flags, byte index, ushort item, Action<byte[]> set = null) => Send(0x052, 76, b =>
            {
                b[4] = slot;
                b[8] = flags; b[9] = index;
                BitConverter.GetBytes(item).CopyTo(b, 10);
                set?.Invoke(b);
            });
            Action<byte[]> withShield = b => { b[12 + 4] = 1; b[12 + 5] = 21; BitConverter.GetBytes((ushort)12289).CopyTo(b, 12 + 6); };
            Equipset(0, 1, 20, 16583, withShield);
            if (Last(0x116) is BasicPacket valid && Size(valid) == 72)
            {
                Check("claymore in main", BitConverter.ToUInt16(valid.Buffer, 6) == 16583 && valid.Buffer[4] == 1 && valid.Buffer[5] == 20 ? 1 : 0, 1);
                Check("the two-handed weapon takes out the shield", BitConverter.ToUInt16(valid.Buffer, 10), 0);
            }
            else Check("EQUIPSET_VALID sent", 0, 1);
            Equipset(1, 1, 21, 12289);
            Check("shield in sub", Last(0x116) is BasicPacket sub ? BitConverter.ToUInt16(sub.Buffer, 10) : -1, 12289);
            Equipset(0, 1, 20, 16535, withShield);
            Check("item id not the one held: empty reply", Last(0x116) is BasicPacket bad && BitConverter.ToUInt32(bad.Buffer, 4) == 0 && BitConverter.ToUInt32(bad.Buffer, 8) == 0 ? 1 : 0, 1);
            Equipset(5, 2, 0, 0);
            Check("slot disabled", Last(0x116)?.Buffer[4 + 5 * 4] ?? -1, 2);
            Check("container 63 refused", Rejected(() => Equipset(0, 63 << 2 | 1, 5, 16583)) ? 1 : 0, 1);
            Check("EquipKind 16 refused", Rejected(() => Equipset(16, 1, 5, 16583)) ? 1 : 0, 1);

            Send(0x0D8, 40, b =>
            {
                BitConverter.GetBytes(c.TargId).CopyTo(b, 4);
                BitConverter.GetBytes(c.Id).CopyTo(b, 12);
            });
            Check("DUNGEON_PARAM: logged only", log[^1].Contains("DUNGEON_PARAM: Not implemented") ? 1 : 0, 1);

            Check("EVENTENDXZY outside an event refused", Rejected(() => Send(0x05C, 32, b => b[30] = 1)) ? 1 : 0, 1);
            Check("PASSWARDS outside an event refused", Rejected(() => Send(0x060, 28)) ? 1 : 0, 1);
            foreach (var line in log)
                if (line.StartsWith("zone: Bad") || line.StartsWith("lua:") && !line.Contains("global scripts") || line.Contains("needs the item tables")) { fail_++; Console.WriteLine("  FAIL " + line); }
        }

        // C2S 0x029: a whole stack to another container, a split, the halves rejoined, a full container, and the moves
        // validate() refuses
        private static void VerifyItemMove(string dataDir, Grades grades)
        {
            Console.WriteLine("item move (C2S 0x029):");
            var db = new StubDatabase(Path.Combine(Path.GetDirectoryName(dataDir), "sql"));
            var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
            var c = Fixture(grades);
            c.SyncedContainers = GameOkPackets.AllSyncedMask;
            // a full stack of fire crystals in inventory 9
            c.Items.Add(new Item { Location = Container.Inventory, Slot = 9, ItemId = 4096, Quantity = 12 });

            var log = new List<string>();
            var zone = new ZoneHost(db, new CharLoader(null, grades, new AbilityTable(), stats), dataDir, true, log.Add);
            var s = new MapSession { Char = c };

            var sent = new List<BasicPacket>();
            List<int> Send(uint num, byte cat1, byte cat2, byte index1, byte index2)
            {
                var b = new byte[12];
                b[0] = 0x29; b[1] = 12 / 4 << 1;
                BitConverter.GetBytes(num).CopyTo(b, 4);
                b[8] = cat1; b[9] = cat2; b[10] = index1; b[11] = index2;
                s.PacketList.Clear();
                zone.Dispatch(s, 0x029, new BasicPacket(b));
                zone.AfterParse(s);
                sent = new List<BasicPacket>(s.PacketList);
                return sent.ConvertAll(r => Opcode(r.Buffer));
            }
            byte Byte(int i, int off) => i < sent.Count ? sent[i].Buffer[off] : (byte)0xFF;
            uint U32At(int i, int off) => i < sent.Count ? BitConverter.ToUInt32(sent[i].Buffer, off) : 0xFFFFFFFF;
            void Opcodes(string label, List<int> got, params int[] want)
            {
                Check($"{label} replies", got.Count, want.Length);
                for (int i = 0; i < Math.Min(got.Count, want.Length); i++) Check($"{label} #{i} opcode", got[i], want[i]);
            }
            bool Rejected(Func<List<int>> send)
            {
                int before = log.Count;
                var got = send();
                return got.Count == 0 && log.Count > before && log[^1].StartsWith("zone: Invalid");
            }

            // the ring (inventory 7) to the wardrobe's first free slot: the old slot emptied, the new one filled
            var ring = c.FindItem(Container.Inventory, 7);
            Opcodes("ring to wardrobe", Send(1, 0, 8, 7, 82), 0x020, 0x020, 0x01D);
            Check("emptied slot category/index", Byte(0, 14) << 8 | Byte(0, 15), 0 << 8 | 7);
            Check("emptied slot has no item", BitConverter.ToUInt16(sent[0].Buffer, 12), 0);
            Check("new slot category/index/item", Byte(1, 14) << 8 | Byte(1, 15), 8 << 8 | 1);
            Check("ring now in wardrobe 1", ring.Location == Container.Wardrobe && ring.Slot == 1 ? 1 : 0, 1);

            // 5 of the 12 crystals split into the inventory's first free slot (7, which the ring left)
            Opcodes("split 5 crystals", Send(5, 0, 0, 9, 82), 0x01E, 0x020, 0x01D, 0x01D);
            Check("ITEM_NUM slot 9 now 7", U32At(0, 4), 7);
            Check("new stack slot", Byte(1, 15), 7);
            Check("new stack quantity", U32At(1, 4), 5);
            Check("two stacks", c.FindItem(Container.Inventory, 9)?.Quantity == 7 && c.FindItem(Container.Inventory, 7)?.Quantity == 5 ? 1 : 0, 1);

            // the 5 back onto the 7: the source row goes, the target fills to its stack of 12, and no ITEM_SAME follows
            Opcodes("rejoin the stacks", Send(5, 0, 0, 7, 9), 0x020, 0x01E);
            Check("source emptied", c.FindItem(Container.Inventory, 7) == null ? 1 : 0, 1);
            Check("target back to 12", c.FindItem(Container.Inventory, 9)?.Quantity ?? 0, 12);

            // more than the stack holds
            Opcodes("too many", Send(13, 0, 0, 9, 82));

            // a container with no room left: what it holds is resent, then ITEM_SAME
            c.StorageSize[(int)Container.Wardrobe2] = 0;
            Opcodes("into a full wardrobe", Send(1, 0, 10, 5, 82), 0x01D);
            Check("dagger stays", c.FindItem(Container.Inventory, 5)?.ItemId ?? 0, 16482);

            Check("gil cannot move", Rejected(() => Send(10, 0, 1, 0, 82)) ? 1 : 0, 1);
            Check("crystals cannot go in a wardrobe", Rejected(() => Send(12, 0, 8, 9, 82)) ? 1 : 0, 1);
            Check("only the locker fills the recycle bin", Rejected(() => Send(1, 0, 17, 5, 82)) ? 1 : 0, 1);
            Check("storage needs the char's own mog house", Rejected(() => Send(1, 0, 2, 5, 82)) ? 1 : 0, 1);
            c.StorageSize[(int)Container.MogLocker] = 80;
            Check("locker without a lease refused", Rejected(() => Send(1, 0, 4, 5, 82)) ? 1 : 0, 1);
            uint vanaNow = LoginPackets.VanadielTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            c.CharVars["mog-locker-expiry-timestamp"] = ((int)vanaNow - 60, 0);
            c.CharVars["mog-locker-access-type"] = (1, 0);
            c.MoghouseId = c.Id;
            Check("locker with a run-out lease refused", Rejected(() => Send(1, 0, 4, 5, 82)) ? 1 : 0, 1);
            c.CharVars["mog-locker-expiry-timestamp"] = ((int)vanaNow + 3600, 0);
            Opcodes("dagger into the leased locker (own mog house)", Send(1, 0, 4, 5, 82), 0x020, 0x020, 0x01D);
            Check("dagger now in the locker", c.FindItem(Container.MogLocker, 1)?.ItemId ?? 0, 16482);
            foreach (var line in log)
                if (line.StartsWith("zone: Bad")) { fail_++; Console.WriteLine("  FAIL " + line); }
        }

        // C2S 0x028: gil and a filled storage slip refused, part of a stack destroyed outside the inventory, inventory items
        // to the recycle bin (and the full bin's slide), a no-recycle item destroyed, and what validate() refuses
        private static void VerifyItemDump(string dataDir, Grades grades)
        {
            Console.WriteLine("item dump (C2S 0x028):");
            var db = new StubDatabase(Path.Combine(Path.GetDirectoryName(dataDir), "sql"));
            var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
            var c = Fixture(grades);
            c.SyncedContainers = GameOkPackets.AllSyncedMask;
            c.Items.Add(new Item { Location = Container.MogCase, Slot = 1, ItemId = 4096, Quantity = 12 });
            var slip = new Item { Location = Container.Inventory, Slot = 9, ItemId = 29312, Quantity = 1 };
            slip.Extra[3] = 1;
            c.Items.Add(slip);
            c.Items.Add(new Item { Location = Container.Inventory, Slot = 10, ItemId = 513, Quantity = 1 });

            var log = new List<string>();
            var zone = new ZoneHost(db, new CharLoader(null, grades, new AbilityTable(), stats), dataDir, true, log.Add);
            var s = new MapSession { Char = c };

            var sent = new List<BasicPacket>();
            List<int> Send(uint num, byte category, byte index)
            {
                var b = new byte[12];
                b[0] = 0x28; b[1] = 12 / 4 << 1;
                BitConverter.GetBytes(num).CopyTo(b, 4);
                b[8] = category; b[9] = index;
                s.PacketList.Clear();
                zone.Dispatch(s, 0x028, new BasicPacket(b));
                zone.AfterParse(s);
                sent = new List<BasicPacket>(s.PacketList);
                return sent.ConvertAll(r => Opcode(r.Buffer));
            }
            void Opcodes(string label, List<int> got, params int[] want)
            {
                Check($"{label} replies", got.Count, want.Length);
                for (int i = 0; i < Math.Min(got.Count, want.Length); i++) Check($"{label} #{i} opcode", got[i], want[i]);
            }
            // 0x009's MesNo and its Data text, up to the packet's size
            void Message(string label, int i, int msg, string text)
            {
                if (i >= sent.Count) { Check($"{label} message sent", 0, 1); return; }
                var b = sent[i].Buffer;
                Check($"{label} message", BitConverter.ToUInt16(b, 10), msg);
                string got = System.Text.Encoding.ASCII.GetString(b, 13, (b[1] >> 1) * 4 - 13).TrimEnd('\0');
                Check($"{label} text '{got}'", got == text ? 1 : 0, 1);
            }
            bool Rejected(Func<List<int>> send)
            {
                int before = log.Count;
                var got = send();
                return got.Count == 0 && log.Count > before && log[^1].StartsWith("zone: Invalid");
            }

            Opcodes("gil", Send(1, 0, 0), 0x009);
            Message("gil", 0, 325, "Para0 65535 ");
            Opcodes("filled slip", Send(1, 0, 9), 0x009);
            Message("filled slip", 0, 325, "Para0 29312 ");

            // outside the inventory the item is destroyed, 5 of the 12 here
            Opcodes("5 crystals from the case", Send(5, 7, 1), 0x01E, 0x009, 0x01D);
            Check("crystals left", c.FindItem(Container.MogCase, 1)?.Quantity ?? 0, 7);
            Message("crystals", 1, 180, "Para0 4096 Para1 5");
            Opcodes("more than the stack", Send(8, 7, 1));

            // an inventory item goes whole to the bin's first free slot
            Opcodes("scroll to the bin", Send(1, 0, 6), 0x020, 0x020, 0x009, 0x01D);
            Check("scroll in bin 1", c.FindItem(Container.RecycleBin, 1)?.ItemId ?? 0, 4606);
            Message("scroll", 2, 180, "Para0 4606 Para1 1");

            // no-recycle items skip the bin
            Opcodes("linkshell", Send(1, 0, 10), 0x020, 0x009, 0x01D);
            Check("linkshell gone", c.FindItem(Container.Inventory, 10) == null && c.FindItem(Container.RecycleBin, 2) == null ? 1 : 0, 1);

            // a full bin drops its first item and slides the rest down
            for (byte slot = 2; slot <= 10; slot++)
                c.Items.Add(new Item { Location = Container.RecycleBin, Slot = slot, ItemId = (ushort)(4095 + slot), Quantity = 1 });
            Opcodes("into a full bin", Send(1, 0, 8), 0x020, 0x020, 0x020, 0x020, 0x020, 0x020, 0x020, 0x020, 0x020, 0x020, 0x020, 0x009, 0x01D);
            Check("scroll evicted", c.HasItem(4606) ? 1 : 0, 0);
            Check("bin 1 slid down", c.FindItem(Container.RecycleBin, 1)?.ItemId ?? 0, 4097);
            Check("coupon in bin 10", c.FindItem(Container.RecycleBin, 10)?.ItemId ?? 0, 536);

            Check("the bin itself is refused", Rejected(() => Send(1, 17, 1)) ? 1 : 0, 1);
            Check("more than 99 is refused", Rejected(() => Send(100, 0, 1)) ? 1 : 0, 1);
            foreach (var line in log)
                if (line.StartsWith("zone: Bad")) { fail_++; Console.WriteLine("  FAIL " + line); }
        }

        // C2S 0x03A: short stacks topped up from the later ones, then sorts within a second ignored until LIGHTLUGGAGE_BLOCK
        // logs the char out
        private static void VerifyItemStack(string dataDir, Grades grades)
        {
            Console.WriteLine("item stack (C2S 0x03A):");
            var db = new StubDatabase(Path.Combine(Path.GetDirectoryName(dataDir), "sql"));
            var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
            var c = Fixture(grades);
            c.SyncedContainers = GameOkPackets.AllSyncedMask;
            c.Items.Add(new Item { Location = Container.Inventory, Slot = 9, ItemId = 4096, Quantity = 5 });
            c.Items.Add(new Item { Location = Container.Inventory, Slot = 10, ItemId = 4096, Quantity = 4 });
            c.Items.Add(new Item { Location = Container.Inventory, Slot = 11, ItemId = 4096, Quantity = 6 });

            var log = new List<string>();
            var zone = new ZoneHost(db, new CharLoader(null, grades, new AbilityTable(), stats), dataDir, true, log.Add);
            long now = 1_000_000;
            zone.NowMs = () => now;
            var s = new MapSession { Char = c };

            var sent = new List<BasicPacket>();
            List<int> Send(uint category)
            {
                var b = new byte[8];
                b[0] = 0x3A; b[1] = 8 / 4 << 1;
                BitConverter.GetBytes(category).CopyTo(b, 4);
                s.PacketList.Clear();
                zone.Dispatch(s, 0x03A, new BasicPacket(b));
                zone.AfterParse(s);
                sent = new List<BasicPacket>(s.PacketList);
                return sent.ConvertAll(r => Opcode(r.Buffer));
            }
            void Opcodes(string label, List<int> got, params int[] want)
            {
                Check($"{label} replies", got.Count, want.Length);
                for (int i = 0; i < Math.Min(got.Count, want.Length); i++) Check($"{label} #{i} opcode", got[i], want[i]);
            }

            // 10's 4 join 9 (emptying 10), then 3 of 11's 6 fill 9 to 12
            Opcodes("sort the inventory", Send(0), 0x020, 0x01E, 0x01E, 0x01E, 0x01D);
            Check("slot 9 full", c.FindItem(Container.Inventory, 9)?.Quantity ?? 0, 12);
            Check("slot 10 emptied", c.FindItem(Container.Inventory, 10) == null ? 1 : 0, 1);
            Check("slot 11 left with 3", c.FindItem(Container.Inventory, 11)?.Quantity ?? 0, 3);

            now += 500;
            Opcodes("again within a second", Send(0));
            Opcodes("third", Send(0));
            Opcodes("fourth", Send(0));
            Check("still in game", s.ShuttingDown, 0);
            Opcodes("the fifth logs out", Send(0), 0x00B);
            Check("logging out", s.ShuttingDown, 1);
            Check("nothing refused so far", log.Exists(l => l.StartsWith("zone: Invalid")) ? 1 : 0, 0);
            Send(18);
            Check("container 18 refused", log[^1].StartsWith("zone: Invalid") ? 1 : 0, 1);
        }

        private static void VerifyChars(string dataDir, Grades grades)
        {
            Console.WriteLine("chars (SpawnPCs, 0x00D, C2S 0x00A 0x015 0x016 0x017):");
            var db = new StubDatabase(Path.Combine(Path.GetDirectoryName(dataDir), "sql"));
            var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
            var log = new List<string>();
            var zone = new ZoneHost(db, new CharLoader(null, grades, new AbilityTable(), stats), dataDir, true, log.Add);

            var a = Fixture(grades);
            var b = Fixture(grades);
            b.Id = 2; b.Name = "Kobo"; b.X = -130f;
            var sa = new MapSession { Char = a, InZone = true };
            var sb = new MapSession { Char = b };

            void Send(MapSession s, ushort opcode, int size, Action<byte[]> fill = null)
            {
                var buf = new byte[size];
                buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
                fill?.Invoke(buf);
                zone.Dispatch(s, opcode, new BasicPacket(buf));
                zone.AfterParse(s);
            }
            void Pos(MapSession s, float x) => Send(s, 0x015, 32, buf =>
            {
                var c = (CharData)s.Char;
                BitConverter.GetBytes(x).CopyTo(buf, 4);
                BitConverter.GetBytes(c.Y).CopyTo(buf, 8);
                BitConverter.GetBytes(c.Z).CopyTo(buf, 12);
                buf[20] = c.Rotation;
            });
            List<BasicPacket> CharPcs(MapSession s, uint id) =>
                new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p.Buffer) == 0x00D && BitConverter.ToUInt32(p.Buffer, 4) == id);
            int Count(MapSession s, int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p.Buffer) == opcode).Count;
            int Size(BasicPacket p) => (p.Buffer[1] & 0xFE) * 2;

            // a is in the zone; b logs in 10 yalms away and takes the next act index
            Pos(sa, a.X);
            sa.PacketList.Clear();
            Send(sb, 0x00A, 92, buf => BitConverter.GetBytes(b.Id).CopyTo(buf, 12));
            Check("second char gets act index 0x401", b.TargId, 0x401);
            var spawn = CharPcs(sa, b.Id);
            Check("a is sent b's spawn", spawn.Count, 1);
            Check("spawn SendFlg 0x1F", spawn.Count == 1 ? spawn[0].Buffer[10] : -1, 0x1F);
            Check("spawn act index", spawn.Count == 1 ? BitConverter.ToUInt16(spawn[0].Buffer, 8) : -1, 0x401);
            Check("spawn size holds the name", spawn.Count == 1 ? Size(spawn[0]) : -1, 100);
            Check("a's list has b", a.SpawnPcList.ContainsKey(b.Id) ? 1 : 0, 1);

            // b's first position update spawns a for b, with a's char sync
            sb.PacketList.Clear();
            Pos(sb, b.X);
            Check("b is sent a's spawn", CharPcs(sb, a.Id).Count, 1);
            Check("b is sent a's 0x067", Count(sb, 0x067), 1);
            Check("b's list has a", b.SpawnPcList.ContainsKey(a.Id) ? 1 : 0, 1);

            // a moves: b gets a position-only update
            sb.PacketList.Clear();
            Pos(sa, -135f);
            var upd = CharPcs(sb, a.Id);
            Check("a's move reaches b", upd.Count, 1);
            Check("update SendFlg 0x01", upd.Count == 1 ? upd[0].Buffer[10] : -1, 0x01);
            Check("update size 64", upd.Count == 1 ? Size(upd[0]) : -1, 64);
            Check("update x", upd.Count == 1 ? (long)BitConverter.ToSingle(upd[0].Buffer, 12) : 0, -135);
            // standing still is not an update
            sb.PacketList.Clear();
            Pos(sa, -135f);
            Check("no move, no update", CharPcs(sb, a.Id).Count, 0);
            // two moves before b's next datagram merge into one packet
            Pos(sa, -136f);
            Pos(sa, -137f);
            upd = CharPcs(sb, a.Id);
            Check("pending updates merge", upd.Count, 1);
            Check("merged x is the latest", upd.Count == 1 ? (long)BitConverter.ToSingle(upd[0].Buffer, 12) : 0, -137);

            // b walks 60 yalms off: a despawns for b
            sb.PacketList.Clear();
            Pos(sb, -77f);
            var gone = CharPcs(sb, a.Id);
            Check("far char despawns", gone.Count == 1 ? gone[0].Buffer[10] & 0x20 : -1, 0x20);
            Check("b's list drops a", b.SpawnPcList.ContainsKey(a.Id) ? 1 : 0, 0);
            // a's sync drops b, then b returns within 45 and a spawns it again
            sa.PacketList.Clear();
            Pos(sa, -137f);
            Check("a's list drops b", a.SpawnPcList.ContainsKey(b.Id) ? 1 : 0, 0);
            Pos(sb, -127f);
            sa.PacketList.Clear();
            Pos(sa, -138f);
            Check("b back in range respawns for a", CharPcs(sa, b.Id).Count == 1 ? CharPcs(sa, b.Id)[0].Buffer[10] : -1, 0x1F);

            // CHARREQ for b's act index; CHARREQ2 by unique number adds the sync and a name update
            sa.PacketList.Clear();
            Send(sa, 0x016, 8, buf => BitConverter.GetBytes((ushort)0x401).CopyTo(buf, 4));
            Check("CHARREQ for a char", CharPcs(sa, b.Id).Count, 1);
            sa.PacketList.Clear();
            Send(sa, 0x017, 20, buf => BitConverter.GetBytes(b.Id).CopyTo(buf, 8));
            Check("CHARREQ2: spawn + name update", CharPcs(sa, b.Id).Count, 2);
            Check("CHARREQ2: char sync", Count(sa, 0x067), 1);
            sa.PacketList.Clear();
            Send(sa, 0x017, 20, buf => { BitConverter.GetBytes((ushort)0x401).CopyTo(buf, 4); BitConverter.GetBytes(b.Id).CopyTo(buf, 8); });
            Check("CHARREQ2: the same char once", Count(sa, 0x067), 1);

            // b goes link dead (MapSessionContainer::cleanupSessions, 5 s without an update): the char_flags row,
            // a sees b's general update with LinkDeadFlag (flags1 bit 18), b its own CHAR_STATUS with it
            // (flags0 bit 26)
            uint LinkDeadUpdate(MapSession s) =>
                CharPcs(s, b.Id) is var u && u.Count == 1 && (u[0].Buffer[10] & 0x04) != 0
                    ? BitConverter.ToUInt32(u[0].Buffer, 32) >> 18 & 1 : 99;
            uint LinkDeadStatus(MapSession s) =>
                new List<BasicPacket>(s.PacketList).FindLast(p => Opcode(p.Buffer) == 0x037) is BasicPacket st
                    ? BitConverter.ToUInt32(st.Buffer, 40) >> 26 & 1 : 99;
            string Flags() => string.Join(",", db.Writes.FindAll(w => w.Contains("char_flags")));
            const string FlagsRow = "UPDATE char_flags SET disconnecting = ? WHERE charid = ?";
            sa.PacketList.Clear();
            sb.PacketList.Clear();
            db.Writes.Clear();
            sb.CharId = b.Id;
            zone.OnLinkDead(sb, false);
            Check("not link dead and fresh: nothing", $"{b.IsLinkDead}/{Flags()}/{CharPcs(sa, b.Id).Count}",
                  "False//0");
            zone.OnLinkDead(sb, true);
            Check("link dead: the flag and the row", $"{b.IsLinkDead}/{Flags()}", $"True/{FlagsRow}|1|2");
            Check("link dead: a sees b's LinkDeadFlag", LinkDeadUpdate(sa), 1);
            Check("link dead: b's CHAR_STATUS LinkDeadFlag", LinkDeadStatus(sb), 1);
            sa.PacketList.Clear();
            db.Writes.Clear();
            zone.OnLinkDead(sb, true);
            Check("still link dead: flagged once", $"{Flags()}/{CharPcs(sa, b.Id).Count}", "/0");
            zone.OnLinkDead(sb, false);
            Check("back: the flag and the row cleared", $"{b.IsLinkDead}/{Flags()}", $"False/{FlagsRow}|0|2");
            bool StatsSaved() => db.Writes.Exists(w => w.StartsWith("UPDATE char_stats SET hp = ?"));
            Check("back: the stats saved", StatsSaved() ? 1 : 0, 1);
            Check("back: a sees b without LinkDeadFlag", LinkDeadUpdate(sa), 0);
            Check("back: b's CHAR_STATUS without it", LinkDeadStatus(sb), 0);
            // a char already out of its zone (a zone change or a logout before its 0x00B; removeCharFromZone nulled
            // loc.zone): the row and the stats are written, nothing is sent
            sb.InZone = false;
            sa.PacketList.Clear();
            sb.PacketList.Clear();
            db.Writes.Clear();
            string Sent() => $"{Count(sb, 0x037)}/{Count(sb, 0x0DD)}/{Count(sb, 0x00D)}/{CharPcs(sa, b.Id).Count}";
            zone.OnLinkDead(sb, true);
            Check("out of its zone, link dead: the row, nothing sent", $"{b.IsLinkDead}/{Flags()}/{Sent()}",
                  $"True/{FlagsRow}|1|2/0/0/0/0");
            db.Writes.Clear();
            zone.OnLinkDead(sb, false);
            Check("out of its zone, back: the row and the stats, nothing sent",
                  $"{b.IsLinkDead}/{Flags()}/{StatsSaved()}/{Sent()}", $"False/{FlagsRow}|0|2/True/0/0/0/0");
            sb.InZone = true;

            // b times out: a gets b's despawn
            sa.PacketList.Clear();
            zone.OnCharTimeout(sb, false);
            gone = CharPcs(sa, b.Id);
            Check("timed-out char despawns", gone.Count == 1 ? gone[0].Buffer[10] & 0x20 : -1, 0x20);
            Check("a's list drops the timed-out char", a.SpawnPcList.ContainsKey(b.Id) ? 1 : 0, 0);
        }

        // 0x0B5/0x0B6 chat: say and shout by distance, the IPC the world routes (tells, yells, unity, #messages) sent
        // and, run through CBOR and back, delivered; the yell cooldown, jail, and a non-GM's failed !command
        private static void VerifyChat(string dataDir, Grades grades)
        {
            Console.WriteLine("chat (C2S 0x0B5 0x0B6 0x118, IPC chat, S2C 0x017 0x009):");
            var db = new StubDatabase(Path.Combine(Path.GetDirectoryName(dataDir), "sql"));
            var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
            var log = new List<string>();
            var zone = new ZoneHost(db, new CharLoader(null, grades, new AbilityTable(), stats), dataDir, true, log.Add);
            var sent = new List<XI.Ipc.IIpcMessage>();
            zone.SendIpc = sent.Add;
            // the world hands the message back through CBOR
            void Loop(XI.Ipc.IIpcMessage m) => zone.OnIpc(XI.Ipc.IpcFraming.Decode(XI.Ipc.IpcFraming.ToBytesWithHeader(m)));

            var a = Fixture(grades);
            var b = Fixture(grades);
            b.Id = 2; b.Name = "Kobo"; b.X = -130f;
            var far = Fixture(grades);
            far.Id = 3; far.Name = "Faro"; far.X = -40f;
            var sa = new MapSession { Char = a, InZone = true };
            var sb = new MapSession { Char = b };
            var sf = new MapSession { Char = far };

            void Send(MapSession s, ushort opcode, int size, Action<byte[]> fill = null)
            {
                var buf = new byte[size];
                buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
                fill?.Invoke(buf);
                zone.Dispatch(s, opcode, new BasicPacket(buf));
                zone.AfterParse(s);
            }
            void Chat(MapSession s, byte kind, byte[] text) => Send(s, 0x0B5, (6 + text.Length + 1 + 3) & ~3, buf =>
            {
                buf[4] = kind;
                text.CopyTo(buf, 6);
            });
            void Tell(MapSession s, string to, byte[] text) => Send(s, 0x0B6, (0x15 + text.Length + 1 + 3) & ~3, buf =>
            {
                buf[4] = 3;
                System.Text.Encoding.ASCII.GetBytes(to).CopyTo(buf, 6);
                text.CopyTo(buf, 0x15);
            });
            List<BasicPacket> Of(MapSession s, int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p.Buffer) == opcode);
            int Size(BasicPacket p) => (p.Buffer[1] & 0xFE) * 2;
            string Name(BasicPacket p) => System.Text.Encoding.ASCII.GetString(p.Buffer, 8, 15).TrimEnd('\0');
            bool Mes(BasicPacket p, byte[] text) => p.Buffer.AsSpan(23, text.Length).SequenceEqual(text) && p.Buffer[23 + text.Length] == 0;
            void Clear() { sa.PacketList.Clear(); sb.PacketList.Clear(); sf.PacketList.Clear(); sent.Clear(); }

            Send(sa, 0x015, 32, buf =>
            {
                BitConverter.GetBytes(a.X).CopyTo(buf, 4); BitConverter.GetBytes(a.Y).CopyTo(buf, 8); BitConverter.GetBytes(a.Z).CopyTo(buf, 12);
            });
            Send(sb, 0x00A, 92, buf => BitConverter.GetBytes(b.Id).CopyTo(buf, 12));
            Send(sf, 0x00A, 92, buf => BitConverter.GetBytes(far.Id).CopyTo(buf, 12));

            // say: Shift-JIS bytes as they are, to the char 10 yalms off but not the one 100 off nor the speaker
            var hello = new byte[] { 0x82, 0xB1, 0x82, 0xF1, (byte)'h', (byte)'i' };
            Clear();
            Chat(sa, 0x00, hello);
            var say = Of(sb, 0x017);
            Check("say reaches the char nearby", say.Count, 1);
            Check("say kind 0", say.Count == 1 ? say[0].Buffer[4] : -1, 0);
            Check("say names the speaker", say.Count == 1 && Name(say[0]) == "Ryoo" ? 1 : 0, 1);
            Check("say zone", say.Count == 1 ? BitConverter.ToUInt16(say[0].Buffer, 6) : -1, 240);
            Check("say message bytes", say.Count == 1 && Mes(say[0], hello) ? 1 : 0, 1);
            Check("say size: the message rounded up to 4", say.Count == 1 ? Size(say[0]) : -1, 32);
            Check("say misses the far char", Of(sf, 0x017).Count, 0);
            Check("say is not echoed", Of(sa, 0x017).Count, 0);
            // emote is kind 8 to the same range; shout goes to 180
            Clear();
            Chat(sa, 0x08, hello);
            Check("emote kind 8", Of(sb, 0x017).Count == 1 ? Of(sb, 0x017)[0].Buffer[4] : -1, 8);
            Clear();
            Chat(sa, 0x01, hello);
            Check("shout reaches the far char", Of(sf, 0x017).Count == 1 ? Of(sf, 0x017)[0].Buffer[4] : -1, 1);
            Check("shout reaches the near char", Of(sb, 0x017).Count, 1);
            // party and linkshell: no party or linkshell, nothing sent
            Clear();
            Chat(sa, 0x04, hello);
            Chat(sa, 0x05, hello);
            Check("party/linkshell without one send nothing", Of(sb, 0x017).Count + sent.Count, 0);

            // tell: out over IPC by name; back in, delivered by name whatever its case
            Clear();
            Tell(sa, "kobo", hello);
            var tell = sent.Count == 1 ? sent[0] as XI.Ipc.ChatMessageTell : null;
            Check("tell goes to the world", tell != null ? 1 : 0, 1);
            Check("tell recipient as typed", tell?.RecipientName == "kobo" ? 1 : 0, 1);
            Check("tell sender", tell != null ? tell.SenderId : 0, 1);
            Loop(tell);
            var got = Of(sb, 0x017);
            Check("tell delivered", got.Count, 1);
            Check("tell kind 3", got.Count == 1 ? got[0].Buffer[4] : -1, 3);
            Check("tell names the sender", got.Count == 1 && Name(got[0]) == "Ryoo" ? 1 : 0, 1);
            Check("tell message bytes survive CBOR", got.Count == 1 && Mes(got[0], hello) ? 1 : 0, 1);
            // an /away recipient: the world is told to answer the sender
            Clear();
            b.Settings |= 1u << 1;
            Loop(tell);
            var away = sent.Count == 1 ? sent[0] as XI.Ipc.MessageStandard : null;
            Check("away recipient: TellNotReceivedAway", away != null ? away.Message : 0, 181);
            Check("away reply goes to the sender", away != null ? away.RecipientId : 0, 1);
            Check("away recipient gets nothing", Of(sb, 0x017).Count, 0);
            b.Settings &= ~(1u << 1);
            Clear();
            Loop(new XI.Ipc.ChatMessageTell { SenderId = 1, SenderName = "Ryoo", RecipientName = "Nobody", Message = "x" });
            Check("unknown recipient: TellNotReceivedOffline", sent.Count == 1 && sent[0] is XI.Ipc.MessageStandard ms ? ms.Message : 0, 125);
            // the world's reply lands as a 0x009 for the sender
            Clear();
            Loop(new XI.Ipc.MessageStandard { RecipientId = 1, Message = 125 });
            var m009 = Of(sa, 0x009);
            Check("MessageStandard: 0x009 to the char", m009.Count, 1);
            Check("MessageStandard: MesNo", m009.Count == 1 ? BitConverter.ToUInt16(m009[0].Buffer, 10) : -1, 125);
            Check("MessageStandard: size 0x24", m009.Count == 1 ? Size(m009[0]) : -1, 0x24);
            Clear();
            Loop(new XI.Ipc.MessageStandard { RecipientId = 1, Message = 89, String2 = "Kobo" });
            m009 = Of(sa, 0x009);
            Check("MessageStandard string2", m009.Count == 1 && System.Text.Encoding.ASCII.GetString(m009[0].Buffer, 13, 12) == "string2 Kobo" ? 1 : 0, 1);

            // yell: out over IPC, then a cooldown; back in, to the yell zones but the sender
            Clear();
            Chat(sa, 0x1A, hello);
            var yell = sent.Count == 1 ? sent[0] as XI.Ipc.ChatMessageYell : null;
            Check("yell goes to the world", yell != null ? 1 : 0, 1);
            Clear();
            Chat(sa, 0x1A, hello);
            m009 = Of(sa, 0x009);
            Check("second yell waits: no IPC", sent.Count, 0);
            Check("second yell: WaitLonger", m009.Count == 1 ? BitConverter.ToUInt16(m009[0].Buffer, 10) : -1, 38);
            Check("WaitLonger names the char", m009.Count == 1 && System.Text.Encoding.ASCII.GetString(m009[0].Buffer, 13, 22) == "string2 Ryoo string3 0" ? 1 : 0, 1);
            Check("WaitLonger size", m009.Count == 1 ? Size(m009[0]) : -1, 40);
            Clear();
            Loop(yell);
            Check("yell reaches the zone", Of(sf, 0x017).Count == 1 ? Of(sf, 0x017)[0].Buffer[4] : -1, 26);
            Check("yell skips the sender", Of(sa, 0x017).Count, 0);

            // unity: none joined, then 0x118 joins the leader's and the message round-trips to the other member
            Clear();
            Chat(sa, 0x21, hello);
            m009 = Of(sa, 0x009);
            Check("unity without one: UnityNotParticipating", m009.Count == 1 ? BitConverter.ToUInt16(m009[0].Buffer, 10) : -1, 286);
            a.UnityLeader = 5; b.UnityLeader = 5;
            Send(sa, 0x118, 8, buf => buf[4] = 1);
            Send(sb, 0x118, 8, buf => buf[4] = 1);
            var joined = Of(sa, 0x061);
            Check("0x118 on: the 0x061 unity_chat_color_flag set", joined.Count > 0 ? (int)BitConverter.ToUInt32(joined[^1].Buffer, 96) : -1, 1);
            Clear();
            Chat(sa, 0x21, hello);
            var unity = sent.Count == 1 ? sent[0] as XI.Ipc.ChatMessageUnity : null;
            Check("unity goes to the world with the leader", unity != null ? unity.UnityLeaderId : 0, 5);
            Loop(unity);
            Check("unity reaches the other member", Of(sb, 0x017).Count == 1 ? Of(sb, 0x017)[0].Buffer[4] : -1, 33);
            Check("unity skips the sender and non-members", Of(sa, 0x017).Count + Of(sf, 0x017).Count, 0);
            Send(sb, 0x118, 8, buf => buf[4] = 0);
            var left = Of(sb, 0x061);
            Check("0x118 off: the 0x061 unity_chat_color_flag clear", left.Count > 0 ? (int)BitConverter.ToUInt32(left[^1].Buffer, 96) : -1, 0);
            Clear();
            Loop(unity);
            Check("unity left: nothing", Of(sb, 0x017).Count, 0);

            // a non-GM's !command that fails (no scripts here) is just said; a GM's #message goes to the server
            Clear();
            var bang = System.Text.Encoding.ASCII.GetBytes("!pos");
            Chat(sa, 0x00, bang);
            Check("failed !command is said", Of(sb, 0x017).Count == 1 && Mes(Of(sb, 0x017)[0], bang) ? 1 : 0, 1);
            a.AccountGmLevel = 1;
            Clear();
            Chat(sa, 0x00, bang);
            Check("a GM's failed !command is not said", Of(sb, 0x017).Count, 0);
            Chat(sa, 0x00, System.Text.Encoding.ASCII.GetBytes("#hello"));
            var server = sent.Count == 1 ? sent[0] as XI.Ipc.ChatMessageServerMessage : null;
            Check("#message to the server", server?.Message == "hello" ? 1 : 0, 1);
            Check("#message GM level", server != null ? server.GmLevel : -1, 1);
            Clear();
            Loop(server);
            Check("server message to every char", Of(sa, 0x017).Count + Of(sb, 0x017).Count + Of(sf, 0x017).Count, 3);
            a.AccountGmLevel = 0;

            // jailed: say only, to those nearby; anything else CannotInThisArea, a tell CannotUseInArea
            a.Zone = 131;
            b.Zone = 131;
            Clear();
            Chat(sa, 0x00, hello);
            Check("jailed say reaches the cell", Of(sb, 0x017).Count, 1);
            Chat(sa, 0x01, hello);
            var bm = Of(sa, 0x029);
            Check("jailed shout: CannotInThisArea", bm.Count == 1 ? BitConverter.ToUInt16(bm[0].Buffer, 24) : -1, 40);
            sa.PacketList.Clear();
            Tell(sa, "kobo", hello);
            bm = Of(sa, 0x029);
            Check("jailed tell: CannotUseInArea", bm.Count == 1 ? BitConverter.ToUInt16(bm[0].Buffer, 24) : -1, 316);
            Check("jailed: nothing to the world", sent.Count, 0);
            a.Zone = 240;
            b.Zone = 240;

            // !commands on LuaJIT: xi.commands.<name> with its cmdprops, the args parsed per parameter
            var cz = new ZoneHost(db, new CharLoader(null, grades, new AbilityTable(), stats), dataDir, true, log.Add);
            using var scripts = new XI.Script.ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
            cz.InitScripts(scripts);
            scripts.Lua.DoString(@"xi.commands.echo = { cmdprops = { permission = 1, parameters = 'sid' },
                onTrigger = function(player, s, i, d) player:printToPlayer(s .. ':' .. i .. ':' .. tostring(d), xi.msg.channel.SYSTEM_3) end }
                xi.commands.broken = { cmdprops = { permission = 0, parameters = '' }, onTrigger = function(player) player:printToPlayer(nil) end }",
                "=verify_commands");
            var g = Fixture(grades);
            var sg = new MapSession { Char = g, InZone = true };
            void Line(string text)
            {
                var bytes = System.Text.Encoding.ASCII.GetBytes(text);
                var buf = new byte[(6 + bytes.Length + 1 + 3) & ~3];
                buf[0] = 0xB5; buf[1] = (byte)(buf.Length / 4 << 1);
                bytes.CopyTo(buf, 6);
                cz.Dispatch(sg, 0x0B5, new BasicPacket(buf));
                cz.AfterParse(sg);
            }
            List<BasicPacket> Mine() => new List<BasicPacket>(sg.PacketList).FindAll(p => Opcode(p.Buffer) == 0x017);
            string Text(BasicPacket p) => System.Text.Encoding.ASCII.GetString(p.Buffer, 23, Size(p) - 23).TrimEnd('\0');
            Line("hi");
            sg.PacketList.Clear();
            g.AccountGmLevel = 1;
            Line("!echo   foo 12x 2.5e1z");
            var printed = Mine();
            Check("!command prints to the GM", printed.Count, 1);
            Check("printToPlayer channel", printed.Count == 1 ? printed[0].Buffer[4] : -1, 29);
            Check("args: token, int prefix, double prefix", printed.Count == 1 && Text(printed[0]) == "foo:12:25" ? 1 : 0, 1);
            Check("printToPlayer names the char", printed.Count == 1 && Name(printed[0]) == "Ryoo" ? 1 : 0, 1);
            sg.PacketList.Clear();
            Line("!echo foo");
            // i is nil, so the concatenation errors
            Check("missing args are not passed", Mine().Count, 0);
            sg.PacketList.Clear();
            Line("!broken");
            Check("a Lua error in a command sends nothing", Mine().Count, 0);
            Check("the error is logged", log.Exists(e => e.Contains("cmdhandler::call: (broken)")) ? 1 : 0, 1);
            g.AccountGmLevel = 0;
            sg.PacketList.Clear();
            Line("!echo foo 1 2");
            Check("below the permission: nothing printed", Mine().Count, 0);
            Check("below the permission: logged", log.Exists(e => e.Contains("higher permission command echo")) ? 1 : 0, 1);
        }

        private static void VerifyTrade(string dataDir, Grades grades)
        {
            Console.WriteLine("trade (C2S 0x032 0x033 0x034 0x036, S2C 0x021 0x022 0x023 0x025, CTradeContainer):");
            var db = new StubDatabase(Path.Combine(Path.GetDirectoryName(dataDir), "sql"));
            var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
            var log = new List<string>();
            var zone = new ZoneHost(db, new CharLoader(null, grades, new AbilityTable(), stats), dataDir, true, log.Add);
            using var scripts = new XI.Script.ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
            zone.InitScripts(scripts);

            // Janshura-Rashura stands at (-230.626, -8.398, 184.050); a is next to her, b next to a
            var a = Fixture(grades);
            (a.X, a.Y, a.Z) = (-229f, -8.398f, 184f);
            a.Items.Add(new Item { Location = Container.Inventory, Slot = 9, ItemId = 4096, Quantity = 12 });
            var b = Fixture(grades);
            b.Id = 2; b.Name = "Kobo";
            (b.X, b.Y, b.Z) = (-227f, -8.398f, 184f);
            var sa = new MapSession { Char = a, InZone = true };
            var sb = new MapSession { Char = b };

            void Send(MapSession s, ushort opcode, int size, Action<byte[]> fill = null)
            {
                var buf = new byte[size];
                buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
                fill?.Invoke(buf);
                zone.Dispatch(s, opcode, new BasicPacket(buf));
                zone.AfterParse(s);
            }
            List<BasicPacket> Of(MapSession s, int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p.Buffer) == opcode);
            void Clear() { sa.PacketList.Clear(); sb.PacketList.Clear(); }
            Item At(CharData c, byte slot) => c.Items.Find(i => i.Location == Container.Inventory && i.Slot == slot);
            Send(sa, 0x015, 32, buf =>
            {
                BitConverter.GetBytes(a.X).CopyTo(buf, 4); BitConverter.GetBytes(a.Y).CopyTo(buf, 8); BitConverter.GetBytes(a.Z).CopyTo(buf, 12);
            });
            Send(sb, 0x00A, 92, buf => BitConverter.GetBytes(b.Id).CopyTo(buf, 12));

            // ---- NPC trade: the script sees the offer through CTradeContainer and takes it with tradeComplete ----
            const uint npcId = 17760269;
            scripts.Lua.DoString(@"InteractionGlobal.onTrade = function(player, npc, trade)
                    local item = trade:getItem(1)
                    player:printToPlayer(string.format('%d:%d:%d:%d:%d:%s:%d', trade:getGil(), trade:getItemCount(), trade:getSlotCount(),
                        trade:getItemId(1), trade:getItemQty(4096), tostring(trade:hasItemQty(4096, 5)), item and item:getQuantity() or -1),
                        xi.msg.channel.SYSTEM_3)
                    if verifyTradeMode == 'complete' then
                        player:printToPlayer(tostring(player:tradeComplete()), xi.msg.channel.SYSTEM_3)
                    elseif verifyTradeMode == 'confirm' then
                        trade:confirmItem(4096, 2)
                        player:printToPlayer(tostring(player:confirmTrade()), xi.msg.channel.SYSTEM_3)
                    end
                end", "=verify_trade");
            void NpcTrade(MapSession s, params (byte Slot, uint Qty)[] items) => Send(s, 0x036, 64, buf =>
            {
                BitConverter.GetBytes(npcId).CopyTo(buf, 4);
                for (int i = 0; i < items.Length; i++)
                {
                    BitConverter.GetBytes(items[i].Qty).CopyTo(buf, 8 + i * 4);
                    buf[48 + i] = items[i].Slot;
                }
                BitConverter.GetBytes((ushort)(npcId & 0xFFF)).CopyTo(buf, 58);
                buf[60] = (byte)items.Length;
            });
            List<string> Printed(MapSession s) =>
                Of(s, 0x017).ConvertAll(p => System.Text.Encoding.ASCII.GetString(p.Buffer, 23, p.Buffer.Length - 23).TrimEnd('\0'));
            bool Said(MapSession s, string text) => Printed(s).Contains(text);

            Clear();
            scripts.Lua.DoString("verifyTradeMode = 'complete'", "=mode");
            NpcTrade(sa, (0, 4), (9, 5));
            Check("onTrade sees gil, count, slots, id, qty, hasItemQty, getItem", Said(sa, "4:6:2:4096:5:true:12") ? 1 : 0, 1);
            Check("tradeComplete returns true", Said(sa, "true") ? 1 : 0, 1);
            Check("tradeComplete takes the gil", At(a, 0).Quantity, 6);
            Check("tradeComplete takes the crystals", At(a, 9).Quantity, 7);
            Check("the crystals are free again", At(a, 9).InTransaction ? 1 : 0, 0);
            Check("ITEM_SAME after the trade", Of(sa, 0x01D).Count >= 1 ? 1 : 0, 1);

            Clear();
            scripts.Lua.DoString("verifyTradeMode = 'confirm'", "=mode");
            NpcTrade(sa, (9, 3));
            Check("confirmTrade returns true", Said(sa, "true") ? 1 : 0, 1);
            Check("confirmTrade takes only what was confirmed", At(a, 9).Quantity, 5);

            Clear();
            scripts.Lua.DoString("verifyTradeMode = nil", "=mode");
            NpcTrade(sa, (9, 2));
            Check("an unanswered offer is released", At(a, 9).InTransaction ? 1 : 0, 0);
            Clear();
            NpcTrade(sa, (9, 0));
            Check("zero quantity: onTrade not called", Printed(sa).Count, 0);
            NpcTrade(sa, (1, 1));
            Check("equipped item: onTrade not called", Printed(sa).Count, 0);
            NpcTrade(sa, (9, 6));
            Check("more than held: onTrade not called", Printed(sa).Count, 0);
            NpcTrade(sa, (9, 1), (9, 1));
            Check("the same slot twice: onTrade not called", Printed(sa).Count, 0);
            ushort Std(MapSession s) => Of(s, 0x053).Count == 1 ? BitConverter.ToUInt16(Of(s, 0x053)[0].Buffer, 12) : (ushort)0;
            a.StatusEffects.AddStatusEffect(new StatusEffect(69, 69, 0, 0, 60_000), EffectNotice.Silent);
            Clear();
            NpcTrade(sa, (9, 1));
            Check("invisible: CannotWhileInvisible (172), onTrade not called", Std(sa) * 10 + Printed(sa).Count, 1720);
            a.StatusEffects.DelStatusEffectSilent(69);

            // ---- player trade: request, start, offers on both sides, Make from both ----
            void Req(MapSession s, CharData to) => Send(s, 0x032, 12, buf =>
            {
                BitConverter.GetBytes(to.Id).CopyTo(buf, 4);
                BitConverter.GetBytes(to.TargId).CopyTo(buf, 8);
            });
            void Res(MapSession s, uint kind) => Send(s, 0x033, 12, buf => BitConverter.GetBytes(kind).CopyTo(buf, 4));
            void Offer(MapSession s, uint qty, ushort itemNo, byte invSlot, byte tradeSlot) => Send(s, 0x034, 12, buf =>
            {
                BitConverter.GetBytes(qty).CopyTo(buf, 4);
                BitConverter.GetBytes(itemNo).CopyTo(buf, 8);
                buf[10] = invSlot;
                buf[11] = tradeSlot;
            });
            uint ResKind(BasicPacket p) => BitConverter.ToUInt32(p.Buffer, 8);
            BasicPacket ResOf(MapSession s, uint kind) => Of(s, 0x022).Find(p => ResKind(p) == kind);

            b.BlockingAid = true;
            Clear();
            Req(sa, b);
            Check("b blocking aid: 225 to a, 226 to b, ErrYouTrade, no request", "225/226/7/0",
                  $"{Std(sa)}/{Std(sb)}/{(Of(sa, 0x022).Count == 1 ? ResKind(Of(sa, 0x022)[0]) : 99)}/{Of(sb, 0x021).Count}");
            b.BlockingAid = false;
            Clear();
            Req(sa, b);
            var req = Of(sb, 0x021);
            Check("b is asked to trade", req.Count, 1);
            Check("TRADE_REQ names a", req.Count == 1 ? BitConverter.ToUInt32(req[0].Buffer, 4) : 0, a.Id);
            Check("TRADE_REQ act index", req.Count == 1 ? BitConverter.ToUInt16(req[0].Buffer, 8) : -1, a.TargId);
            Clear();
            Res(sa, 0);
            Check("the initiator cannot start the trade", Of(sa, 0x022).Count + Of(sb, 0x022).Count, 0);
            Res(sb, 0);
            Check("start: a told about b", ResOf(sa, 0) is BasicPacket s0 ? BitConverter.ToUInt32(s0.Buffer, 4) : 0, b.Id);
            Check("start: b told about a", ResOf(sb, 0) is BasicPacket s1 ? BitConverter.ToUInt32(s1.Buffer, 4) : 0, a.Id);

            Clear();
            Offer(sa, 5, 4096, 9, 1);
            var mine = Of(sa, 0x025);
            Check("a sees its own offer", mine.Count == 1 ? BitConverter.ToUInt16(mine[0].Buffer, 8) : -1, 4096);
            Check("MYLIST quantity and slots", mine.Count == 1 ? BitConverter.ToUInt32(mine[0].Buffer, 4) * 1000 + mine[0].Buffer[10] * 100 + mine[0].Buffer[11] : -1, 5109);
            Check("the offered stack is greyed", Of(sa, 0x01F).Find(p => p.Buffer[11] == 9) is BasicPacket g ? g.Buffer[12] : -1, 0x0F);
            var theirs = Of(sb, 0x023);
            Check("b sees a's offer", theirs.Count == 1 ? BitConverter.ToUInt16(theirs[0].Buffer, 10) : -1, 4096);
            Check("LIST quantity and slot", theirs.Count == 1 ? BitConverter.ToUInt32(theirs[0].Buffer, 4) * 100 + theirs[0].Buffer[13] : -1, 501);

            Clear();
            Offer(sb, 1, 4606, 6, 2);
            Check("an Ex item is refused: empty view", Of(sb, 0x025).Find(p => p.Buffer[10] == 2) is BasicPacket ex ? BitConverter.ToUInt16(ex.Buffer, 8) : -1, 0xFFFF);
            Clear();
            Offer(sb, 3, 0xFFFF, 0, 1);
            Check("gil outside the gil slot is refused", Of(sb, 0x025).Count + Of(sa, 0x023).Count, 0);
            Offer(sb, 3, 0xFFFF, 0, 0);
            Check("a sees b's gil", Of(sa, 0x023).Find(p => p.Buffer[13] == 0) is BasicPacket gl ? BitConverter.ToUInt32(gl.Buffer, 4) : 0, 3);

            Clear();
            Res(sa, 2);
            Check("b is told a pressed Make", ResOf(sb, 2) != null ? 1 : 0, 1);
            Res(sb, 2);
            Check("trade end to a", ResOf(sa, 9) != null ? 1 : 0, 1);
            Check("trade end to b", ResOf(sb, 9) != null ? 1 : 0, 1);
            Check("a handed over the whole stack", At(a, 9) == null ? 1 : 0, 1);
            Check("a gets b's gil", At(a, 0).Quantity, 9);
            Check("b gave the gil", At(b, 0).Quantity, 7);
            Check("b gets the crystals", b.Items.Find(i => i.ItemId == 4096)?.Quantity ?? 0, 5);
            Check("the pending trade is gone", a.TradePending.UniqueNo + b.TradePending.UniqueNo, 0);

            // ---- cancel: the partner is told and the offer ungreyed ----
            a.Items.Add(new Item { Location = Container.Inventory, Slot = 9, ItemId = 4096, Quantity = 4 });
            Req(sa, b);
            Res(sb, 0);
            Offer(sa, 2, 4096, 9, 1);
            Clear();
            Res(sb, 1);
            Check("cancel: a is told", ResOf(sa, 1) is BasicPacket cn ? BitConverter.ToUInt32(cn.Buffer, 4) : 0, b.Id);
            Check("cancel: the offer is ungreyed", Of(sa, 0x01F).Find(p => p.Buffer[11] == 9) is BasicPacket ug ? ug.Buffer[12] : -1, 0);
            Check("cancel: the stack is free", At(a, 9).InTransaction ? 1 : 0, 0);
            Check("cancel: nothing moved", At(a, 9).Quantity, 4);

            // ---- leaving mid-trade aborts it for the partner ----
            Req(sa, b);
            Res(sb, 0);
            Offer(sa, 2, 4096, 9, 1);
            Clear();
            zone.OnCharTimeout(sb, false);
            Check("timeout: a is told the trade is cancelled", ResOf(sa, 1) != null ? 1 : 0, 1);
            Check("timeout: a's stack is free", At(a, 9).InTransaction ? 1 : 0, 0);
        }

        private static void VerifyBazaar(string dataDir, Grades grades)
        {
            Console.WriteLine("bazaar (C2S 0x104 0x105 0x106 0x109 0x10A 0x10B, S2C 0x105 - 0x10A, BazaarPurchaseTransaction):");
            var db = new StubDatabase(Path.Combine(Path.GetDirectoryName(dataDir), "sql"));
            var settings = new StatSettings();
            var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), settings);
            var log = new List<string>();
            var zone = new ZoneHost(db, new CharLoader(null, grades, new AbilityTable(), stats), dataDir, true, log.Add);

            // a sells, b buys
            var a = Fixture(grades);
            a.Items.Find(i => i.Slot == 0 && i.Location == Container.Inventory).Quantity = 100;
            a.Items.Add(new Item { Location = Container.Inventory, Slot = 9, ItemId = 4096, Quantity = 12 });
            var b = Fixture(grades);
            b.Id = 2; b.Name = "Kobo";
            b.Items.Find(i => i.Slot == 0 && i.Location == Container.Inventory).Quantity = 5000;
            var sa = new MapSession { Char = a, InZone = true };
            var sb = new MapSession { Char = b };

            void Send(MapSession s, ushort opcode, int size, Action<byte[]> fill = null)
            {
                var buf = new byte[size];
                buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
                fill?.Invoke(buf);
                zone.Dispatch(s, opcode, new BasicPacket(buf));
                zone.AfterParse(s);
            }
            List<BasicPacket> Of(MapSession s, int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p.Buffer) == opcode);
            void Clear() { sa.PacketList.Clear(); sb.PacketList.Clear(); }
            Item At(CharData c, byte slot) => c.Items.Find(i => i.Location == Container.Inventory && i.Slot == slot);
            string NameAt(BasicPacket p, int off) => System.Text.Encoding.ASCII.GetString(p.Buffer, off, 16).TrimEnd('\0');
            Send(sa, 0x015, 32);
            Send(sb, 0x00A, 92, buf => BitConverter.GetBytes(b.Id).CopyTo(buf, 12));
            Check("seller and buyer have their own act indexes", a.TargId != b.TargId ? 1 : 0, 1);

            void ItemSet(byte slot, uint price) => Send(sa, 0x10A, 12, buf =>
            {
                buf[4] = slot;
                BitConverter.GetBytes(price).CopyTo(buf, 8);
            });
            void Close(uint flag = 0) => Send(sa, 0x10B, 8, buf => BitConverter.GetBytes(flag).CopyTo(buf, 4));
            void Open() => Send(sa, 0x109, 4);
            void List(uint uniqueNo) => Send(sb, 0x105, 12, buf =>
            {
                BitConverter.GetBytes(uniqueNo).CopyTo(buf, 4);
                BitConverter.GetBytes(a.TargId).CopyTo(buf, 8);
            });
            void Buy(byte slot, uint num) => Send(sb, 0x106, 12, buf =>
            {
                buf[4] = slot;
                BitConverter.GetBytes(num).CopyTo(buf, 8);
            });
            uint BuyState() => Of(sb, 0x106) is var r && r.Count == 1 ? BitConverter.ToUInt32(r[0].Buffer, 4) : 99;

            // ---- setting prices ----
            ItemSet(9, 100);
            Check("ITEMSET refused until the char sets prices", At(a, 9).CharPrice, 0);
            Close(1);
            Check("CLOSE needs AllListClearFlg 0", a.IsSettingBazaarPrices ? 1 : 0, 0);
            Close();
            Check("CLOSE: setting prices", a.IsSettingBazaarPrices ? 1 : 0, 1);

            Clear();
            ItemSet(9, 100);
            Check("ITEMSET prices the stack", At(a, 9).CharPrice, 100);
            Check("the priced stack is on display", At(a, 9).InBazaar ? 1 : 0, 1);
            var attr = Of(sa, 0x020);
            Check("ITEM_ATTR carries the price", attr.Count == 1 ? BitConverter.ToUInt32(attr[0].Buffer, 8) : 0, 100);
            Check("ITEM_ATTR lock flag 0x19 for a priced item", attr.Count == 1 ? attr[0].Buffer[16] : -1, 0x19);
            Check("ITEM_SAME after ITEMSET", Of(sa, 0x01D).Count, 1);
            ItemSet(9, 99999999 + 1u);
            Check("ITEMSET over 99,999,999 refused", At(a, 9).CharPrice, 100);
            ItemSet(0, 5);
            Check("ITEMSET refuses gil", At(a, 0).CharPrice, 0);
            ItemSet(1, 5);
            Check("ITEMSET refuses an equipped item", At(a, 1).CharPrice, 0);
            ItemSet(6, 5);
            Check("ITEMSET leaves an Ex item alone", At(a, 6).CharPrice, 0);
            ItemSet(8, 30);
            ItemSet(8, 0);
            Check("price 0 takes the stack off display", At(a, 8).CharPrice + (At(a, 8).InBazaar ? 1u : 0), 0);

            List(a.Id);
            Check("LIST refused while the seller sets prices", b.BazaarId.UniqueNo, 0);

            Clear();
            Open();
            Check("OPEN: done setting prices", a.IsSettingBazaarPrices ? 1 : 0, 0);
            Check("OPEN: the seller has a bazaar", a.HasBazaar ? 1 : 0, 1);
            var status = Of(sa, 0x037);
            Check("CHAR_STATUS BazaarFlag", status.Count >= 1 ? (BitConverter.ToUInt32(status[^1].Buffer, 44) >> 29) & 1 : 0, 1);
            Open();
            Check("OPEN refused when not setting prices", a.IsSettingBazaarPrices ? 1 : 0, 0);

            // ---- browsing ----
            Clear();
            List(a.Id);
            Check("LIST: b browses a's bazaar", b.BazaarId.UniqueNo, a.Id);
            Check("LIST: b is a's customer", a.BazaarCustomers.Count, 1);
            var shopping = Of(sa, 0x108);
            Check("SHOPPING Enter names b", shopping.Count == 1 ? BitConverter.ToUInt32(shopping[0].Buffer, 4) : 0, b.Id);
            Check("SHOPPING Enter state and act index", shopping.Count == 1 ? BitConverter.ToUInt32(shopping[0].Buffer, 8) * 10000 + BitConverter.ToUInt16(shopping[0].Buffer, 14) : -1, b.TargId);
            Check("SHOPPING name", shopping.Count == 1 && NameAt(shopping[0], 16) == "Kobo" ? 1 : 0, 1);
            var listing = Of(sb, 0x105);
            Check("BAZAAR_LIST: one listing", listing.Count, 1);
            Check("BAZAAR_LIST price and quantity", listing.Count == 1 ? BitConverter.ToUInt32(listing[0].Buffer, 4) * 100 + BitConverter.ToUInt32(listing[0].Buffer, 8) : 0, 10012);
            Check("BAZAAR_LIST tax, item and slot", listing.Count == 1 ? BitConverter.ToUInt16(listing[0].Buffer, 12) * 100000 + BitConverter.ToUInt16(listing[0].Buffer, 14) * 10 + listing[0].Buffer[16] : -1, 40969);
            Clear();
            List(a.Id);
            Check("a second LIST is refused", Of(sb, 0x105).Count + a.BazaarCustomers.Count, 1);

            // ---- buying ----
            Clear();
            Buy(9, 0);
            Buy(9, 100);
            Check("BuyNum outside 1 - 99 refused", Of(sb, 0x106).Count, 0);
            Buy(9, 13);
            Check("more than listed: ERR", BuyState(), 1);
            Clear();
            Buy(8, 1);
            Check("an unpriced slot: ERR", BuyState(), 1);
            Clear();
            At(b, 0).Quantity = 50;
            Buy(9, 1);
            Check("not enough gil: ERR", BuyState(), 1);
            Check("not enough gil: nothing moved", At(a, 9).Quantity * 10000 + At(b, 0).Quantity, 120050);
            At(b, 0).Quantity = 5000;

            Clear();
            Buy(9, 5);
            Check("BUY OK", BuyState(), 0);
            Check("BUY names the seller", Of(sb, 0x106) is var ok && ok.Count == 1 && NameAt(ok[0], 8) == "Ryoo" ? 1 : 0, 1);
            Check("the buyer pays", At(b, 0).Quantity, 4500);
            Check("the seller earns", At(a, 0).Quantity, 600);
            Check("the seller's stack shrinks", At(a, 9).Quantity, 7);
            Check("what is left stays on display", (At(a, 9).InBazaar ? 1 : 0) * 1000 + At(a, 9).CharPrice, 1100);
            Check("the seller's stack is not left claimed", At(a, 9).InTransaction ? 1 : 0, 0);
            var got = b.Items.Find(i => i.ItemId == 4096);
            Check("the buyer gets the crystals", got?.Quantity ?? 0, 5);
            Check("the bought stack is unpriced", got != null ? got.CharPrice + (got.InBazaar ? 1u : 0) : 99, 0);
            var sale = Of(sa, 0x10A);
            Check("BAZAAR_SALE item and quantity", sale.Count == 1 ? BitConverter.ToUInt32(sale[0].Buffer, 4) * 10000 + BitConverter.ToUInt16(sale[0].Buffer, 8) : 0, 54096);
            Check("BAZAAR_SALE names the buyer", sale.Count == 1 && NameAt(sale[0], 10) == "Kobo" ? 1 : 0, 1);
            Check("the seller sees the slot", Of(sa, 0x020).Find(p => p.Buffer[15] == 9) is BasicPacket sa9 ? BitConverter.ToUInt32(sa9.Buffer, 4) : 0, 7);
            Check("the buyer is not sent BAZAAR_SELL", Of(sb, 0x109).Count, 0);
            Check("the buyer's listing updates", Of(sb, 0x105).Find(p => p.Buffer[16] == 9) is BasicPacket bl ? BitConverter.ToUInt32(bl.Buffer, 8) : 0, 7);
            Check("the bazaar is still open", Of(sb, 0x107).Count, 0);

            Clear();
            Buy(9, 7);
            Check("buyout OK", BuyState(), 0);
            Check("buyout: the seller's slot is empty", At(a, 9) == null ? 1 : 0, 1);
            uint held = 0;
            foreach (var i in b.Items) if (i.ItemId == 4096) held += i.Quantity;
            Check("buyout: the buyer holds 12", held, 12);
            Check("buyout: the listing clears", Of(sb, 0x105).Find(p => p.Buffer[16] == 9) is BasicPacket cl ? BitConverter.ToUInt32(cl.Buffer, 4) + BitConverter.ToUInt16(cl.Buffer, 14) : 99, 0);
            Check("an empty bazaar closes for its customers", Of(sb, 0x107) is var bc && bc.Count == 1 && NameAt(bc[0], 4) == "Ryoo" ? 1 : 0, 1);
            Check("an empty bazaar: no BazaarFlag", a.HasBazaar ? 1 : 0, 0);

            // ---- leaving ----
            Clear();
            Send(sb, 0x104, 4);
            Check("EXIT: the seller is told", Of(sa, 0x108) is var ex && ex.Count == 1 ? BitConverter.ToUInt32(ex[0].Buffer, 8) : 99, 1);
            Check("EXIT: BazaarID cleared", b.BazaarId.UniqueNo, 0);
            Check("EXIT: no longer a customer", a.BazaarCustomers.Count, 0);

            // ---- a hidden GM shops unseen; closing shows the customers out ----
            a.Items.Add(new Item { Location = Container.Inventory, Slot = 9, ItemId = 4096, Quantity = 3 });
            Close();
            ItemSet(9, 250);
            Open();
            b.GmHidden = true; b.AccountGmLevel = 3;
            Clear();
            List(a.Id);
            Check("a hidden GM browses", b.BazaarId.UniqueNo, a.Id);
            Check("a hidden GM is not announced", Of(sa, 0x108).Count, 0);
            Clear();
            Close();
            Check("CLOSE shows the customer out", Of(sb, 0x107).Count, 1);
            Check("CLOSE clears the customers", a.BazaarCustomers.Count, 0);
            // ITEM_DUMP of the listed stack: a Bazaar item is busy, so it stays
            Send(sa, 0x028, 12, buf =>
            {
                BitConverter.GetBytes(1u).CopyTo(buf, 4);
                buf[8] = 0; buf[9] = 9;
            });
            Check("an item on display cannot be dropped", At(a, 9)?.Quantity ?? 0, 3);

            // a charged item's listing: Attr is the charged flag, the charges, recharged (0xD0) and its next use (the
            // last use plus the 300 s reuse delay), not the exdata
            var ring = new Item { Location = Container.Inventory, Slot = 11, ItemId = 10775, Quantity = 1 };
            ring.Extra[1] = 4;
            BitConverter.GetBytes(1000u).CopyTo(ring.Extra, 4);
            a.Items.Add(ring);
            ItemSet(11, 900);
            Open();
            b.GmHidden = false; b.AccountGmLevel = 0;
            Send(sb, 0x104, 4);
            Clear();
            List(a.Id);
            var charged = Of(sb, 0x105).Find(p => p.Buffer[16] == 11);
            Check("BAZAAR_LIST: a charged item's Attr", "1/4/0/208/1300",
                  charged == null ? "-" : $"{charged.Buffer[17]}/{charged.Buffer[18]}/{charged.Buffer[19]}/{charged.Buffer[20]}/{BitConverter.ToUInt32(charged.Buffer, 21)}");

            // ---- the debug lines (DebugBazaarsFmt): none so far, then each step's under logging.DEBUG_BAZAARS ----
            bool AnyDebugLine() => log.Exists(l => l.Contains("[View Wares]") || l.Contains("[Setting Prices]") ||
                                                   l.Contains("[Price Set]"));
            Check("DEBUG_BAZAARS off: no debug line", AnyDebugLine() ? 1 : 0, 0);
            var parsed = XI.Common.Settings.Parse("xi.settings.logging =\n{\n    DEBUG_BAZAARS = true,\n}\n");
            Check("logging.DEBUG_BAZAARS is read from the settings", StatSettings.From(parsed).DebugBazaars ? 1 : 0, 1);
            settings.DebugBazaars = true;
            string Debug() => string.Join(" | ", log.FindAll(l => l.StartsWith("Bazaar Interaction [")));
            const string Bi = "Bazaar Interaction ";
            log.Clear();
            Send(sb, 0x104, 4);
            Check("debug: EXIT", Debug(), Bi + "[Leave Bazaar] - Buyer: Kobo, Seller: Ryoo");
            log.Clear();
            List(a.Id);
            Check("debug: LIST", Debug(), Bi + "[View Wares] - Buyer: Kobo, Seller: Ryoo");
            log.Clear();
            Close();
            Check("debug: CLOSE, each customer shown out, then the prices", Debug(),
                  Bi + "[Leave Bazaar] - Buyer: Kobo, Seller: Ryoo | " + Bi + "[Setting Prices] - Character: Ryoo");
            log.Clear();
            ItemSet(9, 300);
            Check("debug: ITEMSET", Debug(), Bi + "[Price Set] - Character: Ryoo, Item: fire_crystal, Price: 300");
            Open();
            Send(sb, 0x104, 4);
            Send(sa, 0x105, 12, buf =>
            {
                BitConverter.GetBytes(a.Id).CopyTo(buf, 4);
                BitConverter.GetBytes(a.TargId).CopyTo(buf, 8);
            });
            log.Clear();
            Send(sa, 0x106, 12, buf =>
            {
                buf[4] = 9;
                BitConverter.GetBytes(1u).CopyTo(buf, 8);
            });
            Check("debug: buying from the char's own bazaar", Debug(),
                  Bi + "[Purchase Failed / Self Bazaar] - Character: Ryoo, Item: fire_crystal");
            Send(sa, 0x104, 4);
            List(a.Id);
            log.Clear();
            Buy(9, 3);
            Check("debug: BUY", Debug(),
                  Bi + "[Purchase Successful] - Buyer: Kobo, Seller: Ryoo, Item: fire_crystal, Qty: 3, Cost: 900");
            log.Clear();
            Buy(11, 1);
            Check("debug: the last listing bought empties the bazaar", Debug(),
                  Bi + "[Purchase Successful] - Buyer: Kobo, Seller: Ryoo, Item: gaiardas_ring, Qty: 1, Cost: 900 | " +
                  Bi + "[Bazaar Emptied] - Buyer: Kobo, Seller: Ryoo");
            settings.DebugBazaars = false;
        }

        // C2S 0x0DD EQUIP_INSPECT (/check on a char, /checkparam on self) and 0x0DE INSPECT_MESSAGE
        private static void VerifyInspect(string dataDir, Grades grades)
        {
            Console.WriteLine("inspect (C2S 0x0DD 0x0DE, S2C 0x0C9 0x0CA 0x009 0x029):");
            var db = new StubDatabase(Path.Combine(Path.GetDirectoryName(dataDir), "sql"));
            var items = new ItemData(db);
            var stats = new CharStats(GrowthTables.Load(db), items, new StatSettings());
            var log = new List<string>();
            var zone = new ZoneHost(db, new CharLoader(null, grades, new AbilityTable(), stats), dataDir, true, log.Add);
            uint vanaNow = LoginPackets.VanadielTimestamp(zone.UnixNow());

            // a checks b
            var a = Fixture(grades);
            a.Items.Add(new Item { Location = Container.Inventory, Slot = 12, ItemId = 16482, Quantity = 1 }); // a second dagger
            stats.EquipAtLogin(a);
            var b = Fixture(grades);
            b.Id = 2; b.Name = "Kobo";
            var warp = new Item { Location = Container.Inventory, Slot = 10, ItemId = 28540, Quantity = 1 };
            warp.Extra[1] = 2;                                                    // two charges
            BitConverter.GetBytes(vanaNow - 100).CopyTo(warp.Extra, 4);         // used 100 s ago, reuse 600 s
            b.Items.Add(warp);
            b.Items.Find(i => i.Slot == 7).Extra[2] = 0x2D;                       // the ring gets an augment
            b.Items.Find(i => i.Slot == 7).Extra[13] = 0x5A;                      // and signature bytes
            items.ApplyAugments(b.Items.Find(i => i.Slot == 7));                  // LoadInventory's augment pass
            b.Items.Add(new Item { Location = Container.Inventory, Slot = 11, ItemId = 13008, Quantity = 1 });
            b.Items.Add(new Item { Location = Container.Inventory, Slot = 12, ItemId = 12887, Quantity = 1 });
            stats.EquipAtLogin(b);
            var sa = new MapSession { Char = a, InZone = true };
            var sb = new MapSession { Char = b };

            void Send(MapSession s, ushort opcode, int size, Action<byte[]> fill = null)
            {
                var buf = new byte[size];
                buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
                fill?.Invoke(buf);
                zone.Dispatch(s, opcode, new BasicPacket(buf));
                zone.AfterParse(s);
            }
            List<BasicPacket> Of(MapSession s, int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p.Buffer) == opcode);
            void Clear() { sa.PacketList.Clear(); sb.PacketList.Clear(); log.Clear(); }
            int Size(BasicPacket p) => (p.Buffer[1] >> 1) * 4;
            void Inspect(MapSession s, uint uniqueNo, ushort actIndex, byte kind) => Send(s, 0x0DD, 16, buf =>
            {
                BitConverter.GetBytes(uniqueNo).CopyTo(buf, 4);
                BitConverter.GetBytes((uint)actIndex).CopyTo(buf, 8);
                buf[12] = kind;
            });
            void Equip(MapSession s, byte invSlot, byte equipSlot) => Send(s, 0x050, 8, buf => { buf[4] = invSlot; buf[5] = equipSlot; });
            Send(sa, 0x015, 32);
            Send(sb, 0x00A, 92, buf => BitConverter.GetBytes(b.Id).CopyTo(buf, 12));
            b.X = a.X + 3; b.Y = a.Y; b.Z = a.Z;
            Equip(sb, 5, 0);   // dagger
            Equip(sb, 7, 13);  // ring
            Equip(sb, 10, 14); // warp ring
            // two more rows past the 8 a page holds (no EquipArmor: the packet reads getEquip only)
            b.Equip.Add(new EquipEntry { EquipSlot = 9, InvSlot = 11, Container = Container.Inventory });
            b.Equip.Add(new EquipEntry { EquipSlot = 15, InvSlot = 12, Container = Container.Inventory });
            Check("b has 9 equipped", b.Equip.Count, 9);

            // ---- 0x0DE ----
            Clear();
            Send(sb, 0x0DE, 128, buf => System.Text.Encoding.ASCII.GetBytes("Cheap crystals").CopyTo(buf, 4));
            Check("INSPECT_MESSAGE stores the message", System.Text.Encoding.ASCII.GetString(b.BazaarMessage), "Cheap crystals");
            Send(sb, 0x0DE, 128, buf => { for (int i = 4; i < 128; i++) buf[i] = (byte)'z'; });
            Check("the message stops at 120 bytes", b.BazaarMessage.Length, 120);
            Send(sb, 0x0DE, 128, buf => System.Text.Encoding.ASCII.GetBytes("Cheap crystals").CopyTo(buf, 4));
            Check("INSPECT_MESSAGE: no debug line with DEBUG_BAZAARS off",
                  log.Exists(l => l.Contains("[Set Message]")) ? 1 : 0, 0);
            stats.Settings.DebugBazaars = true;
            Send(sb, 0x0DE, 128, buf => System.Text.Encoding.ASCII.GetBytes("Cheap crystals").CopyTo(buf, 4));
            Check("INSPECT_MESSAGE: the debug line", log.FindLast(l => l.Contains("[Set Message]")) ?? "",
                  "Bazaar Interaction [Set Message] - Character: Kobo, Message: 'Cheap crystals'");
            stats.Settings.DebugBazaars = false;

            // ---- /check ----
            Clear();
            Inspect(sa, b.Id, b.TargId, 0);
            var examine = Of(sb, 0x009);
            Check("the target is told: Examine", examine.Count == 1 ? BitConverter.ToUInt16(examine[0].Buffer, 10) : 0, 89);
            Check("Examine names the checker", examine.Count == 1 ? BitConverter.ToUInt32(examine[0].Buffer, 4) : 0, a.Id);
            var im = Of(sa, 0x0CA);
            Check("INSPECT_MESSAGE: one", im.Count, 1);
            Check("INSPECT_MESSAGE: b's message", im.Count == 1 && System.Text.Encoding.ASCII.GetString(im[0].Buffer, 4, 14) == "Cheap crystals" ? 1 : 0, 1);
            Check("INSPECT_MESSAGE: BazaarFlag + Race, not MyFlag", im.Count == 1 ? im[0].Buffer[127] : -1, 0x05);
            Check("INSPECT_MESSAGE: b's name", im.Count == 1 && System.Text.Encoding.ASCII.GetString(im[0].Buffer, 128, 16).TrimEnd('\0') == "Kobo" ? 1 : 0, 1);
            Check("INSPECT_MESSAGE: b's title", im.Count == 1 ? BitConverter.ToUInt32(im[0].Buffer, 144) : 0, b.Title);
            var ins = Of(sa, 0x0C9);
            Check("EQUIP_INSPECT: two equipment pages and the general", ins.Count, 3);
            if (ins.Count == 3)
            {
                BasicPacket p1 = ins[0], p2 = ins[1], g = ins[2];
                Check("page 1: OptionFlag 3, 8 items", p1.Buffer[10] * 100 + p1.Buffer[11], 308);
                Check("page 1: sized for 8", Size(p1), 12 + 28 * 8);
                Check("page 1: b's id and act index", BitConverter.ToUInt32(p1.Buffer, 4) == b.Id && BitConverter.ToUInt16(p1.Buffer, 8) == b.TargId ? 1 : 0, 1);
                var slots = new List<int>();
                for (int i = 0; i < 8; i++) slots.Add(p1.Buffer[12 + i * 28 + 2]);
                Check("page 1: slots in order", string.Join(",", slots) == "0,5,6,7,8,9,13,14" ? 1 : 0, 1);
                Check("page 1: the dagger first", BitConverter.ToUInt16(p1.Buffer, 12), 16482);
                int ring = 12 + 6 * 28, wr = 12 + 7 * 28;
                Check("augmented ring: Type 2 and the augment", p1.Buffer[ring + 4] * 1000 + p1.Buffer[ring + 4 + 2], 2045);
                Check("augmented ring: signature at Data 12", p1.Buffer[ring + 4 + 13], 0x5A);
                Check("warp ring: Type 1, 2 charges", p1.Buffer[wr + 4] * 10 + p1.Buffer[wr + 5], 12);
                Check("warp ring: on cooldown 0x90", p1.Buffer[wr + 7], 0x90);
                Check("warp ring: next use = last use + reuse", BitConverter.ToUInt32(p1.Buffer, wr + 8), vanaNow - 100 + 600);
                Check("warp ring: use delay + now", BitConverter.ToUInt32(p1.Buffer, wr + 12) - (vanaNow + 8) <= 1 ? 1 : 0, 1); // the clock may tick
                Check("page 2: the back item alone", p2.Buffer[11] * 100 + p2.Buffer[12 + 2], 115);
                Check("page 2: sized for 1", Size(p2), 12 + 28);
                Check("GENERAL: OptionFlag 1, 84 bytes", g.Buffer[10] * 1000 + Size(g), 1084);
                Check("GENERAL: jobs and levels", g.Buffer[34] << 24 | g.Buffer[35] << 16 | g.Buffer[36] << 8 | g.Buffer[37], 5 << 24 | 0 << 16 | 1 << 8 | 0);
                Check("GENERAL: mjob", g.Buffer[38], 5);
            }

            Clear();
            Inspect(sa, b.Id + 1, b.TargId, 0);
            Check("a wrong UniqueNo: nothing", sa.PacketList.Count + sb.PacketList.Count, 0);
            Clear();
            Inspect(sa, b.Id, b.TargId, 1);
            Check("CheckName (no Monstrosity): nothing", sa.PacketList.Count + sb.PacketList.Count, 0);
            Clear();
            Inspect(sa, b.Id, b.TargId, 3);
            Check("Kind 3 refused", log.Exists(l => l.StartsWith("zone: Invalid")) && sa.PacketList.Count == 0 ? 1 : 0, 1);

            Clear();
            b.Settings |= 1u << 2; // anonymous
            Inspect(sa, b.Id, b.TargId, 0);
            Check("an anonymous target: no jobs", Of(sa, 0x0C9) is var an && an.Count == 3 ? an[2].Buffer[34] + an[2].Buffer[36] + an[2].Buffer[38] : -1, 0);
            Clear();
            a.GmLevel = 3;
            Inspect(sa, b.Id, b.TargId, 0);
            Check("a visible GM 3 sees an anonymous target's jobs", Of(sa, 0x0C9) is var gm && gm.Count == 3 ? gm[2].Buffer[34] : -1, 5);
            a.GmLevel = 0;
            b.Settings &= ~(1u << 2);

            Clear();
            a.GmHidden = true; a.AccountGmLevel = 2;
            Inspect(sa, b.Id, b.TargId, 0);
            Check("a hidden GM checks unseen", Of(sb, 0x009).Count, 0);
            Check("a hidden GM still gets the packets", Of(sa, 0x0C9).Count, 3);
            Clear();
            b.AccountGmLevel = 2;
            Inspect(sa, b.Id, b.TargId, 0);
            Check("seen by a GM of the same level", Of(sb, 0x009).Count, 1);
            a.GmHidden = false; a.AccountGmLevel = 0; b.AccountGmLevel = 0;

            Clear();
            b.X += 51;
            Inspect(sa, b.Id, b.TargId, 0);
            Check("too far: nothing", sa.PacketList.Count + sb.PacketList.Count, 0);
            b.X -= 51;

            Clear();
            Inspect(sb, b.Id, b.TargId, 0);
            Check("/check on self: MyFlag", Of(sb, 0x0CA) is var self && self.Count == 1 ? self[0].Buffer[127] : -1, 0x07);
            Check("/check on self: Examine to self", Of(sb, 0x009).Count, 1);

            Clear();
            a.Zone = 131;
            Inspect(sa, b.Id, b.TargId, 0);
            Check("in prison: CannotUseInArea", Of(sa, 0x029) is var jail && jail.Count == 1 ? BitConverter.ToUInt16(jail[0].Buffer, 24) : 0, 316);
            a.Zone = b.Zone;

            // ---- /checkparam ----
            a.BaseStats[0] = 12; a.BaseStats[1] = 20; a.BaseStats[3] = 14;
            Equip(sa, 5, 0);
            a.WorkingSkills[Skill.Dagger] = 30;
            a.WorkingSkills[Skill.Evasion] = 210;
            Clear();
            Inspect(sa, b.Id, b.TargId, 2);
            Check("/checkparam on another: nothing", sa.PacketList.Count, 0);
            Clear();
            Inspect(sa, a.Id, a.TargId, 2);
            var cp = Of(sa, 0x029);
            Check("/checkparam: six battle messages", cp.Count, 6);
            if (cp.Count == 6)
            {
                var ids = new List<int>();
                foreach (var m in cp) ids.Add(BitConverter.ToUInt16(m.Buffer, 24));
                Check("in order: name, ilvl, primary, auxiliary, range, defense", string.Join(",", ids) == "733,731,712,713,714,715" ? 1 : 0, 1);
                int P(int i) => (int)BitConverter.ToUInt32(cp[i].Buffer, 12);
                int V(int i) => (int)BitConverter.ToUInt32(cp[i].Buffer, 16);
                Check("primary ACC: dagger skill + DEX * 0.75 + ACC", P(2), 30 + 15 + a.GetMod(Mod.ACC));
                Check("primary ATT", V(2), stats.AttMain(a));
                Check("ATT(main) = 8 + skill + STR * 0.75 (+ ATT, ATTP)", stats.AttMain(a), Math.Max(1, (int)(8 + a.GetMod(Mod.ATT) + 12 * 0.75f) + 30 is int att ? att + att * a.GetMod(Mod.ATTP) / 100 : 0));
                Check("auxiliary with nothing in the sub: 0 0", P(3) + V(3), 0);
                Check("range with nothing ranged: 0 0", P(4) + V(4), 0);
                Check("EVA: 200 + 10 * 0.9 + AGI / 2 (+ EVA)", P(5), 209 + 7 + a.GetMod(Mod.EVA));
                Check("DEF", V(5), stats.Def(a));
            }
            Equip(sa, 12, 1); // the second dagger into the sub slot
            Check("the sub dagger is m_Weapons[SLOT_SUB]", a.SubWeapon?.Skill ?? 0, Skill.Dagger);
            a.WorkingSkills[Skill.Dagger] = 30; // equipping reworks the skills
            Clear();
            Inspect(sa, a.Id, a.TargId, 2);
            cp = Of(sa, 0x029);
            Check("auxiliary: off-hand ACC", cp.Count == 6 ? (int)BitConverter.ToUInt32(cp[3].Buffer, 12) : -1, 30 + 15 + a.GetMod(Mod.ACC));
            Check("auxiliary: ATT(sub) with STR * 0.5", cp.Count == 6 ? (int)BitConverter.ToUInt32(cp[3].Buffer, 16) : -1, stats.Att(a, CharStats.SlotSub));
            Check("ATT(sub) = 8 + skill + STR * 0.5", stats.Att(a, CharStats.SlotSub), Math.Max(1, (int)(8 + a.GetMod(Mod.ATT) + 12 * 0.5f) + 30 is int sat ? sat + sat * a.GetMod(Mod.ATTP) / 100 : 0));
            Equip(sa, 0, 1);
            Check("an emptied sub slot holds the unarmed weapon", a.SubWeapon == WeaponInfo.Unarmed ? 1 : 0, 1);
            foreach (var line in log)
                if (line.StartsWith("zone: Bad") || line.Contains("needs the item tables")) { fail_++; Console.WriteLine("  FAIL " + line); }
        }

        // ---- Inspect 2: the GENERAL master level, IsAugmented against the augments table, the item signatures
        // (EncodeStringSignature / DecodeStringSignature, CItem / CItemFurnishing / CItemLinkshell setSignature) ----
        private static void VerifyInspect2(string dataDir, Grades grades)
        {
            Console.WriteLine("inspect 2 (GENERAL master level, the augments table, item signatures):");
            string Hex(byte[] b, int at, int n) => Convert.ToHexString(b, at, n);
            string Zeros(int bytes) => new string('0', bytes * 2);

            // the packing: 6 bits a char, most significant first; 0-9 1.., A-Z 11.., a-z 37..
            Check("signature packing: Kobo", Hex(ItemSignature.Encode("Kobo"), 0, 4), "5739B300");
            Check("signature packing: SignatureStringLength 16", ItemSignature.Encode("Kobo").Length, 16);
            Check("signature unpacking: Kobo", ItemSignature.Decode(ItemSignature.Encode("Kobo").AsSpan(0, 12)), "Kobo");
            Check("strncpy: a zero byte ends the encoding (AF0x)", Hex(ItemSignature.Encode("AF0x"), 0, 3) + "/" +
                  ItemSignature.Decode(ItemSignature.Encode("AF0x").AsSpan(0, 12)), "2D0000/AF");
            Check("at most 15 chars", ItemSignature.Decode(ItemSignature.Encode("Abcdefghijklmnopq").AsSpan(0, 12)), "Abcdefghijklmno");
            Check("another char packs as 0 and ends the name", ItemSignature.Decode(ItemSignature.Encode("ab-cd").AsSpan(0, 12)), "ab");
            Check("code 63 decodes as '?'", ItemSignature.Decode(new byte[] { 0xFC }), "?");

            // setSignature: CItem's at 0x0C, CItemFurnishing's at Furniture.Signature (0x0A), CItemLinkshell's in its name
            var plain = new Item { ItemId = 13496 };
            plain.Extra[2] = 0x2D; plain.Extra[11] = 0xEE;
            ItemSignature.Set(plain, "Kobo");
            Check("CItem::setSignature: Signature[12] at 0x0C, the rest kept", Hex(plain.Extra, 0, 24),
                  "00002D" + Zeros(8) + "EE" + "5739B3" + Zeros(9));
            Check("Item.Signature mirrors getSignature", plain.Signature, "Kobo");
            ItemSignature.Set(plain, "AF0x");
            Check("a re-sign clears the old one", Hex(plain.Extra, 12, 12) + "/" + plain.Signature, "2D" + Zeros(11) + "/AF");
            var bed = new Item { ItemId = 2, Furnishing = true };
            bed.Extra[22] = 0x11;
            ItemSignature.Set(bed, "Kobo");
            Check("CItemFurnishing::setSignature: at 0x0A", Hex(bed.Extra, 0, 24), Zeros(10) + "5739B3" + Zeros(9) + "1100");
            Check("CItemFurnishing::getSignature", ItemSignature.Get(bed), "Kobo");
            var pearl = new Item { ItemId = 515 };
            ItemSignature.Set(pearl, "Kobo");
            Check("CItemLinkshell::setSignature: the linkshell packing in Name", Hex(pearl.Extra, 9, 4) + "/" + pearl.Signature, "94F08FFC/Kobo");

            // a checks b, whose ring (13496 in slot 7) goes on its right ring slot
            var db = new StubDatabase(Path.Combine(Path.GetDirectoryName(dataDir), "sql"));
            var items = new ItemData(db);
            var stats = new CharStats(GrowthTables.Load(db), items, new StatSettings());
            var log = new List<string>();
            var zone = new ZoneHost(db, new CharLoader(null, grades, new AbilityTable(), stats), dataDir, true, log.Add);
            var a = Fixture(grades);
            stats.EquipAtLogin(a);
            var b = Fixture(grades);
            b.Id = 2; b.Name = "Kobo";
            stats.EquipAtLogin(b);
            var sa = new MapSession { Char = a, InZone = true };
            var sb = new MapSession { Char = b };
            void Send(MapSession s, ushort opcode, int size, Action<byte[]> fill = null)
            {
                var buf = new byte[size];
                buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
                fill?.Invoke(buf);
                zone.Dispatch(s, opcode, new BasicPacket(buf));
                zone.AfterParse(s);
            }
            Send(sa, 0x015, 32);
            Send(sb, 0x00A, 92, buf => BitConverter.GetBytes(b.Id).CopyTo(buf, 12));
            b.X = a.X + 3; b.Y = a.Y; b.Z = a.Z;
            Send(sb, 0x050, 8, buf => { buf[4] = 7; buf[5] = 13; });
            var ring = b.Items.Find(i => i.Slot == 7 && i.Location == Container.Inventory);

            // the ring's 0x0C9 EQUIPMENT entry (Data at +4) and the GENERAL page
            (byte[] Data, BasicPacket General) Check0C9()
            {
                sa.PacketList.Clear(); sb.PacketList.Clear();
                Send(sa, 0x0DD, 16, buf =>
                {
                    BitConverter.GetBytes(b.Id).CopyTo(buf, 4);
                    BitConverter.GetBytes((uint)b.TargId).CopyTo(buf, 8);
                });
                byte[] data = null;
                BasicPacket general = null;
                foreach (var p in sa.PacketList)
                {
                    if (Opcode(p.Buffer) != 0x0C9) continue;
                    if (p.Buffer[10] == 0x01) general = p;
                    else
                        for (int i = 0; i < p.Buffer[11]; i++)
                            if (p.Buffer[12 + i * 28 + 2] == 13) data = p.Buffer.AsSpan(12 + i * 28 + 4, 24).ToArray();
                }
                return (data, general);
            }

            // LoadInventory's augment pass over the ring's exdata: a fresh CItemEquipment each time
            void Reload(byte lo, byte hi)
            {
                Array.Clear(ring.Extra);
                ring.Equip = null;
                ring.Augmented = false;
                ring.Extra[2] = lo; ring.Extra[3] = hi;
                items.ApplyAugments(ring);
                ItemSignature.Set(ring, "Kobo");
            }
            Reload(0x2D, 0x00); // augment 45, value 0
            var (d, g) = Check0C9();
            Check("the signed ring: in the inspect", d != null ? 1 : 0, 1);
            Check("augment 45 is in the augments table: Type 2", d?[0] ?? -1, 2);
            Check("the packed signature at Data 12..23", d == null ? "-" : Hex(d, 12, 12), "5739B3" + Zeros(9));
            Check("GENERAL: mlvl and mflags 0, as C++", g == null ? -1 : g.Buffer[39] + g.Buffer[40], 0);
            Check("GENERAL: mjob still sent", g?.Buffer[38] ?? -1, b.MJob);

            items.Augments.Remove(45, out var rows45);
            Reload(0x2D, 0x00);
            (d, _) = Check0C9();
            Check("an augment id the table lacks: not ITEM_AUGMENTED", d == null ? "-" : $"{d[0]}/{Hex(d, 2, 8)}", "0/" + Zeros(8));
            items.Augments[45] = rows45;
            Reload(0x00, 0xF8); // id 0, value 31
            (d, _) = Check0C9();
            Check("a value with augment id 0: not ITEM_AUGMENTED", d == null ? "-" : $"{d[0]}/{Hex(d, 2, 8)}", "0/" + Zeros(8));
            Check("the augments table has no id 0, has 1..2047", (items.Augments.ContainsKey(0) ? 1 : 0) + "/" + items.Augments.Count, "0/2047");
            foreach (var line in log)
                if (line.StartsWith("zone: Bad") || line.Contains("needs the item tables")) { fail_++; Console.WriteLine("  FAIL " + line); }
        }

        // ---- Items 2: augment modifiers on the item (CItemEquipment::ApplyAugment / SetAugmentMod) and its wearer,
        // Exdata::getType / fromTable ----
        private static void VerifyItems2(string dataDir, Grades grades)
        {
            Console.WriteLine("items 2 (augment mods per item and on the wearer, Exdata::fromTable):");
            string Hex(byte[] b, int at, int n) => Convert.ToHexString(b, at, n);
            var db = new StubDatabase(Path.Combine(Path.GetDirectoryName(dataDir), "sql"));
            var items = new ItemData(db);
            var log = new List<string>();
            items.Log = log.Add;
            var stats = new CharStats(GrowthTables.Load(db), items, new StatSettings());
            const ushort Ring = 13496, WarpRing = 28540;
            var template = items.Equipment(Ring);
            void SetAug(Item item, int slot, int id, int value) => BitConverter.TryWriteBytes(item.Extra.AsSpan(2 + slot * 2, 2), (ushort)(id | value << 11));

            // LoadInventory's pass: Accuracy+1 at 4 (+5), HP-1 at 2 (-3), Haste+1 (multiplier 100) at 1 ((1+1)*100), a pet
            // Accuracy / Rng.Acc. augment at 0
            var a = Fixture(grades);
            var ring = a.Items.Find(i => i.Slot == 7);
            SetAug(ring, 0, 23, 4); SetAug(ring, 1, 5, 2); SetAug(ring, 2, 49, 1); SetAug(ring, 3, 96, 0);
            items.ApplyAugments(ring);
            Check("ITEM_AUGMENTED, HasAugments / Standard in the exdata", $"{ring.Augmented}/{ring.Extra[0]}/{ring.Extra[1]}", "True/2/3");
            Check("the ring has its own CItemEquipment", ring.Equip != null && ring.Equip != template ? 1 : 0, 1);
            Check("augment mods on the item: ACC +5, HP -3, HASTE_GEAR +200",
                  $"{ring.Equip?.GetModifier(Mod.ACC) - template.GetModifier(Mod.ACC)}/{ring.Equip?.GetModifier(Mod.HP) - template.GetModifier(Mod.HP)}/" +
                  $"{ring.Equip?.GetModifier(Mod.HASTE_GEAR) - template.GetModifier(Mod.HASTE_GEAR)}", "5/-3/200");
            Check("the pet rows go on its pet mods",
                  ring.Equip == null ? "-" : string.Join(",", ring.Equip.PetMods.GetRange(template.PetMods.Count, ring.Equip.PetMods.Count - template.PetMods.Count)),
                  "(ACC, 0, 1),(RACC, 0, 1)");
            Check("the shared template keeps its mods", items.Equipment(Ring) == template && template.GetModifier(Mod.HASTE_GEAR) == 0 ? 1 : 0, 1);
            Check("EquipmentOf: the item's own, else the template",
                  (items.EquipmentOf(ring) == ring.Equip ? 1 : 0) + (items.EquipmentOf(new Item { ItemId = Ring }) == template ? 1 : 0), 2);

            // the wearer: b wears the same ring without augments
            var b = Fixture(grades);
            stats.EquipAtLogin(a);
            stats.EquipAtLogin(b);
            stats.EquipItem(a, 7, 13, Container.Inventory);
            stats.EquipItem(b, 7, 13, Container.Inventory);
            Check("both wear the ring", (a.Equipped[13] != null ? 1 : 0) + (b.Equipped[13] != null ? 1 : 0), 2);
            Check("the augmented ring's wearer: ACC +5, HP -3, HASTE_GEAR +200 over the plain one's",
                  $"{a.GetMod(Mod.ACC) - b.GetMod(Mod.ACC)}/{a.GetMod(Mod.HP) - b.GetMod(Mod.HP)}/{a.GetMod(Mod.HASTE_GEAR) - b.GetMod(Mod.HASTE_GEAR)}", "5/-3/200");
            a.PetMods.TryGetValue((0, Mod.ACC), out short petAcc);
            a.PetMods.TryGetValue((0, Mod.RACC), out short petRacc);
            b.PetMods.TryGetValue((0, Mod.ACC), out short plainPetAcc);
            Check("the pet augment on the wearer's pet mods", $"{petAcc}/{petRacc}/{plainPetAcc}", "1/1/0");
            Check("the slot holds the item's own copy (an item-only latent changes the item)", a.Equipped[13] == ring.Equip ? 1 : 0, 1);
            stats.UnequipItem(a, 13);
            stats.UnequipItem(b, 13);
            a.PetMods.TryGetValue((0, Mod.ACC), out petAcc);
            Check("unequipped: the augment mods and pet mods come off", $"{a.GetMod(Mod.ACC) - b.GetMod(Mod.ACC)}/{a.GetMod(Mod.HASTE_GEAR)}/{petAcc}", "0/0/0");

            // refusals: a charged item is skipped, an id the table lacks (0 here) is refused, a Mod NONE row adds nothing
            var warp = new Item { ItemId = WarpRing };
            SetAug(warp, 0, 23, 1);
            items.ApplyAugments(warp);
            Check("charged equipment: no augment pass", $"{warp.Augmented}/{warp.Equip == null}", "False/True");
            var zeroId = new Item { ItemId = Ring };
            SetAug(zeroId, 0, 0, 31);
            items.ApplyAugments(zeroId);
            Check("augment id 0 with a value: refused, logged",
                  $"{zeroId.Augmented}/{zeroId.Extra[0]}/{log.Exists(l => l == "Invalid augment type 0 requested for item 13496")}", "False/0/True");
            var latentAug = new Item { ItemId = Ring };
            SetAug(latentAug, 0, 59, 0);
            items.ApplyAugments(latentAug);
            Check("a Mod NONE row: ITEM_AUGMENTED, nothing added", $"{latentAug.Augmented}/{latentAug.Equip == null}", "True/True");

            // Exdata::getType
            Func<ushort, bool> isFish = id => id == 4401;
            ItemExdata.ExdataType TypeOf(ushort id) => ItemExdata.GetType(new Item { ItemId = id }, items, isFish);
            Check("getType: equipment, charged, linkshell, fish, lamp, card, untyped",
                  $"{TypeOf(Ring)}/{TypeOf(WarpRing)}/{TypeOf(513)}/{TypeOf(4401)}/{TypeOf(5414)}/{TypeOf(2339)}/{TypeOf(4112)}",
                  "Augment/Usable/Linkshell/Fish/GlowingLamp/ChocoboCard/None");

            // Exdata::fromTable over a table as FromLua reads one (integer keys long, numbers double)
            Dictionary<object, object> T(params (object Key, object Value)[] kv)
            {
                var t = new Dictionary<object, object>();
                foreach (var (k, v) in kv) t[k is int i ? (long)i : k] = v is int n ? (double)n : v;
                return t;
            }
            Item From(ushort id, Dictionary<object, object> data, out bool typed)
            {
                var item = new Item { ItemId = id };
                typed = ItemExdata.FromTable(item, data, items, isFish);
                return item;
            }
            var std = From(Ring, T(("augmentKind", 2), ("augmentSubKind", 3), ("signature", "Kobo"),
                                   ("augments", T((1, T(("id", 23), ("value", 4))), (2, T(("id", 1), ("value", 40)))))), out bool typed);
            Check("AugmentStandard: kind, sub kind, Id 11 / Value 5 bits (40 -> 8), the signature",
                  $"{typed}/{Hex(std.Extra, 0, 24)}/{std.Signature}", "True/0203172001400000000000005739B3000000000000000000/Kobo");
            Check("AugmentStandard: applied to the item", $"{std.Augmented}/{std.Equip?.GetModifier(Mod.ACC) - template.GetModifier(Mod.ACC)}/" +
                  $"{std.Equip?.GetModifier(Mod.HP) - template.GetModifier(Mod.HP)}", "True/5/9");
            var trial = From(Ring, T(("augmentKind", 2), ("augmentSubKind", 0x43), ("augments", T((1, T(("id", 23), ("value", 0))))),
                                     ("trial", T(("id", 1234), ("completed", true)))), out _);
            Check("AugmentTrial: four augments, TrialId 15 bits and Completed", Hex(trial.Extra, 0, 12), "0243170000000000" + "0000" + "D284");
            Check("AugmentTrial: applied", trial.Equip?.GetModifier(Mod.ACC) - template.GetModifier(Mod.ACC) ?? -1, 1);
            var mezzo = From(Ring, T(("augmentKind", 2), ("augmentSubKind", 0x23), ("type", 1), ("rank", 5), ("accumulatedRP", 300),
                                     ("augments", T((1, T(("index", 7), ("value", 9)))))), out _);
            Check("AugmentMezzotint: Type 2 / Rank 5 bits, the RP, LUT index and value; not applied",
                  $"{Hex(mezzo.Extra, 0, 8)}/{mezzo.Augmented}", "022315002C010709/False");
            var bundle = From(Ring, T(("augmentKind", 3), ("type", 2), ("rank", 4), ("accumulatedRP", 0x1234), ("maxRankTier", 3), ("rpCurve", 1),
                                      ("augmentIndex", 0x01020304)), out _);
            Check("AugmentBundle: kind 3 / Standard|Evolith and the packed rank word", Hex(bundle.Extra, 0, 12), "03830000D248900504030201");
            var pearl = From(4089, T(("number", 123), ("title", 7)), out _);
            Check("LotteryTicket: Number 24 / Title 8 bits", Hex(pearl.Extra, 0, 4), "7B000007");
            var kept = From(4089, T(("number", 5)), out _);
            kept.Extra[3] = 9;
            ItemExdata.FromTable(kept, T(("title", "x"), ("number", 6)), items, isFish);
            Check("a key of the wrong Lua type is left out", Hex(kept.Extra, 0, 4), "06000009");
            var lamp = From(5414, T(("chamberId", 3), ("flags", 1), ("startTime", 100), ("endTime", 200)), out _);
            Check("GlowingLamp: the chamber past 0x1D, flags, end then start", Hex(lamp.Extra, 0, 16), "2000010000000000C800000064000000");
            var egg = From(2312, T(("dna", T((1, 1), (2, 2), (3, 3))), ("ability", 5), ("plan", 2), ("isBred", true)), out _);
            Check("ChocoboEgg: DNA, ability, plan, IsBred", Hex(egg.Extra, 0, 4), "D18A0080");
            var ticket = From(2344, T(("plan", 2)), out _);
            Check("HoneymoonTicket: the plan and its PlanB signature", $"{ticket.Extra[0]}/{ticket.Signature}", "2/PlanB");
            var fish = From(4401, T(("size", 50), ("weight", 30), ("isRanked", true)), out _);
            Check("Fish: size, weight, IsRanked", Hex(fish.Extra, 0, 5), "32001E0001");
            var card = From(2339, T(("strength", T(("trait", true), ("rp", 3), ("rank", 2))), ("receptivity", T(("rp", 17), ("rank", 1))),
                                    ("dna", T((1, 7), (2, 0), (3, 1))), ("abilities", T((1, 9), (2, 2))), ("gender", 1), ("color", 5), ("size", 2),
                                    ("name", "Kobo")), out _);
            Check("ChocoboCard: the stat bytes, the packed word and the name", $"{Hex(card.Extra, 0, 8)}/{card.Signature}", "470000314752002B/Kobo");
            var charged = From(WarpRing, T(("remainingCharges", 5), ("timeValue1", 1000)), out _);
            Check("ItemTimerInfo: Header 1, the charges and the first time", Hex(charged.Extra, 0, 8), "01050000E8030000");
            From(4112, T((0, 0x42)), out typed);
            Check("an untyped item: false, for the raw fallback", typed ? 1 : 0, 0);
            Check("RawByte: a number, a numeric string, anything else", $"{ItemExdata.RawByte(260L)}/{ItemExdata.RawByte("5")}/{ItemExdata.RawByte(true)}", "4/5/0");

            // Exdata::toTable: the layouts read back (keys in the C++ toTable's order)
            string Show(object o) => o switch
            {
                Dictionary<object, object> d => "{" + string.Join(",", System.Linq.Enumerable.Select(d, kv => $"{kv.Key}={Show(kv.Value)}")) + "}",
                bool b => b ? "true" : "false",
                _ => o?.ToString() ?? "nil",
            };
            string To(Item item) => ItemExdata.ToTable(item, items, isFish) is { } t ? Show(t) : "nil";
            Check("toTable AugmentStandard: kind, sub kind, five augments, the signature", To(std),
                  "{augmentKind=2,augmentSubKind=3,augments={1={id=23,value=4},2={id=1,value=8},3={id=0,value=0},4={id=0,value=0},5={id=0,value=0}},signature=Kobo}");
            Check("toTable AugmentTrial: four augments and the trial", To(trial),
                  "{augmentKind=2,augmentSubKind=67,augments={1={id=23,value=0},2={id=0,value=0},3={id=0,value=0},4={id=0,value=0}},trial={id=1234,completed=true},signature=}");
            Check("toTable AugmentMezzotint", To(mezzo),
                  "{augmentKind=2,augmentSubKind=35,type=1,rank=5,accumulatedRP=300,augments={1={index=7,value=9},2={index=0,value=0},3={index=0,value=0}},signature=}");
            Check("toTable AugmentBundle", To(bundle),
                  "{augmentKind=3,augmentSubKind=131,type=2,rank=4,accumulatedRP=4660,maxRankTier=3,rpCurve=1,augmentIndex=16909060,signature=}");
            Check("toTable LotteryTicket", To(pearl), "{number=123,title=7}");
            Check("toTable GlowingLamp: the chamber back below 0x1D", To(lamp), "{chamberId=3,flags=1,startTime=100,endTime=200}");
            Check("toTable GlowingLamp: chamber 0 stays 0", To(From(5414, T(("chamberId", 0)), out _)), "{chamberId=0,flags=0,startTime=0,endTime=0}");
            Check("toTable ChocoboEgg", To(egg), "{dna={1=1,2=2,3=3},ability=5,plan=2,isBred=true}");
            Check("toTable HoneymoonTicket", To(ticket), "{plan=2}");
            Check("toTable Fish", To(fish), "{size=50,weight=30,isRanked=true}");
            Check("toTable ChocoboCard", To(card),
                  "{strength={trait=true,rp=3,rank=2},endurance={trait=false,rp=0,rank=0},discernment={trait=false,rp=0,rank=0}," +
                  "receptivity={rp=17,rank=1},dna={1=7,2=0,3=1},abilities={1=9,2=2},temperament=0,weather=0,gender=1,color=5,size=2,name=Kobo}");
            Check("toTable ItemTimerInfo", To(charged), "{remainingCharges=5,flags=0,timeValue1=1000,timeValue2=0,signature=}");
            Check("toTable an untyped item: nil, for the raw bytes", To(new Item { ItemId = 4112 }), "nil");
            var ls = From(513, T(("groupId", 77), ("groupKey", 5), ("color", T(("r", 1), ("g", 2), ("b", 3), ("a", 15))), ("flag", 1), ("name", "Kobo")), out _);
            Check("toTable Linkshell: the ids, the color, the flag and the decoded name", To(ls),
                  "{groupId=77,groupKey=5,color={r=1,g=2,b=3,a=15},flag=1,name=Kobo}");
            var pass = From(3528, T(("timestamp", 1234567), ("title", 3), ("signature", "Kobo")), out _);
            Check("toTable LegionPass", To(pass), "{timestamp=1234567,title=3,signature=Kobo}");
        }

        private static void Check(string label, string got, string want)
        {
            if (got == want) { pass_++; Console.WriteLine($"  ok   {label}"); }
            else { fail_++; Console.WriteLine($"  FAIL {label}: '{got}' != '{want}'"); }
        }

        private static void Check(string label, long got, long want)
        {
            if (got == want) { pass_++; Console.WriteLine($"  ok   {label}"); }
            else { fail_++; Console.WriteLine($"  FAIL {label}: {got} != {want}"); }
        }
    }
}
