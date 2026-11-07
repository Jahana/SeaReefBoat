// XI.Verify.Quests -- quest rework B3b Abyssea group 1 (A Journey Begins .. The Forbidden Frontier): each definition
// validates clean and declares its steps (A Journey Begins, The Truth Beckons and Tenuous Existence: step 1; the rest
// none). Their branches are the flows' (tests/golden/flows/abyssea.*.flow), except what no trace can see:
// - First Contact's only section and Tenuous Existence's first: their check is the hour from 18:00 to 4:59
//   (user ruling 2026-11-04, kept Lua bugs: the Lua's VanadielHour() >= 18 and VanadielHour() < 5 was never
//   true). The check is pinned here on the fake at each side of both bounds; the flows show 12:00, 20:00 and 04:00.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunAbysseaGroup1()
    {
        Console.WriteLine("== quest rework B3b Abyssea group 1 ==");
        void One(string name, QuestScript quest, string steps)
        {
            Check($"abyssea g1:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(quest.Compile())));
            Check($"abyssea g1:{name} declares steps [{steps}]", steps,
                  string.Join(",", quest.Compile().DeclaredSteps));
        }
        One("A Journey Begins", new QC.Abyssea.AJourneyBegins(), "1");
        One("The Truth Beckons", new QC.Abyssea.TheTruthBeckons(), "1");
        One("Dawn of Death", new QC.Abyssea.DawnOfDeath(), "");
        One("First Contact", new QC.Abyssea.FirstContact(), "");
        One("An Officer and a Pirate", new QC.Abyssea.AnOfficerAndAPirate(), "");
        One("Heart of Madness", new QC.Abyssea.HeartOfMadness(), "");
        One("Tenuous Existence", new QC.Abyssea.TenuousExistence(), "1");
        One("Champions of Abyssea", new QC.Abyssea.ChampionsOfAbyssea(), "");
        One("Heroes of Abyssea", new QC.Abyssea.HeroesOfAbyssea(), "");
        One("Scars of Abyssea", new QC.Abyssea.ScarsOfAbyssea(), "");
        One("The Forbidden Frontier", new QC.Abyssea.TheForbiddenFrontier(), "");

        RunAbysseaGroup1HourSections();
    }

    // the hour sections (First_Contact.lua:17-36, Tenuous_Existence.lua:18-34): qm3's reply and the finish, each with
    // its zone, on the fake (accepted, Prog 0); the finish's calls with Complete() true; the check at 4, 5, 17 and 18
    static void RunAbysseaGroup1HourSections()
    {
        string Section(QuestScriptDefinition d)
        {
            var hs = d.States[0].Handlers;
            var trig = hs.Single(h => h.Kind == QuestHandlerKind.OnTrigger);
            var fin = hs.Single(h => h.Kind == QuestHandlerKind.OnEventFinish);
            var f = new Fake { Status = QuestStatus.Accepted, Step = 0 };
            var r = trig.Fixed ?? trig.Reply(f);
            var g = new Fake { Status = QuestStatus.Accepted, Step = 0 };
            fin.Run(g);
            return $"{trig.Zone}/{trig.Entity}:{(r == null ? "null" : QuestContextFake.Describe(r))} " +
                   $"{fin.Zone}/{fin.Id}:{string.Join(";", g.Calls)}";
        }
        ushort lt = QC.Zone.LaTheinePlateau;
        string qm3 = QC.Npc.LaTheinePlateau.qm3;
        Check("abyssea g1:First Contact's section: qm3 at La Theine 11; its finish completes the " +
              "quest and adds An Officer and a Pirate",
              $"{lt}/{qm3}:ProgressEvent(11) {lt}/11:Quest.Complete();Player.AddQuest(Abyssea,AN_OFFICER_AND_A_PIRATE)",
              Section(new QC.Abyssea.FirstContact().Compile()));
        Check("abyssea g1:Tenuous Existence's first section: qm3 at La Theine 12; its finish sets Prog 1",
              $"{lt}/{qm3}:ProgressEvent(12) {lt}/12:Quest.SetStep(1)",
              Section(new QC.Abyssea.TenuousExistence().Compile()));

        string Hours(QuestScriptDefinition d) => string.Join(",", new[] { 4, 5, 17, 18 }.Select(h =>
            d.States[0].Matches.Any(m => m.When(new Fake { Status = QuestStatus.Accepted, Hour = h }))
                ? h + ":y" : h + ":n"));
        Check("abyssea g1:First Contact's section holds from 18:00 to 4:59 (user ruling 2026-11-04: `or`, not the " +
              "Lua's `and`)", "4:y,5:n,17:n,18:y", Hours(new QC.Abyssea.FirstContact().Compile()));
        Check("abyssea g1:Tenuous Existence's first section holds from 18:00 to 4:59 (user ruling 2026-11-04)",
              "4:y,5:n,17:n,18:y", Hours(new QC.Abyssea.TenuousExistence().Compile()));
    }
}
