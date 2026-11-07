// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_EFFECT::PacketData (0x030),
// src/map/packets/s2c/0x030_effect.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cWpos / S2cItemSame convention.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_effect_enum_harness.cpp -> map_s2c_effect_enum_golden.txt), NOT hand-computed.
// The SynthesisEffect enum's literal set is pinned INDEPENDENTLY by re-parsing src/map/enums/synthesis_effect.h
// (magic_enum not vendored), like the other engine-enum bodies.
//
// THE genuinely-new facts this chunk pins:
//   (1) FIRST engine enum with a SIGNED underlying type -- EffectNum is SynthesisEffect = `enum class : int16_t`.
//       Every prior engine-enum body used an UNSIGNED underlying (uint8/uint16/uint32). The chunk-21 underlying-type
//       substitution is RE-CONFIRMED against an int16_t twin (EQ_SIZE/EQ_ALIGN/EEFFECTNUM_OFF/ETIMER_OFF all 1) --
//       signedness of the underlying type is irrelevant to STRUCT LAYOUT (size 2 / align 2 / same offset). All 9
//       literals (None=0x0000..Dark=0x0017) are positive, but the wire value is a SIGNED int16, so the accessor reads
//       BitConverter.ToInt16 (a short), never an unsigned ushort.
//   (2) TWO explicitly-declared int8_t members (Type, Status) -- signed 1-byte members read/written as sbyte. (Unlike
//       the wpos `char dir`, which relies on CHAR_IS_SIGNED=1, the int8_t typedef is ALWAYS signed.) The two int8s
//       sit at 8/9, so the running offset 10 is already 2-aligned -> trailing uint16 Timer@10 needs NO silent pad.
//   (3) The int16-backed enum between two u16s (ActIndex@4 / Timer@10): EffectNum@6 packs tight (6 already 2-aligned),
//       struct align 4 (from UniqueNo), size 12 (10+2==12), 12%4==0 -> no tail pad, PAD_INSERTED=0.
//
// Layout (DEFAULT packing, body-relative). sizeof=12, alignof=4:
//   [00..03]  uint32        UniqueNo
//   [04..05]  uint16        ActIndex
//   [06..07]  int16         EffectNum  (enum SynthesisEffect : int16_t -- the first SIGNED-underlying engine enum)
//   [08]      int8          Type
//   [09]      int8          Status
//   [10..11]  uint16        Timer   (real wire bytes the client reads)

using System;

namespace XI.Map
{
    // SynthesisEffect (src/map/enums/synthesis_effect.h). int16-backed -> the FIRST engine enum with a SIGNED
    // underlying type; the wire value is a signed short. Literals re-parsed independently from the enum header.
    // Members use the .h's VERBATIM PascalCase spellings so VerifyPacketIds' case-sensitive reflection cross-check
    // holds (the chunk-38 lesson).
    public enum SynthesisEffect : short
    {
        None      = 0x0000,   // ENUM_None=0
        Water     = 0x0010,   // ENUM_Water=16
        Wind      = 0x0011,   // ENUM_Wind=17
        Fire      = 0x0012,   // ENUM_Fire=18
        Earth     = 0x0013,   // ENUM_Earth=19
        Lightning = 0x0014,   // ENUM_Lightning=20
        Ice       = 0x0015,   // ENUM_Ice=21
        Light     = 0x0016,   // ENUM_Light=22
        Dark      = 0x0017,   // ENUM_Dark=23
    }

    // GP_SERV_COMMAND_EFFECT::PacketData (outer, body-relative, DEFAULT packing). sizeof=12, alignof=4.
    public static class S2cEffect
    {
        public const int SizeOf  = 12; // PD_SIZE=12 (MEMBER_BYTES==12, PAD_INSERTED==0)
        public const int AlignOf = 4;  // PD_ALIGN=4 -- from the uint32 UniqueNo

        // ---- uint32 UniqueNo (0), uint16 ActIndex (4) ----
        public const int OffUniqueNo = 0;  // OFF_PD.UniqueNo=0
        public const int OffActIndex = 4;  // OFF_PD.ActIndex=4

        // ---- int16-backed engine enum EffectNum (6) -- the first SIGNED-underlying engine enum ----
        public const int OffEffectNum = 6; // OFF_PD.EffectNum=6 (SynthesisEffect, underlying int16_t)

        // ---- two explicitly-signed int8_t members (8, 9) ----
        public const int OffType   = 8;  // OFF_PD.Type=8   (int8_t -> sbyte)
        public const int OffStatus = 9;  // OFF_PD.Status=9 (int8_t -> sbyte)

        // ---- uint16 Timer (10): the two int8s reach the already-2-aligned running offset 10 -> no silent pad. ----
        public const int OffTimer = 10; // OFF_PD.Timer=10 (== Type + 1 + 1, gapless to 12)

        // ---- uint32 / uint16 accessors ----
        public static uint   GetUniqueNo(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(OffUniqueNo, 4));
        public static void   SetUniqueNo(Span<byte> b, uint v) => BitConverter.TryWriteBytes(b.Slice(OffUniqueNo, 4), v);
        public static ushort GetActIndex(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(OffActIndex, 2));
        public static void   SetActIndex(Span<byte> b, ushort v) => BitConverter.TryWriteBytes(b.Slice(OffActIndex, 2), v);

        // ---- EffectNum: the SIGNED int16-backed enum -- read as a short (never ushort) ----
        public static SynthesisEffect GetEffectNum(ReadOnlySpan<byte> b) => (SynthesisEffect)BitConverter.ToInt16(b.Slice(OffEffectNum, 2));
        public static void            SetEffectNum(Span<byte> b, SynthesisEffect v) => BitConverter.TryWriteBytes(b.Slice(OffEffectNum, 2), (short)v);
        public static short           GetEffectNumRaw(ReadOnlySpan<byte> b) => BitConverter.ToInt16(b.Slice(OffEffectNum, 2));
        public static void            SetEffectNumRaw(Span<byte> b, short v) => BitConverter.TryWriteBytes(b.Slice(OffEffectNum, 2), v);

        // ---- Type / Status: explicitly-declared int8_t -> sbyte (ALWAYS signed, unlike a plain char) ----
        public static sbyte GetType(ReadOnlySpan<byte> b) => unchecked((sbyte)b[OffType]);
        public static void  SetType(Span<byte> b, sbyte v) => b[OffType] = unchecked((byte)v);
        public static sbyte GetStatus(ReadOnlySpan<byte> b) => unchecked((sbyte)b[OffStatus]);
        public static void  SetStatus(Span<byte> b, sbyte v) => b[OffStatus] = unchecked((byte)v);

        // ---- uint16 Timer (real wire bytes, NOT droppable) ----
        public static ushort GetTimer(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(OffTimer, 2));
        public static void   SetTimer(Span<byte> b, ushort v) => BitConverter.TryWriteBytes(b.Slice(OffTimer, 2), v);

        // ---- generic little-endian scalar accessors keyed by a compiler offset (cross-check surface) ----
        public static byte   GetU8(ReadOnlySpan<byte> b, int off) => b[off];
        public static sbyte  GetI8(ReadOnlySpan<byte> b, int off) => unchecked((sbyte)b[off]);
        public static void   SetU8(Span<byte> b, int off, byte v) => b[off] = v;
        public static ushort GetU16(ReadOnlySpan<byte> b, int off) => BitConverter.ToUInt16(b.Slice(off, 2));
        public static short  GetI16(ReadOnlySpan<byte> b, int off) => BitConverter.ToInt16(b.Slice(off, 2));
        public static uint   GetU32(ReadOnlySpan<byte> b, int off) => BitConverter.ToUInt32(b.Slice(off, 4));
    }
}
