// Trial-Size Trial by Ice (Cloister of Frost BCNM), a line-for-line port of
// scripts/battlefields/Cloister_of_Frost/trial-size_trial_by_ice.lua (quest rework C3, Q1). A BattlefieldQuest of the
// Sandoria quest Trial-Size Trial by Ice: 15 minutes for one, capped at level 20, no experience lost, index 2,
// entered at IP_Entrance with the Mini Tuning Fork of Ice (taken on entry) and left at Ice_Protocrystal.
// entryRequirement: a Summoner of level 20 or more. Its one group, Shiva_Prime_TSTBI, wins the fight when it dies.
// Its onEventFinishWin is xi.cs.questEvent(player, npc, 'sandoria.trial_size_trial_by_ice', csid, option): the C#
// quest's called 32001 handler.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.CloisterOfFrost
{
    public sealed class TrialSizeTrialByIce : QuestEventTrialSize
    {
        public TrialSizeTrialByIce() : base("TRIAL_SIZE_TRIAL_BY_ICE") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Cloister_of_Frost/trial-size_trial_by_ice";

        protected override string QuestKey => "sandoria.trial_size_trial_by_ice";

        public override BattlefieldData Data { get; } = QuestTrials.TrialSize(
            Zone.CloisterOfFrost, Battlefield.TrialSizeTrialByIce, 2,
            Npc.CloisterOfFrost.IPEntrance,
            Npc.CloisterOfFrost.IceProtocrystal,
            Item.MiniTuningForkOfIce, XI.Quests.QuestLog.Sandoria,
            "TRIAL_SIZE_TRIAL_BY_ICE", "Shiva_Prime_TSTBI");
    }
}
