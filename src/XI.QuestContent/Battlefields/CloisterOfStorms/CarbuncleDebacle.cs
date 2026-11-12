// Carbuncle Debacle (Cloister of Storms BCNM), a line-for-line port of
// scripts/battlefields/Cloister_of_Storms/carbuncle_debacle.lua (quest rework C3, Q2b). A
// BattlefieldQuest of the Windurst quest Carbuncle Debacle: 30 minutes for 18 (no experience lost), index 1,
// entered at the entry NPC with the Lightning Pendulum (not consumed) when CarbuncleDebacleProgress is 3, and
// left at the Protocrystal. One group, one list per area; its death wins the fight.
// checkRequirements: only the registrant needs the item and the quest state, the rest of the party
// passes isValidEntry. onEventFinishWin replaces the framework's: the var 3 -> 4 (no reward).
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.CloisterOfStorms
{
    public sealed class CarbuncleDebacle : BattlefieldScript
    {
        public CarbuncleDebacle() : base("CARBUNCLE_DEBACLE_CLOISTER_OF_STORMS") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Cloister_of_Storms/carbuncle_debacle";

        public override BattlefieldData Data { get; } = new BattlefieldQuestData
        {
            ZoneId = Zone.CloisterOfStorms,
            BattlefieldId = Battlefield.CarbuncleDebacleCloisterOfStorms,
            MaxPlayers = 18,
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 1,
            EntryNpcs = new[] { Npc.CloisterOfStorms.LPEntrance },
            ExitNpcs = new[] { Npc.CloisterOfStorms.LightningProtocrystal },
            // the pendulum is not consumed on entry, but is required to enter the battlefield
            RequiredItems = new BattlefieldRequiredItems { Items = new[] { Item.LightningPendulum }, Keep = true },

            QuestArea = XI.Quests.QuestLog.Windurst,
            Quest = "CARBUNCLE_DEBACLE",
            RequiredVar = "CarbuncleDebacleProgress",
            RequiredValue = 3,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[]
                        {
                            new BattlefieldMobId("LIGHTNING_GREMLIN"),
                            new BattlefieldMobId("LIGHTNING_GREMLIN", 1),
                        },
                        new[]
                        {
                            new BattlefieldMobId("LIGHTNING_GREMLIN", 2),
                            new BattlefieldMobId("LIGHTNING_GREMLIN", 3),
                        },
                        new[]
                        {
                            new BattlefieldMobId("LIGHTNING_GREMLIN", 4),
                            new BattlefieldMobId("LIGHTNING_GREMLIN", 5),
                        },
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

        public override void OnEventFinishWin(IBattlefieldContext q, int csid, int option)
        {
            var player = q.Player;
            if (player.CharVar("CarbuncleDebacleProgress") == 3)
            {
                player.SetCharVar("CarbuncleDebacleProgress", 4);
            }
        }
    }
}
