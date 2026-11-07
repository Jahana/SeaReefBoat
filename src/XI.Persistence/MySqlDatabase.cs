using System;
using System.Collections.Generic;
using System.Threading;
using MySqlConnector;

namespace XI.Persistence
{
    // MySqlConnector-backed IDatabase. Mirrors the C++ MariaDB backend's contract:
    //  - the leading keyword classifies the query into Select/Update/Invalid
    //  - '?' placeholders bind positionally, left-to-right, from the lowered BoundValues
    //  - an Invalid query returns null (the engine returns nullptr)
    //
    // Offline note: constructing and the pure translation helpers run without a server;
    // only Execute/ExecuteBulk touch the network, and that live path is the deferred
    // verification boundary (needs a running MariaDB with the xidb schema).
    public sealed class MySqlDatabase : IDatabase, IDisposable
    {
        private readonly NetworkSettings _settings;
        private readonly string _connectionString;

        // db::transaction: the calling thread's open transaction, which Execute and ExecuteBulk run on so the
        // rollback covers them (each statement otherwise takes a pooled connection of its own)
        [ThreadStatic] private static MySqlTransaction tx_;

        public MySqlDatabase(NetworkSettings settings)
        {
            _settings = settings;
            _connectionString = settings.BuildConnectionString();
        }

        public string Schema => _settings.SqlDatabase;
        public string Version => string.Empty;     // populated by a live SELECT VERSION()
        public string DriverVersion => string.Empty; // populated by a live driver probe

        // Pure: maps a BoundValue to its MySqlDbType, mirroring the db::BoundValue variant's
        // type set (no Int64/UInt64 bind width on the C++ side).
        public static MySqlDbType DbTypeFor(BoundValue value)
        {
            return value.Type switch
            {
                BoundValue.Kind.Int8 => MySqlDbType.Byte,
                BoundValue.Kind.UInt8 => MySqlDbType.UByte,
                BoundValue.Kind.Int16 => MySqlDbType.Int16,
                BoundValue.Kind.UInt16 => MySqlDbType.UInt16,
                BoundValue.Kind.Int32 => MySqlDbType.Int32,
                BoundValue.Kind.UInt32 => MySqlDbType.UInt32,
                BoundValue.Kind.Bool => MySqlDbType.Bool,
                BoundValue.Kind.Float => MySqlDbType.Float,
                BoundValue.Kind.Double => MySqlDbType.Double,
                BoundValue.Kind.String => MySqlDbType.String,
                BoundValue.Kind.Blob => MySqlDbType.Blob,
                _ => throw new NotSupportedException($"unboundable value kind {value.Type}"),
            };
        }

        // Pure: builds MySqlParameters from lowered values, preserving order.
        public static List<MySqlParameter> BuildParameters(IReadOnlyList<BoundValue> parameters)
        {
            var list = new List<MySqlParameter>(parameters?.Count ?? 0);
            if (parameters == null)
            {
                return list;
            }

            foreach (var p in parameters)
            {
                list.Add(new MySqlParameter
                {
                    MySqlDbType = DbTypeFor(p),
                    Value = p.Value,
                });
            }

            return list;
        }

        // the server's lines (ShowInfo / ShowError / ShowCritical): standard error unless the host routes them
        public Action<string> Log { get; set; } = Console.Error.WriteLine;

        // std::terminate after the retries ran out or a reconnect failed. A host (XI.Host) ends the process; the
        // default throws DatabaseTerminatedException, so a test or a tool sees the failure as an exception, as before
        public Action<string> Terminate { get; set; } = message => throw new DatabaseTerminatedException(message);

        // CachingDatabase's makeQueryTimer (logging.SQL_SLOW_QUERY_*): once QueryTimers is on (db::enableTimers, which
        // the map process calls at the end of its init), a statement that ran longer than SlowQueryErrorMs is logged
        // to LogError, else one longer than SlowQueryWarningMs to LogWarning: "SQL query took <n>ms: <query>"
        public bool QueryTimers { get; set; }
        public bool SlowQueryLog { get; set; } = true;
        public uint SlowQueryWarningMs { get; set; } = 100;
        public uint SlowQueryErrorMs { get; set; } = 250;
        public Action<string> LogWarning { get; set; }
        public Action<string> LogError { get; set; }

        // the levels of the server's other lines (Log is ShowError's): ShowInfo's "Connection lost, re-establishing"
        // and ShowCritical's "Query Failed after <n> retries" / "Failed to connect"; null: Log
        public Action<string> LogInfo { get; set; }
        public Action<string> LogCritical { get; set; }

        // DebugSQLFmt (logging.DEBUG_SQL): each statement's "preparedStmt: <query>"; null: off
        public Action<string> DebugSql { get; set; }

        public ResultSet Execute(string query, IReadOnlyList<BoundValue> parameters)
        {
            var type = QueryValidation.ValidateLeadingKeyword(query);
            if (type == ResultSetType.Invalid || !QueryValidation.ValidateContent(query))
            {
                return null; // engine returns nullptr for an invalid query
            }

            // CachingDatabase::runWithRetry: a statement the server refuses (a duplicate key, a bad column) is logged
            // and comes back as nullptr; a lost connection is reconnected and the statement run again (RunWithRetry).
            // A lock conflict (deadlock, lock wait timeout) and a connection count error still get past it, as
            // before: TransactionRetry reads the conflict off the exception
            return RunWithRetry(query, () =>
            {
                try
                {
                    return ExecuteChecked(query, type, parameters);
                }
                catch (MySqlException e) when (!IsConnectionError(e) && !e.IsTransient)
                {
                    Log($"Query Failed: {query}");
                    Log(e.Message);
                    return null;
                }
            });
        }

        // detail::libmariadb::isConnectionLost as MySqlConnector reports its codes. The codes are
        // CR_CONNECTION_ERROR 2002, CR_CONN_HOST_ERROR 2003, CR_SERVER_GONE_ERROR 2006, CR_SERVER_LOST 2013 and
        // CR_SERVER_LOST_EXTENDED 2055. MySqlConnector reports a host it cannot reach as 1042 (or the client codes
        // themselves), and a connection that broke under a command as an incomplete response, an I/O or a socket
        // error somewhere in the exception's chain.
        // A deliberate difference: the pool running out ("Connect Timeout expired. All pooled connections are in
        // use.", also 1042, its message the only thing that tells it apart) is not a lost connection. The C++ has one
        // connection per thread and no pool, so it has no such case; a reconnect would free nothing in use, so the
        // exception goes to the caller as it did before the retries
        public static bool IsConnectionError(Exception e)
        {
            if (IsPoolExhausted(e)) return false;
            for (var x = e; x != null; x = x.InnerException)
            {
                if (x is MySqlException m && (m.ErrorCode == MySqlErrorCode.UnableToConnectToHost ||
                                              m.Number is 2002 or 2003 or 2006 or 2013 or 2055))
                    return true;
                // MySqlEndOfStreamException (an incomplete response) is an EndOfStreamException, an IOException
                if (x is System.IO.IOException || x is System.Net.Sockets.SocketException)
                    return true;
            }
            return false;
        }

        // MySqlConnector's ConnectionPool: every pooled connection in use for the whole Connect Timeout
        public static bool IsPoolExhausted(Exception e)
        {
            for (var x = e; x != null; x = x.InnerException)
                if (x is MySqlException { ErrorCode: MySqlErrorCode.UnableToConnectToHost } m &&
                    m.Message.Contains("All pooled connections are in use", StringComparison.Ordinal))
                    return true;
            return false;
        }

        // CachingDatabase::runWithRetry: the operation once, then up to network.SQL_QUERY_RETRY_COUNT more times while
        // it fails on a lost connection, each after a reconnect; still failing, the process terminates. Inside a
        // transaction a lost connection is not retried (a new connection starts in autocommit, so the statement would
        // commit on its own): logged, and null back, which the transaction then fails on
        private T RunWithRetry<T>(string query, Func<T> operation) where T : class
        {
            int attempts = 1 + Math.Max(0, _settings.SqlQueryRetryCount);
            for (int i = 0; i < attempts; ++i)
            {
                try
                {
                    if (i > 0)
                    {
                        (LogInfo ?? Log)(
                            $"Connection lost, re-establishing connection and retrying query (attempt {i})");
                        Reconnect();
                    }
                    // a transaction whose connection broke on an earlier statement: lost, as the statement would be
                    if (tx_ != null && tx_.Connection?.State != System.Data.ConnectionState.Open)
                        throw new System.IO.IOException("the transaction's connection is closed");
                    return operation();
                }
                catch (Exception e) when (IsConnectionError(e))
                {
                    if (tx_ != null)
                    {
                        Log($"Connection lost mid-transaction, not retrying: {query}");
                        Log(e.Message);
                        return null;
                    }
                }
            }
            (LogCritical ?? Log)($"Query Failed after {attempts} retries: {query}");
            Thread.Sleep(1000);
            Terminate($"Query Failed after {attempts} retries: {query}");
            return null;
        }

        // createConnection: the pool's idle connections dropped and a new one opened (it goes back to the pool for the
        // retry to take). LibMariaDBConnection::connect terminates when the server cannot be reached
        private void Reconnect()
        {
            using (var pooled = new MySqlConnection(_connectionString)) MySqlConnection.ClearPool(pooled);
            try
            {
                using var probe = Open();
            }
            // the pool still full: not a server that cannot be reached, so it goes to the caller (IsConnectionError)
            catch (Exception e) when (e is not DatabaseTerminatedException && !IsPoolExhausted(e))
            {
                (LogCritical ?? Log)("!!! Failed to connect to database, terminating server !!!");
                (LogCritical ?? Log)(e.Message);
                Thread.Sleep(1000);
                Terminate($"Failed to connect to database: {e.Message}");
            }
        }

        private ResultSet ExecuteChecked(string query, ResultSetType type, IReadOnlyList<BoundValue> parameters)
        {
            using var owned = tx_ == null ? Open() : null;
            DebugSql?.Invoke($"preparedStmt: {query}");
            using var command = new MySqlCommand(query, owned ?? tx_.Connection, tx_);
            foreach (var p in BuildParameters(parameters))
            {
                command.Parameters.Add(p);
            }

            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                return Run(command, query, type);
            }
            finally
            {
                TimeQuery(query, start);
            }
        }

        // makeQueryTimer's scope end: the statement's time against the thresholds
        private void TimeQuery(string query, long start)
        {
            if (!QueryTimers || !SlowQueryLog) return;
            long ms = (long)System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            if (ms > SlowQueryErrorMs) (LogError ?? Log)?.Invoke($"SQL query took {ms}ms: {query}");
            else if (ms > SlowQueryWarningMs) (LogWarning ?? Log)?.Invoke($"SQL query took {ms}ms: {query}");
        }

        private static ResultSet Run(MySqlCommand command, string query, ResultSetType type)
        {
            if (type == ResultSetType.Select)
            {
                // Mirror LibMariaDBPreparedStatement::executeQuery -> fetchRows(): the engine
                // materializes every row into an owned std::vector<Row> before returning, and the
                // ResultSet holds NO live cursor/connection. Draining the reader here (while the
                // connection is still open) into the in-memory owned-row model is the faithful
                // port; returning a streaming reader would dangle once `connection` disposes.
                using var reader = command.ExecuteReader();
                return new ResultSet(Materialize(reader), query, ResultSetType.Select);
            }

            var affected = (uint)command.ExecuteNonQuery();
            return new ResultSet(EmptyRaw.Instance, query, ResultSetType.Update, affected);
        }

        // Drains a live reader into the engine's owned snapshot shape: an ordered label list plus
        // per-row cells keyed by label, preserving each column's native CLR type (uint/ushort/byte
        // for unsigned ints, byte[] for BINARY/BLOB, float/double, string) exactly as fetchRows()
        // binds native forms, and mapping DBNull -> null so IsNull/Get<string>="" hold.
        private static InMemoryResultSet Materialize(MySqlDataReader reader)
        {
            var fieldCount = reader.FieldCount;
            var labels = new List<string>(fieldCount);
            for (var i = 0; i < fieldCount; i++)
            {
                labels.Add(reader.GetName(i));
            }

            var rows = new List<Dictionary<string, object>>();
            while (reader.Read())
            {
                var row = new Dictionary<string, object>(fieldCount);
                for (var i = 0; i < fieldCount; i++)
                {
                    row[labels[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }

                rows.Add(row);
            }

            return new InMemoryResultSet(labels, rows);
        }

        public ResultSet ExecuteBulk(string query, IReadOnlyList<BoundValue> parameters)
        {
            // Bulk sends every row in one round trip; the C++ backend validates the query,
            // executes, and throws on failure so db::transaction turns it into a rollback.
            var type = QueryValidation.ValidateLeadingKeyword(query);
            if (type == ResultSetType.Invalid)
            {
                throw new InvalidOperationException($"bulk statement failed: invalid query {query}");
            }

            // a lost connection is retried as a single statement is (runWithRetry); a lost one inside a transaction
            // comes back null there, which db::executeBulk throws on
            var result = RunWithRetry(query, () =>
            {
                using var owned = tx_ == null ? Open() : null;
                using var command = new MySqlCommand(query, owned ?? tx_.Connection, tx_);
                foreach (var p in BuildParameters(parameters))
                {
                    command.Parameters.Add(p);
                }

                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    command.ExecuteNonQuery();
                }
                finally
                {
                    TimeQuery(query, start);
                }
                return new ResultSet(EmptyRaw.Instance, query, ResultSetType.Update);
            });
            return result ?? throw new InvalidOperationException($"bulk statement failed: {query}");
        }

        private MySqlConnection Open()
        {
            var connection = new MySqlConnection(_connectionString);
            try
            {
                connection.Open();
            }
            catch
            {
                connection.Dispose();
                throw;
            }
            return connection;
        }

        // db::transaction: rolls back if the function throws, otherwise commits; true only when the commit succeeded.
        // A transaction opened inside another one joins it. Its START TRANSACTION is a statement of its own
        // (db::transactionStart), so a lost connection there is retried as any statement's is; one the server refuses
        // fails the transaction ("Failed to start transaction")
        public bool Transaction(Fn transactionFn)
        {
            if (tx_ != null)
            {
                transactionFn();
                return true;
            }

            var started = RunWithRetry("START TRANSACTION", () =>
            {
                var c = Open();
                try
                {
                    return c.BeginTransaction();
                }
                catch (MySqlException e) when (!IsConnectionError(e))
                {
                    c.Dispose();
                    Log("Failed to start transaction");
                    Log(e.Message);
                    return null;
                }
                catch
                {
                    c.Dispose();
                    throw;
                }
            });
            if (started == null) return false;
            using var connection = started.Connection;
            using var tx = started;
            tx_ = tx;
            try
            {
                transactionFn();
                tx_ = null;
                tx.Commit();
                return true;
            }
            catch
            {
                tx_ = null;
                try { tx.Rollback(); } catch { }
                return false;
            }
        }

        public void Dispose()
        {
            MySqlConnection.ClearAllPools();
        }

        private sealed class EmptyRaw : IRawResultSet
        {
            public static readonly EmptyRaw Instance = new EmptyRaw();
            public bool RawNext() => false;
            public int RawRowsCount() => 0;
            public int RawColumnCount() => 0;
            public string RawColumnLabel(int oneBasedIndex) => throw new IndexOutOfRangeException();
            public bool RawIsNull(string key) => true;
            public long RawGetInt64(string key) => 0;
            public ulong RawGetUInt64(string key) => 0;
            public int RawGetInt32(string key) => 0;
            public uint RawGetUInt32(string key) => 0;
            public short RawGetInt16(string key) => 0;
            public ushort RawGetUInt16(string key) => 0;
            public sbyte RawGetInt8(string key) => 0;
            public byte RawGetUInt8(string key) => 0;
            public bool RawGetBool(string key) => false;
            public float RawGetFloat(string key) => 0;
            public double RawGetDouble(string key) => 0;
            public string RawGetString(string key) => string.Empty;
            public byte[] RawGetBlobBytes(string key) => Array.Empty<byte>();
        }
    }

    // MySqlDatabase.Terminate's default: where the C++ server terminates (the retries of a lost connection ran out,
    // or a reconnect failed), a process that did not ask to end gets this
    public sealed class DatabaseTerminatedException : Exception
    {
        public DatabaseTerminatedException(string message) : base(message) { }
    }
}
