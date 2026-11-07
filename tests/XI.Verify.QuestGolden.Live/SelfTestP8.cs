using System;
using System.Collections.Generic;
using System.Linq;

// quest rework P8: the flow-list selector and --fail-fast's report (Part A); the new statements' parser (Part B), in the
// self-test (no database)
internal static partial class Program
{
    private static void SelfTestPlatformP8()
    {
        Console.WriteLine("quest rework P8: the selector, --fail-fast");
        SelfTestP8Selector();
        Console.WriteLine("quest rework P8 statements");
        SelfTestP8Statements();
        SelfTestP8Refusals();
        SelfTestP8Guards();
        Console.WriteLine("quest rework P8: --jobs shards by cost");
        SelfTestP8Shards();
    }

    // A4: ShardsByCost (longest first into the least-loaded shard) and FlowCost
    private static void SelfTestP8Shards()
    {
        static string Split(List<int>[] sh) => string.Join(" | ", sh.Select(x => string.Join(",", x)));
        var names = new[] { "a", "b", "c", "d", "e", "f" };
        Check("  shards: 5 4 3 3 2 1 into two: the costliest first into the least loaded (9 and 9)",
              "0,3,5 | 1,2,4", Split(ShardsByCost(new long[] { 5, 4, 3, 3, 2, 1 }, names, 2)));
        Check("  shards: equal costs in name order round the shards", "0,3 | 1,4 | 2,5",
              Split(ShardsByCost(new long[] { 1, 1, 1, 1, 1, 1 }, names, 3)));
        Check("  shards: a tie in cost goes by name, not by index", "1 | 0",
              Split(ShardsByCost(new long[] { 2, 2 }, new[] { "z", "y" }, 2)));
        Check("  shards: one shard takes all; more shards than flows leave some empty", "0,1,2,3,4,5/0 | 1 | ",
              $"{Split(ShardsByCost(new long[] { 5, 4, 3, 3, 2, 1 }, names, 1))}/{Split(ShardsByCost(new long[] { 2, 1 }, new[] { "a", "b" }, 3))}");
        // a skewed set (one zone's many cheap flows and a few long ones, as the zone split left them): the loads within one
        // flow's cost of each other, every index once
        var costs = Enumerable.Range(0, 120).Select(k => (long)(k < 10 ? 9000 : 1800 + k * 7 % 500)).ToList();
        var many = ShardsByCost(costs, costs.Select((_, k) => $"f{k:D3}").ToList(), 6);
        var loads = many.Select(sh => sh.Sum(k => costs[k])).ToList();
        Check("  shards: 120 skewed flows into six: every flow once, the loads within the largest flow's cost",
              "120/True", $"{many.SelectMany(x => x).Distinct().Count()}/{loads.Max() - loads.Min() <= costs.Max()}");
        var f = Flow.Parse("char zone=SouthernSanDoria pos=0,0,0 job=WAR lvl=30\ncharvar X 1\nlogin\ntrigger Kipopo\nwarp 0,0,0 PortBastok\nfinish\n",
                           "cost.flow", "cost", null);
        Check("  FlowCost: 1800 + login 400 + warp 200 + 15 each for trigger and finish (a setup line is no step)", "2430", FlowCost(f).ToString());
    }

    // B1-B9: each new statement parsed (header / step, the label, the parsed values)
    private static void SelfTestP8Statements()
    {
        try
        {
            var f = Flow.Parse("char zone=SouthernSanDoria pos=0,0,0 job=SMN lvl=75 sjob=WHM slvl=37\n" +
                               "item 2012\nitem 850 x2\nitem CopperIngot\nspell 298\n" +
                               "login\n" +
                               "trade Kipopo 2012 x1 850 x1 SpoolOfSilkThread x1\n" +
                               "item 2012 x3\n" +
                               "effect 47\n" +
                               "kill #17813813 # a comment after the id\n" +
                               "claim #17813814\n" +
                               "npcstatus Kipopo normal\n" +
                               "npcstatus Kipopo disappear\n" +
                               "missionstatus Cop 14 pos=2\n" +
                               "missionstatus Cop 70000\n" +
                               "spell 303\n" +
                               "currency bayld 150000\n" +
                               "bfwin\n", "p8.flow", "p8", null);
            var cs = f.Char;
            Check("  char sjob=<JOB> slvl=<n>: the support job and its level (job= kept)", "15/3/37", $"{cs.Job}/{cs.SJob}/{cs.SLvl}");
            Check("  item <id> [x<n>] before the login: the id and the count, a name beside it as before", "item 2012 1|item 850 2|item 648 1|spell 298 0",
                  string.Join("|", f.Setup.Select(st => $"{st.Verb} {st.Value} {st.Amount}")));
            var steps = f.Steps.Skip(1).ToList();
            Check("  trade <Npc> <id> x<n> ...: ids and names mixed", "Kipopo 2012:1,850:1,816:1",
                  $"{steps[0].Name} {string.Join(",", steps[0].Items.Select(i => $"{i.Item}:{i.Qty}"))}");
            Check("  item <id> x<n> after the login: a step", "item 2012 x3|2012|3", $"{steps[1].Label}|{steps[1].Value}|{steps[1].Amount}");
            Check("  effect <id>: a step", "effect 47|47", $"{steps[2].Label}|{steps[2].Value}");
            Check("  kill #<id> / claim #<id>: the id kept (its '#' is no comment; a comment after it is)", "kill #17813813|17813813|claim #17813814|17813814",
                  $"{steps[3].Label}|{steps[3].MobNumber}|{steps[4].Label}|{steps[4].MobNumber}");
            Check("  npcstatus <Npc> <normal|disappear>: the NPC as trigger names it, the status", "Kipopo/0/normal|Kipopo/0/disappear",
                  $"{steps[5].Name}/{steps[5].Offset}/{steps[5].Status}|{steps[6].Name}/{steps[6].Offset}/{steps[6].Status}");
            Check("  missionstatus <MissionLog> <value> [pos=<n>]: the log id, the value, the position (-1: the whole status)", "6 14 2|6 70000 -1",
                  $"{steps[7].Value} {steps[7].Amount} {steps[7].Pos}|{steps[8].Value} {steps[8].Amount} {steps[8].Pos}");
            Check("  spell <id> after the login: a step", "spell|303", $"{steps[9].Verb}|{steps[9].Value}");
            Check("  currency <name> <n>: a step", "bayld 150000", $"{steps[10].Name} {steps[10].Amount}");
            Check("  bfwin: a step", "bfwin", steps[11].Label);
            // the '#' rule touches kill / claim only: a '#' after another verb still starts a comment
            var g = Flow.Parse("char zone=SouthernSanDoria pos=0,0,0\nlogin\ntrigger Kipopo #12 a comment\n", "c.flow", "c", null);
            Check("  '#<digit>' after trigger is still a comment", "trigger Kipopo", g.Steps[1].Label);
            Check("  a flow's hash: kill #<id> hashes its id (a different id, a different hash); a comment after it does not count", "True/True",
                  $"{Flow.Hash("kill #1\n") != Flow.Hash("kill #2\n")}/{Flow.Hash("kill #1\n") == Flow.Hash("kill #1   # x\n")}");
            var items = Flow.Parse("char zone=SouthernSanDoria pos=0,0,0\nitem 2012\nlogin\n", "r.flow", "r", null, null, id => id == 2012);
            Check("  an item id the resolver knows (item_basic) parses", "2012", items.Setup[0].Value.ToString());
        }
        catch (FormatException e)
        {
            Fail("  the P8 statements", e.Message);
        }
    }

    // the parse's refusals, by their messages
    private static void SelfTestP8Refusals()
    {
        const string sd = "char zone=SouthernSanDoria pos=0,0,0\n";
        P5Refused("effect 0", sd + "login\neffect 0\n", "t.flow:3: an effect is an xi.effect id, 1 to 813: 0");
        P5Refused("effect past MAX_EFFECTID", sd + "login\neffect 814\n", "t.flow:3: an effect is an xi.effect id, 1 to 813: 814");
        P5Refused("effect by name", sd + "login\neffect MANAFONT\n", "t.flow:3: not a number: MANAFONT");
        P5Refused("effect before the login", sd + "effect 47\nlogin\n", "t.flow:2: step before login");
        P5Refused("kill #<id> not a number", sd + "login\nkill #12a\n", "t.flow:3: kill #<id> takes a mob's id, a number from 1: #12a");
        P5Refused("claim #0", sd + "login\nclaim #0\n", "t.flow:3: claim #<id> takes a mob's id, a number from 1: #0");
        P5Refused("kill # (a comment: no mob)", sd + "login\nkill # 12\n", "t.flow:3: kill takes 1 arguments");
        P5Refused("npcstatus another status", sd + "login\nnpcstatus Kipopo invisible\n", "t.flow:3: npcstatus takes <Npc> normal or disappear: invisible");
        P5Refused("npcstatus an unknown NPC", sd + "login\nnpcstatus Nobody normal\n", "t.flow:3: unknown npc Nobody");
        P5Refused("missionstatus an unknown log", sd + "login\nmissionstatus Copp 14\n", "t.flow:3: unknown mission log Copp");
        P5Refused("missionstatus pos 8", sd + "login\nmissionstatus Cop 1 pos=8\n", "t.flow:3: a mission status position is 0 to 7: pos=8");
        P5Refused("missionstatus 16 at a position", sd + "login\nmissionstatus Cop 16 pos=2\n", "t.flow:3: a mission status at a position is 0 to 15 (4 bits): 16");
        P5Refused("missionstatus negative", sd + "login\nmissionstatus Cop -1\n", "t.flow:3: a mission status is 0 or more: -1");
        P5Refused("missionstatus with another key", sd + "login\nmissionstatus Cop 1 at=2\n", "t.flow:3: expected pos=<n>: at=2");
        P5Refused("spell 0", sd + "spell 0\nlogin\n", "t.flow:2: a spell is an xi.magic.spell id, 1 to 1023: 0");
        P5Refused("spell by name", sd + "spell IFRIT\nlogin\n", "t.flow:2: not a number: IFRIT");
        P5Refused("currency upper case", sd + "login\ncurrency Bayld 5\n", "t.flow:3: a currency is its lower-case name (bayld, cruor): Bayld");
        P5Refused("currency 0", sd + "login\ncurrency bayld 0\n", "t.flow:3: currency adds 1 or more: 0");
        P5Refused("currency before the login", sd + "currency bayld 5\nlogin\n", "t.flow:2: step before login");
        P5Refused("bfwin with an argument", sd + "login\nbfwin 226\n", "t.flow:3: bfwin takes 0 arguments");
        P5Refused("sjob without slvl", "char zone=SouthernSanDoria pos=0,0,0 sjob=WHM\nlogin\n", "t.flow:1: a support job takes sjob=<JOB> and slvl=<n>, both");
        P5Refused("slvl without sjob", "char zone=SouthernSanDoria pos=0,0,0 slvl=5\nlogin\n", "t.flow:1: a support job takes sjob=<JOB> and slvl=<n>, both");
        P5Refused("sjob the main job", "char zone=SouthernSanDoria pos=0,0,0 job=WAR sjob=WAR slvl=5\nlogin\n", "t.flow:1: the support job is another job than the main one: sjob=WAR");
        P5Refused("sjob unknown", "char zone=SouthernSanDoria pos=0,0,0 sjob=XYZ slvl=5\nlogin\n", "t.flow:1: unknown job XYZ");
        P5Refused("slvl 100", "char zone=SouthernSanDoria pos=0,0,0 sjob=WHM slvl=100\nlogin\n", "t.flow:1: a support job's level is 1 to 99: 100");
        P5Refused("item 0", sd + "item 0\nlogin\n", "t.flow:2: an item id is 1 to 65534 (65535 is the gil: gil=<n>): 0");
        P5Refused("item 65535 (the gil)", sd + "item 65535\nlogin\n", "t.flow:2: an item id is 1 to 65534 (65535 is the gil: gil=<n>): 65535");
        P5Refused("trade with an id that is not a number", sd + "login\ntrade Kipopo 20x1 x1\n", "t.flow:3: an item id is 1 to 65534 (65535 is the gil: gil=<n>): 20x1");
        try
        {
            Flow.Parse(sd + "item 2013\nlogin\n", "t.flow", "t", null, null, id => id == 2012);
            Fail("an item id with no item_basic row", "parsed");
        }
        catch (FormatException e)
        {
            Check("an item id with no item_basic row", "t.flow:2: no item 2013 (no item_basic row)", e.Message);
        }
    }

    // the run-time guards, through their pure parts
    private static void SelfTestP8Guards()
    {
        Check("  B10: a trigger in an open event: the error names the step, the event, its own reason (no client sends it) and the fix",
              "trigger Kipopo: event 910 is open, and a client sends no trigger while a cutscene is open (the engine would queue its event behind the open one, which no player can do): finish or update it first",
              EventOpenProblem("trigger Kipopo", "trigger", 910));
        // the check itself, on a char in an event and one out of it
        string Refused(int eventId)
        {
            var ch = new XI.Zone.CharData();
            ch.CurrentEvent.EventId = eventId;
            try
            {
                RefuseInEvent(new Statement { Verb = "trade", Label = "trade Kipopo 2012 x1" }, ch);
                return "-";
            }
            catch (InvalidOperationException e)
            {
                return e.Message;
            }
        }
        Check("  B10: a trade with no event open goes on; with one open it fails the run",
              "-|trade Kipopo 2012 x1: event 909 is open, and the engine drops a trade sent in an event (BlockedState.InEvent), so the step would record nothing: finish or update it first",
              $"{Refused(-1)}|{Refused(909)}");
        Check("  effect: quiet when the engine takes it, fails when it refuses",
              "-|effect 47: addStatusEffect returned false (the char has it, or one that overwrites it?)",
              $"{EffectProblem(47, "true") ?? "-"}|{EffectProblem(47, "false") ?? "-"}");
        Check("  currency: quiet when getCurrency rose by n; else it names the two reads",
              "-|currency bayld 5: getCurrency was 0, then 0 (an unknown currency, or its cap?)|currency bayld 5: getCurrency was 10, then 12 (an unknown currency, or its cap?)",
              $"{CurrencyProblem("bayld", 5, "10,15") ?? "-"}|{CurrencyProblem("bayld", 5, "0,0") ?? "-"}|{CurrencyProblem("bayld", 5, "10,12") ?? "-"}");
        Check("  item step: quiet when the count rose by n; one that adds nothing (or less) is warned of, " +
              "naming the reads",
              "-|item 2902 x1: the char held 1, then 1 (a Rare item the char holds, or a full bag?)|" +
              "item 4096 x12: the char held 0, then 5 (a Rare item the char holds, or a full bag?)",
              $"{ItemProblem(2902, 1, "0,1") ?? "-"}|{ItemProblem(2902, 1, "1,1") ?? "-"}|" +
              $"{ItemProblem(4096, 12, "0,5") ?? "-"}");
    }

    // replay a#x,b,c#y: the flows in the order given, a key's in name order, each once; a bad element refused by name
    private static void SelfTestP8Selector()
    {
        // all's order is name order (LoadFlows)
        var all = new List<string> { "a.one", "a.one#late", "a.two", "b.three#x", "b.three#y", "probe.z" };
        static string KeyOf(string n) => n.Split('#')[0];
        string Sel(string sel)
        {
            var chosen = SelectFlows(all, sel, n => n, KeyOf, out var bad, out var why);
            return chosen == null ? $"refused {bad}: {why}" : string.Join(",", chosen);
        }
        Check("  selector: all is every flow in name order", string.Join(",", all), Sel("all"));
        Check("  selector: one flow name", "a.one#late", Sel("a.one#late"));
        Check("  selector: one key is its flows in name order", "b.three#x,b.three#y", Sel("b.three"));
        Check("  selector: a list runs in the order given, not name order", "probe.z,b.three#y,a.one,a.one#late",
              Sel("probe.z,b.three#y,a.one"));
        Check("  selector: a flow named twice (by its name, then its key) runs once, at its first place", "b.three#y,a.two,b.three#x",
              Sel("b.three#y,a.two,b.three"));
        Check("  selector: the same name twice runs once", "a.two", Sel("a.two,a.two"));
        Check("  selector: an element that names no flow is refused by that element", "refused a.tow: no flow of that name",
              Sel("a.two,a.tow"));
        Check("  selector: an empty element is refused", "refused a.two,: an empty name in the list (a, b: no spaces, no comma at an end)",
              Sel("a.two,"));
        Check("  selector: all in a list is refused", "refused a.two,all: all runs alone, not in a list", Sel("a.two,all"));
        Check("  selector: a mode and one selector pass; a space-separated list is refused, not cut to its first key",
              "-|-|-|one selector only, not a.one b.three probe.z: join flow names and keys with commas, no spaces " +
              "(a.one,b.three,probe.z); an unknown option is refused too",
              $"{PositionalError(new string[0]) ?? "-"}|{PositionalError(new[] { "replay" }) ?? "-"}|" +
              $"{PositionalError(new[] { "replay", "a.one,b.three" }) ?? "-"}|" +
              $"{PositionalError(new[] { "replay", "a.one", "b.three", "probe.z" }) ?? "-"}");
        Check("  --fail-fast: the report names what ran, where it stopped and what did not run",
              "fail-fast: stopped at b.three#y, the first failure; ran 2 of 4 flows (a.one, b.three#y); not run 2 (a.two, probe.z); the coverage gate not run",
              FailFastSummary("b.three#y", new[] { "a.one", "b.three#y" }, new[] { "a.one", "b.three#y", "a.two", "probe.z" }));
        Check("  --fail-fast: a failed startup gate stops before any flow",
              "fail-fast: stopped at the startup gate, the first failure; ran 0 of 1 flows; not run 1 (a.two); the coverage gate not run",
              FailFastSummary("the startup gate", Array.Empty<string>(), new[] { "a.two" }));
    }
}
