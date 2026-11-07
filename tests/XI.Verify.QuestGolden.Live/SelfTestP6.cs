using System;
using System.Linq;
using XI.Zone;

// quest rework P6: the flow statements the Jeuno and Other Areas drafts needed (storage, setting map.<NAME>, trigger <Npc>
// here), in the self-test (no database); the run-time guards through their pure parts
internal static partial class Program
{
    private static void SelfTestPlatformP6()
    {
        Console.WriteLine("quest rework P6 statements");
        SelfTestP6Storage();
        SelfTestP6MapSetting();
        SelfTestP6TriggerHere();
    }

    // storage <inventory|satchel> <n>: a header line before the login (char_storage's column), a step after it (a GM's
    // changeContainerSize by the difference); the engine's sizes only
    private static void SelfTestP6Storage()
    {
        const string head = "char zone=LowerJeuno pos=0,0,0\nchar bob zone=LowerJeuno pos=0,0,0\n";
        try
        {
            var f = Flow.Parse(head + "storage inventory 35\nstorage satchel 30\nbob: storage inventory 80\nlogin\nstorage inventory 40\nstorage satchel 0\n" +
                               "bob: login\nbob: storage satchel 80\n", "s.flow", "s", null);
            Check("  storage before the login: rows of their chars (the container's xi.inv id, the size)",
                  "0 inventory 0 35|0 satchel 5 30|1 inventory 0 80",
                  string.Join("|", f.Setup.Select(st => $"{st.Actor} {st.Name} {st.Value} {st.Amount}")));
            Check("  storage after the login: steps, the label as written",
                  "storage inventory 40 0 40|storage satchel 0 5 0|bob: storage satchel 80 5 80",
                  string.Join("|", f.Steps.Where(st => st.Verb == "storage").Select(st => $"{st.Label} {st.Value} {st.Amount}")));
        }
        catch (FormatException e)
        {
            Fail("  storage <inventory|satchel> <n>", e.Message);
        }
        const string one = "char zone=LowerJeuno pos=0,0,0\n";
        P5Refused("storage of another container", one + "storage safe 50\nlogin\n", "t.flow:2: storage takes inventory or satchel: safe");
        P5Refused("storage inventory under 30", one + "storage inventory 29\nlogin\n", "t.flow:2: the inventory's size is 30 to 80: 29");
        P5Refused("storage inventory over 80", one + "login\nstorage inventory 81\n", "t.flow:3: the inventory's size is 30 to 80: 81");
        P5Refused("storage satchel under 0", one + "storage satchel -1\nlogin\n", "t.flow:2: the satchel's size is 0 to 80: -1");
        P5Refused("storage satchel over 80", one + "storage satchel 85\nlogin\n", "t.flow:2: the satchel's size is 0 to 80: 85");
        P5Refused("storage without a size", one + "storage inventory\nlogin\n", "t.flow:2: storage takes 2 arguments");
        P5Refused("storage with a size that is not a number", one + "storage inventory big\nlogin\n", "t.flow:2: not a number: big");
        P5Refused("storage of a logged-out char", one + "login\nlogout\nstorage inventory 35\n", "t.flow:4: main is logged out");
        // the step's guard: the size getContainerSize reads after it must be the statement's
        var step = Flow.Parse(one + "login\nstorage inventory 35\n", "g.flow", "g", null).Steps[1];
        Check("  the storage step's guard: quiet at the size asked, fails otherwise (the engine kept the old size: the items in it)",
              "-|storage inventory 35: the size is 30 after changeContainerSize (the items in it?)",
              $"{StorageProblem(step, "35") ?? "-"}|{StorageProblem(step, "30") ?? "-"}");
    }

    // setting map.<NAME> <value>: xi.settings.map's; <NAME> alone stays xi.settings.main's
    private static void SelfTestP6MapSetting()
    {
        const string head = "char zone=Selbina pos=0,0,0\n";
        try
        {
            var f = Flow.Parse(head + "setting map.FISHING_ENABLE true\nsetting map.EXP_RATE 1.5\nsetting map.FISHING_MIN_LEVEL 3\nsetting GIL_RATE 2\nlogin\n" +
                               "setting map.FISHING_ENABLE false\n", "m.flow", "m", null);
            var all = f.Setup.Concat(f.Steps.Skip(1)).ToList();
            Check("  setting map.<NAME>: the table, the name and the Lua value (a boolean, a decimal, a whole number); <NAME> alone main's",
                  "map FISHING_ENABLE bool true|map EXP_RATE number 1.5|map FISHING_MIN_LEVEL number 3|main GIL_RATE number 2|map FISHING_ENABLE bool false",
                  string.Join("|", all.Select(st => $"{st.Table} {st.Name} {st.Status} {SettingLuaValue(st)}")));
            Check("  ... after the login a step, the label as written", "setting map.FISHING_ENABLE false", f.Steps[1].Label);
            // the run's guards: the name must exist; a map setting's value must be of its kind (FISHING_ENABLE == true is
            // false for the number 1); a main setting keeps its rule (existence only)
            Check("  the run's guard: a map setting of its own kind passes; another kind, or none, fails; main's existence only",
                  "-|-|setting map.FISHING_ENABLE is a boolean, not a number: write it as one|setting map.EXP_RATE is a number, not a boolean: write it as one|" +
                  "unknown setting map.NOPE|unknown setting GIL_RATE|-",
                  string.Join("|", new[]
                  {
                      SettingProblem(all[0], "boolean"),
                      SettingProblem(all[1], "number"),
                      SettingProblem(Flow.Parse(head + "setting map.FISHING_ENABLE 1\nlogin\n", "k.flow", "k", null).Setup[0], "boolean"),
                      SettingProblem(Flow.Parse(head + "setting map.EXP_RATE true\nlogin\n", "k.flow", "k", null).Setup[0], "number"),
                      SettingProblem(Flow.Parse(head + "setting map.NOPE 1\nlogin\n", "k.flow", "k", null).Setup[0], "nil"),
                      SettingProblem(all[3], "nil"),
                      SettingProblem(all[3], "boolean"),
                  }.Select(p => p ?? "-")));
        }
        catch (FormatException e)
        {
            Fail("  setting map.<NAME>", e.Message);
        }
        P5Refused("setting of another table", head + "setting login.X 1\nlogin\n", "t.flow:2: a setting name is an identifier, or map.<identifier>: login.X");
        P5Refused("setting map. with no name", head + "setting map. 1\nlogin\n", "t.flow:2: a setting name is an identifier, or map.<identifier>: map.");
        P5Refused("setting map.<NAME> with a dot in the name", head + "setting map.A.B 1\nlogin\n", "t.flow:2: a setting name is an identifier, or map.<identifier>: map.A.B");
        P5Refused("setting map.<NAME> with a prefix (the world's)", head + "login\nmain: setting map.FISHING_ENABLE true\n",
                  "t.flow:3: setting is not a char's statement: no main: prefix");
        P5Refused("setting map.<NAME> not a number", head + "setting map.EXP_RATE fast\nlogin\n", "t.flow:2: not a number: fast");
    }

    // trigger <Npc> here: no walk; the NPC must be in the engine's talk range (OnTalk's own test, ZoneHost.InTalkRange)
    private static void SelfTestP6TriggerHere()
    {
        const string mhaura = "char zone=Mhaura pos=0,0,0\n";
        try
        {
            var f = Flow.Parse(mhaura + "login\nwalk 45.4,-8,37.5\ntrigger Felisa here\ntrigger Felisa\ntrigger Felisa at=0.5\ntrigger Blandine+20 here\n", "h.flow", "h", null);
            Check("  trigger <Npc> here: no walk (here, no at=); without it as before; with <Npc>+<n>",
                  "Felisa 0 True -|Felisa 0 False -|Felisa 0 False 0.5|Blandine 20 True -",
                  string.Join("|", f.Steps.Where(st => st.Verb == "trigger").Select(st => $"{st.Name} {st.Offset} {st.Here} {st.At?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"}")));
            Check("  ... the label as written", "trigger Felisa here", f.Steps[2].Label);
        }
        catch (FormatException e)
        {
            Fail("  trigger <Npc> here", e.Message);
        }
        P5Refused("trigger here and at= both", mhaura + "login\ntrigger Felisa here at=1\n", "t.flow:3: trigger takes 1 to 2 arguments");
        P5Refused("trigger with another word", mhaura + "login\ntrigger Felisa there\n", "t.flow:3: trigger takes <Npc> [at=<yalms>|here]: there");
        P5Refused("trade here (trigger's only)", mhaura + "login\ntrade Felisa here\n", "t.flow:3: trade takes <Npc>, [gil=<n>], <Item> x<n> pairs, then [gil=<n>]");
        // the run's guard: the engine's talk range, 6 yalms in 3D (OnTalk's squared distance 36)
        var npc = new NpcEntity { Name = "Felisa", X = 45.441f, Y = -7.999f, Z = 39.381f };
        CharData At(float x, float y, float z) => new CharData { X = x, Y = y, Z = z };
        Check("  the run's guard: in range at 1.9 and 5.99 yalms; out at 6.01, and 6 up (y counts)",
              "-|-|trigger Felisa here: the char at 51.451,-7.999,39.381 is 6.01 yalms from it, out of talk range (6 yalms): walk closer first|" +
              "trigger Felisa here: the char at 45.441,-1.989,39.381 is 6.01 yalms from it, out of talk range (6 yalms): walk closer first",
              string.Join("|", new[]
              {
                  TalkHereProblem(At(45.4f, -8f, 37.5f), npc),
                  TalkHereProblem(At(45.441f, -7.999f, 33.391f), npc),
                  TalkHereProblem(At(51.451f, -7.999f, 39.381f), npc),
                  TalkHereProblem(At(45.441f, -1.989f, 39.381f), npc),
              }.Select(p => p ?? "-")));
    }
}
