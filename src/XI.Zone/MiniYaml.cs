using System;
using System.Collections.Generic;
using System.IO;

namespace XI.Zone
{
    // The YAML subset the server's data/*.yaml files use: indentation-nested block maps and block sequences ("- x",
    // "- key: v" items that open a map), with scalar, flow-map ({a: 1, b: [2, 3]}) and flow-sequence ([1, 2]) leaves. No
    // anchors or multi-line scalars; quotes around a scalar are stripped. A map is a
    // Dictionary<string, object> in file order, a sequence a List<object>, and a leaf a string.
    public static class MiniYaml
    {
        public static Dictionary<string, object> Load(string path) => Parse(File.ReadAllText(path));

        public static Dictionary<string, object> Parse(string text)
        {
            var lines = new List<(int indent, string body)>();
            foreach (var raw in text.Split('\n'))
            {
                var line = StripComment(raw).TrimEnd();
                if (line.Trim().Length == 0) continue;
                lines.Add((line.Length - line.TrimStart().Length, line.Trim()));
            }
            int i = 0;
            if (lines.Count == 0) return new Dictionary<string, object>();
            return ParseBlock(lines, ref i, lines[0].indent) as Dictionary<string, object>
                ?? throw new FormatException("yaml: document is not a map");
        }

        // Reads the run of lines at exactly `indent`, which is a sequence when it opens with "- " and a map otherwise.
        private static object ParseBlock(List<(int indent, string body)> lines, ref int i, int indent)
        {
            if (IsItem(lines[i].body))
            {
                var list = new List<object>();
                while (i < lines.Count && lines[i].indent == indent && IsItem(lines[i].body))
                {
                    string rest = lines[i].body.Substring(1).TrimStart();
                    if (IsItem(rest))
                    {
                        // "- - x" opens a nested sequence whose further items sit where the inner dash starts
                        int itemIndent = indent + (lines[i].body.Length - rest.Length);
                        lines[i] = (itemIndent, rest);
                        list.Add(ParseBlock(lines, ref i, itemIndent));
                    }
                    else if (rest.Length > 0 && FindColon(rest) >= 0 && !rest.StartsWith('{'))
                    {
                        // "- key: v" opens a map whose further keys sit where `key` starts
                        int itemIndent = indent + (lines[i].body.Length - rest.Length);
                        lines[i] = (itemIndent, rest);
                        list.Add(ParseMap(lines, ref i, itemIndent));
                    }
                    else
                    {
                        i++;
                        list.Add(rest.Length == 0 ? ParseChild(lines, ref i, indent) : Scalar(rest));
                    }
                }
                return list;
            }
            return ParseMap(lines, ref i, indent);
        }

        private static Dictionary<string, object> ParseMap(List<(int indent, string body)> lines, ref int i, int indent)
        {
            var map = new Dictionary<string, object>();
            while (i < lines.Count && lines[i].indent == indent && !IsItem(lines[i].body))
            {
                var (key, value) = SplitPair(lines[i].body);
                i++;
                map[key] = value.Length == 0 ? ParseChild(lines, ref i, indent) : Scalar(value);
            }
            if (i < lines.Count && lines[i].indent > indent) throw new FormatException($"yaml: unexpected indent at '{lines[i].body}'");
            return map;
        }

        // The value of a key with nothing after its colon: a deeper block, or a sequence at the key's own indent
        // (YAML lets "key:\n- a" sit flush), or null when nothing follows.
        private static object ParseChild(List<(int indent, string body)> lines, ref int i, int parentIndent)
        {
            if (i >= lines.Count) return null;
            if (lines[i].indent > parentIndent) return ParseBlock(lines, ref i, lines[i].indent);
            if (lines[i].indent == parentIndent && IsItem(lines[i].body)) return ParseBlock(lines, ref i, parentIndent);
            return null;
        }

        private static bool IsItem(string body) => body == "-" || body.StartsWith("- ");

        private static object Scalar(string v)
        {
            if (v.StartsWith('{')) return ParseFlowMap(v);
            if (v.StartsWith('[')) return ParseFlowSeq(v);
            return Unquote(v);
        }

        private static string Unquote(string v) =>
            v.Length >= 2 && (v[0] == '"' || v[0] == '\'') && v[^1] == v[0] ? v.Substring(1, v.Length - 2) : v;

        private static string StripComment(string line)
        {
            // a '#' opens a comment only at the start or after whitespace, and never inside quotes
            char quote = '\0';
            for (int k = 0; k < line.Length; k++)
            {
                char c = line[k];
                if (quote != '\0') { if (c == quote) quote = '\0'; continue; }
                if (c == '"' || c == '\'') quote = c;
                else if (c == '#' && (k == 0 || char.IsWhiteSpace(line[k - 1]))) return line.Substring(0, k);
            }
            return line;
        }

        // the ':' that separates a key: followed by a space or the end, outside quotes
        private static int FindColon(string s)
        {
            char quote = '\0';
            for (int k = 0; k < s.Length; k++)
            {
                char c = s[k];
                if (quote != '\0') { if (c == quote) quote = '\0'; continue; }
                if (c == '"' || c == '\'') quote = c;
                else if (c == ':' && (k + 1 == s.Length || s[k + 1] == ' ')) return k;
            }
            return -1;
        }

        private static (string, string) SplitPair(string s)
        {
            int colon = FindColon(s);
            if (colon < 0) throw new FormatException($"yaml: expected 'key: value' in '{s}'");
            return (Unquote(s.Substring(0, colon).Trim()), s.Substring(colon + 1).Trim());
        }

        private static Dictionary<string, object> ParseFlowMap(string s)
        {
            if (!s.EndsWith('}')) throw new FormatException($"yaml: unterminated flow map '{s}'");
            var map = new Dictionary<string, object>();
            foreach (var part in SplitFlow(s.Substring(1, s.Length - 2)))
            {
                var (k, v) = SplitPair(part);
                map[k] = Scalar(v);
            }
            return map;
        }

        private static List<object> ParseFlowSeq(string s)
        {
            if (!s.EndsWith(']')) throw new FormatException($"yaml: unterminated flow sequence '{s}'");
            var list = new List<object>();
            foreach (var part in SplitFlow(s.Substring(1, s.Length - 2))) list.Add(Scalar(part));
            return list;
        }

        // the non-empty items between the commas that sit outside nested brackets and quotes
        private static List<string> SplitFlow(string s)
        {
            var parts = new List<string>();
            int depth = 0, start = 0;
            char quote = '\0';
            for (int k = 0; k <= s.Length; k++)
            {
                char c = k < s.Length ? s[k] : ',';
                if (quote != '\0') { if (c == quote) quote = '\0'; continue; }
                if (c == '"' || c == '\'') quote = c;
                else if (c == '[' || c == '{') depth++;
                else if (c == ']' || c == '}') depth--;
                else if (c == ',' && depth == 0)
                {
                    var part = s.Substring(start, k - start).Trim();
                    if (part.Length > 0) parts.Add(part);
                    start = k + 1;
                }
            }
            return parts;
        }

        public static Dictionary<string, object> Map(this Dictionary<string, object> m, string key) =>
            m.TryGetValue(key, out var v) && v is Dictionary<string, object> d ? d : throw new KeyNotFoundException($"yaml: missing map '{key}'");

        public static Dictionary<string, object> MapOrNull(this Dictionary<string, object> m, string key) =>
            m.TryGetValue(key, out var v) ? v as Dictionary<string, object> : null;

        public static List<object> List(this Dictionary<string, object> m, string key) =>
            m.TryGetValue(key, out var v) && v is List<object> l ? l : new List<object>();

        public static string Str(this Dictionary<string, object> m, string key, string fallback = null) =>
            m.TryGetValue(key, out var v) && v is string s ? s : fallback;
    }
}
