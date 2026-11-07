// C# wire model for a Phase-3 map S2C BODY struct: GP_SERV_COMMAND_PENDINGNUM::PacketData (0x05c),
// src/map/packets/s2c/0x05c_pendingnum.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cGroupEffects convention.
//
// CRITICAL METHODOLOGY: every size/offset below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_numeric_harness.cpp -> map_s2c_numeric_golden.txt), NOT hand-computed. Unlike
// the first S2C body (group_effects, whose inner struct was #pragma pack(1) -> alignof 1), this PacketData
// is a single int32_t num[8] under MSVC DEFAULT packing (GP_SERV_PACKET adds no #pragma pack), so the
// compiler pins PN_ALIGN=4 / PN_SIZE=32 with num@0 and NO padding. This is the first Phase-3 body with an
// int32 array (element count 8, byte length 32); the whole-body i*7+3 fill proves every byte.

using System;

namespace XI.Map
{
    public static class S2cPendingnum
    {
        public const int SizeOf = 32;      // PN_SIZE=32
        public const int AlignOf = 4;      // PN_ALIGN=4 (default packing; int32 element)

        public const int OffNum = 0;       // OFF_PN.num=0 (int32[8])
        public const int NumCount = 8;     // PN_num_count=8
        public const int NumLen = 32;      // PN_num_len=32

        // Element offset within the body (little-endian, matching CBasicPacket::ref<int32_t>).
        public static int NumOffset(int index) => OffNum + index * 4;

        public static int GetNum(ReadOnlySpan<byte> body, int index) => BitConverter.ToInt32(body.Slice(NumOffset(index), 4));
        public static void SetNum(Span<byte> body, int index, int v) => BitConverter.TryWriteBytes(body.Slice(NumOffset(index), 4), v);
    }
}
