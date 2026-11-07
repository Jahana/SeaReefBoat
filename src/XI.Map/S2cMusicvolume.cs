// C# wire model for a Phase-3 map S2C BODY struct: GP_SERV_COMMAND_MUSICVOLUME::PacketData (0x060),
// src/map/packets/s2c/0x060_musicvolume.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cGroupEffects convention.
//
// CRITICAL METHODOLOGY: every size/offset below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_numeric_harness.cpp -> map_s2c_numeric_golden.txt), NOT hand-computed. Two
// uint16 under MSVC DEFAULT packing -> MV_ALIGN=2 / MV_SIZE=4, time@0, volume@2, no padding.

using System;

namespace XI.Map
{
    public static class S2cMusicvolume
    {
        public const int SizeOf = 4;      // MV_SIZE=4
        public const int AlignOf = 2;     // MV_ALIGN=2

        public const int OffTime = 0;     // OFF_MV.time=0 (uint16)
        public const int OffVolume = 2;   // OFF_MV.volume=2 (uint16)

        // ---- uint16 accessors (little-endian, matching CBasicPacket::ref<uint16_t>) ---------------
        public static ushort GetTime(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffTime, 2));
        public static void SetTime(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffTime, 2), v);

        public static ushort GetVolume(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffVolume, 2));
        public static void SetVolume(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffVolume, 2), v);
    }
}
