// Legacy of the Lost (Talacca Cove, ToAU 35's fight), a line-for-line port of
// scripts/battlefields/Talacca_Cove/legacy_of_the_lost.lua (quest rework C3 T3). A BattlefieldMission of 30 minutes
// for six, trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL, index 4, only area 1 allowed,
// entered at _1l0 and left at _1l1, _1l2 or _1l3. The mission is ToAU's Legacy of the Lost. Kept as the Lua has it (a
// Lua bug): the Lua sets no missionStatusArea (its ToAU siblings do), so the registrant's status check, and the
// canSkip operand of the mission's 32001, read the char's nation log, not ToAU's; a registrant mid-way through a
// nation mission cannot enter, and needs nation status 0. Title: Gessho's Mercy. Group 1, Gessho (one list per area:
// the id and the ids 7 and 14 after it), wins the fight when it dies. Group 2, Gessho's clones (six per area: ids 1 to
// 6, 8 to 13 and 15 to 20 after Gessho), is not spawned with the fight. The win sets battlefieldWin only while the
// mission is current, and the mission's 32001 finish (Missions/Toau/LegacyOfTheLost.cs) reads it.
// NOTE (Lua): Gessho is missing logic in its mob script for text displayed throughout the fight at minimum and may be
// missing additional mob mechanics along with improper spawning of clones.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.TalaccaCove
{
    public sealed class LegacyOfTheLost : BattlefieldScript
    {
        public LegacyOfTheLost() : base("LEGACY_OF_THE_LOST") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Talacca_Cove/legacy_of_the_lost";

        public override BattlefieldData Data { get; } = new BattlefieldMissionData
        {
            ZoneId = Zone.TalaccaCove,
            BattlefieldId = Battlefield.LegacyOfTheLost,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 4,
            AllowedAreas = new[] { 1 },
            EntryNpcs = new[] { Npc.TalaccaCove._1l0 },
            ExitNpcs = new[]
            {
                Npc.TalaccaCove._1l1,
                Npc.TalaccaCove._1l2,
                Npc.TalaccaCove._1l3,
            },
            MissionArea = XI.Quests.MissionLog.ToAU,
            Mission = XI.QuestContent.Mission.Toau.LegacyOfTheLost,
            RequiredMissionStatus = 0,
            Title = Title.GesshosMercy,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { new BattlefieldMobId("GESSHO") },
                        new[] { new BattlefieldMobId("GESSHO", 7) },
                        new[] { new BattlefieldMobId("GESSHO", 14) },
                    },

                    AllDeath = (q, mob) =>
                    {
                        q.Battlefield.SetStatus(BattlefieldConst.StatusWon);
                    },
                },

                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[]
                        {
                            new BattlefieldMobId("GESSHO", 1), new BattlefieldMobId("GESSHO", 2),
                            new BattlefieldMobId("GESSHO", 3), new BattlefieldMobId("GESSHO", 4),
                            new BattlefieldMobId("GESSHO", 5), new BattlefieldMobId("GESSHO", 6),
                        },
                        new[]
                        {
                            new BattlefieldMobId("GESSHO", 8), new BattlefieldMobId("GESSHO", 9),
                            new BattlefieldMobId("GESSHO", 10), new BattlefieldMobId("GESSHO", 11),
                            new BattlefieldMobId("GESSHO", 12), new BattlefieldMobId("GESSHO", 13),
                        },
                        new[]
                        {
                            new BattlefieldMobId("GESSHO", 15), new BattlefieldMobId("GESSHO", 16),
                            new BattlefieldMobId("GESSHO", 17), new BattlefieldMobId("GESSHO", 18),
                            new BattlefieldMobId("GESSHO", 19), new BattlefieldMobId("GESSHO", 20),
                        },
                    },

                    Spawned = false,
                },
            },
        };
    }
}
