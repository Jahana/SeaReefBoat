// XI.Verify.Quests -- quest rework C3 T1 (ToAU 1-13 on C#, and the SOA case of MissionAdjustments for ToAU 3, 5, 6, 7,
// 8):
// one fake check per handler with logic, the arms the golden flows reach one at a time and those they waive. The traces
// (mission.toau.*) hold the packets; these hold each guard, reply and finish on the reads it branches on.
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    const int ToauLog = QC.MissionLog.Toau;
    static readonly QuestHandlerKind T1Trig = QuestHandlerKind.OnTrigger;
    static readonly QuestHandlerKind T1Fin = QuestHandlerKind.OnEventFinish;
    static readonly QuestHandlerKind T1Upd = QuestHandlerKind.OnEventUpdate;
    static readonly QuestHandlerKind T1Area = QuestHandlerKind.OnTriggerAreaEnter;

    // a fake on the ToAU mission id, current, at the status given
    static Fake T1F(int id, int status = 0, bool mustZone = false, int merc = 3)
    {
        var f = new Fake { ThisMission = (MissionLog.ToAU, id), MercRank = merc, MissionMustZone = mustZone };
        f.CurrentMissions[ToauLog] = id;
        f.MissionStatuses[ToauLog] = status;
        return f;
    }

    static string T1Reply(MissionScriptDefinition d, int state, QuestHandlerKind kind, ushort zone, string entity,
                          int id, Fake f) => C2MReply(C2MHandler(d, state, kind, zone, entity, id), f);

    static string T1Run(MissionScriptDefinition d, int state, QuestHandlerKind kind, ushort zone, int id, Fake f) =>
        C2MRun(C2MHandler(d, state, QuestHandlerKind.OnEventFinish == kind ? kind : kind, zone, null, id), f);

    internal static void RunC3Toau()
    {
        Console.WriteLine("\n== c3 T1: ToAU 1-13 on C# ==");
        RunC3ToauKeys();
        RunC3Toau1To2();
        RunC3Toau3To5();
        RunC3Toau6To8();
        RunC3Toau9To13();
        RunC3ToauAdjustments();
    }

    static void RunC3ToauKeys()
    {
        var all = new MissionScript[]
        {
            new QC.Missions.Toau.LandOfSacredSerpents(), new QC.Missions.Toau.ImmortalSentries(),
            new QC.Missions.Toau.PresidentSalaheem(), new QC.Missions.Toau.KnightOfGold(),
            new QC.Missions.Toau.ConfessionsOfRoyalty(), new QC.Missions.Toau.EasterlyWinds(),
            new QC.Missions.Toau.WesterlyWinds(), new QC.Missions.Toau.AMercenaryLife(),
            new QC.Missions.Toau.UnderseaScouting(), new QC.Missions.Toau.AstralWaves(),
            new QC.Missions.Toau.ImperialSchemes(), new QC.Missions.Toau.RoyalPuppeteer(),
            new QC.Missions.Toau.LostKingdom(),
        };
        string[] names =
        {
            "land_of_sacred_serpents", "immortal_sentries", "president_salaheem", "knight_of_gold",
            "confessions_of_royalty", "easterly_winds", "westerly_winds", "a_mercenary_life", "undersea_scouting",
            "astral_waves", "imperial_schemes", "royal_puppeteer", "lost_kingdom",
        };
        string[] files =
        {
            "01_Land_of_Sacred_Serpents", "02_Immortal_Sentries", "03_President_Salaheem", "04_Knight_of_Gold",
            "05_Confessions_of_Royalty", "06_Easterly_Winds", "07_Westerly_Winds", "08_A_Mercenary_Life",
            "09_Undersea_Scouting", "10_Astral_Waves", "11_Imperial_Schemes", "12_Royal_Puppeteer", "13_Lost_Kingdom",
        };
        Check("c3 T1 toau: 13 missions: clean, keys mission.toau.<name>, LegacyPaths",
              string.Join(",", names.Select((n, i) => $"|mission.toau.{n}|scripts/missions/toau/{files[i]}")),
              string.Join(",",
                  all.Select(m => $"{string.Join(" | ", QuestScriptValidator.Validate(m.Compile()))}|{m.Key}|" +
                      $"{m.LegacyPath}")));
    }

    static void RunC3Toau1To2()
    {
        // 1 Land of Sacred Serpents
        var d1 = new QC.Missions.Toau.LandOfSacredSerpents().Compile();
        int id1 = QC.Mission.Toau.LandOfSacredSerpents;
        Fake F1(bool toau, bool permit)
        {
            var f = T1F(id1);
            f.MainSettings["ENABLE_TOAU"] = toau ? 1 : 0;
            if (permit) f.KeyItems[QC.KeyItem.BoardingPermit] = true;
            return f;
        }
        Check("c3 T1 toau 1: the section holds with ENABLE_TOAU 1 and the Boarding Permit, current; each alone fails",
              "True False False",
              $"{d1.States[0].IsMissionMatch(F1(true, true))} {d1.States[0].IsMissionMatch(F1(false, true))} " +
              $"{d1.States[0].IsMissionMatch(F1(true, false))}");
        Check("c3 T1 toau 1: trigger area 3 plays 3000 (a progress event, text table 0); its finish completes",
              "ProgressEvent(3000,{text_table=0})|Mission.Complete()",
              $"{T1Reply(d1, 0, T1Area, QC.Zone.AhtUrhganWhitegate, null, 3, F1(true, true))}|" +
              $"{T1Run(d1, 0, T1Fin, QC.Zone.AhtUrhganWhitegate, 3000, F1(true, true))}");

        // 2 Immortal Sentries
        var d2 = new QC.Missions.Toau.ImmortalSentries().Compile();
        int id2 = QC.Mission.Toau.ImmortalSentries;
        Fake F2(bool package, int option = 0)
        {
            var f = T1F(id2);
            f.Option = option;
            if (package) f.KeyItems[QC.KeyItem.SuppliesPackage] = true;
            return f;
        }
        var naja = QC.Npc.AhtUrhganWhitegate.NajaSalaheem;
        Check("c3 T1 toau 2: Naja: no package: 3002 (progress, text table 0); the package: 3001 (a plain event)",
              "ProgressEvent(3002,{text_table=0})|Event(3001,{text_table=0})",
              $"{T1Reply(d2, 0, T1Trig, QC.Zone.AhtUrhganWhitegate, naja, 1, F2(false))}|" +
              $"{T1Reply(d2, 0, T1Trig, QC.Zone.AhtUrhganWhitegate, naja, 1, F2(true))}");
        var f3002 = F2(false);
        Check("c3 T1 toau 2: 3002's finish: MEMBER_OF_SALAHEEMS_SENTINELS, complete, ACCESS_TO_A_MOG_LOCKER, in order",
              $"Text.Special({QC.Text.AhtUrhganWhitegate.MEMBER_OF_SALAHEEMS_SENTINELS}),Mission.Complete()," +
              $"Text.Special({QC.Text.AhtUrhganWhitegate.ACCESS_TO_A_MOG_LOCKER})",
              T1Run(d2, 0, T1Fin, QC.Zone.AhtUrhganWhitegate, 3002, f3002));
        var outposts = new (ushort Zone, string Npc, int With, int Without, bool Operand, string Label,
            ZoneText Special1, ZoneText Special2)[]
        {
            (QC.Zone.ArrapagoReef, QC.Npc.ArrapagoReef.Meyaada, 5, 6, false, "Meyaada",
                QC.Text.ArrapagoReef.HAND_OVER_TO_IMMORTAL, QC.Text.ArrapagoReef.YOUR_IMPERIAL_STANDING),
            (QC.Zone.BhaflauThickets, QC.Npc.BhaflauThickets.Daswil, 5, 6, false, "Daswil",
                QC.Text.BhaflauThickets.HAND_OVER_TO_IMMORTAL, QC.Text.BhaflauThickets.YOUR_IMPERIAL_STANDING),
            (QC.Zone.CaedarvaMire, QC.Npc.CaedarvaMire.Nahshib, 5, 6, false, "Nahshib",
                QC.Text.CaedarvaMire.HAND_OVER_TO_IMMORTAL, QC.Text.CaedarvaMire.YOUR_IMPERIAL_STANDING),
            (QC.Zone.CaedarvaMire, QC.Npc.CaedarvaMire.Nareema, 5, 6, true, "Nareema",
                QC.Text.CaedarvaMire.HAND_OVER_TO_IMMORTAL, QC.Text.CaedarvaMire.YOUR_IMPERIAL_STANDING),
            (QC.Zone.MountZhayolm, QC.Npc.MountZhayolm.Waudeen, 4, 5, false, "Waudeen",
                QC.Text.MountZhayolm.HAND_OVER_TO_IMMORTAL, QC.Text.MountZhayolm.YOUR_IMPERIAL_STANDING),
        };
        foreach (var o in outposts)
        {
            string tail = o.Operand ? ",1)" : ")";
            Check($"c3 T1 toau 2: {o.Label}: the package: {o.With}; none: {o.Without}" + (o.Operand ? " (second " +
            "operand 1)" : ""),
                  $"ProgressEvent({o.With}{tail.TrimEnd(')')})|ProgressEvent({o.Without}{tail.TrimEnd(')')})",
                  $"{T1Reply(d2, 0, T1Trig, o.Zone, o.Npc, 1, F2(true))}|" +
                  $"{T1Reply(d2, 0, T1Trig, o.Zone, o.Npc, 1, F2(false))}");
        }
        foreach (var zone in new[] { (QC.Zone.ArrapagoReef, 5, "Arrapago"), (QC.Zone.BhaflauThickets, 5, "Bhaflau"),
                                     (QC.Zone.CaedarvaMire, 5, "Caedarva"), (QC.Zone.MountZhayolm, 4, "Zhayolm") })
        {
            var s = outposts.First(o => o.Zone == zone.Item1);
            Check($"c3 T1 toau 2: {zone.Item3}'s handover finish: option 1 takes the package, gives 150 imperial " +
            $"standing " +
                  "and sends two messages; any other option does nothing",
                  $"Player.DelKeyItem({QC.KeyItem.SuppliesPackage}),Player.AddCurrency(imperial_standing,150)," +
                  $"Text.Special({s.Special1},{QC.KeyItem.SuppliesPackage}),Text.Special({s.Special2})|",
                  $"{T1Run(d2, 0, T1Fin, zone.Item1, zone.Item2, F2(true, 1))}|" +
                  $"{T1Run(d2, 0, T1Fin, zone.Item1, zone.Item2, F2(true, 0))}");
        }
    }

    static void RunC3Toau3To5()
    {
        var wg = QC.Zone.AhtUrhganWhitegate;
        var naja = QC.Npc.AhtUrhganWhitegate.NajaSalaheem;

        // 3 President Salaheem (the base handlers; the SOA module's replace them)
        var d3 = new QC.Missions.Toau.PresidentSalaheem().Compile();
        int id3 = QC.Mission.Toau.PresidentSalaheem;
        Check("c3 T1 toau 3: Naja: status 1 and no mustZone: 3020; status 1 and mustZone: 3003 (the rank as option " +
        "0); " +
              "other status: 73",
              "ProgressEvent(3020,{text_table=0})|Event(3003,{[0]=3,text_table=0})|ProgressEvent(73,{text_table=0})",
              $"{T1Reply(d3, 0, T1Trig, wg, naja, 1, T1F(id3, 1))}|" +
              $"{T1Reply(d3, 0, T1Trig, wg, naja, 1, T1F(id3, 1, mustZone: true))}|" +
              $"{T1Reply(d3, 0, T1Trig, wg, naja, 1, T1F(id3, 0))}");
        Check("c3 T1 toau 3: 73's finish sets status 1 and mustZone; 3020's finish completes",
              $"Player.SetMissionStatus({ToauLog},1),Mission.SetMustZone()|Mission.Complete()",
              $"{T1Run(d3, 0, T1Fin, wg, 73, T1F(id3))}|{T1Run(d3, 0, T1Fin, wg, 3020, T1F(id3, 1))}");

        // 4 Knight of Gold
        var d4 = new QC.Missions.Toau.KnightOfGold().Compile();
        int id4 = QC.Mission.Toau.KnightOfGold;
        var cac = QC.Npc.AhtUrhganWhitegate.Cacaroon;
        Check("c3 T1 toau 4: Cacaroon's trade: status 2 and a matching trade: 3022; wrong status or no match: nothing",
              "ProgressEvent(3022,{text_table=0})|null|null",
              $"{T1Reply(d4, 0, QuestHandlerKind.OnTrade, wg, cac, 1, T1TradeF(id4, 2, true))}|" +
              $"{T1Reply(d4, 0, QuestHandlerKind.OnTrade, wg, cac, 1, T1TradeF(id4, 1, true))}|" +
              $"{T1Reply(d4, 0, QuestHandlerKind.OnTrade, wg, cac, 1, T1TradeF(id4, 2, false))}");
        Check("c3 T1 toau 4: Cacaroon's trigger: status 0 or 1: 3035 with the status as operand 8 (progress); status " +
        "2: " +
              "3036; status 3 or more: 3023 (plain events, text table 0)",
              "ProgressEvent(3035,0,0,0,0,0,0,0,0,0)|ProgressEvent(3035,0,0,0,0,0,0,0,1,0)|" +
              "Event(3036,{text_table=0})|" +
              "Event(3023,{text_table=0})",
              string.Join("|", new[] { 0, 1, 2, 3 }.Select(s => T1Reply(d4, 0, T1Trig, wg, cac, 1, T1F(id4, s)))));
        var nadeey = QC.Npc.AhtUrhganWhitegate.Nadeey;
        Check("c3 T1 toau 4: Nadeey: status 3 or more: 3025; below: nothing",
              "null|Event(3025,{text_table=0})",
              $"{T1Reply(d4, 0, T1Trig, wg, nadeey, 1, T1F(id4, 2))}|" +
              $"{T1Reply(d4, 0, T1Trig, wg, nadeey, 1, T1F(id4, 3))}");
        Check("c3 T1 toau 4: Naja: 3021 with the mercenary rank, 1 second",
              "Event(3021,3,1,0,0,0,0,0,0,0)", T1Reply(d4, 0, T1Trig, wg, naja, 1, T1F(id4, 3)));
        Check("c3 T1 toau 4: trigger area 4 plays 3024 at status 3 only; area 5 plays 3026 at status 4 only",
              "ProgressEvent(3024,{text_table=0})|null|ProgressEvent(3026,0,0,0,0,0,0,0,0,0)|null",
              $"{T1Reply(d4, 0, T1Area, wg, null, 4, T1F(id4, 3))}|" +
              $"{T1Reply(d4, 0, T1Area, wg, null, 4, T1F(id4, 2))}|" +
              $"{T1Reply(d4, 0, T1Area, wg, null, 5, T1F(id4, 4))}|{T1Reply(d4, 0, T1Area, wg, null, 5, T1F(id4, 3))}");
        var fu = T1F(id4, 4);
        fu.Option = 10;
        string u10 = T1Run(d4, 0, T1Upd, wg, 3026, fu);
        fu.Option = 1;
        string u1 = T1Run(d4, 0, T1Upd, wg, 3026, fu);
        fu.Option = 3;
        string u3 = T1Run(d4, 0, T1Upd, wg, 3026, fu);
        Check("c3 T1 toau 4: 3026's update ors bit option-1 (clamped to 1..4) into the local var Option and answers " +
        "it " +
              "with 1: 10 clamps to bit 3 (8), then 1 adds bit 0, then 3 adds bit 2",
              "Mission.SetLocalVar(Option,8),Player.UpdateEvent(8,1)|" +
              "Mission.SetLocalVar(Option,8),Player.UpdateEvent(8,1)," +
              "Mission.SetLocalVar(Option,9),Player.UpdateEvent(9,1)|" +
              "Mission.SetLocalVar(Option,8),Player.UpdateEvent(8,1),Mission.SetLocalVar(Option,9)," +
              "Player.UpdateEvent(9,1),Mission.SetLocalVar(Option,13),Player.UpdateEvent(13,1)",
              $"{u10}|{u1}|{u3}");
        Check("c3 T1 toau 4: finishes: 3022 completes the trade and sets status 3; 3024 sets 4; 3026 completes; 3035 " +
              "sets status 1 on option 0, else 2",
              $"Trade.Complete(),Player.SetMissionStatus({ToauLog},3)|Player.SetMissionStatus({ToauLog},4)|" +
              $"Mission.Complete()|Player.SetMissionStatus({ToauLog},1)|Player.SetMissionStatus({ToauLog},2)",
              string.Join("|", new[]
              {
                  T1Run(d4, 0, T1Fin, wg, 3022, T1F(id4, 2)), T1Run(d4, 0, T1Fin, wg, 3024, T1F(id4, 3)),
                  T1Run(d4, 0, T1Fin, wg, 3026, T1F(id4, 4)), T1Run(d4, 0, T1Fin, wg, 3035, T1F(id4)),
                  T1Run(d4, 0, T1Fin, wg, 3035, T1OptionF(id4, 0, 1)),
              }));

        // 5 Confessions of Royalty
        var d5 = new QC.Missions.Toau.ConfessionsOfRoyalty().Compile();
        int id5 = QC.Mission.Toau.ConfessionsOfRoyalty;
        var ch = QC.Zone.ChateauDoraguille;
        var nsd = QC.Zone.NorthernSanDoria;
        Check("c3 T1 toau 5: the plain events: Cacaroon 3023, Nadeey 3025 (text table 0), Naja 3021 with the rank",
              "Event(3023,{text_table=0})|Event(3025,{text_table=0})|Event(3021,3,1,0,0,0,0,0,0,0)",
              $"{T1Reply(d5, 0, T1Trig, wg, cac, 1, T1F(id5))}|{T1Reply(d5, 0, T1Trig, wg, nadeey, 1, T1F(id5))}|" +
              $"{T1Reply(d5, 0, T1Trig, wg, naja, 1, T1F(id5))}");
        var fl = T1F(id5);
        fl.KeyItems[QC.KeyItem.RaillefalsLetter] = true;
        var halver = QC.Npc.ChateauDoraguille.Halver;
        Check("c3 T1 toau 5: Halver plays 564 with the letter, else nothing",
              "ProgressEvent(564)|null",
              $"{T1Reply(d5, 0, T1Trig, ch, halver, 1, fl)}|{T1Reply(d5, 0, T1Trig, ch, halver, 1, T1F(id5))}");
        var fz = T1F(id5);
        fz.MissionVars[$"{ToauLog}/{id5}/Option"] = 1;
        Check("c3 T1 toau 5: the Chateau zone-in plays 563 with the Option var 1, else nothing",
              "Event(563)|null",
              $"{C2MReply(C2MHandler(d5, 0, QuestHandlerKind.OnZoneIn, ch), fz)}|" +
              $"{C2MReply(C2MHandler(d5, 0, QuestHandlerKind.OnZoneIn, ch), T1F(id5))}");
        Check("c3 T1 toau 5: 563's finish takes the letter, completes and moves the char to Northern San d'Oria; " +
        "564's " +
              "finish with option 1 takes the letter and completes; with another option does nothing",
              $"Player.DelKeyItem({QC.KeyItem.RaillefalsLetter}),Mission.Complete(),Player.SetPos(0,0,100,64,{nsd})|" +
              $"Player.DelKeyItem({QC.KeyItem.RaillefalsLetter}),Mission.Complete()|",
              $"{T1Run(d5, 0, T1Fin, ch, 563, T1F(id5))}|{T1Run(d5, 0, T1Fin, ch, 564, T1OptionF(id5, 0, 1))}|" +
              $"{T1Run(d5, 0, T1Fin, ch, 564, T1OptionF(id5, 0, 0))}");
        var guil = QC.Npc.NorthernSanDoria.Guilerme;
        string Guilerme(int nation, int rank, int nationMission, bool letter)
        {
            var f = T1F(id5);
            f.NationId = nation;
            f.Ranks[nation] = rank;
            f.CurrentMissions[nation] = nationMission;
            if (letter) f.KeyItems[QC.KeyItem.RaillefalsLetter] = true;
            return T1Reply(d5, 0, T1Trig, nsd, guil, 1, f);
        }
        Check("c3 T1 toau 5: Guilerme plays 810 with the letter unless the char can enter the Chateau: rank 3 " +
        "anywhere, " +
              "San d'Orian rank 2, a Bastokan or Windurstian whose nation mission is 5 or more",
              "null|ProgressEvent(810)|null|ProgressEvent(810)|null|null|ProgressEvent(810)|null|ProgressEvent(810)",
              string.Join("|", new[]
              {
                  Guilerme(0, 1, 0, false), Guilerme(0, 1, 0, true), Guilerme(0, 3, 0, true), Guilerme(0, 1, 5, true),
                  Guilerme(0, 2, 0, true), Guilerme(1, 3, 0, true), Guilerme(1, 2, 4, true), Guilerme(1, 2, 5, true),
                  Guilerme(2, 2, 4, true),
              }));
        Check("c3 T1 toau 5: 810's finish with option 1 sets Option 1 and moves the char into the Chateau; another " +
              "option does nothing",
              $"Mission.SetVar(Option,1),Player.SetPos(0,0,-13,192,{ch})|",
              $"{T1Run(d5, 0, T1Fin, nsd, 810, T1OptionF(id5, 0, 1))}|" +
              $"{T1Run(d5, 0, T1Fin, nsd, 810, T1OptionF(id5, 0, 0))}");
    }

    // a fake with a trade (TradeResult as given) at the status
    static Fake T1TradeF(int id, int status, bool matches)
    {
        var f = T1F(id, status);
        f.TradeResult = matches;
        return f;
    }

    // a fake at the status and option
    static Fake T1OptionF(int id, int status, int option)
    {
        var f = T1F(id, status);
        f.Option = option;
        return f;
    }

    static void RunC3Toau6To8()
    {
        var wg = QC.Zone.AhtUrhganWhitegate;
        var naja = QC.Npc.AhtUrhganWhitegate.NajaSalaheem;
        var cac = QC.Npc.AhtUrhganWhitegate.Cacaroon;
        var nadeey = QC.Npc.AhtUrhganWhitegate.Nadeey;

        // 6 Easterly Winds
        var d6 = new QC.Missions.Toau.EasterlyWinds().Compile();
        int id6 = QC.Mission.Toau.EasterlyWinds;
        Check("c3 T1 toau 6: the plain events: Cacaroon 3023, Nadeey 3025, Naja 3021; Halver 565 (Chateau); Ru'Lude " +
              "trigger area 1 plays 10094 (progress)",
              "Event(3023,{text_table=0})|Event(3025,{text_table=0})|Event(3021,3,1,0,0,0,0,0,0,0)|Event(565)|" +
              "ProgressEvent(10094)",
              string.Join("|", new[]
              {
                  T1Reply(d6, 0, T1Trig, wg, cac, 1, T1F(id6)), T1Reply(d6, 0, T1Trig, wg, nadeey, 1, T1F(id6)),
                  T1Reply(d6, 0, T1Trig, wg, naja, 1, T1F(id6)),
                  T1Reply(d6, 0, T1Trig, QC.Zone.ChateauDoraguille, QC.Npc.ChateauDoraguille.Halver, 1, T1F(id6)),
                  T1Reply(d6, 0, T1Area, QC.Zone.RuludeGardens, null, 1, T1F(id6)),
              }));
        var full = T1OptionF(id6, 0, 1);
        full.GiveResult = false;
        Check("c3 T1 toau 6: 10094's finish: option 1 gives 10 imperial bronze pieces and completes only when they " +
        "fit; " +
              "another option completes without them",
              $"Player.GiveItems({QC.Item.ImperialBronzePiece}x10),Mission.Complete()|" +
              $"Player.GiveItems({QC.Item.ImperialBronzePiece}x10)|Mission.Complete()",
              $"{T1Run(d6, 0, T1Fin, QC.Zone.RuludeGardens, 10094, T1OptionF(id6, 0, 1))}|" +
              $"{T1Run(d6, 0, T1Fin, QC.Zone.RuludeGardens, 10094, full)}|" +
              $"{T1Run(d6, 0, T1Fin, QC.Zone.RuludeGardens, 10094, T1OptionF(id6, 0, 0))}");

        // 7 Westerly Winds
        var d7 = new QC.Missions.Toau.WesterlyWinds().Compile();
        int id7 = QC.Mission.Toau.WesterlyWinds;
        Check("c3 T1 toau 7: status 0: Cacaroon 3023, Nadeey 3025, Naja 3021, area 5 plays 3027; status 1: all " +
        "nothing " +
              "but Naja 3028 (progress)",
              "Event(3023,{text_table=0})|Event(3025,{text_table=0})|Event(3021,3,1,0,0,0,0,0,0,0)|" +
              "ProgressEvent(3027,{text_table=0})|null|null|ProgressEvent(3028,3,1,0,0,0,0,0,0,0)|null",
              string.Join("|", new[]
              {
                  T1Reply(d7, 0, T1Trig, wg, cac, 1, T1F(id7)), T1Reply(d7, 0, T1Trig, wg, nadeey, 1, T1F(id7)),
                  T1Reply(d7, 0, T1Trig, wg, naja, 1, T1F(id7)), T1Reply(d7, 0, T1Area, wg, null, 5, T1F(id7)),
                  T1Reply(d7, 0, T1Trig, wg, cac, 1, T1F(id7, 1)), T1Reply(d7, 0, T1Trig, wg, nadeey, 1, T1F(id7, 1)),
                  T1Reply(d7, 0, T1Trig, wg, naja, 1, T1F(id7, 1)), T1Reply(d7, 0, T1Area, wg, null, 5, T1F(id7, 1)),
              }));
        Check("c3 T1 toau 7: 3028's update: option 2 answers 0, any other 2 (second operand 1)",
              "Player.UpdateEvent(0,1,0,0,0,0,0,0,0)|Player.UpdateEvent(2,1,0,0,0,0,0,0,0)",
              $"{T1Run(d7, 0, T1Upd, wg, 3028, T1OptionF(id7, 1, 2))}|" +
              $"{T1Run(d7, 0, T1Upd, wg, 3028, T1OptionF(id7, 1, 1))}");
        var noSlot = T1F(id7);
        noSlot.FreeSlots = 0;
        Check("c3 T1 toau 7: 3027's finish with a free slot gives the note, the silver piece, the title and status " +
        "1; " +
              "with none does nothing",
              $"Player.GiveKeyItem({QC.KeyItem.RaillefalsNote}),Player.GiveItem({QC.Item.ImperialSilverPiece})," +
              $"Player.SetTitle({QC.Title.AgentOfTheAlliedForces}),Player.SetMissionStatus({ToauLog},1)|",
              $"{T1Run(d7, 0, T1Fin, wg, 3027, T1F(id7))}|{T1Run(d7, 0, T1Fin, wg, 3027, noSlot)}");
        var failed7 = T1F(id7, 1);
        failed7.CompleteResult = false;
        Check("c3 T1 toau 7: 3028's finish completes, then takes the note and sets A Mercenary Life's mustZone; a " +
        "false " +
              "complete leaves both",
              $"Mission.Complete(),Player.DelKeyItem({QC.KeyItem.RaillefalsNote})," +
              $"Player.SetMissionMustZone({ToauLog},{QC.Mission.Toau.AMercenaryLife})|Mission.Complete()",
              $"{T1Run(d7, 0, T1Fin, wg, 3028, T1F(id7, 1))}|{T1Run(d7, 0, T1Fin, wg, 3028, failed7)}");

        // 8 A Mercenary Life
        var d8 = new QC.Missions.Toau.AMercenaryLife().Compile();
        int id8 = QC.Mission.Toau.AMercenaryLife;
        Check("c3 T1 toau 8: section 2 holds while the mission is current and its mustZone clear",
              "True False",
              $"{d8.States[1].IsMissionMatch(T1F(id8))} {d8.States[1].IsMissionMatch(T1F(id8, mustZone: true))}");
        string Cycle(int option)
        {
            var f = T1F(id8);
            f.MissionVars[$"{ToauLog}/{id8}/Option"] = option;
            return T1Reply(d8, 0, T1Trig, wg, naja, 1, f);
        }
        Check("c3 T1 toau 8: Naja: Option 2 gives dialog 0, any other gives 2 (3029, a plain event)",
              "Event(3029,3,1,0,0,0,0,0,0,0)|Event(3029,3,1,0,0,0,0,0,2,0)|Event(3029,3,1,0,0,0,0,0,2,0)",
              $"{Cycle(2)}|{Cycle(0)}|{Cycle(1)}");
        var fo = T1F(id8);
        fo.MissionVars[$"{ToauLog}/{id8}/Option"] = 2;
        Check("c3 T1 toau 8: Naja sets the Option var to the dialog it plays",
              "Mission.SetVar(Option,0)",
              T1Reply(d8, 0, T1Trig, wg, naja, 1, fo) is var _ ? string.Join(",",
                  fo.Calls.Where(c => c.StartsWith("Mission.SetVar"))) : "");
        Check("c3 T1 toau 8: trigger area 3 plays 3050 (progress); its update ors bit option-1 into the local var " +
              "Dialog and answers it with 1; its finish completes",
              "ProgressEvent(3050,0,1,0,0,0,0,0,0,0)|" +
              "Mission.SetLocalVar(Dialog,1),Player.UpdateEvent(1,1,0,0,0,0,0,0)|" +
              "Mission.SetLocalVar(Dialog,2),Player.UpdateEvent(2,1,0,0,0,0,0,0)|" +
              "Mission.SetLocalVar(Dialog,-2147483648),Player.UpdateEvent(-2147483648,1,0,0,0,0,0,0)|Mission.Complete()",
              string.Join("|", new[]
              {
                  T1Reply(d8, 1, T1Area, wg, null, 3, T1F(id8)), T1Run(d8, 1, T1Upd, wg, 3050, T1OptionF(id8, 0, 1)),
                  T1Run(d8, 1, T1Upd, wg, 3050, T1OptionF(id8, 0, 2)), T1Run(d8, 1, T1Upd, wg, 3050, T1OptionF(id8,
                      0, 0)),
                  T1Run(d8, 1, T1Fin, wg, 3050, T1F(id8)),
              }));
        // the bit is ored (a topic read twice stays set) and reaches bit 31 with option 32
        var fd = T1OptionF(id8, 0, 1);
        fd.MissionLocalVars["Dialog"] = 1;
        var fb = T1OptionF(id8, 0, 32);
        Check("c3 T1 toau 8: 3050's update ors the bit: a read topic stays set; option 32 sets bit 31",
              "Mission.SetLocalVar(Dialog,1),Player.UpdateEvent(1,1,0,0,0,0,0,0)|" +
              "Mission.SetLocalVar(Dialog,-2147483648),Player.UpdateEvent(-2147483648,1,0,0,0,0,0,0)",
              $"{T1Run(d8, 1, T1Upd, wg, 3050, fd)}|{T1Run(d8, 1, T1Upd, wg, 3050, fb)}");
        // Cacaroon's trade needs exactly status 2 (a char past it is not offered the trade again)
        var d4t = new QC.Missions.Toau.KnightOfGold().Compile();
        var cacn = QC.Npc.AhtUrhganWhitegate.Cacaroon;
        int idk = QC.Mission.Toau.KnightOfGold;
        var trade = QuestHandlerKind.OnTrade;
        Check("c3 T1 toau 4: Cacaroon's trade at status 3 and 4: nothing",
              "null|null",
              $"{T1Reply(d4t, 0, trade, wg, cacn, 1, T1TradeF(idk, 3, true))}|" +
              $"{T1Reply(d4t, 0, trade, wg, cacn, 1, T1TradeF(idk, 4, true))}");
    }

    static void RunC3Toau9To13()
    {
        var wg = QC.Zone.AhtUrhganWhitegate;
        var naja = QC.Npc.AhtUrhganWhitegate.NajaSalaheem;

        // 9 Undersea Scouting
        var d9 = new QC.Missions.Toau.UnderseaScouting().Compile();
        int id9 = QC.Mission.Toau.UnderseaScouting;
        string Naja9(int option)
        {
            var f = T1F(id9);
            f.MissionVars[$"{ToauLog}/{id9}/Option"] = option;
            return T1Reply(d9, 0, T1Trig, wg, naja, 1, f);
        }
        Check("c3 T1 toau 9: Naja: Option 0 plays dialog 1, any other 0 (3051, a plain event)",
              "Event(3051,3,1,0,0,0,0,0,1,0)|Event(3051,3,1,0,0,0,0,0,0,0)|Event(3051,3,1,0,0,0,0,0,0,0)",
              $"{Naja9(0)}|{Naja9(1)}|{Naja9(2)}");
        Check("c3 T1 toau 9: Alzadaal trigger area 23 plays 1 (the rank, 0); its finish completes",
              "ProgressEvent(1,3,0)|Mission.Complete()",
              $"{T1Reply(d9, 0, T1Area, QC.Zone.AlzadaalUnderseaRuins, null, 23, T1F(id9))}|" +
              $"{T1Run(d9, 0, T1Fin, QC.Zone.AlzadaalUnderseaRuins, 1, T1F(id9))}");

        // 10 Astral Waves
        var d10 = new QC.Missions.Toau.AstralWaves().Compile();
        int id10 = QC.Mission.Toau.AstralWaves;
        Check("c3 T1 toau 10: Naja plays 3052 (progress, the rank, 1); its update answers option 1 only",
              "ProgressEvent(3052,3,1,0,0,0,0,0,0,0)|Player.UpdateEvent(3,1,0,0,0,0,0,0)|",
              $"{T1Reply(d10, 0, T1Trig, wg, naja, 1, T1F(id10))}|" +
              $"{T1Run(d10, 0, T1Upd, wg, 3052, T1OptionF(id10, 0, 1))}|" +
              $"{T1Run(d10, 0, T1Upd, wg, 3052, T1OptionF(id10, 0, 2))}");
        var f10 = T1F(id10);
        f10.UniqueDay = 500;
        var failed10 = T1F(id10);
        failed10.CompleteResult = false;
        Check("c3 T1 toau 10: 3052's finish completes, then sets Imperial Schemes' Timer to tomorrow and its " +
        "mustZone; " +
              "a false complete does neither",
              $"Mission.Complete(),Player.SetMissionVar({ToauLog},{QC.Mission.Toau.ImperialSchemes},Timer,501)," +
              $"Player.SetMissionMustZone({ToauLog},{QC.Mission.Toau.ImperialSchemes})|Mission.Complete()",
              $"{T1Run(d10, 0, T1Fin, wg, 3052, f10)}|{T1Run(d10, 0, T1Fin, wg, 3052, failed10)}");

        // 11 Imperial Schemes
        var d11 = new QC.Missions.Toau.ImperialSchemes().Compile();
        int id11 = QC.Mission.Toau.ImperialSchemes;
        string Naja11(int option)
        {
            var f = T1F(id11);
            f.MissionVars[$"{ToauLog}/{id11}/Option"] = option;
            return T1Reply(d11, 0, T1Trig, wg, naja, 1, f);
        }
        Check("c3 T1 toau 11: Naja: Option 2 gives dialog 0, any other 2 (3053, a plain event)",
              "Event(3053,3,1,0,0,0,0,0,0,0)|Event(3053,3,1,0,0,0,0,0,2,0)|Event(3053,3,1,0,0,0,0,0,2,0)",
              $"{Naja11(2)}|{Naja11(0)}|{Naja11(1)}");
        Fake A11(bool mustZone, int day, int timer)
        {
            var f = T1F(id11, mustZone: mustZone);
            f.UniqueDay = day;
            f.MissionVars[$"{ToauLog}/{id11}/Timer"] = timer;
            return f;
        }
        Check("c3 T1 toau 11: area 6 plays 3070 when mustZone is clear and the day has reached the Timer; not when " +
              "mustZone is set or the day is before it",
              "ProgressEvent(3070,{text_table=0})|ProgressEvent(3070,{text_table=0})|null|null",
              $"{T1Reply(d11, 0, T1Area, wg, null, 6, A11(false, 10, 10))}|" +
              $"{T1Reply(d11, 0, T1Area, wg, null, 6, A11(false, 11, 10))}|" +
              $"{T1Reply(d11, 0, T1Area, wg, null, 6, A11(true, 11, 10))}|" +
              $"{T1Reply(d11, 0, T1Area, wg, null, 6, A11(false, 9, 10))}");
        Check("c3 T1 toau 11: 3070's update answers option 2 with 4 as operand 8; its finish completes",
              "Player.UpdateEvent(0,0,0,0,0,0,0,4)||Mission.Complete()",
              $"{T1Run(d11, 0, T1Upd, wg, 3070, T1OptionF(id11, 0, 2))}|" +
              $"{T1Run(d11, 0, T1Upd, wg, 3070, T1OptionF(id11, 0, 1))}|" +
              $"{T1Run(d11, 0, T1Fin, wg, 3070, T1F(id11))}");

        // 12 Royal Puppeteer
        var d12 = new QC.Missions.Toau.RoyalPuppeteer().Compile();
        int id12 = QC.Mission.Toau.RoyalPuppeteer;
        var pyo = QC.Npc.Nashmau.Pyopyoroon;
        var nm = QC.Zone.Nashmau;
        Check("c3 T1 toau 12: Naja plays 3071 (a plain event)", "Event(3071,3,1,0,0,0,0,0,0,0)",
              T1Reply(d12, 0, T1Trig, wg, naja, 1, T1F(id12)));
        Check("c3 T1 toau 12: Pyopyoroon: status 0: 277 (progress); status 1: 278 once per zone; other: nothing",
              "ProgressEvent(277)|Event(278):oncePerZone()|null",
              string.Join("|", new[] { 0, 1, 2 }.Select(s => T1Reply(d12, 0, T1Trig, nm, pyo, 1, T1F(id12, s)))));
        Check("c3 T1 toau 12: Pyopyoroon's trade: status 1 and the vial: 279; wrong status or no match: nothing",
              "ProgressEvent(279)|null|null",
              $"{T1Reply(d12, 0, QuestHandlerKind.OnTrade, nm, pyo, 1, T1TradeF(id12, 1, true))}|" +
              $"{T1Reply(d12, 0, QuestHandlerKind.OnTrade, nm, pyo, 1, T1TradeF(id12, 0, true))}|" +
              $"{T1Reply(d12, 0, QuestHandlerKind.OnTrade, nm, pyo, 1, T1TradeF(id12, 1, false))}");
        var failed12 = T1F(id12, 1);
        failed12.CompleteResult = false;
        Check("c3 T1 toau 12: 277's finish sets status 1; 279's finish completes then completes the trade; a false " +
              "complete keeps the trade",
              $"Player.SetMissionStatus({ToauLog},1)|Mission.Complete(),Trade.Complete()|Mission.Complete()",
              $"{T1Run(d12, 0, T1Fin, nm, 277, T1F(id12))}|{T1Run(d12, 0, T1Fin, nm, 279, T1F(id12, 1))}|" +
              $"{T1Run(d12, 0, T1Fin, nm, 279, failed12)}");

        // 13 Lost Kingdom
        var d13 = new QC.Missions.Toau.LostKingdom().Compile();
        int id13 = QC.Mission.Toau.LostKingdom;
        var cm = QC.Zone.CaedarvaMire;
        var head = QC.Npc.CaedarvaMire.JazaraatsHeadstone;
        Check("c3 T1 toau 13: Naja 3071 and Pyopyoroon 275 (plain events)",
              "Event(3071,3,1,0,0,0,0,0,0,0)|Event(275)",
              $"{T1Reply(d13, 0, T1Trig, wg, naja, 1, T1F(id13))}|{T1Reply(d13, 0, T1Trig, nm, pyo, 1, T1F(id13))}");
        Fake H13(int status, bool vial = false, bool spawned = false)
        {
            var f = T1F(id13, status);
            if (vial) f.KeyItems[QC.KeyItem.VialOfSpectralScent] = true;
            if (spawned) f.SpawnedMobs.Add($"{QC.Mob.CaedarvaMire.JAZARAAT}+0");
            return f;
        }
        Check("c3 T1 toau 13: the headstone: status 0 with the vial: cutscene 8; status 0 without: nothing; status 1 " +
              "with Jazaraat out: nothing; status 2: cutscene 9",
              "ProgressCutscene(8)|null|null|ProgressCutscene(9)",
              $"{T1Reply(d13, 0, T1Trig, cm, head, 1, H13(0, vial: true))}|" +
              $"{T1Reply(d13, 0, T1Trig, cm, head, 1, H13(0))}|" +
              $"{T1Reply(d13, 0, T1Trig, cm, head, 1, H13(1, spawned: true))}|" +
              $"{T1Reply(d13, 0, T1Trig, cm, head, 1, H13(2))}");
        var f13 = H13(1);
        Check("c3 T1 toau 13: the headstone at status 1 with Jazaraat not spawned spawns him on the char's enmity " +
        "and " +
              "answers noAction",
              $"NoAction()|Player.SpawnMob({QC.Mob.CaedarvaMire.JAZARAAT},0,False)",
              $"{T1Reply(d13, 0, T1Trig, cm, head, 1, f13)}|{string.Join(",", f13.Calls)}");
        var jaz = QC.Npc.CaedarvaMire.Jazaraat;
        Check("c3 T1 toau 13: Jazaraat's death at status 1 sets status 2; at another status nothing",
              $"Player.SetMissionStatus({ToauLog},2)|",
              $"{C2MRun(C2MHandler(d13, 0, QuestHandlerKind.OnMobDeath, cm, jaz), T1F(id13, 1))}|" +
              $"{C2MRun(C2MHandler(d13, 0, QuestHandlerKind.OnMobDeath, cm, jaz), T1F(id13, 0))}");
        Check("c3 T1 toau 13: 8's finish sets status 1; 9's finish takes the vial and completes",
              $"Player.SetMissionStatus({ToauLog},1)|" +
              $"Player.DelKeyItem({QC.KeyItem.VialOfSpectralScent}),Mission.Complete()",
              $"{T1Run(d13, 0, T1Fin, cm, 8, T1F(id13))}|{T1Run(d13, 0, T1Fin, cm, 9, T1F(id13, 2))}");
        Fake Done13(bool completed, bool coin)
        {
            var f = T1F(id13);
            if (completed) f.CompletedMissions.Add($"{ToauLog}/{id13}");
            if (coin) f.KeyItems[QC.KeyItem.EphramadianGoldCoin] = true;
            return f;
        }
        Check("c3 T1 toau 13: section 2 holds once completed; the headstone gives the coin as a key item reply, else " +
              "JAZARAATS_HEADSTONE",
              $"True False|KeyItem({QC.KeyItem.EphramadianGoldCoin})|" +
              $"MessageSpecial({QC.Text.CaedarvaMire.JAZARAATS_HEADSTONE})",
              $"{d13.States[1].IsMissionMatch(Done13(true, false))} " +
              $"{d13.States[1].IsMissionMatch(Done13(false, false))}|" +
              $"{T1Reply(d13, 1, T1Trig, cm, head, 1, Done13(true, false))}|" +
              $"{T1Reply(d13, 1, T1Trig, cm, head, 1, Done13(true, true))}");
    }

    static void RunC3ToauAdjustments()
    {
        var wg = QC.Zone.AhtUrhganWhitegate;
        var naja = QC.Npc.AhtUrhganWhitegate.NajaSalaheem;
        var mod = new QC.Modules.Era.Missions.MissionAdjustments();
        Check("c3 T1 adjustments: the SOA and ROV cases patch ToAU 3, 5-8 and 17, 18, 24, 25, 32, 33, 37, 38 "
              + "(and, since C3 N5, San d'Oria 6-2; since C3 N6, San d'Oria 8-1) under xi.pre only",
              "|mission.toau.president_salaheem,mission.toau.confessions_of_royalty,mission.toau.easterly_winds," +
              "mission.toau.westerly_winds,mission.toau.a_mercenary_life,mission.sandoria.ranperres_final_rest," +
              "mission.sandoria.coming_of_age," +
              "mission.toau.guests_of_the_empire," +
              "mission.toau.passing_glory,mission.toau.foiled_ambition,mission.toau.playing_the_part," +
              "mission.toau.in_the_blood,mission.toau.sentinels_honor,mission.toau.path_of_blood," +
              "mission.toau.stirrings_of_war",
              $"{string.Join(",", mod.Compile(QuestModule.Branches[0].Settings).Keys)}|" +
              string.Join(",", mod.Compile(QuestModule.Branches[1].Settings).Keys));
        // the patched definitions
        MissionScriptDefinition Patched<T>(T mission, string key) where T : MissionScript
        {
            var d = mission.Compile();
            var r = QuestOverrides.Apply(d.Core, mod.Compile(QuestModule.Branches[1].Settings), key);
            return d.WithCore(r.Definition);
        }
        int id3 = QC.Mission.Toau.PresidentSalaheem;
        var p3 = Patched(new QC.Missions.Toau.PresidentSalaheem(), QC.MissionKey.Toau.PresidentSalaheem);
        Fake T3(int status, bool mustZone, int timer)
        {
            var f = T1F(id3, status, mustZone);
            f.MissionVars[$"{ToauLog}/{id3}/Timer"] = timer;
            return f;
        }
        Check("c3 T1 adjustments toau 3: Naja plays 3020 only with mustZone clear and the Timer 0; else 3003; other " +
              "status 73",
              "ProgressEvent(3020,{text_table=0})|Event(3003,{[0]=3,text_table=0})|Event(3003,{[0]=3,text_table=0})|" +
              "ProgressEvent(73,{text_table=0})",
              $"{T1Reply(p3, 0, T1Trig, wg, naja, 1, T3(1, false, 0))}|" +
              $"{T1Reply(p3, 0, T1Trig, wg, naja, 1, T3(1, true, 0))}|" +
              $"{T1Reply(p3, 0, T1Trig, wg, naja, 1, T3(1, false, 5))}|" +
              $"{T1Reply(p3, 0, T1Trig, wg, naja, 1, T3(0, false, 0))}");
        var f73 = T3(0, false, 0);
        f73.NextJstMidnight = 777;
        Check("c3 T1 adjustments toau 3: 73's finish sets status 1, mustZone and the Timer 1 expiring at JST midnight",
              $"Player.SetMissionStatus({ToauLog},1),Mission.SetMustZone(),Mission.SetVar(Timer,1,777)",
              T1Run(p3, 0, T1Fin, wg, 73, f73));

        int id5 = QC.Mission.Toau.ConfessionsOfRoyalty;
        var p5 = Patched(new QC.Missions.Toau.ConfessionsOfRoyalty(), QC.MissionKey.Toau.ConfessionsOfRoyalty);
        var f564 = T1OptionF(id5, 0, 1);
        f564.NextJstMidnight = 777;
        var f564b = T1OptionF(id5, 0, 1);
        f564b.CompleteResult = false;
        Check("c3 T1 adjustments toau 5: 564's finish with option 1 takes the letter and completes, then starts the " +
              "Mission[4][5]Timer to JST midnight (not after a false complete); another option does nothing",
              $"Player.DelKeyItem({QC.KeyItem.RaillefalsLetter}),Mission.Complete()," +
              $"Player.SetCharVar(Mission[4][5]Timer,1,777)|" +
              $"Player.DelKeyItem({QC.KeyItem.RaillefalsLetter}),Mission.Complete()|",
              $"{T1Run(p5, 0, T1Fin, QC.Zone.ChateauDoraguille, 564, f564)}|" +
              $"{T1Run(p5, 0, T1Fin, QC.Zone.ChateauDoraguille, 564, f564b)}|" +
              $"{T1Run(p5, 0, T1Fin, QC.Zone.ChateauDoraguille, 564, T1OptionF(id5, 0, 0))}");

        int id6 = QC.Mission.Toau.EasterlyWinds;
        var p6 = Patched(new QC.Missions.Toau.EasterlyWinds(), QC.MissionKey.Toau.EasterlyWinds);
        var f6 = T1F(id6);
        f6.MissionVars[$"{ToauLog}/{id6}/Timer"] = 1;
        Check("c3 T1 adjustments toau 6: Ru'Lude area 1 plays 10094 only with the Timer 0",
              "ProgressEvent(10094)|null",
              $"{T1Reply(p6, 0, T1Area, QC.Zone.RuludeGardens, null, 1, T1F(id6))}|" +
              $"{T1Reply(p6, 0, T1Area, QC.Zone.RuludeGardens, null, 1, f6)}");

        int id7 = QC.Mission.Toau.WesterlyWinds;
        var p7 = Patched(new QC.Missions.Toau.WesterlyWinds(), QC.MissionKey.Toau.WesterlyWinds);
        var f7 = T1F(id7, 1);
        f7.NextJstMidnight = 777;
        var f7b = T1F(id7, 1);
        f7b.CompleteResult = false;
        Check("c3 T1 adjustments toau 7: 3028's finish completes, takes the note, sets the local mustZone and the " +
              "Mission[4][7]Timer to JST midnight; a false complete does nothing more",
              $"Mission.Complete()," +
              $"Player.DelKeyItem({QC.KeyItem.RaillefalsNote}),Player.SetLocalVar(Mission[4][7]mustZone,1)," +
              "Player.SetCharVar(Mission[4][7]Timer,1,777)|Mission.Complete()",
              $"{T1Run(p7, 0, T1Fin, wg, 3028, f7)}|{T1Run(p7, 0, T1Fin, wg, 3028, f7b)}");

        int id8 = QC.Mission.Toau.AMercenaryLife;
        var p8 = Patched(new QC.Missions.Toau.AMercenaryLife(), QC.MissionKey.Toau.AMercenaryLife);
        var f8 = T1F(id8);
        f8.MissionVars[$"{ToauLog}/{id8}/Timer"] = 1;
        Check("c3 T1 adjustments toau 8: area 3 plays 3050 (all threes) only with the Timer 0",
              "ProgressEvent(3050,3,3,3,3,3,3,3,3,0)|null",
              $"{T1Reply(p8, 1, T1Area, wg, null, 3, T1F(id8))}|{T1Reply(p8, 1, T1Area, wg, null, 3, f8)}");
    }
}
