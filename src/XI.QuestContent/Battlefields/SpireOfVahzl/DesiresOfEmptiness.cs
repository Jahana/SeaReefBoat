// Desires of Emptiness (Spire of Vahzl, Promathia 5-2's fight), a line-for-line port of
// scripts/battlefields/Spire_of_Vahzl/desires_of_emptiness.lua (quest rework C3). A BattlefieldMission of 30 minutes
// for six, trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL, entered at _0n0 (index 0) and
// left at _0n1 to _0n3, granting 1500 experience and the Frozen Dead Body title. The mission is the Promathia log's
// Desires of Emptiness, entered by the char var Mission[6][518]Status at 2. Each of its three areas has seven
// Agonizers by id (area 1 AGONIZER + 0 to 6, area 2 + 7 to 13, area 3 + 14 to 20): the first group is three of them
// (+0, +1, +6 of the area) and its death wins the fight, the second group the other four, spawned later
// (spawned = false). The win sets battlefieldWin only while the mission is current; the mission's 32001 finish
// (Missions/Cop/DesiresOfEmptiness.cs) reads it.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.SpireOfVahzl
{
    public sealed class DesiresOfEmptiness : BattlefieldScript
    {
        public DesiresOfEmptiness() : base("DESIRES_OF_EMPTINESS") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Spire_of_Vahzl/desires_of_emptiness";

        public override BattlefieldData Data { get; } = new BattlefieldMissionData
        {
            ZoneId = Zone.SpireOfVahzl,
            BattlefieldId = Battlefield.DesiresOfEmptiness,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 0,
            EntryNpcs = new[] { Npc.SpireOfVahzl._0n0 },
            ExitNpcs = new[] { Npc.SpireOfVahzl._0n1, Npc.SpireOfVahzl._0n2, Npc.SpireOfVahzl._0n3 },
            MissionArea = XI.Quests.MissionLog.CoP,
            Mission = XI.QuestContent.Mission.Cop.DesiresOfEmptiness,
            RequiredVar = "Mission[6][518]Status",
            RequiredValue = 2,
            GrantXP = 1500,
            Title = Title.FrozenDeadBody,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { Agonizer(0), Agonizer(1), Agonizer(6) },
                        new[] { Agonizer(7), Agonizer(8), Agonizer(13) },
                        new[] { Agonizer(14), Agonizer(15), Agonizer(20) },
                    },
                    AllDeath = (q, mob) => q.Battlefield.SetStatus(BattlefieldConst.StatusWon),
                },
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { Agonizer(2), Agonizer(3), Agonizer(4), Agonizer(5) },
                        new[] { Agonizer(9), Agonizer(10), Agonizer(11), Agonizer(12) },
                        new[] { Agonizer(16), Agonizer(17), Agonizer(18), Agonizer(19) },
                    },
                    Spawned = false,
                },
            },
        };

        static BattlefieldMobId Agonizer(int offset) => new BattlefieldMobId("AGONIZER", offset);
    }
}
