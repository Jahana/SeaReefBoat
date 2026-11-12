// Trial-Size Trial by Fire (Cloister of Flames BCNM), a line-for-line port of
// scripts/battlefields/Cloister_of_Flames/trial-size_trial_by_fire.lua (quest rework C3, Q1). A BattlefieldQuest of
// the Outlands quest Trial-Size Trial by Fire: 15 minutes for one, capped at level 20, no experience lost, index 1,
// entered at FP_Entrance with the Mini Tuning Fork of Fire (taken on entry) and left at Fire_Protocrystal.
// entryRequirement: a Summoner of level 20 or more. Its one group, Ifrit_Prime_TSTBF, wins the fight when it dies.
// Its onEventFinishWin is xi.cs.questEvent(player, npc, 'outlands.trial_size_trial_by_fire', csid, option): the C#
// quest's called 32001 handler.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.CloisterOfFlames
{
    public sealed class TrialSizeTrialByFire : QuestEventTrialSize
    {
        public TrialSizeTrialByFire() : base("TRIAL_SIZE_TRIAL_BY_FIRE") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Cloister_of_Flames/trial-size_trial_by_fire";

        protected override string QuestKey => "outlands.trial_size_trial_by_fire";

        public override BattlefieldData Data { get; } = QuestTrials.TrialSize(
            Zone.CloisterOfFlames, Battlefield.TrialSizeTrialByFire, 1,
            Npc.CloisterOfFlames.FPEntrance,
            Npc.CloisterOfFlames.FireProtocrystal,
            Item.MiniTuningForkOfFire, XI.Quests.QuestLog.Outlands,
            "TRIAL_SIZE_TRIAL_BY_FIRE", "Ifrit_Prime_TSTBF");
    }
}
