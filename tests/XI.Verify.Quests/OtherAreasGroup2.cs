// XI.Verify.Quests -- quest rework B3b Other Areas group 2 (The Sand Charm, Under the Sea, The Gift, The Real Gift, Elder
// Memories, The Old Lady): the facet member the group added (XPos), on the fake; and the reply kinds no trace can see.
// A reply that only a default or nothing competes with sends the same packets whatever its kind (Progress, Event or
// replaceDefault all beat a default; Blandine, Zexu, Yaya, Jimaida, Isacio and Vera have no script, Zaldon's answers an
// Odontotyrannus only): Blandine's 125 / 124 / 128 / 129, Zexu's 123, Yaya's 31, Jimaida's 33, Isacio's 111 / 119 / 114 /
// 117 (a Progress against The Old Lady's replaceDefault 99 and an Event against it look alike), Vera's 131 / 133 / 132 /
// 137, and every trade reply (127, 35 / 36, 72, 75, 115 / 116 / 117, 135 / 136 / 137): no other quest answers these
// trades. The kinds the traces do see: Celestina's 126 (over her shop script), Oswald's 32 / 37 / 38 / 70 / 71 / 73 / 74 /
// 76 / 78, Zaldon's 34, Isacio's 118 / 99 and Vera's 130 / 138 (the `#kinds` and `#offer` flows).
using System;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunOtherAreasGroup2()
    {
        Console.WriteLine("== quest rework B3b Other Areas group 2: facets, reply kinds ==");
        RunOtherAreasGroup2Facets();
        RunOtherAreasGroup2Kinds();
    }

    // player:getXPos()
    static void RunOtherAreasGroup2Facets()
    {
        var f = new Fake { XPos = 24.5, ZPos = 38 };
        Check("oa g2 facets:the fake answers XPos",
              "24.5|38", $"{f.Player.XPos.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{f.Player.ZPos.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
    }

    // a state's reply (trigger or trade) for the fake
    static string OA2(QuestScriptDefinition def, int state, QuestHandlerKind kind, string npc, Fake f) =>
        G2Reply(G2Handler(def, state, kind, npc), f);

    static Fake OA2Fake(QuestStatus status, int step, params (int Item, int Qty)[] items)
    {
        var f = new Fake { Status = status, Step = step, XPos = 24.6, ZPos = 39.575, MainLevel = 18, TradeResult = true };
        f.MainNumbers["SUBJOB_QUEST_LEVEL"] = 18;
        foreach (var (item, qty) in items) f.TradeItems[item] = qty;
        return f;
    }

    static void RunOtherAreasGroup2Kinds()
    {
        var tsc = new QC.OtherAreas.TheSandCharm().Compile();
        var uts = new QC.OtherAreas.UnderTheSea().Compile();
        var tg = new QC.OtherAreas.TheGift().Compile();
        var trg = new QC.OtherAreas.TheRealGift().Compile();
        var em = new QC.OtherAreas.ElderMemories().Compile();
        var tol = new QC.OtherAreas.TheOldLady().Compile();
        var trig = QuestHandlerKind.OnTrigger;
        var trade = QuestHandlerKind.OnTrade;
        const QuestStatus av = QuestStatus.Available, acc = QuestStatus.Accepted, done = QuestStatus.Completed;

        var dialog1 = OA2Fake(done, 0);
        dialog1.CharVars["BlandineDialog"] = 1;
        Check("oa g2 kinds:The Sand Charm's 125, 124, 123, 127 and 128 are Progress replies; 129 an Event with replaceDefault",
              "ProgressEvent(125)|ProgressEvent(124)|ProgressEvent(123)|ProgressEvent(127,0,13095)|ProgressEvent(128)|Event(129):replaceDefault()",
              $"{OA2(tsc, 0, trig, QC.Npc.Mhaura.Blandine, OA2Fake(av, 0))}|" +
              $"{OA2(tsc, 0, trig, QC.Npc.Mhaura.Blandine, OA2Fake(av, 2))}|" +
              $"{OA2(tsc, 0, trig, QC.Npc.Mhaura.Zexu, OA2Fake(av, 1))}|" +
              $"{OA2(tsc, 1, trade, QC.Npc.Mhaura.Celestina, OA2Fake(acc, 0, (QC.Item.SandCharm, 1)))}|" +
              $"{OA2(tsc, 2, trig, QC.Npc.Mhaura.Blandine, dialog1)}|" +
              $"{OA2(tsc, 2, trig, QC.Npc.Mhaura.Blandine, OA2Fake(done, 0))}");

        // Zaldon's draw: math.randomInt(1, 100) <= 20, the edge on both sides
        var roll20 = OA2Fake(acc, 3, (QC.Item.FatGreedie, 1));
        roll20.Rolls.Enqueue(20);
        var roll21 = OA2Fake(acc, 3, (QC.Item.FatGreedie, 1));
        roll21.Rolls.Enqueue(21);
        Check("oa g2 kinds:Under the Sea's 31, 33 and 35 are Progress replies, 36 an Event (a draw of 20 and 21)",
              "ProgressEvent(31)|ProgressEvent(33)|ProgressEvent(35)|Event(36)",
              $"{OA2(uts, 0, trig, QC.Npc.Selbina.Yaya, OA2Fake(av, 0))}|" +
              $"{OA2(uts, 1, trig, QC.Npc.Selbina.Jimaida, OA2Fake(acc, 1))}|" +
              $"{OA2(uts, 1, trade, QC.Npc.Selbina.Zaldon, roll20)}|" +
              $"{OA2(uts, 1, trade, QC.Npc.Selbina.Zaldon, roll21)}");

        Check("oa g2 kinds:The Gift's 72 and The Real Gift's 75 are Progress replies",
              "ProgressEvent(72,0,4375)|ProgressEvent(75)",
              $"{OA2(tg, 1, trade, QC.Npc.Selbina.Oswald, OA2Fake(acc, 0, (QC.Item.Danceshroom, 1)))}|" +
              $"{OA2(trg, 1, trade, QC.Npc.Selbina.Oswald, OA2Fake(acc, 0, (QC.Item.ShallShell, 1)))}");

        var low = OA2Fake(av, 0);
        low.MainLevel = 17;
        var letter = OA2Fake(acc, 0);
        letter.KeyItems[QC.KeyItem.GilgameshsIntroductoryLetter] = true;
        Check("oa g2 kinds:Elder Memories' 111, 117 and the trades' 115, 116, 117 are Progress replies; 119 and 114 Event replies",
              "ProgressEvent(111,538)|Event(119)|Event(114,538)|ProgressEvent(117)|ProgressEvent(115,537)|ProgressEvent(116,539)|ProgressEvent(117)",
              $"{OA2(em, 0, trig, QC.Npc.Selbina.Isacio, OA2Fake(av, 0))}|" +
              $"{OA2(em, 0, trig, QC.Npc.Selbina.Isacio, low)}|" +
              $"{OA2(em, 1, trig, QC.Npc.Selbina.Isacio, OA2Fake(acc, 0))}|" +
              $"{OA2(em, 1, trig, QC.Npc.Selbina.Isacio, letter)}|" +
              $"{OA2(em, 1, trade, QC.Npc.Selbina.Isacio, OA2Fake(acc, 0))}|" +
              $"{OA2(em, 1, trade, QC.Npc.Selbina.Isacio, OA2Fake(acc, 1))}|" +
              $"{OA2(em, 1, trade, QC.Npc.Selbina.Isacio, OA2Fake(acc, 2))}");

        Check("oa g2 kinds:The Old Lady's 131, 137 and the trades' 135, 136, 137 are Progress replies; 133 and 132 Event replies",
              "ProgressEvent(131,542)|Event(133)|Event(132,542)|ProgressEvent(137)|ProgressEvent(135,541)|ProgressEvent(136,540)|ProgressEvent(137)",
              $"{OA2(tol, 0, trig, QC.Npc.Mhaura.Vera, OA2Fake(av, 0))}|" +
              $"{OA2(tol, 0, trig, QC.Npc.Mhaura.Vera, low)}|" +
              $"{OA2(tol, 1, trig, QC.Npc.Mhaura.Vera, OA2Fake(acc, 0))}|" +
              $"{OA2(tol, 1, trig, QC.Npc.Mhaura.Vera, letter)}|" +
              $"{OA2(tol, 1, trade, QC.Npc.Mhaura.Vera, OA2Fake(acc, 0))}|" +
              $"{OA2(tol, 1, trade, QC.Npc.Mhaura.Vera, OA2Fake(acc, 1))}|" +
              $"{OA2(tol, 1, trade, QC.Npc.Mhaura.Vera, OA2Fake(acc, 2))}");
    }
}
