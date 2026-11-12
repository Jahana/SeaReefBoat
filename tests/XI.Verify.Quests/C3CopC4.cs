// XI.Verify.Quests -- quest rework C3 C4: CoP 7-1 to 8-4 and The Warrior's Path, When Angels Fall and Dawn. Each
// handler's arms one at a time (each part of a multi-part condition alone), the values no trace sees (the events other
// scripts outrank, positions, priorities, the battlefieldWin part of the 32001 finishes, each Cryptonberry Assassin's
// bit (their deaths reached by #assassins), the unreachable invalid trigger NPC, Tenzen's group), the new facet
// members, and the data of the three battlefields against the Lua files' (as they stood at the recording, e05c9c27).
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    internal static void RunC3CopC4()
    {
        Console.WriteLine("\n== c3 C4: CoP 7-1 to 8-4, The Warrior's Path, When Angels Fall and Dawn ==");
        RunC4ChainsAndBonds();
        RunC4Flames();
        RunC4FireInTheEyes();
        RunC4CalmBeforeTheStorm();
        RunC4WarriorsPath();
        RunC4WarriorsPathBattlefield();
        RunC4GardenOfAntiquity();
        RunC4FateDecided();
        RunC4WhenAngelsFall();
        RunC4WhenAngelsFallBattlefield();
        RunC4Dawn();
        RunC4DawnBattlefield();
    }

    // a fake with the mission current at Status, plus vars
    static Fake C4Fake(int mission, int status = 0, params (string Name, int Value)[] vars)
    {
        var f = C2bFake(mission, status);
        foreach (var (name, value) in vars) f.MissionVars[$"{C2bCop}/{mission}/{name}"] = value;
        return f;
    }

    // whether a state matches the fake (its current-ness and its guard)
    static bool C4Holds(MissionScriptDefinition d, int state, Fake f, int mission)
    {
        var m = d.States[state].MissionMatches[0];
        bool current = f.CurrentMission(C2bCop) == mission;
        return (m.IsCurrent == null || m.IsCurrent == current) && (m.When == null || m.When(f));
    }

    static string C4Header(MissionScript ms, MissionScriptDefinition d) =>
        $"{ms.Key} {ms.LegacyPath} {d.Reward.NextMission} {d.Reward.Title} " +
        string.Join(" | ", QuestScriptValidator.Validate(d));

    static void RunC4ChainsAndBonds()
    {
        var onTrig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var zin = QuestHandlerKind.OnZoneIn;
        var ms = new QC.Missions.Cop.ChainsAndBonds();
        var d = ms.Compile();
        int m = QC.Mission.Cop.ChainsAndBonds;
        ushort lm = QC.Zone.LufaiseMeadows, saf = QC.Zone.TavnazianSafehold, sd = QC.Zone.SealionsDen;
        Fake F(int status, int ring = 0, int option = 0) =>
            C4Fake(m, status, ("RingStashed", ring), ("Option", option));
        string R(QuestHandlerKind kind, ushort zone, string npc, Fake f, int id = 1) =>
            C2bReply(d, 0, kind, zone, npc, f, id);
        string Run(QuestHandlerKind kind, ushort zone, int csid, Fake f) =>
            C2MRun(C2MHandler(d, 0, kind, zone, id: csid), f);

        Check("c3 C4: 7-1 is a CoP mission with its Lua file, Flames in the Darkness next, no title; one state, the " +
              "mission current; validates clean",
              $"mission.cop.chains_and_bonds scripts/missions/cop/7_1_Chains_and_Bonds " +
              $"{QC.Mission.Cop.FlamesInTheDarkness}  |1 True",
              C4Header(ms, d) + $"|{d.States.Count} {d.States[0].MissionMatches[0].IsCurrent}");
        Check("c3 C4: 7-1 Lufaise Meadows' zone-in plays 111 only at Status 0 with RingStashed 0, each alone",
              "Event(111)|null|null",
              string.Join("|", new[] { R(zin, lm, null, F(0)), R(zin, lm, null, F(1)), R(zin, lm, null, F(0, 1)) }));
        Check("c3 C4: 7-1 qm_chains_and_bonds plays 114 at RingStashed 1 only",
              "ProgressEvent(114)|null|null",
              string.Join("|", new[] { 1, 0, 2 }.Select(v =>
                  R(onTrig, lm, QC.Npc.LufaiseMeadows.qmchainsandbonds, F(0, v)))));
        Check("c3 C4: 7-1 update 111 sends (0, the Ducal Guard's Ring)",
              $"Player.UpdateEvent(0,{QC.Item.DucalGuardsRing})",
              C2MRun(C2MHandler(d, 0, QuestHandlerKind.OnEventUpdate, lm, id: 111), F(0)));
        string Fin111(int slots, bool give)
        {
            var f = F(0);
            f.FreeSlots = slots;
            f.GiveResult = give;
            return Run(fin, lm, 111, f);
        }
        int ring = QC.Item.DucalGuardsRing;
        Check("c3 C4: 7-1 finish 111: no free slot says NO_ROOM_COME_BACK_LATER (the ring) and sets RingStashed 1 " +
              "without a give; with room the ring given sets Status 1; a failed give nothing more",
              $"Text.Special({lm}:NO_ROOM_COME_BACK_LATER,{ring}),Mission.SetVar(RingStashed,1)|" +
              $"Player.GiveItem({ring}),Mission.SetVar(Status,1)|Player.GiveItem({ring})",
              Fin111(0, true) + "|" + Fin111(1, true) + "|" + Fin111(1, false));
        string Fin114(bool give)
        {
            var f = F(0, 1);
            f.GiveResult = give;
            return Run(fin, lm, 114, f);
        }
        Check("c3 C4: 7-1 Lufaise's finish 114: the ring given sets RingStashed 0 then Status 1; a failed give nothing",
              $"Player.GiveItem({ring}),Mission.SetVar(RingStashed,0),Mission.SetVar(Status,1)|Player.GiveItem({ring})",
              Fin114(true) + "|" + Fin114(false));
        Check("c3 C4: 7-1 _0qa plays 115 while Option bit 0 is unset, _0q1 116 while bit 1 is (the other bits alone " +
              "change nothing)",
              "ProgressEvent(115)|null|ProgressEvent(115)|ProgressEvent(116)|null|ProgressEvent(116)",
              string.Join("|", new[]
              {
                  R(onTrig, saf, QC.Npc.TavnazianSafehold._0qa, F(2, 0, 0)),
                  R(onTrig, saf, QC.Npc.TavnazianSafehold._0qa, F(2, 0, 1)),
                  R(onTrig, saf, QC.Npc.TavnazianSafehold._0qa, F(2, 0, 6)),
                  R(onTrig, saf, QC.Npc.TavnazianSafehold._0q1, F(2, 0, 0)),
                  R(onTrig, saf, QC.Npc.TavnazianSafehold._0q1, F(2, 0, 2)),
                  R(onTrig, saf, QC.Npc.TavnazianSafehold._0q1, F(2, 0, 5)),
              }));
        Check("c3 C4: 7-1 Despachiaire plays the event 318 and Justinius 135 at any status (the traces show other " +
              "quests' events winning over them)",
              "Event(318)|Event(318)|Event(135)|Event(135)",
              string.Join("|", new[]
              {
                  R(onTrig, saf, QC.Npc.TavnazianSafehold.Despachiaire, F(0)),
                  R(onTrig, saf, QC.Npc.TavnazianSafehold.Despachiaire, F(2)),
                  R(onTrig, saf, QC.Npc.TavnazianSafehold.Justinius, F(0)),
                  R(onTrig, saf, QC.Npc.TavnazianSafehold.Justinius, F(2)),
              }));
        Check("c3 C4: 7-1 Tavnazian Safehold's zone-in plays 114 at Status 1 only; Sealion's Den's plays 14 while " +
              "Option bit 2 is unset",
              "null|Event(114)|null|Event(14)|null|Event(14)",
              string.Join("|", new[]
              {
                  R(zin, saf, null, F(0)), R(zin, saf, null, F(1)), R(zin, saf, null, F(2)),
                  R(zin, sd, null, F(2, 0, 0)), R(zin, sd, null, F(2, 0, 4)), R(zin, sd, null, F(2, 0, 3)),
              }));
        Check("c3 C4: 7-1 Tavnazian's finish 114 sets Status 2",
              "Mission.SetVar(Status,2)", Run(fin, saf, 114, F(1)));
        Check("c3 C4: 7-1 finishes 115, 116 and 14 set their bit and complete only when Option is then 7",
              "Mission.SetVarBit(Option,0),Mission.Complete()|Mission.SetVarBit(Option,0)|" +
              "Mission.SetVarBit(Option,1),Mission.Complete()|Mission.SetVarBit(Option,1)|" +
              "Mission.SetVarBit(Option,2),Mission.Complete()|Mission.SetVarBit(Option,2)",
              string.Join("|", new[]
              {
                  Run(fin, saf, 115, F(2, 0, 6)), Run(fin, saf, 115, F(2, 0, 2)),
                  Run(fin, saf, 116, F(2, 0, 5)), Run(fin, saf, 116, F(2, 0, 1)),
                  Run(fin, sd, 14, F(2, 0, 3)), Run(fin, sd, 14, F(2, 0, 2)),
              }));
    }

    static void RunC4Flames()
    {
        var onTrig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var ms = new QC.Missions.Cop.FlamesInTheDarkness();
        var d = ms.Compile();
        int m = QC.Mission.Cop.FlamesInTheDarkness;
        ushort mc = QC.Zone.MisareauxCoast, sd = QC.Zone.SealionsDen;
        ushort rg = QC.Zone.RuludeGardens, uj = QC.Zone.UpperJeuno;
        string R(QuestHandlerKind kind, ushort zone, string npc, int status, int id = 1) =>
            C2bReply(d, 0, kind, zone, npc, C4Fake(m, status), id);
        string Over(ushort zone, string npc, params int[] statuses) =>
            string.Join("|", statuses.Select(s => R(onTrig, zone, npc, s)));
        string Run(ushort zone, int csid, int status) =>
            C2MRun(C2MHandler(d, 0, fin, zone, id: csid), C4Fake(m, status));

        Check("c3 C4: 7-2 is a CoP mission with its Lua file, Fire in the Eyes of Men next, the Eshantarl's Comrade " +
              "in Arms title; validates clean",
              $"mission.cop.flames_in_the_darkness scripts/missions/cop/7_2_Flames_in_the_Darkness " +
              $"{QC.Mission.Cop.FireInTheEyesOfMen} {QC.Title.EshantarlsComradeInArms} |True",
              C4Header(ms, d) + $"|{d.States[0].MissionMatches[0].IsCurrent}");
        Check("c3 C4: 7-2 _0p2 plays the cutscene 12 at Status 0, Sueleen 16 at 1, _6s1 10012 at 3, each at no other",
              "ProgressCutscene(12)|null|null|ProgressEvent(16)|null|null|ProgressEvent(10012)|null",
              Over(mc, QC.Npc.MisareauxCoast._0p2, 0, 1) + "|" + Over(sd, QC.Npc.SealionsDen.Sueleen, 0, 1, 2) + "|" +
              Over(uj, QC.Npc.UpperJeuno._6s1, 2, 3, 4));
        Check("c3 C4: 7-2 Ru'Lude Gardens' trigger area 1 plays 10051 at Status 2 only",
              "null|ProgressEvent(10051)|null",
              string.Join("|", new[] { 1, 2, 3 }.Select(s => R(QuestHandlerKind.OnTriggerAreaEnter, rg, null, s))));
        Check("c3 C4: 7-2 finishes: 12 sets Status 1, 16 sets 2, 10051 sets 3, 10012 completes",
              "Mission.SetVar(Status,1)|Mission.SetVar(Status,2)|Mission.SetVar(Status,3)|Mission.Complete()",
              string.Join("|", new[] { Run(mc, 12, 0), Run(sd, 16, 1), Run(rg, 10051, 2), Run(uj, 10012, 3) }));
    }

    static void RunC4FireInTheEyes()
    {
        var onTrig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var ms = new QC.Missions.Cop.FireInTheEyesOfMen();
        var d = ms.Compile();
        int m = QC.Mission.Cop.FireInTheEyesOfMen;
        ushort mw = QC.Zone.Metalworks, mine = QC.Zone.MineShaft2716;
        Fake F(int status, int timer)
        {
            var f = C4Fake(m, status, ("Timer", timer));
            f.UniqueDay = 8000;
            return f;
        }
        string Cid(int status, int timer) => C2bReply(d, 0, onTrig, mw, QC.Npc.Metalworks.Cid, F(status, timer));

        Check("c3 C4: 7-3 is a CoP mission with its Lua file, Calm Before the Storm next, the Prishe's Buddy title; " +
              "validates clean",
              $"mission.cop.fire_in_the_eyes_of_men scripts/missions/cop/7_3_Fire_in_the_Eyes_of_Men " +
              $"{QC.Mission.Cop.CalmBeforeTheStorm} {QC.Title.PrishesBuddy} ",
              C4Header(ms, d));
        Check("c3 C4: 7-3 _0d0 plays 4 at Status 0 only; its finish sets Status 1",
              "ProgressEvent(4)|null|Mission.SetVar(Status,1)",
              C2bReply(d, 0, onTrig, mine, QC.Npc.MineShaft2716._0d0, F(0, 0)) + "|" +
              C2bReply(d, 0, onTrig, mine, QC.Npc.MineShaft2716._0d0, F(1, 0)) + "|" +
              C2MRun(C2MHandler(d, 0, fin, mine, id: 4), F(0, 0)));
        Check("c3 C4: 7-3 Cid at Status 1 (day 8000): Timer 0 plays 857, Timer 8000 and 7999 play 890, Timer 8001 " +
              "the important event 858; Status 0 and 2 nothing",
              "ProgressEvent(857)|ProgressEvent(890)|ProgressEvent(890)|Event(858):importantEvent()|null|null",
              string.Join("|", new[] { Cid(1, 0), Cid(1, 8000), Cid(1, 7999), Cid(1, 8001), Cid(0, 0), Cid(2, 8000) }));
        Check("c3 C4: 7-3 finish 857 sets Timer to the day after (8001); 890 completes",
              "Mission.SetVar(Timer,8001)|Mission.Complete()",
              C2MRun(C2MHandler(d, 0, fin, mw, id: 857), F(1, 0)) + "|" +
              C2MRun(C2MHandler(d, 0, fin, mw, id: 890), F(1, 8000)));
    }

    static void RunC4CalmBeforeTheStorm()
    {
        var onTrig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var death = QuestHandlerKind.OnMobDeath;
        var ms = new QC.Missions.Cop.CalmBeforeTheStorm();
        var d = ms.Compile();
        int m = QC.Mission.Cop.CalmBeforeTheStorm;
        ushort bb = QC.Zone.BibikiBay, cl = QC.Zone.CarpentersLanding, mc = QC.Zone.MisareauxCoast;
        ushort mw = QC.Zone.Metalworks, sd = QC.Zone.SealionsDen;
        const int dalham = 16793928, executor = 16785710;
        // the CID nibble (position 6) at cid
        Fake F(int cid, params (string Name, int Value)[] locals)
        {
            var f = C4Fake(m, 0);
            f.MissionStatuses[C2bCop] = cid << 24;
            foreach (var (name, value) in locals) f.MissionLocalVars[name] = value;
            f.MobIdsByName["Dalham"] = dalham;
            f.MobIds[QC.Mob.CarpentersLanding.CRYPTONBERRY_EXECUTOR] = executor;
            return f;
        }
        string Reply(Fake f, ushort zone, string npc) =>
            C2bReply(d, 0, onTrig, zone, npc, f) + "/" + string.Join(",", f.Calls.Where(c => c.Contains("Spawn")));
        string Run(ushort zone, int csid, Fake f) => C2MRun(C2MHandler(d, 0, fin, zone, id: csid), f);
        string Death(ushort zone, string npc, Fake f, int id)
        {
            f.Mob = new QuestNpcFake(id, f.Calls);
            f.MobDeath = new QuestMobDeathFake();
            return C2MRun(C2MHandler(d, 0, death, zone, npc), f);
        }

        Check("c3 C4: 7-4 is a CoP mission with its Lua file, The Warrior's Path next, no title; validates clean",
              $"mission.cop.calm_before_the_storm scripts/missions/cop/7_4_Calm_Before_the_Storm " +
              $"{QC.Mission.Cop.TheWarriorsPath}  ",
              C4Header(ms, d));

        // ---- Bibiki Bay: qm_dalham and Dalham
        string qd = QC.Npc.BibikiBay.qmdalham;
        Fake DalhamUp(params (string, int)[] locals)
        {
            var f = F(0, locals);
            f.SpawnedMobIds.Add(dalham);
            return f;
        }
        Check("c3 C4: 7-4 qm_dalham (CID bit 2 unset): nmBibiki 1 plays the cutscene 41; else Dalham not up is " +
              "spawned by his entity (claimed, no enmity change) with YOU_ARE_NOT_ALONE (nmBibiki 0 or 2); up: " +
              "nothing; bit 2 set: nothing, nmBibiki 1 or not",
              $"ProgressCutscene(41)/|MessageSpecial({bb}:YOU_ARE_NOT_ALONE)/" +
              $"Player.SpawnMobEntityById({dalham},False)|" +
              $"MessageSpecial({bb}:YOU_ARE_NOT_ALONE)/Player.SpawnMobEntityById({dalham},False)|null/|null/|null/",
              string.Join("|", new[]
              {
                  Reply(F(0, ("nmBibiki", 1)), bb, qd), Reply(F(0), bb, qd), Reply(F(0, ("nmBibiki", 2)), bb, qd),
                  Reply(DalhamUp(), bb, qd), Reply(F(4, ("nmBibiki", 1)), bb, qd), Reply(F(4), bb, qd),
              }));
        Check("c3 C4: 7-4 Dalham's death sets nmBibiki 1 only from 0; finish 41 sets CID bit 2 over the others",
              "Mission.SetLocalVar(nmBibiki,1)|||Mission.SetStatusAt(7,6)",
              string.Join("|", new[]
              {
                  Death(bb, QC.Npc.BibikiBay.Dalham, F(0), dalham),
                  Death(bb, QC.Npc.BibikiBay.Dalham, F(0, ("nmBibiki", 1)), dalham),
                  Death(bb, QC.Npc.BibikiBay.Dalham, F(0, ("nmBibiki", 2)), dalham),
                  Run(bb, 41, F(3)),
              }));

        // ---- Carpenters' Landing: qm_cryptonberries, the Executor and the Assassins
        string qc = QC.Npc.CarpentersLanding.qmcryptonberries;
        Fake Up(int id, params (string, int)[] locals)
        {
            var f = F(0, locals);
            f.SpawnedMobIds.Add(id);
            return f;
        }
        Check("c3 C4: 7-4 qm_cryptonberries (CID bit 1 unset): carpentersNm 15 plays the cutscene 37; else with none " +
              "of the Executor and its three Assassins up, the Executor spawned by his entity (claimed, enmity 0) " +
              "with CRYPTONBERRY_EXECUTOR_POP; any one of the four up (the first, the last): nothing, the fifth id " +
              "not counted; bit 1 set: nothing",
              $"ProgressCutscene(37)/|MessageSpecial({cl}:CRYPTONBERRY_EXECUTOR_POP)/" +
              $"Player.SpawnMobEntityById({executor},True)|null/|null/|" +
              $"MessageSpecial({cl}:CRYPTONBERRY_EXECUTOR_POP)/Player.SpawnMobEntityById({executor},True)|null/",
              string.Join("|", new[]
              {
                  Reply(F(0, ("carpentersNm", 15)), cl, qc), Reply(F(0, ("carpentersNm", 14)), cl, qc),
                  Reply(Up(executor), cl, qc), Reply(Up(executor + 3), cl, qc), Reply(Up(executor + 4), cl, qc),
                  Reply(F(2, ("carpentersNm", 15)), cl, qc),
              }));
        Check("c3 C4: 7-4 an Assassin's death sets its bit (its id minus the Executor's) in carpentersNm (reached " +
              "by #assassins); the Executor's sets bit 0 and says CRYPTONBERRY_EXECUTOR_DIE as the mob's own line",
              "Mission.SetLocalVar(carpentersNm,5)|Mission.SetLocalVar(carpentersNm,9)|" +
              $"Mission.SetLocalVar(carpentersNm,15),Npc.MessageTextOwn({cl}:CRYPTONBERRY_EXECUTOR_DIE)",
              string.Join("|", new[]
              {
                  Death(cl, QC.Npc.CarpentersLanding.CryptonberryAssassin, F(0, ("carpentersNm", 1)), executor + 2),
                  Death(cl, QC.Npc.CarpentersLanding.CryptonberryAssassin, F(0, ("carpentersNm", 1)), executor + 3),
                  Death(cl, QC.Npc.CarpentersLanding.CryptonberryExecutor, F(0, ("carpentersNm", 14)), executor),
              }));
        Check("c3 C4: 7-4 finish 37 sets CID bit 1", "Mission.SetStatusAt(6,6)", Run(cl, 37, F(4)));

        // ---- Misareaux Coast: _0p4 and Boggelmann
        string p4 = QC.Npc.MisareauxCoast._0p4;
        Fake BoggUp()
        {
            var f = F(0);
            f.SpawnedMobs.Add($"{QC.Mob.MisareauxCoast.BOGGELMANN}+0");
            return f;
        }
        Check("c3 C4: 7-4 _0p4 (CID bit 0 unset): nmMisareaux 1 plays the cutscene 13 (option 1 the Ducal Guard's " +
              "Ring); else Boggelmann not up spawned (SpawnMob, claimed) with CREATURE_HAS_APPEARED; up: nothing; " +
              "bit 0 set: nothing",
              $"ProgressCutscene(13,{{[1]={QC.Item.DucalGuardsRing}}})/|MessageSpecial({mc}:CREATURE_HAS_APPEARED)/" +
              $"Player.SpawnMob({QC.Mob.MisareauxCoast.BOGGELMANN},0,True)|null/|null/",
              string.Join("|", new[]
              {
                  Reply(F(0, ("nmMisareaux", 1)), mc, p4), Reply(F(0), mc, p4), Reply(BoggUp(), mc, p4),
                  Reply(F(1, ("nmMisareaux", 1)), mc, p4),
              }));
        Check("c3 C4: 7-4 Boggelmann's death sets nmMisareaux 1 only from 0; finish 13 sets CID bit 0 and gives the " +
              "Vessel of Light",
              "Mission.SetLocalVar(nmMisareaux,1)||" +
              $"Mission.SetStatusAt(7,6),Player.GiveKeyItem({QC.KeyItem.VesselOfLight})",
              string.Join("|", new[]
              {
                  Death(mc, QC.Npc.MisareauxCoast.Boggelmann, F(0), 1),
                  Death(mc, QC.Npc.MisareauxCoast.Boggelmann, F(0, ("nmMisareaux", 1)), 1),
                  Run(mc, 13, F(6)),
              }));

        // ---- Metalworks' Cid and Sealion's Den's Sueleen
        int letters = QC.KeyItem.LettersFromUlmiaAndPrishe;
        string CidR(int cid, bool held)
        {
            var f = F(cid);
            if (held) f.KeyItems[letters] = true;
            return C2bReply(d, 0, onTrig, mw, QC.Npc.Metalworks.Cid, f);
        }
        Check("c3 C4: 7-4 Cid at the CID nibble 7 exactly: 892 without the letters, 895 once per zone with them; 6 " +
              "and 15 nothing",
              "ProgressEvent(892)|ProgressEvent(895):oncePerZone()|null|null",
              string.Join("|", new[] { CidR(7, false), CidR(7, true), CidR(6, false), CidR(15, false) }));
        string Sue(bool held)
        {
            var f = F(7);
            if (held) f.KeyItems[letters] = true;
            return C2bReply(d, 0, onTrig, sd, QC.Npc.SealionsDen.Sueleen, f);
        }
        Check("c3 C4: 7-4 finish 892 gives the letters; Sueleen with them plays 17, without nothing; 17 completes",
              $"Player.GiveKeyItem({letters})|ProgressEvent(17)|null|Mission.Complete()",
              Run(mw, 892, F(7)) + "|" + Sue(true) + "|" + Sue(false) + "|" + Run(sd, 17, F(7)));
    }

    static void RunC4WarriorsPath()
    {
        var onTrig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var zin = QuestHandlerKind.OnZoneIn;
        var ms = new QC.Missions.Cop.TheWarriorsPath();
        var d = ms.Compile();
        int m = QC.Mission.Cop.TheWarriorsPath;
        ushort sd = QC.Zone.SealionsDen, alt = QC.Zone.Altaieu;
        Fake F(int status, int option = 0) => C4Fake(m, status, ("Option", option));
        Fake Done(int option)
        {
            var f = new Fake { ThisMission = (MissionLog.CoP, m) };
            f.CurrentMissions[C2bCop] = QC.Mission.Cop.GardenOfAntiquity;
            f.CompletedMissions.Add($"{C2bCop}/{m}");
            f.MissionVars[$"{C2bCop}/{m}/Option"] = option;
            return f;
        }
        string R(int state, QuestHandlerKind kind, ushort zone, string npc, Fake f) =>
            C2bReply(d, state, kind, zone, npc, f);
        string Run(int state, QuestHandlerKind kind, ushort zone, int csid, Fake f) =>
            C2MRun(C2MHandler(d, state, kind, zone, id: csid), f);

        Check("c3 C4: 7-5 is a CoP mission with its Lua file, Garden of Antiquity next, the Seeker of the Light " +
              "title; validates clean; the second state holds once 7-5 is completed, not while current",
              $"mission.cop.the_warriors_path scripts/missions/cop/7_5_The_Warriors_Path " +
              $"{QC.Mission.Cop.GardenOfAntiquity} {QC.Title.SeekerOfTheLight} |True|True False",
              C4Header(ms, d) + $"|{d.States[0].MissionMatches[0].IsCurrent}|" +
              $"{C4Holds(d, 1, Done(0), m)} {C4Holds(d, 1, F(3), m)}");
        Check("c3 C4: 7-5 _0w0 plays 32 at Status 0 only; Sueleen the event 28; Sealion's Den's zone-in 34 at Status " +
              "2 " +
              "only; Al'Taieu's 1 at Status 3 only",
              "ProgressEvent(32)|null|Event(28)|null|Event(34)|null|null|Event(1)|null",
              string.Join("|", new[]
              {
                  R(0, onTrig, sd, QC.Npc.SealionsDen._0w0, F(0)), R(0, onTrig, sd, QC.Npc.SealionsDen._0w0, F(1)),
                  R(0, onTrig, sd, QC.Npc.SealionsDen.Sueleen, F(1)),
                  R(0, zin, sd, null, F(1)), R(0, zin, sd, null, F(2)), R(0, zin, sd, null, F(3)),
                  R(0, zin, alt, null, F(2)), R(0, zin, alt, null, F(3)), R(0, zin, alt, null, F(4)),
              }));
        Fake Opt(int option)
        {
            var f = F(0);
            f.Option = option;
            return f;
        }
        Check("c3 C4: 7-5 update 32 with option 0 sends (32, 0, 0, 0, 0, 0, 0, 0); option 1 nothing; finish 32 sets " +
              "Status 1",
              "Player.UpdateEvent(32,0,0,0,0,0,0,0)||Mission.SetVar(Status,1)",
              Run(0, QuestHandlerKind.OnEventUpdate, sd, 32, Opt(0)) + "|" +
              Run(0, QuestHandlerKind.OnEventUpdate, sd, 32, Opt(1)) + "|" + Run(0, fin, sd, 32, F(0)));
        Check("c3 C4: 7-5 finish 34 sets Status 3 and the char down in Al'Taieu at -422.1 0 -532.092 (220)",
              $"Mission.SetVar(Status,3),Player.SetPos(-422.1,0,-532.092,220,{alt})", Run(0, fin, sd, 34, F(2)));
        string Win(int win, int status)
        {
            var f = F(status);
            f.PlayerLocalVars["battlefieldWin"] = win;
            return Run(0, fin, sd, 32001, f);
        }
        Check("c3 C4: 7-5 32001 sets Status 2 and the char down in Sealion's Den (612.057 132.664 776.92, 188) only " +
              "at Status 1 with The Warrior's Path won, each condition alone",
              $"Mission.SetVar(Status,2),Player.SetPos(612.057,132.664,776.92,188,{sd})||",
              Win(QC.Battlefield.WarriorsPath, 1) + "|" + Win(QC.Battlefield.WarriorsPath, 2) + "|" +
              Win(QC.Battlefield.OneToBeFeared, 1));
        string Light(int race, int held)
        {
            var f = F(3);
            f.RaceId = race;
            return Run(0, fin, alt, 1, f);
        }
        string Stolen(int light) =>
            $"Player.DelKeyItem({QC.KeyItem.MysteriousAmuletDrained})," +
            $"Text.Special({alt}:AMULET_SHATTERED,{QC.KeyItem.MysteriousAmulet})," +
            $"Player.DelKeyItem({light}),Text.Special({alt}:LIGHT_STOLEN,{light})," +
            $"Player.GiveKeyItem({QC.KeyItem.LightOfAltaieu}),Mission.Complete(),Mission.SetVar(Option,1)," +
            "Player.SetCharVar(SagheeraInteractions,7,0)";
        Check("c3 C4: 7-5 Al'Taieu's finish 1 by race: 1 and 2 lose the Light of Vahzl, 3 and 4 Mea, 5 and 6 Holla, " +
              "7 Dem (each LIGHT_STOLEN, the Light of Al'Taieu given), then the mission completes, Option 1, " +
              "SagheeraInteractions 7",
              string.Join("|", new[]
              {
                  Stolen(QC.KeyItem.LightOfVahzl), Stolen(QC.KeyItem.LightOfVahzl), Stolen(QC.KeyItem.LightOfMea),
                  Stolen(QC.KeyItem.LightOfMea), Stolen(QC.KeyItem.LightOfHolla), Stolen(QC.KeyItem.LightOfHolla),
                  Stolen(QC.KeyItem.LightOfDem),
              }),
              string.Join("|", Enumerable.Range(1, 7).Select(r => Light(r, 0))));
        Check("c3 C4: 7-5 Al'Taieu's finish 1 for a Galka: OBTAIN_BUT_STOLEN with the Light of Al'Taieu, nothing " +
              "taken but the amulet",
              $"Player.DelKeyItem({QC.KeyItem.MysteriousAmuletDrained})," +
              $"Text.Special({alt}:AMULET_SHATTERED,{QC.KeyItem.MysteriousAmulet})," +
              $"Text.Special({alt}:OBTAIN_BUT_STOLEN,{QC.KeyItem.LightOfAltaieu}),Mission.Complete()," +
              "Mission.SetVar(Option,1),Player.SetCharVar(SagheeraInteractions,7,0)",
              Light(8, 0));
        Fake DoneOpt(int option, int eventOption)
        {
            var f = Done(option);
            f.Option = eventOption;
            return f;
        }
        Check("c3 C4: 7-5 completed: Sueleen plays the event 12 (finish option 1: the teleport to the Sea, 56; 0: " +
              "nothing); the zone-in plays 18 at Option 1 only (finish: Option 0)",
              "Event(12)|Player.TeleportTo(56)||Event(18)|null|null|Mission.SetVar(Option,0)",
              string.Join("|", new[]
              {
                  R(1, onTrig, sd, QC.Npc.SealionsDen.Sueleen, Done(0)), Run(1, fin, sd, 12, DoneOpt(0, 1)),
                  Run(1, fin, sd, 12, DoneOpt(0, 0)), R(1, zin, sd, null, Done(1)), R(1, zin, sd, null, Done(0)),
                  R(1, zin, sd, null, Done(2)), Run(1, fin, sd, 18, Done(1)),
              }));
    }

    static void RunC4WarriorsPathBattlefield()
    {
        var s = new QC.Battlefields.SealionsDen.WarriorsPath();
        var bd = (BattlefieldMissionData)s.Data;
        Check("c3 C4: The Warrior's Path validates clean, its key and Lua file",
              "|battlefield.warriors_path scripts/battlefields/Sealions_Den/warriors_path",
              $"{string.Join(" | ", BattlefieldScriptValidator.Validate(s))}|{s.Key} {s.LegacyPath}");
        Check("c3 C4: The Warrior's Path's fields (zone, id, no exp loss, mission, trusts, players, MAX_LEVEL, time, " +
              "index, area, grantXP, title, entry and exit NPCs, mission, required var and value, wipe grace, groups)",
              $"{QC.Zone.SealionsDen} {QC.Battlefield.WarriorsPath} False True True 6 MAX_LEVEL 1800 1 0 0 " +
              $"{QC.Title.UnquenchableLight} _0w0 Airship_Door 6/{QC.Mission.Cop.TheWarriorsPath} " +
              "Mission[6][748]Status 1 True 2",
              $"{bd.ZoneId} {bd.BattlefieldId} {bd.CanLoseExp} {bd.IsMission} {bd.AllowTrusts} {bd.MaxPlayers} " +
              $"{bd.LevelCapSetting} {bd.TimeLimit} {bd.Index} {bd.Area} {bd.GrantXP} {bd.Title} " +
              $"{string.Join(",", bd.EntryNpcs)} {string.Join(",", bd.ExitNpcs)} {(int)bd.MissionArea}/{bd.Mission} " +
              $"{bd.RequiredVar} {bd.RequiredValue} {bd.HasWipeGrace} {bd.Groups.Count}");
        string Ids(BattlefieldGroup g) => string.Join(";", g.AreaMobIds.Select(a =>
            string.Join(",", a.Select(o => o.Key + ":" + o.Offset))));
        Check("c3 C4: The Warrior's Path's groups: Tenzen (+0, +4, +8), its allDeath; the Tarus (+1-3, +5-7, " +
              "+9-11), no allDeath; neither superlinked nor with spawned set",
              "TENZEN:0;TENZEN:4;TENZEN:8 True False |" +
              "TENZEN:1,TENZEN:2,TENZEN:3;TENZEN:5,TENZEN:6,TENZEN:7;TENZEN:9,TENZEN:10,TENZEN:11 False False ",
              $"{Ids(bd.Groups[0])} {bd.Groups[0].AllDeath != null} {bd.Groups[0].Superlink} {bd.Groups[0].Spawned}|" +
              $"{Ids(bd.Groups[1])} {bd.Groups[1].AllDeath != null} {bd.Groups[1].Superlink} {bd.Groups[1].Spawned}");
        var bf = new BattlefieldViewFake();
        bd.Groups[0].AllDeath(new BattlefieldContextFake(s) { Battlefield = bf }, null);
        Check("c3 C4: Tenzen's group's allDeath (unreached live: Tenzen is unkillable) sets the status to WON",
              "Battlefield.SetStatus(2)", string.Join(",", bf.Calls));
        var p = new BattlefieldPlayerFake { ZoneId = QC.Zone.SealionsDen };
        var engine = new BattlefieldEngineFake();
        engine.Texts[$"{QC.Zone.SealionsDen}:COSMIC_ELUCIDATION"] = 7905;
        s.OnBattlefieldLoss(new BattlefieldContextFake(s) { Player = p, Engine = engine });
        Check("c3 C4: The Warrior's Path's loss says COSMIC_ELUCIDATION, then the framework's loss event 32002",
              "Player.MessageSpecial(7905),Player.StartEvent(32002,{})", string.Join(",", p.Calls));
        var w = new BattlefieldPlayerFake { ZoneId = QC.Zone.SealionsDen };
        var winCtx = new BattlefieldContextFake(s) { Player = w, Engine = new BattlefieldEngineFake() };
        s.OnEventFinishWin(winCtx, 32001, 0);
        Check("c3 C4: The Warrior's Path's onEventFinishWin adds The Chebukki's Worst Nightmare first, then " +
              "BattlefieldMission's finish (its title Unquenchable Light)",
              $"Player.AddTitle({QC.Title.TheChebukkisWorstNightmare})|True",
              $"{w.Calls.FirstOrDefault()}|" +
              $"{w.Calls.Skip(1).Any(c => c == $"Player.AddTitle({QC.Title.UnquenchableLight})")}");
    }

    static void RunC4GardenOfAntiquity()
    {
        var onTrig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var ms = new QC.Missions.Cop.GardenOfAntiquity();
        var d = ms.Compile();
        int m = QC.Mission.Cop.GardenOfAntiquity;
        ushort alt = QC.Zone.Altaieu, hx = QC.Zone.GrandPalaceOfHuxzoi;
        const int ruaern = 16912300, crystal = 16912900;
        // the CID nibble (6) and the Rubious nibble (7)
        Fake F(int status, int cid = 0, int rub = 0, params (string Name, int Value)[] locals)
        {
            var f = C4Fake(m, status);
            f.MissionStatuses[C2bCop] = cid << 24 | rub << 28;
            foreach (var (name, value) in locals) f.MissionLocalVars[name] = value;
            f.MobIds[QC.Mob.Altaieu.RUAERN] = ruaern;
            f.NpcIds["RUBIOUS_CRYSTAL"] = crystal;
            return f;
        }
        Fake Done()
        {
            var f = new Fake { ThisMission = (MissionLog.CoP, m) };
            f.CurrentMissions[C2bCop] = QC.Mission.Cop.AFateDecided;
            f.CompletedMissions.Add($"{C2bCop}/{m}");
            return f;
        }
        string X0(Fake f) => C2bReply(d, 0, onTrig, alt, QC.Npc.Altaieu._0x0, f);

        Check("c3 C4: 8-1 is a CoP mission with its Lua file, A Fate Decided next, no title; validates clean; the " +
              "second state holds once 8-1 is completed, not while current",
              $"mission.cop.garden_of_antiquity scripts/missions/cop/8_1_Garden_of_Antiquity " +
              $"{QC.Mission.Cop.AFateDecided}  |True False",
              C4Header(ms, d) + $"|{C4Holds(d, 1, Done(), m)} {C4Holds(d, 1, F(1), m)}");
        string poc = "ProgressOptionalCutscene(100,{canSkip=true,cs_option=0})";
        Check("c3 C4: 8-1 _0x0: Status 0 plays the cutscene 164 (whatever the towers); past it, the towers' mask 7 " +
              "(CID bit 3 for tower 0, Rubious bits 0 and 1 for towers 1 and 2) plays the optional cutscene 100 at " +
              "Status 1 or 2, each bit alone missing nothing; the fourth bit getRubiousMask reads (Rubious bit 2) " +
              "makes it 15: nothing",
              $"ProgressCutscene(164)|{poc}|{poc}|null|null|null|null|null",
              string.Join("|", new[]
              {
                  X0(F(0, 8, 3)), X0(F(1, 8, 3)), X0(F(2, 8, 3)), X0(F(1, 0, 3)), X0(F(1, 8, 2)), X0(F(1, 8, 1)),
                  X0(F(1, 7, 3)), X0(F(1, 8, 7)),
              }));
        Check("c3 C4: 8-1 finish 164 sets Status 1; finish 100 sets Status 2, with option 1 also the char down in " +
              "Hu'Xzoi at -20 0.624 -355 (191)",
              $"Mission.SetVar(Status,1)|Mission.SetVar(Status,2)|" +
              $"Mission.SetVar(Status,2),Player.SetPos(-20,0.624,-355,191,{hx})",
              C2MRun(C2MHandler(d, 0, fin, alt, id: 164), F(0)) + "|" +
              C2MRun(C2MHandler(d, 0, fin, alt, id: 100), F(1, 8, 3)) + "|" +
              C2MRun(C2MHandler(d, 0, fin, alt, id: 100), WithOption(F(1, 8, 3), 1)));

        // a Rubious Crystal: _0x1 (index 0, tower 0), _0x2 (1, tower 2), _0x3 (2, tower 1)
        string Tower(string npc, int index, Fake f)
        {
            f.Npc = new QuestNpcFake(crystal + index, f.Calls) { Name = npc };
            var r = C2bReply(d, 0, onTrig, alt, npc, f);
            return r + "/" + string.Join(",", f.Calls);
        }
        string x1 = QC.Npc.Altaieu._0x1, x2 = QC.Npc.Altaieu._0x2, x3 = QC.Npc.Altaieu._0x3;
        Fake NoPop(Fake f)
        {
            f.PopResult = false;
            return f;
        }
        int o2 = ruaern + 3;
        Check("c3 C4: 8-1 a tower at Status 1, not done, its NMs not defeated: popFromQM of its three Ru'aern " +
              "(RUAERN + 3 * index, hide 0), triggerNpc and nmOffset on the alliance, OMINOUS_SHADOW; a refused pop " +
              "nothing; Status 0 or 2 nothing; the tower done nothing",
              $"MessageSpecial({alt}:OMINOUS_SHADOW)/Player.PopFromQmIds({o2};{o2 + 1};{o2 + 2},0)," +
              $"Mission.SetAllianceLocalVars(triggerNpc={crystal + 1};nmOffset={o2})|" +
              $"null/Player.PopFromQmIds({o2};{o2 + 1};{o2 + 2},0)|null/|null/|null/",
              string.Join("|", new[]
              {
                  Tower(x2, 1, F(1)), Tower(x2, 1, NoPop(F(1))), Tower(x2, 1, F(0)), Tower(x2, 1, F(2)),
                  Tower(x2, 1, F(1, 0, 2)),
              }));
        Check("c3 C4: 8-1 a tower's nmDefeated bit is its towerOption (_0x1 0, _0x2 2, _0x3 1): another tower's bit " +
              "pops instead",
              $"MessageSpecial({alt}:OMINOUS_SHADOW)|MessageSpecial({alt}:OMINOUS_SHADOW)|" +
              $"MessageSpecial({alt}:OMINOUS_SHADOW)",
              string.Join("|", new[]
              {
                  Tower(x1, 0, F(1, 0, 0, ("nmDefeated", 6))), Tower(x2, 1, F(1, 0, 0, ("nmDefeated", 3))),
                  Tower(x3, 2, F(1, 0, 0, ("nmDefeated", 5))),
              }.Select(r => r.Split('/')[0])));
        Check("c3 C4: 8-1 a defeated tower sets its status bit and plays 161 (its towerOption) as the first done, " +
              "163 as the third, else 162 with the first tower not done from 2 down (2, 1 and 0 each)",
              "ProgressCutscene(161,0)/Mission.SetStatusAt(8,6)|ProgressCutscene(161,2)/Mission.SetStatusAt(2,7)|" +
              "ProgressCutscene(161,1)/Mission.SetStatusAt(1,7)|ProgressCutscene(162,1)/Mission.SetStatusAt(2,7)|" +
              "ProgressCutscene(162,2)/Mission.SetStatusAt(1,7)|ProgressCutscene(162,0)/Mission.SetStatusAt(3,7)|" +
              "ProgressCutscene(163)/Mission.SetStatusAt(8,6)|ProgressCutscene(163)/Mission.SetStatusAt(3,7)",
              string.Join("|", new[]
              {
                  Tower(x1, 0, F(1, 0, 0, ("nmDefeated", 1))), Tower(x2, 1, F(1, 0, 0, ("nmDefeated", 4))),
                  Tower(x3, 2, F(1, 0, 0, ("nmDefeated", 2))), Tower(x2, 1, F(1, 8, 0, ("nmDefeated", 4))),
                  Tower(x3, 2, F(1, 8, 0, ("nmDefeated", 2))), Tower(x2, 1, F(1, 0, 1, ("nmDefeated", 4))),
                  Tower(x1, 0, F(1, 0, 3, ("nmDefeated", 1))),
                  Tower(x3, 2, F(1, 8, 2, ("nmDefeated", 2))),
              }));
        string Kill(int mobId, int nmOffset, int numDefeated, int triggerNpc, int nmDefeated = 0)
        {
            var f = F(1, 0, 0, ("nmOffset", nmOffset), ("numDefeated", numDefeated), ("triggerNpc", triggerNpc),
                      ("nmDefeated", nmDefeated));
            f.NpcsById[crystal + 1] = new QuestNpcFake(crystal + 1, f.Calls) { Name = x2 };
            f.NpcsById[crystal + 2] = new QuestNpcFake(crystal + 2, f.Calls) { Name = x3 };
            f.Mob = new QuestNpcFake(mobId, f.Calls);
            f.MobDeath = new QuestMobDeathFake();
            return C2MRun(C2MHandler(d, 0, QuestHandlerKind.OnMobDeath, alt, QC.Npc.Altaieu.Ruaern), f);
        }
        Check("c3 C4: 8-1 a Ru'aern's death: no nmOffset nothing; one of the three counts (the first, the last), " +
              "another does not (the count kept); the third sets the trigger NPC's tower bit in nmDefeated over the " +
              "others and resets the count; an invalid trigger NPC prints the error and stops",
              $"|Mission.SetLocalVar(numDefeated,1)|Mission.SetLocalVar(numDefeated,2)|" +
              $"Mission.SetLocalVar(numDefeated,1)|" +
              $"Mission.SetLocalVar(nmDefeated,5),Mission.SetLocalVar(numDefeated,0)|" +
              $"Mission.SetLocalVar(nmDefeated,2),Mission.SetLocalVar(numDefeated,0)|" +
              "Player.LuaPrint(ERROR: triggerNpc returned invalid NPC object.)",
              string.Join("|", new[]
              {
                  Kill(o2, 0, 0, 0), Kill(o2, o2, 0, crystal + 1), Kill(o2 + 2, o2, 1, crystal + 1),
                  Kill(o2 + 3, o2, 1, crystal + 1), Kill(o2 + 1, o2, 2, crystal + 1, 1),
                  Kill(o2 + 1, o2, 2, crystal + 2), Kill(o2, o2, 2, 0),
              }));

        // ---- the Grand Palace of Hu'Xzoi
        string dnr = $"MessageSpecial({hx}:DOES_NOT_RESPOND):setPriority(1000)";
        Check("c3 C4: 8-1 _iya plays 1 at Status 2, else DOES_NOT_RESPOND at priority 1000; _iyb plays 2 at Status 3 " +
              "only",
              $"ProgressEvent(1)|{dnr}|{dnr}|ProgressEvent(2)|null",
              string.Join("|", new[]
              {
                  C2bReply(d, 0, onTrig, hx, QC.Npc.GrandPalaceOfHuxzoi.iya, F(2)),
                  C2bReply(d, 0, onTrig, hx, QC.Npc.GrandPalaceOfHuxzoi.iya, F(1)),
                  C2bReply(d, 0, onTrig, hx, QC.Npc.GrandPalaceOfHuxzoi.iya, F(3)),
                  C2bReply(d, 0, onTrig, hx, QC.Npc.GrandPalaceOfHuxzoi.iyb, F(3)),
                  C2bReply(d, 0, onTrig, hx, QC.Npc.GrandPalaceOfHuxzoi.iyb, F(2)),
              }));
        Fake NoGive()
        {
            var f = F(2);
            f.GiveResult = false;
            return f;
        }
        Check("c3 C4: 8-1 finish 1: the Tavnazian Ring given sets Status 3, a failed give nothing; 2 completes",
              $"Player.GiveItem({QC.Item.TavnazianRing}),Mission.SetVar(Status,3)|" +
              $"Player.GiveItem({QC.Item.TavnazianRing})|Mission.Complete()",
              C2MRun(C2MHandler(d, 0, fin, hx, id: 1), F(2)) + "|" +
              C2MRun(C2MHandler(d, 0, fin, hx, id: 1), NoGive()) +
              "|" + C2MRun(C2MHandler(d, 0, fin, hx, id: 2), F(3)));
        Check("c3 C4: 8-1 completed: _0x0 plays the optional cutscene 100; its finish with option 1 sets the char " +
              "down in Hu'Xzoi, with 0 nothing",
              $"{poc}|Player.SetPos(-20,0.624,-355,191,{hx})|",
              C2bReply(d, 1, onTrig, alt, QC.Npc.Altaieu._0x0, Done()) + "|" +
              C2MRun(C2MHandler(d, 1, fin, alt, id: 100), WithOption(Done(), 1)) + "|" +
              C2MRun(C2MHandler(d, 1, fin, alt, id: 100), Done()));
    }

    static Fake WithOption(Fake f, int option)
    {
        f.Option = option;
        return f;
    }

    static void RunC4FateDecided()
    {
        var onTrig = QuestHandlerKind.OnTrigger;
        var ms = new QC.Missions.Cop.AFateDecided();
        var d = ms.Compile();
        int m = QC.Mission.Cop.AFateDecided;
        ushort hx = QC.Zone.GrandPalaceOfHuxzoi;
        string ix = QC.Mob.GrandPalaceOfHuxzoi.IXGHRAH;
        Fake F(int status, bool up = false)
        {
            var f = C4Fake(m, status);
            if (up) f.SpawnedMobs.Add($"{ix}+0");
            return f;
        }
        string Q(Fake f) => C2bReply(d, 0, onTrig, hx, QC.Npc.GrandPalaceOfHuxzoi.iyq, f) + "/" +
                            string.Join(",", f.Calls.Where(c => c.Contains("Spawn")));

        Check("c3 C4: 8-2 is a CoP mission with its Lua file, When Angels Fall next, no title; validates clean",
              $"mission.cop.a_fate_decided scripts/missions/cop/8_2_A_Fate_Decided {QC.Mission.Cop.WhenAngelsFall}  ",
              C4Header(ms, d));
        Check("c3 C4: 8-2 _iya says DOES_NOT_RESPOND at priority 1000 at any status",
              $"MessageSpecial({hx}:DOES_NOT_RESPOND):setPriority(1000)|" +
              $"MessageSpecial({hx}:DOES_NOT_RESPOND):setPriority(1000)",
              C2bReply(d, 0, onTrig, hx, QC.Npc.GrandPalaceOfHuxzoi.iya, F(0)) + "|" +
              C2bReply(d, 0, onTrig, hx, QC.Npc.GrandPalaceOfHuxzoi.iya, F(1)));
        Check("c3 C4: 8-2 _iyq: Status 0 with Ixghrah not up spawns him (claimed) with PRESENCE_HAS_DRAWN; up or " +
              "Status 2 nothing; Status 1 plays the cutscene 3",
              $"MessageSpecial({hx}:PRESENCE_HAS_DRAWN)/Player.SpawnMob({ix},0,True)|null/|ProgressCutscene(3)/|null/",
              string.Join("|", new[] { Q(F(0)), Q(F(0, true)), Q(F(1)), Q(F(2)) }));
        string Death(int status)
        {
            var f = F(status);
            f.Mob = new QuestNpcFake(1, f.Calls);
            f.MobDeath = new QuestMobDeathFake();
            return C2MRun(C2MHandler(d, 0, QuestHandlerKind.OnMobDeath, hx, QC.Npc.GrandPalaceOfHuxzoi.Ixghrah), f);
        }
        Check("c3 C4: 8-2 Ixghrah's death sets Status 1 only at Status 0; finish 3 completes",
              "Mission.SetVar(Status,1)||Mission.Complete()",
              Death(0) + "|" + Death(1) + "|" +
              C2MRun(C2MHandler(d, 0, QuestHandlerKind.OnEventFinish, hx, id: 3), F(1)));
    }

    static void RunC4WhenAngelsFall()
    {
        var onTrig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var zin = QuestHandlerKind.OnZoneIn;
        var ms = new QC.Missions.Cop.WhenAngelsFall();
        var d = ms.Compile();
        int m = QC.Mission.Cop.WhenAngelsFall;
        ushort gr = QC.Zone.TheGardenOfRuhmet, alt = QC.Zone.Altaieu;
        Fake F(int status) => C4Fake(m, status);
        string R(QuestHandlerKind kind, ushort zone, string npc, Fake f, int id = 1) =>
            C2bReply(d, 0, kind, zone, npc, f, id);
        string Run(ushort zone, int csid, Fake f) => C2MRun(C2MHandler(d, 0, fin, zone, id: csid), f);
        var panels = new[]
        {
            QC.Npc.TheGardenOfRuhmet.iz2, QC.Npc.TheGardenOfRuhmet.EbonPanelElvaan,
            QC.Npc.TheGardenOfRuhmet.EbonPanelTaru,
            QC.Npc.TheGardenOfRuhmet.EbonPanelMithra, QC.Npc.TheGardenOfRuhmet.EbonPanelGalka,
        };
        string Panel(string npc, int status, int race, double distance = 1.5)
        {
            var f = F(status);
            f.RaceId = race;
            f.Npc = new QuestNpcFake(1, f.Calls) { Name = npc, PlayerDistance = distance };
            return R(onTrig, gr, npc, f);
        }

        Check("c3 C4: 8-3 is a CoP mission with its Lua file, Dawn next, no title; validates clean",
              $"mission.cop.when_angels_fall scripts/missions/cop/8_3_When_Angels_Fall {QC.Mission.Cop.Dawn}  ",
              C4Header(ms, d));
        Check("c3 C4: 8-3 The Garden of Ru'Hmet's zone-in plays 201 at Status 0 only; Al'Taieu's 165 at Status 6 only",
              "Event(201)|null|Event(165)|null",
              R(zin, gr, null, F(0)) + "|" + R(zin, gr, null, F(1)) + "|" + R(zin, alt, null, F(6)) + "|" +
              R(zin, alt, null, F(5)));
        Check("c3 C4: 8-3 every panel plays 202 at Status 1; nothing at Status 3",
              "ProgressEvent(202)|ProgressEvent(202)|ProgressEvent(202)|ProgressEvent(202)|ProgressEvent(202)|" +
              "null|null|null|null|null",
              string.Join("|", panels.Select(p => Panel(p, 1, 1)).Concat(panels.Select(p => Panel(p, 3, 1)))));
        Check("c3 C4: 8-3 at Status 2 each race's own panel (races 1 and 2 _iz2: 120, 3 and 4 Elvaan: 121, 5 and 6 " +
              "Taru: 123, 7 Mithra: 124, 8 Galka: 122) and no other",
              "ProgressEvent(120)|ProgressEvent(120)|ProgressEvent(121)|ProgressEvent(121)|ProgressEvent(123)|" +
              "ProgressEvent(123)|ProgressEvent(124)|ProgressEvent(122)|null|null|null|null",
              string.Join("|", new[]
              {
                  Panel(panels[0], 2, 1), Panel(panels[0], 2, 2), Panel(panels[1], 2, 3), Panel(panels[1], 2, 4),
                  Panel(panels[2], 2, 5), Panel(panels[2], 2, 6), Panel(panels[3], 2, 7), Panel(panels[4], 2, 8),
                  Panel(panels[1], 2, 1), Panel(panels[0], 2, 3), Panel(panels[4], 2, 7), Panel(panels[3], 2, 8),
              }));
        Check("c3 C4: 8-3 the own panel within 1.8 yalms plays its event (1.8 itself too); farther " +
              "YOU_MUST_MOVE_CLOSER",
              $"ProgressEvent(120)|MessageSpecial({gr}:YOU_MUST_MOVE_CLOSER)",
              Panel(panels[0], 2, 1, 1.8) + "|" + Panel(panels[0], 2, 1, 1.81));
        string PanelFin(int csid, string npc, int option)
        {
            var f = F(2);
            f.Option = option;
            f.Npc = new QuestNpcFake(1, f.Calls) { Name = npc };
            return Run(gr, csid, f);
        }
        string Got(int light) =>
            $"Player.GiveKeyItem({light}),Player.AddTitle({QC.Title.WarriorOfTheCrystal}),Mission.SetVar(Status,3)";
        Check("c3 C4: 8-3 a panel's finish with option 1 gives that panel's light (by the NPC), the Warrior of the " +
              "Crystal title and Status 3; option 0 nothing",
              string.Join("|", Got(QC.KeyItem.LightOfVahzl), Got(QC.KeyItem.LightOfMea), Got(QC.KeyItem.LightOfAltaieu),
                          Got(QC.KeyItem.LightOfHolla), Got(QC.KeyItem.LightOfDem), ""),
              string.Join("|", new[]
              {
                  PanelFin(120, panels[0], 1), PanelFin(121, panels[1], 1), PanelFin(122, panels[4], 1),
                  PanelFin(123, panels[2], 1), PanelFin(124, panels[3], 1), PanelFin(120, panels[0], 0),
              }));
        string z0 = QC.Npc.TheGardenOfRuhmet._0z0, zt = QC.Npc.TheGardenOfRuhmet._0zt;
        Check("c3 C4: 8-3 _0z0 plays 203 at Status 3, the event 205 at 5, nothing at 4 or 6; _0zt 204 at 5 only",
              "ProgressEvent(203)|null|Event(205)|null|null|ProgressEvent(204)|null",
              string.Join("|", new[]
              {
                  R(onTrig, gr, z0, F(3)), R(onTrig, gr, z0, F(4)), R(onTrig, gr, z0, F(5)), R(onTrig, gr, z0, F(6)),
                  R(onTrig, gr, zt, F(4)), R(onTrig, gr, zt, F(5)), R(onTrig, gr, zt, F(6)),
              }));
        string Brand(string npc, int brand)
        {
            var f = F(1);
            if (brand != 0) f.KeyItems[brand] = true;
            return R(onTrig, gr, npc, f);
        }
        Check("c3 C4: 8-3 _0zu plays the optional cutscene 110 (cs_option 0, skippable) without the Brand of Dawn, " +
              "_0zv 111 without the Brand of Twilight; each brand held nothing, the other brand not counting",
              "ProgressOptionalCutscene(110,{canSkip=true,cs_option=0})|null|" +
              "ProgressOptionalCutscene(110,{canSkip=true,cs_option=0})|" +
              "ProgressOptionalCutscene(111,{canSkip=true,cs_option=0})|null|" +
              "ProgressOptionalCutscene(111,{canSkip=true,cs_option=0})",
              string.Join("|", new[]
              {
                  Brand(QC.Npc.TheGardenOfRuhmet._0zu, 0), Brand(QC.Npc.TheGardenOfRuhmet._0zu, QC.KeyItem.BrandOfDawn),
                  Brand(QC.Npc.TheGardenOfRuhmet._0zu, QC.KeyItem.BrandOfTwilight),
                  Brand(QC.Npc.TheGardenOfRuhmet._0zv, 0),
                  Brand(QC.Npc.TheGardenOfRuhmet._0zv, QC.KeyItem.BrandOfTwilight),
                  Brand(QC.Npc.TheGardenOfRuhmet._0zv, QC.KeyItem.BrandOfDawn),
              }));
        Check("c3 C4: 8-3 finishes 110 and 111 give their brand with option 1 only",
              $"Player.GiveKeyItem({QC.KeyItem.BrandOfDawn})||Player.GiveKeyItem({QC.KeyItem.BrandOfTwilight})|",
              Run(gr, 110, WithOption(F(1), 1)) + "|" + Run(gr, 110, F(1)) + "|" + Run(gr, 111, WithOption(F(1), 1)) +
              "|" + Run(gr, 111, F(1)));
        Check("c3 C4: 8-3 finishes: 201 gives Prishe's amulet and sets Status 1; 202, 203, 204 set 2, 4, 6",
              $"Player.GiveKeyItem({QC.KeyItem.MysteriousAmuletPrishe}),Mission.SetVar(Status,1)|" +
              "Mission.SetVar(Status,2)|Mission.SetVar(Status,4)|Mission.SetVar(Status,6)",
              string.Join("|", new[]
              {
                  Run(gr, 201, F(0)), Run(gr, 202, F(1)), Run(gr, 203, F(3)), Run(gr, 204, F(5)),
              }));
        string Win(int win, int status)
        {
            var f = F(status);
            f.PlayerLocalVars["battlefieldWin"] = win;
            return Run(gr, 32001, f);
        }
        Check("c3 C4: 8-3 32001 sets Status 5 only with When Angels Fall won at Status 4, each condition alone",
              "Mission.SetVar(Status,5)||", Win(QC.Battlefield.WhenAngelsFall, 4) + "|" +
              Win(QC.Battlefield.WhenAngelsFall, 3) + "|" + Win(QC.Battlefield.Dawn, 4));
        Check("c3 C4: 8-3 Al'Taieu's finish 165 takes Prishe's amulet, says RETURN_AMULET_TO_PRISHE (the Mysterious " +
              "Amulet) and completes",
              $"Player.DelKeyItem({QC.KeyItem.MysteriousAmuletPrishe})," +
              $"Text.Special({alt}:RETURN_AMULET_TO_PRISHE,{QC.KeyItem.MysteriousAmulet}),Mission.Complete()",
              Run(alt, 165, F(6)));
    }

    static void RunC4WhenAngelsFallBattlefield()
    {
        var s = new QC.Battlefields.TheGardenOfRuhmet.WhenAngelsFall();
        var bd = (BattlefieldMissionData)s.Data;
        Check("c3 C4: When Angels Fall validates clean, its key and Lua file",
              "|battlefield.when_angels_fall scripts/battlefields/The_Garden_of_RuHmet/when_angels_fall",
              $"{string.Join(" | ", BattlefieldScriptValidator.Validate(s))}|{s.Key} {s.LegacyPath}");
        Check("c3 C4: When Angels Fall's fields (zone, id, no exp loss, mission, trusts, players, MAX_LEVEL, time, " +
              "index, area, grantXP, title, entry NPC, no exit, mission, required var and value, wipe grace, groups)",
              $"{QC.Zone.TheGardenOfRuhmet} {QC.Battlefield.WhenAngelsFall} False True True 6 MAX_LEVEL 1800 " +
              "0 1 1000 " +
              $"0 _0z0 - 6/{QC.Mission.Cop.WhenAngelsFall} Mission[6][828]Status 4 True 1",
              $"{bd.ZoneId} {bd.BattlefieldId} {bd.CanLoseExp} {bd.IsMission} {bd.AllowTrusts} {bd.MaxPlayers} " +
              $"{bd.LevelCapSetting} {bd.TimeLimit} {bd.Index} {bd.Area} {bd.GrantXP} {bd.Title} " +
              $"{string.Join(",", bd.EntryNpcs)} {(bd.ExitNpcs == null ? "-" : string.Join(",", bd.ExitNpcs))} " +
              $"{(int)bd.MissionArea}/{bd.Mission} {bd.RequiredVar} {bd.RequiredValue} {bd.HasWipeGrace} " +
              $"{bd.Groups.Count}");
        var g = bd.Groups[0];
        Check("c3 C4: When Angels Fall's group: the four Ixzdei (IXZDEI_RDM +0 to +3) by id for the one area, " +
              "superlinked, spawned left out",
              "IXZDEI_RDM:0,IXZDEI_RDM:1,IXZDEI_RDM:2,IXZDEI_RDM:3 True  True",
              $"{string.Join(",", g.MobIds.Select(o => o.Key + ":" + o.Offset))} {g.Superlink} {g.Spawned} " +
              $"{g.AreaMobIds == null}");
        var bf = new BattlefieldViewFake();
        g.AllDeath(new BattlefieldContextFake(s) { Battlefield = bf }, null);
        var p = new BattlefieldPlayerFake { ZoneId = QC.Zone.TheGardenOfRuhmet };
        s.OnEventFinishWin(new BattlefieldContextFake(s) { Player = p }, 32001, 0);
        Check("c3 C4: When Angels Fall's allDeath sets WON; onEventFinishWin only sets the char down at 420 0 445 " +
              "(192) in the zone (no experience, no title: the kept quirk)",
              "Battlefield.SetStatus(2)|Player.SetPos(420,0,445,192)",
              string.Join(",", bf.Calls) + "|" + string.Join(",", p.Calls));
    }

    static void RunC4Dawn()
    {
        var onTrig = QuestHandlerKind.OnTrigger;
        var fin = QuestHandlerKind.OnEventFinish;
        var upd = QuestHandlerKind.OnEventUpdate;
        var zin = QuestHandlerKind.OnZoneIn;
        var area = QuestHandlerKind.OnTriggerAreaEnter;
        var ms = new QC.Missions.Cop.Dawn();
        var d = ms.Compile();
        int m = QC.Mission.Cop.Dawn;
        ushort ep = QC.Zone.EmpyrealParadox, rg = QC.Zone.RuludeGardens, uj = QC.Zone.UpperJeuno;
        ushort saf = QC.Zone.TavnazianSafehold, lm = QC.Zone.LufaiseMeadows, norg = QC.Zone.Norg;
        ushort ssd = QC.Zone.SouthernSanDoria, ur = QC.Zone.UleguerandRange, pw = QC.Zone.PortWindurst;
        ushort bb = QC.Zone.BibikiBay, mh = QC.Zone.Mhaura, om = QC.Zone.OldtonMovalpolos, mw = QC.Zone.Metalworks;
        ushort gr = QC.Zone.TheGardenOfRuhmet;
        Fake F(int status, params (string Name, int Value)[] vars) => C4Fake(m, status, vars);
        Fake Current(int mission)
        {
            var f = new Fake { ThisMission = (MissionLog.CoP, m) };
            f.CurrentMissions[C2bCop] = mission;
            return f;
        }
        string R(int state, QuestHandlerKind kind, ushort zone, string npc, Fake f, int id = 1) =>
            C2bReply(d, state, kind, zone, npc, f, id);
        string Run(int state, QuestHandlerKind kind, ushort zone, int csid, Fake f) =>
            C2MRun(C2MHandler(d, state, kind, zone, id: csid), f);

        Check("c3 C4: 8-4 is a CoP mission with its Lua file, The Last Verse next, no title; validates clean; eight " +
              "states",
              $"mission.cop.dawn scripts/missions/cop/8_4_Dawn {QC.Mission.Cop.TheLastVerse}  |8",
              C4Header(ms, d) + $"|{d.States.Count}");

        // ---- the first section: the log's current mission at Dawn or past it
        Fake Zy(int mission, double z)
        {
            var f = Current(mission);
            f.ZPos = z;
            return f;
        }
        Check("c3 C4: 8-4's _0zy section holds with the current mission Dawn or past it (The Last Verse), not before " +
              "(When Angels Fall); _0zy plays 140 at z 360 or below, 141 above",
              "False|True|True|ProgressEvent(140)|ProgressEvent(141)",
              $"{C4Holds(d, 0, Current(QC.Mission.Cop.WhenAngelsFall), m)}|{C4Holds(d, 0, Current(m), m)}|" +
              $"{C4Holds(d, 0, Current(QC.Mission.Cop.TheLastVerse), m)}|" +
              R(0, onTrig, gr, QC.Npc.TheGardenOfRuhmet._0zy, Zy(m, 360)) + "|" +
              R(0, onTrig, gr, QC.Npc.TheGardenOfRuhmet._0zy, Zy(m, 360.5)));

        // ---- the second section: the mission current
        Check("c3 C4: 8-4 Empyreal Paradox's zone-in plays 6 at Status 2 and 3 at 3, nothing at 1 or 4; TR_Entrance " +
              "plays 2 at Status 0 only",
              "null|Event(6)|Event(3)|null|ProgressEvent(2)|null",
              string.Join("|", new[]
              {
                  R(1, zin, ep, null, F(1)), R(1, zin, ep, null, F(2)), R(1, zin, ep, null, F(3)),
                  R(1, zin, ep, null, F(4)), R(1, onTrig, ep, QC.Npc.EmpyrealParadox.TREntrance, F(0)),
                  R(1, onTrig, ep, QC.Npc.EmpyrealParadox.TREntrance, F(1)),
              }));
        Fake Midnight()
        {
            var f = F(3);
            f.NextJstMidnight = 1800025200;
            return f;
        }
        Check("c3 C4: 8-4 finishes: 2 sets Status 1; 3 gives the Tear of Altana, Timer 1 until the next JST " +
              "midnight, " +
              "Option 31, Status 4, the char down in Al'Taieu at 0.18 -10 -470.43 (63); 6 sets Status 3 and the char " +
              "down at 540 0 -514 (63) in the zone",
              "Mission.SetVar(Status,1)|" +
              $"Player.GiveKeyItem({QC.KeyItem.TearOfAltana}),Mission.SetVar(Timer,1,1800025200)," +
              "Mission.SetVar(Option,31),Mission.SetVar(Status,4)," +
              $"Player.SetPos(0.18,-10,-470.43,63,{QC.Zone.Altaieu})|" +
              $"Mission.SetVar(Status,3),Player.SetPos(540,0,-514,63,{ep})",
              Run(1, fin, ep, 2, F(0)) + "|" + Run(1, fin, ep, 3, Midnight()) + "|" + Run(1, fin, ep, 6, F(2)));
        string Win(int win, int status)
        {
            var f = F(status);
            f.PlayerLocalVars["battlefieldWin"] = win;
            return Run(1, fin, ep, 32001, f);
        }
        Check("c3 C4: 8-4 32001 sets Status 2 only at Status 1 with Dawn won, each condition alone",
              "Mission.SetVar(Status,2)||", Win(QC.Battlefield.Dawn, 1) + "|" + Win(QC.Battlefield.Dawn, 2) + "|" +
              Win(QC.Battlefield.ApocalypseNigh, 1));
        Check("c3 C4: 8-4 Ru'Lude Gardens' area 1 plays 122 at Status 4 with Option 0 only, each alone (the Timer " +
              "not " +
              "read); its finish sets Status 5",
              "ProgressEvent(122)|null|null|ProgressEvent(122)|Mission.SetVar(Status,5)",
              string.Join("|", new[]
              {
                  R(1, area, rg, null, F(4)), R(1, area, rg, null, F(4, ("Option", 1))), R(1, area, rg, null, F(3)),
                  R(1, area, rg, null, F(4, ("Timer", 1))), Run(1, fin, rg, 122, F(4)),
              }));
        Check("c3 C4: 8-4 _6s1 plays 129 at Status 5 only (finish: Status 6, firstRing 1); _0qa 543 at 6 only " +
              "(Status 7); Lufaise's area 1 the cutscene 116 at 7 only (the Banisher of Emptiness title, Status 8)",
              "null|ProgressEvent(129)|null|Mission.SetVar(Status,6),Mission.SetVar(firstRing,1)|null|" +
              "ProgressEvent(543)|null|Mission.SetVar(Status,7)|null|ProgressCutscene(116)|null|" +
              $"Player.AddTitle({QC.Title.BanisherOfEmptiness}),Mission.SetVar(Status,8)",
              string.Join("|", new[]
              {
                  R(1, onTrig, uj, QC.Npc.UpperJeuno._6s1, F(4)), R(1, onTrig, uj, QC.Npc.UpperJeuno._6s1, F(5)),
                  R(1, onTrig, uj, QC.Npc.UpperJeuno._6s1, F(6)), Run(1, fin, uj, 129, F(5)),
                  R(1, onTrig, saf, QC.Npc.TavnazianSafehold._0qa, F(5)),
                  R(1, onTrig, saf, QC.Npc.TavnazianSafehold._0qa, F(6)),
                  R(1, onTrig, saf, QC.Npc.TavnazianSafehold._0qa, F(7)), Run(1, fin, saf, 543, F(6)),
                  R(1, area, lm, null, F(6)), R(1, area, lm, null, F(7)), R(1, area, lm, null, F(8)),
                  Run(1, fin, lm, 116, F(7)),
              }));
        string Norg(int csid, int status, int option) => Run(1, fin, norg, csid, WithOption(F(status), option));
        Check("c3 C4: 8-4 Norg's 232 and 234 finishes complete the mission with option 1 to 4 at Status 8 only",
              "Mission.Complete()|Mission.Complete()|||Mission.Complete()|Mission.Complete()||",
              string.Join("|", new[]
              {
                  Norg(232, 8, 1), Norg(232, 8, 4), Norg(232, 8, 0), Norg(232, 8, 5), Norg(234, 8, 1), Norg(234, 8, 4),
                  Norg(234, 7, 1), Norg(234, 8, 5),
              }));

        // ---- the final cutscene sections (2 Louverance bit 0, 3 Chebukkis bit 1, 4 bit 2, 5 bit 3, 6 bit 4)
        Fake Fc(int status, int timer, int option, int drop = 0, int mission = 0)
        {
            var f = F(status, ("Timer", timer), ("Option", option), ("coloredDropId", drop));
            if (mission != 0) f.CurrentMissions[C2bCop] = mission;
            return f;
        }
        string Holds(int state, int bit) =>
            string.Join(",", new[]
            {
                C4Holds(d, state, Fc(4, 0, 1 << bit), m), C4Holds(d, state, Fc(3, 0, 1 << bit), m),
                C4Holds(d, state, Fc(4, 1, 1 << bit), m), C4Holds(d, state, Fc(4, 0, 31 & ~(1 << bit)), m),
                C4Holds(d, state, Fc(4, 0, 1 << bit, 0, QC.Mission.Cop.TheLastVerse), m),
            });
        Check("c3 C4: 8-4 each final cutscene section holds at Status 4, Timer 0, its own bit, the mission current; " +
              "each part alone fails (Status 3, Timer 1, the other four bits, The Last Verse current)",
              "True,False,False,False,False|True,False,False,False,False|True,False,False,False,False|" +
              "True,False,False,False,False|True,False,False,False,False",
              string.Join("|", new[] { Holds(2, 0), Holds(3, 1), Holds(4, 2), Holds(5, 3), Holds(6, 4) }));
        Check("c3 C4: 8-4 the Chebukkis section also holds with a colored drop owed and bit 1 clear; not at Status 3 " +
              "or Timer 1 even so",
              "True|False|False", $"{C4Holds(d, 3, Fc(4, 0, 0, 4259), m)}|{C4Holds(d, 3, Fc(3, 0, 0, 4259), m)}|" +
                                  $"{C4Holds(d, 3, Fc(4, 1, 0, 4259), m)}");
        Fake L(int lprog, int option = 31) =>
            Fc(4, 0, option, 0).Also(f => f.MissionVars[$"{C2bCop}/{m}/LProg"] = lprog);
        Check("c3 C4: 8-4 Louverance: Hinaree plays 757 at LProg 0 (LProg 1), Uleguerand Range's zone-in 17 at 1 " +
              "(LProg 2), Southern San d'Oria's area 1 758 at 2 (bit 0 cleared), each at no other LProg (area 1 at 1 " +
              "and 3)",
              "ProgressEvent(757)|null|Mission.SetVar(LProg,1)|null|Event(17)|null|Mission.SetVar(LProg,2)|null|" +
              "ProgressEvent(758)|null|Mission.SetVar(Option,30)",
              string.Join("|", new[]
              {
                  R(2, onTrig, ssd, QC.Npc.SouthernSanDoria.Hinaree, L(0)),
                  R(2, onTrig, ssd, QC.Npc.SouthernSanDoria.Hinaree, L(1)), Run(2, fin, ssd, 757, L(0)),
                  R(2, zin, ur, null, L(0)), R(2, zin, ur, null, L(1)), R(2, zin, ur, null, L(2)),
                  Run(2, fin, ur, 17, L(1)), R(2, area, ssd, null, L(1)), R(2, area, ssd, null, L(2)),
                  R(2, area, ssd, null, L(3)), Run(2, fin, ssd, 758, L(2)),
              }));
        Fake C(int cprog, int drop = 0, bool give = true)
        {
            var f = Fc(4, 0, 2, drop);
            f.MissionVars[$"{C2bCop}/{m}/CProg"] = cprog;
            f.GiveResult = give;
            return f;
        }
        string Wm(Fake f) => R(3, onTrig, bb, QC.Npc.BibikiBay.Warmachine, f) + "/" + string.Join(",", f.Calls);
        Check("c3 C4: 8-4 Chebukkis: Chipmy-Popmy plays 619 at CProg 0 only (CProg 1)",
              "ProgressEvent(619)|null|Mission.SetVar(CProg,1)",
              R(3, onTrig, pw, QC.Npc.PortWindurst.ChipmyPopmy, C(0)) + "|" +
              R(3, onTrig, pw, QC.Npc.PortWindurst.ChipmyPopmy, C(1)) + "|" + Run(3, fin, pw, 619, C(0)));
        Check("c3 C4: 8-4 Warmachine: an owed drop given clears it and plays nothing; a failed give falls to CProg " +
              "1's " +
              "cutscene 43, else nothing; no drop owed: CProg 1 plays 43 with no give, CProg 0 nothing",
              "null/Player.GiveItem(4259),Mission.SetVar(coloredDropId,0)|ProgressCutscene(43)/Player.GiveItem(4259)|" +
              "null/Player.GiveItem(4259)|ProgressCutscene(43)/|null/",
              string.Join("|", new[]
              {
                  Wm(C(1, 4259)), Wm(C(1, 4259, false)), Wm(C(0, 4259, false)), Wm(C(1)), Wm(C(0)),
              }));
        Fake Roll(int roll, bool give)
        {
            var f = C(1, 0, give);
            f.Rolls.Enqueue(roll);
            return f;
        }
        Check("c3 C4: 8-4 finish 43: a Red Drop + randomInt(0, 7) given, or owed when the give fails; bit 1 cleared " +
              "either way",
              "Player.RandomInt(0,7),Player.GiveItem(4261),Mission.SetVar(Option,0)|" +
              "Player.RandomInt(0,7),Player.GiveItem(4265),Mission.SetVar(coloredDropId,4265),Mission.SetVar(Option,0)",
              Run(3, fin, bb, 43, Roll(3, true)) + "|" + Run(3, fin, bb, 43, Roll(7, false)));
        Check("c3 C4: 8-4 Shikarees: Mhaura's zone-in plays 322 (finish: bit 2 cleared); Jabbos: Oldton Movalpolos' " +
              "57 (bit 3); Tenzen: Cid 897 (bit 4)",
              "Event(322)|Mission.SetVar(Option,27)|Event(57)|Mission.SetVar(Option,23)|ProgressEvent(897)|" +
              "Mission.SetVar(Option,15)",
              string.Join("|", new[]
              {
                  R(4, zin, mh, null, Fc(4, 0, 31)), Run(4, fin, mh, 322, Fc(4, 0, 31)),
                  R(5, zin, om, null, Fc(4, 0, 31)), Run(5, fin, om, 57, Fc(4, 0, 31)),
                  R(6, onTrig, mw, QC.Npc.Metalworks.Cid, Fc(4, 0, 31)), Run(6, fin, mw, 897, Fc(4, 0, 31)),
              }));

        // ---- the rings (section 7)
        Fake Ring(int status, int timer, int firstRing = 0, int mission = 0, bool completed = false)
        {
            var f = F(status, ("Timer", timer), ("firstRing", firstRing));
            if (mission != 0) f.CurrentMissions[C2bCop] = mission;
            if (completed) f.CompletedMissions.Add($"{C2bCop}/{m}");
            return f;
        }
        int last = QC.Mission.Cop.TheLastVerse;
        Check("c3 C4: 8-4 the rings' section holds with the Timer 0 and either the mission current at Status 6 or " +
              "more, or it completed; each part alone fails",
              "True|True|False|False|True|False|False",
              string.Join("|", new[]
              {
                  C4Holds(d, 7, Ring(6, 0), m), C4Holds(d, 7, Ring(8, 0), m), C4Holds(d, 7, Ring(5, 0), m),
                  C4Holds(d, 7, Ring(6, 1), m), C4Holds(d, 7, Ring(0, 0, 0, last, true), m),
                  C4Holds(d, 7, Ring(0, 1, 0, last, true), m), C4Holds(d, 7, Ring(6, 0, 0, last), m),
              }));
        string S1(int firstRing, int held = 0)
        {
            var f = Ring(6, 0, firstRing);
            if (held != 0) f.Items.Add(held);
            return R(7, onTrig, uj, QC.Npc.UpperJeuno._6s1, f);
        }
        Check("c3 C4: 8-4 _6s1 in the rings' section: 84 at firstRing 1, 204 at 0; any of the three rings held " +
              "nothing (each alone)",
              "ProgressEvent(84)|ProgressEvent(204)|null|null|null",
              string.Join("|", new[]
              {
                  S1(1), S1(0), S1(1, QC.Item.RajasRing), S1(0, QC.Item.SattvaRing), S1(1, QC.Item.TamasRing),
              }));
        string Upd(int csid, int option) => Run(7, upd, uj, csid, WithOption(Ring(6, 0), option));
        string RingFin(int csid, int option, bool give = true)
        {
            var f = WithOption(Ring(6, 0, 1), option);
            f.GiveResult = give;
            return Run(7, fin, uj, csid, f);
        }
        Check("c3 C4: 8-4 updates 84 and 204 with option 4 send the three rings; option 3 nothing",
              $"Player.UpdateEvent({QC.Item.RajasRing},{QC.Item.SattvaRing},{QC.Item.TamasRing})|" +
              $"Player.UpdateEvent({QC.Item.RajasRing},{QC.Item.SattvaRing},{QC.Item.TamasRing})||",
              Upd(84, 4) + "|" + Upd(204, 4) + "|" + Upd(84, 3) + "|" + Upd(204, 3));
        Check("c3 C4: 8-4 finishes 84 and 204 with option 5 to 7 give that ring and set firstRing 0; a failed give " +
              "keeps it; 4 and 8 nothing",
              $"Player.GiveItem({QC.Item.RajasRing}),Mission.SetVar(firstRing,0)|" +
              $"Player.GiveItem({QC.Item.SattvaRing}),Mission.SetVar(firstRing,0)|" +
              $"Player.GiveItem({QC.Item.TamasRing}),Mission.SetVar(firstRing,0)|" +
              $"Player.GiveItem({QC.Item.RajasRing})||",
              string.Join("|", new[]
              {
                  RingFin(84, 5), RingFin(204, 6), RingFin(84, 7), RingFin(204, 5, false), RingFin(84, 4),
                  RingFin(204, 8),
              }));
    }

    static T Also<T>(this T value, Action<T> act)
    {
        act(value);
        return value;
    }

    static void RunC4DawnBattlefield()
    {
        var s = new QC.Battlefields.EmpyrealParadox.Dawn();
        var bd = (BattlefieldMissionData)s.Data;
        ushort ep = QC.Zone.EmpyrealParadox;
        Check("c3 C4: Dawn validates clean, its key and Lua file",
              "|battlefield.dawn scripts/battlefields/Empyreal_Paradox/dawn",
              $"{string.Join(" | ", BattlefieldScriptValidator.Validate(s))}|{s.Key} {s.LegacyPath}");
        Check("c3 C4: Dawn's fields (zone, id, no exp loss, mission, trusts, players, MAX_LEVEL, time, index, area, " +
              "grantXP, title, entry and exit NPCs, mission, required var and value, no wipe grace, groups)",
              $"{ep} {QC.Battlefield.Dawn} False True True 6 MAX_LEVEL 1800 0 0 2000 " +
              $"{QC.Title.AverterOfTheApocalypse} " +
              $"TR_Entrance Transcendental_Radiance 6/{QC.Mission.Cop.Dawn} Mission[6][840]Status 1 False 2",
              $"{bd.ZoneId} {bd.BattlefieldId} {bd.CanLoseExp} {bd.IsMission} {bd.AllowTrusts} {bd.MaxPlayers} " +
              $"{bd.LevelCapSetting} {bd.TimeLimit} {bd.Index} {bd.Area} {bd.GrantXP} {bd.Title} " +
              $"{string.Join(",", bd.EntryNpcs)} {string.Join(",", bd.ExitNpcs)} {(int)bd.MissionArea}/{bd.Mission} " +
              $"{bd.RequiredVar} {bd.RequiredValue} {bd.HasWipeGrace} {bd.Groups.Count}");
        string Ids(BattlefieldGroup g) => string.Join(";", g.AreaMobIds.Select(a =>
            string.Join(",", a.Select(o => o.Key + ":" + o.Offset))));
        Check("c3 C4: Dawn's groups: Promathia's first form (+0, +2, +4) with its allDeath; the second (+1, +3, +5), " +
              "not spawned, its allDeath",
              "PROMATHIA:0;PROMATHIA:2;PROMATHIA:4  True|PROMATHIA:1;PROMATHIA:3;PROMATHIA:5 False True",
              $"{Ids(bd.Groups[0])} {bd.Groups[0].Spawned} {bd.Groups[0].AllDeath != null}|" +
              $"{Ids(bd.Groups[1])} {bd.Groups[1].Spawned} {bd.Groups[1].AllDeath != null}");
        var won = new BattlefieldViewFake();
        bd.Groups[1].AllDeath(new BattlefieldContextFake(s) { Battlefield = won }, null);
        Check("c3 C4: Dawn's second group's allDeath sets WON", "Battlefield.SetStatus(2)",
              string.Join(",", won.Calls));

        // setupBattlefield
        foreach (int a in new[] { 1, 3 })
        {
            var bf = new BattlefieldViewFake { Area = a, InitiatorValue = (77, "Main") };
            var prishe = new BattlefieldMobFake { Id = 11 };
            var selhteus = new BattlefieldMobFake { Id = 12 };
            bf.InsertResults[11] = prishe;
            bf.InsertResults[12] = selhteus;
            var engine = new BattlefieldEngineFake();
            engine.PlayersById[77] = new BattlefieldPlayerFake { RaceId = 6 };
            s.SetupBattlefield(new BattlefieldContextFake(s) { Battlefield = bf, Engine = engine });
            string expect = a == 1
                ? "Mob.SetSpawn(-526.678,-120,509.175,192),Mob.Spawn(,),Mob.SetMobLevel(75)|" +
                  "Mob.SetSpawn(-513.769,-120,509.175,170),Mob.Spawn(,),Mob.SetMobLevel(75)"
                : "Mob.SetSpawn(-527.256,120,-533.638,192),Mob.Spawn(,),Mob.SetMobLevel(75)|" +
                  "Mob.SetSpawn(-506.98,120,-533.638,170),Mob.Spawn(,),Mob.SetMobLevel(75)";
            Check($"c3 C4: Dawn's setupBattlefield in area {a}: initRace the initiator's race, Prishe (11) and " +
                  "Selhteus (12) inserted as allies in the battlefield, set at the area's spots, spawned, level 75",
                  "Battlefield.SetLocalVar(initRace,6),Battlefield.InsertEntity(11,True,True)," +
                  "Battlefield.InsertEntity(12,True,True)|" + expect,
                  string.Join(",", bf.Calls) + "|" + string.Join(",", prishe.Calls) + "|" +
                  string.Join(",", selhteus.Calls));
        }
        var none = new BattlefieldViewFake { Area = 2, InitiatorValue = (78, "Gone") };
        none.InsertResults[11] = new BattlefieldMobFake { Id = 11 };
        none.InsertResults[12] = new BattlefieldMobFake { Id = 12 };
        s.SetupBattlefield(new BattlefieldContextFake(s) { Battlefield = none, Engine = new BattlefieldEngineFake() });
        Check("c3 C4: Dawn's setupBattlefield with the initiator gone sets no initRace",
              "Battlefield.InsertEntity(11,True,True),Battlefield.InsertEntity(12,True,True)",
              string.Join(",", none.Calls));

        // the first group's allDeath
        var view = new BattlefieldViewFake { Area = 2 };
        var pr = new BattlefieldMobFake { Id = 11, Pool = 3199 };
        var se = new BattlefieldMobFake { Id = 12, Pool = 5417 };
        var other = new BattlefieldMobFake { Id = 13, Pool = 1 };
        var notMob = new BattlefieldMobFake { Id = 14, Pool = 3199, IsMob = false };
        view.AllyList.AddRange(new IBattlefieldMob[] { pr, se, other, notMob });
        var p1 = new BattlefieldPlayerFake();
        var p2 = new BattlefieldPlayerFake();
        view.PlayerList.AddRange(new IBattlefieldPlayer[] { p1, p2 });
        var eng = new BattlefieldEngineFake { SystemTime = 1000 };
        bd.Groups[0].AllDeath(new BattlefieldContextFake(s) { Battlefield = view, Engine = eng }, null);
        Check("c3 C4: Dawn's first group's allDeath (area 2): each ally mob's local vars reset; Prishe (pool 3199) " +
              "set at her spot with the engage wait 180 from now; Selhteus (5417) at his; another pool only reset; a " +
              "non-mob untouched; every player plays 32004 (2)",
              "Mob.ResetLocalVars(),Mob.SetPos(513.225,0,509.71,192),Mob.SetLocalVar([helperNpc]engageWaitTime,180)," +
              "Mob.SetLocalVar(engageWait,1180)|Mob.ResetLocalVars(),Mob.SetPos(531.881,0,510.375,170)|" +
              "Mob.ResetLocalVars()||Player.StartEvent(32004,2)|Player.StartEvent(32004,2)",
              string.Join("|", new[] { pr, se, other, notMob }.Select(x => string.Join(",", x.Calls))) + "|" +
              string.Join(",", p1.Calls) + "|" + string.Join(",", p2.Calls));

        // onEventFinishBattlefield and onEventFinishWin
        const int promathia = 16924672;
        string Phase(int a, bool up)
        {
            var e = new BattlefieldEngineFake();
            e.ZoneMobIds[$"{ep}:PROMATHIA"] = promathia;
            int phase2 = promathia + (a - 1) * 2 + 1;
            var mob = new BattlefieldMobFake { Id = phase2 };
            if (up) mob.SpawnInWorld();
            e.MobsById[phase2] = mob;
            var p = new BattlefieldPlayerFake { CurrentBattlefield = new BattlefieldViewFake { Area = a } };
            s.OnEventFinishBattlefield(new BattlefieldContextFake(s) { Player = p, Engine = e }, 32004, 0);
            return string.Join(",", p.Calls) + "/" + string.Join(",", e.Calls);
        }
        Check("c3 C4: Dawn's onEventFinishBattlefield sets the char at the area's spot, then spawns the second form " +
              "(PROMATHIA + 2 * (area - 1) + 1) unless it is up",
              $"Player.SetPos(-520,-120,493.778,190)/SpawnMob({promathia + 1})|" +
              $"Player.SetPos(520,0,493.778,190)/SpawnMob({promathia + 3})|Player.SetPos(-520,120,-545.538,190)/|" +
              $"Player.SetPos(-520,120,-545.538,190)/SpawnMob({promathia + 5})",
              Phase(1, false) + "|" + Phase(2, false) + "|" + Phase(3, true) + "|" + Phase(3, false));
        var w = new BattlefieldPlayerFake();
        s.OnEventFinishWin(new BattlefieldContextFake(s) { Player = w }, 32001, 0);
        Check("c3 C4: Dawn's onEventFinishWin only sets the char down at 540 0 -514 (63) in the zone (no experience, " +
              "no title: the kept quirk)",
              $"Player.SetPos(540,0,-514,63,{ep})", string.Join(",", w.Calls));
    }
}
