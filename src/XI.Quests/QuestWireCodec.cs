// XI.Quests — the Phase-6 wire projection codec.
//
// This is the compatibility boundary between the NEW normalized model (a quest is
// (stable area, id, explicit QuestStatus), backed by char_quest_state rows) and the
// LEGACY client wire representation (the 704-byte chars.quests bitmask that the
// 0x05F/0x056 quest-log packets memcpy straight to the socket).
//
// Invariant the migration relies on:
//   Project(Extract(legacyBlob)) == legacyBlob   for every blob the ENGINE can produce.
// The engine's ops (addQuest/completeQuest/delQuest) never leave a quest in the
// (current=1, complete=1) state -- completeQuest clears current when it sets complete --
// so every reachable bit-pair is canonical:
//   (cur,comp)  ->  status      ->  canonical (cur,comp)
//   (0,0)       ->  Available   ->  (0,0)
//   (1,0)       ->  Accepted    ->  (1,0)
//   (0,1)       ->  Completed   ->  (0,1)
//   (1,1)       ->  Completed   ->  (0,1)   <-- LOSSY: only rawSet produces this,
//                                              so Extract/Project is not identity here.
// QuestWireCodec therefore projects canonical pairs; the (1,1) case is asserted as a
// documented lossy edge in the verifier, not silently hidden.
using System.Collections.Generic;

namespace XI.Quests
{
    // One normalized quest fact: which area, which quest, what explicit status.
    // In the full model `Id` is backed by a stable string key resolved to (area, numericId)
    // only at this boundary; the numeric pair is what the client's bitmask understands.
    public readonly struct QuestCell
    {
        public readonly QuestLog Area;
        public readonly ushort Id;
        public readonly QuestStatus Status;

        public QuestCell(QuestLog area, ushort id, QuestStatus status)
        {
            Area = area; Id = id; Status = status;
        }
    }

    public static class QuestWireCodec
    {
        // Project a set of normalized cells onto a fresh canonical 704-byte blob.
        // Only Accepted/Completed carry wire bits; Available is the zero state.
        public static QuestBitBlob Project(IEnumerable<QuestCell> cells)
        {
            var blob = new QuestBitBlob();
            foreach (var c in cells)
            {
                int area = (int)c.Area;
                int id = c.Id;
                switch (c.Status)
                {
                    case QuestStatus.Accepted:
                        blob.RawSet(area, id, current: true, complete: false);
                        break;
                    case QuestStatus.Completed:
                        blob.RawSet(area, id, current: false, complete: true);
                        break;
                    case QuestStatus.Available:
                        blob.RawSet(area, id, current: false, complete: false);
                        break;
                }
            }
            return blob;
        }

        // Extract the normalized cells (non-Available quests) from a legacy blob, using
        // the engine's own precedence (getQuestStatus): completed bit wins.
        public static List<QuestCell> Extract(QuestBitBlob blob)
        {
            var cells = new List<QuestCell>();
            for (int area = 0; area < QuestBitBlob.Areas; area++)
            {
                for (int id = 0; id < QuestBitBlob.MaxQuestId; id++)
                {
                    var status = blob.GetStatus(area, id);
                    if (status != QuestStatus.Available)
                        cells.Add(new QuestCell((QuestLog)area, (ushort)id, status));
                }
            }
            return cells;
        }
    }
}
