// Head Wind (Boneyard Gully, Promathia 5-3's Ulmia fight), a line-for-line port of
// scripts/battlefields/Boneyard_Gully/head_wind.lua (quest rework C3, C3b). A BattlefieldMission of 30 minutes for six,
// trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL, entered at _081 (index 0) and left at _082,
// _084 or _086, granting 1000 experience and the Delta Enforcer title. The mission is the Promathia log's Three Paths,
// entered by Ulmia's status field (position 4) at 7. The three Shikarees (Z, Y, X) of each area are one superlinked
// group (they aggro together); each dies on its own, and the group's allDeath wins once all three are dead. The
// Shikarees' skillchain AI is Shared/ShikareeSkillchain unchanged, with the area stride 5 and every chain below, run by
// onBattlefieldTick after the base tick. The win sets battlefieldWin only while the mission is current; the mission's
// 32001 finish (Missions/Cop/ThreePaths.cs) reads it.
using System.Collections.Generic;
using XI.QuestContent.Battlefields.Shared;
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.BoneyardGully
{
    public sealed class HeadWind : BattlefieldScript
    {
        public HeadWind() : base("HEAD_WIND") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Boneyard_Gully/head_wind";

        const string Z = Mob.BoneyardGully.SHIKAREE_Z_HW;
        const string Y = Mob.BoneyardGully.SHIKAREE_Y_HW;
        const string X = Mob.BoneyardGully.SHIKAREE_X_HW;

        // xi.mobSkill
        const int Shadowstitch = 18;
        const int DancingEdge = 23;
        const int SharkBite = 24;
        const int Evisceration = 25;
        const int VorpalScythe = 101;
        const int Guillotine1 = 102;
        const int SpiralHell = 104;
        const int PentaThrust = 116;
        const int Skewer = 118;
        const int WheelingThrust = 119;
        const int ImpulseDrive = 120;

        static ShikareeStep[] Chain(params (string Mob, int Skill)[] steps)
        {
            var chain = new ShikareeStep[steps.Length];
            for (int i = 0; i < steps.Length; i++)
            {
                chain[i] = new ShikareeStep(steps[i].Mob, steps[i].Skill);
            }

            return chain;
        }

        static readonly ShikareeSkillchain Ai = new ShikareeSkillchain
        {
            ZoneId = Zone.BoneyardGully,
            Stride = 5,
            FirstMob = Z,
            Slots = 3,
            ResetFrom = 0,
            ResetTo = 2,
            MessageOffsets = new[]
            {
                (X, Text.BoneyardGully.SHIKAREE_X_OFFSET),
                (Y, Text.BoneyardGully.SHIKAREE_Y_OFFSET),
                (Z, Text.BoneyardGully.SHIKAREE_Z_OFFSET),
            },
            Nested = new Dictionary<string, (ShikareeStep[] All, Dictionary<string, ShikareeStep[]> With)>
            {
                // Shikaree X initiated
                [X] = (
                    Chain((X, Evisceration), (Y, VorpalScythe), (Z, ImpulseDrive)),
                    new Dictionary<string, ShikareeStep[]>
                    {
                        [Y] = Chain((X, Evisceration), (Y, VorpalScythe)),
                        [Z] = Chain((X, DancingEdge), (Z, PentaThrust)),
                    }),
                // Shikaree Y initiated
                [Y] = (
                    Chain((Y, Guillotine1), (X, Shadowstitch), (Z, WheelingThrust)),
                    new Dictionary<string, ShikareeStep[]>
                    {
                        [X] = Chain((Y, SpiralHell), (X, Shadowstitch)),
                        [Z] = Chain((Y, SpiralHell), (Z, ImpulseDrive)),
                    }),
                // Shikaree Z initiated
                [Z] = (
                    Chain((Z, Skewer), (Y, SpiralHell), (X, Evisceration)),
                    new Dictionary<string, ShikareeStep[]>
                    {
                        [X] = Chain((Z, WheelingThrust), (X, SharkBite)),
                        [Y] = Chain((Z, Skewer), (Y, SpiralHell)),
                    }),
            },
        };

        static BattlefieldMobId[] Trio(int first) => new[]
        {
            new BattlefieldMobId(Z, first),
            new BattlefieldMobId(Y, first),
            new BattlefieldMobId(X, first),
        };

        public override BattlefieldData Data { get; } = new BattlefieldMissionData
        {
            ZoneId = Zone.BoneyardGully,
            BattlefieldId = Battlefield.HeadWind,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 0,
            EntryNpcs = new[] { Npc.BoneyardGully._081 },
            ExitNpcs = new[] { Npc.BoneyardGully._082, Npc.BoneyardGully._084, Npc.BoneyardGully._086 },
            MissionArea = XI.Quests.MissionLog.CoP,
            Mission = XI.QuestContent.Mission.Cop.ThreePaths,
            MissionStatusArea = XI.Quests.MissionLog.CoP,
            // xi.mission.status.COP.ULMIA
            MissionStatus = 4,
            RequiredMissionStatus = 7,
            GrantXP = 1000,
            Title = Title.DeltaEnforcer,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[] { Trio(0), Trio(5), Trio(10) },
                    SuperlinkGroup = 1,
                    AllDeath = (q, mob) => q.Battlefield.SetStatus(BattlefieldConst.StatusWon),
                },

                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { new BattlefieldMobId(Z, 4) },
                        new[] { new BattlefieldMobId(Z, 9) },
                        new[] { new BattlefieldMobId(Z, 14) },
                    },
                    SuperlinkGroup = 1,
                },

                // NOTE: Rabbit is spawned on start, but wyvern is a result of an on engage action.
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { new BattlefieldMobId(Z, 3) },
                        new[] { new BattlefieldMobId(Z, 8) },
                        new[] { new BattlefieldMobId(Z, 13) },
                    },
                    SuperlinkGroup = 1,
                    Spawned = false,
                },
            },
        };

        public override void OnBattlefieldTick(IBattlefieldContext q, int tick)
        {
            base.OnBattlefieldTick(q, tick);

            Ai.Tick(q);
        }
    }
}
