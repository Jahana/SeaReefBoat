using System;
using System.Collections.Generic;

namespace XI.Quests
{
    [Flags]
    public enum RoeFlags : byte
    {
        None = 0, Repeat = 1, Retro = 2, Unity = 4, Timed = 8, Daily = 16, Weekly = 32, Hidden = 64,
    }

    // the record's check: Master runs reqs; the others are the 16 records' own functions, one form each
    public enum RoeCheckForm : byte
    {
        Master, MainLevel, Sparks, ItemLevel, QuestEither, ProgressDmg, ProgressHeal, ProgressExp,
    }

    // a test's own check (XI.Verify.Roe.Live, XI.Verify.Capacity.Live): true when the record's check passes
    public delegate bool RoeCheck(IRoeFacts player, RoeParams p);

    // an element of reward.item: a number ({ id }) when Qty is null, a pair ({ { id, qty } }) otherwise.
    // The two differ: only a number is looked at by the repeat exception
    public readonly struct RoeItem
    {
        public int Id { get; }
        public int? Qty { get; }

        private RoeItem(int id, int? qty)
        {
            Id = id;
            Qty = qty;
        }

        public static RoeItem Of(int id) => new RoeItem(id, null);
        public static RoeItem Of(int id, int qty) => new RoeItem(id, qty);
    }

    // the reqs table: each member's Lua key is in RoeRecordDump; null or false is a key the record does not have
    public sealed class RoeReqs
    {
        public int[] MobId { get; init; }
        public string[] MobName { get; init; }
        public bool MobXp { get; init; }
        public int[] MobSpecies { get; init; }
        public int[] MobFamily { get; init; }
        public int[] MobSystem { get; init; }
        public int? DmgMin { get; init; }
        public int? DmgMax { get; init; }
        public int? AtkType { get; init; }
        public int? HealMin { get; init; }
        public int[] Zone { get; init; }
        public int[] ZoneNot { get; init; }
        public int[] ItemId { get; init; }
        public bool LevelSync { get; init; }
        public (int Job, int Level)? JobLvl { get; init; }
        public (int Log, int Id)? QuestComplete { get; init; }
        public (int Log, int Id)? MissionComplete { get; init; }
        public int? UnityLeader { get; init; }
        public int? SkillType { get; init; }
        public bool Monipulator { get; init; }
        // read by a check form, never by masterCheck, which has no such check and fails on them
        public int? MJobLvl { get; init; }
        public int? CollectSparks { get; init; }
        public int? HasItemLevel { get; init; }
    }

    public sealed class RoeReward
    {
        public IReadOnlyList<RoeItem> Item { get; init; }
        public int? KeyItem { get; init; }
        public int? Sparks { get; init; }
        public int? Exp { get; init; }
        public int? Accolades { get; init; }
        public int? Capacity { get; init; }
    }

    public sealed class RoeRecord
    {
        public RoeRecord(int id) => Id = id;

        public int Id { get; }
        public int Trigger { get; init; }             // 0: none
        public int Goal { get; init; } = 1;
        public int Increment { get; init; } = 1;
        public int Notify { get; init; } = 1;
        public RoeFlags Flags { get; init; }
        public RoeCheckForm Check { get; init; }
        // the two quests of RoeCheckForm.QuestEither: (log, id) each
        public (int Log, int First, int Second) EitherQuest { get; init; }
        public RoeReqs Reqs { get; init; } = new RoeReqs();
        public RoeReward Reward { get; init; } = new RoeReward();
        // test only: runs in place of Check
        public RoeCheck TestCheck { get; init; }
    }
}
