// C# wire model for the Phase-3 map C2S body struct GP_CLI_COMMAND_AUC (0x04e),
// src/map/packets/c2s/0x04e_auc.h via the GP_CLI_PACKET(...) macro (c2s/base.h). FIRST C2S body containing a
// UNION (chunks 15/16/17's unions were all S2C). Offsets are HEADER-INCLUSIVE (the 4-byte GP_CLI_HEADER is
// bytes 0..3), matching C2sPos/C2sFaqGmcall.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_c2s_auc_union_harness.cpp -> map_c2s_auc_union_golden.txt), NOT hand-computed.
//
// This body is the densest union yet and pins union facts no prior chunk could:
//   - SIX-arm union GP_AUC_PARAM: LotIn(12/4) Bid(12/4) Summary(8/4) History(8/4) AskCommit(12/4) Trans(12/2).
//     All six arms alias at offset 0. The union's SIZE = MAX arm size = 12 (mixed 8 vs 12 arms) AND its ALIGN
//     = MAX arm align = 4 (Trans is only align 2, but the union still takes the max) -- a hand-reasoner that
//     sized/aligned the union to the Trans arm, or summed the arms, would be wrong.
//   - The two 6-member-bytes arms (Summary u32+u16, History u32+u16) are each SELF-PADDED to 8 by their OWN
//     align-4, even though the union is 12 -> "arm size" is the arm's own padded sizeof, not its member-sum.
//   - A self-contained hand-authored uint8 enum (GP_CLI_COMMAND_AUC_COMMAND, defined in THIS header) is used as
//     the leading fixed-width MEMBER Command@4 -> both its literals AND its member offset are compiler-pinned.
//   - A trailing nested struct GP_AUC_BOX (40/4) sits at Parcel@20 (= Param@8 + union 12); inside it a
//     char[16]@4 then a u16@20 then two u8 then a u32 Price@24 that is ALREADY 4-aligned so it adds NO padding.
//
// Layout: GP_CLI_HEADER@0..3 ; Command(u8)@4 ; AucWorkIndex(i8)@5 ; Result(i8)@6 ; ResultStatus(i8)@7 ;
//   Param(union 12/4)@8 (align-4 realign after the i8s) ; Parcel(GP_AUC_BOX 40/4)@20 -> whole AUC size 60, align 4.

using System;

namespace XI.Map
{
    // GP_CLI_COMMAND_AUC_COMMAND (0x04e_auc.h). Hand-authored uint8_t literals; pinned vs ENUM_* lines.
    public enum AucCommand : byte
    {
        Open      = 0x02,   // ENUM_Open=2
        AskCommit = 0x04,   // ENUM_AskCommit=4
        Info      = 0x05,   // ENUM_Info=5
        WorkCheck = 0x0A,   // ENUM_WorkCheck=10
        LotIn     = 0x0B,   // ENUM_LotIn=11
        LotCancel = 0x0C,   // ENUM_LotCancel=12
        LotCheck  = 0x0D,   // ENUM_LotCheck=13
        Bid       = 0x0E,   // ENUM_Bid=14
    }

    // GP_AUC_PARAM_LOT (union arm, 12/4). Offsets arm-relative.
    public static class AucParamLot
    {
        public const int SizeOf = 12;        // LOT_SIZE=12
        public const int AlignOf = 4;        // LOT_ALIGN=4
        public const int OffLimitPrice = 0;  // OFF_LOT.LimitPrice=0 (u32)
        public const int OffItemWorkIndex = 4; // OFF_LOT.ItemWorkIndex=4 (u16)
        public const int OffPadding00 = 6;   // u16
        public const int OffItemStacks = 8;  // OFF_LOT.ItemStacks=8 (u32)
    }

    // GP_AUC_PARAM_BID (union arm, 12/4). Offsets arm-relative.
    public static class AucParamBid
    {
        public const int SizeOf = 12;        // BID_SIZE=12
        public const int AlignOf = 4;        // BID_ALIGN=4
        public const int OffBidPrice = 0;    // OFF_BID.BidPrice=0 (u32)
        public const int OffItemNo = 4;      // OFF_BID.ItemNo=4 (u16)
        public const int OffPadding00 = 6;   // u16
        public const int OffItemStacks = 8;  // u32
    }

    // GP_AUC_PARAM_SUMMARY (union arm). u32+u16 = 6 member-bytes SELF-PADDED to 8 by align-4. 8/4.
    public static class AucParamSummary
    {
        public const int SizeOf = 8;         // SUM_SIZE=8 (6 bytes of members, +2 tail pad)
        public const int AlignOf = 4;        // SUM_ALIGN=4
        public const int OffKind = 0;        // OFF_SUM.Kind=0 (u32)
        public const int OffItemNo = 4;      // OFF_SUM.ItemNo=4 (u16)
    }

    // GP_AUC_PARAM_HISTORY (union arm). u32+u16 self-padded to 8. 8/4.
    public static class AucParamHistory
    {
        public const int SizeOf = 8;         // HIS_SIZE=8
        public const int AlignOf = 4;        // HIS_ALIGN=4
        public const int OffRange = 0;       // u32
        public const int OffItemNo = 4;      // u16
    }

    // GP_AUC_PARAM_ASKCOMMIT (union arm, 12/4). Offsets arm-relative.
    public static class AucParamAskCommit
    {
        public const int SizeOf = 12;        // ASK_SIZE=12
        public const int AlignOf = 4;        // ASK_ALIGN=4
        public const int OffCommission = 0;  // u32
        public const int OffItemWorkIndex = 4; // u16
        public const int OffItemNo = 6;      // OFF_ASK.ItemNo=6 (u16)
        public const int OffItemStacks = 8;  // OFF_ASK.ItemStacks=8 (u32)
    }

    // GP_AUC_PARAM_TRANS (union arm, ALL u16 -> 12/2, the only align-2 arm). Offsets arm-relative.
    public static class AucParamTrans
    {
        public const int SizeOf = 12;        // TRA_SIZE=12
        public const int AlignOf = 2;        // TRA_ALIGN=2  (less than the union's align 4 -- the max-align fact)
        public const int OffSignature = 0;   // OFF_TRA.Signature=0
        public const int OffTotalSize = 2;   // u16
        public const int OffOffset = 4;      // u16
        public const int OffSize = 6;        // u16
        public const int OffFragmentNo = 8;  // u16
        public const int OffTotalFragments = 10; // OFF_TRA.TotalFragments=10 (u16)
    }

    // GP_AUC_PARAM = union{ LotIn; Bid; Summary; History; AskCommit; Trans }. Six arms, all alias @0.
    // size == MAX arm size (12), align == MAX arm align (4). Each arm's own offset is 0 (they overlay).
    public static class AucParam
    {
        public const int SizeOf = 12;        // PARAM_SIZE=12
        public const int AlignOf = 4;        // PARAM_ALIGN=4 (== max(4,4,4,4,4,2))
        public const int OffLotIn = 0;       // OFF_PARAM.LotIn=0
        public const int OffBid = 0;         // all arms alias at 0 (the union fact)
        public const int OffSummary = 0;
        public const int OffHistory = 0;
        public const int OffAskCommit = 0;
        public const int OffTrans = 0;
    }

    // GP_AUC_BOX (the trailing nested struct Parcel). 40/4. Offsets box-relative.
    public static class AucBox
    {
        public const int SizeOf = 40;        // BOX_SIZE=40
        public const int AlignOf = 4;        // BOX_ALIGN=4
        public const int OffStat = 0;        // OFF_BOX.Stat=0 (u8)
        public const int OffPadding00 = 1;
        public const int OffItemIndex = 2;   // u8
        public const int OffPadding01 = 3;
        public const int OffName = 4;        // OFF_BOX.Name=4 (uint8[16])
        public const int NameLen = 16;       // BOX_Name_len=16
        public const int OffItemNo = 20;     // OFF_BOX.ItemNo=20 (u16; 4+16=20 already 2-aligned)
        public const int OffItemQuantity = 22; // OFF_BOX.ItemQuantity=22 (u8)
        public const int OffItemCategory = 23; // OFF_BOX.ItemCategory=23 (u8)
        public const int OffPrice = 24;      // OFF_BOX.Price=24 (u32 -- ALREADY 4-aligned, NO pad after 23->24)
        public const int OffMarketNo = 28;   // u32
        public const int OffLotNo = 32;      // u32
        public const int OffTimeStamp = 36;  // OFF_BOX.TimeStamp=36 (u32; 36+4==40 == SizeOf)
    }

    // GP_CLI_COMMAND_AUC (outer, header-inclusive, default packing). size=60, align=4.
    public static class C2sAuc
    {
        // GP_CLI_HEADER prefix (id:9|size:7|sync) -- same 4-byte framing as pos/recipe/lockstyle/faq_gmcall.
        public const int OffHeader = 0;       // OFF_AUC.header=0
        public const int HeaderSize = 4;      // CHDR_SIZE=4
        public const int OffSync = 2;         // HDR_OFF_sync=2

        public const int SizeOf = 60;         // AUC_SIZE=60
        public const int AlignOf = 4;         // AUC_ALIGN=4

        public const int OffCommand = 4;      // OFF_AUC.Command=4 (AucCommand / u8)
        public const int OffAucWorkIndex = 5; // int8
        public const int OffResult = 6;       // int8
        public const int OffResultStatus = 7; // OFF_AUC.ResultStatus=7 (int8)
        public const int OffParam = 8;        // OFF_AUC.Param=8 (six-arm union, realigns to 4 after the i8s)
        public const int OffParcel = 20;      // OFF_AUC.Parcel=20 (== OffParam + union SizeOf 12)

        // nested-inclusive offsets for the accessors (header start + member base + arm/field offset):
        public static int ParamField(int armOff, int fieldOff) => OffParam + armOff + fieldOff; // armOff==0 (all arms alias)
        public static int ParcelField(int boxOff) => OffParcel + boxOff;

        public static int LotLimitPriceOffset() => ParamField(AucParam.OffLotIn, AucParamLot.OffLimitPrice);       // 8
        public static int TransSignatureOffset() => ParamField(AucParam.OffTrans, AucParamTrans.OffSignature);      // 8 (== LotLimitPrice: union alias)
        public static int ParcelPriceOffset() => ParcelField(AucBox.OffPrice);                                     // 44
        public static int ParcelNameOffset() => ParcelField(AucBox.OffName);                                       // 24

        // ---- leading scalars (little-endian) ------------------------------------------------------
        public static AucCommand GetCommand(ReadOnlySpan<byte> body) => (AucCommand)body[OffCommand];
        public static void SetCommand(Span<byte> body, AucCommand v) => body[OffCommand] = (byte)v;

        public static sbyte GetAucWorkIndex(ReadOnlySpan<byte> body) => unchecked((sbyte)body[OffAucWorkIndex]);
        public static void SetAucWorkIndex(Span<byte> body, sbyte v) => body[OffAucWorkIndex] = unchecked((byte)v);
        public static sbyte GetResult(ReadOnlySpan<byte> body) => unchecked((sbyte)body[OffResult]);
        public static sbyte GetResultStatus(ReadOnlySpan<byte> body) => unchecked((sbyte)body[OffResultStatus]);

        // ---- Param union arms (each read through its own arm view of the SAME 12 bytes @8) ---------
        public static uint GetLotLimitPrice(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(LotLimitPriceOffset(), 4));
        public static void SetLotLimitPrice(Span<byte> b, uint v) => BitConverter.TryWriteBytes(b.Slice(LotLimitPriceOffset(), 4), v);
        public static ushort GetLotItemWorkIndex(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(ParamField(AucParam.OffLotIn, AucParamLot.OffItemWorkIndex), 2));
        public static uint GetLotItemStacks(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(ParamField(AucParam.OffLotIn, AucParamLot.OffItemStacks), 4));

        public static uint GetBidBidPrice(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(ParamField(AucParam.OffBid, AucParamBid.OffBidPrice), 4));
        public static void SetBidBidPrice(Span<byte> b, uint v) => BitConverter.TryWriteBytes(b.Slice(ParamField(AucParam.OffBid, AucParamBid.OffBidPrice), 4), v);

        public static uint GetSummaryKind(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(ParamField(AucParam.OffSummary, AucParamSummary.OffKind), 4));
        public static void SetSummaryKind(Span<byte> b, uint v) => BitConverter.TryWriteBytes(b.Slice(ParamField(AucParam.OffSummary, AucParamSummary.OffKind), 4), v);

        public static uint GetHistoryRange(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(ParamField(AucParam.OffHistory, AucParamHistory.OffRange), 4));

        public static uint GetAskCommission(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(ParamField(AucParam.OffAskCommit, AucParamAskCommit.OffCommission), 4));
        public static void SetAskCommission(Span<byte> b, uint v) => BitConverter.TryWriteBytes(b.Slice(ParamField(AucParam.OffAskCommit, AucParamAskCommit.OffCommission), 4), v);

        public static ushort GetTransSignature(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(TransSignatureOffset(), 2));
        public static void SetTransSignature(Span<byte> b, ushort v) => BitConverter.TryWriteBytes(b.Slice(TransSignatureOffset(), 2), v);
        public static ushort GetTransTotalFragments(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(ParamField(AucParam.OffTrans, AucParamTrans.OffTotalFragments), 2));

        // ---- Parcel (GP_AUC_BOX) -------------------------------------------------------------------
        public static byte GetParcelStat(ReadOnlySpan<byte> b) => b[ParcelField(AucBox.OffStat)];
        public static void SetParcelStat(Span<byte> b, byte v) => b[ParcelField(AucBox.OffStat)] = v;
        public static ReadOnlySpan<byte> GetParcelName(ReadOnlySpan<byte> b) => b.Slice(ParcelNameOffset(), AucBox.NameLen);
        public static void SetParcelName(Span<byte> b, ReadOnlySpan<byte> name)
        {
            int o = ParcelNameOffset();
            b.Slice(o, AucBox.NameLen).Clear();
            int n = Math.Min(name.Length, AucBox.NameLen);
            name.Slice(0, n).CopyTo(b.Slice(o, n));
        }
        public static ushort GetParcelItemNo(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(ParcelField(AucBox.OffItemNo), 2));
        public static void SetParcelItemNo(Span<byte> b, ushort v) => BitConverter.TryWriteBytes(b.Slice(ParcelField(AucBox.OffItemNo), 2), v);
        public static byte GetParcelItemQuantity(ReadOnlySpan<byte> b) => b[ParcelField(AucBox.OffItemQuantity)];
        public static uint GetParcelPrice(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(ParcelPriceOffset(), 4));
        public static void SetParcelPrice(Span<byte> b, uint v) => BitConverter.TryWriteBytes(b.Slice(ParcelPriceOffset(), 4), v);
        public static uint GetParcelTimeStamp(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(ParcelField(AucBox.OffTimeStamp), 4));
        public static void SetParcelTimeStamp(Span<byte> b, uint v) => BitConverter.TryWriteBytes(b.Slice(ParcelField(AucBox.OffTimeStamp), 4), v);
    }
}
