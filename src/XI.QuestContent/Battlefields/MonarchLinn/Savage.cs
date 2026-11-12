// The Savage (Monarch Linn, Promathia 4-2's fight), a line-for-line port of
// scripts/battlefields/Monarch_Linn/savage.lua (quest rework C3). A BattlefieldMission of 30 minutes for six, trusts
// allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL, entered at SD_Entrance (index 1) and left at
// SD_BCNM_Exit_1 to 3, granting 1500 experience and the Mist Melter title. The mission is the Promathia log's The
// Savage, entered by the char var Mission[6][418]Status at 1. It overrides entryRequirement alone: the char must not
// have come from Riverne - Site #A01. Its one group is Ouryu, one to an area (by id: Ouryu, +1, +2); its death wins
// the fight. The win sets battlefieldWin only while the mission is current; the mission's 32001 finish
// (Missions/Cop/TheSavage.cs) reads it.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.MonarchLinn
{
    public sealed class Savage : BattlefieldScript
    {
        public Savage() : base("SAVAGE") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Monarch_Linn/savage";

        public override BattlefieldData Data { get; } = new BattlefieldMissionData
        {
            ZoneId = Zone.MonarchLinn,
            BattlefieldId = Battlefield.Savage,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 1,
            EntryNpcs = new[] { Npc.MonarchLinn.SDEntrance },
            ExitNpcs = new[] { Npc.MonarchLinn.SDBCNMExit1, Npc.MonarchLinn.SDBCNMExit2, Npc.MonarchLinn.SDBCNMExit3 },
            MissionArea = XI.Quests.MissionLog.CoP,
            Mission = XI.QuestContent.Mission.Cop.TheSavage,
            RequiredVar = "Mission[6][418]Status",
            RequiredValue = 1,
            GrantXP = 1500,
            Title = Title.MistMelter,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { new BattlefieldMobId("OURYU", 0) },
                        new[] { new BattlefieldMobId("OURYU", 1) },
                        new[] { new BattlefieldMobId("OURYU", 2) },
                    },
                    AllDeath = (q, mob) => q.Battlefield.SetStatus(BattlefieldConst.StatusWon),
                },
            },
        };

        // in cases for testing, or after something happened to the char's previous zone, the char must have entered the
        // "correct" way: not from Site A01
        public override bool EntryRequirement(IBattlefieldContext q, bool isRegistrant)
        {
            return q.Player.PreviousZone != Zone.RiverneSiteA01;
        }
    }
}
