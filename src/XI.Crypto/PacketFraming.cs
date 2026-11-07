// C# port of the FFXI map-packet crypto framing from src/map/map_networking.cpp
// (MapNetworking::finalizePacket + map_decipher_packet).
//
// Wire layout of a game packet:
//   [ 28-byte plaintext header ][ cipher region ][ trailing 16-byte md5 hash ]
// The cipher region (everything after the 28-byte header) = payload || md5(payload),
// then an even count of 4-byte words is Blowfish-enciphered in place:
//   cypherSize = (regionWords) & -2 ; encipher/decipher cypherSize/2 64-bit blocks.
// An odd trailing word (the 7th in the 28-byte example) stays in plaintext.
//
// This models the cipher region as a standalone byte[] (its word 0 == the real
// packet's word 7 at byte offset 28), which is offset-equivalent to the C++.

using System;
using System.Security.Cryptography;

namespace XI.Crypto
{
    public static class PacketFraming
    {
        public const int HeaderSize = 0x1C; // FFXI_HEADER_SIZE
        public const int HashSize = 16;

        // Build the on-wire cipher region from a plaintext payload.
        public static byte[] EncipherPayload(ReadOnlySpan<byte> payload, Blowfish bf)
        {
            byte[] hash = MD5.HashData(payload.ToArray());
            byte[] region = new byte[payload.Length + HashSize];
            payload.CopyTo(region);
            Buffer.BlockCopy(hash, 0, region, payload.Length, HashSize);

            int words = region.Length / 4;
            int cypherSize = words & -2;
            uint[] w = ToWords(region);
            bf.EncipherBlocks(w, cypherSize / 2);
            FromWords(w, region);
            return region;
        }

        // Inverse of EncipherPayload. Returns false on checksum mismatch
        // (map_decipher_packet's checksum() == 0 gate). On true, payload is the plaintext.
        public static bool TryDecipher(ReadOnlySpan<byte> region, Blowfish bf, out byte[] payload)
        {
            payload = Array.Empty<byte>();
            if (region.Length < HashSize)
                return false;

            byte[] buf = region.ToArray();
            int words = buf.Length / 4;
            int tmp = words;
            tmp -= tmp % 2; // tmp % 2 == 0 already, but mirrors the C exactly

            uint[] w = ToWords(buf);
            bf.DecipherBlocks(w, tmp / 2);
            FromWords(w, buf);

            int payloadLen = buf.Length - HashSize;
            byte[] body = new byte[payloadLen];
            Array.Copy(buf, 0, body, 0, payloadLen);

            // checksum(): md5(body) == trailing 16 bytes.
            byte[] h = MD5.HashData(body);
            for (int i = 0; i < HashSize; i++)
                if (h[i] != buf[payloadLen + i])
                    return false;

            payload = body;
            return true;
        }

        private static uint[] ToWords(byte[] b)
        {
            int n = b.Length / 4;
            uint[] w = new uint[n];
            for (int i = 0; i < n; i++)
                w[i] = (uint)(b[i * 4] | (b[i * 4 + 1] << 8) | (b[i * 4 + 2] << 16) | (b[i * 4 + 3] << 24));
            return w;
        }

        private static void FromWords(uint[] w, byte[] b)
        {
            for (int i = 0; i < w.Length; i++)
            {
                b[i * 4] = (byte)(w[i] & 0xFF);
                b[i * 4 + 1] = (byte)((w[i] >> 8) & 0xFF);
                b[i * 4 + 2] = (byte)((w[i] >> 16) & 0xFF);
                b[i * 4 + 3] = (byte)((w[i] >> 24) & 0xFF);
            }
        }
    }
}
