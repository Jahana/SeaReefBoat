// XI.Verify.Quests -- quest rework B3b Windurst group 1 (Glyph Hanger, Early Bird Catches the Bookworm, Chasing Tales,
// A Smudge on One's Record, A Pose by Any Other Name): the facet members the group added, on the fake, and the reply
// modifiers. A modifier changes an action's priority in the lookup; where nothing else answers the NPC, the packets are
// the same with it or without it, so a port that dropped one would still replay. The fake shows those (replaceDefault,
// and the importantOnce events talked to once); A Pose's setPriority(101) is also in its trace (below).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;

internal static partial class Program
{
    static void RunWindurstGroup1()
    {
        Console.WriteLine("== quest rework B3b Windurst group 1: facets, reply modifiers ==");
        RunWindurstGroup1Facets();
        RunWindurstGroup1Modifiers();
    }

    // player:getNation(), player:getCurrentMission(log), player:needToZone() / needToZone(true), npcUtil.tradeHasExactly
    static void RunWindurstGroup1Facets()
    {
        var f = new Fake();
        f.NationId = 2;
        f.CurrentMissions[XI.QuestContent.MissionLog.Windurst] = XI.QuestContent.Mission.Windurst.LostForWords;
        string missions = $"{f.Player.Nation}/{f.Player.CurrentMission(XI.QuestContent.MissionLog.Windurst)}/" +
                          $"{f.Player.CurrentMission(XI.QuestContent.MissionLog.Bastok)}";
        bool zoneBefore = f.Player.NeedToZone;
        f.Player.SetNeedToZone(true);
        Check("windurst g1 facets:the fake answers Nation, CurrentMission (0 for a log not set), NeedToZone; records SetNeedToZone",
              $"2/{XI.QuestContent.Mission.Windurst.LostForWords}/0/False/True/Player.SetNeedToZone(True)",
              $"{missions}/{zoneBefore}/{f.Player.NeedToZone}/{string.Join(",", f.Calls)}");

        // npcUtil.tradeHasExactly: the items listed and nothing else (A Smudge's oil and turnip, Early Bird's beastcoin)
        f.TradeItems[XI.QuestContent.Item.VialOfSlimeOil] = 1;
        f.TradeItems[XI.QuestContent.Item.FrostTurnip] = 1;
        bool both = f.Trade.HasExactly(XI.QuestContent.Item.VialOfSlimeOil, XI.QuestContent.Item.FrostTurnip);
        bool oilOnlyListed = f.Trade.HasExactly(XI.QuestContent.Item.VialOfSlimeOil);
        f.TradeItems[XI.QuestContent.Item.BoneChip] = 1;
        bool more = f.Trade.HasExactly(XI.QuestContent.Item.VialOfSlimeOil, XI.QuestContent.Item.FrostTurnip);
        bool moreHas = f.Trade.Has(XI.QuestContent.Item.VialOfSlimeOil, XI.QuestContent.Item.FrostTurnip);
        f.TradeItems.Clear();
        f.TradeItems[XI.QuestContent.Item.VialOfSlimeOil] = 1;
        bool oilAlone = f.Trade.HasExactly(XI.QuestContent.Item.VialOfSlimeOil, XI.QuestContent.Item.FrostTurnip);
        Check("windurst g1 facets:HasExactly: the two yes; one listed of two traded no; the two and a chip no (Has yes); the oil alone no",
              "True/False/False/True/False", $"{both}/{oilOnlyListed}/{more}/{moreHas}/{oilAlone}");
    }

    static void RunWindurstGroup1Modifiers()
    {
        string D(QuestReply r) => r == null ? "null" : QuestContextFake.Describe(r);
        QuestHandlerDecl Trigger(QuestScriptDefinition def, int state, string npc) =>
            def.States[state].Handlers.Single(h => h.Kind == QuestHandlerKind.OnTrigger && h.Entity == npc);
        string Reply(QuestHandlerDecl h, Fake f) => h.Fixed != null ? D(h.Fixed) : D(h.Reply(f));

        // Glyph Hanger's completed 386 and Early Bird's completed 401: replaceDefault, a priority below an event's; nothing
        // else answers Hariga-Origa or Furakku-Norakku there, so the trace is the same with a plain event
        var gh = new XI.QuestContent.Windurst.GlyphHanger().Compile();
        var eb = new XI.QuestContent.Windurst.EarlyBirdCatchesTheBookworm().Compile();
        Check("windurst g1 modifiers:Glyph Hanger's 386 and Early Bird's 401 replace the default",
              "Event(386):replaceDefault()|Event(401):replaceDefault()",
              $"{Reply(Trigger(gh, 2, XI.QuestContent.Npc.WindurstWaters.HarigaOriga), new Fake())}|" +
              $"{Reply(Trigger(eb, 2, XI.QuestContent.Npc.WindurstWaters.FurakkuNorakku), new Fake())}");

        // Chasing Tales' completed section: Furakku's 411 (replaceDefault) while the local var is set, nil without it; the
        // Woods trio's importantOnce (only An Polaali's is seen dropping to Default by a flow, the other two are talked to
        // once)
        var ct = new XI.QuestContent.Windurst.ChasingTales().Compile();
        var withVar = new Fake();
        withVar.PlayerLocalVars["Quest[2][12]mustZone"] = 1;
        Check("windurst g1 modifiers:Chasing Tales' completed Furakku (local var set / not) and the Woods trio",
              "Event(411):replaceDefault()|null|Event(407):importantOnce()|Event(408,0,126):importantOnce()|Event(406):importantOnce()",
              $"{Reply(Trigger(ct, 2, XI.QuestContent.Npc.WindurstWaters.FurakkuNorakku), withVar)}|" +
              $"{Reply(Trigger(ct, 2, XI.QuestContent.Npc.WindurstWaters.FurakkuNorakku), new Fake())}|" +
              $"{Reply(Trigger(ct, 2, XI.QuestContent.Npc.WindurstWoods.AnPolaali), new Fake())}|" +
              $"{Reply(Trigger(ct, 2, XI.QuestContent.Npc.WindurstWoods.AnShanaa), new Fake())}|" +
              $"{Reply(Trigger(ct, 2, XI.QuestContent.Npc.WindurstWoods.HaeJakkya), new Fake())}");

        // A Smudge's completed 418 / 419: importantOnce (Serukoko's 419 is talked to once in the flow)
        var sm = new XI.QuestContent.Windurst.ASmudgeOnOnesRecord().Compile();
        Check("windurst g1 modifiers:A Smudge's completed 418 and 419 are importantOnce",
              "Event(418):importantOnce()|Event(419):importantOnce()",
              $"{Reply(Trigger(sm, 2, XI.QuestContent.Npc.WindurstWaters.HarigaOriga), new Fake())}|" +
              $"{Reply(Trigger(sm, 2, XI.QuestContent.Npc.WindurstWaters.Serukoko), new Fake())}");

        // A Pose: the job's body worn while available, 86 at priority 101; the completed 101 at priority 101 with
        // mustZone, nil without (her script answers). Her script's action has the event priority, so a plain event would
        // tie with it and cycle to her 87: the main flow's second talks (flow lines 28 and 43) show the priority in the
        // trace too, and the fake checks the reply itself.
        var ap = new XI.QuestContent.Windurst.APoseByAnyOtherName().Compile();
        var war = new Fake { MainJob = XI.QuestContent.Job.War };
        war.Equipment[XI.QuestContent.Slot.Body] = XI.QuestContent.Item.BronzeHarness;
        var sam = new Fake { MainJob = XI.QuestContent.Job.Sam };
        sam.Equipment[XI.QuestContent.Slot.Body] = XI.QuestContent.Item.BronzeHarness;
        var zoned = new Fake();
        zoned.MustZone = true;
        Check("windurst g1 modifiers:A Pose's 86 (the job's body: a WAR's harness; a SAM in the harness gets 90) and completed 101 (mustZone / not)",
              "Event(86):setPriority(101)|ProgressEvent(90)|Event(101):setPriority(101)|null",
              $"{Reply(Trigger(ap, 0, XI.QuestContent.Npc.WindurstWaters.Angelica), war)}|" +
              $"{Reply(Trigger(ap, 0, XI.QuestContent.Npc.WindurstWaters.Angelica), sam)}|" +
              $"{Reply(Trigger(ap, 3, XI.QuestContent.Npc.WindurstWaters.Angelica), zoned)}|" +
              $"{Reply(Trigger(ap, 3, XI.QuestContent.Npc.WindurstWaters.Angelica), new Fake())}");
    }
}
