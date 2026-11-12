// Trial by Water (Cloister of Tides BCNM), a line-for-line port of
// scripts/battlefields/Cloister_of_Tides/trial_by_water.lua (quest rework C3, Q1). A BattlefieldQuest of the Outlands
// quest Trial by Water: 30 minutes for six, no experience lost, entered at WP_Entrance with the Tuning Fork of Water
// (taken on entry) and left at Water_Protocrystal. Its one group, Leviathan_Prime_TBW, wins the fight when it dies.
// Its onEventFinishWin is xi.cs.questEvent(player, npc, 'outlands.trial_by_water', csid, option): the C# quest's
// called 32001 handler.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.CloisterOfTides
{
    public sealed class TrialByWater : QuestEventTrial
    {
        public TrialByWater() : base("TRIAL_BY_WATER") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Cloister_of_Tides/trial_by_water";

        protected override string QuestKey => "outlands.trial_by_water";

        public override BattlefieldData Data { get; } = QuestTrials.Trial(
            Zone.CloisterOfTides, Battlefield.TrialByWater,
            Npc.CloisterOfTides.WPEntrance,
            Npc.CloisterOfTides.WaterProtocrystal,
            KeyItem.TuningForkOfWater, XI.Quests.QuestLog.Outlands,
            "TRIAL_BY_WATER", "Leviathan_Prime_TBW");
    }
}
