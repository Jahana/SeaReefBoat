// C# wire model for the login-server (lobby) protocol structs and the xiloader auth
// surface. This is the Phase-2 login foundation: the byte layouts of the packets the
// connect/data/view sessions exchange with the FFXI client, plus the enums the auth
// handshake uses.
//
// CRITICAL METHODOLOGY: every SizeOf/offset below is copied from the compiled MSVC
// layout oracle (csharp/oracle/login_lobby_harness.cpp -> login_lobby_golden.txt), NOT
// hand-computed. The engine structs use MSVC default packing (no #pragma pack) and a
// byte-wide bit-field run, so sizeof/offsetof are the compiler's, not the author's.
// The gate tests/XI.Verify.Login re-reads the golden file and asserts these constants
// (and the header codec) match it byte-for-byte; any engine layout change requires
// rebuilding the oracle.
//
// Struct source: src/login/login_packets.h. Enum sources: src/login/auth_session.h
// (login_cmd, login_result, AUTH_COMPONENTS, ACCOUNT_*), src/login/login_errors.h
// (errorCode), src/login/login_helpers.h (EXPANSION_DISPLAY, FEATURE_DISPLAY).

using System;

namespace XI.Login
{
    // loginPackets namespace helpers (login_packets.h). The special terminator is the
    // 4 bytes "IXFF" read as a little-endian uint32, exactly as getTerminator() builds it.
    public static class LoginPackets
    {
        // { 0x49, 0x58, 0x46, 0x46 } -> 0x46465849 (oracle: TERM=46465849).
        public const uint Terminator = 0x46465849u;

        public static uint GetTerminator() => Terminator;

        // copyHashIntoPacket: write 16 hash bytes into packet.identifier (offset 12).
        public static void CopyHashIntoPacket(Span<byte> packet, ReadOnlySpan<byte> hash)
        {
            if (hash.Length != 16)
                throw new ArgumentException("hash must be 16 bytes", nameof(hash));
            hash.CopyTo(packet.Slice(PacketLayout.OffIdentifier, 16));
        }

        // clearIdentifier: zero the 16-byte identifier field.
        public static void ClearIdentifier(Span<byte> packet)
            => packet.Slice(PacketLayout.OffIdentifier, 16).Clear();
    }

    // packet_t: the shared 28-byte lobby packet header.
    //   uint32 packet_size; uint32 terminator; uint32 command; uint8 identifer[16];
    public static class PacketLayout
    {
        public const int SizeOf = 28; // oracle: SIZE_packet_t=28

        public const int OffPacketSize = 0;  // OFF_packet.packet_size=0
        public const int OffTerminator = 4;  // OFF_packet.terminator=4
        public const int OffCommand = 8;     // OFF_packet.command=8
        public const int OffIdentifier = 12; // OFF_packet.identifer=12
    }

    // Fixed-field header reader/writer for the lobby packet_t prefix that every derived
    // lobby packet (lpkt_next_login, lpkt_chr_info2, lpkt_world_list, ...) shares.
    public readonly struct PacketHeader
    {
        public readonly uint PacketSize;
        public readonly uint Terminator;
        public readonly uint Command;

        public PacketHeader(uint packetSize, uint terminator, uint command)
        {
            PacketSize = packetSize;
            Terminator = terminator;
            Command = command;
        }

        public static PacketHeader Read(ReadOnlySpan<byte> packet) => new PacketHeader(
            BitConverter.ToUInt32(packet.Slice(PacketLayout.OffPacketSize, 4)),
            BitConverter.ToUInt32(packet.Slice(PacketLayout.OffTerminator, 4)),
            BitConverter.ToUInt32(packet.Slice(PacketLayout.OffCommand, 4)));

        public void Write(Span<byte> packet)
        {
            BitConverter.TryWriteBytes(packet.Slice(PacketLayout.OffPacketSize, 4), PacketSize);
            BitConverter.TryWriteBytes(packet.Slice(PacketLayout.OffTerminator, 4), Terminator);
            BitConverter.TryWriteBytes(packet.Slice(PacketLayout.OffCommand, 4), Command);
        }
    }

    // TC_OPERATION_MAKE (login_packets.h): the fixed character-info block embedded in
    // lpkt_chr_info_sub2. Offsets verbatim from the oracle. sizeof=96.
    public static class TcOperationMakeLayout
    {
        public const int SizeOf = 96; // SIZE_TC_OPERATION_MAKE=96

        public const int OffMonNo = 0;          // mon_no (uint16)
        public const int OffMjobNo = 2;         // mjob_no (uint8)
        public const int OffSjobNo = 3;         // sjob_no (uint8)
        public const int OffFaceNo = 4;         // face_no (uint16)
        public const int OffTownNo = 6;         // town_no (uint8)
        public const int OffGenFlag = 7;        // gen_flag (uint8)
        public const int OffHairNo = 8;         // hair_no (uint8)
        public const int OffSize = 9;           // size (uint8)
        public const int OffWorldNo = 10;       // world_no (uint16)
        public const int OffGrapIdTbl = 12;     // GrapIDTbl (uint16[8])
        public const int OffZoneNo = 28;        // zone_no (uint8)
        public const int OffMjobLevel = 29;     // mjob_level (uint8)
        public const int OffOpenFlag = 30;      // open_flag (uint8)
        public const int OffGmCallCounter = 31; // GMCallCounter (uint8)
        public const int OffVersion = 32;       // version (uint16)
        public const int OffSkill1 = 34;        // skill1 (uint8)
        public const int OffZoneNo2 = 35;       // zone_no2 (uint8)
        public const int OffRankSd = 36;        // ..._RANK_LEVEL_SD_ (uint8)
        public const int OffRankBs = 37;        // ..._RANK_LEVEL_BS_ (uint8)
        public const int OffRankWs = 38;        // ..._RANK_LEVEL_WS_ (uint8)
        public const int OffErrCounter = 39;    // ErrCounter (uint8)
        public const int OffFameSd = 40;        // ..._FAME_SD_COMMON_ (uint16)
        public const int OffFameBs = 42;        // ..._FAME_BS_COMMON_ (uint16)
        public const int OffFameWs = 44;        // ..._FAME_WS_COMMON_ (uint16)
        public const int OffFameDark = 46;      // ..._FAME_DARK_GUILD_ (uint16)
        public const int OffPlayTime = 48;      // PlayTime (uint32)
        public const int OffGetJobFlag = 52;    // get_job_flag (uint32)
        public const int OffJobLev = 56;        // job_lev (uint8[16])
        public const int OffFirstLoginDate = 72;// FirstLoginDate (uint32)
        public const int OffGold = 76;          // Gold (uint32)
        public const int OffSkill2 = 80;        // skill2 (uint8)
        public const int OffSkill3 = 81;        // skill3 (uint8)
        public const int OffSkill4 = 82;        // skill4 (uint8)
        public const int OffSkill5 = 83;        // skill5 (uint8)
        public const int OffChatCounter = 84;   // ChatCounter (uint32)
        public const int OffPartyCounter = 88;  // PartyCounter (uint32)
        public const int OffSkill6 = 92;        // skill6 (uint8)
        public const int OffSkill7 = 93;        // skill7 (uint8)
        public const int OffSkill8 = 94;        // skill8 (uint8)
        public const int OffSkill9 = 95;        // skill9 (uint8)
    }

    // lpkt_chr_info_sub2: one character entry. The bit-field run
    //   uint8 renamef:1; uint8 race_change:1; uint8 unused:6;
    // occupies byte 10 (oracle BITFIELD_off=10); ffxi_id_world_tbl (uint8) is the
    // adjacent byte 11, and character_name starts at 12 (4-byte aligned, so the two
    // single bytes 10,11 are followed by 10 bytes of the name field with no gap).
    public static class ChrInfoSub2Layout
    {
        public const int SizeOf = 140; // SIZE_lpkt_chr_info_sub2=140

        public const int OffFfxiId = 0;         // ffxi_id (uint32)
        public const int OffFfxiIdWorld = 4;    // ffxi_id_world (uint16)
        public const int OffWorldId = 6;        // worldid (uint16)
        public const int OffStatus = 8;         // status (uint16)
        public const int OffBitfield = 10;      // renamef|race_change|unused (1 byte, runtime-derived)
        public const int OffFfxiIdWorldTbl = 11;// ffxi_id_world_tbl (uint8)
        public const int OffCharacterName = 12; // character_name (char[16])
        public const int OffWorldName = 28;     // world_name (char[16])
        public const int OffCharacterInfo = 44; // character_info (TC_OPERATION_MAKE)

        // Bit-field masks within the OffBitfield byte (LSB-first, per MSVC).
        public const int RenamefMask = 0x01;
        public const int RaceChangeMask = 0x02;
    }

    // lpkt_chr_info2 : packet_t: header(28) + uint32 characters(28) + sub2[16](32..).
    public static class ChrInfo2Layout
    {
        public const int SizeOf = 2272; // SIZE_lpkt_chr_info2=2272 (= 32 + 16*140)
        public const int HeaderSize = 28;

        public const int OffCharacters = 28;    // OFF_chr2.characters
        public const int OffCharacterInfo = 32; // OFF_chr2.character_info
        public const int CharacterStride = 140; // sizeof(lpkt_chr_info_sub2)
        public const int MaxCharacters = 16;    // character_info[16]
    }

    // lpkt_world_name { uint32 no; char name[16]; } and lpkt_world_list : packet_t.
    public static class WorldNameLayout
    {
        public const int SizeOf = 20; // SIZE_lpkt_world_name=20
        public const int OffNo = 0;   // OFF_wno.no
        public const int OffName = 4; // OFF_wno.name
    }

    public static class WorldListLayout
    {
        public const int SizeOf = 52; // SIZE_lpkt_world_list=52
        public const int HeaderSize = 28;

        public const int OffSumOfWorld = 28;    // OFF_wlist.sumofworld
        public const int OffWorldName = 32;     // OFF_wlist.world_name
        public const int WorldNameStride = 20;  // sizeof(lpkt_world_name)
    }

    // lpkt_next_login : packet_t: header(28) then ffxi_id(28)...cache_port(68). sizeof=72.
    public static class NextLoginLayout
    {
        public const int SizeOf = 72; // SIZE_lpkt_next_login=72
        public const int HeaderSize = 28;

        public const int OffFfxiId = 28;           // OFF_next.ffxi_id
        public const int OffFfxiIdWorld = 32;      // OFF_next.ffxi_id_world
        public const int OffCharacterName = 36;    // OFF_next.character_name
        public const int OffServerId = 52;         // OFF_next.server_id
        public const int OffServerIp = 56;         // OFF_next.server_ip
        public const int OffServerPort = 60;       // OFF_next.server_port
        public const int OffCacheIp = 64;          // OFF_next.cache_ip
        public const int OffCachePort = 68;        // OFF_next.cache_port
    }

    // lpkt_deletechr { packet_t header; uint32 ffxi_id; uint32 ffxi_id_world; uint8 passwd[16]; }.
    public static class DeleteChrLayout
    {
        public const int SizeOf = 52; // SIZE_lpkt_deletechr=52
        public const int HeaderSize = 28;

        public const int OffFfxiId = 28;         // OFF_delchr.ffxi_id
        public const int OffFfxiIdWorld = 32;    // OFF_delchr.ffxi_id_world
        public const int OffPasswd = 36;         // OFF_delchr.passwd
    }
}
