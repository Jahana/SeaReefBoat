// Accept loops for the lobby view (54001) and data (54230) ports: handler<view_session> and
// handler<data_session> in handler.h / handler_session.cpp.
//
// The engine drives these sockets with next_layer() (plain TCP; the TLS handshake only happens on the
// xiloader auth port), arms one async_read_some, and re-arms it only when a write on that socket
// completes (handler_session::write_func -> do_read). LobbyConnection keeps that exact contract: the
// first read is armed on start, every completed Write re-arms one more read. A socket error or close
// ends the connection and runs handle_error.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace XI.Login
{
    // The per-socket half of a session: one received buffer in, error/close out.
    public interface ILobbyHandler
    {
        void Handle(ReadOnlySpan<byte> received);
        void HandleError();
    }

    public sealed class LobbyConnection : ILobbyChannel
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly CancellationToken _ct;
        private readonly SemaphoreSlim _armed = new(1, 1); // the first read is armed by start()
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private int _closed;
        internal Action<string> Log;

        public string RemoteIp { get; }

        public LobbyConnection(TcpClient client, CancellationToken ct)
        {
            _client = client;
            _stream = client.GetStream();
            _ct = ct;
            RemoteIp = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

            // handler_session::start: keepalives for long sessions (view_session in particular)
            try
            {
                var sock = client.Client;
                sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                sock.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 5 * 60);
                sock.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 10);
                sock.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 5 * 60);
            }
            catch (Exception) { }
        }

        // handler_session::do_write: async write, then write_func re-arms the read.
        public void Write(byte[] data)
        {
            if (Volatile.Read(ref _closed) != 0) { Log?.Invoke($"write of {data.Length} bytes after close"); return; }
            Log?.Invoke($"write {data.Length} bytes: {Convert.ToHexString(data.AsSpan(0, Math.Min(data.Length, 12)))}");
            _ = WriteAsync(data);
        }

        private async Task WriteAsync(byte[] data)
        {
            try
            {
                await _writeLock.WaitAsync(_ct);
                try
                {
                    if (data.Length > 0) await _stream.WriteAsync(data, _ct);
                    await _stream.FlushAsync(_ct);
                }
                finally { _writeLock.Release(); }

                try { _armed.Release(); } catch (SemaphoreFullException) { }
            }
            catch (Exception) { Close(); }
        }

        public void Close()
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0) return;
            try { _client.Client.Shutdown(SocketShutdown.Both); } catch (Exception) { }
            try { _client.Close(); } catch (Exception) { }
        }

        // mainThread: the lock every login socket's request handling takes (handler.h: the read handlers run on the main
        // io_context, so data_session and view_session never touch session_t, or make a char id, at the same time)
        public async Task Run(ILobbyHandler handler, Action<string> log = null, object mainThread = null)
        {
            mainThread ??= new object();
            var buffer = new byte[AuthSession.BufferSize];
            try
            {
                while (true)
                {
                    await _armed.WaitAsync(_ct);
                    Array.Clear(buffer);
                    int n = await _stream.ReadAsync(buffer, _ct);
                    if (n <= 0) { log?.Invoke("closed by peer"); break; }
                    log?.Invoke($"read {n} bytes: {Convert.ToHexString(buffer.AsSpan(0, Math.Min(n, 12)))}");
                    lock (mainThread) handler.Handle(buffer.AsSpan(0, n));
                }
            }
            catch (Exception e) { log?.Invoke($"ended: {e.GetType().Name}: {e.Message}"); }

            Close();
            lock (mainThread) handler.HandleError();
        }
    }

    public sealed class LobbyServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly Func<string, ILobbyChannel, ILobbyHandler> _factory;
        private readonly CancellationTokenSource _cts = new();
        private readonly string _name;
        private readonly Action<string> _log;
        private readonly object _mainThread;

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        // factory(ip, channel) builds the session (ViewSession / DataSession) for a new socket.
        public LobbyServer(IPAddress bind, int port, Func<string, ILobbyChannel, ILobbyHandler> factory,
                           string name = "lobby", Action<string> log = null, object mainThread = null)
        {
            _mainThread = mainThread ?? new object();
            _name = name;
            _log = log;
            _listener = new TcpListener(bind, port);
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _factory = factory;
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
                catch (SocketException) { continue; }

                var conn = new LobbyConnection(client, _cts.Token);
                var handler = _factory(conn.RemoteIp, conn);
                Action<string> log = null;
                if (_log != null)
                {
                    string tag = $"login: {_name} {conn.RemoteIp}: ";
                    log = s => _log(tag + s);
                    log("connection");
                    conn.Log = log;
                }
                _ = Task.Run(() => conn.Run(handler, log, _mainThread));
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
