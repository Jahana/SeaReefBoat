// The Warrior's Path (Sealion's Den, Promathia 7-5's fight), a line-for-line port of
// scripts/battlefields/Sealions_Den/warriors_path.lua (quest rework C3, C4). A BattlefieldMission of 30 minutes for
// six, trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL, entered at _0w0 (index 1) and left at
// the Airship_Door, granting the Unquenchable Light title. The mission is the Promathia log's The Warrior's Path,
// entered by the char var Mission[6][748]Status at 1. Two groups: Tenzen (one id to an area, every fourth), whose
// group's allDeath would set the win (never reached: see below), and his three Taru helpers. onEventFinishWin adds The
// Chebukki's Worst Nightmare, then runs BattlefieldMission's finish (both titles; Unquenchable Light is the one left
// displayed); a loss says COSMIC_ELUCIDATION first. The win sets battlefieldWin only while the mission is current; the
// mission's 32001 finish (Missions/Cop/TheWarriorsPath.cs) reads it. Tenzen is unkillable (his mob script wins the
// fight at 15% HP), so the first group's allDeath is not reached live; it is ported as is. In areas 2 and 3 his script
// raises a Lua error at 15% before its win (Tenzen.lua:250-259: its taruOffsets hold area 1's Taru ids only), so those
// fights end only by time-up or a wipe (a kept Lua bug of the mob script).
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.SealionsDen
{
    public sealed class WarriorsPath : BattlefieldScript
    {
        public WarriorsPath() : base("WARRIORS_PATH") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Sealions_Den/warriors_path";

        static BattlefieldMobId Tenzen(int offset) => new BattlefieldMobId("TENZEN", offset);

        public override BattlefieldData Data { get; } = new BattlefieldMissionData
        {
            ZoneId = Zone.SealionsDen,
            BattlefieldId = Battlefield.WarriorsPath,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 1,
            EntryNpcs = new[] { Npc.SealionsDen._0w0 },
            ExitNpcs = new[] { Npc.SealionsDen.AirshipDoor },
            MissionArea = XI.Quests.MissionLog.CoP,
            Mission = XI.QuestContent.Mission.Cop.TheWarriorsPath,
            RequiredVar = "Mission[6][748]Status",
            RequiredValue = 1,
            Title = Title.UnquenchableLight,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { Tenzen(0) },
                        new[] { Tenzen(4) },
                        new[] { Tenzen(8) },
                    },
                    AllDeath = (q, mob) => q.Battlefield.SetStatus(BattlefieldConst.StatusWon),
                },
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { Tenzen(1), Tenzen(2), Tenzen(3) },
                        new[] { Tenzen(5), Tenzen(6), Tenzen(7) },
                        new[] { Tenzen(9), Tenzen(10), Tenzen(11) },
                    },
                },
            },
        };

        // Both titles are granted here. Unquenchable Light is the one left displayed.
        public override void OnEventFinishWin(IBattlefieldContext q, int csid, int option)
        {
            q.Player.AddTitle(Title.TheChebukkisWorstNightmare);
            base.OnEventFinishWin(q, csid, option);
        }

        public override void OnBattlefieldLoss(IBattlefieldContext q)
        {
            q.Player.MessageSpecial(q.Text.Id(Text.SealionsDen.COSMIC_ELUCIDATION));
            base.OnBattlefieldLoss(q);
        }
    }
}
