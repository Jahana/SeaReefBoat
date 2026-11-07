// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_CURRENCIES_2::PacketData (0x118),
// src/map/packets/s2c/0x118_currencies_2.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cCurrencies1 convention.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_currencies_2_harness.cpp -> map_s2c_currencies_2_golden.txt), NOT hand-computed.
//
// THE genuinely-new packing fact this chunk exists to pin (contrast chunk 28's 0x113):
//   currencies_1 (0x113) was #pragma pack(push,1): alignof 1, offsets == pure running byte sum.
//   currencies_2 (0x118) has NO #pragma pack -> DEFAULT (natural) packing: PD_ALIGN=4 (largest member int32_t), so the
//   compiler is FREE to insert alignment pad before any int32 following an odd-length u8/u16 run. This particular body
//   happens to be HOLE-FREE -- the oracle proves it with PAD_INSERTED=0 (MEMBER_BYTES=156 == PD_SIZE=156) -- but NOT
//   for the pack(1) reason. The two DECLARED padding arrays do the aligning work:
//     padding00[3] @121..123 is exactly the 3 bytes natural alignment needs before silver_aman_vouchers_stored @124
//     (124 % 4 == 0); padding02 @146..147 is exactly the 2 bytes needed before temenos_units @148 (148 % 4 == 0).
//   A hand-reasoner who treated the "padding" arrays as non-data and dropped them would misalign everything from byte
//   121 onward; one who assumed "offsets == running sum" (true for the pack(1) sibling) is accidentally correct here
//   ONLY because the padding is explicit. AlignOf=4 here is a compiler fact, NOT the chunk-28 pack(1) AlignOf=1.
//   No bit-fields / engine enums / nested structs / unions -> pure fixed-width scalar chain; the generic span accessors
//   below are keyed by compiler offset (same realistic surface as a ~110-field flat body).
//
// Layout (DEFAULT packing, body-relative). sizeof=156, alignof=4:
//   i32 bayld@0 | u16 kinetic@4 | u8 coalition@6 mystical@7 | i32 obsidian@8 | u16 lebondopt@12 pulchridopt@14
//   i32 mweya@16 | u8 x45 stone groups (ghastly..duskorb)@20..64 | u8 pellucid@65 fern@66 taupe@67
//   u16 mellidopt@68 escha_beads@70 | i32 escha_silt@72 potpourri@76 hallmarks@80 total_hallmarks@84 badges@88
//     crafter_points@92 | u8 crystals_set@96..103 mc_i_sr@104..106 spheres_set@107..120 | u8 padding00[3]@121..123
//   i32 silver_aman@124 domain_points@128 domain_points_earned_today@132 mog_segments@136 gallimaufry@140
//   u16 is_accolades@144 padding02@146 | i32 temenos_units@148 apollyon_units@152 (152+4==156, no tail pad)

using System;

namespace XI.Map
{
    // GP_SERV_COMMAND_CURRENCIES_2::PacketData (outer, body-relative, DEFAULT packing). sizeof=156, alignof=4.
    // Offsets transcribed verbatim from the compiled oracle (OFF_PD.* keys); accessors are generic little-endian span
    // reads by offset -- the realistic surface for a ~99-field flat body (bespoke Get/Set per field would be ~400
    // hand-written methods prone to transcription error; the gate drives the same accessors over the compiler offsets).
    public static class S2cCurrencies2
    {
        public const int SizeOf  = 156; // PD_SIZE=156 (MEMBER_BYTES==156, PAD_INSERTED==0)
        public const int AlignOf = 4;   // PD_ALIGN=4 -- DEFAULT packing (contrast chunk-28 currencies_1 pack(1)=1)

        // ---- int32 fields (compiler OFF_PD.*) ----
        public const int OffBayld = 0;
        public const int OffObsidianFragments = 8;
        public const int OffMweyaPlasmCorpuscles = 16;
        public const int OffEschaSilt = 72;
        public const int OffPotpourri = 76;
        public const int OffHallmarks = 80;
        public const int OffTotalHallmarks = 84;
        public const int OffBadgesOfGallantry = 88;
        public const int OffCrafterPoints = 92;
        public const int OffSilverAmanVouchers = 124;   // @124 == 4-aligned; reached only past padding00[3]@121..123
        public const int OffDomainPoints = 128;
        public const int OffDomainPointsEarnedToday = 132;
        public const int OffMogSegments = 136;
        public const int OffGallimaufry = 140;
        public const int OffTemenosUnits = 148;         // @148 == 4-aligned; reached past padding02@146..147
        public const int OffApollyonUnits = 152;

        // ---- uint16 fields (compiler OFF_PD.*) ----
        public const int OffKineticUnits = 4;
        public const int OffLebondoptWings = 12;
        public const int OffPulchridoptWings = 14;
        public const int OffMellidoptWings = 68;
        public const int OffEschaBeads = 70;
        public const int OffIsAccolades = 144;
        public const int OffPadding02 = 146;            // declared u16: the align filler before temenos_units@148

        // ---- uint8 fields (compiler OFF_PD.*) ----
        public const int OffCoalitionImprimaturs = 6;
        public const int OffMysticalCanteens = 7;
        public const int OffGhastlyStones0 = 20;  public const int OffGhastlyStones1 = 21;  public const int OffGhastlyStones2 = 22;
        public const int OffVerdigrisStones0 = 23; public const int OffVerdigrisStones1 = 24; public const int OffVerdigrisStones2 = 25;
        public const int OffWailingStones0 = 26;   public const int OffWailingStones1 = 27;   public const int OffWailingStones2 = 28;
        public const int OffSnowslitStones0 = 29;  public const int OffSnowslitStones1 = 30;  public const int OffSnowslitStones2 = 31;
        public const int OffSnowtipStones0 = 32;   public const int OffSnowtipStones1 = 33;   public const int OffSnowtipStones2 = 34;
        public const int OffSnowdimStones0 = 35;   public const int OffSnowdimStones1 = 36;   public const int OffSnowdimStones2 = 37;
        public const int OffSnoworbStones0 = 38;   public const int OffSnoworbStones1 = 39;   public const int OffSnoworbStones2 = 40;
        public const int OffLeafslitStones0 = 41;  public const int OffLeafslitStones1 = 42;  public const int OffLeafslitStones2 = 43;
        public const int OffLeaftipStones0 = 44;   public const int OffLeaftipStones1 = 45;   public const int OffLeaftipStones2 = 46;
        public const int OffLeafdimStones0 = 47;   public const int OffLeafdimStones1 = 48;   public const int OffLeafdimStones2 = 49;
        public const int OffLeaforbStones0 = 50;   public const int OffLeaforbStones1 = 51;   public const int OffLeaforbStones2 = 52;
        public const int OffDuskslitStones0 = 53;  public const int OffDuskslitStones1 = 54;  public const int OffDuskslitStones2 = 55;
        public const int OffDusktipStones0 = 56;   public const int OffDusktipStones1 = 57;   public const int OffDusktipStones2 = 58;
        public const int OffDuskdimStones0 = 59;   public const int OffDuskdimStones1 = 60;   public const int OffDuskdimStones2 = 61;
        public const int OffDuskorbStones0 = 62;   public const int OffDuskorbStones1 = 63;   public const int OffDuskorbStones2 = 64;
        public const int OffPellucidStones = 65;
        public const int OffFernStones = 66;
        public const int OffTaupeStones = 67;
        public const int OffFireCrystalsSet = 96;   public const int OffIceCrystalsSet = 97;
        public const int OffWindCrystalsSet = 98;   public const int OffEarthCrystalsSet = 99;
        public const int OffLightningCrystalsSet = 100; public const int OffWaterCrystalsSet = 101;
        public const int OffLightCrystalsSet = 102; public const int OffDarkCrystalsSet = 103;
        public const int OffMcISr01sSet = 104; public const int OffMcISr02sSet = 105; public const int OffMcISr03sSet = 106;
        public const int OffLiquefactionsSpheres = 107; public const int OffIndurationSpheres = 108;
        public const int OffDenotationSpheres = 109;    public const int OffScissionSpheres = 110;
        public const int OffImpactionSpheres = 111;     public const int OffReverberationSpheres = 112;
        public const int OffTransfixionSpheres = 113;   public const int OffCompressionSpheres = 114;
        public const int OffFusionSpheres = 115;        public const int OffDistortionSpheres = 116;
        public const int OffFragmentationSpheres = 117; public const int OffGravitationSpheres = 118;
        public const int OffLightSpheres = 119;         public const int OffDarknessSpheres = 120;
        public const int OffPadding00 = 121;            // declared u8[3]: the align filler before silver_aman@124
        public const int Padding00Len = 3;

        // ---- generic little-endian scalar accessors keyed by a compiler offset ----
        public static int GetI32(ReadOnlySpan<byte> b, int off) => BitConverter.ToInt32(b.Slice(off, 4));
        public static void SetI32(Span<byte> b, int off, int v) => BitConverter.TryWriteBytes(b.Slice(off, 4), v);
        public static ushort GetU16(ReadOnlySpan<byte> b, int off) => BitConverter.ToUInt16(b.Slice(off, 2));
        public static void SetU16(Span<byte> b, int off, ushort v) => BitConverter.TryWriteBytes(b.Slice(off, 2), v);
        public static byte GetU8(ReadOnlySpan<byte> b, int off) => b[off];
        public static void SetU8(Span<byte> b, int off, byte v) => b[off] = v;

        // padding00[3] / padding02 are real wire bytes (they do the alignment work); expose the slice for completeness.
        public static ReadOnlySpan<byte> GetPadding00(ReadOnlySpan<byte> b) => b.Slice(OffPadding00, Padding00Len);
    }
}
