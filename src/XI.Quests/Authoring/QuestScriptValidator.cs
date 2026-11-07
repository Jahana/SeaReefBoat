// XI.Quests -- offline checks on a compiled quest script (quest rework B2): the slips the compiler can't catch.
using System.Collections.Generic;
using System.Linq;

namespace XI.Quests.Authoring
{
    public static class QuestScriptValidator
    {
        // the problems, in declaration order; empty when the script is clean
        public static IReadOnlyList<string> Validate(QuestScriptDefinition def)
        {
            var problems = new List<string>();

            // a matched step (other than 0) must be one the quest declares it sets
            var reported = new HashSet<int>();
            foreach (var state in def.States)
                foreach (var m in state.Matches)
                    if (m.Step.HasValue && m.Step.Value != 0 && !def.DeclaredSteps.Contains(m.Step.Value) && reported.Add(m.Step.Value))
                        problems.Add($"step {m.Step.Value} is matched but never set");

            problems.AddRange(HandlerProblems(def));

            // reward.itemParams are npcUtil.giveItem's params: npcUtil.completeQuest passes them only with an item
            // (npc_util.lua:665-669), so without one they would be dropped
            if (def.Reward is { ItemParams: not null } reward && reward.Item == null && (reward.Items == null || reward.Items.Length == 0))
                problems.Add("reward.itemParams with no item: npcUtil.completeQuest passes them to npcUtil.giveItem only with reward.item / reward.items");

            problems.AddRange(CallbackProblems(def));
            if (def.Hidden) problems.AddRange(HiddenProblems(def));
            else if (def.HasCalled) problems.AddRange(CallProblems(def));
            return problems;
        }

        // the rules every container kind shares (quest rework C2): one handler per kind and target in a state, and a
        // fixed reply the bridge can build
        static IEnumerable<string> HandlerProblems(QuestScriptDefinition def)
        {
            // one handler per kind and target in a state
            foreach (var state in def.States)
            {
                var seen = new HashSet<string>();
                var said = new HashSet<string>();
                foreach (var h in state.Handlers)
                {
                    string key = h.Entity != null ? $"{h.Zone}/{h.Entity}/{h.Kind}" : $"{h.Zone}/{h.Id}/{h.Kind}";
                    if (seen.Add(key) || !said.Add(key)) continue;
                    yield return h.Entity != null
                        ? $"{h.Entity} {h.Kind} declared twice in one state"
                        : $"{h.Kind} {h.Id} declared twice in one state";
                }
            }

            // a fixed reply must be one the bridge can build
            foreach (var state in def.States)
                foreach (var h in state.Handlers)
                    if (h.Fixed != null && (SequenceProblem(h.Fixed) ?? ArgsProblem(h.Fixed, def.IsCalled(h))) is { } reply)
                        yield return $"{(h.Entity != null ? $"{h.Entity} {h.Kind}" : $"{h.Kind} {h.Id}")}: {reply}";
        }

        // a mission's problems (quest rework C2), in declaration order; empty when it is clean. The shared rules
        // (HandlerProblems), without the quest-only ones (the steps and the reward's itemParams: a mission has
        // neither), then the mission's own: every state is a mission state (b.State(current, status, when); a quest
        // match chained onto one, or a quest state chained from one, is refused). A mission with Called() handlers
        // (xi.cs.mission, missionEvent and missionZoneIn) then has the NPC-call quests' rules (CallProblems)
        public static IReadOnlyList<string> Validate(MissionScriptDefinition def)
        {
            var problems = new List<string>(HandlerProblems(def.Core));
            for (int i = 0; i < def.States.Count; i++)
                if (def.States[i].Matches.Count > 0)
                    problems.Add($"state {i + 1} has a quest match: " +
                                 "a mission's states are b.State(current, status, when)");
            if (def.Core.HasCalled) problems.AddRange(CallProblems(def.Core));
            return problems;
        }

        // the deferred callbacks and exports (quest rework P10): a callback's name is its identity
        // (its label 0/-/callback:<name>, the logs, the fake), so it is [A-Za-z][A-Za-z0-9_]* and declared once; an
        // export names a Lua global by its dotted path, once, and one of the quest's own callbacks. A timer's delay and
        // a callback of another quest are checked when armed
        // (inside the handlers' lambdas, out of the validator's sight); a callback declared and never armed is
        // coverage's to find (its label reached by no flow)
        public static IReadOnlyList<string> CallbackProblems(QuestScriptDefinition def)
        {
            var problems = new List<string>();
            var names = new HashSet<string>();
            var said = new HashSet<string>();
            foreach (var cb in def.Callbacks)
            {
                if (!QuestCallback.NamePattern.IsMatch(cb.Name)) problems.Add($"callback '{cb.Name}': a callback's name is [A-Za-z][A-Za-z0-9_]* (its label 0/-/callback:<name>)");
                else if (!names.Add(cb.Name) && said.Add(cb.Name)) problems.Add($"callback {cb.Name} declared twice");
            }
            var globals = new HashSet<string>();
            foreach (var e in def.Exports)
            {
                if (!QuestExport.GlobalPattern.IsMatch(e.Global)) problems.Add($"export '{e.Global}': a Lua global's dotted path (xi.dancesWithLuopans.onHealing)");
                else if (!globals.Add(e.Global)) problems.Add($"export {e.Global} declared twice");
                if (!def.Callbacks.Contains(e.Callback)) problems.Add($"export {e.Global}: its callback {e.Callback.Name} is not one of the quest's (b.Callback)");
                if (e.Module != null && e.Module.Length == 0) problems.Add($"export {e.Global}: an empty module name");
            }
            return problems;
        }

        // a hidden quest (quest rework P9, HiddenQuest:new(name)) has what the Lua kind has: no quest log, id or status
        // (InteractionLookup passes its section check no status: HiddenQuest has no getCheckArgs), so every state is an
        // AnyState; it is a container (no NPC script calls one: its handlers are the lookup's); and its completion is
        // npcUtil.giveReward, which reads reward.fameArea as a table (fameArea.fame_area), not the number a QuestReward holds
        public static IReadOnlyList<string> HiddenProblems(QuestScriptDefinition def)
        {
            var problems = new List<string>();
            for (int i = 0; i < def.States.Count; i++)
                if (def.States[i].Matches.FirstOrDefault(m => !m.AnyStatus) is { } m)
                    problems.Add($"state {i + 1} matches status {m.Status}: a hidden quest has no status (HiddenQuest:new has no quest log or id); use AnyState(when: ...)");
            foreach (var state in def.States)
                foreach (var h in state.Handlers.Where(h => h.Called))
                    problems.Add($"{(h.Entity != null ? $"{h.Entity} {h.Kind}" : $"{h.Kind} {h.Id}")} is Called(): a hidden quest is a container (HiddenQuest:new), its handlers are the lookup's; no NPC script calls one");
            if (def.Reward is { FameArea: not null })
                problems.Add("reward.fameArea: a hidden quest's completion is npcUtil.giveReward, which reads fameArea as a table (fameArea.fame_area), not a number");
            return problems;
        }

        // the replies an NPC-call handler can render with plain player:* calls; null when fine
        public static string CallReplyProblem(QuestReply r) =>
            r.Modifiers.Count > 0 ? $"reply modifiers are not allowed in an NPC call ({string.Join(", ", r.Modifiers)}): it builds no action"
            : r.TextFace != null ? "a text's face (WithFace) is a sequence step only"
            : r.Kind is QuestReplyKind.Event or QuestReplyKind.ProgressEvent or
            QuestReplyKind.PriorityEvent or QuestReplyKind.Cutscene or QuestReplyKind.MessageSpecial or QuestReplyKind.MessageText or
            QuestReplyKind.MessageName or QuestReplyKind.NoAction
            ? null : $"a {r.Kind} reply is not allowed in an NPC call";

        // the replies an OnZoneIn handler can give, container or NPC call: Lua's onZoneIn returns a cutscene id, or the table
        // { id, textTable, flags } (quest rework P8), not an action, so only an event kind with no arguments, no options, no
        // modifiers (a modifier has no action to act on) and no named field but text_table and flags (luautils::OnZoneIn
        // reads those two only: ZoneInTable); null when fine. The bridge checks each reply with it when the handler runs: an
        // OnZoneIn reply is a lambda, so registration cannot see it
        public static string ZoneInReplyProblem(QuestReply r) =>
            !QuestReply.IsEvent(r.Kind) ? $"a {r.Kind} reply is not a cutscene id"
            : r.Args.Count > 0 || r.Options.Count > 0 || r.StringOptions.Count > 0
                ? $"a cutscene id takes no arguments here ({r.Args.Count + r.Options.Count + r.StringOptions.Count} given)"
            : r.EventFields.Select(f => f.Name).FirstOrDefault(n => !ZoneInFields.Contains(n)) is { } field
                ? $"a zone-in takes text_table and flags only (the engine's OnZoneIn reads {{ id, textTable, flags }}), not {field}"
            : r.Modifiers.Count > 0 ? $"a cutscene id takes no reply modifiers ({string.Join(", ", r.Modifiers)}): onZoneIn builds no action"
            : null;

        // the named fields a zone-in's table carries (quest rework P8)
        static readonly string[] ZoneInFields = { "text_table", "flags" };

        // how the bridge returns a valid zone-in reply to luautils::OnZoneIn (quest rework P8): null, the cutscene id alone
        // (a plain number, as before), when the reply sets neither text_table nor flags; else the table { id, textTable,
        // flags } with OnZoneIn's own defaults for the one not set (get_or: -1 and 0). Lua's { 956, 0 } and the bridge's
        // { 956, 0, 0 } play the same cutscene: the engine reads a missing [3] as 0
        public static (int Id, int TextTable, uint Flags)? ZoneInTable(QuestReply r)
        {
            if (!r.EventFields.Any(f => ZoneInFields.Contains(f.Name))) return null;
            int textTable = r.EventFields.Where(f => f.Name == "text_table").Select(f => (int)f.Number).DefaultIfEmpty(-1).First();
            uint flags = r.EventFields.Where(f => f.Name == "flags").Select(f => (uint)f.Number).DefaultIfEmpty(0u).First();
            return (r.Id, textTable, flags);
        }

        // the replies an OnTriggerAreaEnter handler can give: the lookup performs the action with no NPC (performNextAction's
        // targetId is nil for a trigger area), so a text the NPC shows (message / messageText / messageName / replaceMessage
        // of the Text or Name type, a sequence: its text steps are messageText, its face turns the NPC) has none to show it.
        // Null when fine. The bridge checks each reply with it when the handler runs (the reply is a lambda)
        public static string TriggerAreaReplyProblem(QuestReply r) => NoNpcReplyProblem(r, "a trigger area");

        // the replies an OnAfterZoneIn handler can give (quest rework P5): the lookup performs the action with no NPC
        // (InteractionLookup:afterZoneIn passes no targetId), as for a trigger area. Null when fine; the bridge checks
        // each reply with it when the handler runs
        public static string AfterZoneInReplyProblem(QuestReply r) => NoNpcReplyProblem(r, "an afterZoneIn");

        static string NoNpcReplyProblem(QuestReply r, string what) =>
            r.Kind is QuestReplyKind.MessageText or QuestReplyKind.MessageName or QuestReplyKind.Sequence ||
            r.Kind is QuestReplyKind.Message or QuestReplyKind.ReplaceMessage && r.MessageType is null or 1 or 3
                ? $"a {r.Kind} reply needs an NPC to show it, and {what} has none"
                : SequenceProblem(r);

        // NPC-call quests (Called()): no step matches, OnTrigger / OnTrade / OnMobDeath handlers on entities and OnEventFinish /
        // OnEventUpdate / OnZoneIn zone-wide, the replies of CallReplyProblem, and no entity with both kinds of handler
        static IEnumerable<string> CallProblems(QuestScriptDefinition def)
        {
            for (int i = 0; i < def.States.Count; i++)
            {
                var state = def.States[i];
                if (state.Handlers.Any(def.IsCalled) && state.Matches.Any(m => m.Step.HasValue))
                    yield return $"state {i + 1}: an NPC-call quest cannot match a step";
            }
            // in a mixed quest a state-level event handler is a container one: one in a state with Called() NPC handlers is
            // refused rather than guessed at (chained after the first OnEventFinish it would silently not be called)
            if (!def.CallOnly)
                for (int i = 0; i < def.States.Count; i++)
                {
                    var hs = def.States[i].Handlers;
                    if (hs.Any(h => h.Called && h.Entity != null) &&
                        hs.Any(h => !h.Called && h.Entity == null && h.Kind is QuestHandlerKind.OnEventFinish or QuestHandlerKind.OnEventUpdate))
                        yield return $"state {i + 1}: event handlers in a state with Called() NPC handlers must be chained from the Called() NPC builder, or move it to its own state";
                }
            foreach (var state in def.States)
                foreach (var h in state.Handlers.Where(def.IsCalled))
                {
                    string name = h.Entity != null ? $"{h.Entity} {h.Kind}" : $"{h.Kind} {h.Id}";
                    bool kindOk = h.Entity != null
                        ? h.Kind is QuestHandlerKind.OnTrigger or QuestHandlerKind.OnTrade or QuestHandlerKind.OnMobDeath
                        : h.Kind is QuestHandlerKind.OnEventFinish or QuestHandlerKind.OnEventUpdate or QuestHandlerKind.OnZoneIn;
                    if (!kindOk)
                        yield return $"{name}: a called handler is OnTrigger, OnTrade or OnMobDeath on an entity, OnEventFinish, OnEventUpdate or OnZoneIn state-level";
                    else if (h.Fixed != null && CallReplyProblem(h.Fixed) is { } reply) yield return $"{name}: {reply}";
                }
            var called = new HashSet<(ushort, string)>();
            var plain = new HashSet<(ushort, string)>();
            foreach (var h in def.States.SelectMany(st => st.Handlers).Where(h => h.Entity != null))
                (h.Called ? called : plain).Add((h.Zone, h.Entity));
            foreach (var (zone, entity) in called.Where(plain.Contains).OrderBy(k => k.Item1).ThenBy(k => k.Item2, System.StringComparer.Ordinal))
                yield return $"{entity} in zone {zone} has both Called() and container handlers";
        }

        // the reply arguments the engine would drop for their type: a Lua boolean or string where the binding reads only a
        // number, or where it reads nothing; null when fine. called: the reply rendered as the plain player:* call of an NPC
        // call (ZoneHost.QuestCalls RenderCallReply), else as the container's action (Event:perform / Message:perform).
        // The bridge checks each reply with it when it pushes it (a handler's reply is a lambda), the validator a fixed one
        public static string ArgsProblem(QuestReply r, bool called = false)
        {
            for (int i = 0; i < r.Args.Count; i++)
            {
                var a = r.Args[i];
                // an NPC call's player:messageName(id, player, p0..p3, chat, sender, showSender): argument 6 is the sender
                // entity, which no QuestArg is, a number included (C#: ToObject is null for it, so the sender is dropped
                // silently; C++: sender.as<CLuaBaseEntity*>() on a number reads null userdata). Every Lua NPC script passes
                // an entity there, so no faithful port needs it
                if (called && r.Kind == QuestReplyKind.MessageName && i == 5)
                    return $"{r.Kind} argument {i + 1} ({a}) would be dropped: messageName's argument 6 is the sender entity, which no reply argument can be";
                if (a.Kind == QuestArgKind.Number) continue;
                if (DroppedArg(r, i, a.Kind, called) is { } why) return $"{r.Kind} argument {i + 1} ({a}) would be dropped: {why}";
            }
            return null;
        }

        // why the engine drops a boolean / string at argument index i (0-based, after the id), else null
        static string DroppedArg(QuestReply r, int i, QuestArgKind kind, bool called)
        {
            // CLuaBaseEntity::ParseEvent (lua_base_entity.cpp:1314-1322, 1379-1386): the parameters are numbers only, read
            // positionally (va.get_type == number) or from the option table (get_or<uint32>); a leading string makes the
            // first four arguments the event's strings, which OptionString writes
            if (QuestReply.IsEvent(r.Kind))
                return r.Options.Count > 0 || r.StringOptions.Count > 0 || r.EventFields.Count > 0
                    ? $"the option table's [{i}] is read as a number only (get_or<uint32>); write the number (isLast and 1 or 0), a string with OptionString"
                    : "ParseEvent reads event parameters as numbers only; write the number (isLast and 1 or 0), a string with OptionString";
            // the Lua Message type the reply performs as (message.lua Message:perform): Special, Name, else Text
            int type = r.Kind switch
            {
                QuestReplyKind.MessageSpecial => 2,
                QuestReplyKind.MessageName => 3,
                QuestReplyKind.Message or QuestReplyKind.ReplaceMessage => r.MessageType ?? 1,
                _ => 1,
            };
            bool isString = kind == QuestArgKind.String;
            if (type == 2)
                // messageSpecial(id, p0..p3, showName) (lua_base_entity.cpp:571-575): p0..p3 numbers only, showName a boolean only
                return i < 4 ? "messageSpecial reads its first four arguments as numbers only"
                    : i == 4 ? (isString ? "messageSpecial's showName (argument 5) is a boolean only" : null)
                    : "messageSpecial reads five arguments";
            if (called)
            {
                if (r.Kind == QuestReplyKind.MessageName)
                    // player:messageName(id, player, p0..p3, chat, sender, showSender) (lua_base_entity.cpp:499-528): p0..p3
                    // and chat as<int32> (C#, ZoneHost.CasketBindings: ToIntegral, a boolean is 0), sender an entity (refused
                    // in ArgsProblem whatever its kind), showSender as<bool>
                    return i < 4 ? "messageName reads its first four arguments as numbers only"
                        : i == 4 ? "messageName's chat type (argument 5) is a number only"
                        : i == 5 ? "messageName's argument 6 is the sender entity"
                        : i == 6 ? null
                        : "messageName reads seven arguments";
                if (r.Kind == QuestReplyKind.MessageText)
                    // player:messageText(npc, id, arg2, arg3) (lua_base_entity.cpp:255-307): arg2 a boolean (showName) or a
                    // number (mode), a string matching neither; arg3 the mode, which the C# binding (ZoneHost.Bindings
                    // MessageText) reads with ToIntArg: a boolean or a string raises a Lua error (LuaArgumentException)
                    return i == 0 ? (isString ? "messageText's argument 1 is showName (a boolean) or the mode (a number)" : null)
                        : i == 1 ? "messageText's mode (argument 2) is a number only"
                        : "messageText reads two arguments";
                return null;
            }
            if (type == 3)
                // showText(npc, id, p0..p3, showName, turn, messageType) (lua_base_entity.cpp:197-235): p0..p3 and
                // messageType are numbers, which the C# binding (ZoneHost.Bindings ShowText) reads with ToUIntArg / ToIntArg:
                // a boolean or a string raises a Lua error (LuaArgumentException), so the message is not shown; showName /
                // turn are read as booleans (nil, false: false; anything else true)
                return i < 4 ? "showText reads its first four arguments as numbers only"
                    : i is 4 or 5 ? null
                    : i == 6 ? "showText's messageType (argument 7) is a number only"
                    : "showText reads seven arguments";
            // Message:perform shows a text message as messageText(npc, id, { face = ... }): no argument reaches it
            return "the text message's action (messageText(npc, id, { face })) passes no arguments";
        }

        // a sequence the bridge can build: steps that are plain messages ({ text = id }), a face ({ face = angle }) or a
        // wait ({ wait = ms }); null when fine
        public static string SequenceProblem(QuestReply r)
        {
            if (r.Kind is QuestReplyKind.Face or QuestReplyKind.Wait) return $"a {r.Kind} reply is a sequence step only";
            // { text = id, face = angle } is an entry of a sequence's table; a lone message has no face to take (Lua's
            // Message:face returns nothing, so quest:messageText(id):face(n) is no action)
            if (r.TextFace != null) return "a text's face (WithFace) is a sequence step only";
            if (r.Kind != QuestReplyKind.Sequence) return null;
            if (r.Steps.Count == 0) return "a sequence with no steps";
            // Sequence:new takes its id from the text steps: with none the sequence has no id
            if (r.Steps.All(s => s.Kind is QuestReplyKind.Face or QuestReplyKind.Wait)) return "a sequence with no message step";
            for (int i = 0; i < r.Steps.Count; i++)
            {
                var s = r.Steps[i];
                // a step is a { text = id } entry of the sequence's table, not an action: a modifier has nothing to act on
                if (s.Modifiers.Count > 0) return $"sequence step {i + 1} takes no modifiers ({string.Join(", ", s.Modifiers)})";
                if (s.Kind is QuestReplyKind.Face or QuestReplyKind.Wait) continue;
                bool plain = (s.Kind == QuestReplyKind.MessageText || s.Kind == QuestReplyKind.Message && (s.MessageType == null || s.MessageType == 1)) &&
                             s.Args.Count == 0 && s.Steps.Count == 0;
                if (!plain) return $"sequence step {i + 1} ({s.Kind}) is not a plain message, a face or a wait";
            }
            return null;
        }
    }
}
