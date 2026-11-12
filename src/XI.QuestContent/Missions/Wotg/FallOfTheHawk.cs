// Fall of the Hawk (WotG 36, Log ID 5, Mission ID 35), a line-for-line port of
// scripts/missions/wotg/36_Fall_of_the_Hawk.lua (quest rework C3 T7). One section (the mission current):
// every zone-in to Castle Zvahl Baileys [S] plays the plain event 4, whose finish completes the mission (Darkness
// Descends current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class FallOfTheHawk : MissionScript
    {
        public FallOfTheHawk() : base(XI.Quests.MissionLog.WoTG, "FALL_OF_THE_HAWK") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/36_Fall_of_the_Hawk";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.DarknessDescends,
            });

            b.State(current: true)
                .OnZoneIn(Zone.CastleZvahlBaileysS, q => QuestReply.Event(4))
                .OnEventFinish(Zone.CastleZvahlBaileysS, 4, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
