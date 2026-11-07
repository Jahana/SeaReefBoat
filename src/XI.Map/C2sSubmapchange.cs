// C# wire model for the Phase-3 map C2S body struct GP_CLI_COMMAND_SUBMAPCHANGE (0x0f2),
// src/map/packets/c2s/0x0f2_submapchange.h via the GP_CLI_PACKET(...) macro (c2s/base.h).
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_c2s_numeric2_harness.cpp -> map_c2s_numeric2_golden.txt), NOT hand-computed. The
// body is two uint16 (State@4, SubMapNumber@6) after the 4-byte GP_CLI_HEADER under DEFAULT packing ->
// SUBMAPCHANGE_ALIGN=2 / SUBMAPCHANGE_SIZE=8 (no padding). The hand-authored
// GP_CLI_COMMAND_SUBMAPCHANGE_STATE carries explicit uint16_t literals; the oracle re-emits each
// enumerator (SUBMAP_STATE_*) and the gate asserts the C# enum against it.
//
// GP_CLI_HEADER id:9|size:7|sync framing is shared with the other C2S structs (XI.Map.C2sPos helpers,
// reused unchanged).

using System;

namespace XI.Map
{
    // GP_CLI_COMMAND_SUBMAPCHANGE_STATE (0x0f2_submapchange.h). Hand-authored uint16_t literals; pinned
    // vs SUBMAP_STATE_*.
    public enum SubmapchangeState : ushort
    {
        General = 0x01,  // SUBMAP_STATE_General=1
        Event = 0x02,    // SUBMAP_STATE_Event=2
    }

    public static class C2sSubmapchange
    {
        // GP_CLI_HEADER prefix (id:9|size:7|sync) -- same 4-byte framing as pos/recipe/lockstyle.
        public const int OffHeader = 0;         // OFF_SUBMAPCHANGE.header=0
        public const int HeaderSize = 4;        // HDR_SIZE=4
        public const int OffSync = 2;           // HDR_OFF_sync=2

        // Body (default packing). sizeof=8, alignof=2.
        public const int SizeOf = 8;            // SUBMAPCHANGE_SIZE=8
        public const int AlignOf = 2;           // SUBMAPCHANGE_ALIGN=2

        public const int OffState = 4;          // OFF_SUBMAPCHANGE.State=4 (uint16)
        public const int OffSubMapNumber = 6;   // OFF_SUBMAPCHANGE.SubMapNumber=6 (uint16)

        // ---- uint16 accessors (little-endian, matching CBasicPacket::ref<T>) ----------------------
        public static ushort GetState(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffState, 2));
        public static void SetState(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffState, 2), v);
        public static void SetState(Span<byte> body, SubmapchangeState s) => BitConverter.TryWriteBytes(body.Slice(OffState, 2), (ushort)s);
        public static SubmapchangeState GetSubmapchangeState(ReadOnlySpan<byte> body) => (SubmapchangeState)BitConverter.ToUInt16(body.Slice(OffState, 2));

        public static ushort GetSubMapNumber(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffSubMapNumber, 2));
        public static void SetSubMapNumber(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffSubMapNumber, 2), v);
    }
}
