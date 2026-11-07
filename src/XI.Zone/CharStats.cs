using System;
using System.Collections.Generic;

namespace XI.Zone
{
    // The CCharEntity / CBattleEntity stat code the status packets read: equipping at login with its modifiers,
    // BuildingCharSkillsTable, UpdateHealth, and the STR/VIT/ATT/DEF getters. Status effect modifiers come in through
    // the scripts' addMod, the traits' from ZoneHost.Traits and latents through LatentEffectContainer; pets are not
    // applied, so their modifiers are always 0.
    public sealed class CharStats
    {
        public const int SlotMain = 0, SlotSub = 1, SlotRanged = 2, SlotAmmo = 3, SlotEar1 = 11, SlotEar2 = 12, SlotRing1 = 13,
            SlotRing2 = 14, SlotBack = 15;

        private readonly GrowthTables growth_;
        private readonly ItemData items_;
        private readonly StatSettings settings_;

        public CharStats(GrowthTables growth, ItemData items, StatSettings settings)
        {
            growth_ = growth;
            items_ = items;
            settings_ = settings ?? new StatSettings();
        }

        public GrowthTables Growth => growth_;

        // set by the zone: monstrosity's exp table
        public Func<MonstrosityTable> MonTable;

        // charutils::GetExpNEXTLevelForChar: a Monipulator levels on monstrosity's table
        public uint ExpNextLevelFor(CharData c, int level) =>
            c.Monstrosity != null && MonTable?.Invoke() is { } table ? Monstrosity.ExpNextLevel(table, level) : growth_.GetExpNextLevel(level);

        // the clock a charged item's assign time is taken from (unix seconds)
        public Func<long> UnixNow = XI.Common.EarthTime.UnixSec;
        // a charged item equipped with charges left (true) or taken off (false): the zone keeps its item recast
        public Action<CharData, Item, bool> ChargedItemEquipChanged;
        // luautils::OnItemEquip (true) / OnItemUnequip (false)
        public Action<CharData, Item, bool> ItemEquipScript;
        // an equipment change while riding the char's own chocobo (racing silks change its speed)
        public Action<CharData> PersonalChocoboGearChanged;
        public StatSettings Settings => settings_;
        public ItemData Items => items_;

        // ---- equipment (charutils::LoadEquip / EquipItem / UnequipItem / EquipArmor) ----

        // until BuildingCharTraitsTable has run for the char (a loader without the zone host's trait hook), the dual
        // wield trait is assumed: the client only offers sub weapons to the jobs that can have it
        public const int TraitDualWield = 18;

        // charutils::hasTrait: the m_TraitList bit
        public static bool HasTrait(CharData c, int traitId) => (c.Traits[traitId >> 3] & 1 << (traitId & 7)) != 0;
        private static bool HasDualWield(CharData c) => !c.TraitsBuilt || HasTrait(c, TraitDualWield);

        // LoadEquip: EquipItem per char_equip row in slot order (what fails stays out of c.Equip), then the unarmed
        // weapon when no main-hand row exists. LoginPackets.OnLogin sends what the queued changes would, so they are
        // dropped here. A linkshell row (slots 16, 17) is bound as it is (bindEquip), unless the item is busy; joining
        // the shell is ZoneHost.OnLogin's.
        public void EquipAtLogin(CharData c)
        {
            var rows = c.Equip.ToArray();
            Array.Sort(rows, (a, b) => a.EquipSlot.CompareTo(b.EquipSlot));
            c.Equip.Clear();
            bool hasMainRow = false;
            var links = new List<EquipEntry>();
            foreach (var e in rows)
            {
                if (e.EquipSlot >= 16)
                {
                    links.Add(e);
                    continue;
                }
                if (e.EquipSlot == SlotMain) hasMainRow = true;
                EquipItem(c, e.InvSlot, e.EquipSlot, e.Container);
            }
            foreach (var e in links)
            {
                var item = c.FindItem(e.Container, e.InvSlot);
                if (!LinkshellItem.Is(item) || item.InTransaction || item.InBazaar || IsEquipped(c, item)) continue;
                c.Equip.Add(e);
            }
            if (!hasMainRow) CheckUnarmedWeapon(c);
            c.EquipChanges.Clear();
            c.DirtyContainers.Clear();
            c.UpdateMask = 0;
            c.EquipDirty = false;
        }

        // the item in an equipment slot (getEquip), and whether an item is in one (its Equipped state, so busy)
        public static Item EquippedItem(CharData c, int slot)
        {
            foreach (var e in c.Equip)
                if (e.EquipSlot == slot) return c.FindItem(e.Container, e.InvSlot);
            return null;
        }

        public static bool IsEquipped(CharData c, Item item) => c.Equip.Exists(e => e.Container == item.Location && e.InvSlot == item.Slot);

        // charutils::EquipItem: slotId 0 unequips the slot. `battleMessage` gets (param, MsgBasic) for the sub-slot
        // refusals. The item's mods and pet mods are its own (augments included) once augmented (ItemData.EquipmentOf).
        // No weaponskill state or Aftermath yet.
        public void EquipItem(CharData c, byte slotId, byte equipSlot, Container container, Action<int, ushort> battleMessage = null)
        {
            if ((int)container >= CharData.MaxContainers) return;

            var item = c.FindItem(container, slotId);
            var info = item == null ? null : items_.EquipmentOf(item);
            if (info == null) item = null;

            if (item != null && item == EquippedItem(c, equipSlot)) return;

            // the same item going into the other slot of a pair
            if (slotId > 0)
            {
                int paired = equipSlot switch
                {
                    SlotMain => SlotSub, SlotSub => SlotMain, SlotEar1 => SlotEar2, SlotEar2 => SlotEar1,
                    SlotRing1 => SlotRing2, SlotRing2 => SlotRing1, _ => -1,
                };
                if (paired >= 0 && item == EquippedItem(c, paired)) return;
            }

            // no changing the ranged weapon during a ranged attack (a short-delay crossbow swapped for a longbow)
            if ((equipSlot == SlotRanged || (equipSlot == SlotAmmo && c.Equipped[SlotRanged] == null)) && c.RangedState != null) return;
            // nor the weapons or ammo during a weapon skill
            if ((equipSlot == SlotMain || equipSlot == SlotSub || equipSlot == SlotRanged || equipSlot == SlotAmmo) && c.InWeaponSkill) return;

            if (equipSlot == SlotSub && info != null && !info.IsShield)
            {
                var weapon = info.Weapon;
                var main = c.Equipped[SlotMain]?.Weapon;

                if (weapon != null && weapon.Skill == Skill.None && (main == null || !main.TwoHanded))
                {
                    battleMessage?.Invoke(0, MsgRequires2HForGrip);
                    return;
                }

                if (weapon != null && weapon.Skill != Skill.None)
                {
                    if (!HasDualWield(c))
                    {
                        battleMessage?.Invoke(info.ItemId, MsgNeedDualWield);
                        return;
                    }
                    // no sub weapon under a two-handed main or without one
                    if (main == null || main.TwoHanded) return;
                }

                // only a shield goes with hand-to-hand
                if (main != null && main.HandToHand) return;
            }

            bool equipSucceeded = false;

            if (slotId == 0)
            {
                var sub = c.Equipped[SlotSub];
                UnequipItem(c, equipSlot);
                if (equipSlot == SlotMain && sub != null && !sub.IsShield) RemoveSub(c);
            }
            else if (item != null && !item.InTransaction && !item.InBazaar && !IsEquipped(c, item) && EquipArmor(c, item, info, equipSlot))
            {
                equipSucceeded = true;
                if (equipSlot == SlotSub && c.Equipped[SlotMain] == null) CheckUnarmedWeapon(c);
                AddEquipModifiers(c, info, +1);
                c.Latents.AddLatentEffects(info.Latents, info.ReqLvl, equipSlot);
                c.Latents.CheckLatentsEquip(equipSlot);
                foreach (var (mod, petType, value) in info.PetMods) ZoneHost.AddPetModifier(c, mod, petType, value);
                // the item script's onItemEquip, only for an equip that passed the checks
                ItemEquipScript?.Invoke(c, item, true);
                QueueEquipChange(c, container, slotId, equipSlot, item, true);
                // a charged item's use delay counts from when it was equipped; it goes on the item recast list
                if (info.Charged && item.Extra[1] != 0)
                {
                    item.AssignUnixSec = UnixNow();
                    ChargedItemEquipChanged?.Invoke(c, item, true);
                }
            }

            if (equipSlot == SlotMain || equipSlot == SlotRanged || equipSlot == SlotSub)
            {
                // swapping instruments keeps TP
                bool isRangedInstrument = c.Equipped[equipSlot]?.Weapon?.Instrument == true;
                if (equipSucceeded && !isRangedInstrument) c.Tp = 0;
                if (c.Equipped[SlotMain] == null || c.MainWeapon == WeaponInfo.UnarmedH2H) CheckUnarmedWeapon(c);
                BuildWeaponSkills(c);
            }

            BuildSkillsTable(c);
            UpdateHealth(c);
            c.UpdateMask |= UpdateHp | UpdateLook;
            if (MountUtils.IsPersonalChocobo(c)) PersonalChocoboGearChanged?.Invoke(c);
            c.EquipDirty = true;
        }

        // enums/msg_basic.h
        public const ushort MsgNeedDualWield = 39, MsgRequires2HForGrip = 512;

        // updatemask bits (base_entity.h)
        public const byte UpdatePos = 0x01, UpdateStatus = 0x02, UpdateHp = 0x04, UpdateLook = 0x10;

        // charutils::UnequipItem
        public void UnequipItem(CharData c, int slot, bool recalculate = true, bool isDelevel = false)
        {
            if (slot > 15) return;
            var info = c.Equipped[slot];
            if (info == null) return;
            var item = EquippedItem(c, slot);

            // the armor slots the item covered (rslotlook over rslot) show what is equipped in them again
            int removeSlots = info.RemoveSlotLook > 0 ? info.RemoveSlotLook : info.RemoveSlots;
            for (int i = StyleLock.SlotHead; i <= StyleLock.SlotFeet; i++)
                if ((removeSlots & 1 << i) != 0) StyleLock.SetArmor(c.Look, i, c.Equipped[i]?.ModelId ?? 0);

            // a charged item leaves the item recast list, whatever bag it is in
            if (info.Charged && item != null) ChargedItemEquipChanged?.Invoke(c, item, false);

            // clearEquip
            c.Equipped[slot] = null;
            c.Equip.RemoveAll(x => x.EquipSlot == slot);

            if (slot == SlotSub)
            {
                // with the main hand empty, maybe fists now
                if (c.Equipped[SlotMain] == null) CheckUnarmedWeapon(c);
                c.DualWield = false;
            }
            AddEquipModifiers(c, info, -1, isDelevel);
            c.Latents.DelLatentEffects(info.ReqLvl, (byte)slot);
            foreach (var (mod, petType, value) in info.PetMods) ZoneHost.AddPetModifier(c, mod, petType, (short)-value);

            switch (slot)
            {
                case SlotSub:
                    c.Look.Sub = 0;
                    c.SubWeapon = WeaponInfo.Unarmed; // "nothing" in the sub slot, against the multi attack exploit
                    c.Tp = 0;
                    BuildWeaponSkills(c);
                    StyleLock.UpdateWeaponStyle(c, items_, slot, null);
                    break;
                case SlotAmmo:
                    if (c.Equipped[SlotRanged] == null) c.Look.Ranged = 0;
                    c.AmmoWeapon = null;
                    StyleLock.UpdateWeaponStyle(c, items_, slot, null);
                    break;
                case SlotRanged:
                    c.Look.Ranged = 0;
                    c.RangedWeapon = null;
                    // the incoming item decides whether an outgoing instrument loses TP
                    if (recalculate || info.Weapon?.Instrument != true) c.Tp = 0;
                    BuildWeaponSkills(c);
                    StyleLock.UpdateWeaponStyle(c, items_, slot, null);
                    break;
                case SlotMain:
                    if (info.Weapon != null && (info.Weapon.HandToHand || c.Equipped[SlotSub] == null)) c.Look.Sub = 0;
                    CheckUnarmedWeapon(c);
                    c.Tp = 0;
                    BuildWeaponSkills(c);
                    StyleLock.UpdateWeaponStyle(c, items_, slot, null);
                    break;
                default:
                    StyleLock.SetArmor(c.Look, slot, 0);
                    break;
            }

            if (item != null)
            {
                ItemEquipScript?.Invoke(c, item, false);
                QueueEquipChange(c, Container.Inventory, 0, slot, item, false);
            }
            c.EquipDirty = true;

            if (recalculate)
            {
                BuildSkillsTable(c);
                UpdateHealth(c);
                c.UpdateMask |= UpdateHp | UpdateLook;
            }
            if (MountUtils.IsPersonalChocobo(c)) PersonalChocoboGearChanged?.Invoke(c);
        }

        private void RemoveSub(CharData c)
        {
            if (c.Equipped[SlotSub] != null) UnequipItem(c, SlotSub);
        }

        // InventorySyncState::queueEquipChange: an unequip dirties the container the item is in
        private static void QueueEquipChange(CharData c, Container container, byte containerSlot, int equipSlot, Item item, bool equipping)
        {
            c.EquipChanges.Add(new EquipChange(container, containerSlot, (byte)equipSlot, item, equipping));
            c.DirtyContainers.Add(equipping ? container : item.Location);
        }

        // ranged weapon and ammo of different skills (or sub skills, except arrows for either bow) do not go together
        private static bool AmmoMismatch(WeaponInfo item, WeaponInfo other) =>
            item.Skill != other.Skill || other.Skill != Skill.Archery && item.SubSkill != other.SubSkill;

        // charutils::EquipArmor
        private bool EquipArmor(CharData c, Item item, EquipmentInfo info, int slot)
        {
            int reqLvlCap = settings_.DisableGearScaling ? c.MLvl : c.JobLevels[c.MJob];
            if ((c.EquipBlock & 1 << slot) != 0 || (info.Jobs & 1u << (c.MJob - 1)) == 0 || info.SuperiorLevel > c.GetMod(Mod.SUPERIOR_LEVEL) || info.ReqLvl > reqLvlCap ||
                !info.EquippableByRace(c.Look.Race))
                return false;

            UnequipItem(c, slot, false);

            if ((info.EquipSlots & 1 << slot) == 0) return false;

            // the slots this item covers (it shows in the armor ones), then whatever covers this slot (a cloak when a
            // head piece goes on)
            for (int i = 0; i < 16; i++)
            {
                if ((info.RemoveSlots & 1 << i) == 0) continue;
                UnequipItem(c, i, false);
                StyleLock.SetArmor(c.Look, i, info.ModelId);
            }
            for (int i = 0; i < SlotBack; i++)
                if (c.Equipped[i] is EquipmentInfo armor && (armor.RemoveSlots & info.EquipSlots) != 0) UnequipItem(c, i, false);

            switch (slot)
            {
                case SlotMain:
                    if (info.Weapon != null)
                    {
                        if (info.Weapon.TwoHanded || info.Weapon.HandToHand)
                        {
                            // a two-handed main keeps a grip; hand-to-hand keeps nothing
                            var sub = c.Equipped[SlotSub];
                            if (sub != null && (sub.Weapon == null || sub.Weapon.Skill != Skill.None || info.Weapon.HandToHand))
                                UnequipItem(c, SlotSub, false);
                            if (info.Weapon.HandToHand) c.Look.Sub = (ushort)(info.ModelId + 0x1000);
                        }
                        else if (c.Equipped[SlotSub]?.Weapon is WeaponInfo subWeapon && subWeapon.Skill == Skill.None)
                        {
                            // one-handed main: keep a dual-wield weapon or shield, but drop a grip
                            UnequipItem(c, SlotSub, false);
                        }
                        c.MainWeapon = info.Weapon;
                    }
                    c.Look.Main = info.ModelId;
                    StyleLock.UpdateWeaponStyle(c, items_, slot, info);
                    break;

                case SlotSub:
                {
                    var main = c.Equipped[SlotMain]?.Weapon;
                    if (main == null)
                    {
                        if (!info.IsShield) return false;
                    }
                    else
                    {
                        switch (main.Skill)
                        {
                            case Skill.HandToHand:
                                if (info.Weapon == null) UnequipItem(c, SlotMain, false);
                                break;
                            case Skill.Dagger:
                            case Skill.Sword:
                            case Skill.Axe:
                            case Skill.Katana:
                            case Skill.Club:
                                if (info.Weapon != null && (!HasDualWield(c) || info.Weapon.Skill == Skill.None)) return false;
                                c.SubWeapon = info.Weapon;
                                // only a weapon (not a shield) is dual wielding
                                if (info.Weapon != null) c.DualWield = true;
                                break;
                            default:
                                if (info.Weapon == null) UnequipItem(c, SlotMain, false);
                                else if (info.Weapon.Skill != Skill.None) return false; // only grips
                                break;
                        }
                    }
                    c.Look.Sub = info.ModelId;
                    StyleLock.UpdateWeaponStyle(c, items_, slot, info);
                    break;
                }

                case SlotRanged:
                    if (info.Weapon != null && c.Equipped[SlotAmmo]?.Weapon is WeaponInfo ammo && AmmoMismatch(info.Weapon, ammo))
                        UnequipItem(c, SlotAmmo, false);
                    if (info.Weapon != null) c.RangedWeapon = info.Weapon;
                    c.Look.Ranged = info.ModelId;
                    StyleLock.UpdateWeaponStyle(c, items_, slot, info);
                    break;

                case SlotAmmo:
                    if (info.Weapon != null)
                    {
                        if (c.Equipped[SlotRanged]?.Weapon is WeaponInfo ranged && AmmoMismatch(info.Weapon, ranged))
                            UnequipItem(c, SlotRanged, false);
                        if (c.Equipped[SlotRanged] == null) c.Look.Ranged = info.ModelId;
                        c.AmmoWeapon = info.Weapon;
                        StyleLock.UpdateWeaponStyle(c, items_, slot, info);
                    }
                    break;

                default:
                    StyleLock.SetArmor(c.Look, slot, info.ModelId);
                    break;
            }

            // bindEquip
            c.Equipped[slot] = info;
            c.Equip.RemoveAll(x => x.EquipSlot == slot);
            int at = c.Equip.FindIndex(x => x.EquipSlot > slot);
            var entry = new EquipEntry { EquipSlot = (byte)slot, InvSlot = item.Slot, Container = item.Location };
            if (at < 0) c.Equip.Add(entry);
            else c.Equip.Insert(at, entry);

            if (slot >= StyleLock.SlotHead && slot <= StyleLock.SlotFeet) UpdateRemovedSlotsLook(c);
            return true;
        }

        // charutils::UpdateRemovedSlotsLook: head..legs armor with an rslotlook shows its model over those slots
        private static void UpdateRemovedSlotsLook(CharData c)
        {
            for (int i = StyleLock.SlotHead; i < StyleLock.SlotFeet; i++)
            {
                var armor = c.Equipped[i];
                if (armor == null || armor.RemoveSlotLook == 0) continue;
                for (int j = StyleLock.SlotHead; j <= StyleLock.SlotFeet; j++)
                    if ((armor.RemoveSlotLook & 1 << j) != 0) StyleLock.SetArmor(c.Look, j, armor.ModelId);
            }
        }

        // charutils::RemoveAllEquipment: every equipment slot emptied, then the unarmed weapon and the weapon skills
        public void RemoveAllEquipment(CharData c)
        {
            for (int slot = 0; slot < 16; slot++)
                if (c.Equipped[slot] != null) UnequipItem(c, slot);
            CheckUnarmedWeapon(c);
            BuildWeaponSkills(c);
        }

        // CheckUnarmedWeapon: fists when the main or sub job has H2H skill and nothing is in the sub slot; for a
        // Monipulator, when its species is a MNK
        public void CheckUnarmedWeapon(CharData c)
        {
            bool fists = c.Monstrosity != null ? c.Monstrosity.MainJob == Monstrosity.JobMnk : (growth_.GetSkillRank(Skill.HandToHand, c.MJob) > 0 || growth_.GetSkillRank(Skill.HandToHand, c.SJob) > 0) &&
                         c.Equipped[SlotSub] == null;
            c.MainWeapon = fists ? WeaponInfo.UnarmedH2H : WeaponInfo.Unarmed;
            c.Look.Main = (ushort)(fists ? 21 : 0);
            BuildWeaponSkills(c);
        }

        // ---- weapon skills (charutils::BuildingCharWeaponSkills, CanUseWeaponskill, the learned weapon skills) ----

        public WeaponSkillTable WeaponSkills => items_.WeaponSkills;

        // charutils::hasWeaponSkill / addWeaponSkill: the m_WeaponSkills bit the COMMAND_DATA packet carries
        public static bool HasWeaponSkill(CharData c, int id) => id >= 0 && id / 8 < c.WeaponSkills.Length && (c.WeaponSkills[id / 8] & 1 << (id % 8)) != 0;

        public static void AddWeaponSkill(CharData c, int id)
        {
            if (id >= 0 && id / 8 < c.WeaponSkills.Length) c.WeaponSkills[id / 8] |= (byte)(1 << (id % 8));
        }

        // charutils::hasLearnedWeaponskill: m_LearnedWeaponskills is 64 bits
        public static bool HasLearnedWeaponskill(CharData c, int unlockId) =>
            unlockId >= 0 && unlockId < 64 && (c.LearnedWeaponskills[unlockId / 8] & 1 << (unlockId % 8)) != 0;

        // battleutils::CanUseWeaponskill: the skill level reached (and the unlock learned), or a 0-level skill learned at
        // 75; on the main job, or the sub job when not main-only
        public static bool CanUseWeaponskill(CharData c, WeaponSkill ws)
        {
            bool skillOk = ws.SkillLevel > 0 && GetSkill(c, ws.Type) >= ws.SkillLevel && (ws.UnlockId == 0 || HasLearnedWeaponskill(c, ws.UnlockId)) ||
                           ws.SkillLevel == 0 && (ws.UnlockId == 0 || HasLearnedWeaponskill(c, ws.UnlockId) && c.MLvl >= 75);
            return skillOk && (ws.GetJob(c.MJob) > 0 || ws.GetJob(c.SJob) > 0 && !ws.MainOnly);
        }

        // charutils::canUseWeaponSkill: only the skill level
        public bool CanUseWeaponSkillLevel(CharData c, int wsid)
        {
            var ws = WeaponSkills.Get(wsid);
            return ws != null && GetSkill(c, ws.Type) >= ws.SkillLevel;
        }

        // BuildingCharWeaponSkills: the main weapon's list (hand-to-hand bare handed) and the ranged weapon's (not
        // throwing), plus what a broken (or ordinary) weapon's ADDS_WEAPONSKILL grants
        public void BuildWeaponSkills(CharData c)
        {
            Array.Clear(c.WeaponSkills);
            var table = WeaponSkills;
            if (table == null) return;

            int mainWs = 0, rangeWs = 0;
            foreach (int slot in new[] { SlotMain, SlotRanged })
            {
                var weapon = WeaponIn(c, slot);
                if (weapon?.Equip == null) continue; // the unarmed weapons carry no mods
                var item = EquippedItem(c, slot);
                if (!weapon.IsUnlockable(item) || weapon.IsUnlocked(item))
                {
                    int adds = ScaledItemModifier(c, weapon.Equip, Mod.ADDS_WEAPONSKILL);
                    if (slot == SlotMain) mainWs = adds;
                    else rangeWs = adds;
                }
            }

            int skill = c.Equipped[SlotMain]?.Weapon is WeaponInfo main ? main.Skill : Skill.HandToHand;
            foreach (var ws in table.ForSkill(skill))
                if (CanUseWeaponskill(c, ws) || ws.Id == mainWs) AddWeaponSkill(c, ws.Id);

            if (c.Equipped[SlotRanged]?.Weapon is WeaponInfo ranged && ranged.Skill != Skill.Throwing)
                foreach (var ws in table.ForSkill(ranged.Skill))
                    if (CanUseWeaponskill(c, ws) || ws.Id == rangeWs) AddWeaponSkill(c, ws.Id);
        }

        // charutils::CheckValidEquipment: what the job or level no longer allows comes off (a delevel takes the mods
        // off at the level they went on), then the fists and the weapon skills
        public void CheckValidEquipment(CharData c, bool isDelevel = false)
        {
            for (int slot = 0; slot < 16; slot++)
            {
                var info = c.Equipped[slot];
                if (info == null) continue;
                if (info.ReqLvl > (settings_.DisableGearScaling ? c.MLvl : c.JobLevels[c.MJob]))
                {
                    UnequipItem(c, slot, true, isDelevel);
                    continue;
                }
                if (slot == SlotSub && !info.IsShield)
                {
                    // no main weapon, or a sub weapon (not a grip) without dual wield
                    if (c.Equipped[SlotMain] == null || !HasTrait(c, TraitDualWield) && info.Weapon?.Skill != Skill.None)
                    {
                        UnequipItem(c, SlotSub, true, isDelevel);
                        continue;
                    }
                }
                if ((info.Jobs & 1u << (c.MJob - 1)) != 0 && (info.EquipSlots & 1 << slot) != 0) continue;
                UnequipItem(c, slot, true, isDelevel);
            }
            if (c.Equipped[SlotMain] == null || c.MainWeapon == WeaponInfo.UnarmedH2H) CheckUnarmedWeapon(c);
            BuildWeaponSkills(c);
        }

        // charutils::RemoveAllEquipMods / ApplyAllEquipMods: the worn gear's mods out and back in around a level change,
        // which rescales them, and the latents of the gear the char is at the level of
        public void RemoveAllEquipMods(CharData c)
        {
            for (byte slot = 0; slot < 16; slot++)
            {
                if (c.Equipped[slot] is not EquipmentInfo item) continue;
                AddEquipModifiers(c, item, -1);
                if (item.ReqLvl <= c.MLvl)
                {
                    c.Latents.DelLatentEffects(item.ReqLvl, slot);
                    c.Latents.CheckLatentsEquip(slot);
                }
            }
        }

        public void ApplyAllEquipMods(CharData c)
        {
            for (byte slot = 0; slot < 16; slot++)
            {
                if (c.Equipped[slot] is not EquipmentInfo item) continue;
                AddEquipModifiers(c, item, +1);
                if (item.ReqLvl <= c.MLvl)
                {
                    c.Latents.AddLatentEffects(item.Latents, item.ReqLvl, slot);
                    c.Latents.CheckLatentsEquip(slot);
                }
            }
        }

        // addEquipModifiers: every mod row adds the item's (scaled) total for that mod
        private static void AddEquipModifiers(CharData c, EquipmentInfo item, int sign, bool isDelevel = false)
        {
            foreach (var m in item.Mods)
                c.ModStat[(int)m.Mod] = (short)(c.ModStat[(int)m.Mod] + sign * ScaledItemModifier(c, item, m.Mod, isDelevel));
        }

        // battleutils::GetScaledItemModifier: gear above the main job level gives a fraction of some stats, none of the
        // rest. A delevel has already lowered the level, so it scales at the one before.
        public static int ScaledItemModifier(CharData c, EquipmentInfo item, Mod mod, bool isDelevel = false)
        {
            int level = isDelevel ? c.MLvl + 1 : c.MLvl;
            if (level >= item.ReqLvl) return item.GetModifier(mod);
            short amount = item.GetModifier(mod);
            switch (mod)
            {
                case Mod.DEF:
                case Mod.MAIN_DMG_RATING:
                case Mod.SUB_DMG_RATING:
                case Mod.RANGED_DMG_RATING:
                    amount = (short)(amount * 3);
                    amount = (short)(amount / 4);
                    break;
                case Mod.HP:
                case Mod.MP:
                    amount = (short)(amount / 2);
                    break;
                case Mod.STR: case Mod.DEX: case Mod.VIT: case Mod.AGI: case Mod.INT: case Mod.MND: case Mod.CHR:
                case Mod.ATT: case Mod.RATT: case Mod.ACC: case Mod.RACC: case Mod.MATT: case Mod.MACC:
                    amount = (short)(amount / 3);
                    break;
                default:
                    amount = 0;
                    break;
            }
            return amount / item.ReqLvl;
        }

        // ---- skills (charutils::BuildingCharSkillsTable, without an automaton) ----

        // the xi::StatusEffect ids of the scholar's arts
        private const ushort FxLightArts = 358, FxDarkArts = 359, FxAddendumWhite = 401, FxAddendumBlack = 402;

        // charutils::isArtsBonusActive: Light Arts (or Addendum: White) lift divine through enfeebling magic, Dark Arts
        // (or Addendum: Black) enfeebling through dark magic
        public static bool IsArtsBonusActive(CharData c, int skill) =>
            skill >= Skill.DivineMagic && skill <= Skill.EnfeeblingMagic && c.StatusEffects.HasStatusEffect(FxLightArts, FxAddendumWhite) ||
            skill >= Skill.EnfeeblingMagic && skill <= Skill.DarkMagic && c.StatusEffects.HasStatusEffect(FxDarkArts, FxAddendumBlack);

        // charutils::ArtsBonusSkill: below the E cap the skill is raised to the level's arts baseline, below the D cap
        // by the B+ cap less the D cap, below the B+ cap up to it; then LIGHT_ARTS_SKILL or DARK_ARTS_SKILL
        public short ArtsBonusSkill(CharData c, int skill)
        {
            int bonus = 0;
            int maxMain = growth_.GetMaxSkill(skill, c.MJob, c.MLvl), maxSub = growth_.GetMaxSkill(skill, c.SJob, c.SLvl);
            int artsSkill = growth_.GetMaxSkill(Skill.EnhancingMagic, MobStats.JobRdm, c.MLvl);  // B+ skill
            int skillCapD = growth_.GetMaxSkill(skill, MobStats.JobSch, c.MLvl);                  // D skill cap
            int skillCapE = growth_.GetMaxSkill(Skill.DarkMagic, MobStats.JobRdm, c.MLvl);         // E skill cap
            int current = Math.Clamp(c.RealSkills[skill] / 10, 0, Math.Max(maxMain, maxSub));
            int lvl = c.MLvl;
            int baseline = lvl < 51 ? (ushort)(5 + 2.7 * (lvl - 1))
                : lvl < 61 ? (ushort)(137 + 4.7 * (lvl - 50))
                : lvl < 71 ? (ushort)(184 + 3.7 * (lvl - 60))
                : lvl < 75 ? (ushort)(221 + 5.0 * (lvl - 70))
                : (ushort)(skillCapD + 36);
            if (current < skillCapE) bonus += Math.Max(baseline - current, 0);
            else if (current < skillCapD) bonus += Math.Max(artsSkill - skillCapD, 0);
            else if (current < artsSkill) bonus += Math.Max(artsSkill - current, 0);
            bonus += c.StatusEffects.HasStatusEffect(FxLightArts, FxAddendumWhite) ? c.GetMod(Mod.LIGHT_ARTS_SKILL) : c.GetMod(Mod.DARK_ARTS_SKILL);
            return (short)bonus;
        }

        public void BuildSkillsTable(CharData c)
        {
            for (int i = 1; i < 48; i++)
            {
                if (i >= 13 && i <= 21 || i >= 46 && i <= 47)
                {
                    c.WorkingSkills[i] = 0x8000;
                    continue;
                }
                int maxMain = growth_.GetMaxSkill(i, c.MJob, c.MLvl);
                int maxSub = growth_.GetMaxSkill(i, c.SJob, c.SLvl);
                short bonus = 0;
                // the scholar's arts; else A+ capped down to the automaton's rating
                if (IsArtsBonusActive(c, i)) bonus += ArtsBonusSkill(c, i);
                else if (i >= Skill.AutomatonMelee && i <= Skill.AutomatonMagic) maxMain = growth_.GetMaxSkillByRank(1, c.MLvl);

                // the skill's merit, then the mod (add 79 to get the modifier ID; it can be negative)
                if (c.Merits != null && c.Merits.Table.MeritBySkill.TryGetValue(i, out ushort skillMerit)) bonus += (short)c.Merits.Value(c, skillMerit);
                bonus += c.GetMod((Mod)(i + 79));

                byte mainRank = growth_.GetSkillRank(i, c.MJob), subRank = growth_.GetSkillRank(i, c.SJob);
                c.WorkingSkillRank[i] = mainRank;
                c.RealSkillRank[i] = mainRank != 0 ? mainRank : subRank;

                int current = c.RealSkills[i] / 10;
                int max = maxMain != 0 ? maxMain : maxSub;
                if (max != 0)
                {
                    current = Math.Min(current, max);
                    c.WorkingSkills[i] = (ushort)Math.Max((short)(current + bonus), (short)0);
                    if (current >= max) c.WorkingSkills[i] |= 0x8000; // blue text
                }
                else
                {
                    c.WorkingSkills[i] = (ushort)(Math.Max(bonus, (short)0) | 0x8000);
                }
            }

            // a Monipulator casts like a mob of its species: capped skill from its main job, else its sub job
            if (c.Monstrosity is { } mon)
            {
                var (mainJob, subJob, level) = Monstrosity.SpeciesJobs(mon);
                for (int i = Skill.DivineMagic; i <= Skill.BlueMagic; i++)
                {
                    int max = growth_.GetMaxSkill(i, mainJob, level);
                    if (max == 0) max = growth_.GetMaxSkill(i, subJob, level);
                    c.WorkingSkills[i] = (ushort)Math.Max(max + c.GetMod((Mod)(i + 79)), 0);
                }
                // monstrosity::GetCombatSkill / GetEvasionSkill: retail attack, defence and accuracy fit an A+ skill at
                // the species level, whatever the weapon; evasion the species' main job's
                mon.CombatSkill = growth_.GetMaxSkillByRank(1, c.MLvl);
                mon.EvasionSkill = growth_.GetMaxSkill(Skill.Evasion, mon.MainJob, c.MLvl);
            }

            for (int i = 48; i < 58; i++)
            {
                c.WorkingSkills[i] = (ushort)(c.RealSkills[i] / 10 * 0x20 + c.RealSkillRank[i]);
                if ((c.RealSkillRank[i] + 1) * 100 <= c.RealSkills[i]) c.WorkingSkills[i] += 0x8000;
            }
            for (int i = 58; i < Skill.Max; i++) c.WorkingSkills[i] = 0xFFFF;
        }

        // CBattleEntity::GetSkill
        public static ushort GetSkill(CharData c, int skill) => skill < Skill.Max ? (ushort)(c.WorkingSkills[skill] & 0x7FFF) : (ushort)0;

        // ---- health (CBattleEntity::UpdateHealth): c.MaxHp/MaxMp become GetMaxHP/GetMaxMP ----

        public static void UpdateHealth(BattleEntity c)
        {
            float weakness = (100.0f + c.GetMod(Mod.WEAKNESS_PCT)) / 100.0f;
            float curse = (100.0f + c.GetMod(Mod.CURSE_PCT)) / 100.0f;
            float hppPower = (100.0f + c.GetMod(Mod.HPP)) / 100.0f;
            float mppPower = (100.0f + c.GetMod(Mod.MPP)) / 100.0f;

            int baseHp = (int)MathF.Floor((MathF.Floor((c.BaseMaxHp + c.GetMod(Mod.BASE_HP)) * weakness) + c.GetMod(Mod.HP)) * curse);
            int baseMp = (int)MathF.Floor((MathF.Floor((c.BaseMaxMp + c.GetMod(Mod.BASE_MP)) * weakness) + c.GetMod(Mod.MP)) * curse);

            // the base HP/MP bonus the HP/MP% latents read
            c.LatentHp = baseHp;
            c.LatentMp = baseMp;

            baseHp += c.GetMod(Mod.FOOD_HP);
            baseMp += c.GetMod(Mod.FOOD_MP);

            int convertDiff = c.GetMod(Mod.CONVMPTOHP) - c.GetMod(Mod.CONVHPTOMP);
            int convertHp = 0, convertMp = 0;
            if (convertDiff > 0)
            {
                convertHp = Math.Min(baseMp, convertDiff);
                convertMp = -convertHp;
            }
            else if (convertDiff < 0)
            {
                // -1 so the result is never zero HP
                convertMp = Math.Min(baseHp - 1, -convertDiff);
                convertHp = -convertMp;
            }

            baseHp = (int)MathF.Floor((baseHp + convertHp) * hppPower);
            baseMp = (int)MathF.Floor((baseMp + convertMp) * mppPower);

            int foodHp = Math.Min((short)(baseHp * c.GetMod(Mod.FOOD_HPP) / 100), c.GetMod(Mod.FOOD_HP_CAP));
            int foodMp = Math.Min((short)(baseMp * c.GetMod(Mod.FOOD_MPP) / 100), c.GetMod(Mod.FOOD_MP_CAP));

            // only a char is capped at 9999; a mob keeps what it computes
            c.MaxHp = c is CharData ? Math.Clamp(baseHp + foodHp, 1, 9999) : baseHp + foodHp;
            c.MaxMp = c is CharData ? Math.Clamp(baseMp + foodMp, 0, 9999) : baseMp + foodMp;
            c.Hp = Math.Clamp(c.Hp, 0, c.MaxHp);
            c.Mp = Math.Clamp(c.Mp, 0, c.MaxMp);
        }

        // ---- the battle stat getters ----

        public static int Str(CharData c)
        {
            int bonus = c.MainWeapon != null && c.MainWeapon.TwoHanded ? c.GetMod(Mod.TWOHAND_STR) : 0;
            return Math.Clamp(c.BaseStats[0] + c.GetMod(Mod.STR) + bonus, 0, 999);
        }

        public static int Dex(CharData c) => Math.Clamp(c.BaseStats[1] + c.GetMod(Mod.DEX), 0, 999);
        public static int Vit(CharData c) => Math.Clamp(c.BaseStats[2] + c.GetMod(Mod.VIT), 0, 999);
        public static int Agi(CharData c) => Math.Clamp(c.BaseStats[3] + c.GetMod(Mod.AGI), 0, 999);

        // m_Weapons[slot] as a CItemWeapon
        public static WeaponInfo WeaponIn(CharData c, int slot) => slot switch
        {
            SlotMain => c.MainWeapon,
            SlotSub => c.SubWeapon,
            SlotRanged => c.RangedWeapon,
            SlotAmmo => c.AmmoWeapon,
            _ => null,
        };

        public int AttMain(CharData c) => Att(c, SlotMain);

        // CBattleEntity::ATT for a character (no Endark); a Monipulator's fists and A+ combat skill
        public int Att(CharData c, int slot)
        {
            int att = 8 + c.GetMod(Mod.ATT);
            int attp = c.GetMod(Mod.ATTP);
            var weapon = WeaponIn(c, slot);
            float strMultiplier = c.Monstrosity != null ? 0.75f // Monipulators use the fist multiplier whatever their species
                : weapon != null && weapon.TwoHanded ? settings_.TwoHandedStrAttack
                : weapon != null && weapon.HandToHand ? settings_.HandToHandStrAttack
                : slot == SlotMain ? settings_.OneHandMainStrAttack
                : slot == SlotSub ? settings_.OneHandOffStrAttack
                : slot == SlotRanged || slot == SlotAmmo ? settings_.RangedStrAttack
                : 0.5f;
            att = (int)(att + Str(c) * strMultiplier);
            if (c.StatusEffects.HasStatusEffect(EffectEndark)) att += c.GetMod(Mod.ENSPELL_DMG);
            if (c.Monstrosity != null) att += c.Monstrosity.CombatSkill;
            else if (weapon != null)
            {
                att += GetSkill(c, weapon.Skill) + weapon.IlvlSkill;
                // Smite (256 scale) raises attack with 2H or H2H weapons
                if (weapon.TwoHanded || weapon.HandToHand) attp += (int)(c.GetMod(Mod.SMITE) / 256.0f * 100);
            }
            return Math.Max(1, att + att * attp / 100 + Math.Min((short)(att * c.GetMod(Mod.FOOD_ATTP) / 100), c.GetMod(Mod.FOOD_ATT_CAP)));
        }

        // GetAccFromSkill: skill past 200, 400 and 600 counts for less
        private const ushort EffectEnlight = 274, EffectEndark = 288, EffectBarrage = 73, EffectVelocityShot = 371;
        private const ushort MeritAccuracy = 2326;

        internal static int AccFromSkill(int skill)
        {
            if (skill > 600) return (int)MathF.Floor((skill - 600f) * 0.9f) + 540;
            if (skill > 400) return (int)MathF.Floor((skill - 400f) * 0.8f) + 380;
            if (skill > 200) return (int)MathF.Floor((skill - 200f) * 0.9f) + 200;
            return skill;
        }

        // CBattleEntity::ACC for a character: attack 0 the main hand, 1 the off hand, 2 a kick. No Enlight, Tandem,
        // Accuracy merits. A Monipulator's accuracy is its A+ combat skill.
        public int Acc(CharData c, int attackNumber, int offsetAccuracy)
        {
            float dexMultiplier = 0.5f;
            int skill = Skill.None, ilvlSkill = 0;
            var main = c.MainWeapon;
            if (attackNumber == 0)
            {
                dexMultiplier = settings_.OneHandMainDexAccuracy;
                if (main != null)
                {
                    skill = main.Skill;
                    ilvlSkill = main.IlvlSkill;
                    if ((skill == Skill.None && GetSkill(c, Skill.HandToHand) > 0) || main.HandToHand)
                    {
                        skill = Skill.HandToHand;
                        dexMultiplier = settings_.HandToHandDexAccuracy;
                    }
                }
            }
            else if (attackNumber == 1)
            {
                if (c.SubWeapon is WeaponInfo sub)
                {
                    dexMultiplier = settings_.OneHandOffDexAccuracy;
                    skill = sub.Skill;
                    ilvlSkill = sub.IlvlSkill;
                    if (skill == Skill.None && GetSkill(c, Skill.HandToHand) > 0 &&
                        main != null && (main.Skill == Skill.None || main.Skill == Skill.HandToHand))
                    {
                        skill = Skill.HandToHand;
                        dexMultiplier = settings_.HandToHandDexAccuracy;
                    }
                }
                else if (main != null && main.HandToHand)
                {
                    ilvlSkill = main.IlvlSkill;
                    skill = Skill.HandToHand;
                    dexMultiplier = settings_.HandToHandDexAccuracy;
                }
            }
            else if (attackNumber == 2)
            {
                if (main != null) ilvlSkill = main.IlvlSkill;
                skill = Skill.HandToHand;
                dexMultiplier = settings_.HandToHandDexAccuracy;
            }

            int acc;
            if (c.Monstrosity != null)
            {
                // fitted to retail Monipulator accuracy
                dexMultiplier = 0.75f;
                acc = AccFromSkill(c.Monstrosity.CombatSkill);
            }
            else acc = AccFromSkill(GetSkill(c, skill) + ilvlSkill);
            if (main != null && main.TwoHanded)
            {
                acc += (int)MathF.Floor(Dex(c) * settings_.TwoHandedDexAccuracy);
                acc += c.GetMod(Mod.TWOHAND_ACC);
            }
            else acc += (int)MathF.Floor(Dex(c) * dexMultiplier);
            acc = acc + c.GetMod(Mod.ACC) + offsetAccuracy;
            // Enlight's potency, Tandem Strike with the pet on the same target, the Accuracy merit
            if (c.StatusEffects.HasStatusEffect(EffectEnlight)) acc += c.GetMod(Mod.ENSPELL_DMG);
            if (Melee.IsTandemActive(c)) acc += c.GetMod(Mod.TANDEM_STRIKE_POWER);
            acc += c.Merits?.Value(c, MeritAccuracy) ?? 0;
            acc += Math.Min((short)(acc * c.GetMod(Mod.FOOD_ACCP) / 100f), c.GetMod(Mod.FOOD_ACC_CAP));
            return Math.Max((short)0, (short)acc);
        }

        // CBattleEntity::EVA for a character: evasion skill (past 200 at 0.9), the EVA mod, half AGI, EVA_PERCENT
        public static int Eva(CharData c)
        {
            int evasion = c.Monstrosity?.EvasionSkill ?? GetSkill(c, Skill.Evasion);
            if (evasion > 200) evasion = (int)(200 + (evasion - 200) * 0.9);
            evasion += c.GetMod(Mod.EVA);
            evasion += Agi(c) / 2;
            evasion += (int)MathF.Floor((float)evasion * c.GetMod(Mod.EVA_PERCENT) / 100.0f);
            return Math.Clamp(evasion, 1, ushort.MaxValue);
        }

        // the ranged (or else the ammo) weapon RACC and RATT read; 0 when a bow or gun has no ammo, the weapon does no
        // damage, or there is none. As C++, the skill is not counted for a fishing rod.
        private static bool RangedSkill(CharData c, out int skillLevel)
        {
            skillLevel = 0;
            var weapon = c.RangedWeapon;
            if (weapon != null && weapon.Skill != Skill.Throwing && c.AmmoWeapon == null) return false;
            weapon ??= c.AmmoWeapon;
            if (weapon == null || weapon.DmgType == 0 || weapon.Skill == Skill.None) return false;
            if (weapon.Skill != Skill.Fishing) skillLevel = GetSkill(c, weapon.Skill) + weapon.IlvlSkill;
            return true;
        }

        // CBattleEntity::RACC for a character (no Weakness), with Barrage's accuracy
        // (battleutils::GetRangedAccuracyBonuses). The food term keeps C++'s (100 + FOOD_RACCP * RACC) / 100.
        public int Racc(CharData c, int bonusAcc = 0)
        {
            if (!RangedSkill(c, out int skillLevel)) return 0;
            int racc = AccFromSkill(skillLevel);
            racc += c.GetMod(Mod.RACC);
            racc += bonusAcc;
            if (c.StatusEffects.HasStatusEffect(EffectBarrage)) racc += c.GetMod(Mod.BARRAGE_ACC);
            racc += (int)MathF.Floor(Agi(c) * settings_.RangedAgiAccuracy);
            return Math.Max(1, racc + Math.Min((short)((100 + c.GetMod(Mod.FOOD_RACCP) * racc) / 100), c.GetMod(Mod.FOOD_RACC_CAP)));
        }

        // CBattleEntity::RATT for a character (no Weakness), with Velocity Shot's attack
        // (battleutils::GetRangedAttackBonuses)
        public int Ratt(CharData c, int bonusAtt = 0)
        {
            if (!RangedSkill(c, out int skillLevel)) return 0;
            int velocity = c.StatusEffects.HasStatusEffect(EffectVelocityShot) ? c.GetMod(Mod.VELOCITY_RATT_BONUS) : 0;
            int ratt = 8 + skillLevel + bonusAtt + c.GetMod(Mod.RATT) + velocity + (int)Math.Floor(Str(c) * (double)settings_.RangedStrAttack);
            float total = ratt + ratt * c.GetMod(Mod.RATTP) / 100f + Math.Min((short)(ratt * c.GetMod(Mod.FOOD_RATTP) / 100f), c.GetMod(Mod.FOOD_RATT_CAP));
            return Math.Max((short)1, (short)total);
        }

        // CBattleEntity::DEF for a character
        public int Def(CharData c)
        {
            int def = 8 + (int)MathF.Floor(Vit(c) * settings_.VitDefMultiplier);
            int level = c.MLvl;
            if (level < 51) def += level;
            else if (level < 61) def += 2 * level - 42;
            else if (level < 91) def += level + 18;
            else def += level + 18 + (level - 89) / 2;
            // retail Monipulator defence fits an A+ skill on top
            if (c.Monstrosity != null) def += c.Monstrosity.CombatSkill;
            def += c.GetMod(Mod.DEF);
            return CounterstanceDef(c, def, settings_) ?? Math.Max(1, def + def * c.GetMod(Mod.DEFP) / 100 + Math.Min((short)(def * c.GetMod(Mod.FOOD_DEFP) / 100), c.GetMod(Mod.FOOD_DEF_CAP)));
        }

        // CBattleEntity::DEF under Counterstance (sub id 0): half the DEF, or with USE_OLD_COUNTERSTANCE 1 + VIT / 2 plus
        // Minne's power with the DEF % mods (gear and Protect ignored); null without it
        public static int? CounterstanceDef(BattleEntity e, int def, StatSettings settings)
        {
            const ushort EffectCounterstance = 61, EffectMinne = 197;
            if (!e.StatusEffects.HasStatusEffectWithSubId(EffectCounterstance, 0)) return null;
            if (!settings.UseOldCounterstance) return def / 2;
            def = 1 + Melee.Vit(e) / 2;
            if (e.StatusEffects.GetStatusEffect(EffectMinne) is { } minne) def += minne.Power;
            return Math.Max(1, def + def * e.GetMod(Mod.DEFP) / 100 + Math.Min((short)(def * e.GetMod(Mod.FOOD_DEFP) / 100), e.GetMod(Mod.FOOD_DEF_CAP)));
        }

        // ---- item levels (charutils::getMaxItemLevel and friends) ----

        public static byte MaxItemLevel(CharData c)
        {
            byte max = 0;
            for (int i = 0; i < 16; i++)
                if (c.Equipped[i] is EquipmentInfo e && e.ILvl > max) max = e.ILvl;
            return max;
        }

        public static byte ItemLevelDifference(CharData c)
        {
            float diff = 0;
            byte highest = 0;
            for (int i = 0; i < 4; i++)
                if (c.Equipped[i] is EquipmentInfo e && e.ILvl > highest) highest = e.ILvl;
            if (highest > 99) diff += (highest - 99) / 2.0f;
            for (int i = 4; i < 9; i++)
                if (c.Equipped[i] is EquipmentInfo e && e.ILvl > 99) diff += (e.ILvl - 99) / 10.0f;
            return (byte)MathF.Floor(diff);
        }

        public static byte MainhandItemLevel(CharData c) => c.Equipped[SlotMain]?.ILvl ?? 0;
        public static byte RangedItemLevel(CharData c) => c.Equipped[SlotRanged]?.ILvl ?? c.Equipped[SlotAmmo]?.ILvl ?? 0;
    }
}
