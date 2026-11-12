// XI.Verify.Quests -- quest rework C3 T8 (WotG missions 42-54 on C#): the arms and values the golden flows cannot show
// (the reply kinds and modifiers, the Walk of Echoes 32001 finishes no flow opens, the set-downs on the same spot, the
// Timer boundary of Maiden of the Dusk, the guarded effects after a completion, the Moonshade Earring's augments,
// the afterZoneIn parts, Lest We Forget's reset, the facet members T8 adds). The traces hold the packets.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;
using GR = XI.QuestContent.Npc.GraubergS;
using MW = XI.QuestContent.Mission.Wotg;
using W = XI.QuestContent.Missions.Wotg;

internal static partial class Program
{
    internal static void RunC3ToauT8()
    {
        Console.WriteLine("\n== c3 T8: WotG 42-54 on C# ==");
        var all = new MissionScript[]
        {
            new W.ForgetMeNot(), new W.PillarOfHope(), new W.GlimmerOfLife(), new W.TimeSlipsAway(),
            new W.WhenWillsCollide(), new W.WhispersOfDawn(), new W.ADreamyInterlude(), new W.CaitInTheWoods(),
            new W.ForkInTheRoad(), new W.MaidenOfTheDusk(), new W.WhereItAllBegan(), new W.ATokenOfTroth(),
            new W.LestWeForget(),
        };
        string[] files =
        {
            "42_Forget_Me_Not", "43_Pillar_of_Hope", "44_Glimmer_of_Life", "45_Time_Slips_Away",
            "46_When_Wills_Collide", "47_Whispers_of_Dawn", "48_A_Dreamy_Interlude", "49_Cait_in_the_Woods",
            "50_Fork_in_the_Road", "51_Maiden_of_the_Dusk", "52_Where_It_All_Began", "53_Token_of_Troth",
            "54_Lest_We_Forget",
        };
        Check("c3 T8 wotg: 13 missions: clean, each LegacyPath its Lua file's require name",
              string.Join(",", files.Select(f => $"|scripts/missions/wotg/{f}")),
              string.Join(",", all.Select(m => $"{string.Join(" | ", QuestScriptValidator.Validate(m.Compile()))}|" +
                  $"{m.LegacyPath}")));
        Check("c3 T8 wotg: the mission keys",
              "FORGET_ME_NOT,PILLAR_OF_HOPE,GLIMMER_OF_LIFE,TIME_SLIPS_AWAY,WHEN_WILLS_COLLIDE,WHISPERS_OF_DAWN," +
              "A_DREAMY_INTERLUDE,CAIT_IN_THE_WOODS,FORK_IN_THE_ROAD,MAIDEN_OF_THE_DUSK,WHERE_IT_ALL_BEGAN," +
              "A_TOKEN_OF_TROTH,LEST_WE_FORGET",
              string.Join(",", all.Select(m => m.Key.Split('.').Last().ToUpperInvariant())));
        // the rewards (key item / title / next log / next mission); Lest We Forget's table is empty (no reward)
        Check("c3 T8 wotg: the rewards",
              $"-/-/WoTG/{MW.PillarOfHope}|-/-/WoTG/{MW.GlimmerOfLife}|-/-/WoTG/{MW.TimeSlipsAway}|" +
              $"{QC.KeyItem.BottledPunchBug}/-/WoTG/{MW.WhenWillsCollide}|-/-/WoTG/{MW.WhispersOfDawn}|" +
              $"-/-/WoTG/{MW.ADreamyInterlude}|-/-/WoTG/{MW.CaitInTheWoods}|" +
              $"{QC.KeyItem.RonfaureDawndrop}/-/WoTG/{MW.ForkInTheRoad}|" +
              $"{QC.KeyItem.PrimalGlow}/-/WoTG/{MW.MaidenOfTheDusk}|" +
              $"{QC.KeyItem.MoonshadeEarring}/-/WoTG/{MW.WhereItAllBegan}|" +
              $"{QC.KeyItem.WeddingInvitation}/-/WoTG/{MW.ATokenOfTroth}|-/-/WoTG/{MW.LestWeForget}|none",
              string.Join("|", all.Select(m => m.Compile()).Select(d => d.Reward == null ? "none" : W5Reward(d))));
        Check("c3 T8 wotg: the states (count, and each matches with the mission current)",
              "3:True,True,True|1:True|1:True|1:True|2:True,False|1:True|1:True|1:True|1:True|1:True|1:True|1:True|" +
              "1:True",
              string.Join("|", all.Select((m, i) =>
              {
                  var d = m.Compile();
                  int id = MW.ForgetMeNot + i;
                  return $"{d.States.Count}:" + string.Join(",", d.States.Select((s, k) =>
                  {
                      var f = W5F(id);
                      if (i == 0) f.MissionVars[W6Key(id, "Status")] = k;
                      return s.IsMissionMatch(f).ToString();
                  }));
              })));

        RunC3ToauT8Replies();
        RunC3ToauT8Grauberg();
        RunC3ToauT8Echoes();
        RunC3ToauT8Rest();
        RunC3ToauT8Lest();
        RunC3ToauT8Facets();
    }

    // the reply kinds, operands and modifiers: a progress event and a plain event send the same packet, and a modifier
    // shows only against a rival, so no trace tells them apart
    static void RunC3ToauT8Replies()
    {
        string Rep(MissionScript m, int id, QuestHandlerKind kind, ushort zone, string npc, int state = 0,
            Action<W5Fake> set = null)
        {
            var d = m.Compile();
            var f = W5F(id);
            f.Zone = zone;
            set?.Invoke(f);
            return W6Safe(() => W5Rep(d, state, kind, zone, npc, 1, f));
        }
        var T = QuestHandlerKind.OnTrigger;
        var gs = QC.Zone.GraubergS;
        var vc = GR.VeridicalConflux;
        Action<W5Fake> Ki(int ki) => f => f.KeyItems[ki] = true;
        Action<W5Fake> Var(int id, string name, int v) => f => f.MissionVars[W6Key(id, name)] = v;
        Action<W5Fake> Trade = f => f.TradeResult = true;

        Check("c3 T8 wotg 42: the six maws' progress events",
              "ProgressEvent(4,0,1,2964,1700,43,0,0,0)|ProgressEvent(503,1,26,0,6912,235686,1205,183377,1)|" +
              "ProgressEvent(503,2,0,0,0,0,651,636808,1)|ProgressEvent(8,0,0,0,0,0,6553607,0,1)|" +
              "ProgressEvent(703,1,0,0,0,0,0,3,1)|ProgressEvent(703,2,300,200,100,0,5439495,0,1)",
              string.Join("|",
                  Rep(new W.ForgetMeNot(), MW.ForgetMeNot, T, QC.Zone.BatalliaDowns, QC.Npc.BatalliaDowns.CavernousMaw),
                  Rep(new W.ForgetMeNot(), MW.ForgetMeNot, T, QC.Zone.RolanberryFields,
                      QC.Npc.RolanberryFields.CavernousMaw),
                  Rep(new W.ForgetMeNot(), MW.ForgetMeNot, T, QC.Zone.SauromugueChampaign,
                      QC.Npc.SauromugueChampaign.CavernousMaw),
                  Rep(new W.ForgetMeNot(), MW.ForgetMeNot, T, QC.Zone.BatalliaDownsS,
                      QC.Npc.BatalliaDownsS.CavernousMaw),
                  Rep(new W.ForgetMeNot(), MW.ForgetMeNot, T, QC.Zone.RolanberryFieldsS,
                      QC.Npc.RolanberryFieldsS.CavernousMaw),
                  Rep(new W.ForgetMeNot(), MW.ForgetMeNot, T, QC.Zone.SauromugueChampaignS,
                      QC.Npc.SauromugueChampaignS.CavernousMaw)));
        Check("c3 T8 wotg 42: Grauberg [S]'s zone-in at Status 1 is the plain event 33; the six completing zone-ins " +
              "answer nothing",
              "Event(33)|null|null|null|null|null|null",
              string.Join("|",
                  new[] { Rep(new W.ForgetMeNot(), MW.ForgetMeNot, QuestHandlerKind.OnZoneIn, gs, null, 1) }
                  .Concat(new[]
                  {
                      QC.Zone.BatalliaDowns, QC.Zone.RolanberryFields, QC.Zone.SauromugueChampaign,
                      QC.Zone.BatalliaDownsS, QC.Zone.RolanberryFieldsS, QC.Zone.SauromugueChampaignS,
                  }.Select(z => Rep(new W.ForgetMeNot(), MW.ForgetMeNot, QuestHandlerKind.OnZoneIn, z, null, 2)))));

        Check("c3 T8 wotg 43-48: the Veridical Conflux's kinds and modifiers",
              "ProgressEvent(28,89,23,1743,0,0,0,0,0)|ProgressEvent(34,89,23,1756,0,0,0,0,0)|" +
              "ProgressEvent(35,64,23,1756,0,0,0,0,0)|Event(40,89,10,1,0,0,0,0,0)|" +
              "ProgressEvent(42,76,0,1267351,120,0,8323092,0,0)|Event(41,89,23,1756)|Event(29,89,6)|" +
              "Event(29,89,12354,59449,120,0,8323089,0,0)|ProgressEvent(26,89,23,1756,0,0,8323073,0,0)|" +
              "ProgressEvent(36,89,1,596904,1000,0,8323089,0,0)|" +
              "ProgressEvent(27,89,12354,59449,120,0,8323089,0,0):oncePerZone()",
              string.Join("|",
                  Rep(new W.PillarOfHope(), MW.PillarOfHope, T, gs, vc),
                  Rep(new W.GlimmerOfLife(), MW.GlimmerOfLife, T, gs, vc),
                  Rep(new W.TimeSlipsAway(), MW.TimeSlipsAway, QuestHandlerKind.OnTrade, gs, vc, set: Trade),
                  Rep(new W.TimeSlipsAway(), MW.TimeSlipsAway, T, gs, vc),
                  Rep(new W.WhenWillsCollide(), MW.WhenWillsCollide, QuestHandlerKind.OnTrade, gs, vc, set: Trade),
                  Rep(new W.WhenWillsCollide(), MW.WhenWillsCollide, T, gs, vc),
                  Rep(new W.WhenWillsCollide(), MW.WhenWillsCollide, T, gs, vc, set: Ki(QC.KeyItem.BottledPunchBug)),
                  Rep(new W.WhenWillsCollide(), MW.WhenWillsCollide, T, gs, vc, 1),
                  Rep(new W.WhispersOfDawn(), MW.WhispersOfDawn, T, gs, vc),
                  Rep(new W.ADreamyInterlude(), MW.ADreamyInterlude, T, gs, vc),
                  Rep(new W.ADreamyInterlude(), MW.ADreamyInterlude, T, gs, vc,
                      set: Var(MW.ADreamyInterlude, "Timer", 1))));
        Check("c3 T8 wotg 46: the Ornate Door's 1 is a progress event, the zone-in's 2 a plain event",
              "ProgressEvent(1)|Event(2)",
              Rep(new W.WhenWillsCollide(), MW.WhenWillsCollide, T, QC.Zone.WalkOfEchoes, QC.Npc.WalkOfEchoes._521) +
              "|" + Rep(new W.WhenWillsCollide(), MW.WhenWillsCollide, QuestHandlerKind.OnZoneIn, QC.Zone.WalkOfEchoes,
                  null, set: Var(MW.WhenWillsCollide, "Status", 2)));

        Check("c3 T8 wotg 49, 52, 53: blank_cait, Lion Springs and the Bulwark Gate",
              "ProgressEvent(16,81,300,200,100,0,7405585,0,22292)|ProgressEvent(175,80,72)|" +
              "Event(117,0,23,1756,0,0,0,1,1)|ProgressEvent(115,98,5,1756,0,0,1,0)|" +
              "ProgressEvent(116,98,5,1756,0,0,0,1,0)",
              string.Join("|",
                  Rep(new W.CaitInTheWoods(), MW.CaitInTheWoods, T, QC.Zone.EastRonfaureS,
                      QC.Npc.EastRonfaureS.blankcait),
                  Rep(new W.WhereItAllBegan(), MW.WhereItAllBegan, T, QC.Zone.SouthernSanDoriaS,
                      QC.Npc.SouthernSanDoriaS.LionSprings),
                  Rep(new W.ATokenOfTroth(), MW.ATokenOfTroth, T, QC.Zone.SauromugueChampaignS,
                      QC.Npc.SauromugueChampaignS.BulwarkGate,
                      set: f => f.Equipment[QC.Slot.Main] = QC.Item.BronzeSword),
                  Rep(new W.ATokenOfTroth(), MW.ATokenOfTroth, T, QC.Zone.SauromugueChampaignS,
                      QC.Npc.SauromugueChampaignS.BulwarkGate),
                  Rep(new W.ATokenOfTroth(), MW.ATokenOfTroth, T, QC.Zone.SauromugueChampaignS,
                      QC.Npc.SauromugueChampaignS.BulwarkGate, set: Var(MW.ATokenOfTroth, "Status", 1))));

        var fork = new (ushort Zone, string Npc)[]
        {
            (QC.Zone.JugnerForestS, QC.Npc.JugnerForestS.blankfork), (QC.Zone.LaVauleS, QC.Npc.LaVauleS.blankfork),
            (QC.Zone.SouthernSanDoriaS, QC.Npc.SouthernSanDoriaS.blankfork),
            (QC.Zone.BeaucedineGlacierS, QC.Npc.BeaucedineGlacierS.blankfork),
            (QC.Zone.XarcabardS, QC.Npc.XarcabardS.blankfork), (QC.Zone.ThroneRoomS, QC.Npc.ThroneRoomS.blankfork),
            (QC.Zone.WalkOfEchoes, QC.Npc.WalkOfEchoes.blankfork),
        };
        Check("c3 T8 wotg 50: blank_fork's seven progress events (zoneEventInfo), each by the char's zone",
              "ProgressEvent(7,82,0,0,0,0,7340037,0,0)|ProgressEvent(5,85,23,1756,0,85,1,0,0)|" +
              "ProgressEvent(176,80,23,1756,2000,0,0,0,0)|ProgressEvent(29,136,23,1756,0,0,6357002,0,0)|" +
              "ProgressEvent(42,137,23,2964,2000,66584575,5941395,4095,0)|ProgressEvent(15,156,23,1756,0,156,2,0,0)|" +
              "ProgressEvent(10,182,23,1756,0,0,0,1984,1)",
              string.Join("|", fork.Select(z => Rep(new W.ForkInTheRoad(), MW.ForkInTheRoad, T, z.Zone, z.Npc))));

        Check("c3 T8 wotg 51: the Veridical Conflux's kinds and modifiers, the Ornate Door's 5",
              "ProgressEvent(38,89,23,1756,0,0,0,0,0)|ProgressEvent(44,89,23,1756,0,68032378,209748094,1,0)|" +
              "Event(45,89,23,1756,0,68032378,209748094,1,0):oncePerZone()|null|ProgressEvent(5)",
              string.Join("|",
                  Rep(new W.MaidenOfTheDusk(), MW.MaidenOfTheDusk, T, gs, vc),
                  Rep(new W.MaidenOfTheDusk(), MW.MaidenOfTheDusk, T, gs, vc,
                      set: Var(MW.MaidenOfTheDusk, "Status", 1)),
                  Rep(new W.MaidenOfTheDusk(), MW.MaidenOfTheDusk, T, gs, vc, set: f =>
                  {
                      f.MissionVars[W6Key(MW.MaidenOfTheDusk, "Status")] = 1;
                      f.MissionVars[W6Key(MW.MaidenOfTheDusk, "Timer")] = 1;
                  }),
                  Rep(new W.MaidenOfTheDusk(), MW.MaidenOfTheDusk, T, gs, vc, set: f =>
                  {
                      f.MissionVars[W6Key(MW.MaidenOfTheDusk, "Status")] = 1;
                      f.KeyItems[QC.KeyItem.PrimalGlow] = true;
                  }),
                  Rep(new W.MaidenOfTheDusk(), MW.MaidenOfTheDusk, T, QC.Zone.WalkOfEchoes, QC.Npc.WalkOfEchoes._521,
                      set: Var(MW.MaidenOfTheDusk, "Status", 2))));
        Check("c3 T8 wotg 51: the zone-ins are plain events: 4, 6, 7, 8, 9 at Status 1, 4, 5, 6, 7; none at 0, 2, 3, 8",
              "Event(4)|Event(6)|Event(7)|Event(8)|Event(9)|null|null|null|null",
              string.Join("|", new[] { 1, 4, 5, 6, 7, 0, 2, 3, 8 }.Select(s => Rep(new W.MaidenOfTheDusk(),
                  MW.MaidenOfTheDusk, QuestHandlerKind.OnZoneIn, QC.Zone.WalkOfEchoes, null,
                  set: Var(MW.MaidenOfTheDusk, "Status", s)))));
        Check("c3 T8 wotg 54: the Veridical Conflux's 39 is an important event; the qm_reset's 46 a progress event",
              "ProgressEvent(39,89,23,1756):importantEvent()|ProgressEvent(46,89,23,1756,0,9467,16940788,0,0)",
              Rep(new W.LestWeForget(), MW.LestWeForget, T, gs, vc, set: Var(MW.LestWeForget, "Status", 1)) + "|" +
              Rep(new W.LestWeForget(), MW.LestWeForget, T, gs, GR.qmreset,
                  set: f => f.MissionLocalVars["Option"] = 1));
    }

    // 42-48 in Grauberg [S] and the six maws: the guarded effects, the boundaries
    static void RunC3ToauT8Grauberg()
    {
        var gs = QC.Zone.GraubergS;
        var d42 = new W.ForgetMeNot().Compile();
        int id42 = MW.ForgetMeNot;
        string Maw(ushort zone, int csid)
        {
            var f = W5F(id42);
            f.Zone = zone;
            return W6Safe(() => W5Fin(d42, 0, zone, csid, f));
        }
        var maws = new (ushort Zone, int Csid)[]
        {
            (QC.Zone.BatalliaDowns, 4), (QC.Zone.RolanberryFields, 503), (QC.Zone.SauromugueChampaign, 503),
            (QC.Zone.BatalliaDownsS, 8), (QC.Zone.RolanberryFieldsS, 703), (QC.Zone.SauromugueChampaignS, 703),
        };
        Check("c3 T8 wotg 42: each maw's finish sets Status 1, Option the char's zone and the char down at 800 " +
              "71.839 " +
              "770 rot 63 in Grauberg [S]",
              string.Join("|", maws.Select(m => $"Mission.SetVar(Status,1),Mission.SetVar(Option,{m.Zone})," +
                  W6Pos(800, 71.839, 770, 63, gs))),
              string.Join("|", maws.Select(m => Maw(m.Zone, m.Csid))));
        string Upd(int option)
        {
            var f = W5F(id42);
            return W6Safe(() => W5Upd(d42, 1, gs, 33, f, option));
        }
        Check("c3 T8 wotg 42: 33's update sends (12, 23, 1756, 0, 0, 0, 0, 0) on option 1 only",
              "Player.UpdateEvent(12,23,1756,0,0,0,0,0)||", Upd(1) + "|" + Upd(0) + "|" + Upd(2));
        var f33 = W5F(id42);
        f33.MissionVars[W6Key(id42, "Option")] = QC.Zone.RolanberryFieldsS;
        Check("c3 T8 wotg 42: 33's finish sets Status 2, then goToMaw of xi.maws.pastMaws[Option]",
              $"Mission.SetVar(Status,2),Player.GoToPastMaw({QC.Zone.RolanberryFieldsS})",
              W6Safe(() => W5Fin(d42, 1, gs, 33, f33)));
        string Arrive(bool complete)
        {
            var f = W5F(id42);
            f.CompleteResult = complete;
            return W6Safe(() =>
            {
                var r = W5ZoneIn(d42, 2, QC.Zone.BatalliaDownsS, f);
                return r + "@" + string.Join(",", f.Calls);
            });
        }
        Check("c3 T8 wotg 42: the zone-in completes and takes the fragments only when the completion holds",
              "null@Mission.Complete(),Player.RemoveMemoryFragments()|null@Mission.Complete()",
              Arrive(true) + "|" + Arrive(false));

        var d43 = new W.PillarOfHope().Compile();
        int id43 = MW.PillarOfHope;
        string Pillar(int option)
        {
            var f = W5F(id43);
            f.UniqueDay = 500;
            return W6Safe(() => W5Fin(d43, 0, gs, 28, f, option));
        }
        Check("c3 T8 wotg 43: 28's finish: option 1 completes, then Glimmer of Life's Timer (day + 1) and mustZone; " +
              "else Option 1",
              $"Mission.Complete(),Player.SetMissionVar({WotgLog},{MW.GlimmerOfLife},Timer,501)," +
              $"Player.SetMissionMustZone({WotgLog},{MW.GlimmerOfLife})|Mission.SetVar(Option,1)|" +
              "Mission.SetVar(Option,1)",
              Pillar(1) + "|" + Pillar(0) + "|" + Pillar(2));

        // the Timer read against the day: <= (99 and 100 play at day 100, 101 does not), and the mustZone alone
        string Day(MissionScript m, int id, int timer, bool mustZone)
        {
            var d = m.Compile();
            var f = W5F(id, mustZone: mustZone);
            f.UniqueDay = 100;
            f.MissionVars[W6Key(id, "Timer")] = timer;
            return W6Safe(() => W5Trig(d, 0, gs, GR.VeridicalConflux, f)).Split(',')[0];
        }
        Check("c3 T8 wotg 44: 34 with Timer 99 and 100 at day 100, not 101, not with the mustZone",
              "ProgressEvent(34|ProgressEvent(34|null|null",
              string.Join("|", Day(new W.GlimmerOfLife(), MW.GlimmerOfLife, 99, false),
                  Day(new W.GlimmerOfLife(), MW.GlimmerOfLife, 100, false),
                  Day(new W.GlimmerOfLife(), MW.GlimmerOfLife, 101, false),
                  Day(new W.GlimmerOfLife(), MW.GlimmerOfLife, 0, true)));
        Check("c3 T8 wotg 48: 36 with Timer 99 and 100 at day 100; 27 at 101 and with the mustZone",
              "ProgressEvent(36|ProgressEvent(36|ProgressEvent(27|ProgressEvent(27",
              string.Join("|", Day(new W.ADreamyInterlude(), MW.ADreamyInterlude, 99, false),
                  Day(new W.ADreamyInterlude(), MW.ADreamyInterlude, 100, false),
                  Day(new W.ADreamyInterlude(), MW.ADreamyInterlude, 101, false),
                  Day(new W.ADreamyInterlude(), MW.ADreamyInterlude, 0, true)));
        Check("c3 T8 wotg 44, 48: 34's and 36's finishes complete",
              "Mission.Complete()|Mission.Complete()",
              W5Fin(new W.GlimmerOfLife().Compile(), 0, gs, 34, W5F(MW.GlimmerOfLife)) + "|" +
              W5Fin(new W.ADreamyInterlude().Compile(), 0, gs, 36, W5F(MW.ADreamyInterlude)));

        var d45 = new W.TimeSlipsAway().Compile();
        string Slip(bool complete)
        {
            var f = W5F(MW.TimeSlipsAway);
            f.CompleteResult = complete;
            return W6Safe(() => W5Fin(d45, 0, gs, 35, f));
        }
        Check("c3 T8 wotg 45: 35's finish takes the trade only when the completion holds; the trade asked is one " +
              "Punch Bug",
              $"Mission.Complete(),Trade.Complete()|Mission.Complete()|{QC.Item.PunchBug}x1",
              Slip(true) + "|" + Slip(false) + "|" + W6Safe(() =>
              {
                  var f = W5F(MW.TimeSlipsAway);
                  W5Rep(d45, 0, QuestHandlerKind.OnTrade, gs, GR.VeridicalConflux, 1, f);
                  return string.Join(";", f.Asked);
              }));

        var d47 = new W.WhispersOfDawn().Compile();
        string Dawn(int option)
        {
            var f = W5F(MW.WhispersOfDawn);
            f.UniqueDay = 500;
            return W6Safe(() => W5Fin(d47, 0, gs, 26, f, option));
        }
        Check("c3 T8 wotg 47: 26's finish on option 1 sets A Dreamy Interlude's Timer (day + 1) and mustZone, then " +
              "completes; nothing on another option",
              $"Player.SetMissionVar({WotgLog},{MW.ADreamyInterlude},Timer,501)," +
              $"Player.SetMissionMustZone({WotgLog},{MW.ADreamyInterlude}),Mission.Complete()||",
              Dawn(1) + "|" + Dawn(0) + "|" + Dawn(2));
        // hasWeapons: the main or the sub slot, each alone
        string Weapons(MissionScript m, int id, int slot)
        {
            var f = W5F(id);
            if (slot >= 0) f.Equipment[slot] = QC.Item.BronzeSword;
            return W6Safe(() => W5Trig(m.Compile(), 0, gs, GR.VeridicalConflux, f)).Split(',').Last();
        }
        Check("c3 T8 wotg 43, 47: hasWeapons is 1 with a main or a sub weapon, 0 with neither (or a ranged one)",
              "1)|1)|0)|0)|1)|1)|0)|0)",
              string.Join("|", new[] { QC.Slot.Main, QC.Slot.Sub, -1, QC.Slot.Ranged }
                  .Select(s => Weapons(new W.PillarOfHope(), MW.PillarOfHope, s))
                  .Concat(new[] { QC.Slot.Main, QC.Slot.Sub, -1, QC.Slot.Ranged }
                      .Select(s => Weapons(new W.WhispersOfDawn(), MW.WhispersOfDawn, s)))));
    }

    // 46 and 51 in the Walk of Echoes: the 32001 finishes no flow opens, the set-downs, the updates
    static void RunC3ToauT8Echoes()
    {
        var we = QC.Zone.WalkOfEchoes;
        var gs = QC.Zone.GraubergS;
        var d46 = new W.WhenWillsCollide().Compile();
        int id46 = MW.WhenWillsCollide;
        Check("c3 T8 wotg 46: the 32001 finish (no flow opens it) sets Status 2 and the char down at -700.063 -17.6 " +
              "-331.903 rot 64 in the Walk of Echoes",
              $"Mission.SetVar(Status,2),{W6Pos(-700.063, -17.6, -331.903, 64, we)}",
              W6Safe(() => W5Fin(d46, 0, we, 32001, W5F(id46))));
        Check("c3 T8 wotg 46: no xi.battlefield.id constant for When Wills Collide; the Walk of Echoes has no " +
              "battlefield",
              "False|0",
              (typeof(QC.Battlefield).GetField("WhenWillsCollide") != null ? "True" : "False") + "|" +
              typeof(QC.Battlefield).Assembly.GetTypes().Count(t => t.Namespace == "XI.QuestContent.Battlefields" +
                  ".WalkOfEchoes"));
        string Look(int csid, int option)
        {
            var f = W5F(id46);
            f.FaceId = 7;
            f.Models = (11, 12, 13, 14, 15);
            return W6Safe(() => W5Upd(d46, 0, we, csid, f, option));
        }
        Check("c3 T8 wotg 46: the updates (option 5) send 119 / 182, the face, the head, body, hands, main and sub " +
              "models, then 1 / 0",
              "Player.UpdateEvent(119,7,11,12,13,14,15,1)|Player.UpdateEvent(182,7,11,12,13,14,15,0)||",
              Look(1, 5) + "|" + Look(2, 5) + "|" + Look(1, 4) + "|" + Look(2, 6));
        string Fin29(int state, int option) => W6Safe(() => W5Fin(d46, state, gs, 29, W5F(id46), option));
        string toEchoes = W6Pos(-700.042, 0.4, -441.301, 192, we);
        Check("c3 T8 wotg 46: 29's finish (both sections) sets the char down at -700.042 0.4 -441.301 rot 192 on " +
              "option 99 only",
              $"{toEchoes}||{toEchoes}|", Fin29(0, 99) + "|" + Fin29(0, 98) + "|" + Fin29(1, 99) + "|" + Fin29(1, 0));
        var done = W5F(MW.CaitInTheWoods);
        done.CompletedMissions.Add($"{WotgLog}/{id46}");
        Check("c3 T8 wotg 46: section 2 holds for the mission completed, not for it current",
              "True|False", $"{d46.States[1].IsMissionMatch(done)}|{d46.States[1].IsMissionMatch(W5F(id46))}");
        Check("c3 T8 wotg 46: the trade needs no Bottled Punch Bug as well as the bug",
              "null", W6Safe(() =>
              {
                  var f = W5F(id46);
                  f.TradeResult = true;
                  f.KeyItems[QC.KeyItem.BottledPunchBug] = true;
                  return W5Rep(d46, 0, QuestHandlerKind.OnTrade, gs, GR.VeridicalConflux, 1, f);
              }));

        var d51 = new W.MaidenOfTheDusk().Compile();
        int id51 = MW.MaidenOfTheDusk;
        string down = W6Pos(-700.063, -17.6, -331.903, 64, we);
        string Fin51(int csid) => W6Safe(() => W5Fin(d51, 0, we, csid, W5F(id51)));
        Check("c3 T8 wotg 51: the 32001 finish (no flow opens it) sets Status 4; 6, 7 and 8 set Status 5, 6 and 7; " +
              "each sets the char down at -700.063 -17.6 -331.903 rot 64",
              $"Mission.SetVar(Status,4),{down}|Mission.SetVar(Status,5),{down}|Mission.SetVar(Status,6),{down}|" +
              $"Mission.SetVar(Status,7),{down}",
              Fin51(32001) + "|" + Fin51(6) + "|" + Fin51(7) + "|" + Fin51(8));
        Check("c3 T8 wotg 51: xi.battlefield.id.MAIDEN_OF_THE_DUSK is 385; no C# battlefield in the Walk of Echoes",
              "385|0", QC.Battlefield.MaidenOfTheDusk + "|" +
              typeof(QC.Battlefield).Assembly.GetTypes().Count(t => t.Namespace == "XI.QuestContent.Battlefields" +
                  ".WalkOfEchoes"));
        Check("c3 T8 wotg 51: 4, 5 and 9 set Status 2 and 3 and complete",
              "Mission.SetVar(Status,2)|Mission.SetVar(Status,3)|Mission.Complete()",
              Fin51(4) + "|" + Fin51(5) + "|" + Fin51(9));
        string Upd51(int csid, int option) => W6Safe(() => W5Upd(d51, 0, we, csid, W5F(id51), option));
        Check("c3 T8 wotg 51: the updates: 6 on option 6, 7 and 8 on option 5",
              "Player.UpdateEvent(1,300,200,100,182,1,0,0)|Player.UpdateEvent(1,300,200,100,73,545,278,0)|" +
              "Player.UpdateEvent(1,300,200,100,0,0,0,0)|||",
              Upd51(6, 6) + "|" + Upd51(7, 5) + "|" + Upd51(8, 5) + "|" + Upd51(6, 5) + "|" + Upd51(7, 6) + "|" +
              Upd51(8, 6));
        string Fin38(int option) => W6Safe(() => W5Fin(d51, 0, gs, 38, W5F(id51), option));
        Check("c3 T8 wotg 51: 38's finish: option 1 sets Status 1 and the char down at -700.042 0.399 -441.301 rot " +
              "192; else Option 1",
              $"Mission.SetVar(Status,1),{W6Pos(-700.042, 0.399, -441.301, 192, we)}|Mission.SetVar(Option,1)|" +
              "Mission.SetVar(Option,1)",
              Fin38(1) + "|" + Fin38(0) + "|" + Fin38(2));
        string Glow(int timer)
        {
            var f = W5F(id51);
            f.UniqueDay = 100;
            f.MissionVars[W6Key(id51, "Status")] = 3;
            f.MissionVars[W6Key(id51, "Timer")] = timer;
            return W6Safe(() => W5Trig(d51, 0, gs, GR.VeridicalConflux, f)).Split(',')[0];
        }
        Check("c3 T8 wotg 51: without the Primal Glow, 44 with Timer 99 and 100 at day 100; 45 at 101",
              "ProgressEvent(44|ProgressEvent(44|Event(45", Glow(99) + "|" + Glow(100) + "|" + Glow(101));
        var g44 = W5F(id51);
        g44.UniqueDay = 100;
        Check("c3 T8 wotg 51: 44's finish sets the Timer to the day + 1, then gives the Primal Glow",
              $"Mission.SetVar(Timer,101),Player.GiveKeyItem({QC.KeyItem.PrimalGlow})",
              W6Safe(() => W5Fin(d51, 0, gs, 44, g44)));
        Check("c3 T8 wotg 51: the Ornate Door answers nothing but at Status 2",
              "null|null|ProgressEvent(5)|null",
              string.Join("|", new[] { 0, 1, 2, 3 }.Select(s =>
              {
                  var f = W5F(id51);
                  f.MissionVars[W6Key(id51, "Status")] = s;
                  return W6Safe(() => W5Trig(d51, 0, we, QC.Npc.WalkOfEchoes._521, f));
              })));
    }

    // 50, 53
    static void RunC3ToauT8Rest()
    {
        var d50 = new W.ForkInTheRoad().Compile();
        int id50 = MW.ForkInTheRoad;
        int[] drops =
        {
            QC.KeyItem.BeaucedineDawndrop, QC.KeyItem.JugnerDawndrop, QC.KeyItem.SanDoriaDawndrop,
            QC.KeyItem.WalkOfEchoesDawndrop, QC.KeyItem.LaVauleDawndrop, QC.KeyItem.XarcabardDawndrop,
            QC.KeyItem.ThroneRoomDawndrop,
        };
        string Fork(int held)
        {
            var f = W5F(id50);
            f.Zone = QC.Zone.LaVauleS;
            foreach (int k in drops.Where(k => k != QC.KeyItem.LaVauleDawndrop).Take(held)) f.KeyItems[k] = true;
            return W6Safe(() => W5Fin(d50, 0, QC.Zone.LaVauleS, 5, f));
        }
        string take = string.Join(",", drops.Select(k => $"Player.DelKeyItem({k})"));
        Check("c3 T8 wotg 50: the finish with six others held: the key item, the zone's two texts, the seven taken " +
              "in pairs order, the Ronfaure Dawndrop, the completion; with five: the key item only",
              $"Player.GiveKeyItem({QC.KeyItem.LaVauleDawndrop}),Text.Special({QC.Zone.LaVauleS}:" +
              $"GATHERED_DAWNDROPS_LIGHT,{QC.KeyItem.PrimalGlow}),Text.MessageName({QC.Zone.LaVauleS}:" +
              $"RETRACED_ALL_JUNCTIONS),{take},Player.DelKeyItem({QC.KeyItem.RonfaureDawndrop}),Mission.Complete()|" +
              $"Player.GiveKeyItem({QC.KeyItem.LaVauleDawndrop})",
              Fork(6) + "|" + Fork(5));
        Check("c3 T8 wotg 50: a dawndrop the char does not hold is not taken (seven held but one)",
              $"Player.DelKeyItem({QC.KeyItem.RonfaureDawndrop})", W6Safe(() =>
              {
                  // six held, the zone's given: then the GiveKeyItem fails, so only the six held are taken
                  var f = W5F(id50);
                  f.Zone = QC.Zone.LaVauleS;
                  f.GiveResult = false;
                  foreach (int k in drops.Where(k => k != QC.KeyItem.LaVauleDawndrop)) f.KeyItems[k] = true;
                  W5Fin(d50, 0, QC.Zone.LaVauleS, 5, f);
                  return f.Calls.Any(c => c == $"Player.DelKeyItem({QC.KeyItem.LaVauleDawndrop})")
                      ? "took the one not held" : f.Calls[^2];
              }));
        var held = W5F(id50);
        held.Zone = QC.Zone.JugnerForestS;
        held.KeyItems[QC.KeyItem.JugnerDawndrop] = true;
        Check("c3 T8 wotg 50: blank_fork answers nothing with the zone's dawndrop held", "null",
              W6Safe(() => W5Trig(d50, 0, QC.Zone.JugnerForestS, QC.Npc.JugnerForestS.blankfork, held)));

        var d53 = new W.ATokenOfTroth().Compile();
        string Troth(bool complete)
        {
            var f = W5F(MW.ATokenOfTroth);
            f.CompleteResult = complete;
            return W6Safe(() => W5Fin(d53, 0, QC.Zone.SauromugueChampaignS, 116, f));
        }
        Check("c3 T8 wotg 53: 116's finish takes the invitation and sets Lest We Forget's Status 1 only when the " +
              "completion holds; 115 sets Status 1",
              $"Mission.Complete(),Player.DelKeyItem({QC.KeyItem.WeddingInvitation})," +
              $"Player.SetMissionVar({WotgLog},{MW.LestWeForget},Status,1)|Mission.Complete()|Mission.SetVar(Status,1)",
              Troth(true) + "|" + Troth(false) + "|" +
              W5Fin(d53, 0, QC.Zone.SauromugueChampaignS, 115, W5F(MW.ATokenOfTroth)));
        string Gate(int slot)
        {
            var f = W5F(MW.ATokenOfTroth);
            f.MissionVars[W6Key(MW.ATokenOfTroth, "Status")] = 1;
            if (slot >= 0) f.Equipment[slot] = QC.Item.BronzeSword;
            return W6Safe(() => W5Trig(d53, 0, QC.Zone.SauromugueChampaignS, QC.Npc.SauromugueChampaignS.BulwarkGate,
                f)).Split(',')[0];
        }
        Check("c3 T8 wotg 53: 117 with a main or a sub weapon, ahead of 116; 116 with neither (or a ranged one)",
              "Event(117|Event(117|ProgressEvent(116|ProgressEvent(116",
              string.Join("|", new[] { QC.Slot.Main, QC.Slot.Sub, -1, QC.Slot.Ranged }.Select(Gate)));
    }

    // 54: the augments, the free slot, afterZoneIn's parts, the reset
    static void RunC3ToauT8Lest()
    {
        var gs = QC.Zone.GraubergS;
        var d54 = new W.LestWeForget().Compile();
        int id54 = MW.LestWeForget;
        // the two tables, transcribed from 54_Lest_We_Forget.lua's firstAugInfo and secondAugInfo
        Check("c3 T8 wotg 54: firstAugInfo and secondAugInfo as the Lua's tables, in order",
              "23:3,25:3,27:3,29:3,35:3,133:3,1:24,9:24|59:0,60:0,352:4,353:4,350:2,351:2,145:2,61:4",
              string.Join(",", W.LestWeForget.FirstAugInfo.Select(a => $"{a.Augment}:{a.Value}")) + "|" +
              string.Join(",", W.LestWeForget.SecondAugInfo.Select(a => $"{a.Augment}:{a.Value}")));
        Check("c3 T8 wotg 54: a selection of 0 or past 8 indexes a nil entry and raises (the Lua's 1-based tables)",
              "throws attempt to index a nil value|throws attempt to index a nil value|1:24",
              W6Safe(() => W.LestWeForget.AugAt(W.LestWeForget.FirstAugInfo, 0).ToString()) + "|" +
              W6Safe(() => W.LestWeForget.AugAt(W.LestWeForget.FirstAugInfo, 9).ToString()) + "|" +
              W6Safe(() =>
              {
                  var a = W.LestWeForget.AugAt(W.LestWeForget.FirstAugInfo, 7);
                  return $"{a.Augment}:{a.Value}";
              }));
        int Opt(int first, int second) => (first << 1) | (second << 17);
        string Upd(int option) => W6Safe(() => W5Upd(d54, 0, gs, 39, W5F(id54), option));
        Check("c3 T8 wotg 54: 39's update: ((value << 27) + (augment << 16) + 770, (value << 11) + augment) for each " +
              "selection pair; nothing on option 0 or below",
              "Player.UpdateEvent(404161282,59)|Player.UpdateEvent(-1073151230,8253)|" +
              "Player.UpdateEvent(404423426,8544)||",
              Upd(Opt(1, 1)) + "|" + Upd(Opt(8, 8)) + "|" + Upd(Opt(3, 3)) + "|" + Upd(0) + "|" + Upd(-2));
        string Fin39(int option, int freeSlots)
        {
            var f = W5F(id54);
            f.FreeSlots = freeSlots;
            return W6Safe(() => W5Fin(d54, 0, gs, 39, f, option));
        }
        Check("c3 T8 wotg 54: 39's finish adds the earring with the two augments (one free slot is enough), says " +
              "ITEM_OBTAINED and clears Status; a full bag says ITEM_CANNOT_BE_OBTAINED; the cancel does nothing",
              $"Player.AddItemAugmented({QC.Item.MoonshadeEarring},1,27,3,352,4),Text.Special({gs}:ITEM_OBTAINED," +
              $"{QC.Item.MoonshadeEarring}),Mission.SetVar(Status,0)|Text.Special({gs}:ITEM_CANNOT_BE_OBTAINED)|" +
              $"|Player.AddItemAugmented({QC.Item.MoonshadeEarring},1,9,24,61,4),Text.Special({gs}:ITEM_OBTAINED," +
              $"{QC.Item.MoonshadeEarring}),Mission.SetVar(Status,0)",
              Fin39(Opt(3, 3), 1) + "|" + Fin39(Opt(3, 3), 0) + "|" + Fin39(1 << 30, 5) + "|" + Fin39(Opt(8, 8), 5));
        // the flows pick equal selections (1 and 1, 8 and 8): two different ones tell bits 1-4 from bits 17-20
        Check("c3 T8 wotg 54: 39's update and finish read the first augment from bits 1-4 and the second from bits " +
              "17-20 (selections 2 and 5)",
              $"Player.UpdateEvent(404292354,4446)|Player.AddItemAugmented({QC.Item.MoonshadeEarring},1,25,3,350,2)",
              Upd(Opt(2, 5)) + "|" + Fin39(Opt(2, 5), 1).Split(",Text.")[0]);
        string After(bool earring, int status)
        {
            var f = W5F(id54);
            if (earring) f.Items.Add(QC.Item.MoonshadeEarring);
            f.MissionVars[W6Key(id54, "Status")] = status;
            return W6Safe(() => W5Rep(d54, 0, QuestHandlerKind.OnAfterZoneIn, gs, null, 1, f) + "@" +
                string.Join(",", f.Calls));
        }
        Check("c3 T8 wotg 54: afterZoneIn sets the local var Option 1 only without the earring and at Status 0 (each " +
              "part alone decides)",
              "null@Mission.SetLocalVar(Option,1)|null@|null@|null@",
              After(false, 0) + "|" + After(true, 0) + "|" + After(false, 1) + "|" + After(true, 1));
        string Reset(int option) => W6Safe(() => W5Fin(d54, 0, gs, 46, W5F(id54), option));
        Check("c3 T8 wotg 54: 46's finish: option 1 deletes the four missions, adds Maiden of the Dusk at Status 2, " +
              "gives the Primal Glow and says YOU_HAVE_RETRACED_RIVER; else the mustZone",
              $"Player.DelMission({WotgLog},{MW.MaidenOfTheDusk}),Player.DelMission({WotgLog},{MW.WhereItAllBegan})," +
              $"Player.DelMission({WotgLog},{MW.ATokenOfTroth}),Player.DelMission({WotgLog},{MW.LestWeForget})," +
              $"Player.AddMission({WotgLog},{MW.MaidenOfTheDusk})," +
              $"Player.SetMissionVar({WotgLog},{MW.MaidenOfTheDusk},Status,2)," +
              $"Player.GiveKeyItem({QC.KeyItem.PrimalGlow}),Text.Special({gs}:YOU_HAVE_RETRACED_RIVER)|" +
              "Mission.SetMustZone()|Mission.SetMustZone()",
              Reset(1) + "|" + Reset(0) + "|" + Reset(2));
        string Qm(int option, bool mustZone)
        {
            var f = W5F(id54, mustZone: mustZone);
            f.MissionLocalVars["Option"] = option;
            return W6Safe(() => W5Trig(d54, 0, gs, GR.qmreset, f)).Split(',')[0];
        }
        Check("c3 T8 wotg 54: the qm_reset plays 46 only with the local var Option 1 and no mustZone",
              "ProgressEvent(46|null|null|null", Qm(1, false) + "|" + Qm(1, true) + "|" + Qm(0, false) + "|" +
              Qm(2, false));
        Check("c3 T8 wotg 54: the Veridical Conflux plays 39 only at Status 1",
              "null|ProgressEvent(39|null",
              string.Join("|", new[] { 0, 1, 2 }.Select(s =>
              {
                  var f = W5F(id54);
                  f.MissionVars[W6Key(id54, "Status")] = s;
                  return W6Safe(() => W5Trig(d54, 0, gs, GR.VeridicalConflux, f)).Split(',')[0];
              })));
    }

    // the facet members T8 adds (GoToPastMaw, RemoveMemoryFragments, AddItemAugmented) on the fake
    static void RunC3ToauT8Facets()
    {
        var f = new W5Fake();
        IQuestPlayer p = f;
        Check("c3 T8 facets: GoToPastMaw records; a zone with no past maw throws as the Lua global does with a nil",
              "ok|throws attempt to index local 'maw' (a nil value)|Player.GoToPastMaw(91),Player.GoToPastMaw(0)",
              W6Safe(() => { p.GoToPastMaw(91); return "ok"; }) + "|" +
              W6Safe(() => { p.GoToPastMaw(0); return "ok"; }) +
              "|" + string.Join(",", f.Calls));
        var g = new W5Fake();
        IQuestPlayer gp = g;
        gp.RemoveMemoryFragments();
        gp.AddItemAugmented(11697, 1, 23, 3, 59, 0);
        Check("c3 T8 facets: RemoveMemoryFragments and AddItemAugmented record their calls",
              "Player.RemoveMemoryFragments(),Player.AddItemAugmented(11697,1,23,3,59,0)", string.Join(",", g.Calls));
        Check("c3 T8 facets: the fake's past maw zones are xi.maws.pastMaws' 18 keys",
              "81,82,84,88,90,91,95,97,98,101,104,105,106,109,110,115,119,120",
              string.Join(",", f.PastMawZones.OrderBy(z => z)));
    }
}
