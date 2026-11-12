// XI.Verify.Quests -- quest rework C3 C3a: CoP 5-1, 5-2, 6-1 to 6-4 and the Desires of Emptiness and One to be Feared
// battlefields. Each handler's arms one at a time (each part of a multi-part condition alone), the values no trace
// sees (the positions, the z boundary of the Psoxja trap, the battlefieldWin part of the 32001 finishes), the
// completed states, and the data of both battlefields against the Lua files' (as they stood at the recording).
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    internal static void RunC3CopC3a()
    {
        Console.WriteLine("\n== c3 C3a: CoP 5-1, 5-2, 6-1 to 6-4 and their battlefields ==");
        RunC3C3aTumult();
        RunC3C3aDesires();
        RunC3C3aVahzl();
        RunC3C3aVerse();
        RunC3C3aPlace();
        RunC3C3aQuestions();
        RunC3C3aFeared();
        RunC3C3aFearedBattlefield();
    }

    static Fake C3aFake(int mission, int status = 0, int option = 0)
    {
        var f = C2bFake(mission, status);
        f.MissionVars[$"{C2bCop}/{mission}/Option"] = option;
        return f;
    }

    static string C3aReps(MissionScriptDefinition d, QuestHandlerKind kind, ushort zone, string npc, int mission,
                          params (int Status, int Option)[] cases) =>
        string.Join("|", cases.Select(c => C2bReply(d, 0, kind, zone, npc, C3aFake(mission, c.Status, c.Option))));

    static string C3aCompleted(MissionScriptDefinition d, int mission, int earlier)
    {
        string Held(Fake f) => d.States[1].MissionMatches[0].When(f).ToString();
        var done = new Fake();
        done.CompletedMissions.Add($"{C2bCop}/{mission}");
        var other = new Fake();
        other.CompletedMissions.Add($"{C2bCop}/{earlier}");
        return string.Join("|", Held(done), Held(other), Held(new Fake()));
    }

    static string C3aRun(MissionScriptDefinition d, QuestHandlerKind kind, ushort zone, int id, Fake f) =>
        C2MRun(C2MHandler(d, 0, kind, zone, id: id), f);

    static void RunC3C3aTumult()
    {
        var onTrig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var ms = new QC.Missions.Cop.TheEnduringTumultOfWar();
        var d = ms.Compile();
        int m = QC.Mission.Cop.TheEnduringTumultOfWar;
        Check("c3 C3a: 5-1 is a CoP mission with its Lua file, Desires of Emptiness next, no title; validates clean",
              $"mission.cop.the_enduring_tumult_of_war scripts/missions/cop/5_1_The_Enduring_Tumult_of_War " +
              $"{QC.Mission.Cop.DesiresOfEmptiness}  ",
              $"{ms.Key} {ms.LegacyPath} {d.Reward.NextMission} {d.Reward.Title} " +
              string.Join(" | ", QuestScriptValidator.Validate(d)));
        ushort saf = QC.Zone.TavnazianSafehold, nsd = QC.Zone.NorthernSanDoria, psd = QC.Zone.PortSanDoria;
        Check("c3 C3a: 5-1 Despachiaire plays 117 (important) at Status 0 with bit 0 unset, each condition alone",
              "Event(117):importantEvent()|null|null",
              C3aReps(d, onTrig, saf, QC.Npc.TavnazianSafehold.Despachiaire, m, (0, 0), (0, 1), (1, 0)));
        Check("c3 C3a: 5-1 Chasalvige plays 761 (important) at Status 0 with bit 1 unset, else 764 replacing",
              "Event(761):importantEvent()|Event(764):replaceDefault()|Event(764):replaceDefault()",
              C3aReps(d, onTrig, nsd, QC.Npc.NorthernSanDoria.Chasalvige, m, (0, 0), (0, 2), (1, 0)));
        Check("c3 C3a: 5-1 Anoki plays 724 with bit 2 unset, 728 with bit 3 unset, at Status 0 only",
              "Event(724):importantEvent()|Event(728):importantEvent()|null|null|null",
              C3aReps(d, onTrig, psd, QC.Npc.PortSanDoria.Anoki, m, (0, 0), (0, 4), (0, 12), (1, 0), (1, 4)));
        Check("c3 C3a: 5-1 Port Bastok's zone-in plays 306 at Status 0 only (the bits are not looked at)",
              "Event(306)|Event(306)|null",
              string.Join("|", new[]
              {
                  C2bReply(d, 0, QuestHandlerKind.OnZoneIn, QC.Zone.PortBastok, null, C3aFake(m, 0, 0)),
                  C2bReply(d, 0, QuestHandlerKind.OnZoneIn, QC.Zone.PortBastok, null, C3aFake(m, 0, 15)),
                  C2bReply(d, 0, QuestHandlerKind.OnZoneIn, QC.Zone.PortBastok, null, C3aFake(m, 1, 0)),
              }));
        Check("c3 C3a: 5-1 Metalworks' Cid plays 849 at Status 1, 863 (important) above it, nothing at Status 0",
              "null|ProgressEvent(849)|Event(863):importantEvent()|Event(863):importantEvent()",
              C3aReps(d, onTrig, QC.Zone.Metalworks, QC.Npc.Metalworks.Cid, m, (0, 0), (1, 0), (2, 0), (4, 0)));
        Check("c3 C3a: 5-1's Option bits: 117 sets 0, 761 sets 1, 724 sets 2, 728 sets 3",
              "Mission.SetVarBit(Option,0)|Mission.SetVarBit(Option,1)|Mission.SetVarBit(Option,2)|" +
              "Mission.SetVarBit(Option,3)",
              string.Join("|", new[]
              {
                  C3aRun(d, fin, saf, 117, C3aFake(m)), C3aRun(d, fin, nsd, 761, C3aFake(m)),
                  C3aRun(d, fin, psd, 724, C3aFake(m)), C3aRun(d, fin, psd, 728, C3aFake(m)),
              }));
        Check("c3 C3a: 5-1's finishes set Status: 306 to 1, 849 to 2, Psoxja 1 to 3",
              "Mission.SetVar(Status,1)|Mission.SetVar(Status,2)|Mission.SetVar(Status,3)",
              string.Join("|", new[]
              {
                  C3aRun(d, fin, QC.Zone.PortBastok, 306, C3aFake(m)),
                  C3aRun(d, fin, QC.Zone.Metalworks, 849, C3aFake(m)),
                  C3aRun(d, fin, QC.Zone.Psoxja, 1, C3aFake(m)),
              }));
        // Psoxja's _i98: at Status 3 with Nunyunuwi not spawned the trap; at Status 4 the cutscene by z
        string I98(int status, double z, bool spawned)
        {
            var f = C3aFake(m, status);
            f.ZPos = z;
            if (spawned) f.SpawnedMobs.Add(QC.Mob.Psoxja.NUNYUNUWI + "+0");
            var h = C2MHandler(d, 0, onTrig, QC.Zone.Psoxja, QC.Npc.Psoxja.i98);
            return C2MReply(h, f) + "/" + string.Join(",", f.Calls);
        }
        Check("c3 C3a: 5-1 Psoxja's _i98: Status 3 pops Nunyunuwi (claimed) and says TRAP_ACTIVATES unless he is " +
              "spawned; Status 2 does nothing",
              "NoAction()/Player.SpawnMob(NUNYUNUWI,0,True),Text.MessageNameOfPlayer(Psoxja:TRAP_ACTIVATES)|" +
              "null/|null/",
              string.Join("|", new[] { I98(3, 0, false), I98(3, 0, true), I98(2, 0, false) }).Replace(
                  $"{QC.Zone.Psoxja}:", "Psoxja:"));
        string I98Cs(double z)
        {
            var f = C3aFake(m, 4);
            f.ZPos = z;
            return C2MReply(C2MHandler(d, 0, onTrig, QC.Zone.Psoxja, QC.Npc.Psoxja.i98), f);
        }
        Check("c3 C3a: 5-1 Psoxja's _i98 at Status 4 plays the cutscene 69 under z 318 and 70 from z 318 on",
              string.Join("|", new[] { 69, 69, 70, 70 }.Select(c =>
                  $"ProgressOptionalCutscene({c},{{canSkip=true,cs_option=0}})")),
              string.Join("|", new[] { I98Cs(-500), I98Cs(317.9), I98Cs(318), I98Cs(400) }));
        Check("c3 C3a: 5-1 Psoxja's _i99 plays 2 at Status 4 only",
              "null|null|ProgressEvent(2)",
              C3aReps(d, onTrig, QC.Zone.Psoxja, QC.Npc.Psoxja.i99, m, (0, 0), (3, 0), (4, 0)));
        string Death(int status)
        {
            var f = C3aFake(m, status);
            f.MobDeath = new QuestMobDeathFake();
            return C2MRun(C2MHandler(d, 0, QuestHandlerKind.OnMobDeath, QC.Zone.Psoxja, QC.Npc.Psoxja.Nunyunuwi), f);
        }
        Check("c3 C3a: 5-1 Nunyunuwi's death sets Status 4 at Status 3 only",
              "Mission.SetVar(Status,4)||", string.Join("|", new[] { Death(3), Death(2), Death(4) }));
        string Zin(double x, int prev, int status)
        {
            var f = C3aFake(m, status);
            f.XPos = x;
            f.PrevZone = prev;
            return C2MReply(C2MHandler(d, 0, QuestHandlerKind.OnZoneIn, QC.Zone.Psoxja), f);
        }
        ushort bg = QC.Zone.BeaucedineGlacier;
        Check("c3 C3a: 5-1 Psoxja's zone-in plays 1 only at x -300 from Beaucedine Glacier at Status 2, each alone",
              "Event(1)|null|null|null",
              string.Join("|", new[] { Zin(-300, bg, 2), Zin(-299, bg, 2), Zin(-300, 0, 2), Zin(-300, bg, 3) }));
        string Two(int option)
        {
            var f = C3aFake(m);
            f.Option = option;
            return C3aRun(d, fin, QC.Zone.Psoxja, 2, f);
        }
        Check("c3 C3a: 5-1's finish of Psoxja 2: option 1 sets the char down in Promyvion - Vahzl, others nothing",
              $"Player.SetPos(-14.744,0.036,-119.736,1,{QC.Zone.PromyvionVahzl})||",
              string.Join("|", new[] { Two(1), Two(0), Two(2) }));
        Check("c3 C3a: 5-1 Promyvion - Vahzl's zone-in plays 50 at Status 4 only",
              "null|null|Event(50)",
              string.Join("|", new[] { 0, 3, 4 }.Select(s =>
                  C2bReply(d, 0, QuestHandlerKind.OnZoneIn, QC.Zone.PromyvionVahzl, null, C3aFake(m, s)))));
        var fifty = C3aFake(m, 4);
        Check("c3 C3a: 5-1's finish of 50: the two key items, their two messages, the mission completes",
              $"Player.AddKeyItem({QC.KeyItem.MysteriousAmuletDrained}),Player.AddKeyItem({QC.KeyItem.LightOfVahzl})," +
              $"Text.Special(PromyvionVahzl:AMULET_RETURNED,{QC.KeyItem.MysteriousAmulet})," +
              $"Text.Special(PromyvionVahzl:LIGHT_OF_VAHZL,{QC.KeyItem.LightOfVahzl}),Mission.Complete()",
              C3aRun(d, fin, QC.Zone.PromyvionVahzl, 50, fifty).Replace($"{QC.Zone.PromyvionVahzl}:",
                  "PromyvionVahzl:"));
    }

    static void RunC3C3aDesires()
    {
        var onTrig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var ms = new QC.Missions.Cop.DesiresOfEmptiness();
        var d = ms.Compile();
        int m = QC.Mission.Cop.DesiresOfEmptiness;
        Check("c3 C3a: 5-2 is a CoP mission with its Lua file, Three Paths next, no title; validates clean",
              $"mission.cop.desires_of_emptiness scripts/missions/cop/5_2_Desires_of_Emptiness " +
              $"{QC.Mission.Cop.ThreePaths}  ",
              $"{ms.Key} {ms.LegacyPath} {d.Reward.NextMission} {d.Reward.Title} " +
              string.Join(" | ", QuestScriptValidator.Validate(d)));
        ushort pv = QC.Zone.PromyvionVahzl;
        // a flux: the NM not defeated and not spawned pops it; defeated and unseen plays the cutscene
        string Flux(string npc, int bit, bool spawned, int option)
        {
            var f = C3aFake(m, 0, option);
            if (spawned) f.SpawnedMobs.Add(bit == 0 ? QC.Mob.PromyvionVahzl.PROPAGATOR + "+0" :
                bit == 1 ? QC.Mob.PromyvionVahzl.SOLICITOR + "+0" : QC.Mob.PromyvionVahzl.PONDERER + "+0");
            return C2MReply(C2MHandler(d, 0, onTrig, pv, npc), f) + "/" + string.Join(",", f.Calls);
        }
        foreach (var (npc, bit, mob, cs) in new[]
        {
            (QC.Npc.PromyvionVahzl._0mc, 0, "PROPAGATOR", 51), (QC.Npc.PromyvionVahzl._0md, 1, "SOLICITOR", 52),
            (QC.Npc.PromyvionVahzl._0m0, 2, "PONDERER", 53),
        })
        {
            int b = 1 << bit, seen = 1 << (bit + 3);
            Check($"c3 C3a: 5-2 flux {npc}: pops {mob} claimed when not defeated and not spawned, plays {cs} when " +
                  "defeated and unseen, nothing when spawned or defeated and seen; the seen bit alone pops it again",
                  $"MessageSpecial(PromyvionVahzl:ON_NM_SPAWN)/Player.SpawnMob({mob},0,True)|null/|" +
                  $"ProgressCutscene({cs})/|null/|" +
                  $"MessageSpecial(PromyvionVahzl:ON_NM_SPAWN)/Player.SpawnMob({mob},0,True)",
                  string.Join("|", new[]
                  {
                      Flux(npc, bit, false, 0), Flux(npc, bit, true, 0), Flux(npc, bit, false, b),
                      Flux(npc, bit, false, b | seen), Flux(npc, bit, false, seen),
                  }).Replace($"{pv}:", "PromyvionVahzl:").Replace("Describe", ""));
        }
        string Death(string npc, int status)
        {
            var f = C3aFake(m, status);
            f.MobDeath = new QuestMobDeathFake();
            return C2MRun(C2MHandler(d, 0, QuestHandlerKind.OnMobDeath, pv, npc), f);
        }
        Check("c3 C3a: 5-2's NM deaths set Option bits 0 (Propagator), 1 (Solicitor), 2 (Ponderer)",
              "Mission.SetVarBit(Option,0)|Mission.SetVarBit(Option,1)|Mission.SetVarBit(Option,2)",
              string.Join("|", new[]
              {
                  Death(QC.Npc.PromyvionVahzl.Propagator, 0), Death(QC.Npc.PromyvionVahzl.Solicitor, 0),
                  Death(QC.Npc.PromyvionVahzl.Ponderer, 0),
              }));
        string Seen(int bit, int option)
        {
            var f = C3aFake(m, 0, option);
            return C3aRun(d, fin, pv, 51 + bit, f);
        }
        Check("c3 C3a: 5-2's flux finishes set their seen bit (3, 4, 5); Status 1 only when all three are seen",
              "Mission.SetVarBit(Option,3)|Mission.SetVarBit(Option,4)|Mission.SetVarBit(Option,5)|" +
              "Mission.SetVarBit(Option,3),Mission.SetVar(Status,1)|" +
              "Mission.SetVarBit(Option,5),Mission.SetVar(Status,1)",
              string.Join("|", new[] { Seen(0, 0), Seen(1, 0), Seen(2, 0), Seen(0, 48), Seen(2, 24) }));
        Check("c3 C3a: 5-2 Psoxja's _i99 plays the event of the three seen bits (50, 106, 107, 109, " +
              "108, 110, 111, 112)",
              "ProgressEvent(50)|ProgressEvent(106)|ProgressEvent(107)|ProgressEvent(109)|ProgressEvent(108)|" +
              "ProgressEvent(110)|ProgressEvent(111)|ProgressEvent(112)",
              string.Join("|", Enumerable.Range(0, 8).Select(i =>
                  C2bReply(d, 0, onTrig, QC.Zone.Psoxja, QC.Npc.Psoxja.i99, C3aFake(m, 0, i << 3)))));
        string Pos(int csid, int option)
        {
            var f = C3aFake(m);
            f.Option = option;
            return C3aRun(d, fin, QC.Zone.Psoxja, csid, f);
        }
        string vz = QC.Zone.PromyvionVahzl.ToString();
        Check("c3 C3a: 5-2's Psoxja finishes (50, 106 to 112) set the char down at the Vahzl position of their option",
              $"Player.SetPos(-14.744,0.036,-119.736,1,{vz})|Player.SetPos(183.546,0,-59.906,0,{vz})|" +
              $"Player.SetPos(415.757,0,139.977,128,{vz})|Player.SetPos(-336.001,0,139.815,0,{vz})|||",
              string.Join("|", new[] { Pos(50, 1), Pos(106, 2), Pos(111, 3), Pos(112, 4), Pos(107, 0), Pos(108, 5),
                  Pos(109, -1) }));
        string Spire(int status) =>
            C2bReply(d, 0, QuestHandlerKind.OnZoneIn, QC.Zone.SpireOfVahzl, null, C3aFake(m, status));
        Check("c3 C3a: 5-2 the Spire of Vahzl's zone-in plays 21 at Status 0, 20 at Status 1, nothing above",
              "Event(21)|Event(20)|null|null", string.Join("|", Enumerable.Range(0, 4).Select(Spire)));
        Check("c3 C3a: 5-2's Spire finishes: 20 sets Status 2; 21 sets the char down in Promyvion - Vahzl",
              $"Mission.SetVar(Status,2)|Player.SetPos(-140.011,0,-53.99,192,{vz})",
              C3aRun(d, fin, QC.Zone.SpireOfVahzl, 20, C3aFake(m)) + "|" +
              C3aRun(d, fin, QC.Zone.SpireOfVahzl, 21, C3aFake(m)));
        string Win(int win, int status)
        {
            var f = C3aFake(m, status);
            f.PlayerLocalVars["battlefieldWin"] = win;
            return C3aRun(d, fin, QC.Zone.SpireOfVahzl, 32001, f);
        }
        int dev = QC.Battlefield.DesiresOfEmptiness;
        Check("c3 C3a: 5-2's 32001 finish sets Status 3 and the char down in Beaucedine Glacier only when the win is " +
              "Desires of Emptiness and Status is 2, each condition alone",
              $"Mission.SetVar(Status,3),Player.SetPos(-340,-100.25,140,64,{QC.Zone.BeaucedineGlacier})||||",
              string.Join("|", new[]
              {
                  Win(dev, 2), Win(dev, 1), Win(dev, 3), Win(QC.Battlefield.OneToBeFeared, 2), Win(0, 2),
              }));
        ushort bg = QC.Zone.BeaucedineGlacier;
        Check("c3 C3a: 5-2 Beaucedine Glacier's Leigon-Moigon, Potete and Torino-Samarino play 212, 213, 211 " +
              "(important) at Status 4 only",
              "Event(212):importantEvent()|Event(213):importantEvent()|Event(211):importantEvent()|null|null|null",
              string.Join("|", new[]
              {
                  C3aReps(d, onTrig, bg, QC.Npc.BeaucedineGlacier.LeigonMoigon, m, (4, 0)),
                  C3aReps(d, onTrig, bg, QC.Npc.BeaucedineGlacier.Potete, m, (4, 0)),
                  C3aReps(d, onTrig, bg, QC.Npc.BeaucedineGlacier.TorinoSamarino, m, (4, 0)),
                  C3aReps(d, onTrig, bg, QC.Npc.BeaucedineGlacier.LeigonMoigon, m, (3, 0)),
                  C3aReps(d, onTrig, bg, QC.Npc.BeaucedineGlacier.Potete, m, (3, 0)),
                  C3aReps(d, onTrig, bg, QC.Npc.BeaucedineGlacier.TorinoSamarino, m, (5, 0)),
              }));
        Check("c3 C3a: 5-2 Beaucedine Glacier's zone-in plays 206 at Status 3 only; its update sets the amulet; its " +
              "finish sets Status 4",
              $"null|Event(206)|null|Player.UpdateEvent(0,{QC.KeyItem.MysteriousAmulet})|Mission.SetVar(Status,4)",
              string.Join("|", new[]
              {
                  C2bReply(d, 0, QuestHandlerKind.OnZoneIn, bg, null, C3aFake(m, 2)),
                  C2bReply(d, 0, QuestHandlerKind.OnZoneIn, bg, null, C3aFake(m, 3)),
                  C2bReply(d, 0, QuestHandlerKind.OnZoneIn, bg, null, C3aFake(m, 4)),
                  C3aRun(d, QuestHandlerKind.OnEventUpdate, bg, 206, C3aFake(m, 3)),
                  C3aRun(d, fin, bg, 206, C3aFake(m, 3)),
              }));
        Check("c3 C3a: 5-2 Metalworks' Cid plays 850 at Status 4 only; its finish completes the mission",
              "null|null|ProgressEvent(850)|Mission.Complete()",
              C3aReps(d, onTrig, QC.Zone.Metalworks, QC.Npc.Metalworks.Cid, m, (0, 0), (3, 0), (4, 0)) + "|" +
              C3aRun(d, fin, QC.Zone.Metalworks, 850, C3aFake(m, 4)));
        Check("c3 C3a: 5-2's completed state holds when 5-2 itself is completed, not when only 5-1 is",
              "True|False|False", C3aCompleted(d, m, QC.Mission.Cop.TheEnduringTumultOfWar));
        string Done(int option)
        {
            var f = new Fake { Option = option };
            return C2MRun(C2MHandler(d, 1, fin, QC.Zone.Psoxja, id: 112), f);
        }
        Check("c3 C3a: 5-2 completed: Psoxja's _i99 plays 112 and its finish sets the Vahzl position of the option",
              $"Event(112)|Player.SetPos(183.546,0,-59.906,0,{vz})|",
              C2MReply(C2MHandler(d, 1, onTrig, QC.Zone.Psoxja, QC.Npc.Psoxja.i99), new Fake()) + "|" + Done(2) + "|" +
              Done(0));
    }

    static void RunC3C3aVahzl()
    {
        var s = new QC.Battlefields.SpireOfVahzl.DesiresOfEmptiness();
        var bd = (BattlefieldMissionData)s.Data;
        Check("c3 C3a: Desires of Emptiness validates clean, its key and Lua file",
              "|battlefield.desires_of_emptiness scripts/battlefields/Spire_of_Vahzl/desires_of_emptiness",
              $"{string.Join(" | ", BattlefieldScriptValidator.Validate(s))}|{s.Key} {s.LegacyPath}");
        Check("c3 C3a: Desires of Emptiness's fields (zone, id, no exp loss, mission, trusts, players, MAX_LEVEL, " +
              "time, index, grantXP, title, entry and exit NPCs, mission, required var and value, groups)",
              $"{QC.Zone.SpireOfVahzl} {QC.Battlefield.DesiresOfEmptiness} False True True 6 MAX_LEVEL 1800 0 1500 " +
              "FrozenDeadBody _0n0 _0n1,_0n2,_0n3 6/518 Mission[6][518]Status 2 2",
              $"{bd.ZoneId} {bd.BattlefieldId} {bd.CanLoseExp} {bd.IsMission} {bd.AllowTrusts} {bd.MaxPlayers} " +
              $"{bd.LevelCapSetting} {bd.TimeLimit} {bd.Index} {bd.GrantXP} " +
              $"{(bd.Title == QC.Title.FrozenDeadBody ? "FrozenDeadBody" : bd.Title.ToString())} " +
              $"{string.Join(",", bd.EntryNpcs)} {string.Join(",", bd.ExitNpcs)} {(int)bd.MissionArea}/{bd.Mission} " +
              $"{bd.RequiredVar} {bd.RequiredValue} {bd.Groups.Count}");
        string Ids(BattlefieldGroup g) => string.Join(";", g.AreaMobIds.Select(a =>
            string.Join(",", a.Select(o => o.Key + ":" + o.Offset))));
        Check("c3 C3a: Desires of Emptiness's first group: three Agonizers to an area (0, 1, 6 +7 +14), spawned, " +
              "allDeath wins",
              "AGONIZER:0,AGONIZER:1,AGONIZER:6;AGONIZER:7,AGONIZER:8,AGONIZER:13;" +
              "AGONIZER:14,AGONIZER:15,AGONIZER:20  True",
              $"{Ids(bd.Groups[0])} {bd.Groups[0].Spawned} {bd.Groups[0].AllDeath != null}");
        Check("c3 C3a: Desires of Emptiness's second group: four Agonizers to an area (2 to 5 +7 +14), not spawned, " +
              "no allDeath",
              "AGONIZER:2,AGONIZER:3,AGONIZER:4,AGONIZER:5;AGONIZER:9,AGONIZER:10,AGONIZER:11,AGONIZER:12;" +
              "AGONIZER:16,AGONIZER:17,AGONIZER:18,AGONIZER:19 False False",
              $"{Ids(bd.Groups[1])} {bd.Groups[1].Spawned} {bd.Groups[1].AllDeath != null}");
        var bf = new BattlefieldViewFake();
        bd.Groups[0].AllDeath(new BattlefieldContextFake(s) { Battlefield = bf }, null);
        Check("c3 C3a: Desires of Emptiness's first group's allDeath sets the status to WON",
              "Battlefield.SetStatus(2)", string.Join(",", bf.Calls));
    }

    static void RunC3C3aVerse()
    {
        var onTrig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var ms = new QC.Missions.Cop.ForWhomTheVerseIsSung();
        var d = ms.Compile();
        int m = QC.Mission.Cop.ForWhomTheVerseIsSung;
        Check("c3 C3a: 6-1 is a CoP mission with its Lua file, A Place to Return next, no title; validates clean",
              $"mission.cop.for_whom_the_verse_is_sung scripts/missions/cop/6_1_For_Whom_the_Verse_is_Sung " +
              $"{QC.Mission.Cop.APlaceToReturn}  ",
              $"{ms.Key} {ms.LegacyPath} {d.Reward.NextMission} {d.Reward.Title} " +
              string.Join(" | ", QuestScriptValidator.Validate(d)));
        Check("c3 C3a: 6-1 Pherimociel plays 10046 at Status 0, 10052 (replacing) at Status 1, nothing above",
              "ProgressEvent(10046)|Event(10052):replaceDefault()|null|null",
              string.Join("|", Enumerable.Range(0, 4).Select(s =>
                  C2bReply(d, 0, onTrig, QC.Zone.RuludeGardens, QC.Npc.RuludeGardens.Pherimociel, C3aFake(m, s)))));
        Check("c3 C3a: 6-1 Ru'Lud Gardens' zone-in plays 10047 at Status 2 only",
              "null|null|Event(10047)|null",
              string.Join("|", Enumerable.Range(0, 4).Select(s =>
                  C2bReply(d, 0, QuestHandlerKind.OnZoneIn, QC.Zone.RuludeGardens, null, C3aFake(m, s)))));
        Check("c3 C3a: 6-1 Upper Jeuno's _6s1 plays 10011 at Status 1 only",
              "null|ProgressEvent(10011)|null|null",
              string.Join("|", Enumerable.Range(0, 4).Select(s =>
                  C2bReply(d, 0, onTrig, QC.Zone.UpperJeuno, QC.Npc.UpperJeuno._6s1, C3aFake(m, s)))));
        Check("c3 C3a: 6-1's finishes: 10046 sets Status 1, 10011 sets 2, 10047 completes",
              "Mission.SetVar(Status,1)|Mission.SetVar(Status,2)|Mission.Complete()",
              string.Join("|", new[]
              {
                  C3aRun(d, fin, QC.Zone.RuludeGardens, 10046, C3aFake(m)),
                  C3aRun(d, fin, QC.Zone.UpperJeuno, 10011, C3aFake(m)),
                  C3aRun(d, fin, QC.Zone.RuludeGardens, 10047, C3aFake(m)),
              }));
    }

    static void RunC3C3aPlace()
    {
        var onTrig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var ms = new QC.Missions.Cop.APlaceToReturn();
        var d = ms.Compile();
        int m = QC.Mission.Cop.APlaceToReturn;
        ushort rg = QC.Zone.RuludeGardens, coast = QC.Zone.MisareauxCoast;
        Check("c3 C3a: 6-2 is a CoP mission with its Lua file, More Questions than Answers next, no title; validates " +
              "clean",
              $"mission.cop.a_place_to_return scripts/missions/cop/6_2_A_Place_to_Return " +
              $"{QC.Mission.Cop.MoreQuestionsThanAnswers}  ",
              $"{ms.Key} {ms.LegacyPath} {d.Reward.NextMission} {d.Reward.Title} " +
              string.Join(" | ", QuestScriptValidator.Validate(d)));
        Check("c3 C3a: 6-2 Pherimociel plays 28 and Auchefort 10055 at Status 1 only",
              "null|Event(28)|null|null|Event(10055)|null",
              C3aReps(d, onTrig, rg, QC.Npc.RuludeGardens.Pherimociel, m, (0, 0), (1, 0), (2, 0)) + "|" +
              C3aReps(d, onTrig, rg, QC.Npc.RuludeGardens.Auchefort, m, (0, 0), (1, 0), (2, 0)));
        Check("c3 C3a: 6-2's bystanders play 158 (Adolie), 12 (Chapi Galepilai), 22 (Colti) and 31 (Neraf-Najiruf, " +
              "once per zone) whatever the status",
              "Event(158)|Event(158)|Event(12)|Event(22)|Event(31):oncePerZone()|Event(31):oncePerZone()",
              string.Join("|", new[]
              {
                  C3aReps(d, onTrig, rg, QC.Npc.RuludeGardens.Adolie, m, (0, 0), (2, 0)),
                  C3aReps(d, onTrig, rg, QC.Npc.RuludeGardens.ChapiGalepilai, m, (1, 0)),
                  C3aReps(d, onTrig, rg, QC.Npc.RuludeGardens.Colti, m, (3, 0)),
                  C3aReps(d, onTrig, rg, QC.Npc.RuludeGardens.NerafNajiruf, m, (0, 0), (1, 0)),
              }));
        Check("c3 C3a: 6-2's trigger area 1 plays 10048 at Status 0 only; its finish sets Status 1",
              "ProgressEvent(10048)|null|null|Mission.SetVar(Status,1)",
              string.Join("|", new[]
              {
                  C2bReply(d, 0, QuestHandlerKind.OnTriggerAreaEnter, rg, null, C3aFake(m, 0), 1),
                  C2bReply(d, 0, QuestHandlerKind.OnTriggerAreaEnter, rg, null, C3aFake(m, 1), 1),
                  C2bReply(d, 0, QuestHandlerKind.OnTriggerAreaEnter, rg, null, C3aFake(m, 2), 1),
                  C3aRun(d, fin, rg, 10048, C3aFake(m)),
              }));
        string Gate(int status, int option, params string[] spawned)
        {
            var f = C3aFake(m, status, option);
            foreach (var s in spawned) f.SpawnedMobs.Add(QC.Mob.MisareauxCoast.PM6_2_MOB_OFFSET + "+" + s);
            return C2MReply(C2MHandler(d, 0, onTrig, coast, QC.Npc.MisareauxCoast._0p0), f) + "/" +
                   string.Join(",", f.Calls);
        }
        string pop = "Player.SpawnMob(PM6_2_MOB_OFFSET,0,True),Player.SpawnMob(PM6_2_MOB_OFFSET,1,True)," +
                     "Player.SpawnMob(PM6_2_MOB_OFFSET,2,True)";
        Check("c3 C3a: 6-2 Misareaux Coast's _0p0 at Status 1: Option 7 plays cutscene 10; else with none of the " +
              "three Wardens spawned it pops them all claimed (no reply); any one spawned (each alone) it does " +
              "nothing; Option 3 (not 7) pops them too; not at Status 0 or 2",
              $"ProgressCutscene(10)/|NoAction()/{pop}|null/|null/|null/|NoAction()/{pop}|null/|null/",
              string.Join("|", new[]
              {
                  Gate(1, 7), Gate(1, 0), Gate(1, 0, "0"), Gate(1, 0, "1"), Gate(1, 0, "2"), Gate(1, 3),
                  Gate(0, 7), Gate(2, 7),
              }));
        string Warder(string npc, int status)
        {
            var f = C3aFake(m, status);
            f.MobDeath = new QuestMobDeathFake();
            return C2MRun(C2MHandler(d, 0, QuestHandlerKind.OnMobDeath, coast, npc), f);
        }
        Check("c3 C3a: 6-2's Warders' deaths set Option bit 0 (Aglaia), 1 (Euphrosyne), 2 (Thalia) at Status 1 only",
              "Mission.SetVarBit(Option,0)|Mission.SetVarBit(Option,1)|Mission.SetVarBit(Option,2)|||",
              string.Join("|", new[]
              {
                  Warder(QC.Npc.MisareauxCoast.WarderAglaia, 1), Warder(QC.Npc.MisareauxCoast.WarderEuphrosyne, 1),
                  Warder(QC.Npc.MisareauxCoast.WarderThalia, 1), Warder(QC.Npc.MisareauxCoast.WarderAglaia, 0),
                  Warder(QC.Npc.MisareauxCoast.WarderEuphrosyne, 2), Warder(QC.Npc.MisareauxCoast.WarderThalia, 0),
              }));
        Check("c3 C3a: 6-2's finish of 10 (Misareaux Coast) completes the mission", "Mission.Complete()",
              C3aRun(d, fin, coast, 10, C3aFake(m, 1, 7)));
    }

    static void RunC3C3aQuestions()
    {
        var onTrig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var ms = new QC.Missions.Cop.MoreQuestionsThanAnswers();
        var d = ms.Compile();
        int m = QC.Mission.Cop.MoreQuestionsThanAnswers;
        ushort rg = QC.Zone.RuludeGardens;
        Check("c3 C3a: 6-3 is a CoP mission with its Lua file, One to be Feared next, no title; validates clean",
              $"mission.cop.more_questions_than_answers scripts/missions/cop/6_3_More_Questions_than_Answers " +
              $"{QC.Mission.Cop.OneToBeFeared}  ",
              $"{ms.Key} {ms.LegacyPath} {d.Reward.NextMission} {d.Reward.Title} " +
              string.Join(" | ", QuestScriptValidator.Validate(d)));
        Check("c3 C3a: 6-3 Ru'Lud Gardens' _6r9 plays 10050 at Status 1 only",
              "null|ProgressEvent(10050)|null",
              C3aReps(d, onTrig, rg, QC.Npc.RuludeGardens._6r9, m, (0, 0), (1, 0), (2, 0)));
        Check("c3 C3a: 6-3 Pherimociel plays 10049 at Status 0, 10053 at Status 1, 10063 at Status 2, nothing above",
              "ProgressEvent(10049)|Event(10053)|Event(10063)|null",
              C3aReps(d, onTrig, rg, QC.Npc.RuludeGardens.Pherimociel, m, (0, 0), (1, 0), (2, 0), (3, 0)));
        Check("c3 C3a: 6-3 Auchefort plays 10054 at Status 2, 9 at every other status",
              "Event(9)|Event(9)|Event(10054)|Event(9)",
              C3aReps(d, onTrig, rg, QC.Npc.RuludeGardens.Auchefort, m, (0, 0), (1, 0), (2, 0), (3, 0)));
        Check("c3 C3a: 6-3's bystanders play 158 (Adolie), 12 (Chapi Galepilai), 22 (Colti) and 31 (Neraf-Najiruf, " +
              "once per zone) whatever the status",
              "Event(158)|Event(158)|Event(12)|Event(22)|Event(31):oncePerZone()|Event(31):oncePerZone()",
              string.Join("|", new[]
              {
                  C3aReps(d, onTrig, rg, QC.Npc.RuludeGardens.Adolie, m, (0, 0), (2, 0)),
                  C3aReps(d, onTrig, rg, QC.Npc.RuludeGardens.ChapiGalepilai, m, (1, 0)),
                  C3aReps(d, onTrig, rg, QC.Npc.RuludeGardens.Colti, m, (3, 0)),
                  C3aReps(d, onTrig, rg, QC.Npc.RuludeGardens.NerafNajiruf, m, (0, 0), (1, 0)),
              }));
        Check("c3 C3a: 6-3 Selbina's Mathilde plays 10005 at Status 2 only",
              "null|null|ProgressEvent(10005)|null",
              C3aReps(d, onTrig, QC.Zone.Selbina, QC.Npc.Selbina.Mathilde, m, (0, 0), (1, 0), (2, 0), (3, 0)));
        Check("c3 C3a: 6-3's finishes: 10049 sets Status 1, 10050 sets 2, Selbina 10005 completes",
              "Mission.SetVar(Status,1)|Mission.SetVar(Status,2)|Mission.Complete()",
              string.Join("|", new[]
              {
                  C3aRun(d, fin, rg, 10049, C3aFake(m)), C3aRun(d, fin, rg, 10050, C3aFake(m)),
                  C3aRun(d, fin, QC.Zone.Selbina, 10005, C3aFake(m)),
              }));
    }

    static void RunC3C3aFeared()
    {
        var onTrig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var ms = new QC.Missions.Cop.OneToBeFeared();
        var d = ms.Compile();
        int m = QC.Mission.Cop.OneToBeFeared;
        ushort saf = QC.Zone.TavnazianSafehold, den = QC.Zone.SealionsDen;
        Check("c3 C3a: 6-4 is a CoP mission with its Lua file, Chains and Bonds next, no title; validates clean",
              $"mission.cop.one_to_be_feared scripts/missions/cop/6_4_One_to_be_Feared " +
              $"{QC.Mission.Cop.ChainsAndBonds}  ",
              $"{ms.Key} {ms.LegacyPath} {d.Reward.NextMission} {d.Reward.Title} " +
              string.Join(" | ", QuestScriptValidator.Validate(d)));
        Check("c3 C3a: 6-4 Metalworks' Cid plays 856 at Status 0 only; its finish sets Status 1",
              "ProgressEvent(856)|null|null|Mission.SetVar(Status,1)",
              C3aReps(d, onTrig, QC.Zone.Metalworks, QC.Npc.Metalworks.Cid, m, (0, 0), (1, 0), (2, 0)) + "|" +
              C3aRun(d, fin, QC.Zone.Metalworks, 856, C3aFake(m)));
        Check("c3 C3a: 6-4 Tavnazian Safehold's Despachiaire, Ironclad Gorilla and Justinius play 318, 306 and 267 " +
              "whatever the status",
              "Event(318)|Event(318)|Event(306)|Event(306)|Event(267)|Event(267)",
              string.Join("|", new[]
              {
                  C3aReps(d, onTrig, saf, QC.Npc.TavnazianSafehold.Despachiaire, m, (0, 0), (3, 0)),
                  C3aReps(d, onTrig, saf, QC.Npc.TavnazianSafehold.IroncladGorilla, m, (0, 0), (3, 0)),
                  C3aReps(d, onTrig, saf, QC.Npc.TavnazianSafehold.Justinius, m, (0, 0), (3, 0)),
              }));
        Check("c3 C3a: 6-4 Sealion's Den's _0w0 plays 31 and Sueleen 24 at Status 2 only",
              "null|ProgressEvent(31)|null|null|Event(24)|null",
              C3aReps(d, onTrig, den, QC.Npc.SealionsDen._0w0, m, (1, 0), (2, 0), (3, 0)) + "|" +
              C3aReps(d, onTrig, den, QC.Npc.SealionsDen.Sueleen, m, (1, 0), (2, 0), (3, 0)));
        Check("c3 C3a: 6-4 Sealion's Den's zone-in plays 15 at Status 1, 33 at Status 4, nothing at 0, 2, 3",
              "null|Event(15)|null|null|Event(33)",
              string.Join("|", new[] { 0, 1, 2, 3, 4 }.Select(s =>
                  C2bReply(d, 0, QuestHandlerKind.OnZoneIn, den, null, C3aFake(m, s)))));
        string Upd(int option)
        {
            var f = C3aFake(m, 1);
            f.Option = option;
            return C3aRun(d, QuestHandlerKind.OnEventUpdate, den, 15, f);
        }
        Check("c3 C3a: 6-4's update of 15 with option 0 does updateEvent(0, 0, 0, 0, 32, 2, 0, 0); other options " +
              "nothing",
              "Player.UpdateEvent(0,0,0,0,32,2,0,0)||",
              string.Join("|", new[] { Upd(0), Upd(1), Upd(-1) }));
        Check("c3 C3a: 6-4's Sealion's Den finishes: 15 sets Status 2, 31 sets 3, 33 completes and sets the char " +
              "down in Lufaise Meadows",
              $"Mission.SetVar(Status,2)|Mission.SetVar(Status,3)|" +
              $"Mission.Complete(),Player.SetPos(407.389,-0.262,-72.964,96,{QC.Zone.LufaiseMeadows})",
              string.Join("|", new[]
              {
                  C3aRun(d, fin, den, 15, C3aFake(m)), C3aRun(d, fin, den, 31, C3aFake(m)),
                  C3aRun(d, fin, den, 33, C3aFake(m)),
              }));
        string Win(int win, int status)
        {
            var f = C3aFake(m, status);
            f.PlayerLocalVars["battlefieldWin"] = win;
            return C3aRun(d, fin, den, 32001, f);
        }
        int otbf = QC.Battlefield.OneToBeFeared;
        Check("c3 C3a: 6-4's 32001 finish sets Status 4 and the char down in Sealion's Den only when the win is One " +
              "to be Feared and Status is 3, each condition alone",
              $"Mission.SetVar(Status,4),Player.SetPos(612.057,132.664,776.92,188,{den})||||",
              string.Join("|", new[]
              {
                  Win(otbf, 3), Win(otbf, 2), Win(otbf, 4), Win(QC.Battlefield.DesiresOfEmptiness, 3), Win(0, 3),
              }));
        Check("c3 C3a: 6-4's completed state holds when 6-4 itself is completed, not when only 6-3 is",
              "True|False|False", C3aCompleted(d, m, QC.Mission.Cop.MoreQuestionsThanAnswers));
        Check("c3 C3a: 6-4 completed: Tavnazian Safehold's Ironclad Gorilla plays 306, replacing the default",
              "Event(306):replaceDefault()",
              C2MReply(C2MHandler(d, 1, onTrig, saf, QC.Npc.TavnazianSafehold.IroncladGorilla), new Fake()));
    }

    static void RunC3C3aFearedBattlefield()
    {
        var s = new QC.Battlefields.SealionsDen.OneToBeFeared();
        var bd = (BattlefieldMissionData)s.Data;
        int den = QC.Zone.SealionsDen;
        Check("c3 C3a: One to be Feared validates clean, its key and Lua file",
              "|battlefield.one_to_be_feared scripts/battlefields/Sealions_Den/one_to_be_feared",
              $"{string.Join(" | ", BattlefieldScriptValidator.Validate(s))}|{s.Key} {s.LegacyPath}");
        Check("c3 C3a: One to be Feared's fields (zone, id, no exp loss, mission, trusts, players, MAX_LEVEL, time, " +
              "index, grantXP, title, entry and exit NPCs, mission, required var and value, groups)",
              $"{den} {QC.Battlefield.OneToBeFeared} False True True 6 MAX_LEVEL 2700 0 1500 UltimaUndertaker _0w0 " +
              "Airship_Door 6/638 Mission[6][638]Status 3 3",
              $"{bd.ZoneId} {bd.BattlefieldId} {bd.CanLoseExp} {bd.IsMission} {bd.AllowTrusts} {bd.MaxPlayers} " +
              $"{bd.LevelCapSetting} {bd.TimeLimit} {bd.Index} {bd.GrantXP} " +
              $"{(bd.Title == QC.Title.UltimaUndertaker ? "UltimaUndertaker" : bd.Title.ToString())} " +
              $"{string.Join(",", bd.EntryNpcs)} {string.Join(",", bd.ExitNpcs)} {(int)bd.MissionArea}/{bd.Mission} " +
              $"{bd.RequiredVar} {bd.RequiredValue} {bd.Groups.Count}");
        string Ids(BattlefieldGroup g) => string.Join(";", g.AreaMobIds.Select(a => string.Join(",", a.Select(o =>
            o.Key + ":" + o.Offset))));
        Check("c3 C3a: One to be Feared's first group: five Zetas to an area (0 to 4, 7 to 11, 14 to 18), spawned",
              "MAMMET_22_ZETA:0,MAMMET_22_ZETA:1,MAMMET_22_ZETA:2,MAMMET_22_ZETA:3,MAMMET_22_ZETA:4;" +
              "MAMMET_22_ZETA:7,MAMMET_22_ZETA:8,MAMMET_22_ZETA:9,MAMMET_22_ZETA:10,MAMMET_22_ZETA:11;" +
              "MAMMET_22_ZETA:14,MAMMET_22_ZETA:15,MAMMET_22_ZETA:16,MAMMET_22_ZETA:17,MAMMET_22_ZETA:18 ",
              $"{Ids(bd.Groups[0])} {bd.Groups[0].Spawned}");
        Check("c3 C3a: One to be Feared's Omega group (5, 12, 19) and Ultima group (6, 13, 20), neither spawned",
              "MAMMET_22_ZETA:5;MAMMET_22_ZETA:12;MAMMET_22_ZETA:19 False|" +
              "MAMMET_22_ZETA:6;MAMMET_22_ZETA:13;MAMMET_22_ZETA:20 False",
              $"{Ids(bd.Groups[1])} {bd.Groups[1].Spawned}|{Ids(bd.Groups[2])} {bd.Groups[2].Spawned}");
        BattlefieldPlayerFake Member(int id, bool battlefield = true)
        {
            var p = new BattlefieldPlayerFake { ZoneId = den };
            if (battlefield) p.StatusEffects.Add(BattlefieldConst.EffectBattlefield);
            return p;
        }
        string Starts(BattlefieldGroup g)
        {
            var a = Member(1);
            var b = Member(2);
            var bf = new BattlefieldViewFake();
            bf.PlayerList.Add(a);
            bf.PlayerList.Add(b);
            g.AllDeath(new BattlefieldContextFake(s) { Battlefield = bf }, null);
            return string.Join(",", a.Calls) + "|" + string.Join(",", b.Calls) + "|" + string.Join(",", bf.Calls);
        }
        Check("c3 C3a: the first group's allDeath starts event 10 for every player; the Omega group's event 11; " +
              "the Ultima group's wins the fight",
              "Player.StartEvent(10)|Player.StartEvent(10)|;Player.StartEvent(11)|Player.StartEvent(11)|;||" +
              "Battlefield.SetStatus(2)",
              Starts(bd.Groups[0]) + ";" + Starts(bd.Groups[1]) + ";" + Starts(bd.Groups[2]));
        // the section handlers
        BattlefieldContextFake Ctx(int area, out BattlefieldPlayerFake p, out BattlefieldEngineFake e)
        {
            p = Member(1);
            p.CurrentBattlefield = new BattlefieldViewFake { Area = area, Id = QC.Battlefield.OneToBeFeared };
            e = new BattlefieldEngineFake();
            e.ZoneMobIds[$"{den}:MAMMET_22_ZETA"] = 1000;
            for (int i = 1000; i < 1021; i++) e.MobsById[i] = new BattlefieldMobFake { Id = i };
            return new BattlefieldContextFake(s) { Player = p, Engine = e };
        }
        string Update(int csid, int area, int option, bool already = false)
        {
            var q = Ctx(area, out _, out var e);
            if (already)
                foreach (var id in new[] { 1005 + 7 * (area - 1), 1006 + 7 * (area - 1) })
                    ((BattlefieldMobFake)e.MobsById[id]).IsSpawned = true;
            bd.OnEventUpdate[csid](q, csid, option);
            return string.Join(",", e.Calls);
        }
        Check("c3 C3a: the update of 1 with option 0 spawns Omega (area offset + 5) unless spawned, the update of 2 " +
              "Ultima (+ 6); another option does nothing",
              "SpawnMob(1005)|SpawnMob(1012)|SpawnMob(1019)||SpawnMob(1006)|SpawnMob(1013)|SpawnMob(1020)|||",
              string.Join("|", new[]
              {
                  Update(1, 1, 0), Update(1, 2, 0), Update(1, 3, 0), Update(1, 1, 0, true),
                  Update(2, 1, 0), Update(2, 2, 0), Update(2, 3, 0), Update(2, 1, 0, true), Update(1, 1, 1),
                  Update(2, 1, 3),
              }));
        string Finish(int csid, int area, bool dead, bool pet)
        {
            var q = Ctx(area, out var p, out _);
            p.MaxHP = 900;
            p.MaxMP = 400;
            p.IsAlive = !dead;
            p.IsDead = dead;
            var petMob = new BattlefieldMobFake { MaxHP = 50, MaxMP = 20 };
            if (pet) p.PetMob = petMob;
            bd.OnEventFinish[csid](q, csid, 0);
            return string.Join(",", p.Calls) + (pet ? "/" + string.Join(",", petMob.Calls) : "");
        }
        string heal = "Player.SetHP(900),Player.SetMP(400),Player.SetTP(0)";
        Check("c3 C3a: the finish of 10 gives Branded by Lightning, heals, sets the char down on the airship of its " +
              "area and sets [OTBF]battleCompleted 1",
              $"Player.AddTitle({QC.Title.BrandedByLightning}),{heal},Player.SetPos(-780.01,-103.348,-86.327,193)," +
              "Player.SetLocalVar([OTBF]battleCompleted,1)",
              Finish(10, 1, false, false));
        Check("c3 C3a: the finish of 11 gives Omega Ostracizer and sets [OTBF]battleCompleted 2 (areas 2 and 3's " +
              "airship positions)",
              $"Player.AddTitle({QC.Title.OmegaOstracizer}),{heal},Player.SetPos(-140.029,-23.348,-446.376,193)," +
              "Player.SetLocalVar([OTBF]battleCompleted,2)|" +
              $"Player.AddTitle({QC.Title.OmegaOstracizer}),{heal},Player.SetPos(499.969,56.652,-806.132,193)," +
              "Player.SetLocalVar([OTBF]battleCompleted,2)",
              Finish(11, 2, false, false) + "|" + Finish(11, 3, false, false));
        Check("c3 C3a: the finish heals a living char's pet too; a dead char is not healed and is allowed the raise " +
              "prompt",
              $"Player.AddTitle({QC.Title.BrandedByLightning}),{heal},Player.SetPos(-780.01,-103.348,-86.327,193)," +
              "Player.SetLocalVar([OTBF]battleCompleted,1)/Mob.SetHP(50),Mob.SetMP(20),Mob.SetTP(0)|" +
              $"Player.AddTitle({QC.Title.BrandedByLightning}),Player.SetPos(-780.01,-103.348,-86.327,193)," +
              "Player.AllowSendRaisePrompt(),Player.SetLocalVar([OTBF]battleCompleted,1)",
              Finish(10, 1, false, true) + "|" + Finish(10, 1, true, false));
        // the airship door: the exit trigger and the exit finish
        string Door(int doorId, int completed)
        {
            var q = Ctx(1, out var p, out var e);
            e.ZoneNpcIds[$"{den}:AIRSHIP_DOOR_OFFSET"] = 500;
            p.PlayerLocalVars["[OTBF]battleCompleted"] = completed;
            q.Npc = new BattlefieldNpcFake { Id = doorId, Name = "Airship_Door" };
            return s.OnExitTrigger(q).ToString();
        }
        Check("c3 C3a: the airship door plays 32003 (priority 1001) with its number (npc id - offset + 1) and twice " +
              "[OTBF]battleCompleted",
              "progress(32003,1,0;1001)|progress(32003,2,2;1001)|progress(32003,3,4;1001)",
              $"{Door(500, 0)}|{Door(501, 1)}|{Door(502, 2)}");
        string ExitFinish(int option, bool inParty, bool memberHasEffect, bool inBattlefield)
        {
            var q = Ctx(1, out var p, out _);
            p.PlayerLocalVars["[OTBF]battleCompleted"] = 1;
            var other = Member(2, memberHasEffect);
            other.PlayerLocalVars["[OTBF]battleCompleted"] = 2;
            p.PartyList = inParty ? new List<IBattlefieldPlayer> { p, other } : new List<IBattlefieldPlayer>();
            if (!inBattlefield) p.CurrentBattlefield = null;
            s.OnEventFinishExit(q, 32003, option);
            return string.Join(",", p.Calls) + "/" + string.Join(",", other.Calls);
        }
        Check("c3 C3a: the exit finish with option 100 to 102 starts battleCompleted with option - 99 for every " +
              "party " +
              "member holding the battlefield effect, or for the char alone without a party; others nothing",
              "Player.StartEvent(1,1)/Player.StartEvent(2,1)|Player.StartEvent(1,2)/Player.StartEvent(2,2)|" +
              "Player.StartEvent(1,3)/Player.StartEvent(2,3)|Player.StartEvent(1,1)/|/|Player.StartEvent(1,1)/",
              string.Join("|", new[]
              {
                  ExitFinish(100, true, true, true), ExitFinish(101, true, true, true),
                  ExitFinish(102, true, true, true), ExitFinish(100, false, true, true),
                  ExitFinish(103, true, true, true), ExitFinish(100, true, false, true),
              }));
        {
            var q = Ctx(1, out var p, out _);
            p.PlayerLocalVars["[OTBF]battleCompleted"] = 1;
            p.StatusEffects.Clear();
            p.PartyList = new List<IBattlefieldPlayer> { p };
            s.OnEventFinishExit(q, 32003, 100);
            Check("c3 C3a: the exit finish with a party of one char lacking the battlefield effect starts nothing " +
                  "(the party list, not the char, is walked)", "", string.Join(",", p.Calls));
        }
        Check("c3 C3a: the exit finish with option 99 starts nothing",
              "/", ExitFinish(99, true, true, true));
        Check("c3 C3a: the exit finish with option 4 leaves the battlefield and clears [OTBF]battleCompleted, only " +
              "while in one; other options (3, 5) do nothing",
              "Player.LeaveBattlefield(1),Player.SetLocalVar([OTBF]battleCompleted,0)/|/|/|/",
              string.Join("|", new[]
              {
                  ExitFinish(4, true, true, true), ExitFinish(4, true, true, false), ExitFinish(3, true, true, true),
                  ExitFinish(5, true, true, true),
              }));
    }
}
