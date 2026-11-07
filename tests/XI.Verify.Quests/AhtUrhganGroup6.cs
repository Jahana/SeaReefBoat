// XI.Verify.Quests -- quest rework B3b Aht Urhgan group 6 (Saga of the Skyserpent, Ode to the Serpents, When the Bow
// Breaks, Fist of the People, Soothing Waters, Rock Bottom, Embers of His Past): each definition validates clean; the
// declared steps; the reply kinds that no flow can show: the NPCs with one quest container and no script (the pin
// check) and Embers' Wajaom zone-ins; Embers of His Past's 19 finish arming its 1000 ms player timer, and the
// callback's setPos; its 17 finish's startCutscene.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunAhtUrhganGroup6()
    {
        Console.WriteLine("== quest rework B3b Aht Urhgan group 6: Fari-Wari's serpent chain and Mount Zhayolm ==");
        var sa = new QC.AhtUrhgan.SagaOfTheSkyserpent().Compile();
        var od = new QC.AhtUrhgan.OdeToTheSerpents().Compile();
        var bw = new QC.AhtUrhgan.WhenTheBowBreaks().Compile();
        var fp = new QC.AhtUrhgan.FistOfThePeople().Compile();
        var sw = new QC.AhtUrhgan.SoothingWaters().Compile();
        var rb = new QC.AhtUrhgan.RockBottom().Compile();
        var em = new QC.AhtUrhgan.EmbersOfHisPast().Compile();
        foreach (var (name, def) in new[] { ("Saga of the Skyserpent", sa), ("Ode to the Serpents", od),
                     ("When the Bow Breaks", bw), ("Fist of the People", fp), ("Soothing Waters", sw),
                     ("Rock Bottom", rb), ("Embers of His Past", em) })
            Check($"ahturhgan g6:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(def)));

        // The steps each quest says it sets (the validator's metadata: no flow can see an extra one declared)
        Check("ahturhgan g6:the declared steps", "1,2|||1|1,2,3,4|1,2|1,2,3,4,5,6,7,8,9",
              string.Join("|", new[] { sa, od, bw, fp, sw, rb, em }.Select(d => string.Join(",", d.DeclaredSteps))));
        // The reply kinds no flow can show. A Progress beats an Event and equal kinds take turns, so a kind shows only
        // against a rival container or an NPC script; the flows (saga #rivals, ode #rivals, soothing #rivals and
        // #nadeey) put one at Fari-Wari and Nadeey. These have none: each is the only quest container at a script-less
        // NPC (its default action, where it has one, ranks below every quest reply), and the Leypoint's other trades
        // (Navigating the Unfriendly Seas, Olduum) take other items; Embers' trade is the only trade line at Fari-Wari
        // (embers #rivals shows its talks' kinds). So a later rival there is caught here. Each pin is the Lua's kind.
        var lines = new System.Collections.Generic.List<string>();
        void L(string key, string v) => lines.Add(key + "=" + v);
        Fake Fk(QuestStatus st, int step = 0, int hour = 0) => new Fake { Status = st, Step = step, Hour = hour };
        Fake OdeAccepted()
        {
            var f = Fk(QuestStatus.Available);
            f.QuestStatuses["AhtUrghan/ODE_TO_THE_SERPENTS"] = QuestStatus.Accepted;
            return f;
        }
        Fake Traded(int step, int item, bool match = false)
        {
            var f = Fk(QuestStatus.Accepted, step);
            f.TradeItems[item] = 1;
            f.TradeSlots.Add(item);
            f.TradeResult = match;
            return f;
        }
        ushort Aw = QC.Zone.AhtUrhganWhitegate, Az = QC.Zone.AlZahbi, Wj = QC.Zone.WajaomWoodlands;
        ushort Mz = QC.Zone.MountZhayolm;
        const QuestHandlerKind Tr = QuestHandlerKind.OnTrigger, Td = QuestHandlerKind.OnTrade;
        foreach (var s in new[] { 0, 1, 2 })
            L("sa biyaada " + s, AU3Npc(sa, Tr, Az, QC.Npc.AlZahbi.Biyaada, Fk(QuestStatus.Accepted, s)));
        L("sa qm7 0", AU3Npc(sa, Tr, QC.Zone.Halvung, QC.Npc.Halvung.qm7, Fk(QuestStatus.Accepted, 0)));
        L("bw gaweesh avail", AU3Npc(bw, Tr, Az, QC.Npc.AlZahbi.Gaweesh, OdeAccepted()));
        L("bw tower", AU3Npc(bw, Td, Wj, QC.Npc.WajaomWoodlands.GiwahbWatchtower, Traded(0, QC.Item.FrayedArrow)));
        L("fp talhaal avail", AU3Npc(fp, Tr, Az, QC.Npc.AlZahbi.Talhaal, OdeAccepted()));
        L("fp leypoint 1", AU3Npc(fp, Td, Wj, QC.Npc.WajaomWoodlands.Leypoint, Traded(1, QC.Item.RustyMedal)));
        foreach (var s in new[] { 0, 1, 2, 3, 4 })
            L("sw eunheem " + s, AU3Npc(sw, Tr, Aw, QC.Npc.AhtUrhganWhitegate.Eunheem, Fk(QuestStatus.Accepted, s)));
        L("sw eunheem done", AU3Npc(sw, Tr, Aw, QC.Npc.AhtUrhganWhitegate.Eunheem, Fk(QuestStatus.Completed)));
        L("sw mihli 2", AU3Npc(sw, Tr, Az, QC.Npc.AlZahbi.MihliAliapoh, Fk(QuestStatus.Accepted, 2)));
        L("sw qm10 3", AU3Npc(sw, Td, QC.Zone.AydeewaSubterrane, QC.Npc.AydeewaSubterrane.qm10,
            Traded(3, QC.Item.TuftOfColorfulHair)));
        var rank = Fk(QuestStatus.Completed);
        rank.MercRank = 3;
        L("sw rughadjeen done", AU3Npc(sw, Tr, Az, QC.Npc.AlZahbi.Rughadjeen, rank));
        L("sw gadalar done", AU3Npc(sw, Tr, Az, QC.Npc.AlZahbi.Gadalar, Fk(QuestStatus.Completed)));
        L("sw mihli done", AU3Npc(sw, Tr, Az, QC.Npc.AlZahbi.MihliAliapoh, Fk(QuestStatus.Completed)));
        L("sw zazarg done", AU3Npc(sw, Tr, Az, QC.Npc.AlZahbi.Zazarg, Fk(QuestStatus.Completed)));
        L("sw najelith done", AU3Npc(sw, Tr, Az, QC.Npc.AlZahbi.Najelith, Fk(QuestStatus.Completed)));
        L("rb blank11 avail", AU3Npc(rb, Tr, Mz, QC.Npc.MountZhayolm.blank11, Fk(QuestStatus.Available)));
        L("rb blank11 accepted", AU3Npc(rb, Tr, Mz, QC.Npc.MountZhayolm.blank11, Fk(QuestStatus.Accepted, 1)));
        L("rb trade 1", AU3Npc(rb, Td, Mz, QC.Npc.MountZhayolm.blank11, Traded(1, QC.Item.Pickaxe, match: true)));
        L("rb trade 2", AU3Npc(rb, Td, Mz, QC.Npc.MountZhayolm.blank11, Traded(2, QC.Item.MythrilPick, match: true)));
        L("em sprightly 3", AU3Npc(em, Tr, Mz, QC.Npc.MountZhayolm.SprightlyFootsteps,
            Fk(QuestStatus.Accepted, 3, 20)));
        L("em petals 4", AU3Npc(em, Tr, Mz, QC.Npc.MountZhayolm.WitheredPetals, Fk(QuestStatus.Accepted, 4, 20)));
        var night = Traded(8, QC.Item.Hydrangea);
        night.Hour = 20;
        L("em petals trade 8", AU3Npc(em, Td, Mz, QC.Npc.MountZhayolm.WitheredPetals, night));
        L("em fari-wari trade 5", AU3Npc(em, Td, Aw, QC.Npc.AhtUrhganWhitegate.FariWari, Traded(5, QC.Item.Hydrangea)));
        // Embers' Wajaom zone-ins: the zone-in reply pushes only its id (no kind ranks a zone-in), so no flow sees it
        foreach (var s in new[] { 1, 2, 6, 7 })
            L("em wajaom zone-in " + s, AU1Describe(AU3Up(em, Fk(QuestStatus.Accepted, s))
                .Single(x => x.Kind == QuestHandlerKind.OnZoneIn && x.Zone == Wj), Fk(QuestStatus.Accepted, s)));
        Check("ahturhgan g6:the reply kinds no flow can show",
              "sa biyaada 0=Event(278) ; sa biyaada 1=Event(278) ; sa biyaada 2=Event(279) ; sa qm7 0=KeyItem(855) ; " +
              "bw gaweesh avail=ProgressEvent(280) ; bw tower=ProgressEvent(512) ; " +
              "fp talhaal avail=ProgressEvent(282) ; fp leypoint 1=ProgressEvent(511) ; " +
              "sw eunheem 0=ProgressEvent(895,{text_table=0}) ; sw eunheem 1=ReplaceEvent(907) ; " +
              "sw eunheem 2=ReplaceEvent(907) ; sw eunheem 3=ReplaceEvent(907) ; sw eunheem 4=ReplaceEvent(907) ; " +
              "sw eunheem done=ReplaceEvent(285) ; sw mihli 2=ProgressEvent(289) ; sw qm10 3=ProgressCutscene(34) ; " +
              "sw rughadjeen done=ReplaceEvent(284,{[0]=3}) ; sw gadalar done=ReplaceEvent(285) ; " +
              "sw mihli done=ReplaceEvent(286) ; sw zazarg done=ReplaceEvent(287) ; " +
              "sw najelith done=ReplaceEvent(288) ; rb blank11 avail=ProgressEvent(7) ; " +
              "rb blank11 accepted=MessageSpecial(61:NOTHING_HAPPENS) ; rb trade 1=ProgressEvent(8) ; " +
              "rb trade 2=ProgressEvent(9,{[0]=16651}) ; em sprightly 3=ProgressCutscene(15) ; " +
              "em petals 4=ProgressCutscene(16) ; em petals trade 8=ProgressCutscene(17) ; " +
              "em fari-wari trade 5=ProgressEvent(918,{text_table=0}) ; em wajaom zone-in 1=Event(16) ; " +
              "em wajaom zone-in 2=Event(17) ; em wajaom zone-in 6=Event(18) ; em wajaom zone-in 7=Event(19)",
              string.Join(" ; ", lines));

        // Embers of His Past's 19 finish (Wajaom Woodlands, Prog 7): ITEM_RETURNED, Prog 8 and the 1000 ms player
        // timer; the timer fires on the first tick strictly past its deadline, and its callback sets the char down in
        // Whitegate (80 -6 -123, rotation 55). The golden flow shows the same (embers: tick 500, 500, 1)
        var h19 = em.States.SelectMany(st => st.Handlers)
            .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Zone == Wj && x.Id == 19);
        var f19 = new Fake { Status = QuestStatus.Accepted, Step = 7, Csid = 19, Definition = em };
        h19.Run(f19);
        string armed = string.Join(";", f19.Calls);
        int at1000 = f19.FireTimers(1000);
        int at1001 = f19.FireTimers(1001);
        Check("ahturhgan g6:Embers' 19 finish arms the 1000 ms timer; fired past 1000 ms: setPos into Whitegate",
              "1|0|1|toWhitegate|SetPos(80,-6,-123,55," + QC.Zone.AhtUrhganWhitegate + ")",
              $"{f19.Calls.Count(c => c.StartsWith("Player.Timer(1000,toWhitegate)"))}|{at1000}|{at1001}|" +
              $"{em.Callbacks.Single().Name}|{f19.Calls.Last().Replace("Player.", "")}");
        Check("ahturhgan g6:Embers' 19 finish, in order", "Text.Special(51:ITEM_RETURNED,2729);Quest.SetStep(8);" +
              "Player.Timer(1000,toWhitegate)", armed);

        // Embers' 17 finish (Mount Zhayolm, Prog 8): confirm the trade, then startCutscene 18. A cutscene differs from
        // an event only in locking the char (ZoneHost.Events.cs), which no flow statement sees, so the facet is pinned
        var h17 = em.States.SelectMany(st => st.Handlers)
            .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Zone == Mz && x.Id == 17);
        var f17 = new Fake { Status = QuestStatus.Accepted, Step = 8, Csid = 17, Definition = em };
        h17.Run(f17);
        Check("ahturhgan g6:Embers' 17 finish: confirm the trade, then startCutscene 18",
              "Trade.Confirm();Player.StartCutscene(18)", string.Join(";", f17.Calls));
    }
}
