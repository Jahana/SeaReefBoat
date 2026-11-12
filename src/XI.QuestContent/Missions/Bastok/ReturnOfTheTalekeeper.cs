// Return of the Talekeeper (Bastok 6-1, Log ID 1, Mission ID 16), a line-for-line port of
// scripts/missions/bastok/6_1_Return_of_the_Talekeeper.lua (quest rework C3 N5). Argus !pos 132.157 7.496 -2.187 236;
// Cleades !pos -358 -10 -168 235; Malduc !pos 66.200 -14.999 4.426 237; Rashid !pos -8.444 -2 -123.575 234; Medicine
// Eagle !pos -40 0 38 234; Drake Fang !pos -74 0.1 58 172; qm2 (W.Altepa) !pos -325 0 -111 125; Tall Mountain !pos 71
// 7 -7 234. Reward: 650 rank points. Section 1 (no Bastok mission current, a Bastokan): the four gate guards' 1001
// finish with option 16 (handleAcceptMission) begins the mission and says the zone's YOU_ACCEPT_THE_MISSION. Section 2
// (current): Cleades, Rashid, Malduc and Argus say their zone's EXTENDED_MISSION_OFFSET (messageSpecial); Medicine
// Eagle plays 180 (progress) at status 0, else 181 (event), and 180's finish sets status 1; Tall Mountain at status 3
// plays 182 (progress), whose finish completes the mission and then takes the Altepa Moonpebble. Zeruhn Mines: Drake
// Fang plays 201 above status 1 and 200 at status 1 (progress); 200's finish sets status 2. Western Altepa Desert: qm2
// with the nmDefeated local var 1 sets status 3 and gives the Altepa Moonpebble (keyItem); else, both Sphinxes
// existing, at status 2 with each one not spawned or dead, it spawns and claims the Eastern and the Western Sphinx and
// says EVIL_LOOMING_ABOVE_YOU. Each Sphinx's death at status 2, the other one existing and dead or not spawned, sets
// the nmDefeated local var 1. Kept as the Lua has it: section 1 compares the nation with mission.areaId (the log id,
// 1, which is xi.nation.BASTOK) and has no rank check and no completed check; qm2's keyItem arm leaves the
// nmDefeated local var at 1.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Bastok
{
    public sealed class ReturnOfTheTalekeeper : MissionScript
    {
        public ReturnOfTheTalekeeper() : base(XI.Quests.MissionLog.Bastok, "RETURN_OF_THE_TALEKEEPER") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/bastok/6_1_Return_of_the_Talekeeper";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Bastok;
        const int MissionId = Mission.Bastok.ReturnOfTheTalekeeper;

        // handleAcceptMission: option 16 begins the mission, with the char's zone's YOU_ACCEPT_THE_MISSION
        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 16)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 650 });

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
                .OnTrigger(QuestReply.MessageSpecial(Text.BastokMarkets.EXTENDED_MISSION_OFFSET))
                .Npc(Zone.BastokMines, Npc.BastokMines.MedicineEagle).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 0)
                    {
                        return QuestReply.ProgressEvent(180);
                    }
                    else
                    {
                        return QuestReply.Event(181);
                    }
                })
                .Npc(Zone.BastokMines, Npc.BastokMines.Rashid)
                .OnTrigger(QuestReply.MessageSpecial(Text.BastokMines.EXTENDED_MISSION_OFFSET))
                .Npc(Zone.BastokMines, Npc.BastokMines.TallMountain).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 3)
                    {
                        return QuestReply.ProgressEvent(182);
                    }

                    return null;
                })
                .OnEventFinish(Zone.BastokMines, 180, q => q.Player.SetMissionStatus(AreaId, 1))
                .OnEventFinish(Zone.BastokMines, 182, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.AltepaMoonpebble);
                    }
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Malduc)
                .OnTrigger(QuestReply.MessageSpecial(Text.Metalworks.EXTENDED_MISSION_OFFSET))
                .Npc(Zone.PortBastok, Npc.PortBastok.Argus)
                .OnTrigger(QuestReply.MessageSpecial(Text.PortBastok.EXTENDED_MISSION_OFFSET))
                .Npc(Zone.ZeruhnMines, Npc.ZeruhnMines.DrakeFang).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus > 1)
                    {
                        return QuestReply.ProgressEvent(201);
                    }
                    else if (missionStatus == 1)
                    {
                        return QuestReply.ProgressEvent(200);
                    }

                    return null;
                })
                .OnEventFinish(Zone.ZeruhnMines, 200, q => q.Player.SetMissionStatus(AreaId, 2))
                .Npc(Zone.WesternAltepaDesert, Npc.WesternAltepaDesert.qm2).OnTrigger(q =>
                {
                    if (q.Mission.LocalVar("nmDefeated") == 1)
                    {
                        q.Player.SetMissionStatus(AreaId, 3);
                        return QuestReply.KeyItem(KeyItem.AltepaMoonpebble);
                    }
                    else if (q.Player.MobExists(Mob.WesternAltepaDesert.EASTERN_SPHINX) &&
                             q.Player.MobExists(Mob.WesternAltepaDesert.WESTERN_SPHINX) &&
                             q.Player.MissionStatus(AreaId) == 2 &&
                             (!q.Player.MobIsSpawned(Mob.WesternAltepaDesert.EASTERN_SPHINX) ||
                              q.Player.MobIsDead(Mob.WesternAltepaDesert.EASTERN_SPHINX)) &&
                             (!q.Player.MobIsSpawned(Mob.WesternAltepaDesert.WESTERN_SPHINX) ||
                              q.Player.MobIsDead(Mob.WesternAltepaDesert.WESTERN_SPHINX)))
                    {
                        q.Player.SpawnMob(Mob.WesternAltepaDesert.EASTERN_SPHINX, 0, claim: true);
                        q.Player.SpawnMob(Mob.WesternAltepaDesert.WESTERN_SPHINX, 0, claim: true);
                        return QuestReply.MessageSpecial(Text.WesternAltepaDesert.EVIL_LOOMING_ABOVE_YOU);
                    }

                    return null;
                })
                .Npc(Zone.WesternAltepaDesert, Npc.WesternAltepaDesert.EasternSphinx).OnMobDeath(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 2 &&
                        q.Player.MobExists(Mob.WesternAltepaDesert.WESTERN_SPHINX) &&
                        (q.Player.MobIsDead(Mob.WesternAltepaDesert.WESTERN_SPHINX) ||
                         !q.Player.MobIsSpawned(Mob.WesternAltepaDesert.WESTERN_SPHINX)))
                    {
                        q.Mission.SetLocalVar("nmDefeated", 1);
                    }
                })
                .Npc(Zone.WesternAltepaDesert, Npc.WesternAltepaDesert.WesternSphinx).OnMobDeath(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 2 &&
                        q.Player.MobExists(Mob.WesternAltepaDesert.EASTERN_SPHINX) &&
                        (q.Player.MobIsDead(Mob.WesternAltepaDesert.EASTERN_SPHINX) ||
                         !q.Player.MobIsSpawned(Mob.WesternAltepaDesert.EASTERN_SPHINX)))
                    {
                        q.Mission.SetLocalVar("nmDefeated", 1);
                    }
                });
        }
    }
}
