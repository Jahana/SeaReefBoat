// XI.Quests -- normalized MISSION model + wire projection codec (the mission half of the
// Phase-6 redesign, symmetric to QuestWireCodec).
//
// A mission container is NOT a per-quest tri-state bit. mission.lua:getCheckArgs reads
// (getCurrentMission(area), getMissionStatus(area)), i.e. one AREA carries:
//   * current   -- uint16, the active mission id (or a sentinel: 0xFFFF for areas 0..3,
//                  101 for CoP at fresh init; 0 after a reset)
//   * status    -- uint32 = (statusUpper << 16) | statusLower
//   * complete  -- bool[64], per-mission completion flags (only written when area != CoP
//                  AND missionID < 64)
// plus the distinctive engine branch: completion for CoP / missionID>=64 is CURRENT-RELATIVE
// (missionID < current), it is NEVER stored in complete[].
//
// So the normalized unit is a per-area cell carrying the raw struct faithfully. Unlike quests
// there is no lossy status collapse here: MissionWireCodec.Extract/Project is byte-for-byte
// lossless for EVERY blob (the 64 completion flags are kept as a compact mask; the current-
// relative completion is a derived READ view, IsCompleted, not extra stored bytes). This is
// the compatibility boundary the migration hangs on: the legacy 1050-byte chars.missions blob
// becomes a derived projection of normalized char_mission_state rows.
using System;
using System.Collections.Generic;

namespace XI.Quests
{
    // One normalized mission-area fact: the full per-area struct, faithfully.
    public sealed class MissionCell
    {
        public MissionLog Area;
        public ushort Current;
        public uint Status;             // (statusUpper << 16) | statusLower
        public ulong CompletedMask;     // bit m set == complete[m] true (m < 64)

        public bool Completed(int missionId) => missionId >= 0 && missionId < 64 && ((CompletedMask >> missionId) & 1UL) != 0;
        public void SetCompleted(int missionId, bool v)
        {
            if (missionId < 0 || missionId >= 64) return;
            if (v) CompletedMask |= 1UL << missionId;
            else CompletedMask &= ~(1UL << missionId);
        }

        // The engine's completion semantics, expressed once on the normalized model:
        // CoP and missionID>=64 are current-relative; otherwise the stored flag. Mirrors
        // hasCompletedMission (lua_base_entity.cpp:8711) exactly.
        public bool IsCompleted(int missionId)
        {
            if (missionId < 0 || missionId >= MissionBitBlob.MaxMissionId) return false;
            if ((int)Area == (int)MissionLog.CoP || missionId >= 64)
                return missionId < Current;
            return Completed(missionId);
        }
    }

    public static class MissionWireCodec
    {
        // Project the 15 normalized cells onto a fresh 1050-byte blob (faithful, lossless).
        // Missing cells keep the fresh per-char sentinel init (same as a real new character),
        // so callers that pass all 15 cells reproduce any blob byte-for-byte.
        public static MissionBitBlob Project(IEnumerable<MissionCell> cells)
        {
            var blob = new MissionBitBlob(); // applies char_entity.cpp sentinel init
            foreach (var c in cells)
            {
                int area = (int)c.Area;
                if (area < 0 || area >= MissionBitBlob.Areas) continue;
                blob.RawSetArea(area, c.Current, c.Status);
                // restore the stored completion flags (bit-for-bit; for CoP the mask is empty)
                for (int m = 0; m < 64; m++)
                    blob.RawSetComplete(area, m, c.Completed(m));
            }
            return blob;
        }

        // Extract the 15 per-area cells from a legacy blob (faithful capture of the raw struct).
        public static List<MissionCell> Extract(MissionBitBlob blob)
        {
            var cells = new List<MissionCell>(MissionBitBlob.Areas);
            for (int area = 0; area < MissionBitBlob.Areas; area++)
            {
                var c = new MissionCell
                {
                    Area = (MissionLog)area,
                    Current = blob.GetCurrentMission(area),
                    Status = blob.GetMissionStatus(area),
                };
                for (int m = 0; m < 64; m++)
                    c.SetCompleted(m, blob.GetCompleteFlag(area, m));
                cells.Add(c);
            }
            return cells;
        }
    }
}
