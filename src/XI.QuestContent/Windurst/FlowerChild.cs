// Flower Child (Log ID 2, Quest ID 65), a line-for-line port of scripts/quests/windurst/Flower_Child.lua (quest rework
// B3b). Ojha Rhawash : !pos -209 0 -134 239. Not completed (available or accepted): Ojha Rhawash's 10000 (0, 239, 10)
// on a talk; on a trade 10000 with 2 (a Lilac alone), 1 (one of the 21 flowers alone) or 0 (anything else). Its finish:
// option 3002 (the Lilac) completes the quest (fame 0) and then takes the trade, adds the Windurst Mog House exit
// (flag 0x0004) and says MOGHOUSE_EXIT; option 1 takes the trade; every option but 3002 begins the quest when it is
// available. Completed: 10000 with 4 (the Lilac), 5 (a flower) or 0 (anything else) and 10 on a talk; no finish
// handler (the trade is never taken).
// Kept as the Lua file has it: the finish acts on the client's option, not on what was traded (option 3002 after a
// talk completes the quest with no Lilac; option 1 after a talk confirms nothing); any finish but 3002, a talk's
// included, begins the quest.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class FlowerChild : QuestScript
    {
        public FlowerChild() : base(QuestLog.Windurst, "FLOWER_CHILD") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Flower_Child";

        static readonly int[] FlowerItems =
        {
            Item.Amaryllis,
            Item.Asphodel,
            Item.Carnation,
            Item.Casablanca,
            Item.Cattleya,
            Item.Chamomile,
            Item.Dahlia,
            Item.FlaxFlower,
            Item.Lilac,
            Item.LycopodiumFlower,
            Item.Marguerite,
            Item.OliveFlower,
            Item.PapakaGrass,
            Item.Phalaenopsis,
            Item.RainLily,
            Item.RedRose,
            Item.SnowLily,
            Item.SweetWilliam,
            Item.TahrongiCactus,
            Item.WaterLily,
            Item.Wijnruit,
        };

        // isTradeInTable: npcUtil.tradeHasExactly(trade, itemId) for each item in the table's order, the first true one
        // confirmed
        static bool IsTradeInTable(IQuestTrade trade, int[] itemTable)
        {
            foreach (int itemId in itemTable)
            {
                if (trade.HasExactly(itemId))
                {
                    return true;
                }
            }

            return false;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 0, FameArea = FameArea.Windurst });

            // Lua: status ~= xi.questStatus.QUEST_COMPLETED
            b.State(QuestStatus.Available)
                .Or(QuestStatus.Accepted)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.OjhaRhawash)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.Lilac))
                        {
                            return QuestReply.ProgressEvent(10000, 0, 239, 2);
                        }
                        else if (IsTradeInTable(q.Trade, FlowerItems))
                        {
                            return QuestReply.ProgressEvent(10000, 0, 239, 1);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(10000, 0, 239, 0);
                        }
                    })
                    .OnTrigger(QuestReply.ProgressEvent(10000, 0, 239, 10))
                .OnEventFinish(Zone.WindurstWalls, 10000, q =>
                {
                    // Three possible event options are returned from the above triggers:
                    // 0    : Invalid Item was traded
                    // 1    : Valid Item was traded
                    // 10   : Cancel/Exit event option.
                    // 3002 : Lilac was traded

                    // Correct trade option (Lilac).
                    if (q.Option == 3002)
                    {
                        if (q.Quest.Complete())
                        {
                            q.Trade.Confirm();
                            int mhflag = q.Player.MoghouseFlag;
                            q.Player.SetMoghouseFlag(mhflag + 0x0004);
                            q.Text.Special(Text.WindurstWalls.MOGHOUSE_EXIT);
                        }
                    }
                    else
                    {
                        // Confirm trade if there was a trade.
                        if (q.Option == 1)
                        {
                            q.Trade.Confirm();
                        }

                        // Start quest if it wasn't.
                        if (q.Player.QuestStatus(QuestLog.Windurst, "FLOWER_CHILD") == QuestStatus.Available)
                        {
                            q.Quest.Begin();
                        }
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.OjhaRhawash)
                    .OnTrade(q =>
                    {
                        // NOTE: After completing this quest, trade is not consumed.

                        if (q.Trade.HasExactly(Item.Lilac))
                        {
                            return QuestReply.ProgressEvent(10000, 0, 239, 4);
                        }
                        else if (IsTradeInTable(q.Trade, FlowerItems))
                        {
                            return QuestReply.ProgressEvent(10000, 0, 239, 5);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(10000, 0, 239, 0);
                        }
                    })
                    .OnTrigger(QuestReply.ProgressEvent(10000, 0, 239, 10));
        }
    }
}
