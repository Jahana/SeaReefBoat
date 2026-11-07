// XI.Quests -- one character's live quest and mission rows, and (quest rework C1) its assault, campaign and eminence rows
// (quest rework piece A, docs/superpowers/specs/2026-10-20-quest-storage-design.md). The zone keeps the legacy byte arrays
// as its working copy; this holds the normalized rows next to them: built from the legacy state (Import, through the verified importers) or
// from the stored rows (FromRows), synced from the arrays after each write (SyncQuests / SyncMissions), and given the
// Quest[..] / Mission[..] char vars as they are persisted (SetVar / ClearVarsWithPrefix). Every mutator returns the rows
// it changed, for the caller to write. Pure: no database.
using System;
using System.Collections.Generic;

namespace XI.Quests
{
    // the rows one change touched: written, or (a quest row left with nothing in it) deleted
    public sealed class ProgressChanges
    {
        public readonly List<CharQuestStateRow> QuestUpserts = new List<CharQuestStateRow>();
        public readonly List<CharQuestStateRow> QuestDeletes = new List<CharQuestStateRow>();
        public readonly List<CharMissionStateRow> MissionUpserts = new List<CharMissionStateRow>();
        public CharAssaultStateRow AssaultUpsert;
        public CharCampaignStateRow CampaignUpsert;
        public readonly List<CharEminenceStateRow> EminenceUpserts = new List<CharEminenceStateRow>();
        public readonly List<CharEminenceStateRow> EminenceDeletes = new List<CharEminenceStateRow>();
        public bool IsEmpty => QuestUpserts.Count == 0 && QuestDeletes.Count == 0 && MissionUpserts.Count == 0 && AssaultUpsert == null &&
                               CampaignUpsert == null && EminenceUpserts.Count == 0 && EminenceDeletes.Count == 0;
    }

    public sealed class CharProgress
    {
        private const string Prog = "Prog";

        public uint CharId { get; }
        private readonly Dictionary<QuestWireRef, CharQuestStateRow> quests_ = new Dictionary<QuestWireRef, CharQuestStateRow>();
        private readonly CharMissionStateRow[] missions_ = new CharMissionStateRow[MissionBitBlob.Areas];

        private CharAssaultStateRow assault_;
        private CharCampaignStateRow campaign_;
        private readonly SortedDictionary<uint, CharEminenceStateRow> eminence_ = new SortedDictionary<uint, CharEminenceStateRow>();

        public CharAssaultStateRow Assault => assault_;
        public CharCampaignStateRow Campaign => campaign_;
        public IReadOnlyDictionary<uint, CharEminenceStateRow> Eminence => eminence_;

        public IReadOnlyDictionary<QuestWireRef, CharQuestStateRow> Quests => quests_;
        public IReadOnlyList<CharMissionStateRow> Missions => missions_;

        // the stable keys of the rows this state creates or re-keys; null: every key synthetic (legacy:<area>/<id>)
        private readonly QuestKeyRegistry registry_;

        private CharProgress(uint charId, QuestKeyRegistry registry) { CharId = charId; registry_ = registry; }

        // the rows the legacy state imports to: the arrays as the char loaded them and its char_vars, keyed through the registry
        public static CharProgress Import(uint charId, byte[] questLog, byte[] missionLog, IEnumerable<CharVar> vars, QuestKeyRegistry registry = null,
                                          byte[] assaultLog = null, byte[] campaignLog = null, byte[] eminenceLog = null)
        {
            var questVars = new List<QuestStateImporter.CharVar>();
            var missionVars = new List<MissionStateImporter.CharVar>();
            if (vars != null)
                foreach (var v in vars)
                {
                    questVars.Add(new QuestStateImporter.CharVar(v.VarName, v.Value, v.Expiry));
                    missionVars.Add(new MissionStateImporter.CharVar(v.VarName, v.Value, v.Expiry));
                }
            return FromRows(charId, QuestStateImporter.Import(charId, questLog, questVars, registry),
                            MissionStateImporter.Import(charId, missionLog, missionVars), registry,
                            AssaultStateImporter.Import(charId, assaultLog), CampaignStateImporter.Import(charId, campaignLog),
                            EminenceStateImporter.Import(charId, eminenceLog));
        }

        // the stored rows; a mission area with no row takes the new character's default (MissionBitBlob's init), and no
        // assault or campaign row reads as the empty state (ProgressRows names that as drift)
        public static CharProgress FromRows(uint charId, IEnumerable<CharQuestStateRow> quests, IEnumerable<CharMissionStateRow> missions, QuestKeyRegistry registry = null,
                                            CharAssaultStateRow assault = null, CharCampaignStateRow campaign = null, IEnumerable<CharEminenceStateRow> eminence = null)
        {
            var p = new CharProgress(charId, registry);
            foreach (var q in quests) p.quests_[q.Wire] = q;
            foreach (var m in missions)
                if (m.AreaIndex >= 0 && m.AreaIndex < MissionBitBlob.Areas) p.missions_[m.AreaIndex] = m;
            var fresh = MissionWireCodec.Extract(new MissionBitBlob());
            for (int a = 0; a < MissionBitBlob.Areas; a++)
                if (p.missions_[a] == null)
                    p.missions_[a] = new CharMissionStateRow
                    {
                        CharId = charId, Area = (MissionLog)a, Current = fresh[a].Current, Status = fresh[a].Status, CompletedMask = fresh[a].CompletedMask,
                    };
            p.assault_ = assault ?? AssaultStateImporter.Import(charId, null);
            p.campaign_ = campaign ?? CampaignStateImporter.Import(charId, null);
            if (eminence != null)
                foreach (var e in eminence) p.eminence_[e.RecordId] = e;
            return p;
        }

        // a quest row with nothing in it: no such row is stored
        public static bool IsEmpty(CharQuestStateRow r) =>
            r.Status == QuestStatus.Available && r.Step == 0 && r.SoftVars.Count == 0 && r.Expiry.Count == 0;

        // a char var whose row copy lives here: Quest[a][q]<suffix> / Mission[a][m]<suffix>, in range, suffix not empty
        public static bool IsProgressVar(string name) =>
            QuestStateImporter.TryParseQuestVar(name, out _, out var questSuffix)
                ? questSuffix.Length > 0
                : MissionStateImporter.TryParseMissionVar(name, out _, out _, out var missionSuffix) && missionSuffix.Length > 0;

        // after the quest log changed: every quest whose status differs from its row
        public ProgressChanges SyncQuests(byte[] questLog)
        {
            var ch = new ProgressChanges();
            var blob = new QuestBitBlob();
            blob.LoadFromBlob(questLog);
            for (int area = 0; area < QuestBitBlob.Areas; area++)
                for (int id = 0; id < QuestBitBlob.MaxQuestId; id++)
                {
                    var status = blob.GetStatus(area, id);
                    var wire = new QuestWireRef((QuestLog)area, (ushort)id);
                    quests_.TryGetValue(wire, out var row);
                    if ((row?.Status ?? QuestStatus.Available) == status) continue;
                    row ??= NewQuestRow(wire);
                    row.Status = status;
                    Touch(row, ch);
                }
            return ch;
        }

        // after the mission log changed: every area whose current / status / completions differ from its row
        public ProgressChanges SyncMissions(byte[] missionLog)
        {
            var ch = new ProgressChanges();
            var blob = new MissionBitBlob();
            blob.LoadFromBlob(missionLog);
            foreach (var cell in MissionWireCodec.Extract(blob))
            {
                var row = missions_[(int)cell.Area];
                if (row.Current == cell.Current && row.Status == cell.Status && row.CompletedMask == cell.CompletedMask) continue;
                row.Current = cell.Current;
                row.Status = cell.Status;
                row.CompletedMask = cell.CompletedMask;
                ch.MissionUpserts.Add(row);
            }
            return ch;
        }

        // after the assault log changed: its one row, when its current id or completions differ
        public ProgressChanges SyncAssault(byte[] assaultLog)
        {
            var ch = new ProgressChanges();
            var now = AssaultStateImporter.Import(CharId, assaultLog);
            if (assault_.Current == now.Current && assault_.CompletedMask.AsSpan().SequenceEqual(now.CompletedMask)) return ch;
            assault_.Current = now.Current;
            assault_.CompletedMask = now.CompletedMask;
            ch.AssaultUpsert = assault_;
            return ch;
        }

        // after the campaign log changed: its one row, as SyncAssault
        public ProgressChanges SyncCampaign(byte[] campaignLog)
        {
            var ch = new ProgressChanges();
            var now = CampaignStateImporter.Import(CharId, campaignLog);
            if (campaign_.Current == now.Current && campaign_.CompletedMask.AsSpan().SequenceEqual(now.CompletedMask)) return ch;
            campaign_.Current = now.Current;
            campaign_.CompletedMask = now.CompletedMask;
            ch.CampaignUpsert = campaign_;
            return ch;
        }

        // charutils::SaveMissionsList writes all three blobs in one statement
        public ProgressChanges SyncMissionsList(byte[] missionLog, byte[] assaultLog, byte[] campaignLog)
        {
            var ch = SyncMissions(missionLog);
            ch.AssaultUpsert = SyncAssault(assaultLog).AssaultUpsert;
            ch.CampaignUpsert = SyncCampaign(campaignLog).CampaignUpsert;
            return ch;
        }

        // after the eminence log changed: every record whose slot, progress or completion differs from its row, and the rows
        // of records now neither active nor completed. Removing a record moves the records below it up a slot
        public ProgressChanges SyncEminence(byte[] eminenceLog)
        {
            var ch = new ProgressChanges();
            var seen = new HashSet<uint>();
            foreach (var now in EminenceStateImporter.Import(CharId, eminenceLog))
            {
                seen.Add(now.RecordId);
                if (!eminence_.TryGetValue(now.RecordId, out var row))
                {
                    eminence_[now.RecordId] = now;
                    ch.EminenceUpserts.Add(now);
                    continue;
                }
                if (row.Slot == now.Slot && row.Progress == now.Progress && row.Completed == now.Completed) continue;
                row.Slot = now.Slot;
                row.Progress = now.Progress;
                row.Completed = now.Completed;
                ch.EminenceUpserts.Add(row);
            }
            foreach (var row in new List<CharEminenceStateRow>(eminence_.Values))
                if (!seen.Contains(row.RecordId))
                {
                    eminence_.Remove(row.RecordId);
                    ch.EminenceDeletes.Add(row);
                }
            return ch;
        }

        // a Quest[a][q]<suffix> / Mission[a][m]<suffix> var persisted as (value, expiry): Prog is the quest's step, any
        // other suffix a soft var; 0 removes it, as PersistCharVar deletes the char_vars row. Other names change nothing
        public ProgressChanges SetVar(string name, int value, uint expiry)
        {
            var ch = new ProgressChanges();
            if (value == 0) expiry = 0;
            if (QuestStateImporter.TryParseQuestVar(name, out var wire, out var suffix))
            {
                if (suffix.Length == 0) return ch;
                quests_.TryGetValue(wire, out var row);
                if (row == null && value == 0) return ch;
                row ??= NewQuestRow(wire);
                if (suffix == Prog) row.Step = value;
                else Put(row.SoftVars, suffix, value);
                PutExpiry(row.Expiry, suffix, expiry);
                Touch(row, ch);
            }
            else if (MissionStateImporter.TryParseMissionVar(name, out int area, out int missionId, out suffix))
            {
                if (suffix.Length == 0) return ch;
                var row = missions_[area];
                string key = missionId + ":" + suffix;
                Put(row.SoftVars, key, value);
                PutExpiry(row.Expiry, key, expiry);
                ch.MissionUpserts.Add(row);
            }
            return ch;
        }

        // clearVarsWithPrefix: every Quest / Mission var whose name starts with the prefix (ordinal, as the cache is)
        public ProgressChanges ClearVarsWithPrefix(string prefix)
        {
            var ch = new ProgressChanges();
            foreach (var row in new List<CharQuestStateRow>(quests_.Values))
            {
                string head = QuestVarHead(row);
                bool changed = false;
                if (row.Step != 0 && (head + Prog).StartsWith(prefix, StringComparison.Ordinal))
                {
                    row.Step = 0;
                    row.Expiry.Remove(Prog);
                    changed = true;
                }
                foreach (var key in new List<string>(row.SoftVars.Keys))
                    if ((head + key).StartsWith(prefix, StringComparison.Ordinal))
                    {
                        row.SoftVars.Remove(key);
                        row.Expiry.Remove(key);
                        changed = true;
                    }
                if (changed) Touch(row, ch);
            }
            foreach (var row in missions_)
            {
                bool changed = false;
                foreach (var key in new List<string>(row.SoftVars.Keys))
                    if (MissionVarName(row, key)?.StartsWith(prefix, StringComparison.Ordinal) == true)
                    {
                        row.SoftVars.Remove(key);
                        row.Expiry.Remove(key);
                        changed = true;
                    }
                if (changed) ch.MissionUpserts.Add(row);
            }
            return ch;
        }

        // the vars expired by now (expiry <= now: an expired var reads 0, and the time server deletes its char_vars row)
        public ProgressChanges PruneExpired(long unixNow)
        {
            var ch = new ProgressChanges();
            foreach (var row in new List<CharQuestStateRow>(quests_.Values))
            {
                bool changed = false;
                foreach (var kv in new List<KeyValuePair<string, uint>>(row.Expiry))
                {
                    if (kv.Value > unixNow) continue;
                    if (kv.Key == Prog) row.Step = 0;
                    else row.SoftVars.Remove(kv.Key);
                    row.Expiry.Remove(kv.Key);
                    changed = true;
                }
                if (changed) Touch(row, ch);
            }
            foreach (var row in missions_)
            {
                bool changed = false;
                foreach (var kv in new List<KeyValuePair<string, uint>>(row.Expiry))
                {
                    if (kv.Value > unixNow) continue;
                    row.SoftVars.Remove(kv.Key);
                    row.Expiry.Remove(kv.Key);
                    changed = true;
                }
                if (changed) ch.MissionUpserts.Add(row);
            }
            return ch;
        }

        // the Quest / Mission char vars the rows hold, by name: (value, expiry); a var of value 0 is no var
        public SortedDictionary<string, (int Value, uint Expiry)> Vars()
        {
            var vars = new SortedDictionary<string, (int Value, uint Expiry)>(StringComparer.Ordinal);
            foreach (var row in quests_.Values)
            {
                string head = QuestVarHead(row);
                if (row.Step != 0) vars[head + Prog] = (row.Step, ExpiryOf(row.Expiry, Prog));
                foreach (var kv in row.SoftVars)
                    if (kv.Value != 0) vars[head + kv.Key] = ((int)kv.Value, ExpiryOf(row.Expiry, kv.Key));
            }
            foreach (var row in missions_)
                foreach (var kv in row.SoftVars)
                    if (kv.Value != 0 && MissionVarName(row, kv.Key) is string name) vars[name] = ((int)kv.Value, ExpiryOf(row.Expiry, kv.Key));
            return vars;
        }

        // which parts differ from another state, compared in canonical form: "quests" (the projected quest log),
        // "missions" (the projected mission log), "vars" (Vars()), "assault", "campaign", "eminence" (their projected logs);
        // comma-joined in that order, "" when none. missingAssault / missingCampaign: this state has no such row, which
        // differs whatever the blob holds (ProgressRows, on the first load after migration 075)
        public string Diff(CharProgress other, bool missingAssault = false, bool missingCampaign = false)
        {
            var parts = new List<string>();
            if (ProjectQuests().ToHex() != other.ProjectQuests().ToHex()) parts.Add("quests");
            if (ProjectMissions().ToHex() != other.ProjectMissions().ToHex()) parts.Add("missions");
            if (VarsKey() != other.VarsKey()) parts.Add("vars");
            if (missingAssault || ProjectAssault().ToHex() != other.ProjectAssault().ToHex()) parts.Add("assault");
            if (missingCampaign || ProjectCampaign().ToHex() != other.ProjectCampaign().ToHex()) parts.Add("campaign");
            if (ProjectEminence().ToHex() != other.ProjectEminence().ToHex()) parts.Add("eminence");
            return string.Join(",", parts);
        }

        public QuestBitBlob ProjectQuests() => QuestWireCodec.Project(QuestStateImporter.ToCells(quests_.Values));
        public MissionBitBlob ProjectMissions() => MissionWireCodec.Project(MissionStateImporter.ToCells(missions_));
        public AssaultBitBlob ProjectAssault() => AssaultWireCodec.Project(AssaultStateImporter.ToCell(assault_));
        public CampaignBitBlob ProjectCampaign() => CampaignWireCodec.Project(CampaignStateImporter.ToCell(campaign_));
        public EminenceBitBlob ProjectEminence() => EminenceWireCodec.Project(EminenceStateImporter.ToCells(eminence_.Values));

        // the zone's arrays from the rows (CharLoader, after the rows are chosen)
        public void ProjectInto(byte[] questLog, byte[] missionLog, byte[] assaultLog = null, byte[] campaignLog = null)
        {
            Buffer.BlockCopy(ProjectQuests().Bytes, 0, questLog, 0, Math.Min(questLog.Length, QuestBitBlob.Size));
            Buffer.BlockCopy(ProjectMissions().Bytes, 0, missionLog, 0, Math.Min(missionLog.Length, MissionBitBlob.Size));
            if (assaultLog != null) Buffer.BlockCopy(ProjectAssault().Bytes, 0, assaultLog, 0, Math.Min(assaultLog.Length, AssaultBitBlob.Size));
            if (campaignLog != null) Buffer.BlockCopy(ProjectCampaign().Bytes, 0, campaignLog, 0, Math.Min(campaignLog.Length, CampaignBitBlob.Size));
        }

        private string VarsKey()
        {
            var parts = new List<string>();
            foreach (var kv in Vars()) parts.Add(kv.Key + "=" + kv.Value.Value + "@" + kv.Value.Expiry);
            return string.Join(",", parts);
        }

        private string KeyOf(QuestWireRef wire) => registry_ != null ? registry_.KeyOrSynthetic(wire) : QuestKeyRegistry.Synthetic(wire);

        private CharQuestStateRow NewQuestRow(QuestWireRef wire)
        {
            var row = new CharQuestStateRow { CharId = CharId, QuestKey = KeyOf(wire), Area = wire.Area, QuestId = wire.QuestId };
            quests_[wire] = row;
            return row;
        }

        // the quest rows whose key is not the registry's (a synthetic key the registry now names, a renamed or removed
        // constant), given that key; in (area, id) order, for the caller to write
        public List<CharQuestStateRow> Rekey()
        {
            var changed = new List<CharQuestStateRow>();
            foreach (var row in quests_.Values)
            {
                string key = KeyOf(row.Wire);
                if (row.QuestKey == key) continue;
                row.QuestKey = key;
                changed.Add(row);
            }
            changed.Sort((a, b) => a.Area != b.Area ? ((int)a.Area).CompareTo((int)b.Area) : a.QuestId.CompareTo(b.QuestId));
            return changed;
        }

        // a quest row left with nothing in it goes; otherwise it is written
        private void Touch(CharQuestStateRow row, ProgressChanges ch)
        {
            if (IsEmpty(row))
            {
                quests_.Remove(row.Wire);
                ch.QuestDeletes.Add(row);
            }
            else ch.QuestUpserts.Add(row);
        }

        private static void Put(Dictionary<string, long> soft, string key, int value)
        {
            if (value == 0) soft.Remove(key);
            else soft[key] = value;
        }

        private static void PutExpiry(Dictionary<string, uint> expiry, string key, uint value)
        {
            if (value == 0) expiry.Remove(key);
            else expiry[key] = value;
        }

        private static uint ExpiryOf(Dictionary<string, uint> expiry, string key) => expiry.TryGetValue(key, out var e) ? e : 0;

        private static string QuestVarHead(CharQuestStateRow r) => $"Quest[{(int)r.Area}][{r.QuestId}]";

        // null for a key without ':' (the importers never write one): Vars / ClearVarsWithPrefix skip it, so Diff sees the drift
        private static string MissionVarName(CharMissionStateRow r, string key)
        {
            int colon = key.IndexOf(':');
            if (colon < 0) return null;
            return $"Mission[{r.AreaIndex}][{key.Substring(0, colon)}]{key.Substring(colon + 1)}";
        }
    }
}
