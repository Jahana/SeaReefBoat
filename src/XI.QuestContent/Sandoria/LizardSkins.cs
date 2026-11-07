// Lizard Skins (Log ID 0, Quest ID 15), a line-for-line port of scripts/quests/sandoria/Lizard_Skins.lua (quest rework
// B3b, San d'Oria group 1). Hanaa Punaa : !pos -179.726 -8.8 27.574 230. Offered at San d'Oria fame level 2 once The
// Seamstress is completed: 559 (option 0 begins it, any other option sets Prog 1), then 562 (option 0 begins it).
// Accepted: 560. Accepted or completed, exactly three Lizard Skins play 561: Lizard Gloves (fromTrade), the trade
// confirmed, Lizard Skinner, and the first time the completion (fame +10), after it fame +10 alone. Completed below fame
// level 3: 591 (replaceDefault). Black Tiger Skins reads this quest's status.
// Kept as the Lua has it: the quest repeats without being taken again (the trade works while completed).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class LizardSkins : QuestScript
    {
        public LizardSkins() : base(QuestLog.Sandoria, "LIZARD_SKINS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Lizard_Skins";

        protected override void Build(QuestScriptBuilder b)
        {
            // the Lizard Gloves and the title are given in 561 (repeatable); the reward holds the fame only
            b.Reward(new QuestReward { Fame = 10, FameArea = FameArea.Sandoria });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.FameLevel(FameArea.Sandoria) >= 2 &&
                    q.Player.HasCompletedQuest(QuestLog.Sandoria, "THE_SEAMSTRESS"))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.HanaaPunaa).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(559);
                    }
                    else if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(562);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoria, 559, q =>
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
                .OnEventFinish(Zone.SouthernSanDoria, 562, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.HanaaPunaa).OnTrigger(QuestReply.ProgressEvent(560));

            // status ~= QUEST_AVAILABLE: the trade repeats once completed
            b.State(QuestStatus.Accepted)
                .Or(QuestStatus.Completed)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.HanaaPunaa).OnTrade(q =>
                {
                    // npcUtil.tradeHasExactly(trade, { { xi.item.LIZARD_SKIN, 3 } })
                    if (q.Trade.HasExactly((Item.LizardSkin, 3)))
                    {
                        return QuestReply.ProgressEvent(561);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoria, 561, q =>
                {
                    // npcUtil.giveItem(player, xi.item.LIZARD_GLOVES, { fromTrade = true })
                    if (q.Player.GiveItem(Item.LizardGloves, silent: false, fromTrade: true))
                    {
                        q.Trade.Confirm();
                        q.Player.AddTitle(Title.LizardSkinner);
                        if (!q.Player.HasCompletedQuest(QuestLog.Sandoria, "LIZARD_SKINS"))
                        {
                            q.Quest.Complete();
                        }
                        else
                        {
                            q.Player.AddFame(FameArea.Sandoria, 10);
                        }
                    }
                });

            b.State(QuestStatus.Completed, when: q => q.Player.FameLevel(FameArea.Sandoria) < 3)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.HanaaPunaa).OnTrigger(QuestReply.Event(591).ReplaceDefault());
        }
    }
}
