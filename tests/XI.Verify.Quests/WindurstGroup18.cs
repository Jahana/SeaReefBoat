// XI.Verify.Quests -- quest rework B3b Windurst group 18 (call-only: Tuning In and Tuning Out at Leepe-Hoppe). The golden
// flows take every arm. These checks pin what no flow can see:
// - the replies' kinds. In an NPC call the bridge renders an Event, a ProgressEvent and a PriorityEvent alike, as
//   player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply: no action is built, so no priority), which is what the
//   Lua script calls; the fake keeps them apart;
// - Tuning In's offer condition, operand for operand (mutants I054, I056: The Last Verse operand is always false in
//   game, since the CoP log's current mission never passes 850);
// - Tuning Out's offer condition (mutant O084: its Tuning In completed operand, shadowed in game by Tuning In's 885).
// W15Handlers and W15Reply are in WindurstGroup15.cs.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup18()
    {
        Console.WriteLine("== quest rework B3b Windurst group 18: the reply kinds (call-only) ==");
        var tuningIn = new QC.Windurst.TuningIn().Compile();
        var tuningOut = new QC.Windurst.TuningOut().Compile();

        Check("windurst g18:Tuning In clean", "", string.Join(" | ", QuestScriptValidator.Validate(tuningIn)));
        Check("windurst g18:Tuning Out clean", "", string.Join(" | ", QuestScriptValidator.Validate(tuningOut)));

        // Leepe-Hoppe's trigger replies in declaration order: Tuning In's offer and reminder, Tuning Out's offer, 897, 889
        Check("windurst g18:the trigger replies are the Lua's startEvent ids, as Event",
              "Leepe-Hoppe:Event(884,0,1696,1697,1698),Leepe-Hoppe:Event(885,0,1696,1697,1698)" +
              "|Leepe-Hoppe:Event(888),Leepe-Hoppe:Event(897),Leepe-Hoppe:Event(889)",
              string.Join(",", W15Handlers(tuningIn, QuestHandlerKind.OnTrigger).Select(h =>
                  $"{h.Entity}:{W15Reply(h, QuestStatus.Accepted, 1).Reply}")) + "|" +
              string.Join(",", W15Handlers(tuningOut, QuestHandlerKind.OnTrigger).Select(h =>
                  $"{h.Entity}:{W15Reply(h, QuestStatus.Accepted, 1).Reply}")));

        // his trade, Tuning In accepted: exactly the three items
        Check("windurst g18:Leepe-Hoppe's trade: the three items, as Event",
              "Leepe-Hoppe:Event(886)",
              string.Join(",", W15Handlers(tuningIn, QuestHandlerKind.OnTrade).Select(h =>
                  $"{h.Entity}:{W15Reply(h, QuestStatus.Accepted, 1, (QC.Item.MagickedSteelIngot, 1),
                      (QC.Item.PieceOfSpruceLumber, 1), (QC.Item.ExtraFineFile, 1)).Reply}")));

        // Tuning In's offer condition, the Lua's operands one for one: a 1 per case it matches, the cases Windurst fame
        // level 3 / 4 x the current CoP mission The Lost City / Distant Beliefs / An Eternal Melody x The Last Verse
        // completed no / yes. In game the CoP log's hasCompletedMission(THE_LAST_VERSE) is current > 850, so the
        // getCurrentMission >= DISTANT_BELIEFS operand already holds whenever it does and no flow can see it (mutant round
        // 1 survivors I054, I056); the fake keeps the two apart
        var offer = tuningIn.States.First(s => s.Handlers.Any(h => h.Kind == QuestHandlerKind.OnTrigger));
        var offerCases = from fame in new[] { 3, 4 }
                         from cur in new[] { QC.Mission.Cop.TheLostCity, QC.Mission.Cop.DistantBeliefs, QC.Mission.Cop.AnEternalMelody }
                         from lastVerse in new[] { false, true }
                         select (Fame: fame, Current: cur, LastVerse: lastVerse);
        Check("windurst g18:Tuning In's offer condition (fame level 3 / 4 x CoP 218 / 228 / 238 x The Last Verse completed)",
              "000000011111",
              string.Concat(offerCases.Select(c =>
              {
                  var f = new Fake { Status = QuestStatus.Available };
                  f.Fame[QC.FameArea.Windurst] = c.Fame;
                  f.CurrentMissions[QC.MissionLog.Cop] = c.Current;
                  if (c.LastVerse) f.CompletedMissions.Add($"{QC.MissionLog.Cop}/{QC.Mission.Cop.TheLastVerse}");
                  return offer.IsMatch(QuestStatus.Available, 0, f) ? '1' : '0';
              })));

        // Tuning Out's offer condition: Tuning In available / accepted / completed. While Tuning In is accepted its
        // reminder 885 answers first, so no flow sees a test that lets accepted through (mutant round 1 survivor O084)
        var outOffer = tuningOut.States.First(s => s.Handlers.Any(h => h.Kind == QuestHandlerKind.OnTrigger));
        Check("windurst g18:Tuning Out's offer condition (Tuning In available / accepted / completed)",
              "001",
              string.Concat(new[] { QuestStatus.Available, QuestStatus.Accepted, QuestStatus.Completed }.Select(s =>
              {
                  var f = new Fake { Status = QuestStatus.Available };
                  f.QuestStatuses[$"{QuestLog.Windurst}/TUNING_IN"] = s;
                  return outOffer.IsMatch(QuestStatus.Available, 0, f) ? '1' : '0';
              })));
    }
}
