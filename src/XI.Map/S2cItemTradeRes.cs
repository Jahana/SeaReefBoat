// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_ITEM_TRADE_RES::PacketData (0x022),
// src/map/packets/s2c/0x022_item_trade_res.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cItemSame / S2cWpos convention.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_item_trade_res_enum_harness.cpp -> map_s2c_item_trade_res_enum_golden.txt), NOT hand-computed.
// The enum's literal set is pinned INDEPENDENTLY by re-parsing the packet .h (magic_enum not vendored), like chunks 26/37/38
// (all three enums are DEFINED IN THE PACKET .h, not src/map/enums/).
//
// THE genuinely-new facts this chunk pins:
//   (1) FIRST FIXED-WIDTH S2C body with a COMPILER-INSERTED TAIL PAD: UniqueNo(u32)@0, Kind(enum : uint32_t)@4,
//       ActIndex(u16)@8 -> named members end at 10, but the whole-struct alignof is 4 (raised by the u32 scalar AND the
//       u32-backed enum), so MSVC pads the SIZE up to 12 -> a 2-byte SILENT tail pad (PAD_INSERTED=2, HAS_TAIL_PAD=1).
//       (message/pendingstr also carry tail pad, but those are char[] string bodies -- this is the first PLAIN scalar-
//       struct whose trailing bytes are compiler pad, NOT a declared array.) The compiler's PD_SIZE (12) is ground truth;
//       the trailing pad is still part of the 12-byte wire body the client reads, so it is NOT droppable.
//   (2) A UINT32-BACKED ENGINE ENUM MID-STRUCT re-uses chunk-21 underlying-type substitution (EQ_SIZE/EQ_ALIGN/EKIND_OFF
//       all 1 vs a uint32_t twin) -- the enum member contributes exactly its underlying uint32's 4 bytes + 4 alignment,
//       which is precisely what raises the whole struct to align 4 and forces the tail pad. Its 10 sequential literals
//       (Start=0..End=9) are cross-checked .h-re-parse == compiler (uint32_t)-cast == this C# enum (3 ways).
//
// Layout (DEFAULT packing, body-relative). sizeof=12, alignof=4:
//   [0..3]   uint32 UniqueNo
//   [4..7]   uint32 Kind (enum GP_ITEM_TRADE_RES_KIND : uint32_t)
//   [8..9]   uint16 ActIndex
//   [10..11] <compiler SILENT tail pad, 2 bytes>   (rounds the struct to its 4-alignment; still part of the wire body)

using System;

namespace XI.Map
{
    // GP_ITEM_TRADE_RES_KIND (the engine enum defined in the packet .h 0x022_item_trade_res.h). uint32-backed -> one LE uint32.
    // Members use the .h's VERBATIM PascalCase spellings so VerifyPacketIds' case-sensitive reflection match holds
    // (the chunk-38 lesson: rename the C# enum, never the wire). Literals re-parsed independently from the packet .h.
    public enum ItemTradeResKind : uint
    {
        Start          = 0,   // ENUM_Start=0
        Cancell        = 1,   // ENUM_Cancell=1
        Make           = 2,   // ENUM_Make=2
        MakeCancell    = 3,   // ENUM_MakeCancell=3
        ErrEtc         = 4,   // ENUM_ErrEtc=4
        ErrNoSearchYou = 5,   // ENUM_ErrNoSearchYou=5
        ErrNowReq      = 6,   // ENUM_ErrNowReq=6
        ErrYouTrade    = 7,   // ENUM_ErrYouTrade=7
        ErrLogout      = 8,   // ENUM_ErrLogout=8
        End            = 9,   // ENUM_End=9
    }

    // GP_SERV_COMMAND_ITEM_TRADE_RES::PacketData (outer, body-relative, DEFAULT packing). sizeof=12, alignof=4.
    // u32 + a u32-backed engine enum + a trailing u16 -> 10 named bytes, but 2 compiler TAIL PAD bytes round it to 12.
    public static class S2cItemTradeRes
    {
        public const int SizeOf  = 12; // PD_SIZE=12 (MEMBER_BYTES==10, PAD_INSERTED==2 -- HAS_TAIL_PAD)
        public const int AlignOf = 4;  // PD_ALIGN=4 -- from the u32 scalar AND the u32-backed enum member

        // ---- uint32 UniqueNo (compiler OFF_PD.UniqueNo=0) ----
        public const int OffUniqueNo = 0; // OFF_PD.UniqueNo=0

        // ---- uint32-backed engine enum Kind (compiler OFF_PD.Kind=4; underlying size 4, raises the whole struct to 4-align) ----
        public const int OffKind = 4; // OFF_PD.Kind=4

        // ---- uint16 ActIndex (compiler OFF_PD.ActIndex=8); the LAST named member ends at 10, then 2 SILENT tail-pad bytes ----
        public const int OffActIndex = 8;         // OFF_PD.ActIndex=8
        public const int LastMemberRunOffset = 10; // LAST_MEMBER_RUN_OFFSET (8 + 2), NOT 4-aligned (RUN_MOD_ALIGN=2)
        public const int TailPadBytes = 2;         // TAIL_PAD_BYTES == PD_SIZE - 10 (the compiler's silent tail pad)

        // ---- uint32 UniqueNo accessors ----
        public static uint GetUniqueNo(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(OffUniqueNo, 4));
        public static void SetUniqueNo(Span<byte> b, uint v) => BitConverter.TryWriteBytes(b.Slice(OffUniqueNo, 4), v);

        // ---- Kind: the enum read/written at its underlying uint32 width (chunk-21 substitution: layout == a plain uint32) ----
        public static ItemTradeResKind GetKind(ReadOnlySpan<byte> b) => (ItemTradeResKind)BitConverter.ToUInt32(b.Slice(OffKind, 4));
        public static void             SetKind(Span<byte> b, ItemTradeResKind v) => BitConverter.TryWriteBytes(b.Slice(OffKind, 4), (uint)v);
        public static uint             GetKindRaw(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(OffKind, 4));

        // ---- uint16 ActIndex accessors ----
        public static ushort GetActIndex(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(OffActIndex, 2));
        public static void   SetActIndex(Span<byte> b, ushort v) => BitConverter.TryWriteBytes(b.Slice(OffActIndex, 2), v);

        // ---- generic little-endian scalar accessors keyed by a compiler offset (cross-check surface) ----
        public static uint   GetU32(ReadOnlySpan<byte> b, int off) => BitConverter.ToUInt32(b.Slice(off, 4));
        public static void   SetU32(Span<byte> b, int off, uint v) => BitConverter.TryWriteBytes(b.Slice(off, 4), v);
        public static ushort GetU16(ReadOnlySpan<byte> b, int off) => BitConverter.ToUInt16(b.Slice(off, 2));
        public static void   SetU16(Span<byte> b, int off, ushort v) => BitConverter.TryWriteBytes(b.Slice(off, 2), v);
    }
}
