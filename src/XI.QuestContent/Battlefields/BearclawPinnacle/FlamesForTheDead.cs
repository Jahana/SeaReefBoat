// Flames for the Dead (Bearclaw Pinnacle, Promathia 5-3's Ulmia fight), a line-for-line port of
// scripts/battlefields/Bearclaw_Pinnacle/flames_for_the_dead.lua (quest rework C3, C3b). A BattlefieldMission of 30
// minutes for six, no experience lost, debuffs cleaned up, capped at xi.settings.main.MAX_LEVEL, entered at
// Wind_Pillar_1 (index 0) and left at Wind_Pillar_Exit, granting 1000 experience and the True Companion of Louverance
// title. The mission is the Promathia log's Three Paths, entered by Ulmia's status field (position 4) at 8. The Snoll
// Tzar is its one essential mob (the fight is won by its death through the framework); a loss says BLOWN_AWAY first.
// The win sets battlefieldWin only while the mission is current; the mission's 32001 finish
// (Missions/Cop/ThreePaths.cs) reads it.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.BearclawPinnacle
{
    public sealed class FlamesForTheDead : BattlefieldScript
    {
        public FlamesForTheDead() : base("FLAMES_FOR_THE_DEAD") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Bearclaw_Pinnacle/flames_for_the_dead";

        public override BattlefieldData Data { get; } = new BattlefieldMissionData
        {
            ZoneId = Zone.BearclawPinnacle,
            BattlefieldId = Battlefield.FlamesForTheDead,
            CanLoseExp = false,
            CleanupDebuffs = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 0,
            EntryNpcs = new[] { Npc.BearclawPinnacle.WindPillar1 },
            ExitNpcs = new[] { Npc.BearclawPinnacle.WindPillarExit },
            MissionArea = XI.Quests.MissionLog.CoP,
            Mission = XI.QuestContent.Mission.Cop.ThreePaths,
            MissionStatusArea = XI.Quests.MissionLog.CoP,
            // xi.mission.status.COP.ULMIA
            MissionStatus = 4,
            RequiredMissionStatus = 8,
            Title = Title.TrueCompanionOfLouverance,
            GrantXP = 1000,
            EssentialMobs = { new[] { Npc.BearclawPinnacle.SnollTzar } },
        };

        public override void OnBattlefieldLoss(IBattlefieldContext q)
        {
            q.Player.MessageSpecial(q.Text.Id(Text.BearclawPinnacle.BLOWN_AWAY));

            base.OnBattlefieldLoss(q);
        }
    }
}
