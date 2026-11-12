// Appointment to Jeuno (San d'Oria 3-3, Log ID 0, Mission ID 12), a line-for-line port of
// scripts/missions/sandoria/3_3_Appointment_to_Jeuno.lua (quest rework C3 N3). Ambrotien !pos 93.419 -0.001 -57.347
// 230; Grilau !pos -241.987 6.999 57.887 231; Endracion !pos -110 1 -34 230; Halver !pos 2 0.1 0.1 233; _6h4 (Great
// Hall) !pos 0 -1 13 233; Nelcabrit !pos -32 9 -49 243; _541 (Cermet Door) !pos 636 16 20 184. Reward: 5000 gil and
// rank 4. Section 1 (no San d'Oria mission current, a San d'Orian): the 1009 and 2009 finishes of Southern San d'Oria
// and the 1009 finish of Northern San d'Oria begin the mission on option 12 (no message). Section 2 (current): Grilau,
// Ambrotien and Endracion at status 0 say their zone's ORIGINAL_MISSION_OFFSET + 74 (priority 1000). Chateau
// d'Oraguille: Arsha and Chupaile at 2 play 85 and 86, Halver at 0 508, the Great Hall door _6h4 at 2 537 (progress);
// 508's finish sets status 2; 537's sets status 3 and gives the Letter to the Ambassador. Ru'Lude Gardens: Nelcabrit at
// 3 plays 42, at 4 67, at 5 140 (progress); the door _6r5 at 5 plays 39 (progress), whose finish completes the mission;
// 42's finish sets status 4 and takes the letter. Lower Delkfutt's Tower: the Cermet Door _541 at status 4 takes a
// trade of exactly one Delkfutt Key with cutscene 0 (progress); its trigger at 4 plays 0 with the Delkfutt Key key
// item, else THE_DOOR_IS_FIRMLY_SHUT_OPEN_KEY (priority 1000). 0's finish sets status 5 and, without the key item,
// gives the key item and takes the trade. Kept as the Lua has it: section 1 has no completed check and compares the
// nation with mission.areaId (the log id, 0, which is xi.nation.SANDORIA).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Sandoria
{
    public sealed class AppointmentToJeuno : MissionScript
    {
        public AppointmentToJeuno() : base(XI.Quests.MissionLog.Sandoria, "APPOINTMENT_TO_JEUNO") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/sandoria/3_3_Appointment_to_Jeuno";

        // mission.areaId
        const int AreaId = MissionLog.Sandoria;

        // handleAcceptMission: option 12 begins the mission
        static void HandleAcceptMission(IQuestContext q)
        {
            if (q.Option == 12)
            {
                q.Mission.Begin();
            }
        }

        // the mission NPCs: status 0 ORIGINAL_MISSION_OFFSET + 74
        static QuestReply OfferNote(IQuestContext q, ZoneText offset) =>
            q.Player.MissionStatus(AreaId) == 0
                ? QuestReply.MessageText(q.Text.Id(offset) + 74).SetPriority(1000)
                : null;

        // an event at one status
        static QuestReply AtStatus(IQuestContext q, int status, int csid) =>
            q.Player.MissionStatus(AreaId) == status ? QuestReply.ProgressEvent(csid) : null;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { Gil = 5000, Rank = 4 });

            // Player has no active missions
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Sandoria) == Mission.Sandoria.None &&
                    q.Player.Nation == AreaId)
                .OnEventFinish(Zone.SouthernSanDoria, 1009, HandleAcceptMission)
                .OnEventFinish(Zone.SouthernSanDoria, 2009, HandleAcceptMission)
                .OnEventFinish(Zone.NorthernSanDoria, 1009, HandleAcceptMission);

            // Player has accepted the mission
            b.State(current: true)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Grilau)
                .OnTrigger(q => OfferNote(q, Text.NorthernSanDoria.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Ambrotien)
                .OnTrigger(q => OfferNote(q, Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Endracion)
                .OnTrigger(q => OfferNote(q, Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Arsha).OnTrigger(q => AtStatus(q, 2, 85))
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Chupaile).OnTrigger(q => AtStatus(q, 2, 86))
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Halver).OnTrigger(q => AtStatus(q, 0, 508))
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille._6h4).OnTrigger(q => AtStatus(q, 2, 537))
                // 7092 Here: You have been granted an audience with the king
                .OnEventFinish(Zone.ChateauDoraguille, 508, q => q.Player.SetMissionStatus(AreaId, 2))
                .OnEventFinish(Zone.ChateauDoraguille, 537, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 3);
                    q.Player.GiveKeyItem(KeyItem.LetterToTheAmbassador);
                })
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Nelcabrit).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 3)
                    {
                        return QuestReply.ProgressEvent(42);
                    }
                    else if (missionStatus == 4)
                    {
                        return QuestReply.ProgressEvent(67);
                    }
                    else if (missionStatus == 5)
                    {
                        return QuestReply.ProgressEvent(140);
                    }

                    return null;
                })
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens._6r5).OnTrigger(q => AtStatus(q, 5, 39))
                .OnEventFinish(Zone.RuludeGardens, 39, q => q.Mission.Complete())
                .OnEventFinish(Zone.RuludeGardens, 42, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 4);
                    q.Player.DelKeyItem(KeyItem.LetterToTheAmbassador);
                })
                .Npc(Zone.LowerDelkfuttsTower, Npc.LowerDelkfuttsTower._541)
                .OnTrade(q =>
                {
                    // TODO: As of the April 2009 update, trading the key to any Cermet Door or the elevator will turn
                    // the key into a key item, allowing the player to drop the inventory key for space.
                    if (q.Player.MissionStatus(AreaId) == 4 &&
                        q.Trade.Matches((Item.DelkfuttKey, 1)))
                    {
                        return QuestReply.ProgressCutscene(0);
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 4)
                    {
                        if (q.Player.HasKeyItem(KeyItem.DelkfuttKey))
                        {
                            return QuestReply.ProgressCutscene(0);
                        }
                        else
                        {
                            return QuestReply.MessageSpecial(Text.LowerDelkfuttsTower.THE_DOOR_IS_FIRMLY_SHUT_OPEN_KEY)
                                .SetPriority(1000);
                        }
                    }

                    return null;
                })
                .OnEventFinish(Zone.LowerDelkfuttsTower, 0, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 5);

                    if (!q.Player.HasKeyItem(KeyItem.DelkfuttKey))
                    {
                        q.Player.GiveKeyItem(KeyItem.DelkfuttKey);
                        q.Trade.Complete();
                    }
                });
        }
    }
}
