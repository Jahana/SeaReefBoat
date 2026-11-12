// Trial-Size Trial by Water (Cloister of Tides BCNM), a line-for-line port of
// scripts/battlefields/Cloister_of_Tides/trial-size_trial_by_water.lua (quest rework C3, Q1). A BattlefieldQuest of
// the Outlands quest Trial-Size Trial by Water: 15 minutes for one, capped at level 20, no experience lost, index 1,
// entered at WP_Entrance with the Mini Tuning Fork of Water (taken on entry) and left at Water_Protocrystal.
// entryRequirement: a Summoner of level 20 or more. Its one group, Leviathan_Prime_TSTBW, wins the fight when it
// dies. Its onEventFinishWin is xi.cs.questEvent(player, npc, 'outlands.trial_size_trial_by_water', csid, option):
// the C# quest's called 32001 handler.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.CloisterOfTides
{
    public sealed class TrialSizeTrialByWater : QuestEventTrialSize
    {
        public TrialSizeTrialByWater() : base("TRIAL_SIZE_TRIAL_BY_WATER") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Cloister_of_Tides/trial-size_trial_by_water";

        protected override string QuestKey => "outlands.trial_size_trial_by_water";

        public override BattlefieldData Data { get; } = QuestTrials.TrialSize(
            Zone.CloisterOfTides, Battlefield.TrialSizeTrialByWater, 1,
            Npc.CloisterOfTides.WPEntrance,
            Npc.CloisterOfTides.WaterProtocrystal,
            Item.MiniTuningForkOfWater, XI.Quests.QuestLog.Outlands,
            "TRIAL_SIZE_TRIAL_BY_WATER", "Leviathan_Prime_TSTBW");
    }
}
