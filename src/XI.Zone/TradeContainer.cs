using System.Collections.Generic;

namespace XI.Zone
{
    // trade_container.h SlotRestriction: none, JobRestriction {jobId, level} or GuildRestriction {guildId, rank}
    public readonly record struct SlotRestriction(SlotRestrictionKind Kind, byte Id, ushort Value);

    public enum SlotRestrictionKind : byte { None, Job, Guild }

    // CTradeContainer: the char's universal container for exchange, synthesis and shops. Each slot holds an item id, the
    // inventory slot it came from and a quantity (a shop keeps the price there); m_ItemsCount is what setItem counted,
    // not what the slots hold.
    public sealed class TradeContainer
    {
        public const int ContainerSize = 17; // CONTAINER_SIZE

        private readonly List<ushort> itemId_ = new List<ushort>();
        private readonly List<byte> slotId_ = new List<byte>();
        private readonly List<uint> quantity_ = new List<uint>();
        private readonly List<SlotRestriction> restrictions_ = new List<SlotRestriction>();

        public byte Type;                 // m_type: crystal type, store nation, etc.
        public byte ShopFameArea = 0xFF;  // the fame area for shop price ranks
        public uint ShopVendorId;         // the NPC the shop was opened from, for vendor auditing; 0 if unresolved
        public byte ItemsCount;           // m_ItemsCount
        public byte ExSize;               // a custom delineation point inside the container (a shop's item count)

        public TradeContainer() => Clean();

        public ushort GetItemId(int slot) => slot < itemId_.Count ? itemId_[slot] : (ushort)0;
        public byte GetInvSlotId(int slot) => slot < itemId_.Count ? slotId_[slot] : (byte)0xFF;
        public uint GetQuantity(int slot) => slot < itemId_.Count ? quantity_[slot] : 0;
        public SlotRestriction GetRestriction(int slot) => slot < itemId_.Count ? restrictions_[slot] : default;
        public byte Size => (byte)itemId_.Count;

        public void SetItem(int slot, ushort itemId, byte invSlot, uint quantity)
        {
            if (slot >= itemId_.Count) return;
            ItemsCount++;
            itemId_[slot] = itemId;
            slotId_[slot] = invSlot;
            quantity_[slot] = quantity;
        }

        public void SetRestriction(int slot, SlotRestriction restriction)
        {
            if (slot < itemId_.Count) restrictions_[slot] = restriction;
        }

        // std::vector::resize: new slots are empty, a smaller size drops the tail
        public void SetSize(byte size)
        {
            Resize(itemId_, size, (ushort)0);
            Resize(slotId_, size, (byte)0xFF);
            Resize(quantity_, size, 0u);
            Resize(restrictions_, size, default);
        }

        public void Clean()
        {
            Type = 0;
            ShopFameArea = 0xFF; // match the lua side default value
            ShopVendorId = 0;
            ItemsCount = 0;
            ExSize = 0;
            itemId_.Clear();
            slotId_.Clear();
            quantity_.Clear();
            restrictions_.Clear();
            SetSize(ContainerSize);
        }

        private static void Resize<T>(List<T> list, int size, T fill)
        {
            if (list.Count > size) list.RemoveRange(size, list.Count - size);
            while (list.Count < size) list.Add(fill);
        }
    }
}
