// XI.Verify.Quests -- quest rework B3b Windurst group 23 (call-only: The Postman Always KO's Twice at Ambrosius,
// Orudoba-Sondeba and Tsuaora-Tsuora; To Bee or Not to Bee? at Zayhi-Bauhi, Raamimi, Kalupa-Tawalupa and
// Rutango-Botango). The golden flows take every arm a char reaches in play (including Tsuaora-Tsuora completed, the
// honey and mixed trades, needToZone with the quest available / accepted, and 80's needToZone via a rezone). These
// checks add only what no flow can see, or only a GM can reach:
//  - the replies' kinds (fake-only): in an NPC call the bridge renders an Event, a ProgressEvent and a PriorityEvent
//    alike, as player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply: no action is built, so no priority), which
//    is what the Lua scripts call; the fake keeps them apart. Includes the honey trade's 69 / 70 / 73 / 74 / 75;
//  - states only a GM can set (fake-only): Zayhi's 64 and the honey trade not on an available / completed char with
//    var 10 / 1-4, Raamimi's 80 / 79 with the status or var dropped, Kalupa-Tawalupa's and Rutango-Botango's nulls;
//  - the finishes 57-60, which also complete a completed quest in the mutant: equivalent in game (no packet or row),
//    pinned for fidelity to the Lua's call list; 67 with exactly one free slot (no cheap flow step).
// W15Handlers and W15Reply are in WindurstGroup15.cs.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup23()
    {
        Console.WriteLine("== quest rework B3b Windurst group 23: the reply kinds (call-only) ==");
        var postman = new QC.Windurst.ThePostmanAlwaysKosTwice().Compile();
        var toBee = new QC.Windurst.ToBeeOrNotToBee().Compile();

        Check("windurst g23:The Postman Always KO's Twice clean", "",
              string.Join(" | ", QuestScriptValidator.Validate(postman)));
        Check("windurst g23:To Bee or Not to Bee? clean", "", string.Join(" | ", QuestScriptValidator.Validate(toBee)));

        string Triggers(QuestScriptDefinition def, QuestStatus status) =>
            string.Join(",", W15Handlers(def, QuestHandlerKind.OnTrigger).Select(h =>
                $"{h.Entity}:{W15Reply(h, status, 1).Reply}"));

        // the trigger replies in declaration order (Ambrosius' by status; the other NPCs' accepted 51 / 50)
        Check("windurst g23:The Postman Always KO's Twice's trigger replies are the Lua's startEvent ids, as Event",
              "Ambrosius:Event(48)|Ambrosius:Event(49),Orudoba-Sondeba:Event(51),Tsuaora-Tsuora:Event(50)" +
              "|Ambrosius:Event(56)",
              Triggers(postman, QuestStatus.Available).Split(',')[0] + "|" + Triggers(postman, QuestStatus.Accepted) +
              "|" + Triggers(postman, QuestStatus.Completed).Split(',')[0]);

        // the trades, GIL_RATE 1: one to four letters (accepted 52-55, completed 57-60)
        string Letters(QuestStatus status, int n) =>
            W15Reply(W15Handlers(postman, QuestHandlerKind.OnTrade).Single(), status, 1,
                new[] { QC.Item.TornEpistle, QC.Item.MuddyBarTab, QC.Item.OddPostcard, QC.Item.DampEnvelope }
                    .Take(n).Select(i => (i, 1)).ToArray()).Reply;
        Check("windurst g23:The Postman Always KO's Twice's trades: one to four letters, accepted then completed",
              "Event(52,50)|Event(53,150,2)|Event(54,250,3)|Event(55,500,4)" +
              "|Event(57,50)|Event(58,150,2)|Event(59,250,3)|Event(60,500,4)",
              string.Join("|", Enumerable.Range(1, 4).Select(n => Letters(QuestStatus.Accepted, n))) + "|" +
              string.Join("|", Enumerable.Range(1, 4).Select(n => Letters(QuestStatus.Completed, n))));

        // To Bee: each NPC's trigger by state; the var 10 / 1 / 5 read on a fresh fake
        string Bee(QuestStatus status, int var, bool needToZone = false)
        {
            var parts = W15Handlers(toBee, QuestHandlerKind.OnTrigger).Select(h =>
            {
                var f = new Fake { Status = status };
                f.CharVars["ToBeeOrNot_var"] = var;
                f.ZoningFlag = needToZone;
                f.Fame[QC.FameArea.Windurst] = 2;
                f.QuestStatuses["Windurst/THE_POSTMAN_ALWAYS_KOS_TWICE"] = QuestStatus.Completed;
                var r = h.Fixed ?? h.Reply(f);
                return $"{h.Entity}:{(r == null ? "null" : QuestContextFake.Describe(r))}";
            });
            return string.Join(",", parts);
        }
        Check("windurst g23:To Bee or Not to Bee?'s trigger replies are the Lua's startEvent ids, as Event",
              "Zayhi-Bauhi:Event(64),Raamimi:Event(67),Kalupa-Tawalupa:Event(66),Rutango-Botango:Event(65)" +
              "|Zayhi-Bauhi:Event(69),Raamimi:Event(68),Kalupa-Tawalupa:Event(72),Rutango-Botango:Event(71)" +
              "|Zayhi-Bauhi:Event(78),Raamimi:Event(79),Kalupa-Tawalupa:Event(77),Rutango-Botango:Event(76)" +
              "|Zayhi-Bauhi:null,Raamimi:Event(80),Kalupa-Tawalupa:null,Rutango-Botango:null",
              Bee(QuestStatus.Available, 10) + "|" + Bee(QuestStatus.Accepted, 1) + "|" +
              Bee(QuestStatus.Completed, 0, true) + "|" + Bee(QuestStatus.Completed, 5));

        string Reply1(QuestScriptDefinition def, string npc, QuestStatus status) =>
            W15Reply(Trig(def, npc), status, 1).Reply;
        // what the flows cannot reach cheaply: the nulls (the NPC's own script plays) and the finishes' calls
        QuestHandlerDecl Trig(QuestScriptDefinition def, string npc) =>
            W15Handlers(def, QuestHandlerKind.OnTrigger).Single(h => h.Entity == npc);
        string TrigOf(QuestScriptDefinition def, string npc, QuestStatus status, int var, bool needToZone)
        {
            var f = new Fake { Status = status, ZoningFlag = needToZone };
            f.CharVars["ToBeeOrNot_var"] = var;
            f.Fame[QC.FameArea.Windurst] = 2;
            f.QuestStatuses["Windurst/THE_POSTMAN_ALWAYS_KOS_TWICE"] = QuestStatus.Completed;
            var r = Trig(def, npc).Reply(f);
            return r == null ? "null" : QuestContextFake.Describe(r);
        }
        Check("windurst g23:Orudoba-Sondeba and Tsuaora-Tsuora answer only an accepted char",
              "null,null|null,null|Event(51),Event(50)",
              string.Join("|", new[] { QuestStatus.Available, QuestStatus.Completed, QuestStatus.Accepted }.Select(
                  st => Reply1(postman, "Orudoba-Sondeba", st) + "," + Reply1(postman, "Tsuaora-Tsuora", st))));
        Check("windurst g23:To Bee: Zayhi's 64 needs an accepted quest with var 10; 78 a completed one",
              "null|null|Event(64)|Event(78)",
              string.Join("|", TrigOf(toBee, "Zayhi-Bauhi", QuestStatus.Completed, 10, false),
                  TrigOf(toBee, "Zayhi-Bauhi", QuestStatus.Completed, 3, false),
                  TrigOf(toBee, "Zayhi-Bauhi", QuestStatus.Accepted, 10, false),
                  TrigOf(toBee, "Zayhi-Bauhi", QuestStatus.Completed, 0, true)));
        Check("windurst g23:To Bee: Raamimi's 80 needs completed and var 5, his 79 completed and var 0",
              "null|null|null|Event(80)|Event(79)",
              string.Join("|", TrigOf(toBee, "Raamimi", QuestStatus.Available, 5, false),
                  TrigOf(toBee, "Raamimi", QuestStatus.Completed, 3, true),
                  TrigOf(toBee, "Raamimi", QuestStatus.Available, 0, true),
                  TrigOf(toBee, "Raamimi", QuestStatus.Completed, 5, true),
                  TrigOf(toBee, "Raamimi", QuestStatus.Completed, 0, true)));
        Check("windurst g23:To Bee: Kalupa-Tawalupa's and Rutango-Botango's 77 / 76 need a completed quest",
              "null,null|null,null|Event(77),Event(76)",
              string.Join("|", new[] { QuestStatus.Available, QuestStatus.Accepted, QuestStatus.Completed }.Select(
                  st => TrigOf(toBee, "Kalupa-Tawalupa", st, 0, true) + "," +
                        TrigOf(toBee, "Rutango-Botango", st, 0, true))));

        // Zayhi-Bauhi's trade: accepted, one Pot of Honey and nothing else
        string HoneyTrade(QuestStatus status, params (int Item, int Qty)[] items)
        {
            var f = new Fake { Status = status };
            f.CharVars["ToBeeOrNot_var"] = 10;
            foreach (var (item, qty) in items) f.TradeItems[item] = qty;
            var r = W15Handlers(toBee, QuestHandlerKind.OnTrade).Single(h => h.Entity == "Zayhi-Bauhi").Reply(f);
            return r == null ? "null" : QuestContextFake.Describe(r);
        }
        Check("windurst g23:To Bee: Zayhi takes one Pot of Honey and nothing else, from an accepted char only",
              "Event(69)|null|null|null|null|null|null",
              string.Join("|", HoneyTrade(QuestStatus.Accepted, (QC.Item.PotOfHoney, 1)),
                  HoneyTrade(QuestStatus.Available, (QC.Item.PotOfHoney, 1)),
                  HoneyTrade(QuestStatus.Completed, (QC.Item.PotOfHoney, 1)),
                  HoneyTrade(QuestStatus.Accepted, (QC.Item.PotOfHoney, 1), (QC.Item.TornEpistle, 1)),
                  HoneyTrade(QuestStatus.Accepted, (QC.Item.TornEpistle, 1)),
                  HoneyTrade(QuestStatus.Accepted, (QC.Item.PotOfHoney, 2)),
                  HoneyTrade(QuestStatus.Accepted)));

        // the honey trade's reply for each var: 10 / 1 / 2 / 3 / 4 -> 69 / 70 / 73 / 74 / 75 (the kinds)
        string HoneyAtVar(int var)
        {
            var f = new Fake { Status = QuestStatus.Accepted };
            f.CharVars["ToBeeOrNot_var"] = var;
            f.TradeItems[QC.Item.PotOfHoney] = 1;
            var r = W15Handlers(toBee, QuestHandlerKind.OnTrade).Single(h => h.Entity == "Zayhi-Bauhi").Reply(f);
            return r == null ? "null" : QuestContextFake.Describe(r);
        }
        Check("windurst g23:To Bee: Zayhi's honey trade at var 1 / 2 / 3 / 4 answers 70 / 73 / 74 / 75, as Event",
              "Event(70)|Event(73)|Event(74)|Event(75)", string.Join("|", Enumerable.Range(1, 4).Select(HoneyAtVar)));

        // the finishes' calls
        string Fin(QuestScriptDefinition def, int csid, int freeSlots = 30)
        {
            var h = def.States.SelectMany(s => s.Handlers)
                       .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Id == csid);
            var f = new Fake { Status = QuestStatus.Completed, Csid = csid, FreeSlots = freeSlots };
            f.MainNumbers["GIL_RATE"] = 1;
            h.Run(f);
            return string.Join(";", f.Calls);
        }
        // the raw player:completeQuest (the written-out completion: no quest:cleanup)
        const string PostmanDone = "Player.CompleteQuest(Windurst,THE_POSTMAN_ALWAYS_KOS_TWICE)";
        Check("windurst g23:The Postman: 52-55 complete the quest, 57-60 do not",
              "True,True,True,True|False,False,False,False",
              string.Join(",", new[] { 52, 53, 54, 55 }.Select(c => Fin(postman, c).Contains(PostmanDone))) +
              "|" +
              string.Join(",", new[] { 57, 58, 59, 60 }.Select(c => Fin(postman, c).Contains(PostmanDone))));
        Check("windurst g23:To Bee: 67 gives the honey with one free slot, not with none", "True|False",
              $"{Fin(toBee, 67, 1).Contains("Player.AddItem(")}|{Fin(toBee, 67, 0).Contains("Player.AddItem(")}");
        Check("windurst g23:To Bee: 80 sets needToZone", "True", Fin(toBee, 80).Contains("Player.SetNeedToZone(True)")
            .ToString());
    }
}
