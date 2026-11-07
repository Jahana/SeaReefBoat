using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using XI.Zone;

// quest rework P10: a deferred callback's label
// (0/-/callback:<name>: the bridge's, after the states' handlers, and the waiver regex's separate state-0 alternative),
// in the self-test (no database). The callbacks run live in QuestScripts.Live P10 and in the golden probe.player_timer
// and windurst.hitting_the_marquisate#chandelier
internal static partial class Program
{
    private static void SelfTestPlatformP10()
    {
        Console.WriteLine("quest rework P10 hooks");
        SelfTestP10CallbackLabel();
        SelfTestHeal();
    }

    // B3b Adoulin group 4 (Dances with Luopans' healing hooks): heal | heal stop, parsed, refused, and its run-time guards
    private static void SelfTestHeal()
    {
        try
        {
            var f = Flow.Parse("char zone=CeizakBattlegrounds pos=0,0,0\nlogin\nheal\nheal stop   # a comment\n", "h.flow", "h", null);
            Check("  heal / heal stop: steps, the mode On 1 / Off 2", "heal 1|heal stop 2",
                  string.Join("|", f.Steps.Skip(1).Select(st => $"{st.Label} {st.Amount}")));
            Check("  a flow's hash: heal and heal stop differ", "True", (Flow.Hash("heal\n") != Flow.Hash("heal stop\n")).ToString());
        }
        catch (FormatException e)
        {
            Fail("  heal / heal stop", e.Message);
        }
        const string sd = "char zone=CeizakBattlegrounds pos=0,0,0\n";
        P5Refused("heal with another word", sd + "login\nheal start\n", "t.flow:3: heal takes nothing or stop: start");
        P5Refused("heal with two words", sd + "login\nheal stop now\n", "t.flow:3: heal takes 0 to 1 arguments");
        P5Refused("heal before the login", sd + "heal\nlogin\n", "t.flow:2: step before login");
        Check("  heal in an open event: the error names the step, the event and the engine's drop",
              "heal: event 34 is open, and the engine drops a 0x0E8 sent in an event (BlockedState.InEvent), so the step would record nothing: finish or update it first",
              EventOpenProblem("heal", "heal", 34));
        Check("  heal: quiet when the char rests after it (heal) or no longer does (heal stop); else it fails the run",
              "-|-|heal: the char is not resting after the 0x0E8 (resting already, engaged, dead, or a status that blocks it?)|" +
              "heal stop: the char is still resting after the 0x0E8 (was it resting?)",
              $"{HealProblem(1, true) ?? "-"}|{HealProblem(2, false) ?? "-"}|{HealProblem(1, false) ?? "-"}|{HealProblem(2, true) ?? "-"}");
    }

    private static void SelfTestP10CallbackLabel()
    {
        try
        {
            var f = Flow.Parse("uncovered: 0/-/callback:stoneTimer -- never fired\nuncovered: 0/-/callback:tebhi_Fade2 -- likewise\n" +
                               "char zone=BastokMarkets pos=0,0,0\nlogin\n", "w.flow", "w", null);
            Check("  uncovered: 0/-/callback:<name>: a callback's label (state 0, entity -)", "0/-/callback:stoneTimer|0/-/callback:tebhi_Fade2",
                  string.Join("|", f.Waivers.Select(w => w.Label)));
        }
        catch (FormatException e)
        {
            Fail("  uncovered: 0/-/callback:<name>", e.Message);
        }
        const string Tail = " -- x\nchar zone=BastokMarkets pos=0,0,0\nlogin\n";
        P5Refused("uncovered: a state-0 handler label stays refused (states count from 1)", "uncovered: 0/Npc/onTrigger" + Tail,
                  "t.flow:1: a handler label is <n>/<entity>/<trigger>: 0/Npc/onTrigger");
        P5Refused("uncovered: a callback of an entity is no label", "uncovered: 0/Npc/callback:x" + Tail,
                  "t.flow:1: a handler label is <n>/<entity>/<trigger>: 0/Npc/callback:x");
        P5Refused("uncovered: a callback in a state is no label", "uncovered: 1/-/callback:x" + Tail,
                  "t.flow:1: a handler label is <n>/<entity>/<trigger>: 1/-/callback:x");
        P5Refused("uncovered: a callback without its name is no label", "uncovered: 0/-/callback" + Tail,
                  "t.flow:1: a handler label is <n>/<entity>/<trigger>: 0/-/callback");
        P5Refused("uncovered: a callback name not [A-Za-z][A-Za-z0-9_]* is no label", "uncovered: 0/-/callback:1x" + Tail,
                  "t.flow:1: a handler label is <n>/<entity>/<trigger>: 0/-/callback:1x");
        P5Refused("uncovered: a callback with a zone is no label", "uncovered: 0/-/callback:x@BastokMarkets" + Tail,
                  "t.flow:1: a handler label is <n>/<entity>/<trigger>: 0/-/callback:x@BastokMarkets");
        var labels = ZoneHost.HandlerLabels(new P10CallbackQuest().Compile());
        Check("  the bridge's labels: the states' handlers, then each callback as 0/-/callback:<name>, in declaration order",
              "1/-/onEventFinish:19,2/Npc/onTrigger,0/-/callback:toWhitegate,0/-/callback:stoneTimer", string.Join(",", labels));
    }

    private sealed class P10CallbackQuest : QuestScript
    {
        public P10CallbackQuest() : base(QuestLog.Jeuno, "P10_CALLBACKS") { }

        protected override void Build(QuestScriptBuilder b)
        {
            var toWhitegate = b.Callback("toWhitegate", q => { });
            b.Callback("stoneTimer", q => { });
            b.State(QuestStatus.Accepted).OnEventFinish(2, 19, q => q.Player.Timer(1000, toWhitegate));
            b.State(QuestStatus.Completed).Npc(2, "Npc").OnTrigger(q => null);
        }
    }
}
