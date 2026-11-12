// Xarcabard, Land of Truths (Bastok 5-2, Log ID 1, Mission ID 15), a line-for-line port of
// scripts/missions/bastok/5_2_Xarcabard_Land_of_Truths.lua (quest rework C3 N4). Argus !pos 132.157 7.496 -2.187 236;
// Cleades !pos -358 -10 -168 235; Malduc !pos 66.200 -14.999 4.426 237; Rashid !pos -8.444 -2 -123.575 234; _6ld !pos
// 92 -19 0.1 237; Karst !pos 106 -21 0 237. Reward: 20000 gil and rank 6. Section 1 (no Bastok mission current, a
// Bastokan): the gate guards' 1001 finish with option 15 (handleAcceptMission) begins the mission and says the zone's
// YOU_ACCEPT_THE_MISSION. Section 2 (current): Cleades, Rashid, Malduc and Argus say their zone's
// ORIGINAL_MISSION_OFFSET + 39 (messageSpecial); Karst at status 0 plays 602, whose finish sets status 2; _6ld with
// the Shadow Fragment plays 603, whose finish completes the mission and then takes the fragment. The Throne Room's
// _4l1 at status 2 plays 6, whose finish sets status 3; the zone-in at status 3 with PostBattle 1 plays 7 (text table
// -1, flags UNKNOWN_0008), whose finish sets status 4, clears PostBattle and gives the Shadow Fragment
// (npcUtil.giveKeyItem). 32001's finish at status 3 with battlefieldWin SHADOW_LORD_BATTLE (160) adds Zilart's The New
// Frontier unless it is current or completed, sets PostBattle 1 and moves the char to (90.425, -5.749, 0.089, 3) in
// the Throne Room. Section 3 (a Bastokan with no Bastok mission current who has completed it and not Return of the
// Talekeeper): Pavvke plays 76 (event, importantOnce). Kept as the Lua has it: section 1 compares the nation with
// mission.areaId (the log id, 1, which is xi.nation.BASTOK); section 1 has no rank check and no completed check (it
// reads only the current mission and the nation).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Bastok
{
    public sealed class XarcabardLandOfTruths : MissionScript
    {
        public XarcabardLandOfTruths() : base(XI.Quests.MissionLog.Bastok, "XARCABARD_LAND_OF_TRUTHS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/bastok/5_2_Xarcabard_Land_of_Truths";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Bastok;
        const int MissionId = Mission.Bastok.XarcabardLandOfTruths;

        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 15)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        // mission:messageSpecial(zone.text.ORIGINAL_MISSION_OFFSET + 39)
        static QuestReply OffsetPlus39(IQuestContext q, ZoneText offset) =>
            QuestReply.MessageSpecial(q.Text.Id(offset) + 39);

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { Gil = 20000, Rank = 6 });

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
                .OnTrigger(q => OffsetPlus39(q, Text.BastokMarkets.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.BastokMines, Npc.BastokMines.Rashid)
                .OnTrigger(q => OffsetPlus39(q, Text.BastokMines.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.Metalworks, Npc.Metalworks._6ld).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.ShadowFragment))
                    {
                        return QuestReply.ProgressEvent(603);
                    }

                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Karst).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 0)
                    {
                        return QuestReply.ProgressEvent(602);
                    }

                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Malduc)
                .OnTrigger(q => OffsetPlus39(q, Text.Metalworks.ORIGINAL_MISSION_OFFSET))
                .OnEventFinish(Zone.Metalworks, 602, q => q.Player.SetMissionStatus(AreaId, 2))
                .OnEventFinish(Zone.Metalworks, 603, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.ShadowFragment);
                    }
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.Argus)
                .OnTrigger(q => OffsetPlus39(q, Text.PortBastok.ORIGINAL_MISSION_OFFSET))
                .OnZoneIn(Zone.ThroneRoom, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 3 &&
                        q.Mission.Var("PostBattle") == 1)
                    {
                        // { 7, -1, xi.cutsceneFlag.UNKNOWN_0008 }
                        return QuestReply.Event(7).TextTable(-1).Flags(0x08);
                    }

                    return null;
                })
                .Npc(Zone.ThroneRoom, Npc.ThroneRoom._4l1).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 2)
                    {
                        return QuestReply.ProgressEvent(6);
                    }

                    return null;
                })
                .OnEventFinish(Zone.ThroneRoom, 32001, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 3 &&
                        q.Player.LocalVar("battlefieldWin") == Battlefield.ShadowLordBattle)
                    {
                        if (q.Player.CurrentMission(MissionLog.Zilart) != Mission.Zilart.TheNewFrontier &&
                            !q.Player.HasCompletedMission(MissionLog.Zilart, Mission.Zilart.TheNewFrontier))
                        {
                            // Don't add missions we already completed. Players who change nation will hit this.
                            q.Player.AddMission(MissionLog.Zilart, Mission.Zilart.TheNewFrontier);
                        }

                        q.Mission.SetVar("PostBattle", 1);
                        q.Player.SetPos(90.425, -5.749, 0.089, 3, Zone.ThroneRoom);
                    }
                })
                .OnEventFinish(Zone.ThroneRoom, 6, q => q.Player.SetMissionStatus(AreaId, 3))
                .OnEventFinish(Zone.ThroneRoom, 7, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 4);
                    q.Mission.SetVar("PostBattle", 0);
                    q.Player.GiveKeyItem(KeyItem.ShadowFragment);
                });

            b.State(current: null, when: q =>
                    q.Player.Nation == Nation.Bastok &&
                    q.Player.CurrentMission(AreaId) == Mission.Bastok.None &&
                    q.Player.HasCompletedMission(AreaId, MissionId) &&
                    !q.Player.HasCompletedMission(MissionLog.Bastok, Mission.Bastok.ReturnOfTheTalekeeper))
                .Npc(Zone.BastokMines, Npc.BastokMines.Pavvke).OnTrigger(QuestReply.Event(76).ImportantOnce());
        }
    }
}
