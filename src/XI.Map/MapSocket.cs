// Port of src/map/map_socket.cpp: a UDP socket whose network thread fills an ingress queue that the main (tick) thread
// drains, mirroring the asio net thread + SPSC rings. Datagrams are clamped to kMaxBufferSize; the IPP keeps the
// address in network order (htonl(to_uint())), port in host order.

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace XI.Map
{
    public sealed class MapSocket : IDisposable
    {
        private const int IngressCapacity = 4096; // kIngressRingCapacity
        private const int SocketBufferBytes = 4 * 1024 * 1024;
        private const int SioUdpConnReset = -1744830452; // _WSAIOW(IOC_VENDOR, 12)

        private readonly Socket _socket;
        private readonly ConcurrentQueue<(ulong Ipp, byte[] Data)> _ingress = new ConcurrentQueue<(ulong, byte[])>();
        // scheduleMainDrain: a datagram wakes the main thread at once (postToMainThread) rather than waiting for its next
        // poll, which on Windows (a 15.6 ms timer tick behind every short sleep) held each datagram up ~8 ms on average
        private readonly AutoResetEvent _drainScheduled = new AutoResetEvent(false);
        private readonly Thread _thread;
        private readonly Action<string> _log;
        private volatile bool _stop;
        private long _ingressDropped;

        public ushort Port { get; }

        public long IngressDropped => Interlocked.Read(ref _ingressDropped);

        public MapSocket(ushort port, Action<string> log = null, IPAddress bind = null)
        {
            _log = log ?? (_ => { });
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.ReceiveBufferSize = SocketBufferBytes;
            _socket.SendBufferSize = SocketBufferBytes;
            // an ICMP port-unreachable from a gone client must not error the next ReceiveFrom
            if (OperatingSystem.IsWindows()) _socket.IOControl(SioUdpConnReset, new byte[4], null);
            _socket.Bind(new IPEndPoint(bind ?? IPAddress.Any, port));
            Port = (ushort)((IPEndPoint)_socket.LocalEndPoint).Port;
            _log($"MapSocket: Starting on port {Port}");

            _thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "MapSocket" };
            _thread.Start();
        }

        public static ulong ToIpp(IPEndPoint ep)
        {
            byte[] b = ep.Address.GetAddressBytes();
            return MapIpp.Make((uint)(b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24)), (ushort)ep.Port);
        }

        public static IPEndPoint ToEndPoint(ulong ipp)
        {
            uint ip = MapIpp.Ip(ipp);
            return new IPEndPoint(new IPAddress(new[] { (byte)ip, (byte)(ip >> 8), (byte)(ip >> 16), (byte)(ip >> 24) }), MapIpp.Port(ipp));
        }

        private void ReceiveLoop()
        {
            var buf = new byte[MapFraming.MaxBufferSize];
            EndPoint from = new IPEndPoint(IPAddress.Any, 0);
            while (!_stop)
            {
                int n;
                try { n = _socket.ReceiveFrom(buf, ref from); }
                catch (SocketException e) when (!_stop)
                {
                    _log($"MapSocket: receive error {e.SocketErrorCode}");
                    continue;
                }
                catch (Exception) { break; }

                if (_ingress.Count >= IngressCapacity)
                {
                    Interlocked.Increment(ref _ingressDropped);
                    continue;
                }
                var data = new byte[n];
                Array.Copy(buf, data, n);
                _ingress.Enqueue((ToIpp((IPEndPoint)from), data));
                _drainScheduled.Set();
            }
        }

        // drainIngress: run on the tick thread.
        public int Drain(Action<byte[], ulong> onReceive)
        {
            int count = 0;
            while (_ingress.TryDequeue(out var d))
            {
                onReceive(d.Data, d.Ipp);
                count++;
            }
            return count;
        }

        // the main thread's idle wait: back as soon as a datagram is queued, else after timeoutMs
        public bool WaitForIngress(int timeoutMs) => !_ingress.IsEmpty || _drainScheduled.WaitOne(timeoutMs);

        public void Send(ulong ipp, ReadOnlySpan<byte> datagram)
        {
            try { _socket.SendTo(datagram, SocketFlags.None, ToEndPoint(ipp)); }
            catch (SocketException e) { _log($"MapSocket: send to {MapIpp.ToString(ipp)} failed: {e.SocketErrorCode}"); }
        }

        public void Dispose()
        {
            _stop = true;
            _socket.Dispose();
            _thread.Join(1000);
            _drainScheduled.Dispose();
        }
    }
}
