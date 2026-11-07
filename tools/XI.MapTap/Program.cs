using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using XI.Crypto;
using XI.Map;
using XI.Persistence;

// XI.MapTap -- records real client <-> C++ xi_map traffic as the reference for the C# map server.
//
//   tap    <listenPort> <upstreamPort> <out.tap> [sqlPort]
//          transparent UDP proxy on 127.0.0.1. Run xi_map with XI_NETWORK_MAP_PORT=<upstreamPort> (no --ip, so it
//          still hosts every zone) and leave zone_settings pointing the client at <listenPort>. Every datagram is
//          recorded; on each plaintext 0x00A the char's accounts_sessions.session_key is snapshotted into the file.
//   decode <in.tap> [out.txt]
//          deciphers + decompresses both directions and prints one line per sub-packet.
//
// .tap record: u8 kind (1 = C2S, 2 = S2C, 3 = key) | i64 utc ticks | u16 client port | i32 len | bytes
//              key payload = u32 charid + 20-byte session_key
internal static class Program
{
    private const byte C2S = 1, S2C = 2, Key = 3;

    private static int Main(string[] args)
    {
        if (args.Length >= 4 && args[0] == "tap")
            return Tap(int.Parse(args[1]), int.Parse(args[2]), args[3], args.Length > 4 ? int.Parse(args[4]) : 3307);
        if (args.Length >= 2 && args[0] == "decode")
        {
            using var w = args.Length > 2 ? new StreamWriter(args[2]) : new StreamWriter(Console.OpenStandardOutput());
            return Decode(args[1], w);
        }
        Console.Error.WriteLine("usage: XI.MapTap tap <listenPort> <upstreamPort> <out.tap> [sqlPort] | decode <in.tap> [out.txt]");
        return 2;
    }

    // ---------------------------------------------------------------- tap

    private static readonly object WriteLock = new object();
    private static BinaryWriter _out;

    private static void Record(byte kind, int clientPort, ReadOnlySpan<byte> data)
    {
        lock (WriteLock)
        {
            _out.Write(kind);
            _out.Write(DateTime.UtcNow.Ticks);
            _out.Write((ushort)clientPort);
            _out.Write(data.Length);
            _out.Write(data);
            _out.Flush();
        }
    }

    private static int Tap(int listenPort, int upstreamPort, string path, int sqlPort)
    {
        _out = new BinaryWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read));
        var db = new MySqlDatabase(new NetworkSettings { SqlHost = "127.0.0.1", SqlPort = sqlPort, SqlLogin = "root", SqlPassword = "root", SqlDatabase = "xidb" });
        var front = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        DisableConnReset(front);
        front.Bind(new IPEndPoint(IPAddress.Any, listenPort));
        var upstream = new IPEndPoint(IPAddress.Loopback, upstreamPort);
        var legs = new ConcurrentDictionary<int, Socket>();
        Console.WriteLine($"tap: 0.0.0.0:{listenPort} -> {upstream}, recording to {path}");

        var buf = new byte[4096];
        EndPoint from = new IPEndPoint(IPAddress.Any, 0);
        while (true)
        {
            int n;
            try { n = front.ReceiveFrom(buf, ref from); }
            catch (SocketException) { continue; }
            var client = (IPEndPoint)from;
            var data = buf.AsSpan(0, n);
            Record(C2S, client.Port, data);

            var copy = data.ToArray();
            if (MapFraming.TryReadPlainLogin(copy, n, out var login))
            {
                var key = LoadKey(db, login.CharId);
                Console.WriteLine($"tap: 0x00A charid {login.CharId} from {client} retry {login.Retry} key {(key == null ? "MISSING" : Convert.ToHexString(key))}");
                if (key != null)
                {
                    var rec = new byte[24];
                    BitConverter.TryWriteBytes(rec.AsSpan(0, 4), login.CharId);
                    key.CopyTo(rec, 4);
                    Record(Key, client.Port, rec);
                }
            }

            var leg = legs.GetOrAdd(client.Port, _ => OpenLeg(front, client));
            leg.SendTo(copy, upstream);
        }
    }

    // one upstream socket per client so xi_map sees each client as a distinct address
    private static Socket OpenLeg(Socket front, IPEndPoint client)
    {
        var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        DisableConnReset(s);
        s.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        Console.WriteLine($"tap: new client {client} via {s.LocalEndPoint}");
        new Thread(() =>
        {
            var buf = new byte[4096];
            EndPoint from = new IPEndPoint(IPAddress.Any, 0);
            while (true)
            {
                int n;
                try { n = s.ReceiveFrom(buf, ref from); }
                catch (SocketException) { continue; }
                Record(S2C, client.Port, buf.AsSpan(0, n));
                front.SendTo(buf, 0, n, SocketFlags.None, client);
            }
        }) { IsBackground = true }.Start();
        return s;
    }

    private static void DisableConnReset(Socket s)
    {
        if (OperatingSystem.IsWindows()) s.IOControl(-1744830452, new byte[4], null);
    }

    private static byte[] LoadKey(MySqlDatabase db, uint charId)
    {
        try
        {
            var rs = db.Execute("SELECT session_key FROM accounts_sessions WHERE charid = ?", new List<BoundValue> { Binder.Lower(charId) });
            return rs != null && rs.Next() ? rs.GetBlobBytes("session_key") : null;
        }
        catch (Exception e)
        {
            Console.WriteLine($"tap: key lookup failed: {e.Message}");
            return null;
        }
    }

    // ---------------------------------------------------------------- decode

    private sealed class Stream_
    {
        public uint[] Key;
        public uint CharId;
    }

    private static uint[] Words(byte[] key20)
    {
        var k = new uint[5];
        for (int i = 0; i < 5; i++) k[i] = BitConverter.ToUInt32(key20, i * 4);
        return k;
    }

    private static int Decode(string path, TextWriter w)
    {
        var streams = new Dictionary<int, Stream_>();
        long t0 = 0;
        int bad = 0;
        // ReadWrite share so a capture can be decoded while the tap is still appending to it
        using var r = new BinaryReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        long end = r.BaseStream.Length;
        while (r.BaseStream.Position + 15 <= end)
        {
            byte kind = r.ReadByte();
            long ticks = r.ReadInt64();
            int port = r.ReadUInt16();
            var data = r.ReadBytes(r.ReadInt32());
            if (t0 == 0) t0 = ticks;
            string t = $"{(ticks - t0) / TimeSpan.TicksPerMillisecond,8}";
            if (!streams.TryGetValue(port, out var st)) streams[port] = st = new Stream_();

            if (kind == Key)
            {
                st.CharId = BitConverter.ToUInt32(data, 0);
                st.Key = Words(data.Skip(4).ToArray());
                w.WriteLine($"{t} :{port} KEY charid {st.CharId} {Convert.ToHexString(data, 4, 20)}");
                continue;
            }

            string dir = kind == C2S ? "C>S" : "S>C";
            var buf = new byte[MapFraming.MaxBufferSize];
            data.CopyTo(buf, 0);
            int size = data.Length;
            ushort id0 = BitConverter.ToUInt16(buf, 0), id1 = BitConverter.ToUInt16(buf, 2);
            uint time = BitConverter.ToUInt32(buf, 8);

            bool plain = kind == C2S && MapFraming.ChecksumMatches(buf, size);
            if (plain)
            {
                size -= MapFraming.HashSize;
            }
            else
            {
                if (st.Key == null || !TryDecipher(buf, data, st))
                {
                    w.WriteLine($"{t} :{port} {dir} {id0}/{id1} len {data.Length} UNDECODED");
                    bad++;
                    continue;
                }
                size = MapFraming.Decompress(buf, data.Length);
                if (size < 0)
                {
                    w.WriteLine($"{t} :{port} {dir} {id0}/{id1} len {data.Length} BAD-ZLIB");
                    bad++;
                    continue;
                }
            }

            w.WriteLine($"{t} :{port} {dir} {id0}/{id1} time {time:X8} len {data.Length}{(plain ? " PLAIN" : "")}");
            foreach (var s in MapFraming.SubPackets(buf, size))
                w.WriteLine($"           {s.Type:X3} sz {s.Size,3} sync {s.Sync,5} {Convert.ToHexString(buf, s.Offset, s.Size)}");
        }
        Console.Error.WriteLine($"decode: {bad} undecodable datagrams");
        return 0;
    }

    // the session key in the file is the one in effect at 0x00A; after a 0x00B the server moves to key[4]+2
    private static bool TryDecipher(byte[] buf, byte[] data, Stream_ st)
    {
        if (MapFraming.Decipher(buf, data.Length, SessionCipher.Create(st.Key))) return true;
        var next = (uint[])st.Key.Clone();
        next[4] += 2;
        data.CopyTo(buf, 0);
        if (!MapFraming.Decipher(buf, data.Length, SessionCipher.Create(next))) return false;
        st.Key = next;
        return true;
    }
}
