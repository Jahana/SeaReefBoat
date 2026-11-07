// The Circle of Time (Log ID 3, Quest ID 65), a line-for-line port of scripts/quests/jeuno/The_Circle_of_Time.lua (quest
// rework B3b). BRD AF3. Mertaire : !pos -17 0 -61 245; Imasuke : !pos -165 11 96 246; Perennial Snow : !pos 339 0 -379
// 112; Chalvatot : !pos -105 0.1 72 233; Altar : !pos 109 -3 -145 150. Mertaire's hint (event 139) repeats and plays
// regardless of job (option 0 at Prog 0: Prog 1); the quest is only granted by Imasuke's appraisal (a bard at
// AF3_QUEST_LEVEL): 30 at Prog 1 (option 1 begins it at Prog 3, option 0 sets Prog 2), 29 at Prog 2 (option 1 begins
// it). Accepted: Imasuke 32 at Prog 3; the Perennial Snow of Xarcabard buries Star Ring 1 (3, option 0: Buried = now
// + 30), PERENNIAL_SNOW_WAIT until then, then 2 (option 0: Prog 4); Imasuke 33 (option 0: Prog 5), 31; Chalvatot 99
// (option 1: the Moon Ring, Prog 7; option 0: Prog 6), 98 (option 1: the ring, Prog 7), 97; the Altar of Monastic
// Cavern pops Bugaboo at Prog 7 (noAction), its death sets Prog 8, then 3 (option 0: both rings deleted, Prog 9);
// Chalvatot 96 (option 0: completed, Choral Justaucorps, Paragon of Bard Excellence, fame +7 x3).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class TheCircleOfTime : QuestScript
    {
        public TheCircleOfTime() : base(QuestLog.Jeuno, "THE_CIRCLE_OF_TIME") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/The_Circle_of_Time";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.ChoralJustaucorps, Title = Title.ParagonOfBardExcellence });
            b.Steps(1, 2, 3, 4, 5, 6, 7, 8, 9);

            // Section: Quest is available.
            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedQuest(QuestLog.Jeuno, "THE_REQUIEM"))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Mertaire).OnTrigger(QuestReply.ProgressEvent(139))
                .OnEventFinish(Zone.LowerJeuno, 139, q =>
                {
                    if (q.Option == 0 && q.Quest.Step == 0)
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .Npc(Zone.PortJeuno, Npc.PortJeuno.Imasuke).OnTrigger(q =>
                {
                    if (q.Player.MainJob != Job.Brd ||
                        q.Player.MainLevel < q.Settings.MainNumber("AF3_QUEST_LEVEL"))
                    {
                        return null;
                    }

                    int progress = q.Quest.Step;

                    if (progress == 1)
                    {
                        return QuestReply.ProgressEvent(30);
                    }
                    else if (progress == 2)
                    {
                        return QuestReply.ProgressEvent(29); // Short offer after the player declined once.
                    }
                    return null;
                })
                .OnEventFinish(Zone.PortJeuno, 29, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        q.Quest.SetStep(3);
                    }
                })
                .OnEventFinish(Zone.PortJeuno, 30, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        q.Quest.SetStep(3);
                    }
                    else if (q.Option == 0)
                    {
                        q.Quest.SetStep(2); // Player declined.
                    }
                });

            // Section: Quest accepted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Chalvatot).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 5)
                    {
                        return QuestReply.ProgressEvent(99);
                    }
                    else if (progress == 6)
                    {
                        return QuestReply.ProgressEvent(98);
                    }
                    else if (progress == 7)
                    {
                        return QuestReply.Event(97); // Reminder: place the rings upon the altar.
                    }
                    else if (progress == 9)
                    {
                        return QuestReply.ProgressEvent(96);
                    }
                    return null;
                })
                .OnEventFinish(Zone.ChateauDoraguille, 96, q =>
                {
                    if (q.Option == 0 && q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 7);
                        q.Player.AddFame(FameArea.Bastok, 7);
                        q.Player.AddFame(FameArea.Windurst, 7);
                    }
                })
                .OnEventFinish(Zone.ChateauDoraguille, 98, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.GiveKeyItem(KeyItem.MoonRing);
                        q.Quest.SetStep(7);
                    }
                })
                .OnEventFinish(Zone.ChateauDoraguille, 99, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.GiveKeyItem(KeyItem.MoonRing);
                        q.Quest.SetStep(7);
                    }
                    else if (q.Option == 0)
                    {
                        q.Quest.SetStep(6);
                    }
                })
                .Npc(Zone.MonasticCavern, Npc.MonasticCavern.Altar).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 7 && q.Player.PopFromQm("BUGABOO", claim: true, hide: 0))
                    {
                        return QuestReply.NoAction(); // No message. Bugaboo spawns and immediately aggroes.
                    }
                    else if (progress == 8)
                    {
                        return QuestReply.ProgressEvent(3);
                    }
                    return null;
                })
                .Npc(Zone.MonasticCavern, Npc.MonasticCavern.Bugaboo).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 7)
                    {
                        q.Quest.SetStep(8);
                    }
                })
                .OnEventFinish(Zone.MonasticCavern, 3, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.DelKeyItem(KeyItem.StarRing1);
                        q.Player.DelKeyItem(KeyItem.MoonRing);
                        q.Quest.SetStep(9);
                    }
                })
                .Npc(Zone.PortJeuno, Npc.PortJeuno.Imasuke).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 3)
                    {
                        return QuestReply.Event(32); // Reminder: bury the ring in Xarcabard.
                    }
                    else if (progress == 4)
                    {
                        return QuestReply.ProgressEvent(33);
                    }
                    else if (progress == 5)
                    {
                        return QuestReply.Event(31); // Reminder: take the ring to Chateau d'Oraguille.
                    }
                    return null;
                })
                .OnEventFinish(Zone.PortJeuno, 33, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetStep(5);
                    }
                })
                .Npc(Zone.Xarcabard, Npc.Xarcabard.PerennialSnow).OnTrigger(q =>
                {
                    if (q.Quest.Step != 3)
                    {
                        return null;
                    }

                    int purifiedTime = q.Quest.Var("Buried");

                    if (purifiedTime == 0)
                    {
                        return QuestReply.ProgressEvent(3);
                    }
                    else if (q.Player.SystemTime >= purifiedTime)
                    {
                        return QuestReply.ProgressEvent(2);
                    }
                    else
                    {
                        return QuestReply.MessageSpecial(Text.Xarcabard.PERENNIAL_SNOW_WAIT, KeyItem.StarRing1);
                    }
                })
                .OnEventFinish(Zone.Xarcabard, 2, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetStep(4);
                    }
                })
                .OnEventFinish(Zone.Xarcabard, 3, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetVar("Buried", q.Player.SystemTime + 30); // Capture: the buried ring was purified within 35 seconds.
                    }
                });
        }
    }
}
