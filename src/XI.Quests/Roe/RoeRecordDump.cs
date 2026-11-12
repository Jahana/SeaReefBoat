using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace XI.Quests
{
    // the canonical text of a record, the format of tests/golden/roe_records.txt, which the running Lua wrote before
    // the port: numbers only, every set sorted, keys in byte order
    public static class RoeRecordDump
    {
        private static readonly (RoeFlags Flag, string Name)[] FlagNames =
        {
            (RoeFlags.Daily, "daily"), (RoeFlags.Hidden, "hidden"), (RoeFlags.Repeat, "repeat"),
            (RoeFlags.Retro, "retro"), (RoeFlags.Timed, "timed"), (RoeFlags.Unity, "unity"),
            (RoeFlags.Weekly, "weekly"),
        };

        public static string Text(IEnumerable<RoeRecord> records, int[][] timed)
        {
            var sb = new StringBuilder();
            foreach (var r in records) sb.Append(Line(r)).Append('\n');
            for (int day = 0; day < timed.Length; day++)
                sb.Append($"timed {day + 1} {string.Join(",", timed[day])}\n");
            return sb.ToString();
        }

        public static string Line(RoeRecord r)
        {
            string flags = string.Join(",", FlagNames.Where(f => (r.Flags & f.Flag) != 0).Select(f => f.Name));
            return $"{r.Id} trigger={r.Trigger} goal={r.Goal} increment={r.Increment} notify={r.Notify} " +
                   $"flags={flags} check={Form(r)} reqs={Section(Reqs(r.Reqs))} reward={Section(Reward(r.Reward))}";
        }

        private static string Form(RoeRecord r) => r.Check switch
        {
            RoeCheckForm.Master => "master",
            RoeCheckForm.MainLevel => "mainLevel",
            RoeCheckForm.Sparks => "sparks",
            RoeCheckForm.ItemLevel => "itemLevel",
            RoeCheckForm.QuestEither => $"questEither:{r.EitherQuest.Log}:{r.EitherQuest.First}:{r.EitherQuest.Second}",
            RoeCheckForm.ProgressDmg => "progressDmg",
            RoeCheckForm.ProgressHeal => "progressHeal",
            RoeCheckForm.ProgressExp => "progressExp",
            _ => throw new ArgumentOutOfRangeException(nameof(r)),
        };

        private static string Section(IEnumerable<(string Key, string Value)> entries) =>
            string.Join(";", entries.OrderBy(e => e.Key, StringComparer.Ordinal).Select(e => $"{e.Key}:{e.Value}"));

        private static string Set(IEnumerable<int> values) => "{" + string.Join(",", values.OrderBy(v => v)) + "}";
        private static string Set(IEnumerable<string> values) =>
            "{" + string.Join(",", values.OrderBy(v => v, StringComparer.Ordinal)) + "}";

        private static IEnumerable<(string, string)> Reqs(RoeReqs r)
        {
            if (r.MobId != null) yield return ("mobID", Set(r.MobId));
            if (r.MobName != null) yield return ("mobName", Set(r.MobName));
            if (r.MobXp) yield return ("mobXP", "true");
            if (r.MobSpecies != null) yield return ("mobSpecies", Set(r.MobSpecies));
            if (r.MobFamily != null) yield return ("mobFamily", Set(r.MobFamily));
            if (r.MobSystem != null) yield return ("mobSystem", Set(r.MobSystem));
            if (r.DmgMin is int dmgMin) yield return ("dmgMin", N(dmgMin));
            if (r.DmgMax is int dmgMax) yield return ("dmgMax", N(dmgMax));
            if (r.AtkType is int atkType) yield return ("atkType", N(atkType));
            if (r.HealMin is int healMin) yield return ("healMin", N(healMin));
            if (r.Zone != null) yield return ("zone", Set(r.Zone));
            if (r.ZoneNot != null) yield return ("zoneNot", Set(r.ZoneNot));
            if (r.ItemId != null) yield return ("itemID", Set(r.ItemId));
            if (r.LevelSync) yield return ("levelSync", "true");
            if (r.JobLvl is { } job) yield return ("jobLvl", $"[{job.Job},{job.Level}]");
            if (r.QuestComplete is { } quest) yield return ("questComplete", $"[{quest.Log},{quest.Id}]");
            if (r.MissionComplete is { } mission) yield return ("missionComplete", $"[{mission.Log},{mission.Id}]");
            if (r.UnityLeader is int unity) yield return ("unityLeader", N(unity));
            if (r.SkillType is int skill) yield return ("skillType", N(skill));
            if (r.Monipulator) yield return ("monipulator", "true");
            if (r.MJobLvl is int mJobLvl) yield return ("mJobLvl", N(mJobLvl));
            if (r.CollectSparks is int sparks) yield return ("collectSparks", N(sparks));
            if (r.HasItemLevel is int itemLevel) yield return ("hasItemLevel", N(itemLevel));
        }

        private static IEnumerable<(string, string)> Reward(RoeReward r)
        {
            if (r.Item != null) yield return ("item", "[" + string.Join(",", r.Item.Select(Item)) + "]");
            if (r.KeyItem is int keyItem) yield return ("keyItem", N(keyItem));
            if (r.Sparks is int sparks) yield return ("sparks", N(sparks));
            if (r.Exp is int exp) yield return ("exp", N(exp));
            if (r.Accolades is int accolades) yield return ("accolades", N(accolades));
            if (r.Capacity is int capacity) yield return ("capacity", N(capacity));
        }

        private static string Item(RoeItem i) => i.Qty is int qty ? $"[{i.Id},{qty}]" : N(i.Id);
        private static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
    }
}
