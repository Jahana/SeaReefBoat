// XI.Verify.Quests -- quest rework P5: the platform needs the San d'Oria, Windurst and Jeuno drafts found (replies by
// text id, a fractional setting), on the builder, the fake and the validator. The bridge's side (the Lua action and the
// live context's reads) is in XI.Verify.QuestScripts.Live (PlatformP5.cs).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;

internal static partial class Program
{
    static void RunPlatformP5()
    {
        Console.WriteLine("== quest rework P5: platform needs ==");
        RunP5MainNumber();
        RunP5TextIdReplies();
        RunP5AfterZoneIn();
    }

    // 3. OnAfterZoneIn(zone, q => reply): interaction_lookup's afterZoneIn (luautils::AfterZoneIn, 4 s after the zone-in),
    // a state-level handler with id 1 (as OnZoneIn), its reply an action performed with no NPC (as a trigger area's)
    static void RunP5AfterZoneIn()
    {
        var d = new BuiltQuest(b => b.State(QuestStatus.Accepted, step: 2)
            .Npc(8, "qm_x").OnTrigger(QuestReply.Event(1))
            .OnAfterZoneIn(8, q => q.Quest.Step == 2 ? QuestReply.ProgressEvent(8, 4, 614, 0, 274, 8) : null)
            .OnEventUpdate(8, 8, q => { })).Compile();
        var h = d.States[0].Handlers.Single(x => x.Kind == QuestHandlerKind.OnAfterZoneIn);
        Check("p5 afterZoneIn:a state-level handler of the zone, id 1, a reply (no run, no fixed)", "8/-/1/True/False/False",
              $"{h.Zone}/{h.Entity ?? "-"}/{h.Id}/{h.Reply != null}/{h.Run != null}/{h.Fixed != null}");
        var f = new Fake();
        f.Vars["Prog"] = 2;
        Check("p5 afterZoneIn:its reply in the fake: Prog 2 the event, else none", "ProgressEvent(8,4,614,0,274,8)|null",
              $"{QuestContextFake.Describe(h.Reply(f))}|{(h.Reply(new Fake()) == null ? "null" : "a reply")}");
        Check("p5 afterZoneIn:chained from an NPC builder it is the state's, not the NPC's", "-/1",
              new BuiltQuest(b => b.State(QuestStatus.Accepted).Npc(8, "qm_x").OnTrigger(QuestReply.Event(1)).OnAfterZoneIn(8, q => null)).Compile()
                  .States[0].Handlers.Where(x => x.Kind == QuestHandlerKind.OnAfterZoneIn).Select(x => $"{x.Entity ?? "-"}/{x.Id}").Single());
        Check("p5 afterZoneIn:a null reply is refused", "reply", P5BuildThrows(b => b.State(QuestStatus.Accepted).OnAfterZoneIn(8, null)));
        Check("p5 afterZoneIn:the quest validates clean (Steps declared)", "",
              string.Join(" | ", QuestScriptValidator.Validate(new BuiltQuest(b => { b.Steps(2); b.State(QuestStatus.Accepted, step: 2).OnAfterZoneIn(8, q => null); }).Compile())));
        Check("p5 afterZoneIn:declared twice in one state", "OnAfterZoneIn 1 declared twice in one state",
              string.Join(" | ", QuestScriptValidator.Validate(new BuiltQuest(b => b.State(QuestStatus.Accepted).OnAfterZoneIn(8, q => null).OnAfterZoneIn(8, q => null)).Compile())));
        Check("p5 afterZoneIn:in a call-only quest it is refused (no Zone.lua calls it through xi.cs)",
              "OnAfterZoneIn 1: a called handler is OnTrigger, OnTrade or OnMobDeath on an entity, OnEventFinish, OnEventUpdate or OnZoneIn state-level",
              string.Join(" | ", QuestScriptValidator.Validate(new BuiltQuest(b => b.State(QuestStatus.Available).Npc(235, "Umberto").Called().OnTrigger(QuestReply.Event(1)).OnAfterZoneIn(235, q => null)).Compile())));
        // the lookup performs the action with no NPC: the replies a trigger area takes
        Check("p5 afterZoneIn:a reply the NPC would show is refused, an event / messageSpecial / key item is fine",
              "a MessageText reply needs an NPC to show it, and an afterZoneIn has none|a MessageName reply needs an NPC to show it, and an afterZoneIn has none|" +
              "a Sequence reply needs an NPC to show it, and an afterZoneIn has none|null|null|null|null",
              string.Join("|", new[] { QuestReply.MessageText("X"), QuestReply.MessageName(5), QuestReply.Sequence(QuestReply.MessageText("A")),
                                       QuestReply.ProgressEvent(8, 4), QuestReply.MessageSpecial(5), QuestReply.KeyItem(1), QuestReply.Message(5, 2) }
                  .Select(r => QuestScriptValidator.AfterZoneInReplyProblem(r) ?? "null")));
        Check("p5 afterZoneIn:the trigger area's wording is unchanged", "a MessageText reply needs an NPC to show it, and a trigger area has none",
              QuestScriptValidator.TriggerAreaReplyProblem(QuestReply.MessageText("X")));
        var added = P4Apply(m => m.Quest(P4Key).State(2).AddOnAfterZoneIn(P4Zone, q => null));
        Check("p5 afterZoneIn:a module names its trigger afterZoneIn and may add one", "afterZoneIn|0|1",
              $"{QuestOverrides.Trigger(QuestHandlerKind.OnAfterZoneIn, 1)}|{added.Problems.Count}|" +
              $"{added.Definition?.States[1].Handlers.Count(x => x.Kind == QuestHandlerKind.OnAfterZoneIn && x.Module != null)}");
    }

    static string P5BuildThrows(Action<QuestScriptBuilder> build)
    {
        try
        {
            new BuiltQuest(build).Compile();
            return "no throw";
        }
        catch (ArgumentNullException e)
        {
            return e.ParamName;
        }
    }

    // 4. QuestReply.MessageSpecial(int textId, ...) / MessageName(int textId, ...): a text name plus an offset
    // (quest:messageSpecial(ID.text.THF_AF_WALL_OFFSET + 3, 0, xi.item.GRAPNEL)), which no ZoneText names. The reply
    // carries the id (no text name), its arguments as the named form's, and validates as the named form does
    static void RunP5TextIdReplies()
    {
        var f = new Fake();
        IQuestContext q = f;
        var wall = new ZoneText(120, "THF_AF_WALL_OFFSET");
        f.TextIds[wall.ToString()] = 7300;
        var special = QuestReply.MessageSpecial(q.Text.Id(wall) + 3, 0, 17474);
        var name = QuestReply.MessageName(q.Text.Id(wall) + 1);
        Check("p5 text id:MessageSpecial(id + n, ...) and MessageName(id + n) carry the id, no text name, the args",
              "MessageSpecial 7303 null null 0,17474|MessageName 7301 null null ",
              string.Join("|", new[] { special, name }.Select(r => $"{r.Kind} {r.Id} {r.TextName ?? "null"} {(r.Text?.ToString() ?? "null")} {string.Join(",", r.Args)}")));
        Check("p5 text id:the fake describes them by id", "MessageSpecial(7303,0,17474)|MessageName(7301)",
              $"{QuestContextFake.Describe(special)}|{QuestContextFake.Describe(name)}");
        Check("p5 text id:the named forms are unchanged", "MessageSpecial(THF_AF_WALL_OFFSET)|MessageName(120:THF_AF_WALL_OFFSET,1)",
              $"{QuestContextFake.Describe(QuestReply.MessageSpecial("THF_AF_WALL_OFFSET"))}|{QuestContextFake.Describe(QuestReply.MessageName(wall, 1))}");
        Check("p5 text id:a fixed reply by id validates clean, container and NPC call", "|",
              $"{string.Join(" | ", QuestScriptValidator.Validate(new FixedReplyQuest(special).Compile()))}|{string.Join(" | ", QuestScriptValidator.Validate(new CalledFixedReplyQuest(name).Compile()))}");
        Check("p5 text id:the argument rules are the named form's: messageSpecial's showName (argument 5) is a boolean",
              QuestScriptValidator.ArgsProblem(QuestReply.MessageSpecial("X", 0, 0, 0, 0, "s")),
              QuestScriptValidator.ArgsProblem(QuestReply.MessageSpecial(7303, 0, 0, 0, 0, "s")));
        Check("p5 text id:... and messageName's sender (argument 6) in an NPC call",
              QuestScriptValidator.ArgsProblem(QuestReply.MessageName("X", 0, 0, 0, 0, 1, 2), called: true),
              QuestScriptValidator.ArgsProblem(QuestReply.MessageName(7301, 0, 0, 0, 0, 1, 2), called: true));
        Check("p5 text id:allowed in an NPC call", "null/null",
              $"{QuestScriptValidator.CallReplyProblem(special) ?? "null"}/{QuestScriptValidator.CallReplyProblem(name) ?? "null"}");
    }

    // 5. Settings.MainNumber: xi.settings.main[name] as the Lua number (GIL_RATE 1.5); Main keeps a whole number and rounds
    // a fraction half away from zero, as the live context's ToIntegral (llround) does
    static void RunP5MainNumber()
    {
        var f = new Fake();
        IQuestContext q = f;
        f.MainNumbers["GIL_RATE"] = 1.5;
        f.MainNumbers["BAYLD_RATE"] = 1.4;
        f.MainNumbers["NEG"] = -2.5;
        f.MainSettings["ADVANCED_JOB_LEVEL"] = 30;
        Check("p5 setting:MainNumber reads the fraction, Main rounds it half away from zero", "1.5/2|1.4/1|-2.5/-3",
              string.Join("|", new[] { "GIL_RATE", "BAYLD_RATE", "NEG" }.Select(n => $"{q.Settings.MainNumber(n).ToString(System.Globalization.CultureInfo.InvariantCulture)}/{q.Settings.Main(n)}")));
        Check("p5 setting:a whole number reads the same through both; an unset one is 0", "30/30|0/0",
              $"{q.Settings.MainNumber("ADVANCED_JOB_LEVEL")}/{q.Settings.Main("ADVANCED_JOB_LEVEL")}|{q.Settings.MainNumber("NOPE")}/{q.Settings.Main("NOPE")}");
        Check("p5 setting:400 * GIL_RATE through MainNumber is 600 (The Sweetest Things), through Main 800", "600/800",
              $"{(int)(400 * q.Settings.MainNumber("GIL_RATE"))}/{400 * q.Settings.Main("GIL_RATE")}");
        Check("p5 setting:the check's view reads the same", "1.5", ((IQuestCheck)f).Settings.MainNumber("GIL_RATE").ToString(System.Globalization.CultureInfo.InvariantCulture));
        // a settings source that holds whole numbers only (a module's condition, QuestModule.Branches) gets MainNumber from Main
        IQuestSettings whole = new WholeSettings();
        Check("p5 setting:MainNumber's default is Main's value", "1/0", $"{whole.MainNumber("RESTRICT_CONTENT")}/{whole.MainNumber("X")}");
    }

    sealed class WholeSettings : IQuestSettings
    {
        public int Main(string name) => name == "RESTRICT_CONTENT" ? 1 : 0;
        public int Map(string name) => 0;
        public bool MapFlag(string name) => false;
    }
}
