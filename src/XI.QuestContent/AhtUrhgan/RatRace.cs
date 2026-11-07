// Rat Race (Log ID 6, Quest ID 66), a line-for-line port of scripts/quests/ahtUrhgan/Rat_Race.lua (quest rework B3b Aht
// Urhgan, draft). Nashmau (no scripts): Kakkaroon !pos 13.2 0 -25.3 (no default), Ququroon !pos -2.4 0 66.8 (default
// 241), Kyokyoroon !pos 18 -6 10.5 (default 263). Aht Urhgan Whitegate (no scripts): Nadee Periyaha !pos -10.8 0 -1.2
// (default 90), Cacaroon !pos -72 0 -82.3 (default 248). Available (no other condition): Kakkaroon 308, whose finish
// begins it at Prog 1. Accepted: Nadee Periyaha 849 at Prog 1 (Prog 2), 851 at Prog 2, 852 at Prog 3 or more (Events);
// Cacaroon 853 at Prog 2, 854 at Prog 3 or more (Events), and at Prog 2 a trade holding an Imperial Bronze Piece: 850
// (the trade confirmed, Prog 3); Ququroon 309 at Prog 3 (Prog 4), 242 at Prog 4, 315 at Prog 5 or more (Events), and at
// Prog 4 a trade of exactly the five fish: 310 (a Bowl of Nashmau Stew; when it fits, the trade confirmed and Prog 5);
// Kyokyoroon at Prog 5, a trade of exactly the stew: 311 (the trade confirmed, Prog 6), 316 (an Event) at Prog 6;
// Kakkaroon 312 at Prog 6 (completed: two Imperial Gold Pieces, two Imperial Mythril Pieces, three Imperial Silver
// Pieces), 313 (an Event) otherwise. Completed: Kakkaroon 314, Kyokyoroon 317 (Events). The reward is a list of stacks
// ({ { IMPERIAL_GOLD_PIECE, 2 }, { IMPERIAL_MYTHRIL_PIECE, 2 }, { IMPERIAL_SILVER_PIECE, 3 } }), which QuestReward
// cannot hold: 312 gives the stacks first, as npcUtil.completeQuest does, and completes only when they fit (Delivering
// the Goods' way). Kept as the Lua has it: 308 begins it on any option; Cacaroon's trade wants the bronze piece among
// anything (tradeHas), and 850 confirms the whole trade; the NPCs return nil outside their Progs; Kakkaroon answers 313
// at every Prog but 6.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class RatRace : QuestScript
    {
        public RatRace() : base(QuestLog.AhtUrghan, "RAT_RACE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Rat_Race";

        protected override void Build(QuestScriptBuilder b)
        {
            // the Lua's reward is item = { { GOLD, 2 }, { MYTHRIL, 2 }, { SILVER, 3 } }: given in 312 (see the header)
            b.Steps(1, 2, 3, 4, 5, 6);

            // Section: Quest available
            b.State(QuestStatus.Available)
                .Npc(Zone.Nashmau, Npc.Nashmau.Kakkaroon).OnTrigger(QuestReply.ProgressEvent(308))
                .OnEventFinish(Zone.Nashmau, 308, q =>
                {
                    q.Quest.Begin();
                    q.Quest.SetStep(1);
                });

            // Section: Quest accepted
            b.State(QuestStatus.Accepted)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NadeePeriyaha).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(849);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.Event(851);
                    }
                    else if (questProgress >= 3)
                    {
                        return QuestReply.Event(852);
                    }
                    return null;
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Cacaroon).OnTrade(q =>
                {
                    if (q.Quest.Step == 2 && q.Trade.Has(Item.ImperialBronzePiece))
                    {
                        return QuestReply.ProgressEvent(850);
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 2)
                    {
                        return QuestReply.Event(853);
                    }
                    else if (questProgress >= 3)
                    {
                        return QuestReply.Event(854);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 849, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 850, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.SetStep(3);
                })
                .Npc(Zone.Nashmau, Npc.Nashmau.Kakkaroon).OnTrigger(q =>
                {
                    if (q.Quest.Step == 6)
                    {
                        return QuestReply.ProgressEvent(312);
                    }
                    else
                    {
                        return QuestReply.Event(313);
                    }
                })
                .Npc(Zone.Nashmau, Npc.Nashmau.Kyokyoroon).OnTrade(q =>
                {
                    if (q.Quest.Step == 5 && q.Trade.HasExactly(Item.BowlOfNashmauStew))
                    {
                        return QuestReply.ProgressEvent(311);
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 6)
                    {
                        return QuestReply.Event(316);
                    }
                    return null;
                })
                .Npc(Zone.Nashmau, Npc.Nashmau.Ququroon).OnTrade(q =>
                {
                    if (q.Quest.Step == 4 &&
                        q.Trade.HasExactly(Item.Ahtapot, Item.Istakoz, Item.Istavrit1,
                            Item.Istiridye, Item.Mercanbaligi))
                    {
                        return QuestReply.ProgressEvent(310);
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 3)
                    {
                        return QuestReply.ProgressEvent(309);
                    }
                    else if (questProgress == 4)
                    {
                        return QuestReply.Event(242);
                    }
                    else if (questProgress >= 5)
                    {
                        return QuestReply.Event(315);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Nashmau, 309, q =>
                {
                    q.Quest.SetStep(4);
                })
                .OnEventFinish(Zone.Nashmau, 310, q =>
                {
                    if (q.Player.GiveItem(Item.BowlOfNashmauStew))
                    {
                        q.Trade.Confirm();
                        q.Quest.SetStep(5);
                    }
                })
                .OnEventFinish(Zone.Nashmau, 311, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.SetStep(6);
                })
                .OnEventFinish(Zone.Nashmau, 312, q =>
                {
                    // npcUtil.completeQuest gives reward.item first and stops when it does not fit
                    if (q.Player.GiveItems(
                        (Item.ImperialGoldPiece, 2), (Item.ImperialMythrilPiece, 2), (Item.ImperialSilverPiece, 3)))
                    {
                        q.Quest.Complete();
                    }
                });

            // Section: Quest completed
            b.State(QuestStatus.Completed)
                .Npc(Zone.Nashmau, Npc.Nashmau.Kakkaroon).OnTrigger(QuestReply.Event(314))
                .Npc(Zone.Nashmau, Npc.Nashmau.Kyokyoroon).OnTrigger(QuestReply.Event(317));
        }
    }
}
