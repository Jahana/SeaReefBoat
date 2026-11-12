// A Sanguinary Prelude (WotG 22, Log ID 5, Mission ID 21), a line-for-line port of
// scripts/missions/wotg/22_A_Sanguinary_Prelude.lua (quest rework C3 T6). One section (the mission current): the
// Beaucedine Glacier [S] zone-in plays the plain event 17, whose finish completes the mission (Dungeons and Dancers
// current, the Aroma Bug given).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class ASanguinaryPrelude : MissionScript
    {
        public ASanguinaryPrelude() : base(XI.Quests.MissionLog.WoTG, "A_SANGUINARY_PRELUDE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/22_A_Sanguinary_Prelude";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.AromaBug,
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.DungeonsAndDancers,
            });

            b.State(current: true)
                .OnZoneIn(Zone.BeaucedineGlacierS, q => QuestReply.Event(17))
                .OnEventFinish(Zone.BeaucedineGlacierS, 17, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
