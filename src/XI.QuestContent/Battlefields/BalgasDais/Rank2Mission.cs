// Rank 2 Mission (Balgas Dais, the Three Kingdoms' Windurst leg), a line-for-line port of
// scripts/battlefields/Balgas_Dais/rank_2_mission.lua (quest rework C3 N2). !pos 299 -123 345 146. A mission
// battlefield of 15 minutes for six, trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL, entered
// at BC_Entrance and left at Burning_Circle; Black Dragon and Searcher are the essential mobs. It overrides two
// methods: entryRequirement (San d'Oria's Journey to Windurst 2 or Bastok's The Emissary Windurst 2 current with the
// Dark Key held, or for a helper the nation's mission 5 completed) and checkSkipCutscene (either of the two completed,
// or either current with the nation's status above 8). Kept as the Lua has it: the status read is the nation's log's,
// not the log of the mission it checks; mission 5 is a bare number; a Windurst char never matches the current test.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.BalgasDais
{
    public sealed class Rank2Mission : BattlefieldScript
    {
        public Rank2Mission() : base("RANK_2_MISSION") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Balgas_Dais/rank_2_mission";

        public override BattlefieldData Data { get; } = new BattlefieldData
        {
            ZoneId = Zone.BalgasDais,
            BattlefieldId = Battlefield.Rank2Mission,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(15)
            TimeLimit = 15 * 60,
            Index = 0,
            EntryNpcs = new[] { Npc.BalgasDais.BCEntrance },
            ExitNpcs = new[] { Npc.BalgasDais.BurningCircle },
            EssentialMobs = { new[] { "Black_Dragon", "Searcher" } },
        };

        public override bool EntryRequirement(IBattlefieldContext q, bool isRegistrant)
        {
            var player = q.Player;
            bool isCurrentMission =
                player.CurrentMission(MissionLog.Sandoria) == Mission.Sandoria.JourneyToWindurst2 ||
                player.CurrentMission(MissionLog.Bastok) == Mission.Bastok.TheEmissaryWindurst2;
            bool currentRequirements = isCurrentMission && player.HasKeyItem(KeyItem.DarkKey);
            bool nonRegistrantReqs = player.HasCompletedMission(player.Nation, 5) || currentRequirements;

            return (!isRegistrant && nonRegistrantReqs) || currentRequirements;
        }

        public override bool CheckSkipCutscene(IBattlefieldContext q)
        {
            var player = q.Player;
            return player.HasCompletedMission(MissionLog.Sandoria, Mission.Sandoria.JourneyToWindurst2) ||
                   player.HasCompletedMission(MissionLog.Bastok, Mission.Bastok.TheEmissaryWindurst2) ||
                   (player.MissionStatus(player.Nation) > 8 &&
                    (player.CurrentMission(MissionLog.Sandoria) == Mission.Sandoria.JourneyToWindurst2 ||
                     player.CurrentMission(MissionLog.Bastok) == Mission.Bastok.TheEmissaryWindurst2));
        }
    }
}
