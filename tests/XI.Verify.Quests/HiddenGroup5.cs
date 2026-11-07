// XI.Verify.Quests -- quest rework B3b hidden quests group 5 (the Tutorial): the definition validates clean. Its
// branches are the flows' (tests/golden/flows/hiddenQuests.tutorial_quest*.flow).
using System;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunHiddenGroup5()
    {
        Console.WriteLine("== quest rework B3b hidden quests group 5: the Tutorial ==");
        var tutorial = new QC.HiddenQuests.TutorialQuest();
        Check("hidden g5:Tutorial clean", "", string.Join(" | ", QuestScriptValidator.Validate(tutorial.Compile())));
        Check("hidden g5:Tutorial declares steps 1..12", "1,2,3,4,5,6,7,8,9,10,11,12",
              string.Join(",", tutorial.Compile().DeclaredSteps));
    }
}
