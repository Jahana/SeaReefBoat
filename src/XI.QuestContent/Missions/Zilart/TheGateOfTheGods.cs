// The Gate of the Gods (Zilart 13, Log ID 3, Mission ID 24), a line-for-line port of
// scripts/missions/rotz/13_The_Gate_of_the_Gods.lua (quest rework C3, C6). Shimmering_Circle !pos 0 -20 147 251;
// Ru'Aun Gardens !zone 130. Section 1 (current): Ru'Aun Gardens' zone-in plays 51, whose finish completes the mission
// (Ark Angels next; no key item, no title).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Zilart
{
    public sealed class TheGateOfTheGods : MissionScript
    {
        public TheGateOfTheGods() : base(XI.Quests.MissionLog.Zilart, "THE_GATE_OF_THE_GODS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/rotz/13_The_Gate_of_the_Gods";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.Zilart,
                NextMission = Mission.Zilart.ArkAngels,
            });

            b.State(current: true)
                .OnZoneIn(Zone.RuaunGardens, q => QuestReply.Event(51))
                .OnEventFinish(Zone.RuaunGardens, 51, q => q.Mission.Complete());
        }
    }
}
