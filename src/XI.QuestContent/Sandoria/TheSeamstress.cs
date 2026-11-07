// The Seamstress (Log ID 0, Quest ID 5), a line-for-line port of scripts/quests/sandoria/The_Seamstress.lua (quest
// rework B3b, San d'Oria group 1). Hanaa Punaa : !pos -179.726 -8.8 27.574 230. Her 528 offers it (option 0 begins it,
// any other option sets Prog 1), then 531 offers it again (option 0 begins it). Accepted: 529. Accepted or completed,
// exactly three Sheepskins play 530: Leather Gloves (fromTrade), the trade confirmed, Silencer of the Lambs, and the
// first time the completion (fame +10), after it fame +10 alone. Completed below San d'Oria fame level 2: 590
// (replaceDefault). Lizard Skins (Hanaa Punaa's next quest) reads this quest's status.
// Kept as the Lua has it: the quest repeats without being taken again (the trade works while completed).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class TheSeamstress : QuestScript
    {
        public TheSeamstress() : base(QuestLog.Sandoria, "THE_SEAMSTRESS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/The_Seamstress";

        protected override void Build(QuestScriptBuilder b)
        {
            // the Leather Gloves and the title are given in 530 (repeatable); the reward holds the fame only
            b.Reward(new QuestReward { Fame = 10, FameArea = FameArea.Sandoria });

            b.State(QuestStatus.Available)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.HanaaPunaa).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(528);
                    }
                    else if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(531);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoria, 528, q =>
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
                .OnEventFinish(Zone.SouthernSanDoria, 531, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.HanaaPunaa).OnTrigger(QuestReply.ProgressEvent(529));

            // status ~= QUEST_AVAILABLE: the trade repeats once completed
            b.State(QuestStatus.Accepted)
                .Or(QuestStatus.Completed)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.HanaaPunaa).OnTrade(q =>
                {
                    // npcUtil.tradeHasExactly(trade, { { xi.item.SHEEPSKIN, 3 } })
                    if (q.Trade.HasExactly((Item.Sheepskin, 3)))
                    {
                        return QuestReply.ProgressEvent(530);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoria, 530, q =>
                {
                    // npcUtil.giveItem(player, xi.item.LEATHER_GLOVES, { fromTrade = true })
                    if (q.Player.GiveItem(Item.LeatherGloves, silent: false, fromTrade: true))
                    {
                        q.Trade.Confirm();
                        q.Player.AddTitle(Title.SilencerOfTheLambs);
                        if (!q.Player.HasCompletedQuest(QuestLog.Sandoria, "THE_SEAMSTRESS"))
                        {
                            q.Quest.Complete();
                        }
                        else
                        {
                            q.Player.AddFame(FameArea.Sandoria, 10);
                        }
                    }
                });

            b.State(QuestStatus.Completed, when: q => q.Player.FameLevel(FameArea.Sandoria) < 2)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.HanaaPunaa).OnTrigger(QuestReply.Event(590).ReplaceDefault());
        }
    }
}
