using System;
using System.Linq;

// quest rework P9: an onTriggerAreaLeave handler's label (waivers) and the harness honesty items (D1-D4, D6, D7), in the
// self-test (no database). The bridge's label of the kind is in SelfTestP7's every-kind quest; the probe
// (probe.trigger_area_leave) replays the kind live
internal static partial class Program
{
    private static void SelfTestPlatformP9()
    {
        Console.WriteLine("quest rework P9 hooks");
        SelfTestP9LeaveLabel();
        Console.WriteLine("quest rework P9: harness honesty");
        SelfTestP9Kill();
        SelfTestP9Warnings();
        SelfTestP9Stable();
    }

    // a waiver may name an onTriggerAreaLeave handler (its label n/-/onTriggerAreaLeave:<area id>), as an enter's
    private static void SelfTestP9LeaveLabel()
    {
        try
        {
            var f = Flow.Parse("uncovered: 2/-/onTriggerAreaLeave:1 -- never left\nuncovered: 2/-/onTriggerAreaLeave:2@CeizakBattlegrounds -- likewise\n" +
                               "char zone=BastokMarkets pos=0,0,0\nlogin\n", "w.flow", "w", null);
            Check("  uncovered: <n>/-/onTriggerAreaLeave:<id>[@<Zone>]: a handler label", "2/-/onTriggerAreaLeave:1|2/-/onTriggerAreaLeave:2@CeizakBattlegrounds",
                  string.Join("|", f.Waivers.Select(w => w.Label)));
        }
        catch (FormatException e)
        {
            Fail("  uncovered: <n>/-/onTriggerAreaLeave:<id>", e.Message);
        }
        P5Refused("uncovered: an onTriggerAreaLeave without its area id is no label", "uncovered: 2/-/onTriggerAreaLeave -- x\nchar zone=BastokMarkets pos=0,0,0\nlogin\n",
                  "t.flow:1: a handler label is <n>/<entity>/<trigger>: 2/-/onTriggerAreaLeave");
    }

    // ---- Part D: harness honesty ----

    // D1 / D4: a claim that does not take fails the step, with the reason; D2: kill <Mob> nokiller; D3: a trade from past
    // the engine's 6.0 yalms fails the step
    private static void SelfTestP9Kill()
    {
        Check("  claim: taken: no problem", "null", Harness9(Program.ClaimProblem(17, 9, 9, true, false)));
        Check("  claim: nobody's after 30 s: the step fails (a kill would die unclaimed)",
              "claim 17: the char's swing did not claim it in 30 s (out of reach? a kill would die unclaimed, with no killer)",
              Harness9(Program.ClaimProblem(17, 0, 9, true, false)));
        Check("  claim: not spawned / dead / another's: each its reason",
              "claim 17: the mob is not spawned (pop it first)|claim 17: the mob is dead|claim 17: claimed by 5, not the char",
              $"{Program.ClaimProblem(17, 0, 9, false, false)}|{Program.ClaimProblem(17, 0, 9, true, true)}|{Program.ClaimProblem(17, 5, 9, true, false)}");
        Check("  kill nokiller: an unclaimed living mob: no problem; claimed: refused (its death would have a killer)",
              "null|kill 17 nokiller: claimed by 5: its death would have a killer|kill 17 nokiller: the mob is not spawned (pop it first)",
              $"{Harness9(Program.NoKillerProblem(17, 0, true, false))}|{Program.NoKillerProblem(17, 5, true, false)}|{Program.NoKillerProblem(17, 0, false, false)}");
        Check("  trade: 6.0 yalms is in range, past it the step fails",
              "null|trade 3: the char stands 6.01 yalms off, past the engine's 6.0: it would drop the trade (trigger the NPC first, or walk)",
              $"{Harness9(Program.TradeRangeProblem(3, 6.0f))}|{Program.TradeRangeProblem(3, 6.01f)}");
        Check("  swing reach: the char's hitbox (tenths) + 2 + the mob's (CCharEntity::CanAttack)", "2.8|6.3",
              $"{Program.SwingReach(4, 0.4f):0.0}|{Program.SwingReach(8, 3.5f):0.0}");
        Check("  swing again (D4): not engaged; past the reach; out of the char's front; else not", "True|True|True|False|False",
              $"{Program.SwingAgain(false, 1f, 4f, true)}|{Program.SwingAgain(true, 4.01f, 4f, true)}|{Program.SwingAgain(true, 1f, 4f, false)}|" +
              $"{Program.SwingAgain(true, 4f, 4f, true)}|{Program.SwingAgain(true, 1f, 4f, true)}");
        const string Sd = "char zone=TheEldiemeNecropolis pos=0,0,0\n";
        try
        {
            var f = Flow.Parse(Sd + "login\nkill STURM nokiller\nkill STURM\nclaim STURM\n", "k.flow", "k", null);
            Check("  kill <Mob> nokiller parses (NoKiller), plain kill and claim do not set it", "STURM/True,STURM/False,STURM/False",
                  string.Join(",", f.Steps.Where(s => s.Verb is "kill" or "claim").Select(s => $"{s.Name}/{s.NoKiller}")));
        }
        catch (FormatException e)
        {
            Fail("  kill <Mob> nokiller parses", e.Message);
        }
        P5Refused("kill <Mob> <other word>", Sd + "login\nkill STURM killer\n", "t.flow:3: kill takes <Mob> [nokiller]: killer");
        P5Refused("claim <Mob> nokiller (a claim has a killer by its nature)", Sd + "login\nclaim STURM nokiller\n", "t.flow:3: claim takes 1 arguments");
        Check("  a flow's hash: nokiller counts", "True", (Flow.Hash("kill STURM\n") != Flow.Hash("kill STURM nokiller\n")).ToString());
    }

    // D6: warning: <engine warning> -- <reason>: parsed, refused for a line no engine warning matches, out of the hash;
    // a declared warning the run did not log is a problem
    private static void SelfTestP9Warnings()
    {
        const string Line = "lua: AddItem: Item <0> is not found in a database";
        try
        {
            var f = Flow.Parse($"warning: {Line} -- 709 option 0: addItem(0)\nchar zone=NorthernSanDoria pos=0,0,0\nlogin\n", "w.flow", "w", null);
            Check("  warning: <line> -- <reason>: the line and the reason kept", $"{Line}|709 option 0: addItem(0)",
                  string.Join(",", f.Warnings.Select(x => $"{x.Text}|{x.Reason}")));
        }
        catch (FormatException e)
        {
            Fail("  warning: <line> -- <reason>", e.Message);
        }
        P5Refused("warning: an error line (no engine warning matches it)",
                  "warning: lua: getQuestStatus: questLogID 9 or QuestID 9 is invalid -- x\nchar zone=NorthernSanDoria pos=0,0,0\nlogin\n",
                  "t.flow:1: not an engine warning (an error stays an error): lua: getQuestStatus: questLogID 9 or QuestID 9 is invalid");
        P5Refused("warning: with no reason", $"warning: {Line}\nchar zone=NorthernSanDoria pos=0,0,0\nlogin\n",
                  "t.flow:1: warning: takes <log line> -- <reason>");
        P5Refused("warning: after login", $"char zone=NorthernSanDoria pos=0,0,0\nlogin\nwarning: {Line} -- x\n", "t.flow:3: warning: after login");
        Check("  a flow's hash: a warning: line does not count", "True",
              (Flow.Hash("login\n") == Flow.Hash($"warning: {Line} -- x\nlogin\n")).ToString());
        Check("  a declared warning the run logged: no problem; one it did not: stale", $"|warning: {Line}: declared, but the run did not log it",
              $"{string.Join(";", Program.WarningProblems(new[] { Line }, new[] { Line, "lua: x" }))}|{string.Join(";", Program.WarningProblems(new[] { Line }, new[] { "lua: x" }))}");
        SelfTestQuestLineWarnings();
    }

    // warning: <a quests: line>: only a double finish parses (the kept-bug list went with Sharpening the Sword's
    // check 4 fix, user ruling 2026-11-05, kept bugs): that check's former line and a throw's line are refused; a
    // quests: line the run logs is a problem once, the benign ones stay benign; a declared double finish lets no other
    // quests: line through; a declared double finish not logged is stale
    private static void SelfTestQuestLineWarnings()
    {
        const string Former = "quests: sandoria.sharpening_the_sword check 4: unknown quest constant 0/A_BOY_S_DREAM";
        const string Threw = "quests: bastok.brygid_the_stylist_returns 2/Brygid/onTrade: optionToItems[0] is nil";
        const string Sd = "char zone=NorthernSanDoria pos=0,0,0\nlogin\n";
        string Refusal(string line) =>
            $"t.flow:1: not a double finish quests: line (ZoneHost.DoubleFinishLine; an error stays an error): {line}";
        P5Refused("warning: the former kept-bug line (Sharpening the Sword's check 4) is no double finish",
                  $"warning: {Former} -- x\n{Sd}", Refusal(Former));
        P5Refused("warning: a C# handler's throw is no double finish", $"warning: {Threw} -- x\n{Sd}", Refusal(Threw));
        var none = Array.Empty<string>();
        var logged = new[] { Former, Former, "quests: 13 quest keys" };
        Check("  a quests: line the run logs: a problem, once; the benign ones stay benign", Former,
              string.Join(";", Program.QuestLineProblems(logged, none, none)));
        var two = new[] { "2/-/onEventFinish:4", "3/-/onEventFinish:4" };
        string twice = XI.Zone.ZoneHost.DoubleFinishLine("a.b", 4, two);
        Check("  naming a double finish lets no other quests: line through (the benign ones stay benign)", Former,
              string.Join(";", Program.QuestLineProblems(new[] { twice, Former, "quests: 13 quest keys" },
                                                         new[] { twice }, none)));
        Check("  a declared double finish the run did not log: stale",
              $"warning: {twice}: declared, but the run did not log it",
              string.Join(";", Program.WarningProblems(new[] { twice }, new[] { "quests: 13 quest keys" })));
    }

    // D7: a stable child records with stable's --lua-at; a rerecord child does not take it
    private static void SelfTestP9Stable()
    {
        Check("  stable's children: record gets --lua-at, rerecord not, none without it",
              "record k --out d --lua-at abc|rerecord k --out d|record k --out d",
              $"{string.Join(" ", Program.StableChildArgs("record", "k", "d", "abc"))}|{string.Join(" ", Program.StableChildArgs("rerecord", "k", "d", "abc"))}|" +
              string.Join(" ", Program.StableChildArgs("record", "k", "d", null)));
    }

    private static string Harness9(string s) => s ?? "null";
}
