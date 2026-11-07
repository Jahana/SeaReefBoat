using System;
using System.Collections.Generic;
using XI.Persistence;

namespace XI.Zone
{
    // RECASTTYPE
    public enum RecastType : byte { Item = 0, Magic = 1, Ability = 2, Loot = 3 }

    // Recast_t; the times are in milliseconds of the zone clock (timer::now)
    public sealed class Recast
    {
        // the Recast ids recast_container.h / enums/recast.h name
        public const ushort Special = 0, Sic = 102, BloodPactRage = 173, BloodPactWard = 174, Strategems = 231, Special2 = 254, Mount = 256;

        public ushort Id;
        public long TimeStampMs;
        public long RecastTimeMs;
        public long ChargeTimeMs;
        public byte MaxCharges;
    }

    // CRecastContainer: the magic and ability recasts of a battle entity. The loot recasts (GetLootRecast /
    // AddLootRecast / HasLootRecast) are CharData.LootRecastUntilMs, which ZoneHost.Loot.cs's special drops use.
    public class RecastContainer
    {
        protected readonly Func<long> nowMs_;
        protected readonly Action<string> log_;
        protected readonly List<Recast> magic_ = new List<Recast>();
        protected readonly List<Recast> ability_ = new List<Recast>();

        public RecastContainer(Func<long> nowMs, Action<string> log = null)
        {
            nowMs_ = nowMs;
            log_ = log ?? (_ => { });
        }

        // CRecastContainer::GetRecastList
        public virtual List<Recast> GetRecastList(RecastType type)
        {
            switch (type)
            {
                case RecastType.Magic: return magic_;
                case RecastType.Ability: return ability_;
            }
            // Unhandled Scenario
            log_("Invalid RECASTTYPE received, returning nullptr.");
            return null;
        }

        // CRecastContainer::GetRecast
        public Recast GetRecast(RecastType type, ushort id)
        {
            var list = GetRecastList(type);
            return list?.Find(r => r.Id == id);
        }

        // CRecastContainer::Add
        public virtual void Add(RecastType type, ushort id, long durationMs, long chargeTimeMs = 0, byte maxCharges = 0) =>
            Load(type, id, durationMs, chargeTimeMs, maxCharges);

        // CRecastContainer::Load
        public Recast Load(RecastType type, ushort id, long durationMs, long chargeTimeMs = 0, byte maxCharges = 0)
        {
            var recast = GetRecast(type, id);
            if (recast == null)
            {
                var list = GetRecastList(type);
                if (list == null) return null;
                recast = new Recast { Id = id, TimeStampMs = nowMs_(), RecastTimeMs = durationMs, ChargeTimeMs = chargeTimeMs, MaxCharges = maxCharges };
                list.Add(recast);
                return recast;
            }
            if (chargeTimeMs != 0) recast.ChargeTimeMs = chargeTimeMs;
            if (maxCharges != 0) recast.MaxCharges = maxCharges;
            if (recast.ChargeTimeMs == 0)
            {
                recast.TimeStampMs = nowMs_();
                recast.RecastTimeMs = durationMs;
            }
            else
            {
                if (recast.RecastTimeMs == 0)
                {
                    recast.TimeStampMs = nowMs_();
                }
                else if (recast.RecastTimeMs + durationMs > recast.ChargeTimeMs * recast.MaxCharges)
                {
                    long diff = recast.RecastTimeMs + durationMs - recast.ChargeTimeMs * recast.MaxCharges;
                    recast.TimeStampMs += diff;
                    durationMs -= diff;
                }
                recast.RecastTimeMs += durationMs;
            }
            return recast;
        }

        // CRecastContainer::Del(type): the ability recasts stay listed with no time left, the others go
        public virtual void Del(RecastType type)
        {
            var list = GetRecastList(type);
            if (list == null) return;
            if (type == RecastType.Ability)
                foreach (var recast in list) recast.RecastTimeMs = 0;
            else
                list.Clear();
        }

        // CRecastContainer::Del(type, id)
        public virtual void Del(RecastType type, ushort id)
        {
            var list = GetRecastList(type);
            if (list == null) return;
            if (type == RecastType.Ability)
            {
                if (GetRecast(RecastType.Ability, id) is { } recast) recast.RecastTimeMs = 0;
            }
            else
            {
                list.RemoveAll(r => r.Id == id);
            }
        }

        // CRecastContainer::DeleteByIndex: an ability recast stays listed with no time left, the others go
        public virtual void DeleteByIndex(RecastType type, int index)
        {
            var list = GetRecastList(type);
            if (type == RecastType.Ability) list[index].RecastTimeMs = 0;
            else list.RemoveAt(index);
        }

        // CRecastContainer::Has
        public bool Has(RecastType type, ushort id) => GetRecastList(type)?.Exists(r => r.Id == id) ?? false;

        // CRecastContainer::HasRecast: the recast is running, or (charges) fewer than the requested charges are left
        public bool HasRecast(RecastType type, ushort id, long recastMs)
        {
            var list = GetRecastList(type);
            if (list == null) return false;
            foreach (var i in list)
            {
                if (i.Id == id && i.RecastTimeMs > 0)
                {
                    if (i.ChargeTimeMs == 0) return true;

                    // a request to use more charges than the maximum only applies to abilities who share normal recasts with charges (ie. sic)
                    // "recast" 1-4 = sic/ready
                    // "recast" 1 = quickdraw, stratagems
                    long crypticRecastSecondsAsType = recastMs / 1000;
                    if (crypticRecastSecondsAsType > i.MaxCharges) return true;
                    long currentRecast = i.TimeStampMs - nowMs_() + i.RecastTimeMs;
                    byte availableCharges = (byte)(currentRecast / i.ChargeTimeMs);
                    int charges = i.MaxCharges - availableCharges - 1;
                    if (charges < crypticRecastSecondsAsType) return true;
                }
            }
            return false;
        }

        // CRecastContainer::Check: an expired magic recast goes, an expired ability recast stays with no time left
        public virtual void Check()
        {
            foreach (var type in new[] { RecastType.Magic, RecastType.Ability })
            {
                var list = GetRecastList(type);
                for (int i = 0; i < list.Count; ++i)
                {
                    var recast = list[i];
                    if (nowMs_() >= recast.TimeStampMs + recast.RecastTimeMs)
                    {
                        if (type == RecastType.Magic) list.RemoveAt(i--);
                        else recast.RecastTimeMs = 0;
                    }
                }
            }
        }

        // CRecastContainer::ResetAbilities: every ability recast but the two-hours'
        public virtual void ResetAbilities()
        {
            foreach (var recast in GetRecastList(RecastType.Ability))
                if (recast.Id != Recast.Special && recast.Id != Recast.Special2) recast.RecastTimeMs = 0;
        }
    }

    // CCharRecastContainer: adds the item and loot lists, and keeps the ability recasts in char_recast (time is the
    // unix second the recast started, recast its length in seconds).
    public sealed class CharRecastContainer : RecastContainer
    {
        private readonly Func<string, BoundValue[], ResultSet> exec_;
        private readonly uint charId_;
        private readonly Func<long> unixNow_;
        private readonly Action<ushort> itemReady_;
        private readonly List<Recast> item_ = new List<Recast>();
        private readonly List<Recast> loot_ = new List<Recast>();

        // itemReady gets an expired item recast's id (slot << 8 | container) to push ITEM_ATTR and ITEM_SAME
        public CharRecastContainer(uint charId, Func<string, BoundValue[], ResultSet> exec, Func<long> nowMs, Func<long> unixNow,
            Action<ushort> itemReady = null, Action<string> log = null) : base(nowMs, log)
        {
            charId_ = charId;
            exec_ = exec;
            unixNow_ = unixNow;
            itemReady_ = itemReady ?? (_ => { });
        }

        // the unix second of a zone clock time stamp (timer::to_utc)
        private long ToUnix(long timeStampMs) => unixNow_() - (nowMs_() - timeStampMs) / 1000;

        // CCharRecastContainer::Add
        public override void Add(RecastType type, ushort id, long durationMs, long chargeTimeMs = 0, byte maxCharges = 0)
        {
            var recast = Load(type, id, durationMs, chargeTimeMs, maxCharges);
            if (type == RecastType.Ability && recast != null)
            {
                exec_("INSERT INTO char_recast SET charid = ?, id = ?, time = ?, recast = ? " +
                      "ON DUPLICATE KEY UPDATE time = VALUES(time), recast = VALUES(recast)",
                      new[] { BoundValue.Of(charId_), BoundValue.Of(recast.Id), BoundValue.Of((uint)ToUnix(recast.TimeStampMs)),
                              BoundValue.Of((uint)(recast.RecastTimeMs / 1000)) });
            }
        }

        // CCharRecastContainer::Del(type)
        public override void Del(RecastType type)
        {
            base.Del(type);
            if (type == RecastType.Ability)
                exec_("DELETE FROM char_recast WHERE charid = ?", new[] { BoundValue.Of(charId_) });
        }

        // CCharRecastContainer::Del(type, id)
        public override void Del(RecastType type, ushort id)
        {
            base.Del(type, id);
            exec_("DELETE FROM char_recast WHERE charid = ? AND id = ?", new[] { BoundValue.Of(charId_), BoundValue.Of(id) });
        }

        // CCharRecastContainer::DeleteByIndex
        public override void DeleteByIndex(RecastType type, int index)
        {
            var list = GetRecastList(type);
            if (type == RecastType.Ability)
            {
                list[index].RecastTimeMs = 0;
                exec_("DELETE FROM char_recast WHERE charid = ? AND id = ?", new[] { BoundValue.Of(charId_), BoundValue.Of(list[index].Id) });
            }
            else list.RemoveAt(index);
        }

        // CCharRecastContainer::ResetAbilities
        public override void ResetAbilities()
        {
            base.ResetAbilities();
            exec_("DELETE FROM char_recast WHERE charid = ? AND id != 0", new[] { BoundValue.Of(charId_) });
        }

        // CCharRecastContainer::ChangeJob: only the two-hour recasts survive a job change
        public void ChangeJob()
        {
            GetRecastList(RecastType.Ability).RemoveAll(r => r.Id != Recast.Special && r.Id != Recast.Special2);
            exec_("DELETE FROM char_recast WHERE charid = ? AND id != 0", new[] { BoundValue.Of(charId_) });
        }

        // CCharRecastContainer::GetRecastList
        public override List<Recast> GetRecastList(RecastType type)
        {
            switch (type)
            {
                case RecastType.Magic: return magic_;
                case RecastType.Ability: return ability_;
                case RecastType.Item: return item_;
                case RecastType.Loot: return loot_;
            }
            // Unhandled Scenario
            log_("Invalid RECASTTYPE received, returning nullptr.");
            return null;
        }

        // CCharRecastContainer::Check: an expired item recast also refreshes the item; item, magic and loot recasts go
        public override void Check()
        {
            for (byte type = 0; type <= (byte)RecastType.Loot; ++type)
            {
                var list = GetRecastList((RecastType)type);
                for (int i = 0; i < list.Count; ++i)
                {
                    var recast = list[i];
                    if (nowMs_() >= recast.TimeStampMs + recast.RecastTimeMs)
                    {
                        if (type == (byte)RecastType.Item) itemReady_(recast.Id);
                        if (type == (byte)RecastType.Item || type == (byte)RecastType.Magic || type == (byte)RecastType.Loot)
                            list.RemoveAt(i--);
                        else
                            recast.RecastTimeMs = 0;
                    }
                }
            }
        }
    }
}
