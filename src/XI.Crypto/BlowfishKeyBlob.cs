// The db::extractFromBlob C-array branch for the map session key (the persistence -> crypto seam).
//
// Engine: map_networking.cpp:346 reads accounts_sessions.session_key (binary(20)) and memcpy's it
// into blowfish_t::key (uint32 key[5]) via result_set.h's extractFromBlob, then initBlowfish()
// derives the map Blowfish cipher from those 5 words. So THIS is how a live session_key blob
// becomes a working map cipher — the last DB->cipher step of the Phase-0 handshake.
//
// All blowfish_t layout numbers are COMPILE-DERIVED from csharp/oracle/map_blowfish_t_golden.txt
// (iron rule: sizeof/offsetof come only from the compiled harness, never hand-computed):
//   sizeof=4208 alignof=4  key@0 hash@20 P@36 S@108 status@4204.

using System;

namespace XI.Crypto
{
    // Layout mirror of the engine blowfish_t (src/common/blowfish.h). Constants only; the live
    // map path uses key[5] + hash[16] + P[18] + S[4][256] + status.
    public static class BlowfishT
    {
        public const int SizeOf = 4208;
        public const int AlignOf = 4;
        public const int OffKey = 0;
        public const int OffHash = 20;
        public const int OffP = 36;
        public const int OffS = 108;
        public const int OffStatus = 4204;

        // blowfish_t::key sub-object (the blob destination).
        public const int KeyBytes = 20;        // sizeof(uint32[5])
        public const int KeyElems = 5;
        public const int KeyElemSize = 4;

        // BLOWFISH status enum (blowfish.h): BLOWFISH_WAITING..BLOWFISH_PENDING_ZONE.
        public const int StatusWaiting = 0;
        public const int StatusSent = 1;
        public const int StatusAccepted = 2;
        public const int StatusPendingZone = 3;
    }

    // Faithful port of db::extractFromBlob for the destination `uint32 key[5]` (std::is_array_v
    // branch, result_set.h:299):
    //   if (rset->isNull(blobKey)) return;                 // skip ENTIRELY -> fresh dest stays zero
    //   ranges::fill(key, uint32{});                        // zero-init every element (20 zero bytes)
    //   memcpy(&key, blob.data(), min(sizeof(key), blob.size()));
    // On x86 little-endian the resulting words are the LE u32 of the copied bytes, which the
    // compiled oracle (K32_* lines) pins exactly. Returns the 5 key words.
    public static class BlowfishKeyBlob
    {
        public static uint[] ExtractWords(byte[] blob, bool isNull)
        {
            // ranges::fill(destination, uint32{}) -> zero-init fresh words. On a NULL blob the
            // engine skips the whole body, and a fresh blowfish_t is already zero-initialized,
            // so zeros is the faithful result for both cases.
            var words = new uint[BlowfishT.KeyElems];
            if (isNull)
            {
                return words;
            }

            var blobLen = blob?.Length ?? 0;
            var n = Math.Min(BlowfishT.KeyBytes, blobLen); // std::min(sizeof(key), blobStr.size())
            for (var i = 0; i < n; i++)
            {
                // memcpy of raw bytes: word[k] receives bytes [4k, 4k+4) little-endian on x86.
                words[i / 4] |= (uint)blob[i] << ((i % 4) * 8);
            }

            return words;
        }

        // The full map_networking.cpp:346 -> initBlowfish() chain: session_key blob -> key[5] words
        // -> derived Blowfish cipher (via SessionCipher). onReturnHash (>=16) receives the hash.
        public static Blowfish ExtractToCipher(byte[] sessionKeyBlob, bool isNull, byte[] onReturnHash = null)
        {
            var key5 = ExtractWords(sessionKeyBlob, isNull);
            return SessionCipher.Create(key5, onReturnHash);
        }
    }
}
