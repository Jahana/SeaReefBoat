// XI.Verify.Quests -- quest rework B3b Other Areas group 12 (call-only: Fly High, In the Name of Science): what the
// golden traces cannot show.
// - The reply kinds. Both quests are called from their NPCs' scripts (xi.cs.quest), and an NPC call renders an Event, a
//   ProgressEvent and a PriorityEvent alike, as player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply): there is no
//   lookup action, so no priority to tell them apart, and every trace replays the same with any of the three. The Lua
//   scripts call player:startEvent, so each reply is pinned here as Event.
// - The arms that answer nothing. Ferchinne's and Yurim's scripts are the calls alone and ignore their answers, so a
//   NoAction in place of null renders nothing either and the DefaultActions lookup plays the default (240 / 527) all
//   the same: each "nothing" arm is pinned here as null.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunOtherAreasGroup12()
    {
        Console.WriteLine("== quest rework B3b Other Areas group 12: the call-mode reply kinds, the arms that answer " +
                          "nothing ==");
        OA12FlyHigh();
        OA12InTheNameOfScience();
        OA12InvalidTarget();
    }

    static void OA12FlyHigh()
    {
        var def = new QC.OtherAreas.FlyHigh().Compile();
        Fake Talk(QuestStatus status, int current)
        {
            var f = new Fake { Status = status };
            f.CurrentMissions[QC.MissionLog.Cop] = current;
            return f;
        }
        int savage = QC.Mission.Cop.TheSavage, before = QC.Mission.Cop.ShelteringDoubt;
        var trig = QuestHandlerKind.OnTrigger;
        var trade = QuestHandlerKind.OnTrade;
        int feather = QC.Item.HippogryphTailfeather;
        Check("oa g12:Fly High's replies are events (call mode): 241 / 242 / 244; the trade's 243 / 245; null for " +
              "the arms that answer nothing (no mission arm; a trade while available; one feather)",
              "Event(241)|Event(242)|Event(244)|Event(243)|Event(245)|null|null|null",
              string.Join("|",
                  OA10Reply(def, trig, Talk(QuestStatus.Available, savage)),
                  OA10Reply(def, trig, Talk(QuestStatus.Accepted, savage)),
                  OA10Reply(def, trig, Talk(QuestStatus.Completed, savage)),
                  OA10Reply(def, trade, OA10Traded(QuestStatus.Accepted, (feather, 2))),
                  OA10Reply(def, trade, OA10Traded(QuestStatus.Completed, (feather, 2))),
                  OA10Reply(def, trig, Talk(QuestStatus.Accepted, before)),
                  OA10Reply(def, trade, OA10Traded(QuestStatus.Available, (feather, 2))),
                  OA10Reply(def, trade, OA10Traded(QuestStatus.Accepted, (feather, 1)))));

        // The engine reads a CoP mission completed when it is below the current one, and no CoP mission lies between
        // Sheltering Doubt (368) and The Savage (418), so no trace tells hasCompletedMission(The Savage) from
        // hasCompletedMission(Sheltering Doubt). The fake keeps the completed missions as a set: The Savage completed
        // with no current mission opens the arms, Sheltering Doubt completed does not.
        Fake Done(int mission)
        {
            var f = new Fake { Status = QuestStatus.Available };
            f.CompletedMissions.Add($"{QC.MissionLog.Cop}/{mission}");
            return f;
        }
        Check("oa g12:Fly High's completed operand is The Savage: 241 with The Savage completed (no current " +
              "mission); null with Sheltering Doubt completed",
              "Event(241)|null",
              string.Join("|", OA10Reply(def, trig, Done(savage)), OA10Reply(def, trig, Done(before))));
    }

    static void OA12InTheNameOfScience()
    {
        var def = new QC.OtherAreas.InTheNameOfScience().Compile();
        const string target = "NAME_OF_SCIENCE_target";
        Fake Talk(QuestStatus status, int item, bool pathDone = true, int roll = 0)
        {
            var f = new Fake { Status = status };
            if (pathDone) f.CompletedMissions.Add($"{QC.MissionLog.Cop}/{QC.Mission.Cop.TheWarriorsPath}");
            f.CharVars[target] = item;
            if (roll > 0) f.Rolls.Enqueue(roll);
            return f;
        }
        Fake Traded(QuestStatus status, int item, params (int Item, int Qty)[] slots)
        {
            var f = OA10Traded(status, slots);
            f.CharVars[target] = item;
            return f;
        }
        var trig = QuestHandlerKind.OnTrigger;
        var trade = QuestHandlerKind.OnTrade;
        var acc = QuestStatus.Accepted;
        int karin = QC.Item.KarinObi, obi = QC.Item.SilverObi, red = QC.Item.RedChip, pie = QC.Item.ApplePie;
        string offer = $"{obi},{QC.Item.SilverEarring},{QC.Item.Gorget}";
        Check("oa g12:In the Name of Science's replies are events (call mode): 524 / 525 / 532 (a draw of 30) / 528 " +
              "(31); the trade's 529 / 531 / 526; null for the arms that answer nothing (The Warriors' Path not " +
              "completed; a base while available; a pie with no target and no base)",
              $"Event(524,{offer})|Event(525,{offer})|Event(532,{obi},{red})|Event(528,{obi},{red})|" +
              $"Event(529,{QC.Item.Gorget},{QC.Item.SilverEarring},{obi})|Event(531,{pie},0,1)|" +
              $"Event(526,{obi},{red})|null|null|null",
              string.Join("|",
                  OA10Reply(def, trig, Talk(QuestStatus.Available, 0)),
                  OA10Reply(def, trig, Talk(acc, 0)),
                  OA10Reply(def, trig, Talk(acc, karin, roll: 30)),
                  OA10Reply(def, trig, Talk(acc, karin, roll: 31)),
                  OA10Reply(def, trade, Traded(acc, karin, (QC.Item.PhuaboOrgan, 7), (QC.Item.XzomitOrgan, 3),
                                               (QC.Item.SampleOfLuminianTissue, 3))),
                  OA10Reply(def, trade, Traded(acc, karin, (pie, 1))),
                  OA10Reply(def, trade, Traded(acc, 0, (obi, 1), (red, 1))),
                  OA10Reply(def, trig, Talk(acc, karin, pathDone: false, roll: 30)),
                  OA10Reply(def, trade, Traded(QuestStatus.Available, 0, (obi, 1), (red, 1))),
                  OA10Reply(def, trade, Traded(acc, 0, (pie, 1)))));
    }

    // W1 (task-12-report.md): a target var naming no nosTrades entry (only a GM can set one: 99999). Yurim.lua indexes
    // nosTrades[target] (nil) and raises: on the trade at the organ arm's test (before any other read), on the trigger
    // after its math.randomInt draw. The golden recorder refuses a trace with a script error, so no trace shows it, nor
    // that the bridge's handling of a C# throw (logged, the script returns) matches the Lua raise before the
    // DefaultActions 527. Pinned here: the C# throws on the same reads, the trigger after one RandomInt(1, 100), and a
    // quiet fallback (a TryGetValue, a ContainsKey test) would fail this check.
    static void OA12InvalidTarget()
    {
        var def = new QC.OtherAreas.InTheNameOfScience().Compile();
        string Run(QuestHandlerKind kind, Fake f)
        {
            try
            {
                return OA10Reply(def, kind, f);
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                return "throws:" + string.Join(";", f.Calls.Where(c => c.StartsWith("Player.RandomInt")));
            }
        }
        Fake Bad(QuestStatus status, params (int Item, int Qty)[] slots)
        {
            var f = OA10Traded(status, slots);
            f.CompletedMissions.Add($"{QC.MissionLog.Cop}/{QC.Mission.Cop.TheWarriorsPath}");
            f.CharVars["NAME_OF_SCIENCE_target"] = 99999;
            f.Rolls.Enqueue(30);
            return f;
        }
        Check("oa g12:In the Name of Science, a target naming no entry (99999, a GM's) throws as the Lua raises: the " +
              "trade (a pie; an empty trade; available) at the organ arm; the trigger after one draw (accepted, " +
              "completed); available answers 524 (no table read)",
              "throws:|throws:|throws:|throws:Player.RandomInt(1,100)|throws:Player.RandomInt(1,100)|" +
              $"Event(524,{QC.Item.SilverObi},{QC.Item.SilverEarring},{QC.Item.Gorget})",
              string.Join("|",
                  Run(QuestHandlerKind.OnTrade, Bad(QuestStatus.Accepted, (QC.Item.ApplePie, 1))),
                  Run(QuestHandlerKind.OnTrade, Bad(QuestStatus.Accepted)),
                  Run(QuestHandlerKind.OnTrade, Bad(QuestStatus.Available, (QC.Item.SilverObi, 1))),
                  Run(QuestHandlerKind.OnTrigger, Bad(QuestStatus.Accepted)),
                  Run(QuestHandlerKind.OnTrigger, Bad(QuestStatus.Completed)),
                  Run(QuestHandlerKind.OnTrigger, Bad(QuestStatus.Available))));
    }
}
