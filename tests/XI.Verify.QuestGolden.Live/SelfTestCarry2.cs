using System;
using System.Collections.Generic;
using System.Linq;

// the carry-harness-2 fixes (TODO's golden harness rows the user ruled on, 2026-11-04): their pure parts, in the
// self-test (no database)
internal static partial class Program
{
    private static void SelfTestCarryHarness2()
    {
        Console.WriteLine("golden harness carries 2");
        SelfTestFlowSelector();
        SelfTestExpectErrorParse();
        SelfTestExpectedErrors();
        SelfTestDoubleFinish();
        SelfTestItemStacks();
    }

    // flow:<name> picks one flow; <key> stays every flow of the quest, the base flow (whose name is the key) included
    private static void SelfTestFlowSelector()
    {
        var all = new List<string> { "a.one", "a.one#late", "a.two", "b.three#x" };
        static string KeyOf(string n) => n.Split('#')[0];
        string Sel(string sel)
        {
            var chosen = SelectFlows(all, sel, n => n, KeyOf, out var bad, out var why);
            return chosen == null ? $"refused {bad}: {why}" : string.Join(",", chosen);
        }
        Check("  selector: a base flow's name is its key: every flow of the quest", "a.one,a.one#late", Sel("a.one"));
        Check("  selector: flow:<name> is that one flow, the base flow alone", "a.one", Sel("flow:a.one"));
        Check("  selector: flow:<name> of a branch flow is that flow", "a.one#late", Sel("flow:a.one#late"));
        Check("  selector: flow:<name> in a list, beside a key, each once at its first place",
              "a.one,b.three#x,a.one#late", Sel("flow:a.one,b.three,a.one"));
        Check("  selector: flow:<name> names a flow, not a key: a key with no base flow is refused",
              "refused flow:b.three: no flow of that name", Sel("flow:b.three"));
        Check("  selector: flow: with no name is refused", "refused flow:: flow: takes a flow's name (flow:<name>)",
              Sel("flow:"));
    }

    // <step> expect-error "<text>": a step's suffix, its text kept whole (a '#' in it too), in the step's label and
    // hash
    private static void SelfTestExpectErrorParse()
    {
        const string Head = "char zone=TheEldiemeNecropolis pos=0,0,0\nlogin\n";
        const string Kill = "kill STURM nokiller expect-error \"attempt to index local 'player' (a nil value)\"";
        try
        {
            var f = Flow.Parse(Head + Kill + " # the raise\nkill TAIFUN\n", "e.flow", "e", null);
            var st = f.Steps[1];
            Check("  expect-error: the step's text, its verb and words as without it, the suffix in its label",
                  "attempt to index local 'player' (a nil value)|kill|STURM|True|" + Kill + "|-",
                  $"{st.ExpectError}|{st.Verb}|{st.Name}|{st.NoKiller}|{st.Label}|{f.Steps[2].ExpectError ?? "-"}");
            var g = Flow.Parse(Head + "finish expect-error \"bad argument #2 (number expected, got nil)\" # done\n",
                               "e.flow", "e", null);
            Check("  expect-error: a '#' inside the text is no comment; the comment after it is",
                  "bad argument #2 (number expected, got nil)", g.Steps[1].ExpectError);
            Check("  expect-error: the suffix is in the flow's hash", "False",
                  (Flow.Hash(Head + "kill STURM nokiller\n") == Flow.Hash(Head + Kill + "\n")).ToString());
            var w = Flow.Parse("uncovered: 1/Sturm/onMobDeath -- unrecordable until an expect-error step\n" + Head,
                               "e.flow", "e", null);
            Check("  expect-error: the word in a waiver's reason is free text", "1", w.Waivers.Count.ToString());
            var b = Flow.Parse(Head + "trigger Sarcophagus expect-error \"a b\"\n", "e.flow", "e", null);
            Check("  expect-error: a text with spaces", "a b|trigger Sarcophagus expect-error \"a b\"",
                  $"{b.Steps[1].ExpectError}|{b.Steps[1].Label}");
            var a = Flow.Parse(Head + "charvar X 1 expect-error \"a\"\n", "e.flow", "e", null);
            Check("  expect-error: a GM step after the login takes it", "a", a.Steps[1].ExpectError);
        }
        catch (FormatException e)
        {
            Fail("  expect-error: the suffix parses", e.Message);
        }
        const string Form = "expect-error ends a step: <step> expect-error \"<text>\" (printable, no '\"')";
        P5Refused("  expect-error: no quotes", Head + "finish expect-error attempt\n", $"t.flow:3: {Form}");
        P5Refused("  expect-error: an empty text", Head + "finish expect-error \"\"\n", $"t.flow:3: {Form}");
        P5Refused("  expect-error: words after the text", Head + "trigger Sarcophagus expect-error \"a\" at=2\n",
                  $"t.flow:3: {Form}");
        P5Refused("  expect-error: alone on a line", Head + "expect-error \"a\"\n",
                  "t.flow:3: expect-error ends a step: it has none before it");
        P5Refused("  expect-error: on a setup line (a row, before the login)",
                  "char zone=TheEldiemeNecropolis pos=0,0,0\ncharvar X 1 expect-error \"a\"\nlogin\n",
                  "t.flow:2: expect-error ends a step, not a setup line: charvar before the login is a row");
        P5Refused("  expect-error: on a header line",
                  "lists TheEldiemeNecropolis/Sturm/onMobDeath expect-error \"a\"\n" + Head,
                  "t.flow:1: expect-error ends a step, not a header line: lists");
    }

    // the step's errors the expect-error expects: on the Lua side a script error holding the text; on the C# side a C#
    // handler's throw or that script error. Another step's error, or another error of the step, is not expected
    private static void SelfTestExpectedErrors()
    {
        const string Key = "crystalWar.her_memories_operation_cupid";
        const string LuaErr = "lua: onMobDeath: Sturm.lua:24: attempt to index local 'player' (a nil value)";
        const string Threw = "quests: " + Key + " onEventFinish 24: helpers.lua:121: bad argument #2";
        const string Other = "quests: crystalWar.other onEventFinish 24: helpers.lua:121: bad argument #2";
        var logged = new List<string>
        {
            "event: start", LuaErr, "lua: other: attempt to call a nil value", Threw, LuaErr, "zone: x", Other,
        };
        var threw = new HashSet<string> { Threw, Other };
        string Run(bool luaSide, string text, int from, int to)
        {
            var steps = new[] { new StepLog(9, "kill STURM", text, from, to) };
            var (expected, problems) = ExpectedErrors(logged, steps, luaSide, threw, Key);
            return $"{string.Join(",", expected.OrderBy(i => i))}|{string.Join(";", problems)}";
        }
        const string NoneLua = "9: [kill STURM] the step raised no error it expects (a script error holding the text)";
        const string NoneCs = "9: [kill STURM] the step raised no error it expects (a throw of a " + Key +
                              " handler (quests: " + Key + " <handler>: <message>) or a script error, " +
                              "holding the text)";
        Check("  expected errors, Lua side: the step's script error holding the text, not another, not a later step's",
              "1|", Run(true, "attempt to index local 'player'", 0, 4));
        Check("  expected errors, Lua side: a C# handler's throw is not a Lua error", "|" + NoneLua,
              Run(true, "helpers.lua", 3, 4));
        Check("  expected errors, C# side: a throw of the flow's quest whose message holds the text", "3|",
              Run(false, "bad argument #2", 3, 4));
        Check("  expected errors, C# side: the quest's throw without the text is a problem", "|" + NoneCs,
              Run(false, "no such text", 3, 4));
        Check("  expected errors, C# side: the text in the handler's name only is not in its message", "|" + NoneCs,
              Run(false, "onEventFinish 24", 3, 4));
        Check("  expected errors, C# side: another quest's throw holding the text is a problem", "|" + NoneCs,
              Run(false, "bad argument #2", 6, 7));
        Check("  expected errors, C# side: the same script error (a patched script keeping the raise)", "4|",
              Run(false, "attempt to index local 'player'", 4, 6));
        Check("  expected errors, C# side: a script error without the text and no throw is a problem", "|" + NoneCs,
              Run(false, "attempt to index local 'player'", 2, 3));
        Check("  expected errors: a step that logged nothing is a problem", "|" + NoneLua, Run(true, "x", 5, 5));
        var lines = new[]
        {
            "event: start", LuaErr, "[lua] Error running handler: x", "a luautils::b", "lua: the global scripts loaded",
        };
        Check("  script errors: the predicate as before", "False|True|True|True|False",
              string.Join("|", lines.Select(l => IsScriptError(l).ToString())));
    }

    // one dispatch's double finish: the bridge's line, its declaration by a warning: line, and the Lua side (a record)
    // not needing it of the quest it turned off
    private static void SelfTestDoubleFinish()
    {
        var two = new[] { "2/-/onEventFinish:4", "3/-/onEventFinish:4" };
        string line = XI.Zone.ZoneHost.DoubleFinishLine("crystalWar.seeing_spots", 4, two);
        Check("  double finish: the bridge's line",
              "quests: crystalWar.seeing_spots: one finish of 4 ran 2 handlers " +
              "(2/-/onEventFinish:4, 3/-/onEventFinish:4)",
              line);
        string ten = XI.Zone.ZoneHost.DoubleFinishLine("a.b", 1, Enumerable.Repeat("1/-/onEventFinish:1", 10).ToList());
        string one = "quests: a.b: one finish of 4 ran 1 handlers (x)";
        Check("  double finish: the line's key; one handler is no double finish; ten are",
              "crystalWar.seeing_spots|-|a.b",
              $"{Flow.DoubleFinishKey(line)}|{Flow.DoubleFinishKey(one) ?? "-"}|{Flow.DoubleFinishKey(ten) ?? "-"}");
        try
        {
            var f = Flow.Parse($"warning: {line} -- the Lua's two sections hold it\n" +
                               "char zone=NorthernSanDoria pos=0,0,0\nlogin\n", "d.flow", "d", null);
            Check("  double finish: a warning: line declares it", line, f.Warnings.Single().Text);
        }
        catch (FormatException e)
        {
            Fail("  double finish: a warning: line declares it", e.Message);
        }
        // a declared engine warning beside it: RequiredWarnings keeps every line that is no double finish
        string engine = "lua: AddItem: Item <0> is not found in a database";
        var declared = new[] { line, engine };
        Check("  double finish: a record (Lua side) of that quest does not need it; a replay (nothing off) does; " +
              "another quest's record does",
              $"{engine}|{line};{engine}|{line};{engine}",
              $"{string.Join(";", RequiredWarnings(declared, new[] { "crystalWar.seeing_spots" }))}|" +
              $"{string.Join(";", RequiredWarnings(declared, Array.Empty<string>()))}|" +
              $"{string.Join(";", RequiredWarnings(declared, new[] { "a.b" }))}");
        var none = Array.Empty<string>();
        Check("  double finish: declared, it is no quests: problem; not declared, it is", $"|{line}",
              $"{string.Join(";", QuestLineProblems(new[] { line }, new[] { line }, none))}|" +
              $"{string.Join(";", QuestLineProblems(new[] { line }, none, none))}");
    }

    // setup item rows: split by the stack size, a second Rare row refused, the exempt list's lines as before
    private static void SelfTestItemStacks()
    {
        // 4096 stacks 12; 1000 is Rare (stacks 1); 2000 stacks 1
        static (int, bool) Info(int item) => item switch { 4096 => (12, false), 1000 => (1, true), _ => (1, false) };
        Flow Parse(string body) =>
            Flow.Parse("char zone=NorthernSanDoria pos=0,0,0\n" + body + "login\n", "s.flow", "s", null);
        string Rows(Flow f, Func<Statement, bool> exempt) => string.Join(",",
            ItemRows(SetupItems(f, 0), Info, exempt, null).Select(r => $"{r.Item}x{r.Qty}@{r.Source.Line}"));
        const string Stale = "is stale: that line is no setup item line above its stack size or a second Rare row " +
                             "(drop the entry)";
        try
        {
            var f = Parse("item 4096 x30\nitem 2000 x3\nitem 4096 x12\n");
            Check("  item rows: x<n> above the stack split into full stacks and the rest; at the stack one row",
                  "4096x12@2,4096x12@2,4096x6@2,2000x1@3,2000x1@3,2000x1@3,4096x12@4", Rows(f, _ => false));
            Check("  item rows: an exempt line keeps its one row of n",
                  "4096x30@2,2000x1@3,2000x1@3,2000x1@3,4096x12@4", Rows(f, st => st.Line == 2));
            var issues = new List<ItemStackIssue>();
            var rare = Parse("item 1000\nitem 4096 x5\nitem 1000\nitem 1000 x2\n");
            ItemRows(SetupItems(rare, 0), Info, _ => false, issues);
            Check("  item rows: the issues, a Rare item's second rows (a second line, x2) and x<n> above the stack",
                  "4:False:True,5:True:True",
                  string.Join(",", issues.Select(i => $"{i.Source.Line}:{i.OverStack}:{i.SecondRare}")));
            var problems = new List<string>();
            var exempt = ParseItemStackExempt("# the list\n\ns:3 item 2000 x3  # x3 above the stack size 1\n" +
                                              "s:9 item 1000\nbad\ns:3 item 2000 x3\n", problems);
            Check("  exempt list: entries without their comments; a malformed line and a second entry refused",
                  "s:3=item 2000 x3,s:9=item 1000|item-stack-exempt.txt:5: an entry is <flow>:<line> <statement>: " +
                  "bad;item-stack-exempt.txt:6: a second entry for s:3",
                  $"{string.Join(",", exempt.Select(kv => $"{kv.Key}={kv.Value}"))}|{string.Join(";", problems)}");
            var g = Parse("item 4096 x30\nitem 2000 x3\nitem 1000\nitem 1000\n");
            g.File = "s.flow";
            var list = new Dictionary<string, string> { ["s:3"] = "item 2000 x3", ["s:5"] = "item 1000" };
            Check("  item gate: exempt lines pass; a split line above the stack (not exempt) passes",
                  "", string.Join(";", ItemStackProblems(new[] { g }, Info, list)));
            list.Remove("s:5");
            Check("  item gate: a second Rare row not exempt fails",
                  "s.flow:5: [item 1000] a second row of a Rare item (the game holds one): give it once",
                  string.Join(";", ItemStackProblems(new[] { g }, Info, list)));
            list["s:5"] = "item 1000";
            list["s:2"] = "item 4096 x30";
            list["s:4"] = "item 1000 x1";
            list["t:1"] = "item 1000";
            Check("  item gate: an entry naming another statement of its line, or no flow, is stale",
                  $"item-stack-exempt.txt: s:4 [item 1000 x1] {Stale};item-stack-exempt.txt: t:1 [item 1000] {Stale}",
                  string.Join(";", ItemStackProblems(new[] { g }, Info, list)));
            list.Remove("s:4");
            list.Remove("t:1");
            list["s:4"] = "item 1000";
            Check("  item gate: an entry whose line breaks no rule (the first Rare row) is stale",
                  $"item-stack-exempt.txt: s:4 [item 1000] {Stale}",
                  string.Join(";", ItemStackProblems(new[] { g }, Info, list)));
            var h = Parse("item 4096 x30\nitem 1000\nitem 1000\nitem 2000 x3\nitem 1000\n");
            var old = new Dictionary<string, string>
            {
                ["s:1"] = "item 4096 x30", ["s:3"] = "item 1000", ["s:9"] = "item 7 x1",
            };
            var failures = new List<string>();
            var notes = new List<string>();
            var entries = RekeyItemStackExempt(new[] { h }, Info, old, failures, notes);
            Check("  item-stacks: entries re-keyed by flow and statement (lines moved), the list not grown",
                  "s:2 item 4096 x30  # x30 above the stack size 12;s:4 item 1000  # a second row of a Rare item",
                  string.Join(";", entries));
            Check("  item-stacks: a second Rare row on no entry fails (the list never grows)",
                  "s:6 [item 1000]: a second row of a Rare item on no entry of the list (the list never grows: " +
                  "give the item once)", string.Join(";", failures));
            Check("  item-stacks: the moves, a new line above the stack (split, no entry) and a dropped entry noted",
                  "s:1 -> s:2 [item 4096 x30]: re-keyed (its line moved);s:3 -> s:4 [item 1000]: re-keyed (its line " +
                  "moved);s:5 [item 2000 x3]: x3 above the stack size 1, on no entry: split by the stack size;" +
                  "s:9 [item 7 x1]: no line breaks a rule there any more: dropped", string.Join(";", notes));
        }
        catch (FormatException e)
        {
            Fail("  item rows: the flows parse", e.Message);
        }
    }
}
