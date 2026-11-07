// XI.Verify.Ipc — Phase-1 IPC gate.
//
// 1) Enum parity: diff the machine-extracted C# MessageType against the engine's own
//    generated header (oracle/generated/ipc_stubs.h). That header IS produced by the
//    same tools/generate_ipc_stubs.py the real build runs, so matching it == matching
//    the C++ dispatch contract, with no C++ compile needed here.
// 2) DTO round-trip: every scalar message must survive [type][CBOR] -> decode -> equal.
// 3) Golden emit: write deterministic CBOR vectors to oracle/ipc_golden.txt so a later
//    glaze C++ oracle (needs a built engine tree) can be diffed byte-for-byte, exactly
//    like the blowfish/zlib/login goldens in Phase 0.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using XI.Ipc;

internal static class Program
{
    static int _pass, _fail;

    static int Main(string[] argv)
    {
        string oracleDir = argv.Length > 0
            ? argv[0]
            : @"H:\023-FFXI-server\csharp\oracle";
        string headerPath = Path.Combine(oracleDir, "generated", "ipc_stubs.h");

        Console.WriteLine("== XI.Ipc: MessageType enum parity vs generated ipc_stubs.h ==");
        int enumChecks = 0;
        if (File.Exists(headerPath))
        {
            string header = File.ReadAllText(headerPath);
            int open = header.IndexOf("enum class MessageType");
            if (open < 0) { Bad("ipc_stubs.h has no MessageType enum"); }
            else
            {
                int brace = header.IndexOf('{', open);
                int close = header.IndexOf("};", brace);
                string body = header.Substring(brace + 1, close - brace - 1);
                foreach (Match m in Regex.Matches(body, @"(?m)^\s*([A-Za-z_]\w*)\s*=\s*(\d+)\s*,?\s*$"))
                {
                    string name = m.Groups[1].Value;
                    int cppVal = int.Parse(m.Groups[2].Value);
                    bool defined = Enum.TryParse(name, out MessageType csVal);
                    Check($"enum {name}", cppVal.ToString(), defined ? ((int)csVal).ToString() : "UNDEFINED");
                    enumChecks++;
                }
                // Reverse: every C# member (except Unknown) must exist in the header.
                foreach (string n in Enum.GetNames(typeof(MessageType)))
                {
                    if (n == "Unknown") continue;
                    if (!Regex.IsMatch(body, @"(?m)^\s*" + Regex.Escape(n) + @"\s*=\s*\d+"))
                        Bad($"C# enum has {n} not present in ipc_stubs.h");
                    else
                        Ok($"reverse {n} present");
                }
            }
        }
        else
        {
            Console.WriteLine("  [SKIP] generated/ipc_stubs.h not found (run gen_ipc_messagetype_cs.ps1's source step)");
        }

        Console.WriteLine("\n== XI.Ipc: DTO round-trip ([type byte][CBOR]) ==");
        RoundTrip(new EmptyStruct(0x5A));
        RoundTrip(new AccountLogin(0xDEADBEEF));
        RoundTrip(new PlayerKick(12345u));
        RoundTrip(new PartyReload(0x0F0Fu));
        RoundTrip(new CharVarUpdate(101u, 2, 0u, "Quest[101][5]Prog"));
        RoundTrip(new CharVarUpdate(7u, -3, 1700000000u, "Mission[101][2]Prog")); // negative int32
        // ZoneId (uint16) + float + bool DTOs, unblocked after pinning xi::ZoneId = uint16_t.
        RoundTrip(new CharZone(2001u, (ZoneId)1025));
        RoundTrip(new EntityInformationRequest { RequesterId = 1u, TargetId = 2u, EntityType = 3, Warp = true, SpawnedOnly = false });
        RoundTrip(new EntityInformationResponse
        {
            RequesterId = 100u, TargetId = 200u, EntityType = 1, Warp = true,
            ZoneId = (ZoneId)1001, X = 12.5f, Y = -3.25f, Z = 0.0f, Rot = 64, MoghouseId = 7u,
        });
        RoundTrip(new SendPlayerToLocation
        {
            TargetId = 300u, ZoneId = (ZoneId)180, X = -1024.75f, Y = 0.125f, Z = 999.99f, Rot = 128, MoghouseId = 0u,
        });
        // ChatMessage* family (uint32 / string / ZoneId / CHAT_MESSAGE_TYPE / bool).
        RoundTrip(new ChatMessageTell { SenderId = 11u, SenderName = "Apollo", RecipientName = "Artemis", Message = "hello there", ZoneId = (ZoneId)1001, GmLevel = 0 });
        RoundTrip(new ChatMessageParty { PartyId = 42u, SenderId = 11u, SenderName = "Apollo", Message = "party talk", ZoneId = (ZoneId)1025, GmLevel = 1, MessageTypeField = ChatMessageType.Party });
        RoundTrip(new ChatMessageAlliance { AllianceId = 7u, SenderId = 12u, SenderName = "Zeta", Message = "ally", ZoneId = (ZoneId)180, GmLevel = 0, MessageTypeField = ChatMessageType.NsParty });
        RoundTrip(new ChatMessageLinkshell { LinkshellId = 99u, SenderId = 13u, SenderName = "LSguy", Message = "ls msg", ZoneId = (ZoneId)1030, GmLevel = 2 });
        RoundTrip(new ChatMessageUnity { UnityLeaderId = 5u, SenderId = 14u, SenderName = "Unity", Message = "unity chat", ZoneId = (ZoneId)1001, GmLevel = 0, MessageTypeField = ChatMessageType.Unity });
        RoundTrip(new ChatMessageYell { SenderId = 15u, SenderName = "Yeller", Message = "HELP", ZoneId = (ZoneId)102, GmLevel = 0, MessageTypeField = ChatMessageType.Yell });
        RoundTrip(new ChatMessageAssist { SenderId = 16u, SenderName = "Mentor", Message = "assisting", MentorRank = 1, MasteryRank = 3, GmLevel = 0, MessageTypeField = ChatMessageType.NaAssist });
        RoundTrip(new ChatMessageServerMessage { SenderId = 0u, SenderName = "Server", Message = "global announce", ZoneId = (ZoneId)0, GmLevel = 0, MessageTypeField = ChatMessageType.System1, SkipSender = true });
        RoundTrip(new ChatMessageCustom { RecipientId = 20u, SenderName = "NPC", Message = "custom text", MessageTypeField = ChatMessageType.Linkshell });
        // Party / Alliance / Message / Linkshell / scalar-event families (uint16 targ/race + enum widths).
        RoundTrip(new PartyInvite { InviteeId = 100u, InviteeTargId = 7, InviterId = 200u, InviterTargId = 9, InviterName = "Hume", InviteType = PartyKind.Alliance });
        RoundTrip(new PartyInviteResponse { InviteeId = 100u, InviteeTargId = 7, InviterId = 200u, InviterTargId = 9, InviteAnswer = 1 });
        RoundTrip(new PartyRequest { RecipientId = 300u, RequesterId = 400u, RequesterTargId = 5, RequesterZoneId = (ZoneId)1025, RequesterName = "Tarutaru", RequesterRace = 4, Kind = 2 });
        RoundTrip(new PartyDisband(555u));
        RoundTrip(new AllianceReload(666u));
        RoundTrip(new AllianceDissolve(777u));
        RoundTrip(new MessageStandard { RecipientId = 800u, Message = 0x00C8, Param0 = 1u, Param1 = 2u, String2 = "extra" });
        RoundTrip(new MessageSystem { RecipientId = 801u, Message = 0x00D4, Param0 = 10u, Param1 = 20u });
        RoundTrip(new LinkshellRankChange { RequesterId = 1u, RequesterRank = 2, MemberName = "Bobby", LinkshellId = 9u, NewRank = 0 });
        RoundTrip(new LinkshellRemove { RequesterId = 1u, RequesterRank = 3, VictimName = "Griefer", LinkshellId = 9u });
        RoundTrip(new LinkshellSetMessage { LinkshellId = 9u, LinkshellName = "LS", Poster = "Alzzy", Message = "msg", PostTime = 1700000000u });
        RoundTrip(new LuaFunction { RequesterZoneId = (ZoneId)1001, ExecutorZoneId = (ZoneId)102, FuncString = "zone:getXiTime()" });
        RoundTrip(new KillSession(4242u));
        RoundTrip(new AssistChannelEvent { SenderId = 1u, ReceiverId = 2u, Action = 3 });
        // Regional-event family (byte enum + std::vector<uint8> payload) + GM call.
        RoundTrip(new ConquestEvent { TypeField = 2, Payload = new byte[] { 0x01, 0x02, 0xFE, 0x00, 0x7F } });
        RoundTrip(new ConquestEvent { TypeField = 0, Payload = System.Array.Empty<byte>() }); // empty vector
        RoundTrip(new BesiegedEvent { TypeField = 0, Payload = new byte[] { 0xAA, 0xBB } });
        RoundTrip(new CampaignEvent { TypeField = 0, Payload = new byte[] { 0x10, 0x20, 0x30 } });
        RoundTrip(new ColonizationEvent { TypeField = 0, Payload = new byte[] { 0xFF } });
        RoundTrip(new GMCallRequest
        {
            CallId = 5u, CharId = 900u, CharName = "Summoner", AccId = 33u, ZoneId = (ZoneId)1001,
            PosX = 12.5f, PosY = -3.25f, PosZ = 0.0f, Message = "help me",
        });
        var gmc = new GMCallRequest
        {
            CallId = 6u, CharId = 901u, CharName = "Thf", AccId = 34u, ZoneId = (ZoneId)102,
            PosX = 1f, PosY = 2f, PosZ = 3f, Message = "params test",
        };
        gmc.Parameters["reason"] = "stuck";
        gmc.Parameters["zone"] = "102";
        RoundTrip(gmc);
        RoundTrip(new GMCallResponse { CallId = 7u, CharId = 902u, Message = "gm is coming" });

        Console.WriteLine("\n== XI.Ipc: framing header-byte contract ==");
        byte[] acct = IpcFraming.ToBytesWithHeader(new AccountLogin(3));
        Check("framed[0] == MessageType.AccountLogin (2)", "02", acct[0].ToString("X2"));
        Check("payload starts with CBOR definite map A1", "A1", acct[1].ToString("X2"));
        // fromBytesWithHeader with the wrong expected type must reject (== Maybe nullopt).
        var wrongType = IpcFraming.FromBytesWithHeader(acct, MessageType.PlayerKick, out _);
        Check("wrong expected type rejected", "null", wrongType?.ToString() ?? "null");
        // Dispatch registration completeness.
        Check("EmptyStruct registered", "True", IpcFraming.IsRegistered(MessageType.EmptyStruct).ToString());
        Check("GMCallResponse registered", "True", IpcFraming.IsRegistered(MessageType.GMCallResponse).ToString());
        // All 38 real MessageType members must be dispatchable (Unknown=0 stays null).
        int unregistered = 0;
        foreach (MessageType t in Enum.GetValues(typeof(MessageType)))
        {
            if (t == MessageType.Unknown) continue;
            if (!IpcFraming.IsRegistered(t)) { Bad($"unregistered: {t}"); unregistered++; }
        }
        if (unregistered == 0) Ok("all 38 MessageType members registered");

        // Emit deterministic CBOR golden for the future glaze oracle.
        string goldenPath = Path.Combine(oracleDir, "ipc_golden.txt");
        var g = new StringBuilder();
        g.AppendLine("// XI.Ipc CBOR golden (C# System.Formats.Cbor side).");
        g.AppendLine("// When a glaze C++ oracle is compilable (built engine tree), diff these vectors");
        g.AppendLine("// byte-for-byte, exactly like golden.txt / zlib_golden.txt / login_golden.txt in Phase 0.");
        Emit(g, "EmptyStruct", new EmptyStruct(0x5A));
        Emit(g, "AccountLogin", new AccountLogin(0xDEADBEEF));
        Emit(g, "PlayerKick", new PlayerKick(12345u));
        Emit(g, "PartyReload", new PartyReload(0x0F0Fu));
        Emit(g, "CharVarUpdate", new CharVarUpdate(101u, 2, 0u, "Quest[101][5]Prog"));
        Emit(g, "CharVarUpdateNeg", new CharVarUpdate(7u, -3, 1700000000u, "Mission[101][2]Prog"));
        Emit(g, "CharZone", new CharZone(2001u, (ZoneId)1025));
        Emit(g, "EntityInformationRequest", new EntityInformationRequest { RequesterId = 1u, TargetId = 2u, EntityType = 3, Warp = true, SpawnedOnly = false });
        Emit(g, "EntityInformationResponse", new EntityInformationResponse
        {
            RequesterId = 100u, TargetId = 200u, EntityType = 1, Warp = true,
            ZoneId = (ZoneId)1001, X = 12.5f, Y = -3.25f, Z = 0.0f, Rot = 64, MoghouseId = 7u,
        });
        Emit(g, "SendPlayerToLocation", new SendPlayerToLocation
        {
            TargetId = 300u, ZoneId = (ZoneId)180, X = -1024.75f, Y = 0.125f, Z = 999.99f, Rot = 128, MoghouseId = 0u,
        });
        Emit(g, "ChatMessageTell", new ChatMessageTell { SenderId = 11u, SenderName = "Apollo", RecipientName = "Artemis", Message = "hello there", ZoneId = (ZoneId)1001, GmLevel = 0 });
        Emit(g, "ChatMessageParty", new ChatMessageParty { PartyId = 42u, SenderId = 11u, SenderName = "Apollo", Message = "party talk", ZoneId = (ZoneId)1025, GmLevel = 1, MessageTypeField = ChatMessageType.Party });
        Emit(g, "ChatMessageAlliance", new ChatMessageAlliance { AllianceId = 7u, SenderId = 12u, SenderName = "Zeta", Message = "ally", ZoneId = (ZoneId)180, GmLevel = 0, MessageTypeField = ChatMessageType.NsParty });
        Emit(g, "ChatMessageLinkshell", new ChatMessageLinkshell { LinkshellId = 99u, SenderId = 13u, SenderName = "LSguy", Message = "ls msg", ZoneId = (ZoneId)1030, GmLevel = 2 });
        Emit(g, "ChatMessageUnity", new ChatMessageUnity { UnityLeaderId = 5u, SenderId = 14u, SenderName = "Unity", Message = "unity chat", ZoneId = (ZoneId)1001, GmLevel = 0, MessageTypeField = ChatMessageType.Unity });
        Emit(g, "ChatMessageYell", new ChatMessageYell { SenderId = 15u, SenderName = "Yeller", Message = "HELP", ZoneId = (ZoneId)102, GmLevel = 0, MessageTypeField = ChatMessageType.Yell });
        Emit(g, "ChatMessageAssist", new ChatMessageAssist { SenderId = 16u, SenderName = "Mentor", Message = "assisting", MentorRank = 1, MasteryRank = 3, GmLevel = 0, MessageTypeField = ChatMessageType.NaAssist });
        Emit(g, "ChatMessageServerMessage", new ChatMessageServerMessage { SenderId = 0u, SenderName = "Server", Message = "global announce", ZoneId = (ZoneId)0, GmLevel = 0, MessageTypeField = ChatMessageType.System1, SkipSender = true });
        Emit(g, "ChatMessageCustom", new ChatMessageCustom { RecipientId = 20u, SenderName = "NPC", Message = "custom text", MessageTypeField = ChatMessageType.Linkshell });
        Emit(g, "PartyInvite", new PartyInvite { InviteeId = 100u, InviteeTargId = 7, InviterId = 200u, InviterTargId = 9, InviterName = "Hume", InviteType = PartyKind.Alliance });
        Emit(g, "PartyInviteResponse", new PartyInviteResponse { InviteeId = 100u, InviteeTargId = 7, InviterId = 200u, InviterTargId = 9, InviteAnswer = 1 });
        Emit(g, "PartyRequest", new PartyRequest { RecipientId = 300u, RequesterId = 400u, RequesterTargId = 5, RequesterZoneId = (ZoneId)1025, RequesterName = "Tarutaru", RequesterRace = 4, Kind = 2 });
        Emit(g, "PartyDisband", new PartyDisband(555u));
        Emit(g, "AllianceReload", new AllianceReload(666u));
        Emit(g, "AllianceDissolve", new AllianceDissolve(777u));
        Emit(g, "MessageStandard", new MessageStandard { RecipientId = 800u, Message = 0x00C8, Param0 = 1u, Param1 = 2u, String2 = "extra" });
        Emit(g, "MessageSystem", new MessageSystem { RecipientId = 801u, Message = 0x00D4, Param0 = 10u, Param1 = 20u });
        Emit(g, "LinkshellRankChange", new LinkshellRankChange { RequesterId = 1u, RequesterRank = 2, MemberName = "Bobby", LinkshellId = 9u, NewRank = 0 });
        Emit(g, "LinkshellRemove", new LinkshellRemove { RequesterId = 1u, RequesterRank = 3, VictimName = "Griefer", LinkshellId = 9u });
        Emit(g, "LinkshellSetMessage", new LinkshellSetMessage { LinkshellId = 9u, LinkshellName = "LS", Poster = "Alzzy", Message = "msg", PostTime = 1700000000u });
        Emit(g, "LuaFunction", new LuaFunction { RequesterZoneId = (ZoneId)1001, ExecutorZoneId = (ZoneId)102, FuncString = "zone:getXiTime()" });
        Emit(g, "KillSession", new KillSession(4242u));
        Emit(g, "AssistChannelEvent", new AssistChannelEvent { SenderId = 1u, ReceiverId = 2u, Action = 3 });
        Emit(g, "ConquestEvent", new ConquestEvent { TypeField = 2, Payload = new byte[] { 0x01, 0x02, 0xFE, 0x00, 0x7F } });
        Emit(g, "ConquestEventEmpty", new ConquestEvent { TypeField = 0, Payload = System.Array.Empty<byte>() });
        Emit(g, "BesiegedEvent", new BesiegedEvent { TypeField = 0, Payload = new byte[] { 0xAA, 0xBB } });
        Emit(g, "CampaignEvent", new CampaignEvent { TypeField = 0, Payload = new byte[] { 0x10, 0x20, 0x30 } });
        Emit(g, "ColonizationEvent", new ColonizationEvent { TypeField = 0, Payload = new byte[] { 0xFF } });
        Emit(g, "GMCallRequest", new GMCallRequest
        {
            CallId = 5u, CharId = 900u, CharName = "Summoner", AccId = 33u, ZoneId = (ZoneId)1001,
            PosX = 12.5f, PosY = -3.25f, PosZ = 0.0f, Message = "help me",
        });
        var gmcEmit = new GMCallRequest
        {
            CallId = 6u, CharId = 901u, CharName = "Thf", AccId = 34u, ZoneId = (ZoneId)102,
            PosX = 1f, PosY = 2f, PosZ = 3f, Message = "params test",
        };
        gmcEmit.Parameters["reason"] = "stuck";
        gmcEmit.Parameters["zone"] = "102";
        Emit(g, "GMCallRequestWithParams", gmcEmit);
        Emit(g, "GMCallResponse", new GMCallResponse { CallId = 7u, CharId = 902u, Message = "gm is coming" });
        File.WriteAllText(goldenPath, g.ToString());
        Console.WriteLine($"\n== golden emitted: {goldenPath} ==");

        Console.WriteLine($"\nRESULT: {_pass} passed, {_fail} failed  (enum members checked: {enumChecks})");
        return _fail == 0 ? 0 : 1;
    }

    static void RoundTrip(IIpcMessage msg)
    {
        byte[] framed = IpcFraming.ToBytesWithHeader(msg);
        var back = IpcFraming.FromBytesWithHeader(framed, msg.Type, out _);
        bool ok = back != null && back.GetType() == msg.GetType() && FieldsEqual(msg, back);
        Check($"roundtrip {msg.Type}", "equal", ok ? "equal" : "DIFF");
    }

    static bool FieldsEqual(IIpcMessage a, IIpcMessage b)
    {
        switch (a)
        {
            case EmptyStruct x: return x.Unused == ((EmptyStruct)b).Unused;
            case AccountLogin x: return x.AccountId == ((AccountLogin)b).AccountId;
            case PlayerKick x: return x.VictimId == ((PlayerKick)b).VictimId;
            case PartyReload x: return x.PartyId == ((PartyReload)b).PartyId;
            case CharVarUpdate x:
                var y = (CharVarUpdate)b;
                return x.CharId == y.CharId && x.Value == y.Value && x.Expiry == y.Expiry && x.VarName == y.VarName;
            case CharZone x:
                var cz = (CharZone)b;
                return x.CharId == cz.CharId && x.DestinationZoneId == cz.DestinationZoneId;
            case EntityInformationRequest x:
                var eir = (EntityInformationRequest)b;
                return x.RequesterId == eir.RequesterId && x.TargetId == eir.TargetId
                    && x.EntityType == eir.EntityType && x.Warp == eir.Warp && x.SpawnedOnly == eir.SpawnedOnly;
            case EntityInformationResponse x:
                var ers = (EntityInformationResponse)b;
                return x.RequesterId == ers.RequesterId && x.TargetId == ers.TargetId && x.EntityType == ers.EntityType
                    && x.Warp == ers.Warp && x.ZoneId == ers.ZoneId && x.X == ers.X && x.Y == ers.Y && x.Z == ers.Z
                    && x.Rot == ers.Rot && x.MoghouseId == ers.MoghouseId;
            case SendPlayerToLocation x:
                var sp = (SendPlayerToLocation)b;
                return x.TargetId == sp.TargetId && x.ZoneId == sp.ZoneId && x.X == sp.X && x.Y == sp.Y && x.Z == sp.Z
                    && x.Rot == sp.Rot && x.MoghouseId == sp.MoghouseId;
            case ChatMessageTell x:
                var ct = (ChatMessageTell)b;
                return x.SenderId == ct.SenderId && x.SenderName == ct.SenderName && x.RecipientName == ct.RecipientName
                    && x.Message == ct.Message && x.ZoneId == ct.ZoneId && x.GmLevel == ct.GmLevel;
            case ChatMessageParty x:
                var cp = (ChatMessageParty)b;
                return x.PartyId == cp.PartyId && x.SenderId == cp.SenderId && x.SenderName == cp.SenderName
                    && x.Message == cp.Message && x.ZoneId == cp.ZoneId && x.GmLevel == cp.GmLevel && x.MessageTypeField == cp.MessageTypeField;
            case ChatMessageAlliance x:
                var ca = (ChatMessageAlliance)b;
                return x.AllianceId == ca.AllianceId && x.SenderId == ca.SenderId && x.SenderName == ca.SenderName
                    && x.Message == ca.Message && x.ZoneId == ca.ZoneId && x.GmLevel == ca.GmLevel && x.MessageTypeField == ca.MessageTypeField;
            case ChatMessageLinkshell x:
                var cl = (ChatMessageLinkshell)b;
                return x.LinkshellId == cl.LinkshellId && x.SenderId == cl.SenderId && x.SenderName == cl.SenderName
                    && x.Message == cl.Message && x.ZoneId == cl.ZoneId && x.GmLevel == cl.GmLevel;
            case ChatMessageUnity x:
                var cu = (ChatMessageUnity)b;
                return x.UnityLeaderId == cu.UnityLeaderId && x.SenderId == cu.SenderId && x.SenderName == cu.SenderName
                    && x.Message == cu.Message && x.ZoneId == cu.ZoneId && x.GmLevel == cu.GmLevel && x.MessageTypeField == cu.MessageTypeField;
            case ChatMessageYell x:
                var cy = (ChatMessageYell)b;
                return x.SenderId == cy.SenderId && x.SenderName == cy.SenderName && x.Message == cy.Message
                    && x.ZoneId == cy.ZoneId && x.GmLevel == cy.GmLevel && x.MessageTypeField == cy.MessageTypeField;
            case ChatMessageAssist x:
                var cas = (ChatMessageAssist)b;
                return x.SenderId == cas.SenderId && x.SenderName == cas.SenderName && x.Message == cas.Message
                    && x.MentorRank == cas.MentorRank && x.MasteryRank == cas.MasteryRank && x.GmLevel == cas.GmLevel
                    && x.MessageTypeField == cas.MessageTypeField;
            case ChatMessageServerMessage x:
                var csm = (ChatMessageServerMessage)b;
                return x.SenderId == csm.SenderId && x.SenderName == csm.SenderName && x.Message == csm.Message
                    && x.ZoneId == csm.ZoneId && x.GmLevel == csm.GmLevel && x.MessageTypeField == csm.MessageTypeField
                    && x.SkipSender == csm.SkipSender;
            case ChatMessageCustom x:
                var ccu = (ChatMessageCustom)b;
                return x.RecipientId == ccu.RecipientId && x.SenderName == ccu.SenderName && x.Message == ccu.Message
                    && x.MessageTypeField == ccu.MessageTypeField;
            case PartyInvite x:
                var pi = (PartyInvite)b;
                return x.InviteeId == pi.InviteeId && x.InviteeTargId == pi.InviteeTargId && x.InviterId == pi.InviterId
                    && x.InviterTargId == pi.InviterTargId && x.InviterName == pi.InviterName && x.InviteType == pi.InviteType;
            case PartyInviteResponse x:
                var pr = (PartyInviteResponse)b;
                return x.InviteeId == pr.InviteeId && x.InviteeTargId == pr.InviteeTargId && x.InviterId == pr.InviterId
                    && x.InviterTargId == pr.InviterTargId && x.InviteAnswer == pr.InviteAnswer;
            case PartyRequest x:
                var pq = (PartyRequest)b;
                return x.RecipientId == pq.RecipientId && x.RequesterId == pq.RequesterId && x.RequesterTargId == pq.RequesterTargId
                    && x.RequesterZoneId == pq.RequesterZoneId && x.RequesterName == pq.RequesterName
                    && x.RequesterRace == pq.RequesterRace && x.Kind == pq.Kind;
            case PartyDisband x: return x.PartyId == ((PartyDisband)b).PartyId;
            case AllianceReload x: return x.AllianceId == ((AllianceReload)b).AllianceId;
            case AllianceDissolve x: return x.AllianceId == ((AllianceDissolve)b).AllianceId;
            case MessageStandard x:
                var ms = (MessageStandard)b;
                return x.RecipientId == ms.RecipientId && x.Message == ms.Message && x.Param0 == ms.Param0
                    && x.Param1 == ms.Param1 && x.String2 == ms.String2;
            case MessageSystem x:
                var my = (MessageSystem)b;
                return x.RecipientId == my.RecipientId && x.Message == my.Message && x.Param0 == my.Param0 && x.Param1 == my.Param1;
            case LinkshellRankChange x:
                var lr = (LinkshellRankChange)b;
                return x.RequesterId == lr.RequesterId && x.RequesterRank == lr.RequesterRank && x.MemberName == lr.MemberName
                    && x.LinkshellId == lr.LinkshellId && x.NewRank == lr.NewRank;
            case LinkshellRemove x:
                var lx = (LinkshellRemove)b;
                return x.RequesterId == lx.RequesterId && x.RequesterRank == lx.RequesterRank && x.VictimName == lx.VictimName
                    && x.LinkshellId == lx.LinkshellId;
            case LinkshellSetMessage x:
                var ls = (LinkshellSetMessage)b;
                return x.LinkshellId == ls.LinkshellId && x.LinkshellName == ls.LinkshellName && x.Poster == ls.Poster
                    && x.Message == ls.Message && x.PostTime == ls.PostTime;
            case LuaFunction x:
                var lf = (LuaFunction)b;
                return x.RequesterZoneId == lf.RequesterZoneId && x.ExecutorZoneId == lf.ExecutorZoneId && x.FuncString == lf.FuncString;
            case KillSession x: return x.VictimId == ((KillSession)b).VictimId;
            case AssistChannelEvent x:
                var ac = (AssistChannelEvent)b;
                return x.SenderId == ac.SenderId && x.ReceiverId == ac.ReceiverId && x.Action == ac.Action;
            case ConquestEvent x:
                var ce = (ConquestEvent)b;
                return x.TypeField == ce.TypeField && BytesEqual(x.Payload, ce.Payload);
            case BesiegedEvent x:
                var be = (BesiegedEvent)b;
                return x.TypeField == be.TypeField && BytesEqual(x.Payload, be.Payload);
            case CampaignEvent x:
                var cpe = (CampaignEvent)b;
                return x.TypeField == cpe.TypeField && BytesEqual(x.Payload, cpe.Payload);
            case ColonizationEvent x:
                var cle = (ColonizationEvent)b;
                return x.TypeField == cle.TypeField && BytesEqual(x.Payload, cle.Payload);
            case GMCallRequest x:
                var gr = (GMCallRequest)b;
                return x.CallId == gr.CallId && x.CharId == gr.CharId && x.CharName == gr.CharName
                    && x.AccId == gr.AccId && x.ZoneId == gr.ZoneId && x.PosX == gr.PosX && x.PosY == gr.PosY
                    && x.PosZ == gr.PosZ && x.Message == gr.Message && DictEqual(x.Parameters, gr.Parameters);
            case GMCallResponse x:
                var grr = (GMCallResponse)b;
                return x.CallId == grr.CallId && x.CharId == grr.CharId && x.Message == grr.Message;
            default: return false;
        }
    }

    static bool BytesEqual(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    static bool DictEqual(System.Collections.Generic.Dictionary<string, string> a, System.Collections.Generic.Dictionary<string, string> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var kv in a)
        {
            if (!b.TryGetValue(kv.Key, out var v) || v != kv.Value) return false;
        }
        return true;
    }

    static void Emit(StringBuilder g, string label, IIpcMessage msg)
    {
        byte[] framed = IpcFraming.ToBytesWithHeader(msg);
        var sb = new StringBuilder();
        foreach (byte x in framed) sb.Append(x.ToString("X2"));
        g.AppendLine($"{label} {sb}");
    }

    static void Check(string name, string expected, string actual)
    {
        bool ok = expected == actual;
        if (ok) Ok(name); else Bad($"{name}: expected {expected} actual {actual}");
    }
    static void Ok(string m) { _pass++; Console.WriteLine($"  [PASS] {m}"); }
    static void Bad(string m) { _fail++; Console.WriteLine($"  [FAIL] {m}"); }
}
