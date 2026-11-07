using System.Collections.Generic;
using System.Numerics;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // The CLuaBaseEntity methods xi.player.onGameIn's charCreate (scripts/globals/player.lua) calls that had no
    // binding: equipItem, setGil, setNewPlayer, and getNewPlayer beside it.
    // changeContainerSize and unequipItem are ZoneHost.PlayerBindings'.
    public sealed partial class ZoneHost
    {
        private void RegisterCharCreateBindings(Dictionary<string, LuaFunction> m)
        {
            // CLuaBaseEntity::equipItem(itemID, container = LOC_INVENTORY, equipSlot = the item's slot type): the first
            // slot of the container holding the item, if it is equipment, through charutils::EquipItem
            m["equipItem"] = l =>
            {
                ushort itemId = (ushort)l.ToUIntArg(2);
                if (Pc(l, "equipItem") is not CharData c) return 0;
                byte containerId = l.IsNumber(3) ? (byte)l.ToUIntArg(3) : (byte)Container.Inventory;
                // CCharEntity::getStorage logs an invalid container
                if (containerId >= CharData.MaxContainers)
                {
                    log_($"Unhandled or Invalid Location ID ({containerId}) passed to function.");
                    return 0;
                }
                var stats = loader_.Stats;
                var item = SearchItem(c, (Container)containerId, itemId);
                var info = item == null ? null : stats?.Items.Equipment(item.ItemId);
                if (info == null) return 0;
                byte equipSlot = l.IsNumber(4) ? (byte)l.ToUIntArg(4) : CharCreateSlotType(info.EquipSlots);
                stats.EquipItem(c, item.Slot, equipSlot, (Container)containerId);
                return 0;
            };

            // CLuaBaseEntity::setGil: the gil in inventory slot 0 adjusted to the amount
            m["setGil"] = l =>
            {
                int amount = l.ToIntArg(2);
                if (Pc(l, "setGil") is not CharData c) return 0;
                var gil = Gil(c);
                if (gil == null)
                {
                    log_("lua::setGil : No Gil in currency slot");
                    return 0;
                }
                if (!AdjustGil(c, (int)(amount - (long)gil.Quantity))) log_($"CLuaBaseEntity: could not adjust gil for {c.Name}");
                return 0;
            };

            // CLuaBaseEntity::getNewPlayer: !playerConfig.NewAdventurerOffFlg
            m["getNewPlayer"] = l => Pc(l, "getNewPlayer") is CharData c ? Ret(l, !c.NewAdventurerOffFlg) : Ret(l, false);

            // CLuaBaseEntity::setNewPlayer: NewAdventurerOffFlg = !newplayer, UPDATE_HP, charutils::SavePlayerSettings
            m["setNewPlayer"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("CLuaBaseEntity::setNewPlayer() - m_pBaseEntity is not of type Char.");
                    return 0;
                }
                const uint newAdventurerOffFlg = 1u << 26;
                if (l.ToBoolean(2)) c.Settings &= ~newAdventurerOffFlg;
                else c.Settings |= newAdventurerOffFlg;
                c.UpdateMask |= CharStats.UpdateHp;
                Exec("UPDATE chars SET settings = ? WHERE charid = ? LIMIT 1", BoundValue.Of(c.Settings), BoundValue.Of(c.Id));
                return 0;
            };
        }

        // CItemEquipment::getSlotType: the highest bit of m_equipSlotID (bit_width - 1), 0 for none
        private static byte CharCreateSlotType(ushort equipSlots) =>
            equipSlots == 0 ? (byte)0 : (byte)(31 - BitOperations.LeadingZeroCount(equipSlots));
    }
}
