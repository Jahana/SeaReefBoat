// Early Bird Catches the Bookworm (Log ID 2, Quest ID 20), a line-for-line port of
// scripts/quests/windurst/Early_Bird_Catches_the_Bookworm.lua (quest rework B3b). Tosuka-Porika : !pos -26 -6 103 238;
// Furakku-Norakku : !pos -19 -5 101 238; Orn : !pos -68 -9 30 238; Quu Bokye : !pos -159 16 181 145. Offered after
// Glyph Hanger at fame level 2, except to a Windurst char on Windurst mission 2-1 or 7-1. Furakku gives the overdue
// notice, Orn takes it, Quu Bokye takes a Silver Beastcoin for Art for Everyone, and Furakku completes it. Chasing Tales
// reads its completion. Kept as the Lua file has it: Furakku's 400 completes the quest from Prog 2, so Orn's 398 (Prog
// 3) is optional; the completion sets the player local var 'Quest[2][13]mustZone' (Chasing Tales' prefix), which
// Chasing Tales' getMustZone (a char var) never reads, but which this quest's completed section reads until the char
// zones; the check's quest:getMustZone reads this quest's own char var, which nothing sets.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class EarlyBirdCatchesTheBookworm : QuestScript
    {
        public EarlyBirdCatchesTheBookworm() : base(QuestLog.Windurst, "EARLY_BIRD_CATCHES_THE_BOOKWORM") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Early_Bird_Catches_the_Bookworm";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Windurst, Gil = 1500, Title = Title.SaviorOfKnowledge });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Windurst, "GLYPH_HANGER") &&
                    q.Player.FameLevel(FameArea.Windurst) >= 2 &&
                    !q.Quest.MustZone)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.TosukaPorika).OnTrigger(q =>
                {
                    // TODO (Lua): Requirements for availability need to be verified
                    if (q.Player.Nation != Nation.Windurst ||
                        (q.Player.CurrentMission(MissionLog.Windurst) != Mission.Windurst.LostForWords &&
                        q.Player.CurrentMission(MissionLog.Windurst) != Mission.Windurst.TheSixthMinistry))
                    {
                        return QuestReply.ProgressEvent(387);
                    }

                    return null;
                })
                .OnEventFinish(Zone.WindurstWaters, 387, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.FurakkuNorakku).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        if (!q.Player.HasKeyItem(KeyItem.OverdueBookNotificationsEarlyBird))
                        {
                            return QuestReply.ProgressEvent(389, 0, KeyItem.ArtForEveryone);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(390, 0, KeyItem.ArtForEveryone);
                        }
                    }
                    else if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(397, 0, KeyItem.ArtForEveryone);
                    }
                    else if (questProgress >= 2)
                    {
                        return QuestReply.ProgressEvent(400);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Orn).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0 && q.Player.HasKeyItem(KeyItem.OverdueBookNotificationsEarlyBird))
                    {
                        return QuestReply.ProgressEvent(395, 0, KeyItem.ArtForEveryone);
                    }
                    else if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(396);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.ProgressEvent(398);
                    }
                    else if (questProgress == 3)
                    {
                        return QuestReply.ProgressEvent(399);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.TosukaPorika).OnTrigger(QuestReply.ProgressEvent(388))
                .OnEventFinish(Zone.WindurstWaters, 389, q => q.Player.GiveKeyItem(KeyItem.OverdueBookNotificationsEarlyBird))
                .OnEventFinish(Zone.WindurstWaters, 395, q =>
                {
                    q.Quest.SetStep(1);
                    q.Player.DelKeyItem(KeyItem.OverdueBookNotificationsEarlyBird);
                })
                .OnEventFinish(Zone.WindurstWaters, 398, q => q.Quest.SetStep(3))
                .OnEventFinish(Zone.WindurstWaters, 400, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetLocalVar("Quest[2][13]mustZone", 1);
                        q.Player.DelKeyItem(KeyItem.ArtForEveryone);
                    }
                })
                .Npc(Zone.Giddeus, Npc.Giddeus.QuuBokye)
                    .OnTrade(q =>
                    {
                        if (q.Quest.Step == 1 && q.Trade.HasExactly(Item.SilverBeastcoin))
                        {
                            return QuestReply.ProgressEvent(58);
                        }

                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int questProgress = q.Quest.Step;

                        if (questProgress == 1)
                        {
                            return QuestReply.ProgressEvent(57);
                        }
                        else if (questProgress == 2)
                        {
                            return QuestReply.ProgressEvent(59);
                        }

                        return null;
                    })
                .OnEventFinish(Zone.Giddeus, 58, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveKeyItem(KeyItem.ArtForEveryone);
                    q.Quest.SetStep(2);
                });

            b.State(QuestStatus.Completed, when: q => q.Player.LocalVar("Quest[2][13]mustZone") == 1)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.FurakkuNorakku).OnTrigger(QuestReply.Event(401).ReplaceDefault());
        }
    }
}
