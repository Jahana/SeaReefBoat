// XI.Verify.Quests -- quest rework B3b Crystal War group 2 (Windurst's line and Windurst Waters [S]: The Tigress Stirs,
// The Tigress Strikes, Knot Quite There, The Dawn of Delectability, The Lost Book): what no trace can see.
// - The Tigress Strikes' War_Lynx death: the id test. Every War_Lynx-scripted mob of Fort Karugo-Narugo [S] runs the
//   handler, but the others (17170870-17170875, the Campaign's War_Lynx_campaign) are not spawned outside a Campaign
//   battle, so no flow kills one: their side of the id test is shown here on the fake.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunCrystalWarGroup2()
    {
        Console.WriteLine("== quest rework B3b Crystal War group 2: the validator and The Tigress Strikes' lynx id test ==");
        var stirs = new QC.CrystalWar.TheTigressStirs().Compile();
        var strikes = new QC.CrystalWar.TheTigressStrikes().Compile();
        var knot = new QC.CrystalWar.KnotQuiteThere().Compile();
        var dawn = new QC.CrystalWar.TheDawnOfDelectability().Compile();
        var book = new QC.CrystalWar.TheLostBook().Compile();
        Check("crystalwar g2:The Tigress Stirs clean", "", string.Join(" | ", QuestScriptValidator.Validate(stirs)));
        Check("crystalwar g2:The Tigress Strikes clean", "", string.Join(" | ", QuestScriptValidator.Validate(strikes)));
        Check("crystalwar g2:Knot Quite There clean", "", string.Join(" | ", QuestScriptValidator.Validate(knot)));
        Check("crystalwar g2:The Dawn of Delectability clean", "", string.Join(" | ", QuestScriptValidator.Validate(dawn)));
        Check("crystalwar g2:The Lost Book clean", "", string.Join(" | ", QuestScriptValidator.Validate(book)));

        // TIGRESS_STRIKES_WAR_LYNX = GetFirstID('War_Lynx') = 17170645; the Campaign's War Lynxes run the same handler
        const int lynx = 17170645;
        var death = CW2Handler(strikes, 1, QuestHandlerKind.OnMobDeath, QC.Npc.FortKarugoNarugoS.WarLynx);
        string Dying(int mobId)
        {
            var f = new Fake { Status = QuestStatus.Accepted, Step = 2, Mob = new QuestNpcFake(mobId) };
            f.MobIds[QC.Mob.FortKarugoNarugoS.TIGRESS_STRIKES_WAR_LYNX] = lynx;
            death.Run(f);
            return string.Join(";", f.Calls);
        }
        Check("crystalwar g2:The Tigress Strikes' War_Lynx death sets LynxKilled 1 for TIGRESS_STRIKES_WAR_LYNX only: a Campaign War Lynx (17170870, 17170875) sets nothing",
              "Quest.SetVar(LynxKilled,1,0)||",
              $"{Dying(lynx)}|{Dying(17170870)}|{Dying(17170875)}");

        RunCrystalWarGroup2Kinds(stirs, strikes, knot, dawn, book);
    }

    // The reply kinds no trace can tell apart: each NPC below has no script handler for the interaction (no onTrigger, or
    // no onTrade), so the lookup never alternates; its default (if any) ranks below Event; and no other quest answers it
    // at the same time (Dhea Prandoleh's two lines exclude each other; Bulwark Gate's, Ranpi-Monpi's and Quu Bokye's
    // other trade lines take other items, exactly). Progress and Event then play alike, so the kinds are pinned here.
    static void RunCrystalWarGroup2Kinds(QuestScriptDefinition stirs, QuestScriptDefinition strikes, QuestScriptDefinition knot,
                                         QuestScriptDefinition dawn, QuestScriptDefinition book)
    {
        var trig = QuestHandlerKind.OnTrigger;
        var trade = QuestHandlerKind.OnTrade;
        string R(QuestScriptDefinition d, int state, QuestHandlerKind kind, ushort zone, string npc, int step = 0,
                 int lynxKilled = 0, params int[] traded)
        {
            var h = d.States[state].Handlers.Single(x => x.Kind == kind && x.Zone == zone && x.Entity == npc);
            var f = new Fake { Status = QuestStatus.Accepted, Step = step };
            if (lynxKilled != 0) f.Vars["LynxKilled"] = lynxKilled;
            foreach (int i in traded) f.TradeItems[i] = 1;
            var r = h.Fixed ?? h.Reply(f);
            return r == null ? "null" : QuestContextFake.Describe(r);
        }
        ushort wws = QC.Zone.WindurstWatersS, ww = QC.Zone.WindurstWaters, fk = QC.Zone.FortKarugoNarugoS;
        string dhea = QC.Npc.WindurstWatersS.DheaPrandoleh, rotih = QC.Npc.FortKarugoNarugoS.RotihMoalghett;
        string qm4 = QC.Npc.FortKarugoNarugoS.qm4, rws = QC.Npc.WindurstWatersS.RanpiMonpi, rww = QC.Npc.WindurstWaters.RanpiMonpi;
        string rhi = QC.Npc.WindurstWatersS.DoorRhinosterySouth, opt = QC.Npc.WindurstWatersS.DoorOptistery;
        var got = new[]
        {
            R(stirs, 0, trig, wws, dhea), R(stirs, 1, trig, wws, dhea),
            R(strikes, 0, trig, wws, dhea), R(strikes, 1, trig, fk, rotih, 0), R(strikes, 1, trig, fk, rotih, 1),
            R(strikes, 1, trig, fk, qm4, 1), R(strikes, 1, trig, fk, qm4, 2, 1),
            R(strikes, 1, trig, wws, dhea, 3), R(strikes, 1, trig, wws, dhea, 0),
            R(knot, 1, trade, QC.Zone.SauromugueChampaignS, QC.Npc.SauromugueChampaignS.BulwarkGate, 1, 0, QC.Item.OneHundredEightKnotQuipu),
            R(knot, 1, trig, QC.Zone.SouthernSanDoriaS, QC.Npc.SouthernSanDoriaS._6eo, 3),
            R(dawn, 0, trig, wws, rws), R(dawn, 1, trig, wws, rws),
            R(dawn, 2, trade, ww, rww, 2, 0, QC.Item.Pepperoni, QC.Item.Walnut, QC.Item.DragonFruit, QC.Item.BastoreSweeper),
            R(dawn, 5, trig, wws, rws, 4), R(dawn, 7, trig, wws, rws, 5),
            R(book, 0, trig, wws, rhi), R(book, 1, trig, wws, rhi, 1),
            R(book, 1, trade, QC.Zone.Giddeus, QC.Npc.Giddeus.QuuBokye, 1, 0, QC.Item.MythrilBeastcoin),
            R(book, 2, trig, wws, rhi, 2), R(book, 3, trig, wws, opt, 3), R(book, 3, trig, wws, rhi, 3),
            R(book, 5, trade, wws, opt, 4, 0, QC.Item.SheetOfVellum), R(book, 6, trig, wws, opt, 5),
        };
        Check("crystalwar g2 kinds:the replies at NPCs with no script handler and no rival at the same rank (Dhea Prandoleh, " +
              "Rotih Moalghett, qm4, Bulwark Gate's trade, _6eo, Ranpi-Monpi [S], Ranpi-Monpi's trade, the Rhinostery and " +
              "Optistery doors, Quu Bokye's trade)",
              "ProgressEvent(128) Event(131) | ProgressEvent(133) ProgressEvent(101) Event(104) ProgressEvent(102) ProgressEvent(103) " +
              "ProgressEvent(134) Event(135) | ProgressEvent(106) ProgressEvent(63) | ProgressEvent(117) ProgressEvent(119) " +
              "ProgressEvent(983) ProgressEvent(118) Event(120):replaceDefault() | ProgressEvent(143) ProgressEvent(148) " +
              "ProgressEvent(65) ProgressEvent(144) ProgressEvent(145) Event(150) ProgressEvent(146) ProgressEvent(147)",
              $"{got[0]} {got[1]} | {string.Join(" ", got.Skip(2).Take(7))} | {got[9]} {got[10]} | {string.Join(" ", got.Skip(11).Take(5))} | " +
              string.Join(" ", got.Skip(16)));
    }

    // the handler of that kind (and entity) in the 0-based state
    static QuestHandlerDecl CW2Handler(QuestScriptDefinition d, int state, QuestHandlerKind kind, string entity, int id = 0) =>
        d.States[state].Handlers.Single(h => h.Kind == kind && (entity == null || h.Entity == entity) && (id == 0 || h.Id == id));
}
