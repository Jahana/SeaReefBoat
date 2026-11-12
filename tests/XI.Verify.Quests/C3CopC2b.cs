// XI.Verify.Quests -- quest rework C3 C2b: CoP 4-1 to 4-4 and the Savage battlefield. Each handler's arms one at a time
// (the traces hold the packets of the recorded flows), the arms the lookup order shadows in the traces, the values no
// trace sees, and the data of the battlefield against the Lua file's (as it stood at the recording).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    internal static void RunC3CopC2b()
    {
        Console.WriteLine("\n== c3 C2b: CoP 4-1 to 4-4 and the Savage ==");
        RunC3C2bShelteringDoubt();
        RunC3C2bSavage();
        RunC3C2bSlanderousUtterings();
        RunC3C2bSecretsOfWorship();
    }

    const int C2bCop = 6;

    static Fake C2bFake(int mission, int status = 0)
    {
        var f = new Fake { ThisMission = (MissionLog.CoP, mission) };
        f.CurrentMissions[C2bCop] = mission;
        f.MissionVars[$"{C2bCop}/{mission}/Status"] = status;
        return f;
    }

    static string C2bReply(MissionScriptDefinition d, int state, QuestHandlerKind kind, ushort zone, string entity,
                           Fake f, int id = 1) =>
        C2MReply(C2MHandler(d, state, kind, zone, entity, id), f);

    static void RunC3C2bShelteringDoubt()
    {
        var onTrig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var d = new QC.Missions.Cop.ShelteringDoubt().Compile();
        int m = QC.Mission.Cop.ShelteringDoubt;
        Check("c3 C2b: 4-1 is a CoP mission with its Lua file and The Savage next",
              "mission.cop.sheltering_doubt scripts/missions/cop/4_1_Sheltering_Doubt " + QC.Mission.Cop.TheSavage,
              $"{new QC.Missions.Cop.ShelteringDoubt().Key} {new QC.Missions.Cop.ShelteringDoubt().LegacyPath} " +
              $"{QC.Mission.Cop.TheSavage}");
        Check("c3 C2b: 4-1 validates clean", "", string.Join(" | ", QuestScriptValidator.Validate(d)));
        Check("c3 C2b: 4-1 Tavnazian Safehold's zone-in plays 107 at Status 0 only",
              "Event(107)|null|null",
              string.Join("|", new[]
              {
                  C2bReply(d, 0, QuestHandlerKind.OnZoneIn, QC.Zone.TavnazianSafehold, null, C2bFake(m, 0)),
                  C2bReply(d, 0, QuestHandlerKind.OnZoneIn, QC.Zone.TavnazianSafehold, null, C2bFake(m, 1)),
                  C2bReply(d, 0, QuestHandlerKind.OnZoneIn, QC.Zone.TavnazianSafehold, null, C2bFake(m, 3)),
              }));
        Check("c3 C2b: 4-1 Despachiaire plays 108 at Status 1 only",
              "null|ProgressEvent(108)|null|null",
              string.Join("|", Enumerable.Range(0, 4).Select(s =>
                  C2bReply(d, 0, onTrig, QC.Zone.TavnazianSafehold, QC.Npc.TavnazianSafehold.Despachiaire,
                           C2bFake(m, s)))));
        Check("c3 C2b: 4-1 Justinius plays 109 (important) at Status 2, 266 (replacing the default) at Status 3, " +
              "else nothing; the completed state plays 266 whatever the status",
              "null|null|Event(109):importantEvent()|Event(266):replaceDefault()|"+
              "Event(266):replaceDefault()",
              string.Join("|", new[]
              {
                  C2bReply(d, 0, onTrig, QC.Zone.TavnazianSafehold, QC.Npc.TavnazianSafehold.Justinius, C2bFake(m, 0)),
                  C2bReply(d, 0, onTrig, QC.Zone.TavnazianSafehold, QC.Npc.TavnazianSafehold.Justinius, C2bFake(m, 1)),
                  C2bReply(d, 0, onTrig, QC.Zone.TavnazianSafehold, QC.Npc.TavnazianSafehold.Justinius, C2bFake(m, 2)),
                  C2bReply(d, 0, onTrig, QC.Zone.TavnazianSafehold, QC.Npc.TavnazianSafehold.Justinius, C2bFake(m, 3)),
                  C2bReply(d, 1, onTrig, QC.Zone.TavnazianSafehold, QC.Npc.TavnazianSafehold.Justinius, C2bFake(m, 0)),
              }));
        Check("c3 C2b: 4-1 Misareaux Coast's _0p0 plays cutscene 7 from Status 2 on",
              "null|null|ProgressCutscene(7)|ProgressCutscene(7)",
              string.Join("|", Enumerable.Range(0, 4).Select(s =>
                  C2bReply(d, 0, onTrig, QC.Zone.MisareauxCoast, QC.Npc.MisareauxCoast._0p0, C2bFake(m, s)))));
        Fake Done(int mission)
        {
            var f = new Fake();
            f.CompletedMissions.Add($"{C2bCop}/{mission}");
            return f;
        }
        string Held(Fake f) => d.States[1].MissionMatches[0].When(f).ToString();
        Check("c3 C2b: 4-1's completed state holds when 4-1 itself is completed, not when only The Savage is",
              "True|False|False",
              string.Join("|", new[]
              {
                  Held(Done(m)), Held(Done(QC.Mission.Cop.TheSavage)), Held(new Fake()),
              }));
        Check("c3 C2b: 4-1's finishes: 107 sets Status 1, 108 sets 2, 109 sets 3, 7 completes",
              "Mission.SetVar(Status,1)|Mission.SetVar(Status,2)|Mission.SetVar(Status,3)|Mission.Complete()",
              string.Join("|", new[]
              {
                  C2MRun(C2MHandler(d, 0, fin, QC.Zone.TavnazianSafehold, id: 107), C2bFake(m)),
                  C2MRun(C2MHandler(d, 0, fin, QC.Zone.TavnazianSafehold, id: 108), C2bFake(m)),
                  C2MRun(C2MHandler(d, 0, fin, QC.Zone.TavnazianSafehold, id: 109), C2bFake(m)),
                  C2MRun(C2MHandler(d, 0, fin, QC.Zone.MisareauxCoast, id: 7), C2bFake(m)),
              }));
    }

    static void RunC3C2bSavage()
    {
        var onTrig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var ms = new QC.Missions.Cop.TheSavage();
        var d = ms.Compile();
        int m = QC.Mission.Cop.TheSavage;
        Check("c3 C2b: 4-2 is a CoP mission with its Lua file; its reward is the Nagmolada's Underling title and " +
              "The Secrets of Worship next; it validates clean",
              $"mission.cop.the_savage scripts/missions/cop/4_2_The_Savage {QC.Title.NagmoladasUnderling} " +
              $"{QC.Mission.Cop.TheSecretsOfWorship} ",
              $"{ms.Key} {ms.LegacyPath} {d.Reward.Title} {d.Reward.NextMission} " +
              string.Join(" | ", QuestScriptValidator.Validate(d)));
        Check("c3 C2b: 4-2 Misareaux Coast's _0p2 plays cutscene 8 at Status 0 only",
              "ProgressCutscene(8)|null|null|null",
              string.Join("|", Enumerable.Range(0, 4).Select(s =>
                  C2bReply(d, 0, onTrig, QC.Zone.MisareauxCoast, QC.Npc.MisareauxCoast._0p2, C2bFake(m, s)))));
        string Eight(int option)
        {
            var f = C2bFake(m);
            f.Option = option;
            return C2MRun(C2MHandler(d, 0, fin, QC.Zone.MisareauxCoast, id: 8), f);
        }
        Check("c3 C2b: 4-2's finish of 8: option 1 sets Status 1 and the char down in Riverne - Site #B01; any other " +
              "option does nothing",
              "Mission.SetVar(Status,1),Player.SetPos(729.749,-20.325,407.153,90,29)|||",
              string.Join("|", new[] { Eight(1), Eight(0), Eight(2), Eight(-1) }));
        string Win(int win, int status)
        {
            var f = C2bFake(m, status);
            f.PlayerLocalVars["battlefieldWin"] = win;
            return C2MRun(C2MHandler(d, 0, fin, QC.Zone.MonarchLinn, id: 32001), f);
        }
        int sav = QC.Battlefield.Savage;
        Check("c3 C2b: 4-2's 32001 finish (Monarch Linn) sets Status 2 only when the win is the Savage's and the " +
              "status is 1, each condition alone",
              "Mission.SetVar(Status,2)|||||",
              string.Join("|", new[]
              {
                  Win(sav, 1), Win(sav, 0), Win(sav, 2), Win(QC.Battlefield.AncientVows, 1), Win(0, 1), Win(sav, 3),
              }));
        Check("c3 C2b: 4-2 Justinius plays 110 as a progress event at Status 2, else 130 as an important event",
              "Event(130):importantEvent()|Event(130):importantEvent()|ProgressEvent(110)|" +
              "Event(130):importantEvent()",
              string.Join("|", Enumerable.Range(0, 4).Select(s =>
                  C2bReply(d, 0, onTrig, QC.Zone.TavnazianSafehold, QC.Npc.TavnazianSafehold.Justinius,
                           C2bFake(m, s)))));
        Check("c3 C2b: 4-2's finish of 110 (Tavnazian Safehold) completes the mission", "Mission.Complete()",
              C2MRun(C2MHandler(d, 0, fin, QC.Zone.TavnazianSafehold, id: 110), C2bFake(m)));

        var bs = new QC.Battlefields.MonarchLinn.Savage();
        var bd = (BattlefieldMissionData)bs.Data;
        Check("c3 C2b: the Savage validates clean, its key and Lua file",
              "|battlefield.savage scripts/battlefields/Monarch_Linn/savage",
              $"{string.Join(" | ", BattlefieldScriptValidator.Validate(bs))}|{bs.Key} {bs.LegacyPath}");
        Check("c3 C2b: the Savage's fields (zone, id, no exp loss, mission, trusts, players, MAX_LEVEL, time, index, " +
              "grantXP, title, entry and exit NPCs, mission, required var and value, groups)",
              "31 961 False True True 6 MAX_LEVEL 1800 1 1500 MistMelter SD_Entrance " +
              "SD_BCNM_Exit_1,SD_BCNM_Exit_2,SD_BCNM_Exit_3 6/418 Mission[6][418]Status 1 1",
              $"{bd.ZoneId} {bd.BattlefieldId} {bd.CanLoseExp} {bd.IsMission} {bd.AllowTrusts} {bd.MaxPlayers} " +
              $"{bd.LevelCapSetting} {bd.TimeLimit} {bd.Index} {bd.GrantXP} " +
              $"{(bd.Title == QC.Title.MistMelter ? "MistMelter" : bd.Title.ToString())} " +
              $"{string.Join(",", bd.EntryNpcs)} {string.Join(",", bd.ExitNpcs)} {(int)bd.MissionArea}/{bd.Mission} " +
              $"{bd.RequiredVar} {bd.RequiredValue} {bd.Groups.Count}");
        Check("c3 C2b: the Savage's group: one Ouryu to an area (+0, +1, +2), allDeath wins",
              "OURYU:0;OURYU:1;OURYU:2 True",
              string.Join(";", bd.Groups[0].AreaMobIds.Select(a => a[0].Key + ":" +
                  string.Join(",", a.Select(o => o.Offset)))) + $" {bd.Groups[0].AllDeath != null}");
        Check("c3 C2b: the Savage overrides entryRequirement alone",
              "EntryRequirement", string.Join(",", BattlefieldScript.HookNames.Where(bs.Overrides)));
        string Entry(int prev, bool registrant)
        {
            var (q, p) = C2BContext(bs, "SD_Entrance");
            p.PrevZone = prev;
            return bs.EntryRequirement(q, registrant).ToString();
        }
        Check("c3 C2b: the Savage's entryRequirement: refused from Riverne - Site #A01 (either role), allowed from " +
              "Site #B01 and from nowhere",
              "False False True True True",
              $"{Entry(QC.Zone.RiverneSiteA01, true)} {Entry(QC.Zone.RiverneSiteA01, false)} " +
              $"{Entry(QC.Zone.RiverneSiteB01, true)} {Entry(QC.Zone.RiverneSiteB01, false)} {Entry(0, true)}");
    }

    static void RunC3C2bSlanderousUtterings()
    {
        var onTrig = QuestHandlerKind.OnTrigger;
        var area = QuestHandlerKind.OnTriggerAreaEnter;
        var fin = QuestHandlerKind.OnEventFinish;
        var ms = new QC.Missions.Cop.SlanderousUtterings();
        var d = ms.Compile();
        int m = QC.Mission.Cop.SlanderousUtterings;
        Check("c3 C2b: 4-4 is a CoP mission with its Lua file; its reward is The Lost One title and The Enduring " +
              "Tumult of War next; it validates clean",
              $"mission.cop.slanderous_utterings scripts/missions/cop/4_4_Slanderous_Utterings {QC.Title.TheLostOne} " +
              $"{QC.Mission.Cop.TheEnduringTumultOfWar} ",
              $"{ms.Key} {ms.LegacyPath} {d.Reward.Title} {d.Reward.NextMission} " +
              string.Join(" | ", QuestScriptValidator.Validate(d)));
        Check("c3 C2b: 4-4 Despachiaire plays 132 as an important event whatever the status",
              "Event(132):importantEvent()|Event(132):importantEvent()|Event(132):importantEvent()",
              string.Join("|", Enumerable.Range(0, 3).Select(s =>
                  C2bReply(d, 0, onTrig, QC.Zone.TavnazianSafehold, QC.Npc.TavnazianSafehold.Despachiaire,
                           C2bFake(m, s)))));
        Check("c3 C2b: 4-4's trigger area 2 plays 112 at Status 0 only",
              "ProgressEvent(112)|null|null|null",
              string.Join("|", Enumerable.Range(0, 4).Select(s =>
                  C2bReply(d, 0, area, QC.Zone.TavnazianSafehold, null, C2bFake(m, s), 2))));
        Check("c3 C2b: 4-4 Sealion's Den's _0w0 plays 13 at Status 1 only",
              "null|ProgressEvent(13)|null|null",
              string.Join("|", Enumerable.Range(0, 4).Select(s =>
                  C2bReply(d, 0, onTrig, QC.Zone.SealionsDen, QC.Npc.SealionsDen._0w0, C2bFake(m, s)))));
        Check("c3 C2b: 4-4's finishes: 112 sets Status 1, 13 completes",
              "Mission.SetVar(Status,1)|Mission.Complete()",
              string.Join("|", new[]
              {
                  C2MRun(C2MHandler(d, 0, fin, QC.Zone.TavnazianSafehold, id: 112), C2bFake(m)),
                  C2MRun(C2MHandler(d, 0, fin, QC.Zone.SealionsDen, id: 13), C2bFake(m)),
              }));
        Fake Done(int mission)
        {
            var f = new Fake();
            f.CompletedMissions.Add($"{C2bCop}/{mission}");
            return f;
        }
        string Held(Fake f) => d.States[1].MissionMatches[0].When(f).ToString();
        Check("c3 C2b: 4-4's completed state holds when 4-4 itself is completed, not when only 4-3 is",
              "True|False|False",
              string.Join("|", new[]
              {
                  Held(Done(m)), Held(Done(QC.Mission.Cop.TheSecretsOfWorship)), Held(new Fake()),
              }));
        Check("c3 C2b: 4-4 completed: Arquil plays 293 and Despachiaire 317, both replacing the default",
              "Event(293):replaceDefault()|Event(317):replaceDefault()",
              C2bReply(d, 1, onTrig, QC.Zone.TavnazianSafehold, QC.Npc.TavnazianSafehold.Arquil, new Fake()) + "|" +
              C2bReply(d, 1, onTrig, QC.Zone.TavnazianSafehold, QC.Npc.TavnazianSafehold.Despachiaire, new Fake()));
    }

    static void RunC3C2bSecretsOfWorship()
    {
        var onTrig = QuestHandlerKind.OnTrigger;
        var death = QuestHandlerKind.OnMobDeath;
        var fin = QuestHandlerKind.OnEventFinish;
        var ms = new QC.Missions.Cop.TheSecretsOfWorship();
        var d = ms.Compile();
        int m = QC.Mission.Cop.TheSecretsOfWorship;
        ushort saf = QC.Zone.TavnazianSafehold, coast = QC.Zone.MisareauxCoast, sac = QC.Zone.Sacrarium;
        int key = QC.KeyItem.ReliquiariumKey;
        Check("c3 C2b: 4-3 is a CoP mission with its Lua file; its reward is no title and Slanderous Utterings " +
              "next; it validates clean",
              $"mission.cop.the_secrets_of_worship scripts/missions/cop/4_3_The_Secrets_of_Worship  " +
              $"{QC.Mission.Cop.SlanderousUtterings} ",
              $"{ms.Key} {ms.LegacyPath} {d.Reward.Title} {d.Reward.NextMission} " +
              string.Join(" | ", QuestScriptValidator.Validate(d)));
        string Reps(ushort zone, string npc, int n) => string.Join("|", Enumerable.Range(0, n).Select(st =>
            C2bReply(d, 0, onTrig, zone, npc, C2bFake(m, st))));
        Check("c3 C2b: 4-3 _0qa plays 111 at Status 0 only",
              "ProgressEvent(111)|null|null|null", Reps(saf, QC.Npc.TavnazianSafehold._0qa, 4));
        Check("c3 C2b: 4-3 Justinius plays 131 as an important event at Status 0 only",
              "Event(131):importantEvent()|null|null|null", Reps(saf, QC.Npc.TavnazianSafehold.Justinius, 4));
        Check("c3 C2b: 4-3 Parelbriaux plays 137 at Status 1 only",
              "null|Event(137)|null|null", Reps(saf, QC.Npc.TavnazianSafehold.Parelbriaux, 4));
        Check("c3 C2b: 4-3 Misareaux Coast's _0p8 plays cutscene 9 at Status 1, 502 above it, nothing at Status 0",
              "null|ProgressCutscene(9)|Event(502)|Event(502)|Event(502)",
              Reps(coast, QC.Npc.MisareauxCoast._0p8, 5));
        string Fin(ushort zone, int id, int option)
        {
            var f = C2bFake(m);
            f.Option = option;
            return C2MRun(C2MHandler(d, 0, fin, zone, id: id), f);
        }
        string setDown = "Player.SetPos(-220.075,-15.999,79.634,62,28)";
        Check("c3 C2b: 4-3's finish of 111 sets Status 1", "Mission.SetVar(Status,1)", Fin(saf, 111, 0));
        Check("c3 C2b: 4-3's finish of 9: option 1 sets Status 2 and the char down in the Sacrarium, any other " +
              "option nothing",
              $"Mission.SetVar(Status,2),{setDown}|||",
              string.Join("|", new[] { Fin(coast, 9, 1), Fin(coast, 9, 0), Fin(coast, 9, 2), Fin(coast, 9, -1) }));
        Check("c3 C2b: 4-3's finish of 502: option 0 sets the char down in the Sacrarium, any other option nothing",
              $"{setDown}|||",
              string.Join("|", new[]
              {
                  Fin(coast, 502, 0), Fin(coast, 502, 1), Fin(coast, 502, 2), Fin(coast, 502, -1),
              }));
        Check("c3 C2b: 4-3's finish of 5 completes, of 6 sets Status 3",
              "Mission.Complete()|Mission.SetVar(Status,3)", Fin(sac, 5, 0) + "|" + Fin(sac, 6, 0));

        string Gate(int status, double x, bool hasKey)
        {
            var f = C2bFake(m, status);
            f.XPos = x;
            f.KeyItems[key] = hasKey;
            return C2bReply(d, 0, onTrig, sac, QC.Npc.Sacrarium._0s8, f);
        }
        string side = $"MessageSpecial({QC.Text.Sacrarium.CANNOT_OPEN_SIDE})";
        Check("c3 C2b: 4-3's Wooden Gate west of x 45 (45 itself too) answers CANNOT_OPEN_SIDE at every status",
              $"{side}|{side}|{side}|{side}|{side}",
              string.Join("|", new[]
              {
                  Gate(2, 44, false), Gate(2, 45, false), Gate(3, 45, true), Gate(0, 0, false), Gate(1, -100, true),
              }));
        Check("c3 C2b: 4-3's Wooden Gate east of x 45: cutscene 6 (with the key item to show) at Status 2 whether or " +
              "not the key is held, cutscene 5 at Status 3 with the key only, no reply otherwise",
              $"ProgressCutscene(6,0,{key})|ProgressCutscene(6,0,{key})|ProgressCutscene(5)|null|null|null|null",
              string.Join("|", new[]
              {
                  Gate(2, 45.01, false), Gate(2, 46, true), Gate(3, 46, true), Gate(3, 46, false),
                  Gate(1, 46, true), Gate(0, 46, true), Gate(4, 46, true),
              }));

        string Qm(int status, bool hasKey, int killed, int profVar, bool spawned, string npc)
        {
            var f = C2bFake(m, status);
            f.KeyItems[key] = hasKey;
            f.MissionLocalVars["hasKilledMariselle"] = killed;
            f.Npc = new QuestNpcFake(1) { XPos = 62.5, YPos = -3.25, ZPos = 127.75 };
            f.Npc.LocalVars["hasProfessorMariselle"] = profVar;
            if (spawned) f.SpawnedMobs.Add("OLD_PROFESSOR_MARISELLE+0");
            string r = C2bReply(d, 0, onTrig, sac, npc, f);
            return $"{r} [{string.Join(",", f.Calls)}]";
        }
        string Qm1(int status, bool hasKey, int killed, int profVar, bool spawned) =>
            Qm(status, hasKey, killed, profVar, spawned, QC.Npc.Sacrarium.qmprof1);
        string pos = "62.5,-3.25,127.75,0";
        string pop = $"Player.SetMobSpawn(OLD_PROFESSOR_MARISELLE,{pos})," +
                     $"Player.SetMobSpawn(OLD_PROFESSOR_MARISELLE+1,{pos})," +
                     $"Player.SetMobSpawn(OLD_PROFESSOR_MARISELLE+2,{pos})," +
                     "Player.PopFromQm(OLD_PROFESSOR_MARISELLE,True,0,radius=2)";
        Check("c3 C2b: 4-3's qm_prof_1 at Status 3 without the key item: Mariselle killed gives the key item and no " +
              "action (whatever the professor var or spawn), the professor's var pops her at the qm's spot " +
              "(three spawn points, then the pop) and says EVIL_PRESENCE, spawned says DRAWER_SHUT",
              $"NoAction() [Player.GiveKeyItem({key})]|NoAction() [Player.GiveKeyItem({key})]|" +
              $"MessageSpecial({QC.Text.Sacrarium.EVIL_PRESENCE}) [{pop}]|" +
              $"MessageSpecial({QC.Text.Sacrarium.DRAWER_SHUT}) []|null []",
              string.Join("|", new[]
              {
                  Qm1(3, false, 1, 0, false), Qm1(3, false, 1, 1, true), Qm1(3, false, 0, 1, false),
                  Qm1(3, false, 0, 1, true), Qm1(3, false, 0, 0, true),
              }));
        Check("c3 C2b: 4-3's qm_prof_1 does nothing with the key item held, below Status 3 and above it, " +
              "whatever else holds",
              "null []|null []|null []|null []|null []",
              string.Join("|", new[]
              {
                  Qm1(3, true, 1, 1, false), Qm1(3, true, 0, 1, false), Qm1(2, false, 1, 1, false),
                  Qm1(4, false, 1, 1, false), Qm1(0, false, 0, 1, false),
              }));
        Check("c3 C2b: 4-3's six qm_prof qms share the handler (each gives the key item once Mariselle is killed)",
              string.Join("|", Enumerable.Repeat($"NoAction() [Player.GiveKeyItem({key})]", 6)),
              string.Join("|", new[]
              {
                  QC.Npc.Sacrarium.qmprof0, QC.Npc.Sacrarium.qmprof1, QC.Npc.Sacrarium.qmprof2,
                  QC.Npc.Sacrarium.qmprof3, QC.Npc.Sacrarium.qmprof4, QC.Npc.Sacrarium.qmprof5,
              }.Select(n => Qm(3, false, 1, 0, false, n))));

        string Death(int status) => C2MRun(C2MHandler(d, 0, death, sac, QC.Npc.Sacrarium.OldProfessorMariselle),
                                           C2bFake(m, status));
        Check("c3 C2b: 4-3's Old Professor Mariselle death sets the hasKilledMariselle local var at Status 3 only",
              "|||Mission.SetLocalVar(hasKilledMariselle,1)|",
              string.Join("|", new[] { Death(0), Death(1), Death(2), Death(3), Death(4) }));

        string Closed(double x, bool hasKey)
        {
            var f = new Fake { XPos = x };
            f.KeyItems[key] = hasKey;
            string r = C2bReply(d, 1, onTrig, sac, QC.Npc.Sacrarium._0s8, f);
            return $"{r} [{string.Join(",", f.Calls)}]";
        }
        Check("c3 C2b: 4-3 not current: the Wooden Gate east of x 45 starts cutscene 110 with the key item only and " +
              "gives no reply; west of x 45 (45 itself too) it answers CANNOT_OPEN_SIDE",
              $"null [Player.StartCutscene(110)]|null []|{side} []|{side} []|{side} []",
              string.Join("|", new[]
              {
                  Closed(45.01, true), Closed(46, false), Closed(45, true), Closed(40, true), Closed(40, false),
              }));
        Check("c3 C2b: 4-3's two states: the log's current mission being 4-3, then not",
              "True False",
              $"{d.States[0].MissionMatches[0].IsCurrent} {d.States[1].MissionMatches[0].IsCurrent}");
    }
}
