// src/profile/profile_engine.cpp: the profile server (friend lists, status, files) and the IRC server (presence,
// message notices), both TLS 1.3 behind the xiloader relay. Every connection opens with the account id and its
// session hash.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using XI.Persistence;

namespace XI.Profile
{
    public sealed class ProfileEngine : IDisposable
    {
        private static readonly TimeSpan HandshakeDeadline = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(1);
        private const int ConnectionsPerPeer = 16;

        // hardcoded in polcore
        public const int DefaultIrcPort = 51240;

        private delegate Task Session(Stream stream, TcpClient client, string peer, Credential credential);

        private readonly X509Certificate2 _cert;
        private readonly ProfileAccounts _accounts;
        private readonly ProfileRequests _requests;
        private readonly Action<string> _log;
        private readonly Action<string> _debug;
        private readonly TcpListener _profile;
        private readonly TcpListener _irc;
        private readonly Dictionary<(int Port, string Address), int> _connections = new(); // port, address
        private readonly CancellationTokenSource _cts = new();
        private Timer _refresh;

        public PresenceManager Presence { get; }

        public int ProfilePort => ((IPEndPoint)_profile.LocalEndpoint).Port;
        public int IrcPort => ((IPEndPoint)_irc.LocalEndpoint).Port;

        public ProfileEngine(IDatabase db, X509Certificate2 cert, IPAddress bind, int profilePort, int ircPort, Action<string> log, Action<string> debug = null)
        {
            _cert = cert;
            _log = log ?? (_ => { });
            _debug = debug;
            _accounts = new ProfileAccounts(db);
            var friends = new ProfileFriends(db, _accounts);
            Presence = new PresenceManager(_accounts, friends, _log, _debug);
            _requests = new ProfileRequests(db, _accounts, new ProfileFiles(db), friends, _log);
            _profile = new TcpListener(bind, profilePort);
            _irc = new TcpListener(bind, ircPort);
        }

        public void Start()
        {
            _profile.Start();
            _irc.Start();
            _ = AcceptAsync(_profile, (stream, client, peer, credential) =>
                new ProfileSession(stream, peer, credential.AccountId, Presence, _requests, _log, _debug).RunAsync());
            _ = AcceptAsync(_irc, RunIrcSessionAsync);
            _refresh = new Timer(_ =>
            {
                try
                {
                    lock (Presence.Sync) Presence.RefreshCredentials();
                }
                catch (Exception e) { _log($"credential refresh failed: {e.Message}"); }
            }, null, RefreshInterval, RefreshInterval);
            _log($"listening on {ProfilePort} (profile) and {IrcPort} (irc)");
        }

        // runIrcSession
        private async Task RunIrcSessionAsync(Stream stream, TcpClient client, string peer, Credential credential)
        {
            var session = new IrcSession(stream, client, (IPEndPoint)client.Client.RemoteEndPoint, (IPEndPoint)client.Client.LocalEndPoint, peer, credential,
                                         Presence, _accounts, _log, _debug);
            try
            {
                await session.RunAsync();
            }
            catch (Exception e)
            {
                _log($"{peer} session error: {e.Message}");
            }
            // not in the destructor, presence may already be gone at shutdown
            session.SignOff();
        }

        private async Task AcceptAsync(TcpListener listener, Session session)
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            while (!_cts.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await listener.AcceptTcpClientAsync(_cts.Token); }
                catch (OperationCanceledException) { return; }
                catch (ObjectDisposedException) { return; }
                catch (Exception e)
                {
                    _log($"failed to accept connection: {e.Message}");
                    await Task.Delay(100);
                    continue;
                }

                string address;
                try { address = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString(); }
                catch (Exception) { client.Dispose(); continue; }
                var peer = (port, address);
                lock (_connections)
                {
                    _connections.TryGetValue(peer, out int open);
                    if (open >= ConnectionsPerPeer)
                    {
                        client.Dispose();
                        continue;
                    }
                    _connections[peer] = open + 1;
                }
                _ = Task.Run(() => ServeAsync(client, peer, session));
            }
        }

        private async Task ServeAsync(TcpClient client, (int Port, string Address) peer, Session session)
        {
            try
            {
                using (client)
                {
                    var stream = new SslStream(client.GetStream(), false);
                    bool handshake;
                    using (var cts = new CancellationTokenSource(HandshakeDeadline))
                    {
                        try
                        {
                            await stream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                            {
                                ServerCertificate = _cert,
                                EnabledSslProtocols = SslProtocols.Tls13,
                                ClientCertificateRequired = false,
                            }, cts.Token);
                            handshake = true;
                        }
                        catch (Exception) { handshake = false; }
                    }
                    if (handshake)
                    {
                        var credential = await AuthenticateAsync(stream);
                        if (credential == null)
                        {
                            _log($"{peer.Address} refused an unverified connection");
                        }
                        else
                        {
                            try
                            {
                                await session(stream, client, peer.Address, credential.Value);
                            }
                            catch (Exception e)
                            {
                                _log($"{peer.Address} session error: {e.Message}");
                            }
                        }
                    }
                    stream.Dispose();
                }
            }
            finally
            {
                lock (_connections)
                {
                    if (--_connections[peer] == 0) _connections.Remove(peer);
                }
            }
        }

        // authenticate: the relay opens every connection with the account id and its session hash; an expired hash is
        // fine while the account is online
        private async Task<Credential?> AuthenticateAsync(Stream stream)
        {
            var hello = new byte[4 + 16];
            using (var cts = new CancellationTokenSource(HandshakeDeadline))
            {
                try { await stream.ReadExactlyAsync(hello, cts.Token); }
                catch (Exception) { return null; }
            }
            var credential = new Credential { AccountId = Le.U32(hello, 0), SessionHash = hello.AsSpan(4, 16).ToArray() };
            lock (Presence.Sync)
            {
                var fresh = _accounts.Freshness(credential);
                if (fresh == null || (!fresh.Value && !Presence.IsOnline(credential.AccountId))) return null;
            }
            return credential;
        }

        // ProfileApplication::createEngine: certificateHelpers::generateSelfSignedCert("profile", true) -- <name>.key and
        // <name>.cert in the server root, an EC P-256 pair made (and written) on first start; then PFX round-tripped so
        // SslStream on Windows can use the private key
        public static X509Certificate2 LoadOrCreateCertificate(string root, string authIp, Action<string> log)
        {
            string keyFile = Path.Combine(root, "profile.key"), certFile = Path.Combine(root, "profile.cert");
            if (!File.Exists(keyFile) && !File.Exists(certFile))
            {
                log?.Invoke("Generating self-signed certificate");
                using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                var cn = (authIp ?? string.Empty);
                if (cn.Length > 16) cn = cn.Substring(0, 16);
                var subject = new X500DistinguishedNameBuilder();
                // the builder encodes in reverse: O then CN on the wire, as X509_NAME_add_entry_by_txt does
                subject.AddCommonName(cn.Length > 0 ? cn : "0.0.0.0");
                subject.AddOrganizationName("LSB self-signed certificate for profile server");
                var request = new CertificateRequest(subject.Build(), key, HashAlgorithmName.SHA384);
                var now = DateTimeOffset.UtcNow;
                using var created = request.CreateSelfSigned(now, now.AddSeconds(31536000L * 20)); // expires 20 years from now
                File.WriteAllText(keyFile, key.ExportPkcs8PrivateKeyPem());
                File.WriteAllText(certFile, created.ExportCertificatePem());
            }
            using var pem = X509Certificate2.CreateFromPemFile(certFile, keyFile);
            log?.Invoke($"Found existing {certFile}: {pem.Issuer}");
            if (pem.NotAfter < DateTime.Now || pem.NotBefore > DateTime.Now)
            {
                log?.Invoke($"Existing {certFile} is not valid for the current time. Please regenerate or obtain a new certificate.");
            }
            return X509CertificateLoader.LoadPkcs12(pem.Export(X509ContentType.Pfx), null);
        }

        // an in-memory EC P-256 certificate (tests)
        public static X509Certificate2 CreateEphemeralCertificate()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var request = new CertificateRequest("CN=127.0.0.1", key, HashAlgorithmName.SHA384);
            using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
            return X509CertificateLoader.LoadPkcs12(created.Export(X509ContentType.Pfx), null);
        }

        public void Dispose()
        {
            _cts.Cancel();
            _refresh?.Dispose();
            try { _profile.Stop(); } catch { }
            try { _irc.Stop(); } catch { }
            _cts.Dispose();
        }
    }
}
