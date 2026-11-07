// Chasing Tales (Log ID 2, Quest ID 13), a line-for-line port of scripts/quests/windurst/Chasing_Tales.lua (quest rework
// B3b). Tosuka-Porika : !pos -26 -6 103 238; Furakku-Norakku : !pos -19 -5 101 238; Hae Jakkya : !pos 57.387 -2.5
// -140.757 241; Hae Jakhya : !pos -75.36 -7.4 -23.82 230. Offered after Early Bird Catches the Bookworm at fame level 3,
// except to a Windurst char on Windurst mission 8-2. Furakku gives the overdue notice, Hae Jakkya in Windurst Woods
// sets Prog 1, Hae Jakhya in Southern San d'Oria gives A Song of Love, and Furakku completes it. A Smudge on One's
// Record reads its completion. Kept as the Lua file has it: the completion sets the player local var
// 'Quest[2][12]mustZone' (A Smudge's prefix), which A Smudge's getMustZone (a char var) never reads, but which this
// quest's completed section reads for Furakku until the char zones; the check's quest:getMustZone reads this quest's own
// char var, which nothing sets; Furakku's "notice held" test inside the no-song branch is always true there.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class ChasingTales : QuestScript
    {
        public ChasingTales() : base(QuestLog.Windurst, "CHASING_TALES") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Chasing_Tales";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 30, FameArea = FameArea.Windurst, Gil = 2800, Title = Title.SaviorOfKnowledge });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Windurst, "EARLY_BIRD_CATCHES_THE_BOOKWORM") &&
                    q.Player.FameLevel(FameArea.Windurst) >= 3 &&
                    !q.Quest.MustZone)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.TosukaPorika).OnTrigger(q =>
                {
                    // TODO (Lua): Requirements for availability need to be verified
                    if (q.Player.Nation != Nation.Windurst ||
                        q.Player.CurrentMission(MissionLog.Windurst) != Mission.Windurst.TheJesterWhodBeKing)
                    {
                        return QuestReply.ProgressEvent(403);
                    }

                    return null;
                })
                .OnEventFinish(Zone.WindurstWaters, 403, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.HaeJakhya).OnTrigger(q =>
                {
                    bool hasBookKeyItem = q.Player.HasKeyItem(KeyItem.ASongOfLove);

                    if (!hasBookKeyItem && q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(611);
                    }
                    else if (hasBookKeyItem)
                    {
                        return QuestReply.ProgressEvent(612, 0, KeyItem.ASongOfLove);
                    }

                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoria, 611, q => q.Player.GiveKeyItem(KeyItem.ASongOfLove))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.TosukaPorika).OnTrigger(q =>
                    q.Quest.Step == 0 ? QuestReply.ProgressEvent(406) : QuestReply.ProgressEvent(412))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.FurakkuNorakku).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.OverdueBookNotificationChasingTales))
                    {
                        return QuestReply.ProgressEvent(404, 0, KeyItem.ASongOfLove);
                    }
                    else if (!q.Player.HasKeyItem(KeyItem.ASongOfLove))
                    {
                        if (q.Quest.Step == 1)
                        {
                            return QuestReply.ProgressEvent(409);
                        }
                        else if (q.Player.HasKeyItem(KeyItem.OverdueBookNotificationChasingTales))
                        {
                            return QuestReply.ProgressEvent(405, 0, 126);
                        }
                    }
                    else if (q.Player.HasKeyItem(KeyItem.ASongOfLove))
                    {
                        return QuestReply.ProgressEvent(410);
                    }

                    return null;
                })
                .OnEventFinish(Zone.WindurstWaters, 404, q => q.Player.GiveKeyItem(KeyItem.OverdueBookNotificationChasingTales))
                .OnEventFinish(Zone.WindurstWaters, 410, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.OverdueBookNotificationChasingTales);
                        q.Player.DelKeyItem(KeyItem.ASongOfLove);
                        q.Player.SetLocalVar("Quest[2][12]mustZone", 1);
                    }
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.AnPolaali).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.ASongOfLove))
                    {
                        return QuestReply.ProgressEvent(407);
                    }
                    else if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(404);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.AnShanaa).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.ASongOfLove))
                    {
                        return QuestReply.ProgressEvent(408, 0, KeyItem.ASongOfLove);
                    }
                    else if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(405);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.HaeJakkya).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.ASongOfLove))
                    {
                        return QuestReply.ProgressEvent(406);
                    }
                    else if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(403, 0, KeyItem.ASongOfLove);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.OverdueBookNotificationChasingTales))
                    {
                        return QuestReply.ProgressEvent(402, 0, KeyItem.ASongOfLove);
                    }

                    return null;
                })
                .OnEventFinish(Zone.WindurstWoods, 402, q => q.Quest.SetStep(1));

            b.State(QuestStatus.Completed)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.FurakkuNorakku).OnTrigger(q =>
                    q.Player.LocalVar("Quest[2][12]mustZone") == 1 ? QuestReply.Event(411).ReplaceDefault() : null)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.AnPolaali).OnTrigger(QuestReply.Event(407).ImportantOnce())
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.AnShanaa).OnTrigger(QuestReply.Event(408, 0, 126).ImportantOnce())
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.HaeJakkya).OnTrigger(QuestReply.Event(406).ImportantOnce());
        }
    }
}
