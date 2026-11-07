using System;
using System.Collections.Generic;
using XI.Ipc;
using XI.Map;
using XI.Persistence;

namespace XI.Zone
{
    // CCharEntity::InvitePending: the inviter of the invite still waiting for an answer
    public sealed class PendingInvite
    {
        public uint UniqueNo;
        public ushort ActIndex;
        public byte Kind;

        public void Clean()
        {
            UniqueNo = 0;
            ActIndex = 0;
            Kind = 0;
        }
    }

    // Parties and alliances (party.cpp, alliance.cpp, charutils::ReloadParty / LeaveParty / RemoveSeekFlag, the
    // c2s/0x06E - 0x078 and 0x11C handlers and the IPCClient party messages). accounts_parties holds the truth across
    // processes; a Party / Alliance here is this process's view of it, rebuilt by ReloadParty on the next tick.
    // Level sync (CParty::SetSyncTarget / RefreshSync / DisableSync and its zone, invite and EXP checks) is here too.
    // The 0x071 and 0x077 linkshell kinds (1 and 2) check the char's rank on the equipped shell (hasLinkshellRank) and
    // send LinkshellRemove / LinkshellRankChange, which the world routes to the member's map (ZoneHost.Linkshell.cs).
    // The leader's trusts (ZoneHost.Trusts.cs) are in the party lists and the latents' counts.
    public sealed partial class ZoneHost
    {
        // party flags (PARTYFLAG)
        private const ushort PartySecond = 0x01, PartyThird = 0x02, PartyLeaderFlag = 0x04, AllianceLeaderFlag = 0x08,
                             PartyQm = 0x10, PartySync = 0x100, PartyMoghouse = 0x200;

        private const byte KindParty_ = 0, KindAlliance_ = 5;

        private const ushort MsgStdInvitationDeclined = 11, MsgStdCannotBeProcessed = 14, MsgStdNotPartyLeader = 21,
                             MsgStdCannotInvite = 23, MsgStdTargetIsCurrentlyBlocking = 225, MsgStdBlockedByBlockaid = 226,
                             MsgStdTrustCannotJoinAlliance = 266;
        // MsgStd / MsgBasic: level sync
        private const ushort MsgStdLevelSyncWarning = 235, MsgStdCannotInviteLevelSync = 236, MsgStdCannotJoinLevelSync = 237,
                             MsgStdLevelSyncSet = 238, MsgStdLevelSyncDesigneeBelowMin = 541, MsgStdLevelSyncDesigneeInOtherArea = 542,
                             MsgStdLevelSyncPreventedByStatus = 543, MsgStdLevelSyncDeactivateLeftArea = 551,
                             MsgStdLevelSyncRemoveTooFewMembers = 552, MsgStdLevelSyncRemoveLeftParty = 553,
                             MsgStdLevelSyncRemoveLowLevel = 554, MsgStdLevelSyncRemoveIneligibleExp = 556,
                             MsgBasicLevelSyncActivated = 540, MsgBasicLevelSyncNoExp = 545;
        private const ushort EffectConfrontation = 276;

        // GP_CLI_COMMAND_GROUP_CHANGE2_CHANGEKIND
        private const byte ChangeSetPartyLeader = 0, ChangeSetAllianceLeader = 1, ChangePearlToSack = 2, ChangeSackToPearl = 3,
                           ChangeSetQuartermaster = 4, ChangeSetLottery = 5, ChangeSetLevelSync = 6, ChangeDisableLevelSync = 7;

        // CParty::partyInfo_t
        internal readonly record struct PartyMemberInfo(uint Id, uint PartyId, uint AllianceId, string Name, ushort Flags, ushort Zone, ushort PrevZone);

        // CParty (PARTY_PCS only)
        internal sealed class Party
        {
            private readonly ZoneHost h_;
            public uint Id;
            public byte Number;
            public CharData Leader, QuarterMaster, SyncTarget;
            public Alliance Alliance;
            public readonly List<CharData> Members = new List<CharData>();
            // m_EffectsChanged: a member's icons changed, so PushEffectsPacket sends GROUP_EFFECTS on its PostTick
            public bool EffectsChanged;
            // m_FormedByTrusts: made for a solo char's first trust, so it disbands when the trusts are gone
            public bool FormedByTrusts;

            // CParty::HasTrusts
            public bool HasTrusts()
            {
                foreach (var m in Members)
                    if (m.PTrusts.Count > 0) return true;
                return false;
            }

            private int LeaderTrustCount() => Leader?.PTrusts.Count ?? 0;

            // CParty(PEntity): the char's own party, it the leader
            public Party(ZoneHost h, CharData c)
            {
                h_ = h;
                Id = c.Id;
                AddMember(c);
                SetLeader(c.Name);
            }

            // CParty(id): a party formed elsewhere, its members pushed in by ReloadParty
            public Party(ZoneHost h, uint id)
            {
                h_ = h;
                Id = id;
            }

            private void SendReload()
            {
                if (Alliance != null) h_.SendIpc(new AllianceReload(Alliance.Id));
                else h_.SendIpc(new PartyReload(Id));
            }

            public void DisbandParty(bool playerInitiated = true)
            {
                Alliance?.RemoveParty(this);

                SyncTarget = null;
                Leader = null;
                Alliance = null;

                SetQuarterMaster("");
                PushPacket(0, 0, h_.GroupTbl(null));

                foreach (var m in Members)
                {
                    h_.ClearTrusts(m);
                    m.Party = null;
                    m.Latents.CheckLatentsPartyJobs();
                    m.Latents.CheckLatentsPartyMembers(Members.Count, 0);
                    m.Latents.CheckLatentsPartyAvatar();
                    h_.PushTo(m, GroupList(m, 0, 0, m.Zone));
                    h_.ResetTreasurePool(m);
                    h_.Exec("DELETE FROM accounts_parties WHERE charid = ?", BoundValue.Of(m.Id));
                }

                if (playerInitiated) h_.SendIpc(new PartyDisband(Id));
                Members.Clear(); // delete this
            }

            public void AssignPartyRole(string name, byte role)
            {
                var rs = h_.Exec("SELECT chars.charid FROM chars JOIN accounts_parties ON accounts_parties.charid = chars.charid WHERE charname = ? AND partyid = ?",
                                 BoundValue.Of(name), BoundValue.Of(Id));
                if (rs == null || rs.RowsCount() == 0) return;

                switch (role)
                {
                    case ChangeSetPartyLeader: SetLeader(name); break;
                    case ChangeSetQuartermaster: SetQuarterMaster(name); break;
                    case ChangeSetLottery: SetQuarterMaster(""); break;
                    case ChangeSetLevelSync: SetSyncTarget(name, MsgStdLevelSyncSet); break;
                    case ChangeDisableLevelSync: SetSyncTarget("", MsgStdLevelSyncRemoveLeftParty); break;
                    default: return;
                }
                SendReload();
            }

            // CParty::SetSyncTarget: a member's name syncs the members in its zone to its level (their dispelable and
            // zone-lost effects and TP gone, LevelSync with the level as power, whose script restricts them); "" (or no
            // such member) drops it, each synced member's LevelSync then running out in 30 s
            public void SetSyncTarget(string memberName, ushort message)
            {
                if (!(h_.loader_.Stats?.Settings.LevelSyncEnable ?? true)) return;
                var target = GetMemberByName(memberName);
                if (target != null && Leader != null)
                {
                    if (target.MLvl < 10)
                    {
                        h_.PushTo(Leader, SelfPackets.BattleMessage(Leader, 0, 10, MsgStdLevelSyncDesigneeBelowMin));
                        return;
                    }
                    if (target.Zone != Leader.Zone)
                    {
                        h_.PushTo(Leader, SelfPackets.BattleMessage(Leader, 0, 0, MsgStdLevelSyncDesigneeInOtherArea));
                        return;
                    }
                    foreach (var m in Members)
                    {
                        if (m.StatusEffects.HasStatusEffect(EffectLevelRestriction, EffectId.LevelSync, EffectSjRestriction, EffectConfrontation, EffectBattlefield))
                        {
                            h_.PushTo(Leader, SelfPackets.BattleMessage(Leader, 0, 0, MsgStdLevelSyncPreventedByStatus));
                            return;
                        }
                    }
                    SyncTarget = target;
                    foreach (var m in Members.ToArray())
                    {
                        if (m.Zone != target.Zone) continue;
                        h_.PushTo(m, MessageParams4(target.MLvl, 0, 0, 0, message));
                        SyncMember(m, target.MLvl, EffectFlag.Dispelable | EffectFlag.OnZone, true);
                    }
                    h_.Exec("UPDATE accounts_parties SET partyflag = partyflag & ~? WHERE partyid = ? AND partyflag & ?",
                            BoundValue.Of(PartySync), BoundValue.Of(Id), BoundValue.Of(PartySync));
                    h_.Exec("UPDATE accounts_parties SET partyflag = partyflag | ? WHERE partyid = ? AND charid = ?",
                            BoundValue.Of(PartySync), BoundValue.Of(Id), BoundValue.Of(target.Id));
                }
                else
                {
                    if (SyncTarget != null)
                        foreach (var m in Members.ToArray()) ExpireSync(m, message);
                    SyncTarget = null;
                    h_.Exec("UPDATE accounts_parties SET partyflag = partyflag & ~? WHERE partyid = ? AND partyflag & ?",
                            BoundValue.Of(PartySync), BoundValue.Of(Id), BoundValue.Of(PartySync));
                }
            }

            // a member synced to the level: the flagged effects and its TP gone, LevelSync (permanent) on, the others in
            // range told of the new level when asked
            public void SyncMember(CharData m, byte level, uint delFlags, bool pushSync)
            {
                m.StatusEffects.DelStatusEffectsByFlag(delFlags);
                m.Tp = 0;
                m.UpdateMask |= CharStats.UpdateHp;
                m.StatusEffects.AddStatusEffect(new StatusEffect(EffectId.LevelSync, EffectId.LevelSync, level, 0, 0), EffectNotice.Silent);
                if (pushSync) h_.PushCharInRange(m, h_.CharSyncOf(m), false);
            }

            // a permanent LevelSync runs out in 30 s, the member told why
            private void ExpireSync(CharData m, ushort message)
            {
                if (m.StatusEffects.GetStatusEffect(EffectId.LevelSync) is not { DurationMs: 0 } sync) return;
                h_.PushTo(m, SelfPackets.BattleMessage(m, 0, 30, message));
                sync.SetStartTime(h_.NowMs());
                sync.DurationMs = 30_000;
            }

            // CParty::RemoveMember / DelMember: the designee leaving drops the sync; any member leaving a synced party
            // has its LevelSync run out
            private void LeaveSync(CharData c)
            {
                if (SyncTarget == c)
                {
                    SetSyncTarget("", MsgStdLevelSyncRemoveLeftParty);
                    ExpireSync(c, MsgStdLevelSyncRemoveLeftParty);
                    DisableSync();
                }
                if (SyncTarget != null && SyncTarget != c) ExpireSync(c, MsgStdLevelSyncRemoveLeftParty);
            }

            // CParty::DisableSync
            public void DisableSync()
            {
                SyncTarget = null;
                ReloadParty();
            }

            // CParty::RefreshSync: the designee's level changed. Under 10 the sync is dropped (the C++ goes on and puts
            // the designee back at the end, as here); each member in its zone is capped at the new level
            public void RefreshSync()
            {
                var sync = SyncTarget;
                if (sync == null) return;
                byte syncLevel = sync.JobLevels[sync.MJob];
                if (syncLevel < 10) SetSyncTarget("", MsgStdLevelSyncRemoveLowLevel);
                foreach (var m in Members.ToArray())
                {
                    if (m.Zone != sync.Zone) continue;
                    byte newLevel = Math.Min(syncLevel, m.JobLevels[m.MJob]);
                    if (m.StatusEffects.GetStatusEffect(EffectId.LevelSync) is { } effect) effect.Power = syncLevel;
                    // members below the sync keep their level but track the new cap for their own next level up
                    m.LevelRestriction = syncLevel;
                    if (m.MLvl != newLevel) h_.ApplySyncLevel(m, newLevel);
                    h_.PushTo(m, SelfPackets.BattleMessage(m, 0, syncLevel, MsgBasicLevelSyncActivated));
                }
                SyncTarget = sync;
            }

            public CharData GetMemberByName(string name)
            {
                if (name == "") return null;
                foreach (var m in Members)
                    if (string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)) return m;
                return null;
            }

            public void RemoveMember(CharData c)
            {
                if (c == null || c.Party != this) return;

                if (Leader == c)
                {
                    RemovePartyLeader(c);
                    // Remove their trusts
                    h_.ClearTrusts(c);
                }
                else if (Members.Contains(c))
                {
                    if (QuarterMaster == c) SetQuarterMaster("");
                    LeaveSync(c);

                    c.Latents.CheckLatentsPartyMembers(Members.Count, LeaderTrustCount());
                    h_.PushTo(c, h_.GroupTbl(null));
                    h_.PushTo(c, GroupList(c, 0, 0, c.Zone));
                    h_.PushTo(c, h_.CharStatus(c));

                    h_.Exec("DELETE FROM accounts_parties WHERE charid = ?", BoundValue.Of(c.Id));
                    SendReload();
                    // still in the party, so the new pool is a Party one (as the C++)
                    h_.ResetTreasurePool(c);

                    Members.Remove(c);
                    c.Party = null;
                }
            }

            public void DelMember(CharData c)
            {
                if (c == null || c.Party != this) return;

                if (Leader == c)
                {
                    if (RemovePartyLeader(c)) ReloadParty(); // only if the party has not disbanded
                }
                else
                {
                    if (Members.Contains(c))
                    {
                        if (QuarterMaster == c) SetQuarterMaster("");
                        LeaveSync(c);

                        c.Latents.CheckLatentsPartyMembers(Members.Count, 0);
                        h_.PushTo(c, h_.GroupTbl(null));
                        h_.PushTo(c, GroupList(c, 0, 0, c.Zone));
                        h_.PushTo(c, h_.CharStatus(c));
                        c.Party = null;
                        Members.Remove(c);
                        h_.ResetTreasurePool(c);
                    }
                    ReloadParty();
                }
            }

            // the char leaves this process (zoning, logout, timeout): the party stays in the database
            public void PopMember(CharData c)
            {
                if (c == null || c.Party != this) return;

                Members.Remove(c);
                if (Leader == c) Leader = null;
                if (QuarterMaster == c) QuarterMaster = null;
                if (SyncTarget == c) SyncTarget = null;

                if (Members.Count == 0 && Alliance != null)
                {
                    if (Alliance.MainParty == this) Alliance.MainParty = null;
                    Alliance.PartyList.Remove(this);
                }
                c.Party = null;
            }

            private bool RemovePartyLeader(CharData c)
            {
                if (Members.Count == 0) return false;

                var rs = h_.Exec("SELECT charname FROM accounts_sessions JOIN chars ON accounts_sessions.charid = chars.charid " +
                                 "JOIN accounts_parties ON accounts_parties.charid = chars.charid WHERE partyid = ? AND NOT partyflag & ? " +
                                 "ORDER BY timestamp ASC LIMIT 1",
                                 BoundValue.Of(Id), BoundValue.Of(PartyLeaderFlag));
                if (rs != null && rs.RowsCount() > 0 && rs.Next()) SetLeader(rs.Get<string>("charname"));

                if (Leader == c)
                {
                    DisbandParty();
                    return false;
                }
                RemoveMember(c);
                return true;
            }

            // CParty::PushEffectsPacket: each member gets the icons of the others in this party who are in its zone
            public void PushEffectsPacket()
            {
                if (!EffectsChanged) return;
                var info = GetPartyInfo();
                foreach (var member in Members)
                {
                    var sameZone = new List<CharData>();
                    foreach (var m in info)
                    {
                        if (m.PartyId != Id || m.Id == member.Id) continue;
                        var (other, _) = h_.CharById(m.Id);
                        if (other != null && SameArea(other, member)) sameZone.Add(other);
                    }
                    h_.PushTo(member, GroupEffects(sameZone));
                }
                EffectsChanged = false;
            }

            public List<PartyMemberInfo> GetPartyInfo()
            {
                var info = new List<PartyMemberInfo>();
                var rs = h_.Exec("SELECT chars.charid, partyid, allianceid, charname, partyflag, pos_zone, pos_prevzone FROM accounts_parties " +
                                 "LEFT JOIN chars ON accounts_parties.charid = chars.charid WHERE " +
                                 "(allianceid <> 0 AND allianceid = ?) OR partyid = ? ORDER BY partyflag & ?, timestamp",
                                 BoundValue.Of(Alliance?.Id ?? 0u), BoundValue.Of(Id), BoundValue.Of(PartySecond | PartyThird));
                if (rs != null && rs.RowsCount() > 0)
                    while (rs.Next())
                        info.Add(new PartyMemberInfo(rs.Get<uint>("charid"), rs.Get<uint>("partyid"), rs.Get<uint>("allianceid"),
                                                     rs.Get<string>("charname"), rs.Get<ushort>("partyflag"),
                                                     rs.Get<ushort>("pos_zone"), rs.Get<ushort>("pos_prevzone")));
                return info;
            }

            public void AddMember(CharData c)
            {
                if (c == null || c.Party != null || Members.Contains(c) || IsFull()) return;
                if (HasTrusts())
                {
                    h_.log_("CParty::AddMember() - Party had summoned trusts when trying to add a member.");
                    return;
                }

                c.Party = this;
                Members.Add(c);
                if (Members.Count > 1 && Leader != null) Leader.LeaderCreatedPartyTime = h_.NowMs();

                h_.Exec("INSERT INTO accounts_parties (charid, partyid, allianceid, partyflag) VALUES (?, ?, ?, ?)",
                        BoundValue.Of(c.Id), BoundValue.Of(Id), BoundValue.Of(Alliance?.Id ?? 0u), BoundValue.Of(GetMemberFlags(c)));
                SendReload();

                ReloadTreasurePool(c);
                if (c.InviteFlg) h_.RemoveSeekFlag(c);
                c.TreasurePool.UpdatePool(c);

                // a synced party syncs a newcomer in the designee's zone
                if (SyncTarget != null && c.Zone == SyncTarget.Zone)
                {
                    h_.PushTo(c, SelfPackets.BattleMessage(c, 0, SyncTarget.MLvl, MsgBasicLevelSyncActivated));
                    SyncMember(c, SyncTarget.MLvl, EffectFlag.Dispelable | EffectFlag.OnZone, true);
                }
                // You lose all your summoned trusts upon joining a party
                h_.ClearTrusts(c);
                c.History[HistoryJoinedParties]++;
            }

            // a member on another process
            public void AddMember(uint id)
            {
                if (IsFull()) return;

                ushort flags = 0;
                if (Alliance != null)
                {
                    if (Number == 1) flags = PartySecond;
                    else if (Number == 2) flags = PartyThird;
                }
                h_.Exec("INSERT INTO accounts_parties (charid, partyid, allianceid, partyflag) VALUES (?, ?, ?, ?)",
                        BoundValue.Of(id), BoundValue.Of(Id), BoundValue.Of(Alliance?.Id ?? 0u), BoundValue.Of(flags));
                SendReload();
            }

            // a member already in the database, joining this process's view
            public void PushMember(CharData c)
            {
                if (c == null || c.Party != null) return;

                c.Party = this;
                Members.Add(c);

                foreach (var m in GetPartyInfo())
                {
                    if (m.Id != c.Id) continue;
                    if ((m.Flags & PartyLeaderFlag) != 0) Leader = c;
                    if ((m.Flags & PartyQm) != 0) QuarterMaster = c;
                    if ((m.Flags & PartySync) != 0) SyncTarget = c;
                }

                ReloadTreasurePool(c);
            }

            // CParty::ReloadTreasurePool: a zone's pool stays; else the pool of the first member of the alliance (or of
            // this party) in the char's zone that has one, else a pool of its own when it has none
            public void ReloadTreasurePool(CharData c)
            {
                if (c == null)
                {
                    h_.log_("CParty::ReloadTreasurePool() - PChar was null.");
                    return;
                }
                if (c.TreasurePool?.Type == TreasurePoolType.Zone) return;

                if (c.Party != null)
                {
                    var others = new List<CharData>();
                    if (c.Party.Alliance != null)
                        foreach (var party in c.Party.Alliance.PartyList) others.AddRange(party.Members);
                    else others.AddRange(Members);

                    foreach (var m in others)
                    {
                        if (m == c || m.TreasurePool == null || !SameArea(m, c)) continue;
                        c.TreasurePool?.DelMember(c);
                        c.TreasurePool = m.TreasurePool;
                        c.TreasurePool.AddMember(c);
                        return;
                    }
                }

                if (c.TreasurePool == null) h_.NewSoloTreasurePool(c);
            }

            private ushort GetMemberFlags(CharData c)
            {
                if (c == null || c.Party != this) return 0;

                ushort flags = 0;
                if (Alliance != null && c == Leader && Alliance.MainParty == this) flags |= AllianceLeaderFlag;
                if (Number == 1) flags += PartySecond;
                else if (Number == 2) flags += PartyThird;
                if (c == Leader) flags |= PartyLeaderFlag;
                if (c == QuarterMaster) flags |= PartyQm;
                if (c == SyncTarget) flags |= PartySync;
                return flags;
            }

            // the party list for every member (of every party of the alliance)
            public void ReloadParty()
            {
                var info = GetPartyInfo();

                if (Alliance != null)
                {
                    foreach (var party in Alliance.PartyList.ToArray())
                    {
                        party.RefreshFlags(info);
                        foreach (var m in party.Members)
                        {
                            m.ReloadParty = false;
                            h_.PushTo(m, h_.GroupTbl(party));
                            PushMemberList(m, info, true);
                        }
                    }
                }
                else
                {
                    RefreshFlags(info);
                    foreach (var m in Members.ToArray())
                    {
                        m.Latents.CheckLatentsPartyJobs();
                        m.Latents.CheckLatentsPartyMembers(Members.Count, LeaderTrustCount());
                        m.Latents.CheckLatentsPartyAvatar();
                        m.ReloadParty = false;
                        h_.PushTo(m, h_.GroupTbl(this, Leader != null && SameArea(m, Leader)));
                        PushMemberList(m, info, false, true);
                    }
                }
            }

            // the party list for just this char
            public void ReloadPartyMembers(CharData c)
            {
                c.ReloadParty = false;
                h_.PushTo(c, h_.GroupTbl(this));
                var info = GetPartyInfo();
                RefreshFlags(info);
                PushMemberList(c, info, true);
            }

            // one GROUP_LIST per row: the member's numbers when it is on this process, else what the database knows;
            // numbered within its party when the alliance groups count
            private void PushMemberList(CharData to, List<PartyMemberInfo> info, bool numberPerParty, bool withTrusts = false)
            {
                int group = 0;
                byte j = 0;
                foreach (var row in info)
                {
                    if (numberPerParty && (row.Flags & (PartySecond | PartyThird)) != group)
                    {
                        group = row.Flags & (PartySecond | PartyThird);
                        j = 0;
                    }
                    var (member, _) = h_.CharById(row.Id);
                    if (member != null)
                    {
                        h_.PushTo(to, GroupList(member, j, row.Flags, to.Zone));
                        // Inject the party leader's trusts into the party list (after each member here, as the C++)
                        if (withTrusts && Leader != null)
                        {
                            foreach (var trust in Leader.PTrusts)
                            {
                                j++;
                                h_.PushTo(to, TrustGroupList(trust, j));
                            }
                        }
                    }
                    else h_.PushTo(to, GroupList(row.Id, row.Name, row.Flags, row.Zone == 0 ? row.PrevZone : row.Zone));
                    j++;
                }
            }

            public void SetLeader(string name)
            {
                var rs = h_.Exec("SELECT chars.charid from accounts_sessions JOIN chars ON chars.charid = accounts_sessions.charid WHERE charname = ?",
                                 BoundValue.Of(name));
                if (rs == null || rs.RowsCount() == 0 || !rs.Next()) return;
                uint newId = rs.Get<uint>(0);

                h_.Exec("UPDATE accounts_parties SET partyflag = partyflag & ~? WHERE partyid = ? AND partyflag & ?",
                        BoundValue.Of(AllianceLeaderFlag | PartyLeaderFlag), BoundValue.Of(Id), BoundValue.Of(PartyLeaderFlag));
                h_.Exec("UPDATE accounts_parties SET partyid = ? WHERE partyid = ?", BoundValue.Of(newId), BoundValue.Of(Id));
                h_.Exec("UPDATE accounts_parties SET allianceid = ? WHERE allianceid = ?", BoundValue.Of(newId), BoundValue.Of(Id));

                Leader = GetMemberByName(name);
                if (Alliance != null && Alliance.Id == Id) Alliance.Id = newId;

                Id = newId;
                h_.Exec("UPDATE accounts_parties SET partyflag = partyflag | IF(allianceid = partyid, ?, ?) WHERE charid = ?",
                        BoundValue.Of(AllianceLeaderFlag | PartyLeaderFlag), BoundValue.Of(PartyLeaderFlag), BoundValue.Of(newId));

                // Passing leader dismisses trusts
                foreach (var m in Members.ToArray()) h_.ClearTrusts(m);
            }

            public void SetQuarterMaster(string name)
            {
                var qm = GetMemberByName(name);
                QuarterMaster = qm;

                h_.Exec("UPDATE accounts_parties SET partyflag = partyflag & ~? WHERE partyid = ? AND partyflag & ?",
                        BoundValue.Of(PartyQm), BoundValue.Of(Id), BoundValue.Of(PartyQm));
                if (qm != null)
                    h_.Exec("UPDATE accounts_parties JOIN chars ON accounts_parties.charid = chars.charid " +
                            "SET partyflag = partyflag | ? WHERE partyid = ? AND charname = ?",
                            BoundValue.Of(PartyQm), BoundValue.Of(Id), BoundValue.Of(name));
            }

            // the members but the sender, not zoning or jailed, in the zone unless it is 0
            public void PushPacket(uint senderId, ushort zone, BasicPacket p)
            {
                foreach (var m in Members)
                {
                    if (m.Id == senderId || InPrison(m)) continue;
                    var s = h_.SessionOf(m);
                    if (s == null || h_.zoneChanges_.ContainsKey(s)) continue;
                    if (zone == 0 || m.Zone == zone) Push(s, new BasicPacket(p.Buffer));
                }
            }

            public bool HasOnlyOneMember() => Members.Count == 1 && LoadPartySize() == 1;

            public bool IsFull() => Members.Count > 5 || LoadPartySize() > 5;

            private uint LoadPartySize()
            {
                var rs = h_.Exec("SELECT COUNT(*) FROM accounts_parties WHERE partyid = ?", BoundValue.Of(Id));
                return rs != null && rs.RowsCount() > 0 && rs.Next() ? rs.Get<uint>(0) : 0;
            }

            private CharData MemberById(uint id)
            {
                foreach (var m in Members)
                    if (m.Id == id) return m;
                return null;
            }

            private void RefreshFlags(List<PartyMemberInfo> info)
            {
                Leader = null;
                QuarterMaster = null;
                SyncTarget = null;

                foreach (var row in info)
                {
                    if (row.PartyId != Id) continue;
                    var m = MemberById(row.Id);
                    if ((row.Flags & PartyLeaderFlag) != 0) Leader = m;
                    if ((row.Flags & PartyQm) != 0) QuarterMaster = m;
                    if ((row.Flags & PartySync) != 0) SyncTarget = m;
                    if ((row.Flags & AllianceLeaderFlag) != 0 && Alliance != null) Alliance.MainParty = m != null ? this : null;
                }
            }
        }

        // CAlliance
        internal sealed class Alliance
        {
            private readonly ZoneHost h_;
            public uint Id;
            public Party MainParty;
            public readonly List<Party> PartyList = new List<Party>();

            // CAlliance(PEntity): the char's party leads it
            public Alliance(ZoneHost h, CharData c)
            {
                h_ = h;
                Id = c.Party.Id;
                AddParty(c.Party);
                MainParty = c.Party;
                h_.Exec("UPDATE accounts_parties SET partyflag = partyflag | ? WHERE partyid = ? AND partyflag & ?",
                        BoundValue.Of(AllianceLeaderFlag), BoundValue.Of(Id), BoundValue.Of(PartyLeaderFlag));
            }

            public Alliance(ZoneHost h, uint id)
            {
                h_ = h;
                Id = id;
            }

            public void DissolveAlliance(bool playerInitiated = true)
            {
                if (playerInitiated)
                {
                    h_.SendIpc(new AllianceDissolve(Id));
                    return;
                }

                // the map's own address, from a member's session: just this process's rows
                ulong ipp = 0;
                foreach (var party in PartyList)
                    foreach (var m in party.Members)
                        if (h_.SessionOf(m) is MapSession s && s.ZoneIpp != 0) ipp = s.ZoneIpp;
                uint ip = ipp == 0 ? 0 : MapIpp.Ip(ipp);
                ushort port = ipp == 0 ? (ushort)0 : MapIpp.Port(ipp);

                h_.Exec("UPDATE accounts_parties JOIN accounts_sessions USING (charid) " +
                        "SET allianceid = 0, partyflag = partyflag & ~? " +
                        "WHERE allianceid = ? AND IF(? = 0 AND ? = 0, true, server_addr = ? AND server_port = ?)",
                        BoundValue.Of(AllianceLeaderFlag | PartySecond | PartyThird), BoundValue.Of(Id),
                        BoundValue.Of(ip), BoundValue.Of(port), BoundValue.Of(ip), BoundValue.Of(port));

                while (PartyList.Count > 0)
                {
                    var party = PartyList[0];
                    DelParty(party);
                    party.ReloadParty();
                }
                PartyList.Clear();
            }

            public bool HasOnlyOneParty() => PartyList.Count == 1 && LoadPartyCount() == 1;

            public bool IsFull() => PartyList.Count == 3 || LoadPartyCount() == 3;

            private uint LoadPartyCount() =>
                h_.Exec("SELECT * FROM accounts_parties WHERE allianceid = ? GROUP BY partyid", BoundValue.Of(Id))?.RowsCount() ?? 0;

            public void RemoveParty(Party party)
            {
                if (party == null) return;

                if (MainParty == party)
                {
                    var rs = h_.Exec("SELECT charname FROM accounts_sessions JOIN chars ON accounts_sessions.charid = chars.charid " +
                                     "JOIN accounts_parties ON accounts_parties.charid = chars.charid WHERE allianceid = ? AND partyflag & ? " +
                                     "AND partyid != ? ORDER BY timestamp ASC LIMIT 1",
                                     BoundValue.Of(Id), BoundValue.Of(PartyLeaderFlag), BoundValue.Of(party.Id));
                    if (rs != null && rs.RowsCount() > 0 && rs.Next()) AssignAllianceLeader(rs.Get<string>("charname"));

                    if (MainParty == party)
                    {
                        DissolveAlliance();
                        return;
                    }
                }

                DelParty(party);
                h_.Exec("UPDATE accounts_parties SET allianceid = 0, partyflag = partyflag & ~? WHERE partyid = ?",
                        BoundValue.Of(AllianceLeaderFlag | PartySecond | PartyThird), BoundValue.Of(party.Id));
                h_.SendIpc(new AllianceReload(Id));
                h_.SendIpc(new PartyReload(party.Id));
            }

            public void DelParty(Party party)
            {
                if (party?.Alliance == null || party.Alliance.PartyList.Count == 0) return;

                var alliance = party.Alliance;
                alliance.PartyList.Remove(party);
                foreach (var entry in alliance.PartyList.ToArray()) entry.ReloadParty();

                party.Alliance = null;
                party.Number = 0;

                // out of the alliance's pool (not a zone's), then into the party's own
                foreach (var m in party.Members)
                    if (m.TreasurePool != null && m.TreasurePool.Type != TreasurePoolType.Zone) m.TreasurePool.DelMember(m);
                foreach (var m in party.Members)
                    m.Party?.ReloadTreasurePool(m);
            }

            public void AddParty(Party party)
            {
                if (party == null || PartyList.Contains(party) || PartyList.Count == 3) return;

                party.Alliance = this;
                PartyList.Add(party);

                byte newParty = 0;
                var rs = h_.Exec("SELECT partyflag & ? FROM accounts_parties WHERE allianceid = ? ORDER BY partyflag & ? ASC",
                                 BoundValue.Of(PartySecond | PartyThird), BoundValue.Of(Id), BoundValue.Of(PartySecond | PartyThird));
                if (rs != null && rs.RowsCount() > 0)
                    while (rs.Next())
                        if (rs.Get<byte>(0) == newParty) newParty++;

                foreach (var m in party.Members) h_.SaveCharStats(m);

                h_.Exec("UPDATE accounts_parties SET allianceid = ?, partyflag = partyflag | ? WHERE partyid = ?",
                        BoundValue.Of(Id), BoundValue.Of(newParty), BoundValue.Of(party.Id));
                party.Number = newParty;
                foreach (var m in party.Members)
                {
                    party.ReloadTreasurePool(m);
                    m.History[HistoryJoinedAlliances]++;
                }
                h_.SendIpc(new AllianceReload(Id));
            }

            // a party on another process
            public void AddParty(uint partyId)
            {
                int newParty = 0;
                var rs = h_.Exec("SELECT partyflag FROM accounts_parties WHERE allianceid = ? ORDER BY partyflag & ? ASC",
                                 BoundValue.Of(Id), BoundValue.Of(PartySecond | PartyThird));
                if (rs != null && rs.RowsCount() > 0)
                    while (rs.Next())
                        if ((rs.Get<byte>("partyflag") & (PartySecond | PartyThird)) == newParty) newParty++;

                h_.Exec("UPDATE accounts_parties SET allianceid = ?, partyflag = partyflag | ? WHERE partyid = ?",
                        BoundValue.Of(Id), BoundValue.Of(newParty), BoundValue.Of(partyId));
                h_.SendIpc(new AllianceReload(Id));
            }

            public void PushParty(Party party, byte number)
            {
                if (party == null) return;
                party.Alliance = this;
                PartyList.Add(party);
                party.Number = number;
                foreach (var m in party.Members)
                {
                    party.ReloadTreasurePool(m);
                    h_.SaveCharStats(m);
                }
            }

            public void AssignAllianceLeader(string name)
            {
                var rs = h_.Exec("SELECT chars.charid from accounts_sessions JOIN chars USING (charid) JOIN accounts_parties USING (charid) " +
                                 "WHERE charname = ? AND allianceid = ? AND partyflag & ?",
                                 BoundValue.Of(name), BoundValue.Of(Id), BoundValue.Of(PartyLeaderFlag));
                if (rs == null || rs.RowsCount() == 0 || !rs.Next()) return;
                uint charId = rs.Get<uint>("charid");

                h_.Exec("UPDATE accounts_parties SET partyflag = partyflag & ~? WHERE allianceid = ? AND partyflag & ?",
                        BoundValue.Of(AllianceLeaderFlag), BoundValue.Of(Id), BoundValue.Of(AllianceLeaderFlag));
                h_.Exec("UPDATE accounts_parties SET allianceid = ? WHERE allianceid = ?", BoundValue.Of(charId), BoundValue.Of(Id));

                Id = charId;
                MainParty = null;
                foreach (var party in PartyList)
                    if (party.GetMemberByName(name) != null)
                    {
                        MainParty = party;
                        break;
                    }

                h_.Exec("UPDATE accounts_parties SET partyflag = partyflag | ? WHERE charid = ?", BoundValue.Of(AllianceLeaderFlag), BoundValue.Of(charId));
            }
        }

        // ---- S2C ----------------------------------------------------------------------------------------------------

        // GP_SERV_COMMAND_GROUP_TBL (0x0C8): Kind @4, then up to 20 entries of 12 bytes from @8 (UniqueNo, ActIndex of a
        // char on this process, the low flag bits, ZoneNo @+8); no party sends it empty
        private BasicPacket GroupTbl(Party party, bool loadTrust = false)
        {
            var w = new PacketWriter(0x0C8, 248);
            if (party == null) return w.Packet;

            w.U8(4, party.Alliance != null ? KindAlliance_ : KindParty_);
            var rs = Exec("SELECT chars.charid, partyflag, pos_zone, pos_prevzone FROM accounts_parties " +
                          "LEFT JOIN chars ON accounts_parties.charid = chars.charid WHERE " +
                          "IF (allianceid <> 0, allianceid = ?, partyid = ?) ORDER BY partyflag & ?, timestamp",
                          BoundValue.Of(party.Alliance?.Id ?? 0u), BoundValue.Of(party.Id), BoundValue.Of(PartySecond | PartyThird));
            int i = 0;
            while (rs != null && i < 20 && rs.Next())
            {
                uint id = rs.Get<uint>("charid");
                var (c, _) = CharById(id);
                ushort zone = rs.GetOrDefault<ushort>("pos_zone", 0);
                int off = 8 + i * 12;
                w.U32(off, id);
                w.U16(off + 4, c?.TargId ?? 0);
                w.U8(off + 6, (int)(rs.Get<uint>("partyflag") & 0xFF));
                w.U16(off + 8, zone != 0 ? zone : rs.Get<ushort>("pos_prevzone"));
                i++;
            }
            // the leader's trusts, in the main party
            if (loadTrust && party.Leader != null)
            {
                foreach (var trust in party.Leader.PTrusts)
                {
                    if (i >= 20) break;
                    int off = 8 + i * 12;
                    w.U32(off, trust.Id);
                    w.U16(off + 4, trust.TargId);
                    w.U16(off + 8, trust.Zone);
                    i++;
                }
            }
            return w.Packet;
        }

        // GP_SERV_COMMAND_GROUP_LIST (0x0DD) for a char on this process: its numbers only when it is in the zone asked
        // for, else just the zone it is in; the packet cut to the name rounded up to 4, plus 4
        private static BasicPacket GroupList(CharData c, byte memberNumber, ushort flags, ushort zone)
        {
            var w = GroupListHead(c.Id, c.Name, flags);
            if (c.Zone != zone) w.U16(32, c.Zone);
            else
            {
                w.I32(8, c.Hp);
                w.I32(12, c.Mp);
                w.I32(16, c.Tp);
                w.U16(24, c.TargId);
                w.U8(26, memberNumber);
                w.U8(29, c.Hpp);
                w.U8(30, c.Mpp);
                if (!c.AnonymityFlg)
                {
                    w.U8(34, c.MJob);
                    w.U8(35, c.MLvl);
                    w.U8(36, c.SJob);
                    w.U8(37, c.SLvl);
                }
            }
            return w.Packet;
        }

        // GP_SERV_COMMAND_GROUP_LIST for a member elsewhere: the database's name, flags and zone
        private static BasicPacket GroupList(uint id, string name, ushort flags, ushort zone)
        {
            var w = GroupListHead(id, name, flags);
            w.U16(32, zone);
            return w.Packet;
        }

        private static PacketWriter GroupListHead(uint id, string name, ushort flags)
        {
            var w = new PacketWriter(0x0DD, 60);
            w.U32(4, id);
            w.U32(20, (uint)(flags & 0x1FF));
            w.U8(27, (flags & PartyMoghouse) != 0 ? 1 : 0);
            var n = CborRaw.Bytes(name ?? "");
            int len = Math.Min(n.Length, 16);
            w.Bytes(40, n.AsSpan(0, len));
            w.SetSize((uint)(4 + 52 - 16 + ((len + 3) & ~3) + 4));
            return w;
        }

        // GP_SERV_COMMAND_GROUP_SOLICIT_REQ (0x0DC): the invite, naming the inviter
        private static BasicPacket GroupSolicitReq(uint id, ushort targid, string inviterName, byte kind)
        {
            var w = new PacketWriter(0x0DC, 32);
            w.U32(4, id);
            w.U16(8, targid);
            w.U8(11, kind);
            var n = CborRaw.Bytes(inviterName);
            w.Bytes(12, n.AsSpan(0, Math.Min(n.Length, 16)));
            return w.Packet;
        }

        // GP_SERV_COMMAND_GROUP_CHECKID (0x0E1): the party id, 0 without one
        private static BasicPacket GroupCheckId(CharData c)
        {
            var w = new PacketWriter(0x0E1, 8);
            if (c?.Party != null) w.U32(4, c.Party.Id);
            return w.Packet;
        }

        // GP_SERV_COMMAND_PARTYREQ (0x11D): someone asks to join
        private static BasicPacket PartyReq(uint id, ushort targid, string requesterName, ushort race, byte status)
        {
            var w = new PacketWriter(0x11D, 32);
            w.U32(4, id);
            w.U16(8, targid);
            w.U8(11, status);
            var n = CborRaw.Bytes(requesterName);
            w.Bytes(12, n.AsSpan(0, Math.Min(n.Length, 16)));
            w.U16(28, race);
            return w.Packet;
        }

        // ---- charutils ----------------------------------------------------------------------------------------------

        // zoneutils::GetCharFromWorld(id, targid)
        private CharData CharByIdAndTargId(uint id, ushort targid)
        {
            var (c, _) = CharById(id);
            return c != null && c.TargId == targid ? c : null;
        }

        // a party on this process with this id
        private Party PartyById(uint id)
        {
            foreach (var (c, _) in charSessions_)
                if (c.Party != null && c.Party.Id == id) return c.Party;
            return null;
        }

        private Alliance AllianceById(uint id)
        {
            foreach (var (c, _) in charSessions_)
                if (c.Party?.Alliance != null && c.Party.Alliance.Id == id) return c.Party.Alliance;
            return null;
        }

        // charutils::getCharIdFromName
        private uint CharIdFromName(string name)
        {
            var rs = Exec("SELECT charid FROM chars WHERE charname = ? LIMIT 1", BoundValue.Of(name));
            return rs != null && rs.RowsCount() > 0 && rs.Next() ? rs.Get<uint>("charid") : 0;
        }

        // charutils::RemoveSeekFlag
        private void RemoveSeekFlag(CharData c)
        {
            c.Settings &= ~1u;
            c.UpdateMask |= CharStats.UpdateHp;

            SaveCharStats(c);
            Exec("UPDATE chars SET settings = ? WHERE charid = ? LIMIT 1", BoundValue.Of(c.Settings), BoundValue.Of(c.Id));

            PushTo(c, GameOkPackets.Config(c));
            PushTo(c, CharStatus(c));
            PushTo(c, CharSyncOf(c));
        }

        // charutils::LeaveParty
        private void LeaveParty(CharData c)
        {
            if (c.Party == null) return;

            // a party of one leaves its alliance before it disbands
            if (c.Party.Alliance != null && c.Party.HasOnlyOneMember())
            {
                if (c.Party.Alliance.HasOnlyOneParty()) c.Party.Alliance.DissolveAlliance();
                else c.Party.Alliance.RemoveParty(c.Party);
            }

            c.Party?.RemoveMember(c);
        }

        // a party of one in an alliance, or a party leaving its alliance: the alliance goes when it is the only party
        private static void LeaveAlliance(Party party)
        {
            if (party.Alliance.HasOnlyOneParty()) party.Alliance.DissolveAlliance();
            else party.Alliance.RemoveParty(party);
        }

        // charutils::ReloadParty: link the char to the party and alliance the database has it in, then send the lists
        private void ReloadPartyFor(CharData c)
        {
            var rs = Exec("SELECT partyid, allianceid, partyflag & ? AS partyflag FROM accounts_sessions s JOIN accounts_parties p ON " +
                          "s.charid = p.charid WHERE p.charid = ? LIMIT 1",
                          BoundValue.Of(PartySecond | PartyThird), BoundValue.Of(c.Id));
            if (rs != null && rs.RowsCount() > 0 && rs.Next())
            {
                uint partyId = rs.Get<uint>("partyid");
                uint allianceId = rs.Get<uint>("allianceid");
                byte partyNumber = (byte)rs.Get<uint>("partyflag");

                if (c.Party != null)
                {
                    if (c.Party.Id != partyId) c.Party.Id = partyId;
                }
                else
                {
                    var party = PartyById(partyId) ?? new Party(this, partyId);
                    party.PushMember(c);
                    // joins accepted on another process land here instead of CParty::AddMember
                    if (c.InviteFlg) RemoveSeekFlag(c);
                }

                // the party's sync for a member not yet under it, in the designee's zone while the sync holds
                if (c.Party.SyncTarget is { } syncTarget && c.Zone == syncTarget.Zone && !c.StatusEffects.HasStatusEffect(EffectId.LevelSync) &&
                    syncTarget.StatusEffects.GetStatusEffect(EffectId.LevelSync) is { DurationMs: 0 })
                {
                    PushTo(c, SelfPackets.BattleMessage(c, 0, syncTarget.MLvl, MsgBasicLevelSyncActivated));
                    c.Party.SyncMember(c, syncTarget.MLvl, EffectFlag.Dispelable, false);
                }

                if (allianceId != 0)
                {
                    if (c.Party.Alliance != null)
                    {
                        if (c.Party.Alliance.Id != allianceId) c.Party.Alliance.Id = allianceId;
                    }
                    else (AllianceById(allianceId) ?? new Alliance(this, allianceId)).PushParty(c.Party, partyNumber);
                }
                else if (c.Party.Alliance != null) c.Party.Alliance.DelParty(c.Party);

                c.Party.ReloadParty();
            }
            else
            {
                c.Party?.DelMember(c);
                c.ReloadParty = false;
            }

            // A party that was created with trusts disbands if they get dismissed
            if (c.Party != null && c.Party.FormedByTrusts && c.PTrusts.Count == 0) c.Party.DisbandParty();
        }

        // CCharEntity::PostTick: charutils::ReloadParty for the chars flagged since
        private void ProcessPartyReloads()
        {
            foreach (var c in new List<CharData>(charSessions_.Keys))
                if (c.ReloadParty) ReloadPartyFor(c);
        }

        // CBattleEntity::ForAlliance: every member of every party of the alliance, the party, or just the char
        private IEnumerable<CharData> ForAlliance(CharData c)
        {
            if (c.Party == null) return new[] { c };
            if (c.Party.Alliance == null) return c.Party.Members.ToArray();
            var all = new List<CharData>();
            foreach (var party in c.Party.Alliance.PartyList) all.AddRange(party.Members);
            return all;
        }

        // CZone::CharZoneOut / ~CCharEntity: a restricted char leaving drops its party's sync when it is the designee or
        // the leader, or when fewer than two others are left in the designee's zone; its LevelSync and restriction go
        private void LevelSyncCharZoneOut(CharData c)
        {
            if (c.LevelRestriction == 0) return;
            if (c.Party is { } party)
            {
                if (party.SyncTarget == c || party.Leader == c) party.SetSyncTarget("", MsgStdLevelSyncDeactivateLeftArea);
                if (party.SyncTarget is { } syncTarget)
                {
                    int count = 0;
                    foreach (var m in party.Members)
                        if (m != c && m.Zone == syncTarget.Zone) count++;
                    if (count < 2) party.SetSyncTarget("", MsgStdLevelSyncRemoveTooFewMembers);
                }
            }
            c.StatusEffects.DelStatusEffectSilent(EffectId.LevelSync);
            c.StatusEffects.DelStatusEffectSilent(EffectLevelRestriction);
        }

        // MapNetwork.OnZoneOut: the char left this process (zone change or logout, its 0x00B sent). A zone change tells
        // the world where it went (send_parse's CharZone: the world routes it to the destination's map process, which
        // opens a pending session for the char's 0x00A); the others are told to reload the list unless it went from a
        // mog house, and this process lets go of it.
        public void OnZoneOut(MapSession s)
        {
            if (s.Char is not CharData c) return;
            if (s.ZoneType != LogoutState.Logout) SendIpc(new CharZone(c.Id, (ZoneId)c.Destination));
            ReleaseFromParty(c);
            // ~CCharEntity
            WriteHistory(c);
            c.TreasurePool?.DelMember(c);
            c.LootRecastUntilMs.Clear();
            charSessions_.Remove(c);
            // ~CCharEntity: the char's CAIContainer goes, its action queue, Lua timers and (P10) listeners with it
            ClearActionQueues(c);
            ReleaseListeners(c);
            ResetEntityData(c, destroyed: true);
        }

        // ~CCharEntity: the members elsewhere reload (not for a mog house, whose own exit sends it); the database keeps
        // the char in the party
        private void ReleaseFromParty(CharData c)
        {
            if (c.Party == null) return;
            if (c.MoghouseId == 0)
            {
                if (c.Party.Alliance != null) SendIpc(new AllianceReload(c.Party.Alliance.Id));
                else SendIpc(new PartyReload(c.Party.Id));
            }
            c.Party.PopMember(c);
        }

        // ---- C2S ----------------------------------------------------------------------------------------------------

        private PacketValidator ValidateGroupSolicitReq(MapSession s, CharData c, BasicPacket p) =>
            Pv(c).BlockedBy(BlockedState.InEvent)
                 .OneOf("Kind", p.Buffer[10], KindParty_, KindAlliance_)
                 .MustNotEqual(U32(p, 4), c.Id, "Cannot invite yourself")
                 .MustEqual(IsBlacklisted(U32(p, 4), c.Id) ? 1 : 0, 0, "Character has inviter blacklisted");

        // GP_CLI_COMMAND_GROUP_SOLICIT_REQ: invite a char (or the leader of an unallied party) by id and act index
        private void OnGroupSolicitReq(MapSession s, CharData c, BasicPacket p)
        {
            uint inviteeId = U32(p, 4);
            ushort inviteeTargId = U16(p, 8);
            byte kind = p.Buffer[10];

            if (InPrison(c))
            {
                Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgCannotUseInArea));
                return;
            }

            // JP wiki: a Monipulator cannot party (monstrosity::CanParty)
            if (c.Monstrosity != null)
            {
                Push(s, MessageFor(c, 0, 0, MsgStdCannotInvite));
                return;
            }

            if (kind == KindParty_)
            {
                if (c.Party != null && c.Party.Leader != c)
                {
                    Push(s, MessageFor(c, 0, 0, MsgStdNotPartyLeader));
                    return;
                }
                if (c.Party != null && c.Party.IsFull())
                {
                    Push(s, MessageFor(c, 0, 0, MsgStdCannotInvite));
                    return;
                }

                var invitee = FindInvitee(c, inviteeId, inviteeTargId);
                if (invitee == null)
                {
                    SendIpc(new PartyInvite
                    {
                        InviteeId = inviteeId, InviteeTargId = inviteeTargId, InviterId = c.Id, InviterTargId = c.TargId,
                        InviterName = c.Name, InviteType = XI.Ipc.PartyKind.Party,
                    });
                    return;
                }

                if (invitee.Hp <= 0 || InPrison(invitee) || invitee.Monstrosity != null || invitee.InvitePending.UniqueNo != 0 || invitee.Party != null)
                {
                    Push(s, MessageFor(c, 0, 0, MsgStdCannotInvite));
                    return;
                }
                if (invitee.BlockingAid)
                {
                    BlockedInvite(s, invitee);
                    return;
                }
                if (invitee.StatusEffects.HasStatusEffect(EffectId.LevelSync))
                {
                    Push(s, MessageFor(c, 0, 0, MsgStdCannotInviteLevelSync));
                    return;
                }
                InviteLocal(c, invitee, inviteeId, inviteeTargId, KindParty_);
                if (c.Party?.SyncTarget != null) PushTo(invitee, MessageFor(invitee, 0, 0, MsgStdLevelSyncWarning));
            }
            else if (c.Party != null && c.Party.Leader == c &&
                     (c.Party.Alliance == null || (c.Party.Alliance.MainParty == c.Party && !c.Party.Alliance.IsFull())))
            {
                var invitee = FindInvitee(c, inviteeId, inviteeTargId);
                if (invitee == null)
                {
                    SendIpc(new PartyInvite
                    {
                        InviteeId = inviteeId, InviteeTargId = inviteeTargId, InviterId = c.Id, InviterTargId = c.TargId,
                        InviterName = c.Name, InviteType = XI.Ipc.PartyKind.Alliance,
                    });
                    return;
                }

                if (invitee.BlockingAid)
                {
                    BlockedInvite(s, invitee);
                    return;
                }
                if (invitee.Hp <= 0 || InPrison(invitee) || invitee.InvitePending.UniqueNo != 0 || invitee.Party == null ||
                    invitee.Party.Leader != invitee || invitee.Party.Alliance != null)
                {
                    Push(s, MessageFor(c, 0, 0, MsgStdCannotInvite));
                    return;
                }
                if (invitee.StatusEffects.HasStatusEffect(EffectId.LevelSync))
                {
                    Push(s, MessageFor(c, 0, 0, MsgStdCannotInviteLevelSync));
                    return;
                }
                InviteLocal(c, invitee, inviteeId, inviteeTargId, KindAlliance_);
            }
        }

        // the invitee on this process: by act index in the zone when one is given (it must match the id), else by id
        private CharData FindInvitee(CharData c, uint id, ushort targid)
        {
            if (targid != 0) return CharByTargId(c, targid) is CharData o && o.Id == id ? o : null;
            return CharById(id).Item1;
        }

        // /blockaid: "target is blocking", "interaction blocked" to the invitee, "cannot invite"
        private void BlockedInvite(MapSession s, CharData invitee)
        {
            Push(s, SystemMessage(MsgStdTargetIsCurrentlyBlocking));
            PushTo(invitee, SystemMessage(MsgStdBlockedByBlockaid));
            Push(s, SystemMessage(MsgStdCannotInvite));
        }

        private void InviteLocal(CharData inviter, CharData invitee, uint inviteeId, ushort inviteeTargId, byte kind)
        {
            invitee.InvitePending.UniqueNo = inviter.Id;
            invitee.InvitePending.ActIndex = inviter.TargId;
            invitee.InvitePending.Kind = kind;
            // the C++ sends the ids from the packet, which name the invitee
            PushTo(invitee, GroupSolicitReq(inviteeId, inviteeTargId, inviter.Name, kind));
        }

        private static PacketValidator ValidateGroupLeave(MapSession s, CharData c, BasicPacket p) =>
            Pv(c).BlockedBy(BlockedState.InEvent)
                 .OneOf("Kind", p.Buffer[4], KindParty_, KindAlliance_)
                 .Custom(v => { if (c.Party == null) v.AddError("Not in a party"); });

        // GP_CLI_COMMAND_GROUP_LEAVE: leave the party, or take the party out of the alliance
        private void OnGroupLeave(MapSession s, CharData c, BasicPacket p)
        {
            switch (p.Buffer[4])
            {
                case KindParty_:
                    LeaveParty(c);
                    break;
                case KindAlliance_:
                    if (c.Party?.Alliance != null && c.Party.Leader == c) LeaveAlliance(c.Party);
                    break;
            }
        }

        private static PacketValidator ValidateGroupBreakup(MapSession s, CharData c, BasicPacket p)
        {
            var pv = Pv(c).BlockedBy(BlockedState.InEvent)
                          .OneOf("Kind", p.Buffer[4], KindParty_, KindAlliance_)
                          .IsPartyLeader();
            if (p.Buffer[4] == KindParty_)
                pv.Custom(v => { if (c.Party == null || c.Party.Alliance != null) v.AddError("Cant break party while in alliance"); });
            else pv.IsAllianceLeader();
            return pv;
        }

        // GP_CLI_COMMAND_GROUP_BREAKUP: the leader disbands the party, or the alliance leader the alliance
        private void OnGroupBreakup(MapSession s, CharData c, BasicPacket p)
        {
            if (p.Buffer[4] == KindParty_) c.Party?.DisbandParty();
            else c.Party?.Alliance?.DissolveAlliance();
        }

        private static PacketValidator ValidateGroupStrike(MapSession s, CharData c, BasicPacket p)
        {
            byte kind = p.Buffer[10];
            var pv = Pv(c).BlockedBy(BlockedState.InEvent)
                          .MustEqual(U32(p, 4), 0, "UniqueNo not zero")
                          .MustEqual(U16(p, 8), 0, "ActIndex not zero")
                          .OneOf("Kind", kind, 0, 1, 2, 5);
            switch (kind)
            {
                case 0: pv.IsPartyLeader(); break;
                case 1:
                case 2: pv.HasLinkshellRank(kind, LinkshellItem.TypePearlsack); break;
                case 5: pv.IsAllianceLeader(); break;
            }
            return pv;
        }

        // GP_CLI_COMMAND_GROUP_STRIKE: kick a member from the party, or a party from the alliance, by name
        private void OnGroupStrike(MapSession s, CharData c, BasicPacket p)
        {
            string victimName = CborRaw.FromBytes(UntrustedBytes(p, 12, 15));

            switch (p.Buffer[10])
            {
                case KindParty_:
                {
                    if (c.Party.GetMemberByName(victimName) is CharData victim)
                    {
                        // kicking yourself works like /pcmd leave: a party of one leaves its alliance first
                        if (victim == c && c.Party.Alliance != null && c.Party.HasOnlyOneMember()) LeaveAlliance(c.Party);
                        c.Party?.RemoveMember(victim);
                    }
                    else if (CharIdFromName(victimName) is uint victimId && victimId != 0)
                    {
                        // the victim is on another process
                        var rs = Exec("DELETE FROM accounts_parties WHERE partyid = ? AND charid = ?", BoundValue.Of(c.Id), BoundValue.Of(victimId));
                        if (rs != null && rs.RowsAffected() > 0)
                        {
                            if (c.Party?.Alliance != null) SendIpc(new AllianceReload(c.Party.Alliance.Id));
                            else if (c.Party != null) SendIpc(new PartyReload(c.Party.Id));
                            // they are no longer in the database, so the reloads would not reach them
                            SendIpc(new PlayerKick(victimId));
                        }
                    }
                    break;
                }
                case KindAlliance_:
                {
                    var alliance = c.Party.Alliance;
                    CharData victim = null;
                    foreach (var party in alliance.PartyList.ToArray())
                    {
                        victim = party.GetMemberByName(victimName);
                        if (victim?.Party?.Alliance == null) continue;
                        // yourself, or the alliance leader kicking another party's leader: the party goes
                        if (victim == c || (alliance.MainParty == c.Party && victim.Party.Leader == victim)) LeaveAlliance(victim.Party);
                        break;
                    }
                    if (victim == null && alliance.MainParty == c.Party)
                    {
                        uint allianceId = alliance.Id;
                        uint victimId = CharIdFromName(victimName);
                        if (victimId == 0) break;
                        var rs = Exec("SELECT partyid FROM accounts_parties WHERE charid = ? AND allianceid = ? AND partyflag & ? LIMIT 1",
                                      BoundValue.Of(victimId), BoundValue.Of(allianceId), BoundValue.Of(PartyLeaderFlag | PartySecond | PartyThird));
                        if (rs == null || rs.RowsCount() == 0 || !rs.Next()) break;
                        uint partyId = rs.Get<uint>("partyid");
                        var rs2 = Exec("UPDATE accounts_parties SET allianceid = 0, partyflag = partyflag & ~? WHERE partyid = ?",
                                       BoundValue.Of(PartySecond | PartyThird), BoundValue.Of(partyId));
                        if (rs2 != null && rs2.RowsAffected() > 0)
                        {
                            SendIpc(new PartyReload(partyId));
                            SendIpc(new AllianceReload(allianceId));
                        }
                    }
                    break;
                }
                case 1:
                case 2:
                {
                    // Linkshell1/2: the world routes the kick to the victim's map
                    var ls = p.Buffer[10] == 1 ? c.Linkshell1 : c.Linkshell2;
                    var item = LinkshellItem.Equipped(c, p.Buffer[10] == 1 ? LinkshellItem.SlotLink1 : LinkshellItem.SlotLink2);
                    if (ls != null && item != null)
                        SendIpc(new LinkshellRemove
                        {
                            RequesterId = c.Id, RequesterRank = LinkshellItem.Type(item), VictimName = victimName, LinkshellId = ls.Id,
                        });
                    break;
                }
            }
        }

        private static PacketValidator ValidateGroupSolicitRes(MapSession s, CharData c, BasicPacket p) =>
            Pv(c).BlockedBy(BlockedState.InEvent).OneOf("Res", p.Buffer[4], 0, 1);

        // GP_CLI_COMMAND_GROUP_SOLICIT_RES: the answer to the pending invite
        private void OnGroupSolicitRes(MapSession s, CharData c, BasicPacket p)
        {
            byte res = p.Buffer[4];
            bool accepted = res == 1;
            if (accepted && (c.InvitePending.Kind == KindParty_ && c.Party != null || c.Monstrosity != null))
            {
                Push(s, MessageFor(c, 0, 0, MsgStdCannotBeProcessed));
                c.InvitePending.Clean();
                return;
            }

            var inviter = CharByIdAndTargId(c.InvitePending.UniqueNo, c.InvitePending.ActIndex);
            if (inviter == null)
            {
                // the inviter is on another process: the world routes the answer
                SendIpc(new PartyInviteResponse
                {
                    InviteeId = c.Id, InviteeTargId = c.TargId, InviterId = c.InvitePending.UniqueNo,
                    InviterTargId = c.InvitePending.ActIndex, InviteAnswer = res,
                });
            }
            else if (!accepted) PushTo(inviter, MessageFor(inviter, 0, 0, MsgStdInvitationDeclined));
            // the inviter became a Monipulator while the invite was pending
            else if (inviter.Monstrosity != null) Push(s, MessageFor(c, 0, 0, MsgStdCannotBeProcessed));
            else AcceptInvite(s, c, inviter);

            c.InvitePending.Clean();
        }

        private void AcceptInvite(MapSession s, CharData c, CharData inviter)
        {
            // both party leaders: an alliance invite
            if (c.Party != null && inviter.Party != null && inviter.Party.Leader == inviter && c.Party.Leader == c)
            {
                // no alliance with trusts out
                if (c.Party.HasTrusts() || inviter.Party.HasTrusts()) Push(s, MessageFor(c, 0, 0, MsgStdTrustCannotJoinAlliance));
                else if (inviter.Party.Alliance != null)
                {
                    if (inviter.Party.Alliance.IsFull() || inviter.Party.Alliance.MainParty != inviter.Party)
                    {
                        Push(s, MessageFor(c, 0, 0, MsgStdCannotBeProcessed));
                        return;
                    }
                    inviter.Party.Alliance.AddParty(c.Party);
                }
                else
                {
                    inviter.Party.Alliance = new Alliance(this, inviter);
                    inviter.Party.Alliance.AddParty(c.Party);
                }
            }

            // a party invite: not for a char under level sync and a restriction
            if (c.Party != null) return;
            if (c.StatusEffects.HasStatusEffect(EffectId.LevelSync) && c.StatusEffects.HasStatusEffect(EffectLevelRestriction))
            {
                Push(s, MessageFor(c, 0, 0, MsgStdCannotJoinLevelSync));
                return;
            }
            if (inviter.Party == null) _ = new Party(this, inviter);
            if (inviter.Party?.Leader != inviter) return;
            if (inviter.Party.IsFull()) Push(s, MessageFor(c, 0, 0, MsgStdCannotBeProcessed)); // someone else accepted
            else inviter.Party.AddMember(c);
        }

        private static PacketValidator ValidateGroupListReq(MapSession s, CharData c, BasicPacket p) =>
            Pv(c).BlockedBy(BlockedState.InEvent).MustEqual(p.Buffer[4], 0, "Kind not zero");

        // GP_CLI_COMMAND_GROUP_LIST_REQ: the party list again
        private void OnGroupListReq(MapSession s, CharData c, BasicPacket p)
        {
            if (c.Party != null) c.Party.ReloadPartyMembers(c);
            else Push(s, GroupTbl(null));
        }

        private static PacketValidator ValidateGroupChange2(MapSession s, CharData c, BasicPacket p)
        {
            byte kind = p.Buffer[20], change = p.Buffer[21];
            var pv = Pv(c).BlockedBy(BlockedState.InEvent)
                          .OneOf("Kind", kind, 0, 1, 2, 5)
                          .OneOf("ChangeKind", change, 0, 1, 2, 3, 4, 5, 6, 7);
            switch (change)
            {
                case ChangeSetPartyLeader:
                case ChangeSetQuartermaster:
                case ChangeSetLottery:
                case ChangeSetLevelSync:
                case ChangeDisableLevelSync:
                    pv.MustEqual(kind, KindParty_, "Invalid operation").IsPartyLeader();
                    break;
                case ChangePearlToSack:
                case ChangeSackToPearl:
                    pv.OneOf("Kind", kind, 1, 2).HasLinkshellRank(kind, LinkshellItem.TypeLinkshell);
                    break;
                case ChangeSetAllianceLeader:
                    pv.MustEqual(kind, KindAlliance_, "Invalid operation").IsAllianceLeader();
                    break;
            }
            return pv;
        }

        // GP_CLI_COMMAND_GROUP_CHANGE2: hand over the party or alliance lead, or set the quartermaster
        private void OnGroupChange2(MapSession s, CharData c, BasicPacket p)
        {
            string name = CborRaw.FromBytes(UntrustedBytes(p, 4, 16));
            switch (p.Buffer[20])
            {
                case KindParty_:
                    c.Party.AssignPartyRole(name, p.Buffer[21]);
                    break;
                case KindAlliance_:
                    c.Party.Alliance.AssignAllianceLeader(name);
                    if (c.Party?.Alliance != null) SendIpc(new AllianceReload(c.Party.Alliance.Id));
                    break;
                case 1:
                case 2:
                {
                    // Linkshell1/2: the holder's pearl/sack swap, routed to the member's map
                    var ls = p.Buffer[20] == 1 ? c.Linkshell1 : c.Linkshell2;
                    var item = LinkshellItem.Equipped(c, p.Buffer[20] == 1 ? LinkshellItem.SlotLink1 : LinkshellItem.SlotLink2);
                    if (ls != null && item != null)
                        SendIpc(new LinkshellRankChange
                        {
                            RequesterId = c.Id, RequesterRank = LinkshellItem.Type(item), MemberName = name, LinkshellId = ls.Id,
                            NewRank = p.Buffer[21],
                        });
                    break;
                }
            }
        }

        // GP_CLI_COMMAND_GROUP_CHECKID: the party id
        private void OnGroupCheckId(MapSession s, CharData c, BasicPacket p) => Push(s, GroupCheckId(c));

        private static PacketValidator ValidateGroupRequest(MapSession s, CharData c, BasicPacket p) =>
            Pv(c).BlockedBy(BlockedState.InEvent)
                 .MustNotEqual(U32(p, 4), c.Id, "Cannot request yourself")
                 .OneOf("Kind", p.Buffer[10], 0, 1)
                 .MustEqual(c.Monstrosity == null ? 1 : 0, 1, "Cannot request while in Monstrosity");

        // GP_CLI_COMMAND_PARTYREQ: ask a party leader to be invited, through the world; dropped silently when the
        // leader has the requester blacklisted
        private void OnGroupRequest(MapSession s, CharData c, BasicPacket p)
        {
            if (IsBlacklisted(U32(p, 4), c.Id)) return;
            SendIpc(new PartyRequest
            {
                RecipientId = U32(p, 4), RequesterId = c.Id, RequesterTargId = c.TargId, RequesterZoneId = (XI.Ipc.ZoneId)c.Zone,
                RequesterName = c.Name, RequesterRace = c.Look.Race, Kind = p.Buffer[10],
            });
        }

        // ---- IPC from the world -------------------------------------------------------------------------------------

        // IPCClient::handleMessage_ChatMessageParty ... PlayerKick
        private bool OnPartyIpc(IIpcMessage message)
        {
            switch (message)
            {
                case ChatMessageParty m:
                {
                    var p = ChatStd(m.SenderName, (ushort)m.ZoneId, (byte)m.MessageTypeField, CborRaw.Bytes(m.Message), m.GmLevel);
                    PartyById(m.PartyId)?.PushPacket(m.SenderId, 0, p);
                    return true;
                }
                case ChatMessageAlliance m:
                {
                    var alliance = AllianceById(m.AllianceId);
                    if (alliance == null) return true;
                    var p = ChatStd(m.SenderName, (ushort)m.ZoneId, (byte)m.MessageTypeField, CborRaw.Bytes(m.Message), m.GmLevel);
                    foreach (var party in alliance.PartyList) party.PushPacket(m.SenderId, 0, p);
                    return true;
                }
                case PartyInvite m:
                {
                    var (invitee, s) = CharById(m.InviteeId);
                    if (invitee == null) return true;
                    byte kind = (byte)m.InviteType;
                    if (invitee.Hp <= 0 || InPrison(invitee) || invitee.InvitePending.UniqueNo != 0 || invitee.Monstrosity != null ||
                        (invitee.Party != null && kind == KindParty_) ||
                        (kind == KindAlliance_ && (invitee.Party == null || invitee.Party.Leader != invitee || invitee.Party.Alliance != null)))
                    {
                        SendIpc(new MessageStandard { RecipientId = m.InviterId, Message = MsgStdCannotInvite });
                        return true;
                    }
                    if (invitee.BlockingAid)
                    {
                        SendIpc(new MessageSystem { RecipientId = m.InviterId, Message = MsgStdTargetIsCurrentlyBlocking });
                        Push(s, SystemMessage(MsgStdBlockedByBlockaid));
                        SendIpc(new MessageStandard { RecipientId = m.InviterId, Message = MsgStdCannotInvite });
                        return true;
                    }
                    if (invitee.StatusEffects.HasStatusEffect(EffectId.LevelSync))
                    {
                        SendIpc(new MessageStandard { RecipientId = m.InviterId, Message = MsgStdCannotInviteLevelSync });
                        return true;
                    }
                    invitee.InvitePending.UniqueNo = m.InviterId;
                    invitee.InvitePending.ActIndex = m.InviterTargId;
                    invitee.InvitePending.Kind = kind;
                    Push(s, GroupSolicitReq(m.InviterId, m.InviterTargId, m.InviterName, kind));
                    return true;
                }
                case PartyInviteResponse m:
                    OnPartyInviteResponse(m);
                    return true;
                case PartyRequest m:
                {
                    var (recipient, s) = CharById(m.RecipientId);
                    if (recipient == null) return true;
                    ushort targid = recipient.Zone == (ushort)m.RequesterZoneId ? m.RequesterTargId : (ushort)0;
                    Push(s, PartyReq(m.RequesterId, targid, m.RequesterName, m.RequesterRace, m.Kind));
                    return true;
                }
                case PartyReload m:
                    FlagReload("SELECT charid FROM accounts_parties WHERE partyid = ?", m.PartyId);
                    return true;
                case AllianceReload m:
                    FlagReload("SELECT charid FROM accounts_parties WHERE allianceid = ?", m.AllianceId);
                    return true;
                case PartyDisband m:
                    PartyById(m.PartyId)?.DisbandParty(false);
                    return true;
                case AllianceDissolve m:
                    AllianceById(m.AllianceId)?.DissolveAlliance(false);
                    return true;
                case PlayerKick m:
                {
                    // kicked and no longer in the database: a direct update
                    var (c, _) = CharById(m.VictimId);
                    if (c != null) c.ReloadParty = true;
                    return true;
                }
            }
            return false;
        }

        private void FlagReload(string sql, uint id)
        {
            var rs = Exec(sql, BoundValue.Of(id));
            if (rs == null || rs.RowsCount() == 0) return;
            while (rs.Next())
            {
                var (c, _) = CharById(rs.Get<uint>("charid"));
                if (c != null) c.ReloadParty = true;
            }
        }

        private void OnPartyInviteResponse(PartyInviteResponse m)
        {
            var (inviter, s) = CharById(m.InviterId);
            if (inviter == null) return;

            if (m.InviteAnswer == 0)
            {
                Push(s, MessageFor(inviter, 0, 0, MsgStdInvitationDeclined));
                return;
            }
            // the inviter became a Monipulator while the invite was pending
            if (inviter.Monstrosity != null)
            {
                SendIpc(new MessageStandard { RecipientId = m.InviteeId, Message = MsgStdCannotBeProcessed });
                return;
            }

            // both party leaders?
            var rs = Exec("SELECT * FROM accounts_parties WHERE partyid <> 0 AND ((charid = ? OR charid = ?) AND partyflag & ?)",
                          BoundValue.Of(m.InviterId), BoundValue.Of(m.InviteeId), BoundValue.Of(PartyLeaderFlag));
            if (rs != null && rs.RowsCount() == 2)
            {
                if (inviter.Party == null)
                    SendIpc(new MessageStandard { RecipientId = m.InviteeId, Message = MsgStdCannotBeProcessed });
                else if (inviter.Party.Alliance != null)
                {
                    var rs2 = Exec("SELECT * FROM accounts_parties WHERE allianceid <> 0 AND allianceid = (SELECT allianceid FROM accounts_parties where " +
                                   "charid = ?) GROUP BY partyid", BoundValue.Of(m.InviterId));
                    uint parties = rs2?.RowsCount() ?? 0;
                    if (parties > 0 && parties < 3 && inviter.Party.Alliance.MainParty == inviter.Party && !inviter.Party.HasTrusts())
                        inviter.Party.Alliance.AddParty(m.InviteeId);
                    else
                        SendIpc(new MessageStandard { RecipientId = m.InviteeId, Message = MsgStdCannotBeProcessed });
                }
                else if (inviter.Party.HasTrusts())
                    SendIpc(new MessageStandard { RecipientId = m.InviteeId, Message = MsgStdTrustCannotJoinAlliance });
                else new Alliance(this, inviter).AddParty(m.InviteeId);
            }
            else
            {
                if (inviter.Party == null) _ = new Party(this, inviter);
                if (inviter.Party != null && inviter.Party.Leader == inviter)
                {
                    var rs2 = Exec("SELECT * FROM accounts_parties WHERE partyid <> 0 AND charid = ?", BoundValue.Of(m.InviteeId));
                    if (rs2 != null && rs2.RowsCount() == 0) inviter.Party.AddMember(m.InviteeId);
                }
            }
        }
    }
}
