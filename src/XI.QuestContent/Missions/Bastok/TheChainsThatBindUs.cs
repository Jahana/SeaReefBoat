// The Chains That Bind Us (Bastok 8-1, Log ID 1, Mission ID 20), a line-for-line port of
// scripts/missions/bastok/8_1_The_Chains_That_Bind_Us.lua (quest rework C3 N6). Argus !pos 132.157 7.496 -2.187 236;
// Cleades !pos -358 -10 -168 235; Malduc !pos 66.200 -14.999 4.426 237; Rashid !pos -8.444 -2 -123.575 234; Iron
// Eater !pos 92.936 -19.532 1.814 237; qm6 !pos -469 0 620 208; qm4 !pos -533 -0.851 -415. Reward: 1133 rank points.
// Section 1 (no Bastok mission current, a Bastokan): the gate guards' 1001 finish with option 20
// (handleAcceptMission) begins the mission and says the zone's YOU_ACCEPT_THE_MISSION. Section 2 (current):
// Cleades, Rashid, Malduc and Argus say their zone's EXTENDED_MISSION_OFFSET + 10 (messageSpecial). _6ld at status 3
// plays 702; Iron Eater plays 767 at status 0, 702 at 3 and 768 at 4 (progress). 767's finish with option 0 sets
// status 1; 768's completes the mission. In the Quicksand Caves, qm4 at status 3 plays 10, whose finish sets status
// 4; qm6 at status 1 with none of Centurio IV-VII, Triarius IV-XIV and Princeps IV-XLV up spawns the three with the
// char's claim and says SENSE_OF_FOREBODING, and at status 2 plays 11, whose finish sets status 3. Each of the three
// NMs' deaths at status 1, with the other two dead or not spawned, sets status 2. Kept as the Lua has it: section 1
// compares the nation with mission.areaId (the log id, 1, which is xi.nation.BASTOK) and has no rank check and no
// completed check (it reads only the current mission and the nation).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Bastok
{
    public sealed class TheChainsThatBindUs : MissionScript
    {
        public TheChainsThatBindUs() : base(XI.Quests.MissionLog.Bastok, "THE_CHAINS_THAT_BIND_US") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/bastok/8_1_The_Chains_That_Bind_Us";

        // mission.areaId
        const int AreaId = MissionLog.Bastok;

        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 20)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        // mission:messageSpecial(zone.text.EXTENDED_MISSION_OFFSET + 10): a text id plus an offset, so a closure
        static QuestReply OffsetPlus10(IQuestContext q, ZoneText offset) =>
            QuestReply.MessageSpecial(q.Text.Id(offset) + 10);

        // GetMobByID(quicksandID.mob.<key>):isDead() or not GetMobByID(quicksandID.mob.<key>):isSpawned()
        static bool DeadOrNotSpawned(IQuestContext q, string mob) =>
            q.Player.MobIsDead(mob) || !q.Player.MobIsSpawned(mob);

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 1133 });

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
                .OnTrigger(q => OffsetPlus10(q, Text.BastokMarkets.EXTENDED_MISSION_OFFSET))
                .Npc(Zone.BastokMines, Npc.BastokMines.Rashid)
                .OnTrigger(q => OffsetPlus10(q, Text.BastokMines.EXTENDED_MISSION_OFFSET))
                .Npc(Zone.Metalworks, Npc.Metalworks._6ld).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 3)
                    {
                        return QuestReply.ProgressEvent(702);
                    }

                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.IronEater).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(767);
                    }
                    else if (missionStatus == 3)
                    {
                        return QuestReply.ProgressEvent(702);
                    }
                    else if (missionStatus == 4)
                    {
                        return QuestReply.ProgressEvent(768);
                    }

                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Malduc)
                .OnTrigger(q => OffsetPlus10(q, Text.Metalworks.EXTENDED_MISSION_OFFSET))
                .OnEventFinish(Zone.Metalworks, 767, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.SetMissionStatus(AreaId, 1);
                    }
                })
                .OnEventFinish(Zone.Metalworks, 768, q => q.Mission.Complete())
                .Npc(Zone.PortBastok, Npc.PortBastok.Argus)
                .OnTrigger(q => OffsetPlus10(q, Text.PortBastok.EXTENDED_MISSION_OFFSET))
                .Npc(Zone.QuicksandCaves, Npc.QuicksandCaves.qm4).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 3)
                    {
                        return QuestReply.ProgressEvent(10);
                    }

                    return null;
                })
                .Npc(Zone.QuicksandCaves, Npc.QuicksandCaves.qm6).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 1 &&
                        !q.Player.MobIsSpawned(Mob.QuicksandCaves.CENTURIO_IV_VII) &&
                        !q.Player.MobIsSpawned(Mob.QuicksandCaves.TRIARIUS_IV_XIV) &&
                        !q.Player.MobIsSpawned(Mob.QuicksandCaves.PRINCEPS_IV_XLV))
                    {
                        q.Player.SpawnMob(Mob.QuicksandCaves.CENTURIO_IV_VII, 0, claim: true);
                        q.Player.SpawnMob(Mob.QuicksandCaves.TRIARIUS_IV_XIV, 0, claim: true);
                        q.Player.SpawnMob(Mob.QuicksandCaves.PRINCEPS_IV_XLV, 0, claim: true);
                        return QuestReply.MessageSpecial(Text.QuicksandCaves.SENSE_OF_FOREBODING);
                    }
                    else if (missionStatus == 2)
                    {
                        return QuestReply.ProgressEvent(11);
                    }

                    return null;
                })
                .Npc(Zone.QuicksandCaves, Npc.QuicksandCaves.CenturioIVVII).OnMobDeath(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1 &&
                        DeadOrNotSpawned(q, Mob.QuicksandCaves.TRIARIUS_IV_XIV) &&
                        DeadOrNotSpawned(q, Mob.QuicksandCaves.PRINCEPS_IV_XLV))
                    {
                        q.Player.SetMissionStatus(AreaId, 2);
                    }
                })
                .Npc(Zone.QuicksandCaves, Npc.QuicksandCaves.PrincepsIVXLV).OnMobDeath(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1 &&
                        DeadOrNotSpawned(q, Mob.QuicksandCaves.CENTURIO_IV_VII) &&
                        DeadOrNotSpawned(q, Mob.QuicksandCaves.TRIARIUS_IV_XIV))
                    {
                        q.Player.SetMissionStatus(AreaId, 2);
                    }
                })
                .Npc(Zone.QuicksandCaves, Npc.QuicksandCaves.TriariusIVXIV).OnMobDeath(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1 &&
                        DeadOrNotSpawned(q, Mob.QuicksandCaves.CENTURIO_IV_VII) &&
                        DeadOrNotSpawned(q, Mob.QuicksandCaves.PRINCEPS_IV_XLV))
                    {
                        q.Player.SetMissionStatus(AreaId, 2);
                    }
                })
                .OnEventFinish(Zone.QuicksandCaves, 10, q => q.Player.SetMissionStatus(AreaId, 4))
                .OnEventFinish(Zone.QuicksandCaves, 11, q => q.Player.SetMissionStatus(AreaId, 3));
        }
    }
}
