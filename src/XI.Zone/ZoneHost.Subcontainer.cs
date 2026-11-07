using System;
using XI.Map;
using XI.Persistence;

namespace XI.Zone
{
    public enum SubcontainerKind : uint { Equip = 1, Unequip = 2, UnequipAll = 5 }                       // GP_CLI_COMMAND_SUBCONTAINER_KIND
    public enum SubcontainerIndex : uint { MainWeapon, SubWeapon, RangedWeapon, Head, Body, Hands, Legs, Feet } // ..._CONTAINERINDEX

    // c2s 0x03B SUBCONTAINER: dressing a mannequin (a furnishing in the mog safe) with gear from the storage. The
    // mannequin's exdata (Exdata::Mannequin) holds a storage slot per equipment slot, and those items get the Mannequin
    // lock flag on the client (not saved: they load Normal again).
    public partial class ZoneHost
    {
        private const byte LockFlgMannequin = 0x1B;              // ItemLockFlg::Mannequin
        private const int MannequinEquipOffset = 10;             // Exdata::Mannequin EquipMain; Sub, Ranged, Head .. Feet follow
        private const int MannequinRaceOffset = 18, MannequinPoseOffset = 19;

        // CItem::isMannequin
        public static bool IsMannequin(Item item) => item.ItemId >= 256 && item.ItemId <= 263;

        private void RegisterSubcontainerHandlers()
        {
            Register(0x03B, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent)
                                              .MustEqual(c.MoghouseId, c.Id, "Character not in their mog house")
                                              .OneOf<SubcontainerKind>(BitConverter.ToUInt32(p.Buffer, 4))
                                              .OneOf<SubcontainerIndex>(p.Buffer[13])
                                              .OneOf("Category1", BitConverter.ToUInt32(p.Buffer, 8), SubcontainerContainers(c)),
                     (s, c, p) => OnSubcontainer(s, c, (SubcontainerKind)BitConverter.ToUInt32(p.Buffer, 4), (Container)BitConverter.ToUInt32(p.Buffer, 8),
                                                 p.Buffer[12], p.Buffer[13], BitConverter.ToUInt32(p.Buffer, 16), p.Buffer[20]));
        }

        // validContainers: the safe, and the second floor's once unlocked
        private long[] SubcontainerContainers(CharData c) =>
            (c.MhFlag & 0x20) != 0 && ChatCfg.MogHouse2F ? new[] { (long)Container.MogSafe, (long)Container.MogSafe2 } : new[] { (long)Container.MogSafe };

        private void OnSubcontainer(MapSession s, CharData c, SubcontainerKind kind, Container category1, byte itemIndex1, byte containerIndex,
                                    uint category2, byte itemIndex2)
        {
            var item = StorageItem(c, category1, itemIndex1);
            if (item == null || !IsMannequin(item))
            {
                log_($"GP_CLI_COMMAND_SUBCONTAINER: Unable to load mannequin from slot {itemIndex1} in location {(byte)category1} by {c.Name}");
                return;
            }

            var mannequin = item.Extra;
            int equipAt = MannequinEquipOffset + containerIndex;
            switch (kind)
            {
                case SubcontainerKind.Equip:
                    // only valid for a direct equip / unequip
                    if (category2 != (uint)Container.Storage)
                    {
                        log_($"GP_CLI_COMMAND_SUBCONTAINER: Invalid item location passed to Mannequin Equip packet {category2} by {c.Name}");
                        return;
                    }
                    // the same item again takes it off
                    if (mannequin[equipAt] == itemIndex2)
                    {
                        SetStorageItemStatus(s, c, itemIndex2, 0);
                        mannequin[equipAt] = 0;
                    }
                    else
                    {
                        SetStorageItemStatus(s, c, itemIndex2, LockFlgMannequin);
                        mannequin[equipAt] = itemIndex2;
                    }
                    break;
                case SubcontainerKind.Unequip:
                    SetStorageItemStatus(s, c, itemIndex2, 0);
                    mannequin[equipAt] = 0;
                    break;
                case SubcontainerKind.UnequipAll:
                    for (int i = 0; i < 8; i++)
                    {
                        if (mannequin[MannequinEquipOffset + i] > 0) SetStorageItemStatus(s, c, mannequin[MannequinEquipOffset + i], 0);
                        mannequin[MannequinEquipOffset + i] = 0;
                    }
                    break;
            }

            if (Exec("UPDATE char_inventory SET extra = ? WHERE location = ? AND slot = ? AND charid = ?",
                     BoundValue.Of(item.Extra), BoundValue.Of((byte)category1), BoundValue.Of(itemIndex1), BoundValue.Of(c.Id)) == null)
            {
                log_("GP_CLI_COMMAND_SUBCONTAINER: Problem writing Mannequin to database!");
                return;
            }
            Push(s, GameOkPackets.ItemAttr(item, ItemLockFlg(c, item)));
            Push(s, ItemSubcontainer(c, category1, itemIndex1, item.Extra));
            Push(s, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
        }

        // setStatusOfStorageItemAtSlot: 0x01F for the storage item with the given lock flag
        private void SetStorageItemStatus(MapSession s, CharData c, byte slot, byte lockFlg)
        {
            if (slot == 0) return;
            var item = StorageItem(c, Container.Storage, slot);
            if (item != null) Push(s, LoginPackets.ItemList(item, lockFlg));
        }

        // 0x026 GP_SERV_COMMAND_ITEM_SUBCONTAINER from a mannequin's exdata: each slot's storage item's model, the race and
        // the pose (model_id_race_hair is 1, a TODO in the C++)
        private BasicPacket ItemSubcontainer(CharData c, Container location, byte slot, byte[] mannequin)
        {
            ushort Model(int at)
            {
                byte storageSlot = mannequin[MannequinEquipOffset + at];
                if (storageSlot == 0 || StorageItem(c, Container.Storage, storageSlot) is not Item it) return 0;
                return loader_.Stats?.Items.Equipment(it.ItemId)?.ModelId ?? 0;
            }
            var w = new PacketWriter(0x026, 4 + S2cItemSubContainer.SizeOf);
            const int b = 4 + S2cItemSubContainer.OffModelIdBase;
            w.U8(4 + S2cItemSubContainer.OffIsUsed, 1);
            w.U8(4 + S2cItemSubContainer.OffContainer, (byte)location);
            w.U8(4 + S2cItemSubContainer.OffIndex, slot);
            w.U16(b, 0x01);
            w.U16(b + 2, (ushort)(Model((int)SubcontainerIndex.Head) + 0x1000));
            w.U16(b + 4, (ushort)(Model((int)SubcontainerIndex.Body) + 0x2000));
            w.U16(b + 6, (ushort)(Model((int)SubcontainerIndex.Hands) + 0x3000));
            w.U16(b + 8, (ushort)(Model((int)SubcontainerIndex.Legs) + 0x4000));
            w.U16(b + 10, (ushort)(Model((int)SubcontainerIndex.Feet) + 0x5000));
            w.U16(b + 12, (ushort)(Model((int)SubcontainerIndex.MainWeapon) + 0x6000));
            w.U16(b + 14, (ushort)(Model((int)SubcontainerIndex.SubWeapon) + 0x7000));
            w.U16(b + 16, (ushort)(Model((int)SubcontainerIndex.RangedWeapon) + 0x8000));
            w.U8(4 + S2cItemSubContainer.OffRace, mannequin[MannequinRaceOffset]);
            w.U8(4 + S2cItemSubContainer.OffPose, mannequin[MannequinPoseOffset]);
            return w.Packet;
        }
    }
}
