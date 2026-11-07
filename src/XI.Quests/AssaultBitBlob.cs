// XI.Quests -- the ASSAULT-log wire blob (chars.assault), byte-for-byte port.
//
// Assault is a THIRD, structurally distinct chars state blob -- neither the quest bitmask
// (704B, per-quest current/complete bit-pair) nor the mission struct (70B x 15 areas). It is a
// SINGLE container: mmo.h `assaultlog_t { uint16 current; bool complete[128]; }` = 130 bytes,
// value-initialized all-zero (char_entity.h m_assaultLog{}, NO sentinel unlike missions),
// persisted whole via the same charutils::SaveMissionsList `UPDATE chars ... assault=?`
// (charutils.cpp:5448) and reloaded with db::extractFromBlob(rset,"assault",...) (charutils.cpp:398).
//
// Accessors copied VERBATIM from lua_base_entity.cpp:9324-9437 and diffed against a compiled
// oracle (oracle/assault_harness.cpp -> assault_golden.txt). Two branches differ from missions
// and are the reason this is its own port, not a reuse of MissionBitBlob:
//   * completeAssault is UNCONDITIONAL: it sets current=0 AND complete[id]=true even when id
//     is not the current mission (mission completeMission is guarded on current==id).
//   * delAssault clears ONLY current (guarded on the uint8-truncated current == id) and NEVER
//     touches the completion flag (mission delMission clears complete[] too).
// Also: current is uint16 storage but getCurrentAssault/delAssault read/compare it as a uint8
// (static_cast<uint8_t>) -- the low byte -- reproduced here.
//
// Raw layout (little-endian): current = bytes +0..1; complete[missionID] byte = +2 + missionID
// (missionID < 128). The engine indexes complete[] with a uint8 id, so ids >= 128 are undefined
// behaviour on BOTH sides; the oracle exercises ids 0..127 only, and the read/write paths below
// guard the completion array to that range (setting current=0 for a complete is still faithful).
using System;
using System.Text;

namespace XI.Quests
{
    // Faithful 130-byte mirror of the raw assaultlog_t struct. The legacy representation, not a
    // normalized model.
    public sealed class AssaultBitBlob
    {
        public const int CurrentBytes = 2;    // uint16
        public const int CompleteCount = 128; // bool complete[128]
        public const int Size = CurrentBytes + CompleteCount; // 130
        const int CompleteBase = 2;           // complete[0] byte offset

        private readonly byte[] _data = new byte[Size];

        public byte[] Bytes => (byte[])_data.Clone();

        ushort Current { get => (ushort)(_data[0] | (_data[1] << 8)); set { _data[0] = (byte)(value & 0xFF); _data[1] = (byte)(value >> 8); } }
        // The uint8-truncated view the engine reads/compares.
        byte CurrentByte => (byte)(Current & 0xFF);
        bool GetComplete(int id) => id >= 0 && id < CompleteCount && _data[CompleteBase + id] != 0;
        void SetComplete(int id, bool v) { if (id >= 0 && id < CompleteCount) _data[CompleteBase + id] = (byte)(v ? 1 : 0); }

        // Fresh per-character state is all-zero (no sentinel -- differs from missions).
        public AssaultBitBlob() { Array.Clear(_data, 0, _data.Length); }

        // addAssault (lua_base_entity.cpp:9324): UNCONDITIONAL current = id (id is uint8).
        public void AddAssault(byte id)
        {
            // (engine logs a warning if current != 0; no state effect)
            Current = id;
        }

        // delAssault (lua_base_entity.cpp:9350): compare uint8-truncated current; clear ONLY
        // current, never the completion flag.
        public void DelAssault(byte id)
        {
            byte current = CurrentByte;
            if (current == id)
                Current = 0;
        }

        // getCurrentAssault (lua_base_entity.cpp:9373): returns static_cast<uint8>(current).
        public byte GetCurrentAssault() => CurrentByte;

        // hasCompletedAssault (lua_base_entity.cpp:9401): returns complete[id].
        public bool HasCompletedAssault(byte id) => GetComplete(id);

        // completeAssault (lua_base_entity.cpp:9417): UNCONDITIONAL reset current=0 and set
        // complete[id]=true (guarded to the valid completion-id range; a non-current id still
        // completes -- the branch that differs from completeMission).
        public void CompleteAssault(byte id)
        {
            // (engine warns if current != id, then completes regardless)
            Current = 0;
            SetComplete(id, true);
        }

        // ---- raw storage view for the normalized codec (storage only, no semantics),
        //      symmetric to MissionBitBlob.RawSetArea/GetCompleteFlag. Extract/Project must
        //      read/write the raw uint16 `current` and each per-id complete BYTE verbatim so a
        //      single normalized row round-trips byte-for-byte; GetCurrentAssault applies the
        //      uint8 truncation and Add/Del/Complete mutate, so the codec needs this branch-free
        //      view. Complete bytes are engine-written 0/1, so the codec's 0/1 write is faithful.
        public int CompletionCount => CompleteCount;
        public ushort RawGetCurrent() => Current;
        public void RawSetCurrent(ushort v) => Current = v;
        public bool RawGetCompleteFlag(int id) => id >= 0 && id < CompleteCount && _data[CompleteBase + id] != 0;
        public void RawSetComplete(int id, bool v) => SetComplete(id, v);

        // Load mimics db::extractFromBlob(dest=130, blob, len): zero-init then copy
        // min(sizeof(dest), blobLen); bytes past blobLen stay zero. A loaded blob is taken
        // verbatim (the engine overwrites the whole struct; fresh init is all-zero anyway).
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
