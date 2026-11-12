// XI.Verify.Quests -- quest rework C3 T2 (ToAU 14-25 and the Shield of Diplomacy battlefield on C#): the arms the
// golden flows reach only together, each on the one read that decides it. The traces hold the packets.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    internal static void RunC3ToauT2()
    {
        Console.WriteLine("\n== c3 T2: ToAU 14-25 on C# ==");
        ushort wg = QC.Zone.AhtUrhganWhitegate;
        var naja = QC.Npc.AhtUrhganWhitegate.NajaSalaheem;

        // keys
        var all = new MissionScript[]
        {
            new QC.Missions.Toau.TheDolphinCrest(), new QC.Missions.Toau.TheBlackCoffin(),
            new QC.Missions.Toau.GhostsOfThePast(), new QC.Missions.Toau.GuestsOfTheEmpire(),
            new QC.Missions.Toau.PassingGlory(), new QC.Missions.Toau.SweetsForTheSoul(),
            new QC.Missions.Toau.TeahouseTumult(), new QC.Missions.Toau.FindersKeepers(),
            new QC.Missions.Toau.ShieldOfDiplomacy(), new QC.Missions.Toau.SocialGraces(),
            new QC.Missions.Toau.FoiledAmbition(), new QC.Missions.Toau.PlayingThePart(),
        };
        Check("c3 T2 toau: twelve missions, each with a legacy path", "12 12",
              $"{all.Length} {all.Count(m => !string.IsNullOrEmpty(m.LegacyPath))}");

        // 16 Ghosts of the Past: the dressed operand follows RoyalPalaceArmorCheck alone
        var d16 = new QC.Missions.Toau.GhostsOfThePast().Compile();
        int id16 = QC.Mission.Toau.GhostsOfThePast;
        Check("c3 T2 toau 16: Naja's dressed operand is the armor check: 0 then 1",
              "ProgressEvent(3074,3,1,0,0,0,0,0,0,0)|ProgressEvent(3074,3,1,0,0,0,0,0,1,0)",
              $"{T1Reply(d16, 0, T1Trig, wg, naja, 1, T1F(id16, 0))}|" +
              $"{T1Reply(d16, 0, T1Trig, wg, naja, 1, Armored(T1F(id16, 0)))}");

        // 16/17 dress-up: each option's bit and operands, an unknown option, an existing mask kept
        string Dress(MissionScriptDefinition d, int id, int option, int mask)
        {
            var f = T1F(id, 0);
            f.Option = option;
            f.MissionLocalVars["najaDressUp"] = mask;
            return $"{T1Run(d, 0, T1Upd, wg, 3074, f)}#{f.MissionLocalVars["najaDressUp"]}";
        }
        Check("c3 T2 toau 16: dress-up options 1, 10..17, 5 and an old mask",
              "Player.UpdateEvent(50,1,0,0,0,1,0,0),Mission.SetLocalVar(najaDressUp,0)#0|Player" +
              ".UpdateEvent(2,1,255,0,0,0,4095,0),Mission.SetLocalVar(najaDressUp,2)#2|Player.U" +
              "pdateEvent(4,1,1757,0,0,0,4095,1),Mission.SetLocalVar(najaDressUp,4)#4|Player.Up" +
              "dateEvent(8,1,10,0,1,1,0,0),Mission.SetLocalVar(najaDressUp,8)#8|Player.UpdateEv" +
              "ent(16,1,0,0,0,0,0,0),Mission.SetLocalVar(najaDressUp,16)#16|Player.UpdateEvent(" +
              "32,1,0,0,0,1,32,1),Mission.SetLocalVar(najaDressUp,32)#32|Player.UpdateEvent(64," +
              "1,2964,0,0,2185,3,0),Mission.SetLocalVar(najaDressUp,64)#64|Player.UpdateEvent(1" +
              "28,1,1,22185,0,1,0,0),Mission.SetLocalVar(najaDressUp,128)#128|Player.UpdateEven" +
              "t(256,1,1320,0,0,1,1,1),Mission.SetLocalVar(najaDressUp,256)#256|Mission.SetLoca" +
              "lVar(najaDressUp,0)#0|Player.UpdateEvent(-2147483646,1,255,0,0,0,4095,0),Mission" +
              ".SetLocalVar(najaDressUp,-2147483646)#-2147483646|Player.UpdateEvent(262,1,1320," +
              "0,0,1,1,1),Mission.SetLocalVar(najaDressUp,262)#262",
              string.Join("|", new[] { 1, 10, 11, 12, 13, 14, 15, 16, 17, 5 }.Select(o => Dress(d16, id16, o, 0))) +
              "|" + Dress(d16, id16, 10, 1 << 31) + "|" + Dress(d16, id16, 17, 6));
        Check("c3 T2 toau 16: finish: option 2 sets status 1; others only complete",
              "Mission.Complete(),Player.SetMissionStatus(4,1)|Mission.Complete()",
              T1Run(d16, 0, T1Fin, wg, 3074, T1OptionF(id16, 0, 2)) + "|" +
              T1Run(d16, 0, T1Fin, wg, 3074, T1OptionF(id16, 0, 0)));

        RunC3ToauT2Rest(wg, naja);

        var fk = new Fake { RoyalPalaceArmorOk = true };
        Check("c3 T2 facet: RoyalPalaceArmorCheck reads the fake; StartEventHiddenParam records its operands",
              "True|Player.StartEventHiddenParam(34,7,1)",
              fk.RoyalPalaceArmorCheck() + "|" + RecordHidden());
    }

    static void RunC3ToauT2Rest(ushort wg, string naja)
    {
        const string K = "KeyItem";
        ushort arr = QC.Zone.ArrapagoReef, nash = QC.Zone.Nashmau;
        var onZone = QuestHandlerKind.OnZoneIn;
        string Rep(MissionScriptDefinition d, QuestHandlerKind k, ushort z, string e, int id, Fake f) =>
            T1Reply(d, 0, k, z, e, id, f);

        // 14 Dolphin Crest, 15 Black Coffin
        var d14 = new QC.Missions.Toau.TheDolphinCrest().Compile();
        int id14 = QC.Mission.Toau.TheDolphinCrest;
        Check("c3 T2 toau 14: Naja 3072 with the rank; finish completes",
              "ProgressEvent(3072,5,1,0,0,0,0,0,0,0)|Mission.Complete()",
              Rep(d14, T1Trig, wg, naja, 1, T1F(id14, 0, merc: 5)) + "|" + T1Run(d14, 0, T1Fin, wg, 3072, T1F(id14)));
        var d15 = new QC.Missions.Toau.TheBlackCoffin().Compile();
        int id15 = QC.Mission.Toau.TheBlackCoffin;
        Fake Coin(int status, bool coin, int prev = 0, double x = 0, double y = 0, double z = 0)
        {
            var f = T1F(id15, status);
            f.KeyItems[QC.KeyItem.EphramadianGoldCoin] = coin;
            f.PrevZone = prev;
            f.XPos = x;
            f.YPos = y;
            f.ZPos = z;
            return f;
        }
        Check("c3 T2 toau 15: Naja 3073; the area: coin alone, status alone, both, neither",
              "Event(3073,4,1,0,0,0,0,0,0,0)|ProgressEvent(35,{isHidden=true})|null|null|Event(" +
              "12):oncePerZone()|null",
              Rep(d15, T1Trig, wg, naja, 1, T1F(id15, 0, merc: 4)) + "|" +
              string.Join("|", new[] { (0, true), (1, true), (0, false), (1, false), (2, false) }
                  .Select(t => Rep(d15, T1Area, arr, null, 1, Coin(t.Item1, t.Item2)))));
        var fa = Coin(0, true);
        d15.States[0].Handlers.Single(h => h.Kind == T1Area && h.Zone == arr).Reply(fa);
        Check("c3 T2 toau 15: the area's calls before its reply",
              "Player.StartEvent(8),Player.StartEventHiddenParam(34,7,1)", string.Join(",", fa.Calls));
        Check("c3 T2 toau 15: Arrapago zone-in: prev zone alone, status alone, both",
              "Event(9)|null|null|Player.SetPos(-444.059,-4.124,-413.934,126)",
              string.Join("|", new[] { (QC.Zone.TheAshuTalif, 2), (QC.Zone.TheAshuTalif, 1), (nash, 2) }
                  .Select(t => Rep(d15, onZone, arr, null, 1, Coin(t.Item2, false, t.Item1)))) + "|" +
              CallsOf(d15, onZone, arr, Coin(2, false, QC.Zone.TheAshuTalif)));
        Check("c3 T2 toau 15: finishes 9, 35, 281",
              "Player.SetMissionStatus(4,3),Player.SetPos(0,0,0,0,53)|Player.SetMissionStatus(4" +
              ",1)|Mission.Complete()",
              T1Run(d15, 0, T1Fin, arr, 9, Coin(2, false)) + "|" + T1Run(d15, 0, T1Fin, arr, 35, Coin(0, true)) + "|" +
              T1Run(d15, 0, T1Fin, nash, 281, Coin(3, false)));
        Check("c3 T2 toau 15: Nashmau zone-in: each part alone",
              "Event(281)|null|null|null|null|null|Player.SetPos(0.016,0,-23.753,63)",
              string.Join("|", new[]
              {
                  Coin(3, false, arr), Coin(3, false, QC.Zone.AhtUrhganWhitegate), Coin(1, false, arr),
                  Coin(3, false, arr, 1), Coin(3, false, arr, 0, 1), Coin(3, false, arr, 0, 0, 1),
              }.Select(f => Rep(d15, onZone, nash, null, 1, f))) + "|" +
              CallsOf(d15, onZone, nash, Coin(3, false, arr)));
        RunC3ToauT2More(wg, naja);
    }

    static void RunC3ToauT2More(ushort wg, string naja)
    {
        var mod = new QC.Modules.Era.Missions.MissionAdjustments();
        MissionScriptDefinition Patched<T>(T mission, string key) where T : MissionScript
        {
            var d = mission.Compile();
            var r = QuestOverrides.Apply(d.Core, mod.Compile(QuestModule.Branches[1].Settings), key);
            return d.WithCore(r.Definition);
        }
        var whitegate = QC.Npc.AhtUrhganWhitegate.ImperialWhitegate;
        var area = T1Area;
        var onZone2 = QuestHandlerKind.OnZoneIn;
        string Rep(MissionScriptDefinition d, QuestHandlerKind k, ushort z, string e, int id, Fake f) =>
            T1Reply(d, 0, k, z, e, id, f);

        // 17 Guests of the Empire
        var d17 = new QC.Missions.Toau.GuestsOfTheEmpire().Compile();
        int id17 = QC.Mission.Toau.GuestsOfTheEmpire;
        Fake G(int status, bool dressed, int main = 0, int sub = 0)
        {
            var f = Armored(T1F(id17, status));
            f.RoyalPalaceArmorOk = dressed;
            f.Equipment[QC.Slot.Main] = main;
            f.Equipment[QC.Slot.Sub] = sub;
            return f;
        }
        Check("c3 T2 toau 17: Imperial Whitegate: status 2, bare hands, dressed; then each alone failing",
              "ProgressEvent(3078,0,1,0,0,0,0,0,1,0)|Event(3081,0,2,1,0,1,0,0,1,0)|Event(3081,0" +
              ",2,1,0,1,0,0,1,0)|Event(3081,0,2,1,0,1,0,0,1,0)|Event(3081,0,2,1,0,1,0,0,0,0)",
              string.Join("|", new[]
              {
                  G(2, true), G(1, true), G(2, true, main: 5), G(2, true, sub: 5), G(2, false),
              }.Select(f => Rep17(d17, whitegate, f))));
        Check("c3 T2 toau 17: Naja by status 0, 1 dressed, 1 undressed, 2, 3",
              "ProgressEvent(3076,3,0,0,0,0,0,0,0,0)|ProgressEvent(3076,3,0,0,0,0,0,0,1,0)|Even" +
              "t(3080,3,1,0,0,0,0,0,0,0)|ProgressEvent(3077,3,0,0,0,0,0,0,0,0)|null",
              string.Join("|", new[] { G(0, false), G(1, true), G(1, false), G(2, false), G(3, false) }
                  .Select(f => Rep17(d17, naja, f))));
        Check("c3 T2 toau 17: Naja's dressed operand with status 0",
              "ProgressEvent(3076,3,0,0,0,0,0,0,1,0)", Rep17(d17, naja, G(0, true)));
        Check("c3 T2 toau 17: finish 3076: option 0 status 2; option 1 status 1 and the dress var 0",
              "Player.SetMissionStatus(4,2)|Player.SetMissionStatus(4,1),Mission.SetLocalVar(na" +
              "jaDressUp,0)",
              T1Run(d17, 0, T1Fin, wg, 3076, T1OptionF(id17, 0, 0)) + "|" +
              T1Run(d17, 0, T1Fin, wg, 3076, T1OptionF(id17, 0, 1)));
        Check("c3 T2 toau 17: update 3076 is the dress-up (option 11)",
              "Player.UpdateEvent(4,1,1757,0,0,0,4095,1),Mission.SetLocalVar(najaDressUp,4)",
              T1Run(d17, 0, T1Upd, wg, 3076, T1OptionF(id17, 0, 11)));
        var g17b = G(2, true);
        g17b.CompleteResult = false;
        Check("c3 T2 toau 17: finish 3078 completes, then mustZone and the Timer one day on; not when the completion " +
              "fails",
              "Mission.Complete(),Player.SetLocalVar(Mission[4][17]mustZone,1),Player.SetCharVa" +
              "r(Mission[4][17]Timer,1,0)|Mission.Complete()",
              T1Run(d17, 0, T1Fin, wg, 3078, G(2, true)) + "|" + T1Run(d17, 0, T1Fin, wg, 3078, g17b));
        var p17 = Patched(new QC.Missions.Toau.GuestsOfTheEmpire(), QC.MissionKey.Toau.GuestsOfTheEmpire);
        var f17 = G(2, true);
        f17.NextJstMidnight = 777;
        var f17b = G(2, true);
        f17b.CompleteResult = false;
        Check("c3 T2 adjustments toau 17: finish 3078 completes, then mustZone and the Timer 1 to JST midnight; not " +
              "when the completion fails",
              "Mission.Complete(),Player.SetLocalVar(Mission[4][17]mustZone,1),Player.SetCharVa" +
              "r(Mission[4][17]Timer,1,777)|Mission.Complete()",
              T1Run(p17, 0, T1Fin, wg, 3078, f17) + "|" + T1Run(p17, 0, T1Fin, wg, 3078, f17b));

        // 18 Passing Glory
        var d18 = new QC.Missions.Toau.PassingGlory().Compile();
        int id18 = QC.Mission.Toau.PassingGlory;
        Fake P(bool mustZone, int timer, int day, int option = 0, int merc = 3)
        {
            var f = T1F(id18, 0, mustZone, merc);
            f.MissionVars[$"{ToauLog}/{id18}/Timer"] = timer;
            f.MissionVars[$"{ToauLog}/{id18}/Option"] = option;
            f.UniqueDay = day;
            return f;
        }
        Check("c3 T2 toau 18: Naja: clear; mustZone alone; the day short of the Timer alone; day equal",
              "ProgressEvent(3090,3,1,0,0,0,0,0,0,0)|Event(3079,3,1,0,0,0,0,0,2,0)|Event(3079,3" +
              ",1,0,0,0,0,0,2,0)|ProgressEvent(3090,7,1,0,0,0,0,0,0,0)",
              string.Join("|", new[] { P(false, 5, 9), P(true, 5, 9), P(false, 5, 4), P(false, 5, 5, merc: 7) }
                  .Select(f => Rep(d18, T1Trig, wg, naja, 1, f))));
        Check("c3 T2 toau 18: Naja's dialog cycle on Option 0, 2, 1 and its writes",
              "Event(3079,3,1,0,0,0,0,0,2,0)#Mission.SetVar(Option,2)|Event(3079,3,1,0,0,0,0,0," +
              "0,0)#Mission.SetVar(Option,0)|Event(3079,3,1,0,0,0,0,0,2,0)#Mission.SetVar(Optio" +
              "n,2)",
              string.Join("|", new[] { 0, 2, 1 }.Select(o =>
              {
                  var f = P(true, 0, 0, o);
                  var r = Rep(d18, T1Trig, wg, naja, 1, f);
                  return r + "#" + string.Join(",", f.Calls);
              })));
        Check("c3 T2 toau 18: area 3: clear; mustZone alone; the day short alone; day equal",
              "ProgressEvent(3090,{text_table=0})|null|null|ProgressEvent(3090,{text_table=0})",
              string.Join("|", new[] { P(false, 5, 9), P(true, 5, 9), P(false, 5, 4), P(false, 5, 5) }
                  .Select(f => Rep(d18, area, wg, null, 3, f))));
        Check("c3 T2 toau 18: update 3090: option 1 answers with the rank; option 0 nothing; finish completes",
              "Player.UpdateEvent(6,1,0,0,0,0,1,0)||Mission.Complete()",
              T1Run(d18, 0, T1Upd, wg, 3090, Ranked(T1OptionF(id18, 0, 1), 6)) + "|" +
              T1Run(d18, 0, T1Upd, wg, 3090, T1OptionF(id18, 0, 0)) + "|" +
              T1Run(d18, 0, T1Fin, wg, 3090, P(false, 0, 0)));

        var p18 = Patched(new QC.Missions.Toau.PassingGlory(), QC.MissionKey.Toau.PassingGlory);
        Check("c3 T2 adjustments toau 18: the section applies only current, mustZone clear and the Timer 0",
              "True|False|False|True|False",
              string.Join("|", new[] { P(false, 0, 0), P(true, 0, 0), P(false, 3, 0), P(false, 0, 9) }
                  .Select(f => p18.States[0].MissionMatches.All(m => m.When == null || m.When(f)).ToString())) + "|" +
              Fresh18(p18, id18));

        // 19 Sweets for the Soul
        var d19 = new QC.Missions.Toau.SweetsForTheSoul().Compile();
        int id19 = QC.Mission.Toau.SweetsForTheSoul;
        Fake Sw(int seen)
        {
            var f = T1F(id19, 0, merc: 4);
            f.MissionVars[$"{ToauLog}/{id19}/SeenAbquhbah"] = seen;
            return f;
        }
        var abq = QC.Npc.AhtUrhganWhitegate.Abquhbah;
        Check("c3 T2 toau 19: Abquhbah: unseen 3106; seen nothing; Naja 3091; area 5 3092",
              "ProgressEvent(3106,{text_table=0})|null|Event(3091,4,1,0,0,0,0,0,0,0)|ProgressEv" +
              "ent(3092,1,1,0,0,0,0,0,0)",
              Rep(d19, T1Trig, wg, abq, 1, Sw(0)) + "|" + Rep(d19, T1Trig, wg, abq, 1, Sw(1)) + "|" +
              Rep(d19, T1Trig, wg, naja, 1, Sw(0)) + "|" + Rep(d19, area, wg, null, 5, Sw(0)));
        Check("c3 T2 toau 19: finishes 3092 (completes) and 3106 (SeenAbquhbah 1)",
              "Mission.Complete()|Mission.SetVar(SeenAbquhbah,1)",
              T1Run(d19, 0, T1Fin, wg, 3092, Sw(0)) + "|" + T1Run(d19, 0, T1Fin, wg, 3106, Sw(0)));

        // 20 Teahouse Tumult
        var d20 = new QC.Missions.Toau.TeahouseTumult().Compile();
        int id20 = QC.Mission.Toau.TeahouseTumult;
        ushort aydeewa = QC.Zone.AydeewaSubterrane;
        var blank = QC.Npc.AydeewaSubterrane.blanktoau20;
        Check("c3 T2 toau 20: Naja 3091; the blank NPC at status 0, 1, 2; zone-in at 0, 1",
              "Event(3091,6,1,0,0,0,0,0,0,0)|null|ProgressEvent(11,0,0,0,0,0,0,0,0)|null|Event(" +
              "10)|null",
              Rep(d20, T1Trig, wg, naja, 1, T1F(id20, 0, merc: 6)) + "|" +
              string.Join("|", new[] { 0, 1, 2 }.Select(st => Rep(d20, T1Trig, aydeewa, blank, 1, T1F(id20, st)))) +
              "|" + string.Join("|", new[] { 0, 1 }.Select(st => Rep(d20, onZone2, aydeewa, null, 1, T1F(id20, st)))));
        Check("c3 T2 toau 20: finish 10 sets status 1; finish 11 completes",
              "Player.SetMissionStatus(4,1)|Mission.Complete()",
              T1Run(d20, 0, T1Fin, aydeewa, 10, T1F(id20)) + "|" + T1Run(d20, 0, T1Fin, aydeewa, 11, T1F(id20)));

        // 21 Finders Keepers
        var d21 = new QC.Missions.Toau.FindersKeepers().Compile();
        int id21 = QC.Mission.Toau.FindersKeepers;
        Check("c3 T2 toau 21: area 3 plays 3093; finish completes",
              "ProgressEvent(3093,1,1,0,0,0,0,0,0)|Mission.Complete()",
              Rep(d21, area, wg, null, 3, T1F(id21)) + "|" + T1Run(d21, 0, T1Fin, wg, 3093, T1F(id21)));

        // 22 Shield of Diplomacy
        var d22 = new QC.Missions.Toau.ShieldOfDiplomacy().Compile();
        int id22 = QC.Mission.Toau.ShieldOfDiplomacy;
        ushort nav = QC.Zone.NavukgoExecutionChamber;
        var door = QC.Npc.NavukgoExecutionChamber._1s0;
        Check("c3 T2 toau 22: Naja 3094; the door at status 0, 1, 2; zone-in at 0, 1, 2",
              "Event(3094,2,1,0,0,0,0,0,0,0)|null|ProgressEvent(2)|null|Event(1)|null|null",
              Rep(d22, T1Trig, wg, naja, 1, T1F(id22, 0, merc: 2)) + "|" +
              string.Join("|", new[] { 0, 1, 2 }.Select(st => Rep(d22, T1Trig, nav, door, 1, T1F(id22, st)))) + "|" +
              string.Join("|", new[] { 0, 1, 2 }.Select(st => Rep(d22, onZone2, nav, null, 1, T1F(id22, st)))));
        Fake Win(int bf)
        {
            var f = T1F(id22, 2);
            f.PlayerLocalVars["battlefieldWin"] = bf;
            return f;
        }
        Check("c3 T2 toau 22: finishes 1, 2; 32001 completes only for this battlefield win",
              "Player.SetMissionStatus(4,1)|Player.SetMissionStatus(4,2)|Mission.Complete()||",
              T1Run(d22, 0, T1Fin, nav, 1, T1F(id22)) + "|" + T1Run(d22, 0, T1Fin, nav, 2, T1F(id22)) + "|" +
              T1Run(d22, 0, T1Fin, nav, 32001, Win(QC.Battlefield.ShieldOfDiplomacy)) + "|" +
              T1Run(d22, 0, T1Fin, nav, 32001, Win(QC.Battlefield.ShieldOfDiplomacy + 1)) + "|" +
              T1Run(d22, 0, T1Fin, nav, 32001, Win(0)));

        // 23 Social Graces
        var d23 = new QC.Missions.Toau.SocialGraces().Compile();
        int id23 = QC.Mission.Toau.SocialGraces;
        var f23 = T1F(id23);
        f23.UniqueDay = 40;
        Check("c3 T2 toau 23: area 3 plays 3095; update 1 answers, 0 not; finish sets mustZone, Timer, completes",
              "ProgressEvent(3095,0,1,0,0,0,0,0,4)|Player.UpdateEvent(1,1,0,0,0,0,0,0)||Player." +
              "SetLocalVar(Mission[4][23]mustZone,1),Player.SetCharVar(Mission[4][23]Timer,41,0" +
              "),Mission.Complete()",
              Rep(d23, area, wg, null, 3, T1F(id23)) + "|" +
              T1Run(d23, 0, T1Upd, wg, 3095, T1OptionF(id23, 0, 1)) + "|" +
              T1Run(d23, 0, T1Upd, wg, 3095, T1OptionF(id23, 0, 0)) + "|" + T1Run(d23, 0, T1Fin, wg, 3095, f23));

        // 24 Foiled Ambition
        var d24 = new QC.Missions.Toau.FoiledAmbition().Compile();
        int id24 = QC.Mission.Toau.FoiledAmbition;
        Fake Fo(bool mustZone, int timer, int day)
        {
            var f = T1F(id24, 0, mustZone, 5);
            f.MissionVars[$"{ToauLog}/{id24}/Timer"] = timer;
            f.UniqueDay = day;
            return f;
        }
        Check("c3 T2 toau 24: Naja 3096; area 3: clear, mustZone alone, day short alone, day equal",
              "Event(3096,5,1,0,0,0,1,0,2,0)|ProgressEvent(3097,{text_table=0})|null|null|" +
              "ProgressEvent(3097,{text_table=0})",
              Rep(d24, T1Trig, wg, naja, 1, Fo(false, 0, 0)) + "|" +
              string.Join("|", new[] { Fo(false, 5, 9), Fo(true, 5, 9), Fo(false, 5, 4), Fo(false, 5, 5) }
                  .Select(f => Rep(d24, area, wg, null, 3, f))));
        var fo24b = Fo(false, 0, 7);
        fo24b.CompleteResult = false;
        Check("c3 T2 toau 24: update 3097 answers 78, 1 for any option; finish; not when the completion fails",
              "Player.UpdateEvent(78,1,0,0,0,0,0,0)|Player.UpdateEvent(78,1,0,0,0,0,0,0)|Missio" +
              "n.Complete(),Player.SetLocalVar(Mission[4][24]mustZone,1),Player.SetCharVar(Miss" +
              "ion[4][24]Timer,8,0)|Mission.Complete()",
              T1Run(d24, 0, T1Upd, wg, 3097, T1OptionF(id24, 0, 0)) + "|" +
              T1Run(d24, 0, T1Upd, wg, 3097, T1OptionF(id24, 0, 3)) + "|" +
              T1Run(d24, 0, T1Fin, wg, 3097, Fo(false, 0, 7)) + "|" + T1Run(d24, 0, T1Fin, wg, 3097, fo24b));
        Check("c3 T2 toau 24: the table reward: five Imperial Gold Pieces and the title",
              "1 2187 5 True",
              Reward24(d24));
        var p24 = Patched(new QC.Missions.Toau.FoiledAmbition(), QC.MissionKey.Toau.FoiledAmbition);
        var f24 = Fo(false, 0, 7);
        f24.NextJstMidnight = 777;
        var f24b = Fo(false, 0, 7);
        f24b.CompleteResult = false;
        Check("c3 T2 adjustments toau 24: finish 3097 sets mustZone and the Timer 1 to JST midnight; not on failure",
              "Mission.Complete(),Player.SetLocalVar(Mission[4][24]mustZone,1),Player.SetCharVa" +
              "r(Mission[4][24]Timer,1,777)|Mission.Complete()",
              T1Run(p24, 0, T1Fin, wg, 3097, f24) + "|" + T1Run(p24, 0, T1Fin, wg, 3097, f24b));

        // 25 Playing the Part
        var d25 = new QC.Missions.Toau.PlayingThePart().Compile();
        int id25 = QC.Mission.Toau.PlayingThePart;
        Fake Pl(bool mustZone, int timer, int day, int option = 0)
        {
            var f = T1F(id25, 0, mustZone, 2);
            f.MissionVars[$"{ToauLog}/{id25}/Timer"] = timer;
            f.MissionVars[$"{ToauLog}/{id25}/Option"] = option;
            f.UniqueDay = day;
            return f;
        }
        Check("c3 T2 toau 25: Naja: clear; mustZone alone; day short alone; day equal",
              "ProgressEvent(3110)|Event(3098,2,1,0,0,0,0,0,1,0)|Event(3098,2,1,0,0,0,0,0,1,0)|" +
              "ProgressEvent(3110)",
              string.Join("|", new[] { Pl(false, 5, 9), Pl(true, 5, 9), Pl(false, 5, 4), Pl(false, 5, 5) }
                  .Select(f => Rep(d25, T1Trig, wg, naja, 1, f))));
        Check("c3 T2 toau 25: Naja's dialog for Option 0, 1, 2 and its writes; finish 3110",
              "Event(3098,2,1,0,0,0,0,0,1,0)#Mission.SetVar(Option,1)|Event(3098,2,1,0,0,0,0,0," +
              "2,0)#Mission.SetVar(Option,0)|Event(3098,2,1,0,0,0,0,0,3,0)#Mission.SetVar(Optio" +
              "n,0)|Mission.Complete()",
              string.Join("|", new[] { 0, 1, 2 }.Select(o =>
              {
                  var f = Pl(true, 0, 0, o);
                  return Rep(d25, T1Trig, wg, naja, 1, f) + "#" + string.Join(",", f.Calls);
              })) + "|" + T1Run(d25, 0, T1Fin, wg, 3110, Pl(false, 0, 0)));
        var p25 = Patched(new QC.Missions.Toau.PlayingThePart(), QC.MissionKey.Toau.PlayingThePart);
        Check("c3 T2 adjustments toau 25: Naja: the Timer 1 blocks; mustZone blocks; clear plays 3110; dialog",
              "Event(3098,2,1,0,0,0,0,0,1,0)|Event(3098,2,1,0,0,0,0,0,1,0)|ProgressEvent(3110)|" +
              "Event(3098,2,1,0,0,0,0,0,2,0)",
              string.Join("|", new[] { Pl(false, 1, 0), Pl(true, 0, 0), Pl(false, 0, 0), Pl(true, 0, 0, 1) }
                  .Select(f => Rep(p25, T1Trig, wg, naja, 1, f))));
        var fp25 = Pl(true, 0, 0, 1);
        Rep(p25, T1Trig, wg, naja, 1, fp25);
        Check("c3 T2 adjustments toau 25: Naja's dialog cycle writes Option 0 after dialog 2",
              "Mission.SetVar(Option,0)", string.Join(",", fp25.Calls));

        // the battlefield
        var bd = (BattlefieldMissionData)new QC.Battlefields.NavukgoExecutionChamber.ShieldOfDiplomacy().Data;
        Check("c3 T2 battlefield: the data",
              "64 1124 4 1800 6 MAX_LEVEL 2 False True 1 1 3 1 3",
              $"{bd.ZoneId} {bd.BattlefieldId} {bd.Index} {bd.TimeLimit} {bd.MaxPlayers} {bd.LevelCapSetting} " +
              $"{bd.RequiredMissionStatus} {bd.CanLoseExp} {bd.AllowTrusts} {string.Join(",", bd.AllowedAreas)} " +
              $"{bd.EntryNpcs.Length} {bd.ExitNpcs.Length} {bd.Groups.Count} {bd.Groups[0].AreaMobIds.Length}");
        Check("c3 T2 battlefield: one Khimaira list per area, its id offset 0, 2, 4",
              "KHIMAIRA_13|KHIMAIRA_13+2|KHIMAIRA_13+4",
              string.Join("|", bd.Groups[0].AreaMobIds.Select(a => string.Join(",", a))));
        var sd = new QC.Battlefields.NavukgoExecutionChamber.ShieldOfDiplomacy();
        foreach (var ar in new[] { 1, 2, 3 })
        {
            var view = new BattlefieldViewFake { Area = ar };
            var kara = new BattlefieldMobFake { Id = 8 };
            view.InsertResults[8] = kara;
            sd.SetupBattlefield(new BattlefieldContextFake(sd) { Battlefield = view });
            Check($"c3 T2 battlefield: setup area {ar} inserts Karababa (8) as an ally, spot, spawn, level 75",
                  "Battlefield.InsertEntity(8,True,True)|Mob.SetSpawn(" +
                  (ar == 1 ? "361.488,-116.5,382.511,0" : "0,0,0,0") + "),Mob.Spawn(,),Mob.SetMobLevel(75)",
                  string.Join(",", view.Calls) + "|" + string.Join(",", kara.Calls));
        }
    }

    static Fake Ranked(Fake f, int rank)
    {
        f.MercRank = rank;
        return f;
    }

    // the section's check with another mission current
    static string Fresh18(MissionScriptDefinition p, int id)
    {
        var f = T1F(id, 0);
        f.CurrentMissions[ToauLog] = id + 1;
        return p.States[0].MissionMatches.All(m => m.When == null || m.When(f)).ToString();
    }

    static string Reward24(MissionScriptDefinition d)
    {
        var r = d.Reward;
        return $"{r.Items.Length} {r.Items[0].Id} {r.Items[0].Quantity} {r.Title == QC.Title.KarababasSecretAgent}";
    }

    static string Rep17(MissionScriptDefinition d, string entity, Fake f) =>
        T1Reply(d, 0, T1Trig, QC.Zone.AhtUrhganWhitegate, entity, 1, f);

    static string CallsOf(MissionScriptDefinition d, QuestHandlerKind k, ushort zone, Fake f)
    {
        d.States[0].Handlers.Single(h => h.Kind == k && h.Zone == zone && h.Entity == null).Reply(f);
        return string.Join(",", f.Calls);
    }

    static Fake Armored(Fake f)
    {
        f.RoyalPalaceArmorOk = true;
        return f;
    }

    static string RecordHidden()
    {
        var f = new Fake();
        f.StartEventHiddenParam(34, 7, 1);
        return string.Join(";", f.Calls);
    }
}
