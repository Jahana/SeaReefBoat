// The Beast Within (Jade Sepulcher, BLU limit break), a line-for-line port of
// scripts/battlefields/Jade_Sepulcher/the_beast_within.lua (quest rework C3, Q4). A BattlefieldQuest of the Aht Urhgan
// quest The Beast Within: 10 minutes for one, no support job, capped at xi.settings.main.MAX_LEVEL, index 2, entered
// at _1v0 by a Blue Mage's Testimony (worn on entry: TESTIMONY_WEARS; a worn one is refused: TESTIMONY_IS_TORN) when
// Quest[6][40]Prog is 2, and left at _1v1 to _1v3. One group, Raubahn, one to an area; his death wins the fight.
// entryRequirement: a Blue Mage of level 66 or more. setupBattlefield keeps the initiator's level in the
// battlefield's playerLevel (99 when the initiator is not found), which Raubahn's mob script reads.
// The testimony's wear on entry is the framework's onBattlefieldEnter, which a Lua module may override
// (modules/era/lua/globals/testimony_single_use.lua: the testimony wears out on one entry); the C# framework runs that
// override as the Lua one does (quest rework C3 PR1).
// Kept Lua quirk: GetPlayerByID(battlefield:getInitiator()) passes the initiator's name too, which the binding ignores.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.JadeSepulcher
{
    public sealed class BeastWithin : BattlefieldScript
    {
        public BeastWithin() : base("BEAST_WITHIN") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Jade_Sepulcher/the_beast_within";

        public override BattlefieldData Data { get; } = new BattlefieldQuestData
        {
            ZoneId = Zone.JadeSepulcher,
            BattlefieldId = Battlefield.BeastWithin,
            CanLoseExp = false,
            MaxPlayers = 1,
            LevelCapSetting = "MAX_LEVEL",
            AllowSubjob = false,
            // utils.minutes(10)
            TimeLimit = 10 * 60,
            Index = 2,
            EntryNpcs = new[] { Npc.JadeSepulcher._1v0 },
            ExitNpcs = new[] { Npc.JadeSepulcher._1v1, Npc.JadeSepulcher._1v2, Npc.JadeSepulcher._1v3 },
            RequiredItems = new BattlefieldRequiredItems
            {
                Items = new[] { Item.BlueMagesTestimony },
                WearMessage = Text.JadeSepulcher.TESTIMONY_WEARS,
                WornMessage = Text.JadeSepulcher.TESTIMONY_IS_TORN,
            },

            QuestArea = XI.Quests.QuestLog.AhtUrghan,
            Quest = "THE_BEAST_WITHIN",
            RequiredVar = "Quest[6][40]Prog",
            RequiredValue = 2,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { new BattlefieldMobId("RAUBAHN") },
                        new[] { new BattlefieldMobId("RAUBAHN", 1) },
                        new[] { new BattlefieldMobId("RAUBAHN", 2) },
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
            bool jobRequirement = q.Player.MainJob == Job.Blu;
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
