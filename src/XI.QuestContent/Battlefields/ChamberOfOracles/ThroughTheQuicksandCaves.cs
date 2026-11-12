// Through the Quicksand Caves (Chamber of Oracles, Zilart 6's fight), a line-for-line port of
// scripts/battlefields/Chamber_of_Oracles/through_the_quicksand_caves.lua (quest rework C3, C5). A BattlefieldMission
// of 30 minutes for six, trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL, entered at
// SC_Entrance (index 0) and left at Shimmering_Circle; no title, no experience. The mission is the Zilart log's
// Through the Quicksand Caves, entered at Zilart status 0. Its one group, by id from Centurio V-III three to an area
// (+0 to +8), wins the fight when they all die. The win sets battlefieldWin only while the mission is
// current; the mission's 32001 finish (Missions/Zilart/ThroughTheQuicksandCaves.cs) reads it.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.ChamberOfOracles
{
    public sealed class ThroughTheQuicksandCaves : BattlefieldScript
    {
        public ThroughTheQuicksandCaves() : base("THROUGH_THE_QUICKSAND_CAVES") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Chamber_of_Oracles/through_the_quicksand_caves";

        static BattlefieldMobId[] Ids(int first) => new[]
        {
            new BattlefieldMobId("CENTURIO_V_III", first),
            new BattlefieldMobId("CENTURIO_V_III", first + 1),
            new BattlefieldMobId("CENTURIO_V_III", first + 2),
        };

        public override BattlefieldData Data { get; } = new BattlefieldMissionData
        {
            ZoneId = Zone.ChamberOfOracles,
            BattlefieldId = Battlefield.ThroughTheQuicksandCaves,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 0,
            EntryNpcs = new[] { Npc.ChamberOfOracles.SCEntrance },
            ExitNpcs = new[] { Npc.ChamberOfOracles.ShimmeringCircle },
            MissionArea = XI.Quests.MissionLog.Zilart,
            Mission = XI.QuestContent.Mission.Zilart.ThroughTheQuicksandCaves,
            MissionStatusArea = XI.Quests.MissionLog.Zilart,
            RequiredMissionStatus = 0,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[] { Ids(0), Ids(3), Ids(6) },
                    AllDeath = (q, mob) => q.Battlefield.SetStatus(BattlefieldConst.StatusWon),
                },
            },
        };
    }
}
