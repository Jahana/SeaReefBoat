// Black Tiger Skins (Log ID 0, Quest ID 31), a line-for-line port of scripts/quests/sandoria/Black_Tiger_Skins.lua
// (quest rework B3b, San d'Oria group 1). Hanaa Punaa : !pos -179.726 -8.8 27.574 230. Offered at San d'Oria fame level 3
// once Lizard Skins is completed: 576 (option 0 begins it, any other option sets Prog 1), then 579 (option 0 begins it).
// Accepted: 578; exactly three Tiger Hides play 577: completed (Tiger Stole, fromTrade; fame +30; Cat Skinner), then the
// trade is confirmed. Completed at fame level 3 or more: 592 (replaceDefault).
//
// The reward's item has itemParams = { fromTrade = true }, which QuestReward cannot carry (only Item / Items). So the
// reward holds fame and title, and 577 gives the Tiger Stole first through npcUtil.giveItem with the same params, then
// completes: the same calls in the same order as npcUtil.completeQuest (its item first, stopping when it fails). See
// the platform need "Reward itemParams" (a QuestReward field for it is open).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class BlackTigerSkins : QuestScript
    {
        public BlackTigerSkins() : base(QuestLog.Sandoria, "BLACK_TIGER_SKINS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Black_Tiger_Skins";

        protected override void Build(QuestScriptBuilder b)
        {
            // item = TIGER_STOLE, itemParams = { fromTrade = true }: given in 577 (see the header)
            b.Reward(new QuestReward { Fame = 30, FameArea = FameArea.Sandoria, Title = Title.CatSkinner });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.FameLevel(FameArea.Sandoria) >= 3 &&
                    q.Player.HasCompletedQuest(QuestLog.Sandoria, "LIZARD_SKINS"))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.HanaaPunaa).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(576);
                    }
                    else if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(579);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoria, 576, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                    else
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 579, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.HanaaPunaa)
                    .OnTrade(q =>
                    {
                        // npcUtil.tradeHasExactly(trade, { { xi.item.TIGER_HIDE, 3 } })
                        if (q.Trade.HasExactly((Item.TigerHide, 3)))
                        {
                            return QuestReply.ProgressEvent(577);
                        }
                        return null;
                    })
                    .OnTrigger(QuestReply.ProgressEvent(578))
                .OnEventFinish(Zone.SouthernSanDoria, 577, q =>
                {
                    // quest:complete(player): its item (with itemParams) first, then the rest of the reward
                    // npcUtil.giveItem(player, xi.item.TIGER_STOLE, { fromTrade = true }) (npcUtil.completeQuest's first call)
                    if (q.Player.GiveItem(Item.TigerStole, silent: false, fromTrade: true) && q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                    }
                });

            b.State(QuestStatus.Completed, when: q => q.Player.FameLevel(FameArea.Sandoria) >= 3)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.HanaaPunaa).OnTrigger(QuestReply.Event(592).ReplaceDefault());
        }
    }
}
