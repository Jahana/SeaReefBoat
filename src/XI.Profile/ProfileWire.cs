// src/profile/protocol/*: the profile protocol's packed wire objects (#pragma pack(1), little endian), as byte arrays
// with their field offsets. Bit fields follow MSVC's layout: consecutive fields of one type share a unit, low bit
// first.

using System;
using System.Buffers.Binary;

namespace XI.Profile
{
    internal static class Le
    {
        public static ushort U16(ReadOnlySpan<byte> b, int at) => BinaryPrimitives.ReadUInt16LittleEndian(b.Slice(at));
        public static uint U32(ReadOnlySpan<byte> b, int at) => BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(at));
        public static ulong U64(ReadOnlySpan<byte> b, int at) => BinaryPrimitives.ReadUInt64LittleEndian(b.Slice(at));
        public static void U16(Span<byte> b, int at, ushort v) => BinaryPrimitives.WriteUInt16LittleEndian(b.Slice(at), v);
        public static void U32(Span<byte> b, int at, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(b.Slice(at), v);
        public static void U64(Span<byte> b, int at, ulong v) => BinaryPrimitives.WriteUInt64LittleEndian(b.Slice(at), v);
    }

    // c2s/prof_open_data.h ProfOpenData: opens a profile connection (MakeOpenData)
    public static class ProfOpenData
    {
        public const int Size = 40;
    }

    // s2c/prof_open_ans.h ProfOpenAns: the ProfOpenData answer (__sqProfOpen)
    public static class ProfOpenAns
    {
        public const int Size = 24;

        public static byte[] Make(byte code, uint sessionValue)
        {
            var b = new byte[Size];
            b[0] = code;
            Le.U32(b, 20, sessionValue); // folded into every request signature on this connection
            return b;
        }
    }

    // c2s/prof_send_req_data.h ProfSendReqData: the request header (MakeSendReqData)
    public readonly struct ProfSendReqData
    {
        public const int Size = 40;

        public readonly byte Kind; // 2
        public readonly byte Command;
        public readonly byte SubCommand;
        public readonly uint BodySize; // bytes of objects the client sends after this header

        public ProfSendReqData(ReadOnlySpan<byte> b)
        {
            Kind = b[0];
            Command = b[1];
            SubCommand = b[2];
            BodySize = Le.U32(b, 4);
        }

        public ProfRequest Request => (ProfRequest)(Command << 8 | SubCommand);
    }

    // s2c/prof_recv_ans.h ProfRecvAns: the answer header (__sqProfRecvAns)
    public static class ProfRecvAns
    {
        public const int Size = 24;

        // code: retail sends 0x83; size: bytes that follow (ReadFile: data length + 4); realIp left 0 (a non-zero one
        // redirects every later session to that address)
        public static byte[] Make(byte code, ProfError error, uint size)
        {
            var b = new byte[Size];
            b[0] = code;
            b[1] = (byte)error;
            Le.U32(b, 4, size);
            return b;
        }
    }

    // s2c/prof_count.h ProfCount: entry count, first object of every list answer
    public static class ProfCount
    {
        public const int Size = 8;

        public static byte[] Make(uint count)
        {
            var b = new byte[Size];
            Le.U32(b, 0, count);
            return b;
        }
    }

    // prof_trailer.h ProfTrailer: StoreFriendList trailer with a checksum
    public static class ProfTrailer
    {
        public const int Size = 8;

        public static byte[] Make(uint unused00)
        {
            var b = new byte[Size];
            Le.U32(b, 0, unused00); // the answer echoes the request's
            return b;
        }
    }

    // sq_pol_character_primitive.h sqPolCharacterPrimitive: character identity, also a slot of FriendInfo and friend
    // status notices
    public static class SqPolCharacterPrimitive
    {
        public const int Size = 16;

        public static byte[] Make(Content contentsClass, uint contentsUserSubId, ulong contentsUserId)
        {
            var b = new byte[Size];
            // bValid : 1, __SystemReserved : 15 stay 0
            Le.U16(b, 2, (ushort)contentsClass);
            Le.U32(b, 4, contentsUserSubId);
            Le.U64(b, 8, contentsUserId); // FFXI: character id
            return b;
        }
    }

    // friend_info.h FriendInfo: friend or block list entry, converted into a sqPolFriend (sqPlayOnlineGetFriendInfo,
    // sqPlayOnlineGetBlackInfo, __sqProfCopyFriendMember). Kept as its bytes so a stored entry echoes what it was sent.
    public sealed class FriendInfo
    {
        public const int Size = 168;

        // FriendInfo store op
        public const byte OpSet = 1;
        public const byte OpDelete = 2;

        public readonly byte[] Bytes;

        public FriendInfo() => Bytes = new byte[Size];

        public FriendInfo(ReadOnlySpan<byte> bytes) => Bytes = bytes.ToArray();

        private ulong Bits
        {
            get => Le.U64(Bytes, 0);
            set => Le.U64(Bytes, 0, value);
        }

        private ulong Get(int shift, int width) => (Bits >> shift) & ((1ul << width) - 1);

        private void Set(int shift, int width, ulong v)
        {
            ulong mask = ((1ul << width) - 1) << shift;
            Bits = (Bits & ~mask) | ((v << shift) & mask);
        }

        public byte Op { get => (byte)Get(0, 4); set => Set(0, 4, value); }                  // friendOpSet or friendOpDelete; ignored on load
        public byte IsBlack { get => (byte)Get(4, 1); set => Set(4, 1, value); }             // BlackList instead of FriendList
        public byte Kind { get => (byte)Get(5, 2); set => Set(5, 2, value); }                // PS2: Level + 1; 0 marks an invalid entry
        public byte HandleNumber { get => (byte)Get(7, 6); set => Set(7, 6, value); }        // PS2: HandleNamePrimitive.Num
        public ulong HandleId { get => Get(13, 44); set => Set(13, 44, value); }             // PS2: HandleNamePrimitive.Id
        public byte Group { get => (byte)Get(57, 4); set => Set(57, 4, value); }
        public byte BTemporary { get => (byte)Get(62, 1); set => Set(62, 1, value); }        // a friend request still pending
        public byte Num { get => Bytes[8]; set => Bytes[8] = value; }                        // index in FriendList (0-199) or BlackList (0-99)
        public byte Order { get => Bytes[9]; set => Bytes[9] = value; }
        public byte Result { get => Bytes[10]; set => Bytes[10] = value; }                   // store answer: 0 = applied
        public ulong PolId { get => Le.U64(Bytes, 16); set => Le.U64(Bytes, 16, value); }    // polpro POL id (common id XOR _BaseKey)
        public Span<byte> HandleName => Bytes.AsSpan(24, 16);                                // PS2: HandleNamePrimitive.Name
        // CharacterPrimitive[8] at 40
    }

    // c2s/0x0206_store_friend_list.h StoreFriendListHead: StoreFriendList request, followed by FriendInfo entries
    // (__sqPlayOnlineStoreFriendListCheck)
    public sealed class StoreFriendListHead
    {
        public const int Size = 304;
        public const int FriendOrderSize = 200;
        public const int BlackOrderSize = 100;

        private readonly byte[] _b;

        public StoreFriendListHead(ReadOnlySpan<byte> b) => _b = b.ToArray();

        public byte FriendOrder(int i) => _b[i];               // FriendList display order
        public byte BlackOrder(int i) => _b[FriendOrderSize + i]; // BlackList display order
        public ushort Count => Le.U16(_b, 300);                // FriendInfo entries that follow
    }

    // c2s/prof_file_head.h ProfFileHead: file request header (__sqProfReadFileCheck, __sqProfWriteFileCheck,
    // __sqProfDeleteFileCheck)
    public readonly struct ProfFileHead
    {
        public const int Size = 408;
        public const int PathSize = 0x180;

        public readonly ulong Target; // polpro POL id; 0 = the requester's own files
        public readonly byte[] Path;  // NUL terminated
        public readonly uint Offset;  // the list calls put a u16 max entries here
        public readonly uint Length;

        public ProfFileHead(ReadOnlySpan<byte> b)
        {
            Target = Le.U64(b, 8);
            Path = ProfileCodec.CString(b.Slice(16, PathSize));
            Offset = Le.U32(b, 400);
            Length = Le.U32(b, 404);
        }
    }

    // ProfFileRequest: a file request with a checksum, for ReadFile and the file lists
    public static class ProfFileRequest
    {
        public const int Size = ProfFileHead.Size + 8;
    }

    // s2c/0x0303_get_file_list.h ProfFileInfo: file list entry (__sqProfGetFileListCheck, sqProfGetMessageListCheck)
    public static class ProfFileInfo
    {
        public const int Size = 264;
        public const int NameSize = 0x100;

        public static byte[] Make(uint time, uint size, ReadOnlySpan<byte> name)
        {
            var b = new byte[Size];
            Le.U32(b, 0, time);
            Le.U32(b, 4, size);
            ProfileCodec.PutName(b.AsSpan(8, NameSize), name);
            return b;
        }
    }

    // c2s/0x010b_search_pol_id.h SearchPolId: SearchPolId request (sqPlayOnlineSearchPolId)
    public static class SearchPolIdReq
    {
        public const int Size = 24;

        public static uint WorldAndId(ReadOnlySpan<byte> b) => Le.U32(b, 8); // FFXI: world << 16 | search id
    }

    // s2c/0x010b_search_pol_id.h SearchPolIdAns: SearchPolId answer (sqPlayOnlineSearchPolIdCheck2)
    public static class SearchPolIdAns
    {
        public const int Size = 48;

        public static byte[] Make(ulong polId, uint handleId, ReadOnlySpan<byte> handleName, byte found)
        {
            var b = new byte[Size];
            Le.U64(b, 0, polId);       // polpro POL id
            Le.U32(b, 8, handleId);    // PS2: (New; did not exist.)
            ProfileCodec.PutName(b.AsSpan(16, 16), handleName);
            b[32] = found;             // 0 = not found
            return b;                  // checksum at 44
        }
    }

    // c2s/0x0405_change_my_status.h ChangeMyStatus: ChangeMyStatus request (sqPlayOnlineChangeMyStatus)
    public readonly struct ChangeMyStatusReq
    {
        public const int Size = 40;

        public readonly byte ActiveHandleNameNum;
        public readonly byte BCharacterFound;       // the active character is in the handle's character table
        public readonly byte ActiveCharacterSlot;   // its slot in that table
        public readonly byte BReceiveOnlineMessage; // retail sends 1
        public readonly byte OpenStat;              // OpenStatus + 1; 0 keeps the current one
        public readonly byte FriendAuthMode;
        public readonly byte BNoChange;             // keep-alive, nothing changed

        public ChangeMyStatusReq(ReadOnlySpan<byte> b)
        {
            ActiveHandleNameNum = b[16];
            BCharacterFound = b[17];
            ActiveCharacterSlot = b[18];
            BReceiveOnlineMessage = b[19];
            OpenStat = b[22];
            FriendAuthMode = b[23];
            BNoChange = b[24];
        }
    }

    // s2c/0x0405_change_my_status.h ChangeMyStatusAns: ChangeMyStatus answer (sqPlayOnlineChangeMyStatusCheck)
    public static class ChangeMyStatusAns
    {
        public const int Size = 32;

        public static byte[] Make(ReadOnlySpan<byte> polproRandomValueBinary)
        {
            var b = new byte[Size];
            polproRandomValueBinary.Slice(0, 16).CopyTo(b); // keys the content authentication password
            return b;                                       // checksum at 28
        }
    }

    // s2c/0x0406_load_my_status.h MyStatus: LoadMyStatus answer (sqPlayOnlineLoadMyStatusCheck)
    public static class MyStatus
    {
        public const int Size = 128;

        public static byte[] Make(byte bCharacterActive, byte activeCharacterSlot, byte bReceiveOnlineMessage, Content contentsClass, byte openStat)
        {
            var b = new byte[Size];
            b[1] = bCharacterActive;
            b[2] = activeCharacterSlot; // slot in the handle's character table
            b[3] = bReceiveOnlineMessage;
            Le.U16(b, 4, (ushort)contentsClass);
            b[6] = openStat;            // OpenStatus + 1; 0 = offline
            return b;                   // LastLoginTime 8, LastLogoutTime 12, checksum 124
        }
    }

    // s2c/0x0009_load_handle_name_list.h HandleNameInfo: LoadHandleNameList entry (sqPlayOnlineGetHandleNameInfo)
    public static class HandleNameInfo
    {
        public const int Size = 136;

        public static byte[] Make(ulong id, ReadOnlySpan<byte> name)
        {
            var b = new byte[Size];
            Le.U64(b, 8, id); // low 44 bits
            ProfileCodec.PutName(b.AsSpan(16, 16), name);
            return b;
        }
    }

    // s2c/0x0103_load_character_list.h CharacterInfo: LoadCharacterList entry, converted into a sqPolCharacter
    // (sqPlayOnlineGetCharacterInfo)
    public static class CharacterInfo
    {
        public const int Size = 104;

        public static byte[] Make(byte index, byte order, byte bAttached, byte characterIndexOfHandleNameList, Content contentsClass,
                                  uint contentsUserSubId, ulong contentsUserId, ReadOnlySpan<byte> name)
        {
            var b = new byte[Size];
            b[0] = index;
            b[1] = order;
            b[4] = bAttached;                      // bit0
            b[6] = characterIndexOfHandleNameList; // 0-7
            Le.U16(b, 8, (ushort)contentsClass);
            b[10] = (byte)'0';                     // code: PS2: (New; did not exist.) retail sends "00"
            b[11] = (byte)'0';
            Le.U32(b, 12, contentsUserSubId);
            Le.U64(b, 16, contentsUserId);
            ProfileCodec.PutName(b.AsSpan(24, 16), name);
            return b;
        }
    }

    // sq_pol_message_header.h sqPolMessageHeader: message header; a stored message's file name is O/m/ followed by its
    // base64 (sqPlayOnlineMessageCreateHeader). irc/friend_status_notice.h FriendStatNotice shares its size and its
    // tail from Length on.
    public static class SqPolMessageHeader
    {
        public const int Size = 72;

        public const int SenderPolId = 0x00;
        public const int ReceiverPolId = 0x08;
        public const int SenderName = 0x10;   // char[16]
        public const int Length = 0x38;
        public const int TypeBits = 0x3E;     // DataType : 7, Type : 5, Code : 2, bIsOnline : 1, bValid : 1
        public const int RequestBits = 0x42;  // bPolproRequest : 1, Language : 7, ...

        public static MessageType Type(ReadOnlySpan<byte> b) => (MessageType)((Le.U16(b, TypeBits) >> 7) & 0x1F);

        public static int BPolproRequest(ReadOnlySpan<byte> b) => Le.U16(b, RequestBits) & 1;

        public static void SetBPolproRequest(Span<byte> b) => Le.U16(b, RequestBits, (ushort)(Le.U16(b, RequestBits) | 1));
    }

    // irc/friend_status_notice.h FriendStatNotice: friend status notice (__sqProfCopyFriendStat)
    public sealed class FriendStatNotice
    {
        public const int Size = 72;

        public ulong SenderPolId;       // the friend, masked
        public ulong ReceiverPolId;     // masked; 0 for a friend's status
        public byte Connected;          // 2 connected, 1 polpro available, 0 closed
        public byte Status;             // status + 1: 1 offline, 2 online, 3 away
        public byte Active;             // bit0 playing, bits 1-3 character slot
        public Content ContentsClass;
        public byte HandleNumber;
        public byte HasExtended;        // a FriendStatExtended block follows
        public byte LegacyBlock;
        public byte ListIndex;          // index in the recipient's FriendList
        public ushort UdpPortSlot;      // login notice: the game binds 54090 + slot - 1, 0 leaves the port alone
        public ulong SystemAgeOfData;   // the client applies only newer ones
        public uint Length;             // payload characters + 1
        public MessageType Type;
        public byte BIsOnline;
        public byte BValid;
        public byte BPolproRequest;

        public byte[] ToBytes()
        {
            var b = new byte[Size];
            Le.U64(b, 0x00, SenderPolId);
            Le.U64(b, 0x08, ReceiverPolId);
            b[0x10] = Connected;
            b[0x11] = Status;
            b[0x12] = Active;
            // Purpose 0x13
            Le.U16(b, 0x14, (ushort)ContentsClass);
            b[0x18] = HandleNumber;
            b[0x19] = HasExtended;
            b[0x1B] = LegacyBlock;
            b[0x1C] = ListIndex;
            Le.U16(b, 0x22, UdpPortSlot);
            Le.U64(b, 0x30, SystemAgeOfData);
            Le.U32(b, 0x38, Length);
            Le.U16(b, 0x3E, (ushort)((((uint)Type & 0x1F) << 7) | ((uint)BIsOnline & 1) << 14 | ((uint)BValid & 1) << 15));
            Le.U16(b, 0x42, (ushort)(BPolproRequest & 1));
            return b;
        }
    }

    // FriendStatExtended: friend status notice payload, followed by a sqPolCharacterPrimitive when 0x08 is set
    public static class FriendStatExtended
    {
        public const int Size = 8;
    }
}
