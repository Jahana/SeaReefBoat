using System;
using System.Collections.Generic;
using XI.Map;
using XI.Persistence;

namespace XI.Zone
{
    // CCharEntity::BazaarID and BazaarCustomers entries: an EntityId resolved by its zone and act index, whose UniqueNo
    // the caller then checks
    public struct BazaarRef
    {
        public uint UniqueNo;
        public ushort ActIndex;
        public ushort Zone;

        public BazaarRef(CharData c)
        {
            UniqueNo = c.Id;
            ActIndex = c.TargId;
            Zone = c.Zone;
        }

        public void Clean() => this = default;
    }

    // Bazaars: c2s 0x104 - 0x106 (browsing and buying) and 0x109 - 0x10B (setting prices), and
    // BazaarPurchaseTransaction, with the debug log lines (DebugBazaarsFmt, logged under logging.DEBUG_BAZAARS; the
    // bazaar message's is in ZoneHost.Inspect.cs). The Monstrosity block has no state here, so it never refuses.
    public sealed partial class ZoneHost
    {
        private const uint BazaarMaxPrice = 99999999;

        // GP_BAZAAR_BUY_STATE and GP_SERV_COMMAND_BAZAAR_SHOPPING_STATE
        private const uint BazaarBuyOk = 0, BazaarBuyErr = 1;
        private const uint BazaarShoppingEnter = 0, BazaarShoppingExit = 1;

        private bool AuditBazaar => loader_.Stats?.Settings.AuditPlayerBazaar == true;

        // DebugBazaarsFmt: a line only while logging.DEBUG_BAZAARS is on; the callers ask first, so a line (and its
        // item name lookup) is only built when it is logged
        private bool DebugBazaarsOn => loader_.Stats?.Settings.DebugBazaars == true;

        // CItem::getName: the item_basic name
        private string BazaarItemName(Item item) => LookupItem(item.ItemId)?.Name ?? "";

        private void RegisterBazaarHandlers()
        {
            Register(0x104, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent), (s, c, p) => OnBazaarExit(c));
            Register(0x105, (s, c, p) => Pv(c)
                    .BlockedBy(BlockedState.InEvent)
                    .MustEqual(c.BazaarId.UniqueNo, 0, "Character already has a Bazaar ID")
                    .MustEqual(c.BazaarId.ActIndex, 0, "Character already has a Bazaar Target ID"),
                OnBazaarList);
            Register(0x106, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent).Range("BuyNum", U32(p, 8), 1, 99), OnBazaarBuy);
            Register(0x109, (s, c, p) => Pv(c)
                    .BlockedBy(BlockedState.InEvent)
                    .MustEqual(c.IsSettingBazaarPrices ? 1 : 0, 1, "isSettingBazaarPrices not true"),
                (s, c, p) =>
                {
                    c.IsSettingBazaarPrices = false;
                    c.UpdateMask |= CharStats.UpdateHp;
                });
            Register(0x10A, (s, c, p) => Pv(c)
                    .BlockedBy(BlockedState.InEvent)
                    .MustEqual(c.IsSettingBazaarPrices ? 1 : 0, 1, "isSettingBazaarPrices not true")
                    .Range("Price", U32(p, 8), 0, BazaarMaxPrice),
                OnBazaarItemSet);
            // FFXIclopedia: a Monipulator cannot set up a bazaar
            Register(0x10B, (s, c, p) => Pv(c)
                    .BlockedBy(BlockedState.InEvent | BlockedState.Monstrosity)
                    .MustEqual(U32(p, 4), 0, "AllListClearFlg not 0"),
                (s, c, p) => OnBazaarClose(c));
        }

        // EntityId::resolve for a char: whoever holds the act index in that zone now, or null
        private CharData ResolveBazaarRef(BazaarRef r)
        {
            if (r.ActIndex == 0) return null;
            foreach (var (o, os) in charSessions_)
                if (os.InZone && o.Zone == r.Zone && o.TargId == r.ActIndex) return o;
            return null;
        }

        // the char being browsed, when BazaarID still names it
        private CharData BazaarSeller(CharData c) =>
            ResolveBazaarRef(c.BazaarId) is CharData target && target.Id == c.BazaarId.UniqueNo ? target : null;

        // a GM hidden from the seller does not show up as a shopper
        private static bool ShowsAsShopper(CharData shopper, CharData seller) =>
            !shopper.GmHidden || seller.AccountGmLevel >= shopper.AccountGmLevel;

        // GP_CLI_COMMAND_BAZAAR_EXIT
        private void OnBazaarExit(CharData c)
        {
            if (ResolveBazaarRef(c.BazaarId) is not CharData target) return;
            if (target.Id == c.BazaarId.UniqueNo)
            {
                target.BazaarCustomers.RemoveAll(customer => customer.UniqueNo == c.Id);
                if (ShowsAsShopper(c, target)) PushTo(target, BazaarShoppingPacket(c, BazaarShoppingExit));
                if (DebugBazaarsOn)
                    log_($"Bazaar Interaction [Leave Bazaar] - Buyer: {c.Name}, Seller: {target.Name}");
            }
            c.BazaarId.Clean();
        }

        // GP_CLI_COMMAND_BAZAAR_LIST: UniqueNo names the seller, or the char's target when 0 (which then never matches)
        private void OnBazaarList(MapSession s, CharData c, BasicPacket p)
        {
            uint uniqueNo = U32(p, 4);
            CharData target = null;
            if (uniqueNo != 0)
            {
                if (uniqueNo == c.Id) target = c;
                else
                    foreach (var (o, _) in ZoneChars(c))
                        if (o.Id == uniqueNo) { target = o; break; }
            }
            else target = CharByTargId(c, c.FaceTarget);

            if (target == null || target.Id != uniqueNo || !target.HasBazaar || target.MoghouseId != c.MoghouseId) return;

            c.BazaarId = new BazaarRef(target);
            if (ShowsAsShopper(c, target)) PushTo(target, BazaarShoppingPacket(c, BazaarShoppingEnter));
            target.BazaarCustomers.Add(new BazaarRef(c));

            ushort tax = AreaOf(c).Tax;
            foreach (var item in BazaarListings(target))
                Push(s, BazaarListPacket(item, item.Slot, tax));
            if (DebugBazaarsOn)
                log_($"Bazaar Interaction [View Wares] - Buyer: {c.Name}, Seller: {target.Name}");
        }

        // the seller's priced inventory stacks, in slot order
        private static List<Item> BazaarListings(CharData seller)
        {
            var items = seller.Items.FindAll(i => i.Location == Container.Inventory && i.CharPrice != 0 &&
                                                  i.Slot <= seller.StorageSize[(int)Container.Inventory]);
            items.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            return items;
        }

        // GP_CLI_COMMAND_BAZAAR_BUY
        private void OnBazaarBuy(MapSession s, CharData c, BasicPacket p)
        {
            byte slot = p.Buffer[4];
            uint buyNum = U32(p, 8);

            if (ResolveBazaarRef(c.BazaarId) is not CharData seller || seller.Id != c.BazaarId.UniqueNo) return;
            if (StorageItem(seller, Container.Inventory, slot) is not Item listing) return;

            if (c.Id == seller.Id || FreeSlots(c, Container.Inventory) == 0)
            {
                Push(s, BazaarBuyPacket(seller, BazaarBuyErr));
                if (DebugBazaarsOn)
                {
                    if (c.Id == seller.Id)
                        log_($"Bazaar Interaction [Purchase Failed / Self Bazaar] - Character: {c.Name}, " +
                             $"Item: {BazaarItemName(listing)}");
                    if (FreeSlots(c, Container.Inventory) == 0)
                        log_($"Bazaar Interaction [Purchase Failed / Inventory Full] - Buyer: {c.Name}, " +
                             $"Seller: {seller.Name}, Item: {BazaarItemName(listing)}");
                }
                return;
            }

            var gil = StorageItem(c, Container.Inventory, 0);
            if (gil == null)
            {
                Push(s, BazaarBuyPacket(seller, BazaarBuyErr));
                return;
            }

            if (listing.CharPrice == 0 || listing.Quantity < buyNum)
            {
                Push(s, BazaarBuyPacket(seller, BazaarBuyErr));
                return;
            }

            ushort tax = AreaOf(c).Tax;
            ulong basePrice = (ulong)listing.CharPrice * buyNum;
            ulong totalPrice = tax * basePrice / 10000 + basePrice;
            if (totalPrice > uint.MaxValue)
            {
                log_($"Bazaar Interaction [Price Overflow] - Buyer: {c.Name}, Seller: {seller.Name}, Price: {totalPrice}");
                Push(s, BazaarBuyPacket(seller, BazaarBuyErr));
                return;
            }
            uint price = (uint)basePrice, priceWithTax = (uint)totalPrice;

            if (gil.Quantity < priceWithTax)
            {
                Push(s, BazaarBuyPacket(seller, BazaarBuyErr));
                // exploit attempt
                log_($"Bazaar Interaction [Insufficient Gil] - Buyer: {c.Name}, Seller: {seller.Name}, Buyer Gil: {gil.Quantity}, Price: {priceWithTax}");
                return;
            }

            int? deliveredSlot = BazaarPurchase(c, seller, slot, buyNum, price, priceWithTax);
            if (deliveredSlot == null)
            {
                Push(s, BazaarBuyPacket(seller, BazaarBuyErr));
                return;
            }

            var bought = StorageItem(c, Container.Inventory, (byte)deliveredSlot.Value);
            if (AuditBazaar)
            {
                // written in line rather than on a worker thread
                if (Exec("INSERT INTO audit_bazaar(itemid, quantity, seller, seller_name, purchaser, purchaser_name, price, date) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                         BoundValue.Of((uint)bought.ItemId), BoundValue.Of(buyNum), BoundValue.Of(seller.Id), BoundValue.Of(seller.Name),
                         BoundValue.Of(c.Id), BoundValue.Of(c.Name), BoundValue.Of(priceWithTax), BoundValue.Of((uint)UnixNow())) == null)
                    log_($"Failed to log bazaar purchase (ItemID: {bought.ItemId}, Quantity: {buyNum}, Seller: {seller.Name}, Purchaser: {c.Name}, Price: {priceWithTax})");
            }

            Push(s, BazaarBuyPacket(seller, BazaarBuyOk));
            PushTo(seller, BazaarSalePacket(c, bought));

            var remaining = StorageItem(seller, Container.Inventory, slot);
            PushTo(seller, remaining != null ? GameOkPackets.ItemAttr(remaining, ItemLockFlg(seller, remaining))
                                             : GameOkPackets.EmptySlotAttr(Container.Inventory, slot));
            PushTo(seller, GameOkPackets.ItemSame(1, CharData.MaxContainers, seller.SyncedContainers));
            if (DebugBazaarsOn)
                log_($"Bazaar Interaction [Purchase Successful] - Buyer: {c.Name}, Seller: {seller.Name}, " +
                     $"Item: {BazaarItemName(bought)}, Qty: {buyNum}, Cost: {priceWithTax}");

            bool bazaarIsEmpty = BazaarListings(seller).Count == 0;
            foreach (var customerRef in seller.BazaarCustomers)
            {
                if (ResolveBazaarRef(customerRef) is not CharData customer || customer.Id != customerRef.UniqueNo) continue;
                if (customer.Id != c.Id) PushTo(customer, BazaarSellPacket(c, slot, buyNum));
                PushTo(customer, BazaarListPacket(remaining, slot, tax));
                if (bazaarIsEmpty)
                {
                    PushTo(customer, BazaarClosePacket(seller));
                    if (DebugBazaarsOn)
                        log_($"Bazaar Interaction [Bazaar Emptied] - Buyer: {c.Name}, Seller: {seller.Name}");
                }
            }

            if (bazaarIsEmpty)
            {
                seller.UpdateMask |= CharStats.UpdateHp;
                // the seller's PostTick: its own datagrams are what flush its mask here
                if (SessionOf(seller) is MapSession sellerSession) SendUpdateMask(sellerSession, seller);
            }
        }

        // BazaarPurchaseTransaction: the listing comes off display and is claimed, then the buyer gets an unpriced copy
        // of what was bought, pays with tax, the seller earns the untaxed price and loses the stack, and what is left goes
        // back on display. A failed step undoes the earlier ones on both sides. The delivered slot, or null.
        private int? BazaarPurchase(CharData buyer, CharData seller, byte slot, uint quantity, uint price, uint priceWithTax)
        {
            if (buyer == seller) return null;
            using var buyerTx = new ItemTransaction(this, buyer);
            using var sellerTx = new ItemTransaction(this, seller);

            // claimListing: a listed item is Bazaar, which has to come off before it can be claimed
            var listing = StorageItem(seller, Container.Inventory, slot);
            if (listing == null) return null;
            listing.InBazaar = false;
            if (sellerTx.ClaimSlot(Container.Inventory, slot) == null)
            {
                RestoreBazaarDisplay(seller, slot);
                return null;
            }

            var clone = CloneItem(listing);
            clone.CharPrice = 0;
            clone.Quantity = quantity;

            int? delivered = buyerTx.GiveItem(Container.Inventory, clone);
            bool committed = delivered != null;
            if (committed && !buyerTx.Pay(priceWithTax))
            {
                log_($"BazaarPurchaseTransaction: {buyer.Name} could not pay {priceWithTax}");
                committed = false;
            }
            if (committed && !sellerTx.Earn(price))
            {
                log_($"BazaarPurchaseTransaction: {seller.Name} was not paid {price} for the sale");
                committed = false;
            }
            if (committed && !sellerTx.Take(Container.Inventory, slot, quantity))
            {
                log_($"BazaarPurchaseTransaction: {seller.Name} kept item in slot {slot} after it was sold");
                committed = false;
            }

            if (!committed)
            {
                // rollback: the undos, then the display comes back
                buyerTx.Dispose();
                sellerTx.Dispose();
                RestoreBazaarDisplay(seller, slot);
                return null;
            }

            sellerTx.Release(listing);
            RestoreBazaarDisplay(seller, slot);
            buyerTx.Commit();
            sellerTx.Commit();
            return delivered;
        }

        // restoreDisplay: looked up again because a full buyout leaves nothing to put back
        private void RestoreBazaarDisplay(CharData seller, byte slot)
        {
            var remaining = StorageItem(seller, Container.Inventory, slot);
            if (remaining == null || remaining.CharPrice == 0) return;
            if (IsBusy(seller, remaining) && !remaining.InBazaar)
            {
                log_($"BazaarPurchaseTransaction: could not put item {remaining.ItemId} back on display for {seller.Name}");
                return;
            }
            remaining.InBazaar = true;
        }

        // GP_CLI_COMMAND_BAZAAR_ITEMSET: a price of 0 takes the stack off display
        private void OnBazaarItemSet(MapSession s, CharData c, BasicPacket p)
        {
            byte slot = p.Buffer[4];
            uint price = U32(p, 8);
            if (StorageItem(c, Container.Inventory, slot) is not Item item) return;

            if (item.ItemId == GilItemId)
            {
                log_($"GP_CLI_COMMAND_BAZAAR_ITEMSET: {c.Name} trying to bazaar currency in slot {slot}");
                return;
            }
            if (IsBusy(c, item) && !item.InBazaar)
            {
                log_($"Player {c.Name} trying to bazaar a busy/reserved item! [Item: {item.ItemId} | Slot ID: {slot}] ");
                return;
            }
            if (((ItemBasic(item.ItemId)?.Flags ?? 0) & ItemFlagExclusive) != 0) return;

            // mark: Free always succeeds, Bazaar needs a Free item, which the busy check above leaves
            item.InBazaar = price != 0;

            Exec("UPDATE char_inventory SET bazaar = ? WHERE charid = ? AND location = 0 AND slot = ?",
                 BoundValue.Of(price), BoundValue.Of(c.Id), BoundValue.Of(slot));
            item.CharPrice = price;

            Push(s, GameOkPackets.ItemAttr(item, ItemLockFlg(c, item)));
            Push(s, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
            if (DebugBazaarsOn)
                log_($"Bazaar Interaction [Price Set] - Character: {c.Name}, Item: {BazaarItemName(item)}, " +
                     $"Price: {price}");
        }

        // GP_CLI_COMMAND_BAZAAR_CLOSE: the customers are shown out while the char sets prices
        private void OnBazaarClose(CharData c)
        {
            foreach (var customerRef in c.BazaarCustomers)
            {
                if (ResolveBazaarRef(customerRef) is CharData customer && customer.Id == customerRef.UniqueNo)
                {
                    PushTo(customer, BazaarClosePacket(c));
                    if (DebugBazaarsOn)
                        log_($"Bazaar Interaction [Leave Bazaar] - Buyer: {customer.Name}, Seller: {c.Name}");
                }
            }
            c.BazaarCustomers.Clear();
            c.IsSettingBazaarPrices = true;
            c.UpdateMask |= CharStats.UpdateHp;
            if (DebugBazaarsOn)
                log_($"Bazaar Interaction [Setting Prices] - Character: {c.Name}");
        }

        // ---- packets ------------------------------------------------------------------------------------------------

        // GP_SERV_COMMAND_BAZAAR_LIST (0x105); a null item clears the slot. Attr is the exdata, without the charged-item
        // override (as ItemAttr).
        private BasicPacket BazaarListPacket(Item item, byte slot, ushort tax)
        {
            var w = new PacketWriter(0x105, 44);
            w.U8(4 + 12, slot); // ItemIndex
            if (item != null)
            {
                w.U32(4 + 0, item.CharPrice); // Price
                w.U32(4 + 4, item.Quantity);  // ItemNum
                w.U16(4 + 8, tax);            // TaxRate
                w.U16(4 + 10, item.ItemId);   // ItemNo
                // Attr: a charged item's charges and recharge, else the exdata
                if (loader_.Stats?.Items.Equipment(item.ItemId) is { Charged: true } info)
                {
                    var attr = new byte[24];
                    ChargedAttr(attr, item, info, LoginPackets.VanadielTimestamp(UnixNow()));
                    w.Bytes(4 + 13, attr);
                }
                else w.Bytes(4 + 13, item.Extra);
            }
            return w.Packet;
        }

        // GP_SERV_COMMAND_BAZAAR_BUY (0x106): the seller's name
        private static BasicPacket BazaarBuyPacket(CharData seller, uint state)
        {
            var w = new PacketWriter(0x106, 24);
            w.U32(4 + 0, state);
            w.Name(4 + 4, seller.Name, 16);
            return w.Packet;
        }

        // GP_SERV_COMMAND_BAZAAR_CLOSE (0x107)
        private static BasicPacket BazaarClosePacket(CharData seller)
        {
            var w = new PacketWriter(0x107, 24);
            w.Name(4 + 0, seller.Name, 16);
            return w.Packet;
        }

        // GP_SERV_COMMAND_BAZAAR_SHOPPING (0x108): a shopper coming or going, HideLevel left 0
        private static BasicPacket BazaarShoppingPacket(CharData shopper, uint state)
        {
            var w = new PacketWriter(0x108, 32);
            w.U32(4 + 0, shopper.Id);       // UniqueNo
            w.U32(4 + 4, state);            // State
            w.U16(4 + 10, shopper.TargId);  // ActIndex
            w.Name(4 + 12, shopper.Name, 16);
            return w.Packet;
        }

        // GP_SERV_COMMAND_BAZAAR_SELL (0x109): another shopper bought from the bazaar being browsed. ItemNum is the
        // uint8 the constructor takes; the act indexes are left 0.
        private static BasicPacket BazaarSellPacket(CharData buyer, byte slot, uint quantity)
        {
            var w = new PacketWriter(0x109, 36);
            w.U32(4 + 0, buyer.Id);           // UniqueNo
            w.U32(4 + 4, (byte)quantity);     // ItemNum
            w.Name(4 + 12, buyer.Name, 16);
            w.U8(4 + 28, slot);               // ItemIndex
            return w.Packet;
        }

        // GP_SERV_COMMAND_BAZAAR_SALE (0x10A): to the seller, what the buyer now holds of the stack it got
        private static BasicPacket BazaarSalePacket(CharData buyer, Item bought)
        {
            var w = new PacketWriter(0x10A, 28);
            if (bought != null)
            {
                w.U32(4 + 0, bought.Quantity); // ItemNum
                w.U16(4 + 4, bought.ItemId);   // ItemNo
            }
            w.Name(4 + 6, buyer.Name, 16);
            return w.Packet;
        }
    }
}
