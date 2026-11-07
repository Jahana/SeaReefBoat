// XI.Verify.Quests -- quest rework B3b hidden quests group 2 (the four crafted artifact quests): each definition
// validates clean; what the traces and flows do not pin (the mutants that survive every flow of the group).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunHiddenGroup2()
    {
        Console.WriteLine(
            "== quest rework B3b hidden quests group 2: the crafted artifacts; what the flows do not pin ==");
        var quests = new (string Name, QuestScript Script)[]
        {
            ("Corsair", new QC.HiddenQuests.CraftedCorsairArtifact()),
            ("Dancer", new QC.HiddenQuests.CraftedDancerArtifact()),
            ("Magus", new QC.HiddenQuests.CraftedMagusArtifact()),
            ("Puppetry", new QC.HiddenQuests.CraftedPuppetryArtifact()),
        };
        foreach (var (name, script) in quests)
            Check($"hidden g2:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(script.Compile())));

        // The declared steps (a mutant of one is invisible to every trace: the validator reads them).
        Check("hidden g2:Corsair declares steps 0..4", "0,1,2,3,4",
              string.Join(",", quests[0].Script.Compile().DeclaredSteps));
        Check("hidden g2:Dancer declares steps 2..5", "2,3,4,5",
              string.Join(",", quests[1].Script.Compile().DeclaredSteps));
        RunHiddenGroup2Dancer(quests[1].Script.Compile());
    }

    // Dancer: what the play cannot reach. The Status mask holds bits 0..2 only (Olgald's 10167 finish writes 7, the
    // finishes clear one bit), so a bit 3 exists only in a GM's state: a flow can set it with a charvar step, and the
    // fake check below keeps the three-bit window of CountBits pinned for that GM-only state. Matthias at Prog 0 or 1
    // answers nothing whatever the Status (the Status is only set with Prog 2 or more); the check calls the handler
    // directly, so it documents the reason but does not kill dnc19 / dnc20 (Step > 0 -> >= 0 / > 1), which are
    // equivalent: only a GM's Prog 0 or 1 with Status > 0 tells them apart, and section 3 / the default answer there.
    static void RunHiddenGroup2Dancer(QuestScriptDefinition dnc)
    {
        var handlers = dnc.States.SelectMany(s => s.Handlers)
            .Where(x => x.Entity == QC.Npc.BastokMarkets.Matthias || x.Id == 497).ToList();
        var trigger = handlers.First(
            x => x.Kind == QuestHandlerKind.OnTrigger && x.Entity == QC.Npc.BastokMarkets.Matthias);
        var finish = handlers.First(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Id == 497);
        string Talk(int prog, int status)
        {
            var f = new Fake { MainJob = QC.Job.Dnc, UniqueDay = 5 };
            f.Vars["Prog"] = prog; f.Vars["Status"] = status; f.Vars["Option"] = 1; f.Vars["Timer"] = 5;
            return AU1Describe(trigger, f);
        }
        Check("hidden g2:Dancer's Matthias at Prog 0 and 1 answers nothing", "null|null|null|null",
              $"{Talk(0, 3)}|{Talk(1, 3)}|{Talk(0, 7)}|{Talk(1, 7)}");
        Check("hidden g2:Dancer's last-artifact flag counts three bits (a bit 3 does not count)",
              Talk(5, 1), Talk(5, 9));
        Check("hidden g2:Dancer's last-artifact flag is 1 for one bit and 0 for two",
              "True|False", $"{Talk(5, 1).EndsWith(",1)")}|{Talk(5, 3).EndsWith(",1)")}");
        string Finish(int status)
        {
            var f = new Fake { MainJob = QC.Job.Dnc };
            f.Vars["Prog"] = 5; f.Vars["Status"] = status; f.Vars["Option"] = 1;
            finish.Run(f);
            return string.Join(";", f.Calls);
        }
        Check("hidden g2:Dancer's finish completes on one bit, bit 3 not counted", "True|True|False",
              $"{Finish(1).Contains("Quest.Complete")}|{Finish(9).Contains("Quest.Complete")}|" +
              $"{Finish(3).Contains("Quest.Complete")}");
    }
}
