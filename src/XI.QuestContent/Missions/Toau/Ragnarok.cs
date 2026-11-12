// Ragnarok (ToAU 45, Log ID 4, Mission ID 44), a line-for-line port of scripts/missions/toau/45_Ragnarok.lua (quest
// rework C3 T4). One section (the mission current), all in Aht Urhgan Whitegate: trigger area 3 plays the progress
// event 3139, whose finish completes the mission (Imperial Coronation current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class Ragnarok : MissionScript
    {
        public Ragnarok() : base(XI.Quests.MissionLog.ToAU, "RAGNAROK") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/45_Ragnarok";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.ImperialCoronation,
            });

            b.State(current: true)
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 3, q =>
                {
                    return QuestReply.ProgressEvent(3139, 0, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3139, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
