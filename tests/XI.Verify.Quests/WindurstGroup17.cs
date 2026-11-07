// XI.Verify.Quests -- quest rework B3b Windurst group 17 (call-only: Mandragora Mad at Yoran-Oran). The golden flows take
// every arm. These checks pin what no flow can see: the replies' kinds. In an NPC call the bridge renders an Event, a
// ProgressEvent and a PriorityEvent alike, as player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply: no action is
// built, so no priority), which is what the Lua script calls; the fake keeps them apart. W15Handlers and W15Reply are in
// WindurstGroup15.cs.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup17()
    {
        Console.WriteLine("== quest rework B3b Windurst group 17: the reply kinds (call-only) ==");
        var mad = new QC.Windurst.MandragoraMad().Compile();

        Check("windurst g17:Mandragora Mad clean", "", string.Join(" | ", QuestScriptValidator.Validate(mad)));

        // Yoran-Oran's trigger replies in declaration order (available 249, accepted 256)
        Check("windurst g17:the trigger replies are the Lua's startEvent ids, as Event",
              "Yoran-Oran:Event(249),Yoran-Oran:Event(256)",
              string.Join(",", W15Handlers(mad, QuestHandlerKind.OnTrigger).Select(h =>
                  $"{h.Entity}:{W15Reply(h, QuestStatus.Accepted, 1).Reply}")));

        // his trade, accepted (GIL_RATE 1): each turn-in item alone, then a Bone Chip
        var trade = W15Handlers(mad, QuestHandlerKind.OnTrade).Single();
        int[] items = { QC.Item.Cornette, QC.Item.PinchOfYuhtungaSulfur, QC.Item.ThreeLeafMandragoraBud,
                        QC.Item.FourLeafMandragoraBud, QC.Item.SnobbyLetter, QC.Item.BoneChip };
        Check("windurst g17:Yoran-Oran's trade: each turn-in, then another item, as Event",
              "Event(251,200),Event(252,250),Event(253,1200),Event(254,120),Event(255,5500),Event(250)",
              string.Join(",", items.Select(i => W15Reply(trade, QuestStatus.Accepted, 1, (i, 1)).Reply)));
    }
}
