// XI.Verify.Quests -- quest rework B3b Windurst group 30 (call-only: The Three Magi, Recollections,
// The Root of the Problem; BLM AF1-3 at Chumimi, with Xarcabard's qm1, Laityn, the Ore Door and Koru-Moru). The golden
// flows take every arm a char reaches in play, the GM-set states (her three quests' arms open at once, the vars on an
// available, accepted or completed char), the level gates' boundaries (39 / 49 / 50 at the default AF levels, 39 at
// 39.4 and 49 at 49.4 by setting, AF3_QUEST_LEVEL 51 and 49), not BLM, needToZone, the full inventory and the rare
// reward held, the non-exact crystal trade, the Chaos Elemental up, and Koru-Moru's other quests and SMN AF arms.
// These checks add only what no flow can see (fake-only): the replies' kinds (in an NPC call the bridge renders an
// Event, a ProgressEvent and a PriorityEvent alike, as player:startEvent: ZoneHost.QuestCalls.cs RenderCallReply) and
// the handlers' and finishes' calls in order, a refused reward's among them.
// W15Handlers is in WindurstGroup15.cs.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup30()
    {
        Console.WriteLine("== quest rework B3b Windurst group 30: the reply kinds and the finishes (call-only) ==");
        var ttm = new QC.Windurst.TheThreeMagi().Compile();
        var rec = new QC.Windurst.Recollections().Compile();
        var root = new QC.Windurst.TheRootOfTheProblem().Compile();
        const string Ttm = "Windurst/THE_THREE_MAGI", Rec = "Windurst/RECOLLECTIONS";

        string Problems(QuestScriptDefinition def) => string.Join(" | ", QuestScriptValidator.Validate(def));
        Check("windurst g30:The Three Magi clean", "", Problems(ttm));
        Check("windurst g30:Recollections clean", "", Problems(rec));
        Check("windurst g30:The Root of the Problem clean", "", Problems(root));

        // a fresh fake: that status, the job and level, AF1 40 / AF2 50 / AF3 50
        Fake F(QuestStatus status, int job, int lvl)
        {
            var f = new Fake { Status = status, MainJob = job, MainLevel = lvl };
            f.MainNumbers["AF1_QUEST_LEVEL"] = 40;
            f.MainNumbers["AF2_QUEST_LEVEL"] = 50;
            f.MainNumbers["AF3_QUEST_LEVEL"] = 50;
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
        const int Blm = QC.Job.Blm, War = QC.Job.War;
        const QuestStatus Av = QuestStatus.Available, Ac = QuestStatus.Accepted, Co = QuestStatus.Completed;
        const string Chumimi = "Chumimi", Koru = "Koru-Moru";

        // The Three Magi
        string TtmT(QuestStatus s, int job = Blm, int lvl = 40) => Run(ttm, T, Chumimi, F(s, job, lvl));
        Check("windurst g30:Chumimi's The Three Magi replies, as Event",
              "Event(260,0,613,0,0,582,1104)|null|null|Event(261,0,0,0,0,582,1104)|Event(261,0,0,0,0,582,1104)|null",
              string.Join("|", TtmT(Av), TtmT(Av, Blm, 39), TtmT(Av, War, 75), TtmT(Ac), TtmT(Ac, War, 1), TtmT(Co)));
        string Trade(QuestScriptDefinition def, string npc, Fake f, params (int Item, int Qty)[] items)
        {
            foreach (var (item, qty) in items) f.TradeItems[item] = qty;
            return Run(def, Tr, npc, f);
        }
        var stone = (QC.Item.Glowstone, 1);
        var glue = (QC.Item.BlockOfAnimalGlue, 1);
        Check("windurst g30:Chumimi's trade: the Glowstone alone while accepted, as Event",
              "Event(269)|null|null|null|null",
              string.Join("|", Trade(ttm, Chumimi, F(Ac, War, 1), stone),
                  Trade(ttm, Chumimi, F(Ac, Blm, 75), stone, glue), Trade(ttm, Chumimi, F(Ac, Blm, 75), glue),
                  Trade(ttm, Chumimi, F(Av, Blm, 75), stone), Trade(ttm, Chumimi, F(Co, Blm, 75), stone)));
        Fake Qm(QuestStatus s, bool up = false, bool stoneHeld = false, bool pop = true)
        {
            var f = F(s, War, 1);
            if (up) f.SpawnedMobs.Add("CHAOS_ELEMENTAL+0");
            if (stoneHeld) f.Items.Add(QC.Item.Glowstone);
            f.PopResult = pop;
            return f;
        }
        Check("windurst g30:qm1's messages (MessageSpecial)",
              "MessageSpecial(112:DO_NOT_SENSE)|MessageSpecial(112:DO_NOT_SENSE)|MessageSpecial(112:SENSE_EVIL)|" +
              "MessageSpecial(112:NOTHING_OUT_OF_ORDINARY)|MessageSpecial(112:NOTHING_OUT_OF_ORDINARY)",
              string.Join("|", Run(ttm, T, "qm1", Qm(Ac, up: true)), Run(ttm, T, "qm1", Qm(Av, up: true)),
                  Run(ttm, T, "qm1", Qm(Ac)), Run(ttm, T, "qm1", Qm(Av)), Run(ttm, T, "qm1", Qm(Co))));
        var crystal = (QC.Item.FadedCrystal, 1);
        Check("windurst g30:qm1's trade: the pop, then the confirm (no reply)",
              "null Player.PopFromQm(CHAOS_ELEMENTAL,True,0);Trade.Confirm()|" +
              "null Player.PopFromQm(CHAOS_ELEMENTAL,True,0);Trade.Confirm()|" +
              "null Player.PopFromQm(CHAOS_ELEMENTAL,True,0)|null|null|null",
              string.Join("|", Trade(ttm, "qm1", Qm(Ac), crystal), Trade(ttm, "qm1", Qm(Ac), crystal, glue),
                  Trade(ttm, "qm1", Qm(Ac, pop: false), crystal), Trade(ttm, "qm1", Qm(Ac, stoneHeld: true), crystal),
                  Trade(ttm, "qm1", Qm(Av), crystal), Trade(ttm, "qm1", Qm(Ac), glue)));

        // Recollections
        string RecT(QuestStatus s, QuestStatus ttmStatus, int job = Blm, int lvl = 50, bool zone = false,
            bool ki = false)
        {
            var f = F(s, job, lvl);
            f.QuestStatuses[Ttm] = ttmStatus;
            f.ZoningFlag = zone;
            if (ki) f.KeyItems[QC.KeyItem.FoeFinderMkI] = true;
            return Run(rec, T, Chumimi, f);
        }
        Check("windurst g30:Chumimi's Recollections replies, as Event",
              "Event(270,0,1105)|Event(268)|Event(268)|null|null|null|null|Event(275)|Event(275)|null",
              string.Join("|", RecT(Av, Co), RecT(Av, Co, Blm, 49), RecT(Av, Co, War, 75), RecT(Av, Co, Blm, 50, true),
                  RecT(Av, Ac), RecT(Ac, Co), RecT(Av, Av, War, 75, false, true), RecT(Ac, Av, War, 1, false, true),
                  RecT(Ac, Co, Blm, 75, true, true), RecT(Co, Co, Blm, 75, false, true)));
        Fake RecVar(QuestStatus s, int var)
        {
            var f = F(s, Blm, 75);
            f.CharVars["recollectionsQuest"] = var;
            return f;
        }
        var seeds = (QC.Item.BagOfSeeds, 1);
        var key = (QC.Item.WhineCellarKey, 1);
        Check("windurst g30:Chumimi's seeds, Laityn and the Ore Door, as Event",
              "Event(271,0,520)|Event(271,0,520)|null|null|null|null|" +
              "Event(10003)|null|null|null|" +
              "Event(8,1106)|null|null|null|null",
              string.Join("|", Trade(rec, Chumimi, RecVar(Ac, 0), seeds), Trade(rec, Chumimi, RecVar(Ac, 1), seeds),
                  Trade(rec, Chumimi, RecVar(Ac, 2), seeds), Trade(rec, Chumimi, RecVar(Ac, 1), seeds, glue),
                  Trade(rec, Chumimi, RecVar(Av, 0), seeds), Trade(rec, Chumimi, RecVar(Co, 0), seeds),
                  Run(rec, T, "Laityn", RecVar(Ac, 0)), Run(rec, T, "Laityn", RecVar(Ac, 1)),
                  Run(rec, T, "Laityn", RecVar(Av, 0)), Run(rec, T, "Laityn", RecVar(Co, 0)),
                  Trade(rec, "_4i5", RecVar(Ac, 2), key), Trade(rec, "_4i5", RecVar(Ac, 3), key),
                  Trade(rec, "_4i5", RecVar(Ac, 2), key, glue), Trade(rec, "_4i5", RecVar(Av, 2), key),
                  Trade(rec, "_4i5", RecVar(Co, 2), key)));

        // The Root of the Problem
        string RootT(QuestStatus s, int var, QuestStatus recStatus = Co, int job = Blm, int lvl = 50, bool zone = false)
        {
            var f = F(s, job, lvl);
            f.QuestStatuses[Rec] = recStatus;
            f.ZoningFlag = zone;
            f.CharVars["rootProblem"] = var;
            return Run(root, T, Chumimi, f);
        }
        Check("windurst g30:Chumimi's The Root of the Problem replies, as Event (NoAction at another var)",
              "Event(276,0,829)|null|null|null|null|null|Event(277,0,829)|Event(279)|Event(281)|" +
              "NoAction()|NoAction()|" +
              "Event(277,0,829)|null",
              string.Join("|", RootT(Av, 0), RootT(Av, 0, Co, Blm, 49), RootT(Av, 0, Co, War, 75),
                  RootT(Av, 0, Co, Blm, 75, true), RootT(Av, 0, Ac), RootT(Av, 0, Av), RootT(Ac, 1), RootT(Ac, 2),
                  RootT(Ac, 3), RootT(Ac, 0), RootT(Ac, 4), RootT(Ac, 1, Av, War, 1), RootT(Co, 1)));
        Fake RootVar(QuestStatus s, int var)
        {
            var f = F(s, Blm, 75);
            f.CharVars["rootProblem"] = var;
            return f;
        }
        var silk = (QC.Item.SquareOfSilkCloth, 1);
        Check("windurst g30:Chumimi's silk (Event), Koru-Moru's 348 (Event) and his trade's calls (349 started first)",
              "Event(278)|null|null|null|null|" +
              "Event(348,0,829)|null|null|null|" +
              "null Player.StartEvent(349);Trade.Complete();Player.SetCharVar(rootProblem,2,0)|" +
              "null Player.StartEvent(349);Trade.Complete();Player.SetCharVar(rootProblem,2,0)|null|null",
              string.Join("|", Trade(root, Chumimi, RootVar(Ac, 1), silk), Trade(root, Chumimi, RootVar(Ac, 2), silk),
                  Trade(root, Chumimi, RootVar(Ac, 1), silk, glue), Trade(root, Chumimi, RootVar(Av, 1), silk),
                  Trade(root, Chumimi, RootVar(Co, 1), silk),
                  Run(root, T, Koru, RootVar(Ac, 1)), Run(root, T, Koru, RootVar(Ac, 2)),
                  Run(root, T, Koru, RootVar(Av, 1)), Run(root, T, Koru, RootVar(Co, 1)),
                  Trade(root, Koru, RootVar(Ac, 1), silk), Trade(root, Koru, RootVar(Ac, 3), silk),
                  Trade(root, Koru, RootVar(Av, 1), silk), Trade(root, Koru, RootVar(Co, 1), silk)));

        // the finishes' calls, in order, on that option, var and npcUtil.giveItem's answer
        string Fin(QuestScriptDefinition def, int csid, int option = 0, bool give = true, int var = 0,
            string name = null)
        {
            var h = def.States.SelectMany(s => s.Handlers)
                       .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Id == csid);
            var f = new Fake { Status = QuestStatus.Accepted, Csid = csid, Option = option, GiveResult = give };
            if (name != null) f.CharVars[name] = var;
            h.Run(f);
            return string.Join(";", f.Calls.Where(c => !c.StartsWith("Player.CharVar(")));
        }
        const string Sup = "theThreeMagiSupport";
        Check("windurst g30:The Three Magi's finishes: 260 (options 0 / 3 / 5), 269 (vars 3 / 2 / 5, refused)",
              "Quest.Begin();Player.SetCharVar(theThreeMagiSupport,0,0)|" +
              "Quest.Begin();Player.SetCharVar(theThreeMagiSupport,3,0)|" +
              "Quest.Begin();Player.SetCharVar(theThreeMagiSupport,5,0)|" +
              "Player.GiveItem(17423);Player.AddTitle(189);Trade.Complete();Player.SetNeedToZone(True);" +
              "Player.SetCharVar(theThreeMagiSupport,0,0);Player.AddFame(2,20);" +
              "Player.CompleteQuest(Windurst,THE_THREE_MAGI)|" +
              "Player.GiveItem(17423);Player.AddTitle(188);Trade.Complete();Player.SetNeedToZone(True);" +
              "Player.SetCharVar(theThreeMagiSupport,0,0);Player.AddFame(2,20);" +
              "Player.CompleteQuest(Windurst,THE_THREE_MAGI)|" +
              "Player.GiveItem(17423);Player.AddTitle(187);Trade.Complete();Player.SetNeedToZone(True);" +
              "Player.SetCharVar(theThreeMagiSupport,0,0);Player.AddFame(2,20);" +
              "Player.CompleteQuest(Windurst,THE_THREE_MAGI)|" +
              "Player.GiveItem(17423)",
              string.Join("|", Fin(ttm, 260), Fin(ttm, 260, 3), Fin(ttm, 260, 5), Fin(ttm, 269, 0, true, 3, Sup),
                  Fin(ttm, 269, 0, true, 2, Sup), Fin(ttm, 269, 0, true, 5, Sup), Fin(ttm, 269, 0, false, 3, Sup)));
        Check("windurst g30:Recollections' finishes: 270 (options 0 / 1), 271, 275 given / refused, 10003, 8",
              "Quest.Begin()|Quest.Begin()|Trade.Complete();Player.SetCharVar(recollectionsQuest,2,0)|" +
              "Player.GiveItem(14092);Player.SetCharVar(recollectionsQuest,0,0);Player.DelKeyItem(181);" +
              "Player.AddFame(2,20);Player.CompleteQuest(Windurst,RECOLLECTIONS)|Player.GiveItem(14092)|" +
              "Player.SetCharVar(recollectionsQuest,1,0)|" +
              "Trade.Complete();Player.SetCharVar(recollectionsQuest,3,0);Player.GiveKeyItem(181)",
              string.Join("|", Fin(rec, 270), Fin(rec, 270, 1), Fin(rec, 271), Fin(rec, 275), Fin(rec, 275, 0, false),
                  Fin(rec, 10003), Fin(rec, 8)));
        Check("windurst g30:The Root of the Problem's finishes: 276 (options 0 / 1), 279, 281 given / refused",
              "Quest.Begin();Player.SetCharVar(rootProblem,1,0)|Quest.Begin();Player.SetCharVar(rootProblem,1,0)|" +
              "Player.GiveKeyItem(180)|" +
              "Player.GiveItem(13856);" +
              "Player.CompleteQuest(Windurst,THE_ROOT_OF_THE_PROBLEM);Player.AddFame(2,20);Player.AddTitle(222);" +
              "Player.DelKeyItem(180)|" +
              "Player.GiveItem(13856)",
              string.Join("|", Fin(root, 276), Fin(root, 276, 1), Fin(root, 279), Fin(root, 281),
                  Fin(root, 281, 0, false)));
    }
}
