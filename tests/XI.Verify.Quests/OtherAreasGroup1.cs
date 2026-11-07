// XI.Verify.Quests -- quest rework B3b Other Areas group 1 (Rycharde's cooking chain: Rycharde the Chef, Way of the
// Cook, Unending Chase, His Name is Valgeir, Expertise, The Clue, The Basics): the facet members the group added
// (VanadielTime, ZPos), on the fake; and the trade replies' kind where no trace can see it. No other quest answers
// Rycharde's or Valgeir's trade with the same items (Rycharde the Chef's 73 and Way of the Cook's 73 share their id; no
// trade of Rycharde the Chef's 74, Unending Chase's 83, The Clue's 92 / 93, The Basics' 96 or Expertise's 103 has a
// competitor), and neither NPC has a script, so an Event and a Progress reply send the same packets there. Way of the
// Cook's 80 / 81 and Rycharde the Chef's 73 are told apart by a trace (way_of_the_cook#trade_kinds).
using System;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunOtherAreasGroup1()
    {
        Console.WriteLine("== quest rework B3b Other Areas group 1: facets, trade replies ==");
        RunOtherAreasGroup1Facets();
        RunOtherAreasGroup1Trades();
    }

    // VanadielTime(), player:getZPos()
    static void RunOtherAreasGroup1Facets()
    {
        var f = new Fake { VanaTimeNow = 790189200, ZPos = 38.6 };
        Check("oa g1 facets:the fake answers VanadielTime (VanaTimeNow) and ZPos",
              "790189200|38.6", $"{f.Player.VanadielTime}|{f.Player.ZPos.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
    }

    // a state's trade reply for the items in the trade
    static string OA1Trade(QuestScriptDefinition def, int state, string npc, Fake f) =>
        G2Reply(G2Handler(def, state, QuestHandlerKind.OnTrade, npc), f);

    static Fake OA1Accepted(int step, params (int Item, int Qty)[] items)
    {
        var f = new Fake { Status = QuestStatus.Accepted, Step = step };
        foreach (var (item, qty) in items) f.TradeItems[item] = qty;
        return f;
    }

    // Rycharde the Chef's 74 (two meat alone), Way of the Cook's 73 (the meat alone, the chip alone), Unending Chase's 83
    // (a Puffball alone), The Clue's 92 (four eggs alone) and 93 (three eggs alone), The Basics' 96 (the popoto alone at
    // Prog 1), Expertise's 103 (the fungus and the crab meat alone)
    static void RunOtherAreasGroup1Trades()
    {
        var rtc = new QC.OtherAreas.RychardeTheChef().Compile();
        var wotc = new QC.OtherAreas.WayOfTheCook().Compile();
        var uc = new QC.OtherAreas.UnendingChase().Compile();
        var ex = new QC.OtherAreas.Expertise().Compile();
        var clue = new QC.OtherAreas.TheClue().Compile();
        var basics = new QC.OtherAreas.TheBasics().Compile();
        string r = QC.Npc.Mhaura.Rycharde, v = QC.Npc.Selbina.Valgeir;
        Check("oa g1 trades:74, 83, 92, 96 and 103 are Progress replies; Way of the Cook's 73 and The Clue's 93 are Event replies",
              "ProgressEvent(74)|Event(73)|Event(73)|ProgressEvent(83)|ProgressEvent(92)|Event(93)|ProgressEvent(96)|ProgressEvent(103)",
              $"{OA1Trade(rtc, 1, r, OA1Accepted(0, (QC.Item.SliceOfDhalmelMeat, 2)))}|" +
              $"{OA1Trade(wotc, 1, r, OA1Accepted(0, (QC.Item.SliceOfDhalmelMeat, 1)))}|" +
              $"{OA1Trade(wotc, 1, r, OA1Accepted(0, (QC.Item.BeehiveChip, 1)))}|" +
              $"{OA1Trade(uc, 1, r, OA1Accepted(1, (QC.Item.Puffball, 1)))}|" +
              $"{OA1Trade(clue, 1, r, OA1Accepted(1, (QC.Item.CrawlerEgg, 4)))}|" +
              $"{OA1Trade(clue, 1, r, OA1Accepted(1, (QC.Item.CrawlerEgg, 3)))}|" +
              $"{OA1Trade(basics, 1, r, OA1Accepted(1, (QC.Item.BakedPopoto, 1)))}|" +
              $"{OA1Trade(ex, 1, v, OA1Accepted(1, (QC.Item.ScreamFungus, 1), (QC.Item.SliceOfLandCrabMeat, 1)))}");
    }
}
