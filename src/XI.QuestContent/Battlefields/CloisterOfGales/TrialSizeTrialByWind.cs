// Trial-Size Trial by Wind (Cloister of Gales BCNM), a line-for-line port of
// scripts/battlefields/Cloister_of_Gales/trial-size_trial_by_wind.lua (quest rework C3, Q1). A BattlefieldQuest of
// the Outlands quest Trial-Size Trial by Wind: 15 minutes for one, capped at level 20, no experience lost, index 2,
// entered at WP_Entrance with the Mini Tuning Fork of Wind (taken on entry) and left at Wind_Protocrystal.
// entryRequirement: a Summoner of level 20 or more. Its one group, Garuda_Prime_TSTBW, wins the fight when it dies.
// Its onEventFinishWin is xi.cs.questEvent(player, npc, 'outlands.trial_size_trial_by_wind', csid, option): the C#
// quest's called 32001 handler.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.CloisterOfGales
{
    public sealed class TrialSizeTrialByWind : QuestEventTrialSize
    {
        public TrialSizeTrialByWind() : base("TRIAL_SIZE_TRIAL_BY_WIND") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Cloister_of_Gales/trial-size_trial_by_wind";

        protected override string QuestKey => "outlands.trial_size_trial_by_wind";

        public override BattlefieldData Data { get; } = QuestTrials.TrialSize(
            Zone.CloisterOfGales, Battlefield.TrialSizeTrialByWind, 2,
            Npc.CloisterOfGales.WPEntrance,
            Npc.CloisterOfGales.WindProtocrystal,
            Item.MiniTuningForkOfWind, XI.Quests.QuestLog.Outlands,
            "TRIAL_SIZE_TRIAL_BY_WIND", "Garuda_Prime_TSTBW");
    }
}
