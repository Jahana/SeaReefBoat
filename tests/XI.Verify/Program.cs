// Dependency-free parity verifier. Reads the oracle's golden.txt (produced by the
// real compiled blowfish.cpp) and recomputes every vector with the C# port.
// Any mismatch => exit code 1. This is the Phase-0 go/no-go evidence.

using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using XI.Crypto;

internal static class Program
{
    // Identical to the keys/blocks baked into csharp/oracle/harness.cpp.
    static readonly byte[][] Keys =
    {
        new byte[]{0x00,0x01,0x02,0x03,0x04,0x05,0x06,0x07,0x08,0x09,0x0A,0x0B,0x0C,0x0D,0x0E,0x0F},
        new byte[]{0x88,0x6A,0x3F,0x24,0xD3,0x08,0xA3,0x85,0x2E,0x8A,0x19,0x13,0x44,0x73,0x70,0x03},
        new byte[]{0xDE,0xAD,0xBE,0xEF,0xDE,0xAD,0xBE,0xEF,0xDE,0xAD,0xBE,0xEF,0xDE,0xAD,0xBE,0xEF},
        new byte[]{0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF},
    };
    static readonly uint[][] Blocks =
    {
        new uint[]{0x00000000u,0x00000000u},
        new uint[]{0xFFFFFFFFu,0xFFFFFFFFu},
        new uint[]{0x01234567u,0x89ABCDEFu},
        new uint[]{0xA5A5A5A5u,0x5A5A5A5Au},
    };

    static int _pass, _fail;

    static int Main(string[] argv)
    {
        string goldenPath = argv.Length > 0
            ? argv[0]
            : @"H:\023-FFXI-server\csharp\oracle\golden.txt";

        if (!File.Exists(goldenPath))
        {
            Console.Error.WriteLine($"golden.txt not found: {goldenPath}");
            return 2;
        }
        string golden = File.ReadAllText(goldenPath);

        // The zlib codec oracle writes its vectors to a sibling file.
        string zlibPath = argv.Length > 1
            ? argv[1]
            : Path.Combine(Path.GetDirectoryName(goldenPath) ?? "", "zlib_golden.txt");
        string zlibGolden = File.Exists(zlibPath) ? File.ReadAllText(zlibPath) : "";

        // The GP_CLI_LOGIN layout oracle writes its vectors to another sibling file.
        string loginPath = argv.Length > 2
            ? argv[2]
            : Path.Combine(Path.GetDirectoryName(goldenPath) ?? "", "login_golden.txt");
        string loginGolden = File.Exists(loginPath) ? File.ReadAllText(loginPath) : "";

        Console.WriteLine("== Blowfish parity: C# port vs compiled blowfish.cpp ==");

        // Per-key vectors: V key=N in=.. P0=.. P17=.. enc=..
        foreach (Match m in Regex.Matches(golden,
            @"V key=(?<k>\d+) in=(?<in>[0-9A-Fa-f]{16}) P0=(?<p0>[0-9A-Fa-f]{8}) P17=(?<p17>[0-9A-Fa-f]{8}) enc=(?<enc>[0-9A-Fa-f]{16})"))
        {
            int k = int.Parse(m.Groups["k"].Value);
            var bf = new Blowfish();
            bf.Init(Keys[k]);

            uint expP0 = U32(m.Groups["p0"].Value);
            uint expP17 = U32(m.Groups["p17"].Value);
            uint inL = U32(m.Groups["in"].Value.Substring(0, 8));
            uint inR = U32(m.Groups["in"].Value.Substring(8, 8));
            uint expEncL = U32(m.Groups["enc"].Value.Substring(0, 8));
            uint expEncR = U32(m.Groups["enc"].Value.Substring(8, 8));

            Check($"key{k} init P0", $"0x{expP0:X8}", $"0x{bf.P[0]:X8}");
            Check($"key{k} init P17", $"0x{expP17:X8}", $"0x{bf.P[17]:X8}");

            uint l = inL, r = inR;
            bf.Encipher(ref l, ref r);
            Check($"key{k} encipher", $"{expEncL:X8}{expEncR:X8}", $"{l:X8}{r:X8}");

            bf.Decipher(ref l, ref r);
            Check($"key{k} decipher->in", $"{inL:X8}{inR:X8}", $"{l:X8}{r:X8}");
        }

        // Blocks fast-path parity: encipher 10 blocks (4-wide + 2 tail in C), compare to scalar.
        {
            var bf = new Blowfish();
            bf.Init(Keys[0]);

            const int nBlocks = 10;
            uint[] scalar = new uint[nBlocks * 2];
            uint[] fast = new uint[nBlocks * 2];
            uint[] orig = new uint[nBlocks * 2];
            for (int i = 0; i < nBlocks * 2; i++)
                orig[i] = scalar[i] = fast[i] = unchecked(0x11111111u * (uint)(i + 1));

            for (int b = 0; b < nBlocks; b++)
            {
                uint l = scalar[b * 2], r = scalar[b * 2 + 1];
                bf.Encipher(ref l, ref r);
                scalar[b * 2] = l; scalar[b * 2 + 1] = r;
            }
            bf.EncipherBlocks(fast, nBlocks);

            string firstHex = "";
            for (int i = 0; i < 4; i++) firstHex += fast[i].ToString("X8");
            var bm = Regex.Match(golden, @"B enc_blocks_match=(\d) first=([0-9A-Fa-f]{32})");
            if (bm.Success)
            {
                Check("blocks enc==scalar (oracle flag)", bm.Groups[1].Value, "1");
                Check("blocks enc first4", bm.Groups[2].Value.ToUpperInvariant(), firstHex);
            }
            Check("blocks fast==scalar (C#-internal)",
                string.Concat(System.Linq.Enumerable.Select(scalar, x => x.ToString("X8"))),
                string.Concat(System.Linq.Enumerable.Select(fast, x => x.ToString("X8"))));

            bf.DecipherBlocks(fast, nBlocks);
            bool rt = true;
            for (int i = 0; i < nBlocks * 2; i++) if (fast[i] != orig[i]) rt = false;
            Check("blocks decipher roundtrip", "true", rt.ToString().ToLowerInvariant());
        }

        // Session-cipher chain: MD5(key[5]) -> truncate@zero -> blowfish_init -> encipher.
        // Proves the BCL MD5 stands in for the custom md52.cpp AND the whole
        // initBlowfish() composition is byte-identical to the compiled reference.
        {
            var km = Regex.Match(golden, @"K md5_pre=([0-9A-Fa-f]{32})");
            var kq = Regex.Match(golden, @"K md5_post=([0-9A-Fa-f]{32})");
            var kv = Regex.Match(golden, @"K P0=([0-9A-Fa-f]{8}) P17=([0-9A-Fa-f]{8}) enc=([0-9A-Fa-f]{16})");
            if (km.Success && kq.Success && kv.Success)
            {
                uint[] key5 = { 0x7F000001u, 0x1234ABCDu, 0x00000000u, 0xDEADBEEFu, 0x00000002u };

                // raw 20 bytes little-endian -> BCL MD5, compare to oracle md5_pre
                byte[] raw = new byte[20];
                for (int i = 0; i < 5; i++)
                {
                    raw[i * 4 + 0] = (byte)(key5[i] & 0xFF);
                    raw[i * 4 + 1] = (byte)((key5[i] >> 8) & 0xFF);
                    raw[i * 4 + 2] = (byte)((key5[i] >> 16) & 0xFF);
                    raw[i * 4 + 3] = (byte)((key5[i] >> 24) & 0xFF);
                }
                byte[] bclHash = System.Security.Cryptography.MD5.HashData(raw);
                Check("BCL MD5 == oracle md5_pre", km.Groups[1].Value.ToUpperInvariant(),
                      Hex(bclHash));

                byte[] derived = new byte[16];
                var bf = SessionCipher.Create(key5, derived);
                Check("SessionCipher hash == oracle md5_post", kq.Groups[1].Value.ToUpperInvariant(),
                      Hex(derived));

                uint l = 0x01234567u, r = 0x89ABCDEFu;
                bf.Encipher(ref l, ref r);
                Check("session P0", "0x" + kv.Groups[1].Value.ToUpperInvariant(), $"0x{bf.P[0]:X8}");
                Check("session P17", "0x" + kv.Groups[2].Value.ToUpperInvariant(), $"0x{bf.P[17]:X8}");
                Check("session encipher", kv.Groups[3].Value.ToUpperInvariant(), $"{l:X8}{r:X8}");
            }
        }

        // Packet framing: cipher region = payload||md5(payload), even words enciphered.
        {
            var fp = Regex.Match(golden, @"F pre=([0-9A-Fa-f]+)");
            var fq = Regex.Match(golden, @"F cypherSize=(\d+) post=([0-9A-Fa-f]+)");
            var fr = Regex.Match(golden, @"F roundtrip=(\d)");
            if (fp.Success && fq.Success && fr.Success)
            {
                byte[] body = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
                var bf = new Blowfish();
                bf.Init(Keys[0]);

                // pre = body words || md5(body) words, before encipher.
                byte[] bhash = System.Security.Cryptography.MD5.HashData(body);
                byte[] preRegion = new byte[28];
                Array.Copy(body, preRegion, 12);
                Array.Copy(bhash, 0, preRegion, 12, 16);
                Check("framer pre", fp.Groups[1].Value.ToUpperInvariant(), WordsHex(preRegion));

                // post = EncipherPayload(body); compare word-for-word to oracle.
                byte[] enc = PacketFraming.EncipherPayload(body, bf);
                Check("framer post (EncipherPayload)", fq.Groups[2].Value.ToUpperInvariant(), WordsHex(enc));

                // the odd trailing word (index 6) must survive in plaintext.
                uint lastWord = (uint)(enc[24] | (enc[25] << 8) | (enc[26] << 16) | (enc[27] << 24));
                Check("framer odd tail word unenciphered", "2E4A7945", lastWord.ToString("X8"));

                bool ok = PacketFraming.TryDecipher(enc, bf, out byte[] back);
                bool bodyEq = ok && back.Length == 12
                    && System.Linq.Enumerable.SequenceEqual(back, (System.Collections.Generic.IEnumerable<byte>)body);
                Check("framer roundtrip", fr.Groups[1].Value, bodyEq ? "1" : "0");
            }
        }

        // zlib codec: C# port vs compiled zlib.cpp (custom per-byte prefix code, NOT DEFLATE).
        {
            foreach (Match zm in Regex.Matches(zlibGolden,
                @"P=(?<p>[0-9A-Fa-f]+)\r?\nC idx=(?<idx>\d+) outBits=(?<ob>\d+) outBytes=(?<oz>\d+)\r?\nC=(?<c>[0-9A-Fa-f]+)\r?\nD idx=\d+ got=(?<got>\d+) prefixMatchesIn=(?<pm>\d+)\r?\nR=(?<r>[0-9A-Fa-f]+)"))
            {
                int idx = int.Parse(zm.Groups["idx"].Value);
                byte[] pIn = BytesFromHex(zm.Groups["p"].Value);
                byte[] cExp = BytesFromHex(zm.Groups["c"].Value);
                int outBitsExp = int.Parse(zm.Groups["ob"].Value);
                int outBytesExp = int.Parse(zm.Groups["oz"].Value);

                // Compress must reproduce the exact output bit count and every output byte.
                byte[] scratch = new byte[1 + (pIn.Length * 32 + 15) / 8];
                int outBits = FfxiZlib.Compress(pIn, scratch);
                Check($"zlib[{idx}] compress outBits", outBitsExp.ToString(), outBits.ToString());
                int outBytes = (outBits + 7) / 8;
                Check($"zlib[{idx}] compress outBytes", outBytesExp.ToString(), outBytes.ToString());
                byte[] cGot = new byte[outBytes];
                Array.Copy(scratch, cGot, outBytes);
                Check($"zlib[{idx}] compress bytes", Hex(cExp), Hex(cGot));

                // Decompress (fed the compressed bytes + bit count) must reproduce the
                // input as a prefix. The single trailing pad symbol depends on scratch
                // bytes past the frame in the C harness, so assert only the deterministic
                // prefix (the oracle's prefixMatchesIn flag), not the pad byte.
                byte[] back = new byte[4096];
                int w = FfxiZlib.Decompress(cExp, (uint)outBitsExp, back);
                Check($"zlib[{idx}] decompress got>=in", "true", (w >= pIn.Length).ToString().ToLowerInvariant());
                bool prefixOk = true;
                for (int i = 0; i < pIn.Length; i++) if (back[i] != pIn[i]) prefixOk = false;
                Check($"zlib[{idx}] decompress prefix==in", zm.Groups["pm"].Value, prefixOk ? "1" : "0");
            }
        }

        // GP_CLI_LOGIN layout oracle: prove the C# StructLayout constants, the additive
        // LoginPacketCheck, and the id:9/size:7 header packing match the compiler exactly.
        if (loginGolden.Length > 0)
        {
            Console.WriteLine("\n== GP_CLI_LOGIN (0x00A) layout parity: C# vs compiler ==");

            var sizeM = Regex.Match(loginGolden, @"SIZE=(\d+)");
            if (sizeM.Success)
                Check("login sizeof", sizeM.Groups[1].Value, LoginPacketLayout.SizeOf.ToString());

            // offsetof(...) per field vs the C# constants.
            var offExpected = new System.Collections.Generic.Dictionary<string, int>
            {
                ["sync"] = LoginPacketLayout.OffSync,
                ["loginPacketCheck"] = LoginPacketLayout.OffLoginPacketCheck,
                ["padding00"] = LoginPacketLayout.OffPadding00,
                ["unknown00"] = LoginPacketLayout.OffUnknown00,
                ["unknown01"] = LoginPacketLayout.OffUnknown01,
                ["UniqueNo"] = LoginPacketLayout.OffUniqueNo,
                ["GrapIDTbl"] = LoginPacketLayout.OffGrapIDTbl,
                ["sName"] = LoginPacketLayout.OffSName,
                ["sAccunt"] = LoginPacketLayout.OffSAccunt,
                ["Ticket"] = LoginPacketLayout.OffTicket,
                ["Ver"] = LoginPacketLayout.OffVer,
                ["sPlatform"] = LoginPacketLayout.OffSPlatform,
                ["uCliLang"] = LoginPacketLayout.OffUCliLang,
                ["dammyArea"] = LoginPacketLayout.OffDammyArea,
            };
            foreach (Match om in Regex.Matches(loginGolden, @"OFF_(\w+)=(\d+)"))
            {
                string field = om.Groups[1].Value;
                int exp = int.Parse(om.Groups[2].Value);
                if (offExpected.TryGetValue(field, out int got))
                    Check($"login offsetof {field}", exp.ToString(), got.ToString());
                else
                    Check($"login offsetof {field} (unknown field)", "mapped", "missing");
            }

            var coM = Regex.Match(loginGolden, @"CHECKOFF=(\d+)");
            var clM = Regex.Match(loginGolden, @"CHECKLEN=(\d+)");
            if (coM.Success) Check("login checksum offset", coM.Groups[1].Value, LoginPacketLayout.ChecksumOffset.ToString());
            if (clM.Success) Check("login checksum length", clM.Groups[1].Value, LoginPacketLayout.ChecksumLength.ToString());

            // Header bitfield packing probes: HDR idXX_sizeYY = 2 raw LE bytes.
            foreach (Match hm in Regex.Matches(loginGolden, @"HDR id([0-9A-Fa-f]+)_size([0-9A-Fa-f]+)=([0-9A-Fa-f]{4})"))
            {
                int id = int.Parse(hm.Groups[1].Value, NumberStyles.HexNumber);
                int size = int.Parse(hm.Groups[2].Value, NumberStyles.HexNumber);
                ushort word = LoginPacket.PackIdSize(id, size);
                string raw = $"{(byte)(word & 0xFF):X2}{(byte)(word >> 8):X2}";
                Check($"login hdr id{hm.Groups[1].Value}/size{hm.Groups[2].Value}", hm.Groups[3].Value.ToUpperInvariant(), raw);
                Check($"login hdr id{hm.Groups[1].Value}/size{hm.Groups[2].Value} unpack", $"{id},{size}", $"{LoginPacket.IdOf(word)},{LoginPacket.SizeOf(word)}");
            }

            // Full-packet checksum over the oracle's deterministic byte pattern.
            var pkM = Regex.Match(loginGolden, @"PACKET=([0-9A-Fa-f]+)");
            var csM = Regex.Match(loginGolden, @"CHECKSUM=([0-9A-Fa-f]{2})");
            var agM = Regex.Match(loginGolden, @"CHECKSUM_AGAIN=([0-9A-Fa-f]{2}) match=(\d)");
            if (pkM.Success && csM.Success)
            {
                byte[] pkt = BytesFromHex(pkM.Groups[1].Value);
                Check("login packet size", LoginPacketLayout.SizeOf.ToString(), pkt.Length.ToString());
                Check("login ComputeChecksum", csM.Groups[1].Value.ToUpperInvariant(), LoginPacket.ComputeChecksum(pkt).ToString("X2"));
                Check("login VerifyChecksum (stored==recomputed)", "true", LoginPacket.VerifyChecksum(pkt).ToString().ToLowerInvariant());
                if (agM.Success)
                {
                    Check("login checksum stable after write-back", agM.Groups[1].Value.ToUpperInvariant(),
                          LoginPacket.ComputeChecksum(pkt).ToString("X2"));
                    Check("login match flag", agM.Groups[2].Value, LoginPacket.VerifyChecksum(pkt) ? "1" : "0");
                }
            }
        }

        Console.WriteLine($"\nRESULT: {_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    static uint U32(string hex) =>
        uint.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    static byte[] BytesFromHex(string hex)
    {
        byte[] b = new byte[hex.Length / 2];
        for (int i = 0; i < b.Length; i++)
            b[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return b;
    }

    static string Hex(byte[] b)
    {
        var sb = new System.Text.StringBuilder(b.Length * 2);
        foreach (byte x in b) sb.Append(x.ToString("X2"));
        return sb.ToString();
    }

    // Little-endian 4-byte words rendered as the oracle prints them (%08X per word).
    static string WordsHex(byte[] b)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i + 4 <= b.Length; i += 4)
        {
            uint w = (uint)(b[i] | (b[i + 1] << 8) | (b[i + 2] << 16) | (b[i + 3] << 24));
            sb.Append(w.ToString("X8"));
        }
        return sb.ToString();
    }

    static void Check(string name, string expected, string actual)
    {
        bool ok = expected == actual;
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {name}: expected {expected} actual {actual}");
    }
}
