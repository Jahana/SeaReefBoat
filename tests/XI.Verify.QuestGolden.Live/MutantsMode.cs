using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using XI.Map;
using XI.Persistence;

// mutant-design (option 3, docs/superpowers/specs/2026-11-10-golden-mutant-process-design.md, sections 2 and 4): the
// mutants of a run in one long-lived process, the schema build's (MutantSchema) switches set per mutant.
//
//   mutants <plan.json> --db <lane>
//
// Refused (MutantsError, before any database or build) with all, any option but --db and --port (--jobs, --shard,
// --counts, --fail-fast, --no-gate, --out, ...), a second positional, and the default database (in any case): a lane
// only. The plan: {"union": "<selector>", "canary": "<selector>", "mutants": {"<id>": <switch>, ...},
// "variants": [<switch>, ...], "canary_every": 10, "heap_ceiling_mb": 2048}. The process, once:
//   - sets the switch (MutantSwitch in AppContext, MutantSchema.SwitchKey) to none and records every switch asked
//     while it runs every XI.QuestContent class constructor and the setup below; `STATICINIT <switch> ...` lists
//     them: a value made then holds the base's for every mutant (the runner sends those mutants to option 1);
//   - one setup, LoadSelected over the union, the item stack gate over it, one lock of the union's char ids, one
//     PacketStore, the memo caches primed; the schema's static variants (SchemaVariants) must be the plan's; then
//     `READY`.
// Then a command a line on stdin:
//   RUN <tag> <switch> <selector>   the counters reset (ResetRun), the static variants' caches cleared; for a mutant,
//                                   its static variants made under its switch (as a fresh process's class
//                                   constructor makes them: one that throws prints `VARIANT-THREW <tag> <why>` and a
//                                   strict FINGERPRINT, no block, and the runner sends the mutant to option 1); the
//                                   switch set (0: none) and the selector replayed exactly as
//                                   `replay <selector> --fail-fast --no-gate` replays it, between
//                                   `MUTANT <tag> BEGIN a=<switch>` and `MUTANT <tag> END <its summary line>`; the
//                                   switch back to none; then `FINGERPRINT <tag> ...`: the static state (every
//                                   XI.QuestContent static, the named XI.Zone / XI.Common ones, the harness's process
//                                   caches but the char loader, which caches database rows), the database (CHECKSUM
//                                   TABLE over every table with no charid column but the private world tables) and
//                                   the locked ids' rows (0 after Cleanup), the heap under the ceiling. After a
//                                   mutant's block (and a canary) both fingerprints must be as after the block before,
//                                   else `STOP <why>` and exit 2; after an unmutated block (the base, a warm-up: only
//                                   before the first mutant) they are the new base (a static made lazily on a path that
//                                   block reached first: `FINGERPRINT-BASE <tag> ...` says so). The switches a block
//                                   asks are recorded: a mutant's block in which every flow passed must ask its own
//                                   switch exactly when the unmutated blocks of its selection did (MutantLoop.Reach),
//                                   else STOP;
//   QUIT                            the canary if a mutant ran since the last one, then `BYE`, exit 0.
// The first block must be unmutated, and no unmutated block may follow a mutant's (MutantLoop.Refuse). The canary
// (`CANARY BEGIN after <tags>` ... `CANARY END <summary> pass|FAIL`): the plan's canary selector unmutated, before the
// first block of every canary_every-th mutant after the last canary (so a mutant's blocks, its scope alone too, sit
// between two canaries), after a block whose run threw outside its flows, and at QUIT; a failure is
// `STOP the canary failed: rerun <the tags since the last good canary> with option 1`, exit 2.
internal sealed class MutantSwitch
{
    // the switch number set now ("0": none); the schema's switches ask Equals("<n>")
    public volatile string Now = "0";
    // the startup's record (STATICINIT)
    public volatile bool Tracking;
    public readonly SortedSet<int> Reached = new();
    // the block's record (null: none)
    public volatile HashSet<int> Block;

    public override bool Equals(object obj)
    {
        if (obj is not string s) return false;
        bool tracking = Tracking;
        var block = Block;
        if ((tracking || block != null) && int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out int n))
        {
            if (tracking) lock (Reached) Reached.Add(n);
            if (block != null) lock (block) block.Add(n);
        }
        return s == Now;
    }

    public override int GetHashCode() => 0;
}

// the mutants mode's decisions, pure so the selftest pins them (review M5): which block is refused, when the canary
// runs, which fingerprints are strict, and whether a mutant's block asked its own switch as the base did (review I2)
internal sealed class MutantLoop
{
    private readonly int every_;
    private readonly HashSet<int> known_;
    private int mutants_, last_;
    private bool anyBlock_;
    private readonly Dictionary<string, HashSet<int>> baseReached_ = new(StringComparer.Ordinal);

    // the mutants' tags (each block's) since the last canary
    public readonly List<string> SinceCanary = new();

    public MutantLoop(int every, IEnumerable<int> known) => (every_, known_) = (every, known.ToHashSet());

    // why a block is refused (null: it runs): a switch no mutant of the plan has; a mutant first (the fingerprints'
    // base is an unmutated block); an unmutated block after a mutant's (review M4: its fingerprint would quietly take
    // a mutant's leak as the new base)
    public string Refuse(string tag, int n)
    {
        if (n != 0 && !known_.Contains(n)) return $"{tag}: switch {n} is no mutant of the plan";
        if (n != 0 && !anyBlock_) return $"{tag}: the first block must be unmutated (the fingerprints' base)";
        if (n == 0 && mutants_ > 0)
            return $"{tag}: an unmutated block after a mutant's (its fingerprint would be a new base)";
        return null;
    }

    // a block (not refused) starts: whether the canary runs first. Before a new mutant's first block once every
    // canary_every mutants ran, so all of a mutant's blocks, its scope alone too, run between two canaries
    public bool Begin(int n)
    {
        anyBlock_ = true;
        if (n == 0 || n == last_) return false;
        bool canary = mutants_ > 0 && mutants_ % every_ == 0 && SinceCanary.Count > 0;
        mutants_++;
        last_ = n;
        return canary;
    }

    // the fingerprint after a block is strict (a change stops) after a mutant's; after an unmutated one (only before
    // the first mutant: Refuse) it is the new base. A canary's is strict
    public static bool Strict(int n) => n != 0;

    // a block ended: a mutant's tag joins those since the canary; whether the canary runs now (the block threw
    // outside its flows)
    public bool End(string tag, int n, bool threw)
    {
        if (n != 0) SinceCanary.Add(tag);
        return threw && SinceCanary.Count > 0;
    }

    public void CanaryRan() => SinceCanary.Clear();

    public bool CanaryAtQuit => SinceCanary.Count > 0;

    // review I2: the switches a block asked (reached). An unmutated block's (the base's, a canary's) join its
    // selection's. A mutant's block in which every flow passed must ask its own switch exactly when an unmutated
    // block of the same selection did: the replay is deterministic and a mutant runs as the base up to its switch,
    // so a difference comes from state an earlier block left, such as a process-lifetime cache the base filled that
    // hides the site from every later mutant (the harness's module list did: review I1). Null, or the STOP's text
    public string Reach(string tag, int n, string sel, bool passed, ICollection<int> reached)
    {
        if (n == 0)
        {
            if (!baseReached_.TryGetValue(sel, out var set)) baseReached_[sel] = set = new HashSet<int>();
            set.UnionWith(reached);
            return null;
        }
        if (!passed) return null;
        if (!baseReached_.TryGetValue(sel, out var seen))
            return $"{tag}: no unmutated block replayed its selection, so its switch's reach has nothing to match";
        bool inBase = seen.Contains(n), inBlock = reached.Contains(n);
        if (inBase && !inBlock)
            return $"{tag}: its switch was reached in the base but not in its own block (a process-lifetime cache)";
        if (!inBase && inBlock)
            return $"{tag}: its switch was reached in its own block but in no unmutated block of its selection " +
                   "(state an earlier block left)";
        return null;
    }
}

// review I3: the schema's static variants (MutantSchema.StaticVariant: the cache __c<n>_F and the maker __i<n>_F of
// each XI.QuestContent type), found by reflection. Each block clears every cache, and a mutant's block makes its own
// variants first, under its switch, as a fresh process's class constructor makes the field: once per block (never a
// value another block made), and a maker that throws sends the mutant to option 1, where it poisons its whole type
internal sealed class SchemaVariants
{
    private static readonly Regex Maker = new($"^{MutantSchema.VariantMakerPrefix}([0-9]+)_(.+)$");
    private const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    private readonly List<FieldInfo> caches_ = new();
    private readonly SortedDictionary<int, List<(FieldInfo Cache, MethodInfo Maker)>> byN_ = new();

    public SchemaVariants(Assembly a)
    {
        foreach (var t in a.GetTypes().Where(t => !t.ContainsGenericParameters).OrderBy(t => t.FullName))
        {
            caches_.AddRange(t.GetFields(Flags).Where(f => Program.SchemaVariant.IsMatch(f.Name)));
            foreach (var m in t.GetMethods(Flags).OrderBy(m => m.Name, StringComparer.Ordinal))
            {
                var g = Maker.Match(m.Name);
                if (!g.Success || m.GetParameters().Length != 0) continue;
                int n = int.Parse(g.Groups[1].Value, CultureInfo.InvariantCulture);
                var cache = t.GetField($"{MutantSchema.VariantCachePrefix}{n}_{g.Groups[2].Value}", Flags) ??
                            throw new InvalidOperationException($"{t.FullName}.{m.Name}: a variant with no cache");
                if (!byN_.TryGetValue(n, out var list)) byN_[n] = list = new();
                list.Add((cache, m));
            }
        }
    }

    // the mutants with a static variant
    public IReadOnlyCollection<int> Numbers => byN_.Keys;

    public void Clear()
    {
        foreach (var f in caches_) f.SetValue(null, null);
    }

    // mutant n's variants made (its switch set by the caller): null, or which one threw and why
    public string Make(int n)
    {
        if (!byN_.TryGetValue(n, out var list)) return null;
        foreach (var (cache, maker) in list)
        {
            try
            {
                cache.SetValue(null, maker.Invoke(null, null));
            }
            catch (TargetInvocationException e)
            {
                var inner = e.InnerException ?? e;
                string field = maker.Name.Substring(maker.Name.IndexOf('_', 3) + 1);
                return $"{maker.DeclaringType.FullName}.{field}: {inner.GetType().Name}: " +
                       inner.Message.Replace('\n', ' ').Replace('\r', ' ');
            }
        }
        return null;
    }
}

internal static partial class Program
{
    // schema <mutants.json> --out <dir> (MutantSchema.Mode)
    private static int SchemaArgs(List<string> args, string outDir)
    {
        if (args.Count != 2 || outDir == null)
        {
            Console.WriteLine("schema <mutants.json> --out <dir>");
            return 1;
        }
        return MutantSchema.Mode(Root, Path.GetFullPath(args[1]), outDir);
    }

    internal const string MutantsUsage = "mutants <plan.json> --db <lane>";

    // why the mutants mode is refused with these arguments (null: it goes): one plan file, --db <a lane> and --port
    internal static string MutantsError(IReadOnlyList<string> argv)
    {
        var rest = new List<string>();
        string db = null;
        for (int i = 0; i < argv.Count; i++)
        {
            if ((argv[i] == "--db" || argv[i] == "--port") && i + 1 < argv.Count)
            {
                if (argv[i] == "--db") db = argv[i + 1];
                i++;
                continue;
            }
            rest.Add(argv[i]);
        }
        if (rest.Count == 0 || rest[0] != "mutants") return $"{MutantsUsage}: the mode first";
        var extra = rest.Skip(2).ToList();
        if (extra.Count > 0)
            return $"{MutantsUsage}: no other option or mode (--jobs, --shard, --counts, --fail-fast, " +
                   $"--no-gate, ...), not {string.Join(" ", extra)}";
        if (rest.Count < 2) return $"{MutantsUsage}: a plan file";
        if (rest[1] == "all" || rest[1].StartsWith("--", StringComparison.Ordinal))
            return $"{MutantsUsage}: a plan file, not {rest[1]} (replay all runs alone)";
        // review M2: in any case (MySQL on Windows folds table and database names: XIDB is xidb)
        if (db == null || string.Equals(db, DefaultDb, StringComparison.OrdinalIgnoreCase))
            return $"{MutantsUsage}: on a lane (--db <lane>), never the default {DefaultDb}";
        return null;
    }

    // the counters a run keeps, back to a fresh process's before each block; and the C# module list (review I1: its
    // modules' constructors are mutant sites, so each block makes its own, as a fresh process does)
    internal static void ResetRun()
    {
        _pass = _fail = 0;
        coverage_.Declared.Clear();
        coverage_.Calls.Clear();
        coverage_.Ran.Clear();
        coverage_.NoHandlers.Clear();
        coverage_.Pass = coverage_.Fail = 0;
        replaying_ = null;
        PrimeHour = true;
        ResendAttack = true;
        twins_ = null;
        synthetic_ = null;
        csharpModules_ = null;
        stepWarnings_.Clear();
    }

    private static readonly Regex MutantCommand = new(@"^RUN ([A-Za-z0-9_.:/#-]+) (0|[1-9][0-9]*) (\S+)$");

    // a command line: (tag, switch, selector) of RUN, null tag for QUIT, or why it is no command
    internal static string ParseMutantCommand(string line, out string tag, out int n, out string sel)
    {
        tag = sel = null;
        n = 0;
        if (line == "QUIT") return null;
        var m = MutantCommand.Match(line ?? "");
        if (!m.Success)
            return "not a command: " +
                   (line == null ? "(end of input)" : line.Length > 80 ? line.Substring(0, 80) + "..." : line);
        tag = m.Groups[1].Value;
        sel = m.Groups[3].Value;
        return int.TryParse(m.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out n)
            ? null
            : "a switch number out of range";
    }

    internal static string BlockBegin(string tag, int n) => $"MUTANT {tag} BEGIN a={n}";
    internal static string BlockEnd(string tag, string summary) => $"MUTANT {tag} END {summary}";
    internal static string VariantThrew(string tag, string why) => $"VARIANT-THREW {tag} {why}";

    private sealed class StopRun : Exception
    {
        public StopRun(string why) : base(why) { }
    }

    private static MutantSwitch switch_;

    private static int Mutants(string[] argv, List<string> args, Stopwatch clock)
    {
        if (MutantsError(argv) is { } refused)
        {
            Console.WriteLine($"{refused}: {string.Join(" ", argv)}");
            return 1;
        }
        if (Environment.GetEnvironmentVariable(MutantSchema.SwitchEnv) != null)
        {
            Console.WriteLine($"mutants: {MutantSchema.SwitchEnv} is set; the process sets its switch itself");
            return 1;
        }
        using var planDoc = JsonDocument.Parse(File.ReadAllText(args[1]));
        var plan = planDoc.RootElement;
        string union = plan.GetProperty("union").GetString(), canary = plan.GetProperty("canary").GetString();
        var known = plan.GetProperty("mutants").EnumerateObject().ToDictionary(p => p.Value.GetInt32(), p => p.Name);
        var planVariants = plan.GetProperty("variants").EnumerateArray().Select(v => v.GetInt32()).ToHashSet();
        int canaryEvery = plan.TryGetProperty("canary_every", out var ce) ? ce.GetInt32() : 10;
        long ceiling = plan.TryGetProperty("heap_ceiling_mb", out var hc) ? hc.GetInt64() : 2048;

        // the switch, then every class constructor of XI.QuestContent with the switch recording (before any of its
        // code), and the setup with it still recording
        switch_ = new MutantSwitch { Tracking = true };
        AppContext.SetData(MutantSchema.SwitchKey, switch_);
        var qc = typeof(XI.QuestContent.Zone).Assembly;
        foreach (var t in qc.GetTypes().Where(t => !t.ContainsGenericParameters))
        {
            try { RuntimeHelpers.RunClassConstructor(t.TypeHandle); }
            catch (TypeInitializationException e)
            {
                Console.WriteLine($"STATICINIT-THREW {t.FullName}: {e.InnerException?.Message}");
            }
        }
        var variants = new SchemaVariants(qc);

        var settings = new NetworkSettings
        {
            SqlHost = "127.0.0.1", SqlPort = dbPort_, SqlLogin = "root", SqlPassword = "root", SqlDatabase = dbName_,
        };
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
        Console.WriteLine($"lane {dbName_}");
        int code = 0;
        try
        {
            Setup(database);
            var all = LoadSelected(FlowPaths(), union, path => Flow.Load(path, NpcInZoneData, ItemIsWeapon, ItemExists),
                                   Flow.LoadStub);
            if (all == null || !ItemStackGate(all))
                throw new StopRun("the union does not load (a parse or the item stacks)");
            var chosen = Select(all, union);
            if (chosen == null || !FitsRange(chosen)) throw new StopRun("the union is no selection");
            if (chosen.FirstOrDefault(f => f.Stub) is { } stub)
                throw new StopRun($"{stub.Name}: picked, but left unparsed");
            var ids = chosen.SelectMany(f => CharsFor(all.IndexOf(f), f.Chars.Count)).Distinct().OrderBy(i => i)
                .ToList();
            if (!LockChars(settings, ids)) throw new StopRun("the union's char ids are in use");
            OpenWorldTables($"c{ids.Min()}");
            handlerCalled_ = (key, label) => coverage_.Add(key, label, replaying_);
            var store = PacketStore.Load(GoldenDir);
            PrimeMemos();
            switch_.Tracking = false;
            Console.WriteLine("STATICINIT" + string.Concat(switch_.Reached.Select(n => " " + n)));
            if (!variants.Numbers.ToHashSet().SetEquals(planVariants))
                throw new StopRun($"the schema's static variants ({string.Join(" ", variants.Numbers)}) are not the " +
                                  $"plan's ({string.Join(" ", planVariants.OrderBy(n => n))})");
            var tables = FingerprintTables();
            var charTables = CharIdTables();
            Console.WriteLine($"mutants: {chosen.Count} flows in the union, {ids.Count} char ids locked, " +
                              $"{known.Count} mutants ({variants.Numbers.Count} with a static variant), " +
                              $"{tables.Count} tables fingerprinted, a canary every {canaryEvery} mutants " +
                              $"({clock.Elapsed.TotalSeconds:F0} s)");
            Console.WriteLine("READY");

            string lastFingerprint = null;
            var loop = new MutantLoop(canaryEvery, known.Keys);
            void Fingerprint(string tag, bool strict)
            {
                var t = Stopwatch.StartNew();
                string stat = StaticFingerprint(out var parts);
                string dbSum = DbFingerprint(tables, charTables, ids, out long rows, out var sums);
                long heap = GC.GetTotalMemory(false) >> 20, ws = Process.GetCurrentProcess().WorkingSet64 >> 20;
                string now = $"static={stat} db={dbSum}";
                Console.WriteLine($"FINGERPRINT {tag} {now} rows={rows} heap={heap} ws={ws} " +
                                  $"({t.ElapsedMilliseconds} ms)");
                if (rows != 0) throw new StopRun($"{tag}: {rows} rows of the locked char ids left after the block");
                if (heap > ceiling)
                    throw new StopRun($"{tag}: the heap is {heap} MB, over the ceiling of {ceiling} MB");
                if (lastFingerprint != null && lastFingerprint != now)
                {
                    var was = lastParts_;
                    string what = lastFingerprint.Split(' ')[0] != now.Split(' ')[0]
                        ? "the static fingerprint: " + string.Join(", ", parts
                            .Where(p => !was.TryGetValue(p.Key, out var h) || h != p.Value).Select(p => p.Key).Take(8))
                        : "the database fingerprint: " + DbDifference(sums);
                    if (strict) throw new StopRun($"{tag} changed {what}");
                    Console.WriteLine($"FINGERPRINT-BASE {tag} (unmutated) changed {what}: the new base");
                }
                lastFingerprint = now;
                lastParts_ = parts;
                lastDb_ = sums;
            }
            // one selector replayed with switch n (0: none), the switches it asks recorded: (threw, reached)
            (bool, HashSet<int>) Replayed(int n, string sel)
            {
                var reached = new HashSet<int>();
                switch_.Now = n.ToString(CultureInfo.InvariantCulture);
                switch_.Block = reached;
                try
                {
                    return (RunBlock(all, sel, store), reached);
                }
                finally
                {
                    switch_.Now = "0";
                    switch_.Block = null;
                }
            }
            void Canary()
            {
                ResetRun();
                variants.Clear();
                var t = Stopwatch.StartNew();
                Console.WriteLine($"CANARY BEGIN after {string.Join(",", loop.SinceCanary)}");
                var (threw, reached) = Replayed(0, canary);
                string summary = $"{_pass} passed, {_fail} failed ({t.Elapsed.TotalSeconds:F0} s)";
                Console.WriteLine(summary);
                bool pass = _fail == 0 && !threw;
                Console.WriteLine($"CANARY END {summary} {(pass ? "pass" : "FAIL")}");
                if (!pass)
                    throw new StopRun($"the canary failed: rerun {string.Join(",", loop.SinceCanary)} with option 1");
                Fingerprint("CANARY", true);
                loop.Reach("CANARY", 0, canary, true, reached);
                loop.CanaryRan();
            }
            while (true)
            {
                string line = Console.In.ReadLine();
                if (ParseMutantCommand(line, out string tag, out int n, out string sel) is { } bad)
                    throw new StopRun(bad);
                if (tag == null)
                {
                    if (loop.CanaryAtQuit) Canary();
                    Console.WriteLine("BYE");
                    break;
                }
                if (loop.Refuse(tag, n) is { } no) throw new StopRun(no);
                if (loop.Begin(n)) Canary();
                ResetRun();
                variants.Clear();
                if (n != 0)
                {
                    switch_.Now = n.ToString(CultureInfo.InvariantCulture);
                    string threwVariant;
                    try { threwVariant = variants.Make(n); }
                    finally { switch_.Now = "0"; }
                    if (threwVariant != null)
                    {
                        Console.WriteLine(VariantThrew(tag, threwVariant));
                        Fingerprint(tag, true);
                        loop.End(tag, n, false);
                        continue;
                    }
                }
                var t = Stopwatch.StartNew();
                Console.WriteLine(BlockBegin(tag, n));
                var (threw, reached) = Replayed(n, sel);
                string summary = $"{_pass} passed, {_fail} failed ({t.Elapsed.TotalSeconds:F0} s)";
                Console.WriteLine(summary);
                Console.WriteLine(BlockEnd(tag, summary));
                Fingerprint(tag, MutantLoop.Strict(n));
                if (loop.Reach(tag, n, sel, _fail == 0 && !threw, reached) is { } why) throw new StopRun(why);
                // a block that threw outside its flows: the canary at once
                if (loop.End(tag, n, threw)) Canary();
            }
        }
        catch (StopRun e)
        {
            Console.WriteLine($"STOP {e.Message}");
            code = 2;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"STOP an exception outside any block: {ex}");
            code = 2;
        }
        finally
        {
            switch_.Now = "0";
            switch_.Tracking = false;
            scripts?.Dispose();
            scripts = null;
            CloseSnapshot();
            DropWorldTables();
            UnlockChars();
            fpDb_?.Dispose();
            fpDb_ = null;
        }
        return code;
    }

    // one selector replayed as `replay <selector> --fail-fast --no-gate` replays it (Main), on the union's loaded flows
    // and locked ids; true when it threw outside its flows (printed as Main prints it: FAIL exception)
    private static bool RunBlock(List<Flow> all, string sel, PacketStore store)
    {
        try
        {
            var chosen = Select(all, sel);
            if (chosen == null) return false;
            if (chosen.FirstOrDefault(f => f.Stub) is { } stub)
                throw new InvalidOperationException($"{stub.Name}: picked, but the plan's union left it unparsed");
            if (!FitsRange(chosen)) return false;
            var ids = chosen.SelectMany(f => CharsFor(all.IndexOf(f), f.Chars.Count)).ToList();
            Console.WriteLine(NoGateLine);
            CleanupPartyCheck(ids);
            string stoppedAt = _fail > 0 ? GateStopLabel(true) : null;
            var ran = new List<string>();
            foreach (var f in chosen)
            {
                if (stoppedAt != null) break;
                int failedBefore = _fail;
                Replay(f, all.IndexOf(f), store);
                ran.Add(f.Name);
                if (_fail > failedBefore) stoppedAt = f.Name;
            }
            if (stoppedAt != null)
                Console.WriteLine(FailFastSummary(stoppedAt, ran, chosen.Select(f => f.Name).ToList()));
            else CoverageGate(coverage_, all, null, null);
            return false;
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FAIL exception: {ex}");
            return true;
        }
        finally
        {
            if (world_ != null) world_.Chars = null;
        }
    }

    // ---- the fingerprints -------------------------------------------------------------------------------------------

    private static Dictionary<string, string> lastParts_ = new();
    private static Dictionary<string, string> lastDb_ = new();

    // the statics a mutant could leave changed: every static field of XI.QuestContent (the compiler's lambda caches
    // and the schema's own variant caches __c<n>_<F> aside: each block clears and makes those), the named ones of
    // XI.Zone and XI.Common, and the harness's process caches. {field: hash}; the hash of them all
    internal static string StaticFingerprint(out Dictionary<string, string> parts)
    {
        parts = new Dictionary<string, string>(StringComparer.Ordinal);
        var fields = new List<FieldInfo>();
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
                                   BindingFlags.DeclaredOnly;
        foreach (var t in typeof(XI.QuestContent.Zone).Assembly.GetTypes()
                     .Where(t => !t.ContainsGenericParameters && !t.Name.StartsWith('<'))
                     .OrderBy(t => t.FullName, StringComparer.Ordinal))
            fields.AddRange(t.GetFields(flags).Where(f => !f.IsLiteral && !f.Name.StartsWith('<') &&
                                                          !SchemaVariant.IsMatch(f.Name))
                .OrderBy(f => f.Name, StringComparer.Ordinal));
        foreach (var (type, name) in NamedStatics)
        {
            var f = type.GetField(name, flags) ??
                    throw new InvalidOperationException(
                        $"the static fingerprint names {type.FullName}.{name}, which is gone");
            fields.Add(f);
        }
        using var sha = System.Security.Cryptography.SHA256.Create();
        var all = new StringBuilder();
        foreach (var f in fields)
        {
            var h = new ValueHasher();
            h.Value(f.GetValue(null));
            string hash = h.Hash();
            parts[$"{f.DeclaringType.FullName}.{f.Name}"] = hash;
            all.Append(hash);
        }
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(all.ToString()))).Substring(0, 16)
            .ToLowerInvariant();
    }

    internal static readonly Regex SchemaVariant = new($"^{MutantSchema.VariantCachePrefix}[0-9]+_");

    // the fingerprinted memo caches of constant tables, made on first use and the same in every world: made before
    // the first fingerprint, so a mutant's block that first reaches one (a shared handler label in a failure's text
    // names its zone; a string's Shift-JIS) is no change. N2's M29 stopped the process on zoneConstNames_ so
    internal static void PrimeMemos()
    {
        XI.Zone.ZoneHost.ZoneConstName(0);
        XI.Zone.Sjis.FromUnicode("");
    }

    internal static readonly string[] PrimedMemos = { "zoneConstNames_", "map_" };

    // the XI.Zone / XI.Common statics of the design (section 2.2) and the harness's process caches (not
    // csharpModules_, which each block makes again: ResetRun; not loader_: the char loader caches database rows as the
    // flows read them (ItemData) and takes each world's hooks)
    private static readonly (Type, string)[] NamedStatics =
    {
        (typeof(XI.Zone.GameOkPackets), "AttrOverride"), (typeof(XI.Zone.MountUtils), "SpeedLimit"),
        (typeof(XI.Zone.MountUtils), "MountSpeed"), (typeof(XI.Zone.MountUtils), "BaseSpeed"),
        (typeof(XI.Zone.ZoneHost), "zoneConstNames_"), (typeof(XI.Zone.Sjis), "map_"),
        (typeof(XI.Common.EarthTime), "offsetMs_"),
        (typeof(Program), nameof(zoneNpcs_)), (typeof(Program), nameof(itemStackExempt_)),
        (typeof(Program), nameof(worldSnapshot_)), (typeof(Program), nameof(stateTables_)),
    };

    // a value's hash over its graph: primitives and strings by value, collections by their elements, objects of XI's
    // own assemblies (and value types) by their fields, anything else (a database, a regex) by its type only
    internal sealed class ValueHasher
    {
        private readonly StringBuilder sb_ = new();
        private readonly Dictionary<object, int> seen_ = new(ReferenceEqualityComparer.Instance);
        private int budget_ = 5_000_000;

        public string Hash() =>
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(sb_.ToString())))
                .Substring(0, 16).ToLowerInvariant();

        public void Value(object o)
        {
            if (--budget_ < 0) throw new InvalidOperationException("the static fingerprint's graph is too large");
            if (o == null) { sb_.Append('~'); return; }
            var t = o.GetType();
            if (t.IsPrimitive || t.IsEnum || o is string || o is decimal)
            {
                sb_.Append(t.Name).Append(':').Append(Convert.ToString(o, CultureInfo.InvariantCulture)).Append(';');
                return;
            }
            if (o is Type ty) { sb_.Append("t:").Append(ty.FullName).Append(';'); return; }
            if (!t.IsValueType)
            {
                if (seen_.TryGetValue(o, out int id)) { sb_.Append('@').Append(id).Append(';'); return; }
                seen_[o] = seen_.Count;
            }
            // a delegate by its method only: a world's hooks (GameOkPackets.AttrOverride, the char loader's) are new
            // closures over each new world, the same code
            if (o is Delegate d)
            {
                foreach (var one in d.GetInvocationList())
                    sb_.Append("d:").Append(one.Method.DeclaringType?.FullName).Append('.').Append(one.Method.Name)
                        .Append(';');
                return;
            }
            if (o is IDatabase) { sb_.Append("db:").Append(t.FullName).Append(';'); return; }
            if (o is Array a)
            {
                sb_.Append('[');
                foreach (var x in a) Value(x);
                sb_.Append(']');
                return;
            }
            if (o is System.Collections.IEnumerable e && t.Namespace != null &&
                t.Namespace.StartsWith("System.Collections", StringComparison.Ordinal))
            {
                sb_.Append(t.Name).Append('[');
                foreach (var x in e) Value(x);
                sb_.Append(']');
                return;
            }
            // XI's own objects and every struct (a dictionary's KeyValuePair, a tuple) by their fields
            if (Ours(t) || t.IsValueType)
            {
                sb_.Append(t.Name).Append('{');
                for (var c = t; c != null && c != typeof(object); c = c.BaseType)
                    foreach (var f in c.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                                  BindingFlags.DeclaredOnly))
                    {
                        sb_.Append(f.Name).Append('=');
                        Value(f.GetValue(o));
                    }
                sb_.Append('}');
                return;
            }
            sb_.Append("o:").Append(t.FullName).Append(';');
        }

        private static bool Ours(Type t) =>
            t.Assembly.GetName().Name is { } n && n.StartsWith("XI.", StringComparison.Ordinal);
    }

    // the fingerprint's own connection (MySqlConnector: CHECKSUM TABLE is no SELECT for ResultSet), kept for the run
    private static MySqlConnector.MySqlConnection fpDb_;

    private static List<(string A, string B)> Query(string q)
    {
        if (fpDb_ == null)
        {
            fpDb_ = new MySqlConnector.MySqlConnection(StateConnectionString);
            fpDb_.Open();
        }
        using var cmd = new MySqlConnector.MySqlCommand(q, fpDb_);
        using var r = cmd.ExecuteReader();
        var rows = new List<(string, string)>();
        while (r.Read())
            rows.Add((Convert.ToString(r.GetValue(0), CultureInfo.InvariantCulture),
                      r.FieldCount > 1 ? Convert.ToString(r.GetValue(1), CultureInfo.InvariantCulture) : null));
        return rows;
    }

    // the tables of the database fingerprint: every base table with no charid column, the private world tables
    // (golden_*, restored at each world) aside
    private static List<string> FingerprintTables() =>
        Query("SELECT t.table_name FROM information_schema.tables t WHERE t.table_schema = DATABASE() " +
              "AND t.table_type = 'BASE TABLE' AND t.table_name NOT LIKE 'golden\\_%' AND NOT EXISTS (SELECT 1 " +
              "FROM information_schema.columns c WHERE c.table_schema = t.table_schema AND " +
              "c.table_name = t.table_name AND c.column_name = 'charid') ORDER BY t.table_name")
            .Select(r => r.A).ToList();

    // the tables with a charid column (Cleanup's), the private world tables aside: listed once a run
    private static List<string> CharIdTables() =>
        Query("SELECT DISTINCT table_name FROM information_schema.columns WHERE table_schema = DATABASE() " +
              "AND column_name = 'charid' AND table_name NOT LIKE 'golden\\_%' ORDER BY table_name")
            .Select(r => r.A).ToList();

    private static Dictionary<string, string> DbChecksums(List<string> tables)
    {
        var sums = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < tables.Count; i += 50)
            foreach (var (t, sum) in
                     Query("CHECKSUM TABLE " + string.Join(", ", tables.Skip(i).Take(50).Select(t => $"`{t}`"))))
                sums[t] = sum;
        return sums;
    }

    // the checksums (one CHECKSUM TABLE pass: sums) and their hash, and the rows of the ids in every charid table,
    // chars and accounts (Cleanup's tables)
    private static string DbFingerprint(List<string> tables, List<string> charTables, List<uint> ids, out long rows,
                                        out Dictionary<string, string> sums)
    {
        sums = DbChecksums(tables);
        string text = string.Join("|", sums.OrderBy(k => k.Key, StringComparer.Ordinal)
            .Select(k => $"{k.Key}={k.Value}"));
        string inList = string.Join(",", ids);
        var counts = charTables.Select(t => $"(SELECT COUNT(*) FROM `{t}` WHERE charid IN ({inList}))")
            .Append($"(SELECT COUNT(*) FROM accounts WHERE id IN ({inList}))")
            .Append($"(SELECT COUNT(*) FROM accounts_parties WHERE partyid IN ({inList}) OR allianceid IN ({inList}))");
        rows = long.Parse(Query("SELECT " + string.Join(" + ", counts)).Single().A, CultureInfo.InvariantCulture);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text)))
            .Substring(0, 16).ToLowerInvariant();
    }

    // the tables whose checksums differ from the last fingerprint's
    private static string DbDifference(Dictionary<string, string> now) =>
        string.Join(", ", now.Where(k => !lastDb_.TryGetValue(k.Key, out var v) || v != k.Value).Select(k => k.Key)
            .Take(8));
}
