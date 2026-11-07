// XI.Verify.Quests -- quest rework B3b Windurst group 31 (call-only: Class Reunion, Carbuncle Debacle; SMN AF2 / AF3
// at the door _6n2 and Koru-Moru, with Shantotto, Fuepepe, Furakku-Norakku, Gulmama, Ripapa and Agado-Pugado). The
// golden flows take every arm a char reaches in play, the GM-set states (the vars on an available, accepted or
// completed char, Koru-Moru's three quests' arms open at once), the level gates' boundaries (49 at the default 50, at
// 49.4 and at 49 by setting), not SMN, needToZone, The Puppet Master not completed, the full inventory and the rare
// rewards held. These checks add only what no flow can see (fake-only): the replies' kinds (in an NPC call the bridge
// renders an Event, a ProgressEvent and a PriorityEvent alike, as player:startEvent:
// ZoneHost.QuestCalls.cs RenderCallReply) and the handlers' and finishes' calls in order, a refused reward's among
// them.
// W15Handlers is in WindurstGroup15.cs.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup31()
    {
        Console.WriteLine("== quest rework B3b Windurst group 31: the reply kinds and the finishes (call-only) ==");
        var cr = new QC.Windurst.ClassReunion().Compile();
        var cd = new QC.Windurst.CarbuncleDebacle().Compile();
        const string Tpm = "Windurst/THE_PUPPET_MASTER", Cr = "Windurst/CLASS_REUNION";
        const string CrVar = "ClassReunionProgress", CdVar = "CarbuncleDebacleProgress";
        const string Fupepe = "ClassReunion_TalkedToFupepe", Furakku = "ClassReunion_TalkedToFurakku";

        string Problems(QuestScriptDefinition def) => string.Join(" | ", QuestScriptValidator.Validate(def));
        Check("windurst g31:Class Reunion clean", "", Problems(cr));
        Check("windurst g31:Carbuncle Debacle clean", "", Problems(cd));

        // a fresh fake: that status, an SMN 75, AF2 / AF3 50, The Puppet Master and Class Reunion as given
        Fake F(QuestStatus status, int var = 0, string varName = null, QuestStatus tpm = QuestStatus.Completed,
            QuestStatus crStatus = QuestStatus.Completed, int job = QC.Job.Smn, int lvl = 75, bool zone = false)
        {
            var f = new Fake { Status = status, MainJob = job, MainLevel = lvl, ZoningFlag = zone };
            f.MainNumbers["AF2_QUEST_LEVEL"] = 50;
            f.MainNumbers["AF3_QUEST_LEVEL"] = 50;
            f.QuestStatuses[Tpm] = tpm;
            f.QuestStatuses[Cr] = crStatus;
            if (varName != null) f.CharVars[varName] = var;
            return f;
        }
        string Say(QuestReply r) => r == null ? "null" : QuestContextFake.Describe(r);
        QuestHandlerDecl H(QuestScriptDefinition def, QuestHandlerKind kind, string npc) =>
            W15Handlers(def, kind).Single(h => h.Entity == npc);
        // the reply, then the calls other than the reads
        string Run(QuestScriptDefinition def, QuestHandlerKind kind, string npc, Fake f)
        {
            string reply = Say(H(def, kind, npc).Reply(f));
            string calls = string.Join(";",
                f.Calls.Where(c => !c.StartsWith("Trade.Has") && !c.StartsWith("Player.Has")));
            return calls.Length == 0 ? reply : reply + " " + calls;
        }
        const QuestHandlerKind T = QuestHandlerKind.OnTrigger, Tr = QuestHandlerKind.OnTrade;
        const int Smn = QC.Job.Smn, War = QC.Job.War;
        const QuestStatus Av = QuestStatus.Available, Ac = QuestStatus.Accepted, Co = QuestStatus.Completed;
        const string Door = "_6n2", Koru = "Koru-Moru";

        // the door: Class Reunion's 413, then Carbuncle Debacle's 415
        Check("windurst g31:the door's 413 (Event): TPM completed, available, SMN at AF2, no needToZone",
              "Event(413)|null|null|null|null|null|null|Event(413)",
              string.Join("|", Run(cr, T, Door, F(Av)), Run(cr, T, Door, F(Av, tpm: Ac)), Run(cr, T, Door, F(Ac)),
                  Run(cr, T, Door, F(Co)), Run(cr, T, Door, F(Av, lvl: 49)), Run(cr, T, Door, F(Av, job: War)),
                  Run(cr, T, Door, F(Av, zone: true)), Run(cr, T, Door, F(Av, lvl: 50))));
        Check("windurst g31:the door's 415 (Event): TPM and Class Reunion completed, available, SMN at AF3",
              "Event(415)|null|null|null|null|null|null|null|Event(415)",
              string.Join("|", Run(cd, T, Door, F(Av)), Run(cd, T, Door, F(Av, tpm: Ac)),
                  Run(cd, T, Door, F(Av, crStatus: Ac)), Run(cd, T, Door, F(Ac)), Run(cd, T, Door, F(Co)),
                  Run(cd, T, Door, F(Av, lvl: 49)), Run(cd, T, Door, F(Av, job: War)),
                  Run(cd, T, Door, F(Av, zone: true)), Run(cd, T, Door, F(Av, lvl: 50))));

        // Koru-Moru: Carbuncle Debacle's arms (416 accepted at the var 1 or 2: user ruling 2026-11-04, kept Lua bugs),
        // then Class Reunion's
        string CdK(QuestStatus s, int var, QuestStatus tpm = Co, QuestStatus crs = Co) =>
            Run(cd, T, Koru, F(s, var, CdVar, tpm, crs));
        Check("windurst g31:Koru-Moru's Carbuncle Debacle replies, as Event (416 accepted: user ruling 2026-11-04)",
              "Event(416)|Event(416)|null|Event(420)|null|Event(417)|Event(418)|Event(419)|null|null|" +
              "null|null|null|Event(420)|Event(420)|null|null",
              string.Join("|", CdK(Ac, 1), CdK(Ac, 2), CdK(Av, 2), CdK(Co, 2), CdK(Av, 1), CdK(Ac, 4), CdK(Ac, 5),
                  CdK(Ac, 7), CdK(Ac, 3), CdK(Ac, 6), CdK(Av, 4), CdK(Av, 7), CdK(Av, 0), CdK(Co, 1), CdK(Co, 7),
                  CdK(Co, 1, Ac), CdK(Co, 1, Co, Ac)));
        Fake K(QuestStatus s, int var, int talk1 = 0, int talk2 = 0, QuestStatus tpm = Co)
        {
            var f = F(s, var, CrVar, tpm);
            f.CharVars[Fupepe] = talk1;
            f.CharVars[Furakku] = talk2;
            return f;
        }
        string CrK(QuestStatus s, int var, int talk1 = 0, int talk2 = 0, QuestStatus tpm = Co) =>
            Run(cr, T, Koru, K(s, var, talk1, talk2, tpm));
        Check("windurst g31:Koru-Moru's Class Reunion replies, as Event",
              "Event(412,0,450,17299,0,0,0,0,0)|Event(414,0,0,17299,0,0,0,0,0)|Event(408)|Event(408)|Event(408)|" +
              "Event(408)|Event(408)|Event(408)|Event(410)|null|null|null|null|null|Event(411)|Event(411)|null|null",
              string.Join("|", CrK(Ac, 1), CrK(Ac, 2), CrK(Ac, 3), CrK(Ac, 4, 1), CrK(Ac, 3, 0, 1), CrK(Ac, 3, 2, 1),
                  CrK(Ac, 3, 1, 2), CrK(Ac, 6, 1), CrK(Ac, 6, 1, 1), CrK(Ac, 5, 1, 1), CrK(Ac, 7, 1, 1),
                  CrK(Ac, 0), CrK(Av, 1), CrK(Av, 6, 1, 1), CrK(Co, 1), CrK(Co, 6, 1, 1),
                  CrK(Co, 1, 0, 0, Av), CrK(Av, 2)));
        Check("windurst g31:Koru-Moru's trade (his script checks the Astragaloi): 407 at the var 2, as Event",
              "Event(407)|null|null|null|null",
              string.Join("|", Run(cr, Tr, Koru, K(Ac, 2)), Run(cr, Tr, Koru, K(Ac, 1)), Run(cr, Tr, Koru, K(Ac, 3)),
                  Run(cr, Tr, Koru, K(Av, 2)), Run(cr, Tr, Koru, K(Co, 2))));

        // the students and Gulmama
        string Cr1(string npc, QuestStatus s, int var, int talk1 = 0, int talk2 = 0) =>
            Run(cr, T, npc, K(s, var, talk1, talk2));
        Check("windurst g31:Shantotto's 409, Fuepepe's 817, Furakku-Norakku's 816, as Event",
              "Event(409)|null|null|null|null|" +
              "Event(817)|Event(817)|null|null|Event(817)|null|null|" +
              "Event(816)|Event(816)|null|null|Event(816)|null|null",
              string.Join("|", Cr1("Shantotto", Ac, 3), Cr1("Shantotto", Ac, 2), Cr1("Shantotto", Ac, 4),
                  Cr1("Shantotto", Av, 3), Cr1("Shantotto", Co, 3),
                  Cr1("Fuepepe", Ac, 3), Cr1("Fuepepe", Ac, 7), Cr1("Fuepepe", Ac, 2), Cr1("Fuepepe", Ac, 3, 1),
                  Cr1("Fuepepe", Ac, 3, 2), Cr1("Fuepepe", Av, 3), Cr1("Fuepepe", Co, 3),
                  Cr1("Furakku-Norakku", Ac, 3), Cr1("Furakku-Norakku", Ac, 7), Cr1("Furakku-Norakku", Ac, 2),
                  Cr1("Furakku-Norakku", Ac, 3, 0, 1), Cr1("Furakku-Norakku", Ac, 3, 0, 2),
                  Cr1("Furakku-Norakku", Av, 3), Cr1("Furakku-Norakku", Co, 3)));
        string Gul(QuestStatus s, int var, bool held = false)
        {
            var f = K(s, var);
            if (held) f.Items.Add(QC.Item.IcePendulum);
            return Run(cr, T, "Gulmama", f);
        }
        Check("windurst g31:Gulmama's 713 / 712, as Event (713 with the pendulum held too)",
              "Event(713,0,1171,0,0,0,0,0,0)|Event(713,0,1171,0,0,0,0,0,0)|Event(712,0,1171,0,0,0,0,0,0)|null|" +
              "null|null|null|null|null",
              string.Join("|", Gul(Ac, 4), Gul(Ac, 4, true), Gul(Ac, 5), Gul(Ac, 5, true), Gul(Ac, 3), Gul(Ac, 6),
                  Gul(Av, 4), Gul(Co, 4), Gul(Co, 5)));

        // Ripapa and Agado-Pugado
        string Rip(QuestStatus s, int var, bool held = false)
        {
            var f = F(s, var, CdVar);
            if (held) f.Items.Add(QC.Item.LightningPendulum);
            return Run(cd, T, "Ripapa", f);
        }
        Check("windurst g31:Ripapa's 10022 / 10023, as Event",
              "Event(10022)|Event(10022)|Event(10023,0,1172,0,0,0,0,0,0)|null|null|null|null|null|null",
              string.Join("|", Rip(Ac, 2), Rip(Ac, 2, true), Rip(Ac, 3), Rip(Ac, 3, true), Rip(Ac, 4), Rip(Av, 2),
                  Rip(Co, 2), Rip(Av, 3), Rip(Co, 3)));
        string Aga(QuestStatus s, int var, bool charm = false, bool held = false)
        {
            var f = F(s, var, CdVar);
            if (charm) f.KeyItems[QC.KeyItem.DazeBreakerCharm] = true;
            if (held) f.Items.Add(QC.Item.WindPendulum);
            return Run(cd, T, "Agado-Pugado", f);
        }
        Check("windurst g31:Agado-Pugado's 86 / 87 / 88, as Event",
              "Event(86)|null|Event(87,0,1174,0,0,0,0,0,0)|Event(88)|Event(87,0,1174,0,0,0,0,0,0)|null|null|null|" +
              "null|null",
              string.Join("|", Aga(Ac, 5, true), Aga(Ac, 5), Aga(Ac, 6), Aga(Ac, 6, false, true), Aga(Ac, 6, true),
                  Aga(Ac, 7, true), Aga(Av, 5, true), Aga(Co, 5, true), Aga(Av, 6), Aga(Co, 6)));

        // the finishes' calls, in order, on that option and free slots
        string Fin(QuestScriptDefinition def, int csid, int option = 0, int free = 30)
        {
            var h = def.States.SelectMany(s => s.Handlers)
                       .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Id == csid);
            var f = new Fake { Status = QuestStatus.Accepted, Csid = csid, Option = option, FreeSlots = free };
            h.Run(f);
            return string.Join(";", f.Calls.Where(c => !c.StartsWith("Player.CharVar(")));
        }
        Check("windurst g31:Class Reunion's finishes: 413 (options 0 / 1), 412, 407, 410 given / refused, 409, 817, " +
              "816",
              "Quest.Begin();Player.GiveKeyItem(450);Player.SetCharVar(ClassReunionProgress,1,0)|" +
              "Quest.Begin();Player.GiveKeyItem(450);Player.SetCharVar(ClassReunionProgress,1,0)|" +
              "Player.DelKeyItem(450);Player.SetCharVar(ClassReunionProgress,2,0)|" +
              "Trade.Complete();Player.SetCharVar(ClassReunionProgress,3,0)|" +
              "Player.AddItem(14228,1);Text.Special(239:ITEM_OBTAINED,14228);" +
              "Player.CompleteQuest(Windurst,CLASS_REUNION);" +
              "Player.SetCharVar(ClassReunionProgress,0,0);Player.SetCharVar(ClassReunion_TalkedToFurakku,0,0);" +
              "Player.SetCharVar(ClassReunion_TalkedToFupepe,0,0);Player.SetNeedToZone(True);Player.AddFame(2,20)|" +
              "Text.Special(239:ITEM_CANNOT_BE_OBTAINED,14228)|" +
              "Player.SetCharVar(ClassReunionProgress,4,0)|Player.SetCharVar(ClassReunion_TalkedToFupepe,1,0)|" +
              "Player.SetCharVar(ClassReunion_TalkedToFurakku,1,0)",
              string.Join("|", Fin(cr, 413), Fin(cr, 413, 1), Fin(cr, 412), Fin(cr, 407), Fin(cr, 410),
                  Fin(cr, 410, 0, 0), Fin(cr, 409), Fin(cr, 817), Fin(cr, 816)));
        Check("windurst g31:Gulmama's 713 / 712 finishes: the pendulum given / refused",
              "Player.AddItem(1171,1);Text.Special(231:ITEM_OBTAINED,1171);" +
              "Player.SetCharVar(ClassReunionProgress,5,0)|" +
              "Text.Special(231:ITEM_CANNOT_BE_OBTAINED,1171)|" +
              "Player.AddItem(1171,1);Text.Special(231:ITEM_OBTAINED,1171);" +
              "Player.SetCharVar(ClassReunionProgress,5,0)|" +
              "Text.Special(231:ITEM_CANNOT_BE_OBTAINED,1171)",
              string.Join("|", Fin(cr, 713), Fin(cr, 713, 0, 0), Fin(cr, 712), Fin(cr, 712, 0, 0)));
        Check("windurst g31:Carbuncle Debacle's finishes: 415 (options 0 / 1), 416, 417, 419 given / refused",
              "Quest.Begin();Player.SetCharVar(CarbuncleDebacleProgress,1,0)|" +
              "Quest.Begin();Player.SetCharVar(CarbuncleDebacleProgress,1,0)|" +
              "Player.SetCharVar(CarbuncleDebacleProgress,2,0)|" +
              "Player.SetCharVar(CarbuncleDebacleProgress,5,0);Player.GiveKeyItem(448)|" +
              "Player.AddItem(12520,1);Text.Special(239:ITEM_OBTAINED,12520);Player.AddTitle(203);" +
              "Player.CompleteQuest(Windurst,CARBUNCLE_DEBACLE);" +
              "Player.AddFame(2,20);Player.SetCharVar(CarbuncleDebacleProgress,0,0);Player.SetNeedToZone(True)|" +
              "Text.Special(239:ITEM_CANNOT_BE_OBTAINED,12520)",
              string.Join("|", Fin(cd, 415), Fin(cd, 415, 1), Fin(cd, 416), Fin(cd, 417), Fin(cd, 419),
                  Fin(cd, 419, 0, 0)));
        Check("windurst g31:Ripapa's and Agado-Pugado's finishes: the pendulums given / refused",
              "Player.AddItem(1172,1);Text.Special(249:ITEM_OBTAINED,1172);" +
              "Player.SetCharVar(CarbuncleDebacleProgress,3,0)|" +
              "Text.Special(249:ITEM_CANNOT_BE_OBTAINED,1172)|" +
              "Player.AddItem(1172,1);Text.Special(249:ITEM_OBTAINED,1172);" +
              "Player.SetCharVar(CarbuncleDebacleProgress,3,0)|" +
              "Player.AddItem(1174,1);Text.Special(247:ITEM_OBTAINED,1174);" +
              "Player.SetCharVar(CarbuncleDebacleProgress,6,0)|" +
              "Text.Special(247:ITEM_CANNOT_BE_OBTAINED,1174)|" +
              "Player.AddItem(1174,1);Text.Special(247:ITEM_OBTAINED,1174);" +
              "Player.SetCharVar(CarbuncleDebacleProgress,6,0)",
              string.Join("|", Fin(cd, 10022), Fin(cd, 10022, 0, 0), Fin(cd, 10023), Fin(cd, 86), Fin(cd, 86, 0, 0),
                  Fin(cd, 87)));

        // the free-slot tests at exactly one free slot (a flow's fill-inventory leaves none): given
        Check("windurst g31:one free slot is enough for the spats, the horn and the three pendulums",
              "14228|12520|1171|1172|1174",
              string.Join("|", new[] { Fin(cr, 410, 0, 1), Fin(cd, 419, 0, 1), Fin(cr, 713, 0, 1), Fin(cd, 10022, 0, 1),
                  Fin(cd, 86, 0, 1) }.Select(s => s.StartsWith("Player.AddItem(") ? s.Substring(15, s.IndexOf(',') - 15)
                      : "refused")));
    }
}
