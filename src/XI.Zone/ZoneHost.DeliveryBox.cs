using System;
using System.Text;
using XI.Map;
using XI.Persistence;

namespace XI.Zone
{
    // The C2S 0x04D GP_CLI_COMMAND_PBX handler (the delivery box), dboxutils, and the S2C 0x04B
    // GP_SERV_COMMAND_PBX_RESULT it answers with. The open box's items live in the char's universal container; the
    // delivery_box rows are the boxes themselves (box 1 incoming, 2 outgoing; incoming rows from slot 8 up are the
    // queue the delivery_box_insert trigger numbers).
    public sealed partial class ZoneHost
    {
        // GP_CLI_COMMAND_PBX_COMMAND
        internal enum PbxCommand : byte
        {
            Work = 0x01, Set = 0x02, Send = 0x03, Cancel = 0x04, Check = 0x05, Recv = 0x06, Confirm = 0x07,
            Accept = 0x08, Reject = 0x09, Get = 0x0A, Clear = 0x0B, Query = 0x0C, DeliOpen = 0x0D, PostOpen = 0x0E,
            PostClose = 0x0F,
        }

        // GP_CLI_COMMAND_PBX_BOXNO
        private const sbyte BoxNone = -1, BoxIncoming = 1, BoxOutgoing = 2;

        private const uint ItemFlagCanSendAccount = 0x00000010, ItemFlagNoDelivery = 0x00002000;

        // ---- S2C ----------------------------------------------------------------------------------------------------

        // GP_SERV_COMMAND_PBX_RESULT(action, boxid, count, param): only the header and Represent; the client needs the
        // unused fields at -1
        private static BasicPacket PbxResult(PbxCommand action, sbyte box, byte count, byte param)
        {
            var w = new PacketWriter(0x04B, 0x14);
            w.U8(4, (byte)action);
            w.U8(5, (byte)box);
            w.U8(6, 0xFF);                    // PostWorkNo
            w.U8(7, 0xFF);                    // ItemWorkNo
            w.U32(8, 0xFFFFFFFF);             // ItemStacks
            w.U8(13, 0xFF);                   // ResParam1..3
            w.U8(14, 0xFF);
            w.U8(15, 0xFF);
            if (action == PbxCommand.Check)
            {
                if (box == BoxIncoming) w.U8(14, count);
                else w.U8(15, count);
            }
            else if (action == PbxCommand.Query)
            {
                w.U8(13, count);
            }
            w.U8(12, param);                  // Result
            return w.Packet;
        }

        // GP_SERV_COMMAND_PBX_RESULT(action, boxid, PItem, slotid, count, message): the full GP_POST_BOX_STATE, with
        // the item's other party (the client disables "Return" for a name starting with "AH")
        private static BasicPacket PbxResult(PbxCommand action, sbyte box, DboxItem item, byte slot, byte count, byte message)
        {
            var w = new PacketWriter(0x04B, 0x58);
            w.U8(4, (byte)action);
            w.U8(5, (byte)box);
            w.U8(6, slot);                    // PostWorkNo
            w.U8(12, message);                // Result: 0x01 success, else the error
            w.U8(13, count);                  // ResParam1
            w.U8(7, 0xFF);                    // ItemWorkNo
            w.U32(8, 0xFFFFFFFF);             // ItemStacks
            w.U8(14, 0xFF);
            w.U8(15, 0xFF);

            if (action == PbxCommand.Recv && message == 0x02) w.U32(16, 0x06); // State.Stat

            if (item != null)
            {
                if ((action != PbxCommand.Get && action != PbxCommand.Clear && action != PbxCommand.Reject) || message > 1)
                {
                    if (box == BoxIncoming)
                    {
                        w.U32(16, 0x07);
                        WritePbxName(w, item.Sender);    // Recv.From
                    }
                    else
                    {
                        w.U32(16, item.Sent ? 0x03u : 0x05u); // 0x05 in send: canceled
                        WritePbxName(w, item.Receiver);  // Send.To
                    }
                }
                if (action == PbxCommand.Set)
                {
                    w.U32(16, 0x01);
                    w.U8(7, item.Slot);
                }
                else if (action == PbxCommand.Send)
                {
                    w.U8(7, item.Slot);
                }
                else if (action == PbxCommand.Cancel)
                {
                    if (message == 0x01) w.U32(16, 0x05);
                    else if (message == 0x02) w.U32(16, 0x04);
                }

                w.U32(44, item.SubId);           // box_state.pbox.Send.ItemWorkNo
                w.U16(48, item.ItemId);          // ItemNo
                w.U32(56, item.Quantity);        // Stack
                w.Bytes(60, item.Extra.AsSpan(0, Math.Min(item.Extra.Length, 24))); // Data

                // the sold quantity the trigger keeps in the extra of an auction house's gil
                if (item.ItemId == GilItemId)
                {
                    w.U32(44, (uint)(item.SubId | item.Extra[0] << 16));
                    w.Bytes(60, new byte[28]);
                }
            }
            return w.Packet;
        }

        // memcpy(To/From, name, min(size, 16)): no terminator when the name fills the field
        private static void WritePbxName(PacketWriter w, string name)
        {
            var bytes = Encoding.Latin1.GetBytes(name ?? "");
            w.Bytes(20, bytes.AsSpan(0, Math.Min(bytes.Length, 16)));
        }

        // ---- dboxutils ----------------------------------------------------------------------------------------------

        private static bool IsSendBoxOpen(CharData c) => c.UContainer.Type == UContainerType.SendDeliveryBox;

        private static bool IsRecvBoxOpen(CharData c) => c.UContainer.Type == UContainerType.RecvDeliveryBox;

        private static bool IsAnyDeliveryBoxOpen(CharData c) => IsSendBoxOpen(c) || IsRecvBoxOpen(c);

        private static sbyte OpenBoxNo(CharData c) => IsSendBoxOpen(c) ? BoxOutgoing : IsRecvBoxOpen(c) ? BoxIncoming : BoxNone;

        private bool AuditDbox => loader_.Stats?.Settings.AuditPlayerDbox == true;

        // isDeliveryBoxInflightAtCapacity: the char's queued incoming rows reach map.DELIVERY_BOX_MAX_INFLIGHT
        private bool IsDeliveryBoxInflightAtCapacity(uint charId)
        {
            uint maxInflight = (uint)(loader_.Stats?.Settings.DeliveryBoxMaxInflight ?? 128);
            var r = Exec("SELECT COUNT(*) AS cnt FROM delivery_box WHERE charid = ? AND box = 1 AND slot >= 8", BoundValue.Of(charId));
            return r != null && r.Next() && r.Get<uint>("cnt") >= maxInflight;
        }

        // charutils::getAccountIdFromName
        private uint AccountIdFromName(string name)
        {
            var r = Exec("SELECT accid FROM chars WHERE charname = ? LIMIT 1", BoundValue.Of(name));
            return r != null && r.RowsCount() > 0 && r.Next() ? r.Get<uint>("accid") : 0;
        }

        // charutils::getCharIdAndAccountIdFromName
        private (uint CharId, uint AccId) CharIdAndAccountIdFromName(string name)
        {
            var r = Exec("SELECT charid, accid FROM chars WHERE charname = ? LIMIT 1", BoundValue.Of(name));
            return r != null && r.RowsCount() > 0 && r.Next() ? (r.Get<uint>("charid"), r.Get<uint>("accid")) : (0, 0);
        }

        // xi::items::spawn: a delivery box row whose item id has no template is left out
        private DboxItem SpawnDboxItem(ushort itemId) =>
            itemId == GilItemId || ItemBasic(itemId) != null ? new DboxItem { ItemId = itemId } : null;

        // extractFromBlob: at most the 24 bytes of m_extra
        private static void ExtractExtra(ResultSet r, DboxItem item)
        {
            if (r.IsNull("extra")) return;
            var blob = r.GetBlobBytes("extra");
            Array.Copy(blob, item.Extra, Math.Min(blob.Length, item.Extra.Length));
        }

        // SendOldItems (Work): load the open box's slots 0-7 and send each slot
        private void SendOldItems(CharData c)
        {
            if (!IsAnyDeliveryBoxOpen(c))
            {
                log_($"DBOX: Received action SendOldItems while UContainer is in an invalid state: {c.Name} ({c.Id})");
                return;
            }
            sbyte box = OpenBoxNo(c);

            var r = Exec("SELECT itemid, itemsubid, slot, quantity, sent, extra, sender, charname FROM delivery_box WHERE charid = ? AND box = ? " +
                         "AND slot < 8 ORDER BY slot", BoundValue.Of(c.Id), BoundValue.Of(box));
            if (r == null) return;
            byte items = 0;
            while (r.Next())
            {
                var item = SpawnDboxItem(r.Get<ushort>("itemid"));
                if (item == null) continue;
                item.SubId = r.Get<ushort>("itemsubid");
                item.Slot = r.Get<byte>("slot");
                item.Quantity = r.Get<uint>("quantity");
                if (r.Get<byte>("sent") == 1) item.Sent = true;
                ExtractExtra(r, item);
                if (box == BoxOutgoing)
                {
                    item.Sender = r.Get<string>("charname");
                    item.Receiver = r.Get<string>("sender");
                }
                else
                {
                    item.Sender = r.Get<string>("sender");
                    item.Receiver = r.Get<string>("charname");
                }
                c.UContainer.SetItem(item.Slot, item);
                items++;
            }

            for (byte i = 0; i < 8; i++) PushTo(c, PbxResult(PbxCommand.Work, box, c.UContainer.GetItem(i), i, items, 1));
        }

        // AddItemsToBeSent (Set): write the stack to the outgoing row PostWorkNo, then take it from the inventory
        private void AddItemsToBeSent(CharData c, sbyte box, sbyte postWorkNo, sbyte itemWorkNo, uint itemStacks, string receiverName)
        {
            if (!IsSendBoxOpen(c))
            {
                log_($"DBOX: Received AddItemsToBeSent while UContainer is in a state other than UCONTAINER_SEND_DELIVERYBOX: {c.Name} ({c.Id})");
                return;
            }

            using var t = new ItemTransaction(this, c);
            var item = t.ClaimSlot(Container.Inventory, (byte)itemWorkNo);
            if (itemStacks == 0 || item == null) return;
            if (item.Quantity < itemStacks)
            {
                log_($"DBOX: {c.Name} attempted to send insufficient {itemStacks}: {item.ItemId}");
                return;
            }
            if (!c.UContainer.IsSlotEmpty(postWorkNo)) return;

            var (recvCharId, recvAccId) = CharIdAndAccountIdFromName(receiverName);
            if (recvCharId == 0 || recvAccId == 0) return;

            uint flags = ItemBasic(item.ItemId)?.Flags ?? 0;
            if ((flags & ItemFlagNoDelivery) != 0)
            {
                if ((flags & ItemFlagCanSendAccount) == 0) return;
                if (c.AccId != recvAccId) return; // different accounts
            }

            var boxItem = SpawnDboxItem(item.ItemId);
            if (boxItem == null)
            {
                log_($"DBOX: PUBoxItem was null (player: {c.Name}, item: {item.ItemId})");
                return;
            }
            boxItem.Receiver = receiverName;
            boxItem.Sender = c.Name;
            boxItem.Quantity = itemStacks;
            boxItem.Slot = item.Slot;
            Array.Copy(item.Extra, boxItem.Extra, Math.Min(item.Extra.Length, boxItem.Extra.Length));

            // the delivery_box_insert trigger runs
            var r = Exec("INSERT INTO delivery_box(charid, charname, box, slot, itemid, itemsubid, quantity, extra, senderid, sender) " +
                         "VALUES(?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
                         BoundValue.Of(c.Id), BoundValue.Of(c.Name), BoundValue.Of(2), BoundValue.Of(postWorkNo), BoundValue.Of(item.ItemId),
                         BoundValue.Of((ushort)0), BoundValue.Of(itemStacks), BoundValue.Of(boxItem.Extra), BoundValue.Of(recvCharId),
                         BoundValue.Of(receiverName));
            if (r == null || r.RowsAffected() == 0) return;

            // the row is already committed, so delete it again if the stack cannot be taken
            uint charId = c.Id;
            t.UndoWith(() => Exec("DELETE FROM delivery_box WHERE charid = ? AND box = 2 AND slot = ?", BoundValue.Of(charId), BoundValue.Of(postWorkNo)));

            if (!t.Take(Container.Inventory, (byte)itemWorkNo, itemStacks) || !t.Commit())
            {
                log_($"DBOX: {c.Name} kept item {itemWorkNo} after it was written to the box, taking the row back");
                return;
            }

            c.UContainer.SetItem(postWorkNo, boxItem);
            PushTo(c, PbxResult(PbxCommand.Set, box, boxItem, (byte)postWorkNo, c.UContainer.ItemsCount, 1));
            PushTo(c, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
        }

        // SendConfirmation (Send): mark the outgoing row sent and queue a copy in the receiver's incoming box
        private void SendConfirmation(CharData c, sbyte box, sbyte postWorkNo)
        {
            if (!IsSendBoxOpen(c))
            {
                log_($"DBOX: Received action SendConfirmation while UContainer is in a state other than UCONTAINER_SEND_DELIVERYBOX: {c.Name} ({c.Id})");
                return;
            }

            byte sendItems = 0;
            for (int i = 0; i < 8; i++)
                if (!c.UContainer.IsSlotEmpty(i) && !c.UContainer.GetItem(i).Sent) sendItems++;

            var item = c.UContainer.GetItem(postWorkNo);
            if (item == null || item.Sent) return;

            uint charId = CharIdFromName(item.Receiver);
            if (charId == 0) return;

            if (IsDeliveryBoxInflightAtCapacity(charId))
            {
                PushTo(c, PbxResult(PbxCommand.Send, box, item, (byte)postWorkNo, sendItems, 0x02));
                PushTo(c, PbxResult(PbxCommand.Send, box, item, (byte)postWorkNo, sendItems, 0xFE));
                return;
            }

            bool ok = db_.Transaction(() =>
            {
                var r = Exec("UPDATE delivery_box SET sent = 1 WHERE charid = ? AND senderid = ? AND slot = ? AND box = ?",
                             BoundValue.Of(c.Id), BoundValue.Of(charId), BoundValue.Of(postWorkNo), BoundValue.Of(2));
                if (r != null && r.RowsAffected() > 0)
                {
                    // the delivery_box_insert trigger runs
                    var r2 = Exec("INSERT INTO delivery_box(charid, charname, box, itemid, itemsubid, quantity, extra, senderid, sender) " +
                                  "VALUES(?, ?, ?, ?, ?, ?, ?, ?, ?)",
                                  BoundValue.Of(charId), BoundValue.Of(item.Receiver), BoundValue.Of(1), BoundValue.Of(item.ItemId),
                                  BoundValue.Of(item.SubId), BoundValue.Of(item.Quantity), BoundValue.Of(item.Extra), BoundValue.Of(c.Id),
                                  BoundValue.Of(c.Name));
                    if (r2 != null && r2.RowsAffected() > 0)
                    {
                        item.Sent = true;
                        PushTo(c, PbxResult(PbxCommand.Send, box, item, (byte)postWorkNo, sendItems, 0x02));
                        PushTo(c, PbxResult(PbxCommand.Send, box, item, (byte)postWorkNo, sendItems, 0x01));
                        return;
                    }
                }
                throw new InvalidOperationException($"DBOX: Could not finalize send confirmation transaction (player: {c.Name} ({c.Id}), " +
                                                    $"target: {item.Receiver}, PostWorkNo: {postWorkNo})");
            });
            if (!ok) log_($"DBOX: send confirmation rolled back for {c.Name} ({c.Id}), PostWorkNo: {postWorkNo}");
        }

        // CancelSendingItem (Cancel): unsend the row and take the copy back out of the receiver's queue
        private void CancelSendingItem(CharData c, sbyte box, sbyte postWorkNo)
        {
            if (!IsSendBoxOpen(c))
            {
                log_($"DBOX: Received action CancelSendingItem while UContainer is in a state other than UCONTAINER_SEND_DELIVERYBOX: {c.Name} ({c.Id})");
                return;
            }

            var item = c.UContainer.GetItem(postWorkNo);
            if (item == null) return;

            bool ok = db_.Transaction(() =>
            {
                uint charId = CharIdFromName(item.Receiver);
                if (charId != 0)
                {
                    var r = Exec("UPDATE delivery_box SET sent = 0 WHERE charid = ? AND box = 2 AND slot = ? AND sent = 1 AND received = 0 LIMIT 1",
                                 BoundValue.Of(c.Id), BoundValue.Of(postWorkNo));
                    if (r != null && r.RowsAffected() > 0)
                    {
                        var r2 = Exec("DELETE FROM delivery_box WHERE senderid = ? AND box = 1 AND charid = ? AND itemid = ? AND quantity = ? AND extra = ? " +
                                      "AND slot >= 8 LIMIT 1",
                                      BoundValue.Of(c.Id), BoundValue.Of(charId), BoundValue.Of(item.ItemId), BoundValue.Of(item.Quantity),
                                      BoundValue.Of(item.Extra));
                        if (r2 != null && r2.RowsAffected() > 0)
                        {
                            item.Sent = false;
                            PushTo(c, PbxResult(PbxCommand.Cancel, box, item, (byte)postWorkNo, c.UContainer.ItemsCount, 0x02));
                            PushTo(c, PbxResult(PbxCommand.Cancel, box, item, (byte)postWorkNo, c.UContainer.ItemsCount, 0x01));
                            return;
                        }
                    }
                }
                throw new InvalidOperationException($"DBOX: Could not finalize cancel send transaction (player: {c.Name} ({c.Id}), " +
                                                    $"target: {item.Receiver}, PostWorkNo: {postWorkNo})");
            });
            if (ok) return;

            var del = Exec("DELETE FROM delivery_box WHERE box = 2 AND charid = ? AND itemid = ? AND quantity = ? AND slot = ? LIMIT 1",
                           BoundValue.Of(c.Id), BoundValue.Of(item.ItemId), BoundValue.Of(item.Quantity), BoundValue.Of(postWorkNo));
            if (del != null && del.RowsAffected() > 0)
            {
                log_($"DBOX: Deleting orphaned outbox record (player: {c.Name} ({c.Id}), target: {item.Receiver}, PostWorkNo: {postWorkNo})");
                PushTo(c, PbxResult(PbxCommand.PostClose, box, 0, 1));
            }

            // "Delivery orders are currently backlogged."
            PushTo(c, PbxResult(PbxCommand.Cancel, box, 0, 0xFF));
        }

        // SendClientNewItemCount (Check): the queued items the incoming box has room for, or the delivered outgoing ones
        private void SendClientNewItemCount(CharData c, sbyte box)
        {
            if (!IsAnyDeliveryBoxOpen(c))
            {
                log_($"DBOX: Received action SendClientNewItemCount while UContainer is in an invalid state: {c.Name} ({c.Id})");
                return;
            }

            byte receivedItems = 0;
            if (box == BoxIncoming)
            {
                int limit = 0;
                for (int i = 0; i < 8; i++)
                    if (c.UContainer.IsSlotEmpty(i)) limit++;
                var r = Exec("SELECT charid FROM delivery_box WHERE charid = ? AND box = 1 AND slot >= 8 ORDER BY slot ASC LIMIT ?",
                             BoundValue.Of(c.Id), BoundValue.Of(limit));
                if (r != null) receivedItems = (byte)r.RowsCount();
            }
            else if (box == BoxOutgoing)
            {
                var r = Exec("SELECT charid FROM delivery_box WHERE charid = ? AND received = 1 AND box = 2", BoundValue.Of(c.Id));
                if (r != null) receivedItems = (byte)r.RowsCount();
            }

            PushTo(c, PbxResult(PbxCommand.Check, box, 0xFF, 0x02));
            PushTo(c, PbxResult(PbxCommand.Check, box, receivedItems, 0x01));
        }

        // SendNewItems (Recv): move the first queued item into slot PostWorkNo and mark the sender's row received
        private void SendNewItems(CharData c, sbyte box, sbyte postWorkNo)
        {
            if (!IsRecvBoxOpen(c))
            {
                log_($"DBOX: Received action SendNewItems while UContainer is in a state other than UCONTAINER_RECV_DELIVERYBOX: {c.Name} ({c.Id})");
                return;
            }
            if (box != BoxIncoming) return;

            bool ok = db_.Transaction(() =>
            {
                var r = Exec("SELECT itemid, itemsubid, quantity, extra, sender, senderid FROM delivery_box WHERE charid = ? " +
                             "AND box = 1 AND slot >= 8 ORDER BY slot ASC LIMIT 1", BoundValue.Of(c.Id));
                if (r != null && r.RowsCount() > 0 && r.Next() && SpawnDboxItem(r.Get<ushort>("itemid")) is DboxItem item)
                {
                    item.SubId = r.Get<ushort>("itemsubid");
                    item.Quantity = r.Get<uint>("quantity");
                    ExtractExtra(r, item);
                    item.Sender = r.Get<string>("sender");

                    if (c.UContainer.IsSlotEmpty(postWorkNo))
                    {
                        uint senderId = r.Get<uint>("senderid");
                        item.Slot = (byte)postWorkNo;

                        // it does not matter whether this finds a row: the auction house sends with no sender record
                        Exec("UPDATE delivery_box SET received = 1 WHERE senderid = ? AND charid = ? AND box = 2 AND received = 0 AND quantity = ? AND sent = 1 AND itemid = ? LIMIT 1",
                             BoundValue.Of(c.Id), BoundValue.Of(senderId), BoundValue.Of(item.Quantity), BoundValue.Of(item.ItemId));

                        if (AuditDbox)
                        {
                            if (Exec("INSERT INTO audit_dbox(itemid, quantity, sender, sender_name, receiver, receiver_name, date) VALUES (?, ?, ?, ?, ?, ?, ?)",
                                     BoundValue.Of(item.ItemId), BoundValue.Of(item.Quantity), BoundValue.Of(senderId), BoundValue.Of(item.Sender),
                                     BoundValue.Of(c.Id), BoundValue.Of(c.Name), BoundValue.Of((uint)UnixNow())) == null)
                                log_($"Failed to log delivery box transaction (item: {item.ItemId}, quantity: {item.Quantity}, sender: {senderId}, receiver: {c.Id})");
                        }

                        var q = Exec("SELECT slot FROM delivery_box WHERE charid = ? AND box = 1 AND slot > 7 ORDER BY slot ASC", BoundValue.Of(c.Id));
                        if (q != null && q.RowsCount() > 0 && q.Next())
                        {
                            ushort queue = q.Get<ushort>("slot");
                            var r2 = Exec("UPDATE delivery_box SET slot = ? WHERE charid = ? AND box = 1 AND slot = ?",
                                          BoundValue.Of(postWorkNo), BoundValue.Of(c.Id), BoundValue.Of(queue));
                            if (r2 != null)
                            {
                                var r3 = Exec("UPDATE delivery_box SET slot = slot - 1 WHERE charid = ? AND box = 1 AND slot > ?",
                                              BoundValue.Of(c.Id), BoundValue.Of(queue));
                                if (r3 != null)
                                {
                                    c.UContainer.SetItem(postWorkNo, item);
                                    PushTo(c, PbxResult(PbxCommand.Recv, box, null, (byte)postWorkNo, 1, 2));

                                    // the auction house gil message does not print for slot 0 when both packets go at once,
                                    // so the second waits for the char's next tick
                                    var result = PbxResult(PbxCommand.Recv, box, item, (byte)postWorkNo, 1, 1);
                                    if (postWorkNo == 0 && item.ItemId == GilItemId) c.ActionQueue.Add(() => PushTo(c, result));
                                    else PushTo(c, result);
                                    return;
                                }
                            }
                        }
                    }
                }
                throw new InvalidOperationException($"DBOX: Could not finalize send new items transaction (player: {c.Name} ({c.Id}), PostWorkNo: {postWorkNo})");
            });
            if (!ok) PushTo(c, PbxResult(PbxCommand.Recv, box, 0, 0xEB));
        }

        // RemoveDeliveredItemFromSendingBox (Confirm): drop the first delivered outgoing row from its slot
        private void RemoveDeliveredItemFromSendingBox(CharData c, sbyte box)
        {
            if (!IsSendBoxOpen(c))
            {
                log_($"DBOX: Received action RemoveDeliveredItemFromSendingBox while UContainer is in a state other than UCONTAINER_SEND_DELIVERYBOX: {c.Name} ({c.Id})");
                return;
            }

            var r = Exec("SELECT slot FROM delivery_box WHERE charid = ? AND received = 1 AND box = 2 ORDER BY slot ASC", BoundValue.Of(c.Id));
            if (r == null) return;
            byte receivedItems = (byte)r.RowsCount();
            if (receivedItems == 0 || !r.Next()) return;
            byte slot = (byte)r.Get<ushort>("slot");
            var item = c.UContainer.GetItem(slot);
            if (item == null || !item.Sent) return;

            var r2 = Exec("DELETE FROM delivery_box WHERE charid = ? AND box = 2 AND slot = ? LIMIT 1", BoundValue.Of(c.Id), BoundValue.Of(slot));
            if (r2 == null || r2.RowsAffected() == 0) return;
            PushTo(c, PbxResult(PbxCommand.Confirm, box, 0, 0x02));
            PushTo(c, PbxResult(PbxCommand.Confirm, box, item, slot, receivedItems, 0x01));
            c.UContainer.SetItem(slot, null);
        }

        // UpdateDeliveryCellBeforeRemoving (Accept)
        private void UpdateDeliveryCellBeforeRemoving(CharData c, sbyte box, sbyte postWorkNo)
        {
            if (!IsAnyDeliveryBoxOpen(c))
            {
                log_($"DBOX: Received action UpdateDeliveryCellBeforeRemoving while UContainer is in an invalid state: {c.Name} ({c.Id})");
                return;
            }
            var item = c.UContainer.GetItem(postWorkNo);
            if (item != null) PushTo(c, PbxResult(PbxCommand.Accept, box, item, (byte)postWorkNo, 1, 1));
        }

        // ReturnToSender (Reject): queue the incoming item back in its sender's incoming box
        private void ReturnToSender(CharData c, sbyte box, sbyte postWorkNo)
        {
            if (!IsRecvBoxOpen(c))
            {
                log_($"DBOX: Received action ReturnToSender while UContainer is in a state other than UCONTAINER_RECV_DELIVERYBOX: {c.Name} ({c.Id})");
                return;
            }

            var item = c.UContainer.GetItem(postWorkNo);
            if (item == null) return;

            bool ok = db_.Transaction(() =>
            {
                uint senderId = 0;
                string senderName = "";
                var r = Exec("SELECT senderid, sender FROM delivery_box WHERE charid = ? AND slot = ? AND box = 1 LIMIT 1",
                             BoundValue.Of(c.Id), BoundValue.Of(postWorkNo));
                if (r != null && r.RowsCount() > 0 && r.Next())
                {
                    senderId = r.Get<uint>("senderid");
                    senderName = r.Get<string>("sender");
                }

                if (senderId != 0)
                {
                    if (IsDeliveryBoxInflightAtCapacity(senderId))
                    {
                        PushTo(c, PbxResult(PbxCommand.Reject, box, item, (byte)postWorkNo, c.UContainer.ItemsCount, 0x02));
                        PushTo(c, PbxResult(PbxCommand.Reject, box, item, (byte)postWorkNo, c.UContainer.ItemsCount, 0xFE));
                        return;
                    }

                    // the delivery_box_insert trigger runs
                    var r1 = Exec("INSERT INTO delivery_box (charid, charname, box, itemid, itemsubid, quantity, extra, senderid, sender) " +
                                  "VALUES(?, ?, ?, ?, ?, ?, ?, ?, ?)",
                                  BoundValue.Of(senderId), BoundValue.Of(senderName), BoundValue.Of(1), BoundValue.Of(item.ItemId),
                                  BoundValue.Of(item.SubId), BoundValue.Of(item.Quantity), BoundValue.Of(item.Extra), BoundValue.Of(c.Id),
                                  BoundValue.Of(c.Name));
                    if (r1 != null && r1.RowsAffected() > 0)
                    {
                        // the original delivery record
                        var r2 = Exec("DELETE FROM delivery_box WHERE charid = ? AND slot = ? AND box = 1 LIMIT 1",
                                      BoundValue.Of(c.Id), BoundValue.Of(postWorkNo));
                        if (r2 != null && r2.RowsAffected() > 0)
                        {
                            c.UContainer.SetItem(postWorkNo, null);
                            PushTo(c, PbxResult(PbxCommand.Reject, box, item, (byte)postWorkNo, c.UContainer.ItemsCount, 0x02));
                            PushTo(c, PbxResult(PbxCommand.Reject, box, item, (byte)postWorkNo, c.UContainer.ItemsCount, 0x01));
                            return;
                        }
                    }
                }
                throw new InvalidOperationException($"DBOX: Could not finalize delivery return transaction (player: {c.Name} ({c.Id}), " +
                                                    $"sender: {senderName}, itemID: {item.ItemId}, quantity: {item.Quantity})");
            });
            if (!ok) PushTo(c, PbxResult(PbxCommand.Reject, box, item, (byte)postWorkNo, c.UContainer.ItemsCount, 0xEB));
        }

        // TakeItemFromCell (Get): delete the row and give its item, unless the inventory is full, the char holds the
        // rare item already or the outgoing item is on its way
        private void TakeItemFromCell(CharData c, sbyte postWorkNo)
        {
            if (!IsAnyDeliveryBoxOpen(c))
            {
                log_($"DBOX: Received action TakeItemFromCell while UContainer is in an invalid state {c.Name} ({c.Id})");
                return;
            }
            sbyte box = OpenBoxNo(c);

            var item = c.UContainer.GetItem(postWorkNo);
            if (item == null) return;

            if (item.ItemId != GilItemId && FreeSlots(c, Container.Inventory) == 0)
            {
                PushTo(c, PbxResult(PbxCommand.Get, box, item, (byte)postWorkNo, c.UContainer.ItemsCount, 0xB9));
                return;
            }
            if (((ItemBasic(item.ItemId)?.Flags ?? 0) & ItemFlagRare) != 0 && HasItem(c, item.ItemId))
            {
                PushTo(c, MessageFor(c, item.ItemId, 0, MsgStdItemEx));
                PushTo(c, PbxResult(PbxCommand.Get, box, item, (byte)postWorkNo, c.UContainer.ItemsCount, 0xBA));
                return;
            }
            if (box == BoxOutgoing && item.Sent)
            {
                PushTo(c, PbxResult(PbxCommand.Get, box, item, (byte)postWorkNo, c.UContainer.ItemsCount, 0xBA));
                return;
            }

            using var t = new ItemTransaction(this, c);
            bool ok = db_.Transaction(() =>
            {
                string sql = box == BoxIncoming
                    ? "DELETE FROM delivery_box WHERE charid = ? AND slot = ? AND box = ? LIMIT 1"
                    : "DELETE FROM delivery_box WHERE charid = ? AND sent = 0 AND slot = ? AND box = ? LIMIT 1";
                var r = Exec(sql, BoundValue.Of(c.Id), BoundValue.Of(postWorkNo), BoundValue.Of(box));
                if (r != null && r.RowsAffected() > 0)
                {
                    var given = new Item { ItemId = item.ItemId, Quantity = item.Quantity, Extra = (byte[])item.Extra.Clone() };
                    if (t.GiveItem(Container.Inventory, given, silent: true).HasValue) return;
                }
                throw new InvalidOperationException($"DBOX: Could not finalize take item transaction (player: {c.Name} ({c.Id}), BoxNo: {box}, " +
                                                    $"PostWorkNo: {postWorkNo}, item: {item.ItemId} x{item.Quantity}, sent: {item.Sent})");
            });
            if (ok)
            {
                t.Commit();
                PushTo(c, PbxResult(PbxCommand.Get, box, item, (byte)postWorkNo, c.UContainer.ItemsCount, 1));
                PushTo(c, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
                c.UContainer.SetItem(postWorkNo, null);
            }
            else
            {
                PushTo(c, PbxResult(PbxCommand.Get, box, item, (byte)postWorkNo, c.UContainer.ItemsCount, 0xBA));
            }
        }

        // RemoveItemFromCell (Clear): drop the incoming row
        private void RemoveItemFromCell(CharData c, sbyte box, sbyte postWorkNo)
        {
            if (!IsRecvBoxOpen(c))
            {
                log_($"DBOX: Received action RemoveItemFromCell while UContainer is in a state other than UCONTAINER_RECV_DELIVERYBOX: {c.Name} ({c.Id})");
                return;
            }
            if (c.UContainer.IsSlotEmpty(postWorkNo)) return;

            var r = Exec("DELETE FROM delivery_box WHERE charid = ? AND slot = ? AND box = 1 LIMIT 1", BoundValue.Of(c.Id), BoundValue.Of(postWorkNo));
            if (r == null || r.RowsAffected() == 0) return;
            var item = c.UContainer.GetItem(postWorkNo);
            c.UContainer.SetItem(postWorkNo, null);
            PushTo(c, PbxResult(PbxCommand.Clear, box, item, (byte)postWorkNo, c.UContainer.ItemsCount, 1));
        }

        // ConfirmNameBeforeSending (Query): whether the name is a char, and whether it is on the sender's account
        private void ConfirmNameBeforeSending(CharData c, sbyte box, string receiverName)
        {
            if (!IsSendBoxOpen(c))
            {
                log_($"DBOX: Received action ConfirmNameBeforeSending while UContainer is in a state other than UCONTAINER_SEND_DELIVERYBOX: {c.Name} ({c.Id})");
                return;
            }

            uint accId = AccountIdFromName(receiverName);
            PushTo(c, PbxResult(PbxCommand.Query, box, 0xFF, 0x02));
            if (accId == 0)
            {
                PushTo(c, PbxResult(PbxCommand.Query, box, 0x00, 0xFB));
                return;
            }
            var r = Exec("SELECT COUNT(*) FROM chars WHERE charid = ? AND accid = ? LIMIT 1", BoundValue.Of(c.Id), BoundValue.Of(accId));
            bool sameAccount = r != null && r.RowsCount() > 0 && r.Next() && r.Get<uint>("COUNT(*)") != 0;
            PushTo(c, PbxResult(PbxCommand.Query, box, sameAccount ? (byte)0x01 : (byte)0x00, 0x01));
        }

        // CloseMailWindow (PostClose)
        private void CloseMailWindow(CharData c, sbyte box)
        {
            if (IsAnyDeliveryBoxOpen(c)) c.UContainer.Clean();
            PushTo(c, PbxResult(PbxCommand.PostClose, box, 0, 1));
        }

        // OpenSendBox (DeliOpen) / OpenRecvBox (PostOpen)
        private void OpenDeliveryBox(CharData c, UContainerType type)
        {
            c.UContainer.Clean();
            c.UContainer.SetType(type);
            PushTo(c, type == UContainerType.SendDeliveryBox
                ? PbxResult(PbxCommand.DeliOpen, BoxOutgoing, 0, 1)
                : PbxResult(PbxCommand.PostOpen, BoxIncoming, 0, 1));
        }

        // CAIContainer::ActionQueue: the queued actions run on the char's next tick
        private void TickActionQueues()
        {
            foreach (var (c, s) in new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<CharData, MapSession>>(charSessions_))
            {
                if (c.ActionQueue.Count == 0) continue;
                var actions = c.ActionQueue.ToArray();
                c.ActionQueue.Clear();
                foreach (var action in actions) action();
            }
        }

        // ---- C2S ----------------------------------------------------------------------------------------------------

        // 0x04D PBX: Command u8 @4, BoxNo i8 @5, PostWorkNo i8 @6, ItemWorkNo i8 @7, ItemStacks i32 @8, Result and
        // ResParam1-3 i8 @12-15, TargetName[16] @16
        private void RegisterDeliveryBoxHandlers()
        {
            Register(0x04D, (s, c, p) => ValidatePbx(c, p), (s, c, p) =>
            {
                var cmd = (PbxCommand)p.Buffer[4];
                sbyte box = (sbyte)p.Buffer[5];
                sbyte postWorkNo = (sbyte)p.Buffer[6];
                sbyte itemWorkNo = (sbyte)p.Buffer[7];
                uint itemStacks = U32(p, 8);

                var zone = AreaOf(c);
                if (c.MoghouseId == 0 && c.AccountGmLevel == 0 && !zone.CanUseMisc("auction_house") && !zone.CanUseMisc("mogmenu"))
                {
                    log_($"DBOX: {c.Name} ({c.Id}) is trying to use the delivery box in a disallowed zone [{zone.Name}]");
                    return;
                }

                switch (cmd)
                {
                    case PbxCommand.Work: SendOldItems(c); break;
                    case PbxCommand.Set: AddItemsToBeSent(c, box, postWorkNo, itemWorkNo, itemStacks, PbxTargetName(p)); break;
                    case PbxCommand.Send: SendConfirmation(c, box, postWorkNo); break;
                    case PbxCommand.Cancel: CancelSendingItem(c, box, postWorkNo); break;
                    case PbxCommand.Check: SendClientNewItemCount(c, box); break;
                    case PbxCommand.Recv: SendNewItems(c, box, postWorkNo); break;
                    case PbxCommand.Confirm: RemoveDeliveredItemFromSendingBox(c, box); break;
                    case PbxCommand.Accept: UpdateDeliveryCellBeforeRemoving(c, box, postWorkNo); break;
                    case PbxCommand.Reject: ReturnToSender(c, box, postWorkNo); break;
                    case PbxCommand.Get: TakeItemFromCell(c, postWorkNo); break;
                    case PbxCommand.Clear: RemoveItemFromCell(c, box, postWorkNo); break;
                    case PbxCommand.Query: ConfirmNameBeforeSending(c, box, PbxTargetName(p)); break;
                    case PbxCommand.DeliOpen: OpenDeliveryBox(c, UContainerType.SendDeliveryBox); break;
                    case PbxCommand.PostOpen: OpenDeliveryBox(c, UContainerType.RecvDeliveryBox); break;
                    case PbxCommand.PostClose: CloseMailWindow(c, box); break;
                }
            });
        }

        // db::escapeString(asStringFromUntrustedSource(TargetName, 15))
        private static string PbxTargetName(BasicPacket p) => SqlEscape.Escape(Encoding.Latin1.GetString(UntrustedBytes(p, 16, 15)));

        private static PacketValidator ValidatePbx(CharData c, BasicPacket p)
        {
            var cmd = (PbxCommand)p.Buffer[4];
            sbyte box = (sbyte)p.Buffer[5];
            sbyte postWorkNo = (sbyte)p.Buffer[6];
            sbyte itemWorkNo = (sbyte)p.Buffer[7];
            int itemStacks = (int)U32(p, 8);

            var pv = Pv(c).BlockedBy(BlockedState.InEvent | BlockedState.Crafting | BlockedState.Fishing | BlockedState.Jailed)
                          .OneOf<PbxCommand>(p.Buffer[4])
                          .MustEqual((sbyte)p.Buffer[12], 0, "Result not 0")
                          .MustEqual((sbyte)p.Buffer[13], 0, "ResParam1 not 0")
                          .MustEqual((sbyte)p.Buffer[14], 0, "ResParam2 not 0")
                          .MustEqual((sbyte)p.Buffer[15], 0, "ResParam3 not 0");

            switch (cmd)
            {
                case PbxCommand.Work:
                    pv.Range("BoxNo", box, BoxIncoming, BoxOutgoing)
                      .Range("PostWorkNo", postWorkNo, -1, 7)
                      .MustEqual(itemWorkNo, -1, "ItemWorkNo not -1")
                      .MustEqual(itemStacks, -1, "ItemStacks not -1");
                    break;
                case PbxCommand.Set:
                    pv.MustEqual(box, BoxOutgoing, "BoxNo not Outgoing")
                      .Range("PostWorkNo", postWorkNo, 0, 7)
                      .Range("ItemStacks", itemStacks, 0, 999999999);
                    break;
                case PbxCommand.Send:
                case PbxCommand.Cancel:
                    pv.MustEqual(box, BoxOutgoing, "BoxNo not Outgoing")
                      .Range("PostWorkNo", postWorkNo, 0, 7)
                      .MustEqual(itemWorkNo, -1, "ItemWorkNo not -1")
                      .MustEqual(itemStacks, -1, "ItemStacks not -1");
                    break;
                case PbxCommand.Check:
                    pv.Range("BoxNo", box, BoxIncoming, BoxOutgoing)
                      .MustEqual(postWorkNo, -1, "PostWorkNo not -1")
                      .MustEqual(itemWorkNo, -1, "ItemWorkNo not -1")
                      .MustEqual(itemStacks, -1, "ItemStacks not -1");
                    break;
                case PbxCommand.Recv:
                    pv.MustEqual(box, BoxIncoming, "BoxNo not Incoming")
                      .Range("PostWorkNo", postWorkNo, 0, 7)
                      .MustEqual(itemWorkNo, 1, "ItemWorkNo not 1")
                      .MustEqual(itemStacks, -1, "ItemStacks not -1");
                    break;
                case PbxCommand.Accept:
                case PbxCommand.Reject:
                    pv.MustEqual(box, BoxIncoming, "BoxNo not Incoming")
                      .Range("PostWorkNo", postWorkNo, 0, 7)
                      .MustEqual(itemWorkNo, -1, "ItemWorkNo not -1")
                      .MustEqual(itemStacks, -1, "ItemStacks not -1");
                    break;
                case PbxCommand.Get:
                case PbxCommand.Clear:
                    pv.Range("BoxNo", box, BoxIncoming, BoxOutgoing)
                      .Range("PostWorkNo", postWorkNo, 0, 7)
                      .MustEqual(itemWorkNo, -1, "ItemWorkNo not -1")
                      .MustEqual(itemStacks, -1, "ItemStacks not -1");
                    break;
                case PbxCommand.Confirm:
                case PbxCommand.Query:
                case PbxCommand.DeliOpen:
                case PbxCommand.PostOpen:
                case PbxCommand.PostClose:
                    pv.MustEqual(box, BoxNone, "BoxNo not None")
                      .MustEqual(postWorkNo, -1, "PostWorkNo not -1")
                      .MustEqual(itemWorkNo, -1, "ItemWorkNo not -1")
                      .MustEqual(itemStacks, -1, "ItemStacks not -1");
                    break;
            }
            return pv;
        }
    }
}
