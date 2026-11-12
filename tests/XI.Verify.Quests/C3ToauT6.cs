// XI.Verify.Quests -- quest rework C3 T6 (WotG missions 14-27 on C#): the arms and values the golden flows cannot show
// (the set-down positions, the 10000 battlefield finishes, the isEngaged arm, the time and day boundaries, the
// signature formulas of Proof of Valor). The traces hold the packets.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;
using SN = XI.QuestContent.Npc.SouthernSanDoriaS;

internal static partial class Program
{
    static string W6Safe(Func<string> g)
    {
        try { return g(); }
        catch (Exception e) { return "throws " + e.Message; }
    }

    static string W6Key(int id, string name) => $"{WotgLog}/{id}/{name}";

    static string W6Pos(double x, double y, double z, int rot, int zone) =>
        $"Player.SetPos({x.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
        $"{y.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
        $"{z.ToString(System.Globalization.CultureInfo.InvariantCulture)},{rot},{zone})";

    internal static void RunC3ToauT6()
    {
        Console.WriteLine("\n== c3 T6: WotG 14-27 on C# ==");
        var all = new MissionScript[]
        {
            new QC.Missions.Wotg.ANationOnTheBrink(), new QC.Missions.Wotg.CrossroadsOfTime(),
            new QC.Missions.Wotg.SandsweptMemories(), new QC.Missions.Wotg.NorthlandExposure(),
            new QC.Missions.Wotg.TraitorInTheMidst(), new QC.Missions.Wotg.BetrayalAtBeaucedine(),
            new QC.Missions.Wotg.OnThinIce(), new QC.Missions.Wotg.ProofOfValor(),
            new QC.Missions.Wotg.ASanguinaryPrelude(), new QC.Missions.Wotg.DungeonsAndDancers(),
            new QC.Missions.Wotg.DistorterOfTime(), new QC.Missions.Wotg.TheWillOfTheWorld(),
            new QC.Missions.Wotg.FateInHaze(), new QC.Missions.Wotg.TheScentOfBattle(),
        };
        string[] files =
        {
            "14_A_Nation_on_the_Brink", "15_Crossroads_of_Time", "16_Sandswept_Memories", "17_Northland_Exposure",
            "18_Traitor_in_the_Midst", "19_Betrayal_at_Beaucedine", "20_On_Thin_Ice", "21_Proof_of_Valor",
            "22_A_Sanguinary_Prelude", "23_Dungeons_and_Dancers", "24_Distorter_of_Time", "25_The_Will_of_the_World",
            "26_Fate_in_Haze", "27_The_Scent_of_Battle",
        };
        Check("c3 T6 wotg: 14 missions: clean, each LegacyPath its Lua file's require name",
              string.Join(",", files.Select(f => $"|scripts/missions/wotg/{f}")),
              string.Join(",", all.Select(m => $"{string.Join(" | ", QuestScriptValidator.Validate(m.Compile()))}|" +
                  $"{m.LegacyPath}")));
        Check("c3 T6 wotg: the mission keys",
              "A_NATION_ON_THE_BRINK,CROSSROADS_OF_TIME,SANDSWEPT_MEMORIES,NORTHLAND_EXPOSURE,TRAITOR_IN_THE_MIDST," +
              "BETRAYAL_AT_BEAUCEDINE,ON_THIN_ICE,PROOF_OF_VALOR,A_SANGUINARY_PRELUDE,DUNGEONS_AND_DANCERS," +
              "DISTORTER_OF_TIME,THE_WILL_OF_THE_WORLD,FATE_IN_HAZE,THE_SCENT_OF_BATTLE",
              string.Join(",", all.Select(m => m.Key.Split('.').Last().ToUpperInvariant())));

        RunC3ToauT6Positions();
        RunC3ToauT6Time();
        RunC3ToauT6Proof();
        RunC3ToauT6Pins();
        RunC3ToauT6ReviewPins();
    }

    // the review round's survivors of the full mutant run (the 149 mutants the first run left out, see the T6 report)
    static void RunC3ToauT6ReviewPins()
    {
        var bd = QC.Zone.BatalliaDownsS;
        var ss = QC.Zone.SouthernSanDoriaS;

        // 14 and 26: the zone-in event updates (option 1) carry the campaign allegiance first; every flow has 0
        var d14 = new QC.Missions.Wotg.ANationOnTheBrink().Compile();
        int id14 = QC.Mission.Wotg.ANationOnTheBrink;
        var d26 = new QC.Missions.Wotg.FateInHaze().Compile();
        int id26 = QC.Mission.Wotg.FateInHaze;
        string Upd(MissionScriptDefinition d, int id, ushort zone, int csid)
        {
            var f = W5F(id);
            f.Allegiance = 2;
            return W6Safe(() => W5Upd(d, 0, zone, csid, f, 1));
        }
        Check("c3 T6 wotg 14: update 4 (option 1) sends the campaign allegiance first",
              "Player.UpdateEvent(2,0,1756,0,0,0,0,0)", Upd(d14, id14, bd, 4));
        Check("c3 T6 wotg 26: updates 154 and 155 (option 1) send the campaign allegiance first",
              "Player.UpdateEvent(2,0,1756)|Player.UpdateEvent(2,0,1756)",
              Upd(d26, id26, ss, 154) + "|" + Upd(d26, id26, ss, 155));

        // 14: section 1 (the _qm5 flag) holds only once the mission is completed
        W5Fake Done14(bool completed)
        {
            var f = W5F(id14);
            if (completed) f.CompletedMissions.Add($"{WotgLog}/{id14}");
            return f;
        }
        Check("c3 T6 wotg 14: section 1 holds with the mission completed, not without",
              "True False",
              W6Safe(() => $"{d14.States[1].IsMissionMatch(Done14(true))} "
                         + $"{d14.States[1].IsMissionMatch(Done14(false))}"));

        // 21: the Remind NPCs play their Remind event as a progress event and the next one as a plain event (both send
        // the same 032 packet, so no trace tells the kinds apart)
        var d21 = new QC.Missions.Wotg.ProofOfValor().Compile();
        int id21 = QC.Mission.Wotg.ProofOfValor;
        string Remind(string npc, int remindBit)
        {
            var f = W5F(id21);
            if (remindBit >= 0) f.MissionVars[W6Key(id21, "Remind")] = 1 << remindBit;
            return W6Safe(() => W5Trig(d21, 0, ss, npc, f));
        }
        Check("c3 T6 wotg 21: Sabiliont plays 129 as a progress event, then 139 as a plain event",
              "ProgressEvent(129)|Event(139)",
              Remind(SN.Sabiliont, -1) + "|" + Remind(SN.Sabiliont, 3));
    }

    static void RunC3ToauT6Positions()
    {
        var bd = QC.Zone.BatalliaDownsS;
        var bg = QC.Zone.BeaucedineGlacierS;
        var ss = QC.Zone.SouthernSanDoriaS;

        // 14: the set-downs of the finishes 4, 20, 21 and the Everbloom Hollow 10000
        var d14 = new QC.Missions.Wotg.ANationOnTheBrink().Compile();
        int id14 = QC.Mission.Wotg.ANationOnTheBrink;
        string down14 = W6Pos(399.999, 8.3, -269.999, 159, bd);
        Check("c3 T6 wotg 14: finishes 4, 20 and 21 set the status 3, 4, 5 and the char down at 399.999 8.3 -269.999",
              $"Player.SetMissionStatus({WotgLog},3),{down14}|Player.SetMissionStatus({WotgLog},4),{down14}|" +
              $"Player.SetMissionStatus({WotgLog},5),{down14}",
              string.Join("|", new[] { 4, 20, 21 }.Select(c => W5Fin(d14, 0, bd, c, W5F(id14)))));
        Check("c3 T6 wotg 14: Everbloom Hollow's 10000 sets the status 2 and the char down at 302.747 -1 -174.367",
              $"Player.SetMissionStatus({WotgLog},2),{W6Pos(302.747, -1, -174.367, 31, bd)}",
              W5Fin(d14, 0, QC.Zone.EverbloomHollow, 10000, W5F(id14)));

        // 19: the isEngaged arm of status 1 and the status 3 set-down
        var d19 = new QC.Missions.Wotg.BetrayalAtBeaucedine().Compile();
        int id19 = QC.Mission.Wotg.BetrayalAtBeaucedine;
        W5Fake B(bool spawned, bool engaged)
        {
            var f = W5F(id19);
            f.MissionVars[W6Key(id19, "Status")] = 1;
            f.MobIdsByName[QC.Npc.BeaucedineGlacierS.CountHalphas] = 17;
            if (spawned) f.SpawnedMobIds.Add(17);
            f.Engaged = engaged;
            return f;
        }
        string Halphas(W5Fake f) =>
            W5Trig(d19, 0, bg, QC.Npc.BeaucedineGlacierS.RegalPawprints2, f) + "@" + string.Join(",", f.Calls);
        Check("c3 T6 wotg 19: status 1 spawns the claimed Count Halphas once; spawned, engaged answers " +
              "NOW_IS_NOT_THE_TIME, " +
              "not engaged UNUSUAL_PRESENCE",
              "MessageSpecial(136:UNWANTED_ATTENTION)@Player.SpawnMobById(17,True)|" +
              "MessageSpecial(136:NOW_IS_NOT_THE_TIME)@|MessageSpecial(136:UNUSUAL_PRESENCE)@",
              W6Safe(() => Halphas(B(false, false))) + "|" + W6Safe(() => Halphas(B(true, true))) + "|" +
              W6Safe(() => Halphas(B(true, false))));
        Check("c3 T6 wotg 19: finish 16 sets the status 3 and the char down at -15.036 -40.355 -219.202",
              $"Mission.SetVar(Status,3),{W6Pos(-15.036, -40.355, -219.202, 189, bg)}",
              W5Fin(d19, 0, bg, 16, W5F(id19)));

        // 23 and 24: the 10000 finishes and the Timer boundary (Timer <= the day passes, one more fails)
        var d23 = new QC.Missions.Wotg.DungeonsAndDancers().Compile();
        int id23 = QC.Mission.Wotg.DungeonsAndDancers;
        Check("c3 T6 wotg 23: Everbloom Hollow's 10000 sets Status 2 and the char down at -148.078 -61.320 -176.608",
              $"Mission.SetVar(Status,2),{W6Pos(-148.078, -61.320, -176.608, 32, bg)}",
              W5Fin(d23, 0, QC.Zone.EverbloomHollow, 10000, W5F(id23)));
        W5Fake Day(int id, int timer)
        {
            var f = W5F(id);
            f.UniqueDay = 100;
            f.MissionVars[W6Key(id, "Timer")] = timer;
            return f;
        }
        var pp1 = QC.Npc.BeaucedineGlacierS.RegalPawprints1;
        Check("c3 T6 wotg 23: Regal Pawprints 1 answers with Timer 99 and 100 (the day), not 101",
              "True True False",
              string.Join(" ", new[] { 99, 100, 101 }.Select(t =>
                  (W5Trig(d23, 0, bg, pp1, Day(id23, t)) != "null").ToString())));
        var d24 = new QC.Missions.Wotg.DistorterOfTime().Compile();
        int id24 = QC.Mission.Wotg.DistorterOfTime;
        Check("c3 T6 wotg 24: Ruhotz Silvermines' 10000 sets Status 1 and the char down at 51.641 -41.230 98.680",
              $"Mission.SetVar(Status,1),{W6Pos(51.641, -41.230, 98.680, 0, bg)}",
              W5Fin(d24, 0, QC.Zone.RuhotzSilvermines, 10000, W5F(id24)));
        Check("c3 T6 wotg 24: Regal Pawprints 1 answers with Timer 99 and 100 (the day), not 101",
              "True True False",
              string.Join(" ", new[] { 99, 100, 101 }.Select(t =>
                  (W5Trig(d24, 0, bg, pp1, Day(id24, t)) != "null").ToString())));

        // 26: the set-downs of the finishes 151 and 154
        var d26 = new QC.Missions.Wotg.FateInHaze().Compile();
        int id26 = QC.Mission.Wotg.FateInHaze;
        string down26 = W6Pos(100.801, 1, 103.211, 31, ss);
        Check("c3 T6 wotg 26: finishes 151 and 154 set the Status 1 and 2 and the char down at 100.801 1 103.211",
              $"Mission.SetVar(Status,1),{down26}|Mission.SetVar(Status,2),{down26}",
              W5Fin(d26, 0, ss, 151, W5F(id26)) + "|" + W5Fin(d26, 0, ss, 154, W5F(id26)));
    }

    static void RunC3ToauT6Time()
    {
        // 18: the pawprint game's finish needs 20 s for pawprints 3 to 5 (the `<= 5` rule), none for 6 and 7
        var bg = QC.Zone.BeaucedineGlacierS;
        var d = new QC.Missions.Wotg.TraitorInTheMidst().Compile();
        int id = QC.Mission.Wotg.TraitorInTheMidst;
        string Fin(int n, int elapsed, int option)
        {
            var f = W5F(id);
            f.Now = 1000;
            f.Npc = new QuestNpcFake(0) { Name = $"Regal_Pawprints_{n}" };
            f.MissionLocalVars["Timer"] = 1000 - elapsed;
            f.Option = option;
            C2MHandler(d, 0, QuestHandlerKind.OnEventFinish, bg, null, n + 5).Run(f);
            return string.Join(",", f.Calls);
        }
        Check("c3 T6 wotg 18: pawprints 3 to 5 need 20 s (19 s fails, 20 s sets the bit); 6 and 7 need none",
              "Mission.SetLocalVar(Timer,0)|Mission.SetVarBit(Status,0),Mission.SetLocalVar(Timer,0)|" +
              "Mission.SetLocalVar(Timer,0)|Mission.SetVarBit(Status,2),Mission.SetLocalVar(Timer,0)|" +
              "Mission.SetVarBit(Status,3),Mission.SetLocalVar(Timer,0)|Mission.SetVarBit(Status,4)," +
              "Mission.SetLocalVar(Timer,0)",
              W6Safe(() => string.Join("|", Fin(3, 19, 1), Fin(3, 20, 1), Fin(5, 19, 1), Fin(5, 20, 1), Fin(6, 0, 1),
                  Fin(7, 0, 1))));
        Check("c3 T6 wotg 18: option 2 or 0 sets no bit, and clears the Timer",
              "Mission.SetLocalVar(Timer,0)|Mission.SetLocalVar(Timer,0)",
              W6Safe(() => Fin(4, 99, 2) + "|" + Fin(6, 99, 0)));
    }

    static void RunC3ToauT6Proof()
    {
        var ss = QC.Zone.SouthernSanDoriaS;
        var d = new QC.Missions.Wotg.ProofOfValor().Compile();
        int id = QC.Mission.Wotg.ProofOfValor;
        W5Fake P(int remindBit = -1)
        {
            var f = W5F(id);
            if (remindBit >= 0) f.MissionVars[W6Key(id, "Remind")] = 1 << remindBit;
            return f;
        }

        // Hauberliond: min(12, 8 + floor(n / 99)) for x99 .. x792 of the bolts
        string Hauberliond(int n, int remind = 2)
        {
            var f = P(remind);
            f.TradeItems[QC.Item.CrossbowBolt] = n;
            var r = W5Rep(d, 0, QuestHandlerKind.OnTrade, ss, QC.Npc.SouthernSanDoriaS.Hauberliond, 1, f);
            return r + "@" + (f.MissionLocalVars.TryGetValue("numSignatures", out var ns) ? ns : 0);
        }
        Check("c3 T6 wotg 21: Hauberliond's x98 plays nothing; x99, x198, x297, x396, x792 give 9, 10, 11, 12, 12",
              "null@0|ProgressEvent(134)@9|ProgressEvent(134)@10|ProgressEvent(134)@11|ProgressEvent(134)@12|" +
              "ProgressEvent(134)@12",
              W6Safe(() => string.Join("|", new[] { 98, 99, 198, 297, 396, 792 }.Select(n => Hauberliond(n)))));
        Check("c3 T6 wotg 21: Hauberliond's trade needs Remind bit 2 (bit 0 alone plays nothing)",
              "null@0",
              W6Safe(() => Hauberliond(99, 0)));

        // Sabiliont: min(12, 5 + floor(n / 99)) of the gysahl greens
        string Sabiliont(int n)
        {
            var f = P(3);
            f.TradeItems[QC.Item.BunchOfGysahlGreens] = n;
            var r = W5Rep(d, 0, QuestHandlerKind.OnTrade, ss, QC.Npc.SouthernSanDoriaS.Sabiliont, 1, f);
            return r + "@" + (f.MissionLocalVars.TryGetValue("numSignatures", out var ns) ? ns : 0);
        }
        Check("c3 T6 wotg 21: Sabiliont's x1 and x96 give 5; x99 6, x198 7, x792 12 (the cap), x1089 12",
              "ProgressEvent(133)@5|ProgressEvent(133)@5|ProgressEvent(133)@6|ProgressEvent(133)@7|" +
              "ProgressEvent(133)@12|ProgressEvent(133)@12",
              W6Safe(() => string.Join("|", new[] { 1, 96, 99, 198, 792, 1089 }.Select(Sabiliont))));

        // the game finishes read the CHARVAR numCorrect (the Lua's bug): the local var of the round is not read
        string Game(int csid, int bit, int local, int charvar)
        {
            var f = P();
            f.MissionLocalVars["numCorrect"] = local;
            f.MissionVars[W6Key(id, "numCorrect")] = charvar;
            f.Option = 1;
            C2MHandler(d, 0, QuestHandlerKind.OnEventFinish, ss, null, csid).Run(f);
            return string.Join(",", f.Calls);
        }
        Check("c3 T6 wotg 21: a game's finish adds the charvar numCorrect (0 here), not the local var 3",
              "Mission.SetVarBit(Status,10),Mission.IncrementVar(Option,0),Text.Special(80:HAVE_GATHERED_SIGNATURE," +
              "1162,0)|" +
              "Mission.SetVarBit(Status,10),Mission.IncrementVar(Option,2),Text.Special(80:HAVE_GATHERED_SIGNATURE," +
              "1162,2)",
              W6Safe(() => Game(125, 10, 3, 0) + "|" + Game(125, 10, 3, 2)));

        // the reward by Option, the Lua's pairs order: Oxblood 30, Foulard 41, Angel Skin from 81, up to 120
        string Reward(int option, bool bag = true)
        {
            var f = P();
            f.MissionVars[W6Key(id, "Option")] = option;
            f.GiveResult = bag;
            f.Option = 1;
            C2MHandler(d, 0, QuestHandlerKind.OnEventFinish, ss, null, 148).Run(f);
            return string.Join(",", f.Calls);
        }
        Check("c3 T6 wotg 21: the reward at Option 20, 30, 41, 81, 100, 120 (Oxblood, Foulard, then the Angel Skin " +
              "Orb)",
              $"Mission.Complete()|Player.GiveItem({QC.Item.OxbloodOrb}),Mission.Complete()|" +
              $"Player.GiveItem({QC.Item.SquareOfFoulard}),Mission.Complete()|" +
              $"Player.GiveItem({QC.Item.AngelSkinOrb}),Mission.Complete()|" +
              $"Player.GiveItem({QC.Item.AngelSkinOrb}),Mission.Complete()|" +
              $"Player.GiveItem({QC.Item.AngelSkinOrb}),Mission.Complete()",
              W6Safe(() => string.Join("|", new[] { 20, 30, 41, 81, 100, 120 }.Select(o => Reward(o)))));
        Check("c3 T6 wotg 21: a full bag gives the item and does not complete the mission",
              $"Player.GiveItem({QC.Item.AngelSkinOrb})",
              W6Safe(() => Reward(120, false)));
    }

    // the values and bits no recorded flow tells apart (the survivors of the mutant runs, see the T6 report)
    static void RunC3ToauT6Pins()
    {
        var ss = QC.Zone.SouthernSanDoriaS;

        // 16, 20, 25: the campaign allegiance is the first operand of the progress event
        string Ally(MissionScript m, int id, string npc)
        {
            var f = W5F(id);
            f.Allegiance = 2;
            return W5Trig(m.Compile(), 0, ss, npc, f);
        }
        Check("c3 T6 wotg 16: Lion Springs plays 146 with the campaign allegiance first",
              "ProgressEvent(146,2,0,0,0,0,0,0,4095)",
              W6Safe(() => Ally(new QC.Missions.Wotg.SandsweptMemories(), QC.Mission.Wotg.SandsweptMemories,
                  SN.LionSprings)));
        Check("c3 T6 wotg 20: Raustigne plays 147 with the campaign allegiance first",
              "ProgressEvent(147,2,16,1)",
              W6Safe(() => Ally(new QC.Missions.Wotg.OnThinIce(), QC.Mission.Wotg.OnThinIce, SN.Raustigne)));
        Check("c3 T6 wotg 25: Raustigne plays 149 with the campaign allegiance first",
              "ProgressEvent(149,2,5,2964,0,0,0,1,4095)",
              W6Safe(() => Ally(new QC.Missions.Wotg.TheWillOfTheWorld(), QC.Mission.Wotg.TheWillOfTheWorld,
                  SN.Raustigne)));

        var d = new QC.Missions.Wotg.ProofOfValor().Compile();
        int id = QC.Mission.Wotg.ProofOfValor;

        // Raustigne: under 20 signatures the message names the count (once per zone), from 20 the event 148 opens
        string Raustigne(int option)
        {
            var f = W5F(id);
            f.Allegiance = 2;
            f.MissionVars[W6Key(id, "Option")] = option;
            return W5Trig(d, 0, ss, SN.Raustigne, f) + "@" + string.Join(",", f.Calls);
        }
        string below = W6Safe(() => Raustigne(19));
        Check("c3 T6 wotg 21: Raustigne at Option 19 sets mustZone and names the count 19 (the petition key item)",
              "True", (below.Contains("CURRENT_PETITIONS") && below.Contains(",19,") &&
                  below.Contains("Mission.SetMustZone")).ToString());
        Check("c3 T6 wotg 21: Raustigne at Option 20 plays 148 with the allegiance and the count",
              "ProgressEvent(148,2,20)@", W6Safe(() => Raustigne(20)));

        // each petition and game NPC answers with its own Status bit clear and only then
        (string Npc, int Bit)[] bits =
        {
            (SN.Aissaville, 0), (SN.Illeuse, 1), (SN.Andagge, 2), (SN.Daigraffeaux, 3), (SN.Louxiard, 4),
            (SN.Machionage, 5), (SN.Elnonde, 6), (SN.Loillie, 7), (SN.Remiotte, 8), (SN.Mailleronce, 9),
            (SN.Farouel, 10), (SN.Aurfet, 11), (SN.Corseihaut, 12), (SN.Vichauxdat, 13),
        };
        string Bit(string npc, int status)
        {
            var f = W5F(id);
            f.MissionVars[W6Key(id, "Status")] = status;
            return W5Trig(d, 0, ss, npc, f) == "null" ? "quiet" : "plays";
        }
        Check("c3 T6 wotg 21: each petition and game NPC is quiet with its own Status bit set and plays with only it " +
              "clear",
              string.Join(",", bits.Select(b => $"{b.Npc}=quiet/plays")),
              W6Safe(() => string.Join(",", bits.Select(b =>
                  $"{b.Npc}={Bit(b.Npc, 1 << b.Bit)}/{Bit(b.Npc, 0x7FFFF & ~(1 << b.Bit))}"))));

        // the first right answer of each game: it counts, a wrong one does not
        (int Csid, int First)[] games = { (125, 1), (126, 1), (127, 1), (128, 2) };
        string Upd(int csid, int option)
        {
            var f = W5F(id);
            f.MissionLocalVars["gameRound"] = 1;
            f.MissionLocalVars["numCorrect"] = 0;
            return W5Run(d, 0, QuestHandlerKind.OnEventUpdate, ss, csid, f, option);
        }
        Check("c3 T6 wotg 21: each game counts its first right answer and ignores the next number",
              string.Join("|", games.Select(g =>
                  "Mission.SetLocalVar(numCorrect,1),Mission.SetLocalVar(gameRound,2)/")),
              W6Safe(() => string.Join("|", games.Select(g => Upd(g.Csid, g.First) + "/" + Upd(g.Csid, g.First + 1)))));

        // the stack counts that split on the divisor 99 only above the first stack
        string Trade(string npc, int item, int n, int remind)
        {
            var f = W5F(id);
            f.MissionVars[W6Key(id, "Remind")] = 1 << remind;
            f.TradeItems[item] = n;
            W5Rep(d, 0, QuestHandlerKind.OnTrade, ss, npc, 1, f);
            return (f.MissionLocalVars.TryGetValue("numSignatures", out var ns) ? ns : 0).ToString();
        }
        Check("c3 T6 wotg 21: Hauberliond's x196 and x197 give 9 (one stack), Sabiliont's x98 gives 5 (no stack)",
              "9 9 5",
              W6Safe(() => $"{Trade(SN.Hauberliond, QC.Item.CrossbowBolt, 196, 2)} " +
                  $"{Trade(SN.Hauberliond, QC.Item.CrossbowBolt, 197, 2)} " +
                  $"{Trade(SN.Sabiliont, QC.Item.BunchOfGysahlGreens, 98, 3)}"));
    }
}
