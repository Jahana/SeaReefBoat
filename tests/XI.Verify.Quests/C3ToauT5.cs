// XI.Verify.Quests -- quest rework C3 T5 (WotG missions 1-13 and the La Vaule [S] battlefield on C#): the arms the
// golden flows reach only together, each on the one read that decides it, and values the replay sees only in part (the
// rewards, the positions, the update operands, the items a trade is asked for). The traces hold the packets.
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    const int WotgLog = QC.MissionLog.Wotg;

    // a fake that records the items a trade is asked to match (the plain fake answers TradeResult whatever they are)
    sealed class W5Fake : QuestContextFake
    {
        public readonly List<string> Asked = new List<string>();

        public override bool Matches(params (int Item, int Qty)[] items)
        {
            Asked.Add(string.Join("+", items.Select(i => $"{i.Item}x{i.Qty}")));
            return TradeResult;
        }
    }

    // a fake on the WotG mission id, current, at the status given
    static W5Fake W5F(int id, int status = 0, bool mustZone = false)
    {
        var f = new W5Fake { ThisMission = (MissionLog.WoTG, id), MissionMustZone = mustZone };
        f.CurrentMissions[WotgLog] = id;
        f.MissionStatuses[WotgLog] = status;
        return f;
    }

    static string W5Rep(MissionScriptDefinition d, int state, QuestHandlerKind kind, ushort zone, string entity, int id,
        W5Fake f)
    {
        var h = C2MHandler(d, state, kind, zone, entity, id);
        var r = h.Fixed ?? h.Reply(f);
        return r == null ? "null" : QuestContextFake.Describe(r);
    }

    static string W5Run(MissionScriptDefinition d, int state, QuestHandlerKind kind, ushort zone, int id, W5Fake f,
        int option = 0)
    {
        f.Option = option;
        C2MHandler(d, state, kind, zone, null, id).Run(f);
        return string.Join(",", f.Calls);
    }

    static string W5Trig(MissionScriptDefinition d, int state, ushort zone, string npc, W5Fake f) =>
        W5Rep(d, state, QuestHandlerKind.OnTrigger, zone, npc, 1, f);

    static string W5ZoneIn(MissionScriptDefinition d, int state, ushort zone, W5Fake f) =>
        W5Rep(d, state, QuestHandlerKind.OnZoneIn, zone, null, 1, f);

    static string W5Fin(MissionScriptDefinition d, int state, ushort zone, int csid, W5Fake f, int option = 0) =>
        W5Run(d, state, QuestHandlerKind.OnEventFinish, zone, csid, f, option);

    static string W5Upd(MissionScriptDefinition d, int state, ushort zone, int csid, W5Fake f, int option = 0) =>
        W5Run(d, state, QuestHandlerKind.OnEventUpdate, zone, csid, f, option);

    static string W5Reward(MissionScriptDefinition d) =>
        $"{d.Reward.KeyItem?.ToString() ?? "-"}/{d.Reward.Title?.ToString() ?? "-"}/{d.Reward.NextLog}/" +
        $"{d.Reward.NextMission}";

    // utils.MAX_UINT32 - n
    static int W5Max(uint n) => unchecked((int)(uint.MaxValue - n));

    internal static void RunC3ToauT5()
    {
        Console.WriteLine("\n== c3 T5: WotG 1-13 and Purple, The New Black on C# ==");
        var all = new MissionScript[]
        {
            new QC.Missions.Wotg.CavernousMaws(), new QC.Missions.Wotg.BackToTheBeginning(),
            new QC.Missions.Wotg.CaitSith(), new QC.Missions.Wotg.TheQueenOfTheDance(),
            new QC.Missions.Wotg.WhileTheCatIsAway(), new QC.Missions.Wotg.ATimesweptButterfly(),
            new QC.Missions.Wotg.PurpleTheNewBlack(), new QC.Missions.Wotg.InTheNameOfTheFather(),
            new QC.Missions.Wotg.DancersInDistress(), new QC.Missions.Wotg.DaughterOfAKnight(),
            new QC.Missions.Wotg.ASpoonfulOfSugar(), new QC.Missions.Wotg.AffairsOfState(),
            new QC.Missions.Wotg.BorneByTheWind(),
        };
        string[] files =
        {
            "01_Cavernous_Maws", "02_Back_to_the_Beginning", "03_Cait_Sith", "04_The_Queen_of_the_Dance",
            "05_While_the_Cat_is_Away", "06_A_Timeswept_Butterfly", "07_Purple_The_New_Black",
            "08_In_the_Name_of_the_Father", "09_Dancers_in_Distress", "10_Daughter_of_a_Knight",
            "11_A_Spoonful_of_Sugar", "12_Affairs_of_State", "13_Borne_by_the_Wind",
        };
        Check("c3 T5 wotg: 13 missions: clean, each LegacyPath its Lua file's require name",
              string.Join(",", files.Select(f => $"|scripts/missions/wotg/{f}")),
              string.Join(",", all.Select(m => $"{string.Join(" | ", QuestScriptValidator.Validate(m.Compile()))}|" +
                  $"{m.LegacyPath}")));
        Check("c3 T5 wotg: the mission keys",
              "CAVERNOUS_MAWS,BACK_TO_THE_BEGINNING,CAIT_SITH,THE_QUEEN_OF_THE_DANCE,WHILE_THE_CAT_IS_AWAY," +
              "A_TIMESWEPT_BUTTERFLY,PURPLE_THE_NEW_BLACK,IN_THE_NAME_OF_THE_FATHER,DANCERS_IN_DISTRESS," +
              "DAUGHTER_OF_A_KNIGHT,A_SPOONFUL_OF_SUGAR,AFFAIRS_OF_STATE,BORNE_BY_THE_WIND",
              string.Join(",", all.Select(m => m.Key.Split('.').Last().ToUpperInvariant())));

        RunC3ToauT5Maws();
        RunC3ToauT5Queen();
        RunC3ToauT5Cat();
        RunC3ToauT5Purple();
        RunC3ToauT5Dancers();
        RunC3ToauT5Daughter();
        RunC3ToauT5Tail();
        RunC3ToauT5Rewards();
        RunC3ToauT5Battlefield();
    }

    static void RunC3ToauT5Maws()
    {
        // 1 Cavernous Maws: ENABLE_WOTG 1 and current; the finish completes, then (only then) the random maw
        var d1 = new QC.Missions.Wotg.CavernousMaws().Compile();
        int id1 = QC.Mission.Wotg.CavernousMaws;
        W5Fake M1(int? enable, bool current = true)
        {
            var f = W5F(id1);
            if (enable != null) f.MainSettings["ENABLE_WOTG"] = enable.Value;
            if (!current) f.CurrentMissions[WotgLog] = id1 + 1;
            return f;
        }
        Check("c3 T5 wotg 1: the section holds with ENABLE_WOTG 1 and current; setting 0, unset, not current each fail",
              "True False False False",
              $"{d1.States[0].IsMissionMatch(M1(1))} {d1.States[0].IsMissionMatch(M1(0))} " +
              $"{d1.States[0].IsMissionMatch(M1(null))} {d1.States[0].IsMissionMatch(M1(1, false))}");
        (ushort Zone, string Npc, int Operand)[] maws =
        {
            (QC.Zone.BatalliaDowns, QC.Npc.BatalliaDowns.CavernousMaw, 0),
            (QC.Zone.RolanberryFields, QC.Npc.RolanberryFields.CavernousMaw, 1),
            (QC.Zone.SauromugueChampaign, QC.Npc.SauromugueChampaign.CavernousMaw, 2),
        };
        Check("c3 T5 wotg 1: each maw plays the progress event 500 with its own operand 0, 1, 2",
              "ProgressEvent(500,0)|ProgressEvent(500,1)|" +
              "ProgressEvent(500,2)",
              string.Join("|", maws.Select(m => W5Trig(d1, 0, m.Zone, m.Npc, M1(1)))));
        var failed = M1(1);
        failed.CompleteResult = false;
        Check("c3 T5 wotg 1: finish 500 in each zone completes, then the random maw; not when the completion fails",
              string.Join("|", maws.Select(_ => "Mission.Complete(),Player.GotoRandomMaw()")) + "|Mission.Complete()",
              string.Join("|", maws.Select(m => W5Fin(d1, 0, m.Zone, 500, M1(1)))) + "|" +
              W5Fin(d1, 0, maws[0].Zone, 500, failed));

        // 2 Back to the Beginning: the guard is WotgMeetsMissionReqs(3) (the Lua's own number); six maws
        var d2 = new QC.Missions.Wotg.BackToTheBeginning().Compile();
        int id2 = QC.Mission.Wotg.BackToTheBeginning;
        W5Fake M2(params int[] reqs)
        {
            var f = W5F(id2);
            foreach (var r in reqs) f.WotgReqsMet.Add(r);
            return f;
        }
        Check("c3 T5 wotg 2: the section holds with the mission 3 requirements met; 2 and 4 alone fail, as does none",
              "True False False False",
              $"{d2.States[0].IsMissionMatch(M2(3))} {d2.States[0].IsMissionMatch(M2(2))} " +
              $"{d2.States[0].IsMissionMatch(M2(4))} {d2.States[0].IsMissionMatch(M2())}");
        (ushort Zone, string Npc, int Id, int Operand)[] six =
        {
            (QC.Zone.BatalliaDowns, QC.Npc.BatalliaDowns.CavernousMaw, 501, 0),
            (QC.Zone.RolanberryFields, QC.Npc.RolanberryFields.CavernousMaw, 501, 1),
            (QC.Zone.SauromugueChampaign, QC.Npc.SauromugueChampaign.CavernousMaw, 501, 2),
            (QC.Zone.BatalliaDownsS, QC.Npc.BatalliaDownsS.CavernousMaw, 701, 0),
            (QC.Zone.RolanberryFieldsS, QC.Npc.RolanberryFieldsS.CavernousMaw, 701, 1),
            (QC.Zone.SauromugueChampaignS, QC.Npc.SauromugueChampaignS.CavernousMaw, 701, 2),
        };
        Check("c3 T5 wotg 2: each of the six maws plays 501 or 701 with its operand 0, 1, 2 and 1",
              string.Join("|", six.Select(m => $"ProgressEvent({m.Id},{m.Operand},1)")),
              string.Join("|", six.Select(m => W5Trig(d2, 0, m.Zone, m.Npc, M2(3)))));
        var failed2 = M2(3);
        failed2.CompleteResult = false;
        Check("c3 T5 wotg 2: each finish completes, then adds the maw; not when the completion fails",
              string.Join("|", six.Select(_ => "Mission.Complete(),Player.AddMaw()")) + "|Mission.Complete()",
              string.Join("|", six.Select(m => W5Fin(d2, 0, m.Zone, m.Id, M2(3)))) + "|" +
              W5Fin(d2, 0, six[0].Zone, 501, failed2));
    }

    static void RunC3ToauT5Queen()
    {
        // 3 Cait Sith
        var d3 = new QC.Missions.Wotg.CaitSith().Compile();
        int id3 = QC.Mission.Wotg.CaitSith;
        var ss = QC.Zone.SouthernSanDoriaS;
        W5Fake C(int prev)
        {
            var f = W5F(id3);
            f.PrevZone = prev;
            return f;
        }
        Check("c3 T5 wotg 3: the zone-in to Southern San d'Oria [S] plays 67 from East Ronfaure [S] only",
              "Event(67)|null|null",
              W5ZoneIn(d3, 0, ss, C(QC.Zone.EastRonfaureS)) + "|" + W5ZoneIn(d3, 0, ss, C(QC.Zone.EastRonfaure)) + "|" +
              W5ZoneIn(d3, 0, ss, C(0)));
        W5Fake Rov(int rov)
        {
            var f = W5F(id3);
            f.CurrentMissions[QC.MissionLog.Rov] = rov;
            return f;
        }
        var failed = Rov(QC.Mission.Rov.Cauterize);
        failed.CompleteResult = false;
        Check("c3 T5 wotg 3: finish 67 gives the Lightsworm before completing when the RoV mission is Cauterize; " +
              "another RoV mission, or none, gives nothing; a failed completion still gives it",
              $"Player.GiveKeyItem({QC.KeyItem.Lightsworm}),Mission.Complete()|Mission.Complete()|" +
              $"Mission.Complete()|Player.GiveKeyItem({QC.KeyItem.Lightsworm}),Mission.Complete()",
              W5Fin(d3, 0, ss, 67, Rov(QC.Mission.Rov.Cauterize)) + "|" +
              W5Fin(d3, 0, ss, 67, Rov(QC.Mission.Rov.Cauterize + 1)) + "|" + W5Fin(d3, 0, ss, 67, W5F(id3)) + "|" +
              W5Fin(d3, 0, ss, 67, failed));

        // 4 The Queen of the Dance
        var d4 = new QC.Missions.Wotg.TheQueenOfTheDance().Compile();
        int id4 = QC.Mission.Wotg.TheQueenOfTheDance;
        var uj = QC.Zone.UpperJeuno;
        W5Fake Q(int status, bool reqs = false)
        {
            var f = W5F(id4, status);
            if (reqs) f.WotgReqsMet.Add(4);
            return f;
        }
        Check("c3 T5 wotg 4: section 0: status 0 and the mission 4 requirements; each alone fails; " +
              "section 1: status 1 only; section 2: status 2 and more, not 1",
              "True False False|True False True|False True True True True False",
              $"{d4.States[0].IsMissionMatch(Q(0, true))} {d4.States[0].IsMissionMatch(Q(0))} " +
              $"{d4.States[0].IsMissionMatch(Q(1, true))}|{d4.States[1].IsMissionMatch(Q(1))} " +
              $"{d4.States[1].IsMissionMatch(Q(0))} {d4.States[1].IsMissionMatch(Q(1, true))}|" +
              $"{d4.States[2].IsMissionMatch(Q(1))} {d4.States[2].IsMissionMatch(Q(2))} " +
              $"{d4.States[2].IsMissionMatch(Q(3))} {d4.States[2].IsMissionMatch(Q(4))} " +
              $"{d4.States[2].IsMissionMatch(Q(5))} {d4.States[2].IsMissionMatch(Q(0))}");
        Check("c3 T5 wotg 4: Lion Springs: status 0 plays 68 with its operands; status 1 plays 69; status 2 plays 70",
              $"ProgressEvent(68,80,4224267,1756,{W5Max(1540096)},{W5Max(1239549952)},427798150,3,4095)|" +
              "Event(69)|ProgressEvent(70,1,0,2964,0,66453367,8366690,4095,131140)",
              W5Trig(d4, 0, ss, QC.Npc.SouthernSanDoriaS.LionSprings, Q(0, true)) + "|" +
              W5Trig(d4, 1, ss, QC.Npc.SouthernSanDoriaS.LionSprings, Q(1)) + "|" +
              W5Trig(d4, 2, ss, QC.Npc.SouthernSanDoriaS.LionSprings, Q(2)));
        Check("c3 T5 wotg 4: Turlough plays the progress event 10172; its finish gives the ticket, then status 2; " +
              "finish 68 sets status 1",
              "ProgressEvent(10172)|" +
              $"Player.GiveKeyItem({QC.KeyItem.MayakovShowTicket}),Player.SetMissionStatus({WotgLog},2)|" +
              $"Player.SetMissionStatus({WotgLog},1)",
              W5Trig(d4, 1, uj, QC.Npc.UpperJeuno.Turlough, Q(1)) + "|" + W5Fin(d4, 1, uj, 10172, Q(1)) + "|" +
              W5Fin(d4, 0, ss, 68, Q(0, true)));
        Check("c3 T5 wotg 4: the zone-in plays 152 at status 3 and 153 at status 4, nothing at 2 and 5",
              "Event(152)|Event(153)|null|null",
              W5ZoneIn(d4, 2, ss, Q(3)) + "|" + W5ZoneIn(d4, 2, ss, Q(4)) + "|" + W5ZoneIn(d4, 2, ss, Q(2)) + "|" +
              W5ZoneIn(d4, 2, ss, Q(5)));
        Check("c3 T5 wotg 4: update 152 and 153 answer on option 1 only, each with its own operands",
              "Player.UpdateEvent(1,0,1756,0,66453367,8366690,4095,131140)||" +
              "Player.UpdateEvent(1,0,1756,0,66451386,11124900,4095,131140)|",
              W5Upd(d4, 2, ss, 152, Q(3), 1) + "|" + W5Upd(d4, 2, ss, 152, Q(3), 0) + "|" +
              W5Upd(d4, 2, ss, 153, Q(4), 1) + "|" + W5Upd(d4, 2, ss, 153, Q(4), 0));
        string pos = $"Player.SetPos(100.801,1,103.211,31,{ss})";
        Check("c3 T5 wotg 4: finish 70 sets status 3 and the position; 152 sets status 4 and the same position",
              $"Player.SetMissionStatus({WotgLog},3),{pos}|Player.SetMissionStatus({WotgLog},4),{pos}",
              W5Fin(d4, 2, ss, 70, Q(2)) + "|" + W5Fin(d4, 2, ss, 152, Q(3)));
        var failed4 = Q(4);
        failed4.CompleteResult = false;
        Check("c3 T5 wotg 4: finish 153 completes, then takes the ticket; not when the completion fails",
              $"Mission.Complete(),Player.DelKeyItem({QC.KeyItem.MayakovShowTicket})|Mission.Complete()",
              W5Fin(d4, 2, ss, 153, Q(4)) + "|" + W5Fin(d4, 2, ss, 153, failed4));
    }

    static void RunC3ToauT5Cat()
    {
        // 5 While the Cat Is Away
        var d5 = new QC.Missions.Wotg.WhileTheCatIsAway().Compile();
        int id5 = QC.Mission.Wotg.WhileTheCatIsAway;
        var er = QC.Zone.EastRonfaureS;
        string cw = XI.Quests.QuestLog.CrystalWar.ToString();
        W5Fake W(int prev, bool sandy = false, bool bastok = false, bool windy = false, int rov = 0)
        {
            var f = W5F(id5);
            f.PrevZone = prev;
            if (sandy) f.CompletedQuests.Add($"{cw}/BURDEN_OF_SUSPICION");
            if (bastok) f.CompletedQuests.Add($"{cw}/WRATH_OF_THE_GRIFFON");
            if (windy) f.CompletedQuests.Add($"{cw}/A_MANIFEST_PROBLEM");
            f.CurrentMissions[QC.MissionLog.Rov] = rov;
            return f;
        }
        Check("c3 T5 wotg 5: the zone-in to East Ronfaure [S] plays 7 from Southern San d'Oria [S] only",
              "Event(7)|null|null",
              W5ZoneIn(d5, 0, er, W(QC.Zone.SouthernSanDoriaS)) + "|" +
              W5ZoneIn(d5, 0, er, W(QC.Zone.SouthernSanDoria)) +
              "|" + W5ZoneIn(d5, 0, er, W(0)));
        string Flags(W5Fake f, int option = 0) => W5Upd(d5, 0, er, 7, f, option);
        Check("c3 T5 wotg 5: update 7 option 0: no flag set; each Crystal War quest alone sets its own flag, in the " +
              "order San d'Oria, Bastok, Windurst; the other quests do not count",
              "Player.UpdateEvent(0,0,0,0,0,7733257,0,0)|Player.UpdateEvent(1,0,0,0,0,7733257,0,0)|" +
              "Player.UpdateEvent(0,1,0,0,0,7733257,0,0)|Player.UpdateEvent(0,0,1,0,0,7733257,0,0)",
              Flags(W(0)) + "|" + Flags(W(0, sandy: true)) + "|" + Flags(W(0, bastok: true)) + "|" +
              Flags(W(0, windy: true)));
        Check("c3 T5 wotg 5: the RoV flag is 0 below Ganged Up On, 1 at it and above it; a flag set with the quests " +
              "all done; option 1 answers nothing",
              "Player.UpdateEvent(0,0,0,0,0,7733257,0,0)|Player.UpdateEvent(0,0,0,1,0,7733257,0,0)|" +
              "Player.UpdateEvent(0,0,0,1,0,7733257,0,0)|Player.UpdateEvent(1,1,1,1,0,7733257,0,0)|",
              Flags(W(0, rov: QC.Mission.Rov.GangedUpOn - 1)) + "|" + Flags(W(0, rov: QC.Mission.Rov.GangedUpOn)) +
              "|" + Flags(W(0, rov: QC.Mission.Rov.GangedUpOn + 1)) + "|" +
              Flags(W(0, true, true, true, QC.Mission.Rov.GangedUpOn)) + "|" + Flags(W(0), 1));
        Check("c3 T5 wotg 5: finish 7 completes", "Mission.Complete()", W5Fin(d5, 0, er, 7, W(0)));

        // 6 A Timeswept Butterfly
        var d6 = new QC.Missions.Wotg.ATimesweptButterfly().Compile();
        int id6 = QC.Mission.Wotg.ATimesweptButterfly;
        var lv = QC.Zone.LaVauleS;
        W5Fake B(int prev)
        {
            var f = W5F(id6);
            f.PrevZone = prev;
            return f;
        }
        Check("c3 T5 wotg 6: the zone-in to La Vaule [S] plays 1 from Jugner Forest [S] only; its finish completes",
              "Event(1)|null|null|Mission.Complete()",
              W5ZoneIn(d6, 0, lv, B(QC.Zone.JugnerForestS)) + "|" + W5ZoneIn(d6, 0, lv, B(QC.Zone.JugnerForest)) + "|" +
              W5ZoneIn(d6, 0, lv, B(0)) + "|" + W5Fin(d6, 0, lv, 1, B(0)));
    }

    static void RunC3ToauT5Purple()
    {
        // 7 Purple, The New Black
        var d7 = new QC.Missions.Wotg.PurpleTheNewBlack().Compile();
        int id7 = QC.Mission.Wotg.PurpleTheNewBlack;
        var lv = QC.Zone.LaVauleS;
        Check("c3 T5 wotg 7: section 0 holds at status 0 only; section 1 at status 1 and more, not 0",
              "True False False|False True True",
              $"{d7.States[0].IsMissionMatch(W5F(id7, 0))} {d7.States[0].IsMissionMatch(W5F(id7, 1))} " +
              $"{d7.States[0].IsMissionMatch(W5F(id7, 2))}|{d7.States[1].IsMissionMatch(W5F(id7, 0))} " +
              $"{d7.States[1].IsMissionMatch(W5F(id7, 1))} {d7.States[1].IsMissionMatch(W5F(id7, 2))}");
        Check("c3 T5 wotg 7: the entry NPC plays the progress event 2 with its operands; its finish sets status 1",
              $"ProgressEvent(2,85,3456,{W5Max(207616)},1525,0,19550816,0,0)|Player.SetMissionStatus({WotgLog},1)",
              W5Trig(d7, 0, lv, QC.Npc.LaVauleS._2d1, W5F(id7, 0)) + "|" + W5Fin(d7, 0, lv, 2, W5F(id7, 0)));
        Check("c3 T5 wotg 7: the zone-in plays 6 at status 2 only",
              "Event(6)|null|null",
              W5ZoneIn(d7, 1, lv, W5F(id7, 2)) + "|" + W5ZoneIn(d7, 1, lv, W5F(id7, 1)) + "|" +
              W5ZoneIn(d7, 1, lv, W5F(id7, 3)));
        W5Fake Won(int win)
        {
            var f = W5F(id7, 1);
            f.PlayerLocalVars["battlefieldWin"] = win;
            return f;
        }
        Check("c3 T5 wotg 7: finish 32001 with the battlefield won sets status 2 and the position; another " +
              "battlefield's win, or none, does nothing",
              $"Player.SetMissionStatus({WotgLog},2),Player.SetPos(-260.44,0.134,-156.652,192,{lv})||",
              W5Fin(d7, 1, lv, 32001, Won(QC.Battlefield.PurpleTheNewBlack)) + "|" +
              W5Fin(d7, 1, lv, 32001, Won(QC.Battlefield.PurpleTheNewBlack + 1)) + "|" +
              W5Fin(d7, 1, lv, 32001, Won(0)));
        Check("c3 T5 wotg 7: finish 6 completes", "Mission.Complete()", W5Fin(d7, 1, lv, 6, W5F(id7, 2)));

        // 8 In the Name of the Father
        var d8 = new QC.Missions.Wotg.InTheNameOfTheFather().Compile();
        int id8 = QC.Mission.Wotg.InTheNameOfTheFather;
        var ss = QC.Zone.SouthernSanDoriaS;
        W5Fake F8(params int[] reqs)
        {
            var f = W5F(id8);
            foreach (var r in reqs) f.WotgReqsMet.Add(r);
            f.Allegiance = 2;
            return f;
        }
        Check("c3 T5 wotg 8: the section holds with the mission 8 requirements met; 7 and 9 alone, none fail",
              "True False False False",
              $"{d8.States[0].IsMissionMatch(F8(8))} {d8.States[0].IsMissionMatch(F8(7))} " +
              $"{d8.States[0].IsMissionMatch(F8(9))} {d8.States[0].IsMissionMatch(F8())}");
        Check("c3 T5 wotg 8: Lion Springs plays the progress event 85 with the Campaign allegiance first; finish " +
              "completes",
              "ProgressEvent(85,2,13,0,1,3,4,8,3)|Mission.Complete()",
              W5Trig(d8, 0, ss, QC.Npc.SouthernSanDoriaS.LionSprings, F8(8)) + "|" +
              W5Fin(d8, 0, ss, 85, F8(8)));
    }
}
