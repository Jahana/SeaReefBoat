using System;
using System.Collections.Generic;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // The C2S handlers for the client's own settings and display state (c2s/0x00f, 0x04b, 0x053, 0x0db, 0x0e0, 0x112,
    // 0x11b)
    public sealed partial class ZoneHost
    {
        // CCharStatusPacket
        private BasicPacket CharStatus(CharData c) => LoginPackets.ServerStatus(c, AreaOf(c), UnixNow());

        // SAVE_CONF MentorUnlockedFlg, MentorFlg, NewAdventurerOffFlg: the server's, whatever the client sends
        private const uint ServerConfigBits = 0x07000000;

        // GP_CLI_COMMAND_CONFIG_LANGUAGE: Kind 0 is the config word and chat filters, 1 the party search languages
        private void OnConfigLanguage(MapSession s, CharData c, BasicPacket p)
        {
            if (p.Buffer[6] == 0)
            {
                uint merged = U32(p, 8) & ~ServerConfigBits | c.Settings & ServerConfigBits;
                if (merged != c.Settings)
                {
                    c.Settings = merged;
                    Exec("UPDATE chars SET settings = ? WHERE charid = ? LIMIT 1", BoundValue.Of(c.Settings), BoundValue.Of(c.Id));
                }
                uint filters1 = U32(p, 12), filters2 = U32(p, 16);
                if (filters1 != c.ChatFilters1 || filters2 != c.ChatFilters2)
                {
                    c.ChatFilters1 = filters1;
                    c.ChatFilters2 = filters2;
                    Exec("UPDATE chars SET chatfilters_1 = ?, chatfilters_2 = ? WHERE charid = ? LIMIT 1",
                         BoundValue.Of(filters1), BoundValue.Of(filters2), BoundValue.Of(c.Id));
                }
            }
            else
            {
                uint param = U32(p, 36);
                if (c.Languages != param)
                {
                    c.Languages = (byte)param;
                    Exec("UPDATE chars SET languages = ? WHERE charid = ?", BoundValue.Of(c.Languages), BoundValue.Of(c.Id));
                }
            }
            Push(s, GameOkPackets.Config(c));
            Push(s, CharStatus(c));
        }

        private const int UserMsgOffset = 4, UserMsgSize = 128, UserMsgTypeOffset = 148;

        // asStringFromUntrustedSource(sMessage, 128): up to the first NUL
        private static byte[] UserMessage(BasicPacket p)
        {
            var span = p.Buffer.AsSpan(UserMsgOffset, UserMsgSize);
            int len = span.IndexOf((byte)0);
            return span.Slice(0, len < 0 ? UserMsgSize : len).ToArray();
        }

        // GP_CLI_COMMAND_SET_USERMSG: the search comment; an empty one is the Default type
        private void OnSetUserMsg(MapSession s, CharData c, BasicPacket p)
        {
            var message = UserMessage(p);
            byte type = message.Length == 0 ? (byte)0 : (byte)U32(p, UserMsgTypeOffset);
            if (type == c.SearchMessageType && message.AsSpan().SequenceEqual(c.SearchMessage)) return;

            var r = Exec("UPDATE accounts_sessions SET seacom_type = ?, seacom_message = ? WHERE charid = ? LIMIT 1",
                         BoundValue.Of(type), BoundValue.Of(message), BoundValue.Of(c.Id));
            if (r != null)
            {
                c.SearchMessage = message;
                c.SearchMessageType = type;
            }
            Push(s, CharStatus(c));
        }

        // GP_CLI_COMMAND_MASTERY_DISPLAY
        private void OnMasteryDisplay(MapSession s, CharData c, BasicPacket p)
        {
            bool on = p.Buffer[4] != 0;
            if (c.JobMasterDisplay == on) return;
            c.JobMasterDisplay = on;
            Exec("UPDATE chars SET job_master = ? WHERE charid = ? LIMIT 1", BoundValue.Of((byte)(on ? 1 : 0)), BoundValue.Of(c.Id));
            Push(s, CharStatus(c));
            Push(s, CharSyncOf(c));
        }

        // charutils::SetStyleLock: the style taken from the equipped gear (or cleared), and the message when the flag
        // changes
        private void SetStyleLock(MapSession s, CharData c, bool locked)
        {
            if (StyleLock.Set(c, locked)) Push(s, SelfPackets.Message(locked ? SelfPackets.MsgStyleLockOn : SelfPackets.MsgStyleLockOff));
        }

        // CharPersist::Look, saved at once: char_look gets the shown look, chars the flag, char_style the style items
        // (SaveCharAppearances)
        private void SaveCharAppearance(CharData c)
        {
            var l = c.ShownLook;
            Exec("INSERT INTO char_look (charid, head, body, hands, legs, feet, main, sub, ranged) VALUES (?,?,?,?,?,?,?,?,?) " +
                 "ON DUPLICATE KEY UPDATE head = VALUES(head), body = VALUES(body), hands = VALUES(hands), " +
                 "legs = VALUES(legs), feet = VALUES(feet), main = VALUES(main), sub = VALUES(sub), ranged = VALUES(ranged)",
                 BoundValue.Of(c.Id), BoundValue.Of(l.Head), BoundValue.Of(l.Body), BoundValue.Of(l.Hands), BoundValue.Of(l.Legs),
                 BoundValue.Of(l.Feet), BoundValue.Of(l.Main), BoundValue.Of(l.Sub), BoundValue.Of(l.Ranged));
            Exec("UPDATE chars SET isstylelocked = ? WHERE charid = ?", BoundValue.Of((byte)(c.StyleLocked ? 1 : 0)), BoundValue.Of(c.Id));
            var st = c.StyleItems;
            Exec("INSERT INTO char_style (charid, head, body, hands, legs, feet, main, sub, ranged) VALUES (?,?,?,?,?,?,?,?,?) " +
                 "ON DUPLICATE KEY UPDATE head = VALUES(head), body = VALUES(body), hands = VALUES(hands), " +
                 "legs = VALUES(legs), feet = VALUES(feet), main = VALUES(main), sub = VALUES(sub), ranged = VALUES(ranged)",
                 BoundValue.Of(c.Id), BoundValue.Of(st[StyleLock.SlotHead]), BoundValue.Of(st[StyleLock.SlotBody]),
                 BoundValue.Of(st[StyleLock.SlotHands]), BoundValue.Of(st[StyleLock.SlotLegs]), BoundValue.Of(st[StyleLock.SlotFeet]),
                 BoundValue.Of(st[StyleLock.SlotMain]), BoundValue.Of(st[StyleLock.SlotSub]), BoundValue.Of(st[StyleLock.SlotRanged]));
        }

        // updateClientAppearance
        private void SendAppearance(MapSession s, CharData c)
        {
            Push(s, LoginPackets.GrapList(c));
            Push(s, CharSyncOf(c));
        }

        private const int LockstyleItemsAt = 8, LockstyleItemSize = 8;

        // GP_CLI_COMMAND_LOCKSTYLE
        private void OnLockstyle(MapSession s, CharData c, BasicPacket p)
        {
            var items = loader_.Stats?.Items;
            switch (p.Buffer[5])
            {
                case 0: // Disable
                    if (!c.StyleLocked) return;
                    SetStyleLock(s, c, false);
                    SaveCharAppearance(c);
                    SendAppearance(s, c);
                    break;
                case 1: // Continue: the flag only, saved with the next appearance change
                    c.StyleLocked = true;
                    break;
                case 2: // Query
                    Push(s, SelfPackets.Message(c.StyleLocked ? SelfPackets.MsgStyleLockIsOn : SelfPackets.MsgStyleLockIsOff));
                    break;
                case 3: // Set
                    if (items == null)
                    {
                        log_($"zone: lockstyle set from {c.Name} needs the item tables");
                        return;
                    }
                    SetLockstyle(s, c, p, items);
                    break;
                case 4: // Enable
                    if (items == null)
                    {
                        log_($"zone: lockstyle enable from {c.Name} needs the item tables");
                        return;
                    }
                    SetStyleLock(s, c, true);
                    StyleLock.UpdateRemovedSlotsLook(c, items);
                    SaveCharAppearance(c);
                    SendAppearance(s, c);
                    break;
            }
        }

        // the Set mode: each lockstyleitem_t {ItemIndex, EquipKind, Category, pad, ItemNo} that fits its slot, is owned,
        // wearable by some job and (for a weapon slot) valid over what is equipped there becomes that slot's style item
        private void SetLockstyle(MapSession s, CharData c, BasicPacket p, ItemData items)
        {
            SetStyleLock(s, c, true);

            var failedItemIds = new System.Collections.Generic.List<ushort>();
            bool hasH2HInMainSlot = false;
            int count = p.Buffer[4];
            for (int i = 0; i < count; i++)
            {
                int at = LockstyleItemsAt + i * LockstyleItemSize;
                byte equipKind = p.Buffer[at + 1];
                ushort itemId = U16(p, at + 4);

                // non-visible slots
                if (equipKind > StyleLock.SlotFeet) continue;

                var item = StyleLock.Lookup(items, itemId);
                if (item == null || (item.EquipSlots & 1 << equipKind) == 0)
                {
                    itemId = 0;
                }
                else if (!c.HasItem(itemId, includeRecycleBin: false) || !StyleLock.CanEquipItemOnAnyJob(c, item) ||
                         equipKind <= StyleLock.SlotRanged && !StyleLock.HasValidStyle(c, c.Equipped[equipKind], item))
                {
                    failedItemIds.Add(itemId);
                    itemId = 0;
                }
                c.StyleItems[equipKind] = itemId;

                // as in the C++, the entry's index (not its slot) is compared with SLOT_MAIN
                if (i == StyleLock.SlotMain && itemId != 0 && item.Weapon != null && item.Weapon.HandToHand) hasH2HInMainSlot = true;
            }

            // an item that covers other slots clears their style items
            for (int i = 0; i < 10; i++)
            {
                var item = StyleLock.Lookup(items, c.StyleItems[i]);
                if (item == null) continue;
                for (int x = 0; x < 16; x++)
                    if ((item.RemoveSlots & 1 << x) != 0) c.StyleItems[x] = 0;
            }

            for (int i = 0; i < 10; i++)
            {
                switch (i)
                {
                    case StyleLock.SlotSub:
                        // an H2H main already set the sub look
                        if (!hasH2HInMainSlot) StyleLock.UpdateWeaponStyle(c, items, i, c.Equipped[i]);
                        break;
                    case StyleLock.SlotMain:
                    case StyleLock.SlotRanged:
                    case StyleLock.SlotAmmo:
                        StyleLock.UpdateWeaponStyle(c, items, i, c.Equipped[i]);
                        break;
                    case >= StyleLock.SlotHead and <= StyleLock.SlotFeet:
                        StyleLock.UpdateArmorStyle(c, items, i);
                        break;
                }
            }
            StyleLock.UpdateRemovedSlotsLook(c, items);

            if (failedItemIds.Count > 0) Push(s, SelfPackets.LockstyleError(failedItemIds));
            SaveCharAppearance(c);
            SendAppearance(s, c);
        }

        // GP_CLI_COMMAND_FRAGMENTS: value1 1 is the server message (xi.server.getServerMessage), 2 the fishing
        // ranking
        private void OnFragments(MapSession s, CharData c, BasicPacket p)
        {
            byte msgType = p.Buffer[6], language = p.Buffer[7];
            int timestamp = BitConverter.ToInt32(p.Buffer, 8), offset = BitConverter.ToInt32(p.Buffer, 16);
            if (msgType == 1)
            {
                // fragment retries from a slow zone-in
                if (c.ServmesLastOffset == offset) return;

                var message = ServerMessage(language);
                Push(s, SelfPackets.Servmes(message, language, timestamp, offset, UnixNow()));
                c.ServmesLastOffset = offset;

                // the last fragment resets it so /servmes works later
                int msgSize = message.Length + 1;
                if (offset + Math.Min(236, msgSize - offset) >= msgSize) c.ServmesLastOffset = null;
            }
            else if (msgType == 2)
            {
                OnFishRanking(s, c, p.Buffer[4], language, timestamp, offset, BitConverter.ToInt32(p.Buffer, 20));
            }
        }

        // the contest the Selbina zone loads at start (InitializeFishingContestSystem); loaded on first use here
        private FishingContest fishingContest_;
        public FishingContest Contest
        {
            get => fishingContest_ ??= LoadFishingContest();
            set => fishingContest_ = value;
        }

        // The fish ranking menu. chunk is a sub-type: 1 the first page of the table, 2 the next ones, 12/13 the self
        // block after a submission. Every reply has the self block (empty on chunk 2) and, for 1 and 2, up to five
        // leaderboard entries from offset.
        private void OnFishRanking(MapSession s, CharData c, byte chunk, byte language, int timestamp, int offset, int dataSize)
        {
            var contest = Contest;
            int maxFakes = contest.MaxFakeEntries;
            byte realEntries = contest.RankEntryCount;
            byte fakeEntries = (byte)(realEntries >= maxFakes ? 0 : maxFakes - realEntries);
            byte totalEntries = (byte)(realEntries + fakeEntries);

            var entries = new List<FishingContest.Entry>();
            var self = new FishingContest.Entry();
            if (chunk != 2)
            {
                self.Name = c.Name.Length > 15 ? c.Name.Substring(0, 15) : c.Name;
                self.ResultCount = totalEntries;
                var mine = contest.PlayerEntry(c.Name);
                if (mine != null)
                {
                    self.MJob = mine.MJob; self.SJob = mine.SJob; self.MLvl = mine.MLvl; self.SLvl = mine.SLvl;
                    self.Race = mine.Race; self.Allegiance = mine.Allegiance; self.FishRank = mine.FishRank;
                    self.Score = mine.Score; self.SubmitTime = mine.SubmitTime; self.ContestRank = mine.ContestRank;
                    self.Share = mine.Share; self.DatasetB = mine.DatasetB;
                }
                else
                {
                    self.MJob = c.MJob; self.SJob = c.SJob; self.MLvl = c.MLvl; self.SLvl = c.SLvl;
                    self.Race = c.MainLook.Race;
                    self.Allegiance = c.Allegiance;
                    self.FishRank = c.RealSkillRank[Skill.Fishing];
                    self.SubmitTime = LoginPackets.VanadielTimestamp(UnixNow());
                }
            }
            entries.Add(self);

            if (chunk == 1 || chunk == 2)
            {
                // entries.size() <= data_size / 36 compares as size_t, so a negative size never stops it
                long limit = dataSize / FishingContest.Entry.Size;
                int entryVal = 0;
                while (entries.Count < 6 && (limit < 0 || entries.Count <= limit))
                {
                    byte position = (byte)(offset / FishingContest.Entry.Size + entryVal++);
                    var entry = contest.RankEntry(position);
                    if (entry != null)
                    {
                        entry.ResultCount = totalEntries;
                        entries.Add(entry.Clone());
                    }
                    else entries.Add(new FishingContest.Entry());
                }
            }

            Push(s, SelfPackets.FishRanking(entries, language, timestamp, offset, totalEntries, chunk, UnixNow()));
        }

        // GP_CLI_COMMAND_UNITY_TOGGLE: leave the char's unity chat, join its leader's when turning it on
        private void OnUnityToggle(MapSession s, CharData c, BasicPacket p)
        {
            LeaveUnityChat(c);
            if (p.Buffer[4] == 1) JoinUnityChat(c, c.UnityLeader);
            SendLocalPlayerPackets(s, c);
        }

        // charutils::SendLocalPlayerPackets: the status and skills menus, recasts, merits and job points. Without the
        // stat port (no CharStats) there is nothing faithful to send.
        private void SendLocalPlayerPackets(MapSession s, CharData c)
        {
            var stats = loader_.Stats;
            if (stats == null)
            {
                log_($"zone: status menu for {c.Name} needs the stat tables");
                return;
            }
            Push(s, LoginPackets.GroupAttr(c));
            Push(s, SelfPackets.CliStatus(c, stats, UnityStatus(c)));
            Push(s, SelfPackets.CliStatus2(c));
            Push(s, AbilRecast(c));
            Push(s, SelfPackets.Merits(c, stats));
            Push(s, MonPage1(c));
            Push(s, SelfPackets.JobPoints(c));
        }

        // GP_CLI_COMMAND_REQCONQUEST: the conquest map and the colonization overview (the campaign one is commented
        // out in the C++), from the map's ConquestData cache (conquest::GetConquestData).
        private void OnReqConquest(MapSession s, CharData c, BasicPacket p)
        {
            var state = GetConquestData();
            Push(s, ConquestPackets.Conquest(state, ZoneRegions.Of(c.Zone), UnixNow() * 1000,
                GetPoints(c, ConquestPointsColumn(c)), GetPoints(c, "imperial_standing")));
            Push(s, ConquestPackets.Colonization(GetPoints(c, "bayld")));
        }

        // charutils::GetConquestPointsName; a char without a nation has none
        private static string ConquestPointsColumn(CharData c) => c.Nation switch
        {
            0 => "sandoria_cp",
            1 => "bastok_cp",
            2 => "windurst_cp",
            _ => null,
        };

        private SelfPackets.UnityStatus UnityStatus(CharData c) =>
            new SelfPackets.UnityStatus(c.UnityLeader > 0 ? UnityLeaderRank(c.UnityLeader) : (byte)0,
                GetPoints(c, "unity_accolades"), GetPoints(c, "current_accolades"), GetPoints(c, "prev_accolades"),
                unityOf_.ContainsKey(c));

        // charutils::GetPoints for a char_points column (the callers pass only known names)
        private int GetPoints(CharData c, string column)
        {
            if (column == null) return 0;
            var r = Exec($"SELECT {column} FROM char_points WHERE charid = ?", BoundValue.Of(c.Id));
            return r != null && r.Next() ? r.Get<int>(column) : 0;
        }

        // roeutils::UpdateUnityRankings' rank of a leader: by points per member last week, ties sharing a rank. The C++
        // recomputes it weekly; here it is read on demand, so it follows unity_system as it is now.
        private byte UnityLeaderRank(byte leader)
        {
            var r = Exec("SELECT leader, CASE WHEN members_prev = 0 THEN 0 ELSE FLOOR(points_prev/members_prev) END AS eval " +
                         "FROM unity_system ORDER BY eval DESC");
            byte currentRank = 1, rankGap = 0;
            long prevEval = 0;
            while (r != null && r.Next())
            {
                // FLOOR of a double is a double; the C++ reads it as an int32
                long eval = (long)r.Get<double>("eval");
                if (eval < prevEval)
                {
                    currentRank += rankGap;
                    rankGap = 1;
                }
                else
                {
                    rankGap++;
                }
                prevEval = eval;
                if (r.Get<int>("leader") == leader) return currentRank;
            }
            return 0;
        }

        // luautils::GetServerMessage: "" on a Lua error or without scripts
        private byte[] ServerMessage(byte language)
        {
            if (lua_ == null) return Array.Empty<byte>();
            int top = lua_.Top;
            try
            {
                lua_.GetPath("xi.server.getServerMessage");
                lua_.Push((double)language);
                lua_.Call(1, 1);
                return lua_.IsString(-1) ? lua_.ToBytes(-1) : Array.Empty<byte>();
            }
            catch (LuaException e)
            {
                log_("lua: xi.server.getServerMessage: " + e.Message);
                return Array.Empty<byte>();
            }
            finally
            {
                lua_.SetTop(top);
            }
        }
    }
}
