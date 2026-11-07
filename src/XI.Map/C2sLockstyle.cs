// C# wire model for the Phase-3 map C2S (client->server) BODY struct: GP_CLI_COMMAND_LOCKSTYLE
// (0x053), src/map/packets/c2s/0x053_lockstyle.h via the GP_CLI_PACKET(...) macro (c2s/base.h).
//
// CRITICAL METHODOLOGY: every offset/size/mode value below is copied from the compiled MSVC layout
// oracle (csharp/oracle/map_c2s_lockstyle_harness.cpp -> map_c2s_lockstyle_golden.txt), NOT
// hand-computed. This is the first Phase-3 body struct containing an ARRAY OF A NESTED STRUCT
// (lockstyleitem_t Items[16]) under MSVC DEFAULT packing (GP_CLI_PACKET adds no #pragma pack). The
// non-obvious, compiler-owned facts it pins:
//   - the inner lockstyleitem_t's uint16 ItemNo/padding01 RE-ALIGN to offsets 4/6 after the four
//     uint8s (sizeof=8, alignof=2);
//   - the array starts at offset 8 with stride 8 and NO inter-element padding, so Members[i] field
//     offsets are 8 + i*8 + inner-field-offset (Members[1].ItemNo=20, Members[15] last byte=136);
//   - the whole packet is sizeof=136, alignof=2 -- sized to the uint16s, not 4/8-byte forced.
// tests/XI.Verify.Map re-reads that golden and asserts these constants, the array element/stride math,
// and the whole-struct deterministic byte-fill match it byte-for-byte; any engine layout change
// requires rebuilding the oracle.
//
// The 4-byte GP_CLI_HEADER prefix (id:9|size:7|sync) is the SAME C2S header word layout the pos
// struct uses, so those helpers are reused from XI.Map.C2sPos (already oracle-verified); this class
// models the NEW nested-array surface and its own header-offset constant.

using System;

namespace XI.Map
{
    // lockstyleitem_t (inner struct, default-packed): one lock-style item row. sizeof=8, alignof=2.
    public static class LockstyleItem
    {
        public const int SizeOf = 8;           // LSI_SIZE=8
        public const int AlignOf = 2;          // LSI_ALIGN=2 (forced by the uint16 members)

        public const int OffItemIndex = 0;     // OFF_lsi.ItemIndex=0  (uint8)
        public const int OffEquipKind = 1;     // OFF_lsi.EquipKind=1  (uint8)
        public const int OffCategory = 2;      // OFF_lsi.Category=2   (uint8)
        public const int OffPadding00 = 3;     // OFF_lsi.padding00=3  (uint8)
        public const int OffItemNo = 4;        // OFF_lsi.ItemNo=4     (uint16 -- realigned after 4 u8s)
        public const int OffPadding01 = 6;     // OFF_lsi.padding01=6  (uint16)
    }

    // GP_CLI_COMMAND_LOCKSTYLE_MODE (0x053_lockstyle.h). Hand-authored uint8_t literals; the oracle
    // emitted each value (MODE_*) and the gate asserts this enum against it (the iron rule is about
    // compiler-derived bytes, but the explicit enum is still pinned against the compiled .h, not
    // guessed, so a future engine renumber is caught).
    public enum LockstyleMode : byte
    {
        Disable = 0,   // MODE_Disable=0
        Continue = 1,  // MODE_Continue=1
        Query = 2,     // MODE_Query=2
        Set = 3,       // MODE_Set=3
        Enable = 4,    // MODE_Enable=4
    }

    // GP_CLI_COMMAND_LOCKSTYLE (outer, default-packed). sizeof=136, alignof=2.
    //   GP_CLI_HEADER header;  uint8 Count;  uint8 Mode;  uint8 Flags;  uint8 padding00;
    //   lockstyleitem_t Items[16];
    // `body` is a Span positioned at the packet start (header at [0]).
    public static class C2sLockstyle
    {
        public const int SizeOf = 136;          // LS_SIZE=136
        public const int AlignOf = 2;           // LS_ALIGN=2

        // GP_CLI_HEADER prefix (id:9|size:7|sync) -- same as pos; helpers reused from XI.Map.C2sPos.
        public const int OffHeader = 0;         // OFF_ls.header=0
        public const int HeaderSize = 4;        // == XI.Map.C2sPos.HeaderSize

        // Fixed scalar fields between the header and the items array.
        public const int OffCount = 4;          // OFF_ls.Count=4
        public const int OffMode = 5;           // OFF_ls.Mode=5
        public const int OffFlags = 6;          // OFF_ls.Flags=6
        public const int OffPadding00 = 7;      // OFF_ls.padding00=7

        // The nested-items array: starts at 8, 16 elements, stride == sizeof(lockstyleitem_t).
        public const int OffItems = 8;          // OFF_ls.Items=8
        public const int ItemCount = 16;        // LS_ITEMS_COUNT=16
        public const int ItemStride = 8;        // LS_ITEM_STRIDE=8 (== LockstyleItem.SizeOf, no padding)

        // Byte offset (from packet start) of element `index` and of a field within it. The whole point
        // of the oracle was to pin that this equals 8 + index*8 + innerOffset with NO inter-element
        // padding -- a hand-reasoner sizing for the uint16 might insert a gap and get it wrong.
        public static int ElementOffset(int index) => OffItems + index * ItemStride;
        public static int FieldOffset(int index, int innerOffset) => ElementOffset(index) + innerOffset;

        // ---- scalar field accessors (little-endian, matching CBasicPacket::ref<T>) ----------------
        public static byte GetCount(ReadOnlySpan<byte> body) => body[OffCount];
        public static void SetCount(Span<byte> body, byte v) => body[OffCount] = v;

        public static byte GetMode(ReadOnlySpan<byte> body) => body[OffMode];
        public static void SetMode(Span<byte> body, LockstyleMode m) => body[OffMode] = (byte)m;

        public static byte GetFlags(ReadOnlySpan<byte> body) => body[OffFlags];
        public static void SetFlags(Span<byte> body, byte v) => body[OffFlags] = v;

        // ---- nested-item accessors (index 0..15) --------------------------------------------------
        public static byte GetItemIndex(ReadOnlySpan<byte> body, int index) =>
            body[FieldOffset(index, LockstyleItem.OffItemIndex)];
        public static void SetItemIndex(Span<byte> body, int index, byte v) =>
            body[FieldOffset(index, LockstyleItem.OffItemIndex)] = v;

        public static byte GetEquipKind(ReadOnlySpan<byte> body, int index) =>
            body[FieldOffset(index, LockstyleItem.OffEquipKind)];
        public static void SetEquipKind(Span<byte> body, int index, byte v) =>
            body[FieldOffset(index, LockstyleItem.OffEquipKind)] = v;

        public static byte GetCategory(ReadOnlySpan<byte> body, int index) =>
            body[FieldOffset(index, LockstyleItem.OffCategory)];

        public static ushort GetItemNo(ReadOnlySpan<byte> body, int index) =>
            BitConverter.ToUInt16(body.Slice(FieldOffset(index, LockstyleItem.OffItemNo), 2));
        public static void SetItemNo(Span<byte> body, int index, ushort v) =>
            BitConverter.TryWriteBytes(body.Slice(FieldOffset(index, LockstyleItem.OffItemNo), 2), v);

        public static ushort GetItemPadding01(ReadOnlySpan<byte> body, int index) =>
            BitConverter.ToUInt16(body.Slice(FieldOffset(index, LockstyleItem.OffPadding01), 2));
        public static void SetItemPadding01(Span<byte> body, int index, ushort v) =>
            BitConverter.TryWriteBytes(body.Slice(FieldOffset(index, LockstyleItem.OffPadding01), 2), v);
    }
}
