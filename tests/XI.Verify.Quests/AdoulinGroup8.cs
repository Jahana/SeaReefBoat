// XI.Verify.Quests -- quest rework B3b Adoulin group 8 (call-only: Exotic Delicacies, The Weatherspoon War,
// Do Not Go into the Light, Vegetable Vegetable Crisis): each definition validates clean and is call-only; the
// replies' kinds, which no flow can show: in an NPC call the bridge renders an Event, a ProgressEvent and a
// PriorityEvent alike, as player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply). Each pin is the Lua's:
// player:startEvent(id) with no arguments, or nothing (null: the script goes on to its next call or its shop), with
// what the talk did on the way (Flapano_Odd_Even). The finishes' calls in the scripts' order: 2860 on 0 and 1, 2861
// (npcUtil.completeQuest, then the trade confirmed and the var 0; nothing more when it fails), 5076 and 5089.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunAdoulinGroup8()
    {
        Console.WriteLine("== quest rework B3b Adoulin group 8: Flapano, Chanteillie (call-only) ==");
        var ed = new QC.Adoulin.ExoticDelicacies().Compile();
        var tw = new QC.Adoulin.TheWeatherspoonWar().Compile();
        var dn = new QC.Adoulin.DoNotGoIntoTheLight().Compile();
        var vv = new QC.Adoulin.VegetableVegetableCrisis().Compile();
        foreach (var (name, def) in new[]
                 {
                     ("Exotic Delicacies", ed), ("The Weatherspoon War", tw), ("Do Not Go into the Light", dn),
                     ("Vegetable Vegetable Crisis", vv),
                 })
        {
            Check($"adoulin g8:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(def)));
            Check($"adoulin g8:{name} is call-only", "True", def.CallOnly.ToString());
        }

        const int Saffron = 3916, Mussel = 5949, Barnacle = 5954, Lumber = 3927, Midrium = 3919, Raaz = 8708;
        int Pickaxe = QC.KeyItem.InventorsCoalitionPickaxe;
        var lines = new System.Collections.Generic.List<string>();
        void L(string key, string v) => lines.Add(key + "=" + v);
        Fake Fk(QuestStatus st, (string, int)[] vars = null, int[] keyItems = null, params (int, int)[] trade)
        {
            var f = new Fake { Status = st };
            foreach (var (n, v) in vars ?? Array.Empty<(string, int)>()) f.CharVars[n] = v;
            foreach (int k in keyItems ?? Array.Empty<int>()) f.KeyItems[k] = true;
            foreach (var (i, q) in trade) f.TradeItems[i] = q;
            return f;
        }
        ushort Wa = QC.Zone.WesternAdoulin;
        string Flapano = QC.Npc.WesternAdoulin.Flapano, Chanteillie = QC.Npc.WesternAdoulin.Chanteillie;
        // the reply, then what the handler did on the way
        string R(QuestScriptDefinition def, QuestHandlerKind kind, string npc, Fake f)
        {
            string r = AU3Npc(def, kind, Wa, npc, f);
            return f.Calls.Count == 0 ? r : r + "+" + string.Join("+", f.Calls);
        }
        const QuestHandlerKind Tr = QuestHandlerKind.OnTrigger, Td = QuestHandlerKind.OnTrade;
        var odd0 = new[] { ("Flapano_Odd_Even", 0) };
        var odd1 = new[] { ("Flapano_Odd_Even", 1) };

        L("ed offer", R(ed, Tr, Flapano, Fk(QuestStatus.Available, odd0)));
        L("ed reminder", R(ed, Tr, Flapano, Fk(QuestStatus.Accepted, odd0)));
        L("ed shop turn", R(ed, Tr, Flapano, Fk(QuestStatus.Accepted, odd1)));
        L("ed shop completed", R(ed, Tr, Flapano, Fk(QuestStatus.Completed, odd1)));
        var ingredients = new[] { (Saffron, 1), (Mussel, 1), (Barnacle, 2) };
        var oneBarnacle = new[] { (Saffron, 1), (Mussel, 1), (Barnacle, 1) };
        (int, int) paella = (QC.Item.PlateOfBarnaclePaella, 1), flapanos = (QC.Item.PlateOfFlapanosPaella, 1);
        L("ed ingredients", R(ed, Td, Flapano, Fk(QuestStatus.Accepted, trade: ingredients)));
        L("ed one barnacle", R(ed, Td, Flapano, Fk(QuestStatus.Accepted, trade: oneBarnacle)));
        L("ed paella", R(ed, Td, Flapano, Fk(QuestStatus.Accepted, trade: paella)));
        L("ed flapano's", R(ed, Td, Flapano, Fk(QuestStatus.Accepted, trade: flapanos)));
        L("ed paella available", R(ed, Td, Flapano, Fk(QuestStatus.Available, trade: paella)));
        L("tw 191", R(tw, Tr, Flapano, Fk(QuestStatus.Accepted, new[] { ("Weatherspoon_War_Status", 6) })));
        L("tw 5", R(tw, Tr, Flapano, Fk(QuestStatus.Accepted, new[] { ("Weatherspoon_War_Status", 5) })));
        var dnVar = new[] { ("DNGITL_Status", 3) };
        (int, int) ingot = (QC.Item.DamascusIngot, 1), crystal = (QC.Item.FireCrystal, 1);
        var dnItems = new[] { (Lumber, 1), ingot, crystal };
        L("dn 5076", R(dn, Td, Chanteillie, Fk(QuestStatus.Accepted, dnVar, trade: dnItems)));
        L("dn two", R(dn, Td, Chanteillie, Fk(QuestStatus.Accepted, dnVar, trade: new[] { ingot, crystal })));
        L("dn 5077", R(dn, Tr, Chanteillie, Fk(QuestStatus.Accepted, keyItems: new[] { Pickaxe })));
        L("dn no pickaxe", R(dn, Tr, Chanteillie, Fk(QuestStatus.Accepted)));
        var vvVar = new[] { ("VVC_Status", 1) };
        var vvItems = new[] { (Lumber, 1), (Midrium, 1), (Raaz, 1) };
        L("vv 5089", R(vv, Td, Chanteillie, Fk(QuestStatus.Accepted, vvVar, trade: vvItems)));
        L("vv two", R(vv, Td, Chanteillie, Fk(QuestStatus.Accepted, vvVar, trade: new[] { (Midrium, 1), (Raaz, 1) })));
        L("vv 5088", R(vv, Tr, Chanteillie, Fk(QuestStatus.Accepted, vvVar)));
        L("vv status 2", R(vv, Tr, Chanteillie, Fk(QuestStatus.Accepted, new[] { ("VVC_Status", 2) })));
        Check("adoulin g8:the reply kinds no flow can show",
              "ed offer=Event(2860)+Player.SetCharVar(Flapano_Odd_Even,1,0) ; " +
              "ed reminder=Event(2863)+Player.SetCharVar(Flapano_Odd_Even,1,0) ; " +
              "ed shop turn=null+Player.SetCharVar(Flapano_Odd_Even,0,0) ; ed shop completed=null ; " +
              "ed ingredients=Event(2861) ; ed one barnacle=null ; ed paella=Event(2862) ; " +
              "ed flapano's=Event(2862) ; " +
              "ed paella available=null ; tw 191=Event(191) ; tw 5=null ; dn 5076=Event(5076) ; dn two=null ; " +
              "dn 5077=Event(5077) ; dn no pickaxe=null ; vv 5089=Event(5089) ; vv two=null ; vv 5088=Event(5088) ; " +
              "vv status 2=null",
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
        Check("adoulin g8:2860's finish: added on option 1 only",
              "|Quest.Begin()", Finish(ed, 2860, 0) + "|" + Finish(ed, 2860, 1));
        void Rates(Fake f)
        {
            f.MainNumbers["BAYLD_RATE"] = 1.375;
            f.MainNumbers["EXP_RATE"] = 1.125;
        }
        Check("adoulin g8:2861's finish: npcUtil.completeQuest step for step (the paella, the bayld and the exp at " +
              "their rates, rounded half away from zero, the raw completion), then the trade and the var 0; nothing " +
              "more when the paella cannot be given",
              "Player.GiveItem(5975);Player.AddCurrency(bayld,688);Text.Special(256:BAYLD_OBTAINED,688);" +
              "Player.AddExp(1125);Player.CompleteQuest(Adoulin,EXOTIC_DELICACIES);Trade.Confirm();" +
              "Player.SetCharVar(Flapano_Odd_Even,0,0)|Player.GiveItem(5975)",
              Finish(ed, 2861, setup: Rates) + "|" + Finish(ed, 2861, setup: f => { Rates(f); f.GiveResult = false; }));
        // where half away from zero and half to even part: 500 x 1.125 = 562.5 (563, not 562) and
        // 1000 x 1.0625 = 1062.5 (1063, not 1062); both rates are exact in binary
        Check("adoulin g8:2861's rates round half away from zero, not half to even",
              "Player.AddCurrency(bayld,563)|Text.Special(256:BAYLD_OBTAINED,563)|Player.AddExp(1063)",
              string.Join("|", Finish(ed, 2861, setup: f =>
              {
                  f.MainNumbers["BAYLD_RATE"] = 1.125;
                  f.MainNumbers["EXP_RATE"] = 1.0625;
              }).Split(';').Where(c => c.StartsWith("Player.AddCurrency") || c.StartsWith("Text.Special") ||
                                        c.StartsWith("Player.AddExp"))));
        Check("adoulin g8:5076's finish: the trade confirmed, the pickaxe with its message, DNGITL_Status 0",
              $"Trade.Confirm();Player.GiveKeyItem({Pickaxe});Player.SetCharVar(DNGITL_Status,0,0)", Finish(dn, 5076));
        Check("adoulin g8:5089's finish: the trade confirmed, VVC_Status 2, VVC_Gameday_Wait the unique day",
              "Trade.Confirm();Player.SetCharVar(VVC_Status,2,0);Player.SetCharVar(VVC_Gameday_Wait,8123,0)",
              Finish(vv, 5089, setup: f => f.UniqueDay = 8123));
    }
}
