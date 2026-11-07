using System;
using System.Collections.Generic;
using XI.Persistence;

namespace XI.Zone
{
    // items/transaction.cpp: the inventory mutations the item handlers go through (applyItemUpdate, applyAddItem,
    // restoreItem), each writing char_inventory and telling the client, and ItemClaimTransaction, whose steps are undone
    // unless it is committed
    public sealed partial class ZoneHost
    {
        // CItemContainer::GetItem: nothing past the container's size
        private static Item StorageItem(CharData c, Container loc, byte slot) =>
            slot <= c.StorageSize[(int)loc] ? c.FindItem(loc, slot) : null;

        // CItem::isBusy: of the item states Equipped, Bazaar, InTransaction and PlacedFurniture are ported
        private static bool IsBusy(CharData c, Item item) =>
            item.InTransaction || item.InBazaar || item.PlacedFurniture || CharStats.IsEquipped(c, item);

        private uint StackSize(ushort id) => ItemBasic(id)?.StackSize ?? 1;

        // xi::items::clone: the CItemEquipment copy keeps its own mods (an augmented item's), its ITEM_AUGMENTED
        // subtype and a weapon's own unlock total (m_wsunlockpoints)
        internal static Item CloneItem(Item item) => new Item
        {
            Location = item.Location, Slot = item.Slot, ItemId = item.ItemId, Quantity = item.Quantity,
            Signature = item.Signature, Extra = (byte[])item.Extra.Clone(), CharPrice = item.CharPrice, Furnishing = item.Furnishing,
            Equip = item.Equip?.CopyOwn(), Augmented = item.Augmented, UnlockPointsNeeded = item.UnlockPointsNeeded,
        };

        // applyItemUpdate: add `quantity` (negative takes) to the stack in loc/slot, at most up to its stack size. A busy
        // item is refused unless it is equipped ammo or the owning transaction holds it. A stack taken to nothing loses
        // its row and calls its script's onItemDrop.
        private (bool Applied, int Delta) ApplyItemUpdate(CharData c, Container loc, byte slot, int quantity, ItemTransaction owner)
        {
            var item = StorageItem(c, loc, slot);
            if (item == null)
            {
                log_($"UpdateItem: No item in slot {slot}");
                PushTo(c, GameOkPackets.EmptySlotAttr(loc, slot));
                return default;
            }
            if ((int)(item.Quantity + quantity) < 0)
            {
                log_($"UpdateItem: {c.Name} trying to move invalid quantity {quantity} of itemID {item.ItemId}");
                return default;
            }
            bool heldByOwner = owner?.Holds(item) == true;
            bool isEquippedAmmo = CharStats.EquippedItem(c, CharStats.SlotAmmo) == item;
            if (IsBusy(c, item) && !isEquippedAmmo && !heldByOwner)
            {
                log_($"UpdateItem: refusing to mutate busy item {item.ItemId} in state Equipped (loc={(int)loc}, slot={slot}, char={c.Name})");
                return default;
            }

            uint oldQuantity = item.Quantity;
            uint newQuantity = (uint)(oldQuantity + quantity);
            uint stack = StackSize(item.ItemId);
            if (newQuantity > stack)
            {
                newQuantity = stack;
                log_($"UpdateItem: {c.Name} could only take {newQuantity - oldQuantity} of the {quantity} added to item {item.ItemId}, the stack is full");
            }

            if (newQuantity > 0 || item.ItemId == GilItemId)
            {
                if (Exec("UPDATE char_inventory SET quantity = ? WHERE charid = ? AND location = ? AND slot = ?",
                         BoundValue.Of(newQuantity), BoundValue.Of(c.Id), BoundValue.Of((byte)loc), BoundValue.Of(slot)) == null)
                {
                    log_($"UpdateItem: could not write quantity {newQuantity} of item {item.ItemId} for {c.Name}");
                    return default;
                }
                item.Quantity = newQuantity;
                PushTo(c, EventPackets.ItemNum(loc, slot, newQuantity));
            }
            else
            {
                if (Exec("DELETE FROM char_inventory WHERE charid = ? AND location = ? AND slot = ?",
                         BoundValue.Of(c.Id), BoundValue.Of((byte)loc), BoundValue.Of(slot)) == null)
                {
                    log_($"UpdateItem: could not delete item {item.ItemId} for {c.Name}");
                    return default;
                }
                c.Items.Remove(item);
                PushTo(c, GameOkPackets.EmptySlotAttr(loc, slot));
                OnItemDropScript(c, item);
                // an equipped item consumed to 0: the queued equip changes are dropped and the equipment resynced
                if (c.EquipChanges.Exists(change => change.Item == item))
                {
                    c.EquipChanges.Clear();
                    ResyncEquipment(c);
                }
            }
            return (true, (int)newQuantity - (int)oldQuantity);
        }

        // applyAddItem(itemId): a new stack of `quantity` (clamped to the stack size) when the container has a free slot,
        // signed (setSignature) when given a signature, then given addItem's appraisal and exdata by `setup`
        private int ApplyAddItem(CharData c, Container loc, ushort id, uint quantity, bool silent, ItemTransaction owner, out int applied,
                                 string signature = "", Action<Item> setup = null)
        {
            applied = 0;
            if (FreeSlots(c, loc) == 0 || quantity == 0) return -1;
            var basic = ItemBasic(id);
            if (basic == null)
            {
                log_($"AddItem: Item <{id}> is not found in a database");
                return -1;
            }
            var item = new Item { ItemId = id, Quantity = Math.Min(quantity, basic.Value.StackSize) };
            // itemutils' template: a usable item comes with all its charges
            if (Usable(id) is { MaxCharges: > 0 } usable) item.Extra[1] = usable.MaxCharges;
            item.Furnishing = loader_.Stats?.Items.Furnishing(id) != null;
            if (signature.Length > 0) ItemSignature.Set(item, signature);
            // what the caller sets on the spawned item before it is given (addItem's appraisal and exdata)
            setup?.Invoke(item);
            return ApplyAddItem(c, loc, item, silent, owner, out applied);
        }

        // applyAddItem(item): gil merges into slot 0; a rare item the char already holds is refused (with "You cannot
        // obtain the item" unless silent); anything else takes the container's first free slot. Returns the slot or -1.
        private int ApplyAddItem(CharData c, Container loc, Item item, bool silent, ItemTransaction owner, out int applied)
        {
            applied = 0;
            item.Furnishing = loader_.Stats?.Items.Furnishing(item.ItemId) != null;
            if (item.ItemId == GilItemId)
            {
                var mutation = ApplyItemUpdate(c, loc, 0, (int)item.Quantity, owner);
                if (!mutation.Applied)
                {
                    log_($"AddItem: could not give {item.Quantity} currency to {c.Name}");
                    return -1;
                }
                applied = mutation.Delta;
                return 0;
            }
            if (((ItemBasic(item.ItemId)?.Flags ?? 0) & ItemFlagRare) != 0 && HasItem(c, item.ItemId))
            {
                if (!silent) PushTo(c, StdMessage(c, MsgStdItemEx));
                return -1;
            }

            // CItemContainer::InsertItem: the first empty slot from 1
            int size = c.StorageSize[(int)loc];
            if (FreeSlots(c, loc) == 0)
            {
                log_($"AddItem: Location {(int)loc} is full");
                return -1;
            }
            int slot = 1;
            while (slot <= size && c.FindItem(loc, (byte)slot) != null) slot++;
            if (slot > size)
            {
                log_($"AddItem: Location {(int)loc} is full");
                return -1;
            }
            return InsertItemRow(c, loc, (byte)slot, item, "AddItem", out applied);
        }

        // restoreItem: put a taken stack back in its own slot, or wherever it fits when that slot has filled since
        private int RestoreItem(CharData c, Container loc, byte slot, Item item, ItemTransaction owner)
        {
            if (StorageItem(c, loc, slot) != null || slot > c.StorageSize[(int)loc])
            {
                log_($"RestoreItem: slot {slot} of {c.Name} is taken, putting item {item.ItemId} wherever it fits");
                return ApplyAddItem(c, loc, item, false, owner, out _);
            }
            return InsertItemRow(c, loc, slot, item, "RestoreItem", out _);
        }

        // the container entry, its char_inventory row, then ITEM_ATTR and ITEM_SAME
        private int InsertItemRow(CharData c, Container loc, byte slot, Item item, string caller, out int applied)
        {
            applied = 0;
            item.Location = loc;
            item.Slot = slot;
            c.Items.Add(item);
            if (Exec("INSERT INTO char_inventory(charid, location, slot, itemId, quantity, signature, extra) VALUES(?, ?, ?, ?, ?, ?, ?) LIMIT 1",
                     BoundValue.Of(c.Id), BoundValue.Of((byte)loc), BoundValue.Of(slot), BoundValue.Of(item.ItemId),
                     BoundValue.Of(item.Quantity), BoundValue.Of(item.Signature), BoundValue.Of(item.Extra)) == null)
            {
                log_($"{caller}: Cannot insert item to database");
                c.Items.Remove(item);
                return -1;
            }
            applied = (int)item.Quantity;
            PushTo(c, GameOkPackets.ItemAttr(item, ItemLockFlg(c, item)));
            PushTo(c, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
            return slot;
        }

        // ItemClaimTransaction: claimed stacks are its own to change, and each step records how to undo it, which happens
        // when it is disposed without a commit
        internal sealed class ItemTransaction : IDisposable
        {
            private readonly ZoneHost host_;
            private readonly CharData c_;
            private readonly List<Item> claims_ = new List<Item>();
            private readonly List<Action> undos_ = new List<Action>();
            private bool open_ = true;

            public ItemTransaction(ZoneHost host, CharData c)
            {
                host_ = host;
                c_ = c;
            }

            public bool Holds(Item item) => claims_.Contains(item);

            public IReadOnlyList<Item> Claims => claims_;

            // Transaction::claim: gil is never claimed, and a busy stack cannot be; a claimed stack is InTransaction
            // (busy to everything else) until the transaction ends or releases it
            public Item ClaimSlot(Container loc, byte slot)
            {
                var item = StorageItem(c_, loc, slot);
                if (item == null) return null;
                if (item.ItemId == GilItemId || Holds(item)) return item;
                if (IsBusy(c_, item)) return null;
                claims_.Add(item);
                item.InTransaction = true;
                return item;
            }

            // Transaction::release: the stack goes back to the char before the transaction ends
            public void Release(Item item)
            {
                if (claims_.Remove(item)) item.InTransaction = false;
            }

            private void DropClaims()
            {
                foreach (var item in claims_) item.InTransaction = false;
                claims_.Clear();
            }

            // take: a whole stack is kept so the undo can put it back as it was (exdata and signature included)
            public bool Take(Container loc, byte slot, uint quantity)
            {
                var item = StorageItem(c_, loc, slot);
                if (item == null || item.Quantity < quantity) return false;
                var restore = item.Quantity == quantity ? CloneItem(item) : null;
                if (!host_.ApplyItemUpdate(c_, loc, slot, -(int)quantity, this).Applied) return false;
                if (restore == null)
                {
                    undos_.Add(() =>
                    {
                        if (!host_.ApplyItemUpdate(c_, loc, slot, (int)quantity, this).Applied)
                            host_.log_($"Transaction: could not put {quantity} back for {c_.Name} in slot {slot}");
                    });
                    return true;
                }
                undos_.Add(() =>
                {
                    if (host_.RestoreItem(c_, loc, slot, CloneItem(restore), this) < 0)
                        host_.log_($"Transaction: {c_.Name} has nowhere to put {restore.ItemId} back");
                });
                return true;
            }

            // give: the undo takes back what landed, which is less than asked when the stack size capped it
            public int? Give(Container loc, ushort id, uint quantity, bool silent = false)
            {
                int slot = host_.ApplyAddItem(c_, loc, id, quantity, silent, this, out int applied);
                if (slot < 0) return null;
                undos_.Add(() =>
                {
                    if (!host_.ApplyItemUpdate(c_, loc, (byte)slot, -applied, this).Applied)
                        host_.log_($"Transaction: could not take back {applied} handed to {c_.Name} in slot {slot}");
                });
                return slot;
            }

            // give(item): a stack carrying its own exdata and signature (a traded item); the undo takes back what
            // landed. The slot it landed in, or null.
            public int? GiveItem(Container loc, Item item, bool silent = false)
            {
                int slot = host_.ApplyAddItem(c_, loc, item, silent, this, out int applied);
                if (slot < 0) return null;
                undos_.Add(() =>
                {
                    if (!host_.ApplyItemUpdate(c_, loc, (byte)slot, -applied, this).Applied)
                        host_.log_($"Transaction: could not take back {applied} handed to {c_.Name} in slot {slot}");
                });
                return slot;
            }

            // mergeInto: false when the stack limit kept some of it out
            public bool MergeInto(Container loc, byte slot, uint quantity)
            {
                var merged = host_.ApplyItemUpdate(c_, loc, slot, (int)quantity, this);
                if (!merged.Applied) return false;
                undos_.Add(() =>
                {
                    if (!host_.ApplyItemUpdate(c_, loc, slot, -merged.Delta, this).Applied)
                        host_.log_($"Transaction: could not unmerge {merged.Delta} from {c_.Name} in slot {slot}");
                });
                return merged.Delta == (int)quantity;
            }

            // split: take first, since giving first would leave both halves if the take then failed
            public bool Split(Container fromLoc, byte fromSlot, Container toLoc, uint quantity)
            {
                var item = StorageItem(c_, fromLoc, fromSlot);
                if (item == null) return false;
                ushort id = item.ItemId;
                return Take(fromLoc, fromSlot, quantity) && Give(toLoc, id, quantity).HasValue;
            }

            public bool MoveBetween(Container fromLoc, byte fromSlot, Container toLoc, byte toSlot, uint quantity) =>
                Take(fromLoc, fromSlot, quantity) && MergeInto(toLoc, toSlot, quantity);

            // pay: gil is the inventory's slot 0
            public bool Pay(uint gil) => Take(Container.Inventory, 0, gil);

            // earn: false when the gil stack limit kept some of it out
            public bool Earn(uint gil)
            {
                var earned = host_.ApplyItemUpdate(c_, Container.Inventory, 0, (int)gil, this);
                if (!earned.Applied) return false;
                undos_.Add(() =>
                {
                    if (!host_.ApplyItemUpdate(c_, Container.Inventory, 0, -earned.Delta, this).Applied)
                        host_.log_($"Transaction: could not take back {earned.Delta} gil from {c_.Name}");
                });
                return earned.Delta == (int)gil;
            }

            // a step outside the char's items (a row written ahead of the take), undone with the rest
            public void UndoWith(Action undo) => undos_.Add(undo);

            public bool Commit()
            {
                if (!open_) return false;
                DropClaims();
                undos_.Clear();
                open_ = false;
                return true;
            }

            // rollback: the undos newest first, then the claims are dropped
            public void Dispose()
            {
                if (!open_) return;
                for (int i = undos_.Count - 1; i >= 0; i--) undos_[i]();
                undos_.Clear();
                DropClaims();
                open_ = false;
            }
        }
    }
}
