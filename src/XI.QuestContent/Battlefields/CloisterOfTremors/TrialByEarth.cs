// Trial by Earth (Cloister of Tremors BCNM, the Bastok quest's fight), a line-for-line port of
// scripts/battlefields/Cloister_of_Tremors/trial_by_earth.lua (quest rework C2). A BattlefieldQuest of 30 minutes for
// six, no experience lost, entered at EP_Entrance with the Tuning Fork of Earth (taken on entry) by a char who has
// accepted or completed Bastok's Trial by Earth, and left at Earth_Protocrystal. Its one group, Titan Prime, wins the
// fight when it dies. Every method is BattlefieldQuest's or Battlefield's: the file overrides none. The win sets
// battlefieldWin only while the quest is accepted (repeatable is not set), and the C# quest's 32001 finish
// (Bastok/TrialByEarth.cs) gives the Whisper of Tremors and the title on it.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.CloisterOfTremors
{
    public sealed class TrialByEarth : BattlefieldScript
    {
        public TrialByEarth() : base("TRIAL_BY_EARTH") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Cloister_of_Tremors/trial_by_earth";

        public override BattlefieldData Data { get; } = new BattlefieldQuestData
        {
            ZoneId = Zone.CloisterOfTremors,
            BattlefieldId = Battlefield.TrialByEarth,
            CanLoseExp = false,
            MaxPlayers = 6,
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 0,
            EntryNpcs = new[] { Npc.CloisterOfTremors.EPEntrance },
            ExitNpcs = new[] { Npc.CloisterOfTremors.EarthProtocrystal },
            RequiredKeyItems = new BattlefieldRequiredKeyItems { Items = { KeyItem.TuningForkOfEarth } },

            QuestArea = XI.Quests.QuestLog.Bastok,
            Quest = "TRIAL_BY_EARTH",

            Groups =
            {
                new BattlefieldGroup
                {
                    Mobs = new[] { "Titan_Prime_TBE" },
                    AllDeath = (q, mob) =>
                    {
                        q.Battlefield.SetStatus(BattlefieldConst.StatusWon);
                    },
                },
            },
        };
    }
}
