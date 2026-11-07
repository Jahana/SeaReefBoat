// XI.Verify.Quests -- quest rework B3b Abyssea group 4 (the Dominion Ops of Abyssea - Altepa and Abyssea - Grauberg,
// #01-#14 each): each definition validates clean, declares no steps and has the Lua's one section (accepted), whose one
// handler is the op's target's OnMobDeath, calling xi.abyssea.dominionOnMobDeath with the op's own id. The flows
// (tests/golden/flows/abyssea.dominion_op_*.flow) prove each op's zone, mob and op id by its own kills; this pins the
// same table, the Lua files' data, on the fake.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunAbysseaGroup4()
    {
        Console.WriteLine("== quest rework B3b Abyssea group 4 ==");
        var ops = new (string Name, QuestScript Quest)[]
        {
            ("Altepa #01", new QC.Abyssea.DominionOp01Altepa()),
            ("Altepa #02", new QC.Abyssea.DominionOp02Altepa()),
            ("Altepa #03", new QC.Abyssea.DominionOp03Altepa()),
            ("Altepa #04", new QC.Abyssea.DominionOp04Altepa()),
            ("Altepa #05", new QC.Abyssea.DominionOp05Altepa()),
            ("Altepa #06", new QC.Abyssea.DominionOp06Altepa()),
            ("Altepa #07", new QC.Abyssea.DominionOp07Altepa()),
            ("Altepa #08", new QC.Abyssea.DominionOp08Altepa()),
            ("Altepa #09", new QC.Abyssea.DominionOp09Altepa()),
            ("Altepa #10", new QC.Abyssea.DominionOp10Altepa()),
            ("Altepa #11", new QC.Abyssea.DominionOp11Altepa()),
            ("Altepa #12", new QC.Abyssea.DominionOp12Altepa()),
            ("Altepa #13", new QC.Abyssea.DominionOp13Altepa()),
            ("Altepa #14", new QC.Abyssea.DominionOp14Altepa()),
            ("Grauberg #01", new QC.Abyssea.DominionOp01Grauberg()),
            ("Grauberg #02", new QC.Abyssea.DominionOp02Grauberg()),
            ("Grauberg #03", new QC.Abyssea.DominionOp03Grauberg()),
            ("Grauberg #04", new QC.Abyssea.DominionOp04Grauberg()),
            ("Grauberg #05", new QC.Abyssea.DominionOp05Grauberg()),
            ("Grauberg #06", new QC.Abyssea.DominionOp06Grauberg()),
            ("Grauberg #07", new QC.Abyssea.DominionOp07Grauberg()),
            ("Grauberg #08", new QC.Abyssea.DominionOp08Grauberg()),
            ("Grauberg #09", new QC.Abyssea.DominionOp09Grauberg()),
            ("Grauberg #10", new QC.Abyssea.DominionOp10Grauberg()),
            ("Grauberg #11", new QC.Abyssea.DominionOp11Grauberg()),
            ("Grauberg #12", new QC.Abyssea.DominionOp12Grauberg()),
            ("Grauberg #13", new QC.Abyssea.DominionOp13Grauberg()),
            ("Grauberg #14", new QC.Abyssea.DominionOp14Grauberg()),
        };
        foreach (var o in ops)
        {
            Check($"abyssea g4:{o.Name} clean", "",
                  string.Join(" | ", QuestScriptValidator.Validate(o.Quest.Compile())));
            Check($"abyssea g4:{o.Name} declares steps []", "", string.Join(",", o.Quest.Compile().DeclaredSteps));
        }

        // the statuses and steps each state matches, state by state
        string States(QuestScript quest) => string.Join(" ", quest.Compile().States.Select(s => string.Join("/",
            s.Matches.Select(m => (m.AnyStatus ? "any" : m.Status.ToString()) + (m.Step is int st ? ":" + st : "") +
                                  (m.When == null ? "" : "?")))));
        Check("abyssea g4:the states' statuses and steps", string.Join(" | ", Enumerable.Repeat("Accepted", 28)),
              string.Join(" | ", ops.Select(o => States(o.Quest))));

        // each op's handlers: its zone, its target and the call its run makes on an accepted fake
        string Handlers(QuestScript quest) => string.Join(",",
            quest.Compile().States.SelectMany(s => s.Handlers).Select(h =>
            {
                var f = new Fake { Status = QuestStatus.Accepted };
                h.Run?.Invoke(f);
                string kind = h.Kind == QuestHandlerKind.OnMobDeath && !h.Called ? "" : h.Kind + "!";
                return $"{h.Zone}/{h.Entity}:{kind}" + string.Join(";", f.Calls);
            }));
        Check("abyssea g4:each op's one handler: its target's OnMobDeath calls dominionOnMobDeath with its op id",
              "218/Sand_Sweeper:Player.DominionOnMobDeath(560) ; 218/Surveyor:Player.DominionOnMobDeath(561) ; " +
              "218/Bonfire:Player.DominionOnMobDeath(562) ; 218/Dune_Manticore:Player.DominionOnMobDeath(563) ; " +
              "218/Manigordo:Player.DominionOnMobDeath(564) ; 218/Camelopardalis:Player.DominionOnMobDeath(565) ; " +
              "218/Desert_Puk:Player.DominionOnMobDeath(566) ; 218/Ergdrake:Player.DominionOnMobDeath(567) ; " +
              "218/Dune_Cockatrice:Player.DominionOnMobDeath(568) ; 218/Gastornis:Player.DominionOnMobDeath(569) ; " +
              "218/Nannakola:Player.DominionOnMobDeath(570) ; 218/Akrab:Player.DominionOnMobDeath(571) ; " +
              "218/Fear_Dearg:Player.DominionOnMobDeath(572) ; 218/Barrens_Treant:Player.DominionOnMobDeath(573) ; " +
              "254/Sinister_Seidel:Player.DominionOnMobDeath(588) ; 254/Monitor:Player.DominionOnMobDeath(589) ; " +
              "254/Stygian_Djinn:Player.DominionOnMobDeath(590) ; " +
              "254/Faunus_Wyvern:Player.DominionOnMobDeath(591) ; " +
              "254/Putrid_Peapuk:Player.DominionOnMobDeath(592) ; 254/Glade_Wivre:Player.DominionOnMobDeath(593) ; " +
              "254/Sensenmann:Player.DominionOnMobDeath(594) ; 254/Deimobugard:Player.DominionOnMobDeath(595) ; " +
              "254/Peak_Pugil:Player.DominionOnMobDeath(596) ; 254/Glen_Crab:Player.DominionOnMobDeath(597) ; " +
              "254/Seelie:Player.DominionOnMobDeath(598) ; 254/Unseelie:Player.DominionOnMobDeath(599) ; " +
              "254/Goblin_Plunderer:Player.DominionOnMobDeath(600) ; " +
              "254/Goblin_Meatgrinder:Player.DominionOnMobDeath(601)",
              string.Join(" ; ", ops.Select(o => Handlers(o.Quest))));

        // each op's LegacyPath, its own Lua file: the slot in the containers list (two neighbours swapped land in the
        // same order, so the containers-list gate cannot see it) and the key of CSharpQuests.legacy (!reloadquest)
        Check("abyssea g4:each op's LegacyPath is its own Lua file",
              "Altepa_Dominion_Op_01 ; Altepa_Dominion_Op_02 ; Altepa_Dominion_Op_03 ; Altepa_Dominion_Op_04 ; " +
              "Altepa_Dominion_Op_05 ; Altepa_Dominion_Op_06 ; Altepa_Dominion_Op_07 ; Altepa_Dominion_Op_08 ; " +
              "Altepa_Dominion_Op_09 ; Altepa_Dominion_Op_10 ; Altepa_Dominion_Op_11 ; Altepa_Dominion_Op_12 ; " +
              "Altepa_Dominion_Op_13 ; Altepa_Dominion_Op_14 ; Grauberg_Dominion_Op_01 ; Grauberg_Dominion_Op_02 ; " +
              "Grauberg_Dominion_Op_03 ; Grauberg_Dominion_Op_04 ; Grauberg_Dominion_Op_05 ; " +
              "Grauberg_Dominion_Op_06 ; Grauberg_Dominion_Op_07 ; Grauberg_Dominion_Op_08 ; " +
              "Grauberg_Dominion_Op_09 ; Grauberg_Dominion_Op_10 ; Grauberg_Dominion_Op_11 ; " +
              "Grauberg_Dominion_Op_12 ; Grauberg_Dominion_Op_13 ; Grauberg_Dominion_Op_14",
              string.Join(" ; ", ops.Select(o => o.Quest.LegacyPath.Substring("scripts/quests/abyssea/".Length))));
    }
}
