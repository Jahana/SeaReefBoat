using System;
using System.Text;
using XI.Map;
using XI.Persistence;

namespace XI.Zone
{
    // The C2S 0x04E GP_CLI_COMMAND_AUC handler (the auction house), auctionutils, and the S2C 0x04C
    // GP_SERV_COMMAND_AUC it answers with. auction_house rows are the listings: sale 0 while open, the price paid once
    // sold (the auction_house_buy trigger then sends the seller the gil through the delivery box). The sales status
    // window shows the char's AhHistory, reloaded from the open listings at most every 5 s.
    public sealed partial class ZoneHost
    {
        private const byte AuctionHouseId = 4; // Jeuno linked AH
        private const uint ItemFlagNoAuction = 0x00000040;
        private const long AhHistoryReloadMs = 5000;
        private const int AhHistoryShown = 7;

        // ---- 0x04C GP_SERV_COMMAND_AUC ----------------------------------------------------------------------------

        private static PacketWriter AucPacket(AucCommand action, sbyte aucWorkIndex, byte result)
        {
            var w = new PacketWriter(0x04C, 0x3C);
            w.U8(4, (byte)action);
            w.U8(5, (byte)aucWorkIndex);
            w.U8(6, result);
            return w;
        }

        // (action): the auction house is open
        private static BasicPacket AucOpen(AucCommand action) => AucPacket(action, -1, 1).Packet;

        // (action, PItem, quantity, price): the fee for listing the item, a single or (quantity 0) a stack
        private BasicPacket AucAskCommit(AucCommand action, Item item, byte quantity, uint price)
        {
            var w = AucPacket(action, -1, 1);
            w.U8(7, 0x02);
            w.U32(8, AuctionFee(quantity, price));
            w.U16(12, item.Slot);
            w.U16(14, item.ItemId);
            w.U32(16, quantity);
            w.U32(48, AuctionHouseId);
            return w.Packet;
        }

        // (action, slot, PChar): the sales status window's slot, filled from the char's history
        private static BasicPacket AucSlot(AucCommand action, byte slot, CharData c)
        {
            var w = AucPacket(action, (sbyte)slot, 1);
            if (slot < AhHistoryShown && slot < c.AhHistory.Count) AucParcel(w, c.AhHistory[slot]);
            return w.Packet;
        }

        // (action, message, itemid, price, quantity, stacksize): a bid's outcome; ItemStacks is the stack size for a
        // stack (quantity 0), else 1
        private static BasicPacket AucBid(AucCommand action, byte message, ushort itemId, uint price, byte quantity, byte stackSize)
        {
            var w = AucPacket(action, 0, message);
            w.U32(8, price);
            w.U16(12, itemId);
            w.U32(16, quantity == 0 ? stackSize : 1u);
            return w.Packet;
        }

        // (action, message, PChar, slot, keepItem): a cancel's outcome; a kept listing stays in the window, the char's
        // name with it
        private static BasicPacket AucKeep(AucCommand action, byte message, CharData c, byte slot, bool keepItem)
        {
            var w = AucPacket(action, (sbyte)slot, message);
            if (keepItem && slot < AhHistoryShown && slot < c.AhHistory.Count)
            {
                AucParcel(w, c.AhHistory[slot]);
                var name = Encoding.Latin1.GetBytes(c.Name);
                w.Bytes(24, name.AsSpan(0, Math.Min(name.Length, 16)));
            }
            return w.Packet;
        }

        // GP_AUC_BOX at @20: Stat, ItemIndex, ItemNo, ItemQuantity (1 - stack), ItemCategory, Price, MarketNo
        private static void AucParcel(PacketWriter w, (ushort ItemId, byte Stack, uint Price) listing)
        {
            w.U8(20, 0x03);
            w.U8(22, 0x01);
            w.U16(40, listing.ItemId);
            w.U8(42, (byte)(1 - listing.Stack));
            w.U8(43, 0x02);
            w.U32(44, listing.Price);
            w.U32(48, AuctionHouseId);
        }

        // AH_BASE_FEE + price * AH_TAX_RATE / 100 for a single or (itemStacks 0) a stack, capped at AH_MAX_FEE
        private uint AuctionFee(uint itemStacks, uint price)
        {
            var cfg = loader_.Stats?.Settings ?? new StatSettings();
            uint fee = itemStacks == 0
                ? (uint)(cfg.AhBaseFeeStacks + (price * cfg.AhTaxRateStacks / 100))
                : (uint)(cfg.AhBaseFeeSingle + (price * cfg.AhTaxRateSingle / 100));
            return Math.Min(fee, cfg.AhMaxFee);
        }

        // a charged item with charges spent
        private bool IsPartiallyUsed(Item item)
        {
            var info = loader_.Stats?.Items.Equipment(item.ItemId);
            return info?.Charged == true && item.Extra[1] < info.MaxCharges;
        }

        private static bool HasItemFlag(uint flags, uint flag) => (flags & flag) != 0;

        // ---- auctionutils ----------------------------------------------------------------------------------------

        // SellingItems (AskCommit): the fee for listing an inventory item
        private void SellingItems(CharData c, uint commission, ushort itemWorkIndex, ushort itemNo, uint itemStacks)
        {
            var item = StorageItem(c, Container.Inventory, (byte)itemWorkIndex);
            if (item == null) return;
            if (item.ItemId != itemNo || IsBusy(c, item) || HasItemFlag(ItemBasic(item.ItemId)?.Flags ?? 0, ItemFlagNoAuction)) return;
            if (IsPartiallyUsed(item))
            {
                PushTo(c, AucBid(AucCommand.AskCommit, 197, 0, 0, 0, 0));
                return;
            }
            PushTo(c, AucAskCommit(AucCommand.AskCommit, item, (byte)itemStacks, commission));
        }

        // OpenListOfSales (Info): reload the char's open listings, at most every 5 s
        private void OpenListOfSales(CharData c)
        {
            long now = NowMs();
            if (c.AhHistoryTimestampMs != long.MinValue && now <= c.AhHistoryTimestampMs + AhHistoryReloadMs)
            {
                PushTo(c, AucBid(AucCommand.Info, 246, 0, 0, 0, 0)); // try again in a little while
                return;
            }
            c.AhHistory.Clear();
            c.AhHistoryTimestampMs = now;
            PushTo(c, AucOpen(AucCommand.Info));
            var r = Exec("SELECT itemid, price, stack FROM auction_house WHERE seller = ? AND sale=0 ORDER BY id ASC LIMIT 7", BoundValue.Of(c.Id));
            while (r != null && r.Next())
                c.AhHistory.Add((r.Get<ushort>("itemid"), r.Get<byte>("stack"), r.Get<uint>("price")));
        }

        // RetrieveListOfItemsSoldByPlayer (WorkCheck): every history slot
        private void RetrieveListOfItemsSoldByPlayer(CharData c)
        {
            for (int slot = 0; slot < c.AhHistory.Count; slot++)
                PushTo(c, AucSlot(AucCommand.LotCancel, (byte)slot, c));
        }

        // ProofOfPurchase (LotIn): pay the fee, take the item (one, or a full stack) and list it
        private void ProofOfPurchase(CharData c, uint limitPrice, ushort itemWorkIndex, uint itemStacks)
        {
            using var t = new ItemTransaction(this, c);
            byte slot = (byte)itemWorkIndex;
            var item = t.ClaimSlot(Container.Inventory, slot);
            if (item == null || HasItemFlag(ItemBasic(item.ItemId)?.Flags ?? 0, ItemFlagNoAuction) || item.Quantity < itemStacks) return;
            if (IsPartiallyUsed(item))
            {
                PushTo(c, AucBid(AucCommand.LotIn, 197, 0, 0, 0, 0));
                return;
            }

            uint stackSize = StackSize(item.ItemId);
            if (itemStacks == 0 && (stackSize == 1 || stackSize != item.Quantity))
            {
                log_($"AH: Incorrect quantity of item {item.ItemId}");
                PushTo(c, AucBid(AucCommand.LotIn, 197, 0, 0, 0, 0)); // failed to place up
                return;
            }
            if (!t.Pay(AuctionFee(itemStacks, limitPrice)))
            {
                PushTo(c, AucBid(AucCommand.LotIn, 197, 0, 0, 0, 0)); // not enough gil to pay the fee
                return;
            }

            uint listings = 0;
            var r = Exec("SELECT COUNT(*) FROM auction_house WHERE seller = ? AND sale = 0", BoundValue.Of(c.Id));
            if (r != null && r.RowsCount() > 0 && r.Next()) listings = r.Get<uint>(0u);
            byte limit = loader_.Stats?.Settings?.AhListLimit ?? 7;
            if (limit != 0 && listings >= limit)
            {
                PushTo(c, AucBid(AucCommand.LotIn, 197, 0, 0, 0, 0)); // failed to place up
                return;
            }

            ushort itemId = item.ItemId;
            if (!t.Take(Container.Inventory, slot, itemStacks != 0 ? 1 : stackSize))
            {
                log_($"AH: Cannot take item {itemId} from {c.Name} to list it");
                PushTo(c, AucBid(AucCommand.LotIn, 197, 0, 0, 0, 0));
                return;
            }
            if (Exec("INSERT INTO auction_house(itemid, stack, seller, seller_name, date, price) VALUES(?, ?, ?, ?, ?, ?)",
                     BoundValue.Of(itemId), BoundValue.Of(itemStacks == 0), BoundValue.Of(c.Id), BoundValue.Of(c.Name),
                     BoundValue.Of((uint)UnixNow()), BoundValue.Of(limitPrice)) == null)
            {
                log_($"AH: Cannot insert item {itemId} to database, returning it");
                PushTo(c, AucBid(AucCommand.LotIn, 197, 0, 0, 0, 0));
                return;
            }
            if (!t.Commit()) return;

            PushTo(c, AucBid(AucCommand.LotIn, 1, 0, 0, 0, 0)); // merchandise put up on auction
            PushTo(c, AucSlot(AucCommand.LotCancel, (byte)listings, c));
        }

        // PurchasingItems (Bid): buy the cheapest open listing at or under the bid; the auction_house_buy trigger pays
        // the seller when the row is marked sold, so the sale and the payment unwind together
        private bool PurchasingItems(CharData c, uint bidPrice, ushort itemNo, uint itemStacks)
        {
            if (FreeSlots(c, Container.Inventory) == 0)
            {
                PushTo(c, AucBid(AucCommand.Bid, 0xE5, 0, 0, 0, 0));
                return false;
            }

            var basic = ItemBasic(itemNo);
            byte stacks = (byte)itemStacks;
            if (basic != null)
            {
                byte stackSize = (byte)basic.Value.StackSize;
                if (HasItemFlag(basic.Value.Flags, ItemFlagRare) && HasItem(c, itemNo))
                {
                    PushTo(c, AucBid(AucCommand.Bid, 0xE5, 0, 0, 0, 0));
                    return false;
                }
                var gil = StorageItem(c, Container.Inventory, 0);
                if (gil == null || gil.Quantity < bidPrice)
                {
                    PushTo(c, AucBid(AucCommand.Bid, 0xC5, itemNo, bidPrice, stacks, stackSize));
                    return false;
                }

                using var t = new ItemTransaction(this, c);
                uint boughtQuantity = itemStacks == 0 ? basic.Value.StackSize : 1;
                bool listingFound = false;
                bool ok = db_.Transaction(() =>
                {
                    var r = Exec("UPDATE auction_house SET buyer = ?, buyer_name = ?, sale = ?, sell_date = ? WHERE itemid = ? AND buyer_name IS NULL " +
                                 "AND stack = ? AND price <= ? ORDER BY price ASC, id ASC LIMIT 1",
                                 BoundValue.Of(c.Id), BoundValue.Of(c.Name), BoundValue.Of(bidPrice), BoundValue.Of((uint)UnixNow()),
                                 BoundValue.Of(itemNo), BoundValue.Of(itemStacks == 0), BoundValue.Of(bidPrice));
                    if (r == null) throw new InvalidOperationException($"AH: could not mark item {itemNo} sold to {c.Name} ({c.Id})");
                    if (r.RowsAffected() == 0) return;
                    listingFound = true;
                    if (!t.Pay(bidPrice))
                        throw new InvalidOperationException($"AH: {c.Name} ({c.Id}) bought item {itemNo} for {bidPrice} gil but cannot pay for it");
                    if (!t.Give(Container.Inventory, itemNo, boughtQuantity).HasValue)
                        throw new InvalidOperationException($"AH: {c.Name} ({c.Id}) bought item {itemNo} x{boughtQuantity} but it could not be added to their inventory");
                });
                if (ok && listingFound && t.Commit())
                {
                    PushTo(c, AucBid(AucCommand.Bid, 0x01, itemNo, bidPrice, stacks, stackSize));
                    PushTo(c, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
                    return true;
                }
            }

            // You were unable to buy the {qty} {item}
            PushTo(c, AucBid(AucCommand.Bid, 0xC5, itemNo, bidPrice, stacks, (byte)(basic?.StackSize ?? 0)));
            return false;
        }

        // CancelSale (LotCancel): take an open listing down and return the item
        private void CancelSale(CharData c, sbyte aucWorkIndex)
        {
            byte slot = (byte)aucWorkIndex;
            if (aucWorkIndex >= 0 && aucWorkIndex < c.AhHistory.Count)
            {
                var canceled = c.AhHistory[aucWorkIndex];
                var basic = ItemBasic(canceled.ItemId);
                bool isRareHeld = basic != null && HasItemFlag(basic.Value.Flags, ItemFlagRare) && HasItem(c, canceled.ItemId);
                if (FreeSlots(c, Container.Inventory) == 0 || isRareHeld)
                {
                    PushTo(c, AucKeep(AucCommand.LotCancel, 0xE5, c, slot, true));
                    return;
                }

                using var t = new ItemTransaction(this, c);
                bool listingFound = false;
                bool ok = db_.Transaction(() =>
                {
                    var r = Exec("DELETE FROM auction_house WHERE seller = ? AND itemid = ? AND stack = ? AND price = ? AND sale = 0 LIMIT 1",
                                 BoundValue.Of(c.Id), BoundValue.Of(canceled.ItemId), BoundValue.Of(canceled.Stack), BoundValue.Of(canceled.Price));
                    if (r == null) throw new InvalidOperationException($"AH: could not remove listing of item {canceled.ItemId} for {c.Name} ({c.Id})");
                    if (r.RowsAffected() == 0) return;
                    listingFound = true;
                    if (basic == null)
                        throw new InvalidOperationException($"AH: {c.Name} ({c.Id}) cancelled a listing of unknown item id {canceled.ItemId}");
                    uint returned = canceled.Stack != 0 ? basic.Value.StackSize : 1;
                    if (!t.Give(Container.Inventory, canceled.ItemId, returned, silent: true).HasValue)
                        throw new InvalidOperationException($"AH: {c.Name} ({c.Id}) cancelled item {canceled.ItemId} x{returned} but it could not be returned to their inventory");
                });
                if (ok && listingFound)
                {
                    if (t.Commit())
                    {
                        PushTo(c, AucKeep(AucCommand.LotCancel, 0, c, slot, false));
                        PushTo(c, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
                    }
                    return;
                }
            }

            PushTo(c, AucKeep(AucCommand.LotCancel, 0xE5, c, slot, true)); // inventory full, unable to remove
        }

        // ---- handler ---------------------------------------------------------------------------------------------

        private void RegisterAuctionHandlers()
        {
            Register(0x04E, (s, c, p) => ValidateAuc(c, p), (s, c, p) =>
            {
                sbyte aucWorkIndex = (sbyte)p.Buffer[5];
                switch ((AucCommand)p.Buffer[4])
                {
                    case AucCommand.AskCommit: SellingItems(c, U32(p, 8), U16(p, 12), U16(p, 14), U32(p, 16)); break;
                    case AucCommand.Info:
                        OpenListOfSales(c);
                        RetrieveListOfItemsSoldByPlayer(c); // falls through to WorkCheck
                        break;
                    case AucCommand.WorkCheck: RetrieveListOfItemsSoldByPlayer(c); break;
                    case AucCommand.LotIn: ProofOfPurchase(c, U32(p, 8), U16(p, 12), U32(p, 16)); break;
                    case AucCommand.Bid: PurchasingItems(c, U32(p, 8), U16(p, 12), U32(p, 16)); break;
                    case AucCommand.LotCancel: CancelSale(c, aucWorkIndex); break;
                    case AucCommand.LotCheck: PushTo(c, AucSlot(AucCommand.LotCheck, (byte)aucWorkIndex, c)); break; // UpdateSaleListByPlayer
                }
            });
        }

        private PacketValidator ValidateAuc(CharData c, BasicPacket p)
        {
            var cmd = (AucCommand)p.Buffer[4];
            sbyte aucWorkIndex = (sbyte)p.Buffer[5];
            var zone = AreaOf(c);

            var pv = Pv(c).BlockedBy(BlockedState.InEvent)
                          .Custom(v => { if (c.AccountGmLevel == 0 && !zone.CanUseMisc("auction_house")) v.AddError($"Zone {zone.Name} does not allow misc flag auction_house."); })
                          .MustEqual(InPrison(c) ? 1 : 0, 0, "Character in jail")
                          .OneOf<AucCommand>(p.Buffer[4])
                          .MustEqual((sbyte)p.Buffer[6], 0, "Result not 0")
                          .MustEqual((sbyte)p.Buffer[7], 0, "Result status");

            switch (cmd)
            {
                case AucCommand.AskCommit:
                    pv.Range("Commission", U32(p, 8), 1, 999999999)
                      .Range("ItemStacks", U32(p, 16), 0, 1);
                    break;
                case AucCommand.WorkCheck:
                    pv.MustEqual(aucWorkIndex, -1, "AucWorkIndex not -1");
                    break;
                case AucCommand.LotIn:
                    pv.Range("AucWorkIndex", aucWorkIndex, 0, 6)
                      .Range("LimitPrice", U32(p, 8), 1, 999999999)
                      .Range("ItemStacks", U32(p, 16), 0, 1);
                    break;
                case AucCommand.Bid:
                    pv.Range("AucWorkIndex", aucWorkIndex, 0, 6)
                      .Range("BidPrice", U32(p, 8), 1, 999999999)
                      .Range("ItemStacks", U32(p, 16), 0, 1);
                    break;
                case AucCommand.LotCancel:
                case AucCommand.LotCheck:
                    pv.Range("AucWorkIndex", aucWorkIndex, 0, 6);
                    break;
            }
            return pv;
        }
    }
}
