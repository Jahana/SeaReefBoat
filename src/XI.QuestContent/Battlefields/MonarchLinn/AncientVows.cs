// Ancient Vows (Monarch Linn, Promathia 2-5's fight), a line-for-line port of
// scripts/battlefields/Monarch_Linn/ancient_vows.lua (quest rework C3). A BattlefieldMission of 30 minutes for six,
// trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL, entered at SD_Entrance and left at
// SD_BCNM_Exit_1 to 3, granting 1000 experience and the Tavnazian Traveler title. The mission is the Promathia log's
// Ancient Vows, entered by the char var Mission[6][248]Status at 2. It overrides entryRequirement alone: the char must
// not have come from Riverne - Site #B01. Its one group, the nine Mammet-19 Epsilons (by id, three to an area), wins
// the fight when they die. The win sets battlefieldWin only while the mission is current; the mission's 32001 finish
// (Missions/Cop/AncientVows.cs) reads it.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.MonarchLinn
{
    public sealed class AncientVows : BattlefieldScript
    {
        public AncientVows() : base("ANCIENT_VOWS") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Monarch_Linn/ancient_vows";

        static BattlefieldMobId[] Ids(int first) => new[]
        {
            new BattlefieldMobId("MAMMET_19_EPSILON", first),
            new BattlefieldMobId("MAMMET_19_EPSILON", first + 1),
            new BattlefieldMobId("MAMMET_19_EPSILON", first + 2),
        };

        public override BattlefieldData Data { get; } = new BattlefieldMissionData
        {
            ZoneId = Zone.MonarchLinn,
            BattlefieldId = Battlefield.AncientVows,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 0,
            EntryNpcs = new[] { Npc.MonarchLinn.SDEntrance },
            ExitNpcs = new[] { Npc.MonarchLinn.SDBCNMExit1, Npc.MonarchLinn.SDBCNMExit2, Npc.MonarchLinn.SDBCNMExit3 },
            MissionArea = XI.Quests.MissionLog.CoP,
            Mission = XI.QuestContent.Mission.Cop.AncientVows,
            RequiredVar = "Mission[6][248]Status",
            RequiredValue = 2,
            GrantXP = 1000,
            Title = Title.TavnazianTraveler,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[] { Ids(0), Ids(3), Ids(6) },
                    AllDeath = (q, mob) => q.Battlefield.SetStatus(BattlefieldConst.StatusWon),
                },
            },
        };

        // in cases for testing, or after something happened to the char's previous zone, the char must have entered the
        // "correct" way: not from Site B01
        public override bool EntryRequirement(IBattlefieldContext q, bool isRegistrant)
        {
            return q.Player.PreviousZone != Zone.RiverneSiteB01;
        }
    }
}
