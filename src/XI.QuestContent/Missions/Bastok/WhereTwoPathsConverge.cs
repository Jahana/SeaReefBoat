// Where Two Paths Converge (Bastok 9-2, Log ID 1, Mission ID 23), a line-for-line port of
// scripts/missions/bastok/9_2_Where_Two_Paths_Converge.lua (quest rework C3 N6). Argus !pos 132.157 7.496 -2.187 236;
// Cleades !pos -358 -10 -168 235; Malduc !pos 66.200 -14.999 4.426 237; Rashid !pos -8.444 -2 -123.575 234; Iron
// Eater !pos 92.936 -19.532 1.814 237. Reward: rank 10, 100000 gil and the title Hero Among Heroes. Section 1 (no
// Bastok mission current, a Bastokan): the gate guards' 1001 finish with option 23 (handleAcceptMission) begins the
// mission and says the zone's YOU_ACCEPT_THE_MISSION. Section 2 (current): Cleades, Rashid, Malduc and Argus say their
// zone's EXTENDED_MISSION_OFFSET + 19 (messageSpecial). Iron Eater plays 780 at status 0, 769 at 1 and 782 at 2
// (progress); 780's finish sets status 1; 782's completes the mission and then gives the Bastokan Flag
// (npcUtil.giveItem), setting Flag 1 when it cannot. The Throne Room's 32001 finish at status 1 with battlefieldWin
// WHERE_TWO_PATHS_CONVERGE (161) sets status 2. Section 3 (completed): Iron Eater with Flag 1 gives the flag and, when
// it could, clears Flag. Kept as the Lua has it: section 1 compares the nation with mission.areaId (the log id, 1,
// which is xi.nation.BASTOK) and has no rank check and no completed check (it reads only the current mission and the
// nation); 782's finish gives the flag whatever mission:complete answered.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Bastok
{
    public sealed class WhereTwoPathsConverge : MissionScript
    {
        public WhereTwoPathsConverge() : base(XI.Quests.MissionLog.Bastok, "WHERE_TWO_PATHS_CONVERGE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/bastok/9_2_Where_Two_Paths_Converge";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Bastok;
        const int MissionId = Mission.Bastok.WhereTwoPathsConverge;

        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 23)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        // mission:messageSpecial(zone.text.EXTENDED_MISSION_OFFSET + 19): a text id plus an offset, so a closure
        static QuestReply OffsetPlus19(IQuestContext q, ZoneText offset) =>
            QuestReply.MessageSpecial(q.Text.Id(offset) + 19);

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { Rank = 10, Gil = 100000, Title = Title.HeroAmongHeroes });

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
                .OnTrigger(q => OffsetPlus19(q, Text.BastokMarkets.EXTENDED_MISSION_OFFSET))
                .Npc(Zone.BastokMines, Npc.BastokMines.Rashid)
                .OnTrigger(q => OffsetPlus19(q, Text.BastokMines.EXTENDED_MISSION_OFFSET))
                .Npc(Zone.Metalworks, Npc.Metalworks.IronEater).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(780);
                    }
                    else if (missionStatus == 1)
                    {
                        return QuestReply.ProgressEvent(769);
                    }
                    else if (missionStatus == 2)
                    {
                        return QuestReply.ProgressEvent(782);
                    }

                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Malduc)
                .OnTrigger(q => OffsetPlus19(q, Text.Metalworks.EXTENDED_MISSION_OFFSET))
                .OnEventFinish(Zone.Metalworks, 780, q => q.Player.SetMissionStatus(AreaId, 1))
                // It is possible to obtain the Bastokan flag after completing this mission
                // should inventory be full.  Set 'Flag' mission variable should this occur.
                .OnEventFinish(Zone.Metalworks, 782, q =>
                {
                    q.Mission.Complete();

                    if (!q.Player.GiveItem(Item.BastokanFlag))
                    {
                        q.Mission.SetVar("Flag", 1);
                    }
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.Argus)
                .OnTrigger(q => OffsetPlus19(q, Text.PortBastok.EXTENDED_MISSION_OFFSET))
                .OnEventFinish(Zone.ThroneRoom, 32001, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1 &&
                        q.Player.LocalVar("battlefieldWin") == Battlefield.WhereTwoPathsConverge)
                    {
                        q.Player.SetMissionStatus(AreaId, 2);
                    }
                });

            // Player has completed this mission
            b.State(current: null, when: q => q.Player.HasCompletedMission(AreaId, MissionId))
                // If the player's inventory was full on mission complete, give them the
                // nation flag, and clear the variable.  This could probably be short circuited
                // with one conditional, but playing it safe.
                .Npc(Zone.Metalworks, Npc.Metalworks.IronEater).OnTrigger(q =>
                {
                    if (q.Mission.Var("Flag") == 1)
                    {
                        if (q.Player.GiveItem(Item.BastokanFlag))
                        {
                            q.Mission.SetVar("Flag", 0);
                        }
                    }

                    return null;
                });
        }
    }
}
