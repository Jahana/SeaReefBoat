// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_WPOS::PacketData (0x05B),
// src/map/packets/s2c/0x05b_wpos.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cItemSame / S2cEventucoff convention.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_wpos_enum_harness.cpp -> map_s2c_wpos_enum_golden.txt), NOT hand-computed.
// The POSMODE enum's literal set is pinned INDEPENDENTLY by re-parsing the packet .h (magic_enum not vendored), like
// chunk 37's item_same and chunk 26's influence mode (all defined IN the packet .h, not src/map/enums/).
//
// THE genuinely-new facts this chunk pins:
//   (1) FIRST S2C body LEADING with FLOATs -- x/y/z (IEEE-754) at the struct start, each 4/4, packed 0/4/8 with no
//       inter-float pad (floats lived only in the C2S pos body before, chunk 4). The whole-struct align (4) comes from
//       the floats + uint32s (all align 4); the compiler owns every offset.
//   (2) A SIGNED `char dir` member (oracle CHAR_IS_SIGNED=1) -- read/written as an int8 (sbyte), NOT a uint8. A new
//       fixed-width member SIGN distinct from the u8/i32-typedef scalars elsewhere.
//   (3) An ENGINE-ENUM member (Posmode : uint8, the 9 non-sequential literals NORMAL=0..ROTATE=0x0A skipping 0x04/0x07)
//       MID-STRUCT; chunk-21 underlying-type substitution RE-CONFIRMED vs a uint8_t twin of the WHOLE struct
//       (EQ_SIZE/EQ_ALIGN/EMODE_OFF all 1). Posmode is defined IN the packet .h, so the .h re-parse path is that .h.
//   (4) THE trailing-uint32-after-u8-pair fact: Mode@18 + dir@19 reach the running offset 20, ALREADY 4-aligned, so the
//       trailing uint32 padding18 lands at 20 with NO silent pad -> PAD_INSERTED=0 (the 18+2==20 "already aligned" run).
//
// Layout (DEFAULT packing, body-relative). sizeof=24, alignof=4:
//   [00..03]  float  x
//   [04..07]  float  y
//   [08..11]  float  z
//   [12..15]  uint32 UniqueNo
//   [16..17]  uint16 ActIndex
//   [18]      uint8  Mode (enum Posmode : uint8)
//   [19]      int8   dir  (signed char)
//   [20..23]  uint32 padding18   (real wire bytes the client reads)

using System;

namespace XI.Map
{
    // POSMODE (the engine enum defined in the packet .h 0x05b_wpos.h). uint8-backed -> wire is one byte.
    // Literals re-parsed independently from the packet .h (magic_enum not vendored). NON-sequential: skips 0x04 / 0x07.
    // Members use the .h's VERBATIM UPPERCASE spellings so VerifyPacketIds' independent .h re-parse + reflection
    // cross-check (case-sensitive name match) holds, exactly like chunk 26/37's InfluenceMode / ItemSameState.
    // REUSABLE XI.Map surface -- the 0x065 wpos2 sibling consumes the SAME enum without redefining it.
    public enum Posmode : byte
    {
        NORMAL      = 0x00,   // ENUM_NORMAL=0
        EVENT       = 0x01,   // ENUM_EVENT=1
        CLEAR       = 0x02,   // ENUM_CLEAR=2
        POP         = 0x03,   // ENUM_POP=3
        RESET       = 0x05,   // ENUM_RESET=5
        MATERIALIZE = 0x06,   // ENUM_MATERIALIZE=6
        LOCK        = 0x08,   // ENUM_LOCK=8
        UNLOCK      = 0x09,   // ENUM_UNLOCK=9
        ROTATE      = 0x0A,   // ENUM_ROTATE=10
    }

    // GP_SERV_COMMAND_WPOS::PacketData (outer, body-relative, DEFAULT packing). sizeof=24, alignof=4.
    // Three leading floats + u32 + u16 + a uint8 enum + a signed char + a trailing u32 (all natural alignment, hole-free).
    public static class S2cWpos
    {
        public const int SizeOf  = 24; // PD_SIZE=24 (MEMBER_BYTES==24, PAD_INSERTED==0)
        public const int AlignOf = 4;  // PD_ALIGN=4 -- from the floats + uint32s (largest member align)

        // ---- float x/y/z (compiler OFF 0/4/8; the first S2C floats) ----
        public const int OffX = 0;  // OFF_PD.x=0
        public const int OffY = 4;  // OFF_PD.y=4
        public const int OffZ = 8;  // OFF_PD.z=8

        // ---- uint32 UniqueNo (12), uint16 ActIndex (16) ----
        public const int OffUniqueNo = 12; // OFF_PD.UniqueNo=12
        public const int OffActIndex = 16; // OFF_PD.ActIndex=16

        // ---- uint8 engine-enum Mode (18) + SIGNED char dir (19) ----
        public const int OffMode = 18; // OFF_PD.Mode=18 (Posmode, underlying uint8)
        public const int OffDir  = 19; // OFF_PD.dir=19 (signed char -> sbyte)

        // ---- uint32 padding18 (20): the trailing u32 lands at the already-4-aligned running offset (Mode+dir==20). ----
        public const int OffPadding18 = 20; // OFF_PD.padding18=20 (== Mode + 1 + 1, gapless to 24)

        // ---- float accessors (IEEE-754 LE, via BitConverter) ----
        public static float GetX(ReadOnlySpan<byte> b) => BitConverter.ToSingle(b.Slice(OffX, 4));
        public static void  SetX(Span<byte> b, float v) => BitConverter.TryWriteBytes(b.Slice(OffX, 4), v);
        public static float GetY(ReadOnlySpan<byte> b) => BitConverter.ToSingle(b.Slice(OffY, 4));
        public static void  SetY(Span<byte> b, float v) => BitConverter.TryWriteBytes(b.Slice(OffY, 4), v);
        public static float GetZ(ReadOnlySpan<byte> b) => BitConverter.ToSingle(b.Slice(OffZ, 4));
        public static void  SetZ(Span<byte> b, float v) => BitConverter.TryWriteBytes(b.Slice(OffZ, 4), v);

        // ---- scalar accessors ----
        public static uint   GetUniqueNo(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(OffUniqueNo, 4));
        public static void   SetUniqueNo(Span<byte> b, uint v) => BitConverter.TryWriteBytes(b.Slice(OffUniqueNo, 4), v);
        public static ushort GetActIndex(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(OffActIndex, 2));
        public static void   SetActIndex(Span<byte> b, ushort v) => BitConverter.TryWriteBytes(b.Slice(OffActIndex, 2), v);

        // ---- Mode: the enum byte read/written at the underlying uint8 width ----
        public static Posmode GetMode(ReadOnlySpan<byte> b) => (Posmode)b[OffMode];
        public static void    SetMode(Span<byte> b, Posmode v) => b[OffMode] = (byte)v;
        public static byte    GetModeRaw(ReadOnlySpan<byte> b) => b[OffMode];

        // ---- dir: a SIGNED char -> sbyte (oracle CHAR_IS_SIGNED=1) ----
        public static sbyte GetDir(ReadOnlySpan<byte> b) => unchecked((sbyte)b[OffDir]);
        public static void  SetDir(Span<byte> b, sbyte v) => b[OffDir] = unchecked((byte)v);

        // ---- trailing padding18 u32 (real wire bytes, NOT droppable) ----
        public static uint GetPadding18(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(OffPadding18, 4));
        public static void SetPadding18(Span<byte> b, uint v) => BitConverter.TryWriteBytes(b.Slice(OffPadding18, 4), v);

        // ---- generic little-endian scalar accessors keyed by a compiler offset (cross-check surface) ----
        public static float  GetFloat(ReadOnlySpan<byte> b, int off) => BitConverter.ToSingle(b.Slice(off, 4));
        public static void   SetFloat(Span<byte> b, int off, float v) => BitConverter.TryWriteBytes(b.Slice(off, 4), v);
        public static byte   GetU8(ReadOnlySpan<byte> b, int off) => b[off];
        public static void   SetU8(Span<byte> b, int off, byte v) => b[off] = v;
        public static ushort GetU16(ReadOnlySpan<byte> b, int off) => BitConverter.ToUInt16(b.Slice(off, 2));
        public static uint   GetU32(ReadOnlySpan<byte> b, int off) => BitConverter.ToUInt32(b.Slice(off, 4));
    }
}
