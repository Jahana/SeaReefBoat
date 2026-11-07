// XI.Quests -- reads and writes the char_quest_state / char_mission_state / char_assault_state / char_campaign_state /
// char_eminence_state rows (QuestStateSchema, MissionStateSchema and the C1 schemas) for the live server (quest rework
// piece A). No transactions here: the zone wraps a change and its legacy write in one.
// ReplaceAll / Rekey batch their inserts (at most BatchRows rows a statement).
// The progress JSON binds through NULLIF so a row with no soft vars stores NULL (BoundValue writes a null string as "",
// which is not valid JSON); the BIGINT columns bind as text (BoundValue has no 64-bit kind).
using System.Collections.Generic;
using System.Globalization;
using XI.Persistence;

namespace XI.Quests
{
    public static class QuestStateStore
    {
        public const string SqlLoadQuests =
            "SELECT quest_key, area, quest_id, status, step, progress, repeat_by FROM char_quest_state WHERE charid = ? ORDER BY area, quest_id";
        public const string SqlLoadMissions =
            "SELECT area, current, status, completed_mask, progress FROM char_mission_state WHERE charid = ? ORDER BY area";
        public const string SqlUpsertQuest =
            "INSERT INTO char_quest_state (charid, quest_key, area, quest_id, status, step, progress, repeat_by) VALUES (?, ?, ?, ?, ?, ?, NULLIF(?, ''), ?) " +
            "ON DUPLICATE KEY UPDATE status = VALUES(status), step = VALUES(step), progress = VALUES(progress), repeat_by = VALUES(repeat_by)";
        public const string SqlDeleteQuest = "DELETE FROM char_quest_state WHERE charid = ? AND area = ? AND quest_id = ?";
        public const string SqlUpsertMission =
            "INSERT INTO char_mission_state (charid, area, current, status, completed_mask, progress) VALUES (?, ?, ?, ?, ?, NULLIF(?, '')) " +
            "ON DUPLICATE KEY UPDATE current = VALUES(current), status = VALUES(status), completed_mask = VALUES(completed_mask), progress = VALUES(progress)";
        public const string SqlDeleteQuests = "DELETE FROM char_quest_state WHERE charid = ?";
        public const string SqlDeleteMissions = "DELETE FROM char_mission_state WHERE charid = ?";
        public const string SqlLoadAssault = "SELECT current, completed_mask FROM char_assault_state WHERE charid = ?";
        public const string SqlLoadCampaign = "SELECT current, completed_mask FROM char_campaign_state WHERE charid = ?";
        public const string SqlLoadEminence = "SELECT record_id, slot, progress, completed FROM char_eminence_state WHERE charid = ? ORDER BY record_id";
        public const string SqlUpsertAssault =
            "INSERT INTO char_assault_state (charid, current, completed_mask) VALUES (?, ?, ?) " +
            "ON DUPLICATE KEY UPDATE current = VALUES(current), completed_mask = VALUES(completed_mask)";
        public const string SqlUpsertCampaign =
            "INSERT INTO char_campaign_state (charid, current, completed_mask) VALUES (?, ?, ?) " +
            "ON DUPLICATE KEY UPDATE current = VALUES(current), completed_mask = VALUES(completed_mask)";
        public const string SqlUpsertEminence =
            "INSERT INTO char_eminence_state (charid, record_id, slot, progress, completed) VALUES (?, ?, ?, ?, ?) " +
            "ON DUPLICATE KEY UPDATE slot = VALUES(slot), progress = VALUES(progress), completed = VALUES(completed)";
        public const string SqlDeleteEminence = "DELETE FROM char_eminence_state WHERE charid = ? AND record_id = ?";
        public const string SqlDeleteAssaults = "DELETE FROM char_assault_state WHERE charid = ?";
        public const string SqlDeleteCampaigns = "DELETE FROM char_campaign_state WHERE charid = ?";
        public const string SqlDeleteEminences = "DELETE FROM char_eminence_state WHERE charid = ?";

        // the batched inserts: at most this many rows a statement
        public const int BatchRows = 200;
        private const string SqlInsertQuestsHead = "INSERT INTO char_quest_state (charid, quest_key, area, quest_id, status, step, progress, repeat_by) VALUES ";
        private const string SqlInsertQuestRow = "(?, ?, ?, ?, ?, ?, NULLIF(?, ''), ?)";
        private const string SqlInsertMissionsHead = "INSERT INTO char_mission_state (charid, area, current, status, completed_mask, progress) VALUES ";
        private const string SqlInsertMissionRow = "(?, ?, ?, ?, ?, NULLIF(?, ''))";
        private const string SqlInsertAssaultRow = "INSERT INTO char_assault_state (charid, current, completed_mask) VALUES (?, ?, ?)";
        private const string SqlInsertCampaignRow = "INSERT INTO char_campaign_state (charid, current, completed_mask) VALUES (?, ?, ?)";
        private const string SqlInsertEminenceHead = "INSERT INTO char_eminence_state (charid, record_id, slot, progress, completed) VALUES ";
        private const string SqlInsertEminenceRow = "(?, ?, ?, ?, ?)";

        // the char's quest rows; null when the read failed
        public static List<CharQuestStateRow> LoadQuests(IDatabase db, uint charId)
        {
            var r = db.Execute(SqlLoadQuests, new[] { BoundValue.Of(charId) });
            if (r == null) return null;
            var rows = new List<CharQuestStateRow>();
            while (r.Next())
            {
                var row = new CharQuestStateRow
                {
                    CharId = charId,
                    QuestKey = r.Get<string>("quest_key"),
                    Area = (QuestLog)r.Get<byte>("area"),
                    QuestId = r.Get<ushort>("quest_id"),
                    Status = (QuestStatus)r.Get<byte>("status"),
                    Step = r.Get<int>("step"),
                    RepeatByVanaTime = r.Get<long>("repeat_by"),
                };
                ProgressJsonCodec.Read(r.IsNull("progress") ? null : r.Get<string>("progress"), row.SoftVars, row.Expiry);
                rows.Add(row);
            }
            return rows;
        }

        // the char's mission rows, by area; null when the read failed
        public static List<CharMissionStateRow> LoadMissions(IDatabase db, uint charId)
        {
            var r = db.Execute(SqlLoadMissions, new[] { BoundValue.Of(charId) });
            if (r == null) return null;
            var rows = new List<CharMissionStateRow>();
            while (r.Next())
            {
                var row = new CharMissionStateRow
                {
                    CharId = charId,
                    Area = (MissionLog)r.Get<byte>("area"),
                    Current = r.Get<ushort>("current"),
                    Status = r.Get<uint>("status"),
                    CompletedMask = r.Get<ulong>("completed_mask"),
                };
                ProgressJsonCodec.Read(r.IsNull("progress") ? null : r.Get<string>("progress"), row.SoftVars, row.Expiry);
                rows.Add(row);
            }
            return rows;
        }

        // the char's assault row; false when the read failed, a null row when it has none
        public static bool TryLoadAssault(IDatabase db, uint charId, out CharAssaultStateRow row)
        {
            row = null;
            var r = db.Execute(SqlLoadAssault, new[] { BoundValue.Of(charId) });
            if (r == null) return false;
            if (r.Next()) row = new CharAssaultStateRow { CharId = charId, Current = r.Get<ushort>("current"), CompletedMask = r.GetBlobBytes("completed_mask") };
            return true;
        }

        // the char's campaign row, as TryLoadAssault
        public static bool TryLoadCampaign(IDatabase db, uint charId, out CharCampaignStateRow row)
        {
            row = null;
            var r = db.Execute(SqlLoadCampaign, new[] { BoundValue.Of(charId) });
            if (r == null) return false;
            if (r.Next()) row = new CharCampaignStateRow { CharId = charId, Current = r.Get<ushort>("current"), CompletedMask = r.GetBlobBytes("completed_mask") };
            return true;
        }

        // the char's eminence rows, by record id; null when the read failed
        public static List<CharEminenceStateRow> LoadEminence(IDatabase db, uint charId)
        {
            var r = db.Execute(SqlLoadEminence, new[] { BoundValue.Of(charId) });
            if (r == null) return null;
            var rows = new List<CharEminenceStateRow>();
            while (r.Next())
            {
                byte slot = r.Get<byte>("slot");
                rows.Add(new CharEminenceStateRow
                {
                    CharId = charId,
                    RecordId = r.Get<uint>("record_id"),
                    Slot = slot == EminenceStateSchema.InactiveSlotColumn ? -1 : slot,
                    Progress = r.Get<uint>("progress"),
                    Completed = r.Get<bool>("completed"),
                });
            }
            return rows;
        }

        private static BoundValue[] Bind(CharQuestStateRow r) => new[]
        {
            BoundValue.Of(r.CharId), BoundValue.Of(r.QuestKey), BoundValue.Of((byte)r.Area), BoundValue.Of(r.QuestId),
            BoundValue.Of((byte)r.Status), BoundValue.Of(r.Step), BoundValue.Of(r.ProgressJson), Text(r.RepeatByVanaTime),
        };

        private static BoundValue[] Bind(CharMissionStateRow r) => new[]
        {
            BoundValue.Of(r.CharId), BoundValue.Of((byte)r.Area), BoundValue.Of(r.Current), BoundValue.Of(r.Status),
            BoundValue.Of(r.CompletedMask.ToString(CultureInfo.InvariantCulture)), BoundValue.Of(r.ProgressJson),
        };

        private static BoundValue[] Bind(CharAssaultStateRow r) => new[] { BoundValue.Of(r.CharId), BoundValue.Of(r.Current), BoundValue.Of(r.CompletedMask) };

        private static BoundValue[] Bind(CharCampaignStateRow r) => new[] { BoundValue.Of(r.CharId), BoundValue.Of(r.Current), BoundValue.Of(r.CompletedMask) };

        private static BoundValue[] Bind(CharEminenceStateRow r) => new[]
        {
            BoundValue.Of(r.CharId), BoundValue.Of(r.RecordId), BoundValue.Of((byte)(r.Slot < 0 ? EminenceStateSchema.InactiveSlotColumn : r.Slot)),
            BoundValue.Of(r.Progress), BoundValue.Of(r.Completed),
        };

        public static bool Upsert(IDatabase db, CharQuestStateRow r) => db.Execute(SqlUpsertQuest, Bind(r)) != null;

        public static bool Delete(IDatabase db, CharQuestStateRow r) =>
            db.Execute(SqlDeleteQuest, new[] { BoundValue.Of(r.CharId), BoundValue.Of((byte)r.Area), BoundValue.Of(r.QuestId) }) != null;

        public static bool Upsert(IDatabase db, CharMissionStateRow r) => db.Execute(SqlUpsertMission, Bind(r)) != null;

        public static bool Upsert(IDatabase db, CharAssaultStateRow r) => db.Execute(SqlUpsertAssault, Bind(r)) != null;

        public static bool Upsert(IDatabase db, CharCampaignStateRow r) => db.Execute(SqlUpsertCampaign, Bind(r)) != null;

        public static bool Upsert(IDatabase db, CharEminenceStateRow r) => db.Execute(SqlUpsertEminence, Bind(r)) != null;

        public static bool Delete(IDatabase db, CharEminenceStateRow r) =>
            db.Execute(SqlDeleteEminence, new[] { BoundValue.Of(r.CharId), BoundValue.Of(r.RecordId) }) != null;

        // one change's rows; false at the first statement that failed
        public static bool Apply(IDatabase db, ProgressChanges ch)
        {
            foreach (var r in ch.QuestDeletes) if (!Delete(db, r)) return false;
            foreach (var r in ch.EminenceDeletes) if (!Delete(db, r)) return false;
            foreach (var r in ch.QuestUpserts) if (!Upsert(db, r)) return false;
            foreach (var r in ch.MissionUpserts) if (!Upsert(db, r)) return false;
            if (ch.AssaultUpsert != null && !Upsert(db, ch.AssaultUpsert)) return false;
            if (ch.CampaignUpsert != null && !Upsert(db, ch.CampaignUpsert)) return false;
            foreach (var r in ch.EminenceUpserts) if (!Upsert(db, r)) return false;
            return true;
        }

        // rows in multi-row INSERTs of at most BatchRows each (no ON DUPLICATE: the callers deleted them first)
        public static bool InsertQuests(IDatabase db, IReadOnlyList<CharQuestStateRow> rows) =>
            InsertBatched(db, SqlInsertQuestsHead, SqlInsertQuestRow, rows, Bind);

        public static bool InsertMissions(IDatabase db, IReadOnlyList<CharMissionStateRow> rows) =>
            InsertBatched(db, SqlInsertMissionsHead, SqlInsertMissionRow, rows, Bind);

        private static bool InsertBatched<T>(IDatabase db, string head, string row, IReadOnlyList<T> rows, System.Func<T, BoundValue[]> bind)
        {
            for (int start = 0; start < rows.Count; start += BatchRows)
            {
                int n = System.Math.Min(BatchRows, rows.Count - start);
                var sql = new System.Text.StringBuilder(head);
                var args = new List<BoundValue>();
                for (int i = 0; i < n; i++)
                {
                    if (i > 0) sql.Append(", ");
                    sql.Append(row);
                    args.AddRange(bind(rows[start + i]));
                }
                if (db.Execute(sql.ToString(), args) == null) return false;
            }
            return true;
        }

        // re-keyed rows (all one char's): deleted by their wire coordinates, BatchRows to a statement, then all inserted with
        // their new keys (an upsert would keep the stored key, and deleting them all first cannot collide on
        // (charid, quest_key) when two names swap)
        public static bool Rekey(IDatabase db, IReadOnlyList<CharQuestStateRow> rows)
        {
            for (int start = 0; start < rows.Count; start += BatchRows)
            {
                int n = System.Math.Min(BatchRows, rows.Count - start);
                var sql = new System.Text.StringBuilder("DELETE FROM char_quest_state WHERE charid = ? AND (area, quest_id) IN (");
                var args = new List<BoundValue> { BoundValue.Of(rows[0].CharId) };
                for (int i = 0; i < n; i++)
                {
                    if (i > 0) sql.Append(", ");
                    sql.Append("(?, ?)");
                    args.Add(BoundValue.Of((byte)rows[start + i].Area));
                    args.Add(BoundValue.Of(rows[start + i].QuestId));
                }
                sql.Append(')');
                if (db.Execute(sql.ToString(), args) == null) return false;
            }
            return InsertQuests(db, rows);
        }

        // the char's rows replaced by the state's: its quest rows with something in them, its 15 mission rows, its assault and
        // campaign rows and its eminence rows, batched
        public static bool ReplaceAll(IDatabase db, CharProgress p)
        {
            foreach (var sql in new[] { SqlDeleteQuests, SqlDeleteMissions, SqlDeleteAssaults, SqlDeleteCampaigns, SqlDeleteEminences })
                if (db.Execute(sql, new[] { BoundValue.Of(p.CharId) }) == null) return false;
            var quests = new List<CharQuestStateRow>();
            foreach (var r in p.Quests.Values)
                if (!CharProgress.IsEmpty(r)) quests.Add(r);
            return InsertQuests(db, quests) && InsertMissions(db, p.Missions) &&
                   db.Execute(SqlInsertAssaultRow, Bind(p.Assault)) != null &&
                   db.Execute(SqlInsertCampaignRow, Bind(p.Campaign)) != null &&
                   InsertBatched(db, SqlInsertEminenceHead, SqlInsertEminenceRow, new List<CharEminenceStateRow>(p.Eminence.Values), Bind);
        }

        static BoundValue Text(long v) => BoundValue.Of(v.ToString(CultureInfo.InvariantCulture));
    }
}
