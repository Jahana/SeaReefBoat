// XI.Verify.Quests -- quest rework B3b Windurst group 14 (call-only: A Crisis in the Making at Ranpi-Monpi and the Altar
// of Offerings). The golden flows take every arm. These checks pin what no flow can see: the replies' kinds. In an NPC
// call the bridge renders an Event, a ProgressEvent and a PriorityEvent alike, as player:startEvent
// (ZoneHost.QuestCalls.cs RenderCallReply: no action is built, so no priority), which is what the Lua scripts call; the
// fake keeps them apart. W13Replies is in WindurstGroup13.cs.
using System;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup14()
    {
        Console.WriteLine("== quest rework B3b Windurst group 14: the reply kinds (call-only) ==");
        var crisis = new QC.Windurst.ACrisisInTheMaking().Compile();

        Check("windurst g14:A Crisis in the Making clean", "", string.Join(" | ", QuestScriptValidator.Validate(crisis)));

        // Ranpi-Monpi's arms and the altar's 53; the accepted arm at another var plays nothing (NoAction: the call
        // answers, so his standard dialog is not reached)
        Check("windurst g14:the trigger replies are the Lua's startEvent ids, as Event",
              "Ranpi-Monpi:Event(258,0,625),Ranpi-Monpi:Event(262,0,625),Ranpi-Monpi:Event(267),Ranpi-Monpi:NoAction()," +
              "Ranpi-Monpi:Event(259,0,625),Ranpi-Monpi:Event(262,0,625),Ranpi-Monpi:Event(268)," +
              "Altar_of_Offerings:Event(53)",
              W13Replies(crisis, QuestHandlerKind.OnTrigger));
    }
}
