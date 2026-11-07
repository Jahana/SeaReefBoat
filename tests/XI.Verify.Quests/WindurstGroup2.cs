// XI.Verify.Quests -- quest rework B3b Windurst group 2 (Food for Thought, Overnight Delivery, Water Way to Go, Say It
// with Flowers): the facet members the group added, on the fake; the reply modifiers no trace can see (a replaceDefault
// on an NPC nothing else answers sends the packets a plain event sends); the random draws' edges, which the pinned seed
// takes on one side only per talk (the traces take each side, but not at the edge value); and Say It with Flowers'
// flowerList, of which the traces buy one flower.
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup2()
    {
        Console.WriteLine("== quest rework B3b Windurst group 2: facets, reply modifiers, draws, flowerList ==");
        RunWindurstGroup2Facets();
        RunWindurstGroup2Modifiers();
        RunWindurstGroup2Draws();
        RunWindurstGroup2FlowerList();
    }

    static string G2Describe(QuestReply r) => r == null ? "null" : QuestContextFake.Describe(r);

    static QuestHandlerDecl G2Handler(QuestScriptDefinition def, int state, QuestHandlerKind kind, string npc, int id = 0) =>
        def.States[state].Handlers.Single(h => h.Kind == kind && (npc == null || h.Entity == npc) && (id == 0 || h.Id == id));

    static string G2Reply(QuestHandlerDecl h, Fake f) => h.Fixed != null ? G2Describe(h.Fixed) : G2Describe(h.Reply(f));

    // player:findItem, VanadielHour(), player:getGil / addGil / delGil, npcUtil.giveItem with params, player:messageText
    static void RunWindurstGroup2Facets()
    {
        var f = new Fake();
        f.Items.Add(QC.Item.RhinosteryCanteen);
        f.Hour = 18;
        f.GilHeld = 150;
        string reads = $"{f.Player.FindItem(QC.Item.RhinosteryCanteen)}/{f.Player.FindItem(QC.Item.CanteenOfGiddeusWater)}/" +
                       $"{f.Player.VanadielHour}/{f.Player.Gil}";
        f.Player.AddGil(100);
        f.Player.DelGil(150);
        bool given = f.Player.GiveItem(QC.Item.TahrongiCactus, true, false);
        f.Text.MessageText(QC.Text.WindurstWaters.WHAT_DO_YOU_MEAN, true, 0);
        Check("windurst g2 facets:the fake answers FindItem (Items), VanadielHour, Gil; AddGil / DelGil move it; GiveItem with params and MessageText are recorded",
              "True/False/18/150|100|True|Player.AddGil(100);Player.DelGil(150);Player.GiveItem(950,True,False);Text.MessageText(238:WHAT_DO_YOU_MEAN,True,0)",
              $"{reads}|{f.Player.Gil}|{given}|{string.Join(";", f.Calls)}");
    }

    static void RunWindurstGroup2Modifiers()
    {
        var fft = new QC.Windurst.FoodForThought().Compile();
        var od = new QC.Windurst.OvernightDelivery().Compile();

        // Food for Thought's completed section: Kerutoto's 304 and Ohbiru's 344, replaceDefault, while the mustZone holds.
        // Nothing else answers either NPC there (no scripts), and 344 is Ohbiru's own default: the trace cannot tell
        // either from a plain event, nor Ohbiru's 344 from no reply at all, so the state's check is shown too.
        var zoned = new Fake { Status = QuestStatus.Completed, MustZone = true };
        var notZoned = new Fake { Status = QuestStatus.Completed };
        Check("windurst g2 modifiers:Food for Thought's completed 304 / 344 replace the default, only with mustZone",
              "Event(304):replaceDefault()|Event(344):replaceDefault()|True|False",
              $"{G2Reply(G2Handler(fft, 2, QuestHandlerKind.OnTrigger, QC.Npc.WindurstWaters.Kerutoto), zoned)}|" +
              $"{G2Reply(G2Handler(fft, 2, QuestHandlerKind.OnTrigger, QC.Npc.WindurstWaters.OhbiruDohbiru), zoned)}|" +
              $"{fft.States[2].IsMatch(QuestStatus.Completed, 0, zoned)}|{fft.States[2].IsMatch(QuestStatus.Completed, 0, notZoned)}");

        // Overnight Delivery's completed section: Kotan's 143 (nothing else answers him), Ohbiru's 351 below fame level 6
        // (nil from level 6: the `< 6` edge), Kenapa's 349 / 350 (his script alternates with an event as with a
        // replaceDefault, so the trace cannot tell them apart)
        var level5 = new Fake { Status = QuestStatus.Completed };
        level5.Fame[QC.FameArea.Windurst] = 5;
        var level6 = new Fake { Status = QuestStatus.Completed };
        level6.Fame[QC.FameArea.Windurst] = 6;
        var draws = new Fake { Status = QuestStatus.Completed };
        draws.Rolls.Enqueue(50);
        draws.Rolls.Enqueue(51);
        var kenapa = G2Handler(od, 2, QuestHandlerKind.OnTrigger, QC.Npc.WindurstWaters.KenapaKeppa);
        Check("windurst g2 modifiers:Overnight Delivery's completed 143, 351 (level 5 / level 6) and 349 / 350 (rolls 50 / 51) replace the default",
              "Event(143):replaceDefault()|Event(351):replaceDefault()|null|Event(349):replaceDefault()|Event(350):replaceDefault()",
              $"{G2Reply(G2Handler(od, 2, QuestHandlerKind.OnTrigger, QC.Npc.Mhaura.KotanPurutan), new Fake())}|" +
              $"{G2Reply(G2Handler(od, 2, QuestHandlerKind.OnTrigger, QC.Npc.WindurstWaters.OhbiruDohbiru), level5)}|" +
              $"{G2Reply(G2Handler(od, 2, QuestHandlerKind.OnTrigger, QC.Npc.WindurstWaters.OhbiruDohbiru), level6)}|" +
              $"{G2Reply(kenapa, draws)}|{G2Reply(kenapa, draws)}");
    }

    // the draws: math.randomInt(1, 100) <= 50 and math.randomInt(1, 3), at their edges (the traces take each side with the
    // values the pinned seed drew, which are not the edges)
    static void RunWindurstGroup2Draws()
    {
        var fft = new QC.Windurst.FoodForThought().Compile();
        var kenapaTrade = G2Handler(fft, 1, QuestHandlerKind.OnTrade, QC.Npc.WindurstWaters.KenapaKeppa);
        var ohbiruTrade = G2Handler(fft, 1, QuestHandlerKind.OnTrade, QC.Npc.WindurstWaters.OhbiruDohbiru);
        var kenapaTrigger = G2Handler(fft, 1, QuestHandlerKind.OnTrigger, QC.Npc.WindurstWaters.KenapaKeppa);

        var egg = new Fake { Status = QuestStatus.Accepted };
        egg.TradeItems[QC.Item.HardBoiledEgg] = 1;
        egg.Rolls.Enqueue(50);
        egg.Rolls.Enqueue(51);
        var three = new Fake { Status = QuestStatus.Accepted };
        three.Vars["ohbiruProg"] = 1;
        three.TradeItems[QC.Item.CupOfWindurstianTea] = 1;
        three.TradeItems[QC.Item.Tortilla] = 1;
        three.TradeItems[QC.Item.ClumpOfPamtamKelp] = 1;
        three.Rolls.Enqueue(50);
        three.Rolls.Enqueue(51);
        var order = new Fake { Status = QuestStatus.Accepted };
        order.Vars["kenapaProg"] = 3;
        order.Rolls.Enqueue(1);
        order.Rolls.Enqueue(2);
        order.Rolls.Enqueue(3);
        Check("windurst g2 draws:Food for Thought: the egg early (50 / 51), the three early (50 / 51), kenapaProg 3 (1 / 2 / 3)",
              "ProgressEvent(331)|ProgressEvent(330,120)|ProgressEvent(325,440)|ProgressEvent(326)|" +
              "ProgressEvent(320,0,4409)|ProgressEvent(321)|ProgressEvent(328)",
              $"{G2Reply(kenapaTrade, egg)}|{G2Reply(kenapaTrade, egg)}|{G2Reply(ohbiruTrade, three)}|{G2Reply(ohbiruTrade, three)}|" +
              $"{G2Reply(kenapaTrigger, order)}|{G2Reply(kenapaTrigger, order)}|{G2Reply(kenapaTrigger, order)}");
    }

    // Say It with Flowers' 516: each flowerList row (the item given and the gil taken, with exactly its price held), one
    // gil short (WHAT_DO_YOU_MEAN), option 6 (no row: WHAT_DO_YOU_MEAN), 7 (Option 1) and 8 (nothing)
    static void RunWindurstGroup2FlowerList()
    {
        var siwf = new QC.Windurst.SayItWithFlowers().Compile();
        var finish516 = G2Handler(siwf, 1, QuestHandlerKind.OnEventFinish, null, 516);
        var rows = new List<string>();
        int[] prices = { 300, 200, 250, 150, 200, 100 };
        for (int option = 0; option <= 8; option++)
        {
            var exact = new Fake { Status = QuestStatus.Accepted, Option = option, GilHeld = option < prices.Length ? prices[option] : 1000 };
            finish516.Run(exact);
            rows.Add($"{option}:{string.Join(";", exact.Calls)}");
        }
        var shortOne = new Fake { Status = QuestStatus.Accepted, Option = 5, GilHeld = 99 };
        finish516.Run(shortOne);
        rows.Add($"short:{string.Join(";", shortOne.Calls)}");
        Check("windurst g2 flowerList:516 options 0-8 with exactly the price held, and option 5 one gil short",
              "0:Player.GiveItem(948);Player.DelGil(300);Quest.SetMustZone()|" +
              "1:Player.GiveItem(941);Player.DelGil(200);Quest.SetMustZone()|" +
              "2:Player.GiveItem(949);Player.DelGil(250);Quest.SetMustZone()|" +
              "3:Player.GiveItem(956);Player.DelGil(150);Quest.SetMustZone()|" +
              "4:Player.GiveItem(957);Player.DelGil(200);Quest.SetMustZone()|" +
              "5:Player.GiveItem(958);Player.DelGil(100);Quest.SetMustZone()|" +
              "6:Text.MessageText(238:WHAT_DO_YOU_MEAN,True,0)|" +
              "7:Quest.SetVar(Option,1,0)|" +
              "8:|" +
              "short:Text.MessageText(238:WHAT_DO_YOU_MEAN,True,0)",
              string.Join("|", rows));
    }
}
