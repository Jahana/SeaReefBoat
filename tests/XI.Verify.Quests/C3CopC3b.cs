// XI.Verify.Quests -- quest rework C3 C3b: CoP 5-3 Three Paths and its Head Wind and Flames for the Dead battlefields.
// Each handler's arms one at a time (each part of a multi-part condition alone), the values no trace sees (the 851
// guards, which progress events outrank, the section bounds, the battlefieldWin part of the 32001 finishes, the path
// finishes' complete-or-reset), and the data of both battlefields against the Lua files' (as they stood at the
// recording).
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    internal static void RunC3CopC3b()
    {
        Console.WriteLine("\n== c3 C3b: CoP 5-3 Three Paths, Head Wind and Flames for the Dead ==");
        RunC3C3bMission();
        RunC3C3bHeadWind();
        RunC3C3bChain();
        RunC3C3bFlames();
    }

    // the three path fields of Three Paths' status word: Louverance (position 2), Tenzen (3), Ulmia (4)
    static Fake C3bFake(int l, int t, int u, int cidOption = 0)
    {
        var f = new Fake { ThisMission = (MissionLog.CoP, QC.Mission.Cop.ThreePaths) };
        f.CurrentMissions[C2bCop] = QC.Mission.Cop.ThreePaths;
        f.MissionStatuses[C2bCop] = l << 8 | t << 12 | u << 16;
        f.MissionLocalVars["cidOption"] = cidOption;
        return f;
    }

    static void RunC3C3bMission()
    {
        var onTrig = QuestHandlerKind.OnTrigger;
        var onTrade = QuestHandlerKind.OnTrade;
        var fin = QuestHandlerKind.OnEventFinish;
        var zin = QuestHandlerKind.OnZoneIn;
        var ms = new QC.Missions.Cop.ThreePaths();
        var d = ms.Compile();
        string R(int state, QuestHandlerKind kind, ushort zone, string npc, Fake f, int id = 1) =>
            C2bReply(d, state, kind, zone, npc, f, id);
        string Run(int state, ushort zone, int csid, Fake f) => C2MRun(C2MHandler(d, state, fin, zone, id: csid), f);
        string Held(int state, Fake f) => d.States[state].MissionMatches[0].When(f).ToString();
        // the replies of one NPC over the path field values
        string Over(int state, ushort zone, string npc, Func<int, Fake> make, params int[] values) =>
            string.Join("|", values.Select(v => R(state, onTrig, zone, npc, make(v))));
        int hw = QC.Battlefield.HeadWind, ffd = QC.Battlefield.FlamesForTheDead;

        Check("c3 C3b: 5-3 is a CoP mission with its Lua file, For Whom the Verse is Sung next, the Treader of an " +
              "Icy Past title; validates clean",
              $"mission.cop.three_paths scripts/missions/cop/5_3_Three_Paths {QC.Mission.Cop.ForWhomTheVerseIsSung} " +
              $"{QC.Title.TreaderOfAnIcyPast} ",
              $"{ms.Key} {ms.LegacyPath} {d.Reward.NextMission} {d.Reward.Title} " +
              string.Join(" | ", QuestScriptValidator.Validate(d)));

        // ---- Cid's reminder (851): every guard alone, the argument by path
        ushort mw = QC.Zone.Metalworks;
        string cid = QC.Npc.Metalworks.Cid;
        string Reminder(Fake f) => R(0, onTrig, mw, cid, f);
        Check("c3 C3b: Cid's reminder 851 (priority 995) plays only with cidOption 0 and no path waiting for Cid " +
              "(Louverance 12, Tenzen 12, Ulmia 9), each condition alone",
              "Event(851,0):setPriority(995)|null|null|null|null",
              string.Join("|", new[]
              {
                  Reminder(C3bFake(0, 0, 0)), Reminder(C3bFake(0, 0, 0, 1)), Reminder(C3bFake(12, 0, 0)),
                  Reminder(C3bFake(0, 12, 0)), Reminder(C3bFake(0, 0, 9)),
              }));
        Check("c3 C3b: Cid's reminder argument is 1 for Louverance at 14, 2 for Tenzen, 4 for Ulmia, summed; a path " +
              "at 13 or 15 adds nothing",
              "Event(851,1):setPriority(995)|Event(851,2):setPriority(995)|Event(851,4):setPriority(995)|" +
              "Event(851,7):setPriority(995)|Event(851,0):setPriority(995)|Event(851,0):setPriority(995)",
              string.Join("|", new[]
              {
                  Reminder(C3bFake(14, 0, 0)), Reminder(C3bFake(0, 14, 0)), Reminder(C3bFake(0, 0, 14)),
                  Reminder(C3bFake(14, 14, 14)), Reminder(C3bFake(13, 13, 13)), Reminder(C3bFake(15, 15, 15)),
              }));
        Check("c3 C3b: 851's finish sets cidOption 1",
              "Mission.SetLocalVar(cidOption,1)", Run(0, mw, 851, C3bFake(0, 0, 0)));

        // ---- the sections' bounds: each is held while its own field is 14 or less
        Check("c3 C3b: each path's section holds while its own field is at most 14 (Louverance, Tenzen, Ulmia), " +
              "not at 15, and not by another path's field",
              "True|False|True|False|True|False",
              string.Join("|", new[]
              {
                  Held(1, C3bFake(14, 15, 15)), Held(1, C3bFake(15, 0, 0)), Held(2, C3bFake(15, 14, 15)),
                  Held(2, C3bFake(0, 15, 0)), Held(3, C3bFake(15, 15, 14)), Held(3, C3bFake(0, 0, 15)),
              }));

        // ---- Louverance's path (section 1)
        ushort saf = QC.Zone.TavnazianSafehold, ww = QC.Zone.WindurstWoods, bb = QC.Zone.BibikiBay;
        ushort wwa = QC.Zone.WindurstWalls, om = QC.Zone.OldtonMovalpolos, mine = QC.Zone.MineShaft2716;
        Fake L(int v) => C3bFake(v, 0, 0);
        Check("c3 C3b: Louverance's Despachiaire plays 118 (important) at 0, 134 at any other value",
              "Event(118):importantEvent()|Event(134)|Event(134)|Event(134)",
              Over(1, saf, QC.Npc.TavnazianSafehold.Despachiaire, L, 0, 1, 2, 14));
        Check("c3 C3b: Louverance's Perih Vashai plays 686 at 2 and 687 at 3 (important), else nothing",
              "null|Event(686):importantEvent()|Event(687):importantEvent()|null",
              Over(1, ww, QC.Npc.WindurstWoods.PerihVashai, L, 1, 2, 3, 4));
        Check("c3 C3b: Louverance's Warmachine plays cutscene 33 at 3 only",
              "null|ProgressCutscene(33)|null", Over(1, bb, QC.Npc.BibikiBay.Warmachine, L, 2, 3, 4));
        Check("c3 C3b: Louverance's Yoran-Oran plays 481 (important) at 6 only",
              "null|Event(481):importantEvent()|null", Over(1, wwa, QC.Npc.WindurstWalls.YoranOran, L, 5, 6, 7));
        Check("c3 C3b: Louverance's Tarnotik plays 34 (important) at 11 only",
              "null|Event(34):importantEvent()|null", Over(1, om, QC.Npc.OldtonMovalpolos.Tarnotik, L, 10, 11, 12));
        Check("c3 C3b: Oldton Movalpolos' zone-in plays 1 at Louverance 6 only",
              "null|Event(1)|null",
              string.Join("|", new[] { 5, 6, 7 }.Select(v => R(1, zin, om, null, L(v)))));
        string Trade(int l, bool items)
        {
            var f = L(l);
            f.TradeResult = items;
            return R(1, onTrade, mine, QC.Npc.MineShaft2716._0d0, f);
        }
        Check("c3 C3b: the Gold Key traded to _0d0 plays progress event 3 at Louverance 11 with the key, each alone",
              "ProgressEvent(3)|null|null|null",
              string.Join("|", new[] { Trade(11, true), Trade(11, false), Trade(10, true), Trade(12, true) }));
        Check("c3 C3b: the finishes: 1 sets Louverance 8, 3 takes the trade and sets 12, 33 sets 6, 118 sets 2, " +
              "686 sets 3",
              "Mission.SetStatusAt(8,2)|Trade.Complete(),Mission.SetStatusAt(12,2)|Mission.SetStatusAt(6,2)|" +
              "Mission.SetStatusAt(2,2)|Mission.SetStatusAt(3,2)",
              string.Join("|", new[]
              {
                  Run(1, om, 1, L(6)), Run(1, mine, 3, L(11)), Run(1, bb, 33, L(3)), Run(1, saf, 118, L(0)),
                  Run(1, ww, 686, L(2)),
              }));
        string MineWin(int win, int l)
        {
            var f = L(l);
            f.PlayerLocalVars["battlefieldWin"] = win;
            return Run(1, mine, 32001, f);
        }
        int century = QC.Battlefield.CenturyOfHardship;
        Check("c3 C3b: Mine Shaft's 32001 sets Louverance 9 only when the win is the Century of Hardship and " +
              "Louverance is 8, each condition alone",
              "Mission.SetStatusAt(9,2)|||",
              string.Join("|", new[] { MineWin(century, 8), MineWin(century, 9), MineWin(hw, 8), MineWin(0, 8) }));
        Check("c3 C3b: Louverance's Cid plays 852 at 9, 853 at 12 (its argument the other paths at 14), else nothing",
              "null|ProgressEvent(852)|null|ProgressEvent(853,0)|ProgressEvent(853,6)|null",
              string.Join("|", new[]
              {
                  R(1, onTrig, mw, cid, L(8)), R(1, onTrig, mw, cid, L(9)), R(1, onTrig, mw, cid, L(11)),
                  R(1, onTrig, mw, cid, L(12)), R(1, onTrig, mw, cid, C3bFake(12, 14, 14)),
                  R(1, onTrig, mw, cid, L(13)),
              }));
        Check("c3 C3b: Cid's 852 resets cidOption and sets Louverance 11",
              "Mission.SetLocalVar(cidOption,0),Mission.SetStatusAt(11,2)", Run(1, mw, 852, L(9)));
        string Done(int state, int csid, int l, int t, int u) => Run(state, mw, csid, C3bFake(l, t, u, 1));
        string lv = $"Mission.SetStatusAt(14,2),Player.AddTitle({QC.Title.CompanionOfLouverance})";
        Check("c3 C3b: Cid's 853 sets Louverance 14, gives the Companion of Louverance title, then completes the " +
              "mission only when Tenzen and Ulmia are both 14 (each alone), else resets cidOption",
              $"{lv},Mission.SetLocalVar(cidOption,0)|{lv},Mission.SetLocalVar(cidOption,0)|" +
              $"{lv},Mission.SetLocalVar(cidOption,0)|{lv},Mission.Complete()",
              string.Join("|", new[]
              {
                  Done(1, 853, 12, 0, 0), Done(1, 853, 12, 14, 13), Done(1, 853, 12, 13, 14), Done(1, 853, 12, 14, 14),
              }));

        // ---- Tenzen's path (section 2)
        ushort lt = QC.Zone.LaTheinePlateau, ps = QC.Zone.Psoxja, uj = QC.Zone.UpperJeuno, rg = QC.Zone.RuludeGardens;
        ushort bat = QC.Zone.BatalliaDowns, ld = QC.Zone.LowerDelkfuttsTower;
        Fake T(int v) => C3bFake(0, v, 0);
        Check("c3 C3b: Tenzen's La Theine qm3 plays 203 (important) at 0 only",
              "Event(203):importantEvent()|null|null", Over(2, lt, QC.Npc.LaTheinePlateau.qm3, T, 0, 1, 2));
        Check("c3 C3b: Tenzen's Psoxja _09g plays cutscene 3 at 2 only, _09h cutscene 5 at 11 only",
              "null|ProgressCutscene(3)|null|null|ProgressCutscene(5)|null",
              Over(2, ps, QC.Npc.Psoxja._09g, T, 1, 2, 3) + "|" + Over(2, ps, QC.Npc.Psoxja._09h, T, 10, 11, 12));
        string PsZin(double x, int t)
        {
            var f = T(t);
            f.XPos = x;
            return R(2, zin, ps, null, f);
        }
        Check("c3 C3b: Psoxja's zone-in plays 4 only at x 220 with Tenzen 9, each condition alone",
              "Event(4)|null|null|null",
              string.Join("|", new[] { PsZin(220, 9), PsZin(221, 9), PsZin(220, 8), PsZin(220, 10) }));
        Check("c3 C3b: Tenzen's Psoxja finishes: 3 sets 3, 4 sets 11, 5 sets 12",
              "Mission.SetStatusAt(3,3)|Mission.SetStatusAt(11,3)|Mission.SetStatusAt(12,3)",
              string.Join("|", new[] { Run(2, ps, 3, T(2)), Run(2, ps, 4, T(9)), Run(2, ps, 5, T(11)) }));
        Check("c3 C3b: Tenzen's Monberaux plays 74 at 3, then 5, 6 and 7 (important) at 5, 6 and 8, else nothing; " +
              "Rosaline always plays 96",
              "null|ProgressEvent(74)|null|Event(5):importantEvent()|Event(6):importantEvent()|null|" +
              "Event(7):importantEvent()|null|Event(96)|Event(96)",
              Over(2, uj, QC.Npc.UpperJeuno.Monberaux, T, 2, 3, 4, 5, 6, 7, 8, 9) + "|" +
              Over(2, uj, QC.Npc.UpperJeuno.Rosaline, T, 0, 5));
        Check("c3 C3b: Monberaux's 74 gives the Envelope from Monberaux and sets Tenzen 5",
              $"Player.GiveKeyItem({QC.KeyItem.EnvelopeFromMonberaux}),Mission.SetStatusAt(5,3)", Run(2, uj, 74, T(3)));
        Check("c3 C3b: Tenzen's Pherimociel plays 58 at 5 only; its finish sets 6",
              "null|ProgressEvent(58)|null|Mission.SetStatusAt(6,3)",
              Over(2, rg, QC.Npc.RuludeGardens.Pherimociel, T, 4, 5, 6) + "|" + Run(2, rg, 58, T(5)));
        string Qm4(int t, int option)
        {
            var f = T(t);
            f.MissionVars[$"{C2bCop}/{QC.Mission.Cop.ThreePaths}/Option"] = option;
            return R(2, onTrig, bat, QC.Npc.BatalliaDowns.qm4, f);
        }
        Check("c3 C3b: Batallia's qm4 at Tenzen 6 plays cutscene 0 at Option 0 and 2, event 1 at Option 1; nothing " +
              "at 5 or 7",
              "ProgressCutscene(0)|ProgressEvent(1)|ProgressCutscene(0)|null|null",
              string.Join("|", new[] { Qm4(6, 0), Qm4(6, 1), Qm4(6, 2), Qm4(5, 1), Qm4(7, 1) }));
        Check("c3 C3b: qm4's finishes: 0 sets Option 1; 1 gives the Delkfutt Recognition Device, resets Option and " +
              "sets Tenzen 8",
              $"Mission.SetVar(Option,1)|Player.GiveKeyItem({QC.KeyItem.DelkfuttRecognitionDevice})," +
              "Mission.SetVar(Option,0),Mission.SetStatusAt(8,3)",
              Run(2, bat, 0, T(6)) + "|" + Run(2, bat, 1, T(6)));
        string Door(int t, bool device, int killed, bool pops)
        {
            var f = T(t);
            if (device) f.KeyItems[QC.KeyItem.DelkfuttRecognitionDevice] = true;
            f.MissionLocalVars["hasKilled"] = killed;
            f.PopResult = pops;
            var r = R(2, onTrig, ld, QC.Npc.LowerDelkfuttsTower._545, f);
            return r + "/" + string.Join(",", f.Calls.Where(c => c.StartsWith("Player.PopFromQm")));
        }
        string pop = $"Player.PopFromQm({QC.Mob.LowerDelkfuttsTower.DISASTER_IDOL},True,0)";
        Check("c3 C3b: Lower Delkfutt's _545 at Tenzen 8 with the device: not killed pops the Disaster Idol " +
              "(claimed, hide 0) and says SOMETHING_HUGE_BEARING_DOWN, a pop refused or any other hasKilled plays " +
              "nothing, killed plays cutscene 25 without a pop; without the device or at another value nothing",
              $"MessageSpecial({ld}:SOMETHING_HUGE_BEARING_DOWN)/{pop}|null/{pop}|" +
              "ProgressCutscene(25)/|null/|null/|null/",
              string.Join("|", new[]
              {
                  Door(8, true, 0, true), Door(8, true, 0, false), Door(8, true, 1, false), Door(8, true, 2, true),
                  Door(8, false, 0, true), Door(7, true, 0, true),
              }));
        string Idol(int killed)
        {
            var f = T(8);
            f.MissionLocalVars["hasKilled"] = killed;
            f.MobDeath = new QuestMobDeathFake();
            return C2MRun(C2MHandler(d, 2, QuestHandlerKind.OnMobDeath, ld, QC.Npc.LowerDelkfuttsTower.DisasterIdol),
                          f);
        }
        Check("c3 C3b: the Disaster Idol's death sets hasKilled 1 only when it is 0; finish 25 sets Tenzen 9",
              "Mission.SetLocalVar(hasKilled,1)||Mission.SetStatusAt(9,3)",
              Idol(0) + "|" + Idol(1) + "|" + Run(2, ld, 25, T(8)));
        Check("c3 C3b: Tenzen's Cid plays 854 at 12 only (its argument the other paths at 14)",
              "null|ProgressEvent(854,0)|ProgressEvent(854,5)|null",
              string.Join("|", new[]
              {
                  R(2, onTrig, mw, cid, T(11)), R(2, onTrig, mw, cid, T(12)),
                  R(2, onTrig, mw, cid, C3bFake(14, 12, 14)), R(2, onTrig, mw, cid, T(13)),
              }));
        string tv = $"Mission.SetStatusAt(14,3),Player.AddTitle({QC.Title.TenzensAlly})";
        Check("c3 C3b: Cid's 854 sets Tenzen 14, gives the Tenzen's Ally title, then completes only when Louverance " +
              "and Ulmia are both 14 (each alone), else resets cidOption",
              $"{tv},Mission.SetLocalVar(cidOption,0)|{tv},Mission.SetLocalVar(cidOption,0)|" +
              $"{tv},Mission.SetLocalVar(cidOption,0)|{tv},Mission.Complete()",
              string.Join("|", new[]
              {
                  Done(2, 854, 0, 12, 0), Done(2, 854, 14, 12, 13), Done(2, 854, 13, 12, 14), Done(2, 854, 14, 12, 14),
              }));

        // ---- Ulmia's path (section 3)
        ushort ssd = QC.Zone.SouthernSanDoria, psd = QC.Zone.PortSanDoria, nsd = QC.Zone.NorthernSanDoria;
        ushort wwt = QC.Zone.WindurstWaters, bg = QC.Zone.BoneyardGully, bp = QC.Zone.BearclawPinnacle;
        Fake U(int v) => C3bFake(0, 0, v);
        Check("c3 C3b: Ulmia's Hinaree plays 22 at 0, 25 (important) at 2, else nothing",
              "ProgressEvent(22)|null|Event(25):importantEvent()|null",
              Over(3, ssd, QC.Npc.SouthernSanDoria.Hinaree, U, 0, 1, 2, 3));
        Check("c3 C3b: Port San d'Oria's zone-in plays 4 at Ulmia 2 only",
              "null|Event(4)|null", string.Join("|", new[] { 1, 2, 3 }.Select(v => R(3, zin, psd, null, U(v)))));
        Check("c3 C3b: Ulmia's Chasalvige plays 762 at 3, 763 (important) at 4, else nothing",
              "null|ProgressEvent(762)|Event(763):importantEvent()|null",
              Over(3, nsd, QC.Npc.NorthernSanDoria.Chasalvige, U, 2, 3, 4, 5));
        Check("c3 C3b: Ulmia's Kerutoto plays 876 at 4, 882 (important) at 6, else nothing",
              "null|ProgressEvent(876)|null|Event(882):importantEvent()|null",
              Over(3, wwt, QC.Npc.WindurstWaters.Kerutoto, U, 3, 4, 5, 6, 7));
        Check("c3 C3b: Ulmia's Yoran-Oran plays 473 at 6, 479 (important) at 7, else nothing",
              "null|ProgressEvent(473)|Event(479):importantEvent()|null",
              Over(3, wwa, QC.Npc.WindurstWalls.YoranOran, U, 5, 6, 7, 8));
        Check("c3 C3b: Ulmia's finishes: 22 sets 2, Port San d'Oria 4 sets 3, 762 sets 4, 876 sets 6, 473 sets 7",
              "Mission.SetStatusAt(2,4)|Mission.SetStatusAt(3,4)|Mission.SetStatusAt(4,4)|Mission.SetStatusAt(6,4)|" +
              "Mission.SetStatusAt(7,4)",
              string.Join("|", new[]
              {
                  Run(3, ssd, 22, U(0)), Run(3, psd, 4, U(2)), Run(3, nsd, 762, U(3)), Run(3, wwt, 876, U(4)),
                  Run(3, wwa, 473, U(6)),
              }));
        string Win(ushort zone, int win, int u)
        {
            var f = U(u);
            f.PlayerLocalVars["battlefieldWin"] = win;
            return Run(3, zone, 32001, f);
        }
        Check("c3 C3b: Boneyard Gully's 32001 sets Ulmia 8 only when the win is Head Wind and Ulmia is 7, each " +
              "condition alone; Bearclaw Pinnacle's sets 9 only for Flames for the Dead at 8",
              "Mission.SetStatusAt(8,4)|||Mission.SetStatusAt(9,4)||",
              string.Join("|", new[]
              {
                  Win(bg, hw, 7), Win(bg, hw, 8), Win(bg, ffd, 7), Win(bp, ffd, 8), Win(bp, ffd, 7), Win(bp, hw, 8),
              }));
        Check("c3 C3b: Ulmia's Cid plays 855 at 9 only (its argument the other paths at 14)",
              "null|ProgressEvent(855,0)|ProgressEvent(855,3)|null",
              string.Join("|", new[]
              {
                  R(3, onTrig, mw, cid, U(8)), R(3, onTrig, mw, cid, U(9)), R(3, onTrig, mw, cid, C3bFake(14, 14, 9)),
                  R(3, onTrig, mw, cid, U(10)),
              }));
        string uv = $"Mission.SetStatusAt(14,4),Player.AddTitle({QC.Title.UlmiasSoulmate})";
        Check("c3 C3b: Cid's 855 sets Ulmia 14, gives the Ulmia's Soulmate title, then completes only when " +
              "Louverance and Tenzen are both 14 (each alone), else resets cidOption",
              $"{uv},Mission.SetLocalVar(cidOption,0)|{uv},Mission.SetLocalVar(cidOption,0)|" +
              $"{uv},Mission.SetLocalVar(cidOption,0)|{uv},Mission.Complete()",
              string.Join("|", new[]
              {
                  Done(3, 855, 0, 0, 9), Done(3, 855, 14, 13, 9), Done(3, 855, 13, 14, 9), Done(3, 855, 14, 14, 9),
              }));
    }

    static void RunC3C3bHeadWind()
    {
        var s = new QC.Battlefields.BoneyardGully.HeadWind();
        var bd = (BattlefieldMissionData)s.Data;
        Check("c3 C3b: Head Wind validates clean, its key and Lua file",
              "|battlefield.head_wind scripts/battlefields/Boneyard_Gully/head_wind",
              $"{string.Join(" | ", BattlefieldScriptValidator.Validate(s))}|{s.Key} {s.LegacyPath}");
        Check("c3 C3b: Head Wind's fields (zone, id, no exp loss, mission, trusts, players, MAX_LEVEL, time, index, " +
              "grantXP, title, entry and exit NPCs, mission, status position and required value, groups)",
              $"{QC.Zone.BoneyardGully} {QC.Battlefield.HeadWind} False True True 6 MAX_LEVEL 1800 0 1000 " +
              $"{QC.Title.DeltaEnforcer} _081 _082,_084,_086 6/{QC.Mission.Cop.ThreePaths} 6 4 7 3",
              $"{bd.ZoneId} {bd.BattlefieldId} {bd.CanLoseExp} {bd.IsMission} {bd.AllowTrusts} {bd.MaxPlayers} " +
              $"{bd.LevelCapSetting} {bd.TimeLimit} {bd.Index} {bd.GrantXP} {bd.Title} " +
              $"{string.Join(",", bd.EntryNpcs)} {string.Join(",", bd.ExitNpcs)} {(int)bd.MissionArea}/{bd.Mission} " +
              $"{(int)bd.MissionStatusArea} {bd.MissionStatus} {bd.RequiredMissionStatus} {bd.Groups.Count}");
        string Ids(BattlefieldGroup g) => string.Join(";", g.AreaMobIds.Select(a =>
            string.Join(",", a.Select(o => o.Key + ":" + o.Offset))));
        string z = QC.Mob.BoneyardGully.SHIKAREE_Z_HW, y = QC.Mob.BoneyardGully.SHIKAREE_Y_HW,
               x = QC.Mob.BoneyardGully.SHIKAREE_X_HW;
        Check("c3 C3b: Head Wind's first group: the three Shikarees Z, Y, X to an area (offsets 0, 5, 10), spawned, " +
              "superlinked, allDeath wins",
              $"{z}:0,{y}:0,{x}:0;{z}:5,{y}:5,{x}:5;{z}:10,{y}:10,{x}:10  1 True",
              $"{Ids(bd.Groups[0])} {bd.Groups[0].Spawned} {bd.Groups[0].SuperlinkGroup} " +
              $"{bd.Groups[0].AllDeath != null}");
        Check("c3 C3b: Head Wind's second group (Z +4, +9, +14) is spawned and superlinked, the third (Z +3, +8, " +
              "+13) superlinked and not spawned; neither has an allDeath",
              $"{z}:4;{z}:9;{z}:14  1 False|{z}:3;{z}:8;{z}:13 False 1 False",
              $"{Ids(bd.Groups[1])} {bd.Groups[1].Spawned} {bd.Groups[1].SuperlinkGroup} " +
              $"{bd.Groups[1].AllDeath != null}|" +
              $"{Ids(bd.Groups[2])} {bd.Groups[2].Spawned} {bd.Groups[2].SuperlinkGroup} " +
              $"{bd.Groups[2].AllDeath != null}");
        var bf = new BattlefieldViewFake();
        bd.Groups[0].AllDeath(new BattlefieldContextFake(s) { Battlefield = bf }, null);
        Check("c3 C3b: Head Wind's first group's allDeath sets the status to WON",
              "Battlefield.SetStatus(2)", string.Join(",", bf.Calls));
    }

    static void RunC3C3bFlames()
    {
        var s = new QC.Battlefields.BearclawPinnacle.FlamesForTheDead();
        var bd = (BattlefieldMissionData)s.Data;
        Check("c3 C3b: Flames for the Dead validates clean, its key and Lua file",
              "|battlefield.flames_for_the_dead scripts/battlefields/Bearclaw_Pinnacle/flames_for_the_dead",
              $"{string.Join(" | ", BattlefieldScriptValidator.Validate(s))}|{s.Key} {s.LegacyPath}");
        Check("c3 C3b: Flames for the Dead's fields (zone, id, no exp loss, debuffs cleaned, trusts, players, " +
              "MAX_LEVEL, time, index, grantXP, title, entry and exit NPCs, mission, status position and required " +
              "value, essential mobs, groups)",
              $"{QC.Zone.BearclawPinnacle} {QC.Battlefield.FlamesForTheDead} False True False 6 MAX_LEVEL 1800 0 " +
              $"1000 {QC.Title.TrueCompanionOfLouverance} Wind_Pillar_1 Wind_Pillar_Exit " +
              $"6/{QC.Mission.Cop.ThreePaths} 6 4 8 Snoll_Tzar 0",
              $"{bd.ZoneId} {bd.BattlefieldId} {bd.CanLoseExp} {bd.CleanupDebuffs} {bd.AllowTrusts} {bd.MaxPlayers} " +
              $"{bd.LevelCapSetting} {bd.TimeLimit} {bd.Index} {bd.GrantXP} {bd.Title} " +
              $"{string.Join(",", bd.EntryNpcs)} {string.Join(",", bd.ExitNpcs)} {(int)bd.MissionArea}/{bd.Mission} " +
              $"{(int)bd.MissionStatusArea} {bd.MissionStatus} {bd.RequiredMissionStatus} " +
              $"{string.Join(";", bd.EssentialMobs.Select(m => string.Join(",", m)))} {bd.Groups.Count}");
        var p = new BattlefieldPlayerFake { ZoneId = QC.Zone.BearclawPinnacle };
        var engine = new BattlefieldEngineFake();
        engine.Texts[$"{QC.Zone.BearclawPinnacle}:BLOWN_AWAY"] = 7777;
        s.OnBattlefieldLoss(new BattlefieldContextFake(s) { Player = p, Engine = engine });
        Check("c3 C3b: Flames for the Dead's loss says BLOWN_AWAY, then the framework's loss event 32002",
              "Player.MessageSpecial(7777),Player.StartEvent(32002,{})", string.Join(",", p.Calls));
    }
}
