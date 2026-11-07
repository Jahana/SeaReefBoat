// Apocalypse Nigh (Log ID 3, Quest ID 89), a line-for-line port of scripts/quests/jeuno/Apocalypse_Nigh.lua (quest rework
// B3b). _iya : !pos -20 0.1 -283 34; Aldo : !pos 20 3 -58 245; Gilgamesh : !pos 122.452 -9.009 -12.052 252.
// Available once Shadows of the Departed is completed, the day past the Timer it set and its mustZone cleared: Ru'Lude
// Gardens' trigger area 1 plays 123 (begun). Accepted: Sealion's Den's zone-in at Prog 0 plays 29 (update option 0:
// updateEvent(0, 0, 0, 0, 32, 3, 0, 0); finish: Prog 1); the Grand Palace of Hu'Xzoi's _iya at Prog 1: 4 (Prog 2, set down
// in The Garden of Ru'Hmet); Empyreal Paradox's TR_Entrance at Prog 2: 4 (Prog 3); the Apocalypse Nigh battlefield's
// 32001 at Prog 3 with battlefieldWin 1057: Prog 4, Timer = VanadielUniqueDay() + 1; Empyreal Paradox's zone-in at Prog
// 4: 7 (update option 0: updateEvent(36, 1430550, 2964, 1013, 36, 1, 0, 0); finish: Breaker of the Chains, set down in
// Al'Taieu); Lower Jeuno's Aldo at Prog 4 with rank 5 or more: 10057 (Prog 5), at Prog 5: 10058; Norg's door _700 at
// Prog 5 before the Timer: 235 (the mustZone set); Gilgamesh from the Timer with the mustZone clear: 232 (252, 6) at Prog
// 5, 234 (252) at Prog 6, update option 99: the four earrings, finish option 1-4: the earring (Static + option - 1) and
// completed, else Prog 6. Completed: Empyreal Paradox's qm1 with none of the four earrings held: 5 (option 1 rewinds
// CoP / Zilart to Dawn / Awakening (Zilart status 3, Dawn's Status 8) and deletes Shadows of the Departed and this
// quest); Gilgamesh's 233 (replacing his default).
// Kept Lua quirks: the earring finish completes only when the earring is given (a full bag: nothing, Prog kept); the
// four 1-4 options are read before the Prog; the qm1 returns nil while any earring is held; the reward table is empty
// (quest:complete always succeeds). The 232 / 234 finishes are also Dawn's and Awakening's (their own containers).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class ApocalypseNigh : QuestScript
    {
        public ApocalypseNigh() : base(QuestLog.Jeuno, "APOCALYPSE_NIGH") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Apocalypse_Nigh";

        // Norg's Oaken Door: its npcs.yaml script name _700 has no letter, so gen_content_ids gives it no Npc constant
        // (skip_entity); the handler names it by the string, as the Lua does
        const string NorgDoor700 = "_700";

        static void RewardOnEventUpdate(IQuestContext q)
        {
            if (q.Option == 99)
            {
                q.Player.UpdateEvent(252,
                    Item.StaticEarring,
                    Item.MagneticEarring,
                    Item.HollowEarring,
                    Item.EtherealEarring);
            }
        }

        // NOTE: This event is also captured in both Dawn and Awakening's mission scripts, and
        // the order may not trigger after this function.  While the missions are guaranteed to
        // complete here, the quest may not if inventory is full, but it will not errantly complete.
        // Both missions require their variables to be cleared, and is why they are handled there.
        static void RewardOnEventFinish(IQuestContext q)
        {
            if (q.Option >= 1 &&
                q.Option <= 4)
            {
                int rewardItem = Item.StaticEarring + q.Option - 1;

                if (q.Player.GiveItem(rewardItem))
                {
                    q.Quest.Complete();
                }
            }
            else
            {
                q.Quest.SetStep(6);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Jeuno, "SHADOWS_OF_THE_DEPARTED") &&
                    q.Player.VanadielUniqueDay >= q.Quest.Var("Timer") &&
                    !q.Quest.MustZone)
                .OnTriggerAreaEnter(Zone.RuludeGardens, 1, q =>
                {
                    return QuestReply.ProgressEvent(123);
                })
                .OnEventFinish(Zone.RuludeGardens, 123, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .OnZoneIn(Zone.SealionsDen, q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.Event(29);
                    }
                    return null;
                })
                .OnEventUpdate(Zone.SealionsDen, 29, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.UpdateEvent(0, 0, 0, 0, 32, 3, 0, 0);
                    }
                })
                .OnEventFinish(Zone.SealionsDen, 29, q =>
                {
                    q.Quest.SetStep(1);
                })
                .Npc(Zone.GrandPalaceOfHuxzoi, Npc.GrandPalaceOfHuxzoi.iya).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(4);
                    }
                    return null;
                })
                .OnEventFinish(Zone.GrandPalaceOfHuxzoi, 4, q =>
                {
                    q.Quest.SetStep(2);
                    q.Player.SetPos(-419.995, 0, 248.483, 191, 35);
                })
                .OnZoneIn(Zone.EmpyrealParadox, q =>
                {
                    if (q.Quest.Step == 4)
                    {
                        return QuestReply.Event(7);
                    }
                    return null;
                })
                .Npc(Zone.EmpyrealParadox, Npc.EmpyrealParadox.TREntrance).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressEvent(4);
                    }
                    return null;
                })
                .OnEventUpdate(Zone.EmpyrealParadox, 7, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.UpdateEvent(36, 1430550, 2964, 1013, 36, 1, 0, 0);
                    }
                })
                .OnEventFinish(Zone.EmpyrealParadox, 4, q =>
                {
                    q.Quest.SetStep(3);
                })
                // The title comes with this cutscene, not the battlefield win.
                .OnEventFinish(Zone.EmpyrealParadox, 7, q =>
                {
                    q.Player.AddTitle(Title.BreakerOfTheChains);
                    q.Player.SetPos(-.0745, -10, -465.1132, 63, Zone.Altaieu);
                })
                .OnEventFinish(Zone.EmpyrealParadox, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.ApocalypseNigh &&
                        q.Quest.Step == 3)
                    {
                        q.Quest.SetStep(4);
                        q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay + 1);
                    }
                })
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Aldo).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 4 &&
                        q.Player.Rank(q.Player.Nation) >= 5)
                    {
                        return QuestReply.ProgressEvent(10057);
                    }
                    else if (questProgress == 5)
                    {
                        return QuestReply.Event(10058);
                    }
                    return null;
                })
                .OnEventFinish(Zone.LowerJeuno, 10057, q =>
                {
                    q.Quest.SetStep(5);
                })
                .Npc(Zone.Norg, NorgDoor700).OnTrigger(q =>
                {
                    if (q.Quest.Step == 5 &&
                        q.Quest.Var("Timer") > q.Player.VanadielUniqueDay)
                    {
                        return QuestReply.ProgressEvent(235);
                    }
                    return null;
                })
                .Npc(Zone.Norg, Npc.Norg.Gilgamesh).OnTrigger(q =>
                {
                    if (q.Player.VanadielUniqueDay >= q.Quest.Var("Timer") &&
                        !q.Quest.MustZone)
                    {
                        int questProgress = q.Quest.Step;

                        if (questProgress == 5)
                        {
                            return QuestReply.ProgressEvent(232, 252, 6);
                        }
                        else if (questProgress == 6)
                        {
                            return QuestReply.ProgressEvent(234, 252);
                        }
                    }
                    return null;
                })
                .OnEventUpdate(Zone.Norg, 232, RewardOnEventUpdate)
                .OnEventUpdate(Zone.Norg, 234, RewardOnEventUpdate)
                .OnEventFinish(Zone.Norg, 232, RewardOnEventFinish)
                .OnEventFinish(Zone.Norg, 234, RewardOnEventFinish)
                .OnEventFinish(Zone.Norg, 235, q =>
                {
                    q.Quest.SetMustZone();
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.EmpyrealParadox, Npc.EmpyrealParadox.qm1).OnTrigger(q =>
                {
                    for (int itemId = Item.StaticEarring; itemId <= Item.StaticEarring + 3; itemId++)
                    {
                        if (q.Player.HasItem(itemId))
                        {
                            return null;
                        }
                    }

                    return QuestReply.ProgressEvent(5);
                })
                .OnEventFinish(Zone.EmpyrealParadox, 5, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.DelMission(MissionLog.Cop, Mission.Cop.TheLastVerse);
                        q.Player.DelMission(MissionLog.Zilart, Mission.Zilart.TheLastVerse);

                        q.Player.AddMission(MissionLog.Cop, Mission.Cop.Dawn);
                        q.Player.AddMission(MissionLog.Zilart, Mission.Zilart.Awakening);

                        q.Player.SetMissionStatus(MissionLog.Zilart, 3);
                        q.Player.SetMissionVar(MissionLog.Cop, Mission.Cop.Dawn, "Status", 8);

                        q.Player.DelQuest(QuestLog.Jeuno, "SHADOWS_OF_THE_DEPARTED");
                        q.Player.DelQuest(QuestLog.Jeuno, "APOCALYPSE_NIGH");
                    }
                })
                .Npc(Zone.Norg, Npc.Norg.Gilgamesh).OnTrigger(QuestReply.Event(233).ReplaceDefault());
        }
    }
}
