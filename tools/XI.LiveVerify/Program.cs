// XI.LiveVerify — operator tool for the Phase-0 LIVE handshake gate.
//
// The offline verifier (XI.Verify) proves our C# primitives equal the compiled C++
// reference on synthetic vectors. This tool closes the last mile against a RUNNING
// server by recomputing the same values the server itself derives/logs, so an
// operator can diff them instead of trusting the connection "just works".
//
// Live ground truth (src/login/data_session.cpp 0xA2 handler + src/map/map_networking.cpp):
//   key3[20] = md5(session_hash)[0..16) || (keyCounter+3 as u32 LE @16)
//   -> stored verbatim as accounts_sessions.session_key
//   -> map loads it verbatim into blowfish.key[5] and calls initBlowfish()
//   login server logs: FNV-1a/32 over key3[0..16), printed as "key value {:08X}"
//
// Subcommands:
//   keydb   <hex: 20-byte session_key blob>        derive map cipher + recompute login FNV fingerprint
//   login   <hex: 92-byte GP_CLI_LOGIN body>       verify LoginPacketCheck (0x00A)
//   frame   <hex: 20B key> <hex: cipher region>    decipher a live map packet, verify md5, decompress if flagged
//   zlib    <hex: input>                           round-trip the prefix-code codec
//   fnv     <hex: >=16 bytes>                      print FNV-1a/32 over the first 16 bytes

using System;
using System.Text;
using XI.Crypto;

internal static class Program
{
    static int Main(string[] argv)
    {
        if (argv.Length < 1)
        {
            Usage();
            return 2;
        }

        try
        {
            switch (argv[0].ToLowerInvariant())
            {
                case "keydb": return KeyDb(argv);
                case "login": return Login(argv);
                case "frame": return Frame(argv);
                case "zlib": return Zlib(argv);
                case "fnv": return Fnv(argv);
                case "-h":
                case "--help":
                case "help": Usage(); return 0;
                default:
                    Console.Error.WriteLine($"unknown subcommand: {argv[0]}");
                    Usage();
                    return 2;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
    }

    static void Usage()
    {
        Console.Error.WriteLine("usage: XI.LiveVerify <keydb|login|frame|zlib|fnv> [hex args]");
        Console.Error.WriteLine("  keydb <20B session_key hex>          derive map cipher + login FNV fingerprint");
        Console.Error.WriteLine("  login <92B GP_CLI_LOGIN body hex>    verify LoginPacketCheck (0x00A)");
        Console.Error.WriteLine("  frame <20B key hex> <cipherRegion>   decipher live map packet, verify md5, decompress");
        Console.Error.WriteLine("  zlib  <input hex>                    round-trip the FFXI prefix-code codec");
        Console.Error.WriteLine("  fnv   <hex (>=16B)>                  FNV-1a/32 over first 16 bytes");
    }

    // keydb: feed the exact 20-byte blob from accounts_sessions.session_key.
    static int KeyDb(string[] argv)
    {
        if (argv.Length < 2) { Console.Error.WriteLine("keydb needs a 20-byte session_key blob (40 hex chars)"); return 2; }
        byte[] blob = HexToBytes(argv[1]);
        if (blob.Length != 20) { Console.Error.WriteLine($"session_key blob must be 20 bytes, got {blob.Length}"); return 2; }

        uint[] key5 = ToU32LE(blob);
        Console.WriteLine("session_key words[5] (LE u32):");
        for (int i = 0; i < 5; i++) Console.WriteLine($"  key[{i}] = 0x{key5[i]:X8}");

        // Login server's FNV-1a fingerprint over key3[0..16) == the "key value {:08X}" log line.
        Console.WriteLine($"login FNV-1a 'key value'  = 0x{Fnv1a32(first16(blob)):X8}   <- compare to data_session.cpp log");
        Console.WriteLine($"key counter (u32 @16)     = 0x{BitConverter.ToUInt32(blob, 16):X8}   <- compare to the 'key counter' log field");

        // initBlowfish: md5 over the raw 20 bytes -> truncate at first zero -> blowfish_init.
        byte[] hash = new byte[16];
        var bf = SessionCipher.Create(key5, hash);
        Console.WriteLine($"initBlowfish md5 (pre-trunc, first 16) = {Hex(md5Raw(key5))}");
        Console.WriteLine($"blowfish hash (post-trunc)             = {Hex(hash)}");
        Console.WriteLine($"derived P0 = 0x{bf.P[0]:X8}  P17 = 0x{bf.P[17]:X8}   <- compare to a XI.Verify session vector");

        // Prove the derived cipher is usable: framing a known round-trip through it.
        byte[] probe = { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03, 0x04 };
        byte[] region = PacketFraming.EncipherPayload(probe, bf);
        bool rt = PacketFraming.TryDecipher(region, bf, out byte[] back) && Hex(back) == Hex(probe);
        Console.WriteLine($"framing round-trip under derived key: {(rt ? "OK" : "FAIL")}");
        return rt ? 0 : 1;
    }

    // login: verify the additive LoginPacketCheck on a captured 0x00A body.
    static int Login(string[] argv)
    {
        if (argv.Length < 2) { Console.Error.WriteLine("login needs the 92-byte GP_CLI_LOGIN body"); return 2; }
        byte[] pkt = HexToBytes(argv[1]);
        if (pkt.Length < LoginPacketLayout.SizeOf)
        {
            Console.Error.WriteLine($"GP_CLI_LOGIN body must be >= {LoginPacketLayout.SizeOf} bytes, got {pkt.Length}");
            return 2;
        }
        byte stored = pkt[LoginPacketLayout.OffLoginPacketCheck];
        byte recomputed = LoginPacket.ComputeChecksum(pkt);
        Console.WriteLine($"stored LoginPacketCheck   = 0x{stored:X2}");
        Console.WriteLine($"recomputed (sum [8,92))   = 0x{recomputed:X2}");
        Console.WriteLine($"checksum verdict          = {(stored == recomputed ? "PASS" : "FAIL")}");

        ushort hdr = (ushort)(pkt[0] | (pkt[1] << 8));
        Console.WriteLine($"header id={LoginPacket.IdOf(hdr):X3} size={LoginPacket.SizeOf(hdr):X2} (id:9|size:7)");
        Console.WriteLine($"sync=0x{BitConverter.ToUInt16(pkt, LoginPacketLayout.OffSync):X4} unknown00=0x{BitConverter.ToUInt16(pkt, LoginPacketLayout.OffUnknown00):X4} uniqueNo=0x{BitConverter.ToUInt32(pkt, LoginPacketLayout.OffUniqueNo):X8}");
        return stored == recomputed ? 0 : 1;
    }

    // frame: decipher a captured live map cipher region with the session key.
    static int Frame(string[] argv)
    {
        if (argv.Length < 3) { Console.Error.WriteLine("frame needs <20B key hex> <cipher region hex>"); return 2; }
        byte[] blob = HexToBytes(argv[1]);
        if (blob.Length != 20) { Console.Error.WriteLine("key must be 20 bytes"); return 2; }
        var bf = SessionCipher.Create(ToU32LE(blob), null);

        byte[] region = HexToBytes(argv[2]);
        if (!PacketFraming.TryDecipher(region, bf, out byte[] body))
        {
            Console.Error.WriteLine("TryDecipher FAILED: md5(body) != trailing hash -> wrong key, tampered, or mis-sliced region");
            return 1;
        }
        Console.WriteLine($"md5(region) checksum: OK. body={body.Length} bytes");
        if (body.Length > 0 && body[0] == 1)
        {
            uint inBits = body.Length >= 5 ? BitConverter.ToUInt32(body, 1) * 8u : (uint)(body.Length - 1) * 8u;
            byte[] outb = new byte[65536];
            int got = FfxiZlib.Decompress(Slice(body, 0, body.Length), Math.Min(inBits, (uint)body.Length * 8u), outb);
            Console.WriteLine($"body[0]==1 (compressed): decompressed {Math.Max(got,0)} bytes (prefix-only, trailing pad symbol is non-deterministic)");
            Console.WriteLine($"  payload head = {Hex(Slice(outb, 0, Math.Min(Math.Max(got,0), 32)))}");
        }
        else
        {
            Console.WriteLine($"payload hex = {Hex(Slice(body, 0, Math.Min(body.Length, 64)))}");
        }
        return 0;
    }

    static int Zlib(string[] argv)
    {
        if (argv.Length < 2) { Console.Error.WriteLine("zlib needs input bytes"); return 2; }
        byte[] input = HexToBytes(argv[1]);
        byte[] compressed = FfxiZlib.CompressToBytes(input);
        byte[] back = new byte[65536];
        int outBits = (compressed.Length * 8);
        int got = FfxiZlib.Decompress(compressed, (uint)outBits, back);
        bool prefix = got >= input.Length;
        for (int i = 0; prefix && i < input.Length; i++) if (back[i] != input[i]) prefix = false;
        Console.WriteLine($"in={input.Length}B out={compressed.Length}B (bits~{outBits}) got={got} prefixMatchesIn={(prefix ? 1 : 0)}");
        Console.WriteLine($"  C = {Hex(compressed)}");
        return prefix ? 0 : 1;
    }

    static int Fnv(string[] argv)
    {
        if (argv.Length < 2) { Console.Error.WriteLine("fnv needs >=16 bytes"); return 2; }
        byte[] b = HexToBytes(argv[1]);
        if (b.Length < 16) { Console.Error.WriteLine("fnv needs at least 16 bytes"); return 2; }
        Console.WriteLine($"FNV-1a/32 over first 16 bytes = 0x{Fnv1a32(first16(b)):X8}");
        return 0;
    }

    // ---- helpers ----

    // data_session.cpp: keyFingerprint = 2166136261u; for i<16: fp = (fp ^ key3[i]) * 16777619u;
    static uint Fnv1a32(byte[] b)
    {
        uint h = 2166136261u;
        for (int i = 0; i < b.Length; i++) h = (h ^ b[i]) * 16777619u;
        return h;
    }

    static byte[] first16(byte[] b)
    {
        byte[] r = new byte[16];
        Array.Copy(b, r, 16);
        return r;
    }

    static uint[] ToU32LE(byte[] b)
    {
        int n = b.Length / 4;
        uint[] r = new uint[n];
        for (int i = 0; i < n; i++)
            r[i] = (uint)(b[i * 4] | (b[i * 4 + 1] << 8) | (b[i * 4 + 2] << 16) | (b[i * 4 + 3] << 24));
        return r;
    }

    static byte[] md5Raw(uint[] key5)
    {
        byte[] raw = new byte[20];
        for (int i = 0; i < 5; i++)
        {
            raw[i * 4 + 0] = (byte)(key5[i] & 0xFF);
            raw[i * 4 + 1] = (byte)((key5[i] >> 8) & 0xFF);
            raw[i * 4 + 2] = (byte)((key5[i] >> 16) & 0xFF);
            raw[i * 4 + 3] = (byte)((key5[i] >> 24) & 0xFF);
        }
        return System.Security.Cryptography.MD5.HashData(raw);
    }

    static byte[] Slice(byte[] a, int off, int len)
    {
        if (len < 0) len = 0;
        if (off + len > a.Length) len = a.Length - off;
        byte[] r = new byte[len];
        Array.Copy(a, off, r, 0, len);
        return r;
    }

    static byte[] HexToBytes(string hex)
    {
        hex = hex.Replace(" ", "").Replace("-", "").Replace("\r", "").Replace("\n", "").Trim();
        if (hex.Length % 2 != 0) throw new FormatException("hex string has odd length");
        byte[] b = new byte[hex.Length / 2];
        for (int i = 0; i < b.Length; i++)
            b[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return b;
    }

    static string Hex(byte[] b)
    {
        var sb = new StringBuilder(b.Length * 2);
        foreach (byte x in b) sb.Append(x.ToString("X2"));
        return sb.ToString();
    }
}
