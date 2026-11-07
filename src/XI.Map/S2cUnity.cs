// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_UNITY::PacketData (0x110),
// src/map/packets/s2c/0x110_unity.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cCurrencies2 / S2cEmoteList convention.
//
// CRITICAL METHODOLOGY: every offset/size/bit-position below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_unity_harness.cpp -> map_s2c_unity_golden.txt), NOT hand-computed.
//
// THE genuinely-new packing fact this chunk exists to pin (contrast chunks 26/27/30):
//   FIRST WIDE (24-bit) bit-field inside a uint32 allocation unit. `Sparks : 24` occupies bits 0..23 (LSB at compiler
//   BF_*_off 0 / shift 0 -> bit 0; spans the uint32's bytes 0/1/2), then `unused00 : 8` fills bits 24..31 (LSB at
//   off 3 / shift 0 -> bit 24). The pair EXACTLY fills one uint32 (24 + 8 == 32), so both share word offset 0 and the
//   existing chunk-26 BitWord (BitConverter.ToUInt32 read-modify-write) primitive reads/writes them -- no new reader,
//   only a new width. A 24-bit field STRADDLES three byte boundaries: the gate proves a per-byte read is WRONG by
//   comparing the whole-word masked value against a single-byte (byte 0) read (they differ for a 24-bit value), the
//   same discipline as the chunks 26/27/28/30 straddle checks at a wider width.
//
//   The body also MIXES that bit-field word with plain fixed-width scalars and a trailing fixed ARRAY in one DEFAULT-
//   packed struct: uint32 word@0 | uint16 Deeds@4 | uint16 Plaudits@6 | uint8 RoEUnityShared@8 | uint8 RoEUnityLeader@9
//   | uint8 unknown0E[6]@10..15. AlignOf=4 comes from the uint32 (max member align), sizeof=16 (10+6, 16%4==0 -> no
//   tail pad), PAD_INSERTED=0 (hole-free). The unknown0E[6] is carried as a real wire byte-range slice (its declared
//   name is the byte 0x0E..0x13 region in the XiPackets numbering; the compiler places it at body offset 10).
//
// Layout (DEFAULT packing, body-relative). sizeof=16, alignof=4:
//   [00..03] uint32 unit: Sparks bits 0..23, unused00 bits 24..31
//   [04..05] uint16 Deeds        [06..07] uint16 Plaudits
//   [08]     uint8  RoEUnityShared  [09] uint8 RoEUnityLeader
//   [10..15] uint8 unknown0E[6]

using System;

namespace XI.Map
{
    // GP_SERV_COMMAND_UNITY::PacketData (outer, body-relative, DEFAULT packing). sizeof=16, alignof=4.
    public static class S2cUnity
    {
        public const int SizeOf  = 16; // PD_SIZE=16 (MEMBER_BYTES==16, PAD_INSERTED==0)
        public const int AlignOf = 4;  // PD_ALIGN=4 -- DEFAULT packing, max member align == the uint32 word

        // ---- the uint32 bit-field unit (all bit positions = compiler BF_*_off*8 + shift) ----
        public const int WordOffset   = 0;   // the uint32 that holds both bit-fields sits at body offset 0
        public const int SparksBit    = 0;   // BF_PD_UNITY_Sparks_off 0  *8 + shift 0 == bit 0
        public const int SparksWidth  = 24;  // width 24 -> straddles bytes 0/1/2 of the word
        public const int Unused00Bit  = 24;  // BF_PD_UNITY_unused00_off 3 *8 + shift 0 == bit 24
        public const int Unused00Width = 8;  // bits 24..31 -> the word's byte 3
        // The two declared widths must exactly fill the uint32 allocation unit (the oracle's 24+8==32 invariant).
        public static bool BitFieldsSumTo32() => SparksWidth + Unused00Width == 32;

        // ---- plain scalar fields (compiler OFF_PD.*) ----
        public const int OffDeeds          = 4;
        public const int OffPlaudits       = 6;
        public const int OffRoEUnityShared = 8;
        public const int OffRoEUnityLeader = 9;
        public const int OffUnknown0E      = 10;   // uint8[6] -> bytes 10..15
        public const int Unknown0ELen      = 6;

        // ---- bit-field accessors (reuse the chunk-26 uint32 BitWord primitive; RMW preserves the sibling) ----
        public static uint GetSparks(ReadOnlySpan<byte> b)  => BitWord.Get(b, WordOffset, SparksBit, SparksWidth);
        public static void SetSparks(Span<byte> b, uint v)  => BitWord.Set(b, WordOffset, SparksBit, SparksWidth, v);
        public static uint GetUnused00(ReadOnlySpan<byte> b) => BitWord.Get(b, WordOffset, Unused00Bit, Unused00Width);
        public static void SetUnused00(Span<byte> b, uint v) => BitWord.Set(b, WordOffset, Unused00Bit, Unused00Width, v);

        // ---- generic little-endian scalar accessors keyed by a compiler offset ----
        public static ushort GetU16(ReadOnlySpan<byte> b, int off) => BitConverter.ToUInt16(b.Slice(off, 2));
        public static void   SetU16(Span<byte> b, int off, ushort v) => BitConverter.TryWriteBytes(b.Slice(off, 2), v);
        public static byte   GetU8(ReadOnlySpan<byte> b, int off) => b[off];
        public static void   SetU8(Span<byte> b, int off, byte v) => b[off] = v;

        // ---- raw whole-word read for the gate's straddle proof (Sparks must use this, not a single byte) ----
        public static uint GetWord(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(WordOffset, 4));

        // ---- trailing uint8[6] is real wire bytes; expose the slice ----
        public static ReadOnlySpan<byte> GetUnknown0E(ReadOnlySpan<byte> b) => b.Slice(OffUnknown0E, Unknown0ELen);
        public static void SetUnknown0E(Span<byte> b, ReadOnlySpan<byte> v) => v.Slice(0, Unknown0ELen).CopyTo(b.Slice(OffUnknown0E, Unknown0ELen));
    }
}
