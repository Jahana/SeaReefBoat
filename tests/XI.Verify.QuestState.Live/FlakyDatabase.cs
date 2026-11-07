using System;
using System.Collections.Generic;
using XI.Persistence;

// the real database, with failures on demand: a transaction that cannot start (throws, as MySqlDatabase does when the
// connection cannot open), and statements whose text matches a predicate (null result, as a failed query)
internal sealed class FlakyDatabase : IDatabase
{
    private readonly IDatabase db_;
    public bool ThrowOnNextTransaction;
    public Func<string, bool> FailWhen = _ => false;

    public FlakyDatabase(IDatabase db) { db_ = db; }

    public ResultSet Execute(string query, IReadOnlyList<BoundValue> parameters) => FailWhen(query) ? null : db_.Execute(query, parameters);
    public ResultSet ExecuteBulk(string query, IReadOnlyList<BoundValue> parameters) => FailWhen(query) ? null : db_.ExecuteBulk(query, parameters);
    public string Schema => db_.Schema;
    public string Version => db_.Version;
    public string DriverVersion => db_.DriverVersion;

    public bool Transaction(Fn transactionFn)
    {
        if (ThrowOnNextTransaction)
        {
            ThrowOnNextTransaction = false;
            throw new InvalidOperationException("the connection could not be opened");
        }
        return db_.Transaction(transactionFn);
    }
}
