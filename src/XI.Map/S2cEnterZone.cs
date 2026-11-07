// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_ENTERZONE::PacketData (0x008),
// src/map/packets/s2c/0x008_enterzone.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cItemMax / S2cPacketControl convention.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_enterzone_harness.cpp -> map_s2c_enterzone_golden.txt), NOT hand-computed.
//
// THE genuinely-new facts this chunk pins:
//   (1) FIRST S2C body that is EXCLUSIVELY one fixed uint8_t ARRAY member -- the WHOLE body IS a single array slice, no
//       co-located scalar/word. Prior uint8[N] members were always MIXED with other fields (chunk 31 unknown0E[6]
//       trailing a bit-field word + scalars, the chunk 29/33 padding arrays inside dense chains). The oracle proves
//       BODY_IS_PURE_ARRAY=1 (sizeof(PD)==sizeof(EnterZoneTbl)==48) -- a compiler fact, not a hand assumption.
//   (2) The largest fixed uint8[] yet (48 > unknown0E's 6); element stride 1 (no inter-element pad, OFF.EnterZoneTbl[1]==1).
//   (3) A PURE uint8 body -> whole-struct alignof == 1 (the uint8 element's own align; no u16/u32 raises it -- contrast
//       chunk 33's u16[] body alignof 2 and chunk 35's u32[] body alignof 4). 48%1==0 -> no tail pad, PAD_INSERTED=0.
//
// Layout (DEFAULT packing, body-relative). sizeof=48, alignof=1:
//   [00..47]  uint8 EnterZoneTbl[48]   (the entire body; stride 1, no inter-element pad)

using System;

namespace XI.Map
{
    // GP_SERV_COMMAND_ENTERZONE::PacketData (outer, body-relative, DEFAULT packing). sizeof=48, alignof=1.
    // The whole body is the single fixed uint8 EnterZoneTbl[48] array (no other member).
    public static class S2cEnterZone
    {
        public const int SizeOf  = 48; // PD_SIZE=48 (MEMBER_BYTES==48, PAD_INSERTED==0)
        public const int AlignOf = 1;  // PD_ALIGN=1 -- a pure uint8 body (no u16/u32 member to raise the align)

        // THE first whole-body-is-one-u8-array member (compiler OFF_PD.EnterZoneTbl=0; BODY_IS_PURE_ARRAY=1).
        public const int OffEnterZoneTbl    = 0;    // OFF_PD.EnterZoneTbl=0
        public const int EnterZoneTblCount  = 48;   // ARR_EnterZoneTbl_COUNT=48 (the whole body)
        public const int EnterZoneTblStride = 1;    // ARR_EnterZoneTbl_ELEMSIZE=1 (== sizeof(uint8), no inter-element pad)
        public const int EnterZoneTblLen    = 48;   // ARR_EnterZoneTbl_LEN=48 (== SizeOf -- the array IS the body)

        // Byte offset (body-relative) of an EnterZoneTbl element: base + index*stride (stride 1 -> base+index).
        public static int EnterZoneTblOffset(int index) => OffEnterZoneTbl + index * EnterZoneTblStride;

        // ---- uint8 EnterZoneTbl[] element accessors ----
        public static byte GetEnterZoneTbl(ReadOnlySpan<byte> b, int index) => b[OffEnterZoneTbl + index];
        public static void SetEnterZoneTbl(Span<byte> b, int index, byte v) => b[OffEnterZoneTbl + index] = v;

        // The whole body == the single EnterZoneTbl slice (BODY_IS_PURE_ARRAY: the array is the entire PacketData).
        public static ReadOnlySpan<byte> GetEnterZoneTblSlice(ReadOnlySpan<byte> b) => b.Slice(OffEnterZoneTbl, EnterZoneTblLen);

        // ---- generic little-endian scalar accessors keyed by a compiler offset (cross-check surface) ----
        public static byte GetU8(ReadOnlySpan<byte> b, int off) => b[off];
        public static void SetU8(Span<byte> b, int off, byte v) => b[off] = v;
    }
}
