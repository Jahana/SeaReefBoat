// Trial by Wind (Cloister of Gales BCNM), a line-for-line port of
// scripts/battlefields/Cloister_of_Gales/trial_by_wind.lua (quest rework C3, Q1). A BattlefieldQuest of the Outlands
// quest Trial by Wind: 30 minutes for six, no experience lost, entered at WP_Entrance with the Tuning Fork of Wind
// (taken on entry) and left at Wind_Protocrystal. Its one group, Garuda_Prime_TBW, wins the fight when it dies. Its
// onEventFinishWin is xi.cs.questEvent(player, npc, 'outlands.trial_by_wind', csid, option): the C# quest's called
// 32001 handler.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.CloisterOfGales
{
    public sealed class TrialByWind : QuestEventTrial
    {
        public TrialByWind() : base("TRIAL_BY_WIND") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Cloister_of_Gales/trial_by_wind";

        protected override string QuestKey => "outlands.trial_by_wind";

        public override BattlefieldData Data { get; } = QuestTrials.Trial(
            Zone.CloisterOfGales, Battlefield.TrialByWind,
            Npc.CloisterOfGales.WPEntrance,
            Npc.CloisterOfGales.WindProtocrystal,
            KeyItem.TuningForkOfWind, XI.Quests.QuestLog.Outlands,
            "TRIAL_BY_WIND", "Garuda_Prime_TBW");
    }
}
