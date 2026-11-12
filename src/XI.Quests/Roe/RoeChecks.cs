using System;
using System.Collections.Generic;
using System.Linq;

namespace XI.Quests
{
    public interface IRoeMob
    {
        uint Id { get; }
        string Name { get; }
        int Species { get; }
        int Family { get; }
        int Ecosystem { get; }
    }

    // what the checks read of the player
    public interface IRoeFacts
    {
        bool CheckKillCredit(IRoeMob mob);
        int ZoneId { get; }
        bool HasLevelSync { get; }
        int JobLevel(int job);
        bool QuestCompleted(int log, int id);
        bool MissionCompleted(int log, int id);
        int UnityLeader { get; }
        int MainJob { get; }
        int MainLevel { get; }
        int Sparks { get; }
        int AverageItemLevel { get; }
    }

    // the params table of xi.roe.onRecordTrigger: the progress, the mob, and the datagrams by name
    public sealed class RoeParams
    {
        private readonly Dictionary<string, long> numbers_ = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<string, object> texts_ = new Dictionary<string, object>(StringComparer.Ordinal);

        public long? Progress { get; set; }
        public IRoeMob Mob { get; set; }

        public void Set(string key, long value) => numbers_[key] = value;
        public void SetText(string key, object value) => texts_[key] = value;
        public long? Number(string key) => numbers_.TryGetValue(key, out long v) ? v : null;
        public object Text(string key) => texts_.TryGetValue(key, out var v) ? v : null;

        public bool Claim => numbers_.ContainsKey("claim");
        public long? Dmg => Number("dmg");
        public long? Heal => Number("heal");
        public long? Exp => Number("exp");
        public long? AtkType => Number("atkType");
        public long? ItemId => Number("itemid");
        public long? SkillType => Number("skillType");
    }

    // scripts/globals/roe.lua's checks and masterCheck, and the records' own check functions, pure over IRoeFacts
    public static class RoeChecks
    {
        private const int JobMon = 23;              // xi.job.MON
        private const int SiltPouch = 6391, BeadPouch = 6392;   // xi.item, the Escha rewards

        // xi.roeTrigger, by id: index 0 is ROE_NONE
        public static readonly string[] TriggerNames =
        {
            null, "DEFEAT_MOB", "USE_WEAPONSKILL", "LOOT_ITEM", "SUCCESSFUL_SYNTHESIS", "TAKE_DAMAGE", "DEAL_DAMAGE",
            "GAIN_EXPERIENCE", "HEAL_ALLY", "BUFF_ALLY", "ACHIEVE_LEVEL", "COMPLETE_QUEST", "COMPLETE_MISSION",
            "HELM_SUCCESS", "CHOCOBO_DIG_SUCCESS", "UNITY_CHAT", "MAGIC_BURST", "HEAL_UNITY_ALLY", "TRIGGER_NPC",
        };

        private static readonly string[] ReqKeys =
        {
            "mobID", "mobName", "mobXP", "mobFamily", "mobSystem", "dmgMin", "dmgMax", "atkType", "healMin", "zone",
            "zoneNot", "itemID", "levelSync", "jobLvl", "questComplete", "missionComplete", "unityLeader", "skillType",
            "monipulator",
        };

        private static readonly string[] CheckForms =
        {
            "mainLevel", "sparks", "itemLevel", "questEither", "progressDmg", "progressHeal", "progressExp",
        };

        private static readonly string[] RewardLabels =
        {
            "reward:item:first", "reward:item:repeat", "reward:item:full", "reward:keyItem", "reward:sparks:first",
            "reward:sparks:repeat", "reward:exp", "reward:capacity", "reward:accolades",
        };

        private static readonly string[] FlagLabels =
        {
            "flag:repeat", "flag:timed", "flag:retro", "flag:daily", "flag:weekly", "flag:unity", "flag:hidden",
        };

        // what the coverage gate wants reached by a trace or waived. mJobLvl, collectSparks and hasItemLevel are
        // reached through their check forms; mobSpecies is in no record, and C4Checks has its two arms
        public static List<string> CoverageLabels()
        {
            var labels = new List<string>();
            labels.AddRange(TriggerNames.Skip(1).Select(t => "trigger:" + t));
            foreach (var key in ReqKeys) labels.AddRange(new[] { $"req:{key}:pass", $"req:{key}:fail" });
            foreach (var form in CheckForms) labels.AddRange(new[] { $"check:{form}:pass", $"check:{form}:fail" });
            labels.AddRange(RewardLabels);
            labels.AddRange(FlagLabels);
            return labels;
        }

        // entry:check(player, params): the test's own check, else the record's form
        public static bool Run(RoeRecord self, IRoeFacts player, RoeParams p, Action<string> reached = null)
        {
            if (self.TestCheck != null) return self.TestCheck(player, p);
            var reqs = self.Reqs;
            switch (self.Check)
            {
                case RoeCheckForm.Master:
                    return Master(self, player, p, reached);
                case RoeCheckForm.MainLevel:
                    return Form("mainLevel", player.MainLevel >= reqs.MJobLvl, reached);
                case RoeCheckForm.Sparks:
                    return Form("sparks", player.Sparks >= reqs.CollectSparks, reached);
                case RoeCheckForm.ItemLevel:
                    return Form("itemLevel", player.AverageItemLevel >= reqs.HasItemLevel, reached);
                case RoeCheckForm.QuestEither:
                    var (log, first, second) = self.EitherQuest;
                    bool either = player.QuestCompleted(log, first) || player.QuestCompleted(log, second);
                    return Form("questEither", either, reached);
                case RoeCheckForm.ProgressDmg:
                    return Form("progressDmg", AddProgress(p, p.Dmg), reached);
                case RoeCheckForm.ProgressHeal:
                    return Form("progressHeal", AddProgress(p, p.Heal), reached);
                case RoeCheckForm.ProgressExp:
                    return Form("progressExp", AddProgress(p, p.Exp), reached);
                default:
                    throw new ArgumentOutOfRangeException(nameof(self));
            }
        }

        private static bool Form(string form, bool pass, Action<string> reached)
        {
            reached?.Invoke($"check:{form}:{(pass ? "pass" : "fail")}");
            return pass;
        }

        // if params.X and params.X > 0 then params.progress = params.progress + params.X return true end return false
        private static bool AddProgress(RoeParams p, long? amount)
        {
            if (amount is not long x || x <= 0) return false;
            p.Progress += x;
            return true;
        }

        // masterCheck: every key of reqs must pass, and a key with no check fails. The Lua stops at its first failure
        // in pairs() order; the checks read and never write, so here each one runs and reports, whatever the order
        public static bool Master(RoeRecord self, IRoeFacts player, RoeParams p, Action<string> reached = null)
        {
            var r = self.Reqs;
            var mob = p.Mob;
            bool ok = true;
            void Req(string key, bool present, Func<bool> check)
            {
                if (!present) return;
                bool pass = check();
                reached?.Invoke($"req:{key}:{(pass ? "pass" : "fail")}");
                ok &= pass;
            }
            Req("mobID", r.MobId != null, () => mob != null && r.MobId.Contains((int)mob.Id));
            Req("mobName", r.MobName != null, () => mob != null && r.MobName.Contains(mob.Name));
            Req("mobXP", r.MobXp, () => mob != null && player.CheckKillCredit(mob));
            Req("mobSpecies", r.MobSpecies != null, () => mob != null && r.MobSpecies.Contains(mob.Species));
            Req("mobFamily", r.MobFamily != null, () => mob != null && r.MobFamily.Contains(mob.Family));
            Req("mobSystem", r.MobSystem != null, () => mob != null && r.MobSystem.Contains(mob.Ecosystem));
            Req("dmgMin", r.DmgMin != null, () => p.Dmg >= r.DmgMin);
            Req("dmgMax", r.DmgMax != null, () => p.Dmg <= r.DmgMax);
            Req("atkType", r.AtkType != null, () => p.AtkType == r.AtkType);
            Req("healMin", r.HealMin != null, () => p.Heal >= r.HealMin);
            Req("zone", r.Zone != null, () => r.Zone.Contains(player.ZoneId));
            Req("zoneNot", r.ZoneNot != null, () => !r.ZoneNot.Contains(player.ZoneId));
            Req("itemID", r.ItemId != null, () => p.ItemId is long id && r.ItemId.Contains((int)id));
            Req("levelSync", r.LevelSync, () => player.HasLevelSync);
            Req("jobLvl", r.JobLvl != null, () => player.JobLevel(r.JobLvl.Value.Job) >= r.JobLvl.Value.Level);
            Req("questComplete", r.QuestComplete != null,
                () => player.QuestCompleted(r.QuestComplete.Value.Log, r.QuestComplete.Value.Id));
            Req("missionComplete", r.MissionComplete != null,
                () => player.MissionCompleted(r.MissionComplete.Value.Log, r.MissionComplete.Value.Id));
            Req("unityLeader", r.UnityLeader != null, () => player.UnityLeader == r.UnityLeader);
            Req("skillType", r.SkillType != null, () => p.SkillType == r.SkillType);
            Req("monipulator", r.Monipulator, () => player.MainJob == JobMon);
            // no entry in the Lua's checks table: masterCheck fails
            if (r.MJobLvl != null || r.CollectSparks != null || r.HasItemLevel != null) ok = false;
            return ok;
        }

        // isRepeatItemRewardException: only a number element is looked at, so a { { id, n } } reward is granted on
        // every repeat. Kept (46 repeat records; the "Content bugs the ports keep on purpose" row)
        public static bool IsRepeatItemRewardException(IReadOnlyList<RoeItem> items)
        {
            foreach (var item in items)
                if (item.Qty == null && item.Id != SiltPouch && item.Id != BeadPouch) return false;
            return true;
        }
    }
}
