// Puppetmaster Blues (Talacca Cove BCNM), a line-for-line port of
// scripts/battlefields/Talacca_Cove/puppetmaster_blues.lua (quest rework C3, Q3b). A BattlefieldQuest of the Aht
// Urhgan quest Puppetmaster Blues (PUP AF3): 30 minutes for six, capped at xi.settings.main.MAX_LEVEL, index 2,
// entered at _1l0 with the Toggle Switch and Valkeng's Memory Chip key items (not consumed) and left at _1l1 to
// _1l3. One group, Valkeng; his death wins the fight. checkRequirements: only the registrant needs the key items and
// the quest state, the rest of the party passes isValidEntry.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.TalaccaCove
{
    public sealed class PuppetmasterBlues : BattlefieldScript
    {
        public PuppetmasterBlues() : base("PUPPETMASTER_BLUES") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Talacca_Cove/puppetmaster_blues";

        public override BattlefieldData Data { get; } = new BattlefieldQuestData
        {
            ZoneId = Zone.TalaccaCove,
            BattlefieldId = Battlefield.PuppetmasterBlues,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 2,
            EntryNpcs = new[] { Npc.TalaccaCove._1l0 },
            ExitNpcs = new[] { Npc.TalaccaCove._1l1, Npc.TalaccaCove._1l2, Npc.TalaccaCove._1l3 },
            // key items are not consumed on entry, but are required to enter the battlefield
            RequiredKeyItems = new BattlefieldRequiredKeyItems
            {
                Items = { KeyItem.ToggleSwitch, KeyItem.ValkengsMemoryChip },
                Keep = true,
            },

            QuestArea = XI.Quests.QuestLog.AhtUrghan,
            Quest = "PUPPETMASTER_BLUES",

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { new BattlefieldMobId("VALKENG") },
                        new[] { new BattlefieldMobId("VALKENG", 1) },
                        new[] { new BattlefieldMobId("VALKENG", 2) },
                    },

                    AllDeath = (q, mob) =>
                    {
                        q.Battlefield.SetStatus(BattlefieldConst.StatusWon);
                    },
                },
            },
        };

        // Only the battlefield registrant needs to have the required item and quest state to initiate the battlefield.
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
