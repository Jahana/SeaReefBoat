// XI.Verify.Quests -- quest rework B3b Windurst groups 24 and 25 (call-only: Chocobilious at Kuoh Rhel, Matata and
// Tapoh Lihzeh; Paying Lip Service at Tapoh Lihzeh; Reap What You Sow at Mashuu-Ajuu).
// The golden flows take every arm a char reaches in play: all three quests' triggers and trades, the rival containers
// (#stew), the mixed-state Tapoh (#mixed, reachable in play: Paying Lip Service first, then Chocobilious), the
// full-inventory finishes and declines (#decline), the fractional GIL_RATE sites (the three #gil_rate flows: 1.0625
// kills the missing-rate and the round-half-even mutants), and the turn-in rolls.
// These checks add only what no flow can see, or only a GM can reach. Each pin and why a flow cannot take it:
//  - the replies' kinds (fake-only): in an NPC call the bridge renders an Event, a ProgressEvent and a PriorityEvent
//    alike, as player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply), which is what the Lua scripts call; the
//    fake keeps them apart; Matata's nulls, Mashuu-Ajuu's roll-2 nulls and the completed var 1 arms (464 / 476) are
//    here too (null and Event kinds the bridge renders alike);
//  - states only a GM can set: Kuoh Rhel's 231 / 232 with the status or var dropped, Tapoh's var 1 / 2 with another
//    status and the var 3 arm, Reap's completed var 2 null; the 479 finish at Available (the trade needs a started
//    quest and nothing changes the status before the finish);
//  - the finishes' fidelity: the first Paying Lip Service completion gives no fame (the reward names no area), the gil
//    follows the option, Reap What You Sow's turn-ins at one free slot (no cheap flow step);
//  - the fake's own spots: the no-slot message at 463 / 479 for options 1 and 4 too (the flows decline with 0 and 4
//    and take with 3), and a GIL_RATE of 2 at Reap's turn-ins (the flow rate is 1.0625).
// W15Handlers and W15Reply are in WindurstGroup15.cs.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup24()
    {
        Console.WriteLine("== quest rework B3b Windurst groups 24 and 25: the reply kinds (call-only) ==");
        var choco = new QC.Windurst.Chocobilious().Compile();
        var lip = new QC.Windurst.PayingLipService().Compile();
        var reap = new QC.Windurst.ReapWhatYouSow().Compile();
        // the raw player:completeQuest of each (the written-out completions: no quest:cleanup)
        const string LipDone = "Player.CompleteQuest(Windurst,PAYING_LIP_SERVICE)";
        const string ReapDone = "Player.CompleteQuest(Windurst,REAP_WHAT_YOU_SOW)";

        Check("windurst g24:Chocobilious clean", "", string.Join(" | ", QuestScriptValidator.Validate(choco)));
        Check("windurst g24:Paying Lip Service clean", "", string.Join(" | ", QuestScriptValidator.Validate(lip)));
        Check("windurst g24:Reap What You Sow clean", "", string.Join(" | ", QuestScriptValidator.Validate(reap)));

        string Describe(QuestReply r) => r == null ? "null" : QuestContextFake.Describe(r);
        QuestHandlerDecl H(QuestScriptDefinition def, QuestHandlerKind kind, string npc) =>
            W15Handlers(def, kind).Single(h => h.Entity == npc);
        string Run(QuestScriptDefinition def, QuestHandlerKind kind, string npc, QuestStatus status, int var,
                   bool needToZone = false, int fame = 2, string varName = null, params (int Item, int Qty)[] items)
        {
            var f = new Fake { Status = status, ZoningFlag = needToZone };
            f.MainNumbers["GIL_RATE"] = 1;
            if (varName != null) f.CharVars[varName] = var;
            f.Fame[QC.FameArea.Windurst] = fame;
            foreach (var (item, qty) in items) f.TradeItems[item] = qty;
            return Describe(H(def, kind, npc).Reply(f));
        }
        string Kuoh(QuestStatus s, int v, bool nz = false, int fame = 2) =>
            Run(choco, QuestHandlerKind.OnTrigger, "Kuoh_Rhel", s, v, nz, fame, "ChocobiliousQuest");
        string Tapoh(QuestStatus s, int v) =>
            Run(choco, QuestHandlerKind.OnTrigger, "Tapoh_Lihzeh", s, v, false, 2, "ChocobiliousQuest");

        string Grass = $"{QC.Item.PapakaGrass}";
        Check("windurst g24:Chocobilious' Kuoh Rhel replies are the Lua's startEvent ids, as Event",
              "Event(224)|null|Event(232)|null|Event(231)|Event(225)|null",
              string.Join("|", Kuoh(QuestStatus.Available, 0), Kuoh(QuestStatus.Available, 0, false, 1),
                  Kuoh(QuestStatus.Completed, 0, true), Kuoh(QuestStatus.Completed, 0), Kuoh(QuestStatus.Accepted, 2),
                  Kuoh(QuestStatus.Accepted, 1), Kuoh(QuestStatus.Completed, 2)));
        Check("windurst g24:Chocobilious' Matata answers 226 once completed, else nothing", "Event(226)|null|null",
              string.Join("|", new[] { QuestStatus.Completed, QuestStatus.Accepted, QuestStatus.Available }.Select(
                  s => Run(choco, QuestHandlerKind.OnTrigger, "Matata", s, 0))));
        Check("windurst g24:Chocobilious' Tapoh replies: var 2 -> 230, var 1 -> 228, else 227; nothing otherwise",
              $"Event(230)|Event(228,0,{Grass})|Event(227,0,{Grass})|null|null",
              string.Join("|", Tapoh(QuestStatus.Accepted, 2), Tapoh(QuestStatus.Accepted, 1),
                  Tapoh(QuestStatus.Accepted, 0), Tapoh(QuestStatus.Completed, 2), Tapoh(QuestStatus.Available, 1)));
        string Grasses(QuestStatus s, int v, int qty = 1) =>
            Run(choco, QuestHandlerKind.OnTrade, "Tapoh_Lihzeh", s, v, false, 2, "ChocobiliousQuest",
                (QC.Item.PapakaGrass, qty));
        Check("windurst g24:Chocobilious' grass trade: accepted at var 1 only, as Event",
              $"Event(229,0,{Grass})|null|null|null|null",
              string.Join("|", Grasses(QuestStatus.Accepted, 1), Grasses(QuestStatus.Accepted, 0),
                  Grasses(QuestStatus.Accepted, 2), Grasses(QuestStatus.Completed, 1),
                  Grasses(QuestStatus.Available, 1)));

        string Fin(QuestScriptDefinition def, int csid, QuestStatus status = QuestStatus.Completed, int option = 0,
                   int freeSlots = 30)
        {
            var h = def.States.SelectMany(s => s.Handlers)
                       .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Id == csid);
            var f = new Fake { Status = status, Csid = csid, Option = option, FreeSlots = freeSlots };
            f.MainNumbers["GIL_RATE"] = 1;
            h.Run(f);
            return string.Join(";", f.Calls);
        }
        Check("windurst g24:Chocobilious: 224 begins on option 1 only; 231 completes and sets needToZone",
              "True|False|True",
              $"{Fin(choco, 224, QuestStatus.Available, 1).Contains("Quest.Begin()")}|" +
              $"{Fin(choco, 224, QuestStatus.Available, 0).Contains("Quest.Begin()")}|" +
              $"{Fin(choco, 231, QuestStatus.Accepted).Contains("Player.SetNeedToZone(True)")}");
        Check("windurst g24:Chocobilious: 227 sets the var to 1, 229 to 2, without a status read",
              "Player.SetCharVar(ChocobiliousQuest,1,0)|Player.SetCharVar(ChocobiliousQuest,2,0)",
              Fin(choco, 227, QuestStatus.Available) + "|" + Fin(choco, 229, QuestStatus.Completed));

        // GM-only: an accepted char with a var above 2 meets neither 230 nor 228, only the first-talk 227 (the vars are
        // 0-2 in play; fake-only, mutants "var >= 2" / "var >= 1")
        Check("windurst g24:Chocobilious' Tapoh: an accepted char with var 3 gets 227, not 230 or 228",
              $"Event(227,0,{Grass})", Tapoh(QuestStatus.Accepted, 3));

        // ---- Paying Lip Service
        string LipTrig(QuestStatus s) => Run(lip, QuestHandlerKind.OnTrigger, "Tapoh_Lihzeh", s, 0);
        string Remi = $"{QC.Item.RemiShell}", Chip = $"{QC.Item.BeehiveChip}";
        Check("windurst g24:Paying Lip Service's trigger: accepted 478, available 477 (GIL_RATE 1: 150, 200), " +
              "else nothing",
              $"Event(478,0,{Chip},{Remi},150,200)|Event(477,0,{Chip},{Remi},150,200)|null",
              string.Join("|", LipTrig(QuestStatus.Accepted), LipTrig(QuestStatus.Available),
                  LipTrig(QuestStatus.Completed)));
        string LipTrade(QuestStatus s, params (int Item, int Qty)[] items) =>
            Run(lip, QuestHandlerKind.OnTrade, "Tapoh_Lihzeh", s, 0, false, 2, null, items);
        Check("windurst g24:Paying Lip Service's trade: three chips or two shells, accepted or completed only",
              $"Event(479,0,{Chip},{Remi},0,0)|Event(479,0,{Chip},{Remi},0,1)|Event(479,0,{Chip},{Remi},0,0)" +
              $"|null|null|Event(479,0,{Chip},{Remi},0,0)",
              string.Join("|", LipTrade(QuestStatus.Accepted, (QC.Item.BeehiveChip, 3)),
                  LipTrade(QuestStatus.Accepted, (QC.Item.RemiShell, 2)),
                  LipTrade(QuestStatus.Completed, (QC.Item.BeehiveChip, 3)),
                  LipTrade(QuestStatus.Available, (QC.Item.BeehiveChip, 3)),
                  LipTrade(QuestStatus.Accepted, (QC.Item.BeehiveChip, 2)),
                  LipTrade(QuestStatus.Accepted, (QC.Item.BeehiveChip, 3), (QC.Item.RemiShell, 2))));
        Check("windurst g24:Paying Lip Service: 477 begins on option 1 only", "True|False",
              $"{Fin(lip, 477, QuestStatus.Available, 1).Contains("Quest.Begin()")}|" +
              $"{Fin(lip, 477, QuestStatus.Available, 0).Contains("Quest.Begin()")}");
        Check("windurst g24:Paying Lip Service: first 479 completes (no fame call), a repeat: fame 10, no completion",
              "True|False|False|True",
              $"{Fin(lip, 479, QuestStatus.Accepted).Contains(LipDone)}|" +
              $"{Fin(lip, 479, QuestStatus.Accepted).Contains("AddFame")}|" +
              $"{Fin(lip, 479, QuestStatus.Completed).Contains(LipDone)}|" +
              $"{Fin(lip, 479, QuestStatus.Completed).Contains("Player.AddFame(" + QC.FameArea.Windurst + ",10)")}");
        Check("windurst g24:Paying Lip Service: the gil follows the option (1: 200, else 150), the trade is confirmed",
              "True,False|False,True|True",
              $"{Fin(lip, 479, QuestStatus.Completed, 1).Contains("GiveCurrency(gil,200)")}," +
              $"{Fin(lip, 479, QuestStatus.Completed, 1).Contains("GiveCurrency(gil,150)")}|" +
              $"{Fin(lip, 479, QuestStatus.Completed, 0).Contains("GiveCurrency(gil,200)")}," +
              $"{Fin(lip, 479, QuestStatus.Completed, 0).Contains("GiveCurrency(gil,150)")}|" +
              $"{Fin(lip, 479, QuestStatus.Completed, 0).Contains("Trade.Confirm()")}");

        Check("windurst g24:Paying Lip Service: 479 completes only an accepted quest (a GM-set available one does not)",
              "False", Fin(lip, 479, QuestStatus.Available).Contains(LipDone).ToString());

        // ---- Reap What You Sow
        string Seeds = $"{QC.Item.BagOfHerbSeeds}", Fungus = $"{QC.Item.SobbingFungus}";
        string ReapTrig(QuestStatus s, int roll, int var = 0, bool nz = false)
        {
            var f = new Fake { Status = s, ZoningFlag = nz };
            f.CharVars["QuestReapSow_var"] = var;
            f.Rolls.Enqueue(roll);
            return Describe(H(reap, QuestHandlerKind.OnTrigger, "Mashuu-Ajuu").Reply(f));
        }
        Check("windurst g24:Reap What You Sow's trigger by state and roll, as Event",
              $"Event(463,0,{Fungus},{Seeds})|null|Event(464,0,{Fungus},{Seeds})|Event(476)|Event(478)|Event(478)" +
              $"|Event(479,0,{Fungus},{Seeds})|null|Event(464,0,{Fungus},{Seeds})|Event(476)",
              string.Join("|", ReapTrig(QuestStatus.Available, 1), ReapTrig(QuestStatus.Available, 2),
                  ReapTrig(QuestStatus.Accepted, 1), ReapTrig(QuestStatus.Accepted, 2),
                  ReapTrig(QuestStatus.Completed, 1, 0, true), ReapTrig(QuestStatus.Completed, 2, 1, true),
                  ReapTrig(QuestStatus.Completed, 1, 0), ReapTrig(QuestStatus.Completed, 2, 0),
                  ReapTrig(QuestStatus.Completed, 1, 1), ReapTrig(QuestStatus.Completed, 2, 1)));
        Check("windurst g24:Reap What You Sow's completed char with another var plays nothing", "null",
              ReapTrig(QuestStatus.Completed, 1, 2));
        string ReapTrade(QuestStatus s, params (int Item, int Qty)[] items) =>
            Run(reap, QuestHandlerKind.OnTrade, "Mashuu-Ajuu", s, 0, false, 2, null, items);
        Check("windurst g24:Reap What You Sow's trade: one fungus (475, 500) or Deathball (477, 700), not available",
              $"Event(475,500,{QC.Item.StationerySet})|Event(477,700)|" +
              $"Event(475,500,{QC.Item.StationerySet})|null|null|null",
              string.Join("|", ReapTrade(QuestStatus.Accepted, (QC.Item.SobbingFungus, 1)),
                  ReapTrade(QuestStatus.Accepted, (QC.Item.Deathball, 1)),
                  ReapTrade(QuestStatus.Completed, (QC.Item.SobbingFungus, 1)),
                  ReapTrade(QuestStatus.Available, (QC.Item.SobbingFungus, 1)),
                  ReapTrade(QuestStatus.Accepted, (QC.Item.SobbingFungus, 2)),
                  ReapTrade(QuestStatus.Accepted, (QC.Item.SobbingFungus, 1), (QC.Item.Deathball, 1))));
        Check("windurst g24:Reap What You Sow: 463 / 479 option 3 begin / set var 1 with one free slot, not with none",
              "True,True|False,False",
              $"{Fin(reap, 463, QuestStatus.Available, 3, 1).Contains("Quest.Begin()")}," +
              $"{Fin(reap, 479, QuestStatus.Completed, 3, 1).Contains("SetCharVar(QuestReapSow_var,1")}|" +
              $"{Fin(reap, 463, QuestStatus.Available, 3, 0).Contains("Quest.Begin()")}," +
              $"{Fin(reap, 479, QuestStatus.Completed, 3, 0).Contains("SetCharVar")}");
        Check("windurst g24:Reap What You Sow: 475 / 477 accepted need a free slot; completed do not",
              "True,True|False,False|True,True",
              $"{Fin(reap, 475, QuestStatus.Accepted, 0, 1).Contains(ReapDone)}," +
              $"{Fin(reap, 477, QuestStatus.Accepted, 0, 1).Contains(ReapDone)}|" +
              $"{Fin(reap, 475, QuestStatus.Accepted, 0, 0).Contains(ReapDone)}," +
              $"{Fin(reap, 477, QuestStatus.Accepted, 0, 0).Contains(ReapDone)}|" +
              $"{Fin(reap, 475, QuestStatus.Completed, 0, 0).Contains("SetCharVar(QuestReapSow_var,0")}," +
              $"{Fin(reap, 477, QuestStatus.Completed, 0, 0).Contains("SetCharVar(QuestReapSow_var,0")}");
        string Rate2(int csid)
        {
            var h = reap.States.SelectMany(x => x.Handlers)
                .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Id == csid);
            var f = new Fake { Status = QuestStatus.Completed, Csid = csid };
            f.MainNumbers["GIL_RATE"] = 2;
            h.Run(f);
            return string.Join(";", f.Calls);
        }
        Check("windurst g24:Reap What You Sow: the turn-in gil is multiplied by GIL_RATE (2: 1000 and 1400)",
              "True,True",
              $"{Rate2(475).Contains("Player.AddGil(1000)")},{Rate2(477).Contains("Player.AddGil(1400)")}");
        // a decline (option 0 or 4: in the #decline flow) or option 1 at a full inventory prints nothing; only option 3
        // is the offer's take
        Check("windurst g24:Reap What You Sow: 463 / 479 at no free slot print the message on option 3 only",
              "True,False,False|True,False,False",
              string.Join("|", new[] { 463, 479 }.Select(c => string.Join(",", new[] { 3, 1, 4 }.Select(o =>
                  Fin(reap, c, QuestStatus.Available, o, 0).Contains("ITEM_CANNOT_BE_OBTAINED").ToString())))));
        Check("windurst g24:Reap What You Sow: 475 pays 500 and 477 700 gil (x GIL_RATE) and set needToZone",
              "True,True|True,True",
              $"{Fin(reap, 475).Contains("Player.AddGil(500)")},{Fin(reap, 477).Contains("Player.AddGil(700)")}|" +
              $"{Fin(reap, 475).Contains("SetNeedToZone(True)")},{Fin(reap, 477).Contains("SetNeedToZone(True)")}");
    }
}
