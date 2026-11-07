// XI.Verify.Quests -- quest rework P10: deferred callbacks
// (docs/superpowers/specs/2026-10-28-quest-deferred-callbacks- design.md). The builder (b.Callback's handle, b.Export),
// the validator's rules on names and exports, a module's added states refused a callback, the fake
// (Timers, Listeners, FireTimers: the deadline strictly past, deadline order, arming order among equals, a self-arming
// chain),
// and Hitting the Marquisate's 56 option-1 arm. The labels (0/-/callback:<name>) and the waiver regex are in the golden
// self-test (SelfTestP10.cs); the live side is QuestScripts.Live P10; the golden proof is probe.player_timer and
// windurst.hitting_the_marquisate#chandelier.
using System;
using System.Collections.Generic;
using System.Linq;
using XI.QuestContent;
using XI.Quests;
using XI.Quests.Authoring;

internal static partial class Program
{
    static void RunPlatformP10()
    {
        Console.WriteLine("== quest rework P10 ==");
        RunP10Builder();
        RunP10Validator();
        RunP10Modules();
        RunP10FakeTimers();
        RunP10FakeListeners();
        RunP10Marquisate();
    }

    static string P10Problems(Action<QuestScriptBuilder> build) =>
        string.Join(" | ", QuestScriptValidator.Validate(new BuiltQuest(build).Compile()));

    // the builder: a handle per callback, in declaration order on the definition; null name or body refused; exports
    // kept
    static void RunP10Builder()
    {
        QuestCallback a = null, c = null;
        var def = new BuiltQuest(b =>
        {
            a = b.Callback("stoneTimer", q => { });
            c = b.Callback("toWhitegate", q => { });
            b.Export("xi.test.onHealing", a);
            b.Export("xi.test.onEffectLose", c, "scripts/quests/test/Some_Module");
            b.State(QuestStatus.Accepted).OnEventFinish(50, 19, q => q.Player.Timer(1000, c));
        }).Compile();
        Check("p10 builder:b.Callback returns a handle with its name; the definition lists them in order", "stoneTimer,toWhitegate|True",
              $"{string.Join(",", def.Callbacks.Select(x => x.Name))}|{ReferenceEquals(def.Callbacks[0], a) && ReferenceEquals(def.Callbacks[1], c)}");
        Check("p10 builder:the exports: global, callback, module (null: the LegacyPath's)", "xi.test.onHealing/stoneTimer/-,xi.test.onEffectLose/toWhitegate/scripts/quests/test/Some_Module",
              string.Join(",", def.Exports.Select(e => $"{e.Global}/{e.Callback.Name}/{e.Module ?? "-"}")));
        Check("p10 builder:a callback is the quest's, in no state (the states hold handlers only)", "1",
              def.States.Sum(s => s.Handlers.Count).ToString());
        Check("p10 builder:a null name or body, or an export of nothing, is refused", "name|run|global|callback",
              $"{P5BuildThrows(b => b.Callback(null, q => { }))}|{P5BuildThrows(b => b.Callback("x", null))}|" +
              $"{P5BuildThrows(b => b.Export(null, b.Callback("x", q => { })))}|{P5BuildThrows(b => b.Export("xi.a", null))}");
        Check("p10 builder:a quest with no callbacks has empty lists (not null)", "0/0",
              $"{new BuiltQuest(b => b.State(QuestStatus.Accepted)).Compile().Callbacks.Count}/{new BuiltQuest(b => { }).Compile().Exports.Count}");
    }

    // the validator: names [A-Za-z][A-Za-z0-9_]* and once; exports a dotted Lua path, once, a callback of the quest's
    // own
    static void RunP10Validator()
    {
        Check("p10 validator:clean callbacks and exports validate clean", "",
              P10Problems(b =>
              {
                  var x = b.Callback("stoneTimer", q => { });
                  b.Callback("tebhi_Fade2", q => { });
                  b.Export("xi.dancesWithLuopans.onHealing", x);
              }));
        Check("p10 validator:a name declared twice is refused (once)", "callback stoneTimer declared twice",
              P10Problems(b =>
              {
                  b.Callback("stoneTimer", q => { });
                  b.Callback("stoneTimer", q => { });
                  b.Callback("stoneTimer", q => { });
              }));
        Check("p10 validator:a name not [A-Za-z][A-Za-z0-9_]* is refused (its label would not parse)",
              "callback '1x': a callback's name is [A-Za-z][A-Za-z0-9_]* (its label 0/-/callback:<name>) | " +
              "callback 'a-b': a callback's name is [A-Za-z][A-Za-z0-9_]* (its label 0/-/callback:<name>) | " +
              "callback '': a callback's name is [A-Za-z][A-Za-z0-9_]* (its label 0/-/callback:<name>)",
              P10Problems(b =>
              {
                  b.Callback("1x", q => { });
                  b.Callback("a-b", q => { });
                  b.Callback("", q => { });
              }));
        var other = new BuiltQuest(b => b.Callback("elsewhere", q => { })).Compile().Callbacks[0];
        Check("p10 validator:an export: a bad path, twice, another quest's callback, an empty module",
              "export 'xi..a': a Lua global's dotted path (xi.dancesWithLuopans.onHealing) | export xi.a declared twice | " +
              "export xi.b: its callback elsewhere is not one of the quest's (b.Callback) | export xi.c: an empty module name",
              P10Problems(b =>
              {
                  var x = b.Callback("x", q => { });
                  b.Export("xi..a", x);
                  b.Export("xi.a", x);
                  b.Export("xi.a", x);
                  b.Export("xi.b", other);
                  b.Export("xi.c", x, "");
              }));
    }

    // a module's added states declare no callback (they merge into the quest's states; the callbacks would be dropped)
    static void RunP10Modules()
    {
        var res = P9LeaveApply(m => m.Quest(P4Key).AddStates(b =>
        {
            b.Callback("added", q => { });
            b.State(QuestStatus.Completed).OnZoneIn(P4Zone, q => null);
        }));
        Check("p10 modules:added states with a callback are refused", "True",
              res.Problems.Any(p => p.EndsWith("added states declare no callback or export: the quest's own do")).ToString());
        var kept = QuestOverrides.Apply(new P10ModuleQuest().Compile(), new P4Module("p10_test_module", m => m.Quest(P4Key).State(1)
            .WrapOnTriggerAreaLeave(P4Zone, 1, (q, baseRun) =>
            {
                baseRun(q);
            })).Compile(new Fake()), P4Key, null, null);
        Check("p10 modules:a patched quest keeps its callbacks (the copy's)", "|tick",
              $"{string.Join(" | ", kept.Problems)}|{string.Join(",", kept.Definition?.Callbacks.Select(x => x.Name) ?? new string[0])}");
    }

    sealed class P10ModuleQuest : QuestScript
    {
        public P10ModuleQuest() : base(QuestLog.Jeuno, "P4_TEST") { }

        protected override void Build(QuestScriptBuilder b)
        {
            var tick = b.Callback("tick", q => { });
            b.State(QuestStatus.Accepted)
                .OnTriggerAreaLeave(P4Zone, 1, q => q.Player.Timer(10, tick));
        }
    }

    // the fake's timers: recorded and kept; fired on a tick strictly past the deadline
    // (CAIActionQueue::checkAction's tick > deadline), soonest first, arming order among equal deadlines; each run with
    // the call's arguments of a fresh context (its NPC as Npc, no Csid / Option / trigger area)
    static void RunP10FakeTimers()
    {
        var log = new List<string>();
        QuestCallback a = null, b2 = null, n = null;
        var def = new BuiltQuest(b =>
        {
            a = b.Callback("a", q => log.Add($"a:{q.Csid}/{q.Option}/{(q.Npc == null ? "-" : q.Npc.Id.ToString())}"));
            b2 = b.Callback("b", q => log.Add("b"));
            n = b.Callback("n", q => log.Add($"n:{q.Npc?.Id}"));
        }).Compile();
        var f = new Fake { Definition = def, Csid = 19, Option = 1 };
        f.Npc = new QuestNpcFake(17596831, f.Calls);
        f.Player.Timer(1000, a);
        f.Npc.Timer(500, n, WithPlayer.Yes);
        f.Player.Timer(1000, b2);
        f.Npc.Timer(1000, n);
        Check("p10 fake:armings recorded in Calls and kept in Timers (deadline NowMs + ms)",
              "Player.Timer(1000,a),Npc.Timer(500,n,WithPlayer),Player.Timer(1000,b),Npc.Timer(1000,n)|1000,500,1000,1000",
              $"{string.Join(",", f.Calls)}|{string.Join(",", f.Timers.Select(t => t.Deadline))}");
        int at500 = f.FireTimers(500);
        Check("p10 fake:a tick at the deadline fires nothing (not strictly past it)", "0/", $"{at500}/{string.Join(",", log)}");
        int at501 = f.FireTimers(501);
        Check("p10 fake:a tick past it fires it, with its NPC as Npc", "1/n:17596831", $"{at501}/{string.Join(",", log)}");
        log.Clear();
        int at1001 = f.FireTimers(1001);
        Check("p10 fake:three at one deadline fire in arming order; a player timer's context has no Npc, Csid or Option",
              "3/a:0/0/-,b,n:17596831", $"{at1001}/{string.Join(",", log)}");
        Check("p10 fake:the handler's arguments are back after the callbacks", "19/1/17596831", $"{f.Csid}/{f.Option}/{f.Npc.Id}");
        Check("p10 fake:fired timers are gone", "0", f.Timers.Count.ToString());
        log.Clear();
        f.NowMs = 0;
        f.Player.Timer(300, b2);
        f.Player.Timer(200, a);
        f.FireTimers(1000);
        Check("p10 fake:soonest deadline first, whatever the arming order", "a:0/0/-,b", string.Join(",", log));
        Check("p10 fake:a negative delay throws; a callback of another quest throws (Definition set)", "ms|not this quest's",
              $"{Throws(() => f.Player.Timer(-1, a))}|{Throws(() => f.Player.Timer(1, new BuiltQuest(x => x.Callback("z", q => { })).Compile().Callbacks[0]))}");
        Check("p10 fake:an NPC no context holds cannot arm", "no Timers", Throws(() => new QuestNpcFake(1).Timer(1, a)));

        // the char in a callback, as live: an NPC timer without WithPlayer has none (Player and Quest throw), with
        // it and on the char's own timer the fake is the char; no callback has the handler's trade
        QuestCallback who = null;
        var seen = new List<string>();
        string Try(Func<string> f)
        {
            try
            {
                return f();
            }
            catch (InvalidOperationException e)
            {
                return e.Message;
            }
        }
        var whoDef = new BuiltQuest(b => who = b.Callback("who", q =>
            seen.Add($"{Try(() => q.Player.Id.ToString())}/{Try(() => q.Quest.Var("x").ToString())}/" +
                     Try(() => q.Trade.ItemCount.ToString())))).Compile();
        var w = new Fake { Definition = whoDef, CharId = 7 };
        w.TradeItems[1] = 1;
        w.Npc = new QuestNpcFake(17596831, w.Calls);
        w.Npc.Timer(0, who);
        w.Npc.Timer(0, who, WithPlayer.Yes);
        w.Player.Timer(0, who);
        w.FireTimers(1);
        Check("p10 fake:a callback's char: none on an NPC timer without WithPlayer, the fake with it and on a player timer; " +
              "no trade in any; the handler's trade back after",
              "no player in this handler/no player in this handler/no trade in this handler|" +
              "7/0/no trade in this handler|7/0/no trade in this handler|1",
              $"{string.Join("|", seen)}|{w.Trade.ItemCount}");

        // Mimeo Stone's shape: a callback that re-arms itself (RandomInt * 1000) until its stop rule (3 fades)
        QuestCallback fade = null;
        var mimeo = new BuiltQuest(b =>
        {
            fade = b.Callback("stoneTimer", q =>
            {
                q.Quest.SetVar("NumFades", q.Quest.Var("NumFades") + 1);
                if (q.Quest.Var("NumFades") >= 3) return;
                q.Player.Timer(q.Player.RandomInt(50, 90) * 1000, fade);
            });
        }).Compile();
        var m = new Fake { Definition = mimeo };
        m.Rolls.Enqueue(60);
        m.Rolls.Enqueue(85);
        m.Player.Timer(70 * 1000, fade);
        var fired = new List<int>();
        for (long t = 1000; t <= 400_000; t += 1000)
            if (m.FireTimers(t) > 0) fired.Add((int)(t / 1000));
        Check("p10 fake:a self-arming chain follows its draws (70 s, then 60, then 85, each fired on the first tick past it; the stop rule at 3 fades)",
              "71,132,218|3|0", $"{string.Join(",", fired)}|{m.Var("NumFades")}|{m.Timers.Count}");
    }

    // the fake's listeners: TICK only, an identifier added again replaces the first, removed by itself while it runs
    // (it still ran this time, as the engine walks a copy)
    static void RunP10FakeListeners()
    {
        int heard = 0;
        QuestCallback respawn = null;
        var def = new BuiltQuest(b =>
        {
            respawn = b.Callback("tebhiRespawn", q =>
            {
                heard++;
                if (heard >= 2) q.Player.RemoveListener("TEBHI_RESPAWN");
            });
        }).Compile();
        var f = new Fake { Definition = def };
        f.Player.AddListener("TICK", "TEBHI_RESPAWN", respawn);
        f.Player.AddListener("TICK", "TEBHI_RESPAWN", respawn);
        Check("p10 fake listeners:recorded; one identifier added twice is one listener", "Player.AddListener(TICK,TEBHI_RESPAWN,tebhiRespawn),Player.AddListener(TICK,TEBHI_RESPAWN,tebhiRespawn)|TICK/TEBHI_RESPAWN/tebhiRespawn",
              $"{string.Join(",", f.Calls)}|{string.Join(",", f.Listeners)}");
        int first = f.FireListeners(), second = f.FireListeners(), third = f.FireListeners();
        Check("p10 fake listeners:heard each tick until it removed itself (the removing tick ran it)", "1/1/0|2|0", $"{first}/{second}/{third}|{heard}|{f.Listeners.Count}");
        Check("p10 fake listeners:an event other than TICK is refused", "TICK only", Throws(() => f.Player.AddListener("DEATH", "x", respawn)));
    }

    // Hitting the Marquisate's 56: option 1 arms chandelier on qm15 with the char (5000 ms); it pops the Chandelier
    // unclaimed, not hidden, and says THE_PRESENCE_MOVES + 5; option 0 says + 6 at once and arms nothing
    static void RunP10Marquisate()
    {
        var def = new XI.QuestContent.Windurst.HittingTheMarquisate().Compile();
        var finish = def.States[1].Handlers.Single(h => h.Kind == QuestHandlerKind.OnEventFinish && h.Id == 56);
        var f = new Fake { Definition = def, Option = 1 };
        f.TextIds[Text.GarlaigeCitadel.THE_PRESENCE_MOVES.ToString()] = 7324;
        f.Npc = new QuestNpcFake(17596831, f.Calls);
        finish.Run(f);
        Check("p10 marquisate:56 option 1 arms chandelier on qm15 with the char, 5000 ms; nothing else", "Npc.Timer(5000,chandelier,WithPlayer)",
              string.Join(",", f.Calls));
        f.Calls.Clear();
        f.FireTimers(5000);
        Check("p10 marquisate:at 5000 ms nothing (the deadline)", "", string.Join(",", f.Calls));
        f.FireTimers(5001);
        Check("p10 marquisate:past it: the Chandelier popped unclaimed, not hidden; THE_PRESENCE_MOVES + 5",
              $"Player.PopFromQm({Mob.GarlaigeCitadel.CHANDELIER},False,0),Text.Special(#7329)", string.Join(",", f.Calls));
        var o = new Fake { Definition = def, Option = 0 };
        o.TextIds[Text.GarlaigeCitadel.THE_PRESENCE_MOVES.ToString()] = 7324;
        o.Npc = new QuestNpcFake(17596831, o.Calls);
        finish.Run(o);
        Check("p10 marquisate:56 option 0: THE_PRESENCE_MOVES + 6 at once, no timer", "Text.Special(#7330)|0", $"{string.Join(",", o.Calls)}|{o.Timers.Count}");
        Check("p10 marquisate:its one callback, chandelier", "chandelier", string.Join(",", def.Callbacks.Select(x => x.Name)));
    }

    static string Throws(Action a)
    {
        try
        {
            a();
            return "no throw";
        }
        catch (ArgumentOutOfRangeException e)
        {
            return e.ParamName;
        }
        catch (Exception e)
        {
            string msg = e.Message;
            foreach (var key in new[] { "not this quest's", "no Timers", "TICK only" })
                if (msg.Contains(key)) return key;
            return msg;
        }
    }
}
