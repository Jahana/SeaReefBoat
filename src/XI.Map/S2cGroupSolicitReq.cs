// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_GROUP_SOLICIT_REQ::PacketData (0x0dc),
// src/map/packets/s2c/0x0dc_group_solicit_req.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cPbxResult/S2cRecipe/S2cBattlefield convention.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_solicit_req_enum_harness.cpp -> map_s2c_solicit_req_enum_golden.txt), NOT hand-computed.
//
// SECOND engine-enum body (after 0x06f/0x070 in chunk 21) and the first to COMBINE three previously-isolated
// techniques in one struct:
//   (1) ENGINE-ENUM UNDERLYING-TYPE SUBSTITUTION: Kind is the same `enum class PartyKind : uint8_t`
//       (Party=0, Alliance=5, defined in src/map/enums/party_kind.h, forward-declared in the packet .h). Chunk
//       21 proved the compiler sizes a scoped enum member as its underlying integer; here the oracle re-confirms
//       that invariant in the S2C default-packing context (uint8_t twin SRU8 is byte-identical: EQ_SIZE/ALIGN=1,
//       EQ_KIND_OFFSET=1), so the layout oracle only needs the underlying type and the LITERALS are pinned by the
//       independent party_kind.h re-parse (reusing XI.Map.PartyKind from chunk 21 -- not redefined here).
//   (2) a char[16] NUL-terminated STRING field (sName@8, len 16) -- same accessor pattern as 0x009 message /
//       0x05d pendingstr: read to the first NUL, write = clear-then-copy bounded to the field length.
//   (3) a CONSECUTIVE byte-run: plain uint8 AnonFlag@6 immediately followed by the uint8-backed enum Kind@7 --
//       both alignof 1 so Kind packs right after AnonFlag with no realign (a hand-reasoner aligning the enum to
//       2 would wrongly put it at 8, shifting sName/RaceNo); the whole struct's align comes from UniqueNo (4).
//
// Layout (body-relative): UniqueNo@0 (u32) ; ActIndex@4 (u16) ; AnonFlag@6 (u8) ; Kind@7 (PartyKind/u8) ;
//   sName[16]@8 ; RaceNo@24 (u16, realigns to a 2-boundary after sName) ; padding1E[2]@26 ; size 28 / align 4.

using System;
using System.Text;

namespace XI.Map
{
    // GP_SERV_COMMAND_GROUP_SOLICIT_REQ::PacketData (0x0dc). Body-relative, default packing. size=28, align=4.
    public static class S2cGroupSolicitReq
    {
        public const int SizeOf = 28;        // SR_SIZE=28 (26 member span + 2 padding1E == 28, no tail pad)
        public const int AlignOf = 4;        // SR_ALIGN=4 (from the leading uint32 UniqueNo)

        public const int OffUniqueNo = 0;    // OFF_SR.UniqueNo=0 (u32)
        public const int OffActIndex = 4;    // OFF_SR.ActIndex=4 (u16)
        public const int OffAnonFlag = 6;    // OFF_SR.AnonFlag=6 (u8)
        public const int OffKind = 7;        // OFF_SR.Kind=7 (PartyKind, underlying uint8 -- packs right after AnonFlag)
        public const int OffSName = 8;       // OFF_SR.sName=8 (char[16]) -- packs at the running byte offset
        public const int SNameLen = 16;      // SR_sName_len=16
        public const int OffRaceNo = 24;     // OFF_SR.RaceNo=24 (u16, realigns to a 2-boundary after sName ends)
        public const int OffPadding1E = 26;  // OFF_SR.padding1E=26 (u8[2])
        public const int Padding1ELen = 2;   // SR_padding1E_len=2

        // ---- scalar accessors (little-endian) -----------------------------------------------------
        public static uint GetUniqueNo(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffUniqueNo, 4));
        public static void SetUniqueNo(Span<byte> body, uint v) => BitConverter.TryWriteBytes(body.Slice(OffUniqueNo, 4), v);

        public static ushort GetActIndex(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffActIndex, 2));
        public static void SetActIndex(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffActIndex, 2), v);

        public static byte GetAnonFlag(ReadOnlySpan<byte> body) => body[OffAnonFlag];
        public static void SetAnonFlag(Span<byte> body, byte v) => body[OffAnonFlag] = v;

        // The engine-enum member (PartyKind is the shared separate-header enum from chunk 21, XI.Map.PartyKind).
        public static PartyKind GetKind(ReadOnlySpan<byte> body) => (PartyKind)body[OffKind];
        public static void SetKind(Span<byte> body, PartyKind v) => body[OffKind] = (byte)v;
        public static byte GetKindRaw(ReadOnlySpan<byte> body) => body[OffKind];   // raw underlying byte (any value)

        public static ushort GetRaceNo(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffRaceNo, 2));
        public static void SetRaceNo(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffRaceNo, 2), v);

        // ---- char[16] NUL-terminated ASCII string field (sName) -- mirrors S2cMessage.Data ---------
        public static ReadOnlySpan<byte> GetSNameBytes(ReadOnlySpan<byte> body) => body.Slice(OffSName, SNameLen);

        // Reads bytes up to the first NUL (or the field end). Never exceeds SNameLen.
        public static string GetSNameString(ReadOnlySpan<byte> body)
        {
            var field = body.Slice(OffSName, SNameLen);
            int end = 0;
            while (end < field.Length && field[end] != 0) end++;
            return Encoding.ASCII.GetString(field.Slice(0, end));
        }

        // Zeroes the whole field then copies min(text.Length, SNameLen) ASCII bytes (a full-length write leaves
        // no NUL, matching the engine's bounded C-string copy; shorter writes are NUL-padded).
        public static void SetSNameString(Span<byte> body, string text)
        {
            var field = body.Slice(OffSName, SNameLen);
            field.Clear();
            int n = Math.Min(text.Length, SNameLen);
            for (int i = 0; i < n; i++) field[i] = (byte)text[i];
        }
    }
}
