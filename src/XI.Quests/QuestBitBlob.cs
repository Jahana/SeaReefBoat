// XI.Quests — the Phase-6 wire projection codec (keystone of the strangler migration).
//
// QuestLog / QuestStatus mirror the engine exactly:
//   enum QuestLog : uint8  { Sandoria=0 .. Coalition=10 }   (src/map/enums/quest_log.h, 11 areas)
//   getQuestStatus returns complete?2:(current?1:0)          (charutils.cpp:6298)
//     -> Available=0, Accepted=1, Completed=2.
//
// QuestBitBlob is a faithful 704-byte mirror of the engine's on-disk blob, so the
// stock client's 0x05F/0x056 quest-log packets (which memcpy each area's current[]/
// complete[] straight to the socket) stay byte-compatible. All bit-addressing and
// add/complete/del semantics are copied VERBATIM from lua_base_entity.cpp so the
// oracle (csharp/oracle/quest_harness.cpp -> quest_golden.txt) diffs byte-for-byte.
//
// Layout (mmo.h questlog_t { uint8 current[32]; uint8 complete[32]; }, char_entity.h
// m_questLog[MAX_QUESTAREA], MAX_QUESTID=256):
//   area base      = area * 64
//   current byte   = area*64 + quest/8        bit = 1 << (quest % 8)
//   complete byte  = area*64 + 32 + quest/8    bit = 1 << (quest % 8)
//   total size     = 11 * 64 = 704 bytes
using System;
using System.Text;

namespace XI.Quests
{
    // Indices as used in CCharEntity::m_questLog (src/map/enums/quest_log.h).
    public enum QuestLog : byte
    {
        Sandoria = 0,
        Bastok = 1,
        Windurst = 2,
        Jeuno = 3,
        OtherAreas = 4,
        Outlands = 5,
        AhtUrghan = 6,
        CrystalWar = 7,
        Abyssea = 8,
        Adoulin = 9,
        Coalition = 10,
    }

    // getQuestStatus (charutils.cpp): completed bit wins over current.
    public enum QuestStatus : byte
    {
        Available = 0,
        Accepted = 1,
        Completed = 2,
    }

    // The raw 704-byte questlog_t[11] mirror. This is the wire/legacy representation,
    // NOT the authoritative model -- that is QuestWireCodec's normalized cells.
    public sealed class QuestBitBlob
    {
        public const int Areas = 11;        // MAX_QUESTAREA
        public const int MaxQuestId = 256;  // MAX_QUESTID
        public const int BytesPerArea = 64; // questlog_t = current[32] + complete[32]
        public const int Size = Areas * BytesPerArea; // 704

        private readonly byte[] _data = new byte[Size];

        public byte[] Bytes => (byte[])_data.Clone();

        static int CurrentIndex(int area, int quest) { return area * BytesPerArea + (quest >> 3); }
        static int CompleteIndex(int area, int quest) { return area * BytesPerArea + 32 + (quest >> 3); }
        static byte Mask(int quest) { return (byte)(1 << (quest & 7)); }

        static bool InRange(int area, int quest) { return area >= 0 && area < Areas && quest >= 0 && quest < MaxQuestId; }

        bool IsCurrent(int area, int quest) { return (_data[CurrentIndex(area, quest)] & Mask(quest)) != 0; }
        bool IsComplete(int area, int quest) { return (_data[CompleteIndex(area, quest)] & Mask(quest)) != 0; }

        // addQuest (lua_base_entity.cpp:8390): set current bit if unset. Bounds-guarded.
        public void AddQuest(int area, ushort quest)
        {
            if (!InRange(area, quest)) return;
            if (!IsCurrent(area, quest)) _data[CurrentIndex(area, quest)] |= Mask(quest);
        }

        // completeQuest (lua_base_entity.cpp:8559): if complete unset, clear current, set complete.
        public void CompleteQuest(int area, ushort quest)
        {
            if (!InRange(area, quest)) return;
            if (!IsComplete(area, quest))
            {
                _data[CurrentIndex(area, quest)] &= (byte)~Mask(quest);
                _data[CompleteIndex(area, quest)] |= Mask(quest);
            }
        }

        // delCurrentQuest (lua_base_entity.cpp:8433): clear current bit only.
        public void DelCurrentQuest(int area, ushort quest)
        {
            if (!InRange(area, quest)) return;
            if (IsCurrent(area, quest)) _data[CurrentIndex(area, quest)] &= (byte)~Mask(quest);
        }

        // delQuest (lua_base_entity.cpp:8468): if either set, clear both.
        public void DelQuest(int area, ushort quest)
        {
            if (!InRange(area, quest)) return;
            if (IsCurrent(area, quest) || IsComplete(area, quest))
            {
                _data[CurrentIndex(area, quest)] &= (byte)~Mask(quest);
                _data[CompleteIndex(area, quest)] &= (byte)~Mask(quest);
            }
        }

        // getQuestStatus (charutils.cpp:6298): completed wins over current.
        public QuestStatus GetStatus(int area, int quest)
        {
            bool complete = IsComplete(area, quest);
            bool current = IsCurrent(area, quest);
            return complete ? QuestStatus.Completed : (current ? QuestStatus.Accepted : QuestStatus.Available);
        }

        // rawSet: force the (current,complete) bit pair -- exercises the (1,1) precedence
        // case the normal ops never produce, matching quest_harness.cpp rawSet.
        public void RawSet(int area, int quest, bool current, bool complete)
        {
            if (current) _data[CurrentIndex(area, quest)] |= Mask(quest);
            else _data[CurrentIndex(area, quest)] &= (byte)~Mask(quest);
            if (complete) _data[CompleteIndex(area, quest)] |= Mask(quest);
            else _data[CompleteIndex(area, quest)] &= (byte)~Mask(quest);
        }

        // Load mimics db::extractFromBlob(dest=704, blob, len): zero-init then
        // copy min(sizeof(dest), blobLen). Bytes past blobLen stay zero.
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
