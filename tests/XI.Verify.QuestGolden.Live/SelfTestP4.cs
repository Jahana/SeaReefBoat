using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Script;

// quest rework P4: the module statement (module modules/<path>.lua), the modules' part of the coverage gate, and the Lua
// module loader the record side runs (moduleutils.cpp's way: module_utils.lua, the module file, applyOverride), in the
// self-test (no database)
internal static partial class Program
{
    private static void SelfTestPlatformP4()
    {
        Console.WriteLine("quest rework P4: the module statement");
        SelfTestP4Grammar();
        SelfTestP4CheckedIn();
        SelfTestP4Coverage();
        SelfTestP4Loader();
        SelfTestP4RecordKeys();
    }

    private sealed class SelfTestModule : XI.Quests.Authoring.QuestModule
    {
        private readonly Action<XI.Quests.Authoring.QuestModuleBuilder> build_;

        public SelfTestModule(string name, string file, Action<XI.Quests.Authoring.QuestModuleBuilder> build) : base(name, file) => build_ = build;

        protected override void Build(XI.Quests.Authoring.QuestModuleBuilder m) => build_(m);
    }

    // a module that patches two quests (Divine Might's: Divine_Might and Divine_Might_Repeat): a record turns off and
    // overlays both, not only the flow's own, so the module's modifyInteractionEntry of the second finds its Lua file
    private static void SelfTestP4RecordKeys()
    {
        const string file = "modules/era/lua/quests/outlands/divine_might.lua";
        var two = new SelfTestModule("era_quest_divine_might", file, m =>
        {
            m.Quest("outlands.divine_might").State(1).ReplaceCheck(q => false);
            m.Quest("outlands.divine_might_repeat").State(1).ReplaceCheck(q => false);
            // a quest it patches only while xi.pre holds: a key all the same
            if (m.Pre(XI.Quests.Authoring.QuestExpansion.Rov)) m.Quest("outlands.only_when_restricted").State(1).ReplaceCheck(q => false);
        });
        var other = new SelfTestModule("p4_selftest_other", "modules/custom/lua/other.lua", m => m.Quest("bastok.other").State(1).ReplaceCheck(q => false));
        var keys = ModuleQuestKeys(new[] { file }, new XI.Quests.Authoring.QuestModule[] { two, other });
        Check("  a module's keys: every quest it patches, under each xi.pre branch; another file's module not",
              "outlands.divine_might,outlands.divine_might_repeat,outlands.only_when_restricted", string.Join(",", keys));
        Check("  a record's CSHARP_QUESTS_OFF: the flow's quest, the module's others, then the overlay's",
              "outlands.divine_might,outlands.divine_might_repeat,outlands.only_when_restricted,windurst.x",
              string.Join(",", RecordOffKeys("outlands.divine_might", keys, new[] { "outlands.divine_might_repeat", "windurst.x" })));
        Check("  no module: the flow's quest and the overlay's", "bastok.a,windurst.x",
              string.Join(",", RecordOffKeys("bastok.a", ModuleQuestKeys(Array.Empty<string>(), new[] { two }), new[] { "windurst.x" })));
        var quests = new[]
        {
            (Key: "outlands.divine_might", Legacy: "scripts/quests/outlands/Divine_Might.lua", Constant: "xi.quest.id.outlands.DIVINE_MIGHT"),
            (Key: "outlands.divine_might_repeat", Legacy: "scripts/quests/outlands/Divine_Might_Repeat.lua", Constant: "xi.quest.id.outlands.DIVINE_MIGHT_REPEAT"),
        };
        var none = new SortedSet<string>(StringComparer.Ordinal);
        Check("  the overlay takes the second quest's file, and a script that names its constant; not an unrelated one",
              "True/True/False",
              $"{OverlayTakes("scripts/quests/outlands/Divine_Might_Repeat.lua", "", none, quests)}/" +
              $"{OverlayTakes("scripts/zones/RuAun_Gardens/npcs/x.lua", "local q = xi.quest.id.outlands.DIVINE_MIGHT_REPEAT", none, quests)}/" +
              $"{OverlayTakes("scripts/zones/RuAun_Gardens/npcs/y.lua", "local q = xi.quest.id.outlands.DIVINE_MIGHT_REPEAT_2", none, quests)}");
        SelfTestOverlayPlan();
    }

    // Windurst Task 14 review: the overlay's off set and its closure (OverlayPlan, OverlayStray), on Windurst
    // Woods-shaped texts. A C-3000 record at a commit before the group 22 port, where Apururu.lua already called The
    // Kind Cardian
    private static void SelfTestOverlayPlan()
    {
        static string Call(string key) =>
            $"    if xi.cs.quest(player, npc, '{key}', 'onTrigger') then\n        return\n    end\n";
        const string Tkc = "jeuno.the_kind_cardian", Cry = "windurst.can_cardians_cry";
        const string C2 = "windurst.the_all_new_c_2000", Lpb = "windurst.legendary_plan_b";
        const string C3 = "windurst.the_all_new_c_3000";
        const string C3Status =
            "    local s = player:getQuestStatus(xi.questLog.WINDURST, xi.quest.id.windurst.THE_ALL_NEW_C_3000)\n";
        const string Woods = "scripts/zones/Windurst_Woods/npcs/";
        var c3000 = new[] { (Key: C3, Legacy: (string)null, Constant: "xi.quest.id.windurst.THE_ALL_NEW_C_3000") };
        var apururu = (Rel: Woods + "Apururu.lua", OldText: Call(Tkc) + C3Status, NowText: Call(Tkc) + Call(Cry));
        var (taken, off) = OverlayPlan(new[] { apururu }, c3000);
        Check("  overlay: a file that already called a quest at the commit keeps it C# (The Kind Cardian); " +
              "the newly called one goes off",
              $"scripts/zones/Windurst_Woods/npcs/Apururu.lua|{Cry}",
              $"{string.Join(",", taken)}|{string.Join(",", off)}");
        var kororo = (Rel: Woods + "Kororo.lua", OldText: "    player:startEvent(291)\n",
                      NowText: Call(C2) + Call(Lpb));
        var boizo = (Rel: Woods + "Boizo-Naizo.lua", OldText: "    player:startEvent(290)\n", NowText: Call(C2));
        var kopuro = (Rel: Woods + "Kopuro-Popuro.lua", OldText: C3Status, NowText: Call(C3) + Call(Lpb) + Call(C2));
        var other = (Rel: "scripts/zones/Bastok_Mines/npcs/x.lua", OldText: "    player:startEvent(1)\n",
                     NowText: Call("bastok.x"));
        (taken, off) = OverlayPlan(new[] { boizo, kororo, other, kopuro, apururu }, c3000);
        Check("  ... closure: a changed script that calls a quest gone off is taken too " +
              "(Kororo, Boizo-Naizo: C-2000 / Legendary Plan B), in the diff's order; an unrelated one not",
              $"Boizo-Naizo.lua,Kororo.lua,Kopuro-Popuro.lua,Apururu.lua|{Cry},{Lpb},{C2}",
              $"{string.Join(",", taken.Select(Path.GetFileName))}|{string.Join(",", off)}");
        var offOrKeys = new SortedSet<string>(off.Append(C3), StringComparer.Ordinal);
        var naruru = (Rel: "scripts/zones/Lower_Jeuno/npcs/Naruru.lua", NowText: Call(Tkc));
        var stray = (Rel: "scripts/zones/Windurst_Woods/npcs/y.lua", NowText: Call(C2));
        Check("  ... a script left at HEAD that calls a quest that is off refuses the record; " +
              "one calling a C# quest, or taken, does not",
              $"scripts/zones/Windurst_Woods/npcs/y.lua calls {C2}|null|null",
              $"{OverlayStray(taken, offOrKeys, new[] { naruru, stray })}|" +
              $"{OverlayStray(taken, offOrKeys, new[] { naruru }) ?? "null"}|" +
              $"{OverlayStray(taken, offOrKeys, new[] { (kororo.Rel, kororo.NowText) }) ?? "null"}");
        Check("  ... a plan with no taken script calling an off quest at the commit is not half-ported",
              "null",
              OverlayHalfPorted(new[] { boizo, kororo, other, kopuro, apururu }, taken, off, new[] { C3 }) ?? "null");
        // half-ported at the commit: Kororo already called Can Cardians Cry there, Apururu brings its Lua back
        var halfKororo = (kororo.Rel, OldText: Call(Cry), NowText: Call(Cry));
        var changed = new[] { halfKororo, apururu };
        (taken, off) = OverlayPlan(changed, c3000);
        Check("  ... a quest one taken script called at the commit and another brings back is refused, by name",
              $"{Cry} is half-ported at the commit: scripts/zones/Windurst_Woods/npcs/Kororo.lua calls it there, " +
              "another script the overlay takes brings its Lua back, so that call would find no quest and fall through",
              OverlayHalfPorted(changed, taken, off, new[] { C3 }) ?? "null");
        // carry-harness-3: the record's own quest, already called at the commit by a script the overlay takes
        // (Kopuro-Popuro called C-3000 there): off holds only the other quests, so the own keys are checked too
        var halfKopuro = (kopuro.Rel, OldText: Call(C3), kopuro.NowText);
        changed = new[] { halfKopuro, apururu };
        (taken, off) = OverlayPlan(changed, c3000);
        Check("  ... the record's own quest a taken script called at the commit is refused, by name; " +
              "a key it does not record is not",
              $"{C3} is half-ported at the commit: scripts/zones/Windurst_Woods/npcs/Kopuro-Popuro.lua calls it " +
              "there, and the record turns it off, so that call would find no quest and fall through|null",
              $"{OverlayHalfPorted(changed, taken, off, new[] { C3 }) ?? "null"}|" +
              $"{OverlayHalfPorted(changed, taken, off, new[] { C2 }) ?? "null"}");

        // carry-quests-1: a changed file that a taken file requires at the commit is taken too (helpers.lua, whose
        // Borghertz template the commit's Borghertz's Calling Hands needs once HEAD dropped it); one that only a
        // comment names, or that nothing taken requires, stays at HEAD
        var hands = (Rel: "scripts/quests/jeuno/Borghertzs_Calling_Hands.lua",
                     OldText: "require('scripts/quests/jeuno/helpers')\n" +
                              "-- require('scripts/quests/otherAreas/helpers')\n" +
                              "local quest = xi.jeuno.helpers.BorghertzQuests:new(params)\n",
                     NowText: "");
        var helpers = (Rel: "scripts/quests/jeuno/helpers.lua", OldText: "xi.jeuno.helpers.BorghertzQuests = {}\n",
                       NowText: "xi.jeuno.helpers.GobbiebagQuest = {}\n");
        var oaHelpers = (Rel: "scripts/quests/otherAreas/helpers.lua", OldText: "local a = 1\n",
                         NowText: "local a = 2\n");
        var calling = new[]
        {
            (Key: "jeuno.borghertzs_calling_hands", Legacy: "scripts/quests/jeuno/Borghertzs_Calling_Hands.lua",
             Constant: "xi.quest.id.jeuno.BORGHERTZS_CALLING_HANDS"),
        };
        (taken, off) = OverlayPlan(new[] { helpers, oaHelpers, hands }, calling);
        Check("  overlay: a changed file a taken one requires at the commit is taken too (in the diff's order); " +
              "a require in a comment, or a file nothing taken requires, not",
              "scripts/quests/jeuno/helpers.lua,scripts/quests/jeuno/Borghertzs_Calling_Hands.lua|",
              $"{string.Join(",", taken)}|{string.Join(",", off)}");
        Check("  ... RequiredNames: both quote kinds, no parentheses; not after --",
              "a/b|c|d",
              string.Join("|", RequiredNames("require('a/b')\nlocal x = require(\"c\")\n" +
                                             "require 'd'\n-- require('e')\n")));
    }

    private const string P4Module = "modules/era/lua/quests/bastok/the_gustaberg_tour.lua";
    private const string P4Head = "char zone=NorthGustaberg pos=-233.9,41.465,426.5 lvl=5\n";

    private static void SelfTestP4Grammar()
    {
        try
        {
            var f = Flow.Parse($"module {P4Module}\n# a comment\n" + P4Head + "module modules/custom/lua/quest_workarounds.lua   # another\nsetting RESTRICT_CONTENT 1\nlogin\n",
                               "m.flow", "m", null);
            Check("  module lines: the files, in order (before the char line or after it)",
                  $"{P4Module}|modules/custom/lua/quest_workarounds.lua", string.Join("|", f.Modules));
            Check("  ... header lines, not steps or setup", "login|setting", $"{string.Join(",", f.Steps.Select(st => st.Verb))}|{string.Join(",", f.Setup.Select(st => st.Verb))}");
            Check("  ... as the module's init.txt lines (ZoneHost.ModuleInitLines)", "era/lua/quests/bastok/the_gustaberg_tour.lua|custom/lua/quest_workarounds.lua",
                  string.Join("|", ModuleInitLinesOf(f)));
            var plain = Flow.Parse(P4Head + "login\n", "p.flow", "p", null);
            Check("  a flow without one has none", "0", plain.Modules.Count.ToString());
            Check("  the module line is in the flow hash", "False", (Flow.Hash($"module {P4Module}\n" + P4Head + "login\n") == Flow.Hash(P4Head + "login\n")).ToString());
        }
        catch (FormatException e)
        {
            Fail("  module lines", e.Message);
        }
        SelfTestP3Refused("  refused: no file", P4Head + "module\nlogin\n", "t.flow:2: module takes 1 arguments");
        SelfTestP3Refused("  refused: two files on a line", P4Head + $"module {P4Module} {P4Module}\nlogin\n", "t.flow:2: module takes 1 arguments");
        SelfTestP3Refused("  refused: a path outside modules/", P4Head + "module scripts/quests/bastok/The_Gustaberg_Tour.lua\n",
                          "t.flow:2: a module is modules/<path>.lua (a Lua module file, / separators): scripts/quests/bastok/The_Gustaberg_Tour.lua");
        SelfTestP3Refused("  refused: not a .lua file", P4Head + "module modules/era\n", "t.flow:2: a module is modules/<path>.lua (a Lua module file, / separators): modules/era");
        SelfTestP3Refused("  refused: a backslash", P4Head + "module modules\\era\\x.lua\n", "t.flow:2: a module is modules/<path>.lua (a Lua module file, / separators): modules\\era\\x.lua");
        SelfTestP3Refused("  refused: a .. segment", P4Head + "module modules/era/../x.lua\n", "t.flow:2: a module is modules/<path>.lua (a Lua module file, / separators): modules/era/../x.lua");
        SelfTestP3Refused("  refused: module_utils.lua", P4Head + "module modules/module_utils.lua\n", "t.flow:2: modules/module_utils.lua is the helpers, not a module");
        SelfTestP3Refused("  refused: the same module twice", P4Head + $"module {P4Module}\nmodule {P4Module}\n", $"t.flow:3: a second module line for {P4Module}");
        SelfTestP3Refused("  refused: after login", P4Head + $"login\nmodule {P4Module}\n", "t.flow:3: module after login");
        SelfTestP3Refused("  refused: a char's prefix", P4Head + $"main: module {P4Module}\n", "t.flow:2: module is not a char's statement: no main: prefix");
    }

    // every checked-in flow parses, and each module line names a Lua file a C# module ports (the flows that load one listed)
    private static void SelfTestP4CheckedIn()
    {
        var with = new List<string>();
        int all = 0;
        foreach (var path in Directory.GetFiles(FlowsDir, "*.flow").OrderBy(p => p, StringComparer.Ordinal))
        {
            Flow f;
            try
            {
                f = Flow.Load(path, (_, _) => true);
            }
            catch (FormatException e)
            {
                Fail($"  {Path.GetFileName(path)}", e.Message);
                continue;
            }
            all++;
            if (f.Modules.Count > 0) with.Add($"{f.Name}:{string.Join(",", f.Modules)}");
        }
        Console.WriteLine($"  the checked-in flows ({all}) that load a module: {(with.Count == 0 ? "none" : string.Join(" ", with))}");
        // platform round P11: a module line may name a Lua module no C# module ports (a global's override: the server's
        // Lua module loader runs it on both sides); the file must be there
        Check("  each module line of a checked-in flow names a Lua file that is there", "",
              string.Join(" ", with.SelectMany(w => w.Substring(w.IndexOf(':') + 1).Split(',')).Where(m => !File.Exists(Path.Combine(Root, m))).Distinct()));
    }

    // ModuleCoverageResults: per module some flow loads, once all its flows ran: each override reached, else a failure;
    // not enabled fails; a C# module no flow loads fails; the quests' part leaves the module's labels alone
    private static void SelfTestP4Coverage()
    {
        string key = ModuleKey(P4Module);
        const string label = "bastok.the_gustaberg_tour 2/Hunting_Bear/onTrigger";
        var flows = new List<Flow>
        {
            Flow.Parse($"module {P4Module}\n" + P4Head + "login\n", "a.flow", "bastok.the_gustaberg_tour#era", null),
            Flow.Parse($"module {P4Module}\n" + P4Head + "login\n", "b.flow", "bastok.the_gustaberg_tour#era_level6", null),
            Flow.Parse(P4Head + "login\n", "c.flow", "bastok.the_gustaberg_tour", null),
        };
        Coverage Ran(bool reached, bool enabled = true, int ran = 3)
        {
            var cov = new Coverage();
            foreach (var f in flows.Take(ran)) cov.Ran.Add(f.Name);
            if (enabled) cov.Declared[key] = new List<string> { label };
            else cov.NoHandlers.Add(key);
            cov.Declared["bastok.the_gustaberg_tour"] = new List<string> { "1/Izabele/onTrigger" };
            cov.Add("bastok.the_gustaberg_tour", "1/Izabele/onTrigger", "bastok.the_gustaberg_tour");
            if (reached) cov.Add(key, label, "bastok.the_gustaberg_tour#era");
            return cov;
        }
        Check("  an override reached: PASS", $"PASS coverage {key}: 1 of 1 overrides reached",
              string.Join("|", ModuleCoverageResults(Ran(true), flows, null)));
        Check("  an override reached by no trace: FAIL", $"FAIL coverage {key}: {label} reached by no trace",
              string.Join("|", ModuleCoverageResults(Ran(false), flows, null)));
        Check("  not enabled in its flows: FAIL", $"FAIL coverage {key}: not enabled in its flows (no C# module ports it, or its condition is false with their settings)",
              string.Join("|", ModuleCoverageResults(Ran(true, enabled: false), flows, null)));
        Check("  not every flow that loads it ran: not checked", "", string.Join("|", ModuleCoverageResults(Ran(false, ran: 1), flows, null)));
        const string luaOnly = "modules/era/lua/globals/equipment/unlocking_a_myth.lua";
        var luaFlows = new List<Flow> { Flow.Parse($"module {luaOnly}\n" + P4Head + "login\n", "d.flow", "bastok.the_gustaberg_tour#lua_only", null) };
        var luaCov = new Coverage();
        luaCov.Ran.Add("bastok.the_gustaberg_tour#lua_only");
        luaCov.NoHandlers.Add(ModuleKey(luaOnly));
        Check("  a Lua module no C# module ports (P11): PASS, run by the server's loader",
              $"PASS coverage {ModuleKey(luaOnly)}: a Lua module (no C# port), run by the server's loader in 1 flows",
              string.Join("|", ModuleCoverageResults(luaCov, luaFlows, null)));
        Check("  a C# module no flow loads: FAIL", $"FAIL coverage {ModuleKey("modules/era/lua/x.lua")}: no flows",
              string.Join("|", ModuleCoverageResults(Ran(true), flows, new[] { P4Module, "modules/era/lua/x.lua" }).Where(l => l.StartsWith("FAIL"))));
        Check("  the quest's part is unchanged by the module's runs", "PASS coverage bastok.the_gustaberg_tour: 1 of 1 handlers reached, 0 waived",
              string.Join("|", CoverageResults(Ran(true), flows).Where(l => !l.Contains("module "))));
        // a Reward and a RemoveState are overrides too (ZoneHost.ModuleLabels: "<key> reward", "<key> <n>/-/removed"): not
        // 0 of 0 with nothing tested (Northward, A Hard Day's Knight)
        Coverage StateOps(bool removedReached)
        {
            var cov = Ran(true);
            cov.Declared[key] = new List<string> { label, "bastok.the_gustaberg_tour 3/-/removed", "bastok.the_gustaberg_tour reward" };
            cov.Add(key, "bastok.the_gustaberg_tour reward", "bastok.the_gustaberg_tour#era");
            if (removedReached) cov.Add(key, "bastok.the_gustaberg_tour 3/-/removed", "bastok.the_gustaberg_tour#era");
            return cov;
        }
        Check("  a reward and a removed state count: all reached PASS, the removal reached by no trace FAIL",
              $"PASS coverage {key}: 3 of 3 overrides reached|FAIL coverage {key}: bastok.the_gustaberg_tour 3/-/removed reached by no trace",
              string.Join("|", ModuleCoverageResults(StateOps(true), flows, null).Concat(ModuleCoverageResults(StateOps(false), flows, null))));
        // the counts file (replay all --jobs) carries the module's key and labels through
        var back = Coverage.Parse(Ran(true).Write(), "counts");
        Check("  ... through the counts file of --jobs", $"PASS coverage {key}: 1 of 1 overrides reached",
              string.Join("|", ModuleCoverageResults(back, flows, null)));
    }

    // the server's Lua module loader (XI.Script.LuaModules, platform round P11, which the module lines run through)
    // in a bare LuaState, its root a temp folder with the real modules/module_utils.lua: the init lines' files run,
    // then TryApply of scripts/globals/server and TryApplyRemaining, as ZoneHost.LoadLuaModules after the global
    // scripts
    private static LuaModules P4Loader(LuaState lua, string dir, params string[] lines)
    {
        var modules = new LuaModules(lua, dir, _ => { }, _ => { });
        modules.Load(lines, null, 0);
        modules.TryApply(new[] { "globals", "server" });
        modules.TryApplyRemaining();
        return modules;
    }

    // the loader on a module file of two Modules (one enabled, one not, as Module:new's condition gives), each override
    // applied at its dotted target with its super; the target's base runs first through super(), a missing last part
    // becomes an empty function, a missing table leaves the override not applied (silently, as TryApplyRemaining is)
    private static void SelfTestP4Loader()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"questgolden-p4-{Environment.ProcessId}");
        Directory.CreateDirectory(Path.Combine(dir, "modules"));
        try
        {
            File.Copy(Path.Combine(Root, "modules", "module_utils.lua"), Path.Combine(dir, "modules", "module_utils.lua"), true);
            string module = Path.Combine(dir, "modules", "p4_module.lua").Replace('\\', '/');
            File.WriteAllText(module, @"
local m = Module:new('p4_selftest_module', true)
m:addOverride('xi.server.onServerStart', function()
    super()
    report('module')
end)
m:addOverride('xi.server.onNothingYet', function()
    super()
    report('new')
end)
local off = Module:new('p4_selftest_off', false)
off:addOverride('xi.server.onServerStart', function()
    super()
    report('off')
end)
");
            string broken = Path.Combine(dir, "modules", "p4_broken.lua").Replace('\\', '/');
            File.WriteAllText(broken, @"
local m = Module:new('p4_selftest_broken', true)
m:addOverride('xi.nowhere.fn', function() end)
");
            LuaNative.LibraryDirectory = Root;
            using var lua = new LuaState();
            var got = new List<string>();
            lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
            lua.DoString(@"
xi = { server = {} }
utils = {}
function utils.splitStr(s, sep)
    local fields = {}
    string.gsub(s, string.format('([^%s]+)', sep), function(c) fields[#fields + 1] = c end)
    return fields
end
xi.server.onServerStart = function() report('base') end", "=p4");
            var loaded = P4Loader(lua, dir, "p4_module.lua");
            Check("  the loader registers each Module: name=enabled", "p4_selftest_module=True,p4_selftest_off=False",
                  string.Join(",", loaded.Modules.Select(m => $"{m.Name}={m.Enabled}")));
            got.Clear();
            lua.DoString("xi.server.onServerStart() xi.server.onNothingYet()", "=p4");
            Check("  the enabled override wraps its target (super first); the disabled one is not applied; a missing last part is an empty function",
                  "base,module,new", string.Join(",", got));
            got.Clear();
            lua.DoString("report(tostring(#xi.module.registry))", "=p4");
            Check("  the registry is emptied after the file", "0", string.Join(",", got));
            var brokenLoad = P4Loader(lua, dir, "p4_broken.lua");
            Check("  a target whose table is missing: not applied, no error (TryApplyRemaining is silent)", "xi.nowhere.fn|",
                  string.Join(",", brokenLoad.NotApplied().Select(o => o.Name)) + "|" + string.Join(",", brokenLoad.Errors));
            SelfTestP4LoadTime(dir);
        }
        catch (Exception e)
        {
            Fail("  the Lua module loader", e.Message);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    // a module that overrides a helper a quest file calls when it loads (the Gobbiebag quests' GobbiebagQuest.new): the
    // quest file is cached in package.loaded from the world's first load, so InteractionGlobal.reload(true) alone (and the
    // loader after it) keeps the container built without the override; the loader, then ContainerReload (the module flows'
    // order, as the C++ server applies modules before it builds the containers) builds it under the override. A bare
    // LuaState: a quest file under scripts/quests/, GetContainerFilenamesList and a loadContainers stand-in
    private static void SelfTestP4LoadTime(string dir)
    {
        string questDir = Path.Combine(dir, "scripts", "quests", "p4");
        Directory.CreateDirectory(questDir);
        File.WriteAllText(Path.Combine(questDir, "P4_Bag.lua"), "return xi.p4helpers.BagQuest:new({ fame = 1 })\n");
        string module = Path.Combine(dir, "modules", "p4_loadtime.lua").Replace('\\', '/');
        File.WriteAllText(module, @"
local m = Module:new('p4_selftest_loadtime', true)
m:addOverride('xi.p4helpers.BagQuest.new', function(self, params)
    params.fame = 3
    local quest = super(self, params)
    quest.patched = true
    return quest
end)
");
        using var lua = new LuaState();
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        lua.DoString($@"
package.path = {LuaStr(dir.Replace('\\', '/') + "/?.lua;")} .. package.path
xi = {{ server = {{}}, p4helpers = {{ BagQuest = {{}} }} }}
function xi.p4helpers.BagQuest:new(params) return {{ fame = params.fame }} end
utils = {{}}
function utils.splitStr(s, sep)
    local fields = {{}}
    string.gsub(s, string.format('([^%s]+)', sep), function(c) fields[#fields + 1] = c end)
    return fields
end
function GetContainerFilenamesList() return {{ 'scripts/quests/p4/P4_Bag' }} end
InteractionGlobal = {{}}
function InteractionGlobal.reload()
    local files = GetContainerFilenamesList()
    InteractionGlobal.containers = {{}}
    for i = 1, #files do InteractionGlobal.containers[i] = require(files[i]) end
end
local function show() local c = InteractionGlobal.containers[1] report(c.fame .. '/' .. tostring(c.patched == true)) end
InteractionGlobal.reload(true)
show()
InteractionGlobal.reload(true)
show()", "=p4");
        P4Loader(lua, dir, "p4_loadtime.lua");
        got.Clear();
        lua.DoString("local c = InteractionGlobal.containers[1] report(c.fame .. '/' .. tostring(c.patched == true))", "=p4");
        lua.DoString(ContainerReload, "=p4");
        lua.DoString("local c = InteractionGlobal.containers[1] report(c.fame .. '/' .. tostring(c.patched == true))", "=p4");
        Check("  a load-time helper override: the cached quest file keeps the plain container; ContainerReload builds it under the override",
              "1/false,3/true", string.Join(",", got));
    }
}
