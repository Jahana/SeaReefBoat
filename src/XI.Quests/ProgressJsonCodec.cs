// XI.Quests -- the `progress` JSON of the char_quest_state / char_mission_state rows: the soft vars as one object with
// its keys in ordinal order, plus, when a soft var or a quest's step carries an expiry, one "@expiry" key holding those
// expiries (unix seconds) the same way. A row without expiries writes exactly what it wrote before expiries existed.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace XI.Quests
{
    public static class ProgressJsonCodec
    {
        public const string ExpiryKey = "@expiry";

        // null when there is nothing to write
        public static string Write(IReadOnlyDictionary<string, long> soft, IReadOnlyDictionary<string, uint> expiry)
        {
            bool hasSoft = soft != null && soft.Count > 0, hasExpiry = expiry != null && expiry.Count > 0;
            if (!hasSoft && !hasExpiry) return null;
            var entries = new List<KeyValuePair<string, string>>();
            if (hasSoft)
                foreach (var kv in soft) entries.Add(new KeyValuePair<string, string>(kv.Key, kv.Value.ToString(CultureInfo.InvariantCulture)));
            if (hasExpiry)
            {
                var inner = new List<KeyValuePair<string, string>>();
                foreach (var kv in expiry) inner.Add(new KeyValuePair<string, string>(kv.Key, kv.Value.ToString(CultureInfo.InvariantCulture)));
                entries.Add(new KeyValuePair<string, string>(ExpiryKey, Obj(inner)));
            }
            return Obj(entries);
        }

        static string Obj(List<KeyValuePair<string, string>> entries)
        {
            entries.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            var sb = new StringBuilder();
            sb.Append('{');
            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(Escape(entries[i].Key)).Append("\":").Append(entries[i].Value);
            }
            sb.Append('}');
            return sb.ToString();
        }

        static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        // the inverse of Write: soft and expiry are cleared, then filled; null or empty reads as no vars
        public static void Read(string json, Dictionary<string, long> soft, Dictionary<string, uint> expiry)
        {
            soft.Clear();
            expiry.Clear();
            if (string.IsNullOrEmpty(json)) return;
            using var doc = JsonDocument.Parse(json);
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (p.Name == ExpiryKey && p.Value.ValueKind == JsonValueKind.Object)
                    foreach (var e in p.Value.EnumerateObject()) expiry[e.Name] = e.Value.GetUInt32();
                else
                    soft[p.Name] = p.Value.GetInt64();
            }
        }
    }
}
