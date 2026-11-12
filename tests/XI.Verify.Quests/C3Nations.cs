// XI.Verify.Quests -- quest rework C3 N1 (docs/superpowers/plans/2026-11-07-quest-rework-c3-lanes-1.md, section 3,
// Nations): the Bastok missions on C#, one fake check per arm no packet shows, as the Lua files had them: each section
// check on its operands, each reply on the reads it branches on, each finish on its option and the items it reads.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    internal static void RunC3Nations()
    {
        Console.WriteLine("\n== c3 N1: the Nations' missions on C# ==");
        RunC3N1GeologicalSurvey();
        RunC3N1Fetichism();
        RunC3N1TheCrystalLine();
        RunC3N1WadingBeasts();
        RunC3N1SmashTheOrcishScouts();
        RunC3N1BatHunt();
        RunC3N1TheRescueDrill();
        RunC3N1TheDavoiReport();
        RunC3N1TheHorutotoRuinsExperiment();
        RunC3N1TheHeartOfTheMatter();
        RunC3N1ThePriceOfPeace();
        RunC3N1LostForWords();
        RunC3N1ATestingTime();
        RunC3N2();
        RunC3N3();
        RunC3N4();
        RunC3N5();
        RunC3N6();
    }

    // C2MReply, but a handler that reads a text id the fake was not given (a mutant that names another zone's
    // text) answers "threw: ..." and fails its check, rather than ending the run with no FAIL line
    static string C3NReply(QuestHandlerDecl h, Fake f)
    {
        try
        {
            return C2MReply(h, f);
        }
        catch (Exception e)
        {
            return "threw: " + e.Message;
        }
    }

    static void RunC3N1GeologicalSurvey()
    {
        var m = new QC.Missions.Bastok.GeologicalSurvey();
        var d = m.Compile();
        int bastok = QC.MissionLog.Bastok, id = QC.Mission.Bastok.GeologicalSurvey;
        const int blue = QC.KeyItem.BlueAcidityTester, red = QC.KeyItem.RedAcidityTester;
        Check("c3 n1 geological survey: clean, its key and LegacyPath, no reward table",
              $"|{QC.MissionKey.Bastok.GeologicalSurvey}|scripts/missions/bastok/1_2_A_Geological_Survey|True",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.Reward == null}");
        Fake F(int current, int nation = 1, bool hasBlue = false, bool hasRed = false, int option = 0)
        {
            var f = new Fake { ThisMission = (MissionLog.Bastok, id), NationId = nation, Option = option };
            f.CurrentMissions[bastok] = current;
            if (hasBlue) f.KeyItems[blue] = true;
            if (hasRed) f.KeyItems[red] = true;
            return f;
        }
        const int none = QC.Mission.Bastok.None;
        Check("c3 n1 geological survey: section 1 holds with no current mission and a Bastokan (no completed test: " +
              "1-1 is not needed); fails for a San d'Orian and for a current mission (1-1, 1-2)",
              "True False False False",
              $"{d.States[0].IsMissionMatch(F(none))} {d.States[0].IsMissionMatch(F(none, nation: 0))} " +
              $"{d.States[0].IsMissionMatch(F(QC.Mission.Bastok.TheZeruhnReport))} " +
              $"{d.States[0].IsMissionMatch(F(id))}");
        Check("c3 n1 geological survey: section 2 holds while 1-2 is current only", "True False",
              $"{d.States[1].IsMissionMatch(F(id))} {d.States[1].IsMissionMatch(F(none))}");

        var fin = QuestHandlerKind.OnEventFinish;
        foreach (var (zone, label, text) in new[]
                 {
                     (QC.Zone.BastokMarkets, "Markets", QC.Text.BastokMarkets.YOU_ACCEPT_THE_MISSION),
                     (QC.Zone.BastokMines, "Mines", QC.Text.BastokMines.YOU_ACCEPT_THE_MISSION),
                     (QC.Zone.Metalworks, "Metalworks", QC.Text.Metalworks.YOU_ACCEPT_THE_MISSION),
                     (QC.Zone.PortBastok, "Port", QC.Text.PortBastok.YOU_ACCEPT_THE_MISSION),
                 })
        {
            var h = C2MHandler(d, 0, fin, zone, id: 1001);
            Check($"c3 n1 geological survey: {label} 1001's finish: option 1 begins with its zone's " +
                  "YOU_ACCEPT_THE_MISSION; option 0 does nothing",
                  $"Mission.Begin(),Text.Special({text})|", $"{C2MRun(h, F(none, option: 1))}|{C2MRun(h, F(none))}");
        }

        var trig = QuestHandlerKind.OnTrigger;
        var cid = C2MHandler(d, 1, trig, QC.Zone.Metalworks, QC.Npc.Metalworks.Cid);
        Check("c3 n1 geological survey: Cid: the Red tester: 504 (progress); no Blue tester: 503 (progress); the " +
              "Blue one: 502 (event); both held: 504",
              "ProgressEvent(504)|ProgressEvent(503)|Event(502)|ProgressEvent(504)",
              $"{C2MReply(cid, F(id, hasRed: true))}|{C2MReply(cid, F(id))}|{C2MReply(cid, F(id, hasBlue: true))}|" +
              $"{C2MReply(cid, F(id, hasBlue: true, hasRed: true))}");
        Check("c3 n1 geological survey: 503's finish gives the Blue tester", $"Player.GiveKeyItem({blue})",
              C2MRun(C2MHandler(d, 1, fin, QC.Zone.Metalworks, id: 503), F(id)));
        var f504 = F(id, hasRed: true);
        string done504 = C2MRun(C2MHandler(d, 1, fin, QC.Zone.Metalworks, id: 504), f504);
        var failed = F(id, hasRed: true);
        failed.CompleteResult = false;
        Check("c3 n1 geological survey: 504's finish: completed, then the Red tester taken; complete false: the " +
              "tester kept",
              $"Mission.Complete(),Player.DelKeyItem({red}) True False|Mission.Complete() True",
              $"{done504} {f504.HasCompletedMission(bastok, id)} {f504.HasKeyItem(red)}|" +
              $"{C2MRun(C2MHandler(d, 1, fin, QC.Zone.Metalworks, id: 504), failed)} {failed.HasKeyItem(red)}");
        foreach (int csid in new[] { 10, 11, 12 })
        {
            var h = C2MHandler(d, 1, fin, QC.Zone.DangrufWadi, id: csid);
            var withBlue = F(id, hasBlue: true);
            string ran = C2MRun(h, withBlue);
            Check($"c3 n1 geological survey: Dangruf Wadi {csid}'s finish: the Blue tester turns into the Red one; " +
                  "without it nothing",
                  $"Player.DelKeyItem({blue}),Player.GiveKeyItem({red}) False True|",
                  $"{ran} {withBlue.HasKeyItem(blue)} {withBlue.HasKeyItem(red)}|{C2MRun(h, F(id, hasRed: true))}");
        }
        Check("c3 n1 geological survey: section 2's four NPCs play their zone's ORIGINAL_MISSION_OFFSET + 3",
              "MessageSpecial(7003) MessageSpecial(8003) MessageSpecial(9003) MessageSpecial(10003)",
              string.Join(" ", new[]
              {
                  (QC.Zone.BastokMarkets, QC.Npc.BastokMarkets.Cleades,
                   QC.Text.BastokMarkets.ORIGINAL_MISSION_OFFSET, 7000),
                  (QC.Zone.BastokMines, QC.Npc.BastokMines.Rashid, QC.Text.BastokMines.ORIGINAL_MISSION_OFFSET, 8000),
                  (QC.Zone.Metalworks, QC.Npc.Metalworks.Malduc, QC.Text.Metalworks.ORIGINAL_MISSION_OFFSET, 9000),
                  (QC.Zone.PortBastok, QC.Npc.PortBastok.Argus, QC.Text.PortBastok.ORIGINAL_MISSION_OFFSET, 10000),
              }.Select(n =>
              {
                  var f = F(id);
                  f.TextIds[n.Item3.ToString()] = n.Item4;
                  return C3NReply(C2MHandler(d, 1, trig, n.Item1, n.Item2), f);
              })));
    }

    static void RunC3N1Fetichism()
    {
        var m = new QC.Missions.Bastok.Fetichism();
        var d = m.Compile();
        int bastok = QC.MissionLog.Bastok, id = QC.Mission.Bastok.Fetichism;
        Check("c3 n1 fetichism: clean, its key and LegacyPath, a reward table",
              $"|{QC.MissionKey.Bastok.Fetichism}|scripts/missions/bastok/1_3_Fetichism|1000/2/200",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|" +
              $"{d.Reward.Gil}/{d.Reward.Rank}/{d.Reward.RankPoints}");
        Fake F(int current, int nation = 1, int option = 0, bool trade = false)
        {
            var f = new Fake { ThisMission = (MissionLog.Bastok, id), NationId = nation, Option = option,
                               TradeResult = trade };
            f.CurrentMissions[bastok] = current;
            return f;
        }
        const int none = QC.Mission.Bastok.None;
        Check("c3 n1 fetichism: section 1 holds with no current mission and a Bastokan; fails for a San d'Orian and " +
              "for a current mission (1-1, 1-3)",
              "True False False False",
              $"{d.States[0].IsMissionMatch(F(none))} {d.States[0].IsMissionMatch(F(none, nation: 0))} " +
              $"{d.States[0].IsMissionMatch(F(QC.Mission.Bastok.TheZeruhnReport))} " +
              $"{d.States[0].IsMissionMatch(F(id))}");
        Check("c3 n1 fetichism: section 2 holds while 1-3 is current only", "True False",
              $"{d.States[1].IsMissionMatch(F(id))} {d.States[1].IsMissionMatch(F(none))}");

        var fin = QuestHandlerKind.OnEventFinish;
        var trig = QuestHandlerKind.OnTrigger;
        var trd = QuestHandlerKind.OnTrade;
        foreach (var (zone, npc, label, text, offset, ofs) in new[]
                 {
                     (QC.Zone.BastokMarkets, QC.Npc.BastokMarkets.Cleades, "Markets",
                      QC.Text.BastokMarkets.YOU_ACCEPT_THE_MISSION,
                      QC.Text.BastokMarkets.ORIGINAL_MISSION_OFFSET, 7000),
                     (QC.Zone.BastokMines, QC.Npc.BastokMines.Rashid, "Mines",
                      QC.Text.BastokMines.YOU_ACCEPT_THE_MISSION, QC.Text.BastokMines.ORIGINAL_MISSION_OFFSET, 8000),
                     (QC.Zone.Metalworks, QC.Npc.Metalworks.Malduc, "Metalworks",
                      QC.Text.Metalworks.YOU_ACCEPT_THE_MISSION, QC.Text.Metalworks.ORIGINAL_MISSION_OFFSET, 9000),
                     (QC.Zone.PortBastok, QC.Npc.PortBastok.Argus, "Port",
                      QC.Text.PortBastok.YOU_ACCEPT_THE_MISSION, QC.Text.PortBastok.ORIGINAL_MISSION_OFFSET, 10000),
                 })
        {
            var h = C2MHandler(d, 0, fin, zone, id: 1001);
            Check($"c3 n1 fetichism: {label} 1001's finish: option 2 begins with its zone's " +
                  "YOU_ACCEPT_THE_MISSION; option 1 and 0 do nothing",
                  $"Mission.Begin(),Text.Special({text})||",
                  $"{C2MRun(h, F(none, option: 2))}|{C2MRun(h, F(none, option: 1))}|{C2MRun(h, F(none))}");
            var f = F(id);
            f.TextIds[offset.ToString()] = ofs;
            Check($"c3 n1 fetichism: {label}: ORIGINAL_MISSION_OFFSET + 6",
                  $"MessageSpecial({ofs + 6})", C3NReply(C2MHandler(d, 1, trig, zone, npc), f));
            var th = C2MHandler(d, 1, trd, zone, npc);
            var first = F(id, trade: true);
            var again = F(id, trade: true);
            again.CompletedMissions.Add($"{bastok}/{id}");
            Check($"c3 n1 fetichism: {label}: the four parts: 1008 the first time, 1005 once completed; no match: " +
                  "no event",
                  "ProgressEvent(1008)|ProgressEvent(1005)|null",
                  $"{C2MReply(th, first)}|{C2MReply(th, again)}|{C2MReply(th, F(id))}");
            foreach (int csid in new[] { 1005, 1008 })
            {
                var ch = C2MHandler(d, 1, fin, zone, id: csid);
                var ok = F(id);
                var failed = F(id);
                failed.CompleteResult = false;
                Check($"c3 n1 fetichism: {label} {csid}'s finish: complete then the parts taken; complete false: " +
                      "the parts kept",
                      "Mission.Complete(),Trade.Complete()|Mission.Complete()",
                      $"{C2MRun(ch, ok)}|{C2MRun(ch, failed)}");
            }
        }
    }

    static void RunC3N1TheCrystalLine()
    {
        var m = new QC.Missions.Bastok.TheCrystalLine();
        var d = m.Compile();
        int bastok = QC.MissionLog.Bastok, id = QC.Mission.Bastok.TheCrystalLine;
        const int report = QC.KeyItem.CLReport;
        Check("c3 n1 crystal line: clean, its key and LegacyPath, 200 rank points, no gil or rank",
              $"|{QC.MissionKey.Bastok.TheCrystalLine}|scripts/missions/bastok/2_1_The_Crystal_Line|//200",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|" +
              $"{d.Reward.Gil}/{d.Reward.Rank}/{d.Reward.RankPoints}");
        Fake F(int current, int nation = 1, int option = 0, bool hasReport = false, int status = 0,
               bool trade = false, bool give = true)
        {
            var f = new Fake { ThisMission = (MissionLog.Bastok, id), NationId = nation, Option = option,
                               TradeResult = trade, GiveResult = give };
            f.CurrentMissions[bastok] = current;
            f.MissionStatuses[bastok] = status;
            if (hasReport) f.KeyItems[report] = true;
            return f;
        }
        const int none = QC.Mission.Bastok.None;
        Check("c3 n1 crystal line: section 1 holds with no current mission and a Bastokan; fails for a San d'Orian " +
              "and for a current mission (1-1, 2-1)",
              "True False False False",
              $"{d.States[0].IsMissionMatch(F(none))} {d.States[0].IsMissionMatch(F(none, nation: 0))} " +
              $"{d.States[0].IsMissionMatch(F(QC.Mission.Bastok.TheZeruhnReport))} " +
              $"{d.States[0].IsMissionMatch(F(id))}");
        Check("c3 n1 crystal line: section 2 holds while 2-1 is current only", "True False",
              $"{d.States[1].IsMissionMatch(F(id))} {d.States[1].IsMissionMatch(F(none))}");

        var fin = QuestHandlerKind.OnEventFinish;
        var trig = QuestHandlerKind.OnTrigger;
        foreach (var (zone, npc, label, text, offset, ofs) in new[]
                 {
                     (QC.Zone.BastokMarkets, QC.Npc.BastokMarkets.Cleades, "Markets",
                      QC.Text.BastokMarkets.YOU_ACCEPT_THE_MISSION,
                      QC.Text.BastokMarkets.ORIGINAL_MISSION_OFFSET, 7000),
                     (QC.Zone.BastokMines, QC.Npc.BastokMines.Rashid, "Mines",
                      QC.Text.BastokMines.YOU_ACCEPT_THE_MISSION, QC.Text.BastokMines.ORIGINAL_MISSION_OFFSET, 8000),
                     (QC.Zone.Metalworks, QC.Npc.Metalworks.Malduc, "Metalworks",
                      QC.Text.Metalworks.YOU_ACCEPT_THE_MISSION, QC.Text.Metalworks.ORIGINAL_MISSION_OFFSET, 9000),
                     (QC.Zone.PortBastok, QC.Npc.PortBastok.Argus, "Port",
                      QC.Text.PortBastok.YOU_ACCEPT_THE_MISSION, QC.Text.PortBastok.ORIGINAL_MISSION_OFFSET, 10000),
                 })
        {
            var h = C2MHandler(d, 0, fin, zone, id: 1001);
            Check($"c3 n1 crystal line: {label} 1001's finish: option 3 begins with its zone's " +
                  "YOU_ACCEPT_THE_MISSION; options 2, 4 and 0 do nothing",
                  $"Mission.Begin(),Text.Special({text})|||",
                  $"{C2MRun(h, F(none, option: 3))}|{C2MRun(h, F(none, option: 2))}|" +
                  $"{C2MRun(h, F(none, option: 4))}|{C2MRun(h, F(none))}");
            var f = F(id);
            f.TextIds[offset.ToString()] = ofs;
            Check($"c3 n1 crystal line: {label}: ORIGINAL_MISSION_OFFSET + 19",
                  $"MessageSpecial({ofs + 19})", C3NReply(C2MHandler(d, 1, trig, zone, npc), f));
        }

        var cid = C2MHandler(d, 1, trig, QC.Zone.Metalworks, QC.Npc.Metalworks.Cid);
        var cidTrade = C2MHandler(d, 1, QuestHandlerKind.OnTrade, QC.Zone.Metalworks, QC.Npc.Metalworks.Cid);
        string dialog = $"MessageText({QC.Text.Metalworks.MISSION_DIALOG_CID_TO_AYAME})";
        Check("c3 n1 crystal line: Cid: the report: MISSION_DIALOG_CID_TO_AYAME (at any status); status 0: 505 " +
              "(progress); status 1: 502 (event); other status: nothing",
              $"{dialog}|{dialog}|ProgressEvent(505)|Event(502)|null",
              $"{C2MReply(cid, F(id, hasReport: true))}|{C2MReply(cid, F(id, hasReport: true, status: 1))}|" +
              $"{C2MReply(cid, F(id))}|{C2MReply(cid, F(id, status: 1))}|{C2MReply(cid, F(id, status: 2))}");
        Check("c3 n1 crystal line: Cid's trade: a faded crystal at status 1: 506; at status 0 or 2, or with no " +
              "match: nothing",
              "ProgressEvent(506)|null|null|null",
              $"{C2MReply(cidTrade, F(id, status: 1, trade: true))}|{C2MReply(cidTrade, F(id, trade: true))}|" +
              $"{C2MReply(cidTrade, F(id, status: 2, trade: true))}|{C2MReply(cidTrade, F(id, status: 1))}");
        foreach (var (npc, csid, label) in new[]
                 {
                     (QC.Npc.Metalworks.Ayame, 712, "Ayame"), (QC.Npc.Metalworks.Naji, 711, "Naji"),
                 })
        {
            var h = C2MHandler(d, 1, trig, QC.Zone.Metalworks, npc);
            Check($"c3 n1 crystal line: {label}: the report: {csid} (progress); without it: nothing",
                  $"ProgressEvent({csid})|null", $"{C2MReply(h, F(id, hasReport: true))}|{C2MReply(h, F(id))}");
        }
        var f505 = F(id);
        f505.Rolls.Enqueue(QC.Item.FireCrystal + 3);
        string rolled = C2MRun(C2MHandler(d, 1, fin, QC.Zone.Metalworks, id: 505), f505);
        var noRoom = F(id, give: false);
        noRoom.Rolls.Enqueue(QC.Item.FireCrystal + 3);
        string full = C2MRun(C2MHandler(d, 1, fin, QC.Zone.Metalworks, id: 505), noRoom);
        string other = C2MRun(C2MHandler(d, 1, fin, QC.Zone.Metalworks, id: 505), F(id, option: 1));
        string roll = $"Player.RandomInt({QC.Item.FireCrystal},{QC.Item.DarkCrystal})," +
                      $"Player.GiveItem({QC.Item.FireCrystal + 3})";
        Check("c3 n1 crystal line: 505's finish: option 0 rolls Fire to Dark, gives it and moves to status 1; " +
              "no room: nothing more; another option: nothing",
              $"{roll},Player.SetMissionStatus({bastok},1) 1|{roll} 0|",
              $"{rolled} {f505.MissionStatus(bastok)}|{full} {noRoom.MissionStatus(bastok)}|{other}");
        Check("c3 n1 crystal line: 506's finish: option 0 takes the crystal and gives the report; another option: " +
              "nothing",
              $"Trade.Complete(),Player.GiveKeyItem({report})|",
              $"{C2MRun(C2MHandler(d, 1, fin, QC.Zone.Metalworks, id: 506), F(id))}|" +
              $"{C2MRun(C2MHandler(d, 1, fin, QC.Zone.Metalworks, id: 506), F(id, option: 1))}");
        var ok712 = F(id, hasReport: true);
        string done712 = C2MRun(C2MHandler(d, 1, fin, QC.Zone.Metalworks, id: 712), ok712);
        var failed = F(id, hasReport: true);
        failed.CompleteResult = false;
        Check("c3 n1 crystal line: 712's finish: completed, then the report taken; complete false: the report kept",
              $"Mission.Complete(),Player.DelKeyItem({report}) True False|Mission.Complete() True",
              $"{done712} {ok712.HasCompletedMission(bastok, id)} {ok712.HasKeyItem(report)}|" +
              $"{C2MRun(C2MHandler(d, 1, fin, QC.Zone.Metalworks, id: 712), failed)} {failed.HasKeyItem(report)}");
    }

    static void RunC3N1WadingBeasts()
    {
        var m = new QC.Missions.Bastok.WadingBeasts();
        var d = m.Compile();
        int bastok = QC.MissionLog.Bastok, id = QC.Mission.Bastok.WadingBeasts;
        Check("c3 n1 wading beasts: clean, its key and LegacyPath, 250 rank points, no gil or rank",
              $"|{QC.MissionKey.Bastok.WadingBeasts}|scripts/missions/bastok/2_2_Wading_Beasts|//250",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|" +
              $"{d.Reward.Gil}/{d.Reward.Rank}/{d.Reward.RankPoints}");
        Fake F(int current, int nation = 1, int option = 0, bool trade = false)
        {
            var f = new Fake { ThisMission = (MissionLog.Bastok, id), NationId = nation, Option = option,
                               TradeResult = trade };
            f.CurrentMissions[bastok] = current;
            return f;
        }
        const int none = QC.Mission.Bastok.None;
        Check("c3 n1 wading beasts: section 1 holds with no current mission and a Bastokan; fails for a San d'Orian " +
              "and for a current mission (1-1, 2-2)",
              "True False False False",
              $"{d.States[0].IsMissionMatch(F(none))} {d.States[0].IsMissionMatch(F(none, nation: 0))} " +
              $"{d.States[0].IsMissionMatch(F(QC.Mission.Bastok.TheZeruhnReport))} " +
              $"{d.States[0].IsMissionMatch(F(id))}");
        Check("c3 n1 wading beasts: section 2 holds while 2-2 is current only", "True False",
              $"{d.States[1].IsMissionMatch(F(id))} {d.States[1].IsMissionMatch(F(none))}");

        var fin = QuestHandlerKind.OnEventFinish;
        var trig = QuestHandlerKind.OnTrigger;
        foreach (var (zone, npc, label, text, offset, ofs) in new[]
                 {
                     (QC.Zone.BastokMarkets, QC.Npc.BastokMarkets.Cleades, "Markets",
                      QC.Text.BastokMarkets.YOU_ACCEPT_THE_MISSION,
                      QC.Text.BastokMarkets.ORIGINAL_MISSION_OFFSET, 7000),
                     (QC.Zone.BastokMines, QC.Npc.BastokMines.Rashid, "Mines",
                      QC.Text.BastokMines.YOU_ACCEPT_THE_MISSION, QC.Text.BastokMines.ORIGINAL_MISSION_OFFSET, 8000),
                     (QC.Zone.Metalworks, QC.Npc.Metalworks.Malduc, "Metalworks",
                      QC.Text.Metalworks.YOU_ACCEPT_THE_MISSION, QC.Text.Metalworks.ORIGINAL_MISSION_OFFSET, 9000),
                     (QC.Zone.PortBastok, QC.Npc.PortBastok.Argus, "Port",
                      QC.Text.PortBastok.YOU_ACCEPT_THE_MISSION, QC.Text.PortBastok.ORIGINAL_MISSION_OFFSET, 10000),
                 })
        {
            var h = C2MHandler(d, 0, fin, zone, id: 1001);
            Check($"c3 n1 wading beasts: {label} 1001's finish: option 4 begins with its zone's " +
                  "YOU_ACCEPT_THE_MISSION; options 3 and 0 do nothing",
                  $"Mission.Begin(),Text.Special({text})||",
                  $"{C2MRun(h, F(none, option: 4))}|{C2MRun(h, F(none, option: 3))}|{C2MRun(h, F(none))}");
            var f = F(id);
            f.TextIds[offset.ToString()] = ofs;
            Check($"c3 n1 wading beasts: {label}: ORIGINAL_MISSION_OFFSET + 21",
                  $"MessageSpecial({ofs + 21})", C3NReply(C2MHandler(d, 1, trig, zone, npc), f));
        }

        var th = C2MHandler(d, 1, QuestHandlerKind.OnTrade, QC.Zone.Metalworks, QC.Npc.Metalworks.Alois);
        var first = F(id, trade: true);
        var again = F(id, trade: true);
        again.CompletedMissions.Add($"{bastok}/{id}");
        Check("c3 n1 wading beasts: Alois: a lizard egg: 372 the first time, 373 once completed; no match: no event",
              "ProgressEvent(372)|ProgressEvent(373)|null",
              $"{C2MReply(th, first)}|{C2MReply(th, again)}|{C2MReply(th, F(id))}");
        foreach (int csid in new[] { 372, 373 })
        {
            var ch = C2MHandler(d, 1, fin, QC.Zone.Metalworks, id: csid);
            var ok = F(id);
            var failed = F(id);
            failed.CompleteResult = false;
            Check($"c3 n1 wading beasts: {csid}'s finish: complete then the egg taken; complete false: the egg kept",
                  "Mission.Complete(),Trade.Complete()|Mission.Complete()",
                  $"{C2MRun(ch, ok)}|{C2MRun(ch, failed)}");
        }
    }

    static void RunC3N1SmashTheOrcishScouts()
    {
        var m = new QC.Missions.Sandoria.SmashTheOrcishScouts();
        var d = m.Compile();
        int log = QC.MissionLog.Sandoria, id = QC.Mission.Sandoria.SmashTheOrcishScouts;
        Check("c3 n1 smash the orcish scouts: clean, its key and LegacyPath, 150 rank points, no gil or rank",
              $"|{QC.MissionKey.Sandoria.SmashTheOrcishScouts}|scripts/missions/sandoria/1_1_Smash_the_Orcish_Scouts|" +
              "//150",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|" +
              $"{d.Reward.Gil}/{d.Reward.Rank}/{d.Reward.RankPoints}");
        Fake F(int current, int nation = 0, bool done = false, int option = 0, bool trade = false)
        {
            var f = new Fake { ThisMission = (MissionLog.Sandoria, id), NationId = nation, Option = option,
                               TradeResult = trade };
            f.CurrentMissions[log] = current;
            if (done) f.CompletedMissions.Add($"{log}/{id}");
            return f;
        }
        const int none = QC.Mission.Sandoria.None;
        int other = QC.Mission.Sandoria.BatHunt;
        Check("c3 n1 smash the orcish scouts: section 1 needs no current mission, a San d'Orian and 1-1 not completed",
              "True False False False False",
              $"{d.States[0].IsMissionMatch(F(none))} {d.States[0].IsMissionMatch(F(none, nation: 1))} " +
              $"{d.States[0].IsMissionMatch(F(other))} {d.States[0].IsMissionMatch(F(id))} " +
              $"{d.States[0].IsMissionMatch(F(none, done: true))}");
        Check("c3 n1 smash the orcish scouts: section 2 needs no current mission, a San d'Orian and 1-1 completed",
              "True False False False False",
              $"{d.States[1].IsMissionMatch(F(none, done: true))} " +
              $"{d.States[1].IsMissionMatch(F(none, nation: 1, done: true))} " +
              $"{d.States[1].IsMissionMatch(F(other, done: true))} {d.States[1].IsMissionMatch(F(id, done: true))} " +
              $"{d.States[1].IsMissionMatch(F(none))}");
        Check("c3 n1 smash the orcish scouts: section 3 holds while 1-1 is current only", "True False",
              $"{d.States[2].IsMissionMatch(F(id))} {d.States[2].IsMissionMatch(F(none))}");

        var fin = QuestHandlerKind.OnEventFinish;
        var trig = QuestHandlerKind.OnTrigger;
        var south = (QC.Zone.SouthernSanDoria, QC.Text.SouthernSanDoria.YOU_ACCEPT_THE_MISSION, "Southern");
        var north = (QC.Zone.NorthernSanDoria, QC.Text.NorthernSanDoria.YOU_ACCEPT_THE_MISSION, "Northern");
        foreach (var (state, zone, text, label, csid) in new[]
                 {
                     (0, south.Item1, south.Item2, south.Item3, 1000), (0, south.Item1, south.Item2, south.Item3, 2000),
                     (0, north.Item1, north.Item2, north.Item3, 1000), (1, south.Item1, south.Item2, south.Item3, 1009),
                     (1, south.Item1, south.Item2, south.Item3, 2009), (1, north.Item1, north.Item2, north.Item3, 1009),
                 })
        {
            var h = C2MHandler(d, state, fin, zone, id: csid);
            Check($"c3 n1 smash the orcish scouts: {label} {csid}'s finish: option 0 begins with its zone's " +
                  "YOU_ACCEPT_THE_MISSION; option 1 does nothing",
                  $"Mission.Begin(),Text.Special({text})|", $"{C2MRun(h, F(none))}|{C2MRun(h, F(none, option: 1))}");
        }

        foreach (var (zone, npc, label, offset, ofs) in new[]
                 {
                     (QC.Zone.SouthernSanDoria, QC.Npc.SouthernSanDoria.Ambrotien, "Ambrotien",
                      QC.Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET, 7000),
                     (QC.Zone.SouthernSanDoria, QC.Npc.SouthernSanDoria.Endracion, "Endracion",
                      QC.Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET, 7000),
                     (QC.Zone.NorthernSanDoria, QC.Npc.NorthernSanDoria.Grilau, "Grilau",
                      QC.Text.NorthernSanDoria.ORIGINAL_MISSION_OFFSET, 8000),
                 })
        {
            var f = F(id);
            f.TextIds[offset.ToString()] = ofs;
            Check($"c3 n1 smash the orcish scouts: {label} plays his zone's ORIGINAL_MISSION_OFFSET",
                  $"MessageText({ofs})", C3NReply(C2MHandler(d, 2, trig, zone, npc), f));
            var th = C2MHandler(d, 2, QuestHandlerKind.OnTrade, zone, npc);
            var first = F(id, trade: true);
            var again = F(id, trade: true, done: true);
            string ev1 = label == "Ambrotien" ? "2020" : "1020", ev2 = label == "Ambrotien" ? "2002" : "1002";
            Check($"c3 n1 smash the orcish scouts: {label}: an Orcish Axe: {ev1} the first time, " +
                  $"{ev2} once completed; no match: no event",
                  $"ProgressEvent({ev1})|ProgressEvent({ev2})|null",
                  $"{C2MReply(th, first)}|{C2MReply(th, again)}|{C2MReply(th, F(id))}");
        }

        foreach (var (zone, csid) in new[]
                 {
                     (QC.Zone.SouthernSanDoria, 1002), (QC.Zone.SouthernSanDoria, 1020),
                     (QC.Zone.SouthernSanDoria, 2002), (QC.Zone.SouthernSanDoria, 2020),
                     (QC.Zone.NorthernSanDoria, 1002), (QC.Zone.NorthernSanDoria, 1020),
                 })
        {
            var ch = C2MHandler(d, 2, fin, zone, id: csid);
            var ok = F(id);
            var failed = F(id);
            failed.CompleteResult = false;
            Check($"c3 n1 smash the orcish scouts: {zone}/{csid}'s finish: complete then the axe taken; complete " +
                  "false: the axe kept",
                  "Mission.Complete(),Trade.Complete()|Mission.Complete()",
                  $"{C2MRun(ch, ok)}|{C2MRun(ch, failed)}");
        }
    }

    static void RunC3N1BatHunt()
    {
        var m = new QC.Missions.Sandoria.BatHunt();
        var d = m.Compile();
        int log = QC.MissionLog.Sandoria, id = QC.Mission.Sandoria.BatHunt;
        Check("c3 n1 bat hunt: clean, its key and LegacyPath, 200 rank points, no gil or rank",
              $"|{QC.MissionKey.Sandoria.BatHunt}|scripts/missions/sandoria/1_2_Bat_Hunt|//200",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|" +
              $"{d.Reward.Gil}/{d.Reward.Rank}/{d.Reward.RankPoints}");
        Fake F(int current, int status = 0, int nation = 0, bool done = false, int option = 0, bool trade = false)
        {
            var f = new Fake { ThisMission = (MissionLog.Sandoria, id), NationId = nation, Option = option,
                               TradeResult = trade };
            f.CurrentMissions[log] = current;
            f.MissionStatuses[log] = status;
            if (done) f.CompletedMissions.Add($"{log}/{id}");
            return f;
        }
        const int none = QC.Mission.Sandoria.None;
        int other = QC.Mission.Sandoria.SmashTheOrcishScouts;
        Check("c3 n1 bat hunt: section 1 needs no current mission and a San d'Orian (no completed check)",
              "True False False False True",
              $"{d.States[0].IsMissionMatch(F(none))} {d.States[0].IsMissionMatch(F(none, nation: 1))} " +
              $"{d.States[0].IsMissionMatch(F(other))} {d.States[0].IsMissionMatch(F(id))} " +
              $"{d.States[0].IsMissionMatch(F(none, done: true))}");
        Check("c3 n1 bat hunt: section 2 holds while 1-2 is current at a status below 2 only",
              "True True False False False",
              $"{d.States[1].IsMissionMatch(F(id))} {d.States[1].IsMissionMatch(F(id, status: 1))} " +
              $"{d.States[1].IsMissionMatch(F(id, status: 2))} {d.States[1].IsMissionMatch(F(id, status: 3))} " +
              $"{d.States[1].IsMissionMatch(F(none))}");
        Check("c3 n1 bat hunt: section 3 holds while 1-2 is current at status 2 only", "True False False False",
              $"{d.States[2].IsMissionMatch(F(id, status: 2))} {d.States[2].IsMissionMatch(F(id, status: 1))} " +
              $"{d.States[2].IsMissionMatch(F(id, status: 3))} {d.States[2].IsMissionMatch(F(none, status: 2))}");

        var fin = QuestHandlerKind.OnEventFinish;
        foreach (var (zone, csid, label) in new[]
                 {
                     (QC.Zone.SouthernSanDoria, 1009, "Southern"), (QC.Zone.SouthernSanDoria, 2009, "Southern"),
                     (QC.Zone.NorthernSanDoria, 1009, "Northern"),
                 })
        {
            var h = C2MHandler(d, 0, fin, zone, id: csid);
            Check($"c3 n1 bat hunt: {label} {csid}'s finish: option 101 begins, with no message; 100 and 0 do nothing",
                  "Mission.Begin()||",
                  $"{C2MRun(h, F(none, option: 101))}|{C2MRun(h, F(none, option: 100))}|{C2MRun(h, F(none))}");
        }

        var trig = QuestHandlerKind.OnTrigger;
        string T(int state, ushort zone, string npc, int status) =>
            C2MReply(C2MHandler(d, state, trig, zone, npc), F(id, status));
        const ushort south = QC.Zone.SouthernSanDoria, north = QC.Zone.NorthernSanDoria;
        Check("c3 n1 bat hunt: section 2: Ambrotien 2021, Endracion 1021, Grilau 1021",
              "ProgressEvent(2021) ProgressEvent(1021) ProgressEvent(1021)",
              $"{T(1, south, QC.Npc.SouthernSanDoria.Ambrotien, 0)} " +
              $"{T(1, south, QC.Npc.SouthernSanDoria.Endracion, 0)} " +
              $"{T(1, north, QC.Npc.NorthernSanDoria.Grilau, 0)}");
        Check("c3 n1 bat hunt: the Tombstone plays progress cutscene 4",
              "ProgressCutscene(4)",
              T(1, QC.Zone.KingRanperresTomb, QC.Npc.KingRanperresTomb.TombstoneUpper, 0));
        var tomb = F(id);
        Check("c3 n1 bat hunt: cutscene 4's finish sets the mission status 2", "Player.SetMissionStatus(0,2) 2",
              $"{C2MRun(C2MHandler(d, 1, fin, QC.Zone.KingRanperresTomb, id: 4), tomb)} {tomb.MissionStatus(log)}");
        Check("c3 n1 bat hunt: section 3: Ambrotien 2022, Endracion 1022, Grilau 1022",
              "ProgressEvent(2022) ProgressEvent(1022) ProgressEvent(1022)",
              $"{T(2, south, QC.Npc.SouthernSanDoria.Ambrotien, 2)} " +
              $"{T(2, south, QC.Npc.SouthernSanDoria.Endracion, 2)} " +
              $"{T(2, north, QC.Npc.NorthernSanDoria.Grilau, 2)}");

        foreach (var (zone, npc, label, firstId, repeatId) in new[]
                 {
                     (QC.Zone.SouthernSanDoria, QC.Npc.SouthernSanDoria.Ambrotien, "Ambrotien", 2023, 2003),
                     (QC.Zone.SouthernSanDoria, QC.Npc.SouthernSanDoria.Endracion, "Endracion", 1023, 1003),
                     (QC.Zone.NorthernSanDoria, QC.Npc.NorthernSanDoria.Grilau, "Grilau", 1023, 1003),
                 })
        {
            var th = C2MHandler(d, 2, QuestHandlerKind.OnTrade, zone, npc);
            // the fake's Trade.Matches answers its TradeResult whatever the items: the item ids themselves are
            // pinned by the replay (a Bat Fang is no first-time trade)
            var first = F(id, 2, trade: true);
            var again = F(id, 2, trade: true, done: true);
            Check($"c3 n1 bat hunt: {label}: a match: {firstId} the first time, {repeatId} once completed; no match: " +
                  "no event",
                  $"ProgressEvent({firstId})|ProgressEvent({repeatId})|null|null",
                  $"{C2MReply(th, first)}|{C2MReply(th, again)}|{C2MReply(th, F(id, 2))}|" +
                  $"{C2MReply(th, F(id, 2, done: true))}");
        }

        foreach (var (zone, csid) in new[]
                 {
                     (QC.Zone.SouthernSanDoria, 1003), (QC.Zone.SouthernSanDoria, 1023),
                     (QC.Zone.SouthernSanDoria, 2003), (QC.Zone.SouthernSanDoria, 2023),
                     (QC.Zone.NorthernSanDoria, 1003), (QC.Zone.NorthernSanDoria, 1023),
                 })
        {
            var ch = C2MHandler(d, 2, fin, zone, id: csid);
            var ok = F(id, 2);
            var failed = F(id, 2);
            failed.CompleteResult = false;
            Check($"c3 n1 bat hunt: {zone}/{csid}'s finish: complete then the items taken; complete false: kept",
                  "Mission.Complete(),Trade.Complete()|Mission.Complete()",
                  $"{C2MRun(ch, ok)}|{C2MRun(ch, failed)}");
        }
    }

    static void RunC3N1TheRescueDrill()
    {
        var m = new QC.Missions.Sandoria.TheRescueDrill();
        var d = m.Compile();
        int log = QC.MissionLog.Sandoria, id = QC.Mission.Sandoria.TheRescueDrill;
        Check("c3 n1 the rescue drill: clean, its key and LegacyPath, 300 rank points, no gil or rank",
              $"|{QC.MissionKey.Sandoria.TheRescueDrill}|scripts/missions/sandoria/2_1_The_Rescue_Drill|//300",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|" +
              $"{d.Reward.Gil}/{d.Reward.Rank}/{d.Reward.RankPoints}");
        Fake F(int current, int status = 0, int nation = 0, bool done = false, int option = 0, int optionVar = 0,
               bool trade = false)
        {
            var f = new Fake { ThisMission = (MissionLog.Sandoria, id), NationId = nation, Option = option,
                               TradeResult = trade };
            f.CurrentMissions[log] = current;
            f.MissionStatuses[log] = status;
            if (optionVar != 0) f.MissionVars[$"{log}/{id}/Option"] = optionVar;
            if (done) f.CompletedMissions.Add($"{log}/{id}");
            f.TextIds[QC.Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET.ToString()] = 7000;
            f.TextIds[QC.Text.NorthernSanDoria.ORIGINAL_MISSION_OFFSET.ToString()] = 8000;
            f.TextIds[QC.Text.LaTheinePlateau.RESCUE_DRILL.ToString()] = 5000;
            f.TextIds[QC.Text.OrdellesCaves.RUILLONT_INITIAL_DIALOG.ToString()] = 6000;
            return f;
        }
        const int none = QC.Mission.Sandoria.None;
        int other = QC.Mission.Sandoria.BatHunt;
        Check("c3 n1 the rescue drill: section 1 needs no current mission and a San d'Orian (no completed check)",
              "True False False False True",
              $"{d.States[0].IsMissionMatch(F(none))} {d.States[0].IsMissionMatch(F(none, nation: 1))} " +
              $"{d.States[0].IsMissionMatch(F(other))} {d.States[0].IsMissionMatch(F(id))} " +
              $"{d.States[0].IsMissionMatch(F(none, done: true))}");
        Check("c3 n1 the rescue drill: section 2 holds while 2-1 is current only", "True False False",
              $"{d.States[1].IsMissionMatch(F(id))} {d.States[1].IsMissionMatch(F(none))} " +
              $"{d.States[1].IsMissionMatch(F(other))}");
        Check("c3 n1 the rescue drill: section 3 holds while 2-1 is completed, whatever is current",
              "True True False",
              $"{d.States[2].IsMissionMatch(F(none, done: true))} {d.States[2].IsMissionMatch(F(other, done: true))} " +
              $"{d.States[2].IsMissionMatch(F(none))}");

        var fin = QuestHandlerKind.OnEventFinish;
        var trig = QuestHandlerKind.OnTrigger;
        const ushort south = QC.Zone.SouthernSanDoria, north = QC.Zone.NorthernSanDoria;
        foreach (var (zone, csid, label) in new[]
                 {
                     (south, 1009, "Southern"), (south, 2009, "Southern"), (north, 1009, "Northern"),
                 })
        {
            var h = C2MHandler(d, 0, fin, zone, id: csid);
            Check($"c3 n1 the rescue drill: {label} {csid}'s finish: option 3 begins; 2, 4 and 0 do nothing",
                  "Mission.Begin()|||",
                  $"{C2MRun(h, F(none, option: 3))}|{C2MRun(h, F(none, option: 2))}|" +
                  $"{C2MRun(h, F(none, option: 4))}|{C2MRun(h, F(none))}");
        }

        foreach (var (zone, npc, label, ev, ofs) in new[]
                 {
                     (south, QC.Npc.SouthernSanDoria.Ambrotien, "Ambrotien", 2005, 7000),
                     (south, QC.Npc.SouthernSanDoria.Endracion, "Endracion", 1005, 7000),
                     (north, QC.Npc.NorthernSanDoria.Grilau, "Grilau", 1005, 8000),
                 })
        {
            var th = C2MHandler(d, 1, trig, zone, npc);
            Check($"c3 n1 the rescue drill: {label}: status 11 plays {ev}; status 10 and 12: " +
                  "ORIGINAL_MISSION_OFFSET + 36",
                  $"ProgressEvent({ev})|MessageText({ofs + 36})|MessageText({ofs + 36})",
                  $"{C3NReply(th, F(id, 11))}|{C3NReply(th, F(id, 10))}|{C3NReply(th, F(id, 12))}");
        }
        foreach (var (zone, csid) in new[] { (south, 1005), (south, 2005), (north, 1005) })
        {
            var ch = C2MHandler(d, 1, fin, zone, id: csid);
            var ok = F(id, 11);
            var failed = F(id, 11);
            failed.CompleteResult = false;
            Check($"c3 n1 the rescue drill: {zone}/{csid}'s finish: complete then the certificate taken; complete " +
                  "false: kept",
                  $"Mission.Complete(),Player.DelKeyItem({QC.KeyItem.RescueTrainingCertificate})|Mission.Complete()",
                  $"{C2MRun(ch, ok)}|{C2MRun(ch, failed)}");
        }

        const ushort plateau = QC.Zone.LaTheinePlateau;
        string T(string npc, Fake f) => C3NReply(C2MHandler(d, 1, trig, plateau, npc), f);
        // Equesobillot reads the status of the char's nation (the others, the log's)
        var bastok = F(id, 0, nation: 1);
        bastok.MissionStatuses[1] = 2;
        Check("c3 n1 the rescue drill: Equesobillot reads the status of the char's nation: Bastok status 2: 101; " +
              "Sandoria status 2 with Bastok 0: RESCUE_DRILL + 0",
              "ProgressEvent(101)|MessageText(5000)",
              $"{T(QC.Npc.LaTheinePlateau.Equesobillot, bastok)}|" +
              $"{T(QC.Npc.LaTheinePlateau.Equesobillot, F(id, 2, nation: 1))}");
        Check("c3 n1 the rescue drill: Galaihaurat reads the log's status: Sandoria 0 (Bastok 2): 110; Sandoria 3 " +
              "(Bastok 0): RESCUE_DRILL + 0",
              "ProgressEvent(110)|MessageText(5000)",
              $"{T(QC.Npc.LaTheinePlateau.Galaihaurat, F(id, 0, nation: 1))}|" +
              $"{T(QC.Npc.LaTheinePlateau.Galaihaurat, F(id, 3, nation: 1))}");
        foreach (var (npc, label, own, ev) in new[]
                 {
                     (QC.Npc.LaTheinePlateau.Deaufrain, "Deaufrain", 3, 113),
                     (QC.Npc.LaTheinePlateau.Equesobillot, "Equesobillot", 2, 112),
                     (QC.Npc.LaTheinePlateau.Galaihaurat, "Galaihaurat", 1, 114),
                 })
        {
            int wrong = own == 3 ? 1 : own + 1;
            Check($"c3 n1 the rescue drill: {label}: status 8: Option {own} plays {ev}, any other, or unset, +21; " +
                  $"status 9: Option {own} +25, any other, or unset, +26",
                  $"ProgressEvent({ev})|MessageText(5021)|MessageText(5021)|MessageText(5025)|MessageText(5026)|" +
                  "MessageText(5026)",
                  $"{T(npc, F(id, 8, optionVar: own))}|{T(npc, F(id, 8, optionVar: wrong))}|{T(npc, F(id, 8))}|" +
                  $"{T(npc, F(id, 9, optionVar: own))}|{T(npc, F(id, 9, optionVar: wrong))}|{T(npc, F(id, 9))}");
        }

        foreach (var csid in new[] { 112, 113, 114 })
        {
            var ch = C2MHandler(d, 1, fin, plateau, id: csid);
            var ok = F(id, 8);
            var full = F(id, 8);
            full.GiveResult = false;
            Check($"c3 n1 the rescue drill: {csid}'s finish: the Bronze Sword, then status 9 only when it fits",
                  $"Player.GiveItem({QC.Item.BronzeSword}),Player.SetMissionStatus(0,9) 9|" +
                  $"Player.GiveItem({QC.Item.BronzeSword}) 8",
                  $"{C2MRun(ch, ok)} {ok.MissionStatuses[log]}|{C2MRun(ch, full)} {full.MissionStatuses[log]}");
        }
        var c115 = F(id, 10);
        Check("c3 n1 the rescue drill: 115's finish gives the certificate and sets status 11",
              $"Player.GiveKeyItem({QC.KeyItem.RescueTrainingCertificate}),Player.SetMissionStatus(0,11) 11",
              $"{C2MRun(C2MHandler(d, 1, fin, plateau, id: 115), c115)} {c115.MissionStatuses[log]}");
        foreach (var (csid, from, to) in new[] { (101, 2, 3), (102, 3, 4), (106, 5, 6), (107, 6, 7), (108, 4, 5),
                                                 (110, 0, 2) })
        {
            var f = F(id, from);
            Check($"c3 n1 the rescue drill: {csid}'s finish sets status {to}",
                  $"Player.SetMissionStatus(0,{to}) {to}",
                  $"{C2MRun(C2MHandler(d, 1, fin, plateau, id: csid), f)} {f.MissionStatuses[log]}");
        }

        const ushort caves = QC.Zone.OrdellesCaves;
        string Ru(Fake f) => C3NReply(C2MHandler(d, 1, trig, caves, QC.Npc.OrdellesCaves.Ruillont), f);
        var tr = C2MHandler(d, 1, QuestHandlerKind.OnTrade, caves, QC.Npc.OrdellesCaves.Ruillont);
        // the fake's Trade.Matches answers its TradeResult whatever the items: the item ids are pinned by the replay
        Check("c3 n1 the rescue drill: Ruillont's trade: a match at status 9: 2; at status 8 or 10, or no match: " +
              "no event",
              "ProgressEvent(2)|null|null|null",
              $"{C2MReply(tr, F(id, 9, trade: true))}|{C2MReply(tr, F(id, 8, trade: true))}|" +
              $"{C2MReply(tr, F(id, 10, trade: true))}|{C2MReply(tr, F(id, 9))}");
        Check("c3 n1 the rescue drill: Ruillont's trigger: status 0 and 1: nothing; 2 to 7: event 1; 8 and 9: his " +
              "dialog; 10, or completed at 0, 1, 8: +9",
              "null|null|ProgressEvent(1)|ProgressEvent(1)|MessageText(6000)|MessageText(6000)|MessageText(6009)|" +
              "MessageText(6009)|MessageText(6009)|MessageText(6009)",
              $"{Ru(F(id, 0))}|{Ru(F(id, 1))}|{Ru(F(id, 2))}|{Ru(F(id, 7))}|{Ru(F(id, 8))}|{Ru(F(id, 9))}|" +
              $"{Ru(F(id, 10))}|{Ru(F(id, 8, done: true))}|{Ru(F(id, 0, done: true))}|{Ru(F(id, 1, done: true))}");
        foreach (var roll in new[] { 1, 2, 3 })
        {
            var f = F(id, 4);
            f.Rolls.Enqueue(roll);
            Check($"c3 n1 the rescue drill: Ruillont's finish 1: Option is a random 1 to 3 ({roll}), status 8",
                  $"Player.RandomInt(1,3),Mission.SetVar(Option,{roll}),Player.SetMissionStatus(0,8) 8 {roll}",
                  $"{C2MRun(C2MHandler(d, 1, fin, caves, id: 1), f)} {f.MissionStatuses[log]} " +
                  $"{f.MissionVars[$"{log}/{id}/Option"]}");
        }
        var sw = F(id, 9);
        Check("c3 n1 the rescue drill: Ruillont's finish 2: status 10 then the sword taken",
              "Player.SetMissionStatus(0,10),Trade.Complete() 10",
              $"{C2MRun(C2MHandler(d, 1, fin, caves, id: 2), sw)} {sw.MissionStatuses[log]}");

        Check("c3 n1 the rescue drill: Augevinne and Yaucevouchat: 103 and 104 at 5 to 7, +6 and +7 below",
              "ProgressEvent(103)|ProgressEvent(103)|MessageText(5006)|ProgressEvent(104)|ProgressEvent(104)|" +
              "MessageText(5007)",
              $"{T(QC.Npc.LaTheinePlateau.Augevinne, F(id, 5))}|{T(QC.Npc.LaTheinePlateau.Augevinne, F(id, 7))}|" +
              $"{T(QC.Npc.LaTheinePlateau.Augevinne, F(id, 4))}|{T(QC.Npc.LaTheinePlateau.Yaucevouchat, F(id, 5))}|" +
              $"{T(QC.Npc.LaTheinePlateau.Yaucevouchat, F(id, 7))}|{T(QC.Npc.LaTheinePlateau.Yaucevouchat, F(id, 4))}");
        Check("c3 n1 the rescue drill: Vicorpasse at status 11: RESCUE_DRILL + 29 naming the certificate",
              $"MessageText(5029,{QC.KeyItem.RescueTrainingCertificate})",
              T(QC.Npc.LaTheinePlateau.Vicorpasse, F(id, 11)));
        var galaih = C3NReply(C2MHandler(d, 2, trig, plateau, QC.Npc.LaTheinePlateau.Galaihaurat), F(none, done: true));
        Check("c3 n1 the rescue drill: section 3: Galaihaurat answers RESCUE_DRILL + 39 in place of his default",
              "MessageText(5039):replaceDefault()", galaih);
    }

    static void RunC3N1TheDavoiReport()
    {
        var m = new QC.Missions.Sandoria.TheDavoiReport();
        var d = m.Compile();
        int log = QC.MissionLog.Sandoria, id = QC.Mission.Sandoria.TheDavoiReport;
        Check("c3 n1 the davoi report: clean, its key and LegacyPath, 350 rank points, no gil or rank",
              $"|{QC.MissionKey.Sandoria.TheDavoiReport}|scripts/missions/sandoria/2_2_The_Davoi_Report|//350",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|" +
              $"{d.Reward.Gil}/{d.Reward.Rank}/{d.Reward.RankPoints}");
        Fake F(int current, int status = 0, int nation = 0, bool done = false, int option = 0, bool lost = false,
               bool report = false)
        {
            var f = new Fake { ThisMission = (MissionLog.Sandoria, id), NationId = nation, Option = option };
            f.CurrentMissions[log] = current;
            f.MissionStatuses[log] = status;
            if (done) f.CompletedMissions.Add($"{log}/{id}");
            if (lost) f.KeyItems[QC.KeyItem.LostDocument] = true;
            if (report) f.KeyItems[QC.KeyItem.TempleKnightsDavoiReport] = true;
            f.TextIds[QC.Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET.ToString()] = 7000;
            f.TextIds[QC.Text.NorthernSanDoria.ORIGINAL_MISSION_OFFSET.ToString()] = 8000;
            return f;
        }
        const int none = QC.Mission.Sandoria.None;
        int other = QC.Mission.Sandoria.BatHunt;
        Check("c3 n1 the davoi report: section 1 needs no current mission and a San d'Orian (no completed check)",
              "True False False False True",
              $"{d.States[0].IsMissionMatch(F(none))} {d.States[0].IsMissionMatch(F(none, nation: 1))} " +
              $"{d.States[0].IsMissionMatch(F(other))} {d.States[0].IsMissionMatch(F(id))} " +
              $"{d.States[0].IsMissionMatch(F(none, done: true))}");
        Check("c3 n1 the davoi report: section 2 holds while 2-2 is current only", "True False False",
              $"{d.States[1].IsMissionMatch(F(id))} {d.States[1].IsMissionMatch(F(none))} " +
              $"{d.States[1].IsMissionMatch(F(other))}");

        var fin = QuestHandlerKind.OnEventFinish;
        var trig = QuestHandlerKind.OnTrigger;
        const ushort south = QC.Zone.SouthernSanDoria, north = QC.Zone.NorthernSanDoria, davoi = QC.Zone.Davoi;
        foreach (var (zone, csid, label) in new[] { (south, 1009, "Southern"), (south, 2009, "Southern"),
                                                    (north, 1009, "Northern") })
        {
            var h = C2MHandler(d, 0, fin, zone, id: csid);
            Check($"c3 n1 the davoi report: {label} {csid}'s finish: option 104 begins; 3, 105 and 0 do nothing",
                  "Mission.Begin()|||",
                  $"{C2MRun(h, F(none, option: 104))}|{C2MRun(h, F(none, option: 3))}|" +
                  $"{C2MRun(h, F(none, option: 105))}|{C2MRun(h, F(none))}");
        }

        var zt = C2MHandler(d, 1, trig, davoi, QC.Npc.Davoi.Zantaviat);
        Check("c3 n1 the davoi report: Zantaviat: status 0: 100, the document held or not; status 1 or 2 with the " +
              "document: 104; without it, or at status 3: nothing",
              "ProgressEvent(100)|ProgressEvent(100)|ProgressEvent(104)|ProgressEvent(104)|null|null",
              $"{C3NReply(zt, F(id, 0))}|{C3NReply(zt, F(id, 0, lost: true))}|{C3NReply(zt, F(id, 1, lost: true))}|" +
              $"{C3NReply(zt, F(id, 2, lost: true))}|{C3NReply(zt, F(id, 1))}|{C3NReply(zt, F(id, 3))}");
        var bang = C2MHandler(d, 1, trig, davoi, "!");
        string Bang(double x, bool lost = false, int nation = 0)
        {
            var f = F(id, 1, nation: nation, lost: lost);
            f.Npc = new QuestNpcFake(1) { Name = "!", XPos = x };
            return C3NReply(bang, f) + " " + string.Join(",", f.Calls.Where(c => c.StartsWith("Player.")));
        }
        const string g = "KeyItem(103) Player.SetMissionStatus(0,2)";
        Check("c3 n1 the davoi report: the ! NPC: x strictly between 210 and 212 with no document held: the Lost " +
              "Document and status 2; 210 and 212 themselves, outside them, or the document held: nothing",
              string.Join("|", g, g, g, "N", "N", "N", "N", "N", "N"),
              string.Join("|", new[] { 210.01, 211.99, 211.2, 210, 212, 209.9, 212.1, 294 }.Select(x => Bang(x))
                  .Concat(new[] { Bang(211.2, lost: true) }).Select(s => s.StartsWith("null") ? "N" : s)));
        Check("c3 n1 the davoi report: the ! NPC sets the status of the char's nation: a Bastokan: Bastok's",
              "KeyItem(103) Player.SetMissionStatus(1,2)", Bang(211.2, nation: 1));

        foreach (var (zone, npc, label, rep, first, ofs) in new[]
                 {
                     (south, QC.Npc.SouthernSanDoria.Ambrotien, "Ambrotien", 2006, 2028, 7000),
                     (south, QC.Npc.SouthernSanDoria.Endracion, "Endracion", 1006, 1028, 7000),
                     (north, QC.Npc.NorthernSanDoria.Grilau, "Grilau", 1006, 1028, 8000),
                 })
        {
            var th = C2MHandler(d, 1, trig, zone, npc);
            Check($"c3 n1 the davoi report: {label}: status 3: {rep} once completed, {first} (0, 0, 0, 44) the first " +
                  "time; any other status: ORIGINAL_MISSION_OFFSET + 44",
                  $"ProgressEvent({rep})|ProgressEvent({first},0,0,0,44)|MessageText({ofs + 44})|" +
                  $"MessageText({ofs + 44})|MessageText({ofs + 44})",
                  $"{C3NReply(th, F(id, 3, done: true))}|{C3NReply(th, F(id, 3))}|{C3NReply(th, F(id, 2))}|" +
                  $"{C3NReply(th, F(id, 4))}|{C3NReply(th, F(id, 0, done: true))}");
        }
        var door = C2MHandler(d, 1, trig, north, QC.Npc.NorthernSanDoria._6fc);
        Check("c3 n1 the davoi report: the Papal Chambers door: 695 only when not completed and the report held",
              "ProgressEvent(695)|ProgressEvent(695)|null|null|null",
              $"{C3NReply(door, F(id, 0, report: true))}|{C3NReply(door, F(id, 3, report: true))}|" +
              $"{C3NReply(door, F(id, 3, report: true, done: true))}|{C3NReply(door, F(id, 3))}|" +
              $"{C3NReply(door, F(id, 3, done: true))}");

        var z100 = F(id, 0);
        Check("c3 n1 the davoi report: Davoi 100's finish sets status 1",
              "Player.SetMissionStatus(0,1) 1",
              $"{C2MRun(C2MHandler(d, 1, fin, davoi, id: 100), z100)} {z100.MissionStatuses[log]}");
        var z104 = F(id, 2, lost: true);
        Check("c3 n1 the davoi report: Davoi 104's finish: status 3, the document taken, the report given",
              $"Player.SetMissionStatus(0,3),Player.DelKeyItem({QC.KeyItem.LostDocument})," +
              $"Player.GiveKeyItem({QC.KeyItem.TempleKnightsDavoiReport}) 3",
              $"{C2MRun(C2MHandler(d, 1, fin, davoi, id: 104), z104)} {z104.MissionStatuses[log]}");
        foreach (var (zone, csid) in new[] { (south, 1006), (south, 2006), (north, 695), (north, 1006) })
        {
            var ch = C2MHandler(d, 1, fin, zone, id: csid);
            var ok = F(id, 3, report: true);
            var failed = F(id, 3, report: true);
            failed.CompleteResult = false;
            Check($"c3 n1 the davoi report: {zone}/{csid}'s finish: complete then the report taken; complete false: " +
                  "kept",
                  $"Mission.Complete(),Player.DelKeyItem({QC.KeyItem.TempleKnightsDavoiReport})|Mission.Complete()",
                  $"{C2MRun(ch, ok)}|{C2MRun(ch, failed)}");
        }
    }

    static void RunC3N1TheHorutotoRuinsExperiment()
    {
        var m = new QC.Missions.Windurst.TheHorutotoRuinsExperiment();
        var d = m.Compile();
        int log = QC.MissionLog.Windurst, id = QC.Mission.Windurst.TheHorutotoRuinsExperiment;
        int heart = QC.Mission.Windurst.TheHeartOfTheMatter;
        const int orb = QC.KeyItem.CrackedManaOrb;
        Check("c3 n1 horutoto ruins: clean, its key and LegacyPath, 250 rank points, no gil or rank",
              $"|{QC.MissionKey.Windurst.TheHorutotoRuinsExperiment}|" +
              "scripts/missions/windurst/1_1_The_Horutoto_Ruins_Experiment|//250",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|" +
              $"{d.Reward.Gil}/{d.Reward.Rank}/{d.Reward.RankPoints}");
        Fake F(int current, int status = 0, int nation = 2, bool done = false, bool doneHeart = false, int option = 0)
        {
            var f = new Fake { ThisMission = (MissionLog.Windurst, id), NationId = nation, Option = option };
            f.CurrentMissions[log] = current;
            f.MissionStatuses[log] = status;
            if (done) f.CompletedMissions.Add($"{log}/{id}");
            if (doneHeart) f.CompletedMissions.Add($"{log}/{heart}");
            return f;
        }
        string Hold(int state, params Fake[] fakes) =>
            string.Join(" ", fakes.Select(f => d.States[state].IsMissionMatch(f)));
        const int none = QC.Mission.Windurst.None;
        Check("c3 n1 horutoto ruins: section 1 needs no current mission, a Windurst citizen and 1-1 not completed",
              "True False False False False",
              Hold(0, F(none), F(none, nation: 1), F(heart), F(id), F(none, done: true)));
        Check("c3 n1 horutoto ruins: section 2 holds while 1-1 is current, at any status", "True True True False",
              Hold(1, F(id), F(id, status: 3), F(id, status: 4), F(none)));
        Check("c3 n1 horutoto ruins: status 1's section holds at 1 only while current", "True False False False",
              Hold(2, F(id, status: 1), F(id, status: 2), F(id), F(none, status: 1)));
        Check("c3 n1 horutoto ruins: status 2's section holds at 2 only while current", "True False False False",
              Hold(3, F(id, status: 2), F(id, status: 3), F(id, status: 1), F(none, status: 2)));
        Check("c3 n1 horutoto ruins: status 3's section holds at 3 only while current", "True False False False",
              Hold(4, F(id, status: 3), F(id, status: 4), F(id, status: 2), F(none, status: 3)));
        Check("c3 n1 horutoto ruins: status 4's section holds at 4 only while current", "True False False False",
              Hold(5, F(id, status: 4), F(id, status: 3), F(id, status: 5), F(none, status: 4)));
        Check("c3 n1 horutoto ruins: the last section needs no current mission, a Windurst citizen, 1-1 completed " +
              "and 1-2 not completed",
              "True False False False False False",
              Hold(6, F(none, done: true), F(none, done: true, nation: 1), F(heart, done: true), F(none),
                   F(none, done: true, doneHeart: true), F(id, done: true)));

        var fin = QuestHandlerKind.OnEventFinish;
        var trig = QuestHandlerKind.OnTrigger;
        const ushort port = QC.Zone.PortWindurst, walls = QC.Zone.WindurstWalls, waters = QC.Zone.WindurstWaters;
        const ushort woods = QC.Zone.WindurstWoods, ruins = QC.Zone.InnerHorutotoRuins;
        foreach (var (zone, csid, npc, title, text, label) in new[]
                 {
                     (port, 83, QC.Npc.PortWindurst.JanshuraRashura, QC.Title.NewBestOfTheWestRecruit,
                      QC.Text.PortWindurst.YOU_ACCEPT_THE_MISSION, "Port Windurst"),
                     (walls, 96, QC.Npc.WindurstWalls.ZokimaRokima, QC.Title.HeavensTowerGatehouseRecruit,
                      QC.Text.WindurstWalls.YOU_ACCEPT_THE_MISSION, "Windurst Walls"),
                     (waters, 118, QC.Npc.WindurstWaters.Mokyokyo, QC.Title.FreshNorthWindsRecruit,
                      QC.Text.WindurstWaters.YOU_ACCEPT_THE_MISSION, "Windurst Waters"),
                     (woods, 121, QC.Npc.WindurstWoods.RakohBuuma, QC.Title.NewBuumasBoomersRecruit,
                      QC.Text.WindurstWoods.YOU_ACCEPT_THE_MISSION, "Windurst Woods"),
                 })
        {
            Check($"c3 n1 horutoto ruins: {label}: the gate guard plays progress event {csid}",
                  $"ProgressEvent({csid})", C2MReply(C2MHandler(d, 0, trig, zone, npc), F(none)));
            var h = C2MHandler(d, 0, fin, zone, id: csid);
            Check($"c3 n1 horutoto ruins: {label} {csid}'s finish: option 1 begins, gives its title, sets status 1 " +
                  "and says YOU_ACCEPT_THE_MISSION; option 2 and 0 do nothing",
                  $"Mission.Begin(),Player.AddTitle({title}),Player.SetMissionStatus({log},1),Text.Special({text})||",
                  $"{C2MRun(h, F(none, option: 1))}|{C2MRun(h, F(none, option: 2))}|{C2MRun(h, F(none))}");
        }

        string Ev(int state, ushort zone, string npc, int status = 0) =>
            C2MReply(C2MHandler(d, state, trig, zone, npc), F(id, status));
        Check("c3 n1 horutoto ruins: section 2: Port Windurst's NPCs play 89, 86, 88, 87",
              "Event(89) Event(86) Event(88) Event(87)",
              $"{Ev(1, port, QC.Npc.PortWindurst.JanshuraRashura)} {Ev(1, port, QC.Npc.PortWindurst.NineofClubs)} " +
              $"{Ev(1, port, QC.Npc.PortWindurst.PuoRhen)} {Ev(1, port, QC.Npc.PortWindurst.TenofClubs)}");
        Check("c3 n1 horutoto ruins: section 2: Windurst Walls' NPCs play 100, 101, 99",
              "Event(100) Event(101) Event(99)",
              $"{Ev(1, walls, QC.Npc.WindurstWalls.KeoKoruo)} {Ev(1, walls, QC.Npc.WindurstWalls.PakkePokke)} " +
              $"{Ev(1, walls, QC.Npc.WindurstWalls.ZokimaRokima)}");
        Check("c3 n1 horutoto ruins: section 2: Windurst Waters' NPCs play 121, 123, 122, 124",
              "Event(121) Event(123) Event(122) Event(124)",
              $"{Ev(1, waters, QC.Npc.WindurstWaters.DagozaBeruza)} {Ev(1, waters, QC.Npc.WindurstWaters.Mokyokyo)} " +
              $"{Ev(1, waters, QC.Npc.WindurstWaters.PannaDonna)} {Ev(1, waters, QC.Npc.WindurstWaters.TenofHearts)}");
        Check("c3 n1 horutoto ruins: section 2: Windurst Woods' NPCs play 124, 127, 125, 126",
              "Event(124) Event(127) Event(125) Event(126)",
              $"{Ev(1, woods, QC.Npc.WindurstWoods.MiiriWohri)} {Ev(1, woods, QC.Npc.WindurstWoods.RakohBuuma)} " +
              $"{Ev(1, woods, QC.Npc.WindurstWoods.SolaJaab)} {Ev(1, woods, QC.Npc.WindurstWoods.TihPikeh)}");

        Check("c3 n1 horutoto ruins: status 1: Hakkuru-Rinkuru plays progress event 90; its finish sets status 2",
              $"ProgressEvent(90) Player.SetMissionStatus({log},2)",
              $"{Ev(2, port, QC.Npc.PortWindurst.HakkuruRinkuru, 1)} " +
              C2MRun(C2MHandler(d, 2, fin, port, id: 90), F(id, 1)));

        Check("c3 n1 horutoto ruins: status 2: Sama_Gohjima 53, Hakkuru-Rinkuru 91, Kuroido-Moido 92 (important " +
              "once), the gate progress cutscene 42",
              "Event(53) Event(91) Event(92):importantOnce() ProgressCutscene(42)",
              $"{Ev(3, QC.Zone.EastSarutabaruta, QC.Npc.EastSarutabaruta.SamaGohjima, 2)} " +
              $"{Ev(3, port, QC.Npc.PortWindurst.HakkuruRinkuru, 2)} " +
              $"{Ev(3, port, QC.Npc.PortWindurst.KuroidoMoido, 2)} " +
              Ev(3, ruins, QC.Npc.InnerHorutotoRuins._5c5, 2));
        foreach (int roll in new[] { 1, 4, 6 })
        {
            var gate = F(id, 2);
            gate.Rolls.Enqueue(roll);
            string ran = C2MRun(C2MHandler(d, 3, fin, ruins, id: 42), gate);
            Check($"c3 n1 horutoto ruins: the gate's finish 42 sets status 3 and RandomGizmo to the roll ({roll})",
                  $"Player.SetMissionStatus({log},3),Player.RandomInt(1,6),Mission.SetVar(RandomGizmo,{roll}) 3 {roll}",
                  $"{ran} {gate.MissionStatus(log)} {gate.MissionVars[$"{log}/{id}/RandomGizmo"]}");
        }

        string[] gizmos = { QC.Npc.InnerHorutotoRuins._5cp, QC.Npc.InnerHorutotoRuins._5cq,
                            QC.Npc.InnerHorutotoRuins._5cr, QC.Npc.InnerHorutotoRuins._5cs,
                            QC.Npc.InnerHorutotoRuins._5ct, QC.Npc.InnerHorutotoRuins._5cu };
        Fake G(int random, int examined = 0)
        {
            var f = F(id, 3);
            f.MissionVars[$"{log}/{id}/RandomGizmo"] = random;
            f.MissionVars[$"{log}/{id}/GizmoExamined"] = examined;
            return f;
        }
        string Bits(Fake f) => f.MissionVars[$"{log}/{id}/GizmoExamined"].ToString();
        for (int i = 1; i <= 6; i++)
        {
            var h = C2MHandler(d, 4, trig, ruins, gizmos[i - 1]);
            int success = 46 + 2 * i, failure = 47 + 2 * i, wrong = i % 6 + 1, bit = 1 << i, all = 0x7E;
            Check($"c3 n1 horutoto ruins: gizmo {i}: the broken one plays {success}, with its bit set or not",
                  $"ProgressCutscene({success})|ProgressCutscene({success})",
                  $"{C2MReply(h, G(i))}|{C2MReply(h, G(i, bit))}");
            var f1 = G(wrong);
            var f2 = G(wrong, all & ~bit);
            Check($"c3 n1 horutoto ruins: gizmo {i}: not the broken one, its bit clear: {failure}, and its bit set " +
                  "(with the other bits kept)",
                  $"ProgressCutscene({failure}) {bit}|ProgressCutscene({failure}) {all}",
                  $"{C2MReply(h, f1)} {Bits(f1)}|{C2MReply(h, f2)} {Bits(f2)}");
            Check($"c3 n1 horutoto ruins: gizmo {i}: not the broken one, its bit set: EXAMINED_RECEPTACLE, bits kept",
                  $"MessageSpecial({QC.Text.InnerHorutotoRuins.EXAMINED_RECEPTACLE}) {bit}",
                  $"{C2MReply(h, G(wrong, bit))} {Bits(G(wrong, bit))}");
        }
        for (int csid = 48; csid <= 59; csid++)
        {
            bool win = csid % 2 == 0;
            var f = G(csid / 2 - 23, 5 << 1);
            string ran = C2MRun(C2MHandler(d, 4, fin, ruins, id: csid), f);
            Check($"c3 n1 horutoto ruins: gizmo cutscene {csid}'s finish: " + (win
                      ? "gives the Cracked Mana Orb, sets status 4 and clears the gizmo vars"
                      : "says NOT_BROKEN_ORB and changes nothing"),
                  win ? $"Player.GiveKeyItem({orb}),Player.SetMissionStatus({log},4),Mission.SetVar(GizmoExamined,0)," +
                        $"Mission.SetVar(RandomGizmo,0) True 4 0 0"
                      : $"Text.Special({QC.Text.InnerHorutotoRuins.NOT_BROKEN_ORB}) False 3 {csid / 2 - 23} 10",
                  $"{ran} {f.HasKeyItem(orb)} {f.MissionStatus(log)} " +
                  $"{f.MissionVars[$"{log}/{id}/RandomGizmo"]} {Bits(f)}");
        }
        var noRoom = G(1);
        noRoom.GiveResult = false;
        C2MRun(C2MHandler(d, 4, fin, ruins, id: 48), noRoom);
        Check("c3 n1 horutoto ruins: the success finish goes on when the key item was not given",
              "4 0", $"{noRoom.MissionStatus(log)} {noRoom.MissionVars[$"{log}/{id}/RandomGizmo"]}");

        Check("c3 n1 horutoto ruins: status 4: Sama_Gohjima's text at priority 1000, Hakkuru-Rinkuru 94 with the " +
              "orb, Kuroido-Moido 98",
              $"MessageText({QC.Text.EastSarutabaruta.SAMA_GOHJIMA_POSTDIALOG}):setPriority(1000) " +
              $"ProgressEvent(94,0,{orb}) Event(98)",
              $"{Ev(5, QC.Zone.EastSarutabaruta, QC.Npc.EastSarutabaruta.SamaGohjima, 4)} " +
              $"{Ev(5, port, QC.Npc.PortWindurst.HakkuruRinkuru, 4)} " +
              Ev(5, port, QC.Npc.PortWindurst.KuroidoMoido, 4));
        var ok = F(id, 4);
        var failed = F(id, 4);
        failed.CompleteResult = false;
        var finish94 = C2MHandler(d, 5, fin, port, id: 94);
        Check("c3 n1 horutoto ruins: 94's finish: complete, then the orb taken; complete false: the orb kept",
              $"Mission.Complete(),Player.DelKeyItem({orb})|Mission.Complete()",
              $"{C2MRun(finish94, ok)}|{C2MRun(finish94, failed)}");

        string Post(ushort zone, string npc) => C2MReply(C2MHandler(d, 6, trig, zone, npc), F(none, done: true));
        Check("c3 n1 horutoto ruins: last section: Port Windurst: Hakkuru-Rinkuru 96 and Kuroido-Moido 100 " +
              "(important once), 102, 101, 103",
              "Event(96):importantOnce() Event(100):importantOnce() Event(102) Event(101) Event(103)",
              $"{Post(port, QC.Npc.PortWindurst.HakkuruRinkuru)} {Post(port, QC.Npc.PortWindurst.KuroidoMoido)} " +
              $"{Post(port, QC.Npc.PortWindurst.NineofClubs)} {Post(port, QC.Npc.PortWindurst.PuoRhen)} " +
              Post(port, QC.Npc.PortWindurst.TenofClubs));
        Check("c3 n1 horutoto ruins: last section: Windurst Walls 105, 104; Waters 128, 127, 129; Woods 115, 130, 131",
              "Event(105) Event(104) Event(128) Event(127) Event(129) Event(115) Event(130) Event(131)",
              $"{Post(walls, QC.Npc.WindurstWalls.KeoKoruo)} {Post(walls, QC.Npc.WindurstWalls.PakkePokke)} " +
              $"{Post(waters, QC.Npc.WindurstWaters.DagozaBeruza)} {Post(waters, QC.Npc.WindurstWaters.PannaDonna)} " +
              $"{Post(waters, QC.Npc.WindurstWaters.TenofHearts)} {Post(woods, QC.Npc.WindurstWoods.MiiriWohri)} " +
              $"{Post(woods, QC.Npc.WindurstWoods.SolaJaab)} {Post(woods, QC.Npc.WindurstWoods.TihPikeh)}");
    }

    static void RunC3N1TheHeartOfTheMatter()
    {
        var m = new QC.Missions.Windurst.TheHeartOfTheMatter();
        var d = m.Compile();
        int log = QC.MissionLog.Windurst, id = QC.Mission.Windurst.TheHeartOfTheMatter;
        int price = QC.Mission.Windurst.ThePriceOfPeace, horutoto = QC.Mission.Windurst.TheHorutotoRuinsExperiment;
        Check("c3 n1 heart of the matter: clean, its key and LegacyPath, 250 rank points, no gil or rank",
              $"|{QC.MissionKey.Windurst.TheHeartOfTheMatter}|" +
              "scripts/missions/windurst/1_2_The_Heart_of_the_Matter|//250",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|" +
              $"{d.Reward.Gil}/{d.Reward.Rank}/{d.Reward.RankPoints}");
        const int none = QC.Mission.Windurst.None;
        int textBase = 5000;
        string K(string name) => $"{log}/{id}/{name}";
        Fake F(int current, int status = 0, int nation = 2, bool done = false, bool donePrice = false, int option = 0,
               int placed = 0, int collected = 0, int used = 0, int empty = 0)
        {
            var f = new Fake { ThisMission = (MissionLog.Windurst, id), NationId = nation, Option = option };
            f.CurrentMissions[log] = current;
            f.MissionStatuses[log] = status;
            if (done) f.CompletedMissions.Add($"{log}/{id}");
            if (donePrice) f.CompletedMissions.Add($"{log}/{price}");
            f.TextIds[QC.Text.OuterHorutotoRuins.ORB_ALREADY_PLACED.ToString()] = textBase;
            f.MissionVars[K("OrbsPlaced")] = placed;
            f.MissionVars[K("OrbsCollected")] = collected;
            f.MissionVars[K("GizmoUsed")] = used;
            f.MissionVars[K("GizmoEmpty")] = empty;
            return f;
        }
        string Hold(int state, params Fake[] fakes) =>
            string.Join(" ", fakes.Select(f => d.States[state].IsMissionMatch(f)));
        Check("c3 n1 heart of the matter: section 1 needs no current mission, a Windurst citizen and 1-2 not " +
              "completed",
              "True False False False False",
              Hold(0, F(none), F(none, nation: 1), F(horutoto), F(id), F(none, done: true)));
        Check("c3 n1 heart of the matter: section 2 holds while 1-2 is current, at any status", "True True True False",
              Hold(1, F(id), F(id, status: 3), F(id, status: 6), F(none)));
        Check("c3 n1 heart of the matter: status 1's section holds at 1 only while current", "True False False False",
              Hold(2, F(id, status: 1), F(id, status: 2), F(id), F(none, status: 1)));
        Check("c3 n1 heart of the matter: status 2's section holds at 2 only while current", "True False False False",
              Hold(3, F(id, status: 2), F(id, status: 3), F(id, status: 1), F(none, status: 2)));
        Check("c3 n1 heart of the matter: the gizmo section holds at status 3 or 4 only while current",
              "True True False False False",
              Hold(4, F(id, status: 3), F(id, status: 4), F(id, status: 2), F(id, status: 5), F(none, status: 3)));
        Check("c3 n1 heart of the matter: the gate section needs status 3 and six orbs placed, while current",
              "True False False False False",
              Hold(5, F(id, status: 3, placed: 6), F(id, status: 3, placed: 5), F(id, status: 4, placed: 6),
                   F(id, status: 2, placed: 6), F(none, status: 3, placed: 6)));
        Check("c3 n1 heart of the matter: status 5's section holds at 5 only while current",
              "True False False False",
              Hold(6, F(id, status: 5), F(id, status: 4), F(id, status: 6), F(none, status: 5)));
        Check("c3 n1 heart of the matter: status 6's section holds at 6 only while current",
              "True False False False",
              Hold(7, F(id, status: 6), F(id, status: 5), F(id), F(none, status: 6)));
        Check("c3 n1 heart of the matter: the last section needs a Windurst citizen, 1-2 completed and 1-3 not " +
              "completed, whatever is current",
              "True True False False False",
              Hold(8, F(none, done: true), F(horutoto, done: true), F(none, done: true, nation: 1), F(none),
                   F(none, done: true, donePrice: true)));

        var fin = QuestHandlerKind.OnEventFinish;
        var trig = QuestHandlerKind.OnTrigger;
        const ushort port = QC.Zone.PortWindurst, walls = QC.Zone.WindurstWalls, waters = QC.Zone.WindurstWaters;
        const ushort woods = QC.Zone.WindurstWoods, saruta = QC.Zone.EastSarutabaruta;
        const ushort ruins = QC.Zone.OuterHorutotoRuins;
        foreach (var (zone, csid, text, label) in new[]
                 {
                     (port, 78, QC.Text.PortWindurst.YOU_ACCEPT_THE_MISSION, "Port Windurst"),
                     (walls, 93, QC.Text.WindurstWalls.YOU_ACCEPT_THE_MISSION, "Windurst Walls"),
                     (waters, 111, QC.Text.WindurstWaters.YOU_ACCEPT_THE_MISSION, "Windurst Waters"),
                     (woods, 114, QC.Text.WindurstWoods.YOU_ACCEPT_THE_MISSION, "Windurst Woods"),
                 })
        {
            var h = C2MHandler(d, 0, fin, zone, id: csid);
            Check($"c3 n1 heart of the matter: {label} {csid}'s finish: option 1 begins, sets status 1 and says " +
                  "YOU_ACCEPT_THE_MISSION; option 2 and 0 do nothing",
                  $"Mission.Begin(),Player.SetMissionStatus({log},1),Text.Special({text})||",
                  $"{C2MRun(h, F(none, option: 1))}|{C2MRun(h, F(none, option: 2))}|{C2MRun(h, F(none))}");
        }

        string Ev(int state, ushort zone, string npc, int status = 0) =>
            C2MReply(C2MHandler(d, state, trig, zone, npc), F(id, status));
        Check("c3 n1 heart of the matter: section 2: Port Windurst's NPCs play 105, 106, 108, 107",
              "Event(105) Event(106) Event(108) Event(107)",
              $"{Ev(1, port, QC.Npc.PortWindurst.JanshuraRashura)} {Ev(1, port, QC.Npc.PortWindurst.NineofClubs)} " +
              $"{Ev(1, port, QC.Npc.PortWindurst.PuoRhen)} {Ev(1, port, QC.Npc.PortWindurst.TenofClubs)}");
        Check("c3 n1 heart of the matter: section 2: Windurst Walls' NPCs play 110, 108, 109, 107",
              "Event(110) Event(108) Event(109) Event(107)",
              $"{Ev(1, walls, QC.Npc.WindurstWalls.ChawoShipeynyo)} {Ev(1, walls, QC.Npc.WindurstWalls.KeoKoruo)} " +
              $"{Ev(1, walls, QC.Npc.WindurstWalls.PakkePokke)} {Ev(1, walls, QC.Npc.WindurstWalls.ZokimaRokima)}");
        Check("c3 n1 heart of the matter: section 2: Windurst Waters' NPCs play 132, 131, 133, 134",
              "Event(132) Event(131) Event(133) Event(134)",
              $"{Ev(1, waters, QC.Npc.WindurstWaters.DagozaBeruza)} {Ev(1, waters, QC.Npc.WindurstWaters.Mokyokyo)} " +
              $"{Ev(1, waters, QC.Npc.WindurstWaters.PannaDonna)} {Ev(1, waters, QC.Npc.WindurstWaters.TenofHearts)}");
        Check("c3 n1 heart of the matter: section 2: Windurst Woods' NPCs play 133, 136, 134, 135",
              "Event(133) Event(136) Event(134) Event(135)",
              $"{Ev(1, woods, QC.Npc.WindurstWoods.MiiriWohri)} {Ev(1, woods, QC.Npc.WindurstWoods.RakohBuuma)} " +
              $"{Ev(1, woods, QC.Npc.WindurstWoods.SolaJaab)} {Ev(1, woods, QC.Npc.WindurstWoods.TihPikeh)}");

        int[] dark = { QC.KeyItem.FirstDarkManaOrb, QC.KeyItem.SecondDarkManaOrb, QC.KeyItem.ThirdDarkManaOrb,
                       QC.KeyItem.FourthDarkManaOrb, QC.KeyItem.FifthDarkManaOrb, QC.KeyItem.SixthDarkManaOrb };
        int[] glow = { QC.KeyItem.FirstGlowingManaOrb, QC.KeyItem.SecondGlowingManaOrb,
                       QC.KeyItem.ThirdGlowingManaOrb, QC.KeyItem.FourthGlowingManaOrb,
                       QC.KeyItem.FifthGlowingManaOrb, QC.KeyItem.SixthGlowingManaOrb };
        const int charm = QC.KeyItem.SoutheasternStarCharm;
        Check("c3 n1 heart of the matter: status 1: Apururu plays progress event 137; its finish gives the six Dark " +
              "Mana Orbs and sets status 2",
              $"ProgressEvent(137) {string.Join(",", dark.Select(k => $"Player.GiveKeyItem({k})"))}," +
              $"Player.SetMissionStatus({log},2)",
              $"{Ev(2, woods, QC.Npc.WindurstWoods.Apururu, 1)} " +
              C2MRun(C2MHandler(d, 2, fin, woods, id: 137), F(id, 1)));
        Check("c3 n1 heart of the matter: status 2: Apururu plays progress event 138, Pore-Ohre progress cutscene " +
              "46; its finish gives the Southeastern Star Charm and sets status 3",
              $"ProgressEvent(138) ProgressCutscene(46) Player.GiveKeyItem({charm}),Player.SetMissionStatus({log},3)",
              $"{Ev(3, woods, QC.Npc.WindurstWoods.Apururu, 2)} {Ev(3, saruta, QC.Npc.EastSarutabaruta.PoreOhre, 2)} " +
              C2MRun(C2MHandler(d, 3, fin, saruta, id: 46), F(id, 2)));

        Check("c3 n1 heart of the matter: status 3 and 4: Pore-Ohre plays 47",
              "Event(47) Event(47)",
              $"{Ev(4, saruta, QC.Npc.EastSarutabaruta.PoreOhre, 3)} " +
              Ev(4, saruta, QC.Npc.EastSarutabaruta.PoreOhre, 4));
        string[] gizmos = { QC.Npc.OuterHorutotoRuins._5ee, QC.Npc.OuterHorutotoRuins._5ef,
                            QC.Npc.OuterHorutotoRuins._5eg, QC.Npc.OuterHorutotoRuins._5eh,
                            QC.Npc.OuterHorutotoRuins._5ei, QC.Npc.OuterHorutotoRuins._5ej };
        string Calls(Fake f) => string.Join(",", f.Calls);
        for (int i = 1; i <= 6; i++)
        {
            var h = C2MHandler(d, 4, trig, ruins, gizmos[i - 1]);
            int bit = 1 << i, other = 0x7E & ~bit;
            var fresh = F(id, 3);
            var usedHere = F(id, 3, used: bit);
            var usedElse = F(id, 3, used: other);
            var emptyHere = F(id, 3, empty: bit);
            Check($"c3 n1 heart of the matter: gizmo {i} at status 3: its orb not placed plays cutscene {57 + i}, " +
                  "placed says the already-placed message; the other gizmos' bits and the collecting bit do not count",
                  $"Cutscene({57 + i})|null Text.Special(#{textBase})|Cutscene({57 + i})|Cutscene({57 + i})",
                  $"{C2MReply(h, fresh)}|{C2MReply(h, usedHere)} {Calls(usedHere)}|{C2MReply(h, usedElse)}|" +
                  C2MReply(h, emptyHere));
            var collect = F(id, 4);
            var done = F(id, 4, empty: bit);
            var doneElse = F(id, 4, empty: other);
            var usedAtFour = F(id, 4, used: bit);
            Check($"c3 n1 heart of the matter: gizmo {i} at status 4: its orb not collected plays cutscene " +
                  $"{45 + i}, collected says the already-retrieved message; the other bits do not count",
                  $"Cutscene({45 + i})|null Text.Special(#{textBase + 3})|Cutscene({45 + i})|Cutscene({45 + i})",
                  $"{C2MReply(h, collect)}|{C2MReply(h, done)} {Calls(done)}|{C2MReply(h, doneElse)}|" +
                  C2MReply(h, usedAtFour));
        }
        for (int i = 1; i <= 6; i++)
        {
            int n = i % 6, bit = 1 << i;
            var f = F(id, 3, placed: n, used: 1 << 7);
            string ran = C2MRun(C2MHandler(d, 4, fin, ruins, id: 57 + i), f);
            string last = n == 5 ? $",Text.Special(#{textBase + 5})" : "";
            Check($"c3 n1 heart of the matter: cutscene {57 + i}'s finish with {n} placed: sets bit {i}, counts, " +
                  $"says its message with Dark Mana Orb {n + 1}, takes it" + (n == 5 ? " and says all placed" : ""),
                  $"Mission.SetVarBit(GizmoUsed,{i}),Mission.SetVar(OrbsPlaced,{n + 1})," +
                  $"Text.Special(#{textBase + 1},0,0,{dark[n]}),Player.DelKeyItem({dark[n]}){last} " +
                  $"{(1 << 7) | bit} {n + 1}",
                  $"{ran} {f.MissionVars[K("GizmoUsed")]} {f.MissionVars[K("OrbsPlaced")]}");
            int c = (i + 2) % 6, ebit = 1 << i;
            var g = F(id, 4, collected: c, empty: 1 << 7);
            string got = C2MRun(C2MHandler(d, 4, fin, ruins, id: 45 + i), g);
            string end = c == 5
                ? $",Text.Special(#{textBase + 4}),Player.SetMissionStatus({log},5),Mission.SetVar(OrbsCollected,0)," +
                  "Mission.SetVar(GizmoEmpty,0)"
                : "";
            Check($"c3 n1 heart of the matter: cutscene {45 + i}'s finish with {c} collected: sets bit {i}, counts, " +
                  $"gives Glowing Mana Orb {c + 1}" + (c == 5 ? ", says all retrieved, sets status 5, clears" : ""),
                  $"Mission.SetVarBit(GizmoEmpty,{i}),Mission.SetVar(OrbsCollected,{c + 1})," +
                  $"Player.GiveKeyItem({glow[c]})" +
                  $"{end} {(c == 5 ? 0 : (1 << 7) | ebit)} {(c == 5 ? 0 : c + 1)}",
                  $"{got} {g.MissionVars[K("GizmoEmpty")]} {g.MissionVars[K("OrbsCollected")]}");
        }

        var gate = F(id, 3, placed: 6);
        Check("c3 n1 heart of the matter: the gate _5e9 with six placed says STAR_CHARM_DISAPPEARS and plays " +
              "cutscene 44",
              $"Cutscene(44) Text.Special({QC.Text.OuterHorutotoRuins.STAR_CHARM_DISAPPEARS},{ruins},{charm})",
              $"{C2MReply(C2MHandler(d, 5, trig, ruins, QC.Npc.OuterHorutotoRuins._5e9), gate)} {Calls(gate)}");
        var fin44 = F(id, 3, placed: 6, used: 0x7E);
        Check("c3 n1 heart of the matter: cutscene 44's finish takes the charm, sets status 4, says " +
              "ALL_G_ORBS_ENERGIZED and clears the gizmo vars",
              $"Player.DelKeyItem({charm}),Player.SetMissionStatus({log},4)," +
              $"Text.Special({QC.Text.OuterHorutotoRuins.ALL_G_ORBS_ENERGIZED}),Mission.SetVar(GizmoUsed,0)," +
              "Mission.SetVar(OrbsPlaced,0) 0 0",
              $"{C2MRun(C2MHandler(d, 5, fin, ruins, id: 44), fin44)} {fin44.MissionVars[K("GizmoUsed")]} " +
              fin44.MissionVars[K("OrbsPlaced")]);

        string delGlow = string.Join(",", glow.Select(k => $"Player.DelKeyItem({k})"));
        Check("c3 n1 heart of the matter: status 5: the East Sarutabaruta zone-in plays 48, Apururu 145",
              "Event(48) Event(145)",
              $"{C2MReply(C2MHandler(d, 6, QuestHandlerKind.OnZoneIn, saruta), F(id, 5))} " +
              Ev(6, woods, QC.Npc.WindurstWoods.Apururu, 5));
        Check("c3 n1 heart of the matter: 48's finish takes the six glowing orbs and sets status 6",
              $"{delGlow},Player.SetMissionStatus({log},6)",
              C2MRun(C2MHandler(d, 6, fin, saruta, id: 48), F(id, 5)));
        var ok = F(id, 5);
        var failed = F(id, 5);
        failed.CompleteResult = false;
        var finish145 = C2MHandler(d, 6, fin, woods, id: 145);
        Check("c3 n1 heart of the matter: 145's finish takes the six glowing orbs, then completes, whatever the " +
              "result",
              $"{delGlow},Mission.Complete()|{delGlow},Mission.Complete()",
              $"{C2MRun(finish145, ok)}|{C2MRun(finish145, failed)}");

        Check("c3 n1 heart of the matter: status 6: Pore-Ohre and Quh Berhuja's texts, Apururu progress event 143",
              $"MessageText({QC.Text.EastSarutabaruta.PORE_OHRE_STOLEN_ORBS}) " +
              $"MessageText({QC.Text.EastSarutabaruta.QUH_BERHUJA_STOLEN_ORBS}) ProgressEvent(143)",
              $"{Ev(7, saruta, QC.Npc.EastSarutabaruta.PoreOhre, 6)} " +
              $"{Ev(7, saruta, QC.Npc.EastSarutabaruta.QuhBerhuja, 6)} " +
              Ev(7, woods, QC.Npc.WindurstWoods.Apururu, 6));
        Check("c3 n1 heart of the matter: 143's finish completes",
              "Mission.Complete()", C2MRun(C2MHandler(d, 7, fin, woods, id: 143), F(id, 6)));

        string Post(ushort zone, string npc) => C2MReply(C2MHandler(d, 8, trig, zone, npc), F(none, done: true));
        Check("c3 n1 heart of the matter: last section: Port Windurst 74, 79, 80; Walls 105, 104",
              "Event(74) Event(79) Event(80) Event(105) Event(104)",
              $"{Post(port, QC.Npc.PortWindurst.NineofClubs)} {Post(port, QC.Npc.PortWindurst.PuoRhen)} " +
              $"{Post(port, QC.Npc.PortWindurst.TenofClubs)} {Post(walls, QC.Npc.WindurstWalls.KeoKoruo)} " +
              Post(walls, QC.Npc.WindurstWalls.PakkePokke));
        Check("c3 n1 heart of the matter: last section: Waters 112, 113, 114; Woods Apururu 144 (important once), " +
              "118, 117, 119",
              "Event(112) Event(113) Event(114) Event(144):importantOnce() Event(118) Event(117) Event(119)",
              $"{Post(waters, QC.Npc.WindurstWaters.DagozaBeruza)} {Post(waters, QC.Npc.WindurstWaters.PannaDonna)} " +
              $"{Post(waters, QC.Npc.WindurstWaters.TenofHearts)} {Post(woods, QC.Npc.WindurstWoods.Apururu)} " +
              $"{Post(woods, QC.Npc.WindurstWoods.MiiriWohri)} {Post(woods, QC.Npc.WindurstWoods.SolaJaab)} " +
              Post(woods, QC.Npc.WindurstWoods.TihPikeh));
    }

    static void RunC3N1ThePriceOfPeace()
    {
        var m = new QC.Missions.Windurst.ThePriceOfPeace();
        var d = m.Compile();
        int log = QC.MissionLog.Windurst, id = QC.Mission.Windurst.ThePriceOfPeace;
        int lost = QC.Mission.Windurst.LostForWords, heart = QC.Mission.Windurst.TheHeartOfTheMatter;
        const int food = QC.KeyItem.FoodOffering, drink = QC.KeyItem.DrinkOffering;
        Check("c3 n1 price of peace: clean, its key and LegacyPath, 1000 gil, rank 2, no rank points",
              $"|{QC.MissionKey.Windurst.ThePriceOfPeace}|scripts/missions/windurst/1_3_The_Price_of_Peace|1000/2/",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|" +
              $"{d.Reward.Gil}/{d.Reward.Rank}/{d.Reward.RankPoints}");
        const int none = QC.Mission.Windurst.None;
        string K(string name) => $"{log}/{id}/{name}";
        Fake F(int current, int status = 0, int nation = 2, bool done = false, bool doneLost = false, int option = 0,
               int csid = 0, int offered = 0, bool hasFood = false, bool hasDrink = false)
        {
            var f = new Fake
            {
                ThisMission = (MissionLog.Windurst, id), NationId = nation, Option = option, Csid = csid,
            };
            f.CurrentMissions[log] = current;
            f.MissionStatuses[log] = status;
            if (done) f.CompletedMissions.Add($"{log}/{id}");
            if (doneLost) f.CompletedMissions.Add($"{log}/{lost}");
            f.MissionVars[K("OfferingsTurnedIn")] = offered;
            f.KeyItems[food] = hasFood;
            f.KeyItems[drink] = hasDrink;
            return f;
        }
        string Hold(int state, params Fake[] fakes) =>
            string.Join(" ", fakes.Select(f => d.States[state].IsMissionMatch(f)));
        Check("c3 n1 price of peace: section 1 needs no current mission, a Windurst citizen and 1-3 not completed",
              "True False False False False",
              Hold(0, F(none), F(none, nation: 1), F(heart), F(id), F(none, done: true)));
        Check("c3 n1 price of peace: section 2 holds while 1-3 is current, at any status", "True True True False",
              Hold(1, F(id), F(id, status: 2), F(id, status: 4), F(none)));
        Check("c3 n1 price of peace: status 1's section holds at 1 only while current", "True False False False",
              Hold(2, F(id, status: 1), F(id, status: 2), F(id), F(none, status: 1)));
        Check("c3 n1 price of peace: status 2's section holds at 2 only while current", "True False False False",
              Hold(3, F(id, status: 2), F(id, status: 3), F(id, status: 1), F(none, status: 2)));
        Check("c3 n1 price of peace: status 3's section holds at 3 only while current", "True False False False",
              Hold(4, F(id, status: 3), F(id, status: 4), F(id, status: 2), F(none, status: 3)));
        Check("c3 n1 price of peace: status 4's section holds at 4 only while current", "True False False False",
              Hold(5, F(id, status: 4), F(id, status: 3), F(id, status: 5), F(none, status: 4)));
        Check("c3 n1 price of peace: the last section needs no current mission, a Windurst citizen, 1-3 completed " +
              "and Lost for Words not completed",
              "True False False False False False",
              Hold(6, F(none, done: true), F(none, done: true, nation: 1), F(heart, done: true), F(none),
                   F(none, done: true, doneLost: true), F(id, done: true)));

        var fin = QuestHandlerKind.OnEventFinish;
        var trig = QuestHandlerKind.OnTrigger;
        const ushort port = QC.Zone.PortWindurst, walls = QC.Zone.WindurstWalls, waters = QC.Zone.WindurstWaters;
        const ushort woods = QC.Zone.WindurstWoods, giddeus = QC.Zone.Giddeus;
        foreach (var (zone, csid, text, label) in new[]
                 {
                     (port, 78, QC.Text.PortWindurst.YOU_ACCEPT_THE_MISSION, "Port Windurst"),
                     (walls, 93, QC.Text.WindurstWalls.YOU_ACCEPT_THE_MISSION, "Windurst Walls"),
                     (waters, 111, QC.Text.WindurstWaters.YOU_ACCEPT_THE_MISSION, "Windurst Waters"),
                     (woods, 114, QC.Text.WindurstWoods.YOU_ACCEPT_THE_MISSION, "Windurst Woods"),
                 })
        {
            var h = C2MHandler(d, 0, fin, zone, id: csid);
            Check($"c3 n1 price of peace: {label} {csid}'s finish: option 2 begins, sets status 1 and says " +
                  "YOU_ACCEPT_THE_MISSION; option 1 and 0 do nothing",
                  $"Mission.Begin(),Player.SetMissionStatus({log},1),Text.Special({text})||",
                  $"{C2MRun(h, F(none, option: 2))}|{C2MRun(h, F(none, option: 1))}|{C2MRun(h, F(none))}");
        }

        string Ev(int state, ushort zone, string npc, int status = 0) =>
            C2MReply(C2MHandler(d, state, trig, zone, npc), F(id, status));
        Check("c3 n1 price of peace: section 2: Port Windurst's NPCs play 110, 112, 111, 113",
              "Event(110) Event(112) Event(111) Event(113)",
              $"{Ev(1, port, QC.Npc.PortWindurst.JanshuraRashura)} {Ev(1, port, QC.Npc.PortWindurst.NineofClubs)} " +
              $"{Ev(1, port, QC.Npc.PortWindurst.PuoRhen)} {Ev(1, port, QC.Npc.PortWindurst.TenofClubs)}");
        Check("c3 n1 price of peace: section 2: Windurst Walls' NPCs play 115, 114, 113, 112",
              "Event(115) Event(114) Event(113) Event(112)",
              $"{Ev(1, walls, QC.Npc.WindurstWalls.ChawoShipeynyo)} {Ev(1, walls, QC.Npc.WindurstWalls.KeoKoruo)} " +
              $"{Ev(1, walls, QC.Npc.WindurstWalls.PakkePokke)} {Ev(1, walls, QC.Npc.WindurstWalls.ZokimaRokima)}");
        Check("c3 n1 price of peace: section 2: Windurst Waters' NPCs play 138, 136, 137, 139",
              "Event(138) Event(136) Event(137) Event(139)",
              $"{Ev(1, waters, QC.Npc.WindurstWaters.DagozaBeruza)} {Ev(1, waters, QC.Npc.WindurstWaters.Mokyokyo)} " +
              $"{Ev(1, waters, QC.Npc.WindurstWaters.PannaDonna)} {Ev(1, waters, QC.Npc.WindurstWaters.TenofHearts)}");
        Check("c3 n1 price of peace: section 2: Windurst Woods' NPCs play 151, 150, 153, 152",
              "Event(151) Event(150) Event(153) Event(152)",
              $"{Ev(1, woods, QC.Npc.WindurstWoods.MiiriWohri)} {Ev(1, woods, QC.Npc.WindurstWoods.RakohBuuma)} " +
              $"{Ev(1, woods, QC.Npc.WindurstWoods.SolaJaab)} {Ev(1, woods, QC.Npc.WindurstWoods.TihPikeh)}");

        Check("c3 n1 price of peace: status 1: Kenapa-Keppa 145, Leepe-Hoppe progress event 140, Ohbiru-Dohbiru 143",
              "Event(145) ProgressEvent(140) Event(143)",
              $"{Ev(2, waters, QC.Npc.WindurstWaters.KenapaKeppa, 1)} " +
              $"{Ev(2, waters, QC.Npc.WindurstWaters.LeepeHoppe, 1)} " +
              Ev(2, waters, QC.Npc.WindurstWaters.OhbiruDohbiru, 1));
        Check("c3 n1 price of peace: 140's finish gives the Food and Drink Offerings and sets status 2",
              $"Player.GiveKeyItem({food}),Player.GiveKeyItem({drink}),Player.SetMissionStatus({log},2)",
              C2MRun(C2MHandler(d, 2, fin, waters, id: 140), F(id, 1)));

        Check("c3 n1 price of peace: status 2: Leepe-Hoppe progress event 142, Ghoo Pakya progress cutscene 49, " +
              "Laa Mozi 45",
              "ProgressEvent(142) ProgressCutscene(49) ProgressCutscene(45)",
              $"{Ev(3, waters, QC.Npc.WindurstWaters.LeepeHoppe, 2)} {Ev(3, giddeus, QC.Npc.Giddeus.GhooPakya, 2)} " +
              Ev(3, giddeus, QC.Npc.Giddeus.LaaMozi, 2));
        var gtext = QC.Text.Giddeus.OFFERED_UP_KEY_ITEM;
        string Offered(int csid, int count, bool hasFood, bool hasDrink, out Fake f)
        {
            f = F(id, 2, csid: csid, offered: count, hasFood: hasFood, hasDrink: hasDrink);
            return C2MRun(C2MHandler(d, 3, fin, giddeus, id: csid), f);
        }
        string St(Fake f) => $"{f.MissionStatus(log)}/{f.MissionVars[K("OfferingsTurnedIn")]}";
        string foodOne = $"Player.DelKeyItem({food}),Text.Special({gtext},{food}),Mission.SetVar(OfferingsTurnedIn,1)";
        string drinkOne = $"Player.DelKeyItem({drink}),Text.Special({gtext},{drink}),Mission.SetVar(" +
                          "OfferingsTurnedIn,1)";
        string foodTwo = $"Player.DelKeyItem({food}),Text.Special({gtext},{food}),Player.SetMissionStatus({log},3)," +
                         "Mission.SetVar(OfferingsTurnedIn,0)";
        string drinkTwo = $"Player.DelKeyItem({drink}),Text.Special({gtext},{drink}),Player.SetMissionStatus({log}," +
                          "3),Mission.SetVar(OfferingsTurnedIn,0)";
        Fake a1, a2, a3, a4, a5, a6, a7, a8, a9;
        Check("c3 n1 price of peace: 45's finish with the Food Offering takes it, says OFFERED_UP_KEY_ITEM " +
              "and counts it; the second one sets status 3 and clears the count",
              $"{foodOne} 2/1|{foodTwo} 3/0",
              $"{Offered(45, 0, true, true, out a1)} {St(a1)}|{Offered(45, 1, true, true, out a2)} {St(a2)}");
        Check("c3 n1 price of peace: 49's finish with the Drink Offering takes it, says OFFERED_UP_KEY_ITEM and " +
              "counts it; the second one sets status 3 and clears the count",
              $"{drinkOne} 2/1|{drinkTwo} 3/0",
              $"{Offered(49, 0, true, true, out a3)} {St(a3)}|{Offered(49, 1, true, true, out a4)} {St(a4)}");
        Check("c3 n1 price of peace: 45 without the Food Offering and 49 without the Drink Offering take nothing " +
              "and keep the count (also with the other offering in hand)",
              "Mission.SetVar(OfferingsTurnedIn,0) 2/0|Mission.SetVar(OfferingsTurnedIn,1) 2/1|" +
              "Mission.SetVar(OfferingsTurnedIn,0) 2/0|Mission.SetVar(OfferingsTurnedIn,1) 2/1",
              $"{Offered(45, 0, false, true, out a5)} {St(a5)}|{Offered(45, 1, false, true, out a6)} {St(a6)}|" +
              $"{Offered(49, 0, true, false, out a7)} {St(a7)}|{Offered(49, 1, true, false, out a8)} {St(a8)}");
        Check("c3 n1 price of peace: 45's finish with the Drink Offering only does not take the Drink Offering",
              "Mission.SetVar(OfferingsTurnedIn,0) 2/0", $"{Offered(45, 0, false, true, out a9)} {St(a9)}");

        Check("c3 n1 price of peace: status 3: Ghoo Pakya 52 and Laa Mozi 48 over their own events, Ohbiru-Dohbiru 144",
              "Event(52):replaceDefault() Event(48):replaceDefault() Event(144)",
              $"{Ev(4, giddeus, QC.Npc.Giddeus.GhooPakya, 3)} {Ev(4, giddeus, QC.Npc.Giddeus.LaaMozi, 3)} " +
              Ev(4, waters, QC.Npc.WindurstWaters.OhbiruDohbiru, 3));
        var area = C2MHandler(d, 4, QuestHandlerKind.OnTriggerAreaEnter, waters, id: 1);
        Check("c3 n1 price of peace: status 3: trigger area 1 of Windurst Waters plays progress event 146; 146's " +
              "finish sets status 4",
              $"ProgressEvent(146) Player.SetMissionStatus({log},4)",
              $"{C2MReply(area, F(id, 3))} {C2MRun(C2MHandler(d, 4, fin, waters, id: 146), F(id, 3))}");

        Check("c3 n1 price of peace: status 4: Leepe-Hoppe 147 (important once), the gate guards' progress events " +
              "114, 116, 148, 154",
              "Event(147):importantOnce() ProgressEvent(114) ProgressEvent(116) ProgressEvent(148) ProgressEvent(154)",
              $"{Ev(5, waters, QC.Npc.WindurstWaters.LeepeHoppe, 4)} " +
              $"{Ev(5, port, QC.Npc.PortWindurst.JanshuraRashura, 4)} " +
              $"{Ev(5, walls, QC.Npc.WindurstWalls.ZokimaRokima, 4)} " +
              $"{Ev(5, waters, QC.Npc.WindurstWaters.Mokyokyo, 4)} " +
              Ev(5, woods, QC.Npc.WindurstWoods.RakohBuuma, 4));
        foreach (var (zone, csid, label) in new[]
                 {
                     (port, 114, "Port Windurst 114"), (walls, 116, "Windurst Walls 116"),
                     (waters, 148, "Windurst Waters 148"), (woods, 154, "Windurst Woods 154"),
                 })
        {
            var h = C2MHandler(d, 5, fin, zone, id: csid);
            var failed = F(id, 4);
            failed.CompleteResult = false;
            Check($"c3 n1 price of peace: {label}'s finish completes the mission, whatever the result",
                  "Mission.Complete()|Mission.Complete()", $"{C2MRun(h, F(id, 4))}|{C2MRun(h, failed)}");
        }

        string Post(ushort zone, string npc) => C2MReply(C2MHandler(d, 6, trig, zone, npc), F(none, done: true));
        Check("c3 n1 price of peace: last section: Port Windurst 117, 116, 118; Walls 120, 118, 119",
              "Event(117) Event(116) Event(118) Event(120) Event(118) Event(119)",
              $"{Post(port, QC.Npc.PortWindurst.NineofClubs)} {Post(port, QC.Npc.PortWindurst.PuoRhen)} " +
              $"{Post(port, QC.Npc.PortWindurst.TenofClubs)} {Post(walls, QC.Npc.WindurstWalls.ChawoShipeynyo)} " +
              $"{Post(walls, QC.Npc.WindurstWalls.KeoKoruo)} {Post(walls, QC.Npc.WindurstWalls.PakkePokke)}");
        Check("c3 n1 price of peace: last section: Waters 150, 152, 154; Woods 157, 158, 131",
              "Event(150) Event(152) Event(154) Event(157) Event(158) Event(131)",
              $"{Post(waters, QC.Npc.WindurstWaters.DagozaBeruza)} {Post(waters, QC.Npc.WindurstWaters.PannaDonna)} " +
              $"{Post(waters, QC.Npc.WindurstWaters.TenofHearts)} {Post(woods, QC.Npc.WindurstWoods.MiiriWohri)} " +
              $"{Post(woods, QC.Npc.WindurstWoods.SolaJaab)} {Post(woods, QC.Npc.WindurstWoods.TihPikeh)}");
    }

    static void RunC3N1LostForWords()
    {
        var m = new QC.Missions.Windurst.LostForWords();
        var d = m.Compile();
        int log = QC.MissionLog.Windurst, id = QC.Mission.Windurst.LostForWords;
        int testing = QC.Mission.Windurst.ATestingTime;
        const int coral = QC.KeyItem.LapisCoral, monocle = QC.KeyItem.LapisMonocle, hideout = QC.KeyItem.HideoutKey;
        Check("c3 n1 lost for words: clean, its key and LegacyPath, 300 rank points, no gil, no rank",
              $"|{QC.MissionKey.Windurst.LostForWords}|scripts/missions/windurst/2_1_Lost_for_Words|//300",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|" +
              $"{d.Reward.Gil}/{d.Reward.Rank}/{d.Reward.RankPoints}");
        const int none = QC.Mission.Windurst.None;
        string K(string name) => $"{log}/{id}/{name}";
        Fake F(int current, int status = 0, int nation = 2, bool done = false, bool doneTest = false, int option = 0,
               int rock = 0, int tih = 0)
        {
            var f = new Fake { ThisMission = (MissionLog.Windurst, id), NationId = nation, Option = option };
            f.CurrentMissions[log] = current;
            f.MissionStatuses[log] = status;
            if (done) f.CompletedMissions.Add($"{log}/{id}");
            if (doneTest) f.CompletedMissions.Add($"{log}/{testing}");
            f.MissionVars[K("Rock")] = rock;
            f.PlayerLocalVars["TihPikehRepeatLine"] = tih;
            return f;
        }
        string Hold(int state, params Fake[] fakes) =>
            string.Join(" ", fakes.Select(f => d.States[state].IsMissionMatch(f)));
        Check("c3 n1 lost for words: section 1 needs no current mission, a Windurst citizen and 2-1 not completed",
              "True False False False False",
              Hold(0, F(none), F(none, nation: 1), F(testing), F(id), F(none, done: true)));
        Check("c3 n1 lost for words: section 2 holds while 2-1 is current, at any status", "True True True False",
              Hold(1, F(id), F(id, status: 2), F(id, status: 4), F(none)));
        for (int s = 1; s <= 7; s++)
        {
            int state = s <= 3 ? s + 1 : s + 2;
            Check($"c3 n1 lost for words: status {s}'s section holds at {s} only while current",
                  "True False False False",
                  Hold(state, F(id, status: s), F(id, status: s % 7 + 1), F(id), F(none, status: s)));
        }
        Check("c3 n1 lost for words: the rock section holds at status 3 and 4 only, and only while current",
              "False False True True False False False False",
              Hold(5, F(id), F(id, status: 1), F(id, status: 3), F(id, status: 4), F(id, status: 5),
                   F(id, status: 2), F(none, status: 3), F(none, status: 4)));
        Check("c3 n1 lost for words: the last section needs a Windurst citizen, 2-1 completed and A Testing Time not " +
              "completed, whatever mission is current",
              "True False True False False True",
              Hold(10, F(none, done: true), F(none, done: true, nation: 1), F(testing, done: true), F(none),
                   F(none, done: true, doneTest: true), F(id, done: true)));

        var fin = QuestHandlerKind.OnEventFinish;
        var trig = QuestHandlerKind.OnTrigger;
        const ushort port = QC.Zone.PortWindurst, walls = QC.Zone.WindurstWalls, waters = QC.Zone.WindurstWaters;
        const ushort woods = QC.Zone.WindurstWoods, maze = QC.Zone.MazeOfShakhrami;
        foreach (var (zone, csid, text, label) in new[]
                 {
                     (port, 78, QC.Text.PortWindurst.YOU_ACCEPT_THE_MISSION, "Port Windurst"),
                     (walls, 93, QC.Text.WindurstWalls.YOU_ACCEPT_THE_MISSION, "Windurst Walls"),
                     (waters, 111, QC.Text.WindurstWaters.YOU_ACCEPT_THE_MISSION, "Windurst Waters"),
                     (woods, 114, QC.Text.WindurstWoods.YOU_ACCEPT_THE_MISSION, "Windurst Woods"),
                 })
        {
            var h = C2MHandler(d, 0, fin, zone, id: csid);
            Check($"c3 n1 lost for words: {label} {csid}'s finish: option 3 begins, sets status 1 and says " +
                  "YOU_ACCEPT_THE_MISSION; option 2, 1 and 0 do nothing",
                  $"Mission.Begin(),Player.SetMissionStatus({log},1),Text.Special({text})|||",
                  $"{C2MRun(h, F(none, option: 3))}|{C2MRun(h, F(none, option: 2))}|" +
                  $"{C2MRun(h, F(none, option: 1))}|{C2MRun(h, F(none))}");
        }

        string Ev(int state, ushort zone, string npc, int status = 0, int tih = 0) =>
            C2MReply(C2MHandler(d, state, trig, zone, npc), F(id, status, tih: tih));
        Check("c3 n1 lost for words: section 2: Port Windurst's NPCs play 120, 121, 123, 122",
              "Event(120) Event(121) Event(123) Event(122)",
              $"{Ev(1, port, QC.Npc.PortWindurst.JanshuraRashura)} {Ev(1, port, QC.Npc.PortWindurst.NineofClubs)} " +
              $"{Ev(1, port, QC.Npc.PortWindurst.PuoRhen)} {Ev(1, port, QC.Npc.PortWindurst.TenofClubs)}");
        Check("c3 n1 lost for words: section 2: Windurst Walls' NPCs play 125, 123, 124, 122",
              "Event(125) Event(123) Event(124) Event(122)",
              $"{Ev(1, walls, QC.Npc.WindurstWalls.ChawoShipeynyo)} {Ev(1, walls, QC.Npc.WindurstWalls.KeoKoruo)} " +
              $"{Ev(1, walls, QC.Npc.WindurstWalls.PakkePokke)} {Ev(1, walls, QC.Npc.WindurstWalls.ZokimaRokima)}");
        Check("c3 n1 lost for words: section 2: Windurst Waters' NPCs play 167, 157, 165, 166, 156, 158, 159",
              "Event(167) Event(157) Event(165) Event(166) Event(156) Event(158) Event(159)",
              $"{Ev(1, waters, QC.Npc.WindurstWaters.AoraUora)} {Ev(1, waters, QC.Npc.WindurstWaters.DagozaBeruza)} " +
              $"{Ev(1, waters, QC.Npc.WindurstWaters.FurakkuNorakku)} " +
              $"{Ev(1, waters, QC.Npc.WindurstWaters.KotanMoritan)} {Ev(1, waters, QC.Npc.WindurstWaters.Mokyokyo)} " +
              $"{Ev(1, waters, QC.Npc.WindurstWaters.PannaDonna)} {Ev(1, waters, QC.Npc.WindurstWaters.TenofHearts)}");
        Check("c3 n1 lost for words: section 2: Windurst Woods' NPCs play 161, 160, 162",
              "Event(161) Event(160) Event(162)",
              $"{Ev(1, woods, QC.Npc.WindurstWoods.MiiriWohri)} {Ev(1, woods, QC.Npc.WindurstWoods.RakohBuuma)} " +
              Ev(1, woods, QC.Npc.WindurstWoods.SolaJaab));
        Check("c3 n1 lost for words: section 2: Tih Pikeh plays progress event 163 while the toggle is 0, else 164",
              "ProgressEvent(163) ProgressEvent(164) ProgressEvent(164)",
              $"{Ev(1, woods, QC.Npc.WindurstWoods.TihPikeh)} {Ev(1, woods, QC.Npc.WindurstWoods.TihPikeh, tih: 1)} " +
              Ev(1, woods, QC.Npc.WindurstWoods.TihPikeh, tih: 2));
        Check("c3 n1 lost for words: Tih Pikeh's finishes toggle: 163 sets the toggle to 1, 164 sets it to 0",
              "Player.SetLocalVar(TihPikehRepeatLine,1)|Player.SetLocalVar(TihPikehRepeatLine,0)",
              $"{C2MRun(C2MHandler(d, 1, fin, woods, id: 163), F(id))}|" +
              C2MRun(C2MHandler(d, 1, fin, woods, id: 164), F(id, tih: 1)));

        Check("c3 n1 lost for words: status 1: Tosuka-Porika progress event 160; its finish sets status 2",
              $"ProgressEvent(160) Player.SetMissionStatus({log},2)",
              $"{Ev(2, waters, QC.Npc.WindurstWaters.TosukaPorika, 1)} " +
              C2MRun(C2MHandler(d, 2, fin, waters, id: 160), F(id, 1)));

        string Lapis = $"ProgressEvent(165,0,{coral},{monocle})";
        Check("c3 n1 lost for words: status 2: Nanaa Mihgo progress event 165 with the Lapis Coral and Monocle; " +
              "Tosuka-Porika 161",
              $"{Lapis} Event(161)",
              $"{Ev(3, woods, QC.Npc.WindurstWoods.NanaaMihgo, 2)} " +
              $"{Ev(3, waters, QC.Npc.WindurstWaters.TosukaPorika, 2)}");
        foreach (int roll in new[] { 0, 3, 7 })
        {
            var f = F(id, 2, option: 1);
            f.Rolls.Enqueue(roll);
            string ran = C2MRun(C2MHandler(d, 3, fin, woods, id: 165), f);
            Check($"c3 n1 lost for words: 165's finish with option 1 gives the Lapis Monocle, picks the rock " +
                  $"({roll}) and sets status 3",
                  $"Player.GiveKeyItem({monocle}),Player.RandomInt(0,7),Mission.SetVar(Rock,{roll}),Player." +
                  $"SetMissionStatus({log},3) True 3 {roll}",
                  $"{ran} {f.HasKeyItem(monocle)} {f.MissionStatus(log)} {f.MissionVars[K("Rock")]}");
        }
        foreach (int option in new[] { 0, 2, 3 })
        {
            var f = F(id, 2, option: option);
            f.Rolls.Enqueue(5);
            Check($"c3 n1 lost for words: 165's finish with option {option} does nothing",
                  "|False 2 0", $"{C2MRun(C2MHandler(d, 3, fin, woods, id: 165), f)}|{f.HasKeyItem(monocle)} " +
                                $"{f.MissionStatus(log)} {f.MissionVars[K("Rock")]}");
        }

        Check("c3 n1 lost for words: status 3: Bopa Greso 167, Cha Lebagta 168, Nanaa Mihgo 166 with the Lapis keys",
              $"Event(167) Event(168) Event(166,0,{coral},{monocle})",
              $"{Ev(4, woods, QC.Npc.WindurstWoods.BopaGreso, 3)} {Ev(4, woods, QC.Npc.WindurstWoods.ChaLebagta, 3)} " +
              Ev(4, woods, QC.Npc.WindurstWoods.NanaaMihgo, 3));

        const int rockBase = 17000000;
        string Rock(int status, int rock, int offset, out Fake f)
        {
            f = F(id, status, rock: rock);
            f.NpcIds["FOSSIL_ROCK_OFFSET"] = rockBase;
            f.Npc = new QuestNpcFake(rockBase + offset);
            return C2MReply(C2MHandler(d, 5, trig, maze, QC.Npc.MazeOfShakhrami.FossilRock), f);
        }
        string RS(Fake f) => $"{f.MissionStatus(log)}";
        Fake r, r2, r3, r4, r5, r6;
        string kiCoral = $"KeyItem({coral})";
        Check("c3 n1 lost for words: the right rock at status 3 gives the Lapis Coral and sets status 4, at any rock",
              $"{kiCoral} 4|{kiCoral} 4|{kiCoral} 4",
              $"{Rock(3, 0, 0, out r)} {RS(r)}|{Rock(3, 3, 3, out r2)} {RS(r2)}|{Rock(3, 7, 7, out r3)} {RS(r3)}");
        Check("c3 n1 lost for words: the right rock at status 4 says NO_NEED_INVESTIGATE and keeps status 4",
              $"MessageSpecial({QC.Text.MazeOfShakhrami.NO_NEED_INVESTIGATE}) 4",
              $"{Rock(4, 3, 3, out r)} {RS(r)}");
        string jar = $"MessageSpecial({QC.Text.MazeOfShakhrami.JUST_A_ROCK})";
        Check("c3 n1 lost for words: a wrong rock of the first eight says JUST_A_ROCK, at status 3 and 4",
              $"{jar} 3|{jar} 4|{jar} 3|{jar} 3",
              $"{Rock(3, 3, 0, out r)} {RS(r)}|{Rock(4, 3, 7, out r4)} {RS(r4)}|" +
              $"{Rock(3, 0, 1, out r5)} {RS(r5)}|{Rock(3, 6, 7, out r6)} {RS(r6)}");
        string nof = $"MessageSpecial({QC.Text.MazeOfShakhrami.NOTHING_FOSSIL})";
        Check("c3 n1 lost for words: the rocks past the eighth say NOTHING_FOSSIL, at status 3 and 4",
              $"{nof} 3|{nof} 4|{nof} 3",
              $"{Rock(3, 3, 8, out r)} {RS(r)}|{Rock(4, 3, 13, out r4)} {RS(r4)}|{Rock(3, 0, 20, out r5)} {RS(r5)}");
        Check("c3 n1 lost for words: the right rock at a status other than 3 and 4 gives nothing and changes nothing",
              "null 5|null 2",
              $"{Rock(5, 3, 3, out r)} {RS(r)}|{Rock(2, 3, 3, out r2)} {RS(r2)}");

        Check("c3 n1 lost for words: status 4: Nanaa Mihgo progress event 169",
              "ProgressEvent(169)", Ev(6, woods, QC.Npc.WindurstWoods.NanaaMihgo, 4));
        var back = F(id, 4, rock: 5);
        back.KeyItems[coral] = true;
        back.KeyItems[monocle] = true;
        Check("c3 n1 lost for words: 169's finish takes the Lapis Coral and Monocle, clears the rock, gives the " +
              "Hideout Key and sets status 5",
              $"Player.DelKeyItem({coral}),Player.DelKeyItem({monocle}),Mission.SetVar(Rock,0),Player.GiveKeyItem(" +
              $"{hideout}),Player.SetMissionStatus({log},5) False False 0 True 5",
              $"{C2MRun(C2MHandler(d, 6, fin, woods, id: 169), back)} {back.HasKeyItem(coral)} " +
              $"{back.HasKeyItem(monocle)} {back.MissionVars[K("Rock")]} {back.HasKeyItem(hideout)} " +
              back.MissionStatus(log));

        Check("c3 n1 lost for words: status 5: Bopa Greso 171, Cha Lebagta 172, Nanaa Mihgo 170, the door progress " +
              "cutscene 46",
              "Event(171) Event(172) Event(170) ProgressCutscene(46)",
              $"{Ev(7, woods, QC.Npc.WindurstWoods.BopaGreso, 5)} {Ev(7, woods, QC.Npc.WindurstWoods.ChaLebagta, 5)} " +
              $"{Ev(7, woods, QC.Npc.WindurstWoods.NanaaMihgo, 5)} " +
              Ev(7, QC.Zone.InnerHorutotoRuins, QC.Npc.InnerHorutotoRuins._5ca, 5));
        var door = F(id, 5);
        door.KeyItems[hideout] = true;
        Check("c3 n1 lost for words: 46's finish takes the Hideout Key and sets status 6",
              $"Player.DelKeyItem({hideout}),Player.SetMissionStatus({log},6) False 6",
              $"{C2MRun(C2MHandler(d, 7, fin, QC.Zone.InnerHorutotoRuins, id: 46), door)} " +
              $"{door.HasKeyItem(hideout)} {door.MissionStatus(log)}");

        Check("c3 n1 lost for words: status 6: Bopa Greso 174, Cha Lebagta 175, Nanaa Mihgo 173",
              "Event(174) Event(175) Event(173)",
              $"{Ev(8, woods, QC.Npc.WindurstWoods.BopaGreso, 6)} {Ev(8, woods, QC.Npc.WindurstWoods.ChaLebagta, 6)} " +
              Ev(8, woods, QC.Npc.WindurstWoods.NanaaMihgo, 6));
        var hero = F(id, 6);
        string heroReply = C2MReply(C2MHandler(d, 8, trig, walls, QC.Npc.WindurstWalls._6n2), hero);
        Check("c3 n1 lost for words: status 6: the House of the Hero door says DOOR_UNLOCKED (no name, mode 6) and " +
              "plays progress event 337; 337's finish sets status 7",
              $"Text.MessageText({QC.Text.WindurstWalls.DOOR_UNLOCKED},False,6) ProgressEvent(337) " +
              $"Player.SetMissionStatus({log},7)",
              $"{string.Join(",", hero.Calls)} {heroReply} " +
              C2MRun(C2MHandler(d, 8, fin, walls, id: 337), F(id, 6)));

        var failed = F(id, 7);
        failed.CompleteResult = false;
        Check("c3 n1 lost for words: status 7: Tosuka-Porika progress event 168; its finish completes the mission, " +
              "whatever the result",
              "ProgressEvent(168) Mission.Complete()|Mission.Complete()",
              $"{Ev(9, waters, QC.Npc.WindurstWaters.TosukaPorika, 7)} " +
              $"{C2MRun(C2MHandler(d, 9, fin, waters, id: 168), F(id, 7))}|" +
              C2MRun(C2MHandler(d, 9, fin, waters, id: 168), failed));

        string Post(ushort zone, string npc) => C2MReply(C2MHandler(d, 10, trig, zone, npc), F(none, done: true));
        Check("c3 n1 lost for words: last section: Port Windurst 74, 81, 80; Walls 329, 88, 94",
              "Event(74) Event(81) Event(80) Event(329) Event(88) Event(94)",
              $"{Post(port, QC.Npc.PortWindurst.NineofClubs)} {Post(port, QC.Npc.PortWindurst.PuoRhen)} " +
              $"{Post(port, QC.Npc.PortWindurst.TenofClubs)} {Post(walls, QC.Npc.WindurstWalls.ChawoShipeynyo)} " +
              $"{Post(walls, QC.Npc.WindurstWalls.KeoKoruo)} {Post(walls, QC.Npc.WindurstWalls.PakkePokke)}");
        Check("c3 n1 lost for words: last section: Waters 175, 115, 173, 174, 116, 114, 169",
              "Event(175) Event(115) Event(173) Event(174) Event(116) Event(114) Event(169)",
              $"{Post(waters, QC.Npc.WindurstWaters.AoraUora)} {Post(waters, QC.Npc.WindurstWaters.DagozaBeruza)} " +
              $"{Post(waters, QC.Npc.WindurstWaters.FurakkuNorakku)} " +
              $"{Post(waters, QC.Npc.WindurstWaters.KotanMoritan)} {Post(waters, QC.Npc.WindurstWaters.PannaDonna)} " +
              $"{Post(waters, QC.Npc.WindurstWaters.TenofHearts)} {Post(waters, QC.Npc.WindurstWaters.TosukaPorika)}");
        Check("c3 n1 lost for words: last section: Woods 118, 117, 119",
              "Event(118) Event(117) Event(119)",
              $"{Post(woods, QC.Npc.WindurstWoods.MiiriWohri)} {Post(woods, QC.Npc.WindurstWoods.SolaJaab)} " +
              Post(woods, QC.Npc.WindurstWoods.TihPikeh));
    }

    static void RunC3N1ATestingTime()
    {
        var m = new QC.Missions.Windurst.ATestingTime();
        var d = m.Compile();
        int log = QC.MissionLog.Windurst, id = QC.Mission.Windurst.ATestingTime;
        const int doll = QC.KeyItem.CreatureCounterMagicDoll;
        Check("c3 n1 a testing time: clean, its key and LegacyPath, 300 rank points, no gil, no rank",
              $"|{QC.MissionKey.Windurst.ATestingTime}|scripts/missions/windurst/2_2_A_Testing_Time|//300",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|" +
              $"{d.Reward.Gil}/{d.Reward.Rank}/{d.Reward.RankPoints}");
        const int none = QC.Mission.Windurst.None;
        string K(string name) => $"{log}/{id}/{name}";
        Fake F(int current, int status = 0, int nation = 2, bool done = false, int option = 0, int end = 0,
               int kills = 0, int failed = 0, int now = 0, int hour = 0, bool mustZone = false)
        {
            var f = new Fake { ThisMission = (MissionLog.Windurst, id), NationId = nation, Option = option };
            f.CurrentMissions[log] = current;
            f.MissionStatuses[log] = status;
            if (done) f.CompletedMissions.Add($"{log}/{id}");
            f.MissionVars[K("EndTime")] = end;
            f.MissionVars[K("KillCount")] = kills;
            f.MissionVars[K("Failed")] = failed;
            f.VanaTimeNow = now;
            f.Hour = hour;
            f.MissionMustZone = mustZone;
            return f;
        }
        string Hold(int state, params Fake[] fakes) =>
            string.Join(" ", fakes.Select(f => d.States[state].IsMissionMatch(f)));
        Check("c3 n1 a testing time: section 1 needs no current mission and a Windurst citizen, done or not",
              "True False False True",
              Hold(0, F(none), F(none, nation: 1), F(id), F(none, done: true)));
        Check("c3 n1 a testing time: section 2 holds while 2-2 is current, at any status", "True True True False",
              Hold(1, F(id), F(id, status: 1), F(id, status: 2), F(none)));
        Check("c3 n1 a testing time: status 1's section holds at 1 only while current", "True False False False",
              Hold(2, F(id, status: 1), F(id, status: 2), F(id), F(none, status: 1)));
        Check("c3 n1 a testing time: the Tahrongi section holds at status 2 while 2-2 is not completed",
              "True False False False False",
              Hold(3, F(id, status: 2), F(id, status: 2, done: true), F(id, status: 1), F(id, status: 3),
                   F(none, status: 2)));
        Check("c3 n1 a testing time: the Buburimu section holds at status 2 once 2-2 is completed",
              "True False False False False",
              Hold(4, F(id, status: 2, done: true), F(id, status: 2), F(id, status: 1, done: true),
                   F(id, status: 3, done: true), F(none, status: 2, done: true)));
        Check("c3 n1 a testing time: the assessment section holds at status 2 only while current",
              "True False False False", Hold(5, F(id, status: 2), F(id, status: 1), F(id, status: 3),
                                             F(none, status: 2)));
        Check("c3 n1 a testing time: the parting section needs no mission, Failed 1 and must-zone",
              "True False False False False",
              Hold(6, F(none, failed: 1, mustZone: true), F(none, failed: 1), F(none, mustZone: true),
                   F(id, failed: 1, mustZone: true), F(none, failed: 2, mustZone: true)));
        Check("c3 n1 a testing time: the usual-line section needs no mission, Failed 1 and no must-zone",
              "True False False False False",
              Hold(7, F(none, failed: 1), F(none, failed: 1, mustZone: true), F(none), F(id, failed: 1),
                   F(none, failed: 2)));

        var fin = QuestHandlerKind.OnEventFinish;
        var trig = QuestHandlerKind.OnTrigger;
        var death = QuestHandlerKind.OnMobDeath;
        const ushort port = QC.Zone.PortWindurst, walls = QC.Zone.WindurstWalls, waters = QC.Zone.WindurstWaters;
        const ushort woods = QC.Zone.WindurstWoods;
        foreach (var (zone, csid, text, label) in new[]
                 {
                     (port, 78, QC.Text.PortWindurst.YOU_ACCEPT_THE_MISSION, "Port Windurst"),
                     (walls, 93, QC.Text.WindurstWalls.YOU_ACCEPT_THE_MISSION, "Windurst Walls"),
                     (waters, 111, QC.Text.WindurstWaters.YOU_ACCEPT_THE_MISSION, "Windurst Waters"),
                     (woods, 114, QC.Text.WindurstWoods.YOU_ACCEPT_THE_MISSION, "Windurst Woods"),
                 })
        {
            var h = C2MHandler(d, 0, fin, zone, id: csid);
            Check($"c3 n1 a testing time: {label} {csid}'s finish: option 4 begins, sets status 1, sets " +
                  "AurasteryMissionTaken and says YOU_ACCEPT_THE_MISSION; options 3, 2 and 0 do nothing",
                  $"Mission.Begin(),Player.SetMissionStatus({log},1),Player.SetCharVar(AurasteryMissionTaken,1,0)," +
                  $"Text.Special({text})|||",
                  $"{C2MRun(h, F(none, option: 4))}|{C2MRun(h, F(none, option: 3))}|" +
                  $"{C2MRun(h, F(none, option: 2))}|{C2MRun(h, F(none))}");
        }

        string Ev(int state, ushort zone, string npc) => C2MReply(C2MHandler(d, state, trig, zone, npc), F(id));
        Check("c3 n1 a testing time: section 2: Port Windurst's NPCs play 133, 134, 136, 135",
              "Event(133) Event(134) Event(136) Event(135)",
              $"{Ev(1, port, QC.Npc.PortWindurst.JanshuraRashura)} {Ev(1, port, QC.Npc.PortWindurst.NineofClubs)} " +
              $"{Ev(1, port, QC.Npc.PortWindurst.PuoRhen)} {Ev(1, port, QC.Npc.PortWindurst.TenofClubs)}");
        Check("c3 n1 a testing time: section 2: Windurst Walls' NPCs play 130, 128, 129, 127",
              "Event(130) Event(128) Event(129) Event(127)",
              $"{Ev(1, walls, QC.Npc.WindurstWalls.ChawoShipeynyo)} {Ev(1, walls, QC.Npc.WindurstWalls.KeoKoruo)} " +
              $"{Ev(1, walls, QC.Npc.WindurstWalls.PakkePokke)} {Ev(1, walls, QC.Npc.WindurstWalls.ZokimaRokima)}");
        Check("c3 n1 a testing time: section 2: Windurst Waters' NPCs play 190, 195, 178, 193, 186, 188, 191, 192, " +
              "196, 177, 194, 179, 185, 189, 187, 180",
              "Event(190) Event(195) Event(178) Event(193) Event(186) Event(188) Event(191) Event(192) Event(196) " +
              "Event(177) Event(194) Event(179) Event(185) Event(189) Event(187) Event(180)",
              string.Join(" ", new[]
              {
                  QC.Npc.WindurstWaters.Akkeke, QC.Npc.WindurstWaters.ChomoroKyotoro,
                  QC.Npc.WindurstWaters.DagozaBeruza, QC.Npc.WindurstWaters.FoiMui, QC.Npc.WindurstWaters.Fuepepe,
                  QC.Npc.WindurstWaters.Kirarara, QC.Npc.WindurstWaters.KokoLihzeh,
                  QC.Npc.WindurstWaters.MashuuAjuu, QC.Npc.WindurstWaters.Mimomo, QC.Npc.WindurstWaters.Mokyokyo,
                  QC.Npc.WindurstWaters.PakuNakku, QC.Npc.WindurstWaters.PannaDonna,
                  QC.Npc.WindurstWaters.PechiruMashiru, QC.Npc.WindurstWaters.Rukuku,
                  QC.Npc.WindurstWaters.Tauwawa, QC.Npc.WindurstWaters.TenofHearts
              }.Select(n => Ev(1, waters, n))));
        Check("c3 n1 a testing time: section 2: Windurst Woods' NPCs play 474, 473, 475, 476",
              "Event(474) Event(473) Event(475) Event(476)",
              $"{Ev(1, woods, QC.Npc.WindurstWoods.MiiriWohri)} {Ev(1, woods, QC.Npc.WindurstWoods.RakohBuuma)} " +
              $"{Ev(1, woods, QC.Npc.WindurstWoods.SolaJaab)} {Ev(1, woods, QC.Npc.WindurstWoods.TihPikeh)}");

        // status 1: Moreno-Toeno
        var moreno1 = C2MHandler(d, 2, trig, waters, QC.Npc.WindurstWaters.MorenoToeno);
        Check("c3 n1 a testing time: status 1: Moreno-Toeno plays 687 once 2-2 is completed (whatever Failed is), " +
              "else 181 after a failed run, else 182",
              "ProgressEvent(687) ProgressEvent(687) ProgressEvent(181) ProgressEvent(182) ProgressEvent(182)",
              string.Join(" ", new[]
              {
                  F(id, status: 1, done: true), F(id, status: 1, done: true, failed: 1), F(id, status: 1, failed: 1),
                  F(id, status: 1), F(id, status: 1, failed: 2)
              }.Select(f => C2MReply(moreno1, f))));
        foreach (int csid in new[] { 181, 182 })
        {
            var h = C2MHandler(d, 2, fin, waters, id: csid);
            var f2 = F(id, status: 1, option: 2, now: 1000, failed: 1);
            string run2 = C2MRun(h, f2);
            Check($"c3 n1 a testing time: status 1: {csid}'s finish: option 2 sets status 2, gives the doll, sets " +
                  "EndTime a day ahead and clears Failed; option 1 and 0 do nothing",
                  $"Player.SetMissionStatus({log},2),Player.GiveKeyItem({doll}),Mission.SetVar(EndTime,4456)," +
                  "Mission.SetVar(Failed,0) 2 4456 0||",
                  $"{run2} {f2.MissionStatus(log)} {f2.MissionVar(log, id, "EndTime")} " +
                  $"{f2.MissionVar(log, id, "Failed")}|{C2MRun(h, F(id, status: 1, option: 1, now: 1000))}|" +
                  C2MRun(h, F(id, status: 1, now: 1000)));
        }
        var h687 = C2MHandler(d, 2, fin, waters, id: 687);
        var f687 = F(id, status: 1, option: 2, now: 1000, failed: 1, done: true);
        string run687 = C2MRun(h687, f687);
        Check("c3 n1 a testing time: status 1: 687's finish: option 2 sets status 2, gives the doll and sets EndTime " +
              "two days ahead, leaving Failed; option 1 and 0 do nothing",
              $"Player.SetMissionStatus({log},2),Player.GiveKeyItem({doll}),Mission.SetVar(EndTime,7912) 2 7912 1||",
              $"{run687} {f687.MissionStatus(log)} {f687.MissionVar(log, id, "EndTime")} " +
              $"{f687.MissionVar(log, id, "Failed")}|" +
              $"{C2MRun(h687, F(id, status: 1, option: 1, now: 1000, done: true))}|" +
              C2MRun(h687, F(id, status: 1, now: 1000, done: true)));

        // the kill sections
        string[] tahrongi =
        {
            QC.Npc.TahrongiCanyon.AirElemental, QC.Npc.TahrongiCanyon.Akbaba, QC.Npc.TahrongiCanyon.Barghest,
            QC.Npc.TahrongiCanyon.CanyonCrawler, QC.Npc.TahrongiCanyon.CanyonRarab,
            QC.Npc.TahrongiCanyon.EarthElemental, QC.Npc.TahrongiCanyon.Ghost,
            QC.Npc.TahrongiCanyon.GoblinAmbusher, QC.Npc.TahrongiCanyon.GoblinButcher,
            QC.Npc.TahrongiCanyon.GoblinDigger, QC.Npc.TahrongiCanyon.GoblinThug,
            QC.Npc.TahrongiCanyon.GoblinTinkerer, QC.Npc.TahrongiCanyon.GoblinWeaver,
            QC.Npc.TahrongiCanyon.Grenade, QC.Npc.TahrongiCanyon.Habrok, QC.Npc.TahrongiCanyon.HerbageHunter,
            QC.Npc.TahrongiCanyon.KillerBee, QC.Npc.TahrongiCanyon.Poltergeist, QC.Npc.TahrongiCanyon.Pygmaioi,
            QC.Npc.TahrongiCanyon.SerpopardIshtar, QC.Npc.TahrongiCanyon.SkeletonSorcerer,
            QC.Npc.TahrongiCanyon.SkeletonWarrior, QC.Npc.TahrongiCanyon.StrollingSapling,
            QC.Npc.TahrongiCanyon.WildDhalmel, QC.Npc.TahrongiCanyon.YagudoAcolyte,
            QC.Npc.TahrongiCanyon.YagudoInitiate, QC.Npc.TahrongiCanyon.YagudoMendicant,
            QC.Npc.TahrongiCanyon.YagudoPersecutor, QC.Npc.TahrongiCanyon.YagudoPiper,
            QC.Npc.TahrongiCanyon.YagudoScribe, QC.Npc.TahrongiCanyon.YaraMaYhaWho
        };
        string[] buburimu =
        {
            QC.Npc.BuburimuPeninsula.AirElemental, QC.Npc.BuburimuPeninsula.Backoo, QC.Npc.BuburimuPeninsula.Bogy,
            QC.Npc.BuburimuPeninsula.Buburimboo, QC.Npc.BuburimuPeninsula.BullDhalmel,
            QC.Npc.BuburimuPeninsula.CarnivorousCrawler, QC.Npc.BuburimuPeninsula.Ghoul,
            QC.Npc.BuburimuPeninsula.GoblinAmbusher, QC.Npc.BuburimuPeninsula.GoblinBountyHunter,
            QC.Npc.BuburimuPeninsula.GoblinButcher, QC.Npc.BuburimuPeninsula.GoblinDigger,
            QC.Npc.BuburimuPeninsula.GoblinGambler, QC.Npc.BuburimuPeninsula.GoblinLeecher,
            QC.Npc.BuburimuPeninsula.GoblinMugger, QC.Npc.BuburimuPeninsula.GoblinTinkerer,
            QC.Npc.BuburimuPeninsula.Helldiver, QC.Npc.BuburimuPeninsula.MightyRarab,
            QC.Npc.BuburimuPeninsula.PoisonLeech, QC.Npc.BuburimuPeninsula.ShoalPugil,
            QC.Npc.BuburimuPeninsula.Snipper, QC.Npc.BuburimuPeninsula.Sylvestre,
            QC.Npc.BuburimuPeninsula.WakeWarderWanda, QC.Npc.BuburimuPeninsula.WaterElemental,
            QC.Npc.BuburimuPeninsula.WillotheWisp, QC.Npc.BuburimuPeninsula.Zombie, QC.Npc.BuburimuPeninsula.Zu
        };
        Check("c3 n1 a testing time: the Tahrongi section has the 31 mobs and the Buburimu section the 26 mobs",
              "31 31 26 26",
              $"{d.States[3].Handlers.Count(h => h.Kind == death)} " +
              $"{tahrongi.Count(n => d.States[3].Handlers.Any(h => h.Kind == death && h.Entity == n))} " +
              $"{d.States[4].Handlers.Count(h => h.Kind == death)} " +
              $"{buburimu.Count(n => d.States[4].Handlers.Any(h => h.Kind == death && h.Entity == n))}");
        string Kill(int state, ushort zone, string mob, int kills)
        {
            var f = F(id, status: 2, kills: kills);
            return C2MRun(C2MHandler(d, state, death, zone, mob), f);
        }
        Check("c3 n1 a testing time: every Tahrongi mob's death adds one to KillCount",
              string.Join(" ", tahrongi.Select(_ => "Mission.SetVar(KillCount,5)")),
              string.Join(" ", tahrongi.Select(n => Kill(3, QC.Zone.TahrongiCanyon, n, 4))));
        Check("c3 n1 a testing time: every Buburimu mob's death adds one to KillCount",
              string.Join(" ", buburimu.Select(_ => "Mission.SetVar(KillCount,5)")),
              string.Join(" ", buburimu.Select(n => Kill(4, QC.Zone.BuburimuPeninsula, n, 4))));
        Check("c3 n1 a testing time: a death adds to the count it finds, from 0 and from 34",
              "Mission.SetVar(KillCount,1) Mission.SetVar(KillCount,35)",
              $"{Kill(3, QC.Zone.TahrongiCanyon, tahrongi[0], 0)} " +
              Kill(4, QC.Zone.BuburimuPeninsula, buburimu[0], 34));

        // the assessment
        var assess = C2MHandler(d, 5, trig, waters, QC.Npc.WindurstWaters.MorenoToeno);
        string As(int now, int end, int kills, bool done = false) =>
            C2MReply(assess, F(id, status: 2, done: done, end: end, kills: kills, now: now, hour: 7));
        Check("c3 n1 a testing time: after EndTime Moreno plays 202, done or not, whatever the kills",
              "ProgressEvent(202) ProgressEvent(202)", $"{As(5001, 5000, 40)} {As(5001, 5000, 40, true)}");
        Check("c3 n1 a testing time: at EndTime and an hour before it the kills decide (35: 201, done 206)",
              "ProgressEvent(201,0,7,1,40) ProgressEvent(206,0,7,1,40) ProgressEvent(201,0,7,1,40) " +
              "ProgressEvent(206,0,7,1,40)",
              $"{As(5000, 5000, 40)} {As(5000, 5000, 40, true)} {As(4856, 5000, 40)} {As(4856, 5000, 40, true)}");
        Check("c3 n1 a testing time: more than an hour before EndTime he plays 183 (204 done) with the hour and the " +
              "hours left, rounded up",
              "ProgressEvent(183,0,7,2) ProgressEvent(204,0,0,0,0,0,7,2,0) ProgressEvent(183,0,7,2) " +
              "ProgressEvent(183,0,7,3) ProgressEvent(204,0,0,0,0,0,7,3,0)",
              $"{As(4855, 5000, 40)} {As(4855, 5000, 40, true)} {As(4712, 5000, 40)} {As(4711, 5000, 40)} " +
              As(4711, 5000, 40, true));
        string KC(bool done, params int[] ks) => string.Join(" ", ks.Select(k => As(5000, 5000, k, done)
            .Replace("ProgressEvent(", "").Replace(",0,7,1," + k + ")", "")));
        Check("c3 n1 a testing time: the kills pick, first run: 0, 18 198; 19, 29 199; 30, 34 200; 35 201",
              "198 198 199 199 200 200 201", KC(false, 0, 18, 19, 29, 30, 34, 35));
        Check("c3 n1 a testing time: the kills pick, a repeat: 0, 19, 29: 208; 30, 34: 209; 35: 206",
              "208 208 208 209 209 206", KC(true, 0, 19, 29, 30, 34, 35));
        Check("c3 n1 a testing time: the assessment hands the kill count to the event",
              "ProgressEvent(199,0,7,1,25)", As(5000, 5000, 25));

        string failFirst = "Mission.SetVar(EndTime,0),Mission.SetVar(KillCount,0),Mission.SetVar(Failed,1)," +
                           $"Mission.SetMustZone(),Player.DelKeyItem({doll}),Player.SetMissionStatus({log},0)";
        foreach (int csid in new[] { 198, 199, 202, 208 })
        {
            var h = C2MHandler(d, 5, fin, waters, id: csid);
            Check($"c3 n1 a testing time: {csid}'s finish fails the run: first run deletes the mission, a repeat " +
                  "completes it raw",
                  $"{failFirst},Player.DelMission({log},{id})|{failFirst},Mission.CompleteRaw()",
                  $"{C2MRun(h, F(id, status: 2))}|{C2MRun(h, F(id, status: 2, done: true))}");
        }
        foreach (int csid in new[] { 200, 201, 206, 209 })
        {
            var h = C2MHandler(d, 5, fin, waters, id: csid);
            var lost = F(id, status: 2);
            lost.CompleteResult = false;
            Check($"c3 n1 a testing time: {csid}'s finish completes the mission and takes the doll, only when it " +
                  "completed",
                  $"Mission.Complete(),Player.DelKeyItem({doll})|Mission.Complete()",
                  $"{C2MRun(h, F(id, status: 2))}|{C2MRun(h, lost)}");
        }

        const string moreno = QC.Npc.WindurstWaters.MorenoToeno;
        Check("c3 n1 a testing time: parting words: Moreno-Toeno plays 203 while must-zone holds, 438 after",
              "Event(203) Event(438)",
              $"{C2MReply(C2MHandler(d, 6, trig, waters, moreno), F(none, failed: 1, mustZone: true))} " +
              C2MReply(C2MHandler(d, 7, trig, waters, moreno), F(none, failed: 1)));

    }
}
