// Smash the Orcish Scouts (San d'Oria 1-1, Log ID 0, Mission ID 0), a line-for-line port of
// scripts/missions/sandoria/1_1_Smash_the_Orcish_Scouts.lua (quest rework C3 N1b). It handles both the first
// completion and repeats (!addmission 0 0). Ambrotien !pos 93.419 -0.001 -57.347 230; Grilau !pos -241.987 6.999
// 57.887 231; Endracion !pos -110 1 -34 230; Orcish Axe is item 16656. Section 1 (no San d'Oria mission current, a
// San d'Orian who has not completed 1-1): Ambrotien plays 2000, Endracion and Grilau 1000; the 1000 and 2000 finishes
// on option 0 begin the mission and send YOU_ACCEPT_THE_MISSION of the char's zone. Section 2 (the same, 1-1
// completed): only the 1009 finishes (Southern San d'Oria's 1009 and 2009, Northern's 1009) are registered, they begin
// the mission on option 0 the same way; the NPCs' own scripts play 1009. Section 3 (1-1 current): each NPC plays its
// zone's ORIGINAL_MISSION_OFFSET; the Orcish Axe traded plays 2020 / 1020 the first time and 2002 / 1002 once the
// mission is completed; any of those finishes completes the mission (150 rank points) and takes the axe only when the
// completion succeeded. Kept as the Lua has it: the nation is compared with mission.areaId (the log id, 0, which is
// xi.nation.SANDORIA); the 1000 finish of Southern San d'Oria is Endracion's and the 2000 finish Ambrotien's, each
// the same closure.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Sandoria
{
    public sealed class SmashTheOrcishScouts : MissionScript
    {
        public SmashTheOrcishScouts() : base(XI.Quests.MissionLog.Sandoria, "SMASH_THE_ORCISH_SCOUTS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/sandoria/1_1_Smash_the_Orcish_Scouts";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Sandoria;
        const int MissionId = Mission.Sandoria.SmashTheOrcishScouts;

        // handleTradeEvent: the one Orcish Axe: the first event the first time, the repeat event after
        static QuestReply HandleTradeEvent(IQuestContext q, int firstId, int repeatId)
        {
            if (q.Trade.Matches((Item.OrcishAxe, 1)))
            {
                if (!q.Player.HasCompletedMission(AreaId, MissionId))
                {
                    return QuestReply.ProgressEvent(firstId);
                }
                else
                {
                    return QuestReply.ProgressEvent(repeatId);
                }
            }

            return null;
        }

        // handleAcceptMission: option 0 begins the mission, with the char's zone's YOU_ACCEPT_THE_MISSION
        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 0)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        // handleTradeEventFinish
        static void HandleTradeEventFinish(IQuestContext q)
        {
            if (q.Mission.Complete())
            {
                q.Trade.Complete();
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 150 });

            // Player is offered this mission for the first time
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Sandoria) == Mission.Sandoria.None &&
                    q.Player.Nation == AreaId &&
                    !q.Player.HasCompletedMission(AreaId, MissionId))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Ambrotien).OnTrigger(QuestReply.ProgressEvent(2000))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Endracion).OnTrigger(QuestReply.ProgressEvent(1000))
                .OnEventFinish(Zone.SouthernSanDoria, 1000, q =>
                    HandleAcceptMission(q, Text.SouthernSanDoria.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.SouthernSanDoria, 2000, q =>
                    HandleAcceptMission(q, Text.SouthernSanDoria.YOU_ACCEPT_THE_MISSION))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Grilau).OnTrigger(QuestReply.ProgressEvent(1000))
                .OnEventFinish(Zone.NorthernSanDoria, 1000, q =>
                    HandleAcceptMission(q, Text.NorthernSanDoria.YOU_ACCEPT_THE_MISSION));

            // Player is repeating this Mission
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Sandoria) == Mission.Sandoria.None &&
                    q.Player.Nation == AreaId &&
                    q.Player.HasCompletedMission(AreaId, MissionId))
                .OnEventFinish(Zone.SouthernSanDoria, 1009, q =>
                    HandleAcceptMission(q, Text.SouthernSanDoria.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.SouthernSanDoria, 2009, q =>
                    HandleAcceptMission(q, Text.SouthernSanDoria.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.NorthernSanDoria, 1009, q =>
                    HandleAcceptMission(q, Text.NorthernSanDoria.YOU_ACCEPT_THE_MISSION));

            // Mission has been accepted; this section handles logic for both first time and repeat completion
            b.State(current: true)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Ambrotien)
                .OnTrigger(q => QuestReply.MessageText(q.Text.Id(Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET)))
                .OnTrade(q => HandleTradeEvent(q, 2020, 2002))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Endracion)
                .OnTrigger(q => QuestReply.MessageText(q.Text.Id(Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET)))
                .OnTrade(q => HandleTradeEvent(q, 1020, 1002))
                .OnEventFinish(Zone.SouthernSanDoria, 1002, HandleTradeEventFinish)
                .OnEventFinish(Zone.SouthernSanDoria, 1020, HandleTradeEventFinish)
                .OnEventFinish(Zone.SouthernSanDoria, 2002, HandleTradeEventFinish)
                .OnEventFinish(Zone.SouthernSanDoria, 2020, HandleTradeEventFinish)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Grilau)
                .OnTrigger(q => QuestReply.MessageText(q.Text.Id(Text.NorthernSanDoria.ORIGINAL_MISSION_OFFSET)))
                .OnTrade(q => HandleTradeEvent(q, 1020, 1002))
                .OnEventFinish(Zone.NorthernSanDoria, 1002, HandleTradeEventFinish)
                .OnEventFinish(Zone.NorthernSanDoria, 1020, HandleTradeEventFinish);
        }
    }
}
