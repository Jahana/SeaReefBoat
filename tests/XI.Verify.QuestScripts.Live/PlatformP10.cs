using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using XI.QuestContent;
using XI.Quests;
using XI.Quests.Authoring;

// quest rework P10: deferred callbacks live
// (docs/superpowers/specs/2026-10-28-quest-deferred-callbacks-design.md, section 6). A test quest of the fixture's key
// arms a player timer, an NPC timer (with and without its char) and a TICK listener through the engine's own bindings:
// a timer fires on the first tick strictly past its deadline (CAIActionQueue::checkAction), in arming order with a
// Lua timer of the same tick; a logout before the deadline fires nothing; a listener is heard each tick until it
// removes itself; a char gone before its NPC timer: logged, not run; a callback that throws: logged, the queue goes
// on. A char's listeners go with it on a zone change, their Lua functions released. The exports: the module claim in
// package.loaded (healing.lua loads with Dances with Luopans' Lua file gone), the stand-down for a loaded Lua module,
// a refused quest's no-op globals, a doubled export refused, a second quest of one key leaving the first's exports
internal static partial class Program
{
    private static void RunPlatformP10()
    {
        Console.WriteLine("quest rework P10: deferred callbacks");
        RunP10Timers();
        RunP10ListenersAndZoneChange();
        RunP10Exports();
    }

    // what the test quest's callbacks did, in order
    private static readonly List<string> p10Ran_ = new();
    private const int P10Csid = 9100;

    // the arms, by the finish's option: 1 player timer (1000 ms); 2 NPC timer; 3 NPC timer with the char; 4 a throwing
    // callback then a player timer, both 0 ms; 5 the TICK listener; 6 a negative delay; 7 another quest's callback; 8 a
    // listener of another event; 9 a player timer whose callback adds 2 to P10_SEQ
    // (the order check against a Lua timer)
    private sealed class P10TimerQuest : QuestScript
    {
        public static readonly QuestCallback Foreign = new P10ForeignQuest().Compile().Callbacks[0];

        public P10TimerQuest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            var pt = b.Callback("pt", q => p10Ran_.Add($"pt:{q.Player.Id}:{(q.Npc == null ? "-" : "npc")}"));
            var nt = b.Callback("nt", q =>
            {
                string player;
                try
                {
                    player = q.Player.Id.ToString();
                }
                catch (InvalidOperationException)
                {
                    player = "none";
                }
                p10Ran_.Add($"nt:{q.Npc.Id}:{player}");
            });
            var wp = b.Callback("wp", q => p10Ran_.Add($"wp:{q.Npc.Id}:{q.Player.Id}"));
            var boom = b.Callback("boom", q => throw new InvalidOperationException("boom"));
            var seq = b.Callback("seq", q => q.Player.SetLocalVar("P10_SEQ", q.Player.LocalVar("P10_SEQ") * 10 + 2));
            var tick = b.Callback("tick", q =>
            {
                p10Ran_.Add("tick");
                if (p10Ran_.Count(x => x == "tick") >= 3) q.Player.RemoveListener("P10_TICK");
            });
            b.AnyState()
                .OnEventFinish(Zone.BastokMarkets, P10Csid, q =>
                {
                    switch (q.Option)
                    {
                        case 1: q.Player.Timer(1000, pt); break;
                        case 2: q.Npc.Timer(1000, nt); break;
                        case 3: q.Npc.Timer(1000, wp, WithPlayer.Yes); break;
                        case 4:
                            q.Player.Timer(0, boom);
                            q.Player.Timer(0, pt);
                            break;
                        case 5: q.Player.AddListener("TICK", "P10_TICK", tick); break;
                        case 6: q.Player.Timer(-1, pt); break;
                        case 7: q.Player.Timer(1000, Foreign); break;
                        case 8: q.Player.AddListener("DEATH", "P10_DEATH", tick); break;
                        case 9: q.Player.Timer(1000, seq); break;
                    }
                });
        }
    }

    // another quest's key (P10OtherLog / P10OtherConst: a key no quest has, Program.cs's TestKeysFree guards it)
    private sealed class P10ForeignQuest : QuestScript
    {
        public P10ForeignQuest() : base(P10OtherLog, P10OtherConst) { }

        protected override void Build(QuestScriptBuilder b) => b.Callback("foreign", q => p10Ran_.Add("foreign"));
    }

    // the test quest's 9100 finish run with the option, the NPC given as its npc
    private static void P10Arm(int option, uint npc) =>
        Lua($@"
            for _, e in ipairs(InteractionGlobal.lookup.data[xi.zone.BASTOK_MARKETS].onEventFinish[{P10Csid}] or {{}}) do
                if e.container and e.container.id == '{FixtureContainer}' then e.handler(player, {P10Csid}, {option}, GetNPCByID({npc})) end
            end");

    private static string P10Lines(int mark) => string.Join(" | ", log.Skip(mark).Where(l => l.StartsWith("quests: ")));

    private static void RunP10Timers()
    {
        uint id = CharIds[0];
        zone.TestQuestScripts.Add(new P10TimerQuest());
        Lua("InteractionGlobal.reload(true)");
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        uint npc = NpcId("Samia");
        p10Ran_.Clear();
        int mark = log.Count;

        // a player timer: armed now; at the deadline (now + 1000) nothing, 1 ms past it the callback, with the char
        P10Arm(1, npc);
        Tick(500);
        Tick(500);
        string atDeadline = string.Join(",", p10Ran_);
        Tick(1);
        Check("timers: a player timer is not run at its deadline, run on the first tick past it, with the char (no Npc)",
              $"|pt:{id}:-", $"{atDeadline}|{string.Join(",", p10Ran_)}");
        Tick(1000);
        Check("  ... once", $"pt:{id}:-", string.Join(",", p10Ran_));

        // an NPC timer: the NPC as Npc and no Player
        p10Ran_.Clear();
        P10Arm(2, npc);
        Tick(1000);
        Tick(1);
        Check("timers: an NPC timer runs with the NPC as its Npc and no Player", $"nt:{npc}:none", string.Join(",", p10Ran_));
        // with the char: found by id when it fires
        p10Ran_.Clear();
        P10Arm(3, npc);
        Tick(1001);
        Check("timers: an NPC timer WithPlayer.Yes runs with the NPC and the char that armed it", $"wp:{npc}:{id}", string.Join(",", p10Ran_));

        // armed in one tick with a Lua player:timer, the two fire in arming order (one queue): Lua then C# is 12, C#
        // then Lua 21
        Lua("player:setLocalVar('P10_SEQ', 0) player:timer(1000, function(p) p:setLocalVar('P10_SEQ', p:getLocalVar('P10_SEQ') * 10 + 1) end)");
        P10Arm(9, npc);
        Tick(1001);
        string luaFirst = Lua("report(tostring(player:getLocalVar('P10_SEQ')))");
        Lua("player:setLocalVar('P10_SEQ', 0)");
        P10Arm(9, npc);
        Lua("player:timer(1000, function(p) p:setLocalVar('P10_SEQ', p:getLocalVar('P10_SEQ') * 10 + 1) end)");
        Tick(1001);
        string csFirst = Lua("report(tostring(player:getLocalVar('P10_SEQ')))");
        Check("timers: armed in one tick with a Lua player:timer, both fire in arming order (Lua, C#: 12; C#, Lua: 21)", "12|21",
              $"{luaFirst}|{csFirst}");

        // one tick holding a char's timer (armed first), an NPC's timer and the char's TICK listener: the NPC's timer,
        // then the char's listener, then the char's timer. CZoneEntities::ZoneServer ticks the NPCs before the chars,
        // each through CAIContainer::Tick (its TICK listeners, its tick, its ActionQueue.checkAction): B3b Deferred 1,
        // the P10 final review's M8
        Lua($@"
            __p10order = {{}}
            player:timer(1000, function() table.insert(__p10order, 'char timer') end)
            GetNPCByID({npc}):timer(1000, function() table.insert(__p10order, 'npc timer') end)
            player:addListener('TICK', 'P10_ORDER', function() table.insert(__p10order, 'char listener') end)");
        Tick(1001);
        string order = Lua("report(table.concat(__p10order, ','))");
        Lua("player:removeListener('P10_ORDER') __p10order = nil");
        Check("timers: one tick: an NPC's timer, then the char's TICK listener, then the char's timer (the chars tick last)",
              "npc timer,char listener,char timer", order);

        // a callback that throws: logged as the callback's, and the queue goes on (the next timer of the tick runs)
        p10Ran_.Clear();
        mark = log.Count;
        P10Arm(4, npc);
        Tick(1);
        Check("timers: a callback that throws is logged, the next timer still runs",
              $"quests: {FixtureKey} callback boom: boom|pt:{id}:-", $"{P10Lines(mark)}|{string.Join(",", p10Ran_)}");

        // the arming's refusals: logged by the handler, nothing armed
        p10Ran_.Clear();
        mark = log.Count;
        P10Arm(6, npc);
        P10Arm(7, npc);
        P10Arm(8, npc);
        Tick(2000);
        string refusals = P10Lines(mark);
        Check("timers: a negative delay, another quest's callback, a listener of another event: each refused (logged), nothing run",
              "True|True|True|", $"{refusals.Contains("a timer's delay is 0 ms or more")}|{refusals.Contains("callback foreign is not ")}|" +
                                 $"{refusals.Contains("a quest listener's event is TICK only, not DEATH")}|{string.Join(",", p10Ran_)}");

        // a logout before the deadline: the char's queue goes with it, nothing fires
        p10Ran_.Clear();
        mark = log.Count;
        P10Arm(1, npc);
        Tick(500);
        Logout();
        Tick(2000);
        Check("timers: a logout before the deadline: nothing fires, nothing logged", "|", $"{string.Join(",", p10Ran_)}|{P10Lines(mark)}");

        // an NPC timer with the char, the char gone before it fires: not run, logged
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        p10Ran_.Clear();
        P10Arm(3, npc);
        Logout();
        mark = log.Count;
        Tick(1001);
        Check("timers: WithPlayer.Yes, the char gone (logged out) before it fires: not run, logged",
              $"|quests: {FixtureKey} callback wp: its char {id} is gone", $"{string.Join(",", p10Ran_)}|{P10Lines(mark)}");
        Cleanup(db, id);
        zone.TestQuestScripts.Clear();
        Lua("InteractionGlobal.reload(true)");
    }

    // a TICK listener heard each tick and removed by itself; a char's listeners gone on a zone change, their functions
    // released (the old CharData's handler empty; a Lua listener's closure collected)
    private static void RunP10ListenersAndZoneChange()
    {
        uint id = CharIds[0];
        zone.TestQuestScripts.Add(new P10TimerQuest());
        Lua("InteractionGlobal.reload(true)");
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        uint npc = NpcId("Samia");
        p10Ran_.Clear();
        int baseline = c.Events.Count;
        P10Arm(5, npc);
        string held = $"{c.Events.Count - baseline}/{c.Events.Has("P10_TICK")}";
        for (int i = 0; i < 4; i++) Tick(1000);
        Check("listeners: a TICK listener is heard each tick, removes itself on the third, is not heard after", "1/True|tick,tick,tick|False",
              $"{held}|{string.Join(",", p10Ran_)}|{c.Events.Has("P10_TICK")}");

        // a zone change: both listeners (the C# one, a Lua one holding a weakly kept key) go with the old char
        p10Ran_.Clear();
        P10Arm(5, npc);
        Lua(@"
            __p10probe = setmetatable({}, { __mode = 'k' })
            local key = {}
            __p10probe[key] = true
            player:addListener('TICK', 'P10_LUA', function() local _ = key end)");
        const string Count = "collectgarbage('collect') collectgarbage('collect') local n = 0 for _ in pairs(__p10probe) do n = n + 1 end report(tostring(n))";
        string before = Lua(Count);
        var old = c;
        string oldHeld = $"{old.Events.Has("P10_TICK")}/{old.Events.Has("P10_LUA")}";
        Warp(-280.0f, -16.0f, -137.0f, Zone.BastokMines);
        p10Ran_.Clear();
        Tick(1000);
        Tick(1000);
        Check("listeners: a zone change: the old char's listeners all dropped, the new char has neither, none heard after",
              "True/True|0|False/False|", $"{oldHeld}|{old.Events.Count}|{c.Events.Has("P10_TICK")}/{c.Events.Has("P10_LUA")}|{string.Join(",", p10Ran_)}");
        Check("  ... their Lua functions released: a Lua listener's closure is collected (held 1 before, 0 after)", "1|0",
              $"{before}|{Lua(Count)}");

        // the live host's own paths, which Login / Logout's OnCharTimeout does not take: once the 0x00B is sent,
        // MapNetwork calls ZoneHost.OnZoneOut and the session lets go of the char; a /logout (leaveGame) runs
        // ZoneHost.Logout on the next zone tick first
        Lua("player:addListener('TICK', 'P10_LUA', function() end)");
        var zoned = c;
        Lua(string.Format(CultureInfo.InvariantCulture, "player:setPos({0}, {1}, {2}, 0, {3})", -296.0f, -16.0f, -157.0f, Zone.BastokMarkets));
        Tick(400);
        int zonedHeld = zoned.Events.Count;
        zone.OnZoneOut(s);
        s.Char = null;
        Check("listeners: the live zone-out (OnZoneOut after the 0x00B): the char's listeners dropped", "1|0",
              $"{zonedHeld}|{zoned.Events.Count}");
        Login();
        Lua("player:addListener('TICK', 'P10_LUA', function() end)");
        var leaving = c;
        Lua("player:leaveGame()");
        Tick(400);
        Check("listeners: a /logout (leaveGame, ZoneHost.Logout): the char's listeners dropped before its 0x00B", "True|0",
              $"{s.ShuttingDown == 1}|{leaving.Events.Count}");
        zone.OnZoneOut(s);
        s.Char = null;
        Logout();
        Cleanup(db, id);
        zone.TestQuestScripts.Clear();
        Lua("InteractionGlobal.reload(true)");
    }

    // ---- exports ----

    private const string DancesLua = "scripts/quests/adoulin/Dances_with_Luopans";
    private static int p10Healed_, p10Lost_;

    // a test quest exporting xi.dancesWithLuopans.onHealing / onEffectLose and claiming Dances with Luopans' module
    // (healing.lua calls them by name, and requires the quest's Lua file); refuse: a name declared twice, so the
    // validator refuses it. It takes the fixture's key, not Dances': Dances' own port, once discovered, is then not
    // a second quest of its key (the worlds past the first skip the discovered quests, so it exports nothing there)
    private sealed class P10DancesQuest : QuestScript
    {
        private readonly bool refuse_;

        public P10DancesQuest(bool refuse = false) : base(FixtureLog, FixtureConst) => refuse_ = refuse;

        protected override void Build(QuestScriptBuilder b)
        {
            var heal = b.Callback("onHealing", q =>
            {
                p10Healed_++;
                q.Player.SetLocalVar("P10_HEALED", q.Player.LocalVar("P10_HEALED") + 1);
            });
            var lose = b.Callback("onEffectLose", q => p10Lost_++);
            if (refuse_) b.Callback("onHealing", q => { });
            b.Export("xi.dancesWithLuopans.onHealing", heal, DancesLua);
            b.Export("xi.dancesWithLuopans.onEffectLose", lose, DancesLua);
            b.State(QuestStatus.Accepted).OnZoneOut(Zone.BastokMarkets, q => { });
        }
    }

    // another quest exporting the same global and module (Yet Another Trial in Tandem: a free jeuno key, TestKeysFree;
    // after the fixture's in the registration's key order, so the stand-in registers first)
    private sealed class P10DoubleExportQuest : QuestScript
    {
        public P10DoubleExportQuest() : base(P10OtherLog, P10OtherConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Export("xi.dancesWithLuopans.onHealing", b.Callback("again", q => { }), DancesLua);
            b.State(QuestStatus.Accepted).OnZoneOut(Zone.BastokMarkets, q => { });
        }
    }

    private static void RunP10Exports()
    {
        // the Lua file there (and loaded by the containers' load): the bridge stands down, the Lua's functions stay. The
        // file is gone since B3b Adoulin group 4 (DancesWithLuopans.cs, a discovered quest, claims the module and sets
        // the globals in a world of every quest), so a world of the test quests only stands a Lua module in for it as
        // the file's require left them: package.loaded a Lua table, the globals Lua functions of a chunk named as the
        // file
        NewWorld(null, true);
        Lua($@"
            load([[xi.dancesWithLuopans = {{ onHealing = function(player) end, onEffectLose = function(player) end }}]],
                 '@{DancesLua}.lua')()
            package.loaded['{DancesLua}'] = {{}}");
        zone.TestQuestScripts.Add(new P10DancesQuest());
        Lua("InteractionGlobal.reload(true)");
        Check("exports: the quest's Lua file loaded: the bridge stands down (package.loaded the Lua's, its onHealing the Lua's)",
              "2|true", Lua($"report(CSharpQuests.moduleState('{DancesLua}') .. '|' .. tostring(debug.getinfo(xi.dancesWithLuopans.onHealing, 'S').source:find('Dances_with_Luopans') ~= nil))"));
        zone.TestQuestScripts.Clear();

        // the file gone (hidden from the containers' load, and from require below): the bridge claims the module before
        // any effect script loads; healing.lua then loads, its require answered from package.loaded. Only the test
        // quests registered, so a discovered quest exporting the same names (Dances' port) does not take them
        NewWorld(new[] { DancesLua }, true);
        zone.TestQuestScripts.Add(new P10DancesQuest());
        Lua("InteractionGlobal.reload(true)");
        // require of the module raises from here on unless package.loaded answers it first
        Lua($@"
            table.insert(package.loaders, 1, function(name)
                if name == '{DancesLua}' then error('module ' .. name .. ' not found (P10: the file is gone)') end
            end)");
        Check("exports: the module claimed: package.loaded holds the bridge's sentinel; the globals are C# functions",
              "1|true|true", Lua($@"report(CSharpQuests.moduleState('{DancesLua}') .. '|' ..
                tostring(package.loaded['{DancesLua}'] == CSharpQuests.claimed['{DancesLua}']) .. '|' ..
                tostring(debug.getinfo(xi.dancesWithLuopans.onHealing, 'S').what == 'C' or debug.getinfo(xi.dancesWithLuopans.onHealing, 'S').source == '=xi_wrap'))"));
        Check("  ... without the claim, the require raises (the hidden file's stand-in)", "false|true",
              Lua($@"
                local keep = package.loaded['{DancesLua}']
                package.loaded['{DancesLua}'] = nil
                local ok, err = pcall(require, '{DancesLua}')
                package.loaded['{DancesLua}'] = keep
                report(tostring(ok) .. '|' .. tostring(tostring(err):find('the file is gone') ~= nil))"));
        uint id = CharIds[0];
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        p10Healed_ = p10Lost_ = 0;
        int mark = log.Count;
        Collect();
        pending_.Clear();
        string gained = Lua("report(tostring(player:addStatusEffect(xi.effect.HEALING, { power = 1, origin = player })) .. '|' .. tostring(player:getAnimation() == xi.animation.HEALING))");
        Check("exports: /heal with the file gone: healing.lua loads, the effect and its animation are on, onHealing ran (C#), " +
              "nothing said to a char with no quest, no script error",
              "true|true|1|1|0|", $"{gained}|{p10Healed_}|{Lua("report(tostring(player:getLocalVar('P10_HEALED')))")}|{Of(0x02A).Count}|" +
                                  string.Join(" | ", log.Skip(mark).Where(l => ScriptErrors().Contains(l) || l.StartsWith("quests: "))));
        Lua("player:delStatusEffect(xi.effect.HEALING)");
        Check("  ... the effect's loss: onEffectLose ran (C#)", "1", p10Lost_.ToString());

        // a re-registration (reload(true)) of a refused quest: its own sentinel kept, the globals no-ops that log once
        zone.TestQuestScripts.Clear();
        zone.TestQuestScripts.Add(new P10DancesQuest(refuse: true));
        mark = log.Count;
        Lua("InteractionGlobal.reload(true)");
        string refusedAt = P10Lines(mark);
        p10Healed_ = 0;
        mark = log.Count;
        string twice = Lua(@"
            local a = player:addStatusEffect(xi.effect.HEALING, { power = 1, origin = player })
            player:delStatusEffect(xi.effect.HEALING)
            local b = player:addStatusEffect(xi.effect.HEALING, { power = 1, origin = player })
            player:delStatusEffect(xi.effect.HEALING)
            report(tostring(a) .. '|' .. tostring(b))");
        Check("exports: a refused exporting quest: refused at registration (logged), the module still claimed (1), /heal still " +
              "works twice, its globals no-ops logged once each, its callbacks never run",
              $"True|1|true|true|quests: {FixtureDancesKey} refused: xi.dancesWithLuopans.onHealing not run | quests: {FixtureDancesKey} refused: xi.dancesWithLuopans.onEffectLose not run|0|",
              $"{refusedAt.Contains("callback onHealing declared twice")}|{Lua($"report(CSharpQuests.moduleState('{DancesLua}'))")}|{twice}|" +
              $"{P10Lines(mark)}|{p10Healed_}|" + string.Join(" | ", log.Skip(mark).Where(l => ScriptErrors().Contains(l))));

        // two quests naming one global and module: the second refused
        zone.TestQuestScripts.Clear();
        zone.TestQuestScripts.Add(new P10DancesQuest());
        zone.TestQuestScripts.Add(new P10DoubleExportQuest());
        mark = log.Count;
        Lua("InteractionGlobal.reload(true)");
        string doubled = P10Lines(mark);
        Check("exports: a second quest exporting one global and module is refused (the first keeps them)", "True|True|True",
              $"{doubled.Contains($"export xi.dancesWithLuopans.onHealing: the global is {FixtureDancesKey}'s export")}|" +
              $"{doubled.Contains($"export xi.dancesWithLuopans.onHealing: the module {DancesLua} is {FixtureDancesKey}'s export")}|" +
              $"{!doubled.Contains($"{FixtureDancesKey} not registered")}");

        // two quests of one key: the second refused for the key, and it sets no exports, so the first's stay live
        // (its no-op would otherwise replace them: the ownership checks go by key)
        zone.TestQuestScripts.Clear();
        zone.TestQuestScripts.Add(new P10DancesQuest());
        zone.TestQuestScripts.Add(new P10DancesQuest());
        mark = log.Count;
        Lua("InteractionGlobal.reload(true)");
        string dup = P10Lines(mark);
        p10Healed_ = 0;
        mark = log.Count;
        string healed = Lua(@"
            local a = player:addStatusEffect(xi.effect.HEALING, { power = 1, origin = player })
            player:delStatusEffect(xi.effect.HEALING)
            report(tostring(a))");
        Check("exports: a second quest of one key: refused for the key, the first's exports still run (/heal: onHealing ran)",
              "True|true|1|", $"{dup.Contains($"{FixtureDancesKey} not registered: another C# quest")}|{healed}|{p10Healed_}|{P10Lines(mark)}");
        Logout();
        Cleanup(db, id);
        zone.TestQuestScripts.Clear();
        NewWorld();
    }

    // the Dances stand-in's key: the fixture's (Fixtures/QSL_Fixture.lua)
    private const string FixtureDancesKey = FixtureKey;
}
