// C# wire model for a Phase-3 map S2C BODY struct: GP_SERV_COMMAND_PENDINGSTR::PacketData (0x05d),
// src/map/packets/s2c/0x05d_pendingstr.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cGroupEffects/S2cPendingnum convention.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_chararray_harness.cpp -> map_s2c_chararray_golden.txt), NOT hand-computed. The
// body is int32_t num[9] (36 bytes, @0, stride 4) then four char[16] buffers packed back-to-back at
// 36/52/68/84 under MSVC DEFAULT packing -> PSTR_SIZE=100 (36 + 4*16 = 100, align 4, 100 % 4 == 0 so NO
// tail pad). The char buffers sit at offsets already byte-aligned (16 divides the running total), so the
// compiler adds no padding -- but that is a fact the oracle establishes, not an assumption.

using System;
using System.Text;

namespace XI.Map
{
    public static class S2cPendingstr
    {
        public const int SizeOf = 100;      // PSTR_SIZE=100 (36 + 64, no padding)
        public const int AlignOf = 4;       // PSTR_ALIGN=4 (int32 element)

        public const int OffNum = 0;        // OFF_PSTR.num=0 (int32[9])
        public const int NumCount = 9;      // PSTR_num_count=9
        public const int NumLen = 36;       // PSTR_num_len=36

        // Four char[16] string buffers, back-to-back starting after the int32 array.
        public const int OffString0 = 36;   // OFF_PSTR.string1=36
        public const int StringCount = 4;   // string1..string4
        public const int StringLen = 16;    // PSTR_string1_len=16
        public const int StringStride = 16; // packed: sizeof(char[16]), no gap

        // ---- int32 num[9] element accessors (little-endian, CBasicPacket::ref<int32_t>) -----------
        public static int NumOffset(int index) => OffNum + index * 4;
        public static int GetNum(ReadOnlySpan<byte> body, int index) => BitConverter.ToInt32(body.Slice(NumOffset(index), 4));
        public static void SetNum(Span<byte> body, int index, int v) => BitConverter.TryWriteBytes(body.Slice(NumOffset(index), 4), v);

        // ---- char[16] string buffers --------------------------------------------------------------
        public static int StringOffset(int index) => OffString0 + index * StringStride;

        public static ReadOnlySpan<byte> GetStringBytes(ReadOnlySpan<byte> body, int index) =>
            body.Slice(StringOffset(index), StringLen);

        // Reads bytes up to the first NUL (or the buffer end). Never exceeds StringLen.
        public static string GetString(ReadOnlySpan<byte> body, int index)
        {
            var field = body.Slice(StringOffset(index), StringLen);
            int end = 0;
            while (end < field.Length && field[end] != 0) end++;
            return Encoding.ASCII.GetString(field.Slice(0, end));
        }

        // Zeroes the buffer then copies min(text.Length, StringLen) ASCII bytes.
        public static void SetString(Span<byte> body, int index, string text)
        {
            var field = body.Slice(StringOffset(index), StringLen);
            field.Clear();
            int n = Math.Min(text.Length, StringLen);
            for (int i = 0; i < n; i++) field[i] = (byte)text[i];
        }
    }
}
