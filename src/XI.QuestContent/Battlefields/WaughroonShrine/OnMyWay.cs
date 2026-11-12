// On My Way (Waughroon Shrine, Bastok 7-2's fight), a line-for-line port of
// scripts/battlefields/Waughroon_Shrine/on_my_way.lua (quest rework C3 N5). !pos -345 104 -260 144. A
// BattlefieldMission of 30 minutes for six, trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL,
// entered at BC_Entrance (index 3) and left at Burning_Circle; no title, no experience. The mission is the Bastok
// log's On My Way, entered at Bastok status 2. Its one group, by id from Kuj'hu Graniteskin four to an area (+0 to +3,
// +4 to +7, +8 to +11), wins the fight when they all die. The win sets battlefieldWin only while the mission is
// current; the mission's 32001 finish (Missions/Bastok/OnMyWay.cs, scripts/missions/bastok/7_2_On_My_Way.lua) reads
// it.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.WaughroonShrine
{
    public sealed class OnMyWay : BattlefieldScript
    {
        public OnMyWay() : base("ON_MY_WAY") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Waughroon_Shrine/on_my_way";

        static BattlefieldMobId[] Ids(int first) => new[]
        {
            new BattlefieldMobId("KUJHU_GRANITESKIN", first),
            new BattlefieldMobId("KUJHU_GRANITESKIN", first + 1),
            new BattlefieldMobId("KUJHU_GRANITESKIN", first + 2),
            new BattlefieldMobId("KUJHU_GRANITESKIN", first + 3),
        };

        public override BattlefieldData Data { get; } = new BattlefieldMissionData
        {
            ZoneId = Zone.WaughroonShrine,
            BattlefieldId = Battlefield.OnMyWay,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 3,
            EntryNpcs = new[] { Npc.WaughroonShrine.BCEntrance },
            ExitNpcs = new[] { Npc.WaughroonShrine.BurningCircle },
            MissionArea = XI.Quests.MissionLog.Bastok,
            Mission = XI.QuestContent.Mission.Bastok.OnMyWay,
            MissionStatusArea = XI.Quests.MissionLog.Bastok,
            RequiredMissionStatus = 2,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[] { Ids(0), Ids(4), Ids(8) },
                    AllDeath = (q, mob) => q.Battlefield.SetStatus(BattlefieldConst.StatusWon),
                },
            },
        };
    }
}
