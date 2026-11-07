// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_WPOS2::PacketData (0x065),
// src/map/packets/s2c/0x065_wpos2.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cWpos / S2cItemSame convention.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_wpos2_harness.cpp -> map_s2c_wpos2_golden.txt), NOT hand-computed.
//
// THE genuinely-new fact this chunk pins: wpos2 is the 0x05b wpos SIBLING -- an IDENTICAL 24/4 layout -- but here
// `Mode` is declared as a PLAIN `uint8_t` (the ctor takes a POSMODE argument, yet the PacketData member is uint8_t).
// The oracle proves the chunk-21 underlying-type-substitution invariant on a REAL ENGINE PAIR (two actual engine
// structs, PD_WPOS with `POSMODE Mode` vs PD_WPOS2 with `uint8_t Mode`, sharing every slot): PAIR_EQ_SIZE/ALIGN and
// every PAIR_EQ_<member> offset flag (incl. PAIR_EQ_Mode) are all 1, and the two byte-fills are identical. Because
// 0x065_wpos2.h declares NO engine-enum member, this port needs NO .h enum re-parse -- Mode is modelled as a plain
// byte exactly as the struct declares it (the client reads one raw uint8).
//
// Layout (DEFAULT packing, body-relative). sizeof=24, alignof=4:
//   [00..03]  float  x
//   [04..07]  float  y
//   [08..11]  float  z
//   [12..15]  uint32 UniqueNo
//   [16..17]  uint16 ActIndex
//   [18]      uint8  Mode    (plain uint8 here; enum POSMODE in the 0x05b sibling -- byte-identical slot)
//   [19]      int8   dir     (signed char)
//   [20..23]  uint32 padding18   (real wire bytes the client reads)

using System;

namespace XI.Map
{
    // GP_SERV_COMMAND_WPOS2::PacketData (outer, body-relative, DEFAULT packing). sizeof=24, alignof=4.
    // Three leading floats + u32 + u16 + a plain uint8 Mode + a signed char + a trailing u32 (all natural, hole-free).
    public static class S2cWpos2
    {
        public const int SizeOf  = 24; // PD_SIZE=24 (MEMBER_BYTES==24, PAD_INSERTED==0)
        public const int AlignOf = 4;  // PD_ALIGN=4 -- from the floats + uint32s (largest member align)

        // ---- float x/y/z (compiler OFF 0/4/8) ----
        public const int OffX = 0;  // OFF_PD.x=0
        public const int OffY = 4;  // OFF_PD.y=4
        public const int OffZ = 8;  // OFF_PD.z=8

        // ---- uint32 UniqueNo (12), uint16 ActIndex (16) ----
        public const int OffUniqueNo = 12; // OFF_PD.UniqueNo=12
        public const int OffActIndex = 16; // OFF_PD.ActIndex=16

        // ---- PLAIN uint8 Mode (18) + SIGNED char dir (19) ----
        public const int OffMode = 18; // OFF_PD.Mode=18 (uint8_t; the 0x05b sibling's POSMODE enum is byte-identical here)
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

        // ---- Mode: a PLAIN uint8 (0x065 declares uint8_t Mode, not the POSMODE enum). ----
        public static byte GetMode(ReadOnlySpan<byte> b) => b[OffMode];
        public static void SetMode(Span<byte> b, byte v) => b[OffMode] = v;

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
