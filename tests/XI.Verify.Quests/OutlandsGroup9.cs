// XI.Verify.Quests -- quest rework B3b Outlands group 9 (the Trials: Trial by Water, Trial by Wind, Trial-Size Trial by
// Water / Wind / Fire; call-only): what the golden traces cannot show.
// - The reply kinds. The five quests are called from their NPCs' scripts (xi.cs.quest), and an NPC call renders an Event
//   and a ProgressEvent alike, as player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply): there is no lookup action,
//   so no priority to tell them apart, and every trace replays the same with either kind. The Lua scripts call
//   player:startEvent, so each reply is pinned here as Event.
// - The trade that leaves the rest to the script: Verctissa's, Rahi Fohlatti's and Dodmos's accepted trade answers
//   nothing (null) for a non-Summoner or another item. Their onTrade has nothing after the call and ignores its answer,
//   so NoAction would look the same in a trace. (Edal-Tahdal's and Agado-Pugado's onTrigger do read the answer: 113 / 70
//   follow a false one, so the traces pin theirs.)
// - 112's / 69's finish with option 0 or above 6 and a free slot. The Lua calls player:addItem(0); the engine (C++
//   lua_base_entity.cpp, C# ZoneHost.Progress.cs) logs a warning and returns nil, it raises nothing, so the finish carries
//   on and completes with no reward. The golden recorder refuses any trace with a "lua:" line (Harness.cs ScriptErrors),
//   so no trace can record this arm (as Trial by Ice's, San d'Oria group 12).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunOutlandsGroup9()
    {
        Console.WriteLine("== quest rework B3b Outlands group 9: the call-mode reply kinds, the reward finish's option 0 / 7 ==");
        OL9Trial("Trial by Water", new QC.Outlands.TrialByWater().Compile(), QC.Npc.Norg.EdalTahdal, QC.KeyItem.TuningForkOfWater,
                 QC.KeyItem.WhisperOfTides, 109, 190, 110, 112, "TrialByWater_date", $"Player.AddFame({QC.FameArea.Norg},60)");
        OL9Trial("Trial by Wind", new QC.Outlands.TrialByWind().Compile(), QC.Npc.Rabao.AgadoPugado, QC.KeyItem.TuningForkOfWind,
                 QC.KeyItem.WhisperOfGales, 66, 107, 67, 69, "TrialByWind_date",
                 $"Player.AddFame({QC.FameArea.Sandoria},30);Player.AddFame({QC.FameArea.Bastok},30)");
        OL9TrialSize("Trial-Size Trial by Water", new QC.Outlands.TrialSizeTrialByWater().Compile(), QC.Npc.Norg.Verctissa,
                     QC.Item.MiniTuningForkOfWater, 199, 200, 111, 203, 202);
        OL9TrialSize("Trial-Size Trial by Wind", new QC.Outlands.TrialSizeTrialByWind().Compile(), QC.Npc.Rabao.RahiFohlatti,
                     QC.Item.MiniTuningForkOfWind, 108, 109, 68, 112, 111);
        OL9TrialSize("Trial-Size Trial by Fire", new QC.Outlands.TrialSizeTrialByFire().Compile(), QC.Npc.Kazham.Dodmos,
                     QC.Item.MiniTuningForkOfFire, 286, 287, 272, 290, 289);
    }

    // the Trials: states 0 (available) and 1 (completed, the date behind) start it, 2 (accepted) holds the three arms
    static void OL9Trial(string name, QuestScriptDefinition def, string npc, int fork, int whisper, int start, int newFork,
                         int held, int reward, string date, string fame)
    {
        Fake Accepted(bool hasFork, bool hasWhisper)
        {
            var f = new Fake { Status = QuestStatus.Accepted };
            if (hasFork) f.KeyItems[fork] = true;
            if (hasWhisper) f.KeyItems[whisper] = true;
            return f;
        }
        var any = new Fake { Status = QuestStatus.Available };
        string Ev(int id) => $"Event({id},"; // the csid and its comma: 1090 does not pass for 109
        string r0 = OLReply(OLHandler(def, 0, QuestHandlerKind.OnTrigger, entity: npc), any);
        string r1 = OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrigger, entity: npc), any);
        var acc = OLHandler(def, 2, QuestHandlerKind.OnTrigger, entity: npc);
        string r2 = OLReply(acc, Accepted(false, false)), r3 = OLReply(acc, Accepted(true, false)), r4 = OLReply(acc, Accepted(true, true));
        Console.WriteLine($"  {name}: {r0} | {r1} | {r2} | {r3} | {r4}");
        Check($"outlands g9:{name}'s replies are events (call mode): {start} (available, completed), {newFork}, {held}, {reward}", "ok",
              r0.StartsWith(Ev(start)) && r1.StartsWith(Ev(start)) && r2.StartsWith(Ev(newFork)) && r3.StartsWith(Ev(held)) &&
              r4.StartsWith(Ev(reward)) ? "ok" : $"{r0}|{r1}|{r2}|{r3}|{r4}");

        var h = def.States.SelectMany(s => s.Handlers).Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Id == reward);
        string Finish(int option, int freeSlots)
        {
            var f = new Fake { Status = QuestStatus.Accepted, Csid = reward, Option = option, FreeSlots = freeSlots, NextJstMidnight = 1800025200 };
            h.Run(f);
            return string.Join(";", f.Calls);
        }
        bool Completes(string calls) =>
            calls.StartsWith("Player.AddItem(0,1);Text.Special(") && calls.Contains("ITEM_OBTAINED,0);Player.AddTitle(") &&
            calls.Contains($"Player.DelKeyItem({whisper})") && calls.Contains($"Player.SetCharVar({date},1800025200,0)") &&
            calls.Contains(fame) &&
            calls.EndsWith($"Player.CompleteQuest(Outlands,{name.ToUpperInvariant().Replace(' ', '_')})") &&
            !calls.Contains("Player.GiveCurrency") && !calls.Contains("AddSpell");
        string a0 = Finish(0, 5), a7 = Finish(7, 5);
        Console.WriteLine($"  {reward} option 0: {a0}");
        Console.WriteLine($"  {reward} option 7: {a7}");
        Check($"outlands g9:{name} {reward} option 0 with a free slot completes with no reward (the engine's addItem(0) warns and returns nil)",
              "ok", Completes(a0) ? "ok" : a0);
        Check($"outlands g9:{name} {reward} option 7 with a free slot completes with no reward", "ok", Completes(a7) ? "ok" : a7);
        // user ruling 2026-11-04 (kept Lua bugs): the free-slot test is `option ~= 5 and option ~= 6`, so with no free
        // slot the gil and the spell still complete, while an item option is refused
        string NoSlot(int option)
        {
            string calls = Finish(option, 0);
            bool done = calls.Contains("Player.CompleteQuest(") && !calls.Contains("ITEM_CANNOT_BE_OBTAINED");
            return option == 1 ? (calls.Contains("ITEM_CANNOT_BE_OBTAINED") && !done ? "refused" : calls)
                               : (done ? "completes" : calls);
        }
        Check($"outlands g9:{name} {reward} with no free slot: options 5 and 6 complete, option 1 is refused " +
              "(user ruling 2026-11-04)", "completes|completes|refused", $"{NoSlot(5)}|{NoSlot(6)}|{NoSlot(1)}");
    }

    // the trial-size quests: states 0 (available, the guard), 1 (accepted: the trade and the talk), 2 (completed)
    static void OL9TrialSize(string name, QuestScriptDefinition def, string npc, int fork, int offer, int teleport, int held,
                             int another, int done)
    {
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
        Console.WriteLine($"  {name}: {r0} | {r1} | {r2} | {r3} | {r4}");
        Check($"outlands g9:{name}'s replies are events (call mode): {offer}, the trade's {teleport}, {held}, {another}, {done}", "ok",
              r0.StartsWith($"Event({offer},") && r1.StartsWith($"Event({teleport},") && r2 == $"Event({held})" &&
              r3.StartsWith($"Event({another},") && r4 == $"Event({done})" ? "ok" : $"{r0}|{r1}|{r2}|{r3}|{r4}");
        Check($"outlands g9:{name}'s trade answers nothing (null: the script ends) for a non-Summoner and for another item",
              "null|null", $"{OLReply(trade, warFork)}|{OLReply(trade, smnChip)}");
    }
}
