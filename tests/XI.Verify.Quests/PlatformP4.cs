// XI.Verify.Quests -- quest rework P4: the C# override hook for quest modules on the authoring layer, with the fake: a wrap
// that calls its base and one that does not, a replace, an add, a check wrapped and replaced, the reward / remove / add
// states overrides, the refusal of an override of a handler or state the quest does not have, xi.pre over the settings,
// modules/init.txt read by the C++ server's rules, QuestReply.WithArg, and The Gustaberg Tour's era module on the real
// quest. The registration's side (a refused module logged, the quest registered as it is) is in XI.Verify.QuestScripts.Live
// (PlatformP4.cs); the golden proof that a C# module matches its Lua module is XI.Verify.QuestGolden.Live's module flows.
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;

internal static partial class Program
{
    static void RunPlatformP4()
    {
        Console.WriteLine("== quest rework P4: quest modules (the override hook) ==");
        RunP4Wraps();
        RunP4ReplaceAdd();
        RunP4Checks();
        RunP4Refusals();
        RunP4StateOps();
        RunP4Copies();
        RunP4Pre();
        RunP4InitTxt();
        RunP4WithArg();
        RunP4Gustaberg();
    }

    const string P4Key = "jeuno.p4_test";
    const ushort P4Zone = 252;

    // states: 1 Available (Npc onTrigger 10, finish 10 begins), 2 Accepted step 1 (Npc onTrade sets Wait 5 and replies 11,
    // finish 11 sets step 2), 3 Accepted step 2 (Npc onTrigger 12), 4 Completed (Npc onTrigger 13)
    sealed class P4Quest : QuestScript
    {
        public P4Quest() : base(QuestLog.Jeuno, "P4_TEST") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Gil = 100 });
            b.Steps(1, 2);
            b.State(QuestStatus.Available)
                .Npc(P4Zone, "Npc").OnTrigger(QuestReply.Event(10))
                .OnEventFinish(P4Zone, 10, q => q.Quest.Begin());
            b.State(QuestStatus.Accepted, step: 1)
                .Npc(P4Zone, "Npc").OnTrade(q =>
                {
                    q.Quest.SetVar("Wait", 5);
                    return QuestReply.ProgressEvent(11);
                })
                .OnEventFinish(P4Zone, 11, q => q.Quest.SetStep(2));
            b.State(QuestStatus.Accepted, step: 2)
                .Npc(P4Zone, "Npc").OnTrigger(q => QuestReply.Event(12));
            b.State(QuestStatus.Completed)
                .Npc(P4Zone, "Npc").OnTrigger(QuestReply.Event(13).ReplaceDefault());
        }
    }

    sealed class P4Module : QuestModule
    {
        readonly Action<QuestModuleBuilder> _build;
        readonly Func<IQuestSettings, bool> _condition;

        public P4Module(string name, Action<QuestModuleBuilder> build, Func<IQuestSettings, bool> condition = null, string file = "modules/custom/lua/p4_test.lua")
            : base(name, file)
        {
            _build = build;
            _condition = condition;
        }

        public override bool Condition(IQuestSettings settings) => _condition?.Invoke(settings) ?? true;
        protected override void Build(QuestModuleBuilder m) => _build(m);
    }

    // the quest with the module applied (its problems when refused); the base runs and the check runs counted
    static QuestOverrideResult P4Apply(Action<QuestModuleBuilder> build, List<string> baseRuns = null, List<int> checkRuns = null) =>
        QuestOverrides.Apply(new P4Quest().Compile(), new P4Module("p4_test_module", build).Compile(new Fake()), P4Key,
            baseRuns == null ? null : h => baseRuns.Add($"{h.Kind}:{h.Entity ?? h.Id.ToString()}"), checkRuns == null ? null : checkRuns.Add);

    static QuestHandlerDecl P4Handler(QuestScriptDefinition def, int state, QuestHandlerKind kind, int id = 0) =>
        def.States[state - 1].Handlers.Single(h => h.Kind == kind && (h.Entity != null || h.Id == id));

    // a wrap gets the context and the handler it wraps, and calls it or not (Your Crystal Ball: base first, then the var;
    // Sin Hunting: the base only on a full moon)
    static void RunP4Wraps()
    {
        var baseRuns = new List<string>();
        var r = P4Apply(m => m.Quest(P4Key).State(2).Npc(P4Zone, "Npc").WrapOnTrade((q, baseTrade) =>
        {
            int previous = q.Quest.Var("Wait");
            var result = baseTrade(q);
            if (q.Quest.Var("Wait") != previous) q.Quest.SetVar("Wait", 99);
            return result;
        }), baseRuns);
        Check("p4 wrap:applied, no problems", "True/", $"{r.Definition != null}/{string.Join(" | ", r.Problems)}");
        var trade = P4Handler(r.Definition, 2, QuestHandlerKind.OnTrade);
        var f = new Fake { Status = QuestStatus.Accepted };
        var reply = trade.Reply(f);
        Check("p4 wrap:calling its base: the base's effect, then the module's, the base's reply", "Quest.SetVar(Wait,5,0),Quest.SetVar(Wait,99,0)|ProgressEvent(11)",
              $"{string.Join(",", f.Calls)}|{QuestContextFake.Describe(reply)}");
        Check("p4 wrap:  the handler is the module's (its kind, zone, entity kept)", "p4_test_module/OnTrade/252/Npc/False",
              $"{trade.Module?.Name}/{trade.Kind}/{trade.Zone}/{trade.Entity}/{trade.Called}");
        Check("p4 wrap:  the base's run reported once (the coverage hook: the quest's own label)", "OnTrade:Npc", string.Join(",", baseRuns));
        Check("p4 wrap:  the other handlers are the quest's own", "", string.Join(",", r.Definition.States.SelectMany(s => s.Handlers).Where(h => h != trade && h.Module != null).Select(h => h.Kind)));

        baseRuns.Clear();
        r = P4Apply(m => m.Quest(P4Key).State(2).Npc(P4Zone, "Npc").WrapOnTrade((q, baseTrade) => q.Player.MainLevel >= 50 ? baseTrade(q) : null), baseRuns);
        trade = P4Handler(r.Definition, 2, QuestHandlerKind.OnTrade);
        f = new Fake { Status = QuestStatus.Accepted, MainLevel = 10 };
        reply = trade.Reply(f);
        Check("p4 wrap:not calling its base: no base effect, its own reply (none)", "|null|", $"{string.Join(",", f.Calls)}|{(reply == null ? "null" : QuestContextFake.Describe(reply))}|{string.Join(",", baseRuns)}");
        f = new Fake { Status = QuestStatus.Accepted, MainLevel = 50 };
        Check("p4 wrap:  and calling it when it chooses", "ProgressEvent(11)|OnTrade:Npc", $"{QuestContextFake.Describe(trade.Reply(f))}|{string.Join(",", baseRuns)}");

        // an event finish (an Action): Better the Demon You Know's base, then the var
        r = P4Apply(m => m.Quest(P4Key).State(2).WrapOnEventFinish(P4Zone, 11, (q, baseFinish) =>
        {
            baseFinish(q);
            q.Quest.SetVar("Wait", 7);
        }));
        f = new Fake();
        P4Handler(r.Definition, 2, QuestHandlerKind.OnEventFinish, 11).Run(f);
        Check("p4 wrap:an event finish: base, then the module", "Quest.SetStep(2),Quest.SetVar(Wait,7,0)", string.Join(",", f.Calls));

        // a fixed reply wrapped: its base is that reply; two wraps of one handler: the second wraps the first (Moonlit Path's
        // change of the action its base returned)
        r = P4Apply(m => m.Quest(P4Key).State(4).Npc(P4Zone, "Npc")
            .WrapOnTrigger((q, b) => b(q).OncePerZone())
            .WrapOnTrigger((q, b) => b(q).SetPriority(5)));
        Check("p4 wrap:a fixed reply's base is the reply; a second wrap wraps the first", "Event(13):replaceDefault():oncePerZone():setPriority(5)",
              QuestContextFake.Describe(P4Handler(r.Definition, 4, QuestHandlerKind.OnTrigger).Reply(new Fake())));
    }

    static void RunP4ReplaceAdd()
    {
        var baseRuns = new List<string>();
        var r = P4Apply(m => m.Quest(P4Key).State(2).Npc(P4Zone, "Npc").ReplaceOnTrade(q => QuestReply.Event(77))
            .ReplaceOnEventFinish(P4Zone, 11, q => q.Quest.SetStep(9)), baseRuns);
        var f = new Fake();
        var reply = P4Handler(r.Definition, 2, QuestHandlerKind.OnTrade).Reply(f);
        P4Handler(r.Definition, 2, QuestHandlerKind.OnEventFinish, 11).Run(f);
        Check("p4 replace:the module's handler alone runs (no base)", "Event(77)|Quest.SetStep(9)|",
              $"{QuestContextFake.Describe(reply)}|{string.Join(",", f.Calls)}|{string.Join(",", baseRuns)}");

        r = P4Apply(m => m.Quest(P4Key).State(2).Npc(P4Zone, "Npc").AddOnTrigger(q => QuestReply.Event(27))
            .AddOnEventUpdate(P4Zone, 11, q => q.Player.UpdateEvent(1)));
        var added = r.Definition.States[1].Handlers.Where(h => h.Module != null).ToList();
        Check("p4 add:a handler where the quest has none: after the state's own, the module's", "OnTrigger:Npc:Event(27),OnEventUpdate:11",
              string.Join(",", added.Select(h => h.Kind + ":" + (h.Entity != null ? $"{h.Entity}:{QuestContextFake.Describe(h.Reply(new Fake()))}" : h.Id.ToString()))));
        f = new Fake();
        added[1].Run(f);
        Check("p4 add:  the added run handler runs", "Player.UpdateEvent(1)", string.Join(",", f.Calls));
    }

    // quest.sections[n].check: a wrap gets the state's own matches as its base; a replace ignores them (All in the Cards'
    // return false; Apocalypse Nigh's copy with the timer)
    static void RunP4Checks()
    {
        var runs = new List<int>();
        var r = P4Apply(m => m.Quest(P4Key)
            .State(2).WrapCheck((q, baseCheck) => baseCheck(q) && q.Quest.Var("Wait") == 0)
            .State(1).ReplaceCheck(q => false), checkRuns: runs);
        var s2 = r.Definition.States[1];
        var g = new Fake { Status = QuestStatus.Accepted, Step = 1 };
        string accepted = s2.IsMatch(QuestStatus.Accepted, 1, g).ToString();
        g.Vars["Wait"] = 5;
        string waiting = s2.IsMatch(QuestStatus.Accepted, 1, g).ToString();
        var h = new Fake { Status = QuestStatus.Available };
        string other = s2.IsMatch(QuestStatus.Available, 0, h).ToString();
        Check("p4 check:wrapped: the base (Accepted, step 1) and the module's condition", "True/False/False", $"{accepted}/{waiting}/{other}");
        Check("p4 check:  one match, any status (the bridge reads no step for it), the module's", "1/True/False/p4_test_module",
              $"{s2.Matches.Count}/{s2.Matches[0].AnyStatus}/{s2.Matches[0].Step.HasValue}/{s2.CheckModule?.Name}");
        Check("p4 check:replaced: false for its own status", "False", r.Definition.States[0].IsMatch(QuestStatus.Available, 0, new Fake { Status = QuestStatus.Available }).ToString());
        Check("p4 check:  each run reported with its state (the coverage hook)", "2,2,2,1", string.Join(",", runs));
        Check("p4 check:  the states it did not touch keep their matches", "Accepted/1|", $"{r.Definition.States[2].Matches[0].Status}/{r.Definition.States[2].Matches.Count}|{r.Definition.States[2].CheckModule}");
    }

    // an override of a handler or a state the quest does not have, or an add of one it has, is refused with the handler named
    // as its label is; the definition is then null
    static void RunP4Refusals()
    {
        string Refused(Action<QuestModuleBuilder> build)
        {
            var r = P4Apply(build);
            return r.Definition == null ? string.Join(" | ", r.Problems) : "applied";
        }
        Check("p4 refused:a wrap of a handler the quest does not have",
              "p4_test_module: jeuno.p4_test: has no handler 2/Npc/onTrigger in zone 252 to wrap",
              Refused(m => m.Quest(P4Key).State(2).Npc(P4Zone, "Npc").WrapOnTrigger((q, b) => b(q))));
        Check("p4 refused:  a replace of an NPC the quest does not name",
              "p4_test_module: jeuno.p4_test: has no handler 2/Nobody/onTrade in zone 252 to replace",
              Refused(m => m.Quest(P4Key).State(2).Npc(P4Zone, "Nobody").ReplaceOnTrade(q => null)));
        Check("p4 refused:  an event finish of another csid, or in another zone",
              "p4_test_module: jeuno.p4_test: has no handler 2/-/onEventFinish:12 in zone 252 to wrap | p4_test_module: jeuno.p4_test: has no handler 2/-/onEventFinish:11 in zone 1 to replace",
              Refused(m => m.Quest(P4Key).State(2).WrapOnEventFinish(P4Zone, 12, (q, b) => b(q)).ReplaceOnEventFinish(1, 11, q => { })));
        Check("p4 refused:  a state the quest does not have",
              "p4_test_module: jeuno.p4_test: has no state 9 (it has 4) for 9/Npc/onTrigger in zone 252 | p4_test_module: jeuno.p4_test: has no state 5 (it has 4) for its check",
              Refused(m => m.Quest(P4Key).State(9).Npc(P4Zone, "Npc").ReplaceOnTrigger(q => null).State(5).ReplaceCheck(q => true)));
        Check("p4 refused:  an add of a handler it has",
              "p4_test_module: jeuno.p4_test: has a handler 1/Npc/onTrigger in zone 252 already: wrap or replace it",
              Refused(m => m.Quest(P4Key).State(1).Npc(P4Zone, "Npc").AddOnTrigger(q => null)));
        var def = new P4Quest().Compile();
        var other = new P4Module("p4_other", m => m.Quest("jeuno.other").State(1).ReplaceCheck(q => true)).Compile(new Fake());
        Check("p4 refused:  a module with no overrides for the key leaves the quest as it is", "True",
              ReferenceEquals(def, QuestOverrides.Apply(def, other, P4Key).Definition).ToString());
        Check("p4 refused:a module's name and file are checked (Module:new: 5 characters or more; modules/<path>.lua)", "ArgumentException/ArgumentException/ArgumentException",
              $"{P2Refused(() => new P4Module("abc", m => { }))}/{P2Refused(() => new P4Module("p4_test_module", m => { }, file: "scripts/x.lua"))}/" +
              $"{P2Refused(() => new P4Module("p4_test_module", m => { }, file: "modules/era/../x.lua"))}");
    }

    // quest.reward = { ... }, table.remove(quest.sections, n), table.insert(quest.sections, { ... }) (Northward, A Hard Day's
    // Knight, the Gobbiebag quests' extra section); the validator runs on the patched script
    static void RunP4StateOps()
    {
        var r = P4Apply(m => m.Quest(P4Key)
            .Reward(new QuestReward { KeyItem = 1, Title = 2 })
            .RemoveState(3)
            .AddStates(b => b.State(QuestStatus.Available, when: q => q.Player.FameLevel(4) < 3).Npc(P4Zone, "Bluffnix").OnTrigger(q => QuestReply.Event(42))));
        var d = r.Definition;
        Check("p4 states:the reward replaced", "/1/2", $"{d.Reward.Gil}/{d.Reward.KeyItem}/{d.Reward.Title}");
        Check("p4 states:  the reward is the module's (its coverage: reached when the quest completes with it); the quest's own is no module's",
              "p4_test_module/null", $"{d.RewardModule?.Name}/{new P4Quest().Compile().RewardModule?.Name ?? "null"}");
        Check("p4 states:the removed state is the module's, its matches from before kept (its coverage: reached when a check of the quest runs while they hold)",
              "p4_test_module/True/False/False",
              $"{d.States[2].RemovedBy?.Name}/{d.States[2].RemovedWouldMatch(QuestStatus.Accepted, 2, new Fake())}/" +
              $"{d.States[2].RemovedWouldMatch(QuestStatus.Accepted, 1, new Fake())}/{d.States[1].RemovedWouldMatch(QuestStatus.Accepted, 1, new Fake())}");
        Check("p4 states:a removed state keeps its number, has no handler and never matches; the next keep theirs", "5|0/False|Completed",
              $"{d.States.Count}|{d.States[2].Handlers.Count}/{d.States[2].IsMatch(QuestStatus.Accepted, 2, new Fake())}|{d.States[3].Matches[0].Status}");
        Check("p4 states:an added state after the quest's, its handlers the module's", "Available|Bluffnix/p4_test_module|Event(42)",
              $"{d.States[4].Matches[0].Status}|{d.States[4].Handlers[0].Entity}/{d.States[4].Handlers[0].Module?.Name}|{QuestContextFake.Describe(d.States[4].Handlers[0].Reply(new Fake()))}");
        Check("p4 states:  an override of a removed state is refused",
              "p4_test_module: jeuno.p4_test: state 3 is removed for its check",
              string.Join(" | ", P4Apply(m => m.Quest(P4Key).RemoveState(3).State(3).ReplaceCheck(q => true)).Problems));
        Check("p4 states:  added states set no reward", "p4_test_module: jeuno.p4_test: added states set no reward: use Reward",
              string.Join(" | ", P4Apply(m => m.Quest(P4Key).AddStates(b => b.Reward(new QuestReward { Gil = 1 }))).Problems));
        var bad = P4Apply(m => m.Quest(P4Key).AddStates(b => b.State(QuestStatus.Accepted, step: 7).Npc(P4Zone, "Npc").OnTrigger(q => null)));
        Check("p4 states:the validator on the patched script: an added state's step never set", "step 7 is matched but never set",
              string.Join(" | ", QuestScriptValidator.Validate(bad.Definition)));
    }

    // Apply copies the quest's own declarations (MemberwiseClone: QuestScriptDefinition.With, QuestStateDecl.With,
    // QuestHandlerDecl.WithBehaviour), so a member a port branch adds later is carried: every public property the override
    // does not change is the same in the patched definition, read by reflection (a new one is checked without a change
    // here). Every handler kind has a trigger name and can be added
    static void RunP4Copies()
    {
        var own = new P4Quest().Compile();
        var r = P4Apply(m => m.Quest(P4Key).State(2).Npc(P4Zone, "Npc").WrapOnTrade((q, b) => b(q)));
        var d = r.Definition;
        string Differs(object a, object b, params string[] changed) =>
            string.Join(",", a.GetType().GetProperties().Where(p => p.GetIndexParameters().Length == 0 && !changed.Contains(p.Name))
                .Where(p => !Equals(p.GetValue(a), p.GetValue(b))).Select(p => p.Name));
        Check("p4 copies:the definition: only the reward, the states and the steps are new (the rest the quest's own)", "",
              Differs(own, d, "States", "DeclaredSteps", "Reward"));
        Check("p4 copies:  a state the module does not touch: every member its own", "",
              Differs(own.States[0], d.States[0], "Matches", "Handlers"));
        var before = own.States[1].Handlers.Single(h => h.Kind == QuestHandlerKind.OnTrade);
        var after = d.States[1].Handlers.Single(h => h.Kind == QuestHandlerKind.OnTrade);
        Check("p4 copies:  the wrapped handler: only its behaviour and module are new", "", Differs(before, after, "Reply", "Run", "Fixed", "Module"));
        var reward = new QuestReward { Items = new[] { 1, 2 }, ItemParams = new QuestItemParams { FromTrade = true } };
        var copied = P4Apply(m => m.Quest(P4Key).Reward(reward)).Definition.Reward;
        reward.Items[0] = 9;
        reward.ItemParams.FromTrade = false;
        Check("p4 copies:a module's reward is a deep copy (the list and the params its own)", "1,2/True",
              $"{string.Join(",", copied.Items)}/{copied.ItemParams.FromTrade}");
        Check("p4 copies:every handler kind has a trigger name", "onTrigger,onTrade,onMobDeath,onEventFinish:1,onEventUpdate:1,onZoneIn,onTriggerAreaEnter:1,afterZoneIn,onZoneOut,onTriggerAreaLeave:1",
              string.Join(",", Enum.GetValues<QuestHandlerKind>().Select(k => QuestOverrides.Trigger(k, 1))));
        // a kind whose rule KindProblem does not know throws there: a failed check, not a crashed run
        string addedResult;
        try
        {
            var added = P4Apply(m => m.Quest(P4Key).State(2)
                .Npc(P4Zone, "Other").AddOnTrigger(q => null).AddOnTrade(q => null).AddOnMobDeath(q => { })
                .AddOnEventFinish(P4Zone, 99, q => { }).AddOnEventUpdate(P4Zone, 99, q => { }).AddOnZoneIn(P4Zone, q => null)
                .AddOnTriggerAreaEnter(P4Zone, 3, q => { }).AddOnAfterZoneIn(P4Zone, q => null).AddOnZoneOut(P4Zone, q => { })
                .AddOnTriggerAreaLeave(P4Zone, 3, q => { }));
            addedResult = $"{string.Join(" | ", added.Problems)}|{added.Definition?.States[1].Handlers.Count(h => h.Module != null)}";
        }
        catch (Exception e)
        {
            addedResult = $"threw {e.GetType().Name}: {e.Message}";
        }
        Check("p4 copies:  and can be added (each kind's rule known)", $"|{Enum.GetValues<QuestHandlerKind>().Length}", addedResult);
        Check("p4 copies:  a kind no rule names is refused, not taken as some other", "ArgumentOutOfRangeException",
              P2Refused(() => QuestOverrides.Trigger((QuestHandlerKind)200, 1)));
    }

    // xi.pre(expansion): restricted (RESTRICT_CONTENT 1) and the expansion's ENABLE_<tag> not 1
    static void RunP4Pre()
    {
        string Pre(params (string Name, int Value)[] settings)
        {
            var f = new Fake();
            foreach (var (name, value) in settings) f.MainSettings[name] = value;
            return QuestModule.Pre(f, QuestExpansion.Rov).ToString();
        }
        Check("p4 pre:not restricted: false (the defaults)", "False|False", $"{Pre()}|{Pre(("ENABLE_ROV", 0))}");
        Check("p4 pre:restricted, the expansion on: false", "False", Pre(("RESTRICT_CONTENT", 1), ("ENABLE_ROV", 1)));
        Check("p4 pre:restricted, the expansion off or unset: true", "True|True", $"{Pre(("RESTRICT_CONTENT", 1), ("ENABLE_ROV", 0))}|{Pre(("RESTRICT_CONTENT", 1))}");
        Check("p4 pre:a RESTRICT_CONTENT that is not 1 is not restricted (Lua: == 1 or == true)", "False", Pre(("RESTRICT_CONTENT", 2), ("ENABLE_ROV", 0)));
        Check("p4 pre:the tags are the ENABLE_<tag> settings' (xi.expansion's names)", "ROTZ,COP,TOAU,WOTG,ACP,AMK,ASA,ABYSSEA,VOIDWATCH,SOA,ROV,TVR",
              string.Join(",", Enum.GetValues<QuestExpansion>().Select(QuestModule.Tag)));
        Check("p4 pre:not an xi.expansion value", "ArgumentOutOfRangeException", P2Refused(() => QuestModule.Pre(new Fake(), (QuestExpansion)13)));
        bool seen = false;
        new P4Module("p4_pre_probe", m =>
        {
            seen = m.Pre(QuestExpansion.Soa);
            m.Quest(P4Key).State(1).ReplaceCheck(q => true);
        }).Compile(new Fake { MainSettings = { ["RESTRICT_CONTENT"] = 1 } });
        Check("p4 pre:the builder's Pre reads the settings it compiles with", "True", seen.ToString());
    }

    // modules/init.txt by moduleutils.cpp's LoadLuaModules: a file or any folder above it enables a module; '#' at the
    // start of a line only; trailing whitespace trimmed, leading kept; a line of whitespace is the modules folder itself
    static void RunP4InitTxt()
    {
        const string file = "modules/era/lua/quests/bastok/the_gustaberg_tour.lua";
        string On(string text) => QuestModuleList.Entries(text).Any(e => QuestModuleList.Enables(e, file)).ToString();
        Check("p4 init:the file itself, a folder above it, with or without a trailing '/'", "True|True|True|True|True",
              $"{On("era/lua/quests/bastok/the_gustaberg_tour.lua")}|{On("era")}|{On("era/")}|{On("era/lua/quests")}|{On("era/lua/quests/bastok/")}");
        Check("p4 init:another file, a folder beside it, a name that only starts the same: off", "False|False|False|False",
              $"{On("era/lua/quests/bastok/eco_warrior.lua")}|{On("custom")}|{On("era/lua/quests/bastok/the_gustaberg")}|{On("er")}");
        Check("p4 init:nothing listed, an empty line, a comment (also of the file): off", "False|False|False|False",
              $"{On("")}|{On("\n\r\n")}|{On("# era")}|{On("#era/lua/quests/bastok/the_gustaberg_tour.lua")}");
        Check("p4 init:'#' after a space is no comment: the entry names no file", "False", On(" # era"));
        Check("p4 init:trailing spaces, tabs and CR trimmed (CRLF lines)", "True|True", $"{On("era   \t")}|{On("custom\r\nera\r\n")}");
        Check("p4 init:a leading space is kept: no such file", "False", On("  era"));
        Check("p4 init:a line of whitespace alone is ./modules/: every module (the C++ trims it to the folder)", "True|True", $"{On("   ")}|{On("\t")}");
        Check("p4 init:./, doubled and trailing separators, .. and \\ folded as the file system does", "True|True|True|True",
              $"{On("./era")}|{On("era//lua/")}|{On("custom/../era")}|{On("era\\lua")}");
        Check("p4 init:out of modules/ and back in is the same folder; out to another, or above the csharp folder, enables nothing", "True|False|False",
              $"{On("../modules/era")}|{On("../scripts")}|{On("../../modules/era")}");
        Check("p4 init:names compare as the file system does: without case on Windows (the C++ server finds ./modules/Era/ there), with it elsewhere",
              $"True|False|{OperatingSystem.IsWindows()}|{OperatingSystem.IsWindows()}",
              $"{QuestModuleList.Enables("./modules/Era/Lua", file, ignoreCase: true)}|{QuestModuleList.Enables("./modules/Era/Lua", file, ignoreCase: false)}|" +
              $"{QuestModuleList.IgnoreCase}|{On("Era")}");
        Check("p4 init:  the load order by the same rule", "modules/era/lua/a.lua|",
              $"{string.Join(",", QuestModuleList.LoadOrder(QuestModuleList.Entries("ERA/lua/a.lua"), new[] { "modules/era/lua/a.lua" }, ignoreCase: true))}|" +
              $"{string.Join(",", QuestModuleList.LoadOrder(QuestModuleList.Entries("ERA/lua/a.lua"), new[] { "modules/era/lua/a.lua" }, ignoreCase: false))}");
        Check("p4 init:the entries as LoadLuaModules builds them", "./modules/era|./modules/custom/lua/x.lua|./modules/",
              string.Join("|", QuestModuleList.Entries("# comment\nera  \r\n\ncustom/lua/x.lua\n  \n")));
        var order = QuestModuleList.LoadOrder(QuestModuleList.Entries("era\ncustom/lua/b.lua\nera/lua/a.lua\n"),
            new[] { "modules/era/lua/z.lua", "modules/era/lua/a.lua", "modules/custom/lua/b.lua", "modules/custom/lua/c.lua" });
        Check("p4 init:the load order: the file lines in order, then each folder's files in path order; each once",
              "modules/custom/lua/b.lua,modules/era/lua/a.lua,modules/era/lua/z.lua", string.Join(",", order));
    }

    // a wrap that changes its base's action (The Moonlit Path's era module: action.options[5] = setBit(..., 7))
    static void RunP4WithArg()
    {
        var r = QuestReply.ProgressEvent(846, 0, 1, 2, 3, 0x40, 5).OncePerZone();
        Check("p4 withArg:the argument replaced, the rest kept", "ProgressEvent(846,0,1,2,3,192,5):oncePerZone()|ProgressEvent(846,0,1,2,3,64,5):oncePerZone()",
              $"{QuestContextFake.Describe(r.WithArg(4, 0x40 | 1 << 7))}|{QuestContextFake.Describe(r)}");
        Check("p4 withArg:an argument the reply does not have", "ArgumentOutOfRangeException/ArgumentOutOfRangeException",
              $"{P2Refused(() => r.WithArg(6, 1))}/{P2Refused(() => QuestReply.Event(1).WithArg(0, 1))}");
    }

    // The Gustaberg Tour's era module on the real quest: Hunting_Bear's onTrigger in state 2 replaced; 22 only for six
    // members all at level 5 or below and within 15 yalms
    static void RunP4Gustaberg()
    {
        var module = new XI.QuestContent.Modules.Era.Bastok.TheGustabergTour();
        var off = new Fake();
        var on = new Fake { MainSettings = { ["RESTRICT_CONTENT"] = 1, ["ENABLE_ROV"] = 0 } };
        Check("gustaberg module:its name, its Lua file, its condition xi.pre(ROV)",
              "era_quest_the_gustaberg_tour|modules/era/lua/quests/bastok/the_gustaberg_tour.lua|False/True",
              $"{module.Name}|{module.LuaFile}|{module.Condition(off)}/{module.Condition(on)}");
        var r = QuestOverrides.Apply(new XI.QuestContent.Bastok.TheGustabergTour().Compile(), module.Compile(on), XI.QuestContent.QuestKey.Bastok.TheGustabergTour);
        Check("gustaberg module:applies, and the patched quest validates", "|", $"{string.Join(" | ", r.Problems)}|{string.Join(" | ", QuestScriptValidator.Validate(r.Definition))}");
        var bear = r.Definition.States[1].Handlers.Single(h => h.Entity == XI.QuestContent.Npc.NorthGustaberg.HuntingBear);
        Check("gustaberg module:Hunting_Bear's onTrigger in state 2 is the module's", "era_quest_the_gustaberg_tour/OnTrigger/106",
              $"{bear.Module?.Name}/{bear.Kind}/{bear.Zone}");
        string Bear(int party, params (int Level, double Distance)[] others)
        {
            var f = new Fake { MainLevel = 5, Party = party };
            f.AllianceMembers.AddRange(others);
            return bear.Reply(f).Id.ToString();
        }
        var five = Enumerable.Repeat((5, 3.0), 5).ToArray();
        Check("gustaberg module:six at level 5 in range: 22; a party of two: 21; one at level 6: 21; one 16 yalms off: 21; alone: 21",
              "22/21/21/21/21",
              $"{Bear(6, five)}/{Bear(2, (5, 3.0))}/{Bear(6, five.Take(4).Append((6, 3.0)).ToArray())}/{Bear(6, five.Take(4).Append((5, 16.0)).ToArray())}/{Bear(1)}");
        var own = new XI.QuestContent.Bastok.TheGustabergTour().Compile().States[1].Handlers.Single(h => h.Entity == XI.QuestContent.Npc.NorthGustaberg.HuntingBear);
        var two = new Fake { MainLevel = 10, Party = 2 };
        two.AllianceMembers.Add((10, 3.0));
        Check("gustaberg module:  the quest's own plays 22 for a party of two at level 10", "22", own.Reply(two).Id.ToString());
    }
}
