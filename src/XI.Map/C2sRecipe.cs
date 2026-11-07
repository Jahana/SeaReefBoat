// C# wire model for the Phase-3 map C2S body struct GP_CLI_COMMAND_RECIPE (0x058),
// src/map/packets/c2s/0x058_recipe.h via the GP_CLI_PACKET(...) macro (c2s/base.h).
//
// CRITICAL METHODOLOGY: every offset/size/mask below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_c2s_recipe_harness.cpp -> map_c2s_recipe_golden.txt), NOT hand-computed. This is
// the first Phase-3 body whose LAYOUT is driven by a uint64 recipe member sitting in the MIDDLE of an
// otherwise byte/bool struct under MSVC DEFAULT packing (no #pragma pack): the compiler 8-byte-aligns
// the uint64, so it moves to offset 16 (after bool+5 uint8 + padding00[3]@9..11) and the WHOLE struct
// is RCP_ALIGN=8 / RCP_SIZE=32. That tail padding (bytes 26..31) is the compiler's, not the author's.
// The GP_CLI_HEADER id:9/size:7/sync prefix is identical to the other C2S structs (XI.Map.C2sPos owns
// the WriteHeader/ReadId helpers; reused unchanged here). tests/XI.Verify.Map re-reads that golden and
// asserts these constants + a deterministic byte-fill match it exactly.
//
// Field accessors are little-endian Span reads/writes matching the engine's CBasicPacket::ref<T>.

using System;

namespace XI.Map
{
    public static class C2sRecipe
    {
        // GP_CLI_HEADER prefix (same 4-byte framing as the other C2S structs; id:9|size:7|sync).
        public const int OffHeader = 0;           // OFF_RCP.header=0
        public const int HeaderSize = 4;          // HDR_SIZE=4
        public const int OffSync = 2;             // HDR_OFF_sync=2

        // GP_CLI_COMMAND_RECIPE body (default packing). sizeof=32, alignof=8 (driven by the uint64).
        public const int SizeOf = 32;             // RCP_SIZE=32
        public const int AlignOf = 8;             // RCP_ALIGN=8

        public const int OffBoolNew = 4;          // OFF_RCP.bNew=4  (bool -> 1 byte)
        public const int OffTarget = 5;           // OFF_RCP.target=5
        public const int OffResult = 6;           // OFF_RCP.result=6
        public const int OffSynthesisType = 7;    // OFF_RCP.synthesisType=7
        public const int OffSynthesisAmount = 8;  // OFF_RCP.synthesisAmount=8
        public const int OffPadding00 = 9;        // OFF_RCP.padding00=9
        public const int Padding00Len = 3;        // RCP_padding00_len=3
        public const int OffRecipe = 16;          // OFF_RCP.recipe=16 (uint64 realigned to 8-byte boundary)
        public const int OffCraftType = 24;       // OFF_RCP.craftType=24
        public const int OffPadding01 = 25;       // OFF_RCP.padding01=25

        // ---- bool / uint8 accessors ---------------------------------------------------------------
        public static bool GetBoolNew(ReadOnlySpan<byte> body) => body[OffBoolNew] != 0;
        public static void SetBoolNew(Span<byte> body, bool v) => body[OffBoolNew] = (byte)(v ? 1 : 0);

        public static byte GetTarget(ReadOnlySpan<byte> body) => body[OffTarget];
        public static void SetTarget(Span<byte> body, byte v) => body[OffTarget] = v;
        public static byte GetResult(ReadOnlySpan<byte> body) => body[OffResult];
        public static void SetResult(Span<byte> body, byte v) => body[OffResult] = v;
        public static byte GetSynthesisType(ReadOnlySpan<byte> body) => body[OffSynthesisType];
        public static void SetSynthesisType(Span<byte> body, byte v) => body[OffSynthesisType] = v;
        public static byte GetSynthesisAmount(ReadOnlySpan<byte> body) => body[OffSynthesisAmount];
        public static void SetSynthesisAmount(Span<byte> body, byte v) => body[OffSynthesisAmount] = v;
        public static byte GetCraftType(ReadOnlySpan<byte> body) => body[OffCraftType];
        public static void SetCraftType(Span<byte> body, byte v) => body[OffCraftType] = v;

        // ---- uint64 recipe accessors (the 8-byte-aligned member that pads the whole struct) -------
        public static ulong GetRecipe(ReadOnlySpan<byte> body) => BitConverter.ToUInt64(body.Slice(OffRecipe, 8));
        public static void SetRecipe(Span<byte> body, ulong v) => BitConverter.TryWriteBytes(body.Slice(OffRecipe, 8), v);
    }
}
