// src/search/enums/search_type.h, search_handler.h TCPREQUESTTYPE, search.h SearchRequest and data_loader.h's rows,
// plus common/utils.cpp packBitsLE / unpackBitsLE (an MSB-first bit stream).

using System.Collections.Generic;

namespace XI.Search
{
    public enum SearchType : byte
    {
        Name = 0x00,          // 00000
        Area = 0x01,          // 00001
        Nation = 0x02,        // 00010
        Job = 0x03,           // 00011
        Level = 0x04,         // 00100
        Race = 0x05,          // 00101
        Flags1 = 0x06,        // 00110
        Id = 0x08,            // 01000
        Party = 0x0A,         // 01010
        Linkshell = 0x0B,     // 01011
        Friend = 0x0C,        // 01100
        LinkshellRank = 0x0D, // 01101
        Unknown0E = 0x0E,     // 01110
        Rank = 0x10,          // 10000
        Comment = 0x11,       // 10001
        Linkshell2 = 0x13,    // 10011
        Flags2 = 0x16,        // 10110
        Language = 0x17,      // 10111
        RequestList = 0x18,   // 11000
    }

    // TCPREQUESTTYPE
    public static class TcpRequestType
    {
        public const byte SearchAll = 0x00;
        public const byte IdList = 0x01;
        public const byte GroupList = 0x02;
        public const byte Search = 0x03;
        public const byte AhHistorySingle = 0x05;
        public const byte AhHistoryStack = 0x06;
        public const byte SearchComment = 0x08;
        public const byte AhRequestMore = 0x10;
        public const byte AhRequest = 0x15;
    }

    public static class SearchConstants
    {
        // md5 hash + blowfish key appended by SearchHandler::encrypt()
        public const int PacketTrailerSize = 0x10 + 0x04;
        // Worst case packed entity entry; largest real entry is linkshell at 64 bytes
        public const int EntryMaxSize = 67;
        // SearchPacket::max_size: max size of search packet is 1024 in packets
        public const int PacketMaxSize = 1024;
    }

    public sealed class SearchRequest
    {
        public ushort[] ZoneId = new ushort[15];
        public byte JobId;
        public byte MinLvl;
        public byte MaxLvl;
        public byte Race;
        public byte Nation;
        public byte MinRank;
        public byte MaxRank;
        public uint? LsId;
        public uint Flags;
        public byte[] Name = System.Array.Empty<byte>();
        public byte NameLen;
        public byte CommentType;
        public bool FriendsOnly;
        public bool RequestListOnly;
        public readonly List<uint> CharacterIds = new();
        public readonly List<byte[]> CharacterNames = new();
    }

    public struct AuctionHouseItem
    {
        public ushort ItemID;
        public uint SingleAmount;
        public uint StackAmount;
        public ushort Category;
    }

    public struct AuctionHouseHistory
    {
        public uint Price;
        public uint Data;
        public byte[] Name1;
        public byte[] Name2;
    }

    public sealed class SearchEntity
    {
        public byte[] Name = System.Array.Empty<byte>();
        public uint Id;
        public byte Mjob;
        public byte Mlvl;
        public byte Sjob;
        public byte Slvl;
        public byte Nation;
        public byte Rank;
        public byte Race;
        public ushort Zone;
        public ushort PrevZone;
        public uint Flags1;
        public uint Flags2;
        public uint LinkshellId1;
        public uint LinkshellId2;
        public byte LinkshellRank1;
        public byte LinkshellRank2;
        public bool Mentor;
        public byte SeacomType;
        public byte Languages;
        public bool GmHidden;
        public bool Disconnecting;
        public bool Muted;
        public byte UnityLeader;
    }

    public static class Bits
    {
        // packBitsLE: value's bits, high first, from bit `bitOffset` of the MSB-first stream at `target`; returns the
        // offset after them
        public static int Pack(byte[] target, ulong value, int bitOffset, int lengthInBit)
        {
            for (int i = 0; i < lengthInBit; i++)
            {
                int pos = bitOffset + i;
                int mask = 0x80 >> (pos % 8);
                if (((value >> (lengthInBit - 1 - i)) & 1) != 0) target[pos / 8] |= (byte)mask;
                else target[pos / 8] &= (byte)~mask;
            }
            return bitOffset + lengthInBit;
        }

        // unpackBitsLE
        public static ulong Unpack(byte[] source, int byteOffset, int bitOffset, int lengthInBit)
        {
            ulong value = 0;
            for (int i = 0; i < lengthInBit; i++)
            {
                int pos = byteOffset * 8 + bitOffset + i;
                int bit = pos / 8 < source.Length ? (source[pos / 8] >> (7 - pos % 8)) & 1 : 0;
                value = (value << 1) | (uint)bit;
            }
            return value;
        }
    }
}
