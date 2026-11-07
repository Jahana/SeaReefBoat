// C# wire model for the Phase-3 map C2S body struct GP_CLI_COMMAND_EQUIPSET_CHECK (0x052),
// src/map/packets/c2s/0x052_equipset_check.h via the GP_CLI_PACKET(...) macro (c2s/base.h).
//
// CRITICAL METHODOLOGY: every offset/size/mask below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_c2s_equipset_check_harness.cpp -> map_c2s_equipset_check_golden.txt), NOT
// hand-computed. This is the most layout-dense C2S body ported so far and fuses the two hardest packing
// techniques:
//   (a) a NESTED struct (equipsetrequestitem_t) whose byte 0 is a bit-field run (HasItemFlg:1 /
//       RemoveItemFlg:1 / Category:6). offsetof is ILLEGAL on a bit-field (C2618), so the oracle derived
//       each field's byte + mask + shift by a RUNTIME SENTINEL SCAN: HasItemFlg -> byte 0 mask 0x01 shift 0;
//       RemoveItemFlg -> byte 0 mask 0x02 shift 1; Category -> byte 0 mask 0xFC shift 2. The same struct
//       then re-aligns a uint16: bit-byte@0 + ItemIndex@1 + ItemNo@2 -> ERI_ALIGN=2 / ERI_SIZE=4.
//   (b) that nested struct is embedded BOTH as a single member (ItemChange@8) AND as a 16-element array
//       (Equipment@12, stride 4). Because ERI_ALIGN=2 and 4 is a multiple of 2, the array has NO
//       inter-element padding (Equipment[1].ItemNo at 18, Equipment[15].ItemIndex at 73, last byte 76 =
//       ESC_SIZE, no tail pad). A hand-reasoner sizing ItemNo's natural 4-byte alignment would get stride
//       8 and total 132; the compiler says stride 4 / total 76.
// C++ bit-field read/write semantics are reproduced as read-modify-write on the shared bit-byte. The
// GP_CLI_HEADER id:9|size:7|sync framing is shared with the other C2S structs (XI.Map.C2sPos owns the
// WriteHeader/ReadId helpers, reused unchanged here).

using System;

namespace XI.Map
{
    // equipsetrequestitem_t (inner struct, default-packed). sizeof=4, alignof=2. Offsets are INNER-relative.
    public static class EquipsetRequestItem
    {
        public const int SizeOf = 4;         // ERI_SIZE=4
        public const int AlignOf = 2;        // ERI_ALIGN=2

        public const int OffBitByte = 0;     // the HasItemFlg/RemoveItemFlg/Category bit run (byte 0)
        public const int OffItemIndex = 1;   // OFF_ERI.ItemIndex=1 (uint8)
        public const int OffItemNo = 2;      // OFF_ERI.ItemNo=2 (uint16, realigned to 2)

        // Sentinel-scan-derived bit-field masks/shifts (offsetof illegal on bit-fields).
        public const int HasItemFlgMask = 0x01;      // BF_HasItemFlg_mask=1
        public const int HasItemFlgShift = 0;        // BF_HasItemFlg_shift=0
        public const int RemoveItemFlgMask = 0x02;   // BF_RemoveItemFlg_mask=2
        public const int RemoveItemFlgShift = 1;     // BF_RemoveItemFlg_shift=1
        public const int CategoryMask = 0xFC;        // BF_Category_mask=252
        public const int CategoryShift = 2;          // BF_Category_shift=2
        public const int CategoryWidth = 0x3F;       // 6-bit field value range
    }

    // GP_CLI_COMMAND_EQUIPSET_CHECK (outer, default-packed). sizeof=76, alignof=2.
    public static class C2sEquipsetCheck
    {
        // GP_CLI_HEADER prefix (id:9|size:7|sync) -- same 4-byte framing as pos/recipe/lockstyle.
        public const int OffHeader = 0;             // OFF_ESC.header=0
        public const int HeaderSize = 4;            // HDR_SIZE=4
        public const int OffSync = 2;               // HDR_OFF_sync=2

        public const int SizeOf = 76;               // ESC_SIZE=76
        public const int AlignOf = 2;               // ESC_ALIGN=2

        public const int OffEquipKind = 4;          // OFF_ESC.EquipKind=4 (uint8)
        public const int OffPadding00 = 5;          // OFF_ESC.padding00=5 (uint8[3])
        public const int Padding00Len = 3;          // ESC_padding00_len=3
        public const int OffItemChange = 8;         // OFF_ESC.ItemChange=8 (single equipsetrequestitem_t)
        public const int OffEquipment = 12;         // OFF_ESC.Equipment=12 (equipsetrequestitem_t[16])
        public const int EquipmentCount = 16;       // ESC_Equipment_count=16
        public const int ItemStride = 4;            // ESC_ITEM_STRIDE=4 (== EquipsetRequestItem.SizeOf, no pad)

        // Byte offset of the single ItemChange member and of each Equipment element / a field within one.
        public static int ElementOffset(int index) => OffEquipment + index * ItemStride;
        public static int FieldOffset(int index, int innerOffset) => ElementOffset(index) + innerOffset;

        // ---- EquipKind (outer scalar) -------------------------------------------------------------
        public static byte GetEquipKind(ReadOnlySpan<byte> body) => body[OffEquipKind];
        public static void SetEquipKind(Span<byte> body, byte v) => body[OffEquipKind] = v;

        // ---- shared per-inner-struct accessors, parameterised by the member's body-absolute base ----
        public static bool GetHasItemFlg(ReadOnlySpan<byte> body, int bitByte) => (body[bitByte] & EquipsetRequestItem.HasItemFlgMask) != 0;
        public static void SetHasItemFlg(Span<byte> body, int bitByte, bool v) =>
            body[bitByte] = (byte)((body[bitByte] & ~EquipsetRequestItem.HasItemFlgMask) | (v ? EquipsetRequestItem.HasItemFlgMask : 0));

        public static bool GetRemoveItemFlg(ReadOnlySpan<byte> body, int bitByte) => (body[bitByte] & EquipsetRequestItem.RemoveItemFlgMask) != 0;
        public static void SetRemoveItemFlg(Span<byte> body, int bitByte, bool v) =>
            body[bitByte] = (byte)((body[bitByte] & ~EquipsetRequestItem.RemoveItemFlgMask) | (v ? EquipsetRequestItem.RemoveItemFlgMask : 0));

        // Category is a 6-bit field at shift 2 within the bit-byte; read/mask-modify-write, C++ semantics.
        public static byte GetCategory(ReadOnlySpan<byte> body, int bitByte) =>
            (byte)((body[bitByte] & EquipsetRequestItem.CategoryMask) >> EquipsetRequestItem.CategoryShift);
        public static void SetCategory(Span<byte> body, int bitByte, byte value) =>
            body[bitByte] = (byte)((body[bitByte] & ~EquipsetRequestItem.CategoryMask) | ((value & EquipsetRequestItem.CategoryWidth) << EquipsetRequestItem.CategoryShift));

        public static byte GetItemIndex(ReadOnlySpan<byte> body, int itemIndexOffset) => body[itemIndexOffset];
        public static void SetItemIndex(Span<byte> body, int itemIndexOffset, byte v) => body[itemIndexOffset] = v;

        public static ushort GetItemNo(ReadOnlySpan<byte> body, int itemNoOffset) => BitConverter.ToUInt16(body.Slice(itemNoOffset, 2));
        public static void SetItemNo(Span<byte> body, int itemNoOffset, ushort v) => BitConverter.TryWriteBytes(body.Slice(itemNoOffset, 2), v);

        // ---- single-member (ItemChange) convenience accessors -------------------------------------
        public static bool IcGetHasItemFlg(ReadOnlySpan<byte> b) => GetHasItemFlg(b, OffItemChange + EquipsetRequestItem.OffBitByte);
        public static void IcSetHasItemFlg(Span<byte> b, bool v) => SetHasItemFlg(b, OffItemChange + EquipsetRequestItem.OffBitByte, v);
        public static bool IcGetRemoveItemFlg(ReadOnlySpan<byte> b) => GetRemoveItemFlg(b, OffItemChange + EquipsetRequestItem.OffBitByte);
        public static void IcSetRemoveItemFlg(Span<byte> b, bool v) => SetRemoveItemFlg(b, OffItemChange + EquipsetRequestItem.OffBitByte, v);
        public static byte IcGetCategory(ReadOnlySpan<byte> b) => GetCategory(b, OffItemChange + EquipsetRequestItem.OffBitByte);
        public static void IcSetCategory(Span<byte> b, byte v) => SetCategory(b, OffItemChange + EquipsetRequestItem.OffBitByte, v);
        public static byte IcGetItemIndex(ReadOnlySpan<byte> b) => GetItemIndex(b, OffItemChange + EquipsetRequestItem.OffItemIndex);
        public static void IcSetItemIndex(Span<byte> b, byte v) => SetItemIndex(b, OffItemChange + EquipsetRequestItem.OffItemIndex, v);
        public static ushort IcGetItemNo(ReadOnlySpan<byte> b) => GetItemNo(b, OffItemChange + EquipsetRequestItem.OffItemNo);
        public static void IcSetItemNo(Span<byte> b, ushort v) => SetItemNo(b, OffItemChange + EquipsetRequestItem.OffItemNo, v);

        // ---- array-element (Equipment[index]) convenience accessors -------------------------------
        public static bool EqGetHasItemFlg(ReadOnlySpan<byte> b, int i) => GetHasItemFlg(b, FieldOffset(i, EquipsetRequestItem.OffBitByte));
        public static void EqSetHasItemFlg(Span<byte> b, int i, bool v) => SetHasItemFlg(b, FieldOffset(i, EquipsetRequestItem.OffBitByte), v);
        public static bool EqGetRemoveItemFlg(ReadOnlySpan<byte> b, int i) => GetRemoveItemFlg(b, FieldOffset(i, EquipsetRequestItem.OffBitByte));
        public static void EqSetRemoveItemFlg(Span<byte> b, int i, bool v) => SetRemoveItemFlg(b, FieldOffset(i, EquipsetRequestItem.OffBitByte), v);
        public static byte EqGetCategory(ReadOnlySpan<byte> b, int i) => GetCategory(b, FieldOffset(i, EquipsetRequestItem.OffBitByte));
        public static void EqSetCategory(Span<byte> b, int i, byte v) => SetCategory(b, FieldOffset(i, EquipsetRequestItem.OffBitByte), v);
        public static byte EqGetItemIndex(ReadOnlySpan<byte> b, int i) => GetItemIndex(b, FieldOffset(i, EquipsetRequestItem.OffItemIndex));
        public static void EqSetItemIndex(Span<byte> b, int i, byte v) => SetItemIndex(b, FieldOffset(i, EquipsetRequestItem.OffItemIndex), v);
        public static ushort EqGetItemNo(ReadOnlySpan<byte> b, int i) => GetItemNo(b, FieldOffset(i, EquipsetRequestItem.OffItemNo));
        public static void EqSetItemNo(Span<byte> b, int i, ushort v) => SetItemNo(b, FieldOffset(i, EquipsetRequestItem.OffItemNo), v);
    }
}
