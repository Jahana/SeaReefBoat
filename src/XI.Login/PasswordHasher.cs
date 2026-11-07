// bcrypt as used by the engine's BCrypt::generateHash / validatePassword (libbcrypt: $2a$,
// work factor 12) and the isBcryptHash shape check from auth_session.cpp.

using System;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Generators;

namespace XI.Login
{
    public static class PasswordHasher
    {
        public const int WorkFactor = 12;

        public static bool IsBcryptHash(string passHash) =>
            passHash.Length == 60 &&
            passHash[0] == '$' &&
            passHash[1] == '2' &&
            (passHash[2] == 'a' || passHash[2] == 'b' || passHash[2] == 'y' || passHash[2] == 'x') &&
            passHash[3] == '$';

        public static string Generate(string password, int workFactor = WorkFactor)
        {
            var salt = new byte[16];
            RandomNumberGenerator.Fill(salt);
            return OpenBsdBCrypt.Generate("2a", password.ToCharArray(), salt, workFactor);
        }

        public static bool Validate(string password, string hash)
        {
            try { return OpenBsdBCrypt.CheckPassword(hash, password.ToCharArray()); }
            catch (ArgumentException) { return false; } // e.g. $2x$ (unsupported by the BC port)
        }
    }
}
