using System;
using System.Linq;

// quest rework P1: the flow statements drafting needed (update option="<text>", merits, trigger / trade <Npc>+<n>,
// delkeyitem; localvar, from B3b Bastok group 8), in the self-test (no database)
internal static partial class Program
{
    private static void SelfTestPlatformP1()
    {
        Console.WriteLine("quest rework P1 statements");
        const string head = "char zone=BastokMarkets pos=0,0,0\n";
        void Refused(string what, string text, string message)
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

        // update <csid> option="<text>": the text GP_CLI_COMMAND_PASSWARDS carries
        try
        {
            var u = Flow.Parse(head + "login\nupdate 199 option=\"kurou-morou\"\nupdate 199 option=\"\"\nupdate 199 option=3\n", "u.flow", "u", null);
            Check("  update option=\"<text>\": the text, an empty one, and a number option beside them", "199 kurou-morou -|199  -|199 null 3",
                  string.Join("|", u.Steps.Skip(1).Select(st => $"{st.Value} {st.OptionText ?? "null"} {st.Option?.ToString() ?? "-"}")));
            Check("  ... the label as written", "update 199 option=\"kurou-morou\"", u.Steps[1].Label);
        }
        catch (FormatException e)
        {
            Fail("  update option=\"<text>\"", e.Message);
        }
        Refused("update option=\"<text>\" with no closing quote", head + "login\nupdate 199 option=\"abc\n", "t.flow:3: a text option is option=\"<text>\": option=\"abc");
        Refused("update option=\"<text>\" over 16 characters", head + "login\nupdate 199 option=\"abcdefghijklmnopq\"\n",
                "t.flow:3: a text option is at most 16 characters: option=\"abcdefghijklmnopq\"");
        Refused("update option=\"<text>\" with a quote inside", head + "login\nupdate 199 option=\"a\"b\"\n",
                "t.flow:3: a text option is printable ASCII without '\"': option=\"a\"b\"");
        Refused("update option=\"<text>\" with a space (two words)", head + "login\nupdate 199 option=\"a b\"\n", "t.flow:3: update takes 2 arguments");
        Refused("finish option=\"<text>\": no string finish", head + "login\nfinish option=\"abc\"\n",
                "t.flow:3: finish takes a number option: a text option reaches only onEventUpdate (update <csid> option=\"<text>\"): option=\"abc\"");

        // merits <n>: char_exp.merits before the login, setMerits after it
        try
        {
            var m = Flow.Parse(head + "merits 15\nlogin\nmerits 3\n", "m.flow", "m", null);
            Check("  merits <n>: setup and step", "setup merits 15|step merits 3",
                  $"setup {m.Setup[0].Verb} {m.Setup[0].Amount}|step {m.Steps[1].Verb} {m.Steps[1].Amount}");
        }
        catch (FormatException e)
        {
            Fail("  merits <n>", e.Message);
        }
        Refused("merits out of range", head + "merits 256\nlogin\n", "t.flow:2: merits are 0 to 255: 256");
        Refused("merits not a number", head + "login\nmerits many\n", "t.flow:3: not a number: many");
        Refused("merits without a count", head + "merits\nlogin\n", "t.flow:2: merits takes 1 arguments");

        // trigger / trade <Npc>+<n>, <KEY>[+<n>]: NPCs that share a name (the Eldieme Necropolis' Braziers, Sarcophagi)
        const string eldieme = "char zone=TheEldiemeNecropolis pos=0,0,0\n";
        try
        {
            var n = Flow.Parse(eldieme + "login\ntrigger Brazier\ntrigger Brazier+2 at=1\ntrade Sarcophagus+3 CopperIngot x1\ntrigger BRAZIER+1\ntrade SARCOPHAGUS_OFFSET gil=5\n",
                               "n.flow", "n", null);
            Check("  trigger / trade <Npc>+<n>, <KEY>[+<n>]: the name or key, the offset, key or not, the label as written",
                  "trigger Brazier 0 False trigger Brazier|trigger Brazier 2 False trigger Brazier+2 at=1|trade Sarcophagus 3 False trade Sarcophagus+3 CopperIngot x1|" +
                  "trigger BRAZIER 1 True trigger BRAZIER+1|trade SARCOPHAGUS_OFFSET 0 True trade SARCOPHAGUS_OFFSET gil=5",
                  string.Join("|", n.Steps.Skip(1).Select(st => $"{st.Verb} {st.Name} {st.Offset} {st.NpcKey} {st.Label}")));
        }
        catch (FormatException e)
        {
            Fail("  trigger / trade <Npc>+<n>", e.Message);
        }
        Refused("trigger <Npc>+0", eldieme + "login\ntrigger Brazier+0\n", "t.flow:3: trigger takes <Npc>, <Npc>+<n>, <KEY> or <KEY>+<n>, n from 1: Brazier+0");
        Refused("trade <Npc>+<word>", eldieme + "login\ntrade Brazier+x CopperIngot x1\n", "t.flow:3: trade takes <Npc>, <Npc>+<n>, <KEY> or <KEY>+<n>, n from 1: Brazier+x");
        Refused("trigger <Npc>+-<n>", eldieme + "login\ntrigger Brazier+-1\n", "t.flow:3: trigger takes <Npc>, <Npc>+<n>, <KEY> or <KEY>+<n>, n from 1: Brazier+-1");
        Refused("trigger of an unknown name with an offset", eldieme + "login\ntrigger Brazer+1\n", "t.flow:3: unknown npc Brazer");
        Refused("trigger of a name not all capitals is not a key", eldieme + "login\ntrigger Brazier_Offset+1\n", "t.flow:3: unknown npc Brazier_Offset");
        Check("  <Npc>+<n>: the first's id plus n, when an NPC of the same name has it", "103",
              NpcAtOffset("Brazier", 101, 2, id => id is 101 or 103 ? "Brazier" : null).ToString());
        string NpcRefused(uint first, int offset, Func<uint, string> nameOf)
        {
            try
            {
                return $"accepted {NpcAtOffset("Brazier", first, offset, nameOf)}";
            }
            catch (InvalidOperationException e)
            {
                return e.Message.Split(" of zone")[0];
            }
        }
        Check("  <Npc>+<n> naming another NPC: refused", "Brazier+1 (102) is Sarcophagus, not a Brazier",
              NpcRefused(101, 1, id => id == 101 ? "Brazier" : id == 102 ? "Sarcophagus" : null));
        Check("  <Npc>+<n> naming no NPC: refused", "Brazier+5 (106) names no NPC", NpcRefused(101, 5, id => id == 101 ? "Brazier" : null));
        Check("  <KEY> whose id is no NPC: refused", "Brazier (101) names no NPC", NpcRefused(101, 1, id => null));

        // delkeyitem <KeyItem>: a step only
        try
        {
            var d = Flow.Parse(head + "keyitem Cursepaper\nlogin\ndelkeyitem Cursepaper\n", "d.flow", "d", null);
            Check("  delkeyitem <KeyItem>: a step, the key item's id", $"step delkeyitem {XI.QuestContent.KeyItem.Cursepaper}",
                  $"step {d.Steps[1].Verb} {d.Steps[1].Value}");
        }
        catch (FormatException e)
        {
            Fail("  delkeyitem <KeyItem>", e.Message);
        }
        Refused("delkeyitem before the login", head + "delkeyitem Cursepaper\nlogin\n", "t.flow:2: step before login");
        Refused("delkeyitem of an unknown key item", head + "login\ndelkeyitem Cursepapers\n", "t.flow:3: unknown key item Cursepapers");
        Refused("delkeyitem without a key item", head + "login\ndelkeyitem\n", "t.flow:3: delkeyitem takes 1 arguments");
        // animatenpc <Npc> <n>: a step only (a GM's !animatenpc: GetNPCByID(id):setAnimation(n)), the NPC named as trigger
        // names it, the animation 0 to 255
        try
        {
            var an = Flow.Parse(head + "login\nanimatenpc Zon-Fobun 0\nanimatenpc Zon-Fobun 255\n", "a.flow", "a", null);
            var ae = Flow.Parse(eldieme + "login\nanimatenpc Brazier+2 8\nanimatenpc BRAZIER+1 9\n", "a.flow", "a", null);
            Check("  animatenpc <Npc> <n>: a step, the NPC (name or key, the offset) and the animation",
                  "animatenpc Zon-Fobun 0 False 0|animatenpc Zon-Fobun 0 False 255|animatenpc Brazier 2 False 8|animatenpc BRAZIER 1 True 9",
                  string.Join("|", an.Steps.Skip(1).Concat(ae.Steps.Skip(1)).Select(st => $"{st.Verb} {st.Name} {st.Offset} {st.NpcKey} {st.Amount}")));
        }
        catch (FormatException e)
        {
            Fail("  animatenpc <Npc> <n>", e.Message);
        }
        Refused("animatenpc before the login", head + "animatenpc Zon-Fobun 0\nlogin\n", "t.flow:2: step before login");
        Refused("animatenpc without an animation", head + "login\nanimatenpc Zon-Fobun\n", "t.flow:3: animatenpc takes 2 arguments");
        Refused("animatenpc of an unknown NPC", head + "login\nanimatenpc Zon-Fobunn 0\n", "t.flow:3: unknown npc Zon-Fobunn");
        Refused("animatenpc over 255", head + "login\nanimatenpc Zon-Fobun 256\n", "t.flow:3: an animation is 0 to 255 (setAnimation takes a uint8): 256");
        Refused("animatenpc below 0", head + "login\nanimatenpc Zon-Fobun -1\n", "t.flow:3: an animation is 0 to 255 (setAnimation takes a uint8): -1");
        Refused("animatenpc not a number", head + "login\nanimatenpc Zon-Fobun OPEN_DOOR\n", "t.flow:3: not a number: OPEN_DOOR");

        // localvar <name> <n>: a step only (player:setLocalVar on the logged-in char)
        try
        {
            var l = Flow.Parse(head + "login\nlocalvar EviDialog 1\nlocalvar EviDialog 0\n", "l.flow", "l", null);
            Check("  localvar <name> <n>: a step, the var's name and value", "localvar EviDialog 1|localvar EviDialog 0",
                  string.Join("|", l.Steps.Skip(1).Select(st => $"{st.Verb} {st.Name} {st.Amount}")));
        }
        catch (FormatException e)
        {
            Fail("  localvar <name> <n>", e.Message);
        }
        Refused("localvar before the login", head + "localvar EviDialog 1\nlogin\n", "t.flow:2: step before login");
        Refused("localvar without a value", head + "login\nlocalvar EviDialog\n", "t.flow:3: localvar takes 2 arguments");
        Refused("localvar with a value that is not a number", head + "login\nlocalvar EviDialog one\n", "t.flow:3: not a number: one");
        // weather <n>: a step only (player:setWeather on the logged-in char), an xi.weather id 0 to 19
        try
        {
            var wf = Flow.Parse(head + "login\nweather 0\nweather 19\n", "w.flow", "w", null);
            Check("  weather <n>: a step, the weather id", "weather 0|weather 19",
                  string.Join("|", wf.Steps.Skip(1).Select(st => $"{st.Verb} {st.Amount}")));
        }
        catch (FormatException e)
        {
            Fail("  weather <n>", e.Message);
        }
        Refused("weather before the login", head + "weather 0\nlogin\n", "t.flow:2: step before login");
        Refused("weather without a value", head + "login\nweather\n", "t.flow:3: weather takes 1 arguments");
        Refused("weather over 19", head + "login\nweather 20\n", "t.flow:3: a weather is an xi.weather id, 0 to 19: 20");
        Refused("weather below 0", head + "login\nweather -1\n", "t.flow:3: a weather is an xi.weather id, 0 to 19: -1");
        Refused("localvar with a negative value (setLocalVar takes a uint32)", head + "login\nlocalvar EviDialog -2\n",
                "t.flow:3: a local var is 0 or more (setLocalVar takes a uint32): -2");
    }
}
