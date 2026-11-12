// Uninvited Guests (Monarch Linn BCNM), a line-for-line port of scripts/battlefields/Monarch_Linn/uninvited_guests.lua
// (quest rework C3, Q3a). A BattlefieldQuest of the Other Areas quest Uninvited Guests: 30 minutes for 18, index 6,
// entered at SD_Entrance with the Monarch Linn Patrol Permit (taken on entry, from every member) when
// Quest[4][81]Prog is 1, and left at SD_BCNM_Exit_1 to 3. Two groups, one list per area, both in superlink group 1: the
// Mammet-800 (one to an area) and eight more to an area, not spawned at the start. No group has an allDeath: the win is
// Mammet-800's onMobDeath (scripts/zones/Monarch_Linn/mobs/Mammet-800.lua), which stays Lua. Every method is
// BattlefieldQuest's or Battlefield's: the file overrides none.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.MonarchLinn
{
    public sealed class UninvitedGuests : BattlefieldScript
    {
        public UninvitedGuests() : base("UNINVITED_GUESTS") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Monarch_Linn/uninvited_guests";

        static BattlefieldMobId[] Ids(int first)
        {
            var ids = new BattlefieldMobId[8];
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = new BattlefieldMobId("MAMMET_800", first + i);
            }

            return ids;
        }

        public override BattlefieldData Data { get; } = new BattlefieldQuestData
        {
            ZoneId = Zone.MonarchLinn,
            BattlefieldId = Battlefield.UninvitedGuests,
            MaxPlayers = 18,
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 6,
            EntryNpcs = new[] { Npc.MonarchLinn.SDEntrance },
            ExitNpcs = new[] { Npc.MonarchLinn.SDBCNMExit1, Npc.MonarchLinn.SDBCNMExit2, Npc.MonarchLinn.SDBCNMExit3 },
            RequiredKeyItems = new BattlefieldRequiredKeyItems { Items = { KeyItem.MonarchLinnPatrolPermit } },

            QuestArea = XI.Quests.QuestLog.OtherAreas,
            Quest = "UNINVITED_GUESTS",
            RequiredVar = "Quest[4][81]Prog",
            RequiredValue = 1,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { new BattlefieldMobId("MAMMET_800") },
                        new[] { new BattlefieldMobId("MAMMET_800", 10) },
                        new[] { new BattlefieldMobId("MAMMET_800", 20) },
                    },
                    SuperlinkGroup = 1,
                },

                new BattlefieldGroup
                {
                    AreaMobIds = new[] { Ids(1), Ids(11), Ids(21) },
                    SuperlinkGroup = 1,
                    Spawned = false,
                },
            },
        };
    }
}
