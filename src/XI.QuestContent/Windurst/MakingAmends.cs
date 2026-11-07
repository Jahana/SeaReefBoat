// Making Amends (Log ID 2, Quest ID 3), a line-for-line port of scripts/quests/windurst/Making_Amends.lua (quest rework
// B3b). Hakkuru-Rinkuru !pos -111 -4 101 240. At Windurst fame level 2, Hakkuru-Rinkuru's 274 (0, Block of Animal
// Glue), option 1, begins it. Accepted: Hakkuru-Rinkuru's 275, Kuroido-Moido's 276; a Block of Animal Glue traded to
// Hakkuru-Rinkuru (tradeHas: the glue, the rest kept) plays 277 (1500), which completes it (fame 30, the title Quick
// Fixer; the glue taken; 1500 gil; the quest's mustZone). Completed: Hakkuru-Rinkuru's 278 while the mustZone holds,
// else 281 (replaceDefault); Kuroido-Moido's 279 while it holds. Kuroido-Moido's script reads the status and the
// mustZone (Making Amens' offer). The Lua file has no known bug to keep; the reward has no item, so the completion
// cannot fail.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class MakingAmends : QuestScript
    {
        public MakingAmends() : base(QuestLog.Windurst, "MAKING_AMENDS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Making_Amends";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { FameArea = FameArea.Windurst, Fame = 30, Title = Title.QuickFixer });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.FameLevel(FameArea.Windurst) >= 2)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.HakkuruRinkuru).OnTrigger(QuestReply.ProgressEvent(274, 0, Item.BlockOfAnimalGlue))
                .OnEventFinish(Zone.PortWindurst, 274, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.HakkuruRinkuru)
                    .OnTrigger(q => QuestReply.Event(275, 0, Item.BlockOfAnimalGlue))
                    .OnTrade(q =>
                    {
                        // npcUtil.tradeHas(trade, { { xi.item.BLOCK_OF_ANIMAL_GLUE, 1 } })
                        if (q.Trade.HasQty((Item.BlockOfAnimalGlue, 1)))
                        {
                            return QuestReply.ProgressEvent(277, 1500); // "Obtained X gil." Text is baked into the CS, so gil needs to be given outside of the quest
                        }

                        return null;
                    })
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KuroidoMoido).OnTrigger(QuestReply.Event(276))
                .OnEventFinish(Zone.PortWindurst, 277, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                        q.Player.AddGil(1500);
                        q.Quest.SetMustZone();
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.HakkuruRinkuru).OnTrigger(q =>
                {
                    if (q.Quest.MustZone)
                    {
                        return QuestReply.Event(278);
                    }
                    else
                    {
                        return QuestReply.Event(281).ReplaceDefault();
                    }
                })
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KuroidoMoido).OnTrigger(q =>
                {
                    if (q.Quest.MustZone)
                    {
                        return QuestReply.Event(279);
                    }

                    return null;
                });
        }
    }
}
