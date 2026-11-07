using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MySqlConnector;
using XI.Persistence;
using XI.Quests;
using XI.Zone;

// Two fresh chars with adjacent ids logging in at once (TODO "Quest progress rows lost on concurrent first logins"): each
// first login writes the char's rows in one transaction (ProgressRows.Replace -> QuestStateStore.ReplaceAll). Under
// REPEATABLE READ a DELETE of rows that are not there takes a gap lock, both chars' DELETEs lock the same gap, and each
// INSERT then waits for the other's: a deadlock (1213), the loser rolled back. A gate holds each thread's first INSERT
// until both have run their DELETEs, so the interleaving is the same every run. Each scenario takes a pair of ids that
// has had no rows in this run: a deleted row stays in the index (delete-marked) until the purge, and one between or
// next to the pair changes which gaps the DELETEs lock (the deadlock then comes and goes, as in the golden flake)
internal static partial class Program
{
    // scenario n's pair: 996136 + 2n, 996137 + 2n (CharIds lists them for the cleanup)
    private static uint[] PairIds = { 996136, 996137 };
    private static void UsePair(int n) => PairIds = new[] { 996136u + 2u * (uint)n, 996137u + 2u * (uint)n };

    // the real database, each thread's first statement matching Gated held until both threads reach it
    private sealed class GateDatabase : IDatabase
    {
        private readonly IDatabase db_;
        private readonly Barrier barrier_ = new(2);
        private readonly ThreadLocal<bool> passed_ = new();
        public Func<string, bool> Gated = q => q.StartsWith("INSERT INTO char_", StringComparison.Ordinal);
        public int TimedOut;

        public GateDatabase(IDatabase db) { db_ = db; }

        private void Gate(string query)
        {
            if (passed_.Value || !Gated(query)) return;
            passed_.Value = true;
            // the other thread never comes (it did not need to DELETE, or it failed first): go on alone
            if (!barrier_.SignalAndWait(TimeSpan.FromSeconds(10))) Interlocked.Increment(ref TimedOut);
        }

        public ResultSet Execute(string query, IReadOnlyList<BoundValue> parameters)
        {
            Gate(query);
            return db_.Execute(query, parameters);
        }

        public ResultSet ExecuteBulk(string query, IReadOnlyList<BoundValue> parameters)
        {
            Gate(query);
            return db_.ExecuteBulk(query, parameters);
        }

        public string Schema => db_.Schema;
        public string Version => db_.Version;
        public string DriverVersion => db_.DriverVersion;
        public bool Transaction(Fn transactionFn) => db_.Transaction(transactionFn);
    }

    private static void ClearPair(IDatabase db)
    {
        foreach (var id in PairIds)
        {
            Sql(db, "DELETE FROM char_quest_state WHERE charid = ?", BoundValue.Of(id));
            Sql(db, "DELETE FROM char_mission_state WHERE charid = ?", BoundValue.Of(id));
            Sql(db, "DELETE FROM char_assault_state WHERE charid = ?", BoundValue.Of(id));
            Sql(db, "DELETE FROM char_campaign_state WHERE charid = ?", BoundValue.Of(id));
            Sql(db, "DELETE FROM char_eminence_state WHERE charid = ?", BoundValue.Of(id));
        }
    }

    private static long PairCount(IDatabase db, string table, uint id) =>
        Sql(db, $"SELECT COUNT(*) AS n FROM {table} WHERE charid = ?", BoundValue.Of(id)) is { } r && r.Next() ? r.Get<long>("n") : -1;

    private static void MustOk(bool ok)
    {
        if (!ok) throw new InvalidOperationException("a quest / mission row statement failed");
    }

    // the two chars' state: a quest each, so both tables get rows
    private static CharProgress FreshState(uint id)
    {
        var qb = new QuestBitBlob();
        qb.AddQuest(1, 4);
        return CharProgress.Import(id, qb.Bytes, new MissionBitBlob().Bytes, Array.Empty<CharVar>());
    }

    // both run at once, each on its own thread; the results in PairIds order
    private static T[] RunPair<T>(Func<int, T> body)
    {
        var results = new T[2];
        var errors = new Exception[2];
        var start = new Barrier(2);
        var threads = Enumerable.Range(0, 2).Select(i => new Thread(() =>
        {
            try
            {
                start.SignalAndWait();
                results[i] = body(i);
            }
            catch (Exception e)
            {
                errors[i] = e;
            }
        })).ToArray();
        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();
        foreach (var e in errors) if (e != null) throw new InvalidOperationException("a thread threw", e);
        return results;
    }

    private static string Codes(IEnumerable<Exception> errors) =>
        string.Join(",", errors.Where(e => e != null).Select(e => e is MySqlException m ? ((int)m.ErrorCode).ToString() : e.GetType().Name));

    private static void RunConcurrentFirstLogins(IDatabase db)
    {
        Console.WriteLine("two fresh chars with adjacent ids write their first rows at once");

        // the mechanism: the plain transaction, both DELETEs before either INSERT -> one deadlocks and is rolled back
        UsePair(0);
        var gate = new GateDatabase(db);
        var errors = new Exception[2];
        var plain = RunPair(i => gate.Transaction(() =>
        {
            try { MustOk(QuestStateStore.ReplaceAll(gate, FreshState(PairIds[i]))); }
            catch (Exception e) { errors[i] = e; throw; }
        }));
        Check("the plain transaction: one commits, the other is rolled back on a deadlock (1213)", "1/1213/0",
              $"{plain.Count(ok => ok)}/{Codes(errors)}/{gate.TimedOut}");

        // the same with the retrying transaction: the loser runs again once the winner committed, and both chars' rows land
        UsePair(1);
        gate = new GateDatabase(db);
        int retries = 0;
        var retried = RunPair(i => gate.TransactionRetrying(() => MustOk(QuestStateStore.ReplaceAll(gate, FreshState(PairIds[i]))),
                                                            (attempt, e) => Interlocked.Increment(ref retries)));
        Check("the retrying transaction: both commit, one retry, each char has its quest row and 15 mission rows", "True/True/1/0/1-15/1-15",
              $"{retried[0]}/{retried[1]}/{retries}/{gate.TimedOut}/{PairCount(db, "char_quest_state", PairIds[0])}-{PairCount(db, "char_mission_state", PairIds[0])}" +
              $"/{PairCount(db, "char_quest_state", PairIds[1])}-{PairCount(db, "char_mission_state", PairIds[1])}");

        // the login itself (ProgressRows.Load on two fresh chars at once): both chars get their rows, nothing fails
        UsePair(2);
        gate = new GateDatabase(db);
        var lines = new List<string>();
        var loaded = RunPair(i =>
        {
            var qb = new QuestBitBlob();
            qb.AddQuest(1, 4);
            var c = new CharData { Id = PairIds[i] };
            Array.Copy(qb.Bytes, c.QuestLog, Math.Min(qb.Bytes.Length, c.QuestLog.Length));
            var rows = new ProgressRows(gate) { Log = l => { lock (lines) lines.Add(l); } };
            rows.Load(c);
            return c.Progress != null;
        });
        lines.Sort(StringComparer.Ordinal);
        Check("ProgressRows.Load on both at once: both imported after one retry, no failure line",
              $"True/True/1/progress: char {PairIds[0]} imported | progress: char {PairIds[1]} imported",
              $"{loaded[0]}/{loaded[1]}/{lines.Count(l => l.Contains(" retried its rows write (attempt 2) after a lock conflict (MySQL 1213)"))}" +
              $"/{string.Join(" | ", lines.Where(l => !l.Contains(" retried ")))}");
        Check("  the gate never timed out, each char has its quest row and 15 mission rows", "0/1-15/1-15",
              $"{gate.TimedOut}/{PairCount(db, "char_quest_state", PairIds[0])}-{PairCount(db, "char_mission_state", PairIds[0])}" +
              $"/{PairCount(db, "char_quest_state", PairIds[1])}-{PairCount(db, "char_mission_state", PairIds[1])}");

        // the hit rate without the gate: pairs of plain ReplaceAll transactions started together, then the retrying ones,
        // each pair's rows deleted before the next (as the golden harness cleans its chars before a flow)
        UsePair(3);
        const int pairs = 50;
        int plainLost = 0, retryLost = 0;
        for (int n = 0; n < pairs; n++)
        {
            ClearPair(db);
            plainLost += RunPair(i => db.Transaction(() => MustOk(QuestStateStore.ReplaceAll(db, FreshState(PairIds[i]))))).Count(ok => !ok);
        }
        for (int n = 0; n < pairs; n++)
        {
            ClearPair(db);
            retryLost += RunPair(i => db.TransactionRetrying(() => MustOk(QuestStateStore.ReplaceAll(db, FreshState(PairIds[i]))))).Count(ok => !ok);
        }
        Console.WriteLine($"INFO {pairs} ungated pairs: the plain transaction lost {plainLost} writes, the retrying one {retryLost}");
        Check($"{pairs} ungated pairs with the retrying transaction: no write lost", "0", retryLost);
        for (int n = 0; n <= 3; n++)
        {
            UsePair(n);
            ClearPair(db);
        }
    }
}
