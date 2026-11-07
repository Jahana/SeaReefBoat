// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_ITEM_MAX::PacketData (0x01C),
// src/map/packets/s2c/0x01c_item_max.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cAbilRecast / S2cCurrencies2 convention.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_item_max_harness.cpp -> map_s2c_item_max_golden.txt), NOT hand-computed.
//
// THE genuinely-new packing facts this chunk exists to pin (contrast chunk 0x119 abil_recast's struct-array):
//   (1) FIRST fixed uint16_t ARRAY member: ItemNum2[18]. Prior scalar-array members were uint8[N] (unknown0E[6],
//       the currencies_2 padding arrays) and uint32/int32[N] (pendingstr); struct arrays (Timers[31]) are separate.
//       The uint16 ELEMENT has alignof 2, so the array base must be 2-aligned.
//   (2) THE compiler-decides-the-u16-array-alignment fact: the two leading u8 arrays occupy 18 + 14 == 32 bytes and 32
//       is ALREADY 2-aligned, so ItemNum2 lands at offset 32 with NO silent pad (the oracle pins OFF_PD.ItemNum2==32
//       and the stride via ItemNum2[1]==34). Had padding00 been 13 bytes (odd running offset), the compiler would
//       have inserted a pad byte here -- so "offsets == running sum" is only true for THIS even split; the gate
//       asserts ItemNum2 == U8_RUN_OFFSET (both from the compiler), never trusts the arithmetic by hand.
//   (3) Four-segment array chain, DEFAULT packing: uint8 ItemNum[18]@0..17 | uint8 padding00[14]@18..31 |
//       uint16 ItemNum2[18]@32..67 (stride 2, no inter-element pad) | uint8 padding01[28]@68..95 -> sizeof 96 (68+28),
//       alignof 2 (largest member align, the uint16), 96%2==0 no tail pad, PAD_INSERTED=0 (hole-free, PROVEN).
//
// Layout (DEFAULT packing, body-relative). sizeof=96, alignof=2:
//   [00..17]  uint8  ItemNum[18]
//   [18..31]  uint8  padding00[14]
//   [32..67]  uint16 ItemNum2[18]   (element stride 2)
//   [68..95]  uint8  padding01[28]

using System;

namespace XI.Map
{
    // GP_SERV_COMMAND_ITEM_MAX::PacketData (outer, body-relative, DEFAULT packing). sizeof=96, alignof=2.
    // A four-segment scalar-array chain; every member is a fixed array (no bit-fields / enum / union / nested struct).
    public static class S2cItemMax
    {
        public const int SizeOf  = 96; // PD_SIZE=96 (MEMBER_BYTES==96, PAD_INSERTED==0)
        public const int AlignOf = 2;  // PD_ALIGN=2 -- from the uint16 ItemNum2 elements (largest member align)

        // ---- uint8 ItemNum[18] (compiler OFF_PD.ItemNum=0) ----
        public const int OffItemNum     = 0;    // OFF_PD.ItemNum=0
        public const int ItemNumCount   = 18;   // ARR_ItemNum_COUNT=18 (elemsize 1)
        // ---- uint8 padding00[14] (compiler OFF_PD.padding00=18): the real wire bytes that keep the running u8 offset 32 ----
        public const int OffPadding00   = 18;   // OFF_PD.padding00=18
        public const int Padding00Len   = 14;   // ARR_padding00_COUNT=14
        // ---- uint16 ItemNum2[18] (compiler OFF_PD.ItemNum2=32): THE first uint16[] member, stride 2, base already 2-aligned ----
        public const int OffItemNum2    = 32;   // OFF_PD.ItemNum2=32 == OffItemNum + ItemNumCount + Padding00Len
        public const int ItemNum2Count  = 18;   // ARR_ItemNum2_COUNT=18 (elemsize 2)
        public const int ItemNum2Stride = 2;    // ARR_ItemNum2_ELEMSIZE=2 (== sizeof(uint16), no inter-element pad)
        // ---- uint8 padding01[28] (compiler OFF_PD.padding01=68): 32 + 18*2 == 68, reaches the end 68+28 == 96 ----
        public const int OffPadding01   = 68;   // OFF_PD.padding01=68
        public const int Padding01Len   = 28;   // ARR_padding01_COUNT=28

        // Byte offset (body-relative) of an ItemNum2 element: base + index*stride (no inter-element pad).
        public static int ItemNum2Offset(int index) => OffItemNum2 + index * ItemNum2Stride;

        // ---- uint8 ItemNum[] element accessors ----
        public static byte GetItemNum(ReadOnlySpan<byte> b, int index) => b[OffItemNum + index];
        public static void SetItemNum(Span<byte> b, int index, byte v) => b[OffItemNum + index] = v;
        public static ReadOnlySpan<byte> GetItemNumSlice(ReadOnlySpan<byte> b) => b.Slice(OffItemNum, ItemNumCount);

        // ---- uint16 ItemNum2[] element accessors (the first fixed uint16-array member) ----
        public static ushort GetItemNum2(ReadOnlySpan<byte> b, int index) => BitConverter.ToUInt16(b.Slice(ItemNum2Offset(index), 2));
        public static void   SetItemNum2(Span<byte> b, int index, ushort v) => BitConverter.TryWriteBytes(b.Slice(ItemNum2Offset(index), 2), v);

        // ---- the declared padding arrays are REAL wire bytes (padding00 does the 2-alignment work); expose slices ----
        public static ReadOnlySpan<byte> GetPadding00(ReadOnlySpan<byte> b) => b.Slice(OffPadding00, Padding00Len);
        public static ReadOnlySpan<byte> GetPadding01(ReadOnlySpan<byte> b) => b.Slice(OffPadding01, Padding01Len);

        // ---- generic little-endian scalar accessors keyed by a compiler offset (cross-check surface) ----
        public static byte   GetU8(ReadOnlySpan<byte> b, int off) => b[off];
        public static void   SetU8(Span<byte> b, int off, byte v) => b[off] = v;
        public static ushort GetU16(ReadOnlySpan<byte> b, int off) => BitConverter.ToUInt16(b.Slice(off, 2));
        public static void   SetU16(Span<byte> b, int off, ushort v) => BitConverter.TryWriteBytes(b.Slice(off, 2), v);
    }
}
