using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using XI.Map;
using XI.Persistence;
using XI.Quests;
using XI.Quests.Authoring;

// XI.Verify.QuestGolden.Live -- the golden traces of quest rework B3a (docs/superpowers/specs/2026-10-21-quest-rework-
// b3a-design.md, section 5). Each quest flow is a data file (tests/golden/flows/<key>[#branch].flow, Flow.cs); its Lua
// run is recorded once as a text trace (tests/golden/<key>[#branch].trace, Trace.cs) and the C# run is replayed against
// it, step by step: the packets (their bodies once each in tests/golden/packets.store), the char's database state
// (chars and every table with a charid column), the Lua returns and the lookup lists' order. The header holds hashes of
// the world tables (server_variables, conquest_system) at the run's start. Every world starts them from the snapshot
// tests/golden/world-tables.txt, in the run's private copies (World.cs), so a header that differs fails the replay.
// Every run starts in a new world with every clock and random source pinned (Harness.NewWorld), so a trace
// recorded in one process replays in another; `stable` proves it by recording twice in two processes.
// Quest rework C2: a key is a container's, a quest's (<area>.<const>), a mission's (mission.<log>.<const>) or a
// battlefield's (battlefield.<const>); every mode below takes the three alike, and "quest" in it means any of them
// (LuaIdConstant, KindScripts, OverlayPlan's battlefield framework, the gate over every container's LegacyPath).
//
//   (no mode)                      replay all
//   replay <key|all> [--shard i/n] [--counts <file>]
//                                  the startup gate, then the C# run of each trace's flow against the trace, then the
//                                  coverage gate (CoverageGate) over the quests whose flows all ran here (and the modules
//                                  their module lines load: ModuleCoverageResults, quest rework P4); --counts: the
//                                  gate left to the caller, the runs and totals written to the file (Coverage.Write)
//   replay <name|key>,<name|key>,... [--fail-fast]
//                                  (quest rework P8) a list of flow names and keys (no spaces), its flows run in the order
//                                  given (a key's in name order; one named twice runs once, at its first place), each on
//                                  the char ids it has in `all` (SelectFlows). record, rerecord and stable take the list
//                                  too. --fail-fast (replay only, not with --jobs): stop after the first failing flow (or
//                                  a failed startup gate), print what ran, what failed and what did not run, skip the
//                                  coverage gate, then the summary line. Coverage and the orphan check stay all-only.
//                                  A second selector (`replay a b`) is refused (PositionalError), not dropped. A base
//                                  flow's name is its quest's key, so <key> is every flow of the quest; flow:<name>
//                                  (carry-harness-2) is the one flow of that name (flow:bastok.the_doorman)
//   replay <selection> --no-gate   (harness-speed) the startup gate skipped (Gate), and one line says so (NoGateLine): a
//                                  mutant run's replays, whose gate replay all, the lane gate and main's run. Refused
//                                  with all, --jobs, --shard, --counts and any other mode (NoGateError). Only with the
//                                  flag, the selection parses only the flows it picks (LoadSelected, Speed.cs): each
//                                  keeps its index in all's name order, so its char ids
//   select-check                   (harness-speed) LoadSelected against LoadFlows over every flow file, every flow:<name>,
//                                  key and name: the same flows, indices, char ids, coverage lines and item stack problems
//                                  (no database)
//   replay all --jobs <n>          n child processes `replay all --shard i/n --counts <tmp>/<i>.txt` at once, their
//                                  output printed in shard order, their counts merged and the coverage gate run once.
//                                  A shard's flows are dealt by expected cost (quest rework P8: ShardsByCost, FlowCost),
//                                  the costliest first into the least-loaded shard, so the shards end together
//   record <key|all> [--lua-at <commit>] [--out dir]
//                                  the Lua run (CSHARP_QUESTS_OFF = { key }) written as the trace, its header's lua-at the
//                                  commit whose Lua ran: --lua-at, else HEAD when the quest's Lua is there, else the
//                                  checked-in trace's lua-at (LuaCommit). Not HEAD: the scripts run from a temp tree, the
//                                  working tree's scripts/ with the quest's files from that commit over them (Overlay),
//                                  and every changed script that calls a quest gone off with them (OverlayPlan's
//                                  closure). Refused: at HEAD into tests/golden with scripts/ not committed
//                                  (DirtyScriptsRefusal; --out elsewhere goes on); a script left at HEAD that calls a
//                                  quest gone off (OverlayStray); a quest half-ported at the commit (OverlayHalfPorted)
//   rerecord <key|all> [--out dir] [--explain]
//                                  the C# run written as the trace (header csharp-rerecord, the checked-in lua-at kept);
//                                  --explain (record too): a trace that changes (against the one in --out, else the
//                                  checked-in one), its differences grouped first
//   stable <key|all> [--csharp]    record (--csharp: rerecord) twice in two child processes into a temp folder, and diff
//   startup [--against <commit>] [--synthetic <n>]
//                                  the time from a new ScriptEngine through InitScripts to the first zone (235) loaded, in
//                                  6 fresh NewWorlds: the first (`startup cold <ms> ms`, the JIT paid) and the median of the
//                                  other 5 (`startup median <ms> ms`), with the registration line's ms of each. --against
//                                  builds the commit in a temp worktree under ../ (detached), probes it and this build
//                                  interleaved in 5 rounds, and prints `startup ratio` (the gate) and `startup cold ratio`
//                                  (information only, see StartupAgainst). --synthetic: then n synthetic quests (Synthetic.cs) registered in 6 more
//                                  worlds, their cost per quest and the projected ratios for 75 and 1000 more quests
//   show <key>[#branch] [--all] [--out dir]
//                                  a checked-in trace (--out: the one recorded there, with its store) for reading
//                                  (Show.cs): per step its flow line, its packets (event and message packets decoded, the
//                                  store entries inlined), returns, DB changes and lookup orders; --all also prints the
//                                  login bundle's packets and the first step's rows. No DB
//   world-snapshot                 tests/golden/world-tables.txt written from the --db's real world tables (World.cs):
//                                  the rows every world starts from
//   item-stacks                    (carry-harness-2) tests/golden/item-stack-exempt.txt written again, each entry
//                                  re-keyed to the line holding its flow's statement now; the list never grows: a
//                                  second Rare row on no entry fails it, nothing written (RekeyItemStackExempt)
//   --selftest                     the flow parser's and the trace format's self-test (no database)
//   --db <name>                    the database (xidb): the lane (below). Every connection of the run uses it, and the
//                                  children of --jobs, stable and startup get it
//   --port <n>                     the MariaDB port (3307)
//
// Chars 996161-996199: the trace at index i of all flows in name order uses 996161 + i % 39, cleaned before use. A shard
// (--shard i/n) deals the 39 ids into n disjoint sub-ranges and its traces reuse its own, one at a time, so shards can run
// at once. Each run holds GET_LOCK('golden-<db>-<id>') for every id it uses (LockChars): one that another run on the same
// database holds fails it (on xidb it also holds golden-<id>, the name of the builds from before the lanes).
//
// Lanes: a lane is a database the suite runs on (--db), so two worktrees can record and replay at once, each on its own:
// the default lane is xidb, another is a full copy of it made by tools/golden_lane_db.sh <name> (xidb_lane2). The chars
// and the locks are per database, the world tables per run (World.cs); a trace does not depend on the lane (the char is
// $char / $charname, the world tables the snapshot's, the header names no database), so a trace recorded on one lane
// replays on the other.
//
// Exit codes: 0 = all passed; 1 = something failed; 3 = DB not reachable.
internal static partial class Program
{
    private static int _pass, _fail;
    private const uint FirstChar = 996161;
    private const int CharCount = 39;
    private static int dbPort_ = 3307;
    // --db: the lane's database
    private const string DefaultDb = "xidb";
    private static string dbName_ = DefaultDb;
    // record --lua-at: the commit whose Lua a record runs (null: LuaCommit's choice)
    private static string luaAt_;
    // --explain: a record / rerecord that changes a trace prints its differences grouped (Trace.Explain) first
    private static bool explain_;

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }
    private static void Pass(string name) => Check(name, "", "");
    private static void Fail(string name, string why)
    {
        _fail++;
        Console.WriteLine($"FAIL {name}: {why}");
    }

    private static string GoldenDir => Path.Combine(Root, "tests", "golden");
    private static string FlowsDir => Path.Combine(GoldenDir, "flows");

    private static int Main(string[] argv)
    {
        var args = argv.ToList();
        string Option(string name)
        {
            int i = args.IndexOf(name);
            if (i < 0 || i + 1 >= args.Count) return null;
            string v = args[i + 1];
            args.RemoveRange(i, 2);
            return v;
        }
        if (LaneError(Option("--db"), Option("--port"), out dbName_, out dbPort_) is { } laneError)
        {
            Console.WriteLine(laneError);
            return 1;
        }
        string against = Option("--against");
        string syntheticArg = Option("--synthetic");
        int synthetic = 0;
        if (syntheticArg != null && (!int.TryParse(syntheticArg, out synthetic) || synthetic < 1))
        {
            Console.WriteLine($"--synthetic takes a count of 1 or more, not {syntheticArg}");
            return 1;
        }
        string outDir = Option("--out") is { } outArg ? Path.GetFullPath(outArg) : null;
        string shard = Option("--shard");
        string countsPath = Option("--counts") is { } countsArg ? Path.GetFullPath(countsArg) : null;
        string jobs = Option("--jobs");
        luaAt_ = Option("--lua-at");
        bool csharp = args.Remove("--csharp");
        bool failFast = args.Remove("--fail-fast");
        explain_ = args.Remove("--explain");
        bool showAll = args.Remove("--all");
        bool noGate = args.Remove(NoGateFlag);
        if (PositionalError(args) is { } positionalError)
        {
            Console.WriteLine(positionalError);
            return 1;
        }
        string mode = args.Count > 0 ? args[0] : "replay";
        string sel = args.Count > 1 ? args[1] : "all";
        if (noGate && NoGateError(mode, sel, jobs != null, shard != null, countsPath != null) is { } noGateError)
        {
            Console.WriteLine($"{noGateError}: {string.Join(" ", argv)}");
            return 1;
        }
        var clock = Stopwatch.StartNew();
        dataDir_ = FindDataDir();

        if (mode == "--selftest")
        {
            SelfTest();
            Console.WriteLine($"{_pass} passed, {_fail} failed");
            return _fail == 0 ? 0 : 1;
        }
        if (mode == "show") return Show(args.Count > 1 ? args[1] : "", showAll, outDir);
        if (mode == "select-check") return SelectCheck(clock);
        // mutant-design (option 3): the schema generator (no database) and the mutants process (MutantsMode.cs)
        if (mode == "schema") return SchemaArgs(args, outDir);
        if (mode == "mutants") return Mutants(argv, args, clock);
        if (mode is not ("replay" or "record" or "rerecord" or "stable" or "startup" or "world-snapshot"
                         or "item-stacks"))
        {
            Console.WriteLine($"unknown mode {mode}: replay, record, rerecord, stable, startup, world-snapshot, " +
                              "item-stacks, show, select-check, schema, mutants or --selftest");
            return 1;
        }
        if (synthetic > 0 && mode != "startup")
        {
            Console.WriteLine("--synthetic goes with startup only");
            return 1;
        }
        if (mode == "startup" && against != null) return StartupAgainst(against, synthetic);
        if (failFast && (mode != "replay" || jobs != null))
        {
            Console.WriteLine($"--fail-fast goes with replay only, not with --jobs: {string.Join(" ", argv)}");
            return 1;
        }
        if (jobs != null)
        {
            if (mode != "replay" || sel != "all" || shard != null || countsPath != null || !int.TryParse(jobs, out int n) || n < 1)
            {
                Console.WriteLine($"--jobs takes a count of 1 or more, with replay all only (no --shard, no --counts): {string.Join(" ", argv)}");
                return 1;
            }
            ReplayJobs(n);
            return Done(clock);
        }
        if (mode == "stable")
        {
            Stable(sel, csharp);
            Console.WriteLine($"{_pass} passed, {_fail} failed ({clock.Elapsed.TotalSeconds:F0} s)");
            return _fail == 0 ? 0 : 1;
        }

        var settings = new NetworkSettings { SqlHost = "127.0.0.1", SqlPort = dbPort_, SqlLogin = "root", SqlPassword = "root", SqlDatabase = dbName_ };
        StateConnectionString = settings.BuildConnectionString();
        using var database = new MySqlDatabase(settings);
        try
        {
            var probe = database.Execute("SELECT 1", Array.Empty<BoundValue>());
            if (probe == null || !probe.Next()) { Console.WriteLine($"DB {dbName_} not reachable"); return 3; }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DB {dbName_} not reachable ({ex.GetType().Name}: {ex.Message})");
            return 3;
        }
        if (dbName_ != DefaultDb) Console.WriteLine($"lane {dbName_}");

        try
        {
            Setup(database);
            if (mode == "world-snapshot")
            {
                WriteWorldSnapshot();
                return Done(clock);
            }
            if (mode == "startup")
            {
                // startup locks no char: its world tables are the process's own
                OpenWorldTables($"p{Environment.ProcessId}");
                Startup(synthetic);
                return Done(clock);
            }
            // harness-speed: replay <selection> --no-gate parses only the flows it picks (LoadSelected)
            var all = LazyMode(mode, sel, noGate)
                ? LoadSelected(FlowPaths(), sel, path => Flow.Load(path, NpcInZoneData, ItemIsWeapon, ItemExists), Flow.LoadStub)
                : LoadFlows(NpcInZoneData, ItemIsWeapon, ItemExists);
            if (all == null) return Done(clock);
            if (mode == "item-stacks")
            {
                PrintItemStacks(all);
                return Done(clock);
            }
            // a setup item line's stack rules (carry-harness-2): a second Rare row refused, the exempt list not stale
            if (!ItemStackGate(all)) return Done(clock);
            var chosen = Select(all, sel);
            if (chosen == null) return Done(clock);
            // LoadSelected parsed every flow the selector picks: one it did not is a bug, never a run on a stub
            if (chosen.FirstOrDefault(f => f.Stub) is { } stub)
                throw new InvalidOperationException($"{stub.Name}: picked, but LoadSelected left it unparsed");
            if (mode == "replay")
            {
                if (shard != null)
                {
                    var p = shard.Split('/');
                    if (p.Length != 2 || !int.TryParse(p[0], out int i) || !int.TryParse(p[1], out int n) || i < 1 || i > n)
                    {
                        Fail("--shard", $"takes i/n with i from 1 to n, not {shard}");
                        return Done(clock);
                    }
                    // by expected cost (quest rework P8): every child deals the same split, its own flows kept in name order
                    var costs = chosen.Select(FlowCost).ToList();
                    var mine = ShardsByCost(costs, chosen.Select(f => f.Name).ToList(), n)[i - 1].ToHashSet();
                    long expected = mine.Sum(k => costs[k]);
                    chosen = chosen.Where((f, k) => mine.Contains(k)).ToList();
                    Console.WriteLine($"shard {i}/{n}: {chosen.Count} traces (expected {expected / 1000} s)");
                    if (!SetShardRange(i, n)) return Done(clock);
                }
                if (!FitsRange(chosen)) return Done(clock);
                var ids = chosen.SelectMany(f => CharsFor(shard != null ? chosen.IndexOf(f) : all.IndexOf(f), f.Chars.Count)).ToList();
                if (!LockChars(settings, ids)) return Done(clock);
                OpenWorldTables($"c{ids.Min()}");
                handlerCalled_ = (key, label) => coverage_.Add(key, label, replaying_);
                // --no-gate (harness-speed): a selection's replay skips the startup gate, and says so
                if (noGate) Console.WriteLine(NoGateLine);
                else Gate();
                CleanupPartyCheck(ids);
                var store = PacketStore.Load(GoldenDir);
                // --fail-fast: the first failing flow (or the startup gate) stops the run
                string stoppedAt = failFast && _fail > 0 ? GateStopLabel(noGate) : null;
                var ran = new List<string>();
                // a shard's flows are indexed in the shard's list, the others in all's (the ids they always have)
                var index = shard != null ? chosen : all;
                foreach (var f in chosen)
                {
                    if (stoppedAt != null) break;
                    int failedBefore = _fail;
                    Replay(f, index.IndexOf(f), store);
                    ran.Add(f.Name);
                    if (failFast && _fail > failedBefore) stoppedAt = f.Name;
                }
                if (stoppedAt != null)
                    Console.WriteLine(FailFastSummary(stoppedAt, ran, chosen.Select(f => f.Name).ToList()));
                else
                {
                    if (sel == "all" && shard == null) OrphanTraces(all);
                    bool every = sel == "all" && shard == null;
                    if (countsPath == null)
                        CoverageGate(coverage_, all, every ? CoverageGateKeys() : null,
                                     every ? CSharpModuleFiles() : null);
                }
            }
            else
            {
                if (!FitsRange(chosen)) return Done(clock);
                var ids = chosen.SelectMany(f => CharsFor(all.IndexOf(f), f.Chars.Count)).ToList();
                if (!LockChars(settings, ids)) return Done(clock);
                OpenWorldTables($"c{ids.Min()}");
                string dir = outDir ?? GoldenDir;
                Directory.CreateDirectory(dir);
                var store = PacketStore.Load(dir);
                try
                {
                    foreach (var f in chosen) RecordFlow(f, all.IndexOf(f), mode == "record", dir, sel != "all", store);
                }
                finally
                {
                    // the store again, also after a throw part-way: the packets every trace in the folder references (the
                    // ones rewritten so far included), the stale ones gone
                    var missing = store.Save(dir);
                    if (missing.Count > 0) Fail(PacketStore.FileName, $"traces reference packets the store lacks: {string.Join(", ", missing.Take(10))}");
                    else Pass($"{Path.Combine(dir, PacketStore.FileName)} written");
                }
            }
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FAIL exception: {ex}");
        }
        finally
        {
            scripts?.Dispose();
            scripts = null;
            DeleteOverlay();
            CloseSnapshot();
            // while the locks are held: the tables are the lock holder's
            DropWorldTables();
            UnlockChars();
        }
        if (countsPath != null)
        {
            coverage_.Pass = _pass;
            coverage_.Fail = _fail;
            File.WriteAllText(countsPath, coverage_.Write());
        }
        return Done(clock);
    }

    // a trace in tests/golden whose flow is gone fails
    private static void OrphanTraces(List<Flow> all)
    {
        foreach (var orphan in Directory.GetFiles(GoldenDir, "*.trace").Select(Path.GetFileNameWithoutExtension)
                     .Where(t => all.All(f => f.Name != t)).OrderBy(t => t, StringComparer.Ordinal))
            Fail(orphan, "a trace with no flow");
    }

    // B3a's startup gate (at most 1.10 x the commit before it): six fresh worlds, each timed from the script engine's
    // creation through InitScripts to the first zone (Bastok Markets) loaded; the first is the cold one (the JIT of the
    // engine, the bindings and every quest's Build paid), the median of the other five the warm figure. --synthetic n: the
    // same six again with n synthetic quests, and the projection
    private static void Startup(int synthetic)
    {
        var baseline = StartupRuns("startup");
        if (synthetic == 0)
        {
            Pass("startup measured");
            return;
        }
        Console.WriteLine($"building {synthetic} synthetic quests");
        var set = SyntheticQuests(synthetic);
        Console.WriteLine($"synthetic: {set.Containers} container quests, {set.CallOnly} call-only ({set.OnLuaFile} on a constant whose Lua file is " +
                          $"in the list), NPCs in {set.Zones} zones");
        synthetic_ = set.Quests;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        StartupSeries runs;
        try
        {
            runs = StartupRuns("synthetic startup", set.Hidden);
        }
        finally
        {
            synthetic_ = null;
        }
        Check("synthetic: every one registered", baseline.Registered + synthetic, runs.Registered);
        // the call-only quests' Called() NPCs: no script calls a synthetic key (the narrow startup check says so per NPC)
        Console.WriteLine($"synthetic: {runs.NpcCallLines - baseline.NpcCallLines} more `quests: npc call:` lines (no NPC script calls a synthetic quest)");
        var cost = SyntheticCost(synthetic, baseline.WarmReg, runs.WarmReg, runs.ColdReg);
        cost.Hidden = set.Hidden.Count;
        Console.WriteLine("projection against this build's own startup (no --against)");
        PrintProjection(cost, baseline.Warm, baseline.Cold, baseline.Warm, baseline.Cold, 1.0);
    }

    // the figures of one series of six worlds: the cold (first) run and the median of the others, the registration line's
    // ms of each, the C# quests registered (the last world's) and its `quests: npc call:` lines
    private sealed class StartupSeries
    {
        public double Cold, Warm, ColdReg, WarmReg;
        public int Registered, NpcCallLines;
    }

    // six worlds (hidden: the Lua files they skip); prints `<label> runs` (the warm ones), `<label> cold`, `<label> median`
    // and `<label> registration`. A quests: line refusing a quest or failing one fails the series
    private static StartupSeries StartupRuns(string label, IEnumerable<string> hidden = null)
    {
        var runs = new List<double>();
        var regs = new List<double>();
        var refused = new List<string>();
        var r = new StartupSeries();
        for (int i = 0; i < 6; i++)
        {
            NewWorld(hidden);
            zone.ZoneInfoOf(235);
            runs.Add(worldClock_.Elapsed.TotalMilliseconds);
            var line = log.Select(RegisteredLine).LastOrDefault(x => x != null);
            if (line == null)
                throw new InvalidOperationException($"{label}: no `quests: <q> C# quests, <m> C# missions and <b> C# " +
                                                    "battlefields registered in <ms> ms` line");
            regs.Add(line.Value.Ms);
            r.Registered = line.Value.Count;
            r.NpcCallLines = log.Count(l => l.StartsWith("quests: npc call: ", StringComparison.Ordinal));
            refused.AddRange(log.Where(l => l.StartsWith("quests: ", StringComparison.Ordinal) &&
                                            (l.Contains(" not registered") || l.Contains("failed to load"))));
        }
        r.Cold = runs[0];
        r.ColdReg = regs[0];
        r.Warm = Median(runs.Skip(1));
        r.WarmReg = Median(regs.Skip(1));
        Console.WriteLine($"{label} runs " + string.Join(" ", runs.Skip(1).OrderBy(x => x).Select(x => x.ToString("F0"))) + " ms");
        Console.WriteLine($"{label} cold {r.Cold:F0} ms");
        Console.WriteLine($"{label} median {r.Warm:F0} ms");
        Console.WriteLine($"{label} registration cold {r.ColdReg:F0} ms, median {r.WarmReg:F0} ms ({r.Registered} C# quests)");
        Check($"{label}: no quest refused", "", string.Join(" | ", refused.Distinct().Take(5)));
        return r;
    }

    // the synthetic quests' registration: the warm median's and the first synthetic world's, each against the baseline's
    // warm median per quest (the first synthetic world runs each quest's Build for the first time: its JIT, as a cold start
    // pays it, on a bridge already warm)
    private sealed class SyntheticCostFigures
    {
        public int N;
        public double WarmReg, ColdReg, WarmUs, ColdUs;
        // the Lua quest files the synthetic worlds hid (the container quests' own)
        public int Hidden;
    }

    private static SyntheticCostFigures SyntheticCost(int n, double baselineWarmReg, double warmReg, double coldReg) => new()
    {
        N = n,
        WarmReg = warmReg,
        ColdReg = coldReg,
        WarmUs = PerQuestUs(warmReg, baselineWarmReg, n),
        ColdUs = PerQuestUs(coldReg, baselineWarmReg, n),
    };

    // `synthetic <n> quests: ...` (warm, then cold) and `projected <n'> quests: ...` for 75 and 1000 more quests
    private static void PrintProjection(SyntheticCostFigures c, double warm, double cold, double againstWarm, double againstCold, double scale)
    {
        Console.WriteLine($"synthetic {c.N} quests: {c.Hidden} Lua quest files hidden");
        Console.WriteLine($"synthetic {c.N} quests: {c.WarmReg:F0} ms registration, {c.WarmUs:F0} us per quest");
        Console.WriteLine($"synthetic {c.N} quests cold: {c.ColdReg:F0} ms registration, {c.ColdUs:F0} us per quest (info only)");
        foreach (int more in new[] { 75, 1000 })
            Console.WriteLine($"projected {more} quests: warm ratio {ProjectedRatio(warm, c.WarmUs, againstWarm, more, scale):F3}, " +
                              $"cold ratio {ProjectedRatio(cold, c.ColdUs, againstCold, more, scale):F3} (info only)");
    }

    // the commit whose startup was measured 1.045x pre-B3a (the B3a merge): the ratio against it is scaled to pre-B3a
    private const string B3aMerge = "156ff1e6";

    // startup --against <commit>: the commit built in a detached temp worktree beside the repo, then 5 rounds of a child
    // `startup` probe of this build and of the commit, interleaved so machine drift hits both; the median of each side's
    // round figures, warm (the gate, as B3a measured it) and cold (each process's first world). For the B3a merge,
    // ratio = 1.045 * this / that, both; only the warm one is gated (1.10 x pre-B3a), the cold one is information
    // (StartupGateFailure). --synthetic n: this build's probes add the synthetic
    // series, and the projection is against the commit's figures
    private static int StartupAgainst(string commit, int synthetic)
    {
        SweepTempDirs();
        string tmp = Path.Combine(Path.GetDirectoryName(Root), TempDirName("_startup-"));
        bool added = false;
        try
        {
            RunTool("git", Root, "worktree", "add", "-q", "--detach", tmp, commit);
            added = true;
            foreach (var d in new[] { "navmeshes", "ximeshes" })
                if (Directory.Exists(Path.Combine(Root, d))) RunTool("cp", Root, "-rl", d, Path.Combine(tmp, d));
            foreach (var f in new[] { "login.cert", "login.key", "profile.cert", "profile.key" })
                if (File.Exists(Path.Combine(Root, f))) File.Copy(Path.Combine(Root, f), Path.Combine(tmp, f));
            // a build with no cold line prints its first run too (the measured code untouched)
            string harness = Path.Combine(tmp, "tests", "XI.Verify.QuestGolden.Live", "Program.cs");
            if (File.Exists(harness) && !File.ReadAllText(harness).Contains("startup cold", StringComparison.Ordinal) &&
                PatchFirstRun(File.ReadAllText(harness)) is { } patched)
            {
                File.WriteAllText(harness, patched);
                Console.WriteLine($"{commit}: its startup mode patched to print its first run (`startup first`)");
            }
            // a build from before the lanes takes --db as the port and names xidb itself: on another lane its database
            // is patched to the lane's (only the database it opens; the measured code untouched)
            bool otherLanes = File.Exists(harness) && File.ReadAllText(harness).Contains("\"--port\"", StringComparison.Ordinal);
            if (!otherLanes && dbName_ != DefaultDb && File.Exists(harness))
            {
                if (PatchLaneDb(File.ReadAllText(harness), dbName_) is { } onLane)
                {
                    File.WriteAllText(harness, onLane);
                    Console.WriteLine($"{commit} predates the lanes: its harness patched to open {dbName_}");
                }
                else Console.WriteLine($"{commit} predates the lanes: its probes run on {DefaultDb}, not {dbName_}");
            }
            Console.WriteLine($"building {commit} in {tmp}");
            RunTool("dotnet", tmp, "build", Path.Combine("tests", "XI.Verify.QuestGolden.Live"), "-c", "Release", "-v", "q", "--nologo");
            string otherDll = Directory.GetFiles(Path.Combine(tmp, "tests", "XI.Verify.QuestGolden.Live", "bin", "Release"),
                "XI.Verify.QuestGolden.Live.dll", SearchOption.AllDirectories).First();
            string thisDll = typeof(Program).Assembly.Location;
            var mine = new List<(double Cold, double Warm)>();
            var theirs = new List<(double Cold, double Warm)>();
            var outputs = new List<string>();
            bool oldFormat = false;
            for (int round = 1; round <= 5; round++)
            {
                string a = ProbeStartup(thisDll, synthetic, true), b = ProbeStartup(otherDll, 0, otherLanes);
                var pa = ParseProbe(a);
                var pb = ParseProbe(b);
                oldFormat |= !Regex.IsMatch(b, @"^startup (cold|first) \d+ ms", RegexOptions.Multiline);
                mine.Add(pa);
                theirs.Add(pb);
                outputs.Add(a);
                Console.WriteLine($"round {round}: this {pa.Warm:F0} ms (cold {pa.Cold:F0}), {commit} {pb.Warm:F0} ms (cold {pb.Cold:F0})" +
                                  (synthetic > 0 ? $", with {synthetic} synthetic quests {Figure(a, "synthetic startup median"):F0} ms" : ""));
            }
            if (oldFormat) Console.WriteLine($"{commit} prints no cold run: its slowest run is taken as the cold one, the median of the other 4 as warm");
            double myCold = Median(mine.Select(x => x.Cold)), myWarm = Median(mine.Select(x => x.Warm));
            double theirCold = Median(theirs.Select(x => x.Cold)), theirWarm = Median(theirs.Select(x => x.Warm));
            Console.WriteLine($"startup cold {myCold:F0} ms");
            Console.WriteLine($"startup cold {commit} {theirCold:F0} ms");
            Console.WriteLine($"startup median {myWarm:F0} ms");
            Console.WriteLine($"startup median {commit} {theirWarm:F0} ms");
            // this build's registration line over the rounds, for the batch report (porting recipe, B3a spec section 5)
            Console.WriteLine($"startup registration cold {Median(outputs.Select(o => Figure(o, "startup registration cold"))):F0} ms, " +
                              $"median {Median(outputs.Select(o => Figure(o, "startup registration cold", 2))):F0} ms " +
                              $"({Figure(outputs[0], "startup registration cold", 3):F0} C# quests)");
            bool b3a = commit.StartsWith(B3aMerge, StringComparison.Ordinal) || B3aMerge.StartsWith(commit, StringComparison.Ordinal);
            double scale = b3a ? 1.045 : 1.0;
            double ratio = myWarm / theirWarm * scale, coldRatio = myCold / theirCold * scale;
            if (b3a)
            {
                Console.WriteLine($"startup ratio {ratio:F3} (pre-B3a, limit 1.10)");
                Console.WriteLine($"startup cold ratio {coldRatio:F3} (pre-B3a, info only)");
                if (StartupGateFailure(ratio, coldRatio) is { } failure) Fail("startup ratio", failure);
                else Pass("startup ratio within 1.10 x pre-B3a");
            }
            else
            {
                Console.WriteLine($"startup ratio {ratio:F3} (against {commit})");
                Console.WriteLine($"startup cold ratio {coldRatio:F3} (against {commit}, info only)");
            }
            if (synthetic > 0)
            {
                // per round: the baseline's warm registration, the synthetic series' warm and first-world registration
                var cost = SyntheticCost(synthetic,
                    Median(outputs.Select(o => Figure(o, "startup registration cold", 2))),
                    Median(outputs.Select(o => Figure(o, "synthetic startup registration cold", 2))),
                    Median(outputs.Select(o => Figure(o, "synthetic startup registration cold"))));
                cost.Hidden = HiddenCount(outputs[0], synthetic);
                double synWarm = Median(outputs.Select(o => Figure(o, "synthetic startup median")));
                // warm against warm: the synthetic series' median of its worlds 2-6 against the commit's median of its warm runs
                Console.WriteLine($"synthetic startup median {synWarm:F0} ms (measured with the {synthetic} quests on top: ratio {synWarm / theirWarm * scale:F3})");
                // a cold start with the quests: the first synthetic world runs in a process the baseline already warmed, so
                // per round its increment over this build's warm median is added to this build's cold run, then the median
                double synColdEstimate = Median(outputs.Select(o => SyntheticColdEstimate(Figure(o, "startup cold"), Figure(o, "startup median"),
                                                                                        Figure(o, "synthetic startup cold"))));
                Console.WriteLine($"synthetic startup cold estimate {synColdEstimate:F0} ms: ratio {synColdEstimate / theirCold * scale:F3} (info only)");
                PrintProjection(cost, myWarm, myCold, theirWarm, theirCold, scale);
            }
        }
        catch (Exception ex)
        {
            Fail("startup --against", ex.Message);
        }
        finally
        {
            if (added)
                try { RunTool("git", Root, "worktree", "remove", "--force", tmp); }
                catch (Exception ex) { Console.WriteLine($"could not remove {tmp}: {ex.Message}"); }
        }
        Console.WriteLine($"{_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    // a probe's `synthetic <n> quests: <h> Lua quest files hidden`
    private static int HiddenCount(string output, int n)
    {
        var m = Regex.Match(output, $@"^synthetic {n} quests: (\d+) Lua quest files hidden", RegexOptions.Multiline);
        if (!m.Success) throw new InvalidOperationException("no hidden count in:" + Environment.NewLine + output);
        return int.Parse(m.Groups[1].Value);
    }

    // the registration line, `quests: <q> C# quests, <m> C# missions and <b> C# battlefields registered in <ms> ms`
    // (ZoneHost.RegisterQuestScripts)
    private static readonly Regex RegisteredRe =
        new(@"^quests: (\d+) C# quests, (\d+) C# missions and (\d+) C# battlefields registered in (\d+) ms$");

    // the counts and the ms of a registered line; null for any other line
    private static (int Count, int Missions, int Battlefields, int Ms)? RegisteredLine(string line) =>
        line != null && RegisteredRe.Match(line) is { Success: true } m
            ? (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value),
               int.Parse(m.Groups[4].Value))
            : null;

    // a child `startup` run's cold (first) run and warm median, in ms. A build from before the cold line (B3a's) runs 5
    // worlds and prints them sorted (its median counts the cold one): patched by PatchFirstRun it prints its first run too,
    // the cold one, and the median of the other 4 is its warm figure; unpatched, its slowest run is taken as the cold one
    private static (double Cold, double Warm) ParseProbe(string output)
    {
        var cold = Regex.Match(output, @"^startup cold (\d+) ms", RegexOptions.Multiline);
        var median = Regex.Match(output, @"^startup median (\d+) ms", RegexOptions.Multiline);
        if (cold.Success && median.Success) return (double.Parse(cold.Groups[1].Value), double.Parse(median.Groups[1].Value));
        var runs = Regex.Match(output, @"^startup runs ([\d ]+) ms", RegexOptions.Multiline);
        if (!runs.Success) throw new InvalidOperationException("no startup runs in:" + Environment.NewLine + output);
        var all = runs.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(double.Parse).OrderBy(x => x).ToList();
        var first = Regex.Match(output, @"^startup first (\d+) ms", RegexOptions.Multiline);
        int at = first.Success ? all.IndexOf(double.Parse(first.Groups[1].Value)) : all.Count - 1;
        if (at < 0) throw new InvalidOperationException("the first run is not among the runs in:" + Environment.NewLine + output);
        double coldRun = all[at];
        all.RemoveAt(at);
        return (coldRun, Median(all));
    }

    // a harness from before the lanes with its one `SqlDatabase = "xidb"` naming the lane's database instead; null when
    // it has not exactly one
    internal static string PatchLaneDb(string source, string db)
    {
        const string named = "SqlDatabase = \"xidb\"";
        int at = source.IndexOf(named, StringComparison.Ordinal);
        if (at < 0 || source.IndexOf(named, at + 1, StringComparison.Ordinal) >= 0) return null;
        return source.Substring(0, at) + $"SqlDatabase = \"{db}\"" + source.Substring(at + named.Length);
    }

    // B3a's Startup (5 runs sorted, no cold line) with a line printing its first run before the sort; null when the
    // source has no single such sort. Only the temp tree of startup --against is patched, and only what it prints
    private static string PatchFirstRun(string source)
    {
        const string sort = "        runs.Sort();\n";
        int at = source.IndexOf(sort, StringComparison.Ordinal);
        if (at < 0 || source.IndexOf(sort, at + 1, StringComparison.Ordinal) >= 0) return null;
        return source.Insert(at, "        Console.WriteLine($\"startup first {runs[0]:F0} ms\");\n");
    }

    private static double Median(IEnumerable<double> values)
    {
        var v = values.OrderBy(x => x).ToList();
        return v.Count % 2 == 1 ? v[v.Count / 2] : (v[v.Count / 2 - 1] + v[v.Count / 2]) / 2;
    }

    // the startup gate against the B3a merge: the warm ratio over 1.10 fails (a regression tripwire); the cold ratio never
    // fails, it is printed for information (the user's decision, 2026-10-22: a cold start is rare, maintenance is long).
    // The failure's text, or null
    private static string StartupGateFailure(double warmRatio, double coldRatio) =>
        warmRatio > 1.10 ? $"{warmRatio:F3} is over 1.10" : null;

    // a cold start with the synthetic quests: this build's cold run plus what the first synthetic world took over its warm
    // median (that world's own figure is not cold: the baseline worlds before it warmed the process)
    private static double SyntheticColdEstimate(double cold, double warm, double syntheticFirst) => cold + (syntheticFirst - warm);

    // the registration cost of one quest in us: the registration ms with n more quests over the baseline's, per quest
    private static double PerQuestUs(double ms, double baselineMs, int n) => (ms - baselineMs) * 1000 / n;

    // the startup ratio with n more quests: (the baseline ms + n x the per-quest cost) over the other build's ms, scaled
    // as --against scales (1.045 against the B3a merge, 1 otherwise)
    private static double ProjectedRatio(double baselineMs, double perQuestUs, double againstMs, int n, double scale) =>
        (baselineMs + n * perQuestUs / 1000) / againstMs * scale;

    // a number of a probe's output: on the line starting with the label, its first number (group 2: its second, 3: its third)
    private static double Figure(string output, string label, int group = 1)
    {
        var m = Regex.Match(output, "^" + Regex.Escape(label) + @"\D*(\d+)(?:\D+(\d+))?(?:\D+(\d+))?", RegexOptions.Multiline);
        if (!m.Success || !m.Groups[group].Success) throw new InvalidOperationException($"no `{label}` in:" + Environment.NewLine + output);
        return double.Parse(m.Groups[group].Value);
    }

    // one child `startup` run of a build (with --synthetic n when n > 0): its output. lanes: the build takes --db <name> and
    // --port (else --db is its port, the build from before the lanes)
    private static string ProbeStartup(string dll, int synthetic, bool lanes)
    {
        var args = new List<string> { dll, "startup" };
        args.AddRange(lanes ? LaneArgs() : new[] { "--db", dbPort_.ToString() });
        if (synthetic > 0) args.AddRange(new[] { "--synthetic", synthetic.ToString() });
        return RunTool("dotnet", Path.GetDirectoryName(dll), args.ToArray());
    }

    // runs a tool to its end; its output, or a throw with the output when it exits non-zero
    private static string RunTool(string exe, string dir, params string[] args)
    {
        var psi = new ProcessStartInfo(exe) { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi);
        var err = p.StandardError.ReadToEndAsync();
        string output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new InvalidOperationException($"{exe} {string.Join(" ", args)} exited {p.ExitCode}:\n{output}\n{err.Result}");
        return output;
    }

    // runs a tool to its end: the exit code, the output's bytes as written and the error output
    private static (int Code, byte[] Output, string Error) RunBytes(string exe, string dir, params string[] args)
    {
        var psi = new ProcessStartInfo(exe) { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi);
        var err = p.StandardError.ReadToEndAsync();
        using var output = new MemoryStream();
        p.StandardOutput.BaseStream.CopyTo(output);
        p.WaitForExit();
        return (p.ExitCode, output.ToArray(), err.Result);
    }

    private static int Done(Stopwatch clock)
    {
        Console.WriteLine($"{_pass} passed, {_fail} failed ({clock.Elapsed.TotalSeconds:F0} s)");
        return _fail == 0 ? 0 : 1;
    }

    // the flow files in name order (the file names', .flow included): a flow's index in it gives its char ids
    private static List<string> FlowPaths() =>
        Directory.GetFiles(FlowsDir, "*.flow").OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal).ToList();

    // every flow, in name order (that order gives the char ids); a parse error fails the run
    private static List<Flow> LoadFlows(Flow.NpcResolver npcs, Flow.WeaponResolver weapons = null, Flow.ItemResolver items = null)
    {
        var flows = new List<Flow>();
        foreach (var path in FlowPaths())
        {
            try
            {
                flows.Add(Flow.Load(path, npcs, weapons, items));
            }
            catch (FormatException e)
            {
                Fail("parse", e.Message);
                return null;
            }
        }
        return flows;
    }

    private static List<Flow> Select(List<Flow> all, string sel)
    {
        var chosen = SelectFlows(all, sel, f => f.Name, f => f.Key, out var bad, out var why);
        if (chosen == null) Fail(bad, why);
        return chosen;
    }

    // the selector (quest rework P8): all, or a comma-separated list of flow names and keys, the flows in the order given
    // (a key's flows in all's order, its name order); a flow the list names twice (by name and by its key, or twice) runs
    // once, at its first place. Null, with the element and why, when an element names no flow or is empty, or the list
    // holds all. A base flow's name is its quest's key, so <key> picks every flow of the quest; flow:<name>
    // (carry-harness-2, the user's ruling of 2026-11-04) picks the one flow of that name only
    internal const string FlowPrefix = "flow:";
    internal static List<T> SelectFlows<T>(IReadOnlyList<T> all, string sel, Func<T, string> name, Func<T, string> key, out string bad, out string why)
    {
        bad = why = null;
        if (sel == "all") return all.ToList();
        var chosen = new List<T>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in sel.Split(','))
        {
            if (part.Length == 0)
            {
                (bad, why) = (sel, "an empty name in the list (a, b: no spaces, no comma at an end)");
                return null;
            }
            if (part == "all")
            {
                (bad, why) = (sel, "all runs alone, not in a list");
                return null;
            }
            bool one = part.StartsWith(FlowPrefix, StringComparison.Ordinal);
            string wanted = one ? part.Substring(FlowPrefix.Length) : part;
            if (one && wanted.Length == 0)
            {
                (bad, why) = (part, "flow: takes a flow's name (flow:<name>)");
                return null;
            }
            var some = all.Where(f => name(f) == wanted || !one && key(f) == wanted).ToList();
            if (some.Count == 0)
            {
                (bad, why) = (part, "no flow of that name");
                return null;
            }
            foreach (var f in some)
                if (seen.Add(name(f))) chosen.Add(f);
        }
        return chosen;
    }

    // the arguments left once the options are taken: a mode and one selector at most. A third (`replay a b c`) would be
    // dropped, the run cut to its first key and passing, so it is refused: a list is comma-joined, with no spaces
    internal static string PositionalError(IReadOnlyList<string> args) =>
        args.Count <= 2 ? null
            : $"one selector only, not {string.Join(" ", args.Skip(1))}: " +
              "join flow names and keys with commas, no spaces " +
              $"({string.Join(",", args.Skip(1))}); an unknown option is refused too";

    // --fail-fast's report of a stopped run: what ran, what failed, what did not run (the summary line follows)
    internal static string FailFastSummary(string stoppedAt, IReadOnlyList<string> ran, IReadOnlyList<string> chosen)
    {
        var notRun = chosen.Where(n => !ran.Contains(n)).ToList();
        return $"fail-fast: stopped at {stoppedAt}, the first failure; ran {ran.Count} of {chosen.Count} flows" +
               (ran.Count > 0 ? $" ({string.Join(", ", ran)})" : "") +
               $"; not run {notRun.Count}" + (notRun.Count > 0 ? $" ({string.Join(", ", notRun)})" : "") + "; the coverage gate not run";
    }

    // item wsp=<n>: the item has an item_weapon row
    private static bool ItemIsWeapon(int item)
    {
        var r = Sql(db, "SELECT 1 AS w FROM item_weapon WHERE itemId = ?", BoundValue.Of((ushort)item));
        return r != null && r.Next();
    }

    // an item by id (quest rework P8): the id has an item_basic row
    private static bool ItemExists(int item)
    {
        var r = Sql(db, "SELECT 1 AS w FROM item_basic WHERE itemid = ?", BoundValue.Of((ushort)item));
        return r != null && r.Next();
    }

    // an entity of the zone as the zone loads it: a `script:` name in data/zones/<zone>/npcs.yaml (an NPC that only a
    // quest gives handlers has no script file, so no Npc constant)
    private static readonly Dictionary<ushort, HashSet<string>> zoneNpcs_ = new();
    private static readonly System.Text.RegularExpressions.Regex ScriptLine = new(@"^\s+script:\s+(\S.*?)\s*$", System.Text.RegularExpressions.RegexOptions.Multiline);
    private static bool NpcInZoneData(ushort zoneId, string name)
    {
        if (!zoneNpcs_.TryGetValue(zoneId, out var names))
        {
            names = new HashSet<string>(StringComparer.Ordinal);
            var r = Sql(db, "SELECT name FROM zone_settings WHERE zoneid = ?", BoundValue.Of((int)zoneId));
            if (r != null && r.Next())
            {
                string file = Path.Combine(dataDir_, "zones", XI.Zone.ZoneInfo.FolderName(r.Get<string>("name")), "npcs.yaml");
                if (File.Exists(file))
                    foreach (System.Text.RegularExpressions.Match m in ScriptLine.Matches(File.ReadAllText(file)))
                        names.Add(m.Groups[1].Value.Trim('\''));
            }
            zoneNpcs_[zoneId] = names;
        }
        return names.Contains(name);
    }

    // this run's slice of the ids: all of them, or a shard's own
    private static int rangeStart_ = 0, rangeSize_ = CharCount;

    // the ids of the flow at index i with n chars (main's first): n consecutive ids of the slice, from the slice's start
    // plus i modulo (the slice's size - n + 1), so they never wrap and keep the chars' order (main lowest). One char: the
    // slice's start plus i modulo its size, as before the chars. The trace names none of them (placeholders), so a flow
    // replays the same on any ids: another shard's, another lane's
    internal static uint[] CharsFor(int index, int n, int start, int size)
    {
        if (n < 1 || n > size) throw new InvalidOperationException($"{n} chars need {n} ids; the slice has {size}");
        int from = start + index % (size - n + 1);
        return Enumerable.Range(0, n).Select(k => FirstChar + (uint)(from + k)).ToArray();
    }
    private static uint[] CharsFor(int index, int n) => CharsFor(index, n, rangeStart_, rangeSize_);

    // every flow's chars fit this run's slice of ids; else a failure
    private static bool FitsRange(List<Flow> flows)
    {
        var big = flows.FirstOrDefault(f => f.Chars.Count > rangeSize_);
        if (big == null) return true;
        Fail(big.Name, $"{big.Chars.Count} chars, but this run's slice has {rangeSize_} char ids (fewer --jobs)");
        return false;
    }

    // a flow's expected replay time in ms (quest rework P8), for ShardsByCost: fitted on lane 6's `replay all --jobs 6` of
    // 632 flows (each flow's time against its statements' verbs: r = 0.88). A flow's own cost (its world, its login, its
    // trace read and compare) is about 1.8 s; a statement that loads a zone or a char (login, warp, zoneline, rezone, the
    // party verbs) or walks or ticks adds its share, any other 15 ms. Only the split depends on it, never a trace
    private static readonly Dictionary<string, int> VerbCost = new(StringComparer.Ordinal)
    {
        ["login"] = 400, ["warp"] = 200, ["zoneline"] = 300, ["rezone"] = 150, ["route"] = 700, ["walk"] = 150,
        ["invite"] = 150, ["accept"] = 150, ["decline"] = 150, ["kill"] = 100, ["claim"] = 100, ["tick"] = 60, ["wait"] = 60,
        ["cast"] = 100, ["bfwin"] = 500, ["weather"] = 400, ["npclocalvar"] = 400, ["missionstatus"] = 250,
    };
    internal static long FlowCost(Flow f) => 1800 + f.Steps.Sum(st => VerbCost.TryGetValue(st.Verb, out int ms) ? ms : 15);

    // the flows' indices dealt into n shards by cost (quest rework P8, longest processing time first): the costliest first
    // (ties in name order) into the least-loaded shard (ties: the lower one), in that order (the caller keeps its flows in
    // name order). The same costs and names give the same split in every process. Before P8 the first zones were dealt
    // round the shards, which left two of six at twice the others' time (lane 6: 552 s against 169 to 335 s)
    internal static List<int>[] ShardsByCost(IReadOnlyList<long> costs, IReadOnlyList<string> names, int n)
    {
        var shards = Enumerable.Range(0, n).Select(_ => new List<int>()).ToArray();
        var load = new long[n];
        foreach (int k in Enumerable.Range(0, costs.Count).OrderByDescending(k => costs[k]).ThenBy(k => names[k], StringComparer.Ordinal))
        {
            int least = 0;
            for (int j = 1; j < n; j++) if (load[j] < load[least]) least = j;
            shards[least].Add(k);
            load[least] += costs[k];
        }
        return shards;
    }

    // shard i of n (1-based): the 39 ids dealt out in runs of CharCount / n, the first CharCount % n shards one longer
    private static bool SetShardRange(int i, int n)
    {
        if (n > CharCount)
        {
            Fail("--shard", $"{n} shards but only {CharCount} char ids (996161-996199): a shard would have none");
            return false;
        }
        int size = CharCount / n, extra = CharCount % n;
        rangeStart_ = (i - 1) * size + Math.Min(i - 1, extra);
        rangeSize_ = size + (i - 1 < extra ? 1 : 0);
        Console.WriteLine($"shard {i}/{n}: char ids {FirstChar + (uint)rangeStart_}-{FirstChar + (uint)(rangeStart_ + rangeSize_ - 1)}");
        return true;
    }
    private static string TracePath(string dir, Flow f) => Path.Combine(dir, f.Name + ".trace");

    // ---- one run of a flow -------------------------------------------------------------------------------------------

    // the flow on fresh chars (main's id first, CharsFor) in a new world: Lua (the quest in CSHARP_QUESTS_OFF; a probe:
    // nothing) or C# (nothing off); the problems are the script errors and every quests: log line that is not benign (an
    // unknown xi.cs.quest key, a quest that failed with no Lua fallback, a C# handler that threw)
    private static Trace RunFlow(Flow f, IReadOnlyList<uint> charIds, bool luaSide, IReadOnlyList<Mask> masks, List<string> problems)
    {
        try
        {
            return RunFlowBody(f, charIds, luaSide, masks, problems);
        }
        finally
        {
            if (world_ != null) world_.Chars = null;
            // the chars' rows go with the run (Cleanup otherwise runs only at the next flow's start): char_profile's unity
            // leader would be counted into unity_system by the hourly recount of every later trace on the lane. A failure
            // here must not hide the run's own (the exception in flight, or the problems)
            foreach (var id in charIds)
            {
                try { Cleanup(db, id); }
                catch (Exception e) { Console.WriteLine($"WARN {f.Name}: cleanup of char {id} failed: {e.Message}"); }
            }
        }
    }

    private static Trace RunFlowBody(Flow f, IReadOnlyList<uint> charIds, bool luaSide, IReadOnlyList<Mask> masks, List<string> problems)
    {
        if (charIds.Count != f.Chars.Count) throw new InvalidOperationException($"{f.Name}: {f.Chars.Count} chars, {charIds.Count} ids");
        foreach (var id in charIds) Cleanup(db, id);
        var off = luaSide ? RecordOff(f) : Array.Empty<string>();
        // a probe: the C# quests its run reaches (their handlers' calls), none of them the coverage gate's
        var probeQuests = new SortedSet<string>(StringComparer.Ordinal);
        var coverageHook = handlerCalled_;
        // RoE is no quest: it runs on C# for every record, also on a probe's Lua side
        if (IsProbe(f.Key)) handlerCalled_ = (key, _) => { if (key != ContainerKeys.RoeCoverage) probeQuests.Add(key); };
        // a probe's C# run: its test-only twins of the Lua quests it rides on (Twins.cs)
        if (!luaSide && IsProbe(f.Key)) twins_ = TwinsOf(f.Name, CSharpQuestKeys());
        var runTwinKeys = twins_?.Select(TwinKey).ToList() ?? new List<string>();
        try
        {
            NewWorld(f.Hidden, off);
        }
        finally
        {
            handlerCalled_ = coverageHook;
            twins_ = null;
        }
        if (world_ != null) world_.Chars = charIds.ToList();
        SetActors(f.Chars.Select(cs => cs.Name).ToList(), charIds);
        // more than one char: the world routes the party's IPC back (a one-char flow's world drops every message)
        if (f.Chars.Count > 1) RouteIpc();
        var t = new Trace { Name = f.Name, Seed = Seed, FlowSha = f.Sha256, Recorder = luaSide ? "lua" : "csharp-rerecord" };
        foreach (var table in HashedWorldTables) t.World.Add((table, WorldHash(table)));
        t.BlobColumns.UnionWith(BlobColumns());
        t.Masks.AddRange(masks);
        t.Chars.AddRange(f.Chars.Skip(1).Select(cs => cs.Name));
        // the C# side needs the quest in C#: a quest left to Lua would replay Lua against Lua (a probe has no quest)
        if (!luaSide && !IsProbe(f.Key) && !ContainerKeys.IsRoe(f.Key) && !CSharpQuestLive(f.Key)) problems.Add($"{f.Key}: no C# quest is registered; replay would compare Lua with Lua");
        Statement current = null;
        stepWarnings_.Clear();
        var stepLogs = new List<StepLog>();
        try
        {
            // each char's rows, main's first, from its setup lines
            for (int a = 0; a < f.Chars.Count; a++)
            {
                Use(actors_[a]);
                Setup(f, a);
            }
            Use(MainActor);
            // the flow's modules, with its settings in effect (Modules.cs)
            LoadModules(f, luaSide, problems);
            TakeReturns();
            foreach (var a in actors_) a.Pending.Clear();
            foreach (var st in f.Steps)
            {
                current = st;
                int from = log.Count;
                try
                {
                    // the step as its char; main again after it
                    Use(actors_[st.Actor]);
                    try
                    {
                        Do(st);
                    }
                    finally
                    {
                        Use(MainActor);
                    }
                    t.Steps.Add(RecordStep(st.Label, f.Lists, masks));
                }
                finally
                {
                    // the step's own log lines, for its expect-error (a step that threw too)
                    if (st.ExpectError != null)
                        stepLogs.Add(new StepLog(st.Line, st.Label, st.ExpectError, from, log.Count));
                }
            }
            current = null;
            LogoutAll();
        }
        catch (Exception e)
        {
            problems.Add($"{Path.GetFileName(f.File)}:{current?.Line.ToString() ?? "?"}: [{current?.Label ?? "the setup"}] threw {e.GetType().Name}: {e.Message}");
            try { LogoutAll(); } catch (Exception) { foreach (var a in actors_) { a.S = null; a.C = null; a.Pending.Clear(); } }
        }
        // a step that did not do what it says (ItemStep): printed, not failed
        foreach (var w in stepWarnings_) Console.WriteLine($"WARN {Path.GetFileName(f.File)}:{w}");
        Use(MainActor);
        // a flow's warning: lines (quest rework P9): each an engine warning or a double finish quests: line the run
        // must log; then not a script error
        // a step's expect-error (carry-harness-2): the error lines it expects are left out of the problems below; a
        // step that raised none of them is a problem
        var (expected, unexpected) = ExpectedErrors(log, stepLogs, luaSide, threw_, f.Key);
        problems.AddRange(unexpected.Select(p => $"{Path.GetFileName(f.File)}:{p}"));
        var logged = log.Where((_, i) => !expected.Contains(i)).ToList();
        var scriptErrors = ScriptErrors(logged);
        var declared = f.Warnings.Select(x => x.Text).ToList();
        var questLines = logged.Where(l => l.StartsWith("quests: ", StringComparison.Ordinal));
        var required = RequiredWarnings(declared, off);
        problems.AddRange(WarningProblems(required, scriptErrors.Concat(questLines).ToList()));
        problems.AddRange(scriptErrors.Where(l => !f.Warnings.Any(x => x.Text == l)).Select(l => "script error: " + l));
        // a failed quest / mission row write (a deadlock between two jobs' first logins was one) leaves the char on the legacy
        // state: a problem of the run, not a replay diff to puzzle over
        problems.AddRange(ProgressFailures().Select(l => "quest rows: " + l));
        // a char_effects write lost the same way: a problem of the run, not an effect-row diff
        problems.AddRange(PersistFailures().Select(l => "effect rows: " + l));
        // every quests: line of the run is a problem (a throwing C# handler logs one, a quest that fell back to Lua another),
        // except the benign ones of BenignQuestLines and a double finish the flow's warning: names
        problems.AddRange(QuestLineProblems(logged, declared, off));
        if (zone.QuestLoadFailures.Count > 0) problems.Add("QuestLoadFailures: " + string.Join(",", zone.QuestLoadFailures));
        // a probe's record is a Lua recording (recorder lua) only when no C# quest answered it: it turns nothing off, so a
        // quest it rides on that went to C# would be recorded under the Lua label
        if (!luaSide && IsProbe(f.Key)) problems.AddRange(TwinProblems(f.Name, f.MentionsTwin, runTwinKeys, probeQuests));
        if (luaSide && probeQuests.Count > 0)
            problems.Add($"cannot record {f.Name}: C# quests answered it ({string.Join(", ", probeQuests)}); a probe records Lua only (move it to a quest still on Lua)");
        return t;
    }

    // every char logged out, the others first, then main
    private static void LogoutAll()
    {
        foreach (var a in actors_.Skip(1).Reverse().Append(MainActor))
        {
            Use(a);
            Logout();
        }
        Use(MainActor);
    }

    // the char's fixture and its setup lines as rows (the char driven: Use)
    private static void Setup(Flow f, int actor)
    {
        var cs = f.Chars[actor];
        var setup = f.Setup.Where(st => st.Actor == actor).ToList();
        // the item lines as rows, split by the stack size but for the exempt lines (ItemStacks.cs, carry-harness-2)
        var exempt = ItemStackExempt(new List<string>());
        var rows = ItemRows(SetupItems(f, actor), ItemStack, st => exempt.ContainsKey(ItemStackKey(f, st)), null);
        var items = rows.Select(r => (r.Item, r.Qty)).ToList();
        Fixture(CharId, cs.Zone, cs.X, cs.Y, cs.Z, cs.Job, cs.Level, cs.Nation, cs.Gil, items, cs.Race);
        // sjob=<JOB> slvl=<n> (quest rework P8): the support job and its level
        if (cs.SJob > 0) SetSupportJobRow(cs.SJob, cs.SLvl);
        // item wsp=<n>: that item's row (slot 1 on, in the rows' order) gets the points in its exdata
        for (int i = 0; i < rows.Count; i++)
            if (rows[i].Source.WsPoints is int wsp) SetWsPointsRow((byte)(i + 1), wsp);
        foreach (var st in setup)
            switch (st.Verb)
            {
                case "fame": SetFameRow(st.Value, st.Amount); break;
                case "keyitem": AddKeyItemRow(st.Value); break;
                case "charvar": SetCharVarRow(st.Name, st.Amount); break;
                case "quest":
                {
                    var wire = QuestRef(st.Name);
                    SetQuestRow((int)wire.Area, wire.QuestId, st.Status);
                    break;
                }
                case "mission": SetMissionRow(st.Value, st.Amount, st.Status); break;
                case "rank": SetRankRow(cs.Nation, st.Amount, st.Value); break;
                case "title": AddTitleRow(st.Value); break;
                case "setting": SetSetting(st); break;
                case "skill": SetSkillRow(st.Value, st.Amount); break;
                case "levelcap": SetLevelCapRow(st.Amount); break;
                case "merits": SetMeritsRow(st.Amount); break;
                case "storage": SetStorageRow(st.Name, st.Amount); break;
                case "spell": SetSpellRow(st.Value); break;
                case "unity": SetUnityRow(st.Value); break;
            }
    }

    // the quests: lines a normal run logs; one place, each entry says why it is benign
    private static readonly System.Text.RegularExpressions.Regex[] BenignQuestLines =
    {
        // the registry's size, logged by every load of the quest registry
        new(@"^quests: \d+ quest keys$"),
        // how many C# quests the load registered, logged by every load
        new(@"^quests: \d+ C# quests, \d+ C# missions and \d+ C# battlefields registered in \d+ ms$"),
    };

    // the lines a record's own CSHARP_QUESTS_OFF makes (Lua side only): a recorded key with no C# quest yet (its flows are
    // recorded before the port) and the call-only quests it switched off (M6), when every key named is one it switched off
    private static bool RecordOffLine(string line, string[] off)
    {
        if (off.Length == 0) return false;
        const string m6 = "quests: call-only quests not registered (CSHARP_QUESTS / CSHARP_QUESTS_OFF): ";
        if (line.StartsWith(m6, StringComparison.Ordinal))
            return line.Substring(m6.Length).Split(", ").All(k => off.Contains(k, StringComparer.Ordinal));
        return off.Any(k => line == $"quests: {k} in CSHARP_QUESTS_OFF is not a C# quest");
    }

    // whether the last load put the key in C#: a container in CSharpQuests.live, or an NPC-call quest
    private static bool CSharpQuestLive(string key) =>
        zone.IsNpcCallQuest(key) ||
        Lua($"local live = false for _, k in pairs(CSharpQuests and CSharpQuests.live or {{}}) do if k == {LuaStr(key)} then live = true end end report(tostring(live))") == "true";

    // the quest key's log and id (the quest key registry, from xi.quest.id)
    private static QuestWireRef QuestRef(string key) =>
        loader_.Progress.Registry != null && loader_.Progress.Registry.TryGetRef(key, out var wire) ? wire : throw new InvalidOperationException($"no quest {key} in the registry");

    private static void Do(Statement st)
    {
        switch (st.Verb)
        {
            case "login": Login(open: st.Open); break;
            case "trigger":
                RefuseInEvent(st, c);
                if (st.Here) TalkHere(StatementNpc(st));
                else TalkTo(StatementNpc(st), st.At ?? Flow.TriggerDistance);
                break;
            case "trade":
                RefuseInEvent(st, c);
                TradeItems(StatementNpc(st), st.Items.ToArray());
                break;
            case "finish": Finish(OptionOf(st)); break;
            case "finish-if-event": FinishIfEvent(OptionOf(st)); break;
            case "update":
                if (st.OptionText != null) UpdateText(st.Value, st.OptionTextChar is int who ? CharName(actors_[who].Id) : st.OptionText);
                else EndEvent(st.Value, OptionOf(st), 1);
                break;
            case "zoneline": TakeZoneLine(LineTo(st.ZoneId)); break;
            case "route": Route(st.Zones); break;
            case "warp": Warp(st.X, st.Y, st.Z, st.ZoneId, OptionOf(st), st.Open); break;
            // a GM !pos to the char's own spot in its own zone: a zone change out and back in
            case "rezone": Warp(c.X, c.Y, c.Z, c.Zone, OptionOf(st), st.Open); break;
            case "walk": WalkTo(st.X, st.Y, st.Z); break;
            case "equip": Equip(InventorySlotOf(st.Value), (byte)st.Amount); break;
            case "seekeyitem": SeeKeyItem(st.Value); break;
            // a GM's call: the key item taken away (the arm of a key item gone)
            case "delkeyitem": Lua($"player:delKeyItem({st.Value})"); break;
            // a GM's !animatenpc (GetNPCByID(id):setAnimation(n)): an animation no script gives that NPC (the arm it guards)
            case "animatenpc": Lua($"GetNPCByID({StatementNpc(st)}):setAnimation({st.Amount})"); break;
            // a GM's call (!setlocalvar): the char's local var, which no script may set (the arm it guards)
            case "localvar": Lua($"player:setLocalVar({LuaStr(st.Name)}, {st.Amount})"); break;
            // a GM's call on the NPC (GetNPCByID(id):setLocalVar): a local var only a zone or mob script sets (the arm it guards)
            case "npclocalvar": Lua($"GetNPCByID({StatementNpc(st)}):setLocalVar({LuaStr(st.Var)}, {st.Amount})"); break;
            // a GM's call (!setweather): the char's zone's weather, one its table may never roll (the arm it guards)
            case "weather": Lua($"player:setWeather({st.Amount})"); break;
            case "fill-inventory": FillInventory(); break;
            case "free-inventory": FreeInventory(); break;
            case "kill":
                if (st.NoKiller) KillNoKiller(StatementMob(st));
                else KillByChar(StatementMob(st));
                break;
            case "claim":
                Collect();
                ClaimBySwing(StatementMob(st));
                break;
            case "tick": Tick(st.Value); break;
            case "wait":
                if (st.Name == "vana") vanaTicks += st.Value * VanaHour;
                else unixNow += st.Value * 86400L;
                break;
            case "logout": Logout(); break;
            // the party: an invite, its answer (the engine's own path)
            case "invite": Invite(actors_[st.Target]); break;
            case "accept": AnswerInvite(true); break;
            case "decline": AnswerInvite(false); break;
            // a setup statement after the login: a GM's Lua call on the char
            case "fame": Lua($"player:setFame({st.Value}, {st.Amount})"); break;
            case "item": ItemStep(st); break;
            case "keyitem": Lua($"player:addKeyItem({st.Value})"); break;
            case "charvar": Lua($"player:setCharVar({LuaStr(st.Name)}, {st.Amount})"); break;
            case "quest":
            {
                var wire = QuestRef(st.Name);
                string call = st.Status switch { "completed" => "completeQuest", "accepted" => "addQuest", _ => "delQuest" };
                Lua($"player:{call}({(int)wire.Area}, {wire.QuestId})");
                break;
            }
            case "mission":
                Lua($"player:{(st.Status == "completed" ? "completeMission" : "addMission")}({st.Value}, {st.Amount})");
                break;
            case "rank":
                Lua($"player:setRank({st.Amount})");
                if (st.Value >= 0) Lua($"player:setRankPoints({st.Value})");
                break;
            case "title": Lua($"player:addTitle({st.Value})"); break;
            case "setting": SetSetting(st); break;
            case "skill": Lua($"player:setSkillLevel({st.Value}, {st.Amount})"); break;
            case "levelcap": Lua($"player:setLevelCap({st.Amount})"); break;
            case "merits": Lua($"player:setMerits({st.Amount})"); break;
            case "storage": SetStorageStep(st); break;
            // quest rework P8: GM steps
            case "spell": AddSpellStep(st.Value); break;
            case "effect": AddEffectStep(st.Value); break;
            case "npcstatus": NpcStatusStep(StatementNpc(st), st.Status); break;
            case "missionstatus": MissionStatusStep(st.Value, st.Amount, st.Pos); break;
            case "currency": CurrencyStep(st.Name, st.Amount); break;
            case "bfwin": BattlefieldWinStep(); break;
            // B3b Adoulin group 4: the client's /heal, on or off
            case "heal":
                RefuseInEvent(st, c);
                HealStep(st.Amount);
                break;
            // quest rework C4
            case "cast":
                RefuseInEvent(st, c);
                CastStep(st);
                break;
            case "record":
                if (st.Status is "take" or "remove" or "claim") RefuseInEvent(st, c);
                RoeStep(st);
                break;
            case "spawn": SpawnStep(StatementMob(st)); break;
            case "unity": UnityStep(st.Value); break;
            default: throw new InvalidOperationException($"no runner for {st.Verb}");
        }
    }

    private const long VanaHour = XI.Common.VanaTime.HOUR;

    // quest rework P8 (B10): a trigger or trade sent while the char is in an event fails the run, each for its own reason.
    // A trade: the engine drops a 0x036 sent in an event (ZoneHost.Trade.cs, C++ 0x036_item_transfer.cpp: BlockedState.
    // InEvent), on Lua and C# alike, so the step would record nothing. A trigger: the engine does not drop a 0x01A in an
    // event, it runs onTrigger and queues its event behind the open one (ZoneHost.Events.cs), but a real client sends no
    // 0x01A while a cutscene is open, so the step models nothing a player can do. No flow of main did either on purpose
    // (replay all passed with the check on); GM steps stay allowed in an event (fill-inventory, a keyitem taken inside it)
    internal static void RefuseInEvent(Statement st, XI.Zone.CharData ch)
    {
        if (ch != null && ch.IsInEvent) throw new InvalidOperationException(EventOpenProblem(st.Label, st.Verb, ch.CurrentEvent.EventId));
    }
    internal static string EventOpenProblem(string label, string verb, int csid) => verb == "trade"
        ? $"{label}: event {csid} is open, and the engine drops a trade sent in an event (BlockedState.InEvent), so the step would record nothing: finish or update it first"
        : verb == "heal"
        ? $"{label}: event {csid} is open, and the engine drops a 0x0E8 sent in an event (BlockedState.InEvent), so the step would record nothing: finish or update it first"
        : verb == "record"
        ? $"{label}: event {csid} is open, and the engine drops a 0x10C to 0x10E sent in an event (BlockedState.InEvent), so the step would record nothing: finish or update it first"
        : $"{label}: event {csid} is open, and a client sends no {verb} while a cutscene is open (the engine would queue its event behind the open one, which no player can do): finish or update it first";

    // the statement's option: option=$<name> that char's id, else the number (0 when not given)
    private static uint OptionOf(Statement st) => st.OptionChar is int who ? actors_[who].Id : st.Option ?? 0;

    // the Lua file of a roe.<topic> key, its record's Legacy: the overlay takes it, and what it requires, from the commit
    internal const string RoeLuaFile = "scripts/globals/roe.lua";

    // the key a flow counts under in the coverage gate: its container's, or for every roe.<topic> flow the one RoE key
    internal static string CoverageKey(string key) => ContainerKeys.IsRoe(key) ? ContainerKeys.RoeCoverage : key;

    // a probe flow (tests/golden/flows/probe.<name>.flow, quest rework P3): a harness check, no quest's. Its key names no
    // quest: a record turns no quest off and runs the scripts at hand (no lua-at), a replay needs no C# quest, and the
    // coverage gate leaves it out. Its C# runs register its test-only twins, if it has any (Twins.cs, quest rework P5)
    internal static bool IsProbe(string key) => key.StartsWith("probe.", StringComparison.Ordinal);

    // ---- record / rerecord -------------------------------------------------------------------------------------------

    // the flow's run written as its trace, the masks (and a rerecord the lua-at) of the trace there kept. A record (the
    // Lua side) runs the Lua of the commit LuaCommit picks (through a scripts overlay unless HEAD) and writes it as the
    // lua-at; it is refused for a flow that hides Lua files or a quest whose Lua code is gone there, and when Overlay
    // refuses (HEAD into tests/golden with scripts/ not committed; a script left at HEAD that calls a quest gone off
    // with the overlay's closure; a quest half-ported at the commit); under "all" that skips the flow, by name it fails
    private static void RecordFlow(Flow f, int index, bool luaSide, string dir, bool byName, PacketStore store)
    {
        string path = TracePath(dir, f);
        string checkedIn = TracePath(GoldenDir, f);
        var header = File.Exists(checkedIn) ? Trace.Read(checkedIn, null) : null;
        var masks = header?.Masks ?? new List<Mask>();
        string luaAt = header?.LuaAt;
        try
        {
            if (luaSide)
            {
                string refusal = f.Hidden.Count > 0 ? $"cannot record {f.Key}: the flow hides {string.Join(", ", f.Hidden)}" : null;
                // a probe: the scripts at hand, no quest off and no lua-at
                if (refusal == null && IsProbe(f.Key)) luaAt = null;
                else if (refusal == null)
                {
                    // the flow's quest and the quests its modules patch: each one's Lua runs (off, from the commit)
                    var keys = RecordKeys(f);
                    var (commit, error) = LuaCommit(f.Key, luaAt_, keys.All(LuaPresentAtHead), header?.LuaAt);
                    refusal = error;
                    if (commit != null)
                    {
                        luaAt = FullCommit(commit);
                        scriptsDir_ = Overlay(keys, luaAt, IsGoldenDir(dir), out string overlayRefusal);
                        if (overlayRefusal != null) refusal = $"cannot record {f.Key}: {overlayRefusal}";
                        else NewWorld(null, RecordOff(f));
                        if (refusal == null && keys.FirstOrDefault(LuaCodeGone) is { } gone)
                            refusal = gone == f.Key ? $"cannot record {f.Key}: its Lua code is gone; use --lua-at <commit>"
                                : $"cannot record {f.Key}: the Lua code of {gone} (a module of the flow patches it) is gone; use --lua-at <commit>";
                    }
                }
                if (refusal != null)
                {
                    if (byName) Fail(f.Name, refusal);
                    else Console.WriteLine($"SKIP {f.Name}: {refusal}");
                    return;
                }
            }
            var problems = new List<string>();
            var t = RunFlow(f, CharsFor(index, f.Chars.Count), luaSide, masks, problems);
            t.LuaAt = luaAt;
            if (problems.Count > 0)
            {
                Fail($"{f.Name}: not written", string.Join("\n    ", problems));
                return;
            }
            string before = File.Exists(path) ? File.ReadAllText(path) : null;
            // --explain: against the trace there, else the checked-in one (its packets in the golden store)
            string old = File.Exists(path) ? path : File.Exists(checkedIn) ? checkedIn : null;
            if (explain_ && old != null && t.Write(store) != File.ReadAllText(old)) Explain(old, t, old == path ? store : PacketStore.Load(GoldenDir));
            bool same = t.Save(path, store) == before;
            Pass($"{f.Name}: {t.Steps.Count} steps written to {path}{(same ? " (unchanged)" : "")}");
        }
        finally
        {
            scriptsDir_ = null;
        }
    }

    // the trace there against the new run, the differences grouped (Trace.Explain); nothing when only the recorder line
    // (or the formatting) differs
    private static void Explain(string path, Trace now, PacketStore store)
    {
        try
        {
            var lines = Trace.Explain(Trace.Read(path, store), now);
            if (lines.Count == 0) return;
            Console.WriteLine($"{now.Name}: the trace changes:");
            foreach (var l in lines) Console.WriteLine($"  {l}");
        }
        catch (FormatException e)
        {
            Console.WriteLine($"{now.Name}: the trace there does not read: {e.Message}");
        }
    }

    // the quest's Lua code is gone from the scripts the engine runs: the OFF switch found no Lua file, the C# quest's
    // LegacyPath file is absent, or (an NPC-call quest) a called NPC's script or its zone's Zone.lua calls xi.cs.quest /
    // questEvent with the key, so its Lua branches were replaced by the calls (the xi.quest.id status reads that
    // legitimately stay don't count). Quest rework C2: a C# mission's the same way, its called handlers' scripts with
    // its zones' battlefield files too (a mission's call in place may stand in one, as the call scan reads them); a C#
    // battlefield's by its LegacyPath file
    private static bool LuaCodeGone(string key)
    {
        if (ContainerKeys.IsRoe(key)) return !File.Exists(Path.Combine(scripts.ScriptsDir, "globals", "roe.lua"));
        if (log.Any(l => l == $"quests: {key} in CSHARP_QUESTS_OFF has no Lua file; it stays in C#")) return true;
        string scriptRoot = Path.GetDirectoryName(scripts.ScriptsDir);
        if (CSharpContainerOf(key) is not { } c) return false;
        if (c.LegacyPath != null && !File.Exists(Path.Combine(scriptRoot, c.LegacyPath + ".lua"))) return true;
        var def = c switch
        {
            QuestScript q => q.Compile(),
            MissionScript m => m.Compile().Core,
            _ => null,
        };
        if (def == null || !def.HasCalled) return false;
        foreach (var zoneHandlers in def.States.SelectMany(st => st.Handlers).Where(def.IsCalled).GroupBy(h => h.Zone))
        {
            var r = Sql(db, "SELECT name FROM zone_settings WHERE zoneid = ?", BoundValue.Of((int)zoneHandlers.Key));
            if (r == null || !r.Next()) continue;
            string zoneName = r.Get<string>("name");
            string zoneDir = Path.Combine(scripts.ScriptsDir, "zones", zoneName);
            var files = zoneHandlers.Where(h => h.Entity != null).Select(h => h.Entity).Distinct()
                .SelectMany(e => new[]
                {
                    Path.Combine(zoneDir, "npcs", e + ".lua"), Path.Combine(zoneDir, "mobs", e + ".lua"),
                })
                .Append(Path.Combine(zoneDir, "Zone.lua"));
            string bfDir = ContainerFolders.BattlefieldDir(Path.Combine(scripts.ScriptsDir, "battlefields"), zoneName);
            if (c is MissionScript && Directory.Exists(bfDir))
                files = files.Concat(Directory.GetFiles(bfDir, "*.lua").OrderBy(p => p, StringComparer.Ordinal));
            if (files.Where(File.Exists).Any(file => CallsQuest(File.ReadAllText(file), key))) return true;
        }
        return false;
    }

    // whether the Lua text calls xi.cs.quest( / questEvent( / questZoneIn( with the key as a string literal, outside a
    // comment; or (quest rework C2) xi.cs.mission( / missionEvent( / missionZoneIn(, the mission calls of that shape
    internal static bool CallsQuest(string text, string key)
    {
        var call = new Regex(@"\bxi\.cs\.(quest|questEvent|questZoneIn|mission|missionEvent|missionZoneIn)" +
                             @"\s*\([^)]*?(['""])" + Regex.Escape(key) + @"\2");
        foreach (Match m in call.Matches(text))
        {
            int lineStart = text.LastIndexOf('\n', m.Index) + 1;
            if (!text.Substring(lineStart, m.Index - lineStart).Contains("--", StringComparison.Ordinal)) return true;
        }
        return false;
    }

    // the keys the Lua text calls xi.cs.quest( / questEvent( / questZoneIn( with (each call's first string literal),
    // outside a comment; the mission calls' keys too (quest rework C2)
    internal static SortedSet<string> CalledKeys(string text)
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Match m in CallKey.Matches(text))
        {
            int lineStart = text.LastIndexOf('\n', m.Index) + 1;
            if (!text.Substring(lineStart, m.Index - lineStart).Contains("--", StringComparison.Ordinal)) keys.Add(m.Groups[3].Value);
        }
        return keys;
    }
    private static readonly Regex CallKey =
        new(@"\bxi\.cs\.(quest|questEvent|questZoneIn|mission|missionEvent|missionZoneIn)" +
            @"\s*\([^)'""]*(['""])([^'""\n]*)\2");

    // ---- recording from history ---------------------------------------------------------------------------------------

    // the commit whose Lua a record runs: --lua-at, else HEAD when the quest's Lua is there, else the checked-in trace's
    // lua-at; null with the refusal when none
    internal static (string Commit, string Error) LuaCommit(string key, string luaAt, bool presentAtHead, string traceLuaAt) =>
        luaAt != null ? (luaAt, null)
        : presentAtHead ? ("HEAD", null)
        : traceLuaAt != null ? (traceLuaAt, null)
        : (null, $"cannot record {key}: its Lua code is gone; use --lua-at <commit>");

    // the container key's Lua id constant, the keys' rules backwards:
    //   a quest's: area.const_lower -> xi.quest.id.<area>.<CONST> (QuestKeyRegistry.KeyFor);
    //   a mission's: mission.<log>.const_lower -> xi.mission.id.<log>.<CONST> (ContainerKeys.Mission);
    //   a battlefield's: battlefield.const_lower -> xi.battlefield.id.<CONST> (ContainerKeys.Battlefield).
    // A mission key's log is xi.mission.area's name of the log
    internal static string LuaIdConstant(string key)
    {
        if (ContainerKeys.IsRoe(key)) return "xi.roe.records";
        string table = "xi.quest.id";
        if (ContainerKeys.IsMission(key)) (table, key) = ("xi.mission.id", key.Substring("mission.".Length));
        else if (ContainerKeys.IsBattlefield(key))
            return "xi.battlefield.id." + key.Substring("battlefield.".Length).ToUpperInvariant();
        int dot = key.IndexOf('.');
        return $"{table}.{key.Substring(0, dot)}.{key.Substring(dot + 1).ToUpperInvariant()}";
    }

    // whether the Lua text names the constant as a whole token (GROCERIES, not GROCERIES_2)
    internal static bool RefersTo(string text, string constant) =>
        Regex.IsMatch(text, @"(?<![\w.])" + Regex.Escape(constant) + @"(?![\w])");

    // the keys of the C# quests of XI.QuestContent (all golden quests: the test-only ones live in the self-test)
    private static List<string> CSharpQuestKeys() =>
        CSharpQuests().Select(q => q.Key).ToList();

    // the keys of every C# container of XI.QuestContent, every kind (quest rework C2): the coverage gate's list, so a
    // C# mission or battlefield with no flow fails as a quest does
    private static List<string> CSharpContainerKeys() =>
        CSharpContainers().Select(c => c.Key).ToList();

    // the coverage gate's keys: every C# container's, and RoE's now its Lua is gone
    private static List<string> CoverageGateKeys() =>
        CSharpContainerKeys().Append(ContainerKeys.RoeCoverage).ToList();

    // the quest's key (ContainerScript.Key): <xi.quest.area[log]>.<const>, or a hidden quest's hiddenQuests.<file>
    // (quest rework P9)
    internal static string GoldenKey(QuestScript q) => q.Key;

    // the C# modules of XI.QuestContent (quest rework P4), made once
    private static List<QuestModule> csharpModules_;
    private static List<QuestModule> CSharpModules() =>
        csharpModules_ ??= typeof(XI.QuestContent.Zone).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(QuestModule).IsAssignableFrom(t))
            .Select(t => (QuestModule)Activator.CreateInstance(t)).ToList();

    // the Lua files they port
    private static List<string> CSharpModuleFiles() =>
        CSharpModules().Select(m => m.LuaFile).OrderBy(f => f, StringComparer.Ordinal).ToList();

    // the C# container of the key (a quest, a mission or a battlefield), null when it has none
    private static ContainerScript CSharpContainerOf(string key) =>
        CSharpContainers().FirstOrDefault(c => c.Key == key);

    // the folder of scripts/ that holds the Lua files of the key's kind: scripts/quests, scripts/missions or
    // scripts/battlefields (quest rework C2)
    internal static string KindScripts(string key) =>
        ContainerKeys.IsMission(key) ? "scripts/missions"
        : ContainerKeys.IsBattlefield(key) ? "scripts/battlefields"
        : "scripts/quests";

    // an NPC / mob / zone script (scripts/zones/<zone>/{npcs,mobs}/<name>.lua, Zone.lua)
    private static readonly Regex EntityScript = new(@"^scripts/zones/[^/]+/((npcs|mobs)/[^/]+|Zone)\.lua$");
    // a battlefield file (scripts/battlefields/<zone folder>/<name>.lua), where a mission's call in place may stand
    private static readonly Regex BattlefieldFile = new(@"^scripts/battlefields/[^/]+/[^/]+\.lua$");

    // the container's Lua is in the working tree: its LegacyPath file is there; or (no LegacyPath) an NPC / mob / zone
    // script names its Lua id constant (LuaIdConstant; for a container with no C# class a file of its kind's folder
    // too, KindScripts) and, for a C# NPC-call quest, none calls xi.cs.quest / questEvent with the key (its Lua
    // branches replaced by the calls). A mission's scripts are those and the battlefield files (quest rework C2)
    private static bool LuaPresentAtHead(string key)
    {
        if (ContainerKeys.IsRoe(key)) return File.Exists(Path.Combine(Root, RoeLuaFile));
        var c = CSharpContainerOf(key);
        if (c?.LegacyPath != null) return File.Exists(Path.Combine(Root, c.LegacyPath + ".lua"));
        string constant = LuaIdConstant(key);
        bool mission = ContainerKeys.IsMission(key);
        var callSites = new List<string> { "scripts/zones" };
        if (mission) callSites.Add("scripts/battlefields");
        var paths = new List<string>(callSites);
        if (c == null && !paths.Contains(KindScripts(key))) paths.Add(KindScripts(key));
        bool CallSite(string p) => EntityScript.IsMatch(p) || mission && BattlefieldFile.IsMatch(p);
        bool named = GitGrep(constant, paths).Where(p => c == null || CallSite(p))
            .Any(p => RefersTo(File.ReadAllText(Path.Combine(Root, p)), constant));
        if (!named || c == null) return named;
        return !GitGrep(key, callSites).Any(p => CallsQuest(File.ReadAllText(Path.Combine(Root, p)), key));
    }

    // the tracked files under the paths (working tree) that hold the text
    private static List<string> GitGrep(string text, IEnumerable<string> paths)
    {
        var (code, output, error) = RunBytes("git", Root, new[] { "grep", "-l", "-F", "-e", text, "--" }.Concat(paths).ToArray());
        if (code > 1) throw new InvalidOperationException($"git grep {text} exited {code}: {error}");
        return Encoding.UTF8.GetString(output).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToList();
    }

    // the commit's full hash (HEAD or a short hash resolved)
    private static string FullCommit(string commit)
    {
        var (code, output, _) = RunBytes("git", Root, "rev-parse", "--verify", "--quiet", commit + "^{commit}");
        if (code != 0) throw new InvalidOperationException($"no commit {commit}");
        return Encoding.ASCII.GetString(output).Trim();
    }

    // the scripts overlay: <tmp>/scripts, the working tree's scripts/ hard-linked (or copied) once per run, with one
    // record's quests' files from its commit written over it (a written file replaces its link, never writes through it):
    // the flow's quest and the quests its modules patch (RecordKeys); every scripts/ file deleted or modified since the
    // commit whose text there names one's xi.quest.id constant, or is its LegacyPath file, or (an NPC-call quest's script
    // that only read a char var) whose HEAD text calls it (OverlayTakes), or whose HEAD text calls a quest the overlay
    // turns off (OverlayPlan). A record is refused when a script the overlay leaves at HEAD calls such a quest.
    // Quest rework C2: a mission's or a battlefield's the same way (its files under scripts/missions or
    // scripts/battlefields, its xi.mission.id or xi.battlefield.id constant, its xi.cs.mission calls), and with a
    // battlefield off the framework scripts/globals/battlefield.lua from the commit (OverlayPlan). A taken file that is
    // another C# mission's or battlefield's LegacyPath file turns that container off too (OverlayPlanFor).
    // Under ../_golden-tmp-<random> beside the repo, deleted at the end of the run
    private static string overlayRoot_;
    private static string overlayFor_;
    private static readonly List<string> overlaid_ = new();
    private static readonly List<string> overlayDirs_ = new();
    // the other quests the overlaid files call at HEAD and did not call at the commit: their Lua branches come back
    // with the files, so they are off too (a quest a file already called at the commit stays C#; OverlayPlan)
    private static readonly SortedSet<string> overlayOff_ = new(StringComparer.Ordinal);
    private static string head_;

    // CSHARP_QUESTS_OFF of a record of the flow: its quest, the quests its modules patch, and the other quests whose Lua the
    // overlay brought back
    private static string[] RecordOff(Flow f) =>
        IsProbe(f.Key) ? Array.Empty<string>() : RecordOffKeys(f.Key, ModuleQuestKeys(f.Modules, CSharpModules()), overlayOff_);

    internal static string[] RecordOffKeys(string key, IEnumerable<string> moduleKeys, IEnumerable<string> overlayOff)
    {
        var off = new List<string> { key };
        foreach (var k in moduleKeys.Concat(overlayOff))
            if (!off.Contains(k)) off.Add(k);
        return off.ToArray();
    }

    // the quests a record of the flow runs on Lua: its own, then those its modules patch
    private static List<string> RecordKeys(Flow f) =>
        IsProbe(f.Key) ? new List<string>() : RecordOffKeys(f.Key, ModuleQuestKeys(f.Modules, CSharpModules()), Array.Empty<string>()).ToList();

    // the quest keys the C# ports of the module files patch (QuestModule.PatchedKeys: under every xi.pre branch), in the
    // files' order: a record turns each off and runs its Lua (a module's modifyInteractionEntry of a quest whose Lua file
    // is gone would find no container), as the flow's own
    internal static List<string> ModuleQuestKeys(IEnumerable<string> moduleFiles, IEnumerable<QuestModule> ports)
    {
        var keys = new List<string>();
        var all = ports.ToList();
        foreach (var file in moduleFiles)
            foreach (var m in all.Where(m => m.LuaFile == file))
                foreach (var key in m.PatchedKeys())
                    if (!keys.Contains(key)) keys.Add(key);
        return keys;
    }

    // whether the overlay takes the file from the commit for one of the quests (key, LegacyPath file, Lua id constant:
    // xi.quest.id, or a mission's xi.mission.id, a battlefield's xi.battlefield.id; LuaIdConstant):
    // it is the quest's LegacyPath file, its text at the commit names the constant, or (an NPC-call quest's script, which
    // may only have read a char var) its HEAD text calls the quest
    internal static bool OverlayTakes(string rel, string oldText, ISet<string> calledNow, IEnumerable<(string Key, string Legacy, string Constant)> quests) =>
        quests.Any(q => rel == q.Legacy || RefersTo(oldText, q.Constant) || calledNow.Contains(q.Key));

    // the overlay's plan over the scripts deleted or modified since the commit (in the diff's order): the files it
    // takes (OverlayTakes), and the other quests that go off with them: those a taken file calls at HEAD and did not
    // call at the commit (their Lua branches come back with the file; a quest the file already called stays C#). A
    // changed file whose HEAD text calls a quest that is off (the record's own or one gone off) is taken too, until
    // nothing changes: left at HEAD, its call would find no quest and fall through instead of running the commit's Lua.
    // So is a changed file that a taken file requires at the commit (require('scripts/quests/jeuno/helpers') from a
    // Borghertz's Hands file): left at HEAD, the commit's Lua would load the helper as it is now (its templates gone).
    // Quest rework C2: the containers are quests, missions and battlefields alike (their files are in the diff of
    // scripts/), and when a battlefield key is off for the record (its own, or one gone off) the framework
    // scripts/globals/battlefield.lua is taken too, and the changed files it requires at the commit with it
    // (scripts/globals/pathfind). It is among the changed files when it changed since the commit; else Overlay puts it
    // there for a record of a battlefield key (WithBattlefieldFramework). A ported battlefield's trace is so recorded
    // on the Lua framework
    internal const string BattlefieldFramework = "scripts/globals/battlefield.lua";

    // the changed scripts, with the framework (BattlefieldFramework) among them when one of the keys is a battlefield's
    // and it did not change since the commit: its text at the commit (frameworkAtCommit) both then and now, at its
    // place in the diff's path order, so OverlayPlan takes it and follows its requires
    internal static List<(string Rel, string OldText, string NowText)> WithBattlefieldFramework(
        IReadOnlyList<(string Rel, string OldText, string NowText)> changed, IEnumerable<string> keys,
        Func<string> frameworkAtCommit)
    {
        var list = changed.ToList();
        if (!keys.Any(ContainerKeys.IsBattlefield) || list.Any(c => c.Rel == BattlefieldFramework)) return list;
        string text = frameworkAtCommit();
        int at = list.FindIndex(c => string.CompareOrdinal(c.Rel, BattlefieldFramework) > 0);
        list.Insert(at < 0 ? list.Count : at, (BattlefieldFramework, text, text));
        return list;
    }
    // Quest rework C2 Task 10: a taken file that is the LegacyPath file of another C# container
    // (owners: the file to its container's key) turns that container off too. The mission and battlefield files
    // name each other (save_the_children.lua names its mission, 1_3_Save_the_Children.lua its battlefield), so a
    // record of one brings the other's Lua back, and that Lua must run, not the C# container registered over it
    internal static (List<string> Taken, SortedSet<string> Off) OverlayPlan(
        IReadOnlyList<(string Rel, string OldText, string NowText)> changed,
        IReadOnlyList<(string Key, string Legacy, string Constant)> quests,
        IReadOnlyDictionary<string, string> owners = null)
    {
        var keys = new HashSet<string>(quests.Select(q => q.Key), StringComparer.Ordinal);
        var now = changed.Select(c => CalledKeys(c.NowText)).ToList();
        var taken = new bool[changed.Count];
        var off = new SortedSet<string>(StringComparer.Ordinal);
        var required = new HashSet<string>(StringComparer.Ordinal);
        bool BattlefieldOff() => keys.Concat(off).Any(ContainerKeys.IsBattlefield);
        for (bool grew = true; grew;)
        {
            grew = false;
            for (int i = 0; i < changed.Count; i++)
            {
                if (taken[i]) continue;
                var (rel, oldText, _) = changed[i];
                if (!OverlayTakes(rel, oldText, now[i], quests) && !now[i].Overlaps(off) &&
                    !(rel.EndsWith(".lua", StringComparison.Ordinal) && required.Contains(rel[..^4])) &&
                    !(rel == BattlefieldFramework && BattlefieldOff())) continue;
                taken[i] = grew = true;
                var then = CalledKeys(oldText);
                off.UnionWith(now[i].Where(k => !then.Contains(k) && !keys.Contains(k)));
                if (owners != null && owners.TryGetValue(rel, out var owner) && !keys.Contains(owner)) off.Add(owner);
                required.UnionWith(RequiredNames(oldText));
            }
        }
        return (changed.Where((c, i) => taken[i]).Select(c => c.Rel).ToList(), off);
    }

    // the overlay's plan with the framework: the changed scripts get the framework when a record key is a
    // battlefield's (WithBattlefieldFramework), and OverlayPlan runs over them. When a battlefield goes off only as
    // the owner of a taken file (a mission record that takes its battlefield's file), the plan runs again with the
    // framework added for that off key, so the battlefield's Lua runs on the commit's framework
    internal static (List<(string Rel, string OldText, string NowText)> Changed, List<string> Taken,
                     SortedSet<string> Off)
        OverlayPlanFor(IReadOnlyList<(string Rel, string OldText, string NowText)> changed,
                       IReadOnlyList<(string Key, string Legacy, string Constant)> quests,
                       IReadOnlyDictionary<string, string> owners, Func<string> frameworkAtCommit)
    {
        var keys = quests.Select(q => q.Key).ToList();
        var list = WithBattlefieldFramework(changed, keys, frameworkAtCommit);
        var (taken, off) = OverlayPlan(list, quests, owners);
        var withOff = WithBattlefieldFramework(list, keys.Concat(off), frameworkAtCommit);
        if (withOff.Count != list.Count)
        {
            list = withOff;
            (taken, off) = OverlayPlan(list, quests, owners);
        }
        return (list, taken, off);
    }

    // the LegacyPath file (scripts/....lua) of each C# container, to its key: the owners OverlayPlan turns off when it
    // takes their files
    internal static Dictionary<string, string> OverlayOwners(IEnumerable<ContainerScript> containers) =>
        containers.Where(c => c.LegacyPath != null)
            .ToDictionary(c => c.LegacyPath + ".lua", c => c.Key, StringComparer.Ordinal);

    // require('<name>') / require("<name>") outside a -- comment: the require names a Lua text loads
    private static readonly Regex RequireName = new(@"\brequire\s*\(?\s*(['""])([^'""]+)\1");
    internal static IEnumerable<string> RequiredNames(string text)
    {
        foreach (Match m in RequireName.Matches(text))
        {
            int lineStart = text.LastIndexOf('\n', m.Index) + 1;
            if (!text.Substring(lineStart, m.Index - lineStart).Contains("--", StringComparison.Ordinal))
                yield return m.Groups[2].Value;
        }
    }

    // a quest half-ported at the commit (the Windurst Task 14 final review's latent gap): the plan turns it off (a
    // taken script calls it at HEAD and did not at the commit), but another taken script already called it at the
    // commit, so that script's call, from the commit, would find no C# quest and fall through silently: the record
    // would not be the commit's Lua. So is one of the record's own quests (keys: the flow's and those its modules
    // patch, which the record turns off) that a taken script already called at the commit (carry-harness-3: off holds
    // only the other quests). Refused, naming the script and the quest; null when no taken script is such
    internal static string OverlayHalfPorted(IReadOnlyList<(string Rel, string OldText, string NowText)> changed,
                                             ICollection<string> taken, ISet<string> off, IEnumerable<string> keys)
    {
        var own = new HashSet<string>(keys, StringComparer.Ordinal);
        foreach (var (rel, oldText, _) in changed)
        {
            if (!taken.Contains(rel)) continue;
            var called = CalledKeys(oldText);
            if (called.FirstOrDefault(own.Contains) is { } mine)
                return $"{mine} is half-ported at the commit: {rel} calls it there, and the record turns it off, " +
                       "so that call would find no quest and fall through";
            if (called.FirstOrDefault(off.Contains) is { } key)
                return $"{key} is half-ported at the commit: {rel} calls it there, another script the overlay takes " +
                       "brings its Lua back, so that call would find no quest and fall through";
        }
        return null;
    }

    // the first script the overlay leaves at HEAD that calls a quest that is off ("<rel> calls <key>"), else null
    internal static string OverlayStray(ICollection<string> taken, ISet<string> off,
                                        IEnumerable<(string Rel, string NowText)> scripts)
    {
        foreach (var (rel, text) in scripts)
        {
            if (taken.Contains(rel)) continue;
            if (CalledKeys(text).FirstOrDefault(off.Contains) is { } key) return $"{rel} calls {key}";
        }
        return null;
    }

    // the scripts folder for the quests' Lua at the commit (full hash): null (the real one) at HEAD. intoGolden: the
    // record writes tests/golden (the checked-in traces), where HEAD with scripts/ not committed is refused
    private static string Overlay(IReadOnlyList<string> keys, string commit, bool intoGolden, out string refusal)
    {
        refusal = null;
        head_ ??= FullCommit("HEAD");
        if (commit == head_)
        {
            RevertOverlay();
            // the working tree's scripts/ is HEAD's only when nothing under it is uncommitted: else a checked-in trace
            // would stamp HEAD over Lua that HEAD does not have. Into another folder (--out: stable's children, the
            // draw-search tools' instrumented Lua) the stamp reaches no checked-in trace, so it goes on, said
            string status = RunTool("git", Root, "status", "--porcelain=v1", "--untracked-files=all", "--", "scripts/");
            if (DirtyScriptsRefusal(status, intoGolden) is { } dirty)
            {
                refusal = dirty;
                return null;
            }
            if (!intoGolden && DirtyScriptsRefusal(status, true) is { } allowed)
                Console.WriteLine($"  {allowed.Substring(0, allowed.IndexOf("): ", StringComparison.Ordinal) + 1)}: " +
                                  "allowed, the record writes outside tests/golden");
            Console.WriteLine($"  lua-at {commit} (HEAD): the working tree's scripts, no overlay");
            return null;
        }
        string forKeys = string.Join(",", keys) + "@" + commit;
        if (overlayFor_ == forKeys) return Path.Combine(overlayRoot_, "scripts");
        RevertOverlay();
        if (overlayRoot_ == null)
        {
            SweepTempDirs();
            string tmp = Path.Combine(Path.GetDirectoryName(Root), TempDirName("_golden-tmp-"));
            Directory.CreateDirectory(tmp);
            overlayRoot_ = tmp;
            int files = LinkTree(Path.Combine(Root, "scripts"), Path.Combine(tmp, "scripts"), out int links);
            Console.WriteLine($"  scripts/ in {tmp}: {files} files, {links} of them hard links");
        }
        var quests = keys.Select(k => (Key: k, Legacy: ContainerKeys.IsRoe(k) ? RoeLuaFile
                                       : CSharpContainerOf(k)?.LegacyPath is { } lp ? lp + ".lua" : null,
                                       Constant: LuaIdConstant(k))).ToList();
        var (code, diff, error) = RunBytes("git", Root, "diff", "--name-status", "--no-renames", commit, "HEAD", "--", "scripts/");
        if (code != 0) throw new InvalidOperationException($"git diff {commit} HEAD exited {code}: {error}");
        var changed = new List<(string Rel, string OldText, string NowText)>();
        var oldBytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var line in Encoding.UTF8.GetString(diff).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = line.TrimEnd('\r').Split('\t');
            if (p.Length != 2 || p[0] is not ("D" or "M")) continue;
            string rel = p[1];
            var (shown, old, why) = RunBytes("git", Root, "show", $"{commit}:{rel}");
            if (shown != 0) throw new InvalidOperationException($"git show {commit}:{rel} exited {shown}: {why}");
            changed.Add((rel, Encoding.Latin1.GetString(old),
                         p[0] == "M" ? File.ReadAllText(Path.Combine(Root, rel)) : ""));
            oldBytes[rel] = old;
        }
        // a battlefield record's framework, unchanged since the commit: its text there, among the changed files; the
        // C# missions and battlefields whose LegacyPath files the overlay takes go off with them
        var (planned, taken, off) = OverlayPlanFor(changed, quests, OverlayOwners(CSharpContainers()), () =>
        {
            var (shown, old, why) = RunBytes("git", Root, "show", $"{commit}:{BattlefieldFramework}");
            if (shown != 0)
                throw new InvalidOperationException($"git show {commit}:{BattlefieldFramework} exited {shown}: {why}");
            oldBytes[BattlefieldFramework] = old;
            return Encoding.Latin1.GetString(old);
        });
        changed = planned;
        if (OverlayHalfPorted(changed, taken, off, keys) is { } half)
        {
            refusal = $"lua-at {commit.Substring(0, 8)}: {half}";
            return null;
        }
        overlayOff_.UnionWith(off);
        // a script the overlay leaves as it is at HEAD (the same at the commit, or not there) that calls a quest this
        // record turns off would find no quest (an unknown key falls through): the record would not be the commit's Lua
        var offOrKeys = new SortedSet<string>(off.Concat(keys), StringComparer.Ordinal);
        var callers = offOrKeys.SelectMany(k => GitGrep(k, new[] { "scripts/" })).Distinct(StringComparer.Ordinal)
            .Where(rel => !taken.Contains(rel)).Select(rel => (rel, File.ReadAllText(Path.Combine(Root, rel))));
        if (OverlayStray(taken, offOrKeys, callers) is { } stray)
        {
            refusal = $"lua-at {commit.Substring(0, 8)}: {stray}, which the record turns off, and the overlay " +
                      "leaves that script as it is at HEAD (unchanged since the commit, or added after it): " +
                      "its call would find no quest";
            overlayOff_.Clear();
            return null;
        }
        foreach (var rel in taken)
        {
            byte[] old = oldBytes[rel];
            string dst = Path.Combine(overlayRoot_, rel);
            for (var d = Path.GetDirectoryName(dst); !Directory.Exists(d); d = Path.GetDirectoryName(d)) overlayDirs_.Add(d);
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            if (File.Exists(dst)) File.Delete(dst);
            File.WriteAllBytes(dst, old);
            overlaid_.Add(rel);
        }
        overlayFor_ = forKeys;
        Console.WriteLine($"  lua-at {commit}: {overlaid_.Count} file(s) from it over the scripts in {Path.Combine(overlayRoot_, "scripts")}");
        if (keys.Count > 1) Console.WriteLine($"    for {keys[0]} and the quests its modules patch: {string.Join(", ", keys.Skip(1))}");
        foreach (var rel in overlaid_) Console.WriteLine($"    overlay {rel}");
        if (overlayOff_.Any(k => !keys.Contains(k))) Console.WriteLine($"    also off (their Lua comes back with these files): {string.Join(", ", overlayOff_.Where(k => !keys.Contains(k)))}");
        return Path.Combine(overlayRoot_, "scripts");
    }

    // the overlay's files back to the working tree's, the folders it made gone
    private static void RevertOverlay()
    {
        foreach (var rel in overlaid_)
        {
            string dst = Path.Combine(overlayRoot_, rel), src = Path.Combine(Root, rel);
            File.Delete(dst);
            if (File.Exists(src)) File.Copy(src, dst);
        }
        foreach (var d in overlayDirs_.OrderByDescending(d => d.Length))
            if (Directory.Exists(d) && !Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d);
        overlaid_.Clear();
        overlayDirs_.Clear();
        overlayFor_ = null;
        overlayOff_.Clear();
    }

    // the temp tree deleted (the working directory moved off it first)
    private static void DeleteOverlay()
    {
        if (overlayRoot_ == null) return;
        Directory.SetCurrentDirectory(Root);
        try { Directory.Delete(overlayRoot_, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Console.WriteLine($"could not delete {overlayRoot_}: {ex.Message}"); }
        overlayRoot_ = null;
        overlaid_.Clear();
        overlayDirs_.Clear();
        overlayFor_ = null;
        overlayOff_.Clear();
    }

    // a record at HEAD with scripts/ not committed (git status --porcelain lines) into tests/golden: refused, naming
    // the first five; into another folder (intoGolden false) it goes on (null). Only scripts/: an uncommitted module
    // file under modules/ is still stamped HEAD
    internal static string DirtyScriptsRefusal(string porcelain, bool intoGolden)
    {
        if (!intoGolden) return null;
        var lines = porcelain.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r'))
            .Where(l => l.Length > 3).ToList();
        if (lines.Count == 0) return null;
        return $"lua-at HEAD, but scripts/ has {lines.Count} uncommitted change{(lines.Count == 1 ? "" : "s")} " +
               $"({string.Join(", ", lines.Take(5).Select(l => l.Trim()))}{(lines.Count > 5 ? ", ..." : "")}): " +
               "the trace would stamp HEAD over Lua that HEAD does not have; commit (or stash) them first";
    }

    // whether the record folder is tests/golden (the checked-in traces), whatever its spelling
    internal static bool IsGoldenDir(string dir) =>
        string.Equals(Path.GetFullPath(dir).TrimEnd('\\', '/'), Path.GetFullPath(GoldenDir).TrimEnd('\\', '/'),
                      StringComparison.OrdinalIgnoreCase);

    // a temp folder's name beside the repo (the scripts overlay's _golden-tmp-, startup --against's _startup-): the
    // prefix, this process's id and 8 random hex digits, so a later run can tell a killed run's folder from a live one
    internal static string TempDirName(string prefix) =>
        $"{prefix}{Environment.ProcessId}-{Guid.NewGuid().ToString("N").Substring(0, 8)}";

    // the process id a temp folder's name gives (TempDirName), or null for any other name (an older run's folder,
    // without one, is never swept: it may be live)
    internal static int? TempDirOwner(string name) =>
        Regex.Match(name, @"^_(?:golden-tmp|startup)-(\d{1,9})-[0-9a-f]{8}$") is { Success: true } m &&
        int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int pid) ? pid : null;

    // the temp folders beside the repo a killed run left (its process gone), deleted, and the worktrees of the gone
    // _startup- ones pruned; a folder whose process still runs (another lane's record, maybe) is left alone. Every
    // worktree's runs share the folder beside the repo, so only the process id can tell
    private static void SweepTempDirs()
    {
        bool startup = false;
        foreach (var dir in Directory.GetDirectories(Path.GetDirectoryName(Root)))
        {
            if (TempDirOwner(Path.GetFileName(dir)) is not int pid || ProcessRuns(pid)) continue;
            try
            {
                Directory.Delete(dir, true);
                Console.WriteLine($"swept {dir}: its run (process {pid}) is gone");
                startup |= Path.GetFileName(dir).StartsWith("_startup-", StringComparison.Ordinal);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"could not sweep {dir}: {ex.Message}");
            }
        }
        if (startup) RunTool("git", Root, "worktree", "prune");
    }
    private static bool ProcessRuns(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        // a process the run may not open (another user's, a reused id): taken as running, so its folder stays
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return true;
        }
    }

    // the folder's files into the new folder, hard-linked where the system can (Windows, the same volume), else copied;
    // how many files, and how many of them links
    private static int LinkTree(string from, string to, out int links)
    {
        int files = 0;
        links = 0;
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from))
        {
            string dst = Path.Combine(to, Path.GetFileName(f));
            if (OperatingSystem.IsWindows() && CreateHardLink(dst, f, IntPtr.Zero)) links++;
            else File.Copy(f, dst);
            files++;
        }
        foreach (var d in Directory.GetDirectories(from))
        {
            files += LinkTree(d, Path.Combine(to, Path.GetFileName(d)), out int more);
            links += more;
        }
        return files;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLink(string newFile, string existingFile, IntPtr securityAttributes);

    // ---- char-id locks -------------------------------------------------------------------------------------------------

    // GET_LOCK('golden-<db>-<id>', 0) for each char id the run uses, on a connection of its own kept for the run (a pooled
    // one would drop the locks when it goes back to the pool); false, printed, when another run holds one. A lock is
    // server-wide in MariaDB, so its name carries the lane's database: two lanes use the same ids in their own databases
    private static MySqlConnector.MySqlConnection lockDb_;
    private static readonly List<string> locked_ = new();

    private static bool LockChars(NetworkSettings settings, IEnumerable<uint> ids)
    {
        lockDb_ ??= new MySqlConnector.MySqlConnection(settings.BuildConnectionString() + "Pooling=false;");
        if (lockDb_.State != System.Data.ConnectionState.Open) lockDb_.Open();
        foreach (var id in ids.Distinct().OrderBy(i => i))
            foreach (var name in LockNames(dbName_, id))
            {
                using var cmd = new MySqlConnector.MySqlCommand($"SELECT GET_LOCK('{name}', 0)", lockDb_);
                var got = cmd.ExecuteScalar();
                if (got == null || got is DBNull || Convert.ToInt64(got) != 1)
                {
                    _fail++;
                    Console.WriteLine($"FAIL char {id} is in use by another QuestGolden run on {dbName_} ({name})");
                    return false;
                }
                locked_.Add(name);
            }
        return true;
    }

    // RELEASE_LOCK for each, then the connection closed (which would release them too)
    private static void UnlockChars()
    {
        if (lockDb_ == null) return;
        try
        {
            foreach (var name in locked_)
            {
                using var cmd = new MySqlConnector.MySqlCommand($"SELECT RELEASE_LOCK('{name}')", lockDb_);
                cmd.ExecuteScalar();
            }
        }
        finally
        {
            locked_.Clear();
            lockDb_.Dispose();
            lockDb_ = null;
        }
    }

    // the lock of a char id on a lane's database
    internal static string LockName(string db, uint id) => $"golden-{db}-{id}";

    // the locks a run takes for a char id: its lane's, and on xidb also golden-<id>, the name the builds from before the
    // lanes take (a worktree not yet merged with the lanes runs on xidb with it), so the two never share a char
    internal static string[] LockNames(string db, uint id) =>
        db == DefaultDb ? new[] { LockName(db, id), $"golden-{id}" } : new[] { LockName(db, id) };

    // --db <name> / --port <n> (each null when not given): the lane's database (xidb by default) and the port (3307), or
    // the error. A name is letters, digits and _ (it goes into SQL and the lock names), at most 48, not digits only:
    // --db took the port before the lanes, and `--db 3307` is refused rather than read as a database
    internal static string LaneError(string db, string port, out string name, out int portNumber)
    {
        name = db ?? DefaultDb;
        portNumber = 3307;
        if (port != null && (!int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out portNumber) || portNumber is < 1 or > 65535))
            return $"--port takes a port number, not {port}";
        if (!Regex.IsMatch(name, "^[A-Za-z0-9_]{1,48}$")) return $"--db takes a database name (letters, digits, _; at most 48), not {name}";
        if (Regex.IsMatch(name, "^[0-9]+$")) return $"--db takes a database name, not a number ({name}); the port is --port";
        return null;
    }

    // this run's lane for a child process: --db and --port
    private static string[] LaneArgs() => new[] { "--db", dbName_, "--port", dbPort_.ToString(CultureInfo.InvariantCulture) };

    // the C# containers of XI.QuestContent, every kind, in type-name order
    private static List<ContainerScript> CSharpContainers() =>
        typeof(XI.QuestContent.Zone).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ContainerScript).IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .Select(t => (ContainerScript)Activator.CreateInstance(t)).ToList();

    // the QuestScripts of XI.QuestContent, in type-name order
    private static List<QuestScript> CSharpQuests() =>
        CSharpContainers().OfType<QuestScript>().ToList();

    // ---- replay ------------------------------------------------------------------------------------------------------

    private static void Replay(Flow f, int index, PacketStore store)
    {
        string path = TracePath(GoldenDir, f);
        if (!File.Exists(path))
        {
            Fail(f.Name, "no trace; record it");
            return;
        }
        Trace want;
        try
        {
            want = Trace.Read(path, store);
        }
        catch (FormatException e)
        {
            Fail(f.Name, e.Message);
            return;
        }
        if (want.FlowSha != f.Sha256)
        {
            Fail(f.Name, $"{f.Name}: the flow changed since it was recorded; record it again");
            return;
        }
        if (want.Seed != Seed)
        {
            Fail(f.Name, $"{f.Name}: recorded with seed {want.Seed}, the harness seeds {Seed}; record it again");
            return;
        }
        var problems = new List<string>();
        replaying_ = f.Name;
        Trace actual;
        try
        {
            actual = RunFlow(f, CharsFor(index, f.Chars.Count), false, want.Masks, problems);
        }
        finally
        {
            replaying_ = null;
        }
        // the flow ran: its quest's handlers as this world declares them, for the coverage gate (a probe has no quest); a
        // mission's by its key alike. A roe flow counts once C# runs RoE; on Lua it compares Lua with Lua, as a probe does
        if (!IsProbe(f.Key) && !(ContainerKeys.IsRoe(f.Key) && zone.RoeOnLua))
        {
            string covered = CoverageKey(f.Key);
            coverage_.Ran.Add(f.Name);
            if (zone.HandlerLabels(covered) is { } labels) coverage_.Declared[covered] = labels.ToList();
            else coverage_.NoHandlers.Add(covered);
        }
        // each module the flow loads: its overrides' labels as this world applied them (null: not enabled here)
        foreach (var m in f.Modules)
        {
            string key = ModuleKey(m);
            if (zone.ModuleLabels(m) is { } overrides) coverage_.Declared[key] = overrides.ToList();
            else coverage_.NoHandlers.Add(key);
        }
        // a world table that differs from the recording's: every world starts from the snapshot (World.cs), so the
        // snapshot changed since the recording
        foreach (var (table, hash) in want.World)
            if (actual.World.FirstOrDefault(w => w.Table == table).Hash is var now && now != hash)
                problems.Add($"the world differs from the recording ({table}: recorded {hash}, now {now ?? "(none)"}): {WorldFile} or the world " +
                             "tables' schema changed (the private copies are made LIKE the real tables); record it again");
        problems.AddRange(Trace.Compare(want, actual));
        if (problems.Count == 0) Pass($"{f.Name}: {actual.Steps.Count} steps as recorded ({want.Recorder})");
        else Fail(f.Name, "\n    " + string.Join("\n    ", problems));
    }

    // ---- handler coverage --------------------------------------------------------------------------------------------

    // this run's coverage: the handler runs the hook reported, the flows replayed, their quests' declared handlers
    private static readonly Coverage coverage_ = new();
    // the flow being replayed (null between flows: a run then is no trace's)
    private static string replaying_;
    // ZoneHost.HandlerCalled of each new world: set by replay, null otherwise
    private static Action<string, string> handlerCalled_;

    // the coverage gate over the quests whose flows all ran (in this process, or in the merged --jobs result): every
    // declared handler reached by a trace or waived, no waiver of a reached one; a waiver in a flow that ran names a
    // declared handler (checked whenever its quest's handlers are known); a replayed quest whose handlers are not known
    // (no C# quest compiled for its key) fails
    private static void CoverageGate(Coverage cov, List<Flow> all, IEnumerable<string> questKeys, IEnumerable<string> moduleFiles = null)
    {
        foreach (var line in CoverageResults(cov, all, questKeys, moduleFiles))
            if (line.StartsWith("PASS ", StringComparison.Ordinal)) Pass(line.Substring(5));
            else Fail(line.Substring(5));
    }

    // the gate's lines, "PASS <name>" / "FAIL <line>", by quest key
    // questKeys (null: not checked): the keys of the C# quests; one with no flow fails, so no quest goes unchecked.
    // moduleFiles (null: not checked): the Lua files the C# modules port; one no flow loads fails (quest rework P4)
    internal static List<string> CoverageResults(Coverage cov, List<Flow> all, IEnumerable<string> questKeys = null, IEnumerable<string> moduleFiles = null)
    {
        var lines = new List<string>();
        if (questKeys != null)
        {
            var flowKeys = all.Select(f => CoverageKey(f.Key)).ToHashSet(StringComparer.Ordinal);
            lines.AddRange(questKeys.Distinct().Where(k => !flowKeys.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).Select(k => $"FAIL coverage {k}: no flows"));
        }
        foreach (var group in all.GroupBy(f => CoverageKey(f.Key)).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            string key = group.Key;
            var ran = group.Where(f => cov.Ran.Contains(f.Name)).ToList();
            if (ran.Count == 0) continue;
            if (cov.NoHandlers.Contains(key) || !cov.Declared.TryGetValue(key, out var declared))
            {
                lines.Add($"FAIL coverage {key}: no C# handlers known");
                continue;
            }
            var calls = cov.Calls.TryGetValue(key, out var c) ? c : new Dictionary<string, LabelCalls>();
            if (ran.Count < group.Count())
            {
                // not every flow ran here: only the waivers' labels
                lines.AddRange(ran.SelectMany(f => f.Waivers).Where(w => !declared.Contains(w.Label)).Select(w => $"FAIL {w.File}:{w.Line}: unknown handler {w.Label}"));
                continue;
            }
            var waivers = group.SelectMany(f => f.Waivers).ToList();
            var failures = CoverageFailures(key, declared, calls, waivers);
            lines.AddRange(failures.Select(f => "FAIL " + f));
            if (failures.Count == 0)
            {
                int waived = waivers.Select(w => w.Label).Distinct().Count();
                lines.Add($"PASS coverage {key}: {declared.Count - waived} of {declared.Count} handlers reached, {waived} waived");
            }
        }
        lines.AddRange(ModuleCoverageResults(cov, all, moduleFiles));
        return lines;
    }

    // the modules' part of the gate (quest rework P4): each module some flow loads, once every flow that loads it ran: each
    // of its overrides (ZoneHost.ModuleLabels: "<quest key> <label>", a handler or an overridden check) run by some trace
    // of those flows. A module not enabled in its flows fails; with moduleFiles, a C# module no flow loads fails too. A
    // module's run of a handler of its own counts for the module only; a wrap's call of the quest's handler counts for the
    // quest
    internal static List<string> ModuleCoverageResults(Coverage cov, List<Flow> all, IEnumerable<string> moduleFiles)
    {
        var lines = new List<string>();
        var loaded = all.SelectMany(f => f.Modules).ToHashSet(StringComparer.Ordinal);
        if (moduleFiles != null)
            lines.AddRange(moduleFiles.Distinct().Where(m => !loaded.Contains(m)).OrderBy(m => m, StringComparer.Ordinal)
                .Select(m => $"FAIL coverage {ModuleKey(m)}: no flows"));
        foreach (var m in loaded.OrderBy(m => m, StringComparer.Ordinal))
        {
            string key = ModuleKey(m);
            var flows = all.Where(f => f.Modules.Contains(m)).ToList();
            if (flows.Count == 0 || flows.Any(f => !cov.Ran.Contains(f.Name))) continue;
            // platform round P11: a Lua module no C# module ports (a global's or a helper's override) has no C#
            // handler to reach; the server's loader runs it, and each of its flows fails unless it loaded and is
            // enabled (Modules.cs)
            if (!CSharpModules().Any(x => x.LuaFile == m))
            {
                lines.Add($"PASS coverage {key}: a Lua module (no C# port), run by the server's loader in {flows.Count} flows");
                continue;
            }
            if (cov.NoHandlers.Contains(key) || !cov.Declared.TryGetValue(key, out var declared))
            {
                lines.Add($"FAIL coverage {key}: not enabled in its flows (no C# module ports it, or its condition is false with their settings)");
                continue;
            }
            var calls = cov.Calls.TryGetValue(key, out var c) ? c : new Dictionary<string, LabelCalls>();
            var failures = CoverageFailures(key, declared, calls, new List<Waiver>());
            lines.AddRange(failures.Select(x => "FAIL " + x));
            if (failures.Count == 0) lines.Add($"PASS coverage {key}: {declared.Count} of {declared.Count} overrides reached");
        }
        return lines;
    }

    // a failure whose text is the whole line
    private static void Fail(string line)
    {
        _fail++;
        Console.WriteLine($"FAIL {line}");
    }

    // the gate for one quest whose flows all ran: a waiver naming no declared handler, a declared handler with no runs and
    // no waiver, a waived handler with runs (named by its first trace)
    internal static List<string> CoverageFailures(string key, IReadOnlyList<string> declared, IReadOnlyDictionary<string, LabelCalls> calls,
                                                  IReadOnlyList<Waiver> waivers)
    {
        var failures = new List<string>();
        var waived = new HashSet<string>(StringComparer.Ordinal);
        foreach (var w in waivers)
            if (!declared.Contains(w.Label)) failures.Add($"{w.File}:{w.Line}: unknown handler {w.Label}");
            else waived.Add(w.Label);
        foreach (var label in declared)
        {
            var c = calls.TryGetValue(label, out var hit) ? hit : null;
            int n = c?.Count ?? 0;
            if (n == 0 && !waived.Contains(label)) failures.Add($"coverage {key}: {label} reached by no trace");
            else if (n > 0 && waived.Contains(label)) failures.Add($"coverage {key}: waiver {label} is reached by {c.Traces.FirstOrDefault() ?? "?"}");
        }
        return failures;
    }

    // replay all --jobs n: the shards as child processes at once, each writing its counts to a temp folder; their output
    // printed in shard order, a child that failed or wrote no counts a failure, the counts merged, the coverage gate once
    private static void ReplayJobs(int n)
    {
        var all = LoadFlows((_, _) => true);
        if (all == null) return;
        string tmp = Path.Combine(Path.GetTempPath(), $"questgolden-jobs-{Environment.ProcessId}");
        if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
        Directory.CreateDirectory(tmp);
        try
        {
            string dll = typeof(Program).Assembly.Location;
            var children = new List<(Process P, StringBuilder Output, string Counts)>();
            for (int i = 1; i <= n; i++)
            {
                string counts = Path.Combine(tmp, $"{i}.txt");
                var psi = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                foreach (var a in new[] { dll, "replay", "all", "--shard", $"{i}/{n}", "--counts", counts }.Concat(LaneArgs())) psi.ArgumentList.Add(a);
                var output = new StringBuilder();
                var p = Process.Start(psi);
                p.OutputDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
                p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine("! " + e.Data); };
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                children.Add((p, output, counts));
            }
            var merged = new Coverage();
            for (int i = 0; i < n; i++)
            {
                var (p, output, counts) = children[i];
                p.WaitForExit();
                int code = p.ExitCode;
                p.Dispose();
                Console.WriteLine($"---- shard {i + 1}/{n} (exit code {code})");
                lock (output) Console.Write(output.ToString());
                if (!File.Exists(counts))
                {
                    Fail($"shard {i + 1}/{n}", $"exit code {code}, no counts written");
                    continue;
                }
                var cov = Coverage.Parse(File.ReadAllText(counts), counts);
                merged.Merge(cov);
                if (code != 0 && cov.Fail == 0) Fail($"shard {i + 1}/{n}", $"exit code {code}");
            }
            Console.WriteLine($"---- the {n} shards merged: {merged.Pass} passed, {merged.Fail} failed");
            _pass += merged.Pass;
            _fail += merged.Fail;
            OrphanTraces(all);
            CoverageGate(merged, all, CoverageGateKeys(), CSharpModuleFiles());
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch (IOException) { }
        }
    }

    // ---- the startup gate ----------------------------------------------------------------------------------------------

    // run first in every replay: in a fresh world, and again with every C# quest's LegacyPath file hidden (or already
    // deleted), no C# quest failed to load, the NPC calls are valid, and the containers list is the recorded order
    private static void Gate()
    {
        Console.WriteLine("the startup gate");
        var golden = File.ReadAllLines(Path.Combine(GoldenDir, "container-order.txt")).ToList();
        NewWorld(null);
        OrderCheck("gate: the containers list is the recorded order", golden, zone.ContainerOrder(lua));
        Check("gate: no C# quest failed to load", "", string.Join(",", zone.QuestLoadFailures));
        Check("gate: no NPC call problems", "", string.Join(" | ", zone.NpcCallProblems));
        // the server's startup scans only the called NPCs' scripts; the gate scans every script
        Check("gate: no NPC call problems in any script", "", string.Join(" | ", zone.ValidateNpcCalls(Path.Combine(Root, "scripts"))));
        // quest rework P4: every C# module, enabled or not, applies to the quests it names and they pass the validator
        Check("gate: every C# module applies to its quests", "", string.Join(" | ", zone.CheckQuestModules()));

        // quest rework C4: the Lua is gone; replay would compare Lua with Lua otherwise, and a require left behind fails here
        Check("gate: RoE runs on C#", false, zone.RoeOnLua);
        Check("gate: no global script failed to load", 0, scripts.LoadErrors);
        // both RoE Lua files stay deleted: a roe.lua left behind passes its replays with no coverage verdict
        foreach (var gone in new[] { "roe.lua", "roe_records.lua" })
            Check($"gate: scripts/globals/{gone} stays deleted", false, File.Exists(Path.Combine(Root, "scripts", "globals", gone)));

        // every C# container's LegacyPath file, every kind (quest rework C2: a mission's, a battlefield's)
        var hidden = new List<string>();
        foreach (var q in CSharpContainers().Where(q => q.LegacyPath != null))
        {
            bool there = File.Exists(Path.Combine(Root, q.LegacyPath + ".lua"));
            if (there) hidden.Add(q.LegacyPath);
            Console.WriteLine($"  {q.GetType().Name}: {q.LegacyPath} {(there ? "hidden" : "deleted")}");
        }
        NewWorld(hidden);
        OrderCheck("gate: with every LegacyPath file hidden or deleted, the recorded order", golden, zone.ContainerOrder(lua));
        Check("gate:   ... no C# quest failed to load", "", string.Join(",", zone.QuestLoadFailures));
        Check("gate:   ... no NPC call problems", "", string.Join(" | ", zone.NpcCallProblems));
    }

    // the containers list against the recorded one; a mismatch names the first differing line
    private static void OrderCheck(string name, List<string> want, List<string> actual)
    {
        int i = 0;
        while (i < want.Count && i < actual.Count && want[i] == actual[i]) i++;
        if (i == want.Count && i == actual.Count) Pass(name);
        else Fail(name, $"line {i + 1} differs: recorded [{(i < want.Count ? want[i] : "(end)")}], now [{(i < actual.Count ? actual[i] : "(end)")}] " +
                        $"({want.Count} / {actual.Count} lines)");
    }

    // ---- stable ------------------------------------------------------------------------------------------------------

    // `record` (--csharp: `rerecord`) twice into a temp folder, each in its own child process (dotnet <this dll> record
    // <sel> --out <tmp>), and every trace compared byte for byte; a difference is named by step and field
    private static void Stable(string sel, bool csharp)
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"questgolden-stable-{Environment.ProcessId}");
        if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
        try
        {
            var dirs = new[] { Path.Combine(tmp, "a"), Path.Combine(tmp, "b") };
            foreach (var dir in dirs)
            {
                string mode = csharp ? "rerecord" : "record";
                int code = Child(StableChildArgs(mode, sel, dir, luaAt_).Concat(LaneArgs()).ToArray(), Path.GetFileName(dir));
                if (code != 0)
                {
                    Fail($"stable: {mode} run {Path.GetFileName(dir)}", $"exit code {code}");
                    return;
                }
            }
            var stores = dirs.Select(PacketStore.Load).ToArray();
            var names = Directory.GetFiles(dirs[0], "*.trace").Select(Path.GetFileName)
                .Union(Directory.GetFiles(dirs[1], "*.trace").Select(Path.GetFileName)).OrderBy(n => n, StringComparer.Ordinal).ToList();
            if (names.Count == 0) Fail("stable", "no trace recorded");
            // the stores: the same packets under the same hashes
            byte[] sa = File.ReadAllBytes(Path.Combine(dirs[0], PacketStore.FileName)), sb = File.ReadAllBytes(Path.Combine(dirs[1], PacketStore.FileName));
            if (sa.AsSpan().SequenceEqual(sb)) Pass($"stable: {PacketStore.FileName} byte-identical in two processes");
            else Fail($"stable: {PacketStore.FileName}", "the two runs' stores differ");
            foreach (var name in names)
            {
                string a = Path.Combine(dirs[0], name), b = Path.Combine(dirs[1], name);
                if (!File.Exists(a) || !File.Exists(b))
                {
                    Fail($"stable: {name}", "recorded in one run only");
                    continue;
                }
                if (File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b)))
                {
                    string golden = Path.Combine(GoldenDir, name);
                    bool asCheckedIn = File.Exists(golden) && File.ReadAllBytes(golden).AsSpan().SequenceEqual(File.ReadAllBytes(a));
                    Pass($"stable: {name} byte-identical in two processes{(asCheckedIn ? " and as checked in" : " (differs from the checked-in trace)")}");
                    continue;
                }
                var ta = Trace.Read(a, stores[0]);
                var tb = Trace.Read(b, stores[1]);
                var diffs = Trace.Compare(ta, tb);
                if (ta.FlowSha != tb.FlowSha || ta.Recorder != tb.Recorder || ta.Seed != tb.Seed || ta.LuaAt != tb.LuaAt || !ta.World.SequenceEqual(tb.World))
                    diffs.Insert(0, "the headers differ");
                if (diffs.Count == 0) diffs.Add("the files differ but parse the same (formatting)");
                Fail($"stable: {name}", "\n    " + string.Join("\n    ", diffs));
            }
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch (IOException) { }
        }
    }

    // the run's quests: lines that are problems: all but BenignQuestLines, a record's own CSHARP_QUESTS_OFF lines and
    // the flow's declared warnings (only a double finish parses as one: Flow.DoubleFinishKey), each once
    internal static IEnumerable<string> QuestLineProblems(IEnumerable<string> logged, IReadOnlyList<string> declared,
                                                          string[] off) =>
        logged.Where(l => l.StartsWith("quests: ", StringComparison.Ordinal) &&
                          !BenignQuestLines.Any(re => re.IsMatch(l)) &&
                          !RecordOffLine(l, off) && !declared.Contains(l)).Distinct();

    // a step with expect-error "<text>" and its own log lines, log[From..To) (carry-harness-2)
    internal sealed record StepLog(int Line, string Label, string Text, int From, int To);

    // the log lines (by index) the steps' expect-error expects, and a problem for each such step that raised none of
    // them. The Lua side (a record) expects a script error holding the text; the C# side (replay, rerecord) a throw of
    // a C# handler of the flow's own quest (key) whose message holds the text (its line in threw,
    // ZoneHost.HandlerThrew: "quests: <key> <handler>: <message>"), or the same script error (a call-only port whose
    // patched script keeps the Lua's raise, A New Dawn's Sturm.lua). Any other error line of the step, another quest's
    // throw or one without the text included, stays a problem of the run
    internal static (HashSet<int> Expected, List<string> Problems) ExpectedErrors(IReadOnlyList<string> logged,
        IReadOnlyList<StepLog> steps, bool luaSide, ISet<string> threw, string key)
    {
        // a throw of the quest's own handler, its message (after "quests: <key> <handler>: ") holding the text
        bool OwnThrow(string l, string text)
        {
            string head = $"quests: {key} ";
            if (!threw.Contains(l) || !l.StartsWith(head, StringComparison.Ordinal)) return false;
            int colon = l.IndexOf(": ", head.Length, StringComparison.Ordinal);
            return colon >= 0 && l.IndexOf(text, colon + 2, StringComparison.Ordinal) >= 0;
        }
        var expected = new HashSet<int>();
        var problems = new List<string>();
        foreach (var s in steps)
        {
            bool any = false;
            for (int i = s.From; i < s.To && i < logged.Count; i++)
            {
                string l = logged[i];
                bool scriptError = IsScriptError(l) && l.Contains(s.Text, StringComparison.Ordinal);
                if (!scriptError && (luaSide || !OwnThrow(l, s.Text))) continue;
                expected.Add(i);
                any = true;
            }
            if (any) continue;
            string wanted = luaSide ? "a script error holding the text"
                : $"a throw of a {key} handler (quests: {key} <handler>: <message>) or a script error, " +
                  "holding the text";
            problems.Add($"{s.Line}: [{s.Label}] the step raised no error it expects ({wanted})");
        }
        return (expected, problems);
    }

    // the declared warnings the run must log: all but a double finish of a quest the run turned off (a record's, the
    // Lua side's: its handlers are Lua there, no C# dispatch counts them; the C# side turns none off)
    internal static List<string> RequiredWarnings(IReadOnlyList<string> declared, string[] off) =>
        declared.Where(d => !(Flow.DoubleFinishKey(d) is { } key && off.Contains(key, StringComparer.Ordinal)))
            .ToList();

    // a flow's declared warnings the run did not log (a stale warning: line), one problem each
    internal static IEnumerable<string> WarningProblems(IReadOnlyList<string> declared, IReadOnlyList<string> logged) =>
        declared.Where(d => !logged.Contains(d)).Select(d => $"warning: {d}: declared, but the run did not log it");

    // a stable child's arguments: the record (or rerecord) into the folder; a record gets stable's --lua-at (quest rework
    // P9, D7: without it the children ran the Lua LuaCommit picks and stamped that, HEAD when the quest's Lua was in
    // the tree, so "as checked in" failed for a trace recorded with --lua-at). A rerecord keeps the checked-in lua-at.
    // The children record into a temp folder, so an uncommitted scripts/ does not refuse them (DirtyScriptsRefusal)
    internal static string[] StableChildArgs(string mode, string sel, string dir, string luaAt) =>
        luaAt != null && mode == "record" ? new[] { mode, sel, "--out", dir, "--lua-at", luaAt } : new[] { mode, sel, "--out", dir };

    // this program again in a child process, its output echoed with a prefix; the exit code
    private static int Child(string[] args, string tag)
    {
        string dll = typeof(Program).Assembly.Location;
        var psi = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        psi.ArgumentList.Add(dll);
        foreach (var a in args) psi.ArgumentList.Add(a);
        Console.WriteLine($"stable: run {tag}: dotnet {dll} {string.Join(" ", args)}");
        using var p = Process.Start(psi);
        p.OutputDataReceived += (_, e) => { if (e.Data != null) Console.WriteLine($"  [{tag}] {e.Data}"); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) Console.WriteLine($"  [{tag}!] {e.Data}"); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        p.WaitForExit();
        return p.ExitCode;
    }
}

// a handler label's runs: how many, and the traces (flow names) that ran it
internal sealed class LabelCalls
{
    public int Count;
    public readonly SortedSet<string> Traces = new(StringComparer.Ordinal);
}

// a replay's handler coverage: per quest key its declared labels and their runs, the flows replayed, and the pass / fail
// totals. A --counts file holds one, a line each: pass<TAB><n>, fail<TAB><n>, ran<TAB><flow>, nohandlers<TAB><key>, and
// <key><TAB><label><TAB><count><TAB><trace>,<trace>... for every declared label (0 and no traces when none ran it)
internal sealed class Coverage
{
    public readonly Dictionary<string, List<string>> Declared = new(StringComparer.Ordinal);
    public readonly Dictionary<string, Dictionary<string, LabelCalls>> Calls = new(StringComparer.Ordinal);
    public readonly SortedSet<string> Ran = new(StringComparer.Ordinal);
    // the replayed keys whose handlers the world did not know (no C# quest compiled for the key)
    public readonly SortedSet<string> NoHandlers = new(StringComparer.Ordinal);
    public int Pass, Fail;

    // one run of the handler, by the trace (null: no trace's)
    public void Add(string key, string label, string trace)
    {
        var c = For(key, label);
        c.Count++;
        if (trace != null) c.Traces.Add(trace);
    }

    private LabelCalls For(string key, string label)
    {
        if (!Calls.TryGetValue(key, out var byLabel)) Calls[key] = byLabel = new Dictionary<string, LabelCalls>(StringComparer.Ordinal);
        if (!byLabel.TryGetValue(label, out var c)) byLabel[label] = c = new LabelCalls();
        return c;
    }

    // the counts file's text (LF): the totals, the flows run, then per key (in order) its declared labels (in declaration
    // order) and any other label that ran
    public string Write()
    {
        var sb = new StringBuilder();
        sb.Append($"pass\t{Pass}\nfail\t{Fail}\n");
        foreach (var r in Ran) sb.Append($"ran\t{r}\n");
        foreach (var k in NoHandlers) sb.Append($"nohandlers\t{k}\n");
        // a key whose C# script declares no handler (WotG 41 Her Memories) has no label line; this keeps it known
        var noLabels = Declared.Where(x => x.Value.Count == 0).Select(x => x.Key);
        foreach (var k in noLabels.OrderBy(k => k, StringComparer.Ordinal)) sb.Append($"declared\t{k}\n");
        foreach (var key in Declared.Keys.Union(Calls.Keys).OrderBy(k => k, StringComparer.Ordinal))
        {
            var labels = Declared.TryGetValue(key, out var d) ? d.ToList() : new List<string>();
            Calls.TryGetValue(key, out var byLabel);
            if (byLabel != null) labels.AddRange(byLabel.Keys.Where(l => !labels.Contains(l)).OrderBy(l => l, StringComparer.Ordinal));
            foreach (var label in labels)
            {
                var c = byLabel != null && byLabel.TryGetValue(label, out var hit) ? hit : null;
                sb.Append($"{key}\t{label}\t{c?.Count ?? 0}\t{(c == null ? "" : string.Join(",", c.Traces))}\n");
            }
        }
        return sb.ToString();
    }

    // a counts file read back; every label line's label is declared
    public static Coverage Parse(string text, string file)
    {
        var cov = new Coverage();
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.Length == 0) continue;
            var p = line.Split('\t');
            if (p.Length == 2 && p[0] is "pass" or "fail" && int.TryParse(p[1], out int total))
            {
                if (p[0] == "pass") cov.Pass += total; else cov.Fail += total;
                continue;
            }
            if (p.Length == 2 && p[0] == "ran")
            {
                cov.Ran.Add(p[1]);
                continue;
            }
            if (p.Length == 2 && p[0] == "nohandlers")
            {
                cov.NoHandlers.Add(p[1]);
                continue;
            }
            if (p.Length == 2 && p[0] == "declared")
            {
                if (!cov.Declared.ContainsKey(p[1])) cov.Declared[p[1]] = new List<string>();
                continue;
            }
            if (p.Length != 4 || !int.TryParse(p[2], out int count) || count < 0)
                throw new FormatException($"{file}:{i + 1}: a counts line is key<TAB>label<TAB>count<TAB>traces: {line}");
            if (!cov.Declared.TryGetValue(p[0], out var declared)) cov.Declared[p[0]] = declared = new List<string>();
            if (!declared.Contains(p[1])) declared.Add(p[1]);
            var c = cov.For(p[0], p[1]);
            c.Count += count;
            c.Traces.UnionWith(p[3].Split(',', StringSplitOptions.RemoveEmptyEntries));
        }
        return cov;
    }

    // another's counts added: the runs summed, the traces, flows and declared labels joined, the totals summed
    public void Merge(Coverage o)
    {
        Pass += o.Pass;
        Fail += o.Fail;
        Ran.UnionWith(o.Ran);
        NoHandlers.UnionWith(o.NoHandlers);
        foreach (var (key, labels) in o.Declared)
        {
            if (!Declared.TryGetValue(key, out var mine)) Declared[key] = mine = new List<string>();
            foreach (var l in labels) if (!mine.Contains(l)) mine.Add(l);
        }
        foreach (var (key, byLabel) in o.Calls)
            foreach (var (label, c) in byLabel)
            {
                var m = For(key, label);
                m.Count += c.Count;
                m.Traces.UnionWith(c.Traces);
            }
    }
}
