// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_PACKETCONTROL::PacketData (0x005),
// src/map/packets/s2c/0x005_packetcontrol.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cItemMax / S2cAbilRecast convention.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_packet_control_harness.cpp -> map_s2c_packet_control_golden.txt), NOT hand-computed.
//
// THE genuinely-new packing facts this chunk exists to pin:
//   (1) FIRST S2C body that is purely a native uint32_t SCALAR (PacketCnt) leading a native uint32_t ARRAY
//       (padding08[5]) -- the first uint32[] fixed array member in an S2C body. Prior scalar-arrays were uint8[N]
//       (chunk 31 unknown0E[6], the chunk 29/33 padding), uint16[N] (chunk 33 ItemNum2[18]), and struct[N]
//       (chunk 32 Timers[31]); the int32[9] of pendingstr is a C2S.
//   (2) THE running-offset-already-4-aligned fact: the leading scalar uint32 occupies bytes 0..3, so the running
//       offset (4) is ALREADY 4-aligned -> padding08 lands at offset 4 with NO silent pad (oracle OFF_PD.padding08==4,
//       and OFF_PD.padding08[1]==8 pins the element stride 4). Whole sizeof = 24 (4 + 5*4), alignof = 4 (the uint32),
//       24 % 4 == 0 -> no tail pad. PAD_INSERTED=0 (hole-free) is PROVEN: the gate asserts padding08 == SCALAR_RUN_OFFSET
//       (both from the compiler), never trusting the arithmetic by hand.
//   (3) DEFAULT packing: the .h has NO #pragma pack, so MSVC default alignment governs (contrast pack(1) bodies 0x076/0x113).
//       Every member is a plain uint32 (no bit-fields) -> offsetof is legal throughout.
//
// Layout (DEFAULT packing, body-relative). sizeof=24, alignof=4:
//   [00..03]  uint32 PacketCnt
//   [04..23]  uint32 padding08[5]   (element stride 4)
// The declared padding08[5] are REAL wire bytes (the client reads a fixed 24-byte body), so expose them as an array.

using System;

namespace XI.Map
{
    // GP_SERV_COMMAND_PACKETCONTROL::PacketData (outer, body-relative, DEFAULT packing). sizeof=24, alignof=4.
    // A uint32 scalar + uint32[] array; every member is fixed-width (no bit-fields / enum / union / nested struct).
    public static class S2cPacketControl
    {
        public const int SizeOf  = 24; // PD_SIZE=24 (MEMBER_BYTES==24, PAD_INSERTED==0)
        public const int AlignOf = 4;  // PD_ALIGN=4 -- from the uint32 members (largest member align)

        // ---- uint32 PacketCnt (compiler OFF_PD.PacketCnt=0) ----
        public const int OffPacketCnt   = 0;    // OFF_PD.PacketCnt=0

        // ---- uint32 padding08[5] (compiler OFF_PD.padding08=4): THE first native uint32[] S2C body member.
        //      The leading u32 scalar makes the running offset 4 (already 4-aligned), so the array needs no pad. ----
        public const int OffPadding08   = 4;    // OFF_PD.padding08=4 == OffPacketCnt + sizeof(uint32) (SCALAR_RUN_OFFSET)
        public const int Padding08Count   = 5;  // ARR_padding08_COUNT=5
        public const int Padding08Stride  = 4;  // ARR_padding08_ELEMSIZE=4 (== sizeof(uint32), no inter-element pad)
        public const int Padding08Len     = 20; // ARR_padding08_LEN=20 (5 * 4)

        // Byte offset (body-relative) of a padding08 element: base + index*stride (no inter-element pad).
        public static int Padding08Offset(int index) => OffPadding08 + index * Padding08Stride;

        // ---- uint32 PacketCnt accessors ----
        public static uint   GetPacketCnt(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(OffPacketCnt, 4));
        public static void   SetPacketCnt(Span<byte> b, uint v) => BitConverter.TryWriteBytes(b.Slice(OffPacketCnt, 4), v);

        // ---- uint32 padding08[] element accessors (the first fixed uint32-array member) ----
        public static uint   GetPadding08(ReadOnlySpan<byte> b, int index) => BitConverter.ToUInt32(b.Slice(Padding08Offset(index), 4));
        public static void   SetPadding08(Span<byte> b, int index, uint v) => BitConverter.TryWriteBytes(b.Slice(Padding08Offset(index), 4), v);

        // The declared padding08[5] are REAL wire bytes (a fixed 24-byte body); expose the raw slice.
        public static ReadOnlySpan<byte> GetPadding08Slice(ReadOnlySpan<byte> b) => b.Slice(OffPadding08, Padding08Len);

        // ---- generic little-endian scalar accessors keyed by a compiler offset (cross-check surface) ----
        public static byte   GetU8(ReadOnlySpan<byte> b, int off) => b[off];
        public static void   SetU8(Span<byte> b, int off, byte v) => b[off] = v;
        public static uint   GetU32(ReadOnlySpan<byte> b, int off) => BitConverter.ToUInt32(b.Slice(off, 4));
        public static void   SetU32(Span<byte> b, int off, uint v) => BitConverter.TryWriteBytes(b.Slice(off, 4), v);
    }
}
