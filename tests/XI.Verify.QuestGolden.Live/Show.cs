using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using XI.Map;

// show <key>[#branch] [--all]: a checked-in trace for reading, step by step (quest rework B3b Bastok, task 1b). Each step
// gets its flow line (file line number and text, comment included), then its packets (the opcode, a decoding of the event
// and message packets, the store entry inlined), the Lua returns, the DB changes as the trace writes them and the lookup
// orders. The login step's bundle and the first step's tables are summarised; --all prints every packet and row. Nothing
// is written; no database is needed.
internal static partial class Program
{
    private static int Show(string sel, bool all, string dir = null)
    {
        if (sel == "all" || sel.Length == 0)
        {
            Console.WriteLine("show takes one trace: show <key>[#branch]");
            return 1;
        }
        dir ??= GoldenDir;
        string trace = Path.Combine(dir, sel + ".trace");
        if (!File.Exists(trace))
        {
            Console.WriteLine($"no trace {trace}");
            return 1;
        }
        string flow = Path.Combine(FlowsDir, sel + ".flow");
        Console.Write(ShowText(File.ReadAllText(trace, Encoding.UTF8), File.Exists(flow) ? File.ReadAllText(flow, Encoding.UTF8) : null,
                               PacketStore.Load(dir), all));
        return 0;
    }

    // the flow's statement lines from the login on: (line number, text with its comment, the label a trace step has)
    private static List<(int Line, string Text, string Label)> FlowStepLines(string flowText)
    {
        var list = new List<(int, string, string)>();
        if (flowText == null) return list;
        var lines = flowText.Replace("\r\n", "\n").Split('\n');
        bool steps = false;
        for (int i = 0; i < lines.Length; i++)
        {
            string raw = lines[i];
            // '#' starts a comment at the start of a line or after whitespace (HomePoint#1 keeps its '#'), the parser's rule
            // (Flow.WithoutComment: kill #<id> keeps its id, quest rework P8)
            string code = Flow.WithoutComment(raw).Trim();
            if (code.Length == 0) continue;
            string label = string.Join(" ", code.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
            // main's first login, open or not (login open, quest rework P5)
            if (TraceStep.LoginOf(label) == Flow.MainChar && !label.Contains(':')) steps = true;
            if (steps) list.Add((i + 1, raw.TrimEnd(), label));
        }
        return list;
    }

    internal static string ShowText(string traceText, string flowText, PacketStore store, bool all)
    {
        var sb = new StringBuilder();
        var flowLines = FlowStepLines(flowText);
        int next = 0, stepNo = 0;
        foreach (var raw in traceText.Replace("\r\n", "\n").Split('\n'))
        {
            if (raw.Length == 0) continue;
            if (raw.StartsWith("step "))
            {
                string label = raw.Substring(5);
                stepNo++;
                // the next flow line with this label (an uncovered: line or a setup line in between is skipped)
                int at = flowLines.FindIndex(next, f => f.Label == label);
                string where = at < 0 ? "flow:?" : $"flow:{flowLines[at].Line}";
                if (at >= 0) next = at + 1;
                sb.Append('\n').Append($"step {stepNo}  {where}  ").Append(at >= 0 ? flowLines[at].Text : label).Append('\n');
                continue;
            }
            if (stepNo == 0)
            {
                // the header, as written
                sb.Append(raw).Append('\n');
                continue;
            }
            if (raw == "packets") continue;
            // another char's packets (quest rework P3): packets <name> [@<bundle> (<n>)]
            string who = null;
            if (raw.StartsWith("packets ") && raw.Length > 8 && raw[8] != '@')
            {
                int sp = raw.IndexOf(' ', 8);
                who = sp < 0 ? raw.Substring(8) : raw.Substring(8, sp - 8);
                sb.Append($"  {who}'s packets:\n");
                if (sp < 0) continue;
            }
            if (raw.StartsWith("packets @") || who != null)
            {
                // the login bundle: its packets by opcode, the decoded ones listed
                string hash = raw.Substring(raw.IndexOf('@') + 1, 16);
                var bundle = store.GetBundle(hash) ?? Array.Empty<string>();
                var packets = bundle.Select(store.Get).Where(p => p != null).ToList();
                sb.Append($"  packets: the login bundle @{hash}, {packets.Count} packets: ")
                  .Append(string.Join(", ", packets.GroupBy(p => p.Substring(0, 3)).Select(g => $"{g.Key} x{g.Count()}")))
                  .Append('\n');
                foreach (var p in packets)
                {
                    string what = DecodePacket(p);
                    if (all) sb.Append($"    {p.Substring(0, 3)} {what}{(what.Length > 0 ? "  " : "")}{p.Substring(4)}\n");
                    else if (what.Length > 0) sb.Append($"    {p.Substring(0, 3)} {what}\n");
                }
                continue;
            }
            if (raw.StartsWith("  ") && raw.Contains(" @"))
            {
                string op = raw.Substring(2, 3), hash = raw.Substring(raw.IndexOf('@') + 1);
                string p = store.Get(hash);
                if (p == null)
                {
                    sb.Append($"  {op} @{hash} (not in the store)\n");
                    continue;
                }
                string what = DecodePacket(p);
                sb.Append($"  {op} {what}{(what.Length > 0 ? "  " : "")}{p.Substring(4)}\n");
                continue;
            }
            // a first step's table: its rows, summarised unless --all
            int at2 = raw.IndexOf(" @", StringComparison.Ordinal);
            if (at2 > 0 && !raw.Contains(" - ") && !raw.Contains(" + ") && raw.EndsWith(")"))
            {
                string table = raw.Substring(0, at2), hash = raw.Substring(at2 + 2, 16);
                var rows = store.GetTable(hash) ?? Array.Empty<string>();
                sb.Append($"  {table}: {rows.Length} rows @{hash}\n");
                if (all) foreach (var r in rows) sb.Append($"    {r}\n");
                continue;
            }
            // ret, order, the DB changes: as written
            sb.Append("  ").Append(raw).Append('\n');
        }
        return sb.ToString();
    }

    // the packet's meaning where the layout is known (Events.cs): event start / update / release, NPC messages, item
    // counts; "" for the rest. p is the store's "<OPC>:<hex>", the hex the whole packet with its 4-byte header
    private const string CharIdMark = "<CHARID>";

    // another char's placeholders (quest rework P3): its id <CHARID:<name>>, its name <CHARNAME:<name>>, a 0x028's
    // bit-packed target <CHARID:<name>@<bit>>
    private static readonly System.Text.RegularExpressions.Regex OtherBits = new(@"<CHARID:([a-z][a-z0-9_]*)@(\d+)>");
    private static readonly System.Text.RegularExpressions.Regex OtherChar = new(@"<CHAR(ID|NAME):([a-z][a-z0-9_]*)>");

    internal static string DecodePacket(string p)
    {
        if (p.Length < 4 || p[3] != ':') return "";
        // the normalised char id (<CHARID>, four bytes) is shown as $char (another char's <CHARID:<name>> as
        // $char:<name>); a 0x028's bit-packed char targets (<CHARID@<bit>> after the hex) are named
        string hex = p.Substring(4);
        var charBits = new List<string>();
        foreach (System.Text.RegularExpressions.Match m in OtherBits.Matches(hex)) charBits.Add($"{m.Groups[2].Value} ($char:{m.Groups[1].Value})");
        hex = OtherBits.Replace(hex, "");
        for (int m = hex.IndexOf(Trace.CharBitsMark, StringComparison.Ordinal); m >= 0; m = hex.IndexOf(Trace.CharBitsMark, StringComparison.Ordinal))
        {
            int close = hex.IndexOf('>', m);
            if (close < 0) break;
            charBits.Insert(charBits.Count(b => !b.Contains('$')), hex.Substring(m + Trace.CharBitsMark.Length, close - m - Trace.CharBitsMark.Length));
            hex = hex.Remove(m, close - m + 1);
        }
        // a name is 10 bytes (20 hex), its placeholder shorter: every one replaced first, so the offsets below hold
        hex = hex.Replace("<CHARNAME>", new string('0', 20), StringComparison.Ordinal);
        var charAt = new Dictionary<int, string>();
        for (var m = OtherChar.Match(hex); m.Success; m = OtherChar.Match(hex))
        {
            bool id = m.Groups[1].Value == "ID";
            if (id) charAt[m.Index / 2] = "$char:" + m.Groups[2].Value;
            hex = hex.Substring(0, m.Index) + new string('0', id ? 8 : 20) + hex.Substring(m.Index + m.Length);
        }
        for (int i = hex.IndexOf(CharIdMark, StringComparison.Ordinal); i >= 0; i = hex.IndexOf(CharIdMark, StringComparison.Ordinal))
        {
            charAt[i / 2] = "$char";
            hex = hex.Substring(0, i) + "00000000" + hex.Substring(i + CharIdMark.Length);
        }
        byte[] b;
        try { b = Convert.FromHexString(hex); } catch (FormatException) { return ""; }
        ushort U16(int o) => o + 2 <= b.Length ? BitConverter.ToUInt16(b, o) : (ushort)0;
        uint U32(int o) => o + 4 <= b.Length ? BitConverter.ToUInt32(b, o) : 0u;
        string Entity(int o) => charAt.TryGetValue(o, out var who) ? who : U32(o).ToString();
        string Nums(int from, int count)
        {
            // a char's id among them as $char ($char:<name>)
            var v = Enumerable.Range(0, count).Select(i => charAt.TryGetValue(from + i * 4, out var who) ? who : ((int)U32(from + i * 4)).ToString()).ToList();
            while (v.Count > 0 && v[^1] == "0") v.RemoveAt(v.Count - 1);
            return v.Count == 0 ? "" : $" params [{string.Join(",", v)}]";
        }
        switch (p.Substring(0, 3))
        {
            case "032":
                return $"[event {U16(12)}, entity {Entity(4)}, zone {U16(10)}]";
            case "033":
                return $"[event {U16(4 + S2cEventStr.OffEventPara)} (strings), entity {Entity(4)}]";
            case "034":
                return $"[event {U16(4 + S2cEventNum.OffEventPara)}, entity {Entity(4)}, zone {U16(4 + S2cEventNum.OffEventNum)}{Nums(4 + S2cEventNum.OffNum, 8)}]";
            case "052":
            {
                uint m = U32(4);
                return (m >> 8) != 0 ? $"[event release, mode {m & 0xFF}, event {m >> 8}]" : $"[event release, mode {m & 0xFF}]";
            }
            case "05C":
                return $"[event update{Nums(4, 8)}]";
            case "02A":
                return $"[message {U16(26) & 0x7FFF}, entity {Entity(4)}{Nums(8, 4)}]";
            case "036":
                return $"[message {U16(10) & 0x7FFF}, entity {Entity(4)}]";
            case "028":
                return charBits.Count == 0 ? "" : $"[action, target $char at bit {string.Join(", ", charBits)}]";
            case "01E":
                return b.Length >= 10 ? $"[item count: container {b[8]}, slot {b[9]}, quantity {U32(4)}]" : "";
            default:
                return "";
        }
    }
}
