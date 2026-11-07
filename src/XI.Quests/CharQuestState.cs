// XI.Quests — the normalized char_quest_state storage model + the offline blob importer.
//
// This is the STRANGLER-MIGRATION keystone: it unpacks the LEGACY per-character quest
// state (the 704-byte chars.quests bitmask + the Quest[area][id]<suffix> rows in
// char_vars) into NORMALIZED char_quest_state rows, and proves the round-trip:
//
//     Project(ToCells(Import(legacyBlob)))  ==  canonical(legacyBlob)
//
// for every engine-reachable blob (the sole exception is the documented (current=1,
// complete=1) raw pair, which the byte-proven QuestWireCodec also collapses to Completed
// -- the importer inherits that exact lossy edge, asserts it, and never hides it).
//
// The importer's whole point is what the bitmask CANNOT represent: per-quest progression
// (Quest[area][id]Prog) and arbitrary soft vars (mustZone, ...). Those live only in
// char_vars today. The normalized row folds them into first-class columns (step, progress
// JSON) so a quest is a single inspectable record, and the projection back to the wire
// DROPS them without perturbing the bit-for-bit legacy blob -- verified below, since a
// soft-only quest must not leak a wire bit.
//
// There is no C++ oracle for the normalized layer (it is new design); verification is
// the import->Project parity against the SAME quest_golden.txt blobs that anchor the codec
// (which itself is diffed byte-for-byte against the compiled engine bit logic). The DDL and
// the parameterized INSERT are design/storage contracts asserted for shape offline, not
// executed (no live MariaDB in this gate).
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace XI.Quests
{
    // One normalized char_quest_state row: the authoritative per-character record for one
    // quest container. Maps 1:1 to the CREATE TABLE in QuestStateSchema. Status mirrors the
    // legacy bit state (the wire/authoritative tri-state the proven codec encodes); Step and
    // SoftVars are the normalized state the 704-byte blob cannot hold.
    public sealed class CharQuestStateRow
    {
        public uint CharId;
        public string QuestKey;           // stable string id (from registry) or synthetic "legacy:<area>/<id>"
        public QuestLog Area;             // wire area (denormalized: projection never needs the registry back)
        public ushort QuestId;            // numeric wire id
        public QuestStatus Status;        // Available / Accepted / Completed  (== the legacy bits)
        public int Step;                  // from Quest[area][id]Prog char_var (0 if absent)
        public long RepeatByVanaTime;     // 0 for legacy imports (the bitmask has no repeat notion)
        public long UpdatedAt;            // caller/DB supplied; 0 for an offline import

        // Other Quest[area][id]<suffix> char_vars (mustZone, ...) that the blob cannot carry.
        public readonly Dictionary<string, long> SoftVars = new Dictionary<string, long>();

        // The non-zero char_vars expiries (unix seconds) of this quest's vars: "Prog" for Step, else the SoftVars key.
        public readonly Dictionary<string, uint> Expiry = new Dictionary<string, uint>();

        public QuestWireRef Wire => new QuestWireRef(Area, QuestId);

        // Canonical, deterministic JSON of the soft vars (sorted keys) and their expiries (ProgressJsonCodec). Null when
        // empty, so a row with no progression is indistinguishable from a pure-legacy quest on the wire.
        public string ProgressJson => ProgressJsonCodec.Write(SoftVars, Expiry);
    }

    // The target table's DDL. Documents the normalized storage the importer fills. Column
    // conventions mirror the existing char_vars.sql (int unsigned charid, PK (charid, key),
    // secondary indexes for area/id lookups).
    public static class QuestStateSchema
    {
        public const string TableName = "char_quest_state";

        public const string CreateTableSql =
            "CREATE TABLE IF NOT EXISTS `char_quest_state` (\n" +
            "  `charid`      int(10) unsigned NOT NULL,\n" +
            "  `quest_key`   varchar(96) NOT NULL,\n" +
            "  `area`        tinyint(3) unsigned NOT NULL,\n" +
            "  `quest_id`    smallint(5) unsigned NOT NULL,\n" +
            "  `status`      tinyint(3) unsigned NOT NULL DEFAULT 0,\n" +
            "  `step`        int(11) NOT NULL DEFAULT 0,\n" +
            "  `progress`    json DEFAULT NULL,\n" +
            "  `repeat_by`   bigint(20) NOT NULL DEFAULT 0,\n" +
            "  `updated_at`  timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,\n" +
            "  PRIMARY KEY (`charid`, `quest_key`),\n" +
            "  UNIQUE KEY `uq_char_quest_wire` (`charid`, `area`, `quest_id`),\n" +
            "  KEY `idx_char_quest_area` (`area`, `quest_id`)\n" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;";

        // The 9 columns in INSERT / bind order.
        public static readonly string[] Columns =
        {
            "charid", "quest_key", "area", "quest_id", "status", "step", "progress", "repeat_by", "updated_at"
        };
    }

    // Stable string id <-> (area, numeric id). Legacy blobs carry only the wire coordinate;
    // the registry recovers the stable key from xi.quest.id (KeyFor) when content declares it. It is optional:
    // an importer run with no registry still keys every blob quest with a deterministic
    // synthetic id, so no quest is dropped for lack of a mapping.
    public sealed class QuestKeyRegistry
    {
        private readonly Dictionary<QuestWireRef, string> _refToKey = new Dictionary<QuestWireRef, string>();
        private readonly Dictionary<string, QuestWireRef> _keyToRef = new Dictionary<string, QuestWireRef>();

        // the first registration of a key or a wire wins: false (nothing changed) when either is taken, so no two wires
        // ever share a key (the rows' primary key is (charid, quest_key))
        public bool Register(string key, QuestWireRef wire)
        {
            if (_refToKey.ContainsKey(wire) || _keyToRef.ContainsKey(key)) return false;
            _refToKey[wire] = key;
            _keyToRef[key] = wire;
            return true;
        }

        // xi.quest.id.<areaName>.<CONST> -> "<areaName>.<const>" (bastok.the_elvaan_goldsmith); the area keeps its Lua spelling
        public static string KeyFor(string areaName, string constName) => areaName + "." + constName.ToLowerInvariant();

        public bool TryGetKey(QuestWireRef wire, out string key) => _refToKey.TryGetValue(wire, out key);
        public bool TryGetRef(string key, out QuestWireRef wire) => _keyToRef.TryGetValue(key, out wire);

        public string KeyOrSynthetic(QuestWireRef wire)
            => _refToKey.TryGetValue(wire, out var k) ? k : Synthetic(wire);

        // Deterministic fallback key for a blob quest with no registered stable id.
        public static string Synthetic(QuestWireRef wire) => "legacy:" + (int)wire.Area + "/" + wire.QuestId;

        public int Count => _refToKey.Count;
    }

    // The keys of mission and battlefield containers: mission.<log lower>.<const lower> and battlefield.<const lower>.
    // MissionKey.g.cs and BattlefieldKey.g.cs hold the same strings, generated from the Lua tables.
    public static class ContainerKeys
    {
        public static string Mission(string logName, string constName) =>
            "mission." + logName.ToLowerInvariant() + "." + constName.ToLowerInvariant();

        public static string Battlefield(string constName) => "battlefield." + constName.ToLowerInvariant();

        public static bool IsMission(string key) => key.StartsWith("mission.", StringComparison.Ordinal);

        public static bool IsBattlefield(string key) => key.StartsWith("battlefield.", StringComparison.Ordinal);
    }

    // Unpacks the legacy per-character quest state into normalized rows, and projects rows
    // back to the wire cells the byte-proven codec consumes. Fully offline.
    public static class QuestStateImporter
    {
        // One row of char_vars, as the migration would read it: varname + value. (expiry included: a step or soft var keeps it in the row's Expiry)
        public sealed class CharVar
        {
            public string VarName;
            public int Value;
            public uint Expiry;   // char_vars.expiry, 0 = never
            public CharVar(string varName, int value, uint expiry = 0) { VarName = varName; Value = value; Expiry = expiry; }
        }

        // Quest[<area>][<id>]<suffix>  (suffix: "Prog", "mustZone", ... or empty).
        static readonly Regex QuestVarRx = new Regex(
            @"^Quest\[(\d+)\]\[(\d+)\](.*)$", RegexOptions.Compiled);

        // Parse a char_vars varname into its wire ref + suffix. Rejects non-quest names and
        // out-of-range area/id so a stray var can never fabricate a wire coordinate.
        public static bool TryParseQuestVar(string varName, out QuestWireRef wire, out string suffix)
        {
            wire = default;
            suffix = "";
            if (varName == null) return false;
            var m = QuestVarRx.Match(varName);
            if (!m.Success) return false;
            if (!int.TryParse(m.Groups[1].Value, out int area) || area < 0 || area >= QuestBitBlob.Areas) return false;
            if (!int.TryParse(m.Groups[2].Value, out int id) || id < 0 || id >= QuestBitBlob.MaxQuestId) return false;
            wire = new QuestWireRef((QuestLog)area, (ushort)id);
            suffix = m.Groups[3].Value;
            return true;
        }

        // Parameterized INSERT matching the Persistence `?`-placeholder contract: bind once,
        // execute per row. Column order == QuestStateSchema.Columns.
        public const string InsertSql =
            "INSERT INTO `char_quest_state` " +
            "(`charid`, `quest_key`, `area`, `quest_id`, `status`, `step`, `progress`, `repeat_by`, `updated_at`) " +
            "VALUES (?,?,?,?,?,?,?,?,?) " +
            "ON DUPLICATE KEY UPDATE " +
            "`status`=VALUES(`status`), `step`=VALUES(`step`), `progress`=VALUES(`progress`), " +
            "`repeat_by`=VALUES(`repeat_by`), `updated_at`=VALUES(`updated_at`)";

        public static List<CharQuestStateRow> Import(
            uint charId, byte[] questsBlob, IEnumerable<CharVar> charVars, QuestKeyRegistry registry)
        {
            var blob = new QuestBitBlob();
            blob.LoadFromBlob(questsBlob);

            // Fold char_vars into (prog step) + soft suffix buckets, keyed by wire ref.
            var progByRef = new Dictionary<QuestWireRef, int>();
            var softByRef = new Dictionary<QuestWireRef, Dictionary<string, long>>();
            var expByRef = new Dictionary<QuestWireRef, Dictionary<string, uint>>();
            if (charVars != null)
            {
                foreach (var cv in charVars)
                {
                    if (!TryParseQuestVar(cv.VarName, out var wire, out var suffix)) continue;
                    if (suffix.Length == 0) continue; // bare Quest[a][i] is not used for state
                    if (cv.Expiry != 0)
                    {
                        if (!expByRef.TryGetValue(wire, out var e)) { e = new Dictionary<string, uint>(); expByRef[wire] = e; }
                        e[suffix] = cv.Expiry; // "Prog" for the step, else the soft var's suffix
                    }
                    if (suffix == "Prog")
                    {
                        progByRef[wire] = cv.Value;
                    }
                    else
                    {
                        if (!softByRef.TryGetValue(wire, out var d)) { d = new Dictionary<string, long>(); softByRef[wire] = d; }
                        d[suffix] = cv.Value;
                    }
                }
            }

            var rows = new Dictionary<QuestWireRef, CharQuestStateRow>();

            // 1) every non-Available blob bit -> a row, using the engine's own precedence
            //    (getQuestStatus via Extract) so status agrees with the codec exactly.
            foreach (var cell in QuestWireCodec.Extract(blob))
                rows[new QuestWireRef(cell.Area, cell.Id)] =
                    MakeRow(charId, cell.Area, cell.Id, cell.Status, progByRef, softByRef, expByRef, registry);

            // 2) a quest with only soft state (a Prog / suffix var but no wire bit -- a real
            //    legacy shape, e.g. progression recorded before the current bit is flipped)
            //    still gets a row, at Status=Available. It projects to NO wire bit (step 3
            //    asserts that), so the normalized->wire round-trip stays byte-exact.
            var softOnly = new HashSet<QuestWireRef>();
            foreach (var k in progByRef.Keys) softOnly.Add(k);
            foreach (var k in softByRef.Keys) softOnly.Add(k);
            foreach (var wire in softOnly)
                if (!rows.ContainsKey(wire))
                    rows[wire] = MakeRow(charId, wire.Area, wire.QuestId, QuestStatus.Available, progByRef, softByRef, expByRef, registry);

            // Deterministic order: area, then numeric id (independent of char_vars/HashSet order).
            var list = new List<CharQuestStateRow>(rows.Values);
            list.Sort((a, b) => a.Area != b.Area
                ? ((int)a.Area).CompareTo((int)b.Area)
                : a.QuestId.CompareTo(b.QuestId));
            return list;
        }

        static CharQuestStateRow MakeRow(
            uint charId, QuestLog area, ushort id, QuestStatus status,
            Dictionary<QuestWireRef, int> progByRef,
            Dictionary<QuestWireRef, Dictionary<string, long>> softByRef,
            Dictionary<QuestWireRef, Dictionary<string, uint>> expByRef,
            QuestKeyRegistry registry)
        {
            var wire = new QuestWireRef(area, id);
            var row = new CharQuestStateRow
            {
                CharId = charId,
                Area = area,
                QuestId = id,
                Status = status,
            };
            row.QuestKey = registry != null ? registry.KeyOrSynthetic(wire) : QuestKeyRegistry.Synthetic(wire);
            if (progByRef.TryGetValue(wire, out int step)) row.Step = step;
            if (softByRef.TryGetValue(wire, out var d))
                foreach (var kv in d) row.SoftVars[kv.Key] = kv.Value;
            if (expByRef.TryGetValue(wire, out var ex))
                foreach (var kv in ex) row.Expiry[kv.Key] = kv.Value;
            return row;
        }

        // normalized rows -> wire cells for the byte-proven projection codec. Available rows
        // are emitted too; QuestWireCodec.Project maps Available to the zero bit, so a soft
        // progression never contributes a legacy bit on the round-trip.
        public static IEnumerable<QuestCell> ToCells(IEnumerable<CharQuestStateRow> rows)
        {
            foreach (var r in rows)
                yield return new QuestCell(r.Area, r.QuestId, r.Status);
        }
    }
}
