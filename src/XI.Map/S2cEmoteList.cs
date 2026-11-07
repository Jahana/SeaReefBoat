// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_EMOTE_LIST::PacketData (0x11A),
// src/map/packets/s2c/0x11a_emote_list.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cInfluenceCampaign/S2cCurrencies1 convention.
//
// CRITICAL METHODOLOGY: every offset/bit-position/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_emote_list_harness.cpp -> map_s2c_emote_list_golden.txt), NOT hand-computed.
//
// THE genuinely-new packing facts this chunk exists to pin (no prior chunk 1-29 exercised them):
//   (1) FIRST uint16-allocation-unit bit-field run. chairemotes_t is eleven `uint16_t : 1` + one `uint16_t : 5` =
//       EXACTLY 16 bits = ONE uint16 allocation unit (chunks 12-27 scanned bit-field runs packed into uint8/uint32
//       units; chunk 28 introduced the uint64 unit). A uint16 word MUST be read with BitConverter.ToUInt16 and
//       shifted by the intra-unit bit then masked -- the chunk-26/27 BitWord helper is ToUInt32-only, so this file
//       adds BitWord16 (the same read-modify-write pattern over a 2-byte word).
//   (2) A 22-wide uint32 one-bit-flag run -- the densest single-unit flag word yet (chunk 26 campaignzone run1 was
//       5 fields). jobemotes_t = 22x `uint32_t : 1` (intra-bits 0..21) + `unused : 10` at bit 22. The trailing :10
//       sits at bit 22 -> bits 22..31 CROSS the byte 2/3 boundary INSIDE the uint32 (22 % 8 + 10 == 16 > 8), so it
//       must be read as one BitConverter.ToUInt32 word (a byte-2-only mask drops the 2 bits that live in byte 3).
//   (3) TWO sibling bit-field structs with DIFFERENT allocation units (uint32 jobemotes 4/4 + uint16 chairemotes 2/2)
//       packed BACK-TO-BACK in a DEFAULT-PACKED body. The uint32 JobEmotes sits @0, the uint16 Chairs @4 (4 is
//       already 2-aligned -> NO realign), uint16 padding00 @6 (4 + 2 == 6, already 2-aligned). The WHOLE struct's
//       alignof = MAX of its members' aligns = 4 (from the uint32), NOT 1 or 2 -> PD 8/4 (6 + 2 == 8, no tail pad).
//       Contrast chunk 28 (pack(1) collapsed even a uint64's align to 1): here DEFAULT packing takes the max align.
//
// No engine enum (all members are bit-fields / plain uint16) -> no .h re-parse; the body is pure stdint.
//
// Layout (body-relative, DEFAULT packing): PD_EMOTE 8/4 -- jobemotes_t JobEmotes@0 (4/4: u32 WAR:0 MNK:1 WHM:2 BLM:3
//   RDM:4 THF:5 PLD:6 DRK:7 BST:8 BRD:9 RNG:10 SAM:11 NIN:12 DRG:13 SMN:14 BLU:15 COR:16 PUP:17 DNC:18 SCH:19 GEO:20
//   RUN:21 + unused:10@b22); chairemotes_t Chairs@4 (2/2: u16 Chair1:0..Chair11:10 + unused:5@b11); u16 padding00@6
//   (6 + 2 == 8 == SizeOf, align 4 -> no tail pad).

using System;

namespace XI.Map
{
    // ---- shared bit-field word primitive for a uint16 allocation unit ------------------------------------
    // chairemotes_t's twelve uint16 bit-fields pack into ONE 16-bit word. A field's bits are (word >> bitpos) &
    // ((1<<width)-1); a write is read-modify-write of the whole uint16 so sibling fields sharing the unit survive.
    // The chunk-26/27 BitWord helper does the same over a uint32; this is its uint16 counterpart (chunk 30's need).
    internal static class BitWord16
    {
        public static uint Get(ReadOnlySpan<byte> body, int wordOff, int bitpos, int width)
        {
            uint w = BitConverter.ToUInt16(body.Slice(wordOff, 2));
            uint mask = width >= 16 ? 0xFFFFu : ((1u << width) - 1u);
            return (w >> bitpos) & mask;
        }

        public static void Set(Span<byte> body, int wordOff, int bitpos, int width, uint val)
        {
            uint mask = width >= 16 ? 0xFFFFu : ((1u << width) - 1u);
            uint w = BitConverter.ToUInt16(body.Slice(wordOff, 2));
            w = (w & ~(mask << bitpos)) | ((val & mask) << bitpos);
            BitConverter.TryWriteBytes(body.Slice(wordOff, 2), (ushort)w);
        }
    }

    // jobemotes_t -- one uint32 of 22 one-bit job flags + a 10-bit pad. sizeof=4, align=4. Bit positions are
    // (BF_jobemotes_t_*_off * 8 + shift); every field shares word offset 0 (the body's byte 0).
    public static class JobEmotesEntry
    {
        public const int SizeOf = 4;   // JE_SIZE=4
        public const int AlignOf = 4;  // JE_ALIGN=4 (the uint32 allocation unit)

        public const int WARBit = 0;   // off0 shift0
        public const int MNKBit = 1;   // off0 shift1
        public const int WHMBit = 2;
        public const int BLMBit = 3;
        public const int RDMBit = 4;
        public const int THFBit = 5;
        public const int PLDBit = 6;
        public const int DRKBit = 7;
        public const int BSTBit = 8;   // off1 shift0 -> 8
        public const int BRDBit = 9;
        public const int RNGBit = 10;
        public const int SAMBit = 11;
        public const int NINBit = 12;
        public const int DRGBit = 13;
        public const int SMNBit = 14;
        public const int BLUBit = 15;
        public const int CORBit = 16;  // off2 shift0 -> 16
        public const int PUPBit = 17;
        public const int DNCBit = 18;
        public const int SCHBit = 19;
        public const int GEOBit = 20;
        public const int RUNBit = 21;
        public const int UnusedBit = 22;    // off2 shift6 -> 16+6=22  (STRADDLES bytes 2/3: 22%8 + 10 == 16 > 8)
        public const int UnusedWidth = 10;
        public const int FlagWidth = 1;      // the 22 job flags are each 1 bit (compiler BF_*_width all 1)

        // 22 one-bit flags + a 10-bit pad == exactly 32 bits == one uint32 allocation unit.
        public static bool SumsTo32() => 22 * FlagWidth + UnusedWidth == 32;
    }

    // chairemotes_t -- ONE uint16 of 11 one-bit chair flags + a 5-bit pad. sizeof=2, align=2. THE first
    // uint16-allocation-unit bit-field run (read with BitWord16, not the uint32 BitWord).
    public static class ChairEmotesEntry
    {
        public const int SizeOf = 2;   // CH_SIZE=2
        public const int AlignOf = 2;  // CH_ALIGN=2 (the uint16 allocation unit)

        public const int Chair1Bit = 0;   // off0 shift0
        public const int Chair2Bit = 1;
        public const int Chair3Bit = 2;
        public const int Chair4Bit = 3;
        public const int Chair5Bit = 4;
        public const int Chair6Bit = 5;
        public const int Chair7Bit = 6;
        public const int Chair8Bit = 7;   // off0 shift7 -> bit 7 (last bit of the low byte)
        public const int Chair9Bit = 8;   // off1 shift0 -> bit 8 (STRADDLES the word: the unit spans bytes 0/1)
        public const int Chair10Bit = 9;  // off1 shift1
        public const int Chair11Bit = 10; // off1 shift2
        public const int UnusedBit = 11;  // off1 shift3 -> 8+3=11  (bits 11..15 sit inside byte 1, do NOT straddle out)
        public const int UnusedWidth = 5;
        public const int FlagWidth = 1;

        // 11 one-bit chair flags + a 5-bit pad == exactly 16 bits == one uint16 allocation unit.
        public static bool SumsTo16() => 11 * FlagWidth + UnusedWidth == 16;
    }

    // GP_SERV_COMMAND_EMOTE_LIST::PacketData (outer, body-relative, DEFAULT packing). sizeof=8, alignof=4.
    public static class S2cEmoteList
    {
        public const int SizeOf = 8;    // PD_SIZE=8 (padding00@6 + 2 == 8, align 4 -> NO tail pad)
        public const int AlignOf = 4;   // PD_ALIGN=4 == max(JobEmotes 4, Chairs 2, u16 2) -- DEFAULT packing takes the MAX align

        public const int OffJobEmotes = 0;  // OFF_PD.JobEmotes=0  (uint32 word offset)
        public const int OffChairs = 4;     // OFF_PD.Chairs=4  (uint16 word offset; 4 already 2-aligned -> no realign)
        public const int OffPadding00 = 6;  // OFF_PD.padding00=6 (uint16; 4 + 2 == 6, already 2-aligned)

        // ---- jobemotes (uint32 run, reuses the chunk-26/27 BitWord primitive) ----
        public static uint GetJobFlag(ReadOnlySpan<byte> b, int bitpos)
            => BitWord.Get(b, OffJobEmotes, bitpos, JobEmotesEntry.FlagWidth);
        public static void SetJobFlag(Span<byte> b, int bitpos, uint val)
            => BitWord.Set(b, OffJobEmotes, bitpos, JobEmotesEntry.FlagWidth, val);
        public static uint GetJobWAR(ReadOnlySpan<byte> b) => GetJobFlag(b, JobEmotesEntry.WARBit);
        public static uint GetJobMNK(ReadOnlySpan<byte> b) => GetJobFlag(b, JobEmotesEntry.MNKBit);
        public static uint GetJobRUN(ReadOnlySpan<byte> b) => GetJobFlag(b, JobEmotesEntry.RUNBit);
        public static uint GetJobUnused(ReadOnlySpan<byte> b)
            => BitWord.Get(b, OffJobEmotes, JobEmotesEntry.UnusedBit, JobEmotesEntry.UnusedWidth);
        public static void SetJobUnused(Span<byte> b, uint val)
            => BitWord.Set(b, OffJobEmotes, JobEmotesEntry.UnusedBit, JobEmotesEntry.UnusedWidth, val);

        // ---- chairemotes (uint16 run, the chunk-30 BitWord16 primitive) ----
        public static uint GetChairFlag(ReadOnlySpan<byte> b, int bitpos)
            => BitWord16.Get(b, OffChairs, bitpos, ChairEmotesEntry.FlagWidth);
        public static void SetChairFlag(Span<byte> b, int bitpos, uint val)
            => BitWord16.Set(b, OffChairs, bitpos, ChairEmotesEntry.FlagWidth, val);
        public static uint GetChair1(ReadOnlySpan<byte> b) => GetChairFlag(b, ChairEmotesEntry.Chair1Bit);
        public static uint GetChair8(ReadOnlySpan<byte> b) => GetChairFlag(b, ChairEmotesEntry.Chair8Bit);
        public static uint GetChair9(ReadOnlySpan<byte> b) => GetChairFlag(b, ChairEmotesEntry.Chair9Bit);   // bit 8 (crosses the unit's byte 0/1)
        public static uint GetChair11(ReadOnlySpan<byte> b) => GetChairFlag(b, ChairEmotesEntry.Chair11Bit);
        public static uint GetChairUnused(ReadOnlySpan<byte> b)
            => BitWord16.Get(b, OffChairs, ChairEmotesEntry.UnusedBit, ChairEmotesEntry.UnusedWidth);
        public static void SetChairUnused(Span<byte> b, uint val)
            => BitWord16.Set(b, OffChairs, ChairEmotesEntry.UnusedBit, ChairEmotesEntry.UnusedWidth, val);

        // ---- whole-word raw reads (used by the gate to prove the accessor == a direct uint32/uint16 read) ----
        public static uint GetJobWord(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(OffJobEmotes, 4));
        public static ushort GetChairWord(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(OffChairs, 2));

        // ---- trailing plain uint16 padding00 ----
        public static ushort GetPadding00(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(OffPadding00, 2));
        public static void SetPadding00(Span<byte> b, ushort val) => BitConverter.TryWriteBytes(b.Slice(OffPadding00, 2), val);
    }
}
