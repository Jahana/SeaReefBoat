// XI.Verify.Quests -- quest rework B3b Windurst group 6 (Truth, Justice, and the Onion Way!, Know One's Onions,
// Inspector's Gadget!, Onion Rings): the facet member the group added (VanadielTime), on the fake; and what no trace can
// see: the trade replies' kind (no other quest answers Kohlo-Lakolo's or Chamama's trade with the same items,
// Kohlo-Lakolo has no script, and Chamama's script does nothing with Saruta cotton, so an Event and a Progress reply
// send the same packets there), and the completions' player local vars ('[2][40]mustZone', '[2][41]mustZone',
// '[2][42]mustZone', '[2][76]mustZone': a local var is not in the database, and nothing reads these), set only when
// the completion succeeds.
using System;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup6()
    {
        Console.WriteLine("== quest rework B3b Windurst group 6: facets, trade replies, completion local vars ==");
        RunWindurstGroup6Facets();
        RunWindurstGroup6Trades();
        RunWindurstGroup6LocalVars();
    }

    // VanadielTime()
    static void RunWindurstGroup6Facets()
    {
        var f = new Fake { VanaTimeNow = 790189200 };
        Check("windurst g6 facets:the fake answers VanadielTime (VanaTimeNow)", "790189200", $"{f.Player.VanadielTime}");
    }

    // a state's trade reply for the items in the trade
    static string W6Trade(QuestScriptDefinition def, int state, string npc, Fake f) =>
        G2Reply(G2Handler(def, state, QuestHandlerKind.OnTrade, npc), f);

    // Truth, Justice's 378 (the tail alone), Know One's Onions' 398 (in time) and 390 (late) (four onions alone),
    // Inspector's Gadget's 552 (four balls of Saruta cotton at Prog 2, no moustache): each a Progress reply
    static void RunWindurstGroup6Trades()
    {
        var tj = new QC.Windurst.TruthJusticeAndTheOnionWay().Compile();
        var ko = new QC.Windurst.KnowOnesOnions().Compile();
        var ig = new QC.Windurst.InspectorsGadget().Compile();
        var tail = new Fake { Status = QuestStatus.Accepted };
        tail.TradeItems[QC.Item.RarabTail] = 1;
        var onions = new Fake { Status = QuestStatus.Accepted };
        onions.TradeItems[QC.Item.WildOnion] = 4;
        var cotton = new Fake { Status = QuestStatus.Accepted, Step = 2 };
        cotton.TradeItems[QC.Item.BallOfSarutaCotton] = 4;
        Check("windurst g6 trades:378, 398, 390 and 552 are Progress replies",
              "ProgressEvent(378,0,4444,13093)|ProgressEvent(398,0,4387)|ProgressEvent(390)|ProgressEvent(552)",
              $"{W6Trade(tj, 1, QC.Npc.PortWindurst.KohloLakolo, tail)}|{W6Trade(ko, 1, QC.Npc.PortWindurst.KohloLakolo, onions)}|" +
              $"{W6Trade(ko, 4, QC.Npc.PortWindurst.KohloLakolo, onions)}|{W6Trade(ig, 1, QC.Npc.WindurstWaters.Chamama, cotton)}");
    }

    // each completion's calls, with quest:complete true and false
    static string W6Finish(QuestScriptDefinition def, int state, int csid, bool completes)
    {
        var f = new Fake { Status = QuestStatus.Accepted, Csid = csid, CompleteResult = completes };
        G2Handler(def, state, QuestHandlerKind.OnEventFinish, null, csid).Run(f);
        return string.Join(";", f.Calls);
    }

    static void RunWindurstGroup6LocalVars()
    {
        var tj = new QC.Windurst.TruthJusticeAndTheOnionWay().Compile();
        var ko = new QC.Windurst.KnowOnesOnions().Compile();
        var ig = new QC.Windurst.InspectorsGadget().Compile();
        var rings = new QC.Windurst.OnionRings().Compile();
        Check("windurst g6 local vars:Truth, Justice's 378 sets [2][40]mustZone after the completion (not when it fails)",
              "Quest.Complete();Trade.Confirm();Player.SetLocalVar([2][40]mustZone,1)|Quest.Complete()",
              $"{W6Finish(tj, 1, 378, true)}|{W6Finish(tj, 1, 378, false)}");
        Check("windurst g6 local vars:Know One's Onions' 400, 386 and 390 set [2][41]mustZone after the completion (not when it fails)",
              "Quest.Complete();Player.SetLocalVar([2][41]mustZone,1)|Quest.Complete()|" +
              "Quest.Complete();Player.SetLocalVar([2][41]mustZone,1)|Quest.Complete()|" +
              "Quest.Complete();Trade.Confirm();Player.SetLocalVar([2][41]mustZone,1)|Quest.Complete()",
              $"{W6Finish(ko, 3, 400, true)}|{W6Finish(ko, 3, 400, false)}|{W6Finish(ko, 4, 386, true)}|{W6Finish(ko, 4, 386, false)}|" +
              $"{W6Finish(ko, 4, 390, true)}|{W6Finish(ko, 4, 390, false)}");
        Check("windurst g6 local vars:Inspector's Gadget's 421 sets [2][42]mustZone after the completion (not when it fails)",
              "Quest.Complete();Player.SetLocalVar([2][42]mustZone,1)|Quest.Complete()",
              $"{W6Finish(ig, 1, 421, true)}|{W6Finish(ig, 1, 421, false)}");
        Check("windurst g6 local vars:Onion Rings' 432, 433 and 289 take the Old Ring and set [2][76]mustZone after the completion",
              "Quest.Complete();Player.DelKeyItem(130);Player.SetLocalVar([2][76]mustZone,1)|" +
              "Quest.Complete();Player.DelKeyItem(130);Player.SetLocalVar([2][76]mustZone,1)|" +
              "Quest.Complete();Player.DelKeyItem(130);Player.SetLocalVar([2][76]mustZone,1)",
              $"{W6Finish(rings, 0, 432, true)}|{W6Finish(rings, 1, 433, true)}|{W6Finish(rings, 1, 289, true)}");
    }
}
