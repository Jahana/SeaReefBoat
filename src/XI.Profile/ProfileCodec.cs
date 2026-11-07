// src/profile/protocol/codec.cpp + bytes.h: polcore's checksums, base64 / base32 alphabets, nick scrambling and the
// character key, plus the little-endian byte helpers the wire objects use.

using System;
using System.Buffers.Binary;
using System.Text;

namespace XI.Profile
{
    public static class ProfileCodec
    {
        private const string Base64Alphabet = "TSG8IncW3HFKokOg79qzeCmZs2yBYEQVAUxR5rbwi4P@jMDLtpvad0f_J1hlN6uX";
        private const string StandardAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        private const string Base32Alphabet = "N43OVHBJ1Y2C0WSXED5QFILRZMUTAPGK";
        private const string PolIdDigits = "EFKAOYMJVNGTDSWBQLPCIRHZXU6328401795";
        private const int PolIdLength = 8;
        private const uint PolIdBase = 36;
        private const ulong NickPolIdMask = 0x1FFFFFFFFFFul; // 41 bits
        private const ulong NickMarker = 0xC00000000000ul;
        private const char NickPrefix = 'U';

        // polcore's digit value of each character, -1 for characters outside its alphabet (codec.cpp kFromPolcore maps
        // them to '!' so OpenSSL rejects them)
        private static readonly int[] FromPolcore = BuildFromPolcore();

        private static int[] BuildFromPolcore()
        {
            var table = new int[256];
            Array.Fill(table, -1);
            for (int digit = 0; digit < Base64Alphabet.Length; digit++) table[Base64Alphabet[digit]] = digit;
            return table;
        }

        // checksum: sum of little-endian u32 words, a 1-3 byte tail shifted in from the top (sqCalcChecksum)
        public static uint Checksum(ReadOnlySpan<byte> bytes)
        {
            uint sum = 0;
            int offset = 0;
            for (; offset + 4 <= bytes.Length; offset += 4) sum += BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset));
            uint tail = 0;
            for (; offset < bytes.Length; ++offset) tail = (tail >> 8) | ((uint)bytes[offset] << 24);
            return sum + tail;
        }

        // lineChecksum: the 4 characters polcore appends to every IRC line before CRLF
        public static string LineChecksum(string line)
        {
            uint sum = Checksum(Latin1(line));
            var result = new StringBuilder(4);
            foreach (int shift in new[] { 26, 20, 14, 8 }) result.Append((char)(((sum >> shift) & 0x3F) + 0x3F));
            return result.ToString();
        }

        // base64 (codec.cpp): EVP_EncodeBlock, then the standard alphabet translated to polcore's; polcore pads a short
        // group with its zero digit instead of '=', unpadded drops the padding
        private static string Base64(ReadOnlySpan<byte> bytes, bool unpadded)
        {
            string text = Convert.ToBase64String(bytes);
            if (unpadded) text = text.TrimEnd('=');
            var result = new char[text.Length];
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                result[i] = c == '=' ? Base64Alphabet[0] : Base64Alphabet[StandardAlphabet.IndexOf(c)];
            }
            return new string(result);
        }

        public static string Base64Encode(ReadOnlySpan<byte> bytes) => Base64(bytes, false);

        public static string Base64EncodeUnpadded(ReadOnlySpan<byte> bytes) => Base64(bytes, true);

        // base64Decode: EVP_DecodeBlock over the translated text; no padding, so every group is 3 bytes. A length that
        // is not whole groups, or a character outside the alphabet, fails.
        public static byte[] Base64Decode(string text)
        {
            if (text.Length % 4 != 0) return null;
            var bytes = new byte[text.Length / 4 * 3];
            for (int group = 0; group < text.Length / 4; group++)
            {
                uint word = 0;
                for (int i = 0; i < 4; i++)
                {
                    char c = text[group * 4 + i];
                    int digit = c < 256 ? FromPolcore[c] : -1;
                    if (digit < 0) return null;
                    word = (word << 6) | (uint)digit;
                }
                bytes[group * 3] = (byte)(word >> 16);
                bytes[group * 3 + 1] = (byte)(word >> 8);
                bytes[group * 3 + 2] = (byte)word;
            }
            return bytes;
        }

        // base32Encode: polcore's alphabet, input must be whole 5-byte groups
        public static string Base32Encode(ReadOnlySpan<byte> bytes)
        {
            var result = new StringBuilder();
            for (int bit = 0; bit + 5 <= bytes.Length * 8; bit += 5)
            {
                int b = bit / 8;
                uint word = (uint)bytes[b] << 8;
                if (b + 1 < bytes.Length) word |= bytes[b + 1];
                result.Append(Base32Alphabet[(int)((word >> (11 - bit % 8)) & 31)]);
            }
            return result.ToString();
        }

        // base36: 8 digits, polcore's alphabet
        private static string Base36(ulong value)
        {
            var digits = new char[PolIdLength];
            for (int i = PolIdLength - 1; i >= 0; i--)
            {
                digits[i] = PolIdDigits[(int)(value % PolIdBase)];
                value /= PolIdBase;
            }
            return new string(digits);
        }

        // scrambleNick: "U" + 8 scrambled characters
        public static string ScrambleNick(ulong polId)
        {
            ulong value = (polId & NickPolIdMask) | NickMarker;
            foreach (int b in new[] { 4, 3, 2, 1, 0 }) value ^= (value >> 8) & (0xFFul << (8 * b));
            return NickPrefix + Base36(value & ~NickMarker);
        }

        // characterKey: same as the lobby's, id bits 0-15, and 16-23 in the top byte
        public static uint CharacterKey(uint characterId) => (characterId & 0xFFFF) | ((characterId >> 16) & 0xFF) << 24;

        public static uint CharacterIdFromKey(uint key) => (key & 0xFFFF) | ((key >> 24) & 0xFF) << 16;

        // the wire's strings are bytes: one char per byte both ways
        public static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

        public static string Latin1(ReadOnlySpan<byte> bytes) => Encoding.Latin1.GetString(bytes);

        // asStringFromUntrustedSource(ptr, max_size): the bytes up to the first NUL, at most max_size
        public static byte[] CString(ReadOnlySpan<byte> field)
        {
            int n = field.IndexOf((byte)0);
            return (n < 0 ? field : field.Slice(0, n)).ToArray();
        }

        // std::memcpy(field, name.data(), std::min(name.size(), sizeof(field) - 1)): a NUL always fits
        public static void PutName(Span<byte> field, ReadOnlySpan<byte> name)
        {
            name.Slice(0, Math.Min(name.Length, field.Length - 1)).CopyTo(field);
        }
    }
}
