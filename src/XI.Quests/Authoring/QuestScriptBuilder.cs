// XI.Quests -- the fluent builders behind QuestScript.Build (quest rework B2). They keep declaration order, and
// everything they hand out is read-only once Compile() has run.
using System;
using System.Collections.Generic;
using System.Linq;

namespace XI.Quests.Authoring
{
    // OnAfterZoneIn (quest rework P5): interaction_lookup's afterZoneIn, run by luautils::AfterZoneIn 4 s after the zone-in.
    // OnZoneOut (quest rework P7): interaction_lookup's onZoneOut, run by luautils::OnZoneOut as the char leaves the zone.
    // OnTriggerAreaLeave (quest rework P9): interaction_lookup's onTriggerAreaLeave[id], run by luautils::OnTriggerAreaLeave
    // as the char leaves a trigger area (CZone::CheckTriggerAreas; CZone::CharZoneOut for the area it zones out of)
    public enum QuestHandlerKind : byte { OnTrigger, OnTrade, OnMobDeath, OnEventFinish, OnEventUpdate, OnZoneIn, OnTriggerAreaEnter, OnAfterZoneIn, OnZoneOut, OnTriggerAreaLeave }

    // one handler: on an entity (Entity = NPC / mob name) or zone-wide (Entity = null, Id = csid / trigger area id; 1 for
    // OnZoneIn, OnAfterZoneIn and OnZoneOut)
    public sealed class QuestHandlerDecl
    {
        internal QuestHandlerDecl(QuestHandlerKind kind, ushort zone, string entity, int id,
            Func<IQuestContext, QuestReply> reply, Action<IQuestContext> run, QuestReply fixedReply, bool called = false,
            QuestModuleInfo module = null)
        {
            Module = module;
            Kind = kind;
            Zone = zone;
            Entity = entity;
            Id = id;
            Reply = reply;
            Run = run;
            Fixed = fixedReply;
            Called = called;
        }

        public QuestHandlerKind Kind { get; }
        public ushort Zone { get; }
        public string Entity { get; }
        public int Id { get; }
        // OnTrigger / OnTrade / OnZoneIn / OnAfterZoneIn, and an OnTriggerAreaEnter / OnTriggerAreaLeave whose Lua returned
        // an action (quest:progressEvent(14)): what Lua's handler returned (null = nil)
        public Func<IQuestContext, QuestReply> Reply { get; private set; }
        // OnMobDeath / OnEventFinish / OnEventUpdate / OnTriggerAreaEnter / OnTriggerAreaLeave / OnZoneOut: Lua's handler had
        // no useful return
        public Action<IQuestContext> Run { get; private set; }
        // ['Npc'] = quest:progressEvent(215): a fixed action, not a function
        public QuestReply Fixed { get; private set; }
        // Called(): the NPC's or mob's script calls the quest (xi.cs.quest; 'onMobDeath' for a mob), the container never
        // holds this handler
        public bool Called { get; }
        // the C# module whose override this handler is (QuestOverrides.Apply: a wrap, a replace or an added handler); null for
        // the quest's own
        public QuestModuleInfo Module { get; private set; }

        // a copy with another behaviour and module, every other member kept (a member added later is copied too)
        internal QuestHandlerDecl WithBehaviour(Func<IQuestContext, QuestReply> reply, Action<IQuestContext> run, QuestReply fixedReply, QuestModuleInfo module)
        {
            var copy = (QuestHandlerDecl)MemberwiseClone();
            copy.Reply = reply;
            copy.Run = run;
            copy.Fixed = fixedReply;
            copy.Module = module;
            return copy;
        }
    }

    // a state's match: status (or any status), optionally the step (the quest's 'Prog' var), optionally a guard over the context
    public sealed class QuestMatch
    {
        internal QuestMatch(QuestStatus status, int? step, Func<IQuestCheck, bool> when, bool anyStatus = false)
        {
            Status = status;
            Step = step;
            When = when;
            AnyStatus = anyStatus;
        }

        // ignored when AnyStatus is set
        public QuestStatus Status { get; }
        // AnyState / OrAny: the check does not look at the status
        public bool AnyStatus { get; }
        public int? Step { get; }
        public Func<IQuestCheck, bool> When { get; }
    }

    public sealed class QuestStateDecl
    {
        internal QuestStateDecl(IReadOnlyList<QuestMatch> matches, IReadOnlyList<QuestHandlerDecl> handlers, QuestModuleInfo checkModule = null,
                                QuestModuleInfo removedBy = null, IReadOnlyList<QuestMatch> removedMatches = null,
                                IReadOnlyList<MissionMatch> missionMatches = null)
        {
            MissionMatches = missionMatches ?? Array.Empty<MissionMatch>();
            Matches = matches;
            Handlers = handlers;
            CheckModule = checkModule;
            RemovedBy = removedBy;
            RemovedMatches = removedMatches;
        }

        // the C# module that removed this state (QuestOverrides.Apply: RemoveState); null when it is not removed
        public QuestModuleInfo RemovedBy { get; private set; }
        // a removed state's matches before the removal: the coverage hook reports the removal reached when a check of the
        // quest runs while they hold (the state would have played); null when it is not removed
        public IReadOnlyList<QuestMatch> RemovedMatches { get; private set; }

        // a copy with other matches, handlers and module marks, every other member kept (a member added later is copied too)
        internal QuestStateDecl With(IReadOnlyList<QuestMatch> matches, IReadOnlyList<QuestHandlerDecl> handlers, QuestModuleInfo checkModule,
                                     QuestModuleInfo removedBy, IReadOnlyList<QuestMatch> removedMatches)
        {
            var copy = (QuestStateDecl)MemberwiseClone();
            copy.Matches = matches;
            copy.Handlers = handlers;
            copy.CheckModule = checkModule;
            copy.RemovedBy = removedBy;
            copy.RemovedMatches = removedMatches;
            return copy;
        }

        // the C# module whose override this state's check is (QuestOverrides.Apply: WrapCheck / ReplaceCheck); null for the
        // quest's own check
        public QuestModuleInfo CheckModule { get; private set; }

        // any one matching runs the section (Lua's check: a or b)
        public IReadOnlyList<QuestMatch> Matches { get; private set; }
        public IReadOnlyList<QuestHandlerDecl> Handlers { get; private set; }

        // a mission's state (quest rework C2, MissionScript): its matches, any one of which runs the section; empty for
        // a quest's state (whose matches are Matches)
        public IReadOnlyList<MissionMatch> MissionMatches { get; }

        // true when one MissionMatch matches the check's mission (q.Mission): whether the log's current mission is this
        // one (null = any), the log's status (null = any) and the guard (null = pass)
        public bool IsMissionMatch(IQuestCheck q)
        {
            var mission = q.Mission ??
                throw new InvalidOperationException("a mission state is matched in a mission's check (q.Mission)");
            foreach (var m in MissionMatches)
            {
                if (m.IsCurrent.HasValue && m.IsCurrent.Value != mission.IsCurrent) continue;
                if (m.Status.HasValue && m.Status.Value != mission.Status) continue;
                if (m.When != null && !m.When(q)) continue;
                return true;
            }
            return false;
        }

        // true when one QuestMatch matches: the status (unless AnyStatus), the step (null = any) and the guard (null = pass)
        public bool IsMatch(QuestStatus status, int step, IQuestCheck q) => AnyMatch(Matches, status, step, q);

        // a removed state: whether its matches from before the removal hold (false when it is not removed)
        public bool RemovedWouldMatch(QuestStatus status, int step, IQuestCheck q) => RemovedMatches != null && AnyMatch(RemovedMatches, status, step, q);

        static bool AnyMatch(IReadOnlyList<QuestMatch> matches, QuestStatus status, int step, IQuestCheck q)
        {
            foreach (var m in matches)
            {
                if (!m.AnyStatus && m.Status != status) continue;
                if (m.Step.HasValue && m.Step.Value != step) continue;
                if (m.When != null && !m.When(q)) continue;
                return true;
            }
            return false;
        }
    }

    public sealed class QuestScriptBuilder
    {
        readonly List<StateData> _states = new List<StateData>();
        readonly List<int> _steps = new List<int>();
        readonly List<QuestCallback> _callbacks = new List<QuestCallback>();
        readonly List<QuestExport> _exports = new List<QuestExport>();
        QuestReward _reward;

        internal sealed class StateData
        {
            public readonly List<QuestMatch> Matches = new List<QuestMatch>();
            public readonly List<QuestHandlerDecl> Handlers = new List<QuestHandlerDecl>();
            // a mission's state (MissionScriptBuilder.State): its matches
            public readonly List<MissionMatch> MissionMatches = new List<MissionMatch>();
        }

        // a copy: the quest can't change its reward after declaring it
        public void Reward(QuestReward reward)
        {
            if (reward == null) throw new ArgumentNullException(nameof(reward));
            _reward = CopyReward(reward);
        }

        internal static QuestReward CopyReward(QuestReward reward) => reward.Copy();

        // whether Reward was called (a module's AddStates builder refuses one)
        internal bool HasReward => _reward != null;
        // whether Callback or Export was called (a module's AddStates builder refuses them: its states merge into the
        // quest's, its callbacks would not)
        internal bool HasCallbacks => _callbacks.Count > 0 || _exports.Count > 0;

        // a deferred callback of the quest (quest rework P10): a function the engine calls later, armed from a handler
        // by its handle
        // (q.Player.Timer(ms, cb), q.Npc.Timer(ms, cb[, WithPlayer.Yes]), q.Player.AddListener("TICK", id, cb)), run
        // with a fresh context of the entity it fires on. The name is the callback's identity in labels
        // (0/-/callback:<name>), logs and the fake; the validator refuses a name used twice or not
        // [A-Za-z][A-Za-z0-9_]*
        public QuestCallback Callback(string name, Action<IQuestContext> run)
        {
            var cb = new QuestCallback(name ?? throw new ArgumentNullException(nameof(name)), run ?? throw new ArgumentNullException(nameof(run)));
            _callbacks.Add(cb);
            return cb;
        }

        // the Lua global (a dotted path: xi.dancesWithLuopans.onHealing) set to a function running the callback with
        // (player), for a script that calls the quest's Lua function by name (scripts/effects/healing.lua); the quest
        // stands in for the Lua module replacesModule (null: its LegacyPath), whose package.loaded entry the bridge
        // claims so that a require of the deleted file still loads (design section 3.4)
        public void Export(string global, QuestCallback callback, string replacesModule = null) =>
            _exports.Add(new QuestExport(global ?? throw new ArgumentNullException(nameof(global)),
                                         callback ?? throw new ArgumentNullException(nameof(callback)), replacesModule));

        // the steps this quest sets: SetStep inside a lambda is invisible to the validator, so the quest declares them
        public void Steps(params int[] steps)
        {
            if (steps != null) _steps.AddRange(steps);
        }

        public QuestStateBuilder State(QuestStatus status, int? step = null, Func<IQuestCheck, bool> when = null) =>
            NewState(new QuestMatch(status, step, when));

        // a state whatever the quest's status (an NPC script's branch on a char var alone)
        public QuestStateBuilder AnyState(int? step = null, Func<IQuestCheck, bool> when = null) =>
            NewState(new QuestMatch(default, step, when, anyStatus: true));

        // a mission's state (MissionScriptBuilder.State): the quest states' handlers, entered by a mission match
        internal QuestStateBuilder NewMissionState(MissionMatch match)
        {
            var s = new StateData();
            s.MissionMatches.Add(match);
            _states.Add(s);
            return new QuestStateBuilder(this, s);
        }

        QuestStateBuilder NewState(QuestMatch match)
        {
            var s = new StateData();
            s.Matches.Add(match);
            _states.Add(s);
            return new QuestStateBuilder(this, s);
        }

        internal QuestScriptDefinition ToDefinition(QuestLog log, string constName)
        {
            var states = _states.Select(s => new QuestStateDecl(s.Matches.ToArray(), s.Handlers.ToArray(),
                                                                 missionMatches: s.MissionMatches.ToArray())).ToArray();
            return new QuestScriptDefinition(log, constName, _reward, states, _steps.ToArray())
            {
                Callbacks = _callbacks.ToArray(),
                Exports = _exports.ToArray(),
            };
        }
    }

    public sealed class QuestStateBuilder
    {
        readonly QuestScriptBuilder _owner;
        readonly QuestScriptBuilder.StateData _state;

        internal QuestStateBuilder(QuestScriptBuilder owner, QuestScriptBuilder.StateData state)
        {
            _owner = owner;
            _state = state;
        }

        public QuestStateBuilder Or(QuestStatus status, int? step = null, Func<IQuestCheck, bool> when = null)
        {
            _state.Matches.Add(new QuestMatch(status, step, when));
            return this;
        }

        // or a match whatever the status
        public QuestStateBuilder OrAny(int? step = null, Func<IQuestCheck, bool> when = null)
        {
            _state.Matches.Add(new QuestMatch(default, step, when, anyStatus: true));
            return this;
        }

        // NPCs and mobs alike (Lua keys them by name)
        public QuestNpcBuilder Npc(ushort zone, string name) =>
            new QuestNpcBuilder(this, _state, zone, name ?? throw new ArgumentNullException(nameof(name)));

        public QuestStateBuilder OnEventFinish(ushort zone, int csid, Action<IQuestContext> run) =>
            Add(QuestHandlerKind.OnEventFinish, zone, csid, null, run);

        public QuestStateBuilder OnEventUpdate(ushort zone, int csid, Action<IQuestContext> run) =>
            Add(QuestHandlerKind.OnEventUpdate, zone, csid, null, run);

        public QuestStateBuilder OnZoneIn(ushort zone, Func<IQuestContext, QuestReply> reply) =>
            Add(QuestHandlerKind.OnZoneIn, zone, 1, reply, null);

        // [zone] = { afterZoneIn = function(player) ... end }: luautils::AfterZoneIn runs it 4 s after the zone-in (the
        // zone's own afterZoneIn after the lookup's). The reply is an action the lookup performs with no NPC (One Good
        // Deed?'s return quest:progressEvent(8, ...)), null for none (a handler that only gives key items and sets vars);
        // QuestScriptValidator.AfterZoneInReplyProblem: the replies it can take
        public QuestStateBuilder OnAfterZoneIn(ushort zone, Func<IQuestContext, QuestReply> reply) =>
            Add(QuestHandlerKind.OnAfterZoneIn, zone, 1, reply ?? throw new ArgumentNullException(nameof(reply)), null);

        // [zone] = { onZoneOut = function(player) ... end }: luautils::OnZoneOut runs it as the char leaves the zone (CZone::
        // CharZoneOut: a zone line, a warp, a logout), the lookup's handlers, then the zone's own onZoneOut. An action only:
        // the engine reads nothing back (Elderly Pursuits' Prog 2 -> 1 in Carpenters' Landing)
        public QuestStateBuilder OnZoneOut(ushort zone, Action<IQuestContext> run) =>
            Add(QuestHandlerKind.OnZoneOut, zone, 1, null, run ?? throw new ArgumentNullException(nameof(run)));

        public QuestStateBuilder OnTriggerAreaEnter(ushort zone, int areaId, Action<IQuestContext> run) =>
            Add(QuestHandlerKind.OnTriggerAreaEnter, zone, areaId, null, run);

        // a trigger area whose Lua handler returns an action (return quest:progressEvent(14)): the lookup performs it with
        // no NPC, then the zone's own onTriggerAreaEnter (QuestScriptValidator.TriggerAreaReplyProblem: the replies it can take)
        public QuestStateBuilder OnTriggerAreaEnter(ushort zone, int areaId, Func<IQuestContext, QuestReply> reply) =>
            Add(QuestHandlerKind.OnTriggerAreaEnter, zone, areaId, reply ?? throw new ArgumentNullException(nameof(reply)), null);

        // [zone] = { onTriggerAreaLeave = { [areaId] = function(player, triggerArea) ... end } } (quest rework P9):
        // luautils::OnTriggerAreaLeave runs it as the char leaves the area (CZone::CheckTriggerAreas, or zoning out of it),
        // the lookup's handlers, then the zone's own onTriggerAreaLeave (Dances with Luopans' Ergon Locus: the LocusArea and
        // RestUntil local vars cleared). As an enter: a run, or a reply the lookup performs with no NPC
        // (QuestScriptValidator.TriggerAreaReplyProblem)
        public QuestStateBuilder OnTriggerAreaLeave(ushort zone, int areaId, Action<IQuestContext> run) =>
            Add(QuestHandlerKind.OnTriggerAreaLeave, zone, areaId, null, run ?? throw new ArgumentNullException(nameof(run)));
        public QuestStateBuilder OnTriggerAreaLeave(ushort zone, int areaId, Func<IQuestContext, QuestReply> reply) =>
            Add(QuestHandlerKind.OnTriggerAreaLeave, zone, areaId, reply ?? throw new ArgumentNullException(nameof(reply)), null);

        // chains to the builder's next state
        public QuestStateBuilder State(QuestStatus status, int? step = null, Func<IQuestCheck, bool> when = null) =>
            _owner.State(status, step, when);
        public QuestStateBuilder AnyState(int? step = null, Func<IQuestCheck, bool> when = null) => _owner.AnyState(step, when);

        // an event handler declared through a Called() NPC builder is an NPC-call one too (its script calls xi.cs.questEvent)
        internal QuestStateBuilder AddEvent(QuestHandlerKind kind, ushort zone, int csid, Action<IQuestContext> run, bool called) =>
            Add(kind, zone, csid, null, run, called);

        QuestStateBuilder Add(QuestHandlerKind kind, ushort zone, int id, Func<IQuestContext, QuestReply> reply, Action<IQuestContext> run, bool called = false)
        {
            if (reply == null && run == null) throw new ArgumentNullException(run == null && kind is QuestHandlerKind.OnZoneIn ? "reply" : "run");
            _state.Handlers.Add(new QuestHandlerDecl(kind, zone, null, id, reply, run, null, called));
            return this;
        }
    }

    public sealed class QuestNpcBuilder
    {
        readonly QuestStateBuilder _parent;
        readonly QuestScriptBuilder.StateData _state;
        readonly ushort _zone;
        readonly string _name;
        bool _called;

        internal QuestNpcBuilder(QuestStateBuilder parent, QuestScriptBuilder.StateData state, ushort zone, string name)
        {
            _parent = parent;
            _state = state;
            _zone = zone;
            _name = name;
        }

        // the handlers declared after this are NPC-call handlers: the NPC's (or mob's) own Lua script calls xi.cs.quest at
        // that spot
        public QuestNpcBuilder Called()
        {
            _called = true;
            return this;
        }

        public QuestNpcBuilder OnTrigger(Func<IQuestContext, QuestReply> reply) =>
            Add(QuestHandlerKind.OnTrigger, reply ?? throw new ArgumentNullException(nameof(reply)), null, null);

        // Lua: ['Npc'] = quest:event(215)
        public QuestNpcBuilder OnTrigger(QuestReply fixedReply) =>
            Add(QuestHandlerKind.OnTrigger, null, null, fixedReply ?? throw new ArgumentNullException(nameof(fixedReply)));

        public QuestNpcBuilder OnTrade(Func<IQuestContext, QuestReply> reply) =>
            Add(QuestHandlerKind.OnTrade, reply ?? throw new ArgumentNullException(nameof(reply)), null, null);

        public QuestNpcBuilder OnMobDeath(Action<IQuestContext> run) =>
            Add(QuestHandlerKind.OnMobDeath, null, run ?? throw new ArgumentNullException(nameof(run)), null);

        // the same state's next entity
        public QuestNpcBuilder Npc(ushort zone, string name) => _parent.Npc(zone, name);

        // after Called(), these are NPC-call handlers as well
        public QuestStateBuilder OnEventFinish(ushort zone, int csid, Action<IQuestContext> run) =>
            _parent.AddEvent(QuestHandlerKind.OnEventFinish, zone, csid, run ?? throw new ArgumentNullException(nameof(run)), _called);
        public QuestStateBuilder OnEventUpdate(ushort zone, int csid, Action<IQuestContext> run) =>
            _parent.AddEvent(QuestHandlerKind.OnEventUpdate, zone, csid, run ?? throw new ArgumentNullException(nameof(run)), _called);
        public QuestStateBuilder OnZoneIn(ushort zone, Func<IQuestContext, QuestReply> reply) => _parent.OnZoneIn(zone, reply);
        public QuestStateBuilder OnAfterZoneIn(ushort zone, Func<IQuestContext, QuestReply> reply) => _parent.OnAfterZoneIn(zone, reply);
        public QuestStateBuilder OnZoneOut(ushort zone, Action<IQuestContext> run) => _parent.OnZoneOut(zone, run);
        public QuestStateBuilder OnTriggerAreaEnter(ushort zone, int areaId, Action<IQuestContext> run) => _parent.OnTriggerAreaEnter(zone, areaId, run);
        public QuestStateBuilder OnTriggerAreaEnter(ushort zone, int areaId, Func<IQuestContext, QuestReply> reply) => _parent.OnTriggerAreaEnter(zone, areaId, reply);
        public QuestStateBuilder OnTriggerAreaLeave(ushort zone, int areaId, Action<IQuestContext> run) => _parent.OnTriggerAreaLeave(zone, areaId, run);
        public QuestStateBuilder OnTriggerAreaLeave(ushort zone, int areaId, Func<IQuestContext, QuestReply> reply) => _parent.OnTriggerAreaLeave(zone, areaId, reply);
        public QuestStateBuilder State(QuestStatus status, int? step = null, Func<IQuestCheck, bool> when = null) => _parent.State(status, step, when);
        public QuestStateBuilder AnyState(int? step = null, Func<IQuestCheck, bool> when = null) => _parent.AnyState(step, when);

        QuestNpcBuilder Add(QuestHandlerKind kind, Func<IQuestContext, QuestReply> reply, Action<IQuestContext> run, QuestReply fixedReply)
        {
            if (reply == null && run == null && fixedReply == null)
                throw new ArgumentNullException(kind == QuestHandlerKind.OnMobDeath ? "run" : "reply");
            _state.Handlers.Add(new QuestHandlerDecl(kind, _zone, _name, 0, reply, run, fixedReply, _called));
            return this;
        }
    }
}
