using System;
using System.Collections.Generic;
using XI.Common;
using XI.Map;
using XI.Script;

namespace XI.Zone
{
    // Guild shops: scripts/globals/guild_shops.lua keeps the stock and prices; the core remembers which NPC's shop the
    // char opened (openGuildShop), asks the script for the lists and trades (c2s/0x0AA-0x0AD), claims the stacks a sale
    // takes (GuildSellTransaction) and closes the shop on the hour the script says (time_server's onGameHour).
    public sealed partial class ZoneHost
    {
        private const int GuildListMax = 30;        // GP_GUILD_ITEM List[30]
        private const int GuildListSize = 4 + 244;  // the list, Count and Stat, padded to the int32 of GP_GUILD_ITEM
        private const byte GuildOpen = 0, GuildClose = 1, GuildHoliday = 2;
        private const sbyte GuildBuyRejected = -1, GuildSellRejected = -4;

        // GP_GUILD_ITEM
        private readonly struct GuildItem
        {
            public readonly ushort ItemNo;
            public readonly byte Count, Max;
            public readonly int Price;

            public GuildItem(ushort itemNo, byte count, byte max, int price)
            {
                ItemNo = itemNo;
                Count = count;
                Max = max;
                Price = price;
            }
        }

        // the Vana'diel clock openGuildShop reads; injectable for the harness
        public Func<long> VanaTicksNow = EarthTime.VanaTicks;

        private long lastVanaHour_ = -1;

        private void RegisterGuildBindings(Dictionary<string, LuaFunction> m)
        {
            m["openGuildShop"] = OpenGuildShop;
            m["clearGuildShop"] = l =>
            {
                if (Pc(l, "clearGuildShop") is CharData c) c.GuildShopNpc = 0;
                return 0;
            };
            m["sendGuildClose"] = l =>
            {
                if (Pc(l, "sendGuildClose") is not CharData c) return 0;
                bool passive = !l.IsNil(4) && l.ToBoolean(4);
                PushTo(c, GuildOpenPacket(GuildClose, (byte)l.ToIntArg(2), (byte)l.ToIntArg(3), 0, passive));
                return 0;
            };
            m["facePlayer"] = FacePlayer;
        }

        // CLuaBaseEntity::openGuildShop(npc, open, close, holiday?): the status packet, and whether the shop is open now
        private int OpenGuildShop(LuaState l)
        {
            if (Pc(l, "openGuildShop") is not CharData c) return Ret(l, false);
            if (l.ToObject(2) is not NpcEntity npc)
            {
                log_("lua: Invalid guild shop NPC passed to openGuildShop().");
                return Ret(l, false);
            }
            byte open = (byte)l.ToIntArg(3), close = (byte)l.ToIntArg(4);
            byte? holiday = l.IsNumber(5) ? (byte)l.ToIntegral(5) : null;

            long now = VanaTicksNow();
            bool isHoliday = holiday.HasValue && holiday.Value == VanaTime.GetWeekDay(now);
            uint hour = VanaTime.GetHour(now);
            bool isOpen = !isHoliday && hour >= open && hour < close;
            byte status = isHoliday ? GuildHoliday : isOpen ? GuildOpen : GuildClose;

            c.GuildShopNpc = npc.Id;
            PushTo(c, GuildOpenPacket(status, open, close, holiday ?? 0, false));
            return Ret(l, isOpen);
        }

        // CLuaBaseEntity::facePlayer(player, nonGlobal): by default only that player sees the entity turn to it
        private int FacePlayer(LuaState l)
        {
            if (l.ToObject(2) is not CharData c)
            {
                log_("lua: non-player entity used as param in CLuaBaseEntity::facePlayer call");
                return 0;
            }
            bool onePersonOnly = l.Type(3) != 1 || l.ToBoolean(3);
            if (Entity(l) is BattleEntity b)
            {
                byte angle = WorldAngle(b.X, b.Z, b.Rotation, c.X, c.Z);
                if (!onePersonOnly)
                {
                    b.Rotation = angle;
                    b.UpdateMask |= b is CharData ? CharStats.UpdatePos : EntityUpdatePacket.UpdatePos;
                    return 0;
                }
                // Update 1 player's client only, then turn it back
                if (SessionOf(c) is not { } bs) return 0;
                byte storedRotation = b.Rotation;
                b.Rotation = angle;
                if (b is CharData other) UpdateCharPacket(bs, other, EntityUpdate.Update, CharStats.UpdatePos);
                else if (b is MobEntity mob) UpdateEntityPacket(bs, mob, EntityUpdate.Update, EntityUpdatePacket.UpdatePos);
                b.Rotation = storedRotation;
                return 0;
            }
            if (Entity(l) is not NpcEntity npc) return 0;
            byte facing = WorldAngle(npc.X, npc.Z, npc.Rotation, c.X, c.Z);
            if (!onePersonOnly)
            {
                npc.Rotation = facing;
                BroadcastNpcUpdate(npc, EntityUpdatePacket.UpdatePos);
                return 0;
            }
            var s = SessionOf(c);
            if (s == null) return 0;
            byte stored = npc.Rotation;
            npc.Rotation = facing;
            UpdateEntityPacket(s, npc, EntityUpdate.Update, EntityUpdatePacket.UpdatePos);
            npc.Rotation = stored;
            return 0;
        }

        // the time_server Vana'diel hour: each zone's onGameHour, the spawn handlers, each char's hour latents
        // (ZoneHost.Latents.cs), each char with a guild shop open hears it; then the day's and the time of day's zone
        // hooks (ZoneHost.TimeServer.cs) and the fishing pools' restock
        // the hour the time server last saw, set to the current one, so that the next tick crossing an hour runs the hour
        // hooks. A server's first pass after its start only records the hour (lastVanaHour_ < 0); the golden harness
        // primes it at world setup (quest rework P9), so that a flow's first tick is not that pass
        public void PrimeGameHour() => lastVanaHour_ = VanaTicksNow() / VanaTime.HOUR;

        private void TickGameHour()
        {
            long hour = VanaTicksNow() / VanaTime.HOUR;
            if (lastVanaHour_ < 0 || hour == lastVanaHour_)
            {
                lastVanaHour_ = hour;
                return;
            }
            bool totdChanged = VanaTime.GetTotd(lastVanaHour_ * VanaTime.HOUR) != VanaTime.GetTotd(hour * VanaTime.HOUR);
            lastVanaHour_ = hour;
            // each zone's onGameHour, then its spawn handler's
            ZonesOnGameHour();
            MobsOnGameHour((uint)(hour % 24));
            foreach (var c in new List<CharData>(charSessions_.Keys))
            {
                LatentsOnGameHour(c, hour % 24 == 0, totdChanged);
                if (c.GuildShopNpc == 0) continue;
                if (GuildShopNpc(c) is NpcEntity npc) CallGuildShops<object>("onGameHour", c, npc, l => null);
            }
            if (hour % 24 == 0) ZonesOnGameDay();
            // the time of day changed: zoneutils::TOTDChange, then fishingutils::RestockFishingAreas
            if (totdChanged)
            {
                ZonesOnTotdChange((int)VanaTime.GetTotd(hour * VanaTime.HOUR));
                RestockFishingAreas();
            }
        }

        // ---- S2C ---------------------------------------------------------------------------------------------------

        // GP_SERV_COMMAND_GUILD_OPEN: open and close are hours open[close) as bits of Time, a holiday is its weekday;
        // bit 31 keeps the client quiet about a close it did not see the shop open for
        private static BasicPacket GuildOpenPacket(byte status, byte open, byte close, byte holiday, bool passive)
        {
            var w = new PacketWriter(0x086, 0x0C);
            w.U8(4, status);
            uint time = 0;
            if (status == GuildHoliday) time = holiday;
            else
            {
                int length = close - open;
                if (length > 0) time = (uint)(((1UL << length) - 1) << open);
                if (passive) time |= 0x80000000;
            }
            w.U32(8, time);
            return w.Packet;
        }

        // GP_SERV_COMMAND_GUILD_BUY (0x082) / GUILD_SELL (0x084): the stock left and the quantity traded, or a negative
        // Trade code for a refusal
        private static BasicPacket GuildTrade(ushort opcode, byte count, ushort itemNo, sbyte trade)
        {
            var w = new PacketWriter(opcode, 0x08);
            w.U16(4, itemNo);
            w.U8(6, count);
            w.U8(7, (byte)trade);
            return w.Packet;
        }

        // GP_SERV_COMMAND_GUILD_BUYLIST (0x083) / GUILD_SELLLIST (0x085): 30 items a packet; the first of several is
        // Stat 0x40, the ones after it count up, and the last adds 0x80
        private static List<BasicPacket> GuildList(ushort opcode, List<GuildItem> items)
        {
            var packets = new List<BasicPacket>();
            var w = new PacketWriter(opcode, GuildListSize);
            int n = 0, packetCount = 0;
            foreach (var item in items)
            {
                if (n == GuildListMax)
                {
                    w.U8(4 + 240, n);
                    w.U8(4 + 241, packetCount == 0 ? 0x40 : packetCount);
                    packets.Add(w.Packet);
                    w = new PacketWriter(opcode, GuildListSize);
                    n = 0;
                    packetCount++;
                }
                int at = 4 + n * 8;
                w.U16(at, item.ItemNo);
                w.U8(at + 2, item.Count);
                w.U8(at + 3, item.Max);
                w.U32(at + 4, unchecked((uint)item.Price));
                n++;
            }
            w.U8(4 + 240, n);
            w.U8(4 + 241, (byte)(packetCount + 0x80));
            packets.Add(w.Packet);
            return packets;
        }

        // ---- C2S ---------------------------------------------------------------------------------------------------

        private static PacketValidator ValidateGuildList(CharData c) => Pv(c)
            .BlockedBy(BlockedState.InEvent)
            .MustNotEqual(c.GuildShopNpc, 0, "Character does not have a guild shop");

        private static PacketValidator ValidateGuildBuy(CharData c, BasicPacket p) => ValidateGuildList(c)
            .Range("ItemNum", p.Buffer[7], 1, 99)
            .MustEqual(p.Buffer[6], 0, "PropertyItemIndex not 0");

        private static PacketValidator ValidateGuildSell(CharData c, BasicPacket p) => Pv(c)
            .BlockedBy(BlockedState.InEvent | BlockedState.Crafting)
            .MustNotEqual(c.GuildShopNpc, 0, "Character does not have a guild shop")
            .Range("ItemNum", p.Buffer[7], 1, 99);

        // GP_CLI_COMMAND_GUILD_BUY: ItemNum (at most a stack) of ItemNo, which the script sells or refuses
        private void OnGuildBuy(MapSession s, CharData c, BasicPacket p)
        {
            ushort itemNo = U16(p, 4);
            byte quantity = p.Buffer[7];
            var item = LookupItem(itemNo);
            if (item == null)
            {
                log_($"User '{c.Name}' attempting to buy an invalid item from guild vendor!");
                return;
            }
            // retail turns more than a stack away instead of quietly clamping it
            if (quantity > item.StackSize)
            {
                Push(s, GuildTrade(0x082, 0, 0, GuildBuyRejected));
                return;
            }
            if (GuildShopNpc(c) is not NpcEntity npc) return;

            uint gilBefore = Gil(c)?.Quantity ?? 0;
            var result = CallGuildShops("onPlayerBuy", c, npc, l => new[]
            {
                FieldInt(l, l.Top, "itemNo", 0), FieldInt(l, l.Top, "count", 0), FieldInt(l, l.Top, "tradeCode", 0),
            }, itemNo, quantity);
            if (result == null) return;

            ushort boughtNo = (ushort)result[0];
            int tradeCode = (int)result[2];
            Push(s, GuildTrade(0x082, (byte)result[1], boughtNo, (sbyte)tradeCode));
            int appliedGil = (int)(Gil(c)?.Quantity ?? 0) - (int)gilBefore;
            if (tradeCode > 0 && appliedGil < 0)
            {
                // the prices roll per shop day in the script, so the unit price comes from what was paid
                byte bought = (byte)tradeCode;
                uint total = (uint)-appliedGil;
                AuditVendor(c, c.GuildShopNpc, npc.Name, "buy", boughtNo, bought, bought > 0 ? total / bought : 0, total, appliedGil);
            }
        }

        private void OnGuildBuyList(MapSession s, CharData c, BasicPacket p)
        {
            if (GuildShopNpc(c) is not NpcEntity npc) return;
            foreach (var packet in GuildList(0x083, GuildItems("onBuyList", c, npc))) Push(s, packet);
        }

        private void OnGuildSellList(MapSession s, CharData c, BasicPacket p)
        {
            if (GuildShopNpc(c) is not NpcEntity npc) return;
            foreach (var packet in GuildList(0x085, GuildItems("onSellList", c, npc))) Push(s, packet);
        }

        // GP_CLI_COMMAND_GUILD_SELL: the stacks are claimed before the script quotes, so it only ever buys what the char
        // still has; a sale can span several stacks, the one the client pointed at first
        private void OnGuildSell(MapSession s, CharData c, BasicPacket p)
        {
            ushort itemNo = U16(p, 4);
            byte hintSlot = p.Buffer[6];
            byte quantity = p.Buffer[7];
            var item = LookupItem(itemNo);
            if (item == null)
            {
                log_($"User '{c.Name}' attempting to sell an invalid item to guild vendor!");
                return;
            }
            // a guild shop never buys more than a single stack of an item per transaction
            if (quantity > item.StackSize || GuildShopNpc(c) is not NpcEntity npc)
            {
                Push(s, GuildTrade(0x084, 0, 0, GuildSellRejected));
                return;
            }

            using var transaction = new ItemTransaction(this, c);
            var lots = ClaimGuildSale(c, transaction, itemNo, hintSlot, quantity, out byte claimed);
            if (claimed == 0)
            {
                Push(s, GuildTrade(0x084, 0, 0, GuildSellRejected));
                return;
            }

            uint gilBefore = Gil(c)?.Quantity ?? 0;
            var result = CallGuildShops("onPlayerSell", c, npc, l => new[]
            {
                FieldInt(l, l.Top, "itemNo", 0), FieldInt(l, l.Top, "count", 0), FieldInt(l, l.Top, "sold", 0),
                FieldInt(l, l.Top, "price", 0), FieldInt(l, l.Top, "tradeCode", GuildSellRejected),
            }, itemNo, claimed);
            if (result == null)
            {
                Push(s, GuildTrade(0x084, 0, 0, GuildSellRejected));
                return;
            }

            ushort soldNo = (ushort)result[0];
            byte count = (byte)result[1], sold = (byte)result[2];
            uint price = (uint)result[3];
            // less sold than asked for is a partial fill, refusals keep the script's own code
            int tradeCode = sold;
            if (sold == 0) tradeCode = (int)result[4];
            else if (sold < quantity) tradeCode = -1;

            if (sold > 0)
            {
                if (!CommitGuildSale(c, transaction, lots, claimed, sold, price))
                {
                    Push(s, GuildTrade(0x084, 0, 0, GuildSellRejected));
                    return;
                }
                int appliedGil = (int)(Gil(c)?.Quantity ?? 0) - (int)gilBefore;
                AuditVendor(c, c.GuildShopNpc, npc.Name, "sell", soldNo, sold, price, unchecked(price * sold), appliedGil);
            }
            Push(s, GuildTrade(0x084, count, soldNo, (sbyte)tradeCode));
        }

        // GuildSellTransaction::start: every free stack of the item, the hinted slot first, up to the quantity
        private static List<(byte Slot, byte Quantity)> ClaimGuildSale(CharData c, ItemTransaction transaction, ushort itemNo, byte hintSlot, byte quantity, out byte claimed)
        {
            var slots = new List<byte>();
            foreach (var item in c.Items)
                if (item.Location == Container.Inventory && item.ItemId == itemNo) slots.Add(item.Slot);
            slots.Sort();
            if (slots.Remove(hintSlot)) slots.Insert(0, hintSlot);

            var lots = new List<(byte, byte)>();
            claimed = 0;
            foreach (byte slot in slots)
            {
                if (claimed >= quantity) break;
                var item = StorageItem(c, Container.Inventory, slot);
                if (item == null || item.Quantity == 0 || transaction.ClaimSlot(Container.Inventory, slot) == null) continue;
                byte take = (byte)Math.Min(item.Quantity, (uint)(quantity - claimed));
                lots.Add((slot, take));
                claimed += take;
            }
            return lots;
        }

        // GuildSellTransaction::setPayout + commit: the goods leave the claimed stacks, then the gil comes in
        private bool CommitGuildSale(CharData c, ItemTransaction transaction, List<(byte Slot, byte Quantity)> lots, byte claimed, byte sold, uint unitPrice)
        {
            if (sold > claimed)
            {
                log_($"GuildSellTransaction::setPayout: shop bought {sold} but only {claimed} was claimed");
                sold = claimed;
            }
            byte remaining = sold;
            foreach (var (slot, lotQuantity) in lots)
            {
                if (remaining == 0) break;
                byte take = Math.Min(lotQuantity, remaining);
                remaining -= take;
                if (!transaction.Take(Container.Inventory, slot, take))
                {
                    log_($"GuildSellTransaction: {c.Name} kept {take} of the goods in slot {slot}");
                    return false;
                }
            }
            if (!transaction.Earn(unchecked(sold * unitPrice)))
            {
                log_($"GuildSellTransaction: {c.Name} was not paid for the sale");
                return false;
            }
            PushTo(c, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
            return transaction.Commit();
        }

        // zoneutils::GetEntity(guildShopNpc_, TYPE_NPC)
        private NpcEntity GuildShopNpc(CharData c)
        {
            uint id = c.GuildShopNpc;
            if (id == 0) return null;
            return HeldZone((ushort)(id >> 12 & 0xFFF))?.NpcById.TryGetValue(id, out var npc) == true ? npc : null;
        }

        // the { id, count, max, price } entries of onBuyList / onSellList
        private List<GuildItem> GuildItems(string fn, CharData c, NpcEntity npc) =>
            CallGuildShops(fn, c, npc, l =>
            {
                var items = new List<GuildItem>();
                int t = l.Top, n = l.ObjLen(t);
                for (int i = 1; i <= n; i++)
                {
                    l.RawGetI(t, i);
                    int e = l.Top;
                    if (l.IsTable(e))
                        items.Add(new GuildItem((ushort)FieldInt(l, e, "id", 0), (byte)FieldInt(l, e, "count", 0),
                                                (byte)FieldInt(l, e, "max", 0), (int)FieldInt(l, e, "price", 0)));
                    l.Pop(1);
                }
                return items;
            }) ?? new List<GuildItem>();

        // luautils::callGlobal<sol::table>("xi.guildShops.<fn>", player, npc, args...): read gets the returned table at
        // the top of the stack; null when the script fails or returns no table
        private T CallGuildShops<T>(string fn, CharData c, NpcEntity npc, Func<LuaState, T> read, params double[] args) where T : class
        {
            if (lua_ == null) return null;
            string path = "xi.guildShops." + fn;
            int top = lua_.Top;
            try
            {
                lua_.GetPath(path);
                if (lua_.IsNil(-1))
                {
                    log_($"luautils::callGlobalFunction: {path}: Function not found");
                    return null;
                }
                lua_.PushObject(c);
                lua_.PushObject(npc);
                foreach (double a in args) lua_.Push(a);
                lua_.Call(2 + args.Length, 1);
                return lua_.IsTable(-1) ? read(lua_) : null;
            }
            catch (LuaException e)
            {
                ScriptError(path, e);
                return null;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }
    }
}
