// Claws of the Griffon (Log ID 7, Quest ID 16), a line-for-line port of
// scripts/quests/crystalWar/WOTG_SAN_2_Claws_of_the_Griffon.lua (quest rework B3b Crystal War group 6). Southern San
// d'Oria [S]: Rholont !pos -168 -2 56 (default 32). Jugner Forest [S]: qm6 !pos 68 -0.5 324 (default
// YOU_FIND_NOTHING_ORDINARY), Fingerfilcher Dradzad (FINGERFILCHER_DRADZAD). Available, Gifts of the Griffon
// completed: Rholont: the Timer (Gifts' completion sets it to the next day) at or before today: 47, whose finish
// begins it and sets the quest's mustZone; else nil. Accepted: Rholont: the mustZone clear and Prog 0: 48 (-> Prog
// 1); else Option 0: 50 (-> Option 1); else 49 (an Event). The zone-in to Jugner Forest [S] from East Ronfaure [S] at
// Prog 1: 200 (-> Prog 2). qm6: Prog 2: 201 (-> 3); Prog 3 with the Fingerfilcher not spawned: 202, whose finish
// spawns it claimed by the char; Prog 4: 203, whose finish completes it (an Angelstone). The Fingerfilcher's death at
// Prog 3: Prog 4.
// Kept as the Lua has it: 47 begins on any option; Rholont's 50 / 49 answer the Prog 0 talk with the mustZone set
// too; qm6 answers nil at Prog 0 / 1, at Prog 3 with the NM up and at 5+; 203 completes with no check but the
// reward's fit; no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class ClawsOfTheGriffon : QuestScript
    {
        public ClawsOfTheGriffon() : base(QuestLog.CrystalWar, "CLAWS_OF_THE_GRIFFON") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_SAN_2_Claws_of_the_Griffon";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3, 4);

            b.Reward(new QuestReward
            {
                Item = Item.Angelstone,
            });

            b.State(QuestStatus.Available,
                    when: q => q.Player.HasCompletedQuest(QuestLog.CrystalWar, "GIFTS_OF_THE_GRIFFON"))
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Rholont).OnTrigger(q =>
                {
                    if (q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay)
                    {
                        return QuestReply.ProgressEvent(47);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 47, q =>
                {
                    q.Quest.Begin();
                    q.Quest.SetMustZone();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Rholont).OnTrigger(q =>
                {
                    if (!q.Quest.MustZone &&
                        q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(48);
                    }
                    else
                    {
                        int questOption = q.Quest.Var("Option");

                        if (questOption == 0)
                        {
                            return QuestReply.ProgressEvent(50);
                        }
                        else
                        {
                            return QuestReply.Event(49);
                        }
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 48, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 50, q =>
                {
                    q.Quest.SetVar("Option", 1);
                })
                .Npc(Zone.JugnerForestS, Npc.JugnerForestS.qm6).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 2)
                    {
                        return QuestReply.ProgressEvent(201);
                    }
                    else if (questProgress == 3 &&
                        !q.Player.MobIsSpawned(Mob.JugnerForestS.FINGERFILCHER_DRADZAD))
                    {
                        return QuestReply.ProgressEvent(202);
                    }
                    else if (questProgress == 4)
                    {
                        return QuestReply.ProgressEvent(203);
                    }
                    return null;
                })
                .Npc(Zone.JugnerForestS, Npc.JugnerForestS.FingerfilcherDradzad).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        q.Quest.SetStep(4);
                    }
                })
                .OnZoneIn(Zone.JugnerForestS, q =>
                {
                    if (q.Player.PreviousZone == Zone.EastRonfaureS &&
                        q.Quest.Step == 1)
                    {
                        return QuestReply.Event(200);
                    }
                    return null;
                })
                .OnEventFinish(Zone.JugnerForestS, 200, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.JugnerForestS, 201, q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.JugnerForestS, 202, q =>
                {
                    q.Player.SpawnMob(Mob.JugnerForestS.FINGERFILCHER_DRADZAD, 0, true);
                })
                .OnEventFinish(Zone.JugnerForestS, 203, q =>
                {
                    q.Quest.Complete();
                });
        }
    }
}
