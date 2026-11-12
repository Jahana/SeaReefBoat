// Cactuar Suave (Chamber of Oracles KSNM30, Clotho Orb), a line-for-line port of
// scripts/battlefields/Chamber_of_Oracles/cactuar_suave.lua (quest rework C2). The battlefield is all data: the Lua
// file overrides no method, so every hook is the framework's (BattlefieldFramework). The Clotho Orb is traded to
// SC_Entrance and worn on entry (A_CRACK_HAS_FORMED), and a worn orb is refused (ORB_IS_CRACKED). Sabotender Campeon
// and Sabotender Amante are the essential mobs, whose deaths open the armoury crate with the nine loot groups below.
using System.Collections.Generic;
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.ChamberOfOracles
{
    public sealed class CactuarSuave : BattlefieldScript
    {
        public CactuarSuave() : base("CACTUAR_SUAVE") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Chamber_of_Oracles/cactuar_suave";

        public override BattlefieldData Data { get; } = new BattlefieldData
        {
            ZoneId = Zone.ChamberOfOracles,
            BattlefieldId = Battlefield.CactuarSuave,
            MaxPlayers = 6,
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 5,
            EntryNpcs = new[] { Npc.ChamberOfOracles.SCEntrance },
            ExitNpcs = new[] { Npc.ChamberOfOracles.ShimmeringCircle },
            RequiredItems = new BattlefieldRequiredItems
            {
                Items = new[] { Item.ClothoOrb },
                WearMessage = Text.ChamberOfOracles.A_CRACK_HAS_FORMED,
                WornMessage = Text.ChamberOfOracles.ORB_IS_CRACKED,
            },
            EssentialMobs = { new[] { "Sabotender_Campeon", "Sabotender_Amante" } },
            Loot = new List<BattlefieldLootGroup>
            {
                new BattlefieldLootGroup
                {
                    { Item.Gil, 10000, 24000 },
                },
                new BattlefieldLootGroup
                {
                    { Item.CactuarNeedle, 10000 },
                },
                new BattlefieldLootGroup
                {
                    { Item.BagOfCactusStems, 10000 },
                },
                new BattlefieldLootGroup
                {
                    { Item.TaillefersDagger, 1875 },
                    { Item.SchiltronSpear, 1875 },
                    { Item.ZiskasCrossbow, 1875 },
                    { Item.Unji, 1875 },
                    { Item.DamascusIngot, 625 },
                    { Item.SpoolOfMalboroFiber, 625 },
                    { Item.SquareOfDamasceneCloth, 625 },
                    { Item.VialOfBlackBeetleBlood, 625 },
                },
                new BattlefieldLootGroup
                {
                    { Item.CactuarRibbon, 2500 },
                    { Item.PoleGrip, 2000 },
                    { Item.SwordStrap, 1500 },
                    { Item.SpearStrap, 1500 },
                    { Item.VileElixir, 1250 },
                    { Item.VileElixirP1, 1250 },
                },
                new BattlefieldLootGroup
                {
                    { Item.None, 1500 },
                    { Item.KingMaker, 1500 },
                    { Item.BalansSword, 1500 },
                    { Item.ArgentDagger, 1500 },
                    { Item.CapricornStaff, 1500 },
                    { Item.LightSpiritPact, 500 },
                    { Item.ScrollOfCureV, 500 },
                    { Item.ScrollOfRaiseIii, 500 },
                    { Item.ScrollOfShellIv, 500 },
                    { Item.ScrollOfThunderIii, 500 },
                },
                new BattlefieldLootGroup
                {
                    { Item.ChunkOfDarksteelOre, 550 },
                    { Item.ChunkOfGoldOre, 560 },
                    { Item.ChunkOfMythrilOre, 560 },
                    { Item.ChunkOfPlatinumOre, 550 },
                    { Item.EbonyLog, 560 },
                    { Item.MahoganyLog, 560 },
                    { Item.PetrifiedLog, 550 },
                    { Item.PhilosophersStone, 550 },
                    { Item.SpoolOfGoldThread, 560 },
                    { Item.SquareOfRainbowCloth, 560 },
                    { Item.SquareOfRaxa, 550 },
                    { Item.CoralFragment, 550 },
                    { Item.DemonHorn, 550 },
                    { Item.HandfulOfWyvernScales, 550 },
                    { Item.RamHorn, 560 },
                    { Item.SlabOfGranite, 560 },
                    { Item.Reraiser, 560 },
                    { Item.HiReraiser, 560 },
                },
                new BattlefieldLootGroup
                {
                    { Item.None, 3600 },
                    { Item.ChunkOfDarksteelOre, 400 },
                    { Item.ChunkOfGoldOre, 400 },
                    { Item.ChunkOfMythrilOre, 400 },
                    { Item.ChunkOfPlatinumOre, 400 },
                    { Item.EbonyLog, 400 },
                    { Item.MahoganyLog, 400 },
                    { Item.PetrifiedLog, 400 },
                    { Item.PhilosophersStone, 400 },
                    { Item.SpoolOfGoldThread, 400 },
                    { Item.SquareOfRainbowCloth, 400 },
                    { Item.SquareOfRaxa, 400 },
                    { Item.CoralFragment, 400 },
                    { Item.DemonHorn, 400 },
                    { Item.HandfulOfWyvernScales, 400 },
                    { Item.RamHorn, 400 },
                    { Item.SlabOfGranite, 400 },
                },
                new BattlefieldLootGroup
                {
                    { Item.None, 3000 },
                    { Item.VialOfBlackBeetleBlood, 435 },
                    { Item.SquareOfDamasceneCloth, 435 },
                    { Item.DamascusIngot, 435 },
                    { Item.SpoolOfMalboroFiber, 435 },
                    { Item.PhilosophersStone, 1400 },
                    { Item.PhoenixFeather, 2450 },
                    { Item.SquareOfRaxa, 1400 },
                },
            },
        };
    }
}
