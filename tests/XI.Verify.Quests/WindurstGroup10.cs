// XI.Verify.Quests -- quest rework B3b Windurst group 10 (Let Sleeping Dogs Lie, Making the Grade, Teacher's Pet,
// Flower Child, In a Stew): what no trace can see. Teacher's Pet's 440, Flower Child's 10000 (option 3002) and In a
// Stew's 239 test quest:complete, but their rewards hold no item (fame, gil), so completeQuest never fails in the game:
// the flows finish each with a full bag and the quest still completes. The false arm (nothing after the completion) is
// shown here, on the fake, with the completion made to fail.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup10()
    {
        Console.WriteLine("== quest rework B3b Windurst group 10: the completions' false arms (rewards with no item) ==");
        RunWindurstGroup10Completions();
    }

    // the onEventFinish of csid, wherever its state is, run on a fake whose quest:complete returns completes
    static string W10Finish(QuestScriptDefinition def, int csid, int option, bool completes, Action<Fake> setup = null)
    {
        var h = def.States.SelectMany(s => s.Handlers).Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Id == csid);
        var f = new Fake { Status = QuestStatus.Accepted, Csid = csid, Option = option, CompleteResult = completes };
        setup?.Invoke(f);
        h.Run(f);
        return string.Join(";", f.Calls);
    }

    static void RunWindurstGroup10Completions()
    {
        var tp = new QC.Windurst.TeachersPet().Compile();
        Check("windurst g10 completions:Teacher's Pet's 440 sets Making the Grade's mustZone only when the completion succeeds",
              "Trade.Confirm();Player.AddGil(250);Quest.Complete();Player.SetQuestMustZone(Windurst,MAKING_THE_GRADE)" +
              "|Trade.Confirm();Player.AddGil(250);Quest.Complete()",
              $"{W10Finish(tp, 440, 0, true)}|{W10Finish(tp, 440, 0, false)}");

        var fc = new QC.Windurst.FlowerChild().Compile();
        Check("windurst g10 completions:Flower Child's 10000 option 3002 takes the trade, adds 4 to the flag (a sum, not a bit OR: 4 + 4 = 8) and says MOGHOUSE_EXIT only when the completion succeeds",
              "Quest.Complete();Trade.Confirm();Player.SetMoghouseFlag(8);Text.Special(239:MOGHOUSE_EXIT)" +
              "|Quest.Complete()",
              $"{W10Finish(fc, 10000, 3002, true, f => f.MhFlag = 4)}|{W10Finish(fc, 10000, 3002, false, f => f.MhFlag = 4)}");

        var stew = new QC.Windurst.InAStew().Compile();
        Check("windurst g10 completions:In a Stew's 239 sets the mustZone, takes the stew and sets Wait only when the completion succeeds",
              $"Quest.Complete();Quest.SetMustZone();Player.DelKeyItem({QC.KeyItem.RanpiMonpisSpecialStew});Quest.SetVar(Wait,1800198000,0)" +
              "|Quest.Complete()",
              $"{W10Finish(stew, 239, 0, true, f => f.Tally = 1800198000)}|{W10Finish(stew, 239, 0, false, f => f.Tally = 1800198000)}");
    }
}
