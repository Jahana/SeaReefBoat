// XI.Verify.Quests -- quest rework B3b Windurst group 16 (call-only: Creepy Crawlies at Illu Bohjaa). The golden flows
// take every arm. These checks pin what no flow can see: the replies' kinds. In an NPC call the bridge renders an Event,
// a ProgressEvent and a PriorityEvent alike, as player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply: no action is
// built, so no priority), which is what the Lua script calls; the fake keeps them apart. W15Handlers and W15Reply are
// in WindurstGroup15.cs.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup16()
    {
        Console.WriteLine("== quest rework B3b Windurst group 16: the reply kinds (call-only) ==");
        var crawlies = new QC.Windurst.CreepyCrawlies().Compile();

        Check("windurst g16:Creepy Crawlies clean", "", string.Join(" | ", QuestScriptValidator.Validate(crawlies)));

        // Illu Bohjaa's trigger replies in declaration order, and his trade (three spools, GIL_RATE 1: 600)
        Check("windurst g16:the trigger replies are the Lua's startEvent ids, as Event",
              "Illu_Bohjaa:Event(333,0,816,938,1156),Illu_Bohjaa:Event(334,0,816,938,1156)",
              string.Join(",", W15Handlers(crawlies, QuestHandlerKind.OnTrigger).Select(h =>
                  $"{h.Entity}:{W15Reply(h, QuestStatus.Accepted, 1).Reply}")));
        Check("windurst g16:Illu Bohjaa's trade: three spools, as Event",
              "Illu_Bohjaa:Event(335,600,816,938,1156)",
              string.Join(",", W15Handlers(crawlies, QuestHandlerKind.OnTrade).Select(h =>
                  $"{h.Entity}:{W15Reply(h, QuestStatus.Accepted, 1, (QC.Item.SpoolOfSilkThread, 3)).Reply}")));
    }
}
