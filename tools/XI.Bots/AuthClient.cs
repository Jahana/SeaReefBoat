using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace XI.Bots
{
    // xiloader's half of the auth port (src/login/auth_session.cpp): a TLS 1.3 connection per command, one JSON request,
    // one JSON reply.
    public static class AuthClient
    {
        public const int CmdLogin = 0x10, CmdCreate = 0x20;                    // LOGIN_ATTEMPT, LOGIN_CREATE
        public const int ResultSuccess = 0x01, ResultSuccessCreate = 0x03, ResultCreateTaken = 0x04;

        public sealed class Reply
        {
            public int Result = -1;
            public uint AccountId;
            public byte[] SessionHash;
            public string Error;
            public string Raw;
        }

        public static async Task<Reply> Send(Endpoints ep, int command, string user, string password, CancellationToken ct)
        {
            using var tcp = ep.NewTcpClient();
            await tcp.ConnectAsync(Endpoints.Checked(ep.Host, ep.Auth), ct);
            using var tls = new SslStream(tcp.GetStream(), false);
            await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = "localhost",
                EnabledSslProtocols = SslProtocols.Tls13,
                RemoteCertificateValidationCallback = (_, _, _, _) => true, // the server's self-signed login.cert
            }, ct);

            var req = new Dictionary<string, object>
            {
                ["command"] = command, ["username"] = user, ["password"] = password, ["version"] = new[] { 2, 2, 0 },
            };
            await tls.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(req), ct);
            await tls.FlushAsync(ct);

            // one reply; TLS may hand it over in pieces, so read until it parses
            var acc = new MemoryStream();
            var buf = new byte[4096];
            while (true)
            {
                int n = await tls.ReadAsync(buf, ct);
                if (n <= 0) break;
                acc.Write(buf, 0, n);
                var r = TryParse(acc.ToArray());
                if (r != null) return r;
            }
            return new Reply { Error = acc.Length == 0 ? "closed without a reply" : "unparsable reply " + Convert.ToHexString(acc.ToArray()) };
        }

        private static Reply TryParse(byte[] data)
        {
            JsonDocument doc;
            try { doc = JsonDocument.Parse(data); }
            catch (JsonException) { return data.Length == 1 ? new Reply { Result = data[0], Raw = $"[{data[0]}]" } : null; }
            using (doc)
            {
                var o = doc.RootElement;
                var r = new Reply { Raw = Encoding.UTF8.GetString(data) };
                if (o.TryGetProperty("result", out var res)) r.Result = res.GetInt32();
                if (o.TryGetProperty("error_message", out var err)) r.Error = err.GetString();
                if (o.TryGetProperty("account_id", out var acc)) r.AccountId = acc.GetUInt32();
                if (o.TryGetProperty("session_hash", out var h) && h.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<byte>();
                    foreach (var e in h.EnumerateArray()) list.Add(e.GetByte());
                    r.SessionHash = list.ToArray();
                }
                return r;
            }
        }
    }
}
