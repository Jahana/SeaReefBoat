using System;
using System.Collections.Generic;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // CCharEntity::TradePending: set on both sides by a trade request; the other side is named by its id and index
    public sealed class PendingTrade
    {
        public uint UniqueNo;
        public ushort ActIndex;
        public long InvitedAtMs;
        public bool Initiator;

        public void Clean()
        {
            UniqueNo = 0;
            ActIndex = 0;
            InvitedAtMs = 0;
            Initiator = false;
        }
    }

    // Trading: items handed to an NPC (c2s/0x036 and NpcTradeTransaction, which the NPC's onTrade confirms through
    // CTradeContainer) and player-to-player trades (c2s/0x032 - 0x034 and PlayerTradeTransaction). The Monstrosity
    // checks have no state here yet, so they never refuse.
    public sealed partial class ZoneHost
    {
        private const int TradeMaxNpcSlots = 9;      // 8 trade window slots plus gil
        private const int PlayerTradeMaxSlots = 9;
        private const byte PlayerTradeGilSlot = 0;
        private const float TradeRange = 6.0f;       // the client cancels on its own at this range
        private const ushort EmptyTradeSlotItemNo = 0xFFFF;
        private const uint ItemFlagExclusive = 0x00004000;
        private const byte ItemTypeLinkshell = 2, ItemTypeEquipment = 6, ItemTypeWeapon = 7;
        private const int SlotLink1 = 0x10, SlotLink2 = 0x11;
        private const ushort MsgStdCannotTradeWithTarget = 28, MsgStdCannotWhileInvisible = 172, MsgStdLinkshellEquipBeforeUsing = 108, MsgStdTradeCanceled = 107;

        // GP_ITEM_TRADE_RES_KIND
        private const uint TradeResStart = 0, TradeResCancell = 1, TradeResMake = 2, TradeResErrEtc = 4, TradeResErrYouTrade = 7,
                           TradeResEnd = 9;

        private readonly Dictionary<ushort, (byte Type, bool Charged, ushort SubId)?> itemKind_ = new Dictionary<ushort, (byte, bool, ushort)?>();

        // the item_basic type and subid, and ITEM_CHARGED (equipment with item_usable validTargets)
        private (byte Type, bool Charged, ushort SubId)? ItemKind(ushort id)
        {
            if (itemKind_.TryGetValue(id, out var kind)) return kind;
            var r = Exec("SELECT type, subid FROM item_basic WHERE itemid = ? LIMIT 1", BoundValue.Of(id));
            if (r != null && r.Next())
            {
                byte type = r.Get<byte>("type");
                ushort subId = r.Get<ushort>("subid");
                bool charged = false;
                if (type == ItemTypeEquipment || type == ItemTypeWeapon)
                {
                    var u = Exec("SELECT validTargets FROM item_usable WHERE itemid = ? LIMIT 1", BoundValue.Of(id));
                    charged = u != null && u.Next() && u.Get<uint>("validTargets") != 0;
                }
                kind = (type, charged, subId);
            }
            return itemKind_[id] = kind;
        }

        private bool AuditTrades => loader_.Stats?.Settings.AuditPlayerTrades == true;

        // auditTrade: written in line rather than on a worker thread
        private void AuditTrade(uint sender, string senderName, uint receiver, string receiverName, ushort itemId, uint quantity)
        {
            if (!AuditTrades) return;
            if (Exec("INSERT INTO audit_trade(itemid, quantity, sender, sender_name, receiver, receiver_name, date) VALUES (?, ?, ?, ?, ?, ?, ?)",
                     BoundValue.Of((uint)itemId), BoundValue.Of(quantity), BoundValue.Of(sender), BoundValue.Of(senderName),
                     BoundValue.Of(receiver), BoundValue.Of(receiverName), BoundValue.Of((uint)UnixNow())) == null)
                log_($"Failed to log trade transaction (item: {itemId}, quantity: {quantity}, sender: {sender}, receiver: {receiver})");
        }

        // ---- NpcTradeTransaction ------------------------------------------------------------------------------------

        internal sealed class NpcTradeTransaction
        {
            public struct Slot
            {
                public Item Offered;
                public byte InvSlot;
                public uint Quantity, Confirmed;
            }

            public readonly ItemTransaction Tx;
            public readonly uint NpcId;
            public readonly string NpcName;
            public readonly Slot[] Slots = new Slot[TradeContainer.ContainerSize];

            // the entity traded with: an NPC, or a mob (0x036 takes TYPE_NPC | TYPE_MOB)
            public NpcTradeTransaction(ZoneHost host, CharData c, uint npcId, string npcName)
            {
                Tx = new ItemTransaction(host, c);
                NpcId = npcId;
                NpcName = npcName;
            }
        }

        private bool NpcTradeStage(CharData c, NpcTradeTransaction t, int tradeSlot, Item item, uint quantity)
        {
            if (tradeSlot >= t.Slots.Length || item == null) return false;
            ref var slot = ref t.Slots[tradeSlot];
            if (slot.Offered != null)
            {
                log_($"NpcTradeTransaction: {c.Name} staged trade slot {tradeSlot} twice");
                return false;
            }
            if (t.Tx.ClaimSlot(item.Location, item.Slot) != item) return false;
            slot = new NpcTradeTransaction.Slot { Offered = item, InvSlot = item.Slot, Quantity = quantity };
            return true;
        }

        // resolve: the offered stack while the char still holds it, claimed again if releaseUnconfirmed dropped it
        private static Item NpcTradeResolve(CharData c, NpcTradeTransaction t, ref NpcTradeTransaction.Slot slot)
        {
            var item = slot.Offered;
            if (item == null || !c.Items.Contains(item) || item.Location != Container.Inventory) return null;
            return t.Tx.ClaimSlot(item.Location, item.Slot) == item ? item : null;
        }

        private static Item NpcTradeItem(CharData c, NpcTradeTransaction t, int tradeSlot) =>
            tradeSlot < 0 || tradeSlot >= t.Slots.Length ? null : NpcTradeResolve(c, t, ref t.Slots[tradeSlot]);

        // confirm: bounded by the offer as well as the stack, so a script cannot reach past what was traded
        private static bool NpcTradeConfirm(CharData c, NpcTradeTransaction t, int tradeSlot, uint quantity)
        {
            if (tradeSlot < 0 || tradeSlot >= t.Slots.Length) return false;
            ref var slot = ref t.Slots[tradeSlot];
            var item = NpcTradeResolve(c, t, ref slot);
            if (item == null || quantity > slot.Quantity || item.Quantity < quantity) return false;
            slot.Confirmed = quantity;
            return true;
        }

        // releaseUnconfirmed: the offer is kept, so a script can still confirm it during a later event
        private static void NpcTradeReleaseUnconfirmed(NpcTradeTransaction t)
        {
            foreach (var slot in t.Slots)
                if (slot.Confirmed == 0 && slot.Offered != null) t.Tx.Release(slot.Offered);
        }

        // consumeConfirmed / consumeAll: all or nothing, audited once the trade went through
        private bool NpcTradeConsume(CharData c, NpcTradeTransaction t, bool confirmedOnly)
        {
            var traded = new List<(ushort ItemId, uint Qty)>();
            foreach (var slot in t.Slots)
            {
                uint qty = confirmedOnly ? slot.Confirmed : slot.Quantity;
                if (qty > 0 && slot.Offered != null && c.Items.Contains(slot.Offered)) traded.Add((slot.Offered.ItemId, qty));
            }

            for (int i = 0; i < t.Slots.Length; i++)
            {
                ref var slot = ref t.Slots[i];
                if (slot.Offered == null) continue;
                uint qty = confirmedOnly ? slot.Confirmed : slot.Quantity;
                if (qty == 0) continue;
                byte invSlot = slot.InvSlot;
                if (NpcTradeResolve(c, t, ref slot) == null)
                {
                    log_($"NpcTradeTransaction: {c.Name} no longer holds the item offered in slot {invSlot}");
                    slot = default;
                    t.Tx.Dispose();
                    return false;
                }
                if (!t.Tx.Take(Container.Inventory, slot.Offered.Slot, qty))
                {
                    log_($"NpcTradeTransaction: {c.Name} kept the item in slot {invSlot} after trading it away");
                    t.Tx.Dispose();
                    return false;
                }
                slot = default;
            }
            if (!t.Tx.Commit()) return false;

            foreach (var (itemId, qty) in traded) AuditTrade(c.Id, c.Name, t.NpcId, t.NpcName, itemId, qty);
            return true;
        }

        // removeTransaction: an offer still open gives its claims back
        private static void RemoveNpcTrade(CharData c)
        {
            c.NpcTrade?.Tx.Dispose();
            c.NpcTrade = null;
        }

        // ---- GP_CLI_COMMAND_ITEM_TRANSFER (0x036): items offered to an NPC ------------------------------------------

        // a Monipulator can only trade with the Feretory's NPCs
        private static PacketValidator ValidateItemTransfer(CharData c, BasicPacket p) => Pv(c)
            .BlockedBy(BlockedState.InEvent)
            .MustEqual(c.Monstrosity == null || c.Zone == Monstrosity.ZoneFeretory ? 1 : 0, 1, "Monipulators only trade in the Feretory")
            .Range("ItemNum", p.Buffer[60], 1, TradeMaxNpcSlots);

        private void OnItemTransfer(MapSession s, CharData c, BasicPacket p)
        {
            uint uniqueNo = U32(p, 4);
            ushort actIndex = BitConverter.ToUInt16(p.Buffer, 58);
            int count = p.Buffer[60];

            if (c.StatusEffects.HasStatusEffectByFlag(EffectFlag.Invisible))
            {
                Push(s, SystemMessage(MsgStdCannotWhileInvisible));
                return;
            }

            // GetEntity(ActIndex, TYPE_NPC | TYPE_MOB): below 0x400 the mobs first, then the NPCs; the dynamic indexes
            // (0x700 - 0xFFF) the NPCs first, then the mobs; the chars' range and anything above holds neither
            var area = AreaOf(c);
            MobEntity mob = area.Mobs.Find(m => m.TargId == actIndex);
            NpcEntity npcFound = area.Npcs.Find(n => n.TargId == actIndex);
            object target = actIndex < 0x400 ? (object)mob ?? npcFound
                          : actIndex >= 0x700 && actIndex < 0x1000 ? (object)npcFound ?? mob : null;
            var (npcId, npcName, nx, ny, nz) = target switch
            {
                NpcEntity n => (n.Id, n.Name, n.X, n.Y, n.Z),
                MobEntity m => (m.Id, m.Name, m.X, m.Y, m.Z),
                _ => (0u, null, 0f, 0f, 0f),
            };
            // the NPC must match UniqueNo and be within 6.0' of the player
            if (target == null || npcId != uniqueNo) return;
            float dx = nx - c.X, dy = ny - c.Y, dz = nz - c.Z;
            if (MathF.Sqrt(dx * dx + dy * dy + dz * dz) > 6.0f) return;
            if (c.MoghouseId != 0 && c.MoghouseId != c.Id) return;
            if (target is NpcEntity { ContentDisabled: true }) return;
            // only a mob whose status is that of an NPC (Normal) is traded with
            if (target is MobEntity { Status: not NpcEntity.StatusNormal }) return;

            // close any previous offer before this one replaces it
            RemoveNpcTrade(c);
            c.Trade.Clean();

            string who = $"GP_CLI_COMMAND_ITEM_TRANSFER: {c.Name}";
            var tradeItems = new Item[TradeMaxNpcSlots];
            for (int slotId = 0; slotId < count; slotId++)
            {
                byte invSlotId = p.Buffer[48 + slotId];
                uint quantity = U32(p, 8 + slotId * 4);
                if (quantity == 0)
                {
                    log_($"{who} tried to trade NPC {npcName} with zero quantity in slot {invSlotId}!");
                    return;
                }
                var item = StorageItem(c, Container.Inventory, invSlotId);
                if (item == null)
                {
                    log_($"{who} trying to trade NPC {npcName} with an empty inventory slot {invSlotId}!");
                    return;
                }
                if (item.Quantity < quantity)
                {
                    log_($"{who} trying to trade NPC {npcName} with {quantity} of item {item.ItemId} " +
                         "they do not have!");
                    return;
                }
                if (Array.IndexOf(tradeItems, item, 0, slotId) >= 0)
                {
                    log_($"{who} trying to trade NPC {npcName} with duplicate inventory slot {invSlotId}!");
                    return;
                }
                if (IsBusy(c, item))
                {
                    log_($"{who} trying to trade NPC {npcName} with locked item {item.ItemId}!");
                    return;
                }
                tradeItems[slotId] = item;
            }

            var transaction = c.NpcTrade = new NpcTradeTransaction(this, c, npcId, npcName);
            for (int slotId = 0; slotId < count; slotId++)
            {
                var item = tradeItems[slotId];
                uint quantity = U32(p, 8 + slotId * 4);
                if (!NpcTradeStage(c, transaction, slotId, item, quantity))
                {
                    log_($"GP_CLI_COMMAND_ITEM_TRANSFER: {c.Name} could not offer item {item.ItemId} to NPC {npcName}");
                    RemoveNpcTrade(c);
                    c.Trade.Clean();
                    return;
                }
                c.Trade.SetItem(slotId, item.ItemId, item.Slot, quantity);
            }

            OnTrade(c, target, npcName);

            // looked up again rather than reused: a script can finish the trade from onTrade, which ends the transaction
            if (c.NpcTrade != null) NpcTradeReleaseUnconfirmed(c.NpcTrade);

            // retail: if the trade started an event then any current synth is a crit fail
            if (c.IsInEvent) ForceSynthCritFail("GP_CLI_COMMAND_ITEM_TRANSFER", s, c);
        }

        // luautils::OnTrade
        private void OnTrade(CharData c, object npc, string npcName)
        {
            if (lua_ == null) return;
            string file = $"./scripts/zones/{AreaOf(c).Name}/npcs/{npcName}.lua";
            c.EventPreparation.TargetEntity = npc;
            c.EventPreparation.ScriptFile = file;

            int top = lua_.Top;
            try
            {
                lua_.GetPath("InteractionGlobal.onTrade");
                lua_.PushObject(c);
                lua_.PushObject(npc);
                lua_.PushObject(TradeView(c), TradeContainerType);
                scripts_.PushFileFunction(file, "onTrade");
                lua_.Call(4, 0);
            }
            catch (LuaException e)
            {
                ScriptError("onTrade", e);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // ---- CLuaTradeContainer -------------------------------------------------------------------------------------

        // what CTradeContainer is in Lua: the char's TradeContainer, with the NPC trade it offers
        internal sealed class LuaTrade
        {
            public CharData Owner;
        }

        private readonly Dictionary<CharData, LuaTrade> tradeViews_ = new Dictionary<CharData, LuaTrade>();

        private LuaTrade TradeView(CharData c)
        {
            if (!tradeViews_.TryGetValue(c, out var view)) tradeViews_[c] = view = new LuaTrade { Owner = c };
            return view;
        }

        private void RegisterTradeBindings(Dictionary<string, LuaFunction> m)
        {
            // CLuaBaseEntity
            m["confirmTrade"] = l => Ret(l, Pc(l, "confirmTrade") is CharData c && FinishNpcTrade(c, "confirmTrade", true));
            m["tradeComplete"] = l => Ret(l, Pc(l, "tradeComplete") is CharData c && FinishNpcTrade(c, "tradeComplete", false));
            m["getTrade"] = l =>
            {
                l.PushObject(Pc(l, "getTrade") is CharData c ? TradeView(c) : null, TradeContainerType);
                return 1;
            };

            // sol2's uint8 / uint32 conversion of an optional number argument
            static int OptSlot(LuaState l, int idx) => l.IsNumber(idx) ? (byte)l.ToIntegral(idx) : 0;
            CharData Owner(LuaState l) => (l.ToObject(1) as LuaTrade)?.Owner;
            TradeContainer Trade(LuaState l) => Owner(l).Trade;

            var trade = new Dictionary<string, LuaFunction>
            {
                ["getGil"] = l => Ret(l, Trade(l).GetItemId(0) == GilItemId ? Trade(l).GetQuantity(0) : 0u),
                ["getItem"] = l =>
                {
                    var c = Owner(l);
                    l.PushObject(c.NpcTrade != null ? NpcTradeItem(c, c.NpcTrade, OptSlot(l, 2)) : null, ItemType);
                    return 1;
                },
                ["getItemId"] = l => Ret(l, Trade(l).GetItemId(OptSlot(l, 2))),
                ["getItemSubId"] = l =>
                {
                    var c = Owner(l);
                    var item = c.NpcTrade != null ? NpcTradeItem(c, c.NpcTrade, OptSlot(l, 2)) : null;
                    return Ret(l, item != null ? ItemKind(item.ItemId)?.SubId ?? 0 : 0);
                },
                ["getItemCount"] = l =>
                {
                    var t = Trade(l);
                    uint total = 0;
                    for (int i = 0; i < t.Size; i++) total += t.GetItemId(i) == GilItemId ? 1 : t.GetQuantity(i);
                    return Ret(l, total);
                },
                ["getSlotCount"] = l =>
                {
                    var t = Trade(l);
                    int count = 0;
                    for (int i = 0; i < t.Size; i++)
                        if (t.GetItemId(i) != 0) count++;
                    return Ret(l, count);
                },
                ["getItemQty"] = l => Ret(l, TradeItemQuantity(Trade(l), (ushort)l.ToUIntArg(2))),
                ["getSlotQty"] = l => Ret(l, Trade(l).GetQuantity((byte)l.ToUIntArg(2))),
                ["hasItemQty"] = l => Ret(l, TradeItemQuantity(Trade(l), (ushort)l.ToUIntArg(2)) == l.ToUIntArg(3)),
                ["confirmItem"] = l =>
                {
                    var c = Owner(l);
                    ushort itemId = (ushort)l.ToUIntArg(2);
                    uint amount = l.IsNumber(3) ? (uint)l.ToIntegral(3) : 1;
                    var t = c.Trade;
                    for (int slot = 0; slot < t.Size; slot++)
                    {
                        if (t.GetItemId(slot) != itemId) continue;
                        uint available = t.GetQuantity(slot);
                        if (available < amount)
                        {
                            if (!TradeConfirmQuantity(c, slot, available)) return Ret(l, false);
                            amount -= available;
                        }
                        else return Ret(l, TradeConfirmQuantity(c, slot, amount));
                    }
                    return Ret(l, false);
                },
                ["confirmSlot"] = l => Ret(l, TradeConfirmQuantity(Owner(l), (byte)l.ToUIntArg(2), l.IsNumber(3) ? (uint)l.ToIntegral(3) : 1)),
                ["clean"] = l =>
                {
                    Trade(l).Clean();
                    return 0;
                },
            };
            lua_.RegisterType(TradeContainerType, trade);
        }

        private const string TradeContainerType = "CTradeContainer";

        // CTradeContainer::getItemQuantity: over every slot holding the item
        private static uint TradeItemQuantity(TradeContainer t, ushort itemId)
        {
            uint quantity = 0;
            for (int i = 0; i < t.Size; i++)
                if (t.GetItemId(i) == itemId) quantity += t.GetQuantity(i);
            return quantity;
        }

        private static bool TradeConfirmQuantity(CharData c, int slot, uint quantity) =>
            c.NpcTrade != null && NpcTradeConfirm(c, c.NpcTrade, slot, quantity);

        // CLuaBaseEntity::confirmTrade (the confirmed items) / tradeComplete (everything offered)
        private bool FinishNpcTrade(CharData c, string caller, bool confirmedOnly)
        {
            var transaction = c.NpcTrade;
            if (transaction == null)
            {
                log_($"CLuaBaseEntity::{caller}: {c.Name} has no trade to {(confirmedOnly ? "confirm" : "complete")}");
                return false;
            }
            bool tookEverything = NpcTradeConsume(c, transaction, confirmedOnly);
            if (!tookEverything) log_($"CLuaBaseEntity::{caller}: {c.Name} kept the {(confirmedOnly ? "confirmed" : "traded")} items");
            RemoveNpcTrade(c);
            c.Trade.Clean();
            PushTo(c, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
            return tookEverything;
        }

        // ---- PlayerTradeTransaction ---------------------------------------------------------------------------------

        internal sealed class PlayerTrade
        {
            public struct Slot
            {
                public Item Staged;
                public byte InvSlot;
                public uint Qty;
            }

            public sealed class Side
            {
                public CharData Who;
                public ItemTransaction Tx;
                public readonly Slot[] Slots = new Slot[PlayerTradeMaxSlots];
                public bool Accepted;
            }

            public readonly Side[] Sides = new Side[2];
        }

        // tradePartner: only while both sides' TradePending agree
        private CharData TradePartner(CharData c)
        {
            if (c.TradePending.UniqueNo == 0) return null;
            var (other, _) = CharById(c.TradePending.UniqueNo);
            return other != null && other.TradePending.UniqueNo == c.Id ? other : null;
        }

        // activePlayerTradeTransaction: owned by the initiator
        private PlayerTrade ActivePlayerTrade(CharData c) => c.PlayerTrade ?? TradePartner(c)?.PlayerTrade;

        // a side no longer resolves once its char has left the map
        private CharData SideChar(PlayerTrade.Side side) => charSessions_.ContainsKey(side.Who) ? side.Who : null;

        private static Item StagedItem(PlayerTrade.Side side, in PlayerTrade.Slot slot) =>
            slot.Staged != null && side.Who.Items.Contains(slot.Staged) && slot.Staged.Location == Container.Inventory ? slot.Staged : null;

        private static bool WithinTradeRange(CharData a, CharData b) => Distance(a, b) <= TradeRange && a.MoghouseId == b.MoghouseId;

        // start: registers on the initiator; null when refused, the caller cleans up TradePending
        private PlayerTrade StartPlayerTrade(CharData initiator, CharData target)
        {
            bool invalid = initiator == null || target == null || initiator == target;
            if (invalid || ActivePlayerTrade(initiator) != null || ActivePlayerTrade(target) != null || !WithinTradeRange(initiator, target))
                return null;
            var trade = new PlayerTrade();
            trade.Sides[0] = new PlayerTrade.Side { Who = initiator, Tx = new ItemTransaction(this, initiator) };
            trade.Sides[1] = new PlayerTrade.Side { Who = target, Tx = new ItemTransaction(this, target) };
            return initiator.PlayerTrade = trade;
        }

        // cancel: for `leaving`, whether or not the transaction got as far as being created
        private void CancelPlayerTrade(CharData leaving)
        {
            var trade = ActivePlayerTrade(leaving);
            if (trade != null)
            {
                AbortPlayerTrade(trade, leaving);
                return;
            }
            var partner = TradePartner(leaving);
            if (partner == null) return;
            leaving.TradePending.Clean();
            partner.TradePending.Clean();
            PushTo(partner, TradeRes(leaving, TradeResCancell));
        }

        private static PlayerTrade.Side SideOf(PlayerTrade trade, CharData who) =>
            trade.Sides[0].Who == who ? trade.Sides[0] : trade.Sides[1].Who == who ? trade.Sides[1] : null;

        private CharData PartnerOf(PlayerTrade trade, CharData who) =>
            trade.Sides[0].Who == who ? SideChar(trade.Sides[1]) : trade.Sides[1].Who == who ? SideChar(trade.Sides[0]) : null;

        // accept: true once both sides have
        private static bool AcceptPlayerTrade(PlayerTrade trade, CharData who)
        {
            var side = SideOf(trade, who);
            if (side != null) side.Accepted = true;
            return trade.Sides[0].Accepted && trade.Sides[1].Accepted;
        }

        private static void ReleaseTradeSlot(PlayerTrade.Side side, ref PlayerTrade.Slot slot)
        {
            if (slot.Staged != null) side.Tx.Release(slot.Staged);
            slot = default;
        }

        // setSlot: stages or clears a slot (qty 0 clears it), clears both acceptances and shows both sides
        private Item SetPlayerTradeSlot(PlayerTrade trade, CharData who, byte transactionSlot, byte inventorySlot, ushort expectedItemId, uint qty)
        {
            if (transactionSlot >= PlayerTradeMaxSlots) return null;
            var side = SideOf(trade, who);
            var other = PartnerOf(trade, who);
            if (side == null || other == null) return null;

            void PushSlotView(byte slotIdx, Item shown = null, uint viewQty = 0)
            {
                PushTo(who, TradeMyList(shown, slotIdx, viewQty));
                PushTo(other, TradeList(shown, slotIdx, viewQty));
            }
            void ReleaseAndRestore(ref PlayerTrade.Slot target)
            {
                var released = StagedItem(side, target);
                ReleaseTradeSlot(side, ref target);
                if (released != null) PushTo(who, LoginPackets.ItemList(released, LockNormal));
            }

            // any slot change invalidates both sides' Make
            trade.Sides[0].Accepted = false;
            trade.Sides[1].Accepted = false;

            ReleaseAndRestore(ref side.Slots[transactionSlot]);
            if (qty == 0)
            {
                PushSlotView(transactionSlot);
                return null;
            }

            // retail drops the earlier stage and refuses the new one when the same inventory slot is staged twice
            for (byte slotIdx = 0; slotIdx < PlayerTradeMaxSlots; slotIdx++)
            {
                if (slotIdx == transactionSlot) continue;
                ref var candidate = ref side.Slots[slotIdx];
                if (candidate.Staged == null || candidate.InvSlot != inventorySlot) continue;
                ReleaseAndRestore(ref candidate);
                PushSlotView(slotIdx);
                PushSlotView(transactionSlot);
                return null;
            }

            var item = StorageItem(who, Container.Inventory, inventorySlot);
            uint flags = item != null ? ItemBasic(item.ItemId)?.Flags ?? 0 : 0;
            bool wrongItem = item == null || item.ItemId != expectedItemId;
            bool exclusiveItem = (flags & ItemFlagExclusive) != 0;
            bool qtyExceeds = item != null && qty > item.Quantity;
            bool rareOnPartner = item != null && (flags & ItemFlagRare) != 0 && HasItem(other, item.ItemId);
            var staged = wrongItem || exclusiveItem || qtyExceeds || rareOnPartner ? null : side.Tx.ClaimSlot(Container.Inventory, inventorySlot);
            if (staged == null)
            {
                PushSlotView(transactionSlot);
                return null;
            }

            PushTo(who, LoginPackets.ItemList(item, LockNoSelect));
            side.Slots[transactionSlot] = new PlayerTrade.Slot { Staged = item, InvSlot = inventorySlot, Qty = qty };
            PushSlotView(transactionSlot, item, qty);
            return item;
        }

        // closeAndRemove: both sides' pending trade goes, the staged items are ungreyed, the claims released
        private void ClosePlayerTrade(PlayerTrade trade)
        {
            foreach (var side in trade.Sides)
            {
                var c = SideChar(side);
                if (c == null) continue;
                c.TradePending.Clean();
                foreach (var slot in side.Slots)
                {
                    var item = StagedItem(side, slot);
                    if (item != null) PushTo(c, LoginPackets.ItemList(item, LockNormal));
                }
            }
            foreach (var side in trade.Sides)
            {
                for (int i = 0; i < side.Slots.Length; i++) ReleaseTradeSlot(side, ref side.Slots[i]);
                side.Tx.Dispose();
            }
            if (trade.Sides[0].Who.PlayerTrade == trade) trade.Sides[0].Who.PlayerTrade = null;
        }

        private void AbortPlayerTrade(PlayerTrade trade, CharData leaving)
        {
            var other = PartnerOf(trade, leaving);
            if (other != null) PushTo(other, TradeRes(leaving, TradeResCancell));
            else log_($"PlayerTradeTransaction::abort: {leaving.Name} is not part of this trade");
            ClosePlayerTrade(trade);
        }

        private void CommitAndClosePlayerTrade(PlayerTrade trade)
        {
            var initiator = SideChar(trade.Sides[0]);
            var target = SideChar(trade.Sides[1]);
            if (initiator == null || target == null)
            {
                log_("PlayerTradeTransaction::commitAndClose: a side left before the trade closed");
                ClosePlayerTrade(trade);
                return;
            }

            bool ok = CommitPlayerTrade(trade, initiator, target);
            if (!ok)
            {
                log_($"PlayerTradeTransaction::commitAndClose: trade refused ({initiator.Name} <-> {target.Name})");
                PushTo(initiator, SelfPackets.Message(MsgStdTradeCanceled));
                PushTo(target, SelfPackets.Message(MsgStdTradeCanceled));
            }
            uint kind = ok ? TradeResEnd : TradeResCancell;
            PushTo(initiator, TradeRes(target, kind));
            PushTo(target, TradeRes(initiator, kind));
            ClosePlayerTrade(trade);
        }

        // canReceive: inventory room for every staged slot, the gil fitting in one stack, no rare item already held
        private bool CanReceive(PlayerTrade.Side sender, CharData receiver)
        {
            int slotsNeeded = 0;
            ulong gilOffered = 0;
            foreach (var slot in sender.Slots)
            {
                if (slot.Staged == null) continue;
                slotsNeeded++;
                if (slot.Staged.ItemId == GilItemId) gilOffered += slot.Qty;
            }
            if (slotsNeeded == 0) return true;
            if (FreeSlots(receiver, Container.Inventory) < slotsNeeded) return false;
            if (gilOffered > 0)
            {
                var gil = StorageItem(receiver, Container.Inventory, 0);
                if (gil == null || gil.Quantity + gilOffered > StackSize(GilItemId)) return false;
            }
            foreach (var slot in sender.Slots)
            {
                var staged = StagedItem(sender, slot);
                if (staged != null && ((ItemBasic(staged.ItemId)?.Flags ?? 0) & ItemFlagRare) != 0 && HasItem(receiver, staged.ItemId))
                    return false;
            }
            return true;
        }

        // doCommit: deliver to both sides before consuming either, so a failed delivery can be undone; the steps are
        // undone unless every one of them went through
        private bool CommitPlayerTrade(PlayerTrade trade, CharData initiator, CharData target)
        {
            bool bothAccepted = trade.Sides[0].Accepted && trade.Sides[1].Accepted;
            bool canReceive = CanReceive(trade.Sides[0], target) && CanReceive(trade.Sides[1], initiator);
            if (!bothAccepted || !canReceive || !WithinTradeRange(initiator, target)) return false;

            var traded = new List<(CharData From, CharData To, ushort ItemId, uint Qty)>();
            foreach (var slot in trade.Sides[0].Slots)
                if (StagedItem(trade.Sides[0], slot) is Item i0) traded.Add((initiator, target, i0.ItemId, slot.Qty));
            foreach (var slot in trade.Sides[1].Slots)
                if (StagedItem(trade.Sides[1], slot) is Item i1) traded.Add((target, initiator, i1.ItemId, slot.Qty));

            // cloned rather than spawned by id, so exdata and signature survive the trade
            bool Deliver(PlayerTrade.Side sender, PlayerTrade.Side receiver)
            {
                foreach (var slot in sender.Slots)
                {
                    var staged = StagedItem(sender, slot);
                    if (staged == null) continue;
                    var clone = CloneItem(staged);
                    clone.Quantity = slot.Qty;
                    if (receiver.Tx.GiveItem(Container.Inventory, clone) == null) return false;
                }
                return true;
            }
            bool Consume(PlayerTrade.Side sender)
            {
                for (int i = 0; i < sender.Slots.Length; i++)
                {
                    ref var slot = ref sender.Slots[i];
                    if (slot.Staged == null) continue;
                    var staged = StagedItem(sender, slot);
                    if (staged == null || !sender.Tx.Take(Container.Inventory, staged.Slot, slot.Qty))
                    {
                        log_($"PlayerTradeTransaction::doCommit: {sender.Who.Name} kept item {slot.Staged.ItemId} after it was handed over");
                        return false;
                    }
                    ReleaseTradeSlot(sender, ref slot);
                }
                return true;
            }

            bool exchanged = Deliver(trade.Sides[0], trade.Sides[1]) && Deliver(trade.Sides[1], trade.Sides[0]) &&
                             Consume(trade.Sides[0]) && Consume(trade.Sides[1]);
            if (!exchanged)
            {
                trade.Sides[1].Tx.Dispose();
                trade.Sides[0].Tx.Dispose();
                return false;
            }
            trade.Sides[0].Tx.Commit();
            trade.Sides[1].Tx.Commit();
            foreach (var t in traded) AuditTrade(t.From.Id, t.From.Name, t.To.Id, t.To.Name, t.ItemId, t.Qty);
            return true;
        }

        // removeCharFromZone: a trade in progress is aborted and an NPC offer left open gives its claims back
        private void CloseTrades(CharData c)
        {
            var trade = ActivePlayerTrade(c);
            if (trade != null) AbortPlayerTrade(trade, c);
            RemoveNpcTrade(c);
            c.Trade.Clean();
            c.TradePending.Clean();
            tradeViews_.Remove(c);
        }

        // ---- S2C ----------------------------------------------------------------------------------------------------

        // 0x021 GP_SERV_COMMAND_ITEM_TRADE_REQ
        private static BasicPacket TradeReq(CharData from)
        {
            var w = new PacketWriter(0x021, 0x0C);
            w.U32(4, from.Id);
            w.U16(8, from.TargId);
            return w.Packet;
        }

        // 0x022 GP_SERV_COMMAND_ITEM_TRADE_RES
        private static BasicPacket TradeRes(CharData who, uint kind)
        {
            var w = new PacketWriter(0x022, 0x10);
            w.U32(4, who.Id);
            w.U32(8, kind);
            w.U16(12, who.TargId);
            return w.Packet;
        }

        // 0x023 GP_SERV_COMMAND_ITEM_TRADE_LIST: what the partner offers; a charged item shows its charges, a linkshell
        // its id, colour, type and name (not its key), anything else its exdata
        private BasicPacket TradeList(Item item, byte slot, uint qty)
        {
            var w = new PacketWriter(0x023, 0x28);
            w.U32(4, qty);
            w.U8(13, slot);
            if (item == null || qty == 0)
            {
                w.U16(10, EmptyTradeSlotItemNo);
                return w.Packet;
            }
            w.U16(10, item.ItemId);
            var kind = ItemKind(item.ItemId);
            if (kind?.Charged == true)
            {
                w.U8(14, 0x01);
                if (item.Extra[1] > 0) w.U8(15, item.Extra[1]);
            }
            else if (kind?.Type == ItemTypeLinkshell)
            {
                w.Bytes(14, item.Extra.AsSpan(0, 4));
                w.Bytes(20, item.Extra.AsSpan(6, 18));
            }
            else w.Bytes(14, item.Extra.AsSpan(0, Math.Min(item.Extra.Length, 24)));
            return w.Packet;
        }

        // 0x025 GP_SERV_COMMAND_ITEM_TRADE_MYLIST: the char's own offer
        private static BasicPacket TradeMyList(Item item, byte slot, uint qty)
        {
            var w = new PacketWriter(0x025, 0x0C);
            w.U8(10, slot);
            if (item == null || qty == 0)
            {
                w.U16(8, EmptyTradeSlotItemNo);
                return w.Packet;
            }
            w.U32(4, qty);
            w.U16(8, item.ItemId);
            w.U8(11, item.Slot);
            return w.Packet;
        }

        // ---- C2S ----------------------------------------------------------------------------------------------------

        // GP_CLI_COMMAND_TRADE_REQ (0x032)
        private static PacketValidator ValidateTradeReq(CharData c, BasicPacket p) => Pv(c)
            .BlockedBy(BlockedState.InEvent)
            .MustNotEqual(c.Id, U32(p, 4), "Character trading with itself");

        private void OnTradeReq(MapSession s, CharData c, BasicPacket p)
        {
            uint uniqueNo = U32(p, 4);
            ushort actIndex = BitConverter.ToUInt16(p.Buffer, 8);
            CharData target = null;
            foreach (var (o, _) in ZoneChars(c))
            {
                if (o.TargId == actIndex)
                {
                    target = o;
                    break;
                }
            }
            if (target == null || target.Id != uniqueNo) return;

            // retail answers a Monipulator's request with the generic error, and a request to one with its own message
            if (c.Monstrosity != null)
            {
                PushTo(c, TradeRes(target, TradeResErrEtc));
                return;
            }
            if (target.Monstrosity != null)
            {
                PushTo(c, SystemMessage(MsgStdCannotTradeWithTarget, target.Id));
                return;
            }

            void Refuse(string reason)
            {
                log_($"{c.Name} -> {target.Name} trade refused: {reason}");
                PushTo(c, TradeRes(target, TradeResErrYouTrade));
            }

            if (InPrison(c) || InPrison(target))
            {
                Refuse("in prison");
                return;
            }
            if (target.BlockingAid && !InAllianceOf(target, c))
            {
                Push(s, SystemMessage(MsgStdTargetIsCurrentlyBlocking));
                PushTo(target, SystemMessage(MsgStdBlockedByBlockaid));
                Refuse("blockaid");
                return;
            }
            if (ActivePlayerTrade(c) != null)
            {
                Refuse("already in a trade");
                return;
            }
            // already sent this char a request
            if (target.TradePending.UniqueNo == c.Id) return;
            if (ActivePlayerTrade(target) != null)
            {
                Refuse("target already in a trade");
                return;
            }
            long now = NowMs();
            if (target.TradePending.ActIndex != 0 && now - target.TradePending.InvitedAtMs < 60000)
            {
                Refuse("target has a recent unanswered invite");
                return;
            }

            // usually the client cancels its old trade itself (a 0x033 Cancell from a yes/no menu), but not always
            var oldTarget = TradePartner(c);
            if (oldTarget != null)
            {
                oldTarget.TradePending.Clean();
                c.TradePending.Clean();
                PushTo(oldTarget, TradeRes(c, TradeResErrYouTrade));
                PushTo(c, TradeRes(oldTarget, TradeResErrYouTrade));
                return;
            }

            (c.TradePending.UniqueNo, c.TradePending.ActIndex, c.TradePending.InvitedAtMs, c.TradePending.Initiator) = (target.Id, target.TargId, now, true);
            (target.TradePending.UniqueNo, target.TradePending.ActIndex, target.TradePending.InvitedAtMs, target.TradePending.Initiator) = (c.Id, c.TargId, now, false);
            PushTo(target, TradeReq(c));
        }

        // GP_CLI_COMMAND_TRADE_RES (0x033)
        private static PacketValidator ValidateTradeRes(CharData c, BasicPacket p) => Pv(c)
            .BlockedBy(BlockedState.InEvent)
            .OneOf("Kind", U32(p, 4), 0, 1, 2, 3)
            .MustNotEqual(c.TradePending.ActIndex, 0, "No pending trade target");

        private void OnTradeRes(MapSession s, CharData c, BasicPacket p)
        {
            var target = TradePartner(c);
            if (target == null)
            {
                log_("GP_CLI_COMMAND_TRADE_RES: Could not find trade targets.");
                return;
            }
            switch (U32(p, 4))
            {
                case 0: // Start
                    if (ActivePlayerTrade(c) != null || c.TradePending.Initiator) return;
                    if (StartPlayerTrade(c, target) == null)
                    {
                        log_($"GP_CLI_COMMAND_TRADE_RES: Start refused ({c.Name} -> {target.Name})");
                        c.TradePending.Clean();
                        target.TradePending.Clean();
                        return;
                    }
                    PushTo(c, TradeRes(target, TradeResStart));
                    PushTo(target, TradeRes(c, TradeResStart));
                    break;
                case 1: // Cancell
                    CancelPlayerTrade(c);
                    break;
                case 2: // Make: tell the other side, and once both have, swap and close
                    PushTo(target, TradeRes(c, TradeResMake));
                    var trade = ActivePlayerTrade(c);
                    if (trade != null && AcceptPlayerTrade(trade, c)) CommitAndClosePlayerTrade(trade);
                    break;
                case 3: // MakeCancell: XiPackets say the client can send it, but not when
                    log_($"GP_CLI_COMMAND_TRADE_RES: MakeCancell received from {c.Name}");
                    break;
            }
        }

        // GP_CLI_COMMAND_TRADE_LIST (0x034)
        private static PacketValidator ValidateTradeList(CharData c, BasicPacket p) => Pv(c)
            .BlockedBy(BlockedState.InEvent)
            .MustNotEqual(c.TradePending.UniqueNo, 0, "No trade target")
            .Range("TradeIndex", p.Buffer[11], 0, 8)
            .Custom(v => v.MustEqual(BitConverter.ToUInt16(p.Buffer, 8) == GilItemId ? 1 : 0, p.Buffer[11] == PlayerTradeGilSlot ? 1 : 0,
                                     "Gil belongs in the gil trade slot"));

        private void OnTradeList(MapSession s, CharData c, BasicPacket p)
        {
            uint itemNum = U32(p, 4);
            ushort itemNo = BitConverter.ToUInt16(p.Buffer, 8);
            byte itemIndex = p.Buffer[10], tradeIndex = p.Buffer[11];

            var partner = TradePartner(c);
            var trade = ActivePlayerTrade(c);
            if (partner == null || trade == null)
            {
                log_($"GP_CLI_COMMAND_TRADE_LIST: no active player trade for {c.Name}");
                return;
            }

            // a pearl can only be offered with its linkshell equipped
            var offered = StorageItem(c, Container.Inventory, itemIndex);
            if (itemNum > 0 && offered != null && ItemKind(offered.ItemId)?.Type == ItemTypeLinkshell && !HasLinkshellEquipped(c, offered))
            {
                PushTo(c, SelfPackets.Message(MsgStdLinkshellEquipBeforeUsing));
                return;
            }
            SetPlayerTradeSlot(trade, c, tradeIndex, itemIndex, itemNo, itemNum);
        }

        // the LSID is the exdata's GroupId
        private static bool HasLinkshellEquipped(CharData c, Item offered)
        {
            uint lsid = BitConverter.ToUInt32(offered.Extra, 0);
            foreach (int slot in new[] { SlotLink1, SlotLink2 })
            {
                var equipped = CharStats.EquippedItem(c, slot);
                if (equipped != null && BitConverter.ToUInt32(equipped.Extra, 0) == lsid) return true;
            }
            return false;
        }
    }
}
