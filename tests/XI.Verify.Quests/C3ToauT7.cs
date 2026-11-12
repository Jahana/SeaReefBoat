// XI.Verify.Quests -- quest rework C3 T7 (WotG missions 28-41 on C#): the arms and values the golden flows cannot show
// (the Ghoyu's Reverie 10000 finishes and the Throne Room [S] 32001 no flow opens, the set-downs, the Timer boundaries,
// the campaign allegiance operands, the nil reward of Prelude to a Storm, the maw call of Edge of Existence, the
// blank Her Memories). The traces hold the packets.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;
using XS = XI.QuestContent.Npc.XarcabardS;
using CZ = XI.QuestContent.Npc.CastleZvahlBaileysS;
using MW = XI.QuestContent.Mission.Wotg;
using W = XI.QuestContent.Missions.Wotg;

internal static partial class Program
{
    internal static void RunC3ToauT7()
    {
        Console.WriteLine("\n== c3 T7: WotG 28-41 on C# ==");
        var all = new MissionScript[]
        {
            new W.AnotherWorld(), new W.AHawkInRepose(),
            new W.TheBattleOfXarcabard(), new W.PreludeToAStorm(),
            new W.StormsCrescendo(), new W.IntoTheBeastsMaw(),
            new W.TheHunterEnsnared(), new W.FlightOfTheLion(),
            new W.FallOfTheHawk(), new W.DarknessDescends(),
            new W.AdieuLilisette(), new W.ByTheFadingLight(),
            new W.EdgeOfExistence(), new W.HerMemories(),
        };
        string[] files =
        {
            "28_Another_World", "29_A_Hawk_in_Repose", "30_The_Battle_of_Xarcabard", "31_Prelude_to_a_Storm",
            "32_Storms_Crescendo", "33_Into_the_Beasts_Maw", "34_The_Hunter_Ensnared", "35_Flight_of_the_Lion",
            "36_Fall_of_the_Hawk", "37_Darkness_Descends", "38_Adieu_Lilisette", "39_By_the_Fading_Light",
            "40_Edge_of_Existence", "41_Her_Memories",
        };
        Check("c3 T7 wotg: 14 missions: clean, each LegacyPath its Lua file's require name",
              string.Join(",", files.Select(f => $"|scripts/missions/wotg/{f}")),
              string.Join(",", all.Select(m => $"{string.Join(" | ", QuestScriptValidator.Validate(m.Compile()))}|" +
                  $"{m.LegacyPath}")));
        Check("c3 T7 wotg: the mission keys",
              "ANOTHER_WORLD,A_HAWK_IN_REPOSE,THE_BATTLE_OF_XARCABARD,PRELUDE_TO_A_STORM,STORMS_CRESCENDO," +
              "INTO_THE_BEASTS_MAW,THE_HUNTER_ENSNARED,FLIGHT_OF_THE_LION,FALL_OF_THE_HAWK,DARKNESS_DESCENDS," +
              "ADIEU_LILISETTE,BY_THE_FADING_LIGHT,EDGE_OF_EXISTENCE,HER_MEMORIES",
              string.Join(",", all.Select(m => m.Key.Split('.').Last().ToUpperInvariant())));
        string[] nextNames =
        {
            "AHawkInRepose", "TheBattleOfXarcabard", "PreludeToAStorm", "StormsCrescendo", "IntoTheBeastsMaw",
            "TheHunterEnsnared", "FlightOfTheLion", "FallOfTheHawk", "DarknessDescends", "AdieuLilisette",
            "ByTheFadingLight", "EdgeOfExistence", "HerMemories", "ForgetMeNot",
        };
        Check("c3 T7 wotg: the next missions",
              "AHawkInRepose 28,TheBattleOfXarcabard 29,PreludeToAStorm 30,StormsCrescendo 31,IntoTheBeastsMaw 32," +
              "TheHunterEnsnared 33,FlightOfTheLion 34,FallOfTheHawk 35,DarknessDescends 36,AdieuLilisette 37," +
              "ByTheFadingLight 38,EdgeOfExistence 39,HerMemories 40,ForgetMeNot 41",
              string.Join(",", all.Select(m => m.Compile()).Select((d, i) =>
                  $"{nextNames[i]} {(d.Reward.NextLog == MissionLog.WoTG ? d.Reward.NextMission : -1)}")));

        RunC3ToauT7Replies();
        RunC3ToauT7Xarcabard();
        RunC3ToauT7Baileys();
        RunC3ToauT7Rest();
        RunC3ToauT7Facets();
    }

    // the reply kinds and operands of the NPC events: a progress event and a plain event send the same packet, so no
    // trace tells the kinds apart
    static void RunC3ToauT7Replies()
    {
        string Rep(MissionScript m, int id, QuestHandlerKind kind, ushort zone, string npc, int status = 0,
            string var = "Status", bool trade = false)
        {
            var d = m.Compile();
            var f = W5F(id);
            f.MissionVars[W6Key(id, var)] = status;
            f.TradeResult = trade;
            return W6Safe(() => W5Rep(d, 0, kind, zone, npc, 1, f));
        }
        var T = QuestHandlerKind.OnTrigger;
        var xs = QC.Zone.XarcabardS;
        Check("c3 T7 wotg: the NPC events' kinds and operands",
              "ProgressEvent(566,233,23,1756,0,0,0,1,1)|ProgressEvent(502,105,2,300000,0,0,0,1,22262)|" +
              "ProgressEvent(503,0,23,1753,0,0,0,1,3871)|ProgressEvent(18,0,5,0,0,0,0,0,1)|" +
              "ProgressEvent(21,137,300,200,100,0,6553620,0,0)|ProgressEvent(25,137,23,2963)|ProgressEvent(23,137,5)|" +
              "ProgressEvent(2,53,23,1756,0,0,16384001,0,0)|ProgressEvent(24,137,23,1756,0,65863679,8553890,4095,0)|" +
              "ProgressEvent(10,98,300,200,100,0,9240578,0,0)|ProgressEvent(41,137,0,0,7)|" +
              "ProgressEvent(3,0,0,0,0,0,0,0,1)|ProgressEvent(502,1,26,83764,22093,466180,1822,648024,0)|" +
              "ProgressEvent(502,2,0,0,0,0,0,0,1)|ProgressEvent(7,0,0,0,0,0,0,3,1)|" +
              "ProgressEvent(702,1,0,0,0,0,0,3,1)|ProgressEvent(702,2,-8,-2,-2,0,0,0,1)|" +
              "MessageSpecial(137:JOIN_ALLIED_FORCE)",
              string.Join("|",
                  Rep(new W.AnotherWorld(), MW.AnotherWorld, T, QC.Zone.ChateauDoraguille,
                      QC.Npc.ChateauDoraguille.Halver, 1, "Prog"),
                  Rep(new W.AHawkInRepose(), MW.AHawkInRepose, T, QC.Zone.BatalliaDowns,
                      QC.Npc.BatalliaDowns.WeatheredGravestone),
                  Rep(new W.AHawkInRepose(), MW.AHawkInRepose, QuestHandlerKind.OnTrade,
                      QC.Zone.BatalliaDowns, QC.Npc.BatalliaDowns.WeatheredGravestone, 1, trade: true),
                  Rep(new W.TheBattleOfXarcabard(), MW.TheBattleOfXarcabard, T, xs,
                      XS.RallyPointRed, 1),
                  Rep(new W.PreludeToAStorm(), MW.PreludeToAStorm, T, xs, XS.RallyPointGreen),
                  Rep(new W.PreludeToAStorm(), MW.PreludeToAStorm, T, xs, XS.RallyPointGreen, 1),
                  Rep(new W.StormsCrescendo(), MW.StormsCrescendo, T, xs, XS.RallyPointRed, 3),
                  Rep(new W.IntoTheBeastsMaw(), MW.IntoTheBeastsMaw, T,
                      QC.Zone.CastleZvahlBaileysS, CZ.PeculiarGlint, 1),
                  Rep(new W.TheHunterEnsnared(), MW.TheHunterEnsnared, T, xs, XS.RallyPointRed),
                  Rep(new W.FlightOfTheLion(), MW.FlightOfTheLion, T,
                      QC.Zone.SauromugueChampaignS, QC.Npc.SauromugueChampaignS.BulwarkGate),
                  Rep(new W.ByTheFadingLight(), MW.ByTheFadingLight, T, xs, XS.RallyPointRed),
                  Rep(new W.EdgeOfExistence(), MW.EdgeOfExistence, T, QC.Zone.BatalliaDowns,
                      QC.Npc.BatalliaDowns.CavernousMaw),
                  // review M2: the other four maws' kinds and (no) modifiers
                  Rep(new W.EdgeOfExistence(), MW.EdgeOfExistence, T, QC.Zone.RolanberryFields,
                      QC.Npc.RolanberryFields.CavernousMaw),
                  Rep(new W.EdgeOfExistence(), MW.EdgeOfExistence, T, QC.Zone.SauromugueChampaign,
                      QC.Npc.SauromugueChampaign.CavernousMaw),
                  Rep(new W.EdgeOfExistence(), MW.EdgeOfExistence, T, QC.Zone.BatalliaDownsS,
                      QC.Npc.BatalliaDownsS.CavernousMaw),
                  Rep(new W.EdgeOfExistence(), MW.EdgeOfExistence, T, QC.Zone.RolanberryFieldsS,
                      QC.Npc.RolanberryFieldsS.CavernousMaw),
                  Rep(new W.EdgeOfExistence(), MW.EdgeOfExistence, T,
                      QC.Zone.SauromugueChampaignS, QC.Npc.SauromugueChampaignS.CavernousMaw),
                  Rep(new W.IntoTheBeastsMaw(), MW.IntoTheBeastsMaw, T, xs, XS.RallyPointRed)));
        var cz = QC.Zone.CastleZvahlBaileysS;
        Check("c3 T7 wotg: the zone-in events are plain events",
              "Event(945)|Event(17)|Event(22)|Event(20)|Event(39)|Event(1)|Event(14)|Event(15)|Event(3)|Event(4)|" +
              "Event(8)|Event(10)",
              string.Join("|",
                  ZoneIn(new W.AnotherWorld(), MW.AnotherWorld, QC.Zone.SouthernSanDoria, 0,
                      QC.Zone.EastRonfaure),
                  ZoneIn(new W.TheBattleOfXarcabard(), MW.TheBattleOfXarcabard, xs, 0),
                  ZoneIn(new W.PreludeToAStorm(), MW.PreludeToAStorm, xs, 2),
                  ZoneIn(new W.StormsCrescendo(), MW.StormsCrescendo, xs, 2),
                  ZoneIn(new W.StormsCrescendo(), MW.StormsCrescendo, xs, 4),
                  ZoneIn(new W.IntoTheBeastsMaw(), MW.IntoTheBeastsMaw, cz, 0),
                  ZoneIn(new W.IntoTheBeastsMaw(), MW.IntoTheBeastsMaw, cz, 2),
                  ZoneIn(new W.IntoTheBeastsMaw(), MW.IntoTheBeastsMaw, cz, 3),
                  ZoneIn(new W.IntoTheBeastsMaw(), MW.IntoTheBeastsMaw, cz, 5),
                  ZoneIn(new W.FallOfTheHawk(), MW.FallOfTheHawk, cz, 0),
                  ZoneIn(new W.DarknessDescends(), MW.DarknessDescends, QC.Zone.ThroneRoomS, 0),
                  ZoneIn(new W.DarknessDescends(), MW.DarknessDescends, QC.Zone.ThroneRoomS, 2)));

        string ZoneIn(MissionScript m, int id, ushort zone, int status, int prev = 0)
        {
            var d = m.Compile();
            var f = W5F(id);
            f.MissionVars[W6Key(id, "Status")] = status;
            f.PrevZone = prev;
            return W6Safe(() => W5ZoneIn(d, 0, zone, f));
        }
    }

    // 31 and 32 in Xarcabard [S]: the 10000 finishes, the set-down of 23, the Timer boundary, the allegiance, the
    // reward
    static void RunC3ToauT7Xarcabard()
    {
        var xs = QC.Zone.XarcabardS;
        var gr = QC.Zone.GhoyusReverie;

        var d31 = new W.PreludeToAStorm().Compile();
        int id31 = MW.PreludeToAStorm;
        Check("c3 T7 wotg 31: Ghoyu's Reverie's 10000 sets Status 2 and the char down at 96.77 -23.943 -277.87 rot 253",
              $"Mission.SetVar(Status,2),{W6Pos(96.77, -23.943, -277.87, 253, xs)}",
              W5Fin(d31, 0, gr, 10000, W5F(id31)));
        string Reward(int option, bool give)
        {
            var f = W5F(id31);
            f.MissionVars[W6Key(id31, "Option")] = option;
            f.GiveResult = give;
            return W6Safe(() => W5Fin(d31, 0, xs, 22, f));
        }
        Check("c3 T7 wotg 31: finish 22 gives the Option's reward (nil for 0 and 4: given as nothing) and completes",
              $"Player.GiveItem(nil),Mission.Complete()|Player.GiveItem({QC.Item.Elixir}),Mission.Complete()|" +
              $"Player.GiveItem({QC.Item.VileElixir}),Mission.Complete()|" +
              $"Player.GiveItem({QC.Item.VileElixirP1}),Mission.Complete()|Player.GiveItem(nil),Mission.Complete()",
              string.Join("|", new[] { 0, 1, 2, 3, 4 }.Select(o => Reward(o, true))));
        Check("c3 T7 wotg 31: finish 22 does not complete when the item is not given",
              $"Player.GiveItem({QC.Item.Elixir})", Reward(1, false));
        Check("c3 T7 wotg 31: rewardItems[Option] is nil outside 1 to 3",
              $"null {QC.Item.Elixir} {QC.Item.VileElixir} {QC.Item.VileElixirP1} null null",
              string.Join(" ", new[] { 0, 1, 2, 3, 4, -1 }.Select(o =>
                  W.PreludeToAStorm.RewardItem(o)?.ToString() ?? "null")));

        var d32 = new W.StormsCrescendo().Compile();
        int id32 = MW.StormsCrescendo;
        Check("c3 T7 wotg 32: Ghoyu's Reverie's 10000 sets Status 2 and the char down at 138.527 -16.197 -30.414 " +
              "rot 211",
              $"Mission.SetVar(Status,2),{W6Pos(138.527, -16.197, -30.414, 211, xs)}",
              W5Fin(d32, 0, gr, 10000, W5F(id32)));
        Check("c3 T7 wotg 32: finish 23 sets Status 4 and the char down at -105.798 -25.522 -53.499 rot 176",
              $"Mission.SetVar(Status,4),{W6Pos(-105.798, -25.522, -53.499, 176, xs)}",
              W5Fin(d32, 0, xs, 23, W5F(id32)));
        W5Fake Blue(int status, int timer, bool flare, int allegiance = 0)
        {
            var f = W5F(id32);
            f.UniqueDay = 100;
            f.Allegiance = allegiance;
            f.MissionVars[W6Key(id32, "Status")] = status;
            f.MissionVars[W6Key(id32, "Timer")] = timer;
            if (flare) f.KeyItems[QC.KeyItem.AlchemicalSignalFlare] = true;
            return f;
        }
        string BlueSays(W5Fake f) =>
            W6Safe(() => W5Trig(d32, 0, xs, XS.RallyPointBlue, f) + "@" + string.Join(",", f.Calls));
        Check("c3 T7 wotg 32: Rally Point: Blue at Status 1 without the flare plays 26 with Timer 99 and 100 " +
              "(the day), " +
              "not 101",
              "ProgressEvent(26,137,23,2964)@|ProgressEvent(26,137,23,2964)@|" +
              "NoAction()@Text.MessageName(137:BASTOKAN_OFFICER_GONE)",
              string.Join("|", new[] { 99, 100, 101 }.Select(t => BlueSays(Blue(1, t, false)))));
        Check("c3 T7 wotg 32: Rally Point: Blue at Status 0 sends the campaign allegiance first",
              "ProgressEvent(19,3,23,1756,400,67108863,659255957,4905,0)@", BlueSays(Blue(0, 0, false, 3)));
        Check("c3 T7 wotg 32: Rally Point: Blue answers noAction at Status 2, 4 and 5 and after its messages",
              "NoAction()@|NoAction()@|NoAction()@|NoAction()@Text.MessageName(137:HELP_REPUBLIC_PREPARE)|" +
              "NoAction()@Text.MessageName(137:COMPLETED_TASKS)",
              string.Join("|", new[] { BlueSays(Blue(2, 0, false)), BlueSays(Blue(4, 0, false)),
                  BlueSays(Blue(5, 0, false)), BlueSays(Blue(1, 101, true)), BlueSays(Blue(3, 0, false)) }));
    }

    // 33 in Castle Zvahl Baileys [S]: the 10000 finish, the set-downs of 2 and 14, the Ornate Block's arms
    static void RunC3ToauT7Baileys()
    {
        var cz = QC.Zone.CastleZvahlBaileysS;
        var d33 = new W.IntoTheBeastsMaw().Compile();
        int id33 = MW.IntoTheBeastsMaw;
        Check("c3 T7 wotg 33: Ghoyu's Reverie's 10000 sets Status 5 and the char down at 179.987 -24.046 94.225 " +
              "rot 194",
              $"Mission.SetVar(Status,5),{W6Pos(179.987, -24.046, 94.225, 194, cz)}",
              W5Fin(d33, 0, QC.Zone.GhoyusReverie, 10000, W5F(id33)));
        string down = W6Pos(183.405, -24.041, 100.041, 126, cz);
        Check("c3 T7 wotg 33: finishes 2 and 14 set Status 2 and 3 and the char down at 183.405 -24.041 100.041 " +
              "rot 126",
              $"Mission.SetVar(Status,2),{down}|Mission.SetVar(Status,3),{down}",
              W5Fin(d33, 0, cz, 2, W5F(id33)) + "|" + W5Fin(d33, 0, cz, 14, W5F(id33)));
        W5Fake Block(int status, int timer, bool flare)
        {
            var f = W5F(id33);
            f.UniqueDay = 100;
            f.MissionVars[W6Key(id33, "Status")] = status;
            f.MissionVars[W6Key(id33, "Timer")] = timer;
            if (flare) f.KeyItems[QC.KeyItem.DistressSignalFlare] = true;
            return f;
        }
        string Says(W5Fake f) => W6Safe(() => W5Trig(d33, 0, cz, CZ.OrnateBlock, f) + "@" + string.Join(",", f.Calls));
        Check("c3 T7 wotg 33: the Ornate Block at Status 4 without the flare plays 11 with Timer 99 and 100 " +
              "(the day), " +
              "not 101 (CANNOT_FIND_FLARE with the flare's id)",
              $"ProgressEvent(11,138)@|ProgressEvent(11,138)@|NoAction()@Text.MessageName(138:CANNOT_FIND_FLARE," +
              $"{QC.KeyItem.DistressSignalFlare})",
              string.Join("|", new[] { 99, 100, 101 }.Select(t => Says(Block(4, t, false)))));
        Check("c3 T7 wotg 33: the Ornate Block answers nothing with the flare or at another status",
              "null@|null@|null@",
              Says(Block(4, 0, true)) + "|" + Says(Block(3, 0, false)) + "|" + Says(Block(5, 0, false)));
        Check("c3 T7 wotg 33: finish 11 gives the flare on option 1 only",
              $"|Player.GiveKeyItem({QC.KeyItem.DistressSignalFlare})|",
              W5Fin(d33, 0, cz, 11, W5F(id33), 0) + "|" + W5Fin(d33, 0, cz, 11, W5F(id33), 1) + "|" +
              W5Fin(d33, 0, cz, 11, W5F(id33), 2));
    }

    // 37, 38, 40 and 41
    static void RunC3ToauT7Rest()
    {
        var tr = QC.Zone.ThroneRoomS;
        var d37 = new W.DarknessDescends().Compile();
        int id37 = MW.DarknessDescends;
        string Win(int bf, int status)
        {
            var f = W5F(id37);
            f.PlayerLocalVars["battlefieldWin"] = bf;
            f.MissionVars[W6Key(id37, "Status")] = status;
            return W6Safe(() => W5Fin(d37, 0, tr, 32001, f));
        }
        Check("c3 T7 wotg 37: the 32001 finish (no flow opens it) sets Status 2 and the char down at -90.854 -5.75 " +
              "-0.009 rot 129 only for battlefieldWin 353 at Status 1",
              $"Mission.SetVar(Status,2),{W6Pos(-90.854, -5.75, -0.009, 129, tr)}|||",
              Win(353, 1) + "|" + Win(352, 1) + "|" + Win(353, 0) + "|" + Win(353, 2));
        Check("c3 T7 wotg 37: xi.battlefield.id.DARKNESS_DESCENDS is 353", "353",
              QC.Battlefield.DarknessDescends.ToString());
        string Look(int option)
        {
            var f = W5F(id37);
            f.FaceId = 7;
            f.Models = (11, 12, 13, 14, 15);
            return W6Safe(() => W5Upd(d37, 0, tr, 10, f, option));
        }
        Check("c3 T7 wotg 37: update 10 (option 5) sends 156, the face, then head, body, hands, main and sub models",
              "Player.UpdateEvent(156,7,11,12,13,14,15,0)|", Look(5) + "|" + Look(4));

        var ss = QC.Zone.SouthernSanDoriaS;
        var d38 = new W.AdieuLilisette().Compile();
        int id38 = MW.AdieuLilisette;
        string Lil(string npc, int allegiance)
        {
            var f = W5F(id38);
            f.Allegiance = allegiance;
            return W6Safe(() => W5Trig(d38, 0, ss, npc, f));
        }
        Check("c3 T7 wotg 38: Lion Springs plays 166 with the campaign allegiance; Rholont plays 674 only at " +
              "allegiance 1",
              "ProgressEvent(166,2)|null|Event(674,80,23)|null|null",
              Lil(QC.Npc.SouthernSanDoriaS.LionSprings, 2) + "|" + Lil(QC.Npc.SouthernSanDoriaS.Rholont, 0) + "|" +
              Lil(QC.Npc.SouthernSanDoriaS.Rholont, 1) + "|" + Lil(QC.Npc.SouthernSanDoriaS.Rholont, 2) + "|" +
              Lil(QC.Npc.SouthernSanDoriaS.Rholont, 3));

        var d40 = new W.EdgeOfExistence().Compile();
        int id40 = MW.EdgeOfExistence;
        string Maw(ushort zone, int csid, bool complete)
        {
            var f = W5F(id40);
            f.CompleteResult = complete;
            return W6Safe(() => W5Fin(d40, 0, zone, csid, f));
        }
        var maws = new (ushort Zone, int Csid)[]
        {
            (QC.Zone.BatalliaDowns, 3), (QC.Zone.RolanberryFields, 502), (QC.Zone.SauromugueChampaign, 502),
            (QC.Zone.BatalliaDownsS, 7), (QC.Zone.RolanberryFieldsS, 702), (QC.Zone.SauromugueChampaignS, 702),
        };
        Check("c3 T7 wotg 40: each maw's finish completes, then calls goToMaw with its own zone's id",
              string.Join("|", maws.Select(m => $"Mission.Complete(),Player.GoToMaw({m.Zone})")),
              string.Join("|", maws.Select(m => Maw(m.Zone, m.Csid, true))));
        Check("c3 T7 wotg 40: no goToMaw when the completion fails",
              string.Join("|", maws.Select(m => "Mission.Complete()")),
              string.Join("|", maws.Select(m => Maw(m.Zone, m.Csid, false))));

        var d41 = new W.HerMemories().Compile();
        Check("c3 T7 wotg 41: one state (the mission current) with no handler",
              "1 True 0", $"{d41.States.Count} {d41.States[0].IsMissionMatch(W5F(MW.HerMemories))} " +
              $"{d41.States[0].Handlers.Count}");
    }

    // the facet members T7 adds (GoToMaw, Face, EquipmentModelIds, GiveItemOrNil) on the fake
    static void RunC3ToauT7Facets()
    {
        var f = new W5Fake { MawThrows = true, FaceId = 3, Models = (1, 2, 3, 4, 5) };
        IQuestPlayer p = f;
        Check("c3 T7 facets: GoToMaw records and throws as the Lua global does with a number",
              "throws attempt to index local 'maw' (a number value)|Player.GoToMaw(105)",
              W6Safe(() => { p.GoToMaw(105); return "no throw"; }) + "|" + string.Join(",", f.Calls));
        Check("c3 T7 facets: Face and EquipmentModelIds read the fake", "3 (1, 2, 3, 4, 5)",
              $"{p.Face} {p.EquipmentModelIds}");
        var g = new W5Fake { GiveResult = false };
        IQuestPlayer gp = g;
        Check("c3 T7 facets: GiveItemOrNil of nil answers true, of an item what GiveItem answers",
              "True False|Player.GiveItem(nil),Player.GiveItem(4145)",
              $"{gp.GiveItemOrNil(null)} {gp.GiveItemOrNil(4145)}|{string.Join(",", g.Calls)}");
    }
}
