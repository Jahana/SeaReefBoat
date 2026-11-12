using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using XI.Script;

namespace XI.Zone
{
    // Quest rework C2 (docs/superpowers/specs/2026-11-05-quest-rework-c2-mission-platform-design.md, sections 3.2 and
    // 3.7): the missions written in C# (every MissionScript of XI.QuestContent, and the verify suites' test ones) run
    // inside the Lua InteractionLookup in place of their scripts/missions files, as the C# quests do. Each becomes a
    // real Lua Mission container (Mission:new(log, id), its reward in npcUtil.completeMission's param shape, its
    // sections) whose section checks and handlers are C# closures, and takes the place of the Lua container with its id
    // (Mission[log][id]) at the same index, or the slot of its LegacyPath when the Lua file is gone. Its handlers are
    // the quest bridge's (HandlerClosure, PushReply) over the mission's states; q.Mission calls the container's own Lua
    // methods (mission:begin, mission:complete, getVar, ...), so the packets, the blob and the rows are the Lua file's.
    public sealed partial class ZoneHost
    {
        // the MissionScripts of XI.QuestContent, each instantiated and compiled once, in type-name order
        private List<CompiledMissionScript> missionScripts_ = new List<CompiledMissionScript>();

        private sealed class CompiledMissionScript : CompiledScript<MissionScript, MissionScriptDefinition>
        {
        }

        // the MissionScripts of the containers (DiscoverContainers), each compiled once, in type-name order; a mission
        // whose constructor or Compile threw is kept with the error
        private static List<CompiledMissionScript> DiscoverMissionScripts(List<DiscoveredContainer> containers)
        {
            var list = DiscoverScripts<CompiledMissionScript, MissionScript>(containers);
            foreach (var m in list) CompileScript(m, s => s.Compile());
            return list;
        }

        private static CompiledMissionScript CompiledMission(MissionScript s)
        {
            var m = new CompiledMissionScript { TypeName = s.GetType().FullName, Script = s };
            CompileScript(m, x => x.Compile());
            return m;
        }

        // a test mission compiled once per instance (MissionHandlerLabels asks per replayed flow)
        private readonly Dictionary<MissionScript, CompiledMissionScript> compiledTestMissions_ =
            new Dictionary<MissionScript, CompiledMissionScript>(ReferenceEqualityComparer.Instance);

        private CompiledMissionScript CompiledTestMission(MissionScript s) =>
            compiledTestMissions_.TryGetValue(s, out var m) ? m : compiledTestMissions_[s] = CompiledMission(s);

        // every declared handler's label of the C# mission with the key, as a quest's (HandlerLabels); null when no C#
        // mission has it
        private IReadOnlyList<string> MissionHandlerLabels(string missionKey)
        {
            var tests = TestQuestScripts.OfType<MissionScript>().Select(CompiledTestMission);
            foreach (var m in missionScripts_.Concat(tests))
                if (m.Def != null && m.Script.Key == missionKey)
                    return HandlerLabels(WithModules(missionKey, m.Def.Core));
            return null;
        }

        // the registration's checks that need no Lua (public for the verify suites): QuestScriptValidator's mission
        // rules and every NPC or mob a handler names exists in its zone's data
        public IReadOnlyList<string> ValidateMissionScript(MissionScriptDefinition def)
        {
            var problems = new List<string>(QuestScriptValidator.Validate(def));
            problems.AddRange(EntityProblems(def.Core));
            return problems;
        }

        // the missions of the register pass (CSharpQuests.register), after the quests: those with a LegacyPath first,
        // then the others by key, each registered as RegisterMissionScript says, its exports claimed as a quest's;
        // how many registered. keys: the pass's container keys (a mission key is never a quest's: its prefix is
        // mission.)
        private int RegisterMissionScripts(LuaState l, HashSet<string> keys, int top)
        {
            var all = (SkipDiscoveredQuests ? new List<CompiledMissionScript>() : missionScripts_)
                .Concat(TestQuestScripts.OfType<MissionScript>().Select(CompiledMission));
            return RegisterEach(l, LegacyFirst(all, m => m.Script?.LegacyPath, m => m.Script?.Key ?? m.TypeName), top,
                m => m.TypeName,
                (CompiledMissionScript m, ref string name, ref bool duplicate, out QuestScriptBinding bound) =>
                    RegisterMissionScript(l, m, keys, ref name, ref duplicate, out bound),
                (m, name, bound) => ClaimExports(l, m.Script, m.Def?.Core, name, bound));
        }

        // resolves, validates, builds and substitutes one mission; false when it is skipped or refused (logged), the
        // Lua file then in charge. As a quest's (RegisterQuestScript): CSHARP_QUESTS off registers none; a key in
        // CSHARP_QUESTS_OFF leaves the Lua file in charge while it exists, and is logged and kept in C# when it is
        // gone; a mission refused with no Lua file to fall back to is left out, logged with the no-fallback line and
        // counted in QuestLoadFailures. A mission with Called() handlers is an NPC-call one (xi.cs.mission, Task 4),
        // as a quest is: a call-only mission keeps only its Mission object (its vars) and puts no container in the
        // list, and is named in the call-only line when the switches leave it out. name: the mission's key once
        // known, for the caller's log line when this throws
        private bool RegisterMissionScript(LuaState l, CompiledMissionScript m, HashSet<string> keys, ref string name,
                                           ref bool duplicate, out QuestScriptBinding bound)
        {
            bound = null;
            if (m.Script == null)
            {
                if (CSharpQuestsEnabled) log_($"quests: {m.TypeName} not registered: {m.Error}");
                return false;
            }
            int log = (int)m.Script.Log;
            string key = m.Script.Key;
            name = key;
            bool first = keys.Add(key);
            duplicate = !first;
            bool callOnly = m.Def is { Core.CallOnly: true };
            if (!CSharpQuestsEnabled)
            {
                if (callOnly) callOnlyOff_.Add(key);
                return false;
            }
            string area = MissionScript.AreaName(m.Script.Log);
            if (area == null)
            {
                log_($"quests: {key} not registered: unknown mission log {log}");
                return false;
            }
            int? id = MissionConstId(l, area, m.Script.ConstName);
            // the Lua file's container is in the list (the file is there); without one, a refusal has no fallback
            bool hasLua = id != null && HasContainer(l, MissionContainerId(log, id.Value));
            // whether a refusal leaves the Lua in charge: hasLua, or for an unknown constant or a second class with
            // the key (where the container tells nothing) whether the LegacyPath file is there
            bool fallback = hasLua;
            bool Refuse(string reason, IList<string> each = null) =>
                RefuseContainer("mission", key, fallback, reason, each);
            if (CSharpQuestsOff.Contains(key))
            {
                // OFF leaves the Lua file in charge while it exists; after its deletion there is nothing to fall back
                // to, and rollback is a git revert. A call-only mission's Lua is the scripts' branches: OFF leaves
                // them alone, as for a call-only quest
                if (callOnly) callOnlyOff_.Add(key);
                if (id == null || hasLua || callOnly) return false;
                log_($"quests: {key} in CSHARP_QUESTS_OFF has no Lua file; it stays in C#");
            }
            if (!first)
            {
                fallback = LegacyFileExists(l, m.Script.LegacyPath);
                return Refuse($"another C# mission ({m.TypeName}) has the key");
            }
            if (id == null)
            {
                fallback = LegacyFileExists(l, m.Script.LegacyPath);
                return Refuse("unknown mission constant");
            }
            if (m.Def == null) return Refuse(m.Error);
            // the definition with an enabled module's overrides applied (validated with them), else the mission's
            // own; a patched mission is placed as any other here and moved to the end of the list after the pass
            var def = m.Def.WithCore(WithModules(key, m.Def.Core));
            var problems = new List<string>();
            problems.AddRange(ValidateMissionScript(def));
            // a module or global another container of this pass exports, or a global another script sets
            if (def.Core.Exports.Count > 0)
                problems.AddRange(ExportProblems(l, key, def.Core, m.Script.LegacyPath));
            if (problems.Count > 0) return Refuse(string.Join("; ", problems), each: problems);

            var binding = new QuestScriptBinding(key, def.Core, area, id.Value) { Mission = def };
            binding.MakeCallbackRef = cb => CallbackFunction(binding, cb);
            int top = l.Top;
            try
            {
                int mission = BuildMissionContainer(l, binding);
                // every handler is an NPC-call one: only the Mission object stays (its vars), the lookup gets no
                // container
                if (def.Core.CallOnly)
                {
                    npcCallQuests_[key] = binding;
                    bound = binding;
                    return true;
                }
                PlaceContainer(l, key, binding.ContainerId, mission, m.Script.LegacyPath);
                if (def.Core.HasCalled) npcCallQuests_[key] = binding;
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

        // Mission[<log>][<id>]: Mission.getVarPrefix, the container id and var prefix
        internal static string MissionContainerId(int log, int id) => $"Mission[{log}][{id}]";

        // xi.mission.id[area][constName], null when it is not there
        private static int? MissionConstId(LuaState l, string area, string constName)
        {
            int top = l.Top;
            try
            {
                l.GetPath("xi.mission.id");
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

        // local mission = Mission:new(log, id), with its reward and its sections; leaves it on the stack and returns
        // its index
        private int BuildMissionContainer(LuaState l, QuestScriptBinding b)
        {
            var def = b.Mission;
            l.GetGlobal("Mission");
            int cls = l.Top;
            l.GetField(cls, "new");
            l.PushValue(cls);
            l.Push((double)(int)def.Log);
            l.Push((double)b.Id);
            l.Call(3, 1);
            l.Remove(cls);
            int mission = l.Top;
            l.PushValue(mission);
            b.Container = l.Ref();
            // where the handlers come from, for whoever looks at the lookup
            l.PushString("csharp:" + b.Key);
            l.SetField(mission, "filename");

            if (def.Reward is { } reward)
            {
                PushMissionRewardTable(l, reward);
                l.SetField(mission, "reward");
            }

            l.NewTable();
            int sections = l.Top;
            int at = 0;
            for (int i = 0; i < def.States.Count; i++)
            {
                // a state with no handler has no section, as a quest's
                if (def.States[i].Handlers.All(def.Core.IsCalled)) continue;
                BuildSection(l, b, mission, def.States[i], i + 1);
                l.RawSetI(sections, ++at);
            }
            l.SetField(mission, "sections");
            return mission;
        }

        // npcUtil.completeMission's params, only the fields the mission sets (null is not a key, 0 is): item, itemParams,
        // keyItem, gil, bayld, exp, title, rankPoints, rank, nextMission = { log, id }; leaves the table on the stack
        internal static void PushMissionRewardTable(LuaState l, MissionReward r)
        {
            l.NewTable();
            void Set(string k, int? v)
            {
                if (v is not { } value) return;
                l.Push((double)value);
                l.SetField(-2, k);
            }
            Set("item", r.Item);
            if (r.Items != null)
            {
                l.NewTable();
                for (int i = 0; i < r.Items.Length; i++)
                {
                    var item = r.Items[i];
                    if (item.Quantity is { } quantity)
                    {
                        l.NewTable();
                        l.Push((double)item.Id);
                        l.RawSetI(-2, 1);
                        l.Push((double)quantity);
                        l.RawSetI(-2, 2);
                    }
                    else l.Push((double)item.Id);
                    l.RawSetI(-2, i + 1);
                }
                l.SetField(-2, "item");
            }
            PushItemParams(l, r.ItemParams);
            Set("keyItem", r.KeyItem);
            if (r.KeyItems != null)
            {
                l.NewTable();
                for (int i = 0; i < r.KeyItems.Length; i++)
                {
                    l.Push((double)r.KeyItems[i]);
                    l.RawSetI(-2, i + 1);
                }
                l.SetField(-2, "keyItem");
            }
            Set("gil", r.Gil);
            Set("bayld", r.Bayld);
            Set("exp", r.Exp);
            Set("title", r.Title);
            Set("rankPoints", r.RankPoints);
            Set("rank", r.Rank);
            if (r.NextLog is { } nextLog)
            {
                l.NewTable();
                l.Push((double)(int)nextLog);
                l.RawSetI(-2, 1);
                l.Push((double)r.NextMission);
                l.RawSetI(-2, 2);
                l.SetField(-2, "nextMission");
            }
        }

        // check(player, currentMission, missionStatus, vars, varCache): Mission:getCheckArgs's current mission and
        // status (positions 2 and 3), then the lookup's container var cache (4) and var cache (5); the state's mission
        // match read from them (q.Mission.IsCurrent and Status are the arguments, Var reads vars). A check that throws
        // is false
        private LuaFunction MissionSectionCheck(QuestScriptBinding b, QuestStateDecl state, int number)
        {
            string name = $"check {number}";
            return l =>
            {
                int top = l.Top;
                bool result = false;
                try
                {
                    int current = (int)l.ToIntegral(2), status = (int)l.ToIntegral(3);
                    var ctx = new LiveQuestContext(l, log_, b, name, player: 1, vars: l.IsTable(4) ? 4 : 0, check: true,
                                                   missionCurrent: current, missionStatus: status);
                    result = state.IsMissionMatch(ctx);
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
    }

    // q.Mission over the engine (quest rework C2): the mission's calls on its Lua Mission container and on the player,
    // as mission.lua and container.lua make them
    internal sealed partial class LiveQuestContext
    {
        // a mission check's currentMission and missionStatus arguments (MissionSectionCheck); null in a handler
        private readonly int? missionCurrent_, missionStatus_;
        private LiveMissionProgress missionFacet_;

        // the mission's facet; null for a quest's context
        public IMissionProgress Mission =>
            quest_.Mission == null ? null : missionFacet_ ??= new LiveMissionProgress(this);
        IMissionProgressView IQuestCheck.Mission => Mission;

        private sealed class LiveMissionProgress : IMissionProgress
        {
            private readonly LiveQuestContext c_;

            public LiveMissionProgress(LiveQuestContext c) => c_ = c;

            private int Log => (int)c_.quest_.Mission.Log;
            private int Id => c_.quest_.Id;
            // the call's one result as an integer
            private int ReadInt() => (int)c_.lua_.ToIntegral(-1);

            // in a check the currentMission argument, in a handler player:getCurrentMission(log)
            public bool IsCurrent =>
                (c_.missionCurrent_ ?? c_.PlayerCall("getCurrentMission", 1, ReadInt, Log)) == Id;
            // in a check the missionStatus argument, in a handler player:getMissionStatus(log)
            public int Status =>
                c_.missionStatus_ ?? c_.PlayerCall("getMissionStatus", 1, ReadInt, Log);
            // player:getMissionStatus(log, pos)
            public int StatusAt(int pos) => c_.PlayerCall("getMissionStatus", 1, ReadInt, Log, pos);
            // in a check vars[name] (the lookup's cache over mission:getVar), in a handler mission:getVar(player, name)
            public int Var(string name) => c_.Var(name);
            // mission:getMustZone(player)
            public bool MustZone => c_.MustZone;
            // mission:getLocalVar(player, name)
            public int LocalVar(string name) => ((IQuestProgressView)c_).LocalVar(name);

            // mission:begin(player)
            public void Begin()
            {
                c_.NoEffect("begin the mission");
                c_.QuestCall("begin", 0, true, () => 0);
            }

            // mission:complete(player): npcUtil.completeMission with the container's reward, the status reset except on
            // CoP and the cleanup, all in the Lua
            public bool Complete()
            {
                c_.NoEffect("complete the mission");
                return c_.QuestCall("complete", 1, true, () => c_.lua_.ToBoolean(-1));
            }

            // player:completeMission(log, id)
            public bool CompleteRaw()
            {
                c_.NoEffect("complete the mission");
                c_.PlayerCall("completeMission", 0, () => 0, Log, Id);
                return true;
            }

            // player:setMissionStatus(log, value)
            public void SetStatus(int value)
            {
                c_.NoEffect("set the mission status");
                c_.PlayerCall("setMissionStatus", 0, () => 0, Log, value);
            }

            // player:setMissionStatus(log, value, pos)
            public void SetStatusAt(int value, int pos)
            {
                c_.NoEffect("set the mission status");
                c_.PlayerCall("setMissionStatus", 0, () => 0, Log, value, pos);
            }

            // mission:setVar(player, name, value)
            // mission:setVar(player, name, value, expiry)
            public void SetVar(string name, int value, uint expiry = 0) => c_.SetVar(name, value, expiry);
            // mission:setMustZone(player)
            public void SetMustZone() => c_.SetMustZone();
            // mission:setLocalVar(player, name, value)
            public void SetLocalVar(string name, int value) => ((IQuestProgress)c_).SetLocalVar(name, value);
            // Container:isVarBitsSet(player, name, bits...), setVarBit(player, name, bit) and
            // incrementVar(player, name, value)
            public bool IsVarBitsSet(string name, params int[] bits) => c_.IsVarBitsSet(name, bits);
            public void SetVarBit(string name, int bit) => c_.SetVarBit(name, bit);
            public void IncrementVar(string name, int value) => c_.IncrementVar(name, value);
            // mission:cleanup(player)
            public void Cleanup() => c_.Cleanup();
            // for _, member in ipairs(player:getAlliance()) do mission:setLocalVar(member, name, value) ... end
            public void SetAllianceLocalVars(params (string Name, int Value)[] vars) => c_.AllianceLocalVars(vars);
        }
    }
}
