// XI.Verify.Quests -- quest rework C2 Task 7: the three missions without a battlefield on C# (Bastok 1-1 The Zeruhn
// Report, CoP 1-1 The Rites of Life, RoV 1-1 Rhapsodies of Vana'diel), one fake check per handler with logic: each
// section check on its operands, each reply on the reads it branches on, each finish on its option and the vars it
// reads, as the Lua files did. The traces (mission.bastok.the_zeruhn_report, mission.cop.the_rites_of_life,
// mission.rov.rhapsodies_of_vanadiel) hold the packets; these hold the arms one at a time. Also the three facet members
// CoP 1-1 added: Player.StartEventHidden, Player.EnableEntities and Player.SendPartialMissionLog.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    // the handler of a mission state: an NPC's (entity), or a zone-wide one by its id (the csid; 1 for a zone-in)
    static QuestHandlerDecl C2MHandler(MissionScriptDefinition d, int state, QuestHandlerKind kind, ushort zone,
                                       string entity = null, int id = 1) =>
        d.States[state].Handlers.Single(h =>
            h.Kind == kind && h.Zone == zone && h.Entity == entity && (entity != null || h.Id == id));

    // the reply a handler gives on the fake ("null" for none)
    static string C2MReply(QuestHandlerDecl h, Fake f)
    {
        var r = h.Fixed ?? h.Reply(f);
        return r == null ? "null" : QuestContextFake.Describe(r);
    }

    // a handler run on the fake: its calls
    static string C2MRun(QuestHandlerDecl h, Fake f)
    {
        h.Run(f);
        return string.Join(",", f.Calls);
    }

    internal static void RunC2MissionPorts()
    {
        Console.WriteLine("\n== c2 missions: Bastok 1-1, CoP 1-1 and RoV 1-1 on C# ==");
        RunC2ZeruhnReport();
        RunC2RitesOfLife();
        RunC2Rhapsodies();
        RunC2PortFacets();
        RunC2SaveTheChildrenMission();
        RunC2SaveTheChildrenFacets();
    }

    static void RunC2ZeruhnReport()
    {
        var m = new QC.Missions.Bastok.TheZeruhnReport();
        var d = m.Compile();
        int bastok = QC.MissionLog.Bastok, id = QC.Mission.Bastok.TheZeruhnReport;
        const int report = QC.KeyItem.ZeruhnReport;
        Check("c2 zeruhn: clean, its key and LegacyPath, no reward table",
              $"|{QC.MissionKey.Bastok.TheZeruhnReport}|scripts/missions/bastok/1_1_The_Zeruhn_Report|True",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.Reward == null}");
        Fake F(int current, int nation = 1, bool completed = false, bool key = false, bool seen = false, int option = 0)
        {
            var f = new Fake { ThisMission = (MissionLog.Bastok, id), NationId = nation, Option = option };
            f.CurrentMissions[bastok] = current;
            if (completed) f.CompletedMissions.Add($"{bastok}/{id}");
            if (key) f.KeyItems[report] = true;
            if (seen) f.SeenKeyItems.Add(report);
            return f;
        }
        const int none = QC.Mission.Bastok.None;
        Check("c2 zeruhn: section 1 holds with no current mission, a Bastokan, 1-1 not completed; fails for a San " +
              "d'Orian, a current mission (1-2) and 1-1 completed",
              "True False False False",
              $"{d.States[0].IsMissionMatch(F(none))} {d.States[0].IsMissionMatch(F(none, nation: 0))} " +
              $"{d.States[0].IsMissionMatch(F(QC.Mission.Bastok.GeologicalSurvey))} " +
              $"{d.States[0].IsMissionMatch(F(none, completed: true))}");
        Check("c2 zeruhn: section 2 holds while 1-1 is current only", "True False",
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
            var h = C2MHandler(d, 0, fin, zone, id: 1000);
            Check($"c2 zeruhn: {label} 1000's finish: option 0 begins with its zone's YOU_ACCEPT_THE_MISSION; " +
                  "option 1 does nothing",
                  $"Mission.Begin(),Text.Special({text})|", $"{C2MRun(h, F(none))}|{C2MRun(h, F(none, option: 1))}");
        }
        Check("c2 zeruhn: section 1's four NPCs play 1000 (a Progress)",
              "ProgressEvent(1000) ProgressEvent(1000) ProgressEvent(1000) ProgressEvent(1000)",
              string.Join(" ", new[]
              {
                  (QC.Zone.BastokMarkets, QC.Npc.BastokMarkets.Cleades),
                  (QC.Zone.BastokMines, QC.Npc.BastokMines.Rashid),
                  (QC.Zone.Metalworks, QC.Npc.Metalworks.Malduc), (QC.Zone.PortBastok, QC.Npc.PortBastok.Argus),
              }.Select(n => C2MReply(C2MHandler(d, 0, QuestHandlerKind.OnTrigger, n.Item1, n.Item2), F(none)))));

        var trig = QuestHandlerKind.OnTrigger;
        var naji = C2MHandler(d, 1, trig, QC.Zone.Metalworks, QC.Npc.Metalworks.Naji);
        Check("c2 zeruhn: Naji: no report: nothing; the report not seen: 710 (1); seen: 710 (0)",
              "null|ProgressEvent(710,1)|ProgressEvent(710,0)",
              $"{C2MReply(naji, F(id))}|{C2MReply(naji, F(id, key: true))}|" +
              $"{C2MReply(naji, F(id, key: true, seen: true))}");
        var f710 = F(id, key: true);
        string done710 = C2MRun(C2MHandler(d, 1, fin, QC.Zone.Metalworks, id: 710), f710);
        var failed = F(id, key: true);
        failed.CompleteResult = false;
        Check("c2 zeruhn: 710's finish: completed (status 0), then the report taken; complete false: the report kept",
              $"Mission.Complete(),Player.DelKeyItem({report}) True False|Mission.Complete() True",
              $"{done710} {f710.HasCompletedMission(bastok, id)} {f710.HasKeyItem(report)}|" +
              $"{C2MRun(C2MHandler(d, 1, fin, QC.Zone.Metalworks, id: 710), failed)} {failed.HasKeyItem(report)}");
        var makarim = C2MHandler(d, 1, trig, QC.Zone.ZeruhnMines, QC.Npc.ZeruhnMines.Makarim);
        var rasmus = C2MHandler(d, 1, trig, QC.Zone.ZeruhnMines, QC.Npc.ZeruhnMines.Rasmus);
        Check("c2 zeruhn: Makarim: no report: 121 (canSkip); the report: MAKARIM_DIALOG_I",
              $"ProgressEvent(121,{{canSkip=true}})|MessageSpecial({QC.Text.ZeruhnMines.MAKARIM_DIALOG_I})",
              $"{C2MReply(makarim, F(id))}|{C2MReply(makarim, F(id, key: true))}");
        Check("c2 zeruhn: Rasmus: no report: 120 (canSkip); the report: nothing",
              "ProgressEvent(120,{canSkip=true})|null",
              $"{C2MReply(rasmus, F(id))}|{C2MReply(rasmus, F(id, key: true))}");
        Check("c2 zeruhn: 121's finish gives the report (npcUtil.giveKeyItem)", $"Player.GiveKeyItem({report})",
              C2MRun(C2MHandler(d, 1, fin, QC.Zone.ZeruhnMines, id: 121), F(id)));
        Check("c2 zeruhn: section 2's four NPCs play their zone's ORIGINAL_MISSION_OFFSET",
              $"MessageSpecial({QC.Text.BastokMarkets.ORIGINAL_MISSION_OFFSET}) " +
              $"MessageSpecial({QC.Text.BastokMines.ORIGINAL_MISSION_OFFSET}) " +
              $"MessageSpecial({QC.Text.Metalworks.ORIGINAL_MISSION_OFFSET}) " +
              $"MessageSpecial({QC.Text.PortBastok.ORIGINAL_MISSION_OFFSET})",
              string.Join(" ", new[]
              {
                  (QC.Zone.BastokMarkets, QC.Npc.BastokMarkets.Cleades),
                  (QC.Zone.BastokMines, QC.Npc.BastokMines.Rashid),
                  (QC.Zone.Metalworks, QC.Npc.Metalworks.Malduc), (QC.Zone.PortBastok, QC.Npc.PortBastok.Argus),
              }.Select(n => C2MReply(C2MHandler(d, 1, trig, n.Item1, n.Item2), F(id)))));
    }

    static void RunC2RitesOfLife()
    {
        var m = new QC.Missions.Cop.TheRitesOfLife();
        var d = m.Compile();
        int cop = QC.MissionLog.Cop, id = QC.Mission.Cop.TheRitesOfLife;
        const string tbVar = "[COP]TalesBeginning";
        const int tales = 17531000;
        Check("c2 rites: clean, its key and LegacyPath; the reward: the Mysterious Amulet, Below the Arks next",
              $"|{QC.MissionKey.Cop.TheRitesOfLife}|scripts/missions/cop/1_1_The_Rites_of_Life|" +
              $"{QC.KeyItem.MysteriousAmulet} CoP {QC.Mission.Cop.BelowTheArks}",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|" +
              $"{d.Reward.KeyItem ?? 0} {d.Reward.NextLog} {d.Reward.NextMission}");
        Fake F(int current, int cop1 = 1, int status = 0, bool completed = false, int tb = 0, int option = 0,
               int prev = 0)
        {
            var f = new Fake { ThisMission = (MissionLog.CoP, id), Option = option, PrevZone = prev };
            f.CurrentMissions[cop] = current;
            f.MainSettings["ENABLE_COP"] = cop1;
            if (status != 0) f.MissionVars[$"{cop}/{id}/Status"] = status;
            if (completed) f.CompletedMissions.Add($"{cop}/{id}");
            if (tb != 0) f.CharVars[tbVar] = tb;
            f.NpcIds["TALES_BEGINNING"] = tales;
            return f;
        }
        const int flames = QC.Mission.Cop.AncientFlamesBeckon;
        Check("c2 rites: section 1 holds with ENABLE_COP 1 below 1-1 (101); fails with ENABLE_COP 0, and at 1-1 (110)",
              "True False False",
              $"{d.States[0].IsMissionMatch(F(flames))} {d.States[0].IsMissionMatch(F(flames, cop1: 0))} " +
              $"{d.States[0].IsMissionMatch(F(id))}");
        var ldt = QC.Zone.LowerDelkfuttsTower;
        var trig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        Check("c2 rites: Tales_Beginning: 53 (3)", "ProgressEvent(53,3)",
              C2MReply(C2MHandler(d, 0, trig, ldt, QC.Npc.LowerDelkfuttsTower.TalesBeginning), F(flames)));
        var zoneIn = C2MHandler(d, 0, QuestHandlerKind.OnZoneIn, ldt);
        Check("c2 rites: Lower Delkfutt's zone-in: from Qufim with the var 0: 22; the var 1: nothing; from " +
              "elsewhere: nothing",
              "Event(22)|null|null",
              $"{C2MReply(zoneIn, F(flames, prev: QC.Zone.QufimIsland))}|" +
              $"{C2MReply(zoneIn, F(flames, prev: QC.Zone.QufimIsland, tb: 1))}|" +
              $"{C2MReply(zoneIn, F(flames, prev: QC.Zone.RuludeGardens))}");
        var after = C2MHandler(d, 0, QuestHandlerKind.OnAfterZoneIn, ldt);
        var a1 = F(flames, tb: 1);
        var a0 = F(flames);
        Check("c2 rites: afterZoneIn: the var 1: TALES_BEGINNING and the id before it enabled; the var 0: nothing",
              $"null Player.EnableEntities({tales};{tales - 1})|null ",
              $"{C2MReply(after, a1)} {string.Join(",", a1.Calls)}|{C2MReply(after, a0)} {string.Join(",", a0.Calls)}");
        var h22 = C2MHandler(d, 0, fin, ldt, id: 22);
        Check("c2 rites: 22's finish: option 1: the hidden 36; another option: the var 1, the NPCs enabled, the CoP " +
              "log's page",
              $"Player.StartEventHidden(36)|Player.SetCharVar({tbVar},1,0)," +
              $"Player.EnableEntities({tales};{tales - 1})," +
              $"Player.SendPartialMissionLog({cop},False)",
              $"{C2MRun(h22, F(flames, option: 1))}|{C2MRun(h22, F(flames, option: 0))}");
        Check("c2 rites: 36, 37 and 38's finishes start the hidden 37, 38 and 39",
              "Player.StartEventHidden(37) Player.StartEventHidden(38) Player.StartEventHidden(39)",
              string.Join(" ", new[] { 36, 37, 38 }.Select(c => C2MRun(C2MHandler(d, 0, fin, ldt, id: c), F(flames)))));
        var h39 = C2MHandler(d, 0, fin, ldt, id: 39);
        var b39 = F(flames, tb: 1);
        Check("c2 rites: 39's finish: the var 1: every NPC hidden (the empty table), the var 0, begun, Status 1; the " +
              "var 0: begun, Status 1",
              $"Player.EnableEntities(),Player.SetCharVar({tbVar},0,0),Mission.Begin()," +
              "Mission.SetVar(Status,1) True 1|" +
              "Mission.Begin(),Mission.SetVar(Status,1)",
              $"{C2MRun(h39, b39)} {b39.Mission.IsCurrent} {b39.Mission.Var("Status")}|{C2MRun(h39, F(flames))}");
        var h53 = C2MHandler(d, 0, fin, ldt, id: 53);
        Check("c2 rites: 53's finish: option 4: 22 (0, 0, 0, 1); option 1: nothing",
              "Player.StartEvent(22,0,0,0,1)|",
              $"{C2MRun(h53, F(flames, option: 4))}|{C2MRun(h53, F(flames, option: 1))}");

        Check("c2 rites: section 2 holds at 1-1 with Status 1 only; section 3 at Status 2 only",
              "True False False|True False",
              $"{d.States[1].IsMissionMatch(F(id, status: 1))} {d.States[1].IsMissionMatch(F(id, status: 2))} " +
              $"{d.States[1].IsMissionMatch(F(flames, status: 1))}|" +
              $"{d.States[2].IsMissionMatch(F(id, status: 2))} {d.States[2].IsMissionMatch(F(id, status: 1))}");
        var uj = QC.Zone.UpperJeuno;
        Check("c2 rites: Upper Jeuno's zone-in: 2; its finish: Status 2",
              "Event(2)|Mission.SetVar(Status,2)",
              $"{C2MReply(C2MHandler(d, 1, QuestHandlerKind.OnZoneIn, uj), F(id, status: 1))}|" +
              $"{C2MRun(C2MHandler(d, 1, fin, uj, id: 2), F(id, status: 1))}");
        var done = F(id, status: 2);
        Check("c2 rites: Monberaux: 10 (a Progress); 10 and 206 start 206 and 207; 207 completes (CoP keeps the " +
              "status, the vars cleared)",
              "ProgressEvent(10)|Player.StartEvent(206)|Player.StartEvent(207)|Mission.Complete() True 0",
              $"{C2MReply(C2MHandler(d, 2, trig, uj, QC.Npc.UpperJeuno.Monberaux), F(id, status: 2))}|" +
              $"{C2MRun(C2MHandler(d, 2, fin, uj, id: 10), F(id, status: 2))}|" +
              $"{C2MRun(C2MHandler(d, 2, fin, uj, id: 206), F(id, status: 2))}|" +
              $"{C2MRun(C2MHandler(d, 2, fin, uj, id: 207), done)} {done.HasCompletedMission(cop, id)} " +
              $"{done.Mission.Var("Status")}");
        Check("c2 rites: section 4 holds with 1-1 completed, or Status above 0; not before",
              "True True False",
              $"{d.States[3].IsMissionMatch(F(QC.Mission.Cop.BelowTheArks, completed: true))} " +
              $"{d.States[3].IsMissionMatch(F(id, status: 1))} {d.States[3].IsMissionMatch(F(flames))}");
        var ru = QC.Zone.RuludeGardens;
        Check("c2 rites: section 4: Auchefort 8, Baran 17, Colti 21, Rosaline 97, each replacing the default",
              "Event(8):replaceDefault() Event(17):replaceDefault() Event(21):replaceDefault() " +
              "Event(97):replaceDefault()",
              string.Join(" ", new[]
              {
                  (ru, QC.Npc.RuludeGardens.Auchefort), (ru, QC.Npc.RuludeGardens.Baran),
                  (ru, QC.Npc.RuludeGardens.Colti), (uj, QC.Npc.UpperJeuno.Rosaline),
              }.Select(n => C2MReply(C2MHandler(d, 3, trig, n.Item1, n.Item2), F(id, status: 1)))));
    }

    static void RunC2Rhapsodies()
    {
        var m = new QC.Missions.Rov.RhapsodiesOfVanadiel();
        var d = m.Compile();
        int rov = QC.MissionLog.Rov, id = QC.Mission.Rov.RhapsodiesOfVanadiel;
        Check("c2 rhapsodies: clean, its key and LegacyPath; the reward: Resonance next, nothing else",
              $"|{QC.MissionKey.Rov.RhapsodiesOfVanadiel}|scripts/missions/rov/1_01_Rhapsodies_of_Vanadiel|" +
              $"RoV {QC.Mission.Rov.Resonance} 0 0",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|" +
              $"{d.Reward.NextLog} {d.Reward.NextMission} {d.Reward.KeyItem ?? 0} {d.Reward.Item ?? 0}");
        Fake F(int current = 0, int rov1 = 1, int level = 3, bool mogHouse = false)
        {
            var f = new Fake { ThisMission = (MissionLog.RoV, id), MainLevel = level, InAMogHouse = mogHouse };
            f.CurrentMissions[rov] = current;
            f.MainSettings["ENABLE_ROV"] = rov1;
            return f;
        }
        Check("c2 rhapsodies: the section holds current, ENABLE_ROV 1, level 3, outside a Mog House; fails for " +
              "Resonance current, ENABLE_ROV 0, level 2 and a Mog House",
              "True False False False False",
              $"{d.States[0].IsMissionMatch(F())} {d.States[0].IsMissionMatch(F(current: QC.Mission.Rov.Resonance))} " +
              $"{d.States[0].IsMissionMatch(F(rov1: 0))} {d.States[0].IsMissionMatch(F(level: 2))} " +
              $"{d.States[0].IsMissionMatch(F(mogHouse: true))}");
        ushort[] zones =
        {
            QC.Zone.BastokMarkets, QC.Zone.BastokMines, QC.Zone.NorthernSanDoria, QC.Zone.PortBastok,
            QC.Zone.PortSanDoria, QC.Zone.PortWindurst, QC.Zone.SouthernSanDoria, QC.Zone.WindurstWalls,
            QC.Zone.WindurstWaters, QC.Zone.WindurstWoods,
        };
        Check("c2 rhapsodies: the ten entry zones' zone-ins play 30035, their finishes complete it (the status 0)",
              string.Join(" ", zones.Select(_ => "Event(30035)/Mission.Complete()/True")),
              string.Join(" ", zones.Select(z =>
              {
                  var f = F();
                  string reply = C2MReply(C2MHandler(d, 0, QuestHandlerKind.OnZoneIn, z), f);
                  string run = C2MRun(C2MHandler(d, 0, QuestHandlerKind.OnEventFinish, z, id: 30035), f);
                  return $"{reply}/{run}/{f.HasCompletedMission(rov, id)}";
              })));
        Check("c2 rhapsodies: one section, its 20 handlers in the ten zones only", "1 20 10",
              $"{d.States.Count} {d.States[0].Handlers.Count} " +
              $"{d.States[0].Handlers.Select(h => h.Zone).Distinct().Count(z => zones.Contains(z))}");
    }

    // quest rework C2 Task 9: San d'Oria 1-3 Save the Children, one check per handler with logic
    static void RunC2SaveTheChildrenMission()
    {
        var m = new QC.Missions.Sandoria.SaveTheChildren();
        var d = m.Compile();
        int sandoria = QC.MissionLog.Sandoria, id = QC.Mission.Sandoria.SaveTheChildren;
        const int none = QC.Mission.Sandoria.None, hutKey = QC.KeyItem.OrcishHutKey;
        Check("c2 save the children: clean, its key and LegacyPath, no reward table (the Lua's is empty)",
              $"|{QC.MissionKey.Sandoria.SaveTheChildren}|scripts/missions/sandoria/1_3_Save_the_Children|True",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.Reward == null}");
        Fake F(int current, int status = 0, int nation = 0, bool completed = false, bool key = false, int option = 0,
               int optionVar = 0, int battlefieldWin = 0)
        {
            var f = new Fake { ThisMission = (MissionLog.Sandoria, id), NationId = nation, Option = option };
            f.CurrentMissions[sandoria] = current;
            f.MissionStatuses[sandoria] = status;
            if (completed) f.CompletedMissions.Add($"{sandoria}/{id}");
            if (key) f.KeyItems[hutKey] = true;
            if (optionVar != 0) f.MissionVars[$"{sandoria}/{id}/Option"] = optionVar;
            if (battlefieldWin != 0) f.PlayerLocalVars["battlefieldWin"] = battlefieldWin;
            f.TextIds[QC.Text.NorthernSanDoria.ORIGINAL_MISSION_OFFSET.ToString()] = 7000;
            f.TextIds[QC.Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET.ToString()] = 8000;
            return f;
        }
        Check("c2 save the children: section 1 holds with no current mission for a San d'Orian; fails for a " +
              "Bastokan and with 1-3 current",
              "True False False",
              $"{d.States[0].IsMissionMatch(F(none))} {d.States[0].IsMissionMatch(F(none, nation: 1))} " +
              $"{d.States[0].IsMissionMatch(F(id))}");
        var fin = QuestHandlerKind.OnEventFinish;
        var trig = QuestHandlerKind.OnTrigger;
        foreach (var (zone, csid, label) in new[]
                 {
                     (QC.Zone.SouthernSanDoria, 1009, "S. 1009"), (QC.Zone.SouthernSanDoria, 2009, "S. 2009"),
                     (QC.Zone.NorthernSanDoria, 1009, "N. 1009"),
                 })
        {
            var h = C2MHandler(d, 0, fin, zone, id: csid);
            Check($"c2 save the children: {label}'s finish: option 102 begins at status 1; the cancel option nothing",
                  $"Mission.Begin(),Player.SetMissionStatus({sandoria},1)|",
                  $"{C2MRun(h, F(none, option: 102))}|{C2MRun(h, F(none, option: 1073741824))}");
        }

        Check("c2 save the children: section 2 holds while current below status 3", "True True True False False",
              $"{d.States[1].IsMissionMatch(F(id, 0))} {d.States[1].IsMissionMatch(F(id, 1))} " +
              $"{d.States[1].IsMissionMatch(F(id, 2))} {d.States[1].IsMissionMatch(F(id, 3))} " +
              $"{d.States[1].IsMissionMatch(F(none, 0))}");
        var arnau = C2MHandler(d, 1, trig, QC.Zone.NorthernSanDoria, QC.Npc.NorthernSanDoria.Arnau);
        Check("c2 save the children: Arnau: 693 at status 0 and 1; nothing at 2",
              "ProgressEvent(693)|ProgressEvent(693)|null",
              $"{C2MReply(arnau, F(id, 0))}|{C2MReply(arnau, F(id, 1))}|{C2MReply(arnau, F(id, 2))}");
        Check("c2 save the children: 693's finish: status 2", $"Player.SetMissionStatus({sandoria},2)",
              C2MRun(C2MHandler(d, 1, fin, QC.Zone.NorthernSanDoria, id: 693), F(id, 1)));
        foreach (var (zone, npc, first, offset) in new[]
                 {
                     (QC.Zone.NorthernSanDoria, QC.Npc.NorthernSanDoria.Grilau, 1025, 7000),
                     (QC.Zone.SouthernSanDoria, QC.Npc.SouthernSanDoria.Ambrotien, 2025, 8000),
                     (QC.Zone.SouthernSanDoria, QC.Npc.SouthernSanDoria.Endracion, 1025, 8000),
                 })
        {
            var h = C2MHandler(d, 1, trig, zone, npc);
            Check($"c2 save the children: {npc} in section 2: {first} at status 0, else the zone's " +
                  "ORIGINAL_MISSION_OFFSET + 24 (messageText)",
                  $"ProgressEvent({first})|MessageText({offset + 24})|MessageText({offset + 24})",
                  $"{C2MReply(h, F(id, 0))}|{C2MReply(h, F(id, 1))}|{C2MReply(h, F(id, 2))}");
        }
        var win = C2MHandler(d, 1, fin, QC.Zone.GhelsbaOutpost, id: 32001);
        Check("c2 save the children: Ghelsba's 32001 finish: battlefieldWin Save the Children (32): the key, the " +
              "title, status 3; another fight's win (Wings of Fury, 34) and none: nothing",
              $"Player.GiveKeyItem({hutKey}),Player.SetTitle({QC.Title.FodderchiefFlayer})," +
              $"Player.SetMissionStatus({sandoria},3)||",
              $"{C2MRun(win, F(id, 2, battlefieldWin: QC.Battlefield.SaveTheChildren))}|" +
              $"{C2MRun(win, F(id, 2, battlefieldWin: QC.Battlefield.WingsOfFury))}|{C2MRun(win, F(id, 2))}");

        Check("c2 save the children: section 3 holds current at status 3 with the key; fails without it, at 4, " +
              "not current",
              "True False False False",
              $"{d.States[2].IsMissionMatch(F(id, 3, key: true))} {d.States[2].IsMissionMatch(F(id, 3))} " +
              $"{d.States[2].IsMissionMatch(F(id, 4, key: true))} " +
              $"{d.States[2].IsMissionMatch(F(none, 3, key: true))}");
        var door = C2MHandler(d, 2, trig, QC.Zone.GhelsbaOutpost, QC.Npc.GhelsbaOutpost.HutDoor);
        Check("c2 save the children: Hut_Door: 3 when 1-3 was completed before, else 55",
              "ProgressEvent(3)|ProgressEvent(55)",
              $"{C2MReply(door, F(id, 3, completed: true, key: true))}|{C2MReply(door, F(id, 3, key: true))}");
        foreach (int csid in new[] { 3, 55 })
            Check($"c2 save the children: Hut_Door {csid}'s finish: the key taken, status 4",
                  $"Player.DelKeyItem({hutKey}),Player.SetMissionStatus({sandoria},4)",
                  C2MRun(C2MHandler(d, 2, fin, QC.Zone.GhelsbaOutpost, id: csid), F(id, 3, key: true)));

        Check("c2 save the children: section 4 holds current at status 4 only", "True False False",
              $"{d.States[3].IsMissionMatch(F(id, 4))} {d.States[3].IsMissionMatch(F(id, 3))} " +
              $"{d.States[3].IsMissionMatch(F(none, 4))}");
        foreach (var (zone, npc, first, repeat) in new[]
                 {
                     (QC.Zone.SouthernSanDoria, QC.Npc.SouthernSanDoria.Ambrotien, 2004, 2024),
                     (QC.Zone.SouthernSanDoria, QC.Npc.SouthernSanDoria.Endracion, 1004, 1024),
                     (QC.Zone.NorthernSanDoria, QC.Npc.NorthernSanDoria.Grilau, 1004, 1024),
                 })
        {
            var h = C2MHandler(d, 3, trig, zone, npc);
            Check($"c2 save the children: {npc} in section 4: {first} the first time, {repeat} on a repeat",
                  $"ProgressEvent({first})|ProgressEvent({repeat})",
                  $"{C2MReply(h, F(id, 4))}|{C2MReply(h, F(id, 4, completed: true))}");
        }
        foreach (var (zone, csid, label) in new[]
                 {
                     (QC.Zone.SouthernSanDoria, 1004, "S. 1004"), (QC.Zone.SouthernSanDoria, 1024, "S. 1024"),
                     (QC.Zone.SouthernSanDoria, 2004, "S. 2004"), (QC.Zone.SouthernSanDoria, 2024, "S. 2024"),
                     (QC.Zone.NorthernSanDoria, 1004, "N. 1004"), (QC.Zone.NorthernSanDoria, 1024, "N. 1024"),
                 })
        {
            var h = C2MHandler(d, 3, fin, zone, id: csid);
            var failed = F(id, 4);
            failed.CompleteResult = false;
            Check($"c2 save the children: {label}'s finish: first: rank 2 and 1000 gil; a repeat: 250 rank points; " +
                  "then complete and Option 1; complete false: no Option",
                  "Player.SetRank(2),Player.GiveCurrency(gil,1000),Mission.Complete(),Mission.SetVar(Option,1)|" +
                  "Player.AddRankPoints(250),Mission.Complete(),Mission.SetVar(Option,1)|" +
                  "Player.SetRank(2),Player.GiveCurrency(gil,1000),Mission.Complete()",
                  $"{C2MRun(h, F(id, 4))}|{C2MRun(h, F(id, 4, completed: true))}|{C2MRun(h, failed)}");
        }

        Check("c2 save the children: section 5 holds completed with Option 1; fails at Option 0 and not completed",
              "True False False",
              $"{d.States[4].IsMissionMatch(F(none, completed: true, optionVar: 1))} " +
              $"{d.States[4].IsMissionMatch(F(none, completed: true))} " +
              $"{d.States[4].IsMissionMatch(F(id, optionVar: 1))}");
        Check("c2 save the children: Arnau in section 5: 694; its finish: Option 0",
              "ProgressEvent(694)|Mission.SetVar(Option,0)",
              $"{C2MReply(C2MHandler(d, 4, trig, QC.Zone.NorthernSanDoria, QC.Npc.NorthernSanDoria.Arnau), F(none))}|" +
              C2MRun(C2MHandler(d, 4, fin, QC.Zone.NorthernSanDoria, id: 694),
                     F(none, completed: true, optionVar: 1)));
        Check("c2 save the children: five sections, as the Lua's", "5", d.States.Count.ToString());
    }

    // the facet members San d'Oria 1-3 added: Player.AddRankPoints, a messageText by id
    static void RunC2SaveTheChildrenFacets()
    {
        var f = new Fake();
        f.RankPointsHeld = 100;
        f.Player.AddRankPoints(250);
        Check("c2 facets: AddRankPoints recorded, the points moved by it; MessageText by id",
              "Player.AddRankPoints(250) 350 MessageText(7024)",
              $"{string.Join(",", f.Calls)} {f.RankPoints} {QuestContextFake.Describe(QuestReply.MessageText(7024))}");
    }

    // the facet members CoP 1-1 added, as the fake records them
    static void RunC2PortFacets()
    {
        var f = new Fake();
        f.Player.StartEventHidden(36);
        f.Player.EnableEntities(7, 6);
        f.Player.EnableEntities();
        f.Player.SendPartialMissionLog(6, false);
        Check("c2 facets: StartEventHidden, EnableEntities (two ids, none), SendPartialMissionLog",
              "Player.StartEventHidden(36),Player.EnableEntities(7;6),Player.EnableEntities()," +
              "Player.SendPartialMissionLog(6,False)", string.Join(",", f.Calls));
    }
}
