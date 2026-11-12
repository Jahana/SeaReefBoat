// Journey to Windurst, part 2 (San d'Oria 2-3, Log ID 0, Mission ID 9), a line-for-line port of
// scripts/missions/sandoria/2_3_4_Journey_to_Windurst2.lua (quest rework C3 N2). Kupipi !pos 2 0.1 30 242; Mourices
// !pos -50.646 -0.501 -27.642 241. One section (this mission current), no reward table. Heaven's Tower: Kupipi at
// status 7 plays 242 (progress, parameters 1 to 4 = 1 and 5 = 0; its finish gives the Dark Key and moves to status 8),
// at 8 243 (an event), at 9 246 (progress; its finish moves to status 10), at 10 247 (an event). Balga's Dais: the
// 32001 finish, at status 8 after the Rank 2 Mission battlefield (id 96), gives the Kindred Crest, takes the Dark Key
// and moves to status 9. Windurst Woods: Mourices at status 8 plays 463 (progress), at 9 or 10 467 (progress; its
// finish completes the mission, begins Journey Abroad, takes the Kindred Crest, moves to status 11 and gives the
// Kindred Report). The status reads are the mission's log (mission.areaId, 0).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Sandoria
{
    public sealed class JourneyToWindurst2 : MissionScript
    {
        public JourneyToWindurst2() : base(XI.Quests.MissionLog.Sandoria, "JOURNEY_TO_WINDURST2") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/sandoria/2_3_4_Journey_to_Windurst2";

        protected override void Build(MissionScriptBuilder b)
        {
            b.State(current: true)
                .OnEventFinish(Zone.BalgasDais, 32001, q =>
                {
                    if (q.Player.MissionStatus(MissionLog.Sandoria) == 8 &&
                        q.Player.LocalVar("battlefieldWin") == Battlefield.Rank2Mission)
                    {
                        q.Player.GiveKeyItem(KeyItem.KindredCrest);
                        q.Player.DelKeyItem(KeyItem.DarkKey);
                        q.Player.SetMissionStatus(MissionLog.Sandoria, 9);
                    }
                })
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Kupipi).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Sandoria);

                    if (missionStatus == 7)
                    {
                        return QuestReply.ProgressEvent(242, 1, 1, 1, 1, 0);
                    }
                    else if (missionStatus == 8)
                    {
                        return QuestReply.Event(243);
                    }
                    else if (missionStatus == 9)
                    {
                        return QuestReply.ProgressEvent(246);
                    }
                    else if (missionStatus == 10)
                    {
                        return QuestReply.Event(247);
                    }

                    return null;
                })
                .OnEventFinish(Zone.HeavensTower, 242, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.DarkKey);
                    q.Player.SetMissionStatus(MissionLog.Sandoria, 8);
                })
                .OnEventFinish(Zone.HeavensTower, 246, q =>
                    q.Player.SetMissionStatus(MissionLog.Sandoria, 10))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Mourices).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Sandoria);

                    if (missionStatus == 8)
                    {
                        return QuestReply.ProgressEvent(463);
                    }
                    else if (missionStatus == 9 || missionStatus == 10)
                    {
                        return QuestReply.ProgressEvent(467);
                    }

                    return null;
                })
                .OnEventFinish(Zone.WindurstWoods, 467, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.AddMission(MissionLog.Sandoria, Mission.Sandoria.JourneyAbroad);
                        q.Player.DelKeyItem(KeyItem.KindredCrest);
                        q.Player.SetMissionStatus(MissionLog.Sandoria, 11);
                        q.Player.GiveKeyItem(KeyItem.KindredReport);
                    }
                });
        }
    }
}
