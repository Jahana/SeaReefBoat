// XI.Verify.Quests -- quest rework B3b Windurst group 11 (Making Amends, Orastery Woes, One Good Deed?, All at Sea, A
// Greeting Cardian): what no trace can see. Making Amends' 277 and Orastery Woes' 583 test quest:complete, but their
// rewards hold no item (fame and a title; fame), so completeQuest never fails in the game: the flows finish each with
// room in the bag and the quest completes. The false arm (nothing after the completion) is shown here, on the fake, with
// the completion made to fail. All at Sea's 295 and A Greeting Cardian's 303 (item rewards) take their false arms in the
// flows (a full bag); One Good Deed?'s 597 ignores the completion's result (no arm).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup11()
    {
        Console.WriteLine("== quest rework B3b Windurst group 11: the completions' false arms (rewards with no item) ==");
        RunWindurstGroup11Completions();
    }

    // the onEventFinish of csid, wherever its state is, run on a fake whose quest:complete returns completes
    static string W11Finish(QuestScriptDefinition def, int csid, int option, bool completes)
    {
        var h = def.States.SelectMany(s => s.Handlers).Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Id == csid);
        var f = new Fake { Status = QuestStatus.Accepted, Csid = csid, Option = option, CompleteResult = completes };
        h.Run(f);
        return string.Join(";", f.Calls);
    }

    static void RunWindurstGroup11Completions()
    {
        var ma = new QC.Windurst.MakingAmends().Compile();
        Check("windurst g11 completions:Making Amends' 277 takes the glue, gives 1500 gil and sets the mustZone only when the completion succeeds",
              "Quest.Complete();Trade.Confirm();Player.AddGil(1500);Quest.SetMustZone()" +
              "|Quest.Complete()",
              $"{W11Finish(ma, 277, 0, true)}|{W11Finish(ma, 277, 0, false)}");

        var ow = new QC.Windurst.OrasteryWoes().Compile();
        Check("windurst g11 completions:Orastery Woes' 583 takes the three key items, teaches Black Halo and says BLACK_HALO_LEARNED only when the completion succeeds",
              $"Quest.Complete();Player.DelKeyItem({QC.KeyItem.MapToTheAnnalsOfTruth});Player.DelKeyItem({QC.KeyItem.AnnalsOfTruth});" +
              $"Player.DelKeyItem({QC.KeyItem.WeaponTrainingGuide});Player.AddLearnedWeaponskill(11);Text.Special(240:BLACK_HALO_LEARNED)" +
              "|Quest.Complete()",
              $"{W11Finish(ow, 583, 0, true)}|{W11Finish(ow, 583, 0, false)}");
    }
}
