// XI.Verify.Quests -- quest rework B3b Windurst group 20 (call-only: Hoist the Jelly, Roger at Maysoon). The golden
// flows take every arm. These checks pin what no flow can see:
// - the replies' kinds. In an NPC call the bridge renders an Event, a ProgressEvent and a PriorityEvent alike, as
//   player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply: no action is built, so no priority), which is what the
//   Lua script calls; the fake keeps them apart;
// - the trade's trade:getGil() == 0 operand (in game getItemCount already counts the gil slot, as at Chamama, g15).
// W15Handlers, W15Reply and W15GilApartFake are in WindurstGroup15.cs.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup20()
    {
        Console.WriteLine("== quest rework B3b Windurst group 20: the reply kinds (call-only) ==");
        var jelly = new QC.Windurst.HoistTheJellyRoger().Compile();

        Check("windurst g20:Hoist the Jelly, Roger clean", "",
              string.Join(" | ", QuestScriptValidator.Validate(jelly)));

        // Maysoon's trigger replies in declaration order (Cook's Pride accepted and available 10000, else 266)
        Check("windurst g20:the trigger replies are the Lua's startEvent ids, as Event",
              "Maysoon:Event(10000),Maysoon:Event(266)",
              string.Join(",", W15Handlers(jelly, QuestHandlerKind.OnTrigger).Select(h =>
                  $"{h.Entity}:{W15Reply(h, QuestStatus.Available, 1).Reply}")));

        // her trade, accepted: one jelly, then a Bone Chip
        var trade = W15Handlers(jelly, QuestHandlerKind.OnTrade).Single();
        Check("windurst g20:Maysoon's trade: one jelly, then another item, as Event",
              "Event(10001),null",
              string.Join(",", new[] { QC.Item.ServingOfRoyalJelly, QC.Item.BoneChip }.Select(i =>
                  W15Reply(trade, QuestStatus.Accepted, 1, (i, 1)).Reply)));

        // a jelly and gil: trade:getGil() > 0, nothing. In game a gil slot counts in trade:getItemCount()
        // (ZoneHost.Trade.cs), so getItemCount() == 1 already refuses it and no flow sees the operand; a fake whose
        // getItemCount leaves the gil out reads the two apart
        var gilTrade = new W15GilApartFake { Status = QuestStatus.Accepted };
        gilTrade.TradeItems[QC.Item.ServingOfRoyalJelly] = 1;
        gilTrade.TradeItems[65535] = 10;
        var gilReply = trade.Reply(gilTrade);
        Check("windurst g20:Maysoon's trade, a jelly and gil: nothing",
              "null", gilReply == null ? "null" : QuestContextFake.Describe(gilReply));
    }
}
