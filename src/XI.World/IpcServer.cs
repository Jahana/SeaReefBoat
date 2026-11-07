// Port of world/ipc_server.{h,cpp}: the world router. Map processes send framed IPC messages here; each is
// looked up against accounts_sessions / accounts_parties / the zone table and forwarded to the owning map
// process(es). The ZMQ router socket is behind IWorldChannel.

using System;
using System.Collections.Generic;
using XI.Ipc;
using XI.Login;
using XI.Persistence;

namespace XI.World
{
    public readonly struct IppPayload
    {
        public Ipp Ipp { get; }
        public byte[] Payload { get; }
        public IppPayload(Ipp ipp, byte[] payload) { Ipp = ipp; Payload = payload; }
    }

    // The conquest / besieged / campaign / colonization systems: each takes the sub-type byte and the opaque payload.
    public interface IWorldSystem
    {
        void HandleMessage(byte type, IppPayload payload);
    }

    public sealed class NullWorldSystem : IWorldSystem
    {
        public void HandleMessage(byte type, IppPayload payload) { }
    }

    // The slice of WorldEngine the IPC server reaches into.
    public sealed class WorldSystems
    {
        public IWorldSystem Conquest = new NullWorldSystem();
        public IWorldSystem Besieged = new NullWorldSystem();
        public IWorldSystem Campaign = new NullWorldSystem();
        public IWorldSystem Colonization = new NullWorldSystem();
    }

    public sealed class IpcServer
    {
        public const ushort ZoneNoDestination = 0xFFFF;
        private const ushort MsgStdTellNotReceivedOffline = 125; // MsgStd::TellNotReceivedOffline

        private readonly IDatabase _db;
        private readonly IWorldChannel _channel;
        private readonly WorldSystems _systems;
        private readonly Action<string> _log;

        public CharacterCache CharacterCache { get; } = new();
        public ZoneSettings ZoneSettings { get; }

        public IpcServer(IDatabase db, IWorldChannel channel, ZoneSettings zoneSettings, WorldSystems systems, Action<string> log = null)
        {
            _db = db;
            _channel = channel;
            ZoneSettings = zoneSettings;
            _systems = systems;
            _log = log ?? (_ => { });
        }

        //
        // Sending
        //

        public void SendMessage(Ipp ipp, IIpcMessage message) =>
            _channel.Send(new IppMessage(ipp, IpcFraming.ToBytesWithHeader(message)));

        public void BroadcastMessage(IIpcMessage message)
        {
            foreach (var ipp in ZoneSettings.MapEndpoints) SendMessage(ipp, message);
        }

        //
        // IPP lookup
        //

        public Ipp? GetIppForCharId(uint charId)
        {
            var rset = LoginDb.PreparedStmt(_db, "SELECT server_addr, server_port FROM accounts_sessions WHERE charid = ? LIMIT 1", charId);
            if (rset != null && rset.RowsCount() != 0 && rset.Next())
                return new Ipp(rset.Get<uint>("server_addr"), rset.Get<ushort>("server_port"));
            return null;
        }

        public Ipp? GetIppForCharName(string charName)
        {
            var rset = LoginDb.PreparedStmt(_db, "SELECT server_addr, server_port FROM accounts_sessions LEFT JOIN chars ON " +
                                                 "accounts_sessions.charid = chars.charid WHERE charname = ? LIMIT 1", charName);
            if (rset != null && rset.RowsCount() != 0 && rset.Next())
                return new Ipp(rset.Get<uint>("server_addr"), rset.Get<ushort>("server_port"));
            return null;
        }

        public Ipp? GetIppForZoneId(ushort zoneId) =>
            ZoneSettings.ZoneSettingsMap.TryGetValue(zoneId, out var e) ? e.Ipp : null;

        private List<Ipp> QueryIpps(string query, params object[] args)
        {
            var list = new List<Ipp>();
            var rset = LoginDb.PreparedStmt(_db, query, args);
            if (rset != null && rset.RowsCount() != 0)
                while (rset.Next()) list.Add(new Ipp(rset.Get<uint>("server_addr"), rset.Get<ushort>("server_port")));
            return list;
        }

        public List<Ipp> GetIppsForParty(uint partyId) => QueryIpps(
            "SELECT server_addr, server_port, MIN(charid) FROM accounts_sessions JOIN accounts_parties USING (charid) " +
            "WHERE (allianceid <> 0 AND allianceid = (SELECT MAX(allianceid) FROM accounts_parties WHERE partyid = ?)) " +
            "OR (allianceid = 0 AND partyid = ?) GROUP BY server_addr, server_port", partyId, partyId);

        public List<Ipp> GetIppsForAlliance(uint allianceId) => QueryIpps(
            "SELECT server_addr, server_port, MIN(charid) FROM accounts_sessions JOIN accounts_parties USING (charid) " +
            "WHERE allianceid = ? GROUP BY server_addr, server_port", allianceId);

        public List<Ipp> GetIppsForLinkshell(uint linkshellId) => QueryIpps(
            "SELECT server_addr, server_port FROM accounts_sessions " +
            "WHERE linkshellid1 = ? OR linkshellid2 = ? GROUP BY server_addr, server_port", linkshellId, linkshellId);

        public List<Ipp> GetIppsForUnity(uint unityId) => QueryIpps(
            "SELECT server_addr, server_port FROM accounts_sessions " +
            "WHERE unitychat = ? GROUP BY server_addr, server_port", unityId);

        public List<Ipp> GetIppsForYellZones() => ZoneSettings.YellMapEndpoints;
        public List<Ipp> GetIppsForAssistZones() => ZoneSettings.AssistMapEndpoints;
        public List<Ipp> GetIppsForAllZones() => ZoneSettings.MapEndpoints;

        //
        // Message routing
        //

        public void RerouteMessageToCharId(uint charId, IIpcMessage message)
        {
            var ipp = GetIppForCharId(charId);
            if (ipp != null) SendMessage(ipp.Value, message);
        }

        public void RerouteMessageToCharName(string charName, IIpcMessage message)
        {
            var ipp = GetIppForCharName(charName);
            if (ipp != null) SendMessage(ipp.Value, message);
        }

        public void RerouteMessageToZoneId(ushort zoneId, IIpcMessage message)
        {
            var ipp = GetIppForZoneId(zoneId);
            if (ipp != null) SendMessage(ipp.Value, message);
        }

        private void SendToAll(IEnumerable<Ipp> ipps, IIpcMessage message)
        {
            foreach (var ipp in ipps) SendMessage(ipp, message);
        }

        public void RerouteMessageToPartyMembers(uint partyId, IIpcMessage m) => SendToAll(GetIppsForParty(partyId), m);
        public void RerouteMessageToAllianceMembers(uint allianceId, IIpcMessage m) => SendToAll(GetIppsForAlliance(allianceId), m);
        public void RerouteMessageToLinkshellMembers(uint linkshellId, IIpcMessage m) => SendToAll(GetIppsForLinkshell(linkshellId), m);
        public void RerouteMessageToUnityMembers(uint unityId, IIpcMessage m) => SendToAll(GetIppsForUnity(unityId), m);
        public void RerouteMessageToYellZones(IIpcMessage m) => SendToAll(GetIppsForYellZones(), m);
        public void RerouteMessageToAssistZones(IIpcMessage m) => SendToAll(GetIppsForAssistZones(), m);
        public void RerouteMessageToAllZones(IIpcMessage m) => SendToAll(GetIppsForAllZones(), m);

        //
        // Incoming
        //

        public void HandleIncomingMessages()
        {
            while (_channel.TryReceive(out var message))
            {
                if (message.Payload == null || message.Payload.Length == 0) continue;
                HandleMessage(message.Ipp, message.Payload);
            }
        }

        // ipc::IPCMessageHandlerBase::handleMessage: dispatch on message[0].
        public void HandleMessage(Ipp ipp, byte[] framed)
        {
            var message = IpcFraming.Decode(framed);
            switch (message)
            {
                case EmptyStruct: _log($"Received EmptyStruct message from {ipp} - this is probably a bug"); break;
                case AccountLogin m: HandleAccountLogin(ipp, m); break;
                case CharZone m: HandleCharZone(ipp, m); break;
                case CharVarUpdate m: RerouteMessageToCharId(m.CharId, m); break;
                case ChatMessageTell m: HandleChatMessageTell(ipp, m); break;
                case ChatMessageParty m: RerouteMessageToPartyMembers(m.PartyId, m); break;
                case ChatMessageAlliance m: RerouteMessageToAllianceMembers(m.AllianceId, m); break;
                case ChatMessageLinkshell m: RerouteMessageToLinkshellMembers(m.LinkshellId, m); break;
                case ChatMessageUnity m: RerouteMessageToUnityMembers(m.UnityLeaderId, m); break;
                case ChatMessageYell m: RerouteMessageToYellZones(m); break;
                case ChatMessageAssist m: RerouteMessageToAssistZones(m); break;
                case ChatMessageServerMessage m: RerouteMessageToAllZones(m); break;
                case ChatMessageCustom m: RerouteMessageToCharId(m.RecipientId, m); break;
                case PartyInvite m: RerouteMessageToCharId(m.InviteeId, m); break;
                case PartyInviteResponse m: RerouteMessageToCharId(m.InviterId, m); break;
                case PartyRequest m: RerouteMessageToCharId(m.RecipientId, m); break;
                case PartyReload m: RerouteMessageToPartyMembers(m.PartyId, m); break;
                case PartyDisband m: RerouteMessageToPartyMembers(m.PartyId, m); break;
                case AllianceReload m: RerouteMessageToAllianceMembers(m.AllianceId, m); break;
                case AllianceDissolve m: RerouteMessageToAllianceMembers(m.AllianceId, m); break;
                case PlayerKick m: RerouteMessageToCharId(m.VictimId, m); break;
                case MessageStandard m: RerouteMessageToCharId(m.RecipientId, m); break;
                case MessageSystem m: RerouteMessageToCharId(m.RecipientId, m); break;
                case LinkshellRankChange m: RerouteMessageToCharName(m.MemberName, m); break;
                case LinkshellRemove m: RerouteMessageToCharName(m.VictimName, m); break;
                case LinkshellSetMessage m: RerouteMessageToLinkshellMembers(m.LinkshellId, m); break;
                case LuaFunction m: RerouteMessageToZoneId((ushort)m.ExecutorZoneId, m); break;
                case KillSession m: HandleKillSession(ipp, m); break;
                case ConquestEvent m: _systems.Conquest.HandleMessage(m.TypeField, new IppPayload(ipp, m.Payload)); break;
                case BesiegedEvent m: _systems.Besieged.HandleMessage(m.TypeField, new IppPayload(ipp, m.Payload)); break;
                case CampaignEvent m: _systems.Campaign.HandleMessage(m.TypeField, new IppPayload(ipp, m.Payload)); break;
                case ColonizationEvent m: _systems.Colonization.HandleMessage(m.TypeField, new IppPayload(ipp, m.Payload)); break;
                case EntityInformationRequest m: HandleEntityInformationRequest(m); break;
                case EntityInformationResponse m: RerouteMessageToCharId(m.RequesterId, m); break;
                case SendPlayerToLocation m: RerouteMessageToCharId(m.TargetId, m); break;
                case AssistChannelEvent m: RerouteMessageToCharId(m.ReceiverId, m); break;
                case GMCallRequest m: HandleGmCallRequest(m); break;
                case GMCallResponse m: HandleGmCallResponse(m); break;
                default:
                    _log($"Received unknown message from {ipp} with code {framed[0]} and size {framed.Length}");
                    break;
            }
        }

        private void HandleAccountLogin(Ipp ipp, AccountLogin message)
        {
            // Forwarded to every zone process (C++ logs the sender's address for each hop).
            foreach (var zoneIpp in GetIppsForAllZones()) SendMessage(zoneIpp, message);
        }

        private void HandleCharZone(Ipp ipp, CharZone message)
        {
            if ((ushort)message.DestinationZoneId == ZoneNoDestination)
            {
                CharacterCache.RemoveCharacter(message.CharId);
                return;
            }

            var destination = GetIppForZoneId((ushort)message.DestinationZoneId);
            if (destination != null)
            {
                CharacterCache.UpdateCharacter(message.CharId, destination.Value);
                RerouteMessageToZoneId((ushort)message.DestinationZoneId, message);
            }
        }

        private void HandleChatMessageTell(Ipp ipp, ChatMessageTell message)
        {
            var charIpp = GetIppForCharName(message.RecipientName);
            if (charIpp == null)
            {
                SendMessage(ipp, new MessageStandard { RecipientId = message.SenderId, Message = MsgStdTellNotReceivedOffline });
                return;
            }

            SendMessage(charIpp.Value, message);
        }

        private void HandleKillSession(Ipp ipp, KillSession message)
        {
            var rset = LoginDb.PreparedStmt(_db, "SELECT pos_prevzone, pos_zone from chars where charid = ? LIMIT 1", message.VictimId);

            // Zone ids from the query: try to send to _just_ the previous zone
            if (rset != null && rset.RowsCount() != 0 && rset.Next())
            {
                var prev = rset.Get<ushort>("pos_prevzone");
                var next = rset.Get<ushort>("pos_zone");
                if (prev != next) SendMessage(ZoneSettings.ZoneSettingsMap[prev].Ipp, message); // .at(): throws on an unknown zone
            }
            else // otherwise, every zone
            {
                foreach (var endpoint in ZoneSettings.MapEndpoints) SendMessage(endpoint, message);
            }
        }

        private void HandleEntityInformationRequest(EntityInformationRequest message)
        {
            // ENTITYTYPE TYPE_PC = 0x01, TYPE_NPC = 0x02, TYPE_MOB = 0x04
            if (message.EntityType == 0x01)
            {
                RerouteMessageToCharId(message.TargetId, message);
            }
            else
            {
                RerouteMessageToZoneId((ushort)((message.TargetId >> 12) & 0x0FFF), message);
            }
        }

        private void HandleGmCallRequest(GMCallRequest message)
        {
            _log($"GM Call #{message.CallId} from {message.CharName} (charId: {message.CharId}, accId: {message.AccId}, " +
                 $"zone: {(ushort)message.ZoneId}): {message.Message}");
            // TODO (C++ too): route this to external clients
        }

        private void HandleGmCallResponse(GMCallResponse message)
        {
            // Client can only read up to 1024 characters, drop any extra now.
            if (message.Message.Length > 1024) message.Message = message.Message.Substring(0, 1024);

            LoginDb.PreparedStmt(_db, "UPDATE help_desk SET response = ?, responded_at = NOW() WHERE id = ?", message.Message, message.CallId);

            var ipp = GetIppForCharId(message.CharId);
            if (ipp != null) SendMessage(ipp.Value, message);
        }
    }

    // In-process IWorldChannel: Receive() side is fed with Inject, Send() side is recorded in Sent.
    public sealed class QueueWorldChannel : IWorldChannel
    {
        private readonly Queue<IppMessage> _incoming = new();
        public List<IppMessage> Sent { get; } = new();

        public void Inject(Ipp from, byte[] payload) => _incoming.Enqueue(new IppMessage(from, payload));

        public void Send(IppMessage message) => Sent.Add(message);

        public bool TryReceive(out IppMessage message)
        {
            if (_incoming.Count > 0) { message = _incoming.Dequeue(); return true; }
            message = default;
            return false;
        }
    }
}
