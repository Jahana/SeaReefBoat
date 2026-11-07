// XI.Verify.Quests -- quest rework B3b Windurst groups 27 and 28 (call-only: To Catch a Falling Star at Sigismund,
// Tohopka and the Twinkle Tree; The Amazin' Scorpio at Soni-Muni and Gottah Maporushanoh; Twinstone Bonding at Gioh
// Ajihri and Wani Casdohry). The golden flows take every arm a char reaches in play, the GM-set states (prog 1 once
// completed, the var on an available char, needToZone while accepted), the boundary hours of the tree's
// VanadielHour() <= 3 (03:00 and 04:00), the fame level's 49 / 50, the full inventory (#full), GIL_RATE (#gil_rate) and
// the rival lines (Lure of the Wildcat at Soni-Muni, Mihgo's Amigo at Wani Casdohry, Lost Chick at Gottah and Gioh).
// These checks add only what no flow can see (fake-only): the replies' kinds. In an NPC call the bridge renders an
// Event, a ProgressEvent and a PriorityEvent alike, as player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply),
// which is what the Lua scripts call; and the Twinkle Tree's NoAction and a null alike (its script ignores the call's
// answer); the fake keeps them apart. And Sigismund's trade:getGil() == 0, which in game getItemCount() == 1
// shadows (the gil slot counts as an item).
// W15Handlers and W15GilApartFake are in WindurstGroup15.cs.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup27()
    {
        Console.WriteLine("== quest rework B3b Windurst groups 27-28: the reply kinds (call-only) ==");
        var star = new QC.Windurst.ToCatchAFallingStar().Compile();
        var scorpio = new QC.Windurst.TheAmazinScorpio().Compile();
        var twinstone = new QC.Windurst.TwinstoneBonding().Compile();
        const string Prog = "QuestCatchAFallingStar_prog";
        const string SpokenTo = "GiohAijhriSpokenTo";

        string Problems(QuestScriptDefinition def) => string.Join(" | ", QuestScriptValidator.Validate(def));
        Check("windurst g27:To Catch a Falling Star clean", "", Problems(star));
        Check("windurst g28:The Amazin' Scorpio clean", "", Problems(scorpio));
        Check("windurst g28:Twinstone Bonding clean", "", Problems(twinstone));

        // a handler's reply on a fresh fake (that status, the char vars, the hour, fame level, needToZone, giveItem's
        // answer, the trade's items), and the calls it made other than its trade check
        (string Reply, string Calls) Run(QuestScriptDefinition def, QuestHandlerKind kind, string npc,
            QuestStatus status, (string Name, int Value)[] vars = null, int hour = 0, int fameLevel = 0,
            bool needToZone = false, bool give = true, params (int Item, int Qty)[] items)
        {
            var f = new Fake { Status = status, Hour = hour, ZoningFlag = needToZone, GiveResult = give };
            f.Fame[QC.FameArea.Windurst] = fameLevel;
            foreach (var (name, value) in vars ?? Array.Empty<(string, int)>()) f.CharVars[name] = value;
            foreach (var (item, qty) in items) f.TradeItems[item] = qty;
            var r = W15Handlers(def, kind).Single(h => h.Entity == npc).Reply(f);
            string calls = string.Join(";", f.Calls.Where(c => !c.StartsWith("Trade.HasQty(")));
            return (r == null ? "null" : QuestContextFake.Describe(r), calls);
        }
        string Reply(QuestScriptDefinition def, QuestHandlerKind kind, string npc, QuestStatus status,
            (string, int)[] vars = null, int hour = 0, int fameLevel = 0, bool needToZone = false,
            params (int Item, int Qty)[] items) =>
            Run(def, kind, npc, status, vars, hour, fameLevel, needToZone, true, items).Reply;

        const QuestHandlerKind T = QuestHandlerKind.OnTrigger, Tr = QuestHandlerKind.OnTrade;
        var tear = (QC.Item.StarfallTear, 1);

        // To Catch a Falling Star
        string Sig(QuestStatus s, int prog) => Reply(star, T, "Sigismund", s, new[] { (Prog, prog) });
        Check("windurst g27:Sigismund's replies by state and prog, as Event",
              "Event(196,0,546)|Event(197,0,546)|Event(200)|Event(200)|null",
              string.Join("|", Sig(QuestStatus.Available, 0), Sig(QuestStatus.Accepted, 0),
                  Sig(QuestStatus.Completed, 2), Sig(QuestStatus.Completed, 1), Sig(QuestStatus.Completed, 0)));
        Check("windurst g27:Sigismund's 200 clears prog in the handler",
              "Player.SetCharVar(QuestCatchAFallingStar_prog,0,0)",
              Run(star, T, "Sigismund", QuestStatus.Completed, new[] { (Prog, 2) }).Calls);
        Check("windurst g27:Sigismund's trade: the tear alone while accepted, as Event, else null",
              "Event(199)|null|null|null|null",
              string.Join("|", Reply(star, Tr, "Sigismund", QuestStatus.Accepted, items: tear),
                  Reply(star, Tr, "Sigismund", QuestStatus.Available, items: tear),
                  Reply(star, Tr, "Sigismund", QuestStatus.Completed, items: tear),
                  Reply(star, Tr, "Sigismund", QuestStatus.Accepted, items: new[] { tear, (QC.Item.BoneChip, 1) }),
                  Reply(star, Tr, "Sigismund", QuestStatus.Accepted, items: new[] { tear, (65535, 10) })));
        // the tear and gil: trade:getGil() > 0, nothing. In game a gil slot counts in trade:getItemCount()
        // (ZoneHost.Trade.cs), so getItemCount() == 1 already refuses it and no flow sees the gil operand (#main's
        // trade with gil=10); a fake whose getItemCount leaves the gil out reads the two apart
        var gilTrade = new W15GilApartFake { Status = QuestStatus.Accepted };
        gilTrade.TradeItems[QC.Item.StarfallTear] = 1;
        gilTrade.TradeItems[65535] = 10;
        var gilReply = W15Handlers(star, Tr).Single(h => h.Entity == "Sigismund").Reply(gilTrade);
        Check("windurst g27:Sigismund's trade, the tear and gil: nothing",
              "null", gilReply == null ? "null" : QuestContextFake.Describe(gilReply));
        Check("windurst g27:Tohopka's replies, as Event",
              "Event(198,0,546,868)|null|null",
              string.Join("|", Reply(star, T, "Tohopka", QuestStatus.Accepted),
                  Reply(star, T, "Tohopka", QuestStatus.Available), Reply(star, T, "Tohopka", QuestStatus.Completed)));
        // the tree's trade at that hour and prog, giveItem's answer and the item traded (the scales by default)
        string Tree(QuestStatus s, int hour, int prog, bool give = true, int item = QC.Item.HandfulOfPugilScales)
        {
            var (reply, calls) =
                Run(star, Tr, "Twinkle_Tree", s, new[] { (Prog, prog) }, hour, 0, false, give, (item, 1));
            return reply + (calls.Length > 0 ? "[" + calls + "]" : "");
        }
        const string Twinkles = "Text.Special(115:FROST_DEPOSIT_TWINKLES);Text.Special(115:MELT_BARE_HANDS);" +
                                "Player.GiveItem(546)";
        const string Given = Twinkles + ";Trade.Confirm();Player.SetCharVar(QuestCatchAFallingStar_prog,1,0)";
        Check("windurst g27:the Twinkle Tree's trade: NoAction after the messages (given or refused), else null",
              $"NoAction()[{Given}]|NoAction()[{Twinkles}]|NoAction()[{Given}]|null|null|null|null|null",
              string.Join("|", Tree(QuestStatus.Accepted, 3, 0), Tree(QuestStatus.Accepted, 0, 0, give: false),
                  Tree(QuestStatus.Accepted, 0, 0), Tree(QuestStatus.Accepted, 4, 0), Tree(QuestStatus.Accepted, 3, 1),
                  Tree(QuestStatus.Available, 3, 0), Tree(QuestStatus.Completed, 3, 0),
                  Tree(QuestStatus.Accepted, 3, 0, item: QC.Item.BoneChip)));

        // The Amazin' Scorpio
        string Soni(QuestStatus s, int level) => Reply(scorpio, T, "Soni-Muni", s, fameLevel: level);
        Check("windurst g28:Soni-Muni's replies by state and fame level (1 / 2), as Event",
              "Event(485)|Event(482,0,0,1017)|Event(481,0,0,1017)|null",
              string.Join("|", Soni(QuestStatus.Completed, 0), Soni(QuestStatus.Accepted, 0),
                  Soni(QuestStatus.Available, 2), Soni(QuestStatus.Available, 1)));
        var stinger = (QC.Item.ScorpionStinger, 1);
        Check("windurst g28:Soni-Muni's trade: the stinger while accepted, as Event, else null",
              "Event(484)|null|null|null",
              string.Join("|", Reply(scorpio, Tr, "Soni-Muni", QuestStatus.Accepted, items: stinger),
                  Reply(scorpio, Tr, "Soni-Muni", QuestStatus.Available, items: stinger),
                  Reply(scorpio, Tr, "Soni-Muni", QuestStatus.Completed, items: stinger),
                  Reply(scorpio, Tr, "Soni-Muni", QuestStatus.Accepted, items: (QC.Item.BlockOfAnimalGlue, 1))));
        Check("windurst g28:Gottah Maporushanoh's replies, as Event",
              "Event(486)|Event(483)|null",
              string.Join("|", Reply(scorpio, T, "Gottah_Maporushanoh", QuestStatus.Completed),
                  Reply(scorpio, T, "Gottah_Maporushanoh", QuestStatus.Accepted),
                  Reply(scorpio, T, "Gottah_Maporushanoh", QuestStatus.Available)));

        // Twinstone Bonding
        string Gioh(QuestStatus s, int level, bool zone) =>
            Reply(twinstone, T, "Gioh_Ajihri", s, null, 0, level, zone);
        Check("windurst g28:Gioh Ajihri's replies by state, needToZone and fame level (1 / 2), as Event",
              "Event(491,0,13360)|Event(488,0,13360)|Event(488,0,13360)|Event(488,0,13360)|Event(487,0,13360)|null",
              string.Join("|", Gioh(QuestStatus.Completed, 0, true), Gioh(QuestStatus.Completed, 0, false),
                  Gioh(QuestStatus.Accepted, 0, false), Gioh(QuestStatus.Accepted, 0, true),
                  Gioh(QuestStatus.Available, 2, false), Gioh(QuestStatus.Available, 1, false)));
        var earring = (QC.Item.TwinstoneEarring, 1);
        string GiohTrade(QuestStatus s, int var, bool zone, params (int, int)[] items) =>
            Reply(twinstone, Tr, "Gioh_Ajihri", s, new[] { (SpokenTo, var) }, 0, 0, zone, items);
        Check("windurst g28:Gioh Ajihri's trade: the var 1, no needToZone, the earring (any status), as Event",
              "Event(490)|Event(490)|Event(490)|null|null|null",
              string.Join("|", GiohTrade(QuestStatus.Accepted, 1, false, earring),
                  GiohTrade(QuestStatus.Available, 1, false, earring),
                  GiohTrade(QuestStatus.Completed, 1, false, earring),
                  GiohTrade(QuestStatus.Completed, 0, false, earring),
                  GiohTrade(QuestStatus.Completed, 1, true, earring),
                  GiohTrade(QuestStatus.Accepted, 1, false, (QC.Item.BoneChip, 1))));
        Check("windurst g28:Wani Casdohry's replies, as Event",
              "Event(492,0,13360)|Event(489,0,13360)|null",
              string.Join("|", Reply(twinstone, T, "Wani_Casdohry", QuestStatus.Completed),
                  Reply(twinstone, T, "Wani_Casdohry", QuestStatus.Accepted),
                  Reply(twinstone, T, "Wani_Casdohry", QuestStatus.Available)));

        // 490's finish while accepted: npcUtil.completeQuest written out, ending in the raw player:completeQuest. No
        // play refuses the bow (the finish confirms the earring first, which frees its slot, and the Wrapped Bow is not
        // rare), so no flow reaches the refusal: this pins that a refused give stops the fame, the title and the
        // completion, as npcUtil.completeQuest returns false there (mutant TB:item-nostop)
        string GiohFinish(bool give)
        {
            var h = twinstone.States.SelectMany(s => s.Handlers)
                .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Id == 490);
            var f = new Fake { Status = QuestStatus.Accepted, Csid = 490, GiveResult = give };
            h.Run(f);
            return string.Join(";", f.Calls);
        }
        Check("windurst g28:Gioh Ajihri's 490 while accepted: the bow, fame, title, completed; a refused bow stops it",
              "Trade.Confirm();Player.SetNeedToZone(True);Player.SetCharVar(GiohAijhriSpokenTo,0,0);" +
              "Player.GiveItem(17154);Player.AddFame(2,16);Player.AddTitle(42);" +
              "Player.CompleteQuest(Windurst,TWINSTONE_BONDING)|" +
              "Trade.Confirm();Player.SetNeedToZone(True);Player.SetCharVar(GiohAijhriSpokenTo,0,0);" +
              "Player.GiveItem(17154)",
              GiohFinish(true) + "|" + GiohFinish(false));
    }
}
