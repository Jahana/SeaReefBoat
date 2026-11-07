// XI.Verify.Quests -- the no-log quests (call-only: The Sahagin's Key, All the Way to the Bank): each definition
// validates clean and is call-only; the replies' kinds, which no flow can show: in an NPC call the bridge renders an
// Event, a ProgressEvent and a PriorityEvent alike, as player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply), and
// NoAction as nothing at all. Each pin is the Lua's: player:startEvent(id[, 1]) with what the talk did on the way
// (the char var), nothing started (Flapano's arm with his bit set: his Exotic Delicacies trade not reached), or no arm
// (null). The finishes' calls in the scripts' order: 107 at Progress 1, 2 and 3, with and without room; 5069, 5070,
// 5071 below and at a full mask; 5068 on 0 and 1. The deliberate log writes (user ruling 2026-11-04): Gimb adds The
// Sahagin's Key while it is available, 107 completes it when a key is handed while it is not completed; a payment adds
// All the Way to the Bank while it is available, the receipt completes it while it is not completed.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunNoLogQuests()
    {
        Console.WriteLine("== no-log quests: The Sahagin's Key, All the Way to the Bank (call-only) ==");
        var sk = new QC.Outlands.TheSahaginsKey().Compile();
        var bank = new QC.Adoulin.AllTheWayToTheBank().Compile();
        foreach (var (name, def) in new[] { ("The Sahagin's Key", sk), ("All the Way to the Bank", bank) })
        {
            Check($"nolog:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(def)));
            Check($"nolog:{name} is call-only", "True", def.CallOnly.ToString());
        }

        var lines = new System.Collections.Generic.List<string>();
        void L(string key, string v) => lines.Add(key + "=" + v);
        ushort Norg = QC.Zone.Norg, Ssg = QC.Zone.SeaSerpentGrotto, Wa = QC.Zone.WesternAdoulin;
        const QuestHandlerKind Tr = QuestHandlerKind.OnTrigger, Td = QuestHandlerKind.OnTrade;
        // the reply, then what the handler did on the way
        string R(QuestScriptDefinition def, QuestHandlerKind kind, ushort zone, string npc, Fake f)
        {
            string r = AU3Npc(def, kind, zone, npc, f);
            return f.Calls.Count == 0 ? r : r + "+" + string.Join("+", f.Calls);
        }
        Fake Sk(int progress, int items, bool key = false, params int[] rolls)
        {
            var f = new Fake { Status = QuestStatus.Available };
            f.CharVars["SahaginKeyProgress"] = progress;
            f.CharVars["SahaginKeyItems"] = items;
            if (key) f.Items.Add(QC.Item.SahaginKey);
            foreach (int r in rolls) f.Rolls.Enqueue(r);
            return f;
        }
        Fake SkTrade(int items, params (int Item, int Qty)[] trade)
        {
            var f = Sk(2, items);
            foreach (var (item, qty) in trade) f.TradeItems[item] = qty;
            return f;
        }
        string Gimb = QC.Npc.Norg.Gimb, Bou = QC.Npc.SeaSerpentGrotto.BoutheRighteous;
        string Hurr = QC.Npc.SeaSerpentGrotto.HurrtheBetrayer;
        int mythril = QC.Item.MythrilBeastcoin, gold = QC.Item.GoldBeastcoin, shell = QC.Item.NorgShell;
        L("sk gimb", R(sk, Tr, Norg, Gimb, Sk(3, 0)));
        Fake gimbAccepted = Sk(0, 0);
        gimbAccepted.Status = QuestStatus.Accepted;
        L("sk gimb accepted", R(sk, Tr, Norg, Gimb, gimbAccepted));
        Fake gimbCompleted = Sk(3, 0);
        gimbCompleted.Status = QuestStatus.Completed;
        L("sk gimb completed", R(sk, Tr, Norg, Gimb, gimbCompleted));
        L("sk bou", R(sk, Tr, Ssg, Bou, Sk(1, 0)));
        L("sk 105", R(sk, Tr, Ssg, Hurr, Sk(2, 2)));
        L("sk 2 key", R(sk, Tr, Ssg, Hurr, Sk(2, 0, key: true)));
        L("sk roll 50", R(sk, Tr, Ssg, Hurr, Sk(3, 0, rolls: 50)));
        L("sk roll 51", R(sk, Tr, Ssg, Hurr, Sk(3, 0, rolls: 51)));
        L("sk 3 key", R(sk, Tr, Ssg, Hurr, Sk(3, 0, key: true)));
        L("sk 3 items 1", R(sk, Tr, Ssg, Hurr, Sk(3, 1, key: true)));
        L("sk 3 items 2", R(sk, Tr, Ssg, Hurr, Sk(3, 2, key: true)));
        L("sk 3 items 3", R(sk, Tr, Ssg, Hurr, Sk(3, 3)));
        L("sk 1", R(sk, Tr, Ssg, Hurr, Sk(1, 0)));
        L("sk trade mythril", R(sk, Td, Ssg, Hurr, SkTrade(1, (mythril, 3), (shell, 1))));
        L("sk trade gold", R(sk, Td, Ssg, Hurr, SkTrade(2, (gold, 1), (shell, 1))));
        L("sk trade gold at 1", R(sk, Td, Ssg, Hurr, SkTrade(1, (gold, 1), (shell, 1))));
        L("sk trade mythril at 2", R(sk, Td, Ssg, Hurr, SkTrade(2, (mythril, 3), (shell, 1))));
        L("sk trade at 0", R(sk, Td, Ssg, Hurr, SkTrade(0, (mythril, 3), (shell, 1))));

        Fake Bank(int payments, bool invoice = true, int gil = 0, params int[] items)
        {
            var f = new Fake { Status = QuestStatus.Available };
            f.CharVars["ATWTTB_Payments"] = payments;
            if (invoice) f.KeyItems[QC.KeyItem.TarutaruSauceInvoice] = true;
            if (gil > 0) f.TradeItems[65535] = gil;
            foreach (int i in items) f.TradeItems[i] = 1;
            return f;
        }
        // one item in slot 0, its AH category
        Fake Gruel(int canTrade, int item, int ahCategory)
        {
            var f = new Fake { Status = QuestStatus.Available };
            f.CharVars["ATWTTB_Can_Trade_Gruel"] = canTrade;
            f.TradeItems[item] = 1;
            f.TradeSlots.Add(item);
            f.AhCategories[item] = ahCategory;
            return f;
        }
        string Defliaa = QC.Npc.WesternAdoulin.Defliaa, Hujette = QC.Npc.WesternAdoulin.Hujette;
        string Flapano = QC.Npc.WesternAdoulin.Flapano, Westerly = QC.Npc.WesternAdoulin.WesterlyBreeze;
        int vegetable = QC.Item.BowlOfVegetableGruel;
        L("bank defliaa", R(bank, Td, Wa, Defliaa, Bank(0, gil: 19440)));
        L("bank defliaa paid", R(bank, Td, Wa, Defliaa, Bank(1, gil: 19440)));
        L("bank defliaa no invoice", R(bank, Td, Wa, Defliaa, Bank(0, invoice: false, gil: 19440)));
        L("bank hujette", R(bank, Td, Wa, Hujette, Bank(4, gil: 3000)));
        L("bank hujette paid", R(bank, Td, Wa, Hujette, Bank(2, gil: 3000)));
        L("bank flapano", R(bank, Td, Wa, Flapano, Bank(0, gil: 5600)));
        L("bank flapano paid", R(bank, Td, Wa, Flapano, Bank(4, gil: 5600)));
        L("bank flapano 5599", R(bank, Td, Wa, Flapano, Bank(4, gil: 5599)));
        L("bank flapano no invoice", R(bank, Td, Wa, Flapano, Bank(0, invoice: false, gil: 5600)));
        L("bank vegetable", R(bank, Td, Wa, Westerly, Gruel(1, vegetable, 56)));
        L("bank medicinal", R(bank, Td, Wa, Westerly, Gruel(1, 4534, 56)));
        L("bank gruel 0", R(bank, Td, Wa, Westerly, Gruel(0, vegetable, 56)));
        L("bank pie", R(bank, Td, Wa, Westerly, Gruel(1, QC.Item.ApplePie, 57)));
        L("bank not a food", R(bank, Td, Wa, Westerly, Gruel(1, vegetable, 58)));
        // the handler's own copy of the script's gate (one item, no gil, AH 52 to 57), which no flow reaches: the
        // script calls the quest only inside it. A gruel given category 52 or 57 is still a food; two gruels are not
        // one item; one gruel with gil (a fake whose item count leaves the gil out) has gil
        L("bank gruel 52", R(bank, Td, Wa, Westerly, Gruel(1, vegetable, 52)));
        L("bank gruel 57", R(bank, Td, Wa, Westerly, Gruel(1, 4534, 57)));
        Fake two = Gruel(1, vegetable, 56);
        two.TradeItems[vegetable] = 2;
        L("bank two gruels", R(bank, Td, Wa, Westerly, two));
        var withGil = new GilFake();
        withGil.CharVars["ATWTTB_Can_Trade_Gruel"] = 1;
        withGil.TradeItems[vegetable] = 1;
        withGil.TradeItems[65535] = 100;
        withGil.TradeSlots.Add(vegetable);
        withGil.AhCategories[vegetable] = 56;
        var westerlyTrade = bank.States.SelectMany(st => st.Handlers)
            .Single(x => x.Kind == Td && x.Zone == Wa && x.Entity == Westerly);
        var gilReply = westerlyTrade.Reply(withGil);
        L("bank gruel gil", gilReply == null ? "null" : QuestContextFake.Describe(gilReply));
        Check("nolog:the reply kinds no flow can show",
              "sk gimb=Event(69)+Player.SetCharVar(SahaginKeyProgress,1,0)+" +
              "Player.AddQuest(Outlands,THE_SAHAGINS_KEY) ; " +
              "sk gimb accepted=Event(69)+Player.SetCharVar(SahaginKeyProgress,1,0) ; " +
              "sk gimb completed=Event(69)+Player.SetCharVar(SahaginKeyProgress,1,0) ; " +
              "sk bou=Event(108)+Player.SetCharVar(SahaginKeyProgress,2,0) ; " +
              "sk 105=Event(105)+Player.SetCharVar(SahaginKeyItems,1,0) ; sk 2 key=Event(104) ; " +
              "sk roll 50=Event(105)+Player.RandomInt(1,100)+Player.SetCharVar(SahaginKeyItems,1,0) ; " +
              "sk roll 51=Event(106)+Player.RandomInt(1,100)+Player.SetCharVar(SahaginKeyItems,2,0) ; " +
              "sk 3 key=Event(104) ; sk 3 items 1=Event(105) ; sk 3 items 2=Event(106) ; sk 3 items 3=Event(104) ; " +
              "sk 1=Event(104) ; sk trade mythril=Event(107) ; sk trade gold=Event(107) ; sk trade gold at 1=null ; " +
              "sk trade mythril at 2=null ; sk trade at 0=null ; " +
              "bank defliaa=Event(5069) ; bank defliaa paid=null ; bank defliaa no invoice=null ; " +
              "bank hujette=Event(5070) ; bank hujette paid=null ; bank flapano=Event(5071) ; " +
              "bank flapano paid=NoAction() ; bank flapano 5599=null ; bank flapano no invoice=null ; " +
              "bank vegetable=Event(5068) ; bank medicinal=Event(5068,1) ; bank gruel 0=null ; bank pie=null ; " +
              "bank not a food=null ; bank gruel 52=Event(5068) ; bank gruel 57=Event(5068,1) ; " +
              "bank two gruels=null ; bank gruel gil=null",
              string.Join(" ; ", lines));

        // the finishes, each run on a fake at the csid (and option), in the scripts' order
        string Finish(QuestScriptDefinition def, ushort zone, int csid, int option = 0, Action<Fake> setup = null)
        {
            var h = def.States.SelectMany(st => st.Handlers)
                .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Zone == zone && x.Id == csid);
            var f = new Fake { Status = QuestStatus.Available, Csid = csid, Option = option, Definition = def };
            setup?.Invoke(f);
            h.Run(f);
            return string.Join(";", f.Calls);
        }
        Action<Fake> At(string name, int value, bool room = true, QuestStatus status = QuestStatus.Available) => f =>
        {
            f.CharVars[name] = value;
            f.GiveResult = room;
            f.Status = status;
        };
        int key = QC.Item.SahaginKey;
        const string Done = "Player.CompleteQuest(Outlands,THE_SAHAGINS_KEY)";
        Check("nolog:107's finish at Progress 2, with room and without, in order (completed: the log, deliberate)",
              $"Player.GiveItem({key});Trade.Confirm();Player.SetCharVar(SahaginKeyProgress,3,0);" +
              $"Player.SetCharVar(SahaginKeyItems,0,0);{Done}|Player.GiveItem({key})",
              Finish(sk, Ssg, 107, setup: At("SahaginKeyProgress", 2)) + "|" +
              Finish(sk, Ssg, 107, setup: At("SahaginKeyProgress", 2, room: false)));
        Check("nolog:107's finish at Progress 3, with room (completed unless it is), without; at Progress 1: nothing",
              $"Player.GiveItem({key});Trade.Confirm();Player.SetCharVar(SahaginKeyItems,0,0);{Done}|" +
              $"Player.GiveItem({key});Trade.Confirm();Player.SetCharVar(SahaginKeyItems,0,0)|" +
              $"Player.GiveItem({key})|",
              Finish(sk, Ssg, 107, setup: At("SahaginKeyProgress", 3)) + "|" +
              Finish(sk, Ssg, 107, setup: At("SahaginKeyProgress", 3, status: QuestStatus.Completed)) + "|" +
              Finish(sk, Ssg, 107, setup: At("SahaginKeyProgress", 3, room: false)) + "|" +
              Finish(sk, Ssg, 107, setup: At("SahaginKeyProgress", 1)));
        int receipt = QC.KeyItem.TarutaruSauceReceipt;
        const string Add = "Player.AddQuest(Adoulin,ALL_THE_WAY_TO_THE_BANK)";
        const string End = "Player.CompleteQuest(Adoulin,ALL_THE_WAY_TO_THE_BANK)";
        Check("nolog:5069's, 5070's and 5071's finishes: the trade taken, the bit set, the receipt at a full mask; " +
              "available: added, the receipt: completed (the log, deliberate)",
              $"Trade.Confirm();Player.SetCharVar(ATWTTB_Payments,1,0);{Add}|" +
              $"Trade.Confirm();Player.SetCharVar(ATWTTB_Payments,31,0);{Add};Player.GiveKeyItem({receipt});{End}|" +
              $"Trade.Confirm();Player.SetCharVar(ATWTTB_Payments,4,0);{Add}|" +
              $"Trade.Confirm();Player.SetCharVar(ATWTTB_Payments,29,0);{Add}|" +
              $"Trade.Confirm();Player.SetCharVar(ATWTTB_Payments,31,0);{Add};Player.GiveKeyItem({receipt});{End}",
              Finish(bank, Wa, 5069) + "|" + Finish(bank, Wa, 5069, setup: At("ATWTTB_Payments", 30)) + "|" +
              Finish(bank, Wa, 5070) + "|" + Finish(bank, Wa, 5070, setup: At("ATWTTB_Payments", 29)) + "|" +
              Finish(bank, Wa, 5071, setup: At("ATWTTB_Payments", 27)));
        Check("nolog:the payments' finishes accepted and completed: no second add; completed: no second completion",
              "Trade.Confirm();Player.SetCharVar(ATWTTB_Payments,1,0)|" +
              $"Trade.Confirm();Player.SetCharVar(ATWTTB_Payments,31,0);Player.GiveKeyItem({receipt});{End}|" +
              $"Trade.Confirm();Player.SetCharVar(ATWTTB_Payments,31,0);Player.GiveKeyItem({receipt})",
              Finish(bank, Wa, 5069, setup: At("ATWTTB_Payments", 0, status: QuestStatus.Accepted)) + "|" +
              Finish(bank, Wa, 5071, setup: At("ATWTTB_Payments", 27, status: QuestStatus.Accepted)) + "|" +
              Finish(bank, Wa, 5069, setup: At("ATWTTB_Payments", 30, status: QuestStatus.Completed)));
        Check("nolog:5068's finish on 0 and on 1, in order",
              "Trade.Complete();Player.GiveCurrency(gil,19716);Player.SetCharVar(ATWTTB_Can_Trade_Gruel,0,0)|" +
              "Trade.Complete();Player.GiveCurrency(gil,39432);Player.SetCharVar(ATWTTB_Can_Trade_Gruel,0,0)",
              Finish(bank, Wa, 5068, 0) + "|" + Finish(bank, Wa, 5068, 1));
    }

    // a trade fake whose item count is its items without the gil (trade:getItemCount() and trade:getGil() apart), so
    // the handler's gil test is the one that answers
    sealed class GilFake : QuestContextFake
    {
        public override int ItemCount => TradeItems.Where(t => t.Key != 65535).Sum(t => t.Value);
    }
}
