// Class Reunion (Cloister of Frost BCNM), a line-for-line port of
// scripts/battlefields/Cloister_of_Frost/class_reunion.lua (quest rework C3, Q2b). A
// BattlefieldQuest of the Windurst quest Class Reunion: 30 minutes for 6 (no experience lost), index 1,
// entered at the entry NPC with the Ice Pendulum (not consumed) when ClassReunionProgress is 5, and
// left at the Protocrystal. One group, one list per area; its death wins the fight.
// checkRequirements: only the registrant needs the item and the quest state, the rest of the party
// passes isValidEntry. onEventFinishWin replaces the framework's: the var 5 -> 6 (no reward).
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.CloisterOfFrost
{
    public sealed class ClassReunion : BattlefieldScript
    {
        public ClassReunion() : base("CLASS_REUNION") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Cloister_of_Frost/class_reunion";

        public override BattlefieldData Data { get; } = new BattlefieldQuestData
        {
            ZoneId = Zone.CloisterOfFrost,
            BattlefieldId = Battlefield.ClassReunion,
            MaxPlayers = 6,
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 1,
            EntryNpcs = new[] { Npc.CloisterOfFrost.IPEntrance },
            ExitNpcs = new[] { Npc.CloisterOfFrost.IceProtocrystal },
            // the pendulum is not consumed on entry, but is required to enter the battlefield
            RequiredItems = new BattlefieldRequiredItems { Items = new[] { Item.IcePendulum }, Keep = true },

            QuestArea = XI.Quests.QuestLog.Windurst,
            Quest = "CLASS_REUNION",
            RequiredVar = "ClassReunionProgress",
            RequiredValue = 5,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[]
                        {
                            new BattlefieldMobId("DRYAD"),
                            new BattlefieldMobId("DRYAD", 1),
                            new BattlefieldMobId("DRYAD", 2),
                            new BattlefieldMobId("DRYAD", 3),
                            new BattlefieldMobId("DRYAD", 4),
                            new BattlefieldMobId("DRYAD", 5),
                        },
                        new[]
                        {
                            new BattlefieldMobId("DRYAD", 6),
                            new BattlefieldMobId("DRYAD", 7),
                            new BattlefieldMobId("DRYAD", 8),
                            new BattlefieldMobId("DRYAD", 9),
                            new BattlefieldMobId("DRYAD", 10),
                            new BattlefieldMobId("DRYAD", 11),
                        },
                        new[]
                        {
                            new BattlefieldMobId("DRYAD", 12),
                            new BattlefieldMobId("DRYAD", 13),
                            new BattlefieldMobId("DRYAD", 14),
                            new BattlefieldMobId("DRYAD", 15),
                            new BattlefieldMobId("DRYAD", 16),
                            new BattlefieldMobId("DRYAD", 17),
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
            if (player.CharVar("ClassReunionProgress") == 5)
            {
                player.SetCharVar("ClassReunionProgress", 6);
            }
        }
    }
}
