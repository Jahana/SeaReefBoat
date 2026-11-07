// XI.Quests -- the unified per-character migration seam. The per-container importers
// (CharQuestState / CharMissionState / CharAssaultState / CharCampaignState / CharEminenceState) are each proven
// byte-exact IN ISOLATION. The real strangler migration does not migrate one blob at a time: it
// reads ONE legacy `chars` row -- six whole-struct blob columns (quests 704B, missions 1050B,
// assault 130B, campaign 514B, eminence 700B, key items) PLUS the char that row's `char_vars` -- and must land all
// the normalized tables while reproducing the legacy blobs simultaneously. This file is that
// single entry point and the contract it enforces:
//
//     for each of the blob columns:  Project(Import(legacyRow)) == that column's legacy bytes
//                                  (quests == canonical projection; the others raw-exact, eminence except one id in two active slots)
//
// The load-bearing correctness property here -- which per-blob gates CANNOT prove -- is that
// char_vars is ONE flat table shared by two prefix-regex importers:
//   * Quest[area][id]<suffix>   -> QuestStateImporter
//   * Mission[area][id]<suffix> -> MissionStateImporter
//   * assaultEntered / AssaultComplete / ... (FLAT GLOBALS)  -> belong to NO importer
// Each importer's anchored regex must select ONLY its own prefix, so feeding the FULL char_vars
// set to ImportAll must produce quest rows identical to feeding it only the Quest[*] subset, and
// mission rows identical to feeding it only the Mission[*] subset -- no cross-routing, and the
// flat-global assault vars must never fabricate a quest/mission row. ImportAll fans the neutral
// CharVar list out to each importer's own typed CharVar, so callers pass one list.
//
// Verification stays offline + golden-anchored (no live MariaDB, no C++ oracle for this seam --
// it is pure composition over the already-diffed importers + three byte-verified codecs):
// the migration gate (Program.RunMigrationGate) drives ImportAll with one synthetic char built
// from the RICHEST scenario of each *_golden.txt and a mixed char_vars set, asserting the simultaneous
// parity + the routing/no-fabrication invariants. DDL/INSERT for the target
// tables are the per-importer schemas (re-exposed as AllCreateTables for a migration runner);
// still shape-asserted, not executed.
using System;
using System.Collections.Generic;
using XI.Persistence;

namespace XI.Quests
{
    // The neutral char_vars row ImportAll fans out to the typed per-importer CharVar DTOs.
    // A live migration reads these straight from `SELECT name, value FROM char_vars WHERE
    // charid = ?`; only name+value are needed to route/fold quest+mission soft state (assault's
    // flat globals and any unknown prefix are simply ignored by every importer).
    public sealed class CharVar
    {
        public string VarName;
        public int Value;
        public uint Expiry;   // char_vars.expiry, 0 = never
        public CharVar(string varName, int value, uint expiry = 0) { VarName = varName; Value = value; Expiry = expiry; }
    }

    // One legacy `chars` row's worth of state to migrate: the whole-struct blobs (taken
    // verbatim; a null/short blob zero-fills like extractFromBlob) + the char's flat char_vars.
    public sealed class LegacyCharRow
    {
        public uint CharId;
        public byte[] QuestsBlob;     // chars.quests    (704B, questlog_t[11])
        public byte[] MissionsBlob;   // chars.missions  (1050B, missionlog_t[15])
        public byte[] AssaultBlob;    // chars.assault   (130B, assaultlog_t)
        public byte[] CampaignBlob;   // chars.campaign  (514B, campaignlog_t)
        public byte[] EminenceBlob;   // chars.eminence  (700B, eminencelog_t, 2 pad bytes)
        public byte[] KeyitemsBlob;   // chars.keyitems  (1024B, keyitems_t[8] of {keyList,seenList})
        public IEnumerable<CharVar> CharVars;
    }

    // The normalized result sets ImportAll fills (one per target table). Row counts differ
    // by shape: quests/missions/eminence/keyitems variable, assault exactly 1 row.
    public sealed class CharacterStateImport
    {
        public List<CharQuestStateRow> Quests = new List<CharQuestStateRow>();
        public List<CharMissionStateRow> Missions = new List<CharMissionStateRow>();
        public CharAssaultStateRow Assault;
        public CharCampaignStateRow Campaign;
        public List<CharEminenceStateRow> Eminence = new List<CharEminenceStateRow>();
        public List<CharKeyItemStateRow> Keyitems = new List<CharKeyItemStateRow>();
    }

    public static class CharacterStateMigration
    {
        // The six target-table DDLs in dependency-free order (a migration runner issues these
        // once). Re-exposed from each per-container schema so the seam is the single source a
        // runner reaches for.
        public static readonly string[] AllCreateTables =
        {
            QuestStateSchema.CreateTableSql,
            MissionStateSchema.CreateTableSql,
            AssaultStateSchema.CreateTableSql,
            CampaignStateSchema.CreateTableSql,
            EminenceStateSchema.CreateTableSql,
            KeyItemStateSchema.CreateTableSql,
        };

        // Import one legacy char row into all the normalized tables at once. The SAME
        // (neutral) char_vars list is fanned out to the two prefix-sensitive importers; assault
        // and eminence take only their blob (no char_var layer -- see CharAssaultState /
        // CharEminenceState headers). registry is optional (synthetic keys otherwise).
        public static CharacterStateImport Import(LegacyCharRow legacy, QuestKeyRegistry registry)
        {
            var questVars = new List<QuestStateImporter.CharVar>();
            var missionVars = new List<MissionStateImporter.CharVar>();
            if (legacy.CharVars != null)
            {
                foreach (var cv in legacy.CharVars)
                {
                    questVars.Add(new QuestStateImporter.CharVar(cv.VarName, cv.Value, cv.Expiry));
                    missionVars.Add(new MissionStateImporter.CharVar(cv.VarName, cv.Value, cv.Expiry));
                }
            }

            return new CharacterStateImport
            {
                Quests = QuestStateImporter.Import(legacy.CharId, legacy.QuestsBlob, questVars, registry),
                Missions = MissionStateImporter.Import(legacy.CharId, legacy.MissionsBlob, missionVars),
                Assault = AssaultStateImporter.Import(legacy.CharId, legacy.AssaultBlob),
                Campaign = CampaignStateImporter.Import(legacy.CharId, legacy.CampaignBlob),
                Eminence = EminenceStateImporter.Import(legacy.CharId, legacy.EminenceBlob),
                Keyitems = KeyItemStateImporter.Import(legacy.CharId, legacy.KeyitemsBlob),
            };
        }

        // Project all normalized result sets back to their legacy blobs. Returns raw bytes for
        // direct diff vs the input columns: quests reproduce the CANONICAL projection (inherits the
        // (1,1) edge exactly like QuestWireCodec), missions/assault/eminence/keyitems reproduce their
        // input bytes raw (lossless; eminence except one id in two active slots, which keeps the last slot).
        public static LegacyCharRow Project(CharacterStateImport state)
        {
            return new LegacyCharRow
            {
                CharId = state.Quests.Count > 0 ? state.Quests[0].CharId
                         : state.Missions.Count > 0 ? state.Missions[0].CharId
                         : state.Assault != null ? state.Assault.CharId
                         : 0,
                QuestsBlob = QuestWireCodec.Project(QuestStateImporter.ToCells(state.Quests)).Bytes,
                MissionsBlob = MissionWireCodec.Project(MissionStateImporter.ToCells(state.Missions)).Bytes,
                AssaultBlob = AssaultWireCodec.Project(AssaultStateImporter.ToCell(state.Assault)).Bytes,
                CampaignBlob = CampaignWireCodec.Project(CampaignStateImporter.ToCell(state.Campaign)).Bytes,
                EminenceBlob = EminenceWireCodec.Project(EminenceStateImporter.ToCells(state.Eminence)).Bytes,
                KeyitemsBlob = KeyItemWireCodec.Project(KeyItemStateImporter.ToCells(state.Keyitems)).Bytes,
            };
        }

        // ---- BIND THROUGH: produce the exact BoundValue[] a live db::preparedStmt(InsertSql,…)
        //      receives, in that InsertSql's column order. The ported BoundValue has NO
        //      int64/uint64 case (faithful to db::BoundValue, where binding a 64-bit int is a C++
        //      compile error), so EVERY 64-bit column binds as String kind: quest repeat_by
        //      (Vana'diel reopen ticks, can exceed uint32), all updated_at epochs, and the mission
        //      completed_mask (a 64-bit value; bound as an UNSIGNED decimal -- a (long) cast would
        //      corrupt bit 63). The assault completed_mask is varbinary(16) so it binds as Blob.
        //      A live runner typically elides updated_at and lets the DDL CURRENT_TIMESTAMP default
        //      fill it; the seam still produces the column so arity == InsertSql's `?` count holds.
        //      Enums bind via their byte underlying type (area/status) -- QuestLog/QuestStatus/
        //      MissionLog are all : byte. ----

        static BoundValue Str64(long v) => BoundValue.Of(v.ToString(System.Globalization.CultureInfo.InvariantCulture));
        static BoundValue UStr64(ulong v) => BoundValue.Of(v.ToString(System.Globalization.CultureInfo.InvariantCulture));

        public static BoundValue[] BindQuest(CharQuestStateRow r) => new[]
        {
            BoundValue.Of(r.CharId),                 // charid      uint32
            BoundValue.Of(r.QuestKey),               // quest_key   string
            BoundValue.Of((byte)r.Area),             // area        tinyint (QuestLog : byte)
            BoundValue.Of(r.QuestId),                // quest_id    smallint (ushort)
            BoundValue.Of((byte)r.Status),           // status      tinyint (QuestStatus : byte)
            BoundValue.Of(r.Step),                   // step        int32
            BoundValue.Of(r.ProgressJson),           // progress    json -> string (null -> "")
            Str64(r.RepeatByVanaTime),               // repeat_by   BIGINT  -> String (no int64 bind)
            Str64(r.UpdatedAt),                       // updated_at  BIGINT  -> String
        };

        public static BoundValue[] BindMission(CharMissionStateRow r) => new[]
        {
            BoundValue.Of(r.CharId),                 // charid          uint32
            BoundValue.Of((byte)r.Area),             // area            tinyint (MissionLog : byte)
            BoundValue.Of(r.Current),                // current         smallint (ushort)
            BoundValue.Of(r.Status),                 // status          uint32
            UStr64(r.CompletedMask),                 // completed_mask  BIGINT UNSIGNED -> String
            BoundValue.Of(r.ProgressJson),           // progress        json -> string
            Str64(r.UpdatedAt),                       // updated_at      BIGINT -> String
        };

        public static BoundValue[] BindAssault(CharAssaultStateRow r) => new[]
        {
            BoundValue.Of(r.CharId),                 // charid          uint32
            BoundValue.Of(r.Current),                // current         smallint (ushort)
            BoundValue.Of(r.CompletedMask),          // completed_mask  varbinary(16) -> Blob
            Str64(r.UpdatedAt),                       // updated_at      BIGINT -> String
        };

        public static BoundValue[] BindEminence(CharEminenceStateRow r) => new[]
        {
            BoundValue.Of(r.CharId),                 // charid          uint32
            BoundValue.Of(r.RecordId),               // record_id       int unsigned (uint32)
            BoundValue.Of((byte)(r.Slot < 0 ? EminenceStateSchema.InactiveSlotColumn : r.Slot)), // slot tinyint (255 == inactive)
            BoundValue.Of(r.Progress),               // progress        int unsigned (uint32)
            BoundValue.Of(r.Completed),              // completed       tinyint(1) -> Bool
            Str64(r.UpdatedAt),                       // updated_at      BIGINT -> String
        };

        // keyitems row binds the two orthogonal bits as Bool (tinyint(1)); key_item_id is the
        // smallint(5) unsigned id (ushort). No 64-bit payload here, so only updated_at takes the
        // String route. Column order == KeyItemStateImporter.InsertSql.
        public static BoundValue[] BindKeyItem(CharKeyItemStateRow r) => new[]
        {
            BoundValue.Of(r.CharId),                 // charid        uint32
            BoundValue.Of(r.KeyItemId),              // key_item_id   smallint (ushort)
            BoundValue.Of(r.Possessed),              // possessed     tinyint(1) -> Bool
            BoundValue.Of(r.Seen),                   // seen          tinyint(1) -> Bool
            Str64(r.UpdatedAt),                       // updated_at    BIGINT -> String
        };

        // The per-column bind-width contract (the schema-fidelity vector the gate diffs against).
        public static readonly BoundValue.Kind[] QuestBindKinds =
        {
            BoundValue.Kind.UInt32, BoundValue.Kind.String, BoundValue.Kind.UInt8,
            BoundValue.Kind.UInt16, BoundValue.Kind.UInt8, BoundValue.Kind.Int32,
            BoundValue.Kind.String, BoundValue.Kind.String, BoundValue.Kind.String,
        };
        public static readonly BoundValue.Kind[] MissionBindKinds =
        {
            BoundValue.Kind.UInt32, BoundValue.Kind.UInt8, BoundValue.Kind.UInt16,
            BoundValue.Kind.UInt32, BoundValue.Kind.String, BoundValue.Kind.String,
            BoundValue.Kind.String,
        };
        public static readonly BoundValue.Kind[] AssaultBindKinds =
        {
            BoundValue.Kind.UInt32, BoundValue.Kind.UInt16, BoundValue.Kind.Blob,
            BoundValue.Kind.String,
        };
        public static readonly BoundValue.Kind[] EminenceBindKinds =
        {
            BoundValue.Kind.UInt32, BoundValue.Kind.UInt32, BoundValue.Kind.UInt8,
            BoundValue.Kind.UInt32, BoundValue.Kind.Bool, BoundValue.Kind.String,
        };
        public static readonly BoundValue.Kind[] KeyItemBindKinds =
        {
            BoundValue.Kind.UInt32, BoundValue.Kind.UInt16, BoundValue.Kind.Bool,
            BoundValue.Kind.Bool, BoundValue.Kind.String,
        };
    }
}
