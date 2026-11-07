using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

// A golden trace (docs/superpowers/specs/2026-10-21-quest-rework-b3a-design.md, section 5): the header (the flow's
// name, the seed, the flow's hash (Flow.Hash), the recorder, the commit its Lua came from, the world hashes, the masks)
// and one block per step. A step holds the packets the char got (each by the hash of its normalised hex in
// tests/golden/packets.store; a login step's as one bundle there), the Lua returns, and the char's state in the database
// after it: the rows of chars and of every table with a charid column (Harness.RecordStep), plus the order of each
// lookup list the flow names. The first step writes every table in full, each as one store entry of its rows (shared by
// the traces whose table starts the same); each later step writes only what changed (a table as - / + rows, a
// single-row table's blob column of unchanged length as its changed byte runs, an order when it moved). A reader
// rebuilds the full state of every step, so a replay compares whole states. UTF-8, LF; written the same way every time,
// so a re-record of an unchanged flow is byte-identical.

// a byte range of an opcode's packets that varies for a reason the harness can't pin, written as "**"
internal sealed record Mask(int Opcode, int Offset, int Length, string Reason)
{
    public string Line => $"mask 0x{Opcode:X3} {Offset} {Length} {Reason}";

    public static Mask Parse(string rest)
    {
        var p = rest.Split(' ', 4);
        if (p.Length < 4 || !p[0].StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            throw new FormatException($"mask needs '0x<opcode> <offset> <length> <reason>': {rest}");
        return new Mask(int.Parse(p[0].Substring(2), NumberStyles.HexNumber), int.Parse(p[1]), int.Parse(p[2]), p[3]);
    }
}

// the char's state after a step, and what the step sent and returned
internal sealed class TraceStep
{
    // the label of the step whose packets a trace writes as one bundle (main's login; a named char's is "<name>: login")
    public const string LoginLabel = "login";
    // another char's table in Sets: <name>:<table>
    public const string CharTableMark = ":";
    private static readonly SortedSet<string> NoRows = new(StringComparer.Ordinal);

    public string Label;
    // main's packets
    public readonly List<string> Packets = new();
    // each other char's packets (quest rework P3), in the trace's char order; a char that got none is absent
    public readonly List<(string Name, List<string> Packets)> Others = new();

    // the char's packets (main: Packets), empty when it got none
    public IReadOnlyList<string> PacketsOf(string name) =>
        name == Flow.MainChar ? Packets : Others.FirstOrDefault(o => o.Name == name).Packets ?? new List<string>();

    // the char whose login the step is (its packets one bundle), null when it is no login: login and main: login are
    // main's, <name>: login that char's; login open (quest rework P5: the zone-in's events left open) is a login too
    public static string LoginOf(string label)
    {
        const string open = " open";
        if (label.EndsWith(open, StringComparison.Ordinal)) label = label.Substring(0, label.Length - open.Length);
        if (label == LoginLabel) return Flow.MainChar;
        // <name>: login, one prefix word
        int colon = label.Length - LoginLabel.Length - 2;
        return colon > 0 && label.EndsWith(": " + LoginLabel, StringComparison.Ordinal) && label.IndexOf(' ') == colon + 1 ? label.Substring(0, colon) : null;
    }

    // the char a table key of Sets is of (main: null) and the table's name
    public static (string Char, string Table) TableKey(string key)
    {
        int at = key.IndexOf(CharTableMark, StringComparison.Ordinal);
        return at < 0 ? (null, key) : (key.Substring(0, at), key.Substring(at + 1));
    }
    // the char's rows by table name, in name order (Harness.RecordStep fills them); a table with no rows may be absent
    public readonly SortedDictionary<string, SortedSet<string>> Sets = new(StringComparer.Ordinal);
    public string Ret = "";
    // "<zone>/<second>/<third>" -> the container ids, in the flow's list order
    public readonly List<(string List, string Ids)> Orders = new();

    // the table's rows, created on demand
    public SortedSet<string> Set(string table)
    {
        if (!Sets.TryGetValue(table, out var set)) Sets[table] = set = new SortedSet<string>(StringComparer.Ordinal);
        return set;
    }

    // the table's rows, empty when it has none
    public SortedSet<string> Rows(string table) => Sets.TryGetValue(table, out var set) ? set : NoRows;

    // the tables either step has rows in, in name order
    public static IEnumerable<string> Tables(TraceStep a, TraceStep b) =>
        (a?.Sets.Keys ?? Enumerable.Empty<string>()).Union(b.Sets.Keys).OrderBy(t => t, StringComparer.Ordinal);

    // the next step's starting state: this one's, no packets, no returns
    public TraceStep Carry(string label)
    {
        var n = new TraceStep { Label = label };
        foreach (var (table, rows) in Sets) n.Sets[table] = new SortedSet<string>(rows, StringComparer.Ordinal);
        n.Orders.AddRange(Orders);
        return n;
    }
}

// the normalised packets of every trace, each once, by hash: "<hash> <OPC>:<hex>" lines; the login bundles, each a list
// of packet hashes, as "<hash> @<hash>,<hash>..." lines; and the first steps' tables, each its rows, as
// "<hash> #<row>\t<row>..." lines (a row never holds a tab: RenderValue escapes it). The '@' and '#' mark a bundle and a
// table: a packet line starts with its opcode. All in hash order, one line an entry (merge=union keeps working)
internal sealed class PacketStore
{
    public const string FileName = "packets.store";
    private readonly Dictionary<string, string> byHash_ = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> bundles_ = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> tables_ = new(StringComparer.Ordinal);
    private static readonly Regex Ref = new(@"^  [0-9A-F]{3} @([0-9a-f]{16})$", RegexOptions.Multiline);
    private static readonly Regex BundleRef = new(@"^packets (?:[a-z][a-z0-9_]* )?@([0-9a-f]{16}) \(\d+\)$", RegexOptions.Multiline);
    private static readonly Regex TableRef = new(@"^(?!packets )\S+ @([0-9a-f]{16}) \(\d+\)$", RegexOptions.Multiline);

    // the first 16 hex characters of the sha256 of the text (a normalised packet, a bundle's hashes)
    public static string Hash(string packet) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(packet))).Substring(0, 16).ToLowerInvariant();

    public static PacketStore Load(string dir)
    {
        var s = new PacketStore();
        string path = Path.Combine(dir, FileName);
        if (!File.Exists(path)) return s;
        foreach (var l in File.ReadAllText(path, Encoding.UTF8).Split('\n'))
        {
            if (l.Length == 0) continue;
            int sp = l.IndexOf(' ');
            if (sp != 16) throw new FormatException($"{path}: not a store line: {l}");
            string hash = l.Substring(0, 16), text = l.Substring(17);
            var (kind, into) = text.StartsWith('@') ? ("bundle", s.bundles_) : text.StartsWith('#') ? ("table", s.tables_) : ("packet", s.byHash_);
            if (kind != "packet") text = text.Substring(1);
            // a hand-edited or damaged line fails here, not as a packet difference
            if (Hash(text) != hash) throw new FormatException($"{path}: {kind} {hash} does not hash to its key (edited?)");
            into[hash] = text;
        }
        return s;
    }

    public string Add(string packet)
    {
        string h = Hash(packet);
        if (byHash_.TryGetValue(h, out var had) && had != packet) throw new InvalidOperationException($"packet hash collision {h}: {had} / {packet}");
        byHash_[h] = packet;
        return h;
    }

    public string Get(string hash) => byHash_.TryGetValue(hash, out var p) ? p : null;

    // the packets added, and their hashes by ',' as a bundle: the bundle's hash
    public string AddBundle(IEnumerable<string> packets)
    {
        string text = string.Join(",", packets.Select(Add));
        string h = Hash(text);
        if (bundles_.TryGetValue(h, out var had) && had != text) throw new InvalidOperationException($"bundle hash collision {h}: {had} / {text}");
        bundles_[h] = text;
        return h;
    }

    // the bundle's packet hashes, null when the store has no such bundle
    public string[] GetBundle(string hash) => bundles_.TryGetValue(hash, out var b) ? b.Split(',') : null;

    // a table's rows (in their order) as an entry: its hash
    public string AddTable(IEnumerable<string> rows)
    {
        string text = string.Join("\t", rows);
        string h = Hash(text);
        if (tables_.TryGetValue(h, out var had) && had != text) throw new InvalidOperationException($"table hash collision {h}: {had} / {text}");
        tables_[h] = text;
        return h;
    }

    // the table entry's rows, null when the store has no such entry
    public string[] GetTable(string hash) => tables_.TryGetValue(hash, out var t) ? t.Split('\t') : null;

    // the store rewritten as the bundles, tables and packets the traces in the folder reference (stale ones gone); the
    // missing hashes when a trace references one the store lacks
    public List<string> Save(string dir)
    {
        var keep = new SortedSet<string>(StringComparer.Ordinal);
        var keepBundles = new SortedSet<string>(StringComparer.Ordinal);
        var keepTables = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var t in Directory.GetFiles(dir, "*.trace"))
        {
            string text = File.ReadAllText(t, Encoding.UTF8);
            foreach (Match m in Ref.Matches(text)) keep.Add(m.Groups[1].Value);
            foreach (Match m in BundleRef.Matches(text)) keepBundles.Add(m.Groups[1].Value);
            foreach (Match m in TableRef.Matches(text)) keepTables.Add(m.Groups[1].Value);
        }
        var missing = keepBundles.Where(h => !bundles_.ContainsKey(h)).Concat(keepTables.Where(h => !tables_.ContainsKey(h))).ToList();
        foreach (var h in keepBundles.Where(bundles_.ContainsKey)) keep.UnionWith(GetBundle(h));
        missing.AddRange(keep.Where(h => !byHash_.ContainsKey(h)));
        var lines = keep.Where(byHash_.ContainsKey).Select(h => $"{h} {byHash_[h]}")
            .Concat(keepBundles.Where(bundles_.ContainsKey).Select(h => $"{h} @{bundles_[h]}"))
            .Concat(keepTables.Where(tables_.ContainsKey).Select(h => $"{h} #{tables_[h]}"))
            .OrderBy(l => l, StringComparer.Ordinal);
        var sb = new StringBuilder();
        foreach (var l in lines) sb.Append(l).Append('\n');
        File.WriteAllBytes(Path.Combine(dir, FileName), new UTF8Encoding(false).GetBytes(sb.ToString()));
        return missing;
    }
}

internal sealed class Trace
{
    public string Name;
    public int Seed;
    public string FlowSha;
    public string Recorder;
    // the commit whose Lua the trace was recorded from (record --lua-at; HEAD resolved), null when not known
    public string LuaAt;
    // the world tables' hashes at the start of the run (Harness.WorldHash), in the order written
    public readonly List<(string Table, string Hash)> World = new();
    // the single-row tables' blob columns ("table.column", the header's blobs line): a same-length change to one is
    // written as its changed byte runs
    public readonly SortedSet<string> BlobColumns = new(StringComparer.Ordinal);
    public readonly List<Mask> Masks = new();
    // the flow's chars after main (quest rework P3), in its order: the header's "char <name>" lines; none for one char
    public readonly List<string> Chars = new();
    public readonly List<TraceStep> Steps = new();

    // a step's table keys in the order a trace writes them: main's tables, then each other char's, each by name
    private IEnumerable<string> OrderedTables(TraceStep a, TraceStep b) =>
        TraceStep.Tables(a, b).OrderBy(k => TraceStep.TableKey(k).Char is { } who ? Chars.IndexOf(who) + 1 : 0)
            .ThenBy(k => TraceStep.TableKey(k).Table, StringComparer.Ordinal);

    // the trace's text; its packets go to the store
    public string Write(PacketStore store)
    {
        var sb = new StringBuilder();
        void Line(string l) => sb.Append(l).Append('\n');
        Line($"# golden trace: {Name}");
        Line($"seed {Seed}");
        Line($"flow {FlowSha}");
        Line($"recorder {Recorder}");
        if (LuaAt != null) Line($"lua-at {LuaAt}");
        foreach (var (table, hash) in World) Line($"world {table} {hash}");
        if (BlobColumns.Count > 0) Line($"blobs {string.Join(" ", BlobColumns)}");
        foreach (var m in Masks) Line(m.Line);
        foreach (var who in Chars) Line($"char {who}");
        TraceStep prev = null;
        foreach (var st in Steps)
        {
            Line($"step {st.Label}");
            // main's packets (always a line), then each other char's that got any: a login step's packets of the char
            // logging in are one bundle
            string login = TraceStep.LoginOf(st.Label);
            void Packets(string who, IReadOnlyList<string> packets)
            {
                string head = who == Flow.MainChar ? "packets" : $"packets {who}";
                if (login == who && packets.Count > 0) Line($"{head} @{store.AddBundle(packets)} ({packets.Count})");
                else
                {
                    Line(head);
                    foreach (var p in packets) Line($"  {p.Substring(0, p.IndexOf(':'))} @{store.Add(p)}");
                }
            }
            Packets(Flow.MainChar, st.Packets);
            foreach (var who in Chars)
                if (st.PacketsOf(who).Count > 0) Packets(who, st.PacketsOf(who));
            foreach (var table in OrderedTables(prev, st))
            {
                var b = st.Rows(table);
                // the first step: each table one store entry
                if (prev == null)
                {
                    if (b.Count > 0) Line($"{table} @{store.AddTable(b)} ({b.Count})");
                    continue;
                }
                var a = prev.Rows(table);
                var gone = a.Where(r => !b.Contains(r)).ToList();
                var come = b.Where(r => !a.Contains(r)).ToList();
                var runs = new List<string>();
                foreach (var r in gone.ToList())
                {
                    int eq = r.IndexOf('=');
                    if (eq < 0 || !BlobColumns.Contains($"{TraceStep.TableKey(table).Table}.{r.Substring(0, eq)}")) continue;
                    string to = come.FirstOrDefault(n => n.StartsWith(r.Substring(0, eq + 1), StringComparison.Ordinal));
                    string av = r.Substring(eq + 1), bv = to?.Substring(eq + 1);
                    if (bv == null || av.Length != bv.Length || !IsHex(av) || !IsHex(bv)) continue;
                    runs.Add($"{table} {r.Substring(0, eq)} {string.Join(" ", Runs(Convert.FromHexString(av), Convert.FromHexString(bv)))}");
                    gone.Remove(r);
                    come.Remove(to);
                }
                foreach (var r in gone) Line($"{table} - {r}");
                foreach (var r in come) Line($"{table} + {r}");
                foreach (var r in runs) Line(r);
            }
            if (st.Ret.Length > 0) Line($"ret {st.Ret}");
            foreach (var (list, ids) in st.Orders)
                if (prev == null || prev.Orders.FirstOrDefault(o => o.List == list).Ids != ids) Line($"order {list}={ids}");
            prev = st;
        }
        return sb.ToString();
    }

    // whether the text reads as hex bytes (a rendered blob does, null does not)
    private static bool IsHex(string s) => s.Length % 2 == 0 && s.All(Uri.IsHexDigit);

    // the changed bytes of b against a (same length) as +<offset>:<hex> runs
    private static IEnumerable<string> Runs(byte[] a, byte[] b)
    {
        for (int i = 0; i < b.Length;)
        {
            if (a[i] == b[i]) { i++; continue; }
            int j = i;
            while (j < b.Length && a[j] != b[j]) j++;
            yield return $"+{i}:{Convert.ToHexString(b, i, j - i)}";
            i = j;
        }
    }

    // the runs (+<offset>:<hex> ...) applied to a copy of the bytes
    private static byte[] ApplyRuns(byte[] old, string runs, string where)
    {
        var b = (byte[])old.Clone();
        foreach (var run in runs.Split(' '))
        {
            int colon = run.IndexOf(':');
            if (!run.StartsWith('+') || colon < 0) throw new FormatException($"{where}: a blob change is +<offset>:<hex>: {run}");
            int off = int.Parse(run.AsSpan(1, colon - 1), CultureInfo.InvariantCulture);
            var bytes = Convert.FromHexString(run.Substring(colon + 1));
            if (off + bytes.Length > b.Length) throw new FormatException($"{where}: a blob change past the end: {run}");
            bytes.CopyTo(b, off);
        }
        return b;
    }

    public string Save(string path, PacketStore store)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string text = Write(store);
        File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(text));
        return text;
    }

    // store null: the header only
    public static Trace Read(string path, PacketStore store) => Parse(File.ReadAllText(path, Encoding.UTF8), path, store);

    private static readonly Regex WorldLine = new(@"^(\S+) ([0-9a-f]{16})$");
    // the rest of a "packets" or table line that names a store entry: @<hash> (<count>)
    private static readonly Regex EntryLine = new(@"^@([0-9a-f]{16}) \((\d+)\)$");

    public static Trace Parse(string text, string file, PacketStore store)
    {
        var t = new Trace();
        TraceStep st = null;
        // the packet lines' list: main's or another char's
        List<string> inPackets = null;
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (int n = 0; n < lines.Length; n++)
        {
            string l = lines[n];
            if (l.Length == 0) continue;
            string where = $"{file}:{n + 1}";
            // no store: the header only (the masks a re-record keeps)
            if (store == null && l.StartsWith("step ", StringComparison.Ordinal)) break;
            if (l.StartsWith("  ", StringComparison.Ordinal))
            {
                if (inPackets == null) throw new FormatException($"{where}: a packet line outside packets");
                var p = l.Substring(2).Split(" @");
                if (p.Length != 2) throw new FormatException($"{where}: a packet line is <OPC> @<hash>");
                string packet = store.Get(p[1]) ?? throw new FormatException($"{where}: packet {p[1]} is not in the store");
                inPackets.Add(packet);
                continue;
            }
            inPackets = null;
            int sp = l.IndexOf(' ');
            string word = sp < 0 ? l : l.Substring(0, sp), rest = sp < 0 ? "" : l.Substring(sp + 1);
            TraceStep Need() => st ?? throw new FormatException($"{where}: {word} before a step");
            switch (word)
            {
                case "#":
                    if (rest.StartsWith("golden trace: ", StringComparison.Ordinal)) t.Name = rest.Substring("golden trace: ".Length);
                    break;
                case "seed": t.Seed = int.Parse(rest, CultureInfo.InvariantCulture); break;
                case "flow": t.FlowSha = rest; break;
                case "recorder": t.Recorder = rest; break;
                case "lua-at":
                    if (!Regex.IsMatch(rest, "^[0-9a-f]{40}$")) throw new FormatException($"{where}: lua-at is a 40-hex commit: {rest}");
                    t.LuaAt = rest;
                    break;
                case "world":
                {
                    var m = WorldLine.Match(rest);
                    if (!m.Success) throw new FormatException($"{where}: world is <table> <16-hex>: {rest}");
                    t.World.Add((m.Groups[1].Value, m.Groups[2].Value));
                    break;
                }
                case "mask":
                    if (st != null) throw new FormatException($"{where}: a mask after the first step");
                    t.Masks.Add(Mask.Parse(rest));
                    break;
                case "step":
                    st = st == null ? new TraceStep { Label = rest } : st.Carry(rest);
                    t.Steps.Add(st);
                    break;
                case "packets":
                {
                    Need();
                    // packets [<name>] [@<bundle hash> (<count>)]: main's, or another char's (one of the header's)
                    var into = st.Packets;
                    if (rest.Length > 0 && rest[0] != '@')
                    {
                        int nsp = rest.IndexOf(' ');
                        string who = nsp < 0 ? rest : rest.Substring(0, nsp);
                        if (!t.Chars.Contains(who)) throw new FormatException($"{where}: packets of {who}, no char of the header");
                        if (st.Others.Any(o => o.Name == who)) throw new FormatException($"{where}: a second packets line of {who}");
                        into = new List<string>();
                        st.Others.Add((who, into));
                        rest = nsp < 0 ? "" : rest.Substring(nsp + 1);
                    }
                    if (rest.Length == 0)
                    {
                        inPackets = into;
                        break;
                    }
                    // a bundle: its packets, each from the store
                    var bm = EntryLine.Match(rest);
                    if (!bm.Success) throw new FormatException($"{where}: packets is alone or @<bundle hash> (<count>): {rest}");
                    string bh = bm.Groups[1].Value;
                    var hashes = store.GetBundle(bh) ?? throw new FormatException($"{where}: bundle {bh} is not in the store");
                    if (hashes.Length != int.Parse(bm.Groups[2].Value, CultureInfo.InvariantCulture))
                        throw new FormatException($"{where}: bundle {bh} holds {hashes.Length} packets, not {bm.Groups[2].Value}");
                    foreach (var h in hashes) into.Add(store.Get(h) ?? throw new FormatException($"{where}: packet {h} of bundle {bh} is not in the store"));
                    break;
                }
                case "char":
                    // char <name>: another char of the flow, in the header
                    if (st != null) throw new FormatException($"{where}: a char line after the first step");
                    if (!Regex.IsMatch(rest, "^[a-z][a-z0-9_]*$") || rest == Flow.MainChar || t.Chars.Contains(rest)) throw new FormatException($"{where}: char <name>: {rest}");
                    t.Chars.Add(rest);
                    break;
                case "ret": Need().Ret = rest; break;
                case "order":
                {
                    int eq = rest.IndexOf('=');
                    if (eq < 0) throw new FormatException($"{where}: order is <list>=<ids>");
                    string list = rest.Substring(0, eq), ids = rest.Substring(eq + 1);
                    var orders = Need().Orders;
                    int at = orders.FindIndex(o => o.List == list);
                    if (at < 0) orders.Add((list, ids));
                    else orders[at] = (list, ids);
                    break;
                }
                case "blobs":
                    t.BlobColumns.UnionWith(rest.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                    break;
                default:
                {
                    // <table>, or <name>:<table> for another char's
                    if (TraceStep.TableKey(word).Char is { } owner && !t.Chars.Contains(owner))
                        throw new FormatException($"{where}: a table of {owner}, no char of the header");
                    var set = Need().Set(word);
                    var tm = EntryLine.Match(rest);
                    if (tm.Success)
                    {
                        // <table> @<hash> (<rows>): the table's rows, a store entry
                        string th = tm.Groups[1].Value;
                        var rows = store.GetTable(th) ?? throw new FormatException($"{where}: {word} entry {th} is not in the store");
                        if (rows.Length != int.Parse(tm.Groups[2].Value, CultureInfo.InvariantCulture))
                            throw new FormatException($"{where}: {word} entry {th} holds {rows.Length} rows, not {tm.Groups[2].Value}");
                        set.Clear();
                        set.UnionWith(rows);
                    }
                    else if (rest.Length >= 2 && rest[0] is ('+' or '-') && rest[1] == ' ')
                    {
                        // <table> + <row> or <table> - <row>
                        string row = rest.Substring(2);
                        if (rest[0] == '+' ? !set.Add(row) : !set.Remove(row)) throw new FormatException($"{where}: {word} {rest[0]} of a row {(rest[0] == '+' ? "already there" : "not there")}");
                    }
                    else
                    {
                        // <table> <column> +<offset>:<hex> ...: a blob column's changed bytes
                        int csp = rest.IndexOf(' ');
                        if (csp <= 0 || csp + 1 >= rest.Length || rest[csp + 1] != '+') throw new FormatException($"{where}: unknown line {l}");
                        string prefix = rest.Substring(0, csp) + "=";
                        string old = set.FirstOrDefault(r => r.StartsWith(prefix, StringComparison.Ordinal)) ??
                                     throw new FormatException($"{where}: {word} has no {rest.Substring(0, csp)} to change");
                        string value = old.Substring(prefix.Length);
                        if (!IsHex(value)) throw new FormatException($"{where}: {word} {rest.Substring(0, csp)} is not a blob: {value}");
                        set.Remove(old);
                        set.Add(prefix + Convert.ToHexString(ApplyRuns(Convert.FromHexString(value), rest.Substring(csp + 1), where)));
                    }
                    if (set.Count == 0) st.Sets.Remove(word);
                    break;
                }
            }
        }
        return t;
    }

    // every difference between the recorded trace and another run of its flow, step by step on the rebuilt states, each
    // naming the step and the field; a packet difference shows both hex strings and a ^ under the first differing byte.
    // The header is the caller's business
    public static List<string> Compare(Trace want, Trace got)
    {
        var diffs = new List<string>();
        if (!want.Chars.SequenceEqual(got.Chars))
        {
            diffs.Add($"the chars differ: recorded [{string.Join(" ", want.Chars.Prepend(Flow.MainChar))}], now [{string.Join(" ", got.Chars.Prepend(Flow.MainChar))}]");
            return diffs;
        }
        if (StepsDiffer(want, got) is { } steps)
        {
            diffs.Add(steps);
            return diffs;
        }
        for (int i = 0; i < want.Steps.Count; i++)
        {
            var w = want.Steps[i];
            var g = got.Steps[i];
            string where = $"step {i + 1} [{w.Label}]";
            // main's packets, then each other char's (named after the field)
            foreach (var who in new[] { Flow.MainChar }.Concat(want.Chars.Union(got.Chars)))
            {
                var wp = w.PacketsOf(who);
                var gp = g.PacketsOf(who);
                if (wp.SequenceEqual(gp)) continue;
                string field = who == Flow.MainChar ? "packet" : $"{who}'s packet";
                int k = 0;
                while (k < wp.Count && k < gp.Count && wp[k] == gp[k]) k++;
                if (k >= wp.Count || k >= gp.Count)
                    diffs.Add($"{where}: {field}s: recorded {wp.Count}, now {gp.Count}; the first {k} equal" +
                              (k < wp.Count ? $"; recorded #{k}: {wp[k]}" : "") + (k < gp.Count ? $"; now #{k}: {gp[k]}" : ""));
                else
                {
                    string a = wp[k], b = gp[k];
                    var (at, offset) = FirstDifference(a, b);
                    diffs.Add($"{where}: {field} #{k} (0x{OpcodeOf(a)}) differs at byte {offset}\n" +
                              $"      recorded {a}\n" +
                              $"      now      {b}\n" +
                              $"               {new string(' ', at)}^");
                }
            }
            foreach (var table in TraceStep.Tables(w, g))
            {
                var a = w.Rows(table);
                var b = g.Rows(table);
                if (a.SetEquals(b)) continue;
                diffs.Add($"{where}: {table}: recorded only [{string.Join(", ", a.Where(r => !b.Contains(r)))}], now only [{string.Join(", ", b.Where(r => !a.Contains(r)))}]");
            }
            if (w.Ret != g.Ret) diffs.Add($"{where}: ret: recorded [{w.Ret}], now [{g.Ret}]");
            string wo = string.Join(" ; ", w.Orders.Select(o => $"{o.List}={o.Ids}")), go = string.Join(" ; ", g.Orders.Select(o => $"{o.List}={o.Ids}"));
            if (wo != go) diffs.Add($"{where}: order: recorded [{wo}], now [{go}]");
        }
        return diffs;
    }

    // the step lists' first difference, null when the labels match
    private static string StepsDiffer(Trace want, Trace got)
    {
        var wl = want.Steps.Select(s => s.Label).ToList();
        var gl = got.Steps.Select(s => s.Label).ToList();
        if (wl.SequenceEqual(gl)) return null;
        int i = 0;
        while (i < wl.Count && i < gl.Count && wl[i] == gl[i]) i++;
        return $"the steps differ at step {i + 1}: recorded [{(i < wl.Count ? wl[i] : "(end)")}], now [{(i < gl.Count ? gl[i] : "(end)")}]";
    }

    // rerecord --explain: the differences grouped. The header lines that differ (not the recorder), the world hashes; per (opcode, first differing byte) of the
    // packet pairs (a step's packets paired by opcode in order, as a diff of the opcode lists would), how many and the
    // first step; per other field (a table, ret, an order list) how many steps and the first; per opcode the packets
    // one side has and the other lacks
    public static List<string> Explain(Trace want, Trace got)
    {
        var lines = new List<string>();
        // the header lines but the recorder (a rerecord's always differs from a Lua recording's)
        void Header(string name, string a, string b)
        {
            if (a != b) lines.Add($"header {name}: recorded {a ?? "(none)"}, now {b ?? "(none)"}");
        }
        string Masks(Trace t) => t.Masks.Count == 0 ? null : string.Join(" ; ", t.Masks.Select(m => m.Line));
        string Blobs(Trace t) => t.BlobColumns.Count == 0 ? null : string.Join(" ", t.BlobColumns);
        Header("seed", want.Seed.ToString(CultureInfo.InvariantCulture), got.Seed.ToString(CultureInfo.InvariantCulture));
        Header("flow", want.FlowSha, got.FlowSha);
        Header("lua-at", want.LuaAt, got.LuaAt);
        Header("blobs", Blobs(want), Blobs(got));
        Header("mask", Masks(want), Masks(got));
        Header("char", want.Chars.Count == 0 ? null : string.Join(" ", want.Chars), got.Chars.Count == 0 ? null : string.Join(" ", got.Chars));
        foreach (var (table, hash) in want.World)
        {
            string now = got.World.FirstOrDefault(w => w.Table == table).Hash;
            if (now != hash) lines.Add($"world {table}: recorded {hash}, now {now ?? "(none)"}");
        }
        if (StepsDiffer(want, got) is { } steps)
        {
            lines.Add(steps);
            return lines;
        }
        var packets = new List<(string Key, int N, string First)>();
        var fields = new List<(string Key, int N, string First)>();
        var added = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var missing = new SortedDictionary<string, int>(StringComparer.Ordinal);
        void Count(List<(string Key, int N, string First)> groups, string key, string where)
        {
            int at = groups.FindIndex(g => g.Key == key);
            if (at < 0) groups.Add((key, 1, where));
            else groups[at] = (key, groups[at].N + 1, groups[at].First);
        }
        for (int i = 0; i < want.Steps.Count; i++)
        {
            var w = want.Steps[i];
            var g = got.Steps[i];
            string where = $"{i + 1} [{w.Label}]";
            // main's packets as the opcode; another char's as <name>:<opcode>
            foreach (var who in new[] { Flow.MainChar }.Concat(want.Chars.Union(got.Chars)))
            {
                string tag = who == Flow.MainChar ? "" : who + ":";
                var (pairs, onlyWant, onlyGot) = PairByOpcode(w.PacketsOf(who).ToList(), g.PacketsOf(who).ToList());
                foreach (var (a, b) in pairs.Where(p => p.A != p.B)) Count(packets, $"{tag}0x{OpcodeOf(a)} +{FirstDifference(a, b).Offset}", where);
                foreach (var p in onlyWant) missing[tag + OpcodeOf(p)] = missing.GetValueOrDefault(tag + OpcodeOf(p)) + 1;
                foreach (var p in onlyGot) added[tag + OpcodeOf(p)] = added.GetValueOrDefault(tag + OpcodeOf(p)) + 1;
            }
            foreach (var table in TraceStep.Tables(w, g))
                if (!w.Rows(table).SetEquals(g.Rows(table))) Count(fields, table, where);
            if (w.Ret != g.Ret) Count(fields, "ret", where);
            foreach (var list in w.Orders.Select(o => o.List).Union(g.Orders.Select(o => o.List)))
                if (w.Orders.FirstOrDefault(o => o.List == list).Ids != g.Orders.FirstOrDefault(o => o.List == list).Ids) Count(fields, $"order {list}", where);
        }
        lines.AddRange(packets.Select(p => $"{p.Key}: {p.N} packets, first at step {p.First}"));
        lines.AddRange(fields.Select(f => $"{f.Key}: {f.N} steps, first at step {f.First}"));
        static string Op(string key) => key.Contains(':') ? key.Replace(":", ":0x") : "0x" + key;
        lines.AddRange(added.Select(a => $"{Op(a.Key)} added: {a.Value}"));
        lines.AddRange(missing.Select(m => $"{Op(m.Key)} missing: {m.Value}"));
        return lines;
    }

    // the two packet lists paired by opcode: the longest common subsequence of their opcodes (in order), the rest
    // unpaired; equal opcode lists pair by index
    private static (List<(string A, string B)> Pairs, List<string> OnlyA, List<string> OnlyB) PairByOpcode(List<string> a, List<string> b)
    {
        var oa = a.Select(OpcodeOf).ToList();
        var ob = b.Select(OpcodeOf).ToList();
        var len = new int[a.Count + 1, b.Count + 1];
        for (int i = a.Count - 1; i >= 0; i--)
            for (int j = b.Count - 1; j >= 0; j--)
                len[i, j] = oa[i] == ob[j] ? len[i + 1, j + 1] + 1 : Math.Max(len[i + 1, j], len[i, j + 1]);
        var pairs = new List<(string, string)>();
        var onlyA = new List<string>();
        var onlyB = new List<string>();
        int x = 0, y = 0;
        while (x < a.Count && y < b.Count)
        {
            if (oa[x] == ob[y]) pairs.Add((a[x++], b[y++]));
            else if (len[x + 1, y] >= len[x, y + 1]) onlyA.Add(a[x++]);
            else onlyB.Add(b[y++]);
        }
        onlyA.AddRange(a.Skip(x));
        onlyB.AddRange(b.Skip(y));
        return (pairs, onlyA, onlyB);
    }

    // a normalised packet's opcode (the text before ':')
    private static string OpcodeOf(string packet) => packet.Substring(0, Math.Max(0, packet.IndexOf(':')));

    // where two packets first differ: the character index of the byte holding it, and that byte's offset in the packet
    private static (int At, int Offset) FirstDifference(string a, string b)
    {
        int d = 0;
        while (d < a.Length && d < b.Length && a[d] == b[d]) d++;
        return ByteAt(a, OpcodeOf(a).Length + 1, d);
    }

    // the placeholders Normalise writes and the bytes each stands for: a char's id (<CHARID>, another char's
    // <CHARID:<name>>), 4 bytes, and its name Qgolden<nnn> (<CHARNAME>, <CHARNAME:<name>>), 10 bytes
    private static readonly Regex Placeholder = new(@"\G<(CHARID|CHARNAME)(:[a-z][a-z0-9_]*)?>");

    // a 0x028's bit-packed char target after the hex (Program.CharTargetBits): <CHARID@<bit>> (another char's
    // <CHARID:<name>@<bit>>), no bytes of its own
    internal const string CharBitsMark = "<CHARID@";
    private static readonly Regex CharBits = new(@"\G<CHARID(:[a-z][a-z0-9_]*)?@");

    // the character index where the byte holding character d starts, and that byte's offset in the packet; the
    // normalised hex from index start is two characters a byte, a placeholder its bytes at once, a <CHARID@<bit>> none
    // (it follows the hex: a difference there is at the packet's end)
    private static (int At, int Offset) ByteAt(string s, int start, int d)
    {
        int i = start, offset = 0;
        while (i < s.Length)
        {
            if (s[i] == '<' && CharBits.IsMatch(s, i)) return (i, offset);
            var ph = s[i] == '<' ? Placeholder.Match(s, i) : Match.Empty;
            int len = ph.Success ? ph.Length : 2;
            if (d < i + len) return (i, offset);
            i += len;
            offset += ph.Success ? (ph.Groups[1].Value == "CHARID" ? 4 : 10) : 1;
        }
        return (Math.Min(d, s.Length), offset);
    }
}
