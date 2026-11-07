using System;
using System.Collections.Generic;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // CTreasurePool (treasure_pool.cpp), the C2S 0x041 TROPHY_ENTRY / 0x042 TROPHY_ABSENCE handlers, the pool's
    // assignment (CZone::IncreaseZoneCounter / DecreaseZoneCounter, CParty::ReloadTreasurePool and the party / alliance
    // changes) and the lua side (addTreasure, getTreasurePool, CTreasurePool). A kill's drops come from
    // ZoneHost.Loot.cs; the pool never holds gil (the kill hands it out, the C++'s Gold is a TODO too).
    public sealed partial class ZoneHost
    {
        // TreasurePoolType: the value is the member capacity
        internal enum TreasurePoolType : byte
        {
            Solo = 1,
            Party = 6,
            Alliance = 18,
            Zone = 128,
        }

        private const int TreasurePoolSize = 10;
        private const long TreasureChecktimeMs = 3000, TreasureLivetimeMs = 5 * 60 * 1000;
        private const uint ItemFlagNoRareCheck = 0x00020000;
        private const int RoeEventLootItem = 3;
        private const string TreasurePoolLuaType = "CTreasurePool";

        // GP_TROPHY_SOLUTION_STATE
        private const byte TrophyWin = 0x01, TrophyWinError = 0x02;

        // a CTreasurePool of the zone flagged "treasure" (zone misc Treasure), shared by every char in it
        private readonly Dictionary<ushort, TreasurePool> zoneTreasurePools_ = new Dictionary<ushort, TreasurePool>();

        // timer::start_time: what 0x0D2's StartTime counts from
        private long? treasureStartMs_;
        private long TreasureStartMs => treasureStartMs_ ??= NowMs();

        internal sealed class TreasurePool
        {
            internal sealed class LotInfo
            {
                public CharData Member;
                public ushort Lot;
            }

            internal sealed class PoolItem
            {
                public ushort Id;
                public byte SlotId;
                public long? TimeStamp;
                public readonly List<LotInfo> Lotters = new List<LotInfo>();
            }

            private readonly ZoneHost h_;
            private readonly PoolItem[] items_ = new PoolItem[TreasurePoolSize];
            private byte count_;
            private long tick_;

            public TreasurePoolType Type;
            public readonly List<CharData> Members = new List<CharData>();

            public TreasurePool(ZoneHost h, TreasurePoolType type)
            {
                h_ = h;
                Type = type;
                for (byte i = 0; i < TreasurePoolSize; i++) items_[i] = new PoolItem { SlotId = i };
            }

            public IReadOnlyList<PoolItem> Items => items_;
            public byte ItemCount => count_;

            public void AddMember(CharData c)
            {
                if (c == null || c.TreasurePool != this)
                {
                    h_.log_("CTreasurePool::AddMember() - PChar was null, or PTreasurePool mismatched.");
                    return;
                }
                if (Members.Contains(c))
                {
                    h_.log_("CTreasurePool::AddMember() - PChar was already in the members list!");
                    return;
                }

                Members.Add(c);

                if (Type == TreasurePoolType.Solo && c.Party != null) Type = TreasurePoolType.Party;
                else if (Type == TreasurePoolType.Party && c.Party?.Alliance != null) Type = TreasurePoolType.Alliance;

                UpdatePool(c);
            }

            // lots on every item are forgotten (the zone pool's too, as the C++); an emptied pool other than a zone's is
            // dropped
            public void DelMember(CharData c)
            {
                if (c == null || c.TreasurePool != this)
                {
                    h_.log_("CTreasurePool::DelMember() - PChar was null, or PTreasurePool mismatched.");
                    return;
                }

                foreach (var item in items_) item.Lotters.RemoveAll(info => info.Member.Id == c.Id);

                if (Members.Remove(c)) c.TreasurePool = null;

                if ((Type == TreasurePoolType.Party || Type == TreasurePoolType.Alliance) && Members.Count == 1) Type = TreasurePoolType.Solo;
            }

            // the item count after: unchanged for an unknown item, or a rare one every member has (unless the item skips
            // that check in a shared pool). A full pool first decides its oldest plain item (then its oldest non-Ex one,
            // then its oldest), whose slot the new item takes.
            public byte AddItem(ushort itemId, object entity)
            {
                var basic = h_.ItemBasic(itemId);
                if (basic == null) return count_;
                uint flags = basic.Value.Flags;

                bool skipRareCheck = Type != TreasurePoolType.Solo && (flags & ItemFlagNoRareCheck) != 0;
                if ((flags & ItemFlagRare) != 0 && !skipRareCheck && Members.TrueForAll(m => HasItem(m, itemId))) return count_;

                int freeSlot = Array.FindIndex(items_, i => i.Id == 0);
                bool full = freeSlot < 0;
                if (full)
                {
                    freeSlot = Oldest(i => h_.ItemBasic(i.Id) is var b && b != null && (b.Value.Flags & (ItemFlagRare | ItemFlagExclusive)) == 0);
                    if (freeSlot < 0) freeSlot = Oldest(i => h_.ItemBasic(i.Id) is var b && b != null && (b.Value.Flags & ItemFlagExclusive) == 0);
                    if (freeSlot < 0) freeSlot = Oldest(_ => true);
                    if (freeSlot < 0) freeSlot = 0;

                    items_[freeSlot].TimeStamp = null;
                    CheckTreasureItem(h_.NowMs(), freeSlot);
                }

                count_++;
                var slot = items_[freeSlot];
                slot.Id = itemId;
                slot.TimeStamp = h_.NowMs() - TreasureChecktimeMs;

                foreach (var m in Members.ToArray())
                {
                    h_.RoeEvent(RoeEventLootItem, m, new (string, object)[] { ("itemid", (uint)slot.Id) });
                    h_.PushTo(m, h_.TrophyListPacket(slot, entity, false));
                }

                if (Members.Count == 1) CheckTreasureItem(h_.NowMs(), freeSlot);

                return count_;
            }

            // the slot whose timestamp is the oldest among those matching (std::optional orders an empty one first)
            private int Oldest(Predicate<PoolItem> match)
            {
                int found = -1;
                long? oldest = long.MaxValue;
                foreach (var item in items_)
                {
                    if (!match(item)) continue;
                    if (item.TimeStamp == null ? oldest != null : oldest != null && item.TimeStamp < oldest)
                    {
                        found = item.SlotId;
                        oldest = item.TimeStamp;
                    }
                }
                return found;
            }

            // the 10 slots as old items, unless the char is disappearing
            public void UpdatePool(CharData c)
            {
                if (c == null || c.TreasurePool != this)
                {
                    h_.log_("CTreasurePool::UpdatePool() - PChar was null, or PTreasurePool mismatched.");
                    return;
                }
                if (h_.CharDisappeared(c)) return;
                foreach (var item in items_) h_.PushTo(c, h_.TrophyListPacket(item, null, true));
            }

            // everything decided now, as though the items had been in the pool past their time
            public void Flush()
            {
                if (count_ == 0) return;
                long tick = h_.NowMs() + TreasureChecktimeMs + 1000;
                for (int i = 0; i < TreasurePoolSize; i++) CheckTreasureItem(tick, i);
            }

            // a lot is refused for an empty slot (logged), a full inventory or a rare item already owned; once every
            // member has lotted or passed the item is decided
            public void LotItem(CharData c, byte slotId, ushort lot)
            {
                if (c == null || c.TreasurePool != this)
                {
                    h_.log_("CTreasurePool::LotItem() - PChar was null, or PTreasurePool mismatched.");
                    return;
                }
                if (slotId >= TreasurePoolSize) return;

                var item = items_[slotId];
                var basic = h_.ItemBasic(item.Id);
                if (basic == null)
                {
                    h_.log_($"Player {c.Name} is trying to lot on an item that doesn't exist (PItem was nullptr) (Packet injection?)!");
                    return;
                }
                if (FreeSlots(c, Container.Inventory) == 0) return;
                if ((basic.Value.Flags & ItemFlagRare) != 0 && HasItem(c, item.Id)) return;

                item.Lotters.Add(new LotInfo { Member = c, Lot = lot });

                var (highestLotter, highestLot) = Highest(item);
                foreach (var m in Members) h_.PushTo(m, TrophySolution(highestLotter, highestLot, c, slotId, lot));

                if (item.Lotters.Count == Members.Count) CheckTreasureItem(tick_, slotId);
            }

            // a pass is a lot of 0 (an earlier lot becomes one); the others see 0xFFFF
            public void PassItem(CharData c, byte slotId)
            {
                if (c == null || c.TreasurePool != this)
                {
                    h_.log_("CTreasurePool::PassItem() - PChar was null, or PTreasurePool mismatched.");
                    return;
                }
                if (slotId >= TreasurePoolSize) return;

                var item = items_[slotId];
                var lotted = item.Lotters.Find(info => info.Member.Id == c.Id);
                if (lotted != null) lotted.Lot = 0;
                else item.Lotters.Add(new LotInfo { Member = c, Lot = 0 });

                var (highestLotter, highestLot) = Highest(item);
                const ushort passedLot = 65535;
                foreach (var m in Members) h_.PushTo(m, TrophySolution(highestLotter, highestLot, c, slotId, passedLot));

                if (item.Lotters.Count == Members.Count) CheckTreasureItem(tick_, slotId);
            }

            // the first of the highest lots
            private static (CharData Member, ushort Lot) Highest(PoolItem item)
            {
                CharData member = null;
                ushort lot = 0;
                foreach (var info in item.Lotters)
                {
                    if (info.Lot <= lot) continue;
                    member = info.Member;
                    lot = info.Lot;
                }
                return (member, lot);
            }

            public bool HasLottedItem(CharData c, byte slotId) =>
                slotId < TreasurePoolSize && items_[slotId].Lotters.Exists(info => info.Member.Id == c.Id);

            public bool HasPassedItem(CharData c, byte slotId) =>
                slotId < TreasurePoolSize && items_[slotId].Lotters.Find(info => info.Member.Id == c.Id) is LotInfo info && info.Lot == 0;

            // every 3 s while the pool holds something
            public void CheckItems(long tick)
            {
                if (count_ == 0 || tick - tick_ <= TreasureChecktimeMs) return;
                for (int i = 0; i < TreasurePoolSize; i++) CheckTreasureItem(tick, i);
                tick_ = tick;
            }

            // decided when its time is up (or it was pushed out), a lone member has room, or every member lotted or
            // passed: the highest lotter wins it into the inventory (lost when theirs is full), else a random member
            // with room who neither passed nor owns it already when rare
            private void CheckTreasureItem(long tick, int slotId)
            {
                var item = items_[slotId];
                if (item.Id == 0) return;

                if (!(item.TimeStamp == null || tick - item.TimeStamp.Value > TreasureLivetimeMs ||
                      (Members.Count == 1 && FreeSlots(Members[0], Container.Inventory) != 0) ||
                      item.Lotters.Count == Members.Count))
                    return;

                var highest = new LotInfo();
                foreach (var info in item.Lotters)
                    if (info.Lot > highest.Lot) highest = info;

                if (highest.Member != null && highest.Lot != 0)
                {
                    if (FreeSlots(highest.Member, Container.Inventory) != 0) Give(highest.Member, slotId);
                    else TreasureLost(slotId);
                    return;
                }

                var candidates = new List<CharData>();
                foreach (var m in Members)
                {
                    if (HasItem(m, item.Id) && ((h_.ItemBasic(item.Id)?.Flags ?? 0) & ItemFlagRare) != 0) continue;
                    if (FreeSlots(m, Container.Inventory) != 0 && !HasPassedItem(m, (byte)slotId)) candidates.Add(m);
                }

                if (candidates.Count == 0) TreasureLost(slotId);
                else Give(candidates[h_.RandomBelow(candidates.Count)], slotId);
            }

            // ItemClaimTransaction give (silent), then treasureWon, or treasureError when it could not land
            private void Give(CharData winner, int slotId)
            {
                bool given;
                using (var transaction = new ItemTransaction(h_, winner))
                    given = transaction.Give(Container.Inventory, items_[slotId].Id, 1, silent: true).HasValue && transaction.Commit();
                if (given) TreasureWon(winner, slotId);
                else TreasureError(winner, slotId);
            }

            private void TreasureWon(CharData winner, int slotId) => Decide(winner, slotId, 0, TrophyWin);

            private void TreasureError(CharData winner, int slotId) => Decide(winner, slotId, 0xFFFF, TrophyWinError);

            // treasureWon / treasureError: the winner's slot result to everyone, then the slot empties
            private void Decide(CharData winner, int slotId, ushort lot, byte state)
            {
                var item = items_[slotId];
                if (winner == null || winner.TreasurePool != this || item.Id == 0)
                {
                    // the C++ logs TreasureError for both
                    h_.log_("CTreasurePool::TreasureError() - Winner, or Winner Treasure Pool mismatch, or Pool ID = 0.");
                    return;
                }

                item.TimeStamp = null;
                foreach (var m in Members) h_.PushTo(m, TrophySolution(winner, (byte)slotId, lot, state));
                Clear(item);
            }

            // nobody gets it: the C++ sends WinError (not Lost)
            private void TreasureLost(int slotId)
            {
                var item = items_[slotId];
                if (item.Id == 0)
                {
                    h_.log_($"Pool Items for SlotID ({slotId}) was 0.");
                    return;
                }

                item.TimeStamp = null;
                foreach (var m in Members) h_.PushTo(m, TrophySolution((byte)slotId, TrophyWinError));
                Clear(item);
            }

            private void Clear(PoolItem item)
            {
                count_--;
                item.Id = 0;
                item.Lotters.Clear();
            }
        }

        // CBaseEntity::status == Disappear for a char: not yet (or no longer) in the zone, or zoning out
        private bool CharDisappeared(CharData c) => SessionOf(c) is not MapSession s || !s.InZone || zoneChanges_.ContainsKey(s);

        // ---- packets ------------------------------------------------------------------------------------------------

        // GP_SERV_COMMAND_TROPHY_LIST (0x0D2): one slot; the dropper (a mob, an NPC or a char) when known
        private BasicPacket TrophyListPacket(TreasurePool.PoolItem item, object entity, bool isOldItem)
        {
            var w = new PacketWriter(0x0D2, 60);
            w.U32(4, 1);                        // TrophyItemNum
            w.U16(16, item.Id);                 // TrophyItemNo
            w.U8(20, item.SlotId);              // TrophyItemIndex
            w.U8(21, isOldItem ? 1 : 0);        // Entry
            if (item.TimeStamp is long ts) w.U32(24, (uint)(ts - TreasureStartMs)); // StartTime
            switch (entity)
            {
                case CharData c:
                    w.U32(8, c.Id);
                    w.U16(18, c.TargId);
                    break;
                case NpcEntity n:
                    w.U32(8, n.Id);
                    w.U16(18, n.TargId);
                    w.U8(22, 1); // IsContainer
                    break;
                case MobEntity mob:
                    w.U32(8, mob.Id);
                    w.U16(18, mob.TargId);
                    break;
            }
            return w.Packet;
        }

        private static void TrophyName(PacketWriter w, int offset, string name)
        {
            var bytes = System.Text.Encoding.Latin1.GetBytes(name);
            w.Bytes(offset, bytes.AsSpan(0, Math.Min(bytes.Length, 16)));
        }

        // GP_SERV_COMMAND_TROPHY_SOLUTION (0x0D3) (slot, state): just the slot's result
        private static BasicPacket TrophySolution(byte slotId, byte state)
        {
            var w = new PacketWriter(0x0D3, 60);
            w.U8(20, slotId);
            w.U8(21, state);
            return w.Packet;
        }

        // (winner, slot, lot, state): who won the slot
        private static BasicPacket TrophySolution(CharData winner, byte slotId, ushort lot, byte state)
        {
            var w = new PacketWriter(0x0D3, 60);
            w.U32(4, winner.Id);    // LootUniqueNo
            w.U16(12, winner.TargId);
            w.U16(14, lot);         // LootPoint
            w.U8(20, slotId);
            w.U8(21, state);
            TrophyName(w, 22, winner.Name);
            return w.Packet;
        }

        // (highest lotter, highest lot, lotter, slot, lot): a lot or a pass, with the highest lot so far
        private static BasicPacket TrophySolution(CharData highest, ushort highestLot, CharData lotter, byte slotId, ushort lot)
        {
            var w = new PacketWriter(0x0D3, 60);
            if (highest != null)
            {
                w.U32(4, highest.Id);
                w.U16(12, highest.TargId);
                w.U16(14, highestLot);
                TrophyName(w, 22, highest.Name);
            }
            w.U32(8, lotter.Id);                        // EntryUniqueNo
            w.U16(16, (ushort)(lotter.TargId & 0x7FFF)); // EntryActIndex (15 bits)
            w.U16(18, lot);                             // EntryPoint
            w.U8(20, slotId);
            TrophyName(w, 38, lotter.Name);             // sLootName2
            return w.Packet;
        }

        // ---- assignment ---------------------------------------------------------------------------------------------

        // CZone::IncreaseZoneCounter: the zone's own pool takes precedence, else the party's (ReloadTreasurePool), else
        // a pool of its own
        private void JoinTreasurePool(CharData c)
        {
            if (c.TreasurePool != null)
            {
                log_($"PTreasurePool was not empty for {c.Name}.");
                c.TreasurePool.DelMember(c);
            }

            if (AreaOf(c).CanUseMisc("treasure"))
            {
                if (!zoneTreasurePools_.TryGetValue(c.Zone, out var pool))
                    zoneTreasurePools_[c.Zone] = pool = new TreasurePool(this, TreasurePoolType.Zone);
                c.TreasurePool = pool;
                pool.AddMember(c);
            }
            else if (c.Party != null) c.Party.ReloadTreasurePool(c);
            else NewSoloTreasurePool(c);
        }

        // CZone::DecreaseZoneCounter (and ~CCharEntity): out of the pool; a zone pool left without chars starts over
        // empty, so its loot does not wait for the next visitor
        private void LeaveTreasurePool(CharData c)
        {
            c.TreasurePool?.DelMember(c);
            if (zoneTreasurePools_.TryGetValue(c.Zone, out var pool) && pool.Members.Count == 0)
                zoneTreasurePools_[c.Zone] = new TreasurePool(this, TreasurePoolType.Zone);
        }

        private void NewSoloTreasurePool(CharData c)
        {
            c.TreasurePool = new TreasurePool(this, TreasurePoolType.Solo);
            c.TreasurePool.AddMember(c);
        }

        // the party / alliance steps that leave a char on its own: out of a shared pool (not a zone's) into a new one,
        // its slots resent
        private void ResetTreasurePool(CharData c)
        {
            if (c.TreasurePool == null || c.TreasurePool.Type == TreasurePoolType.Zone) return;
            c.TreasurePool.DelMember(c);
            NewSoloTreasurePool(c);
            c.TreasurePool.UpdatePool(c);
        }

        // zone_entities tick: each char's pool decides what has waited long enough (every 3 s at most, per pool)
        private void TickTreasurePools()
        {
            long now = NowMs();
            foreach (var (c, s) in new List<KeyValuePair<CharData, MapSession>>(charSessions_))
                if (s.InZone && s.Char == c) c.TreasurePool?.CheckItems(now);
        }

        // ---- C2S ----------------------------------------------------------------------------------------------------

        // 0x041 TROPHY_ENTRY / 0x042 TROPHY_ABSENCE: TrophyItemIndex u8 @4
        private void RegisterTreasurePoolHandlers()
        {
            Register(0x041, (s, c, p) => ValidateTrophy(c, p), (s, c, p) =>
            {
                byte slot = p.Buffer[4];
                // 1 ~ 998+1
                if (!c.TreasurePool.HasLottedItem(c, slot)) c.TreasurePool.LotItem(c, slot, (ushort)(1 + RandomBelow(999)));
            });
            Register(0x042, (s, c, p) => ValidateTrophy(c, p), (s, c, p) =>
            {
                byte slot = p.Buffer[4];
                if (!c.TreasurePool.HasPassedItem(c, slot)) c.TreasurePool.PassItem(c, slot);
            });
        }

        private PacketValidator ValidateTrophy(CharData c, BasicPacket p) =>
            Pv(c).BlockedBy(BlockedState.InEvent)
                 .MustEqual(c.TreasurePool != null ? 1 : 0, 1, "Character does not have a treasure pool")
                 .Range("TrophyItemIndex", p.Buffer[4], 0, TreasurePoolSize - 1);

        // ---- lua ----------------------------------------------------------------------------------------------------

        // charutils::DistributeItem: dropRate in 1/1000 (a uint16 times 10, wrapping as the C++), through a mob's
        // treasure hunter, times map.DROP_RATE_MULTIPLIER
        private void DistributeItem(CharData c, object entity, ushort itemId, ushort dropRate)
        {
            ushort thDropRate = (ushort)(dropRate * 10);
            if (entity is MobEntity mob) thDropRate = ThDropRate(mob.ThLvl, thDropRate);
            float multiplier = loader_.Stats?.Settings.DropRateMultiplier ?? 1f;
            if (thDropRate > 0 && 1 + RandomBelow(10000) <= thDropRate * multiplier) c.TreasurePool.AddItem(itemId, entity);
        }

        private void RegisterTreasurePoolBindings(Dictionary<string, LuaFunction> m)
        {
            // player:addTreasure(itemId, [dropper], [dropRate = 1000])
            m["addTreasure"] = l =>
            {
                if (Pc(l, "addTreasure") is not CharData c) return 0;
                ushort itemId = (ushort)l.ToUIntArg(2);
                if (itemId == 0 || c.TreasurePool == null) return 0;
                var entity = l.ToObject(3);
                if (entity is CharData || entity is NpcEntity || entity is MobEntity)
                    DistributeItem(c, entity, itemId, l.IsNil(4) ? (ushort)1000 : (ushort)l.ToUIntArg(4));
                else DistributeItem(c, null, itemId, l.IsNil(3) ? (ushort)1000 : (ushort)l.ToUIntArg(3));
                return 0;
            };
            m["getTreasurePool"] = l =>
            {
                l.PushObject(Entity(l) is CharData c ? c.TreasurePool : null, TreasurePoolLuaType);
                return 1;
            };

            TreasurePool Pool(LuaState l) =>
                l.ToObject(1) as TreasurePool ?? throw new InvalidOperationException("bad self: expected " + TreasurePoolLuaType);

            lua_.RegisterType(TreasurePoolLuaType, new Dictionary<string, LuaFunction>
            {
                ["getType"] = l => Ret(l, (double)(byte)Pool(l).Type),
                ["flush"] = l =>
                {
                    Pool(l).Flush();
                    return 0;
                },
                ["addMember"] = l =>
                {
                    if (l.ToObject(2) is CharData c) Pool(l).AddMember(c);
                    return 0;
                },
                ["delMember"] = l =>
                {
                    if (l.ToObject(2) is CharData c) Pool(l).DelMember(c);
                    return 0;
                },
                ["updatePool"] = l =>
                {
                    if (l.ToObject(2) is CharData c) Pool(l).UpdatePool(c);
                    return 0;
                },
                ["addItem"] = l => Ret(l, (double)Pool(l).AddItem((ushort)l.ToUIntArg(2), l.ToObject(3))),
                ["memberCount"] = l => Ret(l, (double)Pool(l).Members.Count),
                ["getMembers"] = l =>
                {
                    var members = Pool(l).Members;
                    l.NewTable(members.Count);
                    for (int i = 0; i < members.Count; i++)
                    {
                        l.PushObject(members[i]);
                        l.RawSetI(-2, i + 1);
                    }
                    return 1;
                },
                // rows of { id, slotId, timestamp (unix, 0 when none), lotters = { { member, lot } } }
                ["getItems"] = l =>
                {
                    var items = Pool(l).Items;
                    long nowMs = NowMs(), unixNow = UnixNow();
                    l.NewTable(items.Count);
                    for (int i = 0; i < items.Count; i++)
                    {
                        var item = items[i];
                        l.NewTable(0, 4);
                        l.Push((double)item.Id);
                        l.SetField(-2, "id");
                        l.Push((double)item.SlotId);
                        l.SetField(-2, "slotId");
                        l.Push(item.TimeStamp is long ts ? (double)(uint)(unixNow - (nowMs - ts) / 1000) : 0.0);
                        l.SetField(-2, "timestamp");
                        l.NewTable(item.Lotters.Count);
                        for (int j = 0; j < item.Lotters.Count; j++)
                        {
                            l.NewTable(0, 2);
                            l.PushObject(item.Lotters[j].Member);
                            l.SetField(-2, "member");
                            l.Push((double)item.Lotters[j].Lot);
                            l.SetField(-2, "lot");
                            l.RawSetI(-2, j + 1);
                        }
                        l.SetField(-2, "lotters");
                        l.RawSetI(-2, i + 1);
                    }
                    return 1;
                },
            });
        }
    }
}
