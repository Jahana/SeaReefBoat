using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XI.Map;
using XI.Persistence;

namespace XI.Zone
{
    // A login herd: each char's map login (recv_parse's session key, charutils::LoadChar and its hooks, the 0x00A's
    // zone-in) reads about forty rows one query at a time, a few ms each on a database in a container, and the C++ does
    // that in the tick, so twenty logins in one tick hold it for seconds. Here the reads run on a worker first
    // (MapNetwork.PrefetchLogin holds the 0x00A meanwhile) into a PrefetchDatabase read set, and the login then runs in
    // the tick as before, its reads answered from the set. Only reads go to the worker: the char, the zone and every
    // write stay on the main thread, in the order the C++ has them.
    public sealed partial class ZoneHost
    {
        // the database the zone host, its loader and the session store read through (XI.Host's); null: no read-ahead
        public PrefetchDatabase LoginPrefetch { get; set; }

        // the logins whose reads run at once
        public int LoginReadWorkers { get; set; } = 4;

        private SemaphoreSlim loginReadSlots_;

        // MapNetwork.PrefetchLogin: the reads of the char's login, started on a worker
        public IDeferredLogin BeginLoginReads(uint charId)
        {
            var db = LoginPrefetch;
            if (db == null || loader_ == null) return null;
            loginReadSlots_ ??= new SemaphoreSlim(Math.Max(1, LoginReadWorkers));
            var reads = LoginReads(charId, loader_.SpellExpansions, loader_.Progress.On);
            // registered before the first read, so a write from here on that may touch the char is seen
            var set = db.Open(charId);
            set.Derive = (sql, args) => AbsentCharVar(set, charId, sql, args);
            var slots = loginReadSlots_;
            var task = Task.Run(async () =>
            {
                await slots.WaitAsync().ConfigureAwait(false);
                try
                {
                    foreach (var (sql, args) in reads) db.Read(set, sql, args);
                }
                finally
                {
                    slots.Release();
                }
            });
            return new DeferredLogin(db, set, task, log_);
        }

        // the reads a login makes, by the call sites' own SQL texts and bound values (a read missing here is only read in
        // the tick): DbMapSessionStore.TryLoadLogin; CharLoader.Load with its hooks (the recasts, the blue magic and
        // automaton, the effects, the quest / mission rows) and ZoneHost.OnLogin's (the zoning flag); the 0x00A's (the history, the mustZone vars
        // the zone's onZoneIn asks for, the mog locker vars HasMogLockerAccess reads, the previous zone line). A script's
        // getCharVar of a var the char does not have is answered from the char's char_vars (AbsentCharVar)
        public static List<(string Sql, BoundValue[] Args)> LoginReads(uint charId, string spellExpansions, bool progressRows = true)
        {
            var id = BoundValue.Of(charId);
            var list = new List<(string, BoundValue[])>();
            void One(string sql) => list.Add((sql, new[] { id }));
            One(DbMapSessionStore.SqlLogin);
            One(CharLoader.SqlChars);
            One(CharLoader.SqlFlags);
            One(CharLoader.SqlProfile);
            One(CharLoader.SqlUnlocks);
            One(CharLoader.SqlChocobo);
            One(CharLoader.SqlPoints);
            One(CharLoader.SqlVars);
            if (progressRows)
            {
                One(XI.Quests.QuestStateStore.SqlLoadQuests);
                One(XI.Quests.QuestStateStore.SqlLoadMissions);
                One(XI.Quests.QuestStateStore.SqlLoadAssault);
                One(XI.Quests.QuestStateStore.SqlLoadCampaign);
                One(XI.Quests.QuestStateStore.SqlLoadEminence);
            }
            One(CharLoader.SqlLook);
            One(CharLoader.SqlStyle);
            One(CharLoader.SqlStats);
            One(CharLoader.SqlJobs);
            One(CharLoader.SqlMerits);
            One(CharLoader.SqlExp);
            One(SqlRecasts);
            One(CharLoader.SqlSkills);
            One(CharLoader.SqlJobPoints);
            One(CharLoader.SqlStorage);
            One(CharLoader.SqlInventory);
            list.Add((CharLoader.SqlSpells, new[] { id, BoundValue.Of(spellExpansions) }));
            One(SqlSetBlueSpells);
            One(SqlAutomaton);
            One(SqlEffects);
            One(CharLoader.SqlEquip);
            One(SqlZoning);
            One(SqlHistory);
            One(SqlPrevZoneLine);
            list.Add((SqlVarsLike, new[] { id, BoundValue.Of("%]mustZone") }));
            list.Add((SqlCharVar, new[] { id, BoundValue.Of("mog-locker-expiry-timestamp") }));
            list.Add((SqlCharVar, new[] { id, BoundValue.Of("mog-locker-access-type") }));
            return list;
        }

        // CharVar's read of a var the char's cache lacks (a script's getCharVar of a var never set, mostly): the row the
        // database would give, from the char's whole char_vars the set holds. Only an absent var is answered here (no
        // rows): the column compares utf8mb4_general_ci, so when the name is not plain ASCII, or a var of the char could
        // match it but for case, accents or trailing spaces, the database is asked
        private static ResultSet AbsentCharVar(PrefetchDatabase.ReadSet set, uint charId, string sql, IReadOnlyList<BoundValue> args)
        {
            if (!ReferenceEquals(sql, SqlCharVar) && sql != SqlCharVar) return null;
            if (args.Count != 2 || args[0].Type != BoundValue.Kind.UInt32 || args[0].AsUInt32 != charId || args[1].Type != BoundValue.Kind.String) return null;
            string name = args[1].AsString;
            static bool Plain(string s)
            {
                foreach (char ch in s)
                    if (ch < 0x21 || ch > 0x7E) return false;
                return true;
            }
            if (name.Length == 0 || !Plain(name)) return null;
            var all = set.Held(CharLoader.SqlVars, new[] { BoundValue.Of(charId) });
            if (all == null) return null;
            while (all.Next())
            {
                string held = all.Get<string>("varname");
                if (!Plain(held.TrimEnd(' ')) || string.Equals(held.TrimEnd(' '), name, StringComparison.OrdinalIgnoreCase)) return null;
            }
            return new ResultSet(new InMemoryResultSet(new[] { "value", "expiry" }, Array.Empty<Dictionary<string, object>>()), sql, ResultSetType.Select);
        }

        private sealed class DeferredLogin : IDeferredLogin
        {
            private readonly PrefetchDatabase db_;
            private readonly PrefetchDatabase.ReadSet set_;
            private readonly Task task_;
            private readonly Action<string> log_;

            public DeferredLogin(PrefetchDatabase db, PrefetchDatabase.ReadSet set, Task task, Action<string> log)
            {
                db_ = db;
                set_ = set;
                task_ = task;
                log_ = log;
            }

            public bool Ready => task_.IsCompleted;

            // the set as far as it got: a read that failed is read again in the tick
            public IDisposable Serve()
            {
                if (task_.IsFaulted) log_($"zone: the login reads of char {set_.CharId} failed: {task_.Exception?.GetBaseException().Message}");
                return db_.Serve(set_);
            }

            public void Dispose() => set_.Dispose();
        }
    }
}
