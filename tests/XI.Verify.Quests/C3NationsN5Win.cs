// XI.Verify.Quests -- quest rework C3 N5: the four Windurst missions (6-1 Full Moon Fountain, 6-2 Saintly Invitation,
// 7-1 The Sixth Ministry, 7-2 Awakening of the Gods) on C#, one fake check per arm no packet shows, as the Lua files
// had them.
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    // NPCs with a fixed mission:event(csid): one check per NPC, printing its reply
    static void N5Fixed(string label, MissionScriptDefinition d, int state, Fake f,
                        params (ushort Zone, string Npc, int Csid)[] npcs)
    {
        foreach (var (zone, npc, csid) in npcs)
        {
            Check($"{N5} {label}: {npc} (zone {zone}): {csid} (event, fixed)",
                  $"Event({csid})", C3NReply(C2MHandler(d, state, N4Trig, zone, npc), f));
        }
    }

    static void RunC3N5FullMoonFountain()
    {
        const string L = "full moon fountain";
        var m = new QC.Missions.Windurst.FullMoonFountain();
        var d = m.Compile();
        int w = QC.MissionLog.Windurst, id = QC.Mission.Windurst.FullMoonFountain, none = QC.Mission.Windurst.None;
        ushort pw = QC.Zone.PortWindurst, ohr = QC.Zone.OuterHorutotoRuins;
        const int charm = QC.KeyItem.SouthwesternStarCharm;
        N4Header(L, m, d, QC.MissionKey.Windurst.FullMoonFountain, "scripts/missions/windurst/6_1_Full_Moon_Fountain",
                 "///650", N5);
        Check($"{N5} {L}: two sections", "2", d.States.Count.ToString());
        N4Section1Open(L, d, MissionLog.Windurst, id, none, QC.Mission.Windurst.TheShadowAwaits, N5);
        N5WindurstAccept(L, d, id, 16);
        N4Current(L, d, 1, MissionLog.Windurst, id, none, N5);
        Fake F(int status) => N4Fake(MissionLog.Windurst, id, id, status);
        string R(ushort zone, string npc, Fake f) => C3NReply(C2MHandler(d, 1, N4Trig, zone, npc), f);
        string hak = QC.Npc.PortWindurst.HakkuruRinkuru, kur = QC.Npc.PortWindurst.KuroidoMoido;
        Check($"{N5} {L}: Hakkuru-Rinkuru at status 0: 456 (progress, 0 and the Southwestern Star Charm); at 1 and " +
              "2: 457; at 3: 459 (events); at 4: nothing",
              $"ProgressEvent(456,0,{charm})|Event(457)|Event(457)|Event(459)|null",
              string.Join("|", Enumerable.Range(0, 5).Select(st => R(pw, hak, F(st)))));
        Check($"{N5} {L}: Kuroido-Moido at status 1 and 2: 458; at 3: 460 (events); at 0 and 4: nothing",
              "null|Event(458)|Event(458)|Event(460)|null",
              string.Join("|", Enumerable.Range(0, 5).Select(st => R(pw, kur, F(st)))));
        Check($"{N5} {L}: 456's finish sets status 1 and gives the charm",
              $"Player.SetMissionStatus({w},1),Player.GiveKeyItem({charm})",
              C2MRun(C2MHandler(d, 1, N4Fin, pw, id: 456), F(0)));

        // _5eb and the Jacks
        string J = QC.Mob.OuterHorutotoRuins.FULL_MOON_FOUNTAIN_OFFSET;
        var gate = C2MHandler(d, 1, N4Trig, ohr, QC.Npc.OuterHorutotoRuins._5eb);
        string spawn = string.Join(",", Enumerable.Range(0, 4).Select(o => $"Player.SpawnMob({J},{o},True)"));
        string door = $"MessageSpecial({QC.Text.OuterHorutotoRuins.DOOR_WONT_OPEN_STAR_CHARM},{ohr},{charm})/";
        Check($"{N5} {L}: _5eb with no Jack spawned: at status 1 the four spawned and claimed (offset 0 to 3), " +
              "GUARDIAN_BLOCKING_WAY (message special); at 2 STAR_CHARM_DISAPPEARS (zone, charm), then 68 " +
              "(progress); at 0 and 3: nothing",
              $"MessageSpecial({QC.Text.OuterHorutotoRuins.GUARDIAN_BLOCKING_WAY})/{spawn}|" +
              $"ProgressEvent(68)/Text.Special({QC.Text.OuterHorutotoRuins.STAR_CHARM_DISAPPEARS},{ohr},{charm})|" +
              "null/|null/",
              $"{N5RC(gate, F(1))}|{N5RC(gate, F(2))}|{N5RC(gate, F(0))}|{N5RC(gate, F(3))}");
        Check($"{N5} {L}: _5eb with a Jack spawned (offset 0 alive at status 1, offset 3 dead at status 2, offset 1 " +
              "at status 0): DOOR_WONT_OPEN_STAR_CHARM (message special, zone, charm); a spawned Jack missing " +
              "(GetMobByID nil; offset 0, 3) at status 0: nothing",
              $"{door}|{door}|{door}|null/|null/",
              string.Join("|", new[]
              {
                  N5Mob(F(1), J, true, true), N5Mob(F(2), J, true, false, offset: 3), N5Mob(F(0), J, true, true,
                      offset: 1),
                  N5Mob(F(0), J, true, true, true), N5Mob(F(0), J, true, true, true, 3),
              }.Select(f => N5RC(gate, f))));
        string set = $"Player.SetMissionStatus({w},2)";
        foreach (var npc in new[]
                 {
                     QC.Npc.OuterHorutotoRuins.JackofBatons, QC.Npc.OuterHorutotoRuins.JackofCoins,
                     QC.Npc.OuterHorutotoRuins.JackofCups, QC.Npc.OuterHorutotoRuins.JackofSwords,
                 })
        {
            var h = C2MHandler(d, 1, N5Death, ohr, npc);
            Check($"{N5} {L}: {npc}'s death at status 1, no Jack up: status 2; at 0 and 2: nothing",
                  $"{set}||", $"{C2MRun(h, F(1))}|{C2MRun(h, F(0))}|{C2MRun(h, F(2))}");
        }
        var bat = C2MHandler(d, 1, N5Death, ohr, QC.Npc.OuterHorutotoRuins.JackofBatons);
        Check($"{N5} {L}: a Jack's death at status 1: a Jack spawned and alive (offset 0, 3): nothing; spawned and " +
              "dead, alive and not spawned, spawned and alive but missing (offset 3), one at offset 4: status 2",
              $"||{set}|{set}|{set}|{set}",
              string.Join("|", new[]
              {
                  N5Mob(F(1), J, true, true), N5Mob(F(1), J, true, true, offset: 3), N5Mob(F(1), J, true, false),
                  N5Mob(F(1), J, false, true), N5Mob(F(1), J, true, true, true, 3),
                  N5Mob(F(1), J, true, true, offset: 4),
              }.Select(f => C2MRun(bat, f))));
        Check($"{N5} {L}: 68's finish sets status 3 and takes the charm",
              $"Player.SetMissionStatus({w},3),Player.DelKeyItem({charm})",
              C2MRun(C2MHandler(d, 1, N4Fin, ohr, id: 68), F(2)));
        ushort fmf = QC.Zone.FullMoonFountain;
        var zin = C2MHandler(d, 1, N4Zone, fmf, id: 1);
        Check($"{N5} {L}: Full Moon Fountain zone-in at status 3: 50; at 2 and 4: nothing; 50's finish completes",
              "Event(50)|null|null|Mission.Complete()",
              $"{C3NReply(zin, F(3))}|{C3NReply(zin, F(2))}|{C3NReply(zin, F(4))}|" +
              $"{C2MRun(C2MHandler(d, 1, N4Fin, fmf, id: 50), F(3))}");
    }

    static void RunC3N5SaintlyInvitation()
    {
        const string L = "saintly invitation";
        var m = new QC.Missions.Windurst.SaintlyInvitation();
        var d = m.Compile();
        int w = QC.MissionLog.Windurst, id = QC.Mission.Windurst.SaintlyInvitation, none = QC.Mission.Windurst.None;
        ushort ht = QC.Zone.HeavensTower, co = QC.Zone.CastleOztroja;
        const int invite = QC.KeyItem.HolyOnesInvitation, oath = QC.KeyItem.HolyOnesOath;
        const int cert = QC.KeyItem.BalgaChampionCertificate;
        N4Header(L, m, d, QC.MissionKey.Windurst.SaintlyInvitation, "scripts/missions/windurst/6_2_Saintly_Invitation",
                 "40000/7//", N5);
        Check($"{N5} {L}: two sections", "2", d.States.Count.ToString());
        N4Section1Open(L, d, MissionLog.Windurst, id, none, QC.Mission.Windurst.FullMoonFountain, N5);
        N5WindurstAccept(L, d, id, 17);
        N4Current(L, d, 1, MissionLog.Windurst, id, none, N5);
        Fake F(int status) => N4Fake(MissionLog.Windurst, id, id, status);
        string R(ushort zone, string npc, Fake f) => C3NReply(C2MHandler(d, 1, N4Trig, zone, npc), f);
        string door = QC.Npc.HeavensTower._6q2;
        Check($"{N5} {L}: _6q2 at status 0: 310; at 3: 312 (progress); at 1, 2 and 4: nothing",
              "ProgressEvent(310)|ProgressEvent(312)|null|null|null",
              string.Join("|", new[] { 0, 3, 1, 2, 4 }.Select(st => R(ht, door, F(st)))));
        N5Fixed(L, d, 1, F(1),
                (ht, QC.Npc.HeavensTower.FooBeibo, 304), (ht, QC.Npc.HeavensTower.ShazNorem, 306),
                (ht, QC.Npc.HeavensTower.VahnPaineesha, 305), (ht, QC.Npc.HeavensTower.Zubaba, 301),
                (QC.Zone.PortWindurst, QC.Npc.PortWindurst.JanshuraRashura, 464),
                (QC.Zone.PortWindurst, QC.Npc.PortWindurst.NineofClubs, 465),
                (QC.Zone.PortWindurst, QC.Npc.PortWindurst.PuoRhen, 467),
                (QC.Zone.PortWindurst, QC.Npc.PortWindurst.TenofClubs, 466),
                (QC.Zone.WindurstWalls, QC.Npc.WindurstWalls.ChawoShipeynyo, 359),
                (QC.Zone.WindurstWalls, QC.Npc.WindurstWalls.KeoKoruo, 357),
                (QC.Zone.WindurstWalls, QC.Npc.WindurstWalls.PakkePokke, 358),
                (QC.Zone.WindurstWalls, QC.Npc.WindurstWalls.ZokimaRokima, 356),
                (QC.Zone.WindurstWaters, QC.Npc.WindurstWaters.DagozaBeruza, 708),
                (QC.Zone.WindurstWaters, QC.Npc.WindurstWaters.Mokyokyo, 706),
                (QC.Zone.WindurstWaters, QC.Npc.WindurstWaters.PannaDonna, 707),
                (QC.Zone.WindurstWaters, QC.Npc.WindurstWaters.TenofHearts, 709),
                (QC.Zone.WindurstWoods, QC.Npc.WindurstWoods.MiiriWohri, 564),
                (QC.Zone.WindurstWoods, QC.Npc.WindurstWoods.RakohBuuma, 563),
                (QC.Zone.WindurstWoods, QC.Npc.WindurstWoods.SolaJaab, 565),
                (QC.Zone.WindurstWoods, QC.Npc.WindurstWoods.TihPikeh, 566));
        Check($"{N5} {L}: Kupipi at status 0: 300; 1 and 2: 311; else (3, 4): 333 (events)",
              "Event(300)|Event(311)|Event(311)|Event(333)|Event(333)",
              string.Join("|", Enumerable.Range(0, 5).Select(st => R(ht, QC.Npc.HeavensTower.Kupipi, F(st)))));
        Check($"{N5} {L}: Rhy Epocan at status 0 and 3: 302; else (1, 2, 4): 307 (events)",
              "Event(302)|Event(307)|Event(307)|Event(302)|Event(307)",
              string.Join("|", Enumerable.Range(0, 5).Select(st => R(ht, QC.Npc.HeavensTower.RhyEpocan, F(st)))));
        Check($"{N5} {L}: Ufu Koromoa at status 0: 303; else (1, 3): 308 (events)",
              "Event(303)|Event(308)|Event(308)",
              string.Join("|", new[] { 0, 1, 3 }.Select(st => R(ht, QC.Npc.HeavensTower.UfuKoromoa, F(st)))));
        Check($"{N5} {L}: 310's finish sets status 1, the Hero on Behalf of Windurst title, the Holy One's Invitation",
              $"Player.SetMissionStatus({w},1),Player.AddTitle({QC.Title.HeroOnBehalfOfWindurst})," +
              $"Player.GiveKeyItem({invite})",
              C2MRun(C2MHandler(d, 1, N4Fin, ht, id: 310), F(0)));
        N4Complete(L, "312's finish", C2MHandler(d, 1, N4Fin, ht, id: 312), () => F(3), oath, tag: N5);
        var win = C2MHandler(d, 1, N4Fin, QC.Zone.BalgasDais, id: 32001);
        int si = QC.Battlefield.SaintlyInvitation;
        Fake Win(int status, int bf = -1) => N4With(F(status), win: bf);
        Check($"{N5} {L}: Balga's Dais 32001 at status 1 with battlefieldWin {si}: the Victor of the Balga Contest " +
              "title, the Balga Champion Certificate, the invitation taken, status 2; at 0 and 2 with the win, at 1 " +
              $"with {si - 1}, {si + 1} and none: nothing",
              $"Player.AddTitle({QC.Title.VictorOfTheBalgaContest}),Player.GiveKeyItem({cert})," +
              $"Player.DelKeyItem({invite}),Player.SetMissionStatus({w},2)|||||",
              string.Join("|", new[]
              {
                  Win(1, si), Win(0, si), Win(2, si), Win(1, si - 1), Win(1, si + 1), Win(1),
              }.Select(f => C2MRun(win, f))));
        string kaa = QC.Npc.CastleOztroja.KaaTorutheJust;
        Check($"{N5} {L}: Kaa Toru the Just at status 2: 45 (progress cutscene, 0 and the Holy One's Oath); at 3: 46 " +
              "(event); at 1 and 4: nothing",
              $"ProgressCutscene(45,0,{oath})|Event(46)|null|null",
              string.Join("|", new[] { 2, 3, 1, 4 }.Select(st => R(co, kaa, F(st)))));
        var f45 = C2MHandler(d, 1, N4Fin, co, id: 45);
        var full = F(2);
        full.GiveResult = false;
        Check($"{N5} {L}: 45's finish: the Ashura Necklace given, then the oath, the certificate taken, status 3; " +
              "the necklace refused: nothing more",
              $"Player.GiveItem({QC.Item.AshuraNecklace}),Player.GiveKeyItem({oath}),Player.DelKeyItem({cert})," +
              $"Player.SetMissionStatus({w},3)|Player.GiveItem({QC.Item.AshuraNecklace})",
              $"{C2MRun(f45, F(2))}|{C2MRun(f45, full)}");
    }

    static void RunC3N5TheSixthMinistry()
    {
        const string L = "sixth ministry";
        var m = new QC.Missions.Windurst.TheSixthMinistry();
        var d = m.Compile();
        int w = QC.MissionLog.Windurst, id = QC.Mission.Windurst.TheSixthMinistry, none = QC.Mission.Windurst.None;
        ushort wa = QC.Zone.WindurstWaters, tc = QC.Zone.ToraimaraiCanal;
        const int ring = QC.KeyItem.OptisteryRing;
        N4Header(L, m, d, QC.MissionKey.Windurst.TheSixthMinistry, "scripts/missions/windurst/7_1_The_Sixth_Ministry",
                 $"//{QC.KeyItem.BlankBookOfTheGods}/700", N5);
        Check($"{N5} {L}: three sections", "3", d.States.Count.ToString());
        N4Section1Open(L, d, MissionLog.Windurst, id, none, QC.Mission.Windurst.SaintlyInvitation, N5);
        N5WindurstAccept(L, d, id, 18);
        N4Current(L, d, 1, MissionLog.Windurst, id, none, N5);
        Fake F(int status, int current = -1, bool done = false) =>
            done
                ? N4Fake(MissionLog.Windurst, id, current < 0 ? none : current, status, done: id)
                : N4Fake(MissionLog.Windurst, id, current < 0 ? id : current, status);
        N5Fixed(L, d, 1, F(1),
                (QC.Zone.PortWindurst, QC.Npc.PortWindurst.JanshuraRashura, 469),
                (QC.Zone.PortWindurst, QC.Npc.PortWindurst.NineofClubs, 470),
                (QC.Zone.PortWindurst, QC.Npc.PortWindurst.PuoRhen, 472),
                (QC.Zone.PortWindurst, QC.Npc.PortWindurst.TenofClubs, 471),
                (QC.Zone.WindurstWalls, QC.Npc.WindurstWalls.ChawoShipeynyo, 364),
                (QC.Zone.WindurstWalls, QC.Npc.WindurstWalls.KeoKoruo, 363),
                (QC.Zone.WindurstWalls, QC.Npc.WindurstWalls.PakkePokke, 362),
                (QC.Zone.WindurstWalls, QC.Npc.WindurstWalls.ZokimaRokima, 361),
                (wa, QC.Npc.WindurstWaters.DagozaBeruza, 713), (wa, QC.Npc.WindurstWaters.Mokyokyo, 711),
                (wa, QC.Npc.WindurstWaters.PannaDonna, 712), (wa, QC.Npc.WindurstWaters.TenofHearts, 714),
                (QC.Zone.WindurstWoods, QC.Npc.WindurstWoods.MiiriWohri, 570),
                (QC.Zone.WindurstWoods, QC.Npc.WindurstWoods.RakohBuuma, 569),
                (QC.Zone.WindurstWoods, QC.Npc.WindurstWoods.SolaJaab, 571),
                (QC.Zone.WindurstWoods, QC.Npc.WindurstWoods.TihPikeh, 572));
        string tos = QC.Npc.WindurstWaters.TosukaPorika;
        Check($"{N5} {L}: Tosuka-Porika at status 0: 715 (progress, 0 and the Optistery Ring); at 1: 716 (event, " +
              "the same); at 2: 724 (progress); at 3: nothing",
              $"ProgressEvent(715,0,{ring})|Event(716,0,{ring})|ProgressEvent(724)|null",
              string.Join("|", Enumerable.Range(0, 4).Select(st =>
                  C3NReply(C2MHandler(d, 1, N4Trig, wa, tos), F(st)))));
        Check($"{N5} {L}: 715's finish gives the ring and sets status 1",
              $"Player.GiveKeyItem({ring}),Player.SetMissionStatus({w},1)",
              C2MRun(C2MHandler(d, 1, N4Fin, wa, id: 715), F(0)));
        N4Complete(L, "724's finish", C2MHandler(d, 1, N4Fin, wa, id: 724), () => F(2), ring, tag: N5);
        var s3 = d.States[2];
        Check($"{N5} {L}: section 3 holds with the mission current, or completed (none current, Awakening of the " +
              "Gods current); fails with none current before completion, and with Saintly Invitation current",
              "True True True False False",
              $"{s3.IsMissionMatch(F(1))} {s3.IsMissionMatch(F(0, done: true))} " +
              $"{s3.IsMissionMatch(F(0, QC.Mission.Windurst.AwakeningOfTheGods, true))} " +
              $"{s3.IsMissionMatch(F(0, none))} {s3.IsMissionMatch(F(0, QC.Mission.Windurst.SaintlyInvitation))}");
        string H = QC.Mob.ToraimaraiCanal.HINGE_OILS_OFFSET;
        var pc = C2MHandler(d, 2, N4Trig, tc, QC.Npc.ToraimaraiCanal._4pc);
        string alive = "Event(70,0,0,0,1)", dead = "ProgressCutscene(70,0,0,0,2)";
        Check($"{N5} {L}: _4pc with all four Hinge Oils dead: 70 (progress cutscene, 0, 0, 0, 2); one alive " +
              "(offset 0, 3; spawned or not): 70 (event, 0, 0, 0, 1); alive at offset 4: dead",
              $"{dead}|{alive}|{alive}|{alive}|{dead}",
              string.Join("|", new[]
              {
                  F(1), N5Mob(F(1), H, true, true), N5Mob(F(1), H, true, true, offset: 3),
                  N5Mob(F(1, done: true), H, false, true, offset: 3), N5Mob(F(1), H, true, true, offset: 4),
              }.Select(f => C3NReply(pc, f))));
        Fake T(int offset, int status = 1, int current = -1, bool done = false)
        {
            var f = F(status, current, done);
            f.NpcIds["TOME_OF_MAGIC_OFFSET"] = 17000;
            f.Npc = new QuestNpcFake(17000 + offset);
            return f;
        }
        var tome = C2MHandler(d, 2, N4Trig, tc, QC.Npc.ToraimaraiCanal.TomeofMagic);
        Check($"{N5} {L}: Tome of Magic offset 4 with the mission current at status 1: 69 (progress, 0 and the " +
              "ring); offset 3 and 5, status 0 and 2, completed with none current: nothing; 69's finish sets status 2",
              $"ProgressEvent(69,0,{ring})|null|null|null|null|null|Player.SetMissionStatus({w},2)",
              string.Join("|", new[]
              {
                  T(4), T(3), T(5), T(4, 0), T(4, 2), T(4, done: true),
              }.Select(f => C3NReply(tome, f))) + "|" + C2MRun(C2MHandler(d, 2, N4Fin, tc, id: 69), T(4)));
    }

    static void RunC3N5AwakeningOfTheGods()
    {
        const string L = "awakening of the gods";
        var m = new QC.Missions.Windurst.AwakeningOfTheGods();
        var d = m.Compile();
        int w = QC.MissionLog.Windurst, id = QC.Mission.Windurst.AwakeningOfTheGods, none = QC.Mission.Windurst.None;
        ushort wa = QC.Zone.WindurstWaters, kz = QC.Zone.Kazham, tu = QC.Zone.TempleOfUggalepih;
        const int godsBook = QC.KeyItem.BookOfTheGods;
        N4Header(L, m, d, QC.MissionKey.Windurst.AwakeningOfTheGods,
                 "scripts/missions/windurst/7_2_Awakening_of_the_Gods", "60000/8//", N5);
        Check($"{N5} {L}: two sections", "2", d.States.Count.ToString());
        N4Section1Open(L, d, MissionLog.Windurst, id, none, QC.Mission.Windurst.TheSixthMinistry, N5);
        N5WindurstAccept(L, d, id, 19);
        N4Current(L, d, 1, MissionLog.Windurst, id, none, N5);
        Fake F(int status, bool hasBook = false, bool trade = false, double z = 0)
        {
            var f = N4With(N4Fake(MissionLog.Windurst, id, id, status), hasBook ? godsBook : 0);
            f.TradeResult = trade;
            f.ZPos = z;
            return f;
        }
        string R(ushort zone, string npc, Fake f) => C3NReply(C2MHandler(d, 1, N4Trig, zone, npc), f);
        N5Fixed(L, d, 1, F(1),
                (QC.Zone.PortWindurst, QC.Npc.PortWindurst.JanshuraRashura, 474),
                (QC.Zone.PortWindurst, QC.Npc.PortWindurst.NineofClubs, 475),
                (QC.Zone.PortWindurst, QC.Npc.PortWindurst.PuoRhen, 477),
                (QC.Zone.PortWindurst, QC.Npc.PortWindurst.TenofClubs, 476),
                (QC.Zone.WindurstWalls, QC.Npc.WindurstWalls.ChawoShipeynyo, 369),
                (QC.Zone.WindurstWalls, QC.Npc.WindurstWalls.KeoKoruo, 368),
                (QC.Zone.WindurstWalls, QC.Npc.WindurstWalls.PakkePokke, 367),
                (QC.Zone.WindurstWalls, QC.Npc.WindurstWalls.ZokimaRokima, 366),
                (wa, QC.Npc.WindurstWaters.DagozaBeruza, 731), (wa, QC.Npc.WindurstWaters.KenapaKeppa, 740),
                (wa, QC.Npc.WindurstWaters.Mokyokyo, 730), (wa, QC.Npc.WindurstWaters.OhbiruDohbiru, 741),
                (wa, QC.Npc.WindurstWaters.PannaDonna, 732), (wa, QC.Npc.WindurstWaters.TenofHearts, 733),
                (QC.Zone.WindurstWoods, QC.Npc.WindurstWoods.MiiriWohri, 575),
                (QC.Zone.WindurstWoods, QC.Npc.WindurstWoods.RakohBuuma, 574),
                (QC.Zone.WindurstWoods, QC.Npc.WindurstWoods.SolaJaab, 576),
                (QC.Zone.WindurstWoods, QC.Npc.WindurstWoods.TihPikeh, 577));
        Check($"{N5} {L}: Kerutoto at status 0: 737 (event); 1: 736 (progress); 2 and 3 (from 2 on): 738 (event)",
              "Event(737)|ProgressEvent(736)|Event(738)|Event(738)",
              string.Join("|", Enumerable.Range(0, 4).Select(st => R(wa, QC.Npc.WindurstWaters.Kerutoto, F(st)))));
        string lh = QC.Npc.WindurstWaters.LeepeHoppe;
        Check($"{N5} {L}: Leepe-Hoppe at status 0: 734 (progress); 1: 735; 2 to 4: 739 (events); 5 with the Book of " +
              "the Gods: 742 (progress); 5 without, 6 with: nothing",
              "ProgressEvent(734)|Event(735)|Event(739)|Event(739)|Event(739)|ProgressEvent(742)|null|null",
              string.Join("|", new[]
              {
                  F(0), F(1), F(2), F(3), F(4), F(5, true), F(5), F(6, true),
              }.Select(f => R(wa, lh, f))));
        Check($"{N5} {L}: 734's finish sets status 1; 736's status 2; 742's completes",
              $"Player.SetMissionStatus({w},1)|Player.SetMissionStatus({w},2)|Mission.Complete()",
              $"{C2MRun(C2MHandler(d, 1, N4Fin, wa, id: 734), F(0))}|" +
              $"{C2MRun(C2MHandler(d, 1, N4Fin, wa, id: 736), F(1))}|" +
              $"{C2MRun(C2MHandler(d, 1, N4Fin, wa, id: 742), F(5, true))}");
        Check($"{N5} {L}: Jakoh Wahcondalo at status 2: 265 (event); at 1 and 3: nothing",
              "Event(265)|null|null",
              string.Join("|", new[] { 2, 1, 3 }.Select(st => R(kz, QC.Npc.Kazham.JakohWahcondalo, F(st)))));
        Check($"{N5} {L}: Romaa Mihgo at status 2: 266 (progress); 3 and 4: 267; 5 and 6 (from 5 on): 263 " +
              "(events); at 1: nothing",
              "ProgressEvent(266)|Event(267)|Event(267)|Event(263)|Event(263)|null",
              string.Join("|", new[] { 2, 3, 4, 5, 6, 1 }.Select(st => R(kz, QC.Npc.Kazham.RomaaMihgo, F(st)))));
        Check($"{N5} {L}: Vanono at status 3: 264 (progress); above 3 (4): 268 (event); at 2: nothing",
              "ProgressEvent(264)|Event(268)|null",
              string.Join("|", new[] { 3, 4, 2 }.Select(st => R(kz, QC.Npc.Kazham.Vanono, F(st)))));
        Check($"{N5} {L}: 264's finish sets status 4; 266's status 3",
              $"Player.SetMissionStatus({w},4)|Player.SetMissionStatus({w},3)",
              $"{C2MRun(C2MHandler(d, 1, N4Fin, kz, id: 264), F(3))}|" +
              $"{C2MRun(C2MHandler(d, 1, N4Fin, kz, id: 266), F(2))}");
        var fx = C2MHandler(d, 1, N5Trade, tu, QC.Npc.TempleOfUggalepih._4fx);
        string cs = "ProgressCutscene(23,0,0,1,0,0,0,0,0)";
        Check($"{N5} {L}: _4fx's trade of the Cursed Key south of z 332 (331.9) at status 3 and 4: 23 (progress " +
              "cutscene); at 2 and 5, at z 332 and 332.1, without the key: nothing",
              $"{cs}|{cs}|null|null|null|null|null",
              string.Join("|", new[]
              {
                  F(3, trade: true, z: 331.9), F(4, trade: true, z: 331.9), F(2, trade: true, z: 331.9),
                  F(5, trade: true, z: 331.9), F(3, trade: true, z: 332), F(3, trade: true, z: 332.1),
                  F(3, z: 331.9),
              }.Select(f => C3NReply(fx, f))));
        Check($"{N5} {L}: 23's finish takes the trade and the Blank Book of the Gods, sets status 5 and gives the " +
              "Book of the Gods",
              $"Trade.Complete(),Player.DelKeyItem({QC.KeyItem.BlankBookOfTheGods}),Player.SetMissionStatus({w},5)," +
              $"Player.GiveKeyItem({godsBook})",
              C2MRun(C2MHandler(d, 1, N4Fin, tu, id: 23), F(3)));
    }
}
