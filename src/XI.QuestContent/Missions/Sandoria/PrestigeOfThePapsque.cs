// Prestige of the Papsque (San d'Oria 7-1, Log ID 0, Mission ID 18), a line-for-line port of
// scripts/missions/sandoria/7_1_Prestige_of_the_Papsque.lua (quest rework C3 N5). Ambrotien !pos 93.419 -0.001 -57.347
// 230; Grilau !pos -241.987 6.999 57.887 231; Endracion !pos -110 1 -34 230; Papal Chambers (_6fc) !pos 131 -11 122
// 231; qm4 !pos -695 -40 21 100. Reward: 1000 rank points. Section 1 (no San d'Oria mission current, a San d'Orian):
// the gate guards' 1009 and 2009 (Southern San d'Oria) and 1009 (Northern) finish with option 18
// (handleAcceptMission) begins the mission. Section 2 (current): Northern San d'Oria's _6fc plays 7 at status 0, 9 at
// status 1, else 8 with the Ancient San d'Orian Tablet (progress); 7's finish sets status 1 and 8's completes the
// mission and then takes the tablet. West Ronfaure: qm4 at status 1 with the Marauder Dvogzog not spawned, at the
// char's local var Mission[0][18]Stage 1, clears it, sets status 2 and gives the tablet (keyItem), else spawns and
// claims the Marauder Dvogzog and says SOMETHING_IS_AMISS; its death at status 1 sets that local var 1. Kept as the
// Lua has it: section 1 compares the nation with mission.areaId (the log id, 0, which is xi.nation.SANDORIA) and has
// no rank check and no completed check; the Stage local var is the char's own
// (player:setLocalVar('Mission[0][18]Stage')), not mission:setLocalVar; _6fc's tablet arm reads no status.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Sandoria
{
    public sealed class PrestigeOfThePapsque : MissionScript
    {
        public PrestigeOfThePapsque() : base(XI.Quests.MissionLog.Sandoria, "PRESTIGE_OF_THE_PAPSQUE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/sandoria/7_1_Prestige_of_the_Papsque";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Sandoria;
        const int MissionId = Mission.Sandoria.PrestigeOfThePapsque;

        static void HandleAcceptMission(IQuestContext q)
        {
            if (q.Option == 18)
            {
                q.Mission.Begin();
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 1000 });

            // Player has no active missions
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Sandoria) == Mission.Sandoria.None &&
                    q.Player.Nation == AreaId)
                .OnEventFinish(Zone.SouthernSanDoria, 1009, HandleAcceptMission)
                .OnEventFinish(Zone.SouthernSanDoria, 2009, HandleAcceptMission)
                .OnEventFinish(Zone.NorthernSanDoria, 1009, HandleAcceptMission);

            // Player has accepted the mission
            b.State(current: true)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria._6fc).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(7);
                    }
                    else if (missionStatus == 1)
                    {
                        return QuestReply.ProgressEvent(9);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.AncientSanDorianTablet))
                    {
                        return QuestReply.ProgressEvent(8);
                    }

                    return null;
                })
                .OnEventFinish(Zone.NorthernSanDoria, 7, q => q.Player.SetMissionStatus(AreaId, 1))
                .OnEventFinish(Zone.NorthernSanDoria, 8, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.AncientSanDorianTablet);
                    }
                })
                .Npc(Zone.WestRonfaure, Npc.WestRonfaure.qm4).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1 &&
                        !q.Player.MobIsSpawned(Mob.WestRonfaure.MARAUDER_DVOGZOG))
                    {
                        if (q.Player.LocalVar("Mission[0][18]Stage") == 1)
                        {
                            q.Player.SetLocalVar("Mission[0][18]Stage", 0);
                            q.Player.SetMissionStatus(AreaId, 2);
                            return QuestReply.KeyItem(KeyItem.AncientSanDorianTablet);
                        }
                        else
                        {
                            q.Player.SpawnMob(Mob.WestRonfaure.MARAUDER_DVOGZOG, 0, claim: true);
                            return QuestReply.MessageSpecial(Text.WestRonfaure.SOMETHING_IS_AMISS);
                        }
                    }

                    return null;
                })
                .Npc(Zone.WestRonfaure, Npc.WestRonfaure.MarauderDvogzog).OnMobDeath(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1)
                    {
                        q.Player.SetLocalVar("Mission[0][18]Stage", 1);
                    }
                });
        }
    }
}
