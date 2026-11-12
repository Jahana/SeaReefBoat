using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;

// Quest rework C4: the harness for Records of Eminence, its pure parts, in the self-test (no database). The
// statements' parse and read-backs; the live side runs in the probes and the roe.* flows
internal static partial class Program
{
    private static void SelfTestC4()
    {
        Console.WriteLine("quest rework C4: the harness for Records of Eminence");
        SelfTestC4Statements();
        SelfTestC4Keys();
        SelfTestC4Overlay();
        SelfTestC4Coverage();
    }

    private const string C4Head = "char zone=WestRonfaure pos=0,0,0 job=WHM lvl=30\n";

    private static Statement C4Step(string text) =>
        Flow.Parse(C4Head + "login\n" + text + "\n", "c4.flow", "probe.c4", null).Steps.Last();

    private static string C4Refused(string text)
    {
        try
        {
            Flow.Parse(C4Head + "login\n" + text + "\n", "c4.flow", "probe.c4", null);
            return "parsed";
        }
        catch (FormatException e)
        {
            return e.Message.Substring(e.Message.IndexOf(' ') + 1);
        }
    }

    private static void SelfTestC4Statements()
    {
        var take = C4Step("record take 2");
        Check("c4 record: take", "take/2/False", $"{take.Status}/{take.Value}/{take.Refused}");
        Check("c4 record: take refused", true, C4Step("record take 2 refused").Refused);
        Check("c4 record: remove", "remove/2", $"{C4Step("record remove 2").Status}/{C4Step("record remove 2").Value}");
        Check("c4 record: claim", "claim/1200", $"{C4Step("record claim 1200").Status}/{C4Step("record claim 1200").Value}");
        var progress = C4Step("record progress 215 9");
        Check("c4 record: progress", "progress/215/9", $"{progress.Status}/{progress.Value}/{progress.Amount}");
        var ev = C4Step("record event HelmSuccess skillType=4 dmg=7");
        Check("c4 record: event", "event/13/skillType=4,dmg=7",
              $"{ev.Status}/{ev.Value}/{string.Join(",", ev.Datagrams.Select(d => $"{d.Key}={d.Value}"))}");
        Check("c4 record: the id's range", "a record id is 1 to 4095: 4096", C4Refused("record take 4096"));
        Check("c4 record: an unknown word", "record takes take, remove, claim, progress or event: fly",
              C4Refused("record fly 2"));
        Check("c4 record: an unknown trigger", "unknown RoE trigger Nope", C4Refused("record event Nope"));
        Check("c4 record: a datagram's form", "a datagram is <name>=<n>: dmg", C4Refused("record event DealDamage dmg"));

        Check("c4 cast: by name, on oneself", "1/-1/", $"{C4Step("cast Cure").Value}/{C4Step("cast Cure").Target}/{C4Step("cast Cure").Name}");
        Check("c4 cast: by id", 159, C4Step("cast 159").Value);
        Check("c4 cast: on a mob by id", (uint)17186822, C4Step("cast Stone #17186822").MobNumber);
        Check("c4 cast: an id out of range", "a spell is an xi.magic.spell id, 1 to 1023: 2000", C4Refused("cast 2000"));
        Check("c4 spawn: by id", (uint)17187111, C4Step("spawn #17187111").MobNumber);
        Check("c4 unity: a leader by name", 1, C4Step("unity Pieuje").Value);
        Check("c4 unity: unknown", "unknown unity leader Nobody", C4Refused("unity Nobody"));

        Check("c4 read-back: a cast that started", null, CastProblem(1, true));
        Check("c4 read-back: a cast that began standing still", null, MovedCastProblem(1, false));
        Check("c4 read-back: a cast that began with a move fails",
              "cast 1: it begins with a move and is interrupted at its end; tick or stand still before the cast",
              MovedCastProblem(1, true));
        Check("c4 read-back: a refused cast fails the step",
              "cast 1: the char is not casting after the 0x01A (MP, level, range, recast, target?)",
              CastProblem(1, false));
        Check("c4 read-back: take accepted", null, RecordTakeProblem(2, false, true));
        Check("c4 read-back: take not accepted", "record take 2: no ROE_START (full log, already taken, refused?)",
              RecordTakeProblem(2, false, false));
        Check("c4 read-back: take refused as declared", null, RecordTakeProblem(2, true, false));
        Check("c4 read-back: take accepted though declared refused", "record take 2 refused: the record started",
              RecordTakeProblem(2, true, true));
        Check("c4 read-back: claim of a record on the log", null, RecordOnLogProblem("record claim", 2, true));
        Check("c4 read-back: claim of a record off the log",
              "record claim 2: the record is not on the log, so the step would record nothing",
              RecordOnLogProblem("record claim", 2, false));
    }

    private static void SelfTestC4Keys()
    {
        Check("c4 keys: a roe flow's key", "roe.rewards",
              Flow.Parse(C4Head + "login\n", "r.flow", "roe.rewards#sparks_cap", null).Key);
        Check("c4 keys: IsRoe", "True/False", $"{ContainerKeys.IsRoe("roe.rewards")}/{ContainerKeys.IsRoe("bastok.the_doorman")}");
        Check("c4 keys: the coverage key of a roe flow", "roe", CoverageKey("roe.rewards"));
        Check("c4 keys: of a quest", "bastok.the_doorman", CoverageKey("bastok.the_doorman"));
        Check("c4 keys: the Lua constant", "xi.roe.records", LuaIdConstant("roe.rewards"));
        var waiver = Flow.Parse(C4Head + "uncovered: check:itemLevel:pass -- no level 117 gear in a flow\nlogin\n",
                                "r.flow", "roe.checks", null).Waivers.Single();
        Check("c4 keys: a waiver of a label", "check:itemLevel:pass", waiver.Label);
        var labels = new[] { "trigger:DEFEAT_MOB", "req:level:fail", "reward:sparks_cap:first", "reward:sparks_cap", "flag:hidden" };
        Check("c4 keys: every waivable label form parses", string.Join(",", labels),
              string.Join(",", Flow.Parse(C4Head + string.Concat(labels.Select(l => $"uncovered: {l} -- x\n")) + "login\n",
                                          "r.flow", "roe.labels", null).Waivers.Select(w => w.Label)));
    }

    // the Lua files gone at HEAD: a roe record takes both; a quest record that takes a file requiring roe brings them back
    private static void SelfTestC4Overlay()
    {
        var changed = new List<(string Rel, string OldText, string NowText)>
        {
            ("scripts/globals/caskets.lua", "require('scripts/globals/roe')\n", "\n"),
            ("scripts/globals/roe.lua", "require('scripts/globals/roe_records')\nxi.roe.initialize = xi.roe.records\n", ""),
            ("scripts/globals/roe_records.lua", "xi.roe.records = {}\n", ""),
            ("scripts/globals/trust.lua", "require('scripts/globals/roe')\nxi.quest.id.sandoria.TRUST_SANDORIA\n", "\n"),
        };
        var roe = new[] { (Key: "roe.rewards", Legacy: RoeLuaFile, Constant: LuaIdConstant("roe.rewards")) };
        Check("c4 overlay: a roe record takes the two files, nothing else",
              "scripts/globals/roe.lua,scripts/globals/roe_records.lua",
              string.Join(",", OverlayPlan(changed, roe).Taken));
        var quest = new[] { (Key: "sandoria.trust_sandoria", Legacy: (string)null,
                             Constant: "xi.quest.id.sandoria.TRUST_SANDORIA") };
        Check("c4 overlay: a quest record taking a file that requires roe takes roe too",
              "scripts/globals/roe.lua,scripts/globals/roe_records.lua,scripts/globals/trust.lua",
              string.Join(",", OverlayPlan(changed, quest).Taken));
    }

    private static void SelfTestC4Coverage()
    {
        var all = new List<Flow>
        {
            Flow.Parse(C4Head + "login\n", "a.flow", "roe.rewards", null),
            Flow.Parse(C4Head + "uncovered: flag:hidden -- ten completions\nlogin\n", "b.flow", "roe.flags#x", null),
        };
        var cov = new Coverage();
        cov.Ran.Add("roe.rewards");
        cov.Ran.Add("roe.flags#x");
        cov.Declared["roe"] = new List<string> { "trigger:DEFEAT_MOB", "flag:hidden" };
        cov.Add("roe", "trigger:DEFEAT_MOB", "roe.rewards");
        Check("c4 coverage: every roe flow counts for the one key", "PASS coverage roe: 1 of 2 handlers reached, 1 waived",
              string.Join(" | ", CoverageResults(cov, all)));
        cov.Ran.Remove("roe.flags#x");
        Check("c4 coverage: not every flow ran: no verdict", "", string.Join(" | ", CoverageResults(cov, all)));
        cov.Ran.Clear();
        Check("c4 coverage: flows that ran on Lua are no flows of the gate", "", string.Join(" | ", CoverageResults(cov, all)));
    }
}
