// C# wire model for a Phase-3 map S2C BODY struct: GP_SERV_COMMAND_MESSAGE::PacketData (0x009),
// src/map/packets/s2c/0x009_message.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cGroupEffects/S2cPendingnum convention.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_chararray_harness.cpp -> map_s2c_chararray_golden.txt), NOT hand-computed. This
// is the first Phase-3 body with a char[] string field. Its {u32 UniqueNo; u16 ActIndex; u16 MesNo;
// u8 Attr; char Data[128]} prefix is 9 bytes, so Data lands at the UNALIGNED offset 9 (a hand-reasoner
// padding it to 4 would wrongly say 12); the member span 9+128=137 is then rounded by the struct's
// alignof (4) up to MSG_SIZE=140 -- 3 compiler-owned TAIL-PAD bytes. The whole-body i*7+3 fill proves it.

using System;
using System.Text;

namespace XI.Map
{
    public static class S2cMessage
    {
        public const int SizeOf = 140;      // MSG_SIZE=140 (137 member span -> 3 tail-pad bytes)
        public const int AlignOf = 4;       // MSG_ALIGN=4 (from the leading uint32)

        public const int OffUniqueNo = 0;   // OFF_MSG.UniqueNo=0 (uint32)
        public const int OffActIndex = 4;   // OFF_MSG.ActIndex=4 (uint16)
        public const int OffMesNo = 6;      // OFF_MSG.MesNo=6 (uint16)
        public const int OffAttr = 8;       // OFF_MSG.Attr=8 (uint8)
        public const int OffData = 9;       // OFF_MSG.Data=9 (char[128]) -- UNALIGNED start
        public const int DataLen = 128;     // MSG_Data_len=128

        // ---- scalar accessors (little-endian, matching CBasicPacket::ref<T>) ----------------------
        public static uint GetUniqueNo(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffUniqueNo, 4));
        public static void SetUniqueNo(Span<byte> body, uint v) => BitConverter.TryWriteBytes(body.Slice(OffUniqueNo, 4), v);

        public static ushort GetActIndex(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffActIndex, 2));
        public static void SetActIndex(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffActIndex, 2), v);

        public static ushort GetMesNo(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffMesNo, 2));
        public static void SetMesNo(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffMesNo, 2), v);

        public static byte GetAttr(ReadOnlySpan<byte> body) => body[OffAttr];
        public static void SetAttr(Span<byte> body, byte v) => body[OffAttr] = v;

        // ---- char[128] string field: fixed-width, NUL-terminated ASCII within the field ------------
        public static ReadOnlySpan<byte> GetDataBytes(ReadOnlySpan<byte> body) => body.Slice(OffData, DataLen);

        // Reads bytes up to the first NUL (or the field end). Never exceeds DataLen.
        public static string GetDataString(ReadOnlySpan<byte> body)
        {
            var field = body.Slice(OffData, DataLen);
            int end = 0;
            while (end < field.Length && field[end] != 0) end++;
            return Encoding.ASCII.GetString(field.Slice(0, end));
        }

        // Zeroes the whole field then copies min(text.Length, DataLen) ASCII bytes (a full-length write
        // leaves no NUL, matching the engine's bounded C-string copy; shorter writes are NUL-padded).
        public static void SetDataString(Span<byte> body, string text)
        {
            var field = body.Slice(OffData, DataLen);
            field.Clear();
            int n = Math.Min(text.Length, DataLen);
            for (int i = 0; i < n; i++) field[i] = (byte)text[i];
        }
    }
}
