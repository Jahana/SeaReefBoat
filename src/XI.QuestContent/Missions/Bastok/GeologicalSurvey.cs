// A Geological Survey (Bastok 1-2, Log ID 1, Mission ID 1), a line-for-line port of
// scripts/missions/bastok/1_2_A_Geological_Survey.lua (quest rework C3 N1a). Argus !pos 132.157 7.496 -2.187 236;
// Cleades !pos -358 -10 -168 235; Malduc !pos 66.200 -14.999 4.426 237; Rashid !pos -8.444 -2 -123.575 234; Cid !pos
// -12 -12 1 237. Section 1 (no mission current, a Bastokan): the four gate guards keep their script's 1001; its finish
// on option 1 begins the mission and sends YOU_ACCEPT_THE_MISSION of the char's zone. Section 2 (1-2 current): Cleades,
// Rashid, Malduc and Argus play their zone's ORIGINAL_MISSION_OFFSET + 3; Cid plays 504 with the Red Acidity Tester,
// else 503 without the Blue one (its finish gives it), else 502; 504's finish completes the mission and takes the Red
// tester; Dangruf Wadi's geyser events 10, 11 and 12 (the zone's own trigger areas start them) turn a held Blue tester
// into the Red one. Kept as the Lua has it: the one handleAcceptMission closure is four handlers, each its zone's;
// section 1 compares the nation with mission.areaId (the log id, 1, which is xi.nation.BASTOK); no reward table
// (mission:complete gives nothing, then the status reset and the var cleanup); 504's finish takes the tester only when
// mission:complete answers true.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Bastok
{
    public sealed class GeologicalSurvey : MissionScript
    {
        public GeologicalSurvey() : base(XI.Quests.MissionLog.Bastok, "GEOLOGICAL_SURVEY") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/bastok/1_2_A_Geological_Survey";

        // handleAcceptMission: option 1 begins the mission, with the char's zone's YOU_ACCEPT_THE_MISSION
        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 1)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        // mission:messageSpecial(zone.text.ORIGINAL_MISSION_OFFSET + 3)
        static QuestReply OffsetPlus3(IQuestContext q, ZoneText offset) =>
            QuestReply.MessageSpecial(q.Text.Id(offset) + 3);

        // the geyser events: the Blue Acidity Tester turns into the Red one
        static void TurnTester(IQuestContext q)
        {
            if (q.Player.HasKeyItem(KeyItem.BlueAcidityTester))
            {
                q.Player.DelKeyItem(KeyItem.BlueAcidityTester);
                q.Player.GiveKeyItem(KeyItem.RedAcidityTester);
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Bastok) == Mission.Bastok.None &&
                    q.Player.Nation == MissionLog.Bastok)
                .OnEventFinish(Zone.BastokMarkets, 1001, q =>
                    HandleAcceptMission(q, Text.BastokMarkets.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.BastokMines, 1001, q =>
                    HandleAcceptMission(q, Text.BastokMines.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.Metalworks, 1001, q =>
                    HandleAcceptMission(q, Text.Metalworks.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.PortBastok, 1001, q =>
                    HandleAcceptMission(q, Text.PortBastok.YOU_ACCEPT_THE_MISSION));

            b.State(current: true)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Cleades)
                .OnTrigger(q => OffsetPlus3(q, Text.BastokMarkets.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.BastokMines, Npc.BastokMines.Rashid)
                .OnTrigger(q => OffsetPlus3(q, Text.BastokMines.ORIGINAL_MISSION_OFFSET))
                .OnEventFinish(Zone.DangrufWadi, 10, TurnTester)
                .OnEventFinish(Zone.DangrufWadi, 11, TurnTester)
                .OnEventFinish(Zone.DangrufWadi, 12, TurnTester)
                .Npc(Zone.Metalworks, Npc.Metalworks.Cid).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.RedAcidityTester))
                    {
                        return QuestReply.ProgressEvent(504);
                    }
                    else if (!q.Player.HasKeyItem(KeyItem.BlueAcidityTester))
                    {
                        return QuestReply.ProgressEvent(503);
                    }
                    else
                    {
                        return QuestReply.Event(502);
                    }
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Malduc)
                .OnTrigger(q => OffsetPlus3(q, Text.Metalworks.ORIGINAL_MISSION_OFFSET))
                .OnEventFinish(Zone.Metalworks, 503, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.BlueAcidityTester);
                })
                .OnEventFinish(Zone.Metalworks, 504, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.RedAcidityTester);
                    }
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.Argus)
                .OnTrigger(q => OffsetPlus3(q, Text.PortBastok.ORIGINAL_MISSION_OFFSET));
        }
    }
}
