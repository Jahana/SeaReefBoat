// XI.Verify.Quests -- quest rework B3b Crystal War group 7 (San d'Oria 5-8: Perils of the Griffon, In a Haze of Glory,
// The Price of Valor, Bonds That Never Die): what no trace can see (validator, declared steps, the three mob-by-id
// facet members).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunCrystalWarGroup7()
    {
        Console.WriteLine("== quest rework B3b Crystal War group 7: the validator and the mob-by-id facet ==");
        var perils = new QC.CrystalWar.PerilsOfTheGriffon().Compile();
        var haze = new QC.CrystalWar.InAHazeOfGlory().Compile();
        var price = new QC.CrystalWar.ThePriceOfValor().Compile();
        var bonds = new QC.CrystalWar.BondsThatNeverDie().Compile();
        Check("crystalwar g7:Perils of the Griffon clean", "",
              string.Join(" | ", QuestScriptValidator.Validate(perils)));
        Check("crystalwar g7:In a Haze of Glory clean", "",
              string.Join(" | ", QuestScriptValidator.Validate(haze)));
        Check("crystalwar g7:The Price of Valor clean", "",
              string.Join(" | ", QuestScriptValidator.Validate(price)));
        Check("crystalwar g7:Bonds That Never Die clean", "",
              string.Join(" | ", QuestScriptValidator.Validate(bonds)));
        Check("crystalwar g7:the declared steps",
              "1,2,3 | 1,2,3 | 1,2,3,4,5,6 | 1,2,3,4,5",
              string.Join(" | ", new[] { perils, haze, price, bonds }.Select(d => string.Join(",", d.DeclaredSteps))));

        // the Price of Valor's NM: spawn by id, claimed; the fake records the call and counts the id as spawned
        var f = new Fake();
        f.MobIdsByName["Madthrasher_Zradbodd"] = 17117560;
        int id = f.MobIdByName("Madthrasher_Zradbodd");
        bool before = f.MobIsSpawnedById(id);
        f.SpawnMobById(id, true);
        Check("crystalwar g7:SpawnMobById records and spawns", "17117560:False:True:Player.SpawnMobById(17117560,True)",
              $"{id}:{before}:{f.MobIsSpawnedById(id)}:{string.Join(";", f.Calls)}");

        RunCrystalWarGroup7Kinds(perils, haze, price, bonds);
    }

    // The replies the traces cannot tell apart. Rholont's replies and their boundaries are shown by the #rivals flows
    // (a rival of each kind in the traces' order lines); qm6 (Perils / Claws) and Rongelouts N Distaud (Bonds / Chasing
    // Shadows) by the flows that talk beside their rivals (claws_of_the_griffon#qm6, the Bonds #rivals); the boundaries
    // a natural state reaches by the main flows and #edges. Left here: the NPCs with no script of their own and no
    // rival in the state (Diordinne, the Wooden Crates, the Tree Hollow, the Well, Felled Trees, the Toppled Cressets,
    // the Underbrush, the Shimmering Pondweed, the Overgrown Mushrooms, the Mossy Stump), the zone-ins, the Rholont arms
    // the flows reach only through a rival, GM-only states (Felled Trees at an available Prog 3: v27; Rongelouts at
    // Prog 4: b25) and the instances' 10000 finishes (waived in the flows).
    static void RunCrystalWarGroup7Kinds(QuestScriptDefinition perils, QuestScriptDefinition haze,
                                         QuestScriptDefinition price, QuestScriptDefinition bonds)
    {
        var trig = QuestHandlerKind.OnTrigger;
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
        string Fin(QuestScriptDefinition d, ushort zone, int csid)
        {
            var h = d.States.SelectMany(s => s.Handlers)
                .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Zone == zone && x.Id == csid);
            var f = new Fake { Status = QuestStatus.Accepted, Csid = csid };
            h.Run(f);
            return string.Join(";", f.Calls);
        }
        var av = QuestStatus.Available;
        var ac = QuestStatus.Accepted;
        var co = QuestStatus.Completed;
        ushort ssd = QC.Zone.SouthernSanDoriaS, jug = QC.Zone.JugnerForestS, vun = QC.Zone.VunkerlInletS;
        ushort gar = QC.Zone.GarlaigeCitadelS, pas = QC.Zone.PashhowMarshlandsS;
        string dio = QC.Npc.GarlaigeCitadelS.Diordinne, crates = QC.Npc.GarlaigeCitadelS.WoodenCrates;
        string rho = QC.Npc.SouthernSanDoriaS.Rholont;
        string hollow = QC.Npc.SouthernSanDoriaS.TreeHollow, well = QC.Npc.SouthernSanDoriaS.Well;
        string felled = QC.Npc.JugnerForestS.FelledTrees;
        int biscuits = QC.KeyItem.LongLifeBiscuits, water = QC.KeyItem.FlaskOfKingdomWater;
        int syrup = QC.KeyItem.RonfaureMapleSyrup;
        Action<Fake> keys(params int[] kis) => f => { foreach (var k in kis) f.KeyItems[k] = true; };
        Action<Fake> ts(int timer) => f => f.Vars["Timer"] = timer;
        var got = new[]
        {
            // Haze: Diordinne (available; accepted with the key, with the Timer 0, due, due the same day and not yet)
            R(haze, 0, av, trig, gar, dio),
            R(haze, 1, ac, trig, gar, dio, 0, keys(QC.KeyItem.NumberEightShelterKey)),
            R(haze, 1, ac, trig, gar, dio, 0,
              f => { keys(QC.KeyItem.NumberEightShelterKey)(f); f.Vars["Option"] = 1; }),
            R(haze, 1, ac, trig, gar, dio, 0, ts(0)),
            R(haze, 1, ac, trig, gar, dio, 0, ts(7999)),
            R(haze, 1, ac, trig, gar, dio, 0, ts(8000)),
            R(haze, 1, ac, trig, gar, dio, 0, ts(8001)),
            // Haze: the Wooden Crates, the zone-in, Rholont by day
            R(haze, 1, ac, trig, gar, crates, 0), R(haze, 1, ac, trig, gar, crates, 1),
            R(haze, 1, ac, zin, gar, null, 1), R(haze, 1, ac, zin, gar, null, 2),
            R(haze, 1, ac, trig, ssd, rho, 3, f => f.Hour = 17),
            R(haze, 1, ac, trig, ssd, rho, 3, f => f.Hour = 18),
            R(haze, 1, ac, trig, ssd, rho, 2, f => f.Hour = 18),
            // Price: Rholont's three key items one at a time, the Tree Hollow, the Well, Felled Trees
            R(price, 0, av, trig, ssd, rho, 1, keys(biscuits, water, syrup)),
            R(price, 0, av, trig, ssd, rho, 1, keys(water, syrup)),
            R(price, 0, av, trig, ssd, rho, 1, keys(biscuits, syrup)),
            R(price, 0, av, trig, ssd, rho, 1, keys(biscuits, water)),
            R(price, 0, av, trig, ssd, hollow, 1), R(price, 0, av, trig, ssd, hollow, 1, keys(biscuits)),
            R(price, 0, av, trig, ssd, hollow, 2),
            R(price, 0, av, trig, ssd, well, 1), R(price, 0, av, trig, ssd, well, 1, keys(water)),
            R(price, 0, av, trig, ssd, well, 2),
            R(price, 0, av, trig, jug, felled, 1), R(price, 0, av, trig, jug, felled, 2),
            R(price, 0, av, trig, jug, felled, 3),
        };
        Check("crystalwar g7 kinds:the replies the flows cannot show (Haze Diordinne / crates / zone-in / Rholont by " +
              "day, Price Rholont's key items / Tree Hollow / Well / Felled Trees)",
              "ProgressEvent(31) Event(37) null ProgressEvent(34) ProgressEvent(36) ProgressEvent(36) " +
              "Event(35):oncePerZone() ProgressEvent(32) null null Event(33) Event(81) ProgressEvent(82) null " +
              "ProgressEvent(642) Event(643) Event(643) Event(643) ProgressEvent(640) null null ProgressEvent(641) " +
              "null null " +
              "null ProgressEvent(211) null", string.Join(" ", got));

        string mad = QC.Npc.VunkerlInletS.MadthrasherZradbodd;
        string c1 = QC.Npc.VunkerlInletS.ToppledCresset1, c2 = QC.Npc.VunkerlInletS.ToppledCresset2;
        string under = QC.Npc.VunkerlInletS.Underbrush, pond = QC.Npc.PashhowMarshlandsS.ShimmeringPondweed;
        Action<Fake> nm(int defeated) => f =>
        {
            f.MobIdsByName[mad] = 17117560;
            f.QuestLocalVars["nmDefeated"] = defeated;
        };
        var got2 = new[]
        {
            R(price, 1, ac, trig, vun, c1, 1), R(price, 1, ac, trig, vun, c1, 2), R(price, 1, ac, trig, vun, c1, 3),
            R(price, 1, ac, trig, vun, c2, 2), R(price, 1, ac, trig, vun, c2, 3),
            R(price, 1, ac, trig, vun, under, 3), R(price, 1, ac, trig, vun, under, 4),
            R(price, 1, ac, trig, vun, under, 5, nm(0)),
            R(price, 1, ac, trig, vun, under, 5, f => { nm(0)(f); f.SpawnedMobIds.Add(17117560); }),
            R(price, 1, ac, trig, vun, under, 5, nm(1)),
            R(price, 1, ac, trig, pas, pond, 5), R(price, 1, ac, trig, pas, pond, 6),
        };
        Check("crystalwar g7 kinds:the Price of Valor's Cressets, Underbrush and Pondweed (no script of their own)",
              "null ProgressEvent(202) null null ProgressEvent(203) null ProgressEvent(204) ProgressEvent(205) " +
              "MessageSpecial(83:NOW_IS_NOT_THE_TIME) ProgressEvent(206) null ProgressEvent(105)",
              string.Join(" ", got2));

        string mush = QC.Npc.JugnerForestS.OvergrownMushrooms, stump = QC.Npc.JugnerForestS.MossyStump;
        int ivy = QC.KeyItem.LengthOfJugnerIvy;
        var got3 = new[]
        {
            // Bonds: the Pondweed (available), the Mushrooms, the Mossy Stump, the zone-in
            R(bonds, 0, av, trig, pas, pond),
            R(bonds, 0, av, trig, pas, pond, 0, keys(QC.KeyItem.LetterToCountAurchiat)),
            R(bonds, 1, ac, trig, jug, mush, 0), R(bonds, 1, ac, trig, jug, mush, 1),
            R(bonds, 1, ac, trig, jug, mush, 1, keys(ivy)), R(bonds, 1, ac, trig, jug, mush, 3),
            R(bonds, 1, ac, trig, jug, stump, 1), R(bonds, 1, ac, trig, jug, stump, 2),
            R(bonds, 1, ac, trig, jug, stump, 3),
            R(bonds, 1, ac, zin, jug, null, 3), R(bonds, 1, ac, zin, jug, null, 4),
        };
        Check("crystalwar g7 kinds:Bonds That Never Die's Pondweed, Mushrooms, Mossy Stump and zone-in",
              "null ProgressEvent(107) ProgressEvent(212) null ProgressEvent(213) null null ProgressEvent(214) null " +
              "null Event(215)", string.Join(" ", got3));

        Check("crystalwar g7 kinds:the instances' 10000 finishes (Ghoyu's Reverie, Everbloom Hollow)",
              "Quest.SetStep(2);Player.SetPos(2.436,6.235,-99.966,127,164) | " +
              "Quest.SetStep(4);Player.SetPos(-285.717,0.5,88.107,98,82)",
              $"{Fin(haze, QC.Zone.GhoyusReverie, 10000)} | {Fin(bonds, QC.Zone.EverbloomHollow, 10000)}");
    }
}
