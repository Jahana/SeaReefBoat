// Return to Delkfutt's Tower (Stellar Fulcrum, Zilart 8's fight), a line-for-line port of
// scripts/battlefields/Stellar_Fulcrum/return_to_delkfutts_tower.lua (quest rework C3, C5). A BattlefieldMission of 30
// minutes for six, trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL, entered at _4z0 (index
// 0) and left at _4z1 to _4z3, granting the Destroyer of Antiquity title. The mission is the Zilart log's Return to
// Delkfutt's Tower, entered at Zilart status 1. Its one group, the Kamlanaut of each area (Kamlanaut + 0 to 2), wins
// the fight when it dies. The win sets battlefieldWin only while the mission is current; the mission's 32001 finish
// (Missions/Zilart/ReturnToDelkfuttsTower.cs) reads it.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.StellarFulcrum
{
    public sealed class ReturnToDelkfuttsTower : BattlefieldScript
    {
        public ReturnToDelkfuttsTower() : base("RETURN_TO_DELKFUTTS_TOWER") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Stellar_Fulcrum/return_to_delkfutts_tower";

        public override BattlefieldData Data { get; } = new BattlefieldMissionData
        {
            ZoneId = Zone.StellarFulcrum,
            BattlefieldId = Battlefield.ReturnToDelkfuttsTower,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 0,
            EntryNpcs = new[] { Npc.StellarFulcrum._4z0 },
            ExitNpcs = new[] { Npc.StellarFulcrum._4z1, Npc.StellarFulcrum._4z2, Npc.StellarFulcrum._4z3 },
            MissionArea = XI.Quests.MissionLog.Zilart,
            Mission = XI.QuestContent.Mission.Zilart.ReturnToDelkfuttsTower,
            MissionStatusArea = XI.Quests.MissionLog.Zilart,
            RequiredMissionStatus = 1,
            Title = Title.DestroyerOfAntiquity,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { new BattlefieldMobId("KAMLANAUT", 0) },
                        new[] { new BattlefieldMobId("KAMLANAUT", 1) },
                        new[] { new BattlefieldMobId("KAMLANAUT", 2) },
                    },
                    AllDeath = (q, mob) => q.Battlefield.SetStatus(BattlefieldConst.StatusWon),
                },
            },
        };
    }
}
