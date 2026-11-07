// C# port of the FFXI (LandSandBoat) game-packet Blowfish cipher.
// Faithful to src/common/blowfish.cpp, including its two quirks that a textbook
// Blowfish would get wrong:
//   1. The F-function (TT) uses an S-table scramble, not the canonical F.
//   2. blowfish_init sign-extends each key byte (the key is a signed int8 in C),
//      so bytes >= 0x80 fold in as 0xFFFFFFxx. The verifier's 0x88- and 0xFF-first
//      keys exercise exactly this.
// Byte-for-byte parity with the compiled reference is asserted in tests/XI.Verify.

using System;

namespace XI.Crypto
{
    public sealed class Blowfish
    {
        private readonly uint[] _p = new uint[18];
        private readonly uint[] _s = new uint[1024]; // flattened S[4][256]

        public uint[] P => _p;
        public uint[] S => _s;

        private const int N = 16;

        private static uint TT(uint working, uint[] s)
        {
            unchecked
            {
                return
                    ((s[256 + ((working >> 8) & 0xff)] & 1) ^ 32) +
                    ((s[768 + (working >> 24)] & 1) ^ 32) +
                    s[512 + ((working >> 16) & 0xff)] +
                    s[working & 0xff];
            }
        }

        public void Encipher(ref uint xl, ref uint xr)
        {
            unchecked
            {
                uint Xl = xl, Xr = xr, temp;
                for (int i = 0; i < N; ++i)
                {
                    Xl ^= _p[i];
                    Xr = TT(Xl, _s) ^ Xr;
                    temp = Xl; Xl = Xr; Xr = temp;
                }
                temp = Xl; Xl = Xr; Xr = temp;
                Xr ^= _p[N];
                Xl ^= _p[N + 1];
                xl = Xl; xr = Xr;
            }
        }

        public void Decipher(ref uint xl, ref uint xr)
        {
            unchecked
            {
                uint Xl = xl, Xr = xr, temp;
                for (int i = N + 1; i > 1; --i)
                {
                    Xl ^= _p[i];
                    Xr = TT(Xl, _s) ^ Xr;
                    temp = Xl; Xl = Xr; Xr = temp;
                }
                temp = Xl; Xl = Xr; Xr = temp;
                Xr ^= _p[1];
                Xl ^= _p[0];
                xl = Xl; xr = Xr;
            }
        }

        // Mirrors blowfish_init(const int8 key[], int16 keybytes, uint32* P, uint32* S).
        public void Init(ReadOnlySpan<byte> key)
        {
            unchecked
            {
                // memcpy(P, subkey, 72); memcpy(S, subkey + 72, 4096);
                Span<byte> subkey = BlowfishSubkey.Bytes;
                for (int i = 0; i < 18; i++)
                    _p[i] = ReadU32LE(subkey.Slice(i * 4));
                for (int i = 0; i < 1024; i++)
                    _s[i] = ReadU32LE(subkey.Slice(72 + i * 4));

                int keybytes = key.Length;
                int j = 0;
                for (int i = 0; i < N + 2; i++)
                {
                    uint data = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        // key[j] is int8 -> sign-extends to int before the |.
                        data = (data << 8) | (uint)(int)(sbyte)key[j];
                        j = j + 1;
                        if (j >= keybytes)
                            j = 0;
                    }
                    _p[i] = _p[i] ^ data;
                }

                // datal/datar carry across BOTH the P and S reconstruction loops.
                uint datal = 0, datar = 0;
                for (int i = 0; i < N + 2; i += 2)
                {
                    Encipher(ref datal, ref datar);
                    _p[i] = datal;
                    _p[i + 1] = datar;
                }
                for (int i = 0; i < 4; i++)
                {
                    for (int jj = 0; jj < 256; jj += 2)
                    {
                        Encipher(ref datal, ref datar);
                        _s[i * 256 + jj] = datal;
                        _s[i * 256 + jj + 1] = datar;
                    }
                }
            }
        }

        public void EncipherBlocks(Span<uint> data, int count)
        {
            for (int b = 0; b < count; b++)
            {
                uint l = data[b * 2], r = data[b * 2 + 1];
                Encipher(ref l, ref r);
                data[b * 2] = l; data[b * 2 + 1] = r;
            }
        }

        public void DecipherBlocks(Span<uint> data, int count)
        {
            for (int b = 0; b < count; b++)
            {
                uint l = data[b * 2], r = data[b * 2 + 1];
                Decipher(ref l, ref r);
                data[b * 2] = l; data[b * 2 + 1] = r;
            }
        }

        private static uint ReadU32LE(ReadOnlySpan<byte> b) =>
            (uint)(b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24));
    }
}
