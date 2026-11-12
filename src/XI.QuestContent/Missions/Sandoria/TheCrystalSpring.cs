// The Crystal Spring (San d'Oria 3-2, Log ID 0, Mission ID 11), a line-for-line port of
// scripts/missions/sandoria/3_2_The_Crystal_Spring.lua (quest rework C3 N3). Ambrotien !pos 93.419 -0.001 -57.347 230;
// Grilau !pos -241.987 6.999 57.887 231; Endracion !pos -110 1 -34 230. Reward: 400 rank points. Section 1 (no San
// d'Oria mission current, a San d'Orian): the 1009 and 2009 finishes of Southern San d'Oria and the 1009 finish of
// Northern San d'Oria begin the mission on option 111 (no message). Section 2 (current, not completed before): a trade
// of exactly one Crystal Bass to Ambrotien plays 2030, to Endracion or Grilau 1030 (progress), at any status; at status
// 0 each says its zone's ORIGINAL_MISSION_OFFSET + 68 (priority 1000), at 2 Ambrotien plays 2031, Endracion and Grilau
// 1031 (progress); the 1030 (both zones) and 2030 finishes take the trade and set status 2. Chateau d'Oraguille:
// Chalvatot at 3 plays 556 (progress), whose finish completes the mission; a zone-in from Northern San d'Oria at 2
// plays 555, whose finish sets status 3. Section 3 (current, completed before: a repeat): the bass to Ambrotien plays
// 2013, to Endracion or Grilau 1013 (progress); at 0 each says ORIGINAL_MISSION_OFFSET + 68 (priority 1000); the 1013
// (both zones) and 2013 finishes complete the mission and, when that succeeds, take the trade. Kept as the Lua has it:
// section 1 has no completed check and compares the nation with mission.areaId (the log id, 0, which is
// xi.nation.SANDORIA); the trades are not gated on the status.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Sandoria
{
    public sealed class TheCrystalSpring : MissionScript
    {
        public TheCrystalSpring() : base(XI.Quests.MissionLog.Sandoria, "THE_CRYSTAL_SPRING") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/sandoria/3_2_The_Crystal_Spring";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Sandoria;
        const int MissionId = Mission.Sandoria.TheCrystalSpring;

        // handleAcceptMission: option 111 begins the mission
        static void HandleAcceptMission(IQuestContext q)
        {
            if (q.Option == 111)
            {
                q.Mission.Begin();
            }
        }

        // a trade of exactly one Crystal Bass plays the given event
        static QuestReply BassTrade(IQuestContext q, int csid) =>
            q.Trade.Matches((Item.CrystalBass, 1)) ? QuestReply.ProgressEvent(csid) : null;

        // the first run's mission NPCs: status 0 ORIGINAL_MISSION_OFFSET + 68, 2 the given event
        static QuestReply FirstRunTrigger(IQuestContext q, int csid, ZoneText offset)
        {
            int missionStatus = q.Player.MissionStatus(AreaId);

            if (missionStatus == 0)
            {
                return QuestReply.MessageText(q.Text.Id(offset) + 68).SetPriority(1000);
            }
            else if (missionStatus == 2)
            {
                return QuestReply.ProgressEvent(csid);
            }

            return null;
        }

        // the repeat's mission NPCs: status 0 ORIGINAL_MISSION_OFFSET + 68
        static QuestReply RepeatTrigger(IQuestContext q, ZoneText offset) =>
            q.Player.MissionStatus(AreaId) == 0
                ? QuestReply.MessageText(q.Text.Id(offset) + 68).SetPriority(1000)
                : null;

        // the first run's bass finishes: the trade taken, status 2
        static void HandleBassFinish(IQuestContext q)
        {
            q.Trade.Complete();
            q.Player.SetMissionStatus(AreaId, 2);
        }

        // the repeat's bass finishes: complete, then the trade taken
        static void HandleRepeatFinish(IQuestContext q)
        {
            if (q.Mission.Complete())
            {
                q.Trade.Complete();
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 400 });

            // Player has no active missions
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Sandoria) == Mission.Sandoria.None &&
                    q.Player.Nation == AreaId)
                .OnEventFinish(Zone.SouthernSanDoria, 1009, HandleAcceptMission)
                .OnEventFinish(Zone.SouthernSanDoria, 2009, HandleAcceptMission)
                .OnEventFinish(Zone.NorthernSanDoria, 1009, HandleAcceptMission);

            // First time the player has accepted this mission
            b.State(current: true, when: q => !q.Player.HasCompletedMission(AreaId, MissionId))
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Chalvatot).OnTrigger(q =>
                    q.Player.MissionStatus(AreaId) == 3 ? QuestReply.ProgressEvent(556) : null)
                // prevZone is player:getPreviousZone() (the same value: both are CharData.PrevZone)
                .OnZoneIn(Zone.ChateauDoraguille, q =>
                    q.Player.PreviousZone == Zone.NorthernSanDoria &&
                    q.Player.MissionStatus(AreaId) == 2
                        ? QuestReply.Event(555)
                        : null)
                .OnEventFinish(Zone.ChateauDoraguille, 555, q => q.Player.SetMissionStatus(AreaId, 3))
                .OnEventFinish(Zone.ChateauDoraguille, 556, q => q.Mission.Complete())
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Ambrotien)
                .OnTrade(q => BassTrade(q, 2030))
                .OnTrigger(q => FirstRunTrigger(q, 2031, Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Endracion)
                .OnTrade(q => BassTrade(q, 1030))
                .OnTrigger(q => FirstRunTrigger(q, 1031, Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET))
                .OnEventFinish(Zone.SouthernSanDoria, 1030, HandleBassFinish)
                .OnEventFinish(Zone.SouthernSanDoria, 2030, HandleBassFinish)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Grilau)
                .OnTrade(q => BassTrade(q, 1030))
                .OnTrigger(q => FirstRunTrigger(q, 1031, Text.NorthernSanDoria.ORIGINAL_MISSION_OFFSET))
                .OnEventFinish(Zone.NorthernSanDoria, 1030, HandleBassFinish);

            // Player is repeating this mission
            b.State(current: true, when: q => q.Player.HasCompletedMission(AreaId, MissionId))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Ambrotien)
                .OnTrade(q => BassTrade(q, 2013))
                .OnTrigger(q => RepeatTrigger(q, Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Endracion)
                .OnTrade(q => BassTrade(q, 1013))
                .OnTrigger(q => RepeatTrigger(q, Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET))
                .OnEventFinish(Zone.SouthernSanDoria, 1013, HandleRepeatFinish)
                .OnEventFinish(Zone.SouthernSanDoria, 2013, HandleRepeatFinish)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Grilau)
                .OnTrade(q => BassTrade(q, 1013))
                .OnTrigger(q => RepeatTrigger(q, Text.NorthernSanDoria.ORIGINAL_MISSION_OFFSET))
                .OnEventFinish(Zone.NorthernSanDoria, 1013, HandleRepeatFinish);
        }
    }
}
