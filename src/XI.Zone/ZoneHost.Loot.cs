using System;
using System.Collections.Generic;
using XI.Common;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // A kill's loot: CMobEntity::DropItems (the drop list's groups and items, through treasure hunter, then seals, geodes
    // and crystals), the gil (CMobEntity::CanDropGil / GetRandomGil, charutils::DistributeGil), the drop lists
    // themselves (zoneutils::buildDropList from a template's loot, itemutils::LoadDropList from mob_droplist for
    // setDropID) and the special drops' 5 minute party recast. The ITEM_DROPS listeners get a copy of the drop list as a
    // CLootContainer to add to (addItem / addGroup at the xi.loot.rate tiers, addItemFixed / addGroupFixed at a fixed
    // rate treasure hunter leaves alone); TREASUREPOOL hears each item put in the pool.
    // A Monipulator gets no drop list or geodes, and crystals without Signet only under Belligerency (the drops
    // themselves only under Belligerency, ZoneHost.Exp). The treasure hunter level comes from the
    // enmity's direct actions and a THF's procs (HandleEnspell, ZoneHost.Enspell.cs).
    public sealed partial class ZoneHost
    {
        // DROP_TYPE
        private const byte DropNormal = 0x00, DropGrouped = 0x01, DropSteal = 0x02, DropDespoil = 0x04;
        private const uint MaxDropId = 5000;
        private const ushort MsgBasicObtains = 565;
        private const ushort ItemBeastmensSeal = 1126, ItemKindredsSeal = 1127, ItemKindredsCrest = 2955,
                             ItemHighKindredsCrest = 2956, ItemSacredKindredsCrest = 2957;
        private const ushort ItemGeodeBase = 3296, ItemAvatariteBase = 3519, ItemCrystalBase = 4095; // + the element
        private const ushort EffectSigil = 268, EffectIonis = 512;
        private const byte RegionLumoria = 26, RegionMamoolJa = 29, RegionHalvung = 30, RegionArrapago = 31,
                           RegionRonfaureFront = 33, RegionValdeauniaFront = 40, RegionAdoulinIslands = 44, RegionEastUlbuka = 45;
        private const byte NationBeastmen = 3;
        private const float LootRange = 100f;
        private const long SpecialDropCooldownMs = 5 * 60 * 1000;
        private const int LootRecastSeal = 0, LootRecastGeode = 1;   // LootRecastID

        // DropItem_t / DropGroup_t / DropList_t
        internal sealed class LootItem
        {
            public byte Type;
            public ushort ItemId, DropRate;
            public bool FixedRate; // hasFixedRate: treasure hunter does not touch the rate
        }

        internal sealed class LootGroup
        {
            public ushort GroupRate;
            public bool FixedRate;
            public readonly List<LootItem> Items = new List<LootItem>();
        }

        internal sealed class DropList
        {
            public readonly List<LootItem> Items = new List<LootItem>();
            public readonly List<LootGroup> Groups = new List<LootGroup>();
        }

        private readonly Dictionary<MobTemplate, DropList> templateDropLists_ = new Dictionary<MobTemplate, DropList>();
        private Dictionary<uint, DropList> dbDropLists_;
        private Dictionary<string, ushort> itemIdsByName_;

        // xi::items::lookupIdByName: item_basic's names, the lowest id where two items share one
        private ushort? ItemIdByName(string name)
        {
            if (itemIdsByName_ == null)
            {
                itemIdsByName_ = new Dictionary<string, ushort>();
                var r = Exec("SELECT itemid, name FROM item_basic");
                while (r != null && r.Next())
                {
                    ushort id = r.Get<ushort>("itemid");
                    string n = r.Get<string>("name");
                    if (!itemIdsByName_.TryGetValue(n, out var have) || id < have) itemIdsByName_[n] = id;
                }
            }
            return itemIdsByName_.TryGetValue(name, out var found) ? found : null;
        }

        // zoneutils::buildDropList, on the template's first kill: an unknown name is logged and left out (the C++ exits)
        private DropList TemplateDropList(MobTemplate t)
        {
            if (templateDropLists_.TryGetValue(t, out var list)) return list;
            list = new DropList();
            bool Resolve(string name, out ushort id)
            {
                id = 0;
                if (name == "nothing") return true;
                if (ItemIdByName(name) is ushort found)
                {
                    id = found;
                    return true;
                }
                log_($"buildDropList: template '{t.Name}' names unknown or ambiguous item '{name}'");
                return false;
            }
            foreach (var roll in t.Loot.Drops)
            {
                if (roll.OneOf.Count == 0)
                {
                    if (Resolve(roll.Item, out var id)) list.Items.Add(new LootItem { Type = DropNormal, ItemId = id, DropRate = roll.Chance });
                    continue;
                }
                var group = new LootGroup { GroupRate = roll.Chance };
                foreach (var (name, weight) in roll.OneOf)
                    if (Resolve(name, out var id)) group.Items.Add(new LootItem { Type = DropGrouped, ItemId = id, DropRate = weight });
                list.Groups.Add(group);
            }
            foreach (var name in t.Loot.Steal)
                if (Resolve(name, out var id)) list.Items.Add(new LootItem { Type = DropSteal, ItemId = id, DropRate = 0 });
            // despoil keeps its weights, though the engine picks uniformly
            foreach (var (name, weight) in t.Loot.Despoil)
                if (Resolve(name, out var id)) list.Items.Add(new LootItem { Type = DropDespoil, ItemId = id, DropRate = weight });
            templateDropLists_[t] = list;
            return list;
        }

        // itemutils::GetDropList: mob_droplist, read on first use; id 0 is an empty list (loot from scripts only)
        private DropList DbDropList(uint dropId)
        {
            if (dbDropLists_ == null)
            {
                dbDropLists_ = new Dictionary<uint, DropList> { [0] = new DropList() };
                var r = Exec("SELECT dropId, itemId, dropType, itemRate, groupId, groupRate FROM mob_droplist WHERE dropid < ?",
                             BoundValue.Of(MaxDropId));
                while (r != null && r.Next())
                {
                    uint id = r.Get<uint>("dropId");
                    if (id == 0) continue;
                    if (!dbDropLists_.TryGetValue(id, out var list)) dbDropLists_[id] = list = new DropList();
                    var item = new LootItem { Type = r.Get<byte>("dropType"), ItemId = r.Get<ushort>("itemId"), DropRate = r.Get<ushort>("itemRate") };
                    if (item.Type == DropGrouped)
                    {
                        byte groupId = r.Get<byte>("groupId");
                        ushort groupRate = r.Get<ushort>("groupRate");
                        while (groupId > list.Groups.Count) list.Groups.Add(new LootGroup { GroupRate = groupRate });
                        if (groupId == 0) continue;
                        list.Groups[groupId - 1].GroupRate = groupRate;
                        list.Groups[groupId - 1].Items.Add(item);
                    }
                    else list.Items.Add(item);
                }
            }
            return dbDropLists_.TryGetValue(dropId, out var found) ? found : null;
        }

        // CMobEntity::dropList: the template's loot unless setDropID replaced it
        private DropList DropListOf(MobEntity m) =>
            m.TemplateLoot && m.Template != null ? TemplateDropList(m.Template) : DbDropList(m.DropId);

        // CLuaLootContainer: the drop list ITEM_DROPS listeners add to
        private const string LootLuaType = "CLootContainer";
        // the xi.loot.rate tiers in tenths of a percent (never .. guaranteed)
        private static readonly ushort[] LootRatePercentages = { 0, 1, 5, 10, 50, 100, 150, 240, 1000 };

        private void RegisterLootType()
        {
            static DropList D(LuaState l) => l.ToObject(1) as DropList ?? throw new InvalidOperationException("bad self: expected " + LootLuaType);
            static ushort Tier(int rate) => rate >= 0 && rate < LootRatePercentages.Length ? LootRatePercentages[rate] : (ushort)0;
            int AddItem(LuaState l, bool fixedRate)
            {
                var drops = D(l);
                ushort item = (ushort)l.ToIntArg(2);
                ushort rate = fixedRate ? (ushort)l.ToIntArg(3) : Tier(l.ToIntArg(3));
                int quantity = l.IsNumber(4) ? (ushort)l.ToIntArg(4) : 1;
                for (int i = 0; i < quantity; i++)
                    drops.Items.Add(new LootItem { Type = DropNormal, ItemId = item, DropRate = rate, FixedRate = fixedRate });
                return 0;
            }
            int AddGroup(LuaState l, bool fixedRate)
            {
                var drops = D(l);
                var group = new LootGroup { GroupRate = fixedRate ? (ushort)l.ToIntArg(2) : Tier(l.ToIntArg(2)), FixedRate = fixedRate };
                if (l.IsTable(3))
                {
                    int n = l.ObjLen(3);
                    for (int i = 1; i <= n; i++)
                    {
                        l.RawGetI(3, i);
                        if (l.IsTable(-1))
                        {
                            int at = l.Top;
                            // item.is<uint16>() is any number; it and get_or<uint16>("weight", 1) are sol2's llround
                            l.GetField(at, "item");
                            bool hasItem = l.IsNumber(-1);
                            ushort id = hasItem ? (ushort)l.ToIntegral(-1) : (ushort)0;
                            l.Pop(1);
                            if (hasItem)
                                group.Items.Add(new LootItem { Type = DropGrouped, ItemId = id, DropRate = (ushort)FieldInt(l, at, "weight", 1) });
                            else log_("CLuaLootContainer::addGroup() - entry in group is missing valid 'item'");
                        }
                        l.Pop(1);
                    }
                }
                drops.Groups.Add(group);
                return 0;
            }
            lua_.RegisterType(LootLuaType, new Dictionary<string, LuaFunction>
            {
                ["addItem"] = l => AddItem(l, false),
                ["addItemFixed"] = l => AddItem(l, true),
                ["addGroup"] = l => AddGroup(l, false),
                ["addGroupFixed"] = l => AddGroup(l, true),
            });
        }

        // xi.combat.treasureHunter.getDropRate(thLevel, dropRate); without it the rate stands
        private ushort ThDropRate(int thLevel, ushort dropRate)
        {
            if (lua_ == null) return dropRate;
            const string path = "xi.combat.treasureHunter.getDropRate";
            int top = lua_.Top;
            try
            {
                lua_.GetPath(path);
                if (!lua_.IsFunction(-1)) return dropRate;
                lua_.Push((double)thLevel);
                lua_.Push((double)dropRate);
                lua_.Call(2, 1);
                return lua_.IsNumber(-1) ? (ushort)lua_.ToIntegral(-1) : (ushort)0;
            }
            catch (LuaException e)
            {
                ScriptError(path, e);
                return dropRate;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // ---- special drop recast ------------------------------------------------------------------------------------

        // the members of the char's treasure pool in its party (or just the char)
        private IEnumerable<CharData> SpecialRecastMembers(CharData c)
        {
            if (c.Party == null || c.TreasurePool == null) return new[] { c };
            return c.TreasurePool.Members.FindAll(member => member.Party == c.Party);
        }

        private static bool HasLootRecast(CharData c, int id, long now) => c.LootRecastUntilMs.TryGetValue(id, out var until) && until > now;

        // CanAddSpecial: nobody of the party in the pool is on the recast
        private bool CanAddSpecial(CharData c, int id, long now)
        {
            foreach (var member in SpecialRecastMembers(c))
                if (HasLootRecast(member, id, now)) return false;
            return true;
        }

        private void AddSpecialRecast(CharData c, int id, long now)
        {
            foreach (var member in SpecialRecastMembers(c)) member.LootRecastUntilMs[id] = now + SpecialDropCooldownMs;
        }

        // ---- the drops ----------------------------------------------------------------------------------------------

        // CMobEntity::GetEligibleSeals: an ABYSSEA-enabled server drops the later bands
        private ushort[] EligibleSeals(MobEntity m)
        {
            if (!contentEnabled_("ABYSSEA")) return m.MLvl >= 50 ? new[] { ItemBeastmensSeal, ItemKindredsSeal } : new[] { ItemBeastmensSeal };
            if (m.MLvl >= 100) return new[] { ItemKindredsCrest, ItemHighKindredsCrest, ItemSacredKindredsCrest };
            if (m.MLvl >= 80) return new[] { ItemKindredsCrest, ItemHighKindredsCrest };
            if (m.MLvl >= 70) return new[] { ItemKindredsSeal, ItemKindredsCrest };
            if (m.MLvl >= 50) return new[] { ItemBeastmensSeal, ItemKindredsSeal };
            return new[] { ItemBeastmensSeal };
        }

        // CMobEntity::GetEligibleGeodes: ABYSSEA only; the weather's element, else the day's; a geode from level 50, an
        // avatarite too from 80
        private ushort[] EligibleGeodes(MobEntity m, long now)
        {
            if (!contentEnabled_("ABYSSEA")) return Array.Empty<ushort>();
            const ushort weatherHotSpell = 4, weatherDarkness = 19;
            byte element;
            if (ZoneInfoOf(m.Zone).Weather >= weatherHotSpell && ZoneInfoOf(m.Zone).Weather <= weatherDarkness) element = MobSpawnHandler.WeatherElement(ZoneInfoOf(m.Zone).Weather);
            else
            {
                // battleutils::GetDayElement: firesday .. darksday
                byte[] dayElement = { 1, 4, 6, 3, 2, 5, 7, 8 };
                uint day = VanaTime.GetWeekDay(VanaTicksNow());
                element = day < dayElement.Length ? dayElement[day] : (byte)0;
            }
            ushort geode = element == 0 ? (ushort)0 : (ushort)(ItemGeodeBase + element);
            ushort avatarite = element == 0 ? (ushort)0 : (ushort)(ItemAvatariteBase + element);
            if (m.MLvl >= 80) return new[] { geode, avatarite };
            if (m.MLvl >= 50) return new[] { geode };
            return Array.Empty<ushort>();
        }

        // CMobEntity::DropItems: the rolls go into the killer's treasure pool
        private void DropItems(CharData c, MobEntity m, long now)
        {
            if (c.TreasurePool == null) return;
            void AddItemToPool(ushort itemId)
            {
                c.TreasurePool.AddItem(itemId, m);
                TriggerListener(m, "TREASUREPOOL", m, c, itemId);
            }
            float multiplier = loader_.Stats?.Settings.DropRateMultiplier ?? 1f;

            var list = DropListOf(m);
            if (m.GetMobMod(MobMod.NoDrops) == 0 && c.Monstrosity == null && list != null &&
                (list.Items.Count > 0 || list.Groups.Count > 0 || m.Events.HasListener("ITEM_DROPS")))
            {
                // LootContainer: the listeners add to a copy, the list itself stays
                var drops = new DropList();
                drops.Items.AddRange(list.Items);
                drops.Groups.AddRange(list.Groups);
                TriggerListener(m, "ITEM_DROPS", m, drops);
                lua_?.Forget(drops);

                foreach (var group in drops.Groups)
                {
                    ushort total = 0;
                    foreach (var item in group.Items) total += item.DropRate;
                    ushort groupDropRate = (ushort)(group.GroupRate * 10);
                    if (!group.FixedRate) groupDropRate = ThDropRate(m.ThLvl, groupDropRate);
                    if (groupDropRate > 0 && 1 + MobRng.Next(10000) <= groupDropRate * multiplier)
                    {
                        // each item takes the range from the previous total to it plus its rate
                        ushort previousRateValue = 0;
                        int itemRoll = total == 0 ? 0 : MobRng.Next(total);
                        foreach (var item in group.Items)
                        {
                            if (itemRoll < previousRateValue + item.DropRate)
                            {
                                AddItemToPool(item.ItemId);
                                break;
                            }
                            previousRateValue += item.DropRate;
                        }
                    }
                }
                foreach (var item in drops.Items)
                {
                    ushort itemDropRate = (ushort)(item.DropRate * 10);
                    if (!item.FixedRate) itemDropRate = ThDropRate(m.ThLvl, itemDropRate);
                    if (itemDropRate > 0 && 1 + MobRng.Next(10000) <= itemDropRate * multiplier) AddItemToPool(item.ItemId);
                }
            }

            byte mobRegion = ZoneRegions.Of(m.Zone);
            bool validZone = (m.Type & MobType.Battlefield) == 0 && !ZoneInfoOf(m.Zone).Types.Contains("dynamis") && mobRegion != RegionLumoria;
            if (m.GetMobMod(MobMod.NoDrops) != 0 || !validZone || CheckMob(m.HiPcLvl, m) <= MobTooWeak || m.GetMobMod(MobMod.ExpBonus) <= -100)
                return;

            // seals, geodes and avatarites do not drop from notorious monsters; crystals still do
            if (!m.IsNotorious && MobRng.Next(100) < 20 && CanAddSpecial(c, LootRecastSeal, now))
            {
                var seals = EligibleSeals(m);
                AddItemToPool(seals[MobRng.Next(seals.Length)]);
                AddSpecialRecast(c, LootRecastSeal, now);
            }
            if (!m.IsNotorious && c.Monstrosity == null && MobRng.Next(100) < 20 && CanAddSpecial(c, LootRecastGeode, now))
            {
                var geodes = EligibleGeodes(m, now);
                if (geodes.Length > 0)
                {
                    AddItemToPool(geodes[MobRng.Next(geodes.Length)]);
                    AddSpecialRecast(c, LootRecastGeode, now);
                }
            }

            if (m.Element == 0) return;
            byte region = ZoneRegions.Of(c.Zone);
            ushort requiredEffect = 0;
            if (region == RegionWestAhtUrhgan || region == RegionMamoolJa || region == RegionHalvung || region == RegionArrapago ||
                region == RegionAlzadaal)
                requiredEffect = EffectSanction;
            else if (region >= RegionRonfaureFront && region <= RegionValdeauniaFront) requiredEffect = EffectSigil;
            else if (region == RegionAdoulinIslands || region == RegionEastUlbuka) requiredEffect = EffectIonis;
            else if (region < RegionTavnazia && GetConquestData().RegionOwner(region) != NationBeastmen) requiredEffect = EffectSignet;
            if (requiredEffect == 0) return;

            // a Monipulator under Belligerency finds crystals without Signet (Monipulators never party)
            bool belligerent = c.Monstrosity is { Belligerency: true };
            int playersNearby = 0;
            foreach (var member in c.Party != null ? c.Party.Members.ToArray() : new[] { c })
                if ((member.StatusEffects.HasStatusEffect(requiredEffect) || belligerent) && SameArea(member, m) && Distance(member, m) < LootRange)
                    playersNearby++;
            if (playersNearby == 0) return;

            // Signet: 55% solo, 45% in a party; Sanction 30%; the others 20%
            int crystalRate = requiredEffect == EffectSignet ? (playersNearby == 1 ? 55 : 45) : requiredEffect == EffectSanction ? 30 : 20;
            for (int i = 0; i < playersNearby; i++)
                if (MobRng.Next(100) < crystalRate) AddItemToPool((ushort)(ItemCrystalBase + m.Element));
        }

        // ---- gil ----------------------------------------------------------------------------------------------------

        // CMobEntity::CanDropGil: a negative GilMax never drops; a GilMin, a GilMax or a GilBonus does
        private static bool CanDropGil(MobEntity m)
        {
            if (m.GetMobMod(MobMod.GilMax) < 0) return false;
            if (m.GetMobMod(MobMod.GilMin) > 0 || m.GetMobMod(MobMod.GilMax) != 0) return true;
            return m.GetMobMod(MobMod.GilBonus) > 0;
        }

        // CMobEntity::GetRandomGil: an exact range when both bounds are set, else from the level
        private uint RandomGil(MobEntity m)
        {
            int min = m.GetMobMod(MobMod.GilMin), max = m.GetMobMod(MobMod.GilMax);
            if (min != 0 && max != 0)
            {
                if (max <= min) return (uint)min;
                if (max - min < 2)
                {
                    max = min + 2;
                    log_("CMobEntity::GetRandomGil Max value is set too low, defaulting");
                }
                return (uint)MobRng.Next(min, max);
            }
            float gil = MathF.Pow(m.MLvl, 1.05f);
            gil = Math.Max(gil, 1.0f);
            ushort highGil = (ushort)(gil / 3 + 4);
            if (max != 0) highGil = (ushort)max;
            highGil = Math.Max(highGil, (ushort)2);
            gil += MobRng.Next(highGil);
            if (min != 0 && gil < min) gil = min;
            if (m.GetMobMod(MobMod.GilBonus) != 0) gil *= m.GetMobMod(MobMod.GilBonus) / 100.0f;
            return (uint)gil;
        }

        // charutils::DistributeGil: the multiplier, the level bonus, gilfinder and the kill shot's moghancement, split
        // among the alliance members within 100 yalms
        private void DistributeGil(CharData c, MobEntity m)
        {
            var settings = loader_.Stats?.Settings;
            uint gil = RandomGil(m);
            float gilMultiplier = settings?.MobGilMultiplier ?? 1f;
            if (gil != 0 && gilMultiplier >= 0.0f) gil = (uint)(gil * gilMultiplier);
            byte allMobsGilBonus = settings?.AllMobsGilBonus ?? 0;
            if (allMobsGilBonus != 0)
            {
                uint gBonus = (uint)(allMobsGilBonus * m.MLvl);
                gil += Math.Clamp(gBonus, 1u, settings.MaxGilBonus);
            }
            // gilfinder: 1 + (128 + 0..GF level * 16) / 256
            if (m.GilfinderLevel > 0) gil = (uint)(gil * (1 + (128 + MobRng.Next(0, m.GilfinderLevel * 16)) / 256.0));
            short killshotBonus = c.GetMod(Mod.MOGHANCEMENT_GIL_BONUS_P);
            if (killshotBonus > 0) gil = (uint)(gil * ((100.0 + killshotBonus) / 100.0));

            void Earn(CharData member, uint amount)
            {
                if (Gil(member) == null || !AdjustGil(member, (int)amount))
                {
                    log_($"DistributeGil: {member.Name} did not receive {amount} gil");
                    return;
                }
                PushTo(member, SelfPackets.BattleMessage2(member, member, (int)amount, 0, MsgBasicObtains));
            }

            if (c.Party != null)
            {
                var members = new List<CharData>();
                foreach (var member in ForAlliance(c))
                    if (SameArea(member, m) && Distance(member, m) <= LootRange) members.Add(member);
                if (members.Count == 0) return;
                uint gilPerPerson = (uint)(gil / members.Count);
                foreach (var member in members) Earn(member, gilPerPerson);
            }
            else if (Distance(c, m) <= LootRange) Earn(c, gil);
        }

        // the kill's gil and drops (the end of DistributeRewards); a negative GilMax keeps ALL_MOBS_GIL_BONUS off
        // (Dynamis / Limbus)
        private void DistributeLoot(CharData c, MobEntity m, long now)
        {
            var settings = loader_.Stats?.Settings;
            if (((settings?.MobGilMultiplier ?? 1f) > 0.0f && CanDropGil(m)) ||
                ((settings?.AllMobsGilBonus ?? 0) > 0 && m.GetMobMod(MobMod.GilMax) >= 0))
                DistributeGil(c, m);
            DropItems(c, m, now);
        }

        // ---- lua ----------------------------------------------------------------------------------------------------

        private void RegisterLootBindings(Dictionary<string, XI.Script.LuaFunction> m)
        {
            // mob:getDropID() / mob:setDropID(id): a numbered list replaces whatever the mob's own data gave it
            m["getDropID"] = l =>
            {
                if (Entity(l) is MobEntity mob) return Ret(l, mob.DropId);
                log_($"Attempting to get drop id for invalid entity type ({NameOf(Entity(l))}).");
                return Ret(l, 0);
            };
            m["setDropID"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"Attempting to set drop id for invalid entity type ({NameOf(Entity(l))}).");
                    return 0;
                }
                mob.DropId = (uint)l.ToUIntArg(2);
                mob.TemplateLoot = false;
                return 0;
            };
            // mob:getTHlevel() / mob:setTHlevel(level)
            m["getTHlevel"] = l => Ret(l, Entity(l) is MobEntity mob ? mob.ThLvl : 0);
            m["setTHlevel"] = l =>
            {
                if (Entity(l) is MobEntity mob) mob.ThLvl = (short)l.ToIntArg(2);
                return 0;
            };
        }
    }
}
