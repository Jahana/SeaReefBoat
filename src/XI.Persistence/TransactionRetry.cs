using System;
using System.Threading;
using MySqlConnector;

namespace XI.Persistence
{
    // A transaction run again when the server rolled it back on a lock conflict: a deadlock (1213, ER_LOCK_DEADLOCK) or a
    // lock wait timeout (1205, ER_LOCK_WAIT_TIMEOUT). Both are transient in MySqlConnector, so MySqlDatabase.Execute lets
    // them through to the transaction function, which IDatabase.Transaction turns into a rollback and false. Only for a
    // function that can run twice: its statements are rolled back between attempts, so it must change nothing outside
    // the database that a second run would repeat or skip (a packet sent, an in-memory state applied, an item moved).
    // Inside another transaction nothing is retried (the outer one owns the rollback; the conflict propagates to it).
    public static class TransactionRetry
    {
        // attempts in all, the first included
        public const int MaxAttempts = 4;

        // the conflict that rolled the transaction back, if it was one: 1213 or 1205 anywhere in the exception's chain
        public static MySqlException LockConflict(Exception e)
        {
            for (; e != null; e = e.InnerException)
                if (e is MySqlException m && (m.ErrorCode == MySqlErrorCode.LockDeadlock || m.ErrorCode == MySqlErrorCode.LockWaitTimeout))
                    return m;
            return null;
        }

        // db.Transaction(fn), run again after a short random wait (5-10, 10-20, 20-40 ms) while it was rolled back on a
        // deadlock, MaxAttempts in all; retried(attempt, MySQL error number) before each new attempt. false: the last attempt
        // failed, or an attempt failed on something else, a lock wait timeout included (not retried)
        public static bool TransactionRetrying(this IDatabase db, Fn fn, Action<int, int> retried = null)
        {
            for (int attempt = 1; ; attempt++)
            {
                MySqlException conflict = null;
                bool ok = db.Transaction(() =>
                {
                    conflict = null;
                    try
                    {
                        fn();
                    }
                    catch (Exception e) when (LockConflict(e) is { } c)
                    {
                        conflict = c;
                        throw;
                    }
                });
                // a lock wait timeout is not retried: it arrives only after innodb_lock_wait_timeout (50 s by default), and
                // these writes run in the zone tick, so a retry would stall the zone for that long again
                if (ok || conflict == null || conflict.ErrorCode == MySqlErrorCode.LockWaitTimeout || attempt >= MaxAttempts)
                    return ok;
                retried?.Invoke(attempt + 1, (int)conflict.ErrorCode);
                int unit = 5 << (attempt - 1);
                Thread.Sleep(unit + Random.Shared.Next(unit + 1));
            }
        }
    }
}
