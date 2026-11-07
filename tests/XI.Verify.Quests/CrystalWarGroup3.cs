// XI.Verify.Quests -- quest rework B3b Crystal War group 3 (Bastok 1-4: Better Part of Valor, Fires of Discontent,
// Light in the Darkness, Burden of Suspicion): what no trace can see.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunCrystalWarGroup3()
    {
        Console.WriteLine("== quest rework B3b Crystal War group 3: the validator and the reply kinds ==");
        var valor = new QC.CrystalWar.BetterPartOfValor().Compile();
        var fires = new QC.CrystalWar.FiresOfDiscontent().Compile();
        var light = new QC.CrystalWar.LightInTheDarkness().Compile();
        var burden = new QC.CrystalWar.BurdenOfSuspicion().Compile();
        Check("crystalwar g3:Better Part of Valor clean", "", string.Join(" | ", QuestScriptValidator.Validate(valor)));
        Check("crystalwar g3:Fires of Discontent clean", "", string.Join(" | ", QuestScriptValidator.Validate(fires)));
        Check("crystalwar g3:Light in the Darkness clean", "", string.Join(" | ", QuestScriptValidator.Validate(light)));
        Check("crystalwar g3:Burden of Suspicion clean", "", string.Join(" | ", QuestScriptValidator.Validate(burden)));

        RunCrystalWarGroup3Kinds(valor, fires, light, burden);
    }

    // The reply kinds no trace can tell apart. None of these NPCs has a script (no onTrigger or onTrade to alternate
    // with); their defaults (Engelhart 104, Gentle Tiger 109, Pagdako 106, Solitary Ant 112) rank below Event; and no
    // other quest answers them at the same time: the four quests' lines at Gentle Tiger, Engelhart, Pagdako and
    // Blatherix exclude each other (each needs the one before completed, and only Fires of Discontent has a completed
    // section: Engelhart's 165, replaceDefault, under every Event), and the later Bastok quests, Operation Cupid's and
    // Beneath the Mask's trades take other states or other items exactly. On Sabbatical is available only on SCH, but
    // once accepted it answers Gentle Tiger (46 / 47, Progress) on any job: the four flows
    // crystalWar.on_sabbatical#rivals, #rivals2, #rivals3 and #rivals4 play that rival against these quests' Gentle
    // Tiger states: the offers and Event states they reach show their kind (an Event loses to 47, a Progress
    // alternates with it), and the rest are pinned here.
    // (Leadavox's talk and Iron Eater's are not here: Operation Cupid's 5 and the Windurst mission's 702 tie with
    // them in the #rivals flows. The zone-ins' csids play as numbers either way.)
    static void RunCrystalWarGroup3Kinds(QuestScriptDefinition valor, QuestScriptDefinition fires,
                                         QuestScriptDefinition light, QuestScriptDefinition burden)
    {
        var trig = QuestHandlerKind.OnTrigger;
        var trade = QuestHandlerKind.OnTrade;
        string R(QuestScriptDefinition d, int state, QuestHandlerKind kind, ushort zone, string npc, int step = 0,
                 int prevZone = 0, int gil = 0, params (int Item, int Qty)[] traded)
        {
            var h = d.States[state].Handlers.Single(x => x.Kind == kind && x.Zone == zone && x.Entity == npc);
            var f = new Fake { Status = state == 0 ? QuestStatus.Available : QuestStatus.Accepted, Step = step, PrevZone = prevZone };
            if (gil != 0) f.TradeItems[65535] = gil;
            foreach (var (item, qty) in traded) f.TradeItems[item] = qty;
            var r = h.Fixed ?? h.Reply(f);
            return r == null ? "null" : QuestContextFake.Describe(r);
        }
        ushort bms = QC.Zone.BastokMarketsS, ngs = QC.Zone.NorthGustabergS, pms = QC.Zone.PashhowMarshlandsS;
        string eng = QC.Npc.BastokMarketsS.Engelhart, gt = QC.Npc.BastokMarketsS.GentleTiger;
        string pag = QC.Npc.BastokMarketsS.Pagdako, blat = QC.Npc.BastokMarketsS.Blatherix;
        int choco = QC.Item.ChunkOfGoblinChocolate;
        var got = new[]
        {
            // Better Part of Valor
            R(valor, 0, QuestHandlerKind.OnZoneIn, ngs, null, 0, QC.Zone.BastokMarketsS),
            R(valor, 1, trig, bms, eng, 0), R(valor, 1, trig, bms, eng, 4), R(valor, 1, trig, bms, eng, 1),
            R(valor, 1, trig, ngs, QC.Npc.NorthGustabergS.qm1, 1), R(valor, 1, trig, ngs, QC.Npc.NorthGustabergS.SolitaryAnt),
            R(valor, 1, trade, QC.Zone.VunkerlInletS, QC.Npc.VunkerlInletS.Leadavox, 3, 0, 0, (QC.Item.GnoleClaw, 1)),
            // Fires of Discontent
            R(fires, 0, trig, bms, eng),
            R(fires, 1, trig, bms, eng, 0), R(fires, 1, trig, bms, eng, 2), R(fires, 1, trig, bms, eng, 3),
            R(fires, 1, trig, bms, eng, 4), R(fires, 1, trig, bms, eng, 5), R(fires, 1, trig, bms, eng, 6),
            R(fires, 1, trig, bms, pag, 0), R(fires, 1, trig, bms, pag, 1), R(fires, 1, trig, bms, gt, 5), R(fires, 1, trig, bms, gt, 0),
            R(fires, 1, trig, QC.Zone.GraubergS, QC.Npc.GraubergS.qm5, 3),
            // Light in the Darkness: available, then Prog 0, 1, 2, 3, 4, 5, 6, 7 (states 1-8)
            R(light, 0, trig, bms, gt),
            R(light, 1, trig, bms, gt, 0), R(light, 1, trig, bms, eng, 0), R(light, 1, trig, bms, pag, 0),
            R(light, 2, trig, bms, gt, 1), R(light, 2, trig, bms, eng, 1), R(light, 2, trig, bms, pag, 1), R(light, 2, trig, bms, blat, 1),
            R(light, 3, trig, bms, gt, 2), R(light, 3, trig, bms, eng, 2), R(light, 3, trig, bms, pag, 2), R(light, 3, trig, bms, blat, 2),
            R(light, 3, trade, bms, blat, 2, 0, 0, (choco, 30)), R(light, 3, trade, bms, blat, 2, 0, 5000),
            R(light, 4, QuestHandlerKind.OnZoneIn, pms, null, 3, QC.Zone.GraubergS),
            R(light, 4, trig, bms, gt, 3), R(light, 4, trig, bms, eng, 3), R(light, 4, trig, bms, pag, 3), R(light, 4, trig, bms, blat, 3),
            R(light, 5, trig, bms, gt, 4), R(light, 5, trig, bms, eng, 4), R(light, 5, trig, bms, pag, 4), R(light, 5, trig, bms, blat, 4),
            R(light, 6, QuestHandlerKind.OnZoneIn, pms, null, 5),
            R(light, 7, trig, bms, eng, 6), R(light, 7, trig, bms, pag, 6), R(light, 7, trig, bms, blat, 6), R(light, 7, trig, bms, gt, 6),
            R(light, 8, trig, bms, gt, 7), R(light, 8, trig, bms, eng, 7), R(light, 8, trig, bms, pag, 7), R(light, 8, trig, bms, blat, 7),
            R(light, 8, trade, bms, blat, 7, 0, 0, (choco, 10)), R(light, 8, trade, bms, blat, 7, 0, 1000),
            // Burden of Suspicion
            R(burden, 0, trig, bms, gt),
            R(burden, 1, trig, bms, gt, 1), R(burden, 1, trig, bms, gt, 2), R(burden, 1, trig, bms, gt, 0),
            R(burden, 1, trig, bms, eng, 0), R(burden, 1, trig, bms, eng, 2), R(burden, 1, trig, bms, eng, 1),
            R(burden, 1, trig, QC.Zone.TheEldiemeNecropolisS, QC.Npc.TheEldiemeNecropolisS.Sarcophagus, 1),
        };
        Check("crystalwar g3 kinds:the replies at NPCs with no script and no rival at the same rank (Engelhart, Gentle Tiger, " +
              "Pagdako, Blatherix, qm1, Solitary Ant, Leadavox's trade, qm5, Sarcophagus) and the zone-ins",
              "Event(1) ProgressEvent(116) ProgressEvent(118) Event(117) ProgressEvent(3) Event(2):replaceDefault() ProgressEvent(103) | " +
              "ProgressEvent(120) Event(121) ProgressEvent(124) Event(125) ProgressEvent(126) Event(127) ProgressEvent(164) " +
              "ProgressEvent(122) Event(123) ProgressEvent(160) Event(161) ProgressEvent(11) | " +
              "ProgressEvent(16) Event(17) Event(18) ProgressEvent(19) Event(17) Event(18) Event(20) ProgressEvent(21) " +
              "Event(17) Event(18) Event(20) Event(22) ProgressEvent(23) ProgressEvent(23) Event(901) Event(17) Event(18) Event(20) " +
              "Event(24) Event(17) Event(18) Event(20) Event(24) Event(902) Event(18) Event(20) Event(24) ProgressEvent(27) " +
              "Event(17) Event(18) Event(20) Event(25) ProgressEvent(23) ProgressEvent(23) | " +
              "ProgressEvent(30) Event(28) ProgressEvent(34) Event(31) ProgressEvent(32) Event(36) Event(33) ProgressEvent(17)",
              $"{string.Join(" ", got.Take(7))} | {string.Join(" ", got.Skip(7).Take(12))} | {string.Join(" ", got.Skip(19).Take(34))} | " +
              string.Join(" ", got.Skip(53)));
    }
}
