using System;
using System.Linq;
using XI.Quests;
using XI.QuestContent;
using XI.Quests.Authoring;

// carry-harness-2 (the user's ruling of 2026-11-04): the bridge's double-finish tally, on a real event end (0x05B
// through the zone host's OnEventFinish). A probe quest answers Samia (Bastok Markets) with 900 while the player's
// local var CH2 is 1 or more. Its finish 900 is in two states: CH2 1 or more, and CH2 2. At CH2 2 both run in the one
// dispatch and the bridge logs "quests: <key>: one finish of 900 ran 2 handlers (...)"; at CH2 1 one runs and nothing
// is logged. At CH2 3 the first state's 900 opens a dispatch of its own inside the real one
// (ZoneHost.FinishDispatchForTest) and runs the lookup's finish 901 in it, which a third state (CH2 3) answers: one
// handler in each dispatch, so nothing is logged (a shared tally would count two in the outer one). Last, the lookup's
// 900 run with no dispatch open: both handlers run, nothing is tallied.
internal static partial class Program
{
    private static void RunCarryHarness2()
    {
        Console.WriteLine("carry-harness-2: the double-finish tally on a real event end");
        uint id = CharIds[0];
        zone.TestQuestScripts.Add(new CH2Quest());
        Lua("InteractionGlobal.reload(true)");
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        try
        {
            static bool Tally(string l) => l.Contains(": one finish of ", StringComparison.Ordinal);
            // the char's local var, Samia talked to and her 900 finished: the handlers run, the tally's lines
            string Run(int ch2)
            {
                Lua($"player:setLocalVar('CH2', {ch2})");
                CH2Quest.Runs = 0;
                int mark = log.Count;
                TalkTo("Samia");
                bool open = c.IsInEvent && c.CurrentEvent.EventId == CH2Quest.Event;
                Finish();
                return $"{open}|{CH2Quest.Runs}|{string.Join(";", log.Skip(mark).Where(Tally).Select(Untimed))}";
            }
            const string Key = FixtureKey;
            Check("ch2:two finish handlers of one quest in one dispatch: the bridge's line",
                  $"True|2|quests: {Key}: one finish of 900 ran 2 handlers (1/-/onEventFinish:900, " +
                  "2/-/onEventFinish:900)", Run(2));
            Check("ch2:one finish handler in the dispatch: no line", "True|1|", Run(1));
            Check("ch2:a dispatch nested in the real one counts its own handlers: one in each, no line", "True|2|",
                  Run(3));
            Lua("player:setLocalVar('CH2', 2)");
            CH2Quest.Runs = 0;
            int at = log.Count;
            Lua($"InteractionGlobal.onEventFinish(player, {CH2Quest.Event}, 0, GetNPCByID({NpcId("Samia")}), " +
                "function() end)");
            Check("ch2:no dispatch open (both closed, the outer one put back): two handlers run, no line", "2|",
                  $"{CH2Quest.Runs}|{string.Join(";", log.Skip(at).Where(Tally))}");
        }
        finally
        {
            Lua("player:setLocalVar('CH2', 0)");
            Logout();
            Cleanup(db, id);
            zone.TestQuestScripts.Clear();
            Lua("InteractionGlobal.reload(true)");
        }
    }

    private sealed class CH2Quest : QuestScript
    {
        public const int Event = 900, Inner = 901;
        public static int Runs;

        public CH2Quest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q => q.Player.LocalVar("CH2") >= 1)
                .Npc(Zone.BastokMarkets, "Samia").OnTrigger(q => QuestReply.ProgressEvent(Event))
                .OnEventFinish(Zone.BastokMarkets, Event, q =>
                {
                    Runs++;
                    if (q.Player.LocalVar("CH2") != 3) return;
                    // a dispatch of its own, inside the real one: the lookup's finish 901 run in it
                    var samia = q.Npc.Id;
                    zone.FinishDispatchForTest(Inner, () =>
                        Lua($"InteractionGlobal.onEventFinish(player, {Inner}, 0, GetNPCByID({samia}), " +
                            "function() end)"));
                });
            b.AnyState(when: q => q.Player.LocalVar("CH2") == 2)
                .OnEventFinish(Zone.BastokMarkets, Event, q => { Runs++; });
            b.AnyState(when: q => q.Player.LocalVar("CH2") == 3)
                .OnEventFinish(Zone.BastokMarkets, Inner, q => { Runs++; });
        }
    }
}
