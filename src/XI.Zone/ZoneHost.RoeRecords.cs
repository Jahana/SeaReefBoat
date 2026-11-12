using System;
using System.Collections.Generic;
using XI.QuestContent.Roe;
using XI.Quests;
using XI.Script;

namespace XI.Zone
{
    // scripts/globals/roe.lua on C# (quest rework C4): the records' load, xi.roe.onRecordTrigger and completeRecord,
    // each message, bind and reward in the Lua's order. Runs only when the Lua file is absent (RoeOnLua). The item and
    // key item rewards go through the Lua npcUtil, as the Lua's did. A rate's fraction reaches a binding through
    // llround, so the amounts here round the same way (Whole)
    public sealed partial class ZoneHost
    {
        private const ushort MsgRoeComplete = 690, MsgRoeRepeatOrCancel = 691, MsgRoeReceiveSparks = 692,
            MsgRoeBonusItem = 693, MsgRoeUnableBonusItem = 695, MsgRoeYetToReceive = 696, MsgRoeFirstTimeSparks = 707,
            MsgRoeBonusItemPlural = 709, MsgRoeTimedClear = 710, MsgRoeReceivedAccolades = 741;
        private const int RoeAllForOne = 5, RoeTenObjectives = 4085;

        private readonly Dictionary<ushort, RoeRecord> roeRecords_ = new Dictionary<ushort, RoeRecord>();
        private readonly List<RoeRecord> testRoeRecords_ = new List<RoeRecord>();

        // the Lua file is absent: xi.roe.onRecordTrigger is this class's, and the tables load from RoeRecords
        private void InstallRoe()
        {
            int top = lua_.Top;
            lua_.GetGlobal("xi");
            int xi = lua_.Top;
            lua_.NewTable();
            lua_.PushFunction(OnRecordTriggerGlobal);
            lua_.SetField(-2, "onRecordTrigger");
            lua_.SetField(xi, "roe");
            lua_.SetTop(top);
            LoadRoeRecords();
        }

        // xi.roe.initialize: the table the C++ parse would fill, then the timed schedule, which clears Timed (the guard
        // of 0x10C stays dead, as the row says). Its condition is the Lua's: ENABLE_ROE is only tested for nil or false
        private void LoadRoeRecords()
        {
            var r = roe_;
            foreach (var h in r.Handlers) h.SetAll(false);
            r.Implemented.SetAll(false);
            r.Repeatable.SetAll(false);
            r.Retroactive.SetAll(false);
            r.Hidden.SetAll(false);
            r.Daily.SetAll(false);
            r.Weekly.SetAll(false);
            r.Unity.SetAll(false);
            r.Timed.SetAll(false);
            r.DailyIds.Clear();
            r.WeeklyIds.Clear();
            r.UnityIds.Clear();
            Array.Fill(r.NotifyThresholds, 1u);
            roeRecords_.Clear();
            foreach (var record in RoeRecords.All) AddRoeRecord(record);
            foreach (var record in testRoeRecords_) AddRoeRecord(record);

            if (!RoeTimedEnabled()) return;
            Array.Clear(r.TimedTable);
            r.Timed.SetAll(false);
            for (int day = 0; day < 7; day++)
                for (int block = 0; block < 6; block++)
                    r.TimedTable[day, block] = (ushort)RoeRecords.TimedSchedule[day][block];
        }

        private bool RoeTimedEnabled()
        {
            int top = lua_.Top;
            try
            {
                lua_.GetPath("xi.settings.main.ENABLE_ROE");
                if (!lua_.ToBoolean(-1)) return false;
                return RoeSetting("ENABLE_ROE_TIMED") > 0;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        private void AddRoeRecord(RoeRecord record)
        {
            var r = roe_;
            ushort id = (ushort)record.Id;
            roeRecords_[id] = record;
            r.Implemented[id] = true;
            if (record.Trigger > 0) r.Handlers[record.Trigger][id] = true;
            r.NotifyThresholds[id] = (uint)record.Notify;
            if ((record.Flags & RoeFlags.Daily) != 0) { r.Daily[id] = true; r.DailyIds.Add(id); }
            if ((record.Flags & RoeFlags.Weekly) != 0) { r.Weekly[id] = true; r.WeeklyIds.Add(id); }
            if ((record.Flags & RoeFlags.Unity) != 0) { r.Unity[id] = true; r.UnityIds.Add(id); }
            if ((record.Flags & RoeFlags.Timed) != 0) r.Timed[id] = true;
            if ((record.Flags & RoeFlags.Repeat) != 0) r.Repeatable[id] = true;
            if ((record.Flags & RoeFlags.Retro) != 0) r.Retroactive[id] = true;
            if ((record.Flags & RoeFlags.Hidden) != 0) r.Hidden[id] = true;
        }

        // test only: a record added to the table, as xi.roe.records[id] = { ... } and xi.roe.initialize() did
        public void AddRoeRecordForTest(RoeRecord record)
        {
            testRoeRecords_.Add(record);
            LoadRoeRecords();
        }

        // test only: the record of the id, null when none
        public RoeRecord RoeRecordForTest(int id) => roeRecords_.TryGetValue((ushort)id, out var r) ? r : null;

        // the coverage gate's hook: a label of the C# path, so none on the Lua path
        private void RoeReached(string label)
        {
            if (!RoeOnLua) HandlerCalled?.Invoke(ContainerKeys.RoeCoverage, label);
        }

        // ---- xi.roe.onRecordTrigger ---------------------------------------------------------------------------------

        // the Lua global: (player, recordID, params). params.progress or player:getEminenceProgress(recordID)
        private int OnRecordTriggerGlobal(LuaState l)
        {
            ushort id = (ushort)l.ToIntArg(2);
            if (Pc(l, "getEminenceProgress") is not CharData c) return 0;
            RoeRecordTrigger(c, id, RoeParamsOfTable(l, 3));
            return 0;
        }

        private static RoeParams RoeParamsOfTable(LuaState l, int index)
        {
            var p = new RoeParams();
            if (!l.IsTable(index)) return p;
            l.PushNil();
            while (l.Next(index))
            {
                if (l.IsString(-2))
                {
                    string key = l.ToStringValue(-2);
                    if (key == "progress" && l.IsNumber(-1)) p.Progress = l.ToIntegral(-1);
                    else if (l.IsNumber(-1)) p.Set(key, l.ToIntegral(-1));
                    else if (l.IsBoolean(-1) && l.ToBoolean(-1)) p.Set(key, 1);
                    else if (l.ToObject(-1) is MobEntity mob) p.Mob = new RoeMob(mob);
                }
                l.Pop(1);
            }
            return p;
        }

        // the engine's datagrams, as CallOnRecordTrigger hands them to Lua: every integer as a uint32
        private RoeParams RoeParamsOf(ushort id, uint progress, IReadOnlyList<(string Key, object Value)> payload)
        {
            var p = new RoeParams { Progress = progress };
            foreach (var (key, value) in payload)
            {
                if (value is MobEntity mob) p.Mob = new RoeMob(mob);
                else if (value is string or byte[]) p.SetText(key, value);
                else if (value is uint or int or byte or sbyte or ushort or short or long)
                    p.Set(key, unchecked((uint)Convert.ToInt64(value)));
                else log_($"roeutils::onRecordTrigger: Unhandled payload type for '{key}' with record #{id}.");
            }
            return p;
        }

        // onRecordTrigger, line by line
        private void RoeRecordTrigger(CharData c, ushort id, RoeParams p)
        {
            if (p.Progress == null && HasEminenceRecord(c, id)) p.Progress = GetEminenceRecordProgress(c, id);
            if (!roeRecords_.TryGetValue(id, out var entry) || p.Progress == null) return;

            bool isClaiming = p.Claim;
            bool awaitingClaim = p.Progress >= entry.Goal;
            if (awaitingClaim && !isClaiming)
            {
                MessageBasic(c, MsgRoeYetToReceive);
                return;
            }
            if (!isClaiming && !RoeChecks.Run(entry, new RoeFacts(this, c), p, RoeReached)) return;

            p.Progress += isClaiming ? 0 : entry.Increment;
            if (p.Progress >= entry.Goal)
            {
                if (!CompleteRecord(c, id) && !awaitingClaim) SetEminenceProgress(c, id, (uint)entry.Goal, 0);
            }
            else
            {
                SetEminenceProgress(c, id, (uint)p.Progress, (uint)entry.Goal);
            }
        }

        // completeRecord, line by line: false when the items could not be given
        private bool CompleteRecord(CharData c, ushort record)
        {
            var entry = roeRecords_[record];
            var rewards = entry.Reward;
            bool wasComplete = GetEminenceRecordCompletion(c, record);
            bool canRewardItem = rewards.Item != null &&
                                 (!wasComplete || RoeChecks.IsRepeatItemRewardException(rewards.Item));

            if (canRewardItem)
            {
                if (!RoeGiveItem(c, rewards.Item))
                {
                    RoeReached("reward:item:full");
                    MessageBasic(c, MsgRoeUnableBonusItem);
                    return false;
                }
                RoeReached(wasComplete ? "reward:item:repeat" : "reward:item:first");
            }

            MessageBasic(c, MsgRoeComplete, record);

            if (rewards.Sparks is int sparks)
            {
                double rate = RoeSetting("SPARKS_RATE");
                int cap = Whole(RoeSetting("CAP_CURRENCY_SPARKS"));
                if (GetEminenceRecordCompletion(c, record))
                {
                    RoeReached("reward:sparks:repeat");
                    AddPoints(c, "spark_of_eminence", Whole(sparks * 1 * rate), cap);
                    MessageBasic(c, MsgRoeReceiveSparks, (uint)Whole(sparks * rate),
                                 (uint)GetPointsChecked(c, "spark_of_eminence"));
                }
                else
                {
                    const int bonus = 3;
                    RoeReached("reward:sparks:first");
                    AddPoints(c, "spark_of_eminence", Whole(sparks * bonus * rate), cap);
                    MessageBasic(c, MsgRoeFirstTimeSparks, (uint)Whole(sparks * bonus * rate),
                                 (uint)GetPointsChecked(c, "spark_of_eminence"));
                }
            }

            if (rewards.Exp is int exp)
            {
                RoeReached("reward:exp");
                AddExperiencePoints(c, (uint)Whole(exp * RoeSetting("ROE_EXP_RATE")), true);
            }

            if (rewards.Capacity is int capacity)
            {
                RoeReached("reward:capacity");
                AddCapacityPoints(c, c, (uint)capacity);
            }

            // to keep retail's order the message comes here, though the item was given at the start
            if (canRewardItem)
            {
                var first = rewards.Item[0];
                int itemQty = first.Qty ?? 1;
                MessageBasic(c, itemQty > 1 ? MsgRoeBonusItemPlural : MsgRoeBonusItem, (uint)first.Id, (uint)itemQty);
            }

            if ((entry.Flags & RoeFlags.Repeat) != 0)
            {
                RoeReached("flag:repeat");
                bool timed = (entry.Flags & RoeFlags.Timed) != 0;
                if (timed) RoeReached("flag:timed");
                MessageBasic(c, timed ? MsgRoeTimedClear : MsgRoeRepeatOrCancel);
                SetEminenceCompleted(c, record, true);
            }
            else
            {
                SetEminenceCompleted(c, record, false);
            }

            if (c.UnityLeader > 0 && rewards.Accolades is int accolades)
            {
                double bonusAccoladeRate = 1.0;
                // no bonus for All for One
                if (record != RoeAllForOne) bonusAccoladeRate += (UnityLeaderRank(c.UnityLeader) - 1) * 0.05;
                RoeReached("reward:accolades");
                int accoladePayout = (int)Math.Floor(accolades * bonusAccoladeRate);
                AddPoints(c, "unity_accolades", accoladePayout, Whole(RoeSetting("CAP_CURRENCY_ACCOLADES")));
                MessageBasic(c, MsgRoeReceivedAccolades, (uint)accoladePayout,
                             (uint)GetPointsChecked(c, "unity_accolades"));
            }

            if (rewards.KeyItem is int keyItem)
            {
                RoeReached("reward:keyItem");
                RoeGiveKeyItem(c, keyItem);
            }

            // the workaround for hidden record 4085 (10 RoE objectives completed)
            if (!GetEminenceRecordCompletion(c, RoeTenObjectives) && GetNumEminenceCompleted(c) >= 10)
            {
                RoeReached("flag:hidden");
                SetEminenceCompleted(c, RoeTenObjectives, false);
            }
            return true;
        }

        // llround, as a binding reads a Lua number
        private static int Whole(double amount) => (int)LuaState.LlRound(amount);

        // player:messageBasic(msg, p0, p1) on the char itself
        private void MessageBasic(CharData c, ushort msg, uint p0 = 0, uint p1 = 0) =>
            PushTo(c, SelfPackets.BattleMessage(c, (int)p0, (int)p1, msg));

        // xi.settings.main[name] as the Lua number it is
        private double RoeSetting(string name)
        {
            int top = lua_.Top;
            try
            {
                lua_.GetPath("xi.settings.main." + name);
                return lua_.ToNumber(-1);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // npcUtil.giveItem(player, items, { silent = true })
        private bool RoeGiveItem(CharData c, IReadOnlyList<RoeItem> items)
        {
            int top = lua_.Top;
            try
            {
                lua_.GetPath("npcUtil.giveItem");
                lua_.PushObject(c);
                lua_.NewTable();
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].Qty is int qty)
                    {
                        lua_.NewTable();
                        lua_.Push((double)items[i].Id);
                        lua_.RawSetI(-2, 1);
                        lua_.Push((double)qty);
                        lua_.RawSetI(-2, 2);
                    }
                    else lua_.Push((double)items[i].Id);
                    lua_.RawSetI(-2, i + 1);
                }
                lua_.NewTable();
                lua_.Push(true);
                lua_.SetField(-2, "silent");
                lua_.Call(3, 1);
                return lua_.ToBoolean(-1);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // npcUtil.giveKeyItem(player, keyItem)
        private void RoeGiveKeyItem(CharData c, int keyItem)
        {
            int top = lua_.Top;
            try
            {
                lua_.GetPath("npcUtil.giveKeyItem");
                lua_.PushObject(c);
                lua_.Push((double)keyItem);
                lua_.Call(2, 1);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // ---- what the checks read -----------------------------------------------------------------------------------

        private sealed class RoeMob : IRoeMob
        {
            internal readonly MobEntity Mob;
            internal RoeMob(MobEntity mob) => Mob = mob;
            public uint Id => Mob.Id;
            public string Name => Mob.Name;
            public int Species => Mob.Species;
            public int Family => Mob.Family;
            public int Ecosystem => Mob.Ecosystem;
        }

        private sealed class RoeFacts : IRoeFacts
        {
            private readonly ZoneHost h_;
            private readonly CharData c_;

            internal RoeFacts(ZoneHost h, CharData c)
            {
                h_ = h;
                c_ = c;
            }

            public bool CheckKillCredit(IRoeMob mob) => h_.CheckKillCredit(c_, ((RoeMob)mob).Mob);
            public int ZoneId => ZoneOf(c_);
            public bool HasLevelSync => c_.StatusEffects.HasStatusEffect(EffectId.LevelSync);
            public int JobLevel(int job) => job < MaxJobType ? c_.JobLevels[(byte)job] : 0;
            public bool QuestCompleted(int log, int id) => h_.QuestStatus(c_, log, id) == 2;
            public bool MissionCompleted(int log, int id) => h_.CompletedMission(c_, log, id);
            public int UnityLeader => c_.UnityLeader;
            public int MainJob => c_.MJob;
            public int MainLevel => c_.MLvl;
            public int Sparks => h_.GetPointsChecked(c_, "spark_of_eminence");
            public int AverageItemLevel => CharStats.ItemLevelDifference(c_) + 99;
        }
    }
}
