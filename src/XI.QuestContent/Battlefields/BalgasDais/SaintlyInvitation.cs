// Saintly Invitation (Balgas Dais, Windurst 6-2's fight), a line-for-line port of
// scripts/battlefields/Balgas_Dais/saintly_invitation.lua (quest rework C3 N5). !pos 299 -123 345 146. A
// BattlefieldMission of 30 minutes for six, trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL,
// entered at BC_Entrance (index 3) and left at Burning_Circle; no title, no experience. The mission is the Windurst
// log's Saintly Invitation, entered at Windurst status 1. Its first group, by id from Buu Xolo the Bloodfaced four to
// an area (+0 to +3, +6 to +9, +12 to +15), wins the fight when they all die; its second, two to an area (+4 and +5,
// +10 and +11, +16 and +17), is not spawned at the start. The win sets battlefieldWin only while the mission is
// current; the mission's 32001 finish (Missions/Windurst/SaintlyInvitation.cs) reads it.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.BalgasDais
{
    public sealed class SaintlyInvitation : BattlefieldScript
    {
        public SaintlyInvitation() : base("SAINTLY_INVITATION") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Balgas_Dais/saintly_invitation";

        static BattlefieldMobId[] Ids(int first) => new[]
        {
            new BattlefieldMobId("BUU_XOLO_THE_BLOODFACED", first),
            new BattlefieldMobId("BUU_XOLO_THE_BLOODFACED", first + 1),
            new BattlefieldMobId("BUU_XOLO_THE_BLOODFACED", first + 2),
            new BattlefieldMobId("BUU_XOLO_THE_BLOODFACED", first + 3),
        };

        static BattlefieldMobId[] Pair(int first) => new[]
        {
            new BattlefieldMobId("BUU_XOLO_THE_BLOODFACED", first),
            new BattlefieldMobId("BUU_XOLO_THE_BLOODFACED", first + 1),
        };

        public override BattlefieldData Data { get; } = new BattlefieldMissionData
        {
            ZoneId = Zone.BalgasDais,
            BattlefieldId = Battlefield.SaintlyInvitation,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 3,
            EntryNpcs = new[] { Npc.BalgasDais.BCEntrance },
            ExitNpcs = new[] { Npc.BalgasDais.BurningCircle },
            MissionArea = XI.Quests.MissionLog.Windurst,
            Mission = XI.QuestContent.Mission.Windurst.SaintlyInvitation,
            MissionStatusArea = XI.Quests.MissionLog.Windurst,
            RequiredMissionStatus = 1,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[] { Ids(0), Ids(6), Ids(12) },
                    AllDeath = (q, mob) => q.Battlefield.SetStatus(BattlefieldConst.StatusWon),
                },

                new BattlefieldGroup
                {
                    AreaMobIds = new[] { Pair(4), Pair(10), Pair(16) },
                    Spawned = false,
                },
            },
        };
    }
}
