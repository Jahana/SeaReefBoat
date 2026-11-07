using System;
using XI.Ipc;
using XI.Map;

namespace XI.Zone
{
    // Port of src/map/ipc_client.cpp's receiving side: the world routes a message here by the map process's routing id
    // (its zone_settings address). CharZone and KillSession move a char's session between map processes; the rest is
    // the game's (ZoneHost.OnIpc).
    public sealed class MapIpcClient
    {
        private readonly MapSessionContainer sessions_;
        private readonly ZoneHost zone_;
        private readonly Action<string> log_;

        public MapIpcClient(MapSessionContainer sessions, ZoneHost zone, Action<string> log = null)
        {
            sessions_ = sessions;
            zone_ = zone;
            log_ = log ?? (_ => { });
        }

        // the dealer's frame: decoded, then handled
        public void HandleFrame(byte[] frame)
        {
            IIpcMessage decoded;
            try { decoded = IpcFraming.Decode(frame); }
            catch (Exception e)
            {
                log_($"map: IPC message 0x{(frame.Length > 0 ? frame[0] : 0):X2} not decoded: {e.Message}");
                return;
            }
            HandleMessage(decoded);
        }

        public void HandleMessage(IIpcMessage message)
        {
            switch (message)
            {
                case CharZone m:
                    HandleCharZone(m);
                    break;
                case KillSession m:
                    HandleKillSession(m);
                    break;
                case not null:
                    zone_?.OnIpc(message);
                    break;
            }
        }

        // IPCClient::handleMessage_CharZone: the char is on its way here (the 0x00B another process, or this one, sent
        // named this process): a pending session its 0x00A takes up, or a session it still has kept alive
        private void HandleCharZone(CharZone message)
        {
            var session = sessions_.GetSessionByCharId(message.CharId);
            if (session != null) session.TapLastUpdate(sessions_.Now); // Update in case of edge case
            else sessions_.CreatePendingSession(message.CharId);
        }

        // IPCClient::handleMessage_KillSession: the char logged in on another process (KillSession from its recv_parse,
        // routed to the process of its previous zone); the session it left here waiting for a zone change closes. A
        // pending session is never PendingZone, so its branch does nothing (as in C++, whose destroySession would only
        // look for it among the live sessions).
        private void HandleKillSession(KillSession message)
        {
            var toDelete = sessions_.GetSessionByCharId(message.VictimId);
            if (toDelete != null)
            {
                if (toDelete.Blowfish.Status == BlowfishStatus.PendingZone)
                {
                    log_($"Closing session of charid {message.VictimId} on request of other process");
                    sessions_.DestroySession(toDelete);
                }
                else
                {
                    log_($"KillSession for charid {message.VictimId} not needed");
                }
            }

            var pending = sessions_.GetPendingSessionByCharId(message.VictimId);
            if (pending != null && pending.Blowfish.Status == BlowfishStatus.PendingZone)
            {
                log_($"Closing pending session of charid {message.VictimId} on request of other process");
                sessions_.DestroySession(pending);
            }
        }
    }
}
