// XI.Quests -- Phase-6 mission authoring static validator.
//
// Shares the diagnostic vocabulary (QuestDiagnostic / QuestValidationResult) with the quest
// validator: the unified-container-taxonomy redesign uses ONE diagnostic model across
// containers, so callers get the same IsValid/ErrorCount/WarningCount/Has(code) surface for a
// MissionMachine as for a QuestMachine. The structural BFS rules are identical because the
// machines are isomorphic (states with a step + terminal flag, transitions with from/to); the
// ONE semantic difference is completion: a quest marks its wire bit via QuestAction.Complete,
// a mission via MissionAction.Complete (CompleteActive, which the projector replays as the
// engine's addMission+completeMission). Hence COMPLETE_MISSING checks the mission action kind.
//
// Deliberately NOT abstracted behind a shared generic over both machines: the action element
// types differ and the quest validator is oracle-verified -- refactoring proven code to remove
// ~structural duplication here would trade a real regression risk for cosmetic reuse. The
// duplication is confined to the BFS walk; the diagnostics + result are already shared.
using System.Collections.Generic;

namespace XI.Quests
{
    public static class MissionValidator
    {
        public static QuestValidationResult Validate(MissionMachine m)
        {
            var r = new QuestValidationResult();

            var declared = new HashSet<int>();
            foreach (var s in m.States)
            {
                if (!declared.Add(s.Step))
                    r.Diagnostics.Add(new QuestDiagnostic(QuestDiagnosticSeverity.Error, "DUPLICATE_STEP",
                        $"step {s.Step} declared more than once"));
            }

            var outgoing = new Dictionary<int, List<MissionTransition>>();
            foreach (var t in m.Transitions)
            {
                if (!declared.Contains(t.FromStep))
                    r.Diagnostics.Add(new QuestDiagnostic(QuestDiagnosticSeverity.Error, "DANGLING_FROM",
                        $"transition FromStep {t.FromStep} is not a declared state"));
                if (!declared.Contains(t.ToStep))
                    r.Diagnostics.Add(new QuestDiagnostic(QuestDiagnosticSeverity.Error, "DANGLING_TO",
                        $"transition ToStep {t.ToStep} is not a declared state"));

                if (!outgoing.TryGetValue(t.FromStep, out var list))
                    { list = new List<MissionTransition>(); outgoing[t.FromStep] = list; }
                list.Add(t);

                var fromState = m.StateFor(t.FromStep);
                if (fromState != null && fromState.Terminal)
                    r.Diagnostics.Add(new QuestDiagnostic(QuestDiagnosticSeverity.Warning, "TERMINAL_HAS_OUTGOING",
                        $"terminal state {t.FromStep} has an outgoing transition to {t.ToStep}"));

                var toState = m.StateFor(t.ToStep);
                if (toState != null && toState.Terminal)
                {
                    bool hasComplete = false, hasActivate = false;
                    foreach (var a in t.Actions)
                    {
                        if (a.Kind == MissionActionKind.CompleteActive) hasComplete = true;
                        if (a.Kind == MissionActionKind.SetActive) hasActivate = true;
                    }
                    if (!hasComplete)
                        r.Diagnostics.Add(new QuestDiagnostic(QuestDiagnosticSeverity.Error, "COMPLETE_MISSING",
                            $"transition into terminal {t.ToStep} lacks a Complete() action (wire complete[]/current never set)"));
                    if (hasComplete && hasActivate)
                        r.Diagnostics.Add(new QuestDiagnostic(QuestDiagnosticSeverity.Error, "ACTIVATE_AND_COMPLETE",
                            $"transition {t.FromStep}->{t.ToStep} both Activate()s and Complete()s (ambiguous)"));
                }
            }

            bool anyTerminal = false;
            foreach (var s in m.States) if (s.Terminal) { anyTerminal = true; break; }
            if (!anyTerminal)
                r.Diagnostics.Add(new QuestDiagnostic(QuestDiagnosticSeverity.Error, "NO_TERMINAL",
                    "machine declares no terminal state"));

            // BFS reachability from Initial.
            var reachable = new HashSet<int>();
            var queue = new Queue<int>();
            if (m.Initial != null) { queue.Enqueue(m.Initial.Step); reachable.Add(m.Initial.Step); }
            while (queue.Count > 0)
            {
                int step = queue.Dequeue();
                if (!outgoing.TryGetValue(step, out var outs)) continue;
                foreach (var t in outs)
                {
                    if (!declared.Contains(t.ToStep)) continue; // dangling; already flagged
                    if (reachable.Add(t.ToStep)) queue.Enqueue(t.ToStep);
                }
            }

            foreach (var s in m.States)
            {
                if (!reachable.Contains(s.Step))
                    r.Diagnostics.Add(new QuestDiagnostic(QuestDiagnosticSeverity.Warning, "ORPHAN_STATE",
                        $"state {s.Step} is unreachable from initial {m.Initial?.Step}"));
                else if (!s.Terminal && !outgoing.ContainsKey(s.Step))
                    r.Diagnostics.Add(new QuestDiagnostic(QuestDiagnosticSeverity.Error, "DEAD_END",
                        $"reachable non-terminal state {s.Step} has no outgoing transition (mission gets stuck)"));
            }

            if (anyTerminal)
            {
                bool terminalReached = false;
                foreach (var s in m.States) if (s.Terminal && reachable.Contains(s.Step)) { terminalReached = true; break; }
                if (!terminalReached)
                    r.Diagnostics.Add(new QuestDiagnostic(QuestDiagnosticSeverity.Error, "TERMINAL_UNREACHABLE",
                        "no terminal state is reachable from the initial state"));
            }

            return r;
        }
    }
}
