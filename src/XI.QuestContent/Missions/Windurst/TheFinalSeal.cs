// The Final Seal (Windurst 5-1, Log ID 2, Mission ID 14), a line-for-line port of
// scripts/missions/windurst/5_1_The_Final_Seal.lua (quest rework C3 N4). Mokyokyo !pos -55 -8 227 238;
// Janshura-Rashura !pos -227 -8 184 240; Rakoh Buuma !pos 106 -5 -23 241; Zokima-Rokima !pos 0 -16 124 239; Vestal
// Chamber (_6q2) !pos 0.1 -49 37 242. Reward: 600 rank points. Section 1 (no Windurst mission current, a Windurstian
// of rank 5 who has not completed it): the gate guards Mokyokyo 232, Janshura-Rashura 163, Rakoh Buuma 197 and
// Zokima-Rokima 150 (progress); the Vestal Chamber door _6q2 plays 166 with the Message to Jeuno, else 190 (progress).
// The finish of either (handleAcceptMission) with option 0 begins the mission, sets status 10, says the zone's
// YOU_ACCEPT_THE_MISSION and gives the New Fei'Yin Seal; on any option it then takes the message. Section 2 (current):
// _6q2 at status 12 with the Burnt Seal plays 192 (progress), whose finish completes the mission and then takes the
// Burnt Seal. Fei'Yin's zone-in at status 10 plays 1, whose finish sets status 11. Qu'Bia Arena's 32001 finish at
// status 11 with battlefieldWin RANK_5_MISSION (512) gives the Burnt Seal, sets status 12 and takes the New Fei'Yin
// Seal. Kept as the Lua has it: section 1 compares the nation with mission.areaId (the log id, 2, which is
// xi.nation.WINDURST); a declined 166 still takes the message.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Windurst
{
    public sealed class TheFinalSeal : MissionScript
    {
        public TheFinalSeal() : base(XI.Quests.MissionLog.Windurst, "THE_FINAL_SEAL") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/windurst/5_1_The_Final_Seal";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Windurst;
        const int MissionId = Mission.Windurst.TheFinalSeal;

        static void HandleAcceptMission(IQuestContext q)
        {
            if (q.Option == 0)
            {
                q.Mission.Begin();
                q.Player.SetMissionStatus(AreaId, 10);
                q.Text.Special(Text.HeavensTower.YOU_ACCEPT_THE_MISSION);
                q.Player.GiveKeyItem(KeyItem.NewFeiyinSeal);
            }

            q.Player.DelKeyItem(KeyItem.MessageToJeunoWindurst);
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 600 });

            // Player meets requirements, but has not started the mission
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Windurst) == Mission.Windurst.None &&
                    q.Player.Nation == AreaId &&
                    q.Player.Rank(q.Player.Nation) == 5 &&
                    !q.Player.HasCompletedMission(AreaId, MissionId))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Mokyokyo).OnTrigger(q => QuestReply.ProgressEvent(232))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.JanshuraRashura).OnTrigger(q => QuestReply.ProgressEvent(163))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.RakohBuuma).OnTrigger(q => QuestReply.ProgressEvent(197))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.ZokimaRokima).OnTrigger(q => QuestReply.ProgressEvent(150))
                .Npc(Zone.HeavensTower, Npc.HeavensTower._6q2).OnTrigger(q =>
                {
                    // This mission is handled a bit differently when compared to Bastok and San d'Oria. Without the
                    // need of gate guard interaction, KI tracking is used to determine whether its the first or
                    // subsequent time the door has been triggered.
                    if (q.Player.HasKeyItem(KeyItem.MessageToJeunoWindurst))
                    {
                        return QuestReply.ProgressEvent(166);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(190);
                    }
                })
                .OnEventFinish(Zone.HeavensTower, 166, HandleAcceptMission)
                .OnEventFinish(Zone.HeavensTower, 190, HandleAcceptMission);

            // Mission has been accepted
            b.State(current: true)
                .Npc(Zone.HeavensTower, Npc.HeavensTower._6q2).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 12 &&
                        q.Player.HasKeyItem(KeyItem.BurntSeal))
                    {
                        return QuestReply.ProgressEvent(192);
                    }

                    return null;
                })
                .OnEventFinish(Zone.HeavensTower, 192, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.BurntSeal);
                    }
                })
                .OnZoneIn(Zone.Feiyin, q =>
                    q.Player.MissionStatus(AreaId) == 10 ? QuestReply.Event(1) : null)
                .OnEventFinish(Zone.Feiyin, 1, q => q.Player.SetMissionStatus(AreaId, 11))
                .OnEventFinish(Zone.QubiaArena, 32001, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 11 &&
                        q.Player.LocalVar("battlefieldWin") == Battlefield.Rank5Mission)
                    {
                        q.Player.GiveKeyItem(KeyItem.BurntSeal);
                        q.Player.SetMissionStatus(AreaId, 12);
                        q.Player.DelKeyItem(KeyItem.NewFeiyinSeal);
                    }
                });
        }
    }
}
