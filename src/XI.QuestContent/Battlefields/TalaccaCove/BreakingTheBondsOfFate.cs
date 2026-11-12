// Breaking the Bonds of Fate (Talacca Cove, COR limit break), a line-for-line port of
// scripts/battlefields/Talacca_Cove/breaking_the_bonds_of_fate.lua (quest rework C3, Q4). A BattlefieldQuest of the
// Aht Urhgan quest Breaking the Bonds of Fate: 10 minutes for one, no support job, capped at
// xi.settings.main.MAX_LEVEL, index 3, entered at _1l0 by a Corsair's Testimony (worn on entry: TESTIMONY_WEARS; a
// worn one is refused: TESTIMONY_IS_TORN) when Quest[6][41]Prog is 1, and left at _1l1 to _1l3. One group, Qultada,
// one to an area; his death wins the fight. entryRequirement: a Corsair of level 66 or more. setupBattlefield keeps
// the initiator's level in the battlefield's playerLevel (99 when the initiator is not found), which Qultada's mob
// script reads.
// The testimony's wear on entry is the framework's onBattlefieldEnter, which a Lua module may override
// (modules/era/lua/globals/testimony_single_use.lua: the testimony wears out on one entry); the C# framework runs that
// override as the Lua one does (quest rework C3 PR1).
// Kept Lua quirk: GetPlayerByID(battlefield:getInitiator()) passes the initiator's name too, which the binding ignores.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.TalaccaCove
{
    public sealed class BreakingTheBondsOfFate : BattlefieldScript
    {
        public BreakingTheBondsOfFate() : base("BREAKING_THE_BONDS_OF_FATE") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Talacca_Cove/breaking_the_bonds_of_fate";

        public override BattlefieldData Data { get; } = new BattlefieldQuestData
        {
            ZoneId = Zone.TalaccaCove,
            BattlefieldId = Battlefield.BreakingTheBondsOfFate,
            CanLoseExp = false,
            MaxPlayers = 1,
            LevelCapSetting = "MAX_LEVEL",
            AllowSubjob = false,
            // utils.minutes(10)
            TimeLimit = 10 * 60,
            Index = 3,
            EntryNpcs = new[] { Npc.TalaccaCove._1l0 },
            ExitNpcs = new[] { Npc.TalaccaCove._1l1, Npc.TalaccaCove._1l2, Npc.TalaccaCove._1l3 },
            RequiredItems = new BattlefieldRequiredItems
            {
                Items = new[] { Item.CorsairsTestimony },
                WearMessage = Text.TalaccaCove.TESTIMONY_WEARS,
                WornMessage = Text.TalaccaCove.TESTIMONY_IS_TORN,
            },

            QuestArea = XI.Quests.QuestLog.AhtUrghan,
            Quest = "BREAKING_THE_BONDS_OF_FATE",
            RequiredVar = "Quest[6][41]Prog",
            RequiredValue = 1,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { new BattlefieldMobId("QULTADA") },
                        new[] { new BattlefieldMobId("QULTADA", 1) },
                        new[] { new BattlefieldMobId("QULTADA", 2) },
                    },

                    AllDeath = (q, mob) =>
                    {
                        q.Battlefield.SetStatus(BattlefieldConst.StatusWon);
                    },
                },
            },
        };

        public override bool EntryRequirement(IBattlefieldContext q, bool isRegistrant)
        {
            bool jobRequirement = q.Player.MainJob == Job.Cor;
            bool levelRequirement = q.Player.MainLevel >= 66;

            return jobRequirement && levelRequirement;
        }

        public override void SetupBattlefield(IBattlefieldContext q)
        {
            var player = q.Engine.GetPlayerByID(q.Battlefield.Initiator().Id);
            int playerLevel = player != null ? player.MainLevel : 99;

            q.Battlefield.SetLocalVar("playerLevel", playerLevel);
        }
    }
}
