using System;
using System.Collections.Generic;
using System.Linq;
using XI.Ipc;
using XI.Login;
using XI.Persistence;
using XI.World;

// XI.Verify.World.Live -- live gate for the ported world IPC router (ipc_server, character_cache, zone_settings)
// against the real MariaDB (docker xi-live-db, host:3307, xidb). The ZMQ router is an in-memory channel.
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint AccBase = 990400;
    private const uint CA = 990401, CB = 990402, CC = 990403, CD = 990404, CE = 990405, CGone = 990499;

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static void CheckTrue(string name, bool cond) => Check(name, true, cond);

    private static void Sql(IDatabase db, string q, params object[] a) => LoginDb.PreparedStmt(db, q, a);

    private static Ipp P(string ip, ushort port) => new Ipp(IpText.Str2Ip(ip), port);

    private static readonly Ipp S1 = P("10.0.0.1", 1000), S2 = P("10.0.0.2", 2000), S3 = P("10.0.0.3", 3000);
    private static readonly Ipp Sender = P("10.9.9.9", 9999);

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
        for (uint i = 1; i <= 5; i++)
        {
            foreach (var t in CharidTables(db)) Sql(db, $"DELETE FROM `{t}` WHERE charid = ?", AccBase + i);
            Sql(db, "DELETE FROM chars WHERE charid = ?", AccBase + i);
            Sql(db, "DELETE FROM accounts WHERE id = ?", AccBase + i);
        }
        Sql(db, "DELETE FROM help_desk WHERE charid IN (?, ?)", CA, CGone);
    }

    // (char, name, server, linkshell1, linkshell2, unity, party, alliance, prevzone, zone)
    private static void Seed(IDatabase db)
    {
        var rows = new (uint c, string n, Ipp s, uint ls1, uint ls2, uint unity, uint party, uint alliance, ushort prev, ushort zone)[]
        {
            (CA, "Wlaa", S1, 77, 0, 5, 0, 0, 230, 232),
            (CB, "Wlbb", S1, 0, 77, 0, 500, 0, 231, 231),
            (CC, "Wlcc", S2, 0, 0, 0, 500, 0, 230, 230),
            (CD, "Wldd", S3, 0, 0, 0, 501, 9, 230, 230),
            (CE, "Wlee", S2, 0, 0, 0, 502, 9, 230, 230),
        };
        foreach (var r in rows)
        {
            Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)", r.c, "wl" + r.n, "x", (byte)1);
            Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,nation,gmlevel) VALUES (?,?,?,?,?,?,?)",
                r.c, r.c, r.n, r.zone, r.prev, (byte)1, (byte)0);
            Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port,linkshellid1,linkshellid2,unitychat) VALUES (?,?,?,?,?,?,?)",
                r.c, r.c, r.s.Ip, r.s.Port, r.ls1, r.ls2, r.unity);
            if (r.party != 0)
                Sql(db, "INSERT INTO accounts_parties(charid,partyid,allianceid) VALUES (?,?,?)", r.c, r.party, r.alliance);
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

    private sealed class RecSystem : IWorldSystem
    {
        public readonly List<(byte type, Ipp ipp, byte[] payload)> Calls = new();
        public void HandleMessage(byte type, IppPayload p) => Calls.Add((type, p.Ipp, p.Payload));
    }

    private static string Set(IEnumerable<Ipp> ipps) => string.Join(",", ipps.Select(i => i.ToString()).OrderBy(s => s));

    private static ZoneSettings TestZones()
    {
        var z = new ZoneSettings();
        void Add(ushort id, Ipp ipp, uint misc) => z.ZoneSettingsMap[id] = new ZoneSettingsEntry { ZoneId = id, Ipp = ipp, Misc = misc };
        Add(230, S1, ZoneMisc.Yell);
        Add(231, S1, 0);
        Add(232, S2, ZoneMisc.Yell | ZoneMisc.Assist);
        Add(233, S3, ZoneMisc.Assist);
        z.MapEndpoints.AddRange(new[] { S1, S2, S3 });
        z.YellMapEndpoints.AddRange(new[] { S1, S2 });
        z.AssistMapEndpoints.AddRange(new[] { S2, S3 });
        return z;
    }

    private static void Run(IDatabase db)
    {
        // ---- Ipp -------------------------------------------------------------------------------------------
        Check("ipp ToString", "10.0.0.1:1000", S1.ToString());
        Check("ipp raw", (ulong)IpText.Str2Ip("10.0.0.1") | (1000UL << 32), S1.Raw);
        CheckTrue("ipp raw round trip", Ipp.FromRaw(S2.Raw).Equals(S2));
        CheckTrue("ipp order by ip then port", S1.CompareTo(S2) < 0 && P("10.0.0.1", 5).CompareTo(S1) < 0);

        // ---- CharacterCache --------------------------------------------------------------------------------
        var cache = new CharacterCache();
        CheckTrue("cache miss", cache.GetCharacterIpp(1) == null);
        cache.UpdateCharacter(1, S1);
        Check("cache hit", S1.ToString(), cache.GetCharacterIpp(1)?.ToString());
        cache.UpdateCharacter(1, S2);
        Check("cache overwrite", S2.ToString(), cache.GetCharacterIpp(1)?.ToString());
        cache.RemoveCharacter(1);
        CheckTrue("cache remove", cache.GetCharacterIpp(1) == null);

        // ---- ZoneSettings (DB load) ------------------------------------------------------------------------
        var all = new ZoneSettings(db);
        var count = LoginDb.PreparedStmt(db, "SELECT COUNT(*) AS n FROM zone_settings");
        count.Next();
        Check("zone_settings entries == table rows", count.Get<long>("n"), all.ZoneSettingsMap.Count);
        Check("zone_settings endpoints de-duplicated (299 zones on :54230, zone 0 on :0)", 2, all.MapEndpoints.Count);
        Check("zone_settings endpoints sorted by port", "127.0.0.1:0,127.0.0.1:54230", string.Join(",", all.MapEndpoints));
        Check("zone_settings no misc -> no yell", 0, all.YellMapEndpoints.Count);
        Check("zone_settings no misc -> no assist", 0, all.AssistMapEndpoints.Count);
        var withMisc = new ZoneSettings(db, z => z == 230 ? ZoneMisc.Yell : z == 231 ? ZoneMisc.Assist : (uint?)null);
        Check("zone_settings yell endpoints", 1, withMisc.YellMapEndpoints.Count);
        Check("zone_settings assist endpoints", 1, withMisc.AssistMapEndpoints.Count);

        // ---- harness ---------------------------------------------------------------------------------------
        var chan = new QueueWorldChannel();
        var sys = new WorldSystems { Conquest = new RecSystem(), Besieged = new RecSystem(), Campaign = new RecSystem(), Colonization = new RecSystem() };
        var logs = new List<string>();
        var ipc = new IpcServer(db, chan, TestZones(), sys, logs.Add);

        void Inject(IIpcMessage m) => chan.Inject(Sender, IpcFraming.ToBytesWithHeader(m));
        List<IppMessage> Pump()
        {
            chan.Sent.Clear();
            ipc.HandleIncomingMessages();
            return chan.Sent.ToList();
        }
        string Dests(List<IppMessage> sent) => Set(sent.Select(s => s.Ipp));

        // ---- IPP lookup ------------------------------------------------------------------------------------
        Check("ipp for charid", S2.ToString(), ipc.GetIppForCharId(CC)?.ToString());
        CheckTrue("ipp for unknown charid", ipc.GetIppForCharId(CGone) == null);
        Check("ipp for charname", S3.ToString(), ipc.GetIppForCharName("Wldd")?.ToString());
        CheckTrue("ipp for unknown charname", ipc.GetIppForCharName("Nobodyhere") == null);
        Check("ipp for zone", S2.ToString(), ipc.GetIppForZoneId(232)?.ToString());
        CheckTrue("ipp for unknown zone", ipc.GetIppForZoneId(9999) == null);
        Check("ipps for party w/o alliance", Set(new[] { S1, S2 }), Set(ipc.GetIppsForParty(500)));
        Check("ipps for party in alliance (whole alliance)", Set(new[] { S2, S3 }), Set(ipc.GetIppsForParty(501)));
        Check("ipps for unknown party", "", Set(ipc.GetIppsForParty(4242)));
        Check("ipps for alliance", Set(new[] { S2, S3 }), Set(ipc.GetIppsForAlliance(9)));
        Check("ipps for linkshell (ls1 or ls2, one row per process)", Set(new[] { S1 }), Set(ipc.GetIppsForLinkshell(77)));
        Check("ipps for unity", Set(new[] { S1 }), Set(ipc.GetIppsForUnity(5)));
        Check("ipps yell", Set(new[] { S1, S2 }), Set(ipc.GetIppsForYellZones()));
        Check("ipps assist", Set(new[] { S2, S3 }), Set(ipc.GetIppsForAssistZones()));
        Check("ipps all", Set(new[] { S1, S2, S3 }), Set(ipc.GetIppsForAllZones()));

        // ---- AccountLogin / CharZone -----------------------------------------------------------------------
        Inject(new AccountLogin(123));
        var r = Pump();
        Check("AccountLogin -> every zone endpoint", Set(new[] { S1, S2, S3 }), Dests(r));
        Check("AccountLogin payload", 123u, ((AccountLogin)IpcFraming.Decode(r[0].Payload)).AccountId);

        Inject(new CharZone(CA, (ZoneId)232));
        r = Pump();
        Check("CharZone -> destination zone", Set(new[] { S2 }), Dests(r));
        Check("CharZone cache updated", S2.ToString(), ipc.CharacterCache.GetCharacterIpp(CA)?.ToString());
        Inject(new CharZone(CA, (ZoneId)9999));
        r = Pump();
        Check("CharZone unknown zone -> nothing sent", 0, r.Count);
        Check("CharZone unknown zone -> cache untouched", S2.ToString(), ipc.CharacterCache.GetCharacterIpp(CA)?.ToString());
        Inject(new CharZone(CA, (ZoneId)IpcServer.ZoneNoDestination));
        r = Pump();
        Check("CharZone no-destination -> nothing sent", 0, r.Count);
        CheckTrue("CharZone no-destination -> cache removed", ipc.CharacterCache.GetCharacterIpp(CA) == null);

        // ---- char / name / zone targeted -------------------------------------------------------------------
        Inject(new CharVarUpdate { CharId = CC });
        Check("CharVarUpdate -> char's process", Set(new[] { S2 }), Dests(Pump()));
        Inject(new CharVarUpdate { CharId = CGone });
        Check("CharVarUpdate offline char -> dropped", 0, Pump().Count);

        Inject(new ChatMessageCustom { RecipientId = CD });
        Check("ChatMessageCustom", Set(new[] { S3 }), Dests(Pump()));
        Inject(new PartyInvite { InviteeId = CC });
        Check("PartyInvite -> invitee", Set(new[] { S2 }), Dests(Pump()));
        Inject(new PartyInviteResponse { InviterId = CD });
        Check("PartyInviteResponse -> inviter", Set(new[] { S3 }), Dests(Pump()));
        Inject(new PartyRequest { RecipientId = CA });
        Check("PartyRequest -> recipient", Set(new[] { S1 }), Dests(Pump()));
        Inject(new PlayerKick { VictimId = CE });
        Check("PlayerKick -> victim", Set(new[] { S2 }), Dests(Pump()));
        Inject(new MessageStandard { RecipientId = CB, Message = 7 });
        Check("MessageStandard -> recipient", Set(new[] { S1 }), Dests(Pump()));
        Inject(new MessageSystem { RecipientId = CD, Message = 7 });
        Check("MessageSystem -> recipient", Set(new[] { S3 }), Dests(Pump()));
        Inject(new EntityInformationResponse { RequesterId = CC });
        Check("EntityInformationResponse -> requester", Set(new[] { S2 }), Dests(Pump()));
        Inject(new SendPlayerToLocation { TargetId = CA });
        Check("SendPlayerToLocation -> target", Set(new[] { S1 }), Dests(Pump()));
        Inject(new AssistChannelEvent { ReceiverId = CE });
        Check("AssistChannelEvent -> receiver", Set(new[] { S2 }), Dests(Pump()));

        Inject(new LinkshellRankChange { MemberName = "Wlcc" });
        Check("LinkshellRankChange -> member by name", Set(new[] { S2 }), Dests(Pump()));
        Inject(new LinkshellRemove { VictimName = "Wldd" });
        Check("LinkshellRemove -> victim by name", Set(new[] { S3 }), Dests(Pump()));
        Inject(new LinkshellRemove { VictimName = "Nobodyhere" });
        Check("LinkshellRemove offline name -> dropped", 0, Pump().Count);

        Inject(new LuaFunction { ExecutorZoneId = (ZoneId)233, FuncString = "x" });
        Check("LuaFunction -> executor zone", Set(new[] { S3 }), Dests(Pump()));
        Inject(new LuaFunction { ExecutorZoneId = (ZoneId)9999, FuncString = "x" });
        Check("LuaFunction unknown zone -> dropped", 0, Pump().Count);

        // ---- group routing ---------------------------------------------------------------------------------
        Inject(new ChatMessageParty { PartyId = 500 });
        Check("ChatMessageParty", Set(new[] { S1, S2 }), Dests(Pump()));
        Inject(new ChatMessageAlliance { AllianceId = 9 });
        Check("ChatMessageAlliance", Set(new[] { S2, S3 }), Dests(Pump()));
        Inject(new ChatMessageLinkshell { LinkshellId = 77 });
        Check("ChatMessageLinkshell", Set(new[] { S1 }), Dests(Pump()));
        Inject(new ChatMessageUnity { UnityLeaderId = 5 });
        Check("ChatMessageUnity", Set(new[] { S1 }), Dests(Pump()));
        Inject(new ChatMessageYell());
        Check("ChatMessageYell -> yell zones", Set(new[] { S1, S2 }), Dests(Pump()));
        Inject(new ChatMessageAssist());
        Check("ChatMessageAssist -> assist zones", Set(new[] { S2, S3 }), Dests(Pump()));
        Inject(new ChatMessageServerMessage());
        Check("ChatMessageServerMessage -> all zones", Set(new[] { S1, S2, S3 }), Dests(Pump()));
        Inject(new PartyReload { PartyId = 501 });
        Check("PartyReload", Set(new[] { S2, S3 }), Dests(Pump()));
        Inject(new PartyDisband { PartyId = 500 });
        Check("PartyDisband", Set(new[] { S1, S2 }), Dests(Pump()));
        Inject(new AllianceReload { AllianceId = 9 });
        Check("AllianceReload", Set(new[] { S2, S3 }), Dests(Pump()));
        Inject(new AllianceDissolve { AllianceId = 9 });
        Check("AllianceDissolve", Set(new[] { S2, S3 }), Dests(Pump()));
        Inject(new LinkshellSetMessage { LinkshellId = 77 });
        Check("LinkshellSetMessage", Set(new[] { S1 }), Dests(Pump()));
        Inject(new ChatMessageParty { PartyId = 4242 });
        Check("empty group -> nothing", 0, Pump().Count);

        // ---- tell ------------------------------------------------------------------------------------------
        Inject(new ChatMessageTell { SenderId = CA, RecipientName = "Wldd", Message = "hi" });
        r = Pump();
        Check("Tell online -> recipient's process", Set(new[] { S3 }), Dests(r));
        Check("Tell online payload", "hi", ((ChatMessageTell)IpcFraming.Decode(r[0].Payload)).Message);
        Inject(new ChatMessageTell { SenderId = CA, RecipientName = "Nobodyhere", Message = "hi" });
        r = Pump();
        Check("Tell offline -> back to sender process", Set(new[] { Sender }), Dests(r));
        var back = (MessageStandard)IpcFraming.Decode(r[0].Payload);
        Check("Tell offline recipient", CA, back.RecipientId);
        Check("Tell offline MsgStd::TellNotReceivedOffline", (ushort)125, back.Message);

        // ---- KillSession -----------------------------------------------------------------------------------
        Inject(new KillSession { VictimId = CA }); // prev 230 -> zone 232: just the previous zone's process
        Check("KillSession prev != cur -> previous zone only", Set(new[] { S1 }), Dests(Pump()));
        Inject(new KillSession { VictimId = CB }); // prev == cur
        Check("KillSession prev == cur -> nothing", 0, Pump().Count);
        Inject(new KillSession { VictimId = CGone });
        Check("KillSession unknown char -> every zone process", Set(new[] { S1, S2, S3 }), Dests(Pump()));

        // ---- EntityInformationRequest ----------------------------------------------------------------------
        Inject(new EntityInformationRequest { TargetId = CD, EntityType = 0x01 });
        Check("EntityInformationRequest PC -> char's process", Set(new[] { S3 }), Dests(Pump()));
        Inject(new EntityInformationRequest { TargetId = (233u << 12) | 0x45, EntityType = 0x04 });
        Check("EntityInformationRequest mob -> zone from target id", Set(new[] { S3 }), Dests(Pump()));
        Inject(new EntityInformationRequest { TargetId = (232u << 12) | 0x45, EntityType = 0x02 });
        Check("EntityInformationRequest npc -> zone from target id", Set(new[] { S2 }), Dests(Pump()));

        // ---- event systems ---------------------------------------------------------------------------------
        Inject(new ConquestEvent { TypeField = 3, Payload = new byte[] { 1, 2, 3 } });
        Inject(new BesiegedEvent { TypeField = 4, Payload = new byte[] { 9 } });
        Inject(new CampaignEvent { TypeField = 5, Payload = new byte[0] });
        Inject(new ColonizationEvent { TypeField = 6, Payload = new byte[] { 7, 7 } });
        Check("system events are not forwarded", 0, Pump().Count);
        var conq = (RecSystem)sys.Conquest;
        Check("ConquestEvent -> conquest system", 1, conq.Calls.Count);
        Check("ConquestEvent type", (byte)3, conq.Calls[0].type);
        Check("ConquestEvent sender", Sender.ToString(), conq.Calls[0].ipp.ToString());
        Check("ConquestEvent payload", "1,2,3", string.Join(",", conq.Calls[0].payload));
        Check("BesiegedEvent -> besieged system", (byte)4, ((RecSystem)sys.Besieged).Calls[0].type);
        Check("CampaignEvent -> campaign system", (byte)5, ((RecSystem)sys.Campaign).Calls[0].type);
        Check("ColonizationEvent -> colonization system", "7,7", string.Join(",", ((RecSystem)sys.Colonization).Calls[0].payload));

        // ---- GM calls --------------------------------------------------------------------------------------
        logs.Clear();
        Inject(new GMCallRequest { CallId = 11, CharId = CA, CharName = "Wlaa", AccId = CA, ZoneId = (ZoneId)230, Message = "help" });
        Check("GMCallRequest not forwarded", 0, Pump().Count);
        Check("GMCallRequest logged", 1, logs.Count);
        CheckTrue("GMCallRequest log text", logs[0].Contains("GM Call #11 from Wlaa") && logs[0].EndsWith(": help"));

        Sql(db, "INSERT INTO help_desk(id,charid,message) VALUES (?,?,?)", 990411u, CA, "need help");
        Sql(db, "INSERT INTO help_desk(id,charid,message) VALUES (?,?,?)", 990412u, CGone, "need help");
        var longMsg = new string('x', 1500);
        Inject(new GMCallResponse { CallId = 990411, CharId = CA, Message = longMsg });
        r = Pump();
        Check("GMCallResponse -> char's process", Set(new[] { S1 }), Dests(r));
        Check("GMCallResponse forwarded truncated", 1024, ((GMCallResponse)IpcFraming.Decode(r[0].Payload)).Message.Length);
        var hd = LoginDb.PreparedStmt(db, "SELECT response, responded_at FROM help_desk WHERE id = ?", 990411u);
        CheckTrue("help_desk row found", hd.Next());
        Check("help_desk response truncated to 1024", 1024, hd.Get<string>("response").Length);
        CheckTrue("help_desk responded_at set", !hd.IsNull("responded_at"));

        Inject(new GMCallResponse { CallId = 990412, CharId = CGone, Message = "ok" });
        Check("GMCallResponse offline char -> no forward", 0, Pump().Count);
        hd = LoginDb.PreparedStmt(db, "SELECT response FROM help_desk WHERE id = ?", 990412u);
        hd.Next();
        Check("help_desk response stored for offline char", "ok", hd.Get<string>("response"));
        Sql(db, "DELETE FROM help_desk WHERE id IN (?, ?)", 990411u, 990412u);

        // ---- junk ------------------------------------------------------------------------------------------
        logs.Clear();
        Inject(new EmptyStruct());
        chan.Inject(Sender, new byte[] { 200, 1, 2 });
        chan.Inject(Sender, new byte[0]);
        Check("junk is never forwarded", 0, Pump().Count);
        Check("EmptyStruct + unknown logged (empty skipped)", 2, logs.Count);
        CheckTrue("EmptyStruct warning", logs[0].Contains("EmptyStruct"));
        CheckTrue("unknown message warning", logs[1].Contains("unknown message") && logs[1].Contains("code 200") && logs[1].Contains("size 3"));

        // ---- broadcast + send ------------------------------------------------------------------------------
        chan.Sent.Clear();
        ipc.BroadcastMessage(new AccountLogin(1));
        Check("BroadcastMessage -> every map endpoint", Set(new[] { S1, S2, S3 }), Dests(chan.Sent));
    }
}
