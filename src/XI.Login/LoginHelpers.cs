// C# port of the deterministic helpers from src/login/login_helpers.cpp (loginHelpers).
// These are the offline-oracle-verifiable pieces: they emit exact bytes / masks the login
// server sends, so tests/XI.Verify.Login asserts them against the compiled engine oracle
// (csharp/oracle/login_helpers_harness.cpp -> login_helpers_golden.txt, which links the
// real src/common/md52.cpp so the error packet's MD5 is compiler-derived, not hand-done).
//
// NOT ported here (live/infra boundary, deferred): the DB name-in-use + entity-name checks
// in validateCharacterName, the Lua BANNED_WORDS_LIST check, isVulgarName + res/badnames.bin
// (MD5-hashed name filter), and every db::preparedStmt path. Only validateCharacterName's
// PURE prefix (charset + length) is ported; see ValidateNamePure.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace XI.Login
{
    // Outcome codes for the pure prefix of validateCharacterName (login_helpers.cpp).
    // Mirrors the engine's early returns: 0 = prefix OK (DB/Lua/filter checks would follow),
    // 1 = invalid characters, 2 = invalid length.
    public enum NamePureResult
    {
        Ok = 0,
        InvalidCharacters = 1,
        InvalidLength = 2,
    }

    public static class LoginHelpers
    {
        // Lobby error packet (S2C_0x0004) size. Verbatim from generateErrorMessage's memset.
        public const int ErrorMessagePacketSize = 0x24; // 36

        // std::isalpha in the engine's "C" locale == ASCII A-Z/a-z only. The C# char overload
        // is Unicode-aware, so it must NOT be used here.
        private static bool IsAsciiAlpha(byte c) =>
            (c >= (byte)'A' && c <= (byte)'Z') || (c >= (byte)'a' && c <= (byte)'z');

        // generateErrorMessage: builds the fixed 0x24-byte lobby error packet.
        //   [0]=0x24 size; [4..7]="IXFF"; [8]=0x04 command; [28]=0x10 (client-unused constant);
        //   [32..33]=errorCode LE; [12..27]=md5(packet[0..0x24]).
        // Returns the finished packet (the C++ version mutates a caller buffer in place).
        public static byte[] GenerateErrorMessage(ushort errorCode)
        {
            byte[] packet = new byte[ErrorMessagePacketSize];

            packet[0] = 0x24; // size

            packet[4] = 0x49; // I
            packet[5] = 0x58; // X
            packet[6] = 0x46; // F
            packet[7] = 0x46; // F

            packet[8] = 0x04; // result

            packet[28] = 0x10; // never referenced by the client; always observed 0x10.

            // ref<uint16>(packet, 32) = errorCode  (little-endian, matches x86 MSVC)
            packet[32] = (byte)(errorCode & 0xFF);
            packet[33] = (byte)((errorCode >> 8) & 0xFF);

            // md5 over the full 0x24-byte buffer (the [12..27] hash slot is still zero here,
            // exactly as the memset leaves it before memcpy) -> write the digest into [12..27].
            byte[] hash = MD5.HashData(packet);
            Array.Copy(hash, 0, packet, 12, 16);
            return packet;
        }

        // generateExpansionBitmask (login_helpers.cpp): main-menu expansion bits. BASE_GAME is
        // always set; each expansion bit is OR'd only when its login.<NAME> setting is true.
        public static ushort GenerateExpansionBitmask(
            bool riseOfZilart, bool chainsOfPromathia, bool treasuresOfAhtUrghan,
            bool wingsOfTheGoddess, bool aCrystallineProphecy, bool aMoogleKupodEtat,
            bool aShantottoAscension, bool visionsOfAbyssea, bool scarsOfAbyssea,
            bool heroesOfAbyssea, bool seekersOfAdoulin)
        {
            ushort mask = (ushort)ExpansionDisplay.BaseGame;
            if (riseOfZilart) mask |= (ushort)ExpansionDisplay.RiseOfZilart;
            if (chainsOfPromathia) mask |= (ushort)ExpansionDisplay.ChainsOfPromathia;
            if (treasuresOfAhtUrghan) mask |= (ushort)ExpansionDisplay.TreasuresOfAhtUrgan;
            if (wingsOfTheGoddess) mask |= (ushort)ExpansionDisplay.WingsOfTheGoddess;
            if (aCrystallineProphecy) mask |= (ushort)ExpansionDisplay.ACrystallineProphecy;
            if (aMoogleKupodEtat) mask |= (ushort)ExpansionDisplay.AMoogleKupodEtat;
            if (aShantottoAscension) mask |= (ushort)ExpansionDisplay.AShantottoAscension;
            if (visionsOfAbyssea) mask |= (ushort)ExpansionDisplay.VisionsOfAbyssea;
            if (scarsOfAbyssea) mask |= (ushort)ExpansionDisplay.ScarsOfAbyssea;
            if (heroesOfAbyssea) mask |= (ushort)ExpansionDisplay.HeroesOfAbyssea;
            if (seekersOfAdoulin) mask |= (ushort)ExpansionDisplay.SeekersOfAdoulin;
            return mask;
        }

        // generateFeatureBitmask (login_helpers.cpp): main-menu feature bits. MOG_WARDROBE_3..8
        // OR'd when enabled; SECURE_TOKEN OR'd when the account needs an OTP.
        public static ushort GenerateFeatureBitmask(
            bool mogWardrobe3, bool mogWardrobe4, bool mogWardrobe5,
            bool mogWardrobe6, bool mogWardrobe7, bool mogWardrobe8, bool needsOTP)
        {
            ushort mask = 0;
            if (mogWardrobe3) mask |= (ushort)FeatureDisplay.MogWardrobe3;
            if (mogWardrobe4) mask |= (ushort)FeatureDisplay.MogWardrobe4;
            if (mogWardrobe5) mask |= (ushort)FeatureDisplay.MogWardrobe5;
            if (mogWardrobe6) mask |= (ushort)FeatureDisplay.MogWardrobe6;
            if (mogWardrobe7) mask |= (ushort)FeatureDisplay.MogWardrobe7;
            if (mogWardrobe8) mask |= (ushort)FeatureDisplay.MogWardrobe8;
            if (needsOTP) mask |= (ushort)FeatureDisplay.SecureToken;
            return mask;
        }

        // isStringMalformed (login_helpers.cpp): empty, longer than max_length, or containing
        // any byte < 0x20 (unprintable).
        public static bool IsStringMalformed(string str, int maxLength)
        {
            bool isEmpty = string.IsNullOrEmpty(str);
            bool isTooLong = str.Length > maxLength;
            bool hasInvalidChar = false;
            foreach (char c in str)
            {
                if (c < 0x20) { hasInvalidChar = true; break; }
            }
            return isEmpty || isTooLong || hasInvalidChar;
        }

        // comparableName (login_helpers.cpp, file-local): the canonical form used to compare a
        // proposed character name against entity names. Drops '-' and '_'; ASCII a-z -> A-Z.
        // Operates on bytes (the C++ reads char), so a high byte (e.g. 0xE9) is kept unchanged.
        public static byte[] ComparableName(byte[] name)
        {
            var outBytes = new List<byte>(name.Length);
            foreach (byte ch in name)
            {
                if (ch == (byte)'-' || ch == (byte)'_') continue;
                byte c = ch;
                if (c >= (byte)'a' && c <= (byte)'z')
                    c -= (byte)('a' - 'A');
                outBytes.Add(c);
            }
            return outBytes.ToArray();
        }

        public static string ComparableName(string name) =>
            Encoding.Latin1.GetString(ComparableName(Encoding.Latin1.GetBytes(name)));

        // validateCharacterName PURE prefix (login_helpers.cpp): charset then length. The
        // engine returns the first matching message string; here the equivalent outcome code.
        // Everything after this (DB name-in-use, mob/npc entity check, Lua BANNED_WORDS_LIST,
        // isVulgarName/badnames.bin) is live-infra and NOT ported.
        public static NamePureResult ValidateNamePure(string name)
        {
            foreach (char c in name)
            {
                byte b = unchecked((byte)c);
                if (!IsAsciiAlpha(b))
                    return NamePureResult.InvalidCharacters;
            }
            if (name.Length < 3 || name.Length > 15)
                return NamePureResult.InvalidLength;
            return NamePureResult.Ok;
        }
    }
}
