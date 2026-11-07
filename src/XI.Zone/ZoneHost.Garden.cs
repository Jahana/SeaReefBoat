using System;
using System.Collections.Generic;
using System.Linq;
using XI.Common;
using XI.Map;
using XI.Persistence;

namespace XI.Zone
{
    // utils/gardenutils.cpp, items/item_flowerpot.cpp and the GP_CLI_COMMAND_MYROOM_PLANT_* handlers: a flowerpot
    // installed in the char's own mog house grows a seed through its stages, fed a crystal at the two crystal stages, and
    // is harvested once mature. The pot's state is its exdata (Exdata::FlowerPot).
    public partial class ZoneHost
    {
        private const ushort MsgStdMooglePlantsSeeds = 132, MsgStdMoogleDriesPlant = 133, MsgStdMoogleUsesItemOnPlant = 136,
                             MsgStdMoghouseCantPickUp = 137, MsgStdMoogleHarvests = 134;
        private const ushort MsgBasicGardeningSeedSown = 256, MsgBasicGardeningCrystalNone = 257, MsgBasicGardeningCrystalUsed = 258;
        private const ushort FireCrystal = 4096, DarkCrystal = 4103;

        private const uint VanadaySeconds = 3456, VanadaysToWilt = 36, VanadaysToGuaranteeWilt = 144;
        private const uint VanatimeForWiltStage = 65535 * VanadaySeconds;

        // FLOWERPOT_PLANT_TYPE
        private const byte PlantNone = 0, PlantFruit = 1, PlantHerb = 2, PlantGrain = 3, PlantVegetable = 4, PlantCactus = 5,
                           PlantTreeCuttings = 6, PlantTreeSaplings = 7, PlantWildgrass = 8;

        // FLOWERPOT_STAGE_TYPE
        private const byte StageEmpty = 0, StageInitial = 1, StageFirstSprouts = 2, StageFirstSprouts2 = 3, StageFirstSproutsCrystal = 4,
                           StageSecondSprouts = 5, StageSecondSprouts2 = 6, StageSecondSproutsCrystal = 7, StageSecondSprouts3 = 8,
                           StageThirdSprouts = 9, StageMature = 10, StageWilted = 11;

        // FLOWERPOT_ELEMENT_TYPE
        private const byte ElementNone = 0, ElementFire = 1, ElementIce = 2, ElementWind = 3, ElementEarth = 4, ElementLightning = 5,
                           ElementWater = 6, ElementLight = 7, ElementDark = 8;

        // Exdata::FlowerPot: Step, Installed (bit 6) / Dried (bit 7), Crystal1 (the extra feed), Crystal2 (the common feed),
        // Kind, Examined (bit 0) / Strength (bits 1-7), TimePlanted, TimeNextStep
        private static byte PotStage(Item pot) => pot.Extra[0];
        private static bool PotDried(Item pot) => (pot.Extra[1] & 0x80) != 0;
        private static byte PotExtraFeed(Item pot) => pot.Extra[2];
        private static byte PotCommonFeed(Item pot) => pot.Extra[3];
        private static byte PotPlant(Item pot) => pot.Extra[4];
        private static bool PotExamined(Item pot) => (pot.Extra[5] & 0x01) != 0;
        private static byte PotStrength(Item pot) => (byte)(pot.Extra[5] >> 1);
        private static uint PotPlantTimestamp(Item pot) => BitConverter.ToUInt32(pot.Extra, 12);
        private static uint PotStageTimestamp(Item pot) => BitConverter.ToUInt32(pot.Extra, 16);

        private static void SetPotExamined(Item pot, bool examined) => pot.Extra[5] = (byte)(examined ? pot.Extra[5] | 0x01 : pot.Extra[5] & ~0x01);
        private static void SetPotStrength(Item pot, byte strength) => pot.Extra[5] = (byte)((pot.Extra[5] & 0x01) | (strength << 1));
        private static void SetPotDried(Item pot) => pot.Extra[1] |= 0x80;
        private static void SetPotPlantTimestamp(Item pot, uint vanatime) => BitConverter.GetBytes(vanatime).CopyTo(pot.Extra, 12);
        private static void SetPotStageTimestamp(Item pot, uint vanatime) => BitConverter.GetBytes(vanatime).CopyTo(pot.Extra, 16);

        private static bool PotPlanted(Item pot) => PotStage(pot) > 0;
        private static bool PotCanGrow(Item pot) => PotStage(pot) >= StageInitial && PotStage(pot) <= StageThirdSprouts && !PotDried(pot);

        private static bool IsTree(byte plant) =>
            plant == PlantFruit || plant == PlantCactus || plant == PlantTreeCuttings || plant == PlantTreeSaplings;

        private static readonly ushort[] SeedIds = { 0, 574, 572, 575, 573, 1236, 1237, 1238, 2235 };

        private static ushort SeedId(byte plant) => plant < SeedIds.Length ? SeedIds[plant] : (ushort)0;

        private static byte PlantFromSeed(ushort seed)
        {
            int plant = Array.IndexOf(SeedIds, seed);
            return plant > 0 ? (byte)plant : PlantNone;
        }

        // the crystal ids follow the element order
        private static ushort ItemFromElement(byte element) => (ushort)(element + 4095);

        private bool IsFlowerpot(Item item) => FurnishingOf(item)?.IsFlowerpot == true;

        private uint VanadielNow() => LoginPackets.VanadielTimestamp(UnixNow());

        // GetStageDuration: the vana'days the stage just entered lasts, by plant (rows) and stage (columns); a crystal fed
        // at the crystal stages lengthens the stage after
        private static readonly byte[][] StageDays =
        {
            new byte[11],
            new byte[] { 1, 8, 10, 12, 50, 4, 14, 52, 4, 20, 187 },
            new byte[] { 0, 9, 4, 0, 0, 0, 12, 50, 4, 30, 187 },
            new byte[] { 0, 1, 2, 0, 0, 0, 6, 62, 4, 36, 187 },
            new byte[] { 0, 18, 4, 0, 0, 0, 2, 56, 1, 30, 187 },
            new byte[] { 18, 26, 42, 74, 72, 4, 48, 72, 4, 72, 187 },
            new byte[] { 24, 30, 40, 74, 72, 8, 52, 72, 8, 60, 187 },
            new byte[] { 40, 48, 62, 74, 80, 22, 80, 86, 26, 108, 187 },
            new byte[] { 0, 12, 18, 0, 0, 0, 28, 62, 4, 46, 187 },
        };

        // the fed durations of SECOND_SPROUTS and SECOND_SPROUTS_3
        private static readonly byte[][] FedStageDays =
        {
            new byte[2], new byte[] { 18, 16 }, new byte[] { 0, 24 }, new byte[] { 0, 24 }, new byte[] { 0, 16 },
            new byte[] { 40, 52 }, new byte[] { 48, 54 }, new byte[] { 60, 64 }, new byte[] { 0, 36 },
        };

        private static byte GetStageDuration(Item pot, bool growFromFeed = false)
        {
            byte plant = PotPlant(pot), stage = PotStage(pot);
            if (plant >= StageDays.Length || stage > StageMature) return 0;
            if (growFromFeed && stage == StageSecondSprouts) return FedStageDays[plant][0];
            if (growFromFeed && stage == StageSecondSprouts3) return FedStageDays[plant][1];
            return StageDays[plant][stage];
        }

        // GrowToNextStage: a tree goes through the first sprouts' crystal stage, anything else skips to the second sprouts
        private void GrowToNextStage(Item pot, bool growFromFeed = false)
        {
            byte stage = PotStage(pot);
            if (stage == StageFirstSprouts) pot.Extra[0] = IsTree(PotPlant(pot)) ? StageFirstSprouts2 : StageSecondSprouts2;
            else if (stage < StageMature) pot.Extra[0] = (byte)(stage + 1);
            SetPotStageTimestamp(pot, VanadielNow() + GetStageDuration(pot, growFromFeed) * VanadaySeconds);
        }

        private Dictionary<uint, List<(ushort ItemId, byte Min, byte Max, byte Weight)>> gardenResults_;

        // LoadResultList: gardening_results by (seed << 8) + (element1 << 4) + element2
        private Dictionary<uint, List<(ushort ItemId, byte Min, byte Max, byte Weight)>> GardenResults()
        {
            if (gardenResults_ != null) return gardenResults_;
            gardenResults_ = new Dictionary<uint, List<(ushort, byte, byte, byte)>>();
            var r = Exec("SELECT resultId, seed, element1, element2, result, min_quantity, max_quantity, weight FROM gardening_results");
            while (r != null && r.Next())
            {
                uint uid = ((uint)r.Get<byte>("seed") << 8) + ((uint)r.Get<byte>("element1") << 4) + r.Get<byte>("element2");
                if (!gardenResults_.TryGetValue(uid, out var list)) gardenResults_[uid] = list = new List<(ushort, byte, byte, byte)>();
                list.Add((r.Get<ushort>("result"), r.Get<byte>("min_quantity"), r.Get<byte>("max_quantity"), (byte)r.Get<ushort>("weight")));
            }
            return gardenResults_;
        }

        // UpdateGardening: every growing pot whose stage is up grows, or wilts when it went unexamined past the wilt time
        // (or so long that it wilts regardless); its exdata is saved, and the client told when `sendPacket`
        private void UpdateGardening(MapSession s, CharData c, bool sendPacket)
        {
            uint vanatime = VanadielNow();
            foreach (var cat in MogSafes.Select(l => (Container)l))
                foreach (var pot in c.Items.Where(i => i.Location == cat && i.Slot <= c.StorageSize[(int)cat]).OrderBy(i => i.Slot).ToList())
                {
                    if (!IsFlowerpot(pot) || !PotCanGrow(pot) || vanatime < PotStageTimestamp(pot)) continue;

                    uint stageDuration = GetStageDuration(pot);
                    uint daysSinceStageChange = (uint)Math.Floor(Math.Max(0f, (float)(vanatime - PotStageTimestamp(pot)) / VanadaySeconds));
                    uint wiltTime = (uint)(VanadaysToWilt + c.ModStat[(int)Mod.GARDENING_WILT_BONUS]);
                    if ((!PotExamined(pot) && (stageDuration > wiltTime || stageDuration + daysSinceStageChange > wiltTime)) ||
                        daysSinceStageChange > VanadaysToGuaranteeWilt + wiltTime)
                    {
                        pot.Extra[0] = StageWilted;
                        SetPotStageTimestamp(pot, vanatime + VanatimeForWiltStage);
                    }
                    else GrowToNextStage(pot);

                    SetPotExamined(pot, false);
                    Exec("UPDATE char_inventory SET extra = ? WHERE charid = ? AND location = ? AND slot = ? LIMIT 1",
                         BoundValue.Of(pot.Extra), BoundValue.Of(c.Id), BoundValue.Of((byte)cat), BoundValue.Of(pot.Slot));
                    if (sendPacket) Push(s, GameOkPackets.ItemAttr(pot, ItemLockFlg(c, pot)));
                }
        }

        // CCharEntity::Tick: the pots grow while the char is in its own mog house
        private void TickGardening()
        {
            foreach (var (c, s) in new List<KeyValuePair<CharData, MapSession>>(charSessions_))
                if (s.InZone && s.Char == c && InOwnMogHouse(c)) UpdateGardening(s, c, true);
        }

        // CalculateResults: the crystals fed, the plant's own element and the optional day, pot, moon and aura bonuses make
        // the strength that picks the result by cumulative weight, and how far into that result's weight it reached picks
        // the quantity
        private (ushort ItemId, byte Quantity) CalculateResults(CharData c, Item pot)
        {
            var settings = ChatCfg;
            var elements = new byte[9];
            void Add(int element, int value)
            {
                // an element past the array's end is the C++'s out-of-bounds write
                if (element >= 0 && element < elements.Length) elements[element] = (byte)(elements[element] + value);
            }
            byte plant = PotPlant(pot), common = PotCommonFeed(pot), extra = PotExtraFeed(pot);
            bool tree = IsTree(plant);
            Add(common, 10);
            if (tree) Add(extra, 10);

            Add(plant switch
            {
                PlantHerb => ElementWind,
                PlantGrain => ElementFire,
                PlantVegetable => ElementEarth,
                PlantFruit => ElementWater,
                PlantCactus => ElementLight,
                PlantTreeCuttings => ElementIce,
                PlantTreeSaplings => ElementDark,
                PlantWildgrass => ElementLightning,
                _ => ElementNone,
            }, 10);

            if (settings.GardenDayMatters)
                Add((int)VanaTime.GetWeekDay(PotPlantTimestamp(pot) * VanaTime.TicksPerEarthSecond) + 1, 10);

            if (settings.GardenPotMatters)
                switch (pot.ItemId)
                {
                    case 216: Add(ElementWind, 10); break;  // Porcelain Flowerpot
                    case 217: Add(ElementFire, 10); break;  // Brass Flowerpot
                    case 218: Add(ElementEarth, 10); break; // Earthen Flowerpot
                    case 219: Add(ElementWater, 10); break; // Ceramic Flowerpot
                    case 220: Add(ElementLight, 10); break; // Wooden Flowerpot
                    case 221: Add(ElementDark, 10); break;  // Arcane Flowerpot
                }

            short strength = common == ElementNone ? elements.Max() : (common < elements.Length ? elements[common] : (byte)0);
            if (tree) strength += extra == ElementNone ? elements.Max() : (extra < elements.Length ? elements[extra] : (byte)0);

            if (settings.GardenMoonphaseMatters)
                strength += (short)Math.Ceiling(VanaTime.MoonPhase(VanaTime.EarthUnixSecToTicks(UnixNow())) / 10.0f);

            if (settings.GardenMhAuraMatters)
            {
                // both floors' installed furnishings
                var auras = new ushort[8];
                foreach (var (_, f) in InstalledFurnishings(c))
                    if (f.Element >= 1 && f.Element <= 8) auras[f.Element - 1] += f.Aura;
                strength += (short)(auras.Max() / 10);
            }

            strength += (short)((100 - strength) * (PotStrength(pot) / 32.0f));

            uint resultUid = ((uint)plant << 8) + ((uint)common << 4) + extra;
            if (!GardenResults().TryGetValue(resultUid, out var results) || results.Count == 0)
            {
                // the C++ takes the back of an empty list
                log_($"gardenutils::CalculateResults: no gardening_results for {resultUid} ({c.Name})");
                return (0, 0);
            }
            (ushort ItemId, byte Min, byte Max, byte Weight) item = default;
            sbyte cumulativeWeight = 0;
            foreach (var result in results)
            {
                cumulativeWeight += (sbyte)result.Weight;
                if (strength < cumulativeWeight)
                {
                    item = result;
                    break;
                }
            }
            if (item.ItemId == 0) item = results[^1];

            // how far into the result's weight the strength reached, split into max - min + 1 buckets
            float percentage = (strength - (cumulativeWeight - item.Weight)) / (float)item.Weight;
            byte quantity = percentage >= 1.0f ? item.Max : (byte)(item.Min + percentage * (1 + item.Max - item.Min));
            return (item.ItemId, quantity);
        }

        private void RegisterGardenHandlers()
        {
            Register(0x0FC, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent)
                                              .IsInMogHouse(MogHouseKind.Own)
                                              .MustNotEqual(BitConverter.ToUInt16(p.Buffer, 4), 0, "MyroomPlantItemNo must not be 0")
                                              .MustNotEqual(BitConverter.ToUInt16(p.Buffer, 6), 0, "MyroomAddItemNo must not be 0")
                                              .OneOf("MyroomPlantCategory", p.Buffer[10], MogSafes)
                                              .OneOf("MyroomAddCategory", p.Buffer[11], MogSafes),
                     (s, c, p) => OnMyroomPlantAdd(s, c, BitConverter.ToUInt16(p.Buffer, 6), p.Buffer[8], p.Buffer[9],
                                                   (Container)p.Buffer[10], (Container)p.Buffer[11]));
            Register(0x0FD, (s, c, p) => ValidatePlant(c, p),
                     (s, c, p) => OnMyroomPlantCheck(s, c, p.Buffer[6], (Container)p.Buffer[7]));
            Register(0x0FE, (s, c, p) => ValidatePlant(c, p),
                     (s, c, p) => OnMyroomPlantCrop(s, c, p.Buffer[6], (Container)p.Buffer[7], p.Buffer[8]));
            Register(0x0FF, (s, c, p) => ValidatePlant(c, p),
                     (s, c, p) => OnMyroomPlantStop(s, c, BitConverter.ToUInt16(p.Buffer, 4), p.Buffer[6], (Container)p.Buffer[7]));
        }

        // MyroomPlantItemNo u16 @4, MyroomPlantItemIndex @6, MyroomPlantCategory @7
        private static PacketValidator ValidatePlant(CharData c, BasicPacket p) => Pv(c)
            .BlockedBy(BlockedState.InEvent)
            .IsInMogHouse(MogHouseKind.Own)
            .MustNotEqual(BitConverter.ToUInt16(p.Buffer, 4), 0, "MyroomPlantItemNo must not be 0")
            .OneOf("MyroomPlantCategory", p.Buffer[7], MogSafes);

        // the flowerpot in category / index, installed; the C++'s warnings otherwise (the crop handler's say "::process(")
        private Item InstalledPot(CharData c, string handler, Container category, byte index, string process = "::process")
        {
            var item = StorageItem(c, category, index);
            if (item == null || !IsFlowerpot(item))
            {
                if (item != null) log_($"{handler}{process}: {c.Name} has tried to use invalid gardening pot {item.ItemId}");
                else log_($"{handler}{process}: {c.Name} has tried to use invalid gardening pot item (MyroomPlantCategory = {(int)category}, MyroomPlantItemIndex = {index})");
                return null;
            }
            if (!item.Installed)
            {
                log_($"{handler}: {c.Name} tried to interact with an uninstalled flowerpot");
                return null;
            }
            return item;
        }

        private void SavePotExtra(CharData c, Item pot) =>
            Exec("UPDATE char_inventory SET extra = ? WHERE charid = ? AND location = ? AND slot = ? LIMIT 1",
                 BoundValue.Of(pot.Extra), BoundValue.Of(c.Id), BoundValue.Of((byte)pot.Location), BoundValue.Of(pot.Slot));

        // the pot's new exdata: ITEM_ATTR and ITEM_SAME
        private void PushPotState(MapSession s, CharData c, Item pot)
        {
            Push(s, GameOkPackets.ItemAttr(pot, ItemLockFlg(c, pot)));
            Push(s, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
        }

        // GP_CLI_COMMAND_MYROOM_PLANT_ADD: plant a seed (a fresh plant of random strength) or feed a crystal at one of the
        // crystal stages. The seed or crystal is spent through a claim transaction, given back when it had no use.
        private void OnMyroomPlantAdd(MapSession s, CharData c, ushort addItemNo, byte plantIndex, byte addIndex, Container plantCategory, Container addCategory)
        {
            const string Handler = "GP_CLI_COMMAND_MYROOM_PLANT_ADD";
            var pot = InstalledPot(c, Handler, plantCategory, plantIndex);
            if (pot == null || pot.Quantity < 1) return;

            // planting the pot into itself would free it before any of the writes below
            if (addCategory == plantCategory && addIndex == plantIndex)
            {
                log_($"{Handler}: {c.Name} trying to plant a pot into itself");
                return;
            }
            // the item id is client-supplied, so it has to match what is really in the slot
            var addItem = StorageItem(c, addCategory, addIndex);
            if (addItem == null || addItem.ItemId != addItemNo)
            {
                log_($"{Handler}: {c.Name} trying to plant item {addItemNo} that is not in slot {addIndex}");
                return;
            }

            using var transaction = new ItemTransaction(this, c);
            if (transaction.ClaimSlot(addCategory, addIndex) == null)
            {
                log_($"{Handler}: {c.Name} trying to plant a claimed item {pot.ItemId}");
                return;
            }
            // seed first: a pot that turns out to have no use for it rolls the take back
            if (!transaction.Take(addCategory, addIndex, 1))
            {
                log_($"{Handler}: {c.Name} could not spend item {pot.ItemId}");
                return;
            }

            bool updatedPot = false;
            byte plant = PlantFromSeed(addItemNo);
            if (plant != PlantNone)
            {
                Push(s, ParamMessage(addItemNo, MsgStdMooglePlantsSeeds));
                CleanPot(pot);
                pot.Extra[4] = plant;
                SetPotPlantTimestamp(pot, VanadielNow());
                SetPotStrength(pot, (byte)RandomBelow(33));
                GrowToNextStage(pot);
                updatedPot = true;
            }
            else if (addItemNo >= FireCrystal && addItemNo <= DarkCrystal)
            {
                Push(s, ParamMessage(addItemNo, MsgStdMoogleUsesItemOnPlant));
                if (PotStage(pot) == StageFirstSproutsCrystal)
                {
                    pot.Extra[2] = (byte)(addItemNo - 4095);
                    updatedPot = true;
                }
                else if (PotStage(pot) == StageSecondSproutsCrystal)
                {
                    pot.Extra[3] = (byte)(addItemNo - 4095);
                    updatedPot = true;
                }
                if (updatedPot)
                {
                    GrowToNextStage(pot, true);
                    SetPotExamined(pot, true);
                }
            }

            // no commit: the dispose puts the seed back
            if (!updatedPot || !transaction.Commit()) return;

            SavePotExtra(c, pot);
            Push(s, MyroomOperation(pot, plantCategory, plantIndex));
            PushPotState(s, c, pot);
        }

        // GP_CLI_COMMAND_MYROOM_PLANT_CHECK: what was sown and the crystals fed so far; examining it keeps it from wilting
        private void OnMyroomPlantCheck(MapSession s, CharData c, byte index, Container category)
        {
            var pot = InstalledPot(c, "GP_CLI_COMMAND_MYROOM_PLANT_CHECK", category, index);
            if (pot == null) return;

            if (PotPlanted(pot))
            {
                byte stage = PotStage(pot);
                Push(s, SelfPackets.BattleMessage(c, SeedId(PotPlant(pot)), 0, MsgBasicGardeningSeedSown));
                void Crystal(byte element) => Push(s, element != ElementNone
                    ? SelfPackets.BattleMessage(c, ItemFromElement(element), 0, MsgBasicGardeningCrystalUsed)
                    : SelfPackets.BattleMessage(c, 0, 0, MsgBasicGardeningCrystalNone));
                if (IsTree(PotPlant(pot)) && stage > StageFirstSproutsCrystal) Crystal(PotExtraFeed(pot));
                if (stage > StageSecondSproutsCrystal) Crystal(PotCommonFeed(pot));

                if (!PotExamined(pot))
                {
                    SetPotExamined(pot, true);
                    SavePotExtra(c, pot);
                }
            }
            Push(s, MyroomOperation(pot, category, index));
        }

        // GP_CLI_COMMAND_MYROOM_PLANT_CROP: uproot the plant, harvesting it when mature and not cancelled. The harvest
        // goes into the safe, then the 2nd floor's once unlocked, a stack per transaction; it is refused when it does not
        // fit or comes to nothing.
        private void OnMyroomPlantCrop(MapSession s, CharData c, byte index, Container category, byte cancelFlg)
        {
            const string Handler = "GP_CLI_COMMAND_MYROOM_PLANT_CROP";
            var pot = InstalledPot(c, Handler, category, index, "::process(");
            if (pot == null) return;

            // packet abuse that put a gardening pot on the 2nd floor
            if (On2ndFloor(pot))
            {
                log_($"{c.Name} has tried to uproot gardening pot {pot.ItemId} on 2nd floor");
                return;
            }
            if (!PotPlanted(pot)) return;

            if (cancelFlg == 0 && PotStage(pot) == StageMature)
            {
                var (resultId, totalQuantity) = CalculateResults(c, pot);
                // xi::items::lookup of an unknown result is the C++'s null dereference
                byte stackSize = (byte)(resultId != 0 && ItemBasic(resultId) is { } basic ? basic.StackSize : 0);
                byte requiredSlots = stackSize == 0 ? (byte)0 : (byte)Math.Ceiling(totalQuantity / (float)stackSize);
                bool safe2Unlocked = (c.MhFlag & 0x20) != 0 && ChatCfg.MogHouse2F;
                int totalFreeSlots = FreeSlots(c, Container.MogSafe) + (safe2Unlocked ? FreeSlots(c, Container.MogSafe2) : 0);
                if (requiredSlots > totalFreeSlots || totalQuantity == 0 || stackSize == 0)
                {
                    Push(s, SelfPackets.Message(MsgStdMoghouseCantPickUp)); // Kupo. I can't pick anything right now, kupo.
                    return;
                }

                byte remainingQuantity = totalQuantity;
                for (int slot = 0; slot < requiredSlots; ++slot)
                {
                    byte quantity = Math.Min(remainingQuantity, stackSize);
                    using (var transaction = new ItemTransaction(this, c))
                    {
                        // falls through to the second safe when the first is full
                        bool stored = transaction.Give(Container.MogSafe, resultId, quantity).HasValue ||
                                      (safe2Unlocked && transaction.Give(Container.MogSafe2, resultId, quantity).HasValue);
                        if (!stored || !transaction.Commit())
                            log_($"{Handler}: {c.Name} could not receive {quantity} of crop {resultId}");
                    }
                    remainingQuantity -= quantity;
                }
                Push(s, HarvestMessage(resultId, totalQuantity)); // Your moogle <quantity> <item> from the plant!
            }

            Push(s, MyroomOperation(pot, category, index));
            CleanPot(pot);
            SavePotExtra(c, pot);
            PushPotState(s, c, pot);
        }

        // GP_SERV_COMMAND_MESSAGE(param0, param1, messageID): the 0x48 packet with "Para0 %u Para1 %u"
        private static BasicPacket HarvestMessage(uint param0, uint param1)
        {
            var w = new PacketWriter(0x009, 0x48);
            w.U16(10, MsgStdMoogleHarvests);
            w.Name(13, $"Para0 {param0} Para1 {param1}", 23);
            return w.Packet;
        }

        // GP_CLI_COMMAND_MYROOM_PLANT_STOP: dry a growing plant, which stops it growing. Anything but an installed
        // flowerpot is ignored without a word.
        private void OnMyroomPlantStop(MapSession s, CharData c, ushort itemNo, byte index, Container category)
        {
            var pot = StorageItem(c, category, index);
            if (pot == null || !IsFlowerpot(pot)) return;
            if (!pot.Installed)
            {
                log_($"GP_CLI_COMMAND_MYROOM_PLANT_STOP: {c.Name} tried to interact with an uninstalled flowerpot");
                return;
            }
            if (!PotPlanted(pot) || PotStage(pot) <= StageInitial || PotStage(pot) >= StageWilted || PotDried(pot)) return;

            Push(s, ParamMessage(itemNo, MsgStdMoogleDriesPlant));
            Push(s, MyroomOperation(pot, category, index));
            SetPotDried(pot);
            SavePotExtra(c, pot);
            PushPotState(s, c, pot);
        }
    }
}
