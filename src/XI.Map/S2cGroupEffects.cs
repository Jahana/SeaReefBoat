// C# wire model for the first Phase-3 map S2C BODY struct: GP_SERV_COMMAND_GROUP_EFFECTS (0x076),
// src/map/packets/s2c/0x076_group_effects.h. This is layout-only (the same descriptor approach as the
// login lobby structs in XI.Login): the byte offsets/sizes/alignment are copied from the compiled MSVC
// layout oracle (csharp/oracle/map_s2c_group_effects_harness.cpp -> map_s2c_group_effects_golden.txt),
// NEVER hand-computed. tests/XI.Verify.Map re-reads that golden and asserts these constants plus a
// deterministic byte-fill match it exactly; any engine layout change requires rebuilding the oracle.
//
// The load-bearing, hand-reasoner-trap fact the oracle pinned: partymemberbuffs_t is declared inside
// #pragma pack(push,1), so its ALIGNMENT is 1 -- which means the Members[5] array in the outer
// (unpacked) PacketData has NO inter-element padding: stride == sizeof(partymemberbuffs_t) == 48, so
// sizeof(PacketData) == 240. Sizing for the uint64_t's natural 8-byte alignment would wrongly give
// stride 56 / total 280. The engine only reads/writes this through CBasicPacket::ref<T>(index) over a
// 0x1FF buffer, so there is no C# struct-layout concern here -- we reproduce the compiler's byte math.

using System;

namespace XI.Map
{
    // partymemberbuffs_t (packed, sizeof=48): one party member's buff row. Offsets from the oracle.
    public static class PartyMemberBuffs
    {
        public const int SizeOf = 48;  // SIZE_partymemberbuffs_t=48
        public const int AlignOf = 1;  // ALIGN_partymemberbuffs_t=1 (forced by #pragma pack(1))

        public const int OffUniqueNo = 0;   // uint32 UniqueNo
        public const int OffActIndex = 4;   // uint16 ActIndex
        public const int OffPadding06 = 6;  // uint16 padding06
        public const int OffBits = 8;       // uint64 Bits
        public const int OffBuffs = 16;     // uint8  Buffs[32]
        public const int BuffsLen = 32;     // sizeof(Buffs)
    }

    // GP_SERV_COMMAND_GROUP_EFFECTS::PacketData { partymemberbuffs_t Members[5] }. sizeof=240.
    // Body only: the engine embeds this after the 4-byte CBasicPacket header (XI.Map.BasicPacket).
    public static class S2cGroupEffects
    {
        public const int SizeOf = 240;         // SIZE_PacketData=240
        public const int AlignOf = 1;          // ALIGN_PacketData=1 (its only member is pack(1)-aligned)
        public const int MemberCount = 5;      // Members[5]
        public const int MemberStride = 48;    // STRIDE_Members = sizeof(partymemberbuffs_t), no padding

        // Element field offsets within the body (1-based member index * stride + field offset).
        public static int MemberOffset(int index) => index * MemberStride;
        public static int UniqueNoOffset(int index) => MemberOffset(index) + PartyMemberBuffs.OffUniqueNo;
        public static int BitsOffset(int index) => MemberOffset(index) + PartyMemberBuffs.OffBits;
        public static int BuffsOffset(int index) => MemberOffset(index) + PartyMemberBuffs.OffBuffs;

        // Little-endian field accessors over the raw body bytes (matches CBasicPacket::ref<T>).
        public static uint GetUniqueNo(ReadOnlySpan<byte> body, int index) =>
            BitConverter.ToUInt32(body.Slice(UniqueNoOffset(index), 4));

        public static void SetUniqueNo(Span<byte> body, int index, uint value) =>
            BitConverter.TryWriteBytes(body.Slice(UniqueNoOffset(index), 4), value);

        public static ushort GetActIndex(ReadOnlySpan<byte> body, int index) =>
            BitConverter.ToUInt16(body.Slice(MemberOffset(index) + PartyMemberBuffs.OffActIndex, 2));

        public static void SetActIndex(Span<byte> body, int index, ushort value) =>
            BitConverter.TryWriteBytes(body.Slice(MemberOffset(index) + PartyMemberBuffs.OffActIndex, 2), value);

        public static ulong GetBits(ReadOnlySpan<byte> body, int index) =>
            BitConverter.ToUInt64(body.Slice(BitsOffset(index), 8));

        public static void SetBits(Span<byte> body, int index, ulong value) =>
            BitConverter.TryWriteBytes(body.Slice(BitsOffset(index), 8), value);
    }
}
