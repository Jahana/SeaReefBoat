// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_BATTLEFIELD::PacketData (0x075),
// src/map/packets/s2c/0x075_battlefield.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cPbxResult/S2cRecipe convention.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_battlefield_union_harness.cpp -> map_s2c_battlefield_union_golden.txt), NOT
// hand-computed.
//
// This is the THIRD Phase-3 body containing a UNION and the layout-densest: it combines hazards that prior
// chunks exercised only in isolation.
//   - THREE-ARM union (0x04b/0x031 had two): union{ u8 Data[128]; scoreboard_data_t Scoreboard;
//     progressbar_data_t Progress } all alias at body offset 36.
//   - UNION SIZED BY THE LARGEST ARM (128) AND NOT AT THE END: two uint16 members FOLLOW the union, so the
//     union's size (max(128,128,124) == 128, from Data/Scoreboard) sets where the tail lands: MesNumTitle@164
//     (== 36 + 128), MesNumDescription@166 -> BF_SIZE 168. Sizing the union to the Progress arm (124) would
//     wrongly place the tail at 160/162.
//   - A UNION ARM IS A NESTED STRUCT (scoreboard_data_t: 8 u32/i32 + trailing padding[96] -> 128/4).
//   - A UNION ARM HOLDS A NESTED STRUCT ARRAY (progressbar_data_t{ progressbar_row_t Rows[6]; u32 unused }):
//     row = { u32 Progress@0; i8 Name[16]@4 } -> 20/4, stride 20 (no inter-element pad), unused@120 -> 124/4.
//   - CROSS-ARM RAW-VS-STRUCTURED ALIASING: the raw Data[i] bytes are the SAME bytes the structured arms read
//     (e.g. Data[4..19] == Rows[0].Name, both at body offset 40).
// The hand-authored OBJECTIVEUTILITY_TYPE (uint8) enum is defined in THIS header -> literals are compiler-
// pinned via the oracle (magic_enum is not vendored).

using System;

namespace XI.Map
{
    // OBJECTIVEUTILITY_TYPE (hand-authored uint8_t enum). Values pinned vs oracle ENUM_* lines; it is a bit
    // FLAG set (0x01/0x02/0x04/0x08), used in Flags -- not a fixed-width struct member, so no member offset.
    [Flags]
    public enum ObjectiveUtilityType : byte
    {
        Countdown = 0x01,   // ENUM_COUNTDOWN=1
        Progress  = 0x02,   // ENUM_PROGRESS=2
        Help      = 0x04,   // ENUM_HELP=4
        Fence     = 0x08,   // ENUM_FENCE=8
    }

    // scoreboard_data_t -- a union arm that is itself a nested struct. sizeof=128, alignof=4. Offsets arm-relative.
    public static class ScoreboardArm
    {
        public const int SizeOf = 128;       // SBR_SIZE=128
        public const int AlignOf = 4;        // SBR_ALIGN=4
        public const int OffMarchlandScore = 0;    // OFF_SBR.MarchlandScore=0 (int32)
        public const int OffStrongholdScore = 4;   // OFF_SBR.StrongholdScore=4 (int32)
        public const int OffMarchlandProgress = 8; // OFF_SBR.MarchlandProgress=8 (uint32)
        public const int OffMarchlandProgressMax = 12;
        public const int OffStrongholdProgress = 16;
        public const int OffStrongholdProgressMax = 20;
        public const int OffMarchlandNameOverride = 24;
        public const int OffStrongholdNameOverride = 28; // OFF_SBR.StrongholdNameOverride=28
        public const int OffPadding = 32;    // OFF_SBR.padding=32
        public const int PaddingLen = 96;    // SBR_padding_len=96 (32 + 96 == 128, gapless)
    }

    // progressbar_row_t -- the element type of the struct array inside the Progress union arm. 20/4.
    public static class ProgressbarRow
    {
        public const int SizeOf = 20;        // ROW_SIZE=20
        public const int AlignOf = 4;        // ROW_ALIGN=4
        public const int OffProgress = 0;    // OFF_ROW.Progress=0 (uint32)
        public const int OffName = 4;        // OFF_ROW.Name=4 (int8[16]; packs at 4, alignof 1)
        public const int NameLen = 16;       // ROW_Name_len=16 (4 + 16 == 20, no tail pad)
    }

    // progressbar_data_t -- a union arm holding a nested STRUCT ARRAY + a trailing u32. 124/4.
    public static class ProgressbarArm
    {
        public const int SizeOf = 124;       // PB_SIZE=124
        public const int AlignOf = 4;        // PB_ALIGN=4
        public const int OffRows = 0;        // OFF_PB.Rows=0
        public const int RowsLen = 120;      // PB_Rows_len=120
        public const int RowsCount = 6;      // PB_Rows_count=6
        public const int RowStride = 20;     // RowsLen/RowsCount == sizeof(progressbar_row_t) (no inter-element pad)
        public const int OffUnused = 120;    // OFF_PB.unused=120 (u32 after 6 rows: 120 + 4 == 124)
    }

    // GP_SERV_COMMAND_BATTLEFIELD::PacketData (outer, default packing). sizeof=168, alignof=4.
    // Leading scalars (0..35), the three-arm anonymous union at 36 (sized by the largest arm, 128 -> 36..163),
    // then the uint16 tail (MesNumTitle@164, MesNumDescription@166). All three union arms alias at OffUnion.
    public static class S2cBattlefield
    {
        public const int SizeOf = 168;       // BF_SIZE=168
        public const int AlignOf = 4;        // BF_ALIGN=4

        public const int OffMode = 0;        // OFF_BF.Mode=0 (uint32)
        public const int OffTimestamp = 4;   // uint32
        public const int OffDuration = 8;    // uint32
        public const int OffDurationWarn = 12; // OFF_BF.DurationWarn=12 (uint32)
        public const int OffFenceX = 16;     // OFF_BF.FenceX=16 (int32)
        public const int OffFenceY = 20;     // int32
        public const int OffFenceRadius = 24; // uint32
        public const int OffFenceRotation = 28; // OFF_BF.FenceRotation=28 (uint32)
        public const int OffFlags = 32;      // OFF_BF.Flags=32 (uint8, an ObjectiveUtilityType bit set)
        public const int OffFenceColor = 33; // uint8
        public const int OffFenceGateId = 34; // uint8
        public const int OffPadding27 = 35;  // OFF_BF.padding27=35 (uint8)

        public const int OffUnion = 36;      // union start (4-aligned after the byte at 35)
        public const int OffData = 36;       // OFF_BF.Data=36 (u8[128] arm)
        public const int OffScoreboard = 36; // OFF_BF.Scoreboard=36 (aliases Data -- the union fact)
        public const int OffProgress = 36;   // OFF_BF.Progress=36 (aliases Data)
        public const int DataLen = 128;      // BF_Data_len=128

        public const int OffMesNumTitle = 164;       // OFF_BF.MesNumTitle=164 (== OffUnion + 128, tail after union)
        public const int OffMesNumDescription = 166; // OFF_BF.MesNumDescription=166

        // nested-inclusive offsets (union start @36 + arm-relative field offset):
        public static int ScoreboardMarchlandScoreOffset() => OffScoreboard + ScoreboardArm.OffMarchlandScore; // 36
        public static int ScoreboardPaddingOffset() => OffScoreboard + ScoreboardArm.OffPadding;               // 68
        public static int RowOffset(int i) => OffProgress + ProgressbarArm.OffRows + i * ProgressbarRow.SizeOf; // 36 + 20i
        public static int ProgressUnusedOffset() => OffProgress + ProgressbarArm.OffUnused;                    // 156

        // ---- leading scalar accessors (little-endian) ---------------------------------------------
        public static uint GetMode(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffMode, 4));
        public static void SetMode(Span<byte> body, uint v) => BitConverter.TryWriteBytes(body.Slice(OffMode, 4), v);

        public static uint GetTimestamp(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffTimestamp, 4));
        public static void SetTimestamp(Span<byte> body, uint v) => BitConverter.TryWriteBytes(body.Slice(OffTimestamp, 4), v);

        public static uint GetDuration(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffDuration, 4));
        public static void SetDuration(Span<byte> body, uint v) => BitConverter.TryWriteBytes(body.Slice(OffDuration, 4), v);

        public static uint GetDurationWarn(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffDurationWarn, 4));

        public static int GetFenceX(ReadOnlySpan<byte> body) => BitConverter.ToInt32(body.Slice(OffFenceX, 4));
        public static void SetFenceX(Span<byte> body, int v) => BitConverter.TryWriteBytes(body.Slice(OffFenceX, 4), v);

        public static int GetFenceY(ReadOnlySpan<byte> body) => BitConverter.ToInt32(body.Slice(OffFenceY, 4));

        public static uint GetFenceRadius(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffFenceRadius, 4));
        public static uint GetFenceRotation(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffFenceRotation, 4));

        public static byte GetFlags(ReadOnlySpan<byte> body) => body[OffFlags];
        public static void SetFlags(Span<byte> body, byte v) => body[OffFlags] = v;
        public static byte GetFenceColor(ReadOnlySpan<byte> body) => body[OffFenceColor];
        public static byte GetFenceGateId(ReadOnlySpan<byte> body) => body[OffFenceGateId];

        // ---- union raw Data arm -------------------------------------------------------------
        public static byte GetData(ReadOnlySpan<byte> body, int i) => body[OffData + i];
        public static void SetData(Span<byte> body, int i, byte v) => body[OffData + i] = v;
        public static ReadOnlySpan<byte> GetDataSpan(ReadOnlySpan<byte> body) => body.Slice(OffData, DataLen);

        // ---- union Scoreboard arm (a scoreboard_data_t at OffScoreboard == 36) --------------------
        public static int GetScoreboardMarchlandScore(ReadOnlySpan<byte> body) => BitConverter.ToInt32(body.Slice(ScoreboardMarchlandScoreOffset(), 4));
        public static void SetScoreboardMarchlandScore(Span<byte> body, int v) => BitConverter.TryWriteBytes(body.Slice(ScoreboardMarchlandScoreOffset(), 4), v);
        public static int GetScoreboardStrongholdScore(ReadOnlySpan<byte> body) => BitConverter.ToInt32(body.Slice(OffScoreboard + ScoreboardArm.OffStrongholdScore, 4));
        public static uint GetScoreboardMarchlandProgress(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffScoreboard + ScoreboardArm.OffMarchlandProgress, 4));
        public static ReadOnlySpan<byte> GetScoreboardPadding(ReadOnlySpan<byte> body) => body.Slice(ScoreboardPaddingOffset(), ScoreboardArm.PaddingLen);

        // ---- union Progress arm (a progressbar_data_t at OffProgress == 36) -----------------------
        public static uint GetRowProgress(ReadOnlySpan<byte> body, int i) => BitConverter.ToUInt32(body.Slice(RowOffset(i) + ProgressbarRow.OffProgress, 4));
        public static void SetRowProgress(Span<byte> body, int i, uint v) => BitConverter.TryWriteBytes(body.Slice(RowOffset(i) + ProgressbarRow.OffProgress, 4), v);
        public static ReadOnlySpan<byte> GetRowName(ReadOnlySpan<byte> body, int i) => body.Slice(RowOffset(i) + ProgressbarRow.OffName, ProgressbarRow.NameLen);
        public static uint GetProgressUnused(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(ProgressUnusedOffset(), 4));
        public static void SetProgressUnused(Span<byte> body, uint v) => BitConverter.TryWriteBytes(body.Slice(ProgressUnusedOffset(), 4), v);

        // ---- uint16 tail (AFTER the union -- its position is set by the union's SIZE) --------------
        public static ushort GetMesNumTitle(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffMesNumTitle, 2));
        public static void SetMesNumTitle(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffMesNumTitle, 2), v);
        public static ushort GetMesNumDescription(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffMesNumDescription, 2));
        public static void SetMesNumDescription(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffMesNumDescription, 2), v);
    }
}
