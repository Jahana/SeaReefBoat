// XI.Verify.Quests -- quest rework B3b Outlands group 10 (The Opo-Opo and I; call-only): what the golden traces cannot
// show.
// - The reply kinds. The quest is called from its ten NPCs' scripts (xi.cs.quest), and an NPC call renders an Event and
//   a ProgressEvent alike, as player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply): there is no lookup action, so
//   no priority to tell them apart, and every trace replays the same with either kind. The Lua scripts call
//   player:startEvent, so each reply is pinned here as Event.
// - The answers that leave the rest to the script: a trade or a talk none of the quest's arms takes answers nothing
//   (null). The ten patched scripts' onTrade and onTrigger have nothing after the call and ignore its answer (the
//   lookup's default plays when nothing starts either way), so NoAction would look the same in a trace.
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunOutlandsGroup10()
    {
        Console.WriteLine("== quest rework B3b Outlands group 10: The Opo-Opo and I's call-mode reply kinds and nulls ==");
        var def = new QC.Outlands.TheOpoOpoAndI().Compile();
        // the handler index k: the NPC, its item, good / bad trade event, retry / ask / thanks talk event
        var h = new (string Npc, int Item, int Good, int Bad, int Retry, int Ask, int Happy)[]
        {
            (QC.Npc.Kazham.Lulupp, QC.Item.BrokenMithranFishingRod, 219, 229, 239, 207, 242),
            (QC.Npc.Kazham.Kukupp, QC.Item.Workbench, 220, 230, 198, 208, 243),
            (QC.Npc.Kazham.Mumupp, QC.Item.TenOfCoinsCard, 221, 231, 199, 209, 244),
            (QC.Npc.Kazham.Roropp, QC.Item.HandfulOfTheSandsOfSilence, 222, 232, 200, 210, 245),
            (QC.Npc.Kazham.Popopp, QC.Item.WanderingBulb, 223, 233, 201, 210, 246),
            (QC.Npc.Kazham.Bubupp, QC.Item.SetOfGiantFishBones, 224, 234, 202, 211, 247),
            (QC.Npc.Kazham.Tatapp, QC.Item.BlackenedToad, 225, 235, 203, 212, 248),
            (QC.Npc.Kazham.Kakapp, QC.Item.WyvernSkull, 226, 236, 204, 213, 249),
            (QC.Npc.Kazham.Lalapp, QC.Item.RockOfAncientSalt, 227, 237, 205, 214, 250),
            (QC.Npc.Kazham.Nenepp, QC.Item.LuckyEgg, 241, 238, 206, 212, 250),
        };
        const int accepted = 3;
        Fake Acc(int progress, int failed = 0, int retry = 0, int item = 0)
        {
            var f = new Fake { Status = QuestStatus.Accepted };
            f.CharVars["OPO_OPO_PROGRESS"] = progress;
            f.CharVars["OPO_OPO_FAILED"] = failed;
            f.CharVars["OPO_OPO_RETRY"] = retry;
            if (item != 0) f.TradeItems[item] = 1;
            return f;
        }
        var scent = new Fake { Status = QuestStatus.Available };
        scent.CharVars["BathedInScent"] = 1;

        // the replies, each as Describe writes it; every one is an Event
        var got = new List<string>
        {
            OLReply(OLHandler(def, 0, QuestHandlerKind.OnTrigger, entity: h[0].Npc), scent),
            OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrigger, entity: h[8].Npc), new Fake { Status = QuestStatus.Available }),
            OLReply(OLHandler(def, 2, QuestHandlerKind.OnTrigger, entity: h[8].Npc), new Fake { Status = QuestStatus.Completed }),
            OLReply(OLHandler(def, accepted, QuestHandlerKind.OnTrigger, entity: h[0].Npc), Acc(0, retry: 1)),
            OLReply(OLHandler(def, accepted, QuestHandlerKind.OnTrigger, entity: h[0].Npc), Acc(0, retry: 5)),
            // the Lua's thanks by failed >= 2 alone: progress below 0 (no script writes one, so no trace reaches it)
            OLReply(OLHandler(def, accepted, QuestHandlerKind.OnTrigger, entity: h[0].Npc), Acc(-1, failed: 2)),
        };
        var want = new List<string> { "Event(217,0,483)", "Event(205)", "Event(205)", "Event(239)", "Event(239,0,0,4)", "Event(242)" };
        for (int k = 0; k < h.Length; k++)
        {
            var trade = OLHandler(def, accepted, QuestHandlerKind.OnTrade, entity: h[k].Npc);
            var talk = OLHandler(def, accepted, QuestHandlerKind.OnTrigger, entity: h[k].Npc);
            int other = h[(k + 1) % h.Length].Item;
            got.Add(OLReply(trade, Acc(k, item: h[k].Item)));
            got.Add(OLReply(trade, Acc(k, item: other)));
            if (k > 0) got.Add(OLReply(talk, Acc(k, retry: 1)));
            got.Add(OLReply(talk, Acc(k)));
            got.Add(OLReply(talk, Acc(k + 1)));
            want.Add($"Event({h[k].Good})");
            want.Add($"Event({h[k].Bad})");
            if (k > 0) want.Add($"Event({h[k].Retry})");
            want.Add($"Event({h[k].Ask})");
            want.Add($"Event({h[k].Happy})");
        }
        Console.WriteLine($"  replies: {string.Join(" ", got)}");
        Check("outlands g10:The Opo-Opo and I's replies are events (call mode): 217, Lalapp's 205 (available, completed), " +
              "239 (retry 1, retry 5), Lulupp's 242 by failed 2 with progress below 0, each handler's good / bad trade and retry / ask / thanks talk", string.Join("|", want), string.Join("|", got));

        // the nulls: a trade none of the arms takes (another's turn, neither item), a talk none of the arms takes
        var nulls = new List<string>();
        for (int k = 0; k < h.Length; k++)
        {
            var trade = OLHandler(def, accepted, QuestHandlerKind.OnTrade, entity: h[k].Npc);
            int turnOff = k == 0 ? 1 : 0;
            nulls.Add(OLReply(trade, Acc(k, item: QC.Item.BoneChip)));
            nulls.Add(OLReply(trade, Acc(turnOff, item: h[k].Item)));
            if (k > 0) nulls.Add(OLReply(OLHandler(def, accepted, QuestHandlerKind.OnTrigger, entity: h[k].Npc), Acc(0)));
        }
        // Lulupp's nil arm (progress and failed below 0: no script writes one) answers nothing as well
        nulls.Add(OLReply(OLHandler(def, accepted, QuestHandlerKind.OnTrigger, entity: h[0].Npc), Acc(-1, failed: -1)));
        Console.WriteLine($"  nulls: {string.Join(" ", nulls)}");
        Check("outlands g10:The Opo-Opo and I answers nothing (null: the script ends) for a trade no arm takes and a talk no arm takes",
              string.Join("|", Enumerable.Repeat("null", nulls.Count)), string.Join("|", nulls));
    }
}
