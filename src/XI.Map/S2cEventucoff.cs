// C# wire model for a Phase-3 map S2C BODY struct: GP_SERV_COMMAND_EVENTUCOFF::PacketData (0x052),
// src/map/packets/s2c/0x052_eventucoff.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cGroupEffects convention.
//
// CRITICAL METHODOLOGY: the layout (size/offset) is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_numeric_harness.cpp -> map_s2c_numeric_golden.txt), NOT hand-computed. A single
// uint32-backed enum Mode -> EU_ALIGN=4 / EU_SIZE=4, Mode@0, no padding. The hand-authored
// GP_SERV_COMMAND_EVENTUCOFF_MODE literals are pinned the same way as the C2S reqlogout/sit enums: the
// oracle re-declares the enum class and prints each underlying value, and the gate asserts the C# enum
// members against those compiler-emitted numbers so a future engine renumber is caught.

using System;

namespace XI.Map
{
    // GP_SERV_COMMAND_EVENTUCOFF_MODE (0x052_eventucoff.h). Hand-authored uint32_t literals; pinned vs
    // EU_MODE_*.
    public enum EventucoffMode : uint
    {
        Standard = 0,           // EU_MODE_Standard=0
        EventRecvPending = 1,   // EU_MODE_EventRecvPending=1
        CancelEvent = 2,        // EU_MODE_CancelEvent=2
        CancelInput = 3,        // EU_MODE_CancelInput=3
        Fishing = 4,            // EU_MODE_Fishing=4
    }

    public static class S2cEventucoff
    {
        public const int SizeOf = 4;     // EU_SIZE=4
        public const int AlignOf = 4;    // EU_ALIGN=4

        public const int OffMode = 0;    // OFF_EU.Mode=0 (uint32)

        // ---- uint32 accessors (little-endian, matching CBasicPacket::ref<uint32_t>) ---------------
        public static uint GetMode(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffMode, 4));
        public static void SetMode(Span<byte> body, uint v) => BitConverter.TryWriteBytes(body.Slice(OffMode, 4), v);
        public static void SetMode(Span<byte> body, EventucoffMode m) => BitConverter.TryWriteBytes(body.Slice(OffMode, 4), (uint)m);
        public static EventucoffMode GetEventucoffMode(ReadOnlySpan<byte> body) => (EventucoffMode)BitConverter.ToUInt32(body.Slice(OffMode, 4));
    }
}
