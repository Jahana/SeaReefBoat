using System;
using XI.Ipc;
using XI.Map;
using XI.Persistence;

namespace XI.Zone
{
    // CAMANContainer::m_* : built on first use, so per zone-in like the CCharEntity holding it
    public sealed class AmanState
    {
        public bool IsMuted;
        public bool AssistChannelEligible;
        public uint AssistChannelPlaytimeExpiry;
        public bool NotifyExpired;
    }

    // CAMANContainer (aman.cpp): Assist Channel membership (new players, returnees and mentors), mentor evaluations
    // (thumbs up and warnings) and mutes, plus GP_CLI_COMMAND_ASSIST_CHANNEL (0x0B7) that issues them. The mastery rank
    // is not implemented in the C++ either: always 1, so nobody reaches the 6 that muting needs.
    public partial class ZoneHost
    {
        private const string WarningCooldownVar = "[ASSIST][Warnings]Cooldown";
        private const string ThumbsUpCooldownVar = "[ASSIST][ThumbsUp]Cooldown";
        private const string AssistInteractionEligibleVar = "[ASSIST][Evaluations]Eligible";
        private const string EvaluationsDailyVar = "[ASSIST][Evaluations]Today";
        private const string EvaluationsCountVar = "[ASSIST][Evaluations]Count";
        private const string AssistChannelEligibleVar = "[ASSIST]Eligible";
        private const string MutedExpiryVar = "[ASSIST]Muted";
        private const int RoeAssistChannel = 1448;
        private const byte MasteryRank = 1;

        // GP_CLI_COMMAND_ASSIST_CHANNEL_KIND
        private const byte AssistGiveThumbsUp = 0x24, AssistIssueWarning = 0x25, AssistAddToMuteList = 0x26, AssistRemoveFromMuteList = 0x27;

        // enums/msg_std.h
        private const ushort MsgStdGivenThumbsUp = 306, MsgStdGivenWarning = 307, MsgStdAnErrorHasOccured = 308,
            MsgStdThumbsUpCooldown = 309, MsgStdWarningCooldown = 310, MsgStdAlreadyMuted = 312, MsgStdReceivedThumbsUp = 314,
            MsgStdReceivedWarning = 315, MsgStdAssistChannelExpiring = 317, MsgStdAssistChannelExpired = 318;

        private void RegisterAssistHandlers()
        {
            Register(0x0B7, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent)
                                              .OneOf("Kind", p.Buffer[4], AssistGiveThumbsUp, AssistIssueWarning, AssistAddToMuteList, AssistRemoveFromMuteList)
                                              .MustEqual(ChatCfg.AssistChannelEnabled ? 1 : 0, 1, "Assist Channel is not enabled"),
                     OnAssistChannel);
        }

        // GP_CLI_COMMAND_ASSIST_CHANNEL: Kind, unknown00, sName[15] @6, Mes. The receiver acts on it (AssistChannelEvent)
        private void OnAssistChannel(MapSession s, CharData c, BasicPacket p)
        {
            uint victimId = 0;
            var r = Exec("SELECT charid FROM chars WHERE charname = ? LIMIT 1", BoundValue.Of(CborRaw.FromBytes(UntrustedBytes(p, 6, 15))));
            if (r != null && r.Next()) victimId = r.Get<uint>("charid");
            if (victimId == 0)
            {
                Push(s, SelfPackets.Message(MsgStdAnErrorHasOccured));
                return;
            }

            var aman = Aman(c);
            byte kind = p.Buffer[4];
            switch (kind)
            {
                case AssistGiveThumbsUp:
                    // anyone can, once a real life day
                    if (aman.IsMuted) { Push(s, SelfPackets.Message(MsgStdAnErrorHasOccured)); return; }
                    if (CharVar(c, ThumbsUpCooldownVar) != 0) { Push(s, SelfPackets.Message(MsgStdThumbsUpCooldown)); return; }
                    break;
                case AssistIssueWarning:
                    if (!c.MentorFlg || aman.IsMuted) { Push(s, SelfPackets.Message(MsgStdAnErrorHasOccured)); return; }
                    if (CharVar(c, WarningCooldownVar) != 0) { Push(s, SelfPackets.Message(MsgStdWarningCooldown)); return; }
                    break;
                default:
                    // a mentor of mastery rank 6 or more, not muted
                    if (!c.MentorFlg || MasteryRank < 6 || aman.IsMuted) { Push(s, SelfPackets.Message(MsgStdAnErrorHasOccured)); return; }
                    break;
            }
            SendIpc(new AssistChannelEvent { SenderId = c.Id, ReceiverId = victimId, Action = kind });
        }

        // CCharEntity::aman() and the CAMANContainer constructor: the eligibility is worked out once, in an Assist zone
        private AmanState Aman(CharData c)
        {
            if (c.Aman != null) return c.Aman;
            var a = c.Aman = new AmanState();
            var r = Exec("SELECT mentor, DATEDIFF(CURRENT_TIMESTAMP, last_logout) AS days_since_logout, muted FROM chars " +
                         "LEFT JOIN char_flags ON chars.charid = char_flags.charid WHERE chars.charid = ?", BoundValue.Of(c.Id));
            if (r == null || !r.Next()) return a;
            uint logoutDiff = (uint)r.Get<long>("days_since_logout");
            a.IsMuted = r.Get<bool>("muted");

            var cfg = ChatCfg;
            if (!cfg.AssistChannelEnabled || !AreaOf(c).CanUseMisc("assist")) return a;
            // 1. the "Assist Channel" record of eminence
            if (!GetEminenceRecordCompletion(c, RoeAssistChannel)) return a;

            uint playtime = PlayTime(c);
            uint length = (uint)(ushort)cfg.AssistChannelMembershipLength * 3600;
            if (c.MentorFlg) a.AssistChannelEligible = true; // mentors always are
            else if ((a.AssistChannelPlaytimeExpiry = (uint)CharVar(c, AssistChannelEligibleVar)) > 0)
            {
                // flagged before
                if (a.AssistChannelPlaytimeExpiry > playtime) a.AssistChannelEligible = true;
                else
                {
                    SetCharVar(c, AssistChannelEligibleVar, 0, 0);
                    a.NotifyExpired = true;
                }
            }
            else if (logoutDiff >= (ushort)cfg.AssistChannelReturneeLoginGap)
            {
                // a returnee: membership-length playtime hours from now
                a.AssistChannelPlaytimeExpiry = playtime + length;
                SetCharVar(c, AssistChannelEligibleVar, (int)a.AssistChannelPlaytimeExpiry, 0);
                a.AssistChannelEligible = true;
            }
            else if (playtime <= length)
            {
                // a new char: until that much playtime
                a.AssistChannelPlaytimeExpiry = length;
                SetCharVar(c, AssistChannelEligibleVar, (int)a.AssistChannelPlaytimeExpiry, 0);
                a.AssistChannelEligible = true;
            }

            if (a.AssistChannelEligible && a.IsMuted && CharVar(c, MutedExpiryVar) == 0)
            {
                // the mute expired
                Exec("UPDATE char_flags SET muted = false WHERE charid = ?", BoundValue.Of(c.Id));
                a.IsMuted = false;
            }
            return a;
        }

        // CCharEntity::GetPlayTime: the saved playtime plus this session's
        // CCharEntity::GetPlayTime in seconds: the loaded play time plus the steady clock's time since the load
        private uint PlayTime(CharData c) => (uint)(c.PlayTimeSec + (NowMs() - c.LoginMs) / 1000);

        private bool IsAssistChannelEligible(CharData c) =>
            ChatCfg.AssistChannelEnabled && AreaOf(c).CanUseMisc("assist") && Aman(c).AssistChannelEligible;

        // Bronze (1) for a mentor, Silver after 50 thumbs up, Gold after 100
        private byte MentorRank(CharData c) => (byte)Math.Clamp(CharVar(c, EvaluationsCountVar) / 50 + 1, 1, 3);

        // CAMANContainer::onZoneIn (zoneutils::AfterZoneIn)
        private void AmanOnZoneIn(MapSession s, CharData c)
        {
            var a = Aman(c);
            if (a.NotifyExpired)
            {
                Push(s, SelfPackets.Message(MsgStdAssistChannelExpired));
                return;
            }
            // only in Assist zones, eligible and with an expiry
            if (!IsAssistChannelEligible(c) || a.AssistChannelPlaytimeExpiry == 0) return;

            int textId = scripts_?.GetTextIdVariable(c.Zone, "ASSIST_CHANNEL") ?? 0;
            if (textId == 0)
            {
                log_($"Zone {c.Zone} does not have an ASSIST_CHANNEL text ID set");
                return;
            }
            // calculateAssistExpiry: seconds since Jan 01 2002 00:00 JST, now and when the playtime runs out
            const long origin = 1009810800;
            uint current = (uint)(UnixNow() - origin);
            uint expiry = current + (a.AssistChannelPlaytimeExpiry - PlayTime(c));
            Push(s, EventPackets.TalkNumWork(c.Id, c.TargId, c.Name, true, (ushort)textId, expiry, current, 1, 0, false));
            if (expiry - current < 24 * 3600) Push(s, ParamMessage((expiry - current) / 3600, MsgStdAssistChannelExpiring));
        }

        // CAMANContainer::recordLastMessage: thumbs up, warnings and mutes only within 10 minutes of a message
        private void RecordLastAssistMessage(CharData c) => SetCharVar(c, AssistInteractionEligibleVar, 1, (uint)(UnixNow() + 600));

        // IPCClient::handleMessage_AssistChannelEvent: this map has the receiver
        private void OnAssistChannelEvent(AssistChannelEvent m)
        {
            var (c, s) = CharById(m.ReceiverId);
            if (c == null) return;
            var a = Aman(c);
            bool interactionEligible = CharVar(c, AssistInteractionEligibleVar) == 1;
            void Reply(ushort message, string string2 = "") =>
                SendIpc(new MessageStandard { RecipientId = m.SenderId, Message = message, String2 = string2 });
            switch (m.Action)
            {
                case AssistAddToMuteList:
                    if (!interactionEligible) Reply(MsgStdAnErrorHasOccured);
                    else if (a.IsMuted) Reply(MsgStdAlreadyMuted);
                    else
                    {
                        // 24 hours; AddedToMuteList to everyone in assist is a TODO in the C++
                        a.IsMuted = true;
                        Exec("UPDATE char_flags SET muted = true WHERE charid = ?", BoundValue.Of(c.Id));
                        SetCharVar(c, MutedExpiryVar, 1, (uint)(UnixNow() + 24 * 60 * 60));
                        log_($"{c.Name} was muted by player ID {m.SenderId}");
                    }
                    break;
                case AssistRemoveFromMuteList:
                    if (!a.IsMuted) Reply(MsgStdAnErrorHasOccured);
                    else
                    {
                        a.IsMuted = false;
                        Exec("UPDATE char_flags SET muted = false WHERE charid = ?", BoundValue.Of(c.Id));
                        SetCharVar(c, MutedExpiryVar, 0, 0);
                        log_($"{c.Name} was unmuted by player ID {m.SenderId}");
                    }
                    break;
                case AssistGiveThumbsUp:
                case AssistIssueWarning:
                {
                    // only mentors get evaluated, within 10 minutes of their message
                    if (!interactionEligible || !c.MentorUnlocked)
                    {
                        Reply(MsgStdAnErrorHasOccured);
                        break;
                    }
                    bool up = m.Action == AssistGiveThumbsUp;
                    RecordEvaluation(s, c, up ? 1 : -1);
                    // the giver's cooldown runs to the next JST midnight
                    SetCharVarById(m.SenderId, up ? ThumbsUpCooldownVar : WarningCooldownVar, 1, (uint)JstNextMidnight(UnixNow()));
                    Reply(up ? MsgStdGivenThumbsUp : MsgStdGivenWarning, c.Name);
                    break;
                }
            }
        }

        // CAMANContainer::recordEvaluation: at most 3 a day count
        private void RecordEvaluation(MapSession s, CharData c, int val)
        {
            int today = CharVar(c, EvaluationsDailyVar);
            if (today >= 3) return;
            SetCharVar(c, EvaluationsDailyVar, today + 1, (uint)JstNextMidnight(UnixNow()));
            IncrementCharVar(c, EvaluationsCountVar, val);
            Push(s, SelfPackets.Message(val < 0 ? MsgStdReceivedWarning : MsgStdReceivedThumbsUp));
        }

        // charutils::IncrementCharVar: in the db, and the cached value dropped; a Quest[..] / Mission[..]
        // var's row copy takes the value the increment left, read back in the same transaction
        private void IncrementCharVar(CharData c, string name, int value)
        {
            Func<bool> legacy = () => Exec("INSERT INTO char_vars SET charid = ?, varname = ?, value = ? ON DUPLICATE KEY UPDATE value = value + ?",
                                           BoundValue.Of(c.Id), BoundValue.Of(name), BoundValue.Of(value), BoundValue.Of(value)) != null;
            if (XI.Quests.CharProgress.IsProgressVar(name))
                PersistProgress(c, legacy, p =>
                {
                    c.CharVars.Remove(name);
                    int now = CharVar(c, name);
                    return p.SetVar(name, now, c.CharVars[name].Expiry);
                });
            else
                legacy();
            c.CharVars.Remove(name);
        }

        // charutils::SetCharVar(charId, ...): a char on this map sets its own; otherwise the db, and the world tells
        // whichever map has it
        private void SetCharVarById(uint charId, string name, int value, uint expiry)
        {
            var (c, _) = CharById(charId);
            if (c != null)
            {
                SetCharVar(c, name, value, expiry);
                return;
            }
            PersistCharVar(charId, name, value, expiry);
            SendIpc(new CharVarUpdate(charId, value, expiry, name));
        }

        // CAMANContainer::setMentorUnlocked (player:setMentor)
        private void SetMentorUnlocked(CharData c, bool value)
        {
            c.MentorUnlocked = value;
            Exec("UPDATE chars SET mentor = ? WHERE charid = ?", BoundValue.Of(value), BoundValue.Of(c.Id));
        }
    }
}
