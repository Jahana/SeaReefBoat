// Trial by Lightning (Cloister of Storms BCNM), a line-for-line port of
// scripts/battlefields/Cloister_of_Storms/trial_by_lightning.lua (quest rework C3, Q1). A BattlefieldQuest of the
// OtherAreas quest Trial by Lightning: 30 minutes for six, no experience lost, entered at LP_Entrance with the Tuning
// Fork of Lightning (taken on entry) and left at Lightning_Protocrystal. Its one group, Ramuh_Prime_TBL, wins the
// fight when it dies. Its onEventFinishWin is xi.cs.questEvent(player, npc, 'otherAreas.trial_by_lightning', csid,
// option): the C# quest's called 32001 handler.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.CloisterOfStorms
{
    public sealed class TrialByLightning : QuestEventTrial
    {
        public TrialByLightning() : base("TRIAL_BY_LIGHTNING") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Cloister_of_Storms/trial_by_lightning";

        protected override string QuestKey => "otherAreas.trial_by_lightning";

        public override BattlefieldData Data { get; } = QuestTrials.Trial(
            Zone.CloisterOfStorms, Battlefield.TrialByLightning,
            Npc.CloisterOfStorms.LPEntrance,
            Npc.CloisterOfStorms.LightningProtocrystal,
            KeyItem.TuningForkOfLightning, XI.Quests.QuestLog.OtherAreas,
            "TRIAL_BY_LIGHTNING", "Ramuh_Prime_TBL");
    }
}
