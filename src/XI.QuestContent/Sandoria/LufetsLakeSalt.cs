// Lufet's Lake Salt (Log ID 0, Quest ID 81), a line-for-line port of scripts/quests/sandoria/Lufets_Lake_Salt.lua (quest
// rework B3b, San d'Oria group 1). Nogelle : !pos -70 -5.5 -33 232. Available: 12 (option 1 begins it). Accepted: 10;
// three Chunks of Lufet Salt (tradeHas: more may be traded, three are taken) play 11: completed (fame +10, 600 gil, Bean
// Cuisine Salter), the trade confirmed. Completed: 522 (replaceDefault). The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class LufetsLakeSalt : QuestScript
    {
        public LufetsLakeSalt() : base(QuestLog.Sandoria, "LUFETS_LAKE_SALT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Lufets_Lake_Salt";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 10, FameArea = FameArea.Sandoria, Gil = 600, Title = Title.BeanCuisineSalter });

            b.State(QuestStatus.Available)
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Nogelle).OnTrigger(QuestReply.ProgressEvent(12))
                .OnEventFinish(Zone.PortSanDoria, 12, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Nogelle)
                    .OnTrade(q =>
                    {
                        // npcUtil.tradeHas(trade, { { xi.item.CHUNK_OF_LUFET_SALT, 3 } })
                        if (q.Trade.HasQty((Item.ChunkOfLufetSalt, 3)))
                        {
                            return QuestReply.ProgressEvent(11);
                        }
                        return null;
                    })
                    .OnTrigger(QuestReply.Event(10))
                .OnEventFinish(Zone.PortSanDoria, 11, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Nogelle).OnTrigger(QuestReply.Event(522).ReplaceDefault());
        }
    }
}
