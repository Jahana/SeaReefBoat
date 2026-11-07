// XI.Verify.Quests -- quest rework B3b Crystal War group 11 (Message on the Winds, Seeing Spots, The Fumbling Friar,
// The Flipside of Things, Hammering Hearts): what no trace can see (validator, declared steps, the reply kinds at NPCs
// with no script and no rival at the same rank, the completions' false arm). The replies, the Romualdo tie and the full
// bags are flows' (crystalWar.message_on_the_winds#rivals and the group's other flows).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunCrystalWarGroup11()
    {
        Console.WriteLine(
            "== quest rework B3b Crystal War group 11: the validator, the declared steps and the kinds ==");
        var mw = new QC.CrystalWar.MessageOnTheWinds().Compile();
        var ss = new QC.CrystalWar.SeeingSpots().Compile();
        var ff = new QC.CrystalWar.TheFumblingFriar().Compile();
        var fs = new QC.CrystalWar.TheFlipsideOfThings().Compile();
        var hh = new QC.CrystalWar.HammeringHearts().Compile();
        Check("crystalwar g11:Message on the Winds clean", "", string.Join(" | ", QuestScriptValidator.Validate(mw)));
        Check("crystalwar g11:Seeing Spots clean", "", string.Join(" | ", QuestScriptValidator.Validate(ss)));
        Check("crystalwar g11:The Fumbling Friar clean", "", string.Join(" | ", QuestScriptValidator.Validate(ff)));
        Check("crystalwar g11:The Flipside of Things clean", "", string.Join(" | ", QuestScriptValidator.Validate(fs)));
        Check("crystalwar g11:Hammering Hearts clean", "", string.Join(" | ", QuestScriptValidator.Validate(hh)));
        Check("crystalwar g11:the declared steps", "1,2,3,4,5 |  |  |  | 1",
              string.Join(" | ", new[] { mw, ss, ff, fs, hh }.Select(d => string.Join(",", d.DeclaredSteps))));

        // a state's reply: the handler of that kind at that NPC, on a fake at the given step
        string R(QuestScriptDefinition d, int state, QuestHandlerKind kind, ushort zone, string npc, int step = 0,
                 Action<Fake> set = null)
        {
            var h = d.States[state].Handlers.Single(x => x.Kind == kind && x.Zone == zone && x.Entity == npc);
            var fk = new Fake { Status = QuestStatus.Accepted, Step = step };
            set?.Invoke(fk);
            var r = h.Fixed ?? h.Reply(fk);
            return r == null ? "null" : QuestContextFake.Describe(r);
        }
        string Fin(QuestScriptDefinition d, ushort zone, int csid, Action<Fake> set = null)
        {
            var h = d.States.SelectMany(s => s.Handlers)
                .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Zone == zone && x.Id == csid);
            var fk = new Fake { Status = QuestStatus.Accepted, Csid = csid, Zone = zone, CompleteResult = false };
            set?.Invoke(fk);
            h.Run(fk);
            return string.Join(";", fk.Calls);
        }
        var trig = QuestHandlerKind.OnTrigger;
        var trade = QuestHandlerKind.OnTrade;

        // Batallia Downs [S]'s Romualdo, Childerich and qm3 have no script and no other quest line, so a reply's kind
        // never shows in a trace (the Metalworks' 952 / 953 tie with Stamp Hunt's 726 in #rivals)
        ushort bd = QC.Zone.BatalliaDownsS, gs = QC.Zone.GraubergS;
        string rom = QC.Npc.BatalliaDownsS.Romualdo, chi = QC.Npc.GraubergS.Childerich;
        Check("crystalwar g11 kinds:Message on the Winds (109, 110, 1, 2, 3, 4, 5, 111, 6, 112, 7)",
              "ProgressEvent(109) | ProgressEvent(110) | ProgressEvent(1) | ProgressEvent(2) | ProgressEvent(3) | " +
              "ProgressEvent(4) | ProgressEvent(5) | ProgressEvent(111) | ProgressEvent(6) | " +
              "Event(112):replaceDefault() | Event(7):replaceDefault()",
              string.Join(" | ",
                  R(mw, 1, trig, bd, rom, 1), R(mw, 2, trig, bd, rom, 2), R(mw, 2, trig, gs, chi, 2),
                  R(mw, 3, trig, gs, chi, 3), R(mw, 3, trig, gs, QC.Npc.GraubergS.qm3, 3), R(mw, 4, trig, gs, chi, 4),
                  R(mw, 5, trig, gs, chi, 5), R(mw, 5, trig, bd, rom, 5), R(mw, 7, trig, gs, chi),
                  R(mw, 8, trig, bd, rom), R(mw, 9, trig, gs, chi)));

        // Wyatt has no script; Wrath of the Griffon's 66 (setPriority 1005) ranks over these whatever their kind
        ushort sd = QC.Zone.SouthernSanDoriaS;
        string wy = QC.Npc.SouthernSanDoriaS.Wyatt;
        Check("crystalwar g11 kinds:Seeing Spots (2, 3, 4)",
              "ProgressEvent(2) | ProgressEvent(3) | ProgressEvent(4)",
              string.Join(" | ", R(ss, 0, trig, sd, wy), R(ss, 1, trig, sd, wy),
                  R(ss, 1, trade, sd, wy, 0, f => f.TradeItems[QC.Item.LadybugWing] = 4)));

        // Fondactiont, the qm2s, Rarcasmeault, _qm2 and Scarred Shark: no script, no other quest line
        ushort gc = QC.Zone.GarlaigeCitadelS, bm = QC.Zone.BastokMarketsS;
        string fo = QC.Npc.GarlaigeCitadelS.Fondactiont, ra = QC.Npc.GarlaigeCitadelS.Rarcasmeault;
        string sh = QC.Npc.BastokMarketsS.ScarredShark;
        Action<Fake> pkg = f => f.KeyItems[QC.KeyItem.OrnatePackage] = true;
        Action<Fake> fcase = f => f.KeyItems[QC.KeyItem.FirepowerCase] = true;
        Check("crystalwar g11 kinds:The Fumbling Friar (26, 28, 27, qm2 gives the package: user ruling 2026-11-04, 29)",
              "ProgressEvent(26) | ProgressEvent(28) | Event(27) | KeyItem(969) | Event(29):replaceDefault()",
              string.Join(" | ", R(ff, 0, trig, gc, fo), R(ff, 1, trig, gc, fo, 0, pkg), R(ff, 1, trig, gc, fo),
                  R(ff, 1, trig, gs, QC.Npc.GraubergS.qm2), R(ff, 2, trig, gc, fo)));
        Check("crystalwar g11 kinds:The Flipside of Things (6, 9, 8, 7, 4)",
              "ProgressEvent(6) | ProgressEvent(9) | Event(8) | ProgressEvent(7) | Event(4):replaceDefault()",
              string.Join(" | ", R(fs, 0, trig, gc, ra), R(fs, 1, trig, gc, ra, 0, fcase), R(fs, 1, trig, gc, ra),
                  R(fs, 1, trig, gc, QC.Npc.GarlaigeCitadelS.qm2), R(fs, 2, trig, gc, ra)));
        Check("crystalwar g11 kinds:Hammering Hearts (40, 43, 41, 44, 42, 45)",
              "ProgressEvent(40) | ProgressEvent(43) | ProgressEvent(41) | ProgressEvent(44) | ProgressEvent(42) | " +
              "Event(45):replaceDefault()",
              string.Join(" | ", R(hh, 0, trig, bm, sh), R(hh, 1, trig, bm, sh),
                  R(hh, 1, trade, bm, sh, 0, f =>
                  {
                      f.TradeItems[QC.Item.HeavyQuadavChestplate] = 1;
                      f.TradeItems[QC.Item.HeavyQuadavBackplate] = 1;
                  }),
                  R(hh, 2, trig, bm, sh, 1, f => f.PlayerLocalVars["Quest[7][14]NeedToZone"] = 1),
                  R(hh, 2, trig, bm, sh, 1), R(hh, 3, trig, bm, sh)));

        // Complete() false: 111 writes no PostCS, 4 confirms nothing, 28 keeps the package, 9 takes the case anyway
        // (the rewards of 111 / 28 / 42 hold an item: the full-bag flows take those arms; 4 and 9 cannot fail live)
        Check("crystalwar g11 pins:the finishes when Complete() fails (111, 4, 28, 9, 42)",
              "Quest.Complete() | Quest.Complete() | Quest.Complete() | Quest.Complete();Player.DelKeyItem(" +
              QC.KeyItem.FirepowerCase + ") | Quest.Complete()",
              string.Join(" | ", Fin(mw, bd, 111), Fin(ss, sd, 4), Fin(ff, gc, 28, pkg), Fin(fs, gc, 9, fcase),
                  Fin(hh, bm, 42)));
    }
}
