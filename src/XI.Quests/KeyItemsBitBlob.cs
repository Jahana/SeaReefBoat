// XI.Quests -- the KEY-ITEM wire blob (chars.keyitems), byte-for-byte port.
//
// Keyitems is a FIFTH whole-struct chars state blob. mmo.h `keyitems_t { keyitems_table_t tables[8] }`
// where each `keyitems_table_t { xi::bitset<512> keyList; xi::bitset<512> seenList; }`. xi::bitset<512>
// is (512+7)/8 = 64 raw bytes (xi.h:134), so one table = 128 bytes and the blob is 1024 bytes. The
// struct is trivially copyable (std::array of std::array<uint8,64>), so db::BlobWrapper::create memcpy's
// it VERBATIM (reinterpret_cast<char*>(&source), sizeof(T)) -- the identical contract as quests/missions/
// assault/eminence. Persisted whole via charutils::SaveKeyItems `UPDATE chars SET keyitems=? WHERE charid=?`
// (charutils.cpp:5539) and reloaded with db::extractFromBlob(rset,"keyitems",PChar->keys) (charutils.cpp:392).
// The two bitsets map straight to the 0x055 GP_SERV_COMMAND_SCENARIOITEM GetItemFlag/LookItemFlag memcpy
// (0x055_scenarioitem.cpp:34-35); tables[6].keyList is the mount data read by 0x0ae_mount_data.cpp:30.
//
// Accessors copied VERBATIM from charutils.cpp:4003-4085 and diffed against a compiled oracle
// (oracle/keyitems_harness.cpp -> keyitems_golden.txt). The two orthogonal 512-bit bitsets per table
// (keyList "possessed" vs seenList "discovered") are the new structure -- the analog of eminence's
// active[] vs complete[] independence; a key item can be seen-but-not-held (seen_only golden) and
// held-then-dropped while still seen (add_then_del golden).
//
// Raw layout: table = (uint16)id / 512, index = (uint16)id % 512; keyList bit = table*128 + idx/8,
// seenList bit = table*128 + 64 + idx/8 (LSB-first, 1 << (idx%8) -- identical bit addressing to the
// questlog bit-pair). The engine guards every accessor on `table >= keys.tables.size() /*8*/`: writes
// no-op, reads return false -- so id >= 4096 is undefined-but-safe (capacity_guard golden proves it).
using System;
using System.Text;

namespace XI.Quests
{
    // Faithful 1024-byte mirror of the raw keyitems_t struct. The legacy representation, not a
    // normalized model.
    public sealed class KeyItemsBitBlob
    {
        public const int TableCount = 8;        // std::array<keyitems_table_t, 8>
        public const int BitsPerTable = 512;    // xi::bitset<512>
        public const int BytesPerList = 64;     // (512 + 7) / 8
        public const int TableBytes = 2 * BytesPerList; // keyList + seenList = 128
        public const int Size = TableCount * TableBytes; // 1024
        public const int IdSpace = TableCount * BitsPerTable; // 4096 addressable key-item ids (raw view; engine capacity-guards writes/reads at table >= 8)

        const int SeenBase = BytesPerList; // seenList offset within a table (keyList sits at +0)

        private readonly byte[] _data = new byte[Size];

        public byte[] Bytes => (byte[])_data.Clone();

        // ---- xi::bitset<512> set/get copied VERBATIM from xi.h:140-166 (LSB-first, pos = in-bitset index) ----
        static void BitSet(byte[] data, int off, int pos, bool value)
        {
            if (value) data[off + pos / 8] |= (byte)(1 << (pos % 8));
            else data[off + pos / 8] &= (byte)~(1 << (pos % 8));
        }
        static bool BitGet(byte[] data, int off, int pos) => ((data[off + pos / 8] >> (pos % 8)) & 0x01) != 0;

        // table/index split -- the id is bound as uint16 (charutils casts static_cast<uint16_t>(keyItemId)).
        static int TableOf(ushort id) => id / BitsPerTable;
        static int IndexOf(ushort id) => id % BitsPerTable;
        // Capacity guard `keyItemTable >= PChar->keys.tables.size()` (charutils.cpp:4007/4063/4077...).
        static bool InCapacity(ushort id) => TableOf(id) < TableCount;

        // Fresh per-character state is all-zero (bitsets value-init their std::array<uint8,64> data).
        public KeyItemsBitBlob() { Array.Clear(_data, 0, _data.Length); }

        // addKeyItem (charutils.cpp:4059): keyList[index] = true, guarded on capacity.
        public void AddKeyItem(ushort id)
        {
            if (!InCapacity(id)) return;
            BitSet(_data, TableOf(id) * TableBytes, IndexOf(id), true);
        }

        // delKeyItem (charutils.cpp:4073): keyList[index] = false, guarded on capacity.
        public void DelKeyItem(ushort id)
        {
            if (!InCapacity(id)) return;
            BitSet(_data, TableOf(id) * TableBytes, IndexOf(id), false);
        }

        // hasKeyItem (charutils.cpp:4003): return keyList[index]; false when out of capacity.
        public bool HasKeyItem(ushort id)
        {
            if (!InCapacity(id)) return false;
            return BitGet(_data, TableOf(id) * TableBytes, IndexOf(id));
        }

        // markSeenKeyItem (charutils.cpp:4032): seenList[index] = true, guarded on capacity.
        public void MarkSeenKeyItem(ushort id)
        {
            if (!InCapacity(id)) return;
            BitSet(_data, TableOf(id) * TableBytes + SeenBase, IndexOf(id), true);
        }

        // unseenKeyItem (charutils.cpp:4046): seenList[index] = false, guarded on capacity.
        public void UnseenKeyItem(ushort id)
        {
            if (!InCapacity(id)) return;
            BitSet(_data, TableOf(id) * TableBytes + SeenBase, IndexOf(id), false);
        }

        // seenKeyItem (charutils.cpp:4018): return seenList[index]; false when out of capacity.
        public bool SeenKeyItem(ushort id)
        {
            if (!InCapacity(id)) return false;
            return BitGet(_data, TableOf(id) * TableBytes + SeenBase, IndexOf(id));
        }

        // ---- raw storage view for the normalized codec (storage only, no semantics), symmetric to
        //      AssaultBitBlob.RawGetCompleteFlag / MissionBitBlob.RawSetArea. Extract/Project must read
        //      and write each keyList/seenList bit verbatim (ignoring the capacity guard) so a
        //      hand-authored blob with bits in every table round-trips byte-for-byte. ----
        public bool RawGetKey(ushort id) => BitGet(_data, TableOf(id) * TableBytes, IndexOf(id));
        public void RawSetKey(ushort id, bool v) => BitSet(_data, TableOf(id) * TableBytes, IndexOf(id), v);
        public bool RawGetSeen(ushort id) => BitGet(_data, TableOf(id) * TableBytes + SeenBase, IndexOf(id));
        public void RawSetSeen(ushort id, bool v) => BitSet(_data, TableOf(id) * TableBytes + SeenBase, IndexOf(id), v);

        // Load mimics db::extractFromBlob(dest=1024, blob, len): zero-init then copy
        // min(sizeof(dest), blobLen); bytes past blobLen stay zero. A loaded blob is taken verbatim
        // (the engine overwrites the whole struct; fresh init is all-zero anyway).
        public void LoadFromBlob(byte[] blob)
        {
            Array.Clear(_data, 0, _data.Length);
            if (blob == null) return;
            int n = Math.Min(Size, blob.Length);
            Buffer.BlockCopy(blob, 0, _data, 0, n);
        }

        public string ToHex()
        {
            var sb = new StringBuilder(Size * 2);
            for (int i = 0; i < Size; i++) sb.Append(_data[i].ToString("X2"));
            return sb.ToString();
        }
    }
}
