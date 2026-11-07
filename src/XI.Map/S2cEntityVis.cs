// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_ENTITY_VIS::PacketData (0x077),
// src/map/packets/s2c/0x077_entity_vis.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cPacketControl / S2cItemMax convention.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_entity_vis_harness.cpp -> map_s2c_entity_vis_golden.txt), NOT hand-computed.
//
// THE genuinely-new facts this chunk pins:
//   (1) FIRST declared-padding-BRIDGE to a uint32_t ARRAY (not a scalar): a leading uint8 Flags@0 + a DECLARED uint8
//       padding05[3]@1..3 bring the running offset to 4 == the uint32[] UniqueNo[32]'s alignment -> the array lands at
//       offset 4 with NO silent pad (PADDING_BRIDGE==OFF.UniqueNo==4, PAD_INSERTED==0). Contrast chunk 37 (item_same
//       bridged to a u32 SCALAR) and chunk 35 (packetcontrol's u32[] led DIRECTLY by a u32 scalar, base trivially aligned).
//   (2) THE largest fixed uint32[] array in an S2C body yet (32 / 128 bytes > packetcontrol's 5); stride 4 (UniqueNo[1]==8).
//       Whole sizeof = 4 + 32*4 == 132, alignof 4, 132%4==0 no tail pad.
//   (3) Whole-struct alignof == 4 (the uint32 ARRAY element's align) even though the two leading members are uint8 (the
//       u32 array raises the align -- the item_max shape at a new array width; chunk 33's whole align was 2 from its u16).
//
// Layout (DEFAULT packing, body-relative). sizeof=132, alignof=4:
//   [0]        uint8  Flags
//   [1..3]     uint8  padding05[3]   (declared real bytes == the u32-array alignment gap)
//   [4..131]   uint32 UniqueNo[32]   (element stride 4, no inter-element pad)

using System;

namespace XI.Map
{
    // GP_SERV_COMMAND_ENTITY_VIS::PacketData (outer, body-relative, DEFAULT packing). sizeof=132, alignof=4.
    // A uint8 scalar, a declared u8[3] padding bridging to the uint32[] array's 4-alignment, and a 32-element u32 array.
    public static class S2cEntityVis
    {
        public const int SizeOf  = 132; // PD_SIZE=132 (MEMBER_BYTES==132, PAD_INSERTED==0)
        public const int AlignOf = 4;   // PD_ALIGN=4 -- from the uint32 UniqueNo array element (largest member align)

        // ---- uint8 Flags (compiler OFF_PD.Flags=0) ----
        public const int OffFlags = 0; // OFF_PD.Flags=0

        // ---- uint8 padding05[3] (compiler OFF_PD.padding05=1): the DECLARED bytes bridging to the u32 array's 4-align.
        //      PADDING_BRIDGE == sizeof(Flags) + sizeof(padding05) == 4 == OFF_PD.UniqueNo -- no silent pad. ----
        public const int OffPadding05  = 1;   // OFF_PD.padding05=1
        public const int Padding05Len  = 3;   // ARR_padding05_COUNT=3
        public const int PaddingBridge = 4;   // OFF_PD.UniqueNo (== Flags + padding05, the running offset the u32[] needs)

        // ---- uint32 UniqueNo[32] (compiler OFF_PD.UniqueNo=4): THE first padding-bridge-to-a-u32-array member.
        //      Element stride 4 (UniqueNo[1]==8), no inter-element pad; whole array reaches the 132-byte end. ----
        public const int OffUniqueNo     = 4;    // OFF_PD.UniqueNo=4
        public const int UniqueNoCount   = 32;   // ARR_UniqueNo_COUNT=32 (the largest fixed u32[] in an S2C body yet)
        public const int UniqueNoStride  = 4;    // ARR_UniqueNo_ELEMSIZE=4
        public const int UniqueNoLen     = 128;  // ARR_UniqueNo_LEN=128 (32 * 4)

        // Byte offset (body-relative) of a UniqueNo element: base 4 + index*stride (no inter-element pad).
        public static int UniqueNoOffset(int index) => OffUniqueNo + index * UniqueNoStride;

        // ---- uint8 Flags accessor ----
        public static byte GetFlags(ReadOnlySpan<byte> b) => b[OffFlags];
        public static void SetFlags(Span<byte> b, byte v) => b[OffFlags] = v;

        // the declared padding05[3] are REAL wire bytes (== the 3-byte gap the u32 array needs); expose a slice.
        public static ReadOnlySpan<byte> GetPadding05(ReadOnlySpan<byte> b) => b.Slice(OffPadding05, Padding05Len);

        // ---- uint32 UniqueNo[] element accessors ----
        public static uint GetUniqueNo(ReadOnlySpan<byte> b, int index) => BitConverter.ToUInt32(b.Slice(UniqueNoOffset(index), 4));
        public static void SetUniqueNo(Span<byte> b, int index, uint v) => BitConverter.TryWriteBytes(b.Slice(UniqueNoOffset(index), 4), v);

        public static ReadOnlySpan<byte> GetUniqueNoSlice(ReadOnlySpan<byte> b) => b.Slice(OffUniqueNo, UniqueNoLen);

        // ---- generic little-endian scalar accessors keyed by a compiler offset (cross-check surface) ----
        public static byte GetU8(ReadOnlySpan<byte> b, int off) => b[off];
        public static void SetU8(Span<byte> b, int off, byte v) => b[off] = v;
        public static uint GetU32(ReadOnlySpan<byte> b, int off) => BitConverter.ToUInt32(b.Slice(off, 4));
        public static void SetU32(Span<byte> b, int off, uint v) => BitConverter.TryWriteBytes(b.Slice(off, 4), v);
    }
}
