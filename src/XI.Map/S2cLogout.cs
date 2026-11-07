// C# wire model for a Phase-3 map S2C BODY struct: GP_SERV_COMMAND_LOGOUT::PacketData (0x00b),
// src/map/packets/s2c/0x00b_logout.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cGroupEffects/S2cPendingnum convention.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_logout_harness.cpp -> map_logout_golden.txt), NOT hand-computed. This is the first
// Phase-3 body with a NESTED FIXED-WIDTH STRUCT MEMBER inlined (not an array -- lockstyle's Items[] was an
// array, group_effects' Members[] was a pack(1) array). Under MSVC DEFAULT packing the nested
// GP_SERV_LOGOUTSUB { u32 ip; u32 port; u8 padding00[8]; } has alignof 4, so it RE-ALIGNS inside the outer
// body: the leading u8 LogoutState@0 leaves 3 pad bytes, Iwasaki starts at offset 4 (NOT 1), and the
// trailing u32 cliErrCode lands at offset 20 -> LOGOUT_ALIGN=4 / LOGOUT_SIZE=24. None of that (the 3 pad
// bytes, the 4-byte Iwasaki start, the 20-byte cliErrCode) is hand-reasonable.
//
// The two enums are hand-authored literals (magic_enum is NOT vendored): the oracle re-declares each and
// prints the underlying integer cast, and tests/XI.Verify.Map asserts these C# enum members against those
// numbers so a future engine renumber is caught (the chunk-7/8/9/10 technique).

using System;

namespace XI.Map
{
    // GP_GAME_LOGOUT_STATE (0x00b_logout.h). Hand-authored uint8_t literals; pinned vs LGS_*.
    public enum LogoutState : byte
    {
        None = 0,             // LGS_NONE=0
        Logout = 1,           // LGS_LOGOUT=1
        Zonechange = 2,       // LGS_ZONECHANGE=2
        Myroom = 3,           // LGS_MYROOM=3
        Cancell = 4,          // LGS_CANCELL=4
        Polexit = 5,          // LGS_POLEXIT=5
        Jobexit = 6,          // LGS_JOBEXIT=6
        PolexitMyroom = 7,    // LGS_POLEXIT_MYROOM=7
        Timeout = 8,          // LGS_TIMEOUT=8
        Gmlogout = 9,         // LGS_GMLOGOUT=9
        End = 10,             // LGS_END=10
    }

    // GP_GAME_ECODE (0x00b_logout.h). Hand-authored uint32_t literals; pinned vs EC_*.
    public enum GameEcode : uint
    {
        Noerr = 0,            // EC_NOERR=0
        Reserr = 1,           // EC_RESERR=1
        Zonedown = 2,         // EC_ZONEDOWN=2
        Regerr = 3,           // EC_REGERR=3
        Cliverserr = 4,       // EC_CLIVERSERR=4
        Clinoexerr = 5,       // EC_CLINOEXERR=5
        Unknown = 6,          // EC_UNKNOWN=6
        Max = 7,              // EC_MAX=7
    }

    // GP_SERV_LOGOUTSUB (nested struct, default-packed): ip + port + a padding00[8] (used as a raw
    // uint8[16] when not passing IP/Port). sizeof=16, alignof=4 (from the uint32s).
    public static class LogoutSub
    {
        public const int SizeOf = 16;      // LOGOUTSUB_SIZE=16
        public const int AlignOf = 4;      // LOGOUTSUB_ALIGN=4

        public const int OffIp = 0;        // OFF_LOGOUTSUB.ip=0 (uint32)
        public const int OffPort = 4;      // OFF_LOGOUTSUB.port=4 (uint32)
        public const int OffPadding00 = 8; // OFF_LOGOUTSUB.padding00=8 (uint8[8])
        public const int Padding00Len = 8; // LOGOUTSUB_padding00_len=8
    }

    // GP_SERV_COMMAND_LOGOUT::PacketData (outer, default packing). sizeof=24, alignof=4.
    public static class S2cLogout
    {
        public const int SizeOf = 24;         // LOGOUT_SIZE=24
        public const int AlignOf = 4;         // LOGOUT_ALIGN=4

        public const int OffLogoutState = 0;  // OFF_LOGOUT.LogoutState=0 (u8 enum)
        public const int OffIwasaki = 4;      // OFF_LOGOUT.Iwasaki=4 (nested struct, RE-ALIGNED: 3 pad bytes @1..3)
        public const int OffCliErrCode = 20;  // OFF_LOGOUT.cliErrCode=20 (u32 enum == OffIwasaki + 16)

        // ---- LogoutState (u8) accessors -----------------------------------------------------------
        public static byte GetLogoutStateByte(ReadOnlySpan<byte> body) => body[OffLogoutState];
        public static void SetLogoutStateByte(Span<byte> body, byte v) => body[OffLogoutState] = v;
        public static LogoutState GetLogoutState(ReadOnlySpan<byte> body) => (LogoutState)body[OffLogoutState];
        public static void SetLogoutState(Span<byte> body, LogoutState s) => body[OffLogoutState] = (byte)s;

        // ---- nested Iwasaki (GP_SERV_LOGOUTSUB) field accessors: OffIwasaki + inner offset ---------
        public static int IpOffset() => OffIwasaki + LogoutSub.OffIp;
        public static int PortOffset() => OffIwasaki + LogoutSub.OffPort;

        public static uint GetIp(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(IpOffset(), 4));
        public static void SetIp(Span<byte> body, uint v) => BitConverter.TryWriteBytes(body.Slice(IpOffset(), 4), v);

        public static uint GetPort(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(PortOffset(), 4));
        public static void SetPort(Span<byte> body, uint v) => BitConverter.TryWriteBytes(body.Slice(PortOffset(), 4), v);

        // ---- cliErrCode (u32) accessors ------------------------------------------------------------
        public static uint GetCliErrCodeByte(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffCliErrCode, 4));
        public static void SetCliErrCodeByte(Span<byte> body, uint v) => BitConverter.TryWriteBytes(body.Slice(OffCliErrCode, 4), v);
        public static GameEcode GetCliErrCode(ReadOnlySpan<byte> body) => (GameEcode)BitConverter.ToUInt32(body.Slice(OffCliErrCode, 4));
        public static void SetCliErrCode(Span<byte> body, GameEcode c) => BitConverter.TryWriteBytes(body.Slice(OffCliErrCode, 4), (uint)c);
    }
}
