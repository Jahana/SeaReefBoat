// XI.Verify.Quests -- quest rework P6: the platform needs the Jeuno and Other Areas drafts found (a container's size,
// xi.settings.map, a letterless NPC's constant), on the facets and the fake. The live context's side is in
// XI.Verify.QuestScripts.Live (PlatformP6.cs).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;

internal static partial class Program
{
    static void RunPlatformP6()
    {
        Console.WriteLine("== quest rework P6: platform needs ==");
        RunP6ContainerSize();
        RunP6MapSetting();
        RunP6LetterlessNpc();
    }

    // 1. Player.ContainerSize(container) / Player.ChangeContainerSize(container, delta): getContainerSize /
    // changeContainerSize (the Gobbiebag template's reqsMet, its 43 / 73 parameters and its 73 finish)
    static void RunP6ContainerSize()
    {
        var f = new Fake();
        IQuestContext q = f;
        Check("p6 storage:a fresh fake's inventory is 30, its satchel 0 (a fresh char's)", "30/0",
              $"{q.Player.ContainerSize(0)}/{q.Player.ContainerSize(5)}");
        // the Gobbiebag template, Part II's params: available at size 35, 43 (size + 1, 0, 1); 73's finish +5, the satchel +5
        // when it has one
        var d = new BuiltQuest(b =>
        {
            b.State(QuestStatus.Available, when: c => c.Player.ContainerSize(0) == 35)
                .Npc(245, "Bluffnix").OnTrigger(c => QuestReply.ProgressEvent(43, c.Player.ContainerSize(0) + 1, 0, 1));
            b.State(QuestStatus.Accepted)
                .OnEventFinish(245, 73, c =>
                {
                    c.Player.ChangeContainerSize(0, 5);
                    if (c.Player.ContainerSize(5) > 0) c.Player.ChangeContainerSize(5, 5);
                });
        }).Compile();
        var when = d.States[0].Matches[0].When;
        f.ContainerSizes[0] = 35;
        var at30 = new Fake();
        Check("p6 storage:a check reads the size (the start size 35 matches, a fresh 30 does not)", "True/False", $"{when(f)}/{when(at30)}");
        var trigger = d.States[0].Handlers.Single(h => h.Kind == QuestHandlerKind.OnTrigger);
        Check("p6 storage:a reply carries the size (43, 36, 0, 1)", "ProgressEvent(43,36,0,1)", QuestContextFake.Describe(trigger.Reply(f)));
        var finish = d.States[1].Handlers.Single(h => h.Kind == QuestHandlerKind.OnEventFinish);
        f.Calls.Clear();
        finish.Run(f);
        Check("p6 storage:ChangeContainerSize is recorded and moves the size; no satchel, no satchel change", "Player.ChangeContainerSize(0,5)|40/0",
              $"{string.Join(",", f.Calls)}|{q.Player.ContainerSize(0)}/{q.Player.ContainerSize(5)}");
        var s = new Fake();
        s.ContainerSizes[0] = 35;
        s.ContainerSizes[5] = 30;
        finish.Run(s);
        Check("p6 storage:with a satchel (30) both grow by 5", "Player.ChangeContainerSize(0,5),Player.ChangeContainerSize(5,5)|40/35",
              $"{string.Join(",", s.Calls)}|{s.ContainerSize(0)}/{s.ContainerSize(5)}");
        var big = new Fake();
        big.ContainerSizes[0] = 78;
        big.ChangeContainerSize(0, 5);
        big.ChangeContainerSize(5, -3);
        Check("p6 storage:the fake holds a size to 0..80, as the engine does", "80/0", $"{big.ContainerSize(0)}/{big.ContainerSize(5)}");
        Check("p6 storage:the check's view reads the same", "40", ((IQuestCheck)s).Player.ContainerSize(0).ToString());
        Check("p6 storage:the quest validates clean", "", string.Join(" | ", QuestScriptValidator.Validate(d)));
    }

    // 2. Settings.Map(name) / Settings.MapFlag(name): xi.settings.map[name], read as Main reads main (a number, a boolean as
    // 1 / 0), and `== true` (the boolean true only: FISHING_ENABLE)
    static void RunP6MapSetting()
    {
        var f = new Fake();
        IQuestContext q = f;
        f.MapBools["FISHING_ENABLE"] = true;
        f.MapBools["OFF"] = false;
        f.MapSettings["FISHING_MIN_LEVEL"] = 3;
        f.MapSettings["ONE"] = 1;
        Check("p6 setting map:Map reads a number, a boolean as 1 / 0, an unset one 0", "1|0|3|1|0",
              string.Join("|", new[] { "FISHING_ENABLE", "OFF", "FISHING_MIN_LEVEL", "ONE", "NOPE" }.Select(n => q.Settings.Map(n))));
        Check("p6 setting map:MapFlag is the boolean true only (a number 1 is not, as the Lua's == true)", "True|False|False|False|False",
              string.Join("|", new[] { "FISHING_ENABLE", "OFF", "FISHING_MIN_LEVEL", "ONE", "NOPE" }.Select(n => q.Settings.MapFlag(n))));
        f.MainSettings["FISHING_ENABLE"] = 0;
        Check("p6 setting map:map and main are apart (main's FISHING_ENABLE 0, map's true)", "0/1/True",
              $"{q.Settings.Main("FISHING_ENABLE")}/{q.Settings.Map("FISHING_ENABLE")}/{q.Settings.MapFlag("FISHING_ENABLE")}");
        // The Gift's offer (Oswald 70): the check's `xi.settings.map.FISHING_ENABLE == true`
        var d = new BuiltQuest(b => b.State(QuestStatus.Available, when: c => c.Settings.MapFlag("FISHING_ENABLE"))
            .Npc(248, "Oswald").OnTrigger(QuestReply.ProgressEvent(70, 4375))).Compile();
        var when = d.States[0].Matches[0].When;
        var off = new Fake();
        off.MapBools["FISHING_ENABLE"] = false;
        Check("p6 setting map:a check reads it (enabled: the offer; disabled or unset: none)", "True/False/False", $"{when(f)}/{when(off)}/{when(new Fake())}");
        Check("p6 setting map:the check's view reads the same", "True|1", $"{((IQuestCheck)f).Settings.MapFlag("FISHING_ENABLE")}|{((IQuestCheck)f).Settings.Map("FISHING_MIN_LEVEL") / 3}");
    }

    // 4. a letterless object name (_ and digits: Norg's Oaken Door _700) has its Npc constant, named as the name; other
    // letterless names (1, 01, _, !) still have none
    static void RunP6LetterlessNpc()
    {
        Check("p6 npc:Npc.Norg._700 and _701 are the object names", "_700/_701", $"{XI.QuestContent.Npc.Norg._700}/{XI.QuestContent.Npc.Norg._701}");
        Check("p6 npc:the Lower Delkfutts doors too", "_540,_541,_542,_543,_544,_545",
              string.Join(",", typeof(XI.QuestContent.Npc.LowerDelkfuttsTower).GetFields().Where(x => x.IsLiteral && System.Text.RegularExpressions.Regex.IsMatch((string)x.GetRawConstantValue(), "^_[0-9]+$"))
                  .Select(x => (string)x.GetRawConstantValue()).OrderBy(v => v, StringComparer.Ordinal)));
        int ByValue(Type t, params string[] values) => t.GetFields().Count(x => x.IsLiteral && values.Contains((string)x.GetRawConstantValue()));
        Check("p6 npc:a name of digits only, _ alone and ! stay without a constant", "0/0/0",
              $"{ByValue(typeof(XI.QuestContent.Npc.Kazham), "1", "2", "3")}/{ByValue(typeof(XI.QuestContent.Npc.PortBastok), "_")}/{ByValue(typeof(XI.QuestContent.Npc.Davoi), "!")}");
        Check("p6 npc:every constant of the shape is its name (no other spelling)", "0",
              typeof(XI.QuestContent.Npc).GetNestedTypes().SelectMany(t => t.GetFields())
                  .Count(x => x.IsLiteral && System.Text.RegularExpressions.Regex.IsMatch((string)x.GetRawConstantValue(), "^_[0-9]+$") && x.Name != (string)x.GetRawConstantValue()).ToString());
    }
}
