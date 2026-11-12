// Darkness Named (The Shrouded Maw, Promathia 3-5's fight), a line-for-line port of
// scripts/battlefields/The_Shrouded_Maw/darkness_named.lua (quest rework C3). A BattlefieldMission of 30 minutes for
// six, trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL, entered at MC_Entrance and left at
// Memento_Circle, granting 1000 experience and the Dreambreaker title (the Transient Dreamer title too, in its
// onEventFinishWin). The mission is the Promathia log's Darkness Named, entered by the char var
// Mission[6][358]Status at 4. Its first group, the three Diabolos (one to an area), wins the fight when it dies; its
// second, the six Diremites of each area, is superlinked. setupBattlefield closes the doors of the area's eight tiles
// and onBattlefieldTick runs the Diremite respawn and pit logic, both in Shared/DiremitePits (which names its kept
// quirk: a tick with no players disengages the Diremite).
using XI.QuestContent.Battlefields.Shared;
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.TheShroudedMaw
{
    public sealed class DarknessNamed : BattlefieldScript
    {
        public DarknessNamed() : base("DARKNESS_NAMED") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/The_Shrouded_Maw/darkness_named";

        static BattlefieldMobId[] Mites(int first) => new[]
        {
            new BattlefieldMobId("DIABOLOS", first),
            new BattlefieldMobId("DIABOLOS", first + 1),
            new BattlefieldMobId("DIABOLOS", first + 2),
            new BattlefieldMobId("DIABOLOS", first + 3),
            new BattlefieldMobId("DIABOLOS", first + 4),
            new BattlefieldMobId("DIABOLOS", first + 5),
        };

        public override BattlefieldData Data { get; } = new BattlefieldMissionData
        {
            ZoneId = Zone.TheShroudedMaw,
            BattlefieldId = Battlefield.DarknessNamed,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 0,
            EntryNpcs = new[] { Npc.TheShroudedMaw.MCEntrance },
            ExitNpcs = new[] { Npc.TheShroudedMaw.MementoCircle },
            MissionArea = XI.Quests.MissionLog.CoP,
            Mission = XI.QuestContent.Mission.Cop.DarknessNamed,
            RequiredVar = "Mission[6][358]Status",
            RequiredValue = 4,
            GrantXP = 1000,
            Title = Title.Dreambreaker,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { new BattlefieldMobId("DIABOLOS", 0) },
                        new[] { new BattlefieldMobId("DIABOLOS", 7) },
                        new[] { new BattlefieldMobId("DIABOLOS", 14) },
                    },
                    AllDeath = (q, mob) => q.Battlefield.SetStatus(BattlefieldConst.StatusWon),
                },

                new BattlefieldGroup
                {
                    AreaMobIds = new[] { Mites(1), Mites(8), Mites(15) },
                    Superlink = true,
                },
            },
        };

        // Both titles are granted here. Dreambreaker is the one left displayed.
        public override void OnEventFinishWin(IBattlefieldContext q, int csid, int option)
        {
            q.Player.AddTitle(Title.TransientDreamer);

            base.OnEventFinishWin(q, csid, option);
        }

        public override void SetupBattlefield(IBattlefieldContext q)
        {
            DiremitePits.CloseTiles(q);
        }

        public override void OnBattlefieldTick(IBattlefieldContext q, int tick)
        {
            base.OnBattlefieldTick(q, tick);

            DiremitePits.Tick(q, "DIABOLOS");
        }
    }
}
