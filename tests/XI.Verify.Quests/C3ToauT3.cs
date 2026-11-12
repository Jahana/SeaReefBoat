// XI.Verify.Quests -- quest rework C3 T3 (ToAU 26-38, the Puppet in Peril and Legacy of the Lost battlefields, and the
// module overrides of 32, 33, 37 and 38 on C#): the arms the golden flows reach only together, each on the one read
// that decides it, and values the replay sees only in part (the battlefield win var, the rewards). The traces hold the
// packets.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    internal static void RunC3ToauT3()
    {
        Console.WriteLine("\n== c3 T3: ToAU 26-38 and two battlefields on C# ==");
        ushort wg = QC.Zone.AhtUrhganWhitegate;
        var naja = QC.Npc.AhtUrhganWhitegate.NajaSalaheem;
        var all = new MissionScript[]
        {
            new QC.Missions.Toau.SealOfTheSerpent(), new QC.Missions.Toau.MisplacedNobility(),
            new QC.Missions.Toau.BastionOfKnowledge(), new QC.Missions.Toau.PuppetInPeril(),
            new QC.Missions.Toau.PrevalenceOfPirates(), new QC.Missions.Toau.ShadesOfVengeance(),
            new QC.Missions.Toau.InTheBlood(), new QC.Missions.Toau.SentinelsHonor(),
            new QC.Missions.Toau.TestingTheWaters(), new QC.Missions.Toau.LegacyOfTheLost(),
            new QC.Missions.Toau.GazeOfTheSaboteur(), new QC.Missions.Toau.PathOfBlood(),
            new QC.Missions.Toau.StirringsOfWar(),
        };
        Check("c3 T3 toau: thirteen missions, each with a legacy path", "13 13",
              $"{all.Length} {all.Count(m => !string.IsNullOrEmpty(m.LegacyPath))}");

        RunC3ToauT3First(wg, naja);
        RunC3ToauT3Timers(wg, naja);
        RunC3ToauT3Later(wg, naja);
        RunC3ToauT3Battlefields();
    }

    static MissionScriptDefinition T3Patched<T>(T mission, string key) where T : MissionScript
    {
        var mod = new QC.Modules.Era.Missions.MissionAdjustments();
        var d = mission.Compile();
        var r = QuestOverrides.Apply(d.Core, mod.Compile(QuestModule.Branches[1].Settings), key);
        return d.WithCore(r.Definition);
    }

    static void RunC3ToauT3First(ushort wg, string naja)
    {
        var area = T1Area;
        var onZone = QuestHandlerKind.OnZoneIn;
        string Rep(MissionScriptDefinition d, QuestHandlerKind k, ushort z, string e, int id, Fake f) =>
            T1Reply(d, 0, k, z, e, id, f);
        string Fin(MissionScriptDefinition d, ushort z, int csid, Fake f) => T1Run(d, 0, T1Fin, z, csid, f);

        // 26 Seal of the Serpent: the Whitegate guard: bare hands, then each hand alone
        var d26 = new QC.Missions.Toau.SealOfTheSerpent().Compile();
        int id26 = QC.Mission.Toau.SealOfTheSerpent;
        var gate = QC.Npc.AhtUrhganWhitegate.ImperialWhitegate;
        Fake Hands(int main, int sub)
        {
            var f = T1F(id26);
            f.Equipment[QC.Slot.Main] = main;
            f.Equipment[QC.Slot.Sub] = sub;
            return f;
        }
        Check("c3 T3 toau 26: Naja 3115 with the rank; the gate: bare hands, main alone, sub alone",
              "Event(3115,3,1,0,0,0,0,0,0,0)|ProgressEvent(3111,0,1,1,0,0,0,0,128)|Event(3114,0,1,0,0,0,0,1,0)|" +
              "Event(3114,0,1,0,0,0,0,1,0)",
              Rep(d26, T1Trig, wg, naja, 1, T1F(id26)) + "|" +
              string.Join("|", new[] { Hands(0, 0), Hands(5, 0), Hands(0, 5) }
                  .Select(f => Rep(d26, T1Trig, wg, gate, 1, f))));
        Check("c3 T3 toau 26: finish 3111 completes", "Mission.Complete()", Fin(d26, wg, 3111, T1F(id26)));

        // 27 Misplaced Nobility
        var d27 = new QC.Missions.Toau.MisplacedNobility().Compile();
        int id27 = QC.Mission.Toau.MisplacedNobility;
        ushort aydeewa = QC.Zone.AydeewaSubterrane;
        Check("c3 T3 toau 27: Naja 3116; the blank NPC 12; finish 12 completes",
              "Event(3116,3,1,0,0,0,0,0,0,0)|ProgressEvent(12,0,4,0,0,0,0,0,0)|Mission.Complete()",
              Rep(d27, T1Trig, wg, naja, 1, T1F(id27)) + "|" +
              Rep(d27, T1Trig, aydeewa, QC.Npc.AydeewaSubterrane.blanktoau20, 1, T1F(id27)) + "|" +
              Fin(d27, aydeewa, 12, T1F(id27)));

        // 28 Bastion of Knowledge
        var d28 = new QC.Missions.Toau.BastionOfKnowledge().Compile();
        int id28 = QC.Mission.Toau.BastionOfKnowledge;
        Check("c3 T3 toau 28: Naja 3116; area 4 plays 3112; finish completes",
              "Event(3116,3,1,0,0,0,0,0,0,0)|ProgressEvent(3112,1,1,0,0,0,0,0,0)|Mission.Complete()",
              Rep(d28, T1Trig, wg, naja, 1, T1F(id28)) + "|" + Rep(d28, area, wg, null, 4, T1F(id28)) + "|" +
              Fin(d28, wg, 3112, T1F(id28)));

        // 29 Puppet in Peril: the Jade Sepulcher door by status; set status; the win var
        var d29 = new QC.Missions.Toau.PuppetInPeril().Compile();
        int id29 = QC.Mission.Toau.PuppetInPeril;
        ushort jade = QC.Zone.JadeSepulcher;
        var jdoor = QC.Npc.JadeSepulcher._1v0;
        Check("c3 T3 toau 29: Naja 3117; the door at status 0, 1, 2; finish 4 sets status 1",
              "Event(3117,3,1,0,0,0,0,0,0,0)|ProgressEvent(4,0,4,0,0,0,0,0,0)|null|null|Player.SetMissionStatus(4,1)",
              Rep(d29, T1Trig, wg, naja, 1, T1F(id29)) + "|" +
              string.Join("|", new[] { 0, 1, 2 }.Select(st => Rep(d29, T1Trig, jade, jdoor, 1, T1F(id29, st)))) + "|" +
              Fin(d29, jade, 4, T1F(id29)));
        Fake Win29(int bf)
        {
            var f = T1F(id29, 1);
            f.PlayerLocalVars["battlefieldWin"] = bf;
            return f;
        }
        Check("c3 T3 toau 29: 32001 completes only for this battlefield's win",
              "Mission.Complete()||", Fin(d29, jade, 32001, Win29(QC.Battlefield.PuppetInPeril)) + "|" +
              Fin(d29, jade, 32001, Win29(QC.Battlefield.PuppetInPeril + 1)) + "|" +
              Fin(d29, jade, 32001, Win29(0)));
        Check("c3 T3 toau 29: the next mission is Prevalence of Pirates, with no title",
              "True|True|True",
              $"{d29.Reward.NextMission == QC.Mission.Toau.PrevalenceOfPirates}|"
              + $"{d29.Reward.NextLog == XI.Quests.MissionLog.ToAU}|{d29.Reward.Title == null}");

        // 30 Prevalence of Pirates
        var d30 = new QC.Missions.Toau.PrevalenceOfPirates().Compile();
        int id30 = QC.Mission.Toau.PrevalenceOfPirates;
        ushort arr = QC.Zone.ArrapagoReef;
        Check("c3 T3 toau 30: Naja 3118; Arrapago zone-in at status 0, 1; area 1 at status 1, 0",
              "Event(3118,3,1,0,0,0,0,0,0,0)|Event(13)|null|ProgressEvent(14,0,4,0,0,0,0,0,0)|null",
              Rep(d30, T1Trig, wg, naja, 1, T1F(id30)) + "|" +
              string.Join("|", new[] { 0, 1 }.Select(st => Rep(d30, onZone, arr, null, 1, T1F(id30, st)))) + "|" +
              string.Join("|", new[] { 1, 0 }.Select(st => Rep(d30, area, arr, null, 1, T1F(id30, st)))));
        Check("c3 T3 toau 30: finish 13 sets status 1; finish 14 completes",
              "Player.SetMissionStatus(4,1)|Mission.Complete()",
              Fin(d30, arr, 13, T1F(id30)) + "|" + Fin(d30, arr, 14, T1F(id30)));
    }

    static void RunC3ToauT3Timers(ushort wg, string naja)
    {
        string Rep(MissionScriptDefinition d, QuestHandlerKind k, ushort z, string e, int id, Fake f) =>
            T1Reply(d, 0, k, z, e, id, f);
        string Fin(MissionScriptDefinition d, ushort z, int csid, Fake f) => T1Run(d, 0, T1Fin, z, csid, f);

        // 31 Shades of Vengeance: Nahshib needs no permit and the day at the Timer, each part alone
        var d31 = new QC.Missions.Toau.ShadesOfVengeance().Compile();
        int id31 = QC.Mission.Toau.ShadesOfVengeance;
        ushort mire = QC.Zone.CaedarvaMire;
        var nahshib = QC.Npc.CaedarvaMire.Nahshib;
        Fake Sh(bool permit, int timer, int day, int status = 0)
        {
            var f = T1F(id31, status);
            f.KeyItems[QC.KeyItem.PeriqiaAssaultAreaEntryPermit] = permit;
            f.MissionVars[$"{ToauLog}/{id31}/Timer"] = timer;
            f.UniqueDay = day;
            return f;
        }
        Check("c3 T3 toau 31: Naja 3119; Nahshib: clear; permit alone; day short alone; day equal",
              "Event(3119,3,1,0,0,0,0,0,0,0)|ProgressEvent(22)|null|null|ProgressEvent(22)",
              Rep(d31, T1Trig, wg, naja, 1, T1F(id31)) + "|" +
              string.Join("|", new[] { Sh(false, 5, 9), Sh(true, 5, 9), Sh(false, 5, 4), Sh(false, 5, 5) }
                  .Select(f => Rep(d31, T1Trig, mire, nahshib, 1, f))));
        Check("c3 T3 toau 31: Caedarva zone-in at status 1, 0; finish 21 completes; finish 22 gives the permit",
              "Event(21)|null|Mission.Complete()|Player.GiveKeyItem(" + QC.KeyItem.PeriqiaAssaultAreaEntryPermit + ")",
              string.Join("|", new[] { 1, 0 }.Select(st =>
                  Rep(d31, QuestHandlerKind.OnZoneIn, mire, null, 1, Sh(false, 0, 0, st)))) + "|" +
              Fin(d31, mire, 21, Sh(false, 0, 0)) + "|" + Fin(d31, mire, 22, Sh(false, 0, 0)));

        // 32 In the Blood: the finish completes then sets the Timer and mustZone; not when the completion fails
        var d32 = new QC.Missions.Toau.InTheBlood().Compile();
        int id32 = QC.Mission.Toau.InTheBlood;
        var f32 = T1F(id32);
        f32.UniqueDay = 40;
        var f32b = T1F(id32);
        f32b.UniqueDay = 40;
        f32b.CompleteResult = false;
        Check("c3 T3 toau 32: Naja 3113 (progress event); finish completes, Timer, mustZone; not when it fails",
              "ProgressEvent(3113,3,1,0,0,0,0,0,0,0)|Mission.Complete(),Player.SetCharVar(Mission[4][32]Timer,41,0)," +
              "Player.SetLocalVar(Mission[4][32]mustZone,1)|Mission.Complete()",
              Rep(d32, T1Trig, wg, naja, 1, T1F(id32)) + "|" + Fin(d32, wg, 3113, f32) + "|" +
              Fin(d32, wg, 3113, f32b));
        var p32 = T3Patched(new QC.Missions.Toau.InTheBlood(), QC.MissionKey.Toau.InTheBlood);
        var g32 = T1F(id32);
        g32.NextJstMidnight = 777;
        var g32b = T1F(id32);
        g32b.NextJstMidnight = 777;
        g32b.CompleteResult = false;
        Check("c3 T3 adjustments toau 32: finish completes, mustZone and the Timer 1 to JST midnight; " +
              "not when it fails",
              "Mission.Complete(),Player.SetLocalVar(Mission[4][32]mustZone,1),Player.SetCharVar(Mission[4][32]Timer," +
              "1,777)|Mission.Complete()",
              Fin(p32, wg, 3113, g32) + "|" + Fin(p32, wg, 3113, g32b));

        // 33 Sentinel's Honor: Naja: hasZoned and hasTimePassed, each part alone
        var d33 = new QC.Missions.Toau.SentinelsHonor().Compile();
        int id33 = QC.Mission.Toau.SentinelsHonor;
        Fake Se(bool mustZone, int timer, int day, int option = 0)
        {
            var f = T1F(id33, 0, mustZone);
            f.MissionVars[$"{ToauLog}/{id33}/Timer"] = timer;
            f.MissionVars[$"{ToauLog}/{id33}/Option"] = option;
            f.UniqueDay = day;
            return f;
        }
        string Naja33(MissionScriptDefinition d, Fake f) =>
            Rep(d, T1Trig, wg, naja, 1, f) + "#" + string.Join(",", f.Calls);
        Check("c3 T3 toau 33: Naja: both true 3130; mustZone alone 3120 (dialog 2); time short alone 3120 (the Option)",
              "Event(3130,3,1,0,0,0,0,0,2,0)#|Event(3120,3,1,0,0,0,0,0,2,0)#|Event(3120,3,1,0,0,0,0,0,0,0)#" +
              "Mission.SetVar(Option,2)|Event(3120,3,1,0,0,0,0,0,2,0)#Mission.SetVar(Option,0)",
              string.Join("|", new[] { Se(false, 5, 9), Se(true, 5, 9), Se(false, 5, 4), Se(false, 5, 4, 2) }
                  .Select(f => Naja33(d33, f))));
        Check("c3 T3 toau 33: the day equal to the Timer counts as passed; both false 3120",
              "Event(3130,3,1,0,0,0,0,0,2,0)#|Event(3120,3,1,0,0,0,0,0,0,0)#Mission.SetVar(Option,2)",
              Naja33(d33, Se(false, 5, 5)) + "|" + Naja33(d33, Se(true, 5, 4)));
        Check("c3 T3 toau 33: finish 3130 completes", "Mission.Complete()", Fin(d33, wg, 3130, Se(false, 0, 0)));
        var p33 = T3Patched(new QC.Missions.Toau.SentinelsHonor(), QC.MissionKey.Toau.SentinelsHonor);
        Check("c3 T3 adjustments toau 33: time has passed only when the Timer is 0: day past the Timer alone is not " +
              "enough, mustZone alone still holds it",
              "Event(3130,3,1,0,0,0,0,0,2,0)#|Event(3120,3,1,0,0,0,0,0,0,0)#Mission.SetVar(Option,2)|" +
              "Event(3120,3,1,0,0,0,0,0,2,0)#|Event(3130,3,1,0,0,0,0,0,2,0)#",
              Naja33(p33, Se(false, 0, 0)) + "|" + Naja33(p33, Se(false, 5, 9)) + "|" + Naja33(p33, Se(true, 0, 0)) +
              "|" + Naja33(p33, Se(false, 0, 9)));
    }

    static void RunC3ToauT3Later(ushort wg, string naja)
    {
        var area = T1Area;
        var onZone = QuestHandlerKind.OnZoneIn;
        string Rep(MissionScriptDefinition d, QuestHandlerKind k, ushort z, string e, int id, Fake f) =>
            T1Reply(d, 0, k, z, e, id, f);
        string Fin(MissionScriptDefinition d, ushort z, int csid, Fake f) => T1Run(d, 0, T1Fin, z, csid, f);
        ushort arr = QC.Zone.ArrapagoReef, talacca = QC.Zone.TalaccaCove;

        // 34 Testing the Waters
        var d34 = new QC.Missions.Toau.TestingTheWaters().Compile();
        int id34 = QC.Mission.Toau.TestingTheWaters;
        Fake Tw(bool mustZone, bool coin, int status = 0, int option = 0)
        {
            var f = T1F(id34, status, mustZone);
            f.KeyItems[QC.KeyItem.EphramadianGoldCoin] = coin;
            f.Option = option;
            return f;
        }
        Check("c3 T3 toau 34: Naja 3132; area 1: clear; mustZone alone; coin alone missing",
              "Event(3132,3,1,0,0,0,0,0,0,0)|ProgressEvent(15,0,0,0,0,0,0,0,0)|null|null",
              Rep(d34, T1Trig, wg, naja, 1, T1F(id34)) + "|" +
              string.Join("|", new[] { Tw(false, true), Tw(true, true), Tw(false, false) }
                  .Select(f => Rep(d34, area, arr, null, 1, f))));
        Check("c3 T3 toau 34: finish 15: option 1 sets status 1 and moves to Talacca Cove; any other sets mustZone",
              "Player.SetMissionStatus(4,1),Player.SetPos(-88.879,-7.318,-109.233,173," + talacca + ")|" +
              "Mission.SetMustZone()|Mission.SetMustZone()",
              Fin(d34, arr, 15, Tw(false, true, 0, 1)) + "|" + Fin(d34, arr, 15, Tw(false, true, 0, 0)) + "|" +
              Fin(d34, arr, 15, Tw(false, true, 0, 2)));
        var f34 = Tw(false, true, 1);
        f34.CompleteResult = false;
        Check("c3 T3 toau 34: Talacca zone-in at status 1, 0; finish 106 completes and drops the coin; not when the " +
              "completion fails",
              "Event(106)|null|Mission.Complete(),Player.DelKeyItem(" + QC.KeyItem.EphramadianGoldCoin +
              ")|Mission.Complete()",
              string.Join("|", new[] { 1, 0 }.Select(st => Rep(d34, onZone, talacca, null, 1, Tw(false, true, st)))) +
              "|" + Fin(d34, talacca, 106, Tw(false, true, 1)) + "|" + Fin(d34, talacca, 106, f34));

        // 35 Legacy of the Lost
        var d35 = new QC.Missions.Toau.LegacyOfTheLost().Compile();
        int id35 = QC.Mission.Toau.LegacyOfTheLost;
        Fake Win35(int bf)
        {
            var f = T1F(id35);
            f.PlayerLocalVars["battlefieldWin"] = bf;
            return f;
        }
        Check("c3 T3 toau 35: Naja 3133; 32001 completes only for this battlefield's win",
              "Event(3133,3,1,0,0,0,0,0,0,0)|Mission.Complete()||",
              Rep(d35, T1Trig, wg, naja, 1, T1F(id35)) + "|" +
              Fin(d35, talacca, 32001, Win35(QC.Battlefield.LegacyOfTheLost)) + "|" +
              Fin(d35, talacca, 32001, Win35(QC.Battlefield.LegacyOfTheLost + 1)) + "|" +
              Fin(d35, talacca, 32001, Win35(0)));
        Check("c3 T3 toau 35: the mission reward is the Gessho's Mercy title and Gaze of the Saboteur next (the " +
              "battlefield's own Title grants the same title first; this read pins the mission's field)",
              "True|True|True",
              $"{d35.Reward.Title == QC.Title.GesshosMercy}|"
              + $"{d35.Reward.NextMission == QC.Mission.Toau.GazeOfTheSaboteur}|"
              + $"{d35.Reward.NextLog == XI.Quests.MissionLog.ToAU}");

        // 36 Gaze of the Saboteur
        var d36 = new QC.Missions.Toau.GazeOfTheSaboteur().Compile();
        int id36 = QC.Mission.Toau.GazeOfTheSaboteur;
        ushort hazhalm = QC.Zone.HazhalmTestingGrounds;
        Check("c3 T3 toau 36: Naja 3133; NPC 260 at status 1, 0; zone-in at status 0, 1",
              "Event(3133,3,1,0,0,0,0,0,0,0)|ProgressEvent(7)|null|Event(6)|null",
              Rep(d36, T1Trig, wg, naja, 1, T1F(id36)) + "|" +
              string.Join("|", new[] { 1, 0 }.Select(st =>
                  Rep(d36, T1Trig, hazhalm, QC.Npc.HazhalmTestingGrounds._260, 1, T1F(id36, st)))) + "|" +
              string.Join("|", new[] { 0, 1 }.Select(st => Rep(d36, onZone, hazhalm, null, 1, T1F(id36, st)))));
        Check("c3 T3 toau 36: finish 6 sets status 1; finish 7 drops the eye and completes",
              "Player.SetMissionStatus(4,1)|Player.DelKeyItem(" + QC.KeyItem.PercipientEye + "),Mission.Complete()",
              Fin(d36, hazhalm, 6, T1F(id36)) + "|" + Fin(d36, hazhalm, 7, T1F(id36, 1)));

        // 37 Path of Blood
        var d37 = new QC.Missions.Toau.PathOfBlood().Compile();
        int id37 = QC.Mission.Toau.PathOfBlood;
        Check("c3 T3 toau 37: area 3 plays 3131; zone-in at status 1, 0; update 3131 answers 2",
              "ProgressEvent(3131,0,1,0,0,0,0,0)|Event(3220)|null|Player.UpdateEvent(2,1,0,0,0,0,0,0,0)",
              Rep(d37, area, wg, null, 3, T1F(id37)) + "|" +
              string.Join("|", new[] { 1, 0 }.Select(st => Rep(d37, onZone, wg, null, 1, T1F(id37, st)))) + "|" +
              T1Run(d37, 0, T1Upd, wg, 3131, T1F(id37)));
        Check("c3 T3 toau 37: finish 3131 sets status 1 and moves to the Whitegate",
              "Player.SetMissionStatus(4,1),Player.SetPos(97.936,0,0.109,0," + wg + ")",
              Fin(d37, wg, 3131, T1F(id37)));
        var f37 = T1F(id37, 1);
        f37.UniqueDay = 40;
        var f37b = T1F(id37, 1);
        f37b.UniqueDay = 40;
        f37b.CompleteResult = false;
        Check("c3 T3 toau 37: finish 3220 completes, mustZone and the Timer one day on; not when the completion fails",
              "Mission.Complete(),Player.SetLocalVar(Mission[4][37]mustZone,1),Player.SetCharVar(Mission[4][37]Timer," +
              "41,0)|Mission.Complete()",
              Fin(d37, wg, 3220, f37) + "|" + Fin(d37, wg, 3220, f37b));
        var p37 = T3Patched(new QC.Missions.Toau.PathOfBlood(), QC.MissionKey.Toau.PathOfBlood);
        var g37 = T1F(id37, 1);
        g37.NextJstMidnight = 777;
        var g37b = T1F(id37, 1);
        g37b.NextJstMidnight = 777;
        g37b.CompleteResult = false;
        Check("c3 T3 adjustments toau 37: finish 3220 completes, mustZone and the Timer 1 to JST midnight; " +
              "not when it fails",
              "Mission.Complete(),Player.SetLocalVar(Mission[4][37]mustZone,1),Player.SetCharVar(Mission[4][37]Timer," +
              "1,777)|Mission.Complete()",
              Fin(p37, wg, 3220, g37) + "|" + Fin(p37, wg, 3220, g37b));

        // 38 Stirrings of War: the section holds while mustZone is clear and the day is at the Timer
        var d38 = new QC.Missions.Toau.StirringsOfWar().Compile();
        int id38 = QC.Mission.Toau.StirringsOfWar;
        Fake St(bool mustZone, int timer, int day)
        {
            var f = T1F(id38, 0, mustZone);
            f.MissionVars[$"{ToauLog}/{id38}/Timer"] = timer;
            f.UniqueDay = day;
            return f;
        }
        string Sect(MissionScriptDefinition d, Fake f) =>
            d.States[0].MissionMatches.All(m => m.When == null || m.When(f)).ToString();
        Check("c3 T3 toau 38: the section: clear; mustZone alone; day short alone; day equal",
              "True|False|False|True",
              string.Join("|", new[] { St(false, 5, 9), St(true, 5, 9), St(false, 5, 4), St(false, 5, 5) }
                  .Select(f => Sect(d38, f))));
        Check("c3 T3 toau 38: Naja 3134; area 5 plays 3136; finish completes",
              "Event(3134,3,1,0,0,0,0,0,0,0)|ProgressEvent(3136,{text_table=0})|Mission.Complete()",
              Rep(d38, T1Trig, wg, naja, 1, St(false, 0, 0)) + "|" + Rep(d38, area, wg, null, 5, St(false, 0, 0)) +
              "|" + Fin(d38, wg, 3136, St(false, 0, 0)));
        Check("c3 T3 toau 38: the reward is the Allied Council Summons and the next mission is Allied Rumblings",
              "True|True|True",
              $"{d38.Reward.KeyItem == QC.KeyItem.AlliedCouncilSummons}|"
              + $"{d38.Reward.NextMission == QC.Mission.Toau.AlliedRumblings}|"
              + $"{d38.Reward.NextLog == XI.Quests.MissionLog.ToAU}");
        var p38 = T3Patched(new QC.Missions.Toau.StirringsOfWar(), QC.MissionKey.Toau.StirringsOfWar);
        Check("c3 T3 adjustments toau 38: the section holds while mustZone is clear and the Timer is 0 (a past day " +
              "alone does not)",
              "True|False|False|False",
              string.Join("|", new[] { St(false, 0, 0), St(true, 0, 0), St(false, 5, 9), St(false, 5, 4) }
                  .Select(f => Sect(p38, f))));
    }

    static void RunC3ToauT3Battlefields()
    {
        var pp = new QC.Battlefields.JadeSepulcher.PuppetInPeril();
        var ll = new QC.Battlefields.TalaccaCove.LegacyOfTheLost();
        foreach (var (s, name) in new (BattlefieldScript, string)[] { (pp, "puppet"), (ll, "legacy") })
        {
            var d = (BattlefieldMissionData)s.Data;
            Check($"c3 T3 bf {name}: valid, no hook overridden, and the legacy path",
                  "||" + (name == "puppet" ? "scripts/battlefields/Jade_Sepulcher/puppet_in_peril"
                                           : "scripts/battlefields/Talacca_Cove/legacy_of_the_lost"),
                  $"{string.Join(" | ", BattlefieldScriptValidator.Validate(s))}|" +
                  $"{string.Join(",", BattlefieldScript.HookNames.Where(s.Overrides))}|{s.LegacyPath}");
            Check($"c3 T3 bf {name}: BattlefieldMission fields",
                  name == "puppet" ? "True True False 6 MAX_LEVEL 1800 4 1 ToAU 1 ToAU"
                                   : "True True False 6 MAX_LEVEL 1800 4 1 ToAU 0 ",
                  $"{d.IsMission} {d.AllowTrusts} {d.CanLoseExp} {d.MaxPlayers} {d.LevelCapSetting} " +
                  $"{d.TimeLimit} {d.Index} {string.Join(",", d.AllowedAreas)} {d.MissionArea} " +
                  $"{d.RequiredMissionStatus} {d.MissionStatusArea}");
        }
        string Areas(BattlefieldGroup g) =>
            string.Join(";", g.AreaMobIds.Select(a => string.Join(",", a.Select(m => m.ToString()))));
        var dp = (BattlefieldMissionData)pp.Data;
        var dl = (BattlefieldMissionData)ll.Data;
        Check("c3 T3 bf: the entry and exit NPCs, the mission, the title",
              $"{QC.Npc.JadeSepulcher._1v0} {QC.Npc.JadeSepulcher._1v1},{QC.Npc.JadeSepulcher._1v2}," +
              $"{QC.Npc.JadeSepulcher._1v3} {QC.Mission.Toau.PuppetInPeril} 0|{QC.Npc.TalaccaCove._1l0} " +
              $"{QC.Npc.TalaccaCove._1l1},{QC.Npc.TalaccaCove._1l2},{QC.Npc.TalaccaCove._1l3} " +
              $"{QC.Mission.Toau.LegacyOfTheLost} {QC.Title.GesshosMercy}",
              $"{string.Join(",", dp.EntryNpcs)} {string.Join(",", dp.ExitNpcs)} {dp.Mission} {dp.Title}|" +
              $"{string.Join(",", dl.EntryNpcs)} {string.Join(",", dl.ExitNpcs)} {dl.Mission} {dl.Title}");
        Check("c3 T3 bf: the groups: Gaheel Ja by area; Gessho by area, then its clones (not spawned)",
              "1|LANCELORD_GAHEEL_JA;LANCELORD_GAHEEL_JA+1;LANCELORD_GAHEEL_JA+2|2|GESSHO;GESSHO+7;GESSHO+14|" +
              "GESSHO+1,GESSHO+2,GESSHO+3,GESSHO+4,GESSHO+5,GESSHO+6;GESSHO+8,GESSHO+9,GESSHO+10,GESSHO+11," +
              "GESSHO+12,GESSHO+13;GESSHO+15,GESSHO+16,GESSHO+17,GESSHO+18,GESSHO+19,GESSHO+20",
              $"{dp.Groups.Count}|{Areas(dp.Groups[0])}|{dl.Groups.Count}|{Areas(dl.Groups[0])}|" +
              Areas(dl.Groups[1]));
        Check("c3 T3 bf: the fight groups have an allDeath; Gessho's clones have none and are not spawned",
              "True|True False| False",
              $"{dp.Groups[0].AllDeath != null}|{dl.Groups[0].AllDeath != null} {dl.Groups[1].AllDeath != null}|" +
              $"{dl.Groups[0].Spawned} {dl.Groups[1].Spawned}");
        foreach (var (s, d, name) in new (BattlefieldScript, BattlefieldMissionData, string)[]
                 { (pp, dp, "puppet"), (ll, dl, "legacy") })
        {
            var bf = new BattlefieldViewFake { Id = 1 };
            d.Groups[0].AllDeath(new BattlefieldContextFake(s) { Battlefield = bf }, null);
            Check($"c3 T3 bf {name}: the group's allDeath: the battlefield WON", "Battlefield.SetStatus(2)",
                  string.Join(",", bf.Calls));
        }
    }
}
