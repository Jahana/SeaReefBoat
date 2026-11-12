// Shield of Diplomacy (Navukgo Execution Chamber, ToAU 22's fight), a line-for-line port of
// scripts/battlefields/Navukgo_Execution_Chamber/shield_of_diplomacy.lua (quest rework C3 T2). A BattlefieldMission of
// 30 minutes for six, trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL, index 4, only area 1
// allowed, entered at _1s0 and left at _1s1, _1s2 or _1s3. The mission is ToAU's Shield of Diplomacy with its status
// read from the ToAU log; a registrant needs status 2. Its one group, Khimaira (one list per area: KHIMAIRA_13 and the
// ids 2 and 4 after it), wins the fight when it dies. setupBattlefield (overridden, the framework's own is not
// called) puts Karababa in as an ally, set down at the area's spot (only area 1 has a real one), level 75. The win sets
// battlefieldWin only while the mission is current, and the mission's 32001 finish (Missions/Toau/ShieldOfDiplomacy.cs)
// reads it.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.NavukgoExecutionChamber
{
    public sealed class ShieldOfDiplomacy : BattlefieldScript
    {
        public ShieldOfDiplomacy() : base("SHIELD_OF_DIPLOMACY") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Navukgo_Execution_Chamber/shield_of_diplomacy";

        public override BattlefieldData Data { get; } = new BattlefieldMissionData
        {
            ZoneId = Zone.NavukgoExecutionChamber,
            BattlefieldId = Battlefield.ShieldOfDiplomacy,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 4,
            AllowedAreas = new[] { 1 },
            EntryNpcs = new[] { Npc.NavukgoExecutionChamber._1s0 },
            ExitNpcs = new[]
            {
                Npc.NavukgoExecutionChamber._1s1,
                Npc.NavukgoExecutionChamber._1s2,
                Npc.NavukgoExecutionChamber._1s3,
            },
            MissionArea = XI.Quests.MissionLog.ToAU,
            Mission = XI.QuestContent.Mission.Toau.ShieldOfDiplomacy,
            MissionStatusArea = XI.Quests.MissionLog.ToAU,
            RequiredMissionStatus = 2,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { new BattlefieldMobId("KHIMAIRA_13") },
                        new[] { new BattlefieldMobId("KHIMAIRA_13", 2) },
                        new[] { new BattlefieldMobId("KHIMAIRA_13", 4) },
                    },

                    AllDeath = (q, mob) =>
                    {
                        q.Battlefield.SetStatus(BattlefieldConst.StatusWon);
                    },
                },
            },
        };

        // NOTE (Lua): Like Khimaira, Karababa only has a valid position for area one, and will need to be added if
        // future areas are enabled.
        // TODO (Lua): Karababa does not appear to contain spell lists as well. Currently has a standback behavior, and
        // will melee if Khimaira is brought into range.
        static readonly double[][] KarababaCoords =
        {
            new[] { 361.488, -116.500, 382.511, 0 },
            new[] { 0.0, 0, 0, 0 },
            new[] { 0.0, 0, 0, 0 },
        };

        public override void SetupBattlefield(IBattlefieldContext q)
        {
            var karababa = q.Battlefield.InsertEntity(8, true, true);
            var coords = KarababaCoords[q.Battlefield.Area - 1];
            karababa.SetSpawn(coords[0], coords[1], coords[2], (int)coords[3]);
            karababa.Spawn();
            karababa.SetMobLevel(75);
        }
    }
}
