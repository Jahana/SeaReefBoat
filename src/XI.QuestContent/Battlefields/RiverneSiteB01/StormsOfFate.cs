// Storms of Fate (Riverne Site #B01 BCNM), a line-for-line port of
// scripts/battlefields/Riverne_Site_B01/storms_of_fate.lua (quest rework C3, Q3b). A BattlefieldQuest of the Jeuno
// quest Storms of Fate: 30 minutes for 18, trusts allowed, capped at xi.settings.main.MAX_LEVEL, index 0, the fixed
// area 1, entered at Unstable_Displacement when Quest[3][86]Prog is 2 and left at SD_BCNM_Exit, with the Conqueror of
// Fate title. battlefieldEntry heals the char (healCharacter, Shared/HealCharacter.cs). One group, Bahamut; his death
// wins the fight.
using XI.Quests.Authoring;
using XI.QuestContent.Battlefields.Shared;

namespace XI.QuestContent.Battlefields.RiverneSiteB01
{
    public sealed class StormsOfFate : BattlefieldScript
    {
        public StormsOfFate() : base("STORMS_OF_FATE") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Riverne_Site_B01/storms_of_fate";

        public override BattlefieldData Data { get; } = new BattlefieldQuestData
        {
            ZoneId = Zone.RiverneSiteB01,
            BattlefieldId = Battlefield.StormsOfFate,
            AllowTrusts = true,
            MaxPlayers = 18,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 0,
            Area = 1,
            EntryNpcs = new[] { Npc.RiverneSiteB01.UnstableDisplacement },
            ExitNpcs = new[] { Npc.RiverneSiteB01.SDBCNMExit },

            QuestArea = XI.Quests.QuestLog.Jeuno,
            Quest = "STORMS_OF_FATE",
            RequiredVar = "Quest[3][86]Prog",
            RequiredValue = 2,

            Title = Title.ConquerorOfFate,

            Groups =
            {
                new BattlefieldGroup
                {
                    MobIds = new[] { new BattlefieldMobId("BAHAMUT") },

                    AllDeath = (q, mob) =>
                    {
                        q.Battlefield.SetStatus(BattlefieldConst.StatusWon);
                    },
                },
            },
        };

        // players are healed on entry to the battlefield
        public override void BattlefieldEntry(IBattlefieldContext q)
        {
            HealCharacter.Heal(q.Player);
        }
    }
}
