// Her Majesty's Garden (Log ID 0, Quest ID 62), a line-for-line port of scripts/quests/sandoria/Her_Majestys_Garden.lua
// (quest rework B3b, San d'Oria group 1). Chalvatot : !pos -105 0.1 72 233. Offered at San d'Oria fame level 4: 84 (option
// 1 begins it). Accepted: 82; a Chunk of Derfland Humus (tradeHas) plays 83: completed (fame +40, the Map of the
// Northlands Area), the trade confirmed. No section once completed: Chalvatot's default 531. The Lua file has no known
// bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class HerMajestysGarden : QuestScript
    {
        public HerMajestysGarden() : base(QuestLog.Sandoria, "HER_MAJESTYS_GARDEN") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Her_Majestys_Garden";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 40, FameArea = FameArea.Sandoria, KeyItem = KeyItem.MapOfTheNorthlandsArea });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Sandoria) >= 4)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Chalvatot).OnTrigger(QuestReply.ProgressEvent(84))
                .OnEventFinish(Zone.ChateauDoraguille, 84, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Chalvatot)
                    // npcUtil.tradeHas(trade, xi.item.CHUNK_OF_DERFLAND_HUMUS)
                    .OnTrade(q => q.Trade.Has(Item.ChunkOfDerflandHumus) ? QuestReply.ProgressEvent(83) : null)
                    .OnTrigger(QuestReply.Event(82))
                .OnEventFinish(Zone.ChateauDoraguille, 83, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                    }
                });
        }
    }
}
