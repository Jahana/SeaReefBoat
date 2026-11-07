// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_INFLUENCE::CAMPAIGN::PacketData (0x071),
// src/map/packets/s2c/0x071_influence_campaign.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cGroupTbl/S2cBattleMessage/S2cSchedulor convention.
//
// CRITICAL METHODOLOGY: every offset/size/bit-position below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_influence_campaign_enum_harness.cpp -> map_s2c_influence_campaign_enum_golden.txt),
// NOT hand-computed.
//
// SIXTH engine-enum body and the layout-DENSEST offline-verifiable S2C body in the whole packet set -- it packs,
// for the first time, EVERY remaining packing hazard into one struct:
//   (1) ENGINE-ENUM MEMBER (chunk 21, invariant proven uint8/uint16/uint32): Mode is `enum class
//       GP_SERV_COMMAND_INFLUENCE_MODE : uint8_t` (Campaign=2, Colonization=3; src/map/packets/s2c/0x071_influence.h).
//       The oracle's uint8_t twin of the WHOLE 200-byte struct is byte-identical in size/align/mode-offset
//       (EQ_SIZE/EQ_ALIGN/EQ_MODE all 1), so Mode's layout == a plain uint8. Its literals are pinned separately by
//       the .h re-parse (magic_enum not vendored).
//   (2) STRADDLING BIT-FIELDS read as bit positions in the CONTAINING uint32 WORD (the chunk-23 lesson generalised):
//       a field whose bits cross a byte boundary must NOT be read per-byte. Every bit-field here lives in a uint32
//       allocation unit; read/write via BitConverter.ToUInt32(word) + (word >> bitpos) & ((1<<width)-1), where
//       bitpos = (BF_*_off * 8 + shift) measured from that word's start. The compiler emitted off/mask/shift/width
//       for all 17 fields; C# derives bitpos from (off*8+shift) and the gate re-checks it BOTH ways.
//   (3) A BIT-FIELD "RUN-RESET" INSIDE A STRUCT-ARRAY ELEMENT -- the genuinely-new hazard: campaignzone_t holds TWO
//       separate uint32 bit-field runs (run1 widths 1/3/10/10/8 = 32 bits in the element's bytes 0-3; run2 widths
//       10/10/12 = 32 bits in a FRESH uint32 at element bytes 8-11) SPLIT BY four plain u8 influence bytes at 4-7.
//       After run1 exhausts its 32-bit unit, the four u8s, then run2 STARTS A NEW allocation unit (MSVC does not
//       continue the old one): the compiler proves it -- MaxFortifications bit-byte = 8 (element-relative), i.e.
//       OFF_PD.Zones0_MaxFort_bitbyte = Zones@44 + 8 = 52, NOT a continuation of bytes 0-3.
//   (4) A STRUCT-OF-STRUCTS-OF-BITFIELDS (first seen): campaignnations_t nests SEVEN campaignnation_t members, each
//       itself a 4-field bit run of widths 4/14/7/7 = 32 bits -- a struct array (stride 4) whose every element is a
//       packed bit-field word.
//   (5) NEW bit-field WIDTHS beyond the 1/2/4/8-bit runs of earlier chunks: 5 (Sandoria/Bastok/Windurst/Beastman),
//       7 (Morale/Prosperity), 10 (Current/Max Fortifications & Resources), 12 (unused01/unused), 14 (unused).
//   (6) S2C BODY-RELATIVE default packing.
//
// Layouts (DEFAULT packing), body-relative:
//   campaigncontrolledareas_t (4/4): ONE uint32 word (element bytes 0-3): Sandoria:5@b0 Bastok:5@b5 Windurst:5@b10
//     Beastman:5@b15 unused:12@b20 (5+5+5+5+12==32).
//   campaignnation_t          (4/4): ONE uint32 word: Reconnaissance:4@b0 unused:14@b4 Morale:7@b18 Prosperity:7@b25
//     (4+14+7+7==32).
//   campaignnations_t         (28/4): campaignnation_t x7 (stride 4): Sandoria@0 Bastok@4 Windurst@8 BeastmanOrc@12
//     BeastmanQuadav@16 BeastmanYagudo@20 BeastmanDarkKindred@24 (7*4==28, no tail pad).
//   campaignzone_t           (12/4): uint32 run1@bytes0-3: unused00:1@b0 Owner:3@b1 CurrentFortifications:10@b4
//     CurrentResources:10@b14 Heroism:8@b24 (1+3+10+10+8==32); u8 x4 InfluenceSandoria@4 Bastok@5 Windurst@6
//     Beastman@7; uint32 run2@bytes8-11 (FRESH unit): MaxFortifications:10@b0 MaxResources:10@b10 unused01:12@b20.
//   PacketData              (200/4): Mode(u8 enum)@0 padding05(u8)@1 Length(u16)@2 padding07(u8)@4 ZoneOffset(u8)@5
//     padding0A[2]@6 AlliedNotes(i32)@8 ControlledAreas(4)@12 Nations(28)@16 Zones[13](12 each)@44
//     (44 + 13*12 == 200, no tail pad).

using System;

namespace XI.Map
{
    // GP_SERV_COMMAND_INFLUENCE_MODE (src/map/packets/s2c/0x071_influence.h): a uint8-backed engine enum.
    // Layout-irrelevant to the struct (proved by the compiler's uint8_t twin being byte-identical); the two
    // literals are ground truth from the .h via the gate's independent re-parse and cross-checked vs the compiler.
    public enum InfluenceMode : byte
    {
        Campaign     = 2,   // ENUM_Campaign=2
        Colonization = 3,   // ENUM_Colonization=3
    }

    // ---- shared bit-field word primitive ---------------------------------------------------------------
    // Every bit-field in this body lives inside a uint32 allocation unit. A field's bits are (word >> bitpos) &
    // ((1<<width)-1); a write is read-modify-write of the whole word so sibling fields sharing the unit survive.
    internal static class BitWord
    {
        public static uint Get(ReadOnlySpan<byte> body, int wordOff, int bitpos, int width)
        {
            uint w = BitConverter.ToUInt32(body.Slice(wordOff, 4));
            uint mask = width >= 32 ? 0xFFFFFFFFu : ((1u << width) - 1u);
            return (w >> bitpos) & mask;
        }

        public static void Set(Span<byte> body, int wordOff, int bitpos, int width, uint val)
        {
            uint mask = width >= 32 ? 0xFFFFFFFFu : ((1u << width) - 1u);
            uint w = BitConverter.ToUInt32(body.Slice(wordOff, 4));
            w = (w & ~(mask << bitpos)) | ((val & mask) << bitpos);
            BitConverter.TryWriteBytes(body.Slice(wordOff, 4), w);
        }
    }

    // campaigncontrolledareas_t -- a single uint32 word of 5 influence values + a 12-bit pad. sizeof=4, align=4.
    // All bit-positions are (BF_campaigncontrolledareas_t_*_off * 8 + shift); every field shares word offset 0.
    public static class ControlledAreasEntry
    {
        public const int SizeOf = 4;   // CCA_SIZE=4
        public const int AlignOf = 4;  // CCA_ALIGN=4

        public const int SandoriaBit = 0;   public const int SandoriaWidth = 5;    // off0 shift0
        public const int BastokBit = 5;     public const int BastokWidth = 5;      // off0 shift5
        public const int WindurstBit = 10;  public const int WindurstWidth = 5;    // off1 shift2 -> 8+2=10
        public const int BeastmanBit = 15;  public const int BeastmanWidth = 5;    // off1 shift7 -> 8+7=15
        public const int UnusedBit = 20;    public const int UnusedWidth = 12;     // off2 shift4 -> 16+4=20
    }

    // campaignnation_t -- a single uint32 word. sizeof=4, align=4.
    public static class NationEntry
    {
        public const int SizeOf = 4;   // CN_SIZE=4
        public const int AlignOf = 4;  // CN_ALIGN=4

        public const int ReconnaissanceBit = 0;   public const int ReconnaissanceWidth = 4;    // off0 shift0
        public const int UnusedBit = 4;           public const int UnusedWidth = 14;           // off0 shift4
        public const int MoraleBit = 18;          public const int MoraleWidth = 7;            // off2 shift2 -> 16+2=18
        public const int ProsperityBit = 25;      public const int ProsperityWidth = 7;        // off3 shift1 -> 24+1=25
    }

    // campaignzone_t -- the run-reset struct. sizeof=12, align=4.
    // run1 fields live in the uint32 word at element byte 0; the four influence bytes at 4-7; run2 fields live in a
    // FRESH uint32 word at element byte 8 (NOT a continuation of run1 -- the compiler proves the reset).
    public static class ZoneEntry
    {
        public const int SizeOf = 12;  // CZ_SIZE=12
        public const int AlignOf = 4;  // CZ_ALIGN=4

        public const int Run1WordOffset = 0;  // OFF_CZ run1 word (unused00/Owner/CurrentFort/CurrentRes/Heroism)
        public const int Run2WordOffset = 8;  // OFF_CZ run2 word (MaxFort/MaxRes/unused01) -- fresh uint32 at byte 8

        // run1 (relative to the run1 word @ element byte 0):
        public const int Unused00Bit = 0;              public const int Unused00Width = 1;   // off0 shift0
        public const int OwnerBit = 1;                 public const int OwnerWidth = 3;      // off0 shift1
        public const int CurrentFortificationsBit = 4; public const int CurrentFortificationsWidth = 10; // off0 shift4
        public const int CurrentResourcesBit = 14;     public const int CurrentResourcesWidth = 10;      // off1 shift6 -> 8+6=14
        public const int HeroismBit = 24;              public const int HeroismWidth = 8;    // off3 shift0 -> 24+0=24

        // four plain u8 influence bytes:
        public const int OffInfluenceSandoria = 4;     // OFF_CZ.InfluenceSandoria=4
        public const int OffInfluenceBastok = 5;
        public const int OffInfluenceWindurst = 6;
        public const int OffInfluenceBeastman = 7;     // OFF_CZ.InfluenceBeastman=7

        // run2 (relative to the run2 word @ element byte 8; compiler off is element-relative so bitpos=(off-8)*8+shift):
        public const int MaxFortificationsBit = 0;     public const int MaxFortificationsWidth = 10; // off8 shift0
        public const int MaxResourcesBit = 10;         public const int MaxResourcesWidth = 10;      // off9 shift2 -> (9-8)*8+2=10
        public const int Unused01Bit = 20;             public const int Unused01Width = 12;          // off10 shift4 -> (10-8)*8+4=20
    }

    // GP_SERV_COMMAND_INFLUENCE::CAMPAIGN::PacketData (outer, body-relative, default packing). sizeof=200, alignof=4.
    public static class S2cInfluenceCampaign
    {
        public const int SizeOf = 200;    // PD_SIZE=200 (44 + 13*12 == 200, no tail pad)
        public const int AlignOf = 4;     // PD_ALIGN=4

        public const int OffMode = 0;         // OFF_PD.Mode=0 (InfluenceMode, u8)
        public const int OffPadding05 = 1;    // OFF_PD.padding05=1 (u8)
        public const int OffLength = 2;       // OFF_PD.Length=2 (u16)
        public const int OffPadding07 = 4;    // OFF_PD.padding07=4 (u8)
        public const int OffZoneOffset = 5;   // OFF_PD.ZoneOffset=5 (u8)
        public const int OffPadding0A = 6;    // OFF_PD.padding0A=6 (u8[2])
        public const int Padding0ALen = 2;    // PD_padding0A_len=2
        public const int OffAlliedNotes = 8;  // OFF_PD.AlliedNotes=8 (i32)
        public const int OffControlledAreas = 12; // OFF_PD.ControlledAreas=12 (campaigncontrolledareas_t, 4B)
        public const int OffNations = 16;     // OFF_PD.Nations=16 (campaignnations_t, 28B)
        public const int OffZones = 44;       // OFF_PD.Zones=44 (campaignzone_t[13], stride 12)
        public const int ZonesCount = 13;     // PD_Zones_count=13
        public const int ZonesLen = 156;      // PD_Zones_len=156

        public const int NationsCount = 7;    // campaignnations_t holds 7 campaignnation_t (stride 4)

        // ---- nested-inclusive byte offsets --------------------------------------------------------------
        // ControlledAreas is one uint32 word at OffControlledAreas (its run1 word offset is +0).
        public static int CAWord() => OffControlledAreas;
        // Nation i (0..6) is one uint32 word at OffNations + i*4.
        public static int NationWord(int i) => OffNations + i * NationEntry.SizeOf;
        // Zone i (0..12): run1 word at element+0, run2 word at element+8; influence bytes 4-7 are element-relative.
        public static int ZoneRun1Word(int i) => OffZones + i * ZoneEntry.SizeOf + ZoneEntry.Run1WordOffset;
        public static int ZoneRun2Word(int i) => OffZones + i * ZoneEntry.SizeOf + ZoneEntry.Run2WordOffset;
        public static int ZoneByte(int i, int inner) => OffZones + i * ZoneEntry.SizeOf + inner;

        // ---- top-level scalar accessors ----
        public static InfluenceMode GetMode(ReadOnlySpan<byte> body) => (InfluenceMode)body[OffMode];
        public static void SetMode(Span<byte> body, InfluenceMode v) => body[OffMode] = (byte)v;
        public static byte GetModeRaw(ReadOnlySpan<byte> body) => body[OffMode];

        public static byte GetPadding05(ReadOnlySpan<byte> body) => body[OffPadding05];
        public static ushort GetLength(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffLength, 2));
        public static void SetLength(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffLength, 2), v);
        public static byte GetPadding07(ReadOnlySpan<byte> body) => body[OffPadding07];
        public static byte GetZoneOffset(ReadOnlySpan<byte> body) => body[OffZoneOffset];
        public static void SetZoneOffset(Span<byte> body, byte v) => body[OffZoneOffset] = v;
        public static ReadOnlySpan<byte> GetPadding0A(ReadOnlySpan<byte> body) => body.Slice(OffPadding0A, Padding0ALen);
        public static int GetAlliedNotes(ReadOnlySpan<byte> body) => BitConverter.ToInt32(body.Slice(OffAlliedNotes, 4));
        public static void SetAlliedNotes(Span<byte> body, int v) => BitConverter.TryWriteBytes(body.Slice(OffAlliedNotes, 4), v);

        // ---- campaigncontrolledareas_t fields (word @ OffControlledAreas, bitpos relative to that word) ----
        public static uint GetCASandoria(ReadOnlySpan<byte> b) => BitWord.Get(b, CAWord(), ControlledAreasEntry.SandoriaBit, ControlledAreasEntry.SandoriaWidth);
        public static void SetCASandoria(Span<byte> b, uint v) => BitWord.Set(b, CAWord(), ControlledAreasEntry.SandoriaBit, ControlledAreasEntry.SandoriaWidth, v);
        public static uint GetCABastok(ReadOnlySpan<byte> b) => BitWord.Get(b, CAWord(), ControlledAreasEntry.BastokBit, ControlledAreasEntry.BastokWidth);
        public static void SetCABastok(Span<byte> b, uint v) => BitWord.Set(b, CAWord(), ControlledAreasEntry.BastokBit, ControlledAreasEntry.BastokWidth, v);
        public static uint GetCAWindurst(ReadOnlySpan<byte> b) => BitWord.Get(b, CAWord(), ControlledAreasEntry.WindurstBit, ControlledAreasEntry.WindurstWidth);
        public static void SetCAWindurst(Span<byte> b, uint v) => BitWord.Set(b, CAWord(), ControlledAreasEntry.WindurstBit, ControlledAreasEntry.WindurstWidth, v);
        public static uint GetCABeastman(ReadOnlySpan<byte> b) => BitWord.Get(b, CAWord(), ControlledAreasEntry.BeastmanBit, ControlledAreasEntry.BeastmanWidth);
        public static void SetCABeastman(Span<byte> b, uint v) => BitWord.Set(b, CAWord(), ControlledAreasEntry.BeastmanBit, ControlledAreasEntry.BeastmanWidth, v);

        // ---- campaignnation_t fields (nation index 0..6, word @ NationWord(i)) ----
        public static uint GetNationReconnaissance(ReadOnlySpan<byte> b, int i) => BitWord.Get(b, NationWord(i), NationEntry.ReconnaissanceBit, NationEntry.ReconnaissanceWidth);
        public static void SetNationReconnaissance(Span<byte> b, int i, uint v) => BitWord.Set(b, NationWord(i), NationEntry.ReconnaissanceBit, NationEntry.ReconnaissanceWidth, v);
        public static uint GetNationMorale(ReadOnlySpan<byte> b, int i) => BitWord.Get(b, NationWord(i), NationEntry.MoraleBit, NationEntry.MoraleWidth);
        public static void SetNationMorale(Span<byte> b, int i, uint v) => BitWord.Set(b, NationWord(i), NationEntry.MoraleBit, NationEntry.MoraleWidth, v);
        public static uint GetNationProsperity(ReadOnlySpan<byte> b, int i) => BitWord.Get(b, NationWord(i), NationEntry.ProsperityBit, NationEntry.ProsperityWidth);
        public static void SetNationProsperity(Span<byte> b, int i, uint v) => BitWord.Set(b, NationWord(i), NationEntry.ProsperityBit, NationEntry.ProsperityWidth, v);

        // ---- campaignzone_t run1 fields (zone index 0..12, word @ ZoneRun1Word(i)) ----
        public static uint GetZoneOwner(ReadOnlySpan<byte> b, int i) => BitWord.Get(b, ZoneRun1Word(i), ZoneEntry.OwnerBit, ZoneEntry.OwnerWidth);
        public static void SetZoneOwner(Span<byte> b, int i, uint v) => BitWord.Set(b, ZoneRun1Word(i), ZoneEntry.OwnerBit, ZoneEntry.OwnerWidth, v);
        public static uint GetZoneCurrentFortifications(ReadOnlySpan<byte> b, int i) => BitWord.Get(b, ZoneRun1Word(i), ZoneEntry.CurrentFortificationsBit, ZoneEntry.CurrentFortificationsWidth);
        public static void SetZoneCurrentFortifications(Span<byte> b, int i, uint v) => BitWord.Set(b, ZoneRun1Word(i), ZoneEntry.CurrentFortificationsBit, ZoneEntry.CurrentFortificationsWidth, v);
        public static uint GetZoneCurrentResources(ReadOnlySpan<byte> b, int i) => BitWord.Get(b, ZoneRun1Word(i), ZoneEntry.CurrentResourcesBit, ZoneEntry.CurrentResourcesWidth);
        public static void SetZoneCurrentResources(Span<byte> b, int i, uint v) => BitWord.Set(b, ZoneRun1Word(i), ZoneEntry.CurrentResourcesBit, ZoneEntry.CurrentResourcesWidth, v);
        public static uint GetZoneHeroism(ReadOnlySpan<byte> b, int i) => BitWord.Get(b, ZoneRun1Word(i), ZoneEntry.HeroismBit, ZoneEntry.HeroismWidth);
        public static void SetZoneHeroism(Span<byte> b, int i, uint v) => BitWord.Set(b, ZoneRun1Word(i), ZoneEntry.HeroismBit, ZoneEntry.HeroismWidth, v);

        // ---- campaignzone_t plain u8 influence bytes (element offsets 4-7) ----
        public static byte GetZoneInfluenceSandoria(ReadOnlySpan<byte> b, int i) => b[ZoneByte(i, ZoneEntry.OffInfluenceSandoria)];
        public static void SetZoneInfluenceSandoria(Span<byte> b, int i, byte v) => b[ZoneByte(i, ZoneEntry.OffInfluenceSandoria)] = v;
        public static byte GetZoneInfluenceBastok(ReadOnlySpan<byte> b, int i) => b[ZoneByte(i, ZoneEntry.OffInfluenceBastok)];
        public static void SetZoneInfluenceBastok(Span<byte> b, int i, byte v) => b[ZoneByte(i, ZoneEntry.OffInfluenceBastok)] = v;
        public static void SetZoneInfluenceWindurst(Span<byte> b, int i, byte v) => b[ZoneByte(i, ZoneEntry.OffInfluenceWindurst)] = v;
        public static byte GetZoneInfluenceWindurst(ReadOnlySpan<byte> b, int i) => b[ZoneByte(i, ZoneEntry.OffInfluenceWindurst)];
        public static byte GetZoneInfluenceBeastman(ReadOnlySpan<byte> b, int i) => b[ZoneByte(i, ZoneEntry.OffInfluenceBeastman)];
        public static void SetZoneInfluenceBeastman(Span<byte> b, int i, byte v) => b[ZoneByte(i, ZoneEntry.OffInfluenceBeastman)] = v;

        // ---- campaignzone_t run2 fields (FRESH uint32 at element byte 8, word @ ZoneRun2Word(i)) ----
        public static uint GetZoneMaxFortifications(ReadOnlySpan<byte> b, int i) => BitWord.Get(b, ZoneRun2Word(i), ZoneEntry.MaxFortificationsBit, ZoneEntry.MaxFortificationsWidth);
        public static void SetZoneMaxFortifications(Span<byte> b, int i, uint v) => BitWord.Set(b, ZoneRun2Word(i), ZoneEntry.MaxFortificationsBit, ZoneEntry.MaxFortificationsWidth, v);
        public static uint GetZoneMaxResources(ReadOnlySpan<byte> b, int i) => BitWord.Get(b, ZoneRun2Word(i), ZoneEntry.MaxResourcesBit, ZoneEntry.MaxResourcesWidth);
        public static void SetZoneMaxResources(Span<byte> b, int i, uint v) => BitWord.Set(b, ZoneRun2Word(i), ZoneEntry.MaxResourcesBit, ZoneEntry.MaxResourcesWidth, v);
    }
}
