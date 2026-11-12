// Requiem of Sin (Boneyard Gully quest battlefield), a line-for-line port of
// scripts/battlefields/Boneyard_Gully/requiem_of_sin.lua (quest rework C3, Q3c). A BattlefieldQuest of the other-areas
// quest Requiem of Sin: 30 minutes for 6, index 6, entered at _081 with the Letter from Shikaree Y or the Letter from
// the Mithran Trackers (neither is taken; BURSTS_INTO_FLAMES when a char holds none) and left at _082, _084 or _086.
// checkRequirements replaces the framework's: the entry check, then either letter; the quest status does not matter.
// The three Shikarees (Z, Y, X) of each area die together (superlinked, their death wins), with three armoury crates
// (one to an area) and four loot groups. The Shikarees' skillchain AI is Shared/ShikareeSkillchain (stride 6, three
// Shikarees, every chain below), run by onBattlefieldTick after the base tick.
using System.Collections.Generic;
using XI.QuestContent.Battlefields.Shared;
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.BoneyardGully
{
    public sealed class RequiemOfSin : BattlefieldScript
    {
        public RequiemOfSin() : base("REQUIEM_OF_SIN") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Boneyard_Gully/requiem_of_sin";

        const string Z = "SHIKAREE_Z_ROS";
        const string Y = "SHIKAREE_Y_ROS_TWT";
        const string X = "SHIKAREE_X_ROS_TWT";

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
            Stride = 6,
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

        // quantity = 2: the group is rolled twice
        static BattlefieldLootGroup Twice(BattlefieldLootGroup group)
        {
            group.Quantity = 2;

            return group;
        }

        static BattlefieldMobId[] Trio(int first) => new[]
        {
            new BattlefieldMobId(Z, first),
            new BattlefieldMobId(Y, first),
            new BattlefieldMobId(X, first),
        };

        public override BattlefieldData Data { get; } = new BattlefieldQuestData
        {
            ZoneId = Zone.BoneyardGully,
            BattlefieldId = Battlefield.RequiemOfSin,
            MaxPlayers = 6,
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 6,
            EntryNpcs = new[] { Npc.BoneyardGully._081 },
            ExitNpcs = new[] { Npc.BoneyardGully._082, Npc.BoneyardGully._084, Npc.BoneyardGully._086 },
            QuestArea = XI.Quests.QuestLog.OtherAreas,
            Quest = "REQUIEM_OF_SIN",
            RequiredKeyItems = new BattlefieldRequiredKeyItems
            {
                Items =
                {
                    BattlefieldKeyItem.AnyOf(KeyItem.LetterFromShikareeY, KeyItem.LetterFromTheMithranTrackers),
                },
                Message = Text.BoneyardGully.BURSTS_INTO_FLAMES,
            },
            ArmouryCrates = new[]
            {
                new BattlefieldArmouryCrate { Id = new BattlefieldMobId(Z, 5) },
                new BattlefieldArmouryCrate { Id = new BattlefieldMobId(Z, 11) },
                new BattlefieldArmouryCrate { Id = new BattlefieldMobId(Z, 17) },
            },

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[] { Trio(0), Trio(6), Trio(12) },
                    SuperlinkGroup = 1,
                    AllDeath = (q, mob) => q.Framework.HandleAllMonstersDefeated(mob),
                },

                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { new BattlefieldMobId(Z, 4) },
                        new[] { new BattlefieldMobId(Z, 10) },
                        new[] { new BattlefieldMobId(Z, 16) },
                    },
                    SuperlinkGroup = 1,
                },

                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { new BattlefieldMobId(Z, 3) },
                        new[] { new BattlefieldMobId(Z, 9) },
                        new[] { new BattlefieldMobId(Z, 15) },
                    },
                    SuperlinkGroup = 1,
                    Spawned = false,
                },
            },

            Loot = new List<BattlefieldLootGroup>
            {
                Twice(new BattlefieldLootGroup
                {
                    { Item.None, 7500 },
                    { Item.PotOfViridianUrushi, 3500 },
                    { Item.PieceOfCassiaLumber, 2500 },
                    { Item.DragonBone, 2500 },
                    { Item.SquareOfEltoroLeather, 2500 },
                    { Item.SquareOfGalateia, 2500 },
                    { Item.SquareOfKejusuSatin, 2500 },
                }),
                new BattlefieldLootGroup
                {
                    { Item.None, 7500 },
                    { Item.XsKnife, 500 },
                    { Item.YsScythe, 500 },
                    { Item.ZsTrident, 500 },
                    { Item.LacquerTreeLog, 50 },
                    { Item.PhoenixFeather, 50 },
                    { Item.PhilosophersStone, 50 },
                },
                new BattlefieldLootGroup
                {
                    { Item.None, 750 },
                    { Item.LacquerTreeLog, 50 },
                    { Item.PhoenixFeather, 50 },
                    { Item.PhilosophersStone, 50 },
                },
                new BattlefieldLootGroup
                {
                    { Item.None, 800 },
                    { Item.PetrifiedLog, 40 },
                    { Item.DivineLog, 40 },
                    { Item.SquareOfShiningCloth, 40 },
                    { Item.SquareOfRaxa, 40 },
                    { Item.SlabOfGranite, 40 },
                },
            },
        };

        // Override: the quest status does not matter for entry, only the key items.
        public override bool CheckRequirements(IBattlefieldContext q, bool isRegistrant)
        {
            if (!q.Framework.IsValidEntry())
            {
                return false;
            }

            return q.Player.HasKeyItem(KeyItem.LetterFromShikareeY) ||
                q.Player.HasKeyItem(KeyItem.LetterFromTheMithranTrackers);
        }

        public override void OnBattlefieldTick(IBattlefieldContext q, int tick)
        {
            base.OnBattlefieldTick(q, tick);

            Ai.Tick(q);
        }
    }
}
