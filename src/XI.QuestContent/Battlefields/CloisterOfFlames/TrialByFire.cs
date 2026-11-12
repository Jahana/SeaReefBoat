// Trial by Fire (Cloister of Flames BCNM), a line-for-line port of
// scripts/battlefields/Cloister_of_Flames/trial_by_fire.lua (quest rework C3, Q1). A BattlefieldQuest of the Outlands
// quest Trial by Fire: 30 minutes for six, no experience lost, entered at FP_Entrance with the Tuning Fork of Fire
// (taken on entry) and left at Fire_Protocrystal. Its one group, Ifrit_Prime_TBF, wins the fight when it dies. Its
// onEventFinishWin is the framework's (the Lua file overrides none): the quest's own 32001 handler runs from the
// quest engine.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.CloisterOfFlames
{
    public sealed class TrialByFire : BattlefieldScript
    {
        public TrialByFire() : base("TRIAL_BY_FIRE") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Cloister_of_Flames/trial_by_fire";

        public override BattlefieldData Data { get; } = QuestTrials.Trial(
            Zone.CloisterOfFlames, Battlefield.TrialByFire,
            Npc.CloisterOfFlames.FPEntrance,
            Npc.CloisterOfFlames.FireProtocrystal,
            KeyItem.TuningForkOfFire, XI.Quests.QuestLog.Outlands,
            "TRIAL_BY_FIRE", "Ifrit_Prime_TBF");
    }
}
