using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Persistence;
using XI.Quests;
using XI.Quests.Authoring;
using XI.Script;

namespace XI.Zone
{
    // Quest rework B2 (docs/superpowers/specs/2026-10-21-csharp-quests-live-design.md, sections 3, 5 and 6): the quests
    // written in C# (every QuestScript in XI.QuestContent) run inside the Lua InteractionLookup in place of their
    // scripts/quests files. InteractionGlobal.loadContainers calls CSharpQuests.register(containers) on its list of the
    // loaded files before addContainers; each quest switched on becomes a real Lua Quest container (Quest:new, its
    // reward, its sections) whose section checks and handlers are C# closures, and takes the place of the Lua container
    // with its id at the same index, so the lookup lists keep the Lua order. A handler's QuestReply becomes the Lua
    // action through the container's own helper (quest:progressEvent(216), ...), and LiveQuestContext calls the Lua
    // functions the Lua quest calls, so the packets, the rows and the lookup's priorities, cycling and fallbacks are the
    // Lua file's.
    public sealed partial class ZoneHost
    {
        // map.CSHARP_QUESTS: off, no C# quest is registered and the scripts are the Lua server's
        public bool CSharpQuestsEnabled { get; set; } = true;
        // map.CSHARP_QUESTS_OFF: the quest keys (bastok.the_elvaan_goldsmith) left to their Lua files
        public HashSet<string> CSharpQuestsOff { get; } = new();

        // the QuestScripts of XI.QuestContent, each instantiated and compiled once, in type-name order
        private List<CompiledQuestScript> questScripts_ = new List<CompiledQuestScript>();
        // quests (and, quest rework C2, missions) the verify suites register after those (never discovered: a
        // definition to be refused, say)
        internal List<ContainerScript> TestQuestScripts { get; } = new List<ContainerScript>();

        // the keys of the C# quests that failed to load with no Lua file to fall back to (the last load's), for the gate
        internal List<string> QuestLoadFailures { get; } = new List<string>();
        // test only: the discovered quests are left out, so a test quest with one's key can be the one registered
        internal bool SkipDiscoveredQuests { get; set; }
        // test hook (the golden suite's coverage gate): called with the quest key and the handler's label
        // (<n>/<entity>/<trigger>) each time a C# handler runs; null in production. Read when the containers are built
        // too: only while it is set is a fixed reply pushed as a closure that counts its runs
        public Action<string, string> HandlerCalled { get; set; }
        // the test hook of a C# handler that threw: the line logged ("quests: <key> <handler>: <message>") by
        // RunHandler (the handler's body) or by a container handler's closure (its context, its reply's push and
        // checks), what the golden harness's expect-error step needs on the C# side (null: none)
        public Action<string> HandlerThrew { get; set; }
        // the finish handlers each C# quest ran in the onEventFinish dispatch open now, by quest key: their labels in
        // the order they ran (null: none ran yet, or no dispatch open); finishOpen_: a dispatch is open
        private Dictionary<string, List<string>> finishRuns_;
        private bool finishOpen_;

        // the line a dispatch logs when one quest ran more than one finish handler in it (a finish declared in two
        // states that both matched: the Lua lookup reads each section's check after the earlier handlers ran, so a
        // status the first changed lets the second match too). A golden flow declares one with a warning: line
        public static string DoubleFinishLine(string key, int csid, IReadOnlyList<string> labels) =>
            $"quests: {key}: one finish of {csid} ran {labels.Count} handlers ({string.Join(", ", labels)})";
        // the list of containers InteractionGlobal.loadContainers last built (a registry reference)
        private LuaRef containersRef_;
        // key -> LegacyPath of the quests in the list now, for the slot of the next
        private readonly Dictionary<string, string> legacyPaths_ = new Dictionary<string, string>(StringComparer.Ordinal);

        private sealed class CompiledQuestScript : CompiledScript<QuestScript, QuestScriptDefinition>
        {
        }

        // how long finding and compiling the XI.QuestContent quests took (each quest's Build JIT-compiled on a cold start),
        // added to the next register pass's time on the registered line
        private TimeSpan discoverTime_;

        // the global CSharpQuests = { register = ... }, with the quests found in XI.QuestContent
        private void RegisterQuestScriptGlobals()
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var containers = DiscoverContainers();
            questScripts_ = DiscoverQuestScripts(containers);
            // quest rework C2: the missions of the same pass (ZoneHost.MissionScripts)
            missionScripts_ = DiscoverMissionScripts(containers);
            // and the battlefields (ZoneHost.BattlefieldScripts)
            battlefieldScripts_ = DiscoverBattlefieldScripts(containers);
            discoverTime_ = clock.Elapsed;
            lua_.NewTable();
            lua_.PushFunction(RegisterQuestScripts);
            lua_.SetField(-2, "register");
            // platform round P11: the Lua modules' modifyInteractionEntry guard (ZoneHost.LuaModules)
            lua_.PushFunction(SkipLuaPatch);
            lua_.SetField(-2, "skipPatch");
            lua_.SetGlobal("CSharpQuests");
            // t[k] under a protected call (a check reads vars.Prog through the lookup's var cache, a metatable)
            lua_.DoString("CSharpQuests.get = function(t, k) return t[k] end", "=CSharpQuests");
            // Player.SpawnMobWithNpcCooldown: Forge Your Destiny's qm spawn, line for line (the mob claimed, the qm's id on it,
            // its DESPAWN listener setting the qm's cooldown var to GetSystemTime() + seconds)
            lua_.DoString(
                "CSharpQuests.spawnMobWithNpcCooldown = function(player, npcId, mobId, npcVar, seconds)\n" +
                "    local mob = SpawnMob(mobId)\n" +
                "    if not mob then\n" +
                "        return false\n" +
                "    end\n" +
                "    mob:updateClaim(player)\n" +
                "    mob:setLocalVar('QMID', npcId)\n" +
                "    mob:addListener('DESPAWN', 'DESPAWN_' .. mobId, function(mobArg)\n" +
                "        local qmID = mobArg:getLocalVar('QMID')\n" +
                "        mobArg:removeListener('DESPAWN_' .. mobId)\n" +
                "        GetNPCByID(qmID):setLocalVar(npcVar, GetSystemTime() + seconds)\n" +
                "    end)\n" +
                "    return true\n" +
                "end", "=CSharpQuests");
            // Player.MobIdByName / MobIsSpawnedById / SpawnMobById: The Price of Valor's Underbrush, line for line
            lua_.DoString(
                "CSharpQuests.mobIdByName = function(player, name)\n" +
                "    return player:getZone():queryEntitiesByName(name)[1]:getID()\n" +
                "end\n" +
                "CSharpQuests.mobIsSpawnedById = function(id)\n" +
                "    return GetMobByID(id):isSpawned()\n" +
                "end\n" +
                "CSharpQuests.spawnMobById = function(player, id, claim)\n" +
                "    local mob = SpawnMob(id)\n" +
                "    if claim then\n" +
                "        mob:updateClaim(player)\n" +
                "    else\n" +
                "        mob:updateEnmity(player)\n" +
                "    end\n" +
                "end", "=CSharpQuests");
            // quest rework C3 C4: Player.SpawnMobEntityById (CoP 7-4's Executor and Dalham, line for line) and
            // Mission.SetAllianceLocalVars (CoP 8-1's Rubious Crystals: the loop over the alliance)
            lua_.DoString(
                "CSharpQuests.spawnMobEntityById = function(player, id, zeroEnmity)\n" +
                "    local mob = GetMobByID(id)\n" +
                "    if not mob then\n" +
                "        return false\n" +
                "    end\n" +
                "    mob:spawn()\n" +
                "    mob:updateClaim(player)\n" +
                "    if zeroEnmity then\n" +
                "        mob:setCE(player, 0)\n" +
                "    end\n" +
                "    return true\n" +
                "end\n" +
                "CSharpQuests.allianceLocalVars = function(container, player, vars)\n" +
                "    for _, member in ipairs(player:getAlliance()) do\n" +
                "        for _, v in ipairs(vars) do\n" +
                "            container:setLocalVar(member, v[1], v[2])\n" +
                "        end\n" +
                "    end\n" +
                "end", "=CSharpQuests");
            RegisterQuestCallGlobals();
            // quest rework C2: the battlefields' zone hook helpers (ZoneHost.BattlefieldScripts)
            RegisterBattlefieldScriptGlobals();
            // quest rework P10: the deferred callbacks' closures and the exports' module claims
            // (ZoneHost.QuestCallbacks)
            RegisterQuestCallbackGlobals();
        }

        // one class of XI.QuestContent's containers, made once: its instance, or what its constructor threw
        private sealed class DiscoveredContainer
        {
            public Type Type;
            public ContainerScript Script;
            public string Error;
        }

        // every non-abstract ContainerScript class of XI.QuestContent, in type-name order, each instantiated once
        private static List<DiscoveredContainer> DiscoverContainers()
        {
            var types = typeof(global::XI.QuestContent.Zone).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(ContainerScript).IsAssignableFrom(t))
                .OrderBy(t => t.FullName, StringComparer.Ordinal);
            var list = new List<DiscoveredContainer>();
            foreach (var t in types)
            {
                var c = new DiscoveredContainer { Type = t };
                try
                {
                    c.Script = (ContainerScript)Activator.CreateInstance(t);
                }
                catch (Exception e)
                {
                    c.Error = InnerMessage(e);
                }
                list.Add(c);
            }
            return list;
        }

        // what an exception says, a constructor's own when reflection wrapped it
        private static string InnerMessage(Exception e) =>
            (e is System.Reflection.TargetInvocationException { InnerException: { } inner } ? inner : e).Message;

        // the C# containers of XI.QuestContent, every kind, in type-name order; a class whose constructor throws is
        // left out and logged as a quest is ("quests: <type> not registered: <its message>")
        internal IReadOnlyList<ContainerScript> DiscoverContainerScripts()
        {
            var list = new List<ContainerScript>();
            foreach (var c in DiscoverContainers())
            {
                if (c.Script != null) list.Add(c.Script);
                else log_($"quests: {c.Type.FullName} not registered: {c.Error}");
            }
            return list;
        }

        // the QuestScripts of the containers (DiscoverContainers), each compiled once, in type-name order; a quest
        // whose constructor or Compile threw is kept with the error
        private static List<CompiledQuestScript> DiscoverQuestScripts(List<DiscoveredContainer> containers)
        {
            var list = DiscoverScripts<CompiledQuestScript, QuestScript>(containers);
            foreach (var q in list) CompileScript(q, s => s.Compile());
            return list;
        }

        private static CompiledQuestScript Compiled(QuestScript s)
        {
            var q = new CompiledQuestScript { TypeName = s.GetType().FullName, Script = s };
            CompileScript(q, x => x.Compile());
            return q;
        }

        // the registration's checks that need no Lua: QuestScriptValidator's; every NPC or mob a handler names exists in
        // its zone's data; a fixed sequence holds plain messages only (public for the verify suites)
        public IReadOnlyList<string> ValidateQuestScript(QuestScriptDefinition def)
        {
            var problems = new List<string>(QuestScriptValidator.Validate(def));
            problems.AddRange(EntityProblems(def));
            return problems;
        }

        // every NPC or mob a handler of the states names exists in its zone's data (the quests' and the missions'
        // check)
        private List<string> EntityProblems(QuestScriptDefinition def)
        {
            var problems = new List<string>();
            var said = new HashSet<(ushort, string)>();
            foreach (var state in def.States)
                foreach (var h in state.Handlers)
                {
                    if (h.Entity == null || !said.Add((h.Zone, h.Entity))) continue;
                    HashSet<string> names;
                    try
                    {
                        names = ZoneEntityNames(h.Zone);
                    }
                    catch (Exception e)
                    {
                        problems.Add($"zone {h.Zone}'s NPCs and mobs could not be read: {e.Message}");
                        continue;
                    }
                    if (names == null) problems.Add($"unknown zone {h.Zone} (for {h.Entity})");
                    else if (!names.Contains(h.Entity) && !DynamicEntityNames(h.Zone).Contains(h.Entity)) problems.Add($"no NPC or mob named {h.Entity} in zone {h.Zone}");
                }
            return problems;
        }

        // the NPC and mob names of a zone as its data files give them (npcs.yaml's scripts, whatever content is enabled;
        // mobs.yaml's placed spawns), read once: a quest is checked at the start, before any zone is up. null for a zone
        // zone_settings does not have
        private readonly Dictionary<ushort, HashSet<string>> zoneEntityNames_ = new Dictionary<ushort, HashSet<string>>();

        private static readonly System.Text.RegularExpressions.Regex DynamicEntityCall =
            new System.Text.RegularExpressions.Regex(@"insertDynamicEntity\(\s*\{.*?\}\s*\)", System.Text.RegularExpressions.RegexOptions.Singleline);
        private static readonly System.Text.RegularExpressions.Regex DynamicEntityName =
            new System.Text.RegularExpressions.Regex(@"\b(?:name|objName)\s*=\s*['""]([^'""]+)['""]");

        // zone id -> its zone_settings name, read with the zone list in InitScripts (zoneNamesRead_)
        private readonly Dictionary<ushort, string> zoneNameOf_ = new Dictionary<ushort, string>();
        private bool zoneNamesRead_;
        private readonly Dictionary<ushort, HashSet<string>> dynamicNames_ = new Dictionary<ushort, HashSet<string>>();

        // names only a dynamic entity has: the string literal of the name = / objName = field of the zone scripts'
        // insertDynamicEntity({ ... }) calls (a name built at run time is not found). Read lazily, only for a handler
        // whose entity is in neither yaml file: it walks every Lua file of the zone
        private HashSet<string> DynamicEntityNames(ushort zoneId)
        {
            if (dynamicNames_.TryGetValue(zoneId, out var names)) return names;
            names = new HashSet<string>(StringComparer.Ordinal);
            string scripts = scripts_ == null || !zoneNameOf_.TryGetValue(zoneId, out var zn) ? null : Path.Combine(scripts_.ScriptsDir, "zones", zn);
            if (scripts != null && Directory.Exists(scripts))
                foreach (var file in Directory.GetFiles(scripts, "*.lua", SearchOption.AllDirectories))
                {
                    string text = File.ReadAllText(file);
                    foreach (System.Text.RegularExpressions.Match call in DynamicEntityCall.Matches(text))
                        foreach (System.Text.RegularExpressions.Match field in DynamicEntityName.Matches(call.Value))
                            names.Add(field.Groups[1].Value);
                }
            return dynamicNames_[zoneId] = names;
        }

        // the names from the script engine's ID scan of the zone's folder (ScriptEngine.EntityNamesOf), the same line scan
        // for a zone it did not read; the zone's name from the list InitScripts read
        internal HashSet<string> ZoneEntityNames(ushort zoneId)
        {
            if (zoneEntityNames_.TryGetValue(zoneId, out var names)) return names;
            if (!zoneNameOf_.TryGetValue(zoneId, out var zoneName))
            {
                var r = zoneNamesRead_ ? null : Exec("SELECT name FROM zone_settings WHERE zoneid = ?", BoundValue.Of(zoneId));
                if (r == null || !r.Next()) return zoneEntityNames_[zoneId] = null;
                zoneNameOf_[zoneId] = zoneName = r.Get<string>("name");
            }
            string dir = Path.Combine(dataDir_, "zones", ZoneInfo.FolderName(zoneName));
            var scanned = scripts_ != null ? scripts_.EntityNamesOf(dir) : ScriptEngine.ScanEntityNames(dir);
            names = new HashSet<string>(scanned.Npcs, StringComparer.Ordinal);
            // a mobs.yaml spawn with no placement never spawns: a handler naming only that is refused
            names.UnionWith(scanned.PlacedMobs);
            return zoneEntityNames_[zoneId] = names;
        }

        // CSharpQuests.register(containers), from InteractionGlobal.loadContainers before addContainers: every C# quest
        // switched on takes the place of the Lua container with its id in that list of the loaded files, at the same
        // index (or at the end when no file has that id). Each reload(true) builds them anew: their closures and the
        // registry refs of the old ones are not freed (a development path, a few entries per quest)
        private int RegisterQuestScripts(LuaState l)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            int top = l.Top;
            int registered = 0, missions = 0, battlefields = 0;
            var keys = new HashSet<string>();
            callOnlyOff_.Clear();
            exportModules_.Clear();
            exportGlobals_.Clear();
            try
            {
                // CSharpQuests.live = { [container id] = key } for the quests this load put in C# (!reloadquest reads it)
                l.GetGlobal("CSharpQuests");
                if (l.IsTable(-1))
                {
                    l.NewTable();
                    l.SetField(-2, "live");
                    // CSharpQuests.legacy = { [LegacyPath] = key }: !reloadquest finds a quest whose Lua file is gone by it
                    l.NewTable();
                    l.SetField(-2, "legacy");
                }
                l.SetTop(top);
                legacyPaths_.Clear();
                // the CallOnly bindings' quest objects are held only by their Ref; the container bindings' are not released
                foreach (var old in npcCallQuests_.Values)
                    if (old.Def.CallOnly && old.Container != null) l.Unref(old.Container);
                npcCallQuests_.Clear();
                QuestLoadFailures.Clear();
                if (containersRef_ != null) l.Unref(containersRef_);
                containersRef_ = null;
                if (l.IsTable(1))
                {
                    l.PushValue(1);
                    containersRef_ = l.Ref();
                }
                containerCache_ = null;
                // the enabled C# modules applied first (quest rework P4, ZoneHost.QuestModules): a quest they patch is built
                // and placed from its patched definition below
                try
                {
                    PrepareQuestModules(l);
                }
                catch (Exception e)
                {
                    modulePatches_.Clear();
                    enabledModules_.Clear();
                    log_($"modules: none applied: {e.Message}");
                }
                finally
                {
                    l.SetTop(top);
                }
                if (l.IsTable(1))
                    registered = RegisterEach(l, InLoadOrder(
                            (SkipDiscoveredQuests ? new List<CompiledQuestScript>() : questScripts_)
                            .Concat(TestQuestScripts.OfType<QuestScript>().Select(Compiled))), top, q => q.TypeName,
                        (CompiledQuestScript q, ref string name, ref bool duplicate, out QuestScriptBinding bound) =>
                            RegisterQuestScript(l, q, keys, ref name, ref duplicate, out bound),
                        // quest rework P10: its exports, registered or not (a refused quest's are no-ops). A quest
                        // refused because another has its key sets none: the key's first quest owns the module and
                        // the globals, and the second's no-ops would replace them
                        (q, name, bound) => ClaimExports(l, q.Script, q.Def, name, bound));
                // quest rework C2: the missions, each in place of its Lua Mission container (ZoneHost.MissionScripts)
                if (l.IsTable(1)) missions = RegisterMissionScripts(l, keys, top);
                // and the battlefields, each in place of its Lua content, with their zones' hooks set again
                // (ZoneHost.BattlefieldScripts)
                if (l.IsTable(1)) battlefields = RegisterBattlefieldScripts(l, keys, top);
                // the patched quests last, as modifyInteractionEntry leaves them (ZoneHost.QuestModules)
                try
                {
                    PlacePatchedLast(l);
                }
                catch (Exception e)
                {
                    log_($"modules: the patched quests not moved to the end of the list: {e.Message}");
                }
            }
            finally
            {
                containerCache_ = null;
                l.SetTop(top);
            }
            foreach (var key in CSharpQuestsOff.Where(k => !keys.Contains(k)).OrderBy(k => k, StringComparer.Ordinal))
                log_($"quests: {key} in CSHARP_QUESTS_OFF is not a C# quest");
            // a call-only quest has no Lua container to fall back to: switched off, its scripts' calls find no quest (M6)
            if (callOnlyOff_.Count > 0)
                log_($"quests: call-only quests not registered (CSHARP_QUESTS / CSHARP_QUESTS_OFF): {string.Join(", ", callOnlyOff_.OrderBy(k => k, StringComparer.Ordinal))}");
            // the time of the pass, and of the discovery before the first: the C# quests' whole cost at a start
            var spent = clock.Elapsed + discoverTime_;
            discoverTime_ = TimeSpan.Zero;
            log_($"quests: {registered} C# quests, {missions} C# missions and {battlefields} C# battlefields " +
                 $"registered in {spent.TotalMilliseconds:F0} ms");
            return 0;
        }

        private IEnumerable<CompiledQuestScript> InLoadOrder(IEnumerable<CompiledQuestScript> all)
        {
            string SortKey(CompiledQuestScript q)
            {
                try
                {
                    return QuestKeyOf(q.Script) ?? q.TypeName;
                }
                catch (Exception)
                {
                    return q.TypeName;
                }
            }
            return LegacyFirst(all, q => q.Script?.LegacyPath, SortKey);
        }

        // the list at index 1 as C# data, built once per register pass and kept in step with each insert: the container ids
        // and the effective file of each (null when it has none), the id -> index map and the indexes holding a file in
        // list order (the lookups' structures, rebuilt lazily after a change)
        private sealed class ContainerIndexCache
        {
            public readonly List<string> Ids = new List<string>();
            public readonly List<string> Files = new List<string>();
            public Dictionary<string, int> ById;
            public int[] WithFile;
        }

        private ContainerIndexCache containerCache_;

        private ContainerIndexCache ContainerCache(LuaState l)
        {
            if (containerCache_ != null) return containerCache_;
            var c = new ContainerIndexCache();
            int n = l.ObjLen(1);
            for (int i = 1; i <= n; i++)
            {
                var (id, file) = ContainerEntry(l, i);
                c.Ids.Add(id);
                c.Files.Add(file);
            }
            return containerCache_ = c;
        }

        // the id and the effective file (a csharp: file through legacyPaths_) of the entry at index i of the list
        private (string id, string file) ContainerEntry(LuaState l, int i)
        {
            string id = null, file = null;
            l.RawGetI(1, i);
            if (l.IsTable(-1))
            {
                l.GetField(-1, "id");
                if (l.IsString(-1)) id = l.ToStringValue(-1);
                l.Pop(1);
                l.GetField(-1, "filename");
                if (l.IsString(-1)) file = l.ToStringValue(-1);
                l.Pop(1);
            }
            l.Pop(1);
            if (file != null && file.StartsWith("csharp:", StringComparison.Ordinal) &&
                !legacyPaths_.TryGetValue(file.Substring("csharp:".Length), out file)) file = null;
            return (id, file);
        }

        // whether the list at index 1 holds a container with this id
        private bool HasContainer(LuaState l, string containerId) => ContainerIndex(l, containerId) > 0;

        // the index in the list at index 1 of the container with this id (the first), 0 when there is none
        private int ContainerIndex(LuaState l, string containerId)
        {
            var c = ContainerCache(l);
            if (c.ById == null)
            {
                c.ById = new Dictionary<string, int>(c.Ids.Count, StringComparer.Ordinal);
                for (int i = 0; i < c.Ids.Count; i++)
                    if (c.Ids[i] != null) c.ById.TryAdd(c.Ids[i], i + 1);
            }
            return c.ById.TryGetValue(containerId, out int at) ? at : 0;
        }

        // the first index whose file sorts after legacyPath in GetContainerFilenamesList's order, past the end when none
        // does; a C# quest in the list counts by its own LegacyPath. A binary search over the entries holding a file
        private int SortedSlot(LuaState l, string legacyPath)
        {
            var c = ContainerCache(l);
            if (c.WithFile == null)
            {
                var with = new List<int>();
                for (int i = 0; i < c.Files.Count; i++)
                    if (c.Files[i] != null) with.Add(i);
                c.WithFile = with.ToArray();
            }
            int lo = 0, hi = c.WithFile.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (ScriptEngine.CompareContainerFiles(c.Files[c.WithFile[mid]], legacyPath) > 0) hi = mid;
                else lo = mid + 1;
            }
            return lo < c.WithFile.Length ? c.WithFile[lo] + 1 : c.Files.Count + 1;
        }

        // the cache after the list got the container at index at (inserted, or replacing the one there)
        private void ContainerPlaced(LuaState l, int at, bool inserted)
        {
            var c = containerCache_;
            if (c == null) return;
            var (id, file) = ContainerEntry(l, at);
            if (inserted)
            {
                c.Ids.Insert(at - 1, id);
                c.Files.Insert(at - 1, file);
            }
            else
            {
                // a replace: the same id at the same index keeps the id index, and the file indexes while the entry
                // still has a file (or still has none)
                if (c.Ids[at - 1] != id) c.ById = null;
                if ((c.Files[at - 1] != null) != (file != null)) c.WithFile = null;
                c.Ids[at - 1] = id;
                c.Files[at - 1] = file;
                return;
            }
            c.ById = null;
            c.WithFile = null;
        }

        // the container ids of the last load's list, in list order (a container with no id by its file name)
        internal List<string> ContainerOrder(LuaState l)
        {
            var ids = new List<string>();
            if (containersRef_ == null) return ids;
            int top = l.Top;
            try
            {
                l.PushRef(containersRef_);
                int n = l.ObjLen(-1);
                for (int i = 1; i <= n; i++)
                {
                    l.RawGetI(-1, i);
                    l.GetField(-1, "id");
                    string id = l.IsString(-1) ? l.ToStringValue(-1) : null;
                    l.Pop(1);
                    if (id == null)
                    {
                        l.GetField(-1, "filename");
                        id = l.IsString(-1) ? l.ToStringValue(-1) : "?";
                        l.Pop(1);
                    }
                    l.Pop(1);
                    ids.Add(id);
                }
            }
            finally
            {
                l.SetTop(top);
            }
            return ids;
        }

        // the keys of the call-only quests the switches left out in the last register (M6)
        private readonly HashSet<string> callOnlyOff_ = new HashSet<string>(StringComparer.Ordinal);

        // resolves, validates, builds and substitutes one quest; false when it is skipped or refused (logged), the Lua
        // file then in charge. name: the quest's key once known, for the caller's log line when this throws; duplicate:
        // true once the key is known to be another quest's of this pass (its exports are then left to that quest)
        private bool RegisterQuestScript(LuaState l, CompiledQuestScript q, HashSet<string> keys, ref string name, ref bool duplicate, out QuestScriptBinding bound)
        {
            bound = null;
            if (q.Script == null)
            {
                if (CSharpQuestsEnabled) log_($"quests: {q.TypeName} not registered: {q.Error}");
                return false;
            }
            var log = q.Script.Log;
            // a hidden quest (quest rework P9): no quest log or id; its key hiddenQuests.<file>, its container HQuest[name]
            bool hidden = q.Script.HiddenName != null;
            string area = q.Script.KeyArea;
            if (area == null)
            {
                if (CSharpQuestsEnabled) log_($"quests: {q.TypeName} not registered: unknown quest log {(int)log}");
                return false;
            }
            string key = q.Script.Key;
            name = key;
            bool first = keys.Add(key);
            duplicate = !first;
            if (!CSharpQuestsEnabled)
            {
                if (q.Def is { CallOnly: true }) callOnlyOff_.Add(key);
                return false;
            }
            int? id = hidden ? 0 : QuestConstId(l, area, q.Script.ConstName);
            // the Lua file's container is in the list (the file is there); without one, a refusal has no fallback
            bool hasLua = id != null && HasContainer(l, hidden ? HiddenContainerId(q.Script.HiddenName) : $"Quest[{(int)log}][{id.Value}]");
            bool Refuse(string reason, IList<string> each = null) =>
                RefuseContainer("quest", key, id == null || hasLua, reason, each);
            if (CSharpQuestsOff.Contains(key))
            {
                // an NPC-call quest has no Lua container: its Lua is the NPC script's branches. OFF leaves them alone only
                // while the NPC script still has them (before the port); after the port OFF leaves the quest unplayable,
                // and rollback is a git revert
                if (q.Def is { CallOnly: true }) callOnlyOff_.Add(key);
                if (id == null || hasLua || q.Def is { CallOnly: true }) return false;
                log_($"quests: {key} in CSHARP_QUESTS_OFF has no Lua file; it stays in C#");
            }
            if (!first)
            {
                log_($"quests: {key} not registered: another C# quest ({q.TypeName}) has the key");
                return false;
            }
            if (q.Def == null) return Refuse(q.Error);
            // the definition with an enabled module's overrides applied (validated with them), else the quest's own; a
            // patched quest is placed as any other here and moved to the end of the list after the pass (PlacePatchedLast)
            var def = WithModules(key, q.Def);
            if (id == null)
            {
                log_($"quests: {key} not registered: unknown quest constant");
                return false;
            }
            var problems = new List<string>();
            var registry = loader_?.Progress.Registry;
            // a hidden quest has no wire (no log, no id): the registry maps none
            if (!hidden && registry != null && registry.Count > 0 &&
                (!registry.TryGetRef(key, out var wire) || !wire.Equals(new QuestWireRef(log, (ushort)id.Value))))
                problems.Add("its key does not resolve to the quest in the quest key registry");
            problems.AddRange(ValidateQuestScript(def));
            // quest rework P10: a module or global another quest of this pass exports, or a global another script sets
            if (def.Exports.Count > 0) problems.AddRange(ExportProblems(l, key, def, q.Script.LegacyPath));
            if (problems.Count > 0) return Refuse(string.Join("; ", problems), each: problems);

            var binding = new QuestScriptBinding(key, def, area, id.Value);
            // its callbacks' C# functions, made on the first arming of each (ZoneHost.QuestCallbacks)
            binding.MakeCallbackRef = cb => CallbackFunction(binding, cb);
            // a module's reward: reached when the quest completes with it (the coverage hook, read at the completion)
            if (def.RewardModule is { } rewardModule)
                binding.RewardGiven = () => HandlerCalled?.Invoke(ModuleCoverageKey(rewardModule.LuaFile), $"{key} reward");
            int top = l.Top;
            try
            {
                int quest = BuildQuestContainer(l, binding);
                // every handler is an NPC-call one: only the quest object stays (its vars), the lookup gets no container
                if (def.CallOnly)
                {
                    npcCallQuests_[key] = binding;
                    bound = binding;
                    return true;
                }
                PlaceContainer(l, key, binding.ContainerId, quest, q.Script.LegacyPath);
                if (def.HasCalled) npcCallQuests_[key] = binding;
                if (modulePatches_.ContainsKey(key)) placedPatched_[key] = binding.ContainerId;
                bound = binding;
                return true;
            }
            catch (Exception e)
            {
                containerCache_ = null;
                return Refuse(e.Message);
            }
            finally
            {
                l.SetTop(top);
            }
        }

        // a built container (at index container on the stack, its key and id given) put in the list at index 1: refused
        // (throws) when a dry run through a scratch lookup fails, else at the index of the Lua container with its id,
        // or inserted at the slot of its LegacyPath (scriptLegacyPath, or the replaced Lua file's), or at the end;
        // recorded in CSharpQuests.legacy and CSharpQuests.live. The quests' and the missions' (quest rework C2)
        // placement alike
        private void PlaceContainer(LuaState l, string key, string containerId, int container, string scriptLegacyPath)
        {
            // a dry run through a scratch lookup: addContainers' loop is not protected, so a container that would fail
            // there is refused here, before it is in the list
            l.GetGlobal("InteractionLookup");
            l.GetField(-1, "new");
            l.PushValue(-2);
            l.Call(1, 1);
            int scratch = l.Top;
            l.GetField(scratch, "addContainer");
            l.PushValue(scratch);
            l.PushValue(container);
            l.Call(2, 0);

            // the Lua container with the same id, at its index (while its file exists); else the first slot past
            // LegacyPath in the files' order (table.insert shifts the rest up); else the end
            int at = ContainerIndex(l, containerId);
            bool inserted = at == 0;
            string legacy = scriptLegacyPath;
            if (at > 0)
            {
                l.RawGetI(1, at);
                l.GetField(-1, "filename");
                string file = l.IsString(-1) ? l.ToStringValue(-1) : null;
                if (legacy == null && file != null && !file.StartsWith("csharp:", StringComparison.Ordinal))
                    legacy = file;
                l.Pop(2);
            }
            else at = legacy != null ? SortedSlot(l, legacy) : l.ObjLen(1) + 1;
            if (legacy != null) legacyPaths_[key] = legacy;
            if (scriptLegacyPath != null)
            {
                l.GetGlobal("CSharpQuests");
                if (l.IsTable(-1))
                {
                    l.GetField(-1, "legacy");
                    if (l.IsTable(-1))
                    {
                        l.PushString(key);
                        l.SetField(-2, scriptLegacyPath);
                    }
                }
            }
            if (inserted)
            {
                // table.insert(list, at, quest): one call moves the entries from at up (raw, as the loop it replaces)
                l.GetGlobal("table");
                l.GetField(-1, "insert");
                l.PushValue(1);
                l.Push((double)at);
                l.PushValue(container);
                l.Call(3, 0);
                l.Pop(1);
            }
            else
            {
                l.PushValue(container);
                l.RawSetI(1, at);
            }
            ContainerPlaced(l, at, inserted);
            l.GetGlobal("CSharpQuests");
            if (l.IsTable(-1))
            {
                l.GetField(-1, "live");
                if (l.IsTable(-1))
                {
                    l.PushString(key);
                    l.SetField(-2, containerId);
                }
            }
        }

        // the quest's key (QuestScript.Key): hiddenQuests.<file> for a hidden quest, else <xi.quest.area[log]>.<const>;
        // null when the log has no area
        private static string QuestKeyOf(QuestScript s)
        {
            // a hidden quest with no LegacyPath has no key (its registration refuses it)
            if (s.HiddenName != null && string.IsNullOrEmpty(s.LegacyPath)) return null;
            return s.Key;
        }

        // a hidden quest's container id and var prefix, as HiddenQuest:new(name) makes it
        internal static string HiddenContainerId(string name) => $"HQuest[{name}]";

        // xi.quest.id[area][constName], null when it is not there
        private static int? QuestConstId(LuaState l, string area, string constName)
        {
            int top = l.Top;
            try
            {
                l.GetPath("xi.quest.id");
                if (!l.IsTable(-1)) return null;
                l.GetField(-1, area);
                if (!l.IsTable(-1)) return null;
                l.GetField(-1, constName);
                return l.IsNumber(-1) ? (int)l.ToIntegral(-1) : null;
            }
            finally
            {
                l.SetTop(top);
            }
        }

        // ---- the container -----------------------------------------------------------------------------------------

        // local quest = Quest:new(log, id) (a hidden quest's HiddenQuest:new(name), quest rework P9), with its reward and its
        // sections; leaves it on the stack and returns its index
        private int BuildQuestContainer(LuaState l, QuestScriptBinding b)
        {
            var def = b.Def;
            l.GetGlobal(def.Hidden ? "HiddenQuest" : "Quest");
            int cls = l.Top;
            l.GetField(cls, "new");
            l.PushValue(cls);
            if (def.Hidden)
            {
                l.PushString(def.HiddenName);
                l.Call(2, 1);
            }
            else
            {
                l.Push((double)(int)def.Log);
                l.Push((double)b.Id);
                l.Call(3, 1);
            }
            l.Remove(cls);
            int quest = l.Top;
            l.PushValue(quest);
            b.Container = l.Ref();
            // where the handlers come from, for whoever looks at the lookup (addContainer prints it for a container
            // without an id)
            l.PushString("csharp:" + b.Key);
            l.SetField(quest, "filename");

            if (def.Reward is { } reward)
            {
                PushRewardTable(l, reward);
                l.SetField(quest, "reward");
            }
            // an NPC-call quest has no sections
            if (def.CallOnly) return quest;

            l.NewTable();
            int sections = l.Top;
            int at = 0;
            for (int i = 0; i < def.States.Count; i++)
            {
                // a state with only NPC-call handlers has no section
                if (def.States[i].Handlers.All(def.IsCalled)) continue;
                BuildSection(l, b, quest, def.States[i], i + 1);
                l.RawSetI(sections, ++at);
            }
            l.SetField(quest, "sections");
            return quest;
        }

        // only the fields the quest sets (npcUtil.completeQuest reads item as one id or a list of them); leaves the table on the stack
        internal static void PushRewardTable(LuaState l, QuestReward r)
        {
            l.NewTable();
            void Set(string k, int? v)
            {
                if (v == null) return;
                l.Push((double)v.Value);
                l.SetField(-2, k);
            }
            Set("fame", r.Fame);
            Set("fameArea", r.FameArea);
            Set("gil", r.Gil);
            Set("title", r.Title);
            Set("keyItem", r.KeyItem);
            Set("exp", r.Exp);
            Set("bayld", r.Bayld);
            if (r.Items != null)
            {
                l.NewTable();
                for (int i = 0; i < r.Items.Length; i++)
                {
                    l.Push((double)r.Items[i]);
                    l.RawSetI(-2, i + 1);
                }
                l.SetField(-2, "item");
            }
            else Set("item", r.Item);
            if (r.Var != null)
            {
                l.PushString(r.Var);
                l.SetField(-2, "var");
            }
            PushItemParams(l, r.ItemParams);
        }

        // itemParams = { fromTrade = true }: npcUtil.giveItem's params, set on the table at the top of the stack; none
        // when no field is set (an empty table is no Lua quest's)
        internal static void PushItemParams(LuaState l, QuestItemParams ip)
        {
            if (ip == null || ip.Silent == null && ip.FromTrade == null && ip.Multiple == null) return;
            l.NewTable();
            void Flag(string k, bool? v)
            {
                if (v == null) return;
                l.Push(v.Value);
                l.SetField(-2, k);
            }
            Flag("silent", ip.Silent);
            Flag("fromTrade", ip.FromTrade);
            Flag("multiple", ip.Multiple);
            l.SetField(-2, "itemParams");
        }

        // one state as a Lua section: check, then per zone its entity tables and its zone-wide handler tables, keyed as
        // InteractionLookup's addHandlers reads them; leaves the section on the stack
        private void BuildSection(LuaState l, QuestScriptBinding b, int quest, QuestStateDecl state, int number)
        {
            l.NewTable();
            int section = l.Top;
            // a mission's section check reads its own arguments (quest rework C2, ZoneHost.MissionScripts)
            l.PushFunction(b.Mission != null ? MissionSectionCheck(b, state, number) : SectionCheck(b, state, number));
            l.SetField(section, "check");

            var handlers = state.Handlers.Where(h => !b.Def.IsCalled(h)).ToList();
            foreach (var zoneId in handlers.Select(h => h.Zone).Distinct())
            {
                l.Push((double)zoneId);
                l.NewTable();
                int zone = l.Top;
                var inZone = handlers.Where(h => h.Zone == zoneId).ToList();

                // ['Michea'] = { onTrigger = ..., onTrade = ... }; a lone fixed onTrigger is the bare action, as the Lua
                // quests write ['Michea'] = quest:progressEvent(215) (addHandlers makes it the onTrigger)
                foreach (var entity in inZone.Where(h => h.Entity != null).Select(h => h.Entity).Distinct())
                {
                    var hs = inZone.Where(h => h.Entity == entity).ToList();
                    if (hs.Count == 1 && hs[0].Kind == QuestHandlerKind.OnTrigger && hs[0].Fixed != null)
                    {
                        PushHandler(l, quest, b, hs[0]);
                        l.SetField(zone, entity);
                        continue;
                    }
                    l.NewTable();
                    foreach (var h in hs)
                    {
                        PushHandler(l, quest, b, h);
                        l.SetField(-2, KindName(h.Kind));
                    }
                    l.SetField(zone, entity);
                }

                // onEventFinish = { [csid] = fn }, onZoneIn = { [1] = fn }, ...
                foreach (var kind in inZone.Where(h => h.Entity == null).Select(h => h.Kind).Distinct())
                {
                    l.NewTable();
                    foreach (var h in inZone.Where(h => h.Entity == null && h.Kind == kind))
                    {
                        l.Push((double)h.Id);
                        PushHandler(l, quest, b, h);
                        l.SetTable(-3);
                    }
                    l.SetField(zone, KindName(kind));
                }
                l.SetTable(section);
            }
        }

        private static string KindName(QuestHandlerKind kind) => kind switch
        {
            QuestHandlerKind.OnTrigger => "onTrigger",
            QuestHandlerKind.OnTrade => "onTrade",
            QuestHandlerKind.OnMobDeath => "onMobDeath",
            QuestHandlerKind.OnEventFinish => "onEventFinish",
            QuestHandlerKind.OnEventUpdate => "onEventUpdate",
            QuestHandlerKind.OnZoneIn => "onZoneIn",
            QuestHandlerKind.OnTriggerAreaEnter => "onTriggerAreaEnter",
            // interaction_lookup's key (InteractionLookup:afterZoneIn)
            QuestHandlerKind.OnAfterZoneIn => "afterZoneIn",
            // InteractionLookup:onZoneOut's (quest rework P7)
            QuestHandlerKind.OnZoneOut => "onZoneOut",
            // InteractionLookup:onTriggerAreaLeave's (quest rework P9)
            QuestHandlerKind.OnTriggerAreaLeave => "onTriggerAreaLeave",
            // a kind with no key here would sit under a key the lookup never reads (its handler never run, silently): the
            // quest is refused instead (BuildSection throws, InitScripts logs it), until the kind is named
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "a handler kind the C# quest bridge does not know"),
        };

        // a valid zone-in reply as luautils::OnZoneIn reads it (quest rework P8): the cutscene id as a number, or, when the
        // reply sets text_table or flags (QuestReply.Event(956).TextTable(0)), the table { id, textTable, flags } (Lua's
        // return { 956, 0 } / { 1101, -1, flags }), OnZoneIn's defaults (-1, 0) for the one not set
        internal static void PushZoneInReply(LuaState l, QuestReply reply)
        {
            if (QuestScriptValidator.ZoneInTable(reply) is not { } t)
            {
                l.Push((double)reply.Id);
                return;
            }
            l.NewTable(3, 0);
            l.Push((double)t.Id);
            l.RawSetI(-2, 1);
            l.Push((double)t.TextTable);
            l.RawSetI(-2, 2);
            l.Push((double)t.Flags);
            l.RawSetI(-2, 3);
        }

        // the handler's name in the log lines: "Michea onTrade", "onEventFinish 216", "onZoneIn", "afterZoneIn", "onZoneOut"
        private static string HandlerName(QuestHandlerDecl h) =>
            h.Entity != null ? $"{h.Entity} {KindName(h.Kind)}"
            : h.Kind is QuestHandlerKind.OnZoneIn or QuestHandlerKind.OnAfterZoneIn or QuestHandlerKind.OnZoneOut ? KindName(h.Kind) : $"{KindName(h.Kind)} {h.Id}";

        // pushes the handler's value: a fixed reply's action, built once as the Lua file builds it, or a closure; a fixed
        // reply that cannot be built (an unknown text name) throws, which refuses the quest. With HandlerCalled set, the
        // fixed action is behind a closure that reports each run and returns that same action
        private void PushHandler(LuaState l, int quest, QuestScriptBinding b, QuestHandlerDecl h)
        {
            if (h.Fixed == null)
            {
                l.PushFunction(HandlerClosure(b, h));
                return;
            }
            if (PushReply(l, quest, h.Fixed, h.Zone) is { } error) throw new InvalidOperationException($"{HandlerName(h)}: {error}");
            if (HandlerCalled == null) return;
            var action = l.Ref();
            l.PushFunction(fl =>
            {
                ReportHandler(b, h);
                fl.PushRef(action);
                return 1;
            });
        }

        // ---- handler labels ------------------------------------------------------------------------------------------

        // every declared handler's label of the C# quest with the key, in declaration order; null when no C# quest has it
        public IReadOnlyList<string> HandlerLabels(string questKey)
        {
            if (questKey == ContainerKeys.RoeCoverage) return RoeOnLua ? null : RoeChecks.CoverageLabels();
            foreach (var q in questScripts_.Concat(TestQuestScripts.OfType<QuestScript>().Select(CompiledTest)))
            {
                if (q.Def == null) continue;
                string key;
                try
                {
                    key = QuestKeyOf(q.Script);
                }
                catch (Exception)
                {
                    continue;
                }
                if (key == questKey) return HandlerLabels(q.Def);
            }
            // quest rework C2: a mission's, by its key, or a battlefield's coverage labels
            return MissionHandlerLabels(questKey) ?? BattlefieldHandlerLabels(questKey);
        }

        // a test quest compiled once per instance (HandlerLabels asks per replayed flow)
        private readonly Dictionary<QuestScript, CompiledQuestScript> compiledTests_ = new Dictionary<QuestScript, CompiledQuestScript>(ReferenceEqualityComparer.Instance);
        private CompiledQuestScript CompiledTest(QuestScript s) => compiledTests_.TryGetValue(s, out var q) ? q : compiledTests_[s] = Compiled(s);

        // the definition's handler labels, one per handler, in declaration order, then its callbacks'
        // (quest rework P10: 0/-/callback:<name>, in declaration order), which coverage counts as handlers
        internal static IReadOnlyList<string> HandlerLabels(QuestScriptDefinition def) =>
            LabelsOf(def).Select(x => x.Label).Concat(def.Callbacks.Select(CallbackLabel)).ToList();

        // each handler with its label: <state number from 1>/<entity, '-' for a state-level one>/<trigger>, the trigger an
        // event's or trigger area's carrying its id (onEventFinish:216, onTriggerAreaEnter:3). Handlers of the quest that
        // would share a label (the same one in two zones) each get @<the zone's Zone constant name> (2/-/onZoneIn@PortBastok)
        internal static List<(QuestHandlerDecl Handler, string Label)> LabelsOf(QuestScriptDefinition def)
        {
            var plain = new List<(QuestHandlerDecl Handler, string Label)>();
            for (int i = 0; i < def.States.Count; i++)
                foreach (var h in def.States[i].Handlers)
                {
                    string trigger = KindName(h.Kind);
                    if (h.Kind is QuestHandlerKind.OnEventFinish or QuestHandlerKind.OnEventUpdate or QuestHandlerKind.OnTriggerAreaEnter or QuestHandlerKind.OnTriggerAreaLeave) trigger += $":{h.Id}";
                    plain.Add((h, $"{i + 1}/{h.Entity ?? "-"}/{trigger}"));
                }
            var shared = plain.GroupBy(x => x.Label).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
            if (shared.Count == 0) return plain;
            return plain.Select(x => shared.Contains(x.Label) ? (x.Handler, $"{x.Label}@{ZoneConstName(x.Handler.Zone)}") : x).ToList();
        }

        // zone id -> the generated Zone constant's name (XI.QuestContent.Zone), read once
        private static Dictionary<ushort, string> zoneConstNames_;

        // the zone's Zone constant name, its number when it has none
        internal static string ZoneConstName(ushort zone)
        {
            var names = zoneConstNames_;
            if (names == null)
            {
                names = new Dictionary<ushort, string>();
                foreach (var f in typeof(global::XI.QuestContent.Zone).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
                    if (f.IsLiteral) names.TryAdd(Convert.ToUInt16(f.GetRawConstantValue()), f.Name);
                zoneConstNames_ = names;
            }
            return names.TryGetValue(zone, out var name) ? name : zone.ToString();
        }

        // ---- closures ----------------------------------------------------------------------------------------------

        // check(player, status, vars, varCache): the state's match; a check that throws is false. A hidden quest's check
        // (quest rework P9) is check(player, vars, varCache): HiddenQuest has no getCheckArgs, so the lookup passes no status
        // (its states are AnyState: the validator refuses a status match; the context's Status throws). The context reads the
        // quest's vars (Step, Var) through vars, the lookup's per-dispatch cache, as a Lua check does: a handler run
        // earlier in the same dispatch (onEventFinish runs each matching entry's handler before the next entry's check)
        // has changed the var, and the later check still sees the value from before. An any-status match (AnyState) does
        // not compare the status, as a Lua check of `vars.Prog == 2` alone
        private LuaFunction SectionCheck(QuestScriptBinding b, QuestStateDecl state, int number)
        {
            bool readsStep = state.Matches.Any(m => m.Step.HasValue);
            string name = $"check {number}";
            return l =>
            {
                int top = l.Top;
                bool result = false;
                try
                {
                    bool hidden = b.Def.Hidden;
                    var status = hidden ? global::XI.Quests.QuestStatus.Available : (QuestStatus)(int)l.ToIntegral(2);
                    int vars = hidden ? 2 : 3;
                    var ctx = new LiveQuestContext(l, log_, b, name, player: 1, status: hidden ? null : status, vars: l.IsTable(vars) ? vars : 0, check: true);
                    int step = 0;
                    // only when a match of this status (or of any) names a step, as Lua's `status == ACCEPTED and
                    // vars.Prog == 0` reads the var only for that status
                    if (readsStep && state.Matches.Any(m => (m.AnyStatus || m.Status == status) && m.Step.HasValue))
                    {
                        // vars.Prog through the lookup's container var cache, or 0
                        step = ctx.Step;
                    }
                    result = state.IsMatch(status, step, ctx);
                    ReportRemoved(b, status, ctx);
                }
                catch (Exception e)
                {
                    log_($"quests: {b.Key} {name}: {e.Message}");
                    result = false;
                }
                finally
                {
                    l.SetTop(top);
                }
                l.Push(result);
                return 1;
            };
        }

        // the handler as the Lua function the lookup runs: a context over the call's arguments, the C# handler, and its
        // reply turned into the Lua action (or nothing); a handler that throws is logged and returns nothing
        private LuaFunction HandlerClosure(QuestScriptBinding b, QuestHandlerDecl h)
        {
            string name = HandlerName(h);
            return l =>
            {
                int top = l.Top;
                int keep = 0;
                try
                {
                    var ctx = h.Kind switch
                    {
                        // (player, npc), (player, npc, trade)
                        QuestHandlerKind.OnTrigger => new LiveQuestContext(l, log_, b, name, player: 1, npc: 2, zone: h.Zone),
                        QuestHandlerKind.OnTrade => new LiveQuestContext(l, log_, b, name, player: 1, npc: 2, trade: 3, zone: h.Zone),
                        // (mob, player, optParams)
                        QuestHandlerKind.OnMobDeath => new LiveQuestContext(l, log_, b, name, player: 2, mob: 1, mobDeath: 3, zone: h.Zone),
                        // (player, csid, option, npc)
                        QuestHandlerKind.OnEventFinish or QuestHandlerKind.OnEventUpdate =>
                            new LiveQuestContext(l, log_, b, name, player: 1, npc: 4, zone: h.Zone,
                                                 csid: l.IsNumber(2) ? (int)l.ToIntegral(2) : 0, option: l.IsNumber(3) ? (int)l.ToIntegral(3) : 0,
                                                 optionText: l.IsString(3) ? l.ToStringValue(3) : null),
                        // (player, triggerArea), for an enter and a leave (P9) alike
                        QuestHandlerKind.OnTriggerAreaEnter or QuestHandlerKind.OnTriggerAreaLeave =>
                            new LiveQuestContext(l, log_, b, name, player: 1, triggerArea: 2, zone: h.Zone),
                        // (player, prevZone) for onZoneIn, (player) for afterZoneIn and onZoneOut (a Run handler: it
                        // returns nothing, as the Lua function returns nil)
                        _ => new LiveQuestContext(l, log_, b, name, player: 1, zone: h.Zone),
                    };
                    if (!RunHandler(b, h, name, ctx, out var reply) || reply == null) return 0;
                    if (h.Kind == QuestHandlerKind.OnZoneIn)
                    {
                        // onZoneIn gives the engine a cutscene id (or { id, textTable, flags }), not an action; a reply
                        // with arguments is refused rather than sent without them, and one with modifiers, or a named
                        // field the engine does not read there, likewise
                        if (QuestScriptValidator.ZoneInReplyProblem(reply) is { } problem)
                        {
                            log_($"quests: {b.Key} {name}: {problem}");
                            return 0;
                        }
                        PushZoneInReply(l, reply);
                    }
                    else
                    {
                        // a trigger area's action, and an afterZoneIn's, is performed with no NPC: a text the NPC would
                        // show is refused
                        if (h.Kind is QuestHandlerKind.OnTriggerAreaEnter or QuestHandlerKind.OnTriggerAreaLeave && QuestScriptValidator.TriggerAreaReplyProblem(reply) is { } areaProblem)
                        {
                            log_($"quests: {b.Key} {name}: {areaProblem}");
                            return 0;
                        }
                        if (h.Kind == QuestHandlerKind.OnAfterZoneIn && QuestScriptValidator.AfterZoneInReplyProblem(reply) is { } afterProblem)
                        {
                            log_($"quests: {b.Key} {name}: {afterProblem}");
                            return 0;
                        }
                        l.PushRef(b.Container);
                        int quest = l.Top;
                        if (PushReply(l, quest, reply, h.Zone) is { } error)
                        {
                            log_($"quests: {b.Key} {name}: {error}");
                            return 0;
                        }
                    }
                    l.Insert(top + 1);
                    keep = 1;
                    return 1;
                }
                catch (Exception e)
                {
                    string line = $"quests: {b.Key} {name}: {e.Message}";
                    log_(line);
                    HandlerThrew?.Invoke(line);
                    keep = 0;
                    return 0;
                }
                finally
                {
                    l.SetTop(top + keep);
                }
            };
        }

        // the one runner of a handler, the container's and the NPC call's: Run, or the reply of the fixed action / Reply;
        // false (logged) when the handler threw. reply: null for a Run handler and for no reply
        private bool RunHandler(QuestScriptBinding b, QuestHandlerDecl h, string name, LiveQuestContext ctx, out QuestReply reply)
        {
            reply = null;
            ReportHandler(b, h);
            if (h.Kind == QuestHandlerKind.OnEventFinish && finishOpen_)
            {
                finishRuns_ ??= new Dictionary<string, List<string>>(StringComparer.Ordinal);
                if (!finishRuns_.TryGetValue(b.Key, out var labels)) finishRuns_[b.Key] = labels = new List<string>();
                labels.Add(b.Label(h));
            }
            try
            {
                if (h.Run != null) h.Run(ctx);
                else reply = h.Fixed ?? h.Reply?.Invoke(ctx);
                return true;
            }
            catch (Exception e)
            {
                string line = $"quests: {b.Key} {name}: {e.Message}";
                log_(line);
                HandlerThrew?.Invoke(line);
                reply = null;
                return false;
            }
        }

        // ---- replies -----------------------------------------------------------------------------------------------

        // the reply as the Lua action: the container's helper called with the same arguments (quest:progressEvent(216),
        // quest:messageSpecial(<the zone's text id>, ...)); null when pushed, else why not (nothing pushed): a text name
        // unknown in the zone, a sequence step that is not a plain message, an argument the engine would drop (a boolean or
        // a string where it reads a number: QuestScriptValidator.ArgsProblem)
        private string PushReply(LuaState l, int quest, QuestReply r, ushort zone)
        {
            if ((QuestScriptValidator.SequenceProblem(r) ?? QuestScriptValidator.ArgsProblem(r)) is { } problem) return problem;
            int top = l.Top;
            string method = r.Kind switch
            {
                QuestReplyKind.Event => "event",
                QuestReplyKind.ProgressEvent => "progressEvent",
                QuestReplyKind.PriorityEvent => "priorityEvent",
                QuestReplyKind.Cutscene => "cutscene",
                QuestReplyKind.ProgressCutscene => "progressCutscene",
                QuestReplyKind.ProgressOptionalCutscene => "progressOptionalCutscene",
                QuestReplyKind.ReplaceEvent => "replaceEvent",
                QuestReplyKind.Message => "message",
                QuestReplyKind.MessageSpecial => "messageSpecial",
                QuestReplyKind.MessageText => "messageText",
                QuestReplyKind.MessageName => "messageName",
                QuestReplyKind.ReplaceMessage => "replaceMessage",
                QuestReplyKind.KeyItem => "keyItem",
                QuestReplyKind.Sequence => "sequence",
                _ => "noAction",
            };
            l.GetField(quest, method);
            l.PushValue(quest);
            int n = 1;
            switch (r.Kind)
            {
                case QuestReplyKind.Message:
                case QuestReplyKind.ReplaceMessage:
                case QuestReplyKind.MessageSpecial:
                case QuestReplyKind.MessageText:
                case QuestReplyKind.MessageName:
                    int? text = ReplyTextId(l, r, zone);
                    if (text == null)
                    {
                        l.SetTop(top);
                        return UnknownText(r, zone);
                    }
                    l.Push((double)text.Value);
                    n++;
                    // message(id, messageType, ...) and replaceMessage(id, messageType, ...)
                    if (r.Kind is QuestReplyKind.Message or QuestReplyKind.ReplaceMessage)
                    {
                        if (r.MessageType is { } type) l.Push((double)type); else l.PushNil();
                        n++;
                    }
                    break;
                case QuestReplyKind.Sequence:
                    // sequence({ text = id }, { text = id, face = angle }, { face = angle }, { wait = ms }, ...): each step one
                    // entry of the table; a text step with a face (WithFace) is one entry, as Sequence:new makes it one
                    // Message with face
                    foreach (var step in r.Steps)
                    {
                        if (step.Kind is QuestReplyKind.Face or QuestReplyKind.Wait)
                        {
                            l.NewTable();
                            l.Push((double)step.Id);
                            l.SetField(-2, step.Kind == QuestReplyKind.Face ? "face" : "wait");
                            n++;
                            continue;
                        }
                        int? id = ReplyTextId(l, step, zone);
                        if (id == null)
                        {
                            l.SetTop(top);
                            return UnknownText(step, zone);
                        }
                        l.NewTable();
                        l.Push((double)id.Value);
                        l.SetField(-2, "text");
                        if (step.TextFace is int face)
                        {
                            l.Push((double)face);
                            l.SetField(-2, "face");
                        }
                        n++;
                    }
                    break;
                case QuestReplyKind.NoAction:
                    break;
                default:
                    // the event id, the key item
                    l.Push((double)r.Id);
                    n++;
                    break;
            }
            // startEvent takes its parameters as positions or as one table { [index] = value } (ParseEvent reads a table in
            // the first place only), so a reply with options sends one table: the positional arguments at indexes 0.., the
            // options over them, as a Lua quest writes quest:progressEvent(67, { [0] = a, [6] = x })
            if (r.Kind != QuestReplyKind.Sequence) n += PushReplyArgs(l, r);
            l.Call(n, 1);
            // the Action modifiers, called on the helper's action in order as Lua chains them
            // (quest:event(222):oncePerZone()); each returns the action itself
            foreach (var m in r.Modifiers)
            {
                if (!l.IsTable(-1))
                {
                    l.SetTop(top);
                    return $"{r.Kind} built no action for {m}";
                }
                l.GetField(-1, m.LuaName);
                l.PushValue(-2);
                int margs = 1;
                if (m.Arg is int arg)
                {
                    l.Push((double)arg);
                    margs++;
                }
                l.Call(margs, 1);
                l.Remove(-2);
            }
            return null;
        }

        // the reply's remaining arguments: its option table when it has options (or event strings, or named fields), else its
        // arguments; returns how many values
        private static int PushReplyArgs(LuaState l, QuestReply r)
        {
            if (r.Options.Count > 0 || r.StringOptions.Count > 0 || r.EventFields.Count > 0)
            {
                var table = new SortedDictionary<int, QuestArg>();
                for (int i = 0; i < r.Args.Count; i++) table[i] = r.Args[i];
                foreach (var o in r.Options) table[o.Key] = o.Value;
                l.NewTable();
                foreach (var (index, value) in table)
                {
                    l.Push((double)index);
                    PushArg(l, value);
                    l.SetTable(-3);
                }
                // ['strings'] = { [index] = text }: the event's strings, as ParseEvent reads them
                if (r.StringOptions.Count > 0)
                {
                    l.NewTable();
                    foreach (var (index, text) in r.StringOptions.OrderBy(kv => kv.Key))
                    {
                        l.Push((double)index);
                        l.PushString(text);
                        l.SetTable(-3);
                    }
                    l.SetField(-2, "strings");
                }
                // the named fields ParseEvent reads: text_table = 0, canSkip = true, cs_option = { 1, 2 }, ...
                foreach (var f in r.EventFields)
                {
                    switch (f.Kind)
                    {
                        case QuestEventFieldKind.Bool: l.Push(f.Bool); break;
                        case QuestEventFieldKind.List:
                            l.NewTable();
                            for (int i = 0; i < f.List.Count; i++)
                            {
                                l.Push((double)f.List[i]);
                                l.RawSetI(-2, i + 1);
                            }
                            break;
                        default: l.Push((double)f.Number); break;
                    }
                    l.SetField(-2, f.Name);
                }
                return 1;
            }
            foreach (var a in r.Args) PushArg(l, a);
            return r.Args.Count;
        }

        // a reply argument as the Lua value the helper gets: a number (as every argument was before QuestArg), a boolean,
        // a string
        internal static void PushArg(LuaState l, QuestArg a)
        {
            switch (a.Kind)
            {
                case QuestArgKind.Bool: l.Push(a.Bool); break;
                case QuestArgKind.String: l.PushString(a.String); break;
                default: l.Push((double)a.Number); break;
            }
        }

        // a reply's text id: its ZoneText in its own zone, its TextName in the handler's zone, else the id it carries
        internal static int? ReplyTextId(LuaState l, QuestReply r, ushort zone) =>
            r.Text is { } t ? LiveQuestContext.TextId(l, t.Zone, t.Name)
            : r.TextName != null ? LiveQuestContext.TextId(l, zone, r.TextName) : r.Id;

        internal static string UnknownText(QuestReply r, ushort zone) =>
            r.Text is { } t ? $"unknown text {t.Name} in zone {t.Zone}" : $"unknown text {r.TextName} in zone {zone}";
    }

    // one registration of a C# quest: its key, its definition, its Lua identity and its Lua container
    internal sealed class QuestScriptBinding
    {
        public QuestScriptBinding(string key, QuestScriptDefinition def, string area, int id)
        {
            Key = key;
            Def = def;
            Area = area;
            Id = id;
        }

        public string Key { get; }
        public QuestScriptDefinition Def { get; }
        // called each time the quest completes with its reward (the coverage hook of a module's Reward); null when none
        public Action RewardGiven { get; set; }
        // xi.quest.area[log] (QuestScript.AreaName), hiddenQuests for a hidden quest
        public string Area { get; }
        // xi.quest.id[area][const]
        public int Id { get; }
        // Quest[<log>][<id>], the container id and var prefix; a hidden quest's HQuest[<name>] (quest rework P9); a
        // mission's Mission[<log>][<id>] (quest rework C2)
        public string ContainerId =>
            Mission != null ? $"Mission[{(int)Mission.Log}][{Id}]"
            : Def.Hidden ? ZoneHost.HiddenContainerId(Def.HiddenName) : $"Quest[{(int)Def.Log}][{Id}]";
        // a mission's definition (quest rework C2): Def is then its Core, Area xi.mission.area[log] and Id
        // xi.mission.id[area][const]; null for a quest
        public MissionScriptDefinition Mission { get; init; }
        // the Lua quest table (a registry reference)
        public LuaRef Container { get; set; }

        // quest rework P10: makes a callback's C# function (ZoneHost.CallbackFunction); each made once, on its first
        // arming
        public Func<QuestCallback, LuaRef> MakeCallbackRef { get; set; }
        private Dictionary<QuestCallback, LuaRef> callbackRefs_;

        // the callback's C# function (a registry reference), made on the first ask
        public LuaRef CallbackRef(QuestCallback cb)
        {
            callbackRefs_ ??= new Dictionary<QuestCallback, LuaRef>(ReferenceEqualityComparer.Instance);
            if (!callbackRefs_.TryGetValue(cb, out var r))
            {
                if (MakeCallbackRef == null) throw new InvalidOperationException($"{Key}: callbacks cannot be armed here");
                callbackRefs_[cb] = r = MakeCallbackRef(cb);
            }
            return r;
        }

        // handler -> label, built on the first ask (only the coverage hook asks)
        private Dictionary<QuestHandlerDecl, string> labels_;

        // the handler's label (ZoneHost.LabelsOf)
        public string Label(QuestHandlerDecl h)
        {
            if (labels_ == null)
            {
                labels_ = new Dictionary<QuestHandlerDecl, string>(ReferenceEqualityComparer.Instance);
                foreach (var (handler, label) in ZoneHost.LabelsOf(Def)) labels_[handler] = label;
            }
            return labels_[h];
        }
    }

    // the quest context over the engine, for one call of a check or handler: IQuestContext for a handler and IQuestCheck
    // for a check, and every facet (Player, Quest, Trade, Text, Settings) is this object, Npc a small one over the npc
    // argument (npc:getID, npc:getLocalVar, npc:setLocalVar). Its quest members call the container's own
    // Lua methods with the player (quest:getVar, quest:begin, quest:complete, ...), its player members the player's
    // bindings, and its trade and effect members the npcUtil helpers the Lua quests call, so a C# quest sends the packets
    // and writes the rows its Lua twin does. Arguments are positions on the call's Lua stack (0: none); every call into
    // Lua is protected and leaves the stack as it found it. In a check the vars come through the dispatch cache and
    // every effect throws.
    // Each reload(true) builds the C# quests anew, and LuaState.functions_ (the registered closures, among them every
    // check and handler) grows with each one: they are not freed. A development path only.
    internal sealed partial class LiveQuestContext : IQuestContext, IQuestCheck, IQuestPlayer, IQuestProgress, IQuestTrade, IQuestText, IQuestSettings
    {
        private readonly LuaState lua_;
        private readonly Action<string> log_;
        private readonly QuestScriptBinding quest_;
        private readonly string handler_;
        private readonly int player_, npc_, trade_, mob_, mobDeath_, triggerArea_, vars_;
        private readonly bool check_, called_;
        private readonly QuestStatus? status_;
        private ushort? zone_;

        internal LiveQuestContext(LuaState lua, Action<string> log, QuestScriptBinding quest, string handler, int player = 0, int npc = 0,
                                  int trade = 0, int mob = 0, int triggerArea = 0, QuestStatus? status = null, ushort? zone = null, int csid = 0,
                                  int option = 0, int vars = 0, bool check = false, bool called = false, string optionText = null,
                                  int mobDeath = 0, int? missionCurrent = null, int? missionStatus = null)
        {
            missionCurrent_ = missionCurrent;
            missionStatus_ = missionStatus;
            OptionText = optionText;
            called_ = called;
            lua_ = lua;
            log_ = log;
            quest_ = quest;
            handler_ = handler;
            player_ = player;
            npc_ = npc;
            trade_ = trade;
            mob_ = mob;
            mobDeath_ = mobDeath;
            triggerArea_ = triggerArea;
            vars_ = vars;
            check_ = check;
            status_ = status;
            zone_ = zone;
            Csid = csid;
            Option = option;
        }

        // ---- the facets ----

        public IQuestPlayer Player => this;
        // a mission's context has no quest (quest rework C2): its own is Mission
        public IQuestProgress Quest =>
            quest_.Mission != null ? throw new InvalidOperationException(MissionNoQuest) : this;
        private const string MissionNoQuest = "a mission's context has no quest (q.Quest): its own is q.Mission";
        public IQuestTrade Trade => this;
        public IQuestText Text => this;
        public IQuestSettings Settings => this;
        // the npc argument, null when the call has none (zone-in, trigger area, mob death, an event without an npc)
        public IQuestNpc Npc => npc_ == 0 || lua_.IsNil(npc_) ? null : npcFacet_ ??= new LiveQuestNpc(this, npc_);
        // the mob argument (an OnMobDeath's dying mob), null elsewhere: mob:getID(), mob:getLocalVar(name)
        public IQuestNpc Mob => mob_ == 0 || lua_.IsNil(mob_) ? null : mobFacet_ ??= new LiveQuestNpc(this, mob_);
        // the optParams argument (an OnMobDeath's third; an NPC call's fifth, when its mob script passes it), null elsewhere
        // and when it is not a table (quest rework P7)
        public IQuestMobDeath MobDeath => mobDeath_ == 0 || !lua_.IsTable(mobDeath_) ? null : mobDeathFacet_ ??= new LiveMobDeath(this, mobDeath_);
        private LiveMobDeath mobDeathFacet_;
        IQuestPlayerView IQuestCheck.Player => this;
        IQuestProgressView IQuestCheck.Quest => Quest;
        private LiveQuestNpc npcFacet_, mobFacet_;

        // a check reads only
        private void NoEffect(string what)
        {
            if (check_) throw new InvalidOperationException($"a check cannot {what}");
        }

        // ---- the quest ----

        // the status the lookup passed the check, else player:getQuestStatus(log, id); a hidden quest has none (P9)
        public QuestStatus Status => quest_.Def.Hidden ? throw new InvalidOperationException(HiddenNo("status"))
            : status_ ?? (QuestStatus)PlayerCall("getQuestStatus", 1, () => (int)lua_.ToIntegral(-1), (int)quest_.Def.Log, quest_.Id);
        // a hidden quest (HiddenQuest:new) has no quest log or id: no status, no begin, no drop
        private static string HiddenNo(string what) => $"a hidden quest has no {what} (HiddenQuest:new has no quest log or id)";
        public int Step => Var("Prog");
        public void SetStep(int step) => SetVar("Prog", step);
        // in a check, vars[name] (the lookup's cache over quest:getVar); in a handler, quest:getVar live, as in Lua
        public int Var(string name) => vars_ != 0
            ? Protected(() =>
            {
                lua_.GetGlobal("CSharpQuests");
                lua_.GetField(-1, "get");
                lua_.PushValue(vars_);
                lua_.PushString(name);
                lua_.Call(2, 1);
                return lua_.IsNumber(-1) ? (int)lua_.ToIntegral(-1) : 0;
            })
            : QuestCall("getVar", 1, true, () => (int)lua_.ToIntegral(-1), name);
        // an expiry of 0 is no expiry (nil)
        public void SetVar(string name, int value, uint expiry = 0)
        {
            NoEffect("set a var");
            QuestCall("setVar", 0, true, () => 0, name, value, expiry == 0 ? null : (object)(double)expiry);
        }
        public bool IsVarBitsSet(string name, params int[] bits)
        {
            var args = new object[bits.Length + 1];
            args[0] = name;
            for (int i = 0; i < bits.Length; i++) args[i + 1] = bits[i];
            return QuestCall("isVarBitsSet", 1, true, () => lua_.ToBoolean(-1), args);
        }
        public void SetVarBit(string name, int bit)
        {
            NoEffect("set a var bit");
            QuestCall("setVarBit", 0, true, () => 0, name, bit);
        }
        public void UnsetVarBit(string name, int bit)
        {
            NoEffect("unset a var bit");
            QuestCall("unsetVarBit", 0, true, () => 0, name, bit);
        }
        // quest:incrementVar(player, name, value)
        public void IncrementVar(string name, int value)
        {
            NoEffect("increment a var");
            QuestCall("incrementVar", 0, true, () => 0, name, value);
        }
        public bool MustZone => QuestCall("getMustZone", 1, true, () => lua_.ToBoolean(-1));
        // quest:getLocalVar(player, name) / quest:setLocalVar(player, name, value): the player's local var under the
        // container's varPrefix
        int IQuestProgressView.LocalVar(string name) => QuestCall("getLocalVar", 1, true, () => (int)lua_.ToIntegral(-1), name);
        void IQuestProgress.SetLocalVar(string name, int value)
        {
            NoEffect("set a local var");
            QuestCall("setLocalVar", 0, true, () => 0, name, value);
        }
        public void SetMustZone()
        {
            NoEffect("set mustZone");
            QuestCall("setMustZone", 0, true, () => 0);
        }
        // player:delCurrentQuest(log, id), in a container and an NPC call alike
        public void DelCurrent()
        {
            NoEffect("drop the quest");
            if (quest_.Def.Hidden) throw new InvalidOperationException(HiddenNo("current quest to drop"));
            PlayerCall("delCurrentQuest", 0, () => 0, (int)quest_.Def.Log, quest_.Id);
        }
        // quest:cleanup(player): the quest's Quest[log][id] vars cleared, the same call a called Complete() makes
        public void Cleanup()
        {
            NoEffect("clean up the quest's vars");
            QuestCall("cleanup", 0, true, () => 0);
        }
        // an NPC call: player:addQuest(log, id), as an NPC script writes it
        public void Begin()
        {
            NoEffect("begin the quest");
            if (quest_.Def.Hidden) throw new InvalidOperationException(HiddenNo("begin"));
            if (called_) PlayerCall("addQuest", 0, () => 0, (int)quest_.Def.Log, quest_.Id);
            else QuestCall("begin", 0, true, () => 0);
        }
        // quest:complete(player): npcUtil.completeQuest with the reward, the vars cleaned up when it completed (a hidden
        // quest's HiddenQuest:complete: npcUtil.giveReward, then the cleanup when it gave it; P9). An NPC call:
        // npcUtil.completeQuest(player, log, id, reward) with the reward, else player:completeQuest(log, id), then the same
        // quest:cleanup(player) (a no-op for a script that sets no Quest[log][id] vars)
        public bool Complete()
        {
            NoEffect("complete the quest");
            if (!called_)
            {
                bool completed = QuestCall("complete", 1, true, () => lua_.ToBoolean(-1));
                if (completed) quest_.RewardGiven?.Invoke();
                return completed;
            }
            bool done;
            if (quest_.Def.Reward is not { } reward)
            {
                PlayerCall("completeQuest", 0, () => 0, (int)quest_.Def.Log, quest_.Id);
                done = true;
            }
            else
                done = Protected(() =>
                {
                    lua_.GetPath("npcUtil.completeQuest");
                    PushPlayer();
                    lua_.Push((double)(int)quest_.Def.Log);
                    lua_.Push((double)quest_.Id);
                    ZoneHost.PushRewardTable(lua_, reward);
                    lua_.Call(4, 1);
                    return lua_.ToBoolean(-1);
                });
            if (done)
            {
                QuestCall("cleanup", 0, true, () => 0);
                quest_.RewardGiven?.Invoke();
            }
            return done;
        }

        // ---- the call ----

        // the handler's zone, else the player's
        public ushort Zone => zone_ ??= (ushort)PlayerCall("getZoneID", 1, () => (int)lua_.ToIntegral(-1));
        public int Csid { get; }
        public int Option { get; }
        // the option when the call got a Lua string (type(option) == 'string'), else null
        public string OptionText { get; }
        // triggerArea:getTriggerAreaID()
        public int TriggerAreaId
        {
            get
            {
                if (triggerArea_ == 0 || lua_.IsNil(triggerArea_)) throw new InvalidOperationException("no trigger area in this handler");
                return Method(triggerArea_, "getTriggerAreaID", 1, () => (int)lua_.ToIntegral(-1));
            }
        }

        // ---- the player ----

        public int FameLevel(int fameArea) => PlayerCall("getFameLevel", 1, () => (int)lua_.ToIntegral(-1), fameArea);
        // player:hasCompletedQuest(log, xi.quest.id[area][constName])
        public bool HasCompletedQuest(QuestLog log, string constName)
        {
            int id = QuestId(log, constName);
            return PlayerCall("hasCompletedQuest", 1, () => lua_.ToBoolean(-1), (int)log, id);
        }
        public bool HasKeyItem(int keyItem) => PlayerCall("hasKeyItem", 1, () => lua_.ToBoolean(-1), keyItem);
        // player:hasCompletedMission(missionLog, mission)
        public bool HasCompletedMission(int missionLog, int mission) =>
            PlayerCall("hasCompletedMission", 1, () => lua_.ToBoolean(-1), missionLog, mission);
        // xi.maws.hasUnlockedMaw(player, zone)
        public bool HasUnlockedMaw(int zone) => Protected(() =>
        {
            lua_.GetPath("xi.maws.hasUnlockedMaw");
            PushPlayer();
            lua_.Push((double)zone);
            lua_.Call(2, 1);
            return lua_.ToBoolean(-1);
        });
        // xi.quest.getMustZone(player, log, xi.quest.id[area][constName])
        public bool QuestMustZone(QuestLog log, string constName)
        {
            int id = QuestId(log, constName);
            return Protected(() =>
            {
                lua_.GetPath("xi.quest.getMustZone");
                PushPlayer();
                lua_.Push((double)(int)log);
                lua_.Push((double)id);
                lua_.Call(3, 1);
                return lua_.ToBoolean(-1);
            });
        }
        public void DelKeyItem(int keyItem)
        {
            NoEffect("delete a key item");
            PlayerCall("delKeyItem", 0, () => 0, keyItem);
        }
        public bool SeenKeyItem(int keyItem) => PlayerCall("seenKeyItem", 1, () => lua_.ToBoolean(-1), keyItem);
        public int MainLevel => PlayerCall("getMainLvl", 1, () => (int)lua_.ToIntegral(-1));
        public int SubLevel => PlayerCall("getSubLvl", 1, () => (int)lua_.ToIntegral(-1));
        public int EquipId(int slot) => PlayerCall("getEquipID", 1, () => (int)lua_.ToIntegral(-1), slot);
        public double XPos => PlayerCall("getXPos", 1, () => lua_.ToNumber(-1));
        public double YPos => PlayerCall("getYPos", 1, () => lua_.ToNumber(-1));
        public double ZPos => PlayerCall("getZPos", 1, () => lua_.ToNumber(-1));
        // player:checkDistance(x, y, z)
        public double DistanceTo(double x, double y, double z) => PlayerCall("checkDistance", 1, () => lua_.ToNumber(-1), x, y, z);
        // player:getCharVar(name)
        public int CharVar(string name) => PlayerCall("getCharVar", 1, () => lua_.IsNumber(-1) ? (int)lua_.ToIntegral(-1) : 0, name);
        // player:getMod(mod)
        public int Mod(int mod) => PlayerCall("getMod", 1, () => (int)lua_.ToIntegral(-1), mod);
        // player:getFreeSlotsCount()
        public int FreeSlotCount => PlayerCall("getFreeSlotsCount", 1, () => (int)lua_.ToIntegral(-1));
        // player:getContainerSize(container) (quest rework P6)
        public int ContainerSize(int container) => PlayerCall("getContainerSize", 1, () => (int)lua_.ToIntegral(-1), container);
        // player:changeContainerSize(container, delta) (quest rework P6)
        public void ChangeContainerSize(int container, int delta)
        {
            NoEffect("change a container's size");
            PlayerCall("changeContainerSize", 0, () => 0, container, delta);
        }
        // player:hasItem(item)
        public bool HasItem(int item) => PlayerCall("hasItem", 1, () => lua_.ToBoolean(-1), item);
        // player:hasItem(item, location)
        public bool HasItem(int item, int location) => PlayerCall("hasItem", 1, () => lua_.ToBoolean(-1), item, location);
        // GetSystemTime()
        public int SystemTime => Protected(() =>
        {
            lua_.GetGlobal("GetSystemTime");
            lua_.Call(0, 1);
            return (int)lua_.ToIntegral(-1);
        });
        // JstMidnight()
        public int JstMidnight => Protected(() =>
        {
            lua_.GetGlobal("JstMidnight");
            lua_.Call(0, 1);
            return (int)lua_.ToIntegral(-1);
        });
        // player:getID() (explicit: Text.Id(ZoneText) is a method of this class elsewhere)
        int IQuestPlayerView.Id => PlayerCall("getID", 1, () => (int)lua_.ToIntegral(-1));
        // NextConquestTally()
        public int NextConquestTally => Protected(() =>
        {
            lua_.GetGlobal("NextConquestTally");
            lua_.Call(0, 1);
            return (int)lua_.ToIntegral(-1);
        });
        // VanadielTOTD()
        public int VanadielTotd => Protected(() =>
        {
            lua_.GetGlobal("VanadielTOTD");
            lua_.Call(0, 1);
            return (int)lua_.ToIntegral(-1);
        });
        // xi.besieged.getMercenaryRank(player)
        public int MercenaryRank => Protected(() =>
        {
            lua_.GetGlobal("xi");
            lua_.GetField(-1, "besieged");
            lua_.GetField(-1, "getMercenaryRank");
            PushPlayer();
            lua_.Call(1, 1);
            return (int)lua_.ToIntegral(-1);
        });
        // xi.abyssea.getTraverserCap(player)
        public int TraverserCap => Protected(() =>
        {
            lua_.GetPath("xi.abyssea.getTraverserCap");
            PushPlayer();
            lua_.Call(1, 1);
            return (int)lua_.ToIntegral(-1);
        });
        // player:setTraverserEpoch()
        public void SetTraverserEpoch()
        {
            NoEffect("set the traverser epoch");
            PlayerCall("setTraverserEpoch", 0, () => 0);
        }
        // player:getCurrency(currency)
        public int Currency(string currency) => PlayerCall("getCurrency", 1, () => (int)lua_.ToIntegral(-1), currency);
        // xi.abyssea.getHeldTraverserStones(player)
        public int HeldTraverserStones => Protected(() =>
        {
            lua_.GetPath("xi.abyssea.getHeldTraverserStones");
            PushPlayer();
            lua_.Call(1, 1);
            return (int)lua_.ToIntegral(-1);
        });
        // xi.abyssea.getZoneKIReward(player): nil (all nine maw quests completed) reads 0
        public int AbysseaZoneKIReward => Protected(() =>
        {
            lua_.GetPath("xi.abyssea.getZoneKIReward");
            PushPlayer();
            lua_.Call(1, 1);
            return lua_.IsNumber(-1) ? (int)lua_.ToIntegral(-1) : 0;
        });
        // player:addCurrency(currency, amount)
        public void AddCurrency(string currency, int amount)
        {
            NoEffect("add currency");
            PlayerCall("addCurrency", 0, () => 0, currency, amount);
        }
        // player:delCurrency(currency, amount)
        public void DelCurrency(string currency, int amount)
        {
            NoEffect("take currency");
            PlayerCall("delCurrency", 0, () => 0, currency, amount);
        }
        // xi.abyssea.dominionOnMobDeath(mob, player, opId), mob the handler's mob argument (nil when it has none)
        public void DominionOnMobDeath(int opId)
        {
            NoEffect("count a dominion op kill");
            Protected(() =>
            {
                lua_.GetPath("xi.abyssea.dominionOnMobDeath");
                if (mob_ == 0 || lua_.IsNil(mob_)) lua_.PushNil(); else lua_.PushValue(mob_);
                PushPlayer();
                lua_.Push((double)opId);
                lua_.Call(3, 0);
                return 0;
            });
        }
        // #player:getParty()
        public int PartySize => PlayerCall("getParty", 1, () => lua_.IsTable(-1) ? lua_.ObjLen(-1) : 0);
        // player:getAlliance(): each member's member:getMainLvl() and member:checkDistance(player), in the table's order
        public IReadOnlyList<(int MainLevel, double Distance)> Alliance => Protected(() =>
        {
            var members = new List<(int MainLevel, double Distance)>();
            PushPlayer();
            int self = lua_.Top;
            lua_.GetField(self, "getAlliance");
            lua_.PushValue(self);
            lua_.Call(1, 1);
            int list = lua_.Top;
            if (!lua_.IsTable(list)) return members;
            for (int i = 1; ; i++)
            {
                lua_.SetTop(list);
                lua_.RawGetI(list, i);
                if (lua_.IsNil(-1)) break;
                int member = lua_.Top;
                lua_.GetField(member, "getMainLvl");
                lua_.PushValue(member);
                lua_.Call(1, 1);
                int level = (int)lua_.ToIntegral(-1);
                lua_.GetField(member, "checkDistance");
                lua_.PushValue(member);
                lua_.PushValue(self);
                lua_.Call(2, 1);
                members.Add((level, lua_.ToNumber(-1)));
            }
            return members;
        });
        // player:getAlliance(): each member's member:getCharVar(name), in the table's order (quest rework P7)
        public IReadOnlyList<int> AllianceCharVars(string name) => Protected(() =>
        {
            var values = new List<int>();
            PushPlayer();
            int self = lua_.Top;
            lua_.GetField(self, "getAlliance");
            lua_.PushValue(self);
            lua_.Call(1, 1);
            int list = lua_.Top;
            if (!lua_.IsTable(list)) return values;
            for (int i = 1; ; i++)
            {
                lua_.SetTop(list);
                lua_.RawGetI(list, i);
                if (lua_.IsNil(-1)) break;
                int member = lua_.Top;
                lua_.GetField(member, "getCharVar");
                lua_.PushValue(member);
                lua_.PushString(name);
                lua_.Call(2, 1);
                values.Add((int)lua_.ToIntegral(-1));
            }
            return values;
        });
        // player:getRace()
        public int Race => PlayerCall("getRace", 1, () => (int)lua_.ToIntegral(-1));
        // player:getParty(): each member's member:getZoneID(), member:checkDistance(player), member:getNation(),
        // member:getRace() and member:getMainJob(), in the table's order
        public IReadOnlyList<(int ZoneId, double Distance, int Nation, int Race, int MainJob)> PartyMembers => Protected(() =>
        {
            var members = new List<(int ZoneId, double Distance, int Nation, int Race, int MainJob)>();
            PushPlayer();
            int self = lua_.Top;
            lua_.GetField(self, "getParty");
            lua_.PushValue(self);
            lua_.Call(1, 1);
            int list = lua_.Top;
            if (!lua_.IsTable(list)) return members;
            for (int i = 1; ; i++)
            {
                lua_.SetTop(list);
                lua_.RawGetI(list, i);
                if (lua_.IsNil(-1)) break;
                int member = lua_.Top;
                int MemberInt(string method)
                {
                    lua_.GetField(member, method);
                    lua_.PushValue(member);
                    lua_.Call(1, 1);
                    int v = (int)lua_.ToIntegral(-1);
                    lua_.Pop(1);
                    return v;
                }
                int zoneId = MemberInt("getZoneID");
                lua_.GetField(member, "checkDistance");
                lua_.PushValue(member);
                lua_.PushValue(self);
                lua_.Call(2, 1);
                double distance = lua_.ToNumber(-1);
                lua_.Pop(1);
                members.Add((zoneId, distance, MemberInt("getNation"), MemberInt("getRace"), MemberInt("getMainJob")));
            }
            return members;
        });
        // player:getPreviousZone(): the zone-in's prevZone (luautils::OnZoneIn passes the same c.PrevZone)
        public int PreviousZone => PlayerCall("getPreviousZone", 1, () => (int)lua_.ToIntegral(-1));
        // player:findItem(item): an item or nil; its truth, as Lua's `not` tests it
        public bool FindItem(int item) => PlayerCall("findItem", 1, () => lua_.ToBoolean(-1), item);
        // player:getMannequinPose(item)
        public int MannequinPose(int item) => PlayerCall("getMannequinPose", 1, () => (int)lua_.ToIntegral(-1), item);
        // player:getCharSkillLevel(skill): the stored value, 10x the level
        public int CharSkillLevel(int skill) => PlayerCall("getCharSkillLevel", 1, () => (int)lua_.ToIntegral(-1), skill);
        // player:getSkillLevel(skill): the working skill
        public int SkillLevel(int skill) => PlayerCall("getSkillLevel", 1, () => (int)lua_.ToIntegral(-1), skill);
        // player:getGil()
        public int Gil => PlayerCall("getGil", 1, () => (int)lua_.ToIntegral(-1));
        // player:addGil(amount) / player:delGil(amount)
        public void AddGil(int amount)
        {
            NoEffect("add gil");
            PlayerCall("addGil", 0, () => 0, amount);
        }
        public void DelGil(int amount)
        {
            NoEffect("take gil");
            PlayerCall("delGil", 0, () => 0, amount);
        }
        // player:delGil(amount), its boolean
        public bool DelGilChecked(int amount)
        {
            NoEffect("take gil");
            return PlayerCall("delGil", 1, () => lua_.ToBoolean(-1), amount);
        }
        // SetServerVariable(name, value)
        public void SetServerVar(string name, int value)
        {
            NoEffect("set a server variable");
            Protected(() =>
            {
                lua_.GetGlobal("SetServerVariable");
                lua_.PushString(name);
                lua_.Push((double)value);
                lua_.Call(2, 0);
                return 0;
            });
        }
        // player:hasSpell(spell)
        public bool HasSpell(int spell) => PlayerCall("hasSpell", 1, () => lua_.ToBoolean(-1), spell);
        // NextJstDay()
        public int NextJstDay => Protected(() =>
        {
            lua_.GetGlobal("NextJstDay");
            lua_.Call(0, 1);
            return (int)lua_.ToIntegral(-1);
        });
        // player:hasJob(job)
        public bool HasJob(int job) => PlayerCall("hasJob", 1, () => lua_.ToBoolean(-1), job);
        // player:addLearnedWeaponskill(wsUnlock)
        public void AddLearnedWeaponskill(int wsUnlock)
        {
            NoEffect("learn a weapon skill");
            PlayerCall("addLearnedWeaponskill", 0, () => 0, wsUnlock);
        }
        // player:addSpell(spell), no options table
        public void AddSpell(int spell)
        {
            NoEffect("add a spell");
            PlayerCall("addSpell", 0, () => 0, spell);
        }
        // player:addSpell(spell, { silentLog = silentLog })
        public void AddSpell(int spell, bool silentLog)
        {
            NoEffect("add a spell");
            Protected(() =>
            {
                PushPlayer();
                int self = lua_.Top;
                lua_.GetField(self, "addSpell");
                lua_.PushValue(self);
                lua_.Push((double)spell);
                lua_.NewTable();
                lua_.Push(silentLog);
                lua_.SetField(-2, "silentLog");
                lua_.Call(3, 0);
                return 0;
            });
        }
        // xi.teleport.to(player, destination)
        public void TeleportTo(int destination)
        {
            NoEffect("teleport the char");
            Protected(() =>
            {
                lua_.GetPath("xi.teleport.to");
                PushPlayer();
                lua_.Push((double)destination);
                lua_.Call(2, 0);
                return 0;
            });
        }
        // xi.maws.gotoRandomMaw(player)
        public void GotoRandomMaw()
        {
            NoEffect("send the char to a past maw");
            Protected(() =>
            {
                lua_.GetPath("xi.maws.gotoRandomMaw");
                PushPlayer();
                lua_.Call(1, 0);
                return 0;
            });
        }
        // xi.maws.addMaw(player), with no maw: the one of the char's zone
        public void AddMaw()
        {
            NoEffect("add a past maw");
            Protected(() =>
            {
                lua_.GetPath("xi.maws.addMaw");
                PushPlayer();
                lua_.Call(1, 0);
                return 0;
            });
        }
        // xi.maws.goToMaw(player, maw), the maw a number as the caller gives it (its Lua raise is the handler's)
        public void GoToMaw(int maw)
        {
            NoEffect("send the char to a maw");
            Protected(() =>
            {
                lua_.GetPath("xi.maws.goToMaw");
                PushPlayer();
                lua_.Push((double)maw);
                lua_.Call(2, 0);
                return 0;
            });
        }
        // xi.maws.goToMaw(player, xi.maws.pastMaws[zone]): the table's entry pushed as the Lua reads it (nil for a zone
        // with no past maw, and goToMaw's raise is the handler's)
        public void GoToPastMaw(int zone)
        {
            NoEffect("send the char to a maw");
            Protected(() =>
            {
                lua_.GetPath("xi.maws.goToMaw");
                PushPlayer();
                lua_.GetPath("xi.maws.pastMaws");
                lua_.Push((double)zone);
                lua_.GetTable(-2);
                lua_.Remove(-2);
                lua_.Call(2, 0);
                return 0;
            });
        }
        // xi.wotg.helpers.removeMemoryFragments(player); the helper file required first (no container file loads it
        // once the missions are C#)
        public void RemoveMemoryFragments()
        {
            NoEffect("remove the memory fragments");
            Protected(() =>
            {
                RequireHelper("scripts/missions/wotg/helpers");
                lua_.GetPath("xi.wotg.helpers.removeMemoryFragments");
                PushPlayer();
                lua_.Call(1, 0);
                return 0;
            });
        }
        // player:getFace()
        public int Face => PlayerCall("getFace", 1, () => (int)lua_.ToIntegral(-1));
        // player:getEquipmentModelIds(): the table's head, body, hands, main and sub fields
        public (int Head, int Body, int Hands, int Main, int Sub) EquipmentModelIds =>
            PlayerCall("getEquipmentModelIds", 1, () =>
            {
                int Field(string key)
                {
                    lua_.GetField(-1, key);
                    int v = (int)lua_.ToIntegral(-1);
                    lua_.Pop(1);
                    return v;
                }
                return (Field("head"), Field("body"), Field("hands"), Field("main"), Field("sub"));
            });
        // npcUtil.giveItem(player, item), with nil pushed for a null item
        public bool GiveItemOrNil(int? item)
        {
            NoEffect("give an item");
            return Protected(() =>
            {
                lua_.GetPath("npcUtil.giveItem");
                PushPlayer();
                if (item is int i) lua_.Push((double)i);
                else lua_.PushNil();
                lua_.Call(2, 1);
                return lua_.ToBoolean(-1);
            });
        }
        // player:getNation()
        public int Nation => PlayerCall("getNation", 1, () => (int)lua_.ToIntegral(-1));
        // VanadielHour()
        public int VanadielHour => Protected(() =>
        {
            lua_.GetGlobal("VanadielHour");
            lua_.Call(0, 1);
            return (int)lua_.ToIntegral(-1);
        });
        // getVanadielMoonCycle()
        public int VanadielMoonCycle => Protected(() =>
        {
            lua_.GetGlobal("getVanadielMoonCycle");
            lua_.Call(0, 1);
            return (int)lua_.ToIntegral(-1);
        });
        // player:getName()
        public string Name => PlayerCall("getName", 1, () => lua_.ToStringValue(-1));
        // player:getZone():getLocalVar(name)
        public int ZoneLocalVar(string name) => Protected(() =>
        {
            int zone = PushPlayerZone();
            return Method(zone, "getLocalVar", 1, () => (int)lua_.ToIntegral(-1), name);
        });
        // player:getZone():setLocalVar(name, value)
        public void SetZoneLocalVar(string name, int value)
        {
            NoEffect("set a zone's local var");
            Protected(() =>
            {
                int zone = PushPlayerZone();
                return Method(zone, "setLocalVar", 0, () => 0, name, value);
            });
        }
        // player:needToZone() / player:needToZone(value)
        public bool NeedToZone => PlayerCall("needToZone", 1, () => lua_.ToBoolean(-1));
        public void SetNeedToZone(bool value)
        {
            NoEffect("set the zoning flag");
            PlayerCall("needToZone", 0, () => 0, value);
        }
        // player:getGender()
        public int Gender => PlayerCall("getGender", 1, () => (int)lua_.ToIntegral(-1));
        // player:getPos(): the table's x, y, z and rot
        public (double X, double Y, double Z, int Rot) Pos => PlayerCall("getPos", 1, () =>
        {
            int pos = lua_.Top;
            lua_.GetField(pos, "x");
            lua_.GetField(pos, "y");
            lua_.GetField(pos, "z");
            lua_.GetField(pos, "rot");
            return (lua_.ToNumber(pos + 1), lua_.ToNumber(pos + 2), lua_.ToNumber(pos + 3), (int)lua_.ToIntegral(pos + 4));
        });
        // player:getRank(nation)
        public int Rank(int nation) => PlayerCall("getRank", 1, () => (int)lua_.ToIntegral(-1), nation);
        // player:setRank(rank) / player:setRankPoints(points) / player:getRankPoints() (quest rework C2)
        public void SetRank(int rank)
        {
            NoEffect("set the rank");
            PlayerCall("setRank", 0, () => 0, rank);
        }
        public void SetRankPoints(int points)
        {
            NoEffect("set the rank points");
            PlayerCall("setRankPoints", 0, () => 0, points);
        }
        // player:addRankPoints(points)
        public void AddRankPoints(int points)
        {
            NoEffect("add rank points");
            PlayerCall("addRankPoints", 0, () => 0, points);
        }
        public int RankPoints => PlayerCall("getRankPoints", 1, () => (int)lua_.ToIntegral(-1));
        // player:getCurrentMission(missionLog)
        public int CurrentMission(int missionLog) => PlayerCall("getCurrentMission", 1, () => (int)lua_.ToIntegral(-1), missionLog);
        // xi.mission.getVar(player, missionLog, mission, name)
        public int MissionVar(int missionLog, int mission, string name) => Protected(() =>
        {
            lua_.GetPath("xi.mission.getVar");
            PushPlayer();
            lua_.Push((double)missionLog);
            lua_.Push((double)mission);
            lua_.PushString(name);
            lua_.Call(4, 1);
            return lua_.IsNumber(-1) ? (int)lua_.ToIntegral(-1) : 0;
        });
        // player:getMissionStatus(missionLog)
        public int MissionStatus(int missionLog) => PlayerCall("getMissionStatus", 1, () => (int)lua_.ToIntegral(-1), missionLog);
        // player:getMissionStatus(missionLog, pos)
        public int MissionStatus(int missionLog, int pos) =>
            PlayerCall("getMissionStatus", 1, () => (int)lua_.ToIntegral(-1), missionLog, pos);
        // player:addMission(missionLog, mission) / player:delMission(missionLog, mission)
        public void AddMission(int missionLog, int mission)
        {
            NoEffect("add a mission");
            PlayerCall("addMission", 0, () => 0, missionLog, mission);
        }
        public void DelMission(int missionLog, int mission)
        {
            NoEffect("delete a mission");
            PlayerCall("delMission", 0, () => 0, missionLog, mission);
        }
        // player:completeMission(missionLog, mission)
        public void CompleteMissionRaw(int missionLog, int mission)
        {
            NoEffect("complete a mission");
            PlayerCall("completeMission", 0, () => 0, missionLog, mission);
        }
        // player:setMissionStatus(missionLog, status)
        public void SetMissionStatus(int missionLog, int status)
        {
            NoEffect("set a mission status");
            PlayerCall("setMissionStatus", 0, () => 0, missionLog, status);
        }
        // xi.mission.setVar(player, missionLog, mission, name, value)
        public void SetMissionVar(int missionLog, int mission, string name, int value)
        {
            NoEffect("set a mission var");
            Protected(() =>
            {
                lua_.GetPath("xi.mission.setVar");
                PushPlayer();
                lua_.Push((double)missionLog);
                lua_.Push((double)mission);
                lua_.PushString(name);
                lua_.Push((double)value);
                lua_.Call(5, 0);
                return 0;
            });
        }
        // xi.mission.setMustZone(player, missionLog, mission)
        public void SetMissionMustZone(int missionLog, int mission)
        {
            NoEffect("set a mission's mustZone");
            Protected(() =>
            {
                lua_.GetPath("xi.mission.setMustZone");
                PushPlayer();
                lua_.Push((double)missionLog);
                lua_.Push((double)mission);
                lua_.Call(3, 0);
                return 0;
            });
        }
        // npcUtil.completeMission(player, missionLog, mission, { nextMission = { nextMissionLog, nextMission } })
        public bool CompleteMission(int missionLog, int mission, int nextMissionLog, int nextMission)
        {
            NoEffect("complete a mission");
            return Protected(() =>
            {
                lua_.GetPath("npcUtil.completeMission");
                PushPlayer();
                lua_.Push((double)missionLog);
                lua_.Push((double)mission);
                lua_.NewTable();
                lua_.NewTable();
                lua_.Push((double)nextMissionLog);
                lua_.RawSetI(-2, 1);
                lua_.Push((double)nextMission);
                lua_.RawSetI(-2, 2);
                lua_.SetField(-2, "nextMission");
                lua_.Call(4, 1);
                return lua_.ToBoolean(-1);
            });
        }
        // player:getMoghouseFlag()
        public int MoghouseFlag => PlayerCall("getMoghouseFlag", 1, () => (int)lua_.ToIntegral(-1));
        // player:setMoghouseFlag(flag)
        public void SetMoghouseFlag(int flag)
        {
            NoEffect("set the mog house flag");
            PlayerCall("setMoghouseFlag", 0, () => 0, flag);
        }
        // player:getWeather()
        public int Weather => PlayerCall("getWeather", 1, () => (int)lua_.ToIntegral(-1));
        // player:getZone(true):getWeather()
        public int ZoneWeather => PlayerCall("getZone", 1, () => Method(lua_.Top, "getWeather", 1, () => (int)lua_.ToIntegral(-1)), true);
        // player:hasStatusEffect(effect)
        public bool HasStatusEffect(int effect) => PlayerCall("hasStatusEffect", 1, () => lua_.ToBoolean(-1), effect);
        // player:addStatusEffect(effect, { power = power, subPower = subPower, origin = player, flag = flag })
        public void AddStatusEffect(int effect, int power, int subPower, int flag)
        {
            NoEffect("add a status effect");
            Protected(() =>
            {
                PushPlayer();
                int self = lua_.Top;
                lua_.GetField(self, "addStatusEffect");
                lua_.PushValue(self);
                lua_.Push((double)effect);
                lua_.NewTable();
                lua_.Push((double)power);
                lua_.SetField(-2, "power");
                lua_.Push((double)subPower);
                lua_.SetField(-2, "subPower");
                lua_.PushValue(self);
                lua_.SetField(-2, "origin");
                lua_.Push((double)flag);
                lua_.SetField(-2, "flag");
                lua_.Call(3, 0);
                return 0;
            });
        }
        // player:addStatusEffect(effect, { power = power, duration = durationSeconds, origin = player })
        public void AddStatusEffectForDuration(int effect, int power, int durationSeconds)
        {
            NoEffect("add a status effect");
            Protected(() =>
            {
                PushPlayer();
                int self = lua_.Top;
                lua_.GetField(self, "addStatusEffect");
                lua_.PushValue(self);
                lua_.Push((double)effect);
                lua_.NewTable();
                lua_.Push((double)power);
                lua_.SetField(-2, "power");
                lua_.Push((double)durationSeconds);
                lua_.SetField(-2, "duration");
                lua_.PushValue(self);
                lua_.SetField(-2, "origin");
                lua_.Call(3, 0);
                return 0;
            });
        }
        // player:addStatusEffect(effect, { duration = durationSeconds, origin = player }) (no power field)
        public void AddStatusEffectForDuration(int effect, int durationSeconds)
        {
            NoEffect("add a status effect");
            Protected(() =>
            {
                PushPlayer();
                int self = lua_.Top;
                lua_.GetField(self, "addStatusEffect");
                lua_.PushValue(self);
                lua_.Push((double)effect);
                lua_.NewTable();
                lua_.Push((double)durationSeconds);
                lua_.SetField(-2, "duration");
                lua_.PushValue(self);
                lua_.SetField(-2, "origin");
                lua_.Call(3, 0);
                return 0;
            });
        }
        // player:delStatusEffectsByFlag(flag, silent)
        public void DelStatusEffectsByFlag(int flag, bool silent)
        {
            NoEffect("delete status effects");
            PlayerCall("delStatusEffectsByFlag", 0, () => 0, flag, silent);
        }
        // player:delStatusEffect(effect)
        public void DelStatusEffect(int effect)
        {
            NoEffect("delete a status effect");
            PlayerCall("delStatusEffect", 0, () => 0, effect);
        }
        // zones[zone].mob[mobKey] in the handler's zone; an unknown key raises, as the Lua's arithmetic on nil does
        public int MobIdOf(string mobKey)
        {
            ushort zone = Zone;
            return Protected(() =>
            {
                PushZoneEntry(zone, "mob", mobKey);
                if (!lua_.IsNumber(-1)) throw new InvalidOperationException($"unknown mob {mobKey} in zone {zone}");
                return (int)lua_.ToIntegral(-1);
            });
        }
        // zones[zone].npc[npcKey] in the handler's zone, a table of ids (GetTableOfIDs) read in its order ([1] first); a
        // key that is missing or not a table raises, as the Lua's index of nil does
        public IReadOnlyList<int> NpcIdsOf(string npcKey)
        {
            ushort zone = Zone;
            return Protected(() =>
            {
                PushZoneEntry(zone, "npc", npcKey);
                if (!lua_.IsTable(-1)) throw new InvalidOperationException($"unknown npc list {npcKey} in zone {zone}");
                var ids = new List<int>();
                for (int i = 1; ; i++)
                {
                    lua_.RawGetI(-1, i);
                    if (!lua_.IsNumber(-1)) { lua_.Pop(1); break; }
                    ids.Add((int)lua_.ToIntegral(-1));
                    lua_.Pop(1);
                }
                return (IReadOnlyList<int>)ids;
            });
        }
        // zones[zone][kind][key] pushed (nil where a level is missing or not a table), for MobIdOf and NpcIdsOf; the
        // caller's Protected pops it
        private void PushZoneEntry(ushort zone, string kind, string key)
        {
            lua_.GetGlobal("zones");
            if (lua_.IsTable(-1)) lua_.RawGetI(-1, zone); else lua_.PushNil();
            if (lua_.IsTable(-1)) lua_.GetField(-1, kind); else lua_.PushNil();
            if (lua_.IsTable(-1)) lua_.GetField(-1, key); else lua_.PushNil();
        }
        // xi.automaton.getModelId(player)
        public int AutomatonModelId => Protected(() =>
        {
            lua_.GetPath("xi.automaton.getModelId");
            PushPlayer();
            lua_.Call(1, 1);
            return (int)lua_.ToIntegral(-1);
        });
        // player:getLevelCap() / player:setLevelCap(cap)
        public int LevelCap => PlayerCall("getLevelCap", 1, () => (int)lua_.ToIntegral(-1));
        public void SetLevelCap(int cap)
        {
            NoEffect("set the level cap");
            PlayerCall("setLevelCap", 0, () => 0, cap);
        }
        // player:getBattlefield(), then battlefield:getID() on what it returned; null when it returned nil
        public int? BattlefieldId => PlayerCall("getBattlefield", 1,
            () => lua_.IsNil(-1) ? (int?)null : Method(lua_.Top, "getID", 1, () => (int?)(int)lua_.ToIntegral(-1)));
        // player:getMeritCount() / player:setMerits(merits)
        public int MeritCount => PlayerCall("getMeritCount", 1, () => (int)lua_.ToIntegral(-1));
        public void SetMerits(int merits)
        {
            NoEffect("set the merit points");
            PlayerCall("setMerits", 0, () => 0, merits);
        }
        // player:getCampaignAllegiance() / player:setCampaignAllegiance(nation)
        public int CampaignAllegiance => PlayerCall("getCampaignAllegiance", 1, () => (int)lua_.ToIntegral(-1));
        public void SetCampaignAllegiance(int nation)
        {
            NoEffect("set the campaign allegiance");
            PlayerCall("setCampaignAllegiance", 0, () => 0, nation);
        }
        // xi.moghouse.inMogHouseInHomeNation(player)
        public bool InMogHouseInHomeNation => Protected(() =>
        {
            lua_.GetPath("xi.moghouse.inMogHouseInHomeNation");
            PushPlayer();
            lua_.Call(1, 1);
            return lua_.ToBoolean(-1);
        });
        // player:inMogHouse()
        public bool InMogHouse => PlayerCall("inMogHouse", 1, () => lua_.ToBoolean(-1));
        // player:isEngaged()
        public bool IsEngaged => PlayerCall("isEngaged", 1, () => lua_.ToBoolean(-1));
        // xi.monstrosity.hasUnlockedSpecies(player, species)
        public bool MonstrosityHasUnlockedSpecies(int species) => Protected(() =>
        {
            lua_.GetPath("xi.monstrosity.hasUnlockedSpecies");
            PushPlayer();
            lua_.Push((double)species);
            lua_.Call(2, 1);
            return lua_.ToBoolean(-1);
        });
        // player:setPos(x, y, z, rot, zone)
        public void SetPos(double x, double y, double z, int rot, int zone)
        {
            NoEffect("set the char's position");
            PlayerCall("setPos", 0, () => 0, x, y, z, rot, zone);
        }
        // player:getZone() pushed; its stack index
        private int PushPlayerZone()
        {
            PushPlayer();
            int self = lua_.Top;
            lua_.GetField(self, "getZone");
            lua_.PushValue(self);
            lua_.Call(1, 1);
            if (lua_.IsNil(-1)) throw new InvalidOperationException("the player has no zone");
            return lua_.Top;
        }
        // GetPlayerByName(name): the char's getID() and getZoneID(), null when nil
        public (int Id, int ZoneId)? PlayerByName(string name) => Protected(() =>
        {
            lua_.GetGlobal("GetPlayerByName");
            lua_.Push(name);
            lua_.Call(1, 1);
            return PlayerIdAndZone(lua_.Top);
        });
        // GetPlayerByID(id): the same
        public (int Id, int ZoneId)? PlayerById(int id) => Protected(() =>
        {
            lua_.GetGlobal("GetPlayerByID");
            lua_.Push(id);
            lua_.Call(1, 1);
            return PlayerIdAndZone(lua_.Top);
        });
        private (int Id, int ZoneId)? PlayerIdAndZone(int at)
        {
            if (lua_.IsNil(at)) return null;
            int id = Method(at, "getID", 1, () => (int)lua_.ToIntegral(-1));
            int zone = Method(at, "getZoneID", 1, () => (int)lua_.ToIntegral(-1));
            return (id, zone);
        }
        // player:addKeyItem(keyItem)
        public void AddKeyItem(int keyItem)
        {
            NoEffect("add a key item");
            PlayerCall("addKeyItem", 0, () => 0, keyItem);
        }
        // player:startEvent(csid, args...)
        public void StartEvent(int csid, params int[] args)
        {
            NoEffect("start an event");
            PlayerCall("startEvent", 0, () => 0, new object[] { csid }.Concat(args.Cast<object>()).ToArray());
        }
        // player:startCutscene(csid, args...)
        public void StartCutscene(int csid, params int[] args)
        {
            NoEffect("start a cutscene");
            PlayerCall("startCutscene", 0, () => 0, new object[] { csid }.Concat(args.Cast<object>()).ToArray());
        }
        // VanadielTime()
        public int VanadielTime => Protected(() =>
        {
            lua_.GetGlobal("VanadielTime");
            lua_.Call(0, 1);
            return (int)lua_.ToIntegral(-1);
        });
        // local chocoState = player:getChocoboRaisingInfo(); if chocoState then full, first, last =
        // xi.chocoboRaising.nameStrings(chocoState) end ('' x3 without one)
        public (string Full, string First, string Last) ChocoboNameStrings => Protected(() =>
        {
            PushPlayer();
            int self = lua_.Top;
            lua_.GetField(self, "getChocoboRaisingInfo");
            lua_.PushValue(self);
            lua_.Call(1, 1);
            if (lua_.IsNil(-1) || !lua_.IsTable(-1)) return ("", "", "");
            int state = lua_.Top;
            lua_.GetPath("xi.chocoboRaising.nameStrings");
            lua_.PushValue(state);
            lua_.Call(1, 3);
            string Str(int i) => lua_.IsString(i) ? lua_.ToStringValue(i) : "";
            return (Str(-3), Str(-2), Str(-1));
        });
        // player:startEventString(csid, s1, s2, s3, s4, args...)
        public void StartEventString(int csid, string s1, string s2, string s3, string s4, params int[] args)
        {
            NoEffect("start an event");
            Protected(() =>
            {
                PushPlayer();
                int self = lua_.Top;
                lua_.GetField(self, "startEventString");
                lua_.PushValue(self);
                lua_.Push((double)csid);
                foreach (var s in new[] { s1, s2, s3, s4 }) lua_.PushString(s ?? "");
                foreach (var a in args) lua_.Push((double)a);
                lua_.Call(args.Length + 6, 0);
                return 0;
            });
        }
        // xi.chocoboRaising.setWhistleProgress(player, progress)
        public void SetChocoboWhistleProgress(int progress)
        {
            NoEffect("set the chocobo whistle progress");
            Protected(() =>
            {
                lua_.GetPath("xi.chocoboRaising.setWhistleProgress");
                PushPlayer();
                lua_.Push((double)progress);
                lua_.Call(2, 0);
                return 0;
            });
        }
        // xi.wotg.helpers.checkMemoryFragments(player) (raises in the Lua with all four large fragments, in a zone with
        // no REPORT_TO_CAIT_SITH text)
        public void CheckMemoryFragments()
        {
            NoEffect("check the memory fragments");
            Protected(() =>
            {
                lua_.GetPath("xi.wotg.helpers.checkMemoryFragments");
                PushPlayer();
                lua_.Call(1, 0);
                return 0;
            });
        }
        // xi.chocoboRaising.whistle.giveQuestWhistle(player)
        public bool GiveChocoboQuestWhistle()
        {
            NoEffect("give the chocobo whistle");
            return Protected(() =>
            {
                lua_.GetPath("xi.chocoboRaising.whistle.giveQuestWhistle");
                PushPlayer();
                lua_.Call(1, 1);
                return lua_.ToBoolean(-1);
            });
        }
        // xi.maps.onEventUpdate(player, csid, option, npc), npc the handler's npc argument
        public void MapsOnEventUpdate(int csid, int option)
        {
            NoEffect("update a map shop event");
            if (npc_ == 0 || lua_.IsNil(npc_)) throw new InvalidOperationException("no npc in this handler");
            Protected(() =>
            {
                lua_.GetPath("xi.maps.onEventUpdate");
                PushPlayer();
                lua_.Push((double)csid);
                lua_.Push((double)option);
                lua_.PushValue(npc_);
                lua_.Call(4, 0);
                return 0;
            });
        }
        // VanadielUniqueDay()
        public int VanadielUniqueDay => Protected(() =>
        {
            lua_.GetGlobal("VanadielUniqueDay");
            lua_.Call(0, 1);
            return (int)lua_.ToIntegral(-1);
        });
        // utils.vanadielClockTime()
        public int VanadielClockTime => Protected(() =>
        {
            lua_.GetPath("utils.vanadielClockTime");
            lua_.Call(0, 1);
            return (int)lua_.ToIntegral(-1);
        });
        // VanadielRSELocation() / VanadielRSERace()
        public int VanadielRseLocation => Protected(() =>
        {
            lua_.GetGlobal("VanadielRSELocation");
            lua_.Call(0, 1);
            return (int)lua_.ToIntegral(-1);
        });
        public int VanadielRseRace => Protected(() =>
        {
            lua_.GetGlobal("VanadielRSERace");
            lua_.Call(0, 1);
            return (int)lua_.ToIntegral(-1);
        });
        // xi.equipment.vigilWeaponRequiredWsPoints(player): the global looked up at each call, so an override of it is the
        // function called: the base global (250), or the era module
        // modules/era/lua/globals/equipment/unlocking_a_myth.lua's override when the Lua module loader applied it
        // (platform round P11, ZoneHost.LuaModules). The number is rounded to a whole one (ToIntegral: llround), so a
        // non-whole override would lose its fraction
        public int VigilWeaponRequiredWsPoints => Protected(() =>
        {
            lua_.GetPath("xi.equipment.vigilWeaponRequiredWsPoints");
            PushPlayer();
            lua_.Call(1, 1);
            return (int)lua_.ToIntegral(-1);
        });
        // the missions' Lua helpers, each global looked up at each call so a module's override is the one that runs.
        // The helper file is required first: once its missions are C#, no container file loads it
        public bool SoaImprimaturGate(int gateAmount) => Protected(() =>
        {
            RequireHelper("scripts/missions/soa/helpers");
            lua_.GetPath("xi.soa.helpers.imprimaturGate");
            PushPlayer();
            lua_.Push((double)gateAmount);
            lua_.Call(2, 1);
            return lua_.ToBoolean(-1);
        });

        public bool WotgMeetsMissionReqs(int mission) => Protected(() =>
        {
            RequireHelper("scripts/missions/wotg/helpers");
            lua_.GetPath($"xi.wotg.helpers.meetsMission{mission}Reqs");
            PushPlayer();
            lua_.Call(1, 1);
            return lua_.ToBoolean(-1);
        });

        // whitegateShared.doRoyalPalaceArmorCheck(player): a function of the zone's Shared module, which the require
        // returns (it is no global); the module is required at each call; require answers the cached module, the one
        // the Lua files hold
        public bool RoyalPalaceArmorCheck() => Protected(() =>
        {
            lua_.GetGlobal("require");
            lua_.PushString("scripts/zones/Aht_Urhgan_Whitegate/Shared");
            lua_.Call(1, 1);
            lua_.GetField(-1, "doRoyalPalaceArmorCheck");
            PushPlayer();
            lua_.Call(1, 1);
            return lua_.ToBoolean(-1);
        });

        // xi.mission.getMissionRankPoints(player, mission): a boolean
        public bool MissionRankPoints(int mission) => Protected(() =>
        {
            lua_.GetPath("xi.mission.getMissionRankPoints");
            PushPlayer();
            lua_.Push((double)mission);
            lua_.Call(2, 1);
            return lua_.ToBoolean(-1);
        });

        private void RequireHelper(string file)
        {
            lua_.GetGlobal("require");
            lua_.PushString(file);
            lua_.Call(1, 0);
        }
        // zones[zone].npc[npcKey] in the handler's zone; an unknown key raises, as the Lua's arithmetic on nil does
        public int NpcIdOf(string npcKey)
        {
            ushort zone = Zone;
            return Protected(() =>
            {
                lua_.GetGlobal("zones");
                if (lua_.IsTable(-1)) lua_.RawGetI(-1, zone); else lua_.PushNil();
                if (lua_.IsTable(-1)) lua_.GetField(-1, "npc"); else lua_.PushNil();
                if (lua_.IsTable(-1)) lua_.GetField(-1, npcKey); else lua_.PushNil();
                if (!lua_.IsNumber(-1)) throw new InvalidOperationException($"unknown npc {npcKey} in zone {zone}");
                return (int)lua_.ToIntegral(-1);
            });
        }
        // getVanaMidnight()
        public int VanaMidnight => Protected(() =>
        {
            lua_.GetGlobal("getVanaMidnight");
            lua_.Call(0, 1);
            return (int)lua_.ToIntegral(-1);
        });
        // xi.settings.main[name]: a number (a fraction rounded half away from zero, ToIntegral's llround), a boolean as
        // 1 / 0, anything else 0
        public int Main(string name) => SettingInt("xi.settings.main", name);
        // xi.settings.map[name] (quest rework P6), read as Main reads xi.settings.main
        public int Map(string name) => SettingInt("xi.settings.map", name);
        // xi.settings.map[name] == true (quest rework P6): the boolean true only
        public bool MapFlag(string name) => Protected(() =>
        {
            lua_.GetPath("xi.settings.map");
            if (!lua_.IsTable(-1)) return false;
            lua_.GetField(-1, name);
            return lua_.IsBoolean(-1) && lua_.ToBoolean(-1);
        });
        // <table>[name]: a number (llround), a boolean as 1 / 0, anything else 0
        private int SettingInt(string table, string name) => Protected(() =>
        {
            lua_.GetPath(table);
            if (!lua_.IsTable(-1)) return 0;
            lua_.GetField(-1, name);
            return lua_.IsNumber(-1) ? (int)lua_.ToIntegral(-1) : lua_.IsBoolean(-1) ? (lua_.ToBoolean(-1) ? 1 : 0) : 0;
        });
        // xi.settings.main[name] as the Lua number it is (GIL_RATE 1.5), a boolean as 1 / 0, anything else 0
        public double MainNumber(string name) => Protected(() =>
        {
            lua_.GetPath("xi.settings.main");
            if (!lua_.IsTable(-1)) return 0.0;
            lua_.GetField(-1, name);
            return lua_.IsNumber(-1) ? lua_.ToNumber(-1) : lua_.IsBoolean(-1) ? (lua_.ToBoolean(-1) ? 1.0 : 0.0) : 0.0;
        });
        // xi.settings.main[name] with its Lua type (quest rework P9): MainEquals / MainTruthy / MainFlag compare it as Lua
        public QuestSetting MainValue(string name) => Protected(() =>
        {
            lua_.GetPath("xi.settings.main");
            if (!lua_.IsTable(-1)) return QuestSetting.Nil;
            lua_.GetField(-1, name);
            return SettingOf(lua_);
        });
        // the value at the top of the stack as a QuestSetting, by its exact Lua type (a numeric string stays a string)
        internal static QuestSetting SettingOf(LuaState l) =>
            l.IsNil(-1) ? QuestSetting.Nil
            : l.IsBoolean(-1) ? QuestSetting.Of(l.ToBoolean(-1))
            : l.IsNumber(-1) ? QuestSetting.Of(l.ToNumber(-1))
            : l.IsString(-1) ? QuestSetting.Of(l.ToStringValue(-1))
            : QuestSetting.OtherValue;
        // player:getHP() / player:getMP()
        public int HP => PlayerCall("getHP", 1, () => (int)lua_.ToIntegral(-1));
        public int MP => PlayerCall("getMP", 1, () => (int)lua_.ToIntegral(-1));
        // player:setHP(hp) / player:setMP(mp)
        public void SetHP(int hp)
        {
            NoEffect("set HP");
            PlayerCall("setHP", 0, () => 0, hp);
        }
        public void SetMP(int mp)
        {
            NoEffect("set MP");
            PlayerCall("setMP", 0, () => 0, mp);
        }
        // player:getMainJob()
        public int MainJob => PlayerCall("getMainJob", 1, () => (int)lua_.ToIntegral(-1));
        // player:canEquipItem(item, checkLevel)
        public bool CanEquipItem(int item, bool checkLevel) => PlayerCall("canEquipItem", 1, () => lua_.ToBoolean(-1), item, checkLevel);
        // xi.equip.isArtifactArmor(item)
        public bool IsArtifactArmor(int item) => Protected(() =>
        {
            lua_.GetPath("xi.equip.isArtifactArmor");
            lua_.Push((double)item);
            lua_.Call(1, 1);
            return lua_.ToBoolean(-1);
        });
        // math.randomInt(lower, upper): the script RNG, so a port draws what the Lua drew
        public int RandomInt(int lower, int upper) => Protected(() =>
        {
            lua_.GetPath("math.randomInt");
            lua_.Push((double)lower);
            lua_.Push((double)upper);
            lua_.Call(2, 1);
            return (int)lua_.ToIntegral(-1);
        });
        // player:delQuest(log, xi.quest.id[area][constName])
        public void DelQuest(QuestLog log, string constName)
        {
            NoEffect("delete a quest");
            int id = QuestId(log, constName);
            PlayerCall("delQuest", 0, () => 0, (int)log, id);
        }
        // player:addQuest(log, xi.quest.id[area][constName])
        public void AddQuest(QuestLog log, string constName)
        {
            NoEffect("add a quest");
            int id = QuestId(log, constName);
            PlayerCall("addQuest", 0, () => 0, (int)log, id);
        }
        // player:completeQuest(log, xi.quest.id[area][constName])
        public void CompleteQuest(QuestLog log, string constName)
        {
            NoEffect("complete a quest");
            int id = QuestId(log, constName);
            PlayerCall("completeQuest", 0, () => 0, (int)log, id);
        }
        // player:updateEvent(args...)
        public void UpdateEvent(params int[] args)
        {
            NoEffect("update an event");
            PlayerCall("updateEvent", 0, () => 0, args.Cast<object>().ToArray());
        }
        // player:unlockJob(job)
        public void UnlockJob(int job)
        {
            NoEffect("unlock a job");
            PlayerCall("unlockJob", 0, () => 0, job);
        }
        // player:setPetName(petType, nameId)
        public void SetPetName(int petType, int nameId)
        {
            NoEffect("set a pet name");
            PlayerCall("setPetName", 0, () => 0, petType, nameId);
        }
        // player:unlockAttachment(item)
        public void UnlockAttachment(int item)
        {
            NoEffect("unlock an attachment");
            PlayerCall("unlockAttachment", 0, () => 0, item);
        }
        // player:hasTitle(title)
        public bool HasTitle(int title) => PlayerCall("hasTitle", 1, () => lua_.ToBoolean(-1), title);
        // player:getTitle()
        public int CurrentTitle => PlayerCall("getTitle", 1, () => (int)lua_.ToIntegral(-1));
        // player:getPlaytime()
        public int Playtime => PlayerCall("getPlaytime", 1, () => (int)lua_.ToIntegral(-1));
        // player:addExp(exp)
        public void AddExp(int exp)
        {
            NoEffect("add exp");
            PlayerCall("addExp", 0, () => 0, exp);
        }
        // player:getLocalVar(name) / player:setLocalVar(name, value)
        int IQuestPlayerView.LocalVar(string name) => PlayerCall("getLocalVar", 1, () => (int)lua_.ToIntegral(-1), name);
        void IQuestPlayer.SetLocalVar(string name, int value)
        {
            NoEffect("set a local var");
            PlayerCall("setLocalVar", 0, () => 0, name, value);
        }
        // player:getQuestStatus(log, xi.quest.id[area][constName])
        public QuestStatus QuestStatus(QuestLog log, string constName)
        {
            int id = QuestId(log, constName);
            return (QuestStatus)PlayerCall("getQuestStatus", 1, () => (int)lua_.ToIntegral(-1), (int)log, id);
        }
        public void AddFame(int fameArea, int amount)
        {
            NoEffect("add fame");
            PlayerCall("addFame", 0, () => 0, fameArea, amount);
        }
        // player:addItem(item, qty); qty 1 is the call without one, as an NPC script writes it
        public bool AddItem(int item, int qty = 1)
        {
            NoEffect("add an item");
            return qty == 1
                ? PlayerCall("addItem", 1, () => lua_.ToBoolean(-1), item)
                : PlayerCall("addItem", 1, () => lua_.ToBoolean(-1), item, qty);
        }
        // local added = player:addItem({ id = item }) if added then added:setExData({ [field] = value }) end
        public bool AddItemWithExData(int item, string field, int value)
        {
            NoEffect("add an item");
            return Protected(() =>
            {
                PushPlayer();
                int self = lua_.Top;
                lua_.GetField(self, "addItem");
                lua_.PushValue(self);
                lua_.NewTable();
                lua_.Push((double)item);
                lua_.SetField(-2, "id");
                lua_.Call(2, 1);
                int added = lua_.Top;
                if (!lua_.ToBoolean(added)) return false;
                lua_.GetField(added, "setExData");
                lua_.PushValue(added);
                lua_.NewTable();
                lua_.Push((double)value);
                lua_.SetField(-2, field);
                lua_.Call(2, 0);
                return true;
            });
        }
        // player:addItem(item, qty, augment0, value0, augment1, value1): six numbers, the result unread
        public void AddItemAugmented(int item, int qty, int augment0, int value0, int augment1, int value1)
        {
            NoEffect("add an item");
            PlayerCall("addItem", 0, () => 0, item, qty, augment0, value0, augment1, value1);
        }
        // player:setMannequinPose(item, race, pose)
        public void SetMannequinPose(int item, int race, int pose)
        {
            NoEffect("pose a mannequin");
            PlayerCall("setMannequinPose", 0, () => 0, item, race, pose);
        }
        // player:addTitle(title)
        public void AddTitle(int title)
        {
            NoEffect("add a title");
            PlayerCall("addTitle", 0, () => 0, title);
        }
        // xi.instance.onEventFinish(player, csid, option, nil): the npc is nil, the Lua reads only the player, csid,
        // option
        public void InstanceOnEventFinish(int csid, int option)
        {
            NoEffect("enter an instance");
            Protected(() =>
            {
                lua_.GetPath("xi.instance.onEventFinish");
                PushPlayer();
                lua_.Push((double)csid);
                lua_.Push((double)option);
                lua_.PushNil();
                lua_.Call(4, 0);
                return 0;
            });
        }
        // player:setTitle(title)
        public void SetTitle(int title)
        {
            NoEffect("set a title");
            PlayerCall("setTitle", 0, () => 0, title);
        }
        // player:setCharVar(name, value[, expiry]); an expiry of 0 is the call without one, as an NPC script writes it
        public void SetCharVar(string name, int value, uint expiry = 0)
        {
            NoEffect("set a char var");
            if (expiry == 0) PlayerCall("setCharVar", 0, () => 0, name, value);
            else PlayerCall("setCharVar", 0, () => 0, name, value, (double)expiry);
        }
        // player:incrementCharVar(name, value)
        public void IncrementCharVar(string name, int value)
        {
            NoEffect("increment a char var");
            PlayerCall("incrementCharVar", 0, () => 0, name, value);
        }
        // xi.quest.setVar(player, log, xi.quest.id[area][constName], name, value)
        public void SetQuestVar(QuestLog log, string constName, string name, int value)
        {
            NoEffect("set another quest's var");
            int id = QuestId(log, constName);
            Protected(() =>
            {
                lua_.GetPath("xi.quest.setVar");
                PushPlayer();
                lua_.Push((double)(int)log);
                lua_.Push((double)id);
                lua_.PushString(name);
                lua_.Push((double)value);
                lua_.Call(5, 0);
                return 0;
            });
        }
        // GetMobByID(zones[zone].mob[mobName] + offset):isSpawned() / :isAlive(); a nil mob raises, as the Lua's call on nil does
        public bool MobIsSpawned(string mobName, int offset = 0) => MobCall("GetMobByID", mobName, offset, "isSpawned", false);
        public bool MobIsAlive(string mobName, int offset = 0) => MobCall("GetMobByID", mobName, offset, "isAlive", false);
        // GetMobByID(zones[zone].mob[mobName] + offset):isDead()
        public bool MobIsDead(string mobName, int offset = 0) => MobCall("GetMobByID", mobName, offset, "isDead", false);
        // GetMobByID(zones[zone].mob[mobName] + offset) ~= nil (quest rework C6)
        public bool MobExists(string mobName, int offset = 0)
        {
            int id = MobIdOf(mobName) + offset;
            return Protected(() =>
            {
                lua_.GetGlobal("GetMobByID");
                lua_.Push((double)id);
                lua_.Call(1, 1);
                return !lua_.IsNil(-1);
            });
        }
        // GetMobByID(zones[zone].mob[mobName] + offset):getLocalVar(name); a nil mob raises, as the Lua's call on nil
        // does
        public int MobLocalVar(string mobName, string name, int offset = 0)
        {
            int id = MobIdOf(mobName) + offset;
            return Protected(() =>
            {
                lua_.GetGlobal("GetMobByID");
                lua_.Push((double)id);
                lua_.Call(1, 1);
                int mob = lua_.Top;
                if (lua_.IsNil(mob))
                    throw new InvalidOperationException($"GetMobByID({id}) is nil ({mobName}+{offset})");
                lua_.GetField(mob, "getLocalVar");
                lua_.PushValue(mob);
                lua_.PushString(name);
                lua_.Call(2, 1);
                return (int)lua_.ToIntegral(-1);
            });
        }
        // SpawnMob(zones[zone].mob[mobName] + offset):updateClaim(player), or :updateEnmity(player)
        public void SpawnMob(string mobName, int offset, bool claim)
        {
            NoEffect("spawn a mob");
            MobCall("SpawnMob", mobName, offset, claim ? "updateClaim" : "updateEnmity", true);
        }
        // player:setPos(x, y, z): no rot, no zone
        public void SetPos(double x, double y, double z)
        {
            NoEffect("set the char's position");
            PlayerCall("setPos", 0, () => 0, x, y, z);
        }
        // player:setPos(x, y, z, rot): no zone
        public void SetPos(double x, double y, double z, int rot)
        {
            NoEffect("set the char's position");
            PlayerCall("setPos", 0, () => 0, x, y, z, rot);
        }
        // player:showText(npc, zones[text.Zone].text[text.Name], args...), npc the handler's npc argument: sent to this char
        // alone (Bindings.ShowText with a char as self), the npc as the speaker; an unknown name throws, as Special does
        public void ShowText(ZoneText text, params int[] args)
        {
            NoEffect("show a text");
            int? id = TextId(lua_, text.Zone, text.Name);
            if (id == null) throw new InvalidOperationException($"unknown text {text.Name} in zone {text.Zone}");
            if (npc_ == 0 || lua_.IsNil(npc_)) throw new InvalidOperationException("no npc in this handler");
            Protected(() =>
            {
                PushPlayer();
                int self = lua_.Top;
                lua_.GetField(self, "showText");
                lua_.PushValue(self);
                lua_.PushValue(npc_);
                lua_.Push((double)id.Value);
                foreach (var a in args) lua_.Push((double)a);
                lua_.Call(args.Length + 3, 0);
                return 0;
            });
        }
        // player:showText(npc, textId, args...) with a text id the handler computed (a zone text plus an offset), npc
        // the handler's npc argument, as the ZoneText form above
        public void ShowText(int textId, params int[] args)
        {
            NoEffect("show a text");
            if (npc_ == 0 || lua_.IsNil(npc_)) throw new InvalidOperationException("no npc in this handler");
            Protected(() =>
            {
                PushPlayer();
                int self = lua_.Top;
                lua_.GetField(self, "showText");
                lua_.PushValue(self);
                lua_.PushValue(npc_);
                lua_.Push((double)textId);
                foreach (var a in args) lua_.Push((double)a);
                lua_.Call(args.Length + 3, 0);
                return 0;
            });
        }
        // DespawnMob(zones[zone].mob[mobName] + offset), with no method called on the mob
        public void DespawnMob(string mobName, int offset = 0)
        {
            NoEffect("despawn a mob");
            MobCall("DespawnMob", mobName, offset, null, false);
        }
        // SpawnMob(zones[zone].mob[mobName]), with no method called on the mob
        public void SpawnMob(string mobName)
        {
            NoEffect("spawn a mob");
            MobCall("SpawnMob", mobName, 0, null, false);
        }
        // SpawnMob(zones[zone].mob[mobName] + offset), with no method called on the mob
        public void SpawnMob(string mobName, int offset)
        {
            NoEffect("spawn a mob");
            MobCall("SpawnMob", mobName, offset, null, false);
        }
        // local mob = SpawnMob(zones[zone].mob[mobName]); nil: false; else mob:updateClaim(player), mob:setLocalVar('QMID',
        // npcId), and a DESPAWN listener that sets GetNPCByID(QMID):setLocalVar(npcVar, GetSystemTime() + seconds): true
        // (CSharpQuests.spawnMobWithNpcCooldown, the Lua of Forge Your Destiny's qm3 / qm2 trades)
        public bool SpawnMobWithNpcCooldown(string mobName, int npcId, string npcVar, int seconds)
        {
            NoEffect("spawn a mob");
            int mobId = MobIdOf(mobName);
            return Protected(() =>
            {
                lua_.GetPath("CSharpQuests.spawnMobWithNpcCooldown");
                PushPlayer();
                lua_.Push((double)npcId);
                lua_.Push((double)mobId);
                lua_.PushString(npcVar);
                lua_.Push((double)seconds);
                lua_.Call(5, 1);
                return lua_.ToBoolean(-1);
            });
        }
        // player:getZone():queryEntitiesByName(name)[1]:getID() (CSharpQuests.mobIdByName)
        public int MobIdByName(string name)
        {
            return Protected(() =>
            {
                lua_.GetPath("CSharpQuests.mobIdByName");
                PushPlayer();
                lua_.PushString(name);
                lua_.Call(2, 1);
                if (!lua_.IsNumber(-1)) throw new InvalidOperationException($"no entity {name} in zone {Zone}");
                return (int)lua_.ToIntegral(-1);
            });
        }
        // GetMobByID(id):isSpawned(); a nil mob raises, as the Lua's call on nil does
        public bool MobIsSpawnedById(int id)
        {
            return Protected(() =>
            {
                lua_.GetPath("CSharpQuests.mobIsSpawnedById");
                lua_.Push((double)id);
                lua_.Call(1, 1);
                return lua_.ToBoolean(-1);
            });
        }
        // SpawnMob(id):updateClaim(player) or :updateEnmity(player)
        public void SpawnMobById(int id, bool claim)
        {
            NoEffect("spawn a mob");
            Protected(() =>
            {
                lua_.GetPath("CSharpQuests.spawnMobById");
                PushPlayer();
                lua_.Push((double)id);
                lua_.Push(claim);
                lua_.Call(3, 0);
                return 0;
            });
        }
        // local mob = GetMobByID(id); nil: false; else mob:spawn(), mob:updateClaim(player) and, with zeroEnmity,
        // mob:setCE(player, 0) (CSharpQuests.spawnMobEntityById)
        public bool SpawnMobEntityById(int id, bool zeroEnmity)
        {
            NoEffect("spawn a mob");
            return Protected(() =>
            {
                lua_.GetPath("CSharpQuests.spawnMobEntityById");
                PushPlayer();
                lua_.Push((double)id);
                lua_.Push(zeroEnmity);
                lua_.Call(3, 1);
                return lua_.ToBoolean(-1);
            });
        }
        // npcUtil.popFromQM(player, npc, { id, ... }, { hide = hide })
        public bool PopFromQmIds(int[] ids, int hide)
        {
            NoEffect("pop a mob");
            if (npc_ == 0 || lua_.IsNil(npc_)) throw new InvalidOperationException("no npc in this handler");
            return Protected(() =>
            {
                lua_.GetPath("npcUtil.popFromQM");
                PushPlayer();
                lua_.PushValue(npc_);
                lua_.NewTable();
                int list = lua_.Top;
                for (int i = 0; i < ids.Length; i++)
                {
                    lua_.Push((double)ids[i]);
                    lua_.RawSetI(list, i + 1);
                }
                lua_.NewTable();
                lua_.Push((double)hide);
                lua_.SetField(-2, "hide");
                lua_.Call(4, 1);
                return lua_.ToBoolean(-1);
            });
        }
        // print(message)
        public void LuaPrint(string message)
        {
            Protected(() =>
            {
                lua_.GetGlobal("print");
                lua_.PushString(message);
                lua_.Call(1, 0);
                return 0;
            });
        }
        // npcUtil.popFromQM(player, npc, { zones[zone].mob[mobName], ... }, { hide = hide, claim = claim }): each mob
        // entry as IDs.lua has it, in the order given (quest rework C3 N5)
        public bool PopFromQm(string[] mobNames, int hide, bool claim)
        {
            NoEffect("pop a mob");
            if (npc_ == 0 || lua_.IsNil(npc_)) throw new InvalidOperationException("no npc in this handler");
            ushort zone = Zone;
            return Protected(() =>
            {
                lua_.GetPath("npcUtil.popFromQM");
                PushPlayer();
                lua_.PushValue(npc_);
                lua_.NewTable();
                int list = lua_.Top;
                lua_.GetGlobal("zones");
                if (lua_.IsTable(-1)) lua_.RawGetI(-1, zone); else lua_.PushNil();
                if (lua_.IsTable(-1)) lua_.GetField(-1, "mob"); else lua_.PushNil();
                int mobs = lua_.Top;
                for (int i = 0; i < mobNames.Length; i++)
                {
                    if (lua_.IsTable(mobs)) lua_.GetField(mobs, mobNames[i]); else lua_.PushNil();
                    if (lua_.IsNil(-1))
                        throw new InvalidOperationException($"unknown mob {mobNames[i]} in zone {zone}");
                    lua_.RawSetI(list, i + 1);
                }
                // the tables the entries were read through dropped
                lua_.SetTop(list);
                lua_.NewTable();
                lua_.Push((double)hide);
                lua_.SetField(-2, "hide");
                lua_.Push(claim);
                lua_.SetField(-2, "claim");
                lua_.Call(4, 1);
                return lua_.ToBoolean(-1);
            });
        }
        // npcUtil.popFromQM(player, npc, { id, id + 1, ..., id + count - 1 }, { claim = claim, look = look, radius =
        // radius }), id = zones[zone].mob[mobName], which must be an id; no hide, so popFromQM's default applies
        // (quest rework C3 N5)
        public bool PopFromQmSeriesRadius(string mobName, int count, bool claim, bool look, int radius)
        {
            NoEffect("pop a mob");
            if (npc_ == 0 || lua_.IsNil(npc_)) throw new InvalidOperationException("no npc in this handler");
            int first = MobIdOf(mobName);
            return Protected(() =>
            {
                lua_.GetPath("npcUtil.popFromQM");
                PushPlayer();
                lua_.PushValue(npc_);
                lua_.NewTable();
                for (int i = 0; i < count; i++)
                {
                    lua_.Push((double)(first + i));
                    lua_.RawSetI(-2, i + 1);
                }
                lua_.NewTable();
                lua_.Push(claim);
                lua_.SetField(-2, "claim");
                lua_.Push(look);
                lua_.SetField(-2, "look");
                lua_.Push((double)radius);
                lua_.SetField(-2, "radius");
                lua_.Call(4, 1);
                return lua_.ToBoolean(-1);
            });
        }
        // GetMobByID(zones[zone].mob[mobName] + offset):lookAt(player:getPos()); a nil mob raises (quest rework C3 N5)
        public void MobLookAtPlayer(string mobName, int offset = 0)
        {
            NoEffect("turn a mob");
            int id = MobIdOf(mobName) + offset;
            Protected(() =>
            {
                lua_.GetGlobal("GetMobByID");
                lua_.Push((double)id);
                lua_.Call(1, 1);
                int mob = lua_.Top;
                if (lua_.IsNil(mob))
                    throw new InvalidOperationException($"GetMobByID({id}) is nil ({mobName}+{offset})");
                lua_.GetField(mob, "lookAt");
                lua_.PushValue(mob);
                PushPlayer();
                int player = lua_.Top;
                lua_.GetField(player, "getPos");
                lua_.PushValue(player);
                lua_.Call(1, 1);
                // the position table in the player's place, the player dropped
                lua_.Insert(player);
                lua_.Pop(1);
                lua_.Call(2, 0);
                return 0;
            });
        }
        // GetMobByID(zones[zone].mob[mobName] + offset):updateClaim(player); a nil mob raises (quest rework C3 N5)
        public void MobUpdateClaim(string mobName, int offset = 0)
        {
            NoEffect("claim a mob");
            MobCall("GetMobByID", mobName, offset, "updateClaim", true);
        }
        // for _, member in ipairs(player:getAlliance()) do container:setLocalVar(member, name, value) ... end
        // (CSharpQuests.allianceLocalVars), the container this context's quest or mission
        internal void AllianceLocalVars((string Name, int Value)[] vars)
        {
            NoEffect("set a local var");
            Protected(() =>
            {
                lua_.GetPath("CSharpQuests.allianceLocalVars");
                lua_.PushRef(quest_.Container);
                PushPlayer();
                lua_.NewTable();
                int list = lua_.Top;
                for (int i = 0; i < vars.Length; i++)
                {
                    lua_.NewTable();
                    lua_.PushString(vars[i].Name);
                    lua_.RawSetI(-2, 1);
                    lua_.Push((double)vars[i].Value);
                    lua_.RawSetI(-2, 2);
                    lua_.RawSetI(list, i + 1);
                }
                lua_.Call(3, 0);
                return 0;
            });
        }
        // player:setMonstrosityData({ monstrosityId = monstrosityId, species = species })
        public void SetMonstrosityData(int monstrosityId, int species)
        {
            NoEffect("set the Monstrosity data");
            Protected(() =>
            {
                PushPlayer();
                lua_.GetField(-1, "setMonstrosityData");
                lua_.Insert(-2);
                lua_.NewTable();
                lua_.Push((double)monstrosityId);
                lua_.SetField(-2, "monstrosityId");
                lua_.Push((double)species);
                lua_.SetField(-2, "species");
                lua_.Call(2, 0);
                return 0;
            });
        }
        // xi.monstrosity.suibhneGreet(player, npc), npc the handler's npc argument (nil when it has none)
        public void MonstrositySuibhneGreet()
        {
            NoEffect("greet");
            Protected(() =>
            {
                lua_.GetPath("xi.monstrosity.suibhneGreet");
                PushPlayer();
                if (npc_ == 0) lua_.PushNil(); else lua_.PushValue(npc_);
                lua_.Call(2, 0);
                return 0;
            });
        }
        // xi.monstrosity.unlockSpecies(player, species)
        public void MonstrosityUnlockSpecies(int species)
        {
            NoEffect("unlock a species");
            Protected(() =>
            {
                lua_.GetPath("xi.monstrosity.unlockSpecies");
                PushPlayer();
                lua_.Push((double)species);
                lua_.Call(2, 0);
                return 0;
            });
        }
        // player:setHomePoint()
        public void SetHomePoint()
        {
            NoEffect("set the home point");
            PlayerCall("setHomePoint", 0, () => 0);
        }
        // player:startEvent(csid, { flags = flags, isHidden = isHidden })
        public void StartEventFlags(int csid, uint flags, bool isHidden)
        {
            NoEffect("start an event");
            Protected(() =>
            {
                PushPlayer();
                lua_.GetField(-1, "startEvent");
                lua_.Insert(-2);
                lua_.Push((double)csid);
                lua_.NewTable();
                lua_.Push((double)flags);
                lua_.SetField(-2, "flags");
                lua_.Push(isHidden);
                lua_.SetField(-2, "isHidden");
                lua_.Call(3, 0);
                return 0;
            });
        }
        // player:startEvent(csid, { isHidden = true })
        public void StartEventHidden(int csid)
        {
            NoEffect("start an event");
            Protected(() =>
            {
                PushPlayer();
                lua_.GetField(-1, "startEvent");
                lua_.Insert(-2);
                lua_.Push((double)csid);
                lua_.NewTable();
                lua_.Push(true);
                lua_.SetField(-2, "isHidden");
                lua_.Call(3, 0);
                return 0;
            });
        }
        // player:startEvent(csid, { [index] = value, isHidden = true })
        public void StartEventHiddenParam(int csid, int index, int value)
        {
            NoEffect("start an event");
            Protected(() =>
            {
                PushPlayer();
                lua_.GetField(-1, "startEvent");
                lua_.Insert(-2);
                lua_.Push((double)csid);
                lua_.NewTable();
                lua_.Push((double)value);
                lua_.RawSetI(-2, index);
                lua_.Push(true);
                lua_.SetField(-2, "isHidden");
                lua_.Call(3, 0);
                return 0;
            });
        }
        // player:enableEntities({ id, ... }): the ids in the table's order, an empty table for none
        public void EnableEntities(params int[] ids)
        {
            NoEffect("enable entities");
            Protected(() =>
            {
                PushPlayer();
                lua_.GetField(-1, "enableEntities");
                lua_.Insert(-2);
                lua_.NewTable();
                for (int i = 0; i < ids.Length; i++)
                {
                    lua_.Push((double)ids[i]);
                    lua_.RawSetI(-2, i + 1);
                }
                lua_.Call(2, 0);
                return 0;
            });
        }
        // player:sendPartialMissionLog(missionLog, completed)
        public void SendPartialMissionLog(int missionLog, bool completed)
        {
            NoEffect("send the mission log");
            PlayerCall("sendPartialMissionLog", 0, () => 0, missionLog, completed);
        }
        // xi.monstrosity.updateIntroEvent(player)
        public void MonstrosityUpdateIntroEvent()
        {
            NoEffect("update the event");
            Protected(() =>
            {
                lua_.GetPath("xi.monstrosity.updateIntroEvent");
                PushPlayer();
                lua_.Call(1, 0);
                return 0;
            });
        }
        // player:sendMenu(menuType)
        public void SendMenu(int menuType)
        {
            NoEffect("send a menu");
            PlayerCall("sendMenu", 0, () => 0, menuType);
        }
        // xi.survivalGuide.onTrigger(player)
        public void SurvivalGuideOnTrigger()
        {
            NoEffect("trigger the survival guide");
            Protected(() =>
            {
                lua_.GetPath("xi.survivalGuide.onTrigger");
                PushPlayer();
                lua_.Call(1, 0);
                return 0;
            });
        }
        // GetNPCByID(zones[zone].npc[npcName]):setLocalVar(name, value) in the handler's zone; a nil NPC raises, as the Lua's
        // call on nil does
        public void SetNpcLocalVar(string npcName, string name, int value)
        {
            NoEffect("set an NPC's local var");
            ushort zone = Zone;
            Protected(() =>
            {
                lua_.GetGlobal("zones");
                if (lua_.IsTable(-1)) lua_.RawGetI(-1, zone); else lua_.PushNil();
                if (lua_.IsTable(-1)) lua_.GetField(-1, "npc"); else lua_.PushNil();
                if (lua_.IsTable(-1)) lua_.GetField(-1, npcName); else lua_.PushNil();
                if (!lua_.IsNumber(-1)) throw new InvalidOperationException($"unknown npc {npcName} in zone {zone}");
                double id = lua_.ToNumber(-1);
                lua_.GetGlobal("GetNPCByID");
                lua_.Push(id);
                lua_.Call(1, 1);
                int npc = lua_.Top;
                if (lua_.IsNil(npc)) throw new InvalidOperationException($"GetNPCByID({id}) is nil ({npcName} in zone {zone})");
                lua_.GetField(npc, "setLocalVar");
                lua_.PushValue(npc);
                lua_.PushString(name);
                lua_.Push((double)value);
                lua_.Call(3, 0);
                return 0;
            });
        }

        // <getter>(zones[zone].mob[mobName] + offset):<method>([player]) in the handler's zone; the boolean it returns
        // (a null method: the getter's call alone, its result dropped, true)
        private bool MobCall(string getter, string mobName, int offset, string method, bool withPlayer)
        {
            ushort zone = Zone;
            return Protected(() =>
            {
                lua_.GetGlobal("zones");
                if (lua_.IsTable(-1)) lua_.RawGetI(-1, zone); else lua_.PushNil();
                if (lua_.IsTable(-1)) lua_.GetField(-1, "mob"); else lua_.PushNil();
                if (lua_.IsTable(-1)) lua_.GetField(-1, mobName); else lua_.PushNil();
                if (!lua_.IsNumber(-1)) throw new InvalidOperationException($"unknown mob {mobName} in zone {zone}");
                double id = lua_.ToNumber(-1) + offset;
                lua_.GetGlobal(getter);
                lua_.Push(id);
                lua_.Call(1, 1);
                int mob = lua_.Top;
                // the bare call's result is dropped, as the Lua's statement drops it
                if (method == null) return true;
                if (lua_.IsNil(mob)) throw new InvalidOperationException($"{getter}({id}) is nil ({mobName}+{offset} in zone {zone})");
                lua_.GetField(mob, method);
                lua_.PushValue(mob);
                if (withPlayer) PushPlayer();
                lua_.Call(withPlayer ? 2 : 1, 1);
                return lua_.ToBoolean(-1);
            });
        }
        // xi.quest.setMustZone(player, log, xi.quest.id[area][constName])
        public void SetQuestMustZone(QuestLog log, string constName)
        {
            NoEffect("set the mustZone of another quest");
            int id = QuestId(log, constName);
            Protected(() =>
            {
                lua_.GetPath("xi.quest.setMustZone");
                PushPlayer();
                lua_.Push((double)(int)log);
                lua_.Push((double)id);
                lua_.Call(3, 0);
                return 0;
            });
        }

        // ---- the trade ----

        // trade:getItem(slot):getWeaponskillPoints(); a nil item raises, as the Lua's call on nil does
        public int ItemWeaponskillPoints(int slot)
        {
            if (trade_ == 0) throw new InvalidOperationException("no trade in this handler");
            return Protected(() =>
            {
                lua_.PushValue(trade_);
                int trade = lua_.Top;
                lua_.GetField(trade, "getItem");
                lua_.PushValue(trade);
                lua_.Push((double)slot);
                lua_.Call(2, 1);
                int item = lua_.Top;
                if (lua_.IsNil(item)) throw new InvalidOperationException($"trade:getItem({slot}) is nil");
                lua_.GetField(item, "getWeaponskillPoints");
                lua_.PushValue(item);
                lua_.Call(1, 1);
                return (int)lua_.ToIntegral(-1);
            });
        }

        // npcUtil.tradeMatches(trade, { { item, qty }, ... })
        public bool Matches(params (int Item, int Qty)[] items)
        {
            if (trade_ == 0) throw new InvalidOperationException("no trade in this handler");
            return Protected(() =>
            {
                lua_.GetPath("npcUtil.tradeMatches");
                lua_.PushValue(trade_);
                lua_.NewTable();
                for (int i = 0; i < items.Length; i++)
                {
                    lua_.NewTable();
                    lua_.Push((double)items[i].Item);
                    lua_.RawSetI(-2, 1);
                    lua_.Push((double)items[i].Qty);
                    lua_.RawSetI(-2, 2);
                    lua_.RawSetI(-2, i + 1);
                }
                lua_.Call(2, 1);
                return lua_.ToBoolean(-1);
            });
        }
        // trade:hasItemQty(item, qty)
        public bool HasItemQty(int item, int qty)
        {
            if (trade_ == 0) throw new InvalidOperationException("no trade in this handler");
            return Method(trade_, "hasItemQty", 1, () => lua_.ToBoolean(-1), item, qty);
        }
        // trade:getItemQty(item)
        public int ItemQty(int item)
        {
            if (trade_ == 0) throw new InvalidOperationException("no trade in this handler");
            return Method(trade_, "getItemQty", 1, () => (int)lua_.ToIntegral(-1), item);
        }
        // trade:getSlotCount()
        public int SlotCount
        {
            get
            {
                if (trade_ == 0) throw new InvalidOperationException("no trade in this handler");
                return Method(trade_, "getSlotCount", 1, () => (int)lua_.ToIntegral(-1));
            }
        }
        // trade:getItemId(slot)
        public int ItemId(int slot)
        {
            if (trade_ == 0) throw new InvalidOperationException("no trade in this handler");
            return Method(trade_, "getItemId", 1, () => (int)lua_.ToIntegral(-1), slot);
        }
        // trade:getItem(slot):getAHCat(); a nil item raises, as the Lua's call on nil does
        public int ItemAhCategory(int slot)
        {
            if (trade_ == 0) throw new InvalidOperationException("no trade in this handler");
            return Method(trade_, "getItem", 1, () =>
            {
                int item = lua_.Top;
                if (lua_.IsNil(item)) throw new InvalidOperationException($"trade:getItem({slot}) is nil");
                lua_.GetField(item, "getAHCat");
                lua_.PushValue(item);
                lua_.Call(1, 1);
                return (int)lua_.ToIntegral(-1);
            }, slot);
        }
        // trade:getItemCount()
        public int ItemCount
        {
            get
            {
                if (trade_ == 0) throw new InvalidOperationException("no trade in this handler");
                return Method(trade_, "getItemCount", 1, () => (int)lua_.ToIntegral(-1));
            }
        }
        // npcUtil.tradeHas(trade, { item, ... })
        public bool Has(params int[] items)
        {
            if (trade_ == 0) throw new InvalidOperationException("no trade in this handler");
            return Protected(() =>
            {
                lua_.GetPath("npcUtil.tradeHas");
                lua_.PushValue(trade_);
                lua_.NewTable();
                for (int i = 0; i < items.Length; i++)
                {
                    lua_.Push((double)items[i]);
                    lua_.RawSetI(-2, i + 1);
                }
                lua_.Call(2, 1);
                return lua_.ToBoolean(-1);
            });
        }
        // npcUtil.tradeHas(trade, { item, ..., { 'gil', gil } })
        public bool HasWithGil(int gil, params int[] items) => TradeHasWithGil("npcUtil.tradeHas", gil, items);
        // npcUtil.tradeHasExactly(trade, { item, ..., { 'gil', gil } })
        public bool HasExactlyWithGil(int gil, params int[] items) => TradeHasWithGil("npcUtil.tradeHasExactly", gil, items);
        // fn(trade, { item, ..., { 'gil', gil } }): tradeHas / tradeHasExactly with the Lua's item table as written
        private bool TradeHasWithGil(string fn, int gil, int[] items)
        {
            if (trade_ == 0) throw new InvalidOperationException("no trade in this handler");
            return Protected(() =>
            {
                lua_.GetPath(fn);
                lua_.PushValue(trade_);
                lua_.NewTable();
                for (int i = 0; i < items.Length; i++)
                {
                    lua_.Push((double)items[i]);
                    lua_.RawSetI(-2, i + 1);
                }
                lua_.NewTable();
                lua_.PushString("gil");
                lua_.RawSetI(-2, 1);
                lua_.Push((double)gil);
                lua_.RawSetI(-2, 2);
                lua_.RawSetI(-2, items.Length + 1);
                lua_.Call(2, 1);
                return lua_.ToBoolean(-1);
            });
        }
        // npcUtil.tradeHasExactly(trade, { item, ... })
        public bool HasExactly(params int[] items)
        {
            if (trade_ == 0) throw new InvalidOperationException("no trade in this handler");
            return Protected(() =>
            {
                lua_.GetPath("npcUtil.tradeHasExactly");
                lua_.PushValue(trade_);
                lua_.NewTable();
                for (int i = 0; i < items.Length; i++)
                {
                    lua_.Push((double)items[i]);
                    lua_.RawSetI(-2, i + 1);
                }
                lua_.Call(2, 1);
                return lua_.ToBoolean(-1);
            });
        }
        // npcUtil.tradeHasExactly(trade, { { item, qty }, ... })
        public bool HasExactly(params (int Item, int Qty)[] items) => TradeHasPairs("npcUtil.tradeHasExactly", items);
        // npcUtil.tradeHas(trade, { { item, qty }, ... })
        public bool HasQty(params (int Item, int Qty)[] items) => TradeHasPairs("npcUtil.tradeHas", items);
        // fn(trade, { { item, qty }, ... }): tradeHas / tradeHasExactly with the Lua's item table as written
        private bool TradeHasPairs(string fn, (int Item, int Qty)[] items)
        {
            if (trade_ == 0) throw new InvalidOperationException("no trade in this handler");
            return Protected(() =>
            {
                lua_.GetPath(fn);
                lua_.PushValue(trade_);
                lua_.NewTable();
                for (int i = 0; i < items.Length; i++)
                {
                    lua_.NewTable();
                    lua_.Push((double)items[i].Item);
                    lua_.RawSetI(-2, 1);
                    lua_.Push((double)items[i].Qty);
                    lua_.RawSetI(-2, 2);
                    lua_.RawSetI(-2, i + 1);
                }
                lua_.Call(2, 1);
                return lua_.ToBoolean(-1);
            });
        }
        // trade:getGil() (explicit: a Player-side gil member is another one)
        int IQuestTrade.Gil
        {
            get
            {
                if (trade_ == 0) throw new InvalidOperationException("no trade in this handler");
                return Method(trade_, "getGil", 1, () => (int)lua_.ToIntegral(-1));
            }
        }
        // xi.treasure.onTrade(player, npc, trade, bypassType, bypassReward)
        public int TreasureTrade(int bypassType, int bypassReward)
        {
            NoEffect("trade to a treasure chest");
            if (trade_ == 0) throw new InvalidOperationException("no trade in this handler");
            if (npc_ == 0 || lua_.IsNil(npc_)) throw new InvalidOperationException("no npc in this handler");
            return Protected(() =>
            {
                lua_.GetPath("xi.treasure.onTrade");
                PushPlayer();
                lua_.PushValue(npc_);
                lua_.PushValue(trade_);
                lua_.Push((double)bypassType);
                lua_.Push((double)bypassReward);
                lua_.Call(5, 1);
                return lua_.IsNumber(-1) ? (int)lua_.ToIntegral(-1) : 0;
            });
        }
        // xi.helm.onTrade(player, npc, trade, helmType, nil); an error in the global raises here, as in the Lua handler
        public void HelmOnTrade(int helmType)
        {
            NoEffect("trade to a HELM point");
            if (trade_ == 0) throw new InvalidOperationException("no trade in this handler");
            if (npc_ == 0 || lua_.IsNil(npc_)) throw new InvalidOperationException("no npc in this handler");
            Protected(() =>
            {
                lua_.GetPath("xi.helm.onTrade");
                PushPlayer();
                lua_.PushValue(npc_);
                lua_.PushValue(trade_);
                lua_.Push((double)helmType);
                lua_.PushNil();
                lua_.Call(5, 0);
                return 0;
            });
        }
        // trade:confirmItem(item, qty)
        public void ConfirmItem(int item, int qty)
        {
            if (trade_ == 0) throw new InvalidOperationException("no trade in this handler");
            NoEffect("confirm a trade item");
            Method(trade_, "confirmItem", 0, () => 0, item, qty);
        }
        // player:confirmTrade()
        public void Confirm()
        {
            NoEffect("confirm a trade");
            PlayerCall("confirmTrade", 0, () => 0);
        }
        // player:tradeComplete(); explicit because IQuestProgress has a Complete too
        void IQuestTrade.Complete()
        {
            NoEffect("complete a trade");
            PlayerCall("tradeComplete", 0, () => 0);
        }
        // player:tradeComplete(), its boolean
        public bool CompleteChecked()
        {
            NoEffect("complete a trade");
            return PlayerCall("tradeComplete", 1, () => lua_.ToBoolean(-1));
        }

        // ---- effects ----

        // npcUtil.giveKeyItem(player, ki)
        public bool GiveKeyItem(int keyItem)
        {
            NoEffect("give a key item");
            return Protected(() =>
            {
                lua_.GetPath("npcUtil.giveKeyItem");
                PushPlayer();
                lua_.Push((double)keyItem);
                lua_.Call(2, 1);
                return lua_.ToBoolean(-1);
            });
        }

        // npcUtil.giveKeyItem(player, ki, zones[message.Zone].text[message.Name]); an unknown name throws, as Special does
        public bool GiveKeyItem(int keyItem, ZoneText message)
        {
            NoEffect("give a key item");
            int? id = TextId(lua_, message.Zone, message.Name);
            if (id == null) throw new InvalidOperationException($"unknown text {message.Name} in zone {message.Zone}");
            return Protected(() =>
            {
                lua_.GetPath("npcUtil.giveKeyItem");
                PushPlayer();
                lua_.Push((double)keyItem);
                lua_.Push((double)id.Value);
                lua_.Call(3, 1);
                return lua_.ToBoolean(-1);
            });
        }

        // npcUtil.giveItem(player, item)
        public bool GiveItem(int item)
        {
            NoEffect("give an item");
            return Protected(() =>
            {
                lua_.GetPath("npcUtil.giveItem");
                PushPlayer();
                lua_.Push((double)item);
                lua_.Call(2, 1);
                return lua_.ToBoolean(-1);
            });
        }

        // npcUtil.giveItem(player, item, { silent = silent, fromTrade = fromTrade })
        public bool GiveItem(int item, bool silent, bool fromTrade)
        {
            NoEffect("give an item");
            return Protected(() =>
            {
                lua_.GetPath("npcUtil.giveItem");
                PushPlayer();
                lua_.Push((double)item);
                lua_.NewTable();
                lua_.Push(silent);
                lua_.SetField(-2, "silent");
                lua_.Push(fromTrade);
                lua_.SetField(-2, "fromTrade");
                lua_.Call(3, 1);
                return lua_.ToBoolean(-1);
            });
        }

        // npcUtil.giveItem(player, { { item, qty }, ... })
        public bool GiveItems(params (int Item, int Qty)[] items)
        {
            NoEffect("give items");
            return Protected(() =>
            {
                lua_.GetPath("npcUtil.giveItem");
                PushPlayer();
                lua_.NewTable();
                for (int i = 0; i < items.Length; i++)
                {
                    lua_.NewTable();
                    lua_.Push((double)items[i].Item);
                    lua_.RawSetI(-2, 1);
                    lua_.Push((double)items[i].Qty);
                    lua_.RawSetI(-2, 2);
                    lua_.RawSetI(-2, i + 1);
                }
                lua_.Call(2, 1);
                return lua_.ToBoolean(-1);
            });
        }

        // npcUtil.giveCurrency(player, currency, amount)
        public bool GiveCurrency(string currency, int amount) => GiveCurrency(currency, (double)amount);

        public bool GiveCurrency(string currency, double amount)
        {
            NoEffect("give currency");
            return Protected(() =>
            {
                lua_.GetPath("npcUtil.giveCurrency");
                PushPlayer();
                lua_.PushString(currency);
                lua_.Push(amount);
                lua_.Call(3, 1);
                return lua_.ToBoolean(-1);
            });
        }

        // npcUtil.popFromQM(player, npc, zones[zone].mob[mobName] + offset, { claim = claim, hide = hide }); with no offset the
        // mob entry is passed as IDs.lua has it (an id, or a list of them), with one it must be an id (an offset key's mob:
        // DARK_PUPPET_OFFSET + 1). claim's default is popFromQM's own (true)
        public bool PopFromQm(string mobName, int hide, bool claim = true, int offset = 0) =>
            PopFromQmWith(mobName, claim, hide, offset, null);

        // npcUtil.popFromQM(player, npc, zones[zone].mob[mobName], { claim = claim, hide = hide, radius = radius })
        public bool PopFromQm(string mobName, bool claim, int hide, int radius) => PopFromQmWith(mobName, claim, hide, 0, radius);

        // GetMobByID(zones[zone].mob[mobName] + offset):setSpawn(x, y, z, rot); a nil mob raises, as the Lua's call
        // on nil does
        public void SetMobSpawn(string mobName, int offset, double x, double y, double z, int rot)
        {
            NoEffect("set a mob's spawn point");
            ushort zone = Zone;
            Protected(() =>
            {
                lua_.GetGlobal("zones");
                if (lua_.IsTable(-1)) lua_.RawGetI(-1, zone); else lua_.PushNil();
                if (lua_.IsTable(-1)) lua_.GetField(-1, "mob"); else lua_.PushNil();
                if (lua_.IsTable(-1)) lua_.GetField(-1, mobName); else lua_.PushNil();
                if (!lua_.IsNumber(-1)) throw new InvalidOperationException($"unknown mob {mobName} in zone {zone}");
                double id = lua_.ToNumber(-1) + offset;
                lua_.GetGlobal("GetMobByID");
                lua_.Push(id);
                lua_.Call(1, 1);
                int mob = lua_.Top;
                if (lua_.IsNil(mob))
                    throw new InvalidOperationException($"GetMobByID({id}) is nil ({mobName}+{offset} in zone {zone})");
                lua_.GetField(mob, "setSpawn");
                lua_.PushValue(mob);
                lua_.Push(x);
                lua_.Push(y);
                lua_.Push(z);
                lua_.Push((double)rot);
                lua_.Call(5, 0);
                return 0;
            });
        }

        // the one popFromQM call of the single-mob forms: the params table holds claim and hide, and radius only when given
        // (popFromQM draws its random spawn point only for a radius); an offset moves an id entry (see PopFromQm)
        private bool PopFromQmWith(string mobName, bool claim, int hide, int offset, int? radius)
        {
            NoEffect("pop a mob");
            if (npc_ == 0 || lua_.IsNil(npc_)) throw new InvalidOperationException("no npc in this handler");
            ushort zone = Zone;
            return Protected(() =>
            {
                lua_.GetPath("npcUtil.popFromQM");
                int fn = lua_.Top;
                PushPlayer();
                lua_.PushValue(npc_);
                lua_.GetGlobal("zones");
                if (lua_.IsTable(-1)) lua_.RawGetI(-1, zone); else lua_.PushNil();
                if (lua_.IsTable(-1)) lua_.GetField(-1, "mob"); else lua_.PushNil();
                if (lua_.IsTable(-1)) lua_.GetField(-1, mobName); else lua_.PushNil();
                if (lua_.IsNil(-1)) throw new InvalidOperationException($"unknown mob {mobName} in zone {zone}");
                if (offset != 0)
                {
                    if (!lua_.IsNumber(-1)) throw new InvalidOperationException($"mob {mobName} in zone {zone} is not an id: no offset");
                    double id = lua_.ToNumber(-1) + offset;
                    lua_.Pop(1);
                    lua_.Push(id);
                }
                // the mob entry in the argument's place, the tables it was read through dropped
                lua_.Insert(fn + 3);
                lua_.SetTop(fn + 3);
                lua_.NewTable();
                lua_.Push(claim);
                lua_.SetField(-2, "claim");
                lua_.Push((double)hide);
                lua_.SetField(-2, "hide");
                if (radius is int r)
                {
                    lua_.Push((double)r);
                    lua_.SetField(-2, "radius");
                }
                lua_.Call(4, 1);
                return lua_.ToBoolean(-1);
            });
        }

        // npcUtil.popFromQM(player, npc, { zones[zone].mob[mobName], ... }, { hide = hide }): each mob entry as IDs.lua has
        // it, in the order given
        public bool PopFromQm(string[] mobNames, int hide)
        {
            NoEffect("pop a mob");
            if (npc_ == 0 || lua_.IsNil(npc_)) throw new InvalidOperationException("no npc in this handler");
            ushort zone = Zone;
            return Protected(() =>
            {
                lua_.GetPath("npcUtil.popFromQM");
                int fn = lua_.Top;
                PushPlayer();
                lua_.PushValue(npc_);
                lua_.NewTable();
                int list = lua_.Top;
                lua_.GetGlobal("zones");
                if (lua_.IsTable(-1)) lua_.RawGetI(-1, zone); else lua_.PushNil();
                if (lua_.IsTable(-1)) lua_.GetField(-1, "mob"); else lua_.PushNil();
                int mobs = lua_.Top;
                for (int i = 0; i < mobNames.Length; i++)
                {
                    if (lua_.IsTable(mobs)) lua_.GetField(mobs, mobNames[i]); else lua_.PushNil();
                    if (lua_.IsNil(-1)) throw new InvalidOperationException($"unknown mob {mobNames[i]} in zone {zone}");
                    lua_.RawSetI(list, i + 1);
                }
                // the tables the entries were read through dropped
                lua_.SetTop(list);
                lua_.NewTable();
                lua_.Push((double)hide);
                lua_.SetField(-2, "hide");
                lua_.Call(4, 1);
                return lua_.ToBoolean(-1);
            });
        }

        // npcUtil.popFromQM(player, npc, { zones[zone].mob[mobName] + 0, ..., + count - 1 }, { hide = hide }): the mob entry
        // must be an id (a number), as the Lua's arithmetic on it needs
        public bool PopFromQmSeries(string mobName, int count, int hide) => PopFromQmSeries(mobName, count, hide, claim: true, look: false);

        // npcUtil.popFromQM(player, npc, { id, id + 1, ..., id + count - 1 }, { claim = claim, look = look, hide = hide }),
        // id = zones[zone].mob[mobName], which must be an id
        public bool PopFromQmSeries(string mobName, int count, int hide, bool claim, bool look)
        {
            NoEffect("pop a mob");
            if (npc_ == 0 || lua_.IsNil(npc_)) throw new InvalidOperationException("no npc in this handler");
            int first = MobIdOf(mobName);
            return Protected(() =>
            {
                lua_.GetPath("npcUtil.popFromQM");
                PushPlayer();
                lua_.PushValue(npc_);
                lua_.NewTable();
                for (int i = 0; i < count; i++)
                {
                    lua_.Push((double)(first + i));
                    lua_.RawSetI(-2, i + 1);
                }
                lua_.NewTable();
                lua_.Push(claim);
                lua_.SetField(-2, "claim");
                lua_.Push(look);
                lua_.SetField(-2, "look");
                lua_.Push((double)hide);
                lua_.SetField(-2, "hide");
                lua_.Call(4, 1);
                return lua_.ToBoolean(-1);
            });
        }

        // player:messageSpecial(zones[text.Zone].text[text.Name], ...); an unknown name throws, ending the handler as Lua's
        // player:messageSpecial(nil) raising does under the lookup's pcall (the closure logs it)
        public void Special(ZoneText text, params int[] args)
        {
            NoEffect("send a message");
            int? id = TextId(lua_, text.Zone, text.Name);
            if (id == null) throw new InvalidOperationException($"unknown text {text.Name} in zone {text.Zone}");
            var all = new object[args.Length + 1];
            all[0] = id.Value;
            for (int i = 0; i < args.Length; i++) all[i + 1] = args[i];
            PlayerCall("messageSpecial", 0, () => 0, all);
        }

        // player:messageName(zones[text.Zone].text[text.Name], nil, ...); an unknown name throws, as Special does
        public void MessageName(ZoneText text, params int[] args)
        {
            NoEffect("send a message");
            int? id = TextId(lua_, text.Zone, text.Name);
            if (id == null) throw new InvalidOperationException($"unknown text {text.Name} in zone {text.Zone}");
            Protected(() =>
            {
                PushPlayer();
                int self = lua_.Top;
                lua_.GetField(self, "messageName");
                lua_.PushValue(self);
                lua_.Push((double)id.Value);
                lua_.PushNil();
                foreach (var a in args) lua_.Push((double)a);
                lua_.Call(3 + args.Length, 0);
                return 0;
            });
        }

        // player:messageName(zones[text.Zone].text[text.Name], player, ...); an unknown name throws, as Special does
        public void MessageNameOfPlayer(ZoneText text, params int[] args)
        {
            NoEffect("send a message");
            int? id = TextId(lua_, text.Zone, text.Name);
            if (id == null) throw new InvalidOperationException($"unknown text {text.Name} in zone {text.Zone}");
            Protected(() =>
            {
                PushPlayer();
                int self = lua_.Top;
                lua_.GetField(self, "messageName");
                lua_.PushValue(self);
                lua_.Push((double)id.Value);
                lua_.PushValue(self);
                foreach (var a in args) lua_.Push((double)a);
                lua_.Call(3 + args.Length, 0);
                return 0;
            });
        }

        // player:messageText(npc, zones[text.Zone].text[text.Name], showName, mode), npc the handler's npc argument (nil when
        // it has none); an unknown name throws, as Special does
        public void MessageText(ZoneText text, bool showName, int mode)
        {
            NoEffect("send a message");
            int? id = TextId(lua_, text.Zone, text.Name);
            if (id == null) throw new InvalidOperationException($"unknown text {text.Name} in zone {text.Zone}");
            Protected(() =>
            {
                PushPlayer();
                int self = lua_.Top;
                lua_.GetField(self, "messageText");
                lua_.PushValue(self);
                if (npc_ == 0) lua_.PushNil(); else lua_.PushValue(npc_);
                lua_.Push((double)id.Value);
                lua_.Push(showName);
                lua_.Push((double)mode);
                lua_.Call(5, 0);
                return 0;
            });
        }

        // player:messageText(player, zones[text.Zone].text[text.Name]): the char its own speaker; an unknown
        // name throws
        public void MessageTextFromPlayer(ZoneText text)
        {
            NoEffect("send a message");
            int? id = TextId(lua_, text.Zone, text.Name);
            if (id == null) throw new InvalidOperationException($"unknown text {text.Name} in zone {text.Zone}");
            Protected(() =>
            {
                PushPlayer();
                int self = lua_.Top;
                lua_.GetField(self, "messageText");
                lua_.PushValue(self);
                lua_.PushValue(self);
                lua_.Push((double)id.Value);
                lua_.Call(3, 0);
                return 0;
            });
        }

        // player:messageText(npc, zones[text.Zone].text[text.Name]), npc the handler's npc argument (nil when it has
        // none): the npc its speaker, showName and mode left nil; an unknown name throws
        public void MessageTextFromNpc(ZoneText text)
        {
            NoEffect("send a message");
            int? id = TextId(lua_, text.Zone, text.Name);
            if (id == null) throw new InvalidOperationException($"unknown text {text.Name} in zone {text.Zone}");
            Protected(() =>
            {
                PushPlayer();
                int self = lua_.Top;
                lua_.GetField(self, "messageText");
                lua_.PushValue(self);
                if (npc_ == 0) lua_.PushNil(); else lua_.PushValue(npc_);
                lua_.Push((double)id.Value);
                lua_.Call(3, 0);
                return 0;
            });
        }

        // player:messageSpecial(textId, ...): an id from Id plus an offset
        public void Special(int textId, params int[] args)
        {
            NoEffect("send a message");
            var all = new object[args.Length + 1];
            all[0] = textId;
            for (int i = 0; i < args.Length; i++) all[i + 1] = args[i];
            PlayerCall("messageSpecial", 0, () => 0, all);
        }

        // zones[text.Zone].text[text.Name]; an unknown name throws (Lua's nil + offset raises too)
        public int Id(ZoneText text) =>
            TextId(lua_, text.Zone, text.Name) ?? throw new InvalidOperationException($"unknown text {text.Name} in zone {text.Zone}");

        // ---- the npc ----

        // npc:getID(), npc:getLocalVar(name), npc:setLocalVar(name, value) on the call's npc argument
        // optParams.<field> read from the table at its stack slot when asked (quest rework P7): a boolean by Lua's
        // truthiness, a number as the engine's llround reads it; a field the table lacks reads false / 0
        private sealed class LiveMobDeath : IQuestMobDeath
        {
            private readonly LiveQuestContext ctx_;
            private readonly int slot_;

            public LiveMobDeath(LiveQuestContext ctx, int slot)
            {
                ctx_ = ctx;
                slot_ = slot;
            }

            public bool IsKiller => Flag("isKiller");
            public bool NoKiller => Flag("noKiller");
            public bool IsWeaponSkillKill => Flag("isWeaponSkillKill");
            public int WeaponskillUsed => Number("weaponskillUsed");
            public int WeaponskillDamage => Number("weaponskillDamage");

            private bool Flag(string field) => ctx_.Protected(() =>
            {
                ctx_.lua_.GetField(slot_, field);
                return ctx_.lua_.ToBoolean(-1);
            });
            private int Number(string field) => ctx_.Protected(() =>
            {
                ctx_.lua_.GetField(slot_, field);
                return ctx_.lua_.IsNumber(-1) ? (int)ctx_.lua_.ToIntegral(-1) : 0;
            });
        }

        private sealed partial class LiveQuestNpc : IQuestNpc
        {
            private readonly LiveQuestContext ctx_;
            // the entity's stack slot: the call's npc, or its mob (Mob)
            private readonly int slot_;

            public LiveQuestNpc(LiveQuestContext ctx, int slot)
            {
                ctx_ = ctx;
                slot_ = slot;
            }

            public int Id => ctx_.Method(slot_, "getID", 1, () => (int)ctx_.lua_.ToIntegral(-1));
            // npc:getName()
            public string Name => ctx_.Method(slot_, "getName", 1, () => ctx_.lua_.ToStringValue(-1));
            public int LocalVar(string name) => ctx_.Method(slot_, "getLocalVar", 1, () => (int)ctx_.lua_.ToIntegral(-1), name);
            // player:checkDistance(npc)
            public double PlayerDistance => ctx_.Protected(() =>
            {
                ctx_.PushPlayer();
                int player = ctx_.lua_.Top;
                ctx_.lua_.GetField(player, "checkDistance");
                ctx_.lua_.PushValue(player);
                ctx_.lua_.PushValue(slot_);
                ctx_.lua_.Call(2, 1);
                return ctx_.lua_.ToNumber(-1);
            });
            public void SetLocalVar(string name, int value)
            {
                ctx_.NoEffect("set an npc's local var");
                ctx_.Method(slot_, "setLocalVar", 0, () => 0, name, value);
            }
            // npc:showText(npc, zones[text.Zone].text[text.Name], args...): the npc both speaker and self
            public void ShowText(ZoneText text, params int[] args)
            {
                ctx_.NoEffect("show a text");
                int? id = TextId(ctx_.lua_, text.Zone, text.Name);
                if (id == null) throw new InvalidOperationException($"unknown text {text.Name} in zone {text.Zone}");
                ctx_.Protected(() =>
                {
                    var l = ctx_.lua_;
                    l.PushValue(slot_);
                    int npc = l.Top;
                    l.GetField(npc, "showText");
                    l.PushValue(npc);
                    l.PushValue(npc);
                    l.Push((double)id.Value);
                    foreach (var a in args) l.Push((double)a);
                    l.Call(args.Length + 3, 0);
                    return 0;
                });
            }
            // npc:messageText(player, zones[text.Zone].text[text.Name]): the npc the speaker, the char the entity
            public void MessageText(ZoneText text)
            {
                ctx_.NoEffect("show a message text");
                int? id = TextId(ctx_.lua_, text.Zone, text.Name);
                if (id == null) throw new InvalidOperationException($"unknown text {text.Name} in zone {text.Zone}");
                ctx_.Protected(() =>
                {
                    var l = ctx_.lua_;
                    l.PushValue(slot_);
                    int npc = l.Top;
                    l.GetField(npc, "messageText");
                    l.PushValue(npc);
                    ctx_.PushPlayer();
                    l.Push((double)id.Value);
                    l.Call(3, 0);
                    return 0;
                });
            }
            // npc:hideNPC(seconds)
            public void Hide(int seconds)
            {
                ctx_.NoEffect("hide an npc");
                ctx_.Method(slot_, "hideNPC", 0, () => 0, seconds);
            }
            // mob:messageText(mob, zones[text.Zone].text[text.Name]): the entity both self and speaker
            public void MessageTextOwn(ZoneText text)
            {
                ctx_.NoEffect("show a text");
                int? id = TextId(ctx_.lua_, text.Zone, text.Name);
                if (id == null) throw new InvalidOperationException($"unknown text {text.Name} in zone {text.Zone}");
                ctx_.Protected(() =>
                {
                    var l = ctx_.lua_;
                    l.PushValue(slot_);
                    int mob = l.Top;
                    l.GetField(mob, "messageText");
                    l.PushValue(mob);
                    l.PushValue(mob);
                    l.Push((double)id.Value);
                    l.Call(3, 0);
                    return 0;
                });
            }
            // npc:getXPos() / getYPos() / getZPos()
            public double XPos => ctx_.Method(slot_, "getXPos", 1, () => ctx_.lua_.ToNumber(-1));
            public double YPos => ctx_.Method(slot_, "getYPos", 1, () => ctx_.lua_.ToNumber(-1));
            public double ZPos => ctx_.Method(slot_, "getZPos", 1, () => ctx_.lua_.ToNumber(-1));
            // npcUtil.queueMove(npc, { x, y, z })
            public void QueueMove(double x, double y, double z)
            {
                ctx_.NoEffect("move an npc");
                ctx_.Protected(() =>
                {
                    var l = ctx_.lua_;
                    l.GetPath("npcUtil.queueMove");
                    l.PushValue(slot_);
                    l.NewTable();
                    l.Push(x);
                    l.RawSetI(-2, 1);
                    l.Push(y);
                    l.RawSetI(-2, 2);
                    l.Push(z);
                    l.RawSetI(-2, 3);
                    l.Call(2, 0);
                    return 0;
                });
            }
            // npc:setPos(x, y, z)
            public void SetPos(double x, double y, double z)
            {
                ctx_.NoEffect("move an npc");
                ctx_.Method(slot_, "setPos", 0, () => 0, x, y, z);
            }
            // npc:getAnimation() / npc:setAnimation(animation)
            public int Animation => ctx_.Method(slot_, "getAnimation", 1, () => (int)ctx_.lua_.ToIntegral(-1));
            public void SetAnimation(int animation)
            {
                ctx_.NoEffect("set an npc's animation");
                ctx_.Method(slot_, "setAnimation", 0, () => 0, animation);
            }
            // npc:getStatus() / npc:setStatus(status)
            public int Status => ctx_.Method(slot_, "getStatus", 1, () => (int)ctx_.lua_.ToIntegral(-1));
            public void SetStatus(int status)
            {
                ctx_.NoEffect("set an npc's status");
                ctx_.Method(slot_, "setStatus", 0, () => 0, status);
            }
            // npc:entityAnimationPacket(animation)
            public void EntityAnimationPacket(string animation)
            {
                ctx_.NoEffect("send an npc's animation");
                if (animation == null) throw new ArgumentNullException(nameof(animation));
                ctx_.Method(slot_, "entityAnimationPacket", 0, () => 0, animation);
            }
            // npc:clearPath(pause) / npc:continuePath()
            public void ClearPath(bool pause)
            {
                ctx_.NoEffect("stop an npc's path");
                ctx_.Method(slot_, "clearPath", 0, () => 0, pause);
            }
            public void ContinuePath()
            {
                ctx_.NoEffect("continue an npc's path");
                ctx_.Method(slot_, "continuePath", 0, () => 0);
            }
            // npc:facePlayer(player, nonGlobal): the handler's char
            public void FacePlayer(bool nonGlobal)
            {
                ctx_.NoEffect("turn an npc");
                ctx_.Protected(() =>
                {
                    var l = ctx_.lua_;
                    l.PushValue(slot_);
                    int npc = l.Top;
                    l.GetField(npc, "facePlayer");
                    l.PushValue(npc);
                    ctx_.PushPlayer();
                    l.Push(nonGlobal);
                    l.Call(3, 0);
                    return 0;
                });
            }
            // require('scripts/zones/Attohwa_Chasm/globals').handleLuminantUsed(npc)
            public void AttohwaChasmHandleLuminantUsed()
            {
                ctx_.NoEffect("use a luminant");
                ctx_.Protected(() =>
                {
                    var l = ctx_.lua_;
                    l.GetGlobal("require");
                    l.PushString("scripts/zones/Attohwa_Chasm/globals");
                    l.Call(1, 1);
                    l.GetField(-1, "handleLuminantUsed");
                    l.PushValue(slot_);
                    l.Call(1, 0);
                    return 0;
                });
            }
        }

        // GetNPCByID(id), null when nil: the NPC kept at the top of this handler's stack (one slot per lookup) for the
        // facet's calls, as a call's own npc argument is
        public IQuestNpc NpcById(int id)
        {
            if (!lua_.CheckStack(4)) throw new InvalidOperationException("no Lua stack left for an NPC lookup");
            lua_.GetGlobal("GetNPCByID");
            lua_.Push((double)id);
            lua_.Call(1, 1);
            if (lua_.IsNil(-1))
            {
                lua_.Pop(1);
                return null;
            }
            return new LiveQuestNpc(this, lua_.Top);
        }

        // ---- Lua ----

        // zones[zone].text[name], null when the zone or the name has none
        internal static int? TextId(LuaState l, ushort zone, string name)
        {
            int top = l.Top;
            try
            {
                l.GetGlobal("zones");
                if (!l.IsTable(-1)) return null;
                l.RawGetI(-1, zone);
                if (!l.IsTable(-1)) return null;
                l.GetField(-1, "text");
                if (!l.IsTable(-1)) return null;
                l.GetField(-1, name);
                return l.IsNumber(-1) ? (int)l.ToIntegral(-1) : null;
            }
            finally
            {
                l.SetTop(top);
            }
        }

        // xi.quest.id[xi.quest.area[log]][constName]
        private int QuestId(QuestLog log, string constName)
        {
            int top = lua_.Top;
            try
            {
                lua_.GetPath("xi.quest.area");
                if (lua_.IsTable(-1)) lua_.RawGetI(-1, (int)log); else lua_.PushNil();
                string area = lua_.IsString(-1) ? lua_.ToStringValue(-1) : null;
                lua_.GetPath("xi.quest.id");
                if (area != null && lua_.IsTable(-1)) lua_.GetField(-1, area); else lua_.PushNil();
                if (lua_.IsTable(-1)) lua_.GetField(-1, constName); else lua_.PushNil();
                if (!lua_.IsNumber(-1)) throw new InvalidOperationException($"unknown quest constant {(int)log}/{constName}");
                return (int)lua_.ToIntegral(-1);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        private void PushPlayer()
        {
            if (player_ == 0 || lua_.IsNil(player_)) throw new InvalidOperationException("no player in this handler");
            lua_.PushValue(player_);
        }

        // quest:<method>([player,] args...), the result read by read before the stack is restored
        private T QuestCall<T>(string method, int nresults, bool withPlayer, Func<T> read, params object[] args) => Protected(() =>
        {
            lua_.PushRef(quest_.Container);
            int quest = lua_.Top;
            lua_.GetField(quest, method);
            lua_.PushValue(quest);
            int n = 1;
            if (withPlayer)
            {
                PushPlayer();
                n++;
            }
            foreach (var a in args)
            {
                lua_.Push(a);
                n++;
            }
            lua_.Call(n, nresults);
            return read();
        });

        // player:<method>(args...)
        private T PlayerCall<T>(string method, int nresults, Func<T> read, params object[] args)
        {
            if (player_ == 0 || lua_.IsNil(player_)) throw new InvalidOperationException("no player in this handler");
            return Method(player_, method, nresults, read, args);
        }

        // <the value at self>:<method>(args...)
        private T Method<T>(int self, string method, int nresults, Func<T> read, params object[] args) => Protected(() =>
        {
            lua_.PushValue(self);
            int obj = lua_.Top;
            lua_.GetField(obj, method);
            lua_.PushValue(obj);
            foreach (var a in args) lua_.Push(a);
            lua_.Call(args.Length + 1, nresults);
            return read();
        });

        private T Protected<T>(Func<T> body)
        {
            int top = lua_.Top;
            try
            {
                return body();
            }
            finally
            {
                lua_.SetTop(top);
            }
        }
    }
}
