// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_EVENTSTR::PacketData (0x033),
// src/map/packets/s2c/0x033_eventstr.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cWpos / S2cEffect convention.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_eventstr_harness.cpp -> map_s2c_eventstr_golden.txt), NOT hand-computed.
//
// THE genuinely-new facts this chunk pins:
//   (1) FIRST 2D char array -- String is `char String[4][16]`: a ROW-major matrix of 4 rows, each a char[16]. The ROW
//       stride (16, from OFFROW.String[1]=28 vs OFF_PD.String=12) and the COL stride (1, from OFFCOL.String[0][1]=13)
//       are both compiler facts; the last element String[3][15]=75 proves contiguous row-major packing (12+3*16+15).
//       A 2D char array of char (align 1) has NO inter-element padding at any level (total 4*16==64).
//   (2) Four uint16s between the leading uint32 and the char array (ActIndex/EventNum/EventPara/Mode at 4/6/8/10,
//       dense stride 2), reaching offset 12 which is already 4-aligned so the char[4][16] (align 1) packs at 12 with
//       no pad.
//   (3) The trailing uint32 Data[8] AFTER the 2D char array: the char array ends at 12+64==76 which is ALREADY
//       4-aligned, so Data@76 needs NO silent pad (a run-extension of the chunk-38/39 already-aligned running offset).
//       Whole struct 76 + 32 == 108, alignof 4, 108%4==0 no tail pad, PAD_INSERTED=0.
//
// Layout (DEFAULT packing, body-relative). sizeof=108, alignof=4:
//   [00..03]  uint32 UniqueNo
//   [04..05]  uint16 ActIndex
//   [06..07]  uint16 EventNum
//   [08..09]  uint16 EventPara
//   [10..11]  uint16 Mode
//   [12..75]  char   String[4][16]   (4 rows x 16 cols, row stride 16, col stride 1)
//   [76..107] uint32 Data[8]         (element stride 4, no inter-element pad)

using System;

namespace XI.Map
{
    // GP_SERV_COMMAND_EVENTSTR::PacketData (outer, body-relative, DEFAULT packing). sizeof=108, alignof=4.
    public static class S2cEventStr
    {
        public const int SizeOf  = 108; // PD_SIZE=108 (MEMBER_BYTES==108, PAD_INSERTED==0)
        public const int AlignOf = 4;   // PD_ALIGN=4 -- from the uint32s

        // ---- leading u32 + four u16s (compiler OFF 0/4/6/8/10) ----
        public const int OffUniqueNo = 0;  // OFF_PD.UniqueNo=0
        public const int OffActIndex = 4;  // OFF_PD.ActIndex=4
        public const int OffEventNum = 6;  // OFF_PD.EventNum=6
        public const int OffEventPara = 8; // OFF_PD.EventPara=8
        public const int OffMode     = 10; // OFF_PD.Mode=10

        // ---- char String[4][16] (FIRST 2D char array): base 12, ROW stride 16, COL stride 1, total 64 ----
        public const int OffString     = 12;  // OFF_PD.String=12 (== Mode + 2, the four u16s reach the already-4-aligned 12)
        public const int StringRows     = 4;  // ARR_String_ROWS=4
        public const int StringRowStride = 16; // OFFROW.String[1](28) - OFF_PD.String(12) == ARR_String_ROWSTRIDE=16
        public const int StringCols      = 16; // ARR_String_COLS=16 (each row is a char[16])
        public const int StringLen       = 64; // ARR_String_LEN=64 (4*16, contiguous)

        // ---- uint32 Data[8] (trailing 1D array): base 76, element stride 4, count 8, total 32 ----
        public const int OffData      = 76; // OFF_PD.Data=76 (== String_END 76, the char array ends 4-aligned -> no pad)
        public const int DataCount    = 8;  // ARR_Data_COUNT=8
        public const int DataStride   = 4;  // DATA1(80) - OFF_PD.Data(76) == ARR_Data_ELEMSIZE=4
        public const int DataLen      = 32; // ARR_Data_LEN=32

        // ---- leading scalar accessors ----
        public static uint   GetUniqueNo(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(OffUniqueNo, 4));
        public static void   SetUniqueNo(Span<byte> b, uint v) => BitConverter.TryWriteBytes(b.Slice(OffUniqueNo, 4), v);
        public static ushort GetActIndex(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(OffActIndex, 2));
        public static void   SetActIndex(Span<byte> b, ushort v) => BitConverter.TryWriteBytes(b.Slice(OffActIndex, 2), v);
        public static ushort GetEventNum(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(OffEventNum, 2));
        public static void   SetEventNum(Span<byte> b, ushort v) => BitConverter.TryWriteBytes(b.Slice(OffEventNum, 2), v);
        public static ushort GetEventPara(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(OffEventPara, 2));
        public static void   SetEventPara(Span<byte> b, ushort v) => BitConverter.TryWriteBytes(b.Slice(OffEventPara, 2), v);
        public static ushort GetMode(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(OffMode, 2));
        public static void   SetMode(Span<byte> b, ushort v) => BitConverter.TryWriteBytes(b.Slice(OffMode, 2), v);

        // ---- 2D char array: element addressing is base + row*RowStride + col ----
        public static int StringOffset(int row, int col)
        {
            if ((uint)row >= (uint)StringRows) throw new ArgumentOutOfRangeException(nameof(row));
            if ((uint)col >= (uint)StringCols) throw new ArgumentOutOfRangeException(nameof(col));
            return OffString + row * StringRowStride + col;
        }
        public static byte GetStringChar(ReadOnlySpan<byte> b, int row, int col) => b[StringOffset(row, col)];
        public static void SetStringChar(Span<byte> b, int row, int col, byte v) => b[StringOffset(row, col)] = v;
        // a whole row as a 16-byte slice (the engine reads each String[r] as a NUL-bounded C string).
        public static ReadOnlySpan<byte> GetStringRow(ReadOnlySpan<byte> b, int row)
        {
            if ((uint)row >= (uint)StringRows) throw new ArgumentOutOfRangeException(nameof(row));
            return b.Slice(OffString + row * StringRowStride, StringRowStride);
        }
        // the entire char[4][16] matrix (64 contiguous bytes).
        public static ReadOnlySpan<byte> GetStringSlice(ReadOnlySpan<byte> b) => b.Slice(OffString, StringLen);

        // ---- trailing uint32 Data[8]: indexed u32 accessors + slice ----
        public static int DataOffset(int index)
        {
            if ((uint)index >= (uint)DataCount) throw new ArgumentOutOfRangeException(nameof(index));
            return OffData + index * DataStride;
        }
        public static uint   GetData(ReadOnlySpan<byte> b, int index) => BitConverter.ToUInt32(b.Slice(DataOffset(index), 4));
        public static void   SetData(Span<byte> b, int index, uint v) => BitConverter.TryWriteBytes(b.Slice(DataOffset(index), 4), v);
        public static ReadOnlySpan<byte> GetDataSlice(ReadOnlySpan<byte> b) => b.Slice(OffData, DataLen);

        // ---- generic little-endian scalar accessors keyed by a compiler offset (cross-check surface) ----
        public static byte   GetU8(ReadOnlySpan<byte> b, int off) => b[off];
        public static void   SetU8(Span<byte> b, int off, byte v) => b[off] = v;
        public static ushort GetU16(ReadOnlySpan<byte> b, int off) => BitConverter.ToUInt16(b.Slice(off, 2));
        public static void   SetU16(Span<byte> b, int off, ushort v) => BitConverter.TryWriteBytes(b.Slice(off, 2), v);
        public static uint   GetU32(ReadOnlySpan<byte> b, int off) => BitConverter.ToUInt32(b.Slice(off, 4));
        public static void   SetU32(Span<byte> b, int off, uint v) => BitConverter.TryWriteBytes(b.Slice(off, 4), v);
    }
}
