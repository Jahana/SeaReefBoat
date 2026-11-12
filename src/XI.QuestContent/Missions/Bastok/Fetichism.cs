// Fetichism (Bastok 1-3, Log ID 1, Mission ID 2), a line-for-line port of scripts/missions/bastok/1_3_Fetichism.lua
// (quest rework C3 N1a). Argus !pos 132.157 7.496 -2.187 236; Cleades !pos -358 -10 -168 235; Malduc !pos 66.200
// -14.999 4.426 237; Rashid !pos -8.444 -2 -123.575 234. Section 1 (no mission current, a Bastokan): the four gate
// guards keep their script's 1001; its finish on option 2 begins the mission and sends YOU_ACCEPT_THE_MISSION of the
// char's zone. Section 2 (1-3 current): each guard plays its zone's ORIGINAL_MISSION_OFFSET + 6; a trade of the four
// Quadav Fetich parts (head, torso, arms, legs, one each) plays 1008 the first time and 1005 once the mission is
// completed; either finish completes the mission (1000 gil, rank 2 and 200 rank points) and takes the parts only when
// the completion succeeded. Kept as the Lua has it: the one handleAcceptMission closure is four handlers, each its
// zone's; section 1 compares the nation with mission.areaId (the log id, 1, which is xi.nation.BASTOK); the reward's
// gil is given on repeats too (the Lua's TODO).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Bastok
{
    public sealed class Fetichism : MissionScript
    {
        public Fetichism() : base(XI.Quests.MissionLog.Bastok, "FETICHISM") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/bastok/1_3_Fetichism";

        // handleAcceptMission: option 2 begins the mission, with the char's zone's YOU_ACCEPT_THE_MISSION
        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 2)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        // handleFetichTrade: the four parts, one each: 1008 the first time, 1005 after
        static QuestReply HandleFetichTrade(IQuestContext q)
        {
            if (q.Trade.Matches((Item.QuadavFetichHead, 1), (Item.QuadavFetichTorso, 1), (Item.QuadavFetichArms, 1),
                                (Item.QuadavFetichLegs, 1)))
            {
                if (!q.Player.HasCompletedMission(MissionLog.Bastok, Mission.Bastok.Fetichism))
                {
                    return QuestReply.ProgressEvent(1008);
                }
                else
                {
                    return QuestReply.ProgressEvent(1005);
                }
            }

            return null;
        }

        // handleCompleteEvent
        static void HandleCompleteEvent(IQuestContext q)
        {
            if (q.Mission.Complete())
            {
                q.Trade.Complete();
            }
        }

        // mission:messageSpecial(zone.text.ORIGINAL_MISSION_OFFSET + 6)
        static QuestReply OffsetPlus6(IQuestContext q, ZoneText offset) =>
            QuestReply.MessageSpecial(q.Text.Id(offset) + 6);

        protected override void Build(MissionScriptBuilder b)
        {
            // npcUtil.completeMission only awards rank if less than the player's current rank in the nation
            b.Reward(new MissionReward { Gil = 1000, Rank = 2, RankPoints = 200 });

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
                .OnTrade(HandleFetichTrade)
                .OnTrigger(q => OffsetPlus6(q, Text.BastokMarkets.ORIGINAL_MISSION_OFFSET))
                .OnEventFinish(Zone.BastokMarkets, 1005, HandleCompleteEvent)
                .OnEventFinish(Zone.BastokMarkets, 1008, HandleCompleteEvent)
                .Npc(Zone.BastokMines, Npc.BastokMines.Rashid)
                .OnTrade(HandleFetichTrade)
                .OnTrigger(q => OffsetPlus6(q, Text.BastokMines.ORIGINAL_MISSION_OFFSET))
                .OnEventFinish(Zone.BastokMines, 1005, HandleCompleteEvent)
                .OnEventFinish(Zone.BastokMines, 1008, HandleCompleteEvent)
                .Npc(Zone.Metalworks, Npc.Metalworks.Malduc)
                .OnTrade(HandleFetichTrade)
                .OnTrigger(q => OffsetPlus6(q, Text.Metalworks.ORIGINAL_MISSION_OFFSET))
                .OnEventFinish(Zone.Metalworks, 1005, HandleCompleteEvent)
                .OnEventFinish(Zone.Metalworks, 1008, HandleCompleteEvent)
                .Npc(Zone.PortBastok, Npc.PortBastok.Argus)
                .OnTrade(HandleFetichTrade)
                .OnTrigger(q => OffsetPlus6(q, Text.PortBastok.ORIGINAL_MISSION_OFFSET))
                .OnEventFinish(Zone.PortBastok, 1005, HandleCompleteEvent)
                .OnEventFinish(Zone.PortBastok, 1008, HandleCompleteEvent);
        }
    }
}
