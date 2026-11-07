// XI.Verify.Quests -- quest rework B3b Aht Urhgan group 7 (Delivering the Goods, Vanishing Act, A Taste of Honey, Arts
// and Crafts, Keeping Notes): each definition validates clean; the declared steps; the reply kinds. The rival flows
// show the kinds that meet another quest's Progress or Event at the same NPC: Fochacha 50, Ulamaal 56 and Qutiba 579 /
// 585 / 514 (the order lines of the main and rival flows), Mhasbaf's 510 (against Led Astray), Zabahf's 512 and
// Ekhu Pesshyadha's 513 (against Got It All and The Die Is Cast). What no flow can flip stays structural, because
// no rival kind is up beside it, or its default event has the same id (Balakaf 515): Qutiba's Delivering the Goods
// (40 / 52) and Vanishing Act (42 / 43 / 48 / 49 / 54 / 45) replies, which only Arts and Crafts at Qutiba can be up
// beside; Mathlouq, Balakaf and Matifa; Hadahda; the trades; and Ahkk Jharcham's NPC, which has no other quest and
// no script. Each pin is the Lua's kind (progressEvent / event).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;
using Z = XI.QuestContent.Zone;

internal static partial class Program
{
    static void RunAhtUrhganGroup7()
    {
        Console.WriteLine("== quest rework B3b Aht Urhgan group 7: the Whitegate merchants and guild askers ==");
        var dtg = new QC.AhtUrhgan.DeliveringTheGoods().Compile();
        var va = new QC.AhtUrhgan.VanishingAct().Compile();
        var atoh = new QC.AhtUrhgan.ATasteOfHoney().Compile();
        var aac = new QC.AhtUrhgan.ArtsAndCrafts().Compile();
        var kn = new QC.AhtUrhgan.KeepingNotes().Compile();
        foreach (var (name, def) in new[] { ("Delivering the Goods", dtg), ("Vanishing Act", va),
                     ("A Taste of Honey", atoh), ("Arts and Crafts", aac), ("Keeping Notes", kn) })
            Check($"ahturhgan g7:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(def)));

        // The steps each quest says it sets (the validator's metadata: no flow can see an extra one declared)
        Check("ahturhgan g7:the declared steps", "1|1,2|||",
              string.Join("|", new[] { dtg, va, atoh, aac, kn }.Select(d => string.Join(",", d.DeclaredSteps))));

        Fake Q(QuestStatus status) => new Fake { Status = status, Step = 0 };
        string ahkk = QC.Npc.AhtUrhganWhitegate.AhkkJharcham;
        var parts = new System.Collections.Generic.List<string>();
        foreach (var s in new[] { QuestStatus.Available, QuestStatus.Accepted, QuestStatus.Completed })
            parts.Add(AU3Npc(kn, QuestHandlerKind.OnTrigger, Z.AhtUrhganWhitegate, ahkk, Q(s)));
        Check("ahturhgan g7:Keeping Notes' reply kinds, as the Lua has them",
              "ProgressEvent(9)|ProgressEvent(10)|Event(12)",
              string.Join("|", parts));

        // The kinds the flows cannot show, structurally: the reply kind and csid of one handler of the quest at
        // one fake state (the vars the branch reads set on the fake). A rival flow shows a kind through the rank
        // rules only where a rival can be up at the same NPC; beside the handlers below none is (or the default
        // event has the same id).
        string Sw = "AhtUrghan/";
        Fake VaTrade() { var f = new Fake { Status = QuestStatus.Accepted, Step = 2, TradeResult = true }; return f; }
        Fake Fk(QuestStatus st, int step = 0) => new Fake { Status = st, Step = step };
        var W = Z.AhtUrhganWhitegate;
        string Qu = QC.Npc.AhtUrhganWhitegate.Qutiba, Ul = QC.Npc.AhtUrhganWhitegate.Ulamaal,
               Fo = QC.Npc.AhtUrhganWhitegate.Fochacha, Ha = QC.Npc.AhtUrhganWhitegate.Hadahda;
        string T(QuestScriptDefinition d, QuestHandlerKind k, string npc, Fake f, ushort zone = 0) =>
            AU3Npc(d, k, zone == 0 ? W : zone, npc, f);
        var lines = new System.Collections.Generic.List<string>();
        void L(string key, string v) => lines.Add(key + "=" + v);
        // Delivering the Goods: gated in sequence (Available, Prog 0, Prog 1): beside it only Arts and Crafts at Qutiba
        L("dtg avail", T(dtg, QuestHandlerKind.OnTrigger, Fo, Fk(QuestStatus.Available)) + "," +
            T(dtg, QuestHandlerKind.OnTrigger, Qu, Fk(QuestStatus.Available)) + "," +
            T(dtg, QuestHandlerKind.OnTrigger, Ul, Fk(QuestStatus.Available)));
        L("dtg prog0", T(dtg, QuestHandlerKind.OnTrigger, Fo, Fk(QuestStatus.Accepted)) + "," +
            T(dtg, QuestHandlerKind.OnTrigger, Qu, Fk(QuestStatus.Accepted)) + "," +
            T(dtg, QuestHandlerKind.OnTrigger, Ul, Fk(QuestStatus.Accepted)));
        L("dtg prog1", T(dtg, QuestHandlerKind.OnTrigger, Fo, Fk(QuestStatus.Accepted, 1)) + "," +
            T(dtg, QuestHandlerKind.OnTrigger, Qu, Fk(QuestStatus.Accepted, 1)) + "," +
            T(dtg, QuestHandlerKind.OnTrigger, Ul, Fk(QuestStatus.Accepted, 1)));
        Fake VaAv(bool zone, int stage = 0)
        {
            var f = Fk(QuestStatus.Available);
            f.CompletedQuests.Add(Sw + "DELIVERING_THE_GOODS");
            if (zone) f.SetNeedToZone(true);
            f.Vars["Stage"] = stage;
            return f;
        }
        L("va avail", string.Join(",", new[] { Qu, Ul }.SelectMany(n => new[] { false, true }
            .Select(z => T(va, QuestHandlerKind.OnTrigger, n, VaAv(z))))) + "," +
            T(va, QuestHandlerKind.OnTrigger, Fo, VaAv(false)));
        L("va avail stage", T(va, QuestHandlerKind.OnTrigger, Qu, VaAv(false, 9)) + "," +
            T(va, QuestHandlerKind.OnTrigger, Ul, VaAv(false, 9)));
        Fake VaAc(int step, bool berry)
        {
            var f = Fk(QuestStatus.Accepted, step);
            f.KeyItems[QC.KeyItem.RainbowBerry] = berry;
            return f;
        }
        L("va acc", string.Join(",", new[] { 0, 1, 2 }
            .Select(st => T(va, QuestHandlerKind.OnTrigger, Fo, VaAc(st, false)))) +
            "," + string.Join(",", new[] { false, true }.SelectMany(b => new[] { Qu, Ul }
                .Select(n => T(va, QuestHandlerKind.OnTrigger, n, VaAc(2, b))))));
        L("va area", AU3Area(va, W, 2, VaAc(1, false)) + "," + AU3Up(va, VaAc(2, false)).Any(x =>
            x.Kind == QuestHandlerKind.OnTriggerAreaEnter && x.Id == 2 && AU1Describe(x, VaAc(2, false)) != "null"));
        var helm = VaTrade();
        L("va 507", AU3Npc(va, QuestHandlerKind.OnTrade, Z.WajaomWoodlands,
            QC.Npc.WajaomWoodlands.HarvestingPoint, helm));
        Fake Tr(QuestStatus st, bool ok) { var f = Fk(st); f.TradeResult = ok; return f; }
        L("atoh trade", T(atoh, QuestHandlerKind.OnTrade, Qu, Tr(QuestStatus.Accepted, true)) + "," +
            T(atoh, QuestHandlerKind.OnTrade, Qu, Tr(QuestStatus.Completed, true)));
        // Arts and Crafts: Hadahda by Prog bits, the craftsmen while their bit is clear, the Sutlac trade at Stage 1.
        // Mhasbaf, Zabahf, Ekhu Pesshyadha and Qutiba share their NPC with a rival the rival flows show.
        Fake AaPr(int prog, int stage = 0)
        {
            var f = Fk(QuestStatus.Accepted, prog);
            f.Vars["Stage"] = stage;
            return f;
        }
        var craft = new[] { (QC.Npc.AhtUrhganWhitegate.Mathlouq, 2), (QC.Npc.AhtUrhganWhitegate.Balakaf, 6),
                            (QC.Npc.AhtUrhganWhitegate.Matifa, 7) };
        L("aac craft", string.Join(",", craft.Select(c => T(aac, QuestHandlerKind.OnTrigger, c.Item1, AaPr(0)))));
        // Balakaf's default event is also 515, so no flow tells his nil from his progress event: each craftsman only
        // on his own clear bit (all the others set), and nil with all seven set
        L("aac craft bit", string.Join(",", craft.Select(c => T(aac, QuestHandlerKind.OnTrigger, c.Item1,
                AaPr(254 - (1 << c.Item2))))) + "," +
            string.Join(",", craft.Select(c => T(aac, QuestHandlerKind.OnTrigger, c.Item1, AaPr(254)))));
        L("aac hadahda", T(aac, QuestHandlerKind.OnTrigger, Ha, Fk(QuestStatus.Available)) + "," +
            T(aac, QuestHandlerKind.OnTrigger, Ha, AaPr(0)) + "," + T(aac, QuestHandlerKind.OnTrigger, Ha, AaPr(254)));
        Fake AaTr(int stage, bool exact)
        {
            var f = AaPr(0, stage);
            if (exact) f.TradeItems[QC.Item.BowlOfSutlac] = 1;
            return f;
        }
        L("aac trade", T(aac, QuestHandlerKind.OnTrade, Ha, AaTr(1, true)));
        // Keeping Notes: the Accepted / Completed trade and the finish confirm
        Fake KnTr(QuestStatus st, params int[] items)
        {
            var f = Fk(st);
            foreach (var it in items) f.TradeItems[it] = 1;
            return f;
        }
        L("kn trade", T(kn, QuestHandlerKind.OnTrade, ahkk, KnTr(QuestStatus.Accepted, QC.Item.SheetOfParchment,
                QC.Item.JarOfBlackInk)) + "," +
            T(kn, QuestHandlerKind.OnTrade, ahkk, KnTr(QuestStatus.Accepted)) + "," +
            T(kn, QuestHandlerKind.OnTrade, ahkk, KnTr(QuestStatus.Completed, QC.Item.SheetOfParchment)) + "," +
            T(kn, QuestHandlerKind.OnTrade, ahkk, KnTr(QuestStatus.Completed, QC.Item.SheetOfParchment,
                QC.Item.JarOfBlackInk)));
        Check("ahturhgan g7:the reply kinds no rival flow can show",
              "dtg avail=ProgressEvent(39),Event(51),Event(51) ; " +
              "dtg prog0=Event(46),ProgressEvent(40),ProgressEvent(40) ; " +
              "dtg prog1=ProgressEvent(41),Event(52),Event(53) ; " +
              "va avail=ProgressEvent(42),Event(52),ProgressEvent(42),Event(53),Event(47) ; " +
              "va avail stage=Event(52),Event(53) ; " +
              "va acc=ProgressEvent(43),Event(48),Event(49),Event(54),Event(54),ProgressEvent(45)," +
              "ProgressEvent(45) ; " +
              "va area=ProgressEvent(44),False ; " +
              "va 507=ProgressEvent(507,{[0]=-776}) ; " +
              "atoh trade=ProgressEvent(580),ProgressEvent(581) ; " +
              "aac craft=ProgressEvent(511),ProgressEvent(515),ProgressEvent(516) ; " +
              "aac craft bit=ProgressEvent(511),ProgressEvent(515),ProgressEvent(516),null,null,null ; " +
              "aac hadahda=ProgressEvent(508),Event(509),ProgressEvent(517) ; " +
              "aac trade=ProgressEvent(573) ; " +
              "kn trade=ProgressEvent(11),Event(14),Event(13),Event(14)",
              string.Join(" ; ", lines));
    }
}
