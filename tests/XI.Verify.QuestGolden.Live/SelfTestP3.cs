using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using XI.Map;

// quest rework P3: more chars in a flow and a party (char <name> lines, <name>: steps, invite / accept / decline,
// option=$<name>), the char ids each flow takes from the pool, the trace's other chars (packets <name>, <name>:<table>,
// the char placeholders) and every checked-in trace read and written back unchanged, in the self-test (no database)
internal static partial class Program
{
    private static void SelfTestPlatformP3()
    {
        Console.WriteLine("quest rework P3: more chars and a party");
        SelfTestP3Grammar();
        SelfTestP3Ids();
        SelfTestP3Trace();
        SelfTestP3Placeholders();
        SelfTestP3CheckedIn();
    }

    private static void SelfTestP3Refused(string what, string text, string message)
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

    private static void SelfTestP3Grammar()
    {
        const string head = "char zone=WestRonfaure pos=-258.5,-71,423.4 nation=0\n";
        const string two = head + "char bob zone=WestRonfaure pos=-261,-71,421 nation=2 race=2 job=WAR lvl=20 gil=5\n";
        try
        {
            var f = Flow.Parse(two + "char carl zone=EastRonfaure pos=1,2,3\nitem CopperIngot\nbob: item BronzeIngot x2\nlogin\nbob: keyitem Cursepaper\n" +
                               "bob: login\ninvite bob\nbob: accept\nbob: trigger Vilatroire\nupdate 129 option=$bob\nfinish option=$main\nmain: finish\n" +
                               "bob: item CopperIngot\ncarl: login\ninvite carl\ncarl: decline\nupdate 199 option=\"$carl\"\nbob: logout\nbob: login\n",
                               "p.flow", "p", null);
            Check("  char <name> lines: each char's name, zone, race, nation, job, level and gil, main first",
                  "main 100 1 0 1 -|bob 100 2 2 1 20 5|carl 101 1 1 1 -",
                  string.Join("|", f.Chars.Select(cs => $"{cs.Name} {cs.Zone} {cs.Race} {cs.Nation} {cs.Job} {cs.Level?.ToString() ?? "-"}" + (cs.Gil != 1000 ? $" {cs.Gil}" : ""))));
            Check("  ... Flow.Char is main", "main", f.Char.Name);
            Check("  a named char's setup lines are its fixture rows wherever they stand (before its login)", "item:0|item:1|keyitem:1",
                  string.Join("|", f.Setup.Select(st => $"{st.Verb}:{st.Actor}")));
            Check("  <name>: steps run as that char; the label as written",
                  "login:0|bob: login:1|invite bob:0>1|bob: accept:1|bob: trigger Vilatroire:1|update 129 option=$bob:0|finish option=$main:0|main: finish:0|" +
                  "bob: item CopperIngot:1|carl: login:2|invite carl:0>2|carl: decline:2|update 199 option=\"$carl\":0|bob: logout:1|bob: login:1",
                  string.Join("|", f.Steps.Select(st => $"{st.Label}:{st.Actor}" + (st.Target >= 0 ? $">{st.Target}" : ""))));
            Check("  option=$<name>: that char's index, the option 0 until the run; option=\"$<name>\": that char's name typed",
                  "1/0|0/0|2/$carl", $"{f.Steps[5].OptionChar}/{f.Steps[5].Option}|{f.Steps[6].OptionChar}/{f.Steps[6].Option}|{f.Steps[12].OptionTextChar}/{f.Steps[12].OptionText}");
            Check("  bob: trigger resolves the NPC in bob's zone", "Vilatroire", f.Steps[4].Name);
        }
        catch (FormatException e)
        {
            Fail("  more chars", e.Message);
        }
        try
        {
            var z = Flow.Parse(two + "login\nbob: login\nbob: warp 90,-62,268 EastRonfaure\nbob: trigger Andelain\ntrigger Vilatroire\n", "z.flow", "z", null);
            Check("  each char's own zone: bob's warp moves bob's, not main's", "Andelain/Vilatroire", $"{z.Steps[3].Name}/{z.Steps[4].Name}");
        }
        catch (FormatException e)
        {
            Fail("  each char's own zone", e.Message);
        }
        try
        {
            // localvar / weather under a prefix: that char's steps (its own local var; its zone's weather, which is
            // zone-wide: every char in that zone sees it), main's without one
            var lw = Flow.Parse(two + "login\nbob: login\nbob: localvar EviDialog 1\nbob: weather 2\nweather 0\n" +
                                "localvar Q 3\n", "l.flow", "l", null);
            static string Of(Statement st) => st.Verb == "localvar" ? $"{st.Name}={st.Amount}" : $"{st.Amount}";
            Check("  bob: localvar / bob: weather are bob's steps; unprefixed, main's",
                  "localvar:1:EviDialog=1|weather:1:2|weather:0:0|localvar:0:Q=3",
                  string.Join("|", lw.Steps.Skip(2).Select(st => $"{st.Verb}:{st.Actor}:{Of(st)}")));
        }
        catch (FormatException e)
        {
            Fail("  prefixed localvar / weather", e.Message);
        }
        SelfTestP3Refused("a prefixed localvar before that char's login (a step only)",
                          two + "login\nbob: localvar EviDialog 1\n", "t.flow:4: bob has not logged in");
        SelfTestP3Refused("a prefixed weather after that char's logout",
                          two + "login\nbob: login\nbob: logout\nbob: weather 2\n", "t.flow:6: bob is logged out");
        try
        {
            var one = Flow.Parse(head + "item CopperIngot\nlogin\ntrigger Vilatroire\n", "o.flow", "o", null);
            Check("  one char: main alone, every statement main's", "1 main|0,0", $"{one.Chars.Count} {one.Char.Name}|{string.Join(",", one.Steps.Select(st => st.Actor))}");
        }
        catch (FormatException e)
        {
            Fail("  one char", e.Message);
        }
        Check("  the flow hash covers the prefix: bob: trigger and trigger differ", false,
              Flow.Hash(two + "login\nbob: login\ntrigger Vilatroire\n") == Flow.Hash(two + "login\nbob: login\nbob: trigger Vilatroire\n"));

        SelfTestP3Refused("an unknown char name as a prefix", two + "login\ncarl: login\n", "t.flow:4: unknown char carl");
        SelfTestP3Refused("a step as a char that has not logged in", two + "login\nbob: trigger Vilatroire\n", "t.flow:4: bob has not logged in");
        SelfTestP3Refused("a step as a char after its logout", two + "login\nbob: login\nbob: logout\nbob: walk 0,0,0\n", "t.flow:6: bob is logged out");
        SelfTestP3Refused("a GM step as a char after its logout", two + "login\nbob: login\nbob: logout\nbob: item CopperIngot\n", "t.flow:6: bob is logged out");
        SelfTestP3Refused("a named char's login before main's", two + "bob: login\nlogin\n", "t.flow:3: step before login");
        try
        {
            var re = Flow.Parse(two + "login\nbob: login\nlogin\nbob: warp 90,-62,268 EastRonfaure\nbob: login\nbob: logout\nbob: login\n", "r.flow", "r", null);
            Check("  a login of a char logged in (main or named): it logs in again where it stands; after a logout: in again",
                  "login:0|bob: login:1|login:0|bob: warp 90,-62,268 EastRonfaure:1|bob: login:1|bob: logout:1|bob: login:1",
                  string.Join("|", re.Steps.Select(st => $"{st.Label}:{st.Actor}")));
        }
        catch (FormatException e)
        {
            Fail("  a second login", e.Message);
        }
        SelfTestP3Refused("option=$<name> of an unknown char", two + "login\nfinish option=$carl\n", "t.flow:4: unknown char carl");
        SelfTestP3Refused("option=\"$<name>\" of an unknown char", two + "login\nupdate 199 option=\"$carl\"\n", "t.flow:4: unknown char carl");
        SelfTestP3Refused("invite of an unknown char", two + "login\ninvite carl\n", "t.flow:4: unknown char carl");
        SelfTestP3Refused("invite of a char not logged in", two + "login\ninvite bob\n", "t.flow:4: bob has not logged in");
        SelfTestP3Refused("invite of itself", two + "login\nbob: login\nbob: invite bob\n", "t.flow:5: a char does not invite itself: bob");
        const string three = two + "char carl zone=WestRonfaure pos=0,0,0\n";
        SelfTestP3Refused("a second invite before the first is answered", three + "login\nbob: login\ncarl: login\ninvite bob\ninvite carl\n",
                          "t.flow:8: bob's invite is not answered yet (accept or decline first)");
        SelfTestP3Refused("an answer by a char not invited", three + "login\nbob: login\ncarl: login\ninvite bob\ncarl: accept\n",
                          "t.flow:8: carl has no invite to answer");
        SelfTestP3Refused("an answer with no invite", two + "login\nbob: login\nbob: decline\n", "t.flow:5: bob has no invite to answer");
        SelfTestP3Refused("an answer twice", two + "login\nbob: login\ninvite bob\nbob: accept\nbob: accept\n", "t.flow:7: bob has no invite to answer");
        try
        {
            var again = Flow.Parse(three + "login\nbob: login\ncarl: login\ninvite bob\nbob: decline\ninvite bob\nbob: accept\nbob: invite carl\ncarl: accept\n",
                                   "a.flow", "a", null);
            Check("  the same char invited again (after a decline), then another by another char", "0>1,0>1,1>2",
                  string.Join(",", again.Steps.Where(st => st.Verb == "invite").Select(st => $"{st.Actor}>{st.Target}")));
        }
        catch (FormatException e)
        {
            Fail("  invite again", e.Message);
        }
        SelfTestP3Refused("accept takes no arguments", two + "login\nbob: login\nbob: accept main\n", "t.flow:5: accept takes 0 arguments");
        SelfTestP3Refused("a prefix on tick (the world's)", two + "login\nbob: login\nbob: tick 400\n", "t.flow:5: tick is not a char's statement: no bob: prefix");
        SelfTestP3Refused("a prefix on setting", two + "bob: setting FOO 1\nlogin\n", "t.flow:3: setting is not a char's statement: no bob: prefix");
        SelfTestP3Refused("a prefix on a header line", two + "bob: hide scripts/quests/x\nlogin\n", "t.flow:3: hide is not a char's statement: no bob: prefix");
        SelfTestP3Refused("a prefix alone", two + "login\nbob:\n", "t.flow:4: bob: takes a statement");
        SelfTestP3Refused("the first char line named", "char bob zone=WestRonfaure pos=0,0,0\nlogin\n", "t.flow:1: the first char line names no char: it is main");
        SelfTestP3Refused("a second char line with no name", head + "char zone=WestRonfaure pos=0,0,0\nlogin\n",
                          "t.flow:2: a second char line: each char after the first has a name (char <name> zone=...)");
        SelfTestP3Refused("a char named main", head + "char main zone=WestRonfaure pos=0,0,0\nlogin\n",
                          "t.flow:2: a char's name is a lower-case word (letters, digits, _; at most 16), not main or a statement: main");
        SelfTestP3Refused("a char named as a verb", head + "char trigger zone=WestRonfaure pos=0,0,0\nlogin\n",
                          "t.flow:2: a char's name is a lower-case word (letters, digits, _; at most 16), not main or a statement: trigger");
        SelfTestP3Refused("a char name not lower case", head + "char Bob zone=WestRonfaure pos=0,0,0\nlogin\n",
                          "t.flow:2: a char's name is a lower-case word (letters, digits, _; at most 16), not main or a statement: Bob");
        SelfTestP3Refused("two char lines of one name", two + "char bob zone=WestRonfaure pos=0,0,0\nlogin\n", "t.flow:3: a second char line for bob");
        SelfTestP3Refused("a seventh char", head + string.Concat(Enumerable.Range(1, 6).Select(i => $"char c{i} zone=WestRonfaure pos=0,0,0\n")) + "login\n",
                          "t.flow:7: a flow has at most 6 chars (a party)");
        SelfTestP3Refused("a char line after the login", head + "login\nchar bob zone=WestRonfaure pos=0,0,0\n", "t.flow:3: char after login");
        SelfTestP3Refused("race out of range", head.TrimEnd('\n') + " race=9\nlogin\n", "t.flow:1: a race is 1 to 8 (xi.race): 9");
        SelfTestP3Refused("rank needs nation= on the char's own line", two.Replace(" nation=2", "") + "bob: rank 2\nlogin\n", "t.flow:3: rank needs nation= on the char line");
    }

    // the ids of a flow of n chars: consecutive, from the slice, never wrapping; one char as before the chars
    private static void SelfTestP3Ids()
    {
        string Ids(int index, int n, int start, int size) => string.Join(",", CharsFor(index, n, start, size).Select(id => id - FirstChar));
        Check("  one char: the slice's start plus i modulo its size (as CharFor did)", "0|38|0|5|13",
              string.Join("|", Ids(0, 1, 0, 39), Ids(38, 1, 0, 39), Ids(39, 1, 0, 39), Ids(5, 1, 0, 39), Ids(13, 1, 13, 13)));
        Check("  two chars: consecutive ids, main lowest, never past the slice's end", "0,1|37,38|0,1|20,21|13,14",
              string.Join("|", Ids(0, 2, 0, 39), Ids(37, 2, 0, 39), Ids(38, 2, 0, 39), Ids(7, 2, 13, 13), Ids(12, 2, 13, 13)));
        Check("  three chars in a shard of 13", "23,24,25", Ids(10, 3, 13, 13));
        try
        {
            CharsFor(0, 4, 0, 3);
            Fail("  more chars than the slice has ids", "accepted");
        }
        catch (InvalidOperationException e)
        {
            Check("  more chars than the slice has ids: refused", "4 chars need 4 ids; the slice has 3", e.Message);
        }
    }

    // a trace with another char: its header line, its packets (a login bundle for its own login), its tables, read back
    private static void SelfTestP3Trace()
    {
        var t = new Trace { Name = "p", Seed = 7, FlowSha = "ab", Recorder = "lua" };
        t.Chars.Add("bob");
        var login = new TraceStep { Label = "login" };
        login.Packets.AddRange(new[] { "00A:01", "063:02" });
        login.Set("chars").Add("pos_zone=100");
        login.Set("bob:chars").Add("pos_zone=100");
        login.Set("chars").Add("quests=0000");
        login.Set("bob:chars").Add("quests=0000");
        t.Steps.Add(login);
        var bobIn = login.Carry("bob: login");
        bobIn.Packets.Add("00D:<CHARID:bob>");
        bobIn.Others.Add(("bob", new List<string> { "00A:02", "063:02" }));
        t.Steps.Add(bobIn);
        var accept = bobIn.Carry("bob: accept");
        accept.Packets.Add("0DD:<CHARID>");
        accept.Others.Add(("bob", new List<string> { "0DD:<CHARID:bob>" }));
        accept.Set("bob:accounts_parties").Add("partyid=$char");
        accept.Set("bob:chars").Remove("quests=0000");
        accept.Set("bob:chars").Add("quests=0100");
        t.Steps.Add(accept);
        var store = new PacketStore();
        string text = t.Write(store);
        var lines = text.Split('\n');
        string Block(string step) => string.Join("|", lines.SkipWhile(l => l != "step " + step).Skip(1).TakeWhile(l => !l.StartsWith("step ") && l.Length > 0));
        Check("  the header: char <name> after the masks", true, lines.Contains("char bob") && Array.IndexOf(lines, "char bob") < Array.IndexOf(lines, "step login"));
        Check("  main's login: main's bundle, then main's tables, then bob's",
              $"packets @{PacketStore.Hash(string.Join(",", login.Packets.Select(PacketStore.Hash)))} (2)|chars @{PacketStore.Hash("pos_zone=100\tquests=0000")} (2)|" +
              $"bob:chars @{PacketStore.Hash("pos_zone=100\tquests=0000")} (2)", Block("login"));
        Check("  bob's login: main's packets listed, bob's as its bundle",
              $"packets|  00D @{PacketStore.Hash("00D:<CHARID:bob>")}|packets bob @{PacketStore.Hash(string.Join(",", bobIn.Others[0].Packets.Select(PacketStore.Hash)))} (2)",
              Block("bob: login"));
        Check("  another step: bob's packets listed after main's, bob's rows after main's",
              $"packets|  0DD @{PacketStore.Hash("0DD:<CHARID>")}|packets bob|  0DD @{PacketStore.Hash("0DD:<CHARID:bob>")}|" +
              "bob:accounts_parties + partyid=$char|bob:chars - quests=0000|bob:chars + quests=0100",
              Block("bob: accept"));
        t.BlobColumns.Add("chars.quests");
        text = t.Write(store);
        Check("  ... a named char's blob column of a single-row table: its byte runs", true, text.Contains("\nbob:chars quests +0:01\n"));
        var back = Trace.Parse(text, "p.trace", store);
        Check("  read back: the chars, every char's packets and rows the same", "bob|0", $"{string.Join(",", back.Chars)}|{Trace.Compare(t, back).Count}");
        Check("  ... and written back byte-identical", true, back.Write(store) == text);
        var other = Trace.Parse(text, "p.trace", store);
        other.Steps[2].Others[0].Packets.Add("0DD:00");
        Check("  a difference in bob's packets: named by char", "step 3 [bob: accept]: bob's packets: recorded 1",
              string.Join(";", Trace.Compare(back, other)).Split(", now")[0]);
        other = Trace.Parse(text, "p.trace", store);
        other.Steps[2].Others[0].Packets[0] = "0DD:<CHARID:bob>01";
        back.Steps[2].Others[0].Packets[0] = "0DD:<CHARID:bob>00";
        Check("  ... a byte difference past bob's placeholder: its offset counts it as 4 bytes", "step 3 [bob: accept]: bob's packet #0 (0x0DD) differs at byte 4",
              Trace.Compare(back, other).FirstOrDefault()?.Split('\n')[0]);
        Check("  a trace whose chars differ", "the chars differ: recorded [main bob], now [main]",
              Trace.Compare(back, Trace.Parse(text.Replace("char bob\n", "").Replace("packets bob", "packets").Replace("bob:", ""), "q.trace", store)).FirstOrDefault() is { } d && d.StartsWith("the chars")
                  ? d : "(no difference)");
        try
        {
            Trace.Parse(text.Replace("char bob\n", ""), "q.trace", store);
            Fail("  packets of a char not in the header", "parsed");
        }
        catch (FormatException e)
        {
            Check("  packets of a char not in the header: refused", true, e.Message.Contains("no char of the header"));
        }
        Check("  LoginOf: login, main: login, bob: login, bob: accept, a word ending in login",
              "main|main|bob|-|-", string.Join("|", new[] { "login", "main: login", "bob: login", "bob: accept", "relogin" }.Select(l => TraceStep.LoginOf(l) ?? "-")));
        string show = ShowText(text, null, store, false);
        Check("  show: bob's packets named, bob's tables as written", true, show.Contains("  bob's packets:\n") && show.Contains("  bob:chars: 2 rows @"));
        Check("  the store's bundle references: a named char's login bundle kept", true,
              System.Text.RegularExpressions.Regex.IsMatch(text, @"^packets bob @[0-9a-f]{16} \(2\)$", System.Text.RegularExpressions.RegexOptions.Multiline));
    }

    // the placeholders of the chars: main's as before, another char's named, in packets and in rows
    private static void SelfTestP3Placeholders()
    {
        var saved = actors_.Select(a => (a.Name, a.Id)).ToList();
        try
        {
            SetActors(new[] { "main", "bob" }, new uint[] { 996170, 996171 });
            Check("  a row value: main's id $char, bob's $char:bob, bob's name $charname:bob", "$char/$char:bob/$charname:bob/$charname/996172",
                  $"{RenderValue("996170", 996170)}/{RenderValue("996171", 996170)}/{RenderValue("Qgolden171", 996170)}/{RenderValue("Qgolden170", 996170)}/{RenderValue("996172", 996170)}");
            var buf = new byte[24];
            buf[0] = 0x5C;
            buf[1] = (byte)(buf.Length / 4 << 1);
            BitConverter.GetBytes(996170u).CopyTo(buf, 4);
            BitConverter.GetBytes(996171u).CopyTo(buf, 8);
            Encoding.ASCII.GetBytes("Qgolden171").CopyTo(buf, 12);
            string n = Normalise(new BasicPacket(buf), new List<Mask>());
            Check("  a packet: main's id <CHARID>, bob's <CHARID:bob>, bob's name <CHARNAME:bob>", "05C:5C0C0000<CHARID><CHARID:bob><CHARNAME:bob>0000", n);
            Check("  show: the ids as $char and $char:bob", "[event update params [$char,$char:bob]]", DecodePacket(n));
            // a mob hitting bob, then main
            var ap = new XI.Zone.ActionPacket { ActorId = 17633282, ActionType = 1, ActionId = 7 };
            ap.AddTarget(996171).AddResult().Param = 7;
            ap.AddTarget(996170).AddResult().Param = 8;
            var attack = XI.Zone.Battle2Packets.Battle2(ap);
            string na = Normalise(attack, new List<Mask>());
            Check("  a 0x028's bit-packed targets: main's <CHARID@<bit>> first, then bob's <CHARID:bob@<bit>>", true,
                  System.Text.RegularExpressions.Regex.IsMatch(na, @"<CHARID@\d+><CHARID:bob@150>$"));
            Check("  show names both", true, DecodePacket(na).StartsWith("[action, target $char at bit ", StringComparison.Ordinal) && DecodePacket(na).Contains("150 ($char:bob)"));
            // a placeholder of an odd length (<CHARID:carl>, 13 characters) before another pattern: still on the byte grid
            SetActors(new[] { "main", "carl" }, new uint[] { 996170, 996183 });
            var odd = new byte[24];
            odd[0] = 0x0D;
            odd[1] = (byte)(odd.Length / 4 << 1);
            BitConverter.GetBytes(996183u).CopyTo(odd, 4);
            Encoding.ASCII.GetBytes("Qgolden183").CopyTo(odd, 8);
            BitConverter.GetBytes(996170u).CopyTo(odd, 18);
            Check("  an odd-length placeholder keeps the byte grid: carl's name and main's id after carl's id",
                  "00D:0D0C0000<CHARID:carl><CHARNAME:carl><CHARID>0000", Normalise(new BasicPacket(odd), new List<Mask>()));
        }
        finally
        {
            SetActors(saved.Select(s => s.Name).ToList(), saved.Select(s => s.Id).ToList());
        }
    }

    // every checked-in trace reads and writes back byte-identical (with the store as checked in): the format of a
    // one-char trace is the one before the chars
    private static void SelfTestP3CheckedIn()
    {
        var store = PacketStore.Load(GoldenDir);
        int n = 0;
        var bad = new List<string>();
        foreach (var path in Directory.GetFiles(GoldenDir, "*.trace").OrderBy(p => p, StringComparer.Ordinal))
        {
            string text = File.ReadAllText(path, Encoding.UTF8);
            try
            {
                if (Trace.Parse(text, path, store).Write(store) != text) bad.Add(Path.GetFileName(path));
            }
            catch (FormatException e)
            {
                bad.Add($"{Path.GetFileName(path)} ({e.Message})");
            }
            n++;
        }
        Check($"  every checked-in trace ({n}) read and written back byte-identical", "", string.Join(", ", bad));
    }
}
