// XI.Verify.Quests -- Jeuno final-review fix I1: the level compares of the Jeuno ports read the setting as the Lua number
// (Settings.MainNumber), not the rounded Main. A setting of 40.4 with a level 40 char fails the Lua `>=`; Main would round
// it to 40 and pass.
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using XI.QuestContent;

internal static partial class Program
{
    static void RunJenoLevelGate()
    {
        System.Console.WriteLine("== Jeuno: level compares read the fractional setting ==");

        // one quest per setting; the first state is the Available state of each, its gate holds the compare
        // ADVANCED_JOB_LEVEL: The Old Monument (the compare is the whole gate)
        var om = new XI.QuestContent.Jeuno.TheOldMonument().Compile();
        var f1 = new Fake { MainLevel = 40 };
        f1.MainNumbers["ADVANCED_JOB_LEVEL"] = 40.4;
        string omFrac = om.States[0].IsMatch(QuestStatus.Available, 0, f1).ToString();
        f1.MainNumbers["ADVANCED_JOB_LEVEL"] = 40;
        Check("jg15:ADVANCED_JOB_LEVEL 40.4 fails a level 40 char (The Old Monument), 40 passes", "False/True",
              $"{omFrac}/{om.States[0].IsMatch(QuestStatus.Available, 0, f1)}");

        // AF1_QUEST_LEVEL: The Unfinished Waltz (a Dancer; the compare is the last term)
        var uw = new XI.QuestContent.Jeuno.TheUnfinishedWaltz().Compile();
        var f2 = new Fake { MainLevel = 40, MainJob = Job.Dnc };
        f2.MainNumbers["AF1_QUEST_LEVEL"] = 40.4;
        string uwFrac = uw.States[0].IsMatch(QuestStatus.Available, 0, f2).ToString();
        f2.MainNumbers["AF1_QUEST_LEVEL"] = 40;
        Check("jg15:AF1_QUEST_LEVEL 40.4 fails a level 40 Dancer (The Unfinished Waltz), 40 passes", "False/True",
              $"{uwFrac}/{uw.States[0].IsMatch(QuestStatus.Available, 0, f2)}");

        // AF2_QUEST_LEVEL: The Requiem (Painful Memory completed, a bard)
        var rq = new XI.QuestContent.Jeuno.TheRequiem().Compile();
        var f3 = new Fake { MainLevel = 40, MainJob = Job.Brd };
        f3.CompletedQuests.Add("Jeuno/PAINFUL_MEMORY");
        f3.MainNumbers["AF2_QUEST_LEVEL"] = 40.4;
        string rqFrac = rq.States[0].IsMatch(QuestStatus.Available, 0, f3).ToString();
        f3.MainNumbers["AF2_QUEST_LEVEL"] = 40;
        Check("jg15:AF2_QUEST_LEVEL 40.4 fails a level 40 bard (The Requiem), 40 passes", "False/True",
              $"{rqFrac}/{rq.States[0].IsMatch(QuestStatus.Available, 0, f3)}");

        // AF3_QUEST_LEVEL: Comeback of the Queen (a Dancer, Road to Divadom completed, no mustZone, timer passed)
        var cq = new XI.QuestContent.Jeuno.ComebackQueen().Compile();
        var f4 = new Fake { MainLevel = 40, MainJob = Job.Dnc };
        f4.CompletedQuests.Add("Jeuno/THE_ROAD_TO_DIVADOM");
        f4.MainNumbers["AF3_QUEST_LEVEL"] = 40.4;
        string cqFrac = cq.States[0].IsMatch(QuestStatus.Available, 0, f4).ToString();
        f4.MainNumbers["AF3_QUEST_LEVEL"] = 40;
        Check("jg15:AF3_QUEST_LEVEL 40.4 fails a level 40 Dancer (Comeback of the Queen), 40 passes", "False/True",
              $"{cqFrac}/{cq.States[0].IsMatch(QuestStatus.Available, 0, f4)}");
    }
}
