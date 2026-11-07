// Port of common/ipp.h: an IP-port pair. IP is the network-order uint32 (as str2ip yields), port is host order.
// Raw = ip | port << 32, the 8 bytes the ZMQ router uses as the peer identity.

using System;
using XI.Login;

namespace XI.World
{
    public readonly struct Ipp : IEquatable<Ipp>, IComparable<Ipp>
    {
        public uint Ip { get; }
        public ushort Port { get; }

        public Ipp(uint ip, ushort port) { Ip = ip; Port = port; }

        public ulong Raw => Ip | ((ulong)Port << 32);

        // IPP(const uint64&)
        public static Ipp FromRaw(ulong raw) => new Ipp((uint)raw, (ushort)(raw >> 32));

        public string IpString => IpText.Ip2Str(Ip);

        public override string ToString() => $"{IpString}:{Port}";

        // operator<: ip first, then port
        public int CompareTo(Ipp other) => Ip != other.Ip ? Ip.CompareTo(other.Ip) : Port.CompareTo(other.Port);

        public bool Equals(Ipp other) => Ip == other.Ip && Port == other.Port;
        public override bool Equals(object obj) => obj is Ipp o && Equals(o);
        public override int GetHashCode() => Raw.GetHashCode();
    }

    // IPPMessage: one router frame, the peer plus the [type byte][CBOR] payload.
    public readonly struct IppMessage
    {
        public Ipp Ipp { get; }
        public byte[] Payload { get; }
        public IppMessage(Ipp ipp, byte[] payload) { Ipp = ipp; Payload = payload; }
    }

    // ipc::Channel<IPPMessage> as seen by IPCServer: queued send, non-blocking receive.
    public interface IWorldChannel
    {
        void Send(IppMessage message);
        bool TryReceive(out IppMessage message);
    }
}
