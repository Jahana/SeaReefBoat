// C# port of src/login/view_session.cpp (port 54001): the lobby "view" socket that drives version
// check, world list, character select / create / delete / rename. Also ports the name rules used by
// it (login_helpers.cpp validateCharacterName / characterCreationError / createCharacter /
// saveCharacter).
//
// Transport-agnostic like DataSession: Handle() takes one received buffer, replies go through
// ILobbyChannel. ViewSession itself is the ILobbyChannel stored in LoginSession.View so writes made
// by the data session also step the client's key counter (view_session::do_write).

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using XI.Persistence;

namespace XI.Login
{
    public sealed class ViewSettings
    {
        public bool CharacterDeletion = true;           // login.CHARACTER_DELETION
        public byte MaintMode = 0;                      // login.MAINT_MODE
        public bool CharacterCreation = true;           // login.CHARACTER_CREATION
        public string ClientVer = "30000000_0";         // login.CLIENT_VER
        public byte VerLock = 0;                        // login.VER_LOCK
        public string ServerName = "Nameless";          // main.SERVER_NAME
        public bool NewCharacterCutscene = true;        // main.NEW_CHARACTER_CUTSCENE
        public bool DisableMobNpcCharNames = false;     // login.DISABLE_MOB_NPC_CHAR_NAMES
        public IReadOnlyList<string> BannedWords = Array.Empty<string>(); // login.BANNED_WORDS_LIST
        public Func<string, bool> IsEntityName = _ => false; // entityNames().contains(comparableName(name))
        public string BadNamesPath = "res/badnames.bin";
        public Action<string> Log;                      // the C++ ShowError / ShowWarning lines, when set

        public bool RiseOfZilart, ChainsOfPromathia, TreasuresOfAhtUrghan, WingsOfTheGoddess,
            ACrystallineProphecy, AMoogleKupodEtat, AShantottoAscension, VisionsOfAbyssea,
            ScarsOfAbyssea, HeroesOfAbyssea, SeekersOfAdoulin;
        public bool MogWardrobe3, MogWardrobe4, MogWardrobe5, MogWardrobe6, MogWardrobe7, MogWardrobe8;

        // xirand::GetRandomNumber(n): [0, n)
        public Func<int, int> Random = n => System.Random.Shared.Next(n);
    }

    // res/badnames.bin: "BNF1", uint32 counts[4] (exact, substr, prefix, suffix), then 4 blocks of MD5 digests.
    public sealed class BadNameFilter
    {
        private readonly HashSet<string> _exact = new(), _substr = new(), _prefix = new(), _suffix = new();

        public static BadNameFilter Load(string path)
        {
            var f = new BadNameFilter();
            try
            {
                using var fs = File.OpenRead(path);
                var head = new byte[20];
                if (fs.Read(head, 0, 20) != 20 || Encoding.ASCII.GetString(head, 0, 4) != "BNF1") return f;
                var sets = new[] { f._exact, f._substr, f._prefix, f._suffix };
                for (int k = 0; k < 4; k++)
                {
                    uint count = BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(4 + k * 4));
                    var h = new byte[16];
                    for (uint i = 0; i < count && fs.Read(h, 0, 16) == 16; i++) sets[k].Add(LoginSessions.HashKey(h));
                }
            }
            catch (IOException) { }
            return f;
        }

        private static string Md5(byte[] b, int off, int len) =>
            LoginSessions.HashKey(MD5.HashData(b.AsSpan(off, len)));

        // isVulgarName: retail client logic, run on the lowercased name.
        public bool IsVulgar(string name)
        {
            var canon = Encoding.Latin1.GetBytes(name);
            for (int i = 0; i < canon.Length; i++)
                if (canon[i] >= (byte)'A' && canon[i] <= (byte)'Z') canon[i] += 32;
            int n = canon.Length;

            if (_exact.Contains(Md5(canon, 0, n))) return true;

            for (int i = 0; i < n; i++)
                for (int len = 2; i + len <= n; len++)
                {
                    var h = Md5(canon, i, len);
                    if (_substr.Contains(h) || (i == 0 && _prefix.Contains(h))) return true;
                }

            // Retail only cares about the leftmost suffix match.
            for (int i = 0; i < n; i++)
                for (int len = 2; i + len <= n; len++)
                    if (_suffix.Contains(Md5(canon, i, len))) return i + len == n;

            return false;
        }
    }

    public sealed class ViewSession : ILobbyChannel, ILobbyHandler
    {
        private readonly string _ip;
        private readonly ILobbyChannel _self;
        private readonly IDatabase _db;
        private readonly LoginSessions _sessions;
        private readonly OtpStore _otp;
        private readonly ViewSettings _settings;
        private readonly BadNameFilter _badNames;

        public string SessionHash = "";

        public ViewSession(string ipAddress, ILobbyChannel self, IDatabase db, LoginSessions sessions,
                           OtpStore otp, ViewSettings settings, BadNameFilter badNames = null)
        {
            _ip = ipAddress;
            _self = self;
            _db = db;
            _sessions = sessions;
            _otp = otp;
            _settings = settings;
            _badNames = badNames ?? BadNameFilter.Load(settings.BadNamesPath);
        }

        // key counter helpers (view_session.cpp anonymous namespace)
        private static bool StepsKeyOnRequest(byte c) => c is 0x07 or 0x14 or 0x1F or 0x21 or 0x22 or 0x24 or 0x28 or 0x2B;
        private static bool StepsKeyOnReply(byte c) => c is 0x03 or 0x0B or 0x20 or 0x23;
        private static bool StepsKeyOnError(byte c) => c is 0x22 or 0x24 or 0x28 or 0x2B;

        // view_session::do_write: follow the client's lobby key counter on every reply it gets.
        public void Write(byte[] data)
        {
            if (data.Length > 8 && _sessions.TryGetSession(_ip, SessionHash, out var s))
            {
                byte command = data[8];
                lock (s)
                {
                    if (command == 0x05)
                        s.KeyCounter = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0x1C)) + 1;
                    else if (StepsKeyOnReply(command) || (command == 0x04 && StepsKeyOnError(s.KeyRequest)))
                        ++s.KeyCounter;
                }
            }

            _self.Write(data);
        }

        public void Close() => _self.Close();

        public void HandleError() => _sessions.OnViewSessionClosed(_ip, SessionHash);

        private static string CStr(byte[] b, int off, int max)
        {
            int n = 0;
            while (n < max && b[off + n] != 0) n++;
            return Encoding.Latin1.GetString(b, off, n);
        }

        // The 0x20-byte "result 0x03" acknowledgement shared by delete/create/check/rename.
        private static byte[] SuccessPacket()
        {
            var p = new byte[0x20];
            p[0] = 0x20;
            p[4] = 0x49; p[5] = 0x58; p[6] = 0x46; p[7] = 0x46;
            p[8] = 0x03;
            MD5.HashData(p).CopyTo(p, 12);
            return p;
        }

        private ResultSet Stmt(string q, params object[] args)
        {
            try { return LoginDb.PreparedStmt(_db, q, args); }
            catch (Exception) { return null; }
        }

        // validateCharacterName: null when valid, else the reason.
        public string ValidateCharacterName(string name)
        {
            switch (LoginHelpers.ValidateNamePure(name))
            {
                case NamePureResult.InvalidCharacters: return "Invalid characters present in name.";
                case NamePureResult.InvalidLength: return "Invalid name length.";
            }

            var rset0 = Stmt("SELECT charname FROM chars WHERE charname LIKE ?", name);
            if (rset0 == null) return "Internal entity name query failed.";
            if (rset0.RowsCount() != 0) return "Name already in use.";

            if (_settings.DisableMobNpcCharNames && _settings.IsEntityName(LoginHelpers.ComparableName(name)))
                return "Name already in use.";

            var upper = name.ToUpperInvariant();
            foreach (var word in _settings.BannedWords)
            {
                var bad = word.ToUpperInvariant();
                if (upper.Contains(bad, StringComparison.Ordinal)) return $"Name matched with bad words list <{bad}>.";
            }

            if (_badNames.IsVulgar(name)) return "Name matched the character name filter.";
            return null;
        }

        // characterCreationError: null when the account may create `name`, else the lobby error code.
        public ushort? CharacterCreationError(uint accountId, string name)
        {
            if (_settings.MaintMode > 0 || !_settings.CharacterCreation)
                return (ushort)ErrorCode.FailedToRegisterWithTheNameServer;

            if (ValidateCharacterName(name) != null) return (ushort)ErrorCode.CharacterNameUnavailable;

            var rset = Stmt("SELECT content_ids, (SELECT COUNT(*) FROM chars WHERE accid = accounts.id) AS chars FROM accounts WHERE id = ?", accountId);
            if (rset == null || rset.RowsCount() == 0 || !rset.Next() || rset.Get<uint>("chars") >= rset.Get<uint>("content_ids"))
                return (ushort)ErrorCode.FailedToRegisterWithTheNameServer;

            return null;
        }

        private bool SaveCharacter(uint accid, uint charid, string name, ushort zone, byte nation, byte face, byte race, byte size, byte mjob)
        {
            if (Stmt("INSERT INTO chars(charid,accid,charname,pos_zone,nation) VALUES(?, ?, ?, ?, ?)", charid, accid, name, zone, nation) == null) return false;
            if (Stmt("INSERT INTO char_look(charid,face,race,size) VALUES(?, ?, ?, ?)", charid, face, race, size) == null) return false;
            if (Stmt("INSERT INTO char_stats(charid,mjob) VALUES(?, ?)", charid, mjob) == null) return false;
            if (Stmt("INSERT INTO char_exp(charid) VALUES(?) ON DUPLICATE KEY UPDATE charid = charid", charid) == null) return false;
            if (Stmt("INSERT INTO char_flags(charid) VALUES(?) ON DUPLICATE KEY UPDATE disconnecting = disconnecting", charid) == null) return false;
            if (Stmt("INSERT INTO char_jobs(charid) VALUES(?) ON DUPLICATE KEY UPDATE charid = charid", charid) == null) return false;
            if (Stmt("INSERT INTO char_points(charid) VALUES(?) ON DUPLICATE KEY UPDATE charid = charid", charid) == null) return false;
            if (Stmt("INSERT INTO char_unlocks(charid) VALUES(?) ON DUPLICATE KEY UPDATE charid = charid", charid) == null) return false;
            if (Stmt("INSERT INTO char_profile(charid) VALUES(?) ON DUPLICATE KEY UPDATE charid = charid", charid) == null) return false;
            if (Stmt("INSERT INTO char_storage(charid) VALUES(?) ON DUPLICATE KEY UPDATE charid = charid", charid) == null) return false;
            if (Stmt("DELETE FROM char_inventory WHERE charid = ?", charid) == null) return false;
            if (Stmt("INSERT INTO char_inventory(charid) VALUES(?)", charid) == null) return false;
            if (_settings.NewCharacterCutscene &&
                Stmt("INSERT INTO char_vars(charid, varname, value) VALUES(?, ?, ?)", charid, "HQuest[newCharacterCS]notSeen", 1) == null) return false;
            return true;
        }

        private static readonly ushort[] SandoriaStart = { 230, 231, 232 }; // Southern, Northern, Port San d'Oria
        private static readonly ushort[] BastokStart = { 234, 235, 236 };   // Bastok Mines, Markets, Port Bastok
        private static readonly ushort[] WindurstStart = { 238, 240, 241 }; // Windurst Waters, Port Windurst, Windurst Woods

        // createCharacter: false == the C++ -1. Fills the 140-byte lpkt_chr_info_sub2 `charInfo`.
        public bool CreateCharacter(LoginSession session, byte[] buf, byte[] charInfo)
        {
            var name = session.RequestedNewCharacterName;
            if (name.Length > 15) name = name.Substring(0, 15);

            byte race = buf[48], size = buf[57], face = buf[60];
            if (race < 1 || race > 8) return false;
            if (size > 2) return false;
            if (face > 15) return false;

            byte mjob = Math.Clamp(buf[50], (byte)1, (byte)6);
            byte nation = buf[54];
            if (nation > 2) return false;

            ushort zone = nation switch
            {
                2 => WindurstStart[_settings.Random(3)],
                1 => BastokStart[_settings.Random(3)],
                _ => SandoriaStart[_settings.Random(3)],
            };

            var rset = Stmt("SELECT COALESCE(MAX(charid), 0) AS max_id FROM chars");
            if (rset == null) return false;
            uint charId = 0;
            if (rset.RowsCount() != 0 && rset.Next()) charId = rset.Get<uint>("max_id") + 1;

            if (!SaveCharacter(session.AccountId, charId, name, zone, nation, face, race, size, mjob)) return false;

            var nameBytes = Encoding.Latin1.GetBytes(name);
            Array.Copy(nameBytes, 0, charInfo, ChrInfoSub2Layout.OffCharacterName, Math.Min(nameBytes.Length, 16));
            BinaryPrimitives.WriteUInt32LittleEndian(charInfo.AsSpan(ChrInfoSub2Layout.OffFfxiId), charId);
            BinaryPrimitives.WriteUInt16LittleEndian(charInfo.AsSpan(ChrInfoSub2Layout.OffFfxiIdWorld), (ushort)(charId & 0xFFFF));
            BinaryPrimitives.WriteUInt16LittleEndian(charInfo.AsSpan(ChrInfoSub2Layout.OffWorldId), 0);
            BinaryPrimitives.WriteUInt16LittleEndian(charInfo.AsSpan(ChrInfoSub2Layout.OffStatus), 1);
            charInfo[ChrInfoSub2Layout.OffBitfield] = 0; // race_change = renamef = 0
            charInfo[ChrInfoSub2Layout.OffFfxiIdWorldTbl] = (byte)((charId >> 16) & 0xFF);
            return true;
        }

        // read_func: one received buffer (async_read_some delivers into a zeroed 4096-byte buffer).
        public void Handle(ReadOnlySpan<byte> received)
        {
            var buf = new byte[AuthSession.BufferSize];
            received.Slice(0, Math.Min(received.Length, buf.Length)).CopyTo(buf);
            byte code = buf[8];

            var sessionHash = _sessions.GetHashFromPacket(_ip, buf);
            if (sessionHash == "") return; // "Session requested without valid sessionHash"

            var session = _sessions.GetAuthenticatedSession(_ip, sessionHash);
            session.View ??= this;
            SessionHash = sessionHash;

            lock (session)
            {
                session.KeyRequest = code;
                if (StepsKeyOnRequest(code)) ++session.KeyCounter;
            }

            switch (code)
            {
                case 0x07: HandleSelect(session, buf); break;
                case 0x14: HandleDelete(session, buf); break;
                case 0x21: HandleRegister(session, buf); break;
                case 0x22: HandleCheckName(session, buf); break;
                case 0x28: HandleRename(session, buf); break;
                case 0x26: HandleVersion(session, buf); break;
                case 0x1F: HandleAcquirePlayerData(session); break;
                case 0x24: HandleWorldList(session); break;
            }
        }

        private void HandleSelect(LoginSession session, byte[] buf)
        {
            uint charId = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(28));
            string name = CStr(buf, 36, 15);

            uint accountId;
            var rset = Stmt("SELECT accid FROM chars WHERE charid = ? AND charname = ? LIMIT 1", charId, name);
            if (rset != null && rset.RowsCount() != 0 && rset.Next())
            {
                accountId = rset.Get<uint>("accid");
                session.RequestedCharacterId = charId;
            }
            else { _self.Close(); return; } // mismatched character name

            if (accountId != session.AccountId) { _self.Close(); return; }

            session.Data?.Write(new byte[] { 0x02, 0, 0, 0, 0 });
        }

        private void HandleDelete(LoginSession session, byte[] buf)
        {
            if (!_settings.CharacterDeletion)
            {
                Write(LoginHelpers.GenerateErrorMessage((ushort)ErrorCode.CouldNotConnectToLobbyServer));
                return;
            }

            uint charId = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(DeleteChrLayout.OffFfxiId));
            Write(SuccessPacket());

            uint accountId = 0;
            var rset = Stmt("SELECT accid FROM chars WHERE charid = ? LIMIT 1", charId);
            if (rset != null && rset.RowsCount() != 0 && rset.Next()) accountId = rset.Get<uint>("accid");

            if (accountId != session.AccountId) { _self.Close(); return; }

            session.Data?.DeleteCharFromCharInfo(charId);

            // Not a real delete: accid -> 0, original_accid keeps the owner so the character can be recovered.
            Stmt("UPDATE chars SET accid = 0, original_accid = ? WHERE charid = ? AND accid = ?", session.AccountId, charId, session.AccountId);
        }

        private void HandleRegister(LoginSession session, byte[] buf)
        {
            if (CharacterCreationError(session.AccountId, session.RequestedNewCharacterName) != null) { _self.Close(); return; }

            var charInfo = new byte[ChrInfoSub2Layout.SizeOf];
            if (!CreateCharacter(session, buf, charInfo)) { _self.Close(); return; }

            session.Data?.AddCharIntoCharInfo(charInfo);
            session.RequestedNewCharacterName = "";
            Write(SuccessPacket());
        }

        private void HandleCheckName(LoginSession session, byte[] buf)
        {
            string name = CStr(buf, 32, 15);
            var error = CharacterCreationError(session.AccountId, name);
            if (error != null)
            {
                Write(LoginHelpers.GenerateErrorMessage(error.Value));
                return;
            }

            session.RequestedNewCharacterName = name;
            Write(SuccessPacket());
        }

        private void HandleRename(LoginSession session, byte[] buf)
        {
            uint charId = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(28));
            string newName = CStr(buf, 36, 15);

            uint accountId = 0;
            byte renameFlag = 0;
            var rset = Stmt("SELECT accid, COALESCE(char_flags.`rename`, 0) AS `rename` FROM chars LEFT JOIN char_flags USING(charid) WHERE charid = ? LIMIT 1", charId);
            if (rset != null && rset.RowsCount() != 0 && rset.Next())
            {
                accountId = rset.Get<uint>("accid");
                renameFlag = rset.Get<byte>("rename");
            }

            if (accountId == 0 || accountId != session.AccountId) { _self.Close(); return; }
            if (renameFlag == 0) { _self.Close(); return; } // not flagged for rename

            if (ValidateCharacterName(newName) != null)
            {
                Write(LoginHelpers.GenerateErrorMessage((ushort)ErrorCode.CharacterNameUnavailable));
                return;
            }

            if (Stmt("UPDATE chars SET charname = ? WHERE charid = ? AND accid = ?", newName, charId, session.AccountId) == null)
            {
                Write(LoginHelpers.GenerateErrorMessage((ushort)ErrorCode.FailedToRegisterWithTheNameServer));
                return;
            }

            Stmt("UPDATE char_flags SET `rename` = 0 WHERE charid = ?", charId);

            // The client logs straight in with the character: treat it as a regular select.
            session.RequestedCharacterId = charId;
            session.Data?.RenameCharInCharInfo(charId, newName);

            Write(SuccessPacket());
        }

        private void HandleVersion(LoginSession session, byte[] buf)
        {
            string clientVer = CStr(buf, 0x74, 6) + "xx_x";
            string expected = (_settings.ClientVer.Length > 6 ? _settings.ClientVer.Substring(0, 6) : _settings.ClientVer) + "xx_x";

            bool fatal = false;
            if (expected != clientVer)
            {
                _settings.Log?.Invoke($"view_session: Account {session.AccountId} has incorrect client version: got {clientVer}, expected {expected}");
                switch (_settings.VerLock)
                {
                    case 1:
                        fatal = true;
                        _settings.Log?.Invoke(string.CompareOrdinal(expected, clientVer) < 0
                            ? "view_session: The server must be updated to support this client version"
                            : "view_session: The client must be updated to support this server version");
                        break;
                    case 2:
                        fatal = string.CompareOrdinal(expected, clientVer) > 0;
                        if (fatal) _settings.Log?.Invoke("view_session: The client must be updated to support this server version");
                        break;
                }
            }

            if (fatal)
            {
                session.View?.Write(LoginHelpers.GenerateErrorMessage((ushort)ErrorCode.GamesDataHasBeenUpdated));
                return;
            }

            bool needsOtp = _otp.DoesAccountNeedOtp(session.AccountId, "TOTP");

            var p = new byte[0x28];
            BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(0), 0x28);
            BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(4), LoginPackets.Terminator);
            BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(8), 0x05);
            BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(28), 0xAD5DE04F); // old magic key
            BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(32), LoginHelpers.GenerateExpansionBitmask(
                _settings.RiseOfZilart, _settings.ChainsOfPromathia, _settings.TreasuresOfAhtUrghan, _settings.WingsOfTheGoddess,
                _settings.ACrystallineProphecy, _settings.AMoogleKupodEtat, _settings.AShantottoAscension, _settings.VisionsOfAbyssea,
                _settings.ScarsOfAbyssea, _settings.HeroesOfAbyssea, _settings.SeekersOfAdoulin));
            BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(36), LoginHelpers.GenerateFeatureBitmask(
                _settings.MogWardrobe3, _settings.MogWardrobe4, _settings.MogWardrobe5,
                _settings.MogWardrobe6, _settings.MogWardrobe7, _settings.MogWardrobe8, needsOtp));
            LoginPackets.CopyHashIntoPacket(p, MD5.HashData(p));

            session.View?.Write(p);
        }

        private void HandleAcquirePlayerData(LoginSession session)
        {
            if (session.Data != null) session.Data.Write(new byte[] { 0x01, 0, 0, 0, 0 });
            else Write(LoginHelpers.GenerateErrorMessage((ushort)ErrorCode.CouldNotConnectToLobbyServer));
        }

        private void HandleWorldList(LoginSession session)
        {
            var p = new byte[WorldListLayout.SizeOf];
            BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(0), WorldListLayout.SizeOf);
            BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(4), LoginPackets.Terminator);
            BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(8), 0x23);
            BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(WorldListLayout.OffSumOfWorld), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(WorldListLayout.OffWorldName + WorldNameLayout.OffNo), 0x20);
            var name = Encoding.Latin1.GetBytes(_settings.ServerName);
            Array.Copy(name, 0, p, WorldListLayout.OffWorldName + WorldNameLayout.OffName, Math.Min(name.Length, 15));
            LoginPackets.CopyHashIntoPacket(p, MD5.HashData(p));

            session.View?.Write(p);
        }
    }
}
