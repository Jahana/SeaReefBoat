// Searching for the Right Words (Log ID 3, Quest ID 66), a line-for-line port of
// scripts/quests/jeuno/Searching_for_the_Right_Words.lua (quest rework B3b). Kurou-Morou : !pos -4 -6 -28 245; Chululu :
// !pos -13 -6 -42 245; Ilumida : !pos -75 -1 58 244; qm2 (???) : !pos 34.651 -20.183 -61.647 153. With A Candlelight
// Vigil, Rubbish Day and Never to Return completed, Jeuno fame level 6 and no zone change owed (the Vigil's reward sets
// one): Kurou-Morou plays 17 at Prog 0 (option 0: Prog 1), then 38 (Prog 1) / 36 (Prog 2); Chululu 57 at Prog 1-2;
// Ilumida offers 197 at Prog 1 (option 1: begun, Prog 3; option 0: Prog 2) and 201 at Prog 2 (option 1: begun, Prog 3).
// Accepted: Kurou-Morou 39, Chululu 57, Ilumida 199 (198 with the Moondrop: completed, 3000 gil, Scroll of Sleepga II,
// the Moondrop taken, fame +23 x3, Post 1). The ??? in the Boyahda Tree acts from 19:00 to 3:59, not at the new moon,
// without the Moondrop, while Agas is not up: at Prog 3 it spawns Agas claimed (SOMETHING_NOT_RIGHT, over the ???'s own
// text), at Prog 4 (Agas killed at Prog 3) it plays 14 (option 0: the Moondrop, Prog 5). Completed: Kurou-Morou 154
// while Post is 1 (option 0: Post 0), else 37; Chululu 56 once Post is 0; Ilumida 196 (Post 0) / 200 (Post 1), both
// over her default. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class SearchingForTheRightWords : QuestScript
    {
        public SearchingForTheRightWords() : base(QuestLog.Jeuno, "SEARCHING_FOR_THE_RIGHT_WORDS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Searching_for_the_Right_Words";

        const int NewMoon = 0; // xi.moonCycle.NEW_MOON (scripts/enum/moon_cycle.lua)

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Gil = 3000, Item = Item.ScrollOfSleepgaIi });
            b.Steps(1, 2, 3, 4, 5);

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Jeuno, "A_CANDLELIGHT_VIGIL") &&
                    q.Player.HasCompletedQuest(QuestLog.Jeuno, "RUBBISH_DAY") &&
                    q.Player.HasCompletedQuest(QuestLog.Jeuno, "NEVER_TO_RETURN") &&
                    q.Player.FameLevel(FameArea.Jeuno) >= 6 &&
                    !q.Quest.MustZone)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.KurouMorou).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 0)
                    {
                        return QuestReply.ProgressEvent(17); // Runga-Kopunga visits, Kurou-Morou asks for advice
                    }
                    else if (progress == 1)
                    {
                        return QuestReply.Event(38); // 'Oh, what should I do!?'
                    }
                    else if (progress == 2)
                    {
                        return QuestReply.Event(36); // Hint about the moonlight in the Boyahda Tree
                    }
                    return null;
                })
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Chululu).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 1 || progress == 2)
                    {
                        return QuestReply.Event(57); // 'Oh, what should I do...?'
                    }
                    return null;
                })
                .OnEventFinish(Zone.LowerJeuno, 17, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Ilumida).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 1)
                    {
                        return QuestReply.ProgressEvent(197); // Quest offered.
                    }
                    else if (progress == 2)
                    {
                        return QuestReply.ProgressEvent(201); // Quest offered again. (Happens if you decline initially)
                    }
                    return null;
                })
                .OnEventFinish(Zone.UpperJeuno, 197, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        q.Quest.SetStep(3);
                    }
                    else if (q.Option == 0)
                    {
                        q.Quest.SetStep(2);
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 201, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        q.Quest.SetStep(3);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.KurouMorou).OnTrigger(QuestReply.Event(39)) // 'You've got to go to the Boyahda Tree and get that moondrop!'
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Chululu).OnTrigger(QuestReply.Event(57)) // 'Oh, what should I do...?'
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Ilumida).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.Moondrop))
                    {
                        return QuestReply.ProgressEvent(198); // Quest complete
                    }
                    else
                    {
                        return QuestReply.Event(199); // Reminder
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 198, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.Moondrop);
                        q.Player.AddFame(FameArea.Sandoria, 23);
                        q.Player.AddFame(FameArea.Bastok, 23);
                        q.Player.AddFame(FameArea.Windurst, 23);
                        q.Quest.SetVar("Post", 1);
                    }
                })
                .Npc(Zone.TheBoyahdaTree, Npc.TheBoyahdaTree.qm2).OnTrigger(q =>
                {
                    int currentHour = q.Player.VanadielHour;

                    if (q.Player.VanadielMoonCycle == NewMoon)
                    {
                        return null;
                    }

                    if (currentHour < 19 && currentHour >= 4)
                    {
                        return null;
                    }

                    if (q.Player.HasKeyItem(KeyItem.Moondrop))
                    {
                        return null;
                    }

                    // local agas = GetMobByID(boyahdaID.mob.AGAS); if not agas then return end: Agas is a placed mob of
                    // the zone (mobs.yaml), so the lookup never fails; MobIsSpawned reads the same GetMobByID
                    if (q.Player.MobIsSpawned(Mob.TheBoyahdaTree.AGAS))
                    {
                        return null;
                    }

                    int progress = q.Quest.Step;

                    if (progress == 4)
                    {
                        return QuestReply.ProgressEvent(14); // Agas defeated, obtain the Moondrop
                    }
                    else if (progress == 3)
                    {
                        q.Player.SpawnMob(Mob.TheBoyahdaTree.AGAS, 0, claim: true); // Spawn Agas
                        return QuestReply.MessageSpecial(Text.TheBoyahdaTree.SOMETHING_NOT_RIGHT).ReplaceDefault();
                    }
                    return null;
                })
                .Npc(Zone.TheBoyahdaTree, Npc.TheBoyahdaTree.Agas).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        q.Quest.SetStep(4);
                    }
                })
                .OnEventFinish(Zone.TheBoyahdaTree, 14, q =>
                {
                    if (q.Option == 0 && q.Player.GiveKeyItem(KeyItem.Moondrop))
                    {
                        q.Quest.SetStep(5);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.KurouMorou).OnTrigger(q =>
                {
                    if (q.Quest.Var("Post") == 1)
                    {
                        return QuestReply.ProgressEvent(154); // Kurou-Morou confesses to Chululu
                    }
                    else
                    {
                        return QuestReply.Event(37); // 'So, do you think she understood how I feel?'
                    }
                })
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Chululu).OnTrigger(q =>
                {
                    if (q.Quest.Var("Post") == 0)
                    {
                        return QuestReply.Event(56); // 'Boy, was I surprised when he told me he...'
                    }
                    return null;
                })
                .OnEventFinish(Zone.LowerJeuno, 154, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetVar("Post", 0);
                    }
                })
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Ilumida).OnTrigger(q =>
                {
                    if (q.Quest.Var("Post") == 0)
                    {
                        return QuestReply.Event(196).ReplaceDefault(); // 'Our little friend was able to express his feelings?'
                    }
                    else
                    {
                        return QuestReply.Event(200).ReplaceDefault(); // 'Thank you again for all of your help.'
                    }
                });
        }
    }
}
