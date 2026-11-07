using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using XI.Crypto;
using XI.Map;

namespace XI.Bots
{
    // The retail client's map session (the server half is src/map/map_networking.cpp, ported in XI.Map.MapNetwork):
    //   - a plaintext 0x00A (md5 + LoginPacketCheck) opens it; every datagram after it is the ffxi zlib stream under
    //     blowfish (the session key from the lobby, md5'd as in initBlowfish), both ways (MapFraming);
    //   - lockstep: header [0..2) is the sender's datagram id and [2..4) the last id it got from the other side. The
    //     server answers every datagram with one of its own, and resends its previous one when the ack is stale, so
    //     the client sends the next datagram only after the answer to the last one (or a timeout);
    //   - sub-packets carry the datagram id as sync and are repeated until the server acks a datagram at least that
    //     new (the server skips any it has seen: sync <= its client packet id);
    //   - 0x00B moves the session: key[4] += 2, a 0x00D under the old key, then a new 0x00A to the address it names.
    public sealed class MapClient : IDisposable
    {
        private readonly UdpClient _udp;
        private IPEndPoint _server;
        private uint[] _key;
        private Blowfish _cipher;
        private ushort _seq;            // our datagram id
        private ushort _ack;            // the server's last datagram id
        private bool _haveServerId;
        private readonly List<(ushort Sync, byte[] Bytes)> _outbox = new List<(ushort, byte[])>();
        private readonly byte[] _recv = new byte[MapFraming.MaxBufferSize];

        public Action<byte[]> OnPacket;     // every new server sub-packet, whole (header included)
        public Action<double> OnRoundTrip;  // ms from a datagram to the server's answer
        public TimeSpan ReplyTimeout = TimeSpan.FromMilliseconds(1000);
        public int Timeouts, Datagrams, BadDatagrams;

        public IPEndPoint Server => _server;
        public IPEndPoint Local => (IPEndPoint)_udp.Client.LocalEndPoint;
        public uint[] Key => _key;

        public MapClient()
        {
            _udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            // an ICMP port unreachable (a map process gone) must not fault the next receive
            if (OperatingSystem.IsWindows()) _udp.Client.IOControl(-1744830452, new byte[4], null);
        }

        private static readonly byte[] LoginTemplate = Convert.FromHexString(
            // the retail client's GP_CLI_COMMAND_LOGIN (reference/zonein_ryoo.txt), the char id and check filled in
            "0A2E0100BA0000000000000001000000000000000000000000000000000000000000200000000000000000000000" +
            "0000000000000000000000000000000000006EE18941E234C92D984021C0ED914EFE0000000057494E0001000101");

        // the plaintext 0x00A until an answer deciphers under the key (the pending session may still be on its way
        // from the world), then the zone-in packets the answer carried
        public async Task<bool> Login(uint charId, uint[] key, IPEndPoint server, TimeSpan within, CancellationToken ct)
        {
            _server = server;
            _key = (uint[])key.Clone();
            _cipher = SessionCipher.Create(_key);
            _outbox.Clear();
            _haveServerId = false;
            var until = DateTime.UtcNow + within;
            for (ushort retry = 0; DateTime.UtcNow < until; retry++)
            {
                _seq = 1;
                _ack = 0;
                var body = (byte[])LoginTemplate.Clone();
                BitConverter.TryWriteBytes(new Span<byte>(body, MapFraming.LoginRetryOffset, 2), retry);
                BitConverter.TryWriteBytes(new Span<byte>(body, MapFraming.LoginCharIdOffset, 4), charId);
                MapFraming.SignLogin(body);
                var d = MapFraming.BuildPlainDatagram(1, 0, body);
                var sw = Stopwatch.StartNew();
                await _udp.SendAsync(d, d.Length, _server);
                if (await Receive(1, ct))
                {
                    OnRoundTrip?.Invoke(sw.Elapsed.TotalMilliseconds);
                    return true;
                }
            }
            return false;
        }

        // queue sub-packets (type, size, body filler) for the next datagram
        public void Queue(ushort type, int size, Action<byte[]> fill = null)
        {
            var b = new byte[size];
            C2sPos.WriteHeader(b, type, (ushort)(size / 4), 0);
            fill?.Invoke(b);
            _outbox.Add((0, b));
        }

        // one lockstep exchange: everything not yet acked, then the server's answer. False on a timeout.
        public async Task<bool> Exchange(CancellationToken ct)
        {
            _seq++;
            var stream = new List<byte>();
            for (int i = 0; i < _outbox.Count; i++)
            {
                var (sync, bytes) = _outbox[i];
                if (sync == 0)
                {
                    sync = _seq;
                    BitConverter.TryWriteBytes(new Span<byte>(bytes, 2, 2), sync);
                    _outbox[i] = (sync, bytes);
                }
                if (stream.Count + bytes.Length > 1000) break; // the rest the next time
                stream.AddRange(bytes);
            }
            if (stream.Count == 0)
            {
                // a datagram needs at least one sub-packet: a 0x00F (CLSTAT, no reply) keeps the session alive
                var keep = new byte[36];
                C2sPos.WriteHeader(keep, 0x00F, 36 / 4, _seq);
                stream.AddRange(keep);
            }
            var header = new byte[MapFraming.HeaderSize];
            MapFraming.PrepareHeader(header, _seq, _ack, (uint)Environment.TickCount);
            var compressed = MapFraming.Compress(stream.ToArray());
            if (compressed == null) throw new InvalidOperationException("map: zlib compress failed");
            var d = MapFraming.Finalize(header, compressed, _cipher);
            var sw = Stopwatch.StartNew();
            await _udp.SendAsync(d, d.Length, _server);
            Datagrams++;
            if (!await Receive(_seq, ct))
            {
                Timeouts++;
                return false;
            }
            OnRoundTrip?.Invoke(sw.Elapsed.TotalMilliseconds);
            return true;
        }

        // the zone change (0x00B LGS_ZONECHANGE): the 0x00D under the old key, then the next login under key[4] + 2
        public async Task SendNetEnd()
        {
            _seq++;
            var b = new byte[8];
            C2sPos.WriteHeader(b, 0x00D, 2, _seq);
            var header = new byte[MapFraming.HeaderSize];
            MapFraming.PrepareHeader(header, _seq, _ack, (uint)Environment.TickCount);
            var d = MapFraming.Finalize(header, MapFraming.Compress(b), _cipher);
            await _udp.SendAsync(d, d.Length, _server);
        }

        // waits for a datagram that acks `seq`; older ones (answers to a datagram that timed out) are still read
        private async Task<bool> Receive(ushort seq, CancellationToken ct)
        {
            var until = DateTime.UtcNow + ReplyTimeout;
            while (true)
            {
                var left = until - DateTime.UtcNow;
                if (left <= TimeSpan.Zero) return false;
                UdpReceiveResult r;
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    cts.CancelAfter(left);
                    try { r = await _udp.ReceiveAsync(cts.Token); }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return false; }
                    catch (SocketException) { continue; }
                }
                if (!r.RemoteEndPoint.Equals(_server)) continue; // a late answer from the previous map process
                if (!Read(r.Buffer, out ushort acked)) { BadDatagrams++; continue; }
                if (acked == seq) return true;
            }
        }

        private bool Read(byte[] d, out ushort acked)
        {
            acked = 0;
            if (d.Length > _recv.Length || d.Length <= MapFraming.HeaderSize + MapFraming.HashSize) return false;
            Array.Copy(d, _recv, d.Length);
            if (!MapFraming.Decipher(_recv, d.Length, _cipher)) return false;
            int n = MapFraming.Decompress(_recv, d.Length);
            if (n < 0) return false;
            ushort id = BitConverter.ToUInt16(_recv, 0);
            ushort ack = acked = BitConverter.ToUInt16(_recv, 2);
            // what the server has seen goes out of the outbox
            _outbox.RemoveAll(o => o.Sync != 0 && o.Sync <= ack);
            // a resend of the datagram already read carries nothing new
            if (_haveServerId && id == _ack) return true;
            _haveServerId = true;
            _ack = id;
            foreach (var sp in MapFraming.SubPackets(_recv, n))
            {
                var p = new byte[sp.Size];
                Array.Copy(_recv, sp.Offset, p, 0, sp.Size);
                OnPacket?.Invoke(p);
            }
            return true;
        }

        public static ushort Opcode(byte[] p) => (ushort)(p[0] | (p[1] & 1) << 8);

        public void Dispose() => _udp.Dispose();
    }
}
