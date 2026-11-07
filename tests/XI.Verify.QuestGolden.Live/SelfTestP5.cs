using System;
using System.Linq;

// quest rework P5: the flow statements the San d'Oria, Windurst and Jeuno drafts needed (npclocalvar, setting <NAME>
// <decimal>, login / rezone / warp ... open), in the self-test (no database)
internal static partial class Program
{
    private static void SelfTestPlatformP5()
    {
        Console.WriteLine("quest rework P5 statements");
        SelfTestP5NpcLocalVar();
        SelfTestP5DecimalSetting();
        SelfTestP5ZoneInOpen();
        SelfTestP5AfterZoneIn();
        SelfTestP5TwinGuard();
    }

    // afterZoneIn: a waiver may name an afterZoneIn handler (its label n/-/afterZoneIn). probe.after_zone_in's twin (One
    // Good Deed?'s Boneyard Gully handlers) is gone: the probe replays against the port, OneGoodDeed.cs (B3b Windurst
    // group 11)
    private static void SelfTestP5AfterZoneIn()
    {
        try
        {
            var f = Flow.Parse("uncovered: 2/-/afterZoneIn -- the zone-in is never 4 s old\nuncovered: 2/-/afterZoneIn@BoneyardGully -- likewise\n" +
                               "char zone=BastokMarkets pos=0,0,0\nlogin\n", "w.flow", "w", null);
            Check("  uncovered: <n>/-/afterZoneIn[@<Zone>]: a handler label", "2/-/afterZoneIn|2/-/afterZoneIn@BoneyardGully",
                  string.Join("|", f.Waivers.Select(w => w.Label)));
        }
        catch (FormatException e)
        {
            Fail("  uncovered: <n>/-/afterZoneIn", e.Message);
        }
        P5Refused("uncovered: an afterZoneIn with an id is no label", "uncovered: 2/-/afterZoneIn:1 -- x\nchar zone=BastokMarkets pos=0,0,0\nlogin\n",
                  "t.flow:1: a handler label is <n>/<entity>/<trigger>: 2/-/afterZoneIn:1");
    }

    // the twin guard: a flow that names a twin under a name Twins.cs lacks, and a registered twin no handler answered
    private static void SelfTestP5TwinGuard()
    {
        // on probe.zone_in_table, which still lists a twin (probe.after_zone_in's is gone: B3b Windurst group 11; Such
        // Sweet Sorrow's: B3b Aht Urhgan group 10)
        string bigBox = TwinsOf("probe.zone_in_table", Array.Empty<string>()).Select(TwinKey).FirstOrDefault() ?? "?";
        var ok = TwinProblems("probe.zone_in_table", true, new[] { bigBox }, new[] { bigBox });
        var renamed = TwinProblems("probe.zone_in_table_copy", true, Array.Empty<string>(), Array.Empty<string>());
        var silent = TwinProblems("probe.zone_in_table", true, new[] { bigBox }, Array.Empty<string>());
        var plain = TwinProblems("probe.party", false, Array.Empty<string>(), Array.Empty<string>());
        Check("  twin guard: quiet when answered or not expected; fires for a renamed flow and for a twin nobody answered",
              "0|1|1|0", $"{ok.Count}|{renamed.Count}|{silent.Count}|{plain.Count}");
        // the twins a run registers (TwinsOf): a probe's twin while no C# quest has its key, left out once a C# quest
        // has its key (how probe.after_zone_in's twin went: B3b Windurst group 11), none for a probe without twins
        var listed = TwinsOf("probe.zone_in_table", Array.Empty<string>()).Select(TwinKey).ToList();
        var left = TwinsOf("probe.zone_in_table", listed).Count;
        Check("  probe.zone_in_table's twin: I'll Take the Big Box's while no C# quest has its key; " +
              "none once it ships; none for probe.party",
              "outlands.i_ll_take_the_big_box|0|0",
              $"{string.Join(",", listed)}|{left}|{TwinsOf("probe.party", Array.Empty<string>()).Count}");
        try
        {
            var f = Flow.Parse("# copied twin\nchar zone=BastokMarkets pos=0,0,0\nlogin\n", "c.flow", "probe.zone_in_table_copy", null);
            Check("  a flow naming a twin is marked", "True", f.MentionsTwin.ToString());
            var w = Flow.Parse("# One Good Deed? in Port Windurst\nchar zone=PortWindurst pos=0,0,0\nlogin\n", "w.flow", "probe.after_zone_in", null);
            Check("  a flow whose zone names hold \"twin\" (PortWindurst) is not marked", "False", w.MentionsTwin.ToString());
            var c = Flow.Parse("# copied from IllTakeTheBigBoxTwin\n" +
                               "char zone=BastokMarkets pos=0,0,0\nlogin\n", "k.flow",
                               "probe.zone_in_table_copy", null);
            Check("  a flow naming only a twin class (IllTakeTheBigBoxTwin) is marked", "True",
                  c.MentionsTwin.ToString());
        }
        catch (FormatException e)
        {
            Fail("  twin guard flow", e.Message);
        }
        P5Refused("localvar past int32 is out of range, not a number", "char zone=BastokMarkets pos=0,0,0\nlogin\nlocalvar x 3000000000\n",
                  "t.flow:3: a local var value is out of range (int32 here, 2147483647 at most): 3000000000");
    }

    // login open, rezone open, warp <x>,<y>,<z> <Zone> open: the zone-in's events are left open (no 0x05B), so update /
    // finish steps drive them; without open, as before (each answered with the option, 0 when not given)
    private static void SelfTestP5ZoneInOpen()
    {
        const string head = "char zone=BastokMarkets pos=0,0,0\nchar bob zone=BastokMarkets pos=0,0,0\n";
        try
        {
            var f = Flow.Parse(head + "login open\nrezone open\nwarp 1,2,3 Metalworks open\nrezone\nrezone option=2\nwarp 1,2,3 BastokMarkets option=1\nlogin\nbob: login open\n",
                               "o.flow", "o", null);
            Check("  login / rezone / warp ... open: the zone-in left open; without it as before (the option, none when not given)",
                  "login True -|rezone True -|warp True -|rezone False -|rezone False 2|warp False 1|login False -|login True -",
                  string.Join("|", f.Steps.Select(st => $"{st.Verb} {st.Open} {st.Option?.ToString() ?? "-"}")));
            Check("  ... the label as written, the warp's place and zone kept", "warp 1,2,3 Metalworks open|1,2,3 in " + XI.QuestContent.Zone.Metalworks + "|bob: login open",
                  $"{f.Steps[2].Label}|{f.Steps[2].X},{f.Steps[2].Y},{f.Steps[2].Z} in {f.Steps[2].ZoneId}|{f.Steps[7].Label}");
        }
        catch (FormatException e)
        {
            Fail("  login / rezone / warp ... open", e.Message);
        }
        const string one = "char zone=BastokMarkets pos=0,0,0\n";
        P5Refused("rezone open with an option", one + "login\nrezone open option=1\n", "t.flow:3: rezone takes 0 to 1 arguments");
        P5Refused("warp open with an option", one + "login\nwarp 1,2,3 Metalworks option=1 open\n", "t.flow:3: warp takes 2 to 3 arguments");
        P5Refused("rezone with another word", one + "login\nrezone opened\n", "t.flow:3: expected option=<n>: opened");
        P5Refused("login with an option (a login answers with 0, or open)", one + "login option=1\n", "t.flow:2: login takes nothing or open: option=1");
        P5Refused("login with two words", one + "login open open\n", "t.flow:2: login takes 0 to 1 arguments");
        P5Refused("logout open", one + "login\nlogout open\n", "t.flow:3: logout takes 0 arguments");
        // a login open's packets are its char's login bundle, as a login's
        Check("  LoginOf: login open and <name>: login open are a login (one bundle), a rezone open none",
              "main|main|bob|bob|-|-|-", string.Join("|", new[] { "login", "login open", "bob: login", "bob: login open", "rezone open", "login opened", "bob: login open x" }
                  .Select(l => TraceStep.LoginOf(l) ?? "-")));
        // show finds the steps from main's first login, open or not
        var shown = ShowText("# golden trace: t\nstep login open\npackets\nret \nstep trigger Zon-Fobun\npackets\nret \n",
                             "char zone=BastokMarkets pos=0,0,0\nlogin open   # a comment\ntrigger Zon-Fobun\n", new PacketStore(), false);
        Check("  show: a flow that opens with login open maps its steps to their lines", "step 1  flow:2|step 2  flow:3",
              string.Join("|", shown.Split('\n').Where(l => l.StartsWith("step ")).Select(l => string.Join("  ", l.Split("  ").Take(2)))));
    }

    // a flow that must not parse, and the message it fails with
    private static void P5Refused(string what, string text, string message)
    {
        try
        {
            Flow.Parse(text, "t.flow", "t", null);
            Fail(what, "parsed");
        }
        catch (FormatException e)
        {
            Check(what, message, e.Message);
        }
    }

    // npclocalvar <Npc> <name> <n>: a GM's GetNPCByID(id):setLocalVar(name, n), the NPC named as trigger names it
    private static void SelfTestP5NpcLocalVar()
    {
        const string jugner = "char zone=JugnerForest pos=0,0,0\n";
        const string eldieme = "char zone=TheEldiemeNecropolis pos=0,0,0\n";
        try
        {
            var f = Flow.Parse(jugner + "login\nnpclocalvar qm1 Wait 1800000000\nnpclocalvar qm1 Wait 0\n", "n.flow", "n", null);
            Check("  npclocalvar <Npc> <name> <n>: a step, the NPC, the var's name and value, the label as written",
                  "npclocalvar qm1 0 False Wait 1800000000 npclocalvar qm1 Wait 1800000000|npclocalvar qm1 0 False Wait 0 npclocalvar qm1 Wait 0",
                  string.Join("|", f.Steps.Skip(1).Select(st => $"{st.Verb} {st.Name} {st.Offset} {st.NpcKey} {st.Var} {st.Amount} {st.Label}")));
            var e = Flow.Parse(eldieme + "login\nnpclocalvar Brazier+2 Lit 1\nnpclocalvar SARCOPHAGUS_OFFSET+3 Open 1\nnpclocalvar BRAZIER Lit 2\n", "e.flow", "e", null);
            Check("  npclocalvar <Npc>+<n>, <KEY>[+<n>]: the name or key and the offset, as trigger takes them",
                  "Brazier 2 False Lit 1|SARCOPHAGUS_OFFSET 3 True Open 1|BRAZIER 0 True Lit 2",
                  string.Join("|", e.Steps.Skip(1).Select(st => $"{st.Name} {st.Offset} {st.NpcKey} {st.Var} {st.Amount}")));
        }
        catch (FormatException e)
        {
            Fail("  npclocalvar <Npc> <name> <n>", e.Message);
        }
        P5Refused("npclocalvar before the login", jugner + "npclocalvar qm1 Wait 1\nlogin\n", "t.flow:2: step before login");
        P5Refused("npclocalvar without a value", jugner + "login\nnpclocalvar qm1 Wait\n", "t.flow:3: npclocalvar takes 3 arguments");
        P5Refused("npclocalvar with a value that is not a number", jugner + "login\nnpclocalvar qm1 Wait soon\n", "t.flow:3: not a number: soon");
        P5Refused("npclocalvar with a negative value (setLocalVar takes a uint32)", jugner + "login\nnpclocalvar qm1 Wait -1\n",
                  "t.flow:3: a local var is 0 or more (setLocalVar takes a uint32): -1");
        P5Refused("npclocalvar of an NPC not in the char's zone", jugner + "login\nnpclocalvar Zon-Fobun Wait 1\n", "t.flow:3: unknown npc Zon-Fobun");
        P5Refused("npclocalvar <Npc>+0", eldieme + "login\nnpclocalvar Brazier+0 Lit 1\n",
                  "t.flow:3: npclocalvar takes <Npc>, <Npc>+<n>, <KEY> or <KEY>+<n>, n from 1: Brazier+0");
        P5Refused("npclocalvar with a name prefix of an unknown char", jugner + "login\nbob: npclocalvar qm1 Wait 1\n", "t.flow:3: unknown char bob");
    }

    // setting <NAME> <decimal>: a fractional value (GIL_RATE 1.5), written into Lua invariantly; a whole number as before
    private static void SelfTestP5DecimalSetting()
    {
        const string head = "char zone=BastokMarkets pos=0,0,0\n";
        try
        {
            var f = Flow.Parse(head + "setting GIL_RATE 1.5\nsetting EXP_RATE 2\nsetting ENABLE_COP true\nlogin\nsetting GIL_RATE -0.25\nsetting GIL_RATE 0.125\n", "s.flow", "s", null);
            var all = f.Setup.Concat(f.Steps.Skip(1)).ToList();
            Check("  setting <NAME> <decimal>: the kind and the Lua value written (invariant: 1.5, never 1,5); a whole number and a boolean as before",
                  "number 1.5|number 2|bool true|number -0.25|number 0.125",
                  string.Join("|", all.Select(st => $"{st.Status} {SettingLuaValue(st)}")));
            Check("  ... a whole number keeps its Amount, a decimal has none (0)", "0 2 1 0 0", string.Join(" ", all.Select(st => st.Amount)));
            var saved = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                // a comma culture: the value is still read and written with a point
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                var g = Flow.Parse(head + "setting GIL_RATE 1.5\nlogin\n", "g.flow", "g", null);
                Check("  ... under a comma culture (de-DE): parsed and written with a point", "1.5", SettingLuaValue(g.Setup[0]));
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = saved;
            }
        }
        catch (FormatException e)
        {
            Fail("  setting <NAME> <decimal>", e.Message);
        }
        P5Refused("a setting with two points", head + "setting GIL_RATE 1.5.2\nlogin\n", "t.flow:2: not a number: 1.5.2");
        P5Refused("a setting with a comma", head + "setting GIL_RATE 1,5\nlogin\n", "t.flow:2: not a number: 1,5");
        P5Refused("a setting with an exponent", head + "setting GIL_RATE 1e3\nlogin\n", "t.flow:2: not a number: 1e3");
        P5Refused("a setting that is NaN", head + "setting GIL_RATE NaN\nlogin\n", "t.flow:2: not a number: NaN");
        P5Refused("a setting that is Infinity", head + "setting GIL_RATE Infinity\nlogin\n", "t.flow:2: not a number: Infinity");
    }
}
