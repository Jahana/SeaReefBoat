// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_GROUP_TBL::PacketData (0x0c8),
// src/map/packets/s2c/0x0c8_group_tbl.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cRecipe/S2cBattlefield convention.
//
// CRITICAL METHODOLOGY: every offset/size/mask below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_group_tbl_enum_harness.cpp -> map_s2c_group_tbl_enum_golden.txt), NOT hand-computed.
//
// THIRD engine-enum body and the layout-DENSEST S2C body: it combines four previously-isolated techniques in
// ONE struct, and is the first to place a sentinel-scan bit-field run INSIDE a nested struct ARRAY element.
//   (1) ENGINE-ENUM MEMBER: Kind is the SAME `enum class PartyKind : uint8_t` (Party=0, Alliance=5) from
//       src/map/enums/party_kind.h that chunks 21/22 pinned -- REUSED unchanged here (a 4th body on one enum).
//   (2) NESTED STRUCT ARRAY: GroupTbl[20] of GROUP_TBL, packed after Kind+padding05[3] at body offset 4 (the
//       array's start is already 4-aligned from GROUP_TBL's uint32 UniqueNo), stride 12 (no inter-element pad).
//   (3) SENTINEL-SCAN BIT-FIELD RUN INSIDE THAT ELEMENT -- the genuinely-new hazard: GROUP_TBL has SEVEN
//       uint8_t bit-fields whose widths (PartyNo:2, then six :1 flags) sum to EXACTLY 8 bits, so they all pack
//       into ONE byte at element offset 6. The FIRST 2-bit-width field (PartyNo, mask 0x03) among 1-bit
//       neighbours (masks 0x04/0x08/0x10/0x20/0x40/0x80 at shifts 2..7); the compiler confirms the byte + mask +
//       shift for each by runtime sentinel scan (offsetof/C2618 on bit-fields -- a bit-field has no addressable
//       storage, the same reason its address can't be taken). A realigning uint16 ZoneNo@8 follows a padding07@7.
//   (4) S2C BODY-RELATIVE packing.
//
// Layouts (DEFAULT packing), body-relative:
//   GROUP_TBL (element, 12/4): UniqueNo@0(u32) ActIndex@4(u16) [7 bit-fields @6 in one byte: PartyNo:2 then six
//     :1] padding07@7(u8) ZoneNo@8(u16) padding0A@10(u16).
//   PacketData (244/4): Kind@0(u8 enum) padding05[3]@1..3 GroupTbl[20]@4 (stride 12, 4+240==244, no tail pad).

using System;

namespace XI.Map
{
    // GROUP_TBL -- one array element (and the field layout inside it). sizeof=12, alignof=4.
    // The bit-field byte is at element offset 6; PartyNo is a 2-BIT field there (masks/shifts sentinel-scan-derived).
    public static class GroupTblEntry
    {
        public const int SizeOf = 12;        // GT_SIZE=12
        public const int AlignOf = 4;        // GT_ALIGN=4

        public const int OffUniqueNo = 0;    // OFF_GT.UniqueNo=0 (u32)
        public const int OffActIndex = 4;    // OFF_GT.ActIndex=4 (u16)

        public const int OffBitfield = 6;    // BF_*.off=6 (ALL seven bit-fields live in this one byte)
        // The 2-bit PartyNo is the low two bits; the six 1-bit flags occupy bits 2..7 -- widths 2+1+1+1+1+1+1==8.
        public const int PartyNoMask = 0x03;       public const int PartyNoShift = 0;        // BF_PartyNo (width 2)
        public const int PartyLeaderMask = 0x04;   public const int PartyLeaderShift = 2;    // BF_PartyLeaderFlg
        public const int AllianceLeaderMask = 0x08; public const int AllianceLeaderShift = 3; // BF_AllianceLeaderFlg
        public const int PartyRMask = 0x10;        public const int PartyRShift = 4;          // BF_PartyRFlg
        public const int AllianceRMask = 0x20;     public const int AllianceRShift = 5;        // BF_AllianceRFlg
        public const int Unknown06Mask = 0x40;     public const int Unknown06Shift = 6;        // BF_unknown06
        public const int Unknown07Mask = 0x80;     public const int Unknown07Shift = 7;        // BF_unknown07

        public const int OffPadding07 = 7;   // OFF_GT.padding07=7 (u8, after the bit-field byte)
        public const int OffZoneNo = 8;      // OFF_GT.ZoneNo=8 (u16, realigns to 2 after padding07@7)
        public const int OffPadding0A = 10;  // OFF_GT.padding0A=10 (u16)
    }

    // GP_SERV_COMMAND_GROUP_TBL::PacketData (outer, body-relative, default packing). sizeof=244, alignof=4.
    // Kind reuses the chunk-21/22 XI.Map.PartyKind enum (the engine enum from party_kind.h, not redefined).
    public static class S2cGroupTbl
    {
        public const int SizeOf = 244;        // PD_SIZE=244
        public const int AlignOf = 4;         // PD_ALIGN=4

        public const int OffKind = 0;         // OFF_PD.Kind=0 (PartyKind, u8)
        public const int OffPadding05 = 1;    // OFF_PD.padding05=1 (u8[3])
        public const int Padding05Len = 3;    // PD_padding05_len=3
        public const int OffGroupTbl = 4;     // OFF_PD.GroupTbl=4 (array start; already 4-aligned)
        public const int GroupTblCount = 20;  // PD_GroupTbl_count=20
        public const int GroupTblLen = 240;   // PD_GroupTbl_len=240

        // element-relative offset for array element i's `inner` field (i*stride==i*sizeof(GROUP_TBL)==i*12).
        public static int ElementOffset(int i) => OffGroupTbl + i * GroupTblEntry.SizeOf;
        public static int ElementField(int i, int inner) => ElementOffset(i) + inner;

        // ---- top-level members ----
        public static PartyKind GetKind(ReadOnlySpan<byte> body) => (PartyKind)body[OffKind];
        public static void SetKind(Span<byte> body, PartyKind v) => body[OffKind] = (byte)v;
        public static byte GetKindRaw(ReadOnlySpan<byte> body) => body[OffKind];
        public static ReadOnlySpan<byte> GetPadding05(ReadOnlySpan<byte> body) => body.Slice(OffPadding05, Padding05Len);

        // ---- GROUP_TBL[i] fixed-width members ----
        public static uint GetUniqueNo(ReadOnlySpan<byte> body, int i) => BitConverter.ToUInt32(body.Slice(ElementField(i, GroupTblEntry.OffUniqueNo), 4));
        public static void SetUniqueNo(Span<byte> body, int i, uint v) => BitConverter.TryWriteBytes(body.Slice(ElementField(i, GroupTblEntry.OffUniqueNo), 4), v);

        public static ushort GetActIndex(ReadOnlySpan<byte> body, int i) => BitConverter.ToUInt16(body.Slice(ElementField(i, GroupTblEntry.OffActIndex), 2));
        public static void SetActIndex(Span<byte> body, int i, ushort v) => BitConverter.TryWriteBytes(body.Slice(ElementField(i, GroupTblEntry.OffActIndex), 2), v);

        public static ushort GetZoneNo(ReadOnlySpan<byte> body, int i) => BitConverter.ToUInt16(body.Slice(ElementField(i, GroupTblEntry.OffZoneNo), 2));
        public static void SetZoneNo(Span<byte> body, int i, ushort v) => BitConverter.TryWriteBytes(body.Slice(ElementField(i, GroupTblEntry.OffZoneNo), 2), v);

        public static byte GetEntryPadding07(ReadOnlySpan<byte> body, int i) => body[ElementField(i, GroupTblEntry.OffPadding07)];
        public static ushort GetPadding0A(ReadOnlySpan<byte> body, int i) => BitConverter.ToUInt16(body.Slice(ElementField(i, GroupTblEntry.OffPadding0A), 2));

        // ---- the 7-bit-field byte (element offset 6): the 2-bit PartyNo + six 1-bit flags, RMW to preserve siblings ----
        // PartyNo is a 2-bit value (0..3); read-modify-write keeps the six 1-bit flags in the same byte.
        public static int GetPartyNo(ReadOnlySpan<byte> body, int i) => (body[ElementField(i, GroupTblEntry.OffBitfield)] & GroupTblEntry.PartyNoMask) >> GroupTblEntry.PartyNoShift;
        public static void SetPartyNo(Span<byte> body, int i, int v)
        {
            int o = ElementField(i, GroupTblEntry.OffBitfield);
            body[o] = (byte)((body[o] & ~GroupTblEntry.PartyNoMask) | ((v << GroupTblEntry.PartyNoShift) & GroupTblEntry.PartyNoMask));
        }

        public static bool GetPartyLeaderFlg(ReadOnlySpan<byte> body, int i) => (body[ElementField(i, GroupTblEntry.OffBitfield)] & GroupTblEntry.PartyLeaderMask) != 0;
        public static void SetPartyLeaderFlg(Span<byte> body, int i, bool v) => SetBit(body, i, GroupTblEntry.PartyLeaderMask, v);
        public static bool GetAllianceLeaderFlg(ReadOnlySpan<byte> body, int i) => (body[ElementField(i, GroupTblEntry.OffBitfield)] & GroupTblEntry.AllianceLeaderMask) != 0;
        public static void SetAllianceLeaderFlg(Span<byte> body, int i, bool v) => SetBit(body, i, GroupTblEntry.AllianceLeaderMask, v);
        public static bool GetPartyRFlg(ReadOnlySpan<byte> body, int i) => (body[ElementField(i, GroupTblEntry.OffBitfield)] & GroupTblEntry.PartyRMask) != 0;
        public static void SetPartyRFlg(Span<byte> body, int i, bool v) => SetBit(body, i, GroupTblEntry.PartyRMask, v);
        public static bool GetAllianceRFlg(ReadOnlySpan<byte> body, int i) => (body[ElementField(i, GroupTblEntry.OffBitfield)] & GroupTblEntry.AllianceRMask) != 0;
        public static void SetAllianceRFlg(Span<byte> body, int i, bool v) => SetBit(body, i, GroupTblEntry.AllianceRMask, v);
        public static bool GetUnknown06(ReadOnlySpan<byte> body, int i) => (body[ElementField(i, GroupTblEntry.OffBitfield)] & GroupTblEntry.Unknown06Mask) != 0;
        public static bool GetUnknown07(ReadOnlySpan<byte> body, int i) => (body[ElementField(i, GroupTblEntry.OffBitfield)] & GroupTblEntry.Unknown07Mask) != 0;

        // one shared read-modify-write for the single-bit flags in the element's bit-field byte (preserves siblings).
        private static void SetBit(Span<byte> body, int i, int mask, bool v)
        {
            int o = ElementField(i, GroupTblEntry.OffBitfield);
            body[o] = (byte)((body[o] & ~mask) | (v ? mask : 0));
        }
    }
}
