// DB-backed half of otp_helpers.h (otpHelpers::*): account OTP state, TOTP secrets/recovery
// codes, replay-protected TOTP validation, and trust tokens. The pure primitives
// (base32, generateTOTP, hashTrustToken, ...) live in LoginOtpHelpers and are verified there.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using XI.Persistence;

namespace XI.Login
{
    public sealed class OtpStore
    {
        private const string Base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        private readonly IDatabase _db;
        private readonly TimeProvider _time;
        private readonly object _lock = new object();
        // last accepted time step per account, so a code can't be used twice (RFC 6238 5.2)
        private readonly Dictionary<uint, ulong> _lastAcceptedStep = new();

        public OtpStore(IDatabase db, TimeProvider time = null)
        {
            _db = db;
            _time = time ?? TimeProvider.System;
        }

        public uint GetAccountId(string account)
        {
            var rset = LoginDb.PreparedStmt(_db, "SELECT id FROM accounts WHERE accounts.login = ? LIMIT 1", account);
            return rset != null && rset.Next() ? rset.Get<uint>("id") : 0u;
        }

        // accepts one 30s step either side of now for clock drift and input delay (RFC 6238 5.2)
        public bool ValidateTotp(uint accid, string totpCode, string secret)
        {
            const ulong period = 30;
            ulong current = (ulong)_time.GetUtcNow().ToUnixTimeSeconds() / period;

            foreach (var step in new[] { current, current - 1, current + 1 })
            {
                string expected;
                try { expected = LoginOtpHelpers.GenerateTotp(secret, step * period, 6, (int)period); }
                catch (ArgumentException) { return false; } // invalid base32 secret
                catch (FormatException) { return false; }

                var a = Encoding.UTF8.GetBytes(totpCode);
                var b = Encoding.UTF8.GetBytes(expected);
                if (a.Length != b.Length || !CryptographicOperations.FixedTimeEquals(a, b)) continue;

                lock (_lock)
                {
                    if (_lastAcceptedStep.TryGetValue(accid, out var last))
                    {
                        if (last >= step) return false;
                    }

                    _lastAcceptedStep[accid] = step;
                    return true;
                }
            }

            return false;
        }

        public static string NewBase32Secret()
        {
            const int len = 32; // must be % 8 == 0
            var sb = new StringBuilder(len);
            for (int i = 0; i < len; i++) sb.Append(Base32Chars[RandomNumberGenerator.GetInt32(32)]);
            return sb.ToString();
        }

        public bool DoesAccountNeedOtp(uint accid, string secretType)
        {
            if (secretType != "TOTP" || accid == 0) return false;

            var rset = LoginDb.PreparedStmt(_db, "SELECT validated FROM accounts_totp where accid = ?", accid);
            if (rset == null) return false;

            return rset.RowsCount() != 0 && rset.Next() && rset.Get<bool>("validated");
        }

        public bool DoesAccountNeedOtp(string account, string secretType) =>
            DoesAccountNeedOtp(GetAccountId(account), secretType);

        public string CreateAccountSecret(string account, string secretType)
        {
            if (secretType != "TOTP" || DoesAccountNeedOtp(account, "TOTP")) return "";

            uint accid = GetAccountId(account);
            if (accid == 0) return "";

            var newSecret = NewBase32Secret();
            var newRecoveryCode = NewBase32Secret();

            var rset = LoginDb.PreparedStmt(_db,
                "INSERT INTO accounts_totp(accid, secret, recovery_code, validated) VALUES(?, ?, ?, 0) ON DUPLICATE KEY UPDATE secret = values(secret), recovery_code = values(recovery_code)",
                accid, newSecret, newRecoveryCode);
            return rset != null ? newSecret : "";
        }

        public string RegenerateAccountRecoveryCode(string account, string secretType)
        {
            if (secretType != "TOTP" || !DoesAccountNeedOtp(account, "TOTP")) return "";

            uint accid = GetAccountId(account);
            if (accid == 0) return "";

            var code = NewBase32Secret();
            var rset = LoginDb.PreparedStmt(_db, "UPDATE accounts_totp SET accounts_totp.recovery_code = ? WHERE accounts_totp.accid = ?", code, accid);
            return rset != null ? code : "";
        }

        public string GetAccountSecret(string account, string secretType) =>
            GetTotpColumn(account, secretType, "secret");

        public string GetAccountRecoveryCode(string account, string secretType) =>
            GetTotpColumn(account, secretType, "recovery_code");

        private string GetTotpColumn(string account, string secretType, string column)
        {
            if (secretType != "TOTP") return "";

            uint accid = GetAccountId(account);
            if (accid == 0) return "";

            var rset = LoginDb.PreparedStmt(_db, $"SELECT {column} FROM accounts_totp where accid = ? LIMIT 1", accid);
            return rset != null && rset.Next() ? rset.Get<string>(column) : "";
        }

        public static string GenerateTrustToken()
        {
            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            return LoginOtpHelpers.ToHexString(bytes);
        }

        public void SaveTrustToken(uint accid, string token)
        {
            // Delete expired tokens for this account first
            LoginDb.PreparedStmt(_db, "DELETE FROM accounts_trust_tokens WHERE accid = ? AND expires <= NOW()", accid);

            // Insert new token with 30 day expiry (store hashed)
            LoginDb.PreparedStmt(_db, "INSERT INTO accounts_trust_tokens (token, accid, expires) VALUES (?, ?, DATE_ADD(NOW(), INTERVAL 30 DAY))",
                LoginOtpHelpers.HashTrustToken(token), accid);
        }

        public bool ValidateTrustToken(uint accid, string token)
        {
            if (!LoginOtpHelpers.IsValidTrustTokenFormat(token)) return false;

            // Delete expired tokens for this account
            LoginDb.PreparedStmt(_db, "DELETE FROM accounts_trust_tokens WHERE accid = ? AND expires <= NOW()", accid);

            // Check for valid token (compare hashed)
            var rset = LoginDb.PreparedStmt(_db, "SELECT token FROM accounts_trust_tokens WHERE token = ? AND accid = ? AND expires > NOW()",
                LoginOtpHelpers.HashTrustToken(token), accid);
            return rset != null && rset.RowsCount() != 0 && rset.Next();
        }

        public void RemoveAllTrustTokens(uint accid) =>
            LoginDb.PreparedStmt(_db, "DELETE FROM accounts_trust_tokens WHERE accid = ?", accid);
    }
}
