// XI.Quests -- normalized char_eminence_state storage model + offline blob importer, the
// eminence half symmetric to CharMissionState.cs / CharQuestState.cs.
//
// Unpacks the LEGACY per-char Record-of-Eminence state (the 700-byte chars.eminence blob =
// eminencelog_t { uint16 active[31]; [2 pad]; uint32 progress[31]; uint8 complete[512] }) into
// NORMALIZED char_eminence_state rows -- ONE ROW PER DISTINCT RECORD ID -- and proves the
// round-trip:
//
//     Project(ToCells(Import(legacyBlob))) == legacyBlob   -- byte-for-byte, for every golden
//
// Shape contrast with the other three blobs (this is the whole reason the normalized model is
// per-record here rather than per-area / per-wire-coordinate):
//   * quests  -> per (area,quest_id) tri-state, LOSSY (bit-pair collapse)
//   * missions-> per AREA cell, LOSSLESS (full struct kept)
//   * eminence-> per RECORD cell, LOSSLESS, except one id in two active slots (see below)
//
// The active[]/progress[] arrays are a 31-slot ORDERED window over records; complete[] is a
// 4096-bit flag set keyed by record id. A record can be ACTIVE (in a slot, with progress)
// AND/OR COMPLETED -- those are independent (roe.cpp DelEminenceRecord clears active/progress
// but NEVER the complete bit; see the del_keeps_completion golden). So the natural normalized
// row is keyed by record_id, carrying the physical slot it currently occupies (to preserve the
// retail ordering that byte-exact reproduction demands), its in-struct progress, and a bool
// completed flag. There is NO char_var / SoftVars layer here: getEminenceProgress reads the
// in-struct progress[] (roe.lua:304, CBaseEntity.lua:1908), unlike missions' Mission[a][i]Prog,
// so this importer folds ONLY the blob -- no soft state to leak onto the wire.
//
// Honest lossless boundary (documented, not silently claimed): keying by record_id collapses
// the case of the SAME id occupying two active slots at once. The engine CAN reach it through a
// timed id: 0x10C's timed-record guard never fires (ParseTimedSchedule resets TimedRecords after
// the records set it, roe.cpp:198) and addEminenceRecord scans slots 0..29 only, so a timed id in
// slot 30 can also be taken into a normal slot. A row keeps the last slot, which is why the zone
// never projects eminence onto its array (ProgressRows.Load). Every oracle
// golden has distinct slots, so Project(Extract)==blob holds for all of them; the gate asserts
// distinct-slot + the byte parity rather than claiming losslessness for the two-slot blob. No C++ oracle for the normalized layer (new design); verification is the
// Import->Project parity vs the SAME eminence_golden.txt raw bytes that anchor EminenceBitBlob
// (itself byte-for-byte vs a compiled oracle). DDL/INSERT are shape-asserted offline, not run
// against a live DB.
using System;
using System.Collections.Generic;

namespace XI.Quests
{
    // One normalized record fact: everything chars.eminence can say about a single record id.
    // Slot == -1 means the record is not currently in the active window (completed-only).
    public sealed class EminenceCell
    {
        public uint RecordId;
        public int Slot = -1;        // physical active[] slot 0..30, or -1 when not active
        public uint Progress;        // in-struct progress[slot] (meaningful only when Slot >= 0)
        public bool Completed;       // complete[] bit for RecordId (independent of Slot)

        public bool IsActive => Slot >= 0;

        public EminenceCell Clone() => new EminenceCell
        {
            RecordId = RecordId, Slot = Slot, Progress = Progress, Completed = Completed,
        };
    }

    public static class EminenceWireCodec
    {
        // Extract the distinct-record cells from a raw eminence blob (faithful capture of all
        // three sub-arrays). Rows come back record_id-ascending (deterministic).
        public static List<EminenceCell> Extract(EminenceBitBlob blob)
        {
            var byId = new Dictionary<uint, EminenceCell>();

            // active[]/progress[] -- record id occupies a physical slot; keep the slot so a
            // hand-authored blob with gaps (active in slot 5, 0..4 empty) reproduces exactly.
            for (int slot = 0; slot < blob.SlotCount; slot++)
            {
                ushort id = blob.RawGetActive(slot);
                if (id == 0) continue; // slot 0 == empty window position
                if (!byId.TryGetValue(id, out var c))
                {
                    c = new EminenceCell { RecordId = id };
                    byId[id] = c;
                }
                c.Slot = slot;
                c.Progress = blob.RawGetProgress(slot);
            }

            // complete[] -- keyed by record id, merges onto an active cell or stands alone.
            for (uint id = 0; id < EminenceBitBlob.MaxRecordId; id++)
            {
                if (!blob.GetCompletion(id)) continue;
                if (!byId.TryGetValue(id, out var c))
                {
                    c = new EminenceCell { RecordId = id };
                    byId[id] = c;
                }
                c.Completed = true;
            }

            var cells = new List<EminenceCell>(byId.Values);
            cells.Sort((a, b) => a.RecordId.CompareTo(b.RecordId));
            return cells;
        }

        // Project the normalized cells onto a fresh 700-byte blob (faithful, byte-exact). A
        // fresh blob is all-zero, so the 2 padding bytes stay zero and no active slot is set
        // unless a cell carries it -- reproducing the blob exactly, except for one id in two active slots.
        public static EminenceBitBlob Project(IEnumerable<EminenceCell> cells)
        {
            var blob = new EminenceBitBlob();
            foreach (var c in cells)
            {
                if (c.Slot >= 0 && c.Slot < blob.SlotCount)
                    blob.RawSetSlot(c.Slot, (ushort)c.RecordId, c.Progress);
                if (c.Completed)
                    blob.SetCompletion(c.RecordId, true); // guarded id<4096 inside (no-op above)
            }
            return blob;
        }
    }

    // One normalized char_eminence_state row: the authoritative per-character record for one
    // Record-of-Eminence id. Maps 1:1 to the table in EminenceStateSchema. No soft-var/JSON
    // columns -- eminence carries no char_var progress (see file header).
    public sealed class CharEminenceStateRow
    {
        public uint CharId;
        public uint RecordId;
        public int Slot;               // -1 == not currently active (completed-only row)
        public uint Progress;
        public bool Completed;
        public long UpdatedAt;         // caller/DB supplied; 0 for an offline import

        public EminenceCell ToCell() => new EminenceCell
        {
            RecordId = RecordId, Slot = Slot, Progress = Progress, Completed = Completed,
        };
    }

    // The char_eminence_state table DDL + column list. Per-record PK (a RoE record is the
    // natural unit). `slot` is a tinyint with 255 == "inactive" (SQL has no -1 for unsigned;
    // the importer maps int Slot -1 <-> stored 255). Conventions mirror char_vars.sql.
    public static class EminenceStateSchema
    {
        public const string TableName = "char_eminence_state";
        public const int InactiveSlotColumn = 255; // stored value for an inactive record

        public const string CreateTableSql =
            "CREATE TABLE IF NOT EXISTS `char_eminence_state` (\n" +
            "  `charid`     int(10) unsigned NOT NULL,\n" +
            "  `record_id`  int(10) unsigned NOT NULL,\n" +
            "  `slot`       tinyint(3) unsigned NOT NULL DEFAULT 255,\n" +
            "  `progress`   int(10) unsigned NOT NULL DEFAULT 0,\n" +
            "  `completed`  tinyint(1) NOT NULL DEFAULT 0,\n" +
            "  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,\n" +
            "  PRIMARY KEY (`charid`, `record_id`)\n" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;";

        public static readonly string[] Columns =
        {
            "charid", "record_id", "slot", "progress", "completed", "updated_at"
        };
    }

    public static class EminenceStateImporter
    {
        // Parameterized INSERT matching the Persistence `?`-placeholder contract; column order
        // == EminenceStateSchema.Columns.
        public const string InsertSql =
            "INSERT INTO `char_eminence_state` " +
            "(`charid`, `record_id`, `slot`, `progress`, `completed`, `updated_at`) " +
            "VALUES (?,?,?,?,?,?) " +
            "ON DUPLICATE KEY UPDATE " +
            "`slot`=VALUES(`slot`), `progress`=VALUES(`progress`), " +
            "`completed`=VALUES(`completed`), `updated_at`=VALUES(`updated_at`)";

        // Unpack a legacy 700-byte chars.eminence blob into normalized rows (record_id-ascending).
        // LoadFromBlob takes the bytes VERBATIM (the engine's extractFromBlob overwrites the whole
        // struct, so a fresh-init sentinel path is not applied).
        public static List<CharEminenceStateRow> Import(uint charId, byte[] eminenceBlob)
        {
            var blob = new EminenceBitBlob();
            blob.LoadFromBlob(eminenceBlob);

            var rows = new List<CharEminenceStateRow>();
            foreach (var c in EminenceWireCodec.Extract(blob))
            {
                rows.Add(new CharEminenceStateRow
                {
                    CharId = charId,
                    RecordId = c.RecordId,
                    Slot = c.Slot,
                    Progress = c.Progress,
                    Completed = c.Completed,
                });
            }
            return rows;
        }

        public static List<EminenceCell> ToCells(IEnumerable<CharEminenceStateRow> rows)
        {
            var cells = new List<EminenceCell>();
            foreach (var r in rows) cells.Add(r.ToCell());
            return cells;
        }
    }
}
