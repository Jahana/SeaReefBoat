// XI.Verify.Quests -- quest rework B3b Crystal War group 1 (the nation openers: Steamed Rams, The Fighting Fourth, Snake
// on the Plains): what no trace can see.
// - quest:complete's false arm. Each finish that completes tests quest:complete, but the reward is a title only, so the
//   completion never fails in the game: the false arm (no ribbon, no allegiance, no key items taken, no message; the
//   shoes are given before the test) is shown here on the fake.
// - Steamed Rams' 12 rereads the allegiance at its finish. 12 is offered only without an allegiance and no step can
//   change it inside the event, so the pledged side of the finish (no shoes, no ribbon) is shown here on the fake.
// - The new facet members' fake records (Player.CampaignAllegiance / SetCampaignAllegiance, GiveKeyItem with a text).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunCrystalWarGroup1()
    {
        Console.WriteLine("== quest rework B3b Crystal War group 1: the openers' completion false arms and 12's pledged finish ==");
        var sr = new QC.CrystalWar.SteamedRams().Compile();
        var tff = new QC.CrystalWar.TheFightingFourth().Compile();
        var snake = new QC.CrystalWar.SnakeOnThePlains().Compile();
        Check("crystalwar g1:Steamed Rams clean", "", string.Join(" | ", QuestScriptValidator.Validate(sr)));
        Check("crystalwar g1:The Fighting Fourth clean", "", string.Join(" | ", QuestScriptValidator.Validate(tff)));
        Check("crystalwar g1:Snake on the Plains clean", "", string.Join(" | ", QuestScriptValidator.Validate(snake)));

        string evidence = $"Player.DelKeyItem({QC.KeyItem.CharredPropeller});Player.DelKeyItem({QC.KeyItem.OxidizedPlate});" +
                          $"Player.DelKeyItem({QC.KeyItem.PieceOfShatteredLumber})";
        Check("crystalwar g1:Steamed Rams' 12 finish: no allegiance: the shoes, then the ribbon, allegiance 1, the evidence and " +
              "NOW_ALLIED_WITH only when the completion succeeds; with an allegiance at the finish: no shoes, no ribbon",
              $"Player.GiveItem({QC.Item.SprintersShoes});Quest.Complete();Player.GiveKeyItem({QC.KeyItem.BronzeRibbonOfService});" +
              $"Player.SetCampaignAllegiance(1);{evidence};Text.Special({QC.Zone.SouthernSanDoriaS}:NOW_ALLIED_WITH,1)" +
              $"|Player.GiveItem({QC.Item.SprintersShoes});Quest.Complete()" +
              $"|Quest.Complete();Player.SetCampaignAllegiance(1);{evidence};Text.Special({QC.Zone.SouthernSanDoriaS}:NOW_ALLIED_WITH,1)",
              $"{CW1Finish(sr, 12, 0, 0, true)}|{CW1Finish(sr, 12, 0, 0, false)}|{CW1Finish(sr, 12, 0, 3, true)}");
        Check("crystalwar g1:Steamed Rams' 624 finish sets allegiance 1, takes the evidence and says NOW_ALLIED_WITH only when the completion succeeds",
              $"Quest.Complete();Player.SetCampaignAllegiance(1);{evidence};Text.Special({QC.Zone.SouthernSanDoriaS}:NOW_ALLIED_WITH,1)" +
              "|Quest.Complete()",
              $"{CW1Finish(sr, 624, 0, 3, true)}|{CW1Finish(sr, 624, 0, 3, false)}");
        // 10 / 11 (pledged) on option 0 take a held Red Recommendation Letter (user ruling 2026-11-04, kept Lua bugs);
        // the flows reach 11's with the letter, not 10's
        string Pledged(int csid, bool letter)
        {
            var h = sr.States.SelectMany(s => s.Handlers)
                .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Id == csid);
            var lf = new Fake { Status = QuestStatus.Available, Csid = csid, Option = 0, Allegiance = 3 };
            lf.KeyItems[QC.KeyItem.RedRecommendationLetter] = letter;
            h.Run(lf);
            return string.Join(";", lf.Calls);
        }
        string takes = $"Quest.Begin();Player.DelKeyItem({QC.KeyItem.RedRecommendationLetter})";
        Check("crystalwar g1:Steamed Rams' 10 and 11 finishes take a held letter (user ruling 2026-11-04)",
              $"{takes}|Quest.Begin()|{takes}|Quest.Begin()",
              $"{Pledged(10, true)}|{Pledged(10, false)}|{Pledged(11, true)}|{Pledged(11, false)}");
        Check("crystalwar g1:The Fighting Fourth's 143 finish gives the ribbon, allegiance 2 and NOW_ALLIED_WITH only when the completion succeeds",
              $"Player.GiveItem({QC.Item.SprintersShoes});Quest.Complete();Player.GiveKeyItem({QC.KeyItem.BronzeRibbonOfService});" +
              $"Player.SetCampaignAllegiance(2);Text.Special({QC.Zone.BastokMarketsS}:NOW_ALLIED_WITH,2)" +
              $"|Player.GiveItem({QC.Item.SprintersShoes});Quest.Complete()" +
              "|Quest.Complete()",
              $"{CW1Finish(tff, 143, 0, 0, true)}|{CW1Finish(tff, 143, 0, 0, false)}|{CW1Finish(tff, 143, 0, 1, false)}");
        Check("crystalwar g1:Snake on the Plains' 106 finish (option 0) gives the ribbon, allegiance 3 and NOW_ALLIED_WITH only when the completion succeeds",
              $"Player.GiveItem({QC.Item.SprintersShoes});Quest.Complete();Player.GiveKeyItem({QC.KeyItem.BronzeRibbonOfService});" +
              $"Player.SetCampaignAllegiance(3);Text.Special({QC.Zone.WindurstWatersS}:NOW_ALLIED_WITH,3)" +
              $"|Player.GiveItem({QC.Item.SprintersShoes});Quest.Complete()" +
              "|Quest.Complete()",
              $"{CW1Finish(snake, 106, 0, 0, true)}|{CW1Finish(snake, 106, 0, 0, false)}|{CW1Finish(snake, 106, 0, 2, false)}");

        // the facet members' fake: the allegiance read back after SetCampaignAllegiance; GiveKeyItem with a text recorded
        // with it and the key item then held
        var f = new Fake();
        f.Player.SetCampaignAllegiance(2);
        bool given = f.Player.GiveKeyItem(QC.KeyItem.BlueRecommendationLetter, QC.Text.BastokMarketsS.ITEM_RETURNED_TO_CHAR);
        Check("crystalwar g1 facets:the fake answers CampaignAllegiance after SetCampaignAllegiance and records GiveKeyItem with its text",
              $"2/True/True/Player.SetCampaignAllegiance(2);Player.GiveKeyItem({QC.KeyItem.BlueRecommendationLetter},{QC.Zone.BastokMarketsS}:ITEM_RETURNED_TO_CHAR)",
              $"{f.Player.CampaignAllegiance}/{given}/{f.Player.HasKeyItem(QC.KeyItem.BlueRecommendationLetter)}/{string.Join(";", f.Calls)}");
    }

    // the onEventFinish of csid, wherever its state is, run on a fake with that option and allegiance whose quest:complete
    // returns completes
    static string CW1Finish(QuestScriptDefinition def, int csid, int option, int allegiance, bool completes)
    {
        var h = def.States.SelectMany(s => s.Handlers).Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Id == csid);
        var f = new Fake { Status = QuestStatus.Accepted, Csid = csid, Option = option, CompleteResult = completes, Allegiance = allegiance };
        h.Run(f);
        return string.Join(";", f.Calls);
    }
}
