// Reproduces the map-session cipher setup from src/map/map_session.cpp
// (MapSession::initBlowfish). The client's 20 raw key bytes (blowfish.key[5],
// little-endian) are MD5'd into a 16-byte hash; the hash is then truncated at its
// first zero byte (everything from that index on is forced to 0); that 16-byte
// value becomes the Blowfish key.

using System;
using System.Security.Cryptography;

namespace XI.Crypto
{
    public static class SessionCipher
    {
        // key5 == blowfish.key[5] (4 words = client ip/gid, 1 word = incrementing counter).
        // onReturnHash, if non-null (len>=16), receives the post-truncation 16-byte hash.
        public static Blowfish Create(uint[] key5, byte[] onReturnHash = null)
        {
            if (key5 == null || key5.Length != 5)
                throw new ArgumentException("key5 must be 5 words", nameof(key5));

            Span<byte> raw = stackalloc byte[20];
            for (int i = 0; i < 5; i++)
            {
                raw[i * 4 + 0] = (byte)(key5[i] & 0xFF);
                raw[i * 4 + 1] = (byte)((key5[i] >> 8) & 0xFF);
                raw[i * 4 + 2] = (byte)((key5[i] >> 16) & 0xFF);
                raw[i * 4 + 3] = (byte)((key5[i] >> 24) & 0xFF);
            }

            byte[] hash = MD5.HashData(raw.ToArray());

            // truncate at first zero byte, exactly as initBlowfish.
            for (int i = 0; i < 16; i++)
            {
                if (hash[i] == 0)
                {
                    for (int j = i; j < 16; j++)
                        hash[j] = 0;
                    break;
                }
            }

            if (onReturnHash != null)
                Array.Copy(hash, onReturnHash, 16);

            var bf = new Blowfish();
            bf.Init(hash);
            return bf;
        }

        // Mirrors MapSession::incrementBlowfish(): counter (key[4]) += 2, re-derive.
        public static void IncrementKey(uint[] key5)
        {
            unchecked { key5[4] += 2; }
        }
    }
}
