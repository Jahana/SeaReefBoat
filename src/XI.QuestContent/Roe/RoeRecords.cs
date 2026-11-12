// The Records of Eminence: 1081 records and the timed schedule. Generated once from the Lua table (quest rework
// C4), kept by hand since. tests/golden/roe_records.txt holds the Lua's numbers, and XI.Verify.Quests compares every
// record to it. Each record's comment is its Lua title.
using System.Collections.Generic;
using XI.Quests;

namespace XI.QuestContent.Roe
{
    public static class RoeRecords
    {
        private static readonly int[] AbysseaZones =
        {
            Zone.AbysseaKonschtat, Zone.AbysseaTahrongi, Zone.AbysseaLaTheine, Zone.AbysseaAttohwa,
            Zone.AbysseaMisareaux, Zone.AbysseaVunkerl, Zone.AbysseaAltepa, Zone.AbysseaUleguerand,
            Zone.AbysseaGrauberg, Zone.AbysseaEmpyrealParadox,
        };

        // [day][block]: Sunday first, six 4-hour blocks, all in JST
        public static readonly int[][] TimedSchedule =
        {
            new[] { 4021, 4010, 4016, 4012, 4018, 4013 },
            new[] { 4015, 4011, 4017, 4014, 4019, 4008 },
            new[] { 4016, 4012, 4018, 4013, 4020, 4009 },
            new[] { 4017, 4014, 4019, 4008, 4021, 4010 },
            new[] { 4018, 4013, 4020, 4009, 4015, 4011 },
            new[] { 4019, 4008, 4021, 4010, 4016, 4012 },
            new[] { 4020, 4009, 4015, 4011, 4017, 4014 },
        };

        public static readonly IReadOnlyList<RoeRecord> All = Build();

        private static List<RoeRecord> Build()
        {
            var r = new List<RoeRecord>(1081);
            Part0(r);
            Part1(r);
            Part2(r);
            Part3(r);
            Part4(r);
            Part5(r);
            Part6(r);
            Part7(r);
            Part8(r);
            Part9(r);
            Part10(r);
            return r;
        }

        private static void Part0(List<RoeRecord> r)
        {
            // First Step Forward +
            r.Add(new RoeRecord(1)
            {
                Reward = new RoeReward
                {
                    Exp = 300,
                    Item = new RoeItem[] { RoeItem.Of(Item.StripOfMeatJerky, 6) },
                    KeyItem = KeyItem.Memorandoll,
                    Sparks = 100,
                },
            });
            // Vanquish 1 Enemy +
            r.Add(new RoeRecord(2)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reward = new RoeReward { Exp = 500, Sparks = 100 },
            });
            // Undertake a FoV Training Regime
            r.Add(new RoeRecord(3)
            {
                Reward = new RoeReward { Exp = 500, Sparks = 100 },
            });
            // Heal without magic
            r.Add(new RoeRecord(4)
            {
                Reward = new RoeReward { Exp = 500, Sparks = 100 },
            });
            // All for One
            r.Add(new RoeRecord(5)
            {
                Reward = new RoeReward { Accolades = 1000, Exp = 300, KeyItem = KeyItem.Concordoll, Sparks = 100 },
            });
            // Undertake a GoV Training Regime
            r.Add(new RoeRecord(11)
            {
                Reward = new RoeReward { Exp = 500, Sparks = 100 },
            });
            // Vanquish Multiple Enemies I - 200
            r.Add(new RoeRecord(12)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 200,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobXp = true },
                Reward = new RoeReward { Accolades = 100, Exp = 5000, Sparks = 1000 },
            });
            // Vanquish Multiple Enemies II - 500
            r.Add(new RoeRecord(13)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 500,
                Reqs = new RoeReqs { MobXp = true },
                Reward = new RoeReward
                {
                    Exp = 6000,
                    Item = new RoeItem[] { RoeItem.Of(Item.PlutonCase) },
                    Sparks = 2000,
                },
            });
            // Vanquish Multiple Enemies III - 750
            r.Add(new RoeRecord(14)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 750,
                Reqs = new RoeReqs { MobXp = true },
                Reward = new RoeReward
                {
                    Exp = 10000,
                    Item = new RoeItem[] { RoeItem.Of(Item.PlutonBox) },
                    Sparks = 5000,
                },
            });
            // Level Sync to Vanquish I
            r.Add(new RoeRecord(15)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 200,
                Reqs = new RoeReqs { LevelSync = true, MobXp = true },
                Reward = new RoeReward { Accolades = 200, Exp = 6000, Sparks = 2000 },
            });
            // Deal 500+ Damage
            r.Add(new RoeRecord(16)
            {
                Trigger = RoeTrigger.DealDamage,
                Goal = 200,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { DmgMin = 500 },
                Reward = new RoeReward { Accolades = 100, Exp = 5000, Sparks = 1000 },
            });
            // Deal 1000+ Damage
            r.Add(new RoeRecord(17)
            {
                Trigger = RoeTrigger.DealDamage,
                Goal = 150,
                Reqs = new RoeReqs { DmgMin = 1000 },
                Reward = new RoeReward { Exp = 5000, Sparks = 1000 },
            });
            // Deal 1500+ Damage
            r.Add(new RoeRecord(18)
            {
                Trigger = RoeTrigger.DealDamage,
                Goal = 100,
                Reqs = new RoeReqs { DmgMin = 1500 },
                Reward = new RoeReward { Exp = 5000, Sparks = 1000 },
            });
            // Deal 10-20 Damage
            r.Add(new RoeRecord(19)
            {
                Trigger = RoeTrigger.DealDamage,
                Goal = 10,
                Reqs = new RoeReqs { DmgMax = 20, DmgMin = 10 },
                Reward = new RoeReward { Exp = 2500, Sparks = 300 },
            });
            // Deal 110-120 Damage
            r.Add(new RoeRecord(20)
            {
                Trigger = RoeTrigger.DealDamage,
                Goal = 10,
                Reqs = new RoeReqs { DmgMax = 120, DmgMin = 110 },
                Reward = new RoeReward { Exp = 1500, Sparks = 300 },
            });
            // Deal 310-320 Damage
            r.Add(new RoeRecord(21)
            {
                Trigger = RoeTrigger.DealDamage,
                Goal = 10,
                Reqs = new RoeReqs { DmgMax = 320, DmgMin = 310 },
                Reward = new RoeReward { Exp = 1500, Sparks = 300 },
            });
            // Deal 510-520 Damage
            r.Add(new RoeRecord(22)
            {
                Trigger = RoeTrigger.DealDamage,
                Goal = 10,
                Reqs = new RoeReqs { DmgMax = 520, DmgMin = 510 },
                Reward = new RoeReward { Exp = 1500, Sparks = 300 },
            });
            // Deal 1110-1120 Damage
            r.Add(new RoeRecord(23)
            {
                Trigger = RoeTrigger.DealDamage,
                Goal = 10,
                Reqs = new RoeReqs { DmgMax = 1120, DmgMin = 1110 },
                Reward = new RoeReward
                {
                    Exp = 1500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher, 2) },
                    Sparks = 300,
                },
            });
            // Total Damage I
            r.Add(new RoeRecord(29)
            {
                Trigger = RoeTrigger.DealDamage,
                Goal = 100000,
                Increment = 0,
                Notify = 5000,
                Check = RoeCheckForm.ProgressDmg,
                Reward = new RoeReward
                {
                    Exp = 5000,
                    Item = new RoeItem[] { RoeItem.Of(Item.BeitetsuParcel) },
                    Sparks = 1000,
                },
            });
            // Total Damage II
            r.Add(new RoeRecord(30)
            {
                Trigger = RoeTrigger.DealDamage,
                Goal = 200000,
                Increment = 0,
                Notify = 10000,
                Check = RoeCheckForm.ProgressDmg,
                Reward = new RoeReward
                {
                    Exp = 7000,
                    Item = new RoeItem[] { RoeItem.Of(Item.BeitetsuBox) },
                    Sparks = 3000,
                },
            });
            // Total Healing I
            r.Add(new RoeRecord(31)
            {
                Trigger = RoeTrigger.HealAlly,
                Goal = 10000,
                Increment = 0,
                Notify = 500,
                Check = RoeCheckForm.ProgressHeal,
                Reward = new RoeReward
                {
                    Exp = 2500,
                    Item = new RoeItem[] { RoeItem.Of(Item.BoulderCase) },
                    Sparks = 1000,
                },
            });
            // Total Healing II
            r.Add(new RoeRecord(32)
            {
                Trigger = RoeTrigger.HealAlly,
                Goal = 20000,
                Increment = 0,
                Notify = 1000,
                Check = RoeCheckForm.ProgressHeal,
                Reward = new RoeReward
                {
                    Exp = 7000,
                    Item = new RoeItem[] { RoeItem.Of(Item.BoulderBox) },
                    Sparks = 3000,
                },
            });
            // Total Damage Taken I
            r.Add(new RoeRecord(33)
            {
                Trigger = RoeTrigger.TakeDamage,
                Goal = 10000,
                Increment = 0,
                Notify = 500,
                Check = RoeCheckForm.ProgressDmg,
                Reward = new RoeReward
                {
                    Exp = 1000,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher, 2) },
                    Sparks = 1000,
                },
            });
            // Total Damage Taken II
            r.Add(new RoeRecord(34)
            {
                Trigger = RoeTrigger.TakeDamage,
                Goal = 20000,
                Increment = 0,
                Notify = 1000,
                Check = RoeCheckForm.ProgressDmg,
                Reward = new RoeReward
                {
                    Exp = 5000,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher, 4) },
                    Sparks = 3000,
                },
            });
            // Weapon Skills 1
            r.Add(new RoeRecord(45)
            {
                Trigger = RoeTrigger.UseWeaponskill,
                Goal = 100,
                Reward = new RoeReward { Exp = 2500, Sparks = 500 },
            });
            // Total Successful Synthesis Attempts
            r.Add(new RoeRecord(57)
            {
                Trigger = RoeTrigger.SuccessfulSynthesis,
                Goal = 30,
                Flags = RoeFlags.Repeat,
                Reward = new RoeReward { Accolades = 10, Exp = 500, Sparks = 100 },
            });
            // Total Successful Harvesting Attempts
            r.Add(new RoeRecord(61)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 300,
                Reward = new RoeReward { Accolades = 200, Exp = 6000, Sparks = 2000 },
            });
            // Total Suc. Chocobo Digs
            r.Add(new RoeRecord(63)
            {
                Trigger = RoeTrigger.ChocoboDigSuccess,
                Goal = 30,
                Flags = RoeFlags.Repeat,
                Reward = new RoeReward { Accolades = 10, Exp = 500, Sparks = 100 },
            });
            // Mons.: Total Monsters Vanquished
            r.Add(new RoeRecord(65)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 100,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobXp = true, Monipulator = true },
                Reward = new RoeReward { Accolades = 100, Exp = 5000, Sparks = 1000 },
            });
            // Spoils - Fire Crystals
            r.Add(new RoeRecord(71)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.FireCrystal } },
                Reward = new RoeReward { Accolades = 20, Exp = 1000, Sparks = 200 },
            });
            // Spoils - Ice Crystals
            r.Add(new RoeRecord(72)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.IceCrystal } },
                Reward = new RoeReward { Accolades = 20, Exp = 1000, Sparks = 200 },
            });
            // Spoils - Wind Crystals
            r.Add(new RoeRecord(73)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.WindCrystal } },
                Reward = new RoeReward { Accolades = 20, Exp = 1000, Sparks = 200 },
            });
            // Spoils - Earth Crystals
            r.Add(new RoeRecord(74)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.EarthCrystal } },
                Reward = new RoeReward { Accolades = 20, Exp = 1000, Sparks = 200 },
            });
            // Spoils - Lightning Crystals
            r.Add(new RoeRecord(75)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.LightningCrystal } },
                Reward = new RoeReward { Accolades = 20, Exp = 1000, Sparks = 200 },
            });
            // Spoils - Water Crystals
            r.Add(new RoeRecord(76)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.WaterCrystal } },
                Reward = new RoeReward { Accolades = 20, Exp = 1000, Sparks = 200 },
            });
            // Spoils - Light Crystals
            r.Add(new RoeRecord(77)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.LightCrystal } },
                Reward = new RoeReward { Accolades = 20, Exp = 1000, Sparks = 200 },
            });
            // Spoils - Dark Crystals
            r.Add(new RoeRecord(78)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.DarkCrystal } },
                Reward = new RoeReward { Accolades = 20, Exp = 1000, Sparks = 200 },
            });
            // Spoils - Flame Geode
            r.Add(new RoeRecord(84)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 3,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.FlameGeode } },
                Reward = new RoeReward { Accolades = 20, Exp = 1000, Sparks = 200 },
            });
            // Spoils - Snow Geode
            r.Add(new RoeRecord(85)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 3,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.SnowGeode } },
                Reward = new RoeReward { Accolades = 20, Exp = 1000, Sparks = 200 },
            });
            // Spoils - Breeze Geode
            r.Add(new RoeRecord(86)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 3,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.BreezeGeode } },
                Reward = new RoeReward { Accolades = 20, Exp = 1000, Sparks = 200 },
            });
            // Spoils - Soil Geode
            r.Add(new RoeRecord(87)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 3,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.SoilGeode } },
                Reward = new RoeReward { Accolades = 20, Exp = 1000, Sparks = 200 },
            });
            // Spoils - Thunder Geode
            r.Add(new RoeRecord(88)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 3,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.ThunderGeode } },
                Reward = new RoeReward { Accolades = 20, Exp = 1000, Sparks = 200 },
            });
            // Spoils - Aqua Geode
            r.Add(new RoeRecord(89)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 3,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.AquaGeode } },
                Reward = new RoeReward { Accolades = 20, Exp = 1000, Sparks = 200 },
            });
            // Spoils - Light Geode
            r.Add(new RoeRecord(90)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 3,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.LightGeode } },
                Reward = new RoeReward { Accolades = 20, Exp = 1000, Sparks = 200 },
            });
            // Spoils - Shadow Geode
            r.Add(new RoeRecord(91)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 3,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.ShadowGeode } },
                Reward = new RoeReward { Accolades = 20, Exp = 1000, Sparks = 200 },
            });
            // Spoils - Ifritite
            r.Add(new RoeRecord(92)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 3,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.Ifritite } },
                Reward = new RoeReward { Accolades = 20, Exp = 1000, Sparks = 200 },
            });
            // Spoils - Shivite
            r.Add(new RoeRecord(93)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 3,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.Shivite } },
                Reward = new RoeReward { Accolades = 20, Exp = 1000, Sparks = 200 },
            });
            // Spoils - Garudite
            r.Add(new RoeRecord(94)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 3,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.Garudite } },
                Reward = new RoeReward { Accolades = 20, Exp = 1000, Sparks = 200 },
            });
            // Spoils - Titanite
            r.Add(new RoeRecord(95)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 3,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.Titanite } },
                Reward = new RoeReward { Accolades = 20, Exp = 1000, Sparks = 200 },
            });
            // Spoils - Ramuite
            r.Add(new RoeRecord(96)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 3,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.Ramuite } },
                Reward = new RoeReward { Accolades = 20, Exp = 1000, Sparks = 200 },
            });
            // Spoils - Leviatite
            r.Add(new RoeRecord(97)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 3,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.Leviatite } },
                Reward = new RoeReward { Accolades = 20, Exp = 1000, Sparks = 200 },
            });
            // Spoils - Carbite
            r.Add(new RoeRecord(98)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 3,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.Carbite } },
                Reward = new RoeReward { Accolades = 20, Exp = 1000, Sparks = 200 },
            });
            // Spoils - Fenrite
            r.Add(new RoeRecord(99)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 3,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.Fenrite } },
                Reward = new RoeReward { Accolades = 20, Exp = 1000, Sparks = 200 },
            });
            // Speak to Carpenters' Guild Master
            r.Add(new RoeRecord(100)
            {
                Reward = new RoeReward { Exp = 500, Sparks = 100 },
            });
            // Speak to Blacksmiths' Guild Master
            r.Add(new RoeRecord(101)
            {
                Reward = new RoeReward { Exp = 500, Sparks = 100 },
            });
            // Speak to Goldsmiths' Guild Master
            r.Add(new RoeRecord(102)
            {
                Reward = new RoeReward { Exp = 500, Sparks = 100 },
            });
            // Speak to Weavers' Guild Master
            r.Add(new RoeRecord(103)
            {
                Reward = new RoeReward { Exp = 500, Sparks = 100 },
            });
            // Speak to Tanners' Guild Master
            r.Add(new RoeRecord(104)
            {
                Reward = new RoeReward { Exp = 500, Sparks = 100 },
            });
            // Speak to Boneworkers' Guild Master
            r.Add(new RoeRecord(105)
            {
                Reward = new RoeReward { Exp = 500, Sparks = 100 },
            });
            // Speak to Alchemists' Guild Master
            r.Add(new RoeRecord(106)
            {
                Reward = new RoeReward { Exp = 500, Sparks = 100 },
            });
            // Speak to Culinarians' Guild Master
            r.Add(new RoeRecord(107)
            {
                Reward = new RoeReward { Exp = 500, Sparks = 100 },
            });
            // Woodworking: Padded Box
            r.Add(new RoeRecord(108)
            {
                Trigger = RoeTrigger.SuccessfulSynthesis,
                Reqs = new RoeReqs { ItemId = new int[] { Item.PaddedBox } },
                Reward = new RoeReward { Exp = 500, Sparks = 100 },
            });
            // Smithing: Bronze Knife
            r.Add(new RoeRecord(109)
            {
                Trigger = RoeTrigger.SuccessfulSynthesis,
                Reqs = new RoeReqs { ItemId = new int[] { Item.BronzeKnife, Item.BronzeKnifeP1 } },
                Reward = new RoeReward { Exp = 500, Sparks = 100 },
            });
            // Goldsmithing: Copper Ring
            r.Add(new RoeRecord(110)
            {
                Trigger = RoeTrigger.SuccessfulSynthesis,
                Reqs = new RoeReqs { ItemId = new int[] { Item.CopperRing, Item.CopperRingP1 } },
                Reward = new RoeReward { Exp = 500, Sparks = 100 },
            });
            // Weaving: Headgear
            r.Add(new RoeRecord(111)
            {
                Trigger = RoeTrigger.SuccessfulSynthesis,
                Reqs = new RoeReqs { ItemId = new int[] { Item.Headgear, Item.HeadgearP1 } },
                Reward = new RoeReward { Exp = 500, Sparks = 100 },
            });
            // Tanning: Leather Bandana
            r.Add(new RoeRecord(112)
            {
                Trigger = RoeTrigger.SuccessfulSynthesis,
                Reqs = new RoeReqs { ItemId = new int[] { Item.LeatherBandana, Item.LeatherBandanaP1 } },
                Reward = new RoeReward { Exp = 500, Sparks = 100 },
            });
            // Boneworking: Shell Powder
            r.Add(new RoeRecord(113)
            {
                Trigger = RoeTrigger.SuccessfulSynthesis,
                Reqs = new RoeReqs { ItemId = new int[] { Item.PonzeOfShellPowder } },
                Reward = new RoeReward { Exp = 500, Sparks = 100 },
            });
            // Alchemy: Black Ink
            r.Add(new RoeRecord(114)
            {
                Trigger = RoeTrigger.SuccessfulSynthesis,
                Reqs = new RoeReqs { ItemId = new int[] { Item.JarOfBlackInk } },
                Reward = new RoeReward { Exp = 500, Sparks = 100 },
            });
            // Cooking: Pebble Soup
            r.Add(new RoeRecord(115)
            {
                Trigger = RoeTrigger.SuccessfulSynthesis,
                Reqs = new RoeReqs { ItemId = new int[] { Item.BowlOfPebbleSoup, Item.BowlOfWisdomSoup } },
                Reward = new RoeReward { Exp = 500, Sparks = 100 },
            });
            // Level Sync to Vanquish II
            r.Add(new RoeRecord(117)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { LevelSync = true, MobXp = true },
                Reward = new RoeReward { Accolades = 20, Exp = 600, Sparks = 200 },
            });
            // Total Suc. Harvesting Attempts II
            r.Add(new RoeRecord(118)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 30,
                Flags = RoeFlags.Repeat,
                Reward = new RoeReward { Accolades = 20, Exp = 600, Sparks = 200 },
            });
            // Spoils - Bat Wing
            r.Add(new RoeRecord(120)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 2,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.BatWing } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Spoils - Black Tiger Fang
            r.Add(new RoeRecord(121)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 2,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.BlackTigerFang } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Spoils - Flint Stone
            r.Add(new RoeRecord(122)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 2,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.FlintStone } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Spoils - Rabbit Hide
            r.Add(new RoeRecord(123)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 2,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.RabbitHide } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Spoils - Honey
            r.Add(new RoeRecord(124)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 2,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.PotOfHoney } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Spoils - Sheepskin
            r.Add(new RoeRecord(125)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 2,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.Sheepskin } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Spoils - Lizard Skin
            r.Add(new RoeRecord(126)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 2,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.LizardSkin } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Spoils - Beetle Shell
            r.Add(new RoeRecord(127)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 2,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.BeetleShell } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Spoils - Zeruhn Soot
            r.Add(new RoeRecord(128)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 2,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.PinchOfZeruhnSoot } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Spoils - Silver Name Tag
            r.Add(new RoeRecord(129)
            {
                Trigger = RoeTrigger.LootItem,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.SilverNameTag } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Spoils - Quadav Helm
            r.Add(new RoeRecord(130)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 2,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.QuadavHelm } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Spoils - Treant Bulb
            r.Add(new RoeRecord(132)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 2,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.TreantBulb } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Spoils - Wild Onion
            r.Add(new RoeRecord(133)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 2,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.WildOnion } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Spoils - Sleepshroom
            r.Add(new RoeRecord(134)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 2,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.Sleepshroom } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Spoils - Sand Bat Fang
            r.Add(new RoeRecord(135)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 2,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.SandBatFang } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Spoils - Zinc Ore
            r.Add(new RoeRecord(136)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 2,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.ChunkOfZincOre } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Spoils - Giant Bird Feather
            r.Add(new RoeRecord(137)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 2,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.GiantBirdFeather } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Spoils - Three-leaf Mandragora Bud
            r.Add(new RoeRecord(138)
            {
                Trigger = RoeTrigger.LootItem,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.ThreeLeafMandragoraBud } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Spoils - Four-leaf Mandragora Bud
            r.Add(new RoeRecord(139)
            {
                Trigger = RoeTrigger.LootItem,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.FourLeafMandragoraBud } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Spoils - Cornette
            r.Add(new RoeRecord(140)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 2,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.Cornette } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Spoils - Yuhtunga Sulfur
            r.Add(new RoeRecord(141)
            {
                Trigger = RoeTrigger.LootItem,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.PinchOfYuhtungaSulfur } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Spoils - Snobby Letter
            r.Add(new RoeRecord(142)
            {
                Trigger = RoeTrigger.LootItem,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.SnobbyLetter } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Spoils - Yagudo Bead Necklace
            r.Add(new RoeRecord(143)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 2,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.YagudoBeadNecklace } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Spoils - Woozyshroom
            r.Add(new RoeRecord(144)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 2,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.Woozyshroom } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Spoils - Beehive Chip
            r.Add(new RoeRecord(145)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 2,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.BeehiveChip } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Spoils - Remi Shell
            r.Add(new RoeRecord(146)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 2,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.RemiShell } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Spoils - Twinstone Earring
            r.Add(new RoeRecord(147)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 2,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.TwinstoneEarring } },
                Reward = new RoeReward { Accolades = 10, Exp = 300, Sparks = 100 },
            });
            // Harvesting: East Ronfaure
            r.Add(new RoeRecord(180)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.EastRonfaure } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Hatchet, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: Ghelsba Outpost
            r.Add(new RoeRecord(181)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.GhelsbaOutpost } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Hatchet, 12) },
                    Sparks = 100,
                },
            });
        }

        private static void Part1(List<RoeRecord> r)
        {
            // Harvesting: Gusgen Mines
            r.Add(new RoeRecord(182)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.GusgenMines } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Pickaxe, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: Jugner Forest
            r.Add(new RoeRecord(183)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.JugnerForest } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Hatchet, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: Zeruhn Mines
            r.Add(new RoeRecord(184)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.ZeruhnMines } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Pickaxe, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: Palborough Mines
            r.Add(new RoeRecord(185)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.PalboroughMines } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Pickaxe, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: West Sarutabaruta
            r.Add(new RoeRecord(186)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.WestSarutabaruta } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Sickle, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: Giddeus
            r.Add(new RoeRecord(187)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.Giddeus } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Sickle, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: Buburimu Peninsula
            r.Add(new RoeRecord(188)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.BuburimuPeninsula } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Hatchet, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: Tahrongi Canyon
            r.Add(new RoeRecord(189)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.TahrongiCanyon } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Pickaxe, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: Maze of Shakhrami
            r.Add(new RoeRecord(190)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.MazeOfShakhrami } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Pickaxe, 12) },
                    Sparks = 100,
                },
            });
            // Conflict: West Ronfaure
            r.Add(new RoeRecord(215)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.WestRonfaure } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.BowlOfNavarin) },
                    Sparks = 10,
                },
            });
            // Subjugation: Jaggedy-Eared Jack
            r.Add(new RoeRecord(216)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17187111 } },
                Reward = new RoeReward { Exp = 500, Sparks = 250 },
            });
            // Conflict: East Ronfaure
            r.Add(new RoeRecord(217)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.EastRonfaure } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.BrassHarness) },
                    Sparks = 10,
                },
            });
            // Subjugation: Swamfisk
            r.Add(new RoeRecord(218)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17191189, 17191291 } },
                Reward = new RoeReward { Exp = 500, Sparks = 250 },
            });
            // Conflict: Ghelsba Outpost
            r.Add(new RoeRecord(219)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.GhelsbaOutpost } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.SardonyxEarring) },
                    Sparks = 10,
                },
            });
            // Subjugation: Thousandarm Deshglesh
            r.Add(new RoeRecord(220)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17350826 } },
                Reward = new RoeReward { Exp = 550, Sparks = 250 },
            });
            // Conflict: Fort Ghelsba
            r.Add(new RoeRecord(221)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.FortGhelsba } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.AmethystEarring) },
                    Sparks = 10,
                },
            });
            // Subjugation: Hundredscar Hajwaj
            r.Add(new RoeRecord(222)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17354828 } },
                Reward = new RoeReward { Exp = 1000, Sparks = 500 },
            });
            // Conflict: Yughott Grotto
            r.Add(new RoeRecord(223)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.YughottGrotto } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.OnyxEarring) },
                    Sparks = 10,
                },
            });
            // Subjugation: Ashmaker Gotblut
            r.Add(new RoeRecord(224)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17358932 } },
                Reward = new RoeReward { Exp = 500, Sparks = 250 },
            });
            // Conflict: King Ranperre's Tomb
            r.Add(new RoeRecord(225)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.KingRanperresTomb } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.OpalRing) },
                    Sparks = 10,
                },
            });
            // Subjugation: Barbastelle
            r.Add(new RoeRecord(226)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17555721 } },
                Reward = new RoeReward { Exp = 500, Sparks = 250 },
            });
            // Conflict: Bostaunieux Oubliette
            r.Add(new RoeRecord(227)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.BostaunieuxOubliette } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.AccuraCape) },
                    Sparks = 15,
                },
            });
            // Subjugation: Bloodsucker
            r.Add(new RoeRecord(228)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17461478 } },
                Reward = new RoeReward { Exp = 1000, Sparks = 500 },
            });
            // Conflict: Valkurm Dunes
            r.Add(new RoeRecord(229)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.ValkurmDunes } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 550,
                    Item = new RoeItem[] { RoeItem.Of(Item.SilverRing) },
                    Sparks = 11,
                },
            });
            // Subjugation: Valkurm Emperor
            r.Add(new RoeRecord(230)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17199438 } },
                Reward = new RoeReward { Exp = 550, Sparks = 250 },
            });
            // Conflict: Konschtat Highlands
            r.Add(new RoeRecord(231)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.KonschtatHighlands } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 550,
                    Item = new RoeItem[] { RoeItem.Of(Item.LapisLazuliRing) },
                    Sparks = 11,
                },
            });
            // Subjugation: Bendigeit Vran
            r.Add(new RoeRecord(232)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17220001 } },
                Reward = new RoeReward { Exp = 600, Sparks = 250 },
            });
            // Conflict: Gusgen Mines
            r.Add(new RoeRecord(233)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.GusgenMines } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.AmethystRing) },
                    Sparks = 11,
                },
            });
            // Subjugation: Juggler Hecatomb
            r.Add(new RoeRecord(234)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17580248 } },
                Reward = new RoeReward { Exp = 600, Sparks = 250 },
            });
            // Conflict: La Theine Plateau
            r.Add(new RoeRecord(235)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.LaTheinePlateau } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 550,
                    Item = new RoeItem[] { RoeItem.Of(Item.SardonyxRing) },
                    Sparks = 11,
                },
            });
            // Subjugation: Bloodtear Baldurf
            r.Add(new RoeRecord(236)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17195318 } },
                Reward = new RoeReward { Exp = 1000, Sparks = 500 },
            });
            // Conflict: Ordelle's Caves
            r.Add(new RoeRecord(237)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.OrdellesCaves } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.ClearRing) },
                    Sparks = 12,
                },
            });
            // Subjugation: Morbolger
            r.Add(new RoeRecord(238)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17568127 } },
                Reward = new RoeReward { Exp = 1000, Sparks = 500 },
            });
            // Conflict: Jugner Forest
            r.Add(new RoeRecord(239)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.JugnerForest } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 600,
                    Item = new RoeItem[] { RoeItem.Of(Item.MeatMithkabob, 12) },
                    Sparks = 12,
                },
            });
            // Subjugation: King Arthro
            r.Add(new RoeRecord(240)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17203216 } },
                Reward = new RoeReward { Exp = 1000, Sparks = 500 },
            });
            // Conflict: Batallia Downs
            r.Add(new RoeRecord(241)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.BatalliaDowns } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 650,
                    Item = new RoeItem[] { RoeItem.Of(Item.InvisibleMantle) },
                    Sparks = 13,
                },
            });
            // Subjugation: Lumber Jack
            r.Add(new RoeRecord(242)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17207308 } },
                Reward = new RoeReward { Exp = 1000, Sparks = 500 },
            });
            // Conflict: The Eldieme Necropolis
            r.Add(new RoeRecord(243)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.TheEldiemeNecropolis } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.Swordbelt) },
                    Sparks = 14,
                },
            });
            // Subjugation: Cwn Cyrff
            r.Add(new RoeRecord(244)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17576054 } },
                Reward = new RoeReward { Exp = 800, Sparks = 250 },
            });
            // Conflict: Davoi
            r.Add(new RoeRecord(245)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.Davoi } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 650,
                    Item = new RoeItem[] { RoeItem.Of(Item.BandedMail) },
                    Sparks = 13,
                },
            });
            // Subjugation: Hawkeyed Dnatbat
            r.Add(new RoeRecord(246)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17387567 } },
                Reward = new RoeReward { Exp = 600, Sparks = 250 },
            });
            // Conflict: North Gustaberg
            r.Add(new RoeRecord(247)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.NorthGustaberg } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.JackOLantern) },
                    Sparks = 10,
                },
            });
            // Subjugation: Maighdean Uaine
            r.Add(new RoeRecord(248)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17211702, 17211714 } },
                Reward = new RoeReward { Exp = 500, Sparks = 250 },
            });
            // Conflict: South Gustaberg
            r.Add(new RoeRecord(249)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.SouthGustaberg } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Doublet) },
                    Sparks = 10,
                },
            });
            // Subjugation: Carnero
            r.Add(new RoeRecord(250)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17215613, 17215626 } },
                Reward = new RoeReward { Exp = 500, Sparks = 250 },
            });
            // Conflict: Zeruhn Mines
            r.Add(new RoeRecord(251)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.ZeruhnMines } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.AmberEarring) },
                    Sparks = 10,
                },
            });
            // Conflict: Palborough Mines
            r.Add(new RoeRecord(252)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.PalboroughMines } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.TourmalineEarring) },
                    Sparks = 10,
                },
            });
            // Subjugation: Zi-Ghi Bone-eater
            r.Add(new RoeRecord(253)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17363208 } },
                Reward = new RoeReward { Exp = 500, Sparks = 250 },
            });
            // Conflict: Dangruf Wadi
            r.Add(new RoeRecord(254)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.DangrufWadi } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.AmberRing) },
                    Sparks = 10,
                },
            });
            // Subjugation: Teporingo
            r.Add(new RoeRecord(255)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17559584 } },
                Reward = new RoeReward { Exp = 500, Sparks = 250 },
            });
            // Conflict: Pashhow Marshlands
            r.Add(new RoeRecord(256)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.PashhowMarshlands } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 600,
                    Item = new RoeItem[] { RoeItem.Of(Item.PlateOfCrabSushi, 12) },
                    Sparks = 12,
                },
            });
            // Subjugation: Ni'Zho Bladebender
            r.Add(new RoeRecord(257)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17223797 } },
                Reward = new RoeReward { Exp = 700, Sparks = 250 },
            });
            // Conflict: Rolanberry Fields
            r.Add(new RoeRecord(258)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.RolanberryFields } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 600,
                    Item = new RoeItem[] { RoeItem.Of(Item.HighBreathMantle) },
                    Sparks = 12,
                },
            });
            // Subjugation: Simurgh
            r.Add(new RoeRecord(259)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17228242 } },
                Reward = new RoeReward { Exp = 1000, Sparks = 250 },
            });
            // Conflict: Crawler's Nest
            r.Add(new RoeRecord(260)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.CrawlersNest } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.Corsette) },
                    Sparks = 14,
                },
            });
            // Subjugation: Demonic Tiphia
            r.Add(new RoeRecord(261)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17584398 } },
                Reward = new RoeReward { Exp = 800, Sparks = 250 },
            });
            // Conflict: Beadeaux
            r.Add(new RoeRecord(262)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.Beadeaux } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 650,
                    Item = new RoeItem[] { RoeItem.Of(Item.BrigandineArmor) },
                    Sparks = 13,
                },
            });
            // Subjugation: Zo'Khu Blackcloud
            r.Add(new RoeRecord(263)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17379564 } },
                Reward = new RoeReward { Exp = 700, Sparks = 250 },
            });
            // Conflict: West Sarutabaruta
            r.Add(new RoeRecord(264)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.WestSarutabaruta } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CupOfChocomilk) },
                    Sparks = 10,
                },
            });
            // Subjugation: Nunyenunc
            r.Add(new RoeRecord(265)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17248517 } },
                Reward = new RoeReward { Exp = 500, Sparks = 250 },
            });
            // Conflict: East Sarutabaruta
            r.Add(new RoeRecord(266)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.EastSarutabaruta } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.LinenRobe) },
                    Sparks = 10,
                },
            });
            // Subjugation: Spiny Spipi
            r.Add(new RoeRecord(267)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17252657 } },
                Reward = new RoeReward { Exp = 500, Sparks = 250 },
            });
            // Conflict: Giddeus
            r.Add(new RoeRecord(268)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.Giddeus } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.OpalEarring) },
                    Sparks = 10,
                },
            });
            // Subjugation: Hoo Mjuu the Torrent
            r.Add(new RoeRecord(269)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17371515 } },
                Reward = new RoeReward { Exp = 500, Sparks = 250 },
            });
            // Conflict: Toraimarai Canal
            r.Add(new RoeRecord(270)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.ToraimaraiCanal } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.RedCape) },
                    Sparks = 15,
                },
            });
            // Subjugation: Oni Carcass
            r.Add(new RoeRecord(271)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17469587 } },
                Reward = new RoeReward { Exp = 800, Sparks = 250 },
            });
            // Conflict: Inner Horutoto Ruins
            r.Add(new RoeRecord(272)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.InnerHorutotoRuins } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.ClearEarring) },
                    Sparks = 10,
                },
            });
            // Subjugation: Maltha
            r.Add(new RoeRecord(273)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17563749 } },
                Reward = new RoeReward { Exp = 500, Sparks = 250 },
            });
            // Conflict: Outer Horutoto Ruins
            r.Add(new RoeRecord(274)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.OuterHorutotoRuins } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.LapisLazuliEarring) },
                    Sparks = 10,
                },
            });
            // Subjugation: Bomb King
            r.Add(new RoeRecord(275)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17572094, 17572142, 17572146 } },
                Reward = new RoeReward { Exp = 500, Sparks = 250 },
            });
            // Conflict: Buburimu Peninsula
            r.Add(new RoeRecord(276)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.BuburimuPeninsula } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 600,
                    Item = new RoeItem[] { RoeItem.Of(Item.OnyxRing) },
                    Sparks = 12,
                },
            });
            // Subjugation: Helldiver
            r.Add(new RoeRecord(277)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17260907 } },
                Reward = new RoeReward { Exp = 600, Sparks = 250 },
            });
            // Conflict: Tahrongi Canyon
            r.Add(new RoeRecord(278)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.TahrongiCanyon } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 550,
                    Item = new RoeItem[] { RoeItem.Of(Item.TourmalineRing) },
                    Sparks = 11,
                },
            });
            // Subjugation: Serpopard Ishtar
            r.Add(new RoeRecord(279)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17256563, 17256690 } },
                Reward = new RoeReward { Exp = 600, Sparks = 250 },
            });
            // Conflict: Maze of Shakhrami
            r.Add(new RoeRecord(280)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.MazeOfShakhrami } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.BoneEarring) },
                    Sparks = 12,
                },
            });
            // Subjugation: Argus
            r.Add(new RoeRecord(281)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17588674 } },
                Reward = new RoeReward { Exp = 1000, Sparks = 500 },
            });
            // Conflict: Meriphataud Mountains
            r.Add(new RoeRecord(282)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.MeriphataudMountains } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 600,
                    Item = new RoeItem[] { RoeItem.Of(Item.ApplePie, 12) },
                    Sparks = 12,
                },
            });
            // Subjugation: Daggerclaw Dracos
            r.Add(new RoeRecord(283)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17264818 } },
                Reward = new RoeReward { Exp = 600, Sparks = 250 },
            });
            // Conflict: Sauromugue Champaign
            r.Add(new RoeRecord(284)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.SauromugueChampaign } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 650,
                    Item = new RoeItem[] { RoeItem.Of(Item.BlackCape) },
                    Sparks = 13,
                },
            });
            // Subjugation: Roc
            r.Add(new RoeRecord(285)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17269106 } },
                Reward = new RoeReward { Exp = 1000, Sparks = 500 },
            });
            // Conflict: Garlaige Citadel
            r.Add(new RoeRecord(286)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.GarlaigeCitadel } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.QiqirnSash) },
                    Sparks = 14,
                },
            });
            // Subjugation: Serket
            r.Add(new RoeRecord(287)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17596720 } },
                Reward = new RoeReward { Exp = 1000, Sparks = 500 },
            });
            // Conflict: Castle Oztroja
            r.Add(new RoeRecord(288)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.CastleOztroja } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 650,
                    Item = new RoeItem[] { RoeItem.Of(Item.PyroRobe) },
                    Sparks = 13,
                },
            });
            // Subjugation: Lii Jixa the Somnolist
            r.Add(new RoeRecord(289)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17395896 } },
                Reward = new RoeReward { Exp = 800, Sparks = 250 },
            });
            // Conflict: Beaucedine Glacier
            r.Add(new RoeRecord(290)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.BeaucedineGlacier } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 700,
                    Item = new RoeItem[] { RoeItem.Of(Item.MohbwaScarf) },
                    Sparks = 14,
                },
            });
            // Subjugation: Nue
            r.Add(new RoeRecord(291)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17231971 } },
                Reward = new RoeReward { Exp = 700, Sparks = 250 },
            });
            // Conflict: Ranguemont Pass
            r.Add(new RoeRecord(292)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.RanguemontPass } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.BeetleEarring) },
                    Sparks = 11,
                },
            });
            // Subjugation: Gloom Eye
            r.Add(new RoeRecord(293)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17457204 } },
                Reward = new RoeReward { Exp = 700, Sparks = 250 },
            });
            // Conflict: Fei'Yin
            r.Add(new RoeRecord(294)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.Feiyin } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.TortoiseEarring) },
                    Sparks = 14,
                },
            });
            // Subjugation: Goliath
            r.Add(new RoeRecord(295)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17613046, 17613048, 17613052, 17613054 } },
                Reward = new RoeReward { Exp = 800, Sparks = 250 },
            });
            // Conflict: Xarcabard
            r.Add(new RoeRecord(296)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.Xarcabard } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 700,
                    Item = new RoeItem[] { RoeItem.Of(Item.GoldEarring) },
                    Sparks = 14,
                },
            });
            // Subjugation: Biast
            r.Add(new RoeRecord(297)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17235988 } },
                Reward = new RoeReward { Exp = 1000, Sparks = 500 },
            });
            // Conflict: Castle Zvahl Baileys
            r.Add(new RoeRecord(298)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.CastleZvahlBaileys } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 750,
                    Item = new RoeItem[] { RoeItem.Of(Item.HiPotionTank) },
                    Sparks = 15,
                },
            });
            // Subjugation: Duke Haborym
            r.Add(new RoeRecord(299)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17436923 } },
                Reward = new RoeReward { Exp = 1000, Sparks = 500 },
            });
            // Conflict: Castle Zvahl Keep
            r.Add(new RoeRecord(300)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.CastleZvahlKeep } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 750,
                    Item = new RoeItem[] { RoeItem.Of(Item.HiEtherTank) },
                    Sparks = 15,
                },
            });
            // Subjugation: Baron Vapula
            r.Add(new RoeRecord(301)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17440963 } },
                Reward = new RoeReward { Exp = 800, Sparks = 250 },
            });
            // Conflict: Qufim Island
            r.Add(new RoeRecord(302)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.QufimIsland } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 650,
                    Item = new RoeItem[] { RoeItem.Of(Item.FocusCollar) },
                    Sparks = 13,
                },
            });
            // Subjugation: Dosetsu Tree
            r.Add(new RoeRecord(303)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17293640 } },
                Reward = new RoeReward { Exp = 1000, Sparks = 500 },
            });
            // Conflict: Lower Delkfutt's Tower
            r.Add(new RoeRecord(304)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.LowerDelkfuttsTower } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.ConeOfSnollGelato, 12) },
                    Sparks = 13,
                },
            });
            // Subjugation: Epialtes
            r.Add(new RoeRecord(305)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17530881 } },
                Reward = new RoeReward { Exp = 700, Sparks = 250 },
            });
        }

        private static void Part2(List<RoeRecord> r)
        {
            // Conflict: Middle Delkfutt's Tower
            r.Add(new RoeRecord(306)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.MiddleDelkfuttsTower } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.PlateOfSoleSushi, 12) },
                    Sparks = 13,
                },
            });
            // Subjugation: Ogygos
            r.Add(new RoeRecord(307)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17420592 } },
                Reward = new RoeReward { Exp = 700, Sparks = 250 },
            });
            // Conflict: Upper Delkfutt's Tower
            r.Add(new RoeRecord(308)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.UpperDelkfuttsTower } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.YellowCurryBun, 12) },
                    Sparks = 14,
                },
            });
            // Subjugation: Enkelados
            r.Add(new RoeRecord(309)
            {
                Trigger = RoeTrigger.DefeatMob,
                Reqs = new RoeReqs { MobId = new int[] { 17424385, 17424423 } },
                Reward = new RoeReward { Exp = 800, Sparks = 250 },
            });
            // Conflict: Rala Waterways I
            r.Add(new RoeRecord(310)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Spoutdrenched_Toad" },
                    Zone = new int[] { Zone.RalaWaterways },
                },
                Reward = new RoeReward { Accolades = 10, Capacity = 100, Exp = 500, Sparks = 100 },
            });
            // Conflict: Rala Waterways II
            r.Add(new RoeRecord(311)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Stillwater_Funguar" },
                    Zone = new int[] { Zone.RalaWaterways },
                },
                Reward = new RoeReward { Accolades = 10, Capacity = 100, Exp = 500, Sparks = 100 },
            });
            // Conflict: Rala Waterways III
            r.Add(new RoeRecord(312)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Pewter_Diremite" },
                    Zone = new int[] { Zone.RalaWaterways },
                },
                Reward = new RoeReward { Accolades = 10, Capacity = 100, Exp = 500, Sparks = 100 },
            });
            // Conflict: Ceizak Battlegrounds I
            r.Add(new RoeRecord(313)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Blanched_Mandragora" },
                    Zone = new int[] { Zone.CeizakBattlegrounds },
                },
                Reward = new RoeReward { Accolades = 10, Capacity = 100, Exp = 500, Sparks = 100 },
            });
            // Conflict: Ceizak Battlegrounds II
            r.Add(new RoeRecord(314)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Careening_Twitherym" },
                    Zone = new int[] { Zone.CeizakBattlegrounds },
                },
                Reward = new RoeReward { Accolades = 10, Capacity = 100, Exp = 500, Sparks = 100 },
            });
            // Conflict: Ceizak Battlegrounds III
            r.Add(new RoeRecord(315)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Resplendent_Luckybug" },
                    Zone = new int[] { Zone.CeizakBattlegrounds },
                },
                Reward = new RoeReward { Accolades = 10, Capacity = 100, Exp = 500, Sparks = 100 },
            });
            // Conflict: Yahse Hunting Grounds I
            r.Add(new RoeRecord(316)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Calfcleaving_Chapuli" },
                    Zone = new int[] { Zone.YahseHuntingGrounds },
                },
                Reward = new RoeReward { Accolades = 10, Capacity = 100, Exp = 500, Sparks = 100 },
            });
            // Conflict: Yahse Hunting Grounds II
            r.Add(new RoeRecord(317)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Twitherym_Infestation" },
                    Zone = new int[] { Zone.YahseHuntingGrounds },
                },
                Reward = new RoeReward { Accolades = 10, Capacity = 100, Exp = 500, Sparks = 100 },
            });
            // Conflict: Yahse Hunting Grounds III
            r.Add(new RoeRecord(318)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Nettled_Wasp" },
                    Zone = new int[] { Zone.YahseHuntingGrounds },
                },
                Reward = new RoeReward { Accolades = 10, Capacity = 100, Exp = 500, Sparks = 100 },
            });
            // Conflict: Foret de Hennitiel I
            r.Add(new RoeRecord(319)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Perfidious_Crab" },
                    Zone = new int[] { Zone.ForetDeHennetiel },
                },
                Reward = new RoeReward { Accolades = 11, Capacity = 100, Exp = 500, Sparks = 110 },
            });
            // Conflict: Foret de Hennitiel II
            r.Add(new RoeRecord(320)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Scummy_Slug" },
                    Zone = new int[] { Zone.ForetDeHennetiel },
                },
                Reward = new RoeReward { Accolades = 11, Capacity = 100, Exp = 500, Sparks = 110 },
            });
            // Conflict: Foret de Hennitiel III
            r.Add(new RoeRecord(321)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Vorst_Gnat" },
                    Zone = new int[] { Zone.ForetDeHennetiel },
                },
                Reward = new RoeReward { Accolades = 11, Capacity = 100, Exp = 500, Sparks = 110 },
            });
            // Conflict: Morimar Basalt Fields I
            r.Add(new RoeRecord(322)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Sinewy_Matamata" },
                    Zone = new int[] { Zone.MorimarBasaltFields },
                },
                Reward = new RoeReward { Accolades = 11, Capacity = 100, Exp = 500, Sparks = 110 },
            });
            // Conflict: Morimar Basalt Fields II
            r.Add(new RoeRecord(323)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Tephra_Lizard" },
                    Zone = new int[] { Zone.MorimarBasaltFields },
                },
                Reward = new RoeReward { Accolades = 11, Capacity = 100, Exp = 500, Sparks = 110 },
            });
            // Conflict: Morimar Basalt Fields III
            r.Add(new RoeRecord(324)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Maca_Maca" },
                    Zone = new int[] { Zone.MorimarBasaltFields },
                },
                Reward = new RoeReward { Accolades = 11, Capacity = 100, Exp = 500, Sparks = 110 },
            });
            // Conflict: Yorcia Weald I
            r.Add(new RoeRecord(325)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobName = new string[] { "Snapweed" }, Zone = new int[] { Zone.YorciaWeald } },
                Reward = new RoeReward { Accolades = 12, Capacity = 100, Exp = 500, Sparks = 120 },
            });
            // Conflict: Yorcia Weald II
            r.Add(new RoeRecord(326)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Cheeky_Opo-opo" },
                    Zone = new int[] { Zone.YorciaWeald },
                },
                Reward = new RoeReward { Accolades = 12, Capacity = 100, Exp = 500, Sparks = 120 },
            });
            // Conflict: Yorcia Weald III
            r.Add(new RoeRecord(327)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobName = new string[] { "Bronzecap" }, Zone = new int[] { Zone.YorciaWeald } },
                Reward = new RoeReward { Accolades = 12, Capacity = 100, Exp = 500, Sparks = 120 },
            });
            // Conflict: Marjami Ravine I
            r.Add(new RoeRecord(328)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Vinelash_Vulture" },
                    Zone = new int[] { Zone.MarjamiRavine },
                },
                Reward = new RoeReward { Accolades = 12, Capacity = 100, Exp = 500, Sparks = 120 },
            });
            // Conflict: Marjami Ravine II
            r.Add(new RoeRecord(329)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobName = new string[] { "Stryx" }, Zone = new int[] { Zone.MarjamiRavine } },
                Reward = new RoeReward { Accolades = 12, Capacity = 100, Exp = 500, Sparks = 120 },
            });
            // Conflict: Marjami Ravine III
            r.Add(new RoeRecord(330)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobName = new string[] { "Lapinion" }, Zone = new int[] { Zone.MarjamiRavine } },
                Reward = new RoeReward { Accolades = 12, Capacity = 100, Exp = 500, Sparks = 120 },
            });
            // Conflict: Kamihr Drifts I
            r.Add(new RoeRecord(331)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Snowpelt_Rabbit" },
                    Zone = new int[] { Zone.KamihrDrifts },
                },
                Reward = new RoeReward { Accolades = 13, Capacity = 100, Exp = 500, Sparks = 130 },
            });
            // Conflict: Kamihr Drifts II
            r.Add(new RoeRecord(332)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Cicatricose_Raaz" },
                    Zone = new int[] { Zone.KamihrDrifts },
                },
                Reward = new RoeReward { Accolades = 13, Capacity = 100, Exp = 500, Sparks = 130 },
            });
            // Conflict: Kamihr Drifts III
            r.Add(new RoeRecord(333)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobName = new string[] { "Shaggy_Ovim" }, Zone = new int[] { Zone.KamihrDrifts } },
                Reward = new RoeReward { Accolades = 13, Capacity = 100, Exp = 500, Sparks = 130 },
            });
            // Conflict: Sih Gates I
            r.Add(new RoeRecord(334)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobName = new string[] { "Bonaria" }, Zone = new int[] { Zone.SihGates } },
                Reward = new RoeReward { Accolades = 16, Capacity = 100, Exp = 500, Sparks = 160 },
            });
            // Conflict: Sih Gates II
            r.Add(new RoeRecord(335)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Loathsome_Obdella" },
                    Zone = new int[] { Zone.SihGates },
                },
                Reward = new RoeReward { Accolades = 16, Capacity = 100, Exp = 500, Sparks = 160 },
            });
            // Conflict: Sih Gates III
            r.Add(new RoeRecord(336)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Ferocious_Funguar" },
                    Zone = new int[] { Zone.SihGates },
                },
                Reward = new RoeReward { Accolades = 16, Capacity = 100, Exp = 500, Sparks = 160 },
            });
            // Conflict: Moh Gates I
            r.Add(new RoeRecord(337)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Conflagrant_Eruca" },
                    Zone = new int[] { Zone.MohGates },
                },
                Reward = new RoeReward { Accolades = 16, Capacity = 100, Exp = 500, Sparks = 160 },
            });
            // Conflict: Moh Gates II
            r.Add(new RoeRecord(338)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobName = new string[] { "Erythemic_Eft" }, Zone = new int[] { Zone.MohGates } },
                Reward = new RoeReward { Accolades = 16, Capacity = 100, Exp = 500, Sparks = 160 },
            });
            // Conflict: Moh Gates III
            r.Add(new RoeRecord(339)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Ferocious_Funguar" },
                    Zone = new int[] { Zone.MohGates },
                },
                Reward = new RoeReward { Accolades = 16, Capacity = 100, Exp = 500, Sparks = 160 },
            });
            // Conflict: Cirdas Caverns I
            r.Add(new RoeRecord(340)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Subterrane_Spider" },
                    Zone = new int[] { Zone.CirdasCaverns },
                },
                Reward = new RoeReward { Accolades = 16, Capacity = 100, Exp = 500, Sparks = 160 },
            });
            // Conflict: Cirdas Caverns II
            r.Add(new RoeRecord(341)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Livid_Umbril" },
                    Zone = new int[] { Zone.CirdasCaverns },
                },
                Reward = new RoeReward { Accolades = 16, Capacity = 100, Exp = 500, Sparks = 160 },
            });
            // Conflict: Cirdas Caverns III
            r.Add(new RoeRecord(342)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Flatus_Acuex" },
                    Zone = new int[] { Zone.CirdasCaverns },
                },
                Reward = new RoeReward { Accolades = 16, Capacity = 100, Exp = 500, Sparks = 160 },
            });
            // Conflict: Dho Gates I
            r.Add(new RoeRecord(343)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobName = new string[] { "Velkk_Magus" }, Zone = new int[] { Zone.DhoGates } },
                Reward = new RoeReward { Accolades = 16, Capacity = 100, Exp = 500, Sparks = 160 },
            });
            // Conflict: Dho Gates II
            r.Add(new RoeRecord(344)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobName = new string[] { "Ripsaw_Jagil" }, Zone = new int[] { Zone.DhoGates } },
                Reward = new RoeReward { Accolades = 16, Capacity = 100, Exp = 500, Sparks = 160 },
            });
            // Conflict: Dho Gates III
            r.Add(new RoeRecord(345)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Unyielding_Tarichuk" },
                    Zone = new int[] { Zone.DhoGates },
                },
                Reward = new RoeReward { Accolades = 16, Capacity = 100, Exp = 500, Sparks = 160 },
            });
            // Conflict: Woh Gates I
            r.Add(new RoeRecord(346)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Metalcruncher_Worm" },
                    Zone = new int[] { Zone.WohGates },
                },
                Reward = new RoeReward { Accolades = 16, Capacity = 100, Exp = 500, Sparks = 160 },
            });
            // Conflict: Woh Gates II
            r.Add(new RoeRecord(347)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobName = new string[] { "Drusy_Twitherym" }, Zone = new int[] { Zone.WohGates } },
                Reward = new RoeReward { Accolades = 16, Capacity = 100, Exp = 500, Sparks = 160 },
            });
            // Conflict: Woh Gates III
            r.Add(new RoeRecord(348)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobName = new string[] { "Wheezing_Acuex" }, Zone = new int[] { Zone.WohGates } },
                Reward = new RoeReward { Accolades = 16, Capacity = 100, Exp = 500, Sparks = 160 },
            });
            // Conflict: Behemoth's Dominion
            r.Add(new RoeRecord(380)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.BehemothsDominion } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.FishMithkabob, 12) },
                    Sparks = 14,
                },
            });
            // Harvesting: Yughott Grotto
            r.Add(new RoeRecord(389)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.YughottGrotto } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Pickaxe, 12) },
                    Sparks = 100,
                },
            });
            // Conflict: Sanctuary of Zi'Tah
            r.Add(new RoeRecord(390)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.TheSanctuaryOfZitah } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 600,
                    Item = new RoeItem[] { RoeItem.Of(Item.FlaskOfEchoDrops, 12) },
                    Sparks = 12,
                },
            });
            // Conflict: Ro'Maeve
            r.Add(new RoeRecord(392)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.Romaeve } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 800,
                    Item = new RoeItem[] { RoeItem.Of(Item.BottleOfMulsum, 12) },
                    Sparks = 16,
                },
            });
            // Conflict: Boyahda Tree
            r.Add(new RoeRecord(394)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.TheBoyahdaTree } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.FlaskOfDeodorizer, 12) },
                    Sparks = 16,
                },
            });
            // Conflict: Dragon's Aery
            r.Add(new RoeRecord(396)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.DragonsAery } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 850,
                    Item = new RoeItem[] { RoeItem.Of(Item.SuperEther) },
                    Sparks = 17,
                },
            });
            // Conflict: Eastern Altepa Desert
            r.Add(new RoeRecord(398)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.EasternAltepaDesert } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 650,
                    Item = new RoeItem[] { RoeItem.Of(Item.PinchOfPrismPowder, 12) },
                    Sparks = 13,
                },
            });
            // Conflict: Western Altepa Desert
            r.Add(new RoeRecord(400)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.WesternAltepaDesert } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 700,
                    Item = new RoeItem[] { RoeItem.Of(Item.PotOfSilentOil, 12) },
                    Sparks = 14,
                },
            });
            // Conflict: Quicksand Caves
            r.Add(new RoeRecord(402)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.QuicksandCaves } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.GaiaMantle) },
                    Sparks = 15,
                },
            });
            // Conflict: Gustav Tunnel
            r.Add(new RoeRecord(404)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.GustavTunnel } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.JestersCape) },
                    Sparks = 16,
                },
            });
            // Conflict: Kuftal Tunnel
            r.Add(new RoeRecord(406)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.KuftalTunnel } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.PeisteMantle) },
                    Sparks = 14,
                },
            });
            // Conflict: Cape Terrigan
            r.Add(new RoeRecord(408)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.CapeTeriggan } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 800,
                    Item = new RoeItem[] { RoeItem.Of(Item.BeakNecklace) },
                    Sparks = 16,
                },
            });
            // Conflict: Valley of Sorrows
            r.Add(new RoeRecord(410)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.ValleyOfSorrows } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 800,
                    Item = new RoeItem[] { RoeItem.Of(Item.CoralGorget) },
                    Sparks = 16,
                },
            });
            // Conflict: Yuhtunga Jungle
            r.Add(new RoeRecord(412)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.YuhtungaJungle } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 650,
                    Item = new RoeItem[] { RoeItem.Of(Item.Torque) },
                    Sparks = 13,
                },
            });
            // Conflict: Sea Serpent Grotto
            r.Add(new RoeRecord(414)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.SeaSerpentGrotto } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.BrocadeObi) },
                    Sparks = 13,
                },
            });
            // Conflict: Yhoator Jungle
            r.Add(new RoeRecord(416)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.YhoatorJungle } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 650,
                    Item = new RoeItem[] { RoeItem.Of(Item.TwinthreadObi) },
                    Sparks = 13,
                },
            });
            // Conflict: Temple of Uggalepih
            r.Add(new RoeRecord(418)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.TempleOfUggalepih } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.PeisteBelt) },
                    Sparks = 15,
                },
            });
            // Conflict: Den of Rancor
            r.Add(new RoeRecord(420)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.DenOfRancor } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.RainbowObi) },
                    Sparks = 16,
                },
            });
            // Conflict: Ifrit's Cauldron
            r.Add(new RoeRecord(422)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.IfritsCauldron } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.SunEarring) },
                    Sparks = 16,
                },
            });
            // Conflict: Ru'Aun Gardens
            r.Add(new RoeRecord(424)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.RuaunGardens } },
                Reward = new RoeReward
                {
                    Accolades = 7,
                    Exp = 850,
                    Item = new RoeItem[] { RoeItem.Of(Item.PurpleEarring) },
                    Sparks = 70,
                },
            });
            // Conflict: Ve'Lugannon Palace
            r.Add(new RoeRecord(426)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.VelugannonPalace } },
                Reward = new RoeReward
                {
                    Accolades = 7,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.YellowEarring) },
                    Sparks = 70,
                },
            });
            // Conflict: Shrine of Ru'Avitau
            r.Add(new RoeRecord(428)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.TheShrineOfRuavitau } },
                Reward = new RoeReward
                {
                    Accolades = 7,
                    Exp = 100,
                    Item = new RoeItem[] { RoeItem.Of(Item.GreenEarring) },
                    Sparks = 70,
                },
            });
            // Conflict: Labyrinth of Onzozo
            r.Add(new RoeRecord(430)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.LabyrinthOfOnzozo } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 700,
                    Item = new RoeItem[] { RoeItem.Of(Item.ZirconEarring) },
                    Sparks = 14,
                },
            });
            // Conflict: Korroloka Tunnel
            r.Add(new RoeRecord(432)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.KorrolokaTunnel } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 600,
                    Item = new RoeItem[] { RoeItem.Of(Item.AquamarineEarring) },
                    Sparks = 12,
                },
            });
            // Conflict: Oldton Movalpolos
            r.Add(new RoeRecord(434)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.OldtonMovalpolos } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 650,
                    Item = new RoeItem[] { RoeItem.Of(Item.MoonEarring) },
                    Sparks = 13,
                },
            });
            // Conflict: Newton Movalpolos
            r.Add(new RoeRecord(436)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.NewtonMovalpolos } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 800,
                    Item = new RoeItem[] { RoeItem.Of(Item.NightEarring) },
                    Sparks = 16,
                },
            });
            // Conflict: Lufaise Meadows
            r.Add(new RoeRecord(438)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.LufaiseMeadows } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 650,
                    Item = new RoeItem[] { RoeItem.Of(Item.MelodyEarring) },
                    Sparks = 13,
                },
            });
            // Conflict: Misareaux Coast
            r.Add(new RoeRecord(440)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.MisareauxCoast } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 650,
                    Item = new RoeItem[] { RoeItem.Of(Item.ErisEarring) },
                    Sparks = 13,
                },
            });
            // Conflict: Phomiuna Aqueducts
            r.Add(new RoeRecord(442)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.PhomiunaAqueducts } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 650,
                    Item = new RoeItem[] { RoeItem.Of(Item.FangEarring) },
                    Sparks = 13,
                },
            });
            // Conflict: Riverne - Site #A01
            r.Add(new RoeRecord(444)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.RiverneSiteA01 } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 650,
                    Item = new RoeItem[] { RoeItem.Of(Item.CarapaceRing) },
                    Sparks = 13,
                },
            });
            // Conflict: Riverne - Site #B01
            r.Add(new RoeRecord(446)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.RiverneSiteB01 } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 700,
                    Item = new RoeItem[] { RoeItem.Of(Item.TrumpetRing) },
                    Sparks = 14,
                },
            });
            // Conflict: Sacrarium
            r.Add(new RoeRecord(448)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.Sacrarium } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 700,
                    Item = new RoeItem[] { RoeItem.Of(Item.SunRing) },
                    Sparks = 14,
                },
            });
            // Conflict: Promyvion - Holla
            r.Add(new RoeRecord(450)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.PromyvionHolla } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 600,
                    Item = new RoeItem[] { RoeItem.Of(Item.FluoriteRing) },
                    Sparks = 12,
                },
            });
            // Conflict: Promyvion - Dem
            r.Add(new RoeRecord(452)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.PromyvionDem } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 600,
                    Item = new RoeItem[] { RoeItem.Of(Item.ChrysoberylRing) },
                    Sparks = 12,
                },
            });
            // Conflict: Promyvion - Mea
            r.Add(new RoeRecord(454)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.PromyvionMea } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 600,
                    Item = new RoeItem[] { RoeItem.Of(Item.JadeiteRing) },
                    Sparks = 12,
                },
            });
            // Conflict: Promyvion - Vahzl
            r.Add(new RoeRecord(456)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.PromyvionVahzl } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 700,
                    Item = new RoeItem[] { RoeItem.Of(Item.ZirconRing) },
                    Sparks = 14,
                },
            });
            // Conflict: Al'Taieu
            r.Add(new RoeRecord(458)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.Altaieu } },
                Reward = new RoeReward
                {
                    Accolades = 7,
                    Exp = 850,
                    Item = new RoeItem[] { RoeItem.Of(Item.AquamarineRing) },
                    Sparks = 70,
                },
            });
            // Conflict: Grand Palace of Hu'Xzoi
            r.Add(new RoeRecord(460)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.GrandPalaceOfHuxzoi } },
                Reward = new RoeReward
                {
                    Accolades = 7,
                    Exp = 850,
                    Item = new RoeItem[] { RoeItem.Of(Item.MoonRing) },
                    Sparks = 70,
                },
            });
            // Conflict: Garden of Ru'Hmet
            r.Add(new RoeRecord(462)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.TheGardenOfRuhmet } },
                Reward = new RoeReward
                {
                    Accolades = 8,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.MoonringBlade) },
                    Sparks = 80,
                },
            });
            // Conflict: Carpenters' Landing
            r.Add(new RoeRecord(464)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.CarpentersLanding } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 550,
                    Item = new RoeItem[] { RoeItem.Of(Item.PainiteRing) },
                    Sparks = 11,
                },
            });
            // Conflict: Bibiki Bay
            r.Add(new RoeRecord(468)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.BibikiBay } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 650,
                    Item = new RoeItem[] { RoeItem.Of(Item.HardLeatherRing) },
                    Sparks = 13,
                },
            });
            // Conflict: Attohwa Chasm
            r.Add(new RoeRecord(472)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.AttohwaChasm } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 650,
                    Item = new RoeItem[] { RoeItem.Of(Item.DemonsRing) },
                    Sparks = 13,
                },
            });
            // Conflict: Pso'Xja
            r.Add(new RoeRecord(474)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.Psoxja } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 700,
                    Item = new RoeItem[] { RoeItem.Of(Item.GoldRing) },
                    Sparks = 14,
                },
            });
            // Conflict: Uleguerand Range
            r.Add(new RoeRecord(476)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.UleguerandRange } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 800,
                    Item = new RoeItem[] { RoeItem.Of(Item.BehemothMantle) },
                    Sparks = 16,
                },
            });
            // Harvesting: Yuhtunga Jungle
            r.Add(new RoeRecord(478)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.YuhtungaJungle } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Hatchet, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: Yhoator Jungle
            r.Add(new RoeRecord(479)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.YhoatorJungle } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Hatchet, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: Ifrit's Cauldron
            r.Add(new RoeRecord(480)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.IfritsCauldron } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Pickaxe, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: Oldton Movalpolos
            r.Add(new RoeRecord(481)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.OldtonMovalpolos } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Pickaxe, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: Newton Movalpolos
            r.Add(new RoeRecord(482)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.NewtonMovalpolos } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Pickaxe, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: Lufaise Meadows
            r.Add(new RoeRecord(483)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.LufaiseMeadows } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Hatchet, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: Misareaux Coast
            r.Add(new RoeRecord(484)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.MisareauxCoast } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Hatchet, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: Korroloka Tunnel
            r.Add(new RoeRecord(485)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.KorrolokaTunnel } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Pickaxe, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: Carpenters' Landing
            r.Add(new RoeRecord(486)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.CarpentersLanding } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Hatchet, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: Attohwa Chasm
            r.Add(new RoeRecord(487)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.AttohwaChasm } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Pickaxe, 12) },
                    Sparks = 100,
                },
            });
            // Heal for 500+ HP
            r.Add(new RoeRecord(488)
            {
                Trigger = RoeTrigger.HealAlly,
                Goal = 100,
                Reqs = new RoeReqs { HealMin = 500 },
                Reward = new RoeReward { Exp = 6000, Sparks = 2000 },
            });
            // Conflict: Outer Ra'Kaznar I
            r.Add(new RoeRecord(494)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Restless_Twitherym" },
                    Zone = new int[] { Zone.OuterRakaznar },
                },
                Reward = new RoeReward { Accolades = 17, Capacity = 100, Exp = 500, Sparks = 170 },
            });
            // Conflict: Outer Ra'Kaznar II
            r.Add(new RoeRecord(495)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Bristlehair_Bat" },
                    Zone = new int[] { Zone.OuterRakaznar },
                },
                Reward = new RoeReward { Accolades = 17, Capacity = 100, Exp = 500, Sparks = 170 },
            });
        }

        private static void Part3(List<RoeRecord> r)
        {
            // Conflict: Outer Ra'Kaznar III
            r.Add(new RoeRecord(496)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Astringent_Acuex" },
                    Zone = new int[] { Zone.OuterRakaznar },
                },
                Reward = new RoeReward { Accolades = 17, Capacity = 100, Exp = 500, Sparks = 170 },
            });
            // Stepping into an Ambuscade
            r.Add(new RoeRecord(499)
            {
                Reward = new RoeReward { Exp = 300, KeyItem = KeyItem.AmbuscadePrimerVolumeTwo, Sparks = 100 },
            });
            // Mog House Exit: San d'Oria
            r.Add(new RoeRecord(500)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Sandoria, QuestId.Sandoria.GrowingFlowers) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Mog House Exit: Bastok
            r.Add(new RoeRecord(501)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Bastok, QuestId.Bastok.ALadysHeart) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Mog House Exit: Windurst
            r.Add(new RoeRecord(502)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Windurst, QuestId.Windurst.FlowerChild) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Mog House Exit: Jeuno
            r.Add(new RoeRecord(503)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.PrettyLittleThings) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Mog House Exit: Aht Urhgan
            r.Add(new RoeRecord(504)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.AhtUrghan, QuestId.AhtUrhgan.KeepingNotes) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Obtain a Support Job
            r.Add(new RoeRecord(505)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Check = RoeCheckForm.QuestEither,
                EitherQuest = ((int)QuestLog.OtherAreas, QuestId.OtherAreas.ElderMemories,
                    QuestId.OtherAreas.TheOldLady),
                Reward = new RoeReward { Exp = 500, Sparks = 100 },
            });
            // Obtain an Alter Ego: San d'Oria
            r.Add(new RoeRecord(506)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Sandoria, QuestId.Sandoria.TrustSandoria) },
                Reward = new RoeReward { Exp = 500, Sparks = 100 },
            });
            // Obtain an Alter Ego: Bastok
            r.Add(new RoeRecord(507)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Bastok, QuestId.Bastok.TrustBastok) },
                Reward = new RoeReward { Exp = 500, Sparks = 100 },
            });
            // Obtain an Alter Ego: Windurst
            r.Add(new RoeRecord(508)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Windurst, QuestId.Windurst.TrustWindurst) },
                Reward = new RoeReward { Exp = 500, Sparks = 100 },
            });
            // Obtain a Chocobo License
            r.Add(new RoeRecord(509)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.ChocobosWounds) },
                Reward = new RoeReward { Exp = 600, Sparks = 100 },
            });
            // Obtain Job: Paladin
            r.Add(new RoeRecord(510)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Sandoria, QuestId.Sandoria.AKnightsTest) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Obtain Job: Dark Knight
            r.Add(new RoeRecord(511)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Bastok, QuestId.Bastok.BladeOfDarkness) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Obtain Job: Beastmaster
            r.Add(new RoeRecord(512)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.SaveMySon) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Obtain Job: Bard
            r.Add(new RoeRecord(513)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.TheOldMonument) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Obtain Job: Ranger
            r.Add(new RoeRecord(514)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Windurst, QuestId.Windurst.TheFangedOne) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Obtain Job: Samurai
            r.Add(new RoeRecord(515)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Outlands, QuestId.Outlands.ForgeYourDestiny) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Obtain Job: Ninja
            r.Add(new RoeRecord(516)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Bastok, QuestId.Bastok.AyameAndKaede) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Obtain Job: Dragoon
            r.Add(new RoeRecord(517)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Sandoria, QuestId.Sandoria.TheHolyCrest) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Obtain Job: Summoner
            r.Add(new RoeRecord(518)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Windurst, QuestId.Windurst.ICanHearARainbow) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Obtain Job: Blue Mage
            r.Add(new RoeRecord(519)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.AhtUrghan, QuestId.AhtUrhgan.AnEmptyVessel) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Obtain Job: Corsair
            r.Add(new RoeRecord(520)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.AhtUrghan, QuestId.AhtUrhgan.LuckOfTheDraw) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Obtain Job: Puppetmaster
            r.Add(new RoeRecord(521)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.AhtUrghan, QuestId.AhtUrhgan.NoStringsAttached) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Obtain Job: Dancer
            r.Add(new RoeRecord(522)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.LakesideMinuet) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Obtain Job: Scholar
            r.Add(new RoeRecord(523)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.CrystalWar, QuestId.CrystalWar.ALittleKnowledge) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Obtain Job: Geomancer
            r.Add(new RoeRecord(524)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Adoulin, QuestId.Adoulin.DancesWithLuopans) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Obtain Job: Runefencer
            r.Add(new RoeRecord(525)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Adoulin, QuestId.Adoulin.ChildrenOfTheRune) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Conflict: Bhaflau Thickets
            r.Add(new RoeRecord(533)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.BhaflauThickets } },
                Reward = new RoeReward
                {
                    Accolades = 6,
                    Exp = 800,
                    Item = new RoeItem[] { RoeItem.Of(Item.TowerShield) },
                    Sparks = 60,
                },
            });
            // Conflict: Mamook
            r.Add(new RoeRecord(535)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.Mamook } },
                Reward = new RoeReward
                {
                    Accolades = 7,
                    Exp = 850,
                    Item = new RoeItem[] { RoeItem.Of(Item.RitterShield) },
                    Sparks = 70,
                },
            });
            // Conflict: Wajaom Woodlands
            r.Add(new RoeRecord(537)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.WajaomWoodlands } },
                Reward = new RoeReward
                {
                    Accolades = 6,
                    Exp = 800,
                    Item = new RoeItem[] { RoeItem.Of(Item.ArachneObi) },
                    Sparks = 60,
                },
            });
            // Conflict: Aydeewa Subterrane
            r.Add(new RoeRecord(539)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.AydeewaSubterrane } },
                Reward = new RoeReward
                {
                    Accolades = 6,
                    Exp = 800,
                    Item = new RoeItem[] { RoeItem.Of(Item.KoenigsBelt) },
                    Sparks = 60,
                },
            });
            // Conflict: Halvung
            r.Add(new RoeRecord(541)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.Halvung } },
                Reward = new RoeReward
                {
                    Accolades = 7,
                    Exp = 850,
                    Item = new RoeItem[] { RoeItem.Of(Item.MaridBelt) },
                    Sparks = 70,
                },
            });
            // Conflict: Mount Zhayolm
            r.Add(new RoeRecord(543)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.MountZhayolm } },
                Reward = new RoeReward
                {
                    Accolades = 7,
                    Exp = 850,
                    Item = new RoeItem[] { RoeItem.Of(Item.PeaceCape) },
                    Sparks = 70,
                },
            });
            // Conflict: Caedarva Mire
            r.Add(new RoeRecord(545)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.CaedarvaMire } },
                Reward = new RoeReward
                {
                    Accolades = 6,
                    Exp = 800,
                    Item = new RoeItem[] { RoeItem.Of(Item.TarutaruSash) },
                    Sparks = 60,
                },
            });
            // Conflict: Arrapago Reef
            r.Add(new RoeRecord(547)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.ArrapagoReef } },
                Reward = new RoeReward
                {
                    Accolades = 7,
                    Exp = 850,
                    Item = new RoeItem[] { RoeItem.Of(Item.LynxMantle) },
                    Sparks = 70,
                },
            });
            // Conflict: Alzadaal Undersea Ruins
            r.Add(new RoeRecord(549)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.AlzadaalUnderseaRuins } },
                Reward = new RoeReward
                {
                    Accolades = 7,
                    Exp = 850,
                    Item = new RoeItem[] { RoeItem.Of(Item.RainbowCape) },
                    Sparks = 70,
                },
            });
            // Conflict: East Ronfaure [S]
            r.Add(new RoeRecord(553)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.EastRonfaureS } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 550,
                    Item = new RoeItem[] { RoeItem.Of(Item.CoeurlGorget) },
                    Sparks = 11,
                },
            });
            // Conflict: Jugner Forest [S]
            r.Add(new RoeRecord(555)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.JugnerForestS } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 700,
                    Item = new RoeItem[] { RoeItem.Of(Item.Hoplon) },
                    Sparks = 14,
                },
            });
            // Conflict: Batallia Downs [S]
            r.Add(new RoeRecord(557)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.BatalliaDownsS } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 600,
                    Item = new RoeItem[] { RoeItem.Of(Item.JeweledCollar) },
                    Sparks = 12,
                },
            });
            // Conflict: La Vaule [S]
            r.Add(new RoeRecord(559)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.LaVauleS } },
                Reward = new RoeReward
                {
                    Accolades = 7,
                    Exp = 850,
                    Item = new RoeItem[] { RoeItem.Of(Item.OrichalcumEarring) },
                    Sparks = 70,
                },
            });
            // Conflict: The Eldieme Necropolis [S]
            r.Add(new RoeRecord(561)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.TheEldiemeNecropolisS } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 800,
                    Item = new RoeItem[] { RoeItem.Of(Item.SmilodonMantle) },
                    Sparks = 16,
                },
            });
            // Conflict: North Gustaberg [S]
            r.Add(new RoeRecord(563)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.NorthGustabergS } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 550,
                    Item = new RoeItem[] { RoeItem.Of(Item.DarksteelNodowa) },
                    Sparks = 11,
                },
            });
            // Conflict: Grauberg [S]
            r.Add(new RoeRecord(565)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.GraubergS } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 650,
                    Item = new RoeItem[] { RoeItem.Of(Item.PlatinumEarring) },
                    Sparks = 13,
                },
            });
            // Conflict: Vunkerl Inlet [S]
            r.Add(new RoeRecord(567)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.VunkerlInletS } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 650,
                    Item = new RoeItem[] { RoeItem.Of(Item.PhantomEarring) },
                    Sparks = 13,
                },
            });
            // Conflict: Pashhow Marshlands [S]
            r.Add(new RoeRecord(569)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.PashhowMarshlandsS } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 800,
                    Item = new RoeItem[] { RoeItem.Of(Item.CoralEarring) },
                    Sparks = 16,
                },
            });
            // Conflict: Rolanberry Fields [S]
            r.Add(new RoeRecord(571)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.RolanberryFieldsS } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 650,
                    Item = new RoeItem[] { RoeItem.Of(Item.DarksteelShield) },
                    Sparks = 13,
                },
            });
            // Conflict: Beadeaux [S]
            r.Add(new RoeRecord(573)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.BeadeauxS } },
                Reward = new RoeReward
                {
                    Accolades = 7,
                    Exp = 850,
                    Item = new RoeItem[] { RoeItem.Of(Item.StarEarring) },
                    Sparks = 70,
                },
            });
            // Conflict: Crawlers' Nest [S]
            r.Add(new RoeRecord(575)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.CrawlersNestS } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 800,
                    Item = new RoeItem[] { RoeItem.Of(Item.CrimsonEarring) },
                    Sparks = 16,
                },
            });
            // Conflict: West Sarutabaruta [S]
            r.Add(new RoeRecord(577)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.WestSarutabarutaS } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 550,
                    Item = new RoeItem[] { RoeItem.Of(Item.DarksteelGorget) },
                    Sparks = 11,
                },
            });
            // Conflict: Fort Karugo-Narugo [S]
            r.Add(new RoeRecord(579)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.FortKarugoNarugoS } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 600,
                    Item = new RoeItem[] { RoeItem.Of(Item.WivreGorget) },
                    Sparks = 12,
                },
            });
            // Conflict: Meriphataud Mountains [S]
            r.Add(new RoeRecord(581)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.MeriphataudMountainsS } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 650,
                    Item = new RoeItem[] { RoeItem.Of(Item.DarksteelBuckler) },
                    Sparks = 13,
                },
            });
            // Conflict: Sauromugue Champaign [S]
            r.Add(new RoeRecord(583)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.SauromugueChampaignS } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 650,
                    Item = new RoeItem[] { RoeItem.Of(Item.WivreShield) },
                    Sparks = 13,
                },
            });
            // Conflict: Castle Oztroja [S]
            r.Add(new RoeRecord(585)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.CastleOztrojaS } },
                Reward = new RoeReward
                {
                    Accolades = 7,
                    Exp = 850,
                    Item = new RoeItem[] { RoeItem.Of(Item.CrimsonRing) },
                    Sparks = 70,
                },
            });
            // Conflict: Garlaige Citadel [S]
            r.Add(new RoeRecord(587)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.GarlaigeCitadelS } },
                Reward = new RoeReward
                {
                    Accolades = 5,
                    Exp = 800,
                    Item = new RoeItem[] { RoeItem.Of(Item.OrichalcumRing) },
                    Sparks = 16,
                },
            });
            // Conflict: Beaucedine Glacier [S]
            r.Add(new RoeRecord(589)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.BeaucedineGlacierS } },
                Reward = new RoeReward
                {
                    Accolades = 7,
                    Exp = 850,
                    Item = new RoeItem[] { RoeItem.Of(Item.StarRing) },
                    Sparks = 70,
                },
            });
            // Conflict: Xarcabard [S]
            r.Add(new RoeRecord(591)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.XarcabardS } },
                Reward = new RoeReward
                {
                    Accolades = 8,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.AcheronShield) },
                    Sparks = 80,
                },
            });
            // Conflict: Castle Zvahl Baileys [S]
            r.Add(new RoeRecord(593)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.CastleZvahlBaileysS } },
                Reward = new RoeReward
                {
                    Accolades = 8,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.OrochiNodowa) },
                    Sparks = 80,
                },
            });
            // Conflict: Castle Zvahl Keep [S]
            r.Add(new RoeRecord(595)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.CastleZvahlKeepS } },
                Reward = new RoeReward
                {
                    Accolades = 8,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.CerberusRing) },
                    Sparks = 80,
                },
            });
            // Harvesting: Bhaflau Thickets
            r.Add(new RoeRecord(599)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.BhaflauThickets } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Sickle, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: Mamook
            r.Add(new RoeRecord(600)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.Mamook } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Hatchet, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: Wajaom Woodlands
            r.Add(new RoeRecord(601)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.WajaomWoodlands } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Sickle, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: Halvung
            r.Add(new RoeRecord(602)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.Halvung } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Pickaxe, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: Mount Zhayolm
            r.Add(new RoeRecord(603)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.MountZhayolm } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Pickaxe, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: Caedarva Mire
            r.Add(new RoeRecord(604)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.CaedarvaMire } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Hatchet, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: East Ronfaure [S]
            r.Add(new RoeRecord(606)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.EastRonfaureS } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Hatchet, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: Jugner Forest [S]
            r.Add(new RoeRecord(607)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.JugnerForestS } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Hatchet, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: North Gustaberg [S]
            r.Add(new RoeRecord(608)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.NorthGustabergS } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Pickaxe, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: Grauberg [S]
            r.Add(new RoeRecord(609)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.GraubergS } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Sickle, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: West Sarutabaruta [S]
            r.Add(new RoeRecord(610)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.WestSarutabarutaS } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Sickle, 12) },
                    Sparks = 100,
                },
            });
            // Harvesting: Fort Karugo-Narugo [S]
            r.Add(new RoeRecord(611)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.FortKarugoNarugoS } },
                Reward = new RoeReward
                {
                    Accolades = 10,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.Hatchet, 12) },
                    Sparks = 100,
                },
            });
            // Conflict: Abyssea - La Theine
            r.Add(new RoeRecord(613)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 30,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.AbysseaLaTheine } },
                Reward = new RoeReward
                {
                    Accolades = 8,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.MuzzlingCollar) },
                    Sparks = 80,
                },
            });
            // Conflict: Abyssea - Konschtat
            r.Add(new RoeRecord(614)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 30,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.AbysseaKonschtat } },
                Reward = new RoeReward
                {
                    Accolades = 8,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.AlZahbiSash) },
                    Sparks = 80,
                },
            });
            // Conflict: Abyssea - Tahrongi
            r.Add(new RoeRecord(615)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 30,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.AbysseaTahrongi } },
                Reward = new RoeReward
                {
                    Accolades = 8,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.SanctuaryObi) },
                    Sparks = 80,
                },
            });
            // Conflict: Abyssea - Attohwa
            r.Add(new RoeRecord(616)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 30,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.AbysseaAttohwa } },
                Reward = new RoeReward
                {
                    Accolades = 8,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.BeirBelt) },
                    Sparks = 80,
                },
            });
            // Conflict: Abyssea - Misareaux
            r.Add(new RoeRecord(617)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 30,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.AbysseaMisareaux } },
                Reward = new RoeReward
                {
                    Accolades = 8,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.AisanceMantle) },
                    Sparks = 80,
                },
            });
            // Conflict: Abyssea - Vunkerl
            r.Add(new RoeRecord(618)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 30,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.AbysseaVunkerl } },
                Reward = new RoeReward
                {
                    Accolades = 8,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.EloquenceCape) },
                    Sparks = 80,
                },
            });
            // Conflict: Abyssea - Altepa
            r.Add(new RoeRecord(619)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 30,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.AbysseaAltepa } },
                Reward = new RoeReward
                {
                    Accolades = 8,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.VigilanceMantle) },
                    Sparks = 80,
                },
            });
            // Conflict: Abyssea - Uleguerand
            r.Add(new RoeRecord(620)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 30,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.AbysseaUleguerand } },
                Reward = new RoeReward
                {
                    Accolades = 8,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.AreweRing) },
                    Sparks = 80,
                },
            });
            // Conflict: Abyssea - Grauberg
            r.Add(new RoeRecord(621)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 30,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { Zone = new int[] { Zone.AbysseaGrauberg } },
                Reward = new RoeReward
                {
                    Accolades = 8,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.AreweRing) },
                    Sparks = 80,
                },
            });
            // WAR Artifact Quest I
            r.Add(new RoeRecord(629)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Bastok, QuestId.Bastok.TheDoorman) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // WAR Artifact Quest II
            r.Add(new RoeRecord(630)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Bastok, QuestId.Bastok.TheTalekeepersTruth) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // WAR Artifact Quest III
            r.Add(new RoeRecord(631)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Bastok, QuestId.Bastok.TheTalekeepersGift) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // MNK Artifact Quest I
            r.Add(new RoeRecord(632)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Bastok, QuestId.Bastok.GhostsOfThePast) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // MNK Artifact Quest II
            r.Add(new RoeRecord(633)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Bastok, QuestId.Bastok.TheFirstMeeting) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // MNK Artifact Quest III
            r.Add(new RoeRecord(634)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Bastok, QuestId.Bastok.TrueStrength) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // WHM Artifact Quest I
            r.Add(new RoeRecord(635)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Sandoria, QuestId.Sandoria.MessengerFromBeyond) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // WHM Artifact Quest II
            r.Add(new RoeRecord(636)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs
                {
                    QuestComplete = ((int)QuestLog.Sandoria, QuestId.Sandoria.PreludeOfBlackAndWhite),
                },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // WHM Artifact Quest III
            r.Add(new RoeRecord(637)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Sandoria, QuestId.Sandoria.PieujesDecision) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // BLM Artifact Quest I
            r.Add(new RoeRecord(638)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Windurst, QuestId.Windurst.TheThreeMagi) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // BLM Artifact Quest II
            r.Add(new RoeRecord(639)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Windurst, QuestId.Windurst.Recollections) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // BLM Artifact Quest III
            r.Add(new RoeRecord(640)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Windurst, QuestId.Windurst.TheRootOfTheProblem) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // RDM Artifact Quest I
            r.Add(new RoeRecord(641)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Sandoria, QuestId.Sandoria.TheCrimsonTrial) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // RDM Artifact Quest II
            r.Add(new RoeRecord(642)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Sandoria, QuestId.Sandoria.EnvelopedInDarkness) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // RDM Artifact Quest III
            r.Add(new RoeRecord(643)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Sandoria, QuestId.Sandoria.PeaceForTheSpirit) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // THF Artifact Quest I
            r.Add(new RoeRecord(644)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Windurst, QuestId.Windurst.TheTenshodoShowdown) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // THF Artifact Quest II
            r.Add(new RoeRecord(645)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Windurst, QuestId.Windurst.AsThickAsThieves) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // THF Artifact Quest III
            r.Add(new RoeRecord(646)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Windurst, QuestId.Windurst.HittingTheMarquisate) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // PLD Artifact Quest I
            r.Add(new RoeRecord(647)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Sandoria, QuestId.Sandoria.SharpeningTheSword) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // PLD Artifact Quest II
            r.Add(new RoeRecord(648)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Sandoria, QuestId.Sandoria.ABoysDream) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
        }

        private static void Part4(List<RoeRecord> r)
        {
            // PLD Artifact Quest III
            r.Add(new RoeRecord(649)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Sandoria, QuestId.Sandoria.UnderOath) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // DRK Artifact Quest I
            r.Add(new RoeRecord(650)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Bastok, QuestId.Bastok.DarkLegacy) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // DRK Artifact Quest II
            r.Add(new RoeRecord(651)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Bastok, QuestId.Bastok.DarkPuppet) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // DRK Artifact Quest III
            r.Add(new RoeRecord(652)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Bastok, QuestId.Bastok.BladeOfEvil) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // BST Artifact Quest I
            r.Add(new RoeRecord(653)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.WingsOfGold) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // BST Artifact Quest II
            r.Add(new RoeRecord(654)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.ScatteredIntoShadow) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // BST Artifact Quest III
            r.Add(new RoeRecord(655)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.ANewDawn) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // BRD Artifact Quest I
            r.Add(new RoeRecord(656)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.PainfulMemory) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // BRD Artifact Quest II
            r.Add(new RoeRecord(657)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.TheRequiem) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // BRD Artifact Quest III
            r.Add(new RoeRecord(658)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.TheCircleOfTime) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // RNG Artifact Quest I
            r.Add(new RoeRecord(659)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Windurst, QuestId.Windurst.SinHunting) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // RNG Artifact Quest II
            r.Add(new RoeRecord(660)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Windurst, QuestId.Windurst.FireAndBrimstone) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // RNG Artifact Quest III
            r.Add(new RoeRecord(661)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Windurst, QuestId.Windurst.UnbridledPassion) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // SAM Artifact Quest I
            r.Add(new RoeRecord(662)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Outlands, QuestId.Outlands.TheSacredKatana) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // SAM Artifact Quest II
            r.Add(new RoeRecord(663)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Outlands, QuestId.Outlands.YomiOkuri) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // SAM Artifact Quest III
            r.Add(new RoeRecord(664)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Outlands, QuestId.Outlands.AThiefInNorg) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // NIN Artifact Quest I
            r.Add(new RoeRecord(665)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Outlands, QuestId.Outlands.TwentyInPirateYears) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // NIN Artifact Quest II
            r.Add(new RoeRecord(666)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Outlands, QuestId.Outlands.ILlTakeTheBigBox) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // NIN Artifact Quest III
            r.Add(new RoeRecord(667)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Outlands, QuestId.Outlands.TrueWill) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // DRG Artifact Quest I
            r.Add(new RoeRecord(668)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Sandoria, QuestId.Sandoria.ACraftsmansWork) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // DRG Artifact Quest II
            r.Add(new RoeRecord(669)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Sandoria, QuestId.Sandoria.ChasingQuotas) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // DRG Artifact Quest III
            r.Add(new RoeRecord(670)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Sandoria, QuestId.Sandoria.KnightStalker) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // SMN Artifact Quest I
            r.Add(new RoeRecord(671)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Windurst, QuestId.Windurst.ThePuppetMaster) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // SMN Artifact Quest II
            r.Add(new RoeRecord(672)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Windurst, QuestId.Windurst.ClassReunion) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // SMN Artifact Quest III
            r.Add(new RoeRecord(673)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Windurst, QuestId.Windurst.CarbuncleDebacle) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // BLU Artifact Quest I
            r.Add(new RoeRecord(674)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.AhtUrghan, QuestId.AhtUrhgan.Beginnings) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // BLU Artifact Quest II
            r.Add(new RoeRecord(675)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.AhtUrghan, QuestId.AhtUrhgan.Omens) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // BLU Artifact Quest III
            r.Add(new RoeRecord(676)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.AhtUrghan, QuestId.AhtUrhgan.Transformations) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // COR Artifact Quest I
            r.Add(new RoeRecord(677)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs
                {
                    QuestComplete = ((int)QuestLog.AhtUrghan, QuestId.AhtUrhgan.EquippedForAllOccasions),
                },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // COR Artifact Quest II
            r.Add(new RoeRecord(678)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs
                {
                    QuestComplete = ((int)QuestLog.AhtUrghan, QuestId.AhtUrhgan.NavigatingTheUnfriendlySeas),
                },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // COR Artifact Quest III
            r.Add(new RoeRecord(679)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.AhtUrghan, QuestId.AhtUrhgan.AgainstAllOdds) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // PUP Artifact Quest I
            r.Add(new RoeRecord(680)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.AhtUrghan, QuestId.AhtUrhgan.TheWaywardAutomaton) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // PUP Artifact Quest II
            r.Add(new RoeRecord(681)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.AhtUrghan, QuestId.AhtUrhgan.OperationTeatime) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // PUP Artifact Quest III
            r.Add(new RoeRecord(682)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.AhtUrghan, QuestId.AhtUrhgan.PuppetmasterBlues) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // DNC Artifact Quest I
            r.Add(new RoeRecord(683)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.TheUnfinishedWaltz) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // DNC Artifact Quest II
            r.Add(new RoeRecord(684)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.TheRoadToDivadom) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // DNC Artifact Quest III
            r.Add(new RoeRecord(685)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.ComebackQueen) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // SCH Artifact Quest I
            r.Add(new RoeRecord(686)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.CrystalWar, QuestId.CrystalWar.OnSabbatical) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // SCH Artifact Quest II
            r.Add(new RoeRecord(687)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.CrystalWar, QuestId.CrystalWar.DownwardHelix) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // SCH Artifact Quest III
            r.Add(new RoeRecord(688)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.CrystalWar, QuestId.CrystalWar.SeeingBloodRed) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // GEO Artifact Quest I
            r.Add(new RoeRecord(689)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Adoulin, QuestId.Adoulin.ForWhomTheBellTolls) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // GEO Artifact Quest II
            r.Add(new RoeRecord(690)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Adoulin, QuestId.Adoulin.TheBloodlineOfZacariah) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // GEO Artifact Quest III
            r.Add(new RoeRecord(691)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Adoulin, QuestId.Adoulin.TheCommunion) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // RUN Artifact Quest I
            r.Add(new RoeRecord(692)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Adoulin, QuestId.Adoulin.ForgingNewBonds) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // RUN Artifact Quest II
            r.Add(new RoeRecord(693)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Adoulin, QuestId.Adoulin.LegaciesLostAndFound) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // RUN Artifact Quest III
            r.Add(new RoeRecord(694)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Adoulin, QuestId.Adoulin.DestinysDevice) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Total Damage III
            r.Add(new RoeRecord(696)
            {
                Trigger = RoeTrigger.DealDamage,
                Goal = 300000,
                Increment = 0,
                Notify = 10000,
                Check = RoeCheckForm.ProgressDmg,
                Reward = new RoeReward
                {
                    Exp = 7000,
                    Item = new RoeItem[] { RoeItem.Of(Item.BeitetsuBox) },
                    Sparks = 3000,
                },
            });
            // Total Damage Taken III
            r.Add(new RoeRecord(697)
            {
                Trigger = RoeTrigger.TakeDamage,
                Goal = 30000,
                Increment = 0,
                Notify = 1000,
                Check = RoeCheckForm.ProgressDmg,
                Reward = new RoeReward
                {
                    Exp = 5000,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher, 6) },
                    Sparks = 3000,
                },
            });
            // Deal 2000+ Damage
            r.Add(new RoeRecord(698)
            {
                Trigger = RoeTrigger.DealDamage,
                Goal = 100,
                Reqs = new RoeReqs { DmgMin = 2000 },
                Reward = new RoeReward
                {
                    Exp = 5000,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher, 6) },
                    Sparks = 2000,
                },
            });
            // Total Healing III
            r.Add(new RoeRecord(699)
            {
                Trigger = RoeTrigger.HealAlly,
                Goal = 30000,
                Increment = 0,
                Notify = 1000,
                Check = RoeCheckForm.ProgressHeal,
                Reward = new RoeReward
                {
                    Exp = 7000,
                    Item = new RoeItem[] { RoeItem.Of(Item.BoulderBox) },
                    Sparks = 3000,
                },
            });
            // Heal for 750+ HP
            r.Add(new RoeRecord(700)
            {
                Trigger = RoeTrigger.HealAlly,
                Goal = 100,
                Reqs = new RoeReqs { HealMin = 750 },
                Reward = new RoeReward
                {
                    Exp = 7000,
                    Item = new RoeItem[] { RoeItem.Of(Item.PlutonBox) },
                    Sparks = 3000,
                },
            });
            // Level Cap Increase: 55 +
            r.Add(new RoeRecord(705)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.InDefiantChallenge) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Level Cap Increase: 60
            r.Add(new RoeRecord(706)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.AtopTheHighestMountains) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Level Cap Increase: 65
            r.Add(new RoeRecord(707)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.WhenceBlowsTheWind) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Level Cap Increase: 70
            r.Add(new RoeRecord(708)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.RidingOnTheClouds) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Level Cap Increase: 75
            r.Add(new RoeRecord(709)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.ShatteringStars) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Level Cap Increase: 80
            r.Add(new RoeRecord(710)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.NewWorldsAwait) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Level Cap Increase: 85
            r.Add(new RoeRecord(711)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.ExpandingHorizons) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Level Cap Increase: 90
            r.Add(new RoeRecord(712)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.BeyondTheStars) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Level Cap Increase: 95
            r.Add(new RoeRecord(713)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.DormantPowersDislodged) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Level Cap Increase: 99
            r.Add(new RoeRecord(714)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.BeyondInfinity) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Inventory Expansion 35
            r.Add(new RoeRecord(715)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.TheGobbiebagPartI) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Inventory Expansion 40
            r.Add(new RoeRecord(716)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.TheGobbiebagPartIi) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Inventory Expansion 45
            r.Add(new RoeRecord(717)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.TheGobbiebagPartIii) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Inventory Expansion 50
            r.Add(new RoeRecord(718)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.TheGobbiebagPartIv) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Inventory Expansion 55
            r.Add(new RoeRecord(719)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.TheGobbiebagPartV) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Inventory Expansion 60
            r.Add(new RoeRecord(720)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.TheGobbiebagPartVi) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Inventory Expansion 65
            r.Add(new RoeRecord(721)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.TheGobbiebagPartVii) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Inventory Expansion 70
            r.Add(new RoeRecord(722)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.TheGobbiebagPartViii) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Inventory Expansion 75
            r.Add(new RoeRecord(723)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.TheGobbiebagPartIx) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Inventory Expansion 80
            r.Add(new RoeRecord(724)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.TheGobbiebagPartX) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Subjugation: Kirin
            r.Add(new RoeRecord(727)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17506670 } },
                Reward = new RoeReward
                {
                    Accolades = 100,
                    Exp = 5000,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher) },
                    Sparks = 1000,
                },
            });
            // Subjugation: Genbu
            r.Add(new RoeRecord(728)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17309980 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Suzaku
            r.Add(new RoeRecord(729)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17309983 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Seiryu
            r.Add(new RoeRecord(730)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17309981 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Byakko
            r.Add(new RoeRecord(731)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17309982 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Overlord's Tombstone
            r.Add(new RoeRecord(732)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17534977 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Bladeburner Rokgevok
            r.Add(new RoeRecord(733)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17535207 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Steelshank Kratzvatz
            r.Add(new RoeRecord(734)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17535208 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Bloodfist Voshgrosh
            r.Add(new RoeRecord(735)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17535210 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Spellspear Djokvukk
            r.Add(new RoeRecord(736)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17535211 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Gu'Dha Effigy
            r.Add(new RoeRecord(737)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17539073 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Zo'Pha Forgesoul
            r.Add(new RoeRecord(738)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17539307 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Ra'Gho Darkfount
            r.Add(new RoeRecord(739)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17539308 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Va'Zhe Pummelsong
            r.Add(new RoeRecord(740)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17539310 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Bu'Bho Truesteel
            r.Add(new RoeRecord(741)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17539311 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Tzee Xicu Idol
            r.Add(new RoeRecord(742)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17543169 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Xuu Bhoqa the Enigma
            r.Add(new RoeRecord(743)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17543464 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Fuu Tzapo the Blessed
            r.Add(new RoeRecord(744)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17543466 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Naa Yixo the Stillrage
            r.Add(new RoeRecord(745)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17543467 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Tee Zaska the Ceaseless
            r.Add(new RoeRecord(746)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17543468 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Goblin Golem
            r.Add(new RoeRecord(747)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17547265 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Quicktrix Hexhands
            r.Add(new RoeRecord(748)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17547493 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Feralox Honeylips
            r.Add(new RoeRecord(749)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17547494 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Scourquix Scaleskin
            r.Add(new RoeRecord(750)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17547496 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Wilywox Tenderpalm
            r.Add(new RoeRecord(751)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17547498 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Angra Mainyu
            r.Add(new RoeRecord(752)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17326081 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Taquede
            r.Add(new RoeRecord(753)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17326093 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Pignonpausard
            r.Add(new RoeRecord(754)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17326095 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Hitaume
            r.Add(new RoeRecord(755)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17326096 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
        }

        private static void Part5(List<RoeRecord> r)
        {
            // Subjugation: Cavanneche
            r.Add(new RoeRecord(756)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17326097 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Dynamis Lord
            r.Add(new RoeRecord(757)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17330177, 17330178, 17330179, 17330180, 17330181, 17330182 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Duke Haures
            r.Add(new RoeRecord(758)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17330185 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Marquis Caim
            r.Add(new RoeRecord(759)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17330186 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Baron Avnas
            r.Add(new RoeRecord(760)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17330188 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Count Haagenti
            r.Add(new RoeRecord(761)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17330189 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Conflict: Ra'Kaznar Inner Court I
            r.Add(new RoeRecord(762)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Bilespouting_Acuex" },
                    Zone = new int[] { Zone.RakaznarInnerCourt },
                },
                Reward = new RoeReward { Accolades = 18, Capacity = 100, Exp = 600, Sparks = 180 },
            });
            // Conflict: Ra'Kaznar Inner Court II
            r.Add(new RoeRecord(763)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Scowling_Vodoriga" },
                    Zone = new int[] { Zone.RakaznarInnerCourt },
                },
                Reward = new RoeReward { Accolades = 18, Capacity = 100, Exp = 600, Sparks = 180 },
            });
            // Conflict: Ra'Kaznar Inner Court III
            r.Add(new RoeRecord(764)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 5,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Unrepentant_Byrgen" },
                    Zone = new int[] { Zone.RakaznarInnerCourt },
                },
                Reward = new RoeReward { Accolades = 18, Capacity = 100, Exp = 600, Sparks = 180 },
            });
            // Subjugation: Jailer of Justice
            r.Add(new RoeRecord(768)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 16912839 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Jailer of Hope
            r.Add(new RoeRecord(769)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 16912838 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Jailer of Prudence is handled in mob script
            r.Add(new RoeRecord(770)
            {
                Flags = RoeFlags.Repeat,
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Jailer of Love
            r.Add(new RoeRecord(771)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 16912848 } },
                Reward = new RoeReward
                {
                    Accolades = 100,
                    Exp = 5000,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher) },
                    Sparks = 1000,
                },
            });
            // Spoils - Ivory Chip
            r.Add(new RoeRecord(772)
            {
                Trigger = RoeTrigger.LootItem,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.IvoryChip } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Spoils - Scarlet Chip
            r.Add(new RoeRecord(773)
            {
                Trigger = RoeTrigger.LootItem,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.ScarletChip } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Spoils - Emerald Chip
            r.Add(new RoeRecord(774)
            {
                Trigger = RoeTrigger.LootItem,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.EmeraldChip } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Spoils - Orchid Chip
            r.Add(new RoeRecord(775)
            {
                Trigger = RoeTrigger.LootItem,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.OrchidChip } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Spoils - Cerulean Chip
            r.Add(new RoeRecord(776)
            {
                Trigger = RoeTrigger.LootItem,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.CeruleanChip } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Spoils - Silver Chip
            r.Add(new RoeRecord(777)
            {
                Trigger = RoeTrigger.LootItem,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.SilverChip } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Spoils - Smoky Chip
            r.Add(new RoeRecord(778)
            {
                Trigger = RoeTrigger.LootItem,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.SmokyChip } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Spoils - Magenta Chip
            r.Add(new RoeRecord(779)
            {
                Trigger = RoeTrigger.LootItem,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.MagentaChip } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Spoils - Charcoal Chip
            r.Add(new RoeRecord(780)
            {
                Trigger = RoeTrigger.LootItem,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.CharcoalChip } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Spoils - Smalt Chip
            r.Add(new RoeRecord(781)
            {
                Trigger = RoeTrigger.LootItem,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { ItemId = new int[] { Item.SmaltChip } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Proto-Ultima
            r.Add(new RoeRecord(782)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 16928966 } },
                Reward = new RoeReward
                {
                    Accolades = 100,
                    Exp = 5000,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher) },
                    Sparks = 1000,
                },
            });
            // Subjugation: Proto-Omega
            r.Add(new RoeRecord(783)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 16933124 } },
                Reward = new RoeReward
                {
                    Accolades = 100,
                    Exp = 5000,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher) },
                    Sparks = 1000,
                },
            });
            // Subjugation: Vulpangue
            r.Add(new RoeRecord(785)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 16986428 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Chamrosh
            r.Add(new RoeRecord(786)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17043887 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Cheese Hoarder Gigiroon
            r.Add(new RoeRecord(787)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17072172 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Brass Borer
            r.Add(new RoeRecord(788)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17027471 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Claret
            r.Add(new RoeRecord(789)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17027472 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Ob
            r.Add(new RoeRecord(790)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17072171 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Velionis
            r.Add(new RoeRecord(791)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 16998872 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation Lil' Apkallu
            r.Add(new RoeRecord(792)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 16998871 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Chigre
            r.Add(new RoeRecord(793)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17056186 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Iriz Ima
            r.Add(new RoeRecord(794)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 16986429 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Lividroot Amooshah
            r.Add(new RoeRecord(795)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 16990506 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Iriri Samariri
            r.Add(new RoeRecord(796)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17043888 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Anantaboga
            r.Add(new RoeRecord(797)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17027473 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Reacton
            r.Add(new RoeRecord(798)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17031599 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Dextrose
            r.Add(new RoeRecord(799)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17031598 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Wulgaru
            r.Add(new RoeRecord(800)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17072179 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Zareehkl the Jubilant
            r.Add(new RoeRecord(801)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 16998873 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Verdelet
            r.Add(new RoeRecord(802)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17101202 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Armed Gears
            r.Add(new RoeRecord(803)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17072178 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Gotoh Zha the Redolent
            r.Add(new RoeRecord(804)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 16986430 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Dea
            r.Add(new RoeRecord(805)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 16990507 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Nosferatu
            r.Add(new RoeRecord(806)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17056157 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Khromasoul Bhurborlor
            r.Add(new RoeRecord(807)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17027474 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Achamoth
            r.Add(new RoeRecord(808)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17031600 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Mahjlaef the Paintorn
            r.Add(new RoeRecord(809)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17101200 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Experimental Lamia
            r.Add(new RoeRecord(810)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17101205 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Nuhn
            r.Add(new RoeRecord(811)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 16998874 } },
                Reward = new RoeReward { Accolades = 30, Exp = 1500, Sparks = 300 },
            });
            // Subjugation: Battleclad Chariot
            r.Add(new RoeRecord(812)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17076578 } },
                Reward = new RoeReward { Accolades = 50, Exp = 2500, Sparks = 500 },
            });
            // Subjugation: Armored Chariot
            r.Add(new RoeRecord(813)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17080585 } },
                Reward = new RoeReward { Accolades = 50, Exp = 2500, Sparks = 500 },
            });
            // Subjugation: Long-Bowed Chariot
            r.Add(new RoeRecord(814)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17084687 } },
                Reward = new RoeReward { Accolades = 50, Exp = 2500, Sparks = 500 },
            });
            // Subjugation: Long-Armed Chariot
            r.Add(new RoeRecord(815)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobId = new int[] { 17088786 } },
                Reward = new RoeReward { Accolades = 50, Exp = 2500, Sparks = 500 },
            });
            // Asuran Fists
            r.Add(new RoeRecord(839)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Bastok, QuestId.Bastok.TheWallsOfYourMind) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Evisceration
            r.Add(new RoeRecord(840)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Outlands, QuestId.Outlands.CloakAndDagger) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Savage Blade
            r.Add(new RoeRecord(841)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Sandoria, QuestId.Sandoria.OldWounds) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Ground Strike
            r.Add(new RoeRecord(842)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Bastok, QuestId.Bastok.Inheritance) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Decimation
            r.Add(new RoeRecord(843)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Jeuno, QuestId.Jeuno.AxeTheCompetition) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Steel Cyclone
            r.Add(new RoeRecord(844)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Bastok, QuestId.Bastok.TheWeightOfYourLimits) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Spiral Hell
            r.Add(new RoeRecord(845)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Sandoria, QuestId.Sandoria.SoulsInShadow) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Impulse Drive
            r.Add(new RoeRecord(846)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Sandoria, QuestId.Sandoria.MethodsCreateMadness) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Blade: Ku
            r.Add(new RoeRecord(847)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Outlands, QuestId.Outlands.BugiSoden) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Tachi: Kasha
            r.Add(new RoeRecord(848)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Outlands, QuestId.Outlands.ThePotentialWithin) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Black Halo
            r.Add(new RoeRecord(849)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Windurst, QuestId.Windurst.OrasteryWoes) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Retribution
            r.Add(new RoeRecord(850)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Windurst, QuestId.Windurst.BloodAndGlory) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Empyreal Arrow
            r.Add(new RoeRecord(851)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.Windurst, QuestId.Windurst.FromSaplingsGrow) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Detonator
            r.Add(new RoeRecord(852)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs
                {
                    QuestComplete = ((int)QuestLog.Bastok, QuestId.Bastok.ShootFirstAskQuestionsLater),
                },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Conflict: Escha - Zi'Tah I
            r.Add(new RoeRecord(885)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobName = new string[] { "Eschan_Yztarg" }, Zone = new int[] { Zone.EschaZitah } },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.SiltPouch) },
                    Sparks = 300,
                },
            });
            // Conflict: Escha - Zi'Tah II
            r.Add(new RoeRecord(886)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobName = new string[] { "Eschan_Bugard" }, Zone = new int[] { Zone.EschaZitah } },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.SiltPouch) },
                    Sparks = 300,
                },
            });
            // Conflict: Escha - Zi'Tah III
            r.Add(new RoeRecord(887)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Eschan_Tarichuk" },
                    Zone = new int[] { Zone.EschaZitah },
                },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.SiltPouch) },
                    Sparks = 300,
                },
            });
            // Conflict: Escha - Zi'Tah IV
            r.Add(new RoeRecord(888)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Eschan_Shadow_Dragon" },
                    Zone = new int[] { Zone.EschaZitah },
                },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.SiltPouch) },
                    Sparks = 300,
                },
            });
            // Conflict: Escha - Zi'Tah V
            r.Add(new RoeRecord(889)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Eschan_Mosquito" },
                    Zone = new int[] { Zone.EschaZitah },
                },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.BeadPouch) },
                    Sparks = 300,
                },
            });
            // Conflict: Escha - Zi'Tah VI
            r.Add(new RoeRecord(901)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobName = new string[] { "Eschan_Puk" }, Zone = new int[] { Zone.EschaZitah } },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.BeadPouch) },
                    Sparks = 300,
                },
            });
            // Conflict: Escha - Zi'Tah VII
            r.Add(new RoeRecord(902)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Eschan_Opo-opo" },
                    Zone = new int[] { Zone.EschaZitah },
                },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.BeadPouch) },
                    Sparks = 300,
                },
            });
            // Conflict: Escha - Ru'Aun I
            r.Add(new RoeRecord(903)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobName = new string[] { "Eschan_Ilaern" }, Zone = new int[] { Zone.EschaRuaun } },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.SiltPouch) },
                    Sparks = 300,
                },
            });
            // Conflict: Escha - Ru'Aun II
            r.Add(new RoeRecord(904)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobName = new string[] { "Eschan_Phuabo" }, Zone = new int[] { Zone.EschaRuaun } },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.BeadPouch) },
                    Sparks = 300,
                },
            });
            // Conflict: Escha - Ru'Aun III
            r.Add(new RoeRecord(905)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobName = new string[] { "Eschan_Euvhi" }, Zone = new int[] { Zone.EschaRuaun } },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.SiltPouch) },
                    Sparks = 300,
                },
            });
            // Conflict: Escha - Ru'Aun IV
            r.Add(new RoeRecord(906)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Eschan_Clionid" },
                    Zone = new int[] { Zone.EschaRuaun },
                },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.BeadPouch) },
                    Sparks = 300,
                },
            });
            // Conflict: Escha - Ru'Aun V
            r.Add(new RoeRecord(907)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobName = new string[] { "Eschan_Hpemde" }, Zone = new int[] { Zone.EschaRuaun } },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.SiltPouch) },
                    Sparks = 300,
                },
            });
            // Conflict: Escha - Ru'Aun VI
            r.Add(new RoeRecord(908)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Eschan_Amoeban" },
                    Zone = new int[] { Zone.EschaRuaun },
                },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.BeadPouch) },
                    Sparks = 300,
                },
            });
            // Conflict: Escha - Ru'Aun VII
            r.Add(new RoeRecord(909)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobName = new string[] { "Eschan_Xzomit" }, Zone = new int[] { Zone.EschaRuaun } },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.SiltPouch) },
                    Sparks = 300,
                },
            });
            // Conflict: Escha - Ru'Aun VIII
            r.Add(new RoeRecord(910)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobName = new string[] { "Eschan_Murex" }, Zone = new int[] { Zone.EschaRuaun } },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.BeadPouch) },
                    Sparks = 300,
                },
            });
            // Conflict: Escha - Ru'Aun IX
            r.Add(new RoeRecord(911)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobName = new string[] { "Eschan_Ghrah" }, Zone = new int[] { Zone.EschaRuaun } },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.SiltPouch) },
                    Sparks = 300,
                },
            });
            // Conflict: Escha - Ru'Aun X
            r.Add(new RoeRecord(912)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobName = new string[] { "Eschan_Limule" }, Zone = new int[] { Zone.EschaRuaun } },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.BeadPouch) },
                    Sparks = 300,
                },
            });
            // Call Forth an Alter Ego (gives Cipher: Valaineral)
            r.Add(new RoeRecord(932)
            {
                Reward = new RoeReward
                {
                    Exp = 300,
                    Item = new RoeItem[] { RoeItem.Of(Item.CipherOfValaineralsAlterEgo) },
                    Sparks = 100,
                },
            });
            // Alter Ego: Valaineral (gives Cipher: Mihli)
            r.Add(new RoeRecord(933)
            {
                Reward = new RoeReward
                {
                    Exp = 300,
                    Item = new RoeItem[] { RoeItem.Of(Item.CipherOfMihlisAlterEgo) },
                    Sparks = 100,
                },
            });
            // Alter Ego: Mihli Aliapoh (gives Cipher: Tenzen)
            r.Add(new RoeRecord(934)
            {
                Reward = new RoeReward
                {
                    Exp = 300,
                    Item = new RoeItem[] { RoeItem.Of(Item.CipherOfTenzensAlterEgo) },
                    Sparks = 100,
                },
            });
            // Alter Ego: Tenzen (gives Cipher: Adelheid)
            r.Add(new RoeRecord(935)
            {
                Reward = new RoeReward
                {
                    Exp = 300,
                    Item = new RoeItem[] { RoeItem.Of(Item.CipherOfAdelheidsAlterEgo) },
                    Sparks = 100,
                },
            });
            // Alter Ego: Adelheid (gives Cipher: Joachim)
            r.Add(new RoeRecord(936)
            {
                Reward = new RoeReward
                {
                    Exp = 300,
                    Item = new RoeItem[] { RoeItem.Of(Item.CipherOfJoachimsAlterEgo) },
                    Sparks = 100,
                },
            });
            // Alter Ego: Joachim
            r.Add(new RoeRecord(937)
            {
                Reward = new RoeReward { Exp = 500, Sparks = 100 },
            });
            // Mog Safe Expansion: 60
            r.Add(new RoeRecord(939)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.OtherAreas, QuestId.OtherAreas.GiveAMoogleABreak) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Mog Safe Expansion: 70
            r.Add(new RoeRecord(940)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.OtherAreas, QuestId.OtherAreas.TheMooglePicnic) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Mog Safe Expansion: 80
            r.Add(new RoeRecord(941)
            {
                Trigger = RoeTrigger.CompleteQuest,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { QuestComplete = ((int)QuestLog.OtherAreas, QuestId.OtherAreas.MooglesInTheWild) },
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Conflict: Reisenjima I
            r.Add(new RoeRecord(943)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Obstreperous_Panopt" },
                    Zone = new int[] { Zone.Reisenjima },
                },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.BeadPouch) },
                    Sparks = 300,
                },
            });
            // Conflict: Reisenjima II
            r.Add(new RoeRecord(944)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Snaggletoothed_Tiger" },
                    Zone = new int[] { Zone.Reisenjima },
                },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.SiltPouch) },
                    Sparks = 300,
                },
            });
            // Conflict: Reisenjima III
            r.Add(new RoeRecord(945)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Agitated_Chapuli" },
                    Zone = new int[] { Zone.Reisenjima },
                },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.BeadPouch) },
                    Sparks = 300,
                },
            });
            // Conflict: Reisenjima IV
            r.Add(new RoeRecord(946)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Indomitable_Faaz" },
                    Zone = new int[] { Zone.Reisenjima },
                },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.SiltPouch) },
                    Sparks = 300,
                },
            });
        }

        private static void Part6(List<RoeRecord> r)
        {
            // Conflict: Reisenjima V
            r.Add(new RoeRecord(947)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Territorial_Mantis" },
                    Zone = new int[] { Zone.Reisenjima },
                },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.BeadPouch) },
                    Sparks = 300,
                },
            });
            // Conflict: Reisenjima VI
            r.Add(new RoeRecord(948)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Devouring_Mosquito" },
                    Zone = new int[] { Zone.Reisenjima },
                },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.SiltPouch) },
                    Sparks = 300,
                },
            });
            // Conflict: Reisenjima VII
            r.Add(new RoeRecord(949)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobName = new string[] { "Lentic_Toad" }, Zone = new int[] { Zone.Reisenjima } },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.BeadPouch) },
                    Sparks = 300,
                },
            });
            // Conflict: Reisenjima VIII
            r.Add(new RoeRecord(950)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Quarrelsome_Hippogryph" },
                    Zone = new int[] { Zone.Reisenjima },
                },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.SiltPouch) },
                    Sparks = 300,
                },
            });
            // Conflict: Reisenjima IX
            r.Add(new RoeRecord(951)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Rampaging_Beetle" },
                    Zone = new int[] { Zone.Reisenjima },
                },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.BeadPouch) },
                    Sparks = 300,
                },
            });
            // Conflict: Reisenjima X
            r.Add(new RoeRecord(952)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs
                {
                    MobName = new string[] { "Glowering_Ladybug" },
                    Zone = new int[] { Zone.Reisenjima },
                },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.SiltPouch) },
                    Sparks = 300,
                },
            });
            // Conflict: Reisenjima XI
            r.Add(new RoeRecord(953)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Repeat,
                Reqs = new RoeReqs { MobName = new string[] { "Lucani" }, Zone = new int[] { Zone.Reisenjima } },
                Reward = new RoeReward
                {
                    Accolades = 30,
                    Capacity = 150,
                    Exp = 900,
                    Item = new RoeItem[] { RoeItem.Of(Item.SiltPouch) },
                    Sparks = 300,
                },
            });
            // Achieve Level 99 (gives Kupon A-PK109 x5)
            r.Add(new RoeRecord(1045)
            {
                Flags = RoeFlags.Retro,
                Check = RoeCheckForm.MainLevel,
                Reqs = new RoeReqs { MJobLvl = 99 },
                Reward = new RoeReward
                {
                    Exp = 300,
                    Item = new RoeItem[] { RoeItem.Of(Item.MogKuponAPk109, 5) },
                    Sparks = 200,
                },
            });
            // An Eminent Scholar (gives Kupon W-EMI)
            r.Add(new RoeRecord(1046)
            {
                Trigger = RoeTrigger.TriggerNpc,
                Check = RoeCheckForm.Sparks,
                Reqs = new RoeReqs { CollectSparks = 2000 },
                Reward = new RoeReward
                {
                    Exp = 200,
                    Item = new RoeItem[] { RoeItem.Of(Item.MogKuponWEmi) },
                    Sparks = 200,
                },
            });
            // An Eminent Scholar 2 (gives Kupon A-EMI)
            r.Add(new RoeRecord(1047)
            {
                Trigger = RoeTrigger.TriggerNpc,
                Check = RoeCheckForm.Sparks,
                Reqs = new RoeReqs { CollectSparks = 4000 },
                Reward = new RoeReward
                {
                    Exp = 200,
                    Item = new RoeItem[] { RoeItem.Of(Item.MogKuponAEmi) },
                    Sparks = 200,
                },
            });
            // An Eminent Scholar 3 (gives Kupon A-EMI)
            r.Add(new RoeRecord(1048)
            {
                Trigger = RoeTrigger.TriggerNpc,
                Check = RoeCheckForm.Sparks,
                Reqs = new RoeReqs { CollectSparks = 6000 },
                Reward = new RoeReward
                {
                    Exp = 200,
                    Item = new RoeItem[] { RoeItem.Of(Item.MogKuponAEmi) },
                    Sparks = 200,
                },
            });
            // Always Stand on 117 (gives Cipher: Koru-Moru)
            r.Add(new RoeRecord(1049)
            {
                Trigger = RoeTrigger.TriggerNpc,
                Check = RoeCheckForm.ItemLevel,
                Reqs = new RoeReqs { HasItemLevel = 117 },
                Reward = new RoeReward
                {
                    Exp = 300,
                    Item = new RoeItem[] { RoeItem.Of(Item.CipherOfKoruMorusAlterEgo) },
                    Sparks = 200,
                },
            });
            // Level 30 Warrior +
            r.Add(new RoeRecord(1200)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.War, 30) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfDeathForDimwits, 12) },
                    Sparks = 100,
                },
            });
            // Level 50 Warrior
            r.Add(new RoeRecord(1201)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.War, 50) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfStrikingBullsDiary, 12) },
                    Sparks = 200,
                },
            });
            // Level 75 Warrior
            r.Add(new RoeRecord(1202)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.War, 75) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.RavagersSealFeet, 4) },
                    Sparks = 300,
                },
            });
            // Level 99 Warrior
            r.Add(new RoeRecord(1203)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.War, 99) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfRemsTaleChapter1, 2) },
                    Sparks = 400,
                },
            });
            // Level 30 Monk
            r.Add(new RoeRecord(1204)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Mnk, 30) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfMikhesMemo, 12) },
                    Sparks = 100,
                },
            });
            // Level 50 Monk
            r.Add(new RoeRecord(1205)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Mnk, 50) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfKayeelPayeelsMemoirs, 12) },
                    Sparks = 200,
                },
            });
            // Level 75 Monk
            r.Add(new RoeRecord(1206)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Mnk, 75) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.TantraSealFeet, 4) },
                    Sparks = 300,
                },
            });
            // Level 99 Monk
            r.Add(new RoeRecord(1207)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Mnk, 99) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfRemsTaleChapter2, 2) },
                    Sparks = 400,
                },
            });
            // Level 30 White Mage
            r.Add(new RoeRecord(1208)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Whm, 30) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfCoveffeBarrowsMusings, 12) },
                    Sparks = 100,
                },
            });
            // Level 50 White Mage
            r.Add(new RoeRecord(1209)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Whm, 50) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfAltanasHymn, 12) },
                    Sparks = 200,
                },
            });
            // Level 75 White Mage
            r.Add(new RoeRecord(1210)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Whm, 75) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.OrisonSealFeet, 4) },
                    Sparks = 300,
                },
            });
            // Level 99 White Mage
            r.Add(new RoeRecord(1211)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Whm, 99) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfRemsTaleChapter3, 2) },
                    Sparks = 400,
                },
            });
            // Level 30 Black Mage
            r.Add(new RoeRecord(1212)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Blm, 30) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.BountyList, 12) },
                    Sparks = 100,
                },
            });
            // Level 50 Black Mage
            r.Add(new RoeRecord(1213)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Blm, 50) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfDarkDeeds, 12) },
                    Sparks = 200,
                },
            });
            // Level 75 Black Mage
            r.Add(new RoeRecord(1214)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Blm, 75) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.GoetiaSealFeet, 4) },
                    Sparks = 300,
                },
            });
            // Level 99 Black Mage
            r.Add(new RoeRecord(1215)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Blm, 99) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfRemsTaleChapter4, 2) },
                    Sparks = 400,
                },
            });
            // Level 30 Red Mage
            r.Add(new RoeRecord(1216)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Rdm, 30) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.InvestigativeReport, 12) },
                    Sparks = 100,
                },
            });
            // Level 50 Red Mage
            r.Add(new RoeRecord(1217)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Rdm, 50) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfAidForAll, 12) },
                    Sparks = 200,
                },
            });
            // Level 75 Red Mage
            r.Add(new RoeRecord(1218)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Rdm, 75) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.EstoqueursSealFeet, 4) },
                    Sparks = 300,
                },
            });
            // Level 99 Red Mage
            r.Add(new RoeRecord(1219)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Rdm, 99) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfRemsTaleChapter5, 2) },
                    Sparks = 400,
                },
            });
            // Level 30 Thief
            r.Add(new RoeRecord(1220)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Thf, 30) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.DaggerEnchiridion, 12) },
                    Sparks = 100,
                },
            });
            // Level 50 Thief
            r.Add(new RoeRecord(1221)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Thf, 50) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfSwingAndStab, 12) },
                    Sparks = 200,
                },
            });
            // Level 75 Thief
            r.Add(new RoeRecord(1222)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Thf, 75) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.RaidersSealFeet, 4) },
                    Sparks = 300,
                },
            });
            // Level 99 Thief
            r.Add(new RoeRecord(1223)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Thf, 99) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfRemsTaleChapter6, 2) },
                    Sparks = 400,
                },
            });
            // Level 30 Paladin
            r.Add(new RoeRecord(1224)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Pld, 30) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfSwingAndStab, 12) },
                    Sparks = 100,
                },
            });
            // Level 50 Paladin
            r.Add(new RoeRecord(1225)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Pld, 50) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfTheSuccessor, 12) },
                    Sparks = 200,
                },
            });
            // Level 75 Paladin
            r.Add(new RoeRecord(1226)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Pld, 75) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CreedSealFeet, 4) },
                    Sparks = 300,
                },
            });
            // Level 99 Paladin
            r.Add(new RoeRecord(1227)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Pld, 99) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfRemsTaleChapter2, 2) },
                    Sparks = 400,
                },
            });
            // Level 30 Dark Knight
            r.Add(new RoeRecord(1228)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Drk, 30) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfLudwigsReport, 12) },
                    Sparks = 100,
                },
            });
            // Level 50 Dark Knight
            r.Add(new RoeRecord(1229)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Drk, 50) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfMieuseloirsDiary, 12) },
                    Sparks = 200,
                },
            });
            // Level 75 Dark Knight
            r.Add(new RoeRecord(1230)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Drk, 75) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.BaleSealFeet, 4) },
                    Sparks = 300,
                },
            });
            // Level 99 Dark Knight
            r.Add(new RoeRecord(1231)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Drk, 99) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfRemsTaleChapter3, 2) },
                    Sparks = 400,
                },
            });
            // Level 30 Beastmaster
            r.Add(new RoeRecord(1232)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Bst, 30) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfStrikingBullsDiary, 12) },
                    Sparks = 100,
                },
            });
            // Level 50 Beastmaster
            r.Add(new RoeRecord(1233)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Bst, 50) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfLudwigsReport, 12) },
                    Sparks = 200,
                },
            });
            // Level 75 Beastmaster
            r.Add(new RoeRecord(1234)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Bst, 75) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.FerineSealFeet, 4) },
                    Sparks = 300,
                },
            });
            // Level 99 Beastmaster
            r.Add(new RoeRecord(1235)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Bst, 99) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfRemsTaleChapter4, 2) },
                    Sparks = 400,
                },
            });
            // Level 30 Bard
            r.Add(new RoeRecord(1236)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Brd, 30) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfBreezyLibretto, 12) },
                    Sparks = 100,
                },
            });
            // Level 50 Bard
            r.Add(new RoeRecord(1237)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Brd, 50) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CavernousScore, 12) },
                    Sparks = 200,
                },
            });
            // Level 75 Bard
            r.Add(new RoeRecord(1238)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Brd, 75) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.AoidosSealFeet, 4) },
                    Sparks = 300,
                },
            });
            // Level 99 Bard
            r.Add(new RoeRecord(1239)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Brd, 99) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfRemsTaleChapter5, 2) },
                    Sparks = 400,
                },
            });
            // Level 30 Ranger
            r.Add(new RoeRecord(1240)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Rng, 30) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfPerihsPrimer, 12) },
                    Sparks = 100,
                },
            });
            // Level 50 Ranger
            r.Add(new RoeRecord(1241)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Rng, 50) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfBarrelsOfFun, 12) },
                    Sparks = 200,
                },
            });
            // Level 75 Ranger
            r.Add(new RoeRecord(1242)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Rng, 75) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.SylvanSealFeet, 4) },
                    Sparks = 300,
                },
            });
            // Level 99 Ranger
            r.Add(new RoeRecord(1243)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Rng, 99) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfRemsTaleChapter1, 2) },
                    Sparks = 400,
                },
            });
            // Level 30 Samurai
            r.Add(new RoeRecord(1244)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Sam, 30) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfNoilluriesLog, 12) },
                    Sparks = 100,
                },
            });
            // Level 50 Samurai
            r.Add(new RoeRecord(1245)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Sam, 50) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfClashOfTitans, 12) },
                    Sparks = 200,
                },
            });
            // Level 75 Samurai
            r.Add(new RoeRecord(1246)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Sam, 75) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.UnkaiSealFeet, 4) },
                    Sparks = 300,
                },
            });
            // Level 99 Samurai
            r.Add(new RoeRecord(1247)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Sam, 99) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfRemsTaleChapter2, 2) },
                    Sparks = 300,
                },
            });
            // Level 30 Ninja
            r.Add(new RoeRecord(1248)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Nin, 30) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfKagetorasDiary, 12) },
                    Sparks = 100,
                },
            });
            // Level 50 Ninja
            r.Add(new RoeRecord(1249)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Nin, 50) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfYomisDiagram, 12) },
                    Sparks = 200,
                },
            });
            // Level 75 Ninja
            r.Add(new RoeRecord(1250)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Nin, 75) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.IgaSealFeet, 4) },
                    Sparks = 300,
                },
            });
            // Level 99 Ninja
            r.Add(new RoeRecord(1251)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Nin, 99) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfRemsTaleChapter3, 2) },
                    Sparks = 400,
                },
            });
            // Level 30 Dragoon
            r.Add(new RoeRecord(1252)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Drg, 30) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfClashOfTitans, 12) },
                    Sparks = 100,
                },
            });
            // Level 50 Dragoon
            r.Add(new RoeRecord(1253)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Drg, 50) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfKayeelPayeelsMemoirs, 12) },
                    Sparks = 200,
                },
            });
            // Level 75 Dragoon
            r.Add(new RoeRecord(1254)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Drg, 75) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.LancersSealFeet, 4) },
                    Sparks = 300,
                },
            });
            // Level 99 Dragoon
            r.Add(new RoeRecord(1255)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Drg, 99) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfRemsTaleChapter4, 2) },
                    Sparks = 400,
                },
            });
            // Level 30 Summoner
            r.Add(new RoeRecord(1256)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Smn, 30) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfAstralHomeland, 12) },
                    Sparks = 100,
                },
            });
            // Level 50 Summoner
            r.Add(new RoeRecord(1257)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Smn, 50) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfKayeelPayeelsMemoirs, 12) },
                    Sparks = 200,
                },
            });
            // Level 75 Summoner
            r.Add(new RoeRecord(1258)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Smn, 75) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CallersSealFeet, 4) },
                    Sparks = 300,
                },
            });
            // Level 99 Summoner
            r.Add(new RoeRecord(1259)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Smn, 99) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfRemsTaleChapter5, 2) },
                    Sparks = 400,
                },
            });
            // Level 30 Blue Mage
            r.Add(new RoeRecord(1260)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Blu, 30) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfSwingAndStab, 12) },
                    Sparks = 100,
                },
            });
            // Level 50 Blue Mage
            r.Add(new RoeRecord(1261)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Blu, 50) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfLifeFormStudy, 12) },
                    Sparks = 200,
                },
            });
            // Level 75 Blue Mage
            r.Add(new RoeRecord(1262)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Blu, 75) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.MaviSealFeet, 4) },
                    Sparks = 300,
                },
            });
            // Level 99 Blue Mage
            r.Add(new RoeRecord(1263)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Blu, 99) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfRemsTaleChapter1, 2) },
                    Sparks = 400,
                },
            });
            // Level 30 Corsair
            r.Add(new RoeRecord(1264)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Cor, 30) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.DaggerEnchiridion, 12) },
                    Sparks = 100,
                },
            });
            // Level 50 Corsair
            r.Add(new RoeRecord(1265)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Cor, 50) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfBarrelsOfFun, 12) },
                    Sparks = 200,
                },
            });
            // Level 75 Corsair
            r.Add(new RoeRecord(1266)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Cor, 75) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.NavarchsSealFeet, 4) },
                    Sparks = 300,
                },
            });
            // Level 99 Corsair
            r.Add(new RoeRecord(1267)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Cor, 99) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfRemsTaleChapter2, 2) },
                    Sparks = 400,
                },
            });
            // Level 30 Puppetmaster
            r.Add(new RoeRecord(1268)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Pup, 30) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfMikhesMemo, 12) },
                    Sparks = 100,
                },
            });
            // Level 50 Puppetmaster
            r.Add(new RoeRecord(1269)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Pup, 50) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.DaggerEnchiridion, 12) },
                    Sparks = 200,
                },
            });
            // Level 75 Puppetmaster
            r.Add(new RoeRecord(1270)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Pup, 75) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CirqueSealFeet, 4) },
                    Sparks = 300,
                },
            });
            // Level 99 Puppetmaster
            r.Add(new RoeRecord(1271)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Pup, 99) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfRemsTaleChapter3, 2) },
                    Sparks = 400,
                },
            });
            // Level 30 Dancer
            r.Add(new RoeRecord(1272)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Dnc, 30) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.DaggerEnchiridion, 12) },
                    Sparks = 100,
                },
            });
            // Level 50 Dancer
            r.Add(new RoeRecord(1273)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Dnc, 50) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfMikhesMemo, 12) },
                    Sparks = 200,
                },
            });
            // Level 75 Dancer
            r.Add(new RoeRecord(1274)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Dnc, 75) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CharisSealFeet, 4) },
                    Sparks = 300,
                },
            });
            // Level 99 Dancer
            r.Add(new RoeRecord(1275)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Dnc, 99) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfRemsTaleChapter4, 2) },
                    Sparks = 400,
                },
            });
            // Level 30 Scholar
            r.Add(new RoeRecord(1276)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Sch, 30) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfKayeelPayeelsMemoirs, 12) },
                    Sparks = 100,
                },
            });
            // Level 50 Scholar
            r.Add(new RoeRecord(1277)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Sch, 50) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfFerreoussDiary, 12) },
                    Sparks = 200,
                },
            });
            // Level 75 Scholar
            r.Add(new RoeRecord(1278)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Sch, 75) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.SavantsSealFeet, 4) },
                    Sparks = 300,
                },
            });
            // Level 99 Scholar
            r.Add(new RoeRecord(1279)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Sch, 99) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfRemsTaleChapter5, 2) },
                    Sparks = 400,
                },
            });
            // Level 30 Geomancer
            r.Add(new RoeRecord(1280)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Geo, 30) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfHrohjsRecord, 12) },
                    Sparks = 100,
                },
            });
            // Level 50 Geomancer
            r.Add(new RoeRecord(1281)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Geo, 50) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfTheBellTolls, 12) },
                    Sparks = 200,
                },
            });
            // Level 75 Geomancer
            r.Add(new RoeRecord(1282)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Geo, 75) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.PlateOfIndiFrailty, 1) },
                    Sparks = 300,
                },
            });
            // Level 99 Geomancer
            r.Add(new RoeRecord(1283)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Geo, 99) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfRemsTaleChapter6, 1) },
                    Sparks = 400,
                },
            });
            // Level 30 Runefencer
            r.Add(new RoeRecord(1284)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Run, 30) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfMieuseloirsDiary, 12) },
                    Sparks = 100,
                },
            });
            // Level 50 Runefencer
            r.Add(new RoeRecord(1285)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Run, 50) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfSwingAndStab, 12) },
                    Sparks = 200,
                },
            });
            // Level 75 Runefencer
            r.Add(new RoeRecord(1286)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Run, 75) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.ScrollOfCrusade, 1) },
                    Sparks = 300,
                },
            });
            // Level 99 Runefencer
            r.Add(new RoeRecord(1287)
            {
                Trigger = RoeTrigger.AchieveLevel,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { JobLvl = (Job.Run, 99) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopyOfRemsTaleChapter7, 1) },
                    Sparks = 400,
                },
            });
        }

        private static void Part7(List<RoeRecord> r)
        {
            // San d'Oria Rank 1-1
            r.Add(new RoeRecord(1313)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Sandoria, Mission.Sandoria.SmashTheOrcishScouts) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.FireCrystal, 6) },
                    Sparks = 300,
                },
            });
            // San d'Oria Rank 1-2
            r.Add(new RoeRecord(1314)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Sandoria, Mission.Sandoria.BatHunt) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.IceCrystal, 6) },
                    Sparks = 300,
                },
            });
            // San d'Oria Rank 1-3
            r.Add(new RoeRecord(1315)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Sandoria, Mission.Sandoria.SaveTheChildren) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.FireCrystal, 6) },
                    Sparks = 300,
                },
            });
            // San d'Oria Rank 2-1
            r.Add(new RoeRecord(1316)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Sandoria, Mission.Sandoria.TheRescueDrill) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.IceCrystal, 6) },
                    Sparks = 300,
                },
            });
            // San d'Oria Rank 2-2
            r.Add(new RoeRecord(1317)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Sandoria, Mission.Sandoria.TheDavoiReport) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.FireCrystal, 6) },
                    Sparks = 300,
                },
            });
            // San d'Oria Rank 2-3
            r.Add(new RoeRecord(1318)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Sandoria, Mission.Sandoria.JourneyAbroad) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.IceCrystal, 6) },
                    Sparks = 300,
                },
            });
            // San d'Oria Rank 3-1
            r.Add(new RoeRecord(1319)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Sandoria, Mission.Sandoria.InfiltrateDavoi) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.FireCrystal, 6) },
                    Sparks = 300,
                },
            });
            // San d'Oria Rank 3-2
            r.Add(new RoeRecord(1320)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Sandoria, Mission.Sandoria.TheCrystalSpring) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.IceCrystal, 6) },
                    Sparks = 300,
                },
            });
            // San d'Oria Rank 3-3
            r.Add(new RoeRecord(1321)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Sandoria, Mission.Sandoria.AppointmentToJeuno) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.FireCrystal, 8) },
                    Sparks = 300,
                },
            });
            // San d'Oria Rank 4
            r.Add(new RoeRecord(1322)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Sandoria, Mission.Sandoria.Magicite) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.IceCrystal, 8) },
                    Sparks = 300,
                },
            });
            // San d'Oria Rank 5-1
            r.Add(new RoeRecord(1323)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Sandoria, Mission.Sandoria.TheRuinsOfFeiYin) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.FireCrystal, 8) },
                    Sparks = 300,
                },
            });
            // San d'Oria Rank 5-2
            r.Add(new RoeRecord(1324)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Sandoria, Mission.Sandoria.TheShadowLord) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.IceCrystal, 8) },
                    Sparks = 300,
                },
            });
            // San d'Oria Rank 6-1
            r.Add(new RoeRecord(1325)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Sandoria, Mission.Sandoria.LeautesLastWishes) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.FireCrystal, 10) },
                    Sparks = 300,
                },
            });
            // San d'Oria Rank 6-2
            r.Add(new RoeRecord(1326)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Sandoria, Mission.Sandoria.RanperresFinalRest) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.IceCrystal, 10) },
                    Sparks = 300,
                },
            });
            // San d'Oria Rank 7-1
            r.Add(new RoeRecord(1327)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Sandoria, Mission.Sandoria.PrestigeOfThePapsque) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.FireCrystal, 10) },
                    Sparks = 300,
                },
            });
            // San d'Oria Rank 7-2
            r.Add(new RoeRecord(1328)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Sandoria, Mission.Sandoria.TheSecretWeapon) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.IceCrystal, 10) },
                    Sparks = 300,
                },
            });
            // San d'Oria Rank 8-1
            r.Add(new RoeRecord(1329)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Sandoria, Mission.Sandoria.ComingOfAge) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.FireCrystal, 12) },
                    Sparks = 300,
                },
            });
            // San d'Oria Rank 8-2
            r.Add(new RoeRecord(1330)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Sandoria, Mission.Sandoria.Lightbringer) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.IceCrystal, 12) },
                    Sparks = 300,
                },
            });
            // San d'Oria Rank 9-1
            r.Add(new RoeRecord(1331)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Sandoria, Mission.Sandoria.BreakingBarriers) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.FireCrystal, 12) },
                    Sparks = 300,
                },
            });
            // San d'Oria Rank 9-2
            r.Add(new RoeRecord(1332)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Sandoria, Mission.Sandoria.TheHeirToTheLight) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.IceCrystal, 12) },
                    Sparks = 300,
                },
            });
            // Bastok Rank 1-1
            r.Add(new RoeRecord(1333)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Bastok, Mission.Bastok.TheZeruhnReport) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.LightningCrystal, 6) },
                    Sparks = 300,
                },
            });
            // Bastok Rank 1-2
            r.Add(new RoeRecord(1334)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Bastok, Mission.Bastok.GeologicalSurvey) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.WaterCrystal, 6) },
                    Sparks = 300,
                },
            });
            // Bastok Rank 1-3
            r.Add(new RoeRecord(1335)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Bastok, Mission.Bastok.Fetichism) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.LightningCrystal, 6) },
                    Sparks = 300,
                },
            });
            // Bastok Rank 2-1
            r.Add(new RoeRecord(1336)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Bastok, Mission.Bastok.TheCrystalLine) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.WaterCrystal, 6) },
                    Sparks = 300,
                },
            });
            // Bastok Rank 2-2
            r.Add(new RoeRecord(1337)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Bastok, Mission.Bastok.WadingBeasts) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.LightningCrystal, 6) },
                    Sparks = 300,
                },
            });
            // Bastok Rank 2-3
            r.Add(new RoeRecord(1338)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Bastok, Mission.Bastok.TheEmissary) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.WaterCrystal, 6) },
                    Sparks = 300,
                },
            });
            // Bastok Rank 3-1
            r.Add(new RoeRecord(1339)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Bastok, Mission.Bastok.TheFourMusketeers) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.LightningCrystal, 6) },
                    Sparks = 300,
                },
            });
            // Bastok Rank 3-2
            r.Add(new RoeRecord(1340)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Bastok, Mission.Bastok.ToTheForsakenMines) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.WaterCrystal, 6) },
                    Sparks = 300,
                },
            });
            // Bastok Rank 3-3
            r.Add(new RoeRecord(1341)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Bastok, Mission.Bastok.Jeuno) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.LightningCrystal, 8) },
                    Sparks = 300,
                },
            });
            // Bastok Rank 4
            r.Add(new RoeRecord(1342)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Bastok, Mission.Bastok.Magicite) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.WaterCrystal, 8) },
                    Sparks = 300,
                },
            });
            // Bastok Rank 5-1
            r.Add(new RoeRecord(1343)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Bastok, Mission.Bastok.DarknessRising) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.LightningCrystal, 8) },
                    Sparks = 300,
                },
            });
            // Bastok Rank 5-2
            r.Add(new RoeRecord(1344)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Bastok, Mission.Bastok.XarcabardLandOfTruths) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.WaterCrystal, 8) },
                    Sparks = 300,
                },
            });
            // Bastok Rank 6-1
            r.Add(new RoeRecord(1345)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Bastok, Mission.Bastok.ReturnOfTheTalekeeper) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.LightningCrystal, 10) },
                    Sparks = 300,
                },
            });
            // Bastok Rank 6-2
            r.Add(new RoeRecord(1346)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Bastok, Mission.Bastok.ThePiratesCove) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.WaterCrystal, 10) },
                    Sparks = 300,
                },
            });
            // Bastok Rank 7-1
            r.Add(new RoeRecord(1347)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Bastok, Mission.Bastok.TheFinalImage) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.LightningCrystal, 10) },
                    Sparks = 300,
                },
            });
            // Bastok Rank 7-2
            r.Add(new RoeRecord(1348)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Bastok, Mission.Bastok.OnMyWay) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.WaterCrystal, 10) },
                    Sparks = 300,
                },
            });
            // Bastok Rank 8-1
            r.Add(new RoeRecord(1349)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Bastok, Mission.Bastok.TheChainsThatBindUs) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.LightningCrystal, 12) },
                    Sparks = 300,
                },
            });
            // Bastok Rank 8-2
            r.Add(new RoeRecord(1350)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Bastok, Mission.Bastok.EnterTheTalekeeper) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.WaterCrystal, 12) },
                    Sparks = 300,
                },
            });
            // Bastok Rank 9-1
            r.Add(new RoeRecord(1351)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Bastok, Mission.Bastok.TheSaltOfTheEarth) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.LightningCrystal, 12) },
                    Sparks = 300,
                },
            });
            // Bastok Rank 9-2
            r.Add(new RoeRecord(1352)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Bastok, Mission.Bastok.WhereTwoPathsConverge) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.WaterCrystal, 12) },
                    Sparks = 300,
                },
            });
            // Windurst Rank 1-1 +
            r.Add(new RoeRecord(1353)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs
                {
                    MissionComplete = (MissionLog.Windurst, Mission.Windurst.TheHorutotoRuinsExperiment),
                },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.WindCrystal, 6) },
                    Sparks = 300,
                },
            });
            // Windurst Rank 1-2 +
            r.Add(new RoeRecord(1354)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Windurst, Mission.Windurst.TheHeartOfTheMatter) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.EarthCrystal, 6) },
                    Sparks = 300,
                },
            });
            // Windurst Rank 1-3 +
            r.Add(new RoeRecord(1355)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Windurst, Mission.Windurst.ThePriceOfPeace) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.WindCrystal, 6) },
                    Sparks = 300,
                },
            });
            // Windurst Rank 2-1 +
            r.Add(new RoeRecord(1356)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Windurst, Mission.Windurst.LostForWords) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.EarthCrystal, 6) },
                    Sparks = 300,
                },
            });
            // Windurst Rank 2-2 +
            r.Add(new RoeRecord(1357)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Windurst, Mission.Windurst.ATestingTime) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.WindCrystal, 6) },
                    Sparks = 300,
                },
            });
            // Windurst Rank 2-3 +
            r.Add(new RoeRecord(1358)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Windurst, Mission.Windurst.TheThreeKingdoms) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.EarthCrystal, 6) },
                    Sparks = 300,
                },
            });
            // Windurst Rank 3-1 +
            r.Add(new RoeRecord(1359)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Windurst, Mission.Windurst.ToEachHisOwnRight) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.WindCrystal, 6) },
                    Sparks = 300,
                },
            });
            // Windurst Rank 3-2 +
            r.Add(new RoeRecord(1360)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Windurst, Mission.Windurst.WrittenInTheStars) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.EarthCrystal, 6) },
                    Sparks = 300,
                },
            });
            // Windurst Rank 3-3 +
            r.Add(new RoeRecord(1361)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Windurst, Mission.Windurst.ANewJourney) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.WindCrystal, 8) },
                    Sparks = 300,
                },
            });
            // Windurst Rank 4 +
            r.Add(new RoeRecord(1362)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Windurst, Mission.Windurst.Magicite) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.EarthCrystal, 8) },
                    Sparks = 300,
                },
            });
            // Windurst Rank 5-1 +
            r.Add(new RoeRecord(1363)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Windurst, Mission.Windurst.TheFinalSeal) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.WindCrystal, 8) },
                    Sparks = 300,
                },
            });
            // Windurst Rank 5-2 +
            r.Add(new RoeRecord(1364)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Windurst, Mission.Windurst.TheShadowAwaits) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.EarthCrystal, 8) },
                    Sparks = 300,
                },
            });
            // Windurst Rank 6-1
            r.Add(new RoeRecord(1365)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Windurst, Mission.Windurst.FullMoonFountain) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.WindCrystal, 10) },
                    Sparks = 300,
                },
            });
            // Windurst Rank 6-2
            r.Add(new RoeRecord(1366)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Windurst, Mission.Windurst.SaintlyInvitation) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.EarthCrystal, 10) },
                    Sparks = 300,
                },
            });
            // Windurst Rank 7-1
            r.Add(new RoeRecord(1367)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Windurst, Mission.Windurst.TheSixthMinistry) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.WindCrystal, 10) },
                    Sparks = 300,
                },
            });
            // Windurst Rank 7-2
            r.Add(new RoeRecord(1368)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Windurst, Mission.Windurst.AwakeningOfTheGods) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.EarthCrystal, 10) },
                    Sparks = 300,
                },
            });
            // Windurst Rank 8-1
            r.Add(new RoeRecord(1369)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Windurst, Mission.Windurst.Vain) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.WindCrystal, 12) },
                    Sparks = 300,
                },
            });
            // Windurst Rank 8-2
            r.Add(new RoeRecord(1370)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Windurst, Mission.Windurst.TheJesterWhodBeKing) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.EarthCrystal, 12) },
                    Sparks = 300,
                },
            });
            // Windurst Rank 9-1
            r.Add(new RoeRecord(1371)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Windurst, Mission.Windurst.DollOfTheDead) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.WindCrystal, 12) },
                    Sparks = 300,
                },
            });
            // Windurst Rank 9-2
            r.Add(new RoeRecord(1372)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Windurst, Mission.Windurst.MoonReading) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.EarthCrystal, 12) },
                    Sparks = 300,
                },
            });
            // Rhapsodies of Vana'diel 1-1 +
            r.Add(new RoeRecord(1373)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Rov, Mission.Rov.FlamesOfPrayer) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher, 2) },
                    Sparks = 300,
                },
            });
            // Rhapsodies of Vana'diel 1-2 +
            r.Add(new RoeRecord(1374)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Rov, Mission.Rov.ALandAfterTime) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher, 2) },
                    Sparks = 300,
                },
            });
            // Rhapsodies of Vana'diel 1-3 +
            r.Add(new RoeRecord(1375)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Rov, Mission.Rov.SetFree) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher, 2) },
                    Sparks = 300,
                },
            });
            // Zilart Mission 1 +
            r.Add(new RoeRecord(1377)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Zilart, Mission.Zilart.TheNewFrontier) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Zilart Mission 2 +
            r.Add(new RoeRecord(1378)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Zilart, Mission.Zilart.WelcomeTnorg) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Zilart Mission 3 +
            r.Add(new RoeRecord(1379)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Zilart, Mission.Zilart.KazhamsChieftainess) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Zilart Mission 4 +
            r.Add(new RoeRecord(1380)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Zilart, Mission.Zilart.TheTempleOfUggalepih) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Zilart Mission 5 +
            r.Add(new RoeRecord(1381)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Zilart, Mission.Zilart.HeadstonePilgrimage) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Zilart Mission 6 +
            r.Add(new RoeRecord(1382)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Zilart, Mission.Zilart.ThroughTheQuicksandCaves) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Zilart Mission 7 +
            r.Add(new RoeRecord(1383)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Zilart, Mission.Zilart.TheChamberOfOracles) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Zilart Mission 8 +
            r.Add(new RoeRecord(1384)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Zilart, Mission.Zilart.ReturnToDelkfuttsTower) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Zilart Mission 9 +
            r.Add(new RoeRecord(1385)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Zilart, Mission.Zilart.Romaeve) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Zilart Mission 10 +
            r.Add(new RoeRecord(1386)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Zilart, Mission.Zilart.TheTempleOfDesolation) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Zilart Mission 11 +
            r.Add(new RoeRecord(1387)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Zilart, Mission.Zilart.TheHallOfTheGods) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Zilart Mission 12 +
            r.Add(new RoeRecord(1388)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Zilart, Mission.Zilart.TheMithraAndTheCrystal) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Zilart Mission 13 +
            r.Add(new RoeRecord(1389)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Zilart, Mission.Zilart.TheGateOfTheGods) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Zilart Mission 14 +
            r.Add(new RoeRecord(1390)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Zilart, Mission.Zilart.ArkAngels) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Zilart Mission 15 +
            r.Add(new RoeRecord(1391)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Zilart, Mission.Zilart.TheSealedShrine) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Zilart Mission 16 +
            r.Add(new RoeRecord(1392)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Zilart, Mission.Zilart.TheCelestialNexus) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Chains of Promathia Chapter 1 +
            r.Add(new RoeRecord(1393)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Cop, Mission.Cop.TheMothercrystals) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Chains of Promathia Chapter 2 +
            r.Add(new RoeRecord(1394)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Cop, Mission.Cop.AncientVows) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Chains of Promathia Chapter 3
            r.Add(new RoeRecord(1395)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Cop, Mission.Cop.DarknessNamed) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Chains of Promathia Chapter 4
            r.Add(new RoeRecord(1396)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Cop, Mission.Cop.SlanderousUtterings) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Chains of Promathia Chapter 5
            r.Add(new RoeRecord(1397)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Cop, Mission.Cop.ThreePaths) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Chains of Promathia Chapter 6
            r.Add(new RoeRecord(1398)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Cop, Mission.Cop.OneToBeFeared) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Chains of Promathia Chapter 7
            r.Add(new RoeRecord(1399)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Cop, Mission.Cop.TheWarriorsPath) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Chains of Promathia Chapter 8
            r.Add(new RoeRecord(1400)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Cop, Mission.Cop.Dawn) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Wings of the Goddess 1
            r.Add(new RoeRecord(1402)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Wotg, Mission.Wotg.BackToTheBeginning) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Wings of the Goddess 2
            r.Add(new RoeRecord(1403)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Wotg, Mission.Wotg.CaitSith) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Wings of the Goddess 3
            r.Add(new RoeRecord(1404)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Wotg, Mission.Wotg.InTheNameOfTheFather) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Wings of the Goddess 4
            r.Add(new RoeRecord(1405)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Wotg, Mission.Wotg.CrossroadsOfTime) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Wings of the Goddess 5
            r.Add(new RoeRecord(1406)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Wotg, Mission.Wotg.FateInHaze) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Wings of the Goddess 6
            r.Add(new RoeRecord(1407)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Wotg, Mission.Wotg.AdieuLilisette) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Wings of the Goddess 7
            r.Add(new RoeRecord(1408)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Wotg, Mission.Wotg.GlimmerOfLife) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Wings of the Goddess 8
            r.Add(new RoeRecord(1409)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Wotg, Mission.Wotg.ATokenOfTroth) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Treasures of Aht Urhgan 1
            r.Add(new RoeRecord(1410)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Toau, Mission.Toau.AMercenaryLife) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Treasures of Aht Urhgan 2
            r.Add(new RoeRecord(1411)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Toau, Mission.Toau.PassingGlory) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Treasures of Aht Urhgan 3
            r.Add(new RoeRecord(1412)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Toau, Mission.Toau.PlayingThePart) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Treasures of Aht Urhgan 4
            r.Add(new RoeRecord(1413)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Toau, Mission.Toau.SentinelsHonor) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Treasures of Aht Urhgan 5
            r.Add(new RoeRecord(1414)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Toau, Mission.Toau.StirringsOfWar) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
        }

        private static void Part8(List<RoeRecord> r)
        {
            // Treasures of Aht Urhgan 6
            r.Add(new RoeRecord(1415)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Toau, Mission.Toau.TheEmpressCrowned) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Rhapsodies of Vana'diel 2-1
            r.Add(new RoeRecord(1416)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Rov, Mission.Rov.CrashingWaves) },
                Reward = new RoeReward
                {
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher, 2) },
                    Sparks = 300,
                },
            });
            // Seekers of Adoulin Chapter 1
            r.Add(new RoeRecord(1426)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Soa, Mission.Soa.ArcielaAppearsAgain) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Seekers of Adoulin Chapter 2
            r.Add(new RoeRecord(1427)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Soa, Mission.Soa.Yggdrasil) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Seekers of Adoulin Chapter 3
            r.Add(new RoeRecord(1428)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Soa, Mission.Soa.GlimmerOfPortent) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Seekers of Adoulin Chapter 4
            r.Add(new RoeRecord(1429)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Soa, Mission.Soa.RoyalBlessings) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Seekers of Adoulin Chapter 5
            r.Add(new RoeRecord(1430)
            {
                Trigger = RoeTrigger.CompleteMission,
                Flags = RoeFlags.Retro,
                Reqs = new RoeReqs { MissionComplete = (MissionLog.Soa, Mission.Soa.UndyingLight) },
                Reward = new RoeReward { Exp = 500, Sparks = 300 },
            });
            // Assist Channel
            r.Add(new RoeRecord(1448)
            {
                Reward = new RoeReward { Exp = 300, Sparks = 100 },
            });
            // Unity Communique A (UC)
            r.Add(new RoeRecord(3000)
            {
                Trigger = RoeTrigger.UnityChat,
                Flags = RoeFlags.Unity,
                Reward = new RoeReward { Accolades = 100, Exp = 500, Sparks = 100 },
            });
            // Vanquish Multiple Enemies A (UC)
            r.Add(new RoeRecord(3002)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobXp = true },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Aquans A (UC)
            r.Add(new RoeRecord(3003)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobSystem = new int[] { Ecosystem.Aquan }, MobXp = true },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Amorphs with Physical Damage A (UC)
            r.Add(new RoeRecord(3004)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    AtkType = AttackType.Physical,
                    MobSystem = new int[] { Ecosystem.Amorph },
                    MobXp = true,
                },
                Reward = new RoeReward { Accolades = 400, Exp = 500, Sparks = 100 },
            });
            // Vanquish Beasts with Magic A (UC)
            r.Add(new RoeRecord(3005)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    AtkType = AttackType.Magical,
                    MobSystem = new int[] { Ecosystem.Beast },
                    MobXp = true,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Total Successful Woodworking Syntheses A (UC)
            r.Add(new RoeRecord(3006)
            {
                Trigger = RoeTrigger.SuccessfulSynthesis,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.Woodworking },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Total Successful Leatherworking Syntheses A (UC)
            r.Add(new RoeRecord(3007)
            {
                Trigger = RoeTrigger.SuccessfulSynthesis,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.Leathercraft },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Total Suc. Mining Attempts A (UC)
            r.Add(new RoeRecord(3008)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = 4 },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Unity Communique B (UC)
            r.Add(new RoeRecord(3009)
            {
                Trigger = RoeTrigger.UnityChat,
                Flags = RoeFlags.Unity,
                Reward = new RoeReward { Accolades = 100, Exp = 500, Sparks = 100 },
            });
            // Chocobo Digging B (UC)
            r.Add(new RoeRecord(3010)
            {
                Trigger = RoeTrigger.ChocoboDigSuccess,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reward = new RoeReward { Accolades = 100, Exp = 500, Sparks = 100 },
            });
            // Vanquish Multiple Enemies B (UC)
            r.Add(new RoeRecord(3011)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobXp = true },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Arcana B (UC)
            r.Add(new RoeRecord(3012)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobSystem = new int[] { Ecosystem.Arcana }, MobXp = true },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Undead with Physical Damage B (UC)
            r.Add(new RoeRecord(3013)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    AtkType = AttackType.Physical,
                    MobSystem = new int[] { Ecosystem.Undead },
                    MobXp = true,
                },
                Reward = new RoeReward { Accolades = 400, Exp = 500, Sparks = 100 },
            });
            // Vanquish Plantoids with Magic B (UC)
            r.Add(new RoeRecord(3014)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    AtkType = AttackType.Magical,
                    MobSystem = new int[] { Ecosystem.Plantoid },
                    MobXp = true,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Total Successful Blacksmithing Syntheses B (UC)
            r.Add(new RoeRecord(3015)
            {
                Trigger = RoeTrigger.SuccessfulSynthesis,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.Smithing },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Total Suc. Logging Attempts B (UC)
            r.Add(new RoeRecord(3017)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = 3 },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Unity Communique C (UC)
            r.Add(new RoeRecord(3018)
            {
                Trigger = RoeTrigger.UnityChat,
                Flags = RoeFlags.Unity,
                Reward = new RoeReward { Accolades = 100, Exp = 500, Sparks = 100 },
            });
            // Vanquish Multiple Enemies C (UC)
            r.Add(new RoeRecord(3020)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobXp = true },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Vermin C (UC)
            r.Add(new RoeRecord(3021)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobSystem = new int[] { Ecosystem.Vermin }, MobXp = true },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Birds with Physical Damage C (UC)
            r.Add(new RoeRecord(3022)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    AtkType = AttackType.Physical,
                    MobSystem = new int[] { Ecosystem.Bird },
                    MobXp = true,
                },
                Reward = new RoeReward { Accolades = 400, Exp = 500, Sparks = 100 },
            });
            // Vanquish Lizards with Magic C (UC)
            r.Add(new RoeRecord(3023)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    AtkType = AttackType.Magical,
                    MobSystem = new int[] { Ecosystem.Lizard },
                    MobXp = true,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Total Successful Goldsmithing Syntheses C (UC)
            r.Add(new RoeRecord(3024)
            {
                Trigger = RoeTrigger.SuccessfulSynthesis,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.Goldsmithing },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Total Suc. Harvesting Attempts C (UC)
            r.Add(new RoeRecord(3026)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = 1 },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Unity Communique D (UC)
            r.Add(new RoeRecord(3027)
            {
                Trigger = RoeTrigger.UnityChat,
                Flags = RoeFlags.Unity,
                Reward = new RoeReward { Accolades = 100, Exp = 500, Sparks = 100 },
            });
            // Vanquish Multiple Enemies D (UC)
            r.Add(new RoeRecord(3029)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobXp = true },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Beasts D (UC)
            r.Add(new RoeRecord(3030)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobSystem = new int[] { Ecosystem.Beast }, MobXp = true },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Aquans with Physical Damage D (UC)
            r.Add(new RoeRecord(3031)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    AtkType = AttackType.Physical,
                    MobSystem = new int[] { Ecosystem.Aquan },
                    MobXp = true,
                },
                Reward = new RoeReward { Accolades = 400, Exp = 500, Sparks = 100 },
            });
            // Vanquish Amorphs with Magic D (UC)
            r.Add(new RoeRecord(3032)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    AtkType = AttackType.Magical,
                    MobSystem = new int[] { Ecosystem.Amorph },
                    MobXp = true,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Total Successful Cloth Syntheses D (UC)
            r.Add(new RoeRecord(3033)
            {
                Trigger = RoeTrigger.SuccessfulSynthesis,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.Clothcraft },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Total Successful Bone Syntheses D (UC)
            r.Add(new RoeRecord(3034)
            {
                Trigger = RoeTrigger.SuccessfulSynthesis,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.Bonecraft },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Total Suc. Mining Attempts D (UC)
            r.Add(new RoeRecord(3035)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = 4 },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Unity Communique E (UC)
            r.Add(new RoeRecord(3036)
            {
                Trigger = RoeTrigger.UnityChat,
                Flags = RoeFlags.Unity,
                Reward = new RoeReward { Accolades = 100, Exp = 500, Sparks = 100 },
            });
            // Vanquish Multiple Enemies E (UC)
            r.Add(new RoeRecord(3038)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobXp = true },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Plantoids E (UC)
            r.Add(new RoeRecord(3039)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobSystem = new int[] { Ecosystem.Plantoid }, MobXp = true },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Arcana with Physical Damage E (UC)
            r.Add(new RoeRecord(3040)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    AtkType = AttackType.Physical,
                    MobSystem = new int[] { Ecosystem.Arcana },
                    MobXp = true,
                },
                Reward = new RoeReward { Accolades = 400, Exp = 500, Sparks = 100 },
            });
            // Vanquish Undead with Magic E (UC)
            r.Add(new RoeRecord(3041)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    AtkType = AttackType.Magical,
                    MobSystem = new int[] { Ecosystem.Undead },
                    MobXp = true,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Total Successful Cooking Syntheses E (UC)
            r.Add(new RoeRecord(3042)
            {
                Trigger = RoeTrigger.SuccessfulSynthesis,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.Cooking },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Total Suc. Logging Attempts E (UC)
            r.Add(new RoeRecord(3044)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = 3 },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Unity Communique F (UC)
            r.Add(new RoeRecord(3045)
            {
                Trigger = RoeTrigger.UnityChat,
                Flags = RoeFlags.Unity,
                Reward = new RoeReward { Accolades = 100, Exp = 500, Sparks = 100 },
            });
            // Level Sync to Vanquish Enemies F (UC)
            r.Add(new RoeRecord(3046)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { LevelSync = true, MobXp = true },
                Reward = new RoeReward { Accolades = 100, Exp = 500, Sparks = 100 },
            });
            // Vanquish Multiple Enemies F (UC)
            r.Add(new RoeRecord(3047)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobXp = true },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Lizards F (UC)
            r.Add(new RoeRecord(3048)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobSystem = new int[] { Ecosystem.Lizard }, MobXp = true },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Vermin with Physical Damage F (UC)
            r.Add(new RoeRecord(3049)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    AtkType = AttackType.Physical,
                    MobSystem = new int[] { Ecosystem.Vermin },
                    MobXp = true,
                },
                Reward = new RoeReward { Accolades = 400, Exp = 500, Sparks = 100 },
            });
            // Vanquish Birds with Magic F (UC)
            r.Add(new RoeRecord(3050)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    AtkType = AttackType.Magical,
                    MobSystem = new int[] { Ecosystem.Bird },
                    MobXp = true,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Total Successful Alchemy Syntheses F (UC)
            r.Add(new RoeRecord(3051)
            {
                Trigger = RoeTrigger.SuccessfulSynthesis,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.Alchemy },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Total Suc. Harvesting Attempts F (UC)
            r.Add(new RoeRecord(3053)
            {
                Trigger = RoeTrigger.HelmSuccess,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = 1 },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Signet, brb (W)
            r.Add(new RoeRecord(3367)
            {
                Flags = RoeFlags.Weekly,
                Reward = new RoeReward { Exp = 1115, Sparks = 50 },
            });
            // Club Weapon Skills (UC)
            r.Add(new RoeRecord(3488)
            {
                Trigger = RoeTrigger.UseWeaponskill,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.Club, UnityLeader = UnityLeader.Pieuje },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Rala Waterways (UC)
            r.Add(new RoeRecord(3489)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Pieuje, Zone = new int[] { Zone.RalaWaterways } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Rabbits (UC)
            r.Add(new RoeRecord(3490)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Rabbit },
                    MobXp = true,
                    UnityLeader = UnityLeader.Pieuje,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Rambukk (UC)
            r.Add(new RoeRecord(3491)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17191044 }, UnityLeader = UnityLeader.Pieuje },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Heal Unity Allies (UC)
            r.Add(new RoeRecord(3492)
            {
                Trigger = RoeTrigger.HealUnityAlly,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Pieuje },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Spoils - Bat Fangs (UC)
            r.Add(new RoeRecord(3493)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 5,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { ItemId = new int[] { Item.BatFang }, UnityLeader = UnityLeader.Pieuje },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: La Theine Plateau (UC)
            r.Add(new RoeRecord(3494)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Pieuje, Zone = new int[] { Zone.LaTheinePlateau } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Bats (UC)
            r.Add(new RoeRecord(3495)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Bat },
                    MobXp = true,
                    UnityLeader = UnityLeader.Pieuje,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Tumbling Truffle (UC)
            r.Add(new RoeRecord(3496)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17195259 }, UnityLeader = UnityLeader.Pieuje },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Staff Weapon Skills (UC)
            r.Add(new RoeRecord(3498)
            {
                Trigger = RoeTrigger.UseWeaponskill,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.Staff, UnityLeader = UnityLeader.Pieuje },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Eldieme Necropolis (UC)
            r.Add(new RoeRecord(3499)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Pieuje, Zone = new int[] { Zone.TheEldiemeNecropolis } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Treants (UC)
            r.Add(new RoeRecord(3500)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Treant },
                    MobXp = true,
                    UnityLeader = UnityLeader.Pieuje,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Duke Decapod (UC)
            r.Add(new RoeRecord(3501)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17252725 }, UnityLeader = UnityLeader.Pieuje },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Heal Unity Allies (UC)
            r.Add(new RoeRecord(3502)
            {
                Trigger = RoeTrigger.HealUnityAlly,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Pieuje },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Spoils - Bomb Ash (UC)
            r.Add(new RoeRecord(3503)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 5,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { ItemId = new int[] { Item.PinchOfBombAsh }, UnityLeader = UnityLeader.Pieuje },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Davoi (UC)
            r.Add(new RoeRecord(3504)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Pieuje, Zone = new int[] { Zone.Davoi } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Bombs (UC)
            r.Add(new RoeRecord(3505)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Bomb },
                    MobXp = true,
                    UnityLeader = UnityLeader.Pieuje,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Poisonhand Gnadgad (UC)
            r.Add(new RoeRecord(3506)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17387644 }, UnityLeader = UnityLeader.Pieuje },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Magic Bursts (UC)
            r.Add(new RoeRecord(3507)
            {
                Trigger = RoeTrigger.MagicBurst,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Pieuje },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Great Katana Weapon Skills (UC)
            r.Add(new RoeRecord(3508)
            {
                Trigger = RoeTrigger.UseWeaponskill,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.GreatKatana, UnityLeader = UnityLeader.Ayame },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: North Gustaberg (UC)
            r.Add(new RoeRecord(3509)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Ayame, Zone = new int[] { Zone.NorthGustaberg } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Worms (UC)
            r.Add(new RoeRecord(3510)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Worm },
                    MobXp = true,
                    UnityLeader = UnityLeader.Ayame,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Stinging Sophie (UC)
            r.Add(new RoeRecord(3511)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17211537, 17211561 }, UnityLeader = UnityLeader.Ayame },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Heal Unity Allies (UC)
            r.Add(new RoeRecord(3512)
            {
                Trigger = RoeTrigger.HealUnityAlly,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Ayame },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Spoils - Cockatrice Meat (UC)
            r.Add(new RoeRecord(3513)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 5,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    ItemId = new int[] { Item.SliceOfCockatriceMeat },
                    UnityLeader = UnityLeader.Ayame,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Yahse Hunting Grounds (UC)
            r.Add(new RoeRecord(3514)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Ayame, Zone = new int[] { Zone.YahseHuntingGrounds } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Cockatrices (UC)
            r.Add(new RoeRecord(3515)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Cockatrice },
                    MobXp = true,
                    UnityLeader = UnityLeader.Ayame,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Tococo (UC)
            r.Add(new RoeRecord(3516)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17215778 }, UnityLeader = UnityLeader.Ayame },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Archery Weapon Skills (UC)
            r.Add(new RoeRecord(3518)
            {
                Trigger = RoeTrigger.UseWeaponskill,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.Archery, UnityLeader = UnityLeader.Ayame },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Crawlers' Nest (UC)
            r.Add(new RoeRecord(3519)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Ayame, Zone = new int[] { Zone.CrawlersNest } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Lizards (UC)
            r.Add(new RoeRecord(3520)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Lizard },
                    MobXp = true,
                    UnityLeader = UnityLeader.Ayame,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Aqrabuamelu (UC)
            r.Add(new RoeRecord(3521)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17584416 }, UnityLeader = UnityLeader.Ayame },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Heal Unity Allies (UC)
            r.Add(new RoeRecord(3522)
            {
                Trigger = RoeTrigger.HealUnityAlly,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Ayame },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Spoils - Land Crab Meat (UC)
            r.Add(new RoeRecord(3523)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 5,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { ItemId = new int[] { Item.SliceOfLandCrabMeat }, UnityLeader = UnityLeader.Ayame },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Beadeaux (UC)
            r.Add(new RoeRecord(3524)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Ayame, Zone = new int[] { Zone.Beadeaux } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Crabs (UC)
            r.Add(new RoeRecord(3525)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Crab },
                    MobXp = true,
                    UnityLeader = UnityLeader.Ayame,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Ge'Dha Evileye (UC)
            r.Add(new RoeRecord(3526)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17379450 }, UnityLeader = UnityLeader.Ayame },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Magic Bursts (UC)
            r.Add(new RoeRecord(3527)
            {
                Trigger = RoeTrigger.MagicBurst,
                Goal = 5,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Ayame },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Axe Weapon Skills (UC)
            r.Add(new RoeRecord(3528)
            {
                Trigger = RoeTrigger.UseWeaponskill,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.Axe, UnityLeader = UnityLeader.InvincibleShield },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: South Gustaberg (UC)
            r.Add(new RoeRecord(3529)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    UnityLeader = UnityLeader.InvincibleShield,
                    Zone = new int[] { Zone.SouthGustaberg },
                },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Sea Monks (UC)
            r.Add(new RoeRecord(3530)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.SeaMonk },
                    MobXp = true,
                    UnityLeader = UnityLeader.InvincibleShield,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Leaping Lizzy (UC)
            r.Add(new RoeRecord(3531)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobId = new int[] { 17215868, 17215888 },
                    UnityLeader = UnityLeader.InvincibleShield,
                },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Heal Unity Allies (UC)
            r.Add(new RoeRecord(3532)
            {
                Trigger = RoeTrigger.HealUnityAlly,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.InvincibleShield },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Spoils - Sleepshroom (UC)
            r.Add(new RoeRecord(3533)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 5,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    ItemId = new int[] { Item.Sleepshroom },
                    UnityLeader = UnityLeader.InvincibleShield,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Pashhow Marshlands (UC)
            r.Add(new RoeRecord(3534)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    UnityLeader = UnityLeader.InvincibleShield,
                    Zone = new int[] { Zone.PashhowMarshlands },
                },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
        }

        private static void Part9(List<RoeRecord> r)
        {
            // Vanquish Funguars (UC)
            r.Add(new RoeRecord(3535)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Funguar },
                    MobXp = true,
                    UnityLeader = UnityLeader.InvincibleShield,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Bloodpool Vorax (UC)
            r.Add(new RoeRecord(3536)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17224019 }, UnityLeader = UnityLeader.InvincibleShield },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Great Axe Weapon Skills (UC)
            r.Add(new RoeRecord(3538)
            {
                Trigger = RoeTrigger.UseWeaponskill,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.GreatAxe, UnityLeader = UnityLeader.InvincibleShield },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Ceizak Battlegrounds (UC)
            r.Add(new RoeRecord(3539)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    UnityLeader = UnityLeader.InvincibleShield,
                    Zone = new int[] { Zone.CeizakBattlegrounds },
                },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Flies (UC)
            r.Add(new RoeRecord(3540)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Fly },
                    MobXp = true,
                    UnityLeader = UnityLeader.InvincibleShield,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Be'Hya Hundredwall (UC)
            r.Add(new RoeRecord(3541)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17363258 }, UnityLeader = UnityLeader.InvincibleShield },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Heal Unity Allies (UC)
            r.Add(new RoeRecord(3542)
            {
                Trigger = RoeTrigger.HealUnityAlly,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.InvincibleShield },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Spoils - Raptor Skin (UC)
            r.Add(new RoeRecord(3543)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 5,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    ItemId = new int[] { Item.RaptorSkin },
                    UnityLeader = UnityLeader.InvincibleShield,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Beaucedine Glacier (UC)
            r.Add(new RoeRecord(3544)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    UnityLeader = UnityLeader.InvincibleShield,
                    Zone = new int[] { Zone.BeaucedineGlacier },
                },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Raptors (UC)
            r.Add(new RoeRecord(3545)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Raptor },
                    MobXp = true,
                    UnityLeader = UnityLeader.InvincibleShield,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Gargantua (UC)
            r.Add(new RoeRecord(3546)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17232079 }, UnityLeader = UnityLeader.InvincibleShield },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Magic Bursts (UC)
            r.Add(new RoeRecord(3547)
            {
                Trigger = RoeTrigger.MagicBurst,
                Goal = 5,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.InvincibleShield },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Club Weapon Skills (UC)
            r.Add(new RoeRecord(3548)
            {
                Trigger = RoeTrigger.UseWeaponskill,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.Club, UnityLeader = UnityLeader.Apururu },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: West Sarutabaruta (UC)
            r.Add(new RoeRecord(3549)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Apururu, Zone = new int[] { Zone.WestSarutabaruta } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Bees (UC)
            r.Add(new RoeRecord(3550)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Bee },
                    MobXp = true,
                    UnityLeader = UnityLeader.Apururu,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Tom Tit Tat (UC)
            r.Add(new RoeRecord(3551)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17248468, 17248486 }, UnityLeader = UnityLeader.Apururu },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Heal Unity Allies (UC)
            r.Add(new RoeRecord(3552)
            {
                Trigger = RoeTrigger.HealUnityAlly,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Apururu },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Spoils - Silk Thread (UC)
            r.Add(new RoeRecord(3553)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 5,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { ItemId = new int[] { Item.SpoolOfSilkThread }, UnityLeader = UnityLeader.Apururu },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Buburimu Peninsula (UC)
            r.Add(new RoeRecord(3554)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Apururu, Zone = new int[] { Zone.BuburimuPeninsula } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Crawlers (UC)
            r.Add(new RoeRecord(3555)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Crawler },
                    MobXp = true,
                    UnityLeader = UnityLeader.Apururu,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Buburimboo (UC)
            r.Add(new RoeRecord(3556)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17261003 }, UnityLeader = UnityLeader.Apururu },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Staff Weapon Skills (UC)
            r.Add(new RoeRecord(3558)
            {
                Trigger = RoeTrigger.UseWeaponskill,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.Staff, UnityLeader = UnityLeader.Apururu },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Castle Oztroja (UC)
            r.Add(new RoeRecord(3559)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Apururu, Zone = new int[] { Zone.CastleOztroja } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Ghosts (UC)
            r.Add(new RoeRecord(3560)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Ghost },
                    MobXp = true,
                    UnityLeader = UnityLeader.Apururu,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Mee Deggi the Punisher (UC)
            r.Add(new RoeRecord(3561)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17395800 }, UnityLeader = UnityLeader.Apururu },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Heal Unity Allies (UC)
            r.Add(new RoeRecord(3562)
            {
                Trigger = RoeTrigger.HealUnityAlly,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Apururu },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Spoils - Saruta Cotton (UC)
            r.Add(new RoeRecord(3563)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 5,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    ItemId = new int[] { Item.BallOfSarutaCotton },
                    UnityLeader = UnityLeader.Apururu,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Foret de Hennetiel (UC)
            r.Add(new RoeRecord(3564)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Apururu, Zone = new int[] { Zone.ForetDeHennetiel } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Mandragoras (UC)
            r.Add(new RoeRecord(3565)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Mandragora },
                    MobXp = true,
                    UnityLeader = UnityLeader.Apururu,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Juu Duzu the Whirlwind (UC)
            r.Add(new RoeRecord(3566)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17371300 }, UnityLeader = UnityLeader.Apururu },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Magic Bursts (UC)
            r.Add(new RoeRecord(3567)
            {
                Trigger = RoeTrigger.MagicBurst,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Apururu },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Hand-to-Hand Weapon Skills (UC)
            r.Add(new RoeRecord(3568)
            {
                Trigger = RoeTrigger.UseWeaponskill,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.HandToHand, UnityLeader = UnityLeader.Maat },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Yorcia Weald (UC)
            r.Add(new RoeRecord(3569)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Maat, Zone = new int[] { Zone.YorciaWeald } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Leeches (UC)
            r.Add(new RoeRecord(3570)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Leech },
                    MobXp = true,
                    UnityLeader = UnityLeader.Maat,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Canal Moocher (UC)
            r.Add(new RoeRecord(3571)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17469578 }, UnityLeader = UnityLeader.Maat },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Heal Unity Allies (UC)
            r.Add(new RoeRecord(3572)
            {
                Trigger = RoeTrigger.HealUnityAlly,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Maat },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Spoils - Spider Web (UC)
            r.Add(new RoeRecord(3573)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 5,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { ItemId = new int[] { Item.SpiderWeb }, UnityLeader = UnityLeader.Maat },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Rolanberry Fields (UC)
            r.Add(new RoeRecord(3574)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Maat, Zone = new int[] { Zone.RolanberryFields } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Spiders (UC)
            r.Add(new RoeRecord(3575)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Spider },
                    MobXp = true,
                    UnityLeader = UnityLeader.Maat,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Eldritch Edge (UC)
            r.Add(new RoeRecord(3576)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17228150 }, UnityLeader = UnityLeader.Maat },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Great Sword Weapon Skills (UC)
            r.Add(new RoeRecord(3578)
            {
                Trigger = RoeTrigger.UseWeaponskill,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.GreatSword, UnityLeader = UnityLeader.Maat },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Meriphataud Mountains (UC)
            r.Add(new RoeRecord(3579)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Maat, Zone = new int[] { Zone.MeriphataudMountains } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Wyverns (UC)
            r.Add(new RoeRecord(3580)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Wyvern },
                    MobXp = true,
                    UnityLeader = UnityLeader.Maat,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Patripatan (UC)
            r.Add(new RoeRecord(3581)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17264972 }, UnityLeader = UnityLeader.Maat },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Heal Unity Allies (UC)
            r.Add(new RoeRecord(3582)
            {
                Trigger = RoeTrigger.HealUnityAlly,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Maat },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Spoils - Hecteyes Eye (UC)
            r.Add(new RoeRecord(3583)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 5,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { ItemId = new int[] { Item.HecteyesEye }, UnityLeader = UnityLeader.Maat },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Sauromugue Champaign (UC)
            r.Add(new RoeRecord(3584)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Maat, Zone = new int[] { Zone.SauromugueChampaign } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Hecteyes (UC)
            r.Add(new RoeRecord(3585)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Hecteyes },
                    MobXp = true,
                    UnityLeader = UnityLeader.Maat,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Bashe (UC)
            r.Add(new RoeRecord(3586)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17268788 }, UnityLeader = UnityLeader.Maat },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Magic Bursts (UC)
            r.Add(new RoeRecord(3587)
            {
                Trigger = RoeTrigger.MagicBurst,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Maat },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Dagger Weapon Skills (UC)
            r.Add(new RoeRecord(3588)
            {
                Trigger = RoeTrigger.UseWeaponskill,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.Dagger, UnityLeader = UnityLeader.Aldo },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Sanctuary of Zi'Tah (UC)
            r.Add(new RoeRecord(3589)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Aldo, Zone = new int[] { Zone.TheSanctuaryOfZitah } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Hounds (UC)
            r.Add(new RoeRecord(3590)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Hound },
                    MobXp = true,
                    UnityLeader = UnityLeader.Aldo,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Bastet (UC)
            r.Add(new RoeRecord(3591)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17273190 }, UnityLeader = UnityLeader.Aldo },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Heal Unity Allies (UC)
            r.Add(new RoeRecord(3592)
            {
                Trigger = RoeTrigger.HealUnityAlly,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Aldo },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Spoils - Doll Shard (UC)
            r.Add(new RoeRecord(3593)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 5,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { ItemId = new int[] { Item.DollShard }, UnityLeader = UnityLeader.Aldo },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Morimar Basalt Fields (UC)
            r.Add(new RoeRecord(3594)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Aldo, Zone = new int[] { Zone.MorimarBasaltFields } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Dolls (UC)
            r.Add(new RoeRecord(3595)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Doll },
                    MobXp = true,
                    UnityLeader = UnityLeader.Aldo,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Nocuous Weapon (UC)
            r.Add(new RoeRecord(3596)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17563801 }, UnityLeader = UnityLeader.Aldo },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Marksmanship Weapon Skills (UC)
            r.Add(new RoeRecord(3598)
            {
                Trigger = RoeTrigger.UseWeaponskill,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.Marksmanship, UnityLeader = UnityLeader.Aldo },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: The Boyahda Tree (UC)
            r.Add(new RoeRecord(3599)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Aldo, Zone = new int[] { Zone.TheBoyahdaTree } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Slimes (UC)
            r.Add(new RoeRecord(3600)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Slime },
                    MobXp = true,
                    UnityLeader = UnityLeader.Aldo,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Aquarius (UC)
            r.Add(new RoeRecord(3601)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17404000 }, UnityLeader = UnityLeader.Aldo },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Heal Unity Allies (UC)
            r.Add(new RoeRecord(3602)
            {
                Trigger = RoeTrigger.HealUnityAlly,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Aldo },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Spoils - Bird Egg (UC)
            r.Add(new RoeRecord(3603)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 5,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { ItemId = new int[] { Item.BirdEgg }, UnityLeader = UnityLeader.Aldo },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Western Altepa Desert (UC)
            r.Add(new RoeRecord(3604)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Aldo, Zone = new int[] { Zone.WesternAltepaDesert } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Crow-Type Birds (UC)
            r.Add(new RoeRecord(3605)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Bird },
                    MobXp = true,
                    UnityLeader = UnityLeader.Aldo,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Cactuar Cantautor (UC)
            r.Add(new RoeRecord(3606)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17289560 }, UnityLeader = UnityLeader.Aldo },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Magic Bursts (UC)
            r.Add(new RoeRecord(3607)
            {
                Trigger = RoeTrigger.MagicBurst,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Aldo },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Dagger Weapon Skills (UC)
            r.Add(new RoeRecord(3608)
            {
                Trigger = RoeTrigger.UseWeaponskill,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.Dagger, UnityLeader = UnityLeader.JakohWahcondalo },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Yuhtunga Jungle (UC)
            r.Add(new RoeRecord(3609)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    UnityLeader = UnityLeader.JakohWahcondalo,
                    Zone = new int[] { Zone.YuhtungaJungle },
                },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Sheep (UC)
            r.Add(new RoeRecord(3610)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Sheep },
                    MobXp = true,
                    UnityLeader = UnityLeader.JakohWahcondalo,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Mischievous Micholas (UC)
            r.Add(new RoeRecord(3611)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17281149 }, UnityLeader = UnityLeader.JakohWahcondalo },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Heal Unity Allies (UC)
            r.Add(new RoeRecord(3612)
            {
                Trigger = RoeTrigger.HealUnityAlly,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.JakohWahcondalo },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Spoils - Scorpion Claw (UC)
            r.Add(new RoeRecord(3613)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 5,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    ItemId = new int[] { Item.ScorpionClaw },
                    UnityLeader = UnityLeader.JakohWahcondalo,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: East Sarutabaruta (UC)
            r.Add(new RoeRecord(3614)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    UnityLeader = UnityLeader.JakohWahcondalo,
                    Zone = new int[] { Zone.EastSarutabaruta },
                },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Scorpions (UC)
            r.Add(new RoeRecord(3615)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Scorpion },
                    MobXp = true,
                    UnityLeader = UnityLeader.JakohWahcondalo,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Sharp-Eared Ropipi (UC)
            r.Add(new RoeRecord(3616)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobId = new int[] { 17252489, 17252508 },
                    UnityLeader = UnityLeader.JakohWahcondalo,
                },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Archery Weapon Skills (UC)
            r.Add(new RoeRecord(3618)
            {
                Trigger = RoeTrigger.UseWeaponskill,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.Archery, UnityLeader = UnityLeader.JakohWahcondalo },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Marjami Ravine (UC)
            r.Add(new RoeRecord(3619)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    UnityLeader = UnityLeader.JakohWahcondalo,
                    Zone = new int[] { Zone.MarjamiRavine },
                },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Opo-Opos (UC)
            r.Add(new RoeRecord(3620)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.OpoOpo },
                    MobXp = true,
                    UnityLeader = UnityLeader.JakohWahcondalo,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Ah Puch (UC)
            r.Add(new RoeRecord(3621)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17571903 }, UnityLeader = UnityLeader.JakohWahcondalo },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Heal Unity Allies (UC)
            r.Add(new RoeRecord(3622)
            {
                Trigger = RoeTrigger.HealUnityAlly,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.JakohWahcondalo },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Spoils - Black Tiger Fang (UC)
            r.Add(new RoeRecord(3623)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 5,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    ItemId = new int[] { Item.BlackTigerFang },
                    UnityLeader = UnityLeader.JakohWahcondalo,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Sea Serpent Grotto (UC)
            r.Add(new RoeRecord(3624)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    UnityLeader = UnityLeader.JakohWahcondalo,
                    Zone = new int[] { Zone.SeaSerpentGrotto },
                },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Tigers (UC)
            r.Add(new RoeRecord(3625)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Tiger },
                    MobXp = true,
                    UnityLeader = UnityLeader.JakohWahcondalo,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Fyuu the Seabellow (UC)
            r.Add(new RoeRecord(3626)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17498269 }, UnityLeader = UnityLeader.JakohWahcondalo },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Magic Bursts (UC)
            r.Add(new RoeRecord(3627)
            {
                Trigger = RoeTrigger.MagicBurst,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.JakohWahcondalo },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Club Weapon Skills (UC)
            r.Add(new RoeRecord(3628)
            {
                Trigger = RoeTrigger.UseWeaponskill,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.Club, UnityLeader = UnityLeader.NajaSalaheem },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Bhaflau Thickets (UC)
            r.Add(new RoeRecord(3629)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    UnityLeader = UnityLeader.NajaSalaheem,
                    Zone = new int[] { Zone.BhaflauThickets },
                },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Evil Weapons (UC)
            r.Add(new RoeRecord(3630)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.EvilWeapon },
                    MobXp = true,
                    UnityLeader = UnityLeader.NajaSalaheem,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Emergent Elm (UC)
            r.Add(new RoeRecord(3631)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 16990376 }, UnityLeader = UnityLeader.NajaSalaheem },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Heal Unity Allies (UC)
            r.Add(new RoeRecord(3632)
            {
                Trigger = RoeTrigger.HealUnityAlly,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.NajaSalaheem },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Spoils - Puk Wing (UC)
            r.Add(new RoeRecord(3633)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 5,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { ItemId = new int[] { Item.PukWing }, UnityLeader = UnityLeader.NajaSalaheem },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: West Ronfaure (UC)
            r.Add(new RoeRecord(3634)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.NajaSalaheem, Zone = new int[] { Zone.WestRonfaure } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Puks (UC)
            r.Add(new RoeRecord(3635)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Puk },
                    MobXp = true,
                    UnityLeader = UnityLeader.NajaSalaheem,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Fungus Beetle (UC)
            r.Add(new RoeRecord(3636)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17187047 }, UnityLeader = UnityLeader.NajaSalaheem },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Staff Weapon Skills (UC)
            r.Add(new RoeRecord(3638)
            {
                Trigger = RoeTrigger.UseWeaponskill,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.Staff, UnityLeader = UnityLeader.NajaSalaheem },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Wajaom Woodlands (UC)
            r.Add(new RoeRecord(3639)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    UnityLeader = UnityLeader.NajaSalaheem,
                    Zone = new int[] { Zone.WajaomWoodlands },
                },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Elementals (UC)
            r.Add(new RoeRecord(3640)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Elemental },
                    MobXp = true,
                    UnityLeader = UnityLeader.NajaSalaheem,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
        }

        private static void Part10(List<RoeRecord> r)
        {
            // Subjugation: Jaded Jody (UC)
            r.Add(new RoeRecord(3641)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 16986378 }, UnityLeader = UnityLeader.NajaSalaheem },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Heal Unity Allies (UC)
            r.Add(new RoeRecord(3642)
            {
                Trigger = RoeTrigger.HealUnityAlly,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.NajaSalaheem },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Spoils - Dhalmel Meat (UC)
            r.Add(new RoeRecord(3643)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 5,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    ItemId = new int[] { Item.SliceOfDhalmelMeat },
                    UnityLeader = UnityLeader.NajaSalaheem,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Kamihr Drifts (UC)
            r.Add(new RoeRecord(3644)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.NajaSalaheem, Zone = new int[] { Zone.KamihrDrifts } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Dhalmel (UC)
            r.Add(new RoeRecord(3645)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Dhalmel },
                    MobXp = true,
                    UnityLeader = UnityLeader.NajaSalaheem,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Trembler Tabitha (UC)
            r.Add(new RoeRecord(3646)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17588278 }, UnityLeader = UnityLeader.NajaSalaheem },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Magic Bursts (UC)
            r.Add(new RoeRecord(3647)
            {
                Trigger = RoeTrigger.MagicBurst,
                Goal = 5,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.NajaSalaheem },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Polearm Weapon Skills (UC)
            r.Add(new RoeRecord(3648)
            {
                Trigger = RoeTrigger.UseWeaponskill,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.Polearm, UnityLeader = UnityLeader.Flaviria },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Cirdas Caverns (UC)
            r.Add(new RoeRecord(3649)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Flaviria, Zone = new int[] { Zone.CirdasCaverns } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Pugils (UC)
            r.Add(new RoeRecord(3650)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Pugil },
                    MobXp = true,
                    UnityLeader = UnityLeader.Flaviria,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Hovering Hotpot (UC)
            r.Add(new RoeRecord(3651)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17596628 }, UnityLeader = UnityLeader.Flaviria },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Heal Unity Allies (UC)
            r.Add(new RoeRecord(3652)
            {
                Trigger = RoeTrigger.HealUnityAlly,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Flaviria },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Spoils - Beetle Shell (UC)
            r.Add(new RoeRecord(3653)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 5,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { ItemId = new int[] { Item.BeetleShell }, UnityLeader = UnityLeader.Flaviria },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: East Ronfaure (UC)
            r.Add(new RoeRecord(3654)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Flaviria, Zone = new int[] { Zone.EastRonfaure } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Beetles (UC)
            r.Add(new RoeRecord(3655)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Beetle },
                    MobXp = true,
                    UnityLeader = UnityLeader.Flaviria,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Bigmouth Billy (UC)
            r.Add(new RoeRecord(3656)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17191196 }, UnityLeader = UnityLeader.Flaviria },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Sword Weapon Skills (UC)
            r.Add(new RoeRecord(3658)
            {
                Trigger = RoeTrigger.UseWeaponskill,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.Sword, UnityLeader = UnityLeader.Flaviria },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Xarcabard (UC)
            r.Add(new RoeRecord(3659)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Flaviria, Zone = new int[] { Zone.Xarcabard } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Goobbues (UC)
            r.Add(new RoeRecord(3660)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Goobbue },
                    MobXp = true,
                    UnityLeader = UnityLeader.Flaviria,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Barbaric Weapon (UC)
            r.Add(new RoeRecord(3661)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17236027 }, UnityLeader = UnityLeader.Flaviria },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Heal Unity Allies (UC)
            r.Add(new RoeRecord(3662)
            {
                Trigger = RoeTrigger.HealUnityAlly,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Flaviria },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Spoils - Bone Chip (UC)
            r.Add(new RoeRecord(3663)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 5,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { ItemId = new int[] { Item.BoneChip }, UnityLeader = UnityLeader.Flaviria },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Woh Gates (UC)
            r.Add(new RoeRecord(3664)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Flaviria, Zone = new int[] { Zone.WohGates } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Skeletons (UC)
            r.Add(new RoeRecord(3665)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Skeleton },
                    MobXp = true,
                    UnityLeader = UnityLeader.Flaviria,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Hyakume (UC)
            r.Add(new RoeRecord(3666)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17457236 }, UnityLeader = UnityLeader.Flaviria },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Magic Bursts (UC)
            r.Add(new RoeRecord(3667)
            {
                Trigger = RoeTrigger.MagicBurst,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Flaviria },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Club Weapon Skills (UC)
            r.Add(new RoeRecord(3668)
            {
                Trigger = RoeTrigger.UseWeaponskill,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.Club, UnityLeader = UnityLeader.YoranOran },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Giddeus (UC)
            r.Add(new RoeRecord(3669)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.YoranOran, Zone = new int[] { Zone.Giddeus } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Efts (UC)
            r.Add(new RoeRecord(3670)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Eft },
                    MobXp = true,
                    UnityLeader = UnityLeader.YoranOran,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Herbage Hunter (UC)
            r.Add(new RoeRecord(3671)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17256836 }, UnityLeader = UnityLeader.YoranOran },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Heal Unity Allies (UC)
            r.Add(new RoeRecord(3672)
            {
                Trigger = RoeTrigger.HealUnityAlly,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.YoranOran },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Spoils - Rotten Meat (UC)
            r.Add(new RoeRecord(3673)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 5,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    ItemId = new int[] { Item.PieceOfRottenMeat },
                    UnityLeader = UnityLeader.YoranOran,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Moh Gates (UC)
            r.Add(new RoeRecord(3674)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.YoranOran, Zone = new int[] { Zone.MohGates } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Antica (UC)
            r.Add(new RoeRecord(3675)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Antica },
                    MobXp = true,
                    UnityLeader = UnityLeader.YoranOran,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Antican Praefectus (UC)
            r.Add(new RoeRecord(3676)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17629281 }, UnityLeader = UnityLeader.YoranOran },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Staff Weapon Skills (UC)
            r.Add(new RoeRecord(3678)
            {
                Trigger = RoeTrigger.UseWeaponskill,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.Staff, UnityLeader = UnityLeader.YoranOran },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Toraimarai Canal (UC)
            r.Add(new RoeRecord(3679)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.YoranOran, Zone = new int[] { Zone.ToraimaraiCanal } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Coeurls (UC)
            r.Add(new RoeRecord(3680)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Coeurl },
                    MobXp = true,
                    UnityLeader = UnityLeader.YoranOran,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Ose (UC)
            r.Add(new RoeRecord(3681)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17649822 }, UnityLeader = UnityLeader.YoranOran },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Heal Unity Allies (UC)
            r.Add(new RoeRecord(3682)
            {
                Trigger = RoeTrigger.HealUnityAlly,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.YoranOran },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Spoils - Dullahan Armor (UC)
            r.Add(new RoeRecord(3683)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 5,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    ItemId = new int[] { Item.SuitOfDullahanArmor },
                    UnityLeader = UnityLeader.YoranOran,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Outer Ra'Kaznar (UC)
            r.Add(new RoeRecord(3684)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.YoranOran, Zone = new int[] { Zone.OuterRakaznar } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Dullahan (UC)
            r.Add(new RoeRecord(3685)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Dullahan },
                    MobXp = true,
                    UnityLeader = UnityLeader.YoranOran,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Capricious Cassie (UC)
            r.Add(new RoeRecord(3686)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17613130 }, UnityLeader = UnityLeader.YoranOran },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Magic Bursts (UC)
            r.Add(new RoeRecord(3687)
            {
                Trigger = RoeTrigger.MagicBurst,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.YoranOran },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Club Weapon Skills (UC)
            r.Add(new RoeRecord(3688)
            {
                Trigger = RoeTrigger.UseWeaponskill,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.Club, UnityLeader = UnityLeader.Sylvie },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Sih Gates (UC)
            r.Add(new RoeRecord(3689)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Sylvie, Zone = new int[] { Zone.SihGates } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Intulo (UC)
            r.Add(new RoeRecord(3691)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 16793742 }, UnityLeader = UnityLeader.Sylvie },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Heal Unity Allies (UC)
            r.Add(new RoeRecord(3692)
            {
                Trigger = RoeTrigger.HealUnityAlly,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Sylvie },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Spoils - Demon Horn (UC)
            r.Add(new RoeRecord(3693)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 5,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { ItemId = new int[] { Item.DemonHorn }, UnityLeader = UnityLeader.Sylvie },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Konschtat Highlands (UC)
            r.Add(new RoeRecord(3694)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Sylvie, Zone = new int[] { Zone.KonschtatHighlands } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Demons (UC)
            r.Add(new RoeRecord(3695)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobSystem = new int[] { Ecosystem.Demon },
                    MobXp = true,
                    UnityLeader = UnityLeader.Sylvie,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Marquis Naberius (UC)
            r.Add(new RoeRecord(3696)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17436876 }, UnityLeader = UnityLeader.Sylvie },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Staff Weapon Skills (UC)
            r.Add(new RoeRecord(3698)
            {
                Trigger = RoeTrigger.UseWeaponskill,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { SkillType = Skill.Staff, UnityLeader = UnityLeader.Sylvie },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Dho Gates (UC)
            r.Add(new RoeRecord(3699)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Sylvie, Zone = new int[] { Zone.DhoGates } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Velkk (UC)
            r.Add(new RoeRecord(3700)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Velkk },
                    MobXp = true,
                    UnityLeader = UnityLeader.Sylvie,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Dune Widow (UC)
            r.Add(new RoeRecord(3701)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17244396 }, UnityLeader = UnityLeader.Sylvie },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Heal Unity Allies (UC)
            r.Add(new RoeRecord(3702)
            {
                Trigger = RoeTrigger.HealUnityAlly,
                Goal = 30,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Sylvie },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Spoils - Twitherym Wing (UC)
            r.Add(new RoeRecord(3703)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 5,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { ItemId = new int[] { Item.TwitherymWing }, UnityLeader = UnityLeader.Sylvie },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Conflict: Qufim Island (UC)
            r.Add(new RoeRecord(3704)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Sylvie, Zone = new int[] { Zone.QufimIsland } },
                Reward = new RoeReward { Accolades = 300, Exp = 500, Sparks = 100 },
            });
            // Vanquish Twitherym (UC)
            r.Add(new RoeRecord(3705)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs
                {
                    MobFamily = new int[] { MobFamily.Twitherym },
                    MobXp = true,
                    UnityLeader = UnityLeader.Sylvie,
                },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Subjugation: Atkorkamuy (UC)
            r.Add(new RoeRecord(3706)
            {
                Trigger = RoeTrigger.DefeatMob,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { MobId = new int[] { 17293485 }, UnityLeader = UnityLeader.Sylvie },
                Reward = new RoeReward { Accolades = 1000, Exp = 500, Sparks = 100 },
            });
            // Magic Bursts (UC)
            r.Add(new RoeRecord(3707)
            {
                Trigger = RoeTrigger.MagicBurst,
                Goal = 10,
                Flags = RoeFlags.Unity,
                Reqs = new RoeReqs { UnityLeader = UnityLeader.Sylvie },
                Reward = new RoeReward { Accolades = 500, Exp = 500, Sparks = 100 },
            });
            // Vanquish Aquans
            r.Add(new RoeRecord(4008)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Repeat | RoeFlags.Timed,
                Reqs = new RoeReqs { MobSystem = new int[] { Ecosystem.Aquan }, MobXp = true, ZoneNot = AbysseaZones },
                Reward = new RoeReward
                {
                    Accolades = 300,
                    Exp = 1500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher) },
                    Sparks = 300,
                },
            });
            // Vanquish Beasts
            r.Add(new RoeRecord(4009)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Repeat | RoeFlags.Timed,
                Reqs = new RoeReqs { MobSystem = new int[] { Ecosystem.Beast }, MobXp = true, ZoneNot = AbysseaZones },
                Reward = new RoeReward
                {
                    Accolades = 300,
                    Exp = 1500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher) },
                    Sparks = 300,
                },
            });
            // Vanquish Plantoids
            r.Add(new RoeRecord(4010)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Repeat | RoeFlags.Timed,
                Reqs = new RoeReqs
                {
                    MobSystem = new int[] { Ecosystem.Plantoid },
                    MobXp = true,
                    ZoneNot = AbysseaZones,
                },
                Reward = new RoeReward
                {
                    Accolades = 300,
                    Exp = 1500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher) },
                    Sparks = 300,
                },
            });
            // Vanquish Lizards
            r.Add(new RoeRecord(4011)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Repeat | RoeFlags.Timed,
                Reqs = new RoeReqs { MobSystem = new int[] { Ecosystem.Lizard }, MobXp = true, ZoneNot = AbysseaZones },
                Reward = new RoeReward
                {
                    Accolades = 300,
                    Exp = 1500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher) },
                    Sparks = 300,
                },
            });
            // Vanquish Vermin
            r.Add(new RoeRecord(4012)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Repeat | RoeFlags.Timed,
                Reqs = new RoeReqs { MobSystem = new int[] { Ecosystem.Vermin }, MobXp = true, ZoneNot = AbysseaZones },
                Reward = new RoeReward
                {
                    Accolades = 300,
                    Exp = 1500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher) },
                    Sparks = 300,
                },
            });
            // Gain Experience
            r.Add(new RoeRecord(4013)
            {
                Trigger = RoeTrigger.GainExperience,
                Goal = 5000,
                Increment = 0,
                Flags = RoeFlags.Repeat | RoeFlags.Timed,
                Check = RoeCheckForm.ProgressExp,
                Reward = new RoeReward
                {
                    Accolades = 300,
                    Exp = 1500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher) },
                    Sparks = 300,
                },
            });
            // Spoils (Seals)
            r.Add(new RoeRecord(4014)
            {
                Trigger = RoeTrigger.LootItem,
                Goal = 3,
                Flags = RoeFlags.Repeat | RoeFlags.Timed,
                Reqs = new RoeReqs
                {
                    ItemId = new int[] { Item.BeastmensSeal, Item.KindredsSeal, Item.KindredsCrest,
                        Item.HighKindredsCrest, Item.SacredKindredsCrest },
                },
                Reward = new RoeReward
                {
                    Accolades = 300,
                    Exp = 1500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher) },
                    Sparks = 300,
                },
            });
            // Vanquish Birds
            r.Add(new RoeRecord(4015)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Repeat | RoeFlags.Timed,
                Reqs = new RoeReqs { MobSystem = new int[] { Ecosystem.Bird }, MobXp = true, ZoneNot = AbysseaZones },
                Reward = new RoeReward
                {
                    Accolades = 300,
                    Exp = 1500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher) },
                    Sparks = 300,
                },
            });
            // Vanquish Amorphs
            r.Add(new RoeRecord(4016)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Repeat | RoeFlags.Timed,
                Reqs = new RoeReqs { MobSystem = new int[] { Ecosystem.Amorph }, MobXp = true, ZoneNot = AbysseaZones },
                Reward = new RoeReward
                {
                    Accolades = 300,
                    Exp = 1500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher) },
                    Sparks = 300,
                },
            });
            // Vanquish Undead
            r.Add(new RoeRecord(4017)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Repeat | RoeFlags.Timed,
                Reqs = new RoeReqs { MobSystem = new int[] { Ecosystem.Undead }, MobXp = true, ZoneNot = AbysseaZones },
                Reward = new RoeReward
                {
                    Accolades = 300,
                    Exp = 1500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher) },
                    Sparks = 300,
                },
            });
            // Vanquish Arcana
            r.Add(new RoeRecord(4018)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Repeat | RoeFlags.Timed,
                Reqs = new RoeReqs { MobSystem = new int[] { Ecosystem.Arcana }, MobXp = true, ZoneNot = AbysseaZones },
                Reward = new RoeReward
                {
                    Accolades = 300,
                    Exp = 1500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher) },
                    Sparks = 300,
                },
            });
            // Crack Treasure Caskets (Triggered from caskets.lua)
            r.Add(new RoeRecord(4019)
            {
                Goal = 10,
                Flags = RoeFlags.Repeat | RoeFlags.Timed,
                Reward = new RoeReward
                {
                    Accolades = 300,
                    Exp = 1500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher) },
                    Sparks = 300,
                },
            });
            // Physical Damage Kills
            r.Add(new RoeRecord(4020)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Repeat | RoeFlags.Timed,
                Reqs = new RoeReqs { AtkType = AttackType.Physical, MobXp = true },
                Reward = new RoeReward
                {
                    Accolades = 300,
                    Exp = 1500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher) },
                    Sparks = 300,
                },
            });
            // Magic Damage Kills
            r.Add(new RoeRecord(4021)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 20,
                Flags = RoeFlags.Repeat | RoeFlags.Timed,
                Reqs = new RoeReqs { AtkType = AttackType.Magical, MobXp = true },
                Reward = new RoeReward
                {
                    Accolades = 300,
                    Exp = 1500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher) },
                    Sparks = 300,
                },
            });
            // Vanquish Multiple Enemies (D)
            r.Add(new RoeRecord(4082)
            {
                Trigger = RoeTrigger.DefeatMob,
                Goal = 30,
                Flags = RoeFlags.Daily,
                Reqs = new RoeReqs { MobXp = true },
                Reward = new RoeReward
                {
                    Accolades = 300,
                    Exp = 500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher) },
                    Sparks = 100,
                },
            });
            // Buff Allies (D)
            r.Add(new RoeRecord(4083)
            {
                Trigger = RoeTrigger.BuffAlly,
                Goal = 20,
                Flags = RoeFlags.Daily,
                Reward = new RoeReward
                {
                    Accolades = 300,
                    Exp = 1500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher) },
                    Sparks = 300,
                },
            });
            // Heal for 500+ HP (D)
            r.Add(new RoeRecord(4084)
            {
                Trigger = RoeTrigger.HealAlly,
                Goal = 30,
                Flags = RoeFlags.Daily,
                Reqs = new RoeReqs { HealMin = 500 },
                Reward = new RoeReward
                {
                    Accolades = 300,
                    Exp = 1500,
                    Item = new RoeItem[] { RoeItem.Of(Item.CopperAmanVoucher) },
                    Sparks = 300,
                },
            });
            // 10 RoE Objectives Complete (All for One requirement)
            r.Add(new RoeRecord(4085)
            {
                Flags = RoeFlags.Hidden,
            });
        }
    }
}
