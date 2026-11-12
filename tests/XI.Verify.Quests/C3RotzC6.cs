// XI.Verify.Quests -- quest rework C3 C6: Zilart 10 to 17 (The Temple of Desolation to Awakening) and their six fights
// (Ark Angels 1 to 5, The Celestial Nexus) on C#. The data of each against its Lua file's (as it stood at the
// recording), the three facet members C6 added (Player.MobExists, Player.MobLocalVar, the battlefield player's TargId),
// and the arms a trace cannot see: the bounds (1.5 yalms, the respawn time, the shard index), the death handler's
// killer arms, the nil mob, each shard of the five alone, the Awakening bit field and options, and the Celestial
// Nexus' 32004 finish arms and its 30 s timer.
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;
using Z = XI.QuestContent.Missions.Zilart;

internal static partial class Program
{
    internal static void RunC3RotzC6()
    {
        Console.WriteLine("\n== c3 C6: Zilart 10 to 17 and their six fights ==");
        RunC3C6Keys();
        RunC3C6Fixed();
        RunC3C6Facets();
        RunC3C6Missions();
        RunC3C6Mithra();
        RunC3C6ArkAngels();
        RunC3C6Battlefields();
        RunC3C6Nexus();
    }

    static MissionScript[] C3C6All() => new MissionScript[]
    {
        new Z.TheTempleOfDesolation(), new Z.TheHallOfTheGods(), new Z.TheMithraAndTheCrystal(),
        new Z.TheGateOfTheGods(), new Z.ArkAngels(), new Z.TheSealedShrine(), new Z.TheCelestialNexus(),
        new Z.Awakening(),
    };

    static void RunC3C6Keys()
    {
        var all = C3C6All();
        Check("c3 C6: the eight missions' keys and Lua files",
              "mission.zilart.the_temple_of_desolation scripts/missions/rotz/10_The_Temple_of_Desolation|" +
              "mission.zilart.the_hall_of_the_gods scripts/missions/rotz/11_The_Hall_of_the_Gods|" +
              "mission.zilart.the_mithra_and_the_crystal scripts/missions/rotz/12_The_Mithra_and_the_Crystal|" +
              "mission.zilart.the_gate_of_the_gods scripts/missions/rotz/13_The_Gate_of_the_Gods|" +
              "mission.zilart.ark_angels scripts/missions/rotz/14_Ark_Angels|" +
              "mission.zilart.the_sealed_shrine scripts/missions/rotz/15_The_Sealed_Shrine|" +
              "mission.zilart.the_celestial_nexus scripts/missions/rotz/16_The_Celestial_Nexus|" +
              "mission.zilart.awakening scripts/missions/rotz/17_Awakening",
              string.Join("|", all.Select(m => $"{m.Key} {m.LegacyPath}")));
        Check("c3 C6: the missions validate clean", "",
              string.Join(" | ", all.SelectMany(m => QuestScriptValidator.Validate(m.Compile()))));
        Check("c3 C6: the rewards (key item, title, next mission), as the Lua tables have them",
              $",{QC.Title.SealerOfThePortalOfTheGods},22|,,23|,,24|,,26|,,27|,,28|" +
              $",{QC.Title.BurierOfTheIllusion},30|,,31",
              string.Join("|", all.Select(m => m.Compile().Reward).Select(r =>
                  $"{r.KeyItem},{r.Title},{(r.NextLog == MissionLog.Zilart ? r.NextMission : -1)}")));
        Check("c3 C6: the ids the rewards name: The Hall of the Gods 22 to The Last Verse 31",
              "22 23 24 26 27 28 30 31",
              string.Join(" ", new[]
              {
                  QC.Mission.Zilart.TheHallOfTheGods, QC.Mission.Zilart.TheMithraAndTheCrystal,
                  QC.Mission.Zilart.TheGateOfTheGods, QC.Mission.Zilart.ArkAngels, QC.Mission.Zilart.TheSealedShrine,
                  QC.Mission.Zilart.TheCelestialNexus, QC.Mission.Zilart.Awakening, QC.Mission.Zilart.TheLastVerse,
              }));
        Check("c3 C6: the states' shapes (current, status) of each mission",
              "T/|T/|T/,T/0,T/1,T/,T/2,T/,/,/|T/|T/,T/0,T/1|T/|T/|T/,T/",
              string.Join("|", all.Select(m => string.Join(",", m.Compile().States
                  .Select(s => s.MissionMatches.Single()).Select(s =>
                  $"{(s.IsCurrent == true ? "T" : s.IsCurrent == false ? "F" : "")}/{s.Status}")))));
    }

    // Every fixed reply (its state, zone, NPC, kind and modifiers): the mission's reply outranks DefaultActions and an
    // NPC's own script whatever its kind, so a trace tells Event from ProgressEvent or a ReplaceDefault only against
    // another container's reply; these checks pin them
    static void RunC3C6Fixed()
    {
        string Fixed(MissionScript m) => string.Join(" ", m.Compile().States.SelectMany((s, i) =>
            s.Handlers.Where(h => h.Fixed != null).Select(h =>
                $"{i}/{h.Zone}/{h.Entity}={QuestContextFake.Describe(h.Fixed)}")));
        Check("c3 C6: The Temple of Desolation's fixed replies: Aldo 24, _700 5 replacing the default, Gilgamesh 10, " +
              "Kamui 11, _6z0's progress 1",
              "0/245/Aldo=Event(24) 0/252/_700=Event(5):replaceDefault() 0/252/Gilgamesh=Event(10) " +
              "0/252/Kamui=Event(11) 0/251/_6z0=ProgressEvent(1)",
              Fixed(new Z.TheTempleOfDesolation()));
        Check("c3 C6: The Hall of the Gods' fixed replies: Aldo 24, _700's progress 169",
              "0/245/Aldo=Event(24) 0/252/_700=ProgressEvent(169)", Fixed(new Z.TheHallOfTheGods()));
        Check("c3 C6: The Mithra and the Crystal's fixed replies: _700 5 replacing the default, Maryoh 82; the " +
              "papyrus' 83, status 2's 84, the crystal's 4 and 3, the completed 85 (progress events)",
              "0/252/_700=Event(5):replaceDefault() 0/247/Maryoh_Comyujah=Event(82) " +
              "3/247/Maryoh_Comyujah=ProgressEvent(83) 4/247/Maryoh_Comyujah=ProgressEvent(84) " +
              "5/251/_6z0=ProgressEvent(4) 5/251/Shimmering_Circle=ProgressEvent(3) " +
              "7/247/Maryoh_Comyujah=ProgressEvent(85)",
              Fixed(new Z.TheMithraAndTheCrystal()));
        Check("c3 C6: Ark Angels' fixed replies: Gilgamesh 171; status 0's blank_divine_might progress cutscene 53",
              "0/252/Gilgamesh=Event(171) 1/178/blank_divine_might=ProgressCutscene(53,917,1408,1550)",
              Fixed(new Z.ArkAngels()));
        Check("c3 C6: The Sealed Shrine, The Celestial Nexus and Awakening's fixed replies: Gilgamesh 173, 173, 177",
              "0/252/Gilgamesh=Event(173)|0/252/Gilgamesh=Event(173)|0/252/Gilgamesh=Event(177)",
              string.Join("|", new MissionScript[] { new Z.TheSealedShrine(), new Z.TheCelestialNexus(),
                  new Z.Awakening() }.Select(Fixed)));
    }

    static void RunC3C6Facets()
    {
        var f = new Fake();
        f.MobLocalVars["ANCIENT_VESSEL+0:[AncientVessel]Respawn"] = 1234;
        f.MissingMobs.Add("ANCIENT_VESSEL+1");
        Check("c3 C6: Player.MobExists: GetMobByID not nil, unless the test removed the mob; records nothing",
              "True False 0", $"{f.MobExists("ANCIENT_VESSEL")} {f.MobExists("ANCIENT_VESSEL", 1)} {f.Calls.Count}");
        Check("c3 C6: Player.MobLocalVar: the mob's var (0 unset); a nil mob throws, as the Lua's call on nil",
              "1234 0 GetMobByID of ANCIENT_VESSEL is nil",
              $"{f.MobLocalVar("ANCIENT_VESSEL", "[AncientVessel]Respawn")} {f.MobLocalVar("ANCIENT_VESSEL", "x")} " +
              Throws(() => f.MobLocalVar("ANCIENT_VESSEL", "[AncientVessel]Respawn", 1)));
        var p = new BattlefieldPlayerFake { TargId = 1025 };
        Check("c3 C6: the battlefield player's TargId: player:getTargID()", "1025",
              ((IBattlefieldPlayer)p).TargId.ToString());
    }

    // a C6 mission's fake: the mission current (or another id), its status, the option
    static Fake C3C6Fake(int mission, int current, int status = 0, int option = 0)
    {
        var f = new Fake { ThisMission = (MissionLog.Zilart, mission), Option = option };
        f.CurrentMissions[QC.MissionLog.Zilart] = current;
        f.MissionStatuses[QC.MissionLog.Zilart] = status;
        return f;
    }

    static void RunC3C6Missions()
    {
        var fin = QuestHandlerKind.OnEventFinish;
        var trig = QuestHandlerKind.OnTrigger;
        var zin = QuestHandlerKind.OnZoneIn;

        // The Temple of Desolation: _6z0's 1 completes; The Gate of the Gods: the zone-in and 51
        int tod = QC.Mission.Zilart.TheTempleOfDesolation, gog = QC.Mission.Zilart.TheGateOfTheGods;
        var todDef = new Z.TheTempleOfDesolation().Compile();
        var gogDef = new Z.TheGateOfTheGods().Compile();
        Check("c3 C6: The Temple of Desolation's 1 and The Gate of the Gods' 51 complete; the Ru'Aun Gardens " +
              "zone-in plays 51",
              "Mission.Complete()|Mission.Complete()|Event(51)",
              $"{C2MRun(C2MHandler(todDef, 0, fin, QC.Zone.HallOfTheGods, id: 1), C3C6Fake(tod, tod))}|" +
              $"{C2MRun(C2MHandler(gogDef, 0, fin, QC.Zone.RuaunGardens, id: 51), C3C6Fake(gog, gog))}|" +
              $"{C2MReply(C2MHandler(gogDef, 0, zin, QC.Zone.RuaunGardens), C3C6Fake(gog, gog))}");

        // The Hall of the Gods: _6z0's text and no action; 169 on option 0 only
        int hog = QC.Mission.Zilart.TheHallOfTheGods;
        var hogDef = new Z.TheHallOfTheGods().Compile();
        var g = C3C6Fake(hog, hog);
        string clue = C2MReply(C2MHandler(hogDef, 0, trig, QC.Zone.HallOfTheGods, QC.Npc.HallOfTheGods._6z0), g);
        Check("c3 C6: The Hall of the Gods' _6z0: DEPRESSION_A_CLUE (no name, mode 6) and no action",
              $"Text.MessageText({QC.Text.HallOfTheGods.DEPRESSION_A_CLUE},False,6) NoAction()",
              $"{string.Join(",", g.Calls)} {clue}");
        Check("c3 C6: The Hall of the Gods' 169: option 0 completes; 1 and 2 do nothing",
              "Mission.Complete()||",
              string.Join("|", new[] { 0, 1, 2 }.Select(o =>
                  C2MRun(C2MHandler(hogDef, 0, fin, QC.Zone.Norg, id: 169), C3C6Fake(hog, hog, 0, o)))));

        // The Sealed Shrine: _700 at status 0 only, 172's option 0, Aldo's Option arms, the zone-in at status 1
        int ss = QC.Mission.Zilart.TheSealedShrine;
        var ssDef = new Z.TheSealedShrine().Compile();
        Check("c3 C6: The Sealed Shrine's _700: the progress 172 at status 0; nil at 1 and 2",
              "ProgressEvent(172)|null|null",
              string.Join("|", new[] { 0, 1, 2 }.Select(st =>
                  C2MReply(C2MHandler(ssDef, 0, trig, QC.Zone.Norg, QC.Npc.Norg._700), C3C6Fake(ss, ss, st)))));
        Check("c3 C6: The Sealed Shrine's 172: option 0 sets status 1; 1 does nothing",
              $"Player.SetMissionStatus({QC.MissionLog.Zilart},1)|",
              string.Join("|", new[] { 0, 1 }.Select(o =>
                  C2MRun(C2MHandler(ssDef, 0, fin, QC.Zone.Norg, id: 172), C3C6Fake(ss, ss, 0, o)))));
        string Aldo(int option)
        {
            var a = C3C6Fake(ss, ss);
            a.MissionVars[$"{QC.MissionLog.Zilart}/{ss}/Option"] = option;
            return C2MReply(C2MHandler(ssDef, 0, trig, QC.Zone.LowerJeuno, QC.Npc.LowerJeuno.Aldo), a);
        }
        Check("c3 C6: The Sealed Shrine's Aldo: Option 0 the progress 111 (1, 1, 11, 0, 65339390, 899376, 4095, 0); " +
              "else 69 (0, 1, 5, 0, 0, 280, 4095, 4)",
              "ProgressEvent(111,1,1,11,0,65339390,899376,4095,0)|Event(69,0,1,5,0,0,280,4095,4)|" +
              "Event(69,0,1,5,0,0,280,4095,4)",
              $"{Aldo(0)}|{Aldo(1)}|{Aldo(2)}");
        Check("c3 C6: The Sealed Shrine's 111 sets Option 1; 51 completes",
              "Mission.SetVar(Option,1)|Mission.Complete()",
              $"{C2MRun(C2MHandler(ssDef, 0, fin, QC.Zone.LowerJeuno, id: 111), C3C6Fake(ss, ss))}|" +
              $"{C2MRun(C2MHandler(ssDef, 0, fin, QC.Zone.TheShrineOfRuavitau, id: 51), C3C6Fake(ss, ss))}");
        Check("c3 C6: The Sealed Shrine's zone-in to the Shrine of Ru'Avitau: 51 at status 1; nothing at 0 and 2",
              "null|Event(51)|null",
              string.Join("|", new[] { 0, 1, 2 }.Select(st =>
                  C2MReply(C2MHandler(ssDef, 0, zin, QC.Zone.TheShrineOfRuavitau), C3C6Fake(ss, ss, st)))));

        // The Celestial Nexus: 32001 completes on the fight's win only; the set-down always
        int cn = QC.Mission.Zilart.TheCelestialNexus;
        var cnDef = new Z.TheCelestialNexus().Compile();
        string Nexus(int win)
        {
            var n = C3C6Fake(cn, cn);
            n.PlayerLocalVars["battlefieldWin"] = win;
            return C2MRun(C2MHandler(cnDef, 0, fin, QC.Zone.TheCelestialNexus, id: 32001), n);
        }
        const string setDown = "Player.SetPos(0.003,-18.897,137.112,64,251)";
        Check("c3 C6: The Celestial Nexus' 32001: battlefieldWin 320 completes, then the set-down in the Hall of the " +
              "Gods; 319, 321 and 0 only set down",
              $"Mission.Complete(),{setDown}|{setDown}|{setDown}|{setDown}",
              $"{Nexus(320)}|{Nexus(319)}|{Nexus(321)}|{Nexus(0)}");
        Check("c3 C6: the Celestial Nexus fight's id is 320, Ark Angels 1's 288", "320 288",
              $"{QC.Battlefield.CelestialNexus} {QC.Battlefield.ArkAngels1}");

        // Awakening: the bit field and the 232/234 finishes
        int aw = QC.Mission.Zilart.Awakening;
        var awDef = new Z.Awakening().Compile();
        string Bits(Func<int, string> one) => string.Join("|", new[] { 0, 1, 2, 3 }.Select(one));
        Check("c3 C6: Awakening's _6tc: 20 while bit 1 is clear (status 0, 1), nil once set (2, 3)",
              "Event(20)|Event(20)|null|null",
              Bits(st => C2MReply(C2MHandler(awDef, 1, trig, QC.Zone.LowerJeuno, QC.Npc.LowerJeuno._6tc),
                                  C3C6Fake(aw, aw, st))));
        Check("c3 C6: Awakening's Norg zone-in: 176 while bit 0 is clear (status 0, 2), nothing once set (1, 3)",
              "Event(176)|null|Event(176)|null",
              Bits(st => C2MReply(C2MHandler(awDef, 1, zin, QC.Zone.Norg), C3C6Fake(aw, aw, st))));
        int z = QC.MissionLog.Zilart;
        Check("c3 C6: Awakening's 20 adds 2 and 176 adds 1 to the status",
              $"Player.SetMissionStatus({z},3)|Player.SetMissionStatus({z},3)",
              $"{C2MRun(C2MHandler(awDef, 1, fin, QC.Zone.LowerJeuno, id: 20), C3C6Fake(aw, aw, 1))}|" +
              $"{C2MRun(C2MHandler(awDef, 1, fin, QC.Zone.Norg, id: 176), C3C6Fake(aw, aw, 2))}");
        string Finish(int csid, int status, int option) =>
            C2MRun(C2MHandler(awDef, 1, fin, QC.Zone.Norg, id: csid), C3C6Fake(aw, aw, status, option));
        foreach (int csid in new[] { 232, 234 })
        {
            Check($"c3 C6: Awakening's {csid}: options 1 to 4 complete at status 3; 0 and 5 do not; status 2 does not",
                  "|Mission.Complete()|Mission.Complete()|Mission.Complete()|Mission.Complete()||||",
                  $"{Finish(csid, 3, 0)}|{Finish(csid, 3, 1)}|{Finish(csid, 3, 2)}|{Finish(csid, 3, 3)}|" +
                  $"{Finish(csid, 3, 4)}|{Finish(csid, 3, 5)}|{Finish(csid, 2, 1)}|{Finish(csid, 2, 4)}|" +
                  $"{Finish(csid, 4, 1)}");
        }
    }

    static void RunC3C6Mithra()
    {
        var fin = QuestHandlerKind.OnEventFinish;
        var trig = QuestHandlerKind.OnTrigger;
        int mac = QC.Mission.Zilart.TheMithraAndTheCrystal, z = QC.MissionLog.Zilart;
        var d = new Z.TheMithraAndTheCrystal().Compile();
        string V(string name) => $"{z}/{mac}/{name}";

        // section 1: Aldo's Intro, Gilgamesh's crystal
        string Aldo(int intro)
        {
            var f = C3C6Fake(mac, mac);
            f.MissionVars[V("Intro")] = intro;
            return C2MReply(C2MHandler(d, 0, trig, QC.Zone.LowerJeuno, QC.Npc.LowerJeuno.Aldo), f);
        }
        Check("c3 C6 mithra: Aldo: Intro 0 the progress 111, else 69 (0, 1, 2, 0, 0, 0, 4352, 4); 111 sets Intro 1",
              "ProgressEvent(111,0,1,2,0,0,0,4352,4)|Event(69,0,1,2,0,0,0,4352,4)|Event(69,0,1,2,0,0,0,4352,4)|" +
              "Mission.SetVar(Intro,1)",
              $"{Aldo(0)}|{Aldo(1)}|{Aldo(2)}|" +
              C2MRun(C2MHandler(d, 0, fin, QC.Zone.LowerJeuno, id: 111), C3C6Fake(mac, mac)));
        string Gil(bool crystal)
        {
            var f = C3C6Fake(mac, mac);
            f.KeyItems[QC.KeyItem.CeruleanCrystal] = crystal;
            return C2MReply(C2MHandler(d, 0, trig, QC.Zone.Norg, QC.Npc.Norg.Gilgamesh), f);
        }
        Check("c3 C6 mithra: Gilgamesh: 171 with the Cerulean Crystal, else 170", "Event(171)|Event(170)",
              $"{Gil(true)}|{Gil(false)}");

        // section 2: Maryoh's 81 and its finish
        string Maryoh(int option)
        {
            var f = C3C6Fake(mac, mac);
            f.MissionVars[V("Option")] = option;
            return C2MReply(C2MHandler(d, 1, trig, QC.Zone.Rabao, QC.Npc.Rabao.MaryohComyujah), f);
        }
        Check("c3 C6 mithra: Maryoh at status 0: the progress 81 (247, 1, 0, 708, 5, 1, 279, Option)",
              "ProgressEvent(81,247,1,0,708,5,1,279,0)|ProgressEvent(81,247,1,0,708,5,1,279,1)",
              $"{Maryoh(0)}|{Maryoh(1)}");
        Check("c3 C6 mithra: 81: option 1 sets status 1; 0 and 2 set Option 1",
              $"Player.SetMissionStatus({z},1)|Mission.SetVar(Option,1)|Mission.SetVar(Option,1)",
              string.Join("|", new[] { 1, 0, 2 }.Select(o =>
                  C2MRun(C2MHandler(d, 1, fin, QC.Zone.Rabao, id: 81), C3C6Fake(mac, mac, 0, o)))));

        // section 3: qm7, the death, 12 and 13
        string Qm7(int defeated)
        {
            var f = C3C6Fake(mac, mac, 1);
            f.MissionLocalVars["nmDefeated"] = defeated;
            return C2MReply(C2MHandler(d, 2, trig, QC.Zone.QuicksandCaves, QC.Npc.QuicksandCaves.qm7), f);
        }
        Check("c3 C6 mithra: qm7: the progress 13 once nmDefeated is 1, else 12 (0, 2)",
              "ProgressEvent(12)|ProgressEvent(13)|ProgressEvent(12)", $"{Qm7(0)}|{Qm7(1)}|{Qm7(2)}");
        Check("c3 C6 mithra: the states' conditions: section 3 wants no papyrus; 4 the papyrus; 6 the crystal; 7 " +
              "the mission completed or the papyrus; 8 completed",
              "False True|True False|True False|False True True|False True",
              C3C6When(d, mac));
        string Death(bool isKiller, bool noKiller)
        {
            var f = C3C6Fake(mac, mac, 1);
            f.Now = 5000;
            f.Mob = new QuestNpcFake(17, f.Calls);
            f.MobDeath = new QuestMobDeathFake { IsKiller = isKiller, NoKiller = noKiller };
            C2MHandler(d, 2, QuestHandlerKind.OnMobDeath, QC.Zone.QuicksandCaves,
                       QC.Npc.QuicksandCaves.AncientVessel).Run(f);
            return $"{string.Join(",", f.Calls)} {f.MissionLocalVars.GetValueOrDefault("nmDefeated")}";
        }
        Check("c3 C6 mithra: the vessel's death: nmDefeated 1; the respawn time (now + 300) for the killer or a " +
              "death with no killer, not for another member",
              "Mission.SetLocalVar(nmDefeated,1),Npc.SetLocalVar([AncientVessel]Respawn,5300) 1|" +
              "Mission.SetLocalVar(nmDefeated,1),Npc.SetLocalVar([AncientVessel]Respawn,5300) 1|" +
              "Mission.SetLocalVar(nmDefeated,1),Npc.SetLocalVar([AncientVessel]Respawn,5300) 1|" +
              "Mission.SetLocalVar(nmDefeated,1) 1",
              $"{Death(true, false)}|{Death(false, true)}|{Death(true, true)}|{Death(false, false)}");
        string Twelve(int option, double distance = 1, bool missing = false, bool spawned = false, int now = 1000,
                      int respawn = 0)
        {
            var f = C3C6Fake(mac, mac, 1, option);
            f.Npc = new QuestNpcFake(17, f.Calls) { PlayerDistance = distance };
            f.Now = now;
            if (missing) f.MissingMobs.Add("ANCIENT_VESSEL+0");
            if (spawned) f.SpawnedMobs.Add("ANCIENT_VESSEL+0");
            f.MobLocalVars["ANCIENT_VESSEL+0:[AncientVessel]Respawn"] = respawn;
            return C2MRun(C2MHandler(d, 2, fin, QC.Zone.QuicksandCaves, id: 12), f);
        }
        string Msg(string name) => $"Text.MessageText({C3C6Qsc(name)},False,6)";
        Check("c3 C6 mithra: 12: option 0 and 2 do nothing; at 1.5 yalms on, beyond it MUST_MOVE_CLOSER",
              $"||{Msg("SOMETHING_ATTACKING_YOU")},Player.SpawnMob(ANCIENT_VESSEL,0,True)|{Msg("MUST_MOVE_CLOSER")}",
              $"{Twelve(0)}|{Twelve(2)}|{Twelve(1, 1.5)}|{Twelve(1, 1.5001)}");
        Check("c3 C6 mithra: 12 on option 1: no vessel (GetMobByID nil) nothing; spawned THIS_IS_NOT_THE_TIME; " +
              "before the respawn time FAINT_TRACES_OF_MAGIC; at it and after SOMETHING_ATTACKING_YOU and the spawn",
              $"|{Msg("THIS_IS_NOT_THE_TIME")}|{Msg("FAINT_TRACES_OF_MAGIC")}|" +
              $"{Msg("SOMETHING_ATTACKING_YOU")},Player.SpawnMob(ANCIENT_VESSEL,0,True)|" +
              $"{Msg("SOMETHING_ATTACKING_YOU")},Player.SpawnMob(ANCIENT_VESSEL,0,True)",
              $"{Twelve(1, missing: true)}|{Twelve(1, spawned: true, respawn: 2000)}|" +
              $"{Twelve(1, now: 1999, respawn: 2000)}|{Twelve(1, now: 2000, respawn: 2000)}|" +
              $"{Twelve(1, now: 2001, respawn: 2000)}");
        string Thirteen(int option, double distance = 1)
        {
            var f = C3C6Fake(mac, mac, 1, option);
            f.Npc = new QuestNpcFake(17, f.Calls) { PlayerDistance = distance };
            return C2MRun(C2MHandler(d, 2, fin, QC.Zone.QuicksandCaves, id: 13), f);
        }
        Check("c3 C6 mithra: 13: options 0 and 2 nothing; 1 within 1.5 the Scrap of Papyrus, beyond MUST_MOVE_CLOSER",
              $"||Player.GiveKeyItem({QC.KeyItem.ScrapOfPapyrus})|{Msg("MUST_MOVE_CLOSER")}",
              $"{Thirteen(0)}|{Thirteen(2)}|{Thirteen(1, 1.5)}|{Thirteen(1, 1.5001)}");
        Check("c3 C6 mithra: 83 takes the papyrus, gives the Cerulean Crystal and sets status 2; 3 completes",
              $"Player.DelKeyItem({QC.KeyItem.ScrapOfPapyrus}),Player.GiveKeyItem({QC.KeyItem.CeruleanCrystal})," +
              $"Player.SetMissionStatus({z},2)|Mission.Complete()",
              $"{C2MRun(C2MHandler(d, 3, fin, QC.Zone.Rabao, id: 83), C3C6Fake(mac, mac))}|" +
              C2MRun(C2MHandler(d, 5, fin, QC.Zone.HallOfTheGods, id: 3), C3C6Fake(mac, mac)));
        var y = C3C6Fake(mac, mac + 1);
        string nothing = C2MReply(C2MHandler(d, 6, trig, QC.Zone.QuicksandCaves, QC.Npc.QuicksandCaves.qm7), y);
        Check("c3 C6 mithra: section 7's qm7: YOU_FIND_NOTHING and no action",
              $"{Msg("YOU_FIND_NOTHING")} NoAction()", $"{string.Join(",", y.Calls)} {nothing}");
    }

    // a Quicksand Caves text by name, as the fake records it
    static string C3C6Qsc(string name) => name switch
    {
        "MUST_MOVE_CLOSER" => QC.Text.QuicksandCaves.MUST_MOVE_CLOSER.ToString(),
        "THIS_IS_NOT_THE_TIME" => QC.Text.QuicksandCaves.THIS_IS_NOT_THE_TIME.ToString(),
        "FAINT_TRACES_OF_MAGIC" => QC.Text.QuicksandCaves.FAINT_TRACES_OF_MAGIC.ToString(),
        "SOMETHING_ATTACKING_YOU" => QC.Text.QuicksandCaves.SOMETHING_ATTACKING_YOU.ToString(),
        "YOU_FIND_NOTHING" => QC.Text.QuicksandCaves.YOU_FIND_NOTHING.ToString(),
        _ => throw new ArgumentException(name),
    };

    // each state's when over five chars: (papyrus, crystal, completed) = none, papyrus, crystal, completed, both
    static string C3C6When(MissionScriptDefinition d, int mac)
    {
        bool When(int state, bool papyrus, bool crystal, bool completed)
        {
            var f = C3C6Fake(mac, mac, 1);
            f.KeyItems[QC.KeyItem.ScrapOfPapyrus] = papyrus;
            f.KeyItems[QC.KeyItem.CeruleanCrystal] = crystal;
            if (completed) f.CompletedMissions.Add($"{QC.MissionLog.Zilart}/{mac}");
            var match = d.States[state].MissionMatches.Single();
            return match.When == null || match.When(f);
        }
        return $"{When(2, true, false, false)} {When(2, false, false, false)}|" +
               $"{When(3, true, false, false)} {When(3, false, false, false)}|" +
               $"{When(5, false, true, false)} {When(5, false, false, false)}|" +
               $"{When(6, false, false, false)} {When(6, true, false, false)} {When(6, false, false, true)}|" +
               $"{When(7, false, false, false)} {When(7, false, false, true)}";
    }

    static void RunC3C6ArkAngels()
    {
        int aa = QC.Mission.Zilart.ArkAngels, z = QC.MissionLog.Zilart;
        var d = new Z.ArkAngels().Compile();
        int[] shards =
        {
            QC.KeyItem.ShardOfApathy, QC.KeyItem.ShardOfArrogance, QC.KeyItem.ShardOfCowardice,
            QC.KeyItem.ShardOfEnvy, QC.KeyItem.ShardOfRage,
        };
        Check("c3 C6 ark angels: the shards are 455 to 459 (Apathy, Arrogance, Cowardice, Envy, Rage)",
              "455 456 457 458 459", string.Join(" ", shards));
        string Win(int win, IEnumerable<int> held, QuestStatus divine = QuestStatus.Available)
        {
            var f = C3C6Fake(aa, aa, 1);
            f.PlayerLocalVars["battlefieldWin"] = win;
            foreach (int k in held) f.KeyItems[k] = true;
            f.QuestStatuses["Outlands/DIVINE_MIGHT"] = divine;
            return C2MRun(C2MHandler(d, 2, QuestHandlerKind.OnEventFinish, QC.Zone.LaloffAmphitheater, id: 32001), f);
        }
        string Give(int k) => $"Player.GiveKeyItem({k})";
        Check("c3 C6 ark angels: 32001 for each single fight (288 to 292): Apathy, Cowardice, Envy, Arrogance, Rage",
              string.Join("|", new[] { 455, 457, 458, 456, 459 }.Select(Give)),
              string.Join("|", Enumerable.Range(288, 5).Select(w => Win(w, new int[0]))));
        Check("c3 C6 ark angels: 32001 outside the six (287, 294, 0): no shard",
              "|||", $"{Win(287, new int[0])}|{Win(294, new int[0])}|{Win(0, new int[0])}|");
        string all5 = string.Join(",", Enumerable.Range(455, 5).Select(Give));
        Check("c3 C6 ark angels: 32001 for Divine Might (293): the five shards, then all held completes; " +
              "DivineMight 2 only with the quest accepted",
              $"{all5},Mission.Complete()|{all5},Player.SetCharVar(DivineMight,2,0),Mission.Complete()|" +
              $"{all5},Mission.Complete()",
              $"{Win(293, new int[0])}|{Win(293, new int[0], QuestStatus.Accepted)}|" +
              $"{Win(293, new int[0], QuestStatus.Completed)}");
        Check("c3 C6 ark angels: all five held completes (index 6, no shard given); each one missing alone does not",
              "Mission.Complete()|||||",
              $"{Win(294, shards)}|" + string.Join("|", shards.Select(miss =>
                  Win(294, shards.Where(k => k != miss)))));
        Check("c3 C6 ark angels: the shard given counts for the five (Rage given, the four held: completes)",
              $"{Give(459)},Mission.Complete()",
              Win(292, shards.Where(k => k != QC.KeyItem.ShardOfRage)));
        Check("c3 C6 ark angels: 53 sets status 1",
              $"Player.SetMissionStatus({z},1)",
              C2MRun(C2MHandler(d, 1, QuestHandlerKind.OnEventFinish, QC.Zone.TheShrineOfRuavitau, id: 53),
                     C3C6Fake(aa, aa)));
    }

    static void RunC3C6Battlefields()
    {
        BattlefieldScript[] all =
        {
            new QC.Battlefields.LaloffAmphitheater.ArkAngels1(), new QC.Battlefields.LaloffAmphitheater.ArkAngels2(),
            new QC.Battlefields.LaloffAmphitheater.ArkAngels3(), new QC.Battlefields.LaloffAmphitheater.ArkAngels4(),
            new QC.Battlefields.LaloffAmphitheater.ArkAngels5(), new QC.Battlefields.TheCelestialNexus.CelestialNexus(),
        };
        Check("c3 C6: the six fights validate clean, their keys and Lua files",
              string.Join("|", Enumerable.Range(1, 5).Select(n =>
                  $"|battlefield.ark_angels_{n} scripts/battlefields/LaLoff_Amphitheater/ark_angels_{n}")) +
              "||battlefield.celestial_nexus scripts/battlefields/The_Celestial_Nexus/celestial_nexus",
              string.Join("|", all.Select(s =>
                  $"{string.Join(" | ", BattlefieldScriptValidator.Validate(s))}|{s.Key} {s.LegacyPath}")));
        Check("c3 C6: the Lua files' fields (zone, id, no exp loss, mission, trusts, players, MAX_LEVEL, time, " +
              "index, entry and exit NPCs, mission, status area and value, title, groups)",
              "180 288 False True True 6 MAX_LEVEL 1800 0 qm1_1 qm2 3/26 3 1 0 1|" +
              "180 289 False True True 6 MAX_LEVEL 1800 1 qm1_2 qm2 3/26 3 1 0 1|" +
              "180 290 False True True 6 MAX_LEVEL 1800 2 qm1_3 qm2 3/26 3 1 0 3|" +
              "180 291 False True True 6 MAX_LEVEL 1800 3 qm1_4 qm2 3/26 3 1 0 1|" +
              "180 292 False True True 6 MAX_LEVEL 1800 4 qm1_5 qm2 3/26 3 1 0 2|" +
              "181 320 False True True 6 MAX_LEVEL 1800 0 _513 _514,_515 3/28 3 0 0 4",
              string.Join("|", all.Select(s => (BattlefieldMissionData)s.Data).Select(d =>
                  $"{d.ZoneId} {d.BattlefieldId} {d.CanLoseExp} {d.IsMission} {d.AllowTrusts} {d.MaxPlayers} " +
                  $"{d.LevelCapSetting} {d.TimeLimit} {d.Index} {string.Join(",", d.EntryNpcs)} " +
                  $"{string.Join(",", d.ExitNpcs)} {(int)d.MissionArea}/{d.Mission} {(int?)d.MissionStatusArea} " +
                  $"{d.RequiredMissionStatus} {d.Title} {d.Groups.Count}")));
        string Groups(BattlefieldScript s) => string.Join("|", s.Data.Groups.Select(g =>
            string.Join(";", g.AreaMobIds.Select(a => a[0].Key + ":" + string.Join(",", a.Select(m => m.Offset)))) +
            $" spawned={g.Spawned} allDeath={g.AllDeath != null} death={g.Death != null}"));
        Check("c3 C6: the Ark Angels' groups: the Ark Angel, one to an area, wins on all-death; Ark Angel MR's two " +
              "and Ark Angel GK's one pet group (+3.., +6..) wait (spawned false)",
              "ARK_ANGEL_HM:0;ARK_ANGEL_HM:1;ARK_ANGEL_HM:2 spawned= allDeath=True death=False#" +
              "ARK_ANGEL_TT:0;ARK_ANGEL_TT:1;ARK_ANGEL_TT:2 spawned= allDeath=True death=False#" +
              "ARK_ANGEL_MR:0;ARK_ANGEL_MR:1;ARK_ANGEL_MR:2 spawned= allDeath=True death=False|" +
              "ARK_ANGEL_MR:3;ARK_ANGEL_MR:4;ARK_ANGEL_MR:5 spawned=False allDeath=False death=False|" +
              "ARK_ANGEL_MR:6;ARK_ANGEL_MR:7;ARK_ANGEL_MR:8 spawned=False allDeath=False death=False#" +
              "ARK_ANGEL_EV:0;ARK_ANGEL_EV:1;ARK_ANGEL_EV:2 spawned= allDeath=True death=False#" +
              "ARK_ANGEL_GK:0;ARK_ANGEL_GK:1;ARK_ANGEL_GK:2 spawned= allDeath=True death=False|" +
              "ARK_ANGEL_GK:3;ARK_ANGEL_GK:4;ARK_ANGEL_GK:5 spawned=False allDeath=False death=False",
              string.Join("#", all.Take(5).Select(Groups)));
        Check("c3 C6: the Celestial Nexus' groups: phase 1 (+0, 5, 10) on death; the Exoplates (+1, 6, 11); the " +
              "Orbitals (+3 and 4, 8 and 9, 13 and 14) wait; phase 2 (+2, 7, 12) waits, on death",
              "EALDNARCHE:0;EALDNARCHE:5;EALDNARCHE:10 spawned= allDeath=False death=True|" +
              "EALDNARCHE:1;EALDNARCHE:6;EALDNARCHE:11 spawned= allDeath=False death=False|" +
              "EALDNARCHE:3,4;EALDNARCHE:8,9;EALDNARCHE:13,14 spawned=False allDeath=False death=False|" +
              "EALDNARCHE:2;EALDNARCHE:7;EALDNARCHE:12 spawned=False allDeath=False death=True",
              Groups(all[5]));
        Check("c3 C6: the hooks: the Ark Angels override entryRequirement, onBattlefieldLoss and onExitTrigger; " +
              "the Celestial Nexus onEventFinishBattlefield",
              string.Join("|", Enumerable.Repeat("EntryRequirement,OnBattlefieldLoss,OnExitTrigger", 5)) +
              "|OnEventFinishBattlefield",
              string.Join("|", all.Select(s => string.Join(",", BattlefieldScript.HookNames.Where(s.Overrides)))));

        int[] shard =
        {
            QC.KeyItem.ShardOfApathy, QC.KeyItem.ShardOfCowardice, QC.KeyItem.ShardOfEnvy,
            QC.KeyItem.ShardOfArrogance, QC.KeyItem.ShardOfRage,
        };
        Check("c3 C6: each Ark Angels' entryRequirement: its own shard not held (held: false; another held: true)",
              string.Join("|", Enumerable.Repeat("True False True", 5)),
              string.Join("|", all.Take(5).Select((s, i) =>
              {
                  bool Req(int? held)
                  {
                      var (q, p) = C2BContext(s, "qm1_" + (i + 1));
                      if (held is int k) p.KeyItems[k] = true;
                      return s.EntryRequirement(q, true);
                  }
                  return $"{Req(null)} {Req(shard[i])} {Req(shard[(i + 1) % 5])}";
              })));
        Check("c3 C6: each Ark Angels' loss: 32002 (0, 0, 0, 0, 0, the battlefield's id - 288, 180)",
              string.Join("|", Enumerable.Range(0, 5).Select(i => $"Player.StartEvent(32002,0,0,0,0,0,{i},180)")) +
              "|Player.StartEvent(32002,0,0,0,0,0,5,180)",
              string.Join("|", all.Take(5).Select((s, i) => (s, i)).Append((all[0], 5)).Select(t =>
              {
                  var (q, p) = C2BContext(t.Item1, "qm2");
                  q.Battlefield = new BattlefieldViewFake { Id = 288 + t.Item2 };
                  t.Item1.OnBattlefieldLoss(q);
                  return string.Join(",", p.Calls);
              })));
        string Exit(BattlefieldScript s, int? id)
        {
            var (q, p) = C2BContext(s, "qm2");
            if (id is int bf) p.CurrentBattlefield = new BattlefieldViewFake { Id = bf };
            var r = s.OnExitTrigger(q) as BattlefieldActionFake;
            return r == null ? "null" : r.ToString();
        }
        Check("c3 C6: each Ark Angels' qm2: no battlefield nil; in one the event 32003 (0, 0, 0, 0, its id - 288, " +
              "180), the char's battlefield's id read (Divine Might's 293: 5)",
              string.Join("|", Enumerable.Range(0, 5).Select(i => $"null event(32003,0,0,0,0,{i},180)")) +
              "|event(32003,0,0,0,0,5,180)",
              string.Join("|", all.Take(5).Select((s, i) => $"{Exit(s, null)} {Exit(s, 288 + i)}")) + "|" +
              Exit(all[2], 293));
    }

    static void RunC3C6Nexus()
    {
        var s = new QC.Battlefields.TheCelestialNexus.CelestialNexus();
        const int eald = 17518592;
        BattlefieldEngineFake engine = null;
        BattlefieldCallbacksFake queues = null;
        BattlefieldMobFake Mob(int id, bool spawned)
        {
            var m = new BattlefieldMobFake { Id = id, IsSpawned = spawned };
            engine.MobsById[id] = m;
            return m;
        }
        (BattlefieldContextFake Q, BattlefieldPlayerFake P) World(int area)
        {
            engine = new BattlefieldEngineFake();
            engine.ZoneMobIds[$"{QC.Zone.TheCelestialNexus}:EALDNARCHE"] = eald;
            queues = new BattlefieldCallbacksFake(s, engine);
            engine.Callbacks = queues;
            var (q, p) = C2BContext(s, "_513");
            q.Engine = engine;
            p.TargId = 1025;
            p.CurrentBattlefield = new BattlefieldViewFake { Id = 320, Area = area };
            return (q, p);
        }

        // group 1's death: the Orbitals despawned, 32004 (area) to each player
        {
            var (q, p) = World(2);
            var other = new BattlefieldPlayerFake();
            var view = new BattlefieldViewFake { Id = 320, Area = 2 };
            view.PlayerList.Add(p);
            view.PlayerList.Add(other);
            q.Battlefield = view;
            var mob = Mob(eald + 5, true);
            s.Data.Groups[0].Death(q, mob, 1);
            Check("c3 C6 nexus: phase 1's death despawns his id + 3 and + 4 and plays 32004 (the area) to each player",
                  $"DespawnMob({eald + 8}),DespawnMob({eald + 9})|" +
                  "Player.StartEvent(32004,2)|Player.StartEvent(32004,2)",
                  $"{string.Join(",", engine.Calls)}|{string.Join(",", p.Calls)}|{string.Join(",", other.Calls)}");
            var won = new BattlefieldViewFake { Id = 320 };
            q.Battlefield = won;
            s.Data.Groups[3].Death(q, Mob(eald + 7, true), 1);
            Check("c3 C6 nexus: phase 2's death wins", $"Battlefield.SetStatus({BattlefieldConst.StatusWon})",
                  string.Join(",", won.Calls));
        }

        // the 32004 finish's arms
        foreach (int area in new[] { 1, 2, 3 })
        {
            var (q, p) = World(area);
            int one = eald + (area - 1) * 5, two = one + 2;
            var phaseTwo = Mob(two, false);
            Mob(one, true);
            s.OnEventFinishBattlefield(q, 32004, 0);
            Check($"c3 C6 nexus: 32004 in area {area}: phase 1 ({one}) despawned, phase 2 ({two}) spawned, its " +
                  "targetId the char's targid, a 30000 ms timer (engage) armed on it",
                  $"DespawnMob({one}),SpawnMob({two})|Mob.SetLocalVar(targetId,1025),Mob.Timer(30000,engage)|1",
                  $"{string.Join(",", engine.Calls)}|{string.Join(",", phaseTwo.Calls)}|{queues.Pending.Count}");
            if (area != 1) continue;
            queues.Tick(30000);
            Check("c3 C6 nexus: the timer: not at 30000 ms (strictly past), then phase 2 engages its targetId",
                  "False|True Mob.Engage(1025)",
                  $"{phaseTwo.Calls.Contains("Mob.Engage(1025)")}|" +
                  $"{queues.Tick(30001) == 1} {phaseTwo.Calls.Last()}");
        }
        {
            var (q, p) = World(1);
            var up = Mob(eald + 2, true);
            Mob(eald, true);
            s.OnEventFinishBattlefield(q, 32004, 0);
            string spawned = $"{string.Join(",", engine.Calls)}|{string.Join(",", up.Calls)}|{queues.Pending.Count}";
            var (q2, _) = World(1);
            s.OnEventFinishBattlefield(q2, 32004, 0);
            string missing = $"{string.Join(",", engine.Calls)}|{queues.Pending.Count}";
            var (q3, _) = World(1);
            Mob(eald + 2, false);
            Mob(eald, true);
            s.OnEventFinishBattlefield(q3, 32005, 0);
            s.OnEventFinishBattlefield(q3, 32001, 0);
            string other = $"{string.Join(",", engine.Calls)}|{queues.Pending.Count}";
            Check("c3 C6 nexus: 32004 with phase 2 up does nothing; with no phase 2 mob (GetMobByID nil) nothing; " +
                  "another csid nothing",
                  "||0||0||0", $"{spawned}|{missing}|{other}");
        }
    }
}
