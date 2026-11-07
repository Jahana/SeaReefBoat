// Pretty Little Things (Log ID 3, Quest ID 43), a line-for-line port of scripts/quests/jeuno/Pretty_Little_Things.lua
// (quest rework B3b). Zona Shodhun : !pos -175 -5 -4 246. Available or accepted: any trade to Zona Shodhun plays
// 10023 (a Yellow Rock alone: 1, 246, 2, 1, 0, 1, 5, 1; one of the seven other rocks alone: 0, 246, 1, 1, 0, 1, 7, 1;
// one of the twenty flowers alone: 0, 246, 1, 1, 1, 0, 7, 0; anything else: 0, 246, 0, 0, 0, 0, 7, 0); its finish takes
// the trade when the option is over 0, completes the quest on option 4002 (the trade taken again, the Mog House exit
// flag 0x0008 added, MOGHOUSE_EXIT), else begins it while available. Completed: the same four trades play 10023 with
// other arguments and nothing is taken (no finish). Her talk is her script's (10023, 0, 246, 10).
// Kept Lua quirks: the option the client sends decides (4002 completes after any trade, even from available; any other
// option begins it); option 4002 also confirms the trade twice; the reward table is empty; her own script's 10023 (a
// talk with no trade) also runs the quest's finish: option 0 begins the quest and option 4002 completes it with
// nothing traded.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class PrettyLittleThings : QuestScript
    {
        public PrettyLittleThings() : base(QuestLog.Jeuno, "PRETTY_LITTLE_THINGS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Pretty_Little_Things";

        static readonly int[] InvalidRocks =
        {
            Item.RedRock,
            Item.BlueRock,
            Item.GreenRock,
            Item.TranslucentRock,
            Item.PurpleRock,
            Item.BlackRock,
            Item.WhiteRock,
        };

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
        };

        static bool IsTradeInTable(IQuestContext q, int[] itemTable)
        {
            foreach (int itemID in itemTable)
            {
                if (q.Trade.HasExactly(itemID))
                {
                    return true;
                }
            }

            return false;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            b.State(QuestStatus.Available)
                .Or(QuestStatus.Accepted)
                // Any trade or onTrigger flags this quest.  Default action is handled
                // in the NPC script (onTrigger).  All valid items are consumed until
                // quest is completed.
                .Npc(Zone.PortJeuno, Npc.PortJeuno.ZonaShodhun).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.YellowRock))
                    {
                        return QuestReply.ProgressEvent(10023, 1, 246, 2, 1, 0, 1, 5, 1);
                    }
                    else if (IsTradeInTable(q, InvalidRocks))
                    {
                        return QuestReply.ProgressEvent(10023, 0, 246, 1, 1, 0, 1, 7, 1);
                    }
                    else if (IsTradeInTable(q, FlowerItems))
                    {
                        return QuestReply.ProgressEvent(10023, 0, 246, 1, 1, 1, 0, 7, 0);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(10023, 0, 246, 0, 0, 0, 0, 7, 0);
                    }
                })
                .OnEventFinish(Zone.PortJeuno, 10023, q =>
                {
                    // Three possible event options are returned from the above triggers:
                    // 0    : Invalid Item was traded
                    // 1    : Valid Item was traded
                    // 4002 : Yellow Rock was traded
                    if (q.Option > 0)
                    {
                        q.Trade.Confirm();
                    }

                    if (q.Option == 4002)
                    {
                        if (q.Quest.Complete())
                        {
                            q.Trade.Confirm();
                            int mhflag = q.Player.MoghouseFlag;
                            q.Player.SetMoghouseFlag(mhflag + 0x0008);
                            q.Text.Special(Text.PortJeuno.MOGHOUSE_EXIT);
                        }
                    }
                    else if (q.Player.QuestStatus(QuestLog.Jeuno, "PRETTY_LITTLE_THINGS") == QuestStatus.Available)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Completed)
                // NOTE: No items are consumed after quest complete.
                .Npc(Zone.PortJeuno, Npc.PortJeuno.ZonaShodhun).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.YellowRock))
                    {
                        return QuestReply.ProgressEvent(10023, 0, 246, 4, 1, 0, 1, 7, 0);
                    }
                    else if (IsTradeInTable(q, InvalidRocks))
                    {
                        return QuestReply.ProgressEvent(10023, 0, 246, 5, 1, 0, 1, 7, 0);
                    }
                    else if (IsTradeInTable(q, FlowerItems))
                    {
                        return QuestReply.ProgressEvent(10023, 0, 246, 3, 1, 1, 0, 7, 0);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(10023, 0, 246, 5, 0, 0, 0, 7, 0);
                    }
                });
        }
    }
}
