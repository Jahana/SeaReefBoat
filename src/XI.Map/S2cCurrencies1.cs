// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_CURRENCIES_1::PacketData (0x113),
// src/map/packets/s2c/0x113_currencies_1.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cGroupEffects/S2cGroupTbl convention.
//
// CRITICAL METHODOLOGY: every offset/size/bit-position below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_currencies_1_harness.cpp -> map_s2c_currencies_1_golden.txt), NOT hand-computed.
//
// THE genuinely-new packing facts this chunk exists to pin (no prior chunk 1-27 exercised them):
//   (1) FIRST uint64 allocation-unit bit-field run. The plans block is five `uint64_t : 9` + one `uint64_t : 19` =
//       EXACTLY 64 bits = one uint64 unit (chunks 12-27 scanned bit-fields packed into uint8/16/32 units only). The
//       standalone plans_unit_t is 8/8; each field's INTRA-UNIT bit position is 0/9/18/27/36/45 (compiler BF_*_off*8
//       + shift) -- every 9-bit field STRADDLES a byte boundary INSIDE the uint64 (e.g. umbrage off=1 shift=1 = bit 9,
//       crossing bytes 0/1 of the unit), so the whole unit MUST be read as one BitConverter.ToUInt64 and shifted by the
//       intra-unit bit, masked by (1<<width)-1. A per-byte mask drops bits across the unit's internal byte boundaries.
//   (2) pack(1) COLLAPSES a mid-struct uint64's alignment to 1. The whole body is `#pragma pack(push,1)` (the .h
//       comment: "uint64_t throws off alignment"), so PD_ALIGN=1 and PD_SIZE=248 == the pure member-byte sum (NO padding
//       anywhere -- the gapless i*7+3 248-byte fill proves it). Contrast chunk 6 (0x058 recipe): a mid-struct uint64
//       under DEFAULT packing forced the WHOLE struct to alignof 8 and inserted 4 pad bytes. Here pack(1) overrides
//       that: the uint64 plans run starts at body byte 212 -- NOT 8-aligned (212 % 8 == 4) -- legal ONLY because pack(1)
//       removed the alignment requirement. A hand-reasoner aligning the uint64 to 8 would pad to 216 and shift
//       everything after it.
//   (3) A ~110-member dense fixed-width scalar chain (int32 / uint16 / uint8 interleaved) with pack(1) -> every offset
//       is just the running byte sum (no realign). Modelled as compiler-transcribed offset constants + generic typed
//       little-endian span accessors (the realistic port surface for a struct this wide; see the Off* consts below).
//
// Layout (DEFAULT-> actually #pragma pack(push,1), body-relative). The bit-field run:
//   plans_unit_t (8/8 standalone, at body byte 212 inside the pack(1) body): uint64 bloodshed:9@b0 umbrage:9@b9
//     ritualistic:9@b18 tutelary:9@b27 primacy:9@b36 unused:19@b45 (9*5+19==64).
//   PacketData (248/1): [the ~110 scalar fields at the Off* byte sums] | plans(u64 run)@212 | u16 reclamation@220
//     padding00@222 | i32 unity_accolades@224 | u16 fire..dark_crystals@228..242 deeds@244 padding01@246
//     (246+2 == 248 == SizeOf, pack(1) align 1 -> NO tail pad).

using System;

namespace XI.Map
{
    // plans_unit_t -- the uint64 allocation-unit bit-field run. Intra-unit bit positions (0..63) are compiler-derived
    // (BF_plans_unit_t_*_off*8 + shift). Read/write the WHOLE 8-byte unit as one uint64; each 9-bit field straddles a
    // byte boundary within it, so a per-byte mask would drop bits.
    public static class PlansUnit
    {
        public const int SizeOf = 8;   // PLANS_SIZE=8
        public const int AlignOf = 8;  // PLANS_ALIGN=8 (the STANDALONE unit's natural align; the pack(1) body collapses to 1)

        public const int BloodshedBit = 0;   public const int BloodshedWidth = 9;    // off0 shift0
        public const int UmbrageBit = 9;     public const int UmbrageWidth = 9;      // off1 shift1 -> 8+1=9 (STRADDLES bytes 0/1)
        public const int RitualisticBit = 18; public const int RitualisticWidth = 9;  // off2 shift2 -> 16+2=18
        public const int TutelaryBit = 27;   public const int TutelaryWidth = 9;     // off3 shift3 -> 24+3=27
        public const int PrimacyBit = 36;    public const int PrimacyWidth = 9;      // off4 shift4 -> 32+4=36
        public const int UnusedBit = 45;     public const int UnusedWidth = 19;      // off5 shift5 -> 40+5=45

        // the whole run is 9*5 + 19 == 64 bits (exactly one uint64 allocation unit).
        public static bool SumsTo64() => BloodshedWidth + UmbrageWidth + RitualisticWidth + TutelaryWidth + PrimacyWidth + UnusedWidth == 64;
    }

    // GP_SERV_COMMAND_CURRENCIES_1::PacketData (outer, body-relative, #pragma pack(1)). sizeof=248, alignof=1.
    // Offsets are transcribed verbatim from the compiled oracle (OFF_PD.* keys); accessors are generic little-endian
    // span reads by offset (the realistic surface for a ~110-field flat body -- a bespoke Get/Set per field would be
    // ~440 hand-written methods prone to transcription error; the gate drives the same accessors over the compiler's
    // raw offsets instead).
    public static class S2cCurrencies1
    {
        public const int SizeOf = 248;   // PD_SIZE=248 (246 + 2 == 248, pack(1) align 1 -> NO tail pad)
        public const int AlignOf = 1;    // PD_ALIGN=1 -- pack(1) collapses even the mid-struct uint64 run's alignment

        public const int OffPlans = 212; // OFF_PD.rems_tale_10=211 -> the uint64 run starts at 212 (212 % 8 == 4: the
                                         // pack(1) proof that an UNALIGNED uint64 bit-field unit is permitted here)

        // ---- int32 fields (compiler OFF_PD.*) ----
        public const int OffConquestSandoria = 0;
        public const int OffConquestBastok = 4;
        public const int OffConquestWindurst = 8;
        public const int OffGuildFishing = 28;
        public const int OffGuildWoodworking = 32;
        public const int OffGuildSmithing = 36;
        public const int OffGuildGoldsmithing = 40;
        public const int OffGuildWeaving = 44;
        public const int OffGuildLeathercraft = 48;
        public const int OffGuildBonecraft = 52;
        public const int OffGuildAlchemy = 56;
        public const int OffGuildCooking = 60;
        public const int OffCinders = 64;
        public const int OffBallistaPoints = 76;
        public const int OffFellowPoints = 80;
        public const int OffResearchMarks = 92;
        public const int OffMoblinMarbles = 100;
        public const int OffLegionPoints = 108;
        public const int OffSparksOfEminence = 112;
        public const int OffShiningStars = 116;
        public const int OffImperialStanding = 120;
        public const int OffAssaultLSanctum = 124;
        public const int OffAssaultMjtg = 128;
        public const int OffAssaultLCavern = 132;
        public const int OffAssaultPeriqia = 136;
        public const int OffAssaultIlrusi = 140;
        public const int OffTokens = 144;
        public const int OffZeni = 148;
        public const int OffJettons = 152;
        public const int OffTherionIchor = 156;
        public const int OffAlliedNotes = 160;
        public const int OffCruor = 168;
        public const int OffResistanceCredits = 172;
        public const int OffDominionNotes = 176;
        public const int OffTraverserStones = 188;
        public const int OffVoidstones = 192;
        public const int OffKupofriedsCorundums = 196;
        public const int OffUnityAccolades = 224;

        // ---- uint16 fields (compiler OFF_PD.*) ----
        public const int OffBeastmansSeals = 12;
        public const int OffKindredsSeals = 14;
        public const int OffKindredsCrests = 16;
        public const int OffHighKindredsCrests = 18;
        public const int OffSacredKindredsCrests = 20;
        public const int OffAncientBeastcoins = 22;
        public const int OffValorPoints = 24;
        public const int OffScylds = 26;
        public const int OffChocobucksSandoria = 84;
        public const int OffChocobucksBastok = 86;
        public const int OffChocobucksWindurst = 88;
        public const int OffDailyTally = 90;
        public const int OffInfamy = 104;
        public const int OffPrestige = 106;
        public const int OffCopperAmanVouchers = 164;
        public const int OffLoginPoints = 166;
        public const int OffReclamationMarks = 220;   // first u16 AFTER the uint64 run (212 + 8 == 220)
        public const int OffPadding00 = 222;
        public const int OffFireCrystals = 228;
        public const int OffIceCrystals = 230;
        public const int OffWindCrystals = 232;
        public const int OffEarthCrystals = 234;
        public const int OffLightningCrystals = 236;
        public const int OffWaterCrystals = 238;
        public const int OffLightCrystals = 240;
        public const int OffDarkCrystals = 242;
        public const int OffDeeds = 244;
        public const int OffPadding01 = 246;

        // ---- uint8 fields (compiler OFF_PD.*) ----
        public const int OffSynergyFire = 68;
        public const int OffSynergyIce = 69;
        public const int OffSynergyWind = 70;
        public const int OffSynergyEarth = 71;
        public const int OffSynergyLightning = 72;
        public const int OffSynergyWater = 73;
        public const int OffSynergyLight = 74;
        public const int OffSynergyDark = 75;
        public const int OffWizenedTunnel = 96;
        public const int OffWizenedMorion = 97;
        public const int OffWizenedPhantom = 98;
        public const int OffUnknown67 = 99;
        public const int OffEchelon5th = 180;
        public const int OffEchelon4th = 181;
        public const int OffEchelon3rd = 182;
        public const int OffEchelon2nd = 183;
        public const int OffEchelon1st = 184;
        public const int OffCaveConservation = 185;
        public const int OffImperialArmyIdTags = 186;
        public const int OffOpCredits = 187;
        public const int OffMoblinPheromoneSacks = 200;
        public const int OffUnknownCD = 201;
        public const int OffRemsTale1 = 202;
        public const int OffRemsTale2 = 203;
        public const int OffRemsTale3 = 204;
        public const int OffRemsTale4 = 205;
        public const int OffRemsTale5 = 206;
        public const int OffRemsTale6 = 207;
        public const int OffRemsTale7 = 208;
        public const int OffRemsTale8 = 209;
        public const int OffRemsTale9 = 210;
        public const int OffRemsTale10 = 211;   // last u8 before the uint64 run -> run starts at 212

        // ---- generic little-endian scalar accessors keyed by a compiler offset ----
        public static int GetI32(ReadOnlySpan<byte> b, int off) => BitConverter.ToInt32(b.Slice(off, 4));
        public static void SetI32(Span<byte> b, int off, int v) => BitConverter.TryWriteBytes(b.Slice(off, 4), v);
        public static ushort GetU16(ReadOnlySpan<byte> b, int off) => BitConverter.ToUInt16(b.Slice(off, 2));
        public static void SetU16(Span<byte> b, int off, ushort v) => BitConverter.TryWriteBytes(b.Slice(off, 2), v);
        public static byte GetU8(ReadOnlySpan<byte> b, int off) => b[off];
        public static void SetU8(Span<byte> b, int off, byte v) => b[off] = v;

        // ---- plans uint64 run: read the WHOLE 8-byte unit as one uint64, extract/insert an intra-unit bit field ----
        public static ulong GetPlansWord(ReadOnlySpan<byte> b) => BitConverter.ToUInt64(b.Slice(OffPlans, 8));

        public static uint GetPlanBloodshed(ReadOnlySpan<byte> b) => Plan(b, PlansUnit.BloodshedBit, PlansUnit.BloodshedWidth);
        public static void SetPlanBloodshed(Span<byte> b, uint v) => SetPlan(b, PlansUnit.BloodshedBit, PlansUnit.BloodshedWidth, v);
        public static uint GetPlanUmbrage(ReadOnlySpan<byte> b) => Plan(b, PlansUnit.UmbrageBit, PlansUnit.UmbrageWidth);
        public static void SetPlanUmbrage(Span<byte> b, uint v) => SetPlan(b, PlansUnit.UmbrageBit, PlansUnit.UmbrageWidth, v);
        public static uint GetPlanRitualistic(ReadOnlySpan<byte> b) => Plan(b, PlansUnit.RitualisticBit, PlansUnit.RitualisticWidth);
        public static uint GetPlanTutelary(ReadOnlySpan<byte> b) => Plan(b, PlansUnit.TutelaryBit, PlansUnit.TutelaryWidth);
        public static uint GetPlanPrimacy(ReadOnlySpan<byte> b) => Plan(b, PlansUnit.PrimacyBit, PlansUnit.PrimacyWidth);
        public static uint GetPlanUnused(ReadOnlySpan<byte> b) => Plan(b, PlansUnit.UnusedBit, PlansUnit.UnusedWidth);

        // straddling read/write inside the uint64 unit: (word >> bit) & ((1<<width)-1), read-modify-write preserves
        // siblings. `bit` is intra-unit (0..63), so a 9-bit field starting at bit 9 spans bytes 1-2 of the unit.
        private static uint Plan(ReadOnlySpan<byte> b, int bit, int width)
        {
            ulong w = GetPlansWord(b);
            ulong mask = width >= 64 ? ulong.MaxValue : ((1UL << width) - 1UL);
            return (uint)((w >> bit) & mask);
        }

        private static void SetPlan(Span<byte> b, int bit, int width, uint val)
        {
            ulong w = BitConverter.ToUInt64(b.Slice(OffPlans, 8));
            ulong mask = width >= 64 ? ulong.MaxValue : ((1UL << width) - 1UL);
            w = (w & ~(mask << bit)) | (((ulong)val & mask) << bit);
            BitConverter.TryWriteBytes(b.Slice(OffPlans, 8), w);
        }
    }
}
