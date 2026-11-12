using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

// mutant-design (option 3, docs/superpowers/specs/2026-11-10-golden-mutant-process-design.md, sections 1.2 and
// 6.1): the mutant schema generator and its projection proof. No database, no build.
//
//   schema <mutants.json> --out <dir>
//
// mutants.json: {"mutants": [{"id", "n", "path", "old", "new"}]}, a lane's text patches (regexes already applied), n
// the switch number of the mutant in this run (1, 2, ...). For each file of src/XI.QuestContent the generator writes
// one schema file <dir>/gen/<path under src/XI.QuestContent> holding every mutant of the file as a switch on the run's
// mutant (SwitchCondition: the process's MutantSwitch, else the XI_MUTANT variable, else none), and
// <dir>/schema.json with each mutant's status: `schema` (with its kind), `fallback` (with the reason: option 1's loop
// runs it) or `bad` (its old text does not occur exactly once: BAD MUTANT, as option 1 says). A mutant is a schema
// only when
//   - its patched file compiles (the in-memory compilation of XI.QuestContent with the patched file in its source's
//     place): one that does not falls back, so option 1's build reports it BUILD FAILED (a switch would turn a
//     constant into a value: a division by a constant zero, a constant overflow or an out-of-range constant cast
//     compiles as a switch);
//   - the switch sits at the smallest node that holds both versions: an expression `(C(n) ? (new) : (old))` whose
//     arms have the conditional's own type and the original node's converted type (else the node climbs), a
//     statement `if (C(n)) { new } else { old }` in its statement list (not around a `using` declaration, whose
//     Dispose would move: that climbs), a member body likewise; or, for a site in a static field's initializer, a
//     variant: the field keeps its initializer as __s_<F>, the mutant's initializer goes into a private method
//     __i<n>_<F> whose value a private field __c<n>_<F> keeps once made, F becomes a property that picks the variant
//     while the switch says n (StaticVariant; no lambda, so no compiler member the reference assembly shows);
//   - the schema file compiles (the in-memory compilation of XI.QuestContent with the schema files in place); an
//     error in a switch climbs that switch;
//   - the schema file keeps the reference assembly: the in-memory compilation's (a full emit, as the build's refout:
//     a lambda, a closure or constant data in a new arm adds compiler members it shows) is the original's; the
//     switches that change it (RefCulprits) fall back;
//   - the projection proof holds: the schema file specialised for the mutant (every switch of it resolved to its new
//     arm, every other switch to its old arm, the variants folded back to the field: Project) is the text-patched
//     file, syntax tree for syntax tree (Same: trivia aside), and specialised for none it is the original file.
// Falls back: a file outside src/XI.QuestContent, a patched file that does not parse or compile, a const, a static
// constructor, a static property initializer, a change to a declaration (a signature, a modifier, an attribute), a
// switch that no node up to the member holds, a switch that changes the reference assembly. The reference assembly
// is checked again at the schema build (golden_mutants.py: the msbuild one against the base build's), and a switch
// reached while the class constructors and the setup run (the mutants mode's STATICINIT line) falls back at run time
internal static class MutantSchema
{
    // the run's switch: AppContext data (the mutants mode's MutantSwitch, whose Equals tells the switch numbers
    // apart), else the XI_MUTANT variable (the checks), else nothing: every switch then takes its old arm
    internal const string SwitchKey = "XI.M";
    internal const string SwitchEnv = "XI_MUTANT";
    internal static string SwitchCondition(int n) =>
        $"global::System.Object.Equals(global::System.AppContext.GetData(\"{SwitchKey}\") ?? " +
        $"global::System.Environment.GetEnvironmentVariable(\"{SwitchEnv}\"), \"{n}\")";

    internal const string QcDir = "src/XI.QuestContent/";

    internal sealed class Input
    {
        public string Id, Path, Old, New;
        public int N;
    }

    internal sealed class Result
    {
        public string Id, Path, Status, Kind, Reason;
        public int N, Climbs;
    }

    internal static readonly CSharpParseOptions Parse = new(LanguageVersion.Latest);

    // ---- the mode -------------------------------------------------------------------------------------------------

    internal static int Mode(string root, string json, string outDir)
    {
        var clock = Stopwatch.StartNew();
        var doc = JsonDocument.Parse(File.ReadAllText(json));
        var inputs = doc.RootElement.GetProperty("mutants").EnumerateArray().Select(e => new Input
        {
            Id = e.GetProperty("id").GetString(), N = e.GetProperty("n").GetInt32(),
            Path = e.GetProperty("path").GetString(), Old = e.GetProperty("old").GetString(),
            New = e.GetProperty("new").GetString(),
        }).ToList();
        if (inputs.Select(i => i.N).Distinct().Count() != inputs.Count || inputs.Any(i => i.N < 1))
        {
            Console.WriteLine("schema: the switch numbers must be distinct and 1 or more");
            return 1;
        }
        var gen = Path.Combine(outDir, "gen");
        if (Directory.Exists(gen)) Directory.Delete(gen, true);
        Directory.CreateDirectory(gen);
        var refs = References();
        var results = Generate(root, inputs, refs, out var files, Console.WriteLine);
        foreach (var (rel, text) in files)
        {
            var path = Path.Combine(gen, rel.Substring(QcDir.Length));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text, new UTF8Encoding(true));
        }
        foreach (var r in results)
            Console.WriteLine(r.Status == "schema"
                ? $"SCHEMA {r.Id} {r.Kind}{(r.Climbs > 0 ? $" (climbed {r.Climbs})" : "")}"
                : r.Status == "bad" ? $"BAD {r.Id} {r.Reason}" : $"FALLBACK {r.Id} {r.Reason}");
        int schema = results.Count(r => r.Status == "schema"), fallback = results.Count(r => r.Status == "fallback"),
            bad = results.Count(r => r.Status == "bad");
        var opts = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
        File.WriteAllText(Path.Combine(outDir, "schema.json"), JsonSerializer.Serialize(new
        {
            mutants = results.Select(r => new
            {
                id = r.Id, n = r.N, path = r.Path, status = r.Status, kind = r.Kind, reason = r.Reason,
                climbs = r.Climbs,
            }),
            files = files.Select(f => new
            {
                path = f.Rel, gen = Path.Combine(gen, f.Rel.Substring(QcDir.Length)).Replace('\\', '/'),
            }),
        }, opts));
        Console.WriteLine($"schema: {schema} of {results.Count} mutants in a schema, {fallback} fall back, " +
                          $"{bad} bad, {files.Count} schema files ({clock.Elapsed.TotalSeconds:F0} s)");
        foreach (var g in results.Where(r => r.Status == "fallback").GroupBy(r => r.Reason)
                     .OrderByDescending(g => g.Count()))
            Console.WriteLine($"schema: fallback {g.Count()}: {g.Key}");
        return 0;
    }

    // the references of the in-memory compilation: the runtime's assemblies and XI.QuestContent's own dependencies
    // (the harness bin's copies of XI.Quests, XI.Common, XI.Persistence and their packages), never XI.QuestContent
    internal static List<MetadataReference> References()
    {
        var bin = Path.GetDirectoryName(typeof(XI.Quests.Authoring.QuestScript).Assembly.Location);
        var own = new[]
        {
            "XI.Quests.dll", "XI.Common.dll", "XI.Persistence.dll", "MySqlConnector.dll",
            "Microsoft.Extensions.Logging.Abstractions.dll",
            "Microsoft.Extensions.DependencyInjection.Abstractions.dll",
        };
        var framework = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
            .Where(p => !string.Equals(Path.GetDirectoryName(p), bin, StringComparison.OrdinalIgnoreCase));
        return framework.Concat(own.Select(f => Path.Combine(bin, f)).Where(File.Exists))
            .GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();
    }

    // ---- the generator --------------------------------------------------------------------------------------------

    private sealed class Site
    {
        public Input M;
        public Result R;
        public string Patched;              // the text-patched file
        public SyntaxTree PatchedTree;
        public List<SyntaxNode> Candidates; // in the original tree, smallest first
        public int Level;                   // the candidate in use
        public string Kind;                 // expression, statement, body, static-variant
        public FieldDeclarationSyntax Field; // static-variant: the field
        public string VariantInit;          // static-variant: the patched initializer's text
        public bool Done => R.Status != null;
    }

    // every mutant's result; files: (path, schema text) of each QuestContent file with a schema mutant
    internal static List<Result> Generate(string root, List<Input> inputs, List<MetadataReference> refs,
                                          out List<(string Rel, string Text)> files, Action<string> log)
    {
        var results = inputs.Select(i => new Result { Id = i.Id, N = i.N, Path = i.Path.Replace('\\', '/') }).ToList();
        files = new List<(string, string)>();
        var byFile = new Dictionary<string, List<Site>>(StringComparer.Ordinal);
        for (int k = 0; k < inputs.Count; k++)
        {
            var r = results[k];
            if (!r.Path.StartsWith(QcDir, StringComparison.Ordinal))
            {
                Fall(r, "outside src/XI.QuestContent");
                continue;
            }
            if (!File.Exists(Path.Combine(root, r.Path))) { (r.Status, r.Reason) = ("bad", "no such file"); continue; }
            if (!byFile.TryGetValue(r.Path, out var list)) byFile[r.Path] = list = new List<Site>();
            list.Add(new Site { M = inputs[k], R = r });
        }
        if (byFile.Count == 0) return results;

        // the original compilation: every QuestContent file (bin and obj aside), parsed as the build parses them
        var qc = Path.Combine(root, QcDir);
        var paths = Directory.GetFiles(qc, "*.cs", SearchOption.AllDirectories)
            .Where(p => !IsBuildOutput(Path.GetRelativePath(qc, p))).OrderBy(p => p, StringComparer.Ordinal).ToList();
        var trees = new Dictionary<string, SyntaxTree>(StringComparer.Ordinal);
        foreach (var p in paths)
        {
            string rel = QcDir + Path.GetRelativePath(qc, p).Replace('\\', '/');
            string text = File.ReadAllText(p);
            if (text.Contains($"\"{SwitchKey}\"") || text.Contains(SwitchEnv))
                throw new InvalidOperationException(
                    $"{rel} names the switch ({SwitchKey}, {SwitchEnv}): the projection could not tell it apart");
            trees[rel] = CSharpSyntaxTree.ParseText(text, Parse, rel);
        }
        var original = CSharpCompilation.Create("XI.QuestContent", trees.Values, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                                         optimizationLevel: OptimizationLevel.Release,
                                         nullableContextOptions: NullableContextOptions.Disable, deterministic: true));
        var current = original;
        var refs0 = new RefCheck(original, log);
        foreach (var (rel, sites) in byFile.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            var tree = trees[rel];
            var errors = Errors(original, tree);
            if (errors.Count > 0)
            {
                foreach (var s in sites)
                    Fall(s.R, "the original file does not compile in memory: " +
                              $"{errors[0].Id} {errors[0].GetMessage()}");
                continue;
            }
            var text = tree.GetText().ToString();
            foreach (var s in sites) Prepare(s, text, tree, original);
            var schema = SchemaFile(rel, tree, sites, original, ref current, refs0, log);
            if (schema != null) files.Add((rel, schema));
        }
        // the whole compilation with every schema file: an error in another file (a partial type's other part) stops
        if (!ReferenceEquals(current, original))
        {
            var all = current.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            if (all.Count > 0)
                throw new InvalidOperationException(
                    $"XI.QuestContent with the schema files does not compile: {all.Count} errors, the first " +
                    $"{all[0].Location.SourceTree?.FilePath}: {all[0].Id} {all[0].GetMessage()}");
        }
        return results;
    }

    private static List<Diagnostic> Errors(CSharpCompilation c, SyntaxTree tree) =>
        c.GetSemanticModel(tree).GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

    private static bool IsBuildOutput(string rel)
    {
        var first = rel.Replace('\\', '/').Split('/')[0];
        return first is "bin" or "obj";
    }

    private static void Fall(Result r, string why) => (r.Status, r.Reason) = ("fallback", why);

    internal const string NoCompile = "the patched file does not compile";

    // the patched text, its tree and the site's candidates (or its status)
    private static void Prepare(Site s, string text, SyntaxTree tree, CSharpCompilation original)
    {
        int count = Count(text, s.M.Old);
        if (count != 1) { (s.R.Status, s.R.Reason) = ("bad", $"{count} matches"); return; }
        int at = text.IndexOf(s.M.Old, StringComparison.Ordinal);
        s.Patched = text.Substring(0, at) + s.M.New + text.Substring(at + s.M.Old.Length);
        s.PatchedTree = CSharpSyntaxTree.ParseText(s.Patched, Parse, s.R.Path);
        if (s.PatchedTree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error))
        {
            Fall(s.R, "the patched file does not parse (option 1's build decides)");
            return;
        }
        // review C1: the patched file compiled in the original's place. A switch turns a constant operand into a
        // value, so a patch the compiler refuses (CS0020 a division by a constant zero, CS0220 a constant overflow,
        // CS0221 a constant cast out of range, ...) would compile as a schema: it falls back, and option 1's build
        // reports BUILD FAILED from the compiler's own diagnostics
        var patchedErrors = Errors(original.ReplaceSyntaxTree(tree, s.PatchedTree), s.PatchedTree);
        if (patchedErrors.Count > 0)
        {
            Fall(s.R, $"{NoCompile} ({patchedErrors[0].Id} {patchedErrors[0].GetMessage()}): option 1's build " +
                      "reports BUILD FAILED");
            return;
        }
        // the change, its common ends trimmed: [a, b) of the original
        int p = 0;
        while (p < s.M.Old.Length && p < s.M.New.Length && s.M.Old[p] == s.M.New[p]) p++;
        int q = 0;
        while (q < s.M.Old.Length - p && q < s.M.New.Length - p && s.M.Old[^(q + 1)] == s.M.New[^(q + 1)]) q++;
        int a = at + p, b = at + s.M.Old.Length - q;
        var span = a < b
            ? TextSpan.FromBounds(a, b)
            : TextSpan.FromBounds(Math.Max(0, a - 1), Math.Min(text.Length, a + 1));
        var rootNode = tree.GetRoot();
        SyntaxNode Deepest(TextSpan at)
        {
            SyntaxNode node = rootNode;
            while (true)
            {
                var child = node.ChildNodes().FirstOrDefault(c => c.FullSpan.Contains(at));
                if (child == null) return node;
                node = child;
            }
        }
        var chain = Deepest(span).AncestorsAndSelf().ToList();
        // a pure insertion also sits in the node that ends where it goes and in the one that starts there (each
        // node's patched text holds it): `.ReplaceDefault()` after `Event(96)` switches that call, not the lambda
        var sides = new List<SyntaxNode>();
        if (a == b && a > 0 && a < text.Length)
            foreach (var side in new[] { TextSpan.FromBounds(a - 1, a), TextSpan.FromBounds(a, a + 1) })
                sides.AddRange(Deepest(side).AncestorsAndSelf().TakeWhile(n => n != chain[0])
                    .Where(n => n.FullSpan.Start <= a && a <= n.FullSpan.End));
        var member = chain.OfType<MemberDeclarationSyntax>().FirstOrDefault();
        if (member == null || member is BaseTypeDeclarationSyntax or NamespaceDeclarationSyntax
                or FileScopedNamespaceDeclarationSyntax or DelegateDeclarationSyntax)
        {
            Fall(s.R, "a change to a type or namespace declaration (it spans members, or is a declaration)");
            return;
        }
        var inMember = chain.TakeWhile(n => n != member).ToList();
        if (member is FieldDeclarationSyntax field)
        {
            if (field.Modifiers.Any(SyntaxKind.ConstKeyword))
            {
                Fall(s.R, "a const (inlined where it is read)");
                return;
            }
            if (field.Modifiers.Any(SyntaxKind.StaticKeyword))
            {
                var init = field.Declaration.Variables.Count == 1 ? field.Declaration.Variables[0].Initializer : null;
                if (init == null || !init.Value.FullSpan.Contains(span))
                {
                    Fall(s.R, "a change to a static field's declaration");
                    return;
                }
                s.Kind = "static-variant";
                s.Field = field;
                int start = init.Value.FullSpan.Start, end = init.Value.FullSpan.End + s.M.New.Length - s.M.Old.Length;
                s.VariantInit = s.Patched.Substring(start, end - start);
                return;
            }
        }
        if (member is ConstructorDeclarationSyntax ctor && ctor.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            Fall(s.R, "a static constructor (it runs once per process)");
            return;
        }
        if (member is PropertyDeclarationSyntax prop && prop.Modifiers.Any(SyntaxKind.StaticKeyword) &&
            prop.Initializer != null && prop.Initializer.FullSpan.Contains(span))
        {
            Fall(s.R, "a static property's initializer (it runs once per process)");
            return;
        }
        s.Candidates = sides.Concat(inMember).Distinct().Where(Eligible).OrderBy(n => n.FullSpan.Length).ToList();
        if (s.Candidates.Count == 0) Fall(s.R, "a change to a declaration (no expression, statement or body holds it)");
    }

    private static int Count(string text, string old)
    {
        if (old.Length == 0) return text.Length + 1;
        int n = 0;
        for (int i = text.IndexOf(old, StringComparison.Ordinal); i >= 0;
             i = text.IndexOf(old, i + old.Length, StringComparison.Ordinal))
            n++;
        return n;
    }

    // a node a switch can stand in for: an expression (not an initializer, not a type, not a declaration's name), a
    // statement of a statement list, a member's or a function's body
    private static bool Eligible(SyntaxNode n)
    {
        if (n is ExpressionSyntax e)
        {
            if (e is InitializerExpressionSyntax || e is DeclarationExpressionSyntax || e is RefExpressionSyntax)
                return false;
            if (SyntaxFacts.IsInTypeOnlyContext(e)) return false;
            if (e.Parent is MemberAccessExpressionSyntax ma && ma.Name == e) return false;
            if (e.Parent is QualifiedNameSyntax or NameColonSyntax or NameEqualsSyntax or AliasQualifiedNameSyntax)
                return false;
            if (e.Parent is AssignmentExpressionSyntax asg && asg.Left == e &&
                asg.Parent is InitializerExpressionSyntax)
                return false;
            return true;
        }
        if (n is BlockSyntax b && b.Parent is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax
                or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax)
            return true;
        return n is StatementSyntax && n.Parent is BlockSyntax or SwitchSectionSyntax;
    }

    // the schema file of one source file, its sites decided (null: none is a schema); the compilation keeps it
    private static string SchemaFile(string rel, SyntaxTree tree, List<Site> sites, CSharpCompilation original,
                                     ref CSharpCompilation current, RefCheck refCheck, Action<string> log)
    {
        var model0 = original.GetSemanticModel(tree);
        foreach (var s in sites.Where(s => !s.Done && s.Kind == "static-variant")) CheckVariant(s, model0);
        for (int round = 0; round < 200; round++)
        {
            var live = sites.Where(s => !s.Done).ToList();
            if (live.Count == 0) return null;
            // each candidate alone must fit its slot and its new text parse as its kind
            bool moved = false;
            foreach (var s in live.Where(s => s.Kind != "static-variant"))
                while (!s.Done && !FitsAlone(s)) moved |= Climb(s, "no node up to the member holds both versions");
            if (moved) continue;
            live = sites.Where(s => !s.Done).ToList();
            if (live.Count == 0) return null;
            var built = Build(tree, live, out var spans);
            string text = built.ToFullString();
            var reparsed = CSharpSyntaxTree.ParseText(text, Parse, rel);
            // the printed text is what compiles and what the proof reads: it must parse as the tree built
            if (reparsed.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error) ||
                !Same(reparsed.GetRoot(), built))
            {
                var diag = reparsed.GetDiagnostics().FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error);
                log($"schema: {rel} does not print as built: {diag}");
                if (Environment.GetEnvironmentVariable("XI_SCHEMA_DUMP") is { } dump)
                    File.WriteAllText(Path.Combine(dump, Path.GetFileName(rel) + ".bad"), text);
                foreach (var s in live) Fall(s.R, "the schema file does not parse as printed (a generator bug)");
                return null;
            }
            // the projection proof: none is the original, each mutant its patched file
            if (!Same(Project(reparsed.GetRoot(), 0), tree.GetRoot()))
            {
                foreach (var s in live)
                    Fall(s.R, "the schema specialised for none is not the original file (a generator bug)");
                return null;
            }
            foreach (var s in live)
            {
                var projected = Project(reparsed.GetRoot(), s.M.N);
                if (Same(projected, s.PatchedTree.GetRoot())) continue;
                if (Environment.GetEnvironmentVariable("XI_SCHEMA_DUMP") != null)
                    log($"schema: {s.M.Id} level {s.Level} ({s.Kind}): " +
                        FirstDifference(projected, s.PatchedTree.GetRoot()));
                moved |= Climb(s, "the projection proof fails at every node up to the member");
            }
            if (moved) continue;
            // the compilation with the schema file: an error climbs the switch it sits in
            var trial = current.ReplaceSyntaxTree(current.SyntaxTrees.First(t => t.FilePath == rel), reparsed);
            var model = trial.GetSemanticModel(reparsed);
            var errors = model.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            // each blamed site climbs once a round (one bad switch can raise several errors)
            var blamed = new Dictionary<Site, Diagnostic>();
            foreach (var d in errors)
                foreach (var s in Blame(d.Location.SourceSpan, live, spans, built))
                    blamed.TryAdd(s, d);
            foreach (var (s, d) in blamed)
                moved |= Climb(s, $"no node up to the member compiles ({d.Id} {d.GetMessage()})");
            if (moved || errors.Count > 0)
            {
                if (!moved)
                    foreach (var s in live)
                        Fall(s.R, $"the schema file does not compile ({errors[0].Id} {errors[0].GetMessage()})");
                continue;
            }
            // the types: each expression switch's arms of its own type, the original node's converted type
            foreach (var s in live.Where(s => s.Kind == "expression"))
            {
                var why = TypeProblem(s, spans[s], model, model0, reparsed);
                if (why != null) moved |= Climb(s, $"no node up to the member keeps the types ({why})");
            }
            if (moved) continue;
            // the reference assembly: the switches that change it fall back (a bigger node holds the same new arm)
            if (!refCheck.Keeps(trial))
            {
                var culprits = refCheck.Culprits(tree, rel, live, current);
                foreach (var s in culprits)
                    Fall(s.R, "the switch changes the reference assembly (a lambda, a closure or constant data in " +
                              "its new arm: compiler members it shows)");
                if (culprits.Count == 0)
                    foreach (var s in live)
                        Fall(s.R, "the schema file changes the reference assembly, no switch alone does");
                continue;
            }
            current = trial;
            foreach (var s in live)
            {
                s.R.Status = "schema";
                s.R.Kind = s.Kind;
                s.R.Climbs = s.Level;
            }
            return text;
        }
        foreach (var s in sites.Where(s => !s.Done)) Fall(s.R, "the generator did not settle");
        return null;
    }

    // whether two trees are the same syntax, trivia aside: the same node kinds with the same children (lists as the
    // red tree flattens them: the parser keeps a one-statement list as a list node, a rewriter as the statement, and
    // SyntaxFactory.AreEquivalent tells those apart) and the same tokens (kind, missing, text and value)
    internal static bool Same(SyntaxNode a, SyntaxNode b) => FirstDifference(a, b) == null;

    // the first node or token where two trees part (null: none)
    internal static string FirstDifference(SyntaxNode a, SyntaxNode b)
    {
        var stack = new Stack<(SyntaxNodeOrToken, SyntaxNodeOrToken)>();
        stack.Push((a, b));
        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            if (x.RawKind != y.RawKind)
                return $"at {x.SpanStart}: {x.Kind()} [{Short(x.ToString())}] / {y.Kind()} [{Short(y.ToString())}]";
            if (x.IsToken)
            {
                if (!SyntaxFactory.AreEquivalent(x.AsToken(), y.AsToken()))
                    return $"at {x.SpanStart}: token [{Short(x.ToString())}] / [{Short(y.ToString())}]";
                continue;
            }
            var xs = x.ChildNodesAndTokens();
            var ys = y.ChildNodesAndTokens();
            if (xs.Count != ys.Count)
                return $"at {x.SpanStart}: {x.Kind()} has {xs.Count} / {ys.Count} children [{Short(x.ToString())}]";
            for (int i = xs.Count - 1; i >= 0; i--) stack.Push((xs[i], ys[i]));
        }
        return null;
    }

    private static string Short(string s) => s.Length > 60 ? s.Substring(0, 60) + "..." : s;

    // the next candidate, or the fallback; true when something moved
    private static bool Climb(Site s, string why)
    {
        if (s.Kind == "static-variant")
        {
            Fall(s.R, why.Replace("no node up to the member", "the static variant"));
            return true;
        }
        if (Environment.GetEnvironmentVariable("XI_SCHEMA_DUMP") != null)
            Console.WriteLine($"schema: {s.M.Id} climbs from {s.Level} ({s.Candidates[s.Level].Kind()}): {why}");
        s.Level++;
        if (s.Level >= s.Candidates.Count) Fall(s.R, why);
        return true;
    }

    // the candidate's slot takes the switch and its patched text parses as its kind
    private static bool FitsAlone(Site s)
    {
        var node = s.Candidates[s.Level];
        try
        {
            var arm = Arm(s, node);
            if (arm == null) return false;
            // review M1: a statement switch puts its old and its new statements each in a block of their own, so a
            // `using` declaration among them would be disposed at the end of that block, not of the one it was in
            // (the projection cannot see that): the switch climbs to the block around
            if (s.Kind == "statement" &&
                (UsingDeclaration(node) || ((BlockSyntax)arm).Statements.Any(UsingDeclaration)))
                return false;
            var wrapped = Wrap(s, node, node);
            _ = node.SyntaxTree.GetRoot().ReplaceNode(node, wrapped); // throws when the slot takes no such node
            return true;
        }
        catch (InvalidCastException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool UsingDeclaration(SyntaxNode n) =>
        n is LocalDeclarationStatementSyntax l && l.UsingKeyword.IsKind(SyntaxKind.UsingKeyword);

    // the patched text of the node's place, parsed as the node's kind (null: it does not parse so)
    private static SyntaxNode Arm(Site s, SyntaxNode node)
    {
        int delta = s.M.New.Length - s.M.Old.Length;
        string r = s.Patched.Substring(node.FullSpan.Start, node.FullSpan.Length + delta);
        if (node is ExpressionSyntax)
        {
            var e = SyntaxFactory.ParseExpression(r, 0, Parse, true);
            s.Kind = "expression";
            return e.ContainsDiagnostics || e.FullSpan.Length != r.Length ? null : e;
        }
        if (node is BlockSyntax && node.Parent is not (BlockSyntax or SwitchSectionSyntax))
        {
            var b = SyntaxFactory.ParseStatement(r, 0, Parse, true);
            s.Kind = "body";
            return b is BlockSyntax && !b.ContainsDiagnostics && b.FullSpan.Length == r.Length ? b : null;
        }
        string wrapped = "{" + r + "\n}";
        var block = SyntaxFactory.ParseStatement(wrapped, 0, Parse, true);
        s.Kind = "statement";
        return block is BlockSyntax && !block.ContainsDiagnostics && block.FullSpan.Length == wrapped.Length
            ? block
            : null;
    }

    private static ExpressionSyntax Cond(int n) => SyntaxFactory.ParseExpression(SwitchCondition(n), 0, Parse, true);

    // the switch standing in for node (rewritten: node with its inner switches)
    private static SyntaxNode Wrap(Site s, SyntaxNode node, SyntaxNode rewritten)
    {
        var arm = Arm(s, node);
        int n = s.M.N;
        switch (s.Kind)
        {
            case "expression":
            {
                var old = (ExpressionSyntax)rewritten;
                var cond = SyntaxFactory.ConditionalExpression(Cond(n),
                    SyntaxFactory.ParenthesizedExpression(((ExpressionSyntax)arm).WithoutTrivia())
                        .WithAdditionalAnnotations(new SyntaxAnnotation("new", n.ToString())),
                    SyntaxFactory.ParenthesizedExpression(old.WithoutTrivia())
                        .WithAdditionalAnnotations(new SyntaxAnnotation("old", n.ToString())));
                return SyntaxFactory.ParenthesizedExpression(cond).WithLeadingTrivia(old.GetLeadingTrivia())
                    .WithTrailingTrivia(old.GetTrailingTrivia())
                    .WithAdditionalAnnotations(new SyntaxAnnotation("sw", n.ToString()));
            }
            case "statement":
            {
                var old = (StatementSyntax)rewritten;
                return Marker(n, ((BlockSyntax)arm).Statements,
                              SyntaxFactory.SingletonList(old.WithoutLeadingTrivia().WithoutTrailingTrivia()))
                    .WithLeadingTrivia(old.GetLeadingTrivia()).WithTrailingTrivia(old.GetTrailingTrivia());
            }
            default:
            {
                var old = (BlockSyntax)rewritten;
                var inner = Marker(n, ((BlockSyntax)arm).Statements, old.Statements);
                return old.WithStatements(SyntaxFactory.SingletonList<StatementSyntax>(inner));
            }
        }
    }

    // if (C(n)) { new } else { old }: the statement switch
    private static IfStatementSyntax Marker(int n, SyntaxList<StatementSyntax> @new, SyntaxList<StatementSyntax> old) =>
        SyntaxFactory.IfStatement(Cond(n),
                SyntaxFactory.Block(@new).WithAdditionalAnnotations(new SyntaxAnnotation("new", n.ToString())),
                SyntaxFactory.ElseClause(SyntaxFactory.Block(old)
                    .WithAdditionalAnnotations(new SyntaxAnnotation("old", n.ToString()))))
            .WithAdditionalAnnotations(new SyntaxAnnotation("sw", n.ToString()));

    // the schema tree: every switch (inner ones first: ReplaceNodes hands each node rewritten), then the static
    // variants; spans: each site's switch in the printed text
    private static SyntaxNode Build(SyntaxTree tree, List<Site> live, out Dictionary<Site, TextSpan> spans)
    {
        var root = tree.GetRoot();
        var switched = live.Where(s => s.Kind != "static-variant").ToList();
        var byNode = switched.GroupBy(s => s.Candidates[s.Level])
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.M.N).ToList());
        root = root.ReplaceNodes(byNode.Keys, (orig, rewritten) =>
        {
            var node = rewritten;
            foreach (var s in byNode[orig]) node = Wrap(s, orig, node);
            return node;
        });
        // the static variants, a field at a time (each field's variants in switch order): __s_F in F's place, the new
        // members at the end of the type, so every member before keeps its ordinal (a lambda's compiler names carry
        // its member's ordinal, and those are in the reference assembly)
        foreach (var g in live.Where(s => s.Kind == "static-variant").GroupBy(s => s.Field.Span.Start)
                     .OrderByDescending(g => g.Key))
        {
            var first = g.First().Field;
            var field = root.DescendantNodes().OfType<FieldDeclarationSyntax>().First(f => Same(f, first)
                && f.Declaration.Variables[0].Identifier.Text == first.Declaration.Variables[0].Identifier.Text);
            var members = StaticVariant(field, g.OrderBy(s => s.M.N).ToList()).ToList();
            var type = (TypeDeclarationSyntax)field.Parent;
            root = root.ReplaceNode(type, type.ReplaceNode(field, members[0]).AddMembers(members.Skip(1).ToArray()));
        }
        spans = new Dictionary<Site, TextSpan>();
        foreach (var s in live)
        {
            string n = s.M.N.ToString();
            var mark = root.GetAnnotatedNodes("sw").FirstOrDefault(x => x.GetAnnotations("sw").Any(a => a.Data == n));
            mark ??= root.GetAnnotatedNodes("var")
                .FirstOrDefault(x => x.GetAnnotations("var").Any(a => a.Data.Split(',').Contains(n)));
            spans[s] = mark?.Span ?? default;
        }
        return root;
    }

    // F's members: __s_F (the field as it was); per mutant a private method __i<n>_F (its patched initializer) and a
    // private field __c<n>_F that keeps the first value made (Interlocked: one value, as the class constructor makes
    // one; the mutants mode clears it before each block and makes it at the block's start, under the switch, so a
    // variant that throws sends its mutant to option 1, where it poisons its type as a throwing class constructor
    // does); F the property. No lambda: a lambda's compiler members (<>c's cache field, a display class) are in the
    // reference assembly
    private static IEnumerable<MemberDeclarationSyntax> StaticVariant(FieldDeclarationSyntax field, List<Site> sites)
    {
        var v = field.Declaration.Variables[0];
        string name = v.Identifier.Text, type = field.Declaration.Type.ToString();
        var keep = field.WithDeclaration(field.Declaration.WithVariables(SyntaxFactory.SingletonSeparatedList(
            v.WithIdentifier(SyntaxFactory.Identifier(v.Identifier.LeadingTrivia, $"__s_{name}",
                                                      v.Identifier.TrailingTrivia)))));
        var members = new List<MemberDeclarationSyntax> { keep };
        string tag = string.Join(",", sites.Select(s => s.M.N));
        foreach (var s in sites)
        {
            members.Add(SyntaxFactory.ParseMemberDeclaration(VariantCache(type, s.M.N, name) + "\n", 0, Parse, true));
            members.Add(SyntaxFactory.ParseMemberDeclaration(
                $"{VariantMaker(type, s.M.N, name)}\n{s.VariantInit}\n;\n", 0, Parse, true));
        }
        var mods = string.Join(" ", field.Modifiers.Where(m => !m.IsKind(SyntaxKind.ReadOnlyKeyword))
            .Select(m => m.Text));
        string pick = string.Concat(sites.Select(s => $"{SwitchCondition(s.M.N)} ? {VariantPick(s.M.N, name)} : ")) +
                      $"__s_{name}";
        members.Add(SyntaxFactory.ParseMemberDeclaration($"{mods} {type} {name} => {pick};\n", 0, Parse, true));
        return members.Select(m => m.WithAdditionalAnnotations(new SyntaxAnnotation("var", tag)));
    }

    // the variants' member names, which the mutants mode finds by reflection (SchemaVariants)
    internal const string VariantCachePrefix = "__c", VariantMakerPrefix = "__i";

    private static string VariantCache(string type, int n, string name) =>
        $"private static {type} {VariantCachePrefix}{n}_{name};";
    private static string VariantMaker(string type, int n, string name) =>
        $"private static {type} {VariantMakerPrefix}{n}_{name}() =>";
    private static string VariantPick(int n, string name) =>
        $"(__c{n}_{name} ?? (global::System.Threading.Interlocked.CompareExchange(ref __c{n}_{name}, " +
        $"__i{n}_{name}(), null) ?? __c{n}_{name}))";

    // the reference assembly check: the original compilation's reference assembly (a full emit with the refout, as
    // the build makes it: a metadata-only emit compiles no body, so it would miss a lambda's members) against a
    // trial's
    private sealed class RefCheck
    {
        private readonly CSharpCompilation original_;
        private readonly Action<string> log_;
        private byte[] want_;

        public RefCheck(CSharpCompilation original, Action<string> log) => (original_, log_) = (original, log);

        internal static byte[] Image(CSharpCompilation c)
        {
            using var pe = new MemoryStream();
            using var meta = new MemoryStream();
            var r = c.Emit(pe, metadataPEStream: meta, options: new EmitOptions(includePrivateMembers: false));
            return r.Success ? meta.ToArray() : null;
        }

        private byte[] Want()
        {
            if (want_ != null) return want_;
            var clock = Stopwatch.StartNew();
            want_ = Image(original_) ??
                    throw new InvalidOperationException("the original XI.QuestContent does not emit in memory");
            log_($"schema: the original reference assembly emitted in memory ({clock.Elapsed.TotalSeconds:F1} s)");
            return want_;
        }

        public bool Keeps(CSharpCompilation trial)
        {
            var img = Image(trial);
            return img != null && img.AsSpan().SequenceEqual(Want());
        }

        // the sites whose switches change the reference assembly: the file built with subsets of them (suspects
        // first: a new arm with a lambda, a local function, a query, an anonymous type, an array or collection of
        // constants or a u8 string), halved down to each culprit; empty when only several together change it
        public List<Site> Culprits(SyntaxTree tree, string rel, List<Site> live, CSharpCompilation current)
        {
            var clock = Stopwatch.StartNew();
            int emits = 0;
            bool Clean(List<Site> subset)
            {
                if (subset.Count == 0) return true;
                emits++;
                var built = Build(tree, subset, out _);
                var re = CSharpSyntaxTree.ParseText(built.ToFullString(), Parse, rel);
                return Keeps(current.ReplaceSyntaxTree(current.SyntaxTrees.First(t => t.FilePath == rel), re));
            }
            var found = new List<Site>();
            bool all = false;
            void Find(List<Site> set) // set: known to change it
            {
                if (set.Count == 1) { found.Add(set[0]); return; }
                var a = set.Take(set.Count / 2).ToList();
                var b = set.Skip(set.Count / 2).ToList();
                bool da = !Clean(a), db = !Clean(b);
                if (!da && !db) { all = true; return; }
                if (da) Find(a);
                if (db) Find(b);
            }
            var suspects = live.Where(Suspect).ToList();
            var others = live.Except(suspects).ToList();
            if (suspects.Count > 0 && Clean(others)) Find(suspects);
            else Find(live);
            log_($"schema: {rel}: {found.Count} of {live.Count} switches change the reference assembly" +
                 $"{(all ? ", and some only together" : "")} ({emits} emits, {clock.Elapsed.TotalSeconds:F0} s)");
            return all ? new List<Site>() : found;
        }

        private static bool Suspect(Site s)
        {
            var text = s.Kind == "static-variant" ? s.VariantInit : Arm(s, s.Candidates[s.Level])?.ToFullString();
            if (text == null) return true;
            var e = SyntaxFactory.ParseExpression("(" + text + ")", 0, Parse, true);
            var nodes = (s.Kind == "expression" || s.Kind == "static-variant"
                    ? (SyntaxNode)e
                    : SyntaxFactory.ParseStatement("{" + text + "\n}", 0, Parse, true))
                .DescendantNodesAndSelf();
            return nodes.Any(n => n is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax
                                      or QueryExpressionSyntax or AnonymousObjectCreationExpressionSyntax
                                      or InitializerExpressionSyntax or CollectionExpressionSyntax
                                  || n.IsKind(SyntaxKind.Utf8StringLiteralExpression));
        }
    }

    // a static field a variant can stand in for: private, readonly, one declarator, of a reference type, whose
    // initializer reads no static field of its type declared after it (a field initializer sees those unset)
    private static void CheckVariant(Site s, SemanticModel model)
    {
        var f = s.Field;
        var sym = model.GetDeclaredSymbol(f.Declaration.Variables[0]) as IFieldSymbol;
        string why = null;
        if (sym == null) why = "no symbol";
        else if (!f.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)) why = "a mutable static field";
        else if (sym.DeclaredAccessibility != Accessibility.Private)
            why = "a non-private static field (its type would change)";
        else if (!sym.Type.IsReferenceType) why = "a static field of a value type";
        else if (sym.ContainingType.IsGenericType) why = "a static field of a generic type";
        else
        {
            var ids = f.Declaration.Variables[0].Initializer.Value.DescendantNodesAndSelf()
                .OfType<IdentifierNameSyntax>();
            foreach (var id in ids)
                if (model.GetSymbolInfo(id).Symbol is IFieldSymbol other && other.IsStatic && !other.IsConst &&
                    SymbolEqualityComparer.Default.Equals(other.ContainingType, sym.ContainingType) &&
                    (SymbolEqualityComparer.Default.Equals(other, sym) ||
                     other.Locations.FirstOrDefault()?.SourceSpan.Start > f.SpanStart))
                {
                    why = $"its initializer reads the static field {other.Name}, declared after it";
                    break;
                }
        }
        if (why != null) Fall(s.R, "the static variant: " + why);
    }

    // the sites an error at span blames: the innermost switch holding it (its new arm, or its old arm outside any
    // inner new arm), else every site of the member (and the variants of the type) around it
    private static IEnumerable<Site> Blame(TextSpan span, List<Site> live, Dictionary<Site, TextSpan> spans,
                                           SyntaxNode built)
    {
        var holding = live.Where(s => spans[s].Length > 0 && spans[s].Contains(span)).OrderBy(s => spans[s].Length)
            .ToList();
        if (holding.Count > 0) return new[] { holding[0] };
        var at = built.FindToken(Math.Min(span.Start, built.FullSpan.End - 1)).Parent;
        var member = at?.AncestorsAndSelf().OfType<MemberDeclarationSyntax>()
            .FirstOrDefault(m => m is not BaseTypeDeclarationSyntax);
        var type = at?.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        var near = live.Where(s => spans[s].Length > 0 &&
                                   ((member != null && member.Span.Contains(spans[s])) ||
                                    (s.Kind == "static-variant" && type != null && type.Span.Contains(spans[s]))))
            .ToList();
        return near.Count > 0 ? near : live;
    }

    // why an expression switch changes a type (null: it does not): the conditional, both arms and the original node
    // of the same type, the conditional converted as the original node is
    private static string TypeProblem(Site s, TextSpan span, SemanticModel model, SemanticModel model0,
                                      SyntaxTree reparsed)
    {
        var outer = reparsed.GetRoot().DescendantNodes(span).OfType<ParenthesizedExpressionSyntax>()
            .FirstOrDefault(pe => pe.Span == span && pe.Expression is ConditionalExpressionSyntax ce0 &&
                                  IsSwitch(ce0.Condition, out int n0) && n0 == s.M.N);
        if (outer == null) return "the switch is not found";
        var ce = (ConditionalExpressionSyntax)outer.Expression;
        var tCond = model.GetTypeInfo(ce);
        var tOuter = model.GetTypeInfo(outer);
        var tNew = model.GetTypeInfo(((ParenthesizedExpressionSyntax)ce.WhenTrue).Expression);
        var tOld = model.GetTypeInfo(((ParenthesizedExpressionSyntax)ce.WhenFalse).Expression);
        var tOrig = model0.GetTypeInfo(s.Candidates[s.Level]);
        string Name(ITypeSymbol t) => t?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (tCond.Type == null || tNew.Type == null || tOld.Type == null || tOrig.Type == null)
            return "an arm with no type of its own";
        if (Name(tNew.Type) != Name(tCond.Type) || Name(tOld.Type) != Name(tCond.Type))
            return $"arms {Name(tOld.Type)} / {Name(tNew.Type)}";
        if (Name(tOrig.Type) != Name(tCond.Type)) return $"{Name(tOrig.Type)} becomes {Name(tCond.Type)}";
        if (Name(tOrig.ConvertedType) != Name(tOuter.ConvertedType))
            return $"converted to {Name(tOrig.ConvertedType)}, now {Name(tOuter.ConvertedType)}";
        return null;
    }

    // ---- the projection -------------------------------------------------------------------------------------------

    // whether e is the switch C(n) (exactly as SwitchCondition writes it)
    internal static bool IsSwitch(ExpressionSyntax e, out int n)
    {
        n = 0;
        if (e is not InvocationExpressionSyntax inv || inv.ArgumentList.Arguments.Count != 2) return false;
        if (inv.ArgumentList.Arguments[1].Expression is not LiteralExpressionSyntax lit ||
            !lit.IsKind(SyntaxKind.StringLiteralExpression))
            return false;
        if (!int.TryParse(lit.Token.ValueText, System.Globalization.NumberStyles.None,
                          System.Globalization.CultureInfo.InvariantCulture, out n))
            return false;
        return n > 0 && Same(e, Cond(n));
    }

    // the schema specialised for mutant n (0: none): each switch resolved, each static variant folded back to its field
    internal static SyntaxNode Project(SyntaxNode root, int n) => new Projector(n).Visit(root);

    private sealed class Projector : CSharpSyntaxRewriter
    {
        private readonly int n_;
        public Projector(int n) => n_ = n;

        public override SyntaxNode VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
        {
            if (node.Expression is ConditionalExpressionSyntax ce && IsSwitch(ce.Condition, out int k) &&
                ce.WhenTrue is ParenthesizedExpressionSyntax t && ce.WhenFalse is ParenthesizedExpressionSyntax f)
                return Visit(k == n_ ? t.Expression : f.Expression);
            return base.VisitParenthesizedExpression(node);
        }

        // an if (C(k)) { new } else { old } of a statement list: the chosen block's statements in its place
        private SyntaxList<StatementSyntax> Splice(SyntaxList<StatementSyntax> list)
        {
            var outList = new List<StatementSyntax>();
            foreach (var st in list)
            {
                if (st is IfStatementSyntax ifs && IsSwitch(ifs.Condition, out int k) &&
                    ifs.Statement is BlockSyntax yes && ifs.Else?.Statement is BlockSyntax no)
                    outList.AddRange(Splice((k == n_ ? yes : no).Statements));
                else outList.Add((StatementSyntax)Visit(st));
            }
            return SyntaxFactory.List(outList);
        }

        public override SyntaxNode VisitBlock(BlockSyntax node) => node.WithStatements(Splice(node.Statements));

        public override SyntaxNode VisitSwitchSection(SwitchSectionSyntax node) =>
            ((SwitchSectionSyntax)base.VisitSwitchSection(node.WithStatements(default)))
            .WithStatements(Splice(node.Statements));

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node) =>
            base.VisitClassDeclaration(node.WithMembers(Fold(node.Members)));

        public override SyntaxNode VisitStructDeclaration(StructDeclarationSyntax node) =>
            base.VisitStructDeclaration(node.WithMembers(Fold(node.Members)));

        public override SyntaxNode VisitRecordDeclaration(RecordDeclarationSyntax node) =>
            base.VisitRecordDeclaration(node.WithMembers(Fold(node.Members)));

        private static T Named<T>(List<MemberDeclarationSyntax> list, string name) where T : MemberDeclarationSyntax =>
            list.OfType<T>().FirstOrDefault(m => m switch
            {
                FieldDeclarationSyntax f => f.Declaration.Variables.Count == 1 &&
                                            f.Declaration.Variables[0].Identifier.Text == name,
                MethodDeclarationSyntax md => md.Identifier.Text == name,
                _ => false,
            });

        // the static variants of a member list folded back: __s_F as F, with the chosen variant's initializer
        private SyntaxList<MemberDeclarationSyntax> Fold(SyntaxList<MemberDeclarationSyntax> members)
        {
            var list = members.ToList();
            foreach (var prop in list.OfType<PropertyDeclarationSyntax>().ToList())
            {
                if (prop.ExpressionBody == null || prop.AccessorList != null) continue;
                string name = prop.Identifier.Text;
                var picks = new List<int>();
                var e = prop.ExpressionBody.Expression;
                bool shape = true;
                while (e is ConditionalExpressionSyntax c)
                {
                    if (!IsSwitch(c.Condition, out int k) || c.WhenTrue.ToString() != VariantPick(k, name))
                    {
                        shape = false;
                        break;
                    }
                    picks.Add(k);
                    e = c.WhenFalse;
                }
                if (!shape || picks.Count == 0 || e.ToString() != $"__s_{name}") continue;
                var keep = Named<FieldDeclarationSyntax>(list, $"__s_{name}");
                var caches = picks.Select(k => Named<FieldDeclarationSyntax>(list, $"__c{k}_{name}")).ToList();
                var makers = picks.Select(k => Named<MethodDeclarationSyntax>(list, $"__i{k}_{name}")).ToList();
                if (keep == null || caches.Any(l => l == null) || makers.Any(m => m == null)) continue;
                string type = keep.Declaration.Type.ToString();
                var inits = new Dictionary<int, ExpressionSyntax>();
                bool ok = true;
                for (int i = 0; i < picks.Count; i++)
                {
                    var body = makers[i].ExpressionBody?.Expression;
                    if (body == null) { ok = false; break; }
                    var wantCache =
                        SyntaxFactory.ParseMemberDeclaration(VariantCache(type, picks[i], name), 0, Parse, true);
                    var wantMaker = SyntaxFactory.ParseMemberDeclaration(
                        $"{VariantMaker(type, picks[i], name)} {body.ToFullString()};", 0, Parse, true);
                    if (!Same(wantCache, caches[i]) || !Same(wantMaker, makers[i])) { ok = false; break; }
                    inits[picks[i]] = body;
                }
                var mods = string.Join(" ", keep.Modifiers.Where(m => !m.IsKind(SyntaxKind.ReadOnlyKeyword))
                    .Select(m => m.Text));
                if (!ok || prop.Modifiers.ToString() != mods || prop.Type.ToString() != type ||
                    prop.AttributeLists.Count > 0)
                    continue;
                var v = keep.Declaration.Variables[0];
                var chosen = inits.TryGetValue(n_, out var mine) ? mine : v.Initializer.Value;
                var field = keep.WithDeclaration(keep.Declaration.WithVariables(SyntaxFactory.SingletonSeparatedList(
                    v.WithIdentifier(SyntaxFactory.Identifier(name))
                        .WithInitializer(v.Initializer.WithValue(chosen)))));
                int at = list.IndexOf(keep);
                list[at] = field;
                list.Remove(prop);
                foreach (var l in caches) list.Remove(l);
                foreach (var m in makers) list.Remove(m);
            }
            return SyntaxFactory.List(list);
        }
    }
}
