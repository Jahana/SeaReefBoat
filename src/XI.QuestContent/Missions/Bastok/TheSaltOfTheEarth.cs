// The Salt of the Earth (Bastok 9-1, Log ID 1, Mission ID 22), a line-for-line port of
// scripts/missions/bastok/9_1_The_Salt_of_the_Earth.lua (quest rework C3 N6). Argus !pos 132.157 7.496 -2.187 236;
// Cleades !pos -358 -10 -168 235; Malduc !pos 66.200 -14.999 4.426 237; Rashid !pos -8.444 -2 -123.575 234; Alois
// !pos 96 -20 14 237; Dancing Wolf !pos 7.619 7 81.209 247; qm2 (Gustav) !pos -130 1.256 252.696 212. Reward: 1500
// rank points. Section 1 (no Bastok mission current, a Bastokan): the gate guards' 1001 finish with option 22
// (handleAcceptMission) begins the mission and says the zone's YOU_ACCEPT_THE_MISSION. Section 2 (current): Cleades,
// Rashid, Malduc and Argus say their zone's EXTENDED_MISSION_OFFSET + 14 (messageSpecial). Alois plays 773 at status
// 0, 774 at 1, 775 at 3 and 776 at 4 (progress); 773's finish sets status 1, 776's completes the mission and then sets
// Option 1. Dancing Wolf plays 102 at status 1, 103 at 2, 104 at 3 and 105 at 4 (progress); 102's finish sets status
// 2, 104's status 4. Gustav Tunnel's qm2 at status 2 with none of the fifteen plasms (GIGAPLASM + 0 to 14) alive
// spawns the Gigaplasm with the char's claim and says BAD_FEELING_ABOUT_PLACE; at status 3 without the Miraclesalt it
// gives it (keyItem). A Nanoplasm's death at status 2 with no plasm alive sets status 3. Section 3 (a Bastokan with no
// Bastok mission current who has completed it and not Where Two Paths Converge): Alois plays 778 (event,
// importantOnce). Section 4 (completed, Option not 0): Franziska plays 777 at Option 1 and 779 at Option 2
// (progress); 777's finish sets Option 2, 779's Option 0. Kept as the Lua has it: section 1 compares the nation with
// mission.areaId (the log id, 1, which is xi.nation.BASTOK) and has no rank check and no completed check (it reads
// only the current mission and the nation).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Bastok
{
    public sealed class TheSaltOfTheEarth : MissionScript
    {
        public TheSaltOfTheEarth() : base(XI.Quests.MissionLog.Bastok, "THE_SALT_OF_THE_EARTH") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/bastok/9_1_The_Salt_of_the_Earth";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Bastok;
        const int MissionId = Mission.Bastok.TheSaltOfTheEarth;

        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 22)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        // the Lua's local isPlasmsAlive: GetMobByID(i):isAlive() for i = GIGAPLASM to GIGAPLASM + 14
        static bool IsPlasmsAlive(IQuestContext q)
        {
            for (int i = 0; i <= 14; i++)
            {
                if (q.Player.MobIsAlive(Mob.GustavTunnel.GIGAPLASM, i))
                {
                    return true;
                }
            }

            return false;
        }

        // mission:messageSpecial(zone.text.EXTENDED_MISSION_OFFSET + 14): a text id plus an offset, so a closure
        static QuestReply OffsetPlus14(IQuestContext q, ZoneText offset) =>
            QuestReply.MessageSpecial(q.Text.Id(offset) + 14);

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 1500 });

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
                .OnTrigger(q => OffsetPlus14(q, Text.BastokMarkets.EXTENDED_MISSION_OFFSET))
                .Npc(Zone.BastokMines, Npc.BastokMines.Rashid)
                .OnTrigger(q => OffsetPlus14(q, Text.BastokMines.EXTENDED_MISSION_OFFSET))
                .Npc(Zone.Metalworks, Npc.Metalworks.Alois).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(773);
                    }
                    else if (missionStatus == 1)
                    {
                        return QuestReply.ProgressEvent(774);
                    }
                    else if (missionStatus == 3)
                    {
                        return QuestReply.ProgressEvent(775);
                    }
                    else if (missionStatus == 4)
                    {
                        return QuestReply.ProgressEvent(776);
                    }

                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Malduc)
                .OnTrigger(q => OffsetPlus14(q, Text.Metalworks.EXTENDED_MISSION_OFFSET))
                .OnEventFinish(Zone.Metalworks, 773, q => q.Player.SetMissionStatus(AreaId, 1))
                .OnEventFinish(Zone.Metalworks, 776, q =>
                {
                    if (q.Mission.Complete())
                    {
                        // Franziska: Optional Cornelia CS
                        q.Mission.SetVar("Option", 1);
                    }
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.Argus)
                .OnTrigger(q => OffsetPlus14(q, Text.PortBastok.EXTENDED_MISSION_OFFSET))
                .Npc(Zone.Rabao, Npc.Rabao.DancingWolf).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 1)
                    {
                        return QuestReply.ProgressEvent(102);
                    }
                    else if (missionStatus == 2)
                    {
                        return QuestReply.ProgressEvent(103);
                    }
                    else if (missionStatus == 3)
                    {
                        return QuestReply.ProgressEvent(104);
                    }
                    else if (missionStatus == 4)
                    {
                        return QuestReply.ProgressEvent(105);
                    }

                    return null;
                })
                .OnEventFinish(Zone.Rabao, 102, q => q.Player.SetMissionStatus(AreaId, 2))
                .OnEventFinish(Zone.Rabao, 104, q => q.Player.SetMissionStatus(AreaId, 4))
                .Npc(Zone.GustavTunnel, Npc.GustavTunnel.qm2).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 2 &&
                        !IsPlasmsAlive(q))
                    {
                        q.Player.SpawnMob(Mob.GustavTunnel.GIGAPLASM, 0, claim: true);
                        return QuestReply.MessageSpecial(Text.GustavTunnel.BAD_FEELING_ABOUT_PLACE);
                    }
                    else if (missionStatus == 3 &&
                             !q.Player.HasKeyItem(KeyItem.Miraclesalt))
                    {
                        return QuestReply.KeyItem(KeyItem.Miraclesalt);
                    }

                    return null;
                })
                .Npc(Zone.GustavTunnel, Npc.GustavTunnel.Nanoplasm).OnMobDeath(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 2 &&
                        !IsPlasmsAlive(q))
                    {
                        q.Player.SetMissionStatus(AreaId, 3);
                    }
                });

            b.State(current: null, when: q =>
                    q.Player.Nation == Nation.Bastok &&
                    q.Player.CurrentMission(AreaId) == Mission.Bastok.None &&
                    q.Player.HasCompletedMission(AreaId, MissionId) &&
                    !q.Player.HasCompletedMission(MissionLog.Bastok, Mission.Bastok.WhereTwoPathsConverge))
                .Npc(Zone.Metalworks, Npc.Metalworks.Alois).OnTrigger(QuestReply.Event(778).ImportantOnce());

            b.State(current: null, when: q =>
                    q.Player.HasCompletedMission(AreaId, MissionId) &&
                    q.Mission.Var("Option") != 0)
                .Npc(Zone.Metalworks, Npc.Metalworks.Franziska).OnTrigger(q =>
                {
                    int csOption = q.Mission.Var("Option");

                    if (csOption == 1)
                    {
                        return QuestReply.ProgressEvent(777);
                    }
                    else if (csOption == 2)
                    {
                        return QuestReply.ProgressEvent(779);
                    }

                    return null;
                })
                .OnEventFinish(Zone.Metalworks, 777, q => q.Mission.SetVar("Option", 2))
                .OnEventFinish(Zone.Metalworks, 779, q => q.Mission.SetVar("Option", 0));
        }
    }
}
