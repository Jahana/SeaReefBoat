// XI.Verify.Quests -- quest rework B3b Other Areas group 11 (call-only: Trial by Lightning, Trial-Size Trial by
// Lightning): what the golden traces cannot show.
// - The reply kinds. Both quests are called from their NPCs' scripts (xi.cs.quest), and an NPC call renders an Event, a
//   ProgressEvent and a PriorityEvent alike, as player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply): there is no
//   lookup action, so no priority to tell them apart, and every trace replays the same with any of the three. The Lua
//   scripts call player:startEvent, so each reply is pinned here as Event.
// - Lacia's trade that leaves the rest to her script: the accepted trade answers nothing (null) for a non-Summoner or
//   another item. Her onTrade has nothing after the call and ignores its answer, so NoAction would look the same in a
//   trace. (Ripapa's onTrigger does read the answer: 10020 follows a false one, so the traces pin his.)
// - 10019's finish with option 0 or above 6 and a free slot. The Lua calls player:addItem(0); the engine (C++
//   lua_base_entity.cpp, C# ZoneHost.Progress.cs) logs a warning and returns nil, it raises nothing, so the finish
//   carries on and completes with no reward. The golden recorder refuses any trace with a "lua:" line (Harness.cs
//   ScriptErrors), so no trace can record this arm (as Trial by Ice's, San d'Oria group 12).
// The trade fakes hold one Mini Tuning Fork or none, so they read the same under a `>=` or an exact HasItemQty.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunOtherAreasGroup11()
    {
        Console.WriteLine("== quest rework B3b Other Areas group 11: the call-mode reply kinds, Lacia's trade that " +
                          "answers nothing, 10019's option 0 / 7 ==");
        OA11TrialByLightning();
        OA11TrialSize();
    }

    // states 0 (available, fame 6) and 1 (completed, the date behind) start it, 2 (accepted) holds the three arms
    static void OA11TrialByLightning()
    {
        var def = new QC.OtherAreas.TrialByLightning().Compile();
        string npc = QC.Npc.Mhaura.Ripapa;
        int fork = QC.KeyItem.TuningForkOfLightning, whisper = QC.KeyItem.WhisperOfStorms;
        Fake Accepted(bool hasFork, bool hasWhisper)
        {
            var f = new Fake { Status = QuestStatus.Accepted };
            if (hasFork) f.KeyItems[fork] = true;
            if (hasWhisper) f.KeyItems[whisper] = true;
            return f;
        }
        var any = new Fake { Status = QuestStatus.Available };
        string r0 = OLReply(OLHandler(def, 0, QuestHandlerKind.OnTrigger, entity: npc), any);
        string r1 = OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrigger, entity: npc), any);
        var acc = OLHandler(def, 2, QuestHandlerKind.OnTrigger, entity: npc);
        string r2 = OLReply(acc, Accepted(false, false)), r3 = OLReply(acc, Accepted(true, false));
        string r4 = OLReply(acc, Accepted(true, true));
        Console.WriteLine($"  Trial by Lightning: {r0} | {r1} | {r2} | {r3} | {r4}");
        Check("oa g11:Trial by Lightning's replies are events (call mode): 10016 (available, completed), " +
              "10024, 10017, 10019",
              $"Event(10016,0,{fork})|Event(10016,0,{fork})|Event(10024,0,{fork})|Event(10017,0,{fork},5)|" +
              $"Event(10019,0,{fork},5,0,0)",
              $"{r0}|{r1}|{r2}|{r3}|{r4}");

        var h = def.States.SelectMany(s => s.Handlers)
                          .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Id == 10019);
        string Finish(int option, int freeSlots)
        {
            var f = new Fake { Status = QuestStatus.Accepted, Csid = 10019, Option = option, FreeSlots = freeSlots,
                               NextJstMidnight = 1800025200 };
            h.Run(f);
            return string.Join(";", f.Calls);
        }
        bool Done(string calls) =>
            calls.StartsWith("Player.AddItem(0,1);Text.Special(") &&
            calls.Contains("ITEM_OBTAINED,0);Player.AddTitle(") && calls.Contains($"Player.DelKeyItem({whisper})") &&
            calls.Contains("Player.SetCharVar(TrialByLightning_date,1800025200,0)") &&
            calls.Contains($"Player.AddFame({QC.FameArea.Windurst},60)") &&
            calls.EndsWith("Player.CompleteQuest(OtherAreas,TRIAL_BY_LIGHTNING)") &&
            !calls.Contains("Player.GiveCurrency") && !calls.Contains("AddSpell");
        string a0 = Finish(0, 5), a7 = Finish(7, 5);
        Console.WriteLine($"  10019 option 0: {a0}");
        Console.WriteLine($"  10019 option 7: {a7}");
        Check("oa g11:Trial by Lightning 10019 option 0 with a free slot completes with no reward (the engine's " +
              "addItem(0) warns and returns nil)", "ok", Done(a0) ? "ok" : a0);
        Check("oa g11:Trial by Lightning 10019 option 7 with a free slot completes with no reward", "ok",
              Done(a7) ? "ok" : a7);

        // user ruling 2026-11-04 (kept Lua bugs): the free-slot test is `option ~= 5 and option ~= 6`, so with no free
        // slot the gil and the spell still complete, while an item option is refused
        string full5 = Finish(5, 0), full6 = Finish(6, 0), full1 = Finish(1, 0);
        bool Completes(string calls) => calls.EndsWith("Player.CompleteQuest(OtherAreas,TRIAL_BY_LIGHTNING)") &&
                                        !calls.Contains("ITEM_CANNOT_BE_OBTAINED");
        Check("oa g11:Trial by Lightning 10019 with no free slot: options 5 (gil) and 6 (the spell) complete, " +
              "option 1 is refused (user ruling 2026-11-04)", "gil|spell|refused",
              (Completes(full5) && full5.Contains("Player.GiveCurrency(gil,10000)") ? "gil" : full5) + "|" +
              (Completes(full6) && full6.Contains("AddSpell") ? "spell" : full6) + "|" +
              (full1.Contains("ITEM_CANNOT_BE_OBTAINED") && !full1.Contains("CompleteQuest") ? "refused" : full1));
    }

    // states 0 (available, the guard), 1 (accepted: the trade and the talk), 2 (completed)
    static void OA11TrialSize()
    {
        var def = new QC.OtherAreas.TrialSizeTrialByLightning().Compile();
        string npc = QC.Npc.Mhaura.Lacia;
        int fork = QC.Item.MiniTuningForkOfLightning;
        var any = new Fake { Status = QuestStatus.Available, MainJob = QC.Job.Smn };
        var smnFork = new Fake { Status = QuestStatus.Accepted, MainJob = QC.Job.Smn };
        smnFork.TradeItems[fork] = 1;
        var warFork = new Fake { Status = QuestStatus.Accepted, MainJob = QC.Job.War };
        warFork.TradeItems[fork] = 1;
        var smnChip = new Fake { Status = QuestStatus.Accepted, MainJob = QC.Job.Smn };
        smnChip.TradeItems[QC.Item.BoneChip] = 1;
        var holding = new Fake { Status = QuestStatus.Accepted };
        holding.Items.Add(fork);
        var lost = new Fake { Status = QuestStatus.Accepted };
        var trade = OLHandler(def, 1, QuestHandlerKind.OnTrade, entity: npc);
        var talk = OLHandler(def, 1, QuestHandlerKind.OnTrigger, entity: npc);
        string r0 = OLReply(OLHandler(def, 0, QuestHandlerKind.OnTrigger, entity: npc), any);
        string r1 = OLReply(trade, smnFork), r2 = OLReply(talk, holding), r3 = OLReply(talk, lost);
        string r4 = OLReply(OLHandler(def, 2, QuestHandlerKind.OnTrigger, entity: npc), any);
        Console.WriteLine($"  Trial-Size Trial by Lightning: {r0} | {r1} | {r2} | {r3} | {r4}");
        Check("oa g11:Trial-Size Trial by Lightning's replies are events (call mode): 10025, the trade's 10026, " +
              "10018, 10029, 10028",
              $"Event(10025,0,{fork},5,20)|Event(10026,0,{fork},5,20)|Event(10018)|Event(10029,0,{fork},5,20)|" +
              "Event(10028)",
              $"{r0}|{r1}|{r2}|{r3}|{r4}");
        Check("oa g11:Trial-Size Trial by Lightning's trade answers nothing (null: the script ends) for a " +
              "non-Summoner and for another item", "null|null", $"{OLReply(trade, warFork)}|{OLReply(trade, smnChip)}");
    }
}
