using System;

namespace XI.Persistence
{
    // Mirrors db::ResultSetType (src/common/database/result_set.h).
    public enum ResultSetType
    {
        Select,  // queryable result set
        Update,  // only rowsCount()/rowsAffected() populated
        Invalid, // invalid query, nothing to do
    }
}
