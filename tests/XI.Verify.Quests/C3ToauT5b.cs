// XI.Verify.Quests -- quest rework C3 T5, second half: WotG 9-13 (the quiz item and its nil, the Cernunnos reads, the
// WindurstFirst var), the rewards of all thirteen missions and the La Vaule [S] battlefield.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static string W5Msg(ZoneText t, params int[] args) =>
        $"MessageSpecial({t.Zone}:{t.Name}" + string.Concat(args.Select(a => "," + a)) + ")";

    static void RunC3ToauT5Dancers()
    {
        // 9 Dancers in Distress
        var d9 = new QC.Missions.Wotg.DancersInDistress().Compile();
        int id9 = QC.Mission.Wotg.DancersInDistress;
        var ss = QC.Zone.SouthernSanDoriaS;
        var jf = QC.Zone.JugnerForestS;
        W5Fake Allied(W5Fake f)
        {
            f.Allegiance = 2;
            return f;
        }
        var foot = QC.Npc.JugnerForestS.ElegantFootprints;
        Check("c3 T5 wotg 9: Lion Springs sends CONCERNED_FOR_WOUNDED; Raustigne plays 86 with the allegiance first; " +
              "finish 86 sets status 1",
              W5Msg(QC.Text.SouthernSanDoriaS.CONCERNED_FOR_WOUNDED) + "|ProgressEvent(86,2,10,1,1,3,4,8,3)|" +
              $"Player.SetMissionStatus({WotgLog},1)",
              W5Trig(d9, 0, ss, QC.Npc.SouthernSanDoriaS.LionSprings, W5F(id9, 0)) + "|" +
              W5Trig(d9, 0, ss, QC.Npc.SouthernSanDoriaS.Raustigne, Allied(W5F(id9, 0))) +
              "|" + W5Fin(d9, 0, ss, 86, W5F(id9, 0)));
        Check("c3 T5 wotg 9: Elegant Footprints plays the progress event 2; its finish sets status 2 and the " +
              "mustZone",
              "ProgressEvent(2)|" +
              $"Player.SetMissionStatus({WotgLog},2),Mission.SetMustZone()",
              W5Trig(d9, 1, jf, foot, W5F(id9, 1)) + "|" + W5Fin(d9, 1, jf, 2, W5F(id9, 1)));
        W5Fake Roll(int roll)
        {
            var f = W5F(id9, 1);
            f.Rolls.Enqueue(roll);
            return f;
        }
        Check("c3 T5 wotg 9: update 2 option 19 (the quiz's end) rolls 1 to 3, keeps it in the Option var and " +
              "answers with it last; option 18 and 20 do nothing",
              "Player.RandomInt(1,3),Mission.SetVar(Option,3),Player.UpdateEvent(0,0,0,0,0,0,0,3)||",
              W5Upd(d9, 1, jf, 2, Roll(3), 19) + "|" + W5Upd(d9, 1, jf, 2, Roll(3), 18) + "|" +
              W5Upd(d9, 1, jf, 2, Roll(3), 20));
        W5Fake T(int option, bool mustZone = false, bool match = true)
        {
            var f = W5F(id9, 2, mustZone);
            f.MissionVars[$"{WotgLog}/{id9}/Option"] = option;
            f.TradeResult = match;
            return f;
        }
        string Trade(W5Fake f) =>
            W5Rep(d9, 2, QuestHandlerKind.OnTrade, jf, foot, 1, f) + "@" + string.Join("+", f.Asked);
        // a lookup that throws counts as a wrong answer ("throws"), so a mutant that breaks the bounds fails a check
        string Safe(Func<string> g)
        {
            try { return g(); }
            catch (Exception) { return "throws"; }
        }
        Check("c3 T5 wotg 9: the trade plays 3 with the chosen index last, asking for 1 of the item at that index " +
              "(Lynx Meat, Gold Beastcoin, Nyumomo Doll)",
              $"ProgressEvent(3,0,0,0,0,0,0,0,1)@{QC.Item.LynxMeat}x1|ProgressEvent(3,0,0,0,0,0,0,0,2)@" +
              $"{QC.Item.GoldBeastcoin}x1|ProgressEvent(3,0,0,0,0,0,0,0,3)@{QC.Item.NyumomoDoll}x1",
              Trade(T(1)) + "|" + Trade(T(2)) + "|" + Trade(T(3)));
        Check("c3 T5 wotg 9: no reply when the mustZone alone holds, or the match alone fails; Option 0 and 4 look " +
              "up no item (a Lua nil: asked for item 0)",
              $"null@|null@{QC.Item.LynxMeat}x1|null@0x1|null@0x1",
              Trade(T(1, true)) + "|" + Trade(T(1, false, false)) + "|" + Safe(() => Trade(T(0, false, false))) + "|" +
              Safe(() => Trade(T(4, false, false))));
        Check("c3 T5 wotg 9: the trigger sends LILISETTE_IS_PREPARING with the chosen item; Option 0 sends it with " +
              "none",
              W5Msg(QC.Text.JugnerForestS.LILISETTE_IS_PREPARING, QC.Item.GoldBeastcoin) + "|" +
              W5Msg(QC.Text.JugnerForestS.LILISETTE_IS_PREPARING, 0),
              W5Trig(d9, 2, jf, foot, T(2)) + "|" + Safe(() => W5Trig(d9, 2, jf, foot, T(0))));
        var failed = W5F(id9, 2);
        failed.CompleteResult = false;
        Check("c3 T5 wotg 9: finish 3 completes, then completes the trade; not when the completion fails",
              "Mission.Complete(),Trade.Complete()|Mission.Complete()",
              W5Fin(d9, 2, jf, 3, W5F(id9, 2)) + "|" + W5Fin(d9, 2, jf, 3, failed));
    }

    static void RunC3ToauT5Daughter()
    {
        // 10 Daughter of a Knight
        var d = new QC.Missions.Wotg.DaughterOfAKnight().Compile();
        int id = QC.Mission.Wotg.DaughterOfAKnight;
        var sd = QC.Zone.SouthernSanDoria;
        var jfz = QC.Zone.JugnerForest;
        var jfs = QC.Zone.JugnerForestS;
        var amaura = QC.Npc.SouthernSanDoria.Amaura;
        var earth = QC.Npc.JugnerForest.HumusrichEarth;
        var earthS = QC.Npc.JugnerForestS.HumusrichEarth;
        string st(int n) => $"Player.SetMissionStatus({WotgLog},{n})";
        Check("c3 T5 wotg 10: seven sections, one per status 0 to 6, each holding at its own status only",
              "1000000|0100000|0010000|0001000|0000100|0000010|0000001",
              string.Join("|", Enumerable.Range(0, 7).Select(s =>
                  string.Concat(Enumerable.Range(0, 7).Select(n =>
                      d.States[s].IsMissionMatch(W5F(id, n)) ? "1" : "0")))));
        Check("c3 T5 wotg 10: status 0: Amaura plays the progress event 935 (0, 2); its finish sets status 1",
              "ProgressEvent(935,0,2)|" + st(1),
              W5Trig(d, 0, sd, amaura, W5F(id, 0)) + "|" + W5Fin(d, 0, sd, 935, W5F(id, 0)));
        W5Fake B(bool match, int option = 0)
        {
            var f = W5F(id, 1);
            f.TradeResult = match;
            f.MissionVars[$"{WotgLog}/{id}/Option"] = option;
            return f;
        }
        string Tr(MissionScriptDefinition def, int state, ushort z, string npc, W5Fake f) =>
            W5Rep(def, state, QuestHandlerKind.OnTrade, z, npc, 1, f) + "@" + string.Join("+", f.Asked);
        Check("c3 T5 wotg 10: status 1: the bulb trade plays 937 when it matches, asking for 1 Cernunnos Bulb; " +
              "no reply when it does not",
              $"ProgressEvent(937,0,2)@{QC.Item.CernunnosBulb}x1|null@{QC.Item.CernunnosBulb}x1",
              Tr(d, 1, sd, amaura, B(true)) + "|" + Tr(d, 1, sd, amaura, B(false)));
        Check("c3 T5 wotg 10: status 1: the trigger plays 936 while Option is 0, nothing at 1; 936 sets Option 1; " +
              "937 sets status 2 and takes no bulb",
              "Event(936)|null|Mission.SetVar(Option,1)|" + st(2),
              W5Trig(d, 1, sd, amaura, B(true, 0)) + "|" + W5Trig(d, 1, sd, amaura, B(true, 1)) + "|" +
              W5Fin(d, 1, sd, 936, B(true)) + "|" + W5Fin(d, 1, sd, 937, B(true)));
        W5Fake P(bool match)
        {
            var f = W5F(id, 2);
            f.TradeResult = match;
            return f;
        }
        string PlantCalls(W5Fake f) =>
            W5Rep(d, 2, QuestHandlerKind.OnTrade, jfs, earthS, 1, f) + "@" + string.Join(",", f.Calls);
        Check("c3 T5 wotg 10: status 2: the bulb trade to the earth completes it, sets status 3 and sends " +
              "YOU_PLANT_ITEM with the bulb; no match does nothing; the trigger sends IDEAL_PLACE_TO_PLANT_ITEM",
              W5Msg(QC.Text.JugnerForestS.YOU_PLANT_ITEM, QC.Item.CernunnosBulb) + $"@Trade.Complete(),{st(3)}|" +
              "null@|" + W5Msg(QC.Text.JugnerForestS.IDEAL_PLACE_TO_PLANT_ITEM, QC.Item.CernunnosBulb),
              PlantCalls(P(true)) + "|" + PlantCalls(P(false)) + "|" + W5Trig(d, 2, jfs, earthS, P(true)));
        W5Fake S3() => W5F(id, 3);
        Check("c3 T5 wotg 10: status 3: Amaura plays 938 once per zone; the earth in the [S] zone sends " +
              "ITEM_IS_PLANTED_HERE; the present earth plays the progress event 33; its finish sets status 4",
              "Event(938):oncePerZone()|" +
              W5Msg(QC.Text.JugnerForestS.ITEM_IS_PLANTED_HERE, QC.Item.CernunnosBulb) +
              "|ProgressEvent(33,104,0,0,53)|" + st(4),
              W5Trig(d, 3, sd, amaura, S3()) + "|" + W5Trig(d, 3, jfs, earthS, S3()) + "|" +
              W5Trig(d, 3, jfz, earth, S3()) + "|" + W5Fin(d, 3, jfz, 33, S3()));
        W5Fake S4(int defeated, bool spawned = false)
        {
            var f = W5F(id, 4);
            f.PlayerLocalVars["cernunnosDefeated"] = defeated;
            if (spawned) f.SpawnedMobs.Add($"{QC.Mob.JugnerForest.CERNUNNOS}+0");
            return f;
        }
        var spawnFake = S4(0);
        string spawned1 = W5Trig(d, 4, jfz, earth, spawnFake) + "@" + string.Join(",", spawnFake.Calls);
        Check("c3 T5 wotg 10: status 4: defeated 1 plays the progress event 34 (even with him spawned); defeated 0 " +
              "and not spawned spawns him on the char's claim and sends DRAWN_UNWANTED_ATTENTION; defeated 0 and " +
              "spawned sends SENSE_UNUSUAL_PRESENCE",
              "ProgressEvent(34,104,300,200,100,0,1648,0,0)|ProgressEvent(34,104,300,200,100,0,1648,0,0)|" +
              W5Msg(QC.Text.JugnerForest.DRAWN_UNWANTED_ATTENTION) +
              $"@Player.SpawnMob({QC.Mob.JugnerForest.CERNUNNOS},0,True)|" +
              W5Msg(QC.Text.JugnerForest.SENSE_UNUSUAL_PRESENCE),
              W5Trig(d, 4, jfz, earth, S4(1)) + "|" + W5Trig(d, 4, jfz, earth, S4(1, true)) + "|" + spawned1 + "|" +
              W5Trig(d, 4, jfz, earth, S4(0, true)));
        Check("c3 T5 wotg 10: finish 34 gives the Cernunnos Resin, clears the defeated local var and sets status 5",
              $"Player.GiveKeyItem({QC.KeyItem.CernunnosResin}),Player.SetLocalVar(cernunnosDefeated,0),{st(5)}",
              W5Fin(d, 4, jfz, 34, S4(1)));
        W5Fake S5() => W5F(id, 5);
        var f5 = S5();
        f5.UniqueDay = 40;
        Check("c3 T5 wotg 10: status 5: the earth sends DELIVER_TO_AMAURE; Amaura plays 939; its finish takes the " +
              "resin, sets status 6, the Timer to the next day and the mustZone",
              W5Msg(QC.Text.JugnerForest.DELIVER_TO_AMAURE) + "|ProgressEvent(939,0,0,0,31,0,0,1)|" +
              $"Player.DelKeyItem({QC.KeyItem.CernunnosResin}),{st(6)},Mission.SetVar(Timer,41),Mission.SetMustZone()",
              W5Trig(d, 5, jfz, earth, S5()) + "|" + W5Trig(d, 5, sd, amaura, S5()) + "|" +
              W5Fin(d, 5, sd, 939, f5));
        W5Fake S6(bool mustZone, int timer, int day, int amauraOption = 0)
        {
            var f = W5F(id, 6, mustZone);
            f.MissionVars[$"{WotgLog}/{id}/Timer"] = timer;
            f.UniqueDay = day;
            f.MissionLocalVars["amauraOption"] = amauraOption;
            return f;
        }
        Check("c3 T5 wotg 10: status 6: 941 when the char need not zone and the Timer is due (equal counts); each " +
              "part alone failing falls to 940 while amauraOption is 0; none once it is 1",
              "ProgressEvent(941)|ProgressEvent(941)|ProgressEvent(941)|" +
              "ProgressEvent(940)|ProgressEvent(940)|null|null|" +
              "ProgressEvent(941)",
              string.Join("|", new[]
              {
                  S6(false, 40, 41), S6(false, 41, 41), S6(false, 0, 0), S6(true, 40, 41), S6(false, 42, 41),
                  S6(true, 40, 41, 1), S6(false, 42, 41, 1), S6(false, 40, 41, 1),
              }.Select(f => W5Trig(d, 6, sd, amaura, f))));
        Check("c3 T5 wotg 10: finish 940 sets the amauraOption local var to 1 and the mustZone; 941 completes",
              "Mission.SetLocalVar(amauraOption,1),Mission.SetMustZone()|Mission.Complete()",
              W5Fin(d, 6, sd, 940, S6(false, 0, 0)) + "|" + W5Fin(d, 6, sd, 941, S6(false, 0, 0)));
    }

    static void RunC3ToauT5Tail()
    {
        var ss = QC.Zone.SouthernSanDoriaS;
        // 11 A Spoonful of Sugar
        var d11 = new QC.Missions.Wotg.ASpoonfulOfSugar().Compile();
        int id11 = QC.Mission.Wotg.ASpoonfulOfSugar;
        var f11 = W5F(id11);
        f11.Allegiance = 1;
        Check("c3 T5 wotg 11: Raustigne plays the progress event 87 with the allegiance first; finish completes",
              "ProgressEvent(87,1,7,0,0,0,0,3,4095)|Mission.Complete()",
              W5Trig(d11, 0, ss, QC.Npc.SouthernSanDoriaS.Raustigne, f11) + "|" + W5Fin(d11, 0, ss, 87, W5F(id11)));

        // 12 Affairs of State
        var d12 = new QC.Missions.Wotg.AffairsOfState().Compile();
        int id12 = QC.Mission.Wotg.AffairsOfState;
        var ww = QC.Zone.WindurstWatersS;
        var bm = QC.Zone.BastokMarketsS;
        var velda = QC.Npc.WindurstWatersS.VeldaGalda;
        var radford = QC.Npc.BastokMarketsS.Radford;
        string st(int n) => $"Player.SetMissionStatus({WotgLog},{n})";
        W5Fake A(int status, int windurstFirst = 0, int allegiance = 0)
        {
            var f = W5F(id12, status);
            f.MissionVars[$"{WotgLog}/{id12}/WindurstFirst"] = windurstFirst;
            f.Allegiance = allegiance;
            return f;
        }
        Check("c3 T5 wotg 12: two sections, status 0 and status 1, each at its own status only",
              "10|01|00",
              $"{(d12.States[0].IsMissionMatch(A(0)) ? 1 : 0)}{(d12.States[1].IsMissionMatch(A(0)) ? 1 : 0)}|" +
              $"{(d12.States[0].IsMissionMatch(A(1)) ? 1 : 0)}{(d12.States[1].IsMissionMatch(A(1)) ? 1 : 0)}|" +
              $"{(d12.States[0].IsMissionMatch(A(2)) ? 1 : 0)}{(d12.States[1].IsMissionMatch(A(2)) ? 1 : 0)}");
        Check("c3 T5 wotg 12: status 0: Velda-Galda plays 178 and its finish sets WindurstFirst 1 and status 1; " +
              "Radford plays 175 and its finish sets status 1 only (the kept Lua bug: WindurstFirst stays 0)",
              "ProgressEvent(178,3,2)|Mission.SetVar(WindurstFirst,1)," + st(1) +
              "|ProgressEvent(175,2,27,0,0,0,0,1,4095)|" + st(1),
              W5Trig(d12, 0, ww, velda, A(0)) + "|" + W5Fin(d12, 0, ww, 178, A(0)) + "|" +
              W5Trig(d12, 0, bm, radford, A(0)) + "|" + W5Fin(d12, 0, bm, 175, A(0)));
        Check("c3 T5 wotg 12: status 1: Velda-Galda plays the reminder 180 with WindurstFirst 1, else 179 with the " +
              "allegiance first; Radford plays the reminder 177 with WindurstFirst 0, else 176",
              "ProgressEvent(180,94,7)|ProgressEvent(179,2,0,1)|" +
              "ProgressEvent(177,87,55,0,0,0,0,1,4095)|ProgressEvent(176,3,75,1,0,0,0,4095,0)",
              W5Trig(d12, 1, ww, velda, A(1, 1, 2)) + "|" + W5Trig(d12, 1, ww, velda, A(1, 0, 2)) + "|" +
              W5Trig(d12, 1, bm, radford, A(1, 0)) + "|" + W5Trig(d12, 1, bm, radford, A(1, 1)));
        Check("c3 T5 wotg 12: finish 179 and 176 each complete",
              "Mission.Complete()|Mission.Complete()",
              W5Fin(d12, 1, ww, 179, A(1)) + "|" + W5Fin(d12, 1, bm, 176, A(1)));

        // 13 Borne by the Wind
        var d13 = new QC.Missions.Wotg.BorneByTheWind().Compile();
        int id13 = QC.Mission.Wotg.BorneByTheWind;
        var sc = QC.Zone.SauromugueChampaignS;
        Check("c3 T5 wotg 13: Radford plays the plain event 177 (87, 23); Velda-Galda 180 (94, 3); the Bulwark Gate " +
              "the progress event 5; its finish completes",
              "Event(177,87,23)|Event(180,94,3)|ProgressEvent(5,98,0,2963,0,0,0,1,0)|" +
              "Mission.Complete()",
              W5Trig(d13, 0, bm, radford, W5F(id13)) + "|" + W5Trig(d13, 0, ww, velda, W5F(id13)) + "|" +
              W5Trig(d13, 0, sc, QC.Npc.SauromugueChampaignS.BulwarkGate, W5F(id13)) + "|" +
              W5Fin(d13, 0, sc, 5, W5F(id13)));
        Check("c3 T5 wotg 13: the section holds at status 0 only",
              "True False",
              $"{d13.States[0].IsMissionMatch(W5F(id13, 0))} {d13.States[0].IsMissionMatch(W5F(id13, 1))}");
    }

    static void RunC3ToauT5Rewards()
    {
        var w = XI.Quests.MissionLog.WoTG;
        string R(MissionScript m) => W5Reward(m.Compile());
        string E(int? ki, int? title, int next) => $"{ki?.ToString() ?? "-"}/{title?.ToString() ?? "-"}/{w}/{next}";
        Check("c3 T5 wotg: the rewards: key item, title, next mission of each of the 13",
              string.Join("|", new[]
              {
                  E(QC.KeyItem.PureWhiteFeather, null, QC.Mission.Wotg.BackToTheBeginning),
                  E(QC.KeyItem.Lightsworm, null, QC.Mission.Wotg.CaitSith),
                  E(null, QC.Title.CaitSithsAssistant, QC.Mission.Wotg.TheQueenOfTheDance),
                  E(null, null, QC.Mission.Wotg.WhileTheCatIsAway),
                  E(null, null, QC.Mission.Wotg.ATimesweptButterfly),
                  E(null, null, QC.Mission.Wotg.PurpleTheNewBlack),
                  E(null, null, QC.Mission.Wotg.InTheNameOfTheFather),
                  E(null, null, QC.Mission.Wotg.DancersInDistress),
                  E(null, null, QC.Mission.Wotg.DaughterOfAKnight),
                  E(QC.KeyItem.BottleOfTreantTonic, null, QC.Mission.Wotg.ASpoonfulOfSugar),
                  E(null, null, QC.Mission.Wotg.AffairsOfState),
                  E(QC.KeyItem.CountBorelsLetter, null, QC.Mission.Wotg.BorneByTheWind),
                  E(QC.KeyItem.UnderpassHatchKey, null, QC.Mission.Wotg.ANationOnTheBrink),
              }),
              string.Join("|", new MissionScript[]
              {
                  new QC.Missions.Wotg.CavernousMaws(), new QC.Missions.Wotg.BackToTheBeginning(),
                  new QC.Missions.Wotg.CaitSith(), new QC.Missions.Wotg.TheQueenOfTheDance(),
                  new QC.Missions.Wotg.WhileTheCatIsAway(), new QC.Missions.Wotg.ATimesweptButterfly(),
                  new QC.Missions.Wotg.PurpleTheNewBlack(), new QC.Missions.Wotg.InTheNameOfTheFather(),
                  new QC.Missions.Wotg.DancersInDistress(), new QC.Missions.Wotg.DaughterOfAKnight(),
                  new QC.Missions.Wotg.ASpoonfulOfSugar(), new QC.Missions.Wotg.AffairsOfState(),
                  new QC.Missions.Wotg.BorneByTheWind(),
              }.Select(R)));
    }

    static void RunC3ToauT5Battlefield()
    {
        var s = new QC.Battlefields.LaVauleS.PurpleTheNewBlack();
        var d = s.Data;
        var m = (BattlefieldMissionData)d;
        Check("c3 T5 purple bf: validates clean, its key and LegacyPath",
              $"|{QC.BattlefieldKey.PurpleTheNewBlack}|scripts/battlefields/La_Vaule_[S]/purple_the_new_black",
              $"{string.Join(" | ", BattlefieldScriptValidator.Validate(s))}|{s.Key}|{s.LegacyPath}");
        Check("c3 T5 purple bf: the Lua file's fields (zone, id, no exp loss, mission, trusts, players, MAX_LEVEL, " +
              "30 minutes, index 1, entry and exit NPCs)",
              $"{QC.Zone.LaVauleS} {QC.Battlefield.PurpleTheNewBlack} False True True 6 MAX_LEVEL 1800 1 " +
              $"{QC.Npc.LaVauleS._2d1} {QC.Npc.LaVauleS._2d3},{QC.Npc.LaVauleS._2d5},{QC.Npc.LaVauleS._2d7}",
              $"{d.ZoneId} {d.BattlefieldId} {d.CanLoseExp} {d.IsMission} {d.AllowTrusts} {d.MaxPlayers} " +
              $"{d.LevelCapSetting} {d.TimeLimit} {d.Index} {string.Join(",", d.EntryNpcs)} " +
              string.Join(",", d.ExitNpcs));
        Check("c3 T5 purple bf: BattlefieldMission:new's fields (the WotG mission 7, the status from the WotG log, " +
              "required 1, skip defaulting to 1, no requiredVar)",
              $"WoTG {QC.Mission.Wotg.PurpleTheNewBlack} WoTG 1 1 - 0",
              $"{m.MissionArea} {m.Mission} {m.MissionStatusArea} {m.RequiredMissionStatus} {m.SkipStatus} " +
              $"{m.RequiredVar ?? "-"} {m.RequiredValue}");
        Check("c3 T5 purple bf: one group, one area per Galarhigg (the name, +1, +2), with its allDeath",
              "GALARHIGG|GALARHIGG+1|GALARHIGG+2|True|1",
              $"{string.Join("|", d.Groups[0].AreaMobIds.Select(a => string.Join(",", a)))}|" +
              $"{d.Groups[0].AllDeath != null}|{d.Groups.Count}");
        var bf = new BattlefieldViewFake { Id = QC.Battlefield.PurpleTheNewBlack };
        d.Groups[0].AllDeath(new BattlefieldContextFake(s) { Battlefield = bf }, null);
        Check("c3 T5 purple bf: the group's allDeath: the battlefield WON", "Battlefield.SetStatus(2)",
              string.Join(",", bf.Calls));
    }
}
