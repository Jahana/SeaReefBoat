// XI.Verify.Quests -- quest rework B3b Crystal War group 4 (Bastok 5-8: Storm on the Horizon, Fire in the Hole, Quelling
// the Storm, Honor Under Fire): what no trace can see.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunCrystalWarGroup4()
    {
        Console.WriteLine("== quest rework B3b Crystal War group 4: the validator, Npc.Name and the reply kinds ==");
        var storm = new QC.CrystalWar.StormOnTheHorizon().Compile();
        var fire = new QC.CrystalWar.FireInTheHole().Compile();
        var quelling = new QC.CrystalWar.QuellingTheStorm().Compile();
        var honor = new QC.CrystalWar.HonorUnderFire().Compile();
        Check("crystalwar g4:Storm on the Horizon clean", "", string.Join(" | ", QuestScriptValidator.Validate(storm)));
        Check("crystalwar g4:Fire in the Hole clean", "", string.Join(" | ", QuestScriptValidator.Validate(fire)));
        Check("crystalwar g4:Quelling the Storm clean", "", string.Join(" | ", QuestScriptValidator.Validate(quelling)));
        Check("crystalwar g4:Honor Under Fire clean", "", string.Join(" | ", QuestScriptValidator.Validate(honor)));

        // Fire in the Hole's finish 8 (`npc and npc:getName() == 'Solitary_Ant'`): no other North Gustaberg [S] entity
        // plays 8, so no trace takes the false arm. The fake's Npc.Name: Solitary Ant gives the key and Prog 1; another
        // name, or no npc, does nothing.
        string Finish8(string npcName)
        {
            var h = fire.States.SelectMany(s => s.Handlers).Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Id == 8);
            var f = new Fake { Status = QuestStatus.Accepted, Step = 2, Csid = 8 };
            if (npcName != null) f.Npc = new QuestNpcFake(17138486, f.Calls) { Name = npcName };
            h.Run(f);
            return $"{f.Step}:{string.Join(";", f.Calls)}";
        }
        Check("crystalwar g4:Fire in the Hole's finish 8 reads Npc.Name (Solitary Ant / another NPC / none)",
              "1:Player.GiveKeyItem(" + QC.KeyItem.SilvermineKey + ");Quest.SetStep(1) | 2: | 2:",
              $"{Finish8(QC.Npc.NorthGustabergS.SolitaryAnt)} | {Finish8(QC.Npc.NorthGustabergS.Gebhardt)} | {Finish8(null)}");

        // Honor Under Fire's finish 68 keeps the Lua's `if npcUtil.giveKeyItem(...)`. npcUtil.giveKeyItem answers true for
        // any key item, held or not, so no trace can see the test: the fake's give answering false (no Prog 3) pins it.
        string Finish68(bool gives)
        {
            var h = honor.States.SelectMany(s => s.Handlers).Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Id == 68);
            var f = new Fake { Status = QuestStatus.Accepted, Step = 2, Csid = 68, GiveResult = gives };
            h.Run(f);
            return $"{f.Step}:{string.Join(";", f.Calls)}";
        }
        Check("crystalwar g4:Honor Under Fire's finish 68 sets Prog 3 only when the grenade's give answers true",
              "3:Player.GiveKeyItem(" + QC.KeyItem.FlareGrenade + ");Quest.SetStep(3) | 2:Player.GiveKeyItem(" + QC.KeyItem.FlareGrenade + ")",
              $"{Finish68(true)} | {Finish68(false)}");

        RunCrystalWarGroup4Kinds(storm, fire, quelling, honor);
    }

    // The reply kinds no trace can tell apart. None of these NPCs has a script (no onTrigger or onTrade to alternate
    // with); the defaults (Gentle Tiger 109, Solitary Ant 112) rank below Event; and no other quest answers them at the
    // same time: the Bastok line is a chain (each quest wants the one before completed, and no quest of groups 3-4 has
    // a completed section at Gentle Tiger, Blatherix, Solitary Ant, the Monument, qm5, Paul, Biggorf, Wilhelmina, qm8 or
    // the Beastman Ensign), Better Part of Valor's Solitary Ant (accepted) and Fires of Discontent's qm5 (accepted) are
    // long done when these quests run, and Quelling's and Honor's qm8 exclude each other. On Sabbatical is available
    // only on SCH, but once accepted it answers Gentle Tiger (46 / 47, Progress) on any job: the four flows
    // crystalWar.on_sabbatical #rivals, #rivals2, #rivals3 and #rivals4 play that rival against these quests' Gentle
    // Tiger states: the offers and Event states they reach show their kind (an Event loses to 47, a Progress
    // alternates with it), the rest are pinned here.
    // (The Stonehoused Adit's 5 is not here: the Adit has a script, and the main flow's second 5 shows the kind.
    // The zone-ins' csids play as numbers either way.)
    static void RunCrystalWarGroup4Kinds(QuestScriptDefinition storm, QuestScriptDefinition fire, QuestScriptDefinition quelling,
                                         QuestScriptDefinition honor)
    {
        var trig = QuestHandlerKind.OnTrigger;
        var zin = QuestHandlerKind.OnZoneIn;
        string R(QuestScriptDefinition d, int state, QuestHandlerKind kind, ushort zone, string npc, int step = 0,
                 int prevZone = 0, int keyItem = 0, string var = null, params (int Item, int Qty)[] traded)
        {
            var h = d.States[state].Handlers.Single(x => x.Kind == kind && x.Zone == zone && x.Entity == npc);
            var f = new Fake { Status = state == 0 ? QuestStatus.Available : QuestStatus.Accepted, Step = step, PrevZone = prevZone };
            if (keyItem != 0) f.KeyItems[keyItem] = true;
            if (var != null) f.Vars[var] = 1;
            foreach (var (item, qty) in traded) f.TradeItems[item] = qty;
            var r = h.Fixed ?? h.Reply(f);
            return r == null ? "null" : QuestContextFake.Describe(r);
        }
        ushort bms = QC.Zone.BastokMarketsS, ngs = QC.Zone.NorthGustabergS, vis = QC.Zone.VunkerlInletS;
        string gt = QC.Npc.BastokMarketsS.GentleTiger, blat = QC.Npc.BastokMarketsS.Blatherix;
        string paul = QC.Npc.BastokMarketsS.Paul, big = QC.Npc.BastokMarketsS.Biggorf, wil = QC.Npc.BastokMarketsS.Wilhelmina;
        string qm8 = QC.Npc.VunkerlInletS.qm8, ensign = QC.Npc.VunkerlInletS.BeastmanEnsign;
        int silver = QC.KeyItem.SilvermineKey, grenade = QC.KeyItem.FlareGrenade;
        var got = new[]
        {
            // Storm on the Horizon
            R(storm, 0, zin, bms, null, 1, ngs),
            R(storm, 0, trig, bms, blat, 0), R(storm, 0, trig, bms, blat, 1),
            R(storm, 1, trig, bms, gt, 2), R(storm, 1, trig, bms, gt, 3),
            R(storm, 1, trig, ngs, QC.Npc.NorthGustabergS.Monument, 3), R(storm, 1, trig, QC.Zone.GraubergS, QC.Npc.GraubergS.qm5, 4),
            // Fire in the Hole
            R(fire, 0, zin, bms, null, 0, ngs),
            R(fire, 1, trig, ngs, QC.Npc.NorthGustabergS.SolitaryAnt, 1, 0, silver),
            R(fire, 1, trig, ngs, QC.Npc.NorthGustabergS.SolitaryAnt, 2),
            R(fire, 1, zin, ngs, null, 3, QC.Zone.RuhotzSilvermines), R(fire, 1, zin, bms, null, 4, ngs),
            R(fire, 1, trig, bms, gt, 5), R(fire, 1, trig, bms, gt, 6),
            // Quelling the Storm: available, then Prog 0-7 (states 1-8)
            R(quelling, 0, zin, bms, null, 0, ngs),
            R(quelling, 1, trig, bms, gt, 0),
            R(quelling, 2, trig, bms, gt, 1),
            R(quelling, 2, trig, bms, paul, 1), R(quelling, 2, trig, bms, paul, 1, 0, 0, "Paul"),
            R(quelling, 2, trig, bms, big, 1), R(quelling, 2, trig, bms, big, 1, 0, 0, "Biggorf"),
            R(quelling, 2, trig, bms, wil, 1), R(quelling, 2, trig, bms, wil, 1, 0, 0, "Wilhelmina"),
            R(quelling, 3, trig, bms, paul, 2), R(quelling, 3, trig, bms, big, 2), R(quelling, 3, trig, bms, wil, 2),
            R(quelling, 3, trig, bms, gt, 2),
            R(quelling, 4, trig, bms, gt, 3), R(quelling, 4, trig, bms, blat, 3),
            R(quelling, 5, trig, bms, gt, 4), R(quelling, 5, trig, bms, blat, 4),
            R(quelling, 5, QuestHandlerKind.OnTrade, bms, blat, 4, 0, 0, null,
              (QC.Item.GoblinMessTin, 1), (QC.Item.GoblinMushpot, 1), (QC.Item.PinchOfTwinklePowder, 1)),
            R(quelling, 6, trig, bms, blat, 5), R(quelling, 6, trig, bms, gt, 5),
            R(quelling, 7, trig, vis, qm8, 6), R(quelling, 7, trig, bms, gt, 6), R(quelling, 7, trig, bms, blat, 6),
            R(quelling, 8, trig, bms, gt, 7), R(quelling, 8, trig, bms, blat, 7),
            // Honor Under Fire: available, then Prog 0-6 (states 1-7)
            R(honor, 0, trig, bms, gt),
            R(honor, 1, trig, bms, gt, 0), R(honor, 1, trig, vis, qm8, 0),
            R(honor, 2, trig, bms, gt, 1), R(honor, 2, trig, vis, ensign, 1),
            R(honor, 3, trig, bms, gt, 2),
            R(honor, 4, trig, bms, gt, 3), R(honor, 4, trig, vis, ensign, 3),
            R(honor, 5, trig, bms, gt, 4, 0, grenade), R(honor, 5, trig, bms, gt, 4),
            R(honor, 6, zin, vis, null, 5, QC.Zone.EverbloomHollow),
            R(honor, 7, trig, bms, gt, 6),
        };
        Check("crystalwar g4 kinds:the replies at NPCs with no script and no rival at the same rank (Gentle Tiger, Blatherix, " +
              "the Monument, qm5, Solitary Ant, Paul, Biggorf, Wilhelmina, qm8, the Beastman Ensign) and the zone-ins",
              "Event(56) ProgressEvent(37) Event(38) ProgressEvent(57) Event(58) ProgressEvent(4) ProgressEvent(16) | " +
              "Event(60) ProgressEvent(7) ProgressEvent(8) Event(6) Event(62) ProgressEvent(63) ProgressEvent(65) | " +
              "Event(179) ProgressEvent(180) Event(186) ProgressEvent(193) Event(196) ProgressEvent(194) Event(197) " +
              "ProgressEvent(192) Event(195) Event(196) Event(197) Event(195) ProgressEvent(181) Event(187) ProgressEvent(182) " +
              "Event(187) Event(188) ProgressEvent(183) Event(189) ProgressEvent(184) ProgressEvent(113) Event(190) Event(189) " +
              "Event(191) ProgressEvent(185) | " +
              "ProgressEvent(66) Event(67) ProgressEvent(114) Event(67) ProgressEvent(115) ProgressEvent(68) Event(69) " +
              "ProgressEvent(116) Event(69) ProgressEvent(70) Event(117) ProgressEvent(71)",
              $"{string.Join(" ", got.Take(7))} | {string.Join(" ", got.Skip(7).Take(7))} | {string.Join(" ", got.Skip(14).Take(25))} | " +
              string.Join(" ", got.Skip(39)));
    }
}
