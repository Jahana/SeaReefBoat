// The Trader in the Forest (Log ID 0, Quest ID 7), a line-for-line port of scripts/quests/sandoria/The_Trader_in_the_Forest.lua
// (quest rework B3b, San d'Oria group 2). Abeaule : !pos -136 -2 56 231; Phairet : !pos -57 -2 -502 100. Available:
// Abeaule's 524 (Prog 0) or 592 (Prog 1): option 0 with room gives a Supplies Order and begins it (592 also clears
// Prog); 524's other side (another option, or no room) sets Prog 1. Accepted: Abeaule's 593 (option 1: another Supplies
// Order); exactly one Clump of Batagreens plays 525: completed (Robe, fame +20, Green Grocer), the trade confirmed.
// Accepted or completed, Phairet (West Ronfaure) takes exactly one Supplies Order for 124 (a Clump of Batagreens, the
// trade confirmed, Prog 2 while accepted); 50 gil buys a clump at once (no event); any other trade is refused with 125
// once completed or at Prog 2; his trigger plays 127 (the batagreens) on the same condition. The Medicine Woman reads
// this quest's status.
// Kept as the Lua has it: 524 with option 0 and a full inventory sets Prog 1 (the order not given, not begun).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class TheTraderInTheForest : QuestScript
    {
        public TheTraderInTheForest() : base(QuestLog.Sandoria, "THE_TRADER_IN_THE_FOREST") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/The_Trader_in_the_Forest";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Sandoria, Item = Item.Robe, Title = Title.GreenGrocer });

            b.State(QuestStatus.Available)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Abeaule).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(524);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(592);
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 524, q =>
                {
                    if (q.Option == 0 && q.Player.GiveItem(Item.SuppliesOrder))
                    {
                        q.Quest.Begin();
                    }
                    else
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 592, q =>
                {
                    if (q.Option == 0 && q.Player.GiveItem(Item.SuppliesOrder))
                    {
                        q.Quest.Begin();
                        q.Quest.SetStep(0);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Abeaule)
                    .OnTrade(q =>
                    {
                        // npcUtil.tradeHasExactly(trade, xi.item.CLUMP_OF_BATAGREENS)
                        if (q.Trade.HasExactly(Item.ClumpOfBatagreens))
                        {
                            return QuestReply.ProgressEvent(525);
                        }
                        return null;
                    })
                    .OnTrigger(q => QuestReply.ProgressEvent(593))
                .OnEventFinish(Zone.NorthernSanDoria, 525, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 593, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.GiveItem(Item.SuppliesOrder);
                    }
                });

            b.State(QuestStatus.Completed)
                .Or(QuestStatus.Accepted)
                .Npc(Zone.WestRonfaure, Npc.WestRonfaure.Phairet)
                    // Phairet takes Supplies Orders after the completion too (a batagreen bought, or an order picked up
                    // before completing)
                    .OnTrade(q =>
                    {
                        // npcUtil.tradeHasExactly(trade, xi.item.SUPPLIES_ORDER)
                        if (q.Trade.HasExactly(Item.SuppliesOrder))
                        {
                            return QuestReply.ProgressEvent(124);
                        }
                        // npcUtil.tradeHas(trade, { { 'gil', 50 } })
                        else if (q.Trade.HasWithGil(50) &&
                                 q.Player.GiveItem(Item.ClumpOfBatagreens))
                        {
                            q.Trade.Confirm();
                        }
                        // refused only after the order has been traded, or once completed
                        else if (q.Player.HasCompletedQuest(QuestLog.Sandoria, "THE_TRADER_IN_THE_FOREST") ||
                                 q.Quest.Step == 2)
                        {
                            return QuestReply.ProgressEvent(125);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasCompletedQuest(QuestLog.Sandoria, "THE_TRADER_IN_THE_FOREST") ||
                            q.Quest.Step == 2)
                        {
                            return QuestReply.ProgressEvent(127, Item.ClumpOfBatagreens);
                        }
                        return null;
                    })
                .OnEventFinish(Zone.WestRonfaure, 124, q =>
                {
                    if (q.Player.GiveItem(Item.ClumpOfBatagreens))
                    {
                        q.Trade.Confirm();

                        if (q.Quest.Status == QuestStatus.Accepted)
                        {
                            q.Quest.SetStep(2);
                        }
                    }
                });
        }
    }
}
