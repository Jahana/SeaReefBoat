// XI.Verify.Quests -- quest rework B3b Aht Urhgan group 1 (An Empty Vessel, Beginnings, Omens, Transformations, The
// Beast Within, with the era modules beginnings, omens and transformations): what no trace can see.
// - An Empty Vessel's Aydeewa 3 (option 13) tests quest:complete, but the reward is a title only, so the completion
//   never fails in the game: its false arm (no job, no key items, no completeEvent; the setPos still runs) is shown here
//   on the fake.
// - The reply kinds no flow can show (the mutants' structural survivors; every other kind is pinned by a trace, the
//   #kinds flows among them). A kind only ranks the lookup's actions for the same NPC talk, trade or trigger area:
//   * a trade or talk whose NPC has no script of its own (no fallback: the lookup runs the action whatever its rank) and
//     no other line of that interaction, the rest of its rivals being the zone's DefaultActions (below Event): Waoud's
//     trade (An Empty Vessel's 67; no other quest or mission trades there), blank_omens (Omens' 9), blank_transformations
//     (Transformations' 4 and 5), Imperial Whitegate's trade (The Beast Within's 762 and 763; the ToAU / RoV missions,
//     Divine Interference, Waking the Colossus and Trust: Nashmeira only talk to it);
//   * a trigger area: interaction_lookup.lua's onHandler runs the lookup's next action and the zone's own
//     onTriggerAreaEnter together whatever the rank (the alternation is for onTrigger / onTrade only), so the kind only
//     ranks the area's own actions, and these areas have one: Aydeewa's area 1 (An Empty Vessel's 3), Alzadaal's areas
//     24 and 25 (Transformations' 2 and 3; Alzadaal's other quest and mission areas are 3, 13 and 23).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunAhtUrhganGroup1()
    {
        Console.WriteLine("== quest rework B3b Aht Urhgan group 1: An Empty Vessel's completion false arm; the reply kinds no trace sees ==");
        var aev = new QC.AhtUrhgan.AnEmptyVessel().Compile();
        Check("ahturhgan g1:An Empty Vessel clean", "", string.Join(" | ", QuestScriptValidator.Validate(aev)));
        Check("ahturhgan g1:An Empty Vessel's Aydeewa 3 (option 13) unlocks BLU, adds the two key items and sets completeEvent only when the completion succeeds; the setPos runs either way",
              $"Quest.Complete();Player.UnlockJob({QC.Job.Blu});Player.AddKeyItem({QC.KeyItem.MarkOfZahak});" +
              $"Player.AddKeyItem({QC.KeyItem.JobGestureBlueMage});Quest.SetVar(completeEvent,1,0);" +
              $"Player.SetPos(148,-2,0,128,{QC.Zone.AhtUrhganWhitegate})" +
              $"|Quest.Complete();Player.SetPos(148,-2,0,128,{QC.Zone.AhtUrhganWhitegate})",
              $"{AU1Finish(aev, 3, 13, true)}|{AU1Finish(aev, 3, 13, false)}");

        var omens = new QC.AhtUrhgan.Omens().Compile();
        var trans = new QC.AhtUrhgan.Transformations().Compile();
        var tbw = new QC.AhtUrhgan.TheBeastWithin().Compile();
        Fake Acc(int prog, int option = 0)
        {
            var f = new Fake { Status = QuestStatus.Accepted, Step = prog };
            f.Vars["Option"] = option;
            return f;
        }
        var tear = Acc(1, 1);
        tear.TradeResult = true;                                       // the Option item alone
        var area1 = Acc(2, 1);
        area1.Items.Add(QC.Item.SirensTear);
        var testimony2 = Acc(2);
        testimony2.TradeItems[QC.Item.BlueMagesTestimony] = 1;
        var testimony0 = Acc(0);
        testimony0.TradeItems[QC.Item.BlueMagesTestimony] = 1;
        Check("ahturhgan g1:the reply kinds no flow can show are Progress, as the Lua's progressEvent: Waoud's trade 67, Aydeewa area 1's 3, blank_omens' 9, blank_transformations' 4 and 5, Alzadaal areas 24's 2 and 25's 3, Imperial Whitegate's trade 763 and 762",
              $"ProgressEvent(67,{{[6]=1}})|ProgressEvent(3,{QC.Item.SirensTear})|ProgressEvent(9)|ProgressEvent(4)|ProgressEvent(5)|" +
              "ProgressEvent(2)|ProgressEvent(3)|ProgressEvent(763)|ProgressEvent(762)",
              string.Join("|",
                  AU1Reply(aev, QuestHandlerKind.OnTrade, QC.Npc.AhtUrhganWhitegate.Waoud, tear),
                  AU1Area(aev, QC.Zone.AydeewaSubterrane, 1, area1),
                  AU1Reply(omens, QuestHandlerKind.OnTrigger, QC.Npc.AydeewaSubterrane.blankomens, Acc(3)),
                  AU1Reply(trans, QuestHandlerKind.OnTrigger, QC.Npc.AlzadaalUnderseaRuins.blanktransformations, Acc(3)),
                  AU1Reply(trans, QuestHandlerKind.OnTrigger, QC.Npc.AlzadaalUnderseaRuins.blanktransformations, Acc(4)),
                  AU1Area(trans, QC.Zone.AlzadaalUnderseaRuins, 24, Acc(1)),
                  AU1Area(trans, QC.Zone.AlzadaalUnderseaRuins, 25, Acc(2)),
                  AU1Reply(tbw, QuestHandlerKind.OnTrade, QC.Npc.AhtUrhganWhitegate.ImperialWhitegate, testimony2),
                  AU1Reply(tbw, QuestHandlerKind.OnTrade, QC.Npc.AhtUrhganWhitegate.ImperialWhitegate, testimony0)));
    }

    // the onEventFinish of csid, wherever its state is, run on a fake whose quest:complete returns completes
    static string AU1Finish(QuestScriptDefinition def, int csid, int option, bool completes)
    {
        var h = def.States.SelectMany(s => s.Handlers).Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Id == csid);
        var f = new Fake { Status = QuestStatus.Accepted, Csid = csid, Option = option, CompleteResult = completes };
        h.Run(f);
        return string.Join(";", f.Calls);
    }

    // the reply of the quest's only handler of that kind for the NPC, on the fake, as QuestContextFake.Describe writes it
    static string AU1Reply(QuestScriptDefinition def, QuestHandlerKind kind, string npc, Fake f) =>
        AU1Describe(def.States.SelectMany(s => s.Handlers).Single(x => x.Kind == kind && x.Entity == npc), f);

    // the reply of the quest's onTriggerAreaEnter for that zone's area
    static string AU1Area(QuestScriptDefinition def, ushort zone, int area, Fake f) =>
        AU1Describe(def.States.SelectMany(s => s.Handlers)
            .Single(x => x.Kind == QuestHandlerKind.OnTriggerAreaEnter && x.Zone == zone && x.Id == area), f);

    static string AU1Describe(QuestHandlerDecl h, Fake f)
    {
        var r = h.Fixed ?? h.Reply(f);
        return r == null ? "null" : QuestContextFake.Describe(r);
    }
}
