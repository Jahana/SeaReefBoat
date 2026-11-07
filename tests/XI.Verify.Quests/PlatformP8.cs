// XI.Verify.Quests -- quest rework P8 (C1): an OnZoneIn reply as the table { csid, textTable, flags } (luautils::OnZoneIn
// reads [1] the cutscene, [2] its text table, default -1, [3] its flags, default 0), on the validator, the table form the
// bridge pushes (QuestScriptValidator.ZoneInTable) and the fake. The bridge's side (the container's onZoneIn and the NPC
// call's questZoneIn returning the table) is in XI.Verify.QuestScripts.Live (PlatformP8.cs); the golden proof is
// probe.zone_in_table.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;

internal static partial class Program
{
    static void RunPlatformP8()
    {
        Console.WriteLine("== quest rework P8: the OnZoneIn table reply ==");
        RunP8ZoneInValidator();
        RunP8ZoneInTable();
        RunP8ZoneInHandler();
    }

    // the field a refusal names ("not isHidden"), "none" when the reply passes
    static string P8Named(QuestReply r)
    {
        string p = QuestScriptValidator.ZoneInReplyProblem(r);
        return p == null ? "none" : p.Contains("), ") ? p.Split("), ")[1] : p;
    }

    static string P8Table(QuestReply r) => QuestScriptValidator.ZoneInTable(r) is { } t ? $"{t.Id}/{t.TextTable}/{t.Flags}" : "null";

    // 1. the validator: text_table and flags pass (the two fields OnZoneIn reads), every other named field, an argument, an
    // option or a modifier is refused as before
    static void RunP8ZoneInValidator()
    {
        Check("p8 zoneIn:a cutscene id alone validates", "", QuestScriptValidator.ZoneInReplyProblem(QuestReply.Event(956)) ?? "");
        Check("p8 zoneIn:a text table validates (Such Sweet Sorrow: { 956, 0 })", "",
              QuestScriptValidator.ZoneInReplyProblem(QuestReply.Event(956).TextTable(0)) ?? "");
        Check("p8 zoneIn:a text table and flags validate (I'll Take the Big Box: { 1101, -1, 0x13 })", "",
              QuestScriptValidator.ZoneInReplyProblem(QuestReply.Event(1101).TextTable(-1).Flags(0x13)) ?? "");
        Check("p8 zoneIn:flags alone validate, on a Cutscene too", "",
              QuestScriptValidator.ZoneInReplyProblem(QuestReply.Cutscene(30).Flags(4)) ?? "");
        Check("p8 zoneIn:canSkip is refused (OnZoneIn reads no option table)",
              "a zone-in takes text_table and flags only (the engine's OnZoneIn reads { id, textTable, flags }), not canSkip",
              QuestScriptValidator.ZoneInReplyProblem(QuestReply.Event(956).CanSkip()));
        Check("p8 zoneIn:isHidden and interrupt_text likewise, the first one named", "not isHidden|not interrupt_text",
              $"{P8Named(QuestReply.Event(956).TextTable(0).Hidden())}|" +
              $"{P8Named(QuestReply.Event(956).InterruptText(3).Flags(1))}");
        Check("p8 zoneIn:an argument is still refused, the fields not counted", "a cutscene id takes no arguments here (1 given)",
              QuestScriptValidator.ZoneInReplyProblem(QuestReply.Event(956, 1).TextTable(0)));
        Check("p8 zoneIn:a modifier is still refused with a table", "a cutscene id takes no reply modifiers",
              QuestScriptValidator.ZoneInReplyProblem(QuestReply.Event(956).TextTable(0).OncePerZone())?.Split(" (")[0]);
        Check("p8 zoneIn:a message is still not a cutscene id", "a Message reply is not a cutscene id",
              QuestScriptValidator.ZoneInReplyProblem(QuestReply.Message(7)));
    }

    // 2. the table form: none for a plain id (the bridge pushes the number, as before), else { id, textTable, flags } with
    // OnZoneIn's own defaults (-1, 0) for the one not set
    static void RunP8ZoneInTable()
    {
        Check("p8 zoneIn:a plain id has no table (pushed as a number)", "null", P8Table(QuestReply.Event(956)));
        Check("p8 zoneIn:a text table alone: flags default 0", "956/0/0", P8Table(QuestReply.Event(956).TextTable(0)));
        Check("p8 zoneIn:flags alone: the text table defaults to -1 (the zone's)", "1101/-1/19", P8Table(QuestReply.Event(1101).Flags(0x13)));
        Check("p8 zoneIn:both set, as Lua writes them", "1101/-1/19|30/12/4",
              $"{P8Table(QuestReply.Event(1101).TextTable(-1).Flags(0x13))}|{P8Table(QuestReply.Cutscene(30).TextTable(12).Flags(4))}");
        Check("p8 zoneIn:a field OnZoneIn does not read makes no table", "null", P8Table(QuestReply.Event(956).CanSkip()));
        Check("p8 zoneIn:the fake describes it as the bridge returns it", "956|{956,0,0}|{1101,-1,19}",
              $"{QuestContextFake.DescribeZoneIn(QuestReply.Event(956))}|{QuestContextFake.DescribeZoneIn(QuestReply.Event(956).TextTable(0))}|" +
              $"{QuestContextFake.DescribeZoneIn(QuestReply.Event(1101).TextTable(-1).Flags(0x13))}");
        Check("p8 zoneIn:  and a reply the bridge refuses",
              "refused: a zone-in takes text_table and flags only (the engine's OnZoneIn reads { id, textTable, flags }), not canSkip",
              QuestContextFake.DescribeZoneIn(QuestReply.Event(956).CanSkip()));
    }

    // 3. a quest's OnZoneIn handler giving the table (Such Sweet Sorrow's Whitegate zone-in at Option 2), built, validated
    // and run in the fake
    static void RunP8ZoneInHandler()
    {
        var d = new BuiltQuest(b => b.State(QuestStatus.Available)
            .OnZoneIn(50, q => q.Quest.Var("Option") == 2 ? QuestReply.Event(956).TextTable(0) : null)
            .OnEventFinish(50, 956, q => q.Quest.SetVar("Option", 0))).Compile();
        var h = d.States[0].Handlers.Single(x => x.Kind == QuestHandlerKind.OnZoneIn);
        var f = new Fake();
        f.Vars["Option"] = 2;
        Check("p8 zoneIn:the handler in the fake: Option 2 the table, else none", "{956,0,0}|null",
              $"{QuestContextFake.DescribeZoneIn(h.Reply(f))}|{(h.Reply(new Fake()) == null ? "null" : "a reply")}");
        Check("p8 zoneIn:the quest validates clean", "", string.Join(" | ", QuestScriptValidator.Validate(d)));
        var big = new BuiltQuest(b => { b.Steps(5); b.State(QuestStatus.Accepted, step: 5)
            .OnZoneIn(248, q => QuestReply.Event(1101).TextTable(-1).Flags(0x13)); }).Compile();
        Check("p8 zoneIn:a flags table at a step validates clean (I'll Take the Big Box)", "{1101,-1,19}|",
              $"{QuestContextFake.DescribeZoneIn(big.States[0].Handlers.Single().Reply(new Fake()))}|{string.Join(" | ", QuestScriptValidator.Validate(big))}");
    }
}
