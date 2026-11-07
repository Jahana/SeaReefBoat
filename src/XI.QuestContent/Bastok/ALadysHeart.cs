// A Lady's Heart (Log ID 1, Quest ID 50), a line-for-line port of scripts/quests/bastok/A_Ladys_Heart.lua (quest
// rework B3b). Valah Molkot : !pos 59 8 -221 236. Every talk and trade plays 160 (0, 236, n): n is 10 for a talk, 2 for
// one Amaryllis, 1 for one of the other flowers, 0 for anything else (4, 5, 0 once completed). The client answers 2002
// when the Amaryllis was taken: the quest completes (fame 0), the trade is taken and the Mog House exit to Port Bastok
// is unlocked (flag 0x0002, MOGHOUSE_EXIT). Any other answer takes the trade on option 1 and begins the quest while it
// is available, so a plain talk begins it. Completed: no finish handler, and the trade is never taken.
// Kept as the Lua file has it: any finish of 160 but 2002 begins the quest while it is available (a cancel, a wrong
// item), and 2002 completes it from available too.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class ALadysHeart : QuestScript
    {
        // the Lua's flowerItems (the Amaryllis included), in its order
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

        public ALadysHeart() : base(QuestLog.Bastok, "A_LADYS_HEART") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/A_Ladys_Heart";

        // isTradeInTable: exactly one of any flower
        static bool IsTradeInTable(IQuestTrade trade, int[] itemTable)
        {
            foreach (int itemId in itemTable)
            {
                if (trade.Matches((itemId, 1)))
                {
                    return true;
                }
            }

            return false;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 0, FameArea = FameArea.Bastok });

            // Lua: status ~= QUEST_COMPLETED
            b.State(QuestStatus.Available).Or(QuestStatus.Accepted)
                .Npc(Zone.PortBastok, Npc.PortBastok.ValahMolkot)
                    .OnTrade(q =>
                    {
                        if (q.Trade.Matches((Item.Amaryllis, 1)))
                        {
                            return QuestReply.ProgressEvent(160, 0, 236, 2);
                        }
                        else if (IsTradeInTable(q.Trade, FlowerItems))
                        {
                            return QuestReply.ProgressEvent(160, 0, 236, 1);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(160, 0, 236, 0);
                        }
                    })
                    .OnTrigger(QuestReply.ProgressEvent(160, 0, 236, 10))
                // the options: 0 an invalid item was traded, 1 a valid one, 10 cancel / exit, 2002 the Amaryllis
                .OnEventFinish(Zone.PortBastok, 160, q =>
                {
                    if (q.Option == 2002)
                    {
                        if (q.Quest.Complete())
                        {
                            q.Trade.Complete();
                            int mhflag = q.Player.MoghouseFlag;
                            q.Player.SetMoghouseFlag(mhflag + 0x0002);
                            q.Text.Special(Text.PortBastok.MOGHOUSE_EXIT);
                        }
                    }
                    else
                    {
                        if (q.Option == 1)
                        {
                            q.Trade.Complete();
                        }

                        // player:getQuestStatus(quest.areaId, quest.questId): the live status, after the complete above
                        if (q.Player.QuestStatus(QuestLog.Bastok, "A_LADYS_HEART") == QuestStatus.Available)
                        {
                            q.Quest.Begin();
                        }
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.PortBastok, Npc.PortBastok.ValahMolkot)
                    .OnTrade(q =>
                    {
                        // after completion the trade is not taken
                        if (q.Trade.Matches((Item.Amaryllis, 1)))
                        {
                            return QuestReply.ProgressEvent(160, 0, 236, 4);
                        }
                        else if (IsTradeInTable(q.Trade, FlowerItems))
                        {
                            return QuestReply.ProgressEvent(160, 0, 236, 5);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(160, 0, 236, 0);
                        }
                    })
                    .OnTrigger(QuestReply.ProgressEvent(160, 0, 236, 10));
        }
    }
}
