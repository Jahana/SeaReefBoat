// XI.Quests -- normalized char_assault_state storage model + offline blob importer, the
// assault half symmetric to CharMissionState.cs / CharEminenceState.cs.
//
// Unpacks the LEGACY per-char assault state (the 130-byte chars.assault blob = assaultlog_t
// { uint16 current; bool complete[128]; }, a SINGLE all-zero-init container) into a NORMALIZED
// char_assault_state row -- ONE ROW PER CHARACTER (assault is not per-area like missions, nor
// per-record like eminence) -- and proves the round-trip:
//
//     Project(ToCell(Import(legacyBlob))) == legacyBlob   -- byte-for-byte, for every golden
//
// Shape contrast with the other three blobs (the reason this is the SIMPLEST normalized model):
//   * quests   -> per (area,quest_id) tri-state, LOSSY (bit-pair collapse)
//   * missions -> per AREA cell, LOSSLESS (15 rows/char)
//   * eminence -> per RECORD cell, LOSSLESS (variable rows/char)
//   * assault  -> per CHARACTER cell, LOSSLESS (exactly 1 row/char)
//
// Assault carries only two facts for the whole container: the current assault id (raw uint16,
// though the engine truncates to uint8 on read/compare) and a 128-entry completion set. There is
// NO char_var / SoftVars layer to fold here: scripts/globals/assault/container.lua shows the only
// assault char_vars are FLAT GLOBALS (assaultEntered, AssaultFailed, AssaultComplete,
// Assault_Armband, AssaultPromotion) -- not per-assault-id progress -- and the `Assault[%d]`
// varPrefix (container.lua getVarPrefix) exists ONLY as the container cleanup scope handed to
// Container:new, never as `Assault[i]Prog`. So a quest-style per-id importer would model nothing;
// the faithful normalized store is one row carrying the full container. The 128 completion flags
// collapse into a 16-byte bitmask that Project replays byte-for-byte.
//
// Honest lossless BOUNDARY (documented, not silently claimed): Extract reads each raw complete[]
// byte as a bit (byte != 0) and Project writes it back as 0/1. The engine writes complete[] ONLY
// as (byte)(v?1:0), so every reachable byte is already 0/1 and the round-trip is byte-exact for
// all goldens; a corrupt non-0/1 legacy byte would normalize to its tested bit (the same boundary
// missions carry). No C++ oracle for the normalized layer (new design); verification is the
// Import->Project parity vs the SAME assault_golden.txt raw bytes that anchor AssaultBitBlob
// (itself byte-for-byte vs a compiled oracle). DDL/INSERT are shape-asserted offline, NOT run
// against a live DB.
using System;
using System.Collections.Generic;

namespace XI.Quests
{
    // The whole assault container as one normalized cell: raw current id + 128 completion bits.
    public sealed class AssaultCell
    {
        public const int CompletedBits = 128;
        public const int MaskBytes = CompletedBits / 8; // 16

        public ushort Current;                    // raw uint16 current (engine uint8-truncates on read)
        private readonly byte[] _mask = new byte[MaskBytes];

        public bool Completed(int assaultId) =>
            assaultId >= 0 && assaultId < CompletedBits && ((_mask[assaultId / 8] >> (assaultId % 8)) & 1) != 0;

        public void SetCompleted(int assaultId, bool v)
        {
            if (assaultId < 0 || assaultId >= CompletedBits) return;
            if (v) _mask[assaultId / 8] |= (byte)(1 << (assaultId % 8));
            else _mask[assaultId / 8] &= (byte)~(1 << (assaultId % 8));
        }

        public byte[] MaskBytesValue => (byte[])_mask.Clone();

        public AssaultCell Clone()
        {
            var c = new AssaultCell { Current = Current };
            Buffer.BlockCopy(_mask, 0, c._mask, 0, MaskBytes);
            return c;
        }
    }

    public static class AssaultWireCodec
    {
        // Extract the single container cell from a raw 130-byte blob (faithful capture).
        public static AssaultCell Extract(AssaultBitBlob blob)
        {
            var c = new AssaultCell { Current = blob.RawGetCurrent() };
            for (int id = 0; id < AssaultCell.CompletedBits; id++)
                c.SetCompleted(id, blob.RawGetCompleteFlag(id));
            return c;
        }

        // Project the normalized cell onto a fresh 130-byte blob (faithful, byte-exact). A fresh
        // AssaultBitBlob is all-zero (no sentinel, unlike missions), so an empty cell reproduces
        // the all-zero golden exactly.
        public static AssaultBitBlob Project(AssaultCell cell)
        {
            var blob = new AssaultBitBlob();
            blob.RawSetCurrent(cell.Current);
            for (int id = 0; id < AssaultCell.CompletedBits; id++)
                blob.RawSetComplete(id, cell.Completed(id));
            return blob;
        }
    }

    // One normalized char_assault_state row: the authoritative per-character assault record
    // (the container is per-character, so exactly one row per char). Maps 1:1 to the table in
    // AssaultStateSchema. No soft-var/JSON columns -- assault carries no per-id char_var progress.
    public sealed class CharAssaultStateRow
    {
        public uint CharId;
        public ushort Current;
        public byte[] CompletedMask = new byte[AssaultCell.MaskBytes]; // 16 bytes = 128 bits
        public long UpdatedAt;             // caller/DB supplied; 0 for an offline import

        public AssaultCell ToCell()
        {
            var c = new AssaultCell { Current = Current };
            for (int id = 0; id < AssaultCell.CompletedBits; id++)
            {
                bool set = id < CompletedMask.Length * 8 && ((CompletedMask[id / 8] >> (id % 8)) & 1) != 0;
                c.SetCompleted(id, set);
            }
            return c;
        }
    }

    // The char_assault_state table DDL + column list. Per-character PK (assault is a single
    // container). `completed_mask` is a 16-byte binary bitmap (bit i == complete[i]). Conventions
    // mirror char_vars.sql (int unsigned charid etc.).
    public static class AssaultStateSchema
    {
        public const string TableName = "char_assault_state";

        public const string CreateTableSql =
            "CREATE TABLE IF NOT EXISTS `char_assault_state` (\n" +
            "  `charid`         int(10) unsigned NOT NULL,\n" +
            "  `current`        smallint(5) unsigned NOT NULL DEFAULT 0,\n" +
            "  `completed_mask` varbinary(16) NOT NULL DEFAULT '',\n" +
            "  `updated_at`     timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,\n" +
            "  PRIMARY KEY (`charid`)\n" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;";

        public static readonly string[] Columns =
        {
            "charid", "current", "completed_mask", "updated_at"
        };
    }

    public static class AssaultStateImporter
    {
        // Parameterized INSERT matching the Persistence `?`-placeholder contract; column order
        // == AssaultStateSchema.Columns.
        public const string InsertSql =
            "INSERT INTO `char_assault_state` " +
            "(`charid`, `current`, `completed_mask`, `updated_at`) " +
            "VALUES (?,?,?,?) " +
            "ON DUPLICATE KEY UPDATE " +
            "`current`=VALUES(`current`), `completed_mask`=VALUES(`completed_mask`), " +
            "`updated_at`=VALUES(`updated_at`)";

        // Unpack a legacy 130-byte chars.assault blob into one normalized row. LoadFromBlob takes
        // the bytes VERBATIM (the engine's extractFromBlob overwrites the whole struct).
        public static CharAssaultStateRow Import(uint charId, byte[] assaultBlob)
        {
            var blob = new AssaultBitBlob();
            blob.LoadFromBlob(assaultBlob);
            var cell = AssaultWireCodec.Extract(blob);

            var mask = new byte[AssaultCell.MaskBytes];
            for (int id = 0; id < AssaultCell.CompletedBits; id++)
                if (cell.Completed(id)) mask[id / 8] |= (byte)(1 << (id % 8));

            return new CharAssaultStateRow
            {
                CharId = charId,
                Current = cell.Current,
                CompletedMask = mask,
            };
        }

        public static AssaultCell ToCell(CharAssaultStateRow row) => row.ToCell();
    }
}
