// C# wire model for the Phase-3 map C2S body struct GP_CLI_COMMAND_RESCUE (0x0f0),
// src/map/packets/c2s/0x0f0_rescue.h via the GP_CLI_PACKET(...) macro (c2s/base.h).
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_c2s_numeric2_harness.cpp -> map_c2s_numeric2_golden.txt), NOT hand-computed. The
// body is a single uint32 State after the 4-byte GP_CLI_HEADER under DEFAULT packing -> RESCUE_ALIGN=4 /
// RESCUE_SIZE=8 (State@4, no padding).
//
// GP_CLI_HEADER id:9|size:7|sync framing is shared with the other C2S structs (XI.Map.C2sPos helpers,
// reused unchanged).

using System;

namespace XI.Map
{
    public static class C2sRescue
    {
        // GP_CLI_HEADER prefix (id:9|size:7|sync) -- same 4-byte framing as pos/recipe/lockstyle.
        public const int OffHeader = 0;   // OFF_RESCUE.header=0
        public const int HeaderSize = 4;  // HDR_SIZE=4
        public const int OffSync = 2;     // HDR_OFF_sync=2

        // Body (default packing). sizeof=8, alignof=4 (uint32 State@4, no padding).
        public const int SizeOf = 8;      // RESCUE_SIZE=8
        public const int AlignOf = 4;     // RESCUE_ALIGN=4

        public const int OffState = 4;    // OFF_RESCUE.State=4 (uint32)

        // ---- uint32 accessor (little-endian, matching CBasicPacket::ref<T>) ----------------------
        public static uint GetState(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffState, 4));
        public static void SetState(Span<byte> body, uint v) => BitConverter.TryWriteBytes(body.Slice(OffState, 4), v);
    }
}
