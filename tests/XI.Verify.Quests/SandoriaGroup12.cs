// XI.Verify.Quests -- quest rework B3b San d'Oria group 12, Trial by Ice: Gulmama's 709 finish with option 0 or above 6
// and a free slot. The Lua calls player:addItem(0); the engine (C++ lua_base_entity.cpp:4564, C# ZoneHost.Progress.cs:656)
// logs a warning and returns nil, it raises nothing, so the finish carries on and completes with no reward. The golden
// recorder refuses any trace with a "lua:" line (Harness.cs ScriptErrors), so no trace can record this arm; this check does.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunSandoriaGroup12()
    {
        Console.WriteLine("== quest rework B3b San d'Oria group 12: Trial by Ice 709 option 0 / above 6 ==");
        var def = new QC.Sandoria.TrialByIce().Compile();
        var h = def.States.SelectMany(s => s.Handlers)
                         .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Id == 709);
        string Finish(int option, int freeSlots)
        {
            var f = new Fake { Status = QuestStatus.Accepted, Csid = 709, Option = option, FreeSlots = freeSlots, NextJstMidnight = 1800025200 };
            h.Run(f);
            return string.Join(";", f.Calls);
        }
        string a0 = Finish(0, 5), a7 = Finish(7, 5);
        Console.WriteLine("  option 0: " + a0);
        Console.WriteLine("  option 7: " + a7);
        Check("sandoria g12:709 option 0 completes with no reward (the engine's addItem(0) warns and returns nil)", "ok",
              Completes(a0) ? "ok" : a0);
        Check("sandoria g12:709 option 7 completes with no reward", "ok", Completes(a7) ? "ok" : a7);
        Check("sandoria g12:709 option 0 with no free slot is refused (ITEM_CANNOT_BE_OBTAINED 0), not completed", "ok",
              Finish(0, 0).Contains("ITEM_CANNOT_BE_OBTAINED,0") && !Finish(0, 0).Contains("CompleteQuest")
                  ? "ok" : Finish(0, 0));
        // user ruling 2026-11-04 (kept Lua bugs): the free-slot test is `option ~= 5 and option ~= 6`, so with no free
        // slot the gil and the spell still complete, while an item option is refused
        string NoSlot(int option)
        {
            string calls = Finish(option, 0);
            bool done = calls.Contains("Player.CompleteQuest(") && !calls.Contains("ITEM_CANNOT_BE_OBTAINED");
            return option == 1 ? (calls.Contains("ITEM_CANNOT_BE_OBTAINED") && !done ? "refused" : calls)
                               : (done ? "completes" : calls);
        }
        Check("sandoria g12:709 with no free slot: options 5 and 6 complete, option 1 is refused (user ruling " +
              "2026-11-04)", "completes|completes|refused", $"{NoSlot(5)}|{NoSlot(6)}|{NoSlot(1)}");
    }

    // the whole finish ran: the title, the whisper taken, the date set, fame +60, the raw completeQuest (the item-0
    // call is recorded: Player.AddItem(0,1), the engine ignores it)
    static bool Completes(string calls) =>
        calls.Contains("Player.AddItem(0,1);Text.Special(231:ITEM_OBTAINED,0);Player.AddTitle(") && calls.Contains("Player.DelKeyItem(") &&
        calls.Contains("Player.SetCharVar(TrialByIce_date,1800025200,0)") && calls.Contains("Player.AddFame(0,60)") &&
        calls.EndsWith("Player.CompleteQuest(Sandoria,TRIAL_BY_ICE)") && !calls.Contains("Player.GiveCurrency") &&
        !calls.Contains("AddSpell");
}
