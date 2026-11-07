// XI.Quests -- a module's overrides applied to a compiled quest (quest rework P4). The registration calls Apply when an
// enabled module patches a C# quest, before the quest's container is built and placed, and runs the validator on the
// result; Apply refuses an override of a handler or state the quest does not have (or an add of one it has) with a message
// that names it as its handler label does (<state>/<entity or ->/<trigger>, in zone <id>).
using System;
using System.Collections.Generic;
using System.Linq;

namespace XI.Quests.Authoring
{
    public sealed class QuestOverrideResult
    {
        internal QuestOverrideResult(QuestScriptDefinition definition, IReadOnlyList<string> problems)
        {
            Definition = definition;
            Problems = problems;
        }

        // the patched definition; null when an override was refused (Problems says why)
        public QuestScriptDefinition Definition { get; }
        public IReadOnlyList<string> Problems { get; }
    }

    public static class QuestOverrides
    {
        // the quest's definition with the module's overrides of the key applied, in their order; the definition itself when
        // the module has none for it. A wrapped, replaced or added handler is a new QuestHandlerDecl with Module set (its kind,
        // zone, entity, id and Called kept), at the place of the one it overrides; a check override is the state's one
        // match, with CheckModule set. baseCalled: called with the quest's own handler each time a wrap runs it as its base
        // (the coverage hook: the base's own label); checkCalled: with the state's number each time an overridden check runs
        public static QuestOverrideResult Apply(QuestScriptDefinition def, QuestModuleDefinition module, string key,
                                                Action<QuestHandlerDecl> baseCalled = null, Action<int> checkCalled = null)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (module == null) throw new ArgumentNullException(nameof(module));
            var patch = module.PatchFor(key);
            if (patch == null || patch.Ops.Count == 0) return new QuestOverrideResult(def, Array.Empty<string>());

            var info = module.Info;
            var problems = new List<string>();
            var states = def.States.Select(s => new State(s)).ToList();
            var reward = def.Reward;
            var rewardModule = def.RewardModule;
            var steps = def.DeclaredSteps.ToList();
            string Where() => $"{info.Name}: {key}";

            foreach (var op in patch.Ops)
            {
                switch (op)
                {
                    case QuestHandlerOverride h:
                    {
                        string label = $"{h.State}/{h.Entity ?? "-"}/{Trigger(h.Kind, h.Id)} in zone {h.Zone}";
                        if (StateProblem(h.State) is { } bad)
                        {
                            problems.Add($"{Where()}: {bad} for {label}");
                            continue;
                        }
                        var s = states[h.State - 1];
                        int at = s.Handlers.FindIndex(x => x.Kind == h.Kind && x.Zone == h.Zone && x.Entity == h.Entity && (h.Entity != null || x.Id == h.Id));
                        string verb = h.Mode switch { QuestOverrideMode.Wrap => "wrap", QuestOverrideMode.Replace => "replace", _ => "add" };
                        if (h.Mode == QuestOverrideMode.Add)
                        {
                            if (at >= 0)
                            {
                                problems.Add($"{Where()}: has a handler {label} already: wrap or replace it");
                                continue;
                            }
                            if (KindProblem(h.Kind, h.Reply != null, h.Entity != null) is { } kindProblem)
                            {
                                problems.Add($"{Where()}: cannot add {label}: {kindProblem}");
                                continue;
                            }
                            s.Handlers.Add(h.Reply != null
                                ? new QuestHandlerDecl(h.Kind, h.Zone, h.Entity, h.Id, q => h.Reply(q, null), null, null, false, info)
                                : new QuestHandlerDecl(h.Kind, h.Zone, h.Entity, h.Id, null, q => h.Run(q, null), null, false, info));
                            continue;
                        }
                        if (at < 0)
                        {
                            problems.Add($"{Where()}: has no handler {label} to {verb}");
                            continue;
                        }
                        var e = s.Handlers[at];
                        bool replies = e.Reply != null || e.Fixed != null;
                        if (replies != (h.Reply != null))
                        {
                            problems.Add($"{Where()}: {label} {(replies ? "returns a reply: its override takes a Func" : "returns nothing: its override takes an Action")}");
                            continue;
                        }
                        // the base a wrap calls: the handler there (the quest's own reports its run; a module's reports
                        // nothing, the bridge has counted the outer one)
                        bool own = e.Module == null;
                        QuestHandlerDecl patched;
                        if (h.Reply != null)
                        {
                            Func<IQuestContext, QuestReply> inner = e.Fixed is { } fixedReply ? _ => fixedReply : e.Reply;
                            Func<IQuestContext, QuestReply> baseFn = own && baseCalled != null ? q => { baseCalled(e); return inner(q); } : inner;
                            var wrap = h.Reply;
                            patched = e.WithBehaviour(q => wrap(q, baseFn), null, null, info);
                        }
                        else
                        {
                            var inner = e.Run;
                            Action<IQuestContext> baseFn = own && baseCalled != null ? q => { baseCalled(e); inner(q); } : inner;
                            var wrap = h.Run;
                            patched = e.WithBehaviour(null, q => wrap(q, baseFn), null, info);
                        }
                        s.Handlers[at] = patched;
                        break;
                    }
                    case QuestCheckOverride c:
                    {
                        if (StateProblem(c.State) is { } bad)
                        {
                            problems.Add($"{Where()}: {bad} for its check");
                            continue;
                        }
                        var s = states[c.State - 1];
                        var matches = s.Matches;
                        bool hidden = def.Hidden;
                        Func<IQuestCheck, bool> baseCheck = q => MatchesOf(matches, q, hidden);
                        var wrap = c.Check;
                        int number = c.State;
                        Func<IQuestCheck, bool> when = checkCalled == null
                            ? q => wrap(q, baseCheck)
                            : q => { checkCalled(number); return wrap(q, baseCheck); };
                        s.Matches = new List<QuestMatch> { new QuestMatch(default, null, when, anyStatus: true) };
                        s.CheckModule = info;
                        break;
                    }
                    case QuestRewardOverride r:
                        reward = QuestScriptBuilder.CopyReward(r.Reward);
                        rewardModule = info;
                        break;
                    case QuestRemoveStateOverride r:
                    {
                        if (StateProblem(r.State) is { } bad)
                        {
                            problems.Add($"{Where()}: {bad} to remove");
                            continue;
                        }
                        var s = states[r.State - 1];
                        s.Removed = true;
                        s.RemovedBy = info;
                        s.RemovedMatches = s.Matches.ToArray();
                        s.Handlers.Clear();
                        s.Matches = new List<QuestMatch> { new QuestMatch(default, null, _ => false, anyStatus: true) };
                        s.CheckModule = null;
                        break;
                    }
                    case QuestAddStatesOverride a:
                    {
                        var b = new QuestScriptBuilder();
                        try
                        {
                            a.Build(b);
                        }
                        catch (Exception ex)
                        {
                            problems.Add($"{Where()}: its added states threw: {ex.Message}");
                            continue;
                        }
                        if (b.HasReward)
                        {
                            problems.Add($"{Where()}: added states set no reward: use Reward");
                            continue;
                        }
                        // quest rework P10: a callback is the quest's, armed by its handlers; added states have none
                        if (b.HasCallbacks)
                        {
                            problems.Add($"{Where()}: added states declare no callback or export: the quest's own do");
                            continue;
                        }
                        var added = b.ToDefinition(def.Log, def.ConstName);
                        foreach (var st in added.States)
                        {
                            var s = new State(st);
                            for (int i = 0; i < s.Handlers.Count; i++)
                                s.Handlers[i] = s.Handlers[i].WithBehaviour(s.Handlers[i].Reply, s.Handlers[i].Run, s.Handlers[i].Fixed, info);
                            states.Add(s);
                        }
                        steps.AddRange(added.DeclaredSteps.Where(n => !steps.Contains(n)));
                        break;
                    }
                }
            }
            if (problems.Count > 0) return new QuestOverrideResult(null, problems);
            // copies of the quest's own (QuestScriptDefinition.With, QuestStateDecl.With): a member a port adds later is kept
            var result = def.With(reward, rewardModule,
                states.Select(s => s.Source.With(s.Matches.ToArray(), s.Handlers.ToArray(), s.CheckModule, s.RemovedBy, s.RemovedMatches)).ToArray(),
                steps.ToArray());
            return new QuestOverrideResult(result, Array.Empty<string>());

            string StateProblem(int number) =>
                number > states.Count ? $"has no state {number} (it has {states.Count})"
                : states[number - 1].Removed ? $"state {number} is removed" : null;
        }

        // a state's matches as its check: any one matching (QuestStateDecl.IsMatch), the step read only when a match of the
        // status names one (as the bridge's check reads vars.Prog). A hidden quest has no status (Quest.Status throws: its
        // states are AnyState only, the validator refuses a status match), so it is not read there (quest rework P9)
        static bool MatchesOf(IReadOnlyList<QuestMatch> matches, IQuestCheck q, bool hidden)
        {
            var status = hidden ? default : q.Quest.Status;
            bool readsStep = matches.Any(m => (m.AnyStatus || m.Status == status) && m.Step.HasValue);
            return new QuestStateDecl(matches, Array.Empty<QuestHandlerDecl>()).IsMatch(status, readsStep ? q.Quest.Step : 0, q);
        }

        // what an added handler of the kind must be: an entity's OnTrigger / OnTrade reply, OnMobDeath run; a state-level
        // OnEventFinish / OnEventUpdate / OnZoneOut run, OnZoneIn / OnAfterZoneIn reply, OnTriggerAreaEnter / Leave either; null when fine. Every kind is named:
        // a kind added to QuestHandlerKind throws here until it is
        static string KindProblem(QuestHandlerKind kind, bool reply, bool entity) => kind switch
        {
            QuestHandlerKind.OnTrigger or QuestHandlerKind.OnTrade => entity ? (reply ? null : "it returns a reply") : "an entity's handler",
            QuestHandlerKind.OnMobDeath => entity ? (reply ? "it returns nothing" : null) : "an entity's handler",
            QuestHandlerKind.OnEventFinish or QuestHandlerKind.OnEventUpdate or QuestHandlerKind.OnZoneOut => entity ? "a state-level handler" : reply ? "it returns nothing" : null,
            QuestHandlerKind.OnZoneIn or QuestHandlerKind.OnAfterZoneIn => entity ? "a state-level handler" : reply ? null : "it returns a reply",
            QuestHandlerKind.OnTriggerAreaEnter or QuestHandlerKind.OnTriggerAreaLeave => entity ? "a state-level handler" : null,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "a handler kind QuestOverrides does not know"),
        };

        // a handler label's trigger: onTrigger, onEventFinish:22, onTriggerAreaEnter:3 (ZoneHost.LabelsOf). Every kind is
        // named: a kind added to QuestHandlerKind throws here until it is
        public static string Trigger(QuestHandlerKind kind, int id)
        {
            string name = kind switch
            {
                QuestHandlerKind.OnTrigger => "onTrigger",
                QuestHandlerKind.OnTrade => "onTrade",
                QuestHandlerKind.OnMobDeath => "onMobDeath",
                QuestHandlerKind.OnEventFinish => "onEventFinish",
                QuestHandlerKind.OnEventUpdate => "onEventUpdate",
                QuestHandlerKind.OnZoneIn => "onZoneIn",
                QuestHandlerKind.OnTriggerAreaEnter => "onTriggerAreaEnter",
                QuestHandlerKind.OnAfterZoneIn => "afterZoneIn",
                QuestHandlerKind.OnZoneOut => "onZoneOut",
                QuestHandlerKind.OnTriggerAreaLeave => "onTriggerAreaLeave",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "a handler kind QuestOverrides does not know"),
            };
            return kind is QuestHandlerKind.OnEventFinish or QuestHandlerKind.OnEventUpdate or QuestHandlerKind.OnTriggerAreaEnter or QuestHandlerKind.OnTriggerAreaLeave ? $"{name}:{id}" : name;
        }

        // a state being patched: its declaration (copied with the changes at the end) and the members an override changes
        sealed class State
        {
            public State(QuestStateDecl s)
            {
                Source = s;
                Matches = s.Matches.ToList();
                Handlers = s.Handlers.ToList();
                CheckModule = s.CheckModule;
                RemovedBy = s.RemovedBy;
                RemovedMatches = s.RemovedMatches;
                Removed = s.RemovedBy != null;
            }

            public readonly QuestStateDecl Source;
            public List<QuestMatch> Matches;
            public readonly List<QuestHandlerDecl> Handlers;
            public QuestModuleInfo CheckModule;
            public bool Removed;
            public QuestModuleInfo RemovedBy;
            public IReadOnlyList<QuestMatch> RemovedMatches;
        }
    }
}
