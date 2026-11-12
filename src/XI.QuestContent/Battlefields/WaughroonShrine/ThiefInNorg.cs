// A Thief in Norg?! (Waughroon Shrine BCNM), a line-for-line port of
// scripts/battlefields/Waughroon_Shrine/thief_in_norg.lua (quest rework C3, Q1). !pos -345 104 -260 144. A
// BattlefieldQuest of the Outlands quest A Thief in Norg?!: 30 minutes for six (no experience lost), index 4,
// entered at BC_Entrance with the Banishing Charm (not consumed) when Quest[5][142]Prog is 6, and left at
// Burning_Circle. Three groups, one list per area: the Gaki (the first, whose death wins the fight), the elemental
// and the avatar (not spawned at the start). checkRequirements: only the registrant needs the item and the quest
// state, the rest of the party passes isValidEntry.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.WaughroonShrine
{
    public sealed class ThiefInNorg : BattlefieldScript
    {
        public ThiefInNorg() : base("THIEF_IN_NORG") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Waughroon_Shrine/thief_in_norg";

        public override BattlefieldData Data { get; } = new BattlefieldQuestData
        {
            ZoneId = Zone.WaughroonShrine,
            BattlefieldId = Battlefield.ThiefInNorg,
            MaxPlayers = 6,
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 4,
            EntryNpcs = new[] { Npc.WaughroonShrine.BCEntrance },
            ExitNpcs = new[] { Npc.WaughroonShrine.BurningCircle },
            // the Banishing Charm is not consumed on entry, but is required to enter the battlefield
            RequiredItems = new BattlefieldRequiredItems { Items = new[] { Item.BanishingCharm }, Keep = true },

            QuestArea = XI.Quests.QuestLog.Outlands,
            Quest = "A_THIEF_IN_NORG",
            RequiredVar = "Quest[5][142]Prog",
            RequiredValue = 6,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[]
                        {
                            new BattlefieldMobId("GAKI"),
                            new BattlefieldMobId("GAKI", 1),
                            new BattlefieldMobId("GAKI", 2),
                        },
                        new[]
                        {
                            new BattlefieldMobId("GAKI", 5),
                            new BattlefieldMobId("GAKI", 6),
                            new BattlefieldMobId("GAKI", 7),
                        },
                        new[]
                        {
                            new BattlefieldMobId("GAKI", 10),
                            new BattlefieldMobId("GAKI", 11),
                            new BattlefieldMobId("GAKI", 12),
                        },
                    },

                    AllDeath = (q, mob) =>
                    {
                        q.Battlefield.SetStatus(BattlefieldConst.StatusWon);
                    },
                },

                // Elemental
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { new BattlefieldMobId("GAKI", 3) },
                        new[] { new BattlefieldMobId("GAKI", 8) },
                        new[] { new BattlefieldMobId("GAKI", 13) },
                    },
                },

                // Avatar
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { new BattlefieldMobId("GAKI", 4) },
                        new[] { new BattlefieldMobId("GAKI", 9) },
                        new[] { new BattlefieldMobId("GAKI", 14) },
                    },

                    Spawned = false,
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
