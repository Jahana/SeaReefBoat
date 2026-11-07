// Fires of Discontent (Log ID 7, Quest ID 13), a line-for-line port of
// scripts/quests/crystalWar/WOTG_BAS_2_Fires_of_Discontent.lua (quest rework B3b Crystal War group 3). Bastok Markets
// [S]: Engelhart !pos -80.1 -4.4 -125.3 (default 104), Pagdako !pos -202.1 -6 -93.9 (default 106), Gentle Tiger !pos
// -203.9 -10 2.2 (default 109). Metalworks: Iron Eater !pos 92.9 -19.5 1.8 (default 604). Grauberg [S]: qm5 !pos 259.8
// 33.2 515.3.
// Available, Better Part of Valor completed: Engelhart 120, whose finish begins it. Accepted (Prog): Engelhart: 0 / 1:
// 121 (an Event); 2: 124 (-> 3); 3: 125 (an Event); 4: 126 (-> 5); 5: 127 (an Event); 6: 164, whose finish completes
// it (10000 gil). Pagdako: 0: 122 (-> 1), else 123 (an Event). Gentle Tiger: 5: 160 (-> 6), else 161 (an Event). Iron
// Eater: 1: 956 (-> 2), else 957 (an Event). qm5: 3: 11 (-> 4). Completed: Engelhart 165 (an Event, replaceDefault).
// Kept as the Lua has it: 120 begins on any option; Engelhart answers nil at a Prog past 6, qm5 at any Prog but 3; 164
// completes with no check of its result (a gil reward cannot fail).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class FiresOfDiscontent : QuestScript
    {
        public FiresOfDiscontent() : base(QuestLog.CrystalWar, "FIRES_OF_DISCONTENT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_BAS_2_Fires_of_Discontent";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3, 4, 5, 6);

            b.Reward(new QuestReward
            {
                Gil = 10000,
            });

            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedQuest(QuestLog.CrystalWar, "BETTER_PART_OF_VALOR"))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Engelhart).OnTrigger(QuestReply.ProgressEvent(120))
                .OnEventFinish(Zone.BastokMarketsS, 120, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Engelhart).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress < 2)
                    {
                        return QuestReply.Event(121);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.ProgressEvent(124);
                    }
                    else if (questProgress == 3)
                    {
                        return QuestReply.Event(125);
                    }
                    else if (questProgress == 4)
                    {
                        return QuestReply.ProgressEvent(126);
                    }
                    else if (questProgress == 5)
                    {
                        return QuestReply.Event(127);
                    }
                    else if (questProgress == 6)
                    {
                        return QuestReply.ProgressEvent(164);
                    }
                    return null;
                })
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Pagdako).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(122);
                    }
                    else
                    {
                        return QuestReply.Event(123);
                    }
                })
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(q =>
                {
                    if (q.Quest.Step == 5)
                    {
                        return QuestReply.ProgressEvent(160);
                    }
                    else
                    {
                        return QuestReply.Event(161);
                    }
                })
                .OnEventFinish(Zone.BastokMarketsS, 122, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.BastokMarketsS, 124, q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.BastokMarketsS, 126, q =>
                {
                    q.Quest.SetStep(5);
                })
                .OnEventFinish(Zone.BastokMarketsS, 160, q =>
                {
                    q.Quest.SetStep(6);
                })
                .OnEventFinish(Zone.BastokMarketsS, 164, q =>
                {
                    q.Quest.Complete();
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.IronEater).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(956);
                    }
                    else
                    {
                        return QuestReply.Event(957);
                    }
                })
                .OnEventFinish(Zone.Metalworks, 956, q =>
                {
                    q.Quest.SetStep(2);
                })
                .Npc(Zone.GraubergS, Npc.GraubergS.qm5).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(11);
                    }
                    return null;
                })
                .OnEventFinish(Zone.GraubergS, 11, q =>
                {
                    q.Quest.SetStep(4);
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Engelhart).OnTrigger(QuestReply.Event(165).ReplaceDefault());
        }
    }
}
