// The Mithra and the Crystal (Zilart 12, Log ID 3, Mission ID 23), a line-for-line port of
// scripts/missions/rotz/12_The_Mithra_and_the_Crystal.lua (quest rework C3, C6). Gilgamesh !pos 122.452 -9.009
// -12.052 252; Maryoh Comyujah !pos 0 8 73 247; qm7 !pos -504 20 -419 208; _6z0 !pos 0 -12 48 251.
// Section 1 (current): Lower Jeuno's Aldo plays 111 (0, 1, 2, 0, 0, 0, 4352, 4) while the Intro var is 0, whose finish
// sets it to 1, else 69 with the same parameters; Norg's _700 plays 5 (replacing its default) and Gilgamesh 171 with
// the Cerulean Crystal, else 170; Rabao's Maryoh Comyujah plays 82. Section 2 (current, status 0): Maryoh plays 81
// (247, 1, 0, 708, 5, 1, 279, the Option var); its finish on option 1 sets status 1, else sets Option to 1. Section 3
// (current, status 1, no Scrap of Papyrus): the Quicksand Caves' qm7 plays 13 once the nmDefeated local var is 1,
// else 12; the Ancient Vessel's death sets nmDefeated and, for the killer (or a death with no killer), the mob's
// [AncientVessel]Respawn local var to GetSystemTime() + 300. A 12 or 13 finish on option 1 says MUST_MOVE_CLOSER when
// the char stands more than 1.5 yalms from qm7. Then 12's finish: no Ancient Vessel, nothing; spawned,
// THIS_IS_NOT_THE_TIME; inside the respawn wait, FAINT_TRACES_OF_MAGIC; else SOMETHING_ATTACKING_YOU and the vessel
// spawned, claimed by the char. 13's finish gives the Scrap of Papyrus. Section 4 (current, the papyrus): Maryoh plays
// 83, whose finish takes the papyrus, gives the Cerulean Crystal and sets status 2. Section 5 (current, status 2):
// Maryoh plays 84. Section 6 (current, the crystal): the Hall of the Gods' _6z0 plays 4 and the Shimmering Circle 3,
// whose finish completes the mission (The Gate of the Gods next). Section 7 (completed, or the papyrus): qm7 says
// YOU_FIND_NOTHING with no action. Section 8 (completed): Maryoh plays 85. Every messageText here is the player's,
// with the handler's npc as the speaker, no name and mode 6. Kept as the Lua has it: the death handler's noKiller
// arm never runs (the lookup runs no handler for a death with no killer).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Zilart
{
    public sealed class TheMithraAndTheCrystal : MissionScript
    {
        public TheMithraAndTheCrystal() : base(XI.Quests.MissionLog.Zilart, "THE_MITHRA_AND_THE_CRYSTAL") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/rotz/12_The_Mithra_and_the_Crystal";

        // quicksandCavesID.mob.ANCIENT_VESSEL
        const string AncientVessel = "ANCIENT_VESSEL";
        // the vessel's local var: the time it may be spawned again
        const string RespawnVar = "[AncientVessel]Respawn";

        // a 12 or 13 finish's distance test: the char must stand within 1.5 yalms of qm7
        static bool TooFar(IQuestContext q)
        {
            if (q.Npc.PlayerDistance > 1.5)
            {
                q.Text.MessageText(Text.QuicksandCaves.MUST_MOVE_CLOSER, false, 6);

                return true;
            }

            return false;
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.Zilart,
                NextMission = Mission.Zilart.TheGateOfTheGods,
            });

            // Section: Mission Active
            b.State(current: true)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Aldo).OnTrigger(q =>
                {
                    if (q.Mission.Var("Intro") == 0)
                    {
                        return QuestReply.ProgressEvent(111, 0, 1, 2, 0, 0, 0, 4352, 4);
                    }
                    else
                    {
                        return QuestReply.Event(69, 0, 1, 2, 0, 0, 0, 4352, 4);
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 111, q => q.Mission.SetVar("Intro", 1))
                .Npc(Zone.Norg, Npc.Norg._700).OnTrigger(QuestReply.Event(5).ReplaceDefault())
                .Npc(Zone.Norg, Npc.Norg.Gilgamesh).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.CeruleanCrystal))
                    {
                        return QuestReply.Event(171);
                    }
                    else
                    {
                        return QuestReply.Event(170);
                    }
                })
                .Npc(Zone.Rabao, Npc.Rabao.MaryohComyujah).OnTrigger(QuestReply.Event(82));

            // Section: Mission Active, missionStatus == 0
            b.State(current: true, status: 0)
                .Npc(Zone.Rabao, Npc.Rabao.MaryohComyujah).OnTrigger(q =>
                {
                    int hasDeclined = q.Mission.Var("Option");

                    return QuestReply.ProgressEvent(81, 247, 1, 0, 708, 5, 1, 279, hasDeclined);
                })
                .OnEventFinish(Zone.Rabao, 81, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.SetMissionStatus(MissionLog.Zilart, 1);
                    }
                    else
                    {
                        q.Mission.SetVar("Option", 1);
                    }
                });

            // Section: Mission Active, missionStatus == 1, does not have Scrap of Papyrus
            b.State(current: true, status: 1, when: q => !q.Player.HasKeyItem(KeyItem.ScrapOfPapyrus))
                .Npc(Zone.QuicksandCaves, Npc.QuicksandCaves.qm7).OnTrigger(q =>
                {
                    if (q.Mission.LocalVar("nmDefeated") == 1)
                    {
                        return QuestReply.ProgressEvent(13);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(12);
                    }
                })
                .Npc(Zone.QuicksandCaves, Npc.QuicksandCaves.AncientVessel).OnMobDeath(q =>
                {
                    q.Mission.SetLocalVar("nmDefeated", 1);

                    if (q.MobDeath.IsKiller || q.MobDeath.NoKiller)
                    {
                        q.Mob.SetLocalVar(RespawnVar, q.Player.SystemTime + 300);
                    }
                })
                .OnEventFinish(Zone.QuicksandCaves, 12, q =>
                {
                    if (q.Option == 1)
                    {
                        if (TooFar(q))
                        {
                            return;
                        }

                        if (!q.Player.MobExists(AncientVessel))
                        {
                            return;
                        }

                        if (q.Player.MobIsSpawned(AncientVessel))
                        {
                            q.Text.MessageText(Text.QuicksandCaves.THIS_IS_NOT_THE_TIME, false, 6);
                        }
                        else if (q.Player.SystemTime < q.Player.MobLocalVar(AncientVessel, RespawnVar))
                        {
                            q.Text.MessageText(Text.QuicksandCaves.FAINT_TRACES_OF_MAGIC, false, 6);
                        }
                        else
                        {
                            q.Text.MessageText(Text.QuicksandCaves.SOMETHING_ATTACKING_YOU, false, 6);

                            q.Player.SpawnMob(AncientVessel, 0, true);
                        }
                    }
                })
                .OnEventFinish(Zone.QuicksandCaves, 13, q =>
                {
                    if (q.Option == 1)
                    {
                        if (TooFar(q))
                        {
                            return;
                        }

                        q.Player.GiveKeyItem(KeyItem.ScrapOfPapyrus);
                    }
                });

            // Section: Mission Active, has Scrap of Papyrus
            b.State(current: true, when: q => q.Player.HasKeyItem(KeyItem.ScrapOfPapyrus))
                .Npc(Zone.Rabao, Npc.Rabao.MaryohComyujah).OnTrigger(QuestReply.ProgressEvent(83))
                .OnEventFinish(Zone.Rabao, 83, q =>
                {
                    q.Player.DelKeyItem(KeyItem.ScrapOfPapyrus);
                    q.Player.GiveKeyItem(KeyItem.CeruleanCrystal);
                    q.Player.SetMissionStatus(MissionLog.Zilart, 2);
                });

            // Section: Mission Active, missionStatus == 2
            b.State(current: true, status: 2)
                .Npc(Zone.Rabao, Npc.Rabao.MaryohComyujah).OnTrigger(QuestReply.ProgressEvent(84));

            // Section: Mission Active, has Cerulean Crystal
            b.State(current: true, when: q => q.Player.HasKeyItem(KeyItem.CeruleanCrystal))
                .Npc(Zone.HallOfTheGods, Npc.HallOfTheGods._6z0).OnTrigger(QuestReply.ProgressEvent(4))
                .Npc(Zone.HallOfTheGods, Npc.HallOfTheGods.ShimmeringCircle).OnTrigger(QuestReply.ProgressEvent(3))
                .OnEventFinish(Zone.HallOfTheGods, 3, q => q.Mission.Complete());

            // Section: Mission Completed or Papyrus KI has been obtained
            b.State(current: null,
                    when: q => q.Player.HasCompletedMission(MissionLog.Zilart, Mission.Zilart.TheMithraAndTheCrystal) ||
                        q.Player.HasKeyItem(KeyItem.ScrapOfPapyrus))
                .Npc(Zone.QuicksandCaves, Npc.QuicksandCaves.qm7).OnTrigger(q =>
                {
                    q.Text.MessageText(Text.QuicksandCaves.YOU_FIND_NOTHING, false, 6);

                    return QuestReply.NoAction();
                });

            // Section: Mission Completed, Permanent Dialogue Change
            b.State(current: null,
                    when: q => q.Player.HasCompletedMission(MissionLog.Zilart, Mission.Zilart.TheMithraAndTheCrystal))
                .Npc(Zone.Rabao, Npc.Rabao.MaryohComyujah).OnTrigger(QuestReply.ProgressEvent(85));
        }
    }
}
