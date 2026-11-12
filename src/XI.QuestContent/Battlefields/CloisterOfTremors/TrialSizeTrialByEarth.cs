// Trial-Size Trial by Earth (Cloister of Tremors BCNM), a line-for-line port of
// scripts/battlefields/Cloister_of_Tremors/trial-size_trial_by_earth.lua (quest rework C3, Q1). A BattlefieldQuest of
// the Bastok quest Trial-Size Trial by Earth: 15 minutes for one, capped at level 20, no experience lost, index 2,
// entered at EP_Entrance with the Mini Tuning Fork of Earth (taken on entry) and left at Earth_Protocrystal. Its
// entryRequirement is a Summoner of level 20 or more. Its one group, Titan_Prime_TSTBE, wins the fight when it dies.
// Unlike the other trial-size files its onEventFinishWin is inline: it teaches Titan (TITAN_UNLOCKED) if not known,
// gives the Scroll of Instant Warp if not held, adds 20 Bastok fame and completes the quest.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.CloisterOfTremors
{
    public sealed class TrialSizeTrialByEarth : TrialSizeBattlefield
    {
        public TrialSizeTrialByEarth() : base("TRIAL_SIZE_TRIAL_BY_EARTH") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Cloister_of_Tremors/trial-size_trial_by_earth";

        public override BattlefieldData Data { get; } = QuestTrials.TrialSize(
            Zone.CloisterOfTremors, Battlefield.TrialSizeTrialByEarth, 2,
            Npc.CloisterOfTremors.EPEntrance,
            Npc.CloisterOfTremors.EarthProtocrystal,
            Item.MiniTuningForkOfEarth, XI.Quests.QuestLog.Bastok,
            "TRIAL_SIZE_TRIAL_BY_EARTH", "Titan_Prime_TSTBE");

        public override void OnEventFinishWin(IBattlefieldContext q, int csid, int option)
        {
            var player = q.Player;
            if (!player.HasSpell(Spell.Titan))
            {
                player.AddSpell(Spell.Titan);
                player.MessageSpecial(q.Text.Id(Text.CloisterOfTremors.TITAN_UNLOCKED), 0, 0, 1);
            }

            if (!player.HasItem(Item.ScrollOfInstantWarp))
            {
                player.GiveItem(Item.ScrollOfInstantWarp);
            }

            player.AddFame(FameArea.Bastok, 20);
            player.CompleteQuest(XI.Quests.QuestLog.Bastok, "TRIAL_SIZE_TRIAL_BY_EARTH");
        }
    }
}
