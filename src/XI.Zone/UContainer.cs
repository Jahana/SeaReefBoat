using System;

namespace XI.Zone
{
    // UCONTAINERTYPE
    public enum UContainerType : byte
    {
        Empty, Syntes, Fishing, Shop, Trade, UseItem, Auction, SendDeliveryBox, RecvDeliveryBox,
    }

    // A delivery box item: the CItem the delivery box keeps in the universal container. Slot is getSlotID (the box slot
    // when loaded, the inventory slot it came from when set), SubId and Extra are delivery_box's itemsubid and extra.
    public sealed class DboxItem
    {
        public ushort ItemId;
        public ushort SubId;
        public byte Slot;
        public uint Quantity;
        public byte[] Extra = new byte[24];
        public string Sender = "", Receiver = "";
        public bool Sent;
    }

    // CUContainer: the universal general purpose container. Only the delivery box fills it, so a slot holds a DboxItem.
    public sealed class UContainer
    {
        public const int Size = 16; // UCONTAINER_SIZE

        private DboxItem[] items_ = new DboxItem[Size];

        public UContainerType Type { get; private set; }
        public bool IsLocked { get; private set; }
        public ushort Target { get; set; }
        public byte ItemsCount { get; private set; } // m_count: what SetItem counted

        public void Clean()
        {
            Type = UContainerType.Empty;
            IsLocked = false;
            ItemsCount = 0;
            Target = 0;
            items_ = new DboxItem[Size];
        }

        // SetType: a type is only set on an empty container
        public bool SetType(UContainerType type)
        {
            if (Type != UContainerType.Empty) return false;
            Type = type;
            return true;
        }

        public void SetLock() => IsLocked = true;

        public void UnLock() => IsLocked = false;

        public bool IsContainerEmpty => Type == UContainerType.Empty;

        public bool IsSlotEmpty(int slot) => slot < 0 || slot >= items_.Length || items_[slot] == null;

        public DboxItem GetItem(int slot) => slot >= 0 && slot < items_.Length ? items_[slot] : null;

        public bool SetItem(int slot, DboxItem item)
        {
            if (slot < 0 || slot >= items_.Length || IsLocked) return false;
            if (item != null && items_[slot] == null) ItemsCount++;
            if (item == null && items_[slot] != null) ItemsCount--;
            items_[slot] = item;
            return true;
        }

        public void SetSize(int size) => Array.Resize(ref items_, size);
    }
}
