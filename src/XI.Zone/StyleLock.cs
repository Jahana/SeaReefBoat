namespace XI.Zone
{
    // charutils' lockstyle helpers: SetStyleLock, UpdateWeaponStyle / UpdateArmorStyle, UpdateRemovedSlotsLookForLockStyle
    // and the checks a style item has to pass. They write c.MainLook from c.StyleItems; c.Look stays the real gear's.
    public static class StyleLock
    {
        public const int SlotMain = 0, SlotSub = 1, SlotRanged = 2, SlotAmmo = 3,
            SlotHead = 4, SlotBody = 5, SlotHands = 6, SlotLegs = 7, SlotFeet = 8;
        public const int SlotLink1 = 16;
        public const ushort MarvelousCheer = 22283;

        // xi::items::lookup<CItemEquipment>: null for item 0 and for anything that is not equipment
        public static EquipmentInfo Lookup(ItemData items, ushort itemId) => itemId == 0 ? null : items.Equipment(itemId);

        // CItemEquipment::IsShield
        public static bool IsShield(EquipmentInfo e) => e.ShieldSize > 0 && e.ShieldSize <= 6;

        // charutils::canEquipItemOnAnyJob: some job at or above the item's level can wear it (no item counts as yes)
        public static bool CanEquipItemOnAnyJob(CharData c, EquipmentInfo item)
        {
            if (item == null) return true;
            for (int i = 1; i < CharData.MaxJobs; i++)
                if ((item.Jobs & 1u << (i - 1)) != 0 && item.ReqLvl <= c.JobLevels[i]) return true;
            return false;
        }

        // charutils::hasValidStyle: `appearance` may be shown over the equipped `item` -- shield over shield, weapon
        // over a weapon of the same skill, Marvelous Cheer over a wind instrument
        public static bool HasValidStyle(CharData c, EquipmentInfo item, EquipmentInfo appearance)
        {
            if (appearance == null || item == null) return false;
            if (IsShield(appearance) && IsShield(item)) return c.HasItem(appearance.ItemId) && CanEquipItemOnAnyJob(c, appearance);
            if (item.Weapon != null && appearance.ItemId == MarvelousCheer && item.Weapon.Skill == Skill.WindInstrument)
                return c.HasItem(appearance.ItemId);
            if (item.Weapon != null && appearance.Weapon != null && item.Weapon.Skill == appearance.Weapon.Skill)
                return c.HasItem(appearance.ItemId) && CanEquipItemOnAnyJob(c, appearance);
            return false;
        }

        // charutils::SetStyleLock without its message: locking takes the equipped items and the current look as the
        // style. Returns whether the flag changed (the caller sends StyleLockOn/Off then).
        public static bool Set(CharData c, bool locked)
        {
            if (locked)
            {
                for (int i = 0; i < SlotLink1; i++) c.StyleItems[i] = c.Equipped[i]?.ItemId ?? 0;
                c.MainLook.CopyFrom(c.Look);
            }
            else
            {
                System.Array.Clear(c.StyleItems);
            }
            bool changed = c.StyleLocked != locked;
            c.StyleLocked = locked;
            return changed;
        }

        // look.head .. look.feet by equipment slot
        public static void SetArmor(Look look, int slot, ushort model)
        {
            switch (slot)
            {
                case SlotHead: look.Head = model; break;
                case SlotBody: look.Body = model; break;
                case SlotHands: look.Hands = model; break;
                case SlotLegs: look.Legs = model; break;
                case SlotFeet: look.Feet = model; break;
            }
        }

        // charutils::UpdateWeaponStyle: the style weapon's model when it is valid over `item` (what is equipped in the
        // slot), the real look's otherwise
        public static void UpdateWeaponStyle(CharData c, ItemData items, int slot, EquipmentInfo item)
        {
            if (!c.StyleLocked) return;
            var appearance = Lookup(items, c.StyleItems[slot]);
            ushort model = appearance?.ModelId ?? 0;
            Look main = c.MainLook, look = c.Look;
            switch (slot)
            {
                case SlotMain:
                    main.Main = HasValidStyle(c, item, appearance) ? model : look.Main;
                    if (item == null)
                    {
                        main.Sub = look.Sub;
                    }
                    else if (item.Weapon != null)
                    {
                        if (item.Weapon.HandToHand)
                            main.Sub = HasValidStyle(c, item, appearance) ? (ushort)(model + 0x1000) : look.Sub;
                        else if (item.Weapon.TwoHanded)
                            main.Sub = look.Sub;
                    }
                    break;
                case SlotSub:
                    main.Sub = HasValidStyle(c, item, appearance) ? model : look.Sub;
                    break;
                case SlotRanged:
                    main.Ranged = HasValidStyle(c, item, appearance) ? model : look.Ranged;
                    break;
                case SlotAmmo:
                    if (c.Equipped[SlotRanged] == null) main.Ranged = HasValidStyle(c, item, appearance) ? model : look.Ranged;
                    break;
            }
        }

        // charutils::UpdateArmorStyle: the style item's model when the char still owns it, no model when not, and the
        // slot left alone when no job can wear the item
        public static void UpdateArmorStyle(CharData c, ItemData items, int slot)
        {
            if (!c.StyleLocked) return;
            ushort itemId = c.StyleItems[slot];
            var appearance = Lookup(items, itemId);
            ushort model = appearance != null && c.HasItem(itemId) ? appearance.ModelId : (ushort)0;
            if (!CanEquipItemOnAnyJob(c, appearance)) return;
            SetArmor(c.MainLook, slot, model);
        }

        // charutils::UpdateRemovedSlotsLookForLockStyle: a style item covering other armor slots (rslotlook, or rslot
        // without one) shows its own model in them
        public static void UpdateRemovedSlotsLook(CharData c, ItemData items)
        {
            if (!c.StyleLocked) return;
            foreach (ushort itemId in (ushort[])c.StyleItems.Clone())
            {
                var item = Lookup(items, itemId);
                if (item == null) continue;
                int removeSlots = item.RemoveSlotLook != 0 ? item.RemoveSlotLook : item.RemoveSlots;
                for (int i = SlotHead; i <= SlotFeet; i++)
                    if ((removeSlots & 1 << i) != 0) SetArmor(c.MainLook, i, item.ModelId);
            }
        }
    }
}
