// C# wire models for TWO Phase-3 map C2S body structs that reference ONE engine enum defined in a SEPARATE
// header: GP_CLI_COMMAND_GROUP_LEAVE (0x06f) and GP_CLI_COMMAND_GROUP_BREAKUP (0x070), src/map/packets/c2s/
// 0xNNN_*.h via GP_CLI_PACKET(name, PartyKind Kind;). FIRST engine-enum body port -- establishes the technique
// that unblocks the ~50 packet bodies previously deferred as "engine-enum-dependent". Offsets are HEADER-
// INCLUSIVE (the 4-byte GP_CLI_HEADER is bytes 0..3), matching C2sPos/C2sAuc/C2sAction.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_c2s_group_party_enum_harness.cpp -> map_c2s_group_party_enum_golden.txt), NOT hand-computed.
//
// The engine-enum technique (the point of this chunk):
//   The packet headers forward-declare `enum class PartyKind : uint8_t;` and the DEFINITION (Party=0,
//   Alliance=5) lives in a DIFFERENT header (src/map/enums/party_kind.h). magic_enum is not vendored, so a
//   compiled oracle cannot reflect a bare literal's NAME -- but for the STRUCT LAYOUT it does not need to: the
//   oracle proves a scoped enum member's size/align/offsetof equal its UNDERLYING integer type. Specifically the
//   compiler emitted EQ_SIZE=1, EQ_ALIGN=1, EQ_OFFSET=1 comparing GROUP_LEAVE against a `uint8_t`-member twin.
//   Therefore: a compiled layout oracle is valid for any engine-enum body by substituting the enum's underlying
//   integer type; the enum's LITERALS are then pinned separately by the independent .h re-parse (the chunk-2
//   packet-id technique) rather than by the compiler. The C# PartyKind below is cross-checked BOTH ways.
//
// Layout (DEFAULT packing; GP_CLI_HEADER's bit-fields are in the header): header@0 (4 bytes) + PartyKind Kind
//   (underlying uint8_t, alignof 1) at offset 4 -> struct align 2 (from the header's uint16 storage units) and
//   size 4+1=5 padded to 6. Both GROUP_LEAVE and GROUP_BREAKUP are byte-identical (LEAVE_SIZE==BREAKUP_SIZE==6,
//   OFF_LEAVE.Kind==OFF_BREAKUP.Kind==4).

using System;

namespace XI.Map
{
    // PartyKind -- the engine enum shared by BOTH bodies. src/map/enums/party_kind.h:
    //   enum class PartyKind : uint8_t { Party = 0, Alliance = 5 };
    // Underlying uint8_t (the oracle's ENUM_underlying_size=1); literals pinned vs the compiler's
    // ENUM_Party/ENUM_Alliance AND an independent re-parse of the .h in the gate.
    public enum PartyKind : byte
    {
        Party    = 0,   // ENUM_Party=0
        Alliance = 5,   // ENUM_Alliance=5
    }

    // GP_CLI_COMMAND_GROUP_LEAVE (0x06f). Header-inclusive, default packing. size=6, align=2, Kind@4.
    public static class C2sGroupLeave
    {
        // GP_CLI_HEADER prefix (id:9|size:7|sync) -- same 4-byte framing as the other C2S structs.
        public const int OffHeader = 0;    // OFF_LEAVE.header=0
        public const int HeaderSize = 4;   // CHDR_SIZE=4
        public const int OffSync = 2;

        public const int SizeOf = 6;       // LEAVE_SIZE=6
        public const int AlignOf = 2;      // LEAVE_ALIGN=2

        public const int OffKind = 4;      // OFF_LEAVE.Kind=4 (PartyKind, underlying uint8_t)

        public static PartyKind GetKind(ReadOnlySpan<byte> body) => (PartyKind)body[OffKind];
        public static void SetKind(Span<byte> body, PartyKind v) => body[OffKind] = (byte)v;
        public static byte GetKindRaw(ReadOnlySpan<byte> body) => body[OffKind];
    }

    // GP_CLI_COMMAND_GROUP_BREAKUP (0x070). Same single-PartyKind layout as GROUP_LEAVE (byte-identical struct).
    public static class C2sGroupBreakup
    {
        public const int OffHeader = 0;    // OFF_BREAKUP.header=0
        public const int HeaderSize = 4;   // CHDR_SIZE=4
        public const int OffSync = 2;

        public const int SizeOf = 6;       // BREAKUP_SIZE=6
        public const int AlignOf = 2;      // BREAKUP_ALIGN=2

        public const int OffKind = 4;      // OFF_BREAKUP.Kind=4 (PartyKind, underlying uint8_t)

        public static PartyKind GetKind(ReadOnlySpan<byte> body) => (PartyKind)body[OffKind];
        public static void SetKind(Span<byte> body, PartyKind v) => body[OffKind] = (byte)v;
        public static byte GetKindRaw(ReadOnlySpan<byte> body) => body[OffKind];
    }
}
