// The Final Image (Bastok 7-1, Log ID 1, Mission ID 18), a line-for-line port of
// scripts/missions/bastok/7_1_The_Final_Image.lua (quest rework C3 N5). Argus !pos 132.157 7.496 -2.187 236; Cleades
// !pos -358 -10 -168 235; Malduc !pos 66.200 -14.999 4.426 237; Rashid !pos -8.444 -2 -123.575 234; Cid !pos -12 -12 1
// 237; qm2 !pos 102 -4 -114 122 (Varies). Reward: 700 rank points. Section 1 (no Bastok mission current, a
// Bastokan): the four gate guards' 1001 finish with option 18 (handleAcceptMission) begins the mission and says the
// zone's YOU_ACCEPT_THE_MISSION. Section 2 (current): Cleades, Rashid, Malduc and Argus say their zone's
// EXTENDED_MISSION_OFFSET + 5 (messageSpecial); Cid plays 763 at status 0 and 764 at status 2 (progress); 763's
// finish sets status 1, 764's completes the mission and then takes the Reinforced Cermet. Ro'Maeve: qm2 at status 1
// with the nmDefeated local var 1 sets status 2 and gives the Reinforced Cermet (keyItem); else it pops the two
// Mokkurkalfi (no claim, look, radius 2), hides itself for 0 seconds and says A_CHILL_RUNS_DOWN_SPINE. A
// Mokkurkalfi's death at status 1 sets the nmDefeated local var 1. Kept as the Lua has it: section 1 compares the
// nation with mission.areaId (the log id, 1, which is xi.nation.BASTOK) and has no rank check and no completed check;
// qm2 ignores popFromQM's answer, so the hideNPC(0) and the message run whether the mobs popped or not; hideNPC(0)
// follows popFromQM's own hide (FORCE_SPAWN_QM_RESET_TIME, no hide given); either Mokkurkalfi's death alone sets
// nmDefeated.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Bastok
{
    public sealed class TheFinalImage : MissionScript
    {
        public TheFinalImage() : base(XI.Quests.MissionLog.Bastok, "THE_FINAL_IMAGE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/bastok/7_1_The_Final_Image";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Bastok;
        const int MissionId = Mission.Bastok.TheFinalImage;

        // handleAcceptMission: option 18 begins the mission, with the char's zone's YOU_ACCEPT_THE_MISSION
        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 18)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        // mission:messageSpecial(zone.text.EXTENDED_MISSION_OFFSET + 5)
        static QuestReply OffsetPlus5(IQuestContext q, ZoneText offset) =>
            QuestReply.MessageSpecial(q.Text.Id(offset) + 5);

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 700 });

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
                .OnTrigger(q => OffsetPlus5(q, Text.BastokMarkets.EXTENDED_MISSION_OFFSET))
                .Npc(Zone.BastokMines, Npc.BastokMines.Rashid)
                .OnTrigger(q => OffsetPlus5(q, Text.BastokMines.EXTENDED_MISSION_OFFSET))
                .Npc(Zone.Metalworks, Npc.Metalworks.Cid).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(763);
                    }
                    else if (missionStatus == 2)
                    {
                        return QuestReply.ProgressEvent(764);
                    }

                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Malduc)
                .OnTrigger(q => OffsetPlus5(q, Text.Metalworks.EXTENDED_MISSION_OFFSET))
                .OnEventFinish(Zone.Metalworks, 763, q => q.Player.SetMissionStatus(AreaId, 1))
                .OnEventFinish(Zone.Metalworks, 764, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.ReinforcedCermet);
                    }
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.Argus)
                .OnTrigger(q => OffsetPlus5(q, Text.PortBastok.EXTENDED_MISSION_OFFSET))
                .Npc(Zone.Romaeve, Npc.Romaeve.qm2).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1)
                    {
                        if (q.Mission.LocalVar("nmDefeated") == 1)
                        {
                            q.Player.SetMissionStatus(AreaId, 2);
                            return QuestReply.KeyItem(KeyItem.ReinforcedCermet);
                        }
                        else
                        {
                            // npcUtil.popFromQM(player, npc, { MOKKURKALFI, MOKKURKALFI + 1 }, { claim = false,
                            // look = true, radius = 2 }): its answer is not read
                            q.Player.PopFromQmSeriesRadius(Mob.Romaeve.MOKKURKALFI, 2, claim: false, look: true,
                                radius: 2);
                            q.Npc.Hide(0);
                            return QuestReply.MessageSpecial(Text.Romaeve.A_CHILL_RUNS_DOWN_SPINE);
                        }
                    }

                    return null;
                })
                .Npc(Zone.Romaeve, Npc.Romaeve.Mokkurkalfi).OnMobDeath(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1)
                    {
                        q.Mission.SetLocalVar("nmDefeated", 1);
                    }
                });
        }
    }
}
