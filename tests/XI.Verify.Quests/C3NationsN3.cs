// XI.Verify.Quests -- quest rework C3 N3 (docs/superpowers/plans/2026-11-07-quest-rework-c3-lanes-1.md, section 4,
// Nations): the rank 2-3 / rank 3 missions on C#, one fake check per arm no packet shows, as the Lua files had them:
// each section check on its operands, each reply on the reads it branches on, each finish on its option and the
// items it reads.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    internal static void RunC3N3()
    {
        Console.WriteLine("\n== c3 N3: the rank 2-3 / rank 3 missions on C# ==");
        RunC3N3ShowText();
        RunC3N3TheThreeKingdoms();
        RunC3N3TheThreeKingdomsSandoria();
        RunC3N3TheThreeKingdomsBastok();
        RunC3N3TheThreeKingdomsBastok2();
        RunC3N3ToEachHisOwnRight();
        RunC3N3WrittenInTheStars();
        RunC3N3ANewJourney();
        RunC3N3TheFourMusketeers();
        RunC3N3ToTheForsakenMines();
        RunC3N3Jeuno();
        RunC3N3InfiltrateDavoi();
        RunC3N3TheCrystalSpring();
        RunC3N3AppointmentToJeuno();
    }

    static readonly QuestHandlerKind N3Fin = QuestHandlerKind.OnEventFinish;
    static readonly QuestHandlerKind N3Trig = QuestHandlerKind.OnTrigger;
    static readonly QuestHandlerKind N3Trade = QuestHandlerKind.OnTrade;

    // a fake on one mission of a log: current mission, status, nation, option, completed missions of that log
    static Fake N3Fake(MissionLog log, int id, int current, int status = 0, int nation = -1, int option = 0,
                       params int[] done)
    {
        int logId = (int)log;
        var f = new Fake { ThisMission = (log, id), NationId = nation < 0 ? logId : nation, Option = option };
        f.CurrentMissions[logId] = current;
        f.MissionStatuses[logId] = status;
        foreach (int mission in done) f.CompletedMissions.Add($"{logId}/{mission}");
        return f;
    }

    // a handler run on a fake: its calls, then (when given) the status it left
    static string N3Run(QuestHandlerDecl h, Fake f) => C2MRun(h, f);

    // (C3 N3 facet) IQuestPlayer.ShowText(int textId, args): the fake records it with the id first
    static void RunC3N3ShowText()
    {
        var f = new Fake();
        ((IQuestContext)f).Player.ShowText(7300, 1, 2);
        var g = new Fake();
        ((IQuestContext)g).Player.ShowText(7320);
        Check("c3 N3 facets: ShowText(int, args) records Player.ShowText(<id>,<args>); no args: the id alone",
              "Player.ShowText(7300,1,2)|Player.ShowText(7320)",
              $"{string.Join(",", f.Calls)}|{string.Join(",", g.Calls)}");
    }

    // the Windurst gate guards' accept finishes: option <opt> begins with the zone's YOU_ACCEPT_THE_MISSION; the
    // options either side and 0 do nothing; extra: the calls after the message (A New Journey's summons)
    static void N3WindurstAccept(string label, MissionScriptDefinition d, int id, int opt, string extra = "")
    {
        foreach (var (zone, csid, text) in new[]
                 {
                     (QC.Zone.PortWindurst, 78, QC.Text.PortWindurst.YOU_ACCEPT_THE_MISSION),
                     (QC.Zone.WindurstWalls, 93, QC.Text.WindurstWalls.YOU_ACCEPT_THE_MISSION),
                     (QC.Zone.WindurstWaters, 111, QC.Text.WindurstWaters.YOU_ACCEPT_THE_MISSION),
                     (QC.Zone.WindurstWoods, 114, QC.Text.WindurstWoods.YOU_ACCEPT_THE_MISSION),
                 })
        {
            var h = C2MHandler(d, 0, N3Fin, zone, id: csid);
            Fake F(int option) => N3Fake(MissionLog.Windurst, id, QC.Mission.Windurst.None, option: option);
            Check($"c3 N3 {label}: zone {zone}'s {csid} finish: option {opt} begins with its zone's " +
                  $"YOU_ACCEPT_THE_MISSION{(extra.Length > 0 ? " and " + extra : "")}; options {opt - 1}, " +
                  $"{opt + 1} and 0 do nothing",
                  $"Mission.Begin(),Text.Special({text}){extra}|||",
                  $"{C2MRun(h, F(opt))}|{C2MRun(h, F(opt - 1))}|{C2MRun(h, F(opt + 1))}|{C2MRun(h, F(0))}");
        }
    }

    // the Bastok gate guards' 1001 finishes, as N3WindurstAccept
    static void N3BastokAccept(string label, MissionScriptDefinition d, int id, int opt)
    {
        foreach (var (zone, text) in new[]
                 {
                     (QC.Zone.BastokMarkets, QC.Text.BastokMarkets.YOU_ACCEPT_THE_MISSION),
                     (QC.Zone.BastokMines, QC.Text.BastokMines.YOU_ACCEPT_THE_MISSION),
                     (QC.Zone.Metalworks, QC.Text.Metalworks.YOU_ACCEPT_THE_MISSION),
                     (QC.Zone.PortBastok, QC.Text.PortBastok.YOU_ACCEPT_THE_MISSION),
                 })
        {
            var h = C2MHandler(d, 0, N3Fin, zone, id: 1001);
            Fake F(int option) => N3Fake(MissionLog.Bastok, id, QC.Mission.Bastok.None, option: option);
            Check($"c3 N3 {label}: zone {zone}'s 1001 finish: option {opt} begins with its zone's " +
                  $"YOU_ACCEPT_THE_MISSION; options {opt - 1}, {opt + 1} and 0 do nothing",
                  $"Mission.Begin(),Text.Special({text})|||",
                  $"{C2MRun(h, F(opt))}|{C2MRun(h, F(opt - 1))}|{C2MRun(h, F(opt + 1))}|{C2MRun(h, F(0))}");
        }
    }

    // the San d'Oria 1009 / 2009 finishes: option <opt> begins (no message)
    static void N3SandoriaAccept(string label, MissionScriptDefinition d, int id, int opt)
    {
        foreach (var (zone, csid) in new[]
                 {
                     (QC.Zone.SouthernSanDoria, 1009), (QC.Zone.SouthernSanDoria, 2009),
                     (QC.Zone.NorthernSanDoria, 1009),
                 })
        {
            var h = C2MHandler(d, 0, N3Fin, zone, id: csid);
            Fake F(int option) => N3Fake(MissionLog.Sandoria, id, QC.Mission.Sandoria.None, option: option);
            Check($"c3 N3 {label}: zone {zone}'s {csid} finish: option {opt} begins (no message); options " +
                  $"{opt - 1}, {opt + 1} and 0 do nothing",
                  "Mission.Begin()|||",
                  $"{C2MRun(h, F(opt))}|{C2MRun(h, F(opt - 1))}|{C2MRun(h, F(opt + 1))}|{C2MRun(h, F(0))}");
        }
    }

    // section 1 of a nation mission: no mission current and the char of the nation with the log's id; with
    // notDone, the mission not completed either
    static void N3Section1(string label, MissionScriptDefinition d, MissionLog log, int id, int none, int other,
                           bool notDone)
    {
        int l = (int)log, foreign = (l + 1) % 3;
        var s = d.States[0];
        Check($"c3 N3 {label}: section 1 holds with no mission current and a char of nation {l}; fails for nation " +
              $"{foreign}, with a mission current (this one, another)" +
              (notDone ? ", and once this mission is completed" : "; holds once this mission is completed"),
              $"True False False False {!notDone}",
              $"{s.IsMissionMatch(N3Fake(log, id, none))} {s.IsMissionMatch(N3Fake(log, id, none, nation: foreign))} " +
              $"{s.IsMissionMatch(N3Fake(log, id, id))} {s.IsMissionMatch(N3Fake(log, id, other))} " +
              $"{s.IsMissionMatch(N3Fake(log, id, none, done: id))}");
    }

    static void RunC3N3TheThreeKingdoms()
    {
        const string L = "three kingdoms";
        var m = new QC.Missions.Windurst.TheThreeKingdoms();
        var d = m.Compile();
        int w = QC.MissionLog.Windurst, id = QC.Mission.Windurst.TheThreeKingdoms, none = QC.Mission.Windurst.None;
        const int letter = QC.KeyItem.LetterToTheConsulsWindurst, cert = QC.KeyItem.AdventurersCertificate;
        const int cipher = QC.Item.CipherOfSemihsAlterEgo, spell = QC.Spell.SemihLafihna;
        Check($"c3 N3 {L}: clean, its key and LegacyPath, 3000 gil, rank 3, the certificate and its title",
              $"|{QC.MissionKey.Windurst.TheThreeKingdoms}|scripts/missions/windurst/2_3_0_The_Three_Kingdoms|" +
              $"3000/3/{cert}/{QC.Title.CertifiedAdventurer}/",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.Reward.Gil}/" +
              $"{d.Reward.Rank}/{d.Reward.KeyItem}/{d.Reward.Title}/{d.Reward.RankPoints}");
        Fake F(int status = 0, int current = -1, int nation = 2, int option = 0, params int[] done) =>
            N3Fake(MissionLog.Windurst, id, current < 0 ? id : current, status, nation, option, done);
        N3Section1(L, d, MissionLog.Windurst, id, none, QC.Mission.Windurst.TheHeartOfTheMatter, notDone: false);
        Check($"c3 N3 {L}: section 2 holds while this mission is current only", "True False",
              $"{d.States[1].IsMissionMatch(F())} {d.States[1].IsMissionMatch(F(current: none))}");
        Check($"c3 N3 {L}: section 3: no mission current, this one completed, a Windurstian (xi.nation.WINDURST, 2); " +
              "fails for a Bastokan, with a mission current, and before completion",
              "True False False False",
              $"{d.States[2].IsMissionMatch(F(current: none, done: id))} " +
              $"{d.States[2].IsMissionMatch(F(current: none, nation: 1, done: id))} " +
              $"{d.States[2].IsMissionMatch(F(done: id))} {d.States[2].IsMissionMatch(F(current: none))}");
        N3WindurstAccept(L, d, id, 5);

        string R(ushort zone, string npc, Fake f) => C3NReply(C2MHandler(d, 1, N3Trig, zone, npc), f);
        Check($"c3 N3 {L}: the gate guards' lines: Janshura-Rashura 138, Zokima-Rokima 134, Mokyokyo 215, " +
              "Rakoh Buuma 177 (progress); Dagoza-Beruza 213, Panna-Donna 212 (events), at any status",
              "ProgressEvent(138)|ProgressEvent(134)|ProgressEvent(215)|ProgressEvent(177)|Event(213)|Event(212)",
              $"{R(QC.Zone.PortWindurst, QC.Npc.PortWindurst.JanshuraRashura, F(4))}|" +
              $"{R(QC.Zone.WindurstWalls, QC.Npc.WindurstWalls.ZokimaRokima, F(4))}|" +
              $"{R(QC.Zone.WindurstWaters, QC.Npc.WindurstWaters.Mokyokyo, F(4))}|" +
              $"{R(QC.Zone.WindurstWoods, QC.Npc.WindurstWoods.RakohBuuma, F(4))}|" +
              $"{R(QC.Zone.WindurstWaters, QC.Npc.WindurstWaters.DagozaBeruza, F(4))}|" +
              $"{R(QC.Zone.WindurstWaters, QC.Npc.WindurstWaters.PannaDonna, F(4))}");

        // Kupipi
        Fake K(int status, bool trust, bool hasSpell = false, bool hasCipher = false, int line = 0)
        {
            var f = F(status);
            if (trust) f.MainSettings["ENABLE_TRUST_QUESTS"] = 1;
            if (hasSpell) f.Spells.Add(spell);
            if (hasCipher) f.Items.Add(cipher);
            if (line != 0) f.PlayerLocalVars["KupipiRepeatLine"] = line;
            return f;
        }
        string KR(Fake f) => R(QC.Zone.HeavensTower, QC.Npc.HeavensTower.Kupipi, f);
        Fake Trust2()
        {
            var f = K(0, false);
            f.MainSettings["ENABLE_TRUST_QUESTS"] = 2;
            return f;
        }
        Check($"c3 N3 {L}: Kupipi at 0: trust quests off: 95 with the letter in slot 3 and no eighth parameter; on: " +
              "the eighth 1 with neither spell nor cipher, 0 with either; ENABLE_TRUST_QUESTS 2: as off",
              $"ProgressEvent(95,0,0,0,{letter})|ProgressEvent(95,0,0,0,{letter},0,0,0,1)|" +
              $"ProgressEvent(95,0,0,0,{letter},0,0,0,0)|ProgressEvent(95,0,0,0,{letter},0,0,0,0)|" +
              $"ProgressEvent(95,0,0,0,{letter})",
              $"{KR(K(0, false))}|{KR(K(0, true))}|{KR(K(0, true, hasSpell: true))}|" +
              $"{KR(K(0, true, hasCipher: true))}|" +
              $"{KR(Trust2())}");
        Check($"c3 N3 {L}: Kupipi at 11: 101 with the certificate in slot 2; other statuses: 97 while " +
              "KupipiRepeatLine is 0, 98 otherwise",
              $"ProgressEvent(101,0,0,{cert})|ProgressEvent(97)|ProgressEvent(98)|ProgressEvent(98)",
              $"{KR(K(11, false))}|{KR(K(1, false))}|{KR(K(10, false, line: 1))}|{KR(K(12, false, line: 2))}");
        var k95 = C2MHandler(d, 1, N3Fin, QC.Zone.HeavensTower, id: 95);
        Check($"c3 N3 {L}: 95's finish: status 1, the letter; with trust on and neither spell nor cipher the " +
              "cipher too; trust on with the spell or the cipher, or trust off: no cipher",
              $"Player.SetMissionStatus({w},1),Player.GiveKeyItem({letter}),Player.GiveItem({cipher})|" +
              $"Player.SetMissionStatus({w},1),Player.GiveKeyItem({letter})|" +
              $"Player.SetMissionStatus({w},1),Player.GiveKeyItem({letter})|" +
              $"Player.SetMissionStatus({w},1),Player.GiveKeyItem({letter})",
              $"{C2MRun(k95, K(0, true))}|{C2MRun(k95, K(0, true, hasSpell: true))}|" +
              $"{C2MRun(k95, K(0, true, hasCipher: true))}|{C2MRun(k95, K(0, false))}");
        var k101 = C2MHandler(d, 1, N3Fin, QC.Zone.HeavensTower, id: 101);
        var failed = F(11);
        failed.CompleteResult = false;
        Check($"c3 N3 {L}: 97 sets KupipiRepeatLine 1, 98 sets 0; 101 completes, then takes the Kindred Report; " +
              "complete false: the report kept",
              $"Player.SetLocalVar(KupipiRepeatLine,1)|Player.SetLocalVar(KupipiRepeatLine,0)|" +
              $"Mission.Complete(),Player.DelKeyItem({QC.KeyItem.KindredReport})|Mission.Complete()",
              $"{C2MRun(C2MHandler(d, 1, N3Fin, QC.Zone.HeavensTower, id: 97), F(1))}|" +
              $"{C2MRun(C2MHandler(d, 1, N3Fin, QC.Zone.HeavensTower, id: 98), F(1))}|" +
              $"{C2MRun(k101, F(11))}|{C2MRun(k101, failed)}");

        // Metalworks
        string MW(string npc, int status) => R(QC.Zone.Metalworks, npc, F(status));
        Check($"c3 N3 {L}: Mih Ketto, Moyoyo, Topuru-Kuperu: status 11: 268, 265, 262; 1 and 10: 266, 263, 260 " +
              "(events); 0: nothing",
              "Event(268)|Event(266)|Event(266)|null|Event(265)|Event(263)|Event(263)|null|" +
              "Event(262)|Event(260)|Event(260)|null",
              string.Join("|", new[] { QC.Npc.Metalworks.MihKetto, QC.Npc.Metalworks.Moyoyo,
                                       QC.Npc.Metalworks.TopuruKuperu }
                  .SelectMany(n => new[] { MW(n, 11), MW(n, 1), MW(n, 10), MW(n, 0) })));
        Check($"c3 N3 {L}: Patt-Pott: 1: 254, 6: 256, 7: 258, 11: 259 (progress); 0, 2, 5, 8, 10, 12: nothing",
              "ProgressEvent(254)|ProgressEvent(256)|ProgressEvent(258)|ProgressEvent(259)|" +
              "null|null|null|null|null|null",
              string.Join("|", new[] { 1, 6, 7, 11, 0, 2, 5, 8, 10, 12 }
                  .Select(s => MW(QC.Npc.Metalworks.PattPott, s))));
        string Swap(ushort zone, int csid) => C2MRun(C2MHandler(d, 1, N3Fin, zone, id: csid), F(1));
        Check($"c3 N3 {L}: 254 and 546 swap to the first legs (Bastok, San d'Oria) at status 3; 256 and 547 to the " +
              "second legs at status 8 and take the letter",
              $"Player.DelMission({w},{id}),Player.AddMission({w},{QC.Mission.Windurst.TheThreeKingdomsBastok})," +
              $"Player.SetMissionStatus({w},3)|" +
              $"Player.DelMission({w},{id}),Player.AddMission({w},{QC.Mission.Windurst.TheThreeKingdomsBastok2})," +
              $"Player.DelKeyItem({letter}),Player.SetMissionStatus({w},8)|" +
              $"Player.DelMission({w},{id}),Player.AddMission({w},{QC.Mission.Windurst.TheThreeKingdomsSandoria})," +
              $"Player.SetMissionStatus({w},3)|" +
              $"Player.DelMission({w},{id}),Player.AddMission({w},{QC.Mission.Windurst.TheThreeKingdomsSandoria2})," +
              $"Player.DelKeyItem({letter}),Player.SetMissionStatus({w},8)",
              $"{Swap(QC.Zone.Metalworks, 254)}|{Swap(QC.Zone.Metalworks, 256)}|" +
              $"{Swap(QC.Zone.NorthernSanDoria, 546)}|{Swap(QC.Zone.NorthernSanDoria, 547)}");

        // Northern San d'Oria
        var nsd = QC.Zone.NorthernSanDoria;
        Fake Rov(int status, int rovMission)
        {
            var f = F(status);
            f.CurrentMissions[QC.MissionLog.Rov] = rovMission;
            return f;
        }
        string H(Fake f) => R(nsd, QC.Npc.NorthernSanDoria.HeruzeMoruze, f);
        Check($"c3 N3 {L}: Heruze-Moruze at 1: 582 with slot 7 = 1 while RoV's The Path Untraveled is current, 0 " +
              "with another RoV mission (the one before, the one after); status 2: nothing",
              $"ProgressEvent(582,{{[7]=1}})|ProgressEvent(582,{{[7]=0}})|ProgressEvent(582,{{[7]=0}})|null",
              $"{H(Rov(1, QC.Mission.Rov.ThePathUntraveled))}|{H(Rov(1, QC.Mission.Rov.ThePathUntraveled - 1))}|" +
              $"{H(Rov(1, QC.Mission.Rov.ThePathUntraveled + 1))}|{H(Rov(2, QC.Mission.Rov.ThePathUntraveled))}");
        Check($"c3 N3 {L}: 582's finish sets status 2", $"Player.SetMissionStatus({w},2)",
              C2MRun(C2MHandler(d, 1, N3Fin, nsd, id: 582), F(1)));
        Fake Ka(int status)
        {
            var f = F(status);
            f.TextIds[QC.Text.NorthernSanDoria.KASARORO_DIALOG.ToString()] = 7400;
            return f;
        }
        var kas = C2MHandler(d, 1, N3Trig, nsd, QC.Npc.NorthernSanDoria.Kasaroro);
        var k11 = Ka(11);
        string k11Reply = C3NReply(kas, k11);
        Check($"c3 N3 {L}: Kasaroro: 2: 546, 7: 547 (progress); 6: KASARORO_DIALOG + 7 (message text); 11: shows " +
              "KASARORO_DIALOG + 20 and answers no action; 3, 5, 8: nothing",
              "ProgressEvent(546)|ProgressEvent(547)|MessageText(7407)|NoAction()|Player.ShowText(7420)|null|null|null",
              $"{C3NReply(kas, Ka(2))}|{C3NReply(kas, Ka(7))}|{C3NReply(kas, Ka(6))}|{k11Reply}|" +
              $"{string.Join(",", k11.Calls)}|{C3NReply(kas, Ka(3))}|{C3NReply(kas, Ka(5))}|{C3NReply(kas, Ka(8))}");
        Check($"c3 N3 {L}: section 3: Kupipi 102 (no replaceDefault), Kasaroro 604 over his default",
              "Event(102)|Event(604):replaceDefault()",
              $"{C3NReply(C2MHandler(d, 2, N3Trig, QC.Zone.HeavensTower, QC.Npc.HeavensTower.Kupipi), F(0, none))}|" +
              $"{C3NReply(C2MHandler(d, 2, N3Trig, nsd, QC.Npc.NorthernSanDoria.Kasaroro), F(0, none))}");
    }

    static void RunC3N3TheThreeKingdomsSandoria()
    {
        const string L = "three kingdoms sandoria";
        var m = new QC.Missions.Windurst.TheThreeKingdomsSandoria();
        var d = m.Compile();
        int w = QC.MissionLog.Windurst, id = QC.Mission.Windurst.TheThreeKingdomsSandoria;
        const int cipher = QC.Item.CipherOfHalversAlterEgo, spell = QC.Spell.Halver;
        Check($"c3 N3 {L}: clean, its key and LegacyPath, no reward table, one section",
              $"|{QC.MissionKey.Windurst.TheThreeKingdomsSandoria}|" +
              "scripts/missions/windurst/2_3_1_The_Three_Kingdoms_Sandoria|True|1",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.Reward == null}|" +
              $"{d.States.Count}");
        Fake F(int status = 0, bool trust = false, bool hasSpell = false, bool hasCipher = false)
        {
            var f = N3Fake(MissionLog.Windurst, id, id, status);
            if (trust) f.MainSettings["ENABLE_TRUST_QUESTS"] = 1;
            if (hasSpell) f.Spells.Add(spell);
            if (hasCipher) f.Items.Add(cipher);
            f.TextIds[QC.Text.ChateauDoraguille.HALVER_OFFSET.ToString()] = 7000;
            f.TextIds[QC.Text.NorthernSanDoria.KASARORO_DIALOG.ToString()] = 7400;
            return f;
        }
        Check($"c3 N3 {L}: the section holds while this mission is current only", "True False",
              $"{d.States[0].IsMissionMatch(F())} " +
              $"{d.States[0].IsMissionMatch(N3Fake(MissionLog.Windurst, id, QC.Mission.Windurst.TheThreeKingdoms))}");
        var halver = C2MHandler(d, 0, N3Trig, QC.Zone.ChateauDoraguille, QC.Npc.ChateauDoraguille.Halver);
        Check($"c3 N3 {L}: Halver at 3: 502 with slot 7 = 1 with trust on and neither spell nor cipher, else 0 " +
              "(spell, cipher, trust off); other statuses (2, 4): HALVER_OFFSET + 279",
              "ProgressEvent(502,{[7]=1})|ProgressEvent(502,{[7]=0})|ProgressEvent(502,{[7]=0})|" +
              "ProgressEvent(502,{[7]=0})|MessageText(7279)|MessageText(7279)",
              $"{C3NReply(halver, F(3, true))}|{C3NReply(halver, F(3, true, hasSpell: true))}|" +
              $"{C3NReply(halver, F(3, true, hasCipher: true))}|{C3NReply(halver, F(3))}|" +
              $"{C3NReply(halver, F(2))}|{C3NReply(halver, F(4))}");
        var f502 = C2MHandler(d, 0, N3Fin, QC.Zone.ChateauDoraguille, id: 502);
        Check($"c3 N3 {L}: 502's finish: status 4, and the cipher under the same trust test",
              $"Player.SetMissionStatus({w},4),Player.GiveItem({cipher})|Player.SetMissionStatus({w},4)|" +
              $"Player.SetMissionStatus({w},4)|Player.SetMissionStatus({w},4)",
              $"{C2MRun(f502, F(3, true))}|{C2MRun(f502, F(3, true, hasSpell: true))}|" +
              $"{C2MRun(f502, F(3, true, hasCipher: true))}|{C2MRun(f502, F(3))}");
        var vatgit = C2MHandler(d, 0, QuestHandlerKind.OnMobDeath, QC.Zone.GhelsbaOutpost,
                                QC.Npc.GhelsbaOutpost.WarchiefVatgit);
        Check($"c3 N3 {L}: Warchief Vatgit's death: status 4 to 5; 3 and 5: nothing",
              $"Player.SetMissionStatus({w},5)||",
              $"{C2MRun(vatgit, F(4))}|{C2MRun(vatgit, F(3))}|{C2MRun(vatgit, F(5))}");
        var kas = C2MHandler(d, 0, N3Trig, QC.Zone.NorthernSanDoria, QC.Npc.NorthernSanDoria.Kasaroro);
        Check($"c3 N3 {L}: Kasaroro: 3: KASARORO_DIALOG (message text); 4: 549, 5: 550 (progress); 2, 6: nothing",
              $"MessageText({QC.Text.NorthernSanDoria.KASARORO_DIALOG})|ProgressEvent(549)|ProgressEvent(550)|" +
              "null|null",
              $"{C3NReply(kas, F(3))}|{C3NReply(kas, F(4))}|{C3NReply(kas, F(5))}|{C3NReply(kas, F(2))}|" +
              $"{C3NReply(kas, F(6))}");
        var f550 = C2MHandler(d, 0, N3Fin, QC.Zone.NorthernSanDoria, id: 550);
        var failed = F(5);
        failed.CompleteResult = false;
        Check($"c3 N3 {L}: 550's finish: completes, then The Three Kingdoms at status 6; complete false: nothing more",
              $"Mission.Complete(),Player.AddMission({w},{QC.Mission.Windurst.TheThreeKingdoms})," +
              $"Player.SetMissionStatus({w},6)|Mission.Complete()",
              $"{C2MRun(f550, F(5))}|{C2MRun(f550, failed)}");
    }

    static void RunC3N3TheThreeKingdomsBastok()
    {
        const string L = "three kingdoms bastok";
        var m = new QC.Missions.Windurst.TheThreeKingdomsBastok();
        var d = m.Compile();
        int w = QC.MissionLog.Windurst, id = QC.Mission.Windurst.TheThreeKingdomsBastok;
        const int sand = QC.Item.OnzOfMythrilSand, pick = QC.Item.Pickaxe;
        Check($"c3 N3 {L}: clean, its key and LegacyPath, no reward table, one section",
              $"|{QC.MissionKey.Windurst.TheThreeKingdomsBastok}|" +
              "scripts/missions/windurst/2_3_2_The_Three_Kingdoms_Bastok|True|1",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.Reward == null}|" +
              $"{d.States.Count}");
        Fake F(int status = 0, int option = 0, bool hasSand = false, int rov = 0)
        {
            var f = N3Fake(MissionLog.Windurst, id, id, status);
            if (option != 0) f.MissionVars[$"{w}/{id}/Option"] = option;
            if (hasSand) f.Items.Add(sand);
            f.CurrentMissions[QC.MissionLog.Rov] = rov;
            return f;
        }
        Check($"c3 N3 {L}: the section holds while this mission is current only", "True False",
              $"{d.States[0].IsMissionMatch(F())} " +
              $"{d.States[0].IsMissionMatch(N3Fake(MissionLog.Windurst, id, QC.Mission.Windurst.TheThreeKingdoms))}");
        var grohm = C2MHandler(d, 0, N3Trig, QC.Zone.Metalworks, QC.Npc.Metalworks.Grohm);
        int path = QC.Mission.Rov.ThePathUntraveled;
        Check($"c3 N3 {L}: Grohm: Option 1: 425 (progress) at any status (4, 5); Option 2 at 4: as Option 0; at 4: " +
              "423 with slot 7 = 1 while RoV's The Path Untraveled is current, else 0 (one before, one after); at 5 " +
              "without the sand: 424, with it: 422; 3, 6: 422",
              "ProgressEvent(425)|ProgressEvent(425)|ProgressEvent(423,{[7]=0})|ProgressEvent(423,{[7]=1})|" +
              "ProgressEvent(423,{[7]=0})|ProgressEvent(423,{[7]=0})|Event(424)|Event(422)|Event(422)|Event(422)",
              $"{C3NReply(grohm, F(4, 1))}|{C3NReply(grohm, F(5, 1, hasSand: true))}|{C3NReply(grohm, F(4, 2))}|" +
              $"{C3NReply(grohm, F(4, rov: path))}|{C3NReply(grohm, F(4, rov: path - 1))}|" +
              $"{C3NReply(grohm, F(4, rov: path + 1))}|{C3NReply(grohm, F(5))}|" +
              $"{C3NReply(grohm, F(5, hasSand: true))}|{C3NReply(grohm, F(3))}|{C3NReply(grohm, F(6))}");
        var pius = C2MHandler(d, 0, N3Trig, QC.Zone.Metalworks, QC.Npc.Metalworks.Pius);
        Check($"c3 N3 {L}: Pius: 3: 355 (progress, parameter 1); 2, 4: nothing",
              "ProgressEvent(355,1)|null|null",
              $"{C3NReply(pius, F(3))}|{C3NReply(pius, F(2))}|{C3NReply(pius, F(4))}");
        var patt = C2MHandler(d, 0, N3Trade, QC.Zone.Metalworks, QC.Npc.Metalworks.PattPott);
        Fake T(int status, bool match)
        {
            var f = F(status);
            f.TradeResult = match;
            return f;
        }
        Check($"c3 N3 {L}: Patt-Pott's trade: the sand at 5: 255 (progress); at 4 or 6, or no match at 5: nothing",
              "ProgressEvent(255)|null|null|null",
              $"{C3NReply(patt, T(5, true))}|{C3NReply(patt, T(4, true))}|{C3NReply(patt, T(6, true))}|" +
              $"{C3NReply(patt, T(5, false))}");
        string R(string npc) => C3NReply(C2MHandler(d, 0, N3Trig, QC.Zone.Metalworks, npc), F(4));
        Check($"c3 N3 {L}: Mih Ketto 267, Moyoyo 264, Topuru-Kuperu 261 (events)", "Event(267)|Event(264)|Event(261)",
              $"{R(QC.Npc.Metalworks.MihKetto)}|{R(QC.Npc.Metalworks.Moyoyo)}|{R(QC.Npc.Metalworks.TopuruKuperu)}");
        var f255 = C2MHandler(d, 0, N3Fin, QC.Zone.Metalworks, id: 255);
        var failed = F(5);
        failed.CompleteResult = false;
        Check($"c3 N3 {L}: 255's finish: completes, takes the trade, status 7, The Three Kingdoms; complete false: " +
              "nothing more",
              $"Mission.Complete(),Trade.Complete(),Player.SetMissionStatus({w},7)," +
              $"Player.AddMission({w},{QC.Mission.Windurst.TheThreeKingdoms})|Mission.Complete()",
              $"{C2MRun(f255, F(5))}|{C2MRun(f255, failed)}");
        Check($"c3 N3 {L}: 355's finish sets status 4", $"Player.SetMissionStatus({w},4)",
              C2MRun(C2MHandler(d, 0, N3Fin, QC.Zone.Metalworks, id: 355), F(3)));
        var f423 = C2MHandler(d, 0, N3Fin, QC.Zone.Metalworks, id: 423);
        var f425 = C2MHandler(d, 0, N3Fin, QC.Zone.Metalworks, id: 425);
        var full423 = F(4);
        full423.GiveResult = false;
        var full425 = F(4, 1);
        full425.GiveResult = false;
        Check($"c3 N3 {L}: 423: five pickaxes, status 5; no room: Option 1. 425: the pickaxes, status 5, Option 0; " +
              "no room: nothing more",
              $"Player.GiveItems({pick}x5),Player.SetMissionStatus({w},5)|" +
              $"Player.GiveItems({pick}x5),Mission.SetVar(Option,1)|" +
              $"Player.GiveItems({pick}x5),Player.SetMissionStatus({w},5),Mission.SetVar(Option,0)|" +
              $"Player.GiveItems({pick}x5)",
              $"{C2MRun(f423, F(4))}|{C2MRun(f423, full423)}|{C2MRun(f425, F(4, 1))}|{C2MRun(f425, full425)}");
    }

    static void RunC3N3TheThreeKingdomsBastok2()
    {
        const string L = "three kingdoms bastok2";
        var m = new QC.Missions.Windurst.TheThreeKingdomsBastok2();
        var d = m.Compile();
        int w = QC.MissionLog.Windurst, id = QC.Mission.Windurst.TheThreeKingdomsBastok2;
        Check($"c3 N3 {L}: clean, its key and LegacyPath, no reward table, one section",
              $"|{QC.MissionKey.Windurst.TheThreeKingdomsBastok2}|" +
              "scripts/missions/windurst/2_3_4_The_Three_Kingdoms_Bastok2|True|1",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.Reward == null}|" +
              $"{d.States.Count}");
        Fake F(int status = 0, int win = 0)
        {
            var f = N3Fake(MissionLog.Windurst, id, id, status);
            if (win != 0) f.PlayerLocalVars["battlefieldWin"] = win;
            return f;
        }
        Check($"c3 N3 {L}: the section holds while this mission is current only", "True False",
              $"{d.States[0].IsMissionMatch(F())} " +
              $"{d.States[0].IsMissionMatch(N3Fake(MissionLog.Windurst, id, QC.Mission.Windurst.TheThreeKingdoms))}");
        string R(string npc, int status) => C3NReply(C2MHandler(d, 0, N3Trig, QC.Zone.Metalworks, npc), F(status));
        Check($"c3 N3 {L}: Grohm: 9: 426 (progress, 1); 8, 10: 427 (event)",
              "ProgressEvent(426,1)|Event(427)|Event(427)",
              $"{R(QC.Npc.Metalworks.Grohm, 9)}|{R(QC.Npc.Metalworks.Grohm, 8)}|{R(QC.Npc.Metalworks.Grohm, 10)}");
        Check($"c3 N3 {L}: Pius: 8: 355 (progress, 1); 7, 9, 10: 356 (event); 11, 12: nothing",
              "ProgressEvent(355,1)|Event(356)|Event(356)|Event(356)|null|null",
              string.Join("|", new[] { 8, 7, 9, 10, 11, 12 }.Select(s => R(QC.Npc.Metalworks.Pius, s))));
        Check($"c3 N3 {L}: Patt-Pott: 11: 257 (progress); 10, 12: 258 (event)",
              "ProgressEvent(257)|Event(258)|Event(258)",
              string.Join("|", new[] { 11, 10, 12 }.Select(s => R(QC.Npc.Metalworks.PattPott, s))));
        Check($"c3 N3 {L}: the door _6ld and Iron Eater 702; Mih Ketto 267, Moyoyo 264, Topuru-Kuperu 261 (events)",
              "Event(702)|Event(702)|Event(267)|Event(264)|Event(261)",
              $"{R(QC.Npc.Metalworks._6ld, 9)}|{R(QC.Npc.Metalworks.IronEater, 9)}|" +
              $"{R(QC.Npc.Metalworks.MihKetto, 9)}|{R(QC.Npc.Metalworks.Moyoyo, 9)}|" +
              $"{R(QC.Npc.Metalworks.TopuruKuperu, 9)}");
        var f257 = C2MHandler(d, 0, N3Fin, QC.Zone.Metalworks, id: 257);
        var failed = F(11);
        failed.CompleteResult = false;
        Check($"c3 N3 {L}: 257's finish: completes, The Three Kingdoms, crest taken, report given, status 11; " +
              "complete false: nothing more",
              $"Mission.Complete(),Player.AddMission({w},{QC.Mission.Windurst.TheThreeKingdoms})," +
              $"Player.DelKeyItem({QC.KeyItem.KindredCrest}),Player.GiveKeyItem({QC.KeyItem.KindredReport})," +
              $"Player.SetMissionStatus({w},11)|Mission.Complete()",
              $"{C2MRun(f257, F(11))}|{C2MRun(f257, failed)}");
        Check($"c3 N3 {L}: 355 sets status 9, 426 sets 10",
              $"Player.SetMissionStatus({w},9)|Player.SetMissionStatus({w},10)",
              $"{C2MRun(C2MHandler(d, 0, N3Fin, QC.Zone.Metalworks, id: 355), F(8))}|" +
              $"{C2MRun(C2MHandler(d, 0, N3Fin, QC.Zone.Metalworks, id: 426), F(9))}");
        var bf = C2MHandler(d, 0, N3Fin, QC.Zone.WaughroonShrine, id: 32001);
        int r22 = QC.Battlefield.Rank2Mission2;
        Check($"c3 N3 {L}: Waughroon 32001: status 10 and battlefieldWin Rank 2 Mission 2: crest, Dark Key taken, " +
              "status 11; status 9 or 11, or another win (the ids either side, none): nothing",
              $"Player.GiveKeyItem({QC.KeyItem.KindredCrest}),Player.DelKeyItem({QC.KeyItem.DarkKey})," +
              $"Player.SetMissionStatus({w},11)|||||",
              $"{C2MRun(bf, F(10, r22))}|{C2MRun(bf, F(9, r22))}|{C2MRun(bf, F(11, r22))}|" +
              $"{C2MRun(bf, F(10, r22 - 1))}|{C2MRun(bf, F(10, r22 + 1))}|{C2MRun(bf, F(10))}");
    }

    static void RunC3N3ToEachHisOwnRight()
    {
        const string L = "to each his own right";
        var m = new QC.Missions.Windurst.ToEachHisOwnRight();
        var d = m.Compile();
        int w = QC.MissionLog.Windurst, id = QC.Mission.Windurst.ToEachHisOwnRight, none = QC.Mission.Windurst.None;
        const int bauble = QC.KeyItem.StarwayStairwayBauble;
        Check($"c3 N3 {L}: clean, its key and LegacyPath, 450 rank points",
              $"|{QC.MissionKey.Windurst.ToEachHisOwnRight}|scripts/missions/windurst/3_1_To_Each_His_Own_Right|" +
              "450///",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.Reward.RankPoints}/" +
              $"{d.Reward.Gil}/{d.Reward.Rank}/{d.Reward.KeyItem}");
        Fake F(int status = 0, int current = -1, int nation = 2, params int[] done) =>
            N3Fake(MissionLog.Windurst, id, current < 0 ? id : current, status, nation, 0, done);
        N3Section1(L, d, MissionLog.Windurst, id, none, QC.Mission.Windurst.TheThreeKingdoms, notDone: true);
        Check($"c3 N3 {L}: section 2 holds while this mission is current only", "True False",
              $"{d.States[1].IsMissionMatch(F())} {d.States[1].IsMissionMatch(F(current: none))}");
        int stars = QC.Mission.Windurst.WrittenInTheStars;
        Check($"c3 N3 {L}: section 3: no mission current, a Windurstian, this one completed, Written in the Stars " +
              "not; fails with Written in the Stars completed, for a Bastokan, with a mission current, before " +
              "completion",
              "True False False False False",
              $"{d.States[2].IsMissionMatch(F(current: none, done: id))} " +
              $"{d.States[2].IsMissionMatch(F(current: none, done: new[] { id, stars }))} " +
              $"{d.States[2].IsMissionMatch(F(current: none, nation: 1, done: id))} " +
              $"{d.States[2].IsMissionMatch(F(done: id))} {d.States[2].IsMissionMatch(F(current: none))}");
        N3WindurstAccept(L, d, id, 10);
        Check($"c3 N3 {L}: Castle Oztroja's 43 finish sets status 4", $"Player.SetMissionStatus({w},4)",
              C2MRun(C2MHandler(d, 1, N3Fin, QC.Zone.CastleOztroja, id: 43), F(3)));
        string R(string npc, int status, int section = 1) =>
            C3NReply(C2MHandler(d, section, N3Trig, QC.Zone.HeavensTower, npc), section == 1 ? F(status) :
                     F(status, none, done: id));
        Check($"c3 N3 {L}: the Sibyl Guards at 2: Foo Beibo 112, Shaz Norem 110, Ufu Koromoa 111, Vahn Paineesha " +
              "109 (events); at 1 and 3: nothing",
              "Event(112)|null|null|Event(110)|null|null|Event(111)|null|null|Event(109)|null|null",
              string.Join("|", new[] { QC.Npc.HeavensTower.FooBeibo, QC.Npc.HeavensTower.ShazNorem,
                                       QC.Npc.HeavensTower.UfuKoromoa, QC.Npc.HeavensTower.VahnPaineesha }
                  .SelectMany(n => new[] { R(n, 2), R(n, 1), R(n, 3) })));
        Check($"c3 N3 {L}: Kupipi: 0: 103 (progress, the bauble in slot 2); 1: 104, 4: 113 (events); 2, 3: nothing",
              $"ProgressEvent(103,0,0,{bauble})|Event(104)|Event(113)|null|null",
              string.Join("|", new[] { 0, 1, 4, 2, 3 }.Select(s => R(QC.Npc.HeavensTower.Kupipi, s))));
        Check($"c3 N3 {L}: Rhy Epocan: 1: 107 (progress); 2: 108 (event); 4: 114 (progress); 0, 3: nothing",
              "ProgressEvent(107)|Event(108)|ProgressEvent(114)|null|null",
              string.Join("|", new[] { 1, 2, 4, 0, 3 }.Select(s => R(QC.Npc.HeavensTower.RhyEpocan, s))));
        string Fin(ushort zone, int csid) => C2MRun(C2MHandler(d, 1, N3Fin, zone, id: csid), F(1));
        Check($"c3 N3 {L}: 103: status 1, the bauble; 107: status 2; 114: completes; 147: status 3",
              $"Player.SetMissionStatus({w},1),Player.GiveKeyItem({bauble})|Player.SetMissionStatus({w},2)|" +
              $"Mission.Complete()|Player.SetMissionStatus({w},3)",
              $"{Fin(QC.Zone.HeavensTower, 103)}|{Fin(QC.Zone.HeavensTower, 107)}|" +
              $"{Fin(QC.Zone.HeavensTower, 114)}|{Fin(QC.Zone.PortWindurst, 147)}");
        var hak = C2MHandler(d, 1, N3Trig, QC.Zone.PortWindurst, QC.Npc.PortWindurst.HakkuruRinkuru);
        Check($"c3 N3 {L}: Hakkuru-Rinkuru: 2: 147 (progress); 3: 148 (event); 1, 4: nothing",
              "ProgressEvent(147)|Event(148)|null|null",
              string.Join("|", new[] { 2, 3, 1, 4 }.Select(s => C3NReply(hak, F(s)))));
        string Wd(string npc) => C3NReply(C2MHandler(d, 1, N3Trig, QC.Zone.WindurstWoods, npc), F(0));
        Check($"c3 N3 {L}: Windurst Woods: Rakoh Buuma 182, Sola Jaab 184, Tih Pikeh 183 (events)",
              "Event(182)|Event(184)|Event(183)",
              $"{Wd(QC.Npc.WindurstWoods.RakohBuuma)}|{Wd(QC.Npc.WindurstWoods.SolaJaab)}|" +
              $"{Wd(QC.Npc.WindurstWoods.TihPikeh)}");
        Check($"c3 N3 {L}: section 3: Foo Beibo 162, Rhy Epocan 115, Shaz Norem 117, Ufu Koromoa 118, Vahn " +
              "Paineesha 116, each over its default",
              "Event(162):replaceDefault()|Event(115):replaceDefault()|Event(117):replaceDefault()|" +
              "Event(118):replaceDefault()|Event(116):replaceDefault()",
              string.Join("|", new[] { QC.Npc.HeavensTower.FooBeibo, QC.Npc.HeavensTower.RhyEpocan,
                                       QC.Npc.HeavensTower.ShazNorem, QC.Npc.HeavensTower.UfuKoromoa,
                                       QC.Npc.HeavensTower.VahnPaineesha }.Select(n => R(n, 0, 2))));
    }

    static void RunC3N3WrittenInTheStars()
    {
        const string L = "written in the stars";
        var m = new QC.Missions.Windurst.WrittenInTheStars();
        var d = m.Compile();
        int w = QC.MissionLog.Windurst, id = QC.Mission.Windurst.WrittenInTheStars, none = QC.Mission.Windurst.None;
        int journey = QC.Mission.Windurst.ANewJourney;
        const int charm = QC.KeyItem.CharmOfLight, dagger = QC.Item.RustyDagger;
        Check($"c3 N3 {L}: clean, its key and LegacyPath, 400 rank points",
              $"|{QC.MissionKey.Windurst.WrittenInTheStars}|scripts/missions/windurst/3_2_Written_in_the_Stars|" +
              "400///",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.Reward.RankPoints}/" +
              $"{d.Reward.Gil}/{d.Reward.Rank}/{d.Reward.KeyItem}");
        Fake F(int status = 0, int current = -1, int nation = 2, params int[] done) =>
            N3Fake(MissionLog.Windurst, id, current < 0 ? id : current, status, nation, 0, done);
        N3Section1(L, d, MissionLog.Windurst, id, none, QC.Mission.Windurst.TheThreeKingdoms, notDone: false);
        Check($"c3 N3 {L}: section 2: current with neither this mission nor A New Journey completed; fails with " +
              "either completed and when not current",
              "True False False False",
              $"{d.States[1].IsMissionMatch(F())} {d.States[1].IsMissionMatch(F(done: id))} " +
              $"{d.States[1].IsMissionMatch(F(done: journey))} {d.States[1].IsMissionMatch(F(current: none))}");
        Check($"c3 N3 {L}: section 3: current with this mission or A New Journey completed (each alone, both); " +
              "fails with neither and when not current",
              "True True True False False",
              $"{d.States[2].IsMissionMatch(F(done: id))} {d.States[2].IsMissionMatch(F(done: journey))} " +
              $"{d.States[2].IsMissionMatch(F(done: new[] { id, journey }))} {d.States[2].IsMissionMatch(F())} " +
              $"{d.States[2].IsMissionMatch(F(current: none, done: id))}");
        N3WindurstAccept(L, d, id, 11);
        string S1(string npc, params int[] done) =>
            C3NReply(C2MHandler(d, 0, N3Trig, QC.Zone.HeavensTower, npc), F(0, none, done: done));
        Check($"c3 N3 {L}: section 1: Kiwawa 138, Nebibi 147, Zubaba 136 over their defaults with this mission " +
              "completed and A New Journey not; nothing with both completed or neither",
              "Event(138):replaceDefault()|Event(147):replaceDefault()|Event(136):replaceDefault()|" +
              "null|null|null|null|" +
              "null|null",
              string.Join("|", new[] { QC.Npc.HeavensTower.Kiwawa, QC.Npc.HeavensTower.Nebibi,
                                       QC.Npc.HeavensTower.Zubaba }.Select(n => S1(n, id))) + "|" +
              string.Join("|", new[] { QC.Npc.HeavensTower.Kiwawa, QC.Npc.HeavensTower.Nebibi,
                                       QC.Npc.HeavensTower.Zubaba }
                  .SelectMany(n => new[] { S1(n, id, journey), S1(n) })));
        string R(string npc, int status, int section = 1, params int[] done) =>
            C3NReply(C2MHandler(d, section, N3Trig, QC.Zone.HeavensTower, npc), F(status, done: done));
        Check($"c3 N3 {L}: Kiwawa and Nebibi at 1: 124 and 134 (events); at 0 and 2: nothing; Kupipi 120 (event)",
              "Event(124)|null|null|Event(134)|null|null|Event(120)",
              $"{R(QC.Npc.HeavensTower.Kiwawa, 1)}|{R(QC.Npc.HeavensTower.Kiwawa, 0)}|" +
              $"{R(QC.Npc.HeavensTower.Kiwawa, 2)}|{R(QC.Npc.HeavensTower.Nebibi, 1)}|" +
              $"{R(QC.Npc.HeavensTower.Nebibi, 0)}|{R(QC.Npc.HeavensTower.Nebibi, 2)}|" +
              $"{R(QC.Npc.HeavensTower.Kupipi, 3)}");
        Check($"c3 N3 {L}: Zubaba: 0: 121 (progress); 1: 122 (event); 2: 135 (progress); 3: nothing",
              "ProgressEvent(121)|Event(122)|ProgressEvent(135)|null",
              string.Join("|", new[] { 0, 1, 2, 3 }.Select(s => R(QC.Npc.HeavensTower.Zubaba, s))));
        var f135 = C2MHandler(d, 1, N3Fin, QC.Zone.HeavensTower, id: 135);
        var failed = F(2);
        failed.CompleteResult = false;
        Check($"c3 N3 {L}: 121: the charm, status 1; 135: completes, then 100 rank points; complete false: none",
              $"Player.GiveKeyItem({charm}),Player.SetMissionStatus({w},1)|Mission.Complete()," +
              "Player.AddRankPoints(100)|Mission.Complete()",
              $"{C2MRun(C2MHandler(d, 1, N3Fin, QC.Zone.HeavensTower, id: 121), F(0))}|{C2MRun(f135, F(2))}|" +
              $"{C2MRun(f135, failed)}");
        var gate = C2MHandler(d, 1, N3Trig, QC.Zone.InnerHorutotoRuins, QC.Npc.InnerHorutotoRuins._5ci);
        Check($"c3 N3 {L}: the Gate of Light at 1: 41 (progress, the charm in slot 1); 0, 2: nothing; 41's finish: " +
              "status 2, the charm taken",
              $"ProgressEvent(41,0,{charm})|null|null|Player.SetMissionStatus({w},2),Player.DelKeyItem({charm})",
              $"{C3NReply(gate, F(1))}|{C3NReply(gate, F(0))}|{C3NReply(gate, F(2))}|" +
              $"{C2MRun(C2MHandler(d, 1, N3Fin, QC.Zone.InnerHorutotoRuins, id: 41), F(1))}");
        string Wd(string npc) => C3NReply(C2MHandler(d, 1, N3Trig, QC.Zone.WindurstWoods, npc), F(0));
        Check($"c3 N3 {L}: Windurst Woods: Rakoh Buuma 188, Sola Jaab 190, Tih Pikeh 189 (events)",
              "Event(188)|Event(190)|Event(189)",
              $"{Wd(QC.Npc.WindurstWoods.RakohBuuma)}|{Wd(QC.Npc.WindurstWoods.SolaJaab)}|" +
              $"{Wd(QC.Npc.WindurstWoods.TihPikeh)}");
        var trade = C2MHandler(d, 2, N3Trade, QC.Zone.HeavensTower, QC.Npc.HeavensTower.Zubaba);
        Fake T(int status, bool match)
        {
            var f = F(status, done: id);
            f.TradeResult = match;
            return f;
        }
        Check($"c3 N3 {L}: section 3: Zubaba's trade: the daggers at 3: 151 (progress); at 0 or 4, or no match: " +
              "nothing",
              "ProgressEvent(151)|null|null|null",
              $"{C3NReply(trade, T(3, true))}|{C3NReply(trade, T(0, true))}|{C3NReply(trade, T(4, true))}|" +
              $"{C3NReply(trade, T(3, false))}");
        Check($"c3 N3 {L}: section 3: Zubaba: 0: 257 (progress, the dagger in slot 1); 3: 150 (event, the same); 1, " +
              "2: nothing",
              $"ProgressEvent(257,0,{dagger})|Event(150,0,{dagger})|null|null",
              string.Join("|", new[] { 0, 3, 1, 2 }.Select(s => R(QC.Npc.HeavensTower.Zubaba, s, 2, id))));
        var f151 = C2MHandler(d, 2, N3Fin, QC.Zone.HeavensTower, id: 151);
        var failed151 = F(3, done: id);
        failed151.CompleteResult = false;
        Check($"c3 N3 {L}: 151: completes, then takes the trade; complete false: the trade kept; 257: status 3",
              $"Mission.Complete(),Trade.Complete()|Mission.Complete()|Player.SetMissionStatus({w},3)",
              $"{C2MRun(f151, F(3, done: id))}|{C2MRun(f151, failed151)}|" +
              $"{C2MRun(C2MHandler(d, 2, N3Fin, QC.Zone.HeavensTower, id: 257), F(0, done: id))}");
    }

    // the Cermet Door of a Jeuno mission: a trade of the key at its status plays the cutscene; the trigger plays it
    // with the key item, else the door text at priority 1000; the finish sets the next status and turns the key
    static void N3CermetDoor(string label, MissionScriptDefinition d, int section, string door, int status, int cs,
                             Func<int, Fake> make, int log, string finishCalls, string finishNoKey)
    {
        var ldt = QC.Zone.LowerDelkfuttsTower;
        const int ki = QC.KeyItem.DelkfuttKey;
        var trade = C2MHandler(d, section, N3Trade, ldt, door);
        var trig = C2MHandler(d, section, N3Trig, ldt, door);
        Fake T(int s, bool match)
        {
            var f = make(s);
            f.TradeResult = match;
            return f;
        }
        Fake K(int s, bool key)
        {
            var f = make(s);
            if (key) f.KeyItems[ki] = true;
            return f;
        }
        Check($"c3 N3 {label}: {door}'s trade: the key at {status}: cutscene {cs} (progress); at {status - 1} or " +
              $"{status + 1}, or no match: nothing",
              $"ProgressCutscene({cs})|null|null|null",
              $"{C3NReply(trade, T(status, true))}|{C3NReply(trade, T(status - 1, true))}|" +
              $"{C3NReply(trade, T(status + 1, true))}|{C3NReply(trade, T(status, false))}");
        Check($"c3 N3 {label}: {door} at {status}: with the key item cutscene {cs} (progress), without it " +
              $"THE_DOOR_IS_FIRMLY_SHUT_OPEN_KEY at priority 1000; at {status - 1} and {status + 1}: nothing",
              $"ProgressCutscene({cs})|MessageSpecial({QC.Text.LowerDelkfuttsTower.THE_DOOR_IS_FIRMLY_SHUT_OPEN_KEY})" +
              ":setPriority(1000)|null|null",
              $"{C3NReply(trig, K(status, true))}|{C3NReply(trig, K(status, false))}|" +
              $"{C3NReply(trig, K(status - 1, true))}|{C3NReply(trig, K(status + 1, true))}");
        var fin = C2MHandler(d, section, N3Fin, ldt, id: cs);
        Check($"c3 N3 {label}: cutscene {cs}'s finish: status {status + 1}; without the key item it is given and the " +
              "trade taken; with it, nothing more",
              $"{finishNoKey}|{finishCalls}",
              $"{C2MRun(fin, K(status, false))}|{C2MRun(fin, K(status, true))}");
    }

    static void RunC3N3ANewJourney()
    {
        const string L = "a new journey";
        var m = new QC.Missions.Windurst.ANewJourney();
        var d = m.Compile();
        int w = QC.MissionLog.Windurst, id = QC.Mission.Windurst.ANewJourney, none = QC.Mission.Windurst.None;
        const int summons = QC.KeyItem.StarCrestedSummons1, letter = QC.KeyItem.LetterToTheAmbassador;
        Check($"c3 N3 {L}: clean, its key and LegacyPath, 5000 gil and rank 4",
              $"|{QC.MissionKey.Windurst.ANewJourney}|scripts/missions/windurst/3_3_A_New_Journey|5000/4//",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.Reward.Gil}/" +
              $"{d.Reward.Rank}/{d.Reward.RankPoints}/{d.Reward.KeyItem}");
        Fake F(int status = 0, int current = -1, int nation = 2, params int[] done) =>
            N3Fake(MissionLog.Windurst, id, current < 0 ? id : current, status, nation, 0, done);
        N3Section1(L, d, MissionLog.Windurst, id, none, QC.Mission.Windurst.TheThreeKingdoms, notDone: true);
        Check($"c3 N3 {L}: section 2 holds while this mission is current only", "True False",
              $"{d.States[1].IsMissionMatch(F())} {d.States[1].IsMissionMatch(F(current: none))}");
        int magicite = QC.Mission.Windurst.Magicite;
        Check($"c3 N3 {L}: section 3: no mission current, a Windurstian, this one completed, Magicite not; fails " +
              "with Magicite completed, for a Bastokan, with a mission current, before completion",
              "True False False False False",
              $"{d.States[2].IsMissionMatch(F(current: none, done: id))} " +
              $"{d.States[2].IsMissionMatch(F(current: none, done: new[] { id, magicite }))} " +
              $"{d.States[2].IsMissionMatch(F(current: none, nation: 1, done: id))} " +
              $"{d.States[2].IsMissionMatch(F(done: id))} {d.States[2].IsMissionMatch(F(current: none))}");
        N3WindurstAccept(L, d, id, 12, $",Player.GiveKeyItem({summons})");
        string R(ushort zone, string npc, int status) =>
            C3NReply(C2MHandler(d, 1, N3Trig, zone, npc), F(status));
        var ht = QC.Zone.HeavensTower;
        Check($"c3 N3 {L}: Heaven's Tower: _6q2 at 0: 153 (progress), at 1 nothing; Kupipi at 0: 155 (event), at 1 " +
              "nothing; at 1 Foo Beibo 162, Rhy Epocan 160, Ufu Koromoa 161 (events), at 0 and 2 nothing",
              "ProgressEvent(153)|null|Event(155)|null|Event(162)|null|null|Event(160)|null|null|Event(161)|null|null",
              $"{R(ht, QC.Npc.HeavensTower._6q2, 0)}|{R(ht, QC.Npc.HeavensTower._6q2, 1)}|" +
              $"{R(ht, QC.Npc.HeavensTower.Kupipi, 0)}|{R(ht, QC.Npc.HeavensTower.Kupipi, 1)}|" +
              string.Join("|", new[] { QC.Npc.HeavensTower.FooBeibo, QC.Npc.HeavensTower.RhyEpocan,
                                       QC.Npc.HeavensTower.UfuKoromoa }
                  .SelectMany(n => new[] { R(ht, n, 1), R(ht, n, 0), R(ht, n, 2) })));
        Check($"c3 N3 {L}: 153's finish: status 1, the summons taken, the letter given",
              $"Player.SetMissionStatus({w},1),Player.DelKeyItem({summons}),Player.GiveKeyItem({letter})",
              C2MRun(C2MHandler(d, 1, N3Fin, ht, id: 153), F(0)));
        N3CermetDoor(L, d, 1, QC.Npc.LowerDelkfuttsTower._540, 2, 2, s => F(s), w,
                     $"Player.SetMissionStatus({w},3)",
                     $"Player.SetMissionStatus({w},3),Trade.Complete(),Player.GiveKeyItem({QC.KeyItem.DelkfuttKey})");
        var rg = QC.Zone.RuludeGardens;
        Check($"c3 N3 {L}: the Embassy Door _6r8 at 4: 40 (progress), at 3 and 5 nothing; Pakh Jatalfih: 1: 43 " +
              "(progress), 2: 68 (event), 3: 141 (progress), 0 and 4: nothing",
              "ProgressEvent(40)|null|null|ProgressEvent(43)|Event(68)|ProgressEvent(141)|null|null",
              $"{R(rg, QC.Npc.RuludeGardens._6r8, 4)}|{R(rg, QC.Npc.RuludeGardens._6r8, 3)}|" +
              $"{R(rg, QC.Npc.RuludeGardens._6r8, 5)}|" +
              string.Join("|", new[] { 1, 2, 3, 0, 4 }.Select(s => R(rg, QC.Npc.RuludeGardens.PakhJatalfih, s))));
        string Fin(int csid) => C2MRun(C2MHandler(d, 1, N3Fin, rg, id: csid), F(1));
        Check($"c3 N3 {L}: 40 completes; 43: status 2, the letter taken; 141: status 4",
              $"Mission.Complete()|Player.SetMissionStatus({w},2),Player.DelKeyItem({letter})|" +
              $"Player.SetMissionStatus({w},4)",
              $"{Fin(40)}|{Fin(43)}|{Fin(141)}");
        var wd = QC.Zone.WindurstWoods;
        Check($"c3 N3 {L}: Windurst Woods: Miiri-Wohri 196, Rakoh Buuma 193, Sola Jaab 195, Tih Pikeh 194 (events)",
              "Event(196)|Event(193)|Event(195)|Event(194)",
              $"{R(wd, QC.Npc.WindurstWoods.MiiriWohri, 0)}|{R(wd, QC.Npc.WindurstWoods.RakohBuuma, 0)}|" +
              $"{R(wd, QC.Npc.WindurstWoods.SolaJaab, 0)}|{R(wd, QC.Npc.WindurstWoods.TihPikeh, 0)}");
        Check($"c3 N3 {L}: section 3: Pakh Jatalfih 54 over his default", "Event(54):replaceDefault()",
              C3NReply(C2MHandler(d, 2, N3Trig, rg, QC.Npc.RuludeGardens.PakhJatalfih), F(0, none, done: id)));
    }

    static void RunC3N3TheFourMusketeers()
    {
        const string L = "four musketeers";
        var m = new QC.Missions.Bastok.TheFourMusketeers();
        var d = m.Compile();
        int b = QC.MissionLog.Bastok, id = QC.Mission.Bastok.TheFourMusketeers, none = QC.Mission.Bastok.None;
        Check($"c3 N3 {L}: clean, its key and LegacyPath, 350 rank points",
              $"|{QC.MissionKey.Bastok.TheFourMusketeers}|scripts/missions/bastok/3_1_The_Four_Musketeers|350///",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.Reward.RankPoints}/" +
              $"{d.Reward.Gil}/{d.Reward.Rank}/{d.Reward.KeyItem}");
        Fake F(int status = 0, int current = -1, int option = 0, int prev = 0)
        {
            var f = N3Fake(MissionLog.Bastok, id, current < 0 ? id : current, status, 1, option);
            f.PrevZone = prev;
            return f;
        }
        N3Section1(L, d, MissionLog.Bastok, id, none, QC.Mission.Bastok.TheEmissary, notDone: false);
        Check($"c3 N3 {L}: section 2 holds while this mission is current only", "True False",
              $"{d.States[1].IsMissionMatch(F())} {d.States[1].IsMissionMatch(F(current: none))}");
        N3BastokAccept(L, d, id, 10);
        string R(ushort zone, string npc, int status) => C3NReply(C2MHandler(d, 1, N3Trig, zone, npc), F(status));
        Check($"c3 N3 {L}: Cleades, Rashid, Malduc, Argus: 1002 (progress) at any status (0, 5)",
              "ProgressEvent(1002)|ProgressEvent(1002)|ProgressEvent(1002)|ProgressEvent(1002)|ProgressEvent(1002)|" +
              "ProgressEvent(1002)|ProgressEvent(1002)|ProgressEvent(1002)",
              string.Join("|", new[] { (QC.Zone.BastokMarkets, QC.Npc.BastokMarkets.Cleades),
                                       (QC.Zone.BastokMines, QC.Npc.BastokMines.Rashid),
                                       (QC.Zone.Metalworks, QC.Npc.Metalworks.Malduc),
                                       (QC.Zone.PortBastok, QC.Npc.PortBastok.Argus) }
                  .SelectMany(p => new[] { R(p.Item1, p.Item2, 0), R(p.Item1, p.Item2, 5) })));
        var mw = QC.Zone.Metalworks;
        Check($"c3 N3 {L}: Ayame and Naji above 0 (1, 22): 718 and 717 (events), at 0 nothing; Iron Eater: 0: 715 " +
              "(progress), 1 and 2: 716 (event)",
              "Event(718)|Event(718)|null|Event(717)|Event(717)|null|ProgressEvent(715)|Event(716)|Event(716)",
              $"{R(mw, QC.Npc.Metalworks.Ayame, 1)}|{R(mw, QC.Npc.Metalworks.Ayame, 22)}|" +
              $"{R(mw, QC.Npc.Metalworks.Ayame, 0)}|{R(mw, QC.Npc.Metalworks.Naji, 1)}|" +
              $"{R(mw, QC.Npc.Metalworks.Naji, 22)}|{R(mw, QC.Npc.Metalworks.Naji, 0)}|" +
              $"{R(mw, QC.Npc.Metalworks.IronEater, 0)}|{R(mw, QC.Npc.Metalworks.IronEater, 1)}|" +
              $"{R(mw, QC.Npc.Metalworks.IronEater, 2)}");
        var f715 = C2MHandler(d, 1, N3Fin, mw, id: 715);
        Check($"c3 N3 {L}: 715's finish: option 0 sets status 1; options 1 and -1: nothing",
              $"Player.SetMissionStatus({b},1)||",
              $"{C2MRun(f715, F(0))}|{C2MRun(f715, F(0, option: 1))}|{C2MRun(f715, F(0, option: -1))}");
        var death = C2MHandler(d, 1, QuestHandlerKind.OnMobDeath, QC.Zone.Beadeaux, QC.Npc.Beadeaux.CopperQuadav);
        Check($"c3 N3 {L}: a Copper Quadav's death: 2 to 3, 21 to 22; at 1 and 22: nothing",
              $"Player.SetMissionStatus({b},3)|Player.SetMissionStatus({b},22)||",
              $"{C2MRun(death, F(2))}|{C2MRun(death, F(21))}|{C2MRun(death, F(1))}|{C2MRun(death, F(22))}");
        var zin = C2MHandler(d, 1, QuestHandlerKind.OnZoneIn, QC.Zone.Beadeaux, id: 1);
        Check($"c3 N3 {L}: Beadeaux zone-in: 1: 120; 0, 2: nothing; 120's finish: status 2 and set down at " +
              "-297 1 96, " +
              "rotation 1",
              $"Event(120)|null|null|Player.SetMissionStatus({b},2),Player.SetPos(-297,1,96,1)",
              $"{C3NReply(zin, F(1))}|{C3NReply(zin, F(0))}|{C3NReply(zin, F(2))}|" +
              $"{C2MRun(C2MHandler(d, 1, N3Fin, QC.Zone.Beadeaux, id: 120), F(1))}");
        var pz = C2MHandler(d, 1, QuestHandlerKind.OnZoneIn, QC.Zone.PashhowMarshlands, id: 1);
        int bd = QC.Zone.Beadeaux;
        Check($"c3 N3 {L}: Pashhow zone-in from Beadeaux: 2 and 21: 10; 22: 11; 1 and 23: nothing; from another " +
              "zone (Beadeaux - 1, + 1, none) at 2 and 22: nothing",
              "Event(10)|Event(10)|Event(11)|null|null|null|null|null|null",
              $"{C3NReply(pz, F(2, prev: bd))}|{C3NReply(pz, F(21, prev: bd))}|{C3NReply(pz, F(22, prev: bd))}|" +
              $"{C3NReply(pz, F(1, prev: bd))}|{C3NReply(pz, F(23, prev: bd))}|{C3NReply(pz, F(2, prev: bd - 1))}|" +
              $"{C3NReply(pz, F(22, prev: bd + 1))}|{C3NReply(pz, F(2))}|{C3NReply(pz, F(22))}");
        var f11 = C2MHandler(d, 1, N3Fin, QC.Zone.PashhowMarshlands, id: 11);
        var failed = F(22);
        failed.CompleteResult = false;
        Check($"c3 N3 {L}: 10's finish: set down at 578 25 -376, rotation 126; 11's: completes, then the same; " +
              "complete false: stays",
              "Player.SetPos(578,25,-376,126)|Mission.Complete(),Player.SetPos(578,25,-376,126)|Mission.Complete()",
              $"{C2MRun(C2MHandler(d, 1, N3Fin, QC.Zone.PashhowMarshlands, id: 10), F(5))}|" +
              $"{C2MRun(f11, F(22))}|{C2MRun(f11, failed)}");
    }

    static void RunC3N3ToTheForsakenMines()
    {
        const string L = "to the forsaken mines";
        var m = new QC.Missions.Bastok.ToTheForsakenMines();
        var d = m.Compile();
        int id = QC.Mission.Bastok.ToTheForsakenMines, none = QC.Mission.Bastok.None;
        Check($"c3 N3 {L}: clean, its key and LegacyPath, 400 rank points",
              $"|{QC.MissionKey.Bastok.ToTheForsakenMines}|scripts/missions/bastok/3_2_To_the_Forsaken_Mines|400///",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.Reward.RankPoints}/" +
              $"{d.Reward.Gil}/{d.Reward.Rank}/{d.Reward.KeyItem}");
        Fake F(int status = 0, int current = -1, bool match = false, params int[] done)
        {
            var f = N3Fake(MissionLog.Bastok, id, current < 0 ? id : current, status, 1, 0, done);
            f.TradeResult = match;
            return f;
        }
        N3Section1(L, d, MissionLog.Bastok, id, none, QC.Mission.Bastok.TheEmissary, notDone: false);
        Check($"c3 N3 {L}: section 2 holds while this mission is current only", "True False",
              $"{d.States[1].IsMissionMatch(F())} {d.States[1].IsMissionMatch(F(current: none))}");
        N3BastokAccept(L, d, id, 11);
        foreach (var (zone, npc) in new[] { (QC.Zone.BastokMarkets, QC.Npc.BastokMarkets.Cleades),
                                           (QC.Zone.BastokMines, QC.Npc.BastokMines.Rashid),
                                           (QC.Zone.Metalworks, QC.Npc.Metalworks.Malduc),
                                           (QC.Zone.PortBastok, QC.Npc.PortBastok.Argus) })
        {
            var tr = C2MHandler(d, 1, N3Trade, zone, npc);
            var tg = C2MHandler(d, 1, N3Trig, zone, npc);
            Check($"c3 N3 {L}: {npc}: the Glocolite: 1006 (progress) once completed, 1010 before; no match: " +
                  "nothing; trigger 1002 (progress)",
                  "ProgressEvent(1006)|ProgressEvent(1010)|null|ProgressEvent(1002)",
                  $"{C3NReply(tr, F(0, match: true, done: id))}|{C3NReply(tr, F(0, match: true))}|" +
                  $"{C3NReply(tr, F(0, done: id))}|{C3NReply(tg, F(3))}");
            foreach (int csid in new[] { 1006, 1010 })
            {
                var fin = C2MHandler(d, 1, N3Fin, zone, id: csid);
                var failed = F(0);
                failed.CompleteResult = false;
                Check($"c3 N3 {L}: {npc}'s {csid} finish: completes, then takes the trade; complete false: kept",
                      "Mission.Complete(),Trade.Complete()|Mission.Complete()",
                      $"{C2MRun(fin, F(0))}|{C2MRun(fin, failed)}");
            }
        }
        Check($"c3 N3 {L}: Davyad 54 (progress)", "ProgressEvent(54)",
              C3NReply(C2MHandler(d, 1, N3Trig, QC.Zone.BastokMines, QC.Npc.BastokMines.Davyad), F(0)));
        var gm = QC.Zone.GusgenMines;
        var qmTrade = C2MHandler(d, 1, N3Trade, gm, QC.Npc.GusgenMines.qm2);
        Fake G(bool match, bool hasGlocolite = false, bool pops = true)
        {
            var f = F(0, match: match);
            if (hasGlocolite) f.Items.Add(QC.Item.Glocolite);
            f.PopResult = pops;
            return f;
        }
        string put = $"MessageSpecial({QC.Text.GusgenMines.YOU_PUT_ITEM_DOWN},{QC.Item.SliceOfHareMeat})";
        string nothing = $"MessageSpecial({QC.Text.GusgenMines.NOTHING_SEEMS_HAPPENING})";
        var popped = G(true);
        string poppedReply = C3NReply(qmTrade, popped);
        var held = G(true, hasGlocolite: true);
        string heldReply = C3NReply(qmTrade, held);
        Check($"c3 N3 {L}: qm2's trade: the meat, no Glocolite, Blind Moby pops (hide 180): the trade taken, " +
              "YOU_PUT_ITEM_DOWN with the meat; with a Glocolite (no pop tried), a failed pop, or no match: " +
              "NOTHING_SEEMS_HAPPENING",
              $"{put}|Player.PopFromQm({QC.Mob.GusgenMines.BLIND_MOBY},True,180),Trade.Complete()|{nothing}||" +
              $"{nothing}|{nothing}",
              $"{poppedReply}|{string.Join(",", popped.Calls)}|{heldReply}|{string.Join(",", held.Calls)}|" +
              $"{C3NReply(qmTrade, G(true, pops: false))}|{C3NReply(qmTrade, G(false))}");
        Check($"c3 N3 {L}: qm2's trigger: FRESH_MONSTER_TRACKS",
              $"MessageSpecial({QC.Text.GusgenMines.FRESH_MONSTER_TRACKS})",
              C3NReply(C2MHandler(d, 1, N3Trig, gm, QC.Npc.GusgenMines.qm2), F(0)));
    }

    static void RunC3N3Jeuno()
    {
        const string L = "jeuno";
        var m = new QC.Missions.Bastok.Jeuno();
        var d = m.Compile();
        int b = QC.MissionLog.Bastok, id = QC.Mission.Bastok.Jeuno, none = QC.Mission.Bastok.None;
        const int letter = QC.KeyItem.LetterToTheAmbassador;
        Check($"c3 N3 {L}: clean, its key and LegacyPath, 5000 gil and rank 4",
              $"|{QC.MissionKey.Bastok.Jeuno}|scripts/missions/bastok/3_3_Jeuno|5000/4//",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.Reward.Gil}/" +
              $"{d.Reward.Rank}/{d.Reward.RankPoints}/{d.Reward.KeyItem}");
        Fake F(int status = 0, int current = -1) => N3Fake(MissionLog.Bastok, id, current < 0 ? id : current, status);
        N3Section1(L, d, MissionLog.Bastok, id, none, QC.Mission.Bastok.TheEmissary, notDone: false);
        Check($"c3 N3 {L}: section 2 holds while this mission is current only", "True False",
              $"{d.States[1].IsMissionMatch(F())} {d.States[1].IsMissionMatch(F(current: none))}");
        N3BastokAccept(L, d, id, 12);
        string R(ushort zone, string npc, int status) => C3NReply(C2MHandler(d, 1, N3Trig, zone, npc), F(status));
        Check($"c3 N3 {L}: Cleades, Rashid, Malduc, Argus: 1002 (progress) at any status (0, 3)",
              string.Join("|", Enumerable.Repeat("ProgressEvent(1002)", 8)),
              string.Join("|", new[] { (QC.Zone.BastokMarkets, QC.Npc.BastokMarkets.Cleades),
                                       (QC.Zone.BastokMines, QC.Npc.BastokMines.Rashid),
                                       (QC.Zone.Metalworks, QC.Npc.Metalworks.Malduc),
                                       (QC.Zone.PortBastok, QC.Npc.PortBastok.Argus) }
                  .SelectMany(p => new[] { R(p.Item1, p.Item2, 0), R(p.Item1, p.Item2, 3) })));
        var mw = QC.Zone.Metalworks;
        Check($"c3 N3 {L}: Lucius at 0: 322 (progress); at 1: nothing; 322's finish: status 1, the letter",
              $"ProgressEvent(322)|null|Player.SetMissionStatus({b},1),Player.GiveKeyItem({letter})",
              $"{R(mw, QC.Npc.Metalworks.Lucius, 0)}|{R(mw, QC.Npc.Metalworks.Lucius, 1)}|" +
              $"{C2MRun(C2MHandler(d, 1, N3Fin, mw, id: 322), F(0))}");
        var rg = QC.Zone.RuludeGardens;
        Check($"c3 N3 {L}: _6r2 at 3: 38 (progress), at 2 and 4 nothing; Goggehn: 1: 41 (progress), 2: 66, 3: 139 " +
              "(events), 0 and 4: nothing",
              "ProgressEvent(38)|null|null|ProgressEvent(41)|Event(66)|Event(139)|null|null",
              $"{R(rg, QC.Npc.RuludeGardens._6r2, 3)}|{R(rg, QC.Npc.RuludeGardens._6r2, 2)}|" +
              $"{R(rg, QC.Npc.RuludeGardens._6r2, 4)}|" +
              string.Join("|", new[] { 1, 2, 3, 0, 4 }.Select(s => R(rg, QC.Npc.RuludeGardens.Goggehn, s))));
        Check($"c3 N3 {L}: 38 completes; 41: status 2, the letter taken",
              $"Mission.Complete()|Player.SetMissionStatus({b},2),Player.DelKeyItem({letter})",
              $"{C2MRun(C2MHandler(d, 1, N3Fin, rg, id: 38), F(3))}|" +
              $"{C2MRun(C2MHandler(d, 1, N3Fin, rg, id: 41), F(1))}");
        N3CermetDoor(L, d, 1, QC.Npc.LowerDelkfuttsTower._542, 2, 1, s => F(s), b,
                     $"Player.SetMissionStatus({b},3)",
                     $"Player.SetMissionStatus({b},3),Player.GiveKeyItem({QC.KeyItem.DelkfuttKey}),Trade.Complete()");
    }

    static void RunC3N3InfiltrateDavoi()
    {
        const string L = "infiltrate davoi";
        var m = new QC.Missions.Sandoria.InfiltrateDavoi();
        var d = m.Compile();
        int s = QC.MissionLog.Sandoria, id = QC.Mission.Sandoria.InfiltrateDavoi, none = QC.Mission.Sandoria.None;
        const int report = QC.KeyItem.RoyalKnightsDavoiReport;
        Check($"c3 N3 {L}: clean, its key and LegacyPath, 300 rank points",
              $"|{QC.MissionKey.Sandoria.InfiltrateDavoi}|scripts/missions/sandoria/3_1_Infiltrate_Davoi|300///",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.Reward.RankPoints}/" +
              $"{d.Reward.Gil}/{d.Reward.Rank}/{d.Reward.KeyItem}");
        Fake F(int status = 0, int current = -1, int nation = 0, params int[] done)
        {
            var f = N3Fake(MissionLog.Sandoria, id, current < 0 ? id : current, status, nation, 0, done);
            f.TextIds[QC.Text.NorthernSanDoria.ORIGINAL_MISSION_OFFSET.ToString()] = 7000;
            f.TextIds[QC.Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET.ToString()] = 8000;
            return f;
        }
        N3Section1(L, d, MissionLog.Sandoria, id, none, QC.Mission.Sandoria.JourneyAbroad, notDone: false);
        Check($"c3 N3 {L}: section 2: current and not completed; section 3: current and completed; neither when " +
              "not current",
              "True False False True False False",
              $"{d.States[1].IsMissionMatch(F())} {d.States[1].IsMissionMatch(F(done: id))} " +
              $"{d.States[1].IsMissionMatch(F(current: none))} {d.States[2].IsMissionMatch(F(done: id))} " +
              $"{d.States[2].IsMissionMatch(F())} {d.States[2].IsMissionMatch(F(current: none, done: id))}");
        N3SandoriaAccept(L, d, id, 110);
        var nsd = QC.Zone.NorthernSanDoria;
        var ssd = QC.Zone.SouthernSanDoria;
        string R(int section, ushort zone, string npc, int status) =>
            C3NReply(C2MHandler(d, section, N3Trig, zone, npc), section == 1 ? F(status) : F(status, done: id));
        Check($"c3 N3 {L}: first run: Grilau and Endracion at 0: 1029, Ambrotien 2029 (progress); at 4: their " +
              "zone's ORIGINAL_MISSION_OFFSET + 55 at priority 1000; at 1 and 3: nothing",
              "ProgressEvent(1029)|MessageText(7055):setPriority(1000)|null|null|" +
              "ProgressEvent(2029)|MessageText(8055):setPriority(1000)|null|null|" +
              "ProgressEvent(1029)|MessageText(8055):setPriority(1000)|null|null",
              string.Join("|", new[] { (nsd, QC.Npc.NorthernSanDoria.Grilau),
                                       (ssd, QC.Npc.SouthernSanDoria.Ambrotien),
                                       (ssd, QC.Npc.SouthernSanDoria.Endracion) }
                  .SelectMany(p => new[] { 0, 4, 1, 3 }.Select(st => R(1, p.Item1, p.Item2, st)))));
        var cd = QC.Zone.ChateauDoraguille;
        Check($"c3 N3 {L}: the Prince Royal's door _6h0: 4: 554, 0: 553 (progress, the report in slot 1); 2: nothing",
              $"ProgressEvent(554,0,{report})|ProgressEvent(553,0,{report})|null",
              $"{R(1, cd, QC.Npc.ChateauDoraguille._6h0, 4)}|{R(1, cd, QC.Npc.ChateauDoraguille._6h0, 0)}|" +
              $"{R(1, cd, QC.Npc.ChateauDoraguille._6h0, 2)}");
        var f554 = C2MHandler(d, 1, N3Fin, cd, id: 554);
        var failed = F(4);
        failed.CompleteResult = false;
        Check($"c3 N3 {L}: 553: status 2; 554: completes, then the report taken and 100 rank points; complete " +
              "false: nothing more",
              $"Player.SetMissionStatus({s},2)|Mission.Complete(),Player.DelKeyItem({report})," +
              "Player.AddRankPoints(100)|Mission.Complete()",
              $"{C2MRun(C2MHandler(d, 1, N3Fin, cd, id: 553), F(0))}|{C2MRun(f554, F(4))}|{C2MRun(f554, failed)}");
        var dv = QC.Zone.Davoi;
        var quem = C2MHandler(d, 1, N3Trig, dv, QC.Npc.Davoi.Quemaricond);
        var q3 = F(3);
        q3.Npc = new QuestNpcFake(1, q3.Calls) { Name = QC.Npc.Davoi.Quemaricond };
        string q3Reply = C3NReply(quem, q3);
        var q2 = F(2);
        q2.Npc = new QuestNpcFake(1, q2.Calls) { Name = QC.Npc.Davoi.Quemaricond };
        string q2Reply = C3NReply(quem, q2);
        Check($"c3 N3 {L}: Quemaricond at 3: stops his path, 117 (progress); at 2 and 4: nothing, his path kept",
              "ProgressEvent(117)|Npc.ClearPath(True)|null||null",
              $"{q3Reply}|{string.Join(",", q3.Calls)}|{q2Reply}|{string.Join(",", q2.Calls)}|" +
              $"{R(1, dv, QC.Npc.Davoi.Quemaricond, 4)}");
        var zin = C2MHandler(d, 1, QuestHandlerKind.OnZoneIn, dv, id: 1);
        var f117 = F(3);
        f117.Npc = new QuestNpcFake(1, f117.Calls) { Name = QC.Npc.Davoi.Quemaricond };
        Check($"c3 N3 {L}: Davoi zone-in at 2: 116, at 1 and 3 nothing; 116: status 3; 117: the npc walks on, status " +
              "4, the report",
              $"Event(116)|null|null|Player.SetMissionStatus({s},3)|Npc.ContinuePath()," +
              $"Player.SetMissionStatus({s},4),Player.GiveKeyItem({report})",
              $"{C3NReply(zin, F(2))}|{C3NReply(zin, F(1))}|{C3NReply(zin, F(3))}|" +
              $"{C2MRun(C2MHandler(d, 1, N3Fin, dv, id: 116), F(2))}|" +
              $"{C2MRun(C2MHandler(d, 1, N3Fin, dv, id: 117), f117)}");
        Check($"c3 N3 {L}: repeat: Grilau and Endracion at 0: ORIGINAL_MISSION_OFFSET + 60 at priority 1000, at 10: " +
              "1012, Ambrotien 2012 (progress); at 9: nothing",
              "MessageText(7060):setPriority(1000)|ProgressEvent(1012)|null|" +
              "MessageText(8060):setPriority(1000)|ProgressEvent(2012)|null|" +
              "MessageText(8060):setPriority(1000)|ProgressEvent(1012)|null",
              string.Join("|", new[] { (nsd, QC.Npc.NorthernSanDoria.Grilau),
                                       (ssd, QC.Npc.SouthernSanDoria.Ambrotien),
                                       (ssd, QC.Npc.SouthernSanDoria.Endracion) }
                  .SelectMany(p => new[] { 0, 10, 9 }.Select(st => R(2, p.Item1, p.Item2, st)))));
        Check($"c3 N3 {L}: repeat: the 1012 finishes (both zones) and 2012 complete",
              "Mission.Complete()|Mission.Complete()|Mission.Complete()",
              $"{C2MRun(C2MHandler(d, 2, N3Fin, nsd, id: 1012), F(10, done: id))}|" +
              $"{C2MRun(C2MHandler(d, 2, N3Fin, ssd, id: 1012), F(10, done: id))}|" +
              $"{C2MRun(C2MHandler(d, 2, N3Fin, ssd, id: 2012), F(10, done: id))}");

        // the '!': its position box, the char's code, the status range, and the nation it writes
        var bang = C2MHandler(d, 2, N3Trig, dv, "!");
        const int east = QC.KeyItem.EastBlockCode, south = QC.KeyItem.SouthBlockCode, north = QC.KeyItem.NorthBlockCode;
        string Bang(double x, double z, int status, int nation = 0, int has = 0)
        {
            var f = F(status, nation: nation, done: id);
            f.Npc = new QuestNpcFake(1) { Name = "!", XPos = x, ZPos = z };
            if (has != 0) f.KeyItems[has] = true;
            string reply = C3NReply(bang, f);
            return $"{reply}/{string.Join(",", f.Calls)}";
        }
        string Gave(int ki, int log, int st) => $"KeyItem({ki}):setPriority(1000)/Player.SetMissionStatus({log},{st})";
        Check($"c3 N3 {L}: the '!' at status 6 to 9: East (x 292..296, z -30..-26), South (333..337, -138..-134), " +
              "North (161..165, -20..-16): the code, the status of the char's nation's log one up",
              $"{Gave(east, 0, 7)}|{Gave(east, 0, 10)}|{Gave(east, 0, 7)}|{Gave(east, 0, 7)}|" +
              $"{Gave(south, 0, 7)}|{Gave(south, 0, 7)}|{Gave(south, 0, 7)}|" +
              $"{Gave(north, 0, 7)}|{Gave(north, 0, 7)}|{Gave(north, 0, 7)}",
              $"{Bang(294, -28, 6)}|{Bang(294, -28, 9)}|{Bang(292, -30, 6)}|{Bang(296, -26, 6)}|" +
              $"{Bang(335, -136, 6)}|{Bang(333, -138, 6)}|{Bang(337, -134, 6)}|" +
              $"{Bang(163, -18, 6)}|{Bang(161, -20, 6)}|{Bang(165, -16, 6)}");
        Check($"c3 N3 {L}: the '!' outside a box (each edge one past: x and z), at status 5 or 10, or holding that " +
              "code: nothing, no status written",
              string.Join("|", Enumerable.Repeat("null/", 16)),
              string.Join("|", new[]
              {
                  Bang(291, -28, 6), Bang(297, -28, 6), Bang(294, -31, 6), Bang(294, -25, 6),
                  Bang(332, -136, 6), Bang(338, -136, 6), Bang(335, -139, 6), Bang(335, -133, 6),
                  Bang(160, -18, 6), Bang(166, -18, 6), Bang(163, -21, 6), Bang(163, -15, 6),
                  Bang(294, -28, 5), Bang(294, -28, 10), Bang(294, -28, 6, has: east), Bang(335, -136, 6, has: south),
              }));
        Check($"c3 N3 {L}: the '!' holding the North code at its box: nothing; it writes the status of the char's " +
              "nation's log (player:getNation()), not the mission's: a Bastokan (1) and a Windurstian (2)",
              $"null/|{Gave(east, 1, 7)}|{Gave(north, 2, 9)}",
              $"{Bang(163, -18, 6, has: north)}|{Bang(294, -28, 6, nation: 1)}|{Bang(163, -18, 8, nation: 2)}");
        Check($"c3 N3 {L}: Zantaviat: 0: 102, 9: 105 (progress); 6, 10: nothing; 102: status 6; 105: status 10, the " +
              "three codes taken",
              $"ProgressEvent(102)|ProgressEvent(105)|null|null|Player.SetMissionStatus({s},6)|" +
              $"Player.SetMissionStatus({s},10),Player.DelKeyItem({east}),Player.DelKeyItem({south})," +
              $"Player.DelKeyItem({north})",
              $"{R(2, dv, QC.Npc.Davoi.Zantaviat, 0)}|{R(2, dv, QC.Npc.Davoi.Zantaviat, 9)}|" +
              $"{R(2, dv, QC.Npc.Davoi.Zantaviat, 6)}|{R(2, dv, QC.Npc.Davoi.Zantaviat, 10)}|" +
              $"{C2MRun(C2MHandler(d, 2, N3Fin, dv, id: 102), F(0, done: id))}|" +
              $"{C2MRun(C2MHandler(d, 2, N3Fin, dv, id: 105), F(9, done: id))}");
    }

    static void RunC3N3TheCrystalSpring()
    {
        const string L = "the crystal spring";
        var m = new QC.Missions.Sandoria.TheCrystalSpring();
        var d = m.Compile();
        int s = QC.MissionLog.Sandoria, id = QC.Mission.Sandoria.TheCrystalSpring, none = QC.Mission.Sandoria.None;
        Check($"c3 N3 {L}: clean, its key and LegacyPath, 400 rank points",
              $"|{QC.MissionKey.Sandoria.TheCrystalSpring}|scripts/missions/sandoria/3_2_The_Crystal_Spring|400///",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.Reward.RankPoints}/" +
              $"{d.Reward.Gil}/{d.Reward.Rank}/{d.Reward.KeyItem}");
        Fake F(int status = 0, int current = -1, bool match = false, int prev = 0, params int[] done)
        {
            var f = N3Fake(MissionLog.Sandoria, id, current < 0 ? id : current, status, 0, 0, done);
            f.TextIds[QC.Text.NorthernSanDoria.ORIGINAL_MISSION_OFFSET.ToString()] = 7000;
            f.TextIds[QC.Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET.ToString()] = 8000;
            f.TradeResult = match;
            f.PrevZone = prev;
            return f;
        }
        N3Section1(L, d, MissionLog.Sandoria, id, none, QC.Mission.Sandoria.JourneyAbroad, notDone: false);
        Check($"c3 N3 {L}: section 2: current and not completed; section 3: current and completed; neither when " +
              "not current",
              "True False False True False False",
              $"{d.States[1].IsMissionMatch(F())} {d.States[1].IsMissionMatch(F(done: id))} " +
              $"{d.States[1].IsMissionMatch(F(current: none))} {d.States[2].IsMissionMatch(F(done: id))} " +
              $"{d.States[2].IsMissionMatch(F())} {d.States[2].IsMissionMatch(F(current: none, done: id))}");
        N3SandoriaAccept(L, d, id, 111);
        var nsd = QC.Zone.NorthernSanDoria;
        var ssd = QC.Zone.SouthernSanDoria;
        var npcs = new[] { (ssd, QC.Npc.SouthernSanDoria.Ambrotien, 8000, 2030, 2031, 2013),
                           (ssd, QC.Npc.SouthernSanDoria.Endracion, 8000, 1030, 1031, 1013),
                           (nsd, QC.Npc.NorthernSanDoria.Grilau, 7000, 1030, 1031, 1013) };
        foreach (var (zone, npc, ofs, firstTrade, firstTrig, repeatTrade) in npcs)
        {
            var t1 = C2MHandler(d, 1, N3Trade, zone, npc);
            var g1 = C2MHandler(d, 1, N3Trig, zone, npc);
            var t2 = C2MHandler(d, 2, N3Trade, zone, npc);
            var g2 = C2MHandler(d, 2, N3Trig, zone, npc);
            Check($"c3 N3 {L}: {npc}: first run: the bass: {firstTrade} (progress) at any status (0, 3), no match: " +
                  $"nothing; trigger at 0: ORIGINAL_MISSION_OFFSET + 68 at priority 1000, at 2: {firstTrig} " +
                  "(progress), at 1 and 3: nothing",
                  $"ProgressEvent({firstTrade})|ProgressEvent({firstTrade})|null|" +
                  $"MessageText({ofs + 68}):setPriority(1000)|ProgressEvent({firstTrig})|null|null",
                  $"{C3NReply(t1, F(0, match: true))}|{C3NReply(t1, F(3, match: true))}|{C3NReply(t1, F(0))}|" +
                  $"{C3NReply(g1, F(0))}|{C3NReply(g1, F(2))}|{C3NReply(g1, F(1))}|{C3NReply(g1, F(3))}");
            Check($"c3 N3 {L}: {npc}: repeat: the bass: {repeatTrade} (progress) at any status (0, 5), no match: " +
                  "nothing; trigger at 0: ORIGINAL_MISSION_OFFSET + 68 at priority 1000, at 2: nothing",
                  $"ProgressEvent({repeatTrade})|ProgressEvent({repeatTrade})|null|" +
                  $"MessageText({ofs + 68}):setPriority(1000)|null",
                  $"{C3NReply(t2, F(0, match: true, done: id))}|{C3NReply(t2, F(5, match: true, done: id))}|" +
                  $"{C3NReply(t2, F(0, done: id))}|{C3NReply(g2, F(0, done: id))}|{C3NReply(g2, F(2, done: id))}");
        }
        foreach (var (zone, csid) in new[] { (ssd, 1030), (ssd, 2030), (nsd, 1030) })
        {
            Check($"c3 N3 {L}: zone {zone}'s {csid} finish: the trade taken, status 2",
                  $"Trade.Complete(),Player.SetMissionStatus({s},2)",
                  C2MRun(C2MHandler(d, 1, N3Fin, zone, id: csid), F(0)));
        }
        foreach (var (zone, csid) in new[] { (ssd, 1013), (ssd, 2013), (nsd, 1013) })
        {
            var fin = C2MHandler(d, 2, N3Fin, zone, id: csid);
            var failed = F(0, done: id);
            failed.CompleteResult = false;
            Check($"c3 N3 {L}: zone {zone}'s {csid} finish: completes, then takes the trade; complete false: kept",
                  "Mission.Complete(),Trade.Complete()|Mission.Complete()",
                  $"{C2MRun(fin, F(0, done: id))}|{C2MRun(fin, failed)}");
        }
        var cd = QC.Zone.ChateauDoraguille;
        var zin = C2MHandler(d, 1, QuestHandlerKind.OnZoneIn, cd, id: 1);
        Check($"c3 N3 {L}: Chateau zone-in from Northern San d'Oria at 2: 555; at 1 and 3, or from another zone " +
              "(Northern - 1, + 1, none): nothing",
              "Event(555)|null|null|null|null|null",
              $"{C3NReply(zin, F(2, prev: nsd))}|{C3NReply(zin, F(1, prev: nsd))}|{C3NReply(zin, F(3, prev: nsd))}|" +
              $"{C3NReply(zin, F(2, prev: nsd - 1))}|{C3NReply(zin, F(2, prev: nsd + 1))}|{C3NReply(zin, F(2))}");
        var chal = C2MHandler(d, 1, N3Trig, cd, QC.Npc.ChateauDoraguille.Chalvatot);
        Check($"c3 N3 {L}: Chalvatot at 3: 556 (progress), at 2 and 4 nothing; 555: status 3; 556 completes",
              $"ProgressEvent(556)|null|null|Player.SetMissionStatus({s},3)|Mission.Complete()",
              $"{C3NReply(chal, F(3))}|{C3NReply(chal, F(2))}|{C3NReply(chal, F(4))}|" +
              $"{C2MRun(C2MHandler(d, 1, N3Fin, cd, id: 555), F(2))}|" +
              $"{C2MRun(C2MHandler(d, 1, N3Fin, cd, id: 556), F(3))}");
    }

    static void RunC3N3AppointmentToJeuno()
    {
        const string L = "appointment to jeuno";
        var m = new QC.Missions.Sandoria.AppointmentToJeuno();
        var d = m.Compile();
        int s = QC.MissionLog.Sandoria, id = QC.Mission.Sandoria.AppointmentToJeuno, none = QC.Mission.Sandoria.None;
        const int letter = QC.KeyItem.LetterToTheAmbassador;
        Check($"c3 N3 {L}: clean, its key and LegacyPath, 5000 gil and rank 4",
              $"|{QC.MissionKey.Sandoria.AppointmentToJeuno}|scripts/missions/sandoria/3_3_Appointment_to_Jeuno|" +
              "5000/4//",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.Reward.Gil}/" +
              $"{d.Reward.Rank}/{d.Reward.RankPoints}/{d.Reward.KeyItem}");
        Fake F(int status = 0, int current = -1)
        {
            var f = N3Fake(MissionLog.Sandoria, id, current < 0 ? id : current, status, 0);
            f.TextIds[QC.Text.NorthernSanDoria.ORIGINAL_MISSION_OFFSET.ToString()] = 7000;
            f.TextIds[QC.Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET.ToString()] = 8000;
            return f;
        }
        N3Section1(L, d, MissionLog.Sandoria, id, none, QC.Mission.Sandoria.JourneyAbroad, notDone: false);
        Check($"c3 N3 {L}: section 2 holds while this mission is current only", "True False",
              $"{d.States[1].IsMissionMatch(F())} {d.States[1].IsMissionMatch(F(current: none))}");
        N3SandoriaAccept(L, d, id, 12);
        var nsd = QC.Zone.NorthernSanDoria;
        var ssd = QC.Zone.SouthernSanDoria;
        string R(ushort zone, string npc, int status) => C3NReply(C2MHandler(d, 1, N3Trig, zone, npc), F(status));
        Check($"c3 N3 {L}: Grilau, Ambrotien, Endracion at 0: their zone's ORIGINAL_MISSION_OFFSET + 74 at priority " +
              "1000; at 1: nothing",
              "MessageText(7074):setPriority(1000)|null|MessageText(8074):setPriority(1000)|null|" +
              "MessageText(8074):setPriority(1000)|null",
              string.Join("|", new[] { (nsd, QC.Npc.NorthernSanDoria.Grilau),
                                       (ssd, QC.Npc.SouthernSanDoria.Ambrotien),
                                       (ssd, QC.Npc.SouthernSanDoria.Endracion) }
                  .SelectMany(p => new[] { R(p.Item1, p.Item2, 0), R(p.Item1, p.Item2, 1) })));
        var cd = QC.Zone.ChateauDoraguille;
        Check($"c3 N3 {L}: Arsha and Chupaile at 2: 85 and 86, the Great Hall door _6h4 at 2: 537, Halver at 0: 508 " +
              "(progress); one status either side: nothing",
              "ProgressEvent(85)|null|null|ProgressEvent(86)|null|null|ProgressEvent(537)|null|null|" +
              "ProgressEvent(508)|null",
              $"{R(cd, QC.Npc.ChateauDoraguille.Arsha, 2)}|{R(cd, QC.Npc.ChateauDoraguille.Arsha, 1)}|" +
              $"{R(cd, QC.Npc.ChateauDoraguille.Arsha, 3)}|{R(cd, QC.Npc.ChateauDoraguille.Chupaile, 2)}|" +
              $"{R(cd, QC.Npc.ChateauDoraguille.Chupaile, 1)}|{R(cd, QC.Npc.ChateauDoraguille.Chupaile, 3)}|" +
              $"{R(cd, QC.Npc.ChateauDoraguille._6h4, 2)}|{R(cd, QC.Npc.ChateauDoraguille._6h4, 1)}|" +
              $"{R(cd, QC.Npc.ChateauDoraguille._6h4, 3)}|{R(cd, QC.Npc.ChateauDoraguille.Halver, 0)}|" +
              $"{R(cd, QC.Npc.ChateauDoraguille.Halver, 1)}");
        Check($"c3 N3 {L}: 508: status 2; 537: status 3, the letter",
              $"Player.SetMissionStatus({s},2)|Player.SetMissionStatus({s},3),Player.GiveKeyItem({letter})",
              $"{C2MRun(C2MHandler(d, 1, N3Fin, cd, id: 508), F(0))}|" +
              $"{C2MRun(C2MHandler(d, 1, N3Fin, cd, id: 537), F(2))}");
        var rg = QC.Zone.RuludeGardens;
        Check($"c3 N3 {L}: Nelcabrit: 3: 42, 4: 67, 5: 140 (progress), 2 and 6: nothing; the door _6r5 at 5: 39 " +
              "(progress), at 4 and 6 nothing",
              "ProgressEvent(42)|ProgressEvent(67)|ProgressEvent(140)|null|null|ProgressEvent(39)|null|null",
              string.Join("|", new[] { 3, 4, 5, 2, 6 }.Select(st => R(rg, QC.Npc.RuludeGardens.Nelcabrit, st))) + "|" +
              string.Join("|", new[] { 5, 4, 6 }.Select(st => R(rg, QC.Npc.RuludeGardens._6r5, st))));
        Check($"c3 N3 {L}: 39 completes; 42: status 4, the letter taken",
              $"Mission.Complete()|Player.SetMissionStatus({s},4),Player.DelKeyItem({letter})",
              $"{C2MRun(C2MHandler(d, 1, N3Fin, rg, id: 39), F(5))}|" +
              $"{C2MRun(C2MHandler(d, 1, N3Fin, rg, id: 42), F(3))}");
        N3CermetDoor(L, d, 1, QC.Npc.LowerDelkfuttsTower._541, 4, 0, st => F(st), s,
                     $"Player.SetMissionStatus({s},5)",
                     $"Player.SetMissionStatus({s},5),Player.GiveKeyItem({QC.KeyItem.DelkfuttKey}),Trade.Complete()");
    }
}
