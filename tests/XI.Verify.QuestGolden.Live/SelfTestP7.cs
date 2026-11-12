using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using XI.Zone;

// quest rework P7: an OnZoneOut handler's label (waivers) and the bridge's names for every handler kind, in the self-test
// (no database). The twins of probe.zone_out and probe.mob_death are gone: both probes replay against the ports
// (ElderlyPursuits.cs, KnockingOnForbiddenDoors.cs)
internal static partial class Program
{
    private static void SelfTestPlatformP7()
    {
        Console.WriteLine("quest rework P7 hooks");
        SelfTestP7ZoneOutLabel();
        SelfTestP7BattlefieldLabels();
    }

    // a waiver may name a battlefield's group callback, exit NPC or essential group (group:<n>:<Callback>, exit:<Npc>,
    // essentialMobs:<n>), the three label kinds of BattlefieldScript.CoverageLabels the grammar takes; a malformed
    // one is refused
    private static void SelfTestP7BattlefieldLabels()
    {
        const string cs = "char zone=BastokMarkets pos=0,0,0\n" + "login\n";
        try
        {
            var f = Flow.Parse("uncovered: group:1:allDeath -- a\nuncovered: exit:Transcendental_Radiance -- b\n" +
                               "uncovered: essentialMobs:2 -- c\nuncovered: requiredItems:worn -- d\n" + cs, "w.flow",
                               "w", null);
            Check("  uncovered: group:<n>:<Callback>, exit:<Npc>, essentialMobs:<n>, requiredItems:worn (C3 Q4): " +
                  "battlefield labels",
                  "group:1:allDeath|exit:Transcendental_Radiance|essentialMobs:2|requiredItems:worn",
                  string.Join("|", f.Waivers.Select(w => w.Label)));
        }
        catch (FormatException e)
        {
            Fail("  uncovered: the battlefield labels", e.Message);
        }
        P5Refused("uncovered: group:0:allDeath (the groups count from 1) is no label",
                  "uncovered: group:0:allDeath -- x\n" + cs,
                  "t.flow:1: a handler label is <n>/<entity>/<trigger>: group:0:allDeath");
        P5Refused("uncovered: exit: with no NPC is no label", "uncovered: exit: -- x\n" + cs,
                  "t.flow:1: a handler label is <n>/<entity>/<trigger>: exit:");
        try
        {
            var f = Flow.Parse("uncovered: callback:deadBugbearReaction -- a\n" + cs, "w.flow", "w", null);
            Check("  uncovered: callback:<name> (C3 C3c): a battlefield's deferred callback",
                  "callback:deadBugbearReaction", string.Join("|", f.Waivers.Select(w => w.Label)));
        }
        catch (FormatException e)
        {
            Fail("  uncovered: a battlefield's callback label", e.Message);
        }
        P5Refused("uncovered: callback: with no name is no label", "uncovered: callback: -- x\n" + cs,
                  "t.flow:1: a handler label is <n>/<entity>/<trigger>: callback:");
        P5Refused("uncovered: requiredItems:wear (the content's own onBattlefieldEnter reaches it) is no label",
                  "uncovered: requiredItems:wear -- x\n" + cs,
                  "t.flow:1: a handler label is <n>/<entity>/<trigger>: requiredItems:wear");
    }

    // a waiver may name an onZoneOut handler (its label n/-/onZoneOut); the bridge labels it so, and names every handler kind
    // (a kind with no lookup key is refused, not put under a key the lookup never reads)
    private static void SelfTestP7ZoneOutLabel()
    {
        try
        {
            var f = Flow.Parse("uncovered: 2/-/onZoneOut -- never left at Prog 2\nuncovered: 2/-/onZoneOut@CarpentersLanding -- likewise\n" +
                               "char zone=BastokMarkets pos=0,0,0\nlogin\n", "w.flow", "w", null);
            Check("  uncovered: <n>/-/onZoneOut[@<Zone>]: a handler label", "2/-/onZoneOut|2/-/onZoneOut@CarpentersLanding",
                  string.Join("|", f.Waivers.Select(w => w.Label)));
        }
        catch (FormatException e)
        {
            Fail("  uncovered: <n>/-/onZoneOut", e.Message);
        }
        P5Refused("uncovered: an onZoneOut with an id is no label", "uncovered: 2/-/onZoneOut:1 -- x\nchar zone=BastokMarkets pos=0,0,0\nlogin\n",
                  "t.flow:1: a handler label is <n>/<entity>/<trigger>: 2/-/onZoneOut:1");
        try
        {
            var labels = ZoneHost.HandlerLabels(new P7EveryKindQuest().Compile());
            Check("  the bridge's label of each handler kind (one quest holding one of each; onZoneOut and afterZoneIn id-less)",
                  "1/Npc/onTrigger,1/Npc/onTrade,1/Mob/onMobDeath,1/-/onEventFinish:5,1/-/onEventUpdate:5,1/-/onZoneIn,1/-/onTriggerAreaEnter:3,1/-/afterZoneIn,1/-/onZoneOut,1/-/onTriggerAreaLeave:3",
                  string.Join(",", labels));
            Check("  ... every QuestHandlerKind among them", Enum.GetValues<QuestHandlerKind>().Length.ToString(), labels.Count.ToString());
        }
        catch (ArgumentOutOfRangeException e)
        {
            // ZoneHost.KindName refuses a kind it has no lookup key for
            Fail("  the bridge's label of each handler kind", e.Message);
        }
    }

    // one handler of each kind, in QuestHandlerKind's order
    private sealed class P7EveryKindQuest : QuestScript
    {
        public P7EveryKindQuest() : base(QuestLog.Jeuno, "P7_EVERY_KIND") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Accepted)
                .Npc(2, "Npc").OnTrigger(q => null).OnTrade(q => null)
                .Npc(2, "Mob").OnMobDeath(q => { })
                .OnEventFinish(2, 5, q => { })
                .OnEventUpdate(2, 5, q => { })
                .OnZoneIn(2, q => null)
                .OnTriggerAreaEnter(2, 3, q => { })
                .OnAfterZoneIn(2, q => null)
                .OnZoneOut(2, q => { })
                .OnTriggerAreaLeave(2, 3, q => { });
        }
    }
}
