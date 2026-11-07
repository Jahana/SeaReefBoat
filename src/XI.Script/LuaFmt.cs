using System;
using System.Globalization;
using System.Text;
using static XI.Script.LuaNative;

namespace XI.Script
{
    // lua_fmt: fmt::vformat over the Lua arguments (string, double, bool, nil as nullptr, anything else through
    // lua_to_string_depth). Covers the replacement-field forms scripts use: {}, {N}, {:[fill][align][0][width][.prec][type]}.
    public static class LuaFmt
    {
        public static string Format(string format, LuaState l, int firstArg, Func<LuaState, int, string> other)
        {
            var sb = new StringBuilder();
            int next = 0;
            for (int i = 0; i < format.Length; i++)
            {
                char ch = format[i];
                if (ch == '}')
                {
                    if (i + 1 < format.Length && format[i + 1] == '}') i++;
                    else throw new LuaException("unmatched '}' in format string");
                    sb.Append('}');
                    continue;
                }
                if (ch != '{')
                {
                    sb.Append(ch);
                    continue;
                }
                if (i + 1 < format.Length && format[i + 1] == '{')
                {
                    sb.Append('{');
                    i++;
                    continue;
                }
                int close = format.IndexOf('}', i);
                if (close < 0) throw new LuaException("invalid format string");
                string field = format.Substring(i + 1, close - i - 1);
                i = close;

                int colon = field.IndexOf(':');
                string id = colon < 0 ? field : field.Substring(0, colon);
                string spec = colon < 0 ? "" : field.Substring(colon + 1);
                int argNo = id.Length == 0 ? next++ : int.Parse(id, CultureInfo.InvariantCulture);
                int idx = firstArg + argNo;
                if (idx > l.Top) throw new LuaException("argument not found");
                sb.Append(FormatArg(l, idx, spec, other));
            }
            return sb.ToString();
        }

        private static string FormatArg(LuaState l, int idx, string spec, Func<LuaState, int, string> other)
        {
            char fill = ' ', align = '\0', type = '\0';
            bool zero = false;
            int width = 0, precision = -1, p = 0;
            if (spec.Length >= 2 && (spec[1] == '<' || spec[1] == '>' || spec[1] == '^'))
            {
                fill = spec[0];
                align = spec[1];
                p = 2;
            }
            else if (spec.Length >= 1 && (spec[0] == '<' || spec[0] == '>' || spec[0] == '^'))
            {
                align = spec[0];
                p = 1;
            }
            if (p < spec.Length && spec[p] == '0')
            {
                zero = true;
                p++;
            }
            while (p < spec.Length && char.IsAsciiDigit(spec[p])) width = width * 10 + (spec[p++] - '0');
            if (p < spec.Length && spec[p] == '.')
            {
                p++;
                precision = 0;
                while (p < spec.Length && char.IsAsciiDigit(spec[p])) precision = precision * 10 + (spec[p++] - '0');
            }
            if (p < spec.Length) type = spec[p++];
            if (p != spec.Length) throw new LuaException("invalid format specifier");

            string s;
            bool numeric = false;
            switch (l.Type(idx))
            {
                case LUA_TNONE:
                case LUA_TNIL:
                    s = "0x0";
                    break;
                case LUA_TSTRING:
                    s = l.ToStringValue(idx);
                    if (precision >= 0 && precision < s.Length) s = s.Substring(0, precision);
                    break;
                case LUA_TNUMBER:
                    s = FormatDouble(l.ToNumber(idx), precision, type);
                    numeric = true;
                    break;
                case LUA_TBOOLEAN:
                    s = l.ToBoolean(idx) ? "true" : "false";
                    break;
                default:
                    s = other(l, idx);
                    break;
            }

            if (s.Length >= width) return s;
            if (numeric && zero && align == '\0')
            {
                int sign = s.StartsWith('-') || s.StartsWith('+') ? 1 : 0;
                return s.Substring(0, sign) + new string('0', width - s.Length) + s.Substring(sign);
            }
            if (align == '\0') align = numeric ? '>' : '<';
            int pad = width - s.Length;
            return align switch
            {
                '>' => new string(fill, pad) + s,
                '^' => new string(fill, pad / 2) + s + new string(fill, pad - pad / 2),
                _ => s + new string(fill, pad),
            };
        }

        private static string FormatDouble(double d, int precision, char type)
        {
            var inv = CultureInfo.InvariantCulture;
            switch (type)
            {
                case 'f':
                case 'F':
                    return d.ToString("F" + (precision < 0 ? 6 : precision), inv);
                case 'e':
                    return d.ToString((precision < 0 ? "0.000000" : "0." + new string('0', precision)) + "e+00", inv);
                case 'g':
                    return FormatG(d, precision < 0 ? 6 : precision);
                case '\0':
                    return precision < 0 ? Shortest(d) : FormatG(d, precision);
                default:
                    throw new LuaException("invalid format specifier");
            }
        }

        // fmt's {} for a double: the shortest round-trip digits, fixed notation for exponents -5 < e < 16
        public static string Shortest(double d)
        {
            if (double.IsNaN(d)) return "nan";
            if (double.IsInfinity(d)) return d > 0 ? "inf" : "-inf";
            string r = d.ToString("R", CultureInfo.InvariantCulture);
            int e = r.IndexOf('E');
            if (e < 0) return r;
            int exp = int.Parse(r.Substring(e + 1), CultureInfo.InvariantCulture);
            if (exp > -5 && exp < 16) return ((decimal)d).ToString(CultureInfo.InvariantCulture);
            return r.Substring(0, e) + "e" + (exp < 0 ? "-" : "+") + Math.Abs(exp).ToString("00", CultureInfo.InvariantCulture);
        }

        // printf %g (fmt "{0:g}" in lua_to_string_depth)
        public static string FormatG(double d, int precision = 6)
        {
            if (double.IsNaN(d)) return "nan";
            if (double.IsInfinity(d)) return d > 0 ? "inf" : "-inf";
            if (precision == 0) precision = 1;
            if (d == 0) return d.ToString(CultureInfo.InvariantCulture) == "-0" ? "-0" : "0";
            string sci = d.ToString("E" + (precision - 1), CultureInfo.InvariantCulture);
            int exp = int.Parse(sci.Substring(sci.IndexOf('E') + 1), CultureInfo.InvariantCulture);
            if (exp < -4 || exp >= precision)
            {
                string mant = sci.Substring(0, sci.IndexOf('E'));
                if (mant.Contains('.')) mant = mant.TrimEnd('0').TrimEnd('.');
                return mant + "e" + (exp < 0 ? "-" : "+") + Math.Abs(exp).ToString("00", CultureInfo.InvariantCulture);
            }
            string fixedStr = d.ToString("F" + Math.Max(0, precision - 1 - exp), CultureInfo.InvariantCulture);
            if (fixedStr.Contains('.')) fixedStr = fixedStr.TrimEnd('0').TrimEnd('.');
            return fixedStr;
        }
    }
}
