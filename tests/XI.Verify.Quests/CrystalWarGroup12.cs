// XI.Verify.Quests -- quest rework B3b Crystal War group 12 (Redeeming Rocks, Evil at the Inlet, Requiem for the
// Departed, Say It with a Handbag, Beans Ahoy; all call-only): the validator, the declared steps, the reply kinds, the
// completions' false arms, the Mog-Safe held check (the one operand no flow can reach: no statement puts an item in
// location 1) and, as extra checks, the guards' operands. The replies and the whole walks, the guards included, are
// flows' (crystalWar.<quest>, <quest>#edges and the #guards / #level30 flows); the call-only kinds are one reply kind
// (RenderCallReply), so a kind pin here is a pin on the author's choice, not on behaviour.
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunCrystalWarGroup12()
    {
        Console.WriteLine(
            "== quest rework B3b Crystal War group 12: the validator, the declared steps, the kinds and the pins ==");
        var rr = new QC.CrystalWar.RedeemingRocks().Compile();
        var ei = new QC.CrystalWar.EvilAtTheInlet().Compile();
        var rd = new QC.CrystalWar.RequiemForTheDeparted().Compile();
        var hb = new QC.CrystalWar.SayItWithAHandbag().Compile();
        var ba = new QC.CrystalWar.BeansAhoy().Compile();
        Check("crystalwar g12:Redeeming Rocks clean", "", string.Join(" | ", QuestScriptValidator.Validate(rr)));
        Check("crystalwar g12:Evil at the Inlet clean", "", string.Join(" | ", QuestScriptValidator.Validate(ei)));
        Check("crystalwar g12:Requiem for the Departed clean", "", string.Join(" | ", QuestScriptValidator.Validate(rd)));
        Check("crystalwar g12:Say It with a Handbag clean", "", string.Join(" | ", QuestScriptValidator.Validate(hb)));
        Check("crystalwar g12:Beans Ahoy clean", "", string.Join(" | ", QuestScriptValidator.Validate(ba)));
        Check("crystalwar g12:the declared steps (none: char vars, not steps)", " |  |  |  | ",
              string.Join(" | ", new[] { rr, ei, rd, hb, ba }.Select(d => string.Join(",", d.DeclaredSteps))));

        // the handler of that kind at that NPC (in whichever state holds it), run on a fake
        string R(QuestScriptDefinition d, QuestHandlerKind kind, ushort zone, string npc, Action<Fake> set = null,
                 QuestStatus status = QuestStatus.Accepted)
        {
            var h = d.States.SelectMany(s => s.Handlers)
                .Single(x => x.Kind == kind && x.Zone == zone && x.Entity == npc);
            var fk = new Fake { Status = status };
            set?.Invoke(fk);
            var r = h.Fixed ?? h.Reply(fk);
            return r == null ? "null" : QuestContextFake.Describe(r);
        }
        Fake Fin(QuestScriptDefinition d, ushort zone, int csid, Action<Fake> set = null, int option = 0,
                 bool complete = true)
        {
            var h = d.States.SelectMany(s => s.Handlers)
                .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Zone == zone && x.Id == csid);
            var fk = new Fake { Status = QuestStatus.Accepted, Csid = csid, Zone = zone, Option = option,
                                CompleteResult = complete, GiveResult = complete };
            set?.Invoke(fk);
            h.Run(fk);
            return fk;
        }
        string Calls(Fake fk) => string.Join(";", fk.Calls);
        // the state that holds the handler: does it match this status and these char vars
        string In(QuestScriptDefinition d, QuestHandlerKind kind, ushort zone, string npc, QuestStatus status,
                  Action<Fake> set = null)
        {
            var st = d.States.Single(s => s.Handlers.Any(x => x.Kind == kind && x.Zone == zone && x.Entity == npc));
            var fk = new Fake { Status = status };
            set?.Invoke(fk);
            return st.IsMatch(status, 0, fk).ToString();
        }
        var trig = QuestHandlerKind.OnTrigger;
        var trade = QuestHandlerKind.OnTrade;

        // Redeeming Rocks: Kocco 108 / 109 / 110 / 111, Rohn 114, the qm4 grants and answers no action
        ushort ww = QC.Zone.WindurstWatersS, vi = QC.Zone.VunkerlInletS, cn = QC.Zone.CrawlersNestS;
        Action<Fake, int> rprog = (f, v) => f.CharVars["RedeemingRocksProg"] = v;
        string kocco = QC.Npc.WindurstWatersS.KoccoEhllek;
        Check("crystalwar g12 kinds:Redeeming Rocks (108, 109, 110, 111, 114, qm4)",
              "Event(108) | Event(109) | Event(110) | Event(111) | Event(114) | NoAction()",
              string.Join(" | ",
                  R(rr, trig, ww, kocco, null, QuestStatus.Available),
                  R(rr, trig, ww, kocco, f => rprog(f, 2)),
                  R(rr, trig, ww, kocco, f => rprog(f, 4)),
                  R(rr, trig, ww, kocco, f => { rprog(f, 0); f.UniqueDay = 5; }),
                  R(rr, trig, ww, QC.Npc.WindurstWatersS.RohnEhlbalna, f => rprog(f, 1)),
                  R(rr, trig, vi, QC.Npc.VunkerlInletS.qm4, f => rprog(f, 3))));
        // 111 completes only when the earring is given; 110, 109, 108 and 114 write their char vars in any case
        Check("crystalwar g12 pins:Redeeming Rocks 111 when the earring cannot be given: nothing more",
              "Player.GiveItem(15998)", Calls(Fin(rr, ww, 111, null, 0, false)));

        // the arms' guards: prog past 4, a day ahead of today, a completed quest at prog 2, and who answers when
        Check("crystalwar g12 pins:Redeeming Rocks guards (prog 5 / a later day / completed at prog 2)",
              "null | Event(111) | null",
              string.Join(" | ",
                  R(rr, trig, ww, kocco, f => { rprog(f, 5); f.UniqueDay = 5; f.CharVars["RedeemingDay"] = 5; }),
                  R(rr, trig, ww, kocco, f => { rprog(f, 0); f.UniqueDay = 5; f.CharVars["RedeemingDay"] = 9; }),
                  R(rr, trig, ww, kocco, f => rprog(f, 2), QuestStatus.Completed)));
        Check("crystalwar g12 pins:Redeeming Rocks states (Rohn accepted at prog 1 only; qm4 any status at prog 3)",
              "True | False | False | True | True | False",
              string.Join(" | ",
                  In(rr, trig, ww, QC.Npc.WindurstWatersS.RohnEhlbalna, QuestStatus.Accepted, f => rprog(f, 1)),
                  In(rr, trig, ww, QC.Npc.WindurstWatersS.RohnEhlbalna, QuestStatus.Completed, f => rprog(f, 1)),
                  In(rr, trig, ww, QC.Npc.WindurstWatersS.RohnEhlbalna, QuestStatus.Accepted, f => rprog(f, 2)),
                  In(rr, trig, vi, QC.Npc.VunkerlInletS.qm4, QuestStatus.Accepted, f => rprog(f, 3)),
                  In(rr, trig, vi, QC.Npc.VunkerlInletS.qm4, QuestStatus.Completed, f => rprog(f, 3)),
                  In(rr, trig, vi, QC.Npc.VunkerlInletS.qm4, QuestStatus.Accepted, f => rprog(f, 4))));

        // Evil at the Inlet
        string rod = QC.Npc.CrawlersNestS.Rodeupansat;
        Action<Fake> ribbon30 = f => { f.KeyItems[QC.KeyItem.BronzeRibbonOfService] = true; f.MainLevel = 30; };
        Check("crystalwar g12 kinds:Evil at the Inlet (107, 108, 109, 110, qm6)",
              "Event(107) | Event(108) | Event(109) | Event(110) | Event(112)",
              string.Join(" | ",
                  R(ei, trig, cn, rod, ribbon30, QuestStatus.Available),
                  R(ei, trig, cn, rod, f => f.KeyItems[QC.KeyItem.EvilWardingSeal] = true),
                  R(ei, trig, cn, rod),
                  R(ei, trig, cn, rod, null, QuestStatus.Completed),
                  R(ei, trig, vi, QC.Npc.VunkerlInletS.qm6)));
        Check("crystalwar g12 pins:Evil at the Inlet offer (no ribbon / level 29) and 110 (available, level 30, ribbon)",
              "null | null | null",
              string.Join(" | ",
                  R(ei, trig, cn, rod, f => f.MainLevel = 30, QuestStatus.Available),
                  R(ei, trig, cn, rod, f => { f.KeyItems[QC.KeyItem.BronzeRibbonOfService] = true; f.MainLevel = 29; },
                    QuestStatus.Available),
                  R(ei, trig, cn, rod, f => { ribbon30(f); f.MainLevel = 29; }, QuestStatus.Available)));
        Check("crystalwar g12 pins:Evil at the Inlet qm6 answers at the accepted quest with the seal only",
              "True | False | False",
              string.Join(" | ",
                  In(ei, trig, vi, QC.Npc.VunkerlInletS.qm6, QuestStatus.Accepted,
                     f => f.KeyItems[QC.KeyItem.EvilWardingSeal] = true),
                  In(ei, trig, vi, QC.Npc.VunkerlInletS.qm6, QuestStatus.Completed,
                     f => f.KeyItems[QC.KeyItem.EvilWardingSeal] = true),
                  In(ei, trig, vi, QC.Npc.VunkerlInletS.qm6, QuestStatus.Accepted)));
        Check("crystalwar g12 pins:Evil at the Inlet 109 when the scroll cannot be given: nothing more",
              "Player.GiveItem(4687)", Calls(Fin(ei, cn, 109, null, 0, false)));

        // Requiem for the Departed
        ushort en = QC.Zone.TheEldiemeNecropolisS, fk2 = QC.Zone.FortKarugoNarugoS;
        string hep = QC.Npc.TheEldiemeNecropolisS.Heptachiond;
        Check("crystalwar g12 kinds:Requiem for the Departed (105, 107, 106, 108, 234)",
              "Event(105) | Event(107) | Event(106) | Event(108) | Event(234)",
              string.Join(" | ",
                  R(rd, trig, en, hep, ribbon30, QuestStatus.Available),
                  R(rd, trig, en, hep, f => f.KeyItems[QC.KeyItem.SheafOfHandmadeIncense] = true),
                  R(rd, trig, en, hep),
                  R(rd, trig, en, hep, null, QuestStatus.Completed),
                  R(rd, trig, fk2, QC.Npc.FortKarugoNarugoS.PeccaPocca)));
        Check("crystalwar g12 pins:Requiem offer (no ribbon / level 29 / ribbon only)",
              "null | null | null",
              string.Join(" | ",
                  R(rd, trig, en, hep, f => f.MainLevel = 30, QuestStatus.Available),
                  R(rd, trig, en, hep, f => { f.KeyItems[QC.KeyItem.BronzeRibbonOfService] = true; f.MainLevel = 29; },
                    QuestStatus.Available),
                  R(rd, trig, en, hep, f => f.KeyItems[QC.KeyItem.BronzeRibbonOfService] = true, QuestStatus.Available)));
        Check("crystalwar g12 pins:Requiem for the Departed 107 when the scroll cannot be given (the incense stays)",
              "Player.GiveItem(4689)", Calls(Fin(rd, en, 107, null, 0, false)));

        // Say It with a Handbag
        string hampu = QC.Npc.WindurstWatersS.HampuKampu;
        ushort sd = QC.Zone.SouthernSanDoria;
        string kip = QC.Npc.SouthernSanDoria.Kipopo;
        Action<Fake, int> hcs = (f, v) => f.CharVars["sayItWithAHandbagCS"] = v;
        Check("crystalwar g12 kinds:Say It with a Handbag (169, 170, 172, 173, 174, 175, 171)",
              "Event(169) | Event(170) | Event(172) | Event(173) | Event(174) | Event(175) | Event(171)",
              string.Join(" | ",
                  R(hb, trig, ww, hampu, null, QuestStatus.Available),
                  R(hb, trig, ww, hampu, f => hcs(f, 0)),
                  R(hb, trig, ww, hampu, f => hcs(f, 1)),
                  R(hb, trig, ww, hampu, f => hcs(f, 3)),
                  R(hb, trig, ww, hampu, f => { hcs(f, 4); f.KeyItems[QC.KeyItem.RepairedHandbag] = true; }),
                  R(hb, trig, ww, hampu, null, QuestStatus.Completed),
                  R(hb, trig, ww, QC.Npc.WindurstWatersS.DoorAcolyteHostelup, f => hcs(f, 0))));
        Check("crystalwar g12 kinds:Kipopo (914, 913, 911, 912, 909, 908, none)",
              "Event(914) | Event(913) | Event(911) | Event(912) | Event(909) | Event(908) | null",
              string.Join(" | ",
                  R(hb, trig, sd, kip, f => f.CharVars["sayItWithAHandbagBonusCS"] = 1, QuestStatus.Completed),
                  R(hb, trig, sd, kip, f => { hcs(f, 4); f.KeyItems[QC.KeyItem.RepairedHandbag] = true; }),
                  R(hb, trig, sd, kip, f => { hcs(f, 3); f.SetNeedToZone(true); }),
                  R(hb, trig, sd, kip, f => hcs(f, 3)),
                  R(hb, trig, sd, kip, f => hcs(f, 2)),
                  R(hb, trig, sd, kip, f => { hcs(f, 1); f.KeyItems[QC.KeyItem.TornPatchesOfLeather] = true; }),
                  R(hb, trig, sd, kip)));

        // Kipopo's 910 trade is npcUtil.tradeHasExactly: the three items and nothing else (HasExactly, not HasItemQty)
        int ram = 2012, leather = QC.Item.SquareOfSheepLeather, thread = QC.Item.SpoolOfSilkThread;
        Action<Fake> torn2 = f => { hcs(f, 2); f.KeyItems[QC.KeyItem.TornPatchesOfLeather] = true; };
        Action<Fake, int[]> offer = (f, items) => { foreach (var i in items) f.TradeItems[i] = 1; };
        Check("crystalwar g12 pins:Kipopo's 910 trade: exact three / an extra item / a missing one / no key item / CS 1",
              "Event(910) | null | null | null | null",
              string.Join(" | ",
                  R(hb, trade, sd, kip, f => { torn2(f); offer(f, new[] { ram, leather, thread }); }),
                  R(hb, trade, sd, kip, f => { torn2(f); offer(f, new[] { ram, leather, thread, 4096 }); }),
                  R(hb, trade, sd, kip, f => { torn2(f); offer(f, new[] { ram, leather }); }),
                  R(hb, trade, sd, kip, f => { hcs(f, 2); offer(f, new[] { ram, leather, thread }); }),
                  R(hb, trade, sd, kip, f =>
                  {
                      hcs(f, 1); f.KeyItems[QC.KeyItem.TornPatchesOfLeather] = true; offer(f, new[] { ram, leather, thread });
                  })));
        Action<Fake> handbag = f => f.KeyItems[QC.KeyItem.RepairedHandbag] = true;
        Check("crystalwar g12 pins:Hampu-Kampu guards: 174 needs the KI and CS 4 / 173 by KI or CS 3 alone / 172 accepted only",
              "Event(170) | Event(170) | Event(173) | Event(173) | Event(169)",
              string.Join(" | ",
                  R(hb, trig, ww, hampu, f => handbag(f)),
                  R(hb, trig, ww, hampu, f => hcs(f, 4)),
                  R(hb, trig, ww, hampu, f => { hcs(f, 3); }),
                  R(hb, trig, ww, hampu, f => f.KeyItems[QC.KeyItem.TornPatchesOfLeather] = true),
                  R(hb, trig, ww, hampu, f => hcs(f, 1), QuestStatus.Available)));
        Check("crystalwar g12 pins:Kipopo guards (bonus 1 not completed / bonus 2 / CS 4 no KI / KI no CS 4 / CS 1 no KI)",
              "null | null | null | null | null",
              string.Join(" | ",
                  R(hb, trig, sd, kip, f => f.CharVars["sayItWithAHandbagBonusCS"] = 1),
                  R(hb, trig, sd, kip, f => f.CharVars["sayItWithAHandbagBonusCS"] = 2, QuestStatus.Completed),
                  R(hb, trig, sd, kip, f => hcs(f, 4)),
                  R(hb, trig, sd, kip, f => handbag(f)),
                  R(hb, trig, sd, kip, f => hcs(f, 1))));
        Check("crystalwar g12 pins:the Door answers 171 only at the accepted quest with CS 0",
              "True | False | False | False",
              string.Join(" | ",
                  In(hb, trig, ww, QC.Npc.WindurstWatersS.DoorAcolyteHostelup, QuestStatus.Accepted),
                  In(hb, trig, ww, QC.Npc.WindurstWatersS.DoorAcolyteHostelup, QuestStatus.Completed),
                  In(hb, trig, ww, QC.Npc.WindurstWatersS.DoorAcolyteHostelup, QuestStatus.Accepted, f => hcs(f, 1)),
                  In(hb, trig, ww, QC.Npc.WindurstWatersS.DoorAcolyteHostelup, QuestStatus.Available)));
        Check("crystalwar g12 pins:171 with option 2 sets nothing",
              "", Calls(Fin(hb, ww, 171, null, 2)));
        // 174 completes only when the needle is given (the handbag and the bonus var stay otherwise)
        Check("crystalwar g12 pins:Say It with a Handbag 174 when the needle cannot be given: nothing more",
              "Player.GiveItem(19110)", Calls(Fin(hb, ww, 174, null, 0, false)));
        Check("crystalwar g12 pins:171 sets the CS on option 1 only; 908 sets CS 2",
              "Player.SetCharVar(sayItWithAHandbagCS,1,0) | (none) | Player.SetCharVar(sayItWithAHandbagCS,2,0)",
              string.Join(" | ", Calls(Fin(hb, ww, 171, null, 1)),
                  Calls(Fin(hb, ww, 171, null, 0)) is { Length: 0 } ? "(none)" : "other",
                  Calls(Fin(hb, sd, 908, null, 1))));

        // Beans Ahoy
        ushort bs = QC.Zone.SouthernSanDoriaS;
        string thi = QC.Npc.SouthernSanDoriaS.Thierride;
        int salt = QC.Item.ChunkOfLufetSalt, cass = QC.Item.AnglersCassoulet;
        Action<Fake, int, bool> bean = (f, v, z) =>
        {
            f.CharVars["BeansAhoy"] = v;
            if (z) f.SetNeedToZone(true);
        };
        Check("crystalwar g12 kinds:Beans Ahoy (334, 335, 342, 341, 337, 340, the salt at needToZone)",
              "Event(334) | Event(335) | Event(342) | Event(341) | Event(337) | Event(340) | NoAction()",
              string.Join(" | ",
                  R(ba, trig, bs, thi, null, QuestStatus.Available),
                  R(ba, trig, bs, thi),
                  R(ba, trig, bs, thi, null, QuestStatus.Completed),
                  R(ba, trig, bs, thi, f => f.CharVars["BeansAhoy_ConquestWeek"] = 1, QuestStatus.Completed),
                  R(ba, trade, bs, thi, f => { offer(f, new[] { salt }); bean(f, 0, false); }),
                  R(ba, trade, bs, thi, f => { offer(f, new[] { salt }); bean(f, 1, false); }),
                  R(ba, trade, bs, thi, f => { offer(f, new[] { salt }); bean(f, 1, true); })));
        // the salt trade is hasItemQty(salt, 1) and getItemCount() == 1: two salts, or a salt and anything else, is
        // none
        Check("crystalwar g12 pins:Thierride's trade: one salt / two salts / salt and a cassoulet / another item",
              "Event(337) | null | null | null",
              string.Join(" | ",
                  R(ba, trade, bs, thi, f => offer(f, new[] { salt })),
                  R(ba, trade, bs, thi, f => f.TradeItems[salt] = 2),
                  R(ba, trade, bs, thi, f => offer(f, new[] { salt, cass })),
                  R(ba, trade, bs, thi, f => offer(f, new[] { cass }))));

        // GiveCassoulet (342 / 340): player:hasItem(cassoulet), every container (user ruling 2026-11-04, kept Lua bugs:
        // the Lua's hasItem(cassoulet, 1) read the Mog Safe only): one held anywhere, or a full bag, refuses
        Func<int, Action<Fake>, bool> adds = (csid, set) => Fin(ba, bs, csid, set).Calls.Any(c => c.StartsWith("Player.AddItem("));
        Func<int, Action<Fake>, bool> refuses = (csid, set) =>
            Fin(ba, bs, csid, set).Calls.Any(c => c.Contains("ITEM_CANNOT_BE_OBTAINED"));
        var hold = new HashSet<int> { cass };
        Check("crystalwar g12 pins:the cassoulet: none / in the bag / in the Mog Safe / full bag (342: gives, " +
              "refuses, refuses, refuses)",
              "True | False | False | False",
              string.Join(" | ",
                  adds(342, f => { }),
                  adds(342, f => f.Items.Add(cass)),
                  adds(342, f => f.ItemsElsewhere[1] = hold),
                  adds(342, f => f.FreeSlots = 0)));
        Check("crystalwar g12 pins:one free slot is enough for the cassoulet", "True", adds(342, f => f.FreeSlots = 1).ToString());
        Check("crystalwar g12 pins:the cassoulet refusal says so (safe / full bag / bag)",
              "True | True | True",
              string.Join(" | ",
                  refuses(342, f => f.ItemsElsewhere[1] = hold),
                  refuses(342, f => f.FreeSlots = 0),
                  refuses(342, f => f.Items.Add(cass))));
        Check("crystalwar g12 pins:a cassoulet in another container (wardrobe 8) is held too",
              "False", adds(342, f => f.ItemsElsewhere[8] = hold).ToString());
        // 340 completes the quest and the trade only when it gave the cassoulet; 342 never does
        Check("crystalwar g12 pins:340 completes on a given cassoulet, not on a refused one; 342 never",
              "True True True | False False False | False False False",
              string.Join(" | ",
                  Complete340(Fin(ba, bs, 340, f => { })),
                  Complete340(Fin(ba, bs, 340, f => f.ItemsElsewhere[1] = hold)),
                  Complete340(Fin(ba, bs, 342, f => { }))));
        string Complete340(Fake fk) =>
            string.Join(" ", fk.Calls.Any(c => c.StartsWith("Player.CompleteQuest(")),
                fk.Calls.Any(c => c.StartsWith("Player.SetCharVar(BeansAhoy,0")),
                fk.Calls.Any(c => c.StartsWith("Trade.Complete(")));
        Check("crystalwar g12 pins:the conquest-week var rides the tally (expiry set)", "True",
              Fin(ba, bs, 342, f => f.Tally = 777).CharVarExpiry.TryGetValue("BeansAhoy_ConquestWeek", out var ex)
                  && ex == 777u ? "True" : "False");
    }
}
