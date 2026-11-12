// Bat Hunt (San d'Oria 1-2, Log ID 0, Mission ID 1), a line-for-line port of
// scripts/missions/sandoria/1_2_Bat_Hunt.lua (quest rework C3 N1b). It handles both the first completion and repeats
// (!addmission 0 1). Ambrotien !pos 93.419 -0.001 -57.347 230; Grilau !pos -241.987 6.999 57.887 231; Endracion !pos
// -110 1 -34 230; Tombstone_Upper !pos 1 0.1 -101 190; Orcish Scale Mail is item 1112, Bat Fang 891. Section 1 (no
// San d'Oria mission current, a San d'Orian): the 1009 and 2009 finishes of Southern San d'Oria and the 1009 finish of
// Northern San d'Oria begin the mission on option 101, with no message (the NPCs' own scripts play 1009 / 2009).
// Section 2 (1-2 current, status below 2): Ambrotien, Endracion and Grilau play 2021 / 1021 / 1021; the Tombstone_Upper
// of King Ranperre's Tomb plays cutscene 4, whose finish sets status 2. Section 3 (1-2 current, status 2): each NPC
// plays 2022 / 1022 / 1022; the Orcish Scale Mail traded plays 2023 / 1023 / 1023 the first time, the Bat Fang traded
// 2003 / 1003 / 1003 once the mission is completed; any of those finishes completes the mission (200 rank points) and
// takes the traded items only when the completion succeeded. Kept as the Lua has it: the nation is compared with
// mission.areaId (the log id, 0, which is xi.nation.SANDORIA); section 1 has no completed check, so a completed
// mission is begun again by the same finishes; the repeat does not take the first-time item and the first time does
// not take the Bat Fang (a wrong item plays the NPC's own default, not a mission event).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Sandoria
{
    public sealed class BatHunt : MissionScript
    {
        public BatHunt() : base(XI.Quests.MissionLog.Sandoria, "BAT_HUNT") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/sandoria/1_2_Bat_Hunt";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Sandoria;
        const int MissionId = Mission.Sandoria.BatHunt;

        // handleAcceptMission: option 101 begins the mission
        static void HandleAcceptMission(IQuestContext q)
        {
            if (q.Option == 101)
            {
                q.Mission.Begin();
            }
        }

        // handleTradeEvent: the Orcish Scale Mail the first time, the Bat Fang when repeated
        static QuestReply HandleTradeEvent(IQuestContext q, int firstId, int repeatId)
        {
            bool isRepeated = q.Player.HasCompletedMission(AreaId, MissionId);

            if (!isRepeated && q.Trade.Matches((Item.OrcishMailScales, 1)))
            {
                return QuestReply.ProgressEvent(firstId);
            }
            else if (isRepeated && q.Trade.Matches((Item.BatFang, 1)))
            {
                return QuestReply.ProgressEvent(repeatId);
            }

            return null;
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
            b.Reward(new MissionReward { RankPoints = 200 });

            // Player has no active missions
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Sandoria) == Mission.Sandoria.None &&
                    q.Player.Nation == AreaId)
                .OnEventFinish(Zone.SouthernSanDoria, 1009, HandleAcceptMission)
                .OnEventFinish(Zone.SouthernSanDoria, 2009, HandleAcceptMission)
                .OnEventFinish(Zone.NorthernSanDoria, 1009, HandleAcceptMission);

            // Mission has been accepted, but Tomb has not been investigated
            b.State(current: true, when: q => q.Mission.Status < 2)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Ambrotien).OnTrigger(QuestReply.ProgressEvent(2021))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Endracion).OnTrigger(QuestReply.ProgressEvent(1021))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Grilau).OnTrigger(QuestReply.ProgressEvent(1021))
                .Npc(Zone.KingRanperresTomb, Npc.KingRanperresTomb.TombstoneUpper)
                .OnTrigger(QuestReply.ProgressCutscene(4))
                .OnEventFinish(Zone.KingRanperresTomb, 4, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 2);
                });

            b.State(current: true, when: q => q.Mission.Status == 2)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Ambrotien)
                .OnTrade(q => HandleTradeEvent(q, 2023, 2003))
                .OnTrigger(QuestReply.ProgressEvent(2022))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Endracion)
                .OnTrade(q => HandleTradeEvent(q, 1023, 1003))
                .OnTrigger(QuestReply.ProgressEvent(1022))
                .OnEventFinish(Zone.SouthernSanDoria, 1003, HandleTradeEventFinish)
                .OnEventFinish(Zone.SouthernSanDoria, 1023, HandleTradeEventFinish)
                .OnEventFinish(Zone.SouthernSanDoria, 2003, HandleTradeEventFinish)
                .OnEventFinish(Zone.SouthernSanDoria, 2023, HandleTradeEventFinish)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Grilau)
                .OnTrade(q => HandleTradeEvent(q, 1023, 1003))
                .OnTrigger(QuestReply.ProgressEvent(1022))
                .OnEventFinish(Zone.NorthernSanDoria, 1003, HandleTradeEventFinish)
                .OnEventFinish(Zone.NorthernSanDoria, 1023, HandleTradeEventFinish);
        }
    }
}
