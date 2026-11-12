// XI.Verify.Quests -- quest rework C3 N4: the three Magicite missions (Bastok, San d'Oria, Windurst 4-1) on C#, one
// fake check per arm no packet shows.
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    const int N4Opti = QC.KeyItem.MagiciteOptistone, N4Aura = QC.KeyItem.MagiciteAurastone;
    const int N4Ora = QC.KeyItem.MagiciteOrastone, N4Torch = QC.KeyItem.YagudoTorch;
    const int N4Pass = QC.KeyItem.AirshipPass, N4Bell = QC.KeyItem.SilverBell, N4Letter = QC.KeyItem.LetterToAldo;
    const int N4Permit = QC.KeyItem.ArchducalAudiencePermit;

    static void RunC3N4Magicite()
    {
        RunC3N4MagiciteBastok();
        RunC3N4MagiciteSandoria();
        RunC3N4MagiciteWindurst();
    }

    static Fake N4Mag(MissionLog log, int id, int status, int option = 0, int var = 0, params int[] kis)
    {
        var f = N4Fake(log, id, id, status, option: option);
        if (var != 0) f.MissionVars[$"{(int)log}/{id}/Option"] = var;
        foreach (int ki in kis) f.KeyItems[ki] = true;
        return f;
    }

    // the pre-mission state: no mission current, a char of the nation at rank 4
    static void N4MagSection1(string label, MissionScriptDefinition d, MissionLog log, int id, int none, int other)
    {
        int l = (int)log, foreign = (l + 1) % 3;
        var s = d.States[0];
        Check($"c3 N4 {label}: the first section holds with no mission current, a char of nation {l} at rank 4 " +
              $"(completed too); fails for nation {foreign}, with a mission current (this one, another), at rank 3 " +
              "and 5",
              "True True False False False False False",
              $"{s.IsMissionMatch(N4Fake(log, id, none, rank: 4))} " +
              $"{s.IsMissionMatch(N4Fake(log, id, none, rank: 4, done: id))} " +
              $"{s.IsMissionMatch(N4Fake(log, id, none, nation: foreign, rank: 4))} " +
              $"{s.IsMissionMatch(N4Fake(log, id, id, rank: 4))} {s.IsMissionMatch(N4Fake(log, id, other, rank: 4))} " +
              $"{s.IsMissionMatch(N4Fake(log, id, none, rank: 3))} {s.IsMissionMatch(N4Fake(log, id, none, rank: 5))}");
    }

    // the accept finish (129, 130, 131): a cancelled event does nothing; any other option begins (status 1 where
    // given), says YOU_ACCEPT_THE_MISSION (message text, mode 6) and gives the permit
    static void N4MagAccept(string label, MissionScriptDefinition d, MissionLog log, int id, int csid, bool status1)
    {
        int l = (int)log;
        var h = C2MHandler(d, 0, N4Fin, QC.Zone.RuludeGardens, id: csid);
        Fake F(int option) => N4Fake(log, id, l == 1 ? QC.Mission.Bastok.None : 65535, option: option, rank: 4);
        string begun = "Mission.Begin()" + (status1 ? $",Player.SetMissionStatus({l},1)" : "") +
                       $",Text.MessageText(243:YOU_ACCEPT_THE_MISSION,False,6),Player.GiveKeyItem({N4Permit})";
        Check($"c3 N4 {label}: {csid}'s finish: options 0, 1 and 2 begin" + (status1 ? " (status 1)" : "") +
              ", YOU_ACCEPT_THE_MISSION (message text, mode 6), the permit; the cancelled option (1 << 30): nothing",
              $"{begun}|{begun}|{begun}|",
              $"{C2MRun(h, F(0))}|{C2MRun(h, F(1))}|{C2MRun(h, F(2))}|{C2MRun(h, F(QC.Utils.EventCancelledOption))}");
    }

    // the key item hunt section (the same in the three files but where given)
    static void N4MagHunt(string label, MissionScriptDefinition d, MissionLog log, int id, int state, int status,
                          string missing6r9, string payaOther, string monasticBoth, string qulunBoth)
    {
        int l = (int)log;
        Fake F(int var = 0, params int[] kis) => N4Mag(log, id, status, var: var, kis: kis);
        string R(ushort zone, string npc, Fake f) => C3NReply(C2MHandler(d, state, N4Trig, zone, npc), f);
        string Run(ushort zone, int csid, Fake f) => C2MRun(C2MHandler(d, state, N4Fin, zone, id: csid), f);
        ushort rg = QC.Zone.RuludeGardens, lj = QC.Zone.LowerJeuno, uj = QC.Zone.UpperJeuno;
        ushort ar = QC.Zone.AltarRoom, mc = QC.Zone.MonasticCavern, qd = QC.Zone.QulunDome;
        string six = QC.Npc.RuludeGardens._6r9;
        Check($"c3 N4 {label}: _6r9 with the three stones: 60 (0, 0); with the airship pass too: 60 (1, 0); missing " +
              $"one (each) or all: {missing6r9}",
              $"ProgressEvent(60,0,0)|ProgressEvent(60,1,0)|{missing6r9}|{missing6r9}|{missing6r9}|{missing6r9}",
              $"{R((ushort)rg, six, F(0, N4Opti, N4Aura, N4Ora))}|" +
              $"{R((ushort)rg, six, F(0, N4Opti, N4Aura, N4Ora, N4Pass))}|" +
              $"{R((ushort)rg, six, F(0, N4Aura, N4Ora))}|{R((ushort)rg, six, F(0, N4Opti, N4Ora))}|" +
              $"{R((ushort)rg, six, F(0, N4Opti, N4Aura))}|{R((ushort)rg, six, F())}");
        string done60 = $"Player.DelKeyItem({N4Opti}),Player.DelKeyItem({N4Aura}),Player.DelKeyItem({N4Ora}),";
        string tail60 = $"Player.AddTitle({QC.Title.HaveWingsWillFly}),Player.SetMissionStatus({l},{status + 1})";
        Check($"c3 N4 {label}: 60's finish: the three stones taken; with the airship pass 20000 gil, without it the " +
              $"pass; the title Have Wings, Will Fly; status {status + 1}",
              $"{done60}Player.GiveCurrency(gil,20000),{tail60}|{done60}Player.GiveKeyItem({N4Pass}),{tail60}",
              $"{Run((ushort)rg, 60, F(0, N4Pass))}|{Run((ushort)rg, 60, F())}");
        string aldo = QC.Npc.LowerJeuno.Aldo;
        Check($"c3 N4 {label}: Aldo with no stone: 161; with any one (each): 183 (events)",
              "Event(161)|Event(183)|Event(183)|Event(183)",
              $"{R((ushort)lj, aldo, F())}|{R((ushort)lj, aldo, F(0, N4Opti))}|{R((ushort)lj, aldo, F(0, N4Aura))}|" +
              $"{R((ushort)lj, aldo, F(0, N4Ora))}");
        string mv = QC.Npc.LowerJeuno.Muckvix;
        Check($"c3 N4 {label}: Muckvix without the torch: Option 1: 184 (progress), 0 and 2: 80; with the torch: " +
              "Option 2: 81, 0 and 1: 79 (events)",
              "ProgressEvent(184)|Event(80)|Event(80)|Event(81)|Event(79)|Event(79)",
              $"{R((ushort)lj, mv, F(1))}|{R((ushort)lj, mv, F(0))}|{R((ushort)lj, mv, F(2))}|" +
              $"{R((ushort)lj, mv, F(2, N4Torch))}|{R((ushort)lj, mv, F(0, N4Torch))}|" +
              $"{R((ushort)lj, mv, F(1, N4Torch))}");
        Check($"c3 N4 {label}: 184's finish: the torch, Option 2",
              $"Player.GiveKeyItem({N4Torch}),Mission.SetVar(Option,2)", Run((ushort)lj, 184, F(1)));
        string gb = QC.Npc.UpperJeuno.Geebeh;
        Check($"c3 N4 {label}: Geebeh without the torch at Option 1: WITHER_AND_DIE (message text); at 0 and 2, and " +
              "with the torch at 1: nothing",
              "MessageText(244:WITHER_AND_DIE)|null|null|null",
              $"{R((ushort)uj, gb, F(1))}|{R((ushort)uj, gb, F(0))}|{R((ushort)uj, gb, F(2))}|" +
              $"{R((ushort)uj, gb, F(1, N4Torch))}");
        string ps = QC.Npc.UpperJeuno.PayaSabya;
        Check($"c3 N4 {label}: Paya-Sabya without the torch: Option 1: 23 (event), 0: 80 (progress), 2: {payaOther}; " +
              "with the torch: nothing",
              $"Event(23)|ProgressEvent(80)|{payaOther}|null",
              $"{R((ushort)uj, ps, F(1))}|{R((ushort)uj, ps, F(0))}|{R((ushort)uj, ps, F(2))}|" +
              $"{R((ushort)uj, ps, F(0, N4Torch))}");
        Check($"c3 N4 {label}: 80's finish: Option 1", "Mission.SetVar(Option,1)", Run((ushort)uj, 80, F()));
        string mag = QC.Npc.AltarRoom.Magicite;
        Check($"c3 N4 {label}: Altar Room's Magicite: the Orastone held: nothing; " +
              $"the other two: 44 ({ar}, 3); one or " +
              $"none of them: 44 ({ar})",
              $"null|ProgressEvent(44,{ar},3)|ProgressEvent(44,{ar})|ProgressEvent(44,{ar})|ProgressEvent(44,{ar})",
              $"{R((ushort)ar, mag, F(0, N4Ora))}|{R((ushort)ar, mag, F(0, N4Opti, N4Aura))}|" +
              $"{R((ushort)ar, mag, F(0, N4Opti))}|{R((ushort)ar, mag, F(0, N4Aura))}|{R((ushort)ar, mag, F())}");
        var zin = C2MHandler(d, state, N4Zone, (ushort)ar, id: 1);
        var f10000 = C2MHandler(d, state, N4Fin, (ushort)ar, id: 10000);
        Fake O(int var, int option) => N4Mag(log, id, status, option, var);
        Check($"c3 N4 {label}: Altar Room zone-in at Option 2: 10000; 0 and 1: nothing; 10000's finish: Option 0, " +
              "but not when cancelled; 44's finish: the Orastone",
              $"Event(10000)|null|null|Mission.SetVar(Option,0)||Player.GiveKeyItem({N4Ora})",
              $"{C3NReply(zin, F(2))}|{C3NReply(zin, F(0))}|{C3NReply(zin, F(1))}|{C2MRun(f10000, O(2, 0))}|" +
              $"{C2MRun(f10000, O(2, QC.Utils.EventCancelledOption))}|{Run((ushort)ar, 44, F())}");
        string mcm = QC.Npc.MonasticCavern.Magicite;
        Check($"c3 N4 {label}: Monastic Cavern's Magicite: the Optistone held: nothing; the other two: " +
              $"{monasticBoth}; one or none: 0 ({mc}); 0's finish: the Optistone",
              $"null|{monasticBoth}|ProgressEvent(0,{mc})|ProgressEvent(0,{mc})|ProgressEvent(0,{mc})|" +
              $"Player.GiveKeyItem({N4Opti})",
              $"{R((ushort)mc, mcm, F(0, N4Opti))}|{R((ushort)mc, mcm, F(0, N4Aura, N4Ora))}|" +
              $"{R((ushort)mc, mcm, F(0, N4Aura))}|{R((ushort)mc, mcm, F(0, N4Ora))}|{R((ushort)mc, mcm, F())}|" +
              $"{Run((ushort)mc, 0, F())}");
        string qdm = QC.Npc.QulunDome.Magicite;
        Check($"c3 N4 {label}: Qulun Dome's Magicite: the Aurastone held: nothing; the other two: {qulunBoth}; one " +
              "or none: 0 (0, 46, 47); 0's finish: the Aurastone",
              $"null|{qulunBoth}|ProgressEvent(0,0,46,47)|ProgressEvent(0,0,46,47)|ProgressEvent(0,0,46,47)|" +
              $"Player.GiveKeyItem({N4Aura})",
              $"{R((ushort)qd, qdm, F(0, N4Aura))}|{R((ushort)qd, qdm, F(0, N4Opti, N4Ora))}|" +
              $"{R((ushort)qd, qdm, F(0, N4Opti))}|{R((ushort)qd, qdm, F(0, N4Ora))}|{R((ushort)qd, qdm, F())}|" +
              $"{Run((ushort)qd, 0, F())}");
    }

    // a section's status test: holds at <at> only (current), not at the neighbours nor without the mission
    static void N4MagStatus(string label, MissionScriptDefinition d, MissionLog log, int id, int state, int none,
                            int[] holds, int[] fails)
    {
        var s = d.States[state];
        Check($"c3 N4 {label}: section {state + 1} holds while current at status {string.Join(", ", holds)}; not at " +
              $"{string.Join(", ", fails)}, nor at {holds[0]} with no mission current",
              string.Join(" ", holds.Select(_ => "True").Concat(fails.Select(_ => "False"))) + " False",
              string.Join(" ", holds.Concat(fails).Select(st => s.IsMissionMatch(N4Mag(log, id, st)).ToString())) +
              " " + s.IsMissionMatch(N4Fake(log, id, none, holds[0])));
    }

    static void RunC3N4MagiciteBastok()
    {
        const string L = "magicite bastok";
        var m = new QC.Missions.Bastok.Magicite();
        var d = m.Compile();
        int b = QC.MissionLog.Bastok, id = QC.Mission.Bastok.Magicite, none = QC.Mission.Bastok.None;
        ushort rg = QC.Zone.RuludeGardens;
        Check($"c3 N4 {L}: clean, its key and LegacyPath, 10000 gil, rank 5, the Message to Jeuno; four sections",
              $"|{QC.MissionKey.Bastok.Magicite}|scripts/missions/bastok/4_1_Magicite|10000/5/" +
              $"{QC.KeyItem.MessageToJeunoBastok}/|4",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.Reward.Gil}/" +
              $"{d.Reward.Rank}/{d.Reward.KeyItem}/{d.Reward.RankPoints}|{d.States.Count}");
        N4MagSection1(L, d, MissionLog.Bastok, id, none, QC.Mission.Bastok.Jeuno);
        Fake P(bool points, bool permit = false)
        {
            var f = N4Fake(MissionLog.Bastok, id, none, rank: 4);
            if (points) f.RankPointsMet.Add(id);
            if (permit) f.KeyItems[N4Permit] = true;
            return f;
        }
        string R0(string npc, Fake f) => C3NReply(C2MHandler(d, 0, N4Trig, (ushort)rg, npc), f);
        Check($"c3 N4 {L}: _6r2 with the rank points: 129 (0), with the permit too 129 (1) (progress); without the " +
              "points: nothing; Goggehn with the points: 0, without: 4 (events)",
              "ProgressEvent(129,0)|ProgressEvent(129,1)|null|Event(0)|Event(4)",
              $"{R0(QC.Npc.RuludeGardens._6r2, P(true))}|{R0(QC.Npc.RuludeGardens._6r2, P(true, true))}|" +
              $"{R0(QC.Npc.RuludeGardens._6r2, P(false, true))}|{R0(QC.Npc.RuludeGardens.Goggehn, P(true))}|" +
              $"{R0(QC.Npc.RuludeGardens.Goggehn, P(false))}");
        N4MagAccept(L, d, MissionLog.Bastok, id, 129, false);
        N4MagStatus(L, d, MissionLog.Bastok, id, 1, none, new[] { 0, 1 }, new[] { 2, 3 });
        Fake F(int status, params int[] kis) => N4Mag(MissionLog.Bastok, id, status, kis: kis);
        string R1(ushort zone, string npc, Fake f) => C3NReply(C2MHandler(d, 1, N4Trig, zone, npc), f);
        Check($"c3 N4 {L}: section 2: _6r9 at 0: 128 (progress), at 1: 138 (1) (event); Goggehn at 0: 132, at 1: 135",
              "ProgressEvent(128)|Event(138,1)|Event(132)|Event(135)",
              $"{R1((ushort)rg, QC.Npc.RuludeGardens._6r9, F(0))}|{R1((ushort)rg, QC.Npc.RuludeGardens._6r9, F(1))}|" +
              $"{R1((ushort)rg, QC.Npc.RuludeGardens.Goggehn, F(0))}|" +
              $"{R1((ushort)rg, QC.Npc.RuludeGardens.Goggehn, F(1))}");
        Check($"c3 N4 {L}: 128's finish: status 1, the letter to Aldo",
              $"Player.SetMissionStatus({b},1),Player.GiveKeyItem({N4Letter})",
              C2MRun(C2MHandler(d, 1, N4Fin, (ushort)rg, id: 128), F(0)));
        string aldo = QC.Npc.LowerJeuno.Aldo;
        ushort lj = QC.Zone.LowerJeuno;
        Check($"c3 N4 {L}: section 2: Aldo at 1 with the Silver Bell: 152 (1); without: 152 (0, 1) (progress); at 0: " +
              "nothing",
              "ProgressEvent(152,1)|ProgressEvent(152,0,1)|null",
              $"{R1((ushort)lj, aldo, F(1, N4Bell))}|{R1((ushort)lj, aldo, F(1))}|" +
              $"{R1((ushort)lj, aldo, F(0, N4Bell))}");
        var f152 = C2MHandler(d, 1, N4Fin, (ushort)lj, id: 152);
        Check($"c3 N4 {L}: 152's finish: status 2, the letter taken, the Silver Bell unless held",
              $"Player.SetMissionStatus({b},2),Player.DelKeyItem({N4Letter}),Player.GiveKeyItem({N4Bell})|" +
              $"Player.SetMissionStatus({b},2),Player.DelKeyItem({N4Letter})",
              $"{C2MRun(f152, F(1))}|{C2MRun(f152, F(1, N4Bell))}");
        N4MagStatus(L, d, MissionLog.Bastok, id, 2, none, new[] { 2 }, new[] { 1, 3 });
        N4MagHunt(L, d, MissionLog.Bastok, id, 2, 2, "Event(138,1)", "null", "ProgressEvent(0,1,1,1,1,1,1,1,1)",
                  "ProgressEvent(0,1,46,47)");
        Check($"c3 N4 {L}: section 3: Goggehn: 135 (event)", "Event(135)",
              C3NReply(C2MHandler(d, 2, N4Trig, (ushort)rg, QC.Npc.RuludeGardens.Goggehn), F(2)));
        N4MagStatus(L, d, MissionLog.Bastok, id, 3, none, new[] { 3 }, new[] { 2, 4 });
        Check($"c3 N4 {L}: section 4: _6r2 THE_CONSULATE_IS_AWAY (message special), Goggehn 35 (progress), Aldo 183 " +
              "(event); 35's finish completes",
              "MessageSpecial(243:THE_CONSULATE_IS_AWAY)|ProgressEvent(35)|Event(183)|Mission.Complete()",
              $"{C3NReply(C2MHandler(d, 3, N4Trig, (ushort)rg, QC.Npc.RuludeGardens._6r2), F(3))}|" +
              $"{C3NReply(C2MHandler(d, 3, N4Trig, (ushort)rg, QC.Npc.RuludeGardens.Goggehn), F(3))}|" +
              $"{C3NReply(C2MHandler(d, 3, N4Trig, (ushort)lj, aldo), F(3))}|" +
              $"{C2MRun(C2MHandler(d, 3, N4Fin, (ushort)rg, id: 35), F(3))}");
    }

    static void RunC3N4MagiciteSandoria()
    {
        const string L = "magicite sandoria";
        var m = new QC.Missions.Sandoria.Magicite();
        var d = m.Compile();
        int s = QC.MissionLog.Sandoria, id = QC.Mission.Sandoria.Magicite, none = QC.Mission.Sandoria.None;
        ushort rg = QC.Zone.RuludeGardens, lj = QC.Zone.LowerJeuno;
        Check($"c3 N4 {L}: clean, its key and LegacyPath, 10000 gil, rank 5, the Message to Jeuno; four sections",
              $"|{QC.MissionKey.Sandoria.Magicite}|scripts/missions/sandoria/4_1_Magicite|10000/5/" +
              $"{QC.KeyItem.MessageToJeunoSandoria}/|4",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.Reward.Gil}/" +
              $"{d.Reward.Rank}/{d.Reward.KeyItem}/{d.Reward.RankPoints}|{d.States.Count}");
        N4MagSection1(L, d, MissionLog.Sandoria, id, none, QC.Mission.Sandoria.AppointmentToJeuno);
        Fake P(bool points, bool permit = false)
        {
            var f = N4Fake(MissionLog.Sandoria, id, none, rank: 4);
            if (points) f.RankPointsMet.Add(id);
            if (permit) f.KeyItems[N4Permit] = true;
            return f;
        }
        string R0(string npc, Fake f) => C3NReply(C2MHandler(d, 0, N4Trig, (ushort)rg, npc), f);
        Check($"c3 N4 {L}: Nelcabrit with the rank points: 45 (progress), without: 49 (event); _6r5 with the points: " +
              "130 (0), with the permit 130 (1) (progress); without the points: nothing",
              "ProgressEvent(45)|Event(49)|ProgressEvent(130,0)|ProgressEvent(130,1)|null",
              $"{R0(QC.Npc.RuludeGardens.Nelcabrit, P(true))}|{R0(QC.Npc.RuludeGardens.Nelcabrit, P(false))}|" +
              $"{R0(QC.Npc.RuludeGardens._6r5, P(true))}|{R0(QC.Npc.RuludeGardens._6r5, P(true, true))}|" +
              $"{R0(QC.Npc.RuludeGardens._6r5, P(false, true))}");
        N4MagAccept(L, d, MissionLog.Sandoria, id, 130, true);
        N4MagStatus(L, d, MissionLog.Sandoria, id, 1, none, new[] { 0, 1, 2 }, new[] { 3, 4 });
        Fake F(int status, params int[] kis) => N4Mag(MissionLog.Sandoria, id, status, kis: kis);
        string R1(ushort zone, string npc, Fake f) => C3NReply(C2MHandler(d, 1, N4Trig, zone, npc), f);
        Check($"c3 N4 {L}: section 2: _6r9 at 1: 128 (progress), at 0 and 2: nothing; Nelcabrit at 1: 133, at 0 and " +
              "2: 136 (events)",
              "ProgressEvent(128)|null|null|Event(133)|Event(136)|Event(136)",
              $"{R1((ushort)rg, QC.Npc.RuludeGardens._6r9, F(1))}|{R1((ushort)rg, QC.Npc.RuludeGardens._6r9, F(0))}|" +
              $"{R1((ushort)rg, QC.Npc.RuludeGardens._6r9, F(2))}|" +
              $"{R1((ushort)rg, QC.Npc.RuludeGardens.Nelcabrit, F(1))}|" +
              $"{R1((ushort)rg, QC.Npc.RuludeGardens.Nelcabrit, F(0))}|" +
              $"{R1((ushort)rg, QC.Npc.RuludeGardens.Nelcabrit, F(2))}");
        Check($"c3 N4 {L}: 128's finish: status 2, the letter to Aldo",
              $"Player.SetMissionStatus({s},2),Player.GiveKeyItem({N4Letter})",
              C2MRun(C2MHandler(d, 1, N4Fin, (ushort)rg, id: 128), F(1)));
        string aldo = QC.Npc.LowerJeuno.Aldo;
        Check($"c3 N4 {L}: section 2: Aldo at 2 with the Silver Bell: 152 (1); without: 152 (0, 1) (progress); at 1: " +
              "nothing",
              "ProgressEvent(152,1)|ProgressEvent(152,0,1)|null",
              $"{R1((ushort)lj, aldo, F(2, N4Bell))}|{R1((ushort)lj, aldo, F(2))}|" +
              $"{R1((ushort)lj, aldo, F(1, N4Bell))}");
        var f152 = C2MHandler(d, 1, N4Fin, (ushort)lj, id: 152);
        Check($"c3 N4 {L}: 152's finish: status 3, the letter taken, the Silver Bell unless held",
              $"Player.SetMissionStatus({s},3),Player.DelKeyItem({N4Letter}),Player.GiveKeyItem({N4Bell})|" +
              $"Player.SetMissionStatus({s},3),Player.DelKeyItem({N4Letter})",
              $"{C2MRun(f152, F(2))}|{C2MRun(f152, F(2, N4Bell))}");
        N4MagStatus(L, d, MissionLog.Sandoria, id, 2, none, new[] { 3 }, new[] { 2, 4 });
        N4MagHunt(L, d, MissionLog.Sandoria, id, 2, 3, "null", "null", "ProgressEvent(0,1,1,1,1,1,1,1,1)",
                  "ProgressEvent(0,1,46,47)");
        Check($"c3 N4 {L}: section 3: Nelcabrit: 136 (event)", "Event(136)",
              C3NReply(C2MHandler(d, 2, N4Trig, (ushort)rg, QC.Npc.RuludeGardens.Nelcabrit), F(3)));
        N4MagStatus(L, d, MissionLog.Sandoria, id, 3, none, new[] { 4 }, new[] { 3, 5 });
        Check($"c3 N4 {L}: section 4: Nelcabrit 36 (progress), Aldo 183 (event); 36's finish completes",
              "ProgressEvent(36)|Event(183)|Mission.Complete()",
              $"{C3NReply(C2MHandler(d, 3, N4Trig, (ushort)rg, QC.Npc.RuludeGardens.Nelcabrit), F(4))}|" +
              $"{C3NReply(C2MHandler(d, 3, N4Trig, (ushort)lj, aldo), F(4))}|" +
              $"{C2MRun(C2MHandler(d, 3, N4Fin, (ushort)rg, id: 36), F(4))}");
    }

    static void RunC3N4MagiciteWindurst()
    {
        const string L = "magicite windurst";
        var m = new QC.Missions.Windurst.Magicite();
        var d = m.Compile();
        int w = QC.MissionLog.Windurst, id = QC.Mission.Windurst.Magicite, none = QC.Mission.Windurst.None;
        ushort rg = QC.Zone.RuludeGardens, lj = QC.Zone.LowerJeuno;
        Check($"c3 N4 {L}: clean, its key and LegacyPath, 10000 gil, rank 5, the Message to Jeuno; five sections",
              $"|{QC.MissionKey.Windurst.Magicite}|scripts/missions/windurst/4_1_Magicite|10000/5/" +
              $"{QC.KeyItem.MessageToJeunoWindurst}/|5",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{d.Reward.Gil}/" +
              $"{d.Reward.Rank}/{d.Reward.KeyItem}/{d.Reward.RankPoints}|{d.States.Count}");
        N4MagSection1(L, d, MissionLog.Windurst, id, none, QC.Mission.Windurst.TheShadowAwaits);
        Fake P(bool points, bool permit = false)
        {
            var f = N4Fake(MissionLog.Windurst, id, none, rank: 4);
            if (points) f.RankPointsMet.Add(id);
            if (permit) f.KeyItems[N4Permit] = true;
            return f;
        }
        string R0(string npc, Fake f) => C3NReply(C2MHandler(d, 0, N4Trig, (ushort)rg, npc), f);
        Check($"c3 N4 {L}: _6r8 with the rank points: 131 (0), with the permit 131 (1) (progress); without the " +
              "points: nothing; Pakh Jatalfih with the points: 50 (progress), without: 54 (event)",
              "ProgressEvent(131,0)|ProgressEvent(131,1)|null|ProgressEvent(50)|Event(54)",
              $"{R0(QC.Npc.RuludeGardens._6r8, P(true))}|{R0(QC.Npc.RuludeGardens._6r8, P(true, true))}|" +
              $"{R0(QC.Npc.RuludeGardens._6r8, P(false, true))}|{R0(QC.Npc.RuludeGardens.PakhJatalfih, P(true))}|" +
              $"{R0(QC.Npc.RuludeGardens.PakhJatalfih, P(false))}");
        N4MagAccept(L, d, MissionLog.Windurst, id, 131, true);
        Fake F(int status, params int[] kis) => N4Mag(MissionLog.Windurst, id, status, kis: kis);
        N4MagStatus(L, d, MissionLog.Windurst, id, 1, none, new[] { 1 }, new[] { 0, 2 });
        Check($"c3 N4 {L}: section 2: _6r9 128 (progress), Pakh Jatalfih 134 (event); 128's finish: status 2, the " +
              "letter to Aldo",
              $"ProgressEvent(128)|Event(134)|Player.SetMissionStatus({w},2),Player.GiveKeyItem({N4Letter})",
              $"{C3NReply(C2MHandler(d, 1, N4Trig, (ushort)rg, QC.Npc.RuludeGardens._6r9), F(1))}|" +
              $"{C3NReply(C2MHandler(d, 1, N4Trig, (ushort)rg, QC.Npc.RuludeGardens.PakhJatalfih), F(1))}|" +
              $"{C2MRun(C2MHandler(d, 1, N4Fin, (ushort)rg, id: 128), F(1))}");
        N4MagStatus(L, d, MissionLog.Windurst, id, 2, none, new[] { 2 }, new[] { 1, 3 });
        string aldo = QC.Npc.LowerJeuno.Aldo;
        var f152 = C2MHandler(d, 2, N4Fin, (ushort)lj, id: 152);
        Check($"c3 N4 {L}: section 3: Aldo with the Silver Bell: 152 (1, 1); without: 152 (0, 1) (progress); Pakh " +
              "Jatalfih 137 (event); 152's finish: status 3, the letter taken, the bell unless held",
              $"ProgressEvent(152,1,1)|ProgressEvent(152,0,1)|Event(137)|" +
              $"Player.SetMissionStatus({w},3),Player.DelKeyItem({N4Letter}),Player.GiveKeyItem({N4Bell})|" +
              $"Player.SetMissionStatus({w},3),Player.DelKeyItem({N4Letter})",
              $"{C3NReply(C2MHandler(d, 2, N4Trig, (ushort)lj, aldo), F(2, N4Bell))}|" +
              $"{C3NReply(C2MHandler(d, 2, N4Trig, (ushort)lj, aldo), F(2))}|" +
              $"{C3NReply(C2MHandler(d, 2, N4Trig, (ushort)rg, QC.Npc.RuludeGardens.PakhJatalfih), F(2))}|" +
              $"{C2MRun(f152, F(2))}|{C2MRun(f152, F(2, N4Bell))}");
        N4MagStatus(L, d, MissionLog.Windurst, id, 3, none, new[] { 3 }, new[] { 2, 4 });
        N4MagHunt(L, d, MissionLog.Windurst, id, 3, 3, "null", "ProgressEvent(80)",
                  $"ProgressEvent(0,{QC.Zone.MonasticCavern},0,1)", "ProgressEvent(0,1,46,47)");
        Check($"c3 N4 {L}: section 4: Pakh Jatalfih: 137 (event)", "Event(137)",
              C3NReply(C2MHandler(d, 3, N4Trig, (ushort)rg, QC.Npc.RuludeGardens.PakhJatalfih), F(3)));
        N4MagStatus(L, d, MissionLog.Windurst, id, 4, none, new[] { 4 }, new[] { 3, 5 });
        Check($"c3 N4 {L}: section 5: Aldo 183 (event), Pakh Jatalfih 37 (progress); 37's finish completes",
              "Event(183)|ProgressEvent(37)|Mission.Complete()",
              $"{C3NReply(C2MHandler(d, 4, N4Trig, (ushort)lj, aldo), F(4))}|" +
              $"{C3NReply(C2MHandler(d, 4, N4Trig, (ushort)rg, QC.Npc.RuludeGardens.PakhJatalfih), F(4))}|" +
              $"{C2MRun(C2MHandler(d, 4, N4Fin, (ushort)rg, id: 37), F(4))}");
    }
}
