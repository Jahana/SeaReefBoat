// Temple of Uggalepih (Sacrificial Chamber, Zilart 4's fight), a line-for-line port of
// scripts/battlefields/Sacrificial_Chamber/temple_of_uggalepih.lua (quest rework C3, C5). A BattlefieldMission of 30
// minutes for six, trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL, entered at _4j0 (index 0)
// and left at _4j2 to _4j4; no title, no experience. The mission is the Zilart log's The Temple of Uggalepih, entered
// at Zilart status 0. Its first group, by id from Graviton three to an area (+0 to +2, +5 to +7, +10 to +12),
// superlinked, wins the fight when they all die; the second, the Elemental of each area (+3, +8, +13), spawns with
// the fight; the third, the Avatar of each area (+4, +9, +14), does not (spawned false). The win sets battlefieldWin
// only while the mission is current; the mission's 32001 finish (Missions/Zilart/TheTempleOfUggalepih.cs) reads it.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.SacrificialChamber
{
    public sealed class TempleOfUggalepih : BattlefieldScript
    {
        public TempleOfUggalepih() : base("TEMPLE_OF_UGGALEPIH") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Sacrificial_Chamber/temple_of_uggalepih";

        static BattlefieldMobId[] Ids(params int[] offsets)
        {
            var ids = new BattlefieldMobId[offsets.Length];
            for (int i = 0; i < offsets.Length; i++) ids[i] = new BattlefieldMobId("GRAVITON", offsets[i]);
            return ids;
        }

        public override BattlefieldData Data { get; } = new BattlefieldMissionData
        {
            ZoneId = Zone.SacrificialChamber,
            BattlefieldId = Battlefield.TempleOfUggalepih,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 0,
            EntryNpcs = new[] { Npc.SacrificialChamber._4j0 },
            ExitNpcs = new[] { Npc.SacrificialChamber._4j2, Npc.SacrificialChamber._4j3, Npc.SacrificialChamber._4j4 },
            MissionArea = XI.Quests.MissionLog.Zilart,
            Mission = XI.QuestContent.Mission.Zilart.TheTempleOfUggalepih,
            MissionStatusArea = XI.Quests.MissionLog.Zilart,
            RequiredMissionStatus = 0,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[] { Ids(0, 1, 2), Ids(5, 6, 7), Ids(10, 11, 12) },
                    Superlink = true,
                    AllDeath = (q, mob) => q.Battlefield.SetStatus(BattlefieldConst.StatusWon),
                },

                // Elemental
                new BattlefieldGroup
                {
                    AreaMobIds = new[] { Ids(3), Ids(8), Ids(13) },
                },

                // Avatar
                new BattlefieldGroup
                {
                    AreaMobIds = new[] { Ids(4), Ids(9), Ids(14) },
                    Spawned = false,
                },
            },
        };
    }
}
