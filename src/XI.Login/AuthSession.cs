// C# port of the xiloader auth state machine (src/login/auth_session.cpp, port 54231).
//
// AuthSession is transport-agnostic: Handle() takes one received buffer (what the engine's
// async_read_some delivers) and returns the reply bytes plus whether to close, so the DB/protocol
// logic is testable without sockets. AuthServer is the TLS socket loop around it.
//
// Reply contract (matches the engine): a null Reply means the engine wrote nothing and let the
// session die (shared_ptr drops -> socket closed), e.g. malformed input / NOOP / unhandled code.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using XI.Ipc;
using XI.Persistence;

namespace XI.Login
{
    // The engine's dealerChannel_ send of ipc::AccountLogin to the world server.
    public interface IAccountLoginSink
    {
        void AccountLogin(uint accountId);
    }

    // Frames AccountLogin exactly as ipc::toBytesWithHeader and hands the bytes to a transport.
    public sealed class IpcAccountLoginSink : IAccountLoginSink
    {
        private readonly Action<byte[]> _send;
        public IpcAccountLoginSink(Action<byte[]> send) { _send = send; }

        public void AccountLogin(uint accountId) =>
            _send(IpcFraming.ToBytesWithHeader(new XI.Ipc.AccountLogin(accountId)));
    }

    public sealed class AuthSettings
    {
        public bool AccountCreation = true;     // login.ACCOUNT_CREATION
        public string ServerName = "Nameless";  // main.SERVER_NAME
        public Action<string> Log;              // the auth port's connections and TLS failures, when set
    }

    public sealed class AuthReply
    {
        public byte[] Data;  // null = no reply, session ends
        public bool Close;   // close the socket after writing Data
    }

    public sealed class AuthSession
    {
        public const int BufferSize = 4096;

        private readonly IDatabase _db;
        private readonly OtpStore _otp;
        private readonly LoginSessions _sessions;
        private readonly IAccountLoginSink _sink;
        private readonly AuthSettings _settings;
        private readonly TimeProvider _time;
        private readonly string _ip;

        public AuthSession(string ipAddress, IDatabase db, OtpStore otp, LoginSessions sessions,
                           IAccountLoginSink sink, AuthSettings settings, TimeProvider time = null)
        {
            _ip = ipAddress;
            _db = db;
            _otp = otp;
            _sessions = sessions;
            _sink = sink;
            _settings = settings;
            _time = time ?? TimeProvider.System;
        }

        // ---- reply builders ------------------------------------------------------------------

        // nlohmann::json::dump() orders object keys alphabetically; SortedDictionary (ordinal)
        // keeps replies byte-identical to the engine's.
        private static AuthReply Json(SortedDictionary<string, object> obj)
        {
            var sb = new StringBuilder("{");
            bool first = true;
            foreach (var kv in obj)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(JsonSerializer.Serialize(kv.Key)).Append(':');
                sb.Append(kv.Value switch
                {
                    string s => JsonSerializer.Serialize(s),
                    byte[] a => "[" + string.Join(",", a) + "]",
                    _ => Convert.ToString(kv.Value, CultureInfo.InvariantCulture),
                });
            }

            var bytes = Encoding.UTF8.GetBytes(sb.Append('}').ToString());
            if (bytes.Length > BufferSize) throw new InvalidOperationException("reply exceeds session buffer");
            return new AuthReply { Data = bytes };
        }

        private static SortedDictionary<string, object> Obj() => new(StringComparer.Ordinal);

        private static AuthReply LoginResultReply(LoginResult r)
        {
            var o = Obj();
            o["result"] = (byte)r;
            return Json(o);
        }

        private static AuthReply ErrorMessageReply(string msg)
        {
            var o = Obj();
            o["error_message"] = msg;
            return Json(o);
        }

        // ---- request parsing -----------------------------------------------------------------

        // loginHelpers::jsonGet<T>: missing key or wrong JSON type -> default. The engine's integer
        // branches require is_number_unsigned (also for the signed int8 `command`).
        private static string GetString(JsonElement o, string key) =>
            o.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";

        private static bool GetBool(JsonElement o, string key) =>
            o.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.True;

        // nlohmann is_number_unsigned: a plain non-negative integer literal ("1.0", "-1", "1e2" are not).
        private static bool TryGetUnsigned(JsonElement v, out ulong value)
        {
            value = 0;
            if (v.ValueKind != JsonValueKind.Number) return false;
            var raw = v.GetRawText();
            if (raw.AsSpan().IndexOfAny(".eE-") >= 0) return false;
            return v.TryGetUInt64(out value);
        }

        private static sbyte GetCommand(JsonElement o) =>
            o.TryGetProperty("command", out var v) && TryGetUnsigned(v, out var u)
                ? unchecked((sbyte)(byte)u) // nlohmann get<int8_t> narrows
                : (sbyte)0;

        private static byte[] GetVersion(JsonElement o)
        {
            var none = new byte[] { 0, 0, 0 };
            if (!o.TryGetProperty("version", out var arr) || arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() != 3)
                return none;

            var result = new byte[3];
            int i = 0;
            foreach (var e in arr.EnumerateArray())
            {
                if (!TryGetUnsigned(e, out var u)) return none;
                result[i++] = unchecked((byte)u);
            }

            return result;
        }

        // isStringMalformed over the engine's signed `char`: empty, too long (bytes), or any byte
        // < 0x20 -- which includes every byte >= 0x80 since char is signed.
        private static bool IsMalformed(string s, int maxLength)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            if (bytes.Length == 0 || bytes.Length > maxLength) return true;
            foreach (var b in bytes) if (b < 0x20 || b >= 0x80) return true;
            return false;
        }

        // ---- handler -------------------------------------------------------------------------

        public AuthReply Handle(ReadOnlySpan<byte> request)
        {
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(request.ToArray());
            }
            catch (JsonException)
            {
                // newModeFlag: first byte 0xFF = old non-json xiloader; otherwise ancient xiloader.
                bool newModeFlag = request.Length > 0 && request[0] == 0xFF;
                var code = newModeFlag ? LoginResult.LoginErrorVersionUnsupported : LoginResult.LoginError;
                return new AuthReply { Data = new[] { (byte)code }, Close = true };
            }

            using (doc)
            {
                return HandleObject(doc.RootElement);
            }
        }

        private AuthReply HandleObject(JsonElement o)
        {
            // A non-object JSON value still parses in the engine; every jsonGet then yields its default.
            bool isObj = o.ValueKind == JsonValueKind.Object;
            sbyte code = isObj ? GetCommand(o) : (sbyte)0;
            string username = isObj ? GetString(o, "username") : "";
            string password = isObj ? GetString(o, "password") : "";
            string updatedPassword = isObj ? GetString(o, "new_password") : "";
            string otp = isObj ? GetString(o, "otp") : "";
            string trustToken = isObj ? GetString(o, "trust_token") : "";
            bool trustThisComputer = isObj && GetBool(o, "trust_this_computer");
            byte[] version = isObj ? GetVersion(o) : new byte[] { 0, 0, 0 };

            // Check major.minor but ignore trivial
            var sup = XiloaderVersion.Supported;
            if (version[0] != sup[0] || version[1] != sup[1])
            {
                return ErrorMessageReply($"Unsupported xiloader version {version[0]}.{version[1]}.{version[2]}.\n" +
                                         $"This server requires version {sup[0]}.{sup[1]}.x.");
            }

            if (IsMalformed(username, 16)) return null; // login_parse: malformed username
            if (IsMalformed(password, 32)) return null; // login_parse: malformed password

            switch ((LoginCmd)(byte)code)
            {
                case LoginCmd.LoginNoop:
                    return null; // can happen if control + C is pressed in xiloader
                case LoginCmd.LoginAttempt:
                    return Attempt(username, password, otp, trustToken, trustThisComputer);
                case LoginCmd.LoginCreate:
                    return Create(username, password);
                case LoginCmd.LoginChangePassword:
                    return ChangePassword(username, password, updatedPassword, otp);
                case LoginCmd.LoginCreateTotp:
                    return CreateTotp(username, password);
                case LoginCmd.LoginRemoveTotp:
                    return RemoveTotp(username, password, otp);
                case LoginCmd.LoginRegenerateRecovery:
                    return RegenerateRecovery(username, password, otp);
                case LoginCmd.LoginVerifyTotp:
                    return VerifyTotp(username, password, otp);
                default:
                    return null; // Unhandled auth code
            }
        }

        private AuthReply Attempt(string username, string password, string otp, string trustToken, bool trustThisComputer)
        {
            if (_sessions.IsLoginLockedOut(_ip)) return LoginResultReply(LoginResult.LoginError);

            var info = ValidatePassword(username, password);
            if (info == null)
            {
                _sessions.RecordLoginFailure(_ip);
                return LoginResultReply(LoginResult.LoginError);
            }

            var (accountId, status) = info.Value;

            // Reject banned/non-normal accounts before processing OTP or trust tokens
            if ((status & (uint)AccountStatusCode.Normal) == 0)
            {
                // Purge any lingering trust tokens for banned accounts
                if ((status & (uint)AccountStatusCode.Banned) != 0) _otp.RemoveAllTrustTokens(accountId);
                return LoginResultReply(LoginResult.LoginFail);
            }

            bool otpVerified = false;

            if (_otp.DoesAccountNeedOtp(accountId, "TOTP"))
            {
                bool trustedByToken = false;

                // Try trust token first
                if (trustToken.Length != 0) trustedByToken = _otp.ValidateTrustToken(accountId, trustToken);

                // Fall back to OTP code if trust token invalid/missing
                if (!trustedByToken)
                {
                    if (otp.Length == 0 && trustToken.Length != 0)
                        return LoginResultReply(LoginResult.LoginErrorTrustTokenInvalid);

                    if (!_otp.ValidateTotp(accountId, otp, _otp.GetAccountSecret(username, "TOTP")))
                        return LoginResultReply(LoginResult.LoginError);
                }

                otpVerified = true;
            }

            LoginDb.PreparedStmt(_db, "UPDATE accounts SET accounts.timelastmodify = NULL WHERE accounts.id = ?", accountId);

            _sink.AccountLogin(accountId);

            // set Satchel to the same size as inventory on all chars on their account if character has OTP
            // Note: Upgrades happen in-game with gobbiebag
            if (otpVerified)
            {
                LoginDb.PreparedStmt(_db,
                    "UPDATE char_storage a JOIN char_storage b ON a.charid = b.charid " +
                    "SET a.satchel = b.inventory " +
                    "WHERE a.charid IN (SELECT charid FROM chars WHERE accid = ?)",
                    accountId);
            }

            // the session hash is also the profile server credential, so it must be unguessable
            var hash = new byte[16];
            RandomNumberGenerator.Fill(hash);

            // clients behind the same address get different game udp ports, see profileAvailableNotice
            ushort udpPortSlot = 1;
            var slots = LoginDb.PreparedStmt(_db,
                "SELECT udp_port_slot FROM accounts_profile " +
                "WHERE client_addr = ? AND accid != ? AND refreshed > NOW() - INTERVAL 2 HOUR " +
                "ORDER BY udp_port_slot",
                _ip, accountId);

            while (slots != null && slots.Next())
            {
                var taken = slots.Get<ushort>("udp_port_slot");
                if (taken > udpPortSlot) break;
                udpPortSlot = (ushort)(taken + 1);
            }

            // keeps the account's last open status
            var stored = LoginDb.PreparedStmt(_db,
                "INSERT INTO accounts_profile(accid, session_hash, client_addr, udp_port_slot) VALUES(?, ?, ?, ?) " +
                "ON DUPLICATE KEY UPDATE session_hash = VALUES(session_hash), client_addr = VALUES(client_addr), udp_port_slot = VALUES(udp_port_slot), refreshed = NOW()",
                accountId, hash, _ip, udpPortSlot);
            if (stored == null) return LoginResultReply(LoginResult.LoginError);

            var reply = Obj();
            reply["result"] = (byte)LoginResult.LoginSuccess;
            reply["account_id"] = accountId;
            reply["session_hash"] = hash; // sent as an array: raw bytes are not valid UTF-8

            if (trustThisComputer && otpVerified)
            {
                var newToken = OtpStore.GenerateTrustToken();
                _otp.SaveTrustToken(accountId, newToken);
                reply["trust_token"] = newToken;
            }

            var result = Json(reply);

            _sessions.ClearLoginFailures(_ip);

            var session = _sessions.GetAuthenticatedSession(_ip, LoginSessions.HashKey(hash));
            session.AccountId = accountId;
            session.AuthorizedTime = _time.GetUtcNow();
            return result;
        }

        private AuthReply Create(string username, string password)
        {
            if (!_settings.AccountCreation) return LoginResultReply(LoginResult.LoginErrorCreateDisabled);

            // looking for same login
            var rset = LoginDb.PreparedStmt(_db, "SELECT accounts.id FROM accounts WHERE accounts.login = ?", username);
            if (rset == null) return LoginResultReply(LoginResult.LoginErrorCreate);

            if (rset.RowsCount() != 0) return LoginResultReply(LoginResult.LoginErrorCreateTaken);

            // creating new account_id
            var maxRs = LoginDb.PreparedStmt(_db, "SELECT COALESCE(MAX(accounts.id), 0) AS max_id FROM accounts");
            if (maxRs == null || maxRs.RowsCount() == 0 || !maxRs.Next())
                return LoginResultReply(LoginResult.LoginErrorCreate);

            uint accid = Math.Max(maxRs.Get<uint>("max_id") + 1, 1000u);

            var created = LoginDb.PreparedStmt(_db,
                "INSERT INTO accounts(id,login,password,timecreate,timelastmodify,status,priv) " +
                "VALUES(?, ?, ?, ?, NULL, ?, ?)",
                accid,
                username,
                PasswordHasher.Generate(password),
                DateTime.Now.ToString("yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture),
                (byte)AccountStatusCode.Normal,
                (byte)AccountPrivilegeCode.User);

            return LoginResultReply(created == null ? LoginResult.LoginErrorCreate : LoginResult.LoginSuccessCreate);
        }

        private AuthReply ChangePassword(string username, string password, string updatedPassword, string otp)
        {
            var info = ValidatePassword(username, password);
            if (info == null) return LoginResultReply(LoginResult.LoginErrorChangePassword);

            var (accid, status) = info.Value;

            if (_otp.DoesAccountNeedOtp(username, "TOTP") &&
                !_otp.ValidateTotp(accid, otp, _otp.GetAccountSecret(username, "TOTP")))
            {
                return LoginResultReply(LoginResult.LoginErrorChangePassword);
            }

            // The engine does not return after the banned reply; the buffer is rewritten (last
            // reply wins) only if the NORMAL branch also runs, which a single-bit status never does.
            AuthReply reply = null;

            if ((status & (uint)AccountStatusCode.Banned) != 0)
                reply = LoginResultReply(LoginResult.LoginErrorChangePassword);

            if ((status & (uint)AccountStatusCode.Normal) != 0)
            {
                if (updatedPassword.Length == 0) return LoginResultReply(LoginResult.LoginErrorChangePassword);

                LoginDb.PreparedStmt(_db, "UPDATE accounts SET accounts.timelastmodify = NULL WHERE accounts.id = ?", accid);

                var updated = LoginDb.PreparedStmt(_db, "UPDATE accounts SET accounts.password = ? WHERE accounts.id = ?",
                    PasswordHasher.Generate(updatedPassword), accid);
                if (updated == null) return LoginResultReply(LoginResult.LoginErrorChangePassword);

                _otp.RemoveAllTrustTokens(accid);

                var o = Obj();
                o["result"] = (byte)LoginResult.LoginSuccessChangePassword;
                o["account_id"] = 0;
                o["session_hash"] = "";
                return Json(o);
            }

            return reply;
        }

        private AuthReply CreateTotp(string username, string password)
        {
            if (ValidatePassword(username, password) == null) return ErrorMessageReply("Failed to validate credentials");

            var secret = _otp.CreateAccountSecret(username, "TOTP");
            if (secret.Length == 0) return ErrorMessageReply("Failed to validate credentials");

            var name = _settings.ServerName;
            var uri = $"otpauth://totp/{name}:{username}?secret={secret}&issuer={name}&algorithm=SHA1&digits=6&period=30";
            var o = Obj();
            o["result"] = (byte)LoginResult.LoginSuccessCreateTotp;
            o["TOTP_uri"] = uri;
            return Json(o);
        }

        // otp is accepted as a TOTP code or (case-insensitively) the account's recovery code.
        private bool OtpOrRecoveryOk(uint accid, string username, string otp)
        {
            var secret = _otp.GetAccountSecret(username, "TOTP");
            var recovery = _otp.GetAccountRecoveryCode(username, "TOTP");
            return _otp.ValidateTotp(accid, otp, secret) ||
                   string.Equals(otp, recovery, StringComparison.OrdinalIgnoreCase);
        }

        private AuthReply RemoveTotp(string username, string password, string otp)
        {
            var info = ValidatePassword(username, password);
            if (info == null) return ErrorMessageReply("Failed to validate credentials");

            var accid = info.Value.Item1;
            if (!OtpOrRecoveryOk(accid, username, otp)) return ErrorMessageReply("Failed to validate credentials");

            LoginDb.PreparedStmt(_db, "DELETE FROM accounts_totp WHERE accounts_totp.accid = ? LIMIT 1", accid);
            _otp.RemoveAllTrustTokens(accid);
            return LoginResultReply(LoginResult.LoginSuccessRemoveTotp);
        }

        private AuthReply RegenerateRecovery(string username, string password, string otp)
        {
            var info = ValidatePassword(username, password);
            if (info == null) return ErrorMessageReply("Failed to validate credentials");

            var accid = info.Value.Item1;
            if (!OtpOrRecoveryOk(accid, username, otp)) return ErrorMessageReply("Failed to validate credentials");

            var o = Obj();
            o["result"] = (byte)LoginResult.LoginSuccessVerifyTotp;
            o["recovery_code"] = _otp.RegenerateAccountRecoveryCode(username, "TOTP");
            return Json(o);
        }

        private AuthReply VerifyTotp(string username, string password, string otp)
        {
            var info = ValidatePassword(username, password);
            if (info == null) return ErrorMessageReply("Failed to validate credentials");

            var accid = info.Value.Item1;
            if (!_otp.ValidateTotp(accid, otp, _otp.GetAccountSecret(username, "TOTP")))
                return ErrorMessageReply("Failed to validate credentials");

            LoginDb.PreparedStmt(_db, "UPDATE accounts_totp SET validated = TRUE WHERE accid = ? LIMIT 1", accid);
            var o = Obj();
            o["result"] = (byte)LoginResult.LoginSuccessVerifyTotp;
            o["recovery_code"] = _otp.GetAccountRecoveryCode(username, "TOTP");
            return Json(o);
        }

        // auth_session::validatePassword: returns (accountId, status) or null on bad credentials.
        // Legacy (non-bcrypt) hashes are checked with MariaDB PASSWORD() and upgraded in place.
        public (uint, uint)? ValidatePassword(string username, string password)
        {
            uint accountId = 0;
            uint status = 0;
            string passHash = "";

            var rset = LoginDb.PreparedStmt(_db, "SELECT accounts.id, accounts.status, accounts.password FROM accounts WHERE accounts.login = ?", username);
            if (rset != null && rset.RowsCount() != 0 && rset.Next())
            {
                accountId = rset.Get<uint>("id");
                status = rset.Get<uint>("status");
                passHash = rset.Get<string>("password");
            }

            if (PasswordHasher.IsBcryptHash(passHash))
            {
                if (!PasswordHasher.Validate(password, passHash)) return null;
            }
            else
            {
                var legacy = LoginDb.PreparedStmt(_db, "SELECT PASSWORD(?)", password);
                // The engine falls through to success if this query yields no row; fail closed.
                if (legacy == null || legacy.RowsCount() == 0 || !legacy.Next()) return null;
                if (legacy.Get<string>(0u) != passHash) return null;

                passHash = PasswordHasher.Generate(password);
                LoginDb.PreparedStmt(_db, "UPDATE accounts SET accounts.password = ? WHERE accounts.login = ?", passHash, username);
                if (!PasswordHasher.Validate(password, passHash)) return null;
            }

            return (accountId, status);
        }
    }
}
