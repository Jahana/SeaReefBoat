// XI.Quests -- the MISSION authoring core, the unified-container-taxonomy half of the
// Phase-6 redesign. A MissionDefinition drives the SAME explicit state-machine pattern the
// quest authoring core uses (QuestDefinition/QuestBuilder/QuestRuntime/QuestValidator), but
// its NORMALIZED state is a per-area mission log, not a per-quest bit pair. This is the
// "easier to extend" proof: one authoring shape covers two containers whose storage is
// structurally unrelated (quest = current/complete bitmask; mission = current-id pointer +
// complete[] + 32-bit status, with CoP/highid completion-by-current).
//
// Identity (stable string) / state (MissionProgress) / wire (derived projection onto the
// byte-for-byte-verified MissionBitBlob) stay separated exactly as for quests. The projector
// reproduces the legacy 1050-byte blob by REPLAYING the engine's own verified ops (AddMission
// then CompleteMission per completed step, then AddMission for the active step), so the wire
// output is by-construction oracle-correct -- the same trust anchor as QuestWireProjector.
using System;
using System.Collections.Generic;

namespace XI.Quests
{
    // What fires a mission transition. Reuses the quest trigger set (same interaction verbs)
    // -- the unified taxonomy uses ONE trigger enum across containers.
    // (QuestTrigger is defined in QuestAuthoring.cs; missions reference it directly.)

    public enum MissionActionKind : byte
    {
        SetActive,      // begin this mission -> blob.AddMission(area, id); current = id
        CompleteActive, // finish this mission -> blob.AddMission + CompleteMission (marks complete[] / advances CoP current)
        SetStatus,      // blob.SetMissionStatusFull(area, value)
        SetStatusNibble,// blob.SetMissionStatusNibble(area, value, pos)
        Message,        // outward: recorded on the effect log
        Cutscene,       // outward: recorded on the effect log
    }

    // A declarative effect on the normalized MissionProgress (and, for outward kinds, the log).
    public readonly struct MissionAction
    {
        public readonly MissionActionKind Kind;
        public readonly int IntArg;
        public readonly uint UintArg;
        public readonly string StrArg;

        private MissionAction(MissionActionKind k, int i = 0, uint u = 0, string s = null)
        {
            Kind = k; IntArg = i; UintArg = u; StrArg = s;
        }

        public static MissionAction Activate(ushort missionId) => new MissionAction(MissionActionKind.SetActive, i: missionId);
        public static MissionAction Complete(ushort missionId) => new MissionAction(MissionActionKind.CompleteActive, i: missionId);
        public static MissionAction Status(uint value) => new MissionAction(MissionActionKind.SetStatus, u: value);
        public static MissionAction StatusNibble(int pos, int value) => new MissionAction(MissionActionKind.SetStatusNibble, i: value, u: (uint)pos);
        public static MissionAction Message(int messageId) => new MissionAction(MissionActionKind.Message, i: messageId);
        public static MissionAction Cutscene(int cutsceneId) => new MissionAction(MissionActionKind.Cutscene, i: cutsceneId);

        public override string ToString() => Kind + (StrArg != null ? $"({StrArg})" : IntArg != 0 || UintArg != 0 ? $"({IntArg}/{UintArg})" : "");
    }

    // A declared story-beat. Carries the wire mission id this step corresponds to.
    public sealed class MissionState
    {
        public int Step { get; }
        public string Name { get; }
        public int MissionId { get; }   // the wire mission id while active; -1 = none/sentinel
        public bool Terminal { get; }
        public MissionState(int step, string name, int missionId, bool terminal)
        {
            Step = step; Name = name; MissionId = missionId; Terminal = terminal;
        }
    }

    public sealed class MissionTransition
    {
        public int FromStep { get; }
        public QuestTrigger Trigger { get; }
        public Func<MissionContext, bool> When { get; }
        public int ToStep { get; }
        public MissionAction[] Actions { get; }

        public MissionTransition(int from, QuestTrigger trigger, Func<MissionContext, bool> when, int to, MissionAction[] actions)
        {
            FromStep = from; Trigger = trigger; When = when; ToStep = to;
            Actions = actions ?? Array.Empty<MissionAction>();
        }
    }

    public sealed class MissionMachine
    {
        public string Id { get; }
        public MissionLog Area { get; }
        public IReadOnlyList<MissionState> States { get; }
        public MissionState Initial { get; }
        public IReadOnlyList<MissionTransition> Transitions { get; }

        private readonly Dictionary<int, MissionState> _byStep;

        public MissionMachine(string id, MissionLog area, IReadOnlyList<MissionState> states,
                             MissionState initial, IReadOnlyList<MissionTransition> transitions)
        {
            Id = id; Area = area; States = states; Initial = initial; Transitions = transitions;
            _byStep = new Dictionary<int, MissionState>();
            foreach (var s in states) _byStep[s.Step] = s;
        }

        public MissionState StateFor(int step) => _byStep.TryGetValue(step, out var s) ? s : null;
    }

    // Normalized per-character authoritative mission state (maps 1:1 to a char_mission_state
    // row for this area). The ordered Completed list + Active mission id are what the wire
    // projection replays through the verified ops; Status mirrors the 32-bit field.
    public sealed class MissionProgress
    {
        public string MissionId { get; }   // the stable string container id (the definition's Id)
        public int Step { get; set; }
        public int ActiveMission { get; set; } = -1;   // -1 == at the per-area sentinel (no active)
        public uint Status { get; set; }
        public readonly List<int> Completed = new List<int>();   // wire ids, in completion order

        public MissionProgress(string missionId, int initialStep, int initialActive)
        {
            MissionId = missionId; Step = initialStep; ActiveMission = initialActive;
        }
    }

    // Read view a guard/action sees, plus the effect log the headless walker asserts on.
    public sealed class MissionContext
    {
        public uint CharId { get; }
        public MissionProgress Progress { get; }
        public QuestTrigger Trigger { get; }
        public ushort TradedItem { get; }
        public long NowVanaTime { get; }
        public List<MissionAction> Fired { get; } = new List<MissionAction>();

        public MissionContext(uint charId, MissionProgress progress, QuestTrigger trigger,
                             ushort tradedItem = 0, long nowVanaTime = 0)
        {
            CharId = charId; Progress = progress; Trigger = trigger;
            TradedItem = tradedItem; NowVanaTime = nowVanaTime;
        }
    }

    // Fluent, compiler-checked definition builder (mirrors QuestBuilder).
    public sealed class MissionBuilder
    {
        private readonly List<MissionState> _states = new List<MissionState>();
        private readonly List<MissionTransition> _transitions = new List<MissionTransition>();
        private int _initialStep;
        private bool _initialSet;

        public MissionBuilder State(int step, int missionId = -1, string name = "", bool terminal = false)
        {
            _states.Add(new MissionState(step, name, missionId, terminal));
            return this;
        }

        public MissionBuilder Initial(int step) { _initialStep = step; _initialSet = true; return this; }

        public MissionBuilder On(int from, QuestTrigger trigger, int to, params MissionAction[] actions)
        {
            _transitions.Add(new MissionTransition(from, trigger, null, to, actions));
            return this;
        }

        public MissionBuilder On(int from, QuestTrigger trigger, int to, Func<MissionContext, bool> when, params MissionAction[] actions)
        {
            _transitions.Add(new MissionTransition(from, trigger, when, to, actions));
            return this;
        }

        public MissionMachine Build(string id, MissionLog area)
        {
            if (!_initialSet) throw new InvalidOperationException("mission has no Initial() state: " + id);
            var initial = _states.Find(s => s.Step == _initialStep)
                          ?? throw new InvalidOperationException("Initial() step is not a declared state: " + _initialStep);
            return new MissionMachine(id, area, _states, initial, _transitions);
        }
    }

    // Base class for a declared mission. Content authors override Build().
    public abstract class MissionDefinition
    {
        public abstract string Id { get; }
        public abstract MissionLog Area { get; }
        public abstract void Build(MissionBuilder b);

        public MissionMachine Compile()
        {
            var b = new MissionBuilder();
            Build(b);
            return b.Build(Id, Area);
        }
    }

    // Drives one transition: first matching (from-step + trigger + guard) wins, actions apply
    // in DECLARED ORDER (same determinism contract as QuestRuntime). Mutates MissionProgress.
    public static class MissionRuntime
    {
        public static bool Advance(MissionMachine m, MissionProgress p, MissionContext ctx)
        {
            foreach (var t in m.Transitions)
            {
                if (t.FromStep != p.Step) continue;
                if (t.Trigger != ctx.Trigger) continue;
                if (t.When != null && !t.When(ctx)) continue;

                foreach (var a in t.Actions)
                {
                    Apply(m, p, a);
                    ctx.Fired.Add(a);
                }
                p.Step = t.ToStep;
                return true;
            }
            return false;
        }

        private static void Apply(MissionMachine m, MissionProgress p, MissionAction a)
        {
            switch (a.Kind)
            {
                case MissionActionKind.SetActive:
                    p.ActiveMission = a.IntArg;
                    break;
                case MissionActionKind.CompleteActive:
                    // Completing moves this id into the ordered completed list and clears active
                    // (the engine then holds the per-area sentinel until the next addMission).
                    if (!p.Completed.Contains(a.IntArg)) p.Completed.Add(a.IntArg);
                    p.ActiveMission = -1;
                    break;
                case MissionActionKind.SetStatus:
                    p.Status = a.UintArg;
                    break;
                case MissionActionKind.SetStatusNibble:
                    // fold one 4-bit nibble into the 32-bit status (matches setMissionStatus idx form)
                    uint mask = ~(0xFu << (4 * (int)a.UintArg));
                    p.Status = (p.Status & mask) | ((uint)a.IntArg << (4 * (int)a.UintArg));
                    break;
                // Message/Cutscene are outward effects, recorded on ctx.Fired only.
            }
        }
    }

    // Normalized mission state -> legacy 1050-byte wire snapshot. Reproduced by replaying the
    // engine's own byte-verified ops, so the projected blob is oracle-correct by construction.
    public static class MissionWireProjector
    {
        public static void Project(MissionLog area, MissionProgress p, MissionBitBlob blob)
        {
            int a = (int)area;
            // completed story beats, in order: add then complete each (marks complete[] / advances
            // CoP current exactly as the engine did). Fresh blob already has the sentinel init.
            foreach (int id in p.Completed)
            {
                blob.AddMission(a, (ushort)id);
                blob.CompleteMission(a, (ushort)id);
            }
            // the active (not yet completed) mission, if any
            if (p.ActiveMission >= 0)
                blob.AddMission(a, (ushort)p.ActiveMission);
            // the 32-bit status field, verbatim
            blob.SetMissionStatusFull(a, p.Status);
        }
    }
}
