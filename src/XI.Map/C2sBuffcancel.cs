// C# wire model for the Phase-3 map C2S body struct GP_CLI_COMMAND_BUFFCANCEL (0x0f1),
// src/map/packets/c2s/0x0f1_buffcancel.h via the GP_CLI_PACKET(...) macro (c2s/base.h).
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_c2s_numeric_harness.cpp -> map_c2s_numeric_golden.txt), NOT hand-computed. After
// the 4-byte GP_CLI_HEADER the body is uint16 BuffNo + uint16 padding00 under MSVC DEFAULT packing, so
// the compiler pins BUFFCANCEL_ALIGN=2 / BUFFCANCEL_SIZE=8 with BuffNo@4, padding00@6 and no padding.
//
// GP_CLI_HEADER id:9|size:7|sync framing is shared with the other C2S structs (XI.Map.C2sPos helpers,
// reused unchanged).

using System;

namespace XI.Map
{
    public static class C2sBuffcancel
    {
        // GP_CLI_HEADER prefix (id:9|size:7|sync) -- same 4-byte framing as pos/recipe/lockstyle.
        public const int OffHeader = 0;   // OFF_BUFFCANCEL.header=0
        public const int HeaderSize = 4;  // HDR_SIZE=4
        public const int OffSync = 2;     // HDR_OFF_sync=2

        // Body (default packing). sizeof=8, alignof=2.
        public const int SizeOf = 8;             // BUFFCANCEL_SIZE=8
        public const int AlignOf = 2;            // BUFFCANCEL_ALIGN=2

        public const int OffBuffNo = 4;          // OFF_BUFFCANCEL.BuffNo=4 (uint16)
        public const int OffPadding00 = 6;       // OFF_BUFFCANCEL.padding00=6 (uint16)

        // ---- uint16 accessors (little-endian, matching CBasicPacket::ref<T>) ----------------------
        public static ushort GetBuffNo(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffBuffNo, 2));
        public static void SetBuffNo(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffBuffNo, 2), v);

        public static ushort GetPadding00(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffPadding00, 2));
        public static void SetPadding00(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffPadding00, 2), v);
    }
}
