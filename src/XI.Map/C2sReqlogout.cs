// C# wire model for the Phase-3 map C2S body struct GP_CLI_COMMAND_REQLOGOUT (0x0e7),
// src/map/packets/c2s/0x0e7_reqlogout.h via the GP_CLI_PACKET(...) macro (c2s/base.h).
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_c2s_numeric_harness.cpp -> map_c2s_numeric_golden.txt), NOT hand-computed. This
// struct and its siblings (sit/buffcancel) are the simplest fixed-width C2S commands: after the 4-byte
// GP_CLI_HEADER the body is two uint16 under MSVC DEFAULT packing, so the compiler pins LOGOUT_ALIGN=2
// / LOGOUT_SIZE=8 with Mode@4, Kind@6 and NO padding. The two hand-authored enum classes carry explicit
// uint16_t literals; the oracle re-emits each enumerator's underlying value (MODE_*/KIND_*) and the gate
// asserts the C# enums against those numbers so a future engine renumber is caught (mirrors the packet-id
// enum pinning -- literals are authored, not fabricated by the compiler, but still pinned to the .h).
//
// The GP_CLI_HEADER id:9|size:7|sync framing is identical to the other C2S structs (XI.Map.C2sPos owns
// the WriteHeader/ReadId helpers, reused unchanged here -- not re-implemented).

using System;

namespace XI.Map
{
    // GP_CLI_COMMAND_REQLOGOUT_MODE (0x0e7_reqlogout.h). Hand-authored uint16_t literals; the oracle
    // emitted each value (MODE_*) and the gate asserts this enum against it.
    public enum ReqlogoutMode : ushort
    {
        Toggle = 0x00,      // MODE_Toggle=0
        LogoutOn = 0x01,    // MODE_LogoutOn=1  (/logout on)
        Off = 0x02,         // MODE_Off=2       (/logout off and /shutdown off)
        ShutdownOn = 0x03,  // MODE_ShutdownOn=3 (/shutdown on)
    }

    // GP_CLI_COMMAND_REQLOGOUT_KIND (0x0e7_reqlogout.h). Hand-authored uint16_t literals; pinned vs KIND_*.
    public enum ReqlogoutKind : ushort
    {
        Logout = 0x01,      // KIND_Logout=1
        Shutdown = 0x03,    // KIND_Shutdown=3
    }

    public static class C2sReqlogout
    {
        // GP_CLI_HEADER prefix (id:9|size:7|sync) -- same 4-byte framing as pos/recipe/lockstyle.
        public const int OffHeader = 0;   // OFF_LOGOUT.header=0
        public const int HeaderSize = 4;  // HDR_SIZE=4
        public const int OffSync = 2;     // HDR_OFF_sync=2

        // Body (default packing). sizeof=8, alignof=2 (no padding after the 4-byte header).
        public const int SizeOf = 8;      // LOGOUT_SIZE=8
        public const int AlignOf = 2;     // LOGOUT_ALIGN=2

        public const int OffMode = 4;     // OFF_LOGOUT.Mode=4 (uint16)
        public const int OffKind = 6;     // OFF_LOGOUT.Kind=6 (uint16)

        // ---- uint16 accessors (little-endian, matching CBasicPacket::ref<T>) ----------------------
        public static ushort GetMode(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffMode, 2));
        public static void SetMode(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffMode, 2), v);
        public static void SetMode(Span<byte> body, ReqlogoutMode m) => BitConverter.TryWriteBytes(body.Slice(OffMode, 2), (ushort)m);
        public static ReqlogoutMode GetReqlogoutMode(ReadOnlySpan<byte> body) => (ReqlogoutMode)BitConverter.ToUInt16(body.Slice(OffMode, 2));

        public static ushort GetKind(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffKind, 2));
        public static void SetKind(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffKind, 2), v);
        public static void SetKind(Span<byte> body, ReqlogoutKind k) => BitConverter.TryWriteBytes(body.Slice(OffKind, 2), (ushort)k);
        public static ReqlogoutKind GetReqlogoutKind(ReadOnlySpan<byte> body) => (ReqlogoutKind)BitConverter.ToUInt16(body.Slice(OffKind, 2));
    }
}
