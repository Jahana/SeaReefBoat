// The Puppet Master (Cloister of Tremors BCNM), a line-for-line port of
// scripts/battlefields/Cloister_of_Tremors/puppet_master.lua (quest rework C3, Q1). !pos -539 1 -493 209. A
// BattlefieldQuest of the Windurst quest The Puppet Master: 30 minutes for up to 18, no experience lost, index 1,
// entered at EP_Entrance with the Earth Pendulum (not consumed) when Quest[2][81]Prog is 1, and left at
// Earth_Protocrystal. Its one group is Galgalim and its two followers (one list per area); its death wins the fight.
// checkRequirements: only the registrant needs the item and the quest state, the rest of the party passes
// isValidEntry.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.CloisterOfTremors
{
    public sealed class PuppetMaster : BattlefieldScript
    {
        public PuppetMaster() : base("PUPPET_MASTER") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Cloister_of_Tremors/puppet_master";

        public override BattlefieldData Data { get; } = new BattlefieldQuestData
        {
            ZoneId = Zone.CloisterOfTremors,
            BattlefieldId = Battlefield.PuppetMaster,
            CanLoseExp = false,
            MaxPlayers = 18,
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 1,
            EntryNpcs = new[] { Npc.CloisterOfTremors.EPEntrance },
            ExitNpcs = new[] { Npc.CloisterOfTremors.EarthProtocrystal },
            RequiredItems = new BattlefieldRequiredItems { Items = new[] { Item.EarthPendulum }, Keep = true },
            RequiredVar = "Quest[2][81]Prog",
            RequiredValue = 1,

            QuestArea = XI.Quests.QuestLog.Windurst,
            Quest = "THE_PUPPET_MASTER",

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { new BattlefieldMobId("GALGALIM") },
                        new[] { new BattlefieldMobId("GALGALIM", 1) },
                        new[] { new BattlefieldMobId("GALGALIM", 2) },
                    },

                    AllDeath = (q, mob) =>
                    {
                        q.Battlefield.SetStatus(BattlefieldConst.StatusWon);
                    },
                },
            },
        };

        public override bool CheckRequirements(IBattlefieldContext q, bool isRegistrant)
        {
            if (isRegistrant)
            {
                return new BattlefieldQuest(q).CheckRequirements(isRegistrant);
            }

            return q.Framework.IsValidEntry();
        }
    }
}
