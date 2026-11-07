// src/profile/irc/presence_manager.cpp: tracks online accounts and sends their friends status notices.
//
// The C++ runs every session on one main thread; here every caller holds Sync (the engine's request handlers, the IRC
// sessions and the credential refresh), which serializes the presence state and its DB queries the same way.

using System;
using System.Collections.Generic;
using System.Linq;

namespace XI.Profile
{
    public sealed class PresenceManager
    {
        public struct Status
        {
            public OpenStatus OpenStatus = OpenStatus.Online;
            public bool InGame = false;
            public byte CharacterSlot = 0; // when in game
            public bool ReceiveMessages = true;

            public Status() { }
        }

        // the IRC side of a signed-on account (IrcSession)
        public interface ISession
        {
            void Send(string message); // drops the client if too much is queued
            void Close();
        }

        private sealed class Online
        {
            public ISession Session;
            public byte[] SessionHash;
            public ulong SignOnId;
            public Status Status = new();
        }

        public readonly object Sync = new();

        private readonly ProfileAccounts _accounts;
        private readonly ProfileFriends _friends;
        private readonly Action<string> _log;
        private readonly Action<string> _debug;
        private readonly Dictionary<uint, Online> _online = new();
        private ulong _lastSignOnId;
        private ulong _lastTimestamp;

        // the notice clock (ms since the epoch); tests may pin it
        public Func<ulong> NowMs = () => (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public PresenceManager(ProfileAccounts accounts, ProfileFriends friends, Action<string> log, Action<string> debug = null)
        {
            _accounts = accounts;
            _friends = friends;
            _log = log ?? (_ => { });
            _debug = debug ?? (_ => { });
        }

        // listStatus: invisible shows as offline
        private static byte ListStatus(Status status) => status.OpenStatus switch
        {
            OpenStatus.Online => 1,
            OpenStatus.Away => 2,
            _ => 0,
        };

        // signOn: kicks the previous session, if any
        public ulong SignOn(uint accountId, byte[] sessionHash, ISession session)
        {
            bool signedOn = !_online.TryGetValue(accountId, out var online);
            if (signedOn)
            {
                online = new Online();
                _online[accountId] = online;
                // restore the saved status so invisible stays invisible
                online.Status.OpenStatus = _accounts.GetOpenStatus(accountId) ?? OpenStatus.Invisible;
            }
            online.Session?.Close();
            online.Session = session;
            online.SessionHash = sessionHash;
            online.SignOnId = ++_lastSignOnId;
            // the new client reports its character again
            online.Status = new Status { OpenStatus = online.Status.OpenStatus, ReceiveMessages = online.Status.ReceiveMessages };
            Announce(accountId);
            return online.SignOnId;
        }

        // signOff: no-op if a newer session took over
        public void SignOff(uint accountId, ulong signOnId)
        {
            if (_online.TryGetValue(accountId, out var online) && online.SignOnId == signOnId)
            {
                _online.Remove(accountId);
                Announce(accountId);
            }
        }

        public bool IsOnline(uint accountId) => _online.ContainsKey(accountId);

        public Status? GetStatus(uint accountId) => _online.TryGetValue(accountId, out var online) ? online.Status : null;

        // refreshCredentials: bumps the credential timestamp of everyone online
        public void RefreshCredentials()
        {
            foreach (var (accountId, online) in _online) _accounts.Refresh(accountId, online.SessionHash);
        }

        // setStatus: false if saving the open status failed
        public bool SetStatus(uint accountId, Status status)
        {
            if (!_online.TryGetValue(accountId, out var online)) return false;
            if (status.OpenStatus != online.Status.OpenStatus)
            {
                if (!_accounts.SetOpenStatus(accountId, status.OpenStatus)) return false;
                _log($"account {accountId} changed status to {status.OpenStatus}");
            }
            // friends only see open status and in-game
            bool changed = status.OpenStatus != online.Status.OpenStatus || status.InGame != online.Status.InGame;
            online.Status = status;
            if (changed) Announce(accountId);
            return true;
        }

        public void SendFriendsTo(uint accountId)
        {
            var friendList = _friends.VisibleFriends(accountId);
            if (friendList == null) return;
            foreach (var visible in friendList) Notify(visible.AccountId, accountId, visible.ListIndex, true, visible.Playing);
        }

        // introduce: new friends see each other
        public void Introduce(uint accountId, uint otherAccountId)
        {
            var visible = _friends.VisibleFriends(accountId);
            if (visible == null) return;
            var other = visible.FirstOrDefault(f => f.AccountId == otherAccountId);
            if (other != null)
            {
                Notify(otherAccountId, accountId, other.ListIndex, true, other.Playing);
                Notify(accountId, otherAccountId, other.TheirIndex, true, Playing(accountId));
            }
        }

        // hide: accountId appears offline to otherAccountId
        public void Hide(uint accountId, uint otherAccountId)
        {
            var visible = _friends.VisibleFriends(accountId);
            if (visible == null || visible.Any(f => f.AccountId == otherAccountId)) return;
            var listings = _friends.Load(otherAccountId);
            if (listings == null) return;
            foreach (var listing in listings)
            {
                if (listing.IsBlack == 0 && listing.PolId == accountId) Notify(accountId, otherAccountId, listing.Num, false, null);
            }
        }

        public void DeliverMessage(uint senderAccountId, uint recipientAccountId, string name)
        {
            if (!_online.ContainsKey(recipientAccountId))
            {
                _debug($"account {senderAccountId} left a message for offline account {recipientAccountId}");
                return;
            }
            _debug($"account {recipientAccountId} notified of a message from account {senderAccountId}");
            Send(senderAccountId, recipientAccountId, ProfileNotices.MessageNotice(name));
        }

        private void Announce(uint accountId)
        {
            var friendList = _friends.VisibleFriends(accountId);
            if (friendList == null) return;
            var character = Playing(accountId);
            foreach (var visible in friendList) Notify(accountId, visible.AccountId, visible.TheirIndex, true, character);
        }

        private uint? Playing(uint accountId)
        {
            var online = GetStatus(accountId);
            if (online != null && online.Value.InGame) return _accounts.Playing(accountId);
            return null;
        }

        private void Notify(uint subjectAccountId, uint recipientAccountId, byte listIndex, bool visible, uint? character)
        {
            if (!_online.ContainsKey(recipientAccountId)) return;
            var notice = new FriendStatusNotice
            {
                FriendAccountId = subjectAccountId,
                ListIndex = listIndex,
                Timestamp = NextTimestamp(),
            };
            var subject = GetStatus(subjectAccountId);
            if (subject != null && visible)
            {
                notice.Status = ListStatus(subject.Value);
                if (notice.Status != 0 && subject.Value.InGame) notice.ActiveCharacterId = character;
            }
            Send(subjectAccountId, recipientAccountId, ProfileNotices.FriendStatus(notice));
        }

        private void Send(uint senderAccountId, uint recipientAccountId, string text)
        {
            if (!_online.TryGetValue(recipientAccountId, out var recipient)) return;
            recipient.Session?.Send($":{ProfileCodec.ScrambleNick(senderAccountId)}!p@pol NOTICE {ProfileCodec.ScrambleNick(recipientAccountId)} :{text}");
        }

        // nextTimestamp: strictly increasing, the client drops stale notices
        private ulong NextTimestamp()
        {
            ulong now = NowMs();
            _lastTimestamp = Math.Max(now, _lastTimestamp + 1);
            return _lastTimestamp;
        }
    }
}
