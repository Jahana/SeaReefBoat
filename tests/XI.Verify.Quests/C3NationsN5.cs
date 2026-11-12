// XI.Verify.Quests -- quest rework C3 N5 (docs/superpowers/plans/2026-11-07-quest-rework-c3-lanes-1.md, section 4,
// Nations): the rank 6-7 missions (the 6-1s, 6-2s, 7-1s and 7-2s), San d'Oria 6-2's era patch and the three 7-2 / 6-2
// battlefields (Waughroon Shrine's On My Way, Horlais Peak's The Secret Weapon, Balgas Dais' Saintly Invitation) on
// C#, one fake check per arm no packet shows, as the Lua files had them: each section check on its operands, each
// reply on the reads it branches on (with its kind), each finish on its option and the reads it branches on, each
// death on the mobs it reads. This file: the shared helpers and the four Bastok missions.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    internal static void RunC3N5()
    {
        Console.WriteLine("\n== c3 N5: the rank 6-7 missions and their battlefields on C# ==");
        RunC3N5ReturnOfTheTalekeeper();
        RunC3N5ThePiratesCove();
        RunC3N5TheFinalImage();
        RunC3N5OnMyWay();
        RunC3N5LeautesLastWishes();
        RunC3N5RanperresFinalRest();
        RunC3N5RanperresEraPatch();
        RunC3N5PrestigeOfThePapsque();
        RunC3N5TheSecretWeapon();
        RunC3N5FullMoonFountain();
        RunC3N5SaintlyInvitation();
        RunC3N5TheSixthMinistry();
        RunC3N5AwakeningOfTheGods();
        RunC3N5OnMyWayBattle();
        RunC3N5TheSecretWeaponBattle();
        RunC3N5SaintlyInvitationBattle();
    }

    const string N5 = "c3 N5";
    static readonly QuestHandlerKind N5Death = QuestHandlerKind.OnMobDeath;
    static readonly QuestHandlerKind N5Trade = QuestHandlerKind.OnTrade;
    static readonly QuestHandlerKind N5Area = QuestHandlerKind.OnTriggerAreaEnter;

    // a reply and the calls the handler made on the way to it: "<reply>/<calls>"
    static string N5RC(QuestHandlerDecl h, Fake f)
    {
        string r = C3NReply(h, f);
        return $"{r}/{string.Join(",", f.Calls)}";
    }

    // a mob's state on the fake (keyed "<name>+<offset>"): spawned, alive, missing (GetMobByID nil)
    static Fake N5Mob(Fake f, string mob, bool spawned, bool alive, bool missing = false, int offset = 0)
    {
        string k = $"{mob}+{offset}";
        if (spawned) f.SpawnedMobs.Add(k);
        if (alive) f.AliveMobs.Add(k);
        if (missing) f.MissingMobs.Add(k);
        return f;
    }

    // the four Windurst gate guards' finishes (78, 93, 111, 114): option <opt> begins with the zone's
    // YOU_ACCEPT_THE_MISSION; the options either side and 0 do nothing
    static void N5WindurstAccept(string label, MissionScriptDefinition d, int id, int opt)
    {
        foreach (var (zone, csid, text) in new[]
                 {
                     (QC.Zone.PortWindurst, 78, QC.Text.PortWindurst.YOU_ACCEPT_THE_MISSION),
                     (QC.Zone.WindurstWalls, 93, QC.Text.WindurstWalls.YOU_ACCEPT_THE_MISSION),
                     (QC.Zone.WindurstWaters, 111, QC.Text.WindurstWaters.YOU_ACCEPT_THE_MISSION),
                     (QC.Zone.WindurstWoods, 114, QC.Text.WindurstWoods.YOU_ACCEPT_THE_MISSION),
                 })
        {
            var h = C2MHandler(d, 0, N4Fin, zone, id: csid);
            Fake A(int option) => N4Fake(MissionLog.Windurst, id, QC.Mission.Windurst.None, option: option);
            Check($"{N5} {label}: zone {zone}'s {csid} finish: option {opt} begins with its zone's " +
                  $"YOU_ACCEPT_THE_MISSION; options {opt - 1}, {opt + 1} and 0 do nothing",
                  $"Mission.Begin(),Text.Special({text})|||",
                  $"{C2MRun(h, A(opt))}|{C2MRun(h, A(opt - 1))}|{C2MRun(h, A(opt + 1))}|{C2MRun(h, A(0))}");
        }
    }

    // the four Bastok gate guards of section 2 with their zone's EXTENDED_MISSION_OFFSET (and the id a test gives it)
    static (ushort Zone, string Npc, ZoneText Offset, int Id)[] N5BastokGuards() => new[]
    {
        (QC.Zone.BastokMarkets, QC.Npc.BastokMarkets.Cleades, QC.Text.BastokMarkets.EXTENDED_MISSION_OFFSET, 7000),
        (QC.Zone.BastokMines, QC.Npc.BastokMines.Rashid, QC.Text.BastokMines.EXTENDED_MISSION_OFFSET, 7100),
        (QC.Zone.Metalworks, QC.Npc.Metalworks.Malduc, QC.Text.Metalworks.EXTENDED_MISSION_OFFSET, 7200),
        (QC.Zone.PortBastok, QC.Npc.PortBastok.Argus, QC.Text.PortBastok.EXTENDED_MISSION_OFFSET, 7300),
    };

    // section 2's Cleades, Rashid, Malduc and Argus: messageSpecial(<zone>.text.EXTENDED_MISSION_OFFSET + plus)
    static void N5GuardOffsets(string label, MissionScriptDefinition d, Func<Fake> mk, int plus)
    {
        var guards = N5BastokGuards();
        string Say(ushort zone, string npc)
        {
            var f = mk();
            foreach (var g in guards) f.TextIds[g.Offset.ToString()] = g.Id;
            return C3NReply(C2MHandler(d, 1, N4Trig, zone, npc), f);
        }
        Check($"{N5} {label}: section 2: Cleades, Rashid, Malduc and Argus: their zone's EXTENDED_MISSION_OFFSET + " +
              $"{plus} (message special)",
              string.Join("|", guards.Select(g => $"MessageSpecial({g.Id + plus})")),
              string.Join("|", guards.Select(g => Say(g.Zone, g.Npc))));
    }

    static void RunC3N5ReturnOfTheTalekeeper()
    {
        const string L = "return of the talekeeper";
        var m = new QC.Missions.Bastok.ReturnOfTheTalekeeper();
        var d = m.Compile();
        int b = QC.MissionLog.Bastok, id = QC.Mission.Bastok.ReturnOfTheTalekeeper, none = QC.Mission.Bastok.None;
        ushort mines = QC.Zone.BastokMines, wad = QC.Zone.WesternAltepaDesert;
        const int pebble = QC.KeyItem.AltepaMoonpebble;
        N4Header(L, m, d, QC.MissionKey.Bastok.ReturnOfTheTalekeeper,
                 "scripts/missions/bastok/6_1_Return_of_the_Talekeeper", "///650", N5);
        Check($"{N5} {L}: two sections", "2", d.States.Count.ToString());
        N4Section1Open(L, d, MissionLog.Bastok, id, none, QC.Mission.Bastok.XarcabardLandOfTruths, N5);
        N4BastokAccept(L, d, id, 16, tag: N5);
        N4Current(L, d, 1, MissionLog.Bastok, id, none, N5);
        Fake F(int status = 0) => N4Fake(MissionLog.Bastok, id, id, status);
        string R(ushort zone, string npc, Fake f) => C3NReply(C2MHandler(d, 1, N4Trig, zone, npc), f);
        var guards = N5BastokGuards();
        Check($"{N5} {L}: section 2: Cleades, Rashid, Malduc and Argus: their zone's EXTENDED_MISSION_OFFSET " +
              "(message special, fixed)",
              string.Join("|", guards.Select(g => $"MessageSpecial({g.Offset})")),
              string.Join("|", guards.Select(g => R(g.Zone, g.Npc, F(2)))));
        string eagle = QC.Npc.BastokMines.MedicineEagle, tall = QC.Npc.BastokMines.TallMountain;
        Check($"{N5} {L}: Medicine Eagle at status 0: 180 (progress); at 1 and 3: 181 (event)",
              "ProgressEvent(180)|Event(181)|Event(181)",
              $"{R(mines, eagle, F(0))}|{R(mines, eagle, F(1))}|{R(mines, eagle, F(3))}");
        Check($"{N5} {L}: Tall Mountain at status 3: 182 (progress); at 2 and 4: nothing",
              "ProgressEvent(182)|null|null",
              $"{R(mines, tall, F(3))}|{R(mines, tall, F(2))}|{R(mines, tall, F(4))}");
        Check($"{N5} {L}: 180's finish sets status 1",
              $"Player.SetMissionStatus({b},1)", C2MRun(C2MHandler(d, 1, N4Fin, mines, id: 180), F(0)));
        N4Complete(L, "182's finish", C2MHandler(d, 1, N4Fin, mines, id: 182), () => F(3), pebble, tag: N5);
        ushort zm = QC.Zone.ZeruhnMines;
        string drake = QC.Npc.ZeruhnMines.DrakeFang;
        Check($"{N5} {L}: Drake Fang at status 0: nothing; at 1: 200; at 2 and 3 (above 1): 201 (progress); 200's " +
              "finish sets status 2",
              $"null|ProgressEvent(200)|ProgressEvent(201)|ProgressEvent(201)|Player.SetMissionStatus({b},2)",
              $"{R(zm, drake, F(0))}|{R(zm, drake, F(1))}|{R(zm, drake, F(2))}|{R(zm, drake, F(3))}|" +
              $"{C2MRun(C2MHandler(d, 1, N4Fin, zm, id: 200), F(1))}");

        // qm2 and the Sphinxes
        string E = QC.Mob.WesternAltepaDesert.EASTERN_SPHINX, W = QC.Mob.WesternAltepaDesert.WESTERN_SPHINX;
        var qm2 = C2MHandler(d, 1, N4Trig, wad, QC.Npc.WesternAltepaDesert.qm2);
        Fake Q(int status, int nm = 0)
        {
            var f = F(status);
            if (nm != 0) f.MissionLocalVars["nmDefeated"] = nm;
            return f;
        }
        string pop = $"MessageSpecial({QC.Text.WesternAltepaDesert.EVIL_LOOMING_ABOVE_YOU})/" +
                     $"Player.SpawnMob({E},0,True),Player.SpawnMob({W},0,True)";
        string ki = $"KeyItem({pebble})/Player.SetMissionStatus({b},3)";
        Check($"{N5} {L}: qm2 with nmDefeated 1: status 3 and the Altepa Moonpebble (key item), at status 2 and 0; " +
              "nmDefeated 0 and 2 at status 2, neither Sphinx up: both spawned and claimed, EVIL_LOOMING_ABOVE_YOU " +
              "(message special); at 1 and 3: nothing",
              $"{ki}|{ki}|{pop}|{pop}|null/|null/",
              $"{N5RC(qm2, Q(2, 1))}|{N5RC(qm2, Q(0, 1))}|{N5RC(qm2, Q(2))}|{N5RC(qm2, Q(2, 2))}|" +
              $"{N5RC(qm2, Q(1))}|{N5RC(qm2, Q(3))}");
        Check($"{N5} {L}: qm2 at status 2: a Sphinx missing (Eastern, Western) or spawned and alive: nothing; " +
              "spawned and dead, or alive and not spawned: both spawned (Eastern, then Western)",
              $"null/|null/|null/|{pop}|{pop}|null/|{pop}|{pop}",
              string.Join("|", new[]
              {
                  N5RC(qm2, N5Mob(Q(2), E, false, false, true)), N5RC(qm2, N5Mob(Q(2), W, false, false, true)),
                  N5RC(qm2, N5Mob(Q(2), E, true, true)), N5RC(qm2, N5Mob(Q(2), E, true, false)),
                  N5RC(qm2, N5Mob(Q(2), E, false, true)), N5RC(qm2, N5Mob(Q(2), W, true, true)),
                  N5RC(qm2, N5Mob(Q(2), W, true, false)), N5RC(qm2, N5Mob(Q(2), W, false, true)),
              }));
        const string set = "Mission.SetLocalVar(nmDefeated,1)";
        foreach (var (npc, other) in new[]
                 {
                     (QC.Npc.WesternAltepaDesert.EasternSphinx, W), (QC.Npc.WesternAltepaDesert.WesternSphinx, E),
                 })
        {
            var h = C2MHandler(d, 1, N5Death, wad, npc);
            Check($"{N5} {L}: {npc}'s death at status 2, {other} not up: nmDefeated 1; at 1 and 3: nothing; {other} " +
                  "missing or spawned and alive: nothing; spawned and dead, alive and not spawned: nmDefeated 1",
                  $"{set}|||||{set}|{set}",
                  string.Join("|", new[]
                  {
                      C2MRun(h, Q(2)), C2MRun(h, Q(1)), C2MRun(h, Q(3)),
                      C2MRun(h, N5Mob(Q(2), other, false, false, true)), C2MRun(h, N5Mob(Q(2), other, true, true)),
                      C2MRun(h, N5Mob(Q(2), other, true, false)), C2MRun(h, N5Mob(Q(2), other, false, true)),
                  }));
        }
    }

    static void RunC3N5ThePiratesCove()
    {
        const string L = "pirates cove";
        var m = new QC.Missions.Bastok.ThePiratesCove();
        var d = m.Compile();
        int b = QC.MissionLog.Bastok, id = QC.Mission.Bastok.ThePiratesCove, none = QC.Mission.Bastok.None;
        ushort mw = QC.Zone.Metalworks, norg = QC.Zone.Norg, ic = QC.Zone.IfritsCauldron;
        N4Header(L, m, d, QC.MissionKey.Bastok.ThePiratesCove, "scripts/missions/bastok/6_2_The_Pirates_Cove",
                 "40000/7//", N5);
        Check($"{N5} {L}: two sections", "2", d.States.Count.ToString());
        N4Section1Open(L, d, MissionLog.Bastok, id, none, QC.Mission.Bastok.ReturnOfTheTalekeeper, N5);
        N4BastokAccept(L, d, id, 17, tag: N5);
        N4Current(L, d, 1, MissionLog.Bastok, id, none, N5);
        Fake F(int status = 0, bool trade = false)
        {
            var f = N4Fake(MissionLog.Bastok, id, id, status);
            f.TradeResult = trade;
            return f;
        }
        N5GuardOffsets(L, d, () => F(2), 3);
        string R(ushort zone, string npc, Fake f) => C3NReply(C2MHandler(d, 1, N4Trig, zone, npc), f);
        string naji = QC.Npc.Metalworks.Naji;
        Check($"{N5} {L}: Naji at status 0: 761; at 3: 762 (progress); at 1, 2 and 4: nothing",
              "ProgressEvent(761)|ProgressEvent(762)|null|null|null",
              $"{R(mw, naji, F(0))}|{R(mw, naji, F(3))}|{R(mw, naji, F(1))}|{R(mw, naji, F(2))}|{R(mw, naji, F(4))}");
        Check($"{N5} {L}: 761's finish sets status 1; 762's completes",
              $"Player.SetMissionStatus({b},1)|Mission.Complete()",
              $"{C2MRun(C2MHandler(d, 1, N4Fin, mw, id: 761), F(0))}|" +
              $"{C2MRun(C2MHandler(d, 1, N4Fin, mw, id: 762), F(3))}");
        string gil = QC.Npc.Norg.Gilgamesh;
        var gTrade = C2MHandler(d, 1, N5Trade, norg, gil);
        Check($"{N5} {L}: Gilgamesh's trade at status 2 with the Frag Rock: 99 (progress); at 1 and 3 with it, at 2 " +
              "without: nothing",
              "ProgressEvent(99)|null|null|null",
              $"{C3NReply(gTrade, F(2, true))}|{C3NReply(gTrade, F(1, true))}|{C3NReply(gTrade, F(3, true))}|" +
              $"{C3NReply(gTrade, F(2))}");
        Check($"{N5} {L}: Gilgamesh at status 1: 98 (progress); at 0 and 2: nothing",
              "ProgressEvent(98)|null|null",
              $"{R(norg, gil, F(1))}|{R(norg, gil, F(0))}|{R(norg, gil, F(2))}");
        Check($"{N5} {L}: 98's finish sets status 2; 99's takes the trade and sets status 3",
              $"Player.SetMissionStatus({b},2)|Trade.Complete(),Player.SetMissionStatus({b},3)",
              $"{C2MRun(C2MHandler(d, 1, N4Fin, norg, id: 98), F(1))}|" +
              $"{C2MRun(C2MHandler(d, 1, N4Fin, norg, id: 99), F(2))}");
        string nms = QC.Mob.IfritsCauldron.PIRATES_COVE_NMS;
        var qm4 = C2MHandler(d, 1, N5Trade, ic, QC.Npc.IfritsCauldron.qm4);
        string pop = $"Player.PopFromQmSeries({nms},2,900,False,False)";
        Fake NoPop(Fake f)
        {
            f.PopResult = false;
            return f;
        }
        Check($"{N5} {L}: qm4's trade at status 2 with the Adaman Ore: popFromQM of the two NMs (hide 900, no " +
              "claim), the trade taken, the Salamander looks at the player, the Magma claims, " +
              "BAD_FEELING_ABOUT_PLACE (message special); popFromQM false: nothing more; without the ore, at 1 and " +
              "3: not even the pop",
              $"MessageSpecial({QC.Text.IfritsCauldron.BAD_FEELING_ABOUT_PLACE})/{pop},Trade.Complete()," +
              $"Player.MobLookAtPlayer({nms},0),Player.MobUpdateClaim({nms},1)|null/{pop}|null/|null/|null/",
              $"{N5RC(qm4, F(2, true))}|{N5RC(qm4, NoPop(F(2, true)))}|{N5RC(qm4, F(2))}|{N5RC(qm4, F(1, true))}|" +
              $"{N5RC(qm4, F(3, true))}");
    }

    static void RunC3N5TheFinalImage()
    {
        const string L = "final image";
        var m = new QC.Missions.Bastok.TheFinalImage();
        var d = m.Compile();
        int b = QC.MissionLog.Bastok, id = QC.Mission.Bastok.TheFinalImage, none = QC.Mission.Bastok.None;
        ushort mw = QC.Zone.Metalworks, rm = QC.Zone.Romaeve;
        const int cermet = QC.KeyItem.ReinforcedCermet;
        N4Header(L, m, d, QC.MissionKey.Bastok.TheFinalImage, "scripts/missions/bastok/7_1_The_Final_Image",
                 "///700", N5);
        Check($"{N5} {L}: two sections", "2", d.States.Count.ToString());
        N4Section1Open(L, d, MissionLog.Bastok, id, none, QC.Mission.Bastok.ThePiratesCove, N5);
        N4BastokAccept(L, d, id, 18, tag: N5);
        N4Current(L, d, 1, MissionLog.Bastok, id, none, N5);
        Fake F(int status = 0, int nm = 0, bool pop = true)
        {
            var f = N4Fake(MissionLog.Bastok, id, id, status);
            if (nm != 0) f.MissionLocalVars["nmDefeated"] = nm;
            f.PopResult = pop;
            f.Npc = new QuestNpcFake(1, f.Calls);
            return f;
        }
        N5GuardOffsets(L, d, () => F(1), 5);
        string R(ushort zone, string npc, Fake f) => C3NReply(C2MHandler(d, 1, N4Trig, zone, npc), f);
        string cid = QC.Npc.Metalworks.Cid;
        Check($"{N5} {L}: Cid at status 0: 763; at 2: 764 (progress); at 1 and 3: nothing",
              "ProgressEvent(763)|ProgressEvent(764)|null|null",
              $"{R(mw, cid, F(0))}|{R(mw, cid, F(2))}|{R(mw, cid, F(1))}|{R(mw, cid, F(3))}");
        Check($"{N5} {L}: 763's finish sets status 1",
              $"Player.SetMissionStatus({b},1)", C2MRun(C2MHandler(d, 1, N4Fin, mw, id: 763), F(0)));
        N4Complete(L, "764's finish", C2MHandler(d, 1, N4Fin, mw, id: 764), () => F(2), cermet, tag: N5);
        string mok = QC.Mob.Romaeve.MOKKURKALFI;
        var qm2 = C2MHandler(d, 1, N4Trig, rm, QC.Npc.Romaeve.qm2);
        string chill = $"MessageSpecial({QC.Text.Romaeve.A_CHILL_RUNS_DOWN_SPINE})/" +
                       $"Player.PopFromQmSeriesRadius({mok},2,False,True,2),Npc.Hide(0)";
        Check($"{N5} {L}: qm2 at status 1 with nmDefeated 1: status 2 and the Reinforced Cermet (key item); with 0 " +
              "and 2: popFromQM of the two Mokkurkalfi (no claim, look, radius 2), hidden for 0, " +
              "A_CHILL_RUNS_DOWN_SPINE (message special), the same with popFromQM false; at 0 and 2: nothing",
              $"KeyItem({cermet})/Player.SetMissionStatus({b},2)|{chill}|{chill}|{chill}|null/|null/",
              $"{N5RC(qm2, F(1, 1))}|{N5RC(qm2, F(1))}|{N5RC(qm2, F(1, 2))}|{N5RC(qm2, F(1, pop: false))}|" +
              $"{N5RC(qm2, F(0, 1))}|{N5RC(qm2, F(2, 1))}");
        var death = C2MHandler(d, 1, N5Death, rm, QC.Npc.Romaeve.Mokkurkalfi);
        Check($"{N5} {L}: a Mokkurkalfi's death at status 1: nmDefeated 1; at 0 and 2: nothing",
              "Mission.SetLocalVar(nmDefeated,1)||",
              $"{C2MRun(death, F(1))}|{C2MRun(death, F(0))}|{C2MRun(death, F(2))}");
    }

    static void RunC3N5OnMyWay()
    {
        const string L = "on my way";
        var m = new QC.Missions.Bastok.OnMyWay();
        var d = m.Compile();
        int b = QC.MissionLog.Bastok, id = QC.Mission.Bastok.OnMyWay, none = QC.Mission.Bastok.None;
        ushort mines = QC.Zone.BastokMines, mw = QC.Zone.Metalworks, pb = QC.Zone.PortBastok;
        const int letter = QC.KeyItem.LetterFromWerei;
        N4Header(L, m, d, QC.MissionKey.Bastok.OnMyWay, "scripts/missions/bastok/7_2_On_My_Way", "60000/8//", N5);
        Check($"{N5} {L}: four sections", "4", d.States.Count.ToString());
        N4Section1Open(L, d, MissionLog.Bastok, id, none, QC.Mission.Bastok.TheFinalImage, N5);
        N4BastokAccept(L, d, id, 19, tag: N5);
        N4Current(L, d, 1, MissionLog.Bastok, id, none, N5);
        Fake F(int status = 0, int option = 0, int dialog = 0, int stage = 0, bool done = false)
        {
            var f = done
                ? N4Fake(MissionLog.Bastok, id, none, status, done: id)
                : N4Fake(MissionLog.Bastok, id, id, status);
            if (option != 0) f.MissionVars[$"{b}/{id}/Option"] = option;
            if (dialog != 0) f.MissionVars[$"{b}/{id}/GumbahDialog"] = dialog;
            if (stage != 0) f.MissionVars[$"{b}/{id}/Stage"] = stage;
            return f;
        }
        string R(int state, ushort zone, string npc, Fake f) => C3NReply(C2MHandler(d, state, N4Trig, zone, npc), f);
        foreach (var g in N5BastokGuards())
        {
            Check($"{N5} {L}: section 2: {g.Npc} at status 3 with Option 1: 1011 (progress); at 2 and 4 with it, " +
                  "at 3 with Option 0 and 2: nothing (the else arm's messageSpecial is built and dropped)",
                  "ProgressEvent(1011)|null|null|null|null",
                  $"{R(1, g.Zone, g.Npc, F(3, 1))}|{R(1, g.Zone, g.Npc, F(2, 1))}|{R(1, g.Zone, g.Npc, F(4, 1))}|" +
                  $"{R(1, g.Zone, g.Npc, F(3))}|{R(1, g.Zone, g.Npc, F(3, 2))}");
        }
        string gumbah = QC.Npc.BastokMines.Gumbah;
        Check($"{N5} {L}: section 2: Gumbah at status 3 with Option 1 and GumbahDialog 1: 177 (progress); status 2 " +
              "and 4, Option 0 and 2, GumbahDialog 0 and 2: nothing",
              "ProgressEvent(177)|null|null|null|null|null|null",
              string.Join("|", new[]
              {
                  F(3, 1, 1), F(2, 1, 1), F(4, 1, 1), F(3, 0, 1), F(3, 2, 1), F(3, 1, 0), F(3, 1, 2),
              }.Select(f => R(1, mines, gumbah, f))));
        foreach (int state in new[] { 1, 2 })
        {
            Check($"{N5} {L}: section {state + 1}: Bastok Mines' 175 finish sets GumbahDialog 1; 177's sets Option 0 " +
                  "and takes the Letter from Werei",
                  $"Mission.SetVar(GumbahDialog,1)|Mission.SetVar(Option,0),Player.DelKeyItem({letter})",
                  $"{C2MRun(C2MHandler(d, state, N4Fin, mines, id: 175), F(3, 1))}|" +
                  $"{C2MRun(C2MHandler(d, state, N4Fin, mines, id: 177), F(3, 1, 1))}");
        }
        string fz = QC.Npc.Metalworks.Franziska, karst = QC.Npc.Metalworks.Karst, hilda = QC.Npc.PortBastok.Hilda;
        Check($"{N5} {L}: Franziska above status 0: 770 (progress, at 1 and 2); at 0: nothing",
              "ProgressEvent(770)|ProgressEvent(770)|null",
              $"{R(1, mw, fz, F(1))}|{R(1, mw, fz, F(2))}|{R(1, mw, fz, F(0))}");
        Check($"{N5} {L}: Karst at status 0: 765; at 3: 766 with Option as its argument (1, 0, 2) (progress); at 1, " +
              "2 and 4: nothing",
              "ProgressEvent(765)|ProgressEvent(766,1)|ProgressEvent(766,0)|ProgressEvent(766,2)|null|null|null",
              string.Join("|", new[]
              {
                  F(0), F(3, 1), F(3), F(3, 2), F(1, 1), F(2, 1), F(4, 1),
              }.Select(f => R(1, mw, karst, f))));
        Check($"{N5} {L}: Hilda at status 1: 255 (progress); at 0 and 2: nothing",
              "ProgressEvent(255)|null|null",
              $"{R(1, pb, hilda, F(1))}|{R(1, pb, hilda, F(0))}|{R(1, pb, hilda, F(2))}");
        Check($"{N5} {L}: 765's finish sets status 1; 255's status 2",
              $"Player.SetMissionStatus({b},1)|Player.SetMissionStatus({b},2)",
              $"{C2MRun(C2MHandler(d, 1, N4Fin, mw, id: 765), F(0))}|" +
              $"{C2MRun(C2MHandler(d, 1, N4Fin, pb, id: 255), F(1))}");
        var f766 = C2MHandler(d, 1, N4Fin, mw, id: 766);
        var failed = F(3, 1);
        failed.CompleteResult = false;
        Check($"{N5} {L}: 766's finish with Option 1 (read before the complete clears it): completes, Stage 1, " +
              "Option 1 again; with Option 0 and 2: completes, Stage 1; complete false: nothing more",
              "Mission.Complete(),Mission.SetVar(Stage,1),Mission.SetVar(Option,1)|" +
              "Mission.Complete(),Mission.SetVar(Stage,1)|Mission.Complete(),Mission.SetVar(Stage,1)|" +
              "Mission.Complete()",
              $"{C2MRun(f766, F(3, 1))}|{C2MRun(f766, F(3))}|{C2MRun(f766, F(3, 2))}|{C2MRun(f766, failed)}");
        var w = C2MHandler(d, 1, N4Fin, QC.Zone.WaughroonShrine, id: 32001);
        int omw = QC.Battlefield.OnMyWay;
        Fake Win(int status, int win = -1) => N4With(F(status), win: win);
        Check($"{N5} {L}: Waughroon Shrine 32001 at status 2 with battlefieldWin {omw}: the Letter from Werei, " +
              $"status 3, Option 1; at 1 and 3 with the win, at 2 with {omw - 1}, {omw + 1} and none: nothing",
              $"Player.GiveKeyItem({letter}),Player.SetMissionStatus({b},3),Mission.SetVar(Option,1)|||||",
              string.Join("|", new[]
              {
                  Win(2, omw), Win(1, omw), Win(3, omw), Win(2, omw - 1), Win(2, omw + 1), Win(2),
              }.Select(f => C2MRun(w, f))));

        // section 3: completed, Option 1
        var s3 = d.States[2];
        Check($"{N5} {L}: section 3 holds completed with Option 1; fails before completion (Option 1), completed " +
              "with Option 0 and 2",
              "True False False False",
              $"{s3.IsMissionMatch(F(option: 1, done: true))} {s3.IsMissionMatch(F(option: 1))} " +
              $"{s3.IsMissionMatch(F(done: true))} {s3.IsMissionMatch(F(option: 2, done: true))}");
        Fake D(int dialog = 0) => F(0, 1, dialog, done: true);
        Check($"{N5} {L}: section 3: Cleades, Rashid, Malduc, Argus: 1011 (progress, fixed)",
              "ProgressEvent(1011)|ProgressEvent(1011)|ProgressEvent(1011)|ProgressEvent(1011)",
              string.Join("|", N5BastokGuards().Select(g => R(2, g.Zone, g.Npc, D()))));
        Check($"{N5} {L}: section 3: Gumbah with GumbahDialog 1: 177; with 0 and 2: 175 (progress)",
              "ProgressEvent(177)|ProgressEvent(175)|ProgressEvent(175)",
              $"{R(2, mines, gumbah, D(1))}|{R(2, mines, gumbah, D())}|{R(2, mines, gumbah, D(2))}");

        // section 4: completed, Stage 1
        var s4 = d.States[3];
        Check($"{N5} {L}: section 4 holds completed with Stage 1; fails before completion (Stage 1), completed with " +
              "Stage 0 and 2",
              "True False False False",
              $"{s4.IsMissionMatch(F(stage: 1, done: true))} {s4.IsMissionMatch(F(stage: 1))} " +
              $"{s4.IsMissionMatch(F(done: true))} {s4.IsMissionMatch(F(stage: 2, done: true))}");
        string[] quests = { "BEAUTY_AND_THE_GALKA", "FALLEN_COMRADES", "RIVALS" };
        Fake S(params string[] completed)
        {
            var f = F(0, stage: 1, done: true);
            foreach (var q in completed) f.CompletedQuests.Add($"{QuestLog.Bastok}/{q}");
            return f;
        }
        string door = QC.Npc.Metalworks._6lg;
        Check($"{N5} {L}: section 4: _6lg with Beauty and the Galka, Fallen Comrades and Rivals completed: 622; with " +
              "any one missing: 621 (progress); 621's and 622's finishes set Stage 0",
              "ProgressEvent(622)|ProgressEvent(621)|ProgressEvent(621)|ProgressEvent(621)|" +
              "Mission.SetVar(Stage,0)|Mission.SetVar(Stage,0)",
              $"{R(3, mw, door, S(quests))}|{R(3, mw, door, S(quests[1], quests[2]))}|" +
              $"{R(3, mw, door, S(quests[0], quests[2]))}|{R(3, mw, door, S(quests[0], quests[1]))}|" +
              $"{C2MRun(C2MHandler(d, 3, N4Fin, mw, id: 621), S())}|" +
              $"{C2MRun(C2MHandler(d, 3, N4Fin, mw, id: 622), S(quests))}");
    }
}
