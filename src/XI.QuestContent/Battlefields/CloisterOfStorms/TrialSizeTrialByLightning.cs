// Trial-Size Trial by Lightning (Cloister of Storms BCNM), a line-for-line port of
// scripts/battlefields/Cloister_of_Storms/trial-size_trial_by_lightning.lua (quest rework C3, Q1). A BattlefieldQuest
// of the OtherAreas quest Trial-Size Trial by Lightning: 15 minutes for one, capped at level 20, no experience lost,
// index 2, entered at LP_Entrance with the Mini Tuning Fork of Lightning (taken on entry) and left at
// Lightning_Protocrystal. entryRequirement: a Summoner of level 20 or more. Its one group, Ramuh_Prime_TSTBL, wins
// the fight when it dies. Its onEventFinishWin is xi.cs.questEvent(player, npc,
// 'otherAreas.trial_size_trial_by_lightning', csid, option): the C# quest's called 32001 handler.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.CloisterOfStorms
{
    public sealed class TrialSizeTrialByLightning : QuestEventTrialSize
    {
        public TrialSizeTrialByLightning() : base("TRIAL_SIZE_TRIAL_BY_LIGHTNING") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Cloister_of_Storms/trial-size_trial_by_lightning";

        protected override string QuestKey => "otherAreas.trial_size_trial_by_lightning";

        public override BattlefieldData Data { get; } = QuestTrials.TrialSize(
            Zone.CloisterOfStorms, Battlefield.TrialSizeTrialByLightning, 2,
            Npc.CloisterOfStorms.LPEntrance,
            Npc.CloisterOfStorms.LightningProtocrystal,
            Item.MiniTuningForkOfLightning, XI.Quests.QuestLog.OtherAreas,
            "TRIAL_SIZE_TRIAL_BY_LIGHTNING", "Ramuh_Prime_TSTBL");
    }
}
