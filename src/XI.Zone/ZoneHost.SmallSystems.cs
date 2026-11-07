using System;
using System.Collections.Generic;
using System.Linq;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // Small systems behind a handful of CLuaBaseEntity bindings: the crafting guilds' daily guild point items (CGuild,
    // guildutils), the porter moogle's storage slips, traverser stones, Records of Eminence claimed deeds, the maze
    // vouchers and runes, the fishing contest's per-char getters, steal / despoil and the Abyssea chests' restores.
    public sealed partial class ZoneHost
    {
        private const int GpItemRanks = 7;                 // GP_ITEM_RANKS: guild ranks 3 (Novice) .. 9 (Craftsman)
        private const ushort MsgTargetRegainsHp = 587, MsgTargetRegainsMp = 588;
        private const ushort AnimRegainHp = 772, AnimRegainMp = 773;
        private const byte StoreWithPorterMoogleFailed = 3; // an unknown slip leaves the gear alone and plays no cutscene

        // ---- guild points ------------------------------------------------------------------------------------------

        // CGuild: the guild_item_points rows of today's pattern, one list a rank
        private sealed class Guild
        {
            public byte Id;
            public string PointsName;
            public readonly byte[] GpItemsRank = new byte[GpItemRanks];
            public readonly List<(ushort ItemId, ushort MaxPoints, ushort Points)>[] GpItems =
                Enumerable.Range(0, GpItemRanks).Select(_ => new List<(ushort, ushort, ushort)>()).ToArray();
        }

        private List<Guild> guilds_;
        private long guildPatternDay_ = -1;

        // the JST day of the year, 0-based (earth_time::jst::get_yearday)
        private static int JstYearDay(long unix) => DateTimeOffset.FromUnixTimeSeconds(unix + JstOffset).DayOfYear - 1;

        // guildutils::Initialize, then UpdateGuildPointsPattern again at each JST midnight (the time server's daily
        // tick), here on the first use after it
        private List<Guild> Guilds()
        {
            long now = UnixNow();
            long today = FloorDiv(now + JstOffset, Day);
            if (guilds_ == null)
            {
                guilds_ = new List<Guild>();
                // CGuild(): the ranks start from the days since the Vana'diel epoch, a day back
                long elapsedDays = FloorDiv(LoginPackets.VanadielTimestamp(now) - Day, Day);
                var r = Exec("SELECT DISTINCT id, points_name FROM guilds ORDER BY id ASC");
                while (r != null && r.Next())
                {
                    var g = new Guild { Id = r.Get<byte>("id"), PointsName = r.Get<string>("points_name") };
                    for (int i = 0; i < GpItemRanks; i++) g.GpItemsRank[i] = (byte)(elapsedDays % (i + 4));
                    guilds_.Add(g);
                }
            }
            if (guildPatternDay_ != today)
            {
                guildPatternDay_ = today;
                UpdateGuildPointsPattern(now);
            }
            return guilds_;
        }

        // serverutils::GetServerVar: the row (an expired one deleted, 0), which also refreshes the volatile cache
        private int ServerVar(string name)
        {
            var r = Exec("SELECT value, expiry FROM server_variables WHERE name = ? LIMIT 1", BoundValue.Of(name));
            int value = 0;
            uint expiry = 0;
            if (r != null && r.Next())
            {
                value = r.Get<int>("value");
                expiry = r.Get<uint>("expiry");
                if (expiry > 0 && expiry <= UnixNow())
                {
                    value = 0;
                    Exec("DELETE FROM server_variables WHERE name = ? LIMIT 1", BoundValue.Of(name));
                }
            }
            serverVarCache_[name] = (value, expiry);
            return value;
        }

        private void SetServerVar(string name, int value) =>
            Exec("INSERT INTO server_variables VALUES (?, ?, 0) ON DUPLICATE KEY UPDATE value = ?, expiry = 0",
                 BoundValue.Of(name), BoundValue.Of(value), BoundValue.Of(value));

        // guildutils::UpdateGuildPointsPattern: a new random pattern once a JST day (kept in the server variables so
        // another process picks the same one), then each guild's ranks move on a step and their items reload
        private void UpdateGuildPointsPattern(long now)
        {
            int jstDayOfYear = JstYearDay(now);
            int pattern = MobRng.Next(8);
            if (ServerVar("[GUILD]pattern_update") != jstDayOfYear)
            {
                SetServerVar("[GUILD]pattern_update", jstDayOfYear);
                SetServerVar("[GUILD]pattern", pattern);
            }
            else pattern = ServerVar("[GUILD]pattern");

            foreach (var g in guilds_)
            {
                for (int i = 0; i < GpItemRanks; i++)
                {
                    g.GpItems[i].Clear();
                    g.GpItemsRank[i] = (byte)((g.GpItemsRank[i] + 1) % (i + 4));
                    var r = Exec("SELECT itemid, points, max_points FROM guild_item_points WHERE guildid = ? AND pattern = ? AND rank = ?",
                                 BoundValue.Of(g.Id), BoundValue.Of((byte)pattern), BoundValue.Of(g.GpItemsRank[i]));
                    while (r != null && r.Next())
                        g.GpItems[i].Add((r.Get<ushort>("itemid"), r.Get<ushort>("max_points"), r.Get<ushort>("points")));
                }
            }
            log_($"Guild point pattern update has finished. New pattern: {pattern}");
        }

        private Guild GuildById(int guildId)
        {
            var guilds = Guilds();
            if (guildId >= 0 && guildId < guilds.Count) return guilds[guildId];
            log_($"Guild with id <{guildId}> is not found on server");
            return null;
        }

        // the char's rank in the guild's craft, as a GP item list index
        private static int GuildRankIndex(CharData c, Guild g) => Math.Clamp((int)c.RealSkillRank[g.Id + Skill.Fishing], 3, 9) - 3;

        // CGuild::getDailyGPItem: today's item for the char's rank and the points it may still earn (none once the
        // day's cap was reached, which the daily var marks as 1)
        private (ushort ItemId, ushort Remaining) DailyGpItem(CharData c, Guild g)
        {
            var list = g.GpItems[GuildRankIndex(c, g)];
            if (list.Count == 0) return (0, 0);
            var item = list[0];
            int curPoints = (ushort)c.GetCharVar("[GUILD]daily_points", UnixNow());
            if (curPoints == 1) return (item.ItemId, 0);
            return (item.ItemId, (ushort)(item.MaxPoints - Math.Clamp(curPoints, 0, item.MaxPoints)));
        }

        // CGuild::addGuildPoints: as many of the traded items as the day's cap still takes, their points, and the daily
        // var to the next JST midnight
        private (byte Quantity, short Points) AddGuildPoints(CharData c, Guild g, Item item, uint tradedQuantity)
        {
            int curPoints = (ushort)CharVar(c, "[GUILD]daily_points");
            if (curPoints == 1 || item == null) return (0, 0);
            foreach (var gp in g.GpItems[GuildRankIndex(c, g)])
            {
                if (gp.ItemId != item.ItemId) continue;
                ushort quantity = (ushort)Math.Min((gp.MaxPoints - Math.Clamp(curPoints, 0, gp.MaxPoints)) / gp.Points + 1, tradedQuantity);
                int pointsToAdd = (ushort)(gp.Points * quantity);
                pointsToAdd = curPoints <= gp.MaxPoints ? Math.Clamp(pointsToAdd, 0, gp.MaxPoints - curPoints) : 0;
                AddPoints(c, g.PointsName, pointsToAdd);
                SetCharVar(c, "[GUILD]daily_points", curPoints + pointsToAdd, (uint)JstNextMidnight(UnixNow()));
                return ((byte)quantity, (short)pointsToAdd);
            }
            return (0, 0);
        }

        // ---- slips, deeds ------------------------------------------------------------------------------------------

        // SearchItem in the inventory: the first stack of it
        private static Item FirstInInventory(CharData c, ushort itemId) =>
            c.Items.Where(i => i.Location == Container.Inventory && i.ItemId == itemId).OrderBy(i => i.Slot).FirstOrDefault();

        private void SaveSlipExtra(CharData c, Item slip, byte[] extra) =>
            Exec("UPDATE char_inventory SET extra = ? WHERE charid = ? AND location = ? AND slot = ? LIMIT 1",
                 BoundValue.Of(extra), BoundValue.Of(c.Id), BoundValue.Of((byte)slip.Location), BoundValue.Of(slip.Slot));

        private void SaveClaimedDeeds(CharData c) =>
            Exec("UPDATE char_unlocks SET claimed_deeds = ? WHERE charid = ? LIMIT 1", BoundValue.Of(c.ClaimedDeeds), BoundValue.Of(c.Id));

        private static uint ClaimedDeed(CharData c, int index) => BitConverter.ToUInt32(c.ClaimedDeeds, index * 4);

        private static void SetClaimedDeed(CharData c, int index, uint value) => BitConverter.GetBytes(value).CopyTo(c.ClaimedDeeds, index * 4);

        // a Lua array of numbers, as sol's as<std::vector<T>> reads it
        private static List<long> NumberArray(LuaState l, int idx)
        {
            var list = new List<long>();
            if (!l.IsTable(idx)) return list;
            for (int i = 1, n = l.ObjLen(idx); i <= n; i++)
            {
                l.RawGetI(idx, i);
                list.Add(l.IsNumber(-1) ? l.ToIntegral(-1) : 0);
                l.Pop(1);
            }
            return list;
        }

        private void RegisterSmallSystemBindings(Dictionary<string, LuaFunction> m)
        {
            // ---- guild points ----
            // getCurrentGPItem(guildId): today's item and the points left to earn with it
            m["getCurrentGPItem"] = l =>
            {
                ushort itemId = 0, remaining = 0;
                if (Entity(l) is CharData c)
                {
                    if (GuildById(l.ToIntArg(2)) is { } g) (itemId, remaining) = DailyGpItem(c, g);
                }
                else log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                l.Push(itemId);
                l.Push(remaining);
                return 2;
            };
            // addGuildPoints(guildId, tradeSlot): the quantity taken from the traded stack and the points earned
            m["addGuildPoints"] = l =>
            {
                byte quantity = 0;
                short points = 0;
                if (Entity(l) is CharData c)
                {
                    int slot = l.ToIntArg(3);
                    if (c.NpcTrade is { } t && GuildById(l.ToIntArg(2)) is { } g)
                    {
                        uint traded = slot >= 0 && slot < t.Slots.Length ? t.Slots[slot].Quantity : 0;
                        (quantity, points) = AddGuildPoints(c, g, NpcTradeItem(c, t, slot), traded);
                    }
                }
                else log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                l.Push(quantity);
                l.Push(points);
                return 2;
            };

            // ---- traverser stones ----
            m["getAvailableTraverserStones"] = l => Ret(l, Entity(l) is CharData c ? AvailableTraverserStones(c) : 0);
            m["getClaimedTraverserStones"] = l =>
            {
                if (Entity(l) is not CharData c) return Ret(l, 0);
                var r = Exec("SELECT traverser_claimed FROM char_unlocks WHERE charid = ? LIMIT 1", BoundValue.Of(c.Id));
                return Ret(l, r != null && r.Next() ? r.Get<uint>("traverser_claimed") : 0);
            };
            m["addClaimedTraverserStones"] = l =>
            {
                if (Entity(l) is CharData c)
                    Exec("UPDATE char_unlocks SET traverser_claimed = traverser_claimed + ? WHERE charid = ?",
                         BoundValue.Of((ushort)l.ToIntArg(2)), BoundValue.Of(c.Id));
                return 0;
            };
            m["setClaimedTraverserStones"] = l =>
            {
                if (Entity(l) is CharData c)
                    Exec("UPDATE char_unlocks SET traverser_claimed = ? WHERE charid = ?", BoundValue.Of((ushort)l.ToIntArg(2)), BoundValue.Of(c.Id));
                return 0;
            };

            // ---- porter moogle slips ----
            // getRetrievableItemsForSlip(slipId): the slip's 24 exdata bytes, one bit a stored item; nil without one
            m["getRetrievableItemsForSlip"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    l.PushNil();
                    return 1;
                }
                if (FirstInInventory(c, (ushort)l.ToIntArg(2)) is not { } slip)
                {
                    l.PushNil();
                    return 1;
                }
                l.NewTable(slip.Extra.Length);
                for (int i = 0; i < slip.Extra.Length; i++)
                {
                    l.Push(slip.Extra[i]);
                    l.RawSetI(-2, i + 1);
                }
                return 1;
            };
            // storeWithPorterMoogle(slipId, extra, storableItemIds): the traded gear onto the slip; 0 when stored,
            // the clashing byte when one is already on it, 3 when nothing can be done
            m["storeWithPorterMoogle"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("CLuaBaseEntity::storeWithPorterMoogle() - Non_PC passed to function.");
                    return Ret(l, StoreWithPorterMoogleFailed);
                }
                if (FirstInInventory(c, (ushort)l.ToIntArg(2)) is not { } slip) return Ret(l, StoreWithPorterMoogleFailed);
                if (c.NpcTrade is not { } t)
                {
                    log_($"CLuaBaseEntity::storeWithPorterMoogle: {c.Name} has no trade to take the gear from");
                    return Ret(l, StoreWithPorterMoogleFailed);
                }
                var extra = NumberArray(l, 3).Select(v => (byte)v).ToList();
                var storable = NumberArray(l, 4).Select(v => (ushort)v).ToList();
                // every piece checked before the slip is written, so it never records gear that was not taken
                for (int i = 0; i < extra.Count && i < slip.Extra.Length; i++)
                    if ((slip.Extra[i] & extra[i]) != 0) return Ret(l, extra[i]);
                foreach (ushort itemId in storable)
                {
                    if (itemId == 0) continue;
                    int tradeSlot = Array.FindIndex(t.Slots, s => s.Offered?.ItemId == itemId);
                    if (tradeSlot < 0 || !NpcTradeConfirm(c, t, tradeSlot, 1))
                    {
                        log_($"CLuaBaseEntity::storeWithPorterMoogle: {c.Name} did not offer item {itemId} for the slip");
                        return Ret(l, StoreWithPorterMoogleFailed);
                    }
                }
                bool stored = NpcTradeConsume(c, t, true);
                var slipExtra = (byte[])slip.Extra.Clone();
                if (stored)
                {
                    for (int i = 0; i < extra.Count && i < slipExtra.Length; i++) slipExtra[i] |= extra[i];
                    SaveSlipExtra(c, slip, slipExtra);
                }
                else log_($"storeWithPorterMoogle: {c.Name} kept the gear the slip was to record");
                RemoveNpcTrade(c);
                c.Trade.Clean();
                if (!stored) return Ret(l, StoreWithPorterMoogleFailed);
                slipExtra.CopyTo(slip.Extra, 0);
                return Ret(l, 0);
            };
            // retrieveItemFromSlip(slipId, itemId, extraId, extraData): the item back, then its bit off the slip
            m["retrieveItemFromSlip"] = l =>
            {
                if (Pc(l, "retrieveItemFromSlip") is not CharData c) return 0;
                if (FirstInInventory(c, (ushort)l.ToIntArg(2)) is not { } slip) return 0;
                ushort itemId = (ushort)l.ToIntArg(3);
                int extraId = (ushort)l.ToIntArg(4);
                byte extraData = (byte)l.ToIntArg(5);
                if (ItemBasic(itemId) == null)
                {
                    log_("Failed to get item from item ID");
                    return 0;
                }
                using var t = new ItemTransaction(this, c);
                if (t.GiveItem(Container.Inventory, new Item { ItemId = itemId, Quantity = 1 }) == null)
                {
                    log_($"retrieveItemFromSlip: {c.Name} could not receive item {itemId}, leaving it on the slip");
                    return 0;
                }
                // cleared only after the item lands, so a full inventory does not lose it
                if (extraId < slip.Extra.Length) slip.Extra[extraId] &= extraData;
                SaveSlipExtra(c, slip, slip.Extra);
                t.Commit();
                return 0;
            };

            // ---- claimed deeds ----
            m["getClaimedDeedMask"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("Attempt to get claimed deed mask for Non-PC.");
                    l.PushNil();
                    return 1;
                }
                l.NewTable(5);
                for (int i = 0; i < 5; i++)
                {
                    l.Push(ClaimedDeed(c, i));
                    l.RawSetI(-2, i + 1);
                }
                return 1;
            };
            m["setClaimedDeed"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("Attempt to set claimed deed mask for Non-PC.");
                    return 0;
                }
                int bit = (ushort)l.ToIntArg(2), index = bit / 32;
                if (index < 5)
                {
                    SetClaimedDeed(c, index, ClaimedDeed(c, index) | 1u << bit % 32);
                    SaveClaimedDeeds(c);
                }
                return 0;
            };
            // toggleReceivedDeedRewards: the hide / show setting, packed into the first word's unused bit 0
            m["toggleReceivedDeedRewards"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("Attempt to toggle hide/show received rewards for Non-PC.");
                    return 0;
                }
                SetClaimedDeed(c, 0, ClaimedDeed(c, 0) ^ 1);
                SaveClaimedDeeds(c);
                return 0;
            };
            // resetClaimedDeeds: all but the fourth word's first two bits, the reset count in the fifth word's top bits
            m["resetClaimedDeeds"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("Attempt to set claimed deed mask for Non-PC.");
                    return 0;
                }
                uint numResets = (ClaimedDeed(c, 4) >> 18) + 1;
                SetClaimedDeed(c, 3, ClaimedDeed(c, 3) & 0b11);
                SetClaimedDeed(c, 4, numResets << 18);
                SaveClaimedDeeds(c);
                return 0;
            };
            m["sendPartialMissionLog"] = l =>
            {
                if (Pc(l, "sendPartialMissionLog") is not CharData c) return 0;
                int log = (byte)l.ToEnumArg(2); // MissionLog
                if (log < MaxMissionArea) SendPartialMissionLog(c, log, l.ToBoolean(3));
                return 0;
            };

            // ---- maze vouchers and runes (bitset<64>, bitset<512>, numbered from 1) ----
            m["learnMazeVoucher"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"CLuaBaseEntity::learnMazeVoucher called on non-PC entity ({NameOf(Entity(l))}).");
                    return 0;
                }
                int id = (byte)l.ToIntArg(2);
                if (id >= 1 && id <= c.MazeVouchers.Length * 8) c.MazeVouchers[(id - 1) / 8] |= (byte)(1 << (id - 1) % 8);
                SaveMazeUnlocks(c);
                return 0;
            };
            m["hasMazeVoucher"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"CLuaBaseEntity::hasMazeVoucher called on non-PC entity ({NameOf(Entity(l))}).");
                    return Ret(l, false);
                }
                int id = (byte)l.ToIntArg(2);
                return Ret(l, id >= 1 && id <= c.MazeVouchers.Length * 8 && (c.MazeVouchers[(id - 1) / 8] & 1 << (id - 1) % 8) != 0);
            };
            m["learnMazeRune"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"CLuaBaseEntity::learnMazeRune called on non-PC entity ({NameOf(Entity(l))}).");
                    return 0;
                }
                int id = (ushort)l.ToIntArg(2);
                if (id >= 1 && id <= c.MazeRunes.Length * 8) c.MazeRunes[(id - 1) / 8] |= (byte)(1 << (id - 1) % 8);
                SaveMazeUnlocks(c);
                return 0;
            };
            m["hasMazeRune"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"CLuaBaseEntity::hasMazeRune called on non-PC entity ({NameOf(Entity(l))}).");
                    return Ret(l, false);
                }
                int id = (ushort)l.ToIntArg(2);
                return Ret(l, id >= 1 && id <= c.MazeRunes.Length * 8 && (c.MazeRunes[(id - 1) / 8] & 1 << (id - 1) % 8) != 0);
            };

            // ---- the fishing contest ----
            m["getContestScore"] = l => Ret(l, Entity(l) is CharData c && Contest.PlayerEntry(c.Name) is { } e ? e.Score : 0);
            m["getContestRank"] = l => Ret(l, Entity(l) is CharData c && Contest.PlayerEntry(c.Name) is { } e ? e.ContestRank : 0);
            // getContestRewardStatus: { rank, share } of the char's unclaimed entry, else zeros
            m["getContestRewardStatus"] = l =>
            {
                byte rank = 0, share = 0;
                if (Entity(l) is CharData c)
                {
                    var r = Exec("SELECT contestrank, share FROM fishing_contest_entries WHERE charid = ? AND claimed != 1", BoundValue.Of(c.Id));
                    if (r != null && r.Next())
                    {
                        rank = r.Get<byte>("contestrank");
                        share = r.Get<byte>("share");
                    }
                }
                l.NewTable(0, 2);
                l.Push(rank);
                l.SetField(-2, "rank");
                l.Push(share);
                l.SetField(-2, "share");
                return 1;
            };

            // ---- steal and despoil ----
            m["getStealItem"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"Attempting to get steal item for invalid entity type ({NameOf(Entity(l))}).");
                    return Ret(l, 0);
                }
                return Ret(l, StealItem(mob));
            };
            // getDespoilItem: a random despoilable drop not yet despoiled, else the steal item
            m["getDespoilItem"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"Attempting to get despoil item for invalid entity type ({NameOf(Entity(l))}).");
                    return Ret(l, 0);
                }
                if (DropListOf(mob) is { } drops && !mob.ItemDespoiled)
                {
                    var despoilable = drops.Items.Where(d => d.Type == DropDespoil).ToList();
                    if (despoilable.Count > 0) return Ret(l, despoilable[MobRng.Next(despoilable.Count)].ItemId);
                }
                return Ret(l, StealItem(mob));
            };
            m["itemStolen"] = l =>
            {
                if (Entity(l) is MobEntity mob) mob.ItemStolen = l.ToBoolean(2);
                else log_($"Attempting to flag stolen item for invalid entity type ({NameOf(Entity(l))}).");
                return 0;
            };
            m["itemDespoiled"] = l =>
            {
                if (Entity(l) is MobEntity mob) mob.ItemDespoiled = l.ToBoolean(2);
                else log_($"Attempting to flag despoiled item for invalid entity type ({NameOf(Entity(l))}).");
                return 0;
            };
            m["getDespoilDebuff"] = l =>
            {
                var r = Exec("SELECT effectId FROM despoil_effects WHERE itemId = ? LIMIT 1", BoundValue.Of((ushort)l.ToIntArg(2)));
                return Ret(l, r != null && r.Next() ? r.Get<ushort>("effectId") : 0);
            };

            // restoreFromChest(chest, type): the Abyssea chest's MobSkillFinish to those near it, the HP (1) or MP (2)
            // the char lacks; the restore itself is the script's
            m["restoreFromChest"] = l =>
            {
                if (Entity(l) is not CharData c || l.ToObject(2) is not { } chest || c.Animation == Anim.Death) return 0;
                int type = l.ToIntArg(3);
                ushort animation = 0, message = 0;
                int param = 0;
                if (type == 1) (param, message, animation) = (c.MaxHp - c.Hp, MsgTargetRegainsHp, AnimRegainHp);
                else if (type == 2) (param, message, animation) = (c.MaxMp - c.Mp, MsgTargetRegainsMp, AnimRegainMp);
                var action = new ActionPacket { ActorId = IdOf(chest), ActionType = ActionCategoryMobSkillFinish };
                var r = action.AddTarget(c.Id).AddResult();
                r.Animation = animation;
                r.Param = param;
                r.MessageId = message;
                PushCharsNear(chest, Battle2Packets.Battle2(action));
                return 0;
            };
        }

        // getStealItem: a random steal drop, unless one was stolen already
        private ushort StealItem(MobEntity mob)
        {
            if (DropListOf(mob) is not { } drops || mob.ItemStolen) return 0;
            var items = drops.Items.Where(d => d.Type == DropSteal).Select(d => d.ItemId).ToList();
            return items.Count > 0 ? items[MobRng.Next(items.Count)] : (ushort)0;
        }

        // charutils::SaveMazeUnlocks
        private void SaveMazeUnlocks(CharData c) =>
            Exec("UPDATE char_unlocks SET maze_vouchers = ?, maze_runes = ? WHERE charid = ? LIMIT 1",
                 BoundValue.Of(c.MazeVouchers), BoundValue.Of(c.MazeRunes), BoundValue.Of(c.Id));
    }
}
