// XI.Verify.Quests -- quest rework B3b Windurst group 15 (call-only: In a Pickle at Chamama; group 16 is in
// WindurstGroup16.cs). The golden flows take every arm. These checks pin what no flow can see:
// - the replies' kinds. In an NPC call the bridge renders an Event, a ProgressEvent and a PriorityEvent alike, as
//   player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply: no action is built, so no priority), which is what the
//   Lua scripts call; the fake keeps them apart;
// - the trade's trade:getGil() == 0 operand (mutant P008: in game getItemCount already counts the gil slot);
// - the trigger states' conditions, operand for operand (mutants P044, P045, P047: 661's needToZone, var and status
//   operands, shadowed in game by the arms before it).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup15()
    {
        Console.WriteLine("== quest rework B3b Windurst group 15: the reply kinds (call-only) ==");
        var pickle = new QC.Windurst.InAPickle().Compile();

        Check("windurst g15:In a Pickle clean", "", string.Join(" | ", QuestScriptValidator.Validate(pickle)));

        // each trigger reply in declaration order, as her script had them (player:startEvent): each with the draw 1, then
        // 2 (math.randomInt(1, 2); an arm with no draw gives the same reply twice)
        Check("windurst g15:the trigger replies are the Lua's startEvent ids, as Event (draw 1 / draw 2)",
              "Chamama:Event(654,0,4444)/Event(651),Chamama:Event(655,0,4444)/Event(655,0,4444)," +
              "Chamama:Event(660)/Event(660),Chamama:Event(661)/Event(651),Chamama:Event(651)/Event(651)",
              string.Join(",", W15Handlers(pickle, QuestHandlerKind.OnTrigger).Select(h =>
                  $"{h.Entity}:{W15Reply(h, QuestStatus.Available, 1).Reply}/{W15Reply(h, QuestStatus.Available, 2).Reply}")));

        // Chamama's trade, one Smooth Stone: accepted, then completed, with each draw of math.randomInt(1, 4); 657 and 658
        // are started by the handler (player:startEvent, then player:tradeComplete), which answers nil
        var trade = W15Handlers(pickle, QuestHandlerKind.OnTrade).Single();
        string Trades(QuestStatus status) => string.Join(",", Enumerable.Range(1, 4).Select(roll =>
        {
            var (reply, calls) = W15Reply(trade, status, roll, (QC.Item.SmoothStone, 1));
            return reply + (calls.Length > 0 ? "[" + calls + "]" : "");
        }));
        Check("windurst g15:Chamama's trade, one stone: accepted, then completed, draws 1-4",
              "Event(659),Event(659),null[Player.StartEvent(657);Trade.Complete()],null[Player.StartEvent(658);Trade.Complete()]" +
              "|Event(662,200),Event(662,200),null[Player.StartEvent(657);Trade.Complete()],null[Player.StartEvent(658);Trade.Complete()]",
              Trades(QuestStatus.Accepted) + "|" + Trades(QuestStatus.Completed));

        // a stone and gil: trade:getGil() > 0, nothing. In game a gil slot counts in trade:getItemCount()
        // (ZoneHost.Trade.cs), so getItemCount() == 1 already refuses it and no flow sees the operand (mutant round 1
        // survivor P008); a fake whose getItemCount leaves the gil out reads the two apart
        var gilTrade = new W15GilApartFake { Status = QuestStatus.Accepted };
        gilTrade.TradeItems[QC.Item.SmoothStone] = 1;
        gilTrade.TradeItems[65535] = 10;
        var gilReply = trade.Reply(gilTrade);
        Check("windurst g15:Chamama's trade, one stone and gil: nothing",
              "null", gilReply == null ? "null" : QuestContextFake.Describe(gilReply));

        // the trigger states' conditions, the Lua's if-chain operand for operand: a row per state in declaration order,
        // a 1 per case it matches, the cases available / accepted / completed x needToZone off / on x the repeat var
        // 0 / 1 / 2. 661's needToZone and var operands and its completed status are shadowed by the arms before it, so
        // no flow sees them (mutant round 1 survivors P044, P045, P047)
        var cases = (from s in new[] { QuestStatus.Available, QuestStatus.Accepted, QuestStatus.Completed }
                     from z in new[] { false, true }
                     from v in new[] { 0, 1, 2 }
                     select (Status: s, NeedToZone: z, Var: v)).ToArray();
        string Matches(QuestStateDecl state) => string.Concat(cases.Select(c =>
        {
            var f = new Fake { Status = c.Status, ZoningFlag = c.NeedToZone };
            f.CharVars["QuestInAPickle_var"] = c.Var;
            return state.IsMatch(c.Status, 0, f) ? '1' : '0';
        }));
        Check("windurst g15:the trigger states' conditions (available / accepted / completed x needToZone x var 0-2)",
              "111000000000000000|010010111111010010|000000000000000111|000000000000101000|111111111111111111",
              string.Join("|", pickle.States.Where(s => s.Handlers.Any(h => h.Kind == QuestHandlerKind.OnTrigger)).Select(Matches)));
    }

    // trade:getItemCount() without the gil (item 65535): the stone alone counts
    sealed class W15GilApartFake : QuestContextFake
    {
        public override int ItemCount => TradeItems.Where(kv => kv.Key != 65535).Sum(kv => kv.Value);
    }

    static QuestHandlerDecl[] W15Handlers(QuestScriptDefinition def, QuestHandlerKind kind) =>
        def.States.SelectMany(s => s.Handlers).Where(h => h.Kind == kind).ToArray();

    // a handler's reply on a fresh fake (that status, GIL_RATE 1, the draw given, the trade's items), and the calls it
    // made other than its draw and its trade check
    static (string Reply, string Calls) W15Reply(QuestHandlerDecl h, QuestStatus status, int roll, params (int Item, int Qty)[] items)
    {
        var f = new Fake { Status = status };
        f.MainNumbers["GIL_RATE"] = 1;
        f.Rolls.Enqueue(roll);
        foreach (var (item, qty) in items) f.TradeItems[item] = qty;
        var r = h.Fixed ?? h.Reply(f);
        string calls = string.Join(";", f.Calls.Where(c => !c.StartsWith("Player.RandomInt(") && !c.StartsWith("Trade.HasQty(")));
        return (r == null ? "null" : QuestContextFake.Describe(r), calls);
    }
}
