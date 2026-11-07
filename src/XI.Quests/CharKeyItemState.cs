// XI.Quests -- normalized char_keyitem_state storage model + offline blob importer, the
// keyitems half symmetric to CharEminenceState.cs / CharAssaultState.cs.
//
// Unpacks the LEGACY per-char key-item state (the 1024-byte chars.keyitems blob =
// keyitems_t { keyitems_table_t tables[8] }, each = xi::bitset<512> keyList + xi::bitset<512>
// seenList) into NORMALIZED char_keyitem_state rows -- ONE ROW PER DISTINCT KEY-ITEM ID that
// carries either bit -- and proves the round-trip:
//
//     Project(ToCells(Import(legacyBlob))) == legacyBlob   -- byte-for-byte, for every golden
//
// Shape contrast with the other four blobs:
//   * quests   -> per (area,quest_id) tri-state, LOSSY (bit-pair collapse)
//   * missions -> per AREA cell, LOSSLESS
//   * assault  -> ONE row per char (current + 128-bit mask), LOSSLESS
//   * eminence -> per RECORD cell (active slot + progress + completion), LOSSLESS
//   * keyitems -> per KEY-ITEM cell, two ORTHOGONAL bools (possessed / seen), LOSSLESS
//
// The two 512-bit bitsets per table are independent: a key item can be SEEN but not HELD
// (discovered via the 0x055 LookItemFlag but never added) and HELD-then-DROPPED while still
// seen (the add_then_del golden), exactly as eminence keeps active[] and complete[] separate.
// The whole 1024-byte blob IS these 2 x 4096 bits (id -> table=id/512, index=id%512; keyList
// bit at table*128+idx/8, seenList at table*128+64+idx/8), so iterating ids 0..4095 and
// capturing both bits covers every byte with no loss -- unlike quests there is no bit-pair
// collapse and no slot/progress to keep. There is NO char_var / SoftVars layer: grep of
// scripts/ shows the only per-id `Prog` is the QUEST container's vars.Prog, never a key-item
// KeyItem[a][b]Prog, so this importer folds ONLY the blob (no soft state to leak onto the wire).
//
// No C++ oracle for the normalized layer (new design); verification is the Import->Project
// parity vs the SAME keyitems_golden.txt raw bytes that anchor KeyItemsBitBlob (itself byte-for-
// byte vs a compiled oracle). DDL/INSERT are shape-asserted offline, not run against a live DB.
using System;
using System.Collections.Generic;

namespace XI.Quests
{
    // One normalized key-item fact: everything chars.keyitems can say about a single key-item id.
    // Possessed == keyList bit (player holds it); Seen == seenList bit (player has discovered it).
    public sealed class KeyItemCell
    {
        public ushort KeyItemId;
        public bool Possessed;   // keyList bit
        public bool Seen;        // seenList bit

        public KeyItemCell Clone() => new KeyItemCell
        {
            KeyItemId = KeyItemId, Possessed = Possessed, Seen = Seen,
        };
    }

    public static class KeyItemWireCodec
    {
        // Extract every distinct key-item cell that carries either bit. Scans the full id space
        // 0..4095 (8 tables x 512) -- the raw view ignores the engine's capacity guard, so a
        // hand-authored blob with bits in every table is captured completely. Key-item-id-ascending
        // (deterministic).
        public static List<KeyItemCell> Extract(KeyItemsBitBlob blob)
        {
            var cells = new List<KeyItemCell>();
            for (int id = 0; id < KeyItemsBitBlob.IdSpace; id++)
            {
                ushort u = (ushort)id;
                bool key = blob.RawGetKey(u);
                bool seen = blob.RawGetSeen(u);
                if (!key && !seen) continue;
                cells.Add(new KeyItemCell { KeyItemId = u, Possessed = key, Seen = seen });
            }
            return cells;
        }

        // Project the normalized cells onto a fresh 1024-byte blob (faithful, byte-exact). A fresh
        // blob is all-zero, so any id absent from the cells contributes zero bits in both bitsets --
        // reproducing every golden exactly.
        public static KeyItemsBitBlob Project(IEnumerable<KeyItemCell> cells)
        {
            var blob = new KeyItemsBitBlob();
            foreach (var c in cells)
            {
                if (c.Possessed) blob.RawSetKey(c.KeyItemId, true);
                if (c.Seen) blob.RawSetSeen(c.KeyItemId, true);
            }
            return blob;
        }
    }

    // One normalized char_keyitem_state row: the authoritative per-character record for one
    // key-item id. Maps 1:1 to the table in KeyItemStateSchema. No soft-var/JSON columns --
    // keyitems carries no char_var progress (see file header).
    public sealed class CharKeyItemStateRow
    {
        public uint CharId;
        public ushort KeyItemId;
        public bool Possessed;
        public bool Seen;
        public long UpdatedAt;   // caller/DB supplied; 0 for an offline import

        public KeyItemCell ToCell() => new KeyItemCell
        {
            KeyItemId = KeyItemId, Possessed = Possessed, Seen = Seen,
        };
    }

    // The char_keyitem_state table DDL + column list. Per-key-item PK. Conventions mirror
    // char_vars.sql / the other per-id state tables.
    public static class KeyItemStateSchema
    {
        public const string TableName = "char_keyitem_state";

        public const string CreateTableSql =
            "CREATE TABLE IF NOT EXISTS `char_keyitem_state` (\n" +
            "  `charid`      int(10) unsigned NOT NULL,\n" +
            "  `key_item_id` smallint(5) unsigned NOT NULL,\n" +
            "  `possessed`   tinyint(1) NOT NULL DEFAULT 0,\n" +
            "  `seen`        tinyint(1) NOT NULL DEFAULT 0,\n" +
            "  `updated_at`  timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,\n" +
            "  PRIMARY KEY (`charid`, `key_item_id`)\n" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;";

        public static readonly string[] Columns =
        {
            "charid", "key_item_id", "possessed", "seen", "updated_at"
        };
    }

    public static class KeyItemStateImporter
    {
        // Parameterized INSERT matching the Persistence `?`-placeholder contract; column order
        // == KeyItemStateSchema.Columns.
        public const string InsertSql =
            "INSERT INTO `char_keyitem_state` " +
            "(`charid`, `key_item_id`, `possessed`, `seen`, `updated_at`) " +
            "VALUES (?,?,?,?,?) " +
            "ON DUPLICATE KEY UPDATE " +
            "`possessed`=VALUES(`possessed`), `seen`=VALUES(`seen`), `updated_at`=VALUES(`updated_at`)";

        // Unpack a legacy 1024-byte chars.keyitems blob into normalized rows (key-item-id-ascending).
        // LoadFromBlob takes the bytes VERBATIM (the engine's extractFromBlob overwrites the whole
        // struct; fresh init is all-zero anyway).
        public static List<CharKeyItemStateRow> Import(uint charId, byte[] keyitemsBlob)
        {
            var blob = new KeyItemsBitBlob();
            blob.LoadFromBlob(keyitemsBlob);

            var rows = new List<CharKeyItemStateRow>();
            foreach (var c in KeyItemWireCodec.Extract(blob))
            {
                rows.Add(new CharKeyItemStateRow
                {
                    CharId = charId,
                    KeyItemId = c.KeyItemId,
                    Possessed = c.Possessed,
                    Seen = c.Seen,
                });
            }
            return rows;
        }

        public static List<KeyItemCell> ToCells(IEnumerable<CharKeyItemStateRow> rows)
        {
            var cells = new List<KeyItemCell>();
            foreach (var r in rows) cells.Add(r.ToCell());
            return cells;
        }
    }
}
