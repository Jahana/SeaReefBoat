// To the Forsaken Mines (Bastok 3-2, Log ID 1, Mission ID 11), a line-for-line port of
// scripts/missions/bastok/3_2_To_the_Forsaken_Mines.lua (quest rework C3 N3). Argus !pos 132.157 7.496 -2.187 236;
// Cleades !pos -358 -10 -168 235; Malduc !pos 66.200 -14.999 4.426 237; Rashid !pos -8.444 -2 -123.575 234; Davyad !pos
// 83 0 30 234; qm2 (Gusgen) !pos 206 -60 -101 196. Reward: 400 rank points. Section 1 (no Bastok mission current, a
// Bastokan): the four gate guards' 1001 finish with option 11 begins the mission and says the zone's
// YOU_ACCEPT_THE_MISSION. Section 2 (current): Cleades, Rashid, Malduc and Argus play 1002 (progress); a trade of
// exactly one Glocolite to any of them plays 1006 (progress) once the mission is completed, else 1010; either finish,
// in each of their zones, completes the mission and, when that succeeds, takes the trade. Bastok Mines: Davyad plays 54
// (progress). Gusgen Mines: qm2 takes a trade of exactly one Slice of Hare Meat when the char has no Glocolite and
// Blind Moby pops (hide 180): the trade is taken and it says YOU_PUT_ITEM_DOWN with the meat; else it says
// NOTHING_SEEMS_HAPPENING; its trigger says FRESH_MONSTER_TRACKS. Kept as the Lua has it: section 1 has no completed
// check and compares the nation with mission.areaId (the log id, 1, which is xi.nation.BASTOK).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Bastok
{
    public sealed class ToTheForsakenMines : MissionScript
    {
        public ToTheForsakenMines() : base(XI.Quests.MissionLog.Bastok, "TO_THE_FORSAKEN_MINES") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/bastok/3_2_To_the_Forsaken_Mines";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Bastok;
        const int MissionId = Mission.Bastok.ToTheForsakenMines;

        // handleAcceptMission: option 11 begins the mission, with the char's zone's YOU_ACCEPT_THE_MISSION
        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 11)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        // handleMissionTrade: one Glocolite: 1006 once the mission is completed, 1010 the first time
        static QuestReply HandleMissionTrade(IQuestContext q)
        {
            if (q.Trade.Matches((Item.Glocolite, 1)))
            {
                if (q.Player.HasCompletedMission(AreaId, MissionId))
                {
                    return QuestReply.ProgressEvent(1006);
                }
                else
                {
                    return QuestReply.ProgressEvent(1010);
                }
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
            b.Reward(new MissionReward { RankPoints = 400 });

            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Bastok) == Mission.Bastok.None &&
                    q.Player.Nation == AreaId)
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
                .OnTrade(HandleMissionTrade)
                .OnTrigger(q => QuestReply.ProgressEvent(1002))
                .OnEventFinish(Zone.BastokMarkets, 1006, HandleTradeEventFinish)
                .OnEventFinish(Zone.BastokMarkets, 1010, HandleTradeEventFinish)
                .Npc(Zone.BastokMines, Npc.BastokMines.Davyad).OnTrigger(q => QuestReply.ProgressEvent(54))
                .Npc(Zone.BastokMines, Npc.BastokMines.Rashid)
                .OnTrade(HandleMissionTrade)
                .OnTrigger(q => QuestReply.ProgressEvent(1002))
                .OnEventFinish(Zone.BastokMines, 1006, HandleTradeEventFinish)
                .OnEventFinish(Zone.BastokMines, 1010, HandleTradeEventFinish)
                .Npc(Zone.GusgenMines, Npc.GusgenMines.qm2)
                .OnTrade(q =>
                {
                    if (q.Trade.Matches((Item.SliceOfHareMeat, 1)) &&
                        !q.Player.FindItem(Item.Glocolite) &&
                        q.Player.PopFromQm(Mob.GusgenMines.BLIND_MOBY, hide: 180))
                    {
                        q.Trade.Complete();
                        return QuestReply.MessageSpecial(Text.GusgenMines.YOU_PUT_ITEM_DOWN, Item.SliceOfHareMeat);
                    }
                    else
                    {
                        return QuestReply.MessageSpecial(Text.GusgenMines.NOTHING_SEEMS_HAPPENING);
                    }
                })
                .OnTrigger(q => QuestReply.MessageSpecial(Text.GusgenMines.FRESH_MONSTER_TRACKS))
                .Npc(Zone.Metalworks, Npc.Metalworks.Malduc)
                .OnTrade(HandleMissionTrade)
                .OnTrigger(q => QuestReply.ProgressEvent(1002))
                .OnEventFinish(Zone.Metalworks, 1006, HandleTradeEventFinish)
                .OnEventFinish(Zone.Metalworks, 1010, HandleTradeEventFinish)
                .Npc(Zone.PortBastok, Npc.PortBastok.Argus)
                .OnTrade(HandleMissionTrade)
                .OnTrigger(q => QuestReply.ProgressEvent(1002))
                .OnEventFinish(Zone.PortBastok, 1006, HandleTradeEventFinish)
                .OnEventFinish(Zone.PortBastok, 1010, HandleTradeEventFinish);
        }
    }
}
