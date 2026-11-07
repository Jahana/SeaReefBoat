using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using XI.Ipc;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // roeutils: Records of Eminence. xi.roe hands its record and timed tables to RoeParseRecords / RoeParseTimed while
    // the global scripts load; the char's eminencelog_t lives in CharData.EminenceLog (active u16[31] @0, progress
    // u32[31] @64, complete bits[4096] @188) with the activemap beside it.
    public partial class ZoneHost
    {
        private const int RoeRecordCount = 4096, RoeTriggerCount = 19; // ROE_NONE
        private const int RoeEventSynthSuccess = 4, RoeEventExpGain = 7, RoeEventLevelUp = 10, RoeEventQuestComplete = 11,
            RoeEventMissionComplete = 12, RoeEventUnityChat = 15;
        private const ushort MsgBasicRoeRecord = 697, MsgBasicRoeProgress = 698, MsgBasicRoeStart = 704, MsgBasicRoeTimed = 705,
            MsgBasicRoeUnable = 742, MsgBasicExperiencePointsGained = 8, MsgBasicLevelUp = 9, MsgBasicMeritPointGained = 50,
            MsgBasicLimitPointsGained = 371, MsgBasicCapacityPointsGained = 718, MsgBasicJobPointGained = 719,
            MsgBasicCapacityChain = 735;
        private const byte ChatNsSay = 13; // MESSAGE_NS_SAY
        private const int RoeCacheTimeMs = 15000;
        private const int MaxJobType = 24, MaxJobPoints = 500, MaxCapacityPoints = 30000, MaxLimitPointsPerMerit = 10000;

        // ROE_TRUST_ID: the trust each unity leader grants
        private static readonly ushort[] RoeTrustId = { 953, 1005, 954, 955, 1006, 1007, 956, 1008, 957, 980, 981 };

        // RoeSystemData
        private sealed class RoeSystemData
        {
            public readonly BitArray[] Handlers = new BitArray[RoeTriggerCount];
            public readonly BitArray Implemented = new BitArray(RoeRecordCount), Repeatable = new BitArray(RoeRecordCount),
                Retroactive = new BitArray(RoeRecordCount), Hidden = new BitArray(RoeRecordCount), Daily = new BitArray(RoeRecordCount),
                Weekly = new BitArray(RoeRecordCount), Unity = new BitArray(RoeRecordCount), Timed = new BitArray(RoeRecordCount);
            public readonly List<ushort> DailyIds = new List<ushort>(), WeeklyIds = new List<ushort>(), UnityIds = new List<ushort>();
            public readonly uint[] NotifyThresholds = new uint[RoeRecordCount];
            public readonly ushort[,] TimedTable = new ushort[7, 6];

            public RoeSystemData()
            {
                for (int i = 0; i < Handlers.Length; i++) Handlers[i] = new BitArray(RoeRecordCount);
                Array.Fill(NotifyThresholds, 1u);
            }
        }

        private readonly RoeSystemData roe_ = new RoeSystemData();
        private long nextRoeHourUnix_ = -1;
        private HashSet<string> charPointsColumns_;
        private readonly Dictionary<ushort, bool> spellExists_ = new Dictionary<ushort, bool>();

        // ---- the eminence log ---------------------------------------------------------------------------------------

        private static ushort RoeActive(CharData c, int slot) => BitConverter.ToUInt16(c.EminenceLog, 2 * slot);
        private static void SetRoeActive(CharData c, int slot, ushort id) => BitConverter.TryWriteBytes(c.EminenceLog.AsSpan(2 * slot, 2), id);
        private static uint RoeProgress(CharData c, int slot) => BitConverter.ToUInt32(c.EminenceLog, EminenceProgressOffset + 4 * slot);
        private static void SetRoeProgress(CharData c, int slot, uint v) => BitConverter.TryWriteBytes(c.EminenceLog.AsSpan(EminenceProgressOffset + 4 * slot, 4), v);

        private static void SetRoeCompleteBit(CharData c, int id, bool status)
        {
            if (status) c.EminenceLog[EminenceCompleteOffset + id / 8] |= (byte)(1 << id % 8);
            else c.EminenceLog[EminenceCompleteOffset + id / 8] &= (byte)~(1 << id % 8);
        }

        private static bool Test(BitArray bits, int id) => id >= 0 && id < bits.Length && bits[id];

        // ---- parsing ------------------------------------------------------------------------------------------------

        // roeutils::ParseRecords: Weekly / Unity / Timed are not reset, as the C++
        private void ParseRoeRecords(LuaState l)
        {
            var r = roe_;
            foreach (var h in r.Handlers) h.SetAll(false);
            r.Implemented.SetAll(false);
            r.Repeatable.SetAll(false);
            r.Retroactive.SetAll(false);
            r.Hidden.SetAll(false);
            r.Daily.SetAll(false);
            r.DailyIds.Clear();
            Array.Fill(r.NotifyThresholds, 1u);
            if (!l.IsTable(1)) return;

            l.PushNil();
            while (l.Next(1))
            {
                int t = l.Top;
                int id = l.IsNumber(t - 1) ? (ushort)l.ToIntegral(t - 1) : -1;
                if (id >= 0 && id < RoeRecordCount && l.IsTable(t)) ParseRoeRecord(l, t, (ushort)id);
                l.SetTop(t - 1);
            }
        }

        private void ParseRoeRecord(LuaState l, int t, ushort id)
        {
            var r = roe_;
            r.Implemented[id] = true;

            l.GetField(t, "trigger");
            if (!l.IsNil(-1))
            {
                uint trigger = (uint)l.ToIntegral(-1);
                if (trigger > 0 && trigger < RoeTriggerCount) r.Handlers[trigger][id] = true;
                else log_($"ROEUtils: Unknown Record trigger index {trigger} for record {id}.");
            }
            l.Pop(1);

            l.GetField(t, "notify");
            if (!l.IsNil(-1)) r.NotifyThresholds[id] = (uint)l.ToIntegral(-1);
            l.Pop(1);

            l.GetField(t, "flags");
            if (l.IsTable(-1))
            {
                int f = l.Top;
                l.PushNil();
                while (l.Next(f))
                {
                    l.PushValue(-2); // a copy, so the key lua_next walks is not converted
                    string flag = l.ToStringValue(-1);
                    l.Pop(1);
                    switch (flag)
                    {
                        case "daily": r.Daily[id] = true; r.DailyIds.Add(id); break;
                        case "weekly": r.Weekly[id] = true; r.WeeklyIds.Add(id); break;
                        case "unity": r.Unity[id] = true; r.UnityIds.Add(id); break;
                        case "timed": r.Timed[id] = true; break;
                        case "repeat": r.Repeatable[id] = true; break;
                        case "retro": r.Retroactive[id] = true; break;
                        case "hidden": r.Hidden[id] = true; break;
                        default: log_($"ROEUtils: Unknown flag {flag} for record #{id}."); break;
                    }
                    l.Pop(1);
                }
            }
            l.Pop(1);
        }

        // roeutils::ParseTimedSchedule: day (1-7, Sunday first) -> block (1-6, four JST hours each) -> record
        private void ParseRoeTimedSchedule(LuaState l)
        {
            roe_.Timed.SetAll(false);
            Array.Clear(roe_.TimedTable);
            if (!l.IsTable(1)) return;

            l.PushNil();
            while (l.Next(1))
            {
                int t = l.Top;
                int day = (byte)(l.ToIntegral(t - 1) - 1);
                if (l.IsTable(t))
                {
                    l.PushNil();
                    while (l.Next(t))
                    {
                        int block = (ushort)(l.ToIntegral(-2) - 1);
                        ushort id = (ushort)l.ToIntegral(-1);
                        if (day < 7 && block < 6) roe_.TimedTable[day, block] = id;
                        else log_($"ROEUtils: timed schedule slot {day + 1}/{block + 1} is out of range.");
                        l.Pop(1);
                    }
                }
                l.SetTop(t - 1);
            }
        }

        // ---- JST time -----------------------------------------------------------------------------------------------

        private const long JstOffset = 9 * 3600, Day = 86400;

        private static long JstNextMidnight(long unix) => FloorDiv(unix + JstOffset + Day - 1, Day) * Day - JstOffset;
        private static int JstHour(long unix) => (int)((unix + JstOffset) % Day / 3600);
        private static int JstWeekday(long unix) => (int)((FloorDiv(unix + JstOffset, Day) + 4) % 7); // Sunday 0
        private static long NextGameWeek(long unix) => JstNextMidnight(unix) + (6 - (JstWeekday(unix) + 6) % 7) * Day;
        private static long FloorDiv(long a, long b) => a >= 0 ? a / b : (a - b + 1) / b;

        // roeutils::GetActiveTimedRecord
        private ushort GetActiveTimedRecord()
        {
            long now = UnixNow();
            return roe_.TimedTable[JstWeekday(now), JstHour(now) / 4];
        }

        // ---- records ------------------------------------------------------------------------------------------------

        private void PushRoeLogs(CharData c)
        {
            for (int o = 0; o < 4; o++) PushTo(c, GameOkPackets.RoeLog(c, o));
        }

        // charutils::SaveEminenceData, with the char's eminence rows
        private void SaveEminenceData(CharData c)
        {
            if (!enableRoe_) return;
            PersistProgress(c, () => Exec("UPDATE chars SET eminence = ? WHERE charid = ? LIMIT 1", BoundValue.Of(c.EminenceLog), BoundValue.Of(c.Id)) != null,
                            p => p.SyncEminence(c.EminenceLog));
            c.EminenceLastWriteoutMs = NowMs();
        }

        private void SaveEminenceDataNice(CharData c)
        {
            if (c.EminenceLastWriteoutMs < NowMs() - RoeCacheTimeMs) SaveEminenceData(c);
        }

        private void SetEminenceRecordCompletion(CharData c, int id, bool status)
        {
            if (id >= RoeRecordCount) return;
            SetRoeCompleteBit(c, id, status);
            PushRoeLogs(c);
            SaveEminenceData(c);
        }

        private static bool GetEminenceRecordCompletion(CharData c, int id) =>
            id >= 0 && id < RoeRecordCount && (c.EminenceLog[EminenceCompleteOffset + id / 8] & 1 << id % 8) != 0;

        private int GetNumEminenceCompleted(CharData c)
        {
            int count = 0;
            for (int id = 0; id < RoeRecordCount; id++)
                if (GetEminenceRecordCompletion(c, id) && !roe_.Hidden[id]) count++;
            return count;
        }

        // 3000-3053: six daily blocks of nine records, by the Vana'diel day
        private bool IsUnitySharedRecordAvailable(int id) =>
            id < 3000 || id > 3053 || (id - 3000) / 9 == LoginPackets.VanadielTimestamp(UnixNow()) / 86400 % 6;

        // 3488-3707: eleven leaders of twenty records, four Vana'diel weeks of five
        private bool IsUnityLeaderRecordAvailable(int id, byte leader)
        {
            if (id < 3488 || id > 3707) return true;
            int offset = id - 3488;
            return offset / 20 + 1 == leader && offset % 20 / 5 == LoginPackets.VanadielTimestamp(UnixNow()) / 604800 % 4;
        }

        private bool AddEminenceRecord(CharData c, ushort id)
        {
            if (!Test(roe_.Implemented, id))
            {
                PushTo(c, ChatStd(c, ChatNsSay, CborRaw.Bytes($"The record #{id} is not implemented at this time."), "RoE System"));
                return false;
            }
            if (!IsUnitySharedRecordAvailable(id)) return false;
            if (!IsUnityLeaderRecordAvailable(id, c.UnityLeader)) return false;

            for (int i = 0; i < 30; i++)
            {
                if (RoeActive(c, i) == 0)
                {
                    SetRoeActive(c, i, id);
                    c.EminenceActive[id] = true;
                    PushTo(c, GameOkPackets.RoeActiveLog(c));
                    PushTo(c, SelfPackets.BattleMessage(c, id, 0, MsgBasicRoeStart));
                    SaveEminenceData(c);
                    return true;
                }
                if (RoeActive(c, i) == id) return false;
            }
            return false;
        }

        private bool DelEminenceRecord(CharData c, int id)
        {
            for (int i = 0; i < 30; i++)
            {
                if (RoeActive(c, i) != id) continue;
                SetRoeActive(c, i, 0);
                SetRoeProgress(c, i, 0);
                if (id < RoeRecordCount) c.EminenceActive[id] = false;
                // shift the rest up so the log keeps its order
                for (int j = i; j < 29 && RoeActive(c, j + 1) != 0; j++)
                {
                    ushort a = RoeActive(c, j);
                    uint p = RoeProgress(c, j);
                    SetRoeActive(c, j, RoeActive(c, j + 1));
                    SetRoeProgress(c, j, RoeProgress(c, j + 1));
                    SetRoeActive(c, j + 1, a);
                    SetRoeProgress(c, j + 1, p);
                }
                PushTo(c, GameOkPackets.RoeActiveLog(c));
                SaveEminenceData(c);
                return true;
            }
            return false;
        }

        private static bool HasEminenceRecord(CharData c, int id) => Test(c.EminenceActive, id);

        private static uint GetEminenceRecordProgress(CharData c, int id)
        {
            for (int i = 0; i < 31; i++)
                if (RoeActive(c, i) == id) return RoeProgress(c, i);
            return 0;
        }

        private bool SetEminenceRecordProgress(CharData c, int id, uint progress)
        {
            for (int i = 0; i < 31; i++)
            {
                if (RoeActive(c, i) != id) continue;
                if (RoeProgress(c, i) == progress) return true;
                SetRoeProgress(c, i, progress);
                PushTo(c, GameOkPackets.RoeActiveLog(c));
                SaveEminenceDataNice(c);
                return true;
            }
            return false;
        }

        // ---- triggers -----------------------------------------------------------------------------------------------

        // roeutils::event: xi.roe.onRecordTrigger for each active record the trigger handles
        private bool RoeEvent(int eventId, CharData c, IReadOnlyList<(string Key, object Value)> payload)
        {
            if (!enableRoe_ || c == null || eventId <= 0 || eventId >= RoeTriggerCount) return false;
            var handler = roe_.Handlers[eventId];
            bool any = false;
            for (int id = 0; id < RoeRecordCount && !any; id++) any = handler[id] && c.EminenceActive[id];
            if (!any) return false;
            for (int i = 0; i < 31; i++)
            {
                ushort id = RoeActive(c, i);
                if (Test(handler, id)) CallOnRecordTrigger(c, id, payload);
            }
            return true;
        }

        private void CallOnRecordTrigger(CharData c, ushort id, IReadOnlyList<(string Key, object Value)> payload)
        {
            if (lua_ == null) return;
            int top = lua_.Top;
            try
            {
                lua_.GetPath("xi.roe.onRecordTrigger");
                if (lua_.IsNil(-1))
                {
                    log_($"roeutils::onRecordTrigger: record {id}: xi.roe.onRecordTrigger is nil.");
                    return;
                }
                lua_.PushObject(c);
                lua_.Push(id);
                lua_.NewTable();
                lua_.Push(GetEminenceRecordProgress(c, id));
                lua_.SetField(-2, "progress");
                foreach (var (key, value) in payload)
                {
                    // RoeDatagramPayload: uint32 (every integer converts to it, as the C++ constructors do), the mob,
                    // or a string (a raw byte string for a chat message)
                    if (value is string text) lua_.PushString(text);
                    else if (value is byte[] bytes) lua_.PushBytes(bytes);
                    else if (value is MobEntity mob) lua_.PushObject(mob);
                    else if (value is uint or int or byte or sbyte or ushort or short or long)
                        lua_.Push(unchecked((uint)Convert.ToInt64(value)));
                    else
                    {
                        log_($"roeutils::onRecordTrigger: Unhandled payload type for '{key}' with record #{id}.");
                        continue;
                    }
                    lua_.SetField(-2, key);
                }
                lua_.Call(3, 0);
            }
            catch (LuaException e)
            {
                log_($"roeutils::onRecordTrigger: {e.Message}");
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        private static readonly (string, object)[] NoDatagrams = Array.Empty<(string, object)>();

        private void OnRecordTake(CharData c, ushort id)
        {
            if (Test(roe_.Retroactive, id)) CallOnRecordTrigger(c, id, NoDatagrams);
        }

        private bool OnRecordClaim(CharData c, int id)
        {
            if (!HasEminenceRecord(c, id)) return false;
            CallOnRecordTrigger(c, (ushort)id, new (string, object)[] { ("claim", 1u) });
            return true;
        }

        // ---- unity --------------------------------------------------------------------------------------------------

        // spell::GetSpell: the spell is in spell_list
        private bool SpellExists(ushort id)
        {
            if (spellExists_.TryGetValue(id, out bool known)) return known;
            var r = Exec("SELECT spellid FROM spell_list WHERE spellid = ? LIMIT 1", BoundValue.Of(id));
            return spellExists_[id] = r != null && r.Next();
        }

        // roeutils::UpdateUnityTrust: the leader's trust is known while either week's accolades reach 5000
        private void UpdateUnityTrust(CharData c, bool sendUpdate = false)
        {
            int cur = GetPoints(c, "prev_accolades") / 1000;
            int prev = GetPoints(c, "current_accolades") / 1000;
            ushort trust = c.UnityLeader > 0 && c.UnityLeader <= RoeTrustId.Length ? RoeTrustId[c.UnityLeader - 1] : (ushort)0;
            if (trust > 0)
            {
                bool has = HasBit(c.SpellList, trust);
                if (cur >= 5 || prev >= 5)
                {
                    if (SpellExists(trust) && !has)
                    {
                        c.SpellList[trust / 8] |= (byte)(1 << trust % 8);
                        Exec("INSERT IGNORE INTO char_spells VALUES (?, ?)", BoundValue.Of(c.Id), BoundValue.Of(trust));
                    }
                }
                else if (SpellExists(trust) && has)
                {
                    c.SpellList[trust / 8] &= (byte)~(1 << trust % 8);
                    Exec("DELETE FROM char_spells WHERE charid = ? AND spellid = ? LIMIT 1", BoundValue.Of(c.Id), BoundValue.Of(trust));
                }
            }
            if (sendUpdate) PushTo(c, LoginPackets.MagicData(c));
        }

        // GP_SERV_COMMAND_UNITY reads char_points as it is now
        private void RefreshUnityPoints(CharData c)
        {
            var r = Exec("SELECT spark_of_eminence, deeds, plaudits FROM char_points WHERE charid = ? LIMIT 1", BoundValue.Of(c.Id));
            c.HasPointsRow = r != null && r.Next();
            if (!c.HasPointsRow) return;
            c.Sparks = r.Get<int>("spark_of_eminence");
            c.Deeds = r.Get<int>("deeds");
            c.Plaudits = r.Get<int>("plaudits");
        }

        private BasicPacket UnityPacket(CharData c)
        {
            RefreshUnityPoints(c);
            return GameOkPackets.Unity(c, UnixNow());
        }

        // GP_CLI_COMMAND_UNITY_MENU / UNITY_QUEST: GP_SERV_COMMAND_UNITY, then charutils::SendUnityPackets
        private void OnUnityMenu(MapSession s, CharData c, BasicPacket p)
        {
            Push(s, UnityPacket(c));
            var list = new List<BasicPacket>();
            GameOkPackets.AddUnity(list, LoadUnity(), UnixNow());
            foreach (var packet in list) Push(s, packet);
        }

        // ---- char_points by name ------------------------------------------------------------------------------------

        // charutils::LoadCharPointsQueries: every char_points column but charid
        private bool IsCharPointsColumn(string name)
        {
            if (charPointsColumns_ == null)
            {
                charPointsColumns_ = new HashSet<string>();
                var r = Exec("SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'char_points'");
                while (r != null && r.Next()) charPointsColumns_.Add(r.Get<string>("COLUMN_NAME"));
                charPointsColumns_.Remove("charid");
            }
            // without the schema (a stub database) any identifier is taken
            return charPointsColumns_.Count > 0 ? charPointsColumns_.Contains(name) : name != "charid" && Regex.IsMatch(name, "^[a-z_0-9]+$");
        }

        private int GetPointsChecked(CharData c, string type)
        {
            if (IsCharPointsColumn(type)) return GetPoints(c, type);
            log_($"charutils::GetPoints: Invalid type {type} for {c.Name}");
            return 0;
        }

        // charutils::SetPoints
        private void SetPoints(CharData c, string type, int amount)
        {
            if (!IsCharPointsColumn(type))
            {
                log_($"charutils::SetPoints: Invalid type {type} for {c.Name}");
                return;
            }
            Exec($"UPDATE char_points SET {type} = ? WHERE charid = ?", BoundValue.Of(amount), BoundValue.Of(c.Id));
            if (type == "alter_ego_points") c.AlterEgoPoints = amount;
            if (type == "spark_of_eminence") PushTo(c, UnityPacket(c));
            // spending infamy can lower a Monipulator's rank
            if (type == "infamy" && c.Monstrosity != null) UpdateMonstrosityRank(c, amount);
        }

        // charutils::AddPoints: the sum clamped to [0, max(max, 0)]; unity accolades also count for the week, the
        // unity's standing and the trust
        private void AddPoints(CharData c, string type, int amount, int max = int.MaxValue)
        {
            int current = GetPointsChecked(c, type);
            SetPoints(c, type, (int)Math.Clamp((long)current + amount, 0, Math.Max(max, 0)));
            if (type == "unity_accolades" && amount > 0)
            {
                AddPoints(c, "current_accolades", amount);
                Exec("UPDATE unity_system SET points_current = points_current + ? WHERE leader = ?",
                     BoundValue.Of(amount / 1000f), BoundValue.Of(c.UnityLeader));
                UpdateUnityTrust(c, true);
                if (loader_.Stats != null) PushTo(c, SelfPackets.CliStatus(c, loader_.Stats, UnityStatus(c)));
            }
        }

        // ---- resets -------------------------------------------------------------------------------------------------

        // roeutils::onCharLoad: the activemap, then the resets the char missed while offline
        private void RoeOnCharLoad(CharData c)
        {
            if (!enableRoe_) return;
            for (int i = 0; i < 31; i++)
            {
                ushort id = RoeActive(c, i);
                if (id != 0 && id < RoeRecordCount) c.EminenceActive[id] = true;
            }
            // only chars with First Step Forward complete get timed and daily records
            if (!GetEminenceRecordCompletion(c, 1)) return;

            long now = UnixNow();
            long lastJstMidnight = JstNextMidnight(now) - Day;
            if (c.LastOnline < lastJstMidnight) ClearDailyRecords(c);

            long lastWeeklyReset = NextGameWeek(now) - 7 * Day;
            if (c.LastOnline < lastWeeklyReset)
            {
                ClearWeeklyRecords(c);
                if (c.LastOnline < lastWeeklyReset - 7 * Day) SetPoints(c, "prev_accolades", 0);
            }

            long lastTimedBlock = lastJstMidnight + JstHour(now) / 4 * 4 * 3600;
            if (c.LastOnline < lastTimedBlock || RoeActive(c, 30) != GetActiveTimedRecord())
            {
                c.NotifyTimedRecord = GetActiveTimedRecord() != 0;
                AddActiveTimedRecord(c);
            }
        }

        // 0x00C's one-time timed record message: the record, or -1
        private int TakeTimedRecordNotify(CharData c)
        {
            if (!enableRoe_ || !c.NotifyTimedRecord) return -1;
            c.NotifyTimedRecord = false;
            return GetActiveTimedRecord();
        }

        private void AddActiveTimedRecord(CharData c)
        {
            SetRoeProgress(c, 30, 0);
            c.EminenceActive.And(new BitArray(roe_.Timed).Not());
            ushort timed = GetActiveTimedRecord();
            SetRoeActive(c, 30, timed);
            c.EminenceActive[timed] = true;
            PushTo(c, GameOkPackets.RoeActiveLog(c));
            if (timed != 0)
            {
                PushTo(c, SelfPackets.BattleMessage(c, timed, 0, MsgBasicRoeTimed));
                SetEminenceRecordCompletion(c, timed, false);
            }
        }

        private void ClearDailyRecords(CharData c)
        {
            for (int i = 0; i < 30; i++)
                if (Test(roe_.Daily, RoeActive(c, i))) SetRoeProgress(c, i, 0);
            PushTo(c, GameOkPackets.RoeActiveLog(c));
            foreach (ushort id in roe_.DailyIds) SetRoeCompleteBit(c, id, false);
            // unity records lose their completion and keep the active progress
            foreach (ushort id in roe_.UnityIds) SetRoeCompleteBit(c, id, false);
            SaveEminenceData(c);
            PushRoeLogs(c);
        }

        private void ClearWeeklyRecords(CharData c)
        {
            for (int i = 0; i < 30; i++)
                if (Test(roe_.Weekly, RoeActive(c, i))) SetRoeProgress(c, i, 0);
            PushTo(c, GameOkPackets.RoeActiveLog(c));
            foreach (ushort id in roe_.WeeklyIds) SetRoeCompleteBit(c, id, false);
            SaveEminenceData(c);
            SetCharVar(c, "weekly_sparks_spent", 0, 0);
            SetCharVar(c, "weekly_accolades_spent", 0, 0);
            SetCharVar(c, "unity_changed", 0, 0);

            int currentAccolades = GetPoints(c, "current_accolades");
            SetPoints(c, "prev_accolades", currentAccolades);
            SetPoints(c, "current_accolades", 0);
            UpdateUnityTrust(c);
            PushRoeLogs(c);
        }

        // time_server's JST hourly tick: the weekly / daily / 4-hour cycles and the unity member counts
        private void TickRoe()
        {
            if (!enableRoe_) return;
            long now = UnixNow();
            if (nextRoeHourUnix_ < 0) nextRoeHourUnix_ = FloorDiv(now + 3599, 3600) * 3600;
            if (now < nextRoeHourUnix_) return;

            int hour = JstHour(now);
            if (hour == 0)
            {
                if (JstWeekday(now) == 1)
                {
                    foreach (var c in new List<CharData>(charSessions_.Keys)) ClearWeeklyRecords(c);
                    Exec("UPDATE unity_system SET members_prev = members_current, points_prev = points_current, members_current = 0, points_current = 0");
                }
                foreach (var c in new List<CharData>(charSessions_.Keys)) ClearDailyRecords(c);
            }
            // roeutils::UpdateUnityRankings; the ranks themselves are read on demand (UnityLeaderRank)
            Exec("UPDATE unity_system JOIN (SELECT unity_leader, COUNT(*) AS members FROM char_profile GROUP BY unity_leader) " +
                 "TMP ON unity_system.leader = unity_leader SET unity_system.members_current = members");
            if (hour % 4 == 0)
                foreach (var c in new List<CharData>(charSessions_.Keys))
                    if (GetEminenceRecordCompletion(c, 1)) AddActiveTimedRecord(c);

            nextRoeHourUnix_ = (FloorDiv(now, 3600) + 1) * 3600;
        }

        // ---- C2S ----------------------------------------------------------------------------------------------------

        private void RegisterRoeHandlers()
        {
            // GP_CLI_COMMAND_ROE_START: ObjectiveId u16 @4
            Register(0x10C, (s, c, p) =>
            {
                int id = U16(p, 4);
                return Pv(c)
                    .BlockedBy(BlockedState.InEvent)
                    .MustEqual(enableRoe_ ? 1 : 0, 1, "RoE is disabled")
                    .Range("ObjectiveId", id, 0, 4095)
                    .MustEqual(Test(roe_.Timed, id) ? 1 : 0, 0, "Cannot start a timed record")
                    .MustEqual(GetEminenceRecordCompletion(c, id) && !Test(roe_.Repeatable, id) ? 1 : 0, 0,
                               "Cannot start a completed record that is not repeatable");
            }, (s, c, p) =>
            {
                ushort id = U16(p, 4);
                if (AddEminenceRecord(c, id))
                {
                    Push(s, UnityPacket(c));
                    OnRecordTake(c, id);
                }
                else
                {
                    Push(s, SelfPackets.BattleMessage(c, id, 0, MsgBasicRoeUnable));
                }
            });
            // GP_CLI_COMMAND_ROE_REMOVE
            Register(0x10D, (s, c, p) => Pv(c)
                    .BlockedBy(BlockedState.InEvent)
                    .MustEqual(enableRoe_ ? 1 : 0, 1, "RoE is disabled")
                    .Range("ObjectiveId", U16(p, 4), 0, 4096),
                (s, c, p) =>
                {
                    DelEminenceRecord(c, U16(p, 4));
                    Push(s, UnityPacket(c));
                });
            // GP_CLI_COMMAND_ROE_CLAIM
            Register(0x10E, (s, c, p) => Pv(c)
                    .BlockedBy(BlockedState.InEvent)
                    .MustEqual(enableRoe_ ? 1 : 0, 1, "RoE is disabled")
                    .Range("ObjectiveId", U16(p, 4), 0, 4096),
                (s, c, p) =>
                {
                    OnRecordClaim(c, U16(p, 4));
                    Push(s, Currencies1(c));
                });
        }

        // ---- exp, limit and capacity points -------------------------------------------------------------------------

        // CMeritPoints::AddLimitPoints: true when the char earned a merit point
        private bool AddLimitPoints(CharData c, ushort points)
        {
            int MaxMeritPoints = loader_.MaxMeritPoints(c);
            ushort limits = (ushort)(c.LimitPoints + points);
            if (limits >= MaxLimitPointsPerMerit)
            {
                if (c.MeritPoints == MaxMeritPoints)
                {
                    c.LimitPoints = MaxLimitPointsPerMerit - 1;
                    return false;
                }
                ushort merits = (ushort)Math.Min(c.MeritPoints + limits / MaxLimitPointsPerMerit, MaxMeritPoints);
                c.LimitPoints = (ushort)(limits % MaxLimitPointsPerMerit);
                if (c.MeritPoints != merits)
                {
                    c.MeritPoints = merits;
                    return true;
                }
                return false;
            }
            c.LimitPoints = limits;
            return false;
        }

        // charutils::SaveCharJob
        private void SaveCharJob(CharData c, int job)
        {
            if (job == 0 || job >= MaxJobType)
            {
                log_($"Attempt to save Invalid Job with JOBTYPE {job}.");
                return;
            }
            Exec($"UPDATE char_jobs SET unlocked = ?, {CharLoader.JobColumns[job - 1]} = ? WHERE charid = ? LIMIT 1",
                 BoundValue.Of(c.UnlockedJobs), BoundValue.Of(c.JobLevels[job]), BoundValue.Of(c.Id));
        }

        // charutils::SaveCharExp
        private void SaveCharExp(CharData c, int job)
        {
            if (job == 0 || job >= MaxJobType)
            {
                log_($"Attempt to save Char XP with invalid JOBTYPE {job}.");
                return;
            }
            Exec($"UPDATE char_exp SET {CharLoader.JobColumns[job - 1]} = ?, merits = ?, limits = ? WHERE charid = ?",
                 BoundValue.Of(c.JobExp[job]), BoundValue.Of(c.MeritPoints), BoundValue.Of(c.LimitPoints), BoundValue.Of(c.Id));
        }

        // charutils::AddExperiencePoints as scripts call it (fromScripts: no rate, no region points, no infamy)
        private void AddExperiencePoints(CharData c, uint exp, bool allowLimitPoints) =>
            AddExperiencePoints(true, c, c, exp, MobTooWeak, false, allowLimitPoints);

        // charutils::AddExperiencePoints: a kill's EXP (the mob as PMob), a script's (the char) or a raise's (no rate, no
        // message, no limit mode by merit mode, a quiet level up); the EXPERIENCE_POINTS listener hears each gain. Not
        // ported: Abyssea's cruor (a TODO in the C++).
        private void AddExperiencePoints(bool fromScripts, CharData c, BattleEntity mob, uint exp, byte mobCheck, bool isExpChain,
                                         bool allowLimitPoints, bool expFromRaise = false)
        {
            var stats = loader_.Stats;
            if (c.Hp <= 0 && !expFromRaise || stats == null) return;
            if (!expFromRaise && !fromScripts) exp = (uint)(exp * (float)stats.Settings.ExpRate);
            int job = c.MJob;
            uint ExpNext(int level) => stats.ExpNextLevelFor(c, level);

            ushort currentExp = c.JobExp[job];
            bool onLimitMode = c.MeritMode && c.JobLevels[job] > 74 && !expFromRaise;
            if (c.JobLevels[job] > 74 && c.JobLevels[job] >= c.Genkai && c.JobExp[job] == ExpNext(c.JobLevels[job]) - 1) onLimitMode = true;
            // EXP scrolls and Monipulators cannot gain limit points
            if (!allowLimitPoints || c.Monstrosity != null) onLimitMode = false;

            if (!expFromRaise && exp > 0 && allowLimitPoints)
            {
                ushort gained = onLimitMode ? MsgBasicLimitPointsGained : MsgBasicExperiencePointsGained;
                if (mobCheck >= MobEvenMatch && isExpChain)
                {
                    if (c.ExpChainNumber != 0)
                        PushTo(c, SelfPackets.BattleMessage2(c, c, (int)exp, c.ExpChainNumber, onLimitMode ? MsgBasicLimitChain : MsgBasicExpChain));
                    else PushTo(c, SelfPackets.BattleMessage2(c, c, (int)exp, 0, gained));
                    c.ExpChainNumber++;
                }
                else PushTo(c, SelfPackets.BattleMessage2(c, c, (int)exp, 0, gained));
            }

            if (onLimitMode)
            {
                if (AddLimitPoints(c, (ushort)exp)) PushCharInRange(c, SelfPackets.BattleMessage2(c, mob, c.MeritPoints, 0, MsgBasicMeritPointGained), true);
            }
            else
            {
                c.JobExp[job] = (ushort)(c.JobExp[job] + exp);
            }

            if (!expFromRaise && !fromScripts)
            {
                byte region = ZoneRegions.Of(c.Zone);
                // under Signet a kill in the conquest regions earns conquest points and influence for the nation
                if (c.StatusEffects.HasStatusEffect(EffectSignet) && region >= RegionRonfaure && region <= RegionJeuno)
                {
                    AddConquestPoints(c, exp);
                    GainInfluencePoints(c, exp);
                }
                // under Sanction one in Aht Urhgan earns imperial standing
                if (c.StatusEffects.HasStatusEffect(EffectSanction) && region >= RegionWestAhtUrhgan && region <= RegionAlzadaal)
                {
                    AddPoints(c, "imperial_standing", (int)(exp * 0.1f));
                    PushTo(c, ConquestPackets.Conquest(GetConquestData(), region, UnixNow() * 1000,
                        GetPoints(c, ConquestPointsColumn(c)), GetPoints(c, "imperial_standing")));
                }
            }

            TriggerListener(c, "EXPERIENCE_POINTS", c, mob, exp);

            // only kills pay infamy and prestige
            if (!expFromRaise && !fromScripts)
            {
                if (c.Monstrosity != null) GainInfamy(c, (int)(exp / Monstrosity.ExpPerPoint));
                else AddPrestige(c, exp);
            }

            if (currentExp + exp >= ExpNext(c.JobLevels[job]) && !onLimitMode)
            {
                if (c.JobLevels[job] >= c.Genkai)
                {
                    c.JobExp[job] = (ushort)(ExpNext(c.JobLevels[job]) - 1);
                    // a capped designee cannot gain EXP, so the sync goes
                    if (c.Party?.SyncTarget == c) c.Party.SetSyncTarget("", MsgStdLevelSyncRemoveIneligibleExp);
                }
                else
                {
                    c.JobExp[job] = (ushort)(c.JobExp[job] - ExpNext(c.JobLevels[job]));
                    if (c.JobExp[job] >= ExpNext(c.JobLevels[job] + 1)) c.JobExp[job] = (ushort)(ExpNext(c.JobLevels[job] + 1) - 1);
                    c.JobLevels[job]++;
                    if (c.Monstrosity != null) MonstrosityHandleLevelUp(c);
                    LevelUp(c, stats, mob, expFromRaise);
                    return;
                }
            }

            SaveCharStats(c);
            SaveCharJob(c, job);
            SaveCharExp(c, job);
            PushTo(c, SelfPackets.CliStatus(c, stats, UnityStatus(c)));
            if (c.Monstrosity != null)
            {
                // retail refreshes the infamy shown in the Monstrosity menu on every gain
                PushTo(c, SelfPackets.CliStatus2(c));
                PushTo(c, SelfPackets.Merits(c, stats));
                PushTo(c, MonPage1(c));
                PushTo(c, SelfPackets.JobPoints(c));
            }
            else if (onLimitMode)
            {
                PushTo(c, SelfPackets.Merits(c, stats));
                PushTo(c, MonPage1(c));
                PushTo(c, GameOkPackets.Monstrosity2(c));
            }
            if (mob != c) RoeEvent(RoeEventExpGain, c, new (string, object)[] { ("exp", exp) });
        }

        // AddExperiencePoints' level up: the level, then (unless a level restriction holds it) the gift mods, skills,
        // stats, abilities, traits (BuildingCharTraitsTable), weapon skills and the automaton rebuilt; the party reloaded
        // (a sync designee's refreshed first), the full HP / MP, the saves, then the job packets with the extended job ones
        private void LevelUp(CharData c, CharStats stats, BattleEntity mob, bool expFromRaise = false)
        {
            int job = c.MJob;
            if (c.Monstrosity != null) RefreshMonstrositySpells(c);
            // a level restriction at or under the level keeps the level where it is
            if (c.LevelRestriction == 0 || c.LevelRestriction > c.MLvl)
            {
                SetMLevel(c, c.JobLevels[job]);
                SetSLevel(c, c.JobLevels[c.SJob], stats.Settings.SubjobRatio);
                RefreshGiftMods(c);
                stats.BuildSkillsTable(c);
                loader_.CalculateStats(c);
                loader_.BuildAbilityTable(c);
                BuildCharTraits(c);
                stats.BuildWeaponSkills(c);
                LoadAutomaton(c);
            }
            c.Latents.CheckLatentsJobLevel();
            // the party hears of the new level before the HP / MP are restored
            if (c.Party?.SyncTarget == c) c.Party.RefreshSync();
            c.Party?.ReloadParty();
            CharStats.UpdateHealth(c);

            if (!expFromRaise)
            {
                PushCharInRange(c, SelfPackets.BattleMessage2(c, mob, c.JobLevels[job], 0, MsgBasicLevelUp), true);
                c.Hp = c.MaxHp;
                c.Mp = c.MaxMp;
            }

            SaveCharStats(c);
            SaveCharJob(c, job);
            SaveCharExp(c, job);

            long now = UnixNow();
            PushTo(c, GameOkPackets.JobInfo(c, now));
            PushTo(c, CharStatus(c));
            PushTo(c, SelfPackets.CliStatus2(c));
            PushTo(c, AbilRecast(c));
            PushTo(c, LoginPackets.CommandData(c));
            PushTo(c, SelfPackets.Merits(c, stats));
            PushTo(c, MonPage1(c));
            PushTo(c, GameOkPackets.Monstrosity2(c));
            foreach (var p in ExtendedJobPackets(c)) PushTo(c, p);
            PushTo(c, CharSyncOf(c));
            PushTo(c, SelfPackets.CliStatus(c, stats, UnityStatus(c)));

            CallPlayerGlobal("xi.player.onPlayerLevelUp", c);
            RoeEvent(RoeEventLevelUp, c, NoDatagrams);
            c.UpdateMask |= CharStats.UpdateHp;
            if (SessionOf(c) is MapSession s) SendUpdateMask(s, c);
        }

        // charutils::AddCapacityPoints: EXP_RATE applies; a capacity chain (lv100+ mobs) names its number; only a mob
        // kill (the mob is not the char) counts for the EXP gain records
        private void AddCapacityPoints(CharData c, BattleEntity mob, uint capacityPoints, int levelDiff = 0, bool isCapacityChain = false)
        {
            var stats = loader_.Stats;
            if (c.Hp <= 0) return;
            capacityPoints = (uint)(capacityPoints * (float)(stats?.Settings.ExpRate ?? 1.0));
            if (capacityPoints == 0) return;

            if (levelDiff >= 1 && isCapacityChain)
            {
                PushTo(c, c.CapacityChainNumber != 0
                    ? SelfPackets.BattleMessage2(c, c, (int)capacityPoints, c.CapacityChainNumber, MsgBasicCapacityChain)
                    : SelfPackets.BattleMessage2(c, c, (int)capacityPoints, 0, MsgBasicCapacityPointsGained));
                c.CapacityChainNumber++;
            }
            else
            {
                PushTo(c, SelfPackets.BattleMessage2(c, c, (int)capacityPoints, 0, MsgBasicCapacityPointsGained));
            }
            if (AddJobCapacityPoints(c, (ushort)capacityPoints, (float)(stats?.Settings.CapacityRate ?? 1.0)))
                PushCharInRange(c, SelfPackets.BattleMessage2(c, mob, c.JobPoints[c.MJob].Current, 0, MsgBasicJobPointGained), true);
            PushTo(c, SelfPackets.JobPoints(c));
            if (mob != c) RoeEvent(RoeEventExpGain, c, new (string, object)[] { ("capacity", capacityPoints) });
        }

        // charutils::DistributeCapacityPoints: each alliance member alive in the zone, at 99 with the Job Breaker,
        // earns points for a mob above 99 (ffxiclopedia's curve), more in a chain of kills 30 seconds apart. Not
        // ported (TODO in C++ too): Abyssea / Reives, the Gates areas' reduced party penalty, the shorter timer after
        // chain 30.
        private void DistributeCapacityPoints(CharData pc, MobEntity m, long now)
        {
            byte mobLevel = m.MLvl;
            foreach (var member in new List<CharData>(ForAlliance(pc)))
            {
                if (member.Hp <= 0 || !SameArea(member, pc)) continue;
                // no Monstrosity is ported, so only the Job Breaker and level 99 checks apply
                if (!member.HasKeyItem(SelfPackets.KeyItemJobBreaker) || member.MLvl < 99) continue;

                bool chainActive = false;
                int levelDiff = mobLevel - 99;
                // capacity points (and chains) only come from mobs above 99
                if (mobLevel <= 99) continue;
                float capacityPoints = (float)(0.0089 * Math.Pow(levelDiff, 3) + 0.0533 * Math.Pow(levelDiff, 2) + 3.7439 * levelDiff + 89.7);
                // capacityChain{} starts at the clock's epoch, so the C++'s time_point::min() case never matches
                if (member.CapacityChainTimeMs > now)
                {
                    chainActive = true;
                    // the C++'s assumption: chain 0 has no bonus, chains 10+ cap at 1.5
                    float chainModifier = (float)Math.Min(1 + 0.05 * member.CapacityChainNumber, 1.5);
                    capacityPoints *= chainModifier;
                }
                else
                {
                    member.CapacityChainTimeMs = now + 30000;
                    member.CapacityChainNumber = 1;
                }
                if (chainActive) member.CapacityChainTimeMs = now + 30000;

                ushort points = AddCapacityBonus(member, (ushort)capacityPoints);
                AddCapacityPoints(member, m, points, levelDiff, chainActive);
            }
        }

        // the RoE records whose completion adds capacity (roeCapacityBonusRecords: the nations', Zilart, CoP, WotG,
        // ToAU and SoA missions, 10% each) and the RoV key items (Rhapsody in Fuchsia / Puce / Ochre, 30% each)
        private static readonly int[] RoeCapacityBonusRecords = { 1332, 1352, 1372, 1392, 1400, 1409, 1415, 1430 };
        private static readonly int[] CapacityBonusKeyItems = { 2890, 2891, 2892 };
        private const ushort EffectCommitment = 579;
        private const byte RegionAbyssea = 41;

        // charutils::AddCapacityBonus: Commitment (its power percent, up to what is left of its cap, which it uses up),
        // CAPACITY_BONUS, the unity ranking, the RoE records and the RoV key items
        private ushort AddCapacityBonus(CharData c, ushort capacityPoints)
        {
            float rawBonus = 0;
            if (c.StatusEffects.GetStatusEffect(EffectCommitment) is { } commitment && ZoneRegions.Of(c.Zone) != RegionAbyssea)
            {
                short percentage = (short)commitment.Power;
                short cap = (short)commitment.SubPower;
                rawBonus += Math.Clamp(capacityPoints * percentage / 100, 0, (int)cap);
                cap = (short)(cap - rawBonus);
                commitment.SubPower = (ushort)cap;
                if (cap <= 0) c.StatusEffects.DelStatusEffect(EffectCommitment);
            }
            rawBonus += c.GetMod(Mod.CAPACITY_BONUS);
            byte unity = c.UnityLeader;
            if (unity >= 1 && unity <= 11) rawBonus += 2 * (UnityLeaderRank(unity) - 1);
            foreach (int record in RoeCapacityBonusRecords)
                if (GetEminenceRecordCompletion(c, record)) rawBonus += 10;
            foreach (int keyItem in CapacityBonusKeyItems)
                if (c.HasKeyItem(keyItem)) rawBonus += 30;
            return (ushort)(capacityPoints * (1.0f + rawBonus / 100));
        }

        // CJobPoints::AddCapacityPoints: true when the char earned a job point
        private bool AddJobCapacityPoints(CharData c, ushort amount, float capacityRate)
        {
            int job = c.MJob;
            uint adjusted = (uint)(c.JobPoints[job].Capacity + amount * capacityRate);
            ushort currentJp = c.JobPoints[job].Current;
            if (adjusted >= MaxCapacityPoints)
            {
                if (currentJp == MaxJobPoints)
                {
                    SetCapacityPoints(c, MaxCapacityPoints - 1);
                    return false;
                }
                ushort jp = (ushort)Math.Min(currentJp + adjusted / MaxCapacityPoints, MaxJobPoints);
                SetCapacityPoints(c, (ushort)(adjusted % MaxCapacityPoints));
                if (currentJp != jp)
                {
                    SetJobPoints(c, jp);
                    return true;
                }
            }
            else
            {
                SetCapacityPoints(c, (ushort)adjusted);
            }
            return false;
        }

        private void SetCapacityPoints(CharData c, ushort amount)
        {
            int job = c.MJob;
            amount = (ushort)Math.Clamp((short)amount, (short)0, (short)MaxCapacityPoints);
            c.JobPoints[job].Capacity = amount;
            Exec("INSERT INTO char_job_points SET charid=?, jobid=?, capacity_points=? ON DUPLICATE KEY UPDATE capacity_points=?",
                 BoundValue.Of(c.Id), BoundValue.Of((byte)job), BoundValue.Of(amount), BoundValue.Of(amount));
        }

        private void SetJobPoints(CharData c, ushort amount)
        {
            int job = c.MJob;
            amount = (ushort)Math.Clamp((short)amount, (short)0, (short)MaxJobPoints);
            Exec("INSERT INTO char_job_points SET charid=?, jobid=?, job_points=? ON DUPLICATE KEY UPDATE job_points=?",
                 BoundValue.Of(c.Id), BoundValue.Of((byte)job), BoundValue.Of(amount), BoundValue.Of(amount));
            loader_.LoadJobPoints(c);
        }

        // ---- bindings -----------------------------------------------------------------------------------------------

        private void RegisterRoeBindings(Dictionary<string, XI.Script.LuaFunction> m)
        {
            m["messageBasic"] = l =>
            {
                ushort msg = (ushort)l.ToIntArg(2);
                uint p0 = l.IsNil(3) ? 0 : OptU32(l, 3), p1 = l.IsNil(4) ? 0 : OptU32(l, 4);
                var self = Entity(l);
                var target = l.IsNil(5) ? self : l.ToObject(5) ?? self;
                var (sid, starg, _, _) = Speaker(self);
                var (tid, ttarg, _, _) = Speaker(target);
                var packet = SelfPackets.BattleMessage(sid, starg, tid, ttarg, (int)p0, (int)p1, msg);
                if (self is CharData c) PushTo(c, packet);
                else PushInRange((NpcEntity)self, packet);
                return 0;
            };

            m["setEminenceCompleted"] = l =>
            {
                ushort id = (ushort)l.ToIntArg(2);
                if (Pc(l, "setEminenceCompleted") is not CharData c) return 0;
                bool repeat = !l.IsNil(3) && l.ToBoolean(3);
                bool status = l.IsNil(4) || l.ToBoolean(4);
                if (repeat) SetEminenceRecordProgress(c, id, 0);
                else DelEminenceRecord(c, id);
                SetEminenceRecordCompletion(c, id, status);
                return 0;
            };
            m["getNumEminenceCompleted"] = l => Ret(l, Pc(l, "getNumEminenceCompleted") is CharData c ? GetNumEminenceCompleted(c) : 0);
            m["setEminenceProgress"] = l =>
            {
                ushort id = (ushort)l.ToIntArg(2);
                uint progress = l.ToUIntArg(3);
                if (Pc(l, "setEminenceProgress") is not CharData c) return Ret(l, false);
                uint total = l.IsNumber(4) ? (uint)l.ToIntegral(4) : 0;

                // progress messages only as a step of the record's threshold is passed
                bool notify = true;
                uint threshold = id < RoeRecordCount ? roe_.NotifyThresholds[id] : 1;
                if (threshold > 1) notify = progress / threshold > GetEminenceRecordProgress(c, id) / threshold;

                bool result = SetEminenceRecordProgress(c, id, progress);
                if (total != 0 && notify)
                {
                    PushTo(c, SelfPackets.BattleMessage(c, id, 0, MsgBasicRoeRecord));
                    PushTo(c, SelfPackets.BattleMessage(c, (int)progress, (int)total, MsgBasicRoeProgress));
                }
                return Ret(l, result);
            };
            m["getEminenceProgress"] = l =>
            {
                ushort id = (ushort)l.ToIntArg(2);
                if (Pc(l, "getEminenceProgress") is CharData c && HasEminenceRecord(c, id)) return Ret(l, GetEminenceRecordProgress(c, id));
                l.PushNil();
                return 1;
            };
            m["hasEminenceRecord"] = l =>
            {
                ushort id = (ushort)l.ToIntArg(2);
                return Ret(l, Pc(l, "hasEminenceRecord") is CharData c && HasEminenceRecord(c, id));
            };
            m["triggerRoeEvent"] = l =>
            {
                int eventId = (byte)l.ToIntArg(2);
                if (Entity(l) is not CharData c) return 0;
                var data = new List<(string, object)>();
                if (l.IsTable(3))
                {
                    l.PushNil();
                    while (l.Next(3))
                    {
                        if (l.IsString(-2))
                        {
                            if (l.IsNumber(-1)) data.Add((l.ToStringValue(-2), (uint)l.ToIntegral(-1)));
                            else if (l.IsString(-1)) data.Add((l.ToStringValue(-2), l.ToStringValue(-1)));
                        }
                        l.Pop(1);
                    }
                }
                RoeEvent(eventId, c, data);
                return 0;
            };

            m["getCurrency"] = l =>
            {
                string type = l.ToStringArg(2);
                return Ret(l, Pc(l, "getCurrency") is CharData c ? GetPointsChecked(c, type) : 0);
            };
            m["addCurrency"] = l =>
            {
                string type = l.ToStringArg(2);
                int amount = l.ToIntArg(3);
                if (Pc(l, "addCurrency") is not CharData c) return 0;
                int max = l.IsNumber(4) ? (int)l.ToIntegral(4) : int.MaxValue;
                AddPoints(c, type, amount, max);
                return 0;
            };
            m["getUnityLeader"] = l => Ret(l, Pc(l, "getUnityLeader") is CharData c ? c.UnityLeader : 0);
            m["getUnityRank"] = l =>
            {
                if (Pc(l, "getUnityRank") is CharData c)
                {
                    int unity = l.IsNil(2) ? c.UnityLeader : (byte)l.ToIntArg(2);
                    if (unity >= 1 && unity <= 11) return Ret(l, UnityLeaderRank((byte)unity));
                }
                l.PushNil();
                return 1;
            };

            m["getJobLevel"] = l =>
            {
                int job = (byte)l.ToIntArg(2);
                if (Entity(l) is CharData c && job < MaxJobType) return Ret(l, c.JobLevels[job]);
                log_("CLuaBaseEntity::getJobLevel() - Non-PC or Invalid JobID passed to function.");
                return Ret(l, 0);
            };
            m["getMainJob"] = l =>
            {
                return Ret(l, Battle(l) is { } b ? b.MJob : 0);
            };
            m["addExp"] = l =>
            {
                uint exp = l.ToUIntArg(2);
                if (Pc(l, "addExp") is not CharData c) return 0;
                bool allowLimitPoints = l.Type(3) != 1 || l.ToBoolean(3); // only a boolean counts
                AddExperiencePoints(c, exp, allowLimitPoints);
                return 0;
            };
            m["delExp"] = l =>
            {
                uint exp = l.ToUIntArg(2);
                if (Pc(l, "delExp") is CharData c) DelExperiencePoints(c, 0, (ushort)Math.Min(exp, 65535u));
                return 0;
            };
            m["addCapacityPoints"] = l =>
            {
                uint capacity = l.ToUIntArg(2);
                if (Entity(l) is CharData c) AddCapacityPoints(c, c, capacity);
                return 0;
            };
        }
    }
}
