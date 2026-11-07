// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_ITEMSEARCH::PacketData (0x049),
// src/map/packets/s2c/0x049_itemsearch.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cItemSubContainer / S2cItemAttr convention.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_itemsearch_harness.cpp -> map_s2c_itemsearch_golden.txt), NOT hand-computed.
//
// THE genuinely-new facts this chunk pins:
//   (1) The LARGEST fixed uint8 array yet -- ItemName is `uint8_t ItemName[64]` (64 bytes), a TRAILING member (chunk 36's
//       whole body was u8[48], chunk 45's Attr was u8[24]).
//   (2) THE whole-struct align comes from a NON-array member: the leading `uint16 ItemNo` sets alignof 2, and the
//       trailing align-1 u8[64] does NOT raise it (compiler ALIGN_FROM_SCALAR_NOT_ARRAY=1; the array's own align == the
//       u8 element's == 1, U8_ARRAY_ALIGN=1). This is the array-ALIGN CONTRAST to chunks 33/39 where an align-2/4
//       element array DID set the whole align, and chunk 36 where the whole body WAS the u8 array (align 1).
//   (3) The scalar run (ItemNo@0 + Flag@2 + padding07@3) reaches the already-2-aligned running offset 4, the align-1
//       u8 array packs there with NO pad (ItemName[1]=5 stride 1, no inter-element pad), and 4+64==68==SizeOf (the array
//       ends EXACTLY at the struct tail, no tail pad). MEMBER_BYTES==PD_SIZE==68, PAD_INSERTED==0 (gapless i*7+3 fill).
//
// Layout (DEFAULT packing, body-relative). sizeof=68, alignof=2:
//   [00..01]  uint16 ItemNo
//   [02]      uint8  Flag
//   [03]      uint8  padding07
//   [04..67]  uint8  ItemName[64]   (the largest fixed u8 array; reaches the struct end exactly)

using System;

namespace XI.Map
{
    // GP_SERV_COMMAND_ITEMSEARCH::PacketData (outer, body-relative, DEFAULT packing). sizeof=68, alignof=2.
    public static class S2cItemSearch
    {
        public const int SizeOf  = 68; // PD_SIZE=68 (MEMBER_BYTES==68, PAD_INSERTED==0)
        public const int AlignOf = 2;  // PD_ALIGN=2 -- from the leading uint16 (the trailing align-1 u8 array does NOT raise it)

        // ---- leading u16 + two u8 scalars ----
        public const int OffItemNo    = 0; // OFF_PD.ItemNo=0
        public const int OffFlag      = 2; // OFF_PD.Flag=2
        public const int OffPadding07 = 3; // OFF_PD.padding07=3

        // ---- uint8 ItemName[64]: base 4, stride 1, count 64 (the largest fixed u8 array) ----
        public const int OffItemName   = 4; // OFF_PD.ItemName=4 (the u16+u8+u8 run reaches the already-2-aligned 4, no pad)
        public const int ItemNameCount  = 64; // ARR_ItemName_COUNT=64
        public const int ItemNameStride = 1;  // OFF_PD.ItemName[1](5) - OFF_PD.ItemName(4) == ARR_ItemName_ELEMSIZE=1
        public const int ItemNameLen    = 64; // ARR_ItemName_LEN=64

        public static int ItemNameOffset(int index)
        {
            if ((uint)index >= (uint)ItemNameCount) throw new ArgumentOutOfRangeException(nameof(index));
            return OffItemName + index * ItemNameStride;
        }

        // ---- leading scalar accessors ----
        public static ushort GetItemNo(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(OffItemNo, 2));
        public static void   SetItemNo(Span<byte> b, ushort v) => BitConverter.TryWriteBytes(b.Slice(OffItemNo, 2), v);
        public static byte   GetFlag(ReadOnlySpan<byte> b) => b[OffFlag];
        public static void   SetFlag(Span<byte> b, byte v) => b[OffFlag] = v;
        public static byte   GetPadding07(ReadOnlySpan<byte> b) => b[OffPadding07];
        public static void   SetPadding07(Span<byte> b, byte v) => b[OffPadding07] = v;

        // ---- uint8 ItemName[64] (the item's search name string, real wire bytes) ----
        public static byte GetItemName(ReadOnlySpan<byte> b, int index) => b[ItemNameOffset(index)];
        public static void SetItemName(Span<byte> b, int index, byte v) => b[ItemNameOffset(index)] = v;
        public static ReadOnlySpan<byte> GetItemNameSlice(ReadOnlySpan<byte> b) => b.Slice(OffItemName, ItemNameLen);

        // ---- generic little-endian scalar accessors keyed by a compiler offset (cross-check surface) ----
        public static byte   GetU8(ReadOnlySpan<byte> b, int off) => b[off];
        public static void   SetU8(Span<byte> b, int off, byte v) => b[off] = v;
        public static ushort GetU16(ReadOnlySpan<byte> b, int off) => BitConverter.ToUInt16(b.Slice(off, 2));
        public static void   SetU16(Span<byte> b, int off, ushort v) => BitConverter.TryWriteBytes(b.Slice(off, 2), v);
    }
}
