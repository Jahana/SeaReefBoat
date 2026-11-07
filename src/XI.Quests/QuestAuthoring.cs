// XI.Quests — Phase-6 quest/mission AUTHORING core.
//
// Replaces the engine's implicit "ordered Lua sections, first check() that wins"
// (scripts/globals/interaction/{container,quest,mission}.lua) with an EXPLICIT state
// machine whose three concerns are separated:
//   * identity      -> stable string Id (QuestDefinition.Id)
//   * state         -> normalized QuestProgress (lifecycle + typed step + vars)
//   * wire          -> derived projection onto the legacy 704-byte QuestBitBlob
// The bitmask and the old Quest[area][id]Prog char_vars stop being independently
// mutated; they are re-projected from QuestProgress via QuestWireProjector, which reuses
// the byte-for-byte-verified QuestBitBlob ops (addQuest/completeQuest) as its output.
//
// Only lifecycle verbs matter to callers: Accept() and Complete() (as actions). Guards
// (When) and effects (KeyItem/Reputation/RoE/Message/Cutscene) are declarative data, so a
// quest's whole shape is inspectable without running it -- that is the "easier to extend"
// property the redesign is after.
using System;
using System.Collections.Generic;
using XI.Common;

namespace XI.Quests
{
    // Normalized lifecycle of one container on one character. The authoritative fact;
    // the wire bits are derived from it.
    public enum QuestLifecycle : byte
    {
        Locked = 0,     // prerequisite unmet
        Available = 1,  // can be started (this is a fresh QuestProgress's default)
        Active = 2,     // accepted -> wire current bit
        Completed = 3,  // done -> wire complete bit
    }

    // Reset cadence of a repeatable container. Only Daily is modelled now; it mirrors the
    // engine's daily reset boundary (Vana'diel midnight, the time_server daily tick). A
    // repeat is stamped on Complete (QuestRuntime.NextDailyReset) and re-opened by the
    // clock tick (QuestRuntime.ApplyDailyReset) -- kept as separate operations because on a
    // live server those are two different callers (the transition vs the daily tick).
    public enum QuestRepeat : byte
    {
        None = 0,
        Daily = 1,
    }

    // What fired a transition. Mirrors the trigger verbs the Lua framework exposed
    // (onTrigger/onEventTrigger/onZoneTrigger/onTrade...) reduced to a small kind set.
    public enum QuestTrigger : byte
    {
        Interact = 0,
        Trade = 1,
        ZoneEnter = 2,
        Talk = 3,
        Region = 4,
    }

    public enum QuestActionKind : byte
    {
        Accept, Complete, SetStep, GrantKeyItem, Reputation, RoEObjective, Message, Cutscene, SetVar,
    }

    // A declarative effect. Outward effects (KeyItem/Reputation/RoE/Message/Cutscene) are
    // recorded on the run's effect log; a real server pushes them to client + DB. Accept/
    // Complete/SetStep/SetVar mutate normalized state directly.
    public readonly struct QuestAction
    {
        public readonly QuestActionKind Kind;
        public readonly int IntArg;
        public readonly uint UintArg;
        public readonly string StrArg;
        public readonly ushort[] KeyItemsArg;

        private QuestAction(QuestActionKind k, int i = 0, uint u = 0, string s = null, ushort[] ki = null)
        {
            Kind = k; IntArg = i; UintArg = u; StrArg = s; KeyItemsArg = ki;
        }

        public static QuestAction Accept() => new QuestAction(QuestActionKind.Accept);
        public static QuestAction Complete() => new QuestAction(QuestActionKind.Complete);
        public static QuestAction Step(int step) => new QuestAction(QuestActionKind.SetStep, i: step);
        public static QuestAction KeyItem(ushort keyItem) => new QuestAction(QuestActionKind.GrantKeyItem, ki: new[] { keyItem });
        public static QuestAction KeyItems(params ushort[] keyItems) => new QuestAction(QuestActionKind.GrantKeyItem, ki: keyItems);
        public static QuestAction Reputation(uint zone, int amount) => new QuestAction(QuestActionKind.Reputation, i: amount, u: zone);
        public static QuestAction RoE(uint objectiveId) => new QuestAction(QuestActionKind.RoEObjective, u: objectiveId);
        public static QuestAction Message(int messageId) => new QuestAction(QuestActionKind.Message, i: messageId);
        public static QuestAction Cutscene(int cutsceneId) => new QuestAction(QuestActionKind.Cutscene, i: cutsceneId);
        public static QuestAction SetVar(string key, int value) => new QuestAction(QuestActionKind.SetVar, i: value, s: key);

        public override string ToString() => Kind + (StrArg != null ? $"({StrArg}={IntArg})" : IntArg != 0 || UintArg != 0 ? $"({IntArg}/{UintArg})" : "");
    }

    public sealed class QuestState
    {
        public int Step { get; }
        public string Name { get; }
        public bool Terminal { get; }
        public QuestState(int step, string name, bool terminal) { Step = step; Name = name; Terminal = terminal; }
    }

    public sealed class QuestTransition
    {
        public int FromStep { get; }
        public QuestTrigger Trigger { get; }
        public Func<QuestContext, bool> When { get; }
        public int ToStep { get; }
        public QuestAction[] Actions { get; }

        public QuestTransition(int from, QuestTrigger trigger, Func<QuestContext, bool> when, int to, QuestAction[] actions)
        {
            FromStep = from; Trigger = trigger; When = when; ToStep = to;
            Actions = actions ?? Array.Empty<QuestAction>();
        }
    }

    // The legacy numeric coordinate a stable quest id projects onto -- a WIRE concern only.
    // Renaming/re-homing a quest never touches these, so client-visible bytes are stable.
    public readonly struct QuestWireRef : IEquatable<QuestWireRef>
    {
        public QuestLog Area { get; }
        public ushort QuestId { get; }
        public QuestWireRef(QuestLog area, ushort questId) { Area = area; QuestId = questId; }
        public bool Equals(QuestWireRef o) => Area == o.Area && QuestId == o.QuestId;
        public override bool Equals(object o) => o is QuestWireRef r && Equals(r);
        public override int GetHashCode() => ((int)Area << 16) ^ QuestId;
        public override string ToString() => Area + "/" + QuestId;
    }

    // Compiled, immutable machine.
    public sealed class QuestMachine
    {
        public string Id { get; }
        public QuestWireRef Wire { get; }
        public IReadOnlyList<QuestState> States { get; }
        public QuestState Initial { get; }
        public IReadOnlyList<QuestTransition> Transitions { get; }
        public QuestRepeat Repeat { get; }

        private readonly Dictionary<int, QuestState> _byStep;

        public QuestMachine(string id, QuestWireRef wire, IReadOnlyList<QuestState> states,
                            QuestState initial, IReadOnlyList<QuestTransition> transitions,
                            QuestRepeat repeat = QuestRepeat.None)
        {
            Id = id; Wire = wire; States = states; Initial = initial; Transitions = transitions;
            Repeat = repeat;
            _byStep = new Dictionary<int, QuestState>();
            foreach (var s in states) _byStep[s.Step] = s;
        }

        public QuestState StateFor(int step) => _byStep.TryGetValue(step, out var s) ? s : null;
    }

    // Normalized per-character authoritative state (maps 1:1 to a char_quest_state row).
    public sealed class QuestProgress
    {
        public string QuestId { get; }
        public QuestLifecycle Lifecycle { get; set; }
        public int Step { get; set; }
        public long RepeatByVanaTime { get; set; }
        public Dictionary<string, long> Vars { get; } = new Dictionary<string, long>();

        public QuestProgress(string questId, int initialStep)
        {
            QuestId = questId; Step = initialStep; Lifecycle = QuestLifecycle.Available;
        }
    }

    // Read view a guard/action sees, plus the effect log the headless walker asserts on.
    public sealed class QuestContext
    {
        public uint CharId { get; }
        public QuestProgress Progress { get; }
        public QuestTrigger Trigger { get; }
        public ushort TradedItem { get; }
        public IReadOnlyCollection<ushort> KeyItemsHeld { get; }
        public Dictionary<string, long> PlayerVars { get; }

        // The Vana'diel clock (ticks since the vana epoch) at the moment of the trigger.
        // A live server injects vanadiel_clock::now(); the headless walker supplies a fixed
        // value so a daily's reopen window is stamped + advanced deterministically offline.
        // Defaults to 0 so non-repeating quests' contexts are unchanged.
        public long NowVanaTime { get; }

        public List<QuestAction> Fired { get; } = new List<QuestAction>();

        public QuestContext(uint charId, QuestProgress progress, QuestTrigger trigger,
                            ushort tradedItem = 0, IReadOnlyCollection<ushort> keyItems = null,
                            Dictionary<string, long> playerVars = null, long nowVanaTime = 0)
        {
            CharId = charId; Progress = progress; Trigger = trigger; TradedItem = tradedItem;
            KeyItemsHeld = keyItems ?? Array.Empty<ushort>();
            PlayerVars = playerVars ?? new Dictionary<string, long>();
            NowVanaTime = nowVanaTime;
        }

        public Dictionary<string, long> Vars => Progress.Vars;
        public bool HasKeyItem(ushort ki) { foreach (var k in KeyItemsHeld) if (k == ki) return true; return false; }
        public bool HasFlag(string name) => PlayerVars.TryGetValue(name, out var v) && v != 0;
    }

    // Fluent, compiler-checked definition builder.
    public sealed class QuestBuilder
    {
        private readonly List<QuestState> _states = new List<QuestState>();
        private readonly List<QuestTransition> _transitions = new List<QuestTransition>();
        private int _initialStep;
        private bool _initialSet;
        private QuestRepeat _repeat = QuestRepeat.None;

        public QuestBuilder State(int step, string name = "", bool terminal = false)
        {
            _states.Add(new QuestState(step, name, terminal));
            return this;
        }

        public QuestBuilder Initial(int step) { _initialStep = step; _initialSet = true; return this; }

        // Mark the container's reset cadence. Daily completes re-arm at the next Vana'diel
        // midnight (the time_server daily tick), stamped on Complete and applied by TryReopen.
        public QuestBuilder Repeat(QuestRepeat repeat) { _repeat = repeat; return this; }

        // No-guard overload: a bare action list. A lambda first-arg still binds
        // unambiguously to the guarded overload below (lambda is not a QuestAction).
        public QuestBuilder On(int from, QuestTrigger trigger, int to, params QuestAction[] actions)
        {
            _transitions.Add(new QuestTransition(from, trigger, null, to, actions));
            return this;
        }

        public QuestBuilder On(int from, QuestTrigger trigger, int to, Func<QuestContext, bool> when, params QuestAction[] actions)
        {
            _transitions.Add(new QuestTransition(from, trigger, when, to, actions));
            return this;
        }

        public QuestMachine Build(string id, QuestWireRef wire)
        {
            if (!_initialSet) throw new InvalidOperationException("quest has no Initial() state: " + id);
            var initial = _states.Find(s => s.Step == _initialStep)
                          ?? throw new InvalidOperationException("Initial() step is not a declared state: " + _initialStep);
            return new QuestMachine(id, wire, _states, initial, _transitions, _repeat);
        }
    }

    // Base class for a declared quest/mission. Content authors override Build().
    public abstract class QuestDefinition
    {
        public abstract string Id { get; }
        public abstract QuestWireRef Wire { get; }
        public abstract void Build(QuestBuilder b);

        public QuestMachine Compile()
        {
            var b = new QuestBuilder();
            Build(b);
            return b.Build(Id, Wire);
        }
    }

    // Drives one transition: first matching (from-step + trigger + guard) wins. Deterministic
    // -- matches declaration order, so author order is the resolution order (like the Lua
    // sections were, but now explicit + statically checkable).
    public static class QuestRuntime
    {
        public static bool Advance(QuestMachine m, QuestProgress p, QuestContext ctx)
        {
            foreach (var t in m.Transitions)
            {
                if (t.FromStep != p.Step) continue;
                if (t.Trigger != ctx.Trigger) continue;
                if (t.When != null && !t.When(ctx)) continue;

                foreach (var a in t.Actions)
                {
                    Apply(m, p, a, ctx);
                    ctx.Fired.Add(a);
                }
                p.Step = t.ToStep;
                return true;
            }
            return false;
        }

        // Whether a repeatable quest is currently inside its lockout (completed, but the
        // reopen window has not elapsed yet). Non-repeatable quests are never "on cooldown".
        public static bool OnCooldown(QuestProgress p, long nowVanaTime)
        {
            return p.Lifecycle == QuestLifecycle.Completed
                   && p.RepeatByVanaTime != 0
                   && nowVanaTime < p.RepeatByVanaTime;
        }

        // The daily tick: re-arm a completed repeatable quest whose reopen window has elapsed.
        // Mirrors the engine driving per-char resets from time_server's midnight tick. Returns
        // true if it flipped the quest back to a fresh (offerable) state. NowVanaTime is the
        // vana clock in ticks (VanaTime domain), supplied by the caller (or the headless walk).
        public static bool TryReopen(QuestMachine m, QuestProgress p, long nowVanaTime)
        {
            if (m.Repeat == QuestRepeat.None) return false;
            if (p.Lifecycle != QuestLifecycle.Completed) return false;
            if (p.RepeatByVanaTime == 0 || nowVanaTime < p.RepeatByVanaTime) return false;

            p.Lifecycle = QuestLifecycle.Available;
            p.Step = m.Initial.Step;
            p.RepeatByVanaTime = 0;
            p.Vars.Clear();
            return true;
        }

        private static void Apply(QuestMachine m, QuestProgress p, QuestAction a, QuestContext ctx)
        {
            switch (a.Kind)
            {
                case QuestActionKind.Accept: p.Lifecycle = QuestLifecycle.Active; break;
                case QuestActionKind.Complete:
                    p.Lifecycle = QuestLifecycle.Completed;
                    // A daily completion re-arms at the next Vana'diel midnight. Stamp the
                    // boundary from the injected clock via the oracle-verified accessor, so the
                    // reopen window matches the engine's daily tick exactly (ticks already on
                    // midnight map to that same boundary).
                    if (m.Repeat == QuestRepeat.Daily)
                        p.RepeatByVanaTime = VanaTime.NextMidnightTicks(ctx.NowVanaTime);
                    break;
                case QuestActionKind.SetStep: p.Step = a.IntArg; break;
                case QuestActionKind.SetVar: p.Vars[a.StrArg] = a.IntArg; break;
                // GrantKeyItem/Reputation/RoEObjective/Message/Cutscene are outward effects;
                // a server pushes them to client + DB. They never corrupt normalized state, so
                // the headless walker only needs to observe them on ctx.Fired.
            }
        }
    }

    // Normalized state -> legacy 704-byte wire snapshot, reusing the verified bit ops.
    public static class QuestWireProjector
    {
        public static void Project(QuestWireRef wire, QuestProgress p, QuestBitBlob blob)
        {
            int area = (int)wire.Area;
            switch (p.Lifecycle)
            {
                case QuestLifecycle.Active:
                    blob.AddQuest(area, wire.QuestId);       // current bit (addQuest)
                    break;
                case QuestLifecycle.Completed:
                    blob.CompleteQuest(area, wire.QuestId);  // complete bit, clears current (completeQuest)
                    break;
            }
        }
    }
}
