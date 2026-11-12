// XI.Verify.Quests -- quest rework C3 N6: every reply of the rank 8-9 missions on the fake, its kind printed: each
// trigger and zone-in handler over statuses 0 to 11 (with the key items, mobs and vars its branches read), each
// mob death on the mobs it reads. The expected lines are the Lua's (scripts/missions/<nation>/<8|9>_<n>_*.lua at
// 64c010fe): its progressEvent / event / progressCutscene / messageSpecial / messageText / keyItem per branch.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    // a handler's replies over statuses 0 to 11 of a fake builder, '|'-joined
    static string N6Over(QuestHandlerDecl h, Func<int, Fake> f) =>
        string.Join("|", Enumerable.Range(0, 12).Select(s => N6Reply(h, f(s))));

    // the expected line: the replies given at their statuses, "null" at the others (0 to 11)
    static string N6Line(params (int Status, string Reply)[] at) =>
        string.Join("|", Enumerable.Range(0, 12).Select(s => at.FirstOrDefault(a => a.Status == s).Reply ?? "null"));

    // a mob's (or GetMobByID's) state on the fake: spawned and alive, spawned and dead, or not spawned (offset given)
    static Fake N6Mob(Fake f, string mob, string state, int offset = 0)
    {
        string k = $"{mob}+{offset}";
        if (state != "none") f.SpawnedMobs.Add(k);
        if (state == "alive") f.AliveMobs.Add(k);
        if (state == "missing") f.MissingMobs.Add(k);
        return f;
    }

    static Fake N6With(Fake f, Action<Fake> a)
    {
        a(f);
        return f;
    }

    static Fake N6Var(Fake f, MissionLog log, int id, string name, int value)
    {
        f.MissionVars[$"{(int)log}/{id}/{name}"] = value;
        return f;
    }

    static void RunC3N6Replies()
    {
        RunC3N6RepliesBastok();
        RunC3N6RepliesSandoria();
        RunC3N6RepliesWindurst();
    }

    static void RunC3N6RepliesBastok()
    {
        const MissionLog B = MissionLog.Bastok;
        ushort mw = QC.Zone.Metalworks, qs = QC.Zone.QuicksandCaves;

        // 8-1 The Chains That Bind Us
        {
            int id = QC.Mission.Bastok.TheChainsThatBindUs;
            var d = new QC.Missions.Bastok.TheChainsThatBindUs().Compile();
            Fake F(int s) => N6Fake(B, id, id, status: s);
            string T(ushort zone, string npc, Func<int, Fake> f) => N6Over(N6Npc(d, 1, N6Trig, zone, npc), f);
            Check("c3 N6 chains: _6ld: 702 at status 3 (progress)",
                  N6Line((3, "ProgressEvent(702)")), T(mw, QC.Npc.Metalworks._6ld, F));
            Check("c3 N6 chains: Iron Eater: 767 at 0, 702 at 3, 768 at 4 (progress)",
                  N6Line((0, "ProgressEvent(767)"), (3, "ProgressEvent(702)"), (4, "ProgressEvent(768)")),
                  T(mw, QC.Npc.Metalworks.IronEater, F));
            Check("c3 N6 chains: qm4: 10 at status 3 (progress)",
                  N6Line((3, "ProgressEvent(10)")), T(qs, QC.Npc.QuicksandCaves.qm4, F));
            string foreboding = $"MessageSpecial({QC.Text.QuicksandCaves.SENSE_OF_FOREBODING})";
            Check("c3 N6 chains: qm6 with none of the three up: SENSE_OF_FOREBODING at 1 (message special), 11 at 2 " +
                  "(progress)",
                  N6Line((1, foreboding), (2, "ProgressEvent(11)")), T(qs, QC.Npc.QuicksandCaves.qm6, F));
            foreach (var mob in new[] { QC.Mob.QuicksandCaves.CENTURIO_IV_VII, QC.Mob.QuicksandCaves.TRIARIUS_IV_XIV,
                                        QC.Mob.QuicksandCaves.PRINCEPS_IV_XLV })
                Check($"c3 N6 chains: qm6 with {mob} spawned (alive; dead, still spawned): at 1 nothing; at 2 11",
                      N6Line((2, "ProgressEvent(11)")) + "#" + N6Line((2, "ProgressEvent(11)")),
                      T(qs, QC.Npc.QuicksandCaves.qm6, s => N6Mob(F(s), mob, "alive")) + "#" +
                      T(qs, QC.Npc.QuicksandCaves.qm6, s => N6Mob(F(s), mob, "dead")));
            Check("c3 N6 chains: qm6's spawn: the three with the char's claim",
                  $"Player.SpawnMob({QC.Mob.QuicksandCaves.CENTURIO_IV_VII},0,True)," +
                  $"Player.SpawnMob({QC.Mob.QuicksandCaves.TRIARIUS_IV_XIV},0,True)," +
                  $"Player.SpawnMob({QC.Mob.QuicksandCaves.PRINCEPS_IV_XLV},0,True)",
                  N6Run(N6Npc(d, 1, N6Trig, qs, QC.Npc.QuicksandCaves.qm6), F(1)));
            // each NM's death reads the other two: dead or not spawned
            var nms = new[]
            {
                (QC.Npc.QuicksandCaves.CenturioIVVII, QC.Mob.QuicksandCaves.TRIARIUS_IV_XIV,
                 QC.Mob.QuicksandCaves.PRINCEPS_IV_XLV),
                (QC.Npc.QuicksandCaves.TriariusIVXIV, QC.Mob.QuicksandCaves.CENTURIO_IV_VII,
                 QC.Mob.QuicksandCaves.PRINCEPS_IV_XLV),
                (QC.Npc.QuicksandCaves.PrincepsIVXLV, QC.Mob.QuicksandCaves.CENTURIO_IV_VII,
                 QC.Mob.QuicksandCaves.TRIARIUS_IV_XIV),
            };
            foreach (var (npc, a, c) in nms)
            {
                var h = N6Npc(d, 1, N6Death, qs, npc);
                string R(int s, string sa, string sc) => N6Run(h, N6Mob(N6Mob(F(s), a, sa), c, sc));
                Check($"c3 N6 chains: {npc}'s death at status 1: the other two not spawned, dead, or one of each: " +
                      "status 2; either alive: nothing; at statuses 0 and 2: nothing",
                      "Player.SetMissionStatus(1,2)|Player.SetMissionStatus(1,2)|Player.SetMissionStatus(1,2)|" +
                      "Player.SetMissionStatus(1,2)||||",
                      $"{R(1, "none", "none")}|{R(1, "dead", "dead")}|{R(1, "dead", "none")}|{R(1, "none", "dead")}|" +
                      $"{R(1, "alive", "dead")}|{R(1, "dead", "alive")}|{R(0, "none", "none")}|{R(2, "none", "none")}");
            }
            Check("c3 N6 chains: 10's finish sets status 4, 11's status 3, 768's completes",
                  "Player.SetMissionStatus(1,4)|Player.SetMissionStatus(1,3)|Mission.Complete()",
                  $"{N6Run(N6Csid(d, 1, N6Fin, qs, 10), F(3))}|{N6Run(N6Csid(d, 1, N6Fin, qs, 11), F(2))}|" +
                  $"{N6Run(N6Csid(d, 1, N6Fin, mw, 768), F(4))}");
        }

        // 8-2 Enter the Talekeeper
        {
            int id = QC.Mission.Bastok.EnterTheTalekeeper;
            var d = new QC.Missions.Bastok.EnterTheTalekeeper().Compile();
            ushort kt = QC.Zone.KuftalTunnel, zm = QC.Zone.ZeruhnMines, bm = QC.Zone.BastokMines;
            string ghost = QC.Mob.KuftalTunnel.TALEKEEPER_OFFSET;
            Fake F(int s) => N6Fake(B, id, id, status: s);
            string T(ushort zone, string npc, Func<int, Fake> f) => N6Over(N6Npc(d, 1, N6Trig, zone, npc), f);
            Check("c3 N6 talekeeper: Drake Fang: 202 at 0 (progress), 203 at 1 to 3 (event), 204 at 4 (progress)",
                  N6Line((0, "ProgressEvent(202)"), (1, "Event(203)"), (2, "Event(203)"), (3, "Event(203)"),
                         (4, "ProgressEvent(204)")),
                  T(zm, QC.Npc.ZeruhnMines.DrakeFang, F));
            Check("c3 N6 talekeeper: qm5: 12 at status 1 (progress)",
                  N6Line((1, "ProgressEvent(12)")), T(kt, QC.Npc.KuftalTunnel.qm5, F));
            string evil = $"MessageSpecial({QC.Text.KuftalTunnel.EVIL})";
            Check("c3 N6 talekeeper: qm6 with no ghost alive: EVIL at 2 (message special), 13 at 3 (progress)",
                  N6Line((2, evil), (3, "ProgressEvent(13)")), T(kt, QC.Npc.KuftalTunnel.qm6, F));
            for (int i = 0; i <= 2; i++)
            {
                int o = i;
                Check($"c3 N6 talekeeper: qm6 with ghost +{o} alive: nothing at 2 and 3; dead, or GetMobByID nil " +
                      "(spawned): as none",
                      N6Line() + "#" + N6Line((2, evil), (3, "ProgressEvent(13)")) + "#" +
                      N6Line((2, evil), (3, "ProgressEvent(13)")),
                      T(kt, QC.Npc.KuftalTunnel.qm6, s => N6Mob(F(s), ghost, "alive", o)) + "#" +
                      T(kt, QC.Npc.KuftalTunnel.qm6, s => N6Mob(F(s), ghost, "dead", o)) + "#" +
                      T(kt, QC.Npc.KuftalTunnel.qm6, s =>
                      {
                          var f = N6Mob(F(s), ghost, "alive", o);
                          f.MissingMobs.Add($"{ghost}+{o}");
                          return f;
                      }));
            }
            Check("c3 N6 talekeeper: qm6 reads getMissionStatus(getNation()): a San d'Orian char's San d'Oria log at " +
                  "2 with Bastok's at 0: EVIL",
                  evil,
                  N6Reply(N6Npc(d, 1, N6Trig, kt, QC.Npc.KuftalTunnel.qm6),
                          N6With(N6Fake(B, id, id, nation: 0), f => f.MissionStatuses[0] = 2)));
            Check("c3 N6 talekeeper: qm6's spawn: ghosts +0, +1 and +2 with no claim",
                  $"Player.SpawnMob({ghost},0),Player.SpawnMob({ghost},1),Player.SpawnMob({ghost},2)",
                  N6Run(N6Npc(d, 1, N6Trig, kt, QC.Npc.KuftalTunnel.qm6), F(2)));
            foreach (var npc in new[] { QC.Npc.KuftalTunnel.DervosGhost, QC.Npc.KuftalTunnel.GizerlsGhost,
                                        QC.Npc.KuftalTunnel.GordovsGhost })
            {
                var h = N6Npc(d, 1, N6Death, kt, npc);
                Check($"c3 N6 talekeeper: {npc}'s death at status 2: none alive: status 3; +0, +1 or +2 alive: " +
                      "nothing; at 1 and 3: nothing",
                      "Player.SetMissionStatus(1,3)|||||",
                      $"{N6Run(h, F(2))}|{N6Run(h, N6Mob(F(2), ghost, "alive", 0))}|" +
                      $"{N6Run(h, N6Mob(F(2), ghost, "alive", 1))}|{N6Run(h, N6Mob(F(2), ghost, "alive", 2))}|" +
                      $"{N6Run(h, F(1))}|{N6Run(h, F(3))}");
            }
            var zi = N6Csid(d, 1, N6Zone, bm);
            string Z(int s, int prev) => N6Reply(zi, N6With(F(s), f => f.PrevZone = prev));
            Check("c3 N6 talekeeper: Bastok Mines' zone-in: 176 from the Zeruhn Mines at status 5 (event); at 4 and " +
                  "from the Metalworks nothing",
                  "Event(176)|null|null",
                  $"{Z(5, zm)}|{Z(4, zm)}|{Z(5, mw)}");
            Check("c3 N6 talekeeper: finishes: 202 status 1; 204 status 5 and setPos(23, 0, 4); 12 with option 0 " +
                  "status 2 and FELL, option 1 nothing; 13 status 4 and the Old Piece of Wood; 176 completes then " +
                  "takes the wood",
                  $"Player.SetMissionStatus(1,1)|Player.SetMissionStatus(1,5),Player.SetPos(23,0,4)|" +
                  $"Player.SetMissionStatus(1,2),Text.Special({QC.Text.KuftalTunnel.FELL})||" +
                  $"Player.SetMissionStatus(1,4),Player.GiveKeyItem({QC.KeyItem.OldPieceOfWood})|" +
                  $"Mission.Complete(),Player.DelKeyItem({QC.KeyItem.OldPieceOfWood})",
                  $"{N6Run(N6Csid(d, 1, N6Fin, zm, 202), F(0))}|{N6Run(N6Csid(d, 1, N6Fin, zm, 204), F(4))}|" +
                  $"{N6Run(N6Csid(d, 1, N6Fin, kt, 12), F(1))}|" +
                  $"{N6Run(N6Csid(d, 1, N6Fin, kt, 12), N6Fake(B, id, id, status: 1, option: 1))}|" +
                  $"{N6Run(N6Csid(d, 1, N6Fin, kt, 13), F(3))}|{N6Run(N6Csid(d, 1, N6Fin, bm, 176), F(5))}");
            // section 3
            Fake D(int current = -1, params int[] done) =>
                N6Fake(B, id, current < 0 ? QC.Mission.Bastok.None : current, done: done);
            var s3 = d.States[2];
            Check("c3 N6 talekeeper: section 3 holds for a Bastokan with no mission current who has completed 8-2 " +
                  "and not 9-1; fails with 9-1 completed, with 8-2 not completed, with a mission current, for a " +
                  "San d'Orian",
                  "True False False False False",
                  $"{s3.IsMissionMatch(D(-1, id))} " +
                  $"{s3.IsMissionMatch(D(-1, id, QC.Mission.Bastok.TheSaltOfTheEarth))} " +
                  $"{s3.IsMissionMatch(D())} {s3.IsMissionMatch(D(QC.Mission.Bastok.TheSaltOfTheEarth, id))} " +
                  $"{s3.IsMissionMatch(N6Fake(B, id, QC.Mission.Bastok.None, nation: 0, done: id))}");
            Check("c3 N6 talekeeper: section 3: Detzo 184, Gumbah 183, Pavvke 76 (1), Iron Eater 769 (events, " +
                  "importantOnce)",
                  "Event(184):importantOnce()|Event(183):importantOnce()|Event(76,1):importantOnce()|" +
                  "Event(769):importantOnce()",
                  $"{N6Reply(N6Npc(d, 2, N6Trig, bm, QC.Npc.BastokMines.Detzo), D(-1, id))}|" +
                  $"{N6Reply(N6Npc(d, 2, N6Trig, bm, QC.Npc.BastokMines.Gumbah), D(-1, id))}|" +
                  $"{N6Reply(N6Npc(d, 2, N6Trig, bm, QC.Npc.BastokMines.Pavvke), D(-1, id))}|" +
                  $"{N6Reply(N6Npc(d, 2, N6Trig, mw, QC.Npc.Metalworks.IronEater), D(-1, id))}");
        }

        // 9-1 The Salt of the Earth
        {
            int id = QC.Mission.Bastok.TheSaltOfTheEarth;
            var d = new QC.Missions.Bastok.TheSaltOfTheEarth().Compile();
            ushort gt = QC.Zone.GustavTunnel, rb = QC.Zone.Rabao;
            string plasm = QC.Mob.GustavTunnel.GIGAPLASM;
            Fake F(int s) => N6Fake(B, id, id, status: s);
            string T(ushort zone, string npc, Func<int, Fake> f) => N6Over(N6Npc(d, 1, N6Trig, zone, npc), f);
            Check("c3 N6 salt: Alois: 773 at 0, 774 at 1, 775 at 3, 776 at 4 (progress)",
                  N6Line((0, "ProgressEvent(773)"), (1, "ProgressEvent(774)"), (3, "ProgressEvent(775)"),
                         (4, "ProgressEvent(776)")),
                  T(mw, QC.Npc.Metalworks.Alois, F));
            Check("c3 N6 salt: Dancing Wolf: 102 at 1, 103 at 2, 104 at 3, 105 at 4 (progress)",
                  N6Line((1, "ProgressEvent(102)"), (2, "ProgressEvent(103)"), (3, "ProgressEvent(104)"),
                         (4, "ProgressEvent(105)")),
                  T(rb, QC.Npc.Rabao.DancingWolf, F));
            string bad = $"MessageSpecial({QC.Text.GustavTunnel.BAD_FEELING_ABOUT_PLACE})";
            string salt = $"KeyItem({QC.KeyItem.Miraclesalt})";
            Check("c3 N6 salt: qm2 with no plasm alive: BAD_FEELING_ABOUT_PLACE at 2 (message special); the " +
                  "Miraclesalt at 3 without it (keyItem); with it nothing",
                  N6Line((2, bad), (3, salt)) + "#" + N6Line((2, bad)),
                  T(gt, QC.Npc.GustavTunnel.qm2, F) + "#" +
                  T(gt, QC.Npc.GustavTunnel.qm2, s => N6Keys(F(s), QC.KeyItem.Miraclesalt)));
            foreach (int o in new[] { 0, 7, 14 })
                Check($"c3 N6 salt: qm2 with plasm +{o} alive: nothing at 2; the Miraclesalt still at 3",
                      N6Line((3, salt)), T(gt, QC.Npc.GustavTunnel.qm2, s => N6Mob(F(s), plasm, "alive", o)));
            Check("c3 N6 salt: qm2's spawn: the Gigaplasm with the char's claim",
                  $"Player.SpawnMob({plasm},0,True)", N6Run(N6Npc(d, 1, N6Trig, gt, QC.Npc.GustavTunnel.qm2), F(2)));
            var nano = N6Npc(d, 1, N6Death, gt, QC.Npc.GustavTunnel.Nanoplasm);
            Check("c3 N6 salt: a Nanoplasm's death at status 2 with no plasm alive: status 3; +0, +8 or +14 alive: " +
                  "nothing; at 1 and 3: nothing",
                  "Player.SetMissionStatus(1,3)|||||",
                  $"{N6Run(nano, F(2))}|{N6Run(nano, N6Mob(F(2), plasm, "alive", 0))}|" +
                  $"{N6Run(nano, N6Mob(F(2), plasm, "alive", 8))}|{N6Run(nano, N6Mob(F(2), plasm, "alive", 14))}|" +
                  $"{N6Run(nano, F(1))}|{N6Run(nano, F(3))}");
            Check("c3 N6 salt: finishes: 773 status 1; 776 completes then Option 1; 102 status 2; 104 status 4",
                  "Player.SetMissionStatus(1,1)|Mission.Complete(),Mission.SetVar(Option,1)|" +
                  "Player.SetMissionStatus(1,2)|Player.SetMissionStatus(1,4)",
                  $"{N6Run(N6Csid(d, 1, N6Fin, mw, 773), F(0))}|{N6Run(N6Csid(d, 1, N6Fin, mw, 776), F(4))}|" +
                  $"{N6Run(N6Csid(d, 1, N6Fin, rb, 102), F(1))}|{N6Run(N6Csid(d, 1, N6Fin, rb, 104), F(3))}");
            Fake D(int current = -1, params int[] done) =>
                N6Fake(B, id, current < 0 ? QC.Mission.Bastok.None : current, done: done);
            var s3 = d.States[2];
            Check("c3 N6 salt: section 3 holds for a Bastokan with no mission current who has completed 9-1 and not " +
                  "9-2; fails with 9-2 completed, 9-1 not completed, a mission current, for a Windurstian",
                  "True False False False False",
                  $"{s3.IsMissionMatch(D(-1, id))} " +
                  $"{s3.IsMissionMatch(D(-1, id, QC.Mission.Bastok.WhereTwoPathsConverge))} " +
                  $"{s3.IsMissionMatch(D())} {s3.IsMissionMatch(D(QC.Mission.Bastok.WhereTwoPathsConverge, id))} " +
                  $"{s3.IsMissionMatch(N6Fake(B, id, QC.Mission.Bastok.None, nation: 2, done: id))}");
            Check("c3 N6 salt: section 3: Alois 778 (event, importantOnce)",
                  "Event(778):importantOnce()", N6Reply(N6Npc(d, 2, N6Trig, mw, QC.Npc.Metalworks.Alois), D(-1, id)));
            var s4 = d.States[3];
            Fake O(int option, bool completed = true) =>
                N6Var(N6Fake(B, id, QC.Mission.Bastok.None, done: completed ? new[] { id } : new int[0]), B, id,
                      "Option", option);
            Check("c3 N6 salt: section 4 holds when completed with Option 1 or 2; fails with Option 0 or not completed",
                  "True True False False",
                  $"{s4.IsMissionMatch(O(1))} {s4.IsMissionMatch(O(2))} {s4.IsMissionMatch(O(0))} " +
                  $"{s4.IsMissionMatch(O(1, false))}");
            var fr = N6Npc(d, 3, N6Trig, mw, QC.Npc.Metalworks.Franziska);
            Check("c3 N6 salt: Franziska: Option 1 777, Option 2 779 (progress), Option 3 nothing; 777's finish sets " +
                  "Option 2, 779's Option 0",
                  "ProgressEvent(777)|ProgressEvent(779)|null|Mission.SetVar(Option,2)|Mission.SetVar(Option,0)",
                  $"{N6Reply(fr, O(1))}|{N6Reply(fr, O(2))}|{N6Reply(fr, O(3))}|" +
                  $"{N6Run(N6Csid(d, 3, N6Fin, mw, 777), O(1))}|{N6Run(N6Csid(d, 3, N6Fin, mw, 779), O(2))}");
        }

        // 9-2 Where Two Paths Converge
        {
            int id = QC.Mission.Bastok.WhereTwoPathsConverge;
            var d = new QC.Missions.Bastok.WhereTwoPathsConverge().Compile();
            Fake F(int s) => N6Fake(B, id, id, status: s);
            Check("c3 N6 where two paths: Iron Eater: 780 at 0, 769 at 1, 782 at 2 (progress)",
                  N6Line((0, "ProgressEvent(780)"), (1, "ProgressEvent(769)"), (2, "ProgressEvent(782)")),
                  N6Over(N6Npc(d, 1, N6Trig, mw, QC.Npc.Metalworks.IronEater), F));
            var won = N6Csid(d, 1, N6Fin, QC.Zone.ThroneRoom, 32001);
            string W(int s, int win) => N6Run(won, N4With(F(s), win: win));
            Check("c3 N6 where two paths: the Throne Room's 32001 finish: status 1 with battlefieldWin 161: status " +
                  "2; with 160 or 0, at 0 and 2: nothing",
                  "Player.SetMissionStatus(1,2)||||",
                  $"{W(1, 161)}|{W(1, 160)}|{W(1, 0)}|{W(0, 161)}|{W(2, 161)}");
            var fin = N6Csid(d, 1, N6Fin, mw, 782);
            var full = F(2);
            full.GiveResult = false;
            Check("c3 N6 where two paths: 782's finish: completes, gives the Bastokan Flag; a full bag: Flag 1",
                  $"Mission.Complete(),Player.GiveItem({QC.Item.BastokanFlag})|" +
                  $"Mission.Complete(),Player.GiveItem({QC.Item.BastokanFlag}),Mission.SetVar(Flag,1)",
                  $"{N6Run(fin, F(2))}|{N6Run(fin, full)}");
            Fake C(int flag, bool give = true)
            {
                var f = N6Var(N6Fake(B, id, QC.Mission.Bastok.None, done: id), B, id, "Flag", flag);
                f.GiveResult = give;
                return f;
            }
            var ie = N6Npc(d, 2, N6Trig, mw, QC.Npc.Metalworks.IronEater);
            Check("c3 N6 where two paths: section 3 (completed) Iron Eater: Flag 1: the flag and Flag 0; Flag 1 with " +
                  "a full bag: the flag refused, Flag kept; Flag 0: nothing; no reply in each",
                  $"null:Player.GiveItem({QC.Item.BastokanFlag}),Mission.SetVar(Flag,0)|" +
                  $"null:Player.GiveItem({QC.Item.BastokanFlag})|null:",
                  $"{N6Reply(ie, C(1))}:{N6Run(ie, C(1))}|{N6Reply(ie, C(1, false))}:{N6Run(ie, C(1, false))}|" +
                  $"{N6Reply(ie, C(0))}:{N6Run(ie, C(0))}");
        }
    }

    static void RunC3N6RepliesSandoria()
    {
        const MissionLog S = MissionLog.Sandoria;
        ushort ch = QC.Zone.ChateauDoraguille, qs = QC.Zone.QuicksandCaves, ns = QC.Zone.NorthernSanDoria;
        ushort ss = QC.Zone.SouthernSanDoria, tu = QC.Zone.TempleOfUggalepih;

        // 8-1 Coming of Age
        {
            int id = QC.Mission.Sandoria.ComingOfAge;
            var d = new QC.Missions.Sandoria.ComingOfAge().Compile();
            Fake F(int s) => N6Fake(S, id, id, status: s);
            string T(ushort zone, string npc, Func<int, Fake> f) => N6Over(N6Npc(d, 1, N6Trig, zone, npc), f);
            Check("c3 N6 coming of age: Halver: 58 at 1; 102 at 3 with the Drops of Amnio (progress), without them " +
                  "nothing",
                  N6Line((1, "ProgressEvent(58)"), (3, "ProgressEvent(102)")) + "#" + N6Line((1, "ProgressEvent(58)")),
                  T(ch, QC.Npc.ChateauDoraguille.Halver, s => N6Keys(F(s), QC.KeyItem.DropsOfAmnio)) + "#" +
                  T(ch, QC.Npc.ChateauDoraguille.Halver, F));
            Check("c3 N6 coming of age: _6h0 64 and _6h1 75 at status 2 and above (events)",
                  N6Line(Enumerable.Range(2, 10).Select(s => (s, "Event(64)")).ToArray()) + "#" +
                  N6Line(Enumerable.Range(2, 10).Select(s => (s, "Event(75)")).ToArray()),
                  T(ch, QC.Npc.ChateauDoraguille._6h0, F) + "#" + T(ch, QC.Npc.ChateauDoraguille._6h1, F));
            Check("c3 N6 coming of age: the Chateau's zone-in: 116 at status 0 (event)",
                  N6Line((0, "Event(116)")), N6Over(N6Csid(d, 1, N6Zone, ch), F));
            string evil = $"MessageSpecial({QC.Text.QuicksandCaves.SENSE_SOMETHING_EVIL})";
            string drops = $"KeyItem({QC.KeyItem.DropsOfAmnio})";
            var fk = N6Npc(d, 1, N6Trig, qs, QC.Npc.QuicksandCaves.FountainofKings);
            Check("c3 N6 coming of age: the Fountain of Kings with neither NM up: SENSE_SOMETHING_EVIL at 2 (message " +
                  "special); the drops at 3 without them (keyItem), with them nothing",
                  N6Line((2, evil), (3, drops)) + "#" + N6Line((2, evil)),
                  N6Over(fk, F) + "#" + N6Over(fk, s => N6Keys(F(s), QC.KeyItem.DropsOfAmnio)));
            foreach (var mob in new[] { QC.Mob.QuicksandCaves.VALOR, QC.Mob.QuicksandCaves.HONOR })
                Check($"c3 N6 coming of age: the Fountain with {mob} alive: nothing; dead: as none; GetMobByID nil: " +
                      "nothing",
                      N6Line() + "#" + N6Line((2, evil), (3, drops)) + "#" + N6Line(),
                      N6Over(fk, s => N6Mob(F(s), mob, "alive")) + "#" + N6Over(fk, s => N6Mob(F(s), mob, "dead")) +
                      "#" + N6Over(fk, s => N6Mob(F(s), mob, "missing")));
            Check("c3 N6 coming of age: the Fountain's spawn: Valor and Honor with no claim",
                  $"Player.SpawnMob({QC.Mob.QuicksandCaves.VALOR}),Player.SpawnMob({QC.Mob.QuicksandCaves.HONOR})",
                  N6Run(fk, F(2)));
            foreach (var (npc, mob) in new[] { (QC.Npc.QuicksandCaves.Honor, QC.Mob.QuicksandCaves.HONOR),
                                               (QC.Npc.QuicksandCaves.Valor, QC.Mob.QuicksandCaves.VALOR) })
            {
                var h = N6Npc(d, 1, N6Death, qs, npc);
                Check($"c3 N6 coming of age: {npc}'s death reads {mob} (itself, kept): at 2 dead or not spawned: " +
                      "status 3; alive or nil: nothing; at 1 and 3: nothing",
                      "Player.SetMissionStatus(0,3)|Player.SetMissionStatus(0,3)||||",
                      $"{N6Run(h, N6Mob(F(2), mob, "dead"))}|{N6Run(h, F(2))}|{N6Run(h, N6Mob(F(2), mob, "alive"))}|" +
                      $"{N6Run(h, N6Mob(F(2), mob, "missing"))}|{N6Run(h, F(1))}|{N6Run(h, F(3))}");
            }
            var c102 = N6Csid(d, 1, N6Fin, ch, 102);
            var no = F(3);
            no.CompleteResult = false;
            no.Now = 1000;
            var yes = F(3);
            yes.Now = 1000;
            Check("c3 N6 coming of age: finishes: 58 status 2, 116 status 1; 102 completes, Progress now + 60, the " +
                  "drops taken; complete false: nothing more",
                  "Player.SetMissionStatus(0,2)|Player.SetMissionStatus(0,1)|" +
                  $"Mission.Complete(),Mission.SetVar(Progress,1060),Player.DelKeyItem({QC.KeyItem.DropsOfAmnio})|" +
                  "Mission.Complete()",
                  $"{N6Run(N6Csid(d, 1, N6Fin, ch, 58), F(1))}|{N6Run(N6Csid(d, 1, N6Fin, ch, 116), F(0))}|" +
                  $"{N6Run(c102, yes)}|{N6Run(c102, no)}");
            Fake P(int progress, int now = 1000, bool done = true)
            {
                var f = N6Var(N6Fake(S, id, QC.Mission.Sandoria.None, done: done ? new[] { id } : new int[0]), S, id,
                              "Progress", progress);
                f.Now = now;
                return f;
            }
            var s3 = d.States[2];
            Check("c3 N6 coming of age: section 3 holds when completed with Progress above 0; fails at 0, -1 and " +
                  "not completed",
                  "True False False False",
                  $"{s3.IsMissionMatch(P(5))} {s3.IsMissionMatch(P(0))} {s3.IsMissionMatch(P(-1))} " +
                  $"{s3.IsMissionMatch(P(5, done: false))}");
            var nzi = N6Csid(d, 2, N6Zone, ns);
            var mog = P(500);
            mog.InAMogHouse = true;
            Check("c3 N6 coming of age: Northern San d'Oria's zone-in: Progress below now: 16 (event); equal or " +
                  "above: nothing; in the Mog House: nothing; 16's finish: Progress 0",
                  "Event(16)|null|null|null|Mission.SetVar(Progress,0)",
                  $"{N6Reply(nzi, P(999))}|{N6Reply(nzi, P(1000))}|{N6Reply(nzi, P(1001))}|{N6Reply(nzi, mog)}|" +
                  $"{N6Run(N6Csid(d, 2, N6Fin, ns, 16), P(5))}");
            var gf = P(5);
            gf.TextIds[QC.Text.NorthernSanDoria.ORIGINAL_MISSION_OFFSET.ToString()] = 7000;
            Check("c3 N6 coming of age: section 3: Grilau ORIGINAL_MISSION_OFFSET + 127 (message special, " +
                  "setPriority 1000)",
                  "MessageSpecial(7127):setPriority(1000)",
                  N6Reply(N6Npc(d, 2, N6Trig, ns, QC.Npc.NorthernSanDoria.Grilau), gf));
        }

        // 8-2 Lightbringer
        {
            int id = QC.Mission.Sandoria.Lightbringer;
            var d = new QC.Missions.Sandoria.Lightbringer().Compile();
            Fake F(int s) => N6Fake(S, id, id, status: s);
            string T(ushort zone, string npc, Func<int, Fake> f) => N6Over(N6Npc(d, 1, N6Trig, zone, npc), f);
            string extra = $"MessageText({QC.Text.ChateauDoraguille.LIGHTBRINGER_EXTRA}):replaceDefault()";
            Check("c3 N6 lightbringer: the Chateau: _6h4 100 at 0, 104 at 6; Aramaviont 15 at 6; Curilla 57 at 2, " +
                  "103 at 6; Milchupain 36 at 6; Rahal 106 at 1, 107 at 2, 105 at 6 (progress); Halver " +
                  "LIGHTBRINGER_EXTRA at 6 (message text, replaceDefault)",
                  N6Line((0, "ProgressEvent(100)"), (6, "ProgressEvent(104)")) + "#" +
                  N6Line((6, "ProgressEvent(15)")) + "#" +
                  N6Line((2, "ProgressEvent(57)"), (6, "ProgressEvent(103)")) + "#" +
                  N6Line((6, "ProgressEvent(36)")) + "#" +
                  N6Line((1, "ProgressEvent(106)"), (2, "ProgressEvent(107)"), (6, "ProgressEvent(105)")) + "#" +
                  N6Line((6, extra)),
                  T(ch, QC.Npc.ChateauDoraguille._6h4, F) + "#" + T(ch, QC.Npc.ChateauDoraguille.Aramaviont, F) + "#" +
                  T(ch, QC.Npc.ChateauDoraguille.Curilla, F) + "#" + T(ch, QC.Npc.ChateauDoraguille.Milchupain, F) +
                  "#" + T(ch, QC.Npc.ChateauDoraguille.Rahal, F) + "#" + T(ch, QC.Npc.ChateauDoraguille.Halver, F));
            int k1 = QC.KeyItem.PieceOfABrokenKey1, k2 = QC.KeyItem.PieceOfABrokenKey2,
                k3 = QC.KeyItem.PieceOfABrokenKey3;
            string cannot = $"MessageSpecial({QC.Text.TempleOfUggalepih.YOU_CANNOT_OPEN_THIS_DOOR})";
            string quiver = $"MessageSpecial({QC.Text.TempleOfUggalepih.BEGINS_TO_QUIVER},{QC.KeyItem.CrystalDowser})";
            var door = N6Npc(d, 1, N6Trig, tu, QC.Npc.TempleOfUggalepih._4fv);
            Fake K(int s) => N6Keys(F(s), k1, k2, k3);
            Check("c3 N6 lightbringer: _4fv with the three pieces, no Nio up: at 5 BEGINS_TO_QUIVER (the dowser), " +
                  "with Prog 1 65 (progress); else YOU_CANNOT_OPEN_THIS_DOOR (message special)",
                  N6Line(Enumerable.Range(0, 12).Select(s => (s, s == 5 ? quiver : cannot)).ToArray()) + "#" +
                  N6Line(Enumerable.Range(0, 12).Select(s => (s, s == 5 ? "ProgressEvent(65)" : cannot)).ToArray()),
                  N6Over(door, K) + "#" + N6Over(door, s => N6Var(K(s), S, id, "Prog", 1)));
            string D5(Fake f) => N6Reply(door, f);
            Check("c3 N6 lightbringer: _4fv at 5: a piece missing (1, 2, 3): cannot; Nio-A or Nio-Hum alive: cannot; " +
                  "dead: as none; either nil: cannot",
                  string.Join("|", Enumerable.Repeat(cannot, 5)) + $"|{quiver}|{quiver}|{cannot}|{cannot}",
                  $"{D5(N6Keys(F(5), k2, k3))}|{D5(N6Keys(F(5), k1, k3))}|{D5(N6Keys(F(5), k1, k2))}|" +
                  $"{D5(N6Mob(K(5), QC.Mob.TempleOfUggalepih.NIO_A, "alive"))}|" +
                  $"{D5(N6Mob(K(5), QC.Mob.TempleOfUggalepih.NIO_HUM, "alive"))}|" +
                  $"{D5(N6Mob(K(5), QC.Mob.TempleOfUggalepih.NIO_A, "dead"))}|" +
                  $"{D5(N6Mob(K(5), QC.Mob.TempleOfUggalepih.NIO_HUM, "dead"))}|" +
                  $"{D5(N6Mob(K(5), QC.Mob.TempleOfUggalepih.NIO_A, "missing"))}|" +
                  $"{D5(N6Mob(K(5), QC.Mob.TempleOfUggalepih.NIO_HUM, "missing"))}");
            Check("c3 N6 lightbringer: _4fv's spawn: Nio-A and Nio-Hum with no claim",
                  $"Player.SpawnMob({QC.Mob.TempleOfUggalepih.NIO_A})," +
                  $"Player.SpawnMob({QC.Mob.TempleOfUggalepih.NIO_HUM})",
                  N6Run(door, K(5)));
            foreach (var (npc, other) in new[] { (QC.Npc.TempleOfUggalepih.NioA, QC.Mob.TempleOfUggalepih.NIO_HUM),
                                                 (QC.Npc.TempleOfUggalepih.NioHum, QC.Mob.TempleOfUggalepih.NIO_A) })
            {
                var h = N6Npc(d, 1, N6Death, tu, npc);
                Check($"c3 N6 lightbringer: {npc}'s death at 5: {other} dead or not spawned: Prog 1; alive or nil: " +
                      "nothing; at 4 and 6: nothing",
                      "Mission.SetVar(Prog,1)|Mission.SetVar(Prog,1)||||",
                      $"{N6Run(h, N6Mob(F(5), other, "dead"))}|{N6Run(h, F(5))}|" +
                      $"{N6Run(h, N6Mob(F(5), other, "alive"))}|{N6Run(h, N6Mob(F(5), other, "missing"))}|" +
                      $"{N6Run(h, F(4))}|{N6Run(h, F(6))}");
            }
            foreach (var (npc, k) in new[] { (QC.Npc.TempleOfUggalepih.qmkey1, k1),
                                             (QC.Npc.TempleOfUggalepih.qmkey2, k2),
                                             (QC.Npc.TempleOfUggalepih.qmkey3, k3) })
            {
                var h = N6Npc(d, 1, N6Trig, tu, npc);
                Check($"c3 N6 lightbringer: {npc} without its piece at 2 and above: the piece (keyItem), the status " +
                      "raised by one; at 0 and 1, or with the piece: nothing",
                      N6Line(Enumerable.Range(2, 10).Select(s => (s, $"KeyItem({k})")).ToArray()) + "#" + N6Line() +
                      "#Player.SetMissionStatus(0,4)|",
                      N6Over(h, F) + "#" + N6Over(h, s => N6Keys(F(s), k)) + $"#{N6Run(h, F(3))}|{N6Run(h, F(1))}");
            }
            Check("c3 N6 lightbringer: qm17: SLIGHTLY_QUIVERS (the dowser) at every status",
                  N6Line(Enumerable.Range(0, 12).Select(s =>
                      (s, $"MessageSpecial({QC.Text.TempleOfUggalepih.SLIGHTLY_QUIVERS},{QC.KeyItem.CrystalDowser})"))
                      .ToArray()),
                  T(tu, QC.Npc.TempleOfUggalepih.qm17, F));
            var c104 = N6Csid(d, 1, N6Fin, ch, 104);
            var no = F(6);
            no.CompleteResult = false;
            Check("c3 N6 lightbringer: finishes: 100 status 1; 106 status 2 and the dowser; 104 completes then takes " +
                  "the dowser (complete false: nothing more); 65 status 6 and the three pieces taken",
                  "Player.SetMissionStatus(0,1)|" +
                  $"Player.SetMissionStatus(0,2),Player.GiveKeyItem({QC.KeyItem.CrystalDowser})|" +
                  $"Mission.Complete(),Player.DelKeyItem({QC.KeyItem.CrystalDowser})|Mission.Complete()|" +
                  $"Player.SetMissionStatus(0,6),Player.DelKeyItem({k1}),Player.DelKeyItem({k2})," +
                  $"Player.DelKeyItem({k3})",
                  $"{N6Run(N6Csid(d, 1, N6Fin, ch, 100), F(0))}|{N6Run(N6Csid(d, 1, N6Fin, ch, 106), F(1))}|" +
                  $"{N6Run(c104, F(6))}|{N6Run(c104, no)}|{N6Run(N6Csid(d, 1, N6Fin, tu, 65), K(5))}");
            Fake C(int rank = 9, int current = -1, int option = 0, int points = 7)
            {
                var f = N6Var(N6Fake(S, id, current < 0 ? QC.Mission.Sandoria.None : current, rank: rank, done: id), S,
                              id, "Option", option);
                f.RankPointsHeld = points;
                return f;
            }
            var s3 = d.States[2];
            Check("c3 N6 lightbringer: section 3 holds when completed, rank 9, 9-2 not current (none, 9-1 current); " +
                  "fails at rank 8 and 10, with 9-2 current, not completed",
                  "True True False False False False",
                  $"{s3.IsMissionMatch(C())} {s3.IsMissionMatch(C(current: QC.Mission.Sandoria.BreakingBarriers))} " +
                  $"{s3.IsMissionMatch(C(8))} {s3.IsMissionMatch(C(10))} " +
                  $"{s3.IsMissionMatch(C(current: QC.Mission.Sandoria.TheHeirToTheLight))} " +
                  $"{s3.IsMissionMatch(N6Fake(S, id, QC.Mission.Sandoria.None, rank: 9))}");
            var h0 = N6Npc(d, 2, N6Trig, ch, QC.Npc.ChateauDoraguille._6h0);
            var h1 = N6Npc(d, 2, N6Trig, ch, QC.Npc.ChateauDoraguille._6h1);
            Check("c3 N6 lightbringer: section 3: _6h0 63 until Option bit 0, _6h1 74 until bit 1 (progress), each " +
                  "reading its own bit; finishes set the bits",
                  "ProgressEvent(63)|null|ProgressEvent(63)|ProgressEvent(74)|ProgressEvent(74)|null|" +
                  "Mission.SetVarBit(Option,0)|Mission.SetVarBit(Option,1)",
                  $"{N6Reply(h0, C())}|{N6Reply(h0, C(option: 1))}|{N6Reply(h0, C(option: 2))}|" +
                  $"{N6Reply(h1, C())}|{N6Reply(h1, C(option: 1))}|{N6Reply(h1, C(option: 2))}|" +
                  $"{N6Run(N6Csid(d, 2, N6Fin, ch, 63), C())}|{N6Run(N6Csid(d, 2, N6Fin, ch, 74), C())}");
            var s4 = d.States[3];
            Check("c3 N6 lightbringer: section 4 holds when completed, rank 9, no rank points; fails with rank " +
                  "points 1, rank 8 or 10, not completed",
                  "True False False False False",
                  $"{s4.IsMissionMatch(C(points: 0))} {s4.IsMissionMatch(C(points: 1))} " +
                  $"{s4.IsMissionMatch(C(8, points: 0))} {s4.IsMissionMatch(C(10, points: 0))} " +
                  $"{s4.IsMissionMatch(N6Fake(S, id, QC.Mission.Sandoria.None, rank: 9))}");
            var cf = C(points: 0);
            cf.TextIds[QC.Text.ChateauDoraguille.LIGHTBRINGER_EXTRA.ToString()] = 6860;
            Check("c3 N6 lightbringer: section 4: Aramaviont LIGHTBRINGER_EXTRA + 1, Milchupain + 3 (message text); " +
                  "Rahal 42 (event, replaceDefault)",
                  "MessageText(6861)|MessageText(6863)|Event(42):replaceDefault()",
                  $"{N6Reply(N6Npc(d, 3, N6Trig, ch, QC.Npc.ChateauDoraguille.Aramaviont), cf)}|" +
                  $"{N6Reply(N6Npc(d, 3, N6Trig, ch, QC.Npc.ChateauDoraguille.Milchupain), cf)}|" +
                  $"{N6Reply(N6Npc(d, 3, N6Trig, ch, QC.Npc.ChateauDoraguille.Rahal), cf)}");
        }

        // 9-1 Breaking Barriers
        {
            int id = QC.Mission.Sandoria.BreakingBarriers;
            var d = new QC.Missions.Sandoria.BreakingBarriers().Compile();
            ushort vs = QC.Zone.ValleyOfSorrows, xa = QC.Zone.Xarcabard, bd = QC.Zone.BatalliaDowns;
            Fake F(int s) => N6Fake(S, id, id, status: s);
            int lev = QC.KeyItem.FigureOfLeviathan, gar = QC.KeyItem.FigureOfGaruda, tit = QC.KeyItem.FigureOfTitan;
            var gh = N6Npc(d, 1, N6Trig, ch, QC.Npc.ChateauDoraguille._6h4);
            Check("c3 N6 breaking barriers: _6h4: 32 at 0; 76 at 4 with the three figures (progress); a figure " +
                  "missing (Leviathan, Garuda, Titan): only 32",
                  N6Line((0, "ProgressEvent(32)"), (4, "ProgressEvent(76)")) + "#" + N6Line((0, "ProgressEvent(32)")) +
                  "#" + N6Line((0, "ProgressEvent(32)")) + "#" + N6Line((0, "ProgressEvent(32)")),
                  N6Over(gh, s => N6Keys(F(s), lev, gar, tit)) + "#" + N6Over(gh, s => N6Keys(F(s), gar, tit)) + "#" +
                  N6Over(gh, s => N6Keys(F(s), lev, tit)) + "#" + N6Over(gh, s => N6Keys(F(s), lev, gar)));
            Check("c3 N6 breaking barriers: Halver: 26 at 0, 1 at 1 (progress)",
                  N6Line((0, "ProgressEvent(26)"), (1, "ProgressEvent(1)")),
                  N6Over(N6Npc(d, 1, N6Trig, ch, QC.Npc.ChateauDoraguille.Halver), F));
            string burried = $"MessageSpecial({QC.Text.ValleyOfSorrows.SOMETHING_BURRIED})";
            string buried = $"MessageSpecial({QC.Text.Xarcabard.SOMETHING_BURIED})";
            Check("c3 N6 breaking barriers: the Valley qm: 0 at 1 (progress), else SOMETHING_BURRIED; Xarcabard's " +
                  "qm5: 12 at 2 (progress), else SOMETHING_BURIED (message special)",
                  N6Line(Enumerable.Range(0, 12).Select(s => (s, s == 1 ? "ProgressEvent(0)" : burried)).ToArray()) +
                  "#" +
                  N6Line(Enumerable.Range(0, 12).Select(s => (s, s == 2 ? "ProgressEvent(12)" : buried)).ToArray()),
                  N6Over(N6Npc(d, 1, N6Trig, vs, QC.Npc.ValleyOfSorrows.qmbreakingbarriers), F) + "#" +
                  N6Over(N6Npc(d, 1, N6Trig, xa, QC.Npc.Xarcabard.qm5), F));
            string lurk = $"MessageSpecial({QC.Text.BatalliaDowns.SENSE_SOMETHING_LURKING})";
            string someone = $"MessageSpecial({QC.Text.BatalliaDowns.SOMEONE_HAS_BURIED})";
            var q3 = N6Npc(d, 1, N6Trig, bd, QC.Npc.BatalliaDowns.qm3);
            Check("c3 N6 breaking barriers: qm3 with neither bird up: at 3 SENSE_SOMETHING_LURKING, with Prog 1 904 " +
                  "(progress); else SOMEONE_HAS_BURIED",
                  N6Line(Enumerable.Range(0, 12).Select(s => (s, s == 3 ? lurk : someone)).ToArray()) + "#" +
                  N6Line(Enumerable.Range(0, 12).Select(s => (s, s == 3 ? "ProgressEvent(904)" : someone)).ToArray()),
                  N6Over(q3, F) + "#" + N6Over(q3, s => N6Var(F(s), S, id, "Prog", 1)));
            string Q3(string mob, string st) => N6Reply(q3, N6Mob(F(3), mob, st));
            Check("c3 N6 breaking barriers: qm3 at 3: Suparna or its Fledgling alive or nil: SOMEONE_HAS_BURIED; " +
                  "dead: as none",
                  $"{someone}|{someone}|{lurk}|{lurk}|{someone}|{someone}",
                  $"{Q3(QC.Mob.BatalliaDowns.SUPARNA, "alive")}|" +
                  $"{Q3(QC.Mob.BatalliaDowns.SUPARNA_FLEDGLING, "alive")}|" +
                  $"{Q3(QC.Mob.BatalliaDowns.SUPARNA, "dead")}|{Q3(QC.Mob.BatalliaDowns.SUPARNA_FLEDGLING, "dead")}|" +
                  $"{Q3(QC.Mob.BatalliaDowns.SUPARNA, "missing")}|" +
                  $"{Q3(QC.Mob.BatalliaDowns.SUPARNA_FLEDGLING, "missing")}");
            Check("c3 N6 breaking barriers: qm3's spawn: Suparna and the Fledgling with no claim",
                  $"Player.SpawnMob({QC.Mob.BatalliaDowns.SUPARNA})," +
                  $"Player.SpawnMob({QC.Mob.BatalliaDowns.SUPARNA_FLEDGLING})",
                  N6Run(q3, F(3)));
            foreach (var (npc, other) in new[]
                     {
                         (QC.Npc.BatalliaDowns.Suparna, QC.Mob.BatalliaDowns.SUPARNA_FLEDGLING),
                         (QC.Npc.BatalliaDowns.SuparnaFledgling, QC.Mob.BatalliaDowns.SUPARNA),
                     })
            {
                var h = N6Npc(d, 1, N6Death, bd, npc);
                Check($"c3 N6 breaking barriers: {npc}'s death at 3: {other} dead or not spawned: Prog 1; alive or " +
                      "nil: nothing; at 2 and 4: nothing",
                      "Mission.SetVar(Prog,1)|Mission.SetVar(Prog,1)||||",
                      $"{N6Run(h, N6Mob(F(3), other, "dead"))}|{N6Run(h, F(3))}|" +
                      $"{N6Run(h, N6Mob(F(3), other, "alive"))}|{N6Run(h, N6Mob(F(3), other, "missing"))}|" +
                      $"{N6Run(h, F(2))}|{N6Run(h, F(4))}");
            }
            var c76 = N6Csid(d, 1, N6Fin, ch, 76);
            var no = F(4);
            no.CompleteResult = false;
            Check("c3 N6 breaking barriers: finishes: 32 status 1; 76 completes then takes the three figures " +
                  "(complete false: nothing more); 0 Titan then status 2; 12 Garuda then 3; 904 Leviathan then 4",
                  $"Player.SetMissionStatus(0,1)|Mission.Complete(),Player.DelKeyItem({lev})," +
                  $"Player.DelKeyItem({gar}),Player.DelKeyItem({tit})|Mission.Complete()|" +
                  $"Player.GiveKeyItem({tit}),Player.SetMissionStatus(0,2)|" +
                  $"Player.GiveKeyItem({gar}),Player.SetMissionStatus(0,3)|" +
                  $"Player.GiveKeyItem({lev}),Player.SetMissionStatus(0,4)",
                  $"{N6Run(N6Csid(d, 1, N6Fin, ch, 32), F(0))}|{N6Run(c76, F(4))}|{N6Run(c76, no)}|" +
                  $"{N6Run(N6Csid(d, 1, N6Fin, vs, 0), F(1))}|{N6Run(N6Csid(d, 1, N6Fin, xa, 12), F(2))}|" +
                  $"{N6Run(N6Csid(d, 1, N6Fin, bd, 904), F(3))}");
            // section 3: its keys have no zone, so it has no handlers; its check as the Lua has it
            Fake D(int current, bool done) => N6Fake(S, id, current, done: done ? new[] { id } : new int[0]);
            var s3 = d.States[2];
            Check("c3 N6 breaking barriers: section 3 holds when completed with 9-2 not current (none, 8-2 " +
                  "current); fails with 9-2 current and when not completed; it has no handlers",
                  "True True False False 0",
                  $"{s3.IsMissionMatch(D(QC.Mission.Sandoria.None, true))} " +
                  $"{s3.IsMissionMatch(D(QC.Mission.Sandoria.Lightbringer, true))} " +
                  $"{s3.IsMissionMatch(D(QC.Mission.Sandoria.TheHeirToTheLight, true))} " +
                  $"{s3.IsMissionMatch(D(QC.Mission.Sandoria.None, false))} {s3.Handlers.Count}");
        }

        // 9-2 The Heir to the Light
        {
            int id = QC.Mission.Sandoria.TheHeirToTheLight;
            var d = new QC.Missions.Sandoria.TheHeirToTheLight().Compile();
            Fake F(int s) => N6Fake(S, id, id, status: s);
            string T(ushort zone, string npc) => N6Over(N6Npc(d, 1, N6Trig, zone, npc), F);
            (int, string)[] Pe(int from, int to, int csid) =>
                Enumerable.Range(from, to - from + 1).Select(s => (s, $"ProgressEvent({csid})")).ToArray();
            Check("c3 N6 heir: the Chateau (progress): _6h0 3 and _6h1 5 above 5; _6h4 8 at 5",
                  N6Line(Pe(6, 11, 3)) + "#" + N6Line(Pe(6, 11, 5)) + "#" + N6Line((5, "ProgressEvent(8)")),
                  T(ch, QC.Npc.ChateauDoraguille._6h0) + "#" + T(ch, QC.Npc.ChateauDoraguille._6h1) + "#" +
                  T(ch, QC.Npc.ChateauDoraguille._6h4));
            Check("c3 N6 heir: Aramaviont 14 above 4, else 13 above 1; Curilla 19 above 3, else 18 above 1; " +
                  "Milchupain 35 / 34 and Rahal 40 / 39 as Aramaviont (progress)",
                  N6Line(Pe(2, 4, 13).Concat(Pe(5, 11, 14)).ToArray()) + "#" +
                  N6Line(Pe(2, 3, 18).Concat(Pe(4, 11, 19)).ToArray()) + "#" +
                  N6Line(Pe(2, 4, 34).Concat(Pe(5, 11, 35)).ToArray()) + "#" +
                  N6Line(Pe(2, 4, 39).Concat(Pe(5, 11, 40)).ToArray()),
                  T(ch, QC.Npc.ChateauDoraguille.Aramaviont) + "#" + T(ch, QC.Npc.ChateauDoraguille.Curilla) + "#" +
                  T(ch, QC.Npc.ChateauDoraguille.Milchupain) + "#" + T(ch, QC.Npc.ChateauDoraguille.Rahal));
            Check("c3 N6 heir: Halver 9 at 7, else 30 above 5, 28 above 4, 29 above 1; Nachou 6 at 5, 4 at 2; " +
                  "Perfaumand 7 at 5, 2 at 2 (progress)",
                  N6Line(Pe(2, 4, 29).Concat(Pe(5, 5, 28)).Concat(Pe(6, 6, 30)).Concat(Pe(7, 7, 9))
                      .Concat(Pe(8, 11, 30)).ToArray()) + "#" +
                  N6Line((2, "ProgressEvent(4)"), (5, "ProgressEvent(6)")) + "#" +
                  N6Line((2, "ProgressEvent(2)"), (5, "ProgressEvent(7)")),
                  T(ch, QC.Npc.ChateauDoraguille.Halver) + "#" + T(ch, QC.Npc.ChateauDoraguille.Nachou) + "#" +
                  T(ch, QC.Npc.ChateauDoraguille.Perfaumand));
            Check("c3 N6 heir: zone-ins (events): the Chateau 10 at 1; Northern San d'Oria 1 at 0 and 0 at 4; " +
                  "Fei'Yin 23 at 2",
                  N6Line((1, "Event(10)")) + "#" + N6Line((0, "Event(1)"), (4, "Event(0)")) + "#" +
                  N6Line((2, "Event(23)")),
                  N6Over(N6Csid(d, 1, N6Zone, ch), F) + "#" + N6Over(N6Csid(d, 1, N6Zone, ns), F) + "#" +
                  N6Over(N6Csid(d, 1, N6Zone, QC.Zone.Feiyin), F));
            var fc = N6Npc(d, 1, N6Trig, ns, QC.Npc.NorthernSanDoria._6fc);
            Check("c3 N6 heir: Arnau 4 at 0 (event); _6fc 50 above 5 with Papal 0 (progress), with Papal 1 nothing; " +
                  "the Tomb's _5a0 14 at 6 (progress)",
                  N6Line((0, "Event(4)")) + "#" + N6Line(Pe(6, 11, 50)) + "#" + N6Line() + "#" +
                  N6Line((6, "ProgressEvent(14)")),
                  T(ns, QC.Npc.NorthernSanDoria.Arnau) + "#" + N6Over(fc, F) + "#" +
                  N6Over(fc, s => N6Var(F(s), S, id, "Papal", 1)) + "#" +
                  T(QC.Zone.KingRanperresTomb, QC.Npc.KingRanperresTomb._5a0));
            var won = N6Csid(d, 1, N6Fin, QC.Zone.QubiaArena, 32001);
            string W(int s, int win) => N6Run(won, N4With(F(s), win: win));
            Check("c3 N6 heir: Qu'Bia Arena's 32001 finish: status 3 with battlefieldWin 516: status 4; 515, 0, at 2 " +
                  "and 4: nothing; its 32004 finish: Intro 1",
                  "Player.SetMissionStatus(0,4)|||||Mission.SetVar(Intro,1)",
                  $"{W(3, 516)}|{W(3, 515)}|{W(3, 0)}|{W(2, 516)}|{W(4, 516)}|" +
                  $"{N6Run(N6Csid(d, 1, N6Fin, QC.Zone.QubiaArena, 32004), F(3))}");
            var c9 = N6Csid(d, 1, N6Fin, ch, 9);
            var full = F(7);
            full.GiveResult = false;
            Check("c3 N6 heir: 9's finish: completes, Option 1, Mission[0][21]Option 0, the San d'Orian Flag; a full " +
                  "bag: Flag 1",
                  "Mission.Complete(),Mission.SetVar(Option,1),Player.SetCharVar(Mission[0][21]Option,0,0)," +
                  $"Player.GiveItem({QC.Item.SanDorianFlag})|Mission.Complete(),Mission.SetVar(Option,1)," +
                  $"Player.SetCharVar(Mission[0][21]Option,0,0),Player.GiveItem({QC.Item.SanDorianFlag})," +
                  "Mission.SetVar(Flag,1)",
                  $"{N6Run(c9, F(7))}|{N6Run(c9, full)}");
            Check("c3 N6 heir: finishes: 8 status 6; 10 status 2; Northern 0 status 5, 1 status 1, 50 Papal 1; 23 " +
                  "status 3; 14 status 7",
                  "Player.SetMissionStatus(0,6)|Player.SetMissionStatus(0,2)|Player.SetMissionStatus(0,5)|" +
                  "Player.SetMissionStatus(0,1)|Mission.SetVar(Papal,1)|Player.SetMissionStatus(0,3)|" +
                  "Player.SetMissionStatus(0,7)",
                  $"{N6Run(N6Csid(d, 1, N6Fin, ch, 8), F(5))}|{N6Run(N6Csid(d, 1, N6Fin, ch, 10), F(1))}|" +
                  $"{N6Run(N6Csid(d, 1, N6Fin, ns, 0), F(4))}|{N6Run(N6Csid(d, 1, N6Fin, ns, 1), F(0))}|" +
                  $"{N6Run(N6Csid(d, 1, N6Fin, ns, 50), F(6))}|" +
                  $"{N6Run(N6Csid(d, 1, N6Fin, QC.Zone.Feiyin, 23), F(2))}|" +
                  $"{N6Run(N6Csid(d, 1, N6Fin, QC.Zone.KingRanperresTomb, 14), F(6))}");
            Fake C(int option, int papal = 0, int flag = 0, bool give = true)
            {
                var f = N6Var(N6Var(N6Var(N6Fake(S, id, QC.Mission.Sandoria.None, done: id), S, id, "Option", option),
                                    S, id, "Papal", papal), S, id, "Flag", flag);
                f.GiveResult = give;
                return f;
            }
            string R(ushort zone, string npc, Fake f) => N6Reply(N6Npc(d, 2, N6Trig, zone, npc), f);
            Check("c3 N6 heir: section 3: Aramaviont 11, Curilla 20, Rahal 41 at Option 1 (progress); at 0 and 2 " +
                  "nothing",
                  "ProgressEvent(11)|ProgressEvent(20)|ProgressEvent(41)|null|null|null|null|null|null",
                  string.Join("|", new[] { 1, 0, 2 }.SelectMany(o => new[]
                  {
                      R(ch, QC.Npc.ChateauDoraguille.Aramaviont, C(o)), R(ch, QC.Npc.ChateauDoraguille.Curilla, C(o)),
                      R(ch, QC.Npc.ChateauDoraguille.Rahal, C(o)),
                  })));
            Check("c3 N6 heir: section 3: _6fc 50 at Option 1 with Papal 0 (progress); Papal 1, Option 0 or 2: " +
                  "nothing; its finish: Papal 1",
                  "ProgressEvent(50)|null|null|null|Mission.SetVar(Papal,1)",
                  $"{R(ns, QC.Npc.NorthernSanDoria._6fc, C(1))}|{R(ns, QC.Npc.NorthernSanDoria._6fc, C(1, 1))}|" +
                  $"{R(ns, QC.Npc.NorthernSanDoria._6fc, C(0))}|{R(ns, QC.Npc.NorthernSanDoria._6fc, C(2))}|" +
                  $"{N6Run(N6Csid(d, 2, N6Fin, ns, 50), C(1))}");
            var hv = N6Npc(d, 2, N6Trig, ch, QC.Npc.ChateauDoraguille.Halver);
            Check("c3 N6 heir: section 3 Halver: Flag 1: the flag and Flag 0; a full bag: refused, Flag kept; Flag " +
                  "0: nothing; no reply",
                  $"null:Player.GiveItem({QC.Item.SanDorianFlag}),Mission.SetVar(Flag,0)|" +
                  $"null:Player.GiveItem({QC.Item.SanDorianFlag})|null:",
                  $"{N6Reply(hv, C(0, flag: 1))}:{N6Run(hv, C(0, flag: 1))}|" +
                  $"{N6Reply(hv, C(0, flag: 1, give: false))}:{N6Run(hv, C(0, flag: 1, give: false))}|" +
                  $"{N6Reply(hv, C(0))}:{N6Run(hv, C(0))}");
            var szi = N6Csid(d, 2, N6Zone, ss);
            Check("c3 N6 heir: section 3: Southern San d'Oria's zone-in 0 at Option 1 (event), at 0 and 2 nothing; " +
                  "its finish: Option 0 and Papal 0",
                  "Event(0)|null|null|Mission.SetVar(Option,0),Mission.SetVar(Papal,0)",
                  $"{N6Reply(szi, C(1))}|{N6Reply(szi, C(0))}|{N6Reply(szi, C(2))}|" +
                  $"{N6Run(N6Csid(d, 2, N6Fin, ss, 0), C(1))}");
        }
    }

    static void RunC3N6RepliesWindurst()
    {
        const MissionLog W = MissionLog.Windurst;
        ushort pw = QC.Zone.PortWindurst, wl = QC.Zone.WindurstWalls, wt = QC.Zone.WindurstWaters;
        ushort wd = QC.Zone.WindurstWoods, ht = QC.Zone.HeavensTower, dv = QC.Zone.Davoi;

        // 8-1 Vain
        {
            int id = QC.Mission.Windurst.Vain;
            var d = new QC.Missions.Windurst.Vain().Compile();
            Fake F(int s) => N6Fake(W, id, id, status: s);
            string R(ushort zone, string npc, Fake f) => N6Reply(N6Npc(d, 1, N6Trig, zone, npc), f);
            Check("c3 N6 vain: the fixed events: Port Windurst 479-482, Windurst Walls 371-374, Waters 749-751 and " +
                  "757, 748, Woods 579-582 (Sola Jaab 0, 2)",
                  "Event(479)|Event(480)|Event(482)|Event(481)|Event(374)|Event(373)|Event(372)|Event(371)|" +
                  "Event(749)|" +
                  "Event(757)|Event(748)|Event(750)|Event(751)|Event(580)|Event(579)|Event(581,0,2)|Event(582)",
                  string.Join("|", new (ushort, string)[]
                  {
                      (pw, QC.Npc.PortWindurst.JanshuraRashura), (pw, QC.Npc.PortWindurst.NineofClubs),
                      (pw, QC.Npc.PortWindurst.PuoRhen), (pw, QC.Npc.PortWindurst.TenofClubs),
                      (wl, QC.Npc.WindurstWalls.ChawoShipeynyo), (wl, QC.Npc.WindurstWalls.KeoKoruo),
                      (wl, QC.Npc.WindurstWalls.PakkePokke), (wl, QC.Npc.WindurstWalls.ZokimaRokima),
                      (wt, QC.Npc.WindurstWaters.DagozaBeruza), (wt, QC.Npc.WindurstWaters.Mimomo),
                      (wt, QC.Npc.WindurstWaters.Mokyokyo), (wt, QC.Npc.WindurstWaters.PannaDonna),
                      (wt, QC.Npc.WindurstWaters.TenofHearts), (wd, QC.Npc.WindurstWoods.MiiriWohri),
                      (wd, QC.Npc.WindurstWoods.RakohBuuma), (wd, QC.Npc.WindurstWoods.SolaJaab),
                      (wd, QC.Npc.WindurstWoods.TihPikeh),
                  }.Select(n => R(n.Item1, n.Item2, F(0)))));
            (int, string)[] Ev(int from, int csid) =>
                Enumerable.Range(from, 12 - from).Select(s => (s, $"Event({csid})")).ToArray();
            Check("c3 N6 vain: Fuepepe 755, Pechiru-Mashiru 754, Tauwawa 756 at status 1 and above (events)",
                  N6Line(Ev(1, 755)) + "#" + N6Line(Ev(1, 754)) + "#" + N6Line(Ev(1, 756)),
                  N6Over(N6Npc(d, 1, N6Trig, wt, QC.Npc.WindurstWaters.Fuepepe), F) + "#" +
                  N6Over(N6Npc(d, 1, N6Trig, wt, QC.Npc.WindurstWaters.PechiruMashiru), F) + "#" +
                  N6Over(N6Npc(d, 1, N6Trig, wt, QC.Npc.WindurstWaters.Tauwawa), F));
            Check("c3 N6 vain: Moreno-Toeno: 752 (0, the Aurastery Ring) at 0 (progress), 753 at 1 to 3 (event), 758 " +
                  "at 4 (progress)",
                  N6Line((0, $"ProgressEvent(752,0,{QC.KeyItem.AurasteryRing})"), (1, "Event(753)"), (2, "Event(753)"),
                         (3, "Event(753)"), (4, "ProgressEvent(758)")),
                  N6Over(N6Npc(d, 1, N6Trig, wt, QC.Npc.WindurstWaters.MorenoToeno), F));
            int seeker = QC.KeyItem.StarSeeker, drained = QC.KeyItem.MagicDrainedStarSeeker;
            var sg = N6Npc(d, 1, N6Trig, dv, QC.Npc.Davoi.SedalGodjal);
            string c118 = $"ProgressCutscene(118,0,{QC.Item.CurseWand},{seeker})";
            string e119 = $"Event(119,0,{QC.Item.CurseWand},{seeker})";
            Check("c3 N6 vain: Sedal-Godjal at 2 and above: the Star Seeker: 118 (progress cutscene); the drained " +
                  "one at 4: 121 (event), at other statuses 119 (event); neither: 119; below 2 nothing",
                  N6Line(Enumerable.Range(2, 10).Select(s => (s, c118)).ToArray()) + "#" +
                  N6Line(Enumerable.Range(2, 10).Select(s => (s, s == 4 ? "Event(121)" : e119)).ToArray()) + "#" +
                  N6Line(Enumerable.Range(2, 10).Select(s => (s, e119)).ToArray()) + "#" +
                  N6Line(Enumerable.Range(2, 10).Select(s => (s, c118)).ToArray()),
                  N6Over(sg, s => N6Keys(F(s), seeker)) + "#" + N6Over(sg, s => N6Keys(F(s), drained)) + "#" +
                  N6Over(sg, F) + "#" + N6Over(sg, s => N6Keys(F(s), seeker, drained)));
            var tr = N6Npc(d, 1, N6Trade, dv, QC.Npc.Davoi.SedalGodjal);
            Fake Tr(int s, int item, bool hasDrained)
            {
                var f = F(s);
                if (hasDrained) f.KeyItems[drained] = true;
                f.TradeResult = item == QC.Item.CurseWand;
                return f;
            }
            Check("c3 N6 vain: Sedal-Godjal's trade: the Curse Wand at 3 with the drained seeker: 120 (progress " +
                  "cutscene); at 2 or 4, without the drained seeker, another item: nothing",
                  "ProgressCutscene(120)|null|null|null|null",
                  $"{N6Reply(tr, Tr(3, QC.Item.CurseWand, true))}|{N6Reply(tr, Tr(2, QC.Item.CurseWand, true))}|" +
                  $"{N6Reply(tr, Tr(4, QC.Item.CurseWand, true))}|{N6Reply(tr, Tr(3, QC.Item.CurseWand, false))}|" +
                  $"{N6Reply(tr, Tr(3, QC.Item.ClumpOfGoobbueHumus, true))}");
            Check("c3 N6 vain: Qu'Hau Spring 2 at 1 (progress cutscene); Ro'Maeve's zone-in 3 at 1 (event)",
                  N6Line((1, "ProgressCutscene(2)")) + "#" + N6Line((1, "Event(3)")),
                  N6Over(N6Npc(d, 1, N6Trig, QC.Zone.Romaeve, QC.Npc.Romaeve.QuHauSpring), F) + "#" +
                  N6Over(N6Csid(d, 1, N6Zone, QC.Zone.Romaeve), F));
            // the zone-ins at status 1 and their sky updates
            var zones = new (ushort Zone, int Csid)[]
            {
                (QC.Zone.BatalliaDowns, 903), (QC.Zone.BeaucedineGlacier, 116), (QC.Zone.BuburimuPeninsula, 5),
                (QC.Zone.EastRonfaure, 23), (QC.Zone.EastSarutabaruta, 52), (QC.Zone.KonschtatHighlands, 106),
                (QC.Zone.LaTheinePlateau, 125), (QC.Zone.MeriphataudMountains, 34), (QC.Zone.NorthGustaberg, 246),
                (QC.Zone.PashhowMarshlands, 15), (QC.Zone.RolanberryFields, 4), (QC.Zone.SauromugueChampaign, 5),
                (QC.Zone.SouthGustaberg, 37), (QC.Zone.TahrongiCanyon, 37), (QC.Zone.TheSanctuaryOfZitah, 4),
                (QC.Zone.ValkurmDunes, 5), (QC.Zone.WestRonfaure, 53), (QC.Zone.WestSarutabaruta, 50),
                (QC.Zone.Xarcabard, 11),
            };
            foreach (var (zone, csid) in zones)
                Check($"c3 N6 vain: zone {zone}'s zone-in: {csid} at status 1 (event)",
                      N6Line((1, $"Event({csid})")), N6Over(N6Csid(d, 1, N6Zone, zone), F));
            string U(ushort zone, int csid, double x = 0, double y = 0, double z = 0, int prev = 0)
            {
                var f = F(1);
                f.XPos = x;
                f.YPos = y;
                f.ZPos = z;
                f.PrevZone = prev;
                return N6Run(N6Csid(d, 1, N6Upd, zone, csid), f);
            }
            string Sky(int n) => $"Player.UpdateEvent(0,0,0,0,0,{n})";
            Check("c3 N6 vain: Batallia 903: z above -331 sky 3 (z -330), else 2 (-331, -400)",
                  $"{Sky(3)}|{Sky(2)}|{Sky(2)}",
                  $"{U(QC.Zone.BatalliaDowns, 903, z: -330)}|{U(QC.Zone.BatalliaDowns, 903, z: -331)}|" +
                  $"{U(QC.Zone.BatalliaDowns, 903, z: -400)}");
            Check("c3 N6 vain: fixed skies: Beaucedine 116 sky 4, La Theine 125 sky 2, West Ronfaure 53 sky 5",
                  $"{Sky(4)}|{Sky(2)}|{Sky(5)}",
                  $"{U(QC.Zone.BeaucedineGlacier, 116)}|{U(QC.Zone.LaTheinePlateau, 125)}|" +
                  $"{U(QC.Zone.WestRonfaure, 53)}");
            Check("c3 N6 vain: Buburimu 5: from the Labyrinth of Onzozo or Mhaura sky 7, from the Maze of Shakhrami " +
                  "6, else nothing",
                  $"{Sky(7)}|{Sky(7)}|{Sky(6)}|",
                  $"{U(QC.Zone.BuburimuPeninsula, 5, prev: QC.Zone.LabyrinthOfOnzozo)}|" +
                  $"{U(QC.Zone.BuburimuPeninsula, 5, prev: QC.Zone.Mhaura)}|" +
                  $"{U(QC.Zone.BuburimuPeninsula, 5, prev: QC.Zone.MazeOfShakhrami)}|" +
                  $"{U(QC.Zone.BuburimuPeninsula, 5, prev: QC.Zone.TahrongiCanyon)}");
            Check("c3 N6 vain: East Ronfaure 23: y -22 and above sky 7, below 6",
                  $"{Sky(7)}|{Sky(7)}|{Sky(6)}",
                  $"{U(QC.Zone.EastRonfaure, 23, y: -22)}|{U(QC.Zone.EastRonfaure, 23, y: 0)}|" +
                  $"{U(QC.Zone.EastRonfaure, 23, y: -22.5)}");
            ushort es = QC.Zone.EastSarutabaruta;
            Check("c3 N6 vain: East Sarutabaruta 52: from Windurst Woods or West Sarutabaruta z below 570 sky 1, " +
                  "else 2; from the Outer Horutoto Ruins z above 570 sky 2, at 570 nothing; elsewhere nothing",
                  $"{Sky(1)}|{Sky(2)}|{Sky(1)}|{Sky(2)}|{Sky(2)}||",
                  $"{U(es, 52, z: 569, prev: wd)}|{U(es, 52, z: 570, prev: wd)}|" +
                  $"{U(es, 52, z: 0, prev: QC.Zone.WestSarutabaruta)}|" +
                  $"{U(es, 52, z: 600, prev: QC.Zone.WestSarutabaruta)}|" +
                  $"{U(es, 52, z: 571, prev: QC.Zone.OuterHorutotoRuins)}|" +
                  $"{U(es, 52, z: 570, prev: QC.Zone.OuterHorutotoRuins)}|" +
                  $"{U(es, 52, z: 600, prev: QC.Zone.TahrongiCanyon)}");
            ushort kh = QC.Zone.KonschtatHighlands;
            Check("c3 N6 vain: Konschtat 106: z above 855 sky 2; else x between 32 and 370 (exclusive) sky 1; " +
                  "else nothing",
                  $"{Sky(2)}||{Sky(1)}|{Sky(1)}|||",
                  $"{U(kh, 106, x: 0, z: 856)}|{U(kh, 106, x: 0, z: 855)}|{U(kh, 106, x: 33, z: 0)}|" +
                  $"{U(kh, 106, x: 369, z: 0)}|{U(kh, 106, x: 32, z: 0)}|{U(kh, 106, x: 370, z: 0)}|" +
                  $"{U(kh, 106, x: -5, z: 0)}");
            Check("c3 N6 vain: Meriphataud 34: from Sauromugue sky 2, from Tahrongi 1, else nothing",
                  $"{Sky(2)}|{Sky(1)}|",
                  $"{U(QC.Zone.MeriphataudMountains, 34, prev: QC.Zone.SauromugueChampaign)}|" +
                  $"{U(QC.Zone.MeriphataudMountains, 34, prev: QC.Zone.TahrongiCanyon)}|" +
                  $"{U(QC.Zone.MeriphataudMountains, 34, prev: QC.Zone.Xarcabard)}");
            ushort ng = QC.Zone.NorthGustaberg;
            Check("c3 N6 vain: North Gustaberg 246: z above 461 sky 6; else x above -240 sky 7; else nothing",
                  $"{Sky(6)}|{Sky(7)}||",
                  $"{U(ng, 246, x: -500, z: 462)}|{U(ng, 246, x: -239, z: 461)}|{U(ng, 246, x: -240, z: 461)}|" +
                  $"{U(ng, 246, x: -300, z: 0)}");
            Check("c3 N6 vain: Pashhow 15: x below 362 sky 2, else nothing",
                  $"{Sky(2)}||",
                  $"{U(QC.Zone.PashhowMarshlands, 15, x: 361)}|{U(QC.Zone.PashhowMarshlands, 15, x: 362)}|" +
                  $"{U(QC.Zone.PashhowMarshlands, 15, x: 400)}");
            Check("c3 N6 vain: Rolanberry 4: z below 75 sky 1, else 2",
                  $"{Sky(1)}|{Sky(2)}|{Sky(2)}",
                  $"{U(QC.Zone.RolanberryFields, 4, z: 74)}|{U(QC.Zone.RolanberryFields, 4, z: 75)}|" +
                  $"{U(QC.Zone.RolanberryFields, 4, z: 100)}");
            ushort sc = QC.Zone.SauromugueChampaign;
            Check("c3 N6 vain: Sauromugue 5: from Garlaige sky 2, Meriphataud 4, Rolanberry or Port Jeuno 3, else " +
                  "nothing",
                  $"{Sky(2)}|{Sky(4)}|{Sky(3)}|{Sky(3)}|",
                  $"{U(sc, 5, prev: QC.Zone.GarlaigeCitadel)}|{U(sc, 5, prev: QC.Zone.MeriphataudMountains)}|" +
                  $"{U(sc, 5, prev: QC.Zone.RolanberryFields)}|{U(sc, 5, prev: QC.Zone.PortJeuno)}|" +
                  $"{U(sc, 5, prev: QC.Zone.Xarcabard)}");
            ushort sg2 = QC.Zone.SouthGustaberg;
            Check("c3 N6 vain: South Gustaberg 37: x above -390: z above -301 sky 6, else 7; x -390 and below nothing",
                  $"{Sky(6)}|{Sky(7)}|{Sky(7)}||",
                  $"{U(sg2, 37, x: -389, z: -300)}|{U(sg2, 37, x: -389, z: -301)}|{U(sg2, 37, x: 0, z: -400)}|" +
                  $"{U(sg2, 37, x: -390, z: 0)}|{U(sg2, 37, x: -500, z: -400)}");
            ushort tc = QC.Zone.TahrongiCanyon;
            Check("c3 N6 vain: Tahrongi 37: from East Sarutabaruta or Buburimu sky 7, the Maze of Shakhrami 6, else " +
                  "nothing",
                  $"{Sky(7)}|{Sky(7)}|{Sky(6)}|",
                  $"{U(tc, 37, prev: es)}|{U(tc, 37, prev: QC.Zone.BuburimuPeninsula)}|" +
                  $"{U(tc, 37, prev: QC.Zone.MazeOfShakhrami)}|{U(tc, 37, prev: QC.Zone.MeriphataudMountains)}");
            ushort zt = QC.Zone.TheSanctuaryOfZitah;
            Check("c3 N6 vain: Zitah 4: from the Boyahda Tree sky 7, Meriphataud 1, else nothing",
                  $"{Sky(7)}|{Sky(1)}|",
                  $"{U(zt, 4, prev: QC.Zone.TheBoyahdaTree)}|{U(zt, 4, prev: QC.Zone.MeriphataudMountains)}|" +
                  $"{U(zt, 4, prev: QC.Zone.Romaeve)}");
            Check("c3 N6 vain: West Sarutabaruta 50: z above 470 sky 2, else 1",
                  $"{Sky(2)}|{Sky(1)}|{Sky(1)}",
                  $"{U(QC.Zone.WestSarutabaruta, 50, z: 471)}|{U(QC.Zone.WestSarutabaruta, 50, z: 470)}|" +
                  $"{U(QC.Zone.WestSarutabaruta, 50, z: 0)}");
            Check("c3 N6 vain: Valkurm 5: z above 45 sky 1 (the inner z above -301 always holds there: sky 3 is " +
                  "unreachable, kept); 45 and below nothing",
                  $"{Sky(1)}|{Sky(1)}||",
                  $"{U(QC.Zone.ValkurmDunes, 5, z: 46)}|{U(QC.Zone.ValkurmDunes, 5, z: 500)}|" +
                  $"{U(QC.Zone.ValkurmDunes, 5, z: 45)}|{U(QC.Zone.ValkurmDunes, 5, z: -400)}");
            Check("c3 N6 vain: Xarcabard 11: from Beaucedine sky 2, else 3",
                  $"{Sky(2)}|{Sky(3)}|{Sky(3)}",
                  $"{U(QC.Zone.Xarcabard, 11, prev: QC.Zone.BeaucedineGlacier)}|" +
                  $"{U(QC.Zone.Xarcabard, 11, prev: dv)}|{U(QC.Zone.Xarcabard, 11, prev: 0)}");
            var c758 = N6Csid(d, 1, N6Fin, wt, 758);
            var no = F(4);
            no.CompleteResult = false;
            Check("c3 N6 vain: finishes: 752 status 1, the Star Seeker, the title; 758 completes then takes the " +
                  "drained seeker (complete false: nothing more); 118 takes the seeker, gives the drained one, " +
                  "status 3; 120 the trade, status 4; 2 status 2",
                  $"Player.SetMissionStatus(2,1),Player.GiveKeyItem({seeker})," +
                  $"Player.AddTitle({QC.Title.FugitiveMinisterBountyHunter})|" +
                  $"Mission.Complete(),Player.DelKeyItem({drained})|Mission.Complete()|" +
                  $"Player.DelKeyItem({seeker}),Player.GiveKeyItem({drained}),Player.SetMissionStatus(2,3)|" +
                  "Trade.Complete(),Player.SetMissionStatus(2,4)|Player.SetMissionStatus(2,2)",
                  $"{N6Run(N6Csid(d, 1, N6Fin, wt, 752), F(0))}|{N6Run(c758, F(4))}|{N6Run(c758, no)}|" +
                  $"{N6Run(N6Csid(d, 1, N6Fin, dv, 118), F(2))}|{N6Run(N6Csid(d, 1, N6Fin, dv, 120), F(3))}|" +
                  $"{N6Run(N6Csid(d, 1, N6Fin, QC.Zone.Romaeve, 2), F(1))}");
        }

        // 8-2 The Jester Who'd be King
        {
            int id = QC.Mission.Windurst.TheJesterWhodBeKing;
            var d = new QC.Missions.Windurst.TheJesterWhodBeKing().Compile();
            Fake F(int s) => N6Fake(W, id, id, status: s);
            string T(ushort zone, string npc, Func<int, Fake> f) => N6Over(N6Npc(d, 1, N6Trig, zone, npc), f);
            int man = QC.KeyItem.ManusteryRing, opt = QC.KeyItem.OptisteryRing, aur = QC.KeyItem.AurasteryRing,
                rhi = QC.KeyItem.RhinosteryRing, ora = QC.KeyItem.OrasteryRing,
                glove = QC.KeyItem.GloveOfPerpetualTwilight;
            Check("c3 N6 jester: Apururu: 588 (0, Manustery) at 0, 601 (0, Orastery) at 2, 590 at 6, 592 at 8, 609 " +
                  "(0, the glove) at 10 (progress); 589 at 1, 601 at 3, 591 at 7, 593 at 9 (events)",
                  N6Line((0, $"ProgressEvent(588,0,{man})"), (1, "Event(589)"), (2, $"ProgressEvent(601,0,{ora})"),
                         (3, $"Event(601,0,{ora})"), (6, "ProgressEvent(590)"), (7, "Event(591)"),
                         (8, "ProgressEvent(592)"), (9, "Event(593)"), (10, $"ProgressEvent(609,0,{glove})")),
                  T(wd, QC.Npc.WindurstWoods.Apururu, F));
            Check("c3 N6 jester: at status 0: Rakoh Buuma 584, Janshura-Rashura 484, Zokima-Rokima 376, Mokyokyo 764 " +
                  "(events)",
                  N6Line((0, "Event(584)")) + "#" + N6Line((0, "Event(484)")) + "#" + N6Line((0, "Event(376)")) + "#" +
                  N6Line((0, "Event(764)")),
                  T(wd, QC.Npc.WindurstWoods.RakohBuuma, F) + "#" + T(pw, QC.Npc.PortWindurst.JanshuraRashura, F) +
                  "#" + T(wl, QC.Npc.WindurstWalls.ZokimaRokima, F) + "#" + T(wt, QC.Npc.WindurstWaters.Mokyokyo, F));
            Check("c3 N6 jester: at 1: Sedal-Godjal 122 (0, Aurastery; progress) without it; _no4 22 (0, Rhinostery; " +
                  "progress cutscene) without it; Tosuka-Porika 801 (progress) without the Optistery, 802 (event) " +
                  "with it; each held: nothing",
                  N6Line((1, $"ProgressEvent(122,0,{aur})")) + "#" + N6Line() + "#" +
                  N6Line((1, $"ProgressCutscene(22,0,{rhi})")) + "#" + N6Line() + "#" +
                  N6Line((1, $"ProgressEvent(801,0,{opt})")) + "#" + N6Line((1, $"Event(802,0,{opt})")),
                  T(dv, QC.Npc.Davoi.SedalGodjal, F) + "#" + T(dv, QC.Npc.Davoi.SedalGodjal, s => N6Keys(F(s), aur)) +
                  "#" + T(QC.Zone.Feiyin, QC.Npc.Feiyin.no4, F) + "#" +
                  T(QC.Zone.Feiyin, QC.Npc.Feiyin.no4, s => N6Keys(F(s), rhi)) + "#" +
                  T(wt, QC.Npc.WindurstWaters.TosukaPorika, F) + "#" +
                  T(wt, QC.Npc.WindurstWaters.TosukaPorika, s => N6Keys(F(s), opt)));
            Check("c3 N6 jester: Kupipi 326 (0, Orastery) at 3 (progress), 327 at 4 (event); Shantotto 397 (0, 0, 0, " +
                  "the glove) at 7 (progress); _5cb 75 at 9 (progress cutscene)",
                  N6Line((3, $"ProgressEvent(326,0,{ora})"), (4, $"Event(327,0,{ora})")) + "#" +
                  N6Line((7, $"ProgressEvent(397,0,0,0,{glove})")) + "#" + N6Line((9, "ProgressCutscene(75)")),
                  T(ht, QC.Npc.HeavensTower.Kupipi, F) + "#" + T(wl, QC.Npc.WindurstWalls.Shantotto, F) + "#" +
                  T(QC.Zone.InnerHorutotoRuins, QC.Npc.InnerHorutotoRuins._5cb, F));
            string jester = QC.Mob.OuterHorutotoRuins.JESTER_WHOD_BE_KING_OFFSET;
            ushort oh = QC.Zone.OuterHorutotoRuins;
            string guard = $"MessageSpecial({QC.Text.OuterHorutotoRuins.GUARDIAN_BLOCKING_WAY})";
            Check("c3 N6 jester: _5e5 with neither queen spawned: GUARDIAN_BLOCKING_WAY at 4 (message special); 71 " +
                  "at 5 (progress); either queen spawned (alive or dead): only 71",
                  N6Line((4, guard), (5, "ProgressEvent(71)")) + "#" + N6Line((5, "ProgressEvent(71)")) + "#" +
                  N6Line((5, "ProgressEvent(71)")) + "#" + N6Line((5, "ProgressEvent(71)")),
                  T(oh, QC.Npc.OuterHorutotoRuins._5e5, F) + "#" +
                  T(oh, QC.Npc.OuterHorutotoRuins._5e5, s => N6Mob(F(s), jester, "alive", 0)) + "#" +
                  T(oh, QC.Npc.OuterHorutotoRuins._5e5, s => N6Mob(F(s), jester, "dead", 1)) + "#" +
                  T(oh, QC.Npc.OuterHorutotoRuins._5e5, s => N6Mob(F(s), jester, "alive", 1)));
            Check("c3 N6 jester: _5e5's spawn: the two queens with no claim",
                  $"Player.SpawnMob({jester},0),Player.SpawnMob({jester},1)",
                  N6Run(N6Npc(d, 1, N6Trig, oh, QC.Npc.OuterHorutotoRuins._5e5), F(4)));
            foreach (var (npc, other) in new[] { (QC.Npc.OuterHorutotoRuins.QueenofCoins, 0),
                                                 (QC.Npc.OuterHorutotoRuins.QueenofSwords, 1) })
            {
                var h = N6Npc(d, 1, N6Death, oh, npc);
                Check($"c3 N6 jester: {npc}'s death at 4 reads queen +{other}: dead (or never spawned): status 5; " +
                      "alive: nothing; the other queen alive: still status 5; at 3 and 5 nothing",
                      "Player.SetMissionStatus(2,5)|Player.SetMissionStatus(2,5)||Player.SetMissionStatus(2,5)||",
                      $"{N6Run(h, N6Mob(F(4), jester, "dead", other))}|{N6Run(h, F(4))}|" +
                      $"{N6Run(h, N6Mob(F(4), jester, "alive", other))}|" +
                      $"{N6Run(h, N6Mob(F(4), jester, "alive", 1 - other))}|{N6Run(h, F(3))}|{N6Run(h, F(5))}");
            }
            // the ring finishes: each gives its ring and sets 2 with the other two held
            foreach (var (zone, csid, ring, a, b) in new (ushort, int, int, int, int)[]
                     {
                         (dv, 122, aur, rhi, opt), (QC.Zone.Feiyin, 22, rhi, aur, opt), (wt, 801, opt, rhi, aur),
                     })
            {
                var h = N6Csid(d, 1, N6Fin, zone, csid);
                Check($"c3 N6 jester: {csid}'s finish gives its ring; with the other two held status 2; with one or " +
                      "neither: nothing more",
                      $"Player.GiveKeyItem({ring}),Player.SetMissionStatus(2,2)|Player.GiveKeyItem({ring})|" +
                      $"Player.GiveKeyItem({ring})|Player.GiveKeyItem({ring})",
                      $"{N6Run(h, N6Keys(F(1), a, b))}|{N6Run(h, N6Keys(F(1), a))}|{N6Run(h, N6Keys(F(1), b))}|" +
                      $"{N6Run(h, F(1))}");
            }
            var c609 = N6Csid(d, 1, N6Fin, wd, 609);
            var no = F(10);
            no.CompleteResult = false;
            Check("c3 N6 jester: finishes: 588 status 1 and Manustery; 590 status 7; 592 takes the glove, status 9; " +
                  "601 status 3; 609 completes then Option 1 (complete false: nothing more); 326 status 4; 75 takes " +
                  "the book, status 10; 71 Orastery, status 6; 397 the glove, the five rings taken, status 8",
                  $"Player.SetMissionStatus(2,1),Player.GiveKeyItem({man})|Player.SetMissionStatus(2,7)|" +
                  $"Player.DelKeyItem({glove}),Player.SetMissionStatus(2,9)|Player.SetMissionStatus(2,3)|" +
                  "Mission.Complete(),Mission.SetVar(Option,1)|Mission.Complete()|Player.SetMissionStatus(2,4)|" +
                  $"Player.DelKeyItem({QC.KeyItem.BookOfTheGods}),Player.SetMissionStatus(2,10)|" +
                  $"Player.GiveKeyItem({ora}),Player.SetMissionStatus(2,6)|" +
                  $"Player.GiveKeyItem({glove}),Player.DelKeyItem({man}),Player.DelKeyItem({opt})," +
                  $"Player.DelKeyItem({aur}),Player.DelKeyItem({rhi}),Player.DelKeyItem({ora})," +
                  "Player.SetMissionStatus(2,8)",
                  $"{N6Run(N6Csid(d, 1, N6Fin, wd, 588), F(0))}|{N6Run(N6Csid(d, 1, N6Fin, wd, 590), F(6))}|" +
                  $"{N6Run(N6Csid(d, 1, N6Fin, wd, 592), F(8))}|{N6Run(N6Csid(d, 1, N6Fin, wd, 601), F(2))}|" +
                  $"{N6Run(c609, F(10))}|{N6Run(c609, no)}|{N6Run(N6Csid(d, 1, N6Fin, ht, 326), F(3))}|" +
                  $"{N6Run(N6Csid(d, 1, N6Fin, QC.Zone.InnerHorutotoRuins, 75), F(9))}|" +
                  $"{N6Run(N6Csid(d, 1, N6Fin, oh, 71), F(5))}|{N6Run(N6Csid(d, 1, N6Fin, wl, 397), F(7))}");
            Fake O(int option, bool done = true) =>
                N6Var(N6Fake(W, id, QC.Mission.Windurst.None, done: done ? new[] { id } : new int[0]), W, id, "Option",
                      option);
            var s3 = d.States[2];
            Check("c3 N6 jester: section 3 holds when completed with Option 1; fails with 0 or 2, or not completed; " +
                  "Shantotto 399 (0, 0, 0, the glove; progress), Apururu 605 (event); 399's finish: Option 0",
                  $"True False False False|ProgressEvent(399,0,0,0,{glove})|Event(605)|Mission.SetVar(Option,0)",
                  $"{s3.IsMissionMatch(O(1))} {s3.IsMissionMatch(O(0))} {s3.IsMissionMatch(O(2))} " +
                  $"{s3.IsMissionMatch(O(1, false))}|" +
                  $"{N6Reply(N6Npc(d, 2, N6Trig, wl, QC.Npc.WindurstWalls.Shantotto), O(1))}|" +
                  $"{N6Reply(N6Npc(d, 2, N6Trig, wd, QC.Npc.WindurstWoods.Apururu), O(1))}|" +
                  $"{N6Run(N6Csid(d, 2, N6Fin, wl, 399), O(1))}");
        }

        // 9-1 Doll of the Dead
        {
            int id = QC.Mission.Windurst.DollOfTheDead;
            var d = new QC.Missions.Windurst.DollOfTheDead().Compile();
            Fake F(int s) => N6Fake(W, id, id, status: s);
            string T(ushort zone, string npc) => N6Over(N6Npc(d, 1, N6Trig, zone, npc), F);
            Check("c3 N6 doll: Apururu 619 at 0, 620 at 3, 621 at 6; _6q2 362 at 2; Yoran-Oran 439 (0, the jug, the " +
                  "clump) at 4 (progress)",
                  N6Line((0, "ProgressEvent(619)"), (3, "ProgressEvent(620)"), (6, "ProgressEvent(621)")) + "#" +
                  N6Line((2, "ProgressEvent(362)")) + "#" +
                  N6Line((4, $"ProgressEvent(439,0,{QC.Item.JugOfHumus},{QC.Item.ClumpOfGoobbueHumus})")),
                  T(wd, QC.Npc.WindurstWoods.Apururu) + "#" + T(ht, QC.Npc.HeavensTower._6q2) + "#" +
                  T(wl, QC.Npc.WindurstWalls.YoranOran));
            Check("c3 N6 doll: zone-ins (events): Heavens Tower 335 at 1, the Full Moon Fountain 61 at 7",
                  N6Line((1, "Event(335)")) + "#" + N6Line((7, "Event(61)")),
                  N6Over(N6Csid(d, 1, N6Zone, ht), F) + "#" +
                  N6Over(N6Csid(d, 1, N6Zone, QC.Zone.FullMoonFountain), F));
            ushort bt = QC.Zone.TheBoyahdaTree;
            var warden = N6Npc(d, 1, N6Trig, bt, QC.Npc.TheBoyahdaTree.MandragoraWarden);
            string tl = $"MessageSpecial({QC.Text.TheBoyahdaTree.WARDEN_TRANSLATION})";
            Check("c3 N6 doll: the Mandragora Warden at 4 and 5: WARDEN_SPEECH from the npc, then WARDEN_TRANSLATION " +
                  "(message special); else nothing",
                  N6Line((4, tl), (5, tl)) + $"#Text.MessageTextFromNpc({QC.Text.TheBoyahdaTree.WARDEN_SPEECH})|",
                  N6Over(warden, F) + $"#{N6Run(warden, F(4))}|{N6Run(warden, F(3))}");
            var wtr = N6Npc(d, 1, N6Trade, bt, QC.Npc.TheBoyahdaTree.MandragoraWarden);
            int clump = QC.Item.ClumpOfGoobbueHumus;
            Fake Tr(int s, int item)
            {
                var f = F(s);
                f.TradeResult = item == QC.Item.ClumpOfGoobbueHumus;
                return f;
            }
            Check("c3 N6 doll: the Warden's trade: the clump at 4 and 5: 13 (progress cutscene); at 3 and 6, another " +
                  "item: nothing",
                  "ProgressCutscene(13)|ProgressCutscene(13)|null|null|null",
                  $"{N6Reply(wtr, Tr(4, clump))}|{N6Reply(wtr, Tr(5, clump))}|" +
                  $"{N6Reply(wtr, Tr(3, clump))}|{N6Reply(wtr, Tr(6, clump))}|" +
                  $"{N6Reply(wtr, Tr(4, QC.Item.CurseWand))}");
            int letter = QC.KeyItem.LetterFromZonpaZippa;
            Check("c3 N6 doll: finishes: 61 completes; 335 status 2; 362 status 3; 13 the trade, status 6, the " +
                  "letter; 439 status 5; 619 status 1; 620 status 4; 621 status 7 and the letter taken",
                  "Mission.Complete()|Player.SetMissionStatus(2,2)|Player.SetMissionStatus(2,3)|" +
                  $"Trade.Complete(),Player.SetMissionStatus(2,6),Player.GiveKeyItem({letter})|" +
                  "Player.SetMissionStatus(2,5)|Player.SetMissionStatus(2,1)|Player.SetMissionStatus(2,4)|" +
                  $"Player.SetMissionStatus(2,7),Player.DelKeyItem({letter})",
                  $"{N6Run(N6Csid(d, 1, N6Fin, QC.Zone.FullMoonFountain, 61), F(7))}|" +
                  $"{N6Run(N6Csid(d, 1, N6Fin, ht, 335), F(1))}|{N6Run(N6Csid(d, 1, N6Fin, ht, 362), F(2))}|" +
                  $"{N6Run(N6Csid(d, 1, N6Fin, bt, 13), F(4))}|{N6Run(N6Csid(d, 1, N6Fin, wl, 439), F(4))}|" +
                  $"{N6Run(N6Csid(d, 1, N6Fin, wd, 619), F(0))}|{N6Run(N6Csid(d, 1, N6Fin, wd, 620), F(3))}|" +
                  $"{N6Run(N6Csid(d, 1, N6Fin, wd, 621), F(6))}");
        }

        // 9-2 Moon Reading
        {
            int id = QC.Mission.Windurst.MoonReading;
            var d = new QC.Missions.Windurst.MoonReading().Compile();
            Fake F(int s) => N6Fake(W, id, id, status: s);
            int rom = QC.KeyItem.AncientVerseOfRomaeve, alt = QC.KeyItem.AncientVerseOfAltepa,
                ugg = QC.KeyItem.AncientVerseOfUggalepih;
            var q2 = N6Npc(d, 1, N6Trig, ht, QC.Npc.HeavensTower._6q2);
            string cannot = $"MessageSpecial({QC.Text.HeavensTower.ITEM_CANNOT_BE_OBTAINED},{QC.Item.WindurstianFlag})";
            Check("c3 N6 moon reading: _6q2: 384 at 0; 385 at 1 with the three verses; 386 (0, 0, Orastery) at 3; " +
                  "407 at 4 with a free slot (progress); with none ITEM_CANNOT_BE_OBTAINED (the flag); a verse " +
                  "missing: nothing at 1",
                  N6Line((0, "ProgressEvent(384)"), (1, "ProgressEvent(385)"),
                         (3, $"ProgressEvent(386,0,0,{QC.KeyItem.OrasteryRing})"), (4, "ProgressEvent(407)")) + "#" +
                  N6Line((0, "ProgressEvent(384)"), (3, $"ProgressEvent(386,0,0,{QC.KeyItem.OrasteryRing})"),
                         (4, cannot)) + "#" +
                  $"null|null|null",
                  N6Over(q2, s => N6Keys(F(s), rom, alt, ugg)) + "#" +
                  N6Over(q2, s => N6With(F(s), f => f.FreeSlots = 0)) + "#" +
                  $"{N6Reply(q2, N6Keys(F(1), alt, ugg))}|{N6Reply(q2, N6Keys(F(1), rom, ugg))}|" +
                  $"{N6Reply(q2, N6Keys(F(1), rom, alt))}");
            Check("c3 N6 moon reading: Kupipi 400 at 3 and above (progress, importantOnce)",
                  N6Line(Enumerable.Range(3, 9).Select(s => (s, "ProgressEvent(400):importantOnce()")).ToArray()),
                  N6Over(N6Npc(d, 1, N6Trig, ht, QC.Npc.HeavensTower.Kupipi), F));
            Check("c3 N6 moon reading: Qu'Hau Spring 4 and qm_windy_9_2 68 at 1 without their verse (progress " +
                  "cutscene); with it nothing",
                  N6Line((1, "ProgressCutscene(4)")) + "#" + N6Line() + "#" + N6Line((1, "ProgressCutscene(68)")) +
                  "#" + N6Line(),
                  N6Over(N6Npc(d, 1, N6Trig, QC.Zone.Romaeve, QC.Npc.Romaeve.QuHauSpring), F) + "#" +
                  N6Over(N6Npc(d, 1, N6Trig, QC.Zone.Romaeve, QC.Npc.Romaeve.QuHauSpring), s => N6Keys(F(s), rom)) +
                  "#" + N6Over(N6Npc(d, 1, N6Trig, QC.Zone.TempleOfUggalepih, QC.Npc.TempleOfUggalepih.qmwindy92), F) +
                  "#" + N6Over(N6Npc(d, 1, N6Trig, QC.Zone.TempleOfUggalepih, QC.Npc.TempleOfUggalepih.qmwindy92),
                      s => N6Keys(F(s), ugg)));
            ushort qs6 = QC.Zone.QuicksandCaves;
            var co = N6Csid(d, 1, N6Zone, QC.Zone.ChamberOfOracles);
            Fake Co(int s, int prev, bool verse = false)
            {
                var f = N6With(F(s), x => x.PrevZone = prev);
                return verse ? N6Keys(f, alt) : f;
            }
            Check("c3 N6 moon reading: the Chamber of Oracles' zone-in: from the Quicksand Caves at 1 without the " +
                  "verse: 3, text table -1, flags 0x13; from elsewhere, with the verse, at 0 and 2: nothing",
                  "{3,-1,19}|null|null|null|null",
                  $"{QuestContextFake.DescribeZoneIn(co.Reply(Co(1, qs6)))}|{N6Reply(co, Co(1, wl))}|" +
                  $"{N6Reply(co, Co(1, qs6, true))}|{N6Reply(co, Co(0, qs6))}|{N6Reply(co, Co(2, qs6))}");
            var wz = N6Csid(d, 1, N6Zone, wl);
            Check("c3 N6 moon reading: Windurst Walls' zone-in at 4: the char set at (0, -16.75, 130, 64), 443, text " +
                  "table -1, flags 0x82; at 3 and 5 nothing",
                  "{443,-1,130}|Player.SetPos(0,-16.75,130,64)|null|null",
                  $"{QuestContextFake.DescribeZoneIn(wz.Reply(F(4)))}|{N6Run(wz, F(4))}|{N6Reply(wz, F(3))}|" +
                  $"{N6Reply(wz, F(5))}");
            var won = N6Csid(d, 1, N6Fin, QC.Zone.FullMoonFountain, 32001);
            string Wn(int s, int win) => N6Run(won, N4With(F(s), win: win));
            Check("c3 N6 moon reading: the Full Moon Fountain's 32001 finish: status 2 with battlefieldWin 225: the " +
                  "three verses taken, status 3; 224, 0, at 1 and 3: nothing",
                  $"Player.DelKeyItem({rom}),Player.DelKeyItem({alt}),Player.DelKeyItem({ugg})," +
                  "Player.SetMissionStatus(2,3)||||",
                  $"{Wn(2, 225)}|{Wn(2, 224)}|{Wn(2, 0)}|{Wn(1, 225)}|{Wn(3, 225)}");
            Check("c3 N6 moon reading: finishes: 3 Altepa, 4 Ro'Maeve, 68 Uggalepih; 384 status 1, 385 2, 386 4; 407 " +
                  "the char to Windurst Walls (0, -16.75, 130, 64); 443 completes",
                  $"Player.GiveKeyItem({alt})|Player.GiveKeyItem({rom})|Player.GiveKeyItem({ugg})|" +
                  "Player.SetMissionStatus(2,1)|Player.SetMissionStatus(2,2)|Player.SetMissionStatus(2,4)|" +
                  $"Player.SetPos(0,-16.75,130,64,{wl})|Mission.Complete()",
                  $"{N6Run(N6Csid(d, 1, N6Fin, QC.Zone.ChamberOfOracles, 3), F(1))}|" +
                  $"{N6Run(N6Csid(d, 1, N6Fin, QC.Zone.Romaeve, 4), F(1))}|" +
                  $"{N6Run(N6Csid(d, 1, N6Fin, QC.Zone.TempleOfUggalepih, 68), F(1))}|" +
                  $"{N6Run(N6Csid(d, 1, N6Fin, ht, 384), F(0))}|{N6Run(N6Csid(d, 1, N6Fin, ht, 385), F(1))}|" +
                  $"{N6Run(N6Csid(d, 1, N6Fin, ht, 386), F(3))}|{N6Run(N6Csid(d, 1, N6Fin, ht, 407), F(4))}|" +
                  $"{N6Run(N6Csid(d, 1, N6Fin, wl, 443), F(4))}");
            var s3 = d.States[2];
            Check("c3 N6 moon reading: section 3 holds current at 3 and 4, or completed; fails current at 2, with " +
                  "another mission current and not completed",
                  "True True True False False",
                  $"{s3.IsMissionMatch(F(3))} {s3.IsMissionMatch(F(4))} " +
                  $"{s3.IsMissionMatch(N6Fake(W, id, QC.Mission.Windurst.None, done: id))} {s3.IsMissionMatch(F(2))} " +
                  $"{s3.IsMissionMatch(N6Fake(W, id, QC.Mission.Windurst.DollOfTheDead, status: 5))}");
            var lines = new (string Npc, int Csid)[]
            {
                (QC.Npc.HeavensTower.Aeshushu, 391), (QC.Npc.HeavensTower.Boycoco, 388),
                (QC.Npc.HeavensTower.Churara, 390), (QC.Npc.HeavensTower.Dattata, 392),
                (QC.Npc.HeavensTower.FooBeibo, 403), (QC.Npc.HeavensTower.Heruru, 393),
                (QC.Npc.HeavensTower.Ikucheechee, 394), (QC.Npc.HeavensTower.Kinono, 398),
                (QC.Npc.HeavensTower.Kiwawa, 389), (QC.Npc.HeavensTower.Nayutata, 395),
                (QC.Npc.HeavensTower.Nebibi, 399), (QC.Npc.HeavensTower.RhyEpocan, 405),
                (QC.Npc.HeavensTower.ShazNorem, 401), (QC.Npc.HeavensTower.Tsuryarya, 396),
                (QC.Npc.HeavensTower.UfuKoromoa, 402), (QC.Npc.HeavensTower.Utsuitsui, 397),
                (QC.Npc.HeavensTower.VahnPaineesha, 404), (QC.Npc.HeavensTower.Zubaba, 387),
            };
            Check("c3 N6 moon reading: section 3: the eighteen Heavens Tower lines (events, replaceDefault)",
                  string.Join("|", lines.Select(l => $"Event({l.Csid}):replaceDefault()")),
                  string.Join("|", lines.Select(l => N6Reply(N6Npc(d, 2, N6Trig, ht, l.Npc), F(3)))));
            var done = N6Fake(W, id, QC.Mission.Windurst.None, done: id);
            Check("c3 N6 moon reading: section 4 holds when completed only; Janshura-Rashura 567, Mokyokyo 837, " +
                  "Rakoh Buuma 632 (events, oncePerZone); Tosuka-Porika 380 (event, replaceDefault)",
                  "True False|Event(567):oncePerZone()|Event(837):oncePerZone()|Event(632):oncePerZone()|" +
                  "Event(380):replaceDefault()",
                  $"{d.States[3].IsMissionMatch(done)} {d.States[3].IsMissionMatch(F(4))}|" +
                  $"{N6Reply(N6Npc(d, 3, N6Trig, pw, QC.Npc.PortWindurst.JanshuraRashura), done)}|" +
                  $"{N6Reply(N6Npc(d, 3, N6Trig, wt, QC.Npc.WindurstWaters.Mokyokyo), done)}|" +
                  $"{N6Reply(N6Npc(d, 3, N6Trig, wd, QC.Npc.WindurstWoods.RakohBuuma), done)}|" +
                  $"{N6Reply(N6Npc(d, 3, N6Trig, wt, QC.Npc.WindurstWaters.TosukaPorika), done)}");
        }
    }
}
