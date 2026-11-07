// XI.Verify.Quests -- quest rework B3b Aht Urhgan group 10 (Olduum, Such Sweet Sorrow, The Prankster, The Prince and
// the Hopper, Striking a Balance): each definition validates clean; the declared steps; the reply kinds that no
// flow can show: the NPCs and areas with one quest container and no script, and the zone-in replies (two pin checks).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunAhtUrhganGroup10()
    {
        Console.WriteLine("== quest rework B3b Aht Urhgan group 10: Whitegate, Mamook and Bhaflau ==");
        var ol = new QC.AhtUrhgan.Olduum().Compile();
        var ss = new QC.AhtUrhgan.SuchSweetSorrow().Compile();
        var pk = new QC.AhtUrhgan.ThePrankster().Compile();
        var ph = new QC.AhtUrhgan.ThePrinceAndTheHopper().Compile();
        var sb = new QC.AhtUrhgan.StrikingABalance().Compile();
        foreach (var (name, def) in new[] { ("Olduum", ol), ("Such Sweet Sorrow", ss), ("The Prankster", pk),
                     ("The Prince and the Hopper", ph), ("Striking a Balance", sb) })
            Check($"ahturhgan g10:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(def)));

        // The steps each quest says it sets (the validator's metadata: no flow can see an extra one declared)
        Check("ahturhgan g10:the declared steps", "0,1,2,3||1,2,3|1,2,3,4,5,6,7|1,2,3,4,5",
              string.Join("|", new[] { ol, ss, pk, ph, sb }.Select(d => string.Join(",", d.DeclaredSteps))));
        // The reply kinds no flow can show. A Progress beats an Event and equal kinds rotate, so a kind shows only
        // against a rival container or an NPC script; none of these NPCs or areas has either: each is the only quest
        // container at an NPC with no script (its default action, where it has one, ranks below every quest reply) or
        // at a trigger area no other container answers. Area 12 is not here: Promotion: First Lieutenant's 5080 shares
        // it, and the_prankster#rivals rotates The Prankster's 3 with it. Each pin is the Lua's kind.
        var lines = new System.Collections.Generic.List<string>();
        void L(string key, string v) => lines.Add(key + "=" + v);
        Fake Fk(QuestStatus st, int step = 0) => new Fake { Status = st, Step = step };
        Fake Opt(int option) { var f = Fk(QuestStatus.Available); f.QuestLocalVars["Option"] = option; return f; }
        Fake Cell() { var f = Fk(QuestStatus.Accepted); f.KeyItems[(int)QC.KeyItem.Electrocell] = true; return f; }
        ushort Aw = QC.Zone.AhtUrhganWhitegate, Mk = QC.Zone.Mamook, Bt = QC.Zone.BhaflauThickets;
        const QuestHandlerKind Tr = QuestHandlerKind.OnTrigger;
        L("ol dkhaaya avail", AU3Npc(ol, Tr, Aw, QC.Npc.AhtUrhganWhitegate.Dkhaaya, Fk(QuestStatus.Available)));
        L("ol dkhaaya none", AU3Npc(ol, Tr, Aw, QC.Npc.AhtUrhganWhitegate.Dkhaaya, Fk(QuestStatus.Accepted)));
        L("ol dkhaaya cell", AU3Npc(ol, Tr, Aw, QC.Npc.AhtUrhganWhitegate.Dkhaaya, Cell()));
        L("ol dkhaaya done", AU3Npc(ol, Tr, Aw, QC.Npc.AhtUrhganWhitegate.Dkhaaya, Fk(QuestStatus.Completed)));
        L("ss dabhuh avail", AU3Npc(ss, Tr, Aw, QC.Npc.AhtUrhganWhitegate.Dabhuh, Fk(QuestStatus.Available)));
        L("ss dabhuh accepted", AU3Npc(ss, Tr, Aw, QC.Npc.AhtUrhganWhitegate.Dabhuh, Fk(QuestStatus.Accepted)));
        L("ss dabhuh done", AU3Npc(ss, Tr, Aw, QC.Npc.AhtUrhganWhitegate.Dabhuh, Fk(QuestStatus.Completed)));
        L("pk ahaadah avail", AU3Npc(pk, Tr, Aw, QC.Npc.AhtUrhganWhitegate.Ahaadah, Fk(QuestStatus.Available)));
        foreach (var s in new[] { 0, 1, 2, 3 })
            L("pk ahaadah " + s, AU3Npc(pk, Tr, Aw, QC.Npc.AhtUrhganWhitegate.Ahaadah, Fk(QuestStatus.Accepted, s)));
        L("pk ahaadah done", AU3Npc(pk, Tr, Aw, QC.Npc.AhtUrhganWhitegate.Ahaadah, Fk(QuestStatus.Completed)));
        L("pk area11", AU3Area(pk, Aw, 11, Fk(QuestStatus.Accepted, 0)));
        L("pk qm3 2", AU3Npc(pk, Tr, Bt, QC.Npc.BhaflauThickets.qm3, Fk(QuestStatus.Accepted, 2)));
        L("pk qm3 3", AU3Npc(pk, Tr, Bt, QC.Npc.BhaflauThickets.qm3, Fk(QuestStatus.Accepted, 3)));
        L("ph maudaal avail", AU3Npc(ph, Tr, Aw, QC.Npc.AhtUrhganWhitegate.Maudaal, Fk(QuestStatus.Available)));
        L("ph maudaal 7", AU3Npc(ph, Tr, Aw, QC.Npc.AhtUrhganWhitegate.Maudaal, Fk(QuestStatus.Accepted, 7)));
        L("ph footprint2 1", AU3Npc(ph, Tr, Mk, QC.Npc.Mamook.ToadsFootprint2, Fk(QuestStatus.Accepted, 1)));
        L("ph footprint1 4", AU3Npc(ph, Tr, Mk, QC.Npc.Mamook.ToadsFootprint1, Fk(QuestStatus.Accepted, 4)));
        L("ph footprint1 6", AU3Npc(ph, Tr, Mk, QC.Npc.Mamook.ToadsFootprint1, Fk(QuestStatus.Accepted, 6)));
        L("sb wazyih avail 0", AU3Npc(sb, Tr, Aw, QC.Npc.AhtUrhganWhitegate.Wazyih, Opt(0)));
        L("sb wazyih avail 1", AU3Npc(sb, Tr, Aw, QC.Npc.AhtUrhganWhitegate.Wazyih, Opt(1)));
        L("sb saliyahf avail", AU3Npc(sb, Tr, Aw, QC.Npc.AhtUrhganWhitegate.Saliyahf, Fk(QuestStatus.Available)));
        foreach (var s in new[] { 0, 1, 2, 3, 5 })
        {
            L("sb wazyih " + s, AU3Npc(sb, Tr, Aw, QC.Npc.AhtUrhganWhitegate.Wazyih, Fk(QuestStatus.Accepted, s)));
            L("sb saliyahf " + s, AU3Npc(sb, Tr, Aw, QC.Npc.AhtUrhganWhitegate.Saliyahf, Fk(QuestStatus.Accepted, s)));
        }
        L("sb area9", AU3Area(sb, Aw, 9, Fk(QuestStatus.Accepted, 1)));
        Check("ahturhgan g10:the reply kinds no flow can show",
              "ol dkhaaya avail=ProgressEvent(4) ; ol dkhaaya none=Event(5) ; " +
              "ol dkhaaya cell=ProgressEvent(6,{[0]=768}) ; ol dkhaaya done=ProgressEvent(7,{[7]=1}) ; " +
              "ss dabhuh avail=ProgressEvent(582,{text_table=0}) ; " +
              "ss dabhuh accepted=Event(584,{text_table=0}) ; ss dabhuh done=Event(584,{text_table=0}) ; " +
              "pk ahaadah avail=ProgressEvent(1) ; pk ahaadah 0=Event(15) ; pk ahaadah 1=Event(16) ; " +
              "pk ahaadah 2=Event(17) ; pk ahaadah 3=Event(17) ; pk ahaadah done=Event(18):replaceDefault() ; " +
              "pk area11=ProgressEvent(2) ; pk qm3 2=ProgressCutscene(1) ; " +
              "pk qm3 3=ProgressCutscene(2) ; ph maudaal avail=ProgressEvent(889) ; " +
              "ph maudaal 7=ProgressEvent(890) ; ph footprint2 1=ProgressEvent(222) ; " +
              "ph footprint1 4=ProgressEvent(223) ; ph footprint1 6=ProgressEvent(225) ; " +
              "sb wazyih avail 0=ProgressEvent(684) ; sb wazyih avail 1=Event(685) ; " +
              "sb saliyahf avail=ProgressEvent(686) ; sb wazyih 0=ProgressEvent(688) ; " +
              "sb saliyahf 0=ReplaceEvent(687) ; sb wazyih 1=ReplaceEvent(689) ; " +
              "sb saliyahf 1=ReplaceEvent(687) ; sb wazyih 2=ProgressEvent(691) ; " +
              "sb saliyahf 2=ReplaceEvent(692) ; sb wazyih 3=ReplaceEvent(694) ; " +
              "sb saliyahf 3=ReplaceEvent(692) ; sb wazyih 5=ReplaceEvent(697) ; " +
              "sb saliyahf 5=ProgressEvent(696) ; sb area9=ProgressEvent(690)", string.Join(" ; ", lines));
        Pins2(ol, ss, pk, ph, sb);
    }

    // What no flow can tell apart: the reply kinds at Dkhaaya's 7 and 8, the Excavation Sites' 0 (a success and a
    // failure) and the Leypoint's trade 2 (each the only quest line there that can answer: the Leypoint's other trades
    // take other items), Dabhuh's trade 583; the draw's bound (a roll of exactly 50, which the seeded RNG does not
    // give); the zone-in replies' kinds (the engine reads a cutscene id, never a kind: equivalent in play, pinned as
    // the Lua's); and Olduum's nil cell (a GM's Prog outside 1..3 at 0's or 6's finish: the Lua's addKeyItem(nil) /
    // delKeyItem(nil) raise, which the recorder refuses, so no flow can take it). The Wait bounds, the ring, band and
    // cell tests of 7 and 8, the mining failure and Saliyahf's 692 at Prog 3 are flows (olduum#bounds, olduum#draws,
    // striking_a_balance). The container counts per Prog (pk qm3, pk ahaadah, sb count) are extra structural checks,
    // not a stand-in for any flow: every Prog's section is flowed, and no mutant is caught by them alone.
    static void Pins2(QuestScriptDefinition ol, QuestScriptDefinition ss, QuestScriptDefinition pk,
        QuestScriptDefinition ph, QuestScriptDefinition sb)
    {
        var lines = new System.Collections.Generic.List<string>();
        void L(string key, string v) => lines.Add(key + "=" + v);
        ushort Aw = QC.Zone.AhtUrhganWhitegate, Ay = QC.Zone.AydeewaSubterrane, Wj = QC.Zone.WajaomWoodlands;
        const QuestHandlerKind Tr = QuestHandlerKind.OnTrigger, Td = QuestHandlerKind.OnTrade;
        const QuestHandlerKind Zi = QuestHandlerKind.OnZoneIn;
        Fake Cm(int reissue, int wait, bool cell = false, int step = 2)
        {
            var f = new Fake { Status = QuestStatus.Completed, Step = step, Now = 100 };
            f.Vars["Reissue"] = reissue; f.Vars["Wait"] = wait;
            if (cell) f.KeyItems[(int)QC.KeyItem.Electropot] = true;
            return f;
        }
        Fake Pick(Fake f, params int[] rolls)
        {
            f.TradeResult = true;
            foreach (var r in rolls) f.Rolls.Enqueue(r);
            return f;
        }
        Fake Acc(int step = 0) => new Fake { Status = QuestStatus.Accepted, Step = step, Now = 100 };
        string ZI(QuestScriptDefinition def, ushort zone, Fake f) =>
            AU1Describe(AU3Up(def, f).Single(x => x.Kind == Zi && x.Zone == zone), f);
        string Dk(Fake f) => AU3Npc(ol, Tr, Aw, QC.Npc.AhtUrhganWhitegate.Dkhaaya, f);
        L("ol d8", Dk(Cm(1, 0, cell: true)));
        L("ol d7b", Dk(Cm(1, 0)));
        L("ol d7wait", Dk(Cm(0, 200)));
        string Ex(Fake f) => AU3Npc(ol, Td, Ay, QC.Npc.AydeewaSubterrane.ExcavationSite, f);
        L("ol mine50", Ex(Pick(Cm(1, 100), 50, 2)));
        L("ol mine51", Ex(Pick(Cm(1, 100), 51)));
        L("ol leypoint", AU3Npc(ol, Td, Wj, QC.Npc.WajaomWoodlands.Leypoint, Pick(Cm(0, 0))));
        // a finish run on the fake: its calls, then "raises" when the handler throws (the Lua's argument error)
        string Fin(ushort zone, int csid, Fake f)
        {
            var h = AU3Up(ol, f)
                .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Zone == zone && x.Id == csid);
            f.Csid = csid;
            var calls = new System.Collections.Generic.List<string>();
            try { h.Run(f); }
            catch (InvalidOperationException) { calls.Add("raises"); }
            return string.Join(";", f.Calls.Concat(calls));
        }
        L("ol mfin prog0", Fin(Ay, 0, Cm(1, 0, step: 0)));
        L("ol fin6 prog0", Fin(Aw, 6, Acc(0)));
        var tr = new Fake { Status = QuestStatus.Accepted };
        tr.TradeItems[QC.Item.MerrowScale] = 1;
        L("ss trade", AU3Npc(ss, Td, Aw, QC.Npc.AhtUrhganWhitegate.Dabhuh, tr));
        var z2 = new Fake { Status = QuestStatus.Available }; z2.Vars["Option"] = 2;
        L("ss zonein956", ZI(ss, Aw, z2));
        var z1 = new Fake { Status = QuestStatus.Available }; z1.Vars["Option"] = 1;
        L("ss zonein29", ZI(ss, QC.Zone.CaedarvaMire, z1));
        string Qm(int s) => string.Join("/", AU3Up(pk, Acc(s))
            .Where(x => x.Kind == Tr && x.Entity == QC.Npc.BhaflauThickets.qm3).Select(x => "x"));
        L("pk qm3 count", Qm(0) + "," + Qm(1) + "," + Qm(2) + "," + Qm(3) + "," + Qm(4));
        string Ah(int s) => string.Join("/", AU3Up(pk, Acc(s))
            .Where(x => x.Kind == Tr && x.Entity == QC.Npc.AhtUrhganWhitegate.Ahaadah).Select(x => "x"));
        L("pk ahaadah count", Ah(0) + "," + Ah(1) + "," + Ah(2) + "," + Ah(3) + "," + Ah(4));
        var box = Acc(0); box.XPos = 680; box.YPos = -19; box.ZPos = 218;
        L("ph wajaom0", ZI(ph, Wj, box));
        L("ph wajaom2", ZI(ph, Wj, Acc(2)));
        L("ph mamook3", ZI(ph, QC.Zone.Mamook, Acc(3)));
        foreach (var s in new[] { 2, 3 })
        {
            var f = Acc(s); f.QuestLocalVars["Option"] = 1;
            L("sb saliyahf opt1 " + s, AU3Npc(sb, Tr, Aw, QC.Npc.AhtUrhganWhitegate.Saliyahf, f));
        }
        L("sb zonein695", ZI(sb, Aw, Acc(4)));
        L("sb count", string.Join(",", Enumerable.Range(0, 7).Select(s => AU3Up(sb, Acc(s)).Count())));
        Check("ahturhgan g10:the branches no flow can tell apart",
            "ol d8=ProgressEvent(8,{[0]=769}) ; ol d7b=ProgressEvent(7,{[7]=2}) ; " +
            "ol d7wait=ProgressEvent(7) ; ol mine50=ProgressEvent(0,{[0]=769}) ; " +
            "ol mine51=ProgressEvent(0,{[1]=1}) ; ol leypoint=ProgressEvent(2) ; " +
            "ol mfin prog0=raises ; ol fin6 prog0=Quest.Complete();raises ; " +
            "ss trade=ProgressEvent(583,{text_table=0}) ; " +
            "ss zonein956=Event(956,{text_table=0}) ; ss zonein29=Event(29) ; " +
            "pk qm3 count=,,x,x, ; pk ahaadah count=x,x,x,x, ; ph wajaom0=Event(513) ; " +
            "ph wajaom2=Event(20) ; ph mamook3=Event(227) ; " +
            "sb saliyahf opt1 2=ReplaceEvent(693) ; sb saliyahf opt1 3=ReplaceEvent(693) ; " +
            "sb zonein695=Event(695) ; sb count=3,4,4,4,2,3,0",
            string.Join(" ; ", lines));
    }
}
