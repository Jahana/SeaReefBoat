// TLS socket loop for the xiloader auth port (handler<auth_session> in handler.h, default 54231).
// Each read is treated as one request (async_read_some semantics), the reply is written, and the
// loop re-arms the read; a null reply or Close ends the session.

using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using XI.Persistence;

namespace XI.Login
{
    public static class SelfSignedCert
    {
        // certificateHelpers::generateSelfSignedCert equivalent (RSA-2048, 10y), kept in memory.
        public static X509Certificate2 Create(string subject = "CN=LandSandBoat")
        {
            using var rsa = RSA.Create(2048);
            var req = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));
            // Round-trip through PFX so the private key is usable by SslStream on Windows.
            return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null);
        }
    }

    public sealed class AuthServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly X509Certificate2 _cert;
        private readonly IDatabase _db;
        private readonly OtpStore _otp;
        private readonly LoginSessions _sessions;
        private readonly IAccountLoginSink _sink;
        private readonly AuthSettings _settings;
        private readonly CancellationTokenSource _cts = new();
        private readonly object _mainThread;

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        // mainThread: the lock every login socket's request handling takes (ConnectEngine.MainThread); a server of its
        // own when none is given
        public AuthServer(IPAddress bind, int port, X509Certificate2 cert, IDatabase db, LoginSessions sessions,
                          IAccountLoginSink sink, AuthSettings settings, object mainThread = null)
        {
            _listener = new TcpListener(bind, port);
            _cert = cert;
            _db = db;
            _otp = new OtpStore(db);
            _sessions = sessions;
            _sink = sink;
            _settings = settings;
            _mainThread = mainThread ?? new object();
        }

        public Task Start()
        {
            _listener.Start();
            return Task.Run(AcceptLoop);
        }

        private async Task AcceptLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await _listener.AcceptTcpClientAsync(_cts.Token); }
                catch (OperationCanceledException) { return; }
                catch (ObjectDisposedException) { return; }
                _ = Task.Run(() => RunSession(client));
            }
        }

        private async Task RunSession(TcpClient client)
        {
            using (client)
            {
                var ip = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                _settings.Log?.Invoke($"login: auth connection from {ip}");
                var session = new AuthSession(ip, _db, _otp, _sessions, _sink, _settings);
                try
                {
                    using var tls = new SslStream(client.GetStream(), false);
                    await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                    {
                        ServerCertificate = _cert,
                        EnabledSslProtocols = SslProtocols.Tls13,
                        ClientCertificateRequired = false,
                    }, _cts.Token);

                    var buffer = new byte[AuthSession.BufferSize];
                    while (true)
                    {
                        int n = await tls.ReadAsync(buffer, _cts.Token);
                        if (n <= 0) return;

                        // handler.h: the socket's read handler runs on the main io_context, one request at a time
                        // across every login socket (auth_session::read_func's MAX(id) + 1 account id relies on it)
                        AuthReply reply;
                        lock (_mainThread) reply = session.Handle(buffer.AsSpan(0, n));
                        if (reply?.Data == null) return;

                        if (_settings.Log != null)
                        {
                            var m = System.Text.RegularExpressions.Regex.Match(System.Text.Encoding.UTF8.GetString(reply.Data), "\"result\":([0-9]+)");
                            _settings.Log($"login: auth reply to {ip}: {reply.Data.Length} bytes, result {(m.Success ? m.Groups[1].Value : "-")}");
                        }
                        await tls.WriteAsync(reply.Data, _cts.Token);
                        await tls.FlushAsync(_cts.Token);
                        if (reply.Close) return;
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception e)
                {
                    // dropped connections and port scanners are noisy: only logged when a log is set
                    _settings.Log?.Invoke($"login: auth session from {ip} ended: {e.GetType().Name}: {e.Message}");
                }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
            _cts.Dispose();
        }
    }
}
