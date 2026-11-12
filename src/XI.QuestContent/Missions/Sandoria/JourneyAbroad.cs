// Journey Abroad (San d'Oria 2-3, Log ID 0, Mission ID 5), a line-for-line port of
// scripts/missions/sandoria/2_3_0_Journey_Abroad.lua (quest rework C3 N2). Ambrotien !pos 93.419 -0.001 -57.347 230;
// Grilau !pos -241.987 6.999 57.887 231; Endracion !pos -110 1 -34 230; Halver !pos 2 0.1 0.1 233; Mourices !pos
// -50.646 -0.501 -27.642 241; Savae E Paleade !pos 23.724 -17.39 -43.360 237. Section 1 (no mission current, a San
// d'Orian): the 1009 finish in Southern and Northern San d'Oria and the 2009 finish in Southern, on option 5, begin
// the mission (no message). Section 2 (2-3 current): Ambrotien, Endracion and Grilau play their zone's
// ORIGINAL_MISSION_OFFSET + 50 at status 0, else + 51 (priority 1000). Chateau d'Oraguille: Halver at status 11 plays
// 507 (progress; its finish completes the mission and takes the Kindred Report), at 0 505 (progress; the eighth
// parameter 7 is 1 when ENABLE_TRUST_QUESTS is 1 and the char has neither Halver's spell nor his cipher; its finish
// moves to status 2, gives the Letter to the Consuls and, under the same trust condition, the cipher), else 532
// (progress). Metalworks: Chantain, Lutia and Riault at status 7 play 216, 213, 210 and at 11 218, 215, 212
// (progress); Savae E Paleade at 2 plays 204 (its finish ends this mission, begins Journey to Bastok, moves to status
// 3 and takes the letter), at 7 206 (its finish ends this mission, begins Journey to Bastok 2 and moves to status 8),
// at 11 209. Windurst Woods: Catalia, Erpolant and Forine with Journey to Windurst completed and status 7 play 459,
// 460, 461; Mourices at 2 plays 448 (its finish ends this mission, begins Journey to Windurst, moves to status 3 and
// takes the letter), at 6 462 (its finish ends this mission, begins Journey to Windurst 2 and moves to status 7), with
// Journey to Windurst completed at 7 458, at 11 468. The status reads are the mission's log (mission.areaId, 0).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Sandoria
{
    public sealed class JourneyAbroad : MissionScript
    {
        public JourneyAbroad() : base(XI.Quests.MissionLog.Sandoria, "JOURNEY_ABROAD") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/sandoria/2_3_0_Journey_Abroad";

        // handleAcceptMission: option 5 begins the mission
        static void HandleAcceptMission(IQuestContext q)
        {
            if (q.Option == 5)
            {
                q.Mission.Begin();
            }
        }

        // ENABLE_TRUST_QUESTS is 1 and the char has neither Halver's spell nor his cipher
        static bool NeedsHalverTrust(IQuestContext q) =>
            q.Settings.MainEquals("ENABLE_TRUST_QUESTS", 1) &&
            !q.Player.HasSpell(Spell.Halver) &&
            !q.Player.FindItem(Item.CipherOfHalversAlterEgo);

        // the mission list NPCs: status 0 ORIGINAL_MISSION_OFFSET + 50, else + 51
        static QuestReply OfferNote(IQuestContext q, ZoneText offset) =>
            q.Player.MissionStatus(MissionLog.Sandoria) == 0
                ? QuestReply.MessageText(q.Text.Id(offset) + 50).SetPriority(1000)
                : QuestReply.MessageText(q.Text.Id(offset) + 51).SetPriority(1000);

        // Windurst Woods' three: Journey to Windurst completed and status 7
        static bool WindurstHeadStart(IQuestContext q) =>
            q.Player.HasCompletedMission(MissionLog.Sandoria, Mission.Sandoria.JourneyToWindurst) &&
            q.Player.MissionStatus(MissionLog.Sandoria) == 7;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Gil = 3000,
                KeyItem = KeyItem.AdventurersCertificate,
                Rank = 3,
                Title = Title.CertifiedAdventurer,
            });

            // Player has no active missions
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Sandoria) == Mission.Sandoria.None &&
                    q.Player.Nation == MissionLog.Sandoria)
                .OnEventFinish(Zone.SouthernSanDoria, 1009, HandleAcceptMission)
                .OnEventFinish(Zone.SouthernSanDoria, 2009, HandleAcceptMission)
                .OnEventFinish(Zone.NorthernSanDoria, 1009, HandleAcceptMission);

            b.State(current: true)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Ambrotien)
                .OnTrigger(q => OfferNote(q, Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Endracion)
                .OnTrigger(q => OfferNote(q, Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Grilau)
                .OnTrigger(q => OfferNote(q, Text.NorthernSanDoria.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Halver).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Sandoria);

                    if (missionStatus == 11)
                    {
                        return QuestReply.ProgressEvent(507);
                    }
                    else if (missionStatus == 0)
                    {
                        if (q.Settings.MainEquals("ENABLE_TRUST_QUESTS", 1))
                        {
                            int needsHalverTrust = NeedsHalverTrust(q) ? 1 : 0;

                            return QuestReply.ProgressEvent(505).Option(7, needsHalverTrust);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(505);
                        }
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(532);
                    }
                })
                .OnEventFinish(Zone.ChateauDoraguille, 505, q =>
                {
                    q.Player.SetMissionStatus(MissionLog.Sandoria, 2);
                    q.Player.GiveKeyItem(KeyItem.LetterToTheConsulsSandoria);

                    if (NeedsHalverTrust(q))
                    {
                        q.Player.GiveItem(Item.CipherOfHalversAlterEgo);
                    }
                })
                .OnEventFinish(Zone.ChateauDoraguille, 507, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.KindredReport);
                    }
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Chantain).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Sandoria);

                    if (missionStatus == 7)
                    {
                        return QuestReply.ProgressEvent(216);
                    }
                    else if (missionStatus == 11)
                    {
                        return QuestReply.ProgressEvent(218);
                    }

                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Lutia).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Sandoria);

                    if (missionStatus == 7)
                    {
                        return QuestReply.ProgressEvent(213);
                    }
                    else if (missionStatus == 11)
                    {
                        return QuestReply.ProgressEvent(215);
                    }

                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Riault).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Sandoria);

                    if (missionStatus == 7)
                    {
                        return QuestReply.ProgressEvent(210);
                    }
                    else if (missionStatus == 11)
                    {
                        return QuestReply.ProgressEvent(212);
                    }

                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.SavaeEPaleade).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Sandoria);

                    // Part I - Bastok > Windurst
                    if (missionStatus == 2)
                    {
                        return QuestReply.ProgressEvent(204);
                    }
                    // Part II - Windurst > Bastok
                    else if (missionStatus == 7)
                    {
                        return QuestReply.ProgressEvent(206);
                    }
                    else if (missionStatus == 11)
                    {
                        return QuestReply.ProgressEvent(209);
                    }

                    return null;
                })
                .OnEventFinish(Zone.Metalworks, 204, q =>
                {
                    q.Player.DelMission(MissionLog.Sandoria, Mission.Sandoria.JourneyAbroad);
                    q.Player.AddMission(MissionLog.Sandoria, Mission.Sandoria.JourneyToBastok);
                    q.Player.SetMissionStatus(MissionLog.Sandoria, 3);
                    q.Player.DelKeyItem(KeyItem.LetterToTheConsulsSandoria);
                })
                .OnEventFinish(Zone.Metalworks, 206, q =>
                {
                    q.Player.DelMission(MissionLog.Sandoria, Mission.Sandoria.JourneyAbroad);
                    q.Player.AddMission(MissionLog.Sandoria, Mission.Sandoria.JourneyToBastok2);
                    q.Player.SetMissionStatus(MissionLog.Sandoria, 8);
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Catalia).OnTrigger(q =>
                {
                    if (WindurstHeadStart(q))
                    {
                        return QuestReply.ProgressEvent(459);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Erpolant).OnTrigger(q =>
                {
                    if (WindurstHeadStart(q))
                    {
                        return QuestReply.ProgressEvent(460);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Forine).OnTrigger(q =>
                {
                    if (WindurstHeadStart(q))
                    {
                        return QuestReply.ProgressEvent(461);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Mourices).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Sandoria);

                    // Part I - Windurst > Bastok
                    if (missionStatus == 2)
                    {
                        return QuestReply.ProgressEvent(448);
                    }
                    // Part II - Bastok > Windurst
                    else if (missionStatus == 6)
                    {
                        return QuestReply.ProgressEvent(462);
                    }
                    else if (q.Player.HasCompletedMission(MissionLog.Sandoria, Mission.Sandoria.JourneyToWindurst) &&
                             missionStatus == 7)
                    {
                        return QuestReply.ProgressEvent(458); // Head to Bastok
                    }
                    else if (missionStatus == 11)
                    {
                        return QuestReply.ProgressEvent(468);
                    }

                    return null;
                })
                .OnEventFinish(Zone.WindurstWoods, 448, q =>
                {
                    q.Player.DelMission(MissionLog.Sandoria, Mission.Sandoria.JourneyAbroad);
                    q.Player.AddMission(MissionLog.Sandoria, Mission.Sandoria.JourneyToWindurst);
                    q.Player.SetMissionStatus(MissionLog.Sandoria, 3);
                    q.Player.DelKeyItem(KeyItem.LetterToTheConsulsSandoria);
                })
                .OnEventFinish(Zone.WindurstWoods, 462, q =>
                {
                    q.Player.DelMission(MissionLog.Sandoria, Mission.Sandoria.JourneyAbroad);
                    q.Player.AddMission(MissionLog.Sandoria, Mission.Sandoria.JourneyToWindurst2);
                    q.Player.SetMissionStatus(MissionLog.Sandoria, 7);
                });
        }
    }
}
