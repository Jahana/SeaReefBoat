// Port of common/settings.cpp (init + get<T>): flat "<file>.<KEY>" map of bool / double / string.
//
// C++ evaluates settings/default/*.lua then settings/*.lua in a real Lua state. The shipped files only use two
// shapes, which this line-oriented reader covers without a Lua runtime:
//   xi.settings.<file> =            -- table block of  KEY = <string|number|bool>,
//   xi.settings.<file>.KEY = value  -- single-key override
// Nested table values (e.g. { DAY = 6, MONTH = 4 }) are skipped, exactly as the C++ scrape ignores non-scalar values.
// A table of strings (login.BANNED_WORDS_LIST, which login_helpers.cpp reads from Lua) is kept apart: GetStringList.
// User files override per key, then XI_<FILE>_<KEY> environment variables override both (an all-digit value as a
// number, anything else as a string). Get<T> reads a key as settings::get<T> does.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace XI.Common
{
    public sealed class Settings
    {
        private readonly Dictionary<string, object> _map = new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _lists = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, object> All => _map;

        private static readonly Regex BlockStart = new Regex(@"^xi\.settings\.([A-Za-z_]\w*)\s*=\s*(\{\s*)?$", RegexOptions.Compiled);
        private static readonly Regex Entry = new Regex(@"^([A-Za-z_]\w*)\s*=\s*(.+?)\s*,?\s*$", RegexOptions.Compiled);
        private static readonly Regex Assign = new Regex(@"^xi\.settings\.([A-Za-z_]\w*)\.([A-Za-z_]\w*)\s*=\s*(.+?)\s*;?\s*$", RegexOptions.Compiled);
        // `KEY =` / `xi.settings.<file>.KEY =` with the value (a table's brace) on the next line
        private static readonly Regex EntryKeyOnly = new Regex(@"^([A-Za-z_]\w*)\s*=$", RegexOptions.Compiled);
        private static readonly Regex AssignKeyOnly = new Regex(@"^xi\.settings\.([A-Za-z_]\w*)\.([A-Za-z_]\w*)\s*=$", RegexOptions.Compiled);

        // XI_SETTINGS_DEFAULTS_ONLY=1 (C# only; every test project sets it): settings/default/*.lua alone, without the
        // local settings/*.lua overrides, so the suites see the shipped values whatever the live server is set to
        public static bool DefaultsOnly => Environment.GetEnvironmentVariable("XI_SETTINGS_DEFAULTS_ONLY") == "1";

        // Loads <root>/settings/default/*.lua then <root>/settings/*.lua (both in ordinal filename order; the second
        // skipped under DefaultsOnly), then env vars.
        public static Settings Load(string root, Func<string, string> env = null)
        {
            var s = new Settings();
            foreach (var dir in DefaultsOnly ? new[] { Path.Combine(root, "settings", "default") }
                                             : new[] { Path.Combine(root, "settings", "default"), Path.Combine(root, "settings") })
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var f in Directory.GetFiles(dir, "*.lua").OrderBy(x => x, StringComparer.Ordinal))
                    s.ParseInto(File.ReadAllText(f));
            }
            s.ApplyEnvironment(env ?? Environment.GetEnvironmentVariable);
            return s;
        }

        public static Settings Parse(string lua)
        {
            var s = new Settings();
            s.ParseInto(lua);
            return s;
        }

        public void ParseInto(string lua)
        {
            string block = null, pending = null;
            int depth = 0; // nesting inside a block's nested table value
            // a nested table value being read (a block's `KEY = {` or a `xi.settings.<file>.KEY = {` over several lines):
            // its text, kept when the table turns out to be a list of strings
            string listKey = null;
            var listText = new System.Text.StringBuilder();
            int assignDepth = 0;
            string pendingKey = null; // a `KEY =` whose value starts on the next line
            foreach (var raw in lua.Split('\n'))
            {
                string line = StripComment(raw).Trim();
                if (line.Length == 0) continue;
                int opens = line.Count(c => c == '{'), closes = line.Count(c => c == '}');

                string waiting = pendingKey;
                pendingKey = null;
                if (waiting != null && line.StartsWith("{") && (block == null || depth == 0))
                {
                    int open = opens - closes;
                    if (open <= 0) StoreList(waiting, line);
                    else
                    {
                        listKey = waiting;
                        listText.Clear().Append(line);
                        if (block == null) assignDepth = open; else depth = open;
                    }
                    continue;
                }

                if (block == null)
                {
                    if (assignDepth > 0)
                    {
                        assignDepth += opens - closes;
                        listText.Append(' ').Append(line);
                        if (assignDepth <= 0) { StoreList(listKey, listText.ToString()); listKey = null; assignDepth = 0; }
                        continue;
                    }

                    if (pending != null)
                    {
                        string name = pending;
                        pending = null;
                        if (line == "{") { block = name; depth = 0; continue; }
                    }

                    var m = BlockStart.Match(line);
                    if (m.Success)
                    {
                        if (m.Groups[2].Success) { block = m.Groups[1].Value; depth = 0; }
                        else pending = m.Groups[1].Value; // brace on the next line
                        continue;
                    }

                    var ak = AssignKeyOnly.Match(line);
                    if (ak.Success) { pendingKey = ak.Groups[1].Value + "." + ak.Groups[2].Value; continue; }

                    var a = Assign.Match(line);
                    if (a.Success)
                    {
                        string key = a.Groups[1].Value + "." + a.Groups[2].Value, value = a.Groups[3].Value;
                        if (TryValue(value, out var v)) _map[key] = v;
                        else if (value.StartsWith("{") && opens > closes)
                        {
                            listKey = key;
                            listText.Clear().Append(value);
                            assignDepth = opens - closes;
                        }
                        else StoreList(key, value);
                    }
                    continue;
                }

                if (depth == 0 && line.StartsWith("}")) { block = null; continue; }

                if (depth > 0)
                {
                    depth += opens - closes;
                    if (listKey != null)
                    {
                        listText.Append(' ').Append(line);
                        if (depth <= 0) { StoreList(listKey, listText.ToString()); listKey = null; depth = 0; }
                    }
                    continue;
                }

                var ek = EntryKeyOnly.Match(line);
                if (ek.Success) { pendingKey = block + "." + ek.Groups[1].Value; continue; }

                var e = Entry.Match(line);
                if (opens > closes)
                {
                    // nested table value: skipped as a scalar; a list of strings (login.BANNED_WORDS_LIST) is kept
                    depth += opens - closes;
                    if (e.Success && e.Groups[2].Value.StartsWith("{"))
                    {
                        listKey = block + "." + e.Groups[1].Value;
                        listText.Clear().Append(e.Groups[2].Value);
                    }
                    continue;
                }

                if (!e.Success) continue;
                if (TryValue(e.Groups[2].Value, out var val)) _map[block + "." + e.Groups[1].Value] = val;
                else StoreList(block + "." + e.Groups[1].Value, e.Groups[2].Value);
            }
        }

        private static readonly Regex StringList = new Regex(@"^\{\s*(?:(?:'[^']*'|""[^""]*"")\s*[,;]?\s*)*\}$", RegexOptions.Compiled);
        private static readonly Regex StringItem = new Regex(@"'([^']*)'|""([^""]*)""", RegexOptions.Compiled);

        // A table of string literals ({ 'a', "b", }) under key; a later file's table replaces an earlier one whole, as the
        // C++ fillMissingKeys never merges into a sequence. Anything else (a keyed table, numbers) is not a list.
        private void StoreList(string key, string text)
        {
            text = text.Trim();
            if (text.EndsWith(",") || text.EndsWith(";")) text = text.Substring(0, text.Length - 1).TrimEnd();
            if (!StringList.IsMatch(text)) return;
            var items = new List<string>();
            foreach (Match m in StringItem.Matches(text)) items.Add(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value);
            _lists[key] = items;
        }

        private void ApplyEnvironment(Func<string, string> env)
        {
            foreach (var key in _map.Keys.ToList())
            {
                int dot = key.IndexOf('.');
                string ev = env("XI_" + key.Substring(0, dot).ToUpperInvariant() + "_" + key.Substring(dot + 1).ToUpperInvariant());
                if (ev == null) continue;
                ev = ev.Trim();
                // settings.cpp isNumber: only an all-digit value is a number (std::stod); anything else ("1.5", "-1", "true")
                // stays a string, read as settings::get<T> reads a string. An empty value, which the C++ hands to std::stod
                // and aborts on, stays an empty string here.
                _map[key] = ev.Length > 0 && ev.All(c => c >= '0' && c <= '9')
                    ? double.Parse(ev, NumberStyles.None, CultureInfo.InvariantCulture)
                    : (object)ev;
            }
        }

        // `-- comment` outside of string literals.
        private static string StripComment(string line)
        {
            char quote = '\0';
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quote != '\0') { if (c == quote) quote = '\0'; }
                else if (c == '\'' || c == '"') quote = c;
                else if (c == '-' && i + 1 < line.Length && line[i + 1] == '-') return line.Substring(0, i);
            }
            return line;
        }

        private static bool TryValue(string text, out object value)
        {
            text = text.Trim();
            if (text == "true") { value = true; return true; }
            if (text == "false") { value = false; return true; }
            if (text.Length >= 2 && (text[0] == '\'' || text[0] == '"') && text[text.Length - 1] == text[0])
            {
                value = text.Substring(1, text.Length - 2);
                return true;
            }
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && long.TryParse(text.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var h))
            {
                value = (double)h;
                return true;
            }
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) { value = d; return true; }
            value = null;
            return false;
        }

        public bool Has(string key) => _map.ContainsKey(key);

        public void Set(string key, object value) => _map[key] = value;

        public string GetString(string key, string fallback = "") => _map.TryGetValue(key, out var v) && v is string s ? s : fallback;

        public bool GetBool(string key, bool fallback = false) => _map.TryGetValue(key, out var v) && v is bool b ? b : fallback;

        public double GetDouble(string key, double fallback = 0) => _map.TryGetValue(key, out var v) && v is double d ? d : fallback;

        public int GetInt(string key, int fallback = 0) => (int)GetDouble(key, fallback);

        // ---- settings::get<T> (settings.h getUncached): the C++ conversions between the held bool / double / string and
        // the asked type. A missing key is T(): false, 0, "". A string is truthy unless empty, "false" or "0".

        public T Get<T>(string key)
        {
            _map.TryGetValue(key, out var v);
            object r;
            if (typeof(T) == typeof(bool)) r = AsBool(v);
            else if (typeof(T) == typeof(string)) r = AsString(v);
            else if (typeof(T) == typeof(double)) r = AsDouble(v);
            else if (typeof(T) == typeof(float)) r = (float)AsDouble(v);
            // unsigned: static_cast<unsigned int>(held), then the assignment narrows to T
            else if (typeof(T) == typeof(byte)) r = unchecked((byte)AsUInt(v));
            else if (typeof(T) == typeof(ushort)) r = unchecked((ushort)AsUInt(v));
            else if (typeof(T) == typeof(uint)) r = AsUInt(v);
            // signed: static_cast<int>(held), then the assignment narrows to T
            else if (typeof(T) == typeof(sbyte)) r = unchecked((sbyte)AsInt(v));
            else if (typeof(T) == typeof(short)) r = unchecked((short)AsInt(v));
            else if (typeof(T) == typeof(int)) r = AsInt(v);
            else throw new NotSupportedException($"settings::get<{typeof(T).Name}>");
            return (T)r;
        }

        private static bool Truthy(string s) => s.Length > 0 && s != "false" && s != "0";

        private static bool AsBool(object v) => v switch { bool b => b, double d => d != 0, string s => Truthy(s), _ => false };

        private static double AsDouble(object v) => v switch { bool b => b ? 1 : 0, double d => d, string s => Truthy(s) ? 1 : 0, _ => 0 };

        // a double out of range is undefined behaviour in the C++; MSVC x64 converts through a 64-bit integer
        private static long Truncate(double d) => double.IsNaN(d) || d >= 9.2e18 || d <= -9.2e18 ? long.MinValue : (long)d;

        private static uint AsUInt(object v) => v switch { bool b => b ? 1u : 0u, double d => unchecked((uint)Truncate(d)), string s => Truthy(s) ? 1u : 0u, _ => 0u };

        private static int AsInt(object v) => v switch { bool b => b ? 1 : 0, double d => unchecked((int)Truncate(d)), string s => Truthy(s) ? 1 : 0, _ => 0 };

        // fmt::format("{}", double): the shortest text that reads back the same, no trailing ".0"
        private static string AsString(object v) => v switch
        {
            bool b => b ? "true" : "false",
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            string s => s,
            _ => "",
        };

        // A settings table of strings (login.BANNED_WORDS_LIST: lua["xi"]["settings"]["login"]["BANNED_WORDS_LIST"]); null
        // when no file defines one.
        public IReadOnlyList<string> GetStringList(string key) => _lists.TryGetValue(key, out var l) ? l : null;
    }
}
