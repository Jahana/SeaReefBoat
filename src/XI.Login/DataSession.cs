// C# port of src/login/data_session.cpp (port 54230): the xiloader "data" socket that drives the
// character list (0xA1) and the final character select (0xA2) on behalf of the lobby view socket.
//
// DataSession is transport-agnostic like AuthSession: Handle() takes one received buffer and writes
// through ILobbyChannel (self = this socket, session.View = the lobbyview socket). All per-account
// state lives in LoginSessions, mirroring session_t.

using System;
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using XI.Ipc;
using XI.Persistence;

namespace XI.Login
{
    // The engine's dealerChannel_ send of ipc::CharZone to the world server.
    public interface ICharZoneSink
    {
        void CharZone(uint charId, ushort destinationZoneId);
    }

    public sealed class IpcCharZoneSink : ICharZoneSink
    {
        private readonly Action<byte[]> _send;
        public IpcCharZoneSink(Action<byte[]> send) { _send = send; }

        public void CharZone(uint charId, ushort destinationZoneId) =>
            _send(IpcFraming.ToBytesWithHeader(new XI.Ipc.CharZone(charId, (ZoneId)destinationZoneId)));
    }

    public sealed class LobbySettings
    {
        public byte MaintMode = 0;                // login.MAINT_MODE (get<uint8>: the 0xA1 list filter)
        public bool? MaintModeOn;                 // login.MAINT_MODE (get<bool>: the 0xA2 select); null = MaintMode != 0
        public bool CharacterCreation = true;     // login.CHARACTER_CREATION
        public byte LoginLimit = 0;               // login.LOGIN_LIMIT
        public bool LogUserIp = false;            // login.LOG_USER_IP
        public string ServerName = "Nameless";    // main.SERVER_NAME
        public ushort SearchPort = 54002;         // network.SEARCH_PORT
        public ushort ZonePlayerCap = 700;        // map.ZONE_PLAYER_CAP
        public ushort ZonePlayerGmReserved = 5;   // map.ZONE_PLAYER_GM_RESERVED
        public Func<ushort, bool> IsInstancedZone = _ => false; // loginHelpers::instancedZones()
        public bool ZoneIpFromClient = false;     // network.ZONE_IP_FROM_CLIENT (C# only): ClientAddress.ClientFacingIp
    }

    public static class IpText
    {
        private static readonly Regex Quad = new(@"^(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})$", RegexOptions.CultureInvariant);

        // str2ip: inet_pton(AF_INET) read as a native (little-endian) uint32; 0 on failure.
        public static uint Str2Ip(string s)
        {
            var m = Quad.Match(s ?? "");
            if (!m.Success) return 0;
            uint ip = 0;
            for (int i = 0; i < 4; i++)
            {
                if (!uint.TryParse(m.Groups[i + 1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var b) || b > 255) return 0;
                ip |= b << (8 * i);
            }
            return ip;
        }

        public static string Ip2Str(uint ip) =>
            string.Create(CultureInfo.InvariantCulture, $"{ip & 0xFF}.{(ip >> 8) & 0xFF}.{(ip >> 16) & 0xFF}.{(ip >> 24) & 0xFF}");
    }

    public sealed class DataSession : ILobbyHandler
    {
        private readonly string _ip;
        private readonly IDatabase _db;
        private readonly LoginSessions _sessions;
        private readonly LobbySettings _settings;
        private readonly ICharZoneSink _sink;
        private readonly TimeProvider _time;
        private readonly ILobbyChannel _self;

        public string SessionHash = "";

        // Cached lobby list (lpkt_chr_info2): kept so char deletion/creation can skip slots instead of flattening.
        private byte[] _chrInfo = new byte[ChrInfo2Layout.SizeOf];
        private bool _generatedCharInfo;

        public DataSession(string ipAddress, ILobbyChannel self, IDatabase db, LoginSessions sessions,
                           ICharZoneSink sink, LobbySettings settings, TimeProvider time = null)
        {
            _ip = ipAddress;
            _self = self;
            _db = db;
            _sessions = sessions;
            _sink = sink;
            _settings = settings;
            _time = time ?? TimeProvider.System;
        }

        private uint Characters
        {
            get => BinaryPrimitives.ReadUInt32LittleEndian(_chrInfo.AsSpan(ChrInfo2Layout.OffCharacters));
            set => BinaryPrimitives.WriteUInt32LittleEndian(_chrInfo.AsSpan(ChrInfo2Layout.OffCharacters), value);
        }

        private Span<byte> Slot(int i) =>
            _chrInfo.AsSpan(ChrInfo2Layout.OffCharacterInfo + i * ChrInfo2Layout.CharacterStride, ChrInfo2Layout.CharacterStride);

        // The view session's writes to this socket (data_session::do_write).
        public void Write(byte[] data) => _self.Write(data);

        public void DeleteCharFromCharInfo(uint ffxiId)
        {
            for (int i = 0; i < ChrInfo2Layout.MaxCharacters; i++)
            {
                var s = Slot(i);
                if (BinaryPrimitives.ReadUInt32LittleEndian(s) != ffxiId) continue;
                BinaryPrimitives.WriteUInt16LittleEndian(s.Slice(ChrInfoSub2Layout.OffStatus), 0x01); // Available
                s[ChrInfoSub2Layout.OffCharacterName] = 0x20;     // space shows an empty slot; NULL would show a hume
                s[ChrInfoSub2Layout.OffCharacterName + 1] = 0x00; // terminator so the deleted name is not displayed
            }
        }

        public void AddCharIntoCharInfo(ReadOnlySpan<byte> charInfo)
        {
            // First empty slot (name is a space); the client expects this.
            for (int i = 0; i < ChrInfo2Layout.MaxCharacters; i++)
            {
                var s = Slot(i);
                if (s[ChrInfoSub2Layout.OffCharacterName] != 0x20) continue;
                charInfo.Slice(0, ChrInfoSub2Layout.SizeOf).CopyTo(s);
                break;
            }
        }

        public void RenameCharInCharInfo(uint charId, string newName)
        {
            for (int i = 0; i < ChrInfo2Layout.MaxCharacters; i++)
            {
                var s = Slot(i);
                if (BinaryPrimitives.ReadUInt32LittleEndian(s) != charId) continue;
                var name = s.Slice(ChrInfoSub2Layout.OffCharacterName, 16);
                name.Clear();
                var bytes = Encoding.UTF8.GetBytes(newName);
                bytes.AsSpan(0, Math.Min(bytes.Length, 15)).CopyTo(name);
                s[ChrInfoSub2Layout.OffBitfield] &= unchecked((byte)~ChrInfoSub2Layout.RenamefMask);
                break;
            }
        }

        private static byte[] Padded(byte[] data, int len)
        {
            var b = new byte[Math.Max(len, 0)];
            Array.Copy(data, b, Math.Min(data.Length, b.Length));
            return b;
        }

        // read_func: one received buffer (async_read_some delivers into a zeroed 4096-byte buffer).
        public void Handle(ReadOnlySpan<byte> received)
        {
            var buf = new byte[AuthSession.BufferSize];
            received.Slice(0, Math.Min(received.Length, buf.Length)).CopyTo(buf);

            var sessionHash = _sessions.GetHashFromPacket(_ip, buf);
            if (sessionHash == "")
            {
                sessionHash = SessionHash; // stored session hash
                if (sessionHash == "") return; // "Session requested without valid sessionHash"
            }

            var session = _sessions.GetAuthenticatedSession(_ip, sessionHash);
            if (session.Data == null)
            {
                session.Data = this;
                SessionHash = sessionHash;
            }

            switch (buf[0])
            {
                case 0xA1: HandleCharList(session, buf); break;
                case 0xA2: HandleCharSelect(session, buf); break;
                case 0xFE: session.Data?._self.Write(Array.Empty<byte>()); break; // keep xiloader spinning
            }
        }

        // data_session::handle_error
        public void HandleError() => _sessions.OnDataSessionClosed(_ip, SessionHash);

        private void SendViewError(LoginSession session, ushort code) =>
            session.View?.Write(LoginHelpers.GenerateErrorMessage(code));

        private void HandleCharList(LoginSession session, byte[] buf)
        {
            uint received = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(1));
            if (session.AccountId != received) return;

            session.ServerIp = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(5)); // search-server ip

            uint numContentIds;
            var rset0 = LoginDb.PreparedStmt(_db, "SELECT content_ids FROM accounts WHERE id = ?", session.AccountId);
            if (rset0 != null && rset0.RowsCount() > 0 && rset0.Next())
            {
                numContentIds = rset0.Get<uint>("content_ids");
            }
            else
            {
                _self.Close(); // claimed account does not exist: close so the client errors
                return;
            }

            var rset1 = LoginDb.PreparedStmt(_db,
                "SELECT charid, charname, pos_zone, pos_prevzone, mjob," +
                "race, face, head, body, hands, legs, feet, main, sub," +
                "war, mnk, whm, blm, rdm, thf, pld, drk, bst, brd, rng," +
                "sam, nin, drg, smn, blu, cor, pup, dnc, sch, geo, run, " +
                "gmlevel, nation, size, sjob, COALESCE(char_flags.`rename`, 0) AS `rename`, " +
                "EXISTS(SELECT 1 FROM char_vars " +
                "WHERE char_vars.charid = chars.charid " +
                "AND varname = '[RaceChange]Eligible' AND value > UNIX_TIMESTAMP()) AS race_change " +
                "FROM chars " +
                "INNER JOIN char_stats USING(charid) " +
                "INNER JOIN char_look  USING(charid) " +
                "INNER JOIN char_jobs  USING(charid) " +
                "LEFT JOIN  char_flags USING(charid) " +
                "WHERE accid = ? " +
                "LIMIT ?",
                session.AccountId, numContentIds);
            if (rset1 == null)
            {
                _self.Close();
                return;
            }

            var serverName = Encoding.UTF8.GetBytes(_settings.ServerName);
            var uList = new byte[500];
            uint i = 0;

            if (!_generatedCharInfo)
            {
                _chrInfo = new byte[ChrInfo2Layout.SizeOf];
                BinaryPrimitives.WriteUInt32LittleEndian(_chrInfo.AsSpan(PacketLayout.OffTerminator), LoginPackets.GetTerminator());
                BinaryPrimitives.WriteUInt32LittleEndian(_chrInfo.AsSpan(PacketLayout.OffCommand), 0x20);

                while (rset1.Next() && i < ChrInfo2Layout.MaxCharacters)
                {
                    var name = new byte[16];
                    var dbName = Encoding.UTF8.GetBytes(rset1.Get<string>("charname"));
                    Array.Copy(dbName, name, Math.Min(dbName.Length, 15));

                    int gmlevel = rset1.Get<int>("gmlevel");
                    if (_settings.MaintMode != 0 && gmlevel <= 0) continue;

                    byte worldId = 0;
                    uint charId = rset1.Get<uint>("charid");
                    uint contentId = charId; // content id == char id (USER directory name)
                    ushort charIdMain = (ushort)(charId & 0xFFFF);
                    byte charIdExtra = (byte)((charId >> 16) & 0xFF);

                    var s = Slot((int)i);
                    BinaryPrimitives.WriteUInt32LittleEndian(s, contentId);
                    BinaryPrimitives.WriteUInt16LittleEndian(s.Slice(ChrInfoSub2Layout.OffFfxiIdWorld), charIdMain);
                    BinaryPrimitives.WriteUInt16LittleEndian(s.Slice(ChrInfoSub2Layout.OffWorldId), worldId);
                    BinaryPrimitives.WriteUInt16LittleEndian(s.Slice(ChrInfoSub2Layout.OffStatus), 1); // 0 hidden, 1 available, 2 disabled
                    byte bits = 0;
                    if (rset1.Get<byte>("race_change") != 0) bits |= ChrInfoSub2Layout.RaceChangeMask;
                    if (rset1.Get<byte>("rename") != 0) bits |= ChrInfoSub2Layout.RenamefMask;
                    s[ChrInfoSub2Layout.OffBitfield] = bits;
                    s[ChrInfoSub2Layout.OffFfxiIdWorldTbl] = charIdExtra;
                    name.CopyTo(s.Slice(ChrInfoSub2Layout.OffCharacterName));
                    serverName.AsSpan(0, Math.Min(serverName.Length, 15)).CopyTo(s.Slice(ChrInfoSub2Layout.OffWorldName));

                    ushort zone = rset1.Get<ushort>("pos_zone");
                    byte mainJob = rset1.Get<byte>("mjob");
                    byte lvlMainJob = rset1.Get<byte>((uint)(13 + mainJob)); // column index into the job-level run (quirk: mjob 0 reads `sub`)

                    var mk = s.Slice(ChrInfoSub2Layout.OffCharacterInfo);
                    BinaryPrimitives.WriteUInt16LittleEndian(mk.Slice(TcOperationMakeLayout.OffMonNo), rset1.Get<ushort>("race"));
                    mk[TcOperationMakeLayout.OffMjobNo] = mainJob;
                    mk[TcOperationMakeLayout.OffMjobLevel] = lvlMainJob;
                    mk[TcOperationMakeLayout.OffSjobNo] = unchecked((byte)rset1.Get<ushort>("sjob"));
                    BinaryPrimitives.WriteUInt16LittleEndian(mk.Slice(TcOperationMakeLayout.OffFaceNo), rset1.Get<ushort>("face"));
                    mk[TcOperationMakeLayout.OffTownNo] = rset1.Get<byte>("nation");
                    mk[TcOperationMakeLayout.OffZoneNo] = (byte)zone;
                    mk[TcOperationMakeLayout.OffZoneNo2] = (byte)((zone >> 8) & 1);
                    mk[TcOperationMakeLayout.OffHairNo] = rset1.Get<byte>("face");
                    mk[TcOperationMakeLayout.OffSize] = rset1.Get<byte>("size");

                    string[] grap = { "face", "head", "body", "hands", "legs", "feet", "main", "sub" };
                    for (int g = 0; g < grap.Length; g++)
                        BinaryPrimitives.WriteUInt16LittleEndian(mk.Slice(TcOperationMakeLayout.OffGrapIdTbl + 2 * g), rset1.Get<ushort>(grap[g]));

                    int off = 16 * ((int)i + 1);
                    BinaryPrimitives.WriteUInt32LittleEndian(uList.AsSpan(off), contentId);
                    BinaryPrimitives.WriteUInt16LittleEndian(uList.AsSpan(off + 4), charIdMain);
                    uList[off + 6] = worldId;
                    uList[off + 7] = charIdExtra;

                    ++i;
                    Characters++;
                }

                _generatedCharInfo = true;

                if (_settings.CharacterCreation)
                {
                    // extra empty slots up to the account's content-id allowance
                    while (Characters < numContentIds && Characters < ChrInfo2Layout.MaxCharacters)
                    {
                        var s = Slot((int)Characters);
                        BinaryPrimitives.WriteUInt16LittleEndian(s.Slice(ChrInfoSub2Layout.OffStatus), 0x01);
                        s[ChrInfoSub2Layout.OffCharacterName] = 0x20; // space shows an empty slot; NULL shows a hume
                        Characters++;
                    }
                }

                // maintenance filtering removed every non-GM: stop empty lists from logging in / creating
                if (_settings.MaintMode > 0 && i == 0)
                {
                    SendViewError(session, 332); // COULD_NOT_CONNECT_TO_LOBBY_SERVER
                    return;
                }
            }
            else
            {
                LoginPackets.ClearIdentifier(_chrInfo);
                for (i = 0; i < Characters; i++)
                {
                    var s = Slot((int)i);
                    int off = 16 * ((int)i + 1);
                    BinaryPrimitives.WriteUInt32LittleEndian(uList.AsSpan(off),
                        BinaryPrimitives.ReadUInt32LittleEndian(s));                                            // contentId
                    BinaryPrimitives.WriteUInt16LittleEndian(uList.AsSpan(off + 4),
                        BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(ChrInfoSub2Layout.OffFfxiIdWorld)));    // charIdMain
                    uList[off + 6] = (byte)BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(ChrInfoSub2Layout.OffWorldId));
                    uList[off + 7] = s[ChrInfoSub2Layout.OffFfxiIdWorldTbl];                                    // charIdExtra
                }
            }

            if (session.Data != null)
            {
                uList[0] = 0x03; // send-character-list command in xiloader
                uList[1] = (byte)Characters;
                session.Data._self.Write(Padded(uList, 0x148));
            }

            if (session.View != null)
            {
                // size of packet + 1 uint32 + the actually set number of characters
                uint size = (uint)(PacketLayout.SizeOf + 4 + ChrInfoSub2Layout.SizeOf * (int)Characters);
                BinaryPrimitives.WriteUInt32LittleEndian(_chrInfo.AsSpan(PacketLayout.OffPacketSize), size);
                var hash = MD5.HashData(_chrInfo.AsSpan(0, (int)size));
                LoginPackets.CopyHashIntoPacket(_chrInfo, hash);
                session.View.Write(Padded(_chrInfo, (int)size));
            }
        }

        private void HandleCharSelect(LoginSession session, byte[] buf)
        {
            var key3 = new byte[20];
            Array.Copy(buf, 1, key3, 0, key3.Length);

            var resp = new byte[NextLoginLayout.SizeOf];
            BinaryPrimitives.WriteUInt32LittleEndian(resp.AsSpan(PacketLayout.OffPacketSize), 0x48);
            BinaryPrimitives.WriteUInt32LittleEndian(resp.AsSpan(PacketLayout.OffTerminator), LoginPackets.GetTerminator());
            BinaryPrimitives.WriteUInt32LittleEndian(resp.AsSpan(PacketLayout.OffCommand), 0x0B);

            if (session.AccountId == 0)
            {
                // "login data corrupt (0xA2)"
                _self.Write(LoginHelpers.GenerateErrorMessage(332));
                _self.Close();
                return;
            }

            // the client got the same 16 bytes from xi_profile (accounts::keyValue), the loader's are placeholders
            {
                var profile = LoginDb.PreparedStmt(_db, "SELECT session_hash FROM accounts_profile WHERE accid = ?", session.AccountId);
                if (profile != null && profile.Next())
                {
                    var h = profile.GetBlobBytes("session_hash");
                    if (h.Length == 16) MD5.HashData(h).CopyTo(key3, 0);
                }
            }

            uint charid = session.RequestedCharacterId;
            uint accountIp = IpText.Str2Ip(_ip);

            uint zoneIp = 0;
            ushort zonePort = 0;
            ushort zoneId = 0;
            ushort prevZone = 0;
            ushort gmlevel = 0;

            var rset = LoginDb.PreparedStmt(_db,
                "SELECT zoneip, zoneport, zoneid, pos_prevzone, gmlevel, accid, charname " +
                "FROM zone_settings, chars " +
                "WHERE IF(pos_zone = 0, zoneid = pos_prevzone, zoneid = pos_zone) AND charid = ? AND accid = ?",
                charid, session.AccountId);

            if (rset != null && rset.RowsCount() > 0 && rset.Next())
            {
                zoneId = rset.Get<ushort>("zoneid");
                prevZone = rset.Get<ushort>("pos_prevzone");
                gmlevel = rset.Get<ushort>("gmlevel");

                // the client hashes its counter after one more step for the 0x0B below and two at zone connect
                if (session.KeyCounter != 0)
                    BinaryPrimitives.WriteUInt32LittleEndian(key3.AsSpan(16), session.KeyCounter + 3);

                zoneIp = IpText.Str2Ip(rset.Get<string>("zoneip"));
                zonePort = rset.Get<ushort>("zoneport");

                BinaryPrimitives.WriteUInt32LittleEndian(resp.AsSpan(NextLoginLayout.OffServerIp),
                    XI.Common.ClientAddress.ClientFacingIp(zoneIp, session.ServerIp, _settings.ZoneIpFromClient));
                BinaryPrimitives.WriteUInt32LittleEndian(resp.AsSpan(NextLoginLayout.OffServerPort), zonePort);
                BinaryPrimitives.WriteUInt32LittleEndian(resp.AsSpan(NextLoginLayout.OffCacheIp), session.ServerIp); // search-server ip
                BinaryPrimitives.WriteUInt32LittleEndian(resp.AsSpan(NextLoginLayout.OffCachePort), _settings.SearchPort);

                var dbName = Encoding.UTF8.GetBytes(rset.Get<string>("charname"));
                Array.Copy(dbName, 0, resp, NextLoginLayout.OffCharacterName, Math.Min(dbName.Length, 15));

                BinaryPrimitives.WriteUInt32LittleEndian(resp.AsSpan(NextLoginLayout.OffFfxiId), charid);
                BinaryPrimitives.WriteUInt32LittleEndian(resp.AsSpan(NextLoginLayout.OffFfxiIdWorld), charid & 0xFFFF);
                BinaryPrimitives.WriteUInt32LittleEndian(resp.AsSpan(NextLoginLayout.OffServerId), (charid >> 16) & 0xFF);

                // If the client was zoning out but never seen at the destination past 2 minutes, drop the old session.
                var rset2 = LoginDb.PreparedStmt(_db,
                    "SELECT * FROM accounts_sessions " +
                    "WHERE accid = ? AND charid = ? AND client_port = '0' AND last_zoneout_time <= SUBTIME(NOW(), \"00:02:00\")",
                    session.AccountId, charid);
                if (rset2 != null && rset2.RowsCount() != 0 && rset2.Next())
                    LoginDb.PreparedStmt(_db, "DELETE FROM accounts_sessions WHERE accid = ? AND charid = ?", session.AccountId, charid);

                ushort sessionCount = 0;
                var rset0 = LoginDb.PreparedStmt(_db, "SELECT COUNT(client_addr) FROM accounts_sessions WHERE client_addr = ?", accountIp);
                if (rset0 != null && rset0.RowsCount() != 0 && rset0.Next())
                    sessionCount = rset0.Get<ushort>("COUNT(client_addr)");

                DateTimeOffset exceptionTime = DateTimeOffset.MinValue;
                var rset3 = LoginDb.PreparedStmt(_db, "SELECT UNIX_TIMESTAMP(exception) FROM ip_exceptions WHERE accid = ?", session.AccountId);
                if (rset3 != null && rset3.RowsCount() != 0 && rset3.Next())
                    exceptionTime = DateTimeOffset.FromUnixTimeSeconds((long)rset3.Get<ulong>("UNIX_TIMESTAMP(exception)"));

                bool isNotMaint = !(_settings.MaintModeOn ?? _settings.MaintMode != 0);
                bool excepted = exceptionTime > _time.GetUtcNow();
                bool loginLimitOk = _settings.LoginLimit == 0 || sessionCount < _settings.LoginLimit || excepted;
                bool isGm = gmlevel > 0;

                if (IsZoneAtPlayerCap(zoneId, isGm) && session.View != null)
                {
                    SendViewError(session, 208); // WORLD_IS_FULL
                    return;
                }

                if ((isNotMaint && loginLimitOk) || isGm)
                {
                    if (prevZone == 0)
                        LoginDb.PreparedStmt(_db, "UPDATE chars SET pos_prevzone = ? WHERE charid = ?", zoneId, charid);

                    BinaryPrimitives.WriteUInt32LittleEndian(resp.AsSpan(NextLoginLayout.OffCacheIp), session.ServerIp);
                    BinaryPrimitives.WriteUInt32LittleEndian(resp.AsSpan(NextLoginLayout.OffCachePort), _settings.SearchPort);

                    var rset1 = LoginDb.PreparedStmt(_db, "SELECT charid FROM accounts_sessions WHERE accid = ? LIMIT 1", session.AccountId);
                    if (rset1 != null && rset1.RowsCount() != 0 && rset1.Next())
                    {
                        // character already logged in (session still exists): refuse
                        if (rset1.Get<uint>("charid") == session.RequestedCharacterId && session.View != null)
                        {
                            SendViewError(session, 201); // CHARACTER_ALREADY_LOGGED_IN
                            return;
                        }
                    }

                    var ins = _settings.ZoneIpFromClient
                        ? LoginDb.PreparedStmt(_db,
                            "INSERT INTO accounts_sessions(accid, charid, session_key, server_addr, server_port, client_addr, version_mismatch, client_server_addr) " +
                            "VALUES(?, ?, ?, ?, ?, ?, ?, ?)",
                            session.AccountId, charid, key3, zoneIp, zonePort, accountIp, session.VersionMismatch ? 1 : 0, session.ServerIp)
                        : LoginDb.PreparedStmt(_db,
                            "INSERT INTO accounts_sessions(accid, charid, session_key, server_addr, server_port, client_addr, version_mismatch) " +
                            "VALUES(?, ?, ?, ?, ?, ?, ?)",
                            session.AccountId, charid, key3, zoneIp, zonePort, accountIp, session.VersionMismatch ? 1 : 0);
                    if (ins == null && session.View != null)
                    {
                        SendViewError(session, 305); // UNABLE_TO_CONNECT_TO_WORLD_SERVER
                        return;
                    }
                }
                else if (session.View != null)
                {
                    SendViewError(session, 332); // COULD_NOT_CONNECT_TO_LOBBY_SERVER
                    return;
                }
            }
            else if (session.View != null)
            {
                SendViewError(session, 305);
                return;
            }

            // a disabled zone has no map address
            if (BinaryPrimitives.ReadUInt32LittleEndian(resp.AsSpan(NextLoginLayout.OffServerIp)) == 0)
            {
                SendViewError(session, 305);
                return;
            }

            LoginPackets.CopyHashIntoPacket(resp, MD5.HashData(resp));

            if (session.View != null)
            {
                var view = session.View;
                view.Write(resp);
                view.Close(); // the client waits for us to close the socket
                session.View = null;

                _generatedCharInfo = false; // regenerate the char list next time we log out

                LoginDb.PreparedStmt(_db, "UPDATE char_flags SET disconnecting = 0 WHERE charid = ?", charid);
                LoginDb.PreparedStmt(_db, "UPDATE char_stats SET zoning = 2 WHERE charid = ?", charid);

                _sink.CharZone(charid, zoneId);
            }

            if (_settings.LogUserIp)
            {
                var timeAndDate = _time.GetLocalNow().ToString("yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture);
                LoginDb.PreparedStmt(_db,
                    "INSERT INTO account_ip_record(login_time,accid,charid,client_ip) VALUES (?, ?, ?, ?)",
                    timeAndDate, session.AccountId, charid, IpText.Ip2Str(accountIp));
            }
        }

        // loginHelpers::isZoneAtPlayerCap
        private bool IsZoneAtPlayerCap(ushort zoneId, bool isGm)
        {
            var cap = _settings.ZonePlayerCap;
            if (cap == 0) return false;

            var reserved = _settings.ZonePlayerGmReserved;
            ushort threshold = isGm ? cap : (ushort)(cap > reserved ? cap - reserved : 0);

            if (_settings.IsInstancedZone(zoneId)) return false;

            var rset = LoginDb.PreparedStmt(_db,
                "SELECT COUNT(*) AS pop FROM accounts_sessions s JOIN chars c ON c.charid = s.charid WHERE c.pos_zone = ?",
                zoneId);
            if (rset != null && rset.RowsCount() > 0 && rset.Next())
                return rset.Get<uint>("pop") >= threshold;
            return false;
        }
    }
}
