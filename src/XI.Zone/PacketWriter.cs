using System;
using System.Buffers.Binary;
using System.Text;
using XI.Map;

namespace XI.Zone
{
    // Little-endian writes into a BasicPacket at packet-relative offsets (the body starts at +4, after the id/size/sync
    // header). The builders name each field's offset as the layout oracle or the reference capture pins it.
    public readonly struct PacketWriter
    {
        public readonly BasicPacket Packet;
        private readonly byte[] b_;

        public PacketWriter(ushort id, uint size)
        {
            Packet = new BasicPacket();
            Packet.SetOpcode(id);
            Packet.SetSize(size);
            b_ = Packet.Buffer;
        }

        // writes into a packet that already holds data (an update merged into one still waiting to be sent)
        public PacketWriter(BasicPacket packet)
        {
            Packet = packet;
            b_ = Packet.Buffer;
        }

        public void U8(int off, int v) => b_[off] = (byte)v;
        public void U16(int off, int v) => BinaryPrimitives.WriteUInt16LittleEndian(b_.AsSpan(off), (ushort)v);
        public void U32(int off, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(b_.AsSpan(off), v);
        public void U64(int off, ulong v) => BinaryPrimitives.WriteUInt64LittleEndian(b_.AsSpan(off), v);
        public void I32(int off, int v) => BinaryPrimitives.WriteInt32LittleEndian(b_.AsSpan(off), v);
        public void F32(int off, float v) => BinaryPrimitives.WriteSingleLittleEndian(b_.AsSpan(off), v);
        public void Bytes(int off, ReadOnlySpan<byte> v) => v.CopyTo(b_.AsSpan(off));
        public void Fill(int off, int len, byte v) => b_.AsSpan(off, len).Fill(v);

        // memcpy(name, str, min(len, max)) -- no terminator is written past what fits
        public void Name(int off, string name, int max)
        {
            var bytes = Encoding.ASCII.GetBytes(name ?? "");
            Bytes(off, bytes.AsSpan(0, Math.Min(bytes.Length, max)));
        }

        public void SetSize(uint size) => Packet.SetSize(size);
    }
}
