using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using XI.Ipc;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // Chat: GP_CLI_COMMAND_CHAT_STD (0x0B5) and GP_CLI_COMMAND_CHAT_NAME (0x0B6), the !commands (CCommandHandler), and
    // the chat IPC the world routes back to this map (ipc_client.cpp). Messages are raw bytes (Shift-JIS and
    // auto-translate phrases); over IPC they ride as Latin-1 strings (CborRaw). Unity chat goes to the char's unity
    // (unitychat, the members online here) and the assist channels to the eligible (CAMANContainer, ZoneHost.Assist);
    // party and alliance chat ride the party IPC (ZoneHost.Party), linkshell chat the linkshell IPC (ZoneHost.Linkshell).
    public sealed partial class ZoneHost
    {
        // CHAT_MESSAGE_TYPE
        private const byte ChatSay = 0, ChatShout = 1, ChatTell = 3, ChatSystem1 = 6, ChatEmotion = 8;

        // GP_CLI_COMMAND_CHAT_STD_KIND
        private const byte KindSay = 0x00, KindShout = 0x01, KindParty = 0x04, KindLinkshell1 = 0x05, KindEmote = 0x08,
            KindLinkshellPvp = 0x18, KindYell = 0x1A, KindLinkshell2 = 0x1B, KindUnity = 0x21, KindAssistJ = 0x22, KindAssistE = 0x23;

        // enums/msg_std.h and msg_basic.h, the ones chat sends
        private const ushort MsgStdWaitLonger = 38, MsgStdExamine = 89, MsgStdTellNotReceivedOffline = 125, MsgStdTellNotReceivedAway = 181,
            MsgStdCannotHere = 256, MsgStdMonstrosityCheckOut = 263, MsgStdMonstrosityCheckIn = 264, MsgStdUnityNotParticipating = 286;
        private const ushort MsgCannotInThisArea = 40;

        private const float ShoutDistance = 180.0f;

        // message::send: Program wires this to the world dealer
        public Action<IIpcMessage> SendIpc = _ => { };

        // unitychat::UnityChatList (leader -> the online members on this map) and each member's PUnityChat leader
        private readonly Dictionary<uint, List<CharData>> unityChats_ = new();
        private readonly Dictionary<CharData, uint> unityOf_ = new();

        private StatSettings ChatCfg => loader_.Stats?.Settings ?? defaultChatCfg_;
        private static readonly StatSettings defaultChatCfg_ = new();

        // jailutils::InPrison
        private static bool InPrison(CharData c) => c.AccountGmLevel == 0 && c.Zone == MordionGaolZone;

        // ---- S2C ----------------------------------------------------------------------------------------------------

        // GP_SERV_COMMAND_CHAT_STD (0x017): Kind @4, Attr @5, Data (zone) @6, sName[15] @8, Mes[150] @23, the packet cut
        // to the message rounded up to 4
        private static BasicPacket ChatStd(byte kind, string name, ushort data, bool gmAttr, ReadOnlySpan<byte> message)
        {
            int len = Math.Min(message.Length, 150);
            var w = new PacketWriter(0x017, (uint)((23 + len + 3) & ~3));
            w.U8(4, kind);
            if (gmAttr) w.U8(5, 1);
            w.U16(6, data);
            var n = CborRaw.Bytes(name);
            w.Bytes(8, n.AsSpan(0, Math.Min(n.Length, 15)));
            w.Bytes(23, message[..len]);
            return w.Packet;
        }

        // GP_SERV_COMMAND_CHAT_STD(PChar, type, message, sender): the char's name unless a sender is given
        internal static BasicPacket ChatStd(CharData c, byte kind, ReadOnlySpan<byte> message, string sender = "") =>
            ChatStd(kind, sender == "" ? c.Name : sender, c.Zone, c.GmLevel >= 3 && sender == "", message);

        // GP_SERV_COMMAND_CHAT_STD(name, zone, type, message, gmLevel)
        private static BasicPacket ChatStd(string name, ushort zone, byte kind, ReadOnlySpan<byte> message, byte gmLevel) =>
            ChatStd(kind, name, zone, gmLevel >= 3 && name == "", message);

        // GP_SERV_COMMAND_MESSAGE(PChar, param0, MsgStd): "string2 <name> string3 <param0>"
        private static BasicPacket MessageNamed(CharData c, uint param0, ushort messageId) =>
            MessageData(messageId, $"string2 {c.Name} string3 {param0}", 40);

        // GP_SERV_COMMAND_MESSAGE(string2, MsgStd)
        private static BasicPacket MessageString2(string string2, ushort messageId) => MessageData(messageId, "string2 " + string2, 24);

        // GP_SERV_COMMAND_MESSAGE(PChar, param0, param1, MsgStd): the char's ids; Examine and Monstrosity name it
        private static BasicPacket MessageFor(CharData c, uint param0, uint param1, ushort messageId)
        {
            if (c != null && (messageId == MsgStdExamine || messageId == MsgStdMonstrosityCheckIn || messageId == MsgStdMonstrosityCheckOut))
            {
                var p = MessageData(messageId, "string2 " + c.Name, 24);
                if (messageId == MsgStdExamine) p.Buffer[12] = 0x10;
                new PacketWriter(p).U32(4, c.Id);
                new PacketWriter(p).U16(8, c.TargId);
                return p;
            }
            var w = new PacketWriter(0x009, 0x24);
            w.U16(10, messageId);
            if (c != null)
            {
                w.U32(4, c.Id);
                w.U16(8, c.TargId);
            }
            else w.Name(13, $"Para0 {param0} Para1 {param1}", 23);
            return w.Packet;
        }

        // 0x009 with snprintf(Data, max, ...) at @13: the packet cut to the text rounded up to 4
        private static BasicPacket MessageData(ushort messageId, string data, int max)
        {
            var bytes = CborRaw.Bytes(data);
            int len = Math.Min(bytes.Length, max - 1);
            var w = new PacketWriter(0x009, (uint)((0x10 + len + 3) & ~3));
            w.U16(10, messageId);
            w.Bytes(13, bytes.AsSpan(0, len));
            return w.Packet;
        }

        // ---- C2S ----------------------------------------------------------------------------------------------------

        // asStringFromUntrustedSource(ptr, len): at most len bytes, cut at the first NUL
        private static byte[] UntrustedBytes(BasicPacket p, int off, int len)
        {
            len = Math.Clamp(len, 0, Math.Max(0, p.Buffer.Length - off));
            var span = p.Buffer.AsSpan(off, len);
            int nul = span.IndexOf((byte)0);
            return (nul >= 0 ? span[..nul] : span).ToArray();
        }

        private static PacketValidator ValidateChatStd(CharData c, BasicPacket p) =>
            Pv(c).OneOf("Kind", p.Buffer[4], KindSay, KindShout, KindParty, KindLinkshell1, KindEmote, KindLinkshellPvp, KindYell,
                        KindLinkshell2, KindUnity, KindAssistJ, KindAssistE);

        // GP_CLI_COMMAND_CHAT_STD
        private void OnChatStd(MapSession s, CharData c, BasicPacket p)
        {
            byte kind = p.Buffer[4];
            var raw = UntrustedBytes(p, 6, Math.Min((int)p.GetSize() - 6, 128));
            byte first = raw.Length > 0 ? raw[0] : (byte)0;
            var withoutFirst = raw.Length > 0 ? raw[1..] : Array.Empty<byte>();

            // !commands; a GM's mistyped one goes no further either
            if (first == '!' && !InPrison(c))
            {
                if (CallCommand(c, withoutFirst) || c.AccountGmLevel > 0) return;
            }

            // a GM's #message to the whole server
            if (first == '#' && c.AccountGmLevel > 0)
            {
                SendIpc(new ChatMessageServerMessage
                {
                    SenderId = c.Id, SenderName = c.Name, Message = CborRaw.FromBytes(withoutFirst),
                    ZoneId = (XI.Ipc.ZoneId)c.Zone, GmLevel = c.AccountGmLevel,
                });
                return;
            }

            // jailed: /say only
            if (InPrison(c))
            {
                if (kind == KindSay)
                {
                    AuditChat(c, "SAY", raw);
                    PushCharInRange(c, ChatStd(c, ChatSay, raw), false);
                }
                else Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgCannotInThisArea));
                return;
            }

            var msg = CborRaw.FromBytes(raw);
            switch (kind)
            {
                case KindSay:
                    AuditChat(c, "SAY", raw);
                    PushCharInRange(c, ChatStd(c, ChatSay, raw), false);
                    break;
                case KindEmote:
                    PushCharInRange(c, ChatStd(c, ChatEmotion, raw), false);
                    break;
                case KindShout:
                    AuditChat(c, "SHOUT", raw);
                    PushCharInShout(c, ChatStd(c, ChatShout, raw));
                    break;
                case KindLinkshell1:
                    SendLinkshellChat(c, c.Linkshell1, msg, raw);
                    break;
                case KindLinkshell2:
                    SendLinkshellChat(c, c.Linkshell2, msg, raw);
                    break;
                case KindParty:
                    if (c.Party == null) break;
                    if (c.Party.Alliance != null)
                        SendIpc(new ChatMessageAlliance
                        {
                            AllianceId = c.Party.Alliance.Id, SenderId = c.Id, SenderName = c.Name, Message = msg,
                            ZoneId = (XI.Ipc.ZoneId)c.Zone, GmLevel = c.AccountGmLevel,
                        });
                    else
                        SendIpc(new ChatMessageParty
                        {
                            PartyId = c.Party.Id, SenderId = c.Id, SenderName = c.Name, Message = msg,
                            ZoneId = (XI.Ipc.ZoneId)c.Zone, GmLevel = c.AccountGmLevel,
                        });
                    AuditChat(c, "PARTY", raw);
                    break;
                case KindYell:
                    if (!AreaOf(c).CanUseMisc("yell")) Push(s, MessageNamed(c, 0, MsgStdCannotHere));
                    else if (CharVar(c, "[YELL]Banned") == 1) Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgCannotUseInArea));
                    else if (CharVar(c, "[YELL]Cooldown") == 1) Push(s, MessageNamed(c, 0, MsgStdWaitLonger));
                    else
                    {
                        // self-expires after the cooldown
                        SetCharVar(c, "[YELL]Cooldown", 1, (uint)(UnixNow() + (ushort)ChatCfg.YellCooldown));
                        SendIpc(new ChatMessageYell
                        {
                            SenderId = c.Id, SenderName = c.Name, Message = msg, ZoneId = (XI.Ipc.ZoneId)c.Zone, GmLevel = c.AccountGmLevel,
                        });
                        AuditChat(c, "YELL", raw);
                    }
                    break;
                case KindUnity:
                    if (!unityOf_.TryGetValue(c, out uint leader))
                    {
                        Push(s, SelfPackets.Message(MsgStdUnityNotParticipating));
                        return;
                    }
                    SendIpc(new ChatMessageUnity
                    {
                        UnityLeaderId = leader, SenderId = c.Id, SenderName = c.Name, Message = msg,
                        ZoneId = (XI.Ipc.ZoneId)c.Zone, GmLevel = c.AccountGmLevel,
                    });
                    RoeEvent(RoeEventUnityChat, c, new (string, object)[] { ("unityMessage", raw) });
                    if (ChatCfg.AuditChat && ChatCfg.AuditUnity)
                        Exec("INSERT INTO audit_chat (speaker, type, zoneid, unity, message, datetime) VALUES(?, 'UNITY', ?, ?, ?, current_timestamp())",
                             BoundValue.Of(c.Name), BoundValue.Of(c.Zone), BoundValue.Of(leader), BoundValue.Of(raw));
                    break;
                case KindLinkshellPvp:
                    // not implemented in the C++ either, only audited
                    AuditChat(c, "BALLISTA", raw);
                    break;
                case KindAssistJ:
                case KindAssistE:
                {
                    // muted or not eligible: silently dropped
                    if (!ChatCfg.AssistChannelEnabled || !AreaOf(c).CanUseMisc("assist") || Aman(c).IsMuted || !IsAssistChannelEligible(c))
                        return;
                    RecordLastAssistMessage(c);
                    bool jp = kind == KindAssistJ;
                    SendIpc(new ChatMessageAssist
                    {
                        SenderId = c.Id, SenderName = c.Name, Message = msg, MentorRank = c.MentorFlg ? MentorRank(c) : (byte)0,
                        MasteryRank = MasteryRank, GmLevel = c.AccountGmLevel,
                        MessageTypeField = jp ? ChatMessageType.JpAssist : ChatMessageType.NaAssist,
                    });
                    AuditChat(c, jp ? "ASSISTJ" : "ASSISTE", raw);
                    break;
                }
            }
            c.History[HistoryChatsSent]++;
        }

        private static PacketValidator ValidateChatName(CharData c, BasicPacket p) =>
            Pv(c).MustEqual(p.Buffer[4], 3, "unknown04 not 3").MustEqual(p.Buffer[5], 0, "unknown05 not 0");

        // GP_CLI_COMMAND_CHAT_NAME: a tell, through the world to whichever map has the recipient
        private void OnChatName(MapSession s, CharData c, BasicPacket p)
        {
            if (InPrison(c))
            {
                Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgCannotUseInArea));
                return;
            }

            var recipient = CborRaw.FromBytes(UntrustedBytes(p, 6, 15));
            var raw = UntrustedBytes(p, 0x15, Math.Min((int)p.GetSize() - 0x15, 128));
            // "_CUSTOM_MENU" answers a player:customMenu()
            if (recipient == "_CUSTOM_MENU" && customMenus_.ContainsKey(c.Id))
            {
                HandleCustomMenu(c, raw);
                return;
            }

            SendIpc(new ChatMessageTell
            {
                SenderId = c.Id, SenderName = c.Name, RecipientName = recipient, Message = CborRaw.FromBytes(raw),
                ZoneId = (XI.Ipc.ZoneId)c.Zone, GmLevel = c.AccountGmLevel,
            });
            if (ChatCfg.AuditChat && ChatCfg.AuditTell)
                Exec("INSERT INTO audit_chat (speaker, type, zoneid, recipient, message, datetime) VALUES(?, 'TELL', ?, ?, ?, current_timestamp())",
                     BoundValue.Of(c.Name), BoundValue.Of(c.Zone), BoundValue.Of(recipient), BoundValue.Of(raw));
        }

        // auditChat: map.AUDIT_CHAT and map.AUDIT_<type>
        private void AuditChat(CharData c, string type, byte[] raw)
        {
            bool on = type switch
            {
                "SAY" => ChatCfg.AuditSay, "SHOUT" => ChatCfg.AuditShout, "YELL" => ChatCfg.AuditYell, "PARTY" => ChatCfg.AuditParty,
                "BALLISTA" => ChatCfg.AuditBallista, "ASSISTE" => ChatCfg.AuditAssistE, "ASSISTJ" => ChatCfg.AuditAssistJ,
                _ => false,
            };
            if (!ChatCfg.AuditChat || !on) return;
            Exec("INSERT INTO audit_chat (speaker, type, zoneid, message, datetime) VALUES(?, ?, ?, ?, current_timestamp())",
                 BoundValue.Of(c.Name), BoundValue.Of(type), BoundValue.Of(c.Zone), BoundValue.Of(raw));
        }

        // CHAR_INSHOUT: the other chars in the zone under 180 yalms, in the same mog house
        private void PushCharInShout(CharData source, BasicPacket p)
        {
            foreach (var (c, s) in charSessions_)
            {
                if (c == source || !SameArea(c, source) || c.MoghouseId != source.MoghouseId) continue;
                if (Distance(c, source) < ShoutDistance) s.PacketList.AddLast(new BasicPacket(p.Buffer));
            }
        }

        // ---- unity chat ---------------------------------------------------------------------------------------------

        // unitychat::DelOnlineMember for the char's PUnityChat (also ~CCharEntity: leaving the zone)
        private void LeaveUnityChat(CharData c)
        {
            if (!unityOf_.Remove(c, out uint leader)) return;
            Exec("UPDATE accounts_sessions SET unitychat = 0 WHERE charid = ? LIMIT 1", BoundValue.Of(c.Id));
            if (unityChats_.TryGetValue(leader, out var members))
            {
                members.Remove(c);
                if (members.Count == 0) unityChats_.Remove(leader);
            }
        }

        // unitychat::AddOnlineMember (leader 0 joins nothing)
        private void JoinUnityChat(CharData c, uint leader)
        {
            if (leader == 0) return;
            if (!unityChats_.TryGetValue(leader, out var members)) unityChats_[leader] = members = new List<CharData>();
            Exec("UPDATE accounts_sessions SET unitychat = ? WHERE charid = ? LIMIT 1", BoundValue.Of(leader), BoundValue.Of(c.Id));
            unityOf_[c] = leader;
            members.Add(c);
        }

        // ---- IPC from the world -------------------------------------------------------------------------------------

        // zoneutils::GetChar: a char in a zone on this map
        private (CharData, MapSession) CharById(uint id)
        {
            foreach (var (c, s) in charSessions_)
                if (c.Id == id && s.InZone) return (c, s);
            return (null, null);
        }

        // zoneutils::GetCharByName: strcmpi
        private (CharData, MapSession) CharByName(string name)
        {
            foreach (var (c, s) in charSessions_)
                if (s.InZone && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)) return (c, s);
            return (null, null);
        }

        // IPCClient::handleMessage_*: the chat and messages the world routed to this map
        public void OnIpc(IIpcMessage message)
        {
            switch (message)
            {
                // IPCClient::handleMessage_LuaFunction: the string run as a script (SendLuaFuncStringToZone); its
                // result is not sent back to the requester (a TODO in C++ too)
                case XI.Ipc.LuaFunction m:
                {
                    try
                    {
                        lua_?.DoString(m.FuncString, m.FuncString);
                    }
                    catch (LuaException e)
                    {
                        log_($"IPCClient::handleMessage_LuaFunction: error: {e.Message}: {m.FuncString}");
                    }
                    break;
                }
                case ChatMessageTell m:
                {
                    var (c, s) = CharByName(m.RecipientName);
                    bool gmSent = m.GmLevel > 0;
                    if (c == null || zoneChanges_.ContainsKey(s) || InPrison(c))
                        SendIpc(new MessageStandard { RecipientId = m.SenderId, Message = MsgStdTellNotReceivedOffline });
                    else if (ChatCfg.BlockTellToHiddenGm && c.GmHidden && !gmSent)
                        SendIpc(new MessageStandard { RecipientId = m.SenderId, Message = MsgStdTellNotReceivedOffline });
                    else if (c.AwayFlg && !gmSent)
                        SendIpc(new MessageStandard { RecipientId = m.SenderId, Message = MsgStdTellNotReceivedAway });
                    else
                        Push(s, ChatStd(c, ChatTell, CborRaw.Bytes(m.Message), m.SenderName));
                    break;
                }
                case ChatMessageYell m:
                {
                    var p = ChatStd(m.SenderName, (ushort)m.ZoneId, (byte)m.MessageTypeField, CborRaw.Bytes(m.Message), m.GmLevel);
                    foreach (var (c, s) in charSessions_)
                        if (s.InZone && c.Id != m.SenderId && AreaOf(c).CanUseMisc("yell")) Push(s, new BasicPacket(p.Buffer));
                    break;
                }
                case ChatMessageServerMessage m:
                {
                    var p = ChatStd(m.SenderName, (ushort)m.ZoneId, (byte)m.MessageTypeField, CborRaw.Bytes(m.Message), m.GmLevel);
                    foreach (var (c, s) in charSessions_)
                        if (s.InZone && !(c.Id == m.SenderId && m.SkipSender)) Push(s, new BasicPacket(p.Buffer));
                    break;
                }
                case ChatMessageUnity m:
                {
                    // CUnityChat::PushPacket: the members but the sender, not Disappear or jailed
                    if (!unityChats_.TryGetValue(m.UnityLeaderId, out var members)) break;
                    var p = ChatStd(m.SenderName, (ushort)m.ZoneId, (byte)m.MessageTypeField, CborRaw.Bytes(m.Message), m.GmLevel);
                    foreach (var c in members)
                        if (c.Id != m.SenderId && charSessions_.TryGetValue(c, out var s) && s.InZone && !zoneChanges_.ContainsKey(s) &&
                            !InPrison(c))
                            Push(s, new BasicPacket(p.Buffer));
                    break;
                }
                case ChatMessageCustom m:
                {
                    var (c, s) = CharById(m.RecipientId);
                    if (c != null && !zoneChanges_.ContainsKey(s) && !InPrison(c))
                        Push(s, ChatStd(c, (byte)m.MessageTypeField, CborRaw.Bytes(m.Message), m.SenderName));
                    break;
                }
                case MessageStandard m:
                {
                    var (c, s) = CharById(m.RecipientId);
                    if (c == null) break;
                    // the one-offs carrying just a string
                    if (m.String2.Length > 0 && m.Param0 == 0 && m.Param1 == 0) Push(s, MessageString2(m.String2, m.Message));
                    else Push(s, MessageFor(c, m.Param0, m.Param1, m.Message));
                    break;
                }
                case MessageSystem m:
                {
                    var (c, s) = CharById(m.RecipientId);
                    if (c != null) Push(s, MessageFor(c, m.Param0, m.Param1, m.Message));
                    break;
                }
                case EntityInformationRequest m:
                    OnEntityInformationRequest(m);
                    break;
                case EntityInformationResponse m:
                    if (m.Warp) WarpByIpc(m.RequesterId, (ushort)m.ZoneId, m.X, m.Y, m.Z, m.Rot, m.MoghouseId);
                    break;
                case SendPlayerToLocation m:
                    WarpByIpc(m.TargetId, (ushort)m.ZoneId, m.X, m.Y, m.Z, m.Rot, m.MoghouseId);
                    break;
                case GMCallResponse m:
                {
                    var (c, s) = CharById(m.CharId);
                    if (c != null) SendPendingGmResponse(s, c);
                    break;
                }
                case ChatMessageAssist m:
                {
                    // Data: the mastery rank (mentors only) low, the mentor rank high
                    var p = ChatStd((byte)m.MessageTypeField, m.SenderName, (ushort)((m.MentorRank != 0 ? m.MasteryRank : 0) | m.MentorRank << 8),
                                    m.GmLevel >= 3, CborRaw.Bytes(m.Message));
                    foreach (var (c, s) in charSessions_)
                        if (s.InZone && c.Id != m.SenderId && AreaOf(c).CanUseMisc("assist") && IsAssistChannelEligible(c))
                            Push(s, new BasicPacket(p.Buffer));
                    break;
                }
                case AssistChannelEvent m:
                    OnAssistChannelEvent(m);
                    break;
                case ConquestEvent m:
                    ConquestHandleMessage(m);
                    break;
                case CharVarUpdate m:
                {
                    // CCharEntity::updateCharVarCache; the sender wrote char_vars, a Quest[..] / Mission[..] var's row
                    // copy follows here, where the char is
                    var (c, _) = CharById(m.CharId);
                    if (c != null)
                    {
                        c.CharVars[m.VarName] = (m.Value, m.Expiry);
                        if (XI.Quests.CharProgress.IsProgressVar(m.VarName))
                            PersistProgress(c, () => true, p => p.SetVar(m.VarName, m.Value, m.Expiry));
                    }
                    break;
                }
                default:
                    if (!OnLinkshellIpc(message)) OnPartyIpc(message);
                    break;
            }
        }

        // ---- !commands ----------------------------------------------------------------------------------------------

        // CCommandHandler::call: xi.commands[name].onTrigger(player, args...) when the char's GM level reaches the
        // command's cmdprops.permission. False (Failure) when there is no such command, it is out of reach, or it
        // errors.
        private bool CallCommand(CharData c, byte[] commandline)
        {
            if (lua_ == null) return false;
            // byte indices: the line as Latin-1
            string line = CborRaw.FromBytes(commandline);
            int pos = 0;
            void TrimLeft() { while (pos < line.Length && (line[pos] == ' ' || line[pos] == '\t')) pos++; }
            string PopToken()
            {
                int end = line.IndexOfAny(new[] { ' ', '\t' }, pos);
                string token = end < 0 ? line[pos..] : line[pos..end];
                pos = end < 0 ? line.Length : end + 1;
                return token;
            }

            TrimLeft();
            if (pos >= line.Length)
            {
                log_("cmdhandler::call: function name was empty");
                return false;
            }
            string cmdName = PopToken();

            int top = lua_.Top;
            try
            {
                lua_.GetPath("xi.commands");
                if (lua_.IsTable(-1))
                {
                    lua_.PushBytes(CborRaw.Bytes(cmdName));
                    lua_.GetTable(-2);
                }
                if (!lua_.IsTable(-1))
                {
                    log_($"cmdhandler::call: Function does not exist ({cmdName})");
                    return false;
                }
                int cmd = lua_.Top;
                lua_.GetField(cmd, "cmdprops");
                if (!lua_.IsTable(-1))
                {
                    log_($"cmdhandler::call: ({cmdName}): Undefined 'cmdprops' table");
                    return false;
                }
                lua_.GetField(-1, "permission");
                lua_.GetField(-2, "parameters");
                if (!lua_.IsNumber(-2) || !lua_.IsString(-1))
                {
                    log_($"cmdhandler::call: ({cmdName}): Invalid or missing permission/parameters in cmdprops");
                    return false;
                }
                int permission = (sbyte)lua_.ToIntegral(-2); // get<sol::optional<int8>>
                string parameters = lua_.ToStringValue(-1);

                if (permission > c.AccountGmLevel)
                {
                    log_($"cmdhandler::call: Character {c.Name} attempting to use higher permission command {cmdName}");
                    return false;
                }

                // audit_gm: commands at or above map.AUDIT_GM_CMD (autotranslate phrases are left as their bytes)
                int auditLevel = (byte)ChatCfg.AuditGmCmd;
                if (auditLevel <= permission && auditLevel > 0)
                    Exec("INSERT into audit_gm (date_time, gm_name, command, full_string) VALUES(CURRENT_TIMESTAMP(3), ?, ?, ?)",
                         BoundValue.Of(c.Name), BoundValue.Of(cmdName), BoundValue.Of(line));

                lua_.GetField(cmd, "onTrigger");
                if (!lua_.IsFunction(-1))
                {
                    log_($"cmdhandler::call: ({cmdName}) missing onTrigger function");
                    return false;
                }

                lua_.PushObject(c);
                int nargs = 1;
                foreach (char paramType in parameters)
                {
                    if (paramType == 'b')
                    {
                        lua_.PushBytes(commandline);
                        nargs++;
                        continue;
                    }
                    TrimLeft();
                    if (pos >= line.Length) break;

                    if (paramType == 's')
                    {
                        if (parameters.Length == 1)
                        {
                            lua_.PushBytes(CborRaw.Bytes(line[pos..]));
                            nargs++;
                            pos = line.Length;
                            break;
                        }
                        lua_.PushBytes(CborRaw.Bytes(PopToken()));
                        nargs++;
                    }
                    else if (paramType == 'i')
                    {
                        lua_.Push((double)FromCharsInt(PopToken()));
                        nargs++;
                    }
                    else if (paramType == 'd')
                    {
                        lua_.Push(FromCharsDouble(PopToken()));
                        nargs++;
                    }
                    else log_($"cmdhandler::call: ({cmdName}) undefined type for param: symbol: {paramType}");
                }
                lua_.Call(nargs, 0);
                return true;
            }
            catch (LuaException e)
            {
                log_($"cmdhandler::call: ({cmdName}) error: {e.Message}");
                return false;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // std::from_chars into an int: the leading [-]digits, 0 when there are none or they overflow
        private static int FromCharsInt(string token)
        {
            var m = Regex.Match(token, @"^-?[0-9]+");
            return m.Success && int.TryParse(m.Value, System.Globalization.NumberStyles.AllowLeadingSign,
                System.Globalization.CultureInfo.InvariantCulture, out int v) ? v : 0;
        }

        // std::from_chars into a double (chars_format::general): the leading decimal, 0 when there is none
        private static double FromCharsDouble(string token)
        {
            var m = Regex.Match(token, @"^-?(?:[0-9]+\.?[0-9]*|\.[0-9]+)(?:[eE][+-]?[0-9]+)?");
            if (!m.Success)
            {
                var special = Regex.Match(token, @"^-?(?:inf(?:inity)?|nan)", RegexOptions.IgnoreCase);
                if (!special.Success) return 0;
                bool neg = special.Value.StartsWith('-');
                return special.Value.Contains('n', StringComparison.OrdinalIgnoreCase) && special.Value.TrimStart('-').StartsWith("n", StringComparison.OrdinalIgnoreCase)
                    ? double.NaN : neg ? double.NegativeInfinity : double.PositiveInfinity;
            }
            return double.TryParse(m.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : 0;
        }

        // ---- Lua ----------------------------------------------------------------------------------------------------

        private void RegisterChatBindings(Dictionary<string, XI.Script.LuaFunction> m)
        {
            // printToPlayer(message, messageType = MESSAGE_SYSTEM_1, name = "")
            m["printToPlayer"] = l =>
            {
                var c = Pc(l, "printToPlayer");
                if (c == null) return 0;
                byte type = l.IsNil(3) ? ChatSystem1 : (byte)l.ToEnumArg(3); // CHAT_MESSAGE_TYPE
                string name = l.IsNil(4) ? "" : CborRaw.FromBytes(l.ToBytes(4));
                PushTo(c, ChatStd(c, type, ArgBytes(l, 2), name));
                return 0;
            };
            m["printToArea"] = PrintToArea;
        }

        // a const std::string& argument: its raw bytes (a number as its text); anything else is a Lua error
        private static byte[] ArgBytes(LuaState l, int idx)
        {
            l.ToStringArg(idx);
            return l.ToBytes(idx);
        }

        // ChatMessageArea
        private const int AreaSystem = 0, AreaSay = 1, AreaShout = 2, AreaParty = 3, AreaYell = 4, AreaUnity = 5;

        // printToArea(message, messageLook = MESSAGE_SYSTEM_1, range = System, name = "", skipSender = false)
        private int PrintToArea(LuaState l)
        {
            var c = Pc(l, "printToArea");
            if (c == null) return 0;
            var message = ArgBytes(l, 2);
            byte look = l.IsNil(3) ? ChatSystem1 : (byte)l.ToEnumArg(3); // CHAT_MESSAGE_TYPE
            int range = l.IsNil(4) ? AreaSystem : l.ToEnumArg(4); // ChatMessageArea
            string name = l.IsNil(5) ? "" : CborRaw.FromBytes(l.ToBytes(5));
            bool skipSender = !l.IsNil(6) && l.ToBoolean(6);
            string msg = CborRaw.FromBytes(message);

            switch (range)
            {
                case AreaSystem:
                    SendIpc(new ChatMessageServerMessage
                    {
                        SenderId = c.Id, SenderName = name, Message = msg, MessageTypeField = (ChatMessageType)look, SkipSender = skipSender,
                    });
                    break;
                case AreaSay:
                    PushCharInRange(c, ChatStd(c, look, message, name), true);
                    break;
                case AreaShout:
                    PushCharInShout(c, ChatStd(c, look, message, name));
                    PushTo(c, ChatStd(c, look, message, name));
                    break;
                case AreaParty:
                    if (c.Party?.Alliance != null)
                        SendIpc(new ChatMessageAlliance
                        {
                            AllianceId = c.Party.Alliance.Id, SenderId = c.Id, SenderName = name, Message = msg, MessageTypeField = (ChatMessageType)look,
                        });
                    else if (c.Party != null)
                        SendIpc(new ChatMessageParty
                        {
                            PartyId = c.Party.Id, SenderId = c.Id, SenderName = name, Message = msg, MessageTypeField = (ChatMessageType)look,
                        });
                    break;
                case AreaYell:
                    SendIpc(new ChatMessageYell { SenderId = c.Id, SenderName = name, Message = msg, MessageTypeField = (ChatMessageType)look });
                    PushCharInRange(c, ChatStd(c, look, message, name), true);
                    break;
                case AreaUnity:
                    SendIpc(new ChatMessageUnity
                    {
                        UnityLeaderId = c.Id, SenderId = c.Id, SenderName = name, Message = msg, MessageTypeField = (ChatMessageType)look,
                    });
                    break;
            }
            return 0;
        }
    }
}
