using System;
using System.Collections.Generic;
using System.Linq;

// harness-speed: --no-gate's refusals and its line, LazyMode, LoadSelected on made-up files, ItemStackProblems' only, in
// the self-test (no database). select-check runs LoadSelected against LoadFlows over the real flows
internal static partial class Program
{
    private static void SelfTestSpeed()
    {
        Console.WriteLine("harness-speed: --no-gate, a selection's flows");
        Check("  --no-gate: replay of a selection goes; all, --jobs, --shard, --counts and the other modes are refused",
              "-|" +
              "--no-gate goes with replay <selection> only, not with all (replay all runs the startup gate)|" +
              "--no-gate goes with replay <selection> only, not with --jobs, --shard or --counts|" +
              "--no-gate goes with replay <selection> only, not with --jobs, --shard or --counts|" +
              "--no-gate goes with replay <selection> only, not with --jobs, --shard or --counts|" +
              "--no-gate goes with replay <selection> only, not with record|" +
              "--no-gate goes with replay <selection> only, not with rerecord|" +
              "--no-gate goes with replay <selection> only, not with stable|" +
              "--no-gate goes with replay <selection> only, not with startup|" +
              "--no-gate goes with replay <selection> only, not with show|" +
              "--no-gate goes with replay <selection> only, not with --selftest",
              string.Join("|", new[]
              {
                  NoGateError("replay", "a.one,flow:b.two#x", false, false, false),
                  NoGateError("replay", "all", false, false, false),
                  NoGateError("replay", "a", true, false, false),
                  NoGateError("replay", "a", false, true, false),
                  NoGateError("replay", "a", false, false, true),
                  NoGateError("record", "a", false, false, false),
                  NoGateError("rerecord", "a", false, false, false),
                  NoGateError("stable", "a", false, false, false),
                  NoGateError("startup", "all", false, false, false),
                  NoGateError("show", "a", false, false, false),
                  NoGateError("--selftest", "all", false, false, false),
              }.Select(e => e ?? "-")));
        Check("  --no-gate: the skipped gate's line names the flag, and is no PASS / FAIL line (a runner counts those)",
              "True/False/False", $"{NoGateLine.Contains(NoGateFlag)}/{NoGateLine.StartsWith("PASS")}/{NoGateLine.StartsWith("FAIL")}");
        Check("  only replay <selection> --no-gate loads lazily (opt-in); without the flag, record and rerecord load every flow",
              "True/False False False/False False False False",
              $"{LazyMode("replay", "a", true)}/" +
              $"{LazyMode("replay", "a", false)} {LazyMode("record", "a", false)} {LazyMode("rerecord", "a,b", false)}/" +
              $"{LazyMode("replay", "all", true)} {LazyMode("record", "a", true)} {LazyMode("stable", "a", true)} " +
              $"{LazyMode("item-stacks", "a", true)}");
        Check("  --fail-fast stops at the party cleanup check under its own name with --no-gate (no flow's, not the gate's)",
              "the party cleanup check (--no-gate)|the startup gate", $"{GateStopLabel(true)}|{GateStopLabel(false)}");
        // review M8: a stub's module lines (Flow.ModuleLines) must track Parse's module syntax; every gate runs this
        var every = LoadFlows((_, _) => true);
        int withModules = 0;
        var mismatches = every == null ? new List<string> { "a flow file does not parse" }
                                       : ModuleLineMismatches(every, out withModules);
        Check($"  every flow file: Flow.ModuleLines gives its parsed module lines ({every?.Count} flows, {withModules} " +
              "load a module; select-check checks the selections)", "", string.Join(" | ", mismatches.Take(5)));

        // made-up files in FlowPaths' order (the file names', .flow included: a#x.flow sorts before a.flow)
        var paths = new[] { "f/a#x.flow", "f/a.flow", "f/b#1.flow", "f/b#2.flow", "f/c.flow" };
        var loaded = new List<string>();
        var stubbed = new List<string>();
        Flow Parsed(string p, bool module)
        {
            loaded.Add(p);
            var f = Flow.Parse("char zone=SouthernSanDoria pos=0,0,0\n" + (module ? "module modules/x/y.lua\n" : "") + "login\n",
                               System.IO.Path.GetFileName(p), System.IO.Path.GetFileNameWithoutExtension(p), (_, _) => true);
            f.File = p;
            return f;
        }
        Flow Stubbed(string p)
        {
            stubbed.Add(p);
            return new Flow { Name = System.IO.Path.GetFileNameWithoutExtension(p), File = p, Stub = true };
        }
        string Run(string sel, bool module = false, string bad = null)
        {
            loaded.Clear();
            stubbed.Clear();
            var all = LoadSelected(paths, sel, p => p == bad ? throw new FormatException($"{p}:1: bad") : Parsed(p, module), Stubbed,
                                   out var b, out var why);
            if (all == null) return $"refused {b}: {why}";
            var chosen = SelectFlows(all, sel, f => f.Name, f => f.Key, out _, out _);
            return string.Join(",", chosen.Select(f => $"{f.Name}@{all.IndexOf(f)}")) + " parsed " +
                   string.Join(",", all.Where(f => !f.Stub).Select(f => f.Name)) + $" files read {loaded.Count + stubbed.Count}";
        }
        Check("  LoadSelected: a key picks its flows, parsed alone, each at its index in name order; no picked module: no stub read",
              "b#1@2,b#2@3 parsed b#1,b#2 files read 2", Run("b"));
        Check("  LoadSelected: a#x sorts before a (the file names'), so a is index 1; a's key picks both, in name order",
              "a#x@0,a@1 parsed a#x,a files read 2", Run("a"));
        Check("  LoadSelected: a list in the order given, a flow named twice once; the parse in name order",
              "c@4,a#x@0,b#2@3 parsed a#x,b#2,c files read 3", Run("c,flow:a#x,b#2,c"));
        Check("  LoadSelected: a picked flow that loads a module: every stub's module lines read too",
              "b#2@3 parsed b#2 files read 5", Run("flow:b#2", module: true));
        Check("  LoadSelected: the selector's refusal, before any file is read",
              "refused zz: no flow of that name|0", $"{Run("a,zz")}|{loaded.Count + stubbed.Count}");
        Check("  LoadSelected: a picked flow's parse error fails the run (an unpicked one's is not read)",
              "refused parse: f/b#1.flow:1: bad|a#x@0,a@1 parsed a#x,a files read 2",
              $"{Run("b,a", bad: "f/b#1.flow")}|{Run("a", bad: "f/b#1.flow")}");

        // ItemStackProblems' only: a's second Rare row exempt, b's not, an entry for c (a stub) whose line breaks no rule
        const string twoRare = "char zone=SouthernSanDoria pos=0,0,0\nitem 2012\nitem 2012\nlogin\n";
        var fa = Flow.Parse(twoRare, "a.flow", "a", (_, _) => true);
        var fb = Flow.Parse(twoRare, "b.flow", "b", (_, _) => true);
        fa.File = "a.flow";
        fb.File = "b.flow";
        var exempt = new Dictionary<string, string> { ["a:3"] = "item 2012", ["c:2"] = "item 4096" };
        static (int, bool) Rare(int item) => (1, true);
        string Problems(IReadOnlyList<Flow> flows, params string[] only) =>
            string.Join(" / ", ItemStackProblems(flows, Rare, exempt, only.Length == 0 ? null : only.ToHashSet()));
        Check("  item stacks: every flow (b's second Rare row and c's stale entry); a selection of b (b's row), of a (none)",
              "b.flow:3: [item 2012] a second row of a Rare item (the game holds one): give it once / " +
              "item-stack-exempt.txt: c:2 [item 4096] is stale: that line is no setup item line above its stack size or a second Rare row (drop the entry)" +
              "|b.flow:3: [item 2012] a second row of a Rare item (the game holds one): give it once|",
              $"{Problems(new[] { fa, fb })}|{Problems(new[] { fb }, "b")}|{Problems(new[] { fa }, "a")}");
    }
}
