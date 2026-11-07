// C# port of the deterministic-pure subset of src/login/otp_helpers.h (namespace otpHelpers).
// These are the offline-oracle-verifiable pieces of the login TOTP / trust-token surface: they
// derive exact byte values (TOTP codes, SHA256 hex) from inputs, so tests/XI.Verify.Login asserts
// them against the compiled engine oracle (csharp/oracle/login_otp_harness.cpp ->
// login_otp_golden.txt), which links the engine's vendored OpenSSL (ext/openssl/lib64/libcrypto.lib)
// so the HMAC-SHA1 / SHA256 outputs come from the SAME library the client uses -- NOT hand-derived
// (repo iron rule). BCL HMACSHA1/SHA256 are confirmed byte-equal to OpenSSL's by the gate.
//
// NOT ported here (live/RNG/DB boundary, deferred): getNewBase32Secret + generateTrustToken
// (xirand / OpenSSL RAND_bytes), validateTOTP (wall-clock + a Synchronized replay-guard map),
// and every db::preparedStmt path (doesAccountNeedOTP / createAccountSecret /
// regenerateAccountRecoveryCode / getAccountSecret / getAccountRecoveryCode / saveTrustToken /
// validateTrustToken / removeAllTrustTokens).

using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace XI.Login
{
    public static class LoginOtpHelpers
    {
        // otpHelpers::base32Decode. RFC-4648 base32 alphabet (A-Z=0..25, 2..7=26..31). The
        // characters '=', ' ', '-' and '_' are skipped. Bytes are uppercased with the C-locale
        // toupper (ASCII a-z only -- std::toupper does NOT fold >=0x80), then table-looked-up;
        // any value not in the table (incl. a high byte, '0'/'1'/'8'/'9') throws. The bit reader
        // keeps `buffer`/`bitsLeft` as 32-bit ints exactly like the engine: a fresh 5-bit value is
        // shifted in, and once >=8 bits are held the top byte is emitted (leftover bits stay).
        public static byte[] Base32Decode(string base32)
        {
            var bytes = new List<byte>();
            int buffer = 0;
            int bitsLeft = 0;

            foreach (char raw in base32)
            {
                if (raw == '=' || raw == ' ' || raw == '-' || raw == '_') continue;

                // C-locale toupper: only ASCII a-z folds to A-Z; every other byte is unchanged.
                char ch = (raw >= 'a' && raw <= 'z') ? (char)(raw - 32) : raw;

                int val;
                if (ch >= 'A' && ch <= 'Z') val = ch - 'A';
                else if (ch >= '2' && ch <= '7') val = 26 + (ch - '2');
                else throw new ArgumentException("Invalid Base32 character");

                buffer <<= 5;
                buffer |= val;
                bitsLeft += 5;
                if (bitsLeft >= 8)
                {
                    bitsLeft -= 8;
                    bytes.Add((byte)((buffer >> bitsLeft) & 0xFF));
                }
            }

            return bytes.ToArray();
        }

        // otpHelpers::generateTOTP. counter = epochSeconds / period, HMAC-SHA1 over the 8-byte
        // big-endian counter, RFC-4226 dynamic truncation, then otp = binaryCode % 1000000.
        // CRITICAL engine quirk: the modulus is ALWAYS 1e6 regardless of `digits`; `digits` only
        // sets the snprintf "%0*u" MIN WIDTH -- so otp is zero-padded up to `digits` but is NEVER
        // truncated when it has more digits than `digits` (e.g. digits=4 keeps a 6-digit otp).
        public static string GenerateTotp(string base32Secret, ulong epochSeconds, int digits = 6, int period = 30)
        {
            byte[] key = Base32Decode(base32Secret);
            ulong counter = epochSeconds / (ulong)period;

            byte[] counterBytes = new byte[8];
            for (int i = 7; i >= 0; i--)
            {
                counterBytes[i] = (byte)(counter & 0xFF);
                counter >>= 8;
            }

            byte[] hash;
            using (var hmac = new HMACSHA1(key))
            {
                hash = hmac.ComputeHash(counterBytes);
            }

            int offset = hash[hash.Length - 1] & 0x0F;
            uint binaryCode = (uint)(
                ((hash[offset] & 0x7F) << 24) |
                ((hash[offset + 1] & 0xFF) << 16) |
                ((hash[offset + 2] & 0xFF) << 8) |
                (hash[offset + 3] & 0xFF));

            uint otp = binaryCode % 1000000;

            // %0*u: pad with leading zeros to `digits`, but never shorten a longer value.
            return otp.ToString().PadLeft(digits, '0');
        }

        // otpHelpers::toHexString: lowercase hex, "0123456789abcdef".
        public static string ToHexString(byte[] data)
        {
            const string hexChars = "0123456789abcdef";
            var sb = new System.Text.StringBuilder(data.Length * 2);
            foreach (byte b in data)
            {
                sb.Append(hexChars[(b >> 4) & 0x0F]);
                sb.Append(hexChars[b & 0x0F]);
            }
            return sb.ToString();
        }

        // otpHelpers::isValidTrustTokenFormat: exactly 64 bytes, all C-locale isxdigit == ASCII
        // [0-9A-Fa-f] only (a high byte like 0xE9 is NOT a hex digit here). char.IsAsciiHexDigit
        // matches that byte-for-byte; the Unicode-aware char.IsHexDigit would also pass (there are
        // no non-ASCII hex-digit codepoints), but IsAsciiHexDigit states the intent precisely.
        public static bool IsValidTrustTokenFormat(string token)
        {
            if (token.Length != 64) return false;
            foreach (char c in token)
            {
                if (!char.IsAsciiHexDigit(c)) return false;
            }
            return true;
        }

        // otpHelpers::hashTrustToken: SHA256(token bytes) rendered as lowercase hex. BCL SHA256 is
        // byte-equal to OpenSSL's SHA256 (proven by the golden's ""/abc vectors), so this is a
        // faithful stand-in -- the same reasoning as MD5.HashData for the lobby error packet.
        public static string HashTrustToken(string token)
        {
            byte[] data = System.Text.Encoding.Latin1.GetBytes(token);
            byte[] hash = SHA256.HashData(data);
            return ToHexString(hash);
        }
    }
}
