// The Davoi Report (San d'Oria 2-2, Log ID 0, Mission ID 4), a line-for-line port of
// scripts/missions/sandoria/2_2_The_Davoi_Report.lua (quest rework C3 N1b). !addmission 0 4. Ambrotien !pos 93.419
// -0.001 -57.347 230; Grilau !pos -241.987 6.999 57.887 231; Endracion !pos -110 1 -34 230; Zantaviat !pos 215 0.1 -10
// 149; the "!" !pos 211 2 -104 149; the Papal Chambers door (_6fc) !pos 131 -11 122 231. Section 1 (no San d'Oria
// mission current, a San d'Orian): the 1009 and 2009 finishes of Southern San d'Oria and the 1009 finish of Northern
// San d'Oria begin the mission on option 104. Section 2 (2-2 current): Zantaviat in Davoi plays 100 at status 0 (its
// finish sets status 1), else 104 when the Lost Document is held (its finish sets status 3, takes the document and
// gives the Temple Knights Davoi Report); the "!" whose x is between 210 and 212 gives the Lost Document and sets the
// status of the char's nation to 2; at status 3 Ambrotien (2006 / 2028), Endracion (1006 / 1028) and Grilau (1006 /
// 1028) play the completed-before or the first event, the first with the arguments 0, 0, 0, 44 and no finish; the
// finishes 2006, 1006 and the Papal Chambers door's 695 complete the mission (350 rank points) and take the report.
// Other statuses answer ORIGINAL_MISSION_OFFSET + 44 of the NPC's zone. Kept as the Lua has it: section 1 has no
// completed check, so a completed mission is begun again by the same finishes; the nation is compared with
// mission.areaId (the log id, 0, which is xi.nation.SANDORIA); several "!" NPCs share the one name in Davoi, told
// apart only by their x position; the door answers 695 when the mission is not completed and the report is held,
// whatever the status.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Sandoria
{
    public sealed class TheDavoiReport : MissionScript
    {
        public TheDavoiReport() : base(XI.Quests.MissionLog.Sandoria, "THE_DAVOI_REPORT") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/sandoria/2_2_The_Davoi_Report";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Sandoria;
        const int MissionId = Mission.Sandoria.TheDavoiReport;

        // handleAcceptMission: option 104 begins the mission
        static void HandleAcceptMission(IQuestContext q)
        {
            if (q.Option == 104)
            {
                q.Mission.Begin();
            }
        }

        // the report events' finish: complete, then the report taken
        static void HandleCompleteEvent(IQuestContext q)
        {
            if (q.Mission.Complete())
            {
                q.Player.DelKeyItem(KeyItem.TempleKnightsDavoiReport);
            }
        }

        // the three report NPCs: status 3 plays the completed-before or the first event, else the text
        static QuestReply HandleReportTrigger(IQuestContext q, int repeatId, int firstId, ZoneText offset)
        {
            if (q.Player.MissionStatus(AreaId) == 3)
            {
                if (q.Player.HasCompletedMission(AreaId, MissionId))
                {
                    return QuestReply.ProgressEvent(repeatId);
                }
                else
                {
                    return QuestReply.ProgressEvent(firstId, 0, 0, 0, 44);
                }
            }
            else
            {
                return QuestReply.MessageText(q.Text.Id(offset) + 44);
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 350 });

            // Player has no active missions
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Sandoria) == Mission.Sandoria.None &&
                    q.Player.Nation == AreaId)
                .OnEventFinish(Zone.SouthernSanDoria, 1009, HandleAcceptMission)
                .OnEventFinish(Zone.SouthernSanDoria, 2009, HandleAcceptMission)
                .OnEventFinish(Zone.NorthernSanDoria, 1009, HandleAcceptMission);

            b.State(current: true)
                .Npc(Zone.Davoi, Npc.Davoi.Zantaviat).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 0)
                    {
                        return QuestReply.ProgressEvent(100);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.LostDocument))
                    {
                        return QuestReply.ProgressEvent(104);
                    }

                    return null;
                })
                // TODO (Lua): rename this NPC to be a non-special character, and make this NPC unique
                .Npc(Zone.Davoi, "!").OnTrigger(q =>
                {
                    double xPos = q.Npc.XPos;

                    if (!q.Player.HasKeyItem(KeyItem.LostDocument) && xPos > 210 && xPos < 212)
                    {
                        q.Player.SetMissionStatus(q.Player.Nation, 2);
                        return QuestReply.KeyItem(KeyItem.LostDocument);
                    }

                    return null;
                })
                .OnEventFinish(Zone.Davoi, 100, q => q.Player.SetMissionStatus(AreaId, 1))
                .OnEventFinish(Zone.Davoi, 104, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 3);
                    q.Player.DelKeyItem(KeyItem.LostDocument);
                    q.Player.GiveKeyItem(KeyItem.TempleKnightsDavoiReport);
                })
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Ambrotien)
                .OnTrigger(q => HandleReportTrigger(q, 2006, 2028, Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Endracion)
                .OnTrigger(q => HandleReportTrigger(q, 1006, 1028, Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET))
                .OnEventFinish(Zone.SouthernSanDoria, 1006, HandleCompleteEvent)
                .OnEventFinish(Zone.SouthernSanDoria, 2006, HandleCompleteEvent)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria._6fc).OnTrigger(q =>
                {
                    if (!q.Player.HasCompletedMission(AreaId, MissionId) &&
                        q.Player.HasKeyItem(KeyItem.TempleKnightsDavoiReport))
                    {
                        return QuestReply.ProgressEvent(695);
                    }

                    return null;
                })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Grilau)
                .OnTrigger(q => HandleReportTrigger(q, 1006, 1028, Text.NorthernSanDoria.ORIGINAL_MISSION_OFFSET))
                .OnEventFinish(Zone.NorthernSanDoria, 695, HandleCompleteEvent)
                .OnEventFinish(Zone.NorthernSanDoria, 1006, HandleCompleteEvent);
        }
    }
}
