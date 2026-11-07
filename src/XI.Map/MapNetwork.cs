// Port of the session-aware half of src/map/map_networking.cpp: handle_incoming_packet, recv_parse, parse, send_parse
// (+ preparePacket / compressPacket / finalizePacket via MapFraming). The UDP socket is MapSocket.
//
// Not ported here (entity layer): charutils::LoadChar / EnsureZoneLoaded / the KillSession + CharZone IPC sends and
// PChar teardown after 0x00B. Those are the OnLogin / OnZoneOut hooks (ZoneHost.OnLogin sends KillSession,
// ZoneHost.OnZoneOut CharZone); packet handlers are the Dispatch hook.

using System;
using System.Collections.Generic;

namespace XI.Map
{
    // Whole GP_SERV_COMMAND_LOGOUT packets (header + S2cLogout body at +4), as send_parse handles them.
    public static class LogoutPacket
    {
        public const ushort Id = 0x00B;
        private const int Body = 4; // sizeof(GP_SERV_HEADER)

        public static BasicPacket Create(LogoutState state, ulong zoneIpp)
        {
            var p = new BasicPacket();
            p.SetOpcode(Id);
            p.SetSize(Body + S2cLogout.SizeOf);
            var body = new Span<byte>(p.Buffer, Body, S2cLogout.SizeOf);
            S2cLogout.SetLogoutState(body, state);
            S2cLogout.SetIp(body, MapIpp.Ip(zoneIpp));
            S2cLogout.SetPort(body, MapIpp.Port(zoneIpp));
            S2cLogout.SetCliErrCode(body, GameEcode.Noerr);
            return p;
        }

        public static LogoutState State(BasicPacket p) => S2cLogout.GetLogoutState(new ReadOnlySpan<byte>(p.Buffer, Body, S2cLogout.SizeOf));

        public static ulong ZoneIpp(BasicPacket p)
        {
            var body = new ReadOnlySpan<byte>(p.Buffer, Body, S2cLogout.SizeOf);
            return MapIpp.Make(S2cLogout.GetIp(body), (ushort)S2cLogout.GetPort(body));
        }

        // network.ZONE_IP_FROM_CLIENT: rewrite the ip of a 0x00B's wire copy (packet = the 0x00B's bytes) with
        // ClientAddress.ClientFacingIp; the port stays
        public static void ApplyClientAddress(Span<byte> packet, uint clientServerIp, bool enabled)
        {
            if (!enabled) return;
            var body = packet.Slice(Body, S2cLogout.SizeOf);
            S2cLogout.SetIp(body, XI.Common.ClientAddress.ClientFacingIp(S2cLogout.GetIp(body), clientServerIp, enabled));
        }
    }

    // A char load whose reads run ahead of it (MapNetwork.PrefetchLogin): Ready once they are in, Serve answers the
    // load's reads from them while the datagram is handled, Dispose lets them go.
    public interface IDeferredLogin : IDisposable
    {
        bool Ready { get; }
        IDisposable Serve();
    }

    public sealed class MapNetwork
    {
        private readonly MapSessionContainer _sessions;
        private readonly IMapSessionStore _store;
        private readonly Action<string> _log;
        private readonly byte[] _buff = new byte[MapFraming.MaxBufferSize];     // PBuff
        private readonly byte[] _buffCopy = new byte[MapFraming.MaxBufferSize]; // PBuffCopy

        public MapNetwork(MapSessionContainer sessions, IMapSessionStore store, Action<string> log = null)
        {
            _sessions = sessions;
            _store = store;
            _log = log ?? (_ => { });
        }

        public MapSessionContainer Sessions => _sessions;

        // u32 unix time written by preparePacket (earth_time::timestamp, the time offset in it).
        public Func<uint> Timestamp = () => (uint)XI.Common.EarthTime.UnixSec();

        // network.ZONE_IP_FROM_CLIENT (C# only): 0x00B carries the client's own server address for a 127.x zone
        public bool ZoneIpFromClient;

        // The engine's packetSystem_.dispatch: (session, type, 0x1FF-byte packet copy).
        public Action<MapSession, ushort, BasicPacket> Dispatch;

        // after a datagram's packets were dispatched (flushEquipChanges at the end of the network cycle)
        public Action<MapSession> AfterParse;

        // recv_parse after a session's char was (re)loaded from a plaintext 0x00A, the session's status still what it
        // was (Waiting for a new one). Default: a placeholder char so the rest of the pipeline runs (C++
        // charutils::LoadChar, then KillSession for a char new to this process whose zone changed).
        public Action<MapSession> OnLogin = s => s.Char ??= new object();

        // send_parse after 0x00B went out and the key was incremented (C++ sends CharZone IPC and drops PChar).
        public Action<MapSession> OnZoneOut;

        // recv_parse's char load (charutils::LoadChar's sixty-odd queries), read ahead: a plaintext 0x00A that would load
        // a char is held, with every datagram from its address (or for its char) after it, while the reads PrefetchLogin
        // starts run elsewhere; PumpDeferred then handles the held datagrams in the order they came, each exactly as
        // HandleIncoming would have, so only the time it happens moves. null (the C++ way): the char loads in the tick.
        public Func<uint, IDeferredLogin> PrefetchLogin;

        private sealed class Deferred
        {
            public byte[] Data;
            public ulong Ipp;
            public uint CharId;          // a held 0x00A's char (0: a datagram held behind one)
            public IDeferredLogin Reads; // a held 0x00A's reads
        }

        private readonly LinkedList<Deferred> deferred_ = new LinkedList<Deferred>();
        private readonly Dictionary<ulong, int> deferredIpps_ = new Dictionary<ulong, int>();
        private readonly Dictionary<uint, int> deferredChars_ = new Dictionary<uint, int>();
        private readonly byte[] _scratch = new byte[MapFraming.MaxBufferSize];

        // the datagrams held for a char load
        public int DeferredCount => deferred_.Count;

        // handle_incoming_packet. Returns the datagram to send back to ipp (null: nothing, or held for PumpDeferred).
        public byte[] HandleIncoming(ReadOnlySpan<byte> datagram, ulong ipp)
        {
            if (PrefetchLogin != null && Defer(datagram, ipp)) return null;
            return Handle(datagram, ipp);
        }

        // the held datagrams, oldest first, while the one in front has its reads in and the budget (ms, measured from the
        // first; at least one is handled) lasts; each reply goes to send
        public void PumpDeferred(Action<ulong, byte[]> send, double budgetMs = double.PositiveInfinity)
        {
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            bool first = true;
            while (deferred_.First is { } node)
            {
                var d = node.Value;
                if (d.Reads != null && !d.Reads.Ready) break;
                if (!first && System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds >= budgetMs) break;
                first = false;
                deferred_.RemoveFirst();
                Release(deferredIpps_, d.Ipp);
                if (d.CharId != 0) Release(deferredChars_, d.CharId);
                byte[] reply;
                try
                {
                    using (d.Reads?.Serve()) reply = Handle(d.Data, d.Ipp);
                }
                finally
                {
                    d.Reads?.Dispose();
                }
                if (reply != null) send(d.Ipp, reply);
            }
        }

        // the held datagrams dropped unhandled (shutdown)
        public void DropDeferred()
        {
            foreach (var d in deferred_) d.Reads?.Dispose();
            deferred_.Clear();
            deferredIpps_.Clear();
            deferredChars_.Clear();
        }

        private static void Release<T>(Dictionary<T, int> counts, T key)
        {
            if (counts.TryGetValue(key, out int n) && n > 1) counts[key] = n - 1;
            else counts.Remove(key);
        }

        // held: behind another held datagram of the address or the char, or a 0x00A whose char load gets its reads ahead
        private bool Defer(ReadOnlySpan<byte> datagram, ulong ipp)
        {
            var session = _sessions.GetSessionByIpp(ipp);
            uint charId = 0;
            IDeferredLogin reads = null;
            bool behind = deferredIpps_.ContainsKey(ipp) || (session != null && session.CharId != 0 && deferredChars_.ContainsKey(session.CharId));
            if (!behind)
            {
                if (!WouldLoadChar(datagram, session, out charId)) return false;
                reads = PrefetchLogin(charId);
                if (reads == null) return false;
            }
            deferred_.AddLast(new Deferred { Data = datagram.ToArray(), Ipp = ipp, CharId = charId, Reads = reads });
            deferredIpps_[ipp] = deferredIpps_.TryGetValue(ipp, out int n) ? n + 1 : 1;
            if (charId != 0) deferredChars_[charId] = deferredChars_.TryGetValue(charId, out int m) ? m + 1 : 1;
            return true;
        }

        // RecvParse's plaintext branch, read only: a signed 0x00A for a session (or pending session) it would accept
        // whose char is to be loaded. Only how soon the datagram is handled depends on it: one that is held needlessly is
        // handled the same way later, one that is not loads its char in the tick.
        private bool WouldLoadChar(ReadOnlySpan<byte> datagram, MapSession session, out uint charId)
        {
            charId = 0;
            // the session at the address has its char (most datagrams): nothing to load, and no md5 to take
            if (session != null && session.Char != null && session.Blowfish.Status != BlowfishStatus.PendingZone) return false;
            int size = Math.Min(datagram.Length, _scratch.Length);
            datagram.Slice(0, size).CopyTo(_scratch);
            if (size <= MapFraming.HeaderSize + MapFraming.HashSize || !MapFraming.TryReadPlainLogin(_scratch, size, out var login)) return false;
            charId = login.CharId;
            session ??= _sessions.GetSessionByCharId(charId) ?? _sessions.GetPendingSessionByCharId(charId);
            if (session == null) return false;
            if (session.CharId != 0 && session.CharId != charId) return false;
            if (session.Blowfish.Status == BlowfishStatus.Accepted && session.HasDecryptedPacket) return false;
            return session.Blowfish.Status == BlowfishStatus.PendingZone || session.Char == null;
        }

        private byte[] Handle(ReadOnlySpan<byte> datagram, ulong ipp)
        {
            MapSession session = _sessions.GetSessionByIpp(ipp);

            int len = Math.Min(datagram.Length, _buff.Length);
            datagram.Slice(0, len).CopyTo(_buff);
            int size = len;

            int decryptCount = RecvParse(_buff, ref size, ref session, ipp);
            if (session == null) return null;

            byte[] reply = null;
            if (decryptCount != -1)
            {
                byte[] outBuf = _buff;
                if (decryptCount == 0 && session.Char != null)
                {
                    // If the previous packet was lost, resend it instead of building a new one
                    if (Parse(_buff, ref size, session) == 0)
                        size = SendParse(_buff, session, usePreviousKey: false);
                }
                else if (decryptCount == 1 && session.Blowfish.Status == BlowfishStatus.PendingZone)
                {
                    // client already got 0x00B
                    if (HasZoneOutReply(_buff, size)) return null;

                    // Client failed to receive 0x00B: rebuild it and send it with the previous key
                    PreparePacket(_buff, session);
                    var zonePacket = LogoutPacket.Create(session.ZoneType, session.ZoneIpp);
                    zonePacket.SetSequence(session.ServerPacketId);
                    int zsz = (int)zonePacket.GetSize();
                    LogoutPacket.ApplyClientAddress(new Span<byte>(zonePacket.Buffer, 0, zsz), session.ClientServerIp, ZoneIpFromClient);
                    Array.Copy(zonePacket.Buffer, 0, _buff, MapFraming.HeaderSize, zsz);
                    size = MapFraming.HeaderSize + zsz;

                    byte[] compressed = MapFraming.Compress(new ReadOnlySpan<byte>(_buff, MapFraming.HeaderSize, size - MapFraming.HeaderSize));
                    if (compressed == null)
                    {
                        _log($"zlib compression error from charid '{session.CharId}', attempted pre-compression packet size '{size}'");
                        size = 0;
                    }
                    else
                    {
                        size = Finalize(_buff, compressed, session, usePreviousKey: true);
                    }
                    session.ServerPacketId += 1;
                }

                if (size > 0)
                {
                    reply = new byte[size];
                    Array.Copy(outBuf, reply, size);
                }

                // std::swap(PBuff, server_packet_data): the sent datagram becomes the cached one
                Array.Copy(outBuf, session.ServerPacketData, Math.Max(size, 0));
                session.ServerPacketSize = Math.Max(size, 0);
            }

            // If client is logging out, just close it.
            if (session.ShuttingDown == 1) _sessions.DestroySession(session);

            return reply;
        }

        // 0x00D is the client's reply to 0x00B, sent with the old key
        public static bool HasZoneOutReply(byte[] buff, int size)
        {
            foreach (var p in MapFraming.SubPackets(buff, size))
                if (p.Type == 0x00D) return true;
            return false;
        }

        // Returns -1 (drop), 0 (current key), 1 (previous key while pending zone). size is updated to the decompressed size.
        public int RecvParse(byte[] buff, ref int size, ref MapSession session, ulong ipp)
        {
            if (size <= MapFraming.HeaderSize + MapFraming.HashSize) return -1;

            if (MapFraming.ChecksumMatches(buff, size))
            {
                if (!MapFraming.TryReadPlainLogin(buff, size, out var login)) return -1;
                uint charId = login.CharId;

                // client may zone in from a new address, find the session by char instead
                if (session == null)
                {
                    var existing = _sessions.GetSessionByCharId(charId);
                    if (existing != null)
                    {
                        if (existing.Blowfish.Status == BlowfishStatus.Accepted && existing.HasDecryptedPacket)
                        {
                            _log($"recv_parse: 0x00A for charid {charId} from {MapIpp.ToString(ipp)} ignored, session is bound to {MapIpp.ToString(existing.ClientIpp)} (retry {login.Retry})");
                            return -1;
                        }
                        if (_sessions.Now < existing.AddressChanged.AddSeconds(1))
                        {
                            _log($"recv_parse: 0x00A for charid {charId} from {MapIpp.ToString(ipp)} ignored, session moved to {MapIpp.ToString(existing.ClientIpp)} less than a second ago (retry {login.Retry})");
                            return -1;
                        }
                        _log($"recv_parse: session for charid {charId} moved from {MapIpp.ToString(existing.ClientIpp)} to {MapIpp.ToString(ipp)} (retry {login.Retry})");
                        _sessions.MoveSession(existing, ipp);
                        existing.AddressChanged = _sessions.Now;
                        session = existing;
                    }
                    else
                    {
                        var pending = _sessions.GetPendingSessionByCharId(charId);
                        if (pending == null)
                        {
                            _log($"recv_parse: 0x00A for charid {charId} from {MapIpp.ToString(ipp)} ignored, no session or pending session (retry {login.Retry})");
                            return -1;
                        }
                        session = _sessions.CreateSession(ipp);
                        _sessions.DestroyPendingSession(pending);
                    }
                }

                if (session.CharId != 0 && session.CharId != charId) return -1;
                if (session.Blowfish.Status == BlowfishStatus.Accepted && session.HasDecryptedPacket) return -1;

                // If we were pending zone, delete our old char
                if (session.Blowfish.Status == BlowfishStatus.PendingZone)
                {
                    session.Char = null;
                    session.InZone = false;
                }

                if (session.Char == null)
                {
                    if (!_store.TryLoadLogin(charId, out uint accountId, out uint[] key5))
                    {
                        _log($"recv_parse: Cannot load char {charId} (no such charid)");
                        return -1;
                    }
                    if (key5 == null)
                    {
                        _log($"recv_parse: Cannot load session_key for charid {charId}");
                        return -1;
                    }
                    Array.Copy(key5, session.Blowfish.Key, 5);
                    session.InitBlowfish();
                    session.CharId = charId;
                    session.AccountId = accountId;
                    OnLogin?.Invoke(session);
                }

                if (ZoneIpFromClient) session.ClientServerIp = _store.GetClientServerAddr(charId);
                session.ClientPacketId = 0;
                session.ServerPacketId = 0;
                session.ZoneIpp = 0;
                session.ZoneType = LogoutState.None;
                return 0;
            }

            if (session == null) return -1;

            if (session.Blowfish.Status == BlowfishStatus.PendingZone)
                Array.Copy(buff, _buffCopy, size); // blowfish can't be rewound

            int decryptCount = 0;
            if (!DecipherLogged(buff, size, session, session.Blowfish))
            {
                // pending zone: the client may not have received 0x00B and still uses the old key
                if (session.Blowfish.Status == BlowfishStatus.PendingZone && session.PrevBlowfish.Cipher != null &&
                    DecipherLogged(_buffCopy, size, session, session.PrevBlowfish))
                {
                    Array.Copy(_buffCopy, buff, size);
                    decryptCount++;
                }
                else
                {
                    size = 0;
                    return -1;
                }
            }

            // a real decrypt with the current key: no more 0x00A accepted
            if (decryptCount == 0) session.HasDecryptedPacket = true;

            int n = MapFraming.Decompress(buff, size);
            if (n != -1) size = n;
            return decryptCount;
        }

        private bool DecipherLogged(byte[] buff, int size, MapSession session, MapBlowfish key)
        {
            if (key.Cipher != null && MapFraming.Decipher(buff, size, key.Cipher)) return true;
            if (session.Blowfish.Status != BlowfishStatus.PendingZone)
                _log($"map_decipher_packet: bad packet from <{MapIpp.ToString(session.ClientIpp).Split(':')[0]}> (charid {session.CharId})");
            return false;
        }

        // Returns 0 (build a new datagram) or -1 (buff now holds the cached previous datagram; size updated).
        public int Parse(byte[] buff, ref int size, MapSession session)
        {
            ushort code = BitConverter.ToUInt16(buff, 0);

            if (session.Blowfish.Status != BlowfishStatus.PendingZone && session.Blowfish.Status != BlowfishStatus.Waiting)
                session.TapLastUpdate(_sessions.Now);

            ushort lastType = 0;
            foreach (var sp in MapFraming.SubPackets(buff, size))
            {
                lastType = sp.Type;
                if (sp.Sync <= session.ClientPacketId || sp.Sync > code) continue;

                if (!session.InZone && sp.Type != 0x00A)
                {
                    if (session.Blowfish.Status != BlowfishStatus.PendingZone)
                        _log($"This packet is unexpected from charid {session.CharId} - Received {sp.Type:X3} earlier without matching 0x0A");
                    continue;
                }

                var packet = new BasicPacket(new ReadOnlySpan<byte>(buff, sp.Offset, Math.Min(MapFraming.PacketSize, buff.Length - sp.Offset)).ToArray());
                Dispatch?.Invoke(session, sp.Type, packet);
            }
            if (session.InZone) AfterParse?.Invoke(session);

            session.ClientPacketId = code;

            // the client did not get our previous datagram: resend it instead of building a new one
            if (BitConverter.ToUInt16(buff, 2) != session.ServerPacketId)
            {
                // caching zone packets would keep the client from ever connecting
                if (lastType == 0x00A) return 0;

                BitConverter.TryWriteBytes(new Span<byte>(session.ServerPacketData, 2, 2), code);
                BitConverter.TryWriteBytes(new Span<byte>(session.ServerPacketData, 8, 2), (ushort)Timestamp()); // ref<uint16>(…, 8)

                size = session.ServerPacketSize;
                Array.Copy(session.ServerPacketData, buff, size);
                return -1;
            }

            session.ServerPacketId += 1;
            return 0;
        }

        // Builds the outgoing datagram from session.PacketList into buff. Returns its size (0: nothing sendable).
        public int SendParse(byte[] buff, MapSession session, bool usePreviousKey)
        {
            PreparePacket(buff, session);

            const uint NoSize = uint.MaxValue;
            uint packetSize = NoSize;
            int packetCount = Math.Clamp(session.PacketList.Count, 0, MapFraming.MaxPacketPerCompression);
            int packets = 0;
            int size = MapFraming.HeaderSize;
            bool incrementKeyAfterEncrypt = false;
            byte[] compressed = null;

            do
            {
                do
                {
                    size = MapFraming.HeaderSize;
                    packets = 0;

                    var node = session.PacketList.First;
                    while (node != null && size + node.Value.GetSize() < MapFraming.MaxBufferSize && packets < packetCount)
                    {
                        var small = node.Value;

                        // hold 0x00B until the client decrypts a packet with the current key
                        if (small.GetOpcode() == LogoutPacket.Id && !session.HasDecryptedPacket) break;

                        small.SetSequence(session.ServerPacketId);

                        // keep the zoneout packet in case it has to be resent
                        if (small.GetOpcode() == LogoutPacket.Id)
                        {
                            session.ZoneIpp = LogoutPacket.ZoneIpp(small);
                            session.ZoneType = LogoutPacket.State(small);
                            incrementKeyAfterEncrypt = true;
                            _store.MarkZoneOut(session.CharId, session.ZoneType, session.ZoneIpp);
                        }

                        int sz = (int)small.GetSize();
                        Array.Copy(small.Buffer, 0, buff, size, sz);
                        if (small.GetOpcode() == LogoutPacket.Id)
                            LogoutPacket.ApplyClientAddress(new Span<byte>(buff, size, sz), session.ClientServerIp, ZoneIpFromClient);
                        size += sz;
                        packets++;
                        node = node.Next;
                    }

                    packetCount -= packetCount / 3;

                    compressed = MapFraming.Compress(new ReadOnlySpan<byte>(buff, MapFraming.HeaderSize, size - MapFraming.HeaderSize));
                    if (compressed == null)
                    {
                        _log($"zlib compression error from charid '{session.CharId}', attempted pre-compression packet size '{size}'");
                        continue;
                    }
                    packetSize = (uint)compressed.Length;
                } while (packetCount > 0 && packetSize > MapFraming.MaxClientPayload);

                if (packetSize == NoSize)
                {
                    if (session.PacketList.Count > 0)
                    {
                        session.PacketList.RemoveFirst();
                        packetCount = session.PacketList.Count;
                    }
                    else
                    {
                        return 0;
                    }
                }
            } while (packetSize == NoSize);

            for (int i = 0; i < packets; i++) session.PacketList.RemoveFirst();

            size = Finalize(buff, compressed, session, usePreviousKey);

            // increment the key after 0x00B was sent (otherwise the client would never get it)
            if (incrementKeyAfterEncrypt)
            {
                session.IncrementBlowfish();
                _log($"map_session: key counter for charid {session.CharId} is now {session.Blowfish.Key[4]:X8}");
                _store.UpdateSessionKey(session.CharId, session.Blowfish.Key);
                session.Blowfish.Status = BlowfishStatus.PendingZone;
                OnZoneOut?.Invoke(session);
                session.Char = null;
                session.InZone = false;
                session.PacketList.Clear();
            }

            return size;
        }

        private void PreparePacket(byte[] buff, MapSession session) =>
            MapFraming.PrepareHeader(buff, session.ServerPacketId, session.ClientPacketId, Timestamp());

        // finalizePacket: writes the enciphered region after the (already prepared) header; returns the datagram size.
        private static int Finalize(byte[] buff, byte[] compressed, MapSession session, bool usePreviousKey)
        {
            var key = session.Blowfish.Status == BlowfishStatus.PendingZone && usePreviousKey ? session.PrevBlowfish : session.Blowfish;
            byte[] datagram = MapFraming.Finalize(buff, compressed, key.Cipher);
            Array.Copy(datagram, buff, datagram.Length);
            return datagram.Length;
        }
    }
}
