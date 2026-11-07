// XI.Quests — the MISSION-log wire blob (chars.missions), byte-for-byte port.
//
// Missions are NOT the quest bitmask: mmo.h missionlog_t is
//   { uint16 current; uint16 statusUpper; uint16 statusLower; bool complete[64]; }
// (70 bytes), stored as m_missionLog[MAX_MISSIONAREA=15] => a 1050-byte raw blob,
// persisted whole via charutils::SaveMissionsList (`UPDATE chars SET missions=?`) and
// reloaded with db::extractFromBlob(rset,"missions",...). This is a HARD wire contract
// (the mission packets memcpy it), so the accessors are copied VERBATIM from
// lua_base_entity.cpp and diffed against a compiled oracle (oracle/mission_harness.cpp).
//
// Engine branches reproduced exactly (and exercised by the oracle scenarios):
//   * fresh init is NOT all-zero: areas 0..3 current=0xFFFF, area 4/5 current=0,
//     area 6 (CoP) current=101, all statusUpper/Lower=0  (char_entity.cpp:176-195)
//   * completeMission resets current to (area>2 ? 0 : 0xFFFF); it writes complete[] only
//     when area != CoP AND missionID < 64
//   * hasCompletedMission / delMission treat (area == CoP || missionID >= 64) as
//     "completion is current-relative (missionID < current)" and never index complete[]
//   * setMissionStatus indexed form writes one 4-bit nibble (pos 0..3 -> statusLower,
//     pos 4..7 -> statusUpper) of a 32-bit status; the whole form writes the full uint32
//
// Raw layout (little-endian, area-major): area base = area*70; current = +0..1;
// statusUpper = +2..3; statusLower = +4..5; complete[missionID] byte = +6 + missionID.
using System;
using System.Text;

namespace XI.Quests
{
    // MissionLog log ids (src/map/enums/mission_log.h), uint8.
    public enum MissionLog : byte
    {
        Sandoria = 0,
        Bastok = 1,
        Windurst = 2,
        Zilart = 3,
        ToAU = 4,
        WoTG = 5,
        CoP = 6,
        Assault = 7,
        Campaign = 8,
        ACP = 9,
        AMK = 10,
        ASA = 11,
        SoA = 12,
        RoV = 13,
        // xi.mission.log_id.TVR (scripts/globals/missions.lua): a mission key's log name (mission.tvr.<const>)
        TVR = 14,
    }

    // Faithful 1050-byte mirror of missionlog_t[15]. The raw representation, not a
    // normalized model -- normalized mission rows are a later Phase-6 concern.
    public sealed class MissionBitBlob
    {
        public const int Areas = 15;          // MAX_MISSIONAREA
        public const int MaxMissionId = 851;  // MAX_MISSIONID
        public const int BytesPerArea = 70;   // uint16*3 + bool[64]
        public const int Size = Areas * BytesPerArea; // 1050
        const int CompleteBase = 6;           // complete[0] byte offset within an area
        const int CoP = 6;                     // MissionLog::CoP

        private readonly byte[] _data = new byte[Size];

        public byte[] Bytes => (byte[])_data.Clone();

        static int Base(int area) => area * BytesPerArea;
        static bool InRange(int area, int missionId) => area >= 0 && area < Areas && missionId >= 0 && missionId < MaxMissionId;

        ushort GetU16(int off) => (ushort)(_data[off] | (_data[off + 1] << 8));
        void SetU16(int off, ushort v) { _data[off] = (byte)(v & 0xFF); _data[off + 1] = (byte)(v >> 8); }

        // current is stored as uint16; the C++ "-1" reset for area<=2 is uint16 0xFFFF.
        ushort Current(int area) => GetU16(Base(area));
        void SetCurrent(int area, ushort v) => SetU16(Base(area), v);
        bool GetComplete(int area, int missionId) => _data[Base(area) + CompleteBase + missionId] != 0;
        void SetComplete(int area, int missionId, bool v) => _data[Base(area) + CompleteBase + missionId] = (byte)(v ? 1 : 0);

        uint Status32(int area)
        {
            // (statusUpper << 16) | statusLower  -- both uint16, joined into a 32-bit field.
            uint upper = GetU16(Base(area) + 2);
            uint lower = GetU16(Base(area) + 4);
            return (upper << 16) | lower;
        }
        void SetStatus32(int area, uint missionStatus)
        {
            SetU16(Base(area) + 4, (ushort)(missionStatus & 0xFFFF));        // statusLower
            SetU16(Base(area) + 2, (ushort)((missionStatus >> 16) & 0xFFFF)); // statusUpper
        }

        // Fresh per-character init, copied from char_entity.cpp:176-195.
        public MissionBitBlob()
        {
            Array.Clear(_data, 0, _data.Length);
            for (int i = 0; i <= 3; i++) SetCurrent(i, 0xFFFF);
            SetCurrent(4, 0);   // MISSION_TOAU
            SetCurrent(5, 0);   // MISSION_WOTG
            SetCurrent(6, 101); // MISSION_COP
            // statusUpper/Lower already zero from the clear (engine zeroes them too)
        }

        // addMission (lua_base_entity.cpp:8587): set current = missionID.
        public void AddMission(int area, ushort missionId)
        {
            if (area < Areas && missionId < MaxMissionId)
                SetCurrent(area, missionId);
        }

        // delMission (lua_base_entity.cpp:8622).
        public void DelMission(int area, ushort missionId)
        {
            if (area >= 0 && area < Areas && missionId < MaxMissionId)
            {
                ushort current = Current(area);
                bool complete = (area == CoP || missionId >= 64) ? false : GetComplete(area, missionId);

                if (current == missionId)
                    SetCurrent(area, (ushort)(area > 2 ? 0 : 0xFFFF));
                if (complete)
                    SetComplete(area, missionId, false);
            }
        }

        // completeMission (lua_base_entity.cpp:8740).
        public void CompleteMission(int area, ushort missionId)
        {
            if (area >= 0 && area < Areas && missionId < MaxMissionId)
            {
                if (Current(area) == missionId)
                {
                    SetCurrent(area, (ushort)(area > 2 ? 0 : 0xFFFF));
                    if (area != CoP && missionId < 64)
                        SetComplete(area, missionId, true);
                }
            }
        }

        // hasCompletedMission (lua_base_entity.cpp:8711).
        public bool HasCompletedMission(int area, ushort missionId)
        {
            if (area >= 0 && area < Areas && missionId < MaxMissionId)
                return (area == CoP || missionId >= 64)
                    ? (missionId < Current(area))
                    : GetComplete(area, missionId);
            return false;
        }

        // getCurrentMission (lua_base_entity.cpp:8661).
        public ushort GetCurrentMission(int area)
        {
            return area >= 0 && area < Areas ? Current(area) : (ushort)0;
        }

        // setMissionStatus (indexed 4-bit nibble), lua_base_entity.cpp:8780 arg3-as-uint8 branch.
        public void SetMissionStatusNibble(int area, byte value, byte pos)
        {
            if (area >= Areas) return;
            if (pos > 7) return;
            if (value > 0xF) return;
            uint missionStatus = Status32(area);
            uint mask = ~(0xFu << (4 * pos));
            missionStatus &= mask;
            missionStatus |= (uint)value << (4 * pos);
            SetStatus32(area, missionStatus);
        }

        // setMissionStatus (whole uint32), lua_base_entity.cpp:8821 else branch.
        public void SetMissionStatusFull(int area, uint value)
        {
            if (area >= Areas) return;
            SetStatus32(area, value);
        }

        // getMissionStatus (whole), lua_base_entity.cpp:8849 else branch.
        public uint GetMissionStatus(int area)
        {
            if (area >= 0 && area < Areas) return Status32(area);
            return 0;
        }

        // getMissionStatus (indexed nibble), lua_base_entity.cpp:8851 nibble branch.
        public uint GetMissionStatusNibble(int area, byte pos)
        {
            if (area >= Areas) return 0;
            if (pos > 7) return 0;
            return (Status32(area) >> (4 * pos)) & 0xF;
        }

        // ---- raw wire-write / read primitives, symmetric to QuestBitBlob.RawSet ----
        // MissionWireCodec needs the branch-free storage view: HasCompletedMission applies
        // the CoP / missionID>=64 current-relative logic, and AddMission/CompleteMission
        // mutate; the codec must read/write the raw per-area struct fields verbatim so a
        // normalized cell round-trips byte-for-byte. These touch storage only, no semantics.

        // Raw complete[missionID] flag (only valid for missionID < 64; CoP/highid never set it).
        public bool GetCompleteFlag(int area, int missionId)
        {
            if (area < 0 || area >= Areas || missionId < 0 || missionId >= 64) return false;
            return GetComplete(area, missionId);
        }

        // Write an area's current + full 32-bit status (upper<<16 | lower) verbatim.
        public void RawSetArea(int area, ushort current, uint status)
        {
            if (area < 0 || area >= Areas) return;
            SetCurrent(area, current);
            SetStatus32(area, status);
        }

        // Write one raw complete[missionID] flag (missionID < 64 only; mirrors GetCompleteFlag).
        public void RawSetComplete(int area, int missionId, bool value)
        {
            if (area < 0 || area >= Areas || missionId < 0 || missionId >= 64) return;
            SetComplete(area, missionId, value);
        }

        // Load mimics db::extractFromBlob(dest=1050, blob, len): zero-init then copy
        // min(sizeof(dest), blobLen); bytes past blobLen stay zero. NOTE: a loaded blob is
        // taken verbatim (no sentinel init) -- the engine overwrites the whole struct.
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
