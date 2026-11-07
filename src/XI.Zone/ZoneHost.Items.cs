using System;
using System.Collections.Generic;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // The C2S handlers for the char's items and equipment (c2s/0x050), and what the network cycle sends and saves after
    // a datagram's packets are parsed (flushEquipChanges, the updatemask half of PostTick, CharPersist::Equip/Look)
    public sealed partial class ZoneHost
    {
        private static readonly Container[] UnlockableWardrobes =
        {
            Container.Wardrobe3, Container.Wardrobe4, Container.Wardrobe5, Container.Wardrobe6, Container.Wardrobe7, Container.Wardrobe8,
        };

        private static readonly Container[] AdditionalEquipContainers = { Container.MogSatchel, Container.MogSack, Container.MogCase };

        // 0x050's validContainers: the inventory and first two wardrobes always, the other wardrobes once unlocked, and the
        // satchel, sack and case only with EQUIP_FROM_OTHER_CONTAINERS
        private long[] EquipContainers(CharData c)
        {
            var allowed = new List<long> { (long)Container.Inventory, (long)Container.Wardrobe, (long)Container.Wardrobe2 };
            foreach (var container in UnlockableWardrobes)
                if (c.StorageSize[(int)container] > 0) allowed.Add((long)container);
            if (loader_.Stats?.Settings.EquipFromOtherContainers == true)
                foreach (var container in AdditionalEquipContainers)
                    if (c.StorageSize[(int)container] > 0) allowed.Add((long)container);
            return allowed.ToArray();
        }

        private const int EquipSlotCount = 18; // SLOTTYPE, with the two linkshells

        private PacketValidator ValidateEquipSet(CharData c, BasicPacket p) => Pv(c)
            .BlockedBy(BlockedState.InEvent | BlockedState.AbnormalStatus)
            .Range("EquipKind", p.Buffer[5], 0, EquipSlotCount - 1)
            .OneOf("Category", p.Buffer[6], EquipContainers(c));

        // GP_CLI_COMMAND_EQUIP_SET: equip PropertyItemIndex of Category in EquipKind (index 0 unequips), unless encumbrance
        // blocks the slot; a changed slot is a monstrosity gladiator gear change, the gear sets are rechecked
        // (CheckForGearSet) and every slot's latents after the parse (retriggerLatents).
        private void OnEquipSet(MapSession s, CharData c, BasicPacket p)
        {
            var stats = loader_.Stats;
            if (stats == null) return;
            // encumbrance locks the slot against unequipping too, which EquipItem allows for scripts
            if ((c.EquipBlock & 1 << p.Buffer[5]) != 0) return;
            var before = p.Buffer[5] < c.Equipped.Length ? c.Equipped[p.Buffer[5]] : null;
            stats.EquipItem(c, p.Buffer[4], p.Buffer[5], (Container)p.Buffer[6],
                            (param, msg) => Push(s, SelfPackets.BattleMessage(c, param, 0, msg)));
            if (p.Buffer[5] < c.Equipped.Length && c.Equipped[p.Buffer[5]] != before) GladiatorGearChange(c);
            CheckForGearSet(c);
            CharStats.UpdateHealth(c);
            c.Latents.RetriggerLatents = true;
        }

        // SLOTTYPE, for oneOf<SLOTTYPE>'s message
        private enum SLOTTYPE : byte
        {
            SLOT_MAIN, SLOT_SUB, SLOT_RANGED, SLOT_AMMO, SLOT_HEAD, SLOT_BODY, SLOT_HANDS, SLOT_LEGS, SLOT_FEET, SLOT_NECK,
            SLOT_WAIST, SLOT_EAR1, SLOT_EAR2, SLOT_RING1, SLOT_RING2, SLOT_BACK, SLOT_LINK1, SLOT_LINK2,
        }

        private const int EquipsetMax = 16;
        private const int EquipsetAt = 8; // equipsetitem_t Equipment[16]: ItemIndex, EquipKind, Category, padding

        // GP_CLI_COMMAND_EQUIPSET_SET::validate: a Monipulator cannot use /equipset
        private PacketValidator ValidateEquipsetSet(CharData c, BasicPacket p)
        {
            var allowed = EquipContainers(c);
            byte count = p.Buffer[4];
            var pv = Pv(c)
                .BlockedBy(BlockedState.InEvent | BlockedState.AbnormalStatus | BlockedState.Monstrosity)
                .Range("Count", count, 1, EquipsetMax);
            if (count <= EquipsetMax)
                for (int i = 0; i < count; i++)
                    pv = pv.OneOf<SLOTTYPE>(p.Buffer[EquipsetAt + i * 4 + 1])
                           .OneOf("equipment.Category", p.Buffer[EquipsetAt + i * 4 + 2], allowed);
            return pv;
        }

        // GP_CLI_COMMAND_EQUIPSET_SET: each of Count entries equips like 0x050, the gear sets rechecked
        // (CheckForGearSet) and the latents retriggered after the parse.
        private void OnEquipsetSet(MapSession s, CharData c, BasicPacket p)
        {
            var stats = loader_.Stats;
            if (stats == null) return;
            bool changed = false;
            for (int i = 0; i < p.Buffer[4]; i++)
            {
                int at = EquipsetAt + i * 4;
                if ((c.EquipBlock & 1 << p.Buffer[at + 1]) != 0) continue;
                byte slot = p.Buffer[at + 1];
                var before = slot < c.Equipped.Length ? c.Equipped[slot] : null;
                stats.EquipItem(c, p.Buffer[at], slot, (Container)p.Buffer[at + 2],
                                (param, msg) => Push(s, SelfPackets.BattleMessage(c, param, 0, msg)));
                changed = changed || slot < c.Equipped.Length && c.Equipped[slot] != before;
            }
            if (changed) GladiatorGearChange(c);
            CheckForGearSet(c);
            CharStats.UpdateHealth(c);
            c.Latents.RetriggerLatents = true;
        }

        // luautils::CheckForGearSet: xi.gear_sets.checkForGearSet on a gear change (equip, equipset, job change), which
        // rebuilds the char's gear set mods
        private void CheckForGearSet(CharData c)
        {
            if (lua_ == null) return;
            int top = lua_.Top;
            try
            {
                lua_.GetPath("xi.gear_sets.checkForGearSet");
                if (!lua_.IsFunction(-1)) return;
                lua_.PushObject(c);
                lua_.Call(1, 0);
            }
            catch (LuaException e)
            {
                ScriptError("luautils::CheckForGearSet", e);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        private static readonly Container[] UnlockableMoveContainers =
        {
            Container.MogSack, Container.Wardrobe3, Container.Wardrobe4, Container.Wardrobe5, Container.Wardrobe6,
            Container.Wardrobe7, Container.Wardrobe8, Container.MogSatchel,
        };

        private const ushort ZoneAlZahbi = 48, ZoneAhtUrhganWhitegate = 50, ZoneNashmau = 53;

        // charutils::hasMogLockerAccess: a lease that has not run out (Vana'diel time); access type 1 opens it in a zone
        // with a Nomad Moogle or the char's own mog house, any other type in its own mog house in Al Zahbi or Whitegate,
        // or in Nashmau
        private bool HasMogLockerAccess(CharData c)
        {
            int tstamp = CharVar(c, "mog-locker-expiry-timestamp");
            uint accessType = (uint)CharVar(c, "mog-locker-access-type");
            if (tstamp <= 0 || LoginPackets.VanadielTimestamp(UnixNow()) >= (uint)tstamp) return false;
            if (accessType == 1) return AreaOf(c).CanUseMisc("mogmenu") || c.MoghouseId == c.Id;
            if (c.MoghouseId == c.Id && (c.Zone == ZoneAlZahbi || c.Zone == ZoneAhtUrhganWhitegate)) return true;
            return c.Zone == ZoneNashmau;
        }

        // 0x029's validContainers: the inventory, case and first two wardrobes always; the safe (and the second floor's
        // once unlocked) in the char's own mog house or a zone with a Nomad Moogle; the locker with access; storage only in
        // the char's own mog house; the sack, satchel and other wardrobes once unlocked; and the recycle bin when enabled
        private long[] ItemMoveContainers(CharData c)
        {
            var settings = loader_.Stats?.Settings ?? new StatSettings();
            var allowed = new List<long> { (long)Container.Inventory, (long)Container.MogCase, (long)Container.Wardrobe, (long)Container.Wardrobe2 };
            if (AreaOf(c).CanUseMisc("mogmenu") || c.MoghouseId == c.Id)
            {
                allowed.Add((long)Container.MogSafe);
                if ((c.MhFlag & 0x20) != 0 && settings.MogHouse2F) allowed.Add((long)Container.MogSafe2);
            }
            c.MogLockerAccess = HasMogLockerAccess(c);
            if (c.MogLockerAccess) allowed.Add((long)Container.MogLocker);
            if (c.MoghouseId == c.Id) allowed.Add((long)Container.Storage);
            foreach (var container in UnlockableMoveContainers)
                if (c.StorageSize[(int)container] > 0) allowed.Add((long)container);
            if (settings.ItemRecycleBin) allowed.Add((long)Container.RecycleBin);
            return allowed.ToArray();
        }

        private static bool IsWardrobe(Container container) =>
            container == Container.Wardrobe || container == Container.Wardrobe2 ||
            (container >= Container.Wardrobe3 && container <= Container.Wardrobe8);

        // isValidMovement: a free, non-gil item; the recycle bin only empties into the inventory or locker and only fills
        // from the locker (everything else goes through 0x028); wardrobes take equipment only
        private bool IsValidMovement(CharData c, Container from, Container to, byte itemIndex)
        {
            var item = StorageItem(c, from, itemIndex);
            if (item == null || IsBusy(c, item) || item.ItemId == GilItemId) return false;
            if (from == Container.RecycleBin && to != Container.Inventory && to != Container.MogLocker) return false;
            if (to == Container.RecycleBin && from != Container.MogLocker) return false;
            if (IsWardrobe(to) && loader_.Stats?.Items.Equipment(item.ItemId) == null) return false;
            return true;
        }

        private PacketValidator ValidateItemMove(CharData c, BasicPacket p)
        {
            var containers = ItemMoveContainers(c);
            var pv = Pv(c)
                .BlockedBy(BlockedState.InEvent)
                .OneOf("Category1", p.Buffer[8], containers)
                .OneOf("Category2", p.Buffer[9], containers);
            // the custom check reads the containers, which only exist once both are known to be valid
            if (!pv.Valid) return pv;
            return pv.MustEqual(IsValidMovement(c, (Container)p.Buffer[8], (Container)p.Buffer[9], p.Buffer[10]) ? 1 : 0, 1, "Illegal movement");
        }

        // GP_CLI_COMMAND_ITEM_MOVE: ItemNum of the stack in Category1/ItemIndex1 goes to Category2. Part of a stack splits
        // into a new one there; a whole stack joins the stack at ItemIndex2 (when that is a real slot) or moves to the
        // first free slot.
        private void OnItemMove(MapSession s, CharData c, BasicPacket p)
        {
            uint itemNum = U32(p, 4);
            var cat1 = (Container)p.Buffer[8];
            var cat2 = (Container)p.Buffer[9];
            byte index1 = p.Buffer[10], index2 = p.Buffer[11];

            var item = StorageItem(c, cat1, index1);
            if (item == null) return;
            if (item.Quantity < itemNum)
            {
                log_($"GP_CLI_COMMAND_ITEM_MOVE: Trying to move too much quantity from location {(int)cat1} slot {index1}");
                return;
            }

            using var transaction = new ItemTransaction(this, c);
            if (item.Quantity - itemNum != 0) // split item stack
            {
                if (transaction.ClaimSlot(cat1, index1) == null) return;
                if (!transaction.Split(cat1, index1, cat2, itemNum) || !transaction.Commit())
                    log_($"GP_CLI_COMMAND_ITEM_MOVE: {c.Name} could not split item {item.ItemId}");
            }
            else if (index2 < 82) // move stack / combine items into stack (80 + 1)
            {
                var item2 = StorageItem(c, cat2, index2);
                if (item2 == null || item2.ItemId != item.ItemId || IsBusy(c, item2))
                {
                    log_($"GP_CLI_COMMAND_ITEM_MOVE: Trying to unite items with invalid item {item2?.ItemId ?? 0} at location {(int)cat2} slot {index2}");
                    return;
                }
                uint stack = StackSize(item2.ItemId);
                if (item2.Quantity < stack)
                {
                    uint totalQty = item.Quantity + item2.Quantity;
                    uint moveQty = totalQty >= stack ? stack - item2.Quantity : item.Quantity;
                    if (transaction.ClaimSlot(cat1, index1) == null || transaction.ClaimSlot(cat2, index2) == null) return;
                    if (moveQty > 0 && (!transaction.MoveBetween(cat1, index1, cat2, index2, moveQty) || !transaction.Commit()))
                        log_($"GP_CLI_COMMAND_ITEM_MOVE: {c.Name} could not combine stacks");
                }
                return;
            }
            else
            {
                // CItemContainer::MoveItemTo into the first free slot
                int size = c.StorageSize[(int)cat2];
                int newSlot = 1;
                while (newSlot <= size && c.FindItem(cat2, (byte)newSlot) != null) newSlot++;
                if (FreeSlots(c, cat2) == 0 || newSlot > size)
                {
                    // the client thought there was room; resend the container so it sees what is there
                    for (int slot = 0; slot <= size; slot++)
                        if (c.FindItem(cat2, (byte)slot) is Item slotItem) Push(s, GameOkPackets.ItemAttr(slotItem, ItemLockFlg(c, slotItem)));
                    Push(s, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
                    log_($"GP_CLI_COMMAND_ITEM_MOVE: Location {(int)cat2} Slot {index2} is full");
                    return;
                }

                var r = Exec("UPDATE char_inventory SET location = ?, slot = ? WHERE charid = ? AND location = ? AND slot = ?",
                             BoundValue.Of((byte)cat2), BoundValue.Of((byte)newSlot), BoundValue.Of(c.Id), BoundValue.Of((byte)cat1), BoundValue.Of(index1));
                if (r != null && r.RowsAffected() != 0)
                {
                    item.Location = cat2;
                    item.Slot = (byte)newSlot;
                    bool leak = loader_.Stats?.Settings.LeakExtDataOnItemMove ?? true;
                    Push(s, GameOkPackets.EmptySlotAttr(cat1, index1, leak ? item.Extra : null));
                    Push(s, GameOkPackets.ItemAttr(item, ItemLockFlg(c, item)));
                }
            }

            Push(s, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
        }

        // lockFlagFor (GameOkPackets.LockFlagFor): a bazaar price, an equipped item (a linkshell its own flag), an
        // installed furnishing
        private static byte ItemLockFlg(CharData c, Item item) => GameOkPackets.LockFlagFor(c, item);

        private const ushort MsgStdThrowAway = 180;          // "You throw away <item>."
        private const ushort MsgStdUnableToThrowAway = 325;  // "You are unable to throw away the <item>."
        private const uint ItemFlagNoRecycle = 0x00010000;
        private const int RecycleBinSlots = 10;

        // 0x028's validContainers: every container but the recycle bin, whether or not the char can reach it now
        private static readonly long[] ItemDumpContainers =
        {
            (long)Container.Inventory, (long)Container.MogSafe, (long)Container.MogSafe2, (long)Container.Storage,
            (long)Container.TempItems, (long)Container.MogLocker, (long)Container.MogSatchel, (long)Container.MogSack,
            (long)Container.MogCase, (long)Container.Wardrobe, (long)Container.Wardrobe2, (long)Container.Wardrobe3,
            (long)Container.Wardrobe4, (long)Container.Wardrobe5, (long)Container.Wardrobe6, (long)Container.Wardrobe7,
            (long)Container.Wardrobe8,
        };

        private static PacketValidator ValidateItemDump(CharData c, BasicPacket p) => Pv(c)
            .BlockedBy(BlockedState.InEvent)
            .OneOf("Category", p.Buffer[8], ItemDumpContainers)
            .Range("ItemNum", U32(p, 4), 0, 99); // retail honors 0 quantity

        // GP_SERV_COMMAND_MESSAGE(param0, MsgStd): "Para0 %u " in the 15 bytes the 0x1C packet has for it
        private static BasicPacket ParamMessage(uint param0, ushort message)
        {
            var w = new PacketWriter(0x009, 0x1C);
            w.U16(10, message);
            w.Name(13, $"Para0 {param0} ", 15);
            return w.Packet;
        }

        // GP_SERV_COMMAND_MESSAGE(nullptr, param0, param1, MsgStd)
        private static BasicPacket ParamMessage(uint param0, uint param1, ushort message)
        {
            var w = new PacketWriter(0x009, 0x24);
            w.U16(10, message);
            w.Name(13, $"Para0 {param0} Para1 {param1}", 23);
            return w.Packet;
        }

        // GP_CLI_COMMAND_ITEM_DUMP: ItemNum of Category/ItemIndex is thrown away. Gil and storage slips holding items
        // cannot be; an inventory item goes whole to the recycle bin when it is enabled (the C++ ignores ItemNum there too),
        // anything else is destroyed. Throwing away a linkshell (the shell itself) breaks it. Item scripts' onItemDrop
        // is not called.
        private void OnItemDump(MapSession s, CharData c, BasicPacket p)
        {
            uint itemNum = U32(p, 4);
            var category = (Container)p.Buffer[8];
            byte index = p.Buffer[9];

            if (category == Container.Inventory && index == 0)
            {
                Push(s, ParamMessage(GilItemId, MsgStdUnableToThrowAway));
                return;
            }

            var item = StorageItem(c, category, index);
            if (item == null || IsBusy(c, item))
            {
                log_($"GP_CLI_COMMAND_ITEM_DUMP: Attempt of removal of invalid item from slot {index}");
                return;
            }
            if (item.Quantity < itemNum)
            {
                log_($"GP_CLI_COMMAND_ITEM_DUMP: Trying to drop too much quantity from location {(int)category} slot {index}");
                return;
            }

            // CItem::isStorageSlip: a slip with anything stored in its exdata stays
            if (item.ItemId > 29311 && item.ItemId < 29340)
            {
                int slipData = 0;
                foreach (byte b in item.Extra) slipData += b;
                if (slipData != 0)
                {
                    Push(s, ParamMessage(item.ItemId, MsgStdUnableToThrowAway));
                    return;
                }
            }

            BreakDumpedLinkshell(item);

            bool recycle = loader_.Stats?.Settings.ItemRecycleBin ?? true;
            uint flags = ItemBasic(item.ItemId)?.Flags ?? 0;
            if (!recycle || category != Container.Inventory || (flags & ItemFlagNoRecycle) != 0)
                DropItem(s, c, category, index, itemNum, item.ItemId);
            else
                AddItemToRecycleBin(s, c, category, index, (byte)itemNum);
        }

        // GP_CLI_COMMAND_ITEM_STACK: every stack in Category short of its stack size is topped up from the later stacks of
        // the same item, one transaction per pair. A sort within a second of the last only counts toward
        // LIGHTLUGGAGE_BLOCK, which logs the char out.
        private void OnItemStack(MapSession s, CharData c, BasicPacket p)
        {
            var category = (Container)U32(p, 4);
            int index = (int)category;
            int size = c.StorageSize[index];

            long now = NowMs();
            if (c.LastSortingMs[index] is long last && now < last + 1000)
            {
                byte block = loader_.Stats?.Settings.LightluggageBlock ?? 4;
                if (block == ++c.SortingPackets[index])
                {
                    log_($"lightluggage detected: <{c.Name}> will be removed from server");
                    Logout(s, c);
                }
                return;
            }

            c.SortingPackets[index] = 0;
            c.LastSortingMs[index] = now;
            for (int slot = 1; slot <= size; slot++)
            {
                var item = c.FindItem(category, (byte)slot);
                if (item == null || IsBusy(c, item) || item.Quantity >= StackSize(item.ItemId)) continue;

                for (int slot2 = slot + 1; slot2 <= size; slot2++)
                {
                    var item2 = c.FindItem(category, (byte)slot2);
                    if (item2 == null || item2.ItemId != item.ItemId || IsBusy(c, item2) || item2.Quantity >= StackSize(item2.ItemId))
                        continue;

                    uint stack = StackSize(item.ItemId);
                    uint moveQty = item.Quantity + item2.Quantity >= stack ? stack - item.Quantity : item2.Quantity;
                    if (moveQty == 0) continue;

                    using var transaction = new ItemTransaction(this, c);
                    if (transaction.ClaimSlot(category, (byte)slot) == null || transaction.ClaimSlot(category, (byte)slot2) == null) continue;
                    if (!transaction.MoveBetween(category, (byte)slot2, category, (byte)slot, moveQty) || !transaction.Commit())
                        log_($"GP_CLI_COMMAND_ITEM_STACK: {c.Name} could not merge stacks");
                }
            }

            Push(s, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
        }

        // charutils::DropItem
        private void DropItem(MapSession s, CharData c, Container category, byte slot, uint quantity, ushort itemId)
        {
            using var transaction = new ItemTransaction(this, c);
            if (!transaction.Take(category, slot, quantity) || !transaction.Commit()) return;

            log_($"Player {c.Name} DROPPING itemID: {itemId} quantity: {quantity}");
            Push(s, ParamMessage(itemId, quantity, MsgStdThrowAway));
            Push(s, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
        }

        // charutils::AddItemToRecycleBin: the whole stack into the bin's first free slot, or when the bin is full, slot 1 is
        // destroyed, 2..10 slide down and the item lands in 10
        private void AddItemToRecycleBin(MapSession s, CharData c, Container container, byte slot, byte quantity)
        {
            var item = StorageItem(c, container, slot);
            if (item == null) return;
            if (IsBusy(c, item))
            {
                log_($"AddItemToRecycleBin: refusing to move busy item {item.ItemId} (char={c.Name})");
                return;
            }

            bool MoveRow(Container fromLoc, byte fromSlot, int toSlot)
            {
                var r = Exec("UPDATE char_inventory SET location = ?, slot = ? WHERE charid = ? AND location = ? AND slot = ? LIMIT 1",
                             BoundValue.Of((byte)Container.RecycleBin), BoundValue.Of((byte)toSlot), BoundValue.Of(c.Id),
                             BoundValue.Of((byte)fromLoc), BoundValue.Of(fromSlot));
                return r != null && r.RowsAffected() != 0;
            }

            const Container bin = Container.RecycleBin;
            if (FreeSlots(c, bin) > 0)
            {
                int newSlot = 1;
                while (newSlot <= c.StorageSize[(int)bin] && c.FindItem(bin, (byte)newSlot) != null) newSlot++;
                if (newSlot > c.StorageSize[(int)bin] || !MoveRow(container, slot, newSlot)) return;

                item.Location = bin;
                item.Slot = (byte)newSlot;
                Push(s, GameOkPackets.EmptySlotAttr(container, slot));
                Push(s, GameOkPackets.ItemAttr(item, ItemLockFlg(c, item)));
                Push(s, ParamMessage(item.ItemId, quantity, MsgStdThrowAway));
                OnItemDropScript(c, item, true);
            }
            else
            {
                var evicted = c.FindItem(bin, 1);
                if (evicted != null) c.Items.Remove(evicted);
                Exec("DELETE FROM char_inventory WHERE charid = ? AND location = ? AND slot = ? LIMIT 1",
                     BoundValue.Of(c.Id), BoundValue.Of((byte)bin), BoundValue.Of((byte)1));
                if (evicted != null) OnItemDropScript(c, evicted);

                for (int i = 2; i <= RecycleBinSlots; i++)
                {
                    if (c.FindItem(bin, (byte)i) is not Item binItem) continue;
                    binItem.Slot = (byte)(i - 1);
                    if (!MoveRow(bin, (byte)i, i - 1)) log_($"Problem moving Recycle Bin items! ({c.Name} - {item.ItemId})");
                }

                item.Location = bin;
                item.Slot = RecycleBinSlots;
                if (!MoveRow(container, slot, RecycleBinSlots)) log_($"Problem moving Recycle Bin items! ({c.Name} - {item.ItemId})");

                Push(s, GameOkPackets.EmptySlotAttr(container, slot));
                for (int i = 1; i <= RecycleBinSlots; i++)
                    Push(s, c.FindItem(bin, (byte)i) is Item binItem ? GameOkPackets.ItemAttr(binItem, ItemLockFlg(c, binItem))
                                                                    : GameOkPackets.EmptySlotAttr(bin, (byte)i));
                Push(s, ParamMessage(item.ItemId, quantity, MsgStdThrowAway));
                OnItemDropScript(c, item, true);
            }
            Push(s, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
        }

        // charutils::EmptyRecycleBin, as LoadChar does on every zone in: each item's onItemDrop, then the bin is cleared
        private void EmptyRecycleBin(CharData c)
        {
            var binned = c.Items.FindAll(i => i.Location == Container.RecycleBin);
            foreach (var item in binned) OnItemDropScript(c, item);
            Exec("DELETE FROM char_inventory WHERE charid = ? AND location = 17", BoundValue.Of(c.Id));
            c.Items.RemoveAll(i => i.Location == Container.RecycleBin);
        }

        // charutils::ClearTempItems: the temporary items' rows and the container emptied (CItemContainer::Clear)
        private void ClearTempItems(CharData c)
        {
            Exec("DELETE FROM char_inventory WHERE charid = ? AND location = 3", BoundValue.Of(c.Id));
            c.Items.RemoveAll(i => i.Location == Container.TempItems);
        }

        // MapNetwork's end of a datagram for the session's char
        public void AfterParse(MapSession s)
        {
            if (s.Char is not CharData c) return;
            // map_networking parse: a gear change's retriggerLatents rechecks every worn slot before the flush
            if (c.Latents.RetriggerLatents)
            {
                for (byte slot = 0; slot < 16; slot++)
                    if (c.Equipped[slot] != null) c.Latents.CheckLatentsEquip(slot);
                c.Latents.RetriggerLatents = false;
            }
            FlushEquipChanges(s, c);
            SendUpdateMask(s, c);
            if (c.EquipDirty)
            {
                SaveCharEquip(c);
                SaveCharAppearance(c);
                c.EquipDirty = false;
            }
        }

        // CCharEntity::flushEquipChanges: each change's EQUIP_LIST and look, then per touched container the items that
        // moved and (once synced at zone-in) the container's ITEM_SAME pair, then the spell and ability lists. Charged
        // items go as ITEM_ATTR without their charge and reuse attributes.
        private void FlushEquipChanges(MapSession s, CharData c)
        {
            if (c.EquipChanges.Count == 0) return;

            foreach (var change in c.EquipChanges)
            {
                Push(s, change.Equipping
                    ? LoginPackets.EquipList(change.ContainerSlot, change.EquipSlot, change.Container)
                    : LoginPackets.EquipList(0, change.EquipSlot, Container.Inventory));
                Push(s, LoginPackets.GrapList(c));
            }

            foreach (var container in c.DirtyContainers)
            {
                foreach (var change in c.EquipChanges)
                {
                    if (change.Item.Location != container) continue;
                    bool charged = loader_.Stats?.Items.Equipment(change.Item.ItemId)?.Charged == true;
                    byte lockFlg = (byte)(CharStats.IsEquipped(c, change.Item) ? 5 : 0);
                    Push(s, charged ? GameOkPackets.ItemAttr(change.Item, lockFlg)
                                    : LoginPackets.ItemList(change.Item, (byte)(change.Equipping ? 5 : 0)));
                }
                if ((c.SyncedContainers & 1u << (int)container) != 0)
                {
                    Push(s, GameOkPackets.ItemSame(0, (int)container, c.SyncedContainers));
                    Push(s, GameOkPackets.ItemSame(1, 0x12, c.SyncedContainers));
                }
            }

            Push(s, LoginPackets.MagicData(c));
            Push(s, LoginPackets.CommandData(c));
            c.EquipChanges.Clear();
            c.DirtyContainers.Clear();
        }

        // CCharEntity::PostTick's updatemask: a 0x00D update to the chars that see this one, the party HP list for
        // UPDATE_HP (every member of the party or alliance on this process, or just the char), then 0x037 for anything
        // but a position change. The C++ sends it from PostTick on the 400 ms zone tick, so its 250 ms throttle never
        // holds one back; here each datagram's changes go at once, merged into any 0x00D still pending.
        private void SendUpdateMask(MapSession s, CharData c)
        {
            if (c.UpdateMask == 0) return;
            if (s.InZone) BroadcastCharUpdate(c, c.UpdateMask);
            if ((c.UpdateMask & CharStats.UpdateHp) != 0)
                foreach (var m in ForAlliance(c))
                    if (m == c) Push(s, LoginPackets.GroupAttr(c));
                    else PushTo(m, LoginPackets.GroupAttr(c));
            if (c.UpdateMask != CharStats.UpdatePos) Push(s, CharStatus(c));
            c.UpdateMask = 0;
        }

        // charutils::SaveCharEquip: the slots as they are now, the linkshells' included
        private void SaveCharEquip(CharData c)
        {
            Exec("DELETE FROM char_equip WHERE charid = ?", BoundValue.Of(c.Id));
            foreach (var e in c.Equip)
                Exec("INSERT INTO char_equip (charid, equipslotid, slotid, containerid) VALUES (?,?,?,?)",
                     BoundValue.Of(c.Id), BoundValue.Of(e.EquipSlot), BoundValue.Of(e.InvSlot), BoundValue.Of((byte)e.Container));
        }
    }
}
