// XI.Verify.Quests -- quest rework C3 N5: the four San d'Oria missions (6-1 Leaute's Last Wishes, 6-2 Ranperre's Final
// Rest with its era patch, 7-1 Prestige of the Papsque, 7-2 The Secret Weapon) on C#, one fake check per arm no packet
// shows, as the Lua files had them.
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunC3N5LeautesLastWishes()
    {
        const string L = "leautes last wishes";
        var m = new QC.Missions.Sandoria.LeautesLastWishes();
        var d = m.Compile();
        int s = QC.MissionLog.Sandoria, id = QC.Mission.Sandoria.LeautesLastWishes, none = QC.Mission.Sandoria.None;
        ushort cd = QC.Zone.ChateauDoraguille, wad = QC.Zone.WesternAltepaDesert;
        const int rose = QC.KeyItem.Dreamrose;
        N4Header(L, m, d, QC.MissionKey.Sandoria.LeautesLastWishes, "scripts/missions/sandoria/6_1_Leautes_Last_Wishes",
                 $"//{QC.KeyItem.PieceOfPaper}/600", N5);
        Check($"{N5} {L}: two sections", "2", d.States.Count.ToString());
        N4Section1Open(L, d, MissionLog.Sandoria, id, none, QC.Mission.Sandoria.TheShadowLord, N5);
        N4SandoriaAccept(L, d, id, 16, false, N5);
        N4Current(L, d, 1, MissionLog.Sandoria, id, none, N5);
        Fake F(int status = 0, int progress = 0, bool hasRose = false)
        {
            var f = N4With(N4Fake(MissionLog.Sandoria, id, id, status), hasRose ? rose : 0);
            if (progress != 0) f.MissionVars[$"{s}/{id}/Progress"] = progress;
            return f;
        }
        string R(string npc, Fake f) => C3NReply(C2MHandler(d, 1, N4Trig, cd, npc), f);
        foreach (var (npc, csid) in new[]
                 {
                     (QC.Npc.ChateauDoraguille.Arsha, 85), (QC.Npc.ChateauDoraguille.Chupaile, 86),
                     (QC.Npc.ChateauDoraguille._6h4, 87),
                 })
        {
            Check($"{N5} {L}: {npc} at status 1: {csid} (progress); at 0 and 2: nothing",
                  $"ProgressEvent({csid})|null|null", $"{R(npc, F(1))}|{R(npc, F(0))}|{R(npc, F(2))}");
        }
        string H = QC.Npc.ChateauDoraguille.Halver;
        Check($"{N5} {L}: Halver at status 0: 25; 1: 23; 2: 24; 3: 22 (progress); 4: nothing",
              "ProgressEvent(25)|ProgressEvent(23)|ProgressEvent(24)|ProgressEvent(22)|null",
              string.Join("|", Enumerable.Range(0, 5).Select(st => R(H, F(st)))));
        var area = C2MHandler(d, 1, N5Area, cd, id: 2);
        Check($"{N5} {L}: Chateau trigger area 2 at status 4 with the Dreamrose: 111 (progress); at 3 and 5 with it, " +
              "at 4 without: nothing",
              "ProgressEvent(111)|null|null|null",
              $"{C3NReply(area, F(4, hasRose: true))}|{C3NReply(area, F(3, hasRose: true))}|" +
              $"{C3NReply(area, F(5, hasRose: true))}|{C3NReply(area, F(4))}");
        Check($"{N5} {L}: 22's finish sets status 4; 25's status 1; 87's status 2",
              $"Player.SetMissionStatus({s},4)|Player.SetMissionStatus({s},1)|Player.SetMissionStatus({s},2)",
              $"{C2MRun(C2MHandler(d, 1, N4Fin, cd, id: 22), F(3))}|" +
              $"{C2MRun(C2MHandler(d, 1, N4Fin, cd, id: 25), F(0))}|" +
              $"{C2MRun(C2MHandler(d, 1, N4Fin, cd, id: 87), F(1))}");
        N4Complete(L, "111's finish", C2MHandler(d, 1, N4Fin, cd, id: 111), () => F(4, hasRose: true), rose,
                   tag: N5);
        string sabo = QC.Mob.WesternAltepaDesert.SABOTENDER_ENAMORADO;
        var dr = C2MHandler(d, 1, N4Trig, wad, QC.Npc.WesternAltepaDesert.Dreamrose);
        string ki = $"KeyItem({rose})/Mission.SetVar(Progress,0),Player.SetMissionStatus({s},3)";
        string pop = $"MessageSpecial({QC.Text.WesternAltepaDesert.FEEL_SOMETHING_PRICKLY})/" +
                     $"Player.SpawnMob({sabo},0,True)";
        Check($"{N5} {L}: Dreamrose at status 2, the Sabotender not up: Progress 1: Progress 0, status 3 and the " +
              "Dreamrose (key item); Progress 0 and 2: the Sabotender spawned and claimed, FEEL_SOMETHING_PRICKLY " +
              "(message special); at 1 and 3: nothing",
              $"{ki}|{pop}|{pop}|null/|null/",
              $"{N5RC(dr, F(2, 1))}|{N5RC(dr, F(2))}|{N5RC(dr, F(2, 2))}|{N5RC(dr, F(1, 1))}|{N5RC(dr, F(3, 1))}");
        Check($"{N5} {L}: Dreamrose at status 2: the Sabotender spawned and alive: nothing; spawned and dead, alive " +
              "and not spawned: spawned",
              $"null/|{pop}|{pop}",
              $"{N5RC(dr, N5Mob(F(2), sabo, true, true))}|{N5RC(dr, N5Mob(F(2), sabo, true, false))}|" +
              $"{N5RC(dr, N5Mob(F(2), sabo, false, true))}");
        var death = C2MHandler(d, 1, N5Death, wad, QC.Npc.WesternAltepaDesert.SabotenderEnamorado);
        Check($"{N5} {L}: the Sabotender's death at status 2: Progress 1; at 1 and 3: nothing",
              "Mission.SetVar(Progress,1)||",
              $"{C2MRun(death, F(2))}|{C2MRun(death, F(1))}|{C2MRun(death, F(3))}");
    }

    // a Ranperre's Final Rest fake: status, the Ancient San d'Orian Book, the x position, the char's
    // Mission[0][17]requiredToZone local var, the Timer mission var
    static Fake N5Ranperre(int status, bool book = false, double x = 0, int req = 0, int timer = 0)
    {
        int s = QC.MissionLog.Sandoria, id = QC.Mission.Sandoria.RanperresFinalRest;
        var f = N4With(N4Fake(MissionLog.Sandoria, id, id, status), book ? QC.KeyItem.AncientSanDorianBook : 0);
        f.XPos = x;
        if (req != 0) f.PlayerLocalVars["Mission[0][17]requiredToZone"] = req;
        if (timer != 0) f.MissionVars[$"{s}/{id}/Timer"] = timer;
        f.NextJstMidnight = 777;
        return f;
    }

    static void RunC3N5RanperresFinalRest()
    {
        const string L = "ranperres final rest";
        var m = new QC.Missions.Sandoria.RanperresFinalRest();
        var d = m.Compile();
        int s = QC.MissionLog.Sandoria, id = QC.Mission.Sandoria.RanperresFinalRest, none = QC.Mission.Sandoria.None;
        ushort cd = QC.Zone.ChateauDoraguille, krt = QC.Zone.KingRanperresTomb;
        ushort ssd = QC.Zone.SouthernSanDoria, nsd = QC.Zone.NorthernSanDoria;
        const int book = QC.KeyItem.AncientSanDorianBook;
        N4Header(L, m, d, QC.MissionKey.Sandoria.RanperresFinalRest,
                 "scripts/missions/sandoria/6_2_Ranperres_Final_Rest", "40000/7//", N5);
        Check($"{N5} {L}: two sections", "2", d.States.Count.ToString());
        N4Section1Open(L, d, MissionLog.Sandoria, id, none, QC.Mission.Sandoria.LeautesLastWishes, N5);
        N4SandoriaAccept(L, d, id, 17, false, N5);
        N4Current(L, d, 1, MissionLog.Sandoria, id, none, N5);
        Fake F(int status, bool hasBook = false, double x = 0, int req = 0) => N5Ranperre(status, hasBook, x, req);
        string R(ushort zone, string npc, Fake f) => C3NReply(C2MHandler(d, 1, N4Trig, zone, npc), f);
        string h0 = QC.Npc.ChateauDoraguille._6h0, perf = QC.Npc.ChateauDoraguille.Perfaumand;
        Check($"{N5} {L}: _6h0 at status 0: 81; 5: 21; 7: 79 (progress); 1, 4, 6 and 8: nothing",
              "ProgressEvent(81)|ProgressEvent(21)|ProgressEvent(79)|null|null|null|null",
              string.Join("|", new[] { 0, 5, 7, 1, 4, 6, 8 }.Select(st => R(cd, h0, F(st)))));
        Check($"{N5} {L}: Perfaumand with the book at status 3 and 5: 49; at 2: nothing; at 6 (with or without): " +
              "50; at 7 (with or without): 79 (progress); without the book at 3: nothing",
              "ProgressEvent(49)|ProgressEvent(49)|null|ProgressEvent(50)|ProgressEvent(50)|ProgressEvent(79)|" +
              "ProgressEvent(79)|null",
              string.Join("|", new[]
              {
                  F(3, true), F(5, true), F(2, true), F(6, true), F(6), F(7, true), F(7), F(3),
              }.Select(f => R(cd, perf, f))));
        Check($"{N5} {L}: 21's finish sets status 6; 81's status 1",
              $"Player.SetMissionStatus({s},6)|Player.SetMissionStatus({s},1)",
              $"{C2MRun(C2MHandler(d, 1, N4Fin, cd, id: 21), F(5))}|" +
              $"{C2MRun(C2MHandler(d, 1, N4Fin, cd, id: 81), F(0))}");

        // _5a0
        var door = C2MHandler(d, 1, N4Trig, krt, QC.Npc.KingRanperresTomb._5a0);
        string pop = $"Player.PopFromQm({QC.Mob.KingRanperresTomb.CORRUPTED_YORGOS};" +
                     $"{QC.Mob.KingRanperresTomb.CORRUPTED_SOFFEIL};" +
                     $"{QC.Mob.KingRanperresTomb.CORRUPTED_ULBRIG},0,False)";
        Fake NoPop(Fake f)
        {
            f.PopResult = false;
            return f;
        }
        Check($"{N5} {L}: _5a0 at status 1: popFromQM of Corrupted Yorgos, Soffeil and Ulbrig (hide 0, no claim), " +
              "SENSE_SOMETHING_EVIL (message special); popFromQM false: nothing; at 0, 4, 5 and 7: nothing, no pop",
              $"MessageSpecial({QC.Text.KingRanperresTomb.SENSE_SOMETHING_EVIL})/{pop}|null/{pop}|null/|null/|" +
              "null/|null/",
              string.Join("|", new[] { F(1), NoPop(F(1)), F(0), F(4), F(5), F(7) }.Select(f => N5RC(door, f))));
        Check($"{N5} {L}: _5a0 at status 2 east of x -39.019 (-39.018): 6; at it and west (-39.02): nothing; at " +
              "status 3 east: 6; at it and west (-40): 7; at status 6 (x 0, -40): 5 (progress)",
              "ProgressEvent(6)|null|null|ProgressEvent(6)|ProgressEvent(7)|ProgressEvent(7)|ProgressEvent(5)|" +
              "ProgressEvent(5)",
              string.Join("|", new[]
              {
                  F(2, x: -39.018), F(2, x: -39.019), F(2, x: -39.02), F(3, x: -39.018), F(3, x: -39.019),
                  F(3, x: -40), F(6), F(6, x: -40),
              }.Select(f => C3NReply(door, f))));

        // the Corrupted's deaths
        string Y = QC.Mob.KingRanperresTomb.CORRUPTED_YORGOS, So = QC.Mob.KingRanperresTomb.CORRUPTED_SOFFEIL;
        string U = QC.Mob.KingRanperresTomb.CORRUPTED_ULBRIG;
        string set = $"Player.SetMissionStatus({s},2)";
        foreach (var (npc, a, b) in new[]
                 {
                     (QC.Npc.KingRanperresTomb.CorruptedSoffeil, Y, U),
                     (QC.Npc.KingRanperresTomb.CorruptedUlbrig, Y, So),
                     (QC.Npc.KingRanperresTomb.CorruptedYorgos, U, So),
                 })
        {
            var h = C2MHandler(d, 1, N5Death, krt, npc);
            Check($"{N5} {L}: {npc}'s death at status 1, {a} and {b} not up: status 2; at 0 and 2: nothing; " +
                  $"{a}, then {b}, spawned and alive: nothing; spawned and dead, alive and not spawned: status 2",
                  $"{set}|||||{set}|{set}|{set}|{set}",
                  string.Join("|", new[]
                  {
                      F(1), F(0), F(2), N5Mob(F(1), a, true, true), N5Mob(F(1), b, true, true),
                      N5Mob(F(1), a, true, false), N5Mob(F(1), a, false, true), N5Mob(F(1), b, true, false),
                      N5Mob(F(1), b, false, true),
                  }.Select(f => C2MRun(h, f))));
        }

        string tomb = QC.Npc.KingRanperresTomb.TombstoneLower;
        string rest = $"MessageSpecial({QC.Text.KingRanperresTomb.FINAL_RESTING_PLACE})";
        Check($"{N5} {L}: Tombstone_Lower at status 3 without the book: 8 (progress); with it (status 3, 2): " +
              "FINAL_RESTING_PLACE (message special); without it at 2 and 4: nothing",
              $"ProgressEvent(8)|{rest}|{rest}|null|null",
              $"{R(krt, tomb, F(3))}|{R(krt, tomb, F(3, true))}|{R(krt, tomb, F(2, true))}|{R(krt, tomb, F(2))}|" +
              $"{R(krt, tomb, F(4))}");
        var f6 = C2MHandler(d, 1, N4Fin, krt, id: 6);
        Check($"{N5} {L}: 5's finish sets status 7; 6's at status 2 at or west of x -39.019 (-39.019, -40): status " +
              "3; east (-39.018), at 1 and 3 (x -40): nothing; 8's gives the book and takes the Piece of Paper",
              $"Player.SetMissionStatus({s},7)|Player.SetMissionStatus({s},3)|Player.SetMissionStatus({s},3)||||" +
              $"Player.GiveKeyItem({book}),Player.DelKeyItem({QC.KeyItem.PieceOfPaper})",
              $"{C2MRun(C2MHandler(d, 1, N4Fin, krt, id: 5), F(6))}|{C2MRun(f6, F(2, x: -39.019))}|" +
              $"{C2MRun(f6, F(2, x: -40))}|{C2MRun(f6, F(2, x: -39.018))}|{C2MRun(f6, F(1, x: -40))}|" +
              $"{C2MRun(f6, F(3, x: -40))}|{C2MRun(C2MHandler(d, 1, N4Fin, krt, id: 8), F(3))}");

        // the gate guards
        foreach (var (zone, npc, withBook, req1, req0, seven) in new[]
                 {
                     (ssd, QC.Npc.SouthernSanDoria.Ambrotien, 1036, 1038, 1040, 1034),
                     (ssd, QC.Npc.SouthernSanDoria.Endracion, 1035, 1037, 1039, 1033),
                     (nsd, QC.Npc.NorthernSanDoria.Grilau, 1035, 1037, 1039, 1033),
                 })
        {
            Check($"{N5} {L}: {npc} with the book (status 4, 0): {withBook}; at 4 with requiredToZone 1: {req1}, " +
                  $"with 0 and 2: {req0}; at 7: {seven} (progress); at 3, 5, 6 and 8: nothing",
                  $"ProgressEvent({withBook})|ProgressEvent({withBook})|ProgressEvent({req1})|ProgressEvent({req0})|" +
                  $"ProgressEvent({req0})|ProgressEvent({seven})|null|null|null|null",
                  string.Join("|", new[]
                  {
                      F(4, true), F(0, true), F(4, req: 1), F(4), F(4, req: 2), F(7), F(3), F(5), F(6), F(8),
                  }.Select(f => R(zone, npc, f))));
        }
        string hand = $"Player.DelKeyItem({book}),Player.SetLocalVar(Mission[0][17]requiredToZone,1)," +
                      $"Player.SetMissionStatus({s},4)";
        string five = $"Player.SetMissionStatus({s},5)";
        Check($"{N5} {L}: Southern San d'Oria's 1033 and 1034 finishes complete; 1035 and 1036 take the book, set " +
              "requiredToZone 1 and status 4; 1039 and 1040 set status 5; Northern's 1033, 1035 and 1039 likewise",
              $"Mission.Complete()|Mission.Complete()|{hand}|{hand}|{five}|{five}|Mission.Complete()|{hand}|{five}",
              string.Join("|", new[]
              {
                  (ssd, 1033, 7), (ssd, 1034, 7), (ssd, 1035, 4), (ssd, 1036, 4), (ssd, 1039, 4), (ssd, 1040, 4),
                  (nsd, 1033, 7), (nsd, 1035, 4), (nsd, 1039, 4),
              }.Select(e => C2MRun(C2MHandler(d, 1, N4Fin, e.Item1, id: e.Item2), F(e.Item3, e.Item3 == 4)))));
    }

    // the SOA era patch on 6-2 (modules/era/lua/missions/mission_adjustments.lua): the handover finishes start the
    // JST midnight Timer, the gate guards hold the char at their wait event while it runs
    static void RunC3N5RanperresEraPatch()
    {
        const string L = "ranperres era patch";
        var mod = new QC.Modules.Era.Missions.MissionAdjustments();
        string key = QC.MissionKey.Sandoria.RanperresFinalRest;
        Check($"{N5} {L}: the SOA case patches San d'Oria 6-2 under xi.pre only",
              "False|True",
              $"{mod.Compile(QuestModule.Branches[0].Settings).Keys.Contains(key)}|" +
              $"{mod.Compile(QuestModule.Branches[1].Settings).Keys.Contains(key)}");
        var d0 = new QC.Missions.Sandoria.RanperresFinalRest().Compile();
        var p = d0.WithCore(QuestOverrides.Apply(d0.Core, mod.Compile(QuestModule.Branches[1].Settings), key)
            .Definition);
        int s = QC.MissionLog.Sandoria;
        ushort ssd = QC.Zone.SouthernSanDoria, nsd = QC.Zone.NorthernSanDoria;
        const int book = QC.KeyItem.AncientSanDorianBook;
        string hand = $"Player.DelKeyItem({book}),Player.SetLocalVar(Mission[0][17]requiredToZone,1)," +
                      $"Player.SetMissionStatus({s},4)";
        Fake F(int status, bool hasBook = false, int timer = 0) => N5Ranperre(status, hasBook, timer: timer);
        Check($"{N5} {L}: Southern 1035 and 1036, Northern 1035: the base finish, then the Timer 1 to JST midnight " +
              "(777); Southern 1039 and Northern 1033 untouched",
              $"{hand},Mission.SetVar(Timer,1,777)|{hand},Mission.SetVar(Timer,1,777)|" +
              $"{hand},Mission.SetVar(Timer,1,777)|Player.SetMissionStatus({s},5)|Mission.Complete()",
              string.Join("|", new[]
              {
                  (ssd, 1035, F(4, true)), (ssd, 1036, F(4, true)), (nsd, 1035, F(4, true)), (ssd, 1039, F(4)),
                  (nsd, 1033, F(7)),
              }.Select(e => C2MRun(C2MHandler(p, 1, N4Fin, e.Item1, id: e.Item2), e.Item3))));
        foreach (var (zone, npc, wait, req0, withBook, seven) in new[]
                 {
                     (ssd, QC.Npc.SouthernSanDoria.Ambrotien, 1038, 1040, 1036, 1034),
                     (ssd, QC.Npc.SouthernSanDoria.Endracion, 1037, 1039, 1035, 1033),
                     (nsd, QC.Npc.NorthernSanDoria.Grilau, 1037, 1039, 1035, 1033),
                 })
        {
            var h = C2MHandler(p, 1, N4Trig, zone, npc);
            Check($"{N5} {L}: {npc} at status 4 with the Timer 1 and 2 (and with the book): {wait} (progress); " +
                  $"Timer 0: the base ({req0}); at 3 and 5 with the Timer: the base (nothing; {withBook} with the " +
                  $"book); at 7 with it: the base ({seven})",
                  $"ProgressEvent({wait})|ProgressEvent({wait})|ProgressEvent({wait})|ProgressEvent({req0})|null|" +
                  $"null|ProgressEvent({withBook})|ProgressEvent({seven})",
                  string.Join("|", new[]
                  {
                      F(4, timer: 1), F(4, timer: 2), F(4, true, 1), F(4), F(3, timer: 1), F(5, timer: 1),
                      F(3, true, 1), F(7, timer: 1),
                  }.Select(f => C3NReply(h, f))));
        }
    }

    static void RunC3N5PrestigeOfThePapsque()
    {
        const string L = "prestige of the papsque";
        var m = new QC.Missions.Sandoria.PrestigeOfThePapsque();
        var d = m.Compile();
        int s = QC.MissionLog.Sandoria, id = QC.Mission.Sandoria.PrestigeOfThePapsque, none = QC.Mission.Sandoria.None;
        ushort nsd = QC.Zone.NorthernSanDoria, wr = QC.Zone.WestRonfaure;
        const int tablet = QC.KeyItem.AncientSanDorianTablet;
        N4Header(L, m, d, QC.MissionKey.Sandoria.PrestigeOfThePapsque,
                 "scripts/missions/sandoria/7_1_Prestige_of_the_Papsque", "///1000", N5);
        Check($"{N5} {L}: two sections", "2", d.States.Count.ToString());
        N4Section1Open(L, d, MissionLog.Sandoria, id, none, QC.Mission.Sandoria.RanperresFinalRest, N5);
        N4SandoriaAccept(L, d, id, 18, false, N5);
        N4Current(L, d, 1, MissionLog.Sandoria, id, none, N5);
        Fake F(int status, bool hasTablet = false, int stage = 0)
        {
            var f = N4With(N4Fake(MissionLog.Sandoria, id, id, status), hasTablet ? tablet : 0);
            if (stage != 0) f.PlayerLocalVars["Mission[0][18]Stage"] = stage;
            return f;
        }
        var fc = C2MHandler(d, 1, N4Trig, nsd, QC.Npc.NorthernSanDoria._6fc);
        Check($"{N5} {L}: _6fc at status 0: 7; at 1: 9 (with or without the tablet); else with the tablet (2, 3): 8 " +
              "(progress); at 2 without: nothing",
              "ProgressEvent(7)|ProgressEvent(7)|ProgressEvent(9)|ProgressEvent(9)|ProgressEvent(8)|ProgressEvent(8)|" +
              "null",
              string.Join("|", new[]
              {
                  F(0), F(0, true), F(1), F(1, true), F(2, true), F(3, true), F(2),
              }.Select(f => C3NReply(fc, f))));
        Check($"{N5} {L}: 7's finish sets status 1",
              $"Player.SetMissionStatus({s},1)", C2MRun(C2MHandler(d, 1, N4Fin, nsd, id: 7), F(0)));
        N4Complete(L, "8's finish", C2MHandler(d, 1, N4Fin, nsd, id: 8), () => F(2, true), tablet, tag: N5);
        string dv = QC.Mob.WestRonfaure.MARAUDER_DVOGZOG;
        var qm4 = C2MHandler(d, 1, N4Trig, wr, QC.Npc.WestRonfaure.qm4);
        string pop = $"MessageSpecial({QC.Text.WestRonfaure.SOMETHING_IS_AMISS})/Player.SpawnMob({dv},0,True)";
        Check($"{N5} {L}: qm4 at status 1, the Marauder not spawned: Stage 1: Stage 0, status 2 and the tablet (key " +
              "item); Stage 0 and 2: the Marauder spawned and claimed, SOMETHING_IS_AMISS (message special); at 0 " +
              "and 2: nothing; the Marauder spawned (alive, dead): nothing; alive and not spawned: spawned",
              $"KeyItem({tablet})/Player.SetLocalVar(Mission[0][18]Stage,0),Player.SetMissionStatus({s},2)|" +
              $"{pop}|{pop}|null/|null/|null/|null/|{pop}",
              string.Join("|", new[]
              {
                  F(1, stage: 1), F(1), F(1, stage: 2), F(0, stage: 1), F(2, stage: 1),
                  N5Mob(F(1, stage: 1), dv, true, true), N5Mob(F(1, stage: 1), dv, true, false),
                  N5Mob(F(1), dv, false, true),
              }.Select(f => N5RC(qm4, f))));
        var death = C2MHandler(d, 1, N5Death, wr, QC.Npc.WestRonfaure.MarauderDvogzog);
        Check($"{N5} {L}: the Marauder's death at status 1: Stage 1 (the char's local var); at 0 and 2: nothing",
              "Player.SetLocalVar(Mission[0][18]Stage,1)||",
              $"{C2MRun(death, F(1))}|{C2MRun(death, F(0))}|{C2MRun(death, F(2))}");
    }

    static void RunC3N5TheSecretWeapon()
    {
        const string L = "secret weapon";
        var m = new QC.Missions.Sandoria.TheSecretWeapon();
        var d = m.Compile();
        int s = QC.MissionLog.Sandoria, id = QC.Mission.Sandoria.TheSecretWeapon, none = QC.Mission.Sandoria.None;
        int papsque = QC.Mission.Sandoria.PrestigeOfThePapsque;
        ushort ssd = QC.Zone.SouthernSanDoria, nsd = QC.Zone.NorthernSanDoria, cd = QC.Zone.ChateauDoraguille;
        const int dowser = QC.KeyItem.CrystalDowser;
        N4Header(L, m, d, QC.MissionKey.Sandoria.TheSecretWeapon, "scripts/missions/sandoria/7_2_The_Secret_Weapon",
                 "60000/8//", N5);
        Check($"{N5} {L}: two sections", "2", d.States.Count.ToString());
        Fake S1(int status = 0, int current = -1, int nation = 0, int rank = 7, bool points = true, int done = -1)
        {
            var f = N4Fake(MissionLog.Sandoria, id, current < 0 ? none : current, status, nation, rank: rank,
                           done: done < 0 ? papsque : done);
            if (points) f.RankPointsMet.Add(id);
            return f;
        }
        var s1 = d.States[0];
        Check($"{N5} {L}: section 1 holds with no mission current, a San d'Orian at rank 7 who completed Prestige of " +
              "the Papsque, with the rank points; fails for a Bastokan and a Windurstian (rank 7), with a mission " +
              "current (Prestige, this one), at rank 6 and 8, with Ranperre's Final Rest completed instead, without " +
              "the rank points",
              "True False False False False False False False False",
              string.Join(" ", new[]
              {
                  S1(), S1(nation: 1), S1(nation: 2), S1(current: papsque), S1(current: id), S1(rank: 6), S1(rank: 8),
                  S1(done: QC.Mission.Sandoria.RanperresFinalRest), S1(points: false),
              }.Select(f => s1.IsMissionMatch(f).ToString())));
        string R(int state, ushort zone, string npc, Fake f) => C3NReply(C2MHandler(d, state, N4Trig, zone, npc), f);
        foreach (var (zone, npc, csid) in new[]
                 {
                     (nsd, QC.Npc.NorthernSanDoria.Grilau, 1041), (ssd, QC.Npc.SouthernSanDoria.Ambrotien, 1042),
                     (ssd, QC.Npc.SouthernSanDoria.Endracion, 1041),
                 })
        {
            Check($"{N5} {L}: section 1: {npc} below status 2 (0, 1): {csid} (progress); at 2: nothing",
                  $"ProgressEvent({csid})|ProgressEvent({csid})|null",
                  $"{R(0, zone, npc, S1(0))}|{R(0, zone, npc, S1(1))}|{R(0, zone, npc, S1(2))}");
        }
        Check($"{N5} {L}: section 1: Northern 1041, Southern 1041 and 1042 finishes at status 0: status 1; at 1 and " +
              "2: nothing",
              string.Join("|", Enumerable.Repeat($"Player.SetMissionStatus({s},1)||", 3)),
              string.Join("|", new[] { (nsd, 1041), (ssd, 1041), (ssd, 1042) }.Select(e =>
              {
                  var h = C2MHandler(d, 0, N4Fin, e.Item1, id: e.Item2);
                  return $"{C2MRun(h, S1(0))}|{C2MRun(h, S1(1))}|{C2MRun(h, S1(2))}";
              })));
        N4SandoriaAccept(L, d, id, 19, false, N5);
        var area = C2MHandler(d, 0, N5Area, cd, id: 1);
        Check($"{N5} {L}: section 1: Chateau trigger area 1 at status 1: 0 (progress); at 0 and 2: nothing; 0's " +
              "finish sets status 2",
              $"ProgressEvent(0)|null|null|Player.SetMissionStatus({s},2)",
              $"{C3NReply(area, S1(1))}|{C3NReply(area, S1(0))}|{C3NReply(area, S1(2))}|" +
              $"{C2MRun(C2MHandler(d, 0, N4Fin, cd, id: 0), S1(1))}");
        N4Current(L, d, 1, MissionLog.Sandoria, id, none, N5);
        Fake F(int status, int win = -1) => N4With(N4Fake(MissionLog.Sandoria, id, id, status), win: win);
        var w = C2MHandler(d, 1, N4Fin, QC.Zone.HorlaisPeak, id: 32001);
        int tsw = QC.Battlefield.TheSecretWeapon;
        Check($"{N5} {L}: Horlais Peak 32001 at status 2 with battlefieldWin {tsw}: the Crystal Dowser, status 3; at " +
              $"1 and 3 with the win, at 2 with {tsw - 1}, {tsw + 1} and none: nothing",
              $"Player.GiveKeyItem({dowser}),Player.SetMissionStatus({s},3)|||||",
              string.Join("|", new[]
              {
                  F(2, tsw), F(1, tsw), F(3, tsw), F(2, tsw - 1), F(2, tsw + 1), F(2),
              }.Select(f => C2MRun(w, f))));
        foreach (var (zone, npc, csid) in new[]
                 {
                     (nsd, QC.Npc.NorthernSanDoria.Grilau, 1043), (ssd, QC.Npc.SouthernSanDoria.Ambrotien, 1044),
                     (ssd, QC.Npc.SouthernSanDoria.Endracion, 1043),
                 })
        {
            Check($"{N5} {L}: section 2: {npc} at status 3: {csid} (progress); at 2 and 4: nothing",
                  $"ProgressEvent({csid})|null|null",
                  $"{R(1, zone, npc, F(3))}|{R(1, zone, npc, F(2))}|{R(1, zone, npc, F(4))}");
        }
        foreach (var (zone, csid) in new[] { (nsd, 1043), (ssd, 1043), (ssd, 1044) })
        {
            N4Complete(L, $"zone {zone}'s {csid} finish", C2MHandler(d, 1, N4Fin, zone, id: csid),
                       () => N4With(F(3), dowser), dowser, tag: N5);
        }
    }
}
