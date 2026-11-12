// Dawn (Empyreal Paradox, Promathia 8-4's fight), a line-for-line port of
// scripts/battlefields/Empyreal_Paradox/dawn.lua (quest rework C3, C4). A BattlefieldMission of 30 minutes for six,
// trusts allowed, no experience lost, no wipe grace, capped at xi.settings.main.MAX_LEVEL, entered at TR_Entrance
// (index 0) and left at Transcendental_Radiance (a kept data quirk: the zone data's three Transcendental_Radiance are
// cutscene_only, so the exit NPC can't be triggered and onExitTrigger never runs). The mission is the Promathia log's
// Dawn, entered by the char var Mission[6][840]Status at 1. setupBattlefield keeps the initiator's race in the local
// var initRace and puts Prishe and Selhteus (target ids 11 and 12) in as allies at the area's spots, level 75.
// Promathia's first form dead (the first group): every ally mob's local vars reset, Prishe (pool PRISHE_DAWN) and
// Selhteus (SELHTEUS_DAWN) set back at their spots, Prishe's engage wait set to 180 seconds from now, and every player
// plays 32004 (the area). Its finish (onEventFinishBattlefield) sets the char at the area's spot and spawns the second
// form (PROMATHIA + 2 * (area - 1) + 1) unless it is up; that form's death wins. Kept data quirks: grantXP is 2000 and
// title Averter of the Apocalypse, but onEventFinishWin replaces BattlefieldMission's finish and only sets the char
// down at 540 0 -514 (rotation 63) in the zone, so the win gives neither. The win sets battlefieldWin only while the
// mission is current; the mission's 32001 finish (Missions/Cop/Dawn.cs) reads it.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.EmpyrealParadox
{
    public sealed class Dawn : BattlefieldScript
    {
        public Dawn() : base("DAWN") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Empyreal_Paradox/dawn";

        // xi.mobPool.PRISHE_DAWN and SELHTEUS_DAWN (scripts/enum/mob_pool.lua)
        const int PrisheDawn = 3199;
        const int SelhteusDawn = 5417;

        static BattlefieldMobId Promathia(int offset) => new BattlefieldMobId("PROMATHIA", offset);

        static readonly double[][] PlayerCoords =
        {
            new[] { -520.000, -120.000, 493.778, 190 },
            new[] { 520.000, 0.000, 493.778, 190 },
            new[] { -520.000, 120.000, -545.538, 190 },
        };

        static readonly double[][] PrisheCoords =
        {
            new[] { -526.678, -120.000, 509.175, 192 },
            new[] { 513.225, 0.000, 509.710, 192 },
            new[] { -527.256, 120.000, -533.638, 192 },
        };

        static readonly double[][] SelhteusCoords =
        {
            new[] { -513.769, -120.000, 509.175, 170 },
            new[] { 531.881, 0.000, 510.375, 170 },
            new[] { -506.980, 120.000, -533.638, 170 },
        };

        public override BattlefieldData Data { get; } = new BattlefieldMissionData
        {
            ZoneId = Zone.EmpyrealParadox,
            BattlefieldId = Battlefield.Dawn,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 0,
            EntryNpcs = new[] { Npc.EmpyrealParadox.TREntrance },
            ExitNpcs = new[] { Npc.EmpyrealParadox.TranscendentalRadiance },
            MissionArea = XI.Quests.MissionLog.CoP,
            Mission = XI.QuestContent.Mission.Cop.Dawn,
            RequiredVar = "Mission[6][840]Status",
            RequiredValue = 1,
            HasWipeGrace = false,
            GrantXP = 2000,
            Title = Title.AverterOfTheApocalypse,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { Promathia(0) },
                        new[] { Promathia(2) },
                        new[] { Promathia(4) },
                    },
                    AllDeath = (q, mob) =>
                    {
                        int battlefieldArea = q.Battlefield.Area;

                        // Reposition Prishe and Selhteus
                        foreach (var ally in q.Battlefield.Allies())
                        {
                            if (ally.IsMob)
                            {
                                ally.ResetLocalVars();

                                if (ally.Pool == PrisheDawn)
                                {
                                    var prishePos = PrisheCoords[battlefieldArea - 1];
                                    ally.SetPos(prishePos[0], prishePos[1], prishePos[2], (int)prishePos[3]);

                                    // Reset Prishe's engage wait time
                                    ally.SetLocalVar("[helperNpc]engageWaitTime", 180);
                                    ally.SetLocalVar("engageWait", q.Engine.GetSystemTime() + 180);
                                }
                                else if (ally.Pool == SelhteusDawn)
                                {
                                    var selhteusPos = SelhteusCoords[battlefieldArea - 1];
                                    ally.SetPos(selhteusPos[0], selhteusPos[1], selhteusPos[2], (int)selhteusPos[3]);
                                }
                            }
                        }

                        foreach (var player in q.Battlefield.Players())
                        {
                            player.StartEvent(32004, battlefieldArea);
                        }
                    },
                },
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { Promathia(1) },
                        new[] { Promathia(3) },
                        new[] { Promathia(5) },
                    },
                    Spawned = false,
                    AllDeath = (q, mob) => q.Battlefield.SetStatus(BattlefieldConst.StatusWon),
                },
            },
        };

        public override void SetupBattlefield(IBattlefieldContext q)
        {
            int battlefieldArea = q.Battlefield.Area;
            var (initiatorId, _) = q.Battlefield.Initiator();
            var initiator = q.Engine.GetPlayerByID(initiatorId);
            if (initiator != null)
            {
                q.Battlefield.SetLocalVar("initRace", initiator.Race);
            }

            var prishe = q.Battlefield.InsertEntity(11, true, true);
            var prishePos = PrisheCoords[battlefieldArea - 1];
            prishe.SetSpawn(prishePos[0], prishePos[1], prishePos[2], (int)prishePos[3]);
            prishe.Spawn();
            prishe.SetMobLevel(75);

            var selhteus = q.Battlefield.InsertEntity(12, true, true);
            var selhteusPos = SelhteusCoords[battlefieldArea - 1];
            selhteus.SetSpawn(selhteusPos[0], selhteusPos[1], selhteusPos[2], (int)selhteusPos[3]);
            selhteus.Spawn();
            selhteus.SetMobLevel(75);
        }

        public override void OnEventFinishBattlefield(IBattlefieldContext q, int csid, int option)
        {
            var battlefield = q.Player.CurrentBattlefield;
            int battlefieldArea = battlefield.Area;
            int phaseTwoMobId = q.Engine.ZoneMobId(Zone.EmpyrealParadox, "PROMATHIA").Value +
                                (battlefieldArea - 1) * 2 + 1;
            var playerPos = PlayerCoords[battlefieldArea - 1];

            q.Player.SetPos(playerPos[0], playerPos[1], playerPos[2], (int)playerPos[3]);

            // Bail out if anyone else got here first
            if (q.Engine.MobById(phaseTwoMobId).IsSpawned)
            {
                return;
            }

            // Spawn Promathia phase 2
            q.Engine.SpawnMob(phaseTwoMobId);
        }

        public override void OnEventFinishWin(IBattlefieldContext q, int csid, int option) =>
            q.Player.SetPos(540, 0, -514, 63, Zone.EmpyrealParadox);
    }
}
