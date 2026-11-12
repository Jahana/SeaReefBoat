using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Persistence;

// A flow's setup item lines as inventory rows (carry-harness-2, the user's ruling of 2026-11-04: enforced for new
// flows, the existing lines exempted by a checked-in list, no trace changed). `item X x<n>` above the item's stack size
// is split into rows of a full stack and the rest, as the game holds them (one row of n for a stack-1 item made a state
// the game cannot hold, and a trade then took the wrong count); a second row of a Rare item (n item lines of it, or
// x<n> with n above 1: every Rare item stacks 1) is refused, the game forbids it. The lines of
// tests/golden/item-stack-exempt.txt keep the one row of n and the second Rare row they had, so no checked-in trace
// changes; an entry whose line no longer does either is stale and fails the run.
internal static partial class Program
{
    // item_basic's flag of a Rare item (ITEM_FLAG_RARE)
    private const int ItemFlagRare = 0x8000;

    private static string ItemStackExemptFile => Path.Combine(GoldenDir, "item-stack-exempt.txt");

    // an inventory row a setup item line writes, and the line
    internal sealed record ItemRow(int Item, uint Qty, Statement Source);

    // an item line that breaks a stack rule, and which: OverStack (x<n> above the stack size) or SecondRare
    internal sealed record ItemStackIssue(Statement Source, bool OverStack, bool SecondRare, int Stack);

    // the rows of one char's setup item lines, in order: an exempt line one row of its count as before; another line
    // split by the stack size. issues gets every line that breaks a rule, exempt or not (the gate tells them apart)
    internal static List<ItemRow> ItemRows(IReadOnlyList<Statement> items, Func<int, (int Stack, bool Rare)> info,
                                           Func<Statement, bool> exempt, List<ItemStackIssue> issues)
    {
        var rows = new List<ItemRow>();
        var held = new Dictionary<int, int>();
        foreach (var st in items)
        {
            var (stack, rare) = info(st.Value);
            stack = Math.Max(stack, 1);
            bool over = st.Amount > stack;
            int before = rows.Count;
            if (over && !exempt(st))
            {
                for (int left = st.Amount; left > 0; left -= stack)
                    rows.Add(new ItemRow(st.Value, (uint)Math.Min(left, stack), st));
            }
            else rows.Add(new ItemRow(st.Value, (uint)st.Amount, st));
            held.TryGetValue(st.Value, out int had);
            held[st.Value] = had + rows.Count - before;
            bool secondRare = rare && held[st.Value] > 1;
            if (over || secondRare) issues?.Add(new ItemStackIssue(st, over, secondRare, stack));
        }
        return rows;
    }

    // the exempt list: <flow>:<line> <the line's statement, as its step label>, '#' comments; problems for a malformed
    // line or a second entry of one line
    internal static Dictionary<string, string> ParseItemStackExempt(string text, List<string> problems)
    {
        var exempt = new Dictionary<string, string>(StringComparer.Ordinal);
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (int n = 0; n < lines.Length; n++)
        {
            string line = lines[n].Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            int space = line.IndexOf(' ');
            string at = space < 0 ? line : line.Substring(0, space);
            int colon = at.LastIndexOf(':');
            if (space < 0 || colon <= 0 || !int.TryParse(at.AsSpan(colon + 1), out _))
            {
                problems.Add($"item-stack-exempt.txt:{n + 1}: an entry is <flow>:<line> <statement>: {line}");
                continue;
            }
            // the statement, up to the entry's comment (an item line holds no '#')
            string statement = line.Substring(space + 1);
            int hash = statement.IndexOf(" #", StringComparison.Ordinal);
            if (hash >= 0) statement = statement.Substring(0, hash);
            if (!exempt.TryAdd(at, statement.Trim()))
                problems.Add($"item-stack-exempt.txt:{n + 1}: a second entry for {at}");
        }
        return exempt;
    }

    // the key of a flow's line in the exempt list
    internal static string ItemStackKey(Flow f, Statement st) => $"{f.Name}:{st.Line}";

    // the gate over every flow: a line that breaks a rule, not exempt, and the split cannot mend (a second Rare row) is
    // a failure; an exempt entry whose line breaks no rule (or names no setup item line, or another statement) is
    // stale. only (harness-speed, a selection's run: LoadSelected): the flows are a selection's, and an entry is
    // checked only when it names one of them. Each problem is one flow's (its lines, or an entry <flow>:<line>, used by
    // that flow's lines alone), so the result is exactly the full gate's problems of those flows
    internal static List<string> ItemStackProblems(IReadOnlyList<Flow> flows, Func<int, (int Stack, bool Rare)> info,
                                                   IReadOnlyDictionary<string, string> exempt,
                                                   IReadOnlySet<string> only = null)
    {
        var problems = new List<string>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in flows)
            for (int a = 0; a < f.Chars.Count; a++)
            {
                var issues = new List<ItemStackIssue>();
                ItemRows(SetupItems(f, a), info, st => exempt.ContainsKey(ItemStackKey(f, st)), issues);
                foreach (var i in issues)
                {
                    string key = ItemStackKey(f, i.Source);
                    if (exempt.TryGetValue(key, out var label))
                    {
                        if (label == i.Source.Label) used.Add(key);
                        continue;
                    }
                    if (i.SecondRare)
                        problems.Add($"{Path.GetFileName(f.File)}:{i.Source.Line}: [{i.Source.Label}] " +
                                     "a second row of a Rare item (the game holds one): give it once");
                }
            }
        foreach (var (key, label) in exempt)
            if (!used.Contains(key) && (only == null || only.Contains(key.Substring(0, key.LastIndexOf(':')))))
                problems.Add($"item-stack-exempt.txt: {key} [{label}] is stale: that line is no setup item line " +
                             "above its stack size or a second Rare row (drop the entry)");
        return problems;
    }

    // a char's setup item lines (its rows, before its login), in order
    internal static List<Statement> SetupItems(Flow f, int actor) =>
        f.Setup.Where(st => st.Actor == actor && st.Verb == "item").ToList();

    // item_basic's stack size and Rare flag of an item, read once
    private static readonly Dictionary<int, (int Stack, bool Rare)> itemStacks_ = new();
    private static (int Stack, bool Rare) ItemStack(int item)
    {
        if (itemStacks_.TryGetValue(item, out var known)) return known;
        var r = Sql(db, "SELECT CAST(stackSize AS SIGNED) AS s, CAST(flags AS SIGNED) AS fl FROM item_basic " +
                        "WHERE itemid = ?", BoundValue.Of((ushort)item));
        var info = r != null && r.Next()
            ? ((int)r.Get<long>("s"), (r.Get<long>("fl") & ItemFlagRare) != 0)
            : (1, false);
        itemStacks_[item] = info;
        return info;
    }

    // the exempt list as checked in (none: empty)
    private static Dictionary<string, string> itemStackExempt_;
    private static Dictionary<string, string> ItemStackExempt(List<string> problems)
    {
        if (itemStackExempt_ != null) return itemStackExempt_;
        string file = ItemStackExemptFile;
        itemStackExempt_ = ParseItemStackExempt(File.Exists(file) ? File.ReadAllText(file) : "", problems);
        return itemStackExempt_;
    }

    // the exempt list re-keyed (item-stacks): each setup item line that breaks a rule and matches an entry by its flow
    // and its statement (an entry whose line moved: a line added above it) is an entry at its line now, in the flows'
    // order. The list never grows: a second Rare row matching no entry is a failure (a new flow's: give the item
    // once), an x<n> above the stack matching none a note (a new flow's line, split by the stack size: no entry
    // needed); an entry no line matches any more is dropped, with a note. The entries as the file writes them
    internal static List<string> RekeyItemStackExempt(IReadOnlyList<Flow> flows, Func<int, (int Stack, bool Rare)> info,
                                                     IReadOnlyDictionary<string, string> exempt, List<string> failures,
                                                     List<string> notes)
    {
        // the entries left to match: (flow, statement) -> how many, and their old keys in the list's order
        var left = new Dictionary<(string Flow, string Label), List<string>>();
        foreach (var (key, label) in exempt)
        {
            var pair = (key.Substring(0, key.LastIndexOf(':')), label);
            if (!left.TryGetValue(pair, out var keys)) left[pair] = keys = new List<string>();
            keys.Add(key);
        }
        var entries = new List<string>();
        foreach (var f in flows)
            for (int a = 0; a < f.Chars.Count; a++)
            {
                var issues = new List<ItemStackIssue>();
                ItemRows(SetupItems(f, a), info, _ => true, issues);
                foreach (var i in issues)
                {
                    string key = ItemStackKey(f, i.Source);
                    if (left.TryGetValue((f.Name, i.Source.Label), out var keys) && keys.Count > 0)
                    {
                        string was = keys[0];
                        keys.RemoveAt(0);
                        if (was != key) notes.Add($"{was} -> {key} [{i.Source.Label}]: re-keyed (its line moved)");
                        string why = string.Join("; ", new[]
                        {
                            i.OverStack ? $"x{i.Source.Amount} above the stack size {i.Stack}" : null,
                            i.SecondRare ? "a second row of a Rare item" : null,
                        }.Where(w => w != null));
                        entries.Add($"{key} {i.Source.Label}  # {why}");
                    }
                    else if (i.SecondRare)
                        failures.Add($"{key} [{i.Source.Label}]: a second row of a Rare item on no entry of the list " +
                                     "(the list never grows: give the item once)");
                    else notes.Add($"{key} [{i.Source.Label}]: x{i.Source.Amount} above the stack size {i.Stack}, " +
                                   "on no entry: split by the stack size");
                }
            }
        foreach (var keys in left.Values)
            foreach (var was in keys)
                notes.Add($"{was} [{exempt[was]}]: no line breaks a rule there any more: dropped");
        return entries;
    }

    // item-stacks: the exempt list re-keyed (RekeyItemStackExempt) and written back, its header comment kept; with a
    // failure nothing is written
    private static void PrintItemStacks(IReadOnlyList<Flow> flows)
    {
        var problems = new List<string>();
        var exempt = ItemStackExempt(problems);
        foreach (var p in problems) Fail("item stacks", p);
        if (problems.Count > 0) return;
        var failures = new List<string>();
        var notes = new List<string>();
        var entries = RekeyItemStackExempt(flows, ItemStack, exempt, failures, notes);
        foreach (var n in notes) Console.WriteLine($"item-stacks: {n}");
        foreach (var p in failures) Fail("item stacks", p);
        if (failures.Count > 0) return;
        string file = ItemStackExemptFile;
        var header = File.Exists(file)
            ? File.ReadAllText(file).Replace("\r\n", "\n").Split('\n')
                .TakeWhile(l => l.StartsWith('#')).ToList()
            : new List<string>();
        File.WriteAllText(file, string.Join("\n", header.Concat(entries)) + "\n");
        Console.WriteLine($"item-stacks: {entries.Count} entries written to {file}");
    }

    // the gate's run over the loaded flows: false (each problem failed) when any. A selection's run (harness-speed: all
    // holds stubs, LoadSelected) checks its picked flows' part (ItemStackProblems' only) and the list's own form; the
    // rest is the flow files' and the list's, not the code's, and every `replay all` checks it whole
    private static bool ItemStackGate(IReadOnlyList<Flow> flows)
    {
        var problems = new List<string>();
        var exempt = ItemStackExempt(problems);
        var parsed = flows.Where(f => !f.Stub).ToList();
        var only = parsed.Count < flows.Count ? parsed.Select(f => f.Name).ToHashSet(StringComparer.Ordinal) : null;
        problems.AddRange(ItemStackProblems(parsed, ItemStack, exempt, only));
        foreach (var p in problems) Fail("item stacks", p);
        return problems.Count == 0;
    }
}
