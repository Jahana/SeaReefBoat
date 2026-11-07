// XI.Verify.Quests -- quest rework B3b Crystal War group 6 (San d'Oria 1-4: Gifts of the Griffon, Claws of the Griffon,
// Boy and the Beast, Wrath of the Griffon): what no trace can see.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunCrystalWarGroup6()
    {
        Console.WriteLine("== quest rework B3b Crystal War group 6: the validator and the reply kinds ==");
        var gifts = new QC.CrystalWar.GiftsOfTheGriffon().Compile();
        var claws = new QC.CrystalWar.ClawsOfTheGriffon().Compile();
        var boy = new QC.CrystalWar.BoyAndTheBeast().Compile();
        var wrath = new QC.CrystalWar.WrathOfTheGriffon().Compile();
        Check("crystalwar g6:Gifts of the Griffon clean", "", string.Join(" | ", QuestScriptValidator.Validate(gifts)));
        Check("crystalwar g6:Claws of the Griffon clean", "", string.Join(" | ", QuestScriptValidator.Validate(claws)));
        Check("crystalwar g6:Boy and the Beast clean", "", string.Join(" | ", QuestScriptValidator.Validate(boy)));
        Check("crystalwar g6:Wrath of the Griffon clean", "", string.Join(" | ", QuestScriptValidator.Validate(wrath)));
        // the steps each quest says it sets (the validator does not count them against what the handlers set)
        Check("crystalwar g6:the declared steps",
              "1,2 | 1,2,3,4 | 1,2,3 | 1,2,3",
              string.Join(" | ", new[] { gifts, claws, boy, wrath }.Select(d => string.Join(",", d.DeclaredSteps))));

        string Finish(QuestScriptDefinition d, int csid, Action<Fake> set, ushort zone)
        {
            var h = d.States.SelectMany(s => s.Handlers)
                .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Id == csid && x.Zone == zone);
            var f = new Fake { Status = QuestStatus.Accepted, Csid = csid, UniqueDay = 8000 };
            set(f);
            h.Run(f);
            return $"{f.Step}:{string.Join(";", f.Calls)}";
        }
        string Death(QuestScriptDefinition d, string mob, int step)
        {
            var h = d.States.SelectMany(s => s.Handlers)
                .Single(x => x.Kind == QuestHandlerKind.OnMobDeath && x.Entity == mob);
            var f = new Fake { Status = QuestStatus.Accepted, Step = step };
            h.Run(f);
            return $"{f.Step}:{string.Join(";", f.Calls)}";
        }
        ushort ssd = QC.Zone.SouthernSanDoriaS;
        // Gifts' finish 35 gives the plumes not yet traded (all 7 bits of Option count, bit 6 included); Wrath's finish
        // 60 writes Perils of the Griffon's Timer and mustZone only when the completion answers true; the two mobs'
        // deaths advance only at their own Prog
        Check("crystalwar g6:Gifts' 35 counts all 7 bits of Option",
              "0:Quest.SetVar(Timer,0,0);Player.GiveItems(2528x6)",
              Finish(gifts, 35, f => f.Vars["Option"] = 64, ssd));
        Check("crystalwar g6:Wrath's 60 writes Perils' Timer and mustZone only when Complete answers true",
              "0:Quest.Complete();Player.SetQuestVar(CrystalWar,PERILS_OF_THE_GRIFFON,Timer,8001);" +
              "Player.SetQuestMustZone(CrystalWar,PERILS_OF_THE_GRIFFON) | 0:Quest.Complete()",
              $"{Finish(wrath, 60, f => { }, ssd)} | {Finish(wrath, 60, f => f.CompleteResult = false, ssd)}");
        Check("crystalwar g6:Claws' Fingerfilcher and Wrath's Cobraclaw count only at their step",
              "4:Quest.SetStep(4) 4: 2:Quest.SetStep(2) 2:",
              $"{Death(claws, QC.Npc.JugnerForestS.FingerfilcherDradzad, 3)} " +
                   $"{Death(claws, QC.Npc.JugnerForestS.FingerfilcherDradzad, 4)} " +
                   $"{Death(wrath, QC.Npc.JugnerForestS.CobraclawBuchzvotch, 1)} " +
                   $"{Death(wrath, QC.Npc.JugnerForestS.CobraclawBuchzvotch, 2)}");

        RunCrystalWarGroup6Kinds(gifts, claws, boy, wrath);
    }

    // The replies the traces cannot tell apart. The replies at Louxiard, Rholont, qm6 and Raustigne and their Timer /
    // Option / Prog boundaries are shown by the #rivals, #louxiard, #qm6 and #mission flows (a rival of each kind in
    // the traces' order lines). Left here: a trade (no rival), qm7, qm8 and the Leafy Patch (no script of their own,
    // nothing else answers them) and Wyatt (Seeing Spots' 2 is group 11, still Lua: 66's priority 1005 wins it, as
    // the main flow shows); the zone-in. The hour boundaries are caught by boy_and_the_beast#rivals.
    static void RunCrystalWarGroup6Kinds(QuestScriptDefinition gifts, QuestScriptDefinition claws,
                                         QuestScriptDefinition boy, QuestScriptDefinition wrath)
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
        ushort ssd = QC.Zone.SouthernSanDoriaS, jug = QC.Zone.JugnerForestS, vun = QC.Zone.VunkerlInletS;
        string lou = QC.Npc.SouthernSanDoriaS.Louxiard, rho = QC.Npc.SouthernSanDoriaS.Rholont;
        string rau = QC.Npc.SouthernSanDoriaS.Raustigne, wya = QC.Npc.SouthernSanDoriaS.Wyatt;
        string qm6 = QC.Npc.JugnerForestS.qm6, qm7 = QC.Npc.VunkerlInletS.qm7, qm8 = QC.Npc.JugnerForestS.qm8;
        string leaf = QC.Npc.VunkerlInletS.LeafyPatch;
        Action<Fake> now(int t, int timer) => f => { f.Now = t; f.Vars["Timer"] = timer; };
        Action<Fake> hour(int h) => f => f.Hour = h;
        Action<Fake> herb(int step) => f => { f.KeyItems[QC.KeyItem.VunkerlHerb] = true; f.Step = step; };
        var got = new[]
        {
            // Gifts: a trade at Louxiard (a trade has no rival in the traces' order lines). The replies at Louxiard,
            // Rholont, qm6 and Raustigne and their Timer / Option / Prog boundaries are shown by the #rivals,
            // #louxiard, #qm6 and #mission flows (each rival's event shows in the traces' order lines)
            R(gifts, 1, ac, trade, ssd, lou, 2, f => f.TradeItems[QC.Item.PlumeDor] = 1),
            // Claws
            R(claws, 1, ac, zin, jug, null, 1, f => f.PrevZone = QC.Zone.EastRonfaureS),
            // Boy and the Beast: qm7 and the Leafy Patch have no script of their own and no rival in these states
            R(boy, 0, av, trig, vun, qm7, 1), R(boy, 0, av, trig, vun, qm7, 2), R(boy, 0, av, trig, vun, qm7, 3),
            R(boy, 1, ac, trig, vun, qm7, 3, herb(3)), R(boy, 1, ac, trig, vun, qm7, 2, herb(2)),
            R(boy, 1, ac, trig, vun, qm7, 2), R(boy, 1, ac, trig, vun, qm7, 2, f => f.Vars["Option"] = 1),
            R(boy, 1, ac, trig, vun, leaf, 2, hour(16)), R(boy, 1, ac, trig, vun, leaf, 2, hour(15)),
            R(boy, 1, ac, trig, vun, leaf, 2, hour(8)), R(boy, 1, ac, trig, vun, leaf, 2, hour(7)),
            R(boy, 1, ac, trig, vun, leaf, 2, herb(2)),
            // Wrath of the Griffon: qm8 (no rival) and Wyatt (Seeing Spots' 2 is group 11, still Lua)
            R(wrath, 1, ac, trig, jug, qm8, 0), R(wrath, 1, ac, trig, jug, qm8, 1),
            R(wrath, 1, ac, trig, jug, qm8, 1, f =>
                f.SpawnedMobs.Add(QC.Mob.JugnerForestS.COBRACLAW_BUCHZVOTCH + "+0")),
            R(wrath, 1, ac, trig, jug, qm8, 2), R(wrath, 1, ac, trig, jug, qm8, 3),
            R(wrath, 2, co, trig, ssd, wya),
        };
        Check("crystalwar g6 kinds:the replies the flows cannot show (a trade, qm7, qm8, the Leafy Patch, Wyatt) " +
              "and the hour boundaries",
              "ProgressEvent(26) Event(200) null ProgressEvent(105) null ProgressEvent(108) ProgressEvent(109) " +
              "Event(110) Event(111) ProgressEvent(107,2) ProgressEvent(107,1) ProgressEvent(107,1) " +
              "ProgressEvent(107,0) null ProgressEvent(204) ProgressEvent(205) null ProgressEvent(206) null " +
              "ProgressEvent(66):setPriority(1005)",
              string.Join(" ", got));
    }
}
