// XI.Verify.Quests -- quest rework C4: RoeChecks over fakes: each reqs key, pass and fail; each check form; the repeat
// item exception, the keys masterCheck has no check for, and the coverage labels
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;

internal static partial class Program
{
    private sealed class FakeRoeMob : IRoeMob
    {
        public uint Id { get; init; }
        public string Name { get; init; } = "Mob";
        public int Species { get; init; }
        public int Family { get; init; }
        public int Ecosystem { get; init; }
    }

    private sealed class FakeRoeFacts : IRoeFacts
    {
        public bool Credit;
        public bool CheckKillCredit(IRoeMob mob) => Credit;
        public int ZoneId { get; init; } = 100;
        public bool HasLevelSync { get; init; }
        public Dictionary<int, int> Jobs = new();
        public int JobLevel(int job) => Jobs.GetValueOrDefault(job);
        public HashSet<(int, int)> Quests = new(), Missions = new();
        public bool QuestCompleted(int log, int id) => Quests.Contains((log, id));
        public bool MissionCompleted(int log, int id) => Missions.Contains((log, id));
        public int UnityLeader { get; init; }
        public int MainJob { get; init; } = 1;
        public int MainLevel { get; init; } = 30;
        public int Sparks { get; init; }
        public int AverageItemLevel { get; init; } = 99;
    }

    private static bool C4Pass(RoeReqs reqs, IRoeFacts facts, RoeParams p) =>
        RoeChecks.Run(new RoeRecord(1) { Reqs = reqs }, facts, p);

    private static RoeParams C4Params(params (string Key, long Value)[] numbers)
    {
        var p = new RoeParams { Progress = 0 };
        foreach (var (key, value) in numbers) p.Set(key, value);
        return p;
    }

    // one reqs key: passing and failing inputs give true and false
    private static void C4Req(string key, RoeReqs reqs, IRoeFacts passFacts, RoeParams passParams, IRoeFacts failFacts, RoeParams failParams)
    {
        Check($"c4 checks: {key} passes", "True", C4Pass(reqs, passFacts, passParams).ToString());
        Check($"c4 checks: {key} fails", "False", C4Pass(reqs, failFacts, failParams).ToString());
    }

    internal static void RunC4Checks()
    {
        Console.WriteLine("\n== c4 checks: RoeChecks over fakes ==");
        var none = new FakeRoeFacts();
        RoeParams Mob(IRoeMob m)
        {
            var p = new RoeParams { Progress = 0 };
            p.Mob = m;
            return p;
        }
        var rabbit = new FakeRoeMob { Id = 17186822, Name = "Wild_Rabbit", Species = 3, Family = 7, Ecosystem = 5 };
        C4Req("mobID", new RoeReqs { MobId = new[] { 17186822 } }, none, Mob(rabbit), none, Mob(new FakeRoeMob { Id = 9 }));
        Check("c4 checks: mobID without a mob fails", "False", C4Pass(new RoeReqs { MobId = new[] { 1 } }, none, C4Params()).ToString());
        C4Req("mobName", new RoeReqs { MobName = new[] { "Wild_Rabbit" } }, none, Mob(rabbit), none, Mob(new FakeRoeMob()));
        C4Req("mobXP", new RoeReqs { MobXp = true }, new FakeRoeFacts { Credit = true }, Mob(rabbit), none, Mob(rabbit));
        C4Req("mobSpecies", new RoeReqs { MobSpecies = new[] { 3 } }, none, Mob(rabbit), none, Mob(new FakeRoeMob()));
        C4Req("mobFamily", new RoeReqs { MobFamily = new[] { 7 } }, none, Mob(rabbit), none, Mob(new FakeRoeMob()));
        C4Req("mobSystem", new RoeReqs { MobSystem = new[] { 5 } }, none, Mob(rabbit), none, Mob(new FakeRoeMob()));
        C4Req("dmgMin", new RoeReqs { DmgMin = 10 }, none, C4Params(("dmg", 10)), none, C4Params(("dmg", 9)));
        C4Req("dmgMax", new RoeReqs { DmgMax = 20 }, none, C4Params(("dmg", 20)), none, C4Params(("dmg", 21)));
        Check("c4 checks: dmgMin with no dmg fails", "False", C4Pass(new RoeReqs { DmgMin = 1 }, none, C4Params()).ToString());
        C4Req("atkType", new RoeReqs { AtkType = 1 }, none, C4Params(("atkType", 1)), none, C4Params(("atkType", 2)));
        C4Req("healMin", new RoeReqs { HealMin = 500 }, none, C4Params(("heal", 500)), none, C4Params(("heal", 499)));
        C4Req("zone", new RoeReqs { Zone = new[] { 100 } }, none, C4Params(), new FakeRoeFacts { ZoneId = 101 }, C4Params());
        C4Req("zoneNot", new RoeReqs { ZoneNot = new[] { 101 } }, none, C4Params(), new FakeRoeFacts { ZoneId = 101 }, C4Params());
        C4Req("itemID", new RoeReqs { ItemId = new[] { 4096 } }, none, C4Params(("itemid", 4096)), none, C4Params(("itemid", 4097)));
        C4Req("levelSync", new RoeReqs { LevelSync = true }, new FakeRoeFacts { HasLevelSync = true }, C4Params(), none, C4Params());
        C4Req("jobLvl", new RoeReqs { JobLvl = (1, 30) }, new FakeRoeFacts { Jobs = { [1] = 30 } }, C4Params(), new FakeRoeFacts { Jobs = { [1] = 29 } }, C4Params());
        C4Req("questComplete", new RoeReqs { QuestComplete = (0, 5) }, new FakeRoeFacts { Quests = { (0, 5) } }, C4Params(), none, C4Params());
        C4Req("missionComplete", new RoeReqs { MissionComplete = (0, 5) }, new FakeRoeFacts { Missions = { (0, 5) } }, C4Params(), none, C4Params());
        C4Req("unityLeader", new RoeReqs { UnityLeader = 1 }, new FakeRoeFacts { UnityLeader = 1 }, C4Params(), new FakeRoeFacts { UnityLeader = 2 }, C4Params());
        C4Req("skillType", new RoeReqs { SkillType = 50 }, none, C4Params(("skillType", 50)), none, C4Params(("skillType", 49)));
        C4Req("monipulator", new RoeReqs { Monipulator = true }, new FakeRoeFacts { MainJob = 23 }, C4Params(), none, C4Params());
        Check("c4 checks: the three keys masterCheck has no check for fail it", "False,False,False", string.Join(",",
            new[] { new RoeReqs { MJobLvl = 1 }, new RoeReqs { CollectSparks = 1 }, new RoeReqs { HasItemLevel = 1 } }
                .Select(r => C4Pass(r, none, C4Params()))));
        Check("c4 checks: no reqs pass", "True", C4Pass(new RoeReqs(), none, C4Params()).ToString());
        Check("c4 checks: all reqs must pass", "False",
              C4Pass(new RoeReqs { Zone = new[] { 100 }, DmgMin = 5 }, none, C4Params()).ToString());

        bool Form(RoeRecord r, IRoeFacts f, RoeParams p = null) => RoeChecks.Run(r, f, p ?? C4Params());
        var level = new RoeRecord(1045) { Check = RoeCheckForm.MainLevel, Reqs = new RoeReqs { MJobLvl = 99 } };
        Check("c4 checks: form mainLevel", "True,False",
              $"{Form(level, new FakeRoeFacts { MainLevel = 99 })},{Form(level, new FakeRoeFacts { MainLevel = 98 })}");
        var sparks = new RoeRecord(1046) { Check = RoeCheckForm.Sparks, Reqs = new RoeReqs { CollectSparks = 2000 } };
        Check("c4 checks: form sparks", "True,False",
              $"{Form(sparks, new FakeRoeFacts { Sparks = 2000 })},{Form(sparks, new FakeRoeFacts { Sparks = 1999 })}");
        var ilvl = new RoeRecord(1049) { Check = RoeCheckForm.ItemLevel, Reqs = new RoeReqs { HasItemLevel = 117 } };
        Check("c4 checks: form itemLevel", "True,False",
              $"{Form(ilvl, new FakeRoeFacts { AverageItemLevel = 117 })},{Form(ilvl, new FakeRoeFacts { AverageItemLevel = 116 })}");
        var either = new RoeRecord(505) { Check = RoeCheckForm.QuestEither, EitherQuest = (4, 1, 2) };
        Check("c4 checks: form questEither", "True,True,False",
              $"{Form(either, new FakeRoeFacts { Quests = { (4, 1) } })},{Form(either, new FakeRoeFacts { Quests = { (4, 2) } })},{Form(either, none)}");
        var total = new RoeRecord(29) { Check = RoeCheckForm.ProgressDmg };
        var added = C4Params(("dmg", 40));
        added.Progress = 100;
        Check("c4 checks: form progress adds the datagram", "True/140", $"{Form(total, none, added)}/{added.Progress}");
        var zero = C4Params(("dmg", 0));
        Check("c4 checks: form progress at 0 fails, the progress as it was", "False/0", $"{Form(total, none, zero)}/{zero.Progress}");
        Check("c4 checks: form heal reads heal, not dmg", "False",
              Form(new RoeRecord(31) { Check = RoeCheckForm.ProgressHeal }, none, C4Params(("dmg", 5))).ToString());
        Check("c4 checks: form exp reads exp, not capacity", "False",
              Form(new RoeRecord(4013) { Check = RoeCheckForm.ProgressExp }, none, C4Params(("capacity", 5), ("heal", 5))).ToString());
        var expRecord = new RoeRecord(4013) { Check = RoeCheckForm.ProgressExp };
        var gained = C4Params(("exp", 7));
        Check("c4 checks: form exp adds the exp", "True/7", $"{Form(expRecord, none, gained)}/{gained.Progress}");

        bool Ex(params RoeItem[] items) => RoeChecks.IsRepeatItemRewardException(items);
        const int voucher = XI.QuestContent.Item.CopperAmanVoucher;
        Check("c4 checks: a number item is no exception", "False", Ex(RoeItem.Of(voucher)).ToString());
        Check("c4 checks: the two pouches are", "True,True",
              $"{Ex(RoeItem.Of(XI.QuestContent.Item.SiltPouch))},{Ex(RoeItem.Of(XI.QuestContent.Item.BeadPouch))}");
        Check("c4 checks: the pair form always is (the kept quirk)", "True", Ex(RoeItem.Of(voucher, 2)).ToString());
        Check("c4 checks: one number among pairs is not", "False",
              Ex(RoeItem.Of(voucher, 2), RoeItem.Of(voucher)).ToString());

        var labels = RoeChecks.CoverageLabels();
        // 18 triggers, 19 reqs and 7 check forms pass and fail, 9 reward labels, 7 flags
        Check("c4 checks: 86 labels, each once", "86/86", $"{labels.Count}/{labels.Distinct().Count()}");
        var seen = new List<string>();
        RoeChecks.Run(new RoeRecord(1) { Reqs = new RoeReqs { Zone = new[] { 1 }, DmgMin = 5 } }, none, C4Params(("dmg", 5)), seen.Add);
        Check("c4 checks: every req reports, past a failed one", "req:dmgMin:pass,req:zone:fail", string.Join(",", seen.OrderBy(s => s, StringComparer.Ordinal)));
        Check("c4 checks: reached labels are declared", "", string.Join(",", seen.Except(labels)));
    }
}
