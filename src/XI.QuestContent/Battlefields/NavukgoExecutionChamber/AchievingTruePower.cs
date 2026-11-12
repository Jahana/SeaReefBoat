// Achieving True Power (Navukgo Execution Chamber, PUP limit break), a line-for-line port of
// scripts/battlefields/Navukgo_Execution_Chamber/achieving_true_power.lua (quest rework C3, Q4). A BattlefieldQuest
// of the Bastok quest Achieving True Power: 10 minutes for one, no support job, capped at xi.settings.main.MAX_LEVEL,
// index 3, entered at _1s0 by a Puppetmaster's Testimony (worn on entry: TESTIMONY_WEARS; a worn one is refused:
// TESTIMONY_IS_TORN) when Quest[1][85]Prog is 1, and left at _1s1 to _1s3. One superlinked group, Shamarhaan and the
// next id, two to an area; their deaths win the fight. entryRequirement: a Puppetmaster of level 66 or more.
// setupBattlefield keeps the initiator's level in the battlefield's playerLevel (99 when the initiator is not found).
// The testimony's wear on entry is the framework's onBattlefieldEnter, which a Lua module may override
// (modules/era/lua/globals/testimony_single_use.lua: the testimony wears out on one entry); the C# framework runs that
// override as the Lua one does (quest rework C3 PR1).
// Kept Lua quirk: GetPlayerByID(battlefield:getInitiator()) passes the initiator's name too, which the binding ignores.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.NavukgoExecutionChamber
{
    public sealed class AchievingTruePower : BattlefieldScript
    {
        public AchievingTruePower() : base("ACHIEVING_TRUE_POWER") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Navukgo_Execution_Chamber/achieving_true_power";

        public override BattlefieldData Data { get; } = new BattlefieldQuestData
        {
            ZoneId = Zone.NavukgoExecutionChamber,
            BattlefieldId = Battlefield.AchievingTruePower,
            CanLoseExp = false,
            MaxPlayers = 1,
            LevelCapSetting = "MAX_LEVEL",
            AllowSubjob = false,
            // utils.minutes(10)
            TimeLimit = 10 * 60,
            Index = 3,
            EntryNpcs = new[] { Npc.NavukgoExecutionChamber._1s0 },
            ExitNpcs = new[]
            {
                Npc.NavukgoExecutionChamber._1s1, Npc.NavukgoExecutionChamber._1s2, Npc.NavukgoExecutionChamber._1s3,
            },
            RequiredItems = new BattlefieldRequiredItems
            {
                Items = new[] { Item.PuppetmastersTestimony },
                WearMessage = Text.NavukgoExecutionChamber.TESTIMONY_WEARS,
                WornMessage = Text.NavukgoExecutionChamber.TESTIMONY_IS_TORN,
            },

            QuestArea = XI.Quests.QuestLog.Bastok,
            Quest = "ACHIEVING_TRUE_POWER",
            RequiredVar = "Quest[1][85]Prog",
            RequiredValue = 1,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { new BattlefieldMobId("SHAMARHAAN"), new BattlefieldMobId("SHAMARHAAN", 1) },
                        new[] { new BattlefieldMobId("SHAMARHAAN", 2), new BattlefieldMobId("SHAMARHAAN", 3) },
                        new[] { new BattlefieldMobId("SHAMARHAAN", 4), new BattlefieldMobId("SHAMARHAAN", 5) },
                    },

                    Superlink = true,

                    AllDeath = (q, mob) =>
                    {
                        q.Battlefield.SetStatus(BattlefieldConst.StatusWon);
                    },
                },
            },
        };

        public override bool EntryRequirement(IBattlefieldContext q, bool isRegistrant)
        {
            bool jobRequirement = q.Player.MainJob == Job.Pup;
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
