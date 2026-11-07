// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_NARAKU::PacketData (0x006),
// src/map/packets/s2c/0x006_naraku.h. This is the FIRST EMPTY body struct -- PacketData has no members at all
// ("Empty packet - only header is sent"), so the wire packet is the 4-byte GP_SERV_HEADER and NOTHING else.
//
// CRITICAL METHODOLOGY: every number below is copied from the compiled MSVC framing oracle
// (csharp/oracle/map_s2c_naraku_harness.cpp -> map_s2c_naraku_golden.txt), NOT hand-computed.
//
// THE genuinely-new fact this chunk exists to pin (the C++-vs-wire trap, contrast every prior body 1-33):
//   An EMPTY C++ struct's `sizeof` is 1 -- but that is a LANGUAGE ARTIFACT (so every instance has a distinct address),
//   NOT a wire byte count. The engine's GP_SERV_PACKET ctor (s2c/base.h) branches on std::is_empty_v<PacketData>:
//     if constexpr (is_empty_v) setSize(sizeof(GP_SERV_HEADER));                       // -> 4, header ONLY
//     else                      setSize(sizeof(GP_SERV_HEADER) + sizeof(PacketData));  // -> header + body
//   so for naraku the framed packet size is 4 (the oracle pins PD_PACKET_SIZE=4), NOT the naive 4 + sizeof(1) == 5
//   (which the oracle emits separately as PD_NAIVE_HDR_PLUS_SIZEOF=5 so the gate can prove the is_empty_v branch --
//   not the plain sum -- is what the engine uses). A port that copied the C++ `sizeof` (1) as the body size would add
//   one SPURIOUS trailing byte and desync the framing; the correct wire body contribution is 0 (PD_WIRE_BODY=0).
//   The non-empty uint8 twin (TWIN_IS_EMPTY=0 -> TWIN_PACKET_SIZE=5 == 4 + 1) proves the discriminator is emptiness,
//   not the member count -- an empty struct and a one-byte struct both have sizeof 1, yet only the former is header-only.
//
// Layout (body-relative): the wire body is ZERO bytes; the whole packet is the 4-byte header the engine prepends.

using System;

namespace XI.Map
{
    // GP_SERV_COMMAND_NARAKU::PacketData (outer, body-relative). An EMPTY body struct -- no fields, no accessors.
    // BodySize is the WIRE contribution (0), which is DISTINCT from the C++ sizeof(empty)==1 artifact.
    public static class S2cNaraku
    {
        public const int BodySize = 0;   // PD_WIRE_BODY=0 -- an empty struct contributes ZERO wire bytes
        public const int AlignOf  = 1;   // PD_ALIGN=1
        public const int HeaderSize = 4; // == XI.Map.C2sPos.HeaderSize == PD_PACKET_SIZE (the whole framed packet)

        // The C++ `sizeof` of the empty struct (an address-distinctness artifact, NOT a wire byte count).
        public const int CppSizeOfEmptyStruct = 1; // PD_SIZE=1

        // The engine treats naraku as a header-only packet (std::is_empty_v<PacketData> is true).
        public static bool IsHeaderOnly => true;   // PD_IS_EMPTY=1

        // Framed packet size the engine sets via the is_empty_v branch: header only (4), NOT header + sizeof(empty) (5).
        public const int FramedPacketSize = 4;     // PD_PACKET_SIZE=4
    }
}
