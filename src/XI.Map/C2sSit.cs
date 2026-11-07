// C# wire model for the Phase-3 map C2S body struct GP_CLI_COMMAND_SIT (0x0ea),
// src/map/packets/c2s/0x0ea_sit.h via the GP_CLI_PACKET(...) macro (c2s/base.h).
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_c2s_numeric_harness.cpp -> map_c2s_numeric_golden.txt), NOT hand-computed. The
// body is a single uint32 Mode after the 4-byte GP_CLI_HEADER under MSVC DEFAULT packing, so the
// compiler pins SIT_ALIGN=4 / SIT_SIZE=8 with Mode@4 (no padding: 4-byte header is already a multiple
// of the uint32's alignment). The hand-authored GP_CLI_COMMAND_SIT_MODE carries explicit uint32_t
// literals; the oracle re-emits each enumerator (SITMODE_*) and the gate asserts the C# enum against it.
//
// GP_CLI_HEADER id:9|size:7|sync framing is shared with the other C2S structs (XI.Map.C2sPos helpers,
// reused unchanged).

using System;

namespace XI.Map
{
    // GP_CLI_COMMAND_SIT_MODE (0x0ea_sit.h). Hand-authored uint32_t literals; pinned vs SITMODE_*.
    public enum SitMode : uint
    {
        Toggle = 0x00,  // SITMODE_Toggle=0
        On = 0x01,      // SITMODE_On=1
        Off = 0x02,     // SITMODE_Off=2
    }

    public static class C2sSit
    {
        // GP_CLI_HEADER prefix (id:9|size:7|sync) -- same 4-byte framing as pos/recipe/lockstyle.
        public const int OffHeader = 0;   // OFF_SIT.header=0
        public const int HeaderSize = 4;  // HDR_SIZE=4
        public const int OffSync = 2;     // HDR_OFF_sync=2

        // Body (default packing). sizeof=8, alignof=4 (uint32 Mode@4, no padding).
        public const int SizeOf = 8;      // SIT_SIZE=8
        public const int AlignOf = 4;     // SIT_ALIGN=4

        public const int OffMode = 4;     // OFF_SIT.Mode=4 (uint32)

        // ---- uint32 accessors (little-endian, matching CBasicPacket::ref<T>) ----------------------
        public static uint GetMode(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffMode, 4));
        public static void SetMode(Span<byte> body, uint v) => BitConverter.TryWriteBytes(body.Slice(OffMode, 4), v);
        public static void SetMode(Span<byte> body, SitMode m) => BitConverter.TryWriteBytes(body.Slice(OffMode, 4), (uint)m);
        public static SitMode GetSitMode(ReadOnlySpan<byte> body) => (SitMode)BitConverter.ToUInt32(body.Slice(OffMode, 4));
    }
}
