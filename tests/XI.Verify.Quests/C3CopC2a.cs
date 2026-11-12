// XI.Verify.Quests -- quest rework C3 C2a: CoP 3-1 to 3-5 and the Darkness Named battlefield. Each handler's arms one
// at a time (the traces hold the packets of the recorded flows), the values no trace sees (the gil, the jewel timer's
// messages, the arena boxes, the respawn clock) and the data of the battlefield against the Lua file's (as it stood at
// the recording).
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    internal static void RunC3CopC2a()
    {
        Console.WriteLine("\n== c3 C2a: CoP 3-1 to 3-5 and Darkness Named ==");
        RunC3C2aKeys();
        RunC3C2aShort();
        RunC3C2aDarknessNamed();
        RunC3C2aRoadForks();
        RunC3C2aBattlefield();
    }

    const int C2aCop = 6;

    // the reply of a handler on a fake as text ("null" for none)
    static string C2aReply(MissionScriptDefinition d, int state, QuestHandlerKind kind, ushort zone, string entity,
                           Fake f, int id = 1) =>
        C2MReply(C2MHandler(d, state, kind, zone, entity, id), f);

    static Fake C2aFake(int mission, int status = 0, int nation = 0)
    {
        var f = new Fake { ThisMission = (MissionLog.CoP, mission), NationId = nation };
        f.CurrentMissions[C2aCop] = mission;
        f.MissionVars[$"{C2aCop}/{mission}/Status"] = status;
        return f;
    }

    static void RunC3C2aKeys()
    {
        MissionScript[] all =
        {
            new QC.Missions.Cop.TheCallOfTheWyrmking(), new QC.Missions.Cop.AVesselWithoutACaptain(),
            new QC.Missions.Cop.TheRoadForks(), new QC.Missions.Cop.TendingAgedWounds(),
            new QC.Missions.Cop.DarknessNamed(),
        };
        Check("c3 C2a: the five missions' keys and Lua files",
              "mission.cop.the_call_of_the_wyrmking scripts/missions/cop/3_1_The_Call_of_the_Wyrmking|" +
              "mission.cop.a_vessel_without_a_captain scripts/missions/cop/3_2_A_Vessel_Without_a_Captain|" +
              "mission.cop.the_road_forks scripts/missions/cop/3_3_The_Road_Forks|" +
              "mission.cop.tending_aged_wounds scripts/missions/cop/3_4_Tending_Aged_Wounds|" +
              "mission.cop.darkness_named scripts/missions/cop/3_5_Darkness_Named",
              string.Join("|", all.Select(m => $"{m.Key} {m.LegacyPath}")));
        Check("c3 C2a: the five missions validate clean", "",
              string.Join(" | ", all.SelectMany(m => QuestScriptValidator.Validate(m.Compile()))));
    }

    // 3-1, 3-2 and 3-4: each handler's status arm
    static void RunC3C2aShort()
    {
        var onTrig = QuestHandlerKind.OnTrigger;
        var zin = QuestHandlerKind.OnZoneIn;
        var area = QuestHandlerKind.OnTriggerAreaEnter;
        var fin = QuestHandlerKind.OnEventFinish;

        var call = new QC.Missions.Cop.TheCallOfTheWyrmking().Compile();
        int m1 = QC.Mission.Cop.TheCallOfTheWyrmking;
        Check("c3 C2a: 3-1 South Gustaberg's zone-in plays 906 at Status 0 only; Port Bastok's area 1 plays 305 at " +
              "Status 1 only; Cid plays 845 at Status 2 only",
              "Event(906)|null|ProgressEvent(305)|null|null|ProgressEvent(845)|null|null",
              string.Join("|", new[]
              {
                  C2aReply(call, 0, zin, QC.Zone.SouthGustaberg, null, C2aFake(m1, 0)),
                  C2aReply(call, 0, zin, QC.Zone.SouthGustaberg, null, C2aFake(m1, 1)),
                  C2aReply(call, 0, area, QC.Zone.PortBastok, null, C2aFake(m1, 1), 1),
                  C2aReply(call, 0, area, QC.Zone.PortBastok, null, C2aFake(m1, 0), 1),
                  C2aReply(call, 0, area, QC.Zone.PortBastok, null, C2aFake(m1, 2), 1),
                  C2aReply(call, 0, onTrig, QC.Zone.Metalworks, QC.Npc.Metalworks.Cid, C2aFake(m1, 2)),
                  C2aReply(call, 0, onTrig, QC.Zone.Metalworks, QC.Npc.Metalworks.Cid, C2aFake(m1, 1)),
                  C2aReply(call, 0, onTrig, QC.Zone.Metalworks, QC.Npc.Metalworks.Cid, C2aFake(m1, 3)),
              }));
        Check("c3 C2a: 3-1's finishes: 906 sets Status 1, 305 sets 2, 845 completes",
              "Mission.SetVar(Status,1)|Mission.SetVar(Status,2)|Mission.Complete()",
              string.Join("|", new[]
              {
                  C2MRun(C2MHandler(call, 0, fin, QC.Zone.SouthGustaberg, id: 906), C2aFake(m1)),
                  C2MRun(C2MHandler(call, 0, fin, QC.Zone.PortBastok, id: 305), C2aFake(m1)),
                  C2MRun(C2MHandler(call, 0, fin, QC.Zone.Metalworks, id: 845), C2aFake(m1)),
              }));

        var vessel = new QC.Missions.Cop.AVesselWithoutACaptain().Compile();
        int m2 = QC.Mission.Cop.AVesselWithoutACaptain;
        Check("c3 C2a: 3-2 Cid plays 861 once a zone at any status; _6tc 86 at Status 0 only; Harnek 12 at Status 1" +
            " only",
              "ProgressEvent(861):oncePerZone()|ProgressEvent(861):oncePerZone()|ProgressEvent(86)|null|" +
              "Event(12):importantEvent()|null",
              string.Join("|", new[]
              {
                  C2aReply(vessel, 0, onTrig, QC.Zone.Metalworks, QC.Npc.Metalworks.Cid, C2aFake(m2, 0)),
                  C2aReply(vessel, 0, onTrig, QC.Zone.Metalworks, QC.Npc.Metalworks.Cid, C2aFake(m2, 1)),
                  C2aReply(vessel, 0, onTrig, QC.Zone.LowerJeuno, QC.Npc.LowerJeuno._6tc, C2aFake(m2, 0)),
                  C2aReply(vessel, 0, onTrig, QC.Zone.LowerJeuno, QC.Npc.LowerJeuno._6tc, C2aFake(m2, 1)),
                  C2aReply(vessel, 0, onTrig, QC.Zone.LowerJeuno, QC.Npc.LowerJeuno.Harnek, C2aFake(m2, 1)),
                  C2aReply(vessel, 0, onTrig, QC.Zone.LowerJeuno, QC.Npc.LowerJeuno.Harnek, C2aFake(m2, 0)),
              }));
        string Area65(int status, int nation) =>
            C2aReply(vessel, 0, area, QC.Zone.RuludeGardens, null, C2aFake(m2, status, nation), 1);
        Check("c3 C2a: 3-2 Ru'Lude's area 1 at Status 1 plays 65 with the char's nation; any other status nothing",
              "ProgressEvent(65,0)|ProgressEvent(65,1)|ProgressEvent(65,2)|null|null",
              $"{Area65(1, 0)}|{Area65(1, 1)}|{Area65(1, 2)}|{Area65(0, 1)}|{Area65(2, 1)}");
        Check("c3 C2a: 3-2's finishes: 86 sets Status 1, 65 completes",
              "Mission.SetVar(Status,1)|Mission.Complete()",
              C2MRun(C2MHandler(vessel, 0, fin, QC.Zone.LowerJeuno, id: 86), C2aFake(m2)) + "|" +
              C2MRun(C2MHandler(vessel, 0, fin, QC.Zone.RuludeGardens, id: 65), C2aFake(m2)));

        var tend = new QC.Missions.Cop.TendingAgedWounds().Compile();
        int m4 = QC.Mission.Cop.TendingAgedWounds;
        Check("c3 C2a: 3-4 Cid plays 862 once a zone at any status; _6tc 22 at Status 1 only; Lower Jeuno's zone-in " +
              "70 at Status 0 only",
              "ProgressEvent(862):oncePerZone()|ProgressEvent(862):oncePerZone()|ProgressEvent(22)|null|Event(70)|null",
              string.Join("|", new[]
              {
                  C2aReply(tend, 0, onTrig, QC.Zone.Metalworks, QC.Npc.Metalworks.Cid, C2aFake(m4, 0)),
                  C2aReply(tend, 0, onTrig, QC.Zone.Metalworks, QC.Npc.Metalworks.Cid, C2aFake(m4, 1)),
                  C2aReply(tend, 0, onTrig, QC.Zone.LowerJeuno, QC.Npc.LowerJeuno._6tc, C2aFake(m4, 1)),
                  C2aReply(tend, 0, onTrig, QC.Zone.LowerJeuno, QC.Npc.LowerJeuno._6tc, C2aFake(m4, 0)),
                  C2aReply(tend, 0, zin, QC.Zone.LowerJeuno, null, C2aFake(m4, 0)),
                  C2aReply(tend, 0, zin, QC.Zone.LowerJeuno, null, C2aFake(m4, 1)),
              }));
        Check("c3 C2a: 3-4's finishes: 22 completes, 70 sets Status 1",
              "Mission.Complete()|Mission.SetVar(Status,1)",
              C2MRun(C2MHandler(tend, 0, fin, QC.Zone.LowerJeuno, id: 22), C2aFake(m4)) + "|" +
              C2MRun(C2MHandler(tend, 0, fin, QC.Zone.LowerJeuno, id: 70), C2aFake(m4)));
    }

    class C2aChipFake : QuestContextFake
    {
        public override bool Matches(params (int Item, int Qty)[] items) =>
            items.Any(i => TradeItems.TryGetValue(i.Item, out var n) && n == i.Qty);
    }

    static void RunC3C2aDarknessNamed()
    {
        var s = new QC.Missions.Cop.DarknessNamed();
        var d = s.Compile();
        int m5 = QC.Mission.Cop.DarknessNamed;
        var onTrig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var jeuno = QC.Zone.LowerJeuno;

        Fake Cur(int current, bool completed = false)
        {
            var f = new Fake { ThisMission = (MissionLog.CoP, m5) };
            f.CurrentMissions[C2aCop] = current;
            if (completed) f.CompletedMissions.Add($"{C2aCop}/{m5}");
            return f;
        }
        Check("c3 C2a: 3-5 section 1 is the mission current only; section 2 holds once it is completed (a later " +
              "mission current) and not while it is current or untouched",
              "True False|False False True",
              $"{d.States[0].IsMissionMatch(Cur(m5))} {d.States[0].IsMissionMatch(Cur(0))}|" +
              $"{d.States[1].IsMissionMatch(Cur(m5))} {d.States[1].IsMissionMatch(Cur(0))} " +
              $"{d.States[1].IsMissionMatch(Cur(QC.Mission.Cop.ShelteringDoubt, true))}");
        string Trig(ushort zone, string npc, int status) =>
            C2aReply(d, 0, onTrig, zone, npc, C2aFake(m5, status));
        Check("c3 C2a: 3-5 Aldo 7, Harnek 13 and Sattal-Mansal 11 at Status 0 only (important); Monberaux 82 at 0, " +
              "3 once at 1, 75 at 5, nothing at 2 to 4; Ghebi 54 at 1, 53 at 2, nothing at 0 and 3",
              "Event(7):importantEvent()|null|Event(13):importantEvent()|null|Event(11):importantEvent()|null|" +
              "ProgressEvent(82)|Event(3):importantOnce()|null|null|null|ProgressEvent(75)|" +
              "ProgressEvent(54)|Event(53):importantEvent()|null|null",
              string.Join("|", new[]
              {
                  Trig(jeuno, QC.Npc.LowerJeuno.Aldo, 0), Trig(jeuno, QC.Npc.LowerJeuno.Aldo, 1),
                  Trig(jeuno, QC.Npc.LowerJeuno.Harnek, 0), Trig(jeuno, QC.Npc.LowerJeuno.Harnek, 1),
                  Trig(jeuno, QC.Npc.LowerJeuno.SattalMansal, 0), Trig(jeuno, QC.Npc.LowerJeuno.SattalMansal, 1),
                  Trig(QC.Zone.UpperJeuno, QC.Npc.UpperJeuno.Monberaux, 0),
                  Trig(QC.Zone.UpperJeuno, QC.Npc.UpperJeuno.Monberaux, 1),
                  Trig(QC.Zone.UpperJeuno, QC.Npc.UpperJeuno.Monberaux, 2),
                  Trig(QC.Zone.UpperJeuno, QC.Npc.UpperJeuno.Monberaux, 3),
                  Trig(QC.Zone.UpperJeuno, QC.Npc.UpperJeuno.Monberaux, 4),
                  Trig(QC.Zone.UpperJeuno, QC.Npc.UpperJeuno.Monberaux, 5),
                  Trig(jeuno, QC.Npc.LowerJeuno.GhebiDamomohe, 1), Trig(jeuno, QC.Npc.LowerJeuno.GhebiDamomohe, 2),
                  Trig(jeuno, QC.Npc.LowerJeuno.GhebiDamomohe, 0), Trig(jeuno, QC.Npc.LowerJeuno.GhebiDamomohe, 3),
              }));

        // Ghebi's trade: the Pass, the status and each chip alone
        string Chip(int item, int status, bool pass, double gilRate = 1)
        {
            var f = new C2aChipFake { ThisMission = (MissionLog.CoP, m5) };
            f.CurrentMissions[C2aCop] = m5;
            f.MissionVars[$"{C2aCop}/{m5}/Status"] = status;
            f.MainNumbers["GIL_RATE"] = gilRate;
            if (pass) f.KeyItems[QC.KeyItem.PsoxjaPass] = true;
            if (item != 0) f.TradeItems[item] = 1;
            var r = C2MHandler(d, 0, QuestHandlerKind.OnTrade, jeuno, QC.Npc.LowerJeuno.GhebiDamomohe).Reply(f);
            return r == null ? "null" : QuestContextFake.Describe(r);
        }
        Check("c3 C2a: 3-5 Ghebi's trade of a Carmine, Cyan or Gray Chip at Status 2 without the Pass plays 52 with " +
              "the gil; the Pass held, Status 1 or 3, no chip or another item each refuse",
              "ProgressEvent(52,500)|ProgressEvent(52,500)|ProgressEvent(52,500)|null|null|null|null|null",
              string.Join("|", new[]
              {
                  Chip(QC.Item.CarmineChip, 2, false), Chip(QC.Item.CyanChip, 2, false),
                  Chip(QC.Item.GrayChip, 2, false), Chip(QC.Item.CarmineChip, 2, true),
                  Chip(QC.Item.CarmineChip, 1, false), Chip(QC.Item.CarmineChip, 3, false),
                  Chip(0, 2, false), Chip(QC.Item.Gil, 2, false),
              }));

        Fake F5(int status = 0, int nation = 0)
        {
            var f = C2aFake(m5, status, nation);
            f.MainNumbers["GIL_RATE"] = 1;
            return f;
        }
        Check("c3 C2a: 3-5's finishes (the Lower Jeuno 52 takes the trade, the gil and the Pass, Status 3; 54 sets " +
            "2; Upper Jeuno 82 takes the amulet, says LEND_PRISHE_AMULET, Status 1; 75 completes; the Maw's 2 " +
            "sets 4)",
              "Trade.Complete(),Player.AddGil(500),Player.GiveKeyItem(604),Mission.SetVar(Status,3)|" +
              "Mission.SetVar(Status,2)|Player.DelKeyItem(579),Text.Special(244:LEND_PRISHE_AMULET,579)," +
              "Mission.SetVar(Status,1)|Mission.Complete()|Mission.SetVar(Status,4)",
              string.Join("|", new[]
              {
                  C2MRun(C2MHandler(d, 0, fin, jeuno, id: 52), F5()),
                  C2MRun(C2MHandler(d, 0, fin, jeuno, id: 54), F5()),
                  C2MRun(C2MHandler(d, 0, fin, QC.Zone.UpperJeuno, id: 82), F5()),
                  C2MRun(C2MHandler(d, 0, fin, QC.Zone.UpperJeuno, id: 75), F5()),
                  C2MRun(C2MHandler(d, 0, fin, QC.Zone.TheShroudedMaw, id: 2), F5()),
              }));
        string Gil(double rate)
        {
            var f = F5();
            f.MainNumbers["GIL_RATE"] = rate;
            return C2MRun(C2MHandler(d, 0, fin, jeuno, id: 52), f).Split(',')[1];
        }
        Check("c3 C2a: 3-5's gil is GIL_RATE * 500 rounded half away from zero (the engine's llround): 1, 1.5, " +
              "0.001 (0.5 up to 1), 0.0014 (0.7 up to 1), 0.0009 (0.45 down to 0)",
              "Player.AddGil(500) Player.AddGil(750) Player.AddGil(1) Player.AddGil(1) Player.AddGil(0)",
              $"{Gil(1)} {Gil(1.5)} {Gil(0.001)} {Gil(0.0014)} {Gil(0.0009)}");
        string Maw(int status, int win) => C2MRun(C2MHandler(d, 0, fin, QC.Zone.TheShroudedMaw, id: 32001),
            WinFake(status, win));
        Fake WinFake(int status, int win)
        {
            var f = F5(status);
            f.PlayerLocalVars["battlefieldWin"] = win;
            return f;
        }
        Check("c3 C2a: 3-5's 32001 sets Status 5 at Status 4 after Darkness Named's win only: Status 3 or 5, another " +
              "battlefield's win and none each do nothing",
              "Mission.SetVar(Status,5)||||",
              $"{Maw(4, QC.Battlefield.DarknessNamed)}|{Maw(3, QC.Battlefield.DarknessNamed)}|" +
              $"{Maw(5, QC.Battlefield.DarknessNamed)}|{Maw(4, QC.Battlefield.AncientVows)}|{Maw(4, 0)}");
        Check("c3 C2a: 3-5's Shrouded Maw zone-in plays 2 at Status 3 only", "Event(2)|null|null",
              $"{C2aReply(d, 0, QuestHandlerKind.OnZoneIn, QC.Zone.TheShroudedMaw, null, F5(3))}|" +
              $"{C2aReply(d, 0, QuestHandlerKind.OnZoneIn, QC.Zone.TheShroudedMaw, null, F5(2))}|" +
              $"{C2aReply(d, 0, QuestHandlerKind.OnZoneIn, QC.Zone.TheShroudedMaw, null, F5(4))}");
        string Tav(string npc, bool slanderous) =>
            C2aReply(d, 1, onTrig, QC.Zone.TavnazianSafehold, npc, SlanderFake(slanderous));
        Fake SlanderFake(bool done)
        {
            var f = new Fake();
            if (done) f.CompletedMissions.Add($"{C2aCop}/{QC.Mission.Cop.SlanderousUtterings}");
            return f;
        }
        Check("c3 C2a: 3-5 section 2: Chemioue 282, Justinius 129, Parelbriaux 296 replace the default; " +
              "Arquil 292 and Despachiaire 315 only while Slanderous Utterings is not completed",
              "Event(282):replaceDefault()|Event(282):replaceDefault()|Event(129):replaceDefault()|" +
              "Event(296):replaceDefault()|Event(292):replaceDefault()|null|Event(315):replaceDefault()|null",
              string.Join("|", new[]
              {
                  Tav(QC.Npc.TavnazianSafehold.Chemioue, false), Tav(QC.Npc.TavnazianSafehold.Chemioue, true),
                  Tav(QC.Npc.TavnazianSafehold.Justinius, true), Tav(QC.Npc.TavnazianSafehold.Parelbriaux, true),
                  Tav(QC.Npc.TavnazianSafehold.Arquil, false), Tav(QC.Npc.TavnazianSafehold.Arquil, true),
                  Tav(QC.Npc.TavnazianSafehold.Despachiaire, false), Tav(QC.Npc.TavnazianSafehold.Despachiaire, true),
              }));
    }

    static void RunC3C2aRoadForks()
    {
        var d = new QC.Missions.Cop.TheRoadForks().Compile();
        int m3 = QC.Mission.Cop.TheRoadForks;
        var onTrig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var zin = QuestHandlerKind.OnZoneIn;
        const int sd = 0, win = 1, done = 2;

        // the CoP status word: field 0 San d'Oria, field 1 Windurst
        Fake RF(int sandoria, int windurst = 0, int nation = 0, int status = 0)
        {
            var f = C2aFake(m3, status, nation);
            f.MissionStatuses[C2aCop] = sandoria | windurst << 4;
            f.TextIds[QC.Text.AttohwaChasm.MIMEO_JEWEL_OFFSET.ToString()] = 7341;
            return f;
        }
        string Rep(int state, QuestHandlerKind kind, ushort zone, string npc, Fake f, int id = 1)
        {
            var h = C2MHandler(d, state, kind, zone, npc, id);
            string r = C2MReply(h, f);
            return f.Calls.Count == 0 ? r : $"{r} {string.Join(";", f.Calls)}";
        }
        string Run(int state, QuestHandlerKind kind, ushort zone, Fake f, int id, string npc = null) =>
            C2MRun(C2MHandler(d, state, kind, zone, npc, id), f);
        string Holds(Fake f, bool current = true)
        {
            if (!current) f.CurrentMissions[C2aCop] = 0;
            return $"{d.States[sd].IsMissionMatch(f)} {d.States[win].IsMissionMatch(f)} " +
                   $"{d.States[done].IsMissionMatch(f)}";
        }

        Check("c3 C2a: 3-3's sections: San d'Oria holds while field 0 is 14 or less (15 ends it), Windurst while " +
              "field 1 is, the completion only with both at 14 (then all three hold); each needs the mission current",
              "True True False|True True True|False True False|True False False|True True True|False False False",
              string.Join("|", new[]
              {
                  Holds(RF(0)), Holds(RF(14, 14, 0)), Holds(RF(15, 0)), Holds(RF(14, 15)), Holds(RF(14, 14)),
                  Holds(RF(14, 14), current: false),
              }));

        // San d'Oria path
        string Nsd(string npc, int s0) => Rep(sd, onTrig, QC.Zone.NorthernSanDoria, npc, RF(s0));
        Check("c3 C2a: 3-3 SD: Arnau 51 at field 0 = 1; Chasalvige 38 at 2, event 6 (replaces the default) at 5; " +
              "zone-in 14 at 0 only; Hinaree 23 at 9, 24 at 14",
              "ProgressEvent(51)|null|null|ProgressEvent(38)|Event(6):replaceDefault()|null|null|Event(14)|null|" +
              "ProgressEvent(23)|Event(24):replaceDefault()|null",
              string.Join("|", new[]
              {
                  Nsd(QC.Npc.NorthernSanDoria.Arnau, 1), Nsd(QC.Npc.NorthernSanDoria.Arnau, 2),
                  Nsd(QC.Npc.NorthernSanDoria.Arnau, 0),
                  Nsd(QC.Npc.NorthernSanDoria.Chasalvige, 2), Nsd(QC.Npc.NorthernSanDoria.Chasalvige, 5),
                  Nsd(QC.Npc.NorthernSanDoria.Chasalvige, 1), Nsd(QC.Npc.NorthernSanDoria.Chasalvige, 6),
                  Rep(sd, zin, QC.Zone.NorthernSanDoria, null, RF(0)), Rep(sd, zin, QC.Zone.NorthernSanDoria, null,
                       RF(1)),
                  Rep(sd, onTrig, QC.Zone.SouthernSanDoria, QC.Npc.SouthernSanDoria.Hinaree, RF(9)),
                  Rep(sd, onTrig, QC.Zone.SouthernSanDoria, QC.Npc.SouthernSanDoria.Hinaree, RF(14)),
                  Rep(sd, onTrig, QC.Zone.SouthernSanDoria, QC.Npc.SouthernSanDoria.Hinaree, RF(10)),
              }));
        Check("c3 C2a: 3-3 SD finishes: 14 sets field 0 to 1, 51 to 2, 38 to 5, Carpenter's Landing's 0 to 9, " +
              "Southern's 23 to 14 (the field index 0)",
              "Mission.SetStatusAt(1,0)|Mission.SetStatusAt(2,0)|Mission.SetStatusAt(5,0)|Mission.SetStatusAt(9,0)|" +
              "Mission.SetStatusAt(14,0)",
              string.Join("|", new[]
              {
                  Run(sd, fin, QC.Zone.NorthernSanDoria, RF(0), 14), Run(sd, fin, QC.Zone.NorthernSanDoria, RF(0), 51),
                  Run(sd, fin, QC.Zone.NorthernSanDoria, RF(0), 38), Run(sd, fin, QC.Zone.CarpentersLanding, RF(0), 0),
                  Run(sd, fin, QC.Zone.SouthernSanDoria, RF(0), 23),
              }));
        string Guilloud(int s0, bool ivyDefeated = false, bool spawned = false, int nation = 0)
        {
            var f = RF(s0, 0, nation);
            if (ivyDefeated) f.MissionLocalVars["ivyDefeated"] = 1;
            if (spawned) f.SpawnedMobs.Add("OVERGROWN_IVY+0");
            return Rep(sd, onTrig, QC.Zone.CarpentersLanding, QC.Npc.CarpentersLanding.Guilloud, f);
        }
        Check("c3 C2a: 3-3 Guilloud at 5: the Ivy defeated plays cutscene 0 with 1 for a San d'Orian (0 else), " +
            "whether or not it is spawned; not spawned: two messages, the Ivy spawned claimed, NoAction; spawned:" +
            " BEGONE_TRESPASSER priority 1000; any other field nothing",
              "ProgressCutscene(0,1)|ProgressCutscene(0,0)|ProgressCutscene(0,1)|NoAction() " +
              "Text.MessageText(2:YOU_WISH_TO_KNOW_MISTALLE,True,0);Text.MessageText(2:SQUASH_ANOTHER_WORM,True,0);" +
              "Player.SpawnMob(OVERGROWN_IVY,0,True)|MessageName(2:BEGONE_TRESPASSER):setPriority(1000)|null|null|" +
              "null",
              string.Join("|", new[]
              {
                  Guilloud(5, true), Guilloud(5, true, nation: 1), Guilloud(5, true, true), Guilloud(5),
                  Guilloud(5, spawned: true), Guilloud(4, true), Guilloud(6), Guilloud(14),
              }));
        string Ivy(int s0) => Run(sd, QuestHandlerKind.OnMobDeath, QC.Zone.CarpentersLanding, RF(s0), 1,
                                  QC.Npc.CarpentersLanding.OvergrownIvy);
        Check("c3 C2a: 3-3's Ivy death sets ivyDefeated at field 0 = 5 only", "Mission.SetLocalVar(ivyDefeated,1)||",
              $"{Ivy(5)}|{Ivy(4)}|{Ivy(6)}");

        // Windurst path
        string Wat(string npc, int s1) => Rep(win, onTrig, QC.Zone.WindurstWaters, npc, RF(0, s1));
        Check("c3 C2a: 3-3 Windurst Waters: Honoi-Gomoi 874 at 5, 879 (the Mirror) at 6; Kyume-Romeh 873 at 3, 878 " +
            "at 5; Ohbiru-Dohbiru 872 at 1, 877 at 2; Tosuka-Porika 875 at 11, 881 at 12; the zone-in 871 at 0",
              "ProgressEvent(874)|Event(879,0,581):importantEvent()|null|ProgressEvent(873)|" +
              "Event(878):importantEvent()|null|ProgressEvent(872)|Event(877):importantEvent()|null|" +
              "ProgressEvent(875)|Event(881):importantEvent()|null|Event(871)|null",
              string.Join("|", new[]
              {
                  Wat(QC.Npc.WindurstWaters.HonoiGomoi, 5), Wat(QC.Npc.WindurstWaters.HonoiGomoi, 6),
                  Wat(QC.Npc.WindurstWaters.HonoiGomoi, 4),
                  Wat(QC.Npc.WindurstWaters.KyumeRomeh, 3), Wat(QC.Npc.WindurstWaters.KyumeRomeh, 5),
                  Wat(QC.Npc.WindurstWaters.KyumeRomeh, 4),
                  Wat(QC.Npc.WindurstWaters.OhbiruDohbiru, 1), Wat(QC.Npc.WindurstWaters.OhbiruDohbiru, 2),
                  Wat(QC.Npc.WindurstWaters.OhbiruDohbiru, 3),
                  Wat(QC.Npc.WindurstWaters.TosukaPorika, 11), Wat(QC.Npc.WindurstWaters.TosukaPorika, 12),
                  Wat(QC.Npc.WindurstWaters.TosukaPorika, 13),
                  Rep(win, zin, QC.Zone.WindurstWaters, null, RF(0, 0)), Rep(win, zin, QC.Zone.WindurstWaters, null,
                       RF(0, 1)),
              }));
        string Yoran(int s1, bool feather = false)
        {
            var f = RF(0, s1);
            if (feather) f.KeyItems[QC.KeyItem.MimeoFeather] = true;
            return Rep(win, onTrig, QC.Zone.WindurstWalls, QC.Npc.WindurstWalls.YoranOran, f);
        }
        Check("c3 C2a: 3-3 Yoran-Oran: 469 at 2, 474 at 3, 470 (the three items) at 6, 471 at 8 with the Feather " +
            "else 476, 477 at 9, 472 at 12, 478 at 14, nothing else",
              "ProgressEvent(469)|Event(474):importantEvent()|ProgressEvent(470,0,587,581,586)|ProgressEvent(471)|" +
              "Event(476,0,587,0,586):importantEvent()|Event(477):importantEvent()|ProgressEvent(472)|" +
              "Event(478):importantEvent()|null|null|null",
              string.Join("|", new[]
              {
                  Yoran(2), Yoran(3), Yoran(6), Yoran(8, true), Yoran(8), Yoran(9), Yoran(12), Yoran(14), Yoran(1),
                  Yoran(4), Yoran(13),
              }));
        Check("c3 C2a: 3-3 Windurst finishes: 871, 872, 873, 469, 472, 592, 875 set their fields; 874 gives the " +
            "Mirror and sets 6; 470 takes it and sets 8; 471 takes the three Feathers and sets 9",
              "Mission.SetStatusAt(1,1)|Mission.SetStatusAt(2,1)|Mission.SetStatusAt(5,1)|" +
              "Player.GiveKeyItem(581),Mission.SetStatusAt(6,1)|Mission.SetStatusAt(12,1)|Mission.SetStatusAt(3,1)|" +
              "Player.DelKeyItem(581),Mission.SetStatusAt(8,1)|" +
              "Player.DelKeyItem(587),Player.DelKeyItem(588),Player.DelKeyItem(589),Mission.SetStatusAt(9,1)|" +
              "Mission.SetStatusAt(14,1)|Mission.SetStatusAt(11,1)",
              string.Join("|", new[]
              {
                  Run(win, fin, QC.Zone.WindurstWaters, RF(0), 871), Run(win, fin, QC.Zone.WindurstWaters, RF(0), 872),
                  Run(win, fin, QC.Zone.WindurstWaters, RF(0), 873), Run(win, fin, QC.Zone.WindurstWaters, RF(0), 874),
                  Run(win, fin, QC.Zone.WindurstWaters, RF(0), 875), Run(win, fin, QC.Zone.WindurstWalls, RF(0), 469),
                  Run(win, fin, QC.Zone.WindurstWalls, RF(0), 470), Run(win, fin, QC.Zone.WindurstWalls, RF(0), 471),
                  Run(win, fin, QC.Zone.WindurstWalls, RF(0), 472), Run(win, fin, QC.Zone.PortWindurst, RF(0), 592),
              }));
        string Yujuju(int s1) => Rep(win, onTrig, QC.Zone.PortWindurst, QC.Npc.PortWindurst.Yujuju, RF(0, s1));
        Check("c3 C2a: 3-3 Port Windurst Yujuju: 592 at 9, 593 at 11, nothing else",
             "ProgressEvent(592)|Event(593):importantEvent()|null",
              $"{Yujuju(9)}|{Yujuju(11)}|{Yujuju(10)}");

        // Attohwa Chasm
        string Sand(double distance, int s1, int status, bool spawned = false, bool jewel = false)
        {
            var f = RF(0, s1, 0, status);
            f.Npc = new QuestNpcFake(1, f.Calls) { PlayerDistance = distance };
            if (spawned) f.SpawnedMobs.Add("LIOUMERE+0");
            if (jewel) f.KeyItems[QC.KeyItem.MimeoJewel] = true;
            f.Now = 1000;
            return Rep(win, onTrig, QC.Zone.AttohwaChasm, QC.Npc.AttohwaChasm.LooseSand, f) +
                   $" opt={f.MissionLocalVars.GetValueOrDefault("Option")}" +
                   $" timer={f.MissionLocalVars.GetValueOrDefault("Timer")}";
        }
        Check("c3 C2a: 3-3 Loose Sand: 0.5 yalms or more away MUST_MOVE_CLOSER; at 8 and Status 0 not spawned: the " +
            "Lioumere spawned claimed; spawned: nothing; Status 1 without the Jewel: the Jewel, Option 0, Timer " +
            "now + 1800, the callback's first message; with it nothing; not at 8 nothing",
              "MessageSpecial(7:MUST_MOVE_CLOSER) opt=0 timer=0|" +
              "NoAction() Player.SpawnMob(LIOUMERE,0,True) opt=0 timer=0|" +
              "MessageSpecial(7:MUST_MOVE_CLOSER) opt=0 timer=0|null opt=0 timer=0|NoAction() " +
              "Player.GiveKeyItem(586);Mission.SetLocalVar(Option,0);Mission.SetLocalVar(Timer,2800);Text.Special(#" +
              "7340,586);Mission.SetLocalVar(Option,1);Player.Timer(3000,jewelTimer) opt=1 timer=2800|" +
              "null opt=0 timer=0|null opt=0 timer=0|null opt=0 timer=0|null opt=0 timer=0",
              string.Join("|", new[]
              {
                  Sand(0.5, 8, 0), Sand(0.49, 8, 0), Sand(2, 8, 1), Sand(0.49, 8, 0, true), Sand(0.49, 8, 1),
                  Sand(0.49, 8, 1, jewel: true), Sand(0.49, 7, 0), Sand(0.49, 8, 2), Sand(0.49, 9, 1),
              }));
        string Lio(int s1, int status) => Run(win, QuestHandlerKind.OnMobDeath, QC.Zone.AttohwaChasm, RF(0, s1, 0,
             status),
                                              1, QC.Npc.AttohwaChasm.Lioumere);
        Check("c3 C2a: 3-3 Lioumere death sets Status 1 at field 1 = 8 and Status 0 only",
             "Mission.SetVar(Status,1)|||",
              $"{Lio(8, 0)}|{Lio(8, 1)}|{Lio(7, 0)}|{Lio(9, 0)}");

        string Cradle(bool jewel, bool next = true)
        {
            var f = RF(0, 8);
            if (jewel) f.KeyItems[QC.KeyItem.MimeoJewel] = true;
            f.Npc = new QuestNpcFake(500, f.Calls);
            if (next) f.NpcsById[501] = new QuestNpcFake(501, f.Calls);
            return Rep(win, onTrig, QC.Zone.AttohwaChasm, QC.Npc.AttohwaChasm.CradleofRebirth, f);
        }
        Check("c3 C2a: 3-3 Cradle of Rebirth with the Jewel plays 2 and sends 'krtu' to the next NPC when there is " +
            "one; without the Jewel nothing",
              "ProgressEvent(2) Npc.EntityAnimationPacket(krtu)|ProgressEvent(2)|null",
                   $"{Cradle(true)}|{Cradle(true, false)}|{Cradle(false)}");
        Check("c3 C2a: 3-3's Cradle finish 2 takes the Jewel and gives the three Feathers",
             "Player.DelKeyItem(586),Player.GiveKeyItem(587),Player.GiveKeyItem(588),Player.GiveKeyItem(589)",
              Run(win, fin, QC.Zone.AttohwaChasm, RF(0, 8), 2));
        string Out(bool jewel)
        {
            var f = RF(0, 8);
            if (jewel) f.KeyItems[QC.KeyItem.MimeoJewel] = true;
            return Run(win, QuestHandlerKind.OnZoneOut, QC.Zone.AttohwaChasm, f, 1);
        }
        Check("c3 C2a: 3-3's zone-out of Attohwa Chasm with the Jewel says MIMEO_JEWEL_OFFSET + 4 and takes it; " +
              "without it nothing", "Text.Special(#7345,586),Player.DelKeyItem(586)|", $"{Out(true)}|{Out(false)}");

        var timer = d.Core.Callbacks.Single(c => c.Name == "jewelTimer");
        string Jewel(int now, int timerAt, int option, bool jewel = true)
        {
            var f = RF(0, 8);
            f.Now = now;
            if (jewel) f.KeyItems[QC.KeyItem.MimeoJewel] = true;
            f.MissionLocalVars["Timer"] = timerAt;
            f.MissionLocalVars["Option"] = option;
            f.RunCallback(timer, null);
            return $"{string.Join(";", f.Calls)} opt={f.MissionLocalVars["Option"]}";
        }
        Check("c3 C2a: 3-3's jewelTimer: no Jewel stops; at or past the expiry the last message (offset 4) and the " +
              "Jewel taken; before it the message at each 6 minute mark (next = 30 - 6 * Option minutes, the message " +
              "OFFSET + Option - 1, Option + 1) and the timer armed again in 3 s",
              " opt=0|Text.Special(#7345,586);Player.DelKeyItem(586) opt=0|" +
              "Text.Special(#7345,586);Player.DelKeyItem(586) opt=0|" +
              "Text.Special(#7340,586);Mission.SetLocalVar(Option,1);Player.Timer(3000,jewelTimer) opt=1|" +
              "Text.Special(#7340,586);Mission.SetLocalVar(Option,1);Player.Timer(3000,jewelTimer) opt=1|" +
              "Player.Timer(3000,jewelTimer) opt=0|" +
              "Text.Special(#7341,586);Mission.SetLocalVar(Option,2);Player.Timer(3000,jewelTimer) opt=2|" +
              "Player.Timer(3000,jewelTimer) opt=1|" +
              "Text.Special(#7342,586);Mission.SetLocalVar(Option,3);Player.Timer(3000,jewelTimer) opt=3|" +
              "Text.Special(#7344,586);Mission.SetLocalVar(Option,5);Player.Timer(3000,jewelTimer) opt=5|" +
              "Player.Timer(3000,jewelTimer) opt=4|Player.Timer(3000,jewelTimer) opt=5",
              string.Join("|", new[]
              {
                  Jewel(1000, 2800, 0, false), Jewel(1000, 1000, 0), Jewel(1000, 999, 0), Jewel(1000, 1001, 0),
                  Jewel(1000, 1000 + 1800, 0), Jewel(1000, 1000 + 1801, 0), Jewel(1000, 1000 + 1440, 1),
                  Jewel(1000, 1000 + 1441, 1), Jewel(1000, 1000 + 1080, 2), Jewel(1000, 1000 + 360, 4),
                  Jewel(1000, 1000 + 361, 4), Jewel(1000, 1000 + 60, 5),
              }));

        string Cid(int s0, int s1) => Rep(done, onTrig, QC.Zone.Metalworks, QC.Npc.Metalworks.Cid, RF(s0, s1));
        Check("c3 C2a: 3-3 Cid plays 847 with both fields at 14 and its finish completes the mission",
             "ProgressEvent(847)|Mission.Complete()",
              $"{Cid(14, 14)}|{Run(done, fin, QC.Zone.Metalworks, RF(14, 14), 847)}");
    }

    static void RunC3C2aBattlefield()
    {
        var s = new QC.Battlefields.TheShroudedMaw.DarknessNamed();
        var d = (BattlefieldMissionData)s.Data;
        const int maw = QC.Zone.TheShroudedMaw;
        Check("c3 C2a: Darkness Named validates clean, its key and Lua file",
              "|battlefield.darkness_named scripts/battlefields/The_Shrouded_Maw/darkness_named",
              $"{string.Join(" | ", BattlefieldScriptValidator.Validate(s))}|{s.Key} {s.LegacyPath}");
        Check("c3 C2a: Darkness Named's fields (zone, id, no exp loss, mission, trusts, players, MAX_LEVEL, time, " +
              "index, grantXP, title, entry and exit NPCs, mission, required var and value, groups)",
              "10 704 False True True 6 MAX_LEVEL 1800 0 1000 Dreambreaker MC_Entrance Memento_Circle 6/358 " +
              "Mission[6][358]Status 4 2",
              $"{d.ZoneId} {d.BattlefieldId} {d.CanLoseExp} {d.IsMission} {d.AllowTrusts} {d.MaxPlayers} " +
              $"{d.LevelCapSetting} {d.TimeLimit} {d.Index} {d.GrantXP} " +
              $"{(d.Title == QC.Title.Dreambreaker ? "Dreambreaker" : d.Title.ToString())} " +
              $"{string.Join(",", d.EntryNpcs)} {string.Join(",", d.ExitNpcs)} {(int)d.MissionArea}/{d.Mission} " +
              $"{d.RequiredVar} {d.RequiredValue} {d.Groups.Count}");
        Check("c3 C2a: Darkness Named's groups: the three Diabolos (ids +0, +7, +14) win when dead; the six " +
            "Diremites of each area (+1..6, +8..13, +15..20) are superlinked, with no allDeath",
              "DIABOLOS:0;DIABOLOS:7;DIABOLOS:14 True False|" +
              "DIABOLOS:1,2,3,4,5,6;DIABOLOS:8,9,10,11,12,13;DIABOLOS:15,16,17,18,19,20 False True",
              string.Join("|", d.Groups.Select(g =>
                  string.Join(";", g.AreaMobIds.Select(a => a[0].Key + ":" + string.Join(",",
                       a.Select(m => m.Offset)))) +
                  $" {g.AllDeath != null} {g.Superlink}")));
        Check("c3 C2a: Darkness Named overrides onEventFinishWin, setupBattlefield and onBattlefieldTick; its " +
            "coverage labels", "OnBattlefieldTick,OnEventFinishWin,SetupBattlefield|" +
              "hook:OnBattlefieldTick,hook:OnEventFinishWin,hook:SetupBattlefield,group:1:allDeath,exit:Memento_Cir" +
              "cle",
              $"{string.Join(",", BattlefieldScript.HookNames.Where(s.Overrides))}|" +
              string.Join(",", s.CoverageLabels()));

        // onEventFinishWin: both titles (Transient Dreamer first), the base's the Dreambreaker and the XP
        {
            var (q, p) = C2BContext(s, "MC_Entrance");
            p.Now = 1000;
            p.NextJstMidnight = 5000;
            s.OnEventFinishWin(q, 32001, 0);
            Check("c3 C2a: Darkness Named's onEventFinishWin adds Transient Dreamer, then the base's title, the XP " +
                "var and the XP",
                "Player.AddTitle(349),Player.AddTitle(356),Player.SetVar(Battlefield[704]XP,5000),Player.AddExp(1000)",
                string.Join(",", p.Calls));
        }

        // setupBattlefield: the area's eight tiles closed (the animation 9), none other
        string Setup(int area)
        {
            var (q, _) = C2BContext(s, "MC_Entrance");
            var e = (BattlefieldEngineFake)q.Engine;
            e.ZoneNpcIds[$"{maw}:DARKNESS_NAMED_TILE_OFFSET"] = 5000;
            var tiles = new Dictionary<int, BattlefieldNpcFake>();
            for (int id = 4990; id < 5040; id++) e.Npcs[id] = tiles[id] = new BattlefieldNpcFake { Id = id };
            q.Battlefield = new BattlefieldViewFake { Id = d.BattlefieldId, ZoneId = maw, Area = area };
            s.SetupBattlefield(q);
            return string.Join(",", tiles.Where(t => t.Value.Animations.Count > 0)
                .Select(t => $"{t.Key}:{string.Join("+", t.Value.Animations)}"));
        }
        Check("c3 C2a: Darkness Named's setupBattlefield closes tiles offset + 8 * (area - 1) to +7 with animation 9",
              "5000:9,5001:9,5002:9,5003:9,5004:9,5005:9,5006:9,5007:9|" +
              "5008:9,5009:9,5010:9,5011:9,5012:9,5013:9,5014:9,5015:9|" +
              "5016:9,5017:9,5018:9,5019:9,5020:9,5021:9,5022:9,5023:9", $"{Setup(1)}|{Setup(2)}|{Setup(3)}");

        // onBattlefieldTick
        var boxes = new[]
        {
            new double[] { -250, -230, -46, -25, 275, 294 },
            new double[] { -10, 10, 0, 13, -6, 14 },
            new double[] { 270, 290, 35, 52, -286, -266 },
        };
        (BattlefieldContextFake Q, BattlefieldEngineFake E, BattlefieldMobFake Mite, List<BattlefieldPlayerFake> P)
            Arena(int area, bool engaged, bool alive, params (double X, double Y, double Z, int Pit)[] players)
        {
            var (q, _) = C2BContext(s, "MC_Entrance");
            var e = (BattlefieldEngineFake)q.Engine;
            e.ZoneMobIds[$"{maw}:DIABOLOS"] = 1000;
            int baseId = 1000 + (area - 1) * 7;
            BattlefieldMobFake mite = null;
            for (int i = 0; i <= 7; i++)
            {
                var m = new BattlefieldMobFake { Id = baseId + i, IsSpawned = true, HP = 100, MaxHP = 100 };
                if (i == 1)
                {
                    mite = m;
                    m.IsSpawned = alive;
                    m.IsEngaged = engaged;
                }
                e.MobsById[m.Id] = m;
            }
            var view = new BattlefieldViewFake { Id = d.BattlefieldId, ZoneId = maw, Area = area };
            var list = new List<BattlefieldPlayerFake>();
            foreach (var (x, y, z, pit) in players)
            {
                var p = new BattlefieldPlayerFake { XPos = x, YPos = y, ZPos = z, CharId = 10 + list.Count };
                if (pit != 0) p.PlayerLocalVars["inDiremitePit"] = pit;
                list.Add(p);
                view.PlayerList.Add(p);
            }
            q.Battlefield = view;
            return (q, e, mite, list);
        }
        string Tick(int area, bool engaged, bool alive, params (double X, double Y, double Z, int Pit)[] players)
        {
            var (q, e, mite, ps) = Arena(area, engaged, alive, players);
            s.OnBattlefieldTick(q, 1);
            return $"{string.Join(",", mite.Calls)}|{string.Join(",", e.Calls)}|" +
                   string.Join(";", ps.Select(p => string.Join(",", p.Calls)));
        }
        var inside1 = (-240.0, -30.0, 280.0, 0);
        Check("c3 C2a: Darkness Named's tick on an engaged live Diremite: all players inside disengages it; one " +
            "outside marks it in the pit (no disengage); one outside already in the pit changes nothing; one back" +
            " inside from the pit resets enmity, clears the mark and disengages; one inside and not marked " +
            "changes nothing; no players at all disengages",
              "Mob.Disengage()||||||;Player.SetLocalVar(inDiremitePit,1)||||;||Mob.ResetEnmity(10),Mob.Disengage()|" +
              "|Player.SetLocalVar(inDiremitePit,0)||Mob.ResetEnmity(10)||" +
              "Player.SetLocalVar(inDiremitePit,0);Player.SetLocalVar(inDiremitePit,1)||Mob.Disengage()||",
              string.Join("||", new[]
              {
                  Tick(1, true, true, inside1), Tick(1, true, true, inside1, (0, 0, 0, 0)),
                  Tick(1, true, true, inside1, (0, 0, 0, 1)), Tick(1, true, true, (-240, -30, 280, 1)),
                  Tick(1, true, true, (-240, -30, 280, 1), (0, 0, 0, 0)), Tick(1, true, true),
              }));
        Check("c3 C2a: a player inside the box whose inDiremitePit mark is 2 (not 1) is not reset: no enmity reset " +
            "and no var write, the Diremite is disengaged",
              "Mob.Disengage()||",
              Tick(1, true, true, (-240, -30, 280, 2)));
        Check("c3 C2a: Darkness Named's tick does nothing to a Diremite that is not engaged or not alive (the area " +
              "and the players do not matter)",
              "||||||||||",
              $"{Tick(1, false, true, (0, 0, 0, 0))}||{Tick(1, true, false, (0, 0, 0, 0))}||" +
              $"{Tick(1, false, false, (0, 0, 0, 1))}");
        // the arena boxes: each face inclusive, a hair past it out; the Diremite of area N reads box N
        string Faces(int area)
        {
            var b = boxes[area - 1];
            var mid = new[] { (b[0] + b[1]) / 2, (b[2] + b[3]) / 2, (b[4] + b[5]) / 2 };
            var res = new List<string>();
            for (int axis = 0; axis < 3; axis++)
            {
                foreach (double v in new[] { b[axis * 2], b[axis * 2 + 1], b[axis * 2] - 0.001,
                     b[axis * 2 + 1] + 0.001 })
                {
                    var pos = (double[])mid.Clone();
                    pos[axis] = v;
                    var (q, _, mite, _) = Arena(area, true, true, (pos[0], pos[1], pos[2], 0));
                    s.OnBattlefieldTick(q, 1);
                    res.Add(mite.Calls.Contains("Mob.Disengage()") ? "in" : "out");
                }
            }
            return string.Join(",", res);
        }
        string pattern = "in,in,out,out,in,in,out,out,in,in,out,out";
        Check("c3 C2a: Darkness Named's arena boxes: each of the six faces inclusive (min, max in; a hair below min, " +
              "above max out), the box of the Diremite's own area",
              $"{pattern}|{pattern}|{pattern}", $"{Faces(1)}|{Faces(2)}|{Faces(3)}");
        Check("c3 C2a: a player inside another area's box is outside this one's (area 2's arena for an area 1 " +
            "Diremite)",
              "||Player.SetLocalVar(inDiremitePit,1)",
              Tick(1, true, true, (0, 5, 5, 0)));

        // the respawn: dead for 10 s since the deathTime its zone script set
        string Respawn(bool alive, uint deathTime, uint now, bool engaged = false)
        {
            var (q, e, mite, _) = Arena(1, engaged, alive);
            e.SystemTime = now;
            mite.LocalVars["deathTime"] = deathTime;
            s.OnBattlefieldTick(q, 1);
            return $"{string.Join(",", e.Calls)} {mite.IsSpawned} dt={mite.LocalVar("deathTime")}";
        }
        Check("c3 C2a: Darkness Named's respawn: a dead Diremite with a deathTime 10 seconds old or more respawns " +
            "and its deathTime clears; 9 seconds, no deathTime, a deathTime in the future and a living one do not",
              "SpawnMob(1001) True dt=0| False dt=1000|SpawnMob(1001) True dt=0| False dt=0| False dt=1000|" +
              " True dt=1000",
              string.Join("|", new[]
              {
                  Respawn(false, 1000, 1010), Respawn(false, 1000, 1009), Respawn(false, 1000, 1011),
                  Respawn(false, 0, 5000), Respawn(false, 1000, 990), Respawn(true, 1000, 2000),
              }));
        // base.OnBattlefieldTick runs first, then the six Diremites of the area only (offsets +1..+6, not +0 or +7)
        string Reach(int area)
        {
            var (q, e, _, _) = Arena(area, false, false);
            e.SystemTime = 2000;
            foreach (var m in e.MobsById.Values.Cast<BattlefieldMobFake>())
            {
                m.IsSpawned = false;
                m.LocalVars["deathTime"] = 1000;
            }
            try { s.OnBattlefieldTick(q, 1); }
            catch (Exception ex) { return "THREW " + ex.GetType().Name; }
            return string.Join(",", e.Calls);
        }
        Check("c3 C2a: Darkness Named's tick walks the six Diremites of its area (+1 to +6 of that area's Diabolos), " +
              "not the Diabolos or the next area's",
                   "SpawnMob(1001),SpawnMob(1002),SpawnMob(1003),SpawnMob(1004),SpawnMob(1005),SpawnMob(1006)|" +
              "SpawnMob(1008),SpawnMob(1009),SpawnMob(1010),SpawnMob(1011),SpawnMob(1012),SpawnMob(1013)|" +
              "SpawnMob(1015),SpawnMob(1016),SpawnMob(1017),SpawnMob(1018),SpawnMob(1019),SpawnMob(1020)",
                   $"{Reach(1)}|{Reach(2)}|{Reach(3)}");

        // the shared helper (Battlefields/Shared/DiremitePits): the Diabolos key and the tile key are parameters
        string WdTick(int area, string key)
        {
            var (q, e, _, _) = Arena(area, false, false);
            e.ZoneMobIds[$"{maw}:DIABOLOS_WD"] = 3000;
            e.SystemTime = 2000;
            foreach (int id in new[] { 1001, 1008, 3001, 3008 })
            {
                var m = new BattlefieldMobFake { Id = id, IsSpawned = false };
                m.LocalVars["deathTime"] = 1000;
                e.MobsById[id] = m;
            }
            try { QC.Battlefields.Shared.DiremitePits.Tick(q, key); }
            catch (Exception ex) { return "THREW " + ex.GetType().Name; }
            return string.Join(",", e.Calls);
        }
        Check("c3 C2a: DiremitePits.Tick reads the zone mob id of the key it is given (DIABOLOS_WD walks 3001 and " +
            "3008 of the areas 1 and 2, DIABOLOS walks 1001 and 1008)",
              "SpawnMob(3001)|SpawnMob(3008)|SpawnMob(1001)|SpawnMob(1008)",
              $"{WdTick(1, "DIABOLOS_WD")}|{WdTick(2, "DIABOLOS_WD")}|{WdTick(1, "DIABOLOS")}|{WdTick(2, "DIABOLOS")}");
    }
}
