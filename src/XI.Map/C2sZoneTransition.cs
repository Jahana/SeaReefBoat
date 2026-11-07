// C# wire model for the Phase-3 map C2S body struct GP_CLI_COMMAND_ZONE_TRANSITION (0x011),
// src/map/packets/c2s/0x011_zone_transition.h via the GP_CLI_PACKET(...) macro (c2s/base.h).
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_logout_harness.cpp -> map_logout_golden.txt, the ZTRANS_* keys), NOT hand-computed.
// After the 4-byte GP_CLI_HEADER the body is uint8 unknown00 + uint8 unknown01 + uint16 padding00 under
// MSVC DEFAULT packing -> ZTRANS_ALIGN=2 / ZTRANS_SIZE=8 (unknown00@4, unknown01@5, padding00@6, no
// padding: the 4-byte header is already a multiple of the uint16's alignment).
//
// GP_CLI_HEADER id:9|size:7|sync framing is shared with the other C2S structs (XI.Map.C2sPos owns the
// WriteHeader/ReadId helpers, reused unchanged here).

using System;

namespace XI.Map
{
    public static class C2sZoneTransition
    {
        // GP_CLI_HEADER prefix (id:9|size:7|sync) -- same 4-byte framing as pos/recipe/lockstyle.
        public const int OffHeader = 0;      // OFF_ZTRANS.header=0
        public const int HeaderSize = 4;     // HDR_SIZE=4
        public const int OffSync = 2;        // HDR_OFF_sync=2

        // Body (default packing). sizeof=8, alignof=2.
        public const int SizeOf = 8;         // ZTRANS_SIZE=8
        public const int AlignOf = 2;        // ZTRANS_ALIGN=2

        public const int OffUnknown00 = 4;   // OFF_ZTRANS.unknown00=4 (uint8)
        public const int OffUnknown01 = 5;   // OFF_ZTRANS.unknown01=5 (uint8)
        public const int OffPadding00 = 6;   // OFF_ZTRANS.padding00=6 (uint16)

        // ---- uint8 / uint16 accessors (little-endian, matching CBasicPacket::ref<T>) --------------
        public static byte GetUnknown00(ReadOnlySpan<byte> body) => body[OffUnknown00];
        public static void SetUnknown00(Span<byte> body, byte v) => body[OffUnknown00] = v;

        public static byte GetUnknown01(ReadOnlySpan<byte> body) => body[OffUnknown01];
        public static void SetUnknown01(Span<byte> body, byte v) => body[OffUnknown01] = v;

        public static ushort GetPadding00(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffPadding00, 2));
        public static void SetPadding00(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffPadding00, 2), v);
    }
}
