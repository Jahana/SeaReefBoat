// Rank 2 Mission 1 (Horlais Peak, the Three Kingdoms' San d'Oria leg), a line-for-line port of
// scripts/battlefields/Horlais_Peak/rank_2_mission.lua (quest rework C3 N2). A mission battlefield of 15 minutes for
// six, trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL, entered at BC_Entrance and left at
// Burning_Circle; Dread Dragon and Spotter are the essential mobs. It overrides two methods: entryRequirement
// (Windurst's The Three Kingdoms San d'Oria 2 or Bastok's The Emissary San d'Oria 2 current at the nation's status 9
// or more, or for a helper the nation's mission 5 completed) and checkSkipCutscene (either of the two completed, or
// either current with the nation's status above 9). Kept as the Lua has it: the status read is the nation's log's, not
// the log of the mission it checks; mission 5 is a bare number; a San d'Orian never matches the current test.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.HorlaisPeak
{
    public sealed class Rank2Mission1 : BattlefieldScript
    {
        public Rank2Mission1() : base("RANK_2_MISSION_1") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Horlais_Peak/rank_2_mission";

        public override BattlefieldData Data { get; } = new BattlefieldData
        {
            ZoneId = Zone.HorlaisPeak,
            BattlefieldId = Battlefield.Rank2Mission1,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(15)
            TimeLimit = 15 * 60,
            Index = 0,
            EntryNpcs = new[] { Npc.HorlaisPeak.BCEntrance },
            ExitNpcs = new[] { Npc.HorlaisPeak.BurningCircle },
            EssentialMobs = { new[] { "Dread_Dragon", "Spotter" } },
        };

        public override bool EntryRequirement(IBattlefieldContext q, bool isRegistrant)
        {
            var player = q.Player;
            bool isCurrentMission =
                player.CurrentMission(MissionLog.Windurst) == Mission.Windurst.TheThreeKingdomsSandoria2 ||
                player.CurrentMission(MissionLog.Bastok) == Mission.Bastok.TheEmissarySandoria2;
            int missionStatus = player.MissionStatus(player.Nation);
            bool currentRequirements = isCurrentMission && missionStatus >= 9;
            bool nonRegistrantReqs = player.HasCompletedMission(player.Nation, 5) || currentRequirements;

            return (!isRegistrant && nonRegistrantReqs) || currentRequirements;
        }

        public override bool CheckSkipCutscene(IBattlefieldContext q)
        {
            var player = q.Player;
            return player.HasCompletedMission(MissionLog.Windurst, Mission.Windurst.TheThreeKingdomsSandoria2) ||
                   player.HasCompletedMission(MissionLog.Bastok, Mission.Bastok.TheEmissarySandoria2) ||
                   (player.MissionStatus(player.Nation) > 9 &&
                    (player.CurrentMission(MissionLog.Windurst) == Mission.Windurst.TheThreeKingdomsSandoria2 ||
                     player.CurrentMission(MissionLog.Bastok) == Mission.Bastok.TheEmissarySandoria2));
        }
    }
}
