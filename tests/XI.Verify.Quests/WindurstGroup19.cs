// XI.Verify.Quests -- quest rework B3b Windurst group 19 (call-only: Something Fishy at Tokaka). The golden flows take
// every arm. These checks pin what no flow can see: the replies' kinds. In an NPC call the bridge renders an Event, a
// ProgressEvent and a PriorityEvent alike, as player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply: no action is
// built, so no priority), which is what the Lua script calls; the fake keeps them apart. W15Handlers and W15Reply are
// in WindurstGroup15.cs.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup19()
    {
        Console.WriteLine("== quest rework B3b Windurst group 19: the reply kinds (call-only) ==");
        var fishy = new QC.Windurst.SomethingFishy().Compile();

        Check("windurst g19:Something Fishy clean", "", string.Join(" | ", QuestScriptValidator.Validate(fishy)));

        // Tokaka's trigger replies in declaration order (needToZone 211, accepted or completed 209, available 208)
        Check("windurst g19:the trigger replies are the Lua's startEvent ids, as Event",
              "Tokaka:Event(211),Tokaka:Event(209,0,4360),Tokaka:Event(208,0,4360)",
              string.Join(",", W15Handlers(fishy, QuestHandlerKind.OnTrigger).Select(h =>
                  $"{h.Entity}:{W15Reply(h, QuestStatus.Accepted, 1).Reply}")));

        // his trade (GIL_RATE 1): one sardine, then a Bone Chip
        var trade = W15Handlers(fishy, QuestHandlerKind.OnTrade).Single();
        Check("windurst g19:Tokaka's trade: one sardine, then another item, as Event",
              "Event(210,70,4360),null",
              string.Join(",", new[] { QC.Item.BastoreSardine1, QC.Item.BoneChip }.Select(i =>
                  W15Reply(trade, QuestStatus.Accepted, 1, (i, 1)).Reply)));
    }
}
