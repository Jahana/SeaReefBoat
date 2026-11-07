// Port of src/map/map_session.h/.cpp: per-client UDP session state (packet ids, cached last datagram, blowfish keys).
// The game character (PChar) is not modelled yet; Char is an opaque slot the zone layer fills in, and InZone stands in
// for PChar->loc.zone != nullptr in parse().

using System;
using System.Collections.Generic;
using XI.Crypto;

namespace XI.Map
{
    public enum BlowfishStatus : byte
    {
        Waiting,     // BLOWFISH_WAITING
        Sent,        // BLOWFISH_SENT
        Accepted,    // BLOWFISH_ACCEPTED
        PendingZone, // BLOWFISH_PENDING_ZONE
    }

    public sealed class MapBlowfish
    {
        public readonly uint[] Key = new uint[5];
        public byte[] Hash = new byte[16];
        public Blowfish Cipher;
        public BlowfishStatus Status = BlowfishStatus.Waiting;

        public MapBlowfish Clone()
        {
            var c = new MapBlowfish { Hash = (byte[])Hash.Clone(), Cipher = Cipher, Status = Status };
            Array.Copy(Key, c.Key, 5);
            return c;
        }
    }

    public sealed class MapSession
    {
        public ulong ClientIpp;                                       // IPP raw (ip | port << 32)
        public ushort ClientPacketId;                                 // id of the last packet that came from the client
        public ushort ServerPacketId;                                 // id of the last packet sent by the server
        public byte[] ServerPacketData = new byte[MapFraming.MaxBufferSize]; // previously sent datagram
        public int ServerPacketSize;
        public DateTime LastUpdate;                                   // time of last packet recv
        public MapBlowfish Blowfish = new MapBlowfish();              // currently expected keys
        public object Char;                                           // game char (PChar)
        public bool InZone;                                           // PChar->loc.zone != nullptr
        public readonly LinkedList<BasicPacket> PacketList = new LinkedList<BasicPacket>(); // PChar's outgoing packet list
        public byte ShuttingDown;                                     // prevents double session closing
        public uint CharId;
        public uint AccountId;
        public uint NextZoneId;
        public bool ForceLinkDead;                                    // don't allow last_update tap if forced to die
        public bool HasDecryptedPacket;                               // used to check if the client still needs an 0x00A
        public DateTime AddressChanged = DateTime.MinValue;           // last time a 0x00A moved this session

        // Old keys: if the client never got 0x00B it keeps using these, and 0x00B must be resent.
        public MapBlowfish PrevBlowfish = new MapBlowfish();

        // Used to resend 0x00B
        public LogoutState ZoneType = LogoutState.None;
        public ulong ZoneIpp;
        public uint ClientServerIp;     // the address the client reached the login on (accounts_sessions.client_server_addr)

        public void IncrementBlowfish()
        {
            HasDecryptedPacket = false;
            PrevBlowfish = Blowfish.Clone();
            Blowfish = Blowfish.Clone();
            unchecked { Blowfish.Key[4] += 2; }
            InitBlowfish();
        }

        public void InitBlowfish()
        {
            var hash = new byte[16];
            Blowfish.Cipher = SessionCipher.Create(Blowfish.Key, hash);
            Blowfish.Hash = hash;
        }

        public void TapLastUpdate(DateTime now)
        {
            if (!ForceLinkDead) LastUpdate = now;
        }

        public override string ToString() => $"MapSession: client_ipp: {MapIpp.ToString(ClientIpp)}";
    }

    public static class MapIpp
    {
        public static ulong Make(uint ip, ushort port) => ip | ((ulong)port << 32);

        public static uint Ip(ulong ipp) => (uint)ipp;

        public static ushort Port(ulong ipp) => (ushort)(ipp >> 32);

        // ip is stored in network order as read off the socket (first octet in the low byte), as in IPP::toString.
        public static string ToString(ulong ipp)
        {
            uint ip = Ip(ipp);
            return $"{ip & 0xFF}.{(ip >> 8) & 0xFF}.{(ip >> 16) & 0xFF}.{ip >> 24}:{Port(ipp)}";
        }
    }
}
