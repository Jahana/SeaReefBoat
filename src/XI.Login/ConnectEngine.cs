// C# port of src/login/connect_engine.cpp: owns the three login listeners (auth 54231, data 54230,
// view 54001), the shared authenticated-session table and the 15 minute stale-session sweep.
//
// The engine's ZMQ dealer (registerDealer + routing id from LOGIN_AUTH_IP:PORT) is not created here:
// the callers pass the already-built IAccountLoginSink / ICharZoneSink, so any transport can sit behind them.

using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using XI.Persistence;

namespace XI.Login
{
    public sealed class ConnectConfig
    {
        public IPAddress Bind = IPAddress.Any;
        public int AuthPort = 54231;  // network.LOGIN_AUTH_PORT
        public int DataPort = 54230;  // network.LOGIN_DATA_PORT
        public int ViewPort = 54001;  // network.LOGIN_VIEW_PORT
        public Action<string> Log;    // connection open / close lines; null = silent
    }

    public sealed class ConnectEngine : IDisposable
    {
        private readonly AuthServer _auth;
        private readonly LobbyServer _data;
        private readonly LobbyServer _view;
        private readonly LoginSessions _sessions;
        private readonly Timer _cleanup;

        // The scheduler's main io_context: handler.h makes every acceptor and socket on it, so the auth, data and view
        // sessions' read_func (and handle_error) all run on the main thread, one at a time. Taken around each of them
        // here; without it two account creations race to the same MAX(id) + 1 (a duplicate key and
        // LOGIN_ERROR_CREATE) and two char creations to the same charid.
        public readonly object MainThread = new object();

        public int AuthPort => _auth.Port;
        public int DataPort => _data.Port;
        public int ViewPort => _view.Port;

        public ConnectEngine(ConnectConfig cfg, IDatabase db, LoginSessions sessions, X509Certificate2 cert,
                             IAccountLoginSink accountSink, ICharZoneSink zoneSink,
                             AuthSettings authSettings, LobbySettings lobbySettings, ViewSettings viewSettings)
        {
            _sessions = sessions;
            var otp = new OtpStore(db);
            var badNames = BadNameFilter.Load(viewSettings.BadNamesPath);

            _auth = new AuthServer(cfg.Bind, cfg.AuthPort, cert, db, sessions, accountSink, authSettings, MainThread);
            _data = new LobbyServer(cfg.Bind, cfg.DataPort,
                (ip, ch) => new DataSession(ip, ch, db, sessions, zoneSink, lobbySettings), "data", cfg.Log, MainThread);
            _view = new LobbyServer(cfg.Bind, cfg.ViewPort,
                (ip, ch) => new ViewSession(ip, ch, db, sessions, otp, viewSettings, badNames), "view", cfg.Log, MainThread);

            // intervalOnMainThread(kSessionCleanTime, periodicCleanup)
            _cleanup = new Timer(_ => { lock (MainThread) _sessions.PeriodicCleanup(); }, null,
                LoginSessions.SessionCleanTime, LoginSessions.SessionCleanTime);
        }

        public Task Start() => Task.WhenAll(_auth.Start(), _data.Start(), _view.Start());

        public void Dispose()
        {
            _cleanup.Dispose();
            _auth.Dispose();
            _data.Dispose();
            _view.Dispose();
            _sessions.Clear();
        }
    }
}
