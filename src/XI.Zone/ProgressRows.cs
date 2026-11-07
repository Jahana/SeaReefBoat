using System;
using System.Collections.Generic;
using XI.Persistence;
using XI.Quests;

namespace XI.Zone
{
    // Quest rework piece A (docs/superpowers/specs/2026-10-20-quest-storage-design.md) and C1: a char's quests, missions, assault,
    // campaign and eminence in the char_quest_state / char_mission_state / char_assault_state / char_campaign_state /
    // char_eminence_state rows next to the legacy chars.quests / missions / assault / campaign / eminence blobs and Quest[..] /
    // Mission[..] char_vars. A load takes the char's state from the rows, imported from the legacy side on the first load
    // and rebuilt from it when it changed behind them (the legacy side wins); each write goes to both in one transaction
    // (ZoneHost.PersistProgress). Rows are keyed through Registry (xi.quest.id) and re-keyed at load when a key differs
    public sealed class ProgressRows
    {
        private readonly IDatabase db_;

        // map.QUEST_STATE_ROWS (XI.Host); in-process hosts keep the default
        public bool Enabled { get; set; } = true;
        // all five tables exist (checked once, when built)
        public bool TablesPresent { get; }
        public bool On => Enabled && TablesPresent;
        public Func<long> UnixNow = XI.Common.EarthTime.UnixSec;
        public Action<string> Log = _ => { };
        // the stable quest keys (ZoneHost.InitScripts, from xi.quest.id); null: every key synthetic
        public QuestKeyRegistry Registry { get; set; }

        public ProgressRows(IDatabase db)
        {
            db_ = db;
            TablesPresent = db != null && HasTable(QuestStateSchema.TableName) && HasTable(MissionStateSchema.TableName) &&
                            HasTable(AssaultStateSchema.TableName) && HasTable(CampaignStateSchema.TableName) && HasTable(EminenceStateSchema.TableName);
        }

        private bool HasTable(string name)
        {
            var r = db_.Execute($"SHOW TABLES LIKE '{name}'", Array.Empty<BoundValue>());
            return r != null && r.Next();
        }

        // CharLoader.Load, once the blobs and char_vars are read: c.Progress, and the quest, mission, assault and campaign logs from its rows
        public void Load(CharData c)
        {
            c.Progress = null;
            if (!On) return;
            long now = UnixNow();
            var vars = new List<CharVar>();
            foreach (var kv in c.CharVars) vars.Add(new CharVar(kv.Key, kv.Value.Value, kv.Value.Expiry));
            var expected = CharProgress.Import(c.Id, c.QuestLog, c.MissionLog, vars, Registry, c.AssaultLog, c.CampaignLog, c.EminenceLog);
            expected.PruneExpired(now);

            try
            {
                CharProgress chosen;
                try
                {
                    chosen = ChooseState(c, expected, now);
                }
                catch (Exception ex)
                {
                    // a malformed stored row (a non-integer value, a key the importers never write, ...) is the same as a
                    // drifted one: the legacy side wins and rebuilds the rows
                    chosen = Replace(expected, $"progress: char {c.Id} re-imported (its rows could not be read: {ex.Message})");
                }
                if (chosen == null) return;
                // The eminence array stays as loaded: a row, keyed by record id, cannot hold one id in two slots, and the
                // engine can reach that (0x10C's timed guard never fires). Every other engine-reachable blob projects equal, and the drift check
                // just compared them.
                chosen.ProjectInto(c.QuestLog, c.MissionLog, c.AssaultLog, c.CampaignLog);
                c.Progress = chosen;
            }
            catch (Exception ex)
            {
                c.Progress = null;
                Log($"progress: char {c.Id}: {ex.Message}; the legacy state is used");
            }
        }

        // the state the char's rows give, or the rebuilt one when they are missing or drifted; null: the legacy state is used
        private CharProgress ChooseState(CharData c, CharProgress expected, long now)
        {
            var quests = QuestStateStore.LoadQuests(db_, c.Id);
            var missions = QuestStateStore.LoadMissions(db_, c.Id);
            var eminence = QuestStateStore.LoadEminence(db_, c.Id);
            if (quests == null || missions == null || eminence == null ||
                !QuestStateStore.TryLoadAssault(db_, c.Id, out var assault) || !QuestStateStore.TryLoadCampaign(db_, c.Id, out var campaign))
            {
                Log($"progress: char {c.Id}: its rows could not be read; the legacy state is used");
                return null;
            }
            if (quests.Count == 0 && missions.Count == 0 && assault == null && campaign == null && eminence.Count == 0)
                return Replace(expected, $"progress: char {c.Id} imported");
            var loaded = CharProgress.FromRows(c.Id, quests, missions, Registry, assault, campaign, eminence);
            // re-keyed first: a row the prune empties leaves the state, and its stored row is deleted only after the re-keyed
            // rows are written, so it must not hide a row whose new key is its stored one (the diff ignores keys)
            var rekeyed = loaded.Rekey();
            var pruned = loaded.PruneExpired(now);
            string diff = loaded.Diff(expected, assault == null, campaign == null);
            if (diff.Length > 0)
                return Replace(expected, $"progress: char {c.Id} re-imported (legacy {diff} changed)");
            // keys the registry names differently (a synthetic key from before it, a renamed constant): written before the
            // pruned rows are deleted, so each delete meets its own row's key
            if (pruned.IsEmpty && rekeyed.Count == 0) return loaded;
            if (db_.TransactionRetrying(() =>
                {
                    Must(QuestStateStore.Rekey(db_, rekeyed));
                    Must(QuestStateStore.Apply(db_, pruned));
                }, Retried(c.Id)))
            {
                if (rekeyed.Count > 0) Log($"progress: char {c.Id} re-keyed {rekeyed.Count} quests");
                return loaded;
            }
            Log($"progress: char {c.Id}: writing its pruned or re-keyed rows failed; the legacy state is used");
            return null;
        }

        // the load's writes are retried on a lock conflict (TransactionRetry): two chars logging in at once from two
        // processes can deadlock on the gap locks of their DELETEs (REPEATABLE READ), and the loser's write would otherwise
        // be lost for the session. Both write a state computed before the transaction, so a second run writes the same rows
        private CharProgress Replace(CharProgress state, string line)
        {
            if (!db_.TransactionRetrying(() => Must(QuestStateStore.ReplaceAll(db_, state)), Retried(state.CharId)))
            {
                Log($"progress: char {state.CharId}: writing its rows failed; the legacy state is used");
                return null;
            }
            Log(line);
            return state;
        }

        // logged without the "char <id>:" form of the failure lines: a retry that then succeeds lost nothing
        private Action<int, int> Retried(uint charId) =>
            (attempt, error) => Log($"progress: char {charId} retried its rows write (attempt {attempt}) after a lock conflict (MySQL {error})");

        private static void Must(bool ok)
        {
            if (!ok) throw new InvalidOperationException("a quest / mission row statement failed");
        }

        // ZoneHost.PersistProgress: the legacy write, then the row changes it makes, in one transaction. Rows off for the
        // char: the legacy write alone, as before. The rows failed: the legacy write again on its own, and the char's
        // rows off until its next load (whose drift check rebuilds them). The transaction failed before the legacy write
        // went through (it could not start, or the legacy statement failed): the legacy write once on its own, as the
        // rows-off path makes it; done, the rows go off until the next load; failed again, logged and lost, as before. That
        // fallback legacy() may itself throw while the DB is down, as the rows-off path does
        public void Persist(CharData c, Func<bool> legacy, Func<CharProgress, ProgressChanges> rows)
        {
            if (!On || c.Progress == null)
            {
                legacy();
                return;
            }
            var p = c.Progress;
            bool legacyOk = false;
            bool ok;
            try
            {
                ok = db_.Transaction(() =>
                {
                    legacyOk = legacy();
                    Must(legacyOk);
                    Must(QuestStateStore.Apply(db_, rows(p)));
                });
            }
            catch (Exception) when (!legacyOk)
            {
                // MySqlDatabase opens the connection and begins the transaction outside its own try; a throw after a good
                // legacy write is not caught here and propagates as before
                ok = false;
            }
            if (ok) return;
            if (!legacyOk)
            {
                if (legacy())
                {
                    c.Progress = null;
                    Log($"progress: char {c.Id}: the transaction failed; the legacy write ran on its own and its rows are off until its next load");
                }
                else
                    Log($"progress: char {c.Id}: its legacy write failed");
                return;
            }
            c.Progress = null;
            Log($"progress: char {c.Id}: writing its rows failed; its rows are off until its next load");
            legacy();
        }
    }
}
