// Fear of the Dark (Log ID 0, Quest ID 78), a line-for-line port of scripts/quests/sandoria/Fear_of_the_Dark.lua (quest
// rework B3b, San d'Oria group 1). Secodiand : !pos -160 -0 137 231. Available: 19 (option 1 begins it). Accepted or
// completed (status >= QUEST_ACCEPTED), two Bat Wings (tradeHas) play 18: while accepted the completion (fame +10, 200
// gil), after it 200 gil and fame +10; the trade confirmed. No trigger once accepted: Secodiand's default 17.
// Kept as the Lua has it: the quest repeats without being taken again.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class FearOfTheDark : QuestScript
    {
        public FearOfTheDark() : base(QuestLog.Sandoria, "FEAR_OF_THE_DARK") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Fear_of_the_Dark";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 10, FameArea = FameArea.Sandoria, Gil = 200 });

            b.State(QuestStatus.Available)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Secodiand).OnTrigger(QuestReply.ProgressEvent(19))
                .OnEventFinish(Zone.NorthernSanDoria, 19, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            // status >= QUEST_ACCEPTED
            b.State(QuestStatus.Accepted)
                .Or(QuestStatus.Completed)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Secodiand).OnTrade(q =>
                {
                    // npcUtil.tradeHas(trade, { { xi.item.BAT_WING, 2 } })
                    if (q.Trade.HasQty((Item.BatWing, 2)))
                    {
                        return QuestReply.ProgressEvent(18);
                    }
                    return null;
                })
                .OnEventFinish(Zone.NorthernSanDoria, 18, q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted)
                    {
                        q.Quest.Complete();
                    }
                    else
                    {
                        q.Player.GiveCurrency("gil", 200);
                        q.Player.AddFame(FameArea.Sandoria, 10);
                    }

                    q.Trade.Confirm();
                });
        }
    }
}
