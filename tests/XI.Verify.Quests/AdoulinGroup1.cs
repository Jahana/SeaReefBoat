// XI.Verify.Quests -- quest rework B3b Adoulin group 1 (the fame cycle: It Sets My Heart Aflutter, A Good Pair of
// Crocs, A Shot in the Dark; Hunger Strikes, The Starving): each definition validates clean and declares no steps.
// Their branches are the flows' (tests/golden/flows/adoulin.*.flow), except what no trace can see:
// - The reply kinds at Saldinor, Felmsy and Pudith. Each has one container and no script, and Saldinor's default 329
//   ranks Default, below any Event: a Progress beats an Event and equal kinds rotate, so the first offer's kind (330 /
//   3001 / 3011), the reminder's (333 / 3004 / 3014) and the turn-in's (331 / 3002 / 3012) show against no rival. The
//   repeat's (332 / 3003 / 3013) and the completed event's (329 / 3000 / 3010) show in the flows' second repeat talk
//   (equal kinds would rotate to the completed event), Westerly Breeze's 2530 / 3005 in the #kinds flows (an Event
//   would give his script its turn). All are pinned here, each the Lua's kind.
// - It Sets My Heart Aflutter's completed 329 is also Saldinor's default 329: no trace tells them apart.
// - The turn-in finishes with quest:complete false (no item reward: never false in play): no confirm, no tracker.
// - Hunger Strikes' and The Starving's accepted sections, registered under Western Adoulin (user ruling 2026-11-04,
//   kept Lua bugs: the Lua's have no zone key and never registered): their reply kinds (2531, 2533, 3006 and 3008
//   events, 2532 and 3007 progressEvents) and their finishes, with quest:complete true and false.
// - Each quest's LegacyPath, its own Lua file (its slot in the containers list).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunAdoulinGroup1()
    {
        Console.WriteLine("== quest rework B3b Adoulin group 1 ==");
        var quests = new QuestScript[] { new QC.Adoulin.ItSetsMyHeartAflutter(), new QC.Adoulin.AGoodPairOfCrocs(),
            new QC.Adoulin.AShotInTheDark(), new QC.Adoulin.HungerStrikes(), new QC.Adoulin.TheStarving() };
        foreach (var quest in quests)
        {
            string name = quest.GetType().Name;
            Check($"adoulin g1:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(quest.Compile())));
            Check($"adoulin g1:{name} declares no steps", "", string.Join(",", quest.Compile().DeclaredSteps));
        }

        // the statuses each state matches, state by state (the cycle's repeat looks at no status; Hunger Strikes' and
        // The Starving's accepted states: user ruling 2026-11-04, kept Lua bugs)
        string States(QuestScript quest) => string.Join(" ", quest.Compile().States.Select(s => string.Join("/",
            s.Matches.Select(m => m.AnyStatus ? "any" : m.Status.ToString()))));
        Check("adoulin g1:the states' statuses",
              "Available any Accepted Completed | Available any Accepted Completed | " +
              "Available any Accepted Completed | Available Accepted | Available Accepted",
              string.Join(" | ", quests.Select(States)));

        Check("adoulin g1:each quest's LegacyPath is its own Lua file",
              "It_Sets_My_Heart_Aflutter ; A_Good_Pair_of_Crocs ; A_Shot_in_the_Dark ; Hunger_Strikes ; The_Starving",
              string.Join(" ; ", quests.Select(q => q.LegacyPath.Substring("scripts/quests/adoulin/".Length))));

        // the reward tables as the Lua's (Hunger Strikes' and The Starving's have no fame field: npcUtil.completeQuest
        // defaults it to 30); fame / fameArea / bayld / exp / gil / item
        string Rw(QuestScript q)
        {
            var r = q.Compile().Reward;
            return $"{r.Fame?.ToString() ?? "-"}/{r.FameArea}/{r.Bayld}/{r.Exp}/{r.Gil?.ToString() ?? "-"}/" +
                   $"{r.Item?.ToString() ?? "-"}";
        }
        int ad = QC.FameArea.Adoulin;
        Check("adoulin g1:the rewards",
              $"6/{ad}/200/500/-/- | 6/{ad}/200/500/-/- | 6/{ad}/200/500/-/- | " +
              $"-/{ad}/500/1000/-/- | -/{ad}/500/1000/-/-",
              string.Join(" | ", quests.Select(Rw)));

        RunAdoulinGroup1Kinds();
        RunAdoulinGroup1Finishes();
    }

    static Fake AD1Fake(QuestStatus st, int tracker = 0, bool cycleDone = false)
    {
        var f = new Fake { Status = st, UniqueDay = 8000 };
        f.CharVars["ADOULIN_FAME_QUEST_TRACKER"] = tracker;
        if (cycleDone)
            foreach (var c in new[] { "IT_SETS_MY_HEART_AFLUTTER", "A_GOOD_PAIR_OF_CROCS", "A_SHOT_IN_THE_DARK" })
                f.CompletedQuests.Add($"{QuestLog.Adoulin}/{c}");
        return f;
    }

    // the repeat's trigger (the state that looks at no status), on a fake where its check holds
    static string AD1Repeat(QuestScriptDefinition d, ushort zone, string npc, Fake f)
    {
        var st = d.States.Single(s => s.Matches.Any(m => m.AnyStatus));
        if (!st.Matches.Any(m => m.When == null || m.When(f))) return "check false";
        return AU1Describe(st.Handlers.Single(h => h.Kind == QuestHandlerKind.OnTrigger && h.Zone == zone &&
                                                   h.Entity == npc), f);
    }

    // every reply of the five, each as the fake state that offers it gives it (the repeat: its own state's, on a
    // completed char, all three done, the tracker at the quest's number)
    static void RunAdoulinGroup1Kinds()
    {
        var lines = new System.Collections.Generic.List<string>();
        void L(string key, string v) => lines.Add(key + "=" + v);
        const QuestHandlerKind Tr = QuestHandlerKind.OnTrigger, Td = QuestHandlerKind.OnTrade;
        ushort rala = QC.Zone.RalaWaterways, east = QC.Zone.EasternAdoulin, west = QC.Zone.WesternAdoulin;
        string Sal = QC.Npc.RalaWaterways.Saldinor, Fel = QC.Npc.EasternAdoulin.Felmsy,
               Pud = QC.Npc.EasternAdoulin.Pudith, Wes = QC.Npc.WesternAdoulin.WesterlyBreeze;
        var al = new QC.Adoulin.ItSetsMyHeartAflutter().Compile();
        var cr = new QC.Adoulin.AGoodPairOfCrocs().Compile();
        var sd = new QC.Adoulin.AShotInTheDark().Compile();
        var hs = new QC.Adoulin.HungerStrikes().Compile();
        var ts = new QC.Adoulin.TheStarving().Compile();
        Fake Badge() { var f = AD1Fake(QuestStatus.Available); f.KeyItems[QC.KeyItem.PioneersBadge] = true; return f; }
        Fake Items(int item, int qty) { var f = AD1Fake(QuestStatus.Accepted); f.TradeItems[item] = qty; return f; }

        L("al offer", AU3Npc(al, Tr, rala, Sal, Badge()));
        L("al repeat", AD1Repeat(al, rala, Sal, AD1Fake(QuestStatus.Completed, 0, true)));
        L("al accepted", AU3Npc(al, Tr, rala, Sal, AD1Fake(QuestStatus.Accepted)));
        L("al trade", AU3Npc(al, Td, rala, Sal, Items(QC.Item.TwitherymWing, 2)));
        L("al completed", AU3Npc(al, Tr, rala, Sal, AD1Fake(QuestStatus.Completed)));
        L("cr offer", AU3Npc(cr, Tr, east, Fel, AD1Fake(QuestStatus.Available)));
        L("cr repeat", AD1Repeat(cr, east, Fel, AD1Fake(QuestStatus.Completed, 1, true)));
        L("cr accepted", AU3Npc(cr, Tr, east, Fel, AD1Fake(QuestStatus.Accepted)));
        L("cr trade necklace", AU3Npc(cr, Td, east, Fel, Items(QC.Item.VelkkNecklace, 1)));
        L("cr trade mask", AU3Npc(cr, Td, east, Fel, Items(QC.Item.VelkkMask, 1)));
        L("cr completed", AU3Npc(cr, Tr, east, Fel, AD1Fake(QuestStatus.Completed)));
        L("sd offer", AU3Npc(sd, Tr, east, Pud, AD1Fake(QuestStatus.Available)));
        L("sd repeat", AD1Repeat(sd, east, Pud, AD1Fake(QuestStatus.Completed, 2, true)));
        L("sd accepted", AU3Npc(sd, Tr, east, Pud, AD1Fake(QuestStatus.Accepted)));
        L("sd trade", AU3Npc(sd, Td, east, Pud, Items(QC.Item.UmbrilOoze, 1)));
        L("sd completed", AU3Npc(sd, Tr, east, Pud, AD1Fake(QuestStatus.Completed)));
        L("hs offer", AU3Npc(hs, Tr, west, Wes, AD1Fake(QuestStatus.Available)));
        L("ts offer", AU3Npc(ts, Tr, west, Wes, AD1Fake(QuestStatus.Available)));
        // Hunger Strikes' and The Starving's accepted sections (user ruling 2026-11-04, kept Lua bugs); the AH
        // categories: 52 MEAT_EGGS, 57 SWEETS, 58 DRINKS
        Fake Ah(int item, int category)
        {
            var f = Items(item, 1);
            f.TradeSlots.Add(item);
            f.AhCategories[item] = category;
            return f;
        }
        L("hs accepted", AU3Npc(hs, Tr, west, Wes, AD1Fake(QuestStatus.Accepted)));
        L("hs trade soup", AU3Npc(hs, Td, west, Wes, Items(QC.Item.BowlOfWisdomSoup, 1)));
        L("hs trade meat", AU3Npc(hs, Td, west, Wes, Ah(QC.Item.StripOfMeatJerky, 52)));
        L("hs trade sweet", AU3Npc(hs, Td, west, Wes, Ah(QC.Item.StripOfMeatJerky, 57)));
        L("hs trade drink", AU3Npc(hs, Td, west, Wes, Ah(QC.Item.BottleOfOrangeJuice, 58)));
        var two = Ah(QC.Item.StripOfMeatJerky, 52);
        two.TradeItems[QC.Item.StripOfMeatJerky] = 2;
        L("hs trade two", AU3Npc(hs, Td, west, Wes, two));
        L("ts accepted", AU3Npc(ts, Tr, west, Wes, AD1Fake(QuestStatus.Accepted)));
        L("ts trade goblin drink", AU3Npc(ts, Td, west, Wes, Items(QC.Item.BottleOfGoblinDrink, 1)));
        L("ts trade drink", AU3Npc(ts, Td, west, Wes, Ah(QC.Item.BottleOfOrangeJuice, 58)));
        L("ts trade food", AU3Npc(ts, Td, west, Wes, Ah(QC.Item.StripOfMeatJerky, 52)));
        L("ts trade two", AU3Npc(ts, Td, west, Wes, Items(QC.Item.BottleOfGoblinDrink, 2)));
        Check("adoulin g1:the reply kinds",
              "al offer=ProgressEvent(330) ; al repeat=ProgressEvent(332) ; al accepted=Event(333) ; " +
              "al trade=ProgressEvent(331) ; al completed=Event(329) ; cr offer=ProgressEvent(3001) ; " +
              "cr repeat=ProgressEvent(3003) ; cr accepted=Event(3004) ; cr trade necklace=ProgressEvent(3002) ; " +
              "cr trade mask=ProgressEvent(3002) ; cr completed=Event(3000) ; sd offer=ProgressEvent(3011) ; " +
              "sd repeat=ProgressEvent(3013) ; sd accepted=Event(3014) ; sd trade=ProgressEvent(3012) ; " +
              "sd completed=Event(3010) ; hs offer=ProgressEvent(2530) ; ts offer=ProgressEvent(3005) ; " +
              "hs accepted=Event(2531) ; hs trade soup=ProgressEvent(2532) ; hs trade meat=Event(2533) ; " +
              "hs trade sweet=Event(2533) ; hs trade drink=null ; hs trade two=null ; ts accepted=Event(3006) ; " +
              "ts trade goblin drink=ProgressEvent(3007) ; ts trade drink=Event(3008) ; ts trade food=Event(3006) ; " +
              "ts trade two=Event(3006)",
              string.Join(" ; ", lines));
    }

    // the turn-in finishes, with quest:complete true and false (false never happens in play: no item reward)
    static void RunAdoulinGroup1Finishes()
    {
        string Fin(QuestScript quest, int csid, bool complete)
        {
            var d = quest.Compile();
            var fin = d.States.SelectMany(s => s.Handlers)
                .Single(h => h.Kind == QuestHandlerKind.OnEventFinish && h.Id == csid);
            var f = AD1Fake(QuestStatus.Accepted);
            f.CompleteResult = complete;
            fin.Run(f);
            return $"{fin.Zone}/{csid}:{string.Join(";", f.Calls)}";
        }
        ushort rala = QC.Zone.RalaWaterways, east = QC.Zone.EasternAdoulin, west = QC.Zone.WesternAdoulin;
        // Hunger Strikes' and The Starving's (user ruling 2026-11-04, kept Lua bugs: 3007 now confirms the trade)
        Check("adoulin g1:Hunger Strikes' and The Starving's finishes, completed / not (user ruling 2026-11-04)",
              $"{west}/2532:Quest.Complete();Trade.Confirm();Player.SetQuestVar(Adoulin,THE_STARVING,Timer,8001) | " +
              $"{west}/2532:Quest.Complete() | {west}/2533:Trade.Confirm() | {west}/2533:Trade.Confirm() | " +
              $"{west}/3007:Quest.Complete();Trade.Confirm();" +
              "Player.SetQuestMustZone(Adoulin,ALWAYS_MORE_QUOTH_THE_RAVENOUS);" +
              "Player.SetQuestVar(Adoulin,ALWAYS_MORE_QUOTH_THE_RAVENOUS,Timer,8001) | " +
              $"{west}/3007:Quest.Complete() | {west}/3008:Trade.Confirm() | {west}/3008:Trade.Confirm()",
              string.Join(" | ", new (QuestScript Q, int Csid)[] { (new QC.Adoulin.HungerStrikes(), 2532),
                  (new QC.Adoulin.HungerStrikes(), 2533), (new QC.Adoulin.TheStarving(), 3007),
                  (new QC.Adoulin.TheStarving(), 3008) }
                  .SelectMany(x => new[] { Fin(x.Q, x.Csid, true), Fin(x.Q, x.Csid, false) })));
        Check("adoulin g1:the turn-in finishes, completed / not",
              $"{rala}/331:Quest.Complete();Trade.Confirm();Player.SetCharVar(ADOULIN_FAME_QUEST_TRACKER,1,0) | " +
              $"{rala}/331:Quest.Complete() | " +
              $"{east}/3002:Quest.Complete();Trade.Confirm();Player.SetCharVar(ADOULIN_FAME_QUEST_TRACKER,2,0) | " +
              $"{east}/3002:Quest.Complete() | " +
              $"{east}/3012:Quest.Complete();Trade.Confirm();Player.SetCharVar(ADOULIN_FAME_QUEST_TRACKER,0,0) | " +
              $"{east}/3012:Quest.Complete()",
              string.Join(" | ", new (QuestScript Q, int Csid)[] { (new QC.Adoulin.ItSetsMyHeartAflutter(), 331),
                  (new QC.Adoulin.AGoodPairOfCrocs(), 3002), (new QC.Adoulin.AShotInTheDark(), 3012) }
                  .SelectMany(x => new[] { Fin(x.Q, x.Csid, true), Fin(x.Q, x.Csid, false) })));
    }
}
