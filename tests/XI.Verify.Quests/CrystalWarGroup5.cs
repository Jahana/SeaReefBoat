// XI.Verify.Quests -- quest rework B3b Crystal War group 5 (Bastok 9-12: Beneath the Mask, What Price Loyalty, The
// Truth Lies Hid, Bonds of Mythril): what no trace can see.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunCrystalWarGroup5()
    {
        Console.WriteLine("== quest rework B3b Crystal War group 5: the validator, the unreachable arms and the " +
                          "reply kinds ==");
        var mask = new QC.CrystalWar.BeneathTheMask().Compile();
        var loyalty = new QC.CrystalWar.WhatPriceLoyalty().Compile();
        var truth = new QC.CrystalWar.TheTruthLiesHid().Compile();
        var bonds = new QC.CrystalWar.BondsOfMythril().Compile();
        Check("crystalwar g5:Beneath the Mask clean", "", string.Join(" | ", QuestScriptValidator.Validate(mask)));
        Check("crystalwar g5:What Price Loyalty clean", "", string.Join(" | ", QuestScriptValidator.Validate(loyalty)));
        Check("crystalwar g5:The Truth Lies Hid clean", "", string.Join(" | ", QuestScriptValidator.Validate(truth)));
        Check("crystalwar g5:Bonds of Mythril clean", "", string.Join(" | ", QuestScriptValidator.Validate(bonds)));

        string Finish(QuestScriptDefinition d, int csid, Action<Fake> set, ushort zone = 0)
        {
            var h = d.States.SelectMany(s => s.Handlers)
                .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Id == csid && (zone == 0 || x.Zone == zone));
            var f = new Fake { Status = QuestStatus.Accepted, Csid = csid };
            set(f);
            h.Run(f);
            return $"{f.Step}:{string.Join(";", f.Calls)}";
        }

        // Beneath the Mask's finish 2 keeps the Lua's `if npcUtil.giveKeyItem(...)`, which answers true for any key
        // item, held or not: the fake's give answering false (no Prog 4) pins the test.
        Check("crystalwar g5:Beneath the Mask's finish 2 sets Prog 4 only when the seal's give answers true",
              "4:Trade.Confirm();Player.GiveKeyItem(" + QC.KeyItem.WaxSeal + ");Quest.SetStep(4) | " +
              "3:Trade.Confirm();Player.GiveKeyItem(" + QC.KeyItem.WaxSeal + ")",
              $"{Finish(mask, 2, f => f.Step = 3)} | {Finish(mask, 2, f => { f.Step = 3; f.GiveResult = false; })}");

        // The Truth Lies Hid's trade finishes (12 / 13 / 14) test `TradeImp == <its imp> and TradeSuccess == 1`. Each
        // csid is only that imp's trade, whose handler wrote TradeImp first, so no trace sees the TradeImp operand
        // false: pinned.
        string Imp(int csid, int tradeImp, int success) => Finish(truth, csid, f =>
        {
            f.Step = 1;
            f.Vars["TradeImp"] = tradeImp;
            f.Vars["TradeSuccess"] = success;
        }, QC.Zone.CastleZvahlKeepS);
        Check("crystalwar g5:The Truth Lies Hid's trade finishes test TradeImp and TradeSuccess",
              "2:Quest.SetStep(2) 1: 1: | 2:Quest.SetStep(2) 1: 1: | 2:Quest.SetStep(2) 1: 1:",
              $"{Imp(12, 1, 1)} {Imp(12, 3, 1)} {Imp(12, 1, 0)} | {Imp(13, 2, 1)} {Imp(13, 1, 1)} {Imp(13, 2, 0)} | " +
              $"{Imp(14, 3, 1)} {Imp(14, 2, 1)} {Imp(14, 3, 0)}");

        // Bonds of Mythril's deaths test `Prog == 1` inside the Prog 1 section (the section's own check): their false
        // arm is unreachable. The handlers run at Prog 1 (the imp's count capped at 4) and at Prog 2 / 3: nothing.
        string Death(string mob, int step, int kills)
        {
            var h = bonds.States.SelectMany(s => s.Handlers)
                .Single(x => x.Kind == QuestHandlerKind.OnMobDeath && x.Entity == mob);
            var f = new Fake { Status = QuestStatus.Accepted, Step = step };
            f.Vars["ImpKills"] = kills;
            h.Run(f);
            return $"{f.Step}/{f.Var("ImpKills")}:{string.Join(";", f.Calls)}";
        }
        string imp = QC.Npc.CastleZvahlKeepS.KeepImp, warden = QC.Npc.CastleZvahlKeepS.GargouilleWarden;
        Check("crystalwar g5:Bonds of Mythril's deaths count only at Prog 1",
              "1/3:Quest.SetVar(ImpKills,3,0) 1/4:Quest.SetVar(ImpKills,4,0) 2/1: | 2/0:Quest.SetStep(2) 3/0:",
              $"{Death(imp, 1, 2)} {Death(imp, 1, 4)} {Death(imp, 2, 1)} | " +
              $"{Death(warden, 1, 0)} {Death(warden, 3, 0)}");

        // The waived finishes (no instance / battlefield opens them), run on the fake: What Price Loyalty's Everbloom
        // Hollow 10000 (Prog 5, the char set down by the portal) and Bonds of Mythril's Throne Room [S] 32001 (Prog 5
        // only with battlefieldWin 354 at Prog 4).
        Check("crystalwar g5:What Price Loyalty's Everbloom Hollow 10000 sets Prog 5 and the char by the portal",
              "5:Quest.SetStep(5);Player.SetPos(320,-10.835,158.699,0," + QC.Zone.XarcabardS + ")",
              Finish(loyalty, 10000, f => f.Step = 4));
        string Win(int bf, int step) => Finish(bonds, 32001, f =>
        {
            f.Step = step;
            f.KeyItems[QC.KeyItem.ZvahlPasskey] = true;
            f.PlayerLocalVars["battlefieldWin"] = bf;
        });
        Check("crystalwar g5:Bonds of Mythril's 32001 sets Prog 5 with battlefieldWin 354 at Prog 4 only",
              "5:Quest.SetStep(5) 4: 3:", $"{Win(354, 4)} {Win(353, 4)} {Win(354, 3)}");

        RunCrystalWarGroup5Kinds(mask, loyalty, truth, bonds);
    }

    // The reply kinds no trace can tell apart. None of these NPCs has a script (no onTrigger or onTrade to alternate
    // with); their defaults (Gentle Tiger 109, Leadavox 100, Roderich 111, the imps 28 / 29 / 30, the Keep's qm and the
    // Forbidding Portal a message) rank below Event; and no other quest answers them at an Event rank at the same time:
    // the Bastok line is a chain (each quest wants the one before completed), Better Part of Valor's and Operation
    // Cupid's Leadavox lines take other states or other items exactly, The Fighting Fourth's Roderich lines are under
    // or over What Price Loyalty's alike (its #rivals flow), and The Truth Lies Hid's and Bonds of Mythril's qm lines
    // exclude each other. Since group 10 Roderich has a rival at the same rank: Azure Footfalls' 15 (Progress, its Prog
    // 3) ties 10 and ranks over 11, and crystalWar.her_memories_azure_footfalls#rivals plays both (the pins stay). On
    // Sabbatical is available only on SCH, but once accepted it answers Gentle Tiger (46 / 47, Progress) on any job:
    // the four flows crystalWar.on_sabbatical#rivals, #rivals2, #rivals3 and #rivals4 play that rival against these
    // quests' Gentle Tiger states: the offers and Event states they reach show their kind (an Event loses to 47, a
    // Progress alternates with it), and the rest are pinned here. (What Price Loyalty's completed 356 and The Truth
    // Lies Hid's 212 tie in that quest's main flow, 211 and Bonds' 213 rank over 356 there and in Bonds' #rivals, and
    // Bonds' 214 sits under 356 in #rivals: the traces show those. Naji (default 700) does have an Event-rank rival:
    // Bastok mission 3-1's 717 at mission status 1 or more, over Bonds' 971 (replaceDefault) in Bonds' #naji flow,
    // which tells 971's kind apart; the pin keeps it as well. The zone-ins' csids play as numbers either way.)
    static void RunCrystalWarGroup5Kinds(QuestScriptDefinition mask, QuestScriptDefinition loyalty,
                                         QuestScriptDefinition truth, QuestScriptDefinition bonds)
    {
        var trig = QuestHandlerKind.OnTrigger;
        var trade = QuestHandlerKind.OnTrade;
        var zin = QuestHandlerKind.OnZoneIn;
        string R(QuestScriptDefinition d, int state, QuestStatus status, QuestHandlerKind kind, ushort zone, string npc,
                 int step = 0, Action<Fake> set = null)
        {
            var h = d.States[state].Handlers.Single(x => x.Kind == kind && x.Zone == zone && x.Entity == npc);
            var f = new Fake { Status = status, Step = step, UniqueDay = 8000 };
            set?.Invoke(f);
            var r = h.Fixed ?? h.Reply(f);
            return r == null ? "null" : QuestContextFake.Describe(r);
        }
        var av = QuestStatus.Available;
        var ac = QuestStatus.Accepted;
        var co = QuestStatus.Completed;
        ushort bms = QC.Zone.BastokMarketsS, eld = QC.Zone.TheEldiemeNecropolisS, keep = QC.Zone.CastleZvahlKeepS;
        string gt = QC.Npc.BastokMarketsS.GentleTiger, axe = QC.Npc.TheEldiemeNecropolisS.RedAxe;
        string rikke = QC.Npc.CastleZvahlKeepS.Rikke, rakke = QC.Npc.CastleZvahlKeepS.Rakke;
        string rokke = QC.Npc.CastleZvahlKeepS.Rokke, rod = QC.Npc.NorthGustabergS.Roderich;
        ushort ngs = QC.Zone.NorthGustabergS, xar = QC.Zone.XarcabardS;
        string qm = QC.Npc.CastleZvahlKeepS.qm;
        Action<Fake> elixir = f => f.TradeItems[QC.Item.Elixir] = 1;
        Action<Fake> traded(int imp, int success) => f =>
        {
            f.Vars["TradeDay"] = 8000;
            f.Vars["TradeImp"] = imp;
            f.Vars["TradeSuccess"] = success;
        };
        Action<Fake> talked(string dayVar) => f => f.Vars[dayVar] = 8000;
        var got = new[]
        {
            // Beneath the Mask: available, then Prog 0-6 (states 1-7)
            R(mask, 0, av, trig, bms, gt),
            R(mask, 1, ac, trig, bms, gt, 0), R(mask, 1, ac, zin, QC.Zone.BeadeauxS, null, 0),
            R(mask, 2, ac, trig, bms, gt, 1),
            R(mask, 3, ac, trig, bms, gt, 2), R(mask, 3, ac, trig, eld, axe, 2),
            R(mask, 4, ac, trig, bms, gt, 3), R(mask, 4, ac, trig, eld, axe, 3),
            R(mask, 4, ac, trade, QC.Zone.VunkerlInletS, QC.Npc.VunkerlInletS.Leadavox, 3, f =>
            {
                f.TradeItems[QC.Item.LumpOfBeeswax] = 1;
                f.TradeItems[QC.Item.JarOfRedTextileDye] = 1;
            }),
            R(mask, 5, ac, trig, bms, gt, 4), R(mask, 5, ac, trig, eld, axe, 4),
            R(mask, 6, ac, trig, bms, gt, 5), R(mask, 6, ac, trig, eld, axe, 5),
            R(mask, 7, ac, trig, bms, gt, 6),
            R(mask, 7, ac, trig, QC.Zone.BeaucedineGlacierS, QC.Npc.BeaucedineGlacierS.Hoarfang, 6),
            // What Price Loyalty: available, then Prog 0-6 (states 1-7), completed (state 8)
            R(loyalty, 0, av, trig, bms, gt),
            R(loyalty, 1, ac, trig, bms, gt, 0), R(loyalty, 1, ac, trig, ngs, rod, 0),
            R(loyalty, 2, ac, trig, ngs, rod, 1), R(loyalty, 2, ac, trig, bms, gt, 1),
            R(loyalty, 3, ac, trig, bms, gt, 2), R(loyalty, 3, ac, zin, QC.Zone.XarcabardS, null, 2),
            R(loyalty, 4, ac, trig, bms, gt, 3), R(loyalty, 4, ac, trig, xar, QC.Npc.XarcabardS.ForbiddingPortal, 3),
            R(loyalty, 5, ac, trig, bms, gt, 4, f => f.KeyItems[QC.KeyItem.CommandersEndorsement] = true),
            R(loyalty, 5, ac, trig, bms, gt, 4),
            R(loyalty, 6, ac, zin, QC.Zone.XarcabardS, null, 5, f => f.PrevZone = QC.Zone.EverbloomHollow),
            R(loyalty, 7, ac, trig, bms, gt, 6),
            R(loyalty, 8, co, trig, bms, gt),
            // The Truth Lies Hid: available, Prog 0 (state 1), the imps at Prog 1 (state 2, day 8000: scenario C,
            // honest Rokke) and Prog 2 (state 3), the Displaced Block at Prog 3 (state 4)
            R(truth, 0, av, trig, bms, gt),
            R(truth, 1, ac, trig, bms, gt, 0), R(truth, 1, ac, zin, QC.Zone.CastleZvahlBaileysS, null, 0),
            R(truth, 2, ac, trig, keep, rikke, 1), R(truth, 2, ac, trig, keep, rikke, 1, talked("Imp1Day")),
            R(truth, 2, ac, trig, keep, rikke, 1, traded(1, 0)), R(truth, 2, ac, trig, keep, rikke, 1, traded(2, 1)),
            R(truth, 2, ac, trade, keep, rikke, 1, elixir), R(truth, 2, ac, trade, keep, rokke, 1, elixir),
            R(truth, 2, ac, trig, keep, rakke, 1), R(truth, 2, ac, trig, keep, rakke, 1, talked("Imp2Day")),
            R(truth, 2, ac, trig, keep, rokke, 1, talked("Imp3Day")), R(truth, 2, ac, trade, keep, rakke, 1, elixir),
            R(truth, 3, ac, trig, keep, rokke, 2), R(truth, 3, ac, trig, keep, rikke, 2),
            R(truth, 3, ac, trig, keep, qm, 2),
            R(truth, 4, ac, trig, keep, QC.Npc.CastleZvahlKeepS.DisplacedBlock, 3),
            // Bonds of Mythril: available (state 0), Prog 0-2 (states 1-3), the passkey (state 4), Prog 5-7 (states
            // 5-7), completed (state 8)
            R(bonds, 0, av, zin, QC.Zone.CastleZvahlBaileysS, null, 0, f => f.PrevZone = keep),
            R(bonds, 1, ac, trig, keep, qm, 0),
            R(bonds, 2, ac, trig, keep, qm, 1), R(bonds, 2, ac, trig, keep, qm, 1, f => f.Vars["ImpKills"] = 4),
            R(bonds, 3, ac, trig, keep, qm, 2),
            R(bonds, 4, ac, trig, QC.Zone.ThroneRoomS, QC.Npc.ThroneRoomS._4c1, 3),
            R(bonds, 5, ac, trig, bms, gt, 5),
            R(bonds, 6, ac, zin, QC.Zone.Metalworks, null, 6), R(bonds, 7, ac, zin, QC.Zone.NorthGustabergS, null, 7),
            R(bonds, 8, co, trig, bms, gt), R(bonds, 8, co, trig, QC.Zone.Metalworks, QC.Npc.Metalworks.Naji),
        };
        Check("crystalwar g5 kinds:the replies at NPCs with no script and no rival at the same rank (Gentle Tiger, " +
              "Red Axe, Leadavox, Hoarfang, Roderich (before group 10), the Forbidding Portal, Rikke, Rakke, Rokke, " +
              "the Keep's qm, the Displaced Block, _4c1, Naji) and the zone-ins",
              "ProgressEvent(345) Event(351) Event(1) ProgressEvent(346) Event(352) ProgressEvent(42) Event(352) " +
              "Event(44) ProgressEvent(2) Event(352) ProgressEvent(43) ProgressEvent(347) Event(45) Event(353) " +
              "ProgressEvent(7) | " +
              "ProgressEvent(348) Event(354) ProgressEvent(10) Event(11) ProgressEvent(349) Event(355) Event(8) " +
              "Event(355) ProgressEvent(9) Event(355) ProgressEvent(357) Event(10) ProgressEvent(350) Event(356) | " +
              "ProgressEvent(211) Event(212) Event(12) Event(9,155,0,0) Event(19,155,0,0) Event(22,155,0,0) " +
              "Event(25,1) Event(12,155,0,0) ProgressEvent(14,155,0,1) Event(10,155,0,2) Event(20,155,0,2) " +
              "Event(21,155,0,0) Event(13,155,0,0) Event(24,155,0,1) Event(25,1) ProgressEvent(15) " +
              "ProgressEvent(16) | " +
              "Event(13) ProgressEvent(17) ProgressEvent(17) MessageSpecial(155:A_SHIVER_RUNS_DOWN) " +
              "ProgressEvent(18) ProgressEvent(12) ProgressEvent(213,87,0,2964,0) Event(972) Event(13) " +
              "Event(214):replaceDefault() Event(971):replaceDefault()",
              $"{string.Join(" ", got.Take(15))} | {string.Join(" ", got.Skip(15).Take(14))} | " +
              $"{string.Join(" ", got.Skip(29).Take(17))} | {string.Join(" ", got.Skip(46))}");
    }
}
