// C# model for GP_CLI_LOGIN (game packet 0x00A), the unencrypted login packet the
// client sends to bind a session. Layout is taken from the compiler-derived oracle
// (csharp/oracle/login_harness.cpp -> login_golden.txt), NOT hand-computed: the
// struct uses MSVC default packing and a uint16 bitfield, so sizeof/offsetof are the
// compiler's, not something to guess.
//
// Reference: src/common/mmo.h:424 and src/map/map_networking.cpp recv_parse (0x00A path).
using System;

namespace XI.Crypto
{
    // Byte offsets as reported by the C++ compiler for GP_CLI_LOGIN (sizeof == 92).
    public static class LoginPacketLayout
    {
        public const int SizeOf = 92;

        public const int OffIdSize = 0;    // uint16 bitfield: id:9 | size:7
        public const int OffSync = 2;      // uint16 sync
        public const int OffLoginPacketCheck = 4; // uint8 checksum byte
        public const int OffPadding00 = 5; // uint8
        public const int OffUnknown00 = 6; // uint16 (MyPort)
        public const int OffUnknown01 = 8; // uint32 (MyIP)  -- checksum region starts here
        public const int OffUniqueNo = 12; // uint32
        public const int OffGrapIDTbl = 16; // uint16[9]
        public const int OffSName = 34;    // char[15]
        public const int OffSAccunt = 49;  // char[15]
        public const int OffTicket = 64;   // uint8[16]
        public const int OffVer = 80;      // uint32
        public const int OffSPlatform = 84;// uint8[4]
        public const int OffUCliLang = 88; // uint16
        public const int OffDammyArea = 90;// uint16

        // The additive checksum covers [OffUnknown01, SizeOf). LoginPacketCheck itself
        // is at offset 4, outside that region, so writing it back never disturbs the sum.
        public const int ChecksumOffset = OffUnknown01;             // 8
        public const int ChecksumLength = SizeOf - ChecksumOffset;  // 84
    }

    public static class LoginPacket
    {
        // Pack the first header word exactly as MSVC lays out `uint16 id:9; uint16 size:7;`
        // in little-endian: low 9 bits = id, next 7 bits = size.
        public static ushort PackIdSize(int id, int size) =>
            (ushort)((id & 0x1FF) | ((size & 0x7F) << 9));

        public static int IdOf(ushort word) => word & 0x1FF;
        public static int SizeOf(ushort word) => (word >> 9) & 0x7F;

        // recv_parse's LoginPacketCheck: uint8 sum over [ChecksumOffset, SizeOf).
        public static byte ComputeChecksum(ReadOnlySpan<byte> packet)
        {
            if (packet.Length < LoginPacketLayout.SizeOf)
                throw new ArgumentException("packet shorter than sizeof(GP_CLI_LOGIN)", nameof(packet));

            byte sum = 0;
            int end = LoginPacketLayout.ChecksumOffset + LoginPacketLayout.ChecksumLength;
            for (int i = LoginPacketLayout.ChecksumOffset; i < end; i++)
                sum = unchecked((byte)(sum + packet[i]));
            return sum;
        }

        // A packet is accepted when its stored LoginPacketCheck byte equals the recomputed sum.
        public static bool VerifyChecksum(ReadOnlySpan<byte> packet) =>
            packet.Length >= LoginPacketLayout.SizeOf &&
            packet[LoginPacketLayout.OffLoginPacketCheck] == ComputeChecksum(packet);
    }
}
