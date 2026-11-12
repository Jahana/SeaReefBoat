using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

// harness-speed: a mutant run's replay made cheaper, opt-in, nothing else of what a replay checks changed.
//
// --no-gate: `replay <selection> --no-gate` skips the startup gate (Gate: two new worlds and a scan of every script) and
// prints NoGateLine instead. A mutant run replays its scope and group many times on one tree; the gate's checks are
// the tree's, and `replay all`, the lane gate and main's run keep them. Refused with all, --jobs, --shard, --counts and
// every other mode (NoGateError). With the gate skipped, the party cleanup check that still runs before the flows stops
// a --fail-fast run under its own name (GateStopLabel), so a runner never takes it for the gate or a flow.
//
// A selection's flows (LoadSelected), opt-in with --no-gate (LazyMode): that replay parses only the flows the selector
// picks; every run without the flag (a lane's replay, record or rerecord of a selection) parses every flow as before,
// so it still reports every flow's parse error and item stack problem. The selector needs only the names (a flow's name is its file's, its key the name up to its first '#':
// SelectFlows over the names picks what it picks over the parsed flows), and a flow's char ids only its index in the
// files' name order and its own chars, so the picked flows keep both. Every other flow is a stub in its place
// (Flow.Stub: its name, file and module lines, Flow.ModuleLines) so all keeps its length and order: CharsFor's
// all.IndexOf, the coverage gate's groups by key (CoverageResults: a key's flows that did not all run check only
// their waivers, the ran flows' and so parsed) and its module part (ModuleCoverageResults: a module is checked only
// once every flow that loads it ran, which the stubs' module lines tell) see what they see today. ItemStackGate checks
// the picked flows' part (ItemStackProblems' only). select-check proves it against LoadFlows over every flow
internal static partial class Program
{
    internal const string NoGateFlag = "--no-gate";
    internal const string NoGateLine =
        "SKIP the startup gate (--no-gate): a selection's replay; replay all and the lane and main gates run it";

    // why --no-gate is refused with this run (null: it goes): replay <selection> only
    internal static string NoGateError(string mode, string sel, bool jobs, bool shard, bool counts)
    {
        if (mode != "replay") return $"{NoGateFlag} goes with replay <selection> only, not with {mode}";
        if (sel == "all") return $"{NoGateFlag} goes with replay <selection> only, not with all (replay all runs the startup gate)";
        if (jobs || shard || counts) return $"{NoGateFlag} goes with replay <selection> only, not with --jobs, --shard or --counts";
        return null;
    }

    // whether the run loads its selection's flows only (LoadSelected): replay <selection> --no-gate (opt-in, review M1)
    internal static bool LazyMode(string mode, string sel, bool noGate) => noGate && mode == "replay" && sel != "all";

    // the name a --fail-fast run stops at when a check before the flows failed: the startup gate, or with --no-gate
    // the party cleanup check (CleanupPartyCheck), the one check left before the flows
    internal static string GateStopLabel(bool noGate) => noGate ? "the party cleanup check (--no-gate)" : "the startup gate";

    // a selection's flows: every flow file in its place (paths in name order: FlowPaths), the picked ones loaded (load:
    // Flow.Load), the others stubs (stub: Flow.LoadStub) whose module lines are read only when a picked flow loads a
    // module (a module no picked flow loads is checked by no selection's coverage gate: its flows did not all run).
    // Null, with a failure, when the selector refuses (SelectFlows' element and why) or a picked flow fails to parse
    // (the first in name order)
    private static List<Flow> LoadSelected(IReadOnlyList<string> paths, string sel, Func<string, Flow> load, Func<string, Flow> stub)
    {
        var flows = LoadSelected(paths, sel, load, stub, out var bad, out var why);
        if (flows == null) Fail(bad, why);
        return flows;
    }

    internal static List<Flow> LoadSelected(IReadOnlyList<string> paths, string sel, Func<string, Flow> load, Func<string, Flow> stub,
                                            out string bad, out string why)
    {
        var names = paths.Select(Path.GetFileNameWithoutExtension).ToList();
        var picked = SelectFlows(Enumerable.Range(0, names.Count).ToList(), sel, k => names[k], k => Flow.KeyOf(names[k]),
                                 out bad, out why);
        if (picked == null) return null;
        var parsed = new Dictionary<int, Flow>();
        foreach (int k in picked.OrderBy(k => k))
        {
            try
            {
                parsed[k] = load(paths[k]);
            }
            catch (FormatException e)
            {
                (bad, why) = ("parse", e.Message);
                return null;
            }
        }
        bool modules = parsed.Values.Any(f => f.Modules.Count > 0);
        var flows = new List<Flow>(names.Count);
        for (int k = 0; k < paths.Count; k++)
            flows.Add(parsed.TryGetValue(k, out var f) ? f
                      : modules ? stub(paths[k]) : new Flow { Name = names[k], File = paths[k], Stub = true });
        return flows;
    }

    // each parsed flow whose file's module lines as Flow.ModuleLines scans them (a stub's) are not its parsed Modules.
    // select-check and the self-test (review M8: every gate runs it, so a ModuleLines that drifts from Parse's module
    // syntax fails there) run it over every flow file
    internal static List<string> ModuleLineMismatches(IReadOnlyList<Flow> flows, out int withModules)
    {
        withModules = 0;
        var mismatches = new List<string>();
        foreach (var f in flows)
        {
            var scanned = Flow.ModuleLines(Encoding.UTF8.GetString(File.ReadAllBytes(f.File)));
            if (!scanned.SequenceEqual(f.Modules))
                mismatches.Add($"{f.Name}: scanned [{string.Join(",", scanned)}], parsed [{string.Join(",", f.Modules)}]");
            if (f.Modules.Count > 0) withModules++;
        }
        return mismatches;
    }

    // ---- select-check -------------------------------------------------------------------------------------------------

    // LoadSelected against LoadFlows (the parse with every NPC known: no database): for every flow file its module lines
    // scanned as parsed; for every flow:<name>, every key, every branch name and a few lists (refused ones too) the same
    // refusal or the same flows in the same order, each at the same index of all with the same chars, so the same ids;
    // the coverage gate's lines (CoverageResults, every picked flow ran) and the item stack problems (a made-up stack
    // table, so many lines break a rule) of the selection those of the full list restricted to the picked flows
    private static int SelectCheck(Stopwatch clock)
    {
        Console.WriteLine("select-check: LoadSelected against LoadFlows, over every flow");
        var paths = FlowPaths();
        var full = LoadFlows((_, _) => true);
        if (full == null) return Done(clock);
        Check("select-check: LoadFlows reads every flow file, in FlowPaths' order", string.Join("|", paths),
              string.Join("|", full.Select(f => f.File)));
        var moduleMismatch = ModuleLineMismatches(full, out int withModules);
        Check($"select-check: Flow.ModuleLines gives every flow's parsed module lines ({full.Count} flows, {withModules} load a module)",
              "", string.Join(" | ", moduleMismatch.Take(5)));

        static (int Stack, bool Rare) Fake(int item) => (item % 7 == 0 ? 1 : item % 3 == 0 ? 12 : 99, item % 5 == 0);
        var exemptProblems = new List<string>();
        var exempt = ItemStackExempt(exemptProblems);
        var fullStack = ItemStackProblems(full, Fake, exempt);
        Console.WriteLine($"select-check: {fullStack.Count} item stack problems over every flow with the made-up stack table");

        var stubs = new Dictionary<string, Flow>(StringComparer.Ordinal);
        Flow Stub(string p)
        {
            if (!stubs.TryGetValue(p, out var s)) stubs[p] = s = Flow.LoadStub(p);
            return s;
        }
        int parses = 0;
        Flow Load(string p)
        {
            parses++;
            return Flow.Load(p, (_, _) => true);
        }

        var keys = full.Select(f => f.Key).Distinct(StringComparer.Ordinal).ToList();
        var sels = full.Select(f => FlowPrefix + f.Name)
            .Concat(keys)
            .Concat(full.Where(f => f.Name != f.Key).Select(f => f.Name))
            .ToList();
        // lists: a mutant run's shape (a scope's keys and flows, then a group's rest), names twice, refusals
        var cop = keys.Where(k => k.StartsWith("mission.cop.", StringComparison.Ordinal)).ToList();
        var bfs = full.Where(f => f.Key.StartsWith("battlefield.", StringComparison.Ordinal)).Select(f => FlowPrefix + f.Name).Take(40).ToList();
        sels.Add(string.Join(",", cop));
        sels.Add(string.Join(",", bfs.Concat(cop).Concat(bfs)));
        sels.Add(string.Join(",", Enumerable.Reverse(keys).Take(25)));
        sels.Add($"{full[^1].Name},{full[0].Key},{FlowPrefix}{full[0].Name},{full[0].Key}");
        sels.Add("no.such_flow");
        sels.Add($"{full[0].Key},");
        sels.Add($"{full[0].Key},all");
        sels.Add(FlowPrefix);
        sels.Add(FlowPrefix + full[0].Key + "#no_such_branch");

        int checkedSels = 0, refused = 0, picks = 0;
        var problems = new List<string>();
        void Problem(string sel, string what)
        {
            if (problems.Count < 20) problems.Add($"{(sel.Length > 80 ? sel.Substring(0, 80) + "..." : sel)}: {what}");
            else if (problems.Count == 20) problems.Add("...");
        }
        foreach (var sel in sels)
        {
            checkedSels++;
            var want = SelectFlows(full, sel, f => f.Name, f => f.Key, out var wantBad, out var wantWhy);
            var all = LoadSelected(paths, sel, Load, Stub, out var bad, out var why);
            if (want == null || all == null)
            {
                if (want != null || all != null || bad != wantBad || why != wantWhy)
                    Problem(sel, $"refusal: full [{wantBad}: {wantWhy}], lazy [{bad}: {why}]");
                refused++;
                continue;
            }
            if (all.Count != full.Count || !all.Select(f => f.Name).SequenceEqual(full.Select(f => f.Name)))
            {
                Problem(sel, "all's names or order differ");
                continue;
            }
            var chosen = SelectFlows(all, sel, f => f.Name, f => f.Key, out _, out _);
            if (chosen == null || !chosen.Select(f => f.Name).SequenceEqual(want.Select(f => f.Name)))
            {
                Problem(sel, $"picked [{string.Join(",", chosen?.Select(f => f.Name) ?? new string[0])}], full picks [{string.Join(",", want.Select(f => f.Name))}]");
                continue;
            }
            for (int i = 0; i < chosen.Count; i++)
            {
                Flow l = chosen[i], f = want[i];
                int li = all.IndexOf(l), fi = full.IndexOf(f);
                string lIds = string.Join(",", CharsFor(li, l.Chars.Count, 0, CharCount)), fIds = string.Join(",", CharsFor(fi, f.Chars.Count, 0, CharCount));
                if (l.Stub || li != fi || lIds != fIds || l.Sha256 != f.Sha256 || ReferenceEquals(l, f))
                    Problem(sel, $"{l.Name}: stub {l.Stub}, index {li} / {fi}, ids {lIds} / {fIds}, sha {l.Sha256 == f.Sha256}");
            }
            if (all.Count(f => !f.Stub) != chosen.Count) Problem(sel, $"{all.Count(f => !f.Stub)} parsed, {chosen.Count} picked");
            picks += chosen.Count;

            // the coverage gate's lines: every picked flow ran; each picked key declares its flows' waived labels and one
            // no flow reaches, each module of a picked flow one override no flow reaches
            var cov = new Coverage();
            foreach (var f in want) cov.Ran.Add(f.Name);
            foreach (var key in want.Select(f => CoverageKey(f.Key)).Distinct())
                cov.Declared[key] = full.Where(f => CoverageKey(f.Key) == key).SelectMany(f => f.Waivers).Select(w => w.Label)
                    .Append("1/Nobody/onTrigger").Distinct().ToList();
            foreach (var m in want.SelectMany(f => f.Modules).Distinct()) cov.Declared[ModuleKey(m)] = new List<string> { "x 1/Nobody/onTrigger" };
            var wantCov = CoverageResults(cov, full);
            var lazyCov = CoverageResults(cov, all);
            if (!wantCov.SequenceEqual(lazyCov))
                Problem(sel, $"coverage lines differ: full {wantCov.Count} [{string.Join(" / ", wantCov.Except(lazyCov).Take(2))}], " +
                             $"lazy {lazyCov.Count} [{string.Join(" / ", lazyCov.Except(wantCov).Take(2))}]");

            // the item stack problems: the full list's of the picked flows (their lines, the entries naming them)
            var names = want.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
            var wantStack = fullStack.Where(p => want.Any(f => p.StartsWith($"{f.Name}.flow:", StringComparison.Ordinal) ||
                                                               p.StartsWith($"item-stack-exempt.txt: {f.Name}:", StringComparison.Ordinal))).ToList();
            var lazyStack = ItemStackProblems(all.Where(f => !f.Stub).ToList(), Fake, exempt, names);
            if (!wantStack.SequenceEqual(lazyStack))
                Problem(sel, $"item stack problems differ: full {wantStack.Count}, lazy {lazyStack.Count}");
        }
        Check($"select-check: {checkedSels} selections ({refused} refused alike, {picks} flows picked, {parses} parsed): " +
              "the same flows, indices, char ids, coverage lines and item stack problems as LoadFlows",
              "", string.Join("\n    ", problems));
        return Done(clock);
    }
}
