using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XI.Crypto;

namespace XI.Bots
{
    // The client's /sea (src/search/search_handler.cpp, ported in XI.Search.SearchHandler): a TCP connection per
    // search, one request under blowfish (md5 of 16 fixed bytes + the request's last 4), answered by search list
    // packets under md5 of those 20 bytes + 4 of the request's payload, the last one flagged final.
    public static class SearchClient
    {
        private static readonly byte[] KeyBase = { 0x30, 0x73, 0x3D, 0x6D, 0x3C, 0x31, 0x49, 0x5A, 0x32, 0x7A, 0x42, 0x43, 0x63, 0x38, 0x7B, 0x7E };
        private const byte TypeSearch = 0x03;
        private const int EntryName = 0x00, EntryArea = 0x01;

        public sealed class Found
        {
            public string Name;
            public int Zone = -1;
        }

        // the chars the replies list, and the total the server found (CSearchListPacket's u16 at 0x0E: data_loader's
        // GetPlayersList lists at most 40 of them)
        public sealed class Results : List<Found>
        {
            public int Total;
        }

        // /sea <zone>: every char the search server lists in it
        public static async Task<Results> SearchZone(string host, int port, ushort zone, uint key, CancellationToken ct)
        {
            // the query bit stream: an area entry (5-bit type, sort, present, 10-bit zone), its size byte first
            var bits = new byte[16];
            int at = Pack(bits, EntryArea, 0, 5);
            at = Pack(bits, 0, at, 1);
            at = Pack(bits, 1, at, 1);
            at = Pack(bits, zone, at, 10);
            int size = (at + 7) / 8 + 1;
            var payload = new[] { (byte)size }.Concat(bits.Take(size)).ToArray();
            var plain = Request(TypeSearch, payload, key);

            using var tcp = new TcpClient { NoDelay = true };
            await tcp.ConnectAsync(Endpoints.Checked(host, port), ct);
            var s = tcp.GetStream();
            await s.WriteAsync(Encrypt(plain), ct);

            var replyKey = KeyBase.Concat(plain.Skip(plain.Length - 4).Take(4)).Concat(plain.Skip(plain.Length - 0x18).Take(4)).ToArray();
            var found = new Results();
            var acc = new List<byte>();
            var buf = new byte[8192];
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            while (true)
            {
                int n;
                try { n = await s.ReadAsync(buf, cts.Token); }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new TimeoutException("search: no final packet"); }
                if (n <= 0) throw new IOException("search: closed before the final packet");
                acc.AddRange(new ArraySegment<byte>(buf, 0, n));
                while (acc.Count >= 2)
                {
                    int len = BinaryPrimitives.ReadUInt16LittleEndian(acc.GetRange(0, 2).ToArray());
                    if (len < 28) throw new InvalidDataException($"search: bad packet size {len}");
                    if (acc.Count < len) break;
                    var p = acc.GetRange(0, len).ToArray();
                    acc.RemoveRange(0, len);
                    Decrypt(p, replyKey);
                    if (!MD5.HashData(p.AsSpan(8, len - 28)).AsSpan().SequenceEqual(p.AsSpan(len - 20, 16)))
                        throw new InvalidDataException("search: reply hash mismatch");
                    found.Total = Math.Max(found.Total, BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(0x0E)));
                    found.AddRange(Entities(p));
                    if (p[0x0A] == 0x80) return found; // the final packet
                }
            }
        }

        // a request: payload from 0x10, 4 bytes folded into the reply key, md5, the request key
        private static byte[] Request(byte type, byte[] payload, uint key)
        {
            int length = Math.Max((0x10 + payload.Length + 4 + 20 + 3) / 4 * 4, 28);
            var b = new byte[length];
            BinaryPrimitives.WriteUInt16LittleEndian(b, (ushort)length);
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4), 0x46465849); // "IXFF"
            b[0x0B] = type;
            payload.CopyTo(b, 0x10);
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(length - 0x18), (uint)Environment.TickCount);
            MD5.HashData(b.AsSpan(8, length - 28)).CopyTo(b, length - 20);
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(length - 4), key);
            return b;
        }

        private static Blowfish Cipher(byte[] key)
        {
            var bf = new Blowfish();
            bf.Init(MD5.HashData(key));
            return bf;
        }

        private static byte[] Encrypt(byte[] plain)
        {
            var b = (byte[])plain.Clone();
            int length = b.Length;
            var bf = Cipher(KeyBase.Concat(b.Skip(length - 4).Take(4)).ToArray());
            int tmp = (length - 12) / 4;
            tmp -= tmp % 2;
            for (int i = 0; i < tmp; i += 2)
            {
                uint l = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan((i + 2) * 4)), r = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan((i + 3) * 4));
                bf.Encipher(ref l, ref r);
                BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan((i + 2) * 4), l);
                BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan((i + 3) * 4), r);
            }
            return b;
        }

        private static void Decrypt(byte[] p, byte[] key)
        {
            var bf = Cipher(key);
            int tmp = (byte)((p.Length - 12) / 4);
            tmp -= tmp % 2;
            for (int i = 0; i < tmp; i += 2)
            {
                uint l = BinaryPrimitives.ReadUInt32LittleEndian(p.AsSpan((i + 2) * 4)), r = BinaryPrimitives.ReadUInt32LittleEndian(p.AsSpan((i + 3) * 4));
                bf.Decipher(ref l, ref r);
                BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan((i + 2) * 4), l);
                BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan((i + 3) * 4), r);
            }
        }

        // a search list packet's entries: a size byte, then (5-bit type, value) fields
        private static List<Found> Entities(byte[] p)
        {
            var list = new List<Found>();
            int end = BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(8));
            int at = 24;
            while (at < end && at < p.Length)
            {
                int size = p[at];
                if (size == 0) break;
                int bit = (at + 1) * 8, stop = (at + 1 + size) * 8;
                var e = new Found();
                ulong U(int n)
                {
                    ulong v = Unpack(p, bit, n);
                    bit += n;
                    return v;
                }
                while (bit + 5 <= stop)
                {
                    switch ((int)U(5))
                    {
                        case EntryName:
                            int len = (int)U(4);
                            var sb = new StringBuilder();
                            for (int i = 0; i < len; i++) sb.Append((char)U(7));
                            e.Name = sb.ToString();
                            break;
                        case EntryArea: e.Zone = (int)U(10); break;
                        case 0x02: U(2); break;                     // nation
                        case 0x03: U(10); break;                    // jobs
                        case 0x04: U(16); break;                    // levels
                        case 0x05: U(4); break;                     // race
                        case 0x10: U(8); break;                     // rank
                        case 0x06: U(16); break;                    // flags1
                        case 0x08: U(20); break;                    // id
                        case 0x0D: U(8); U(16); U(32); U(64); break; // linkshell rank
                        case 0x0E: U(32); break;
                        case 0x11: U(32); break;                    // comment
                        case 0x16: U(32); break;                    // flags2
                        default: bit = stop; break;                 // language (the last one) and padding
                    }
                }
                list.Add(e);
                at += 1 + size;
            }
            return list;
        }

        private static int Pack(byte[] target, ulong value, int bitOffset, int lengthInBit)
        {
            for (int i = 0; i < lengthInBit; i++)
            {
                int pos = bitOffset + i;
                int mask = 0x80 >> (pos % 8);
                if (((value >> (lengthInBit - 1 - i)) & 1) != 0) target[pos / 8] |= (byte)mask;
                else target[pos / 8] &= (byte)~mask;
            }
            return bitOffset + lengthInBit;
        }

        private static ulong Unpack(byte[] source, int bitPos, int lengthInBit)
        {
            ulong value = 0;
            for (int i = 0; i < lengthInBit; i++)
            {
                int pos = bitPos + i;
                int bit = pos / 8 < source.Length ? (source[pos / 8] >> (7 - pos % 8)) & 1 : 0;
                value = (value << 1) | (uint)bit;
            }
            return value;
        }
    }
}
