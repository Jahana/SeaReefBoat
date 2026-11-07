using System.Collections.Generic;

namespace XI.Persistence
{
    // Mirror of the db::Database virtual surface (src/common/database/database.h) that the
    // port actually needs. Return type is the public 0-indexed ResultSet façade; a null
    // result means the query was Invalid (the engine returns nullptr there).
    public interface IDatabase
    {
        ResultSet Execute(string query, IReadOnlyList<BoundValue> parameters);
        ResultSet ExecuteBulk(string query, IReadOnlyList<BoundValue> parameters);
        string Schema { get; }
        string Version { get; }
        string DriverVersion { get; }
        bool Transaction(Fn transactionFn);
    }

    public delegate void Fn();
}
