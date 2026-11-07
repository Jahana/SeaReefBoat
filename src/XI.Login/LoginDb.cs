// Thin db::preparedStmt equivalent over IDatabase: lowers C# scalar args to BoundValue
// (callers cast enums to their underlying type first) and runs the query.

using System;
using XI.Persistence;

namespace XI.Login
{
    public static class LoginDb
    {
        public static ResultSet PreparedStmt(IDatabase db, string query, params object[] args)
        {
            var bound = new BoundValue[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                bound[i] = args[i] switch
                {
                    uint v => BoundValue.Of(v),
                    int v => BoundValue.Of(v),
                    ushort v => BoundValue.Of(v),
                    short v => BoundValue.Of(v),
                    byte v => BoundValue.Of(v),
                    sbyte v => BoundValue.Of(v),
                    bool v => BoundValue.Of(v),
                    string v => BoundValue.Of(v),
                    byte[] v => BoundValue.Of(v),
                    _ => throw new NotSupportedException($"unbindable argument type {args[i]?.GetType()}"),
                };
            }

            return db.Execute(query, bound);
        }
    }
}
