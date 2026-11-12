// Northland Exposure (WotG 17, Log ID 5, Mission ID 16), a line-for-line port of
// scripts/missions/wotg/17_Northland_Exposure.lua (quest rework C3 T6). One section (the mission current): the
// Beaucedine Glacier [S] zone-in plays the plain event 13, whose finish completes the mission (Traitor in the Midst
// current, the Shadow Bug given).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class NorthlandExposure : MissionScript
    {
        public NorthlandExposure() : base(XI.Quests.MissionLog.WoTG, "NORTHLAND_EXPOSURE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/17_Northland_Exposure";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.ShadowBug,
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.TraitorInTheMidst,
            });

            b.State(current: true)
                .OnZoneIn(Zone.BeaucedineGlacierS, q => QuestReply.Event(13))
                .OnEventFinish(Zone.BeaucedineGlacierS, 13, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
