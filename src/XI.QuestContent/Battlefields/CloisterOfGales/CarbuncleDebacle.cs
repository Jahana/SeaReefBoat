// Carbuncle Debacle (Cloister of Gales BCNM), a line-for-line port of
// scripts/battlefields/Cloister_of_Gales/carbuncle_debacle.lua (quest rework C3, Q2b). A
// BattlefieldQuest of the Windurst quest Carbuncle Debacle: 30 minutes for 18 (no experience lost), index 1,
// entered at the entry NPC with the Wind Pendulum (not consumed) when CarbuncleDebacleProgress is 6, and
// left at the Protocrystal. One group, one list per area; its death wins the fight.
// checkRequirements: only the registrant needs the item and the quest state, the rest of the party
// passes isValidEntry. onEventFinishWin replaces the framework's: the var 6 -> 7, and the Daze
// Breaker Charm is taken whatever the var was.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.CloisterOfGales
{
    public sealed class CarbuncleDebacle : BattlefieldScript
    {
        public CarbuncleDebacle() : base("CARBUNCLE_DEBACLE_CLOISTER_OF_GALES") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Cloister_of_Gales/carbuncle_debacle";

        public override BattlefieldData Data { get; } = new BattlefieldQuestData
        {
            ZoneId = Zone.CloisterOfGales,
            BattlefieldId = Battlefield.CarbuncleDebacleCloisterOfGales,
            MaxPlayers = 18,
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 1,
            EntryNpcs = new[] { Npc.CloisterOfGales.WPEntrance },
            ExitNpcs = new[] { Npc.CloisterOfGales.WindProtocrystal },
            // the pendulum is not consumed on entry, but is required to enter the battlefield
            RequiredItems = new BattlefieldRequiredItems { Items = new[] { Item.WindPendulum }, Keep = true },

            QuestArea = XI.Quests.QuestLog.Windurst,
            Quest = "CARBUNCLE_DEBACLE",
            RequiredVar = "CarbuncleDebacleProgress",
            RequiredValue = 6,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[]
                        {
                            new BattlefieldMobId("OGMIOS"),
                        },
                        new[]
                        {
                            new BattlefieldMobId("OGMIOS", 1),
                        },
                        new[]
                        {
                            new BattlefieldMobId("OGMIOS", 2),
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
            if (player.CharVar("CarbuncleDebacleProgress") == 6)
            {
                player.SetCharVar("CarbuncleDebacleProgress", 7);
            }

            player.DelKeyItem(KeyItem.DazeBreakerCharm);
        }
    }
}
