using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using XI.Login;
using XI.Persistence;

// XI.Verify.Data.Live -- live gate for the ported data_session (0xA1 char list, 0xA2 char select,
// 0xFE) against the real MariaDB (docker xi-live-db, host:3307, xidb). Lobby view/data sockets are
// recording fakes; every DB effect is read back from the live tables.
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint Acc = 990001, Acc2 = 990002;
    private const uint Char1 = 990101, Char2 = 990102, Char3 = 990103;
    private const string Ip = "127.0.0.1";

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static void CheckTrue(string name, bool cond) => Check(name, true, cond);

    private sealed class FakeChannel : ILobbyChannel
    {
        public readonly List<byte[]> Writes = new();
        public bool Closed;
        public void Write(byte[] data) => Writes.Add(data);
        public void Close() => Closed = true;
        public byte[] Last => Writes.Count == 0 ? null : Writes[^1];
    }

    private sealed class RecSink : ICharZoneSink
    {
        public readonly List<(uint, ushort)> Zones = new();
        public void CharZone(uint charId, ushort zone) => Zones.Add((charId, zone));
    }

    private static void Sql(IDatabase db, string q, params object[] a) => LoginDb.PreparedStmt(db, q, a);

    // every table with a charid column, read from the schema at run time: chars' char_insert trigger fills char_equip, char_exp,
    // char_history, char_inventory, char_jobs, char_pet, char_points, char_profile, char_storage and char_unlocks and only
    // char_delete clears them, so a run aborted with one of them left behind would make the next run's INSERT INTO chars collide
    private static List<string> CharidTables(IDatabase db)
    {
        var tables = new List<string>();
        var r = LoginDb.PreparedStmt(db, "SELECT table_name AS t FROM information_schema.columns WHERE table_schema = DATABASE() AND column_name = 'charid' AND table_name <> 'chars'");
        while (r != null && r.Next()) tables.Add(r.Get<string>("t"));
        foreach (var t in new[] { "char_equip", "char_exp", "char_history", "char_inventory", "char_jobs", "char_pet", "char_points", "char_profile",
                                  "char_storage", "char_unlocks" })
            if (!tables.Contains(t)) tables.Add(t);
        return tables;
    }

    private static void Cleanup(IDatabase db)
    {
        foreach (var c in new[] { Char1, Char2, Char3 })
        {
            foreach (var t in CharidTables(db)) Sql(db, $"DELETE FROM `{t}` WHERE charid = ?", c);
            Sql(db, "DELETE FROM chars WHERE charid = ?", c);
        }
        foreach (var a in new[] { Acc, Acc2 })
        {
            Sql(db, "DELETE FROM accounts_sessions WHERE accid = ?", a);
            Sql(db, "DELETE FROM ip_exceptions WHERE accid = ?", a);
            Sql(db, "DELETE FROM accounts_profile WHERE accid = ?", a);
            Sql(db, "DELETE FROM accounts WHERE id = ?", a);
        }
    }

    private static readonly byte[] ProfileHash = Enumerable.Range(1, 16).Select(i => (byte)(i * 7)).ToArray();

    private static void Seed(IDatabase db)
    {
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)", Acc, "dsgate1", "x", (byte)3);
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)", Acc2, "dsgate2", "x", (byte)3);
        Sql(db, "INSERT INTO accounts_profile(accid, session_hash) VALUES (?, ?)", Acc, ProfileHash);
        // char 1: WAR 25, race 3, face 5, zone 230, nation 1, GM 0
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,nation,gmlevel) VALUES (?,?,?,?,?,?,?)", Char1, Acc, "Dsgatea", (ushort)230, (ushort)230, (byte)1, (byte)0);
        Sql(db, "INSERT INTO char_stats(charid,mjob,sjob) VALUES (?,?,?)", Char1, (byte)1, (byte)3);
        Sql(db, "INSERT INTO char_look(charid,race,face,head,body,hands,legs,feet,main,sub,size) VALUES (?,?,?,?,?,?,?,?,?,?,?)", Char1, (byte)3, (byte)5, (ushort)0x1001, (ushort)0x2002, (ushort)0x3003, (ushort)0x4004, (ushort)0x5005, (ushort)0x6006, (ushort)0x7007, (byte)2);
        Sql(db, "UPDATE char_jobs SET war = ? WHERE charid = ?", (byte)25, Char1);
        Sql(db, "INSERT INTO char_flags(charid,`rename`) VALUES (?,?)", Char1, (byte)1);
        Sql(db, "INSERT INTO char_vars(charid,varname,value) VALUES (?,?,?)", Char1, "[RaceChange]Eligible", 2000000000u);
        // char 2: plain
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,nation,gmlevel) VALUES (?,?,?,?,?,?,?)", Char2, Acc, "Dsgateb", (ushort)0, (ushort)231, (byte)0, (byte)0);
        Sql(db, "INSERT INTO char_stats(charid,mjob,sjob) VALUES (?,?,?)", Char2, (byte)2, (byte)0);
        Sql(db, "INSERT INTO char_look(charid) VALUES (?)", Char2);
    }

    private static byte[] ListReq(uint acc, uint serverIp, string hashKey)
    {
        var b = new byte[64];
        b[0] = 0xA1;
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(1), acc);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(5), serverIp);
        Encoding.Latin1.GetBytes(hashKey).CopyTo(b, 12);
        return b;
    }

    private static byte[] SelectReq(byte fill = 0x55)
    {
        var b = new byte[64];
        b[0] = 0xA2;
        for (int i = 1; i <= 20; i++) b[i] = fill;
        return b;
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
            Seed(db);
            Run(db);
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

    private static uint U32(byte[] b, int o) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o));
    private static ushort U16(byte[] b, int o) => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(o));
    private static string Str(byte[] b, int o, int len) { var s = b.AsSpan(o, len); int z = s.IndexOf((byte)0); return Encoding.UTF8.GetString(z < 0 ? s : s[..z]); }

    private static bool HashOk(byte[] pkt, int len)
    {
        var copy = (byte[])pkt.Clone();
        Array.Clear(copy, 12, 16);
        return MD5.HashData(copy.AsSpan(0, len)).AsSpan().SequenceEqual(pkt.AsSpan(12, 16));
    }

    private static (LoginSessions, LoginSession, DataSession, FakeChannel self, FakeChannel view, RecSink sink, string key) NewSession(
        IDatabase db, LobbySettings ls, uint acc = Acc, uint requested = Char1)
    {
        var sessions = new LoginSessions();
        var hash = new byte[16]; RandomNumberGenerator.Fill(hash);
        var key = LoginSessions.HashKey(hash);
        var s = sessions.GetAuthenticatedSession(Ip, key);
        s.AccountId = acc; s.RequestedCharacterId = requested; s.KeyCounter = 0x100;
        var self = new FakeChannel(); var view = new FakeChannel(); var sink = new RecSink();
        s.View = view;
        var ds = new DataSession(Ip, self, db, sessions, sink, ls);
        return (sessions, s, ds, self, view, sink, key);
    }

    private static uint Count(IDatabase db, string q, params object[] a)
    {
        var rs = LoginDb.PreparedStmt(db, q, a);
        return rs != null && rs.Next() ? rs.Get<uint>("c") : uint.MaxValue;
    }

    private static void Run(IDatabase db)
    {
        var ls = new LobbySettings { ServerName = "Nameless", ZonePlayerCap = 0 };

        // ---- unknown / missing session hash -> no reaction
        {
            var (_, _, ds, self, view, _, _) = NewSession(db, ls);
            ds.Handle(ListReq(Acc, 0x0100007F, new string('\u0001', 16)));
            Check("unknown hash: no data write", 0, self.Writes.Count);
            Check("unknown hash: no view write", 0, view.Writes.Count);
        }

        // ---- 0xA1
        var (sessions, session, data, dself, dview, sink, key) = NewSession(db, ls);
        data.Handle(ListReq(Acc, 0x0100007F, key));
        CheckTrue("A1 binds data session", session.Data == data);
        Check("A1 data reply count", 1, dself.Writes.Count);
        var u = dself.Last;
        Check("A1 data reply len", 0x148, u.Length);
        Check("A1 data cmd", 3, u[0]);
        Check("A1 data count (2 chars + 1 free slot)", 3, u[1]);
        Check("A1 uList[0] contentId", Char1, U32(u, 16));
        Check("A1 uList[0] charIdMain", (ushort)(Char1 & 0xFFFF), U16(u, 20));
        Check("A1 uList[0] extra", (byte)((Char1 >> 16) & 0xFF), u[23]);
        Check("A1 uList[1] contentId", Char2, U32(u, 32));
        Check("A1 serverIp stored", 0x0100007Fu, session.ServerIp);

        var v = dview.Last;
        int expLen = 28 + 4 + 140 * 3;
        Check("A1 view len", expLen, v.Length);
        Check("A1 view packet_size", (uint)expLen, U32(v, 0));
        Check("A1 view terminator", 0x46465849u, U32(v, 4));
        Check("A1 view command", 0x20u, U32(v, 8));
        CheckTrue("A1 view md5", HashOk(v, expLen));
        Check("A1 view characters", 3u, U32(v, 28));
        int s0 = 32, s1 = 32 + 140, s2 = 32 + 280;
        Check("A1 c0 ffxi_id", Char1, U32(v, s0));
        Check("A1 c0 ffxi_id_world", (ushort)(Char1 & 0xFFFF), U16(v, s0 + 4));
        Check("A1 c0 status", 1, U16(v, s0 + 8));
        Check("A1 c0 bitfield (rename|race_change)", 3, v[s0 + 10]);
        Check("A1 c0 name", "Dsgatea", Str(v, s0 + 12, 16));
        Check("A1 c0 world", "Nameless", Str(v, s0 + 28, 16));
        int mk = s0 + 44;
        Check("A1 c0 mon_no", 3, U16(v, mk + 0));
        Check("A1 c0 mjob", 1, v[mk + 2]);
        Check("A1 c0 sjob", 3, v[mk + 3]);
        Check("A1 c0 mjob_level", 25, v[mk + 29]);
        Check("A1 c0 face_no", 5, U16(v, mk + 4));
        Check("A1 c0 hair_no", 5, v[mk + 8]);
        Check("A1 c0 town_no", 1, v[mk + 6]);
        Check("A1 c0 size", 2, v[mk + 9]);
        Check("A1 c0 zone_no", (byte)230, v[mk + 28]);
        Check("A1 c0 zone_no2", 0, v[mk + 35]);
        Check("A1 c0 grap[0..7]", "5,4097,8194,12291,16388,20485,24582,28679",
              string.Join(",", Enumerable.Range(0, 8).Select(g => U16(v, mk + 12 + 2 * g))));
        Check("A1 c1 name", "Dsgateb", Str(v, s1 + 12, 16));
        Check("A1 c1 bitfield", 0, v[s1 + 10]);
        Check("A1 empty slot status", 1, U16(v, s2 + 8));
        Check("A1 empty slot name[0]", 0x20, v[s2 + 12]);

        // ---- cached second read is the same list
        data.Handle(ListReq(Acc, 0x0100007F, key));
        CheckTrue("A1 cached view identical", dview.Last.AsSpan().SequenceEqual(v));
        CheckTrue("A1 cached data identical", dself.Last.AsSpan().SequenceEqual(u));

        // ---- wrong account id -> ignored
        int before = dview.Writes.Count;
        data.Handle(ListReq(Acc + 1, 0, key));
        Check("A1 wrong account ignored", before, dview.Writes.Count);

        // ---- cache mutators
        data.DeleteCharFromCharInfo(Char2);
        data.Handle(ListReq(Acc, 0x0100007F, key));
        Check("delete: c1 name[0] = space", 0x20, dview.Last[s1 + 12]);
        Check("delete: c1 status", 1, U16(dview.Last, s1 + 8));
        var newChar = new byte[140];
        BinaryPrimitives.WriteUInt32LittleEndian(newChar, 990200u);
        BinaryPrimitives.WriteUInt16LittleEndian(newChar.AsSpan(8), 1);
        Encoding.ASCII.GetBytes("Added").CopyTo(newChar, 12);
        data.AddCharIntoCharInfo(newChar);
        data.Handle(ListReq(Acc, 0x0100007F, key));
        Check("add: first empty slot filled", "Added", Str(dview.Last, s1 + 12, 16));
        data.RenameCharInCharInfo(990200u, "Renamed");
        data.Handle(ListReq(Acc, 0x0100007F, key));
        Check("rename: name", "Renamed", Str(dview.Last, s1 + 12, 16));

        // ---- 0xFE
        data.Handle(new byte[] { 0xFE });
        Check("FE: empty write", 0, dself.Last.Length);

        // ---- 0xA2 error paths
        {
            // unknown zone (char zone has no zone_settings row)
            Sql(db, "UPDATE chars SET pos_zone = 60000 WHERE charid = ?", Char1);
            var (_, _, ds, _, view, _, k) = NewSession(db, ls);
            ds.Handle(ListReq(Acc, 0x0100007F, k));
            view.Writes.Clear();
            ds.Handle(SelectReq());
            var e = view.Last;
            Check("A2 no zone_settings -> 305 error", (ushort)305, U16(e, 32));
            Check("A2 error pkt len", 0x24, e.Length);
            Sql(db, "UPDATE chars SET pos_zone = 230 WHERE charid = ?", Char1);

            // accountID 0 -> error to self + close
            var (_, _, dsa, selfa, _, _, ka) = NewSession(db, ls, acc: 0);
            dsa.Handle(ListReq(0, 0, ka));
            dsa.Handle(SelectReq());
            Check("A2 account 0: 332 to self", (ushort)332, U16(selfa.Last, 32));
            CheckTrue("A2 account 0: self closed", selfa.Closed);
        }
        {
            // maintenance mode, non-GM
            var m = new LobbySettings { ServerName = "Nameless", MaintMode = 1, ZonePlayerCap = 0 };
            var (_, _, ds, self, view, _, k) = NewSession(db, m);
            ds.Handle(ListReq(Acc, 0x0100007F, k));
            Check("maint: list -> 332 to view", (ushort)332, U16(view.Last, 32));
            Check("maint: no data list sent", 0, self.Writes.Count);
            ds.Handle(SelectReq());
            Check("maint: select -> 332", (ushort)332, U16(view.Last, 32));
            Check("maint: no session row", 0u, Count(db, "SELECT COUNT(*) c FROM accounts_sessions WHERE accid = ?", Acc));
        }
        {
            // zone cap 1, reserved 0, with one other session in zone 230 -> full
            var cap = new LobbySettings { ServerName = "Nameless", ZonePlayerCap = 1, ZonePlayerGmReserved = 0 };
            Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone) VALUES (?,?,?,?)", Char3, Acc2, "Dsgatec", (ushort)230);
            Sql(db, "INSERT INTO accounts_sessions(accid,charid) VALUES (?,?)", Acc2, Char3);
            var (_, _, ds, _, view, _, k) = NewSession(db, cap);
            ds.Handle(ListReq(Acc, 0x0100007F, k));
            view.Writes.Clear();
            ds.Handle(SelectReq());
            Check("zone cap: 208 WORLD_IS_FULL", (ushort)208, U16(view.Last, 32));
            Check("zone cap: no session inserted", 0u, Count(db, "SELECT COUNT(*) c FROM accounts_sessions WHERE accid = ?", Acc));
            // instanced zones are exempt
            var inst = new LobbySettings { ServerName = "Nameless", ZonePlayerCap = 1, ZonePlayerGmReserved = 0, IsInstancedZone = z => z == 230 };
            var (_, _, ds2, _, view2, _, k2) = NewSession(db, inst);
            ds2.Handle(ListReq(Acc, 0x0100007F, k2));
            view2.Writes.Clear();
            ds2.Handle(SelectReq());
            Check("instanced zone exempt from cap: handoff", 72, view2.Last.Length);
            Sql(db, "DELETE FROM accounts_sessions WHERE accid IN (?, ?)", Acc, Acc2);
            Sql(db, "DELETE FROM chars WHERE charid = ?", Char3);
        }
        {
            // login limit (1) with an existing session from the same client address
            var lim = new LobbySettings { ServerName = "Nameless", LoginLimit = 1, ZonePlayerCap = 0 };
            Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone) VALUES (?,?,?,?)", Char3, Acc2, "Dsgatec", (ushort)231);
            Sql(db, "INSERT INTO accounts_sessions(accid,charid,client_addr) VALUES (?,?,?)", Acc2, Char3, 0x0100007Fu);
            var (_, _, ds, _, view, snk, k) = NewSession(db, lim);
            ds.Handle(ListReq(Acc, 0x0100007F, k));
            view.Writes.Clear();
            ds.Handle(SelectReq());
            Check("login limit: 332", (ushort)332, U16(view.Last, 32));
            Check("login limit: no CharZone", 0, snk.Zones.Count);
            // ip exception lifts the limit
            Sql(db, "INSERT INTO ip_exceptions(accid, exception) VALUES (?, DATE_ADD(NOW(), INTERVAL 1 DAY))", Acc);
            var (_, _, ds2, _, view2, _, k2) = NewSession(db, lim);
            ds2.Handle(ListReq(Acc, 0x0100007F, k2));
            view2.Writes.Clear();
            ds2.Handle(SelectReq());
            Check("ip exception lifts login limit", 72, view2.Last.Length);
            Sql(db, "DELETE FROM ip_exceptions WHERE accid = ?", Acc);
            Sql(db, "DELETE FROM accounts_sessions WHERE accid IN (?, ?)", Acc, Acc2);
            Sql(db, "DELETE FROM chars WHERE charid = ?", Char3);
        }

        // ---- 0xA2 success
        {
            var ok = new LobbySettings { ServerName = "Nameless", ZonePlayerCap = 0, LogUserIp = true, SearchPort = 54002 };
            var (ss, s, ds, self, view, snk, k) = NewSession(db, ok);
            ds.Handle(ListReq(Acc, 0x0A0B0C0D, k));
            view.Writes.Clear();
            Sql(db, "UPDATE char_stats SET zoning = 0 WHERE charid = ?", Char1);
            Sql(db, "UPDATE char_flags SET disconnecting = 1 WHERE charid = ?", Char1);
            ds.Handle(SelectReq(0x55));
            var r = view.Last;
            Check("A2 reply len", 72, r.Length);
            Check("A2 packet_size", 0x48u, U32(r, 0));
            Check("A2 command", 0x0Bu, U32(r, 8));
            CheckTrue("A2 md5", HashOk(r, 72));
            Check("A2 ffxi_id", Char1, U32(r, 28));
            Check("A2 ffxi_id_world", Char1 & 0xFFFF, U32(r, 32));
            Check("A2 name", "Dsgatea", Str(r, 36, 16));
            Check("A2 server_id", (Char1 >> 16) & 0xFF, U32(r, 52));
            Check("A2 server_ip", 0x0100007Fu, U32(r, 56));
            Check("A2 server_port", 54230u, U32(r, 60));
            Check("A2 cache_ip", 0x0A0B0C0Du, U32(r, 64));
            Check("A2 cache_port", 54002u, U32(r, 68));
            CheckTrue("A2 view closed", view.Closed);
            CheckTrue("A2 session.View cleared", s.View == null);
            Check("A2 CharZone sink", "990101:230", string.Join(",", snk.Zones.Select(z => $"{z.Item1}:{z.Item2}")));
            Check("A2 char_stats.zoning", 2u, Count(db, "SELECT zoning c FROM char_stats WHERE charid = ?", Char1));
            Check("A2 char_flags.disconnecting", 0u, Count(db, "SELECT disconnecting c FROM char_flags WHERE charid = ?", Char1));

            var row = LoginDb.PreparedStmt(db, "SELECT session_key, server_addr, server_port, client_addr, version_mismatch FROM accounts_sessions WHERE accid = ?", Acc);
            CheckTrue("A2 accounts_sessions row", row.Next());
            var sk = row.GetBlobBytes("session_key");
            CheckTrue("A2 session_key[0..16] = md5(profile hash)", sk.AsSpan(0, 16).SequenceEqual(MD5.HashData(ProfileHash)));
            Check("A2 session_key counter = keyCounter+3", 0x103u, U32(sk, 16));
            Check("A2 server_addr", 0x0100007Fu, row.Get<uint>("server_addr"));
            Check("A2 server_port col", 54230u, row.Get<uint>("server_port"));
            Check("A2 client_addr", 0x0100007Fu, row.Get<uint>("client_addr"));
            Check("A2 version_mismatch", 0u, row.Get<uint>("version_mismatch"));
            Check("A2 account_ip_record", 1u, Count(db, "SELECT COUNT(*) c FROM account_ip_record WHERE accid = ? AND charid = ? AND client_ip = '127.0.0.1'", Acc, Char1));

            // same character again from a fresh view -> 201
            var (_, _, ds2, _, view2, snk2, k2) = NewSession(db, ok);
            ds2.Handle(ListReq(Acc, 0x0100007F, k2));
            view2.Writes.Clear();
            ds2.Handle(SelectReq());
            Check("A2 already logged in -> 201", (ushort)201, U16(view2.Last, 32));
            Check("A2 already logged in: no second CharZone", 0, snk2.Zones.Count);

            // list after handoff regenerates (generatedCharInfo reset); no view -> no view write
            ds.Handle(ListReq(Acc, 0x0100007F, k));
            Check("after handoff: data list resent", 0x148, self.Last.Length);
            Check("after handoff: no view write", 1, view.Writes.Count);

            // handle_error drops the session entry
            ds.HandleError();
            CheckTrue("HandleError removes entry", ss.GetHashFromPacket(Ip, ListReq(Acc, 0, k)) == "");
        }

        // ---- 0xA2 with network.ZONE_IP_FROM_CLIENT: the client's 0xA1 address replaces a 127.x zoneip
        foreach (bool on in new[] { true, false })
        {
            Sql(db, "DELETE FROM accounts_sessions WHERE accid = ?", Acc);
            Sql(db, "UPDATE char_stats SET zoning = 0 WHERE charid = ?", Char1);
            var zs = new LobbySettings { ServerName = "Nameless", ZonePlayerCap = 0, SearchPort = 54002, ZoneIpFromClient = on };
            var (_, _, dsz, _, viewz, _, kz) = NewSession(db, zs);
            dsz.Handle(ListReq(Acc, 0x0D0C0B0A, kz));
            viewz.Writes.Clear();
            dsz.Handle(SelectReq(0x55));
            var rz = viewz.Last;
            Check($"ZONE_IP_FROM_CLIENT={on}: A2 server_ip", on ? 0x0D0C0B0Au : 0x0100007Fu, U32(rz, 56));
            Check($"ZONE_IP_FROM_CLIENT={on}: A2 server_port", 54230u, U32(rz, 60));
            Check($"ZONE_IP_FROM_CLIENT={on}: A2 cache_ip", 0x0D0C0B0Au, U32(rz, 64));
            var rowz = LoginDb.PreparedStmt(db, "SELECT server_addr, client_server_addr FROM accounts_sessions WHERE accid = ?", Acc);
            CheckTrue($"ZONE_IP_FROM_CLIENT={on}: row", rowz.Next());
            Check($"ZONE_IP_FROM_CLIENT={on}: server_addr stays internal", 0x0100007Fu, rowz.Get<uint>("server_addr"));
            Check($"ZONE_IP_FROM_CLIENT={on}: client_server_addr", on ? 0x0D0C0B0Au : 0u, rowz.Get<uint>("client_server_addr"));
        }
        Sql(db, "DELETE FROM accounts_sessions WHERE accid = ?", Acc);

        // prevzone set when 0
        {
            Sql(db, "DELETE FROM accounts_sessions WHERE accid = ?", Acc);
            Sql(db, "UPDATE chars SET pos_prevzone = 0 WHERE charid = ?", Char1);
            var (_, _, ds, _, _, _, k) = NewSession(db, ls);
            ds.Handle(ListReq(Acc, 0x0100007F, k));
            ds.Handle(SelectReq());
            Check("pos_prevzone filled from zoneid", 230u, Count(db, "SELECT pos_prevzone c FROM chars WHERE charid = ?", Char1));
        }
    }
}
