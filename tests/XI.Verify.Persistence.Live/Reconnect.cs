using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;
using XI.Persistence;

// CachingDatabase::runWithRetry on MySqlDatabase (lane engine-infra-1): a statement whose connection the server kills
// under it is run again after a reconnect (network.SQL_QUERY_RETRY_COUNT times), then the process terminates
// (Terminate, a DatabaseTerminatedException here); inside a transaction it is not retried; a reconnect that cannot
// reach the server terminates; a statement the server refuses is still "Query Failed" and null, with no retry; a pool
// with every connection in use is not a lost connection: its exception goes to the caller, no retry, no terminate
internal static partial class Program
{
    // a MySqlDatabase of its own whose lines go to the list
    private static MySqlDatabase LoggedDb(NetworkSettings settings, int retries, List<string> lines,
                                          string options = "")
    {
        var s = new NetworkSettings
        {
            SqlHost = settings.SqlHost, SqlPort = settings.SqlPort, SqlLogin = settings.SqlLogin,
            SqlPassword = settings.SqlPassword, SqlDatabase = settings.SqlDatabase, SqlQueryRetryCount = retries,
            ConnectionOptions = options,
        };
        return new MySqlDatabase(s) { Log = l => { lock (lines) lines.Add(l); } };
    }

    // kills the first connection running the statement (by its text), from a connection of its own, once it shows in
    // the process list; true when it killed one
    private static Task<bool> KillFirst(NetworkSettings settings, string statement) => Task.Run(() =>
    {
        using var k = new MySqlConnection(settings.BuildConnectionString() + "Pooling=false;");
        k.Open();
        for (int i = 0; i < 100; i++)
        {
            using var find = new MySqlCommand("SELECT ID FROM information_schema.PROCESSLIST WHERE INFO = @q LIMIT 1",
                                              k);
            find.Parameters.AddWithValue("@q", statement);
            if (find.ExecuteScalar() is { } id)
            {
                using var kill = new MySqlCommand($"KILL {Convert.ToInt64(id)}", k);
                kill.ExecuteNonQuery();
                return true;
            }
            Thread.Sleep(20);
        }
        return false;
    });

    private static string Retried(List<string> lines)
    {
        lock (lines) return string.Join(" | ", lines.Where(l => l.StartsWith("Connection lost")));
    }

    private static void CheckReconnectRetry(NetworkSettings settings)
    {
        Console.WriteLine("  reconnect and retry (network.SQL_QUERY_RETRY_COUNT)");
        try
        {
            // 1. killed under the statement, one retry: reconnected, run again, its row back
            var lines = new List<string>();
            using (var db = LoggedDb(settings, 1, lines))
            {
                const string q = "SELECT SLEEP(1) AS ei1_retry";
                var kill = KillFirst(settings, q);
                var rs = db.Execute(q, Array.Empty<BoundValue>());
                CheckEq("retry.killed_once_killed", kill.Result, true);
                CheckEq("retry.killed_once_row_back", rs != null && rs.Next() && rs.Get<int>(0) == 0, true);
                CheckEq("retry.killed_once_line", Retried(lines),
                        "Connection lost, re-establishing connection and retrying query (attempt 1)");
            }

            // 2. no retries (SQL_QUERY_RETRY_COUNT 0): the attempts run out, the process would terminate
            lines = new List<string>();
            using (var db = LoggedDb(settings, 0, lines))
            {
                const string q = "SELECT SLEEP(1) AS ei1_noretry";
                var kill = KillFirst(settings, q);
                string terminated = "no";
                try { db.Execute(q, Array.Empty<BoundValue>()); }
                catch (DatabaseTerminatedException e) { terminated = e.Message; }
                kill.Wait();
                CheckEq("retry.none_terminates", terminated, $"Query Failed after 1 retries: {q}");
                CheckEq("retry.none_no_retry_line", Retried(lines), "");
            }

            // 3. in a transaction: not retried (a new connection would commit the statement on its own); the
            // statement and the next one null, the transaction false
            lines = new List<string>();
            using (var db = LoggedDb(settings, 1, lines))
            {
                const string q = "SELECT SLEEP(1) AS ei1_tx";
                var kill = KillFirst(settings, q);
                ResultSet first = null, second = null;
                bool ok = db.Transaction(() =>
                {
                    first = db.Execute(q, Array.Empty<BoundValue>());
                    second = db.Execute("SELECT 2 AS ei1_tx_next", Array.Empty<BoundValue>());
                });
                kill.Wait();
                CheckEq("retry.tx_statement_null", first == null, true);
                CheckEq("retry.tx_next_statement_null", second == null, true);
                CheckEq("retry.tx_false", ok, false);
                CheckEq("retry.tx_lines", Retried(lines),
                        $"Connection lost mid-transaction, not retrying: {q} | " +
                        "Connection lost mid-transaction, not retrying: SELECT 2 AS ei1_tx_next");
                // the database still works after it
                var after = db.Execute("SELECT 3", Array.Empty<BoundValue>());
                CheckEq("retry.tx_after_ok", after != null && after.Next() && after.Get<int>(0) == 3, true);
            }

            // 4. a server that cannot be reached: the retry's reconnect fails and terminates
            lines = new List<string>();
            var unreachable = new NetworkSettings
            {
                SqlHost = "127.0.0.1", SqlPort = 3399, SqlLogin = "root", SqlPassword = "root", SqlDatabase = "xidb",
            };
            using (var db = LoggedDb(unreachable, 1, lines))
            {
                string terminated = "no";
                try { db.Execute("SELECT 1", Array.Empty<BoundValue>()); }
                catch (DatabaseTerminatedException e) { terminated = e.Message.Split(':')[0]; }
                CheckEq("retry.unreachable_terminates", terminated, "Failed to connect to database");
                lock (lines)
                    CheckEq("retry.unreachable_lines",
                            string.Join(" | ",
                                        lines.Where(l => l.StartsWith("Connection lost") || l.StartsWith("!!!"))),
                            "Connection lost, re-establishing connection and retrying query (attempt 1) | " +
                            "!!! Failed to connect to database, terminating server !!!");
            }

            // 5. a statement the server refuses: Query Failed and null, no retry
            lines = new List<string>();
            using (var db = LoggedDb(settings, 1, lines))
            {
                var rs = db.Execute("SELECT ei1_no_such_column FROM accounts", Array.Empty<BoundValue>());
                CheckEq("retry.refused_null", rs == null, true);
                lock (lines)
                    CheckEq("retry.refused_lines", string.Join(" | ", lines.Take(1)),
                            "Query Failed: SELECT ei1_no_such_column FROM accounts");
                CheckEq("retry.refused_no_retry", Retried(lines), "");
            }

            // 6. a pool of one connection, held by a transaction on another thread: the statement's Open runs out of
            // Connect Timeout (2 s) on the full pool, and that exception reaches the caller, no retry, no terminate
            lines = new List<string>();
            using (var db = LoggedDb(settings, 1, lines, "Maximum Pool Size=1;Connection Timeout=2;"))
            {
                using var holding = new ManualResetEventSlim();
                using var release = new ManualResetEventSlim();
                var holder = Task.Run(() => db.Transaction(() => { holding.Set(); release.Wait(10_000); }));
                holding.Wait(10_000);
                string got = "none";
                try { db.Execute("SELECT 6 AS ei1_pool", Array.Empty<BoundValue>()); got = "no exception"; }
                catch (DatabaseTerminatedException e) { got = "terminated: " + e.Message; }
                catch (MySqlException e) { got = MySqlDatabase.IsPoolExhausted(e) ? "pool exhausted" : e.Message; }
                release.Set();
                holder.Wait();
                CheckEq("retry.pool_exhausted_propagates", got, "pool exhausted");
                CheckEq("retry.pool_exhausted_no_retry", Retried(lines), "");
                var after = db.Execute("SELECT 7", Array.Empty<BoundValue>());
                CheckEq("retry.pool_after_ok", after != null && after.Next() && after.Get<int>(0) == 7, true);
            }
        }
        catch (Exception ex)
        {
            Bad("reconnect_retry", $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}
