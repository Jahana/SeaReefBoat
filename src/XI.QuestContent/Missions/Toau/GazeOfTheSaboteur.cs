// Gaze of the Saboteur (ToAU 36, Log ID 4, Mission ID 35), a line-for-line port of
// scripts/missions/toau/36_Gaze_of_the_Saboteur.lua (quest rework C3 T3). One section (the mission current). Aht Urhgan
// Whitegate: Naja Salaheem plays the plain event 3133 (the mercenary rank and 1; the same event as Legacy of the
// Lost's). Hazhalm Testing Grounds: the zone-in at status 0 plays the plain event 6, whose finish sets status 1; the
// door _260 at status 1 plays the progress event 7, whose finish takes the Percipient Eye and completes the mission
// (Path of Blood current, the Luminian Dagger key item, the Emissary of the Empress title).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class GazeOfTheSaboteur : MissionScript
    {
        public GazeOfTheSaboteur() : base(XI.Quests.MissionLog.ToAU, "GAZE_OF_THE_SABOTEUR") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/36_Gaze_of_the_Saboteur";

        const int AreaId = MissionLog.Toau;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.LuminianDagger,
                Title = Title.EmissaryOfTheEmpress,
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.PathOfBlood,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    return QuestReply.Event(3133, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .Npc(Zone.HazhalmTestingGrounds, Npc.HazhalmTestingGrounds._260).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1)
                    {
                        return QuestReply.ProgressEvent(7);
                    }

                    return null;
                })
                .OnZoneIn(Zone.HazhalmTestingGrounds, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 0)
                    {
                        return QuestReply.Event(6);
                    }

                    return null;
                })
                .OnEventFinish(Zone.HazhalmTestingGrounds, 6, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 1);
                })
                .OnEventFinish(Zone.HazhalmTestingGrounds, 7, q =>
                {
                    q.Player.DelKeyItem(KeyItem.PercipientEye);
                    q.Mission.Complete();
                });
        }
    }
}
