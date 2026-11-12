// Her Memories (WotG 41, Log ID 5, Mission ID 40), a line-for-line port of scripts/missions/wotg/41_Her_Memories.lua
// (quest rework C3 T7). One section (the mission current) with no handler: the Lua leaves the mission blank, since
// the Crystal War Her Memories quests carry it, and xi.wotg.helpers.checkMemoryFragments (which stays Lua) completes
// it with player:completeMission and adds Forget Me Not once the char holds the four Large Memory Fragments. The reward
// table's next mission is kept as the Lua has it, though no handler here completes the mission.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class HerMemories : MissionScript
    {
        public HerMemories() : base(XI.Quests.MissionLog.WoTG, "HER_MEMORIES") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/41_Her_Memories";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.ForgetMeNot,
            });

            b.State(current: true);
        }
    }
}
