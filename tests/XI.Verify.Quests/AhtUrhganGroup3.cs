// XI.Verify.Quests -- quest rework B3b Aht Urhgan group 3 (the Salaheem's Sentinels promotions I: Private First Class,
// Superior Private, Lance Corporal, Corporal, Sergeant): each definition validates clean; what no trace can see.
// - The reply kinds no flow can show (the mutants' structural survivors; every other kind is pinned by a trace, the
//   #kinds flows among them). A kind only ranks the lookup's actions for the same NPC talk, trade or trigger area;
//   these have no script of their own (no fallback: the lookup runs the action whatever its rank) and no other line of
//   that interaction ranking Event or above, the rest of their rivals being the zone's DefaultActions (below Event):
//   * Naja Salaheem's trade: Private First Class's 5002 (Imp Wing) is the only trade line at her;
//   * Nafiwaa (Lance Corporal's 5035, 5036, 5037, 5038, 5039: one quest's lines, one state up at a time; her default
//     661 a DefaultAction) and the two Mythralline Wellsprings (Wajaom's 4 to 7, Bhaflau's 10; their WELLSPRING
//     messages DefaultActions);
//   * Sergeant's Whitegate area 10 (5056; the zone's switch has no case 10), Nashmau's area 1 (303; the zone's handler
//     is empty, ToAU 15's area 1 is Arrapago's), Totoroon (252 and the trade 304; his default 246 a DefaultAction) and
//     Caedarva's qm11 (20): one quest line each.
// - Abquhbah's importantOnce lines (5032, 5033, 5034): his script's 255 alternates with an action at Event or below
//   either way (interaction_lookup.lua's fallback var), so Event then Default plays as a plain Event would: the traces
//   cannot tell the modifier is there; and importantOnce sets the rank to Event itself, so neither can they tell a
//   Progress under it.
// - Not pinned, equivalent in play: fillTestTube's `Stage == 1` (its callers, the Wellsprings' finishes, run only in
//   the Stage 1 or 2 section; #edges sets Stage 3 while 4 is open: the finish does not run) and getFluidLevel's `bits >
//   0` (at 0 both branches give 0).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunAhtUrhganGroup3()
    {
        Console.WriteLine("== quest rework B3b Aht Urhgan group 3: the promotions I; the reply kinds no trace sees ==");
        var pfc = new QC.AhtUrhgan.PromotionPrivateFirstClass().Compile();
        var sp = new QC.AhtUrhgan.PromotionSuperiorPrivate().Compile();
        var lc = new QC.AhtUrhgan.PromotionLanceCorporal().Compile();
        var corporal = new QC.AhtUrhgan.PromotionCorporal().Compile();
        var sergeant = new QC.AhtUrhgan.PromotionSergeant().Compile();
        foreach (var (name, def) in new[] { ("Private First Class", pfc), ("Superior Private", sp),
                     ("Lance Corporal", lc), ("Corporal", corporal), ("Sergeant", sergeant) })
            Check($"ahturhgan g3:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(def)));

        var wing = new Fake { Status = QuestStatus.Accepted, TradeResult = true };
        var nafiwaaNone = AU3Lc(1, 0);
        var nafiwaaOne = AU3Lc(1, 0);
        nafiwaaOne.KeyItems[QC.KeyItem.TestTube1] = true;
        var nafiwaaAll = AU3Lc(1, 0);
        foreach (var tube in new[] { QC.KeyItem.TestTube1, QC.KeyItem.TestTube2, QC.KeyItem.TestTube3,
                     QC.KeyItem.TestTube4, QC.KeyItem.TestTube5 })
            nafiwaaAll.KeyItems[tube] = true;
        var remixDry = AU3Lc(2, 1023);
        var wajaom = AU3Lc(1, 0);
        wajaom.Npc = new QuestNpcFake(17060000);
        wajaom.NpcIds["WELLSPRING"] = 17060000;   // the first Wellspring: tube 1
        wajaom.KeyItems[QC.KeyItem.EmptyTestTube1] = true;
        var bhaflau = AU3Lc(1, 0);
        bhaflau.KeyItems[QC.KeyItem.EmptyTestTube5] = true;
        var waiting = AU3Lc(3, 0);
        waiting.Vars["Option"] = 48;
        var nashmau = AU3Sg(1);
        nashmau.UniqueDay = 1;
        var sutlac = AU3Sg(2);
        sutlac.TradeItems[QC.Item.BowlOfSutlac] = 1;
        const QuestHandlerKind talk = QuestHandlerKind.OnTrigger, trade = QuestHandlerKind.OnTrade;
        ushort whitegate = QC.Zone.AhtUrhganWhitegate, nash = QC.Zone.Nashmau;
        string nafiwaa = QC.Npc.AhtUrhganWhitegate.Nafiwaa, abquhbah = QC.Npc.AhtUrhganWhitegate.Abquhbah;
        string totoroon = QC.Npc.Nashmau.Totoroon;
        Check("ahturhgan g3:the reply kinds no flow can show, as the Lua has them: Naja's trade 5002, Nafiwaa's 5035 " +
              "and 5038 Progress, 5036, 5037 and 5039 Events, the Wellsprings' 4 and 10, Sergeant's area 10 (5056), " +
              "Nashmau area 1 (303), Totoroon's trade 304 and qm11's 20 Progress, Totoroon's 252 an Event; " +
              "Abquhbah's 5032, 5033, 5034 importantOnce Events",
              "ProgressEvent(5002,{text_table=0})|ProgressEvent(5035,{text_table=0})|Event(5036)|Event(5037,{[1]=1})|" +
              "ProgressEvent(5038,{[2]=0,[5]=0,[7]=0,text_table=0})|Event(5036)|Event(5039)|ProgressEvent(4)|" +
              "ProgressEvent(10)|ProgressEvent(5056)|ProgressEvent(303)|ProgressEvent(304)|Event(252)|" +
              "ProgressEvent(20)|Event(5032):importantOnce()|Event(5033):importantOnce()|Event(5034):importantOnce()",
              string.Join("|",
                  AU3Npc(pfc, trade, whitegate, QC.Npc.AhtUrhganWhitegate.NajaSalaheem, wing),
                  AU3Npc(lc, talk, whitegate, nafiwaa, AU3Lc(0, 0)),
                  AU3Npc(lc, talk, whitegate, nafiwaa, nafiwaaNone),
                  AU3Npc(lc, talk, whitegate, nafiwaa, nafiwaaOne),
                  AU3Npc(lc, talk, whitegate, nafiwaa, nafiwaaAll),
                  AU3Npc(lc, talk, whitegate, nafiwaa, remixDry),
                  AU3Npc(lc, talk, whitegate, nafiwaa, waiting),
                  AU3Npc(lc, talk, QC.Zone.WajaomWoodlands, QC.Npc.WajaomWoodlands.MythrallineWellspring, wajaom),
                  AU3Npc(lc, talk, QC.Zone.BhaflauThickets, QC.Npc.BhaflauThickets.MythrallineWellspring, bhaflau),
                  AU3Area(sergeant, whitegate, 10, AU3Sg(0)),
                  AU3Area(sergeant, nash, 1, nashmau),
                  AU3Npc(sergeant, trade, nash, totoroon, sutlac),
                  AU3Npc(sergeant, talk, nash, totoroon, AU3Sg(2)),
                  AU3Npc(sergeant, talk, QC.Zone.CaedarvaMire, QC.Npc.CaedarvaMire.qm11, AU3Sg(3)),
                  AU3Npc(lc, talk, whitegate, abquhbah, AU3Lc(0, 0)),
                  AU3Npc(lc, talk, whitegate, abquhbah, AU3Lc(1, 0)),
                  AU3Npc(lc, talk, whitegate, abquhbah, waiting)));
    }

    // Lance Corporal accepted at that Stage, Prog (the tube contents) set
    static Fake AU3Lc(int stage, int prog)
    {
        var f = new Fake { Status = QuestStatus.Accepted };
        f.Vars["Stage"] = stage;
        f.Vars["Prog"] = prog;
        return f;
    }

    // Sergeant accepted at that Prog
    static Fake AU3Sg(int prog) => new Fake { Status = QuestStatus.Accepted, Step = prog };

    // the reply of the one handler of that kind for the NPC in the zone, among the states up on the fake
    static string AU3Npc(QuestScriptDefinition def, QuestHandlerKind kind, ushort zone, string npc, Fake f) =>
        AU1Describe(AU3Up(def, f).Single(x => x.Kind == kind && x.Zone == zone && x.Entity == npc), f);

    // the reply of the quest's onTriggerAreaEnter for that zone's area, among the states up on the fake
    static string AU3Area(QuestScriptDefinition def, ushort zone, int area, Fake f) =>
        AU1Describe(AU3Up(def, f)
            .Single(x => x.Kind == QuestHandlerKind.OnTriggerAreaEnter && x.Zone == zone && x.Id == area), f);

    static System.Collections.Generic.IEnumerable<QuestHandlerDecl> AU3Up(QuestScriptDefinition def, Fake f) =>
        def.States.Where(s => s.Matches.Any(m => (m.AnyStatus || m.Status == f.Status) &&
                                                 (m.Step == null || m.Step == f.Step) &&
                                                 (m.When == null || m.When(f))))
            .SelectMany(s => s.Handlers);
}
