using System;

namespace XI.Persistence
{
    // Mirror of db::detail query validation (src/common/database/database.cpp).
    public static class QueryValidation
    {
        // Mirrors validateQueryLeadingKeyword: split on spaces (uppercased), take first
        // non-empty token, map to a ResultSetType.
        public static ResultSetType ValidateLeadingKeyword(string query)
        {
            var parts = query.ToUpperInvariant().Split(' ');

            string keyword = null;
            foreach (var raw in parts)
            {
                var part = raw.Trim('\n', ' ');
                if (part.Length > 0)
                {
                    keyword = part;
                    break;
                }
            }

            if (keyword == null)
            {
                return ResultSetType.Invalid;
            }

            switch (keyword)
            {
                case "SELECT":
                case "SHOW":
                    return ResultSetType.Select;
                case "INSERT":
                case "UPDATE":
                case "DELETE":
                case "REPLACE":
                case "CREATE":
                case "ALTER":
                case "DROP":
                case "TRUNCATE":
                case "SET":
                case "START":
                case "COMMIT":
                case "ROLLBACK":
                    return ResultSetType.Update;
                default:
                    return ResultSetType.Invalid;
            }
        }

        // Mirrors validateQueryContent: reject '{}' (fmt leftovers) and ';' (multi-statement).
        // '%' is the SQL wildcard and is deliberately NOT rejected.
        public static bool ValidateContent(string query)
        {
            if (query.Contains("{}"))
            {
                return false;
            }

            if (query.Contains(';'))
            {
                return false;
            }

            return true;
        }
    }
}
