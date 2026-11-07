// XI.Quests -- the RECORD-OF-EMINENCE blob (chars.eminence), byte-for-byte port.
//
// A FOURTH structurally distinct chars state blob, persisted whole via
// charutils::SaveEminenceData `UPDATE chars ... eminence=?` (charutils.cpp:5482) and reloaded
// with db::extractFromBlob(rset,"eminence",...) (charutils.cpp:400). mmo.h eminencelog_t:
//   uint16 active[31];    // slots 0..30; slot 30 is the "time-limited" slot Add never fills
//   uint32 progress[31];  // PADDING: uint32 aligns to 4, so 2 dead bytes sit between the arrays
//   uint8  complete[512]; // 4096-bit "record completed" bitmap
// sizeof = 700 bytes. The raw blob LAYOUT (esp. the 2 padding bytes at offsets 62-63, and
// progress[] starting at 64 not 62) is the whole reason this is diffed against a compiled
// oracle -- hand-transcribing the alignment is unsafe. The padding bytes are never written,
// so keeping them zero in the backing buffer reproduces them byte-for-byte.
//
// Accessors copied VERBATIM from roe.cpp:248-425 (storage arithmetic only; the ImplementedRecords
// / Unity-rotation policy gates read Lua-loaded config, not the struct, so they are out of scope
// for the wire port). Three faithful quirks the golden pins:
//   * Add/Del scan the active[] array for i in [0,30) (30 slots); Get/SetProgress scan [0,31) (31).
//   * DelEminenceRecord compacts by swapping BOTH active[] and progress[] upward (j<29 bound),
//     so a record's progress travels with it -- verified in the mixed scenario's raw bytes.
//   * complete[] (Get/SetCompletion) is independent of active[]: Del never clears it, and a
//     record can be simultaneously active and completed.
// The runtime activemap (eminencecache_t) is NOT serialized; reproduced here as a bool[]
// maintained in lockstep by Add (set) / Del (reset) so Has() matches the engine read-back.
using System;
using System.Text;

namespace XI.Quests
{
    public sealed class EminenceBitBlob
    {
        public const int ActiveCount = 31;     // uint16 active[31]
        public const int ProgressCount = 31;   // uint32 progress[31]
        public const int CompletePages = 512;  // uint8 complete[512] (4096 records)
        public const int MaxRecordId = 4096;    // recordID >= 4096 is ignored by Set/GetCompletion

        // Raw struct offsets (little-endian).
        const int ActiveBase = 0;               // 0..62
        const int ProgressBase = 64;            // 64..188 (2 pad bytes at 62..64)
        const int CompleteBase = 188;           // 188..700
        public const int Size = 700;

        // Add/Del scan bound (i < 30) vs progress scan bound (i < 31) -- engine asymmetry.
        const int AddDelScanBound = 30;
        const int ProgressScanBound = 31;
        const int DelShiftBound = 29;           // del compaction inner loop: j < 29

        private readonly byte[] _data = new byte[Size];
        private readonly bool[] _activemap = new bool[MaxRecordId]; // runtime-only, not serialized

        public byte[] Bytes => (byte[])_data.Clone();

        ushort Active(int i) => (ushort)(_data[ActiveBase + i * 2] | (_data[ActiveBase + i * 2 + 1] << 8));
        void SetActive(int i, ushort v) { _data[ActiveBase + i * 2] = (byte)(v & 0xFF); _data[ActiveBase + i * 2 + 1] = (byte)(v >> 8); }

        uint Progress(int i)
        {
            int o = ProgressBase + i * 4;
            return (uint)_data[o] | ((uint)_data[o + 1] << 8) | ((uint)_data[o + 2] << 16) | ((uint)_data[o + 3] << 24);
        }
        void SetProgressAt(int i, uint v)
        {
            int o = ProgressBase + i * 4;
            _data[o] = (byte)(v & 0xFF);
            _data[o + 1] = (byte)((v >> 8) & 0xFF);
            _data[o + 2] = (byte)((v >> 16) & 0xFF);
            _data[o + 3] = (byte)((v >> 24) & 0xFF);
        }

        // ---- completion bitmap (complete[page] bit = id%8) ----
        public bool SetCompletion(uint recordId, bool newStatus)
        {
            if (recordId >= MaxRecordId) return false;
            int page = (int)(recordId / 8);
            int bit = (int)(recordId % 8);
            if (newStatus) _data[CompleteBase + page] |= (byte)(1 << bit);
            else _data[CompleteBase + page] &= (byte)~(1 << bit);
            return true;
        }

        public bool GetCompletion(uint recordId)
        {
            if (recordId >= MaxRecordId) return false;
            int page = (int)(recordId / 8);
            int bit = (int)(recordId % 8);
            return (_data[CompleteBase + page] & (1 << bit)) != 0;
        }

        // ---- active[] slot management ----
        public bool Add(uint recordId)
        {
            for (int i = 0; i < AddDelScanBound; i++)
            {
                if (Active(i) == 0)
                {
                    SetActive(i, (ushort)recordId);
                    if (recordId < MaxRecordId) _activemap[recordId] = true;
                    return true;
                }
                else if (Active(i) == recordId)
                {
                    return false;
                }
            }
            return false;
        }

        public bool Del(uint recordId)
        {
            for (int i = 0; i < AddDelScanBound; i++)
            {
                if (Active(i) == recordId)
                {
                    SetActive(i, 0);
                    SetProgressAt(i, 0);
                    if (recordId < MaxRecordId) _activemap[recordId] = false;
                    // shift entries up (swap BOTH arrays) so records stay in retail order
                    for (int j = i; j < DelShiftBound && Active(j + 1) != 0; j++)
                    {
                        ushort a = Active(j); SetActive(j, Active(j + 1)); SetActive(j + 1, a);
                        uint p = Progress(j); SetProgressAt(j, Progress(j + 1)); SetProgressAt(j + 1, p);
                    }
                    return true;
                }
            }
            return false;
        }

        public bool Has(uint recordId) => recordId < MaxRecordId && _activemap[recordId];

        public uint GetProgress(uint recordId)
        {
            for (int i = 0; i < ProgressScanBound; i++)
                if (Active(i) == recordId)
                    return Progress(i);
            return 0;
        }

        public bool SetProgress(uint recordId, uint progress)
        {
            for (int i = 0; i < ProgressScanBound; i++)
            {
                if (Active(i) == recordId)
                {
                    if (Progress(i) == progress) return true;
                    SetProgressAt(i, progress);
                    return true;
                }
            }
            return false;
        }

        // ---- raw storage view for the normalized codec (storage only, no semantics),
        //      symmetric to MissionBitBlob.RawSetArea/GetCompleteFlag. Extract/Project must
        //      read/write the per-slot active+progress pair verbatim so a normalized row
        //      round-trips byte-for-byte; the engine methods above (Add/Del/GetProgress) apply
        //      the scan-bound and compaction semantics and would NOT reproduce a stored slot.
        public int SlotCount => ActiveCount;
        public ushort RawGetActive(int slot) => (slot >= 0 && slot < ActiveCount) ? Active(slot) : (ushort)0;
        public uint RawGetProgress(int slot) => (slot >= 0 && slot < ActiveCount) ? Progress(slot) : 0u;
        public void RawSetSlot(int slot, ushort recordId, uint progress)
        {
            if (slot < 0 || slot >= ActiveCount) return;
            SetActive(slot, recordId);
            SetProgressAt(slot, progress);
        }

        // Rebuild the runtime activemap from active[] (what a live load does after extractFromBlob).
        void RebuildActivemap()
        {
            Array.Clear(_activemap, 0, _activemap.Length);
            for (int i = 0; i < ActiveCount; i++)
            {
                ushort id = Active(i);
                if (id != 0 && id < MaxRecordId) _activemap[id] = true;
            }
        }

        // Load mimics db::extractFromBlob(dest=700, blob, len): zero-init then copy
        // min(sizeof(dest), blobLen); padding bytes come straight from the blob (they are 0).
        public void LoadFromBlob(byte[] blob)
        {
            Array.Clear(_data, 0, _data.Length);
            if (blob == null) { RebuildActivemap(); return; }
            int n = Math.Min(Size, blob.Length);
            Buffer.BlockCopy(blob, 0, _data, 0, n);
            RebuildActivemap();
        }

        public string ToHex()
        {
            var sb = new StringBuilder(Size * 2);
            for (int i = 0; i < Size; i++) sb.Append(_data[i].ToString("X2"));
            return sb.ToString();
        }
    }
}
