// A Timeswept Butterfly (WotG 6, Log ID 5, Mission ID 5), a line-for-line port of
// scripts/missions/wotg/06_A_Timeswept_Butterfly.lua (quest rework C3 T5). One section (the mission current, any
// status): the zone-in to La Vaule [S] from Jugner Forest [S] plays 1; its finish completes the mission (Purple, The
// New Black current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class ATimesweptButterfly : MissionScript
    {
        public ATimesweptButterfly() : base(XI.Quests.MissionLog.WoTG, "A_TIMESWEPT_BUTTERFLY") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/06_A_Timeswept_Butterfly";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.PurpleTheNewBlack,
            });

            b.State(current: true)
                .OnZoneIn(Zone.LaVauleS, q =>
                    q.Player.PreviousZone == Zone.JugnerForestS ? QuestReply.Event(1) : null)
                .OnEventFinish(Zone.LaVauleS, 1, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
