// Dependency-free parity verifier for the login-server (lobby) wire model in XI.Login.
// Reads the oracle's login_lobby_golden.txt (produced by the compiled MSVC
// login_lobby_harness.cpp) and asserts every sizeof/offsetof/enum value, plus that the
// C# header codec + bit-field byte + deterministic struct fill reproduce the compiler's
// bytes. Any mismatch => exit code 1. This is the Phase-2 login go/no-go evidence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using XI.Login;

internal static class Program
{
    static int _pass, _fail;

    static int Main(string[] argv)
    {
        string goldenPath = argv.Length > 0
            ? argv[0]
            : @"H:\023-FFXI-server\csharp\oracle\login_lobby_golden.txt";

        if (!File.Exists(goldenPath))
        {
            Console.Error.WriteLine($"golden file not found: {goldenPath}");
            return 2;
        }

        // Parse KEY=VALUE on the first '=' into a dictionary.
        var g = new Dictionary<string, string>();
        foreach (var raw in File.ReadAllLines(goldenPath))
        {
            int eq = raw.IndexOf('=');
            if (eq <= 0) continue;
            g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // Build the full expected map from the C# constants. Any golden key that is not
        // covered here is a coverage gap and fails, so a new engine field can't slip past.
        var E = new Dictionary<string, string>();

        // Struct sizeof (decimal, oracle printed %zu).
        E["SIZE_packet_t"] = PacketLayout.SizeOf.ToString();
        E["SIZE_TC_OPERATION_MAKE"] = TcOperationMakeLayout.SizeOf.ToString();
        E["SIZE_lpkt_chr_info_sub2"] = ChrInfoSub2Layout.SizeOf.ToString();
        E["SIZE_lpkt_chr_info2"] = ChrInfo2Layout.SizeOf.ToString();
        E["SIZE_lpkt_world_name"] = WorldNameLayout.SizeOf.ToString();
        E["SIZE_lpkt_world_list"] = WorldListLayout.SizeOf.ToString();
        E["SIZE_lpkt_next_login"] = NextLoginLayout.SizeOf.ToString();
        E["SIZE_lpkt_deletechr"] = DeleteChrLayout.SizeOf.ToString();

        // packet_t header offsets.
        E["OFF_packet.packet_size"] = PacketLayout.OffPacketSize.ToString();
        E["OFF_packet.terminator"] = PacketLayout.OffTerminator.ToString();
        E["OFF_packet.command"] = PacketLayout.OffCommand.ToString();
        E["OFF_packet.identifer"] = PacketLayout.OffIdentifier.ToString();

        // TC_OPERATION_MAKE offsets (const int fields; read raw constant values).
        var t = typeof(TcOperationMakeLayout);
        string T(string f) => Convert.ToString((int)t.GetField(f).GetRawConstantValue(), CultureInfo.InvariantCulture);
        E["OFF_tcom.mon_no"] = T(nameof(TcOperationMakeLayout.OffMonNo));
        E["OFF_tcom.mjob_no"] = T(nameof(TcOperationMakeLayout.OffMjobNo));
        E["OFF_tcom.sjob_no"] = T(nameof(TcOperationMakeLayout.OffSjobNo));
        E["OFF_tcom.face_no"] = T(nameof(TcOperationMakeLayout.OffFaceNo));
        E["OFF_tcom.town_no"] = T(nameof(TcOperationMakeLayout.OffTownNo));
        E["OFF_tcom.gen_flag"] = T(nameof(TcOperationMakeLayout.OffGenFlag));
        E["OFF_tcom.hair_no"] = T(nameof(TcOperationMakeLayout.OffHairNo));
        E["OFF_tcom.size"] = T(nameof(TcOperationMakeLayout.OffSize));
        E["OFF_tcom.world_no"] = T(nameof(TcOperationMakeLayout.OffWorldNo));
        E["OFF_tcom.GrapIDTbl"] = T(nameof(TcOperationMakeLayout.OffGrapIdTbl));
        E["OFF_tcom.zone_no"] = T(nameof(TcOperationMakeLayout.OffZoneNo));
        E["OFF_tcom.mjob_level"] = T(nameof(TcOperationMakeLayout.OffMjobLevel));
        E["OFF_tcom.open_flag"] = T(nameof(TcOperationMakeLayout.OffOpenFlag));
        E["OFF_tcom.GMCallCounter"] = T(nameof(TcOperationMakeLayout.OffGmCallCounter));
        E["OFF_tcom.version"] = T(nameof(TcOperationMakeLayout.OffVersion));
        E["OFF_tcom.skill1"] = T(nameof(TcOperationMakeLayout.OffSkill1));
        E["OFF_tcom.zone_no2"] = T(nameof(TcOperationMakeLayout.OffZoneNo2));
        E["OFF_tcom.rank_sd"] = T(nameof(TcOperationMakeLayout.OffRankSd));
        E["OFF_tcom.rank_bs"] = T(nameof(TcOperationMakeLayout.OffRankBs));
        E["OFF_tcom.rank_ws"] = T(nameof(TcOperationMakeLayout.OffRankWs));
        E["OFF_tcom.ErrCounter"] = T(nameof(TcOperationMakeLayout.OffErrCounter));
        E["OFF_tcom.fame_sd"] = T(nameof(TcOperationMakeLayout.OffFameSd));
        E["OFF_tcom.fame_bs"] = T(nameof(TcOperationMakeLayout.OffFameBs));
        E["OFF_tcom.fame_ws"] = T(nameof(TcOperationMakeLayout.OffFameWs));
        E["OFF_tcom.fame_dark"] = T(nameof(TcOperationMakeLayout.OffFameDark));
        E["OFF_tcom.PlayTime"] = T(nameof(TcOperationMakeLayout.OffPlayTime));
        E["OFF_tcom.get_job_flag"] = T(nameof(TcOperationMakeLayout.OffGetJobFlag));
        E["OFF_tcom.job_lev"] = T(nameof(TcOperationMakeLayout.OffJobLev));
        E["OFF_tcom.FirstLoginDate"] = T(nameof(TcOperationMakeLayout.OffFirstLoginDate));
        E["OFF_tcom.Gold"] = T(nameof(TcOperationMakeLayout.OffGold));
        E["OFF_tcom.skill2"] = T(nameof(TcOperationMakeLayout.OffSkill2));
        E["OFF_tcom.skill3"] = T(nameof(TcOperationMakeLayout.OffSkill3));
        E["OFF_tcom.skill4"] = T(nameof(TcOperationMakeLayout.OffSkill4));
        E["OFF_tcom.skill5"] = T(nameof(TcOperationMakeLayout.OffSkill5));
        E["OFF_tcom.ChatCounter"] = T(nameof(TcOperationMakeLayout.OffChatCounter));
        E["OFF_tcom.PartyCounter"] = T(nameof(TcOperationMakeLayout.OffPartyCounter));
        E["OFF_tcom.skill6"] = T(nameof(TcOperationMakeLayout.OffSkill6));
        E["OFF_tcom.skill7"] = T(nameof(TcOperationMakeLayout.OffSkill7));
        E["OFF_tcom.skill8"] = T(nameof(TcOperationMakeLayout.OffSkill8));
        E["OFF_tcom.skill9"] = T(nameof(TcOperationMakeLayout.OffSkill9));

        // lpkt_chr_info_sub2 offsets.
        E["OFF_cis.ffxi_id"] = ChrInfoSub2Layout.OffFfxiId.ToString();
        E["OFF_cis.ffxi_id_world"] = ChrInfoSub2Layout.OffFfxiIdWorld.ToString();
        E["OFF_cis.worldid"] = ChrInfoSub2Layout.OffWorldId.ToString();
        E["OFF_cis.status"] = ChrInfoSub2Layout.OffStatus.ToString();
        E["OFF_cis.ffxi_id_world_tbl"] = ChrInfoSub2Layout.OffFfxiIdWorldTbl.ToString();
        E["OFF_cis.character_name"] = ChrInfoSub2Layout.OffCharacterName.ToString();
        E["OFF_cis.world_name"] = ChrInfoSub2Layout.OffWorldName.ToString();
        E["OFF_cis.character_info"] = ChrInfoSub2Layout.OffCharacterInfo.ToString();

        // lpkt_chr_info2 offsets (__hdr == offsetof(packet_size) == 0, the header start).
        E["OFF_chr2.__hdr"] = "0";
        E["OFF_chr2.characters"] = ChrInfo2Layout.OffCharacters.ToString();
        E["OFF_chr2.character_info"] = ChrInfo2Layout.OffCharacterInfo.ToString();

        // lpkt_world_name / lpkt_world_list.
        E["OFF_wno.no"] = WorldNameLayout.OffNo.ToString();
        E["OFF_wno.name"] = WorldNameLayout.OffName.ToString();
        E["OFF_wlist.__hdr"] = "0";
        E["OFF_wlist.sumofworld"] = WorldListLayout.OffSumOfWorld.ToString();
        E["OFF_wlist.world_name"] = WorldListLayout.OffWorldName.ToString();

        // lpkt_next_login.
        E["OFF_next.__hdr"] = "0";
        E["OFF_next.ffxi_id"] = NextLoginLayout.OffFfxiId.ToString();
        E["OFF_next.ffxi_id_world"] = NextLoginLayout.OffFfxiIdWorld.ToString();
        E["OFF_next.character_name"] = NextLoginLayout.OffCharacterName.ToString();
        E["OFF_next.server_id"] = NextLoginLayout.OffServerId.ToString();
        E["OFF_next.server_ip"] = NextLoginLayout.OffServerIp.ToString();
        E["OFF_next.server_port"] = NextLoginLayout.OffServerPort.ToString();
        E["OFF_next.cache_ip"] = NextLoginLayout.OffCacheIp.ToString();
        E["OFF_next.cache_port"] = NextLoginLayout.OffCachePort.ToString();

        // lpkt_deletechr.
        E["OFF_delchr.__hdr"] = "0";
        E["OFF_delchr.ffxi_id"] = DeleteChrLayout.OffFfxiId.ToString();
        E["OFF_delchr.ffxi_id_world"] = DeleteChrLayout.OffFfxiIdWorld.ToString();
        E["OFF_delchr.passwd"] = DeleteChrLayout.OffPasswd.ToString();

        // Terminator (oracle %08X).
        E["TERM"] = LoginPackets.Terminator.ToString("X8");

        // Bit-field byte: renamef=1, race_change=1, unused=0 => 0b00000011 == 0x03.
        byte bfByte = (byte)(ChrInfoSub2Layout.RenamefMask | ChrInfoSub2Layout.RaceChangeMask);
        E["BITFIELD_byte"] = bfByte.ToString("X2");
        E["BITFIELD_val"] = bfByte.ToString("X2");
        E["BITFIELD_off"] = ChrInfoSub2Layout.OffBitfield.ToString();
        E["BITFIELD_off_derived"] = "1";
        E["BITFIELD_adjacent_tbl"] = (ChrInfoSub2Layout.OffBitfield + 1 == ChrInfoSub2Layout.OffFfxiIdWorldTbl) ? "1" : "0";
        E["BITFIELD_tbl_val"] = "5A";

        // Enum numeric values (decimal, oracle %d).
        void Enum(string k, long v) => E[k] = v.ToString(CultureInfo.InvariantCulture);
        Enum("login_cmd.LOGIN_NOOP", (long)LoginCmd.LoginNoop);
        Enum("login_cmd.LOGIN_ATTEMPT", (long)LoginCmd.LoginAttempt);
        Enum("login_cmd.LOGIN_CREATE", (long)LoginCmd.LoginCreate);
        Enum("login_cmd.LOGIN_CHANGE_PASSWORD", (long)LoginCmd.LoginChangePassword);
        Enum("login_cmd.LOGIN_CREATE_TOTP", (long)LoginCmd.LoginCreateTotp);
        Enum("login_cmd.LOGIN_REMOVE_TOTP", (long)LoginCmd.LoginRemoveTotp);
        Enum("login_cmd.LOGIN_REGENERATE_RECOVERY", (long)LoginCmd.LoginRegenerateRecovery);
        Enum("login_cmd.LOGIN_VERIFY_TOTP", (long)LoginCmd.LoginVerifyTotp);
        Enum("login_result.LOGIN_FAIL", (long)LoginResult.LoginFail);
        Enum("login_result.LOGIN_SUCCESS", (long)LoginResult.LoginSuccess);
        Enum("login_result.LOGIN_ERROR", (long)LoginResult.LoginError);
        Enum("login_result.LOGIN_SUCCESS_CREATE", (long)LoginResult.LoginSuccessCreate);
        Enum("login_result.LOGIN_ERROR_CREATE_TAKEN", (long)LoginResult.LoginErrorCreateTaken);
        Enum("login_result.LOGIN_REQUEST_NEW_PASSWORD", (long)LoginResult.LoginRequestNewPassword);
        Enum("login_result.LOGIN_SUCCESS_CHANGE_PASSWORD", (long)LoginResult.LoginSuccessChangePassword);
        Enum("login_result.LOGIN_ERROR_CHANGE_PASSWORD", (long)LoginResult.LoginErrorChangePassword);
        Enum("login_result.LOGIN_ERROR_CREATE_DISABLED", (long)LoginResult.LoginErrorCreateDisabled);
        Enum("login_result.LOGIN_ERROR_CREATE", (long)LoginResult.LoginErrorCreate);
        Enum("login_result.LOGIN_ERROR_ALREADY_LOGGED_IN", (long)LoginResult.LoginErrorAlreadyLoggedIn);
        Enum("login_result.LOGIN_ERROR_VERSION_UNSUPPORTED", (long)LoginResult.LoginErrorVersionUnsupported);
        Enum("login_result.LOGIN_SUCCESS_CREATE_TOTP", (long)LoginResult.LoginSuccessCreateTotp);
        Enum("login_result.LOGIN_SUCCESS_VERIFY_TOTP", (long)LoginResult.LoginSuccessVerifyTotp);
        Enum("login_result.LOGIN_SUCCESS_REMOVE_TOTP", (long)LoginResult.LoginSuccessRemoveTotp);
        Enum("login_result.LOGIN_ERROR_TRUST_TOKEN_INVALID", (long)LoginResult.LoginErrorTrustTokenInvalid);
        Enum("acct_status.NORMAL", (long)AccountStatusCode.Normal);
        Enum("acct_status.BANNED", (long)AccountStatusCode.Banned);
        Enum("acct_priv.USER", (long)AccountPrivilegeCode.User);
        Enum("acct_priv.ADMIN", (long)AccountPrivilegeCode.Admin);
        Enum("acct_priv.ROOT", (long)AccountPrivilegeCode.Root);
        Enum("err.UNABLE_TO_CONNECT_TO_WORLD_SERVER", (long)ErrorCode.UnableToConnectToWorldServer);
        Enum("err.CHARACTER_NAME_UNAVAILABLE", (long)ErrorCode.CharacterNameUnavailable);
        Enum("err.CHARACTER_ALREADY_LOGGED_IN", (long)ErrorCode.CharacterAlreadyLoggedIn);
        Enum("err.WORLD_IS_FULL", (long)ErrorCode.WorldIsFull);
        Enum("err.FAILED_TO_REGISTER_WITH_THE_NAME_SERVER", (long)ErrorCode.FailedToRegisterWithTheNameServer);
        Enum("err.CHARACTERS_PARAMETERS_ARE_INCORRECT", (long)ErrorCode.CharactersParametersAreIncorrect);
        Enum("err.GAMES_DATA_HAS_BEEN_UPDATED", (long)ErrorCode.GamesDataHasBeenUpdated);
        Enum("err.COULD_NOT_CONNECT_TO_LOBBY_SERVER", (long)ErrorCode.CouldNotConnectToLobbyServer);
        Enum("exp.BASE_GAME", (long)ExpansionDisplay.BaseGame);
        Enum("exp.SEEKERS_OF_ADOULIN", (long)ExpansionDisplay.SeekersOfAdoulin);
        Enum("feat.SECURE_TOKEN", (long)FeatureDisplay.SecureToken);
        Enum("feat.MOG_WARDROBE_3", (long)FeatureDisplay.MogWardrobe3);

        Console.WriteLine("== Login-server (lobby) wire-model parity: C# XI.Login vs compiler oracle ==");

        // Assert every golden key is mapped (coverage) and matches.
        foreach (var kv in g)
        {
            if (kv.Key.StartsWith("CIS_", StringComparison.Ordinal)) continue; // handled by the fill probe
            if (!E.TryGetValue(kv.Key, out string exp))
            {
                Check($"COVERAGE {kv.Key}", "mapped", "MISSING");
                continue;
            }
            Check(kv.Key, exp, kv.Value);
        }

        // CIS_SIZE must equal the C# struct size; CIS_PATTERN must equal the deterministic fill.
        if (g.TryGetValue("CIS_SIZE", out string cisSize))
            Check("CIS_SIZE == ChrInfoSub2Layout.SizeOf", cisSize, ChrInfoSub2Layout.SizeOf.ToString());
        if (g.TryGetValue("CIS_PATTERN", out string cisPat))
        {
            byte[] buf = new byte[ChrInfoSub2Layout.SizeOf];
            for (int i = 0; i < buf.Length; i++) buf[i] = unchecked((byte)(i * 7 + 3));
            var sb = new System.Text.StringBuilder(buf.Length * 2);
            foreach (byte x in buf) sb.Append(x.ToString("X2"));
            Check("CIS_PATTERN deterministic fill", cisPat.ToUpperInvariant(), sb.ToString());
        }

        // Header codec round-trip through the real C# PacketHeader + terminator helpers.
        {
            byte[] pkt = new byte[PacketLayout.SizeOf];
            var hdr = new PacketHeader(0x00480020u, LoginPackets.GetTerminator(), 0x0000001Cu);
            hdr.Write(pkt);
            Check("hdr packet_size @0", "20 00 48 00", BytesHex(pkt, PacketLayout.OffPacketSize, 4));
            Check("hdr terminator @4", "49 58 46 46", BytesHex(pkt, PacketLayout.OffTerminator, 4));
            Check("hdr command @8", "1C 00 00 00", BytesHex(pkt, PacketLayout.OffCommand, 4));
            var back = PacketHeader.Read(pkt);
            Check("hdr roundtrip size", hdr.PacketSize.ToString(), back.PacketSize.ToString());
            Check("hdr roundtrip terminator", LoginPackets.Terminator.ToString("X8"), back.Terminator.ToString("X8"));
            Check("hdr roundtrip command", hdr.Command.ToString(), back.Command.ToString());

            // CopyHashIntoPacket / ClearIdentifier act on the 16-byte identifier at offset 12.
            byte[] hash = new byte[16];
            for (int i = 0; i < 16; i++) hash[i] = (byte)(0xA0 + i);
            LoginPackets.CopyHashIntoPacket(pkt, hash);
            Check("copyHash identifer @12", BytesHex(hash, 0, 16), BytesHex(pkt, PacketLayout.OffIdentifier, 16));
            LoginPackets.ClearIdentifier(pkt);
            Check("clearIdentifier zeroes identifer", "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00",
                  BytesHex(pkt, PacketLayout.OffIdentifier, 16));
        }

        // Second golden file: the compiled login_helpers.cpp oracle (error packet + bitmasks
        // + pure predicates). Skipped if absent, like the other sibling vectors.
        VerifyLoginHelpers(Path.GetDirectoryName(goldenPath) ?? "");

        // Third golden file: the compiled otp_helpers.cpp oracle (base32/TOTP/trust-token), which
        // links the engine's vendored OpenSSL so HMAC-SHA1/SHA256 bytes are compiler-derived.
        VerifyLoginOtp(Path.GetDirectoryName(goldenPath) ?? "");

        Console.WriteLine($"\nRESULT: {_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    // Asserts the deterministic C# port in LoginHelpers against login_helpers_golden.txt,
    // produced by the compiled login_helpers_harness.cpp (links the engine's md52.cpp, so the
    // error packet's MD5 is compiler-derived). Uses the same Check/_pass/_fail counters.
    static void VerifyLoginHelpers(string dir)
    {
        string path = Path.Combine(dir, "login_helpers_golden.txt");
        if (!File.Exists(path)) return;

        Console.WriteLine("\n== Login helpers parity: C# LoginHelpers vs compiled md52 oracle ==");

        // Latin1 (28591) decodes every byte 0x00-0xFF losslessly, so a golden line that echoes
        // a high byte (e.g. CMP [caf<E9>]) parses without a UTF-8 exception.
        string[] lines = File.ReadAllLines(path, Encoding.Latin1);

        // Order-pinned replicas of the harness input vectors (the golden prints only the result
        // for MALFORMED / VALPURE, and echoes the name for CMP, so inputs must be re-derived).
        (string s, int max)[] malCases =
        {
            ("", 16), ("ab", 16), ("abc", 16), ("abcdefghijklmno", 15),
            ("abcdefghijklmnop", 15), ("abcdefghijklmnop", 16), ("a\tb", 16),
            ("ab", 16), ("ok NAME", 16), ("", 16),
        };
        string[] valNames = { "ab", "abc", "abcdefghijklmno", "abcdefghijklmnop", "Bob1", "bob-smith", "", "A B" };
        byte[][] cmpNames =
        {
            E("Bob"), E("bob-smith"), E("bob_smith"), E("BoB"), E("A B"), E("123"),
            new byte[]{0x63,0x61,0x66,0xE9}, // "caf<E9>"
        };
        string[] cmpLabel = { "Bob", "bob-smith", "bob_smith", "BoB", "A B", "123", "caf\u00E9" };

        int mi = 0, vi = 0, ci = 0;
        foreach (string line in lines)
        {
            if (line.StartsWith("MD5_ABC=", StringComparison.Ordinal))
            {
                byte[] d = System.Security.Cryptography.MD5.HashData(new byte[]{(byte)'a',(byte)'b',(byte)'c'});
                Check("BCL MD5(\"abc\") == engine md5", line.Substring(8).ToUpperInvariant(), NoSpaceHex(d));
            }
            else if (line.StartsWith("ERRPACKET code=", StringComparison.Ordinal))
            {
                int sp = line.IndexOf(' ', "ERRPACKET code=".Length);
                int code = int.Parse(line.Substring("ERRPACKET code=".Length, sp - "ERRPACKET code=".Length));
                string exp = line.Substring(sp + 1).Trim().ToUpperInvariant();
                Check($"generateErrorMessage({code})", exp, NoSpaceHex(LoginHelpers.GenerateErrorMessage((ushort)code)));
            }
            else if (line.StartsWith("EXPB ", StringComparison.Ordinal))
            {
                int combo = int.Parse(Val(line, "combo="));
                string expMask = Val(line, "mask=");
                // build enabled set from combo bits using the harness expBits order
                bool bRoz  = (combo & (1<<5))!=0; // RISE_OF_ZILART  value 0x0002 at expBits[5]
                bool bCop  = (combo & (1<<3))!=0; // CHAINS_OF_PROMATHIA expBits[3]
                bool bToau = (combo & (1<<8))!=0; // TREASURES_OF_AHT_URGHAN expBits[8]
                bool bWotg = (combo & (1<<10))!=0;// WINGS_OF_THE_GODDESS expBits[10]
                bool bAc   = (combo & (1<<0))!=0; // A_CRYSTALLINE_PROPHECY expBits[0]
                bool bAhk  = (combo & (1<<1))!=0; // A_MOOGLE_KUPOD_ETAT expBits[1]
                bool bAsh  = (combo & (1<<2))!=0; // A_SHANTOTTO_ASCENSION expBits[2]
                bool bVoa  = (combo & (1<<9))!=0; // VISIONS_OF_ABYSSEA expBits[9]
                bool bSoa  = (combo & (1<<6))!=0; // SCARS_OF_ABYSSEA expBits[6]
                bool bHoa  = (combo & (1<<4))!=0; // HEROES_OF_ABYSSEA expBits[4]
                bool bSea  = (combo & (1<<7))!=0; // SEEKERS_OF_ADOULIN expBits[7]
                ushort got = LoginHelpers.GenerateExpansionBitmask(bRoz,bCop,bToau,bWotg,bAc,bAhk,bAsh,bVoa,bSoa,bHoa,bSea);
                Check($"expansionBitmask combo{combo}", expMask, got.ToString());
            }
            else if (line.StartsWith("FEATB ", StringComparison.Ordinal))
            {
                int combo = int.Parse(Val(line, "combo="));
                int otp = int.Parse(Val(line, "otp="));
                string expMask = Val(line, "mask=");
                bool w3=(combo&(1<<0))!=0, w4=(combo&(1<<1))!=0, w5=(combo&(1<<2))!=0,
                     w6=(combo&(1<<3))!=0, w7=(combo&(1<<4))!=0, w8=(combo&(1<<5))!=0;
                ushort got = LoginHelpers.GenerateFeatureBitmask(w3,w4,w5,w6,w7,w8, otp!=0);
                Check($"featureBitmask combo{combo} otp{otp}", expMask, got.ToString());
            }
            else if (line.StartsWith("MALFORMED ", StringComparison.Ordinal))
            {
                int expRes = int.Parse(Val(line, "res="));
                if (mi < malCases.Length)
                {
                    var (s, max) = malCases[mi];
                    int got = LoginHelpers.IsStringMalformed(s, max) ? 1 : 0;
                    Check($"isStringMalformed[{mi}] \"{Esc(s)}\" max{max}", expRes.ToString(), got.ToString());
                }
                mi++;
            }
            else if (line.StartsWith("VALPURE ", StringComparison.Ordinal))
            {
                int expRes = int.Parse(Val(line, "res="));
                if (vi < valNames.Length)
                {
                    int got = (int)LoginHelpers.ValidateNamePure(valNames[vi]);
                    Check($"validateNamePure[{vi}] \"{Esc(valNames[vi])}\"", expRes.ToString(), got.ToString());
                }
                vi++;
            }
            else if (line.StartsWith("CMP ", StringComparison.Ordinal))
            {
                int eq = line.IndexOf('=');
                string exp = line.Substring(eq + 1).Trim().ToUpperInvariant();
                if (ci < cmpNames.Length)
                {
                    byte[] got = LoginHelpers.ComparableName(cmpNames[ci]);
                    Check($"comparableName \"{cmpLabel[ci]}\"", exp, NoSpaceHex(got));
                }
                ci++;
            }
        }
    }

    // Asserts the deterministic C# port in LoginOtpHelpers against login_otp_golden.txt, produced
    // by the compiled login_otp_harness.cpp which links the engine's vendored OpenSSL (HMAC-SHA1 /
    // SHA256 -> TOTP codes + trust-token hashes are compiler-derived, not hand-reasoned). Inputs are
    // hex-encoded in the golden; decoding via Latin1 keeps high bytes (e.g. the 0xE9 base32/isxdigit
    // traps) byte-exact. Same Check/_pass/_fail counters as the other gates.
    static void VerifyLoginOtp(string dir)
    {
        string path = Path.Combine(dir, "login_otp_golden.txt");
        if (!File.Exists(path)) return;

        Console.WriteLine("\n== Login OTP parity: C# LoginOtpHelpers vs compiled OpenSSL oracle ==");

        string[] lines = File.ReadAllLines(path, Encoding.Latin1);

        string lastB32In = null, lastHexIn = null, lastTrustIn = null, lastShaIn = null;
        string tB32 = null; ulong tEpoch = 0; int tDigits = 6, tPeriod = 30;
        int b32i = 0, hexi = 0, totpi = 0, trusti = 0, shai = 0;

        foreach (string line in lines)
        {
            if (line.StartsWith("B32IN=", StringComparison.Ordinal))
            {
                lastB32In = Latin1(FromHex(line.Substring(6)));
            }
            else if (line.StartsWith("B32OUT=", StringComparison.Ordinal))
            {
                string exp = line.Substring(7);
                string nm = $"base32Decode[{b32i}] \"{Esc(lastB32In)}\"";
                if (exp == "THROW")
                {
                    try { LoginOtpHelpers.Base32Decode(lastB32In); Check(nm, "THROW", "RETURNED"); }
                    catch (ArgumentException) { Check(nm, "THROW", "THROW"); }
                }
                else
                {
                    byte[] got = LoginOtpHelpers.Base32Decode(lastB32In);
                    Check(nm, exp, NoSpaceHex(got));
                }
                b32i++;
            }
            else if (line.StartsWith("HEXIN=", StringComparison.Ordinal))
            {
                lastHexIn = line.Substring(6);
            }
            else if (line.StartsWith("HEXOUT=", StringComparison.Ordinal))
            {
                string exp = line.Substring(7);
                Check($"toHexString[{hexi}]", exp, LoginOtpHelpers.ToHexString(FromHex(lastHexIn)));
                hexi++;
            }
            else if (line.StartsWith("TOTPIN_B32=", StringComparison.Ordinal))
            {
                tB32 = Latin1(FromHex(line.Substring(11)));
            }
            else if (line.StartsWith("TOTPIN_EPOCH=", StringComparison.Ordinal))
            {
                tEpoch = ulong.Parse(line.Substring(13), CultureInfo.InvariantCulture);
            }
            else if (line.StartsWith("TOTPIN_DIGITS=", StringComparison.Ordinal))
            {
                tDigits = int.Parse(line.Substring(14), CultureInfo.InvariantCulture);
            }
            else if (line.StartsWith("TOTPIN_PERIOD=", StringComparison.Ordinal))
            {
                tPeriod = int.Parse(line.Substring(14), CultureInfo.InvariantCulture);
            }
            else if (line.StartsWith("TOTPCODE=", StringComparison.Ordinal))
            {
                string exp = line.Substring(9);
                string got = LoginOtpHelpers.GenerateTotp(tB32, tEpoch, tDigits, tPeriod);
                Check($"generateTOTP[{totpi}] d{tDigits} p{tPeriod} e{tEpoch}", exp, got);
                totpi++;
            }
            else if (line.StartsWith("TRUSTIN=", StringComparison.Ordinal))
            {
                lastTrustIn = Latin1(FromHex(line.Substring(8)));
            }
            else if (line.StartsWith("TRUSTFMT ", StringComparison.Ordinal))
            {
                int exp = int.Parse(Val(line, "res="), CultureInfo.InvariantCulture);
                int got = LoginOtpHelpers.IsValidTrustTokenFormat(lastTrustIn) ? 1 : 0;
                Check($"isValidTrustTokenFormat[{trusti}] len{lastTrustIn.Length}", exp.ToString(), got.ToString());
                trusti++;
            }
            else if (line.StartsWith("SHAIN=", StringComparison.Ordinal))
            {
                lastShaIn = Latin1(FromHex(line.Substring(6)));
            }
            else if (line.StartsWith("SHAOUT=", StringComparison.Ordinal))
            {
                string exp = line.Substring(7);
                Check($"hashTrustToken[{shai}]", exp, LoginOtpHelpers.HashTrustToken(lastShaIn));
                shai++;
            }
        }
    }

    static byte[] FromHex(string h)
    {
        var b = new byte[h.Length / 2];
        for (int i = 0; i < b.Length; i++)
            b[i] = Convert.ToByte(h.Substring(i * 2, 2), 16);
        return b;
    }

    static string Latin1(byte[] b) => Encoding.Latin1.GetString(b);

    static string Val(string line, string key)
    {
        int i = line.IndexOf(key, StringComparison.Ordinal);
        int j = i + key.Length;
        while (j < line.Length && line[j] != ' ' && line[j] != '\r' && line[j] != '\n') j++;
        return line.Substring(i + key.Length, j - (i + key.Length));
    }

    static byte[] E(string s) { var b = new byte[s.Length]; for (int i = 0; i < s.Length; i++) b[i] = unchecked((byte)s[i]); return b; }
    static string Esc(string s) { var sb = new StringBuilder(); foreach (char c in s) sb.Append(c < 0x20 ? $"\\x{(int)c:X2}" : c.ToString()); return sb.ToString(); }

    static string NoSpaceHex(byte[] b)
    {
        var sb = new StringBuilder(b.Length * 2);
        foreach (byte x in b) sb.Append(x.ToString("X2"));
        return sb.ToString();
    }

    static string BytesHex(byte[] b, int off, int len)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < len; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(b[off + i].ToString("X2"));
        }
        return sb.ToString();
    }

    static void Check(string name, string expected, string actual)
    {
        bool ok = string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {name}: expected {expected} actual {actual}");
    }
}
