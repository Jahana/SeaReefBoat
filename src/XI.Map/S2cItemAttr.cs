// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_ITEM_ATTR::PacketData (0x020),
// src/map/packets/s2c/0x020_item_attr.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cEffect / S2cEventStr convention.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_item_attr_enum_harness.cpp -> map_s2c_item_attr_enum_golden.txt), NOT hand-computed.
// Each enum's literal set is pinned INDEPENDENTLY by re-parsing its DEFINING header.
//
// THE genuinely-new facts this chunk pins:
//   (1) FIRST body with TWO engine enums of DIFFERENT KINDS -- Category is the FIRST UNSCOPED engine enum (the .h
//       declares `enum CONTAINER_ID : uint8`, a classic enum with a fixed underlying type, from src/map/item_container.h;
//       19 members LOC_INVENTORY=0..MAX_CONTAINER_ID=18) while LockFlg is a SCOPED `enum class ItemLockFlg : uint8_t`
//       (6 members, src/map/enums/item_lockflg.h). The chunk-21 underlying-type-substitution invariant (a scoped enum
//       member's STRUCT LAYOUT == its underlying integer) is now RE-CONFIRMED for an UNSCOPED enum too -- the oracle
//       replaces BOTH enum members with plain uint8_t and asserts the whole 40-byte struct is byte-identical
//       (EQ_SIZE/EQ_ALIGN/ECATEGORY_OFF/ELOCKFLG_OFF/EATTR_OFF all 1). Both enums' members are modelled at their
//       underlying uint8 (one byte, align 1) -- a fixed-underlying-type enum, scoped or not, occupies exactly its
//       underlying integer's storage.
//   (2) A trailing fixed uint8 Attr[24] array (the item's extra-data blob) reaching offset 13 (no pad: the two u32s +
//       u16 + three u8s land at 13) and ending at 13+24==37 (align-1 array, no silent pad anywhere inside).
//   (3) FIRST fixed-width S2C body whose 3-byte TAIL pad (bytes 37..39) is driven by a trailing align-1 u8 ARRAY, not a
//       scalar: MEMBER_BYTES=37, the two leading uint32s raise whole align to 4, so sizeof rounds 37 -> 40 (PAD_INSERTED=3,
//       HAS_TAIL_PAD=1, LAST_MEMBER_RUN_OFFSET=37, RUN_MOD_ALIGN=1). Contrast chunk 40 (item_trade_res, 2 tail bytes from
//       a u16 ending at 10 -> 12). The 3 tail bytes are dead wire space no accessor owns.
//
// Layout (DEFAULT packing, body-relative). sizeof=40, alignof=4:
//   [00..03]  uint32    ItemNum
//   [04..07]  uint32    Price
//   [08..09]  uint16    ItemNo
//   [10]      uint8     Category  (enum CONTAINER_ID : uint8 -- UNSCOPED, first of its kind)
//   [11]      uint8     ItemIndex
//   [12]      uint8     LockFlg   (enum class ItemLockFlg : uint8_t -- SCOPED)
//   [13..36]  uint8     Attr[24]  (real wire bytes, the item extra-data)
//   [37..39]  uint8     (3 compiler tail-pad bytes, dead space)

using System;

namespace XI.Map
{
    // CONTAINER_ID (src/map/item_container.h) -- the FIRST engine enum ported from an UNSCOPED `enum : uint8`
    // declaration. Underlying byte (1). Members use the .h's VERBATIM UPPERCASE/underscore spellings so VerifyPacketIds'
    // case-sensitive reflection cross-check holds (the chunk-38 lesson; ParseCppEnum's new unscoped-enum fallback
    // re-parses this header).
    public enum ContainerId : byte
    {
        LOC_INVENTORY    = 0,
        LOC_MOGSAFE      = 1,
        LOC_STORAGE      = 2,
        LOC_TEMPITEMS    = 3,
        LOC_MOGLOCKER    = 4,
        LOC_MOGSATCHEL   = 5,
        LOC_MOGSACK      = 6,
        LOC_MOGCASE      = 7,
        LOC_WARDROBE     = 8,
        LOC_MOGSAFE2     = 9,
        LOC_WARDROBE2    = 10,
        LOC_WARDROBE3    = 11,
        LOC_WARDROBE4    = 12,
        LOC_WARDROBE5    = 13,
        LOC_WARDROBE6    = 14,
        LOC_WARDROBE7    = 15,
        LOC_WARDROBE8    = 16,
        LOC_RECYCLEBIN   = 17,
        MAX_CONTAINER_ID = 18,
    }

    // ItemLockFlg (src/map/enums/item_lockflg.h) -- a SCOPED `enum class : uint8_t`. Members use the .h's VERBATIM
    // PascalCase spellings. Underlying byte (1).
    public enum ItemLockFlg : byte
    {
        Normal    = 0x00,
        NoDrop    = 0x05,
        NoSelect  = 0x0F,
        Linkshell = 0x13,
        Unknown0  = 0x19,
        Mannequin = 0x1B,
    }

    // GP_SERV_COMMAND_ITEM_ATTR::PacketData (outer, body-relative, DEFAULT packing). sizeof=40, alignof=4.
    public static class S2cItemAttr
    {
        public const int SizeOf  = 40; // PD_SIZE=40 (MEMBER_BYTES==37 + 3 compiler tail pad)
        public const int AlignOf = 4;  // PD_ALIGN=4 -- from the two leading uint32s

        public const int OffItemNum  = 0;  // OFF_PD.ItemNum=0
        public const int OffPrice    = 4;  // OFF_PD.Price=4
        public const int OffItemNo   = 8;  // OFF_PD.ItemNo=8
        public const int OffCategory = 10; // OFF_PD.Category=10 (ContainerId, unscoped enum underlying byte)
        public const int OffItemIndex = 11; // OFF_PD.ItemIndex=11
        public const int OffLockFlg  = 12; // OFF_PD.LockFlg=12 (ItemLockFlg, scoped enum underlying byte)
        public const int OffAttr     = 13; // OFF_PD.Attr=13 (the u8 array packs at the running offset 13, no pad)

        public const int AttrLen   = 24; // ARR_Attr_LEN=24 (== ARR_Attr_COUNT 24 * ARR_Attr_ELEMSIZE 1)
        public const int AttrCount = 24; // ARR_Attr_COUNT=24

        // ---- tail-pad facts (compiler-inserted dead space, NOT a declared array) ----
        public const int LastMemberRunOffset = 37; // LAST_MEMBER_RUN_OFFSET=37 (Attr base 13 + len 24)
        public const int TailPadBytes        = 3;  // TAIL_PAD_BYTES=3 (37 -> align 4 rounds to 40)

        public static int AttrOffset(int index)
        {
            if ((uint)index >= (uint)AttrCount) throw new ArgumentOutOfRangeException(nameof(index));
            return OffAttr + index; // stride 1 (uint8 array)
        }

        // ---- uint32 / uint16 accessors ----
        public static uint   GetItemNum(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(OffItemNum, 4));
        public static void   SetItemNum(Span<byte> b, uint v) => BitConverter.TryWriteBytes(b.Slice(OffItemNum, 4), v);
        public static uint   GetPrice(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(OffPrice, 4));
        public static void   SetPrice(Span<byte> b, uint v) => BitConverter.TryWriteBytes(b.Slice(OffPrice, 4), v);
        public static ushort GetItemNo(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(OffItemNo, 2));
        public static void   SetItemNo(Span<byte> b, ushort v) => BitConverter.TryWriteBytes(b.Slice(OffItemNo, 2), v);

        // ---- UNSCOPED enum member Category (underlying byte) ----
        public static ContainerId GetCategory(ReadOnlySpan<byte> b) => (ContainerId)b[OffCategory];
        public static void        SetCategory(Span<byte> b, ContainerId v) => b[OffCategory] = (byte)v;
        public static byte        GetCategoryRaw(ReadOnlySpan<byte> b) => b[OffCategory];

        // ---- plain uint8 ItemIndex ----
        public static byte GetItemIndex(ReadOnlySpan<byte> b) => b[OffItemIndex];
        public static void SetItemIndex(Span<byte> b, byte v) => b[OffItemIndex] = v;

        // ---- SCOPED enum member LockFlg (underlying byte) ----
        public static ItemLockFlg GetLockFlg(ReadOnlySpan<byte> b) => (ItemLockFlg)b[OffLockFlg];
        public static void        SetLockFlg(Span<byte> b, ItemLockFlg v) => b[OffLockFlg] = (byte)v;
        public static byte        GetLockFlgRaw(ReadOnlySpan<byte> b) => b[OffLockFlg];

        // ---- uint8 Attr[24] (real wire bytes, NOT droppable) ----
        public static byte GetAttr(ReadOnlySpan<byte> b, int index) => b[AttrOffset(index)];
        public static void SetAttr(Span<byte> b, int index, byte v) => b[AttrOffset(index)] = v;
        public static ReadOnlySpan<byte> GetAttrSlice(ReadOnlySpan<byte> b) => b.Slice(OffAttr, AttrLen);

        // ---- generic little-endian scalar accessors keyed by a compiler offset (cross-check surface) ----
        public static byte   GetU8(ReadOnlySpan<byte> b, int off) => b[off];
        public static void   SetU8(Span<byte> b, int off, byte v) => b[off] = v;
        public static ushort GetU16(ReadOnlySpan<byte> b, int off) => BitConverter.ToUInt16(b.Slice(off, 2));
        public static uint   GetU32(ReadOnlySpan<byte> b, int off) => BitConverter.ToUInt32(b.Slice(off, 4));
    }
}
