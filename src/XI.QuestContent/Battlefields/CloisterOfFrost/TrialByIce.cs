// Trial by Ice (Cloister of Frost BCNM), a line-for-line port of
// scripts/battlefields/Cloister_of_Frost/trial_by_ice.lua (quest rework C3, Q1). A BattlefieldQuest of the Sandoria
// quest Trial by Ice: 30 minutes for six, no experience lost, entered at IP_Entrance with the Tuning Fork of Ice
// (taken on entry) and left at Ice_Protocrystal. Its one group, Shiva_Prime_TBI, wins the fight when it dies. Its
// onEventFinishWin is xi.cs.questEvent(player, npc, 'sandoria.trial_by_ice', csid, option): the C# quest's called
// 32001 handler.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.CloisterOfFrost
{
    public sealed class TrialByIce : QuestEventTrial
    {
        public TrialByIce() : base("TRIAL_BY_ICE") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Cloister_of_Frost/trial_by_ice";

        protected override string QuestKey => "sandoria.trial_by_ice";

        public override BattlefieldData Data { get; } = QuestTrials.Trial(
            Zone.CloisterOfFrost, Battlefield.TrialByIce,
            Npc.CloisterOfFrost.IPEntrance,
            Npc.CloisterOfFrost.IceProtocrystal,
            KeyItem.TuningForkOfIce, XI.Quests.QuestLog.Sandoria,
            "TRIAL_BY_ICE", "Shiva_Prime_TBI");
    }
}
