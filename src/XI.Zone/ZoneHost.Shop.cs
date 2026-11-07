using System;
using System.Collections.Generic;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // NPC shops: the createShop/addShopItem/sendMenu bindings that stock the char's trade container, the C2S handlers
    // that buy from it and sell to it (c2s/0x083-0x085), the SHOP_* packets they answer with, and GetReadOnlyItem, which
    // the sell price script reads the base price from
    public sealed partial class ZoneHost
    {
        private const string ItemType = "CItem";
        private const uint ItemFlagNoSale = 0x00001000;
        private const ushort MsgStdSell = 232; // "You sell <item>."
        private const int ShopListMax = 19;    // GP_SHOP ShopItemTbl[19]
        private const int ShopEntrySize = 12;  // GP_SHOP: ItemPrice, ItemNo, ShopIndex, padding, Skill, GuildInfo
        private const byte MenuMoogle = 1, MenuShop = 2, MenuAuction = 3; // xi.menuType

        // xi::items::lookup: the item_basic row a CItem reads from; GetReadOnlyItem hands out one object per item id
        private sealed class ReadOnlyItem
        {
            public ushort Id, SubId;
            public string Name;
            public uint Flags, StackSize, BasePrice;
            public byte AhCategory;
        }

        private readonly Dictionary<ushort, ReadOnlyItem> readOnlyItems_ = new Dictionary<ushort, ReadOnlyItem>();

        private ReadOnlyItem LookupItem(ushort id)
        {
            if (readOnlyItems_.TryGetValue(id, out var item)) return item;
            var r = Exec("SELECT itemid, subid, name, stackSize, flags, aH, BaseSell FROM item_basic WHERE itemid = ? LIMIT 1", BoundValue.Of(id));
            item = r != null && r.Next()
                ? new ReadOnlyItem
                {
                    Id = id, SubId = r.Get<ushort>("subid"), Name = r.Get<string>("name"), StackSize = r.Get<uint>("stackSize"),
                    Flags = r.Get<uint>("flags"), AhCategory = r.Get<byte>("aH"), BasePrice = r.Get<uint>("BaseSell"),
                }
                : null;
            readOnlyItems_[id] = item;
            return item;
        }

        private const ushort ItemTypeUsableBit = 0x02; // ITEM_USABLE
        private const byte ItemSubTypeCharged = 0x02, ItemSubTypeAugmented = 0x04; // ITEM_SUBTYPE

        // the ITEM_TYPE bits LoadItemList's buildFromType gives an item_basic type: each class sets its own on top of its
        // base class's (CItemWeapon : CItemEquipment : CItemUsable, CItemFlowerpot : CItemFurnishing); an unknown type
        // builds a CItemGeneral
        private ushort ItemTypeBits(ushort id) => ItemKind(id)?.Type switch
        {
            2 => 0x80,          // Linkshell
            3 => 0x40,          // Furnishing
            4 => 0x04,          // Puppet
            5 => 0x02,          // Usable
            6 => 0x02 | 0x08,   // Equipment
            7 => 0x02 | 0x08 | 0x10, // Weapon
            8 => 0x20,          // Currency
            9 => 0x40 | 0x100,  // FlowerPot
            _ => 0x01,          // General
        };

        private void RegisterShopBindings(Dictionary<string, LuaFunction> m)
        {
            lua_.SetGlobal("GetReadOnlyItem", l =>
            {
                l.PushObject(LookupItem((ushort)l.ToUIntArg(1)), ItemType);
                return 1;
            });

            // CLuaItem: a lookup item (read only) or a char's own stack (a traded one), whose item_basic row answers
            // the same getters
            ReadOnlyItem Self(LuaState l) => l.ToObject(1) switch
            {
                ReadOnlyItem item => item,
                Item item => LookupItem(item.ItemId) ?? throw new InvalidOperationException($"item {item.ItemId} is not in item_basic"),
                _ => throw new InvalidOperationException("bad self: expected " + ItemType),
            };
            static Item Owned(LuaState l) => l.ToObject(1) as Item;
            lua_.RegisterType(ItemType, new Dictionary<string, LuaFunction>
            {
                ["getID"] = l => Ret(l, Self(l).Id),
                ["getSubID"] = l => Ret(l, Self(l).SubId),
                ["getFlag"] = l => Ret(l, Self(l).Flags),
                ["getAHCat"] = l => Ret(l, Self(l).AhCategory),
                ["getBasePrice"] = l => Ret(l, Self(l).BasePrice),
                ["getStackSize"] = l => Ret(l, Self(l).StackSize),
                ["getName"] = l =>
                {
                    l.PushString(Self(l).Name);
                    return 1;
                },
                ["getQuantity"] = l => Ret(l, Owned(l)?.Quantity ?? 0),
                ["getSlotID"] = l => Ret(l, Owned(l)?.Slot ?? 0),
                ["getLocationID"] = l => Ret(l, (byte)(Owned(l)?.Location ?? 0)),
                ["getSignature"] = l =>
                {
                    l.PushString(Owned(l)?.Signature ?? "");
                    return 1;
                },
                // CLuaItem::isType: the ITEM_TYPE bits of the item's class (CItem::isType); the type is passed as a uint8
                ["isType"] = l => Ret(l, (ItemTypeBits(Self(l).Id) & (byte)l.ToUIntArg(2)) != 0),
                // CLuaItem::getCurrentCharges: a CItemUsable's Exdata::ItemTimerInfo RemainingCharges (0 for a lookup item)
                ["getCurrentCharges"] = l => Ret(l, Owned(l) is { } owned && (ItemTypeBits(owned.ItemId) & ItemTypeUsableBit) != 0 ? owned.Extra[1] : 0),
                // CLuaItem::isInstalled: a furnishing's Exdata::Furniture Installed bit (false for a lookup item)
                ["isInstalled"] = l => Ret(l, Owned(l)?.Installed ?? false),
                // CItemWeapon / CItemEquipment getters: -1 (255) for the skill type of a non-weapon
                ["getSkillType"] = l => Ret(l, ItemEquipment(l)?.Weapon?.Skill ?? 255),
                // CLuaItem::getReqLvl (xi.gear_sets.checkForGearSet)
                ["getReqLvl"] = l => Ret(l, ItemEquipment(l)?.ReqLvl ?? 0),
                // CLuaItem::getMod: the item's modifier (xi.additionalEffect.attack's ITEM_ADDEFFECT_* mods)
                ["getMod"] = l => Ret(l, ItemEquipment(l)?.GetModifier((Mod)l.ToIntArg(2)) ?? 0),
                ["isTwoHanded"] = l => Ret(l, ItemWeapon(l, "isTwoHanded")?.TwoHanded ?? false),
                ["isHandToHand"] = l => Ret(l, ItemWeapon(l, "isHandToHand")?.HandToHand ?? false),
                ["isShield"] = l =>
                {
                    var e = ItemEquipment(l);
                    if (e == null) log_("CLuaItem::isShield - not a valid Armor.");
                    return Ret(l, e?.IsShield ?? false);
                },
                ["getShieldSize"] = l => Ret(l, ItemShield(l, "getShieldSize")?.ShieldSize ?? 0),
                ["getShieldAbsorptionRate"] = l => Ret(l, ItemShield(l, "getShieldSize")?.ShieldAbsorption ?? 0),
                // CItemEquipment::getILvl (0 for an item that is no equipment)
                ["getILvl"] = l => Ret(l, ItemEquipment(l)?.ILvl ?? 0),
                // CItemEquipment::getTrialNumber: Exdata::AugmentTrial's TrialId (bytes 10-11, 15 bits); the C++ static_cast
                // is unchecked, so any item's exdata is read
                ["getTrialNumber"] = l =>
                {
                    var item = ReadItem(l);
                    return Ret(l, (item.Extra[10] | item.Extra[11] << 8) & 0x7FFF);
                },
                // CLuaItem::getAugment(slot): Exdata::AugmentStandard's Augments[slot] (bytes 2 + 2 * slot) as
                // { id (11 bits), value (5 bits) }; the C++ static_cast is unchecked, so any item's exdata is read
                ["getAugment"] = l =>
                {
                    var item = ReadItem(l);
                    int at = 2 + 2 * (int)l.ToUIntArg(2);
                    int raw = at + 1 < item.Extra.Length ? item.Extra[at] | item.Extra[at + 1] << 8 : 0;
                    l.NewTable();
                    l.Push((double)(raw & 0x7FF)); l.RawSetI(-2, 1);
                    l.Push((double)(raw >> 11)); l.RawSetI(-2, 2);
                    return 1;
                },
                // CItemWeapon's current / total weapon skill points (getCurrentUnlockPoints: Exdata::WeaponUnlock,
                // m_wsunlockpoints, the item's own once set); 0 for anything that is no weapon
                ["getWeaponskillPoints"] = l => Ret(l, ItemEquipment(l)?.Weapon != null ? WeaponInfo.CurrentUnlockPoints(ReadItem(l)) : 0),
                ["getWeaponskillPointsNeeded"] = l => Ret(l, ItemEquipment(l)?.Weapon?.UnlockPointsOf(Owned(l)) ?? 0),
                // CLuaItem::setWeaponskillPointsNeeded: the weapon's m_wsunlockpoints (setTotalUnlockPointsNeeded); a
                // lookup item (no m_writeItem) or one that is no weapon is not written
                ["setWeaponskillPointsNeeded"] = l =>
                {
                    if (Owned(l) is { } item && ItemEquipment(l)?.Weapon != null)
                        item.UnlockPointsNeeded = (ushort)l.ToUIntArg(2);
                    return 0;
                },
                // CItem::getAppraisalID / setAppraisalID: Exdata::Appraisable's AppraisalId (byte 22); a lookup item is
                // not written
                ["getAppraisalID"] = l => Ret(l, ReadItem(l).Extra[22]),
                ["setAppraisalID"] = l =>
                {
                    if (Owned(l) is { } item) item.Extra[22] = (byte)l.ToUIntArg(2);
                    return 0;
                },
                // CItem::state (ItemState): InTransaction, Bazaar, PlacedFurniture or Equipped (an equip entry of its
                // owner holds it), else Free; a lookup item is Free
                ["state"] = l =>
                {
                    if (Owned(l) is not { } item) return Ret(l, 0);
                    if (item.InTransaction) return Ret(l, 4);
                    if (item.InBazaar) return Ret(l, 2);
                    if (item.PlacedFurniture) return Ret(l, 3);
                    foreach (var c in charSessions_.Keys)
                        if (c.Items.Contains(item))
                            return Ret(l, c.Equip.Exists(e => e.Container == item.Location && e.InvSlot == item.Slot) ? 1 : 0);
                    return Ret(l, 0);
                },
                // CItem::isSubType / setSubType (ITEM_SUBTYPE): ITEM_CHARGED is the template's (an item_usable row with
                // valid targets), ITEM_AUGMENTED the item's own; setSubType ands a mask over 128, else ors the bits in
                ["isSubType"] = l => Ret(l, (ItemSubType(l) & (byte)l.ToUIntArg(2)) != 0),
                ["setSubType"] = l =>
                {
                    if (Owned(l) is not { } item) return 0;
                    byte subtype = (byte)l.ToUIntArg(2);
                    byte bits = (byte)(subtype > 128 ? ItemSubType(l) & subtype : ItemSubType(l) | subtype);
                    item.Augmented = (bits & ItemSubTypeAugmented) != 0;
                    return 0;
                },
                // CLuaItem::getExData: Exdata::toTable's typed table, else the raw bytes 0-indexed
                ["getExData"] = l =>
                {
                    var item = ReadItem(l);
                    if (ItemExdata.ToTable(item, loader_.Stats?.Items, id => Fishing.Fish.ContainsKey(id)) is { } t) ItemExdata.ToLua(l, t);
                    else PushRawExdata(l, item);
                    return 1;
                },
                // CLuaItem::setExData: Exdata::fromTable, else the table's keys as byte indices into m_extra; the item
                // is then dirty (saved and re-sent on the char's PostTick). A lookup item is not written.
                ["setExData"] = l =>
                {
                    if (Owned(l) is not { } item) return 0;
                    var data = ItemExdata.FromLua(l, 2);
                    if (!ItemExdata.FromTable(item, data, loader_.Stats?.Items, id => Fishing.Fish.ContainsKey(id)))
                    {
                        WriteRawExdata(item, data, "setExData");
                    }
                    item.Dirty = true;
                    return 0;
                },
                // CLuaItem::getExDataRaw: the raw bytes, 0-indexed
                ["getExDataRaw"] = l =>
                {
                    PushRawExdata(l, ReadItem(l));
                    return 1;
                },
                // CLuaItem::setExDataRaw: the table's keys as byte indices into m_extra, then dirty
                ["setExDataRaw"] = l =>
                {
                    if (Owned(l) is not { } item) return 0;
                    WriteRawExdata(item, ItemExdata.FromLua(l, 2), "setExDataRaw");
                    item.Dirty = true;
                    return 0;
                },
            });
            // m_readItem: the char's own stack, or for a lookup item its id with blank exdata
            Item ReadItem(LuaState l) => Owned(l) ?? new Item { ItemId = Self(l).Id };
            static void PushRawExdata(LuaState l, Item item)
            {
                l.NewTable();
                for (int idx = 0; idx < item.Extra.Length; idx++)
                {
                    l.Push((double)item.Extra[idx]);
                    l.RawSetI(-2, idx);
                }
            }
            void WriteRawExdata(Item item, Dictionary<object, object> data, string fn)
            {
                foreach (var (key, value) in data)
                {
                    byte index = ItemExdata.RawByte(key);
                    if (index >= item.Extra.Length)
                    {
                        log_($"{fn}: key too large for exdata array: {LookupItem(item.ItemId)?.Name}[{index}]");
                        continue;
                    }
                    item.Extra[index] = ItemExdata.RawByte(value);
                }
                item.Signature = ItemSignature.Get(item); // getSignature reads the exdata
            }
            EquipmentInfo ItemEquipment(LuaState l) =>
                Owned(l) is { } own ? loader_.Stats?.Items.EquipmentOf(own) : loader_.Stats?.Items.Equipment(Self(l).Id);
            byte ItemSubType(LuaState l) =>
                (byte)((ItemEquipment(l)?.Charged == true ? ItemSubTypeCharged : 0) | (Owned(l)?.Augmented == true ? ItemSubTypeAugmented : 0));
            WeaponInfo ItemWeapon(LuaState l, string fn)
            {
                var w = ItemEquipment(l)?.Weapon;
                if (w == null) log_($"CLuaItem::{fn} - not a valid Weapon.");
                return w;
            }
            EquipmentInfo ItemShield(LuaState l, string fn)
            {
                var e = ItemEquipment(l);
                if (e == null) log_($"CLuaItem::{fn} - not a valid Armor.");
                else if (!e.IsShield) log_($"CLuaItem::{fn} - not a valid Shield.");
                return e is { IsShield: true } ? e : null;
            }

            m["createShop"] = CreateShop;
            m["addShopItem"] = AddShopItem;
            m["sendMenu"] = SendMenu;
        }

        // CLuaBaseEntity::createShop(size, fameArea): the vendor is the event's target, else the one being prepared
        private int CreateShop(LuaState l)
        {
            if (Pc(l, "createShop") is not CharData c) return 0;
            var shop = c.Exchange;
            shop.Clean();
            shop.SetSize((byte)(l.ToIntArg(2) + 1));
            var vendor = c.IsInEvent ? c.CurrentEvent.TargetEntity : c.EventPreparation.TargetEntity;
            shop.ShopVendorId = (vendor as NpcEntity)?.Id ?? 0;
            if (l.IsNumber(3))
            {
                byte area = (byte)l.ToIntegral(3); // xi.shop.general passes -1 for "no fame area"
                shop.Type = area;
                shop.ShopFameArea = area;
            }
            return 0;
        }

        // CLuaBaseEntity::addShopItem(itemID, price, { job, level } | { guild, rank })
        private int AddShopItem(LuaState l)
        {
            if (Pc(l, "addShopItem") is not CharData c) return 0;
            var shop = c.Exchange;
            byte slot = shop.ItemsCount;
            shop.SetItem(slot, (ushort)l.ToUIntArg(2), 0, (uint)(long)l.ToNumberArg(3));
            // a char can add items to the container while shopping, so the shop's own count is kept apart
            shop.ExSize++;

            if (l.IsTable(4))
            {
                long job = FieldInt(l, 4, "job", -1);
                long guild = FieldInt(l, 4, "guild", -1);
                if (job >= 0)
                {
                    byte level = (byte)Math.Clamp((byte)FieldInt(l, 4, "level", 1), (byte)1, (byte)99);
                    shop.SetRestriction(slot, new SlotRestriction(SlotRestrictionKind.Job, (byte)job, level));
                }
                else if (guild >= 0)
                {
                    shop.SetRestriction(slot, new SlotRestriction(SlotRestrictionKind.Guild, (byte)guild, (ushort)FieldInt(l, 4, "rank", 0)));
                }
            }
            return 0;
        }

        // CLuaBaseEntity::sendMenu: the mog house menu (0x02E, header only), the shop, the auction house
        private int SendMenu(LuaState l)
        {
            if (Pc(l, "sendMenu") is not CharData c) return 0;
            int menu = l.ToIntArg(2);
            switch (menu)
            {
                case MenuMoogle:
                    PushTo(c, new PacketWriter(0x02E, 4).Packet);
                    break;
                case MenuShop:
                    PushTo(c, ShopOpen(c.Exchange));
                    foreach (var packet in ShopList(c.Exchange)) PushTo(c, packet);
                    break;
                case MenuAuction:
                    PushTo(c, AucOpen(AucCommand.Open));
                    break;
                default:
                    log_($"lua: Menu {menu} not implemented, yet.");
                    break;
            }
            return 0;
        }

        // ---- S2C ---------------------------------------------------------------------------------------------------

        // GP_SERV_COMMAND_SHOP_OPEN: ShopListNum
        private static BasicPacket ShopOpen(TradeContainer shop)
        {
            var w = new PacketWriter(0x03E, 0x08);
            w.U16(4, shop.ItemsCount);
            return w.Packet;
        }

        // GP_SERV_COMMAND_SHOP_LIST: 19 items a packet, the last one flagged 0x89. A guild restriction shows as the
        // craft skill and (rank + 1) * 100, a job one as the job + 64 (which the client reads as a job) and the level.
        private static List<BasicPacket> ShopList(TradeContainer shop)
        {
            var packets = new List<BasicPacket>();
            var w = new PacketWriter(0x03C, 8 + ShopListMax * ShopEntrySize);
            int i = 0;
            ushort offset = 0;
            for (int slot = 0; slot < shop.ItemsCount; slot++)
            {
                if (i == ShopListMax)
                {
                    w.U16(4, offset);
                    w.U8(6, 0x00); // more packets to come
                    packets.Add(w.Packet);
                    w = new PacketWriter(0x03C, 8 + ShopListMax * ShopEntrySize);
                    i = 0;
                    offset += ShopListMax;
                }

                int at = 8 + i * ShopEntrySize;
                w.U32(at, shop.GetQuantity(slot));
                w.U16(at + 4, shop.GetItemId(slot));
                w.U8(at + 6, slot);
                var restriction = shop.GetRestriction(slot);
                if (restriction.Kind == SlotRestrictionKind.Guild)
                {
                    w.U16(at + 8, restriction.Id);
                    w.U16(at + 10, (restriction.Value + 1) * 100);
                }
                else if (restriction.Kind == SlotRestrictionKind.Job)
                {
                    w.U16(at + 8, restriction.Id + 64);
                    w.U16(at + 10, restriction.Value);
                }
                i++;
            }
            w.U16(4, offset);
            w.U8(6, 0x89); // the last packet
            w.SetSize((uint)(8 + i * ShopEntrySize));
            packets.Add(w.Packet);
            return packets;
        }

        // GP_SERV_COMMAND_SHOP_SELL: the price offered for the inventory slot's item
        private static BasicPacket ShopSell(byte itemIndex, uint price)
        {
            var w = new PacketWriter(0x03D, 0x10);
            w.U32(4, price);
            w.U8(8, itemIndex);
            return w.Packet;
        }

        // GP_SERV_COMMAND_SHOP_BUY: BuyState 0, the purchase went through
        private static BasicPacket ShopBuy(ushort shopItemIndex, uint count)
        {
            var w = new PacketWriter(0x03F, 0x0C);
            w.U16(4, shopItemIndex);
            w.U32(8, count);
            return w.Packet;
        }

        // ---- C2S ---------------------------------------------------------------------------------------------------

        private static PacketValidator ValidateShopBuy(CharData c, BasicPacket p) => Pv(c)
            .BlockedBy(BlockedState.InEvent)
            .MustEqual(p.Buffer[12], 0, "PropertyItemIndex not 0");

        // GP_CLI_COMMAND_SHOP_BUY: ItemNum (at most a stack) of the shop's ShopItemIndex, for its price each
        private void OnShopBuy(MapSession s, CharData c, BasicPacket p)
        {
            var shop = c.Exchange;
            uint quantity = U32(p, 4);
            ushort index = U16(p, 10);
            if (index > shop.ExSize - 1)
            {
                log_($"User '{c.Name}' attempting to buy vendor item from an invalid slot!");
                return;
            }

            ushort itemId = shop.GetItemId(index);
            uint price = shop.GetQuantity(index); // the shop keeps the price in the quantity
            var item = LookupItem(itemId);
            if (item == null)
            {
                log_($"User '{c.Name}' attempting to buy an invalid item from vendor!");
                return;
            }

            var restriction = shop.GetRestriction(index);
            bool meetsRequirement = restriction.Kind switch
            {
                SlotRestrictionKind.Job => c.JobLevels[restriction.Id] >= restriction.Value,
                SlotRestrictionKind.Guild => c.RealSkillRank[restriction.Id] >= restriction.Value,
                _ => true,
            };
            if (!meetsRequirement)
            {
                log_($"{c.Name} attempting to buy item {itemId} without meeting shop requirement!");
                return;
            }

            quantity = Math.Min(quantity, item.StackSize);
            uint cost = unchecked(price * quantity);
            var gil = Gil(c);
            if (gil == null) return;
            uint gilBefore = gil.Quantity;
            using (var transaction = new ItemTransaction(this, c))
            {
                if (!transaction.Pay(cost) || !transaction.Give(Container.Inventory, itemId, quantity).HasValue || !transaction.Commit()) return;
            }

            var vendor = ShopVendor(shop);
            if (vendor != null) OnShopBuyScript(c, vendor, itemId, quantity, cost);
            AuditVendor(c, vendor, "buy", itemId, quantity, price, (int)(Gil(c)?.Quantity ?? 0) - (int)gilBefore);

            log_($"User '{c.Name}' purchased {quantity} of item of ID {itemId} [from VENDOR] ");
            Push(s, ShopBuy(index, quantity));
            Push(s, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
        }

        private static PacketValidator ValidateShopSell(CharData c) => Pv(c)
            .BlockedBy(BlockedState.InEvent | BlockedState.Crafting);

        // GP_CLI_COMMAND_SHOP_SELL_REQ: the item to sell waits past the shop's items until 0x085 confirms it
        private void OnShopSellReq(MapSession s, CharData c, BasicPacket p)
        {
            uint quantity = U32(p, 4);
            ushort itemNo = U16(p, 8);
            byte itemIndex = p.Buffer[10];
            var item = StorageItem(c, Container.Inventory, itemIndex);
            if (item == null || item.ItemId != itemNo || ((ItemBasic(item.ItemId)?.Flags ?? 0) & ItemFlagNoSale) != 0) return;

            quantity = Math.Min(quantity, item.Quantity);
            var shop = c.Exchange;
            shop.SetItem(shop.ExSize, itemNo, itemIndex, quantity);
            Push(s, ShopSell(itemIndex, SellPrice(c, itemNo, shop.ShopFameArea)));
        }

        private static PacketValidator ValidateShopSellSet(CharData c, BasicPacket p) => Pv(c)
            .BlockedBy(BlockedState.InEvent | BlockedState.Crafting)
            .RequiresPriorPacket(0x084)
            .MustEqual(U16(p, 4), 1, "SellFlag not 1");

        // GP_CLI_COMMAND_SHOP_SELL_SET: sell what 0x084 put aside, for the fame-adjusted price each
        private void OnShopSellSet(MapSession s, CharData c, BasicPacket p)
        {
            var shop = c.Exchange;
            uint quantity = shop.GetQuantity(shop.ExSize);
            ushort itemId = shop.GetItemId(shop.ExSize);
            byte slot = shop.GetInvSlotId(shop.ExSize);

            uint cost, unitPrice;
            uint gilBefore;
            using (var transaction = new ItemTransaction(this, c))
            {
                var item = transaction.ClaimSlot(Container.Inventory, slot);
                if (item == null)
                {
                    log_($"GP_CLI_COMMAND_SHOP_SELL_SET: Player {c.Name} trying to sell an item that is missing or already claimed!");
                    return;
                }
                if (quantity < 1 || quantity > StackSize(item.ItemId))
                {
                    log_($"GP_CLI_COMMAND_SHOP_SELL_SET: Player {c.Name} trying to sell invalid quantity {quantity} of itemID {item.ItemId} [to VENDOR] ");
                    return;
                }
                if (quantity > item.Quantity)
                {
                    log_($"GP_CLI_COMMAND_SHOP_SELL_SET: Player {c.Name} trying to sell more items than they have in stack ({quantity}/{item.Quantity}) of itemID {item.ItemId} [to VENDOR] ");
                    return;
                }
                if (itemId != item.ItemId)
                {
                    log_($"GP_CLI_COMMAND_SHOP_SELL_SET: Player {c.Name} trying to sell an item different than the original ID (original: {itemId}, current {item.ItemId}) [to VENDOR] ");
                    return;
                }

                unitPrice = SellPrice(c, itemId, shop.ShopFameArea);
                cost = unchecked(quantity * unitPrice);
                gilBefore = Gil(c)?.Quantity ?? 0;
                if (!transaction.Take(Container.Inventory, slot, quantity) || !transaction.Earn(cost) || !transaction.Commit())
                {
                    log_($"GP_CLI_COMMAND_SHOP_SELL_SET: Player {c.Name} could not sell item ID {itemId}");
                    return;
                }
            }

            AuditVendor(c, ShopVendor(shop), "sell", itemId, quantity, unitPrice, (int)(Gil(c)?.Quantity ?? 0) - (int)gilBefore);
            log_($"GP_CLI_COMMAND_SHOP_SELL_SET: Player '{c.Name}' sold {quantity} of itemID {itemId} (Total: {cost} gil) [to VENDOR] ");
            Push(s, ParamMessage(itemId, quantity, MsgStdSell));
            Push(s, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
            shop.SetItem(shop.ExSize, 0, 0xFF, 0);
        }

        // zoneutils::GetEntity(vendorId, TYPE_NPC)
        private NpcEntity ShopVendor(TradeContainer shop)
        {
            uint id = shop.ShopVendorId;
            if (id == 0) return null;
            return HeldZone((ushort)(id >> 12 & 0xFFF))?.NpcById.TryGetValue(id, out var npc) == true ? npc : null;
        }

        // luautils::callGlobal<uint32>("xi.shop.onSellPriceCheck"): 0 when the script fails or returns no number
        private uint SellPrice(CharData c, ushort itemId, byte fameArea)
        {
            if (lua_ == null) return 0;
            int top = lua_.Top;
            try
            {
                lua_.GetPath("xi.shop.onSellPriceCheck");
                if (lua_.IsNil(-1))
                {
                    log_("luautils::callGlobalFunction: xi.shop.onSellPriceCheck: Function not found");
                    return 0;
                }
                lua_.PushObject(c);
                lua_.Push((double)itemId);
                lua_.Push((double)fameArea);
                lua_.Call(3, 1);
                return lua_.IsNumber(-1) ? (uint)lua_.ToIntegral(-1) : 0;
            }
            catch (LuaException e)
            {
                ScriptError("xi.shop.onSellPriceCheck", e);
                return 0;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::OnShopBuy: the vendor's onShopBuy, when its script has one
        private void OnShopBuyScript(CharData c, NpcEntity npc, ushort itemId, uint quantity, uint gil)
        {
            if (lua_ == null) return;
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction($"./scripts/zones/{AreaOf(npc).Name}/npcs/{npc.Name}.lua", "onShopBuy");
                if (lua_.IsNil(-1)) return;
                lua_.PushObject(c);
                lua_.PushObject(npc);
                lua_.Push((double)itemId);
                lua_.Push((double)quantity);
                lua_.Push((double)gil);
                lua_.Call(5, 0);
            }
            catch (LuaException e)
            {
                ScriptError("OnShopBuy", e);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // auditPurchase / auditSale (the NPC and guild shop ones), with map.AUDIT_PLAYER_VENDOR; written in line rather than on a worker thread
        private void AuditVendor(CharData c, NpcEntity vendor, string direction, ushort itemId, uint quantity, uint basePrice, int appliedGil) =>
            AuditVendor(c, c.Exchange.ShopVendorId, vendor?.Name ?? "", direction, itemId, quantity, basePrice, unchecked(quantity * basePrice), appliedGil);

        private void AuditVendor(CharData c, uint npcId, string npcName, string direction, ushort itemId, uint quantity, uint basePrice, uint totalPrice, int appliedGil)
        {
            if (loader_.Stats?.Settings.AuditPlayerVendor != true) return;
            if (Exec("INSERT INTO audit_vendor(itemid, quantity, seller, seller_name, direction, npcid, npc_name, zoneid, baseprice, totalprice, applied_gil, date) " +
                     $"VALUES (?, ?, ?, ?, '{direction}', ?, ?, ?, ?, ?, ?, UNIX_TIMESTAMP())",
                     BoundValue.Of((uint)itemId), BoundValue.Of(quantity), BoundValue.Of(c.Id), BoundValue.Of(c.Name),
                     BoundValue.Of(npcId), BoundValue.Of(npcName), BoundValue.Of(c.Zone),
                     BoundValue.Of(basePrice), BoundValue.Of(totalPrice), BoundValue.Of(appliedGil)) == null)
                log_($"Failed to log vendor {(direction == "buy" ? "purchase" : "sale")} (item: {itemId}, quantity: {quantity}, {c.Name}");
        }
    }
}
