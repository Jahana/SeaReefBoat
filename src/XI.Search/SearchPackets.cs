// src/search/packets/*: the search server's reply packets. Bytes 0-7 (size, "IXFF") and the md5 / key trailer are
// filled by SearchHandler.Encrypt.

using System;
using System.Buffers.Binary;

namespace XI.Search
{
    internal static class Ref
    {
        public static void U16(byte[] b, int at, ushort v) => BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(at), v);
        public static void U32(byte[] b, int at, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(at), v);
        public static ushort U16(byte[] b, int at) => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(at));
        public static uint U32(byte[] b, int at) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at));
    }

    // the entity entry CSearchListPacket, CPartyListPacket and CLinkshellListPacket share (AddPlayer); the linkshell
    // list adds the linkshell ranks and ids after the id
    internal static class EntityEntry
    {
        public static int Add(byte[] data, int offset, SearchEntity player, bool linkshell)
        {
            int sizeOffset = offset / 8;
            offset += 8;
            int length = Math.Min(player.Name.Length, 15);
            offset = Bits.Pack(data, (ulong)SearchType.Name, offset, 5);
            offset = Bits.Pack(data, (ulong)length, offset, 4);
            for (int c = 0; c < length; ++c) offset = Bits.Pack(data, player.Name[c], offset, 7);
            offset = Bits.Pack(data, (ulong)SearchType.Area, offset, 5);
            offset = Bits.Pack(data, player.Zone, offset, 10);
            if ((player.Flags1 & 0x4000) == 0)
            {
                offset = Bits.Pack(data, (ulong)SearchType.Nation, offset, 5);
                offset = Bits.Pack(data, player.Nation, offset, 2);
                offset = Bits.Pack(data, (ulong)SearchType.Job, offset, 5);
                offset = Bits.Pack(data, player.Mjob, offset, 5);
                offset = Bits.Pack(data, player.Sjob, offset, 5);
                offset = Bits.Pack(data, (ulong)SearchType.Level, offset, 5);
                offset = Bits.Pack(data, player.Mlvl, offset, 8);
                offset = Bits.Pack(data, player.Slvl, offset, 8);
                offset = Bits.Pack(data, (ulong)SearchType.Race, offset, 5);
                offset = Bits.Pack(data, player.Race, offset, 4);
                offset = Bits.Pack(data, (ulong)SearchType.Rank, offset, 5);
                offset = Bits.Pack(data, player.Rank, offset, 8);
            }
            offset = Bits.Pack(data, (ulong)SearchType.Flags1, offset, 5);
            offset = Bits.Pack(data, player.Flags1, offset, 16);
            offset = Bits.Pack(data, (ulong)SearchType.Id, offset, 5);
            offset = Bits.Pack(data, player.Id, offset, 20);
            if (linkshell)
            {
                offset = Bits.Pack(data, (ulong)SearchType.LinkshellRank, offset, 5);
                offset = Bits.Pack(data, player.LinkshellRank1, offset, 8); // 2=sack, 1=holder, 3=pearl
                offset = Bits.Pack(data, player.LinkshellRank2, offset, 8);
                offset = Bits.Pack(data, 0, offset, 8); // linkshellrank3
                offset = Bits.Pack(data, player.LinkshellId1, offset, 32);
                offset = Bits.Pack(data, player.LinkshellId2, offset, 32);
                offset = Bits.Pack(data, 0, offset, 32); // linkshellid3
            }
            offset = Bits.Pack(data, (ulong)SearchType.Unknown0E, offset, 5);
            offset = Bits.Pack(data, 0, offset, 32);
            if (player.SeacomType != 0)
            {
                offset = Bits.Pack(data, (ulong)SearchType.Comment, offset, 5);
                offset = Bits.Pack(data, player.SeacomType, offset, 32);
            }
            offset = Bits.Pack(data, (ulong)SearchType.Flags2, offset, 5);
            offset = Bits.Pack(data, player.Flags2, offset, 32);
            offset = Bits.Pack(data, (ulong)SearchType.Language, offset, 5);
            offset = Bits.Pack(data, player.Languages, offset, 16);
            if (offset % 8 > 0) offset += 8 - offset % 8; // Byte alignment
            data[sizeOffset] = (byte)(offset / 8 - sizeOffset - 1); // Entity data size
            Ref.U16(data, 0x08, (ushort)(offset / 8));             // Size of the data to send
            return offset;
        }

        // not enough space available, worst case
        public static bool Fits(byte[] data, int offset) => data.Length - offset / 8 >= SearchConstants.PacketTrailerSize + SearchConstants.EntryMaxSize;
    }

    // search_list.cpp CSearchListPacket: a maximum of 20 characters can be added to a single packet
    public sealed class SearchListPacket
    {
        private readonly byte[] _data = new byte[1024];
        private int _offset = 192;

        public SearchListPacket(uint total)
        {
            _data[0x0A] = 0x00;
            _data[0x0B] = 0x80;
            Ref.U16(_data, 0x0E, (ushort)total); // the total number of characters found (may differ from the amount that gets sent)
        }

        public bool AddPlayer(SearchEntity player)
        {
            if (!EntityEntry.Fits(_data, _offset)) return false;
            _offset = EntityEntry.Add(_data, _offset, player, false);
            return true;
        }

        public void SetFinal() => _data[0x0A] = 0x80; // is final packet

        public byte[] Data => _data;
        public int Size => _offset / 8 + SearchConstants.PacketTrailerSize;
    }

    // party_list.cpp CPartyListPacket
    public sealed class PartyListPacket
    {
        private readonly byte[] _data = new byte[1024];
        private int _offset = 192;

        public PartyListPacket(uint partyid, uint total)
        {
            _data[0x0A] = 0x80;
            _data[0x0B] = 0x82;         // packet type
            _data[0x0E] = (byte)total;  // Number of characters per packet.
        }

        public bool AddPlayer(SearchEntity player)
        {
            if (!EntityEntry.Fits(_data, _offset)) return false;
            _offset = EntityEntry.Add(_data, _offset, player, false);
            return true;
        }

        public byte[] Data => _data;
        public int Size => _offset / 8 + SearchConstants.PacketTrailerSize;
    }

    // linkshell_list.cpp CLinkshellListPacket
    public sealed class LinkshellListPacket
    {
        private readonly byte[] _data = new byte[1024];
        private int _offset = 192;

        public LinkshellListPacket(uint linkshellid, uint total)
        {
            _data[0x0A] = 0x00;                  // is final packet
            _data[0x0B] = 0x82;                  // packet type
            Ref.U16(_data, 0x0E, (ushort)total); // Total overall results
        }

        public bool AddPlayer(SearchEntity player)
        {
            if (!EntityEntry.Fits(_data, _offset)) return false;
            _offset = EntityEntry.Add(_data, _offset, player, true);
            return true;
        }

        public void SetFinal() => _data[0x0A] = 0x80; // is final packet

        public byte[] Data => _data;
        public int Size => _offset / 8 + SearchConstants.PacketTrailerSize;
    }

    // search_comment.cpp SearchCommentPacket
    public sealed class SearchCommentPacket
    {
        private readonly byte[] _data = new byte[204];

        public SearchCommentPacket(uint playerId, byte[] comment)
        {
            _data[0x08] = 154;  // Search comment packet size
            _data[0x0A] = 0x80; // Search server packet
            _data[0x0B] = 0x88; // Packet type
            _data[0x0E] = 0x01;
            Ref.U32(_data, 0x18, playerId);
            Ref.U16(_data, 0x1C, 124); // Comment length
            int length = Math.Min(comment.Length, 123);
            // Add comment bytes
            Array.Copy(comment, 0, _data, 0x1E, length);
            // Fill rest with whitespace
            _data.AsSpan(0x1E + length, 123 - length).Fill((byte)' ');
            // End comment with 0 byte
            _data[0x9A] = 0;
        }

        public byte[] Data => _data;
        public int Size => 204;
    }

    // auction_list.cpp CAHItemsListPacket: if the number of items exceeds 20, then we send several packets; the offset
    // is which set of items this packet contains, relative to the total number of items across all packets
    public sealed class AhItemsListPacket
    {
        private readonly byte[] _data = new byte[256];
        private readonly ushort _offset;
        private byte _count;

        public AhItemsListPacket(ushort offset)
        {
            _offset = offset;
            _data[0x0B] = 0x95; // packet type
        }

        // AddItem: 10 bytes per item
        public void AddItem(AuctionHouseItem item)
        {
            Ref.U16(_data, 0x18 + 0x0A * _count + 0, item.ItemID);
            Ref.U32(_data, 0x18 + 0x0A * _count + 2, item.SingleAmount);
            Ref.U32(_data, 0x18 + 0x0A * _count + 6, item.StackAmount);
            _count++;
        }

        // SetItemCount: the total number of items sent in the packet
        public void SetItemCount(ushort count)
        {
            Ref.U16(_data, 0x0E, count);
            if (count - _offset <= 20)
            {
                _data[0x0A] = 0x80;
                Ref.U16(_data, 0x08, (ushort)(0x18 + 0x0A * (count - _offset)));
            }
            else
            {
                Ref.U16(_data, 0x08, 0x18 + 0x0A * 20);
            }
        }

        public byte[] Data => _data;
        public int Size => 0x18 + 0x0A * _count + 28;
    }

    // auction_history.cpp CAHHistoryPacket (comments and RE by atom0s)
    public sealed class AhHistoryPacket
    {
        private readonly byte[] _data = new byte[475];
        private byte _count;

        public AhHistoryPacket(AuctionHouseItem item, byte stack)
        {
            _data[0x0A] = 0x80;              // flags, may be int8 as it's checked against negative.
            _data[0x0B] = 0x85;              // packet type, masked as val & 0x1F
            Ref.U16(_data, 0x10, item.ItemID); // may be obsolete
            Ref.U16(_data, 0x18, item.ItemID);
            Ref.U32(_data, 0x1A, stack != 0 ? item.StackAmount : item.SingleAmount);
            Ref.U16(_data, 0x1E, item.Category);
        }

        public void AddItem(AuctionHouseHistory item)
        {
            if (_count >= 10) return;
            int at = 0x20 + 40 * _count;
            Ref.U32(_data, at + 0x00, item.Price);
            Ref.U32(_data, at + 0x04, item.Data);
            Array.Copy(item.Name1, 0, _data, at + 0x08, Math.Min(item.Name1.Length, 15));
            Array.Copy(item.Name2, 0, _data, at + 0x18, Math.Min(item.Name2.Length, 15));
            Ref.U16(_data, 0x08, (ushort)(0x20 + 40 * ++_count));
        }

        public byte[] Data => _data;
        public int Size => 0x20 + 40 * _count + 28;
    }
}
