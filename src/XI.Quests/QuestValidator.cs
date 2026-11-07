// XI.Quests — Phase-6 quest authoring static validator.
//
// The redesign's promise is "illegal states unrepresentable". The state machine lets an
// author declare anything, so this validator turns the structural + semantic rules the old
// Lua section system relied on (and frequently got wrong -- the split-brain blob/char_vars
// drift, quests that could never complete) into explicit, machine-checkable diagnostics.
//
// Checks (reachability is BFS from Initial over the transition graph):
//   * DUPLICATE_STEP      -- two states declared with the same step (builder dedups silently).
//   * DANGLING_FROM       -- a transition's FromStep names no declared state.
//   * DANGLING_TO         -- a transition's ToStep   names no declared state.
//   * NO_TERMINAL         -- the machine declares no terminal state.
//   * TERMINAL_UNREACHABLE-- no terminal state is reachable from Initial (quest can't finish).
//   * DEAD_END            -- a reachable non-terminal state with no outgoing transition.
//   * ORPHAN_STATE        -- a declared state unreachable from Initial.
//   * TERMINAL_HAS_OUTGOING-- a transition leaves a terminal state (should be a leaf).
//   * COMPLETE_MISSING    -- a transition INTO a terminal state lacks a Complete() action, so
//                            QuestWireProjector would never set the wire complete bit. Guards
//                            the normalized->wire invariant directly.
//   * ACCEPT_AND_COMPLETE -- a single action list both Accept()s and Complete()s (ambiguous).
using System.Collections.Generic;

namespace XI.Quests
{
    public enum QuestDiagnosticSeverity : byte { Warning, Error }

    public readonly struct QuestDiagnostic
    {
        public readonly QuestDiagnosticSeverity Severity;
        public readonly string Code;
        public readonly string Message;
        public QuestDiagnostic(QuestDiagnosticSeverity sev, string code, string msg)
        {
            Severity = sev; Code = code; Message = msg;
        }
        public override string ToString() => $"[{Severity}] {Code}: {Message}";
    }

    public sealed class QuestValidationResult
    {
        public readonly List<QuestDiagnostic> Diagnostics = new List<QuestDiagnostic>();
        public bool IsValid { get { foreach (var d in Diagnostics) if (d.Severity == QuestDiagnosticSeverity.Error) return false; return true; } }
        public int ErrorCount { get { int n = 0; foreach (var d in Diagnostics) if (d.Severity == QuestDiagnosticSeverity.Error) n++; return n; } }
        public int WarningCount { get { int n = 0; foreach (var d in Diagnostics) if (d.Severity == QuestDiagnosticSeverity.Warning) n++; return n; } }
        public bool Has(string code) { foreach (var d in Diagnostics) if (d.Code == code) return true; return false; }
    }

    public static class QuestValidator
    {
        public static QuestValidationResult Validate(QuestMachine m)
        {
            var r = new QuestValidationResult();

            // Index declared steps + detect duplicates. QuestMachine.States keeps the raw list
            // (StateFor dedups via a dictionary), so iterate States for duplicates.
            var declared = new HashSet<int>();
            foreach (var s in m.States)
            {
                if (!declared.Add(s.Step))
                    r.Diagnostics.Add(new QuestDiagnostic(QuestDiagnosticSeverity.Error, "DUPLICATE_STEP",
                        $"step {s.Step} declared more than once"));
            }

            // Outgoing adjacency for BFS; also dangling-endpoint + terminal rules.
            var outgoing = new Dictionary<int, List<QuestTransition>>();
            foreach (var t in m.Transitions)
            {
                if (!declared.Contains(t.FromStep))
                    r.Diagnostics.Add(new QuestDiagnostic(QuestDiagnosticSeverity.Error, "DANGLING_FROM",
                        $"transition FromStep {t.FromStep} is not a declared state"));
                if (!declared.Contains(t.ToStep))
                    r.Diagnostics.Add(new QuestDiagnostic(QuestDiagnosticSeverity.Error, "DANGLING_TO",
                        $"transition ToStep {t.ToStep} is not a declared state"));

                if (!outgoing.TryGetValue(t.FromStep, out var list))
                    { list = new List<QuestTransition>(); outgoing[t.FromStep] = list; }
                list.Add(t);

                var fromState = m.StateFor(t.FromStep);
                if (fromState != null && fromState.Terminal)
                    r.Diagnostics.Add(new QuestDiagnostic(QuestDiagnosticSeverity.Warning, "TERMINAL_HAS_OUTGOING",
                        $"terminal state {t.FromStep} has an outgoing transition to {t.ToStep}"));

                var toState = m.StateFor(t.ToStep);
                if (toState != null && toState.Terminal)
                {
                    bool hasComplete = false, hasAccept = false;
                    foreach (var a in t.Actions)
                    {
                        if (a.Kind == QuestActionKind.Complete) hasComplete = true;
                        if (a.Kind == QuestActionKind.Accept) hasAccept = true;
                    }
                    if (!hasComplete)
                        r.Diagnostics.Add(new QuestDiagnostic(QuestDiagnosticSeverity.Error, "COMPLETE_MISSING",
                            $"transition into terminal {t.ToStep} lacks a Complete() action (wire complete bit never set)"));
                    if (hasComplete && hasAccept)
                        r.Diagnostics.Add(new QuestDiagnostic(QuestDiagnosticSeverity.Error, "ACCEPT_AND_COMPLETE",
                            $"transition {t.FromStep}->{t.ToStep} both Accept()s and Complete()s"));
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

            // Orphans + dead-ends.
            foreach (var s in m.States)
            {
                if (!reachable.Contains(s.Step))
                    r.Diagnostics.Add(new QuestDiagnostic(QuestDiagnosticSeverity.Warning, "ORPHAN_STATE",
                        $"state {s.Step} is unreachable from initial {m.Initial?.Step}"));
                else if (!s.Terminal && !outgoing.ContainsKey(s.Step))
                    r.Diagnostics.Add(new QuestDiagnostic(QuestDiagnosticSeverity.Error, "DEAD_END",
                        $"reachable non-terminal state {s.Step} has no outgoing transition (quest gets stuck)"));
            }

            // Terminal reachability.
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
