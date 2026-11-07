// XI.Quests -- normalized char_mission_state storage model + offline blob importer, the
// mission half symmetric to CharQuestState.cs.
//
// Unpacks the LEGACY per-char mission state (the 1050-byte chars.missions blob =
// missionlog_t[15], plus the Mission[area][id]<suffix> rows in char_vars -- mission.lua:40
// getVarPrefix) into NORMALIZED char_mission_state rows (one per area), and proves the
// round-trip:
//
//     Project(ToCells(Import(legacyBlob))) == legacyBlob   -- byte-for-byte, for EVERY blob
//
// This is STRICTER than the quest importer: quests collapse a bit-PAIR into a status tri-state
// (with the documented (1,1) lossy edge), so quest Import reproduces the CANONICAL projection.
// Missions keep the full per-area struct faithfully (current + status + 64 completion flags as
// a mask; CoP/highid completion is a derived read view, not extra bytes), so the round-trip is
// lossless against the raw golden bytes.
//
// char_vars carry per-mission progress the blob cannot represent (Mission[area][id]Prog). The
// importer folds those into SoftVars/ProgressJson; they NEVER touch the wire projection --
// proven by a soft-only quest projecting back to the identical 1050-byte blob.
//
// No C++ oracle for the normalized layer (new design); verification is Import->Project parity
// vs the SAME mission_golden.txt raw bytes that anchor MissionBitBlob (itself byte-for-byte
// against a compiled oracle). DDL/INSERT are shape-asserted offline, not executed (no live DB).
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace XI.Quests
{
    // One normalized char_mission_state row: the authoritative per-character record for one
    // mission AREA (a mission container is per-area, not per-mission). Maps 1:1 to the table
    // in MissionStateSchema.
    public sealed class CharMissionStateRow
    {
        public uint CharId;
        public MissionLog Area;              // the row identity (per-area), == char_mission_state.area
        public ushort Current;               // current mission id (or the 0xFFFF/101/0 sentinel)
        public uint Status;                  // (statusUpper << 16) | statusLower
        public ulong CompletedMask;          // bit m == complete[m] (m < 64); empty for CoP
        public long UpdatedAt;               // caller/DB supplied; 0 for an offline import

        // Other Mission[area][id]<suffix> char_vars (Prog, ...), keyed "<id>:<suffix>". The
        // 1050-byte blob cannot carry these, so they are pure normalized soft state.
        public readonly Dictionary<string, long> SoftVars = new Dictionary<string, long>();

        // The non-zero char_vars expiries (unix seconds) of the SoftVars, by the same "<id>:<suffix>" key.
        public readonly Dictionary<string, uint> Expiry = new Dictionary<string, uint>();

        public int AreaIndex => (int)Area;
        public string ProgressJson => ProgressJsonCodec.Write(SoftVars, Expiry);

        // Engine completion semantics for one mission id in this area (current-relative for
        // CoP / high ids, else the stored flag). Delegates to the faithful MissionCell view.
        public bool IsCompleted(int missionId) => ToCell().IsCompleted(missionId);

        public MissionCell ToCell() => new MissionCell
        {
            Area = Area,
            Current = Current,
            Status = Status,
            CompletedMask = CompletedMask,
        };
    }

    // The char_mission_state table DDL + column list. Per-area PK (a mission is an area
    // container). Conventions mirror char_vars.sql (int unsigned charid, etc.).
    public static class MissionStateSchema
    {
        public const string TableName = "char_mission_state";

        public const string CreateTableSql =
            "CREATE TABLE IF NOT EXISTS `char_mission_state` (\n" +
            "  `charid`         int(10) unsigned NOT NULL,\n" +
            "  `area`           tinyint(3) unsigned NOT NULL,\n" +
            "  `current`        smallint(5) unsigned NOT NULL DEFAULT 0,\n" +
            "  `status`         int(10) unsigned NOT NULL DEFAULT 0,\n" +
            "  `completed_mask` bigint(20) unsigned NOT NULL DEFAULT 0,\n" +
            "  `progress`       json DEFAULT NULL,\n" +
            "  `updated_at`     timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,\n" +
            "  PRIMARY KEY (`charid`, `area`)\n" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;";

        public static readonly string[] Columns =
        {
            "charid", "area", "current", "status", "completed_mask", "progress", "updated_at"
        };
    }

    public static class MissionStateImporter
    {
        public sealed class CharVar
        {
            public string VarName;
            public int Value;
            public uint Expiry;   // char_vars.expiry, 0 = never
            public CharVar(string varName, int value, uint expiry = 0) { VarName = varName; Value = value; Expiry = expiry; }
        }

        // Mission[<area>][<id>]<suffix>  (suffix: "Prog", ... or empty).
        static readonly Regex MissionVarRx = new Regex(
            @"^Mission\[(\d+)\]\[(\d+)\](.*)$", RegexOptions.Compiled);

        // Parse a char_vars varname into its area/mission/suffix. Rejects non-mission names and
        // out-of-range area(>=15)/id(>=851) so a stray var never fabricates a wire coordinate.
        public static bool TryParseMissionVar(string varName, out int area, out int missionId, out string suffix)
        {
            area = 0; missionId = 0; suffix = "";
            if (varName == null) return false;
            var m = MissionVarRx.Match(varName);
            if (!m.Success) return false;
            if (!int.TryParse(m.Groups[1].Value, out area) || area < 0 || area >= MissionBitBlob.Areas) return false;
            if (!int.TryParse(m.Groups[2].Value, out missionId) || missionId < 0 || missionId >= MissionBitBlob.MaxMissionId) return false;
            suffix = m.Groups[3].Value;
            return true;
        }

        // Parameterized INSERT matching the Persistence `?`-placeholder contract; column order
        // == MissionStateSchema.Columns.
        public const string InsertSql =
            "INSERT INTO `char_mission_state` " +
            "(`charid`, `area`, `current`, `status`, `completed_mask`, `progress`, `updated_at`) " +
            "VALUES (?,?,?,?,?,?,?) " +
            "ON DUPLICATE KEY UPDATE " +
            "`current`=VALUES(`current`), `status`=VALUES(`status`), `completed_mask`=VALUES(`completed_mask`), " +
            "`progress`=VALUES(`progress`), `updated_at`=VALUES(`updated_at`)";

        public static List<CharMissionStateRow> Import(uint charId, byte[] missionsBlob, IEnumerable<CharVar> charVars)
        {
            var blob = new MissionBitBlob();
            blob.LoadFromBlob(missionsBlob); // verbatim (extractFromBlob overwrites the whole struct)

            // One row per area, faithfully from the raw struct (all 15, so Project reproduces
            // any blob byte-for-byte rather than only the sentinel default).
            var rows = new List<CharMissionStateRow>(MissionBitBlob.Areas);
            var byArea = new CharMissionStateRow[MissionBitBlob.Areas];
            foreach (var cell in MissionWireCodec.Extract(blob))
            {
                var row = new CharMissionStateRow
                {
                    CharId = charId,
                    Area = cell.Area,
                    Current = cell.Current,
                    Status = cell.Status,
                    CompletedMask = cell.CompletedMask,
                };
                byArea[(int)cell.Area] = row;
                rows.Add(row);
            }

            // Fold per-mission soft progress into the owning area row (never the blob).
            if (charVars != null)
            {
                foreach (var cv in charVars)
                {
                    if (!TryParseMissionVar(cv.VarName, out int area, out int mid, out var suffix)) continue;
                    if (suffix.Length == 0) continue; // bare Mission[a][i] is not state
                    string key = mid + ":" + suffix;
                    byArea[area].SoftVars[key] = cv.Value;
                    if (cv.Expiry != 0) byArea[area].Expiry[key] = cv.Expiry;
                }
            }
            return rows; // already area-ascending from Extract (deterministic)
        }

        public static List<MissionCell> ToCells(IEnumerable<CharMissionStateRow> rows)
        {
            var cells = new List<MissionCell>();
            foreach (var r in rows) cells.Add(r.ToCell());
            return cells;
        }
    }
}
