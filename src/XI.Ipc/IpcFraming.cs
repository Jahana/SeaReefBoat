// C# mirror of src/common/ipc.h wire framing.
//
//   toBytesWithHeader:   bytes = cbor(payload); bytes.insert(begin, (uint8)MessageType)
//   fromBytesWithHeader: type = message[0]; if type != expected -> nullopt; else cbor(message[1..])
//
// So the on-wire form is: [ 1 raw MessageType byte ][ glaze CBOR map ]. NOT a CBOR
// self-describing envelope -- the type byte is a bare prefix outside the CBOR document.
using System;
using System.Collections.Generic;
using System.Formats.Cbor;

namespace XI.Ipc
{
    public static class IpcFraming
    {
        // Serialize a message to [type byte][CBOR].
        public static byte[] ToBytesWithHeader(IIpcMessage message)
        {
            var w = new CborWriter(CborConformanceMode.Lax);
            message.WriteBody(w);
            byte[] payload = w.Encode();

            byte[] framed = new byte[payload.Length + 1];
            framed[0] = (byte)message.Type;
            Buffer.BlockCopy(payload, 0, framed, 1, payload.Length);
            return framed;
        }

        // CBOR-only payload (no type prefix), == ipc::toBytes.
        public static byte[] ToBytes(IIpcMessage message)
        {
            var w = new CborWriter(CborConformanceMode.Lax);
            message.WriteBody(w);
            return w.Encode();
        }

        // Decode [type byte][CBOR] into a message of the expected type.
        // Returns null on empty, type mismatch, or a CBOR error -- mirroring Maybe<T>.
        public static IIpcMessage FromBytesWithHeader(ReadOnlySpan<byte> framed, MessageType expected, out byte[] payload)
        {
            payload = null;
            if (framed.Length < 1) return null;
            var type = (MessageType)framed[0];
            if (type != expected) return null;

            var body = framed.Slice(1).ToArray();
            try
            {
                var msg = Create(expected);
                if (msg == null) return null;
                var r = new CborReader(body, CborConformanceMode.Lax);
                msg.ReadBody(r);
                payload = body;
                return msg;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        public static IIpcMessage FromBytes(ReadOnlySpan<byte> body, MessageType type)
        {
            if (body.Length < 1) return null;
            try
            {
                var msg = Create(type);
                if (msg == null) return null;
                var r = new CborReader(body.ToArray(), CborConformanceMode.Lax);
                msg.ReadBody(r);
                return msg;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        // Mirror of the generated IPCMessageHandlerBase switch: dispatch on message[0].
        // Returns null for an unknown/unregistered type (== handleUnknownMessage).
        public static IIpcMessage Decode(ReadOnlySpan<byte> framed)
        {
            if (framed.Length < 1) return null;
            return FromBytes(framed.Slice(1), (MessageType)framed[0]);
        }

        public static bool IsRegistered(MessageType type) => Create(type) != null;

        static IIpcMessage Create(MessageType type)
        {
            switch (type)
            {
                case MessageType.EmptyStruct: return new EmptyStruct();
                case MessageType.AccountLogin: return new AccountLogin();
                case MessageType.CharZone: return new CharZone();
                case MessageType.CharVarUpdate: return new CharVarUpdate();
                case MessageType.PartyReload: return new PartyReload();
                case MessageType.PlayerKick: return new PlayerKick();
                case MessageType.EntityInformationRequest: return new EntityInformationRequest();
                case MessageType.EntityInformationResponse: return new EntityInformationResponse();
                case MessageType.SendPlayerToLocation: return new SendPlayerToLocation();
                case MessageType.ChatMessageTell: return new ChatMessageTell();
                case MessageType.ChatMessageParty: return new ChatMessageParty();
                case MessageType.ChatMessageAlliance: return new ChatMessageAlliance();
                case MessageType.ChatMessageLinkshell: return new ChatMessageLinkshell();
                case MessageType.ChatMessageUnity: return new ChatMessageUnity();
                case MessageType.ChatMessageYell: return new ChatMessageYell();
                case MessageType.ChatMessageAssist: return new ChatMessageAssist();
                case MessageType.ChatMessageServerMessage: return new ChatMessageServerMessage();
                case MessageType.ChatMessageCustom: return new ChatMessageCustom();
                case MessageType.PartyInvite: return new PartyInvite();
                case MessageType.PartyInviteResponse: return new PartyInviteResponse();
                case MessageType.PartyRequest: return new PartyRequest();
                case MessageType.PartyDisband: return new PartyDisband();
                case MessageType.AllianceReload: return new AllianceReload();
                case MessageType.AllianceDissolve: return new AllianceDissolve();
                case MessageType.MessageStandard: return new MessageStandard();
                case MessageType.MessageSystem: return new MessageSystem();
                case MessageType.LinkshellRankChange: return new LinkshellRankChange();
                case MessageType.LinkshellRemove: return new LinkshellRemove();
                case MessageType.LinkshellSetMessage: return new LinkshellSetMessage();
                case MessageType.LuaFunction: return new LuaFunction();
                case MessageType.KillSession: return new KillSession();
                case MessageType.AssistChannelEvent: return new AssistChannelEvent();
                case MessageType.ConquestEvent: return new ConquestEvent();
                case MessageType.BesiegedEvent: return new BesiegedEvent();
                case MessageType.CampaignEvent: return new CampaignEvent();
                case MessageType.ColonizationEvent: return new ColonizationEvent();
                case MessageType.GMCallRequest: return new GMCallRequest();
                case MessageType.GMCallResponse: return new GMCallResponse();
                default: return null;
            }
        }
    }
}
