// Perils of the Griffon (Log ID 7, Quest ID 37), a line-for-line port of
// scripts/quests/crystalWar/WOTG_SAN_5_Perils_of_the_Griffon.lua (quest rework B3b Crystal War, draft). Southern San
// d'Oria [S]: Rholont !pos -168 -2 56 (default 32), Daigraffeaux !pos -7.7 2 -89.2 (default 620). Jugner Forest [S]:
// qm6 !pos 68 -0.5 324 (default YOU_FIND_NOTHING_ORDINARY).
// Available, Wrath of the Griffon and the WotG mission Purple, the New Black completed (Prog is set while available):
// Rholont: Prog 0: the Timer (Wrath's 60 sets it to tomorrow) at or before today and the mustZone clear (Wrath's 60
// sets it too): 629 (-> Prog 1); else 49 (an Event); Prog 1: the mustZone clear: 630 (-> Prog 2); else 634 (an Event); Prog
// 2: 635 (an Event); Prog 3: 632, whose finish begins it. Daigraffeaux: Prog 2: 631 (-> Prog 3). Accepted: Rholont: no
// Orcish Warmachine Body: 636 (an Event); else 633, whose finish completes it (an Elixir, Knight of the Swiftwing
// Griffin). qm6: no body: 210, whose finish gives it.
// Kept as the Lua has it: 632 begins on any option; the body is never taken (the completed section is none, so qm6 /
// Rholont stop answering); Prog 1's mustZone is Wrath's (630 never sets one); 633 completes with no check but the
// reward's fit.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class PerilsOfTheGriffon : QuestScript
    {
        public PerilsOfTheGriffon() : base(QuestLog.CrystalWar, "PERILS_OF_THE_GRIFFON") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_SAN_5_Perils_of_the_Griffon";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3);

            b.Reward(new QuestReward
            {
                Item = Item.Elixir,
                Title = Title.KnightOfTheSwiftwingGriffin,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.CrystalWar, "WRATH_OF_THE_GRIFFON") &&
                    q.Player.HasCompletedMission(MissionLog.Wotg, Mission.Wotg.PurpleTheNewBlack))
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Rholont).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        if (q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay &&
                            !q.Quest.MustZone)
                        {
                            return QuestReply.ProgressEvent(629);
                        }
                        else
                        {
                            return QuestReply.Event(49);
                        }
                    }
                    else if (questProgress == 1)
                    {
                        if (!q.Quest.MustZone)
                        {
                            return QuestReply.ProgressEvent(630);
                        }
                        else
                        {
                            return QuestReply.Event(634);
                        }
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.Event(635);
                    }
                    else if (questProgress == 3)
                    {
                        return QuestReply.ProgressEvent(632);
                    }
                    return null;
                })
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Daigraffeaux).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressEvent(631);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 629, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 630, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 631, q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 632, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Rholont).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.OrcishWarmachineBody))
                    {
                        return QuestReply.Event(636);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(633);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 633, q =>
                {
                    q.Quest.Complete();
                })
                .Npc(Zone.JugnerForestS, Npc.JugnerForestS.qm6).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.OrcishWarmachineBody))
                    {
                        return QuestReply.ProgressEvent(210);
                    }
                    return null;
                })
                .OnEventFinish(Zone.JugnerForestS, 210, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.OrcishWarmachineBody);
                });
        }
    }
}
