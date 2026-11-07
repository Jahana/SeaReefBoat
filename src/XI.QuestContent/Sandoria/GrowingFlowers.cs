// Growing Flowers (Log ID 0, Quest ID 58), a line-for-line port of scripts/quests/sandoria/Growing_Flowers.lua (quest
// rework B3b, San d'Oria group 9). Kuu Mohzolhi : !pos -123 0 80 231. Every talk and trade plays 605 (0, 231, n): n is 10
// for a talk, 2 for one Marguerite, 1 for one of the other flowers, 0 for anything else (4, 5, 0 once completed). The
// client answers 1002 when the Marguerite was taken: the quest completes (fame 0), the trade is taken and the Mog House
// exit to Northern San d'Oria is unlocked (flag + 0x0001, MOGHOUSE_EXIT). Any other answer takes the trade on option 1
// and begins the quest while it is available, so a plain talk begins it. Completed: no finish handler, and the trade
// is never taken. Outside reference: globals/roe_records.lua:301 (questComplete: a status read, it stays).
// The San d'Oria twin of Bastok's A Lady's Heart (src/XI.QuestContent/Bastok/ALadysHeart.cs).
// Kept as the Lua file has it: any finish of 605 but 1002 begins the quest while it is available (a cancel, a wrong
// item), and 1002 completes it from available too; the flag is the old flag + 1, not a bit set.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class GrowingFlowers : QuestScript
    {
        // the Lua's flowerItems (the Marguerite included), in its order
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

        public GrowingFlowers() : base(QuestLog.Sandoria, "GROWING_FLOWERS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Growing_Flowers";

        // isTradeInTable: npcUtil.tradeHasExactly(trade, itemId) for each, the first match wins
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
            b.Reward(new QuestReward { Fame = 0, FameArea = FameArea.Sandoria });

            // Lua: status ~= QUEST_COMPLETED
            b.State(QuestStatus.Available).Or(QuestStatus.Accepted)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.KuuMohzolhi)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.Marguerite))
                        {
                            return QuestReply.ProgressEvent(605, 0, 231, 2);
                        }
                        else if (IsTradeInTable(q.Trade, FlowerItems))
                        {
                            return QuestReply.ProgressEvent(605, 0, 231, 1);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(605, 0, 231, 0);
                        }
                    })
                    .OnTrigger(QuestReply.ProgressEvent(605, 0, 231, 10))
                // the options: 0 an invalid item was traded, 1 a valid one, 10 cancel / exit, 1002 the Marguerite
                .OnEventFinish(Zone.NorthernSanDoria, 605, q =>
                {
                    // Correct trade option (Marguerite).
                    if (q.Option == 1002)
                    {
                        if (q.Quest.Complete())
                        {
                            q.Trade.Confirm();
                            int mhflag = q.Player.MoghouseFlag;
                            q.Player.SetMoghouseFlag(mhflag + 0x0001);
                            q.Text.Special(Text.NorthernSanDoria.MOGHOUSE_EXIT);
                        }
                    }
                    else
                    {
                        // Confirm trade if there was a trade.
                        if (q.Option == 1)
                        {
                            q.Trade.Confirm();
                        }

                        // Start quest if it wasn't. player:getQuestStatus(quest.areaId, quest.questId): the live status
                        if (q.Player.QuestStatus(QuestLog.Sandoria, "GROWING_FLOWERS") == QuestStatus.Available)
                        {
                            q.Quest.Begin();
                        }
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.KuuMohzolhi)
                    .OnTrade(q =>
                    {
                        // NOTE: After completing this quest, trade is not consumed.
                        if (q.Trade.HasExactly(Item.Marguerite))
                        {
                            return QuestReply.ProgressEvent(605, 0, 231, 4);
                        }
                        else if (IsTradeInTable(q.Trade, FlowerItems))
                        {
                            return QuestReply.ProgressEvent(605, 0, 231, 5);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(605, 0, 231, 0);
                        }
                    })
                    .OnTrigger(QuestReply.ProgressEvent(605, 0, 231, 10));
        }
    }
}
