// XI.Verify.Quests -- quest rework B3b Windurst group 21 (call-only: Making Amens! at Kuroido-Moido and Mashira, Wonder
// Wands at Hakkuru-Rinkuru, Goltata and Yaman-Hachuman, Catch It If You Can at Ohruru). The golden flows take every
// arm. These checks pin what no flow can see:
// - the replies' kinds. In an NPC call the bridge renders an Event, a ProgressEvent and a PriorityEvent alike, as
//   player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply: no action is built, so no priority), which is what the
//   Lua scripts call; the fake keeps them apart;
// - the available state's replies by the var (253, 231) and the `== Completed` of the states after a Wonder Wands
//   accepted state (Kuroido-Moido's 266, Ohruru's 265), which no flow reads apart from `!= Available`;
// - Catch It If You Can's 246 finish with none of Mute, Bane or Plague left (an effect that wears off while the event
//   is open): needToZone, the var and the fame alone, no gil. No flow statement takes an effect away inside an event.
// W15Handlers and W15Reply are in WindurstGroup15.cs, W10Finish in WindurstGroup10.cs.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup21()
    {
        Console.WriteLine("== quest rework B3b Windurst group 21: the reply kinds, 246 with no effect (call-only) ==");
        var amens = new QC.Windurst.MakingAmens().Compile();
        var wands = new QC.Windurst.WonderWands().Compile();
        var catchIt = new QC.Windurst.CatchItIfYouCan().Compile();

        Check("windurst g21:Making Amens! clean", "", string.Join(" | ", QuestScriptValidator.Validate(amens)));
        Check("windurst g21:Wonder Wands clean", "", string.Join(" | ", QuestScriptValidator.Validate(wands)));
        Check("windurst g21:Catch It If You Can clean", "", string.Join(" | ", QuestScriptValidator.Validate(catchIt)));

        string Triggers(QuestScriptDefinition def, QuestStatus status, int roll) =>
            string.Join(",", W15Handlers(def, QuestHandlerKind.OnTrigger).Select(h =>
                $"{h.Entity}:{W15Reply(h, status, roll).Reply}"));

        // the trigger replies in declaration order (GIL_RATE 1: 284's 6000)
        Check("windurst g21:Making Amens!'s trigger replies are the Lua's startEvent ids, as Event",
              "Kuroido-Moido:Event(280),Kuroido-Moido:NoAction(),Kuroido-Moido:Event(283),Mashira:Event(11,0)," +
              "Kuroido-Moido:Event(284,6000),Kuroido-Moido:Event(261),Kuroido-Moido:Event(266)," +
              "Kuroido-Moido:Event(286,0,937)",
              Triggers(amens, QuestStatus.Accepted, 1));
        Check("windurst g21:Wonder Wands' trigger replies are the Lua's startEvent ids, as Event",
              "Hakkuru-Rinkuru:Event(259),Hakkuru-Rinkuru:Event(260),Goltata:Event(257,0,0,17091)," +
              "Yaman-Hachuman:Event(256,0,0,0,17061),Hakkuru-Rinkuru:Event(267),Goltata:Event(269)," +
              "Yaman-Hachuman:Event(268)",
              Triggers(wands, QuestStatus.Accepted, 1));
        // the available state's lambda on QuestCatchItIfYouCan_var 0, the accepted roll 1 then 2
        Check("windurst g21:Catch It If You Can's trigger replies are the Lua's startEvent ids, as Event",
              "Ohruru:Event(258,0,17053),Ohruru:Event(230),Ohruru:Event(246),Ohruru:Event(255),Ohruru:Event(248)," +
              "Ohruru:Event(265)|Ohruru:Event(251)",
              Triggers(catchIt, QuestStatus.Accepted, 1) + "|" + string.Join(",",
                  W15Handlers(catchIt, QuestHandlerKind.OnTrigger).Where(h => h.Fixed == null).Skip(1).Select(h =>
                      $"{h.Entity}:{W15Reply(h, QuestStatus.Accepted, 2).Reply}")));

        // Ohruru's available state on QuestCatchItIfYouCan_var 0 to 3 (the reply, then the var writes): the kinds of
        // 253 and 231 (mutant round 1 survivors C183, C187)
        var available = W15Handlers(catchIt, QuestHandlerKind.OnTrigger).First(h => h.Fixed == null);
        Check("windurst g21:Ohruru's available replies by the var, as Event",
              "Event(230)[Player.SetCharVar(QuestCatchItIfYouCan_var,1,0)]," +
              "Event(253)[Player.SetCharVar(QuestCatchItIfYouCan_var,2,0)]," +
              "Event(231),NoAction()",
              string.Join(",", Enumerable.Range(0, 4).Select(v =>
              {
                  var f = new Fake { Status = QuestStatus.Available };
                  f.CharVars["QuestCatchItIfYouCan_var"] = v;
                  var r = available.Reply(f);
                  string calls = string.Join(";", f.Calls.Where(c => c.StartsWith("Player.SetCharVar(")));
                  return QuestContextFake.Describe(r) + (calls.Length > 0 ? "[" + calls + "]" : "");
              })));

        // the states that read Wonder Wands' status, against Wonder Wands available / accepted / completed (this quest
        // completed): Kuroido-Moido's 261 and 266, Ohruru's 258 and 265. 266's and 265's `== Completed` follow a state
        // that takes Wonder Wands accepted, so no flow reads them apart from `!= Available` (mutant round 1 survivors
        // A035, C216)
        string ByWonderWands(QuestScriptDefinition def, params int[] csids) => string.Join("|", csids.Select(csid =>
        {
            var state = def.States.Single(s => s.Handlers.Any(h => h.Kind == QuestHandlerKind.OnTrigger &&
                                                                   h.Fixed is { } r && r.Id == csid));
            var wws = new[] { QuestStatus.Available, QuestStatus.Accepted, QuestStatus.Completed };
            return string.Concat(wws.Select(ww =>
            {
                var f = new Fake { Status = QuestStatus.Completed };
                f.QuestStatuses["Windurst/WONDER_WANDS"] = ww;
                return state.IsMatch(QuestStatus.Completed, 0, f) ? '1' : '0';
            }));
        }));
        Check("windurst g21:the states reading Wonder Wands (available / accepted / completed): 261, 266, 258, 265",
              "010|001|010|001", ByWonderWands(amens, 261, 266) + "|" + ByWonderWands(catchIt, 258, 265));

        // Hakkuru-Rinkuru's trade, accepted: the three, then the glue alone
        var trade = W15Handlers(wands, QuestHandlerKind.OnTrade).Single();
        Check("windurst g21:Hakkuru-Rinkuru's trade: the three, then another item, as Event",
              "Event(265,0,17091,17061,17053),Event(260,0,17091,17061,17053)",
              W15Reply(trade, QuestStatus.Accepted, 1,
                       (QC.Item.OakStaff, 1), (QC.Item.MythrilRod, 1), (QC.Item.RoseWand, 1)).Reply +
              "," + W15Reply(trade, QuestStatus.Accepted, 1, (QC.Item.BlockOfAnimalGlue, 1)).Reply);

        // 246's finish, option 0, none of the three effects left: accepted, then completed
        Check("windurst g21:Ohruru's 246, option 0, no effect left: needToZone, var 0, fame, no gil",
              "Player.SetNeedToZone(True);Player.SetCharVar(QuestCatchItIfYouCan_var,0,0);" +
              "Player.CompleteQuest(Windurst,CATCH_IT_IF_YOU_CAN);" +
              "Player.AddFame(2,10)" +
              "|Player.SetNeedToZone(True);Player.SetCharVar(QuestCatchItIfYouCan_var,0,0);Player.AddFame(2,10)",
              W10Finish(catchIt, 246, 0, true) + "|" +
              W10Finish(catchIt, 246, 0, true, f => f.Status = QuestStatus.Completed));
    }
}
