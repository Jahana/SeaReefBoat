// When Angels Fall (The Garden of Ru'Hmet, Promathia 8-3's fight), a line-for-line port of
// scripts/battlefields/The_Garden_of_RuHmet/when_angels_fall.lua (quest rework C3, C4). A BattlefieldMission of 30
// minutes for six, trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL, entered at _0z0 (index 0,
// always area 1), with no exit NPC. The mission is the Promathia log's When Angels Fall, entered by the char var
// Mission[6][828]Status at 4. Its one group is the four Ixzdei, superlinked; their death wins the fight. Kept data
// quirk: grantXP is 1000, but onEventFinishWin replaces BattlefieldMission's finish and only sets the char down at 420
// 0 445 (rotation 192) in the zone, so the win gives no experience. The win sets battlefieldWin only while the mission
// is current; the mission's 32001 finish (Missions/Cop/WhenAngelsFall.cs) reads it.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.TheGardenOfRuhmet
{
    public sealed class WhenAngelsFall : BattlefieldScript
    {
        public WhenAngelsFall() : base("WHEN_ANGELS_FALL") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/The_Garden_of_RuHmet/when_angels_fall";

        static BattlefieldMobId Ixzdei(int offset) => new BattlefieldMobId("IXZDEI_RDM", offset);

        public override BattlefieldData Data { get; } = new BattlefieldMissionData
        {
            ZoneId = Zone.TheGardenOfRuhmet,
            BattlefieldId = Battlefield.WhenAngelsFall,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 0,
            Area = 1,
            EntryNpcs = new[] { Npc.TheGardenOfRuhmet._0z0 },
            MissionArea = XI.Quests.MissionLog.CoP,
            Mission = XI.QuestContent.Mission.Cop.WhenAngelsFall,
            RequiredVar = "Mission[6][828]Status",
            RequiredValue = 4,
            GrantXP = 1000,

            Groups =
            {
                new BattlefieldGroup
                {
                    MobIds = new[] { Ixzdei(0), Ixzdei(1), Ixzdei(2), Ixzdei(3) },
                    Superlink = true,
                    AllDeath = (q, mob) => q.Battlefield.SetStatus(BattlefieldConst.StatusWon),
                },
            },
        };

        public override void OnEventFinishWin(IBattlefieldContext q, int csid, int option) =>
            q.Player.SetPos(420, 0, 445, 192);
    }
}
