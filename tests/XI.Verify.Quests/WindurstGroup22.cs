// XI.Verify.Quests -- quest rework B3b Windurst group 22 (call-only: The All-New C-2000 at Kopuro-Popuro, Kororo and
// Boizo-Naizo, Legendary Plan B at Kopuro-Popuro and Kororo, The All-New C-3000 at Kopuro-Popuro, Can Cardians Cry? at
// Apururu). The golden flows take every arm. These checks pin what no flow can see: the replies' kinds. In an NPC call
// the bridge renders an Event, a ProgressEvent and a PriorityEvent alike, as player:startEvent (ZoneHost.QuestCalls.cs
// RenderCallReply: no action is built, so no priority), which is what the Lua scripts call; the fake keeps them apart.
// W15Handlers and W15Reply are in WindurstGroup15.cs.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup22()
    {
        Console.WriteLine("== quest rework B3b Windurst group 22: the reply kinds (call-only) ==");
        var c2000 = new QC.Windurst.TheAllNewC2000().Compile();
        var planB = new QC.Windurst.LegendaryPlanB().Compile();
        var c3000 = new QC.Windurst.TheAllNewC3000().Compile();
        var cry = new QC.Windurst.CanCardiansCry().Compile();

        Check("windurst g22:The All-New C-2000 clean", "", string.Join(" | ", QuestScriptValidator.Validate(c2000)));
        Check("windurst g22:Legendary Plan B clean", "", string.Join(" | ", QuestScriptValidator.Validate(planB)));
        Check("windurst g22:The All-New C-3000 clean", "", string.Join(" | ", QuestScriptValidator.Validate(c3000)));
        Check("windurst g22:Can Cardians Cry? clean", "", string.Join(" | ", QuestScriptValidator.Validate(cry)));

        string Triggers(QuestScriptDefinition def) =>
            string.Join(",", W15Handlers(def, QuestHandlerKind.OnTrigger).Select(h =>
                $"{h.Entity}:{W15Reply(h, QuestStatus.Accepted, 1).Reply}"));

        // the trigger replies in declaration order
        Check("windurst g22:The All-New C-2000's trigger replies are the Lua's startEvent ids, as Event",
              "Kopuro-Popuro:Event(285,0,856,846,4368),Kopuro-Popuro:Event(288,0,856,846,4368),Kororo:Event(291)," +
              "Boizo-Naizo:Event(290),Kopuro-Popuro:Event(293)",
              Triggers(c2000));
        Check("windurst g22:Legendary Plan B's trigger replies are the Lua's startEvent ids, as Event",
              "Kopuro-Popuro:Event(301),Kopuro-Popuro:Event(306),Kopuro-Popuro:Event(308,0,529,940,858)," +
              "Kopuro-Popuro:Event(309,0,529,940,858),Kororo:Event(312,0,529,940,858),Kopuro-Popuro:Event(316)",
              Triggers(planB));
        Check("windurst g22:The All-New C-3000's trigger replies are the Lua's startEvent ids, as Event",
              "Kopuro-Popuro:Event(316),Kopuro-Popuro:Event(655,0,889,939),Kopuro-Popuro:Event(659,0,889,939)",
              Triggers(c3000));
        Check("windurst g22:Can Cardians Cry?'s trigger replies are the Lua's startEvent ids, as Event",
              "Apururu:Event(319,0,20000),Apururu:Event(320,0,20000),Apururu:Event(330)",
              Triggers(cry));

        // the trades (GIL_RATE 1): the items, then one missing
        string Trade(QuestScriptDefinition def, QuestStatus status, params (int Item, int Qty)[] items) =>
            W15Reply(W15Handlers(def, QuestHandlerKind.OnTrade).Single(), status, 1, items).Reply;
        Check("windurst g22:the trades: the items, then one missing, as Event (Can Cardians Cry?: none)",
              "Event(292,200),Event(288,0,856,846,4368)|Event(314,0,529,940,858),Event(309,0,529,940,858)" +
              "|Event(657,0,889,939),Event(656,0,889,939)|Event(325,0,20000,5000),null",
              Trade(c2000, QuestStatus.Accepted, (QC.Item.InsectWing, 1), (QC.Item.RabbitHide, 1),
                    (QC.Item.TwoLeafMandragoraBud, 1)) + "," +
              Trade(c2000, QuestStatus.Accepted, (QC.Item.InsectWing, 1), (QC.Item.RabbitHide, 1)) + "|" +
              Trade(planB, QuestStatus.Accepted, (529, 1), (QC.Item.WolfHide, 1), (QC.Item.RevivalTreeRoot, 1)) + "," +
              Trade(planB, QuestStatus.Accepted, (QC.Item.WolfHide, 1), (QC.Item.RevivalTreeRoot, 1)) + "|" +
              Trade(c3000, QuestStatus.Completed, (QC.Item.BeetleShell, 1), (QC.Item.HecteyesEye, 1)) + "," +
              Trade(c3000, QuestStatus.Completed, (QC.Item.HecteyesEye, 1)) + "|" +
              Trade(cry, QuestStatus.Accepted, (QC.Item.BruisedStarfruit, 1)) + "," +
              Trade(cry, QuestStatus.Accepted, (QC.Item.WolfHide, 1)));
    }
}
