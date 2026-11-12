// Finders Keepers (ToAU 21, Log ID 4, Mission ID 20), a line-for-line port of
// scripts/missions/toau/21_Finders_Keepers.lua (quest rework C3 T2). One section (the mission current): Aht Urhgan
// Whitegate's trigger area 3 plays the progress event 3093 (1, 1), whose finish completes the mission (Shield of
// Diplomacy current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class FindersKeepers : MissionScript
    {
        public FindersKeepers() : base(XI.Quests.MissionLog.ToAU, "FINDERS_KEEPERS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/21_Finders_Keepers";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.ShieldOfDiplomacy,
            });

            b.State(current: true)
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 3, q =>
                {
                    return QuestReply.ProgressEvent(3093, 1, 1, 0, 0, 0, 0, 0, 0);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3093, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
