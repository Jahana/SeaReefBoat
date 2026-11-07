using System;
using System.Text;

namespace XI.Persistence
{
    // Mirror of db::escapeString (src/common/database/database.cpp).
    // Byte-for-byte: same replacement set, same strlen-style early break on NUL.
    public static class SqlEscape
    {
        public static string Escape(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(str.Length);
            for (var i = 0; i < str.Length; i++)
            {
                var c = str[i];

                // Emulate the original strlen-based SqlConnection::EscapeString:
                // the C++ loop breaks on '\0' BEFORE consulting the map, so the
                // {'\0', "\\0"} entry in the C++ table is dead code. Preserve that.
                if (c == '\0')
                {
                    break;
                }

                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\'': sb.Append("\\'"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\x1a': sb.Append("\\Z"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '%': sb.Append("\\%"); break;
                    case '|': sb.Append("\\|"); break;
                    case ';': sb.Append("\\;"); break;
                    default: sb.Append(c); break;
                }
            }

            return sb.ToString();
        }
    }
}
