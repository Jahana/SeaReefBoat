// C# wire model for the Phase-3 map C2S body struct GP_CLI_COMMAND_CAMP (0x0e8),
// src/map/packets/c2s/0x0e8_camp.h via the GP_CLI_PACKET(...) macro (c2s/base.h).
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_c2s_numeric2_harness.cpp -> map_c2s_numeric2_golden.txt), NOT hand-computed. The
// body is a single uint32 Mode after the 4-byte GP_CLI_HEADER under DEFAULT packing -> CAMP_ALIGN=4 /
// CAMP_SIZE=8 (Mode@4, no padding). The hand-authored GP_CLI_COMMAND_CAMP_MODE carries explicit uint32_t
// literals; the oracle re-emits each enumerator (CAMP_MODE_*) and the gate asserts the C# enum against it.
//
// GP_CLI_HEADER id:9|size:7|sync framing is shared with the other C2S structs (XI.Map.C2sPos helpers,
// reused unchanged).

using System;

namespace XI.Map
{
    // GP_CLI_COMMAND_CAMP_MODE (0x0e8_camp.h). Hand-authored uint32_t literals; pinned vs CAMP_MODE_*.
    public enum CampMode : uint
    {
        Toggle = 0x00,  // CAMP_MODE_Toggle=0
        On = 0x01,      // CAMP_MODE_On=1
        Off = 0x02,     // CAMP_MODE_Off=2
    }

    public static class C2sCamp
    {
        // GP_CLI_HEADER prefix (id:9|size:7|sync) -- same 4-byte framing as pos/recipe/lockstyle.
        public const int OffHeader = 0;   // OFF_CAMP.header=0
        public const int HeaderSize = 4;  // HDR_SIZE=4
        public const int OffSync = 2;     // HDR_OFF_sync=2

        // Body (default packing). sizeof=8, alignof=4 (uint32 Mode@4, no padding).
        public const int SizeOf = 8;      // CAMP_SIZE=8
        public const int AlignOf = 4;     // CAMP_ALIGN=4

        public const int OffMode = 4;     // OFF_CAMP.Mode=4 (uint32)

        // ---- uint32 accessors (little-endian, matching CBasicPacket::ref<T>) ----------------------
        public static uint GetMode(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffMode, 4));
        public static void SetMode(Span<byte> body, uint v) => BitConverter.TryWriteBytes(body.Slice(OffMode, 4), v);
        public static void SetMode(Span<byte> body, CampMode m) => BitConverter.TryWriteBytes(body.Slice(OffMode, 4), (uint)m);
        public static CampMode GetCampMode(ReadOnlySpan<byte> body) => (CampMode)BitConverter.ToUInt32(body.Slice(OffMode, 4));
    }
}
