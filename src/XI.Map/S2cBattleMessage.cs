// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_BATTLE_MESSAGE::PacketData (0x029),
// src/map/packets/s2c/0x029_battle_message.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cGroupSolicitReq/S2cBattlefield convention.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_battle_message_enum_harness.cpp -> map_s2c_battle_message_enum_golden.txt), NOT hand-computed.
//
// FOURTH engine-enum body, and the FIRST whose engine enum is backed by a DIFFERENT width than the uint8
// PartyKind of chunks 21/22/23. Its member MessageNum is `enum class MsgStd : uint16_t` (src/map/enums/msg_std.h,
// ~140 explicit literals). The oracle proves the chunk-21 underlying-type-substitution invariant holds for a
// scoped enum member REGARDLESS of underlying width: the uint16_t twin of the whole struct is byte-identical in
// size/align AND the MessageNum offset (BMQ_SIZE_EQ=1, BMQ_ALIGN_EQ=1, BMQ_MSGOFF_EQ=1; twin size 24 / align 4 /
// MessageNum@20). So on the wire MessageNum is exactly a 2-byte little-endian uint16 -- the C# accessor models it
// at that underlying width (the enum's NAME->literal set is NOT a wire-layout concern; the gate validates the
// independent .h re-parse against the compiler's underlying-cast for the 15 representative enumerators).
//
// The distinct new packing fact: a uint16-backed enum member sitting MID-STRUCT between a uint16 (ActIndexTar@18)
// and a byte (Type@22): MessageNum@20 packs right after ActIndexTar (18+2==20, already 2-aligned, no realign),
// and Type@22 / padding1B@23 follow it as plain bytes. Struct align 4 (from the u32s); size 24 (23+1==24, no tail
// pad).
//
// Layout (body-relative, default packing): UniqueNoCas@0(u32) UniqueNoTar@4(u32) Data@8(u32) Data2@12(u32)
//   ActIndexCas@16(u16) ActIndexTar@18(u16) MessageNum@20(MsgStd/u16) Type@22(u8) padding1B@23(u8) -> 24/4.

using System;

namespace XI.Map
{
    // GP_SERV_COMMAND_BATTLE_MESSAGE::PacketData (0x029). Body-relative, default packing. size=24, align=4.
    public static class S2cBattleMessage
    {
        public const int SizeOf = 24;        // BM_SIZE=24 (23+1==24, no tail pad)
        public const int AlignOf = 4;        // BM_ALIGN=4 (from the leading u32s)

        public const int OffUniqueNoCas = 0; // OFF_BM.UniqueNoCas=0 (u32)
        public const int OffUniqueNoTar = 4; // OFF_BM.UniqueNoTar=4 (u32)
        public const int OffData = 8;        // OFF_BM.Data=8 (u32)
        public const int OffData2 = 12;      // OFF_BM.Data2=12 (u32)
        public const int OffActIndexCas = 16;// OFF_BM.ActIndexCas=16 (u16)
        public const int OffActIndexTar = 18;// OFF_BM.ActIndexTar=18 (u16)
        public const int OffMessageNum = 20; // OFF_BM.MessageNum=20 (MsgStd, underlying uint16 -- packs right after ActIndexTar, 20 already 2-aligned)
        public const int OffType = 22;       // OFF_BM.Type=22 (u8)
        public const int OffPadding1B = 23;  // OFF_BM.padding1B=23 (u8)

        // ---- u32 accessors (little-endian) --------------------------------------------------------
        public static uint GetUniqueNoCas(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffUniqueNoCas, 4));
        public static void SetUniqueNoCas(Span<byte> body, uint v) => BitConverter.TryWriteBytes(body.Slice(OffUniqueNoCas, 4), v);
        public static uint GetUniqueNoTar(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffUniqueNoTar, 4));
        public static void SetUniqueNoTar(Span<byte> body, uint v) => BitConverter.TryWriteBytes(body.Slice(OffUniqueNoTar, 4), v);
        public static uint GetData(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffData, 4));
        public static void SetData(Span<byte> body, uint v) => BitConverter.TryWriteBytes(body.Slice(OffData, 4), v);
        public static uint GetData2(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffData2, 4));
        public static void SetData2(Span<byte> body, uint v) => BitConverter.TryWriteBytes(body.Slice(OffData2, 4), v);

        // ---- u16 accessors -------------------------------------------------------------------------
        public static ushort GetActIndexCas(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffActIndexCas, 2));
        public static void SetActIndexCas(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffActIndexCas, 2), v);
        public static ushort GetActIndexTar(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffActIndexTar, 2));
        public static void SetActIndexTar(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffActIndexTar, 2), v);

        // MessageNum is a MsgStd engine enum (underlying uint16). The oracle proved a scoped enum member's STORAGE
        // is its underlying integer (the uint16_t twin is byte-identical), so the wire is a plain 2-byte LE uint16.
        public static ushort GetMessageNum(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffMessageNum, 2));
        public static void SetMessageNum(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffMessageNum, 2), v);

        // ---- u8 accessors --------------------------------------------------------------------------
        public static byte GetType(ReadOnlySpan<byte> body) => body[OffType];
        public static void SetType(Span<byte> body, byte v) => body[OffType] = v;
        public static byte GetPadding1B(ReadOnlySpan<byte> body) => body[OffPadding1B];
    }
}
