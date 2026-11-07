// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_ITEM_SAME::PacketData (0x01D),
// src/map/packets/s2c/0x01d_item_same.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cInfluenceCampaign / S2cItemMax convention.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_item_same_enum_harness.cpp -> map_s2c_item_same_enum_golden.txt), NOT hand-computed.
// The enum's literal set is pinned INDEPENDENTLY by re-parsing the packet .h (magic_enum not vendored), exactly like
// chunk 26's InfluenceMode whose enum is ALSO defined in the packet .h (not src/map/enums/).
//
// THE genuinely-new facts this chunk pins (an engine-enum body, 8th overall):
//   (1) AN ENGINE-ENUM (uint8-backed) LEADING MEMBER + a DECLARED uint8[3] padding array that EXACTLY bridges to the
//       trailing uint32's 4-alignment: State@0 (1 byte) + padding00[3]@1..3 -> Flags lands at offset 4 (already 4-aligned),
//       so the compiler inserts NO silent pad -> PAD_INSERTED=0. This re-pins chunk 29's "declared padding absorbs the
//       alignment filler" fact in the SMALLEST body, and the oracle PROVES it against a NO-PADDING counterfactual
//       {enum State; uint32 Flags}: SAME Flags offset (4) and SAME sizeof (8), but reached via 3 SILENT pad bytes
//       (NOPAD_PAD_INSERTED=3). Same Flags offset, same size -- different hole provenance. A port that dropped the
//       padding arrays as non-data still reads Flags@4 here, but only because the compiler silently reinserted exactly
//       what the engine declared -- the compiler's OFF_PD.Flags is ground truth, never the hand arithmetic.
//   (2) chunk-21 ENGINE-ENUM underlying-type substitution RE-CONFIRMED (uint8): the oracle's uint8_t-twin of the whole
//       struct is byte-identical (ESIZE_EQ/EALIGN_EQ/ESTATE_OFF_EQ all 1), so State's layout == a plain uint8; the two
//       literals (StillLoading=0 / AllLoaded=1) are cross-checked .h-re-parse == compiler-cast == this C# enum (3 ways).
//
// Layout (DEFAULT packing, body-relative). sizeof=8, alignof=4:
//   [0]       uint8  State (enum GP_SERV_COMMAND_ITEM_SAME_STATE : uint8_t)
//   [1..3]    uint8  padding00[3]   (declared real bytes; == the 3 pad bytes the no-padding twin gets SILENTLY)
//   [4..7]    uint32 Flags

using System;

namespace XI.Map
{
    // GP_SERV_COMMAND_ITEM_SAME_STATE (the engine enum defined in the packet .h). uint8-backed -> wire is one byte.
    // Literals re-parsed independently from src/map/packets/s2c/0x01d_item_same.h (magic_enum not vendored).
    public enum ItemSameState : byte
    {
        StillLoading = 0,   // ENUM_StillLoading=0
        AllLoaded    = 1,   // ENUM_AllLoaded=1
    }

    // GP_SERV_COMMAND_ITEM_SAME::PacketData (outer, body-relative, DEFAULT packing). sizeof=8, alignof=4.
    // A uint8-backed engine-enum member, a declared u8[3] padding that bridges to the trailing u32, and a uint32 Flags.
    public static class S2cItemSame
    {
        public const int SizeOf  = 8; // PD_SIZE=8 (MEMBER_BYTES==8, PAD_INSERTED==0)
        public const int AlignOf = 4; // PD_ALIGN=4 -- from the uint32 Flags (largest member align)

        // ---- uint8 State (the enum member; layout == a plain uint8, oracle ESIZE_EQ/EALIGN_EQ/ESTATE_OFF_EQ all 1) ----
        public const int OffState = 0; // OFF_PD.State=0

        // ---- uint8 padding00[3] (compiler OFF_PD.padding00=1): the DECLARED bytes that reach the 4-aligned Flags@4.
        //      The no-padding counterfactual reaches the SAME Flags@4 via 3 SILENT pad bytes (NOPAD_PAD_INSERTED=3). ----
        public const int OffPadding00 = 1;   // OFF_PD.padding00=1
        public const int Padding00Len = 3;   // ARR_padding00_COUNT=3
        // State(1) + padding00(3) == 4 == OFF_PD.Flags -- the bridge the compiler's OFF_PD.Flags confirms (PADDING_BRIDGE=4).
        public const int PaddingBridge = 4;  // OFF_PD.Flags (== sizeof(State)+sizeof(padding00)) -- compiler-verified

        // ---- uint32 Flags (compiler OFF_PD.Flags=4, already 4-aligned -> no silent pad, PAD_INSERTED=0) ----
        public const int OffFlags = 4; // OFF_PD.Flags=4

        // ---- State accessors: the wire byte read/written at the enum's underlying uint8 width ----
        public static ItemSameState GetState(ReadOnlySpan<byte> b) => (ItemSameState)b[OffState];
        public static void          SetState(Span<byte> b, ItemSameState v) => b[OffState] = (byte)v;
        // raw byte view of the enum slot (for cross-checking the compiler's uint8 underlying value).
        public static byte          GetStateRaw(ReadOnlySpan<byte> b) => b[OffState];
        public static void          SetStateRaw(Span<byte> b, byte v) => b[OffState] = v;

        // the declared padding00[3] are REAL wire bytes (== the 3 silent-pad the no-padding twin gets); expose a slice.
        public static ReadOnlySpan<byte> GetPadding00(ReadOnlySpan<byte> b) => b.Slice(OffPadding00, Padding00Len);

        // ---- uint32 Flags accessors ----
        public static uint GetFlags(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(OffFlags, 4));
        public static void SetFlags(Span<byte> b, uint v) => BitConverter.TryWriteBytes(b.Slice(OffFlags, 4), v);

        // ---- generic little-endian scalar accessors keyed by a compiler offset (cross-check surface) ----
        public static byte GetU8(ReadOnlySpan<byte> b, int off) => b[off];
        public static void SetU8(Span<byte> b, int off, byte v) => b[off] = v;
        public static uint GetU32(ReadOnlySpan<byte> b, int off) => BitConverter.ToUInt32(b.Slice(off, 4));
        public static void SetU32(Span<byte> b, int off, uint v) => BitConverter.TryWriteBytes(b.Slice(off, 4), v);
    }
}
