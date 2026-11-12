using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests.Authoring;
using XI.Script;

namespace XI.Zone
{
    // Quest rework P10 (docs/superpowers/specs/2026-10-28-quest-deferred-callbacks-design.md): a C# quest's deferred
    // callbacks. A Lua quest hands the engine a closure
    // (npc:timer(5000, function(npcArg) ... end), player:addListener('TICK', id, function(playerArg) ... end)); a C#
    // quest arms a callback it declared (QuestScriptBuilder.Callback) through the very same bindings, with a Lua
    // closure that holds the callback's C# function and two numbers (how it fires and the char that armed it), nothing
    // of the handler's Lua state. So the deadline (the timer binding's NowMs() + ms, run on the first tick strictly
    // past it: CAIActionQueue::checkAction's `tick > deadline`, lua_base_entity.cpp CLuaBaseEntity::timer),
    // the order against Lua timers armed in the same tick, the TICK listeners' place before the action queue
    // (CAIContainer::Tick) and the clearing on a zone change or a logout (ClearActionQueues, ReleaseListeners) are the
    // engine's, for a Lua quest and a C# one alike. When it fires, the callback gets a fresh LiveQuestContext of the
    // entity the engine passes.
    //
    // The function is made once per callback and registration (QuestScriptBinding.CallbackRef), not per arming:
    // LuaState keeps every C# function it is given (functions_ is never freed), so a function per timer would grow it
    // with every arming (Mimeo Stone re-arms for each char every 50 to 90 s). The closure CSharpQuests.callback makes
    // per arming is plain Lua, collected once the queue or the listener lets go of it.
    //
    // Exports (b.Export): a Lua global set to a function running a callback with (player), for a script that calls the
    // quest's Lua function by name (scripts/effects/healing.lua: xi.dancesWithLuopans.onHealing), with the Lua module
    // the quest stands in for claimed in package.loaded, so the require of its deleted file still loads (ClaimExports).
    public sealed partial class ZoneHost
    {
        // how a callback's closure fires, its second argument: on the char it was armed on
        // (player:timer, a TICK listener), on an NPC or mob (npc:timer), or on one with the char that armed it
        // (WithPlayer.Yes), its id the third
        internal const int CallbackOnChar = 0, CallbackOnEntity = 1, CallbackOnEntityWithChar = 2;

        // the Lua side: the arming's closure, the claimed modules' sentinels and the dotted-path helpers
        private void RegisterQuestCallbackGlobals()
        {
            lua_.DoString(
                // a timer's closure gets (entity), a TICK listener's (entity, elapsed): the elapsed milliseconds are
                // not passed on (no quest reads them)
                "CSharpQuests.callback = function(raw, mode, charId)\n" +
                "    return function(entity) raw(entity, mode, charId) end\n" +
                "end\n" +
                // module -> the table the bridge put in package.loaded for it (its own, told from a Lua module's)
                "CSharpQuests.claimed = CSharpQuests.claimed or {}\n" +
                // 0: nothing loaded; 1: the bridge's claim; 2: a Lua module's own value
                "CSharpQuests.moduleState = function(module)\n" +
                "    local v = package.loaded[module]\n" +
                "    if v == nil then return 0 elseif v == CSharpQuests.claimed[module] then return 1 else return 2 end\n" +
                "end\n" +
                "CSharpQuests.claim = function(module)\n" +
                "    local s = CSharpQuests.claimed[module] or {}\n" +
                "    CSharpQuests.claimed[module] = s\n" +
                "    package.loaded[module] = s\n" +
                "end\n" +
                "CSharpQuests.getPath = function(path)\n" +
                "    local t = _G\n" +
                "    for part in string.gmatch(path, '[^.]+') do\n" +
                "        if type(t) ~= 'table' then return nil end\n" +
                "        t = t[part]\n" +
                "    end\n" +
                "    return t\n" +
                "end\n" +
                "CSharpQuests.setPath = function(path, value)\n" +
                "    local parts = {}\n" +
                "    for part in string.gmatch(path, '[^.]+') do parts[#parts + 1] = part end\n" +
                "    local t = _G\n" +
                "    for i = 1, #parts - 1 do\n" +
                "        if type(t[parts[i]]) ~= 'table' then t[parts[i]] = {} end\n" +
                "        t = t[parts[i]]\n" +
                "    end\n" +
                "    t[parts[#parts]] = value\n" +
                "end", "=CSharpQuests");
        }

        // the callback's C# function, which its closures call with (entity, mode, charId); one per callback and
        // registration
        private LuaRef CallbackFunction(QuestScriptBinding b, QuestCallback cb)
        {
            lua_.PushFunction(fl => RunCallback(fl, b, cb, fl.IsNumber(2) ? (int)fl.ToIntegral(2) : CallbackOnChar,
                                                fl.IsNumber(3) ? (uint)fl.ToIntegral(3) : 0));
            return lua_.Ref();
        }

        // the label coverage counts a callback by: state 0 (the quest's level), no entity, callback:<name>
        internal static string CallbackLabel(QuestCallback cb) => $"0/-/callback:{cb.Name}";

        // the callback run with a context of the entity at 1: the char (CallbackOnChar), or the NPC / mob as its Npc
        // with no Player (CallbackOnEntity), or with the char of the id as its Player too (CallbackOnEntityWithChar).
        // That char is found by id among this process's chars, in the entity's area (SameArea): a Lua closure keeps the
        // player userdata, and if the char zoned or logged out in the meantime C++ calls through a dangling entity, so
        // here it does not run and the line says so (design section 3.2). A callback that throws is logged, nothing
        // returned: the queue or the listeners go on
        private int RunCallback(LuaState l, QuestScriptBinding b, QuestCallback cb, int mode, uint charId)
        {
            int top = l.Top;
            string name = $"callback {cb.Name}";
            try
            {
                LiveQuestContext ctx;
                if (mode == CallbackOnChar) ctx = new LiveQuestContext(l, log_, b, name, player: 1);
                else
                {
                    object entity = l.ToObject(1);
                    if (entity == null) throw new InvalidOperationException("no entity to fire on");
                    int player = 0;
                    if (mode == CallbackOnEntityWithChar)
                    {
                        var c = charSessions_.Keys.FirstOrDefault(x => x.Id == charId && SameArea(x, entity));
                        if (c == null)
                        {
                            log_($"quests: {b.Key} {name}: its char {charId} is gone");
                            return 0;
                        }
                        l.PushObject(c);
                        player = l.Top;
                    }
                    ctx = new LiveQuestContext(l, log_, b, name, player: player, npc: 1, zone: ZoneOf(entity));
                }
                HandlerCalled?.Invoke(b.Key, CallbackLabel(cb));
                cb.Run(ctx);
            }
            catch (Exception e)
            {
                log_($"quests: {b.Key} {name}: {e.Message}");
            }
            finally
            {
                l.SetTop(top);
            }
            return 0;
        }

        // ~CCharEntity: the char's CAIContainer goes, its EventHandler's listeners with it (design section 2): their
        // Lua functions released from the registry. The zone-in builds a new CharData with none, so TickListeners never
        // reaches these again; without this their LuaRefs stayed in the registry for good
        // (Lua listeners and C# callbacks alike)
        private void ReleaseListeners(CharData c)
        {
            if (lua_ != null) c.Events.Clear(lua_);
        }

        // ---- exports ----------------------------------------------------------------------------------------------

        // the modules and globals the exports of this register pass claimed, by quest key: a second quest naming one is
        // a registration problem (ExportProblems)
        private readonly Dictionary<string, string> exportModules_ = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> exportGlobals_ = new Dictionary<string, string>(StringComparer.Ordinal);
        // "<key> <global>" of the refused quests' no-op exports that have logged (once per process)
        private readonly HashSet<string> refusedExportsLogged_ = new HashSet<string>(StringComparer.Ordinal);

        // the module an export claims: its own, else the quest's LegacyPath (null when neither)
        private static string ExportModule(QuestExport e, string legacyPath) => e.Module ?? legacyPath;

        // package.loaded[module]: 0 nothing, 1 the bridge's claim, 2 a Lua module's
        private static int ModuleState(LuaState l, string module)
        {
            int top = l.Top;
            try
            {
                l.GetPath("CSharpQuests.moduleState");
                l.PushString(module);
                l.Call(1, 1);
                return (int)l.ToIntegral(-1);
            }
            finally
            {
                l.SetTop(top);
            }
        }

        // the exports' registration problems: a module or a global another quest of this pass claimed, no module to
        // claim, and, for a module nothing loaded, a global another script already set
        private List<string> ExportProblems(LuaState l, string key, QuestScriptDefinition def, string legacyPath)
        {
            var problems = new List<string>();
            foreach (var e in def.Exports)
            {
                string module = ExportModule(e, legacyPath);
                if (module == null)
                {
                    problems.Add($"export {e.Global}: no module to claim (no replacesModule and no LegacyPath)");
                    continue;
                }
                if (exportModules_.TryGetValue(module, out var m) && m != key) problems.Add($"export {e.Global}: the module {module} is {m}'s export");
                if (exportGlobals_.TryGetValue(e.Global, out var g) && g != key) problems.Add($"export {e.Global}: the global is {g}'s export");
                if (ModuleState(l, module) == 0)
                {
                    int top = l.Top;
                    l.GetPath("CSharpQuests.getPath");
                    l.PushString(e.Global);
                    l.Call(1, 1);
                    bool set = !l.IsNil(-1);
                    l.SetTop(top);
                    if (set) problems.Add($"export {e.Global}: another script already sets the global");
                }
            }
            return problems;
        }

        // the container's exports after its registration, by the state of package.loaded[module] (design section 3.4):
        // nothing loaded: the bridge claims it (CSharpQuests.claim) and sets each global; the bridge's own claim
        // (a reload(true)): kept, the globals set again; a Lua module's table
        // (the container's file still there and loaded: a twin, a port not yet deleted, a quest in CSHARP_QUESTS_OFF):
        // the bridge stands down, the Lua's own functions stay. A registered container's globals run its callbacks
        // (context as a player timer's: the first argument the char); a container not registered
        // (refused, switched off, CSHARP_QUESTS false) still claims the module, its globals set to a no-op that logs
        // once per process, so a script requiring the deleted file (healing.lua) still loads and does the rest of what
        // it does. A module or a global another container of this pass claimed is left to it
        // (that container's refusal is logged). Not run for a container refused because another C# container has its
        // key: the ownership checks below go by key, so its no-ops would replace the first container's functions
        // (RegisterQuestScripts skips it)
        private void ClaimExports(LuaState l, ContainerScript script, QuestScriptDefinition own, string key,
                                  QuestScriptBinding bound)
        {
            var def = bound?.Def ?? own;
            if (def == null || def.Exports.Count == 0 || key == null) return;
            int top = l.Top;
            try
            {
                foreach (var group in def.Exports.GroupBy(e => ExportModule(e, script?.LegacyPath)))
                {
                    string module = group.Key;
                    if (module == null || exportModules_.TryGetValue(module, out var owner) && owner != key) continue;
                    if (ModuleState(l, module) == 2) continue;
                    exportModules_[module] = key;
                    l.GetPath("CSharpQuests.claim");
                    l.PushString(module);
                    l.Call(1, 0);
                    foreach (var e in group)
                    {
                        if (exportGlobals_.TryGetValue(e.Global, out var g) && g != key) continue;
                        exportGlobals_[e.Global] = key;
                        l.GetPath("CSharpQuests.setPath");
                        l.PushString(e.Global);
                        var cb = e.Callback;
                        string global = e.Global;
                        if (bound != null && def.Callbacks.Contains(cb))
                            l.PushFunction(fl => RunCallback(fl, bound, cb, CallbackOnChar, 0));
                        else
                            l.PushFunction(fl =>
                            {
                                if (refusedExportsLogged_.Add($"{key} {global}")) log_($"quests: {key} refused: {global} not run");
                                return 0;
                            });
                        l.Call(2, 0);
                    }
                }
            }
            catch (Exception e)
            {
                log_($"quests: {key} exports not set: {e.Message}");
            }
            finally
            {
                l.SetTop(top);
            }
        }
    }

    internal sealed partial class LiveQuestContext
    {
        // ---- deferred callbacks (quest rework P10) ----

        // player:timer(ms, fn)
        public void Timer(int ms, QuestCallback callback) => ArmTimer(player_, "player", ms, callback, ZoneHost.CallbackOnChar);

        // <the entity at self>:timer(ms, <the callback's closure>): the entity's own timer binding
        // (CLuaBaseEntity::timer, ZoneHost.QueueAction), so the deadline and the queue are the engine's. With the char
        // (CallbackOnEntityWithChar) the closure carries the handler's char id
        internal void ArmTimer(int self, string what, int ms, QuestCallback callback, int mode)
        {
            NoEffect("arm a timer");
            CheckCallback(callback);
            if (self == 0 || lua_.IsNil(self)) throw new InvalidOperationException($"no {what} in this handler");
            // C++ takes an int ms and fires a negative one at once: a slip, refused when armed
            if (ms < 0) throw new ArgumentOutOfRangeException(nameof(ms), ms, "a timer's delay is 0 ms or more");
            uint charId = mode == ZoneHost.CallbackOnEntityWithChar ? (uint)PlayerCall("getID", 1, () => (long)lua_.ToIntegral(-1)) : 0;
            Protected(() =>
            {
                lua_.PushValue(self);
                int obj = lua_.Top;
                lua_.GetField(obj, "timer");
                lua_.PushValue(obj);
                lua_.Push((double)ms);
                PushCallback(callback, mode, charId);
                lua_.Call(3, 0);
                return 0;
            });
        }

        // player:addListener(evt, identifier, <the callback's closure>), evt TICK only
        public void AddListener(string evt, string identifier, QuestCallback callback)
        {
            NoEffect("add a listener");
            CheckCallback(callback);
            if (evt != "TICK") throw new ArgumentException($"a quest listener's event is TICK only, not {evt}", nameof(evt));
            if (identifier == null) throw new ArgumentNullException(nameof(identifier));
            if (player_ == 0 || lua_.IsNil(player_)) throw new InvalidOperationException("no player in this handler");
            Protected(() =>
            {
                lua_.PushValue(player_);
                int obj = lua_.Top;
                lua_.GetField(obj, "addListener");
                lua_.PushValue(obj);
                lua_.PushString(evt);
                lua_.PushString(identifier);
                PushCallback(callback, ZoneHost.CallbackOnChar, 0);
                lua_.Call(4, 0);
                return 0;
            });
        }

        // player:removeListener(identifier)
        public void RemoveListener(string identifier)
        {
            NoEffect("remove a listener");
            if (identifier == null) throw new ArgumentNullException(nameof(identifier));
            PlayerCall("removeListener", 0, () => 0, identifier);
        }

        // a callback of this quest's definition only (a handle of another quest's would run with this quest's binding)
        private void CheckCallback(QuestCallback callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (!quest_.Def.Callbacks.Contains(callback)) throw new InvalidOperationException($"callback {callback.Name} is not {quest_.Key}'s");
        }

        // CSharpQuests.callback(<the callback's function>, mode, charId): the closure the engine keeps
        private void PushCallback(QuestCallback callback, int mode, uint charId)
        {
            lua_.GetPath("CSharpQuests.callback");
            lua_.PushRef(quest_.CallbackRef(callback));
            lua_.Push((double)mode);
            lua_.Push((double)charId);
            lua_.Call(3, 1);
        }

        private sealed partial class LiveQuestNpc
        {
            // npc:timer(ms, fn), with or without the char
            public void Timer(int ms, QuestCallback callback) => Timer(ms, callback, WithPlayer.No);
            public void Timer(int ms, QuestCallback callback, WithPlayer withPlayer) =>
                ctx_.ArmTimer(slot_, "npc", ms, callback, withPlayer == WithPlayer.Yes ? ZoneHost.CallbackOnEntityWithChar : ZoneHost.CallbackOnEntity);
        }
    }
}
