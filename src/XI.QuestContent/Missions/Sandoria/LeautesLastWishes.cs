// Leaute's Last Wishes (San d'Oria 6-1, Log ID 0, Mission ID 16), a line-for-line port of
// scripts/missions/sandoria/6_1_Leautes_Last_Wishes.lua (quest rework C3 N5). Ambrotien !pos 93.419 -0.001 -57.347
// 230; Grilau !pos -241.987 6.999 57.887 231; Endracion !pos -110 1 -34 230; Halver !pos 2 0.1 0.1 233; Door: Great
// Hall !pos 0 -1 13 233; Chalvatot !pos -105 0.1 72 233; Dreamrose !pos -262.403 -10.155 49.164 125. Reward: 600 rank
// points and the Piece of Paper. Section 1 (no San d'Oria mission current, a San d'Orian): the gate guards' 1009 and
// 2009 (Southern San d'Oria) and 1009 (Northern) finish with option 16 (handleAcceptMission) begins the mission.
// Section 2 (current): Arsha and Chupaile at status 1 play 85 and 86 (progress); Halver plays 25 at status 0, 23 at
// 1, 24 at 2 and 22 at 3 (progress); _6h4 at status 1 plays 87 (progress); trigger area 2 at status 4 with the
// Dreamrose plays 111 (progress). 22's finish sets status 4, 25's status 1, 87's status 2 and 111's completes the
// mission and then takes the Dreamrose. Western Altepa Desert: the Dreamrose at status 2, with the Sabotender
// Enamorado not spawned or dead, at Progress 1 clears Progress, sets status 3 and gives the Dreamrose (keyItem), else
// spawns and claims the Sabotender Enamorado and says FEEL_SOMETHING_PRICKLY; its death at status 2 sets Progress 1.
// Kept as the Lua has it: section 1 compares the nation with mission.areaId (the log id, 0, which is
// xi.nation.SANDORIA) and has no rank check and no completed check; the Dreamrose reads the Sabotender Enamorado with
// no nil guard.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Sandoria
{
    public sealed class LeautesLastWishes : MissionScript
    {
        public LeautesLastWishes() : base(XI.Quests.MissionLog.Sandoria, "LEAUTES_LAST_WISHES") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/sandoria/6_1_Leautes_Last_Wishes";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Sandoria;
        const int MissionId = Mission.Sandoria.LeautesLastWishes;

        static void HandleAcceptMission(IQuestContext q)
        {
            if (q.Option == 16)
            {
                q.Mission.Begin();
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 600, KeyItem = KeyItem.PieceOfPaper });

            // Player has no active missions
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Sandoria) == Mission.Sandoria.None &&
                    q.Player.Nation == AreaId)
                .OnEventFinish(Zone.SouthernSanDoria, 1009, HandleAcceptMission)
                .OnEventFinish(Zone.SouthernSanDoria, 2009, HandleAcceptMission)
                .OnEventFinish(Zone.NorthernSanDoria, 1009, HandleAcceptMission);

            // Player has accepted the mission
            b.State(current: true)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Arsha).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1)
                    {
                        return QuestReply.ProgressEvent(85);
                    }

                    return null;
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Chupaile).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1)
                    {
                        return QuestReply.ProgressEvent(86);
                    }

                    return null;
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Halver).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(25);
                    }
                    else if (missionStatus == 1)
                    {
                        return QuestReply.ProgressEvent(23);
                    }
                    else if (missionStatus == 2)
                    {
                        return QuestReply.ProgressEvent(24);
                    }
                    else if (missionStatus == 3)
                    {
                        return QuestReply.ProgressEvent(22);
                    }

                    return null;
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille._6h4).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1)
                    {
                        return QuestReply.ProgressEvent(87);
                    }

                    return null;
                })
                .OnTriggerAreaEnter(Zone.ChateauDoraguille, 2, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 4 &&
                        q.Player.HasKeyItem(KeyItem.Dreamrose))
                    {
                        return QuestReply.ProgressEvent(111);
                    }

                    return null;
                })
                .OnEventFinish(Zone.ChateauDoraguille, 22, q => q.Player.SetMissionStatus(AreaId, 4))
                .OnEventFinish(Zone.ChateauDoraguille, 25, q => q.Player.SetMissionStatus(AreaId, 1))
                .OnEventFinish(Zone.ChateauDoraguille, 87, q => q.Player.SetMissionStatus(AreaId, 2))
                .OnEventFinish(Zone.ChateauDoraguille, 111, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.Dreamrose);
                    }
                })
                .Npc(Zone.WesternAltepaDesert, Npc.WesternAltepaDesert.Dreamrose).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 2 &&
                        (!q.Player.MobIsSpawned(Mob.WesternAltepaDesert.SABOTENDER_ENAMORADO) ||
                         q.Player.MobIsDead(Mob.WesternAltepaDesert.SABOTENDER_ENAMORADO)))
                    {
                        if (q.Mission.Var("Progress") == 1)
                        {
                            q.Mission.SetVar("Progress", 0);
                            q.Player.SetMissionStatus(AreaId, 3);
                            return QuestReply.KeyItem(KeyItem.Dreamrose);
                        }
                        else
                        {
                            q.Player.SpawnMob(Mob.WesternAltepaDesert.SABOTENDER_ENAMORADO, 0, claim: true);
                            return QuestReply.MessageSpecial(Text.WesternAltepaDesert.FEEL_SOMETHING_PRICKLY);
                        }
                    }

                    return null;
                })
                .Npc(Zone.WesternAltepaDesert, Npc.WesternAltepaDesert.SabotenderEnamorado).OnMobDeath(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 2)
                    {
                        q.Mission.SetVar("Progress", 1);
                    }
                });
        }
    }
}
