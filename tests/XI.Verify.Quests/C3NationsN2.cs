// XI.Verify.Quests -- quest rework C3 N2 (docs/superpowers/plans/2026-11-07-quest-rework-c3-lanes-1.md, section 3,
// Nations): the 2-3 missions on C#, one fake check per arm no packet shows, as the Lua files had them: each section
// check on its operands, each reply on the reads it branches on, each finish on its option and the items it reads.
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    internal static void RunC3N2()
    {
        Console.WriteLine("\n== c3 N2: the 2-3 missions on C# ==");
        RunC3N2TheEmissary();
        RunC3N2TheEmissarySandoria();
        RunC3N2TheEmissaryWindurst();
        RunC3N2TheEmissarySandoria2();
        RunC3N2TheEmissaryWindurst2();
        RunC3N2JourneyToWindurst2();
        RunC3N2JourneyAbroad();
        RunC3N2TheThreeKingdomsSandoria2();
        RunC3N2BalgasRank2Mission();
        RunC3N2HorlaisRank2Mission1();
        RunC3N2JourneyToBastok2();
        RunC3N2JourneyToBastok();
        RunC3N2JourneyToWindurst();
    }

    static void RunC3N2TheEmissary()
    {
        var m = new QC.Missions.Bastok.TheEmissary();
        var d = m.Compile();
        int bastok = QC.MissionLog.Bastok, id = QC.Mission.Bastok.TheEmissary;
        const int letter = QC.KeyItem.LetterToTheConsulsBastok, report = QC.KeyItem.KindredReport;
        Check("c3 n2 emissary: clean, its key and LegacyPath, 3000 gil, rank 3, the certificate and its title",
              $"|{QC.MissionKey.Bastok.TheEmissary}|scripts/missions/bastok/2_3_0_The_Emissary|" +
              $"3000/3/{QC.KeyItem.AdventurersCertificate}/{QC.Title.CertifiedAdventurer}",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.Reward.Gil}/" +
              $"{d.Reward.Rank}/{d.Reward.KeyItem}/{d.Reward.Title}");
        Fake F(int current, int nation = 1, int option = 0, int status = 0, bool hasLetter = false,
               bool hasReport = false, params (int log, int mission)[] done)
        {
            var f = new Fake { ThisMission = (MissionLog.Bastok, id), NationId = nation, Option = option };
            f.CurrentMissions[bastok] = current;
            f.MissionStatuses[bastok] = status;
            if (hasLetter) f.KeyItems[letter] = true;
            if (hasReport) f.KeyItems[report] = true;
            foreach (var (log, mission) in done) f.CompletedMissions.Add($"{log}/{mission}");
            return f;
        }
        const int none = QC.Mission.Bastok.None;
        Check("c3 n2 emissary: section 1 holds with no current mission and a Bastokan; fails for a San d'Orian alone " +
              "and for a current mission alone (2-2, 2-3)",
              "True False False False",
              $"{d.States[0].IsMissionMatch(F(none))} {d.States[0].IsMissionMatch(F(none, nation: 0))} " +
              $"{d.States[0].IsMissionMatch(F(QC.Mission.Bastok.WadingBeasts))} " +
              $"{d.States[0].IsMissionMatch(F(id))}");
        Check("c3 n2 emissary: section 2 holds while 2-3 is current only", "True False",
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
            Check($"c3 n2 emissary: {label} 1001's finish: option 5 begins with its zone's " +
                  "YOU_ACCEPT_THE_MISSION; options 4, 6 and 0 do nothing",
                  $"Mission.Begin(),Text.Special({text})|||",
                  $"{C2MRun(h, F(none, option: 5))}|{C2MRun(h, F(none, option: 4))}|" +
                  $"{C2MRun(h, F(none, option: 6))}|{C2MRun(h, F(none))}");
            var f = F(id);
            f.TextIds[offset.ToString()] = ofs;
            Check($"c3 n2 emissary: {label}: ORIGINAL_MISSION_OFFSET + 23",
                  $"MessageSpecial({ofs + 23})", C3NReply(C2MHandler(d, 1, trig, zone, npc), f));
        }

        // Naji: the report; no letter and status 0; each of those two alone; the first-time flag by each completed
        // mission alone
        var naji = C2MHandler(d, 1, trig, QC.Zone.Metalworks, QC.Npc.Metalworks.Naji);
        int sand = QC.MissionLog.Sandoria, wind = QC.MissionLog.Windurst;
        (int, int) abroad = (sand, QC.Mission.Sandoria.JourneyAbroad);
        (int, int) kingdoms = (wind, QC.Mission.Windurst.TheThreeKingdoms);
        string good = $"MessageText({QC.Text.Metalworks.GOOD_LUCK})";
        Check("c3 n2 emissary: Naji: the Kindred Report: 714 (progress), at any status and with the letter; " +
              "no letter and status 0: 713; the letter alone or status alone: GOOD_LUCK",
              $"ProgressEvent(714)|ProgressEvent(714)|ProgressEvent(713,0,0,0,0,0,0,0,1)|{good}|{good}",
              $"{C2MReply(naji, F(id, hasReport: true))}|" +
              $"{C2MReply(naji, F(id, hasReport: true, status: 3, hasLetter: true))}|" +
              $"{C2MReply(naji, F(id))}|{C2MReply(naji, F(id, hasLetter: true))}|{C2MReply(naji, F(id, status: 1))}");
        Check("c3 n2 emissary: Naji 713's eighth parameter: 1 with neither Journey Abroad nor the Three Kingdoms " +
              "completed; 0 with either one alone or both",
              "ProgressEvent(713,0,0,0,0,0,0,0,1)|ProgressEvent(713,0,0,0,0,0,0,0,0)|" +
              "ProgressEvent(713,0,0,0,0,0,0,0,0)|ProgressEvent(713,0,0,0,0,0,0,0,0)",
              $"{C2MReply(naji, F(id))}|{C2MReply(naji, F(id, done: abroad))}|" +
              $"{C2MReply(naji, F(id, done: kingdoms))}|" +
              $"{C2MReply(naji, F(id, done: new[] { abroad, kingdoms }))}");
        var f713 = F(id);
        Check("c3 n2 emissary: 713's finish gives the letter and moves to status 1",
              $"Player.GiveKeyItem({letter}),Player.SetMissionStatus({bastok},1) 1",
              $"{C2MRun(C2MHandler(d, 1, fin, QC.Zone.Metalworks, id: 713), f713)} {f713.MissionStatus(bastok)}");
        var f714 = F(id, hasReport: true);
        var failed = F(id, hasReport: true);
        failed.CompleteResult = false;
        Check("c3 n2 emissary: 714's finish: completed, then the report taken; complete false: the report kept",
              $"Mission.Complete(),Player.DelKeyItem({report}) True False|Mission.Complete() True",
              $"{C2MRun(C2MHandler(d, 1, fin, QC.Zone.Metalworks, id: 714), f714)} " +
              $"{f714.HasCompletedMission(bastok, id)} {f714.HasKeyItem(report)}|" +
              $"{C2MRun(C2MHandler(d, 1, fin, QC.Zone.Metalworks, id: 714), failed)} {failed.HasKeyItem(report)}");

        // Northern San d'Oria
        var nsd = QC.Zone.NorthernSanDoria;
        var baraka = C2MHandler(d, 1, trig, nsd, QC.Npc.NorthernSanDoria.Baraka);
        Check("c3 n2 emissary: Baraka: status 1: 581 (progress); other status: nothing",
              "ProgressEvent(581)|null|null",
              $"{C2MReply(baraka, F(id, status: 1))}|{C2MReply(baraka, F(id))}|{C2MReply(baraka, F(id, status: 2))}");
        var helaku = C2MHandler(d, 1, trig, nsd, QC.Npc.NorthernSanDoria.Helaku);
        (int, int) sandoria2 = (bastok, QC.Mission.Bastok.TheEmissarySandoria2);
        Check("c3 n2 emissary: Helaku: status 1: 676; status 7: 537; status 11 with The Emissary Sandoria 2 " +
              "completed: 557; status 11 alone or the completed mission alone: nothing",
              "ProgressEvent(676)|ProgressEvent(537)|ProgressEvent(557)|null|null|null",
              $"{C2MReply(helaku, F(id, status: 1))}|{C2MReply(helaku, F(id, status: 7))}|" +
              $"{C2MReply(helaku, F(id, status: 11, done: sandoria2))}|{C2MReply(helaku, F(id, status: 11))}|" +
              $"{C2MReply(helaku, F(id, done: sandoria2))}|{C2MReply(helaku, F(id, status: 8))}");
        var shakir = C2MHandler(d, 1, trig, nsd, QC.Npc.NorthernSanDoria.Shakir);
        Check("c3 n2 emissary: Shakir: status 1 or 7: 556 (event); other status: nothing",
              "Event(556)|Event(556)|null|null",
              $"{C2MReply(shakir, F(id, status: 1))}|{C2MReply(shakir, F(id, status: 7))}|" +
              $"{C2MReply(shakir, F(id))}|{C2MReply(shakir, F(id, status: 2))}");
        var f537 = F(id, status: 7, hasLetter: true);
        string run537 = C2MRun(C2MHandler(d, 1, fin, nsd, id: 537), f537);
        var other537 = F(id, status: 7, hasLetter: true, option: 1);
        Check("c3 n2 emissary: 537's finish: option 0 swaps to The Emissary Sandoria 2, takes the letter, status 8; " +
              "another option: nothing",
              $"Player.DelMission({bastok},{id}),Player.AddMission({bastok},{sandoria2.Item2})," +
              $"Player.DelKeyItem({letter}),Player.SetMissionStatus({bastok},8) {sandoria2.Item2} False 8|",
              $"{run537} {f537.CurrentMission(bastok)} {f537.HasKeyItem(letter)} {f537.MissionStatus(bastok)}|" +
              $"{C2MRun(C2MHandler(d, 1, fin, nsd, id: 537), other537)}");
        var f581 = F(id, status: 1);
        Check("c3 n2 emissary: 581's finish: swaps to The Emissary Sandoria, status 2",
              $"Player.DelMission({bastok},{id}),Player.AddMission({bastok},{QC.Mission.Bastok.TheEmissarySandoria})," +
              $"Player.SetMissionStatus({bastok},2) {QC.Mission.Bastok.TheEmissarySandoria} 2",
              $"{C2MRun(C2MHandler(d, 1, fin, nsd, id: 581), f581)} {f581.CurrentMission(bastok)} " +
              $"{f581.MissionStatus(bastok)}");

        // Port Windurst
        var pw = QC.Zone.PortWindurst;
        (int, int) wind2 = (bastok, QC.Mission.Bastok.TheEmissaryWindurst2);
        foreach (var (npc, label, first, last) in new[]
                 {
                     (QC.Npc.PortWindurst.Ada, "Ada", 58, 69), (QC.Npc.PortWindurst.GoldSkull, "Gold Skull", 57, 68),
                     (QC.Npc.PortWindurst.Josef, "Josef", 59, 70),
                 })
        {
            var h = C2MHandler(d, 1, trig, pw, npc);
            Check($"c3 n2 emissary: {label}: status 7: {first} (event); the report with The Emissary Windurst 2 " +
                  $"completed: {last}; the report alone, the completed mission alone or neither: nothing",
                  $"Event({first})|Event({last})|null|null|null",
                  $"{C2MReply(h, F(id, status: 7))}|{C2MReply(h, F(id, hasReport: true, done: wind2))}|" +
                  $"{C2MReply(h, F(id, hasReport: true))}|{C2MReply(h, F(id, done: wind2))}|{C2MReply(h, F(id))}");
        }
        var melek = C2MHandler(d, 1, trig, pw, QC.Npc.PortWindurst.Melek);
        Check("c3 n2 emissary: Melek: status 1: 48; status 6: 61 (progress); status 7: 56 (event); the report with " +
              "The Emissary Windurst 2 completed: 67 (progress); the report alone, the completed mission alone or " +
              "neither: nothing",
              "ProgressEvent(48)|ProgressEvent(61)|Event(56)|ProgressEvent(67)|null|null|null",
              $"{C2MReply(melek, F(id, status: 1))}|{C2MReply(melek, F(id, status: 6))}|" +
              $"{C2MReply(melek, F(id, status: 7))}|{C2MReply(melek, F(id, hasReport: true, done: wind2))}|" +
              $"{C2MReply(melek, F(id, hasReport: true))}|{C2MReply(melek, F(id, done: wind2))}|" +
              $"{C2MReply(melek, F(id))}");
        var f48 = F(id, status: 1);
        Check("c3 n2 emissary: 48's finish: swaps to The Emissary Windurst, status 2",
              $"Player.DelMission({bastok},{id}),Player.AddMission({bastok},{QC.Mission.Bastok.TheEmissaryWindurst})," +
              $"Player.SetMissionStatus({bastok},2) {QC.Mission.Bastok.TheEmissaryWindurst} 2",
              $"{C2MRun(C2MHandler(d, 1, fin, pw, id: 48), f48)} {f48.CurrentMission(bastok)} " +
              $"{f48.MissionStatus(bastok)}");
        var f61 = F(id, status: 6, hasLetter: true);
        Check("c3 n2 emissary: 61's finish: swaps to The Emissary Windurst 2, takes the letter, status 7",
              $"Player.DelMission({bastok},{id}),Player.AddMission({bastok},{wind2.Item2})," +
              $"Player.DelKeyItem({letter}),Player.SetMissionStatus({bastok},7) {wind2.Item2} False 7",
              $"{C2MRun(C2MHandler(d, 1, fin, pw, id: 61), f61)} {f61.CurrentMission(bastok)} " +
              $"{f61.HasKeyItem(letter)} {f61.MissionStatus(bastok)}");
    }

    static void RunC3N2TheEmissarySandoria()
    {
        var m = new QC.Missions.Bastok.TheEmissarySandoria();
        var d = m.Compile();
        int bastok = QC.MissionLog.Bastok, id = QC.Mission.Bastok.TheEmissarySandoria;
        Check("c3 n2 emissary sandoria: clean, its key and LegacyPath, one section, no reward table",
              $"|{QC.MissionKey.Bastok.TheEmissarySandoria}|scripts/missions/bastok/2_3_1_The_Emissary_Sandoria|1|True",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.States.Count}|" +
              $"{d.Reward == null}");
        Fake F(int status, int current = -1, bool setting = true, bool spell = false, bool cipher = false)
        {
            var f = new Fake { ThisMission = (MissionLog.Bastok, id) };
            f.CurrentMissions[bastok] = current == -1 ? id : current;
            f.MissionStatuses[bastok] = status;
            if (setting) f.MainSettings["ENABLE_TRUST_QUESTS"] = 1;
            if (spell) f.Spells.Add(QC.Spell.Halver);
            if (cipher) f.Items.Add(QC.Item.CipherOfHalversAlterEgo);
            return f;
        }
        Check("c3 n2 emissary sandoria: the section holds while 2-3 Sandoria is current only", "True False",
              $"{d.States[0].IsMissionMatch(F(0))} " +
              $"{d.States[0].IsMissionMatch(F(0, current: QC.Mission.Bastok.TheEmissary))}");

        var trig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var halver = C2MHandler(d, 0, trig, QC.Zone.ChateauDoraguille, QC.Npc.ChateauDoraguille.Halver);
        var f266 = F(4);
        f266.TextIds[QC.Text.ChateauDoraguille.HALVER_OFFSET.ToString()] = 5000;
        var f266b = F(5);
        f266b.TextIds[QC.Text.ChateauDoraguille.HALVER_OFFSET.ToString()] = 5000;
        Check("c3 n2 emissary sandoria: Halver at status 3: 501, parameter 7 = 1 only with the setting on, no spell " +
              "and no cipher (each of those three alone gives 0)",
              "ProgressEvent(501,{[7]=1})|ProgressEvent(501,{[7]=0})|ProgressEvent(501,{[7]=0})|" +
              "ProgressEvent(501,{[7]=0})",
              $"{C3NReply(halver, F(3))}|{C3NReply(halver, F(3, setting: false))}|" +
              $"{C3NReply(halver, F(3, spell: true))}|{C3NReply(halver, F(3, cipher: true))}");
        Check("c3 n2 emissary sandoria: Halver above status 3: HALVER_OFFSET + 266; below 3: nothing",
              "MessageText(5266)|MessageText(5266)|null|null",
              $"{C3NReply(halver, f266)}|{C3NReply(halver, f266b)}|{C3NReply(halver, F(0))}|" +
              $"{C3NReply(halver, F(2))}");
        string cipher = $"Player.GiveItem({QC.Item.CipherOfHalversAlterEgo})";
        var h501 = C2MHandler(d, 0, fin, QC.Zone.ChateauDoraguille, id: 501);
        var a = F(3); var b2 = F(3, setting: false); var c = F(3, spell: true); var e = F(3, cipher: true);
        Check("c3 n2 emissary sandoria: 501's finish: status 4, and the cipher only with the setting on, no spell " +
              "and no cipher (each of those three alone: no cipher)",
              $"Player.SetMissionStatus({bastok},4),{cipher} 4|Player.SetMissionStatus({bastok},4)|" +
              "Player.SetMissionStatus(" + bastok + ",4)|Player.SetMissionStatus(" + bastok + ",4)",
              $"{C2MRun(h501, a)} {a.MissionStatus(bastok)}|{C2MRun(h501, b2)}|{C2MRun(h501, c)}|" +
              $"{C2MRun(h501, e)}");

        var vatgit = C2MHandler(d, 0, QuestHandlerKind.OnMobDeath, QC.Zone.GhelsbaOutpost,
                                QC.Npc.GhelsbaOutpost.WarchiefVatgit);
        // each death's calls and the status after it: only status 4 sets anything (the Lua's `== 4`)
        string deaths = string.Join(" ", new[] { 0, 3, 4, 5, 6, 7 }.Select(s0 =>
        {
            var f = F(s0);
            string calls = C2MRun(vatgit, f);
            return $"{calls}/{f.MissionStatus(bastok)}";
        }));
        Check("c3 n2 emissary sandoria: Warchief Vatgit's death at status 4 sets status 5; at 0, 3, 5, 6 and 7 it " +
              "calls nothing and the status stays",
              $"/0 /3 Player.SetMissionStatus({bastok},5)/5 /5 /6 /7", deaths);

        var nsd = QC.Zone.NorthernSanDoria;
        var baraka = C2MHandler(d, 0, trig, nsd, QC.Npc.NorthernSanDoria.Baraka);
        Check("c3 n2 emissary sandoria: Baraka: status 2: HERE_WITH_BUSINESS (a message); any other: 539 (progress)",
              $"MessageText({QC.Text.NorthernSanDoria.HERE_WITH_BUSINESS})|ProgressEvent(539)|ProgressEvent(539)",
              $"{C3NReply(baraka, F(2))}|{C3NReply(baraka, F(0))}|{C3NReply(baraka, F(3))}");
        var helaku = C2MHandler(d, 0, trig, nsd, QC.Npc.NorthernSanDoria.Helaku);
        Check("c3 n2 emissary sandoria: Helaku: status 2: 536; 3: HELAKU_DIALOG; 4: 542; 5: 543; 0 and 6: nothing",
              $"ProgressEvent(536)|MessageText({QC.Text.NorthernSanDoria.HELAKU_DIALOG})|ProgressEvent(542)|" +
              "ProgressEvent(543)|null|null",
              $"{C3NReply(helaku, F(2))}|{C3NReply(helaku, F(3))}|{C3NReply(helaku, F(4))}|" +
              $"{C3NReply(helaku, F(5))}|{C3NReply(helaku, F(0))}|{C3NReply(helaku, F(6))}");
        var shakir = C2MHandler(d, 0, trig, nsd, QC.Npc.NorthernSanDoria.Shakir);
        Check("c3 n2 emissary sandoria: Shakir: status 2: 556 (progress); other status: nothing",
              "ProgressEvent(556)|null|null",
              $"{C3NReply(shakir, F(2))}|{C3NReply(shakir, F(0))}|{C3NReply(shakir, F(3))}");
        var f536 = F(2);
        Check("c3 n2 emissary sandoria: 536's finish: status 3", $"Player.SetMissionStatus({bastok},3) 3",
              $"{C2MRun(C2MHandler(d, 0, fin, nsd, id: 536), f536)} {f536.MissionStatus(bastok)}");
        var h543 = C2MHandler(d, 0, fin, nsd, id: 543);
        var f543 = F(5);
        var bad = F(5);
        bad.CompleteResult = false;
        Check("c3 n2 emissary sandoria: 543's finish: completed, status 6, begins The Emissary; complete false: " +
              "nothing more",
              $"Mission.Complete(),Player.SetMissionStatus({bastok},6)," +
              $"Player.AddMission({bastok},{QC.Mission.Bastok.TheEmissary}) 6|Mission.Complete() 5",
              $"{C2MRun(h543, f543)} {f543.MissionStatus(bastok)}|{C2MRun(h543, bad)} {bad.MissionStatus(bastok)}");
    }

    static void RunC3N2TheEmissaryWindurst()
    {
        var m = new QC.Missions.Bastok.TheEmissaryWindurst();
        var d = m.Compile();
        int bastok = QC.MissionLog.Bastok, id = QC.Mission.Bastok.TheEmissaryWindurst;
        Check("c3 n2 emissary windurst: clean, its key and LegacyPath, one section, no reward table",
              $"|{QC.MissionKey.Bastok.TheEmissaryWindurst}|scripts/missions/bastok/2_3_2_The_Emissary_Windurst|1|True",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.States.Count}|" +
              $"{d.Reward == null}");
        Fake F(int status, int current = -1, bool setting = true, bool spell = false, bool cipher = false,
               int permit = 0, bool so = false, bool ds = false, bool trade = false, int rov = 0, bool complete = true)
        {
            var f = new Fake { ThisMission = (MissionLog.Bastok, id), TradeResult = trade, CompleteResult = complete };
            f.CurrentMissions[bastok] = current == -1 ? id : current;
            f.CurrentMissions[QC.MissionLog.Rov] = rov;
            f.MissionStatuses[bastok] = status;
            if (setting) f.MainSettings["ENABLE_TRUST_QUESTS"] = 1;
            if (spell) f.Spells.Add(QC.Spell.SemihLafihna);
            if (cipher) f.Items.Add(QC.Item.CipherOfSemihsAlterEgo);
            if (permit == 1) f.KeyItems[QC.KeyItem.SanDoriaTrustPermit] = true;
            if (permit == 2) f.KeyItems[QC.KeyItem.BastokTrustPermit] = true;
            if (permit == 3) f.KeyItems[QC.KeyItem.WindurstTrustPermit] = true;
            if (so) f.KeyItems[QC.KeyItem.SwordOffering] = true;
            if (ds) f.KeyItems[QC.KeyItem.DullSword] = true;
            return f;
        }
        Check("c3 n2 emissary windurst: the section holds while 2-3 Windurst is current only", "True False",
              $"{d.States[0].IsMissionMatch(F(0))} " +
              $"{d.States[0].IsMissionMatch(F(0, current: QC.Mission.Bastok.TheEmissary))}");

        var trig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var gid = QC.Zone.Giddeus;
        var uu = C2MHandler(d, 0, trig, gid, QC.Npc.Giddeus.UuZhoumo);
        Check("c3 n2 emissary windurst: Uu Zhoumo: status 4: 40; 5: 43; 6 and above: 44 (all progress); 0, 3: nothing",
              "ProgressEvent(40)|ProgressEvent(43)|ProgressEvent(44)|ProgressEvent(44)|ProgressEvent(44)|null|null",
              string.Join("|", new[] { 4, 5, 6, 7, 9, 0, 3 }.Select(s0 => C3NReply(uu, F(s0)))));
        var uuTrade = C2MHandler(d, 0, QuestHandlerKind.OnTrade, gid, QC.Npc.Giddeus.UuZhoumo);
        // a Lua quirk kept: the trade is not gated on the status, so a knife at status 2 (before Kupipi) plays 41 too
        Check("c3 n2 emissary windurst: Uu Zhoumo's trade: an Aspir Knife: 41 (progress) at status 4 and at status 2 " +
              "(no status gate); no match: nothing",
              "ProgressEvent(41)|ProgressEvent(41)|null",
              $"{C3NReply(uuTrade, F(4, trade: true))}|{C3NReply(uuTrade, F(2, trade: true))}|" +
              $"{C3NReply(uuTrade, F(4))}");
        var f40 = F(4);
        Check("c3 n2 emissary windurst: 40's finish: status 5", $"Player.SetMissionStatus({bastok},5) 5",
              $"{C2MRun(C2MHandler(d, 0, fin, gid, id: 40), f40)} {f40.MissionStatus(bastok)}");
        var f41 = F(5, ds: true);
        Check("c3 n2 emissary windurst: 41's finish: takes the trade and the Dull Sword, status 6",
              $"Trade.Complete(),Player.DelKeyItem({QC.KeyItem.DullSword}),Player.SetMissionStatus({bastok},6) 6",
              $"{C2MRun(C2MHandler(d, 0, fin, gid, id: 41), f41)} {f41.MissionStatus(bastok)}");

        var ht = QC.Zone.HeavensTower;
        var kupipi = C2MHandler(d, 0, trig, ht, QC.Npc.HeavensTower.Kupipi);
        Check("c3 n2 emissary windurst: Kupipi at status 3: 239, parameters 4 = Bastok, 6 = 1 with no permit, 7 = 1 " +
              "with neither spell nor cipher (the setting on); each permit alone, the spell alone, the cipher alone " +
              "and the setting off change only their own",
              "ProgressEvent(239,0,0,0,1,0,1,1)|ProgressEvent(239,0,0,0,1,0,0,1)|" +
              "ProgressEvent(239,0,0,0,1,0,0,1)|ProgressEvent(239,0,0,0,1,0,0,1)|" +
              "ProgressEvent(239,0,0,0,1,0,1,0)|ProgressEvent(239,0,0,0,1,0,1,0)|ProgressEvent(239)",
              string.Join("|", new[]
              {
                  F(3), F(3, permit: 1), F(3, permit: 2), F(3, permit: 3), F(3, spell: true), F(3, cipher: true),
                  F(3, setting: false),
              }.Select(f => C3NReply(kupipi, f))));
        Check("c3 n2 emissary windurst: Kupipi at 5: 240; at 6: 241 (events); at 0, 2, 4 and 7: nothing",
              "Event(240)|Event(241)|null|null|null|null",
              string.Join("|", new[] { 5, 6, 0, 2, 4, 7 }.Select(s0 => C3NReply(kupipi, F(s0)))));
        var zin = C2MHandler(d, 0, QuestHandlerKind.OnZoneIn, ht);
        Check("c3 n2 emissary windurst: Heaven's Tower zone-in: status 2: 42 (an event); any other: nothing",
              "Event(42)|null|null|null",
              $"{C3NReply(zin, F(2))}|{C3NReply(zin, F(1))}|{C3NReply(zin, F(3))}|{C3NReply(zin, F(0))}");
        var upd = C2MHandler(d, 0, QuestHandlerKind.OnEventUpdate, ht, id: 42);
        var path = F(2, rov: QC.Mission.Rov.ThePathUntraveled);
        var other = F(2, rov: QC.Mission.Rov.ThePathUntraveled + 1);
        Check("c3 n2 emissary windurst: 42's update: parameter 8 = 1 only while The Path Untraveled is the RoV mission",
              "Player.UpdateEvent(0,0,0,0,0,0,0,1)|Player.UpdateEvent(0,0,0,0,0,0,0,0)|" +
              "Player.UpdateEvent(0,0,0,0,0,0,0,0)",
              $"{C2MRun(upd, path)}|{C2MRun(upd, other)}|{C2MRun(upd, F(2))}");
        var f42 = F(2);
        Check("c3 n2 emissary windurst: 42's finish: status 3", $"Player.SetMissionStatus({bastok},3) 3",
              $"{C2MRun(C2MHandler(d, 0, fin, ht, id: 42), f42)} {f42.MissionStatus(bastok)}");
        string so1 = $"Player.SetMissionStatus({bastok},4),Player.GiveKeyItem({QC.KeyItem.SwordOffering})";
        string ciph = $",Player.GiveItem({QC.Item.CipherOfSemihsAlterEgo})";
        var h239 = C2MHandler(d, 0, fin, ht, id: 239);
        var fa = F(3); var fb = F(3, setting: false); var fc = F(3, spell: true); var fd = F(3, cipher: true);
        Check("c3 n2 emissary windurst: 239's finish: status 4, the Sword Offering, and the cipher only with the " +
              "setting on, no spell and no cipher (each of those three alone: no cipher)",
              $"{so1}{ciph} 4|{so1}|{so1}|{so1}",
              $"{C2MRun(h239, fa)} {fa.MissionStatus(bastok)}|{C2MRun(h239, fb)}|{C2MRun(h239, fc)}|" +
              $"{C2MRun(h239, fd)}");

        var helaku = C2MHandler(d, 0, trig, QC.Zone.NorthernSanDoria, QC.Npc.NorthernSanDoria.Helaku);
        Check("c3 n2 emissary windurst: Helaku: THE_EMISSARY_PLACEHOLDER (a message) at any status",
              $"MessageText({QC.Text.NorthernSanDoria.THE_EMISSARY_PLACEHOLDER})|" +
              $"MessageText({QC.Text.NorthernSanDoria.THE_EMISSARY_PLACEHOLDER})",
              $"{C3NReply(helaku, F(0))}|{C3NReply(helaku, F(6))}");
        var pw = QC.Zone.PortWindurst;
        var ada = C2MHandler(d, 0, trig, pw, QC.Npc.PortWindurst.Ada);
        var josef = C2MHandler(d, 0, trig, pw, QC.Npc.PortWindurst.Josef);
        Check("c3 n2 emissary windurst: Ada: 51; Josef: 52 (events) at any status",
              "Event(51)|Event(51)|Event(52)|Event(52)",
              $"{C3NReply(ada, F(0))}|{C3NReply(ada, F(6))}|{C3NReply(josef, F(0))}|{C3NReply(josef, F(6))}");
        var gold = C2MHandler(d, 0, trig, pw, QC.Npc.PortWindurst.GoldSkull);
        Check("c3 n2 emissary windurst: Gold Skull: the Sword Offering and no Dull Sword: 53 (progress) at any " +
              "status; else up to status 3: 50 (event); 4 to 6: 54 (progress); 7: nothing (neither key item alone " +
              "gives 53)",
              "ProgressEvent(53)|ProgressEvent(53)|Event(50)|Event(50)|Event(50)|ProgressEvent(54)|" +
              "ProgressEvent(54)|null|ProgressEvent(54)",
              string.Join("|", new[]
              {
                  F(0, so: true), F(6, so: true), F(0, so: true, ds: true), F(3), F(0, ds: true), F(4),
                  F(6), F(7), F(4, ds: true),
              }.Select(f => C3NReply(gold, f))));
        var melek = C2MHandler(d, 0, trig, pw, QC.Npc.PortWindurst.Melek);
        Check("c3 n2 emissary windurst: Melek: up to status 5: 49; at 6: 55 (progress); above: nothing",
              "ProgressEvent(49)|ProgressEvent(49)|ProgressEvent(49)|ProgressEvent(55)|null",
              string.Join("|", new[] { 0, 3, 5, 6, 7 }.Select(s0 => C3NReply(melek, F(s0)))));
        var f53 = F(0, so: true);
        Check("c3 n2 emissary windurst: 53's finish: gives the Dull Sword, takes the Sword Offering",
              $"Player.GiveKeyItem({QC.KeyItem.DullSword}),Player.DelKeyItem({QC.KeyItem.SwordOffering})",
              C2MRun(C2MHandler(d, 0, fin, pw, id: 53), f53));
        var h55 = C2MHandler(d, 0, fin, pw, id: 55);
        var f55 = F(6);
        var bad = F(6, complete: false);
        Check("c3 n2 emissary windurst: 55's finish: completed, begins The Emissary, status 7; complete false: " +
              "nothing more",
              $"Mission.Complete(),Player.AddMission({bastok},{QC.Mission.Bastok.TheEmissary}),Player." +
              $"SetMissionStatus({bastok},7) 7|Mission.Complete() 6",
              $"{C2MRun(h55, f55)} {f55.MissionStatus(bastok)}|{C2MRun(h55, bad)} {bad.MissionStatus(bastok)}");
    }

    static void RunC3N2TheEmissarySandoria2()
    {
        var m = new QC.Missions.Bastok.TheEmissarySandoria2();
        var d = m.Compile();
        int bastok = QC.MissionLog.Bastok, id = QC.Mission.Bastok.TheEmissarySandoria2;
        Check("c3 n2 emissary sandoria 2: clean, its key and LegacyPath, one section, no reward table",
              $"|{QC.MissionKey.Bastok.TheEmissarySandoria2}|" +
              "scripts/missions/bastok/2_3_3_The_Emissary_Sandoria2|1|True",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.States.Count}|" +
              $"{d.Reward == null}");
        Fake F(int status, int current = -1, bool setting = true, bool spell = false, bool cipher = false,
               bool crest = false, int win = 0, bool complete = true)
        {
            var f = new Fake { ThisMission = (MissionLog.Bastok, id), CompleteResult = complete };
            f.CurrentMissions[bastok] = current == -1 ? id : current;
            f.MissionStatuses[bastok] = status;
            if (setting) f.MainSettings["ENABLE_TRUST_QUESTS"] = 1;
            if (spell) f.Spells.Add(QC.Spell.Halver);
            if (cipher) f.Items.Add(QC.Item.CipherOfHalversAlterEgo);
            if (crest) f.KeyItems[QC.KeyItem.KindredCrest] = true;
            f.PlayerLocalVars["battlefieldWin"] = win;
            f.TextIds[QC.Text.ChateauDoraguille.HALVER_OFFSET.ToString()] = 5000;
            f.TextIds[QC.Text.NorthernSanDoria.HELAKU_DIALOG.ToString()] = 7000;
            return f;
        }
        Check("c3 n2 emissary sandoria 2: the section holds while 2-3 Sandoria 2 is current only", "True False",
              $"{d.States[0].IsMissionMatch(F(0))} " +
              $"{d.States[0].IsMissionMatch(F(0, current: QC.Mission.Bastok.TheEmissary))}");

        var trig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var halver = C2MHandler(d, 0, trig, QC.Zone.ChateauDoraguille, QC.Npc.ChateauDoraguille.Halver);
        Check("c3 n2 emissary sandoria 2: Halver at status 8: 503, parameter 7 = 1 only with the setting on, no " +
              "spell and no cipher (each of those three alone gives 0)",
              "ProgressEvent(503,{[7]=1})|ProgressEvent(503,{[7]=0})|ProgressEvent(503,{[7]=0})|" +
              "ProgressEvent(503,{[7]=0})",
              $"{C3NReply(halver, F(8))}|{C3NReply(halver, F(8, setting: false))}|" +
              $"{C3NReply(halver, F(8, spell: true))}|{C3NReply(halver, F(8, cipher: true))}");
        Check("c3 n2 emissary sandoria 2: Halver at statuses 0, 7, 9 and 10: HALVER_OFFSET + 279; at 11: nothing",
              "MessageText(5279)|MessageText(5279)|MessageText(5279)|MessageText(5279)|null",
              string.Join("|", new[] { 0, 7, 9, 10, 11 }.Select(s0 => C3NReply(halver, F(s0)))));
        string cipher = $"Player.GiveItem({QC.Item.CipherOfHalversAlterEgo})";
        var h503 = C2MHandler(d, 0, fin, QC.Zone.ChateauDoraguille, id: 503);
        var a = F(8); var b2 = F(8, setting: false); var c = F(8, spell: true); var e = F(8, cipher: true);
        Check("c3 n2 emissary sandoria 2: 503's finish: status 9, and the cipher only with the setting on, no " +
              "spell and no cipher (each of those three alone: no cipher)",
              $"Player.SetMissionStatus({bastok},9),{cipher} 9|Player.SetMissionStatus({bastok},9)|" +
              $"Player.SetMissionStatus({bastok},9)|Player.SetMissionStatus({bastok},9)",
              $"{C2MRun(h503, a)} {a.MissionStatus(bastok)}|{C2MRun(h503, b2)}|{C2MRun(h503, c)}|" +
              $"{C2MRun(h503, e)}");

        var h32001 = C2MHandler(d, 0, fin, QC.Zone.HorlaisPeak, id: 32001);
        string win9 = $"Player.GiveKeyItem({QC.KeyItem.KindredCrest}),Player.SetMissionStatus({bastok},10)";
        var w9 = F(9);
        Check("c3 n2 emissary sandoria 2: Horlais Peak 32001 at status 9 after the Rank 2 Mission win (0): the " +
              "Kindred Crest and status 10; at status 8 or 10, or with another battlefield's win (each operand " +
              "alone): nothing",
              $"{win9} 10||||",
              $"{C2MRun(h32001, w9)} {w9.MissionStatus(bastok)}|{C2MRun(h32001, F(8))}|{C2MRun(h32001, F(10))}|" +
              $"{C2MRun(h32001, F(9, win: QC.Battlefield.Rank2Mission))}|" +
              $"{C2MRun(h32001, F(9, win: QC.Battlefield.Rank2Mission2))}");

        var nsd = QC.Zone.NorthernSanDoria;
        var helaku = C2MHandler(d, 0, trig, nsd, QC.Npc.NorthernSanDoria.Helaku);
        Check("c3 n2 emissary sandoria 2: Helaku with the Kindred Crest: 545 (progress); without: " +
              "HELAKU_DIALOG + 16 (a message), at any status",
              "ProgressEvent(545)|ProgressEvent(545)|MessageText(7016)|MessageText(7016)",
              $"{C3NReply(helaku, F(9, crest: true))}|{C3NReply(helaku, F(0, crest: true))}|" +
              $"{C3NReply(helaku, F(9))}|{C3NReply(helaku, F(11))}");
        var shakir = C2MHandler(d, 0, trig, nsd, QC.Npc.NorthernSanDoria.Shakir);
        Check("c3 n2 emissary sandoria 2: Shakir: 538 (an event, not a progress event) at any status",
              "Event(538)|Event(538)|Event(538)",
              $"{C3NReply(shakir, F(0))}|{C3NReply(shakir, F(9))}|{C3NReply(shakir, F(11))}");
        var h545 = C2MHandler(d, 0, fin, nsd, id: 545);
        var f545 = F(10, crest: true);
        var bad = F(10, crest: true, complete: false);
        Check("c3 n2 emissary sandoria 2: 545's finish: completed, begins The Emissary, status 11, takes the " +
              "Kindred Crest and gives the Kindred Report; complete false: nothing more",
              $"Mission.Complete(),Player.AddMission({bastok},{QC.Mission.Bastok.TheEmissary}),Player." +
              $"SetMissionStatus({bastok},11),Player.DelKeyItem({QC.KeyItem.KindredCrest}),Player." +
              $"GiveKeyItem({QC.KeyItem.KindredReport}) 11|Mission.Complete() 10",
              $"{C2MRun(h545, f545)} {f545.MissionStatus(bastok)}|{C2MRun(h545, bad)} {bad.MissionStatus(bastok)}");
    }

    static void RunC3N2TheEmissaryWindurst2()
    {
        var m = new QC.Missions.Bastok.TheEmissaryWindurst2();
        var d = m.Compile();
        int bastok = QC.MissionLog.Bastok, id = QC.Mission.Bastok.TheEmissaryWindurst2;
        Check("c3 n2 emissary windurst 2: clean, its key and LegacyPath, one section, no reward table",
              $"|{QC.MissionKey.Bastok.TheEmissaryWindurst2}|" +
              "scripts/missions/bastok/2_3_4_The_Emissary_Windurst2|1|True",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.States.Count}|" +
              $"{d.Reward == null}");
        Fake F(int status, int current = -1, bool crest = false, int win = 0, bool complete = true)
        {
            var f = new Fake { ThisMission = (MissionLog.Bastok, id), CompleteResult = complete };
            f.CurrentMissions[bastok] = current == -1 ? id : current;
            f.MissionStatuses[bastok] = status;
            if (crest) f.KeyItems[QC.KeyItem.KindredCrest] = true;
            f.KeyItems[QC.KeyItem.DarkKey] = true;
            f.PlayerLocalVars["battlefieldWin"] = win;
            f.TextIds[QC.Text.PortWindurst.GOLD_SKULL_DIALOG.ToString()] = 5000;
            f.TextIds[QC.Text.PortWindurst.MELEK_DIALOG_A.ToString()] = 6000;
            return f;
        }
        Check("c3 n2 emissary windurst 2: the section holds while 2-3 Windurst 2 is current only", "True False",
              $"{d.States[0].IsMissionMatch(F(0))} " +
              $"{d.States[0].IsMissionMatch(F(0, current: QC.Mission.Bastok.TheEmissary))}");

        var trig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var ht = QC.Zone.HeavensTower;
        var kupipi = C2MHandler(d, 0, trig, ht, QC.Npc.HeavensTower.Kupipi);
        Check("c3 n2 emissary windurst 2: Kupipi at status 7: 242 (progress, parameters 1 to 4 = 1, 5 = Bastok); 8: " +
              "243; 9: 244 (progress); 10: 245 (events unless noted); 0, 6 and 11: nothing",
              "ProgressEvent(242,1,1,1,1,1)|Event(243)|ProgressEvent(244)|Event(245)|null|null|null",
              string.Join("|", new[] { 7, 8, 9, 10, 0, 6, 11 }.Select(s0 => C3NReply(kupipi, F(s0)))));
        var f242 = F(7);
        Check("c3 n2 emissary windurst 2: 242's finish: the Dark Key, status 8",
              $"Player.GiveKeyItem({QC.KeyItem.DarkKey}),Player.SetMissionStatus({bastok},8) 8",
              $"{C2MRun(C2MHandler(d, 0, fin, ht, id: 242), f242)} {f242.MissionStatus(bastok)}");
        var f244 = F(9);
        Check("c3 n2 emissary windurst 2: 244's finish: status 10", $"Player.SetMissionStatus({bastok},10) 10",
              $"{C2MRun(C2MHandler(d, 0, fin, ht, id: 244), f244)} {f244.MissionStatus(bastok)}");

        var h32001 = C2MHandler(d, 0, fin, QC.Zone.BalgasDais, id: 32001);
        string won = $"Player.GiveKeyItem({QC.KeyItem.KindredCrest}),Player.DelKeyItem({QC.KeyItem.DarkKey})," +
                     $"Player.SetMissionStatus({bastok},9)";
        var w8 = F(8, win: QC.Battlefield.Rank2Mission);
        Check("c3 n2 emissary windurst 2: Balga's Dais 32001 at status 8 after the Rank 2 Mission win (96): the " +
              "Kindred Crest, the Dark Key taken, status 9; at status 7 or 9, or with another battlefield's win " +
              "(each operand alone): nothing",
              $"{won} 9|||",
              $"{C2MRun(h32001, w8)} {w8.MissionStatus(bastok)}|" +
              $"{C2MRun(h32001, F(7, win: QC.Battlefield.Rank2Mission))}|" +
              $"{C2MRun(h32001, F(9, win: QC.Battlefield.Rank2Mission))}|{C2MRun(h32001, F(8))}");

        var pw = QC.Zone.PortWindurst;
        var ada = C2MHandler(d, 0, trig, pw, QC.Npc.PortWindurst.Ada);
        var josef = C2MHandler(d, 0, trig, pw, QC.Npc.PortWindurst.Josef);
        Check("c3 n2 emissary windurst 2: Ada: 63; Josef: 65 (progress) at status 7 only",
              "ProgressEvent(63)|null|null|null|ProgressEvent(65)|null|null|null",
              string.Join("|", new[] { 7, 6, 8, 0 }.Select(s0 => C3NReply(ada, F(s0)))) + "|" +
              string.Join("|", new[] { 7, 6, 8, 0 }.Select(s0 => C3NReply(josef, F(s0)))));
        var gold = C2MHandler(d, 0, trig, pw, QC.Npc.PortWindurst.GoldSkull);
        Check("c3 n2 emissary windurst 2: Gold Skull: 7: 62 (progress); 8: GOLD_SKULL_DIALOG + 27 (a message); 9: " +
              "57 (progress); 0, 6 and 10: nothing",
              "ProgressEvent(62)|MessageText(5027)|ProgressEvent(57)|null|null|null",
              string.Join("|", new[] { 7, 8, 9, 0, 6, 10 }.Select(s0 => C3NReply(gold, F(s0)))));
        var melek = C2MHandler(d, 0, trig, pw, QC.Npc.PortWindurst.Melek);
        Check("c3 n2 emissary windurst 2: Melek: 7: 64 (progress); 8: MELEK_DIALOG_A (a message); with the Kindred " +
              "Crest at any other status: 66 (progress); no crest at 9, 0 and 10: nothing; the crest does not " +
              "override status 7 or 8",
              "ProgressEvent(64)|MessageText(6000)|ProgressEvent(66)|ProgressEvent(66)|null|null|null|" +
              "ProgressEvent(64)|MessageText(6000)",
              $"{C3NReply(melek, F(7))}|{C3NReply(melek, F(8))}|{C3NReply(melek, F(9, crest: true))}|" +
              $"{C3NReply(melek, F(0, crest: true))}|{C3NReply(melek, F(9))}|{C3NReply(melek, F(0))}|" +
              $"{C3NReply(melek, F(10))}|{C3NReply(melek, F(7, crest: true))}|{C3NReply(melek, F(8, crest: true))}");
        var h66 = C2MHandler(d, 0, fin, pw, id: 66);
        var f66 = F(9, crest: true);
        var bad = F(9, crest: true, complete: false);
        Check("c3 n2 emissary windurst 2: 66's finish: completed, begins The Emissary, the crest taken, status 10, " +
              "the Kindred Report; complete false: nothing more",
              $"Mission.Complete(),Player.AddMission({bastok},{QC.Mission.Bastok.TheEmissary}),Player." +
              $"DelKeyItem({QC.KeyItem.KindredCrest}),Player.SetMissionStatus({bastok},10),Player." +
              $"GiveKeyItem({QC.KeyItem.KindredReport}) 10|Mission.Complete() 9",
              $"{C2MRun(h66, f66)} {f66.MissionStatus(bastok)}|{C2MRun(h66, bad)} {bad.MissionStatus(bastok)}");
    }

    static void RunC3N2JourneyToWindurst2()
    {
        var m = new QC.Missions.Sandoria.JourneyToWindurst2();
        var d = m.Compile();
        int sandoria = QC.MissionLog.Sandoria, id = QC.Mission.Sandoria.JourneyToWindurst2;
        Check("c3 n2 journey to windurst 2: clean, its key and LegacyPath, one section, no reward table",
              $"|{QC.MissionKey.Sandoria.JourneyToWindurst2}|" +
              "scripts/missions/sandoria/2_3_4_Journey_to_Windurst2|1|True",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.States.Count}|" +
              $"{d.Reward == null}");
        Fake F(int status, int current = -1, int win = 0, bool complete = true)
        {
            var f = new Fake { ThisMission = (MissionLog.Sandoria, id), CompleteResult = complete };
            f.CurrentMissions[sandoria] = current == -1 ? id : current;
            f.MissionStatuses[sandoria] = status;
            f.KeyItems[QC.KeyItem.DarkKey] = true;
            f.PlayerLocalVars["battlefieldWin"] = win;
            return f;
        }
        Check("c3 n2 journey to windurst 2: the section holds while 2-3 Journey to Windurst 2 is current only",
              "True False",
              $"{d.States[0].IsMissionMatch(F(0))} " +
              $"{d.States[0].IsMissionMatch(F(0, current: QC.Mission.Sandoria.JourneyAbroad))}");

        var trig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var ht = QC.Zone.HeavensTower;
        var kupipi = C2MHandler(d, 0, trig, ht, QC.Npc.HeavensTower.Kupipi);
        Check("c3 n2 journey to windurst 2: Kupipi at status 7: 242 (progress, parameters 1 to 4 = 1, 5 = 0); 8: " +
              "243; 9: 246 (progress); 10: 247 (events unless noted); 0, 6 and 11: nothing",
              "ProgressEvent(242,1,1,1,1,0)|Event(243)|ProgressEvent(246)|Event(247)|null|null|null",
              string.Join("|", new[] { 7, 8, 9, 10, 0, 6, 11 }.Select(s0 => C3NReply(kupipi, F(s0)))));
        var f242 = F(7);
        Check("c3 n2 journey to windurst 2: 242's finish: the Dark Key, status 8",
              $"Player.GiveKeyItem({QC.KeyItem.DarkKey}),Player.SetMissionStatus({sandoria},8) 8",
              $"{C2MRun(C2MHandler(d, 0, fin, ht, id: 242), f242)} {f242.MissionStatus(sandoria)}");
        var f246 = F(9);
        Check("c3 n2 journey to windurst 2: 246's finish: status 10", $"Player.SetMissionStatus({sandoria},10) 10",
              $"{C2MRun(C2MHandler(d, 0, fin, ht, id: 246), f246)} {f246.MissionStatus(sandoria)}");

        var h32001 = C2MHandler(d, 0, fin, QC.Zone.BalgasDais, id: 32001);
        string won = $"Player.GiveKeyItem({QC.KeyItem.KindredCrest}),Player.DelKeyItem({QC.KeyItem.DarkKey})," +
                     $"Player.SetMissionStatus({sandoria},9)";
        var w8 = F(8, win: QC.Battlefield.Rank2Mission);
        Check("c3 n2 journey to windurst 2: Balga's Dais 32001 at status 8 after the Rank 2 Mission win (96): the " +
              "Kindred Crest, the Dark Key taken, status 9; at status 7 or 9, or with another battlefield's win " +
              "(each operand alone): nothing",
              $"{won} 9|||",
              $"{C2MRun(h32001, w8)} {w8.MissionStatus(sandoria)}|" +
              $"{C2MRun(h32001, F(7, win: QC.Battlefield.Rank2Mission))}|" +
              $"{C2MRun(h32001, F(9, win: QC.Battlefield.Rank2Mission))}|{C2MRun(h32001, F(8))}");

        var mourices = C2MHandler(d, 0, trig, QC.Zone.WindurstWoods, QC.Npc.WindurstWoods.Mourices);
        Check("c3 n2 journey to windurst 2: Mourices at status 8: 463; 9 and 10: 467 (progress events); 0, 7 and 11: " +
              "nothing",
              "ProgressEvent(463)|ProgressEvent(467)|ProgressEvent(467)|null|null|null",
              string.Join("|", new[] { 8, 9, 10, 0, 7, 11 }.Select(s0 => C3NReply(mourices, F(s0)))));
        var h467 = C2MHandler(d, 0, fin, QC.Zone.WindurstWoods, id: 467);
        var f467 = F(9);
        var bad = F(9, complete: false);
        Check("c3 n2 journey to windurst 2: 467's finish: completed, begins Journey Abroad, the crest taken, status " +
              "11, the Kindred Report; complete false: nothing more",
              $"Mission.Complete(),Player.AddMission({sandoria},{QC.Mission.Sandoria.JourneyAbroad}),Player." +
              $"DelKeyItem({QC.KeyItem.KindredCrest}),Player.SetMissionStatus({sandoria},11),Player." +
              $"GiveKeyItem({QC.KeyItem.KindredReport}) 11|Mission.Complete() 9",
              $"{C2MRun(h467, f467)} {f467.MissionStatus(sandoria)}|{C2MRun(h467, bad)} {bad.MissionStatus(sandoria)}");
    }

    static void RunC3N2JourneyToBastok2()
    {
        var m = new QC.Missions.Sandoria.JourneyToBastok2();
        var d = m.Compile();
        int sandoria = QC.MissionLog.Sandoria, id = QC.Mission.Sandoria.JourneyToBastok2;
        Check("c3 n2 journey to bastok 2: clean, its key and LegacyPath, one section, no reward table",
              $"|{QC.MissionKey.Sandoria.JourneyToBastok2}|" +
              "scripts/missions/sandoria/2_3_3_Journey_to_Bastok2|1|True",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.States.Count}|" +
              $"{d.Reward == null}");
        Fake F(int status, int current = -1, int win = 0, bool complete = true)
        {
            var f = new Fake { ThisMission = (MissionLog.Sandoria, id), CompleteResult = complete };
            f.CurrentMissions[sandoria] = current == -1 ? id : current;
            f.MissionStatuses[sandoria] = status;
            f.PlayerLocalVars["battlefieldWin"] = win;
            return f;
        }
        Check("c3 n2 journey to bastok 2: the section holds while 2-3 Journey to Bastok 2 is current only",
              "True False",
              $"{d.States[0].IsMissionMatch(F(0))} " +
              $"{d.States[0].IsMissionMatch(F(0, current: QC.Mission.Sandoria.JourneyAbroad))}");

        var trig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var mw = QC.Zone.Metalworks;
        int[] all = { 7, 8, 9, 10, 11, 0 };
        string Row(QuestHandlerDecl h) => string.Join("|", all.Select(s0 => C3NReply(h, F(s0))));
        Check("c3 n2 journey to bastok 2: Chantain at 8, 9, 10: 217 (an event); 7, 11 and 0: nothing",
              "null|Event(217)|Event(217)|Event(217)|null|null",
              Row(C2MHandler(d, 0, trig, mw, QC.Npc.Metalworks.Chantain)));
        Check("c3 n2 journey to bastok 2: Lutia at 8, 9, 10: 214 (progress); 7, 11 and 0: nothing",
              "null|ProgressEvent(214)|ProgressEvent(214)|ProgressEvent(214)|null|null",
              Row(C2MHandler(d, 0, trig, mw, QC.Npc.Metalworks.Lutia)));
        Check("c3 n2 journey to bastok 2: Riault at 8, 9, 10: 211 (an event); 7, 11 and 0: nothing",
              "null|Event(211)|Event(211)|Event(211)|null|null",
              Row(C2MHandler(d, 0, trig, mw, QC.Npc.Metalworks.Riault)));
        Check("c3 n2 journey to bastok 2: Grohm at status 9: 426 (progress, parameter 0); any other: 427 (an event)",
              "Event(427)|Event(427)|ProgressEvent(426,0)|Event(427)|Event(427)|Event(427)",
              Row(C2MHandler(d, 0, trig, mw, QC.Npc.Metalworks.Grohm)));
        Check("c3 n2 journey to bastok 2: Pius at status 8: 355 (progress); 9, 10, 7 and 0: 356 (an event); " +
              "11: nothing",
              "Event(356)|ProgressEvent(355)|Event(356)|Event(356)|null|Event(356)",
              Row(C2MHandler(d, 0, trig, mw, QC.Npc.Metalworks.Pius)));
        Check("c3 n2 journey to bastok 2: Savae E Paleade at 8, 9, 10: 208 (an event); 11: 207 (progress); 7 and 0: " +
              "nothing",
              "null|Event(208)|Event(208)|Event(208)|ProgressEvent(207)|null",
              Row(C2MHandler(d, 0, trig, mw, QC.Npc.Metalworks.SavaeEPaleade)));

        var f355 = F(8);
        Check("c3 n2 journey to bastok 2: 355's finish: status 9", $"Player.SetMissionStatus({sandoria},9) 9",
              $"{C2MRun(C2MHandler(d, 0, fin, mw, id: 355), f355)} {f355.MissionStatus(sandoria)}");
        var f426 = F(9);
        Check("c3 n2 journey to bastok 2: 426's finish: status 10", $"Player.SetMissionStatus({sandoria},10) 10",
              $"{C2MRun(C2MHandler(d, 0, fin, mw, id: 426), f426)} {f426.MissionStatus(sandoria)}");
        var h207 = C2MHandler(d, 0, fin, mw, id: 207);
        var f207 = F(11);
        var bad = F(11, complete: false);
        Check("c3 n2 journey to bastok 2: 207's finish: completed, begins Journey Abroad, the crest taken, the " +
              "Kindred Report, status 11; complete false: nothing more",
              $"Mission.Complete(),Player.AddMission({sandoria},{QC.Mission.Sandoria.JourneyAbroad}),Player." +
              $"DelKeyItem({QC.KeyItem.KindredCrest}),Player.GiveKeyItem({QC.KeyItem.KindredReport})," +
              $"Player.SetMissionStatus({sandoria},11) 11|Mission.Complete() 11",
              $"{C2MRun(h207, f207)} {f207.MissionStatus(sandoria)}|{C2MRun(h207, bad)} {bad.MissionStatus(sandoria)}");

        var h32001 = C2MHandler(d, 0, fin, QC.Zone.WaughroonShrine, id: 32001);
        string won = $"Player.GiveKeyItem({QC.KeyItem.KindredCrest}),Player.DelKeyItem({QC.KeyItem.DarkKey})," +
                     $"Player.SetMissionStatus({sandoria},11)";
        var w10 = F(10, win: QC.Battlefield.Rank2Mission2);
        Check("c3 n2 journey to bastok 2: Waughroon Shrine 32001 at status 10 after the Rank 2 Mission 2 win (64): " +
              "the Kindred Crest, the Dark Key taken, status 11; at status 9 or 11, or with another battlefield's " +
              "win (each operand alone): nothing",
              $"{won} 11|||",
              $"{C2MRun(h32001, w10)} {w10.MissionStatus(sandoria)}|" +
              $"{C2MRun(h32001, F(9, win: QC.Battlefield.Rank2Mission2))}|" +
              $"{C2MRun(h32001, F(11, win: QC.Battlefield.Rank2Mission2))}|{C2MRun(h32001, F(10))}");
    }

    static void RunC3N2TheThreeKingdomsSandoria2()
    {
        var m = new QC.Missions.Windurst.TheThreeKingdomsSandoria2();
        var d = m.Compile();
        int windurst = QC.MissionLog.Windurst, id = QC.Mission.Windurst.TheThreeKingdomsSandoria2;
        Check("c3 n2 three kingdoms sandoria 2: clean, its key and LegacyPath, one section, no reward table",
              $"|{QC.MissionKey.Windurst.TheThreeKingdomsSandoria2}|" +
              "scripts/missions/windurst/2_3_3_The_Three_Kingdoms_Sandoria2|1|True",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.States.Count}|" +
              $"{d.Reward == null}");
        Fake F(int status, int current = -1, bool setting = true, bool spell = false, bool cipher = false,
               int win = 0, bool complete = true)
        {
            var f = new Fake { ThisMission = (MissionLog.Windurst, id), CompleteResult = complete };
            f.CurrentMissions[windurst] = current == -1 ? id : current;
            f.MissionStatuses[windurst] = status;
            if (setting) f.MainSettings["ENABLE_TRUST_QUESTS"] = 1;
            if (spell) f.Spells.Add(QC.Spell.Halver);
            if (cipher) f.Items.Add(QC.Item.CipherOfHalversAlterEgo);
            f.PlayerLocalVars["battlefieldWin"] = win;
            f.TextIds[QC.Text.ChateauDoraguille.HALVER_OFFSET.ToString()] = 5000;
            f.TextIds[QC.Text.NorthernSanDoria.KASARORO_DIALOG.ToString()] = 7000;
            return f;
        }
        Check("c3 n2 three kingdoms sandoria 2: the section holds while 2-3 Sandoria 2 is current only", "True False",
              $"{d.States[0].IsMissionMatch(F(0))} " +
              $"{d.States[0].IsMissionMatch(F(0, current: QC.Mission.Windurst.TheThreeKingdoms))}");

        var trig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var halver = C2MHandler(d, 0, trig, QC.Zone.ChateauDoraguille, QC.Npc.ChateauDoraguille.Halver);
        Check("c3 n2 three kingdoms sandoria 2: Halver at status 8: 504, parameter 7 = 1 only with the setting on, " +
              "no spell and no cipher (each of those three alone gives 0)",
              "ProgressEvent(504,{[7]=1})|ProgressEvent(504,{[7]=0})|ProgressEvent(504,{[7]=0})|" +
              "ProgressEvent(504,{[7]=0})",
              $"{C3NReply(halver, F(8))}|{C3NReply(halver, F(8, setting: false))}|" +
              $"{C3NReply(halver, F(8, spell: true))}|{C3NReply(halver, F(8, cipher: true))}");
        Check("c3 n2 three kingdoms sandoria 2: Halver at every other status (0, 7, 9, 10, 11): HALVER_OFFSET + 279",
              "MessageText(5279)|MessageText(5279)|MessageText(5279)|MessageText(5279)|MessageText(5279)",
              string.Join("|", new[] { 0, 7, 9, 10, 11 }.Select(s0 => C3NReply(halver, F(s0)))));
        string cipher = $"Player.GiveItem({QC.Item.CipherOfHalversAlterEgo})";
        var h504 = C2MHandler(d, 0, fin, QC.Zone.ChateauDoraguille, id: 504);
        var a = F(8); var b2 = F(8, setting: false); var c = F(8, spell: true); var e = F(8, cipher: true);
        Check("c3 n2 three kingdoms sandoria 2: 504's finish: status 9, and the cipher only with the setting on, no " +
              "spell and no cipher (each of those three alone: no cipher)",
              $"Player.SetMissionStatus({windurst},9),{cipher} 9|Player.SetMissionStatus({windurst},9)|" +
              $"Player.SetMissionStatus({windurst},9)|Player.SetMissionStatus({windurst},9)",
              $"{C2MRun(h504, a)} {a.MissionStatus(windurst)}|{C2MRun(h504, b2)}|{C2MRun(h504, c)}|" +
              $"{C2MRun(h504, e)}");

        var h32001 = C2MHandler(d, 0, fin, QC.Zone.HorlaisPeak, id: 32001);
        string win9 = $"Player.GiveKeyItem({QC.KeyItem.KindredCrest}),Player.DelKeyItem({QC.KeyItem.DarkKey})," +
                      $"Player.SetMissionStatus({windurst},10)";
        var w9 = F(9);
        Check("c3 n2 three kingdoms sandoria 2: Horlais Peak 32001 at status 9 after the Rank 2 Mission 1 win (0): " +
              "the Kindred Crest, the Dark Key taken and status 10; at status 8 or 10, or with another " +
              "battlefield's win (each operand alone): nothing",
              $"{win9} 10||||",
              $"{C2MRun(h32001, w9)} {w9.MissionStatus(windurst)}|{C2MRun(h32001, F(8))}|{C2MRun(h32001, F(10))}|" +
              $"{C2MRun(h32001, F(9, win: QC.Battlefield.Rank2Mission))}|" +
              $"{C2MRun(h32001, F(9, win: QC.Battlefield.Rank2Mission2))}");

        var nsd = QC.Zone.NorthernSanDoria;
        var kasaroro = C2MHandler(d, 0, trig, nsd, QC.Npc.NorthernSanDoria.Kasaroro);
        Check("c3 n2 three kingdoms sandoria 2: Kasaroro at status 8: KASARORO_DIALOG (a message); at 10: 551 " +
              "(progress); at 0, 7, 9 and 11: nothing",
              "MessageText(231:KASARORO_DIALOG)|ProgressEvent(551)|null|null|null|null",
              string.Join("|", new[] { 8, 10, 0, 7, 9, 11 }.Select(s0 => C3NReply(kasaroro, F(s0)))));
        var h551 = C2MHandler(d, 0, fin, nsd, id: 551);
        var f551 = F(10);
        var bad = F(10, complete: false);
        Check("c3 n2 three kingdoms sandoria 2: 551's finish: completed, begins The Three Kingdoms, takes the " +
              "Kindred Crest, gives the Kindred Report, status 11; complete false: nothing more",
              $"Mission.Complete(),Player.AddMission({windurst},{QC.Mission.Windurst.TheThreeKingdoms}),Player." +
              $"DelKeyItem({QC.KeyItem.KindredCrest}),Player.GiveKeyItem({QC.KeyItem.KindredReport}),Player." +
              $"SetMissionStatus({windurst},11) 11|Mission.Complete() 10",
              $"{C2MRun(h551, f551)} {f551.MissionStatus(windurst)}|{C2MRun(h551, bad)} {bad.MissionStatus(windurst)}");
    }

    static void RunC3N2BalgasRank2Mission()
    {
        var s = new QC.Battlefields.BalgasDais.Rank2Mission();
        var d = s.Data;
        Check("c3 n2 rank2 balgas: validates clean, its key and LegacyPath",
              $"|{QC.BattlefieldKey.Rank2Mission}|scripts/battlefields/Balgas_Dais/rank_2_mission",
              $"{string.Join(" | ", BattlefieldScriptValidator.Validate(s))}|{s.Key}|{s.LegacyPath}");
        Check("c3 n2 rank2 balgas: the Lua file's fields (zone, id, no exp loss, mission, trusts, players, " +
              "MAX_LEVEL, time, index, entry and exit NPCs, essential mobs)",
              "146 96 False True True 6 MAX_LEVEL 900 0 BC_Entrance Burning_Circle Black_Dragon,Searcher 0 0",
              $"{d.ZoneId} {d.BattlefieldId} {d.CanLoseExp} {d.IsMission} {d.AllowTrusts} {d.MaxPlayers} " +
              $"{d.LevelCapSetting} {d.TimeLimit} {d.Index} {string.Join(",", d.EntryNpcs)} " +
              $"{string.Join(",", d.ExitNpcs)} " +
              $"{string.Join(";", d.EssentialMobs.Select(m => string.Join(",", m)))} {d.Loot.Count} {d.Groups.Count}");
        Check("c3 n2 rank2 balgas: overrides entryRequirement and checkSkipCutscene; its coverage labels",
              "CheckSkipCutscene,EntryRequirement|hook:CheckSkipCutscene,hook:EntryRequirement,essentialMobs:1," +
              "exit:Burning_Circle",
              $"{string.Join(",", BattlefieldScript.HookNames.Where(s.Overrides))}|" +
              string.Join(",", s.CoverageLabels()));

        int sandoria = QC.MissionLog.Sandoria, bastok = QC.MissionLog.Bastok, windurst = QC.MissionLog.Windurst;
        int jtw2 = QC.Mission.Sandoria.JourneyToWindurst2, tew2 = QC.Mission.Bastok.TheEmissaryWindurst2;
        BattlefieldContextFake Q(int nation, int sCurrent = 0, int bCurrent = 0, int status = 0, bool key = false,
                                 params string[] completed)
        {
            var (q, p) = C2BContext(s, "BC_Entrance");
            p.NationId = nation;
            p.CurrentMissions[sandoria] = sCurrent;
            p.CurrentMissions[bastok] = bCurrent;
            p.MissionStatuses[nation] = status;
            if (key) p.KeyItems[QC.KeyItem.DarkKey] = true;
            foreach (var c in completed) p.CompletedMissions.Add(c);
            return q;
        }
        string Skip(BattlefieldContextFake q) => s.CheckSkipCutscene(q).ToString();
        Check("c3 n2 rank2 balgas: checkSkipCutscene: San d'Oria's Journey to Windurst 2 completed; Bastok's The " +
              "Emissary Windurst 2 completed; neither (each alone)",
              "True True False",
              $"{Skip(Q(sandoria, completed: $"{sandoria}/{jtw2}"))} " +
              $"{Skip(Q(bastok, completed: $"{bastok}/{tew2}"))} " +
              Skip(Q(sandoria)));
        Check("c3 n2 rank2 balgas: checkSkipCutscene: status 9 with San d'Oria's current; with Bastok's current; " +
              "status 8 with either current; status 9 with neither current",
              "True True False False False",
              $"{Skip(Q(sandoria, sCurrent: jtw2, status: 9))} {Skip(Q(bastok, bCurrent: tew2, status: 9))} " +
              $"{Skip(Q(sandoria, sCurrent: jtw2, status: 8))} {Skip(Q(bastok, bCurrent: tew2, status: 8))} " +
              Skip(Q(sandoria, status: 9)));
        Check("c3 n2 rank2 balgas: checkSkipCutscene reads the nation's status, not the current mission's log (a " +
              "Windurstian on San d'Oria's mission skips only with Windurst's status above 8)",
              "False True",
              $"{Skip(Q(windurst, sCurrent: jtw2, status: 0))} {Skip(Q(windurst, sCurrent: jtw2, status: 9))}");

        string Entry(BattlefieldContextFake q, bool registrant) => s.EntryRequirement(q, registrant).ToString();
        Check("c3 n2 rank2 balgas: entryRequirement: the registrant on either mission with the Dark Key; either " +
              "mission without the key; the key without either mission; a Windurstian with the key",
              "True True False False False",
              $"{Entry(Q(sandoria, sCurrent: jtw2, key: true), true)} " +
              $"{Entry(Q(bastok, bCurrent: tew2, key: true), true)} " +
              $"{Entry(Q(sandoria, sCurrent: jtw2), true)} {Entry(Q(bastok, bCurrent: tew2), true)} " +
              Entry(Q(windurst, key: true), true));
        Check("c3 n2 rank2 balgas: entryRequirement: a helper with the nation's mission 5 completed (San d'Oria, " +
              "Windurst); a registrant with only that; a helper with another log's mission 5; a helper with " +
              "neither; a helper on the mission with the key",
              "True True False False False True",
              $"{Entry(Q(sandoria, completed: $"{sandoria}/5"), false)} " +
              $"{Entry(Q(windurst, completed: $"{windurst}/5"), false)} " +
              $"{Entry(Q(sandoria, completed: $"{sandoria}/5"), true)} " +
              $"{Entry(Q(sandoria, completed: $"{bastok}/5"), false)} {Entry(Q(sandoria), false)} " +
              Entry(Q(sandoria, sCurrent: jtw2, key: true), false));
        var (rq, rp) = C2BContext(s, "BC_Entrance");
        rp.CurrentMissions[sandoria] = jtw2;
        rp.KeyItems[QC.KeyItem.DarkKey] = true;
        Check("c3 n2 rank2 balgas: checkRequirements reaches the override (no items, no trade); refuses a trade",
              "True False",
              $"{s.CheckRequirements(rq, true)} {s.CheckRequirements(C2BWithTrade(rq, s), true)}");
    }

    static void RunC3N2HorlaisRank2Mission1()
    {
        var s = new QC.Battlefields.HorlaisPeak.Rank2Mission1();
        var d = s.Data;
        Check("c3 n2 rank2 horlais: validates clean, its key and LegacyPath",
              $"|{QC.BattlefieldKey.Rank2Mission1}|scripts/battlefields/Horlais_Peak/rank_2_mission",
              $"{string.Join(" | ", BattlefieldScriptValidator.Validate(s))}|{s.Key}|{s.LegacyPath}");
        Check("c3 n2 rank2 horlais: the Lua file's fields (zone, id, no exp loss, mission, trusts, players, " +
              "MAX_LEVEL, time, index, entry and exit NPCs, essential mobs)",
              "139 0 False True True 6 MAX_LEVEL 900 0 BC_Entrance Burning_Circle Dread_Dragon,Spotter 0 0",
              $"{d.ZoneId} {d.BattlefieldId} {d.CanLoseExp} {d.IsMission} {d.AllowTrusts} {d.MaxPlayers} " +
              $"{d.LevelCapSetting} {d.TimeLimit} {d.Index} {string.Join(",", d.EntryNpcs)} " +
              $"{string.Join(",", d.ExitNpcs)} " +
              $"{string.Join(";", d.EssentialMobs.Select(m => string.Join(",", m)))} {d.Loot.Count} {d.Groups.Count}");
        Check("c3 n2 rank2 horlais: overrides entryRequirement and checkSkipCutscene; its coverage labels",
              "CheckSkipCutscene,EntryRequirement|hook:CheckSkipCutscene,hook:EntryRequirement,essentialMobs:1," +
              "exit:Burning_Circle",
              $"{string.Join(",", BattlefieldScript.HookNames.Where(s.Overrides))}|" +
              string.Join(",", s.CoverageLabels()));

        int sandoria = QC.MissionLog.Sandoria, bastok = QC.MissionLog.Bastok, windurst = QC.MissionLog.Windurst;
        int tks2 = QC.Mission.Windurst.TheThreeKingdomsSandoria2, es2 = QC.Mission.Bastok.TheEmissarySandoria2;
        BattlefieldContextFake Q(int nation, int wCurrent = 0, int bCurrent = 0, int status = 0,
                                 params string[] completed)
        {
            var (q, p) = C2BContext(s, "BC_Entrance");
            p.NationId = nation;
            p.CurrentMissions[windurst] = wCurrent;
            p.CurrentMissions[bastok] = bCurrent;
            p.MissionStatuses[nation] = status;
            foreach (var c in completed) p.CompletedMissions.Add(c);
            return q;
        }
        string Skip(BattlefieldContextFake q) => s.CheckSkipCutscene(q).ToString();
        Check("c3 n2 rank2 horlais: checkSkipCutscene: Windurst's The Three Kingdoms San d'Oria 2 completed; " +
              "Bastok's The Emissary San d'Oria 2 completed; neither (each alone)",
              "True True False",
              $"{Skip(Q(windurst, completed: $"{windurst}/{tks2}"))} {Skip(Q(bastok, completed: $"{bastok}/{es2}"))} " +
              Skip(Q(windurst)));
        Check("c3 n2 rank2 horlais: checkSkipCutscene: status 10 with Windurst's current; with Bastok's current; " +
              "status 9 with either current; status 10 with neither current",
              "True True False False False",
              $"{Skip(Q(windurst, wCurrent: tks2, status: 10))} {Skip(Q(bastok, bCurrent: es2, status: 10))} " +
              $"{Skip(Q(windurst, wCurrent: tks2, status: 9))} {Skip(Q(bastok, bCurrent: es2, status: 9))} " +
              Skip(Q(windurst, status: 10)));
        Check("c3 n2 rank2 horlais: checkSkipCutscene reads the nation's status, not the current mission's log (a " +
              "San d'Orian on Windurst's mission skips only with San d'Oria's status above 9)",
              "False True",
              $"{Skip(Q(sandoria, wCurrent: tks2, status: 0))} {Skip(Q(sandoria, wCurrent: tks2, status: 10))}");

        string Entry(BattlefieldContextFake q, bool registrant) => s.EntryRequirement(q, registrant).ToString();
        Check("c3 n2 rank2 horlais: entryRequirement: the registrant on either mission at status 9 and 10; at " +
              "status 8; at status 9 with neither mission current",
              "True True True False False False",
              $"{Entry(Q(windurst, wCurrent: tks2, status: 9), true)} " +
              $"{Entry(Q(bastok, bCurrent: es2, status: 9), true)} " +
              $"{Entry(Q(windurst, wCurrent: tks2, status: 10), true)} " +
              $"{Entry(Q(windurst, wCurrent: tks2, status: 8), true)} " +
              $"{Entry(Q(bastok, bCurrent: es2, status: 8), true)} " +
              Entry(Q(windurst, status: 9), true));
        Check("c3 n2 rank2 horlais: entryRequirement: a helper with the nation's mission 5 completed (San d'Oria, " +
              "Bastok); a registrant with only that; a helper with another log's mission 5; a helper with " +
              "neither; a helper on the mission at status 9",
              "True True False False False True",
              $"{Entry(Q(sandoria, completed: $"{sandoria}/5"), false)} " +
              $"{Entry(Q(bastok, completed: $"{bastok}/5"), false)} " +
              $"{Entry(Q(sandoria, completed: $"{sandoria}/5"), true)} " +
              $"{Entry(Q(sandoria, completed: $"{bastok}/5"), false)} {Entry(Q(sandoria), false)} " +
              Entry(Q(windurst, wCurrent: tks2, status: 9), false));
        Check("c3 n2 rank2 horlais: entryRequirement reads the nation's status, not the current mission's log (a " +
              "San d'Orian on Windurst's mission enters only with San d'Oria's status at 9)",
              "False True",
              $"{Entry(Q(sandoria, wCurrent: tks2, status: 8), true)} " +
              $"{Entry(Q(sandoria, wCurrent: tks2, status: 9), true)}");
        var (rq, rp) = C2BContext(s, "BC_Entrance");
        rp.NationId = windurst;
        rp.CurrentMissions[windurst] = tks2;
        rp.MissionStatuses[windurst] = 9;
        Check("c3 n2 rank2 horlais: checkRequirements reaches the override (no items, no trade); refuses a trade",
              "True False",
              $"{s.CheckRequirements(rq, true)} {s.CheckRequirements(C2BWithTrade(rq, s), true)}");
    }

    static void RunC3N2JourneyAbroad()
    {
        var m = new QC.Missions.Sandoria.JourneyAbroad();
        var d = m.Compile();
        int sandoria = QC.MissionLog.Sandoria, id = QC.Mission.Sandoria.JourneyAbroad;
        Check("c3 n2 journey abroad: clean, its key and LegacyPath, two sections, 3000 gil, rank 3, the certificate " +
              "and its title",
              $"|{QC.MissionKey.Sandoria.JourneyAbroad}|scripts/missions/sandoria/2_3_0_Journey_Abroad|2|" +
              $"3000/3/{QC.KeyItem.AdventurersCertificate}/{QC.Title.CertifiedAdventurer}",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.States.Count}|" +
              $"{d.Reward.Gil}/{d.Reward.Rank}/{d.Reward.KeyItem}/{d.Reward.Title}");
        Fake F(int status, int current = -1, int nation = 0, int option = 0, bool setting = true, bool spell = false,
               bool cipher = false, bool windurstDone = false, bool complete = true)
        {
            var f = new Fake { ThisMission = (MissionLog.Sandoria, id), CompleteResult = complete, NationId = nation,
                               Option = option };
            f.CurrentMissions[sandoria] = current == -1 ? id : current;
            f.MissionStatuses[sandoria] = status;
            if (setting) f.MainSettings["ENABLE_TRUST_QUESTS"] = 1;
            if (spell) f.Spells.Add(QC.Spell.Halver);
            if (cipher) f.Items.Add(QC.Item.CipherOfHalversAlterEgo);
            if (windurstDone) f.CompletedMissions.Add($"{sandoria}/{QC.Mission.Sandoria.JourneyToWindurst}");
            return f;
        }
        const int none = QC.Mission.Sandoria.None;
        Check("c3 n2 journey abroad: section 1 holds with no current mission and a San d'Orian; fails for a Bastokan " +
              "alone and for a current mission alone (2-3, 1-3)",
              "True False False False",
              $"{d.States[0].IsMissionMatch(F(0, current: none))} " +
              $"{d.States[0].IsMissionMatch(F(0, current: none, nation: 1))} " +
              $"{d.States[0].IsMissionMatch(F(0))} " +
              $"{d.States[0].IsMissionMatch(F(0, current: QC.Mission.Sandoria.SaveTheChildren))}");
        Check("c3 n2 journey abroad: section 2 holds while 2-3 Journey Abroad is current only", "True False",
              $"{d.States[1].IsMissionMatch(F(0))} {d.States[1].IsMissionMatch(F(0, current: none))}");

        var fin = QuestHandlerKind.OnEventFinish;
        var trig = QuestHandlerKind.OnTrigger;
        foreach (var (zone, cid) in new[]
                 {
                     (QC.Zone.SouthernSanDoria, 1009), (QC.Zone.SouthernSanDoria, 2009),
                     (QC.Zone.NorthernSanDoria, 1009)
                 })
        {
            var h = C2MHandler(d, 0, fin, zone, id: cid);
            Check($"c3 n2 journey abroad: zone {zone} {cid}'s finish: option 5 begins; options 4, 6 and 0 do nothing",
                  "Mission.Begin()|||",
                  $"{C2MRun(h, F(0, current: none, option: 5))}|{C2MRun(h, F(0, current: none, option: 4))}|" +
                  $"{C2MRun(h, F(0, current: none, option: 6))}|{C2MRun(h, F(0, current: none))}");
        }
        foreach (var (zone, npc, offset, label, ofs) in new[]
                 {
                     (QC.Zone.SouthernSanDoria, QC.Npc.SouthernSanDoria.Ambrotien,
                      QC.Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET, "Ambrotien", 3000),
                     (QC.Zone.SouthernSanDoria, QC.Npc.SouthernSanDoria.Endracion,
                      QC.Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET, "Endracion", 3000),
                     (QC.Zone.NorthernSanDoria, QC.Npc.NorthernSanDoria.Grilau,
                      QC.Text.NorthernSanDoria.ORIGINAL_MISSION_OFFSET, "Grilau", 4000),
                 })
        {
            var h = C2MHandler(d, 1, trig, zone, npc);
            Fake T(int s0)
            {
                var f = F(s0);
                f.TextIds[offset.ToString()] = ofs;
                return f;
            }
            Check($"c3 n2 journey abroad: {label} at status 0: ORIGINAL_MISSION_OFFSET + 50, any other: + 51 " +
                  "(priority 1000)",
                  string.Join("|", new[] { 0, 1, 11 }.Select(s0 =>
                      $"MessageText({ofs + (s0 == 0 ? 50 : 51)}):setPriority(1000)")),
                  string.Join("|", new[] { 0, 1, 11 }.Select(s0 => C3NReply(h, T(s0)))));
        }

        var halver = C2MHandler(d, 1, trig, QC.Zone.ChateauDoraguille, QC.Npc.ChateauDoraguille.Halver);
        Check("c3 n2 journey abroad: Halver at status 0: 505, parameter 7 = 1 only with the setting on, no spell and " +
              "no cipher (each of those three alone gives 0); with the setting off: 505 bare",
              "ProgressEvent(505,{[7]=1})|ProgressEvent(505,{[7]=0})|ProgressEvent(505,{[7]=0})|" +
              "ProgressEvent(505)",
              $"{C3NReply(halver, F(0))}|{C3NReply(halver, F(0, spell: true))}|" +
              $"{C3NReply(halver, F(0, cipher: true))}|{C3NReply(halver, F(0, setting: false))}");
        Check("c3 n2 journey abroad: Halver at status 11: 507; at 1, 2, 5, 10: 532 (progress events)",
              "ProgressEvent(507)|ProgressEvent(532)|ProgressEvent(532)|ProgressEvent(532)|ProgressEvent(532)",
              string.Join("|", new[] { 11, 1, 2, 5, 10 }.Select(s0 => C3NReply(halver, F(s0)))));
        string cipherGive = $"Player.GiveItem({QC.Item.CipherOfHalversAlterEgo})";
        string letter = $"Player.SetMissionStatus({sandoria},2)," +
                        $"Player.GiveKeyItem({QC.KeyItem.LetterToTheConsulsSandoria})";
        var h505 = C2MHandler(d, 1, fin, QC.Zone.ChateauDoraguille, id: 505);
        var a = F(0); var b2 = F(0, setting: false); var c = F(0, spell: true); var e = F(0, cipher: true);
        Check("c3 n2 journey abroad: 505's finish: status 2, the letter, and the cipher only with the setting on, no " +
              "spell and no cipher (each of those three alone: no cipher)",
              $"{letter},{cipherGive} 2|{letter}|{letter}|{letter}",
              $"{C2MRun(h505, a)} {a.MissionStatus(sandoria)}|{C2MRun(h505, b2)}|{C2MRun(h505, c)}|" +
              $"{C2MRun(h505, e)}");
        var h507 = C2MHandler(d, 1, fin, QC.Zone.ChateauDoraguille, id: 507);
        Check("c3 n2 journey abroad: 507's finish: completed, the Kindred Report taken; complete false: nothing more",
              $"Mission.Complete(),Player.DelKeyItem({QC.KeyItem.KindredReport})|Mission.Complete()",
              $"{C2MRun(h507, F(11))}|{C2MRun(h507, F(11, complete: false))}");

        var mw = QC.Zone.Metalworks;
        foreach (var (npc, label, a7, a11) in new[]
                 {
                     (QC.Npc.Metalworks.Chantain, "Chantain", 216, 218), (QC.Npc.Metalworks.Lutia, "Lutia", 213, 215),
                     (QC.Npc.Metalworks.Riault, "Riault", 210, 212)
                 })
        {
            var h = C2MHandler(d, 1, trig, mw, npc);
            Check($"c3 n2 journey abroad: {label} at status 7: {a7}; 11: {a11} (progress events); 0, 2, 6, 8, " +
                  "10: nothing",
                  $"ProgressEvent({a7})|ProgressEvent({a11})|null|null|null|null|null",
                  string.Join("|", new[] { 7, 11, 0, 2, 6, 8, 10 }.Select(s0 => C3NReply(h, F(s0)))));
        }
        var savae = C2MHandler(d, 1, trig, mw, QC.Npc.Metalworks.SavaeEPaleade);
        Check("c3 n2 journey abroad: Savae E Paleade at status 2: 204; 7: 206; 11: 209 (progress events); 0, 3, " +
              "6, 10: nothing",
              "ProgressEvent(204)|ProgressEvent(206)|ProgressEvent(209)|null|null|null|null",
              string.Join("|", new[] { 2, 7, 11, 0, 3, 6, 10 }.Select(s0 => C3NReply(savae, F(s0)))));
        var h204 = C2MHandler(d, 1, fin, mw, id: 204);
        var f204 = F(2);
        Check("c3 n2 journey abroad: 204's finish: this mission deleted, Journey to Bastok begun, status 3, the " +
              "letter taken",
              $"Player.DelMission({sandoria},{id})," +
              $"Player.AddMission({sandoria},{QC.Mission.Sandoria.JourneyToBastok})," +
              $"Player.SetMissionStatus({sandoria},3),Player.DelKeyItem({QC.KeyItem.LetterToTheConsulsSandoria}) 3",
              $"{C2MRun(h204, f204)} {f204.MissionStatus(sandoria)}");
        var h206 = C2MHandler(d, 1, fin, mw, id: 206);
        var f206 = F(7);
        Check("c3 n2 journey abroad: 206's finish: this mission deleted, Journey to Bastok 2 begun, status 8",
              $"Player.DelMission({sandoria},{id})," +
              $"Player.AddMission({sandoria},{QC.Mission.Sandoria.JourneyToBastok2})," +
              $"Player.SetMissionStatus({sandoria},8) 8",
              $"{C2MRun(h206, f206)} {f206.MissionStatus(sandoria)}");

        var ww = QC.Zone.WindurstWoods;
        foreach (var (npc, label, ev) in new[]
                 {
                     (QC.Npc.WindurstWoods.Catalia, "Catalia", 459), (QC.Npc.WindurstWoods.Erpolant, "Erpolant", 460),
                     (QC.Npc.WindurstWoods.Forine, "Forine", 461)
                 })
        {
            var h = C2MHandler(d, 1, trig, ww, npc);
            Check($"c3 n2 journey abroad: {label} at status 7 with Journey to Windurst completed: {ev} (progress); " +
                  "either condition alone: nothing",
                  $"ProgressEvent({ev})|null|null|null",
                  $"{C3NReply(h, F(7, windurstDone: true))}|{C3NReply(h, F(7))}|" +
                  $"{C3NReply(h, F(6, windurstDone: true))}|" +
                  $"{C3NReply(h, F(8, windurstDone: true))}");
        }
        var mourices = C2MHandler(d, 1, trig, ww, QC.Npc.WindurstWoods.Mourices);
        Check("c3 n2 journey abroad: Mourices at status 2: 448; 6: 462; 11: 468 (with or without Journey to Windurst " +
              "completed); 7 with it completed: 458; 7 without it, 0, 3, 5, 8, 10: nothing",
              "ProgressEvent(448)|ProgressEvent(462)|ProgressEvent(468)|ProgressEvent(468)|ProgressEvent(458)|" +
              "null|null|null|null|null|null",
              $"{C3NReply(mourices, F(2))}|{C3NReply(mourices, F(6))}|{C3NReply(mourices, F(11))}|" +
              $"{C3NReply(mourices, F(11, windurstDone: true))}|{C3NReply(mourices, F(7, windurstDone: true))}|" +
              string.Join("|", new[] { 7, 0, 3, 5, 8, 10 }.Select(s0 => C3NReply(mourices, F(s0)))));
        var h448 = C2MHandler(d, 1, fin, ww, id: 448);
        var f448 = F(2);
        Check("c3 n2 journey abroad: 448's finish: this mission deleted, Journey to Windurst begun, status 3, the " +
              "letter taken",
              $"Player.DelMission({sandoria},{id})," +
              $"Player.AddMission({sandoria},{QC.Mission.Sandoria.JourneyToWindurst})," +
              $"Player.SetMissionStatus({sandoria},3),Player.DelKeyItem({QC.KeyItem.LetterToTheConsulsSandoria}) 3",
              $"{C2MRun(h448, f448)} {f448.MissionStatus(sandoria)}");
        var h462 = C2MHandler(d, 1, fin, ww, id: 462);
        var f462 = F(6);
        Check("c3 n2 journey abroad: 462's finish: this mission deleted, Journey to Windurst 2 begun, status 7",
              $"Player.DelMission({sandoria},{id})," +
              $"Player.AddMission({sandoria},{QC.Mission.Sandoria.JourneyToWindurst2})," +
              $"Player.SetMissionStatus({sandoria},7) 7",
              $"{C2MRun(h462, f462)} {f462.MissionStatus(sandoria)}");
    }

    static void RunC3N2JourneyToBastok()
    {
        var m = new QC.Missions.Sandoria.JourneyToBastok();
        var d = m.Compile();
        int sandoria = QC.MissionLog.Sandoria, id = QC.Mission.Sandoria.JourneyToBastok;
        Check("c3 n2 journey to bastok: clean, its key and LegacyPath, one section, no reward table",
              $"|{QC.MissionKey.Sandoria.JourneyToBastok}|scripts/missions/sandoria/2_3_1_Journey_to_Bastok|1|True",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.States.Count}|" +
              $"{d.Reward == null}");
        Fake F(int status, int option = 0, bool sand = false, bool trade = true, bool give = true, bool complete = true,
               int current = -1)
        {
            var f = new Fake
            {
                ThisMission = (MissionLog.Sandoria, id), CompleteResult = complete, TradeResult = trade,
                GiveResult = give
            };
            f.CurrentMissions[sandoria] = current == -1 ? id : current;
            f.MissionStatuses[sandoria] = status;
            if (option != 0) f.MissionVars[$"{sandoria}/{id}/Option"] = option;
            if (sand) f.Items.Add(QC.Item.OnzOfMythrilSand);
            return f;
        }
        Check("c3 n2 journey to bastok: the section holds while 2-3 Journey to Bastok is current only", "True False",
              $"{d.States[0].IsMissionMatch(F(0))} " +
              $"{d.States[0].IsMissionMatch(F(0, current: QC.Mission.Sandoria.JourneyAbroad))}");

        var trig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var mw = QC.Zone.Metalworks;
        var grohm = C2MHandler(d, 0, trig, mw, QC.Npc.Metalworks.Grohm);
        Check("c3 n2 journey to bastok: Grohm at status 4: 423 (progress); 5 without sand: 424; 5 with sand: 422; " +
              "0 and 6: 422; Option 1 first at any status: 425 (progress)",
              "ProgressEvent(423)|Event(424)|Event(422)|Event(422)|Event(422)|ProgressEvent(425)|ProgressEvent(425)",
              string.Join("|", new[]
              {
                  C3NReply(grohm, F(4)), C3NReply(grohm, F(5)), C3NReply(grohm, F(5, sand: true)),
                  C3NReply(grohm, F(0)), C3NReply(grohm, F(6)), C3NReply(grohm, F(4, option: 1)),
                  C3NReply(grohm, F(0, option: 1))
              }));
        var pius = C2MHandler(d, 0, trig, mw, QC.Npc.Metalworks.Pius);
        Check("c3 n2 journey to bastok: Pius at status 3: 355 (progress); at 0, 4: 356 (an event)",
              "ProgressEvent(355)|Event(356)|Event(356)",
              string.Join("|", new[] { 3, 0, 4 }.Select(s0 => C3NReply(pius, F(s0)))));
        var savae = C2MHandler(d, 0, QuestHandlerKind.OnTrade, mw, QC.Npc.Metalworks.SavaeEPaleade);
        Check("c3 n2 journey to bastok: Savae's trade at status 5 with the sand matched: 205 (progress); " +
              "at status 4 or 6, or unmatched (each alone): nothing",
              "ProgressEvent(205)|null|null|null",
              $"{C3NReply(savae, F(5))}|{C3NReply(savae, F(4))}|{C3NReply(savae, F(6))}|" +
              $"{C3NReply(savae, F(5, trade: false))}");
        var f205 = F(5);
        var no205 = F(5, complete: false);
        Check("c3 n2 journey to bastok: 205's finish: completed, the trade taken, status 6, Journey Abroad begun; " +
              "complete false: nothing more",
              $"Mission.Complete(),Trade.Complete(),Player.SetMissionStatus({sandoria},6)," +
              $"Player.AddMission({sandoria},{QC.Mission.Sandoria.JourneyAbroad}) 6|Mission.Complete() 5",
              $"{C2MRun(C2MHandler(d, 0, fin, mw, id: 205), f205)} {f205.MissionStatus(sandoria)}|" +
              $"{C2MRun(C2MHandler(d, 0, fin, mw, id: 205), no205)} {no205.MissionStatus(sandoria)}");
        var f355 = F(3);
        Check("c3 n2 journey to bastok: 355's finish: status 4", $"Player.SetMissionStatus({sandoria},4) 4",
              $"{C2MRun(C2MHandler(d, 0, fin, mw, id: 355), f355)} {f355.MissionStatus(sandoria)}");
        string pick = $"Player.GiveItems({QC.Item.Pickaxe}x3)";
        var f423 = F(4);
        var f423f = F(4, give: false);
        Check("c3 n2 journey to bastok: 423's finish: three Pickaxes then status 5; a full bag: Option 1, status kept",
              $"{pick},Player.SetMissionStatus({sandoria},5) 5 0|{pick},Mission.SetVar(Option,1) " +
              "4 1",
              $"{C2MRun(C2MHandler(d, 0, fin, mw, id: 423), f423)} {f423.MissionStatus(sandoria)} " +
              $"{f423.MissionVar(sandoria, id, "Option")}|{C2MRun(C2MHandler(d, 0, fin, mw, id: 423), f423f)} " +
              $"{f423f.MissionStatus(sandoria)} {f423f.MissionVar(sandoria, id, "Option")}");
        var f425 = F(4, option: 1);
        var f425f = F(4, option: 1, give: false);
        Check("c3 n2 journey to bastok: 425's finish: three Pickaxes, status 5, Option 0; a full bag: nothing more",
              $"{pick},Player.SetMissionStatus({sandoria},5),Mission.SetVar(Option,0) 5 0|" +
              $"{pick} 4 1",
              $"{C2MRun(C2MHandler(d, 0, fin, mw, id: 425), f425)} {f425.MissionStatus(sandoria)} " +
              $"{f425.MissionVar(sandoria, id, "Option")}|{C2MRun(C2MHandler(d, 0, fin, mw, id: 425), f425f)} " +
              $"{f425f.MissionStatus(sandoria)} {f425f.MissionVar(sandoria, id, "Option")}");
    }

    static void RunC3N2JourneyToWindurst()
    {
        var m = new QC.Missions.Sandoria.JourneyToWindurst();
        var d = m.Compile();
        int sandoria = QC.MissionLog.Sandoria, id = QC.Mission.Sandoria.JourneyToWindurst;
        Check("c3 n2 journey to windurst: clean, its key and LegacyPath, one section, no reward table",
              $"|{QC.MissionKey.Sandoria.JourneyToWindurst}|scripts/missions/sandoria/2_3_2_Journey_to_Windurst|1|True",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.States.Count}|" +
              $"{d.Reward == null}");
        Fake F(int status, int current = -1, bool setting = true, bool spell = false, bool cipher = false,
               bool shield = false, bool trade = false, int rov = 0, bool complete = true)
        {
            var f = new Fake
            {
                ThisMission = (MissionLog.Sandoria, id), TradeResult = trade, CompleteResult = complete
            };
            f.CurrentMissions[sandoria] = current == -1 ? id : current;
            f.CurrentMissions[QC.MissionLog.Rov] = rov;
            f.MissionStatuses[sandoria] = status;
            if (setting) f.MainSettings["ENABLE_TRUST_QUESTS"] = 1;
            if (spell) f.Spells.Add(QC.Spell.SemihLafihna);
            if (cipher) f.Items.Add(QC.Item.CipherOfSemihsAlterEgo);
            if (shield) f.KeyItems[QC.KeyItem.ShieldOffering] = true;
            return f;
        }
        Check("c3 n2 journey to windurst: the section holds while 2-3 Journey to Windurst is current only",
              "True False",
              $"{d.States[0].IsMissionMatch(F(0))} " +
              $"{d.States[0].IsMissionMatch(F(0, current: QC.Mission.Sandoria.JourneyToBastok))}");

        var trig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var uu = C2MHandler(d, 0, trig, QC.Zone.Giddeus, QC.Npc.Giddeus.UuZhoumo);
        Check("c3 n2 journey to windurst: Uu Zhoumo: the Shield Offering: 42 (progress), at any status; without: " +
              "nothing",
              "ProgressEvent(42)|ProgressEvent(42)|null",
              $"{C3NReply(uu, F(3, shield: true))}|{C3NReply(uu, F(0, shield: true))}|{C3NReply(uu, F(3))}");
        var f42g = F(5, shield: true);
        Check("c3 n2 journey to windurst: Giddeus 42's finish: status 6, the offered-up message, takes the key item",
              $"Player.SetMissionStatus({sandoria},6),Text.Special({QC.Zone.Giddeus}:OFFERED_UP_KEY_ITEM," +
              $"{QC.KeyItem.ShieldOffering}),Player.DelKeyItem({QC.KeyItem.ShieldOffering}) 6",
              $"{C2MRun(C2MHandler(d, 0, fin, QC.Zone.Giddeus, id: 42), f42g)} {f42g.MissionStatus(sandoria)}");

        var ht = QC.Zone.HeavensTower;
        var kupipi = C2MHandler(d, 0, trig, ht, QC.Npc.HeavensTower.Kupipi);
        Check("c3 n2 journey to windurst: Kupipi at status 4: 238, parameter 8 = 1 with neither spell nor cipher " +
              "(the setting on); the spell alone, the cipher alone: 0; the setting off: the bare event",
              "ProgressEvent(238,1,1,1,1,0,0,0,1)|ProgressEvent(238,1,1,1,1,0,0,0,0)|" +
              "ProgressEvent(238,1,1,1,1,0,0,0,0)|ProgressEvent(238)",
              string.Join("|", new[] { F(4), F(4, spell: true), F(4, cipher: true), F(4, setting: false) }
                  .Select(f => C3NReply(kupipi, f))));
        Check("c3 n2 journey to windurst: Kupipi at 5: 240; at 6: 241 (events); at 0, 3 and 7: nothing",
              "Event(240)|Event(241)|null|null|null",
              string.Join("|", new[] { 5, 6, 0, 3, 7 }.Select(s0 => C3NReply(kupipi, F(s0)))));
        var zin = C2MHandler(d, 0, QuestHandlerKind.OnZoneIn, ht);
        Check("c3 n2 journey to windurst: Heaven's Tower zone-in: status 3: 42 (an event); any other: nothing",
              "Event(42)|null|null|null",
              $"{C3NReply(zin, F(3))}|{C3NReply(zin, F(2))}|{C3NReply(zin, F(4))}|{C3NReply(zin, F(0))}");
        var upd = C2MHandler(d, 0, QuestHandlerKind.OnEventUpdate, ht, id: 42);
        Check("c3 n2 journey to windurst: 42's update: parameter 8 = 1 only while The Path Untraveled is the RoV " +
              "mission",
              "Player.UpdateEvent(0,0,0,0,0,0,0,1)|Player.UpdateEvent(0,0,0,0,0,0,0,0)|" +
              "Player.UpdateEvent(0,0,0,0,0,0,0,0)",
              $"{C2MRun(upd, F(3, rov: QC.Mission.Rov.ThePathUntraveled))}|" +
              $"{C2MRun(upd, F(3, rov: QC.Mission.Rov.ThePathUntraveled + 1))}|{C2MRun(upd, F(3))}");
        var f42 = F(3);
        Check("c3 n2 journey to windurst: Heaven's Tower 42's finish: status 4",
              $"Player.SetMissionStatus({sandoria},4) 4",
              $"{C2MRun(C2MHandler(d, 0, fin, ht, id: 42), f42)} {f42.MissionStatus(sandoria)}");
        string so1 = $"Player.SetMissionStatus({sandoria},5),Player.GiveKeyItem({QC.KeyItem.ShieldOffering})";
        string ciph = $",Player.GiveItem({QC.Item.CipherOfSemihsAlterEgo})";
        var h238 = C2MHandler(d, 0, fin, ht, id: 238);
        var fa = F(4); var fb = F(4, setting: false); var fc = F(4, spell: true); var fd = F(4, cipher: true);
        Check("c3 n2 journey to windurst: 238's finish: status 5, the Shield Offering, and the cipher only with the " +
              "setting on, no spell and no cipher (each of those three alone: no cipher)",
              $"{so1}{ciph} 5|{so1}|{so1}|{so1}",
              $"{C2MRun(h238, fa)} {fa.MissionStatus(sandoria)}|{C2MRun(h238, fb)}|{C2MRun(h238, fc)}|" +
              $"{C2MRun(h238, fd)}");

        var ww = QC.Zone.WindurstWoods;
        var cat = C2MHandler(d, 0, trig, ww, QC.Npc.WindurstWoods.Catalia);
        var erp = C2MHandler(d, 0, trig, ww, QC.Npc.WindurstWoods.Erpolant);
        var for_ = C2MHandler(d, 0, trig, ww, QC.Npc.WindurstWoods.Forine);
        Check("c3 n2 journey to windurst: Catalia 452, Erpolant 454, Forine 453 (progress) from status 3 on; below " +
              "3: nothing",
              "ProgressEvent(452)|ProgressEvent(454)|ProgressEvent(453)|ProgressEvent(452)|ProgressEvent(454)|" +
              "ProgressEvent(453)|null|null|null",
              $"{C3NReply(cat, F(3))}|{C3NReply(erp, F(3))}|{C3NReply(for_, F(3))}|{C3NReply(cat, F(7))}|" +
              $"{C3NReply(erp, F(7))}|{C3NReply(for_, F(7))}|{C3NReply(cat, F(2))}|{C3NReply(erp, F(2))}|" +
              $"{C3NReply(for_, F(2))}");
        var mou = C2MHandler(d, 0, trig, ww, QC.Npc.WindurstWoods.Mourices);
        Check("c3 n2 journey to windurst: Mourices: 3 to 5: 451; 6: 456 (progress); 2 and 7: nothing",
              "ProgressEvent(451)|ProgressEvent(451)|ProgressEvent(451)|ProgressEvent(456)|null|null",
              string.Join("|", new[] { 3, 4, 5, 6, 2, 7 }.Select(s0 => C3NReply(mou, F(s0)))));
        var mouT = C2MHandler(d, 0, QuestHandlerKind.OnTrade, ww, QC.Npc.WindurstWoods.Mourices);
        Check("c3 n2 journey to windurst: Mourices' trade: two Parana Shields at status 6: 457 (progress); no match " +
              "or another status: nothing",
              "ProgressEvent(457)|null|null|null",
              $"{C3NReply(mouT, F(6, trade: true))}|{C3NReply(mouT, F(6))}|{C3NReply(mouT, F(5, trade: true))}|" +
              $"{C3NReply(mouT, F(7, trade: true))}");
        var h457 = C2MHandler(d, 0, fin, ww, id: 457);
        var f457 = F(6);
        var bad = F(6, complete: false);
        Check("c3 n2 journey to windurst: 457's finish: completed, takes the trade, begins Journey Abroad, status 7; " +
              "complete false: nothing more",
              "Mission.Complete(),Trade.Complete()," +
              $"Player.AddMission({sandoria},{QC.Mission.Sandoria.JourneyAbroad})," +
              $"Player.SetMissionStatus({sandoria},7) 7|Mission.Complete() 6",
              $"{C2MRun(h457, f457)} {f457.MissionStatus(sandoria)}|{C2MRun(h457, bad)} {bad.MissionStatus(sandoria)}");
    }
}
