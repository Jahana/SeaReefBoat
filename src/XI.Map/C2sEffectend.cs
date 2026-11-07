// C# wire model for the Phase-3 map C2S body struct GP_CLI_COMMAND_EFFECTEND (0x059),
// src/map/packets/c2s/0x059_effectend.h via the GP_CLI_PACKET(...) macro (c2s/base.h).
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_c2s_numeric2_harness.cpp -> map_c2s_numeric2_golden.txt), NOT hand-computed. This
// is the first Phase-3 body with a raw BYTE-ARRAY field: after the 4-byte GP_CLI_HEADER comes a uint32
// effectpara@4 and uint8_t padding00[8]@8 under MSVC DEFAULT packing, so the compiler pins
// EFFECTEND_ALIGN=4 / EFFECTEND_SIZE=16 with NO padding anywhere (4 + 4 + 8 = 16). The whole-struct
// i*7+3 deterministic fill (030A11181F262D343B424950575E656C) proves every byte, including the array.
//
// GP_CLI_HEADER id:9|size:7|sync framing is shared with the other C2S structs (XI.Map.C2sPos owns the
// WriteHeader/ReadId helpers, reused unchanged here).

using System;

namespace XI.Map
{
    public static class C2sEffectend
    {
        // GP_CLI_HEADER prefix (id:9|size:7|sync) -- same 4-byte framing as pos/recipe/lockstyle.
        public const int OffHeader = 0;        // OFF_EFFECTEND.header=0
        public const int HeaderSize = 4;       // HDR_SIZE=4
        public const int OffSync = 2;          // HDR_OFF_sync=2

        // Body (default packing). sizeof=16, alignof=4.
        public const int SizeOf = 16;          // EFFECTEND_SIZE=16
        public const int AlignOf = 4;          // EFFECTEND_ALIGN=4

        public const int OffEffectpara = 4;    // OFF_EFFECTEND.effectpara=4 (uint32)
        public const int OffPadding00 = 8;     // OFF_EFFECTEND.padding00=8 (uint8[8])
        public const int Padding00Len = 8;     // EFFECTEND_padding00_len=8

        // ---- uint32 accessor (little-endian, matching CBasicPacket::ref<T>) ----------------------
        public static uint GetEffectpara(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffEffectpara, 4));
        public static void SetEffectpara(Span<byte> body, uint v) => BitConverter.TryWriteBytes(body.Slice(OffEffectpara, 4), v);

        // padding00 is a raw byte-array field (engine-side padding); expose the slice for completeness.
        public static ReadOnlySpan<byte> GetPadding00(ReadOnlySpan<byte> body) => body.Slice(OffPadding00, Padding00Len);
    }
}
