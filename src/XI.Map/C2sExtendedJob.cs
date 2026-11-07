// C# wire model for the Phase-3 map C2S body struct GP_CLI_COMMAND_EXTENDED_JOB (0x102),
// src/map/packets/c2s/0x102_extended_job.h via the GP_CLI_PACKET(...) macro. FOURTH Phase-3 union body.
// Offsets are HEADER-INCLUSIVE (the 4-byte GP_CLI_HEADER is bytes 0..3), matching C2sPos/C2sAuc/C2sFaqGmcall.
//
// CRITICAL METHODOLOGY: every offset/size/mask below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_c2s_extjob_union_harness.cpp -> map_c2s_extjob_union_golden.txt), NOT hand-computed.
//
// New union/bit-field facts this body pins (no prior chunk 15/16/17/18 showed them):
//   - A BIT-FIELD STRUCT NESTED INSIDE A UNION ARM: mon_data_t (a union arm) contains mon_flags0_t, whose eight
//     uint8_t:1 bit-fields pack into ONE byte (size 1 / align 1). offsetof is ILLEGAL on the bit-fields (C2618),
//     so each field's byte+mask+shift came from the oracle's RUNTIME SENTINEL SCAN (set one field to 1, find the
//     single changed byte). The bit struct is reached at body offset OFF_EJ.Data_MON_Flags0 == 10 (Data@4 + Flags0@6).
//   - UNION ARMS OF EQUAL SIZE (all 160) BUT UNEQUAL ALIGNMENT: blu_data_t(160/1) and pup_data_t(160/1) are pure
//     uint8, mon_data_t(160/2) has u16 members. Union SIZE == max arm size (160); union ALIGN == max arm ALIGN (2)
//     -- a max-align case where all arms happen to be the same size (AUC 0x04e had mixed sizes AND mixed aligns).
//   - The u16 SpeciesIndex realigns to arm offset 8 right after the two bytes Flags0@6 / unknown01@7 with NO
//     extra pad (8 is already 2-aligned) -- a compiler-decided re-align the port mirrors exactly.
//   - CROSS-ARM raw-vs-structured aliasing at body offset 12: BLU.Spells[0] (u8), PUP.Slots[0] (u8) AND
//     MON.SpeciesIndex (u16) all view the same bytes.
//   - A 14-member hand-authored enum AutomatonSlot (uint8_t, Head=0..Attachment12=13) -- the largest literal set
//     pinned so far; it is NOT a struct member (only the meaning of pup Slots[14]) -> literals pinned, no member offset.
//
// The GP_CLI_HEADER id:9|size:7|sync framing is shared with the other C2S structs (XI.Map.C2sPos owns the
// WriteHeader/ReadId helpers, reused unchanged here).

using System;

namespace XI.Map
{
    // AutomatonSlot (0x102_extended_job.h). Hand-authored uint8_t literals; pinned vs ENUM_* lines.
    public enum AutomatonSlot : byte
    {
        Head        = 0,    // ENUM_Head=0
        Frame       = 1,    // ENUM_Frame=1
        Attachment1 = 2,    // ENUM_Att1=2
        Attachment2 = 3,
        Attachment3 = 4,
        Attachment4 = 5,
        Attachment5 = 6,
        Attachment6 = 7,
        Attachment7 = 8,
        Attachment8 = 9,
        Attachment9 = 10,
        Attachment10 = 11,
        Attachment11 = 12,
        Attachment12 = 13,
    }

    // mon_flags0_t -- eight uint8_t:1 bit-fields packed into ONE byte (size 1 / align 1). Byte/mask/shift all
    // from the oracle sentinel scan (offsetof illegal on a bit-field). These are Flags0-relative (byte 0).
    public static class MonFlags0
    {
        public const int SizeOf = 1;      // FLG_SIZE=1
        public const int AlignOf = 1;     // FLG_ALIGN=1

        public const int Off = 0;         // every BF_*_off=0 (all eight live in Flags0's single byte)
        public const int SpeciesMask = 0x01;     public const int SpeciesShift = 0;     // BF_SpeciesFlag_mask/shift
        public const int Unknown01Mask = 0x02;   public const int Unknown01Shift = 1;   // BF_unknown_0_1
        public const int InstinctMask = 0x04;    public const int InstinctShift = 2;    // BF_InstinctFlag
        public const int Desc1Mask = 0x08;       public const int Desc1Shift = 3;       // BF_Descriptor1Flag
        public const int Desc2Mask = 0x10;       public const int Desc2Shift = 4;       // BF_Descriptor2Flag
        public const int Unknown11Mask = 0x20;   public const int Unknown11Shift = 5;   // BF_unknown_1_1
        public const int Unknown12Mask = 0x40;   public const int Unknown12Shift = 6;   // BF_unknown_1_2
        public const int Unknown13Mask = 0x80;   public const int Unknown13Shift = 7;   // BF_unknown_1_3
    }

    // blu_data_t (union arm, all uint8 -> align 1). Offsets arm-relative. Size 160.
    public static class BluData
    {
        public const int SizeOf = 160;       // BLU_SIZE=160
        public const int AlignOf = 1;        // BLU_ALIGN=1
        public const int OffSpellId = 0;     // OFF_BLU.SpellId=0 (u8)
        public const int OffUnknown00 = 1;
        public const int OffPadding00 = 2;   // u8[2]
        public const int OffJobIndex = 4;    // OFF_BLU.JobIndex=4 (u8)
        public const int OffSupportJobFlg = 5;
        public const int OffPadding01 = 6;   // u8[2]
        public const int OffSpells = 8;      // OFF_BLU.Spells=8 (u8[20])
        public const int SpellsLen = 20;     // BLU_Spells_len=20
        public const int OffUnused00 = 28;   // OFF_BLU.unused00=28 (u8[132])
    }

    // pup_data_t (union arm, all uint8 -> align 1). Offsets arm-relative. Size 160.
    public static class PupData
    {
        public const int SizeOf = 160;       // PUP_SIZE=160
        public const int AlignOf = 1;        // PUP_ALIGN=1
        public const int OffItemId = 0;      // OFF_PUP.ItemId=0 (u8)
        public const int OffUnknown00 = 1;
        public const int OffPadding00 = 2;
        public const int OffJobIndex = 4;
        public const int OffSupportJobFlg = 5;
        public const int OffPadding01 = 6;
        public const int OffSlots = 8;       // OFF_PUP.Slots=8 (u8[14], each an AutomatonSlot value)
        public const int SlotsLen = 14;      // PUP_Slots_len=14 / PUP_Slots_count=14
        public const int OffUnused00 = 22;   // OFF_PUP.unused00=22 (u8[138])
    }

    // mon_data_t (union arm, has u16 members -> align 2). Offsets arm-relative. Size 160.
    // Contains the mon_flags0_t bit struct at Flags0@6; SpeciesIndex(u16) realigns to 8 (no extra pad).
    public static class MonData
    {
        public const int SizeOf = 160;       // MON_SIZE=160
        public const int AlignOf = 2;        // MON_ALIGN=2
        public const int OffUnknown00 = 0;   // OFF_MON.unknown00=0 (u8[6])
        public const int OffFlags0 = 6;      // OFF_MON.Flags0=6 (mon_flags0_t, 1 byte of 8 bit-fields)
        public const int OffUnknown01 = 7;   // OFF_MON.unknown01=7 (u8)
        public const int OffSpeciesIndex = 8; // OFF_MON.SpeciesIndex=8 (u16; 8 already 2-aligned after the two bytes)
        public const int OffUnknown02 = 10;  // u16
        public const int OffSlots = 12;      // OFF_MON.Slots=12 (u16[12])
        public const int SlotsLen = 24;      // MON_Slots_len=24
        public const int SlotsCount = 12;    // MON_Slots_count=12
        public const int OffDescriptor1Index = 36; // OFF_MON.Descriptor1Index=36 (u8)
        public const int OffDescriptor2Index = 37; // u8
        public const int OffUnused00 = 38;   // OFF_MON.unused00=38 (u8[122]; 38+122==160 gapless)
    }

    // EXTJOB_DATA = the union member `Data` = union{ blu_data_t bluData; pup_data_t pupData; mon_data_t monData }.
    // Three arms, all alias @0. size == max arm size (160), align == max arm align (2, from mon_data_t).
    public static class ExtJobData
    {
        public const int SizeOf = 160;       // DATA_SIZE=160
        public const int AlignOf = 2;        // DATA_ALIGN=2 (== max(1,1,2); mon arm is the aligner)
        public const int OffBluData = 0;     // OFF_DATA.bluData=0
        public const int OffPupData = 0;     // OFF_DATA.pupData=0 (aliases blu -- the union fact)
        public const int OffMonData = 0;     // OFF_DATA.monData=0
    }

    // GP_CLI_COMMAND_EXTENDED_JOB (outer, header-inclusive, default packing). size=164, align=2.
    public static class C2sExtendedJob
    {
        // GP_CLI_HEADER prefix (id:9|size:7|sync) -- same 4-byte framing as the other C2S structs.
        public const int OffHeader = 0;       // OFF_EJ.header=0
        public const int HeaderSize = 4;      // CHDR_SIZE=4
        public const int OffSync = 2;

        public const int SizeOf = 164;        // EJ_SIZE=164 (== header 4 + union 160)
        public const int AlignOf = 2;         // EJ_ALIGN=2 (== union align)

        public const int OffData = 4;         // OFF_EJ.Data=4 (the union starts right after the header)

        // nested-inclusive offset helpers (header start + arm base + arm-relative field):
        public static int BluField(int off) => OffData + ExtJobData.OffBluData + off;   // arm base == 0
        public static int PupField(int off) => OffData + ExtJobData.OffPupData + off;
        public static int MonField(int off) => OffData + ExtJobData.OffMonData + off;

        public static int BluSpellsOffset() => BluField(BluData.OffSpells);   // 12
        public static int PupSlotsOffset() => PupField(PupData.OffSlots);      // 12 (aliases BluSpells: cross-arm)
        public static int MonFlags0Offset() => MonField(MonData.OffFlags0);    // 10 (OFF_EJ.Data_MON_Flags0)
        public static int MonSpeciesIndexOffset() => MonField(MonData.OffSpeciesIndex); // 12 (OFF_EJ.Data_MON_SpeciesIndex)

        // ---- BLU arm (u8 fields) -------------------------------------------------------------------
        public static byte GetSpellId(ReadOnlySpan<byte> b) => b[BluField(BluData.OffSpellId)];
        public static void SetSpellId(Span<byte> b, byte v) => b[BluField(BluData.OffSpellId)] = v;
        public static byte GetBluJobIndex(ReadOnlySpan<byte> b) => b[BluField(BluData.OffJobIndex)];
        public static void SetBluJobIndex(Span<byte> b, byte v) => b[BluField(BluData.OffJobIndex)] = v;
        public static byte GetBluSpell(ReadOnlySpan<byte> b, int i) => b[BluField(BluData.OffSpells) + i];
        public static void SetBluSpell(Span<byte> b, int i, byte v) => b[BluField(BluData.OffSpells) + i] = v;
        public static ReadOnlySpan<byte> GetBluUnused00(ReadOnlySpan<byte> b) => b.Slice(BluField(BluData.OffUnused00), 132);

        // ---- PUP arm (u8 fields; Slots values are AutomatonSlot literals) --------------------------
        public static byte GetItemId(ReadOnlySpan<byte> b) => b[PupField(PupData.OffItemId)];
        public static void SetItemId(Span<byte> b, byte v) => b[PupField(PupData.OffItemId)] = v;
        public static byte GetPupSlot(ReadOnlySpan<byte> b, int i) => b[PupField(PupData.OffSlots) + i];
        public static void SetPupSlot(Span<byte> b, int i, byte v) => b[PupField(PupData.OffSlots) + i] = v;

        // ---- MON arm (the bit struct + u16 fields) -------------------------------------------------
        public static byte GetFlags0Byte(ReadOnlySpan<byte> b) => b[MonFlags0Offset()];
        public static void SetFlags0Byte(Span<byte> b, byte v) => b[MonFlags0Offset()] = v;

        // bit-field accessors: read = (byte & mask) != 0; write = read-modify-write preserving the sibling bits.
        public static bool GetSpeciesFlag(ReadOnlySpan<byte> b) => (GetFlags0Byte(b) & MonFlags0.SpeciesMask) != 0;
        public static void SetSpeciesFlag(Span<byte> b, bool v) => SetFlags0Byte(b, (byte)((GetFlags0Byte(b) & ~MonFlags0.SpeciesMask) | (v ? MonFlags0.SpeciesMask : 0)));
        public static bool GetInstinctFlag(ReadOnlySpan<byte> b) => (GetFlags0Byte(b) & MonFlags0.InstinctMask) != 0;
        public static void SetInstinctFlag(Span<byte> b, bool v) => SetFlags0Byte(b, (byte)((GetFlags0Byte(b) & ~MonFlags0.InstinctMask) | (v ? MonFlags0.InstinctMask : 0)));
        public static bool GetDescriptor1Flag(ReadOnlySpan<byte> b) => (GetFlags0Byte(b) & MonFlags0.Desc1Mask) != 0;
        public static void SetDescriptor1Flag(Span<byte> b, bool v) => SetFlags0Byte(b, (byte)((GetFlags0Byte(b) & ~MonFlags0.Desc1Mask) | (v ? MonFlags0.Desc1Mask : 0)));
        public static bool GetDescriptor2Flag(ReadOnlySpan<byte> b) => (GetFlags0Byte(b) & MonFlags0.Desc2Mask) != 0;
        public static void SetDescriptor2Flag(Span<byte> b, bool v) => SetFlags0Byte(b, (byte)((GetFlags0Byte(b) & ~MonFlags0.Desc2Mask) | (v ? MonFlags0.Desc2Mask : 0)));

        public static ushort GetSpeciesIndex(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(MonSpeciesIndexOffset(), 2));
        public static void SetSpeciesIndex(Span<byte> b, ushort v) => BitConverter.TryWriteBytes(b.Slice(MonSpeciesIndexOffset(), 2), v);
        public static ushort GetMonSlot(ReadOnlySpan<byte> b, int i) => BitConverter.ToUInt16(b.Slice(MonField(MonData.OffSlots) + i * 2, 2));
        public static void SetMonSlot(Span<byte> b, int i, ushort v) => BitConverter.TryWriteBytes(b.Slice(MonField(MonData.OffSlots) + i * 2, 2), v);
        public static byte GetDescriptor1Index(ReadOnlySpan<byte> b) => b[MonField(MonData.OffDescriptor1Index)];
        public static void SetDescriptor1Index(Span<byte> b, byte v) => b[MonField(MonData.OffDescriptor1Index)] = v;
    }
}
