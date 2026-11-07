// C# port of the CBasicPacket HEADER framing from src/map/packets/basic.h.
//
// The 4-byte map-packet header is a little-endian bit-packed word: a 9-bit packet type (opcode)
// whose top bit overflows into byte-1 bit-0, a 7-bit size field in byte-1 bits 1..7, and a 16-bit
// sequence at bytes 2..3. The non-obvious arithmetic -- ((size+3)&~3)/2 rounding with uint8
// truncation, and the min(2*(byte1&~1), PACKET_SIZE) readback clamp -- is derived from the compiled
// MSVC oracle (csharp/oracle/map_packet_harness.cpp -> map_packet_golden.txt), never hand-computed
// (repo iron rule). E.g. setSize(509) yields 0 in the engine because ((509+3)&~3)/2 == 256 overflows
// the byte-1 OR; the oracle pinned that, and tests/XI.Verify.Map asserts this port reproduces it.
//
// Only the header framing is here. The packet BODY codec, opcode dispatch, and entity/zone/session
// paths are the live sibling boundary (Phase 3+, deferred) and are NOT ported.

using System;

namespace XI.Map
{
    // src/map/packets/basic.h: enum ENTITYUPDATE { ENTITY_SPAWN, ENTITY_SHOW, ENTITY_UPDATE,
    // ENTITY_HIDE, ENTITY_DESPAWN } -- implicit ordinals 0..4 (oracle-derived).
    public enum EntityUpdate
    {
        Spawn = 0,
        Show = 1,
        Update = 2,
        Hide = 3,
        Despawn = 4,
    }

    // Mirrors CBasicPacket's raw 0x1FF buffer + header accessors. The buffer is exposed so callers
    // can lay body bytes at their own offsets (the engine's ref<T>(index)); this class owns only the
    // header accessors' semantics.
    public sealed class BasicPacket
    {
        // constexpr size_t PACKET_SIZE = 0x1FF;
        public const int PacketSize = 0x1FF;

        private readonly byte[] buffer_ = new byte[PacketSize];

        // Access to the raw packet bytes (engine: operator uint8* / ref<T>(index)).
        public byte[] Buffer => buffer_;

        public BasicPacket()
        {
            // CBasicPacket() { buffer_.fill(0); } -- a zeroed byte[] already matches.
        }

        // CBasicPacket(const CBasicPacket& other) memcpy's the whole buffer; adopt an existing one.
        public BasicPacket(byte[] copyFrom)
        {
            int n = copyFrom.Length < PacketSize ? copyFrom.Length : PacketSize;
            Array.Copy(copyFrom, 0, buffer_, 0, n);
        }

        // ref<uint16>(0): little-endian word over bytes 0..1.
        private ushort Word0
        {
            get => (ushort)(buffer_[0] | (buffer_[1] << 8));
            set
            {
                buffer_[0] = (byte)(value & 0xFF);
                buffer_[1] = (byte)(value >> 8);
            }
        }

        // ref<uint16>(2): little-endian word over bytes 2..3.
        private ushort Word2
        {
            get => (ushort)(buffer_[2] | (buffer_[3] << 8));
            set
            {
                buffer_[2] = (byte)(value & 0xFF);
                buffer_[3] = (byte)(value >> 8);
            }
        }

        // auto getType() -> uint16 { return ref<uint16>(0) & 0x1FF; }
        // Named GetOpcode (not GetType) to avoid shadowing object.GetType(); the engine's packet
        // "type" is the 9-bit packet id/opcode held in word0 bits 0..8.
        public ushort GetOpcode() => (ushort)(Word0 & 0x1FF);

        // auto getSize() -> std::size_t { return std::min<std::size_t>(2U * (ref<uint8>(1) & ~1), PACKET_SIZE); }
        // buffer_[1] is promoted to int, & ~1 clears byte-1 bit-0 (the opcode bit-8), *2 scales the
        // 7-bit size back to bytes, then clamp to PACKET_SIZE.
        public uint GetSize()
        {
            uint s = (uint)(2 * (buffer_[1] & ~1));
            return s < PacketSize ? s : PacketSize;
        }

        // auto getSequence() -> uint16 { return ref<uint16>(2); }
        public ushort GetSequence() => Word2;

        // void setType(uint16 id): clear the low 9 bits of the type/size word, set them from id.
        // The ~0x1FF (== 0xFE00) mask preserves byte-1 bits 1..7 (the size field), so SetOpcode and
        // SetSize touch disjoint bits and commute (verified by the oracle's HDRSWAP == HDR rows).
        // Named SetOpcode (not SetType) to avoid shadowing object semantics; the engine's packet
        // "type" is the 9-bit packet id/opcode held in word0 bits 0..8.
        public void SetOpcode(ushort id)
        {
            Word0 = (ushort)((Word0 & 0xFE00) | (id & 0x1FF));
        }

        // void setSize(size_t size): preserve byte-1 bit-0 (opcode bit-8), store ((size+3)&~3)/2
        // in bits 1..7. The result is OR'd into a uint8, so a value >=256 truncates away -- matching
        // the engine's `ref<uint8>(1) |= ...` exactly (the oracle pinned setSize(509) -> 0).
        public void SetSize(uint size)
        {
            buffer_[1] = (byte)(buffer_[1] & 1);
            buffer_[1] = (byte)(buffer_[1] | (((size + 3) & ~3u) / 2));
        }

        // void setSequence(uint16 sequence) { ref<uint16>(2) = sequence; }
        public void SetSequence(ushort sequence)
        {
            Word2 = sequence;
        }
    }
}
