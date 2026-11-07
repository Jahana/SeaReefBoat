using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using XI.Ipc;
using XI.Login;
using XI.Persistence;

// XI.Verify.Auth.Live -- end-to-end gate for the ported auth_session: a real TLS 1.3 socket
// (AuthServer) in front of a real MariaDB (docker xi-live-db, host:3307, xidb schema), driven by a
// TLS client speaking the xiloader JSON protocol.
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const string User = "authgate1";   // <= 16 bytes
    private const string User2 = "authgate2";
    private const string LegacyUser = "authlegacy";
    private const string Pass = "gatepass123";
    private static readonly string[] AllUsers = { User, User2, LegacyUser, "authnew1" };

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static void CheckTrue(string name, bool cond) => Check(name, true, cond);

    private sealed class RecordingSink : IAccountLoginSink
    {
        public readonly List<uint> Logins = new();
        public readonly List<byte[]> Frames = new();
        public void AccountLogin(uint accountId)
        {
            Logins.Add(accountId);
            Frames.Add(IpcFraming.ToBytesWithHeader(new AccountLogin(accountId)));
        }
    }

    private static async Task<JsonDocument> Send(SslStream tls, object req)
    {
        var bytes = req is string s ? Encoding.UTF8.GetBytes(s) : JsonSerializer.SerializeToUtf8Bytes(req);
        await tls.WriteAsync(bytes);
        await tls.FlushAsync();
        var buf = new byte[4096];
        int n = await tls.ReadAsync(buf);
        return n <= 0 ? null : JsonDocument.Parse(buf.AsMemory(0, n));
    }

    private static async Task<SslStream> Connect(int port)
    {
        var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, port);
        var tls = new SslStream(tcp.GetStream(), false);
        await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = "localhost",
            EnabledSslProtocols = SslProtocols.Tls13,
            RemoteCertificateValidationCallback = (_, _, _, _) => true,
        });
        return tls;
    }

    private static Dictionary<string, object> Req(int cmd, string user, string pass, Action<Dictionary<string, object>> extra = null)
    {
        var d = new Dictionary<string, object>
        {
            ["command"] = cmd, ["username"] = user, ["password"] = pass, ["version"] = new[] { 2, 2, 0 },
        };
        extra?.Invoke(d);
        return d;
    }

    private static void Cleanup(IDatabase db)
    {
        foreach (var u in AllUsers)
        {
            var id = LoginDb.PreparedStmt(db, "SELECT id FROM accounts WHERE login = ?", u);
            if (id != null && id.Next())
            {
                var accid = id.Get<uint>("id");
                LoginDb.PreparedStmt(db, "DELETE FROM accounts_trust_tokens WHERE accid = ?", accid);
                LoginDb.PreparedStmt(db, "DELETE FROM accounts_totp WHERE accid = ?", accid);
                LoginDb.PreparedStmt(db, "DELETE FROM accounts_profile WHERE accid = ?", accid);
                LoginDb.PreparedStmt(db, "DELETE FROM accounts WHERE id = ?", accid);
            }
        }
    }

    private static int Main(string[] argv)
    {
        int dbPort = argv.Length > 0 ? int.Parse(argv[0]) : 3307;
        var settings = new NetworkSettings { SqlHost = "127.0.0.1", SqlPort = dbPort, SqlLogin = "root", SqlPassword = "root", SqlDatabase = "xidb" };
        using var db = new MySqlDatabase(settings);

        try
        {
            var probe = db.Execute("SELECT 1", Array.Empty<BoundValue>());
            if (probe == null || !probe.Next()) { Console.WriteLine("DB not reachable"); return 3; }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DB not reachable ({ex.GetType().Name}: {ex.Message})");
            return 3;
        }

        try
        {
            Cleanup(db);
            RunAsync(db).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FAIL unhandled {ex}");
        }
        finally
        {
            Cleanup(db);
        }

        Console.WriteLine($"RESULT: {_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    private static async Task RunAsync(IDatabase db)
    {
        var sink = new RecordingSink();
        var sessions = new LoginSessions();
        using var server = new AuthServer(IPAddress.Loopback, 0, SelfSignedCert.Create(), db, sessions, sink, new AuthSettings());
        _ = server.Start();
        int port = server.Port;

        // --- create account, duplicate, malformed ---
        using (var tls = await Connect(port))
        {
            var r = await Send(tls, Req(0x20, User, Pass));
            Check("create -> LOGIN_SUCCESS_CREATE(3)", 3, r.RootElement.GetProperty("result").GetInt32());
            r = await Send(tls, Req(0x20, User, Pass));
            Check("create dup -> LOGIN_ERROR_CREATE_TAKEN(4)", 4, r.RootElement.GetProperty("result").GetInt32());

            var row = LoginDb.PreparedStmt(db, "SELECT id, password, status, priv FROM accounts WHERE login = ?", User);
            CheckTrue("account row exists", row.Next());
            CheckTrue("stored password is bcrypt $2a$ 60 chars", PasswordHasher.IsBcryptHash(row.Get<string>("password")) && row.Get<string>("password").StartsWith("$2a$12$"));
            Check("status NORMAL", 1, row.Get<int>("status"));
            CheckTrue("account id >= 1000", row.Get<uint>("id") >= 1000);
        }

        // other accounts signed on from 127.0.0.1 in the last 2 hours (a live client) hold udp slots; the test's
        // accounts take the free ones in order
        var taken = new SortedSet<int>();
        var others = LoginDb.PreparedStmt(db, "SELECT udp_port_slot FROM accounts_profile WHERE client_addr = '127.0.0.1' AND refreshed > NOW() - INTERVAL 2 HOUR");
        while (others != null && others.Next()) taken.Add(others.Get<int>("udp_port_slot"));
        int NextFree() { int s = 1; while (taken.Contains(s)) s++; taken.Add(s); return s; }
        int slot1 = NextFree(), slot2 = NextFree();

        // --- login success ---
        uint accid;
        using (var tls = await Connect(port))
        {
            var r = await Send(tls, Req(0x10, User, Pass));
            var root = r.RootElement;
            Check("login -> LOGIN_SUCCESS(1)", 1, root.GetProperty("result").GetInt32());
            accid = root.GetProperty("account_id").GetUInt32();
            var hash = new List<byte>();
            foreach (var e in root.GetProperty("session_hash").EnumerateArray()) hash.Add(e.GetByte());
            Check("session_hash is 16 bytes", 16, hash.Count);

            var p = LoginDb.PreparedStmt(db, "SELECT session_hash, client_addr, udp_port_slot FROM accounts_profile WHERE accid = ?", accid);
            CheckTrue("accounts_profile row stored", p.Next());
            CheckTrue("stored session_hash == replied hash", p.GetBlobBytes("session_hash").AsSpan().SequenceEqual(hash.ToArray()));
            Check("client_addr", "127.0.0.1", p.Get<string>("client_addr"));
            Check("udp_port_slot: the first free (1 with no other client)", slot1, p.Get<int>("udp_port_slot"));
            CheckTrue("authenticated session registered", sessions.GetAuthenticatedSession("127.0.0.1", LoginSessions.HashKey(hash.ToArray())).AccountId == accid);
            Check("AccountLogin IPC sent once", 1, sink.Logins.Count);
            Check("AccountLogin IPC accountId", accid, sink.Logins[0]);
            var rt = (AccountLogin)IpcFraming.FromBytesWithHeader(sink.Frames[0], MessageType.AccountLogin, out _);
            Check("AccountLogin IPC frame round-trip", accid, rt.AccountId);
            // nlohmann key order
            var rawKeys = new List<string>();
            foreach (var pr in root.EnumerateObject()) rawKeys.Add(pr.Name);
            Check("reply key order (alphabetical)", "account_id,result,session_hash", string.Join(",", rawKeys));
        }

        // --- udp slot allocation: second account behind same address gets a different slot ---
        using (var tls = await Connect(port))
        {
            await Send(tls, Req(0x20, User2, Pass));
            var r = await Send(tls, Req(0x10, User2, Pass));
            Check("2nd account login", 1, r.RootElement.GetProperty("result").GetInt32());
            var id2 = r.RootElement.GetProperty("account_id").GetUInt32();
            var p = LoginDb.PreparedStmt(db, "SELECT udp_port_slot FROM accounts_profile WHERE accid = ?", id2);
            p.Next();
            Check("2nd account udp_port_slot: the next free (2 with no other client)", slot2, p.Get<int>("udp_port_slot"));
        }

        // --- version / non-json handling ---
        using (var tls = await Connect(port))
        {
            var r = await Send(tls, Req(0x10, User, Pass, d => d["version"] = new[] { 2, 1, 0 }));
            CheckTrue("old version -> error_message", r.RootElement.GetProperty("error_message").GetString().StartsWith("Unsupported xiloader version 2.1.0."));
        }

        using (var tls = await Connect(port))
        {
            var buf = new byte[4096];
            await tls.WriteAsync(new byte[] { 0xFF, 1, 2, 3 });
            int n = await tls.ReadAsync(buf);
            Check("0xFF non-json -> VERSION_UNSUPPORTED(0x0B), 1 byte", "1:11", $"{n}:{buf[0]}");
            n = await tls.ReadAsync(buf);
            Check("socket closed after non-json", 0, n);
        }

        using (var tls = await Connect(port))
        {
            var buf = new byte[4096];
            await tls.WriteAsync(new byte[] { 0x01, 2, 3 });
            int n = await tls.ReadAsync(buf);
            Check("ancient client -> LOGIN_ERROR(2), 1 byte", "1:2", $"{n}:{buf[0]}");
        }

        // --- bad password, then lockout after 5 failures from the same address ---
        using (var tls = await Connect(port))
        {
            for (int i = 0; i < 5; i++)
            {
                var r = await Send(tls, Req(0x10, User, "wrongpass"));
                Check($"bad password #{i + 1} -> LOGIN_ERROR(2)", 2, r.RootElement.GetProperty("result").GetInt32());
            }

            var locked = await Send(tls, Req(0x10, User, Pass));
            Check("locked out even with correct password", 2, locked.RootElement.GetProperty("result").GetInt32());
            sessions.ClearLoginFailures("127.0.0.1");
            var ok = await Send(tls, Req(0x10, User, Pass));
            Check("after clearing lockout, login works", 1, ok.RootElement.GetProperty("result").GetInt32());
        }

        // --- malformed username -> no reply, socket closed ---
        using (var tls = await Connect(port))
        {
            var buf = new byte[4096];
            await tls.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(Req(0x10, "this_name_is_way_too_long", Pass)));
            int n = await tls.ReadAsync(buf);
            Check("malformed username -> connection dropped, no reply", 0, n);
        }

        // --- banned account -> LOGIN_FAIL(0) ---
        LoginDb.PreparedStmt(db, "UPDATE accounts SET status = 2 WHERE id = ?", accid);
        using (var tls = await Connect(port))
        {
            var r = await Send(tls, Req(0x10, User, Pass));
            Check("banned -> LOGIN_FAIL(0)", 0, r.RootElement.GetProperty("result").GetInt32());
        }

        LoginDb.PreparedStmt(db, "UPDATE accounts SET status = 1 WHERE id = ?", accid);

        // --- change password ---
        using (var tls = await Connect(port))
        {
            var r = await Send(tls, Req(0x30, User, Pass, d => d["new_password"] = "newpass456"));
            Check("change password -> LOGIN_SUCCESS_CHANGE_PASSWORD(6)", 6, r.RootElement.GetProperty("result").GetInt32());
            Check("change password reply account_id 0", 0, r.RootElement.GetProperty("account_id").GetInt32());
            Check("change password reply session_hash \"\"", "", r.RootElement.GetProperty("session_hash").GetString());
            r = await Send(tls, Req(0x10, User, Pass));
            Check("old password rejected", 2, r.RootElement.GetProperty("result").GetInt32());
            r = await Send(tls, Req(0x10, User, "newpass456"));
            Check("new password accepted", 1, r.RootElement.GetProperty("result").GetInt32());
            r = await Send(tls, Req(0x30, User, "wrong", d => d["new_password"] = "x"));
            Check("change password w/ bad pw -> LOGIN_ERROR_CHANGE_PASSWORD(7)", 7, r.RootElement.GetProperty("result").GetInt32());
        }

        // --- legacy MariaDB PASSWORD() hash is accepted and upgraded to bcrypt ---
        var legacyId = (uint)2000;
        LoginDb.PreparedStmt(db, "INSERT INTO accounts(id,login,password,timecreate,timelastmodify,status,priv) VALUES(?, ?, PASSWORD(?), NOW(), NULL, 1, 1)", legacyId, LegacyUser, "legacypw1");
        using (var tls = await Connect(port))
        {
            var r = await Send(tls, Req(0x10, LegacyUser, "legacypw1"));
            Check("legacy PASSWORD() hash login", 1, r.RootElement.GetProperty("result").GetInt32());
            var row = LoginDb.PreparedStmt(db, "SELECT password FROM accounts WHERE login = ?", LegacyUser);
            row.Next();
            CheckTrue("legacy hash upgraded to bcrypt", PasswordHasher.IsBcryptHash(row.Get<string>("password")));
            r = await Send(tls, Req(0x10, LegacyUser, "legacypw1"));
            Check("login after upgrade", 1, r.RootElement.GetProperty("result").GetInt32());
        }

        // --- TOTP lifecycle ---
        string secret;
        string recovery;
        using (var tls = await Connect(port))
        {
            const string P = "newpass456";
            var r = await Send(tls, Req(0x31, User, P));
            Check("create TOTP -> LOGIN_SUCCESS_CREATE_TOTP(0x10)", 0x10, r.RootElement.GetProperty("result").GetInt32());
            var uri = r.RootElement.GetProperty("TOTP_uri").GetString();
            CheckTrue("TOTP uri format", uri.StartsWith($"otpauth://totp/Nameless:{User}?secret=") && uri.EndsWith("&issuer=Nameless&algorithm=SHA1&digits=6&period=30"));
            secret = uri.Split("secret=")[1].Split('&')[0];
            Check("secret is 32 base32 chars", 32, secret.Length);

            // Not yet validated -> login does not need OTP
            r = await Send(tls, Req(0x10, User, P));
            Check("login before TOTP verified needs no OTP", 1, r.RootElement.GetProperty("result").GetInt32());

            var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var code = LoginOtpHelpers.GenerateTotp(secret, now);
            r = await Send(tls, Req(0x34, User, P, d => d["otp"] = "000000" == code ? "000001" : "000000"));
            CheckTrue("verify w/ wrong code -> error_message", r.RootElement.TryGetProperty("error_message", out _));
            r = await Send(tls, Req(0x34, User, P, d => d["otp"] = code));
            Check("verify TOTP -> LOGIN_SUCCESS_VERIFY_TOTP(0x11)", 0x11, r.RootElement.GetProperty("result").GetInt32());
            recovery = r.RootElement.GetProperty("recovery_code").GetString();
            Check("recovery code is 32 chars", 32, recovery.Length);

            // Replay of the same code must be refused (RFC 6238 5.2)
            r = await Send(tls, Req(0x10, User, P, d => d["otp"] = code));
            Check("TOTP replay refused -> LOGIN_ERROR(2)", 2, r.RootElement.GetProperty("result").GetInt32());

            r = await Send(tls, Req(0x10, User, P));
            Check("login w/o otp when TOTP enabled -> LOGIN_ERROR(2)", 2, r.RootElement.GetProperty("result").GetInt32());

            // next time step's code (accepted: within +-1 step window, and newer than last)
            var next = LoginOtpHelpers.GenerateTotp(secret, now + 30);
            r = await Send(tls, Req(0x10, User, P, d => { d["otp"] = next; d["trust_this_computer"] = true; }));
            Check("login with fresh TOTP + trust_this_computer", 1, r.RootElement.GetProperty("result").GetInt32());
            var token = r.RootElement.GetProperty("trust_token").GetString();
            CheckTrue("trust token is 64 hex", LoginOtpHelpers.IsValidTrustTokenFormat(token));
            var tk = LoginDb.PreparedStmt(db, "SELECT token FROM accounts_trust_tokens WHERE accid = ?", accid);
            tk.Next();
            Check("trust token stored hashed", LoginOtpHelpers.HashTrustToken(token), tk.Get<string>("token"));

            r = await Send(tls, Req(0x10, User, P, d => d["trust_token"] = token));
            Check("login via trust token (no otp)", 1, r.RootElement.GetProperty("result").GetInt32());

            var bad = new string('a', 64);
            r = await Send(tls, Req(0x10, User, P, d => d["trust_token"] = bad));
            Check("invalid trust token, no otp -> TRUST_TOKEN_INVALID(0x13)", 0x13, r.RootElement.GetProperty("result").GetInt32());

            // regenerate recovery with the recovery code as 'otp' (case-insensitive)
            r = await Send(tls, Req(0x33, User, P, d => d["otp"] = recovery.ToLowerInvariant()));
            Check("regenerate recovery via recovery code", 0x11, r.RootElement.GetProperty("result").GetInt32());
            var recovery2 = r.RootElement.GetProperty("recovery_code").GetString();
            CheckTrue("recovery code changed", recovery2 != recovery);

            // remove TOTP with the new recovery code; trust tokens purged
            r = await Send(tls, Req(0x32, User, P, d => d["otp"] = recovery2));
            Check("remove TOTP -> LOGIN_SUCCESS_REMOVE_TOTP(0x12)", 0x12, r.RootElement.GetProperty("result").GetInt32());
            var gone = LoginDb.PreparedStmt(db, "SELECT COUNT(*) AS c FROM accounts_totp WHERE accid = ?", accid);
            gone.Next();
            Check("accounts_totp row removed", 0, gone.Get<int>("c"));
            tk = LoginDb.PreparedStmt(db, "SELECT COUNT(*) AS c FROM accounts_trust_tokens WHERE accid = ?", accid);
            tk.Next();
            Check("trust tokens purged", 0, tk.Get<int>("c"));
            r = await Send(tls, Req(0x10, User, P));
            Check("login without OTP after removal", 1, r.RootElement.GetProperty("result").GetInt32());
        }

        // --- NOOP -> no reply ---
        using (var tls = await Connect(port))
        {
            var buf = new byte[4096];
            await tls.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(Req(0x00, User, Pass)));
            int n = await tls.ReadAsync(buf);
            Check("NOOP -> no reply, connection dropped", 0, n);
        }
    }
}
