// Rank 2 Mission 2 (Waughroon Shrine, the Three Kingdoms' Bastok leg), a line-for-line port of
// scripts/battlefields/Waughroon_Shrine/rank_2_mission.lua (quest rework C2). !pos -345 104 -260 144. A mission
// battlefield of 15 minutes for six, trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL, entered
// at BC_Entrance and left at Burning_Circle; Dark Dragon and Seeker are the essential mobs. It overrides two methods:
// entryRequirement (San d'Oria's Journey to Bastok 2 or Windurst's Three Kingdoms Bastok 2 current at status 10 or
// more, or for a helper the nation's mission 5 completed) and checkSkipCutscene (either of the two completed, or
// either current with the nation's status above 9). Kept as the Lua has it: the status read is the nation's log's, not
// the log of the mission it checks; mission 5 is a bare number.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.WaughroonShrine
{
    public sealed class Rank2Mission2 : BattlefieldScript
    {
        public Rank2Mission2() : base("RANK_2_MISSION_2") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Waughroon_Shrine/rank_2_mission";

        public override BattlefieldData Data { get; } = new BattlefieldData
        {
            ZoneId = Zone.WaughroonShrine,
            BattlefieldId = Battlefield.Rank2Mission2,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(15)
            TimeLimit = 15 * 60,
            Index = 0,
            EntryNpcs = new[] { Npc.WaughroonShrine.BCEntrance },
            ExitNpcs = new[] { Npc.WaughroonShrine.BurningCircle },
            EssentialMobs = { new[] { "Dark_Dragon", "Seeker" } },
        };

        public override bool EntryRequirement(IBattlefieldContext q, bool isRegistrant)
        {
            var player = q.Player;
            bool isCurrentMission =
                player.CurrentMission(MissionLog.Sandoria) == Mission.Sandoria.JourneyToBastok2 ||
                player.CurrentMission(MissionLog.Windurst) == Mission.Windurst.TheThreeKingdomsBastok2;
            int missionStatus = player.MissionStatus(player.Nation);
            bool currentRequirements = isCurrentMission && missionStatus >= 10;
            bool nonRegistrantReqs = player.HasCompletedMission(player.Nation, 5) || currentRequirements;

            return (!isRegistrant && nonRegistrantReqs) || currentRequirements;
        }

        public override bool CheckSkipCutscene(IBattlefieldContext q)
        {
            var player = q.Player;
            return player.HasCompletedMission(MissionLog.Windurst, Mission.Windurst.TheThreeKingdomsBastok2) ||
                   player.HasCompletedMission(MissionLog.Sandoria, Mission.Sandoria.JourneyToBastok2) ||
                   (player.MissionStatus(player.Nation) > 9 &&
                    (player.CurrentMission(MissionLog.Windurst) == Mission.Windurst.TheThreeKingdomsBastok2 ||
                     player.CurrentMission(MissionLog.Sandoria) == Mission.Sandoria.JourneyToBastok2));
        }
    }
}
