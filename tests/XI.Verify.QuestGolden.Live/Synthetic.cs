using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XI.Quests;
using XI.Quests.Authoring;

// startup --synthetic <N> (quest rework B3b start, spec section 2.3): N quests shaped like a typical port, registered
// through ZoneHost.TestQuestScripts so the registration line shows what N more C# quests cost. Each is its own class,
// compiled here with Roslyn after the baseline runs, so its Build is JIT-compiled on the first run that registers it, as
// a real quest's is on a cold start (one shared class would pay that once). Quest i:
//   - its key: a real xi.quest.id constant (the bridge takes a quest's key from its log and constant, and refuses one the
//     quest key registry does not resolve), no real C# quest's. Even i: a constant whose Lua quest file is in the list,
//     the quest's LegacyPath that file, and the file hidden (ScriptEngine.HiddenContainerFiles), as a batch deletes it:
//     the quest takes the file's slot and the file's load is gone, a real port's net cost. Once those run out (about 563),
//     a constant with no Lua file and a LegacyPath beside a real file's. Odd i: preferably a constant whose Lua file is
//     there (a call-only quest has no container, the file is left alone)
//   - 6 states (Available, Accepted at steps 1-4, Completed) and 11 handlers over 3 NPCs: two from the Npc constants of a
//     few cities, cycling, and one from the zones the Lua quests name (scripts/quests), cycling, so the registration's
//     per-zone entity reads are paid for as many zones as a full port touches; each NPC checked against its zone's data
//     the way the registration checks it, before use
//   - even i: a container quest in place of its Lua file (see the key); odd i: call-only (every NPC handler Called(), the states told apart by a char var, an NPC-call quest matches no step)
internal static partial class Program
{
    // the synthetic quests every new world registers (null: none)
    private static List<QuestScript> synthetic_;

    // the cities the first two NPCs of each quest cycle over
    private static readonly string[] SyntheticCities =
        { "BastokMarkets", "PortBastok", "SouthernSanDoria", "WindurstWaters", "LowerJeuno", "AhtUrhganWhitegate" };

    // what SyntheticQuests built, for the log
    private sealed class SyntheticSet
    {
        public List<QuestScript> Quests;
        public int Containers, CallOnly, Zones, OnLuaFile;
        // the container quests' Lua files, hidden in every synthetic world
        public List<string> Hidden = new List<string>();
    }

    // N synthetic quests, built from the world of the last baseline run (its Lua, its registry, its container list)
    private static SyntheticSet SyntheticQuests(int n)
    {
        // the quest constants, in (log, id) order, that resolve in the registry and are no real C# quest's
        var real = new HashSet<string>(CSharpQuestKeys(), StringComparer.Ordinal);
        var constants = new List<(int Log, string Area, string Const, int Id)>();
        foreach (var line in LuaReports(@"
            for log = 0, 10 do
                local area = xi.quest.area[log]
                local ids = area and xi.quest.id[area]
                if ids then
                    local k, v = next(ids)
                    while k ~= nil do
                        if type(v) == 'number' then report(log .. ' ' .. area .. ' ' .. k .. ' ' .. v) end
                        k, v = next(ids, k)
                    end
                end
            end"))
        {
            var p = line.Split(' ');
            int log = int.Parse(p[0]), id = int.Parse(p[3]);
            string key = QuestKeyRegistry.KeyFor(p[1], p[2]);
            if (real.Contains(key) || !loader_.Progress.Registry.TryGetRef(key, out var wire) ||
                !wire.Equals(new QuestWireRef((QuestLog)log, (ushort)id))) continue;
            constants.Add((log, p[1], p[2], id));
        }
        constants = constants.OrderBy(c => c.Log).ThenBy(c => c.Id).ToList();
        var inList = new HashSet<string>(zone.ContainerOrder(lua), StringComparer.Ordinal);
        // container id -> the require name of the quest file the list loaded it from (package.loaded holds each one)
        var fileOf = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in LuaReports(@"
            for name, c in pairs(package.loaded) do
                if type(name) == 'string' and name:sub(1, 15) == 'scripts/quests/' and type(c) == 'table' and type(c.id) == 'string' then
                    report(c.id .. ' ' .. name)
                end
            end").OrderBy(x => x, StringComparer.Ordinal))
        {
            int sp = line.IndexOf(' ');
            fileOf.TryAdd(line[..sp], line[(sp + 1)..]);
        }
        string FileOf((int Log, string Area, string Const, int Id) c) =>
            inList.Contains($"Quest[{c.Log}][{c.Id}]") && fileOf.TryGetValue($"Quest[{c.Log}][{c.Id}]", out var f) ? f : null;
        var free = new Queue<(int Log, string Area, string Const, int Id)>(constants.Where(c => !inList.Contains($"Quest[{c.Log}][{c.Id}]")));
        // the container quests take the constants with a quest file first, the call-only ones what is left of them
        var withFile = constants.Where(c => FileOf(c) != null).GroupBy(FileOf).Select(g => g.First()).ToList();
        int containers = (n + 1) / 2;
        var onFileContainers = new Queue<(int Log, string Area, string Const, int Id)>(withFile.Take(containers));
        var onFile = new Queue<(int Log, string Area, string Const, int Id)>(withFile.Skip(containers)
            .Concat(constants.Where(c => inList.Contains($"Quest[{c.Log}][{c.Id}]") && FileOf(c) == null)));
        if (containers - onFileContainers.Count > free.Count || n > constants.Count)
            throw new InvalidOperationException($"{n} synthetic quests need {containers - onFileContainers.Count} quest constants with no Lua file in the list " +
                                                $"({free.Count}) and {n} in all ({constants.Count})");

        // the NPCs: the cities' (interleaved), and one per zone the Lua quests name
        var cityLists = SyntheticCities.Select(z => ValidNpcs(ZoneIdOf(z), z)).ToList();
        var cities = new List<(ushort Zone, string Name)>();
        for (int k = 0; cityLists.Any(l => k < l.Count); k++)
            foreach (var l in cityLists)
                if (k < l.Count) cities.Add(l[k]);
        var fields = new List<(ushort Zone, string Name)>();
        foreach (var zoneId in QuestFileZones())
            if (ZoneNameOf(zoneId) is { } zn && ValidNpcs(zoneId, zn) is { Count: > 0 } valid) fields.Add(valid[0]);
        if (cities.Count < 2 || fields.Count == 0) throw new InvalidOperationException($"too few NPCs: {cities.Count} in the cities, {fields.Count} zones");

        // the LegacyPaths: each just after a real quest file's require name, spread over them
        string questsDir = Path.Combine(Root, "scripts", "quests");
        var files = Directory.GetFiles(questsDir, "*.lua", SearchOption.AllDirectories)
            .Select(f => "scripts/quests/" + Path.GetRelativePath(questsDir, f).Replace('\\', '/')[..^4])
            .OrderBy(f => f, StringComparer.Ordinal).ToList();

        var source = new StringBuilder("using XI.Quests;\nusing XI.Quests.Authoring;\nnamespace XI.SyntheticQuests\n{\n");
        var set = new SyntheticSet();
        var zones = new HashSet<ushort>();
        for (int i = 0; i < n; i++)
        {
            bool container = i % 2 == 0;
            var c = container ? (onFileContainers.Count > 0 ? onFileContainers.Dequeue() : free.Dequeue())
                : onFile.Count > 0 ? onFile.Dequeue() : free.Dequeue();
            string luaFile = container ? FileOf(c) : null;
            if (container) set.Containers++;
            else
            {
                set.CallOnly++;
                if (inList.Contains($"Quest[{c.Log}][{c.Id}]")) set.OnLuaFile++;
            }
            var a = cities[2 * i % cities.Count];
            var b = cities[(2 * i + 1) % cities.Count];
            var f = fields[i % fields.Count];
            zones.Add(a.Zone);
            zones.Add(b.Zone);
            zones.Add(f.Zone);
            // a container quest in place of its Lua file (hidden), else beside a real file's
            string legacy = luaFile ?? (container ? $"{files[i / 2 * 37 % files.Count]}_synthetic_{i}" : null);
            if (luaFile != null) set.Hidden.Add(luaFile);
            source.Append(SyntheticSource(i, c.Log, c.Const, legacy, a, b, f));
        }
        source.Append("}\n");
        set.Zones = zones.Count;
        var asm = CompileSynthetic(source.ToString());
        set.Quests = Enumerable.Range(0, n).Select(i => (QuestScript)Activator.CreateInstance(asm.GetType($"XI.SyntheticQuests.Q{i}", true))).ToList();
        return set;
    }

    // quest i's class: the shape in the header comment
    private static string SyntheticSource(int i, int log, string constName, string legacyPath,
                                          (ushort Zone, string Name) a, (ushort Zone, string Name) b, (ushort Zone, string Name) f)
    {
        string A = $"{a.Zone}, {Literal(a.Name)}", B = $"{b.Zone}, {Literal(b.Name)}", F = $"{f.Zone}, {Literal(f.Name)}";
        int cs = 100 + i % 300 * 6, item = 640 + i % 200;
        string cv = Literal($"[Synthetic]Q{i}");
        var s = new StringBuilder();
        s.Append($"public sealed class Q{i} : QuestScript\n{{\n");
        s.Append($"    public Q{i}() : base((QuestLog){log}, {Literal(constName)}) {{ }}\n");
        if (legacyPath != null) s.Append($"    public override string LegacyPath => {Literal(legacyPath)};\n");
        s.Append("    protected override void Build(QuestScriptBuilder b)\n    {\n");
        if (legacyPath != null)
        {
            s.Append($@"        b.Steps(1, 2, 3, 4);
        b.Reward(new QuestReward {{ Fame = 30, FameArea = {log % 4}, Gil = {200 + i % 50 * 10} }});
        b.State(QuestStatus.Available)
            .Npc({A}).OnTrigger(QuestReply.ProgressEvent({cs}))
            .OnEventFinish({a.Zone}, {cs}, q => {{ q.Quest.Begin(); q.Quest.SetStep(1); }});
        b.State(QuestStatus.Accepted, step: 1)
            .Npc({B}).OnTrigger(q => q.Player.CharVar({cv}) > 0 ? QuestReply.Event({cs + 1}) : QuestReply.ProgressEvent({cs + 1}))
            .OnEventFinish({b.Zone}, {cs + 1}, q => q.Quest.SetStep(2));
        b.State(QuestStatus.Accepted, step: 2)
            .Npc({F}).OnTrade(q => q.Trade.Matches(({item}, 1)) ? QuestReply.ProgressEvent({cs + 2}) : null)
            .OnEventFinish({f.Zone}, {cs + 2}, q => {{ q.Trade.Complete(); q.Quest.SetStep(3); }});
        b.State(QuestStatus.Accepted, step: 3)
            .Npc({A}).OnTrigger(QuestReply.ProgressEvent({cs + 3}))
            .OnEventFinish({a.Zone}, {cs + 3}, q => q.Quest.SetStep(4));
        b.State(QuestStatus.Accepted, step: 4)
            .Npc({B}).OnTrigger(q => QuestReply.ProgressEvent({cs + 4}))
            .OnEventFinish({b.Zone}, {cs + 4}, q => {{ if (q.Quest.Complete()) q.Player.SetCharVar({cv}, 0); }});
        b.State(QuestStatus.Completed)
            .Npc({A}).OnTrigger(QuestReply.Event({cs + 5}));
");
        }
        else
        {
            s.Append($@"        b.Reward(new QuestReward {{ Gil = {200 + i % 50 * 10} }});
        b.State(QuestStatus.Available)
            .Npc({A}).Called().OnTrigger(QuestReply.Event({cs}))
            .OnEventFinish({a.Zone}, {cs}, q => {{ q.Quest.Begin(); q.Player.SetCharVar({cv}, 1); }});
        b.State(QuestStatus.Accepted, when: q => q.Player.CharVar({cv}) == 1)
            .Npc({B}).Called().OnTrigger(q => QuestReply.Event({cs + 1}))
            .OnEventFinish({b.Zone}, {cs + 1}, q => q.Player.SetCharVar({cv}, 2));
        b.State(QuestStatus.Accepted, when: q => q.Player.CharVar({cv}) == 2)
            .Npc({F}).Called().OnTrade(q => q.Trade.Matches(({item}, 1)) ? QuestReply.Event({cs + 2}) : null)
            .OnEventFinish({f.Zone}, {cs + 2}, q => {{ q.Trade.Complete(); q.Player.SetCharVar({cv}, 3); }});
        b.State(QuestStatus.Accepted, when: q => q.Player.CharVar({cv}) == 3)
            .Npc({A}).Called().OnTrigger(QuestReply.Event({cs + 3}))
            .OnEventFinish({a.Zone}, {cs + 3}, q => q.Player.SetCharVar({cv}, 4));
        b.State(QuestStatus.Accepted, when: q => q.Player.CharVar({cv}) == 4)
            .Npc({B}).Called().OnTrigger(q => QuestReply.Event({cs + 4}))
            .OnEventFinish({b.Zone}, {cs + 4}, q => {{ if (q.Quest.Complete()) q.Player.SetCharVar({cv}, 0); }});
        b.State(QuestStatus.Completed)
            .Npc({A}).Called().OnTrigger(QuestReply.Event({cs + 5}));
");
        }
        s.Append("    }\n}\n");
        return s.ToString();
    }

    private static string Literal(string text) => SymbolDisplay.FormatLiteral(text, true);

    // the source compiled (optimized, as a Release build) and loaded; nothing of it runs yet
    private static System.Reflection.Assembly CompileSynthetic(string source)
    {
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
            .Append(typeof(QuestScript).Assembly.Location)
            .GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).Select(g => g.First());
        var compilation = CSharpCompilation.Create("XI.SyntheticQuests", new[] { CSharpSyntaxTree.ParseText(source) },
            paths.Select(p => MetadataReference.CreateFromFile(p)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));
        using var dll = new MemoryStream();
        var result = compilation.Emit(dll);
        if (!result.Success)
            throw new InvalidOperationException("the synthetic quests do not compile: " +
                string.Join("; ", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(5)));
        return System.Reflection.Assembly.Load(dll.ToArray());
    }

    // the Lua code's report(...) values
    private static List<string> LuaReports(string code)
    {
        got.Clear();
        lua.DoString(code, "=synthetic");
        var values = got.ToList();
        got.Clear();
        return values;
    }

    // the zone constants of XI.QuestContent, by name and by id
    private static ushort ZoneIdOf(string name) =>
        (ushort)typeof(XI.QuestContent.Zone).GetField(name).GetValue(null);
    private static Dictionary<ushort, string> zoneNames_;
    private static string ZoneNameOf(ushort id)
    {
        zoneNames_ ??= typeof(XI.QuestContent.Zone).GetFields().Where(f => f.IsLiteral)
            .GroupBy(f => (ushort)f.GetValue(null)).ToDictionary(g => g.Key, g => g.First().Name);
        return zoneNames_.TryGetValue(id, out var name) ? name : null;
    }

    // the zones the Lua quest files name ([xi.zone.X] keys), in id order
    private static List<ushort> QuestFileZones()
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(Path.Combine(Root, "scripts", "quests"), "*.lua", SearchOption.AllDirectories))
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"\[xi\.zone\.([A-Z0-9_]+)\]")) names.Add(m.Groups[1].Value);
        return LuaReports(string.Join("\n", names.Select(z => $"report(tostring(xi.zone.{z}))")))
            .Where(v => v != "nil").Select(ushort.Parse).Distinct().OrderBy(z => z).ToList();
    }

    // the Npc constants of the zone (by constant name) whose entity the registration's check finds in the zone's data
    private static List<(ushort Zone, string Name)> ValidNpcs(ushort zoneId, string zoneName)
    {
        var type = typeof(XI.QuestContent.Npc).GetNestedType(zoneName);
        if (type == null) return new List<(ushort, string)>();
        var names = type.GetFields().Where(f => f.IsLiteral).OrderBy(f => f.Name, StringComparer.Ordinal)
            .Select(f => (string)f.GetValue(null)).Distinct().ToList();
        if (names.Count == 0) return new List<(ushort, string)>();
        var problems = zone.ValidateQuestScript(new NpcProbeQuest(zoneId, names).Compile());
        var missing = new HashSet<string>(problems.Select(p => Regex.Match(p, @"^no NPC or mob named (.+) in zone \d+$"))
            .Where(m => m.Success).Select(m => m.Groups[1].Value), StringComparer.Ordinal);
        return names.Where(nm => !missing.Contains(nm)).Select(nm => (zoneId, nm)).ToList();
    }

    // one OnTrigger per name, for the registration's entity check
    private sealed class NpcProbeQuest : QuestScript
    {
        private readonly ushort zone_;
        private readonly List<string> names_;
        public NpcProbeQuest(ushort zone, List<string> names) : base(QuestLog.Bastok, "GROCERIES") { zone_ = zone; names_ = names; }

        protected override void Build(QuestScriptBuilder b)
        {
            var state = b.State(QuestStatus.Available);
            foreach (var name in names_) state.Npc(zone_, name).OnTrigger(QuestReply.Event(1));
        }
    }
}
