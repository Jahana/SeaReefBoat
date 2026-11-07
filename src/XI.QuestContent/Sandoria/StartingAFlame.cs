// Starting a Flame (Log ID 0, Quest ID 77), a line-for-line port of scripts/quests/sandoria/Starting_a_Flame.lua (quest
// rework B3b, San d'Oria group 1). Legata : !pos 82 0 116 230. Available: 37 (option 1 begins it). Accepted: 35; completed:
// 37 with option [7] = 1 (no finish handler). Accepted or completed, four Flint Stones (tradeHas: more may be traded,
// four are taken) play 36: the first time the completion (no reward table), then each time fame +10, 100 gil and the
// trade confirmed.
// Kept as the Lua has it: the quest repeats without being taken again.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class StartingAFlame : QuestScript
    {
        public StartingAFlame() : base(QuestLog.Sandoria, "STARTING_A_FLAME") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Starting_a_Flame";

        protected override void Build(QuestScriptBuilder b)
        {
            // no quest.reward in the Lua

            b.State(QuestStatus.Available)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Legata).OnTrigger(QuestReply.ProgressEvent(37))
                .OnEventFinish(Zone.SouthernSanDoria, 37, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            // status ~= QUEST_AVAILABLE
            b.State(QuestStatus.Accepted)
                .Or(QuestStatus.Completed)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Legata)
                    .OnTrade(q =>
                    {
                        // npcUtil.tradeHas(trade, { { xi.item.FLINT_STONE, 4 } })
                        if (q.Trade.HasQty((Item.FlintStone, 4)))
                        {
                            return QuestReply.ProgressEvent(36);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Status == QuestStatus.Accepted)
                        {
                            return QuestReply.Event(35);
                        }
                        else
                        {
                            return QuestReply.Event(37).Option(7, 1);
                        }
                    })
                .OnEventFinish(Zone.SouthernSanDoria, 36, q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted)
                    {
                        q.Quest.Complete();
                    }

                    q.Player.AddFame(FameArea.Sandoria, 10);
                    q.Player.GiveCurrency("gil", 100);
                    q.Trade.Confirm();
                });
        }
    }
}
