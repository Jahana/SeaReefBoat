// Port of common/zmq/{router,dealer}_endpoint + ipc::Channel: the real ZeroMQ transport behind IWorldChannel.
//
//   Router (world): binds; each inbound multipart is [8-byte routing id][payload]; the routing id is the sender's
//   IPP raw value (uint64 little-endian = ip | port<<32). Outbound frames are [IPP raw][payload].
//   Dealer (map):   connects with its IPP raw as the socket routing id; frames are the bare payload.
//
// A single I/O thread owns each socket (NetMQ sockets are not thread-safe): it drains the outbound queue, then polls
// for inbound frames, mirroring flushOutbound()/onReadable().

using System;
using System.Collections.Concurrent;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;

namespace XI.World
{
    internal static class IppBytes
    {
        public static byte[] ToRoutingId(Ipp ipp)
        {
            var b = new byte[8];
            BitConverter.TryWriteBytes(b, ipp.Raw);
            return b;
        }

        public static Ipp FromRoutingId(byte[] id) => Ipp.FromRaw(BitConverter.ToUInt64(id, 0));
    }

    public sealed class ZmqRouterChannel : IWorldChannel, IDisposable
    {
        private readonly RouterSocket _socket;
        private readonly ConcurrentQueue<IppMessage> _incoming = new();
        private readonly ConcurrentQueue<IppMessage> _outgoing = new();
        private readonly Thread _thread;
        private volatile bool _stop;

        public bool Opened { get; }

        public ZmqRouterChannel(string endpoint, Action<string> log = null)
        {
            _socket = new RouterSocket();
            try
            {
                _socket.Bind(endpoint);
                Opened = true;
            }
            catch (Exception e) when (e is NetMQException || e is ArgumentException)
            {
                (log ?? (_ => { }))($"IPC router could not bind '{endpoint}': {e.Message}. Is another xi_world already bound to this endpoint? IPC will not function in this process.");
                Opened = false;
            }
            _thread = new Thread(Loop) { IsBackground = true, Name = "zmq-router" };
            if (Opened) _thread.Start();
        }

        public void Send(IppMessage message) => _outgoing.Enqueue(message);
        public bool TryReceive(out IppMessage message) => _incoming.TryDequeue(out message);

        private void Loop()
        {
            while (!_stop)
            {
                while (_outgoing.TryDequeue(out var o))
                {
                    var m = new NetMQMessage(2);
                    m.Append(IppBytes.ToRoutingId(o.Ipp));
                    m.Append(o.Payload);
                    _socket.TrySendMultipartMessage(TimeSpan.Zero, m); // dontwait: unroutable peers are dropped
                }

                NetMQMessage inbound = null;
                if (_socket.TryReceiveMultipartMessage(TimeSpan.FromMilliseconds(5), ref inbound, 2) && inbound != null && inbound.FrameCount == 2
                    && inbound[0].MessageSize >= 8)
                {
                    _incoming.Enqueue(new IppMessage(IppBytes.FromRoutingId(inbound[0].ToByteArray()), inbound[1].ToByteArray()));
                }
            }
        }

        public void Dispose()
        {
            _stop = true;
            if (Opened) _thread.Join(1000);
            _socket.Dispose();
        }
    }

    // Map-side counterpart (ipc_client's channel): payloads only, identity = own IPP.
    public sealed class ZmqDealerChannel : IDisposable
    {
        private readonly DealerSocket _socket;
        private readonly ConcurrentQueue<byte[]> _incoming = new();
        private readonly ConcurrentQueue<byte[]> _outgoing = new();
        private readonly Thread _thread;
        private volatile bool _stop;

        public ZmqDealerChannel(string endpoint, Ipp self)
        {
            _socket = new DealerSocket();
            _socket.Options.Identity = IppBytes.ToRoutingId(self);
            _socket.Connect(endpoint);
            _thread = new Thread(Loop) { IsBackground = true, Name = "zmq-dealer" };
            _thread.Start();
        }

        public void Send(byte[] payload) => _outgoing.Enqueue(payload);
        public bool TryReceive(out byte[] payload) => _incoming.TryDequeue(out payload);

        private void Loop()
        {
            while (!_stop)
            {
                while (_outgoing.TryDequeue(out var o)) _socket.TrySendFrame(TimeSpan.Zero, o);

                if (_socket.TryReceiveFrameBytes(TimeSpan.FromMilliseconds(5), out var bytes)) _incoming.Enqueue(bytes);
            }
        }

        public void Dispose()
        {
            _stop = true;
            _thread.Join(1000);
            _socket.Dispose();
        }
    }
}
