// Wading Beasts (Bastok 2-2, Log ID 1, Mission ID 4), a line-for-line port of
// scripts/missions/bastok/2_2_Wading_Beasts.lua (quest rework C3 N1a). Argus !pos 132.157 7.496 -2.187 236; Cleades
// !pos -358 -10 -168 235; Malduc !pos 66.200 -14.999 4.426 237; Rashid !pos -8.444 -2 -123.575 234; Alois !pos 96 -20
// 14 237. Section 1 (no mission current, a Bastokan): the four gate guards keep their script's 1001; its finish on
// option 4 begins the mission and sends YOU_ACCEPT_THE_MISSION of the char's zone. Section 2 (2-2 current): Cleades,
// Rashid, Malduc and Argus play their zone's ORIGINAL_MISSION_OFFSET + 21. Alois takes one lizard egg: event 372 the
// first time, 373 once the mission is completed; either finish completes the mission (250 rank points, no gil or rank)
// and takes the egg only when the completion succeeded. Kept as the Lua has it: the one handleAcceptMission closure is
// four handlers, each its zone's; section 1 compares the nation with mission.areaId (the log id, 1, which is
// xi.nation.BASTOK).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Bastok
{
    public sealed class WadingBeasts : MissionScript
    {
        public WadingBeasts() : base(XI.Quests.MissionLog.Bastok, "WADING_BEASTS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/bastok/2_2_Wading_Beasts";

        // handleAcceptMission: option 4 begins the mission, with the char's zone's YOU_ACCEPT_THE_MISSION
        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 4)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        // mission:messageSpecial(zone.text.ORIGINAL_MISSION_OFFSET + 21)
        static QuestReply OffsetPlus21(IQuestContext q, ZoneText offset) =>
            QuestReply.MessageSpecial(q.Text.Id(offset) + 21);

        // the 372 and 373 finishes
        static void HandleCompleteEvent(IQuestContext q)
        {
            if (q.Mission.Complete())
            {
                q.Trade.Complete();
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 250 });

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
                .OnTrigger(q => OffsetPlus21(q, Text.BastokMarkets.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.BastokMines, Npc.BastokMines.Rashid)
                .OnTrigger(q => OffsetPlus21(q, Text.BastokMines.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.Metalworks, Npc.Metalworks.Alois)
                .OnTrade(q =>
                {
                    if (q.Trade.Matches((Item.LizardEgg, 1)))
                    {
                        if (!q.Player.HasCompletedMission(MissionLog.Bastok, Mission.Bastok.WadingBeasts))
                        {
                            return QuestReply.ProgressEvent(372);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(373);
                        }
                    }

                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Malduc)
                .OnTrigger(q => OffsetPlus21(q, Text.Metalworks.ORIGINAL_MISSION_OFFSET))
                .OnEventFinish(Zone.Metalworks, 372, HandleCompleteEvent)
                .OnEventFinish(Zone.Metalworks, 373, HandleCompleteEvent)
                .Npc(Zone.PortBastok, Npc.PortBastok.Argus)
                .OnTrigger(q => OffsetPlus21(q, Text.PortBastok.ORIGINAL_MISSION_OFFSET));
        }
    }
}
