// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_EVENTNUM::PacketData (0x034),
// src/map/packets/s2c/0x034_eventnum.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cEventStr / S2cEffect convention.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_eventnum_harness.cpp -> map_s2c_eventnum_golden.txt), NOT hand-computed.
//
// THE genuinely-new facts this chunk pins:
//   (1) FIRST SIGNED fixed-width ARRAY element -- `num` is `int32_t num[8]`, a native signed INT32 array. Every prior
//       fixed int array in an S2C body was UNSIGNED (chunk 35 uint32[5], 36 uint8[48], 39 uint32[32], 43 uint32[8]).
//       The int32_t typedef is ALWAYS signed, so each element is read/written as a signed int (BitConverter.ToInt32),
//       and the gate proves a negative element round-trips as negative (SetNum(-1) stores 0xFFFFFFFF, GetNum reads -1,
//       NOT 4294967295).
//   (2) The signed array follows a leading uint32 SCALAR: UniqueNo@0 -> running offset 4 (already 4-aligned) so num[8]
//       packs at 4 with NO silent pad (compiler num[1]=8 pins stride 4, no inter-element pad) -- the same scalar-then-
//       array fact as chunk 35, now with a SIGNED element type.
//   (3) SIX consecutive uint16 members after the array (ActIndex/EventNum/EventPara/Mode/EventNum2/EventPara2 @36/38/
//       40/42/44/46, dense stride 2); the array ends at 4+32==36 (already 2-aligned) so the u16-run packs with no pad;
//       whole struct 36 + 12 == 48, alignof 4 (from the u32/int32), 48%4==0, PAD_INSERTED=0 (no tail pad).
//
// Layout (DEFAULT packing, body-relative). sizeof=48, alignof=4:
//   [00..03]  uint32 UniqueNo
//   [04..35]  int32  num[8]         (SIGNED, element stride 4, no inter-element pad)
//   [36..37]  uint16 ActIndex
//   [38..39]  uint16 EventNum
//   [40..41]  uint16 EventPara
//   [42..43]  uint16 Mode
//   [44..45]  uint16 EventNum2
//   [46..47]  uint16 EventPara2

using System;

namespace XI.Map
{
    // GP_SERV_COMMAND_EVENTNUM::PacketData (outer, body-relative, DEFAULT packing). sizeof=48, alignof=4.
    public static class S2cEventNum
    {
        public const int SizeOf  = 48; // PD_SIZE=48 (MEMBER_BYTES==48, PAD_INSERTED==0)
        public const int AlignOf = 4;  // PD_ALIGN=4 -- from the uint32/int32

        // ---- leading uint32 (0) ----
        public const int OffUniqueNo = 0; // OFF_PD.UniqueNo=0

        // ---- SIGNED int32 num[8]: base 4, element stride 4, count 8, total 32 ----
        public const int OffNum     = 4;  // OFF_PD.num=4 (the leading u32 reaches the already-4-aligned running offset)
        public const int NumCount    = 8;  // ARR_num_COUNT=8
        public const int NumStride   = 4;  // OFF_PD.num[1](8) - OFF_PD.num(4) == ARR_num_ELEMSIZE=4
        public const int NumLen      = 32; // ARR_num_LEN=32

        // ---- six consecutive uint16 members (36..47, dense stride 2) ----
        public const int OffActIndex   = 36; // OFF_PD.ActIndex=36 (== num array end 4+32)
        public const int OffEventNum   = 38; // OFF_PD.EventNum=38
        public const int OffEventPara  = 40; // OFF_PD.EventPara=40
        public const int OffMode       = 42; // OFF_PD.Mode=42
        public const int OffEventNum2  = 44; // OFF_PD.EventNum2=44
        public const int OffEventPara2 = 46; // OFF_PD.EventPara2=46

        // ---- leading uint32 ----
        public static uint GetUniqueNo(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(OffUniqueNo, 4));
        public static void SetUniqueNo(Span<byte> b, uint v) => BitConverter.TryWriteBytes(b.Slice(OffUniqueNo, 4), v);

        // ---- SIGNED int32 num[8] ----
        public static int  NumOffset(int index)
        {
            if ((uint)index >= (uint)NumCount) throw new ArgumentOutOfRangeException(nameof(index));
            return OffNum + index * NumStride;
        }
        public static int  GetNum(ReadOnlySpan<byte> b, int index) => BitConverter.ToInt32(b.Slice(NumOffset(index), 4));
        public static void SetNum(Span<byte> b, int index, int v) => BitConverter.TryWriteBytes(b.Slice(NumOffset(index), 4), v);
        public static ReadOnlySpan<byte> GetNumSlice(ReadOnlySpan<byte> b) => b.Slice(OffNum, NumLen);

        // ---- six uint16 accessors ----
        public static ushort GetActIndex(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(OffActIndex, 2));
        public static void   SetActIndex(Span<byte> b, ushort v) => BitConverter.TryWriteBytes(b.Slice(OffActIndex, 2), v);
        public static ushort GetEventNum(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(OffEventNum, 2));
        public static void   SetEventNum(Span<byte> b, ushort v) => BitConverter.TryWriteBytes(b.Slice(OffEventNum, 2), v);
        public static ushort GetEventPara(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(OffEventPara, 2));
        public static void   SetEventPara(Span<byte> b, ushort v) => BitConverter.TryWriteBytes(b.Slice(OffEventPara, 2), v);
        public static ushort GetMode(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(OffMode, 2));
        public static void   SetMode(Span<byte> b, ushort v) => BitConverter.TryWriteBytes(b.Slice(OffMode, 2), v);
        public static ushort GetEventNum2(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(OffEventNum2, 2));
        public static void   SetEventNum2(Span<byte> b, ushort v) => BitConverter.TryWriteBytes(b.Slice(OffEventNum2, 2), v);
        public static ushort GetEventPara2(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(OffEventPara2, 2));
        public static void   SetEventPara2(Span<byte> b, ushort v) => BitConverter.TryWriteBytes(b.Slice(OffEventPara2, 2), v);

        // ---- generic little-endian scalar accessors keyed by a compiler offset (cross-check surface) ----
        public static uint   GetU32(ReadOnlySpan<byte> b, int off) => BitConverter.ToUInt32(b.Slice(off, 4));
        public static int    GetI32(ReadOnlySpan<byte> b, int off) => BitConverter.ToInt32(b.Slice(off, 4));
        public static void   SetI32(Span<byte> b, int off, int v) => BitConverter.TryWriteBytes(b.Slice(off, 4), v);
        public static ushort GetU16(ReadOnlySpan<byte> b, int off) => BitConverter.ToUInt16(b.Slice(off, 2));
        public static void   SetU16(Span<byte> b, int off, ushort v) => BitConverter.TryWriteBytes(b.Slice(off, 2), v);
    }
}
