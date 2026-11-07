using System;
using System.Collections.Generic;
using System.IO;
using XI.Ipc;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Assist.Live -- live gate for the Assist Channel (ZoneHost.Assist: CAMANContainer's eligibility, the
// AfterZoneIn notice, the assist chat both ways, c2s 0x0B7 ASSIST_CHANNEL and the AssistChannelEvent its receiver acts
// on, CharVarUpdate) against the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint IdA = 990911, IdB = 990912, IdC = 990913, IdZ = 990919;
    private static readonly uint[] Ids = { IdA, IdB, IdC, IdZ };
    private const ushort Waters = 238;
    private const int EminenceAssistByte = 64 + 31 * 4 + 1448 / 8; // eminencelog_t complete[], record 1448

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static ResultSet Sql(IDatabase db, string q, params BoundValue[] a) => db.Execute(q, a);

    private static string Q1(IDatabase db, string q, uint id)
    {
        var r = Sql(db, q, BoundValue.Of(id));
        return r != null && r.Next() ? r.Get<string>(0u) ?? "-" : "-";
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
        foreach (var id in Ids)
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
    private static string Text(byte[] b, int off, int len) => CborRaw.FromBytes(b.AsSpan(off, Math.Min(len, b.Length - off))).Split('\0')[0];

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
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        zone.UnixNow = () => now;
        long nowMs = 1_000_000;
        zone.NowMs = () => nowMs;
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);
        scripts.Lua.DoString("zones = zones or {}; zones[238] = zones[238] or {}; zones[238].text = zones[238].text or {}; " +
                             "zones[238].text.ASSIST_CHANNEL = 6380", "=verify_assist");

        var eminence = new byte[700];
        eminence[EminenceAssistByte] |= 1 << 1448 % 8;
        // Humes past their first login (the NEW_ADVENTURER title: no charCreate) that have seen the RoV opening
        // (Rhapsodies of Vana'diel, a zone-in cutscene in the starting cities: Resonance current, the other logs as a
        // new char has them), standing at the mog house exit (a char at 0,0,0 is moved there by the zone's onZoneIn)
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        var missions = new byte[15 * 70];
        for (int i = 0; i <= 3; i++) BitConverter.GetBytes((ushort)0xFFFF).CopyTo(missions, i * 70);
        BitConverter.GetBytes((ushort)101).CopyTo(missions, 6 * 70);
        BitConverter.GetBytes((ushort)1).CopyTo(missions, 13 * 70);
        missions[13 * 70 + 6] = 1;
        // A: a new char; B: a mentor; C: an old hand, back the day after; Z: offline
        void Insert(uint id, string name, uint playtime, uint settings, bool mentor, bool roe)
        {
            Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of("as" + name), BoundValue.Of("x"), BoundValue.Of((byte)1));
            Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,playtime,settings,mentor," +
                    "last_logout,eminence,titles,missions) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,NOW() - INTERVAL 1 DAY,?,?,?)",
                BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(name), BoundValue.Of(Waters), BoundValue.Of((ushort)0),
                BoundValue.Of(160.0f), BoundValue.Of(-2.65f), BoundValue.Of(-53.7f), BoundValue.Of((byte)2),
                BoundValue.Of((byte)0), BoundValue.Of(playtime), BoundValue.Of(settings), BoundValue.Of(mentor ? 1 : 0),
                BoundValue.Of(roe ? eminence : new byte[700]), BoundValue.Of(titles), BoundValue.Of(missions));
            Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(id));
            Sql(db, "INSERT INTO char_flags(charid) VALUES (?)", BoundValue.Of(id));
            Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(0u), BoundValue.Of((ushort)0));
            Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl, sjob, slvl) VALUES (?, 300, 50, 1, 75, 0, 1)", BoundValue.Of(id));
            Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 75)", BoundValue.Of(id));
        }
        Insert(IdA, "Assa", 0, 0, false, true);
        Insert(IdB, "Assb", 5_000_000, 1u << 25, true, true);
        Insert(IdC, "Assc", 5_000_000, 0, false, true);
        Insert(IdZ, "Assz", 5_000_000, 0, false, false);

        var sessions = new Dictionary<uint, MapSession>();
        CharData Ch(uint id) => (CharData)sessions[id].Char;
        void Send(uint id, ushort opcode, int size, Action<byte[]> fill = null)
        {
            var s = sessions[id];
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(s, opcode, new BasicPacket(buf));
            zone.AfterParse(s);
        }
        // the way out saves last_logout (removeCharFromZone), so a fixture that backdates it goes between the two
        void Logout(uint id)
        {
            if (sessions.Remove(id, out var old)) zone.OnCharTimeout(old, false);
        }
        void Login(uint id)
        {
            Logout(id);
            var s = sessions[id] = new MapSession { CharId = id, AccountId = id };
            zone.OnLogin(s);
            Send(id, 0x00A, 92, buf => BitConverter.GetBytes(id).CopyTo(buf, 12));
            s.PacketList.Clear();
        }
        List<BasicPacket> Of(uint id, int opcode) => new List<BasicPacket>(sessions[id].PacketList).FindAll(p => Opcode(p) == opcode);
        void Clear()
        {
            foreach (var s in sessions.Values) s.PacketList.Clear();
            log.Clear();
            sent.Clear();
        }
        string Var(uint id, string name) => Q1(db, $"SELECT CONCAT(value, '/', expiry) FROM char_vars WHERE charid = ? AND varname = '{name}'", id);
        string Muted(uint id) => Q1(db, "SELECT muted FROM char_flags WHERE charid = ?", id);
        // 0x009: MesNo, then the Data text
        string Msg(uint id) => Of(id, 0x009) is { Count: > 0 } m ? $"{BitConverter.ToUInt16(m[^1].Buffer, 10)}:{Text(m[^1].Buffer, 13, 40)}" : "none";
        void Chat(uint id, byte kind, string text) => Send(id, 0x0B5, 8 + (text.Length + 4 & ~3), buf =>
        {
            buf[4] = kind;
            CborRaw.Bytes(text).CopyTo(buf, 6);
        });
        void Assist(uint id, byte kind, string name) => Send(id, 0x0B7, 24, buf =>
        {
            buf[4] = kind;
            CborRaw.Bytes(name).CopyTo(buf, 6);
            buf[21] = (byte)' ';
        });
        string Std(MessageStandard m) => m == null ? "none" : $"{m.RecipientId - 990900}/{m.Message}/{m.String2}";
        MessageStandard LastStd() => sent.FindLast(m => m is MessageStandard) as MessageStandard;
        void Event(uint from, uint to, byte action) => zone.OnIpc(new AssistChannelEvent { SenderId = from, ReceiverId = to, Action = action });
        long jstMidnight = ((now + 9 * 3600) / 86400 + 1) * 86400 - 9 * 3600;

        Console.WriteLine("eligibility, and the AfterZoneIn notice 4 s after the login:");
        Login(IdA);
        Login(IdB);
        Login(IdC);
        long loginC = nowMs;
        Clear();
        nowMs += 3999;
        zone.Tick();
        Check("not before 4 s", 0, Of(IdA, 0x02A).Count);
        nowMs += 1;
        zone.Tick();
        var talk = Of(IdA, 0x02A);
        uint since2002 = (uint)(now - 1009810800);
        Check("a new char: [ASSIST]Eligible is the membership length in playtime seconds", "864000/0", Var(IdA, "[ASSIST]Eligible"));
        // until = now + expiry - GetPlayTime(): the steady clock has counted 4 s of play since the login
        Check("TALKNUMWORK ASSIST_CHANNEL: until (JST 2002 seconds), now, 1, 0; no name",
              $"1/6380/{since2002 + 864000 - 4}/{since2002}/1/0/{IdA}",
              talk.Count == 1 ? $"1/{BitConverter.ToUInt16(talk[0].Buffer, 26) & 0x7FFF}/{BitConverter.ToUInt32(talk[0].Buffer, 8)}/" +
                                $"{BitConverter.ToUInt32(talk[0].Buffer, 12)}/{BitConverter.ToUInt32(talk[0].Buffer, 16)}/" +
                                $"{BitConverter.ToUInt32(talk[0].Buffer, 20)}/{BitConverter.ToUInt32(talk[0].Buffer, 4)}" : $"{talk.Count}");
        Check("10 days left: no expiring warning", 0, Of(IdA, 0x009).Count);
        Check("the mentor is eligible without an expiry: no notice, no var", "0/-", $"{Of(IdB, 0x02A).Count}/{Var(IdB, "[ASSIST]Eligible")}");
        Check("the old hand is not eligible", "0/0/-", $"{Of(IdC, 0x02A).Count}/{Of(IdC, 0x009).Count}/{Var(IdC, "[ASSIST]Eligible")}");
        nowMs += 10_000;
        Clear();
        zone.Tick();
        Check("once per zone-in", 0, Of(IdA, 0x02A).Count);

        Console.WriteLine("assist chat out (0x0B5 AssistE / AssistJ):");
        Clear();
        Chat(IdA, 0x23, "how do I sub?");
        var out1 = sent.Find(m => m is ChatMessageAssist) as ChatMessageAssist;
        Check("ChatMessageAssist: sender, name, text, mentor rank 0, mastery 1, NaAssist",
              $"{IdA}/Assa/how do I sub?/0/1/{ChatMessageType.NaAssist}",
              out1 == null ? "none" : $"{out1.SenderId}/{out1.SenderName}/{out1.Message}/{out1.MentorRank}/{out1.MasteryRank}/{out1.MessageTypeField}");
        Check("the 10 minute interaction window", $"1/{now + 600}", Var(IdA, "[ASSIST][Evaluations]Eligible"));
        Check("nothing pushed locally", 0, sessions[IdA].PacketList.Count);
        Clear();
        Chat(IdB, 0x22, "quest at the 3rd door");
        var out2 = sent.Find(m => m is ChatMessageAssist) as ChatMessageAssist;
        Check("a mentor: rank 1 (bronze), JpAssist", $"1/{ChatMessageType.JpAssist}", out2 == null ? "none" : $"{out2.MentorRank}/{out2.MessageTypeField}");
        Clear();
        Chat(IdC, 0x23, "hello?");
        Check("not eligible: silently dropped", "0/0/-", $"{sent.Count}/{sessions[IdC].PacketList.Count}/{Var(IdC, "[ASSIST][Evaluations]Eligible")}");
        cfg.AssistChannelEnabled = false;
        Clear();
        Chat(IdA, 0x23, "off?");
        Check("ASSIST_CHANNEL_ENABLED off: dropped", 0, sent.Count);
        cfg.AssistChannelEnabled = true;

        Console.WriteLine("assist chat in (ChatMessageAssist from the world):");
        Clear();
        zone.OnIpc(new ChatMessageAssist
        {
            SenderId = IdB, SenderName = "Assb", Message = "quest at the 3rd door", MentorRank = 2, MasteryRank = 1, GmLevel = 3,
            MessageTypeField = ChatMessageType.JpAssist,
        });
        var inA = Of(IdA, 0x017);
        Check("to the eligible: kind 34, GM attr, Data mastery 1 | mentor 2 << 8, name, text, roundUp4(23 + 21)",
              "1/34/1/513/Assb/quest at the 3rd door/44",
              inA.Count == 1 ? $"1/{inA[0].Buffer[4]}/{inA[0].Buffer[5]}/{BitConverter.ToUInt16(inA[0].Buffer, 6)}/{Text(inA[0].Buffer, 8, 15)}/" +
                               $"{Text(inA[0].Buffer, 23, 150)}/{inA[0].GetSize()}" : $"{inA.Count}");
        Check("not to the sender, nor the ineligible", "0/0", $"{Of(IdB, 0x017).Count}/{Of(IdC, 0x017).Count}");
        Clear();
        zone.OnIpc(new ChatMessageAssist { SenderId = IdZ, SenderName = "Assz", Message = "hi", MentorRank = 0, MasteryRank = 1 });
        Check("no mentor rank: Data 0", "0/1", Of(IdA, 0x017) is { Count: 1 } l ? $"{BitConverter.ToUInt16(l[0].Buffer, 6)}/{Of(IdB, 0x017).Count}" : "none");

        Console.WriteLine("0x0B7 ASSIST_CHANNEL:");
        Clear();
        Assist(IdA, 0x23, "Assb");
        Check("Kind 0x23 is refused", "True/0", $"{log.Exists(l => l.Contains("Kind"))}/{sent.Count}");
        cfg.AssistChannelEnabled = false;
        Clear();
        Assist(IdA, 0x24, "Assb");
        Check("ASSIST_CHANNEL_ENABLED off is refused", "True/0", $"{log.Exists(l => l.Contains("Assist Channel is not enabled"))}/{sent.Count}");
        cfg.AssistChannelEnabled = true;
        Clear();
        Assist(IdA, 0x24, "Nobody");
        Check("no such char: AnErrorHasOccured (308), 0x10 long", "308:/16/0", $"{Msg(IdA)}/{Of(IdA, 0x009)[0].GetSize()}/{sent.Count}");
        Clear();
        Assist(IdA, 0x24, "Assb");
        var ev = sent.Find(m => m is AssistChannelEvent) as AssistChannelEvent;
        Check("thumbs up: AssistChannelEvent to the receiver", $"{IdA}/{IdB}/36", ev == null ? "none" : $"{ev.SenderId}/{ev.ReceiverId}/{ev.Action}");
        Clear();
        Assist(IdA, 0x25, "Assb");
        Check("a warning from a non-mentor: AnErrorHasOccured", "308:/0", $"{Msg(IdA)}/{sent.Count}");
        Clear();
        Assist(IdB, 0x25, "Assa");
        ev = sent.Find(m => m is AssistChannelEvent) as AssistChannelEvent;
        Check("a mentor's warning goes out", $"{IdB}/{IdA}/37", ev == null ? "none" : $"{ev.SenderId}/{ev.ReceiverId}/{ev.Action}");
        Clear();
        Assist(IdB, 0x26, "Assa");
        Assist(IdB, 0x27, "Assa");
        Check("mute / unmute: mastery rank 1 is under 6", "2/0", $"{Of(IdB, 0x009).FindAll(p => BitConverter.ToUInt16(p.Buffer, 10) == 308).Count}/{sent.Count}");

        Console.WriteLine("evaluations (AssistChannelEvent at the receiver):");
        Clear();
        Event(IdA, IdC, 0x24);
        Check("the receiver is no mentor: AnErrorHasOccured to the giver", "11/308/", Std(LastStd()));
        Clear();
        Event(IdA, IdB, 0x24);
        Check("the mentor said something: ReceivedThumbsUp (314)", "314:", Msg(IdB));
        Check("Today 1 to JST midnight, Count 1", $"1/{jstMidnight}|1/0", $"{Var(IdB, "[ASSIST][Evaluations]Today")}|{Var(IdB, "[ASSIST][Evaluations]Count")}");
        Check("GivenThumbsUp (306) to the giver, with the name", "11/306/Assb", Std(LastStd()));
        Check("the giver on this map: its own cooldown to JST midnight", $"1/{jstMidnight}/0", $"{Var(IdA, "[ASSIST][ThumbsUp]Cooldown")}/{sent.FindAll(m => m is CharVarUpdate).Count}");
        Clear();
        Assist(IdA, 0x24, "Assb");
        Check("thumbs up on cooldown: ThumbsUpCooldown (309)", "309:/0", $"{Msg(IdA)}/{sent.Count}");
        Clear();
        Event(IdZ, IdB, 0x25);
        var cvu = sent.Find(m => m is CharVarUpdate) as CharVarUpdate;
        Check("a warning from an offline giver: ReceivedWarning (315), Count back to 0", "315:/0/2/",
              $"{Msg(IdB)}/{Var(IdB, "[ASSIST][Evaluations]Count").Split('/')[0]}/{Var(IdB, "[ASSIST][Evaluations]Today").Split('/')[0]}/");
        Check("the giver's cooldown in the db and through the world", $"1/{jstMidnight}|{IdZ}/[ASSIST][Warnings]Cooldown/1/{jstMidnight}",
              $"{Var(IdZ, "[ASSIST][Warnings]Cooldown")}|{(cvu == null ? "none" : $"{cvu.CharId}/{cvu.VarName}/{cvu.Value}/{cvu.Expiry}")}");
        Check("GivenWarning (307)", "19/307/Assb", Std(LastStd()));
        Sql(db, "UPDATE char_vars SET value = 3 WHERE charid = ? AND varname = '[ASSIST][Evaluations]Today'", BoundValue.Of(IdB));
        Ch(IdB).CharVars.Remove("[ASSIST][Evaluations]Today");
        Clear();
        Event(IdC, IdB, 0x24);
        Check("3 a day: no more counted, the giver still told", "none/0/11/306/Assb",
              $"{Msg(IdB)}/{Var(IdB, "[ASSIST][Evaluations]Count").Split('/')[0]}/{Std(LastStd()).Replace("13/", "11/")}");
        Clear();
        Event(IdA, IdC, 0x25);
        Check("no window for the receiver: AnErrorHasOccured", "11/308/", Std(LastStd()));
        Sql(db, "INSERT INTO char_vars(charid, varname, value, expiry) VALUES (?, '[ASSIST][Warnings]Cooldown', 1, ?)", BoundValue.Of(IdB), BoundValue.Of((uint)jstMidnight));
        Ch(IdB).CharVars.Remove("[ASSIST][Warnings]Cooldown");
        Clear();
        Assist(IdB, 0x25, "Assa");
        Check("a mentor's warning on cooldown: WarningCooldown (310)", "310:/0", $"{Msg(IdB)}/{sent.Count}");
        Clear();
        zone.OnIpc(new CharVarUpdate(IdA, 5, 0, "[ASSIST]Probe"));
        Check("CharVarUpdate: the cache of a char on this map", "5/0", Ch(IdA).CharVars.TryGetValue("[ASSIST]Probe", out var pv) ? $"{pv.Value}/{pv.Expiry}" : "none");

        Console.WriteLine("mutes (AssistChannelEvent):");
        Clear();
        Event(IdB, IdC, 0x26);
        Check("no window: AnErrorHasOccured", "12/308/False", $"{Std(LastStd())}{Muted(IdC)}");
        Clear();
        Event(IdB, IdA, 0x26);
        Check("muted for 24 h, logged", $"True/1/{now + 86400}/True",
              $"{Muted(IdA)}/{Var(IdA, "[ASSIST]Muted")}/{log.Exists(l => l == $"Assa was muted by player ID {IdB}")}");
        Clear();
        Event(IdB, IdA, 0x26);
        Check("again: AlreadyMuted (312)", "12/312/", Std(LastStd()));
        Clear();
        Chat(IdA, 0x23, "hello?");
        Check("a muted char's assist chat is dropped", 0, sent.Count);
        Clear();
        Assist(IdA, 0x24, "Assc");
        Check("a muted char can't give a thumbs up", "308:", Msg(IdA));
        Clear();
        Event(IdB, IdA, 0x27);
        Check("unmuted, logged", $"False/-/True", $"{Muted(IdA)}/{Var(IdA, "[ASSIST]Muted")}/{log.Exists(l => l == $"Assa was unmuted by player ID {IdB}")}");
        Clear();
        Event(IdB, IdA, 0x27);
        Check("not muted: AnErrorHasOccured", "12/308/", Std(LastStd()));

        Console.WriteLine("the mute runs out (char_flags still set, [ASSIST]Muted gone):");
        Sql(db, "UPDATE char_flags SET muted = 1 WHERE charid = ?", BoundValue.Of(IdA));
        Login(IdA);
        Clear();
        Chat(IdA, 0x23, "back");
        Check("the next zone-in clears it, and chat goes out", "False/1", $"{Muted(IdA)}/{sent.FindAll(m => m is ChatMessageAssist).Count}");

        Console.WriteLine("a returnee, an expiring and an expired membership:");
        // the logout saves the play time the steady clock counted since the login
        uint playedC = (uint)((nowMs - loginC) / 1000);
        Logout(IdC);
        Sql(db, "UPDATE chars SET last_logout = NOW() - INTERVAL 60 DAY WHERE charid = ?", BoundValue.Of(IdC));
        Login(IdC);
        Clear();
        nowMs += 4000;
        zone.Tick();
        Sql(db, "UPDATE chars SET last_logout = NOW() - INTERVAL 1 DAY WHERE charid = ?", BoundValue.Of(IdC));
        // the first aman() call is the zone-in notice 4 s after the login: that play time counts too
        Check("48+ days away: eligible for playtime + 240 h", $"{5_000_000 + playedC + 4 + 864000}/0|1",
              $"{Var(IdC, "[ASSIST]Eligible")}|{Of(IdC, 0x02A).Count}");
        Sql(db, "UPDATE char_vars SET value = ? WHERE charid = ? AND varname = '[ASSIST]Eligible'", BoundValue.Of(5_000_000 + 5 * 3600 + 1800), BoundValue.Of(IdC));
        Login(IdC);
        Clear();
        nowMs += 4000;
        zone.Tick();
        Check("5.5 h left: the notice, then AssistChannelExpiring (317) Para0 5", "1/317:Para0 5 /28",
              $"{Of(IdC, 0x02A).Count}/{Msg(IdC)}/{Of(IdC, 0x009)[^1].GetSize()}");
        Sql(db, "UPDATE char_vars SET value = 4000000 WHERE charid = ? AND varname = '[ASSIST]Eligible'", BoundValue.Of(IdC));
        Login(IdC);
        Clear();
        nowMs += 4000;
        zone.Tick();
        Check("run out: AssistChannelExpired (318), no notice, the var dropped", "318:/0/-",
              $"{Msg(IdC)}/{Of(IdC, 0x02A).Count}/{Var(IdC, "[ASSIST]Eligible")}");
        Clear();
        Chat(IdC, 0x23, "still here?");
        Check("and the chat is dropped", 0, sent.Count);

        Console.WriteLine("lua player:getMentor / setMentor:");
        scripts.Lua.SetGlobal("assistPlayer", l => { l.PushObject(Ch(IdC)); return 1; });
        var got = new List<string>();
        scripts.Lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        scripts.Lua.DoString("local p = assistPlayer(); report(tostring(p:getMentor())); p:setMentor(true); report(tostring(p:getMentor()))", "=verify_assist");
        Check("false, then true and saved", "false,true/1/True",
              $"{string.Join(",", got)}/{Q1(db, "SELECT mentor FROM chars WHERE charid = ?", IdC)}/{(Ch(IdC).UpdateMask & CharStats.UpdateHp) != 0}");
    }
}
