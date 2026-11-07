using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using XI.Script;

namespace XI.Zone
{
    // Quest rework P4: the C# quest modules (every QuestModule in XI.QuestContent), the C# ports of the Lua modules that patch
    // quest containers with xi.module.modifyInteractionEntry. The Lua module itself runs too (platform round P11,
    // ZoneHost.LuaModules), its modifyInteractionEntry of a C# quest skipped; a C# module is enabled when
    // modules/init.txt lists its Lua file or a folder above it (QuestModuleList: the C++ server's rules) and its condition
    // (xi.pre over xi.settings.main) holds. Each registration pass (CSharpQuests.register) works out the enabled modules
    // first, applies each one's overrides to the compiled definitions of the quests it names (QuestOverrides.Apply) and runs
    // the validator on the result; RegisterQuestScript then builds the patched definition, and once every quest is placed
    // the patched containers move to the end of the list, in the order the modules patched them (PlacePatchedLast): Lua's
    // modifyInteractionEntry removes the container from the lookup and adds it back, which appends its entries to the end
    // of every list it is in (after the defaults, which the lookup gets before any container). A module with an override
    // the quest cannot take, or whose patched quest fails the validator, is refused whole (logged) and its quests register
    // as they are.
    public sealed partial class ZoneHost
    {
        // modules/init.txt's lines, as the file holds them; null (the default): the file is read at each registration pass.
        // The golden harness gives a flow's module lines here, so no run depends on the local init.txt
        public IReadOnlyList<string> ModuleInitLines { get; set; }
        // modules the verify suites add after the discovered ones (a module to be refused, say)
        internal List<QuestModule> TestQuestModules { get; } = new List<QuestModule>();
        // the modules the last registration pass enabled, in load order: a copy (the next pass does not change it)
        public IReadOnlyList<QuestModuleInfo> EnabledQuestModules => enabledModules_.ToArray();

        private readonly List<QuestModuleInfo> enabledModules_ = new List<QuestModuleInfo>();
        // quest key -> its definition with the last pass's module applied, and that module
        private readonly Dictionary<string, (QuestScriptDefinition Def, QuestModuleInfo Module)> modulePatches_ =
            new Dictionary<string, (QuestScriptDefinition, QuestModuleInfo)>(StringComparer.Ordinal);
        // the patched quest keys in the order their last patch came (a module's quests in the order it names them, the
        // modules in load order; a quest patched again moves to the later place, as a second modifyInteractionEntry would)
        private readonly List<string> patchOrder_ = new List<string>();
        // quest key -> the container id of the patched quest the last pass placed in the list
        private readonly Dictionary<string, string> placedPatched_ = new Dictionary<string, string>(StringComparer.Ordinal);
        // the XI.QuestContent modules, found once (when a pass first has an entry); their constructors' errors
        private List<QuestModule> questModules_;
        private readonly List<string> questModuleErrors_ = new List<string>();

        // the coverage hook's key for a module's handlers and checks (HandlerCalled(key, "<quest key> <label>"))
        public static string ModuleCoverageKey(string luaFile) => "module " + luaFile;

        // the definition the registration builds for the quest: the last pass's patched one, else its own
        private QuestScriptDefinition WithModules(string key, QuestScriptDefinition def) =>
            modulePatches_.TryGetValue(key, out var p) ? p.Def : def;

        // one run of a handler for the coverage hook: a module's under its module's key, a quest's own under the quest's
        private void ReportHandler(QuestScriptBinding b, QuestHandlerDecl h)
        {
            var hook = HandlerCalled;
            if (hook == null) return;
            if (h.Module != null) hook(ModuleCoverageKey(h.Module.LuaFile), $"{b.Key} {b.Label(h)}");
            else hook(b.Key, b.Label(h));
        }

        private List<QuestModule> AllQuestModules()
        {
            if (questModules_ == null)
            {
                questModules_ = new List<QuestModule>();
                var types = typeof(global::XI.QuestContent.Zone).Assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && typeof(QuestModule).IsAssignableFrom(t))
                    .OrderBy(t => t.FullName, StringComparer.Ordinal);
                foreach (var t in types)
                {
                    try
                    {
                        questModules_.Add((QuestModule)Activator.CreateInstance(t));
                    }
                    catch (Exception e)
                    {
                        questModuleErrors_.Add($"{t.FullName}: {(e is System.Reflection.TargetInvocationException { InnerException: { } inner } ? inner : e).Message}");
                    }
                }
            }
            return questModules_.Concat(TestQuestModules).ToList();
        }

        // init.txt's lines (none when the file is not there, as the C++ reads an absent file)
        private IReadOnlyList<string> ReadModuleInit()
        {
            string root = scripts_?.Root;
            string path = root == null ? null : Path.Combine(root, "modules", "init.txt");
            return path != null && File.Exists(path) ? File.ReadAllText(path).Split('\n') : Array.Empty<string>();
        }

        // the first step of a registration pass: the enabled modules, each applied and validated; logs a line per module
        // enabled (modules: <name> enabled (<file>)), listed but off, or refused
        private void PrepareQuestModules(LuaState l)
        {
            modulePatches_.Clear();
            enabledModules_.Clear();
            patchOrder_.Clear();
            placedPatched_.Clear();
            if (!CSharpQuestsEnabled) return;
            var entries = QuestModuleList.Entries(ModuleInitLines ?? ReadModuleInit());
            // nothing listed: no module is looked for (the startup's common case costs nothing)
            if (entries.Count == 0) return;
            var modules = AllQuestModules();
            foreach (var error in questModuleErrors_) log_($"modules: {error}");
            var settings = new ModuleSettings(l);
            var quests = QuestsByKey(l);
            foreach (var file in QuestModuleList.LoadOrder(entries, modules.Select(m => m.LuaFile)))
            {
                var ports = modules.Where(m => m.LuaFile == file).ToList();
                if (ports.Count > 1)
                {
                    log_($"modules: {file} refused: more than one C# module ports it ({string.Join(", ", ports.Select(m => m.GetType().FullName))})");
                    continue;
                }
                var m = ports[0];
                bool on;
                try
                {
                    on = m.Condition(settings);
                }
                catch (Exception e)
                {
                    log_($"modules: {m.Name} refused: its condition threw: {e.Message}");
                    continue;
                }
                if (!on)
                {
                    log_($"modules: {m.Name} listed, not enabled: its condition does not hold ({m.LuaFile})");
                    continue;
                }
                var patched = Patch(m, settings, quests, out var problems, honourOff: true);
                if (problems.Count > 0)
                {
                    foreach (var p in problems) log_($"modules: {m.Name} refused: {p}");
                    continue;
                }
                foreach (var (key, def) in patched)
                {
                    // modifyInteractionEntry reloads the quest's file each time: the later module's patch replaces the earlier's
                    if (modulePatches_.TryGetValue(key, out var earlier))
                        log_($"modules: {key} is patched by {earlier.Module.Name} and {m.Name}: as on the Lua server, only {m.Name}'s overrides apply (modifyInteractionEntry reloads the quest's file)");
                    modulePatches_[key] = (def, m.Info);
                    patchOrder_.Remove(key);
                    patchOrder_.Add(key);
                }
                enabledModules_.Add(m.Info);
                log_($"modules: {m.Name} enabled ({m.LuaFile})");
            }
        }

        // the last step of a registration pass: each patched quest's container taken out of the list and appended, in
        // patchOrder_, as xi.module.modifyInteractionEntry's removeContainer / addContainer at xi.server.onServerStart
        // would leave the lookup: the patched quest's entries after every other container's in each list it shares
        private void PlacePatchedLast(LuaState l)
        {
            if (placedPatched_.Count == 0 || !l.IsTable(1)) return;
            int top = l.Top;
            try
            {
                foreach (var key in patchOrder_)
                {
                    if (!placedPatched_.TryGetValue(key, out var containerId)) continue;
                    int at = ContainerIndex(l, containerId);
                    if (at == 0) continue;
                    // local c = table.remove(list, at); list[#list + 1] = c
                    l.GetGlobal("table");
                    l.GetField(-1, "remove");
                    l.PushValue(1);
                    l.Push((double)at);
                    l.Call(2, 1);
                    l.RawSetI(1, l.ObjLen(1) + 1);
                    l.SetTop(top);
                    containerCache_ = null;
                }
            }
            finally
            {
                containerCache_ = null;
                l.SetTop(top);
            }
        }

        // the module's patched definitions, by quest key; problems: why the module is refused (empty when it is not).
        // honourOff: a quest in CSHARP_QUESTS_OFF is left to its Lua (logged), not patched
        private List<(string Key, QuestScriptDefinition Def)> Patch(QuestModule m, IQuestSettings settings, Dictionary<string, CompiledQuestScript> quests,
                                                                    out List<string> problems, bool honourOff)
        {
            problems = new List<string>();
            var patched = new List<(string, QuestScriptDefinition)>();
            QuestModuleDefinition md;
            try
            {
                md = m.Compile(settings);
            }
            catch (Exception e)
            {
                problems.Add($"its Build threw: {e.Message}");
                return patched;
            }
            if (md.Patches.Count == 0) problems.Add("it declares no override");
            foreach (var patch in md.Patches)
            {
                string key = patch.Key;
                if (!quests.TryGetValue(key, out var q) || q.Def == null)
                {
                    problems.Add(q == null ? $"{key} is not a C# quest" : $"{key} did not compile: {q.Error}");
                    continue;
                }
                if (honourOff && CSharpQuestsOff.Contains(key))
                {
                    log_($"modules: {m.Name}: {key} is in CSHARP_QUESTS_OFF; its Lua file is in charge and the C# overrides are not applied");
                    continue;
                }
                var own = LabelsOf(q.Def).ToDictionary(x => x.Handler, x => x.Label, ReferenceEqualityComparer.Instance);
                string moduleKey = ModuleCoverageKey(m.LuaFile);
                var result = QuestOverrides.Apply(q.Def, md, key,
                    baseCalled: h => HandlerCalled?.Invoke(key, own.TryGetValue(h, out var label) ? label : "?"),
                    checkCalled: n => HandlerCalled?.Invoke(moduleKey, $"{key} {n}/-/check"));
                if (result.Definition == null)
                {
                    // Apply names the module first; the log line does already
                    problems.AddRange(result.Problems.Select(p => p.StartsWith(m.Name + ": ", StringComparison.Ordinal) ? p.Substring(m.Name.Length + 2) : p));
                    continue;
                }
                // the validator on the patched script, the registration's own checks (entity names) included
                var invalid = ValidateQuestScript(result.Definition);
                if (invalid.Count > 0)
                {
                    problems.AddRange(invalid.Select(p => $"{key} patched: {p}"));
                    continue;
                }
                patched.Add((key, result.Definition));
            }
            return patched;
        }

        // the coverage hook for the quest's removed states (RemoveState), called where a check of the quest runs: each
        // removed state whose matches from before the removal hold for the player is reported reached, "<n>/-/removed" under
        // its module's key (the state would have played; the module's removal is what the flow saw). Only while the hook is
        // set: the matches read the quest's vars and the player as the state's check would
        private void ReportRemoved(QuestScriptBinding b, QuestStatus status, LiveQuestContext ctx)
        {
            var hook = HandlerCalled;
            if (hook == null) return;
            var states = b.Def.States;
            for (int i = 0; i < states.Count; i++)
            {
                var removed = states[i];
                if (removed.RemovedBy == null || removed.RemovedMatches == null) continue;
                try
                {
                    int step = removed.RemovedMatches.Any(m => (m.AnyStatus || m.Status == status) && m.Step.HasValue) ? ctx.Step : 0;
                    if (removed.RemovedWouldMatch(status, step, ctx)) hook(ModuleCoverageKey(removed.RemovedBy.LuaFile), $"{b.Key} {i + 1}/-/removed");
                }
                catch (Exception)
                {
                    // a guard that throws: not reached
                }
            }
        }

        // the compiled quests (XI.QuestContent's, then the test ones) by key; the first of a key
        private Dictionary<string, CompiledQuestScript> QuestsByKey(LuaState l)
        {
            var byKey = new Dictionary<string, CompiledQuestScript>(StringComparer.Ordinal);
            foreach (var q in questScripts_.Concat(TestQuestScripts.OfType<QuestScript>().Select(CompiledTest)))
            {
                if (q.Script == null) continue;
                string key = QuestKeyOf(q.Script);
                if (key != null) byKey.TryAdd(key, q);
            }
            return byKey;
        }

        // the coverage labels of the module's overrides in the last pass, "<quest key> <label>" (a handler's label in the
        // patched quest; an overridden check's <n>/-/check; a removed state's <n>/-/removed; a replaced reward's reward), by
        // key then declaration order; null when the last pass did not enable the module
        public IReadOnlyList<string> ModuleLabels(string luaFile)
        {
            if (!enabledModules_.Any(m => m.LuaFile == luaFile)) return null;
            var labels = new List<string>();
            foreach (var (key, (def, module)) in modulePatches_.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                if (module.LuaFile != luaFile) continue;
                for (int i = 0; i < def.States.Count; i++)
                {
                    if (def.States[i].CheckModule?.LuaFile == luaFile) labels.Add($"{key} {i + 1}/-/check");
                    if (def.States[i].RemovedBy?.LuaFile == luaFile) labels.Add($"{key} {i + 1}/-/removed");
                }
                labels.AddRange(LabelsOf(def).Where(x => x.Handler.Module?.LuaFile == luaFile).Select(x => $"{key} {x.Label}"));
                if (def.RewardModule?.LuaFile == luaFile) labels.Add($"{key} reward");
            }
            return labels;
        }

        // every C# module (whatever init.txt and its condition say) applied to its quests and the patched quests validated,
        // under the settings of now and under each xi.pre branch (QuestModule.Branches: a Build that reads Pre or the
        // settings is compiled down each way): the problems, "<module>: <problem>", a branch's own with " (with xi.pre
        // true)" (the golden startup gate and the verify suites)
        public IReadOnlyList<string> CheckQuestModules()
        {
            var problems = new List<string>();
            if (lua_ == null) return problems;
            int top = lua_.Top;
            try
            {
                var modules = AllQuestModules();
                problems.AddRange(questModuleErrors_);
                var settings = new ModuleSettings(lua_);
                var quests = QuestsByKey(lua_);
                foreach (var dup in modules.GroupBy(m => m.LuaFile).Where(g => g.Count() > 1))
                    problems.Add($"{dup.Key}: more than one C# module ports it");
                foreach (var m in modules)
                {
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var (branch, branchSettings) in new[] { ((string)null, (IQuestSettings)settings) }.Concat(QuestModule.Branches))
                    {
                        Patch(m, branchSettings, quests, out var each, honourOff: false);
                        foreach (var p in each)
                            if (seen.Add(p)) problems.Add(branch == null ? $"{m.Name}: {p}" : $"{m.Name}: {p} (with {branch})");
                    }
                }
            }
            finally
            {
                lua_.SetTop(top);
            }
            return problems;
        }

        // the verify suites' view of what a module's condition reads (QuestScripts.Live, quest rework P9): a setting's
        // MainValue and xi.pre(expansion), through the same ModuleSettings the module list uses
        internal string ModuleSettingRead(string name, QuestExpansion expansion)
        {
            var settings = new ModuleSettings(lua_);
            return $"{settings.MainValue(name)}|{QuestModule.Pre(settings, expansion)}";
        }

        // xi.settings.main as a module's condition reads it: a whole number as itself, true / false as 1 / 0, anything else
        // (unset, a fraction, a string) as 0, so xi.pre's "== 1 or == true" is Main(name) == 1. xi.settings.map the same
        // (quest rework P6), and MapFlag the boolean true only
        private sealed class ModuleSettings : IQuestSettings
        {
            private readonly LuaState l_;

            public ModuleSettings(LuaState l) => l_ = l;

            public int Main(string name) => Read("xi.settings.main", name);
            public int Map(string name) => Read("xi.settings.map", name);

            // xi.settings.main[name] with its Lua type (quest rework P9): xi.pre's `== 1 or == true` is MainEquals or MainFlag
            public QuestSetting MainValue(string name)
            {
                int top = l_.Top;
                try
                {
                    l_.GetPath("xi.settings.main");
                    if (!l_.IsTable(-1)) return QuestSetting.Nil;
                    l_.GetField(-1, name);
                    return LiveQuestContext.SettingOf(l_);
                }
                finally
                {
                    l_.SetTop(top);
                }
            }

            public bool MapFlag(string name)
            {
                int top = l_.Top;
                try
                {
                    l_.GetPath("xi.settings.map");
                    if (!l_.IsTable(-1)) return false;
                    l_.GetField(-1, name);
                    return l_.IsBoolean(-1) && l_.ToBoolean(-1);
                }
                finally
                {
                    l_.SetTop(top);
                }
            }

            private int Read(string table, string name)
            {
                int top = l_.Top;
                try
                {
                    l_.GetPath(table);
                    if (!l_.IsTable(-1)) return 0;
                    l_.GetField(-1, name);
                    if (l_.IsBoolean(-1)) return l_.ToBoolean(-1) ? 1 : 0;
                    if (!l_.IsNumber(-1)) return 0;
                    double v = l_.ToNumber(-1);
                    return Math.Floor(v) == v && v >= int.MinValue && v <= int.MaxValue ? (int)v : 0;
                }
                finally
                {
                    l_.SetTop(top);
                }
            }
        }
    }
}
