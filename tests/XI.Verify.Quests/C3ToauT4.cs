// XI.Verify.Quests -- quest rework C3 T4 (ToAU 39-48 on C#): the arms the golden flows reach only together, each on
// the one read that decides it, and values the replay sees only in part (the rewards, the positions, the Ring var and
// its expiry, the new facets CurrentTitle and InstanceOnEventFinish). The traces hold the packets.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    internal static void RunC3ToauT4()
    {
        Console.WriteLine("\n== c3 T4: ToAU 39-48 on C# ==");
        var all = new MissionScript[]
        {
            new QC.Missions.Toau.AlliedRumblings(), new QC.Missions.Toau.UnravelingReason(),
            new QC.Missions.Toau.LightOfJudgement(), new QC.Missions.Toau.PathOfDarkness(),
            new QC.Missions.Toau.FangsOfTheLion(), new QC.Missions.Toau.NashmeirasPlea(),
            new QC.Missions.Toau.Ragnarok(), new QC.Missions.Toau.ImperialCoronation(),
            new QC.Missions.Toau.TheEmpressCrowned(), new QC.Missions.Toau.EternalMercenary(),
        };
        Check("c3 T4 toau: ten missions, each with a legacy path", "10 10",
              $"{all.Length} {all.Count(m => !string.IsNullOrEmpty(m.LegacyPath))}");

        RunC3ToauT4First();
        RunC3ToauT4Instance();
        RunC3ToauT4Coronation();
        RunC3ToauT4Rewards();
        RunC3ToauT4Facets();
    }

    static string T4Rep(MissionScriptDefinition d, QuestHandlerKind k, ushort z, string e, int id, Fake f,
        int st = 0) =>
        T1Reply(d, st, k, z, e, id, f);

    static string T4Fin(MissionScriptDefinition d, ushort z, int csid, Fake f, int option = 0, int st = 0)
    {
        f.Option = option;
        return T1Run(d, st, T1Fin, z, csid, f);
    }

    static void RunC3ToauT4First()
    {
        ushort rg = QC.Zone.RuludeGardens, waj = QC.Zone.WajaomWoodlands;

        // 39 Allied Rumblings: the finish completes, then the mustZone local var and the Timer; not when it fails
        var d39 = new QC.Missions.Toau.AlliedRumblings().Compile();
        int id39 = QC.Mission.Toau.AlliedRumblings;
        var f39 = T1F(id39);
        f39.UniqueDay = 40;
        var f39b = T1F(id39);
        f39b.UniqueDay = 40;
        f39b.CompleteResult = false;
        Check("c3 T4 toau 39: finish 10097 completes, then the mustZone LOCAL var (a kept Lua bug) and the Timer; " +
              "not when the completion fails",
              "Mission.Complete(),Player.SetLocalVar(Mission[4][39]mustZone,1),Player.SetCharVar(Mission[4][39]Timer," +
              "41,0)|Mission.Complete()",
              T4Fin(d39, rg, 10097, f39) + "|" + T4Fin(d39, rg, 10097, f39b));
        Check("c3 T4 toau 39: Ru'Lude Gardens area 1 plays the progress event 10097",
              "ProgressEvent(10097,0,0,0,0,0,0,0,0)",
              T4Rep(d39, T1Area, rg, null, 1, T1F(id39)));

        // 40 Unraveling Reason: Pherimociel's three reads, each part alone
        var d40 = new QC.Missions.Toau.UnravelingReason().Compile();
        int id40 = QC.Mission.Toau.UnravelingReason;
        var pher = QC.Npc.RuludeGardens.Pherimociel;
        Fake U(int status, bool mustZone, int timer, int day, int seen = 0)
        {
            var f = T1F(id40, status, mustZone);
            f.MissionVars[$"{ToauLog}/{id40}/Timer"] = timer;
            f.UniqueDay = day;
            f.PlayerLocalVars["Mission[4][39]seenCS"] = seen;
            return f;
        }
        Check("c3 T4 toau 40: Pherimociel: ready 10098; mustZone alone 10099; day short alone 10099; " +
              "day equal 10098; " +
              "seen alone none; status 1 none",
              "ProgressEvent(10098)|Event(10099)|Event(10099)|ProgressEvent(10098)|null|null",
              string.Join("|", new[]
              {
                  U(0, false, 5, 9), U(0, true, 5, 9), U(0, false, 5, 4), U(0, false, 5, 5), U(0, true, 5, 4, 1),
                  U(1, false, 5, 9),
              }.Select(f => T4Rep(d40, T1Trig, rg, pher, 1, f))));
        Check("c3 T4 toau 40: seenCS alone silences 10099 (ready still answers 10098)",
              "null|ProgressEvent(10098)",
              T4Rep(d40, T1Trig, rg, pher, 1, U(0, false, 5, 4, 1)) + "|" +
              T4Rep(d40, T1Trig, rg, pher, 1, U(0, false, 5, 9, 1)));
        Check("c3 T4 toau 40: finish 10098: option 1 sets status 1 and moves to Wajaom; others nothing; " +
              "finish 10099 sets seenCS and mustZone",
              "Player.SetMissionStatus(4,1),Player.SetPos(-200.036,-10,79.948,254," + waj + ")||" +
              "Player.SetLocalVar(Mission[4][39]seenCS,1),Mission.SetMustZone()",
              T4Fin(d40, rg, 10098, U(0, false, 0, 0), 1) + "|" + T4Fin(d40, rg, 10098, U(0, false, 0, 0), 0) + "|" +
              T4Fin(d40, rg, 10099, U(0, false, 0, 0)));
        Check("c3 T4 toau 40: Wajaom zone-in by status 1, 2, 3, 0",
              "Event(11)|Event(21)|Event(22)|null",
              string.Join("|", new[] { 1, 2, 3, 0 }.Select(st =>
                  T4Rep(d40, QuestHandlerKind.OnZoneIn, waj, null, 1, U(st, false, 0, 0)))));
        Check("c3 T4 toau 40: update 11 and 21: option 0 answers (0,4,...,3); option 1 nothing",
              "Player.UpdateEvent(0,4,0,0,0,0,0,3)|Player.UpdateEvent(0,4,0,0,0,0,0,3)||",
              string.Join("|", new[] { (11, 0), (21, 0), (11, 1), (21, 1) }.Select(p =>
              {
                  var f = U(1, false, 0, 0);
                  f.Option = p.Item2;
                  return C2MRun(C2MHandler(d40, 0, T1Upd, waj, null, p.Item1), f);
              })));
        Check("c3 T4 toau 40: finish 11 and 21 set the status and move; finish 22 completes",
              "Player.SetMissionStatus(4,2),Player.SetPos(-200.036,-10,79.948,254," + waj + ")|" +
              "Player.SetMissionStatus(4,3),Player.SetPos(-200.036,-10,79.948,254," + waj + ")|Mission.Complete()",
              T4Fin(d40, waj, 11, U(1, false, 0, 0)) + "|" + T4Fin(d40, waj, 21, U(2, false, 0, 0)) + "|" +
              T4Fin(d40, waj, 22, U(3, false, 0, 0)));

        // 41 Light of Judgement: the finish completes on option 0 only
        var d41 = new QC.Missions.Toau.LightOfJudgement().Compile();
        int id41 = QC.Mission.Toau.LightOfJudgment;
        Check("c3 T4 toau 41: finish 3137 completes on option 0 only",
              "Mission.Complete()||",
              T4Fin(d41, QC.Zone.AhtUrhganWhitegate, 3137, T1F(id41), 0) + "|" +
              T4Fin(d41, QC.Zone.AhtUrhganWhitegate, 3137, T1F(id41), 1) + "|" +
              T4Fin(d41, QC.Zone.AhtUrhganWhitegate, 3137, T1F(id41), 2));
        Check("c3 T4 toau 41: Rodin plays the progress event 3137 with text table 0",
              "ProgressEvent(3137,{text_table=0})",
              T4Rep(d41, T1Trig, QC.Zone.AhtUrhganWhitegate, QC.Npc.AhtUrhganWhitegate.RodinComidin, 1, T1F(id41)));

        // 43 Fangs of the Lion, 45 Ragnarok, 47 The Empress Crowned: the update and the finish
        var wg = QC.Zone.AhtUrhganWhitegate;
        var d43 = new QC.Missions.Toau.FangsOfTheLion().Compile();
        Check("c3 T4 toau 43: area 3 plays 3138 with the rank; update answers (0,1); finish completes",
              "ProgressEvent(3138,3,1,0,0,0,0,0,0,0)|Player.UpdateEvent(0,1,0,0,0,0,0,0)|Mission.Complete()",
              T4Rep(d43, T1Area, wg, null, 3, T1F(QC.Mission.Toau.FangsOfTheLion)) + "|" +
              C2MRun(C2MHandler(d43, 0, T1Upd, wg, null, 3138), T1F(QC.Mission.Toau.FangsOfTheLion)) + "|" +
              T4Fin(d43, wg, 3138, T1F(QC.Mission.Toau.FangsOfTheLion)));
        var d45 = new QC.Missions.Toau.Ragnarok().Compile();
        Check("c3 T4 toau 45: area 3 plays 3139; finish completes",
              "ProgressEvent(3139,0,1,0,0,0,0,0,0,0)|Mission.Complete()",
              T4Rep(d45, T1Area, wg, null, 3, T1F(QC.Mission.Toau.Ragnarok)) + "|" +
              T4Fin(d45, wg, 3139, T1F(QC.Mission.Toau.Ragnarok)));
        var d47 = new QC.Missions.Toau.TheEmpressCrowned().Compile();
        Check("c3 T4 toau 47: area 3 plays 3144; update answers (0,1,0,0,1,1); finish completes",
              "ProgressEvent(3144,3,1,0,0,0,0,0,0,0)|Player.UpdateEvent(0,1,0,0,1,1,0,0)|Mission.Complete()",
              T4Rep(d47, T1Area, wg, null, 3, T1F(QC.Mission.Toau.TheEmpressCrowned)) + "|" +
              C2MRun(C2MHandler(d47, 0, T1Upd, wg, null, 3144), T1F(QC.Mission.Toau.TheEmpressCrowned)) + "|" +
              T4Fin(d47, wg, 3144, T1F(QC.Mission.Toau.TheEmpressCrowned)));
        var d48 = new QC.Missions.Toau.EternalMercenary().Compile();
        Check("c3 T4 toau 48: Naja plays 3151 with the rank and replaces the default",
              "Event(3151,3,1,0,0,0,0,0,0,0):replaceDefault()",
              T4Rep(d48, T1Trig, wg, QC.Npc.AhtUrhganWhitegate.NajaSalaheem, 1, T1F(QC.Mission.Toau.EternalMercenary)));
    }

    static void RunC3ToauT4Instance()
    {
        ushort wg = QC.Zone.AhtUrhganWhitegate, alz = QC.Zone.AlzadaalUnderseaRuins;
        var naja = QC.Npc.AhtUrhganWhitegate.NajaSalaheem;
        var rodin = QC.Npc.AhtUrhganWhitegate.RodinComidin;
        var lamp = QC.Npc.AlzadaalUnderseaRuins.blanklamp;

        // 42 Path of Darkness
        var d42 = new QC.Missions.Toau.PathOfDarkness().Compile();
        int id42 = QC.Mission.Toau.PathOfDarkness;
        Fake P(int status, bool key = false, int blocked = 0)
        {
            var f = T1F(id42, status);
            f.KeyItems[QC.KeyItem.NyzulIsleRoute] = key;
            f.MissionLocalVars["blockedDialog"] = blocked;
            return f;
        }
        Check("c3 T4 toau 42: Rodin: no key 3142; key 3141 (both with text table 0)",
              "ProgressEvent(3142,{text_table=0})|ProgressEvent(3141,{text_table=0})",
              T4Rep(d42, T1Trig, wg, rodin, 1, P(0)) + "|" + T4Rep(d42, T1Trig, wg, rodin, 1, P(0, true)));
        Check("c3 T4 toau 42: area 3: status 0 none; status 1 plays 3143 with blockedDialog 0; with it 1",
              "null|ProgressEvent(3143,0,1,0,0,0,0,0,0,0)|ProgressEvent(3143,0,1,0,0,0,0,1,0,0)",
              T4Rep(d42, T1Area, wg, null, 3, P(0)) + "|" + T4Rep(d42, T1Area, wg, null, 3, P(1)) + "|" +
              T4Rep(d42, T1Area, wg, null, 3, P(1, false, 1)));
        var f3143 = P(1);
        f3143.Option = 1;
        Check("c3 T4 toau 42: update 3143 option 0 answers (0,1); option 1 nothing; finish 3142 gives the key; " +
              "finish 3143 sets blockedDialog and moves",
              "Player.UpdateEvent(0,1,0,0,0,0,0,0)||Player.GiveKeyItem(" + QC.KeyItem.NyzulIsleRoute + ")|" +
              "Mission.SetLocalVar(blockedDialog,1),Player.SetPos(23.978,-6,-64.624,63)",
              C2MRun(C2MHandler(d42, 0, T1Upd, wg, null, 3143), P(1)) + "|" +
              C2MRun(C2MHandler(d42, 0, T1Upd, wg, null, 3143), f3143) + "|" + T4Fin(d42, wg, 3142, P(0)) + "|" +
              T4Fin(d42, wg, 3143, P(1)));
        Check("c3 T4 toau 42: lamp: status 0 plays 6; status 1 none; zone-in at status 2 plays 7; 0 none",
              "ProgressEvent(6)|null|Event(7)|null",
              T4Rep(d42, T1Trig, alz, lamp, 1, P(0)) + "|" + T4Rep(d42, T1Trig, alz, lamp, 1, P(1)) + "|" +
              T4Rep(d42, QuestHandlerKind.OnZoneIn, alz, null, 1, P(2)) + "|" +
              T4Rep(d42, QuestHandlerKind.OnZoneIn, alz, null, 1, P(0)));
        Check("c3 T4 toau 42: finish 6 option 0 sets status 1, option 1 nothing; finish 7 completes",
              "Player.SetMissionStatus(4,1)||Mission.Complete()",
              T4Fin(d42, alz, 6, P(0), 0) + "|" + T4Fin(d42, alz, 6, P(0), 1) + "|" + T4Fin(d42, alz, 7, P(2)));
        Fake I(int instanceId)
        {
            var f = P(0);
            f.PlayerLocalVars["INSTANCE_ID"] = instanceId;
            return f;
        }
        Check("c3 T4 toau 42: finish 116: option 1 with INSTANCE_ID 7700 starts the instance; option 0 alone no; " +
              "INSTANCE_ID 7701 alone no",
              "Player.InstanceOnEventFinish(116,1)|||",
              T4Fin(d42, alz, 116, I(7700), 1) + "|" + T4Fin(d42, alz, 116, I(7700), 0) + "|" +
              T4Fin(d42, alz, 116, I(7701), 1) + "|" + T4Fin(d42, alz, 116, I(0), 1));

        // 44 Nashmeira's Plea
        var d44 = new QC.Missions.Toau.NashmeirasPlea().Compile();
        int id44 = QC.Mission.Toau.NashmeirasPlea;
        Fake N(int status, bool key = false)
        {
            var f = T1F(id44, status);
            f.KeyItems[QC.KeyItem.MythrilMirror] = key;
            return f;
        }
        Check("c3 T4 toau 44: Naja: no mirror 3156 (progress); mirror 3149; finish 3156 gives the mirror",
              "ProgressEvent(3156,3,1,0,0,0,0,0,0,0)|Event(3149,3,1,0,0,0,0,0,0,0)|Player.GiveKeyItem(" +
              QC.KeyItem.MythrilMirror + ")",
              T4Rep(d44, T1Trig, wg, naja, 1, N(0)) + "|" + T4Rep(d44, T1Trig, wg, naja, 1, N(0, true)) + "|" +
              T4Fin(d44, wg, 3156, N(0)));
        Check("c3 T4 toau 44: lamp: status 0 plays 8; status 1 none; zone-in at status 2 plays 10; 0 none",
              "ProgressEvent(8,0,4,0,0,0,1,0,0)|null|Event(10)|null",
              T4Rep(d44, T1Trig, alz, lamp, 1, N(0)) + "|" + T4Rep(d44, T1Trig, alz, lamp, 1, N(1)) + "|" +
              T4Rep(d44, QuestHandlerKind.OnZoneIn, alz, null, 1, N(2)) + "|" +
              T4Rep(d44, QuestHandlerKind.OnZoneIn, alz, null, 1, N(0)));
        Check("c3 T4 toau 44: finish 8 option 0 sets status 1, option 1 nothing; finish 10 completes",
              "Player.SetMissionStatus(4,1)||Mission.Complete()",
              T4Fin(d44, alz, 8, N(0), 0) + "|" + T4Fin(d44, alz, 8, N(0), 1) + "|" + T4Fin(d44, alz, 10, N(2)));
        Fake J(int instanceId)
        {
            var f = N(0);
            f.PlayerLocalVars["INSTANCE_ID"] = instanceId;
            return f;
        }
        Check("c3 T4 toau 44: finish 116: option 1 with INSTANCE_ID 7701 starts the instance; option 0 alone no; " +
              "INSTANCE_ID 7700 alone no",
              "Player.InstanceOnEventFinish(116,1)|||",
              T4Fin(d44, alz, 116, J(7701), 1) + "|" + T4Fin(d44, alz, 116, J(7701), 0) + "|" +
              T4Fin(d44, alz, 116, J(7700), 1) + "|" + T4Fin(d44, alz, 116, J(0), 1));
    }

    static void RunC3ToauT4Coronation()
    {
        ushort wg = QC.Zone.AhtUrhganWhitegate;
        var imp = QC.Npc.AhtUrhganWhitegate.ImperialWhitegate;
        var d = new QC.Missions.Toau.ImperialCoronation().Compile();
        int id = QC.Mission.Toau.ImperialCoronation;
        Fake C(bool dressed, int main = 0, int sub = 0, int title = 0)
        {
            var f = T1F(id);
            f.RoyalPalaceArmorOk = dressed;
            f.Equipment[QC.Slot.Main] = main;
            f.Equipment[QC.Slot.Sub] = sub;
            f.CurrentTitleId = title;
            return f;
        }
        Check("c3 T4 toau 46: the Imperial: bare and dressed 3140 with the title; main alone, sub alone, undressed " +
              "alone: 3081",
              "ProgressEvent(3140,0,5,0,0,0,0,0,1,0)|Event(3081,0,1,0,0,0,0,0,0,1,0)|Event(3081,0,1,0,0,0,0,0,0,1,0)|" +
              "Event(3081,0,1,0,0,0,0,0,0,0,0)",
              string.Join("|", new[] { C(true, 0, 0, 5), C(true, 17000), C(true, 0, 17001), C(false) }
                  .Select(f => T4Rep(d, T1Trig, wg, imp, 1, f))));
        Check("c3 T4 toau 46: Naja plays 3150 with the rank",
              "Event(3150,3,1,0,0,0,0,0,0,0)",
              T4Rep(d, T1Trig, wg, QC.Npc.AhtUrhganWhitegate.NajaSalaheem, 1, T1F(id)));
        Fake S2(bool dressed, int main = 0, int sub = 0, bool standard = false, int ring = 0)
        {
            var f = C(dressed, main, sub);
            f.CompletedMissions.Add($"{ToauLog}/{id}");
            f.CurrentMissions[ToauLog] = id + 1;
            if (standard) f.Items.Add(QC.Item.ImperialStandard);
            f.CharVars["Mission[4][45]Ring"] = ring;
            return f;
        }
        string Sec2(Fake f) => d.States[1].MissionMatches.All(m => m.When == null || m.When(f)).ToString();
        Check("c3 T4 toau 46: section 2 holds only once the mission is completed",
              "True|False",
              Sec2(S2(true)) + "|" + Sec2(C(true)));
        Check("c3 T4 toau 46 section 2: bare and dressed 3155 (standard 1, ring 1); standard held 0; ring held 0; " +
              "main alone, sub alone, undressed alone none",
              "Event(3155,1,1,0,0,0,0,0,1,0)|Event(3155,0,1,0,0,0,0,0,1,0)|" +
              "Event(3155,1,0,0,0,0,0,0,1,0)|null|null|null",
              string.Join("|", new[]
              {
                  S2(true), S2(true, standard: true), S2(true, ring: 9), S2(true, 17000), S2(true, 0, 17001),
                  S2(false),
              }.Select(f => T4Rep(d, T1Trig, wg, imp, 1, f, 1))));
    }

    static void RunC3ToauT4Rewards()
    {
        ushort wg = QC.Zone.AhtUrhganWhitegate;
        var d = new QC.Missions.Toau.ImperialCoronation().Compile();
        int id = QC.Mission.Toau.ImperialCoronation;
        string Upd(int option, bool give, int tally, int state = 0)
        {
            var f = T1F(id);
            f.GiveResult = give;
            f.Tally = tally;
            f.Option = option;
            return C2MRun(C2MHandler(d, state, T1Upd, wg, null, state == 0 ? 3140 : 3155), f);
        }
        Check("c3 T4 toau 46 section 1: update 3140: 0 rejects; 1 gives Balrahn's ring then the Ring var to the " +
              "tally with the same expiry; 1 when the give fails still answers (1,1); 2 and 3 only on a give; " +
              "4 the Standard, (0,0) when it fails; 99 offers the rings",
              $"Player.UpdateEvent(0,1,0,0,0,0,0,0)|Player.GiveItem({QC.Item.BalrahnsRing}),Player.UpdateEvent(1,1,0," +
              $"0,0,0,0,0),Player.SetCharVar(Mission[4][45]Ring,77,77)|Player.GiveItem({QC.Item.BalrahnsRing})," +
              $"Player.UpdateEvent(1,1,0,0,0,0,0,0)|" +
              $"Player.GiveItem({QC.Item.UlthalamsRing}),Player.UpdateEvent(1,1,0," +
              $"0,0,0,0,0),Player.SetCharVar(Mission[4][45]Ring,77,77)|Player.GiveItem({QC.Item.UlthalamsRing})|" +
              $"Player.GiveItem({QC.Item.JalzahnsRing}),Player.UpdateEvent(1,1,0,0,0,0,0,0),Player.SetCharVar(" +
              $"Mission[4][45]Ring,77,77)|Player.GiveItem({QC.Item.ImperialStandard}),Player.UpdateEvent(1,1,0,0,0,0," +
              $"0,0)|Player.GiveItem({QC.Item.ImperialStandard}),Player.UpdateEvent(0,0,0,0,0,0,0,0)|" +
              $"Player.UpdateEvent({QC.Item.BalrahnsRing},{QC.Item.UlthalamsRing},{QC.Item.JalzahnsRing},0,0,0,0,0)",
              string.Join("|", new[]
              {
                  Upd(0, true, 77), Upd(1, true, 77), Upd(1, false, 77), Upd(2, true, 77), Upd(2, false, 77),
                  Upd(3, true, 77), Upd(4, true, 77), Upd(4, false, 77), Upd(99, true, 77),
              }));
        Check("c3 T4 toau 46 section 2: update 3155: 1 gives the ring and keeps its id in the Ring var; 6 the " +
              "Standard; option 4 nothing",
              $"Player.GiveItem({QC.Item.BalrahnsRing}),Player.UpdateEvent(1,1,0,0,0,0,0,0),Player.SetCharVar(" +
              $"Mission[4][45]Ring,{QC.Item.BalrahnsRing},0)|Player.GiveItem({QC.Item.UlthalamsRing})," +
              $"Player.UpdateEvent(1,1,0,0,0,0,0,0),Player.SetCharVar(Mission[4][45]Ring,{QC.Item.UlthalamsRing},0)|" +
              $"Player.GiveItem({QC.Item.JalzahnsRing}),Player.UpdateEvent(1,1,0,0,0,0,0,0),Player.SetCharVar(" +
              $"Mission[4][45]Ring,{QC.Item.JalzahnsRing},0)|Player.GiveItem({QC.Item.ImperialStandard})," +
              $"Player.UpdateEvent(1,1,0,0,0,0,0,0)|Player.GiveItem({QC.Item.ImperialStandard})," +
              $"Player.UpdateEvent(0,0,0,0,0,0,0,0)|",
              string.Join("|", new[]
              {
                  Upd(1, true, 77, 1), Upd(2, true, 77, 1), Upd(3, true, 77, 1), Upd(6, true, 77, 1),
                  Upd(6, false, 77, 1), Upd(4, true, 77, 1),
              }));

        // the rewards
        string Rw(MissionScript m)
        {
            var r = m.Compile().Reward;
            return $"{r.Title ?? 0}/{r.KeyItem ?? 0}/{r.Item ?? 0}/{r.NextMission}";
        }
        Check("c3 T4 toau: the rewards (title, key item, item, next mission)",
              $"0/0/0/{QC.Mission.Toau.UnravelingReason}|{QC.Title.EndymionParatrooper}/0/0/{QC.Mission.Toau.LightOfJudgment}|" +
              $"0/{QC.KeyItem.NyzulIsleRoute}/0/{QC.Mission.Toau.PathOfDarkness}|" +
              $"{QC.Title.NajasComradeInArms}/0/0/{QC.Mission.Toau.FangsOfTheLion}|" +
              $"{QC.Title.NashmeirasLoyalist}/{QC.KeyItem.MythrilMirror}/0/{QC.Mission.Toau.NashmeirasPlea}|" +
              $"{QC.Title.PreventerOfRagnarok}/0/0/{QC.Mission.Toau.Ragnarok}|0/0/0/{QC.Mission.Toau.ImperialCoronation}|" +
              $"0/0/0/{QC.Mission.Toau.TheEmpressCrowned}|" +
              $"{QC.Title.EternalMercenary}/0/{QC.Item.GloryCrown}/{QC.Mission.Toau.EternalMercenary}",
              string.Join("|", new MissionScript[]
              {
                  new QC.Missions.Toau.AlliedRumblings(), new QC.Missions.Toau.UnravelingReason(),
                  new QC.Missions.Toau.LightOfJudgement(), new QC.Missions.Toau.PathOfDarkness(),
                  new QC.Missions.Toau.FangsOfTheLion(), new QC.Missions.Toau.NashmeirasPlea(),
                  new QC.Missions.Toau.Ragnarok(), new QC.Missions.Toau.ImperialCoronation(),
                  new QC.Missions.Toau.TheEmpressCrowned(),
              }.Select(Rw)));
    }

    static void RunC3ToauT4Facets()
    {
        var f = new Fake { CurrentTitleId = 12 };
        Check("c3 T4 facets: CurrentTitle reads the fake's title; InstanceOnEventFinish records its two operands",
              "12|Player.InstanceOnEventFinish(116,1)",
              $"{f.CurrentTitle}|{RecordInstance(f)}");
    }

    static string RecordInstance(Fake f)
    {
        f.InstanceOnEventFinish(116, 1);
        return string.Join(",", f.Calls);
    }
}
