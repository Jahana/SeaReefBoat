// The Rescue Drill (San d'Oria 2-1, Log ID 0, Mission ID 3), a line-for-line port of
// scripts/missions/sandoria/2_1_The_Rescue_Drill.lua (quest rework C3 N1b). !addmission 0 3. Ambrotien !pos 93.419
// -0.001 -57.347 230; Grilau !pos -241.987 6.999 57.887 231; Endracion !pos -110 1 -34 230; La Theine Plateau (102):
// Augevinne !pos -361 39 266, Deaufrain -304 28 339, Equesobillot -287 9 284, Galaihaurat -482 -7 222, Laurisse -292
// 28 143, Narvecaint -263 22 129, Vicorpasse -344 37 266, Yaucevouchat -318 39 183; Ruillont !pos -70 1 607 193.
// Section 1 (no San d'Oria mission current, a San d'Orian): the 1009 and 2009 finishes of Southern San d'Oria and the
// 1009 finish of Northern San d'Oria begin the mission on option 3. Section 2 (2-1 current), by mission status: 2
// Equesobillot 101 (finish sets 3), 3 Deaufrain 102 (sets 4), 4 Vicorpasse 108 (sets 5), 5 Laurisse 106 (sets 6), 6
// Narvecaint 107 (sets 7), 0 Galaihaurat 110 (sets 2); 7 sends the char to Ruillont in Ordelles Caves, whose event 1
// (status 2 to 7) sets the Option var to a random 1 to 3 and status 8; at status 8 the one of Deaufrain (Option 3),
// Equesobillot (Option 2) and Galaihaurat (Option 1) that Option names plays 113, 112 or 114, whose finish gives a
// Bronze Sword and sets status 9; the sword traded to Ruillont at status 9 plays 2, whose finish sets status 10 and
// takes it; Vicorpasse at status 10 plays 115, whose finish gives the Rescue Training Certificate and sets status 11;
// Ambrotien, Endracion (1005 / 2005) and Grilau (1005) at status 11 play the final event, whose finish completes the
// mission (300 rank points) and takes the certificate. The other statuses answer texts: Augevinne, Laurisse,
// Narvecaint, Vicorpasse and Yaucevouchat their RESCUE_DRILL ones. Augevinne (103 at status 5 to 7) and Yaucevouchat
// (104 at 5 to 7) have an event with no finish. Section 3 (2-1 completed, whatever else is current): Galaihaurat plays
// RESCUE_DRILL + 39 in place of his default. Kept as the Lua has it: section 1 does not check that 2-1 is not
// completed; the nation is compared with mission.areaId (the log id, 0, which is xi.nation.SANDORIA); Equesobillot
// reads the status with player:getNation() as the log; Ruillont's trigger answers nothing at status 0 and 1 unless
// 2-1 is completed (then RUILLONT_INITIAL_DIALOG + 9), and the completed check there is before status 8's; the
// Bronze Sword events set status 9 only when the sword fits (npcUtil.giveItem); Option is read raw (an unset var is 0).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Sandoria
{
    public sealed class TheRescueDrill : MissionScript
    {
        public TheRescueDrill() : base(XI.Quests.MissionLog.Sandoria, "THE_RESCUE_DRILL") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/sandoria/2_1_The_Rescue_Drill";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Sandoria;
        const int MissionId = Mission.Sandoria.TheRescueDrill;

        // handleAcceptMission: option 3 begins the mission
        static void HandleAcceptMission(IQuestContext q)
        {
            if (q.Option == 3)
            {
                q.Mission.Begin();
            }
        }

        // the final event's finish: complete, then the certificate taken
        static void HandleCompleteEvent(IQuestContext q)
        {
            if (q.Mission.Complete())
            {
                q.Player.DelKeyItem(KeyItem.RescueTrainingCertificate);
            }
        }

        // player:setMissionStatus(mission.areaId, status)
        static void SetStatus(IQuestContext q, int status)
        {
            q.Player.SetMissionStatus(AreaId, status);
        }

        // [112] / [113] / [114]: the Bronze Sword, then status 9 when it fits
        static void HandleGiveSword(IQuestContext q)
        {
            if (q.Player.GiveItem(Item.BronzeSword))
            {
                q.Player.SetMissionStatus(AreaId, 9);
            }
        }

        // the gate NPCs: the final event at status 11, else ORIGINAL_MISSION_OFFSET + 36
        static QuestReply HandleGateTrigger(IQuestContext q, int finalId, ZoneText offset)
        {
            if (q.Player.MissionStatus(AreaId) == 11)
            {
                return QuestReply.ProgressEvent(finalId);
            }
            else
            {
                return QuestReply.MessageText(q.Text.Id(offset) + 36);
            }
        }

        // mission:messageText(laTheinePlateauID.text.RESCUE_DRILL + offset)
        static QuestReply Drill(IQuestContext q, int offset) =>
            QuestReply.MessageText(q.Text.Id(Text.LaTheinePlateau.RESCUE_DRILL) + offset);

        // mission:messageText(ordellesCavesID.text.RUILLONT_INITIAL_DIALOG + offset)
        static QuestReply RuillontText(IQuestContext q, int offset) =>
            QuestReply.MessageText(q.Text.Id(Text.OrdellesCaves.RUILLONT_INITIAL_DIALOG) + offset);

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 300 });

            // Player has no active missions
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Sandoria) == Mission.Sandoria.None &&
                    q.Player.Nation == AreaId)
                .OnEventFinish(Zone.SouthernSanDoria, 1009, HandleAcceptMission)
                .OnEventFinish(Zone.SouthernSanDoria, 2009, HandleAcceptMission)
                .OnEventFinish(Zone.NorthernSanDoria, 1009, HandleAcceptMission);

            // Initially attempted to split this section up based on missionStatus.  This proved readable at first,
            // but then got very messy as time went on.  Would suggest revisiting this in the future for a potential
            // refactor.
            b.State(current: true)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Ambrotien)
                .OnTrigger(q => HandleGateTrigger(q, 2005, Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Endracion)
                .OnTrigger(q => HandleGateTrigger(q, 1005, Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET))
                .OnEventFinish(Zone.SouthernSanDoria, 1005, HandleCompleteEvent)
                .OnEventFinish(Zone.SouthernSanDoria, 2005, HandleCompleteEvent)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Grilau)
                .OnTrigger(q => HandleGateTrigger(q, 1005, Text.NorthernSanDoria.ORIGINAL_MISSION_OFFSET))
                .OnEventFinish(Zone.NorthernSanDoria, 1005, HandleCompleteEvent)
                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.Augevinne).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus >= 5 && missionStatus <= 7)
                    {
                        return QuestReply.ProgressEvent(103);
                    }
                    else if (missionStatus == 8)
                    {
                        return Drill(q, 21);
                    }
                    else if (missionStatus >= 9)
                    {
                        return Drill(q, 26);
                    }
                    else
                    {
                        return Drill(q, 6);
                    }
                })
                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.Deaufrain).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 3)
                    {
                        return QuestReply.ProgressEvent(102);
                    }
                    else if (missionStatus == 4)
                    {
                        return Drill(q, 4);
                    }
                    else if (missionStatus == 8)
                    {
                        if (q.Mission.Var("Option") == 3)
                        {
                            return QuestReply.ProgressEvent(113);
                        }
                        else
                        {
                            return Drill(q, 21);
                        }
                    }
                    else if (missionStatus == 9)
                    {
                        if (q.Mission.Var("Option") == 3)
                        {
                            return Drill(q, 25);
                        }
                        else
                        {
                            return Drill(q, 26);
                        }
                    }
                    else if (missionStatus >= 10)
                    {
                        return Drill(q, 30);
                    }
                    else
                    {
                        return Drill(q, 0);
                    }
                })
                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.Equesobillot).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(q.Player.Nation);

                    if (missionStatus == 2)
                    {
                        return QuestReply.ProgressEvent(101);
                    }
                    else if (missionStatus == 3)
                    {
                        return Drill(q, 3);
                    }
                    else if (missionStatus == 8)
                    {
                        if (q.Mission.Var("Option") == 2)
                        {
                            return QuestReply.ProgressEvent(112);
                        }
                        else
                        {
                            return Drill(q, 21);
                        }
                    }
                    else if (missionStatus == 9)
                    {
                        if (q.Mission.Var("Option") == 2)
                        {
                            return Drill(q, 25);
                        }
                        else
                        {
                            return Drill(q, 26);
                        }
                    }
                    else if (missionStatus >= 10)
                    {
                        return Drill(q, 30);
                    }
                    else
                    {
                        return Drill(q, 0);
                    }
                })
                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.Galaihaurat).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(110);
                    }
                    else if (missionStatus == 2)
                    {
                        return Drill(q, 16);
                    }
                    else if (missionStatus == 8)
                    {
                        if (q.Mission.Var("Option") == 1)
                        {
                            return QuestReply.ProgressEvent(114);
                        }
                        else
                        {
                            return Drill(q, 21);
                        }
                    }
                    else if (missionStatus == 9)
                    {
                        if (q.Mission.Var("Option") == 1)
                        {
                            return Drill(q, 25);
                        }
                        else
                        {
                            return Drill(q, 26);
                        }
                    }
                    else if (missionStatus >= 10)
                    {
                        return Drill(q, 30);
                    }
                    else
                    {
                        return Drill(q, 0);
                    }
                })
                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.Laurisse).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 5)
                    {
                        return QuestReply.ProgressEvent(106);
                    }
                    else if (missionStatus >= 6 && missionStatus <= 7)
                    {
                        return QuestReply.ProgressEvent(109);
                    }
                    else if (missionStatus == 8)
                    {
                        return Drill(q, 21);
                    }
                    else if (missionStatus >= 9)
                    {
                        return Drill(q, 26);
                    }
                    else
                    {
                        return Drill(q, 0);
                    }
                })
                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.Narvecaint).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 6)
                    {
                        return QuestReply.ProgressEvent(107);
                    }
                    else if (missionStatus == 7)
                    {
                        return Drill(q, 14);
                    }
                    else if (missionStatus == 8)
                    {
                        return Drill(q, 21);
                    }
                    else if (missionStatus >= 9)
                    {
                        return Drill(q, 26);
                    }
                    else
                    {
                        return Drill(q, 0);
                    }
                })
                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.Vicorpasse).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 4)
                    {
                        return QuestReply.ProgressEvent(108);
                    }
                    else if (missionStatus >= 5 && missionStatus <= 7)
                    {
                        return Drill(q, 19);
                    }
                    else if (missionStatus == 8)
                    {
                        return Drill(q, 21);
                    }
                    else if (missionStatus == 9)
                    {
                        return Drill(q, 26);
                    }
                    else if (missionStatus == 10)
                    {
                        return QuestReply.ProgressEvent(115);
                    }
                    else if (missionStatus == 11)
                    {
                        return QuestReply.MessageText(q.Text.Id(Text.LaTheinePlateau.RESCUE_DRILL) + 29,
                                                      KeyItem.RescueTrainingCertificate);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(5);
                    }
                })
                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.Yaucevouchat).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus >= 5 && missionStatus <= 7)
                    {
                        return QuestReply.ProgressEvent(104);
                    }
                    else if (missionStatus == 8)
                    {
                        return Drill(q, 21);
                    }
                    else if (missionStatus >= 9)
                    {
                        return Drill(q, 26);
                    }
                    else
                    {
                        return Drill(q, 7);
                    }
                })
                .OnEventFinish(Zone.LaTheinePlateau, 101, q => SetStatus(q, 3))
                .OnEventFinish(Zone.LaTheinePlateau, 102, q => SetStatus(q, 4))
                .OnEventFinish(Zone.LaTheinePlateau, 106, q => SetStatus(q, 6))
                .OnEventFinish(Zone.LaTheinePlateau, 107, q => SetStatus(q, 7))
                .OnEventFinish(Zone.LaTheinePlateau, 108, q => SetStatus(q, 5))
                .OnEventFinish(Zone.LaTheinePlateau, 110, q => SetStatus(q, 2))
                .OnEventFinish(Zone.LaTheinePlateau, 112, HandleGiveSword)
                .OnEventFinish(Zone.LaTheinePlateau, 113, HandleGiveSword)
                .OnEventFinish(Zone.LaTheinePlateau, 114, HandleGiveSword)
                .OnEventFinish(Zone.LaTheinePlateau, 115, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.RescueTrainingCertificate);
                    q.Player.SetMissionStatus(AreaId, 11);
                })
                .Npc(Zone.OrdellesCaves, Npc.OrdellesCaves.Ruillont)
                .OnTrade(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 9 && q.Trade.Matches((Item.BronzeSword, 1)))
                    {
                        return QuestReply.ProgressEvent(2);
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus >= 2 && missionStatus <= 7)
                    {
                        return QuestReply.ProgressEvent(1);
                    }
                    else if (missionStatus >= 10 || q.Player.HasCompletedMission(AreaId, MissionId))
                    {
                        return RuillontText(q, 9);
                    }
                    else if (missionStatus >= 8)
                    {
                        return RuillontText(q, 0);
                    }

                    return null;
                })
                .OnEventFinish(Zone.OrdellesCaves, 1, q =>
                {
                    q.Mission.SetVar("Option", q.Player.RandomInt(1, 3));
                    q.Player.SetMissionStatus(AreaId, 8);
                })
                .OnEventFinish(Zone.OrdellesCaves, 2, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 10);
                    q.Trade.Complete();
                });

            // Player has completed Mission
            b.State(current: null, when: q => q.Player.HasCompletedMission(AreaId, MissionId))
                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.Galaihaurat)
                .OnTrigger(q => Drill(q, 39).ReplaceDefault());
        }
    }
}
