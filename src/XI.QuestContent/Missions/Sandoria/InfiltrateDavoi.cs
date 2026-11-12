// Infiltrate Davoi (San d'Oria 3-1, Log ID 0, Mission ID 10), a line-for-line port of
// scripts/missions/sandoria/3_1_Infiltrate_Davoi.lua (quest rework C3 N3). Ambrotien !pos 93.419 -0.001 -57.347 230;
// Grilau !pos -241.987 6.999 57.887 231; Endracion !pos -110 1 -34 230; Door: Prince Royal's !pos -38 -3 73 233;
// Quemaricond !pos 23 0.1 -23 149; Zantaviat !pos 215 0.1 -10 149; '!' East Block Code !pos 294 0 -28 149; '!' South
// Block Code !pos 335.5 0 -136 149; '!' North Block Code !pos 163 0 -18 149. Reward: 300 rank points (400 the first
// time: 554's finish adds 100). Section 1 (no San d'Oria mission current, a San d'Orian): the 1009 and 2009 finishes of
// Southern San d'Oria and the 1009 finish of Northern San d'Oria begin the mission on option 110 (no message). Section
// 2 (current, not completed before): Grilau and Endracion at status 0 play 1029, Ambrotien 2029 (progress); at 4 each
// says its zone's ORIGINAL_MISSION_OFFSET + 55 (priority 1000). Chateau d'Oraguille: the Prince Royal's door _6h0 at 4
// plays 554, at 0 553 (progress, the Royal Knights' Davoi Report in slot 1); 553's finish sets status 2; 554's
// completes the mission and, when that succeeds, takes the report and adds 100 rank points. Davoi: Quemaricond at 3
// stops his path and plays 117 (progress); the zone-in at 2 plays 116, whose finish sets status 3; 117's finish walks
// the npc on, sets status 4 and gives the report. Section 3 (current, completed before: a repeat): Grilau and Endracion
// at status 0 say their zone's ORIGINAL_MISSION_OFFSET + 60 (priority 1000), at 10 play 1012, Ambrotien 2012
// (progress); the 1012 finishes (both zones) and 2012 complete the mission. Davoi: a '!' at status 6 to 9 gives the
// East, South or North Block Code (key item reply, priority 1000) when its position is in that code's 4-yalm box and
// the char lacks the code, adding one to the status of the char's nation's log; Zantaviat at 0 plays 102, at 9 105
// (progress); 102's finish sets status 6; 105's sets status 10 and takes the three codes. Kept as the Lua has it:
// section 1 has no completed check and compares the nation with mission.areaId (the log id, 0, which is
// xi.nation.SANDORIA); the '!' sets the status through player:getNation(), not mission.areaId; several '!' NPCs share
// the one name in Davoi, told apart only by their position.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Sandoria
{
    public sealed class InfiltrateDavoi : MissionScript
    {
        public InfiltrateDavoi() : base(XI.Quests.MissionLog.Sandoria, "INFILTRATE_DAVOI") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/sandoria/3_1_Infiltrate_Davoi";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Sandoria;
        const int MissionId = Mission.Sandoria.InfiltrateDavoi;

        // handleAcceptMission: option 110 begins the mission
        static void HandleAcceptMission(IQuestContext q)
        {
            if (q.Option == 110)
            {
                q.Mission.Begin();
            }
        }

        // the first run's mission NPCs: status 0 the given event, 4 ORIGINAL_MISSION_OFFSET + 55
        static QuestReply FirstRunTrigger(IQuestContext q, int csid, ZoneText offset)
        {
            int missionStatus = q.Player.MissionStatus(AreaId);

            if (missionStatus == 0)
            {
                return QuestReply.ProgressEvent(csid);
            }
            else if (missionStatus == 4)
            {
                return QuestReply.MessageText(q.Text.Id(offset) + 55).SetPriority(1000);
            }

            return null;
        }

        // the repeat's mission NPCs: status 0 ORIGINAL_MISSION_OFFSET + 60, 10 the given event
        static QuestReply RepeatTrigger(IQuestContext q, int csid, ZoneText offset)
        {
            int missionStatus = q.Player.MissionStatus(AreaId);

            if (missionStatus == 0)
            {
                return QuestReply.MessageText(q.Text.Id(offset) + 60).SetPriority(1000);
            }
            else if (missionStatus == 10)
            {
                return QuestReply.ProgressEvent(csid);
            }

            return null;
        }

        protected override void Build(MissionScriptBuilder b)
        {
            // Rank Points: 400 First, 300 Repeat
            b.Reward(new MissionReward { RankPoints = 300 });

            // Player has no active missions
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Sandoria) == Mission.Sandoria.None &&
                    q.Player.Nation == AreaId)
                .OnEventFinish(Zone.SouthernSanDoria, 1009, HandleAcceptMission)
                .OnEventFinish(Zone.SouthernSanDoria, 2009, HandleAcceptMission)
                .OnEventFinish(Zone.NorthernSanDoria, 1009, HandleAcceptMission);

            // First time the player has accepted this mission
            b.State(current: true, when: q => !q.Player.HasCompletedMission(AreaId, MissionId))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Grilau)
                .OnTrigger(q => FirstRunTrigger(q, 1029, Text.NorthernSanDoria.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Ambrotien)
                .OnTrigger(q => FirstRunTrigger(q, 2029, Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Endracion)
                .OnTrigger(q => FirstRunTrigger(q, 1029, Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille._6h0).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 4)
                    {
                        return QuestReply.ProgressEvent(554, 0, KeyItem.RoyalKnightsDavoiReport);
                    }
                    else if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(553, 0, KeyItem.RoyalKnightsDavoiReport);
                    }

                    return null;
                })
                .OnEventFinish(Zone.ChateauDoraguille, 553, q => q.Player.SetMissionStatus(AreaId, 2))
                .OnEventFinish(Zone.ChateauDoraguille, 554, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.RoyalKnightsDavoiReport);
                        q.Player.AddRankPoints(100);
                    }
                })
                .Npc(Zone.Davoi, Npc.Davoi.Quemaricond).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 3)
                    {
                        q.Npc.ClearPath(true);
                        return QuestReply.ProgressEvent(117);
                    }

                    return null;
                })
                .OnZoneIn(Zone.Davoi, q =>
                    q.Player.MissionStatus(AreaId) == 2 ? QuestReply.Event(116) : null)
                .OnEventFinish(Zone.Davoi, 116, q => q.Player.SetMissionStatus(AreaId, 3))
                .OnEventFinish(Zone.Davoi, 117, q =>
                {
                    q.Npc.ContinuePath();
                    q.Player.SetMissionStatus(AreaId, 4);
                    q.Player.GiveKeyItem(KeyItem.RoyalKnightsDavoiReport);
                });

            // Player is repeating this mission
            b.State(current: true, when: q => q.Player.HasCompletedMission(AreaId, MissionId))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Grilau)
                .OnTrigger(q => RepeatTrigger(q, 1012, Text.NorthernSanDoria.ORIGINAL_MISSION_OFFSET))
                .OnEventFinish(Zone.NorthernSanDoria, 1012, q => q.Mission.Complete())
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Ambrotien)
                .OnTrigger(q => RepeatTrigger(q, 2012, Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Endracion)
                .OnTrigger(q => RepeatTrigger(q, 1012, Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET))
                .OnEventFinish(Zone.SouthernSanDoria, 1012, q => q.Mission.Complete())
                .OnEventFinish(Zone.SouthernSanDoria, 2012, q => q.Mission.Complete())
                // TODO (Lua): Rename this NPC to be a non-special character, also separate these duplicate NPCs
                .Npc(Zone.Davoi, "!").OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus >= 6 && missionStatus <= 9)
                    {
                        double xPos = q.Npc.XPos;
                        double zPos = q.Npc.ZPos;

                        if (xPos >= 292 &&
                            xPos <= 296 &&
                            zPos >= -30 &&
                            zPos <= -26 &&
                            !q.Player.HasKeyItem(KeyItem.EastBlockCode))
                        {
                            q.Player.SetMissionStatus(q.Player.Nation, missionStatus + 1);
                            return QuestReply.KeyItem(KeyItem.EastBlockCode).SetPriority(1000);
                        }
                        else if (xPos >= 333 &&
                                 xPos <= 337 &&
                                 zPos >= -138 &&
                                 zPos <= -134 &&
                                 !q.Player.HasKeyItem(KeyItem.SouthBlockCode))
                        {
                            q.Player.SetMissionStatus(q.Player.Nation, missionStatus + 1);
                            return QuestReply.KeyItem(KeyItem.SouthBlockCode).SetPriority(1000);
                        }
                        else if (xPos >= 161 &&
                                 xPos <= 165 &&
                                 zPos >= -20 &&
                                 zPos <= -16 &&
                                 !q.Player.HasKeyItem(KeyItem.NorthBlockCode))
                        {
                            q.Player.SetMissionStatus(q.Player.Nation, missionStatus + 1);
                            return QuestReply.KeyItem(KeyItem.NorthBlockCode).SetPriority(1000);
                        }
                    }

                    return null;
                })
                .Npc(Zone.Davoi, Npc.Davoi.Zantaviat).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(102);
                    }
                    else if (missionStatus == 9)
                    {
                        return QuestReply.ProgressEvent(105);
                    }

                    return null;
                })
                .OnEventFinish(Zone.Davoi, 102, q => q.Player.SetMissionStatus(AreaId, 6))
                .OnEventFinish(Zone.Davoi, 105, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 10);
                    q.Player.DelKeyItem(KeyItem.EastBlockCode);
                    q.Player.DelKeyItem(KeyItem.SouthBlockCode);
                    q.Player.DelKeyItem(KeyItem.NorthBlockCode);
                });
        }
    }
}
