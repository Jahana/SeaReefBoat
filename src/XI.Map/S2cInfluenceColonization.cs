// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_INFLUENCE::COLONIZATION::PacketData (0x071),
// src/map/packets/s2c/0x071_influence_colonization.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cInfluenceCampaign/S2cGroupTbl convention.
//
// CRITICAL METHODOLOGY: every offset/size/bit-position below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_influence_colonization_enum_harness.cpp -> map_s2c_influence_colonization_enum_golden.txt),
// NOT hand-computed.
//
// The 0x071 SIBLING of chunk 26 (CAMPAIGN): it REUSES two chunk-26 techniques and adds one S2C-first trailing array:
//   (1) ENGINE-ENUM MEMBER (chunk 21, matrix completed 21-25): Mode is the SAME uint8 engine enum
//       GP_SERV_COMMAND_INFLUENCE_MODE (Campaign=2, Colonization=3; src/map/packets/s2c/0x071_influence.h) already
//       ported as XI.Map.InfluenceMode in chunk 26 -- REUSED, not redefined. The oracle's uint8_t twin of the WHOLE
//       200-byte struct is byte-identical (EQ_SIZE/EQ_ALIGN/EQ_MODE all 1), so Mode's layout == a plain uint8.
//   (2) STRADDLING BIT-FIELD read as a bit POSITION in the CONTAINING uint32 (chunk 23/26): colonizationzone_t has
//       ColonizationRate:7 (bits 0-6) then CurrentBivouacs:3 at bit 7 -- bits 7,8,9 CROSS the byte 0/1 boundary, so it
//       MUST be read via BitConverter.ToUInt32(word) >> (off*8+shift) & ((1<<width)-1), NOT a per-byte mask.
//   (3) STRUCT-OF-STRUCTS-OF-BITFIELDS (chunk 26): colonizationzones_t nests NINE colonizationzone_t (stride 4), each
//       itself a 7/3/3/19 bit run in one uint32.
//   CONTRAST (the negative case chunk 26 proved): coalitionranks_t's six 4-bit fields + an 8-bit pad are ALL
//       byte-aligned (bits 0/4/8/12/16/20/24), so NO straddle there -- but the same uint32-word access path is used,
//       which is correct whether or not a field crosses a byte. NEW for this body: a trailing fixed uint8_t
//       padding00[144] (Zones end @52 -> Bayld@52 -> padding00@56, 56+144==200, no tail pad).
//
// Layouts (DEFAULT packing), body-relative:
//   coalitionranks_t   (4/4): ONE uint32 word (bytes 0-3): Pioneers:4@b0 Peacekeepers:4@b4 Couriers:4@b8 Scouts:4@b12
//     Inventors:4@b16 Mummers:4@b20 unused:8@b24 (4*6+8==32, all byte-aligned).
//   colonizationzone_t (4/4): ONE uint32 word: ColonizationRate:7@b0 CurrentBivouacs:3@b7 (STRADDLES 0/1) MaxBivouacs:3@b10
//     unused:19@b13 (7+3+3+19==32).
//   colonizationzones_t(36/4): colonizationzone_t x9 (stride 4): unused@0 YahseHuntingGrounds@4 ... RaKaznar@32 (9*4==36).
//   PacketData       (200/4): Mode(u8 enum)@0 padding05(u8)@1 Length(u16)@2 unknown00(u32)@4 unknown01(u32)@8
//     Ranks(coalitionranks_t)@12 Zones(colonizationzones_t)@16 Bayld(i32)@52 padding00[144]@56 (56+144==200, no tail pad).

using System;

namespace XI.Map
{
    // coalitionranks_t -- a single uint32 word of six 4-bit coalition ranks + an 8-bit pad (all byte-aligned).
    // sizeof=4, align=4. Bit positions are (BF_coalitionranks_t_*_off * 8 + shift).
    public static class CoalitionRanksEntry
    {
        public const int SizeOf = 4;   // CR_SIZE=4
        public const int AlignOf = 4;  // CR_ALIGN=4

        public const int PioneersBit = 0;       public const int PioneersWidth = 4;       // off0 shift0
        public const int PeacekeepersBit = 4;   public const int PeacekeepersWidth = 4;    // off0 shift4
        public const int CouriersBit = 8;       public const int CouriersWidth = 4;        // off1 shift0
        public const int ScoutsBit = 12;        public const int ScoutsWidth = 4;          // off1 shift4
        public const int InventorsBit = 16;     public const int InventorsWidth = 4;       // off2 shift0
        public const int MummersBit = 20;       public const int MummersWidth = 4;         // off2 shift4
        public const int UnusedBit = 24;        public const int UnusedWidth = 8;          // off3 shift0
    }

    // colonizationzone_t -- a single uint32 word. sizeof=4, align=4. CurrentBivouacs (bit 7, width 3) STRADDLES the
    // byte 0/1 boundary -> it must be read/written as a bit position in the whole uint32, never a per-byte mask.
    public static class ColonizationZoneEntry
    {
        public const int SizeOf = 4;   // CZ_SIZE=4
        public const int AlignOf = 4;  // CZ_ALIGN=4

        public const int ColonizationRateBit = 0;   public const int ColonizationRateWidth = 7;    // off0 shift0
        public const int CurrentBivouacsBit = 7;    public const int CurrentBivouacsWidth = 3;     // off0 shift7 -> STRADDLE
        public const int MaxBivouacsBit = 10;       public const int MaxBivouacsWidth = 3;         // off1 shift2 -> 8+2=10
        public const int UnusedBit = 13;            public const int UnusedWidth = 19;             // off1 shift5 -> 8+5=13
    }

    // GP_SERV_COMMAND_INFLUENCE::COLONIZATION::PacketData (outer, body-relative, default packing). sizeof=200, alignof=4.
    // Mode REUSES the chunk-26 XI.Map.InfluenceMode enum (not redefined); bit access reuses chunk-26 XI.Map.BitWord.
    public static class S2cInfluenceColonization
    {
        public const int SizeOf = 200;    // PD_SIZE=200 (56 + 144 == 200, no tail pad)
        public const int AlignOf = 4;     // PD_ALIGN=4

        public const int OffMode = 0;         // OFF_PD.Mode=0 (InfluenceMode, u8)
        public const int OffPadding05 = 1;    // OFF_PD.padding05=1 (u8)
        public const int OffLength = 2;       // OFF_PD.Length=2 (u16)
        public const int OffUnknown00 = 4;    // OFF_PD.unknown00=4 (u32)
        public const int OffUnknown01 = 8;    // OFF_PD.unknown01=8 (u32)
        public const int OffRanks = 12;       // OFF_PD.Ranks=12 (coalitionranks_t, 4B -- one uint32 word)
        public const int OffZones = 16;       // OFF_PD.Zones=16 (colonizationzones_t, 36B -- 9 words)
        public const int ZonesCount = 9;      // PD_Zones_count=9
        public const int ZonesLen = 36;       // PD_Zones_len=36
        public const int OffBayld = 52;       // OFF_PD.Bayld=52 (i32)
        public const int OffPadding00 = 56;   // OFF_PD.padding00=56 (u8[144])
        public const int Padding00Len = 144;  // PD_padding00_len=144

        // ---- nested-inclusive word offsets (each sub-struct/zone is ONE uint32 word) ----
        public static int RanksWord() => OffRanks;                      // OFF_PD.Ranks_bitbyte=12
        public static int ZoneWord(int i) => OffZones + i * ColonizationZoneEntry.SizeOf;  // ZoneWord(1)=20 == OFF_PD.Zones1

        // ---- top-level scalar accessors ----
        public static InfluenceMode GetMode(ReadOnlySpan<byte> body) => (InfluenceMode)body[OffMode];
        public static void SetMode(Span<byte> body, InfluenceMode v) => body[OffMode] = (byte)v;
        public static byte GetModeRaw(ReadOnlySpan<byte> body) => body[OffMode];

        public static byte GetPadding05(ReadOnlySpan<byte> body) => body[OffPadding05];
        public static ushort GetLength(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffLength, 2));
        public static void SetLength(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffLength, 2), v);
        public static uint GetUnknown00(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffUnknown00, 4));
        public static void SetUnknown00(Span<byte> body, uint v) => BitConverter.TryWriteBytes(body.Slice(OffUnknown00, 4), v);
        public static uint GetUnknown01(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffUnknown01, 4));
        public static void SetUnknown01(Span<byte> body, uint v) => BitConverter.TryWriteBytes(body.Slice(OffUnknown01, 4), v);
        public static int GetBayld(ReadOnlySpan<byte> body) => BitConverter.ToInt32(body.Slice(OffBayld, 4));
        public static void SetBayld(Span<byte> body, int v) => BitConverter.TryWriteBytes(body.Slice(OffBayld, 4), v);
        public static ReadOnlySpan<byte> GetPadding00(ReadOnlySpan<byte> body) => body.Slice(OffPadding00, Padding00Len);

        // ---- coalitionranks_t fields (word @ OffRanks; all byte-aligned but read through the same uint32 path) ----
        public static uint GetRankPioneers(ReadOnlySpan<byte> b) => BitWord.Get(b, RanksWord(), CoalitionRanksEntry.PioneersBit, CoalitionRanksEntry.PioneersWidth);
        public static void SetRankPioneers(Span<byte> b, uint v) => BitWord.Set(b, RanksWord(), CoalitionRanksEntry.PioneersBit, CoalitionRanksEntry.PioneersWidth, v);
        public static uint GetRankPeacekeepers(ReadOnlySpan<byte> b) => BitWord.Get(b, RanksWord(), CoalitionRanksEntry.PeacekeepersBit, CoalitionRanksEntry.PeacekeepersWidth);
        public static void SetRankPeacekeepers(Span<byte> b, uint v) => BitWord.Set(b, RanksWord(), CoalitionRanksEntry.PeacekeepersBit, CoalitionRanksEntry.PeacekeepersWidth, v);
        public static uint GetRankCouriers(ReadOnlySpan<byte> b) => BitWord.Get(b, RanksWord(), CoalitionRanksEntry.CouriersBit, CoalitionRanksEntry.CouriersWidth);
        public static void SetRankCouriers(Span<byte> b, uint v) => BitWord.Set(b, RanksWord(), CoalitionRanksEntry.CouriersBit, CoalitionRanksEntry.CouriersWidth, v);
        public static uint GetRankScouts(ReadOnlySpan<byte> b) => BitWord.Get(b, RanksWord(), CoalitionRanksEntry.ScoutsBit, CoalitionRanksEntry.ScoutsWidth);
        public static uint GetRankInventors(ReadOnlySpan<byte> b) => BitWord.Get(b, RanksWord(), CoalitionRanksEntry.InventorsBit, CoalitionRanksEntry.InventorsWidth);
        public static uint GetRankMummers(ReadOnlySpan<byte> b) => BitWord.Get(b, RanksWord(), CoalitionRanksEntry.MummersBit, CoalitionRanksEntry.MummersWidth);

        // ---- colonizationzones_t[i] fields (word @ ZoneWord(i); CurrentBivouacs straddles byte 0/1 -> uint32 path) ----
        public static uint GetZoneColonizationRate(ReadOnlySpan<byte> b, int i) => BitWord.Get(b, ZoneWord(i), ColonizationZoneEntry.ColonizationRateBit, ColonizationZoneEntry.ColonizationRateWidth);
        public static void SetZoneColonizationRate(Span<byte> b, int i, uint v) => BitWord.Set(b, ZoneWord(i), ColonizationZoneEntry.ColonizationRateBit, ColonizationZoneEntry.ColonizationRateWidth, v);
        public static uint GetZoneCurrentBivouacs(ReadOnlySpan<byte> b, int i) => BitWord.Get(b, ZoneWord(i), ColonizationZoneEntry.CurrentBivouacsBit, ColonizationZoneEntry.CurrentBivouacsWidth);
        public static void SetZoneCurrentBivouacs(Span<byte> b, int i, uint v) => BitWord.Set(b, ZoneWord(i), ColonizationZoneEntry.CurrentBivouacsBit, ColonizationZoneEntry.CurrentBivouacsWidth, v);
        public static uint GetZoneMaxBivouacs(ReadOnlySpan<byte> b, int i) => BitWord.Get(b, ZoneWord(i), ColonizationZoneEntry.MaxBivouacsBit, ColonizationZoneEntry.MaxBivouacsWidth);
        public static void SetZoneMaxBivouacs(Span<byte> b, int i, uint v) => BitWord.Set(b, ZoneWord(i), ColonizationZoneEntry.MaxBivouacsBit, ColonizationZoneEntry.MaxBivouacsWidth, v);
    }
}
