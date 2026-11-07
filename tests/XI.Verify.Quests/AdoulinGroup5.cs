// XI.Verify.Quests -- quest rework B3b Adoulin group 5 (call-only: Always More Quoth the Ravenous, Raptor Rapture):
// each definition validates clean and is call-only; the replies' kinds, which no flow can show: in an NPC call the
// bridge renders an Event, a ProgressEvent and a PriorityEvent alike, as player:startEvent (ZoneHost.QuestCalls.cs
// RenderCallReply), and a zone-in reply as the csid the zone's onZoneIn returns. Each pin is the Lua's:
// player:startEvent(id) with no arguments, `return 5056`, or nothing (null), with what the talk did on the way
// (Raptor_Rapture_Status 1). Trade.ItemAhCategory reads the fake's category of the slot's item. The finishes' calls in
// the scripts' order: 3012 and 5039 at midpoint rates, 3010, 3014, 5032, 5034, 5035, 5037,
// 5061 on 0 and 1, 5056 on 0 and 1.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunAdoulinGroup5()
    {
        Console.WriteLine(
            "== quest rework B3b Adoulin group 5: Westerly Breeze, Pagnelle, Gontrain, the zone-in (call-only) ==");
        var am = new QC.Adoulin.AlwaysMoreQuothTheRavenous().Compile();
        var rr = new QC.Adoulin.RaptorRapture().Compile();
        foreach (var (name, def) in new[] { ("Always More Quoth the Ravenous", am), ("Raptor Rapture", rr) })
        {
            Check($"adoulin g5:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(def)));
            Check($"adoulin g5:{name} is call-only", "True", def.CallOnly.ToString());
        }

        var lines = new System.Collections.Generic.List<string>();
        void L(string key, string v) => lines.Add(key + "=" + v);
        ushort Wa = QC.Zone.WesternAdoulin;
        string Westerly = QC.Npc.WesternAdoulin.WesterlyBreeze, Pagnelle = QC.Npc.WesternAdoulin.Pagnelle;
        string Gontrain = QC.Npc.WesternAdoulin.Gontrain;
        const QuestHandlerKind Tr = QuestHandlerKind.OnTrigger, Td = QuestHandlerKind.OnTrade;
        // the reply, then what the handler did on the way
        string R(QuestScriptDefinition def, QuestHandlerKind kind, string npc, Fake f)
        {
            string r = AU3Npc(def, kind, Wa, npc, f);
            return f.Calls.Count == 0 ? r : r + "+" + string.Join("+", f.Calls);
        }
        int adoulin = QC.FameArea.Adoulin;
        Fake Talk(QuestStatus st, int level, bool zone = false, int wait = 0)
        {
            var f = new Fake { Status = st, UniqueDay = 8000, ZoningFlag = zone };
            f.Fame[adoulin] = level;
            f.CharVars["Westerly_Breeze_Wait"] = wait;
            return f;
        }
        // one item in slot 0, its AH category
        Fake Give(QuestStatus st, int item, int ahCategory, int qty = 1)
        {
            var f = new Fake { Status = st };
            f.TradeItems[item] = qty;
            f.TradeSlots.Add(item);
            f.AhCategories[item] = ahCategory;
            return f;
        }
        L("am offer", R(am, Tr, Westerly, Talk(QuestStatus.Available, 3)));
        L("am reminder", R(am, Tr, Westerly, Talk(QuestStatus.Accepted, 3)));
        L("am level 2", R(am, Tr, Westerly, Talk(QuestStatus.Available, 2)));
        L("am zone", R(am, Tr, Westerly, Talk(QuestStatus.Available, 3, zone: true)));
        L("am wait", R(am, Tr, Westerly, Talk(QuestStatus.Available, 3, wait: 8000)));
        L("am completed", R(am, Tr, Westerly, Talk(QuestStatus.Completed, 3)));
        int goblin = QC.Item.BottleOfGoblinDrink, juice = QC.Item.BottleOfAppleJuice;
        int cursed = QC.Item.BottleOfCursedBeverage;
        L("am goblin", R(am, Td, Westerly, Give(QuestStatus.Accepted, goblin, 58)));
        L("am juice", R(am, Td, Westerly, Give(QuestStatus.Accepted, juice, 58)));
        L("am juice available", R(am, Td, Westerly, Give(QuestStatus.Available, juice, 58)));
        L("am cursed", R(am, Td, Westerly, Give(QuestStatus.Accepted, cursed, 37)));
        L("am cursed completed", R(am, Td, Westerly, Give(QuestStatus.Completed, cursed, 37)));
        L("am pie", R(am, Td, Westerly, Give(QuestStatus.Accepted, QC.Item.ApplePie, 57)));
        L("am crystal", R(am, Td, Westerly, Give(QuestStatus.Accepted, QC.Item.FireCrystal, 35)));
        L("am two goblins", R(am, Td, Westerly, Give(QuestStatus.Accepted, goblin, 58, qty: 2)));

        Fake Rr(QuestStatus st, int status, bool zone = false, params int[] keyItems)
        {
            var f = new Fake { Status = st, ZoningFlag = zone };
            f.CharVars["Raptor_Rapture_Status"] = status;
            foreach (int k in keyItems) f.KeyItems[k] = true;
            return f;
        }
        int b1 = QC.KeyItem.Rockberry1, b2 = QC.KeyItem.Rockberry2, b3 = QC.KeyItem.Rockberry3;
        L("rr intro", R(rr, Tr, Pagnelle, Rr(QuestStatus.Available, 0)));
        L("rr offer", R(rr, Tr, Pagnelle, Rr(QuestStatus.Available, 3)));
        foreach (int s in new[] { 4, 5, 7, 8, 9 })
            L($"rr {s}", R(rr, Tr, Pagnelle, Rr(QuestStatus.Accepted, s)));
        L("rr 6", R(rr, Tr, Pagnelle, Rr(QuestStatus.Accepted, 6)));
        L("rr 6 berries", R(rr, Tr, Pagnelle, Rr(QuestStatus.Accepted, 6, keyItems: new[] { b1, b2, b3 })));
        L("rr completed zone", R(rr, Tr, Pagnelle, Rr(QuestStatus.Completed, 0, zone: true)));
        L("rr completed", R(rr, Tr, Pagnelle, Rr(QuestStatus.Completed, 0)));
        L("rr gontrain", R(rr, Tr, Gontrain, Rr(QuestStatus.Accepted, 4)));
        L("rr gontrain 5", R(rr, Tr, Gontrain, Rr(QuestStatus.Accepted, 5)));
        string ZoneIn(Fake f) =>
            AU1Describe(AU3Up(rr, f).Single(x => x.Kind == QuestHandlerKind.OnZoneIn && x.Zone == Wa), f);
        L("rr zone-in 2", ZoneIn(Rr(QuestStatus.Available, 2)));
        L("rr zone-in 3", ZoneIn(Rr(QuestStatus.Available, 3)));
        Check("adoulin g5:the reply kinds no flow can show",
              "am offer=Event(3010) ; am reminder=Event(3011) ; am level 2=null ; am zone=null ; am wait=null ; " +
              "am completed=null ; am goblin=Event(3013) ; am juice=Event(3014) ; am juice available=null ; " +
              "am cursed=Event(3012) ; am cursed completed=null ; am pie=null ; am crystal=null ; " +
              "am two goblins=null ; " +
              "rr intro=Event(5032)+Player.SetCharVar(Raptor_Rapture_Status,1,0) ; rr offer=Event(5061) ; " +
              "rr 4=Event(5033) ; rr 5=Event(5035) ; rr 7=Event(5038) ; rr 8=Event(5039) ; rr 9=null ; " +
              "rr 6=Event(5036) ; rr 6 berries=Event(5037) ; rr completed zone=Event(5040) ; " +
              "rr completed=Event(5041) ; " +
              "rr gontrain=Event(5034) ; rr gontrain 5=null ; rr zone-in 2=Event(5056) ; rr zone-in 3=null",
              string.Join(" ; ", lines));

        // the finishes, each run on a fake at the csid (and option), in the scripts' order
        string Finish(QuestScriptDefinition def, int csid, int option = 0, Action<Fake> setup = null)
        {
            var h = def.States.SelectMany(st => st.Handlers)
                .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Zone == Wa && x.Id == csid);
            var f = new Fake { Status = QuestStatus.Accepted, Csid = csid, Option = option, Definition = def };
            setup?.Invoke(f);
            h.Run(f);
            return string.Join(";", f.Calls);
        }
        Check("adoulin g5:3012's finish at EXP_RATE 1.375, BAYLD_RATE 1.0625 (llround: half away from zero), in order",
              "Trade.Complete();Player.CompleteQuest(Adoulin,ALWAYS_MORE_QUOTH_THE_RAVENOUS);Player.AddExp(2063);" +
              "Player.AddCurrency(bayld,1063);Text.Special(256:BAYLD_OBTAINED,1063);" +
              $"Player.AddFame({adoulin},30);Player.SetCharVar(Westerly_Breeze_Wait,0,0)",
              Finish(am, 3012, setup: f =>
              {
                  f.MainNumbers["EXP_RATE"] = 1.375;
                  f.MainNumbers["BAYLD_RATE"] = 1.0625;
              }));
        Check("adoulin g5:3010's and 3014's finishes: begun; the trade taken",
              "Quest.Begin()|Trade.Complete()", Finish(am, 3010) + "|" + Finish(am, 3014));
        Check("adoulin g5:5039's finish at BAYLD_RATE 1.0625, in order",
              "Player.SetCharVar(Raptor_Rapture_Status,0,0);Player.CompleteQuest(Adoulin,RAPTOR_RAPTURE);" +
              "Player.AddCurrency(bayld,1063);Text.Special(256:BAYLD_OBTAINED,1063);" +
              $"Player.AddFame({adoulin},30);Player.SetNeedToZone(True)",
              Finish(rr, 5039, setup: f => f.MainNumbers["BAYLD_RATE"] = 1.0625));
        Check("adoulin g5:5061's finish: begun and status 4 on option 1 only",
              "|Quest.Begin();Player.SetCharVar(Raptor_Rapture_Status,4,0)",
              Finish(rr, 5061, 0) + "|" + Finish(rr, 5061, 1));
        Check("adoulin g5:5056's finish: status 3 on any option, then begun and status 4 on option 1",
              "Player.SetCharVar(Raptor_Rapture_Status,3,0)|" +
              "Player.SetCharVar(Raptor_Rapture_Status,3,0);Quest.Begin();Player.SetCharVar(Raptor_Rapture_Status,4,0)",
              Finish(rr, 5056, 0) + "|" + Finish(rr, 5056, 1));
        Check("adoulin g5:5037's finish: the three Rockberries taken, then status 7",
              $"Player.DelKeyItem({b1});Player.DelKeyItem({b2});Player.DelKeyItem({b3});" +
              "Player.SetCharVar(Raptor_Rapture_Status,7,0)", Finish(rr, 5037));
        Check("adoulin g5:5032's, 5034's and 5035's finishes",
              "Player.SetPos(0,0,0,0,258)|Player.SetCharVar(Raptor_Rapture_Status,5,0)|" +
              "Player.SetCharVar(Raptor_Rapture_Status,6,0)",
              Finish(rr, 5032) + "|" + Finish(rr, 5034) + "|" + Finish(rr, 5035));
    }
}
