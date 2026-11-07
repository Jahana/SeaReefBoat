// XI.Verify.Quests -- quest rework B3b Windurst group 7 (Crying Over Onions, Wild Card, The Promise): what no trace can
// see. The trade replies' kind: Honoi-Gomoi's script has no onTrade, so the lookup gives the quest every trade there and
// an Event 775 sends the same packets as a Progress one; Chamama's 799 plays once only (its finish gives the sticker, and
// the sticker closes the trade), so the trade turn its kind leaves is never read. Wild Card's completed 783 at Honoi-Gomoi
// is a replaceDefault line, which alternates with her script as an Event does, and no other quest or mission line can
// stand there with it (CoP 3-3's needs a mission status no flow can set). The completions' player local vars
// ('[2][77]mustZone', '[2][78]mustZone': a local var is not in the database, and nothing reads these), set only when
// the completion succeeds.
using System;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup7()
    {
        Console.WriteLine("== quest rework B3b Windurst group 7: trade replies, replaceDefault, completion local vars ==");
        RunWindurstGroup7Trades();
        RunWindurstGroup7Modifiers();
        RunWindurstGroup7LocalVars();
    }

    // an accepted quest's trade reply for one item alone, at a Prog and with vars
    static string W7Trade(QuestScriptDefinition def, string npc, int item, int step, string varName = null, int varValue = 0)
    {
        var f = new Fake { Status = QuestStatus.Accepted, Step = step };
        if (varName != null)
        {
            f.Vars[varName] = varValue;
        }

        f.TradeItems[item] = 1;
        return G2Reply(G2Handler(def, 1, QuestHandlerKind.OnTrade, npc), f);
    }

    // Crying Over Onions' 775 (the Star Spinel alone at Prog 1 and 2), The Promise's 799 (Shoalweed alone, Chamama 1, no
    // sticker): each a Progress reply
    static void RunWindurstGroup7Trades()
    {
        var co = new QC.Windurst.CryingOverOnions().Compile();
        var tp = new QC.Windurst.ThePromise().Compile();
        Check("windurst g7 trades:775 (Prog 1 and 2) and 799 are Progress replies",
              "ProgressEvent(775,0,1149)|ProgressEvent(775,0,1149)|ProgressEvent(799,0,0,271)",
              $"{W7Trade(co, QC.Npc.WindurstWaters.HonoiGomoi, QC.Item.StarSpinel, 1)}|" +
              $"{W7Trade(co, QC.Npc.WindurstWaters.HonoiGomoi, QC.Item.StarSpinel, 2)}|" +
              $"{W7Trade(tp, QC.Npc.WindurstWaters.Chamama, QC.Item.Shoalweed, 0, "Chamama", 1)}");
    }

    // Wild Card's completed section: Honoi-Gomoi's 783 replaces the default
    static void RunWindurstGroup7Modifiers()
    {
        var wc = new QC.Windurst.WildCard().Compile();
        Check("windurst g7 modifiers:Wild Card's completed 783 at Honoi-Gomoi is replaceDefault",
              "Event(783):replaceDefault()",
              G2Reply(G2Handler(wc, 2, QuestHandlerKind.OnTrigger, QC.Npc.WindurstWaters.HonoiGomoi), new Fake { Status = QuestStatus.Completed }));
    }

    // a completion's calls, with quest:complete true and false
    static string W7Finish(QuestScriptDefinition def, int csid, bool completes)
    {
        var f = new Fake { Status = QuestStatus.Accepted, Step = 4, Csid = csid, CompleteResult = completes };
        G2Handler(def, 1, QuestHandlerKind.OnEventFinish, null, csid).Run(f);
        return string.Join(";", f.Calls);
    }

    static void RunWindurstGroup7LocalVars()
    {
        var co = new QC.Windurst.CryingOverOnions().Compile();
        var wc = new QC.Windurst.WildCard().Compile();
        Check("windurst g7 local vars:Crying Over Onions' 776 sets [2][77]mustZone after the completion (not when it fails)",
              "Quest.Complete();Player.SetLocalVar([2][77]mustZone,1)|Quest.Complete()",
              $"{W7Finish(co, 776, true)}|{W7Finish(co, 776, false)}");
        Check("windurst g7 local vars:Wild Card's 782 sets [2][78]mustZone after the completion (not when it fails)",
              "Quest.Complete();Player.SetLocalVar([2][78]mustZone,1)|Quest.Complete()",
              $"{W7Finish(wc, 782, true)}|{W7Finish(wc, 782, false)}");
    }
}
