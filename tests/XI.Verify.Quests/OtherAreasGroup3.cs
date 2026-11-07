// XI.Verify.Quests -- quest rework B3b Other Areas group 3 (Inside the Belly, Test My Mettle): the facet members the group
// added (Player.Mod, Player.SetServerVar, Quest.DelCurrent, Trade.SlotCount / ItemId, Npc.XPos / YPos / ZPos / QueueMove),
// on the fake; the arms no trace can record; and the reply kinds no trace can see.
// - Inside the Belly's accepted 166 / 167 finish (state 2) ends in a Lua error (the completed state's finish runs
//   after it, with the fishId it cleared; the #accepted flow records it): `oa g3 itb` checks what it does (the reward,
//   complete, the fame), and that the completed state's finish then does nothing (fixed, user ruling 2026-11-04).
// - Test My Mettle's 120 finish with an option of 1 to 100 (no bet below it) makes the Lua error on the gil compare:
//   `oa g3 tmm` checks the C# does nothing.
// - Kinds: Zaldon's 166 / 167 (Event) have no competitor (Zaldon's script answers an Odontotyrannus only, Under the Sea a
//   Fat Greedie only); Devean has no script and no other quest (120 / 122 / 123 / 125 Progress, 121 Event); the Jar's
//   noAction beats only its default. The traces do see Zaldon's 161 (Progress, a tie with Under the Sea's 34) and the tier
//   events (Event, beaten by 34): `otherAreas.inside_the_belly#kinds`.
using System;
using System.Globalization;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunOtherAreasGroup3()
    {
        Console.WriteLine("== quest rework B3b Other Areas group 3: facets, the waived arms, reply kinds ==");
        RunOtherAreasGroup3Facets();
        RunOtherAreasGroup3InsideTheBelly();
        RunOtherAreasGroup3TestMyMettle();
    }

    static string OA3Inv(double d) => d.ToString(CultureInfo.InvariantCulture);

    // player:getMod, SetServerVariable, player:delCurrentQuest, trade:getSlotCount / getItemId, npc:getXPos / getYPos /
    // getZPos, npcUtil.queueMove
    static void RunOtherAreasGroup3Facets()
    {
        var f = new Fake { Status = QuestStatus.Accepted };
        f.Mods[127] = 2;
        f.TradeSlots.Add(QC.Item.Ahtapot);
        f.TradeSlots.Add(QC.Item.Hakuryu);
        f.Npc = new QuestNpcFake(17388022, f.Calls) { XPos = 25.08, YPos = 0.44, ZPos = -107.3 };
        IQuestNpc npc = f.Npc;
        string reads = $"{f.Player.Mod(127)}/{f.Player.Mod(1)}/{f.Trade.SlotCount}/{f.Trade.ItemId(0)}/{f.Trade.ItemId(1)}/" +
                       $"{f.Trade.ItemId(2)}/{OA3Inv(npc.XPos)}/{OA3Inv(npc.YPos)}/{OA3Inv(npc.ZPos)}";
        f.Quest.DelCurrent();
        f.Player.SetServerVar("Davoi_Jar_Move_Time", 1800000500);
        npc.QueueMove(58.3, -9.78, -132.43);
        Check("oa g3 facets:the fake answers Mod, SlotCount / ItemId and the npc's position; DelCurrent (accepted to available), SetServerVar and QueueMove are recorded",
              $"2/0/2/{QC.Item.Ahtapot}/{QC.Item.Hakuryu}/0/25.08/0.44/-107.3|Available|1800000500|" +
              "Quest.DelCurrent();Player.SetServerVar(Davoi_Jar_Move_Time,1800000500);Npc.QueueMove(58.3,-9.78,-132.43)",
              $"{reads}|{f.Status}|{f.ServerVars["Davoi_Jar_Move_Time"]}|{string.Join(";", f.Calls)}");
    }

    static Fake OA3ItbFake(QuestStatus status, int fishId, int itemIdx)
    {
        var f = new Fake { Status = status };
        f.QuestLocalVars["fishId"] = fishId;
        f.QuestLocalVars["itemIdx"] = itemIdx;
        return f;
    }

    static Fake OA3ItbTrade(int fish, int roll)
    {
        var f = new Fake { Status = QuestStatus.Completed };
        f.TradeItems[fish] = 1;
        f.TradeSlots.Add(fish);
        f.Rolls.Enqueue(roll);
        f.Skills[QC.Skill.Fishing] = 300;
        return f;
    }

    static void RunOtherAreasGroup3InsideTheBelly()
    {
        var itb = new QC.OtherAreas.InsideTheBelly().Compile();
        var fin = QuestHandlerKind.OnEventFinish;
        int sandoria = QC.FameArea.Sandoria, bastok = QC.FameArea.Bastok;

        // state 2's 166: the Silver Shark's Trident (no min / max), 250 gil, the title, complete, fame +10 x2
        var shark = OA3ItbFake(QuestStatus.Accepted, QC.Item.SilverShark, 1);
        G2Handler(itb, 1, fin, null, 166).Run(shark);
        // state 2's 166 for a Monke-Onke: randomInt(1, 6) of the dust (a roll of 4), no title
        var monke = OA3ItbFake(QuestStatus.Accepted, QC.Item.MonkeOnke1, 1);
        monke.Rolls.Enqueue(4);
        G2Handler(itb, 1, fin, null, 166).Run(monke);
        // state 2's 167: no item, the gil, complete, fame
        var none = OA3ItbFake(QuestStatus.Accepted, QC.Item.Ahtapot, 0);
        G2Handler(itb, 1, fin, null, 167).Run(none);
        Check("oa g3 itb:the accepted 166 / 167 finish gives the reward (the item, min..max of it, the gil, the title), clears the local vars, completes and adds fame +10 San d'Oria and Bastok",
              $"Player.GiveItems({QC.Item.Trident}x1);Player.GiveCurrency(gil,250);Quest.SetLocalVar(fishId,0);Quest.SetLocalVar(itemIdx,0);" +
              $"Player.AddTitle({QC.Title.AceAngler});Quest.Complete();Player.AddFame({sandoria},10);Player.AddFame({bastok},10)|" +
              $"Player.RandomInt(1,6);Player.GiveItems({QC.Item.PinchOfPoisonDust}x4);Player.GiveCurrency(gil,150);Quest.SetLocalVar(fishId,0);" +
              $"Quest.SetLocalVar(itemIdx,0);Quest.Complete();Player.AddFame({sandoria},10);Player.AddFame({bastok},10)|" +
              $"Player.GiveCurrency(gil,350);Quest.SetLocalVar(fishId,0);Quest.SetLocalVar(itemIdx,0);Quest.Complete();" +
              $"Player.AddFame({sandoria},10);Player.AddFame({bastok},10)",
              $"{string.Join(";", shark.Calls)}|{string.Join(";", monke.Calls)}|{string.Join(";", none.Calls)}");

        // the completed state's finish, run after it by the lookup with fishId 0: nothing (the Lua raises there)
        string after;
        var cleared = OA3ItbFake(QuestStatus.Completed, 0, 0);
        try
        {
            G2Handler(itb, 2, fin, null, 166).Run(cleared);
            after = "ran: " + string.Join(";", cleared.Calls);
        }
        catch (NullReferenceException)
        {
            after = "NullReferenceException: " + string.Join(";", cleared.Calls);
        }
        Check("oa g3 itb:the completed state's 166 with the fishId the accepted finish cleared does nothing (the Lua raises on reward.gil)",
              "ran: ", after);

        // the accepted state's finish with no fish pending (a GM-forced 166): it fails where the Lua fails, before
        // the completion (only the completed state's finishes got the fix)
        string forced;
        var gm = OA3ItbFake(QuestStatus.Accepted, 0, 0);
        try
        {
            G2Handler(itb, 1, fin, null, 166).Run(gm);
            forced = "ran: " + string.Join(";", gm.Calls);
        }
        catch (NullReferenceException)
        {
            forced = "NullReferenceException: " + string.Join(";", gm.Calls);
        }
        Check("oa g3 itb:the accepted state's 166 with no fish pending fails before the completion (the Lua's reward.gil on nil)",
              "NullReferenceException: ", forced);

        // one fake through both finishes: state 2's 166 completes it, state 3 then matches, and its 166 (the fishId cleared)
        // runs with no further effect
        var chain = OA3ItbFake(QuestStatus.Accepted, QC.Item.SilverShark, 1);
        chain.MapBools["FISHING_ENABLE"] = true;
        G2Handler(itb, 1, fin, null, 166).Run(chain);
        int before = chain.Calls.Count;
        string matched = itb.States[2].IsMatch(chain.Status, 0, chain).ToString();
        string second;
        try
        {
            G2Handler(itb, 2, fin, null, 166).Run(chain);
            second = "ran";
        }
        catch (NullReferenceException)
        {
            second = "NullReferenceException";
        }
        Check("oa g3 itb:one fake through both finishes: after state 2's 166 completes it, state 3 matches, and its 166 runs with no effect (the Lua raises)",
              "Completed|True|ran|0",
              $"{chain.Status}|{matched}|{second}|{chain.Calls.Count - before}");

        // the trade replies: Event 166 (0, the item) at the first sum the roll does not exceed, Event 167 past them; the
        // trade confirmed first. Zaldon's 161 a Progress reply
        var hit = OA3ItbTrade(QC.Item.Ahtapot, 287);
        var miss = OA3ItbTrade(QC.Item.Ahtapot, 288);
        Check("oa g3 itb:Zaldon's 166 and 167 are Event replies (an Ahtapot's roll of 287 and 288), the trade confirmed before; 161 a Progress reply",
              $"Event(166,0,{QC.Item.DecayedIngot})|Event(167)|Quest.SetLocalVar(fishId,{QC.Item.Ahtapot});Quest.SetLocalVar(itemIdx,0);" +
              "Player.RandomInt(1,1000);Trade.Confirm();Quest.SetLocalVar(itemIdx,2)|ProgressEvent(161)",
              $"{G2Reply(G2Handler(itb, 2, QuestHandlerKind.OnTrade, QC.Npc.Selbina.Zaldon), hit)}|" +
              $"{G2Reply(G2Handler(itb, 2, QuestHandlerKind.OnTrade, QC.Npc.Selbina.Zaldon), miss)}|" +
              $"{string.Join(";", hit.Calls)}|" +
              $"{G2Reply(G2Handler(itb, 0, QuestHandlerKind.OnTrigger, QC.Npc.Selbina.Zaldon), new Fake { Status = QuestStatus.Available })}");

        // the Abaia's last sum (15 + 78 + 125 + 7 = 225, the Plumb Boots): the golden stream (seed 7) has no roll of 225
        // before round ~3350 of a #draws flow, so its bound is checked here (#draws has 223, inside the window)
        Check("oa g3 itb:an Abaia's roll of 225 is the Plumb Boots' 166, 226 a 167",
              $"Event(166,0,{QC.Item.PlumbBoots})|Event(167)",
              $"{G2Reply(G2Handler(itb, 2, QuestHandlerKind.OnTrade, QC.Npc.Selbina.Zaldon), OA3ItbTrade(QC.Item.Abaia, 225))}|" +
              $"{G2Reply(G2Handler(itb, 2, QuestHandlerKind.OnTrade, QC.Npc.Selbina.Zaldon), OA3ItbTrade(QC.Item.Abaia, 226))}");
    }

    static Fake OA3TmmFake(QuestStatus status, int option)
    {
        var f = new Fake { Status = status, Option = option, GilHeld = 20000, Now = 1800000000 };
        return f;
    }

    static void RunOtherAreasGroup3TestMyMettle()
    {
        var tmm = new QC.OtherAreas.TestMyMettle().Compile();
        var fin = QuestHandlerKind.OnEventFinish;

        // 120's finish with an option no bet is below (the Lua errors on `getGil() >= nil`): nothing
        var o1 = OA3TmmFake(QuestStatus.Available, 1);
        var o50 = OA3TmmFake(QuestStatus.Available, 50);
        var o100 = OA3TmmFake(QuestStatus.Available, 100);
        var o101 = OA3TmmFake(QuestStatus.Available, 101);
        foreach (var f in new[] { o1, o50, o100, o101 }) G2Handler(tmm, 0, fin, null, 120).Run(f);
        Check("oa g3 tmm:120's finish with an option of 1, 50 or 100 (no bet below it; the Lua errors) does nothing; 101 bets 100",
              "|||Player.DelGil(100);Quest.SetVar(Timer,1800000144,0);Quest.SetVar(Reward,300,0);Quest.Begin()",
              $"{string.Join(";", o1.Calls)}|{string.Join(";", o50.Calls)}|{string.Join(";", o100.Calls)}|{string.Join(";", o101.Calls)}");

        // Devean's replies: 120 Progress (fixed), 121 Event, 122 / 123 / 125 Progress; the Jar's noAction
        var running = new Fake { Status = QuestStatus.Accepted, Now = 1800000000 };
        running.Vars["Timer"] = 1800000144;
        var over = new Fake { Status = QuestStatus.Accepted, Now = 1800000000 };
        over.Vars["Timer"] = 1800000000;
        over.Items.Add(QC.Item.PowerSandals);
        var overOption = new Fake { Status = QuestStatus.Accepted, Now = 1800000000 };
        overOption.Vars["Option"] = 1;
        var tradeRunning = new Fake { Status = QuestStatus.Accepted, Now = 1800000000 };
        tradeRunning.Vars["Timer"] = 1800000001;
        tradeRunning.TradeItems[QC.Item.PowerSandals] = 1;
        var tradeOver = new Fake { Status = QuestStatus.Accepted, Now = 1800000000 };
        tradeOver.TradeItems[QC.Item.PowerSandals] = 1;
        var jar = new Fake { Status = QuestStatus.Accepted };
        jar.GiveResult = false;
        Check("oa g3 tmm:Devean's 120, 122, 123 and 125 are Progress replies, 121 an Event; the Jar answers noAction",
              $"ProgressEvent(120,0,{QC.Item.PowerSandals},0,0,0,{QC.Item.FlintStone})|Event(121,{QC.Item.PowerSandals},1)|ProgressEvent(125)|" +
              "ProgressEvent(123)|ProgressEvent(122)|ProgressEvent(123)|NoAction()",
              $"{G2Reply(G2Handler(tmm, 0, QuestHandlerKind.OnTrigger, QC.Npc.Selbina.Devean), new Fake { Status = QuestStatus.Available })}|" +
              $"{G2Reply(G2Handler(tmm, 1, QuestHandlerKind.OnTrigger, QC.Npc.Selbina.Devean), running)}|" +
              $"{G2Reply(G2Handler(tmm, 1, QuestHandlerKind.OnTrigger, QC.Npc.Selbina.Devean), over)}|" +
              $"{G2Reply(G2Handler(tmm, 1, QuestHandlerKind.OnTrigger, QC.Npc.Selbina.Devean), overOption)}|" +
              $"{G2Reply(G2Handler(tmm, 1, QuestHandlerKind.OnTrade, QC.Npc.Selbina.Devean), tradeRunning)}|" +
              $"{G2Reply(G2Handler(tmm, 1, QuestHandlerKind.OnTrade, QC.Npc.Selbina.Devean), tradeOver)}|" +
              $"{G2Reply(G2Handler(tmm, 1, QuestHandlerKind.OnTrigger, QC.Npc.Davoi.Jar), jar)}");

        // moveJar (the Jar's trigger, given the sandals): randomInt(125, 10800) for Davoi_Jar_Move_Time = now + it (a server
        // variable, which no trace shows); pickNewPosition's randomInt(1, 0) (the engine answers 1 without drawing; the fake
        // dequeues the 1 given), the jar's spot found by its floored coordinates (spot 6), randomInt(1, 10) until it
        // differs (6, 6, then 3), the move to spot 3
        var move = new Fake { Status = QuestStatus.Accepted, Now = 1800000000 };
        move.Npc = new QuestNpcFake(17388022, move.Calls) { XPos = 25.08, YPos = 0.44, ZPos = -107.3 };
        foreach (int r in new[] { 500, 1, 6, 6, 3 }) move.Rolls.Enqueue(r);
        G2Handler(tmm, 1, QuestHandlerKind.OnTrigger, QC.Npc.Davoi.Jar).Reply(move);
        Check("oa g3 jar:the Jar gives the sandals and moveJar sets Davoi_Jar_Move_Time to now + randomInt(125, 10800) and moves the jar off its spot",
              $"Player.GiveItem({QC.Item.PowerSandals});Player.RandomInt(125,10800);Player.SetServerVar(Davoi_Jar_Move_Time,1800000500);Player.RandomInt(1,0);" +
              "Player.RandomInt(1,10);Player.RandomInt(1,10);Player.RandomInt(1,10);Npc.QueueMove(275.77,3.16,-204.12)",
              string.Join(";", move.Calls));
    }
}
