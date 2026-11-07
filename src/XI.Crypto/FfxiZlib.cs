// C# port of src/common/zlib.cpp — the FFXI game-packet body compressor.
//
// This is NOT DEFLATE. It is a custom per-byte variable-length prefix code:
//   * compress.dat (512 uint32): enc[(sbyte)b + 0x80] = code word,
//                                  enc[(sbyte)b + 0x180] = code bit length (1..32).
//   * decompress.dat (2556 uint32): a serialized jump-tree the decoder walks LSB-first.
//
// The port is kept faithful to the compiled reference so it can be diffed
// byte-for-byte against oracle/zlib_golden.txt. See csharp/oracle/zlib_harness.cpp.
using System;

namespace XI.Crypto
{
    public static class FfxiZlib
    {
        private static readonly uint[] Enc;        // 512 words from compress.dat
        private static readonly int Root;          // jump-tree root element index
        private static readonly bool[] IsNull;     // dec[i] == 0
        private static readonly bool[] IsPtr;      // dec[i] > 0xff (a jump target)
        private static readonly int[] Target;      // element index for pointer cells
        private static readonly byte[] Data;       // 0..0xff payload for data cells
        private static readonly byte[] DtabSymbol; // fast 8-bit decode: symbol
        private static readonly byte[] DtabLength; // fast 8-bit decode: length (0 => >8-bit code)

        static FfxiZlib()
        {
            if (!BitConverter.IsLittleEndian)
                throw new PlatformNotSupportedException("zlib tables are little-endian; big-endian hosts need byteswap as in zlib.cpp.");

            Enc = ReadU32Array(ZlibTables.Compress);
            uint[] dec = ReadU32Array(ZlibTables.Decompress);

            int n = dec.Length;
            IsNull = new bool[n];
            IsPtr = new bool[n];
            Target = new int[n];
            Data = new byte[n];

            // base = dec[0] - sizeof(uint32). Pointers normalize to element offsets.
            long @base = (long)dec[0] - 4;
            for (int i = 0; i < n; i++)
            {
                if (dec[i] > 0xff)
                {
                    IsPtr[i] = true;
                    Target[i] = (int)(((long)dec[i] - @base) / 4);
                }
                else
                {
                    IsNull[i] = dec[i] == 0;
                    Data[i] = (byte)dec[i];
                }
            }

            // root = (zlib_jump*)jump[0].ptr
            Root = Target[0];

            DtabSymbol = new byte[256];
            DtabLength = new byte[256];
            BuildDecodeTable();
        }

        private static void BuildDecodeTable()
        {
            for (int b = 0; b < 256; b++)
            {
                int e = Root;
                DtabSymbol[b] = 0;
                DtabLength[b] = 0;

                for (int bit = 0; bit < 8; bit++)
                {
                    e = Descend(e, (b >> bit) & 1);
                    if (IsLeaf(e))
                    {
                        DtabSymbol[b] = Data[e + 3];
                        DtabLength[b] = (byte)(bit + 1);
                        break;
                    }
                }
            }
        }

        private static bool IsLeaf(int e) => IsNull[e] && IsNull[e + 1];

        // jmp = jmp[bit].ptr : move to the child element referenced by cell (e+bit).
        private static int Descend(int e, int bit) => Target[e + bit];

        // Equivalent of zlib_compress. Returns the same value as the C reference:
        // total bits written + 8. Writes out[0]=1 flag then the bitstream at out[1..].
        public static int Compress(ReadOnlySpan<byte> input, byte[] output)
        {
            // out_sz in the C version is the byte capacity; max_sz = (out_sz-1)*8 bits.
            uint outSz = (uint)output.Length;
            uint maxSz = (outSz - 1) * 8;

            uint accBits = 0;
            uint outPos = 0;
            uint read = 0;
            ulong acc = 0;

            for (int i = 0; i < input.Length; i++)
            {
                int sb = (sbyte)input[i];
                uint elem = Enc[sb + 0x180];
                if (elem + read >= maxSz)
                    throw new InvalidOperationException("zlib_compress: output buffer too small (mirror of C's -1/garbage path).");

                uint v = Enc[sb + 0x80];
                uint code = elem >= 32 ? v : (v & ((1u << (int)elem) - 1));

                acc |= (ulong)code << (int)accBits;
                accBits += elem;
                read += elem;

                while (accBits >= 8)
                {
                    output[1 + outPos++] = (byte)(acc & 0xFF);
                    acc >>= 8;
                    accBits -= 8;
                }
            }

            if (accBits > 0)
                output[1 + outPos] = (byte)(acc & 0xFF);

            output[0] = 1;
            return (int)(read + 8);
        }

        // Convenience wrapper: allocate a worst-case buffer (prefix codes can be up to
        // 32 bits/byte + 1 flag), compress, and return exactly the meaningful bytes.
        public static byte[] CompressToBytes(ReadOnlySpan<byte> input)
        {
            byte[] scratch = new byte[1 + ((input.Length * 32) + 7) / 8 + 1];
            int outBits = Compress(input, scratch);
            int outBytes = (outBits + 7) / 8;
            byte[] result = new byte[outBytes];
            Array.Copy(scratch, result, outBytes);
            return result;
        }

        // Equivalent of zlib_decompress. inBits is the BIT count zlib_compress returned
        // (matches the uint32 on the wire). Returns number of output bytes written.
        public static int Decompress(byte[] input, uint inBits, byte[] output)
        {
            if (input[0] != 1)
                return -1;

            uint payloadBytes = (inBits + 7) / 8; // meaningful bytes in input (incl. flag byte)
            uint w = 0;
            uint bitpos = 0;
            uint outSz = (uint)output.Length;

            while (bitpos < inBits && w < outSz)
            {
                uint byteoff = bitpos >> 3;
                int bitoff = (int)(bitpos & 7);

                uint lo = ReadByte(input, (int)(1 + byteoff));
                uint hi = (byteoff + 1 < payloadBytes) ? ReadByte(input, (int)(1 + byteoff + 1)) : 0u;
                uint peek = (lo >> bitoff) | (hi << (8 - bitoff));

                byte len = DtabLength[peek & 0xFF];
                if (len != 0)
                {
                    output[w++] = DtabSymbol[peek & 0xFF];
                    bitpos += len;
                }
                else
                {
                    int e = Root;
                    for (;;)
                    {
                        int bit = (int)((ReadByte(input, (int)(1 + (bitpos >> 3))) >> (int)(bitpos & 7)) & 1);
                        e = Descend(e, bit);
                        ++bitpos;
                        if (IsLeaf(e))
                        {
                            output[w++] = Data[e + 3];
                            break;
                        }
                    }
                }
            }

            return (int)w;
        }

        // Read a byte from the payload, treating offsets past the array as 0. The C
        // reference reads adjacent scratch bytes here; those only affect the single
        // trailing pad symbol (verified non-deterministic), never the input prefix.
        private static uint ReadByte(byte[] a, int i) =>
            (uint)((i >= 0 && i < a.Length) ? a[i] : 0);

        private static uint[] ReadU32Array(byte[] b)
        {
            int count = b.Length / 4;
            uint[] r = new uint[count];
            for (int i = 0; i < count; i++)
                r[i] = (uint)(b[i * 4] | (b[i * 4 + 1] << 8) | (b[i * 4 + 2] << 16) | (b[i * 4 + 3] << 24));
            return r;
        }
    }
}
