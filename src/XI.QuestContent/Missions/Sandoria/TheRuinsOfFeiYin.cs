// The Ruins of Fei'Yin (San d'Oria 5-1, Log ID 0, Mission ID 14), a line-for-line port of
// scripts/missions/sandoria/5_1_The_Ruins_of_FeiYin.lua (quest rework C3 N4). Ambrotien !pos 93.419 -0.001 -57.347
// 230; Grilau !pos -241.987 6.999 57.887 231; Endracion !pos -110 1 -34 230; Halver !pos 2 0.1 0.1 233; _6h0 !pos -38
// -3 73 233; Curilla !pos 27 0.1 0.1 233; Rahal !pos -28 0.1 -6 233. Reward: 400 rank points. Section 1 (no San d'Oria
// mission current, a San d'Orian of rank 5 who has not completed it): at status 0 Ambrotien plays 62, Endracion 61
// and Grilau 52 (progress); the gate guards' 1009 and 2009 (Southern San d'Oria) and 1009 (Northern) finish with
// option 14 (handleAcceptMission) begins the mission, sets status 9, takes the Message to Jeuno and says the zone's
// YOU_ACCEPT_THE_MISSION. Halver says SHALL_BEGIN_DISCUSSION. Chateau d'Oraguille's zone-in from Northern San d'Oria at
// status 0 plays 509, whose finish sets status 8. Section 2 (current): Halver at status 9 plays 533 (progress), at 10
// says FEI_YIN_NORTHEAST (messageText), at 12 with the Burnt Seal plays 534 (progress). 533's finish gives the New
// Fei'Yin Seal and sets status 10; 534's completes the mission and then takes the Burnt Seal. Fei'Yin's zone-in at
// status 10 plays 1, whose finish sets status 11. Qu'Bia Arena's 32001 finish at status 11 with battlefieldWin
// RANK_5_MISSION (512) gives the Burnt Seal, sets status 12 and takes the New Fei'Yin Seal. Section 3 (completed, The
// Shadow Lord not current, rank 5): _6h0 115, Curilla 545, Rahal 544 (events) and Halver TIME_TO_REACH_DECISION
// (messageText). Kept as the Lua has it: section 1 compares the nation with mission.areaId (the log id, 0, which is
// xi.nation.SANDORIA); section 3 reads the current mission only against The Shadow Lord, and its rank check reads
// getRank(mission.areaId), a log id passed as a nation (the same equivalence).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Sandoria
{
    public sealed class TheRuinsOfFeiYin : MissionScript
    {
        public TheRuinsOfFeiYin() : base(XI.Quests.MissionLog.Sandoria, "THE_RUINS_OF_FEI_YIN") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/sandoria/5_1_The_Ruins_of_FeiYin";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Sandoria;
        const int MissionId = Mission.Sandoria.TheRuinsOfFeiYin;

        // The message KI is removed here to still preserve this mission should a player decline and then choose to
        // change nations. By retaining it until this point, we'll still have a point of reference to return to should
        // missionStatus be reset.
        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 14)
            {
                q.Mission.Begin();
                q.Player.SetMissionStatus(AreaId, 9);
                q.Player.DelKeyItem(KeyItem.MessageToJeunoSandoria);
                q.Text.Special(accepted);
            }
        }

        // a gate guard at status 0: its event (progress)
        static QuestReply GuardTrigger(IQuestContext q, int csid) =>
            q.Player.MissionStatus(AreaId) == 0 ? QuestReply.ProgressEvent(csid) : null;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 400 });

            // Player meets requirements, but has not started the mission
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Sandoria) == Mission.Sandoria.None &&
                    q.Player.Nation == AreaId &&
                    q.Player.Rank(q.Player.Nation) == 5 &&
                    !q.Player.HasCompletedMission(AreaId, MissionId))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Ambrotien).OnTrigger(q => GuardTrigger(q, 62))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Endracion).OnTrigger(q => GuardTrigger(q, 61))
                .OnEventFinish(Zone.SouthernSanDoria, 1009, q =>
                    HandleAcceptMission(q, Text.SouthernSanDoria.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.SouthernSanDoria, 2009, q =>
                    HandleAcceptMission(q, Text.SouthernSanDoria.YOU_ACCEPT_THE_MISSION))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Grilau).OnTrigger(q => GuardTrigger(q, 52))
                .OnEventFinish(Zone.NorthernSanDoria, 1009, q =>
                    HandleAcceptMission(q, Text.NorthernSanDoria.YOU_ACCEPT_THE_MISSION))
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Halver).OnTrigger(q =>
                    QuestReply.MessageSpecial(Text.ChateauDoraguille.SHALL_BEGIN_DISCUSSION))
                .OnZoneIn(Zone.ChateauDoraguille, q =>
                    q.Player.PreviousZone == Zone.NorthernSanDoria &&
                    q.Player.MissionStatus(AreaId) == 0
                        ? QuestReply.Event(509)
                        : null)
                .OnEventFinish(Zone.ChateauDoraguille, 509, q => q.Player.SetMissionStatus(AreaId, 8));

            // Mission has been accepted
            b.State(current: true)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Halver).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 9)
                    {
                        return QuestReply.ProgressEvent(533);
                    }
                    else if (missionStatus == 10)
                    {
                        return QuestReply.MessageText(Text.ChateauDoraguille.FEI_YIN_NORTHEAST);
                    }
                    else if (missionStatus == 12 && q.Player.HasKeyItem(KeyItem.BurntSeal))
                    {
                        return QuestReply.ProgressEvent(534);
                    }

                    return null;
                })
                .OnEventFinish(Zone.ChateauDoraguille, 533, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.NewFeiyinSeal);
                    q.Player.SetMissionStatus(AreaId, 10);
                })
                .OnEventFinish(Zone.ChateauDoraguille, 534, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.BurntSeal);
                    }
                })
                .OnZoneIn(Zone.Feiyin, q =>
                    q.Player.MissionStatus(AreaId) == 10 ? QuestReply.Event(1) : null)
                .OnEventFinish(Zone.Feiyin, 1, q => q.Player.SetMissionStatus(AreaId, 11))
                .OnEventFinish(Zone.QubiaArena, 32001, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 11 &&
                        q.Player.LocalVar("battlefieldWin") == Battlefield.Rank5Mission)
                    {
                        q.Player.GiveKeyItem(KeyItem.BurntSeal);
                        q.Player.SetMissionStatus(AreaId, 12);
                        q.Player.DelKeyItem(KeyItem.NewFeiyinSeal);
                    }
                });

            // Optional Dialogue after Completion
            b.State(current: null, when: q =>
                    q.Player.HasCompletedMission(AreaId, MissionId) &&
                    q.Player.CurrentMission(MissionLog.Sandoria) != Mission.Sandoria.TheShadowLord &&
                    q.Player.Rank(AreaId) == 5)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille._6h0).OnTrigger(QuestReply.Event(115))
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Curilla).OnTrigger(QuestReply.Event(545))
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Halver)
                .OnTrigger(QuestReply.MessageText(Text.ChateauDoraguille.TIME_TO_REACH_DECISION))
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Rahal).OnTrigger(QuestReply.Event(544));
        }
    }
}
