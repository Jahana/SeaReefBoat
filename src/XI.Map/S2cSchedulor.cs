// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_SCHEDULOR::PacketData (0x038),
// src/map/packets/s2c/0x038_schedulor.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cBattleMessage/S2cGroupTbl convention.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_schedulor_enum_harness.cpp -> map_s2c_schedulor_enum_golden.txt), NOT hand-computed.
//
// FIFTH engine-enum body and the FIRST with a UINT32-BACKED enum member -- this COMPLETES the underlying-type-
// substitution matrix that chunk 21 established: chunks 21/22/23 proved the invariant for uint8 (PartyKind),
// chunk 24 for uint16 (MsgStd), and here the oracle proves it for uint32 (FourCC): a uint32_t twin of the whole
// struct is byte-identical in size/align/id-offset (SCQ_SIZE_EQ=1/SCQ_ALIGN_EQ=1/SCQ_IDOFF_EQ=1; twin 16/4 id@8).
// So a scoped enum member's STRUCT LAYOUT equals its underlying integer for ALL THREE common widths (8/16/32).
// FourCC is also the FIRST engine enum with HEX literals (byte-swapped 4-char codes, e.g. BasicAttack=0x306B7461
// which is > int32 range, so the compiler prints (uint32_t)); the ~140-line .h re-parse path in the gate uses the
// SAME ParseCppEnum hex handling proven in chunk 24 (but the full 26-member FourCC set is ported as a C# enum and
// cross-checked three ways: compiler underlying-cast == .h re-parse == C# enum, like chunk 21/22/23's PartyKind).
//
// Layout (body-relative, default packing): UniqueNoCas@0(u32) UniqueNoTar@4(u32) id@8(FourCC/u32, packs right
//   after UniqueNoTar -- 8 already 4-aligned, no realign) ActIndexCast@12(u16) ActIndexTar@14(u16) -> 16/4
//   (14+2==16, no tail pad; struct align 4 from the u32s / u32-backed enum member).

using System;

namespace XI.Map
{
    // FourCC (src/map/enums/four_cc.h). Hand-authored uint32_t HEX literals (byte-swapped 4-char tags); the full
    // 26-member set is a wire-layout-irrelevant name->value map (magic_enum not vendored -> the .h is ground truth
    // via the gate's independent re-parse), but every value is ALSO cross-checked vs the compiler's (uint32_t) cast.
    public enum FourCC : uint
    {
        BasicAttack            = 0x306B7461,   // ENUM_BasicAttack=812348513
        SkillUse               = 0x65746163,
        SkillInterrupt         = 0x65747073,
        ItemUse                = 0x74696163,
        ItemInterrupt          = 0x74697073,
        RangedStart            = 0x676C6163,
        RangedInterrupt        = 0x676C7073,
        RangedFinish           = 0x676C6873,
        WhiteMagicCast         = 0x68776163,
        BlackMagicCast         = 0x6B626163,
        BlueMagicCast          = 0x6C626163,
        SongMagicCast          = 0x6F736163,
        NinjutsuMagicCast      = 0x6A6E6163,
        SummonMagicCast        = 0x6D736163,
        GeomancyMagicCast      = 0x65676163,
        TrustMagicCast         = 0x61666163,
        WhiteMagicInterrupt    = 0x68777073,
        BlackMagicInterrupt    = 0x6B627073,
        BlueMagicInterrupt     = 0x6C627073,
        SongMagicInterrupt     = 0x6F737073,
        NinjutsuMagicInterrupt = 0x6A6E7073,
        SummonMagicInterrupt   = 0x6D737073,
        GeomancyMagicInterrupt = 0x65677073,
        TrustMagicInterrupt    = 0x61667073,
        FadeOut                = 0x7573656B,
        Sweating               = 0x6C746968,
    }

    // GP_SERV_COMMAND_SCHEDULOR::PacketData (0x038). Body-relative, default packing. size=16, align=4.
    public static class S2cSchedulor
    {
        public const int SizeOf = 16;        // SC_SIZE=16 (14+2==16, no tail pad)
        public const int AlignOf = 4;        // SC_ALIGN=4 (from the u32s / the u32-backed enum member id)

        public const int OffUniqueNoCas = 0; // OFF_SC.UniqueNoCas=0 (u32)
        public const int OffUniqueNoTar = 4; // OFF_SC.UniqueNoTar=4 (u32)
        public const int OffId = 8;          // OFF_SC.id=8 (FourCC, underlying uint32; packs right after UniqueNoTar, 8 already 4-aligned)
        public const int OffActIndexCast = 12; // OFF_SC.ActIndexCast=12 (u16)
        public const int OffActIndexTar = 14;  // OFF_SC.ActIndexTar=14 (u16)

        // ---- u32 accessors (little-endian) --------------------------------------------------------
        public static uint GetUniqueNoCas(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffUniqueNoCas, 4));
        public static void SetUniqueNoCas(Span<byte> body, uint v) => BitConverter.TryWriteBytes(body.Slice(OffUniqueNoCas, 4), v);
        public static uint GetUniqueNoTar(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffUniqueNoTar, 4));
        public static void SetUniqueNoTar(Span<byte> body, uint v) => BitConverter.TryWriteBytes(body.Slice(OffUniqueNoTar, 4), v);

        // The uint32-backed engine-enum member: the wire is a 4-byte LE uint32 (the oracle proved a uint32_t twin
        // is byte-identical), so read the raw u32 and cast to FourCC; GetIdRaw returns the underlying bytes as a u32.
        public static FourCC GetId(ReadOnlySpan<byte> body) => (FourCC)BitConverter.ToUInt32(body.Slice(OffId, 4));
        public static void SetId(Span<byte> body, FourCC v) => BitConverter.TryWriteBytes(body.Slice(OffId, 4), (uint)v);
        public static uint GetIdRaw(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffId, 4));
        public static void SetIdRaw(Span<byte> body, uint v) => BitConverter.TryWriteBytes(body.Slice(OffId, 4), v);

        // ---- u16 accessors ------------------------------------------------------------------------
        public static ushort GetActIndexCast(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffActIndexCast, 2));
        public static void SetActIndexCast(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffActIndexCast, 2), v);
        public static ushort GetActIndexTar(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffActIndexTar, 2));
        public static void SetActIndexTar(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffActIndexTar, 2), v);
    }
}
