// The Crystal Line (Bastok 2-1, Log ID 1, Mission ID 3), a line-for-line port of
// scripts/missions/bastok/2_1_The_Crystal_Line.lua (quest rework C3 N1a). Argus !pos 132.157 7.496 -2.187 236; Cleades
// !pos -358 -10 -168 235; Malduc !pos 66.200 -14.999 4.426 237; Rashid !pos -8.444 -2 -123.575 234; Cid !pos -12 -12 1
// 237; Naji !pos 64 -14 -4 237; Ayame !pos 133 -19 34 237. Section 1 (no mission current, a Bastokan): the four gate
// guards keep their script's 1001; its finish on option 3 begins the mission and sends YOU_ACCEPT_THE_MISSION of the
// char's zone. Section 2 (2-1 current): Cleades, Rashid, Malduc and Argus play their zone's ORIGINAL_MISSION_OFFSET +
// 19. Cid: with the C. L. Report, MISSION_DIALOG_CID_TO_AYAME; status 0, 505 (its finish, option 0, gives a random
// crystal, Fire to Dark, and moves to status 1 only when it was given); status 1, event 502; a trade of one faded
// crystal at status 1 plays 506 (its finish, option 0, takes the crystal and gives the report). Naji with the report:
// 711; Ayame with it: 712, whose finish completes the mission (200 rank points, no gil or rank) and takes the report.
// Kept as the Lua has it: the one handleAcceptMission closure is four handlers, each its zone's; section 1 compares
// the nation with mission.areaId (the log id, 1, which is xi.nation.BASTOK); the crystal is rolled before it is known
// to fit, 505 without room gives nothing and leaves status 0; 712 takes the report only if the completion succeeded.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Bastok
{
    public sealed class TheCrystalLine : MissionScript
    {
        public TheCrystalLine() : base(XI.Quests.MissionLog.Bastok, "THE_CRYSTAL_LINE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/bastok/2_1_The_Crystal_Line";

        // handleAcceptMission: option 3 begins the mission, with the char's zone's YOU_ACCEPT_THE_MISSION
        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 3)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        // mission:messageSpecial(zone.text.ORIGINAL_MISSION_OFFSET + 19)
        static QuestReply OffsetPlus19(IQuestContext q, ZoneText offset) =>
            QuestReply.MessageSpecial(q.Text.Id(offset) + 19);

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 200 });

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
                .OnTrigger(q => OffsetPlus19(q, Text.BastokMarkets.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.BastokMines, Npc.BastokMines.Rashid)
                .OnTrigger(q => OffsetPlus19(q, Text.BastokMines.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.Metalworks, Npc.Metalworks.Ayame).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.CLReport))
                    {
                        return QuestReply.ProgressEvent(712);
                    }

                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Cid)
                .OnTrade(q =>
                {
                    if (q.Player.MissionStatus(MissionLog.Bastok) == 1 &&
                        q.Trade.Matches((Item.FadedCrystal, 1)))
                    {
                        return QuestReply.ProgressEvent(506);
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Bastok);

                    if (q.Player.HasKeyItem(KeyItem.CLReport))
                    {
                        return QuestReply.MessageText(Text.Metalworks.MISSION_DIALOG_CID_TO_AYAME);
                    }
                    else if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(505);
                    }
                    else if (missionStatus == 1)
                    {
                        return QuestReply.Event(502);
                    }

                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Malduc)
                .OnTrigger(q => OffsetPlus19(q, Text.Metalworks.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.Metalworks, Npc.Metalworks.Naji).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.CLReport))
                    {
                        return QuestReply.ProgressEvent(711);
                    }

                    return null;
                })
                .OnEventFinish(Zone.Metalworks, 505, q =>
                {
                    if (q.Option == 0)
                    {
                        int crystalItem = q.Player.RandomInt(Item.FireCrystal, Item.DarkCrystal);

                        if (q.Player.GiveItem(crystalItem))
                        {
                            q.Player.SetMissionStatus(MissionLog.Bastok, 1);
                        }
                    }
                })
                .OnEventFinish(Zone.Metalworks, 506, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Trade.Complete();
                        q.Player.GiveKeyItem(KeyItem.CLReport);
                    }
                })
                .OnEventFinish(Zone.Metalworks, 712, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.CLReport);
                    }
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.Argus)
                .OnTrigger(q => OffsetPlus19(q, Text.PortBastok.ORIGINAL_MISSION_OFFSET));
        }
    }
}
