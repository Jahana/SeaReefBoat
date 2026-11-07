// C# port of the stateless byte-level half of src/map/map_networking.cpp:
//   recv_parse's plaintext-0x00A gate (md5 checksum + LoginPacketCheck), map_decipher_packet, the zlib
//   decompress step, preparePacket / compressPacket / finalizePacket, and parse()'s sub-packet walk.
//
// Datagram layout (both directions):
//   [28-byte header][ffxi-zlib bitstream][u32 bit count][md5 of the previous two] -- everything after the header
//   enciphered in (len/4 & ~1) words. An unencrypted 0x00A has the plain sub-packet stream instead of the bitstream.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using XI.Crypto;

namespace XI.Map
{
    public readonly struct SubPacket
    {
        public readonly int Offset;   // into the decompressed buffer (header included)
        public readonly int Size;     // bytes: (byte1 & 0xFE) * 2
        public readonly ushort Type;  // u16 & 0x1FF
        public readonly ushort Sync;  // u16 at +2

        public SubPacket(int offset, int size, ushort type, ushort sync)
        {
            Offset = offset;
            Size = size;
            Type = type;
            Sync = sync;
        }
    }

    public readonly struct LoginRequest
    {
        public readonly uint CharId;     // UniqueNo
        public readonly ushort Retry;    // unknown00: goes up by one on every client retry
        public readonly ushort Language; // uCliLang

        public LoginRequest(uint charId, ushort retry, ushort language)
        {
            CharId = charId;
            Retry = retry;
            Language = language;
        }
    }

    public static class MapFraming
    {
        public const int HeaderSize = 0x1C;              // FFXI_HEADER_SIZE
        public const int HashSize = 16;
        public const int MaxBufferSize = 2500;           // kMaxBufferSize
        public const int MaxPacketPerCompression = 32;   // kMaxPacketPerCompression
        public const int MaxClientPayload = 1300 - HeaderSize - HashSize; // largest compressed size the client accepts
        public const int PacketSize = 0x1FF;             // PACKET_SIZE (CBasicPacket buffer)

        // GP_CLI_COMMAND_LOGIN (pack default, 4-byte GP_CLI_HEADER first).
        public const int LoginSize = 92;
        public const int LoginCheckOffset = 4;   // LoginPacketCheck
        public const int LoginRetryOffset = 6;   // unknown00
        public const int LoginSumOffset = 8;     // offsetof(unknown01): start of the summed range
        public const int LoginCharIdOffset = 12; // UniqueNo
        public const int LoginLangOffset = 88;   // uCliLang

        // checksum(): md5(buf[28 .. size-16]) == buf[size-16 .. size].
        public static bool ChecksumMatches(byte[] buf, int size)
        {
            if (size <= HeaderSize + HashSize) return false;
            byte[] h = MD5.HashData(new ReadOnlySpan<byte>(buf, HeaderSize, size - HeaderSize - HashSize));
            return new ReadOnlySpan<byte>(buf, size - HashSize, HashSize).SequenceEqual(h);
        }

        // recv_parse's unencrypted branch up to the session lookup: md5 ok, first sub-packet is 0x00A, big enough,
        // and the additive LoginPacketCheck over [unknown01, end) matches.
        public static bool TryReadPlainLogin(byte[] buf, int size, out LoginRequest login)
        {
            login = default;
            if (!ChecksumMatches(buf, size)) return false;
            if ((BitConverter.ToUInt16(buf, HeaderSize) & 0x1FF) != 0x00A) return false;
            if (size < HeaderSize + LoginSize) return false;

            byte sum = 0;
            for (int i = LoginSumOffset; i < LoginSize; i++) sum += buf[HeaderSize + i];
            if (sum != buf[HeaderSize + LoginCheckOffset]) return false;

            login = new LoginRequest(BitConverter.ToUInt32(buf, HeaderSize + LoginCharIdOffset),
                                     BitConverter.ToUInt16(buf, HeaderSize + LoginRetryOffset),
                                     BitConverter.ToUInt16(buf, HeaderSize + LoginLangOffset));
            return true;
        }

        // map_decipher_packet: decipher in place, then verify the md5. On failure buf is left deciphered (as in C++),
        // so callers that need a retry with another key must keep a copy first.
        public static bool Decipher(byte[] buf, int size, Blowfish bf)
        {
            if (size < HeaderSize) return false;
            int words = (size - HeaderSize) / 4;
            words -= words % 2;
            var w = new uint[words];
            for (int i = 0; i < words; i++) w[i] = BitConverter.ToUInt32(buf, HeaderSize + i * 4);
            bf.DecipherBlocks(w, words / 2);
            for (int i = 0; i < words; i++) BitConverter.TryWriteBytes(new Span<byte>(buf, HeaderSize + i * 4, 4), w[i]);
            return ChecksumMatches(buf, size);
        }

        // recv_parse's tail: replace the bitstream after the header with the decompressed sub-packets.
        // Returns the new total size, or -1 when the decompressor rejects the stream (C++ then keeps the old size).
        public static int Decompress(byte[] buf, int size)
        {
            if (size < HeaderSize + 4 + HashSize) return -1;
            uint bits = BitConverter.ToUInt32(buf, size - 4 - HashSize);
            int streamLen = size - HeaderSize;
            var input = new byte[streamLen];
            Array.Copy(buf, HeaderSize, input, 0, streamLen);
            var output = new byte[MaxBufferSize];
            int n = FfxiZlib.Decompress(input, bits, output);
            if (n < 0) return -1;
            n = Math.Min(n, buf.Length - HeaderSize);
            Array.Copy(output, 0, buf, HeaderSize, n);
            return HeaderSize + n;
        }

        // parse()'s loop bound: walk while the sub-packet fits and has a non-zero size.
        public static IEnumerable<SubPacket> SubPackets(byte[] buf, int size)
        {
            int p = HeaderSize;
            while (p + 4 <= size)
            {
                int sz = (buf[p + 1] & 0xFE) * 2;
                if (sz == 0 || p + sz > size) yield break;
                yield return new SubPacket(p, sz, (ushort)(BitConverter.ToUInt16(buf, p) & 0x1FF), BitConverter.ToUInt16(buf, p + 2));
                p += sz;
            }
        }

        // preparePacket: [0]=server_packet_id, [2]=client_packet_id, [8]=u32 unix time; other header bytes zero.
        public static void PrepareHeader(byte[] buf, ushort serverPacketId, ushort clientPacketId, uint timestamp)
        {
            Array.Clear(buf, 0, HeaderSize);
            BitConverter.TryWriteBytes(new Span<byte>(buf, 0, 2), serverPacketId);
            BitConverter.TryWriteBytes(new Span<byte>(buf, 2, 2), clientPacketId);
            BitConverter.TryWriteBytes(new Span<byte>(buf, 8, 4), timestamp);
        }

        // compressPacket: the bitstream plus its trailing u32 bit count, i.e. the bytes finalizePacket hashes.
        // Null when the stream would not fit kMaxBufferSize (zlib_compress's -1).
        public static byte[] Compress(ReadOnlySpan<byte> subPackets)
        {
            var scratch = new byte[MaxBufferSize];
            int bits;
            try { bits = FfxiZlib.Compress(subPackets, scratch); }
            catch (InvalidOperationException) { return null; }
            int bytes = (bits + 7) / 8; // zlib_compressed_size
            if (bytes + 4 > MaxBufferSize) return null;
            var result = new byte[bytes + 4];
            Array.Copy(scratch, result, bytes);
            BitConverter.TryWriteBytes(new Span<byte>(result, bytes, 4), (uint)bits);
            return result;
        }

        // finalizePacket: header || encipher(compressed || md5(compressed)).
        public static byte[] Finalize(byte[] header, byte[] compressed, Blowfish bf)
        {
            byte[] region = PacketFraming.EncipherPayload(compressed, bf);
            var datagram = new byte[HeaderSize + region.Length];
            Array.Copy(header, datagram, HeaderSize);
            Array.Copy(region, 0, datagram, HeaderSize, region.Length);
            return datagram;
        }

        // Client-side counterpart (what the retail client sends): header + plain sub-packet stream + md5. Used to drive
        // the C++ map server and the C# port with the same bytes.
        public static byte[] BuildPlainDatagram(ushort clientSeq, ushort ackServerSeq, ReadOnlySpan<byte> subPackets)
        {
            var d = new byte[HeaderSize + subPackets.Length + HashSize];
            PrepareHeader(d, clientSeq, ackServerSeq, 0);
            subPackets.CopyTo(new Span<byte>(d, HeaderSize, subPackets.Length));
            MD5.HashData(subPackets).CopyTo(d, HeaderSize + subPackets.Length);
            return d;
        }

        // Fills in LoginPacketCheck for a GP_CLI_COMMAND_LOGIN body (header included, LoginSize bytes).
        public static void SignLogin(byte[] login)
        {
            byte sum = 0;
            for (int i = LoginSumOffset; i < LoginSize; i++) sum += login[i];
            login[LoginCheckOffset] = sum;
        }
    }
}
