// src/profile/protocol/irc/notices.cpp: the IRC notice payloads (friend status, profile available, message notices)
// and the message name parser.

using System;
using System.Collections.Generic;

namespace XI.Profile
{
    // the client drops notices older than the last one
    public struct FriendStatusNotice
    {
        public uint FriendAccountId;
        public byte ListIndex;          // where the recipient lists the friend
        public byte Status;             // 0 offline, 1 online, 2 away
        public ulong Timestamp;
        public uint? ActiveCharacterId; // lets the client look up world, zone and job
    }

    // only types a client may send
    public sealed class MessageName
    {
        public uint Sender;
        public uint Recipient;
        public MessageType Type;
        public byte[] SenderName;
        public uint Length;
    }

    public static class ProfileNotices
    {
        private const ulong MessagePolIdMask = 0x1C273E4567891133ul; // XORed into both POL ids of a message header
        private const byte ExtendedStatus = 0x01;                    // apply the notice's status fields
        private const byte ExtendedCharacter = 0x08;                 // a character slot follows
        private const byte NoHandle = 0xFF;

        private static byte[] DecodeHeader(string name)
        {
            var bytes = ProfileCodec.Base64Decode(name);
            if (bytes == null || bytes.Length != SqPolMessageHeader.Size) return null;
            return bytes;
        }

        private static bool IsClientMessage(MessageType type) =>
            type is MessageType.Plain or MessageType.FriendRequest or MessageType.FriendAccepted or MessageType.FriendDeclined;

        // friendStatusNotice
        public static string FriendStatus(FriendStatusNotice notice)
        {
            bool connected = notice.Status != 0;
            bool playing = connected && notice.ActiveCharacterId.HasValue;

            var payload = new List<byte>();
            var extended = new byte[FriendStatExtended.Size];
            extended[0] = ExtendedStatus;
            if (playing) extended[0] |= ExtendedCharacter;
            payload.AddRange(extended);
            if (playing)
            {
                uint id = notice.ActiveCharacterId.Value;
                payload.AddRange(SqPolCharacterPrimitive.Make(Content.FFXI, ProfileCodec.CharacterKey(id), id));
            }
            string text = ProfileCodec.Base64EncodeUnpadded(payload.ToArray());

            var header = new FriendStatNotice
            {
                SenderPolId = notice.FriendAccountId ^ MessagePolIdMask,
                Status = (byte)(notice.Status + 1),
                Active = (byte)(playing ? 1 : 0),
                HasExtended = 1,
                ListIndex = notice.ListIndex,
                SystemAgeOfData = notice.Timestamp,
                Length = (uint)(text.Length + 1),
                Type = MessageType.FriendStatus,
                BIsOnline = 1,
                BValid = 1,
                BPolproRequest = 1,
            };
            if (connected)
            {
                header.Connected = 2;
                header.ContentsClass = Content.FFXI;
            }
            return ProfileCodec.Base64Encode(header.ToBytes()) + text;
        }

        // profileAvailableNotice: polcore waits for this before finishing login. The slot keeps clients behind the same
        // nat on different game ports.
        public static string ProfileAvailable(ushort udpPortSlot)
        {
            var header = new FriendStatNotice
            {
                SenderPolId = MessagePolIdMask,
                ReceiverPolId = MessagePolIdMask,
                Connected = 1,
                HandleNumber = NoHandle,
                LegacyBlock = 1,
                ListIndex = NoHandle,
                UdpPortSlot = udpPortSlot,
                Type = MessageType.FriendStatus,
                BPolproRequest = 1,
            };
            return ProfileCodec.Base64Encode(header.ToBytes());
        }

        // parseMessageName
        public static MessageName ParseMessageName(string name)
        {
            var header = DecodeHeader(name);
            if (header == null) return null;

            ulong sender = Le.U64(header, SqPolMessageHeader.SenderPolId) ^ MessagePolIdMask;
            ulong recipient = Le.U64(header, SqPolMessageHeader.ReceiverPolId) ^ MessagePolIdMask;
            var type = SqPolMessageHeader.Type(header);
            // POL ids are account ids, and clients can't forge online notices
            if (sender > 0xFFFFFFFF || recipient > 0xFFFFFFFF || !IsClientMessage(type) || SqPolMessageHeader.BPolproRequest(header) != 0) return null;

            return new MessageName
            {
                Sender = (uint)sender,
                Recipient = (uint)recipient,
                Type = type,
                SenderName = ProfileCodec.CString(header.AsSpan(SqPolMessageHeader.SenderName, 16)),
                Length = Le.U32(header, SqPolMessageHeader.Length),
            };
        }

        // messageNotice: the message name with the online-notice bit set
        public static string MessageNotice(string name)
        {
            var header = DecodeHeader(name);
            if (header == null) return name;
            SqPolMessageHeader.SetBPolproRequest(header);
            return ProfileCodec.Base64Encode(header);
        }
    }
}
