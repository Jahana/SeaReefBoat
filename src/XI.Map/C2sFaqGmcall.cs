// C# wire model for the Phase-3 map C2S body struct GP_CLI_COMMAND_FAQ_GMCALL (0x0d3),
// src/map/packets/c2s/0x0d3_faq_gmcall.h via the GP_CLI_PACKET_VLA(...) macro (c2s/base.h).
//
// CRITICAL METHODOLOGY: every offset/size/mask below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_c2s_faq_gmcall_harness.cpp -> map_c2s_faq_gmcall_golden.txt), NOT hand-computed.
// Two genuinely new surfaces this slice pins:
//   (1) FIRST STRADDLING bit-field run. Prior bit-field runs all fit inside ONE allocation unit (pos's 4
//       bools in a byte, comlink's r/g/b/a in one uint16, equipset's 3 fields in a byte). Here SIX
//       `uint16_t : w` fields span TWO uint16 allocation units: type:4+vers:4+pktId:8 = 16 (unit 0) then
//       seq:7+eos:1+blkNum:8 = 16 (unit 1). Because offsetof is ILLEGAL on a bit-field (C2618), the
//       oracle derived each field's byte + mask + shift by a RUNTIME SENTINEL SCAN (set the field to its
//       all-1s width, find the single changed byte, trailing-zero-count = shift). Compiler result (byte
//       offsets from struct start; the 4-byte GP_CLI_HEADER is bytes 0..3):
//         type   byte4 mask 0x0F shift 0 | vers  byte4 mask 0xF0 shift 4 | pktId byte5 mask 0xFF shift 0
//         seq    byte6 mask 0x7F shift 0 | eos   byte6 mask 0x80 shift 7 | blkNum byte7 mask 0xFF shift 0
//       So MSVC's little-endian packing keeps every field byte-contained here even though the run crosses
//       the uint16-unit boundary between pktId (byte5) and seq (byte6) -- the boundary falls on a byte edge.
//   (2) FIRST GP_CLI_PACKET_VLA body. Unlike GP_CLI_PACKET, the VLA macro defines
//       `static constexpr size_t getMinSize() { return offsetof(PacketName, VLAField); }` -- the offset of
//       the trailing variable array. Here that is OFF_GMC.Data == 8 (the 4-byte header + the 4 bytes of
//       straddling bit fields). The engine rejects packets shorter than this; MinSize below mirrors it.
//
// The two hand-authored uint8_t enums (GP_CLI_COMMAND_FAQ_GMCALL_TYPE, GMReportBlockType) are pinned the
// same way as chunk 7/8/12/13 (re-declared in the oracle, underlying value printed, asserted vs C#).
//
// GP_CLI_HEADER id:9|size:7|sync framing is shared with the other C2S structs (XI.Map.C2sPos owns the
// WriteHeader/ReadId helpers, reused unchanged here).

using System;

namespace XI.Map
{
    // GP_CLI_COMMAND_FAQ_GMCALL_TYPE (0x0d3_faq_gmcall.h). Hand-authored uint8_t literals; pinned vs FAQTYPE_*.
    public enum FaqGmcallType : byte
    {
        AddHistory = 1,   // FAQTYPE_AddHistory=1
        GMCall = 2,       // FAQTYPE_GMCall=2
        GMNotice = 3,     // FAQTYPE_GMNotice=3
    }

    // GMReportBlockType (0x0d3_faq_gmcall.h). Hand-authored uint8_t literals; pinned vs BLKTYPE_*.
    public enum GmReportBlockType : byte
    {
        Position = 0x00,      // BLKTYPE_Position=0
        Version = 0x01,       // BLKTYPE_Version=1
        Error = 0x02,         // BLKTYPE_Error=2
        StringParam = 0x03,   // BLKTYPE_StringParam=3
        LobbyHistory = 0x04,  // BLKTYPE_LobbyHistory=4
    }

    public static class C2sFaqGmcall
    {
        // GP_CLI_HEADER prefix (id:9|size:7|sync) -- same 4-byte framing as pos/recipe/lockstyle.
        public const int OffHeader = 0;       // OFF_GMC.header=0
        public const int HeaderSize = 4;      // HDR_SIZE=4
        public const int OffSync = 2;         // HDR_OFF_sync=2

        // Body (default packing). sizeof=264, alignof=2. Data[256] is the VLA trailing array.
        public const int SizeOf = 264;        // GMC_SIZE=264
        public const int AlignOf = 2;         // GMC_ALIGN=2

        // GP_CLI_PACKET_VLA getMinSize() == offsetof(Data): the 4-byte header + 4 bytes of straddling run.
        public const int MinSize = 8;         // OFF_GMC.Data=8

        // ---- straddling bit-field run (six uint16_t fields across bytes 4..7; masks from the scan) ----
        public const int OffTypeVers = 4;     // BF_type_off=4 / BF_vers_off=4 (one byte)
        public const int TypeMask = 0x0F;     // BF_type_mask=15
        public const int TypeShift = 0;       // BF_type_shift=0
        public const int VersMask = 0xF0;     // BF_vers_mask=240
        public const int VersShift = 4;       // BF_vers_shift=4

        public const int OffPktId = 5;        // BF_pktId_off=5 (whole byte)
        public const int PktIdMask = 0xFF;    // BF_pktId_mask=255
        public const int PktIdShift = 0;      // BF_pktId_shift=0

        public const int OffSeqEos = 6;       // BF_seq_off=6 / BF_eos_off=6 (one byte)
        public const int SeqMask = 0x7F;      // BF_seq_mask=127 (7-bit field)
        public const int SeqShift = 0;        // BF_seq_shift=0
        public const int EosMask = 0x80;      // BF_eos_mask=128 (1-bit field)
        public const int EosShift = 7;        // BF_eos_shift=7

        public const int OffBlkNum = 7;       // BF_blkNum_off=7 (whole byte)
        public const int BlkNumMask = 0xFF;   // BF_blkNum_mask=255
        public const int BlkNumShift = 0;     // BF_blkNum_shift=0

        public const int OffData = 8;         // OFF_GMC.Data=8 (uint8[256]) == MinSize
        public const int DataLen = 256;       // GMC_Data_len=256

        // ---- bit-field accessors (read / read-modify-write preserving siblings) ----------------------
        public static byte GetType(ReadOnlySpan<byte> body) => (byte)((body[OffTypeVers] & TypeMask) >> TypeShift);
        public static void SetType(Span<byte> body, byte v) =>
            body[OffTypeVers] = (byte)((body[OffTypeVers] & ~TypeMask) | ((v << TypeShift) & TypeMask));

        public static byte GetVers(ReadOnlySpan<byte> body) => (byte)((body[OffTypeVers] & VersMask) >> VersShift);
        public static void SetVers(Span<byte> body, byte v) =>
            body[OffTypeVers] = (byte)((body[OffTypeVers] & ~VersMask) | ((v << VersShift) & VersMask));

        public static byte GetPktId(ReadOnlySpan<byte> body) => (byte)((body[OffPktId] & PktIdMask) >> PktIdShift);
        public static void SetPktId(Span<byte> body, byte v) =>
            body[OffPktId] = (byte)((body[OffPktId] & ~PktIdMask) | ((v << PktIdShift) & PktIdMask));

        public static byte GetSeq(ReadOnlySpan<byte> body) => (byte)((body[OffSeqEos] & SeqMask) >> SeqShift);
        public static void SetSeq(Span<byte> body, byte v) =>
            body[OffSeqEos] = (byte)((body[OffSeqEos] & ~SeqMask) | ((v << SeqShift) & SeqMask));

        public static bool GetEos(ReadOnlySpan<byte> body) => (body[OffSeqEos] & EosMask) != 0;
        public static void SetEos(Span<byte> body, bool v) =>
            body[OffSeqEos] = (byte)((body[OffSeqEos] & ~EosMask) | (v ? EosMask : 0));

        public static byte GetBlkNum(ReadOnlySpan<byte> body) => (byte)((body[OffBlkNum] & BlkNumMask) >> BlkNumShift);
        public static void SetBlkNum(Span<byte> body, byte v) =>
            body[OffBlkNum] = (byte)((body[OffBlkNum] & ~BlkNumMask) | ((v << BlkNumShift) & BlkNumMask));

        // ---- the VLA trailing array (Data[256]) ------------------------------------------------------
        public static ReadOnlySpan<byte> GetData(ReadOnlySpan<byte> body) => body.Slice(OffData, DataLen);
    }
}
