// XI.Verify.Quests -- quest rework C3 C1: the CoP 1-2 to 2-5 missions, the three Promyvion spires' battlefields
// (Ancient Flames Beckon) and Monarch Linn's Ancient Vows. The data of each against its Lua file's (as it stood at the
// recording), every arm of the spires' entryRequirement, checkSkipCutscene and onBattlefieldWin (the 32000 update
// keeps only number params, so a trace cannot see the booleans), Ancient Vows' entryRequirement, and the Promyvion
// helpers' counting.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    internal static void RunC3Cop()
    {
        Console.WriteLine("\n== c3 C1: CoP 1-2 to 2-5, the spires and Ancient Vows ==");
        RunC3CopMissions();
        RunC3CopSpires();
        RunC3CopAncientVows();
        RunC3CopHelperPositions();
        RunC3CopHandlerPins();
        RunC3CopC2a();
        RunC3CopC2b();
    }

    static void RunC3CopMissions()
    {
        MissionScript[] all =
        {
            new QC.Missions.Cop.BelowTheArks(), new QC.Missions.Cop.TheMothercrystals(),
            new QC.Missions.Cop.AnInvitationWest(), new QC.Missions.Cop.TheLostCity(),
            new QC.Missions.Cop.DistantBeliefs(), new QC.Missions.Cop.AnEternalMelody(),
            new QC.Missions.Cop.AncientVows(),
        };
        Check("c3 C1: the seven missions' keys and Lua files",
              "mission.cop.below_the_arks scripts/missions/cop/1_2_Below_the_Arks|" +
              "mission.cop.the_mothercrystals scripts/missions/cop/1_3_The_Mothercrystals|" +
              "mission.cop.an_invitation_west scripts/missions/cop/2_1_An_Invitation_West|" +
              "mission.cop.the_lost_city scripts/missions/cop/2_2_The_Lost_City|" +
              "mission.cop.distant_beliefs scripts/missions/cop/2_3_Distant_Beliefs|" +
              "mission.cop.an_eternal_melody scripts/missions/cop/2_4_An_Eternal_Melody|" +
              "mission.cop.ancient_vows scripts/missions/cop/2_5_Ancient_Vows",
              string.Join("|", all.Select(m => $"{m.Key} {m.LegacyPath}")));
        Check("c3 C1: the missions validate clean", "",
              string.Join(" | ", all.SelectMany(m => QuestScriptValidator.Validate(m.Compile()))));
    }

    // a spire's win, entry and skip arms over fakes
    static (BattlefieldContextFake Q, BattlefieldPlayerFake P) C3CopSpire(BattlefieldScript s, int current = 0,
        string completed = null, params int[] keyItems)
    {
        var (q, p) = C2BContext(s, "_0j0");
        p.CurrentMissions[QC.MissionLog.Cop] = current;
        if (completed != null) p.CompletedMissions.Add(completed);
        foreach (var k in keyItems) p.KeyItems[k] = true;
        return (q, p);
    }

    static void RunC3CopSpires()
    {
        var dem = new QC.Battlefields.SpireOfDem.AncientFlamesBeckonSpireOfDem();
        var holla = new QC.Battlefields.SpireOfHolla.AncientFlamesBeckonSpireOfHolla();
        var mea = new QC.Battlefields.SpireOfMea.AncientFlamesBeckonSpireOfMea();
        BattlefieldScript[] spires = { dem, holla, mea };
        Check("c3 C1: the spires validate clean, their keys and Lua files",
              "|battlefield.ancient_flames_beckon_spire_of_dem " +
              "scripts/battlefields/Spire_of_Dem/ancient_flames_beckon|" +
              "|battlefield.ancient_flames_beckon_spire_of_holla " +
              "scripts/battlefields/Spire_of_Holla/ancient_flames_beckon|" +
              "|battlefield.ancient_flames_beckon_spire_of_mea scripts/battlefields/Spire_of_Mea/ancient_flames_beckon",
              string.Join("|", spires.Select(s =>
                  $"{string.Join(" | ", BattlefieldScriptValidator.Validate(s))}|{s.Key} {s.LegacyPath}")));
        Check("c3 C1: the Lua files' fields (zone, id, no exp loss, mission, trusts, players, MAX_LEVEL, time, " +
              "index, " +
              "grantXP and its lockout, entry and exit NPCs, groups)",
              "19 800 False True True 6 MAX_LEVEL 1800 0 1500 True _0j0 _0j1,_0j2,_0j3 2|" +
              "17 768 False True True 6 MAX_LEVEL 1800 0 1500 True _0h0 _0h1,_0h2,_0h3 1|" +
              "21 832 False True True 6 MAX_LEVEL 1800 0 1500 True _0l0 _0l1,_0l2,_0l3 1",
              string.Join("|", spires.Select(s => $"{s.Data.ZoneId} {s.Data.BattlefieldId} {s.Data.CanLoseExp} " +
                  $"{s.Data.IsMission} {s.Data.AllowTrusts} {s.Data.MaxPlayers} {s.Data.LevelCapSetting} " +
                  $"{s.Data.TimeLimit} {s.Data.Index} {s.Data.GrantXP} {s.Data.GrantXPLockout} " +
                  $"{string.Join(",", s.Data.EntryNpcs)} {string.Join(",", s.Data.ExitNpcs)} {s.Data.Groups.Count}")));
        Check("c3 C1: Dem's groups: the Progenerators win (by id +0, +5, +10); the twelve after them wait " +
              "(not spawned)",
              "PROGENERATOR:0;PROGENERATOR:5;PROGENERATOR:10 True |" +
              "PROGENERATOR:1,2,3,4;PROGENERATOR:6,7,8,9;PROGENERATOR:11,12,13,14 False False",
              string.Join("|", dem.Data.Groups.Select(g =>
                  string.Join(";", g.AreaMobIds.Select(a => a[0].Key + ":" +
                      string.Join(",", a.Select(m => m.Offset)))) +
                  $" {g.AllDeath != null} {g.Spawned}")));
        Check("c3 C1: the spires override the three hooks; their coverage labels",
              "CheckSkipCutscene,EntryRequirement,OnBattlefieldWin|hook:CheckSkipCutscene,hook:EntryRequirement," +
              "hook:OnBattlefieldWin,group:1:allDeath,exit:_0j1,exit:_0j2,exit:_0j3",
              $"{string.Join(",", BattlefieldScript.HookNames.Where(dem.Overrides))}|" +
              string.Join(",", dem.CoverageLabels()));

        int cop = QC.MissionLog.Cop, arks = QC.Mission.Cop.BelowTheArks, mother = QC.Mission.Cop.TheMothercrystals;
        string motherDone = $"{cop}/{mother}";
        int holKi = QC.KeyItem.LightOfHolla, demKi = QC.KeyItem.LightOfDem, meaKi = QC.KeyItem.LightOfMea;
        string Entry(BattlefieldScript s, bool registrant, int current = 0, string done = null, params int[] ki)
        {
            var (q, _) = C3CopSpire(s, current, done, ki);
            return s.EntryRequirement(q, registrant).ToString();
        }
        // Dem: Below the Arks current; The Mothercrystals current without / with the light; the helper arms
        Check("c3 C1: Dem entryRequirement: a registrant on 1-2; on 1-3 without the light; on 1-3 with it; " +
              "with nothing",
              "True True False False",
              $"{Entry(dem, true, arks)} {Entry(dem, true, mother)} {Entry(dem, true, mother, null, demKi)} " +
              Entry(dem, true));
        Check("c3 C1: Dem entryRequirement: a helper with 1-3 completed; with the light alone; with another crag's " +
              "light; with nothing; a registrant with 1-3 completed only",
              "True True False False False",
              $"{Entry(dem, false, 0, motherDone)} {Entry(dem, false, 0, null, demKi)} " +
              $"{Entry(dem, false, 0, null, holKi)} {Entry(dem, false)} {Entry(dem, true, 0, motherDone)}");
        Check("c3 C1: Holla and Mea read their own light: 1-3 current holding it refuses a registrant, holding the " +
              "other's does not",
              "False True False True",
              $"{Entry(holla, true, mother, null, holKi)} {Entry(holla, true, mother, null, demKi)} " +
              $"{Entry(mea, true, mother, null, meaKi)} {Entry(mea, true, mother, null, holKi)}");
        string Skip(BattlefieldScript s, string done = null, params int[] ki)
        {
            var (q, _) = C3CopSpire(s, 0, done, ki);
            return s.CheckSkipCutscene(q).ToString();
        }
        Check("c3 C1: Dem checkSkipCutscene: 1-3 completed; the light; another crag's light; nothing",
              "True True False False",
              $"{Skip(dem, motherDone)} {Skip(dem, null, demKi)} {Skip(dem, null, holKi)} {Skip(dem)}");

        string Win(BattlefieldScript s, int zone, int current, string done = null, params int[] ki)
        {
            var (q, p) = C3CopSpire(s, current, done, ki);
            p.ZoneId = zone;
            q.Battlefield = new BattlefieldViewFake
            {
                Id = s.Data.BattlefieldId, Area = 1, TimeInside = 300, RecordValue = ("Meme", 250, 3),
            };
            s.OnBattlefieldWin(q);
            return string.Join(",", p.Calls);
        }
        Check("c3 C1: Dem's win on 1-2 holding no light: newPromy, battlefieldWin 800, 32001 (repeatClear 0, " +
              "arg8 1)",
              "Player.SetLocalVar(newPromy,1),Player.SetLocalVar(battlefieldWin,800)," +
              "Player.StartEvent(32001,1,250,3,300,19,0,0,1)",
              Win(dem, 19, arks));
        Check("c3 C1: Dem's win on 1-3 holding Holla's light: newPromy, arg8 2",
              "Player.SetLocalVar(newPromy,1),Player.SetLocalVar(battlefieldWin,800)," +
              "Player.StartEvent(32001,1,250,3,300,19,0,0,2)",
              Win(dem, 19, mother, null, holKi));
        Check("c3 C1: Dem's win on 1-3 holding Dem's light: no newPromy, repeatClear 1, arg8 1 (its own light " +
              "excluded)",
              "Player.SetLocalVar(battlefieldWin,800),Player.StartEvent(32001,1,250,3,300,19,0,1,1)",
              Win(dem, 19, mother, null, demKi));
        Check("c3 C1: Dem's win with 1-3 completed, no mission current, no light: no newPromy, repeatClear 1",
              "Player.SetLocalVar(battlefieldWin,800),Player.StartEvent(32001,1,250,3,300,19,0,1,1)",
              Win(dem, 19, 0, motherDone));
        Check("c3 C1: Holla's win reads the light by the zone ((17 - 17) / 2: Holla's) and Mea's ((21 - 17) / 2: " +
              "Mea's); each counts the other two lights",
              "Player.SetLocalVar(newPromy,1),Player.SetLocalVar(battlefieldWin,768)," +
              "Player.StartEvent(32001,1,250,3,300,17,0,0,3)|" +
              "Player.SetLocalVar(newPromy,1),Player.SetLocalVar(battlefieldWin,832)," +
              "Player.StartEvent(32001,1,250,3,300,21,0,0,3)",
              $"{Win(holla, 17, arks, null, demKi, meaKi)}|{Win(mea, 21, arks, null, demKi, holKi)}");
    }

    static void RunC3CopHelperPositions()
    {
        string Walk(Action<FakeQuestContext> act)
        {
            var q = new FakeQuestContext();
            act(q);
            return string.Join(",", q.Calls);
        }
        Check("c3 C1: sendToPromyvionZone: Holla, Dem and Mea's landing spots; any other crag nowhere",
              "Player.SetPos(92.033,0,80.38,255,16)|Player.SetPos(185.891,0,-52.331,128,18)|" +
              "Player.SetPos(-93.268,0,170.749,162,20)|",
              string.Join("|", new[] { 1, 2, 3, 4 }.Select(c =>
                  Walk(q => QC.Missions.Cop.PromyvionHelpers.SendToPromyvionZone(q, c)))));
        Check("c3 C1: shatteredTelepointEntry (Option 0): each telepoint's hall position; Option 1 stays put",
              "Player.SetPos(-266.76,-0.635,280.058,0,14)|Player.SetPos(-267.194,-40.634,-280.019,0,14)|" +
              "Player.SetPos(280.066,-80.635,-67.096,191,14)|",
              string.Join("|", new[]
              {
                  Walk(q => QC.Missions.Cop.PromyvionHelpers.TelepointEntry(q, QC.Zone.LaTheinePlateau)),
                  Walk(q => QC.Missions.Cop.PromyvionHelpers.TelepointEntry(q, QC.Zone.KonschtatHighlands)),
                  Walk(q => QC.Missions.Cop.PromyvionHelpers.TelepointEntry(q, QC.Zone.TahrongiCanyon)),
                  Walk(q =>
                  {
                      q.Option = 1;
                      QC.Missions.Cop.PromyvionHelpers.TelepointEntry(q, QC.Zone.TahrongiCanyon);
                  }),
              }));
    }

    // values no trace sees: the Mothercrystals' section 2 Hall positions and Ancient Vows' South Gustaberg landing
    static void RunC3CopHandlerPins()
    {
        var cop = QC.MissionLog.Cop;
        var fin = QuestHandlerKind.OnEventFinish;
        var moth = new QC.Missions.Cop.TheMothercrystals().Compile();
        string Hall(ushort zone, int id, int option)
        {
            var f = new Fake { ThisMission = (MissionLog.CoP, QC.Mission.Cop.TheMothercrystals), Option = option };
            return C2MRun(C2MHandler(moth, 1, fin, zone, id: id), f);
        }
        Check("c3 C1: Mothercrystals section 2: Konschtat's 913, Tahrongi's 913 and La Theine's 202 with option 0 land " +
              "in the Hall at their spots; option 1 stays",
              "Player.SetPos(-267.194,-40.634,-280.019,0,14)|Player.SetPos(280.066,-80.635,-67.096,191,14)|" +
              "Player.SetPos(-266.76,-0.635,280.058,0,14)||||",
              $"{Hall(QC.Zone.KonschtatHighlands, 913, 0)}|{Hall(QC.Zone.TahrongiCanyon, 913, 0)}|" +
              $"{Hall(QC.Zone.LaTheinePlateau, 202, 0)}|{Hall(QC.Zone.KonschtatHighlands, 913, 1)}|" +
              $"{Hall(QC.Zone.TahrongiCanyon, 913, 1)}|{Hall(QC.Zone.LaTheinePlateau, 202, 1)}|");
        var arks = new QC.Missions.Cop.BelowTheArks().Compile();
        string ArksEntry(ushort zone, int id, int option)
        {
            var f = new Fake { ThisMission = (MissionLog.CoP, QC.Mission.Cop.BelowTheArks), Option = option };
            return C2MRun(C2MHandler(arks, 0, fin, zone, id: id), f);
        }
        Check("c3 C1: Below the Arks: Konschtat's 913, Tahrongi's 913 and La Theine's 202 with option 0 land in the " +
              "Hall at their own spots; option 1 stays",
              "Player.SetPos(-267.194,-40.634,-280.019,0,14)|Player.SetPos(280.066,-80.635,-67.096,191,14)|" +
              "Player.SetPos(-266.76,-0.635,280.058,0,14)|",
              $"{ArksEntry(QC.Zone.KonschtatHighlands, 913, 0)}|{ArksEntry(QC.Zone.TahrongiCanyon, 913, 0)}|" +
              $"{ArksEntry(QC.Zone.LaTheinePlateau, 202, 0)}|{ArksEntry(QC.Zone.KonschtatHighlands, 913, 1)}");
        string Send(ushort zone, int id, int toPromyvion, int option)
        {
            var f = new Fake { ThisMission = (MissionLog.CoP, QC.Mission.Cop.TheMothercrystals), Option = option };
            f.PlayerLocalVars["toPromyvion"] = toPromyvion;
            return C2MRun(C2MHandler(moth, 0, fin, zone, id: id), f);
        }
        Check("c3 C1: Mothercrystals' 912 (Konschtat, Tahrongi) and 201 (La Theine) finishes with option 0 send the " +
              "char to the promyvion in toPromyvion; option 1 nowhere",
              "Player.SetPos(185.891,0,-52.331,128,18)|Player.SetPos(-93.268,0,170.749,162,20)|" +
              "Player.SetPos(92.033,0,80.38,255,16)|",
              $"{Send(QC.Zone.KonschtatHighlands, 912, 2, 0)}|{Send(QC.Zone.TahrongiCanyon, 912, 3, 0)}|" +
              $"{Send(QC.Zone.LaTheinePlateau, 201, 1, 0)}|{Send(QC.Zone.KonschtatHighlands, 912, 2, 1)}");
        var vows = new QC.Missions.Cop.AncientVows().Compile();
        string Win(int status, int win)
        {
            var f = new Fake { ThisMission = (MissionLog.CoP, QC.Mission.Cop.AncientVows) };
            f.CurrentMissions[cop] = QC.Mission.Cop.AncientVows;
            f.MissionVars[$"{cop}/{QC.Mission.Cop.AncientVows}/Status"] = status;
            f.PlayerLocalVars["battlefieldWin"] = win;
            return C2MRun(C2MHandler(vows, 0, fin, QC.Zone.MonarchLinn, id: 32001), f);
        }
        string winCalls = Win(2, QC.Battlefield.AncientVows);
        Check("c3 C1: Ancient Vows' win at Status 2: complete, then South Gustaberg (694, -5.5, -619, 74)",
              "Mission.Complete(),Player.SetPos(694,-5.5,-619,74,107)", winCalls);
        Check("c3 C1: Ancient Vows' win: Status 1, Status 3 and another battlefield's win do nothing",
              "||", $"{Win(1, QC.Battlefield.AncientVows)}|{Win(3, QC.Battlefield.AncientVows)}|" +
              $"{Win(2, QC.Battlefield.AncientFlamesBeckonSpireOfDem)}");
    }

    static void RunC3CopAncientVows()
    {
        var s = new QC.Battlefields.MonarchLinn.AncientVows();
        var d = (BattlefieldMissionData)s.Data;
        Check("c3 C1: Ancient Vows validates clean, its key and Lua file",
              "|battlefield.ancient_vows scripts/battlefields/Monarch_Linn/ancient_vows",
              $"{string.Join(" | ", BattlefieldScriptValidator.Validate(s))}|{s.Key} {s.LegacyPath}");
        Check("c3 C1: Ancient Vows' fields (zone, id, no exp loss, mission, trusts, players, MAX_LEVEL, time, index, " +
              "grantXP, title, entry and exit NPCs, mission, required var and value, groups)",
              "31 960 False True True 6 MAX_LEVEL 1800 0 1000 TavnazianTraveler SD_Entrance " +
              "SD_BCNM_Exit_1,SD_BCNM_Exit_2,SD_BCNM_Exit_3 6/248 Mission[6][248]Status 2 1",
              $"{d.ZoneId} {d.BattlefieldId} {d.CanLoseExp} {d.IsMission} {d.AllowTrusts} {d.MaxPlayers} " +
              $"{d.LevelCapSetting} {d.TimeLimit} {d.Index} {d.GrantXP} " +
              $"{(d.Title == QC.Title.TavnazianTraveler ? "TavnazianTraveler" : d.Title.ToString())} " +
              $"{string.Join(",", d.EntryNpcs)} {string.Join(",", d.ExitNpcs)} {(int)d.MissionArea}/{d.Mission} " +
              $"{d.RequiredVar} {d.RequiredValue} {d.Groups.Count}");
        Check("c3 C1: Ancient Vows' group: nine Mammet-19 Epsilons, three to an area, allDeath wins",
              "MAMMET_19_EPSILON:0,1,2;MAMMET_19_EPSILON:3,4,5;MAMMET_19_EPSILON:6,7,8 True",
              string.Join(";", d.Groups[0].AreaMobIds.Select(a => a[0].Key + ":" +
                  string.Join(",", a.Select(m => m.Offset)))) + $" {d.Groups[0].AllDeath != null}");
        Check("c3 C1: Ancient Vows overrides entryRequirement alone",
              "EntryRequirement", string.Join(",", BattlefieldScript.HookNames.Where(s.Overrides)));
        string Entry(int prev, bool registrant)
        {
            var (q, p) = C2BContext(s, "SD_Entrance");
            p.PrevZone = prev;
            return s.EntryRequirement(q, registrant).ToString();
        }
        Check("c3 C1: Ancient Vows entryRequirement: refused from Riverne - Site #B01 (either role), allowed from " +
              "Site #A01 and from nowhere",
              "False False True True True",
              $"{Entry(QC.Zone.RiverneSiteB01, true)} {Entry(QC.Zone.RiverneSiteB01, false)} " +
              $"{Entry(QC.Zone.RiverneSiteA01, true)} {Entry(QC.Zone.RiverneSiteA01, false)} {Entry(0, true)}");
    }
}
