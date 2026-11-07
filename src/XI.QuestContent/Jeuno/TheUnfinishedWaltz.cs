// The Unfinished Waltz (Log ID 3, Quest ID 96), a line-for-line port of
// scripts/quests/jeuno/DNC_AF1_The_Unfinished_Waltz.lua (quest rework B3b). Laila : !pos -54.045 -1 100.996 244; qm1 :
// !pos -157.16 -8 596.9 89. A Dancer from AF1_QUEST_LEVEL: Laila offers 10129 (begun). Accepted: Laila 10130 until The
// Essence of Dance has been seen, then 10133 (completed, War Hoop, Promising Dancer; The Road to Divadom's Timer =
// tomorrow, by its var name, and the local var Quest[3][97]mustZone); Rhea Myuliah 10131 at Prog 0 (Prog 1), else
// 10132; the ??? in Grauberg [S] (qm1: both NPCs of that name), at Prog 1 without the key item, by the hippoEvent local
// var: 12 for a Dancer at 0 (hippoEvent 1), the Migratory Hippogryph spawned with enmity at 1 when it is not up
// (A_SHIVER_RUNS_DOWN), 13 at 2 (the key item), else ATTEND_TO_MORE_PRESSING; the hippogryph's death at 1 sets 2.
// Completed, until The Road to Divadom is completed: Laila 10134, Rhea Myuliah 10135, over the default.
// Kept Lua quirk: 10133 writes The Road to Divadom's mustZone as a local var (player:setLocalVar), which getMustZone
// (the char var) never reads, so it holds nothing back.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class TheUnfinishedWaltz : QuestScript
    {
        public TheUnfinishedWaltz() : base(QuestLog.Jeuno, "THE_UNFINISHED_WALTZ") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/DNC_AF1_The_Unfinished_Waltz";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.WarHoop, Title = Title.PromisingDancer });
            b.Steps(1);

            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainJob == Job.Dnc &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF1_QUEST_LEVEL"))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Laila).OnTrigger(QuestReply.ProgressEvent(10129))
                .OnEventFinish(Zone.UpperJeuno, 10129, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Laila).OnTrigger(q =>
                {
                    if (q.Player.SeenKeyItem(KeyItem.TheEssenceOfDance))
                    {
                        return QuestReply.ProgressEvent(10133);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(10130);
                    }
                })
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.RheaMyuliah).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(10131);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(10132);
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 10131, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.UpperJeuno, 10133, q =>
                {
                    if (q.Quest.Complete())
                    {
                        // Set mustZone and Timer for "The Road to Divadom" Quest
                        q.Player.SetCharVar("Quest[3][97]Timer", q.Player.VanadielUniqueDay + 1);
                        q.Player.SetLocalVar("Quest[3][97]mustZone", 1);
                    }
                })
                .Npc(Zone.GraubergS, Npc.GraubergS.qm1).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.TheEssenceOfDance) &&
                        q.Quest.Step == 1)
                    {
                        int hippoEvent = q.Quest.LocalVar("hippoEvent");

                        if (hippoEvent == 0 &&
                            q.Player.MainJob == Job.Dnc)
                        {
                            return QuestReply.ProgressEvent(12);
                        }
                        else if (hippoEvent == 1 &&
                                 !q.Player.MobIsSpawned(Mob.GraubergS.MIGRATORY_HIPPOGRYPH))
                        {
                            q.Player.SpawnMob(Mob.GraubergS.MIGRATORY_HIPPOGRYPH, 0, claim: false);
                            return QuestReply.MessageSpecial(Text.GraubergS.A_SHIVER_RUNS_DOWN);
                        }
                        else if (hippoEvent == 2)
                        {
                            return QuestReply.ProgressEvent(13);
                        }
                        else
                        {
                            return QuestReply.MessageSpecial(Text.GraubergS.ATTEND_TO_MORE_PRESSING);
                        }
                    }
                    return null;
                })
                .Npc(Zone.GraubergS, Npc.GraubergS.MigratoryHippogryph).OnMobDeath(q =>
                {
                    if (q.Quest.LocalVar("hippoEvent") == 1)
                    {
                        q.Quest.SetLocalVar("hippoEvent", 2);
                    }
                })
                .OnEventFinish(Zone.GraubergS, 12, q =>
                {
                    q.Quest.SetLocalVar("hippoEvent", 1);
                })
                .OnEventFinish(Zone.GraubergS, 13, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.TheEssenceOfDance);
                });

            b.State(QuestStatus.Completed, when: q =>
                    !q.Player.HasCompletedQuest(QuestLog.Jeuno, "THE_ROAD_TO_DIVADOM"))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Laila).OnTrigger(QuestReply.Event(10134).ReplaceDefault())
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.RheaMyuliah).OnTrigger(QuestReply.Event(10135).ReplaceDefault());
        }
    }
}
