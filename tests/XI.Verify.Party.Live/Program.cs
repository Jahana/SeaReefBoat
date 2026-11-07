using System;
using System.Collections.Generic;
using System.IO;
using XI.Ipc;
using XI.Map;
using XI.Persistence;
using XI.Zone;

// XI.Verify.Party.Live -- live gate for the ported parties and alliances (ZoneHost.Party: c2s 0x06E 0x06F 0x070 0x071
// 0x074 0x076 0x077 0x078, the party IPC, GROUP_TBL / GROUP_LIST) and linkshells (ZoneHost.Linkshell: c2s 0x0C3
// 0x0C4 0x0E1 0x0E2 0x0E4, the 0x071 / 0x077 / 0x0B5 linkshell kinds, the linkshell IPC) against the real MariaDB
// (xidb), whose accounts_parties holds the party and linkshells the shells. One zone process; the world's routing is a
// loop that hands every message back.
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint IdBase = 990600;
    private static readonly (uint Id, string Name)[] Chars =
    {
        (990601, "Ptaa"), (990602, "Ptbb"), (990603, "Ptcc"), (990604, "Ptdd"),
    };

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
        foreach (var (id, _) in Chars)
        {
            foreach (var t in CharidTables(db))
                Sql(db, $"DELETE FROM `{t}` WHERE charid = ?", BoundValue.Of(id));
            Sql(db, "DELETE FROM char_blacklist WHERE charid_owner = ? OR charid_target = ?", BoundValue.Of(id), BoundValue.Of(id));
            Sql(db, "DELETE FROM chars WHERE charid = ?", BoundValue.Of(id));
            Sql(db, "DELETE FROM accounts WHERE id = ?", BoundValue.Of(id));
        }
        Sql(db, "DELETE FROM linkshells WHERE name LIKE 'Ptls%'");
    }

    private static void Seed(IDatabase db)
    {
        foreach (var (id, name) in Chars)
        {
            Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of("pt" + name), BoundValue.Of("x"), BoundValue.Of((byte)1));
            Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,nation,gmlevel) VALUES (?,?,?,?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(name), BoundValue.Of((ushort)240), BoundValue.Of((ushort)0),
                BoundValue.Of((byte)2), BoundValue.Of((byte)0));
            Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(0u), BoundValue.Of((ushort)0));
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
            Seed(db);
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
        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(null, grades, new AbilityTable(), stats), dataDir, true, log.Add);

        // the world: every message comes back to this process, through CBOR
        var queue = new Queue<IIpcMessage>();
        var sent = new List<IIpcMessage>();
        zone.SendIpc = m => { sent.Add(m); queue.Enqueue(m); };
        void Pump()
        {
            for (int round = 0; round < 8; round++)
            {
                while (queue.Count > 0) zone.OnIpc(IpcFraming.Decode(IpcFraming.ToBytesWithHeader(queue.Dequeue())));
                zone.Tick();
                if (queue.Count == 0) return;
            }
        }

        var cs = new CharData[Chars.Length];
        var ss = new MapSession[Chars.Length];
        for (int i = 0; i < Chars.Length; i++)
        {
            var c = new CharData { Id = Chars[i].Id, Name = Chars[i].Name, Nation = 2, Zone = 240, X = -140f + i, Y = -7f, Z = 172f, MJob = 5, MLvl = 10 };
            c.Look.Race = 1;
            c.JobLevels[5] = 10;
            c.MaxHp = c.Hp = 100;
            c.MaxMp = c.Mp = 50;
            cs[i] = c;
            ss[i] = new MapSession { Char = c, CharId = c.Id };
        }
        var (a, b, c3, d) = (cs[0], cs[1], cs[2], cs[3]);
        var (sa, sb, sc, sd) = (ss[0], ss[1], ss[2], ss[3]);

        void Send(MapSession s, ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(s, opcode, new BasicPacket(buf));
            zone.AfterParse(s);
        }
        List<BasicPacket> Of(MapSession s, int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);
        void Clear() { foreach (var s in ss) s.PacketList.Clear(); sent.Clear(); log.Clear(); }
        void Name(byte[] buf, int off, string name) => System.Text.Encoding.ASCII.GetBytes(name).CopyTo(buf, off);

        // accounts_parties for one char: partyid, allianceid, partyflag, or "-" without a row
        string Row(CharData c)
        {
            var rs = Sql(db, "SELECT partyid, allianceid, partyflag FROM accounts_parties WHERE charid = ?", BoundValue.Of(c.Id));
            if (rs == null || !rs.Next()) return "-";
            return $"{rs.Get<uint>("partyid")}/{rs.Get<uint>("allianceid")}/{rs.Get<uint>("partyflag")}";
        }
        // GROUP_TBL: kind and the member ids in order
        string Tbl(BasicPacket p)
        {
            var ids = new List<string>();
            for (int i = 0; i < 20; i++)
            {
                uint id = BitConverter.ToUInt32(p.Buffer, 8 + i * 12);
                if (id != 0) ids.Add((id - IdBase).ToString());
            }
            return $"{p.Buffer[4]}:{string.Join(",", ids)}";
        }
        string LastTbl(MapSession s) => Of(s, 0x0C8) is var l && l.Count > 0 ? Tbl(l[l.Count - 1]) : "none";
        // GROUP_LIST: the ids listed, in order
        string Lists(MapSession s) => string.Join(",", Of(s, 0x0DD).ConvertAll(p => (BitConverter.ToUInt32(p.Buffer, 4) - IdBase).ToString()));
        ushort MsgId(BasicPacket p) => BitConverter.ToUInt16(p.Buffer, 10);
        bool Told(MapSession s, ushort msg) => Of(s, 0x009).Exists(p => MsgId(p) == msg);

        void Invite(MapSession s, CharData to, byte kind) => Send(s, 0x06E, 12, buf =>
        {
            BitConverter.GetBytes(to.Id).CopyTo(buf, 4);
            BitConverter.GetBytes(to.TargId).CopyTo(buf, 8);
            buf[10] = kind;
        });
        void Answer(MapSession s, byte res) => Send(s, 0x074, 8, buf => buf[4] = res);
        void Leave(MapSession s, byte kind) => Send(s, 0x06F, 8, buf => buf[4] = kind);
        void Breakup(MapSession s, byte kind) => Send(s, 0x070, 8, buf => buf[4] = kind);
        void Strike(MapSession s, string name, byte kind) => Send(s, 0x071, 28, buf => { buf[10] = kind; Name(buf, 12, name); });
        void Change(MapSession s, string name, byte kind, byte change) => Send(s, 0x077, 24, buf => { Name(buf, 4, name); buf[20] = kind; buf[21] = change; });
        void Chat(MapSession s, string text) => Send(s, 0x0B5, (6 + text.Length + 1 + 3) & ~3, buf => { buf[4] = 4; Name(buf, 6, text); });

        Console.WriteLine("log in four chars at Port Windurst:");
        foreach (var s in ss) Send(s, 0x00A, 92, buf => BitConverter.GetBytes(((CharData)s.Char).Id).CopyTo(buf, 12));
        Pump();
        Check("act indexes given", true, a.TargId != 0 && b.TargId != 0 && c3.TargId != 0 && d.TargId != 0);
        Check("no party: no row", "-", Row(a));

        Console.WriteLine("invite (0x06E, S2C 0x0DC):");
        Clear();
        Invite(sa, a, 0);
        Check("inviting yourself is refused", true, log.Exists(l => l.Contains("Cannot invite yourself")));
        Clear();
        Invite(sa, b, 0);
        var req = Of(sb, 0x0DC);
        Check("b gets the invite", 1, req.Count);
        Check("GROUP_SOLICIT_REQ names the inviter", "Ptaa", req.Count == 1 ? System.Text.Encoding.ASCII.GetString(req[0].Buffer, 12, 4) : "");
        Check("GROUP_SOLICIT_REQ kind party", 0, req.Count == 1 ? req[0].Buffer[11] : -1);
        Check("b has the invite pending", a.Id, b.InvitePending.UniqueNo);
        Clear();
        Invite(sc, b, 0);
        Check("a second invite while one is pending: cannot invite", true, Told(sc, 23));

        Console.WriteLine("accept (0x074): the party forms, GROUP_TBL / GROUP_LIST on the next tick:");
        Clear();
        Answer(sb, 1);
        Check("a is the leader in the database", $"{a.Id}/0/4", Row(a));
        Check("b joined a's party", $"{a.Id}/0/0", Row(b));
        Check("PartyReload sent", true, sent.Exists(m => m is PartyReload r && r.PartyId == a.Id));
        Check("the invite is cleared", 0u, b.InvitePending.UniqueNo);
        Pump();
        Check("a's GROUP_TBL", "0:1,2", LastTbl(sa));
        Check("b's GROUP_TBL", "0:1,2", LastTbl(sb));
        Check("a's GROUP_LIST", "1,2", Lists(sa));
        Check("b's GROUP_LIST", "1,2", Lists(sb));
        var bl = Of(sa, 0x0DD).Find(p => BitConverter.ToUInt32(p.Buffer, 4) == b.Id);
        Check("GROUP_LIST hp", 100, bl != null ? BitConverter.ToInt32(bl.Buffer, 8) : -1);
        Check("GROUP_LIST act index", b.TargId, bl != null ? BitConverter.ToUInt16(bl.Buffer, 24) : -1);
        Check("GROUP_LIST member number", 1, bl != null ? bl.Buffer[26] : -1);
        Check("GROUP_LIST job and level", "5/10", bl != null ? $"{bl.Buffer[34]}/{bl.Buffer[35]}" : "");
        Check("GROUP_LIST name", "Ptbb", bl != null ? System.Text.Encoding.ASCII.GetString(bl.Buffer, 40, 4) : "");
        Check("GROUP_LIST size", 48, bl != null ? (bl.Buffer[1] & 0xFE) * 2 : -1);
        var al = Of(sa, 0x0DD).Find(p => BitConverter.ToUInt32(p.Buffer, 4) == a.Id);
        Check("GROUP_LIST leader flag", 4u, al != null ? BitConverter.ToUInt32(al.Buffer, 20) : 0);

        Console.WriteLine("check id (0x078), list request (0x076):");
        Clear();
        Send(sb, 0x078, 4);
        Check("GROUP_CHECKID: the party id", a.Id, Of(sb, 0x0E1) is var ck && ck.Count == 1 ? BitConverter.ToUInt32(ck[0].Buffer, 4) : 0);
        Send(sd, 0x078, 4);
        Check("GROUP_CHECKID without a party: 0", 0u, Of(sd, 0x0E1) is var ck2 && ck2.Count == 1 ? BitConverter.ToUInt32(ck2[0].Buffer, 4) : 1);
        Clear();
        Send(sb, 0x076, 8);
        Check("list request: GROUP_TBL", "0:1,2", LastTbl(sb));
        Check("list request: GROUP_LIST", "1,2", Lists(sb));
        Check("list request: only to b", 0, Of(sa, 0x0C8).Count);
        Send(sd, 0x076, 8);
        Check("list request without a party: empty GROUP_TBL", "0:", LastTbl(sd));

        Console.WriteLine("party chat (0x0B5 kind 4, IPC ChatMessageParty):");
        Clear();
        Chat(sb, "hi party");
        Check("ChatMessageParty sent", true, sent.Exists(m => m is ChatMessageParty cm && cm.PartyId == a.Id));
        Pump();
        var said = Of(sa, 0x017);
        Check("a hears b", "hi party", said.Count == 1 ? System.Text.Encoding.ASCII.GetString(said[0].Buffer, 23, 8) : "");
        Check("the sender does not hear itself", 0, Of(sb, 0x017).Count);
        Check("outside the party: not heard", 0, Of(sc, 0x017).Count + Of(sd, 0x017).Count);

        Console.WriteLine("GROUP_EFFECTS (0x076) when a member's icons change:");
        Clear();
        Check("b gains protect", true, b.StatusEffects.AddStatusEffect(new StatusEffect(40, 40, 10, 0, 60_000)));
        zone.Tick();
        var ge = Of(sa, 0x076);
        Check("a gets b's icons", $"{b.Id}/{b.TargId}/40/255", ge.Count == 1
              ? $"{BitConverter.ToUInt32(ge[0].Buffer, 4)}/{BitConverter.ToUInt16(ge[0].Buffer, 8)}/{ge[0].Buffer[20]}/{ge[0].Buffer[21]}" : $"{ge.Count}");
        var geb = Of(sb, 0x076);
        Check("b gets a's (none) and not its own", $"{a.Id}/255/0", geb.Count == 1
              ? $"{BitConverter.ToUInt32(geb[0].Buffer, 4)}/{geb[0].Buffer[20]}/{BitConverter.ToUInt32(geb[0].Buffer, 52)}" : $"{geb.Count}");
        Check("GROUP_EFFECTS size", 244, ge.Count == 1 ? (ge[0].Buffer[1] & 0xFE) * 2 : -1);
        Check("outside the party: none", 0, Of(sc, 0x076).Count + Of(sd, 0x076).Count);
        Clear();
        zone.Tick();
        Check("no change: not re-sent", 0, Of(sa, 0x076).Count);
        b.StatusEffects.DelStatusEffect(40);

        Console.WriteLine("decline (0x074 res 0):");
        Clear();
        Invite(sa, c3, 0);
        Answer(sc, 0);
        Check("a is told the invite was declined", true, Told(sa, 11));
        Check("c did not join", "-", Row(c3));

        Console.WriteLine("hand over the lead (0x077):");
        Clear();
        Change(sb, "Ptaa", 0, 0);
        Check("only the leader may", true, log.Exists(l => l.Contains("GROUP_CHANGE2")));
        Change(sa, "Ptbb", 0, 0);
        Pump();
        Check("b leads: the party takes b's id", $"{b.Id}/0/4", Row(b));
        Check("a is a member of b's party", $"{b.Id}/0/0", Row(a));
        Check("GROUP_CHECKID follows", b.Id, ((Func<uint>)(() => { sa.PacketList.Clear(); Send(sa, 0x078, 4); return BitConverter.ToUInt32(Of(sa, 0x0E1)[0].Buffer, 4); }))());
        Change(sb, "Ptaa", 0, 4);
        Pump();
        Check("a is quartermaster", $"{b.Id}/0/{0x10}", Row(a));

        Console.WriteLine("a second party (c leads, d), then an alliance (0x06E kind 5):");
        Invite(sc, d, 0);
        Answer(sd, 1);
        Pump();
        Check("c leads its party", $"{c3.Id}/0/4", Row(c3));
        Check("d in c's party", $"{c3.Id}/0/0", Row(d));
        Clear();
        Invite(sa, c3, 5);
        Check("a member cannot invite an alliance", 0, Of(sc, 0x0DC).Count);
        Invite(sb, c3, 5);
        var areq = Of(sc, 0x0DC);
        Check("c gets the alliance invite", 5, areq.Count == 1 ? areq[0].Buffer[11] : -1);
        Answer(sc, 1);
        Pump();
        Check("b leads the alliance", $"{b.Id}/{b.Id}/{0x0C}", Row(b));
        Check("a in the alliance", $"{b.Id}/{b.Id}/{0x10}", Row(a));
        Check("c's party is the second", $"{c3.Id}/{b.Id}/{0x05}", Row(c3));
        Check("d's party is the second", $"{c3.Id}/{b.Id}/{0x01}", Row(d));
        Check("d's GROUP_TBL: alliance kind, the whole alliance", "5:1,2,3,4", LastTbl(sd));
        Check("b's GROUP_TBL: alliance kind, the whole alliance", "5:1,2,3,4", LastTbl(sb));
        Check("d's GROUP_LIST covers the alliance", "1,2,3,4", Lists(sd).Length >= 7 ? SortedIds(Lists(sd)) : Lists(sd));
        var dl = Of(sd, 0x0DD).FindLast(p => BitConverter.ToUInt32(p.Buffer, 4) == d.Id);
        Check("GROUP_LIST numbers within the party", 1, dl != null ? dl.Buffer[26] : -1);

        Console.WriteLine("alliance chat (IPC ChatMessageAlliance):");
        Clear();
        Chat(sa, "hi all");
        Check("ChatMessageAlliance sent", true, sent.Exists(m => m is ChatMessageAlliance cm && cm.AllianceId == b.Id));
        Pump();
        Check("every other member hears it", 3, Of(sb, 0x017).Count + Of(sd, 0x017).Count + Of(sc, 0x017).Count);
        // m_charHistory: JOINED_PARTIES (3), JOINED_ALLIANCES (4), CHATS_SENT (9)
        Check("char history: every member joined one party and one alliance; a and b sent one chat line each",
            "1/1/1/1 1/1/1/1 1/1/0/0", $"{a.History[3]}/{b.History[3]}/{c3.History[3]}/{d.History[3]} " +
            $"{a.History[4]}/{b.History[4]}/{c3.History[4]}/{d.History[4]} {a.History[9]}/{b.History[9]}/{c3.History[9]}/{d.History[9]}");
        Check("the sender does not", 0, Of(sa, 0x017).Count);

        Console.WriteLine("HP update fans out GROUP_ATTR to the alliance:");
        Clear();
        d.Hp = 60;
        d.UpdateMask |= CharStats.UpdateHp;
        zone.AfterParse(sd);
        Check("GROUP_ATTR to every member", 4, Of(sa, 0x0DF).Count + Of(sb, 0x0DF).Count + Of(sc, 0x0DF).Count + Of(sd, 0x0DF).Count);

        Console.WriteLine("kick a party from the alliance (0x071 kind 5):");
        Clear();
        Strike(sb, "Ptcc", 5);
        Pump();
        Check("c's party left the alliance", $"{c3.Id}/0/4", Row(c3));
        Check("d with it", $"{c3.Id}/0/0", Row(d));
        Check("b's party stays an alliance of one (as the C++ does)", $"{b.Id}/{b.Id}/{0x0C}", Row(b));
        Check("d's GROUP_TBL: party kind", "0:3,4", LastTbl(sd));

        Console.WriteLine("dissolve the alliance (0x070 kind 5, IPC AllianceDissolve):");
        Clear();
        Breakup(sa, 5);
        Check("only the alliance leader may", 0, sent.Count);
        Breakup(sb, 5);
        Check("AllianceDissolve sent", true, sent.Exists(m => m is AllianceDissolve ad && ad.AllianceId == b.Id));
        Pump();
        Check("b's alliance is gone", $"{b.Id}/0/4", Row(b));
        Check("a keeps quartermaster", $"{b.Id}/0/{0x10}", Row(a));
        Check("a's GROUP_TBL: party kind", "0:1,2", LastTbl(sa));

        Console.WriteLine("kick a member (0x071 kind 0), zone out (OnZoneOut):");
        Clear();
        Strike(sc, "Ptdd", 0);
        Pump();
        Check("d was kicked", "-", Row(d));
        Check("d's GROUP_TBL emptied", "0:", Of(sd, 0x0C8).Count > 0 ? Tbl(Of(sd, 0x0C8)[0]) : "none");
        Check("c is a party of one", $"{c3.Id}/0/4", Row(c3));
        Clear();
        zone.OnZoneOut(sb);
        Check("zoning out sends PartyReload", true, sent.Exists(m => m is PartyReload r && r.PartyId == b.Id));
        Check("the database keeps b in the party", $"{b.Id}/0/4", Row(b));
        Pump();
        var gone = Of(sa, 0x0DD).FindLast(p => BitConverter.ToUInt32(p.Buffer, 4) == b.Id);
        Check("a lists b from the database: no act index", 0, gone != null ? BitConverter.ToUInt16(gone.Buffer, 24) : -1);
        Check("a lists b's zone", 240, gone != null ? BitConverter.ToUInt16(gone.Buffer, 32) : -1);
        Check("a lists b by name", "Ptbb", gone != null ? System.Text.Encoding.ASCII.GetString(gone.Buffer, 40, 4) : "");

        var hist = Sql(db, "SELECT CONCAT(joined_parties, '/', joined_alliances, '/', chats_sent) AS h FROM char_history WHERE charid = ?", BoundValue.Of(b.Id));
        Check("char history written on zone-out", "1/1/1", hist != null && hist.Next() ? hist.Get<string>("h") : "none");
        Array.Clear(b.History);

        Console.WriteLine("b back (0x00A): the party is rebuilt from the database:");
        Clear();
        ss[1] = sb = new MapSession { Char = b, CharId = b.Id };
        Send(sb, 0x00A, 92, buf => BitConverter.GetBytes(b.Id).CopyTo(buf, 12));
        Pump();
        Check("b's GROUP_TBL", "0:1,2", LastTbl(sb));
        Send(sb, 0x078, 4);
        Check("b is in its party again", b.Id, Of(sb, 0x0E1) is var ck3 && ck3.Count == 1 ? BitConverter.ToUInt32(ck3[0].Buffer, 4) : 0);
        Check("char history read back on zone-in; the rebuilt party is no new join", "1/1/1", $"{b.History[3]}/{b.History[4]}/{b.History[9]}");

        Console.WriteLine("the leader leaves (0x06F), the last disbands (0x070):");
        Clear();
        Leave(sb, 0);
        Pump();
        Check("b left", "-", Row(b));
        Check("a leads now", $"{a.Id}/0/{0x14}", Row(a));
        Clear();
        Breakup(sb, 0);
        Check("b without a party cannot disband", true, log.Exists(l => l.Contains("0x070") || l.Contains("GROUP_BREAKUP")));
        Breakup(sa, 0);
        Pump();
        Check("disbanded: a's row gone", "-", Row(a));
        Check("disbanded: empty GROUP_TBL to a", "0:", Of(sa, 0x0C8).Count > 0 ? Tbl(Of(sa, 0x0C8)[0]) : "none");
        Breakup(sc, 0);
        Pump();
        Check("c's party of one disbands", "-", Row(c3));

        Console.WriteLine("level sync (0x077 change 6 / 7, CParty::SetSyncTarget):");
        Clear();
        Invite(sa, b, 0);
        Answer(sb, 1);
        Invite(sa, c3, 0);
        Answer(sc, 1);
        Pump();
        Check("a, b and c in a's party", $"{a.Id}/{a.Id}/{a.Id}", $"{Row(a).Split('/')[0]}/{Row(b).Split('/')[0]}/{Row(c3).Split('/')[0]}");
        (a.JobLevels[5], a.MLvl, b.JobLevels[5], b.MLvl) = (30, 30, 20, 20);
        c3.MLvl = 9;
        Clear();
        Change(sa, "Ptcc", 0, 6);
        Check("a designee under 10: refused (541, the minimum 10)", "541/10",
              Of(sa, 0x029) is var low && low.Count == 1 ? $"{BitConverter.ToUInt16(low[0].Buffer, 24)}/{BitConverter.ToUInt32(low[0].Buffer, 16)}" : "none");
        c3.MLvl = 10;
        b.StatusEffects.AddStatusEffect(new StatusEffect(157, 157, 0, 0, 0));
        Clear();
        Change(sa, "Ptcc", 0, 6);
        Check("a member under SJ Restriction: refused (543)", true, Of(sa, 0x029).Exists(p => BitConverter.ToUInt16(p.Buffer, 24) == 543));
        b.StatusEffects.DelStatusEffectSilent(157);
        a.Tp = 1000;
        b.StatusEffects.AddStatusEffect(new StatusEffect(40, 40, 10, 0, 60_000));
        Clear();
        Change(sa, "Ptcc", 0, 6);
        var syncA = a.StatusEffects.GetStatusEffect(EffectId.LevelSync);
        Check("synced: LevelSync at the designee's level on each", "10/10/10",
              $"{syncA?.Power}/{b.StatusEffects.GetStatusEffect(EffectId.LevelSync)?.Power}/{c3.StatusEffects.GetStatusEffect(EffectId.LevelSync)?.Power}");
        Check("synced: a permanent effect", 0L, syncA?.DurationMs ?? -1);        Check("synced: the members told (238)", true, Told(sa, 238) && Told(sb, 238) && Told(sc, 238));
        Check("synced: TP gone, the dispelable effects gone", "0/False", $"{a.Tp}/{b.StatusEffects.HasStatusEffect(40)}");
        Check("synced: c carries PARTY_SYNC", (uint)0x100, uint.Parse(Row(c3).Split('/')[2]) & 0x100);
        Pump();
        var cl = Of(sa, 0x0DD).FindLast(p => BitConverter.ToUInt32(p.Buffer, 4) == c3.Id);
        Check("GROUP_LIST: c's sync flag", true, cl != null && (BitConverter.ToUInt32(cl.Buffer, 20) & 0x100) != 0);
        d.StatusEffects.AddStatusEffect(new StatusEffect(EffectId.LevelSync, EffectId.LevelSync, 10, 0, 0));
        Clear();
        Invite(sa, d, 0);
        Check("inviting a char under level sync: 236", true, Told(sa, 236));
        Check("no invite for it", 0, Of(sd, 0x0DC).Count);
        d.StatusEffects.DelStatusEffectSilent(EffectId.LevelSync);
        Clear();
        Invite(sa, d, 0);
        Check("an invite from a synced party warns (235)", "1/True", $"{Of(sd, 0x0DC).Count}/{Told(sd, 235)}");
        Answer(sd, 0);
        Clear();
        Change(sa, "Ptcc", 0, 7);
        Check("sync off: c's PARTY_SYNC cleared", 0u, uint.Parse(Row(c3).Split('/')[2]) & 0x100);
        Check("sync off: each LevelSync runs out in 30 s", "30000/30000/30000",
              $"{a.StatusEffects.GetStatusEffect(EffectId.LevelSync)?.DurationMs}/{b.StatusEffects.GetStatusEffect(EffectId.LevelSync)?.DurationMs}/{c3.StatusEffects.GetStatusEffect(EffectId.LevelSync)?.DurationMs}");
        Check("sync off: the members told (553, 30 s)", "553/30",
              Of(sb, 0x029) is var off && off.Count > 0 ? $"{BitConverter.ToUInt16(off[0].Buffer, 24)}/{BitConverter.ToUInt32(off[0].Buffer, 16)}" : "none");
        foreach (var m in new[] { a, b, c3 }) m.StatusEffects.DelStatusEffectSilent(EffectId.LevelSync);
        (a.JobLevels[5], a.MLvl, b.JobLevels[5], b.MLvl) = (10, 10, 10, 10);
        Breakup(sa, 0);
        Pump();
        Check("sync party disbanded", "-/-/-", $"{Row(a)}/{Row(b)}/{Row(c3)}");

        Console.WriteLine("blacklist (0x03C 0x03D, S2C 0x041 0x042, game-ok, invites from the blacklisted):");
        void Edit(MapSession s, string name, byte mode) => Send(s, 0x03D, 28, buf => { Name(buf, 8, name); buf[24] = mode; });
        string Listed(BasicPacket p)
        {
            var e = new List<string>();
            for (int i = 0; i < p.Buffer[245]; i++)
                e.Add($"{BitConverter.ToUInt32(p.Buffer, 4 + i * 20) - IdBase}={System.Text.Encoding.ASCII.GetString(p.Buffer, 8 + i * 20, 16).TrimEnd('\0')}");
            return $"{p.Buffer[244]}:{string.Join(",", e)}";
        }
        string EditReply(MapSession s) => Of(s, 0x042) is var r && r.Count == 1
            ? $"{r[0].Buffer[24]}:{BitConverter.ToUInt32(r[0].Buffer, 4)}:{System.Text.Encoding.ASCII.GetString(r[0].Buffer, 8, 16).TrimEnd('\0')}"
            : $"{r.Count} replies";
        int Rows() => (int)Sql(db, "SELECT * FROM char_blacklist WHERE charid_owner = ?", BoundValue.Of(a.Id)).RowsCount();

        Clear();
        Send(sa, 0x03C, 28);
        Check("an empty blacklist: reset + last, no entries", "3:", Of(sa, 0x041) is var bl0 && bl0.Count == 1 ? Listed(bl0[0]) : "none");
        Clear();
        Edit(sa, "Ptbb", 0);
        Check("add b: BLACK_EDIT Add with b's account id", $"0:{b.Id}:Ptbb", EditReply(sa));
        Check("add b: a row", 1, Rows());
        Clear();
        Edit(sa, "Ptbb", 0);
        Check("add b again: Error", "2:0:", EditReply(sa));
        Clear();
        Edit(sa, "Nobodyhere", 0);
        Check("an unknown name: Error", "2:0:", EditReply(sa));
        Clear();
        Edit(sa, "Ptcc", 2);
        Check("Mode 2 refused", true, Of(sa, 0x042).Count == 0 && log.Exists(l => l.StartsWith("zone: Invalid")));
        Clear();
        Send(sa, 0x03C, 28);
        Check("the list: the remainder page is last but does not reset", $"2:{b.Id - IdBase}=Ptbb", Of(sa, 0x041) is var bl1 && bl1.Count == 1 ? Listed(bl1[0]) : "none");
        Clear();
        Send(sa, 0x00C, 12);
        Check("game-ok sends the blacklist", $"2:{b.Id - IdBase}=Ptbb", Of(sa, 0x041) is var bl2 && bl2.Count == 1 ? Listed(bl2[0]) : "none");

        Clear();
        Invite(sb, a, 0);
        Pump();
        Check("b cannot invite a, who has b blacklisted", true, Of(sa, 0x0DC).Count == 0 && log.Exists(l => l.Contains("inviter blacklisted")));
        Clear();
        Send(sb, 0x11C, 12, buf => { BitConverter.GetBytes(a.Id).CopyTo(buf, 4); buf[10] = 0; });
        Check("b's party request to a is dropped", false, sent.Exists(m => m is PartyRequest));
        Clear();
        Send(sc, 0x11C, 12, buf => { BitConverter.GetBytes(a.Id).CopyTo(buf, 4); buf[10] = 0; });
        Check("c's party request to a goes out", true, sent.Exists(m => m is PartyRequest r && r.RequesterId == c3.Id));
        Clear();
        Invite(sa, b, 0);
        Pump();
        Check("a can still invite b", 1, Of(sb, 0x0DC).Count);
        Answer(sb, 0);
        Pump();

        Clear();
        Edit(sa, "Ptbb", 1);
        Check("remove b: BLACK_EDIT Delete", $"1:{b.Id}:Ptbb", EditReply(sa));
        Check("remove b: the row is gone", 0, Rows());
        Clear();
        Edit(sa, "Ptbb", 1);
        Check("remove b again: Error", "2:0:", EditReply(sa));
        Clear();
        Invite(sb, a, 0);
        Pump();
        Check("off the list, b can invite a", 1, Of(sa, 0x0DC).Count);
        Answer(sa, 0);
        Pump();

        // ================= linkshells =================
        Console.WriteLine("linkshells (0x0C3 0x0C4 0x0E1 0x0E2 0x0E4, the 0x071 0x077 0x0B5 linkshell kinds, S2C 0x0CC 0x0E0):");
        Check("name packing: Test", "B854D4FC00", Convert.ToHexString(LsEncode("Test"), 0, 5));
        Check("name packing: Ab (a 4-bit tail)", "6C2F00", Convert.ToHexString(LsEncode("Ab"), 0, 3));
        foreach (var x in cs) x.StorageSize[0] = 30;

        void GiveRow(CharData c, Item it)
        {
            c.Items.Add(it);
            Sql(db, "INSERT INTO char_inventory (charid, location, slot, itemId, quantity, signature, extra) VALUES (?,?,?,?,?,?,?)",
                BoundValue.Of(c.Id), BoundValue.Of((byte)it.Location), BoundValue.Of(it.Slot), BoundValue.Of(it.ItemId), BoundValue.Of(it.Quantity),
                BoundValue.Of(it.Signature), BoundValue.Of(it.Extra));
        }
        void Active(MapSession s, byte index, byte flg, byte lsNum, string name = "", byte r = 0, byte g = 0, byte bl = 0) => Send(s, 0x0C4, 28, buf =>
        {
            buf[4] = (byte)(g << 4 | r);
            buf[5] = (byte)(0xF0 | bl);
            buf[6] = index;
            buf[8] = flg;
            Array.Copy(LsEncode(name), 0, buf, 12, 15);
            buf[27] = lsNum;
        });
        void LsChat(MapSession s, byte kind, string text) => Send(s, 0x0B5, (6 + text.Length + 1 + 3) & ~3, buf => { buf[4] = kind; Name(buf, 6, text); });
        void SetMsg(MapSession s, byte flags, byte level, string text = "") => Send(s, 0x0E2, 144, buf => { buf[4] = flags; buf[5] = (byte)(level << 2); Name(buf, 16, text); });
        void GetMsg(MapSession s, byte slot) => Send(s, 0x0E1, 144, buf => buf[5] = (byte)(slot << 6));
        string Q1(string q, params BoundValue[] args)
        {
            var rs = Sql(db, q, args);
            if (rs == null || !rs.Next()) return "-";
            var cols = new List<string>();
            for (uint i = 0; i < rs.ColumnCount(); i++)
                cols.Add(rs.IsNull(rs.ColumnName(i)) ? "null" : rs.Get<string>(i));
            return string.Join("/", cols);
        }
        string Sess(CharData c) => Q1("SELECT linkshellid1, linkshellrank1, linkshellid2, linkshellrank2 FROM accounts_sessions WHERE charid = ?", BoundValue.Of(c.Id));
        string Eq(CharData c) => Q1("SELECT GROUP_CONCAT(CONCAT(equipslotid, ':', slotid) ORDER BY equipslotid) FROM char_equip WHERE charid = ? AND equipslotid >= 16", BoundValue.Of(c.Id));
        string Text(byte[] buf, int off, int len) => System.Text.Encoding.ASCII.GetString(buf, off, len).Split('\0')[0];
        uint Lsid(Item it) => BitConverter.ToUInt32(it.Extra, 0);
        BasicPacket Last(MapSession s, int opcode) => Of(s, opcode) is var l && l.Count > 0 ? l[l.Count - 1] : null;

        // ---- create (0x0C4 on a new linkshell, 512) ----
        Clear();
        var fresh = new Item { Location = Container.Inventory, Slot = 20, ItemId = 512, Quantity = 1 };
        GiveRow(a, fresh);
        Active(sa, 20, 1, 1, "Ptlsone", 1, 2, 3);
        string row = Q1("SELECT linkshellid, color, postrights, broken FROM linkshells WHERE name = 'Ptlsone'");
        Check("create: the linkshells row (colour 0xF321, postrights 1)", true, row.EndsWith("/62241/1/0"));
        uint lsid = row == "-" ? 0 : uint.Parse(row.Split('/')[0]);
        Check("create: the item is the shell, 513", 513, fresh.ItemId);
        Check("create: LSID, type 1", $"{lsid}/1", $"{Lsid(fresh)}/{fresh.Extra[8]}");
        Check("create: colour in the exdata", 0xF321, BitConverter.ToUInt16(fresh.Extra, 6));
        Check("create: signature", "Ptlsone", fresh.Signature);
        Check("create: the name packed at Extra[9]", Convert.ToHexString(LsEncode("Ptlsone"), 0, 15), Convert.ToHexString(fresh.Extra, 9, 15));
        Check("create: char_inventory updated", "513/Ptlsone", Q1("SELECT itemId, signature FROM char_inventory WHERE charid = ? AND slot = 20", BoundValue.Of(a.Id)));
        Check("create: ITEM_ATTR, not locked", "513/0", Last(sa, 0x020) is BasicPacket at ? $"{BitConverter.ToUInt16(at.Buffer, 12)}/{at.Buffer[16]}" : "none");
        Check("create: ITEM_SAME and the status", true, Of(sa, 0x01D).Count > 0 && Of(sa, 0x037).Count > 0);
        Check("create: not equipped", "0/0/0/0", Sess(a));

        Clear();
        var fresh2 = new Item { Location = Container.Inventory, Slot = 21, ItemId = 512, Quantity = 1 };
        GiveRow(a, fresh2);
        Active(sa, 21, 1, 1, "Ptlsone", 1, 2, 3);
        Check("create: a taken name, LinkshellUnavailable (112)", true, Told(sa, 112));
        Check("create: the new linkshell stays", 512, fresh2.ItemId);
        Clear();
        Send(sa, 0x0C4, 28, buf => { buf[5] = 0xE0; buf[6] = 21; buf[8] = 1; buf[27] = 1; });
        Check("0x0C4: a not 15 refused", true, log.Exists(l => l.Contains("a not 15")));

        // ---- equip ----
        Clear();
        Active(sa, 20, 1, 1);
        Check("equip: accounts_sessions LS1, rank 1", $"{lsid}/1/0/0", Sess(a));
        Check("equip: char_equip slot 16", "16:20", Eq(a));
        Check("equip: ITEM_LIST lock 0x13", "513/19", Last(sa, 0x01F) is BasicPacket il ? $"{BitConverter.ToUInt16(il.Buffer, 8)}/{il.Buffer[12]}" : "none");
        Check("equip: GROUP_COMLINK 1, slot 20, inventory", "1/20/0", Last(sa, 0x0E0) is BasicPacket gc ? $"{gc.Buffer[4]}/{gc.Buffer[5]}/{gc.Buffer[6]}" : "none");
        Check("equip: ITEM_ATTR lock 0x13", 0x13, Last(sa, 0x020)?.Buffer[16] ?? -1);
        var st = Last(sa, 0x037);
        Check("equip: 0x037 LinkShellFlag", true, st != null && (BitConverter.ToUInt32(st.Buffer, 40) & 1u << 25) != 0);
        Check("equip: 0x037 colour", "1F2F3F", st == null ? "none" : Convert.ToHexString(st.Buffer, 49, 3));
        var zi = ZoneInfo.Load(Path.Combine(dataDir, "zones"), 240, "Port_Windurst");
        var cu = LoginPackets.CharPc(a, zi, EntityUpdate.Update, 0x1F);
        Check("equip: CHAR_PC LinkShellFlag and colour", "True/1F2F3F",
              cu == null ? "none" : $"{(BitConverter.ToUInt32(cu.Buffer, 32) & 0x20000) != 0}/{Convert.ToHexString(cu.Buffer, 36, 3)}");

        // ---- 0x0C3 make a pearl ----
        Clear();
        Send(sa, 0x0C3, 8, buf => buf[5] = 1);
        var made = a.Items.Find(x => x.ItemId == 515);
        Check("make: a linkpearl", true, made != null);
        Check("make: same LSID, type 3, signature", $"{lsid}/3/Ptlsone", made == null ? "none" : $"{Lsid(made)}/{made.Extra[8]}/{made.Signature}");
        Check("make: its row", "515", made == null ? "-" : Q1("SELECT itemId FROM char_inventory WHERE charid = ? AND slot = ?", BoundValue.Of(a.Id), BoundValue.Of(made.Slot)));
        Clear();
        Send(sc, 0x0C3, 8, buf => buf[5] = 1);
        Check("make: no linkshell, refused", true, log.Exists(l => l.Contains("Invalid linkshell item")));
        Check("make: LinkshellId 3 refused", true, Pv0C3(3));
        bool Pv0C3(byte id) { log.Clear(); Send(sa, 0x0C3, 8, buf => buf[5] = id); return log.Exists(l => l.Contains("LinkshellId")); }

        // b and d get pearls, c a sack it equips as LS2
        Item Copy(Item src, byte slot, ushort id, byte type)
        {
            var it = new Item { Location = Container.Inventory, Slot = slot, ItemId = id, Quantity = 1, Signature = src.Signature, Extra = (byte[])src.Extra.Clone() };
            it.Extra[8] = type;
            return it;
        }
        var pb = Copy(fresh, 20, 515, 3); GiveRow(b, pb);
        var pc = Copy(fresh, 20, 514, 2); GiveRow(c3, pc);
        var pd = Copy(fresh, 20, 515, 3); GiveRow(d, pd);
        Clear();
        Active(sb, 20, 1, 1);
        Active(sc, 20, 1, 2);
        Active(sd, 20, 1, 1);
        Check("b equips the pearl: rank 3", $"{lsid}/3/0/0", Sess(b));
        Check("c equips the sack as LS2: rank 2", $"0/0/{lsid}/2", Sess(c3));
        Check("c: GROUP_COMLINK 2", "2/20", Last(sc, 0x0E0) is BasicPacket gc2 ? $"{gc2.Buffer[4]}/{gc2.Buffer[5]}" : "none");
        Check("c: char_equip slot 17", "17:20", Eq(c3));
        Check("c: no LinkShellFlag for LS2", true, Last(sc, 0x037) is BasicPacket st2 && (BitConverter.ToUInt32(st2.Buffer, 40) & 1u << 25) == 0);

        // ---- chat ----
        Clear();
        LsChat(sa, 5, "hi shell");
        Pump();
        Check("chat: ChatMessageLinkshell", true, sent.Exists(m => m is ChatMessageLinkshell cm && cm.LinkshellId == lsid && cm.SenderId == a.Id));
        Check("chat: b gets it as LS1 (5)", "5/hi shell", Last(sb, 0x017) is BasicPacket cb ? $"{cb.Buffer[4]}/{Text(cb.Buffer, 23, 20)}" : "none");
        Check("chat: c gets it as LS2 (0x1B)", 0x1B, Last(sc, 0x017)?.Buffer[4] ?? -1);
        Check("chat: not back to a", 0, Of(sa, 0x017).Count);
        Clear();
        LsChat(sc, 0x1B, "from two");
        Pump();
        Check("chat on LS2: a gets it as LS1", 5, Last(sa, 0x017)?.Buffer[4] ?? -1);
        Clear();
        LsChat(sb, 0x1B, "no ls2");
        Pump();
        Check("chat on an LS2 b lacks: nothing", false, sent.Exists(m => m is ChatMessageLinkshell));

        // ---- /nominate, /vote, /random ----
        Console.WriteLine("nominate / vote / dice (0x0A0 0x0A1 0x0A2, S2C 0x078 0x079):");
        long nowMs = 1_000_000;
        zone.NowMs = () => nowMs;
        void Propose(MapSession s, byte kind, string text) => Send(s, 0x0A0, 136, buf => { buf[4] = kind; Name(buf, 5, text); });
        void Vote(MapSession s, byte index, string name) => Send(s, 0x0A1, 20, buf => { buf[4] = index; Name(buf, 5, name); });
        // SWITCH_START: proposer (from the base) / AllNum / ActIndex is the receiver's own / name / kind / body / size
        string Start(MapSession s) => Last(s, 0x078) is BasicPacket st0
            ? $"{BitConverter.ToUInt32(st0.Buffer, 4) - IdBase}/{BitConverter.ToUInt32(st0.Buffer, 8)}/{BitConverter.ToUInt16(st0.Buffer, 12) == ((CharData)s.Char).TargId}/" +
              $"{Text(st0.Buffer, 14, 15)}/{st0.Buffer[29]}/{Text(st0.Buffer, 30, 224).Replace("\n", "|")}/{st0.GetSize()}"
            : "none";
        // SWITCH_PROC: AllNum / tallies 1..8 / kind / state / QuestionNum / name / body / size
        string Proc(MapSession s)
        {
            if (Last(s, 0x079) is not BasicPacket pr) return "none";
            var votes = new List<string>();
            for (int i = 1; i <= 8; i++) votes.Add(BitConverter.ToUInt16(pr.Buffer, 8 + 2 * i).ToString());
            return $"{BitConverter.ToUInt32(pr.Buffer, 4)}/{string.Join(",", votes)}/{pr.Buffer[26]}/{pr.Buffer[27]}/{pr.Buffer[28]}/" +
                   $"{Text(pr.Buffer, 29, 15)}/{Text(pr.Buffer, 44, 256).Replace("\n", "|")}/{pr.GetSize()}";
        }
        string Msg9(MapSession s) => Last(s, 0x009) is BasicPacket m9 ? $"{MsgId(m9)}:{Text(m9.Buffer, 13, 40)}" : "none";

        Clear();
        Propose(sa, 4, "x");
        Check("Kind 4 is refused", "True/0", $"{log.Exists(l => l.Contains("Kind"))}/{Of(sa, 0x078).Count}");
        Clear();
        Propose(sa, 2, "\"Lunch now?\" yes \"\" maybe");
        Check("LS1 proposal: SWITCH_START to the proposer", "1/64/True/Ptaa/2/[Lunch now?]|1:yes|2:|3:maybe/60", Start(sa));
        Check("to the pearls, and the LS2 sack of the same shell", "True/True/True",
              $"{Start(sb).StartsWith("1/64/False/Ptaa/2/")}/{Start(sc).StartsWith("1/64/False/")}/{Start(sd).StartsWith("1/64/False/")}");
        Clear();
        Vote(sb, 2, "Ptaa");
        Check("a vote: the live tally to the voter", "64/0,1,0,0,0,0,0,0/2/0/4/Ptaa//48", Proc(sb));
        Check("and to the proposer", "64/0,1,0,0,0,0,0,0/2/0/4/Ptaa//48", Proc(sa));
        Check("not to the others", 0, Of(sc, 0x079).Count + Of(sd, 0x079).Count);
        Clear();
        Vote(sb, 1, "Ptaa");
        Check("twice: AlreadyVotedOnProposal (145)", "145:string2 Ptaa", Msg9(sb));
        Vote(sd, 4, "Ptaa");
        Check("index 4 of 3: OnlyChooseFromGivenChoices (144)", "144:Para0 3 ", Msg9(sd));
        Vote(sd, 0, "Ptaa");
        Check("index 0 too", 2, Of(sd, 0x009).Count);
        Vote(sd, 3, "Ptzz");
        Check("no such poll: NotProposedAnything (143)", "143:string2 Ptzz", Msg9(sd));
        Clear();
        Vote(sc, 1, "Ptaa");
        Vote(sd, 1, "Ptaa");
        Check("the sack and a pearl vote", "1,1,0,0,0,0,0,0|2,1,0,0,0,0,0,0", $"{Proc(sc).Split('/')[1]}|{Proc(sd).Split('/')[1]}");
        Clear();
        Propose(sa, 6, "ignored");
        const string closed = "64/2,1,0,0,0,0,0,0/2/2/4/Ptaa/[Lunch now?]|1[2]:yes|2[1]:|3[0]:maybe/88";
        Check("a /nominate again closes the poll: the results to the proposer", closed, Proc(sa));
        Check("and to every voter", "True/True/True", $"{Proc(sb) == closed}/{Proc(sc) == closed}/{Proc(sd) == closed}");
        Check("no new poll", 0, Of(sa, 0x078).Count + Of(sb, 0x078).Count);
        Vote(sb, 1, "Ptaa");
        Check("closed: NotProposedAnything", "143:string2 Ptaa", Msg9(sb));
        Clear();
        Propose(sa, 5, "");
        Check("within 60 s of the close: CannotUseCommandAtTheMoment (142)", "142:", Msg9(sa));
        nowMs += 60_000;
        Clear();
        float dx = d.X;
        d.X = 500f;
        Propose(sa, 5, "");
        Check("say without a question: 512, no body, 8 blank options", "1/512/True/Ptaa/5//32", Start(sa));
        Check("say: the chars in range", "True/True/none", $"{Start(sb).StartsWith("1/512/False/")}/{Start(sc).StartsWith("1/512/")}/{Start(sd)}");
        d.X = dx;
        Clear();
        Vote(sd, 8, "Ptaa");
        Check("anyone in the zone may vote on a say poll: option 8", "512/0,0,0,0,0,0,0,1/5/0/9/Ptaa//48", Proc(sd));
        Clear();
        nowMs += 1;
        Propose(sa, 5, "");
        Check("closed without a question: the tallies only", "512/0,0,0,0,0,0,0,1/5/2/9/Ptaa/1[0] 2[0] 3[0] 4[0] 5[0] 6[0] 7[0] 8[1] /88", Proc(sa));
        Clear();
        Propose(sb, 1, "Party?");
        Check("party without a party: NoPartyMembers (36)", "36:", Msg9(sb));
        Propose(sb, 3, "LS2?");
        Check("LS2 without one: NoLinkshellEquipped (37)", "37:", Msg9(sb));
        Propose(sc, 2, "LS1?");
        Check("LS1 for c (only an LS2): NoLinkshellEquipped", "37:", Msg9(sc));
        Clear();
        Propose(sc, 3, "Sack? a b c d e f g h i j");
        Check("LS2 proposal: kind 3, options cut at 8", "3/64/True/Ptcc/3/[Sack?]|1:a|2:b|3:c|4:d|5:e|6:f|7:g|8:h/72", Start(sc));
        Check("the shell's LS1 holders get it", "True/True/True", $"{Start(sa).StartsWith("3/64/False/Ptcc/3/")}/{Start(sb).StartsWith("3/")}/{Start(sd).StartsWith("3/")}");
        Clear();
        Vote(sa, 1, "Ptcc");
        Check("a votes with its LS1 of the shell", "64/1,0,0,0,0,0,0,0/3/0/9/Ptcc//48", Proc(sa));
        Clear();
        Propose(sc, 3, "");
        Check("closed for c and a", "True/True", $"{Proc(sc).Contains("/3/2/9/Ptcc/[Sack?]|1[1]:a|")}/{Proc(sa) == Proc(sc)}");

        Clear();
        Invite(sa, b, 0);
        Answer(sb, 1);
        Pump();
        Clear();
        nowMs += 60_000;
        Propose(sb, 1, "Party? y n");
        Check("party proposal: the party in the zone", "True/True/none/none",
              $"{Start(sa).StartsWith("2/18/False/Ptbb/1/[Party?]|1:y|2:n/")}/{Start(sb).StartsWith("2/18/True/")}/{Start(sc)}/{Start(sd)}");
        Clear();
        Vote(sd, 1, "Ptbb");
        Check("outside the party: NotProposedAnything", "143:string2 Ptbb", Msg9(sd));
        Vote(sa, 1, "Ptbb");
        Check("a member votes", "18/1,0,0,0,0,0,0,0/1/0/3/Ptbb//48", Proc(sa));
        Clear();
        d.Zone = 241;
        Propose(sd, 5, "Elsewhere?");
        d.Zone = 240;
        Vote(sa, 1, "Ptdd");
        Check("a poll in another zone: not found", "143:string2 Ptdd", Msg9(sa));
        Clear();
        Propose(sb, 1, "");
        Check("party poll closed", "18/1,0,0,0,0,0,0,0/1/2/3/Ptbb/[Party?]|1[1]:y|2[0]:n/72", Proc(sb));
        Breakup(sa, 0);
        Pump();
        d.Zone = 241;
        nowMs += 60_000;
        Propose(sd, 5, "");
        d.Zone = 240;

        Clear();
        zone.RandomBelow = n => n - 1;
        Send(sb, 0x0A2, 8, buf => buf[4] = 42);
        Check("dice: DiceRoll (88) to the roller and the chars in range", "88:string2 Ptbb string3 999/True/True",
              $"{Msg9(sb)}/{Msg9(sa) == Msg9(sb)}/{Msg9(sd) == Msg9(sb)}");

        // ---- messages ----
        Clear();
        SetMsg(sb, 0x40, 0, "Pearl post");
        Check("post as a pearl under sack rights: Denied", "3/0/3", Last(sb, 0x0CC) is BasicPacket dn ? $"{dn.Buffer[4]}/{dn.Buffer[4] >> 4}/{BitConverter.ToUInt16(dn.Buffer, 156)}" : "none");
        Clear();
        SetMsg(sa, 0x40, 0, "Welcome");
        Pump();
        Check("post: stored", "Ptaa/Welcome", Q1("SELECT poster, message FROM linkshells WHERE linkshellid = ?", BoundValue.Of(lsid)));
        var post = Last(sb, 0x0CC);
        Check("post: b gets it (attr 7, read 2, write 1, op Post)", "112/6/1/Welcome/Ptaa",
              post == null ? "none" : $"{post.Buffer[4]}/{post.Buffer[5]}/{BitConverter.ToUInt16(post.Buffer, 156)}/{Text(post.Buffer, 8, 128)}/{Text(post.Buffer, 140, 16)}");
        Check("post: the encoded name", Convert.ToHexString(LsEncode("Ptlsone"), 0, 6), post == null ? "none" : Convert.ToHexString(post.Buffer, 160, 6));
        Check("post: c gets it marked LS2", 0x40, (Last(sc, 0x0CC)?.Buffer[5] ?? 0) & 0x40);
        Check("post: a too", 1, Of(sa, 0x0CC).Count);
        Clear();
        GetMsg(sb, 0);
        Check("GET_LSMSG: b loads it (op Load)", "2/Welcome", Last(sb, 0x0CC) is BasicPacket ld ? $"{BitConverter.ToUInt16(ld.Buffer, 156)}/{Text(ld.Buffer, 8, 128)}" : "none");
        GetMsg(sc, 1);
        Check("GET_LSMSG: c loads LS2's", 0x40, (Last(sc, 0x0CC)?.Buffer[5] ?? 0) & 0xC0);
        GetMsg(sb, 1);
        Check("GET_LSMSG: b has no LS2", true, log.Exists(l => l.Contains("does not have Linkshell 2")));
        Clear();
        SetMsg(sa, 0x20, 2);
        Check("level: LevelChanged (attr 3, write 2, op 3)", "48/10/3", Last(sa, 0x0CC) is BasicPacket lc ? $"{lc.Buffer[4]}/{lc.Buffer[5]}/{BitConverter.ToUInt16(lc.Buffer, 156)}" : "none");
        Check("level: stored", "2", Q1("SELECT postrights FROM linkshells WHERE linkshellid = ?", BoundValue.Of(lsid)));
        Clear();
        SetMsg(sb, 0x20, 0);
        Check("level: a pearl is Denied", 3, Last(sb, 0x0CC)?.Buffer[4] ?? -1);
        SetMsg(sb, 0x40, 0, "Pearl post");
        Check("post: a pearl under pearl rights", "Ptbb/Pearl post", Q1("SELECT poster, message FROM linkshells WHERE linkshellid = ?", BoundValue.Of(lsid)));
        Clear();
        SetMsg(sd, 0x40, 3, "x");
        Check("SET_LSMSG: writeLevel 3 refused", true, log.Exists(l => l.Contains("writeLevel")));
        Clear();
        Send(sa, 0x0E4, 144);
        Check("GET_LSPRIV: logged only", true, log.Exists(l => l.Contains("GET_LSPRIV: Not implemented")));

        // ---- rank change (0x077 linkshell kinds) ----
        Clear();
        Change(sa, "Ptbb", 1, 2);
        Pump();
        Check("rank: LinkshellRankChange", true, sent.Exists(m => m is LinkshellRankChange rc && rc.NewRank == 2 && rc.RequesterRank == 1 && rc.LinkshellId == lsid));
        Check("rank: b's pearl is a sack", "514/2", $"{pb.ItemId}/{pb.Extra[8]}");
        Check("rank: the row", "514", Q1("SELECT itemId FROM char_inventory WHERE charid = ? AND slot = 20", BoundValue.Of(b.Id)));
        Check("rank: accounts_sessions", $"{lsid}/2/0/0", Sess(b));
        Check("rank: b still has it on", "16:20", Eq(b));
        Check("rank: b told (ITEM_LIST, GROUP_COMLINK, ITEM_ATTR 514)", "514/1/514",
              $"{(Last(sb, 0x01F) is BasicPacket rl ? BitConverter.ToUInt16(rl.Buffer, 8) : 0)}/{Last(sb, 0x0E0)?.Buffer[4]}/{(Last(sb, 0x020) is BasicPacket ra ? BitConverter.ToUInt16(ra.Buffer, 12) : 0)}");
        Clear();
        Change(sb, "Ptdd", 1, 2);
        Check("rank: only the shell may", true, log.Exists(l => l.Contains("Invalid linkshell rank")));
        Clear();
        Change(sa, "Ptbb", 1, 4);
        Check("rank: SetQuartermaster on a linkshell kind refused", true, log.Exists(l => l.Contains("Invalid operation")));

        // ---- kick (0x071 linkshell kinds) ----
        Clear();
        Strike(sb, "Ptcc", 1);
        Pump();
        Check("kick: a sack cannot kick a sack", $"0/0/{lsid}/2", Sess(c3));
        Clear();
        Strike(sb, "Ptdd", 1);
        Pump();
        Check("kick: LinkshellRemove", true, sent.Exists(m => m is LinkshellRemove lr && lr.VictimName == "Ptdd" && lr.RequesterRank == 2));
        Check("kick: d's pearl broken", 4, pd.Extra[8]);
        Check("kick: d's row broken", 4, Sql(db, "SELECT extra FROM char_inventory WHERE charid = ? AND slot = 20", BoundValue.Of(d.Id)) is var xr && xr != null && xr.Next() ? xr.GetBlobBytes("extra")[8] : -1);
        Check("kick: d unequipped", "-/0/0/0/0", $"{(Eq(d) == "null" ? "-" : Eq(d))}/{Sess(d)}");
        Check("kick: d told LinkshellKicked (109)", true, Told(sd, 109));
        Check("kick: d's GROUP_COMLINK empty", "1/0", Last(sd, 0x0E0) is BasicPacket kc ? $"{kc.Buffer[4]}/{kc.Buffer[5]}" : "none");
        Check("kick: d's CHAR_PC drops the flag", 0u, BitConverter.ToUInt32(LoginPackets.CharPc(d, zi, EntityUpdate.Update, 0x1F).Buffer, 32) & 0x20000);
        Clear();
        Active(sd, 20, 1, 1);
        Check("a broken pearl: LinkshellNoLongerExists (110)", true, Told(sd, 110));
        Check("a broken pearl stays off", "null", Eq(d));

        // ---- unequip, then throwing the shell away breaks it ----
        Clear();
        Active(sa, 20, 0, 1);
        Check("unequip: accounts_sessions cleared", "0/0/0/0", Sess(a));
        Check("unequip: char_equip", "null", Eq(a));
        Check("unequip: ITEM_ATTR lock 0", 0, Last(sa, 0x020)?.Buffer[16] ?? -1);
        Clear();
        Active(sa, 20, 0, 2);
        Check("unequip from the wrong slot: nothing", 0, Of(sa, 0x01D).Count);
        Clear();
        Send(sa, 0x028, 12, buf => { buf[4] = 1; buf[8] = 0; buf[9] = 20; });
        Check("break: linkshells.broken", "1", Q1("SELECT broken FROM linkshells WHERE linkshellid = ?", BoundValue.Of(lsid)));
        Check("break: b's sack broken, unequipped", "4/null/0/0/0/0", $"{pb.Extra[8]}/{Eq(b)}/{Sess(b)}");
        Check("break: c's LS2 sack broken, unequipped", "4/null/0/0/0/0", $"{pc.Extra[8]}/{Eq(c3)}/{Sess(c3)}");
        Check("break: both told LinkshellNoLongerExists", true, Told(sb, 110) && Told(sc, 110));
        Check("break: a's pearl, not on, stays a pearl", 3, made?.Extra[8] ?? -1);
        Clear();
        Active(sa, made.Slot, 1, 1);
        Check("break: the pearl of a broken shell is broken on equip", "4/True", $"{made.Extra[8]}/{Told(sa, 110)}");

        // ---- a char leaving the zone closes its poll ----
        Clear();
        nowMs += 60_000;
        Propose(sc, 5, "Bye? y");
        Vote(sa, 1, "Ptcc");
        Clear();
        zone.OnCharTimeout(sc, false);
        Check("c timed out: its poll closed, the results to the voters", "512/1,0,0,0,0,0,0,0/5/2/2/Ptcc/[Bye?]|1[1]:y/64", Proc(sa));
        Vote(sb, 1, "Ptcc");
        Check("gone", "143:string2 Ptcc", Msg9(sb));

        // ---- login: char_equip linkshell rows, a broken one dropped ----
        Sql(db, "INSERT INTO linkshells (name, color, postrights) VALUES ('Ptlstwo', 61440, 0)");
        uint lsid2 = uint.Parse(Q1("SELECT linkshellid FROM linkshells WHERE name = 'Ptlstwo'"));
        var two = new Item { Location = Container.Inventory, Slot = 25, ItemId = 515, Quantity = 1, Signature = "Ptlstwo" };
        BitConverter.GetBytes(lsid2).CopyTo(two.Extra, 0);
        two.Extra[8] = 0; // untyped: the id gives the type
        GiveRow(b, two);
        Sql(db, "INSERT INTO char_equip (charid, equipslotid, slotid, containerid) VALUES (?, 16, 25, 0), (?, 17, 20, 0)", BoundValue.Of(b.Id), BoundValue.Of(b.Id));
        Sql(db, "INSERT IGNORE INTO char_stats (charid) VALUES (?)", BoundValue.Of(b.Id));
        var lzone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
        var ls_ = new MapSession { CharId = b.Id };
        lzone.OnLogin(ls_);
        var lb = ls_.Char as CharData;
        Check("login: b loads", true, lb != null);
        if (lb != null)
        {
            var li = lb.Items.Find(x => x.Slot == 25 && x.Location == Container.Inventory);
            Check("login: the pearl typed from its id, signature packed", $"3/{Convert.ToHexString(LsEncode("Ptlstwo"), 0, 15)}",
                  li == null ? "none" : $"{li.Extra[8]}/{Convert.ToHexString(li.Extra, 9, 15)}");
            Check("login: slot 16 bound, the broken 17 dropped", "16:25", string.Join(",", lb.Equip.FindAll(e => e.EquipSlot >= 16).ConvertAll(e => $"{e.EquipSlot}:{e.InvSlot}")));
            Check("login: the broken row deleted", "16:25", Eq(b));
            Check("login: joined LS1", $"{lsid2}/3/0/0", Sess(b));
            var inv = new List<BasicPacket>();
            GameOkPackets.AddInventory(inv, lb);
            int gi = inv.FindIndex(x => Opcode(x) == 0x0E0);
            Check("login: the inventory sends ITEM_ATTR 0x13, ITEM_LIST 0x13, GROUP_COMLINK", "19/19/1/25",
                  gi < 2 ? "none" : $"{inv[gi - 2].Buffer[16]}/{inv[gi - 1].Buffer[12]}/{inv[gi].Buffer[4]}/{inv[gi].Buffer[5]}");
            var lp = LoginPackets.OnLogin(lb, zi, 0);
            Check("login: no EQUIP_LIST for slot 16", 0, lp.FindAll(x => Opcode(x) == 0x050 && x.Buffer[5] >= 16).Count);
        }
    }

    // EncodeStringLinkshell, independently: 6-bit codes most significant first, a run of set bits, cut at a zero byte
    private static byte[] LsEncode(string s)
    {
        var bits = new byte[20];
        int n = Math.Min(20, s.Length), pos = 0;
        void Put(int v, int len)
        {
            for (int i = len - 1; i >= 0; i--, pos++)
                if (pos / 8 < 20 && (v >> i & 1) != 0) bits[pos / 8] |= (byte)(0x80 >> pos % 8);
        }
        foreach (char ch in s.Substring(0, n))
            Put(char.IsDigit(ch) ? ch - '0' + 53 : char.IsUpper(ch) ? ch - 'A' + 27 : char.IsLower(ch) ? ch - 'a' + 1 : 0, 6);
        int left = 8 - n * 6 % 8;
        Put(0x3F, left == 8 || left == 2 ? 6 : left);
        int z = Array.IndexOf(bits, (byte)0);
        if (z >= 0) Array.Clear(bits, z, 20 - z);
        return bits;
    }

    private static string SortedIds(string csv)
    {
        var l = new List<string>(new HashSet<string>(csv.Split(',')));
        l.Sort(StringComparer.Ordinal);
        return string.Join(",", l);
    }
}
