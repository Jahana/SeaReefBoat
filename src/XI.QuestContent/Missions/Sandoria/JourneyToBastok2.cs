// Journey to Bastok, part 2 (San d'Oria 2-3, Log ID 0, Mission ID 8), a line-for-line port of
// scripts/missions/sandoria/2_3_3_Journey_to_Bastok2.lua (quest rework C3 N2). All in Metalworks: Grohm !pos -18 -11
// -27, Pius !pos 99 -21 -12, Savae E Paleade !pos 23.724 -17.39 -43.360. One section (this mission current), no reward
// table. Chantain and Riault play 217 and 211 (events) and Lutia 214 (progress, no finish handler) at status 8 to 10.
// Grohm at status 9 plays 426 (progress; its finish moves to status 10), else 427 (an event). Pius at status 8 plays
// 355 (progress; its finish moves to status 9), below status 11 356 (an event). Savae E Paleade at 8 to 10 plays 208
// (an event), at 11 207 (progress; its finish completes the mission, begins Journey Abroad, takes the Kindred Crest,
// gives the Kindred Report and sets status 11). Waughroon Shrine: the 32001 finish, at status 10 after the Rank 2
// Mission 2 battlefield (id 64), gives the Kindred Crest, takes the Dark Key and moves to status 11. Status reads are
// the mission's log.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Sandoria
{
    public sealed class JourneyToBastok2 : MissionScript
    {
        public JourneyToBastok2() : base(XI.Quests.MissionLog.Sandoria, "JOURNEY_TO_BASTOK2") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/sandoria/2_3_3_Journey_to_Bastok2";

        protected override void Build(MissionScriptBuilder b)
        {
            b.State(current: true)
                .Npc(Zone.Metalworks, Npc.Metalworks.Chantain).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Sandoria);

                    if (missionStatus >= 8 && missionStatus <= 10)
                    {
                        return QuestReply.Event(217);
                    }

                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Lutia).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Sandoria);

                    if (missionStatus >= 8 && missionStatus <= 10)
                    {
                        return QuestReply.ProgressEvent(214);
                    }

                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Grohm).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(MissionLog.Sandoria) == 9)
                    {
                        return QuestReply.ProgressEvent(426, 0);
                    }
                    else
                    {
                        return QuestReply.Event(427);
                    }
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Pius).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Sandoria);

                    if (missionStatus == 8)
                    {
                        return QuestReply.ProgressEvent(355);
                    }
                    else if (missionStatus < 11)
                    {
                        return QuestReply.Event(356);
                    }

                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Riault).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Sandoria);

                    if (missionStatus >= 8 && missionStatus <= 10)
                    {
                        return QuestReply.Event(211);
                    }

                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.SavaeEPaleade).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Sandoria);

                    if (missionStatus >= 8 && missionStatus <= 10)
                    {
                        return QuestReply.Event(208);
                    }
                    else if (missionStatus == 11)
                    {
                        return QuestReply.ProgressEvent(207);
                    }

                    return null;
                })
                .OnEventFinish(Zone.Metalworks, 207, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.AddMission(MissionLog.Sandoria, Mission.Sandoria.JourneyAbroad);
                        q.Player.DelKeyItem(KeyItem.KindredCrest);
                        q.Player.GiveKeyItem(KeyItem.KindredReport);
                        q.Player.SetMissionStatus(MissionLog.Sandoria, 11);
                    }
                })
                .OnEventFinish(Zone.Metalworks, 355, q =>
                    q.Player.SetMissionStatus(MissionLog.Sandoria, 9))
                .OnEventFinish(Zone.Metalworks, 426, q =>
                    q.Player.SetMissionStatus(MissionLog.Sandoria, 10))
                .OnEventFinish(Zone.WaughroonShrine, 32001, q =>
                {
                    if (q.Player.MissionStatus(MissionLog.Sandoria) == 10 &&
                        q.Player.LocalVar("battlefieldWin") == Battlefield.Rank2Mission2)
                    {
                        q.Player.GiveKeyItem(KeyItem.KindredCrest);
                        q.Player.DelKeyItem(KeyItem.DarkKey);
                        q.Player.SetMissionStatus(MissionLog.Sandoria, 11);
                    }
                });
        }
    }
}
