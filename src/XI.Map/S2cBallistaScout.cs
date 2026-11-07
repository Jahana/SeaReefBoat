// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_BALLISTA::SCOUT::PacketData (0x0e6),
// src/map/packets/s2c/0x0e6_ballista.h (the Mode=3 "Scout Response" sub-struct of a DISCRIMINATED opcode -- the same
// opcode GP_SERV_COMMAND_BALLISTA also carries a SCOREBOARD PacketData, mirroring the 0x071 influence campaign/
// colonization pair). Body-only (the engine's data<T>() places PacketData at buffer_.data() + sizeof(GP_SERV_HEADER)
// == 4), matching the S2cWpos / S2cItemSearch convention.
//
// CRITICAL METHODOLOGY: every offset/size/bit-field byte+mask+shift below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_ballista_scout_harness.cpp -> map_s2c_ballista_scout_golden.txt), NOT hand-computed. The
// bit-field byte+shift+straddle are SENTINEL-SCAN derived (offsetof is ILLEGAL on bit-fields -- C2618).
//
// THE genuinely-new facts this chunk pins:
//   (1) FIRST float[] ARRAY EVER -- RookPosition is `float RookPosition[4]` (compiler OFF 12, stride 4 via [1]=16,
//       LEN 16). Prior floats were only three scalars (wpos x/y/z) or a float<->u32 pun (action union arm); an indexed
//       float array reads each element via BitConverter.ToSingle at base + i*4.
//   (2) A leading `uint16` bit-field run (Flags:6 + Mode:10 == exactly 16 bits, ONE uint16 allocation unit at body byte
//       0) where `Mode` STRADDLES the byte 0/1 boundary. The sentinel scan proves: Flags byte=0 shift=0 mask=0x3F
//       (non-straddle); Mode byte=0 shift=6 straddle=1 (bits 6..15 cross into byte 1). So BOTH fields must be read via
//       the WHOLE uint16 (BitConverter.ToUInt16 of bytes 0..1) then shifted+masked -- a per-byte mask on Mode would
//       read only bits 6..7 (max 3), losing bits 8..15 (the gate's negative check).
//   (3) The whole struct alignof 4 is driven by the FLOATS + uint32s (largest member align), NOT the leading u16
//       bit-field word; offsets are compiler-pinned hole-free: bitword(2) + padding06(u16)@2 reach offset 4,
//       RookUniqueNo(u32)@4, padding0C(u32)@8, RookPosition(float[4])@12 (already 4-aligned -> no pad),
//       RookDistance(float)@28, 28+4==32, 32%4==0 -> PAD_INSERTED==0 (the gapless i*7+3 32-byte fill proves it).
//
// Layout (DEFAULT packing, body-relative). sizeof=32, alignof=4:
//   [00..01]  uint16 bitword  : Flags (bits 0..5, mask 0x003F) + Mode (bits 6..15, STRADDLES bytes 0/1, mask 0xFFC0)
//   [02..03]  uint16 padding06
//   [04..07]  uint32 RookUniqueNo
//   [08..11]  uint32 padding0C
//   [12..27]  float  RookPosition[4]   (the first float array; stride 4)
//   [28..31]  float  RookDistance

using System;

namespace XI.Map
{
    // GP_SERV_COMMAND_BALLISTA::SCOUT::PacketData (outer, body-relative, DEFAULT packing). sizeof=32, alignof=4.
    public static class S2cBallistaScout
    {
        public const int SizeOf  = 32; // PD_SIZE=32 (MEMBER_BYTES==32, PAD_INSERTED==0)
        public const int AlignOf = 4;  // PD_ALIGN=4 -- from the floats + uint32s (NOT the leading u16 bitword)

        // ---- leading uint16 bit-field run (sentinel-scan derived; offsetof illegal on bit-fields) ----
        public const int OffBitword = 0;             // the bit-field run sits at body byte 0
        public const int FlagsShift = 0;             // BF_Flags_shift=0
        public const uint FlagsMask = 0x003Fu;       // BF_Flags_lowmask=0x3F (width 6)
        public const int FlagsWidth = 6;             // BF_Flags width
        public const int ModeShift = 6;              // BF_Mode_shift=6 (within the whole uint16)
        public const uint ModeMask = 0x3FFu;         // width 10 -> (1<<10)-1 (the whole-word read; NOT a per-byte mask)
        public const int ModeWidth = 10;             // BF_Mode width
        public const bool ModeStraddles = true;      // BF_Mode_straddle=1 (bits 6..15 cross byte 0/1)

        // ---- scalar + float-array members ----
        public const int OffPadding06   = 2;  // OFF_PD.padding06=2
        public const int OffRookUniqueNo = 4; // OFF_PD.RookUniqueNo=4
        public const int OffPadding0C    = 8; // OFF_PD.padding0C=8
        public const int OffRookPosition = 12;// OFF_PD.RookPosition=12 (the first float array; already 4-aligned -> no pad)
        public const int RookPosCount    = 4;  // ARR_RookPosition_COUNT=4
        public const int RookPosStride   = 4;  // OFF_PD.RookPosition[1](16) - OFF_PD.RookPosition(12) == ARR ELEMSIZE=4
        public const int RookPosLen      = 16; // ARR_RookPosition_LEN=16
        public const int OffRookDistance = 28; // OFF_PD.RookDistance=28 (28+4==32, gapless to the end)

        // ---- the bit-field word: BOTH fields read via the WHOLE uint16 (Mode is a straddler) ----
        public static ushort GetBitword(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(OffBitword, 2));
        public static void   SetBitword(Span<byte> b, ushort v) => BitConverter.TryWriteBytes(b.Slice(OffBitword, 2), v);

        public static uint GetFlags(ReadOnlySpan<byte> b) => ((uint)GetBitword(b) >> FlagsShift) & FlagsMask;
        public static void SetFlags(Span<byte> b, uint v)
        {
            ushort w = GetBitword(b);
            w = (ushort)((w & ~(ushort)(FlagsMask << FlagsShift)) | ((ushort)(v & FlagsMask) << FlagsShift));
            SetBitword(b, w);
        }

        public static uint GetMode(ReadOnlySpan<byte> b) => ((uint)GetBitword(b) >> ModeShift) & ModeMask;
        public static void SetMode(Span<byte> b, uint v)
        {
            ushort w = GetBitword(b);
            w = (ushort)((w & ~(ushort)(ModeMask << ModeShift)) | ((ushort)(v & ModeMask) << ModeShift));
            SetBitword(b, w);
        }

        // ---- scalar accessors ----
        public static ushort GetPadding06(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(OffPadding06, 2));
        public static void   SetPadding06(Span<byte> b, ushort v) => BitConverter.TryWriteBytes(b.Slice(OffPadding06, 2), v);
        public static uint   GetRookUniqueNo(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(OffRookUniqueNo, 4));
        public static void   SetRookUniqueNo(Span<byte> b, uint v) => BitConverter.TryWriteBytes(b.Slice(OffRookUniqueNo, 4), v);
        public static uint   GetPadding0C(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(OffPadding0C, 4));
        public static void   SetPadding0C(Span<byte> b, uint v) => BitConverter.TryWriteBytes(b.Slice(OffPadding0C, 4), v);

        // ---- float RookPosition[4] (the first float array) ----
        public static int RookPositionOffset(int index)
        {
            if ((uint)index >= (uint)RookPosCount) throw new ArgumentOutOfRangeException(nameof(index));
            return OffRookPosition + index * RookPosStride;
        }
        public static float GetRookPosition(ReadOnlySpan<byte> b, int index) => BitConverter.ToSingle(b.Slice(RookPositionOffset(index), 4));
        public static void  SetRookPosition(Span<byte> b, int index, float v) => BitConverter.TryWriteBytes(b.Slice(RookPositionOffset(index), 4), v);
        public static ReadOnlySpan<byte> GetRookPositionSlice(ReadOnlySpan<byte> b) => b.Slice(OffRookPosition, RookPosLen);

        public static float GetRookDistance(ReadOnlySpan<byte> b) => BitConverter.ToSingle(b.Slice(OffRookDistance, 4));
        public static void  SetRookDistance(Span<byte> b, float v) => BitConverter.TryWriteBytes(b.Slice(OffRookDistance, 4), v);

        // ---- generic little-endian scalar accessors keyed by a compiler offset (cross-check surface) ----
        public static float  GetFloat(ReadOnlySpan<byte> b, int off) => BitConverter.ToSingle(b.Slice(off, 4));
        public static void   SetFloat(Span<byte> b, int off, float v) => BitConverter.TryWriteBytes(b.Slice(off, 4), v);
        public static ushort GetU16(ReadOnlySpan<byte> b, int off) => BitConverter.ToUInt16(b.Slice(off, 2));
        public static void   SetU16(Span<byte> b, int off, ushort v) => BitConverter.TryWriteBytes(b.Slice(off, 2), v);
        public static uint   GetU32(ReadOnlySpan<byte> b, int off) => BitConverter.ToUInt32(b.Slice(off, 4));
        public static void   SetU32(Span<byte> b, int off, uint v) => BitConverter.TryWriteBytes(b.Slice(off, 4), v);
    }
}
