using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

// mutant-design (option 3): the mutants mode's refusals, its command and block lines, the counters' reset, the
// harness's statics placed, the switch, the loop's decisions (the canary, the strict fingerprints, the refused
// blocks, the switches' reach), the static fingerprint's hasher, the static variants made and cleared, and the schema
// generator with its projection proof on a made-up XI.QuestContent file and on one real file (no database). The
// runner's side: python -I -B tools/golden_mutants.py --selftest
internal static partial class Program
{
    private static void SelfTestMutants()
    {
        Console.WriteLine("mutant-design: the mutants mode, the schema generator");
        string R(params string[] a) => MutantsError(a) ?? "-";
        Check("  mutants: a plan file on a lane goes (--db and --port before or after)", "-|-|-",
              string.Join("|", R("mutants", "p.json", "--db", "xidb_lane7"),
                          R("--db", "xidb_lane7", "mutants", "p.json"),
                          R("mutants", "p.json", "--port", "3307", "--db", "xidb_lane7")));
        const string lane = "xidb_lane7";
        var refused = new[]
        {
            R("mutants", "all", "--db", lane), R("mutants", "p.json", "--db", "xidb"),
            R("mutants", "p.json", "--db", "XIDB"), R("mutants", "p.json"),
            R("mutants", "p.json", "--db", lane, "--jobs", "6"),
            R("mutants", "p.json", "--db", lane, "--shard", "1/2"),
            R("mutants", "p.json", "--db", lane, "--counts", "c.txt"),
            R("mutants", "p.json", "--db", lane, "--fail-fast"), R("mutants", "p.json", "--db", lane, "--no-gate"),
            R("mutants", "p.json", "x", "--db", lane), R("mutants", "--db", lane), R("replay", "p.json", "--db", lane),
        };
        string Why(string w) =>
            w.Contains("not all") ? "all" : w.Contains("never the default") ? "xidb"
            : w.Contains("not --jobs") ? "--jobs" : w.Contains("not --shard") ? "--shard"
            : w.Contains("not --counts") ? "--counts" : w.Contains("not --fail-fast") ? "--fail-fast"
            : w.Contains("not --no-gate") ? "--no-gate" : w.Contains("not x") ? "x"
            : w.Contains("a plan file") ? "plan" : w.Contains("the mode first") ? "mode" : w;
        Check("  mutants: refused with all, the default db (in any case, or none), --jobs, --shard, --counts, " +
              "--fail-fast, --no-gate, a second positional, no plan, another mode",
              "all, xidb, xidb, xidb, --jobs, --shard, --counts, --fail-fast, --no-gate, x, plan, mode",
              string.Join(", ", refused.Select(Why)));

        string Cmd(string line) => ParseMutantCommand(line, out var tag, out int n, out var sel) is { } bad ? "bad"
            : tag == null ? "quit" : $"{tag}/{n}/{sel}";
        Check("  mutants: the commands (RUN <tag> <switch> <selector>, QUIT; anything else is no command)",
              "M01/17/a.b,flow:c#d|BASE/SC1/0/x|M01/scope/3/y|quit|bad|bad|bad|bad|bad",
              string.Join("|", new[]
              {
                  "RUN M01 17 a.b,flow:c#d", "RUN BASE/SC1 0 x", "RUN M01/scope 3 y", "QUIT", "RUN M01 x y",
                  "RUN M01 017 a", "RUN M01 1 a b", "RUN M 01 a", null,
              }.Select(Cmd)));
        Check("  mutants: the block lines and a variant's throw",
              "MUTANT M01 BEGIN a=17|MUTANT M01 END 3 passed, 1 failed (9 s)|VARIANT-THREW M01 X.F: E: m",
              BlockBegin("M01", 17) + "|" + BlockEnd("M01", "3 passed, 1 failed (9 s)") + "|" +
              VariantThrew("M01", "X.F: E: m"));

        SelfTestMutantsReset();
        SelfTestMutantsLoop();

        // the hasher: by value, a change shows, a delegate by its method
        string H(object o)
        {
            var h = new ValueHasher();
            h.Value(o);
            return h.Hash();
        }
        var d1 = new Dictionary<string, int[]> { ["a"] = new[] { 1, 2 } };
        var d2 = new Dictionary<string, int[]> { ["a"] = new[] { 1, 2 } };
        string h1 = H(d1);
        d2["a"][1] = 3;
        Check("  mutants: the hasher hashes by value, a change shows, a delegate by its method", "True,False,True",
              $"{h1 == H(new Dictionary<string, int[]> { ["a"] = new[] { 1, 2 } })},{h1 == H(d2)}," +
              $"{H(MakeHook(1)) == H(MakeHook(2))}");

        // the memo caches primed before the first fingerprint: each a named static, and set after PrimeMemos; the
        // module list is made per block (ResetRun), so it is no named static
        PrimeMemos();
        var named = (typeof(Program).GetField("NamedStatics", BindingFlags.Static | BindingFlags.NonPublic)
            .GetValue(null) as (Type, string)[]).ToList();
        string Primed(string m) =>
            named.FirstOrDefault(x => x.Item2 == m) is var (t, f) && t != null &&
            t.GetField(f, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null) != null
                ? $"{m} set named"
                : $"{m} null";
        Check("  mutants: PrimeMemos makes each primed memo cache, and each is fingerprinted; the module list is not",
              "zoneConstNames_ set named,map_ set named,False",
              string.Join(",", PrimedMemos.Select(Primed)) + "," + named.Any(x => x.Item2 == nameof(csharpModules_)));

        SelfTestSchema();
    }

    private static Func<int> MakeHook(int x) => () => x;

    // ResetRun and the harness's statics: each non-readonly static of Program is reset per block, made per flow (its
    // world), or kept per process; a new one fails here until it is placed (review M5)
    private static void SelfTestMutantsReset()
    {
        int savedPass = _pass, savedFail = _fail;
        var savedModules = csharpModules_;
        _fail += 3;
        coverage_.Add("k", "1/X/onTrigger", "t");
        coverage_.Declared["k"] = new List<string> { "1/X/onTrigger" };
        coverage_.Ran.Add("t");
        coverage_.NoHandlers.Add("k2");
        coverage_.Pass = coverage_.Fail = 2;
        replaying_ = "t";
        PrimeHour = ResendAttack = false;
        stepWarnings_.Add("w");
        csharpModules_ = new();
        ResetRun();
        string after = $"{_pass},{_fail},{coverage_.Calls.Count},{coverage_.Declared.Count},{coverage_.Ran.Count}," +
                       $"{coverage_.NoHandlers.Count},{coverage_.Pass},{coverage_.Fail},{replaying_ ?? "null"}," +
                       $"{PrimeHour},{ResendAttack},{stepWarnings_.Count},{twins_ == null},{synthetic_ == null}," +
                       $"{csharpModules_ == null}";
        (_pass, _fail) = (savedPass, savedFail);
        csharpModules_ = savedModules;
        Check("  mutants: ResetRun clears the counters, the coverage, the flow, the hour priming, the attack resend, " +
              "the warnings, the twins, the synthetic quests and the C# module list",
              "0,0,0,0,0,0,0,0,null,True,True,0,True,True,True", after);
        Check("  mutants: Coverage has only the fields ResetRun clears", "Calls,Declared,Fail,NoHandlers,Pass,Ran",
              string.Join(",", typeof(Coverage)
                  .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                  .Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal)));

        // ResetRun's (each block a fresh process's); each flow's world's (Harness.NewWorld and the replay set them);
        // the process's (the arguments, the setup, the lane's connections and locks, constant tables, the mutants
        // mode's own)
        string[] perBlock = { "_pass", "_fail", "replaying_", "PrimeHour", "ResendAttack", "twins_", "synthetic_",
                              "csharpModules_" };
        string[] perWorld = { "db", "zone", "scripts", "lua", "actor_", "now", "vanaTicks", "unixNow", "worldClock_",
                              "stateDb_", "stateBatch_" };
        string[] perProcess =
        {
            "dbPort_", "dbName_", "luaAt_", "explain_", "rangeStart_", "rangeSize_", "dataDir_", "scriptsDir_",
            "loader_", "world_", "worldSnapshot_", "stateTables_", "schema_", "StateConnectionString",
            "itemStackExempt_", "overlayRoot_", "overlayFor_", "head_", "lockDb_", "handlerCalled_", "zoneNames_",
            "switch_", "fpDb_", "lastParts_", "lastDb_",
        };
        // readonly collections the harness fills: cleared with ResetRun (stepWarnings_, coverage_), by each world's
        // setup (the lists and queues), or by the overlay's setup and teardown
        string[] readonlyReset =
        {
            "coverage_", "stepWarnings_", "log", "got", "actors_", "ipc_", "threw_", "joined_", "overlaid_",
            "overlayDirs_", "overlayOff_", "locked_",
        };
        // known immutable: constants, regexes and tables built once; zoneNpcs_ and itemStacks_ are lazily filled
        // caches of the database's and the data files' rows, which no mutant's quest code changes (review N2)
        string[] immutable =
        {
            "BattlefieldFile", "BenignQuestLines", "BlobTypes", "CallKey", "ClockTypes", "EntityScript", "Escaped",
            "HashedWorldTables", "Ignored", "Jobs", "MutantCommand", "NamedStatics", "NotRecorded", "OtherBits",
            "OtherChar", "PersistFailureLine", "PrimedMemos", "ProbeTwins", "ProgressFailureLine", "RegisteredRe",
            "RequireName", "SchemaVariant", "ScriptLine", "SeedFiles", "SeedOverrides", "SeededWorldTables",
            "SyntheticCities", "VerbCost", "WorldTables", "zoneNpcs_", "itemStacks_",
        };
        var statics = typeof(Program).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
                                                BindingFlags.DeclaredOnly)
            .Where(f => !f.IsLiteral)
            .Select(f => f.Name.StartsWith('<') ? f.Name.Substring(1, f.Name.IndexOf('>') - 1) : f.Name)
            .ToHashSet(StringComparer.Ordinal);
        var placed = perBlock.Concat(perWorld).Concat(perProcess).Concat(readonlyReset).Concat(immutable)
            .ToHashSet(StringComparer.Ordinal);
        Check("  mutants: every static of the harness (readonly too) is placed: reset per block, made per world, or " +
              "kept per process (new: unplaced; gone: placed but no more)", "",
              string.Join(" ", statics.Except(placed).Select(n => "new " + n)
                  .Concat(placed.Except(statics).Select(n => "gone " + n)).OrderBy(n => n, StringComparer.Ordinal)));
    }

    // the switch and the loop's decisions (review M5, I2, M4)
    private static void SelfTestMutantsLoop()
    {
        var sw = new MutantSwitch { Now = "17", Tracking = true };
        string first = $"{Equals(sw, "17")},{Equals(sw, "170")},{Equals(sw, 17)},{Equals(null, "17")}," +
                       string.Join(" ", sw.Reached);
        var block = new HashSet<int>();
        sw.Tracking = false;
        sw.Block = block;
        _ = Equals(sw, "5");
        sw.Block = null;
        _ = Equals(sw, "6");
        Check("  mutants: the switch is its number only; it records what it is asked at the startup (Reached) and " +
              "in a block (Block)", "True,False,False,False,17 170|17 170|5",
              $"{first}|{string.Join(" ", sw.Reached)}|{string.Join(" ", block)}");
        var c42 = SyntaxFactory.ParseExpression(MutantSchema.SwitchCondition(42));
        var other = SyntaxFactory.ParseExpression(MutantSchema.SwitchCondition(42).Replace("XI.M", "XI.N"));
        Check("  mutants: the switch condition is what the projection reads", "42,False",
              $"{(MutantSchema.IsSwitch(c42, out int k) ? k : -1)},{MutantSchema.IsSwitch(other, out _)}");

        // a run's blocks, a canary every 2 mutants: what the loop decides at each
        var loop = new MutantLoop(2, new[] { 1, 2, 3, 4 });
        var said = new List<string>();
        void Run(string tag, int n, bool threw = false)
        {
            if (loop.Refuse(tag, n) is { } no)
            {
                said.Add("refused " + (no.Contains("no mutant of the plan") ? "plan" : no.Contains("must be unmutated")
                    ? "first" : no.Contains("after a mutant") ? "after" : no));
                return;
            }
            if (loop.Begin(n))
            {
                said.Add("canary before " + tag);
                loop.CanaryRan();
            }
            said.Add($"{tag} {(MutantLoop.Strict(n) ? "strict" : "base")}");
            if (loop.End(tag, n, threw))
            {
                said.Add("canary after " + tag);
                loop.CanaryRan();
            }
        }
        Run("M1", 1);
        Run("BASE/SC", 0);
        Run("BASE/RUN", 0);
        Run("M1", 1);
        Run("M1/scope", 1);
        Run("M2", 2);
        Run("M3", 3);
        Run("X", 0);
        Run("M9", 9);
        Run("M4", 4, threw: true);
        Run("M4/scope", 4);
        said.Add("quit " + (loop.CanaryAtQuit ? "canary" : "no canary"));
        Check("  mutants: the loop: a mutant first refused, unmutated blocks the base, a canary before every 2nd new " +
              "mutant (a mutant's blocks between two canaries), after a block that threw, at QUIT; an unmutated " +
              "block after a mutant and a switch not in the plan refused",
              "refused first|BASE/SC base|BASE/RUN base|M1 strict|M1/scope strict|M2 strict|canary before M3|" +
              "M3 strict|refused after|refused plan|M4 strict|canary after M4|M4/scope strict|quit canary",
              string.Join("|", said));

        // the switches' reach (review I2): an unmutated block's join its selection's; a passing mutant's block must
        // reach its switch exactly when they did
        var reach = new MutantLoop(10, new[] { 1, 2, 3, 4, 5 });
        string Reach(string tag, int n, string sel, bool passed, params int[] reached)
        {
            string why = reach.Reach(tag, n, sel, passed, reached);
            return why == null ? "ok" : why.Contains("in the base but not") ? "STOP hidden"
                : why.Contains("in no unmutated block") ? "STOP new"
                : why.Contains("no unmutated block replayed") ? "STOP none" : why;
        }
        var got = new[]
        {
            Reach("BASE/SC", 0, "a", true, 1, 2, 5), Reach("BASE/RUN", 0, "a,b", true, 2),
            Reach("M1", 1, "a,b", true), Reach("M1/scope", 1, "a", true), Reach("M2", 2, "a,b", true, 2),
            Reach("M3", 3, "a", true, 3), Reach("M3", 3, "a", false, 3), Reach("M4", 4, "c", true),
            Reach("CANARY", 0, "a,b", true, 5), Reach("M5", 5, "a,b", true),
        };
        Check("  mutants: the reach: a switch the base reached but its passing block did not (a cache the base " +
              "filled) stops, one the base never reached stops, a failed block is not compared, a selection with no " +
              "unmutated block stops, a canary's reach joins its selection's",
              "ok|ok|ok|STOP hidden|ok|STOP new|ok|STOP none|ok|STOP hidden", string.Join("|", got));
    }

    // the generator on a made-up tree: each shape's status, every schema mutant's projection its patched file; the
    // static variants of the written schema compiled, made and cleared; a real file's patch that does not compile
    private static void SelfTestSchema()
    {
        string root = Path.Combine(Path.GetTempPath(), $"questgolden-schema-{Environment.ProcessId}");
        string qc = Path.Combine(root, "src", "XI.QuestContent");
        Directory.CreateDirectory(qc);
        try
        {
            const string source = @"namespace XI.QuestContent.SchemaTest
{
    public sealed class A
    {
        const int K = 5;
        static readonly System.Collections.Generic.List<int> Table =
            new System.Collections.Generic.List<int> { 1, 2, 3 };
        static readonly string Name = ""abc"".Substring(1);
        static int s_;
        static readonly System.Func<int> Fn = () => 1;
        public int L(int x) { return x; }
        static System.Func<int, int> Wrap(System.Func<int, int> f) => f;
        public System.Func<int, int> H() => Wrap(q => q + 1);
        static A() { s_ = 1; }
        public int F(int a)
        {
            int b = a + 1;
            if (a == 0 && b > 1 && a < 9) return b * 2;
            Log(b);
            return Table[0] + K + s_;
        }
        void Log(int v) { }
        public object G() => new B { P = 1, Q = 2 };
        public int D(int a)
        {
            int[] t = new int[4];
            return t[a + 8 / 2] + (byte)200 + (int.MaxValue + 0);
        }
        public int U(int a)
        {
            using var d = new System.IO.MemoryStream();
            int c = a + 3;
            return c;
        }
        public static int N() => Name.Length + Table.Count;
    }
    public sealed class B { public int P, Q; }
}
";
            File.WriteAllText(Path.Combine(qc, "A.cs"), source);
            const string a = "src/XI.QuestContent/A.cs";
            var inputs = new List<MutantSchema.Input>
            {
                new() { Id = "E1", N = 1, Path = a, Old = "a + 1", New = "a + 2" },
                new() { Id = "E2", N = 2, Path = a, Old = "a == 0 && b > 1", New = "a == 0 || b > 1" },
                new() { Id = "S1", N = 3, Path = a, Old = "            Log(b);\n", New = "" },
                new() { Id = "I1", N = 4, Path = a, Old = "Q = 2", New = "Q = 3" },
                new() { Id = "D1", N = 5, Path = a, Old = "P = 1, ", New = "" },
                new() { Id = "V1", N = 6, Path = a, Old = "{ 1, 2, 3 }", New = "{ 1, 2, 4 }" },
                new() { Id = "C1", N = 7, Path = a, Old = "const int K = 5;", New = "const int K = 6;" },
                new() { Id = "SC", N = 8, Path = a, Old = "s_ = 1;", New = "s_ = 2;" },
                new() { Id = "B1", N = 9, Path = a, Old = "nope", New = "x" },
                new() { Id = "T1", N = 10, Path = a, Old = "return b * 2;", New = "return null;" },
                new() { Id = "X1", N = 11, Path = "src/XI.Zone/Z.cs", Old = "a", New = "b" },
                new() { Id = "R1", N = 12, Path = a, Old = "return Table[0]", New = "return Table[1]" },
                new() { Id = "L1", N = 13, Path = a, Old = "return x;",
                        New = "return new System.Func<int>(() => x)();" },
                new() { Id = "V2", N = 14, Path = a, Old = "() => 1", New = "() => 2" },
                new() { Id = "P1", N = 15, Path = a, Old = "Wrap(q => q + 1)", New = "Wrap(q => q + 1 + 1)" },
                // review C1: a switch would make each constant operand a value, so these would compile as switches
                // (CS0020 needs a constant dividend: `a / 0` compiles, and throws at run time in both options)
                new() { Id = "Z1", N = 16, Path = a, Old = "8 / 2", New = "8 / 0" },
                new() { Id = "Z2", N = 17, Path = a, Old = "(byte)200", New = "(byte)300" },
                new() { Id = "Z3", N = 18, Path = a, Old = "int.MaxValue + 0", New = "int.MaxValue + 1" },
                // review M1: the `using` declaration's statement switch climbs to the body
                new() { Id = "U1", N = 19, Path = a,
                        Old = "            using var d = new System.IO.MemoryStream();\n", New = "" },
                // review I3: a static variant whose initializer throws
                new() { Id = "VT", N = 21, Path = a, Old = "Substring(1)", New = "Substring(9)" },
            };
            var results = MutantSchema.Generate(root, inputs, MutantSchema.References(), out var files, _ => { });
            string Say(MutantSchema.Result r)
            {
                if (r.Status == "schema") return r.Kind;
                if (r.Status == "bad") return "bad";
                if (r.Reason.StartsWith(MutantSchema.NoCompile, StringComparison.Ordinal))
                    return "fallback patch" + (r.Id.StartsWith('Z') ? " " + r.Reason.Split('(', ' ')[7] : "");
                return r.Reason.Contains("reference assembly") ? "fallback ref"
                    : r.Reason.Contains("static constructor") ? "fallback cctor"
                    : r.Reason.Contains("a const") ? "fallback const"
                    : r.Reason.Contains("outside") ? "fallback outside" : "fallback " + r.Reason;
            }
            Check("  schema: each shape's status (expression, a climbed expression, statement, initializer member, " +
                  "deleted member, static variant, const, static constructor, bad, a patch that does not compile, " +
                  "outside, expression, a new lambda and a static variant with a lambda: those change the reference " +
                  "assembly; an insertion after a lambda's body switches in the body, not around the lambda; a " +
                  "division by a constant zero, a constant cast out of range and a constant overflow do not compile; " +
                  "a deleted `using` declaration switches its body; a static variant that throws)",
                  "E1 expression|E2 expression|S1 statement|I1 expression|D1 expression|V1 static-variant|" +
                  "C1 fallback const|SC fallback cctor|B1 bad|T1 fallback patch|X1 fallback outside|R1 expression|" +
                  "L1 fallback ref|V2 fallback ref|P1 expression|Z1 fallback patch CS0020|Z2 fallback patch CS0221|" +
                  "Z3 fallback patch CS0220|U1 body|VT static-variant",
                  string.Join("|", results.Select(r => $"{r.Id} {Say(r)}")));
            Check("  schema: E2 (&& to || changes the tree's shape) and U1 (the `using` declaration) climbed",
                  "True,True",
                  $"{results[1].Climbs > 0},{results.Single(r => r.Id == "U1").Climbs > 0}");
            // the proof again, from the written text: each schema mutant's projection is its patched file, none's the
            // source (and an unknown switch's)
            var gen = CSharpSyntaxTree.ParseText(files.Single().Text, MutantSchema.Parse).GetRoot();
            var src = CSharpSyntaxTree.ParseText(source, MutantSchema.Parse).GetRoot();
            var bad = new List<string>();
            if (!MutantSchema.Same(MutantSchema.Project(gen, 0), src)) bad.Add("none");
            if (!MutantSchema.Same(MutantSchema.Project(gen, 99), src)) bad.Add("99");
            foreach (var (r, m) in results.Zip(inputs).Where(x => x.First.Status == "schema"))
            {
                int at = source.IndexOf(m.Old, StringComparison.Ordinal);
                string patched = source.Substring(0, at) + m.New + source.Substring(at + m.Old.Length);
                if (!MutantSchema.Same(MutantSchema.Project(gen, m.N),
                                       CSharpSyntaxTree.ParseText(patched, MutantSchema.Parse).GetRoot()))
                    bad.Add(m.Id);
            }
            Check("  schema: the written schema file specialised for each mutant is its patched file, for none (and " +
                  "an unknown switch) the source", "", string.Join(",", bad));
            // the proof is no rubber stamp: a schema with one arm swapped does not project to its patched file
            string swapped = files.Single().Text.Replace(", \"1\")?(2)", ", \"1\")?(3)");
            var e1 = CSharpSyntaxTree.ParseText(source.Replace("a + 1", "a + 2"), MutantSchema.Parse).GetRoot();
            Check("  schema: a schema whose arm differs from the patch fails the proof", "True,False",
                  (swapped != files.Single().Text) + "," +
                  MutantSchema.Same(
                      MutantSchema.Project(CSharpSyntaxTree.ParseText(swapped, MutantSchema.Parse).GetRoot(), 1), e1));
            SelfTestVariants(files.Single().Text);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch (IOException) { }
        }

        // review C1 on the real tree: AnEmptyVessel's `numCorrect / 2]` to `/ 0]` (the review's probe) compiles, as the
        // build does (msbuild: 0 errors, wt/mutant-design-sdd/vessel-build): CS0020 is a constant dividend's only, and
        // a local's division by zero throws at run time in both options. So it stays a schema mutant, its patched file
        // checked; `8 / 2` to `8 / 0` above is the CS0020 that falls back
        const string vessel = "src/XI.QuestContent/AhtUrhgan/AnEmptyVessel.cs";
        var probe = MutantSchema.Generate(Root, new List<MutantSchema.Input>
        {
            new() { Id = "Z0", N = 1, Path = vessel, Old = "numCorrect / 2]", New = "numCorrect / 0]" },
        }, MutantSchema.References(), out var probeFiles, _ => { });
        Check("  schema: AnEmptyVessel's numCorrect / 2] to / 0] compiles (a local's division by zero: no " +
              "CS0020), so it is a schema mutant", "schema expression 1",
              $"{probe[0].Status} {probe[0].Kind ?? probe[0].Reason} {probeFiles.Count}");
    }

    // review I3: the written schema's static variants compiled and loaded: the property reads the field's own value
    // with no mutant (asking its switch), the variant's under its switch; SchemaVariants clears the cache, makes it
    // again, and says which variant threw
    private static void SelfTestVariants(string schemaText)
    {
        var comp = CSharpCompilation.Create("SchemaSelfTest",
            new[] { CSharpSyntaxTree.ParseText(schemaText, MutantSchema.Parse) }, MutantSchema.References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var pe = new MemoryStream();
        var emit = comp.Emit(pe);
        if (!emit.Success)
        {
            Fail("  schema: the written schema compiles",
                 emit.Diagnostics.First(d => d.Severity == DiagnosticSeverity.Error).ToString());
            return;
        }
        pe.Position = 0;
        var alc = new AssemblyLoadContext("schema-selftest", isCollectible: true);
        var sw = new MutantSwitch();
        AppContext.SetData(MutantSchema.SwitchKey, sw);
        try
        {
            var asm = alc.LoadFromStream(pe);
            var variants = new SchemaVariants(asm);
            var type = asm.GetType("XI.QuestContent.SchemaTest.A");
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            var table = type.GetProperty("Table", flags);
            var cache = type.GetField("__c6_Table", flags);
            string Read() => string.Join(",", (IEnumerable<int>)table.GetValue(null));
            var block = new HashSet<int>();
            sw.Block = block;
            string none = Read();
            sw.Block = null;
            sw.Now = "6";
            string v1 = Read();
            bool cached = cache.GetValue(null) != null;
            variants.Clear();
            bool cleared = cache.GetValue(null) == null;
            string made = variants.Make(6) ?? "made";
            bool remade = cache.GetValue(null) != null;
            sw.Now = "21";
            string threw = variants.Make(21);
            sw.Now = "0";
            Check("  schema: the static variants (6 and 21): none reads the field's value and asks switch 6, 6 its " +
                  "variant (kept), Clear empties the cache, Make makes it again, and names the variant that throws",
                  "6 21|1,2,3|6|1,2,4|True|True|made|True|True",
                  $"{string.Join(" ", variants.Numbers)}|{none}|{string.Join(" ", block)}|{v1}|{cached}|{cleared}|" +
                  $"{made}|{remade}|" +
                  $"{threw?.StartsWith("XI.QuestContent.SchemaTest.A.Name: ArgumentOutOfRangeException")}");
        }
        finally
        {
            AppContext.SetData(MutantSchema.SwitchKey, null);
            alc.Unload();
        }
    }
}
