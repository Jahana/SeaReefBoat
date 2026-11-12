// XI.Verify.Quests -- quest rework C3 N4 (docs/superpowers/plans/2026-11-07-quest-rework-c3-lanes-1.md, section 4,
// Nations): the rank 4-5 missions (Magicite, the 5-1s, the 5-2s) and their two battlefields (Qu'Bia Arena's Rank 5
// Mission, the Throne Room's Shadow Lord) on C#, one fake check per arm no packet shows, as the Lua files had them:
// each section check on its operands, each reply on the reads it branches on, each finish on its option and the
// items it reads.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    internal static void RunC3N4()
    {
        Console.WriteLine("\n== c3 N4: the rank 4-5 missions and their battlefields on C# ==");
        RunC3N4Magicite();
        RunC3N4DarknessRising();
        RunC3N4TheRuinsOfFeiYin();
        RunC3N4TheFinalSeal();
        RunC3N4XarcabardLandOfTruths();
        RunC3N4TheShadowLord();
        RunC3N4TheShadowAwaits();
        RunC3N4Rank5Mission();
        RunC3N4ShadowLordBattle();
    }

    static readonly QuestHandlerKind N4Fin = QuestHandlerKind.OnEventFinish;
    static readonly QuestHandlerKind N4Trig = QuestHandlerKind.OnTrigger;
    static readonly QuestHandlerKind N4Zone = QuestHandlerKind.OnZoneIn;

    // a fake on one nation mission: current, status, nation (the log's by default), option, rank (of that nation),
    // completed missions of the log
    static Fake N4Fake(MissionLog log, int id, int current, int status = 0, int nation = -1, int option = 0,
                       int rank = 5, params int[] done)
    {
        var f = N3Fake(log, id, current, status, nation, option, done);
        f.Ranks[f.NationId] = rank;
        return f;
    }

    static Fake N4With(Fake f, int keyItem = 0, int win = -1, int prev = -1)
    {
        if (keyItem != 0) f.KeyItems[keyItem] = true;
        if (win >= 0) f.PlayerLocalVars["battlefieldWin"] = win;
        if (prev >= 0) f.PrevZone = prev;
        return f;
    }

    // the clean / key / LegacyPath / reward line of a mission
    static void N4Header(string label, MissionScript m, MissionScriptDefinition d, string key, string path,
                         string reward, string tag = "c3 N4")
    {
        Check($"{tag} {label}: clean, its key and LegacyPath, the reward (gil/rank/key item/rank points)",
              $"|{key}|{path}|{reward}",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.Reward.Gil}/" +
              $"{d.Reward.Rank}/{d.Reward.KeyItem}/{d.Reward.RankPoints}");
    }

    // a 5-1's section 1: no mission current, a char of the log's nation, rank 5, the mission not completed
    static void N4Section1Rank5(string label, MissionScriptDefinition d, MissionLog log, int id, int none, int other)
    {
        int l = (int)log, foreign = (l + 1) % 3;
        var s = d.States[0];
        Check($"c3 N4 {label}: section 1 holds with no mission current, a char of nation {l} at rank 5; fails for " +
              $"nation {foreign}, with a mission current (this one, another), at rank 4 and 6, and once completed",
              "True False False False False False False",
              $"{s.IsMissionMatch(N4Fake(log, id, none))} {s.IsMissionMatch(N4Fake(log, id, none, nation: foreign))} " +
              $"{s.IsMissionMatch(N4Fake(log, id, id))} {s.IsMissionMatch(N4Fake(log, id, other))} " +
              $"{s.IsMissionMatch(N4Fake(log, id, none, rank: 4))} " +
              $"{s.IsMissionMatch(N4Fake(log, id, none, rank: 6))} " +
              $"{s.IsMissionMatch(N4Fake(log, id, none, done: id))}");
    }

    // a 5-2's section 1: no mission current and a char of the log's nation (no rank, no completion test)
    static void N4Section1Open(string label, MissionScriptDefinition d, MissionLog log, int id, int none, int other,
                               string tag = "c3 N4")
    {
        int l = (int)log, foreign = (l + 1) % 3;
        var s = d.States[0];
        Check($"{tag} {label}: section 1 holds with no mission current and a char of nation {l} (any rank: 1, 5; " +
              $"completed too); fails for nation {foreign} and with a mission current (this one, another)",
              "True True True False False False",
              $"{s.IsMissionMatch(N4Fake(log, id, none, rank: 1))} {s.IsMissionMatch(N4Fake(log, id, none))} " +
              $"{s.IsMissionMatch(N4Fake(log, id, none, done: id))} " +
              $"{s.IsMissionMatch(N4Fake(log, id, none, nation: foreign))} " +
              $"{s.IsMissionMatch(N4Fake(log, id, id))} {s.IsMissionMatch(N4Fake(log, id, other))}");
    }

    // section 2 of a mission: current only
    static void N4Current(string label, MissionScriptDefinition d, int state, MissionLog log, int id, int none,
                          string tag = "c3 N4")
    {
        Check($"{tag} {label}: section {state + 1} holds while this mission is current only (not with none, not with " +
              "the next)",
              "True False False",
              $"{d.States[state].IsMissionMatch(N4Fake(log, id, id))} " +
              $"{d.States[state].IsMissionMatch(N4Fake(log, id, none))} " +
              $"{d.States[state].IsMissionMatch(N4Fake(log, id, id + 1))}");
    }

    // the Bastok gate guards' 1001 finishes with the YOU_ACCEPT_THE_MISSION line; extra: the calls between the begin
    // and the message
    static void N4BastokAccept(string label, MissionScriptDefinition d, int id, int opt, string mid = "",
                               string tag = "c3 N4")
    {
        foreach (var (zone, text) in new[]
                 {
                     (QC.Zone.BastokMarkets, QC.Text.BastokMarkets.YOU_ACCEPT_THE_MISSION),
                     (QC.Zone.BastokMines, QC.Text.BastokMines.YOU_ACCEPT_THE_MISSION),
                     (QC.Zone.Metalworks, QC.Text.Metalworks.YOU_ACCEPT_THE_MISSION),
                     (QC.Zone.PortBastok, QC.Text.PortBastok.YOU_ACCEPT_THE_MISSION),
                 })
        {
            var h = C2MHandler(d, 0, N4Fin, zone, id: 1001);
            Fake F(int option) => N4Fake(MissionLog.Bastok, id, QC.Mission.Bastok.None, option: option);
            Check($"{tag} {label}: zone {zone}'s 1001 finish: option {opt} begins{mid} with its zone's " +
                  $"YOU_ACCEPT_THE_MISSION; options {opt - 1}, {opt + 1} and 0 do nothing",
                  $"Mission.Begin(){mid},Text.Special({text})|||",
                  $"{C2MRun(h, F(opt))}|{C2MRun(h, F(opt - 1))}|{C2MRun(h, F(opt + 1))}|{C2MRun(h, F(0))}");
        }
    }

    // the San d'Oria 1009 / 2009 finishes: option <opt> begins, then <after> (the 5-1's status, message)
    static void N4SandoriaAccept(string label, MissionScriptDefinition d, int id, int opt, bool five1,
                                 string tag = "c3 N4")
    {
        foreach (var (zone, csid, text) in new[]
                 {
                     (QC.Zone.SouthernSanDoria, 1009, QC.Text.SouthernSanDoria.YOU_ACCEPT_THE_MISSION),
                     (QC.Zone.SouthernSanDoria, 2009, QC.Text.SouthernSanDoria.YOU_ACCEPT_THE_MISSION),
                     (QC.Zone.NorthernSanDoria, 1009, QC.Text.NorthernSanDoria.YOU_ACCEPT_THE_MISSION),
                 })
        {
            var h = C2MHandler(d, 0, N4Fin, zone, id: csid);
            Fake F(int option) => N4Fake(MissionLog.Sandoria, id, QC.Mission.Sandoria.None, option: option);
            string after = five1
                ? $",Player.SetMissionStatus(0,9),Player.DelKeyItem({QC.KeyItem.MessageToJeunoSandoria})," +
                  $"Text.Special({text})"
                : "";
            Check($"{tag} {label}: zone {zone}'s {csid} finish: option {opt} begins" +
                  (five1 ? ", status 9, the message taken and the zone's YOU_ACCEPT_THE_MISSION" : " (no message)") +
                  $"; options {opt - 1}, {opt + 1} and 0 do nothing",
                  $"Mission.Begin(){after}|||",
                  $"{C2MRun(h, F(opt))}|{C2MRun(h, F(opt - 1))}|{C2MRun(h, F(opt + 1))}|{C2MRun(h, F(0))}");
        }
    }

    // the 5-1s' Fei'Yin zone-in, its finish 1 and the Qu'Bia Arena 32001 finish
    static void N4FeiYin(string label, MissionScriptDefinition d, MissionLog log, int id)
    {
        int l = (int)log;
        Fake F(int status, int win = -1) => N4With(N4Fake(log, id, id, status), win: win);
        var zin = C2MHandler(d, 1, N4Zone, QC.Zone.Feiyin, id: 1);
        Check($"c3 N4 {label}: Fei'Yin zone-in at status 10: 1; at 9 and 11: nothing; 1's finish sets status 11",
              $"Event(1)|null|null|Player.SetMissionStatus({l},11)",
              $"{C3NReply(zin, F(10))}|{C3NReply(zin, F(9))}|{C3NReply(zin, F(11))}|" +
              $"{C2MRun(C2MHandler(d, 1, N4Fin, QC.Zone.Feiyin, id: 1), F(10))}");
        var w = C2MHandler(d, 1, N4Fin, QC.Zone.QubiaArena, id: 32001);
        int r5 = QC.Battlefield.Rank5Mission;
        Check($"c3 N4 {label}: Qu'Bia Arena 32001 at status 11 with battlefieldWin {r5}: the Burnt Seal, status 12, " +
              $"the New Fei'Yin Seal taken; at 10 and 12 with the win, at 11 with another win (the Shadow Lord's " +
              $"{QC.Battlefield.ShadowLordBattle}, {r5 - 1}, {r5 + 1}) and with none: nothing",
              $"Player.GiveKeyItem({QC.KeyItem.BurntSeal}),Player.SetMissionStatus({l},12)," +
              $"Player.DelKeyItem({QC.KeyItem.NewFeiyinSeal})||||||",
              $"{C2MRun(w, F(11, r5))}|{C2MRun(w, F(10, r5))}|{C2MRun(w, F(12, r5))}|" +
              $"{C2MRun(w, F(11, QC.Battlefield.ShadowLordBattle))}|{C2MRun(w, F(11, r5 - 1))}|" +
              $"{C2MRun(w, F(11, r5 + 1))}|{C2MRun(w, F(11))}");
    }

    // a completion finish: complete, then take <ki>; complete false: the key item kept
    static void N4Complete(string label, string what, QuestHandlerDecl h, Func<Fake> f, int ki, string extra = "",
                           string tag = "c3 N4")
    {
        var failed = f();
        failed.CompleteResult = false;
        Check($"{tag} {label}: {what} completes, then takes the key item {ki}{extra}; complete false: kept",
              $"Mission.Complete(),Player.DelKeyItem({ki}){(extra.Length > 0 ? extra : "")}|Mission.Complete()",
              $"{C2MRun(h, f())}|{C2MRun(h, failed)}");
    }

    // the 5-2s' Throne Room: the zone-in, _4l1, 6, 7 and 32001
    static void N4ThroneRoom(string label, MissionScriptDefinition d, MissionLog log, int id)
    {
        int l = (int)log;
        ushort tr = QC.Zone.ThroneRoom;
        int z = QC.MissionLog.Zilart, tnf = QC.Mission.Zilart.TheNewFrontier;
        int slb = QC.Battlefield.ShadowLordBattle;
        Fake F(int status, int post = 0, int win = -1, int zilart = -1, bool zDone = false)
        {
            var f = N4With(N4Fake(log, id, id, status), win: win);
            if (post != 0) f.MissionVars[$"{l}/{id}/PostBattle"] = post;
            f.CurrentMissions[z] = zilart < 0 ? 0xFFFF : zilart;
            if (zDone) f.CompletedMissions.Add($"{z}/{tnf}");
            return f;
        }
        var zin = C2MHandler(d, 1, N4Zone, tr, id: 1);
        Check($"c3 N4 {label}: Throne Room zone-in at status 3 with PostBattle 1: 7 (text table -1, flags 8); at 3 " +
              "with PostBattle 0 and 2, at 2 and 4 with PostBattle 1: nothing",
              "Event(7,{text_table=-1,flags=8})|null|null|null|null",
              $"{C3NReply(zin, F(3, 1))}|{C3NReply(zin, F(3))}|{C3NReply(zin, F(3, 2))}|{C3NReply(zin, F(2, 1))}|" +
              $"{C3NReply(zin, F(4, 1))}");
        var door = C2MHandler(d, 1, N4Trig, tr, QC.Npc.ThroneRoom._4l1);
        Check($"c3 N4 {label}: _4l1 at status 2: 6 (progress); at 1 and 3: nothing; 6's finish sets status 3",
              $"ProgressEvent(6)|null|null|Player.SetMissionStatus({l},3)",
              $"{C3NReply(door, F(2))}|{C3NReply(door, F(1))}|{C3NReply(door, F(3))}|" +
              $"{C2MRun(C2MHandler(d, 1, N4Fin, tr, id: 6), F(2))}");
        Check($"c3 N4 {label}: 7's finish: status 4, PostBattle 0, the Shadow Fragment",
              $"Player.SetMissionStatus({l},4),Mission.SetVar(PostBattle,0)," +
              $"Player.GiveKeyItem({QC.KeyItem.ShadowFragment})",
              C2MRun(C2MHandler(d, 1, N4Fin, tr, id: 7), F(3, 1)));
        var w = C2MHandler(d, 1, N4Fin, tr, id: 32001);
        string tail = "Mission.SetVar(PostBattle,1),Player.SetPos(90.425,-5.749,0.089,3,165)";
        Check($"c3 N4 {label}: 32001 at status 3 with battlefieldWin {slb}: Zilart's The New Frontier added (Zilart " +
              "none, or another Zilart mission current), PostBattle 1, set down at 90.425 -5.749 0.089 (3) in the " +
              "Throne Room; with The New Frontier current, or completed: not added",
              $"Player.AddMission({z},{tnf}),{tail}|Player.AddMission({z},{tnf}),{tail}|{tail}|{tail}",
              $"{C2MRun(w, F(3, win: slb))}|{C2MRun(w, F(3, win: slb, zilart: tnf + 1))}|" +
              $"{C2MRun(w, F(3, win: slb, zilart: tnf))}|{C2MRun(w, F(3, win: slb, zDone: true))}");
        Check($"c3 N4 {label}: 32001 at status 2 and 4 with the win, at 3 with another win (Rank 5 Mission's " +
              $"{QC.Battlefield.Rank5Mission}, {slb - 1}, {slb + 1}) and with none: nothing",
              "|||||",
              $"{C2MRun(w, F(2, win: slb))}|{C2MRun(w, F(4, win: slb))}|" +
              $"{C2MRun(w, F(3, win: QC.Battlefield.Rank5Mission))}|{C2MRun(w, F(3, win: slb - 1))}|" +
              $"{C2MRun(w, F(3, win: slb + 1))}|{C2MRun(w, F(3))}");
    }

    static void RunC3N4DarknessRising()
    {
        const string L = "darkness rising";
        var m = new QC.Missions.Bastok.DarknessRising();
        var d = m.Compile();
        int b = QC.MissionLog.Bastok, id = QC.Mission.Bastok.DarknessRising, none = QC.Mission.Bastok.None;
        const int msg = QC.KeyItem.MessageToJeunoBastok, seal = QC.KeyItem.NewFeiyinSeal;
        N4Header(L, m, d, QC.MissionKey.Bastok.DarknessRising, "scripts/missions/bastok/5_1_Darkness_Rising",
                 "///600");
        Check($"c3 N4 {L}: two sections (section 1 and the current one)", "2", d.States.Count.ToString());
        N4Section1Rank5(L, d, MissionLog.Bastok, id, none, QC.Mission.Bastok.Magicite);
        N4Current(L, d, 1, MissionLog.Bastok, id, none);
        Fake F(int status = 0, int current = -1, int option = 0) =>
            N4Fake(MissionLog.Bastok, id, current < 0 ? none : current, status, option: option);
        string R(int state, ushort zone, string npc, Fake f) => C3NReply(C2MHandler(d, state, N4Trig, zone, npc), f);
        foreach (var (zone, npc) in new[]
                 {
                     (QC.Zone.BastokMarkets, QC.Npc.BastokMarkets.Cleades),
                     (QC.Zone.BastokMines, QC.Npc.BastokMines.Rashid),
                     (QC.Zone.Metalworks, QC.Npc.Metalworks.Malduc),
                     (QC.Zone.PortBastok, QC.Npc.PortBastok.Argus),
                 })
        {
            Check($"c3 N4 {L}: section 1: {npc} at status 0: 1007 (progress); at 1 and 8: nothing",
                  "ProgressEvent(1007)|null|null",
                  $"{R(0, zone, npc, F(0))}|{R(0, zone, npc, F(1))}|{R(0, zone, npc, F(8))}");
        }
        Check($"c3 N4 {L}: section 1: Naji at status 0: 720 (progress); at 1 and 8: nothing",
              "ProgressEvent(720)|null|null",
              $"{R(0, QC.Zone.Metalworks, QC.Npc.Metalworks.Naji, F(0))}|" +
              $"{R(0, QC.Zone.Metalworks, QC.Npc.Metalworks.Naji, F(1))}|" +
              $"{R(0, QC.Zone.Metalworks, QC.Npc.Metalworks.Naji, F(8))}");
        var f720 = C2MHandler(d, 0, N4Fin, QC.Zone.Metalworks, id: 720);
        Check($"c3 N4 {L}: 720's finish: option 0 begins, takes the message, gives the New Fei'Yin Seal and sets " +
              "status 10; options 1 and 14: status 8 (declined)",
              $"Mission.Begin(),Player.DelKeyItem({msg}),Player.GiveKeyItem({seal}),Player.SetMissionStatus({b},10)|" +
              $"Player.SetMissionStatus({b},8)|Player.SetMissionStatus({b},8)",
              $"{C2MRun(f720, F(0, option: 0))}|{C2MRun(f720, F(0, option: 1))}|{C2MRun(f720, F(0, option: 14))}");
        N4BastokAccept(L, d, id, 14, $",Player.SetMissionStatus({b},9),Player.DelKeyItem({msg})");
        var naji = C2MHandler(d, 1, N4Trig, QC.Zone.Metalworks, QC.Npc.Metalworks.Naji);
        Check($"c3 N4 {L}: section 2: Naji at 9: 721; at 12 with the Burnt Seal: 722 (progress); at 12 without, 10, " +
              "11 and 13 with the seal: nothing",
              "ProgressEvent(721)|ProgressEvent(722)|null|null|null|null",
              $"{C3NReply(naji, F(9, id))}|{C3NReply(naji, N4With(F(12, id), QC.KeyItem.BurntSeal))}|" +
              $"{C3NReply(naji, F(12, id))}|{C3NReply(naji, F(10, id))}|" +
              $"{C3NReply(naji, N4With(F(11, id), QC.KeyItem.BurntSeal))}|" +
              $"{C3NReply(naji, N4With(F(13, id), QC.KeyItem.BurntSeal))}");
        Check($"c3 N4 {L}: 721's finish: the New Fei'Yin Seal, status 10",
              $"Player.GiveKeyItem({seal}),Player.SetMissionStatus({b},10)",
              C2MRun(C2MHandler(d, 1, N4Fin, QC.Zone.Metalworks, id: 721), F(9, id)));
        N4Complete(L, "722's finish", C2MHandler(d, 1, N4Fin, QC.Zone.Metalworks, id: 722), () => F(12, id),
                   QC.KeyItem.BurntSeal);
        N4FeiYin(L, d, MissionLog.Bastok, id);
    }

    static void RunC3N4TheRuinsOfFeiYin()
    {
        const string L = "ruins of fei'yin";
        var m = new QC.Missions.Sandoria.TheRuinsOfFeiYin();
        var d = m.Compile();
        int s = QC.MissionLog.Sandoria, id = QC.Mission.Sandoria.TheRuinsOfFeiYin, none = QC.Mission.Sandoria.None;
        ushort cd = QC.Zone.ChateauDoraguille;
        N4Header(L, m, d, QC.MissionKey.Sandoria.TheRuinsOfFeiYin,
                 "scripts/missions/sandoria/5_1_The_Ruins_of_FeiYin", "///400");
        Check($"c3 N4 {L}: three sections", "3", d.States.Count.ToString());
        N4Section1Rank5(L, d, MissionLog.Sandoria, id, none, QC.Mission.Sandoria.Magicite);
        N4Current(L, d, 1, MissionLog.Sandoria, id, none);
        Fake F(int status = 0, int current = -1, int option = 0, int rank = 5, params int[] done) =>
            N4Fake(MissionLog.Sandoria, id, current < 0 ? none : current, status, option: option, rank: rank,
                   done: done);
        string R(int state, ushort zone, string npc, Fake f) => C3NReply(C2MHandler(d, state, N4Trig, zone, npc), f);
        foreach (var (zone, npc, csid) in new[]
                 {
                     (QC.Zone.SouthernSanDoria, QC.Npc.SouthernSanDoria.Ambrotien, 62),
                     (QC.Zone.SouthernSanDoria, QC.Npc.SouthernSanDoria.Endracion, 61),
                     (QC.Zone.NorthernSanDoria, QC.Npc.NorthernSanDoria.Grilau, 52),
                 })
        {
            Check($"c3 N4 {L}: section 1: {npc} at status 0: {csid} (progress); at 1 and 8: nothing",
                  $"ProgressEvent({csid})|null|null",
                  $"{R(0, zone, npc, F(0))}|{R(0, zone, npc, F(1))}|{R(0, zone, npc, F(8))}");
        }
        N4SandoriaAccept(L, d, id, 14, five1: true);
        Check($"c3 N4 {L}: section 1: Halver: SHALL_BEGIN_DISCUSSION (message special) at any status (0, 8)",
              "MessageSpecial(233:SHALL_BEGIN_DISCUSSION)|MessageSpecial(233:SHALL_BEGIN_DISCUSSION)",
              $"{R(0, cd, QC.Npc.ChateauDoraguille.Halver, F(0))}|{R(0, cd, QC.Npc.ChateauDoraguille.Halver, F(8))}");
        var zin = C2MHandler(d, 0, N4Zone, cd, id: 1);
        int nsd = QC.Zone.NorthernSanDoria;
        Check($"c3 N4 {L}: section 1: Chateau zone-in from Northern San d'Oria at status 0: 509; at 8: nothing; from " +
              "another zone (Northern - 1, + 1, Southern) at 0: nothing; 509's finish sets status 8",
              $"Event(509)|null|null|null|null|Player.SetMissionStatus({s},8)",
              $"{C3NReply(zin, N4With(F(0), prev: nsd))}|{C3NReply(zin, N4With(F(8), prev: nsd))}|" +
              $"{C3NReply(zin, N4With(F(0), prev: nsd - 1))}|{C3NReply(zin, N4With(F(0), prev: nsd + 1))}|" +
              $"{C3NReply(zin, N4With(F(0), prev: QC.Zone.SouthernSanDoria))}|" +
              $"{C2MRun(C2MHandler(d, 0, N4Fin, cd, id: 509), F(0))}");
        var halver = C2MHandler(d, 1, N4Trig, cd, QC.Npc.ChateauDoraguille.Halver);
        Check($"c3 N4 {L}: section 2: Halver at 9: 533; at 10: FEI_YIN_NORTHEAST (message text); at 12 with the " +
              "Burnt Seal: 534 (progress); at 12 without, 11 and 13 with the seal, 8: nothing",
              "ProgressEvent(533)|MessageText(233:FEI_YIN_NORTHEAST)|ProgressEvent(534)|null|null|null|null",
              $"{C3NReply(halver, F(9, id))}|{C3NReply(halver, F(10, id))}|" +
              $"{C3NReply(halver, N4With(F(12, id), QC.KeyItem.BurntSeal))}|{C3NReply(halver, F(12, id))}|" +
              $"{C3NReply(halver, N4With(F(11, id), QC.KeyItem.BurntSeal))}|" +
              $"{C3NReply(halver, N4With(F(13, id), QC.KeyItem.BurntSeal))}|{C3NReply(halver, F(8, id))}");
        Check($"c3 N4 {L}: 533's finish: the New Fei'Yin Seal, status 10",
              $"Player.GiveKeyItem({QC.KeyItem.NewFeiyinSeal}),Player.SetMissionStatus({s},10)",
              C2MRun(C2MHandler(d, 1, N4Fin, cd, id: 533), F(9, id)));
        N4Complete(L, "534's finish", C2MHandler(d, 1, N4Fin, cd, id: 534), () => F(12, id), QC.KeyItem.BurntSeal);
        N4FeiYin(L, d, MissionLog.Sandoria, id);
        var s3 = d.States[2];
        int tsl = QC.Mission.Sandoria.TheShadowLord;
        Check($"c3 N4 {L}: section 3: completed, The Shadow Lord not current (none, another), rank 5; fails with The " +
              "Shadow Lord current, at rank 4 and 6, and before completion",
              "True True False False False False",
              $"{s3.IsMissionMatch(F(done: id))} {s3.IsMissionMatch(F(current: tsl + 1, done: id))} " +
              $"{s3.IsMissionMatch(F(current: tsl, done: id))} {s3.IsMissionMatch(F(rank: 4, done: id))} " +
              $"{s3.IsMissionMatch(F(rank: 6, done: id))} {s3.IsMissionMatch(F())}");
        Check($"c3 N4 {L}: section 3: _6h0 115, Curilla 545, Rahal 544 (events); Halver TIME_TO_REACH_DECISION " +
              "(message text)",
              "Event(115)|Event(545)|Event(544)|MessageText(233:TIME_TO_REACH_DECISION)",
              $"{R(2, cd, QC.Npc.ChateauDoraguille._6h0, F(done: id))}|" +
              $"{R(2, cd, QC.Npc.ChateauDoraguille.Curilla, F(done: id))}|" +
              $"{R(2, cd, QC.Npc.ChateauDoraguille.Rahal, F(done: id))}|" +
              $"{R(2, cd, QC.Npc.ChateauDoraguille.Halver, F(done: id))}");
    }

    static void RunC3N4TheFinalSeal()
    {
        const string L = "final seal";
        var m = new QC.Missions.Windurst.TheFinalSeal();
        var d = m.Compile();
        int w = QC.MissionLog.Windurst, id = QC.Mission.Windurst.TheFinalSeal, none = QC.Mission.Windurst.None;
        ushort ht = QC.Zone.HeavensTower;
        const int msg = QC.KeyItem.MessageToJeunoWindurst, seal = QC.KeyItem.NewFeiyinSeal;
        N4Header(L, m, d, QC.MissionKey.Windurst.TheFinalSeal, "scripts/missions/windurst/5_1_The_Final_Seal",
                 "///600");
        Check($"c3 N4 {L}: two sections", "2", d.States.Count.ToString());
        N4Section1Rank5(L, d, MissionLog.Windurst, id, none, QC.Mission.Windurst.Magicite);
        N4Current(L, d, 1, MissionLog.Windurst, id, none);
        Fake F(int status = 0, int current = -1, int option = 0) =>
            N4Fake(MissionLog.Windurst, id, current < 0 ? none : current, status, option: option);
        string R(int state, ushort zone, string npc, Fake f) => C3NReply(C2MHandler(d, state, N4Trig, zone, npc), f);
        Check($"c3 N4 {L}: section 1: Mokyokyo 232, Janshura-Rashura 163, Rakoh Buuma 197, Zokima-Rokima 150 " +
              "(progress) at any status (0, 8)",
              "ProgressEvent(232)|ProgressEvent(163)|ProgressEvent(197)|ProgressEvent(150)|ProgressEvent(150)",
              $"{R(0, QC.Zone.WindurstWaters, QC.Npc.WindurstWaters.Mokyokyo, F())}|" +
              $"{R(0, QC.Zone.PortWindurst, QC.Npc.PortWindurst.JanshuraRashura, F())}|" +
              $"{R(0, QC.Zone.WindurstWoods, QC.Npc.WindurstWoods.RakohBuuma, F())}|" +
              $"{R(0, QC.Zone.WindurstWalls, QC.Npc.WindurstWalls.ZokimaRokima, F())}|" +
              $"{R(0, QC.Zone.WindurstWalls, QC.Npc.WindurstWalls.ZokimaRokima, F(8))}");
        Check($"c3 N4 {L}: section 1: _6q2 with the Message to Jeuno: 166; without: 190 (progress)",
              "ProgressEvent(166)|ProgressEvent(190)",
              $"{R(0, ht, QC.Npc.HeavensTower._6q2, N4With(F(), msg))}|{R(0, ht, QC.Npc.HeavensTower._6q2, F())}");
        foreach (int csid in new[] { 166, 190 })
        {
            var h = C2MHandler(d, 0, N4Fin, ht, id: csid);
            Check($"c3 N4 {L}: {csid}'s finish: option 0 begins, status 10, YOU_ACCEPT_THE_MISSION, the New Fei'Yin " +
                  "Seal, then the message taken; options 1 and 15: only the message taken",
                  $"Mission.Begin(),Player.SetMissionStatus({w},10),Text.Special(242:YOU_ACCEPT_THE_MISSION)," +
                  $"Player.GiveKeyItem({seal}),Player.DelKeyItem({msg})|Player.DelKeyItem({msg})|" +
                  $"Player.DelKeyItem({msg})",
                  $"{C2MRun(h, F(option: 0))}|{C2MRun(h, F(option: 1))}|{C2MRun(h, F(option: 15))}");
        }
        var door = C2MHandler(d, 1, N4Trig, ht, QC.Npc.HeavensTower._6q2);
        Check($"c3 N4 {L}: section 2: _6q2 at 12 with the Burnt Seal: 192 (progress); at 12 without, 11 and 13 with " +
              "the seal: nothing",
              "ProgressEvent(192)|null|null|null",
              $"{C3NReply(door, N4With(F(12, id), QC.KeyItem.BurntSeal))}|{C3NReply(door, F(12, id))}|" +
              $"{C3NReply(door, N4With(F(11, id), QC.KeyItem.BurntSeal))}|" +
              $"{C3NReply(door, N4With(F(13, id), QC.KeyItem.BurntSeal))}");
        N4Complete(L, "192's finish", C2MHandler(d, 1, N4Fin, ht, id: 192), () => F(12, id), QC.KeyItem.BurntSeal);
        N4FeiYin(L, d, MissionLog.Windurst, id);
    }

    static void RunC3N4XarcabardLandOfTruths()
    {
        const string L = "xarcabard";
        var m = new QC.Missions.Bastok.XarcabardLandOfTruths();
        var d = m.Compile();
        int b = QC.MissionLog.Bastok, id = QC.Mission.Bastok.XarcabardLandOfTruths, none = QC.Mission.Bastok.None;
        ushort mw = QC.Zone.Metalworks;
        N4Header(L, m, d, QC.MissionKey.Bastok.XarcabardLandOfTruths,
                 "scripts/missions/bastok/5_2_Xarcabard_Land_of_Truths", "20000/6//");
        Check($"c3 N4 {L}: three sections", "3", d.States.Count.ToString());
        N4Section1Open(L, d, MissionLog.Bastok, id, none, QC.Mission.Bastok.DarknessRising);
        N4BastokAccept(L, d, id, 15);
        N4Current(L, d, 1, MissionLog.Bastok, id, none);
        Fake F(int status = 0, int current = -1, params int[] done)
        {
            var f = N4Fake(MissionLog.Bastok, id, current < 0 ? id : current, status, done: done);
            f.TextIds[QC.Text.BastokMarkets.ORIGINAL_MISSION_OFFSET.ToString()] = 7000;
            f.TextIds[QC.Text.BastokMines.ORIGINAL_MISSION_OFFSET.ToString()] = 7100;
            f.TextIds[QC.Text.Metalworks.ORIGINAL_MISSION_OFFSET.ToString()] = 7200;
            f.TextIds[QC.Text.PortBastok.ORIGINAL_MISSION_OFFSET.ToString()] = 7300;
            return f;
        }
        string R(int state, ushort zone, string npc, Fake f) => C3NReply(C2MHandler(d, state, N4Trig, zone, npc), f);
        Check($"c3 N4 {L}: section 2: Cleades, Rashid, Malduc and Argus: their zone's ORIGINAL_MISSION_OFFSET + 39 " +
              "(message special)",
              "MessageSpecial(7039)|MessageSpecial(7139)|MessageSpecial(7239)|MessageSpecial(7339)",
              $"{R(1, QC.Zone.BastokMarkets, QC.Npc.BastokMarkets.Cleades, F())}|" +
              $"{R(1, QC.Zone.BastokMines, QC.Npc.BastokMines.Rashid, F())}|" +
              $"{R(1, mw, QC.Npc.Metalworks.Malduc, F())}|{R(1, QC.Zone.PortBastok, QC.Npc.PortBastok.Argus, F())}");
        Check($"c3 N4 {L}: section 2: Karst at status 0: 602 (progress); at 1 and 2: nothing; 602's finish sets " +
              "status 2",
              $"ProgressEvent(602)|null|null|Player.SetMissionStatus({b},2)",
              $"{R(1, mw, QC.Npc.Metalworks.Karst, F(0))}|{R(1, mw, QC.Npc.Metalworks.Karst, F(1))}|" +
              $"{R(1, mw, QC.Npc.Metalworks.Karst, F(2))}|{C2MRun(C2MHandler(d, 1, N4Fin, mw, id: 602), F(0))}");
        Check($"c3 N4 {L}: section 2: _6ld with the Shadow Fragment: 603 (progress, any status: 4, 0); without: " +
              "nothing",
              "ProgressEvent(603)|ProgressEvent(603)|null",
              $"{R(1, mw, QC.Npc.Metalworks._6ld, N4With(F(4), QC.KeyItem.ShadowFragment))}|" +
              $"{R(1, mw, QC.Npc.Metalworks._6ld, N4With(F(0), QC.KeyItem.ShadowFragment))}|" +
              $"{R(1, mw, QC.Npc.Metalworks._6ld, F(4))}");
        N4Complete(L, "603's finish", C2MHandler(d, 1, N4Fin, mw, id: 603), () => F(4), QC.KeyItem.ShadowFragment);
        N4ThroneRoom(L, d, MissionLog.Bastok, id);
        var s3 = d.States[2];
        int rot = QC.Mission.Bastok.ReturnOfTheTalekeeper;
        Fake G(int nation = 1, int current = -1, params int[] done)
        {
            var f = F(0, current < 0 ? none : current, done);
            f.NationId = nation;
            return f;
        }
        Check($"c3 N4 {L}: section 3: a Bastokan with no Bastok mission current who has completed it and not Return " +
              "of the Talekeeper; fails for a San d'Orian and a Windurstian, with a mission current (Return of the " +
              "Talekeeper, this one), before completion, and once Return of the Talekeeper is completed",
              "True False False False False False False",
              $"{s3.IsMissionMatch(G(done: id))} {s3.IsMissionMatch(G(0, done: id))} " +
              $"{s3.IsMissionMatch(G(2, done: id))} {s3.IsMissionMatch(G(current: rot, done: id))} " +
              $"{s3.IsMissionMatch(G(current: id, done: id))} {s3.IsMissionMatch(G())} " +
              $"{s3.IsMissionMatch(G(done: new[] { id, rot }))}");
        Check($"c3 N4 {L}: section 3: Pavvke: 76 (event, importantOnce)",
              "Event(76):importantOnce()",
              R(2, QC.Zone.BastokMines, QC.Npc.BastokMines.Pavvke, G(done: id)));
    }

    static void RunC3N4TheShadowLord()
    {
        const string L = "shadow lord";
        var m = new QC.Missions.Sandoria.TheShadowLord();
        var d = m.Compile();
        int s = QC.MissionLog.Sandoria, id = QC.Mission.Sandoria.TheShadowLord, none = QC.Mission.Sandoria.None;
        ushort cd = QC.Zone.ChateauDoraguille;
        N4Header(L, m, d, QC.MissionKey.Sandoria.TheShadowLord, "scripts/missions/sandoria/5_2_The_Shadow_Lord",
                 "20000/6//");
        Check($"c3 N4 {L}: four sections", "4", d.States.Count.ToString());
        N4Section1Open(L, d, MissionLog.Sandoria, id, none, QC.Mission.Sandoria.TheRuinsOfFeiYin);
        N4SandoriaAccept(L, d, id, 15, five1: false);
        N4Current(L, d, 1, MissionLog.Sandoria, id, none);
        Fake F(int status = 0, int current = -1, int hall = 0, params int[] done)
        {
            var f = N4Fake(MissionLog.Sandoria, id, current < 0 ? id : current, status, done: done);
            if (hall != 0) f.MissionVars[$"{s}/{id}/hallEvent"] = hall;
            return f;
        }
        string R(int state, string npc, Fake f) => C3NReply(C2MHandler(d, state, N4Trig, (ushort)cd, npc), f);
        string H = QC.Npc.ChateauDoraguille.Halver;
        Check($"c3 N4 {L}: section 2: Halver at 0: 546 (progress); 1: PRINCE_TRION_CHAMBERS, 2: WHAT_TRION_WILL_SAY " +
              "(message text); 4 with the Shadow Fragment: 548 (progress); 4 without, 3, 5 with: nothing",
              "ProgressEvent(546)|MessageText(233:PRINCE_TRION_CHAMBERS)|MessageText(233:WHAT_TRION_WILL_SAY)|" +
              "ProgressEvent(548)|null|null|null",
              $"{R(1, H, F(0))}|{R(1, H, F(1))}|{R(1, H, F(2))}|" +
              $"{R(1, H, N4With(F(4), QC.KeyItem.ShadowFragment))}|{R(1, H, F(4))}|" +
              $"{R(1, H, N4With(F(3), QC.KeyItem.ShadowFragment))}|{R(1, H, N4With(F(5), QC.KeyItem.ShadowFragment))}");
        Check($"c3 N4 {L}: section 2: _6h0 at 1: 547 (progress); at 0 and 2: nothing; 546's finish: status 1; 547's: " +
              "status 2",
              $"ProgressEvent(547)|null|null|Player.SetMissionStatus({s},1)|Player.SetMissionStatus({s},2)",
              $"{R(1, QC.Npc.ChateauDoraguille._6h0, F(1))}|{R(1, QC.Npc.ChateauDoraguille._6h0, F(0))}|" +
              $"{R(1, QC.Npc.ChateauDoraguille._6h0, F(2))}|" +
              $"{C2MRun(C2MHandler(d, 1, N4Fin, (ushort)cd, id: 546), F(0))}|" +
              $"{C2MRun(C2MHandler(d, 1, N4Fin, (ushort)cd, id: 547), F(1))}");
        N4Complete(L, "548's finish", C2MHandler(d, 1, N4Fin, (ushort)cd, id: 548), () => F(4),
                   QC.KeyItem.ShadowFragment, ",Mission.SetVar(hallEvent,1)");
        N4ThroneRoom(L, d, MissionLog.Sandoria, id);
        var s3 = d.States[2];
        Check($"c3 N4 {L}: section 3 holds with hallEvent 1 only (any mission: none, this one); 0 and 2: no",
              "True True False False",
              $"{s3.IsMissionMatch(F(current: none, hall: 1))} {s3.IsMissionMatch(F(hall: 1))} " +
              $"{s3.IsMissionMatch(F(current: none))} {s3.IsMissionMatch(F(current: none, hall: 2))}");
        Check($"c3 N4 {L}: section 3: _6h4 61 (progress); Arsha 85, Chupaile 86 (events); Halver HIS_MAJESTY_AWAITS " +
              "(message text); 61's finish: hallEvent 0",
              "ProgressEvent(61)|Event(85)|Event(86)|MessageText(233:HIS_MAJESTY_AWAITS)|Mission.SetVar(hallEvent,0)",
              $"{R(2, QC.Npc.ChateauDoraguille._6h4, F(hall: 1))}|{R(2, QC.Npc.ChateauDoraguille.Arsha, F(hall: 1))}|" +
              $"{R(2, QC.Npc.ChateauDoraguille.Chupaile, F(hall: 1))}|{R(2, H, F(hall: 1))}|" +
              $"{C2MRun(C2MHandler(d, 2, N4Fin, (ushort)cd, id: 61), F(hall: 1))}");
        var s4 = d.States[3];
        int llw = QC.Mission.Sandoria.LeautesLastWishes;
        Check($"c3 N4 {L}: section 4: current at status 4 and 5; or none with it completed and not Leaute's Last " +
              "Wishes; fails current at 3, none before completion, none with Leaute's Last Wishes completed, " +
              "another mission current with it completed",
              "True True True False False False False",
              $"{s4.IsMissionMatch(F(4))} {s4.IsMissionMatch(F(5))} {s4.IsMissionMatch(F(current: none, done: id))} " +
              $"{s4.IsMissionMatch(F(3))} {s4.IsMissionMatch(F(current: none))} " +
              $"{s4.IsMissionMatch(F(current: none, done: new[] { id, llw }))} " +
              $"{s4.IsMissionMatch(F(current: llw, done: id))}");
        Check($"c3 N4 {L}: section 4: Halver with hallEvent 0: KNEW_YOU_WERE_THE_ONE (message text); " +
              "with 1: nothing; " +
              "Aramaviont 12, Curilla 56, Milchupain 33, Rahal 77 (events)",
              "MessageText(233:KNEW_YOU_WERE_THE_ONE)|null|Event(12)|Event(56)|Event(33)|Event(77)",
              $"{R(3, H, F(4))}|{R(3, H, F(4, hall: 1))}|{R(3, QC.Npc.ChateauDoraguille.Aramaviont, F(4))}|" +
              $"{R(3, QC.Npc.ChateauDoraguille.Curilla, F(4))}|{R(3, QC.Npc.ChateauDoraguille.Milchupain, F(4))}|" +
              $"{R(3, QC.Npc.ChateauDoraguille.Rahal, F(4))}");
    }

    static void RunC3N4TheShadowAwaits()
    {
        const string L = "shadow awaits";
        var m = new QC.Missions.Windurst.TheShadowAwaits();
        var d = m.Compile();
        int w = QC.MissionLog.Windurst, id = QC.Mission.Windurst.TheShadowAwaits, none = QC.Mission.Windurst.None;
        ushort ht = QC.Zone.HeavensTower;
        const int scs = QC.KeyItem.StarCrestedSummons1, frag = QC.KeyItem.ShadowFragment;
        N4Header(L, m, d, QC.MissionKey.Windurst.TheShadowAwaits, "scripts/missions/windurst/5_2_The_Shadow_Awaits",
                 "20000/6//");
        Check($"c3 N4 {L}: two sections", "2", d.States.Count.ToString());
        N4Section1Open(L, d, MissionLog.Windurst, id, none, QC.Mission.Windurst.TheFinalSeal);
        foreach (var (zone, csid, text) in new[]
                 {
                     (QC.Zone.PortWindurst, 78, QC.Text.PortWindurst.YOU_ACCEPT_THE_MISSION),
                     (QC.Zone.WindurstWalls, 93, QC.Text.WindurstWalls.YOU_ACCEPT_THE_MISSION),
                     (QC.Zone.WindurstWaters, 111, QC.Text.WindurstWaters.YOU_ACCEPT_THE_MISSION),
                     (QC.Zone.WindurstWoods, 114, QC.Text.WindurstWoods.YOU_ACCEPT_THE_MISSION),
                 })
        {
            var h = C2MHandler(d, 0, N4Fin, zone, id: csid);
            Fake A(int option) => N4Fake(MissionLog.Windurst, id, none, option: option);
            Check($"c3 N4 {L}: zone {zone}'s {csid} finish: option 15 begins with its zone's YOU_ACCEPT_THE_MISSION " +
                  "and the Star-Crested Summons; options 14, 16 and 0 do nothing",
                  $"Mission.Begin(),Text.Special({text}),Player.GiveKeyItem({scs})|||",
                  $"{C2MRun(h, A(15))}|{C2MRun(h, A(14))}|{C2MRun(h, A(16))}|{C2MRun(h, A(0))}");
        }
        N4Current(L, d, 1, MissionLog.Windurst, id, none);
        Fake F(int status = 0, int ki = 0, int ret = 0)
        {
            var f = N4With(N4Fake(MissionLog.Windurst, id, id, status), ki);
            if (ret != 0) f.MissionVars[$"{w}/{id}/ReturnCS"] = ret;
            return f;
        }
        Fake Both(int status)
        {
            var f = F(status, scs);
            f.KeyItems[frag] = true;
            return f;
        }
        var zin = C2MHandler(d, 1, N4Zone, ht, id: 1);
        Check($"c3 N4 {L}: Heavens Tower zone-in at 4 with ReturnCS 0 and the fragment: 215 (text table -1, flags " +
              "0x13); at 4 without the fragment, with ReturnCS 1 and 2, at 3 and 5 with the fragment: nothing",
              "Event(215,{text_table=-1,flags=19})|null|null|null|null|null",
              $"{C3NReply(zin, F(4, frag))}|{C3NReply(zin, F(4))}|{C3NReply(zin, F(4, frag, 1))}|" +
              $"{C3NReply(zin, F(4, frag, 2))}|{C3NReply(zin, F(3, frag))}|{C3NReply(zin, F(5, frag))}");
        string R(string npc, Fake f) => C3NReply(C2MHandler(d, 1, N4Trig, (ushort)ht, npc), f);
        Check($"c3 N4 {L}: _6q2 with the summons: 214; else with the fragment: 216 (progress); both: 214; neither: " +
              "nothing (any status)",
              "ProgressEvent(214)|ProgressEvent(216)|ProgressEvent(214)|null",
              $"{R(QC.Npc.HeavensTower._6q2, F(0, scs))}|{R(QC.Npc.HeavensTower._6q2, F(4, frag))}|" +
              $"{R(QC.Npc.HeavensTower._6q2, Both(4))}|{R(QC.Npc.HeavensTower._6q2, F(2))}");
        Check($"c3 N4 {L}: Zubaba with the summons: 157; else with the fragment: 194 (events); both: 157; neither: " +
              "nothing",
              "Event(157)|Event(194)|Event(157)|null",
              $"{R(QC.Npc.HeavensTower.Zubaba, F(0, scs))}|{R(QC.Npc.HeavensTower.Zubaba, F(4, frag))}|" +
              $"{R(QC.Npc.HeavensTower.Zubaba, Both(4))}|{R(QC.Npc.HeavensTower.Zubaba, F(2))}");
        Check($"c3 N4 {L}: 214's finish: status 2, the Star-Ordained Warrior title, the summons taken; 215's: " +
              "ReturnCS 1",
              $"Player.SetMissionStatus({w},2),Player.AddTitle({QC.Title.StarOrdainedWarrior})," +
              $"Player.DelKeyItem({scs})|Mission.SetVar(ReturnCS,1)",
              $"{C2MRun(C2MHandler(d, 1, N4Fin, (ushort)ht, id: 214), F(0, scs))}|" +
              $"{C2MRun(C2MHandler(d, 1, N4Fin, (ushort)ht, id: 215), F(4, frag))}");
        N4Complete(L, "216's finish", C2MHandler(d, 1, N4Fin, (ushort)ht, id: 216), () => F(4, frag), frag);
        N4ThroneRoom(L, d, MissionLog.Windurst, id);
    }
}
