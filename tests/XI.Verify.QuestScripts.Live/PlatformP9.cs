using System;
using System.Collections.Generic;
using System.Linq;
using XI.QuestContent;
using XI.Quests;
using XI.Quests.Authoring;

// quest rework P9: the live Settings facet keeps a setting's Lua type (MainValue) and compares it as Lua does (MainEquals /
// MainTruthy / MainFlag), each read against what the Lua itself computes from the same xi.settings.main value; the hidden
// quest kind live (HiddenQuest:new); a quest's onTriggerAreaLeave live
internal static partial class Program
{
    private static void RunPlatformP9()
    {
        Console.WriteLine("quest rework P9: settings compared as Lua");
        RunP9Settings();
        RunP9Hidden();
        RunP9TriggerAreaLeave();
    }

    // what the test quest's handler read last: "<MainValue>|<MainEquals(1)>/<MainTruthy>/<MainFlag>|<Main>/<MainNumber>"
    private static string p9Read_ = "not run";

    // A. a test quest of the fixture's key whose onZoneOut reads the settings; its entry in the lookup is called directly
    // with xi.settings.main.P9_SETTING set to each kind of Lua value in turn
    private static void RunP9Settings()
    {
        uint id = CharIds[0];
        zone.TestQuestScripts.Add(new P9SettingsQuest());
        Lua("InteractionGlobal.reload(true)");
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        // the C# reading, then Lua's own: `v == 1`, `if v then`, `v == true`
        string Read(string luaValue)
        {
            p9Read_ = "not run";
            string lua = Lua($@"
                xi.settings.main.P9_SETTING = {luaValue}
                local v = xi.settings.main.P9_SETTING
                for _, e in ipairs(InteractionGlobal.lookup.data[xi.zone.BASTOK_MARKETS].onZoneOut[1] or {{}}) do
                    if e.container and e.container.id == '{FixtureContainer}' then e.handler(player) end
                end
                xi.settings.main.P9_SETTING = nil
                report(tostring(v == 1) .. '/' .. tostring(v and true or false) .. '/' .. tostring(v == true))");
            return $"{p9Read_}#lua {lua}";
        }
        Check("settings: the number 1: equal to 1, truthy, not true; as Lua", "1|true/true/false|1/1#lua true/true/false", Read("1"));
        Check("  true: a boolean, never 1 (Lua's true == 1 is false); Main still reads 1", "true|false/true/true|1/1#lua false/true/true", Read("true"));
        Check("  false: not truthy", "false|false/false/false|0/0#lua false/false/false", Read("false"));
        Check("  1.25: not 1 (Main rounds it to 1)", "1.25|false/true/false|1/1.25#lua false/true/false", Read("1.25"));
        Check("  0: truthy in Lua", "0|false/true/false|0/0#lua false/true/false", Read("0"));
        Check("  the string '1': a string, not the number 1", "\"1\"|false/true/false|0/0#lua false/true/false", Read("'1'"));
        Check("  unset: nil", "nil|false/false/false|0/0#lua false/false/false", Read("nil"));
        Check("  a table: other, truthy", "other|false/true/false|0/0#lua false/true/false", Read("{}"));
        Logout();
        Cleanup(db, id);
        zone.TestQuestScripts.Clear();
        Lua("InteractionGlobal.reload(true)");

        // a module's condition reads the settings through its own reader (ZoneHost.QuestModules.cs ModuleSettings): the
        // MainValue of RESTRICT_CONTENT and xi.pre(ROV), both halves (RESTRICT_CONTENT and ENABLE_ROV), each against
        // module_utils.lua's own xi.pre run on the same values
        string Pre(string restrict, string rov)
        {
            string lua = Lua($@"
                if xi.pre == nil then require('modules/module_utils') end
                xi.settings.main.RESTRICT_CONTENT = {restrict}
                xi.settings.main.ENABLE_ROV = {rov}
                report(tostring(xi.pre(xi.expansion.ROV)))");
            string cs = zone.ModuleSettingRead("RESTRICT_CONTENT", QuestExpansion.Rov);
            return $"{cs}#lua {lua}";
        }
        string keepRestrict = Lua("report(tostring(xi.settings.main.RESTRICT_CONTENT))");
        string keepRov = Lua("report(tostring(xi.settings.main.ENABLE_ROV))");
        try
        {
            Check("settings in a module's condition (xi.pre): RESTRICT_CONTENT 1, ROV unset: restricted, as Lua", "1|True#lua true", Pre("1", "nil"));
            Check("  RESTRICT_CONTENT true: restricted (== true)", "true|True#lua true", Pre("true", "nil"));
            Check("  RESTRICT_CONTENT 1.25: not 1, not true: not restricted", "1.25|False#lua false", Pre("1.25", "nil"));
            Check("  RESTRICT_CONTENT the string '1': not restricted", "\"1\"|False#lua false", Pre("'1'", "nil"));
            Check("  RESTRICT_CONTENT 0: not restricted", "0|False#lua false", Pre("0", "nil"));
            Check("  RESTRICT_CONTENT unset: not restricted", "nil|False#lua false", Pre("nil", "nil"));
            Check("  ENABLE_ROV 1: the content on: not restricted", "1|False#lua false", Pre("1", "1"));
            Check("  ENABLE_ROV true: on (== true)", "1|False#lua false", Pre("1", "true"));
            Check("  ENABLE_ROV 1.25: not 1, not true: off: restricted", "1|True#lua true", Pre("1", "1.25"));
            Check("  ENABLE_ROV the string '1': off: restricted", "1|True#lua true", Pre("1", "'1'"));
            Check("  ENABLE_ROV 0: off: restricted", "1|True#lua true", Pre("1", "0"));
        }
        finally
        {
            Lua($"xi.settings.main.RESTRICT_CONTENT = {LuaLiteral(keepRestrict)}; xi.settings.main.ENABLE_ROV = {LuaLiteral(keepRov)}");
        }
    }

    // a value report(tostring(v)) gave back as a Lua literal: nil, a boolean or a number
    private static string LuaLiteral(string reported) => reported is "nil" or "true" or "false" ? reported :
        double.TryParse(reported, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _) ? reported : $"'{reported}'";

    private sealed class P9SettingsQuest : QuestScript
    {
        public P9SettingsQuest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .OnZoneOut(Zone.BastokMarkets, q =>
                {
                    var s = q.Settings;
                    string B(bool v) => v ? "true" : "false";
                    p9Read_ = $"{s.MainValue("P9_SETTING")}|{B(s.MainEquals("P9_SETTING", 1))}/{B(s.MainTruthy("P9_SETTING"))}/" +
                              $"{B(s.MainFlag("P9_SETTING"))}|{s.Main("P9_SETTING")}/" +
                              s.MainNumber("P9_SETTING").ToString(System.Globalization.CultureInfo.InvariantCulture);
                });
        }
    }

    // ---- Part B: the hidden quest kind live ----

    // the test hidden quest's arm, by the char var P9H: 1 sets HQuest[qslHiddenFixture]Count 5; 2 completes; 3 reads
    // the status (it throws); its section holds while Prog is 1 (the check reads the vars the lookup passes, the second
    // argument)
    private sealed class P9HiddenLive : HiddenQuestScript
    {
        public P9HiddenLive() : base(HiddenFixtureName) { }

        public override string LegacyPath => HiddenFixtureLuaFile;

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { KeyItem = KeyItem.PortalCharm });
            b.Steps(1);
            b.AnyState(when: q => q.Quest.Step == 1)
                .OnZoneOut(Zone.BastokMarkets, q =>
                {
                    switch (q.Player.CharVar("P9H"))
                    {
                        case 1: q.Quest.SetVar("Count", 5); break;
                        case 2: q.Player.SetCharVar("P9H_DONE", q.Quest.Complete() ? 1 : 2); break;
                        case 3: q.Player.SetCharVar("P9H_DONE", (int)q.Quest.Status + 10); break;
                    }
                });
        }
    }

    // the kind live: Portal Charm's container in the lookup's list under its key; a test hidden quest in the hidden Lua
    // fixture's place (Fixtures/QSL_Hidden_Fixture.lua: Mimeo Stone, the last hidden quest on Lua, went to C# in B3b
    // Deferred 1): its id HQuest[qslHiddenFixture], its check given (player, vars, varCache), its vars under
    // HQuest[qslHiddenFixture], its completion npcUtil.giveReward then the cleanup (as a Lua HiddenQuest's), its status
    // refused
    private static void RunP9Hidden()
    {
        Check("hidden: Portal Charm is live in C# as hiddenQuests.portal_charm, its legacy path too",
              "hiddenQuests.portal_charm|hiddenQuests.portal_charm",
              Lua("report(tostring(CSharpQuests.live['HQuest[portalCharm]']) .. '|' .. tostring(CSharpQuests.legacy['scripts/quests/hiddenQuests/Portal_Charm']))"));
        Check("  Mimeo Stone is live in C# as hiddenQuests.mimeo_stone (no Lua hidden quest is left)",
              "hiddenQuests.mimeo_stone|hiddenQuests.mimeo_stone",
              Lua("report(tostring(CSharpQuests.live['HQuest[mimeoStone]']) .. '|' .. tostring(CSharpQuests.legacy['scripts/quests/hiddenQuests/Mimeo_Stone']))"));
        // the fixture's Lua container first (its file's), then the test quest in its slot
        string FixtureOwner() => Lua($"report(tostring(InteractionGlobal.lookup.containers['{HiddenFixtureContainer}'] and " +
                                     $"(CSharpQuests.live['{HiddenFixtureContainer}'] or package.loaded['{HiddenFixtureLuaFile}'].filename)))");
        Check("  the hidden Lua fixture: a Lua HiddenQuest container (HQuest[qslHiddenFixture]) of its file", HiddenFixtureLuaFile,
              FixtureOwner());
        uint id = CharIds[0];
        zone.TestQuestScripts.Add(new P9HiddenLive());
        Lua("InteractionGlobal.reload(true)");
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        Check("  the test hidden quest replaces the hidden Lua fixture's container (HQuest[qslHiddenFixture])", HiddenFixtureKey,
              FixtureOwner());
        const string Entry = @"
            local entry
            for _, e in ipairs(InteractionGlobal.lookup.data[xi.zone.BASTOK_MARKETS].onZoneOut[1] or {}) do
                if e.container and e.container.id == 'HQuest[qslHiddenFixture]' then entry = e end
            end";
        // the check as the lookup calls a hidden quest's: (player, containerVars, varCache), no status
        Check("  its check gets (player, vars, varCache): Prog 1 holds, Prog 0 does not; the container is a HiddenQuest",
              "true|false|HQuest[qslHiddenFixture]|true",
              Lua(Entry + @"
                local a = entry.check(player, { Prog = 1 }, {})
                local b = entry.check(player, { Prog = 0 }, {})
                report(tostring(a) .. '|' .. tostring(b) .. '|' .. entry.container.varPrefix .. '|' .. tostring(getmetatable(entry.container) == HiddenQuest))"));
        Lua("player:setCharVar('P9H', 1)");
        Lua(Entry + " entry.handler(player)");
        Check("  Quest.SetVar writes HQuest[qslHiddenFixture]Count, as quest:setVar on HiddenQuest:new('qslHiddenFixture')", "5|5",
              $"{Var("HQuest[qslHiddenFixture]Count")}|" + Lua("report(tostring(HiddenQuest:new('qslHiddenFixture'):getVar(player, 'Count')))"));
        Lua("player:setCharVar('P9H', 2)");
        Lua(Entry + " entry.handler(player)");
        Check("  Quest.Complete: true, the reward's key item given, HQuest[qslHiddenFixture] vars cleared (giveReward, then cleanup; '-': no row)",
              "1|true|-",
              $"{Var("P9H_DONE")}|" + Lua("report(tostring(player:hasKeyItem(xi.keyItem.PORTAL_CHARM)))") + $"|{Var("HQuest[qslHiddenFixture]Count")}");
        // the same on a Lua hidden quest: the same effects
        Check("  a Lua HiddenQuest's complete does the same (true, its key item, its vars cleared)", "true|true|0",
              Lua(@"
                local lq = HiddenQuest:new('p9Lua')
                lq.reward = { keyItem = xi.keyItem.MAP_OF_THE_SAN_DORIA_AREA }
                lq:setVar(player, 'Count', 5)
                local done = lq:complete(player)
                report(tostring(done) .. '|' .. tostring(player:hasKeyItem(xi.keyItem.MAP_OF_THE_SAN_DORIA_AREA)) .. '|' .. tostring(player:getCharVar('HQuest[p9Lua]Count')))"));
        Lua("player:setCharVar('P9H', 3) player:setCharVar('P9H_DONE', 0)");
        int mark = log.Count;
        Lua(Entry + " entry.handler(player)");
        Check("  Quest.Status throws in a hidden quest (logged; nothing set)",
              $"quests: {HiddenFixtureKey} onZoneOut: a hidden quest has no status (HiddenQuest:new has no quest log or id)|-",
              string.Join(" | ", log.Skip(mark).Where(l => l.StartsWith("quests: "))) + $"|{Var("P9H_DONE")}");
        Lua("player:setCharVar('P9H', 0) player:setCharVar('P9H_DONE', 0)");
        Logout();
        Cleanup(db, id);
        zone.TestQuestScripts.Clear();
        Lua("InteractionGlobal.reload(true)");
    }

    // C. onTriggerAreaLeave live: a test quest of the fixture's key with Bastok Markets areas 903 (an enter that sets a local
    // var, a leave that replies with event 9994 when its q.TriggerAreaId is 903) and 904 (a leave with a text, refused), away
    // from the areas of RunTriggerAreaReply. Walked in: the enter only; walked out: the leave's event, the char's own (no NPC)
    private sealed class P9LeaveQuest : QuestScript
    {
        public const int Area = 903, TextArea = 904;
        public P9LeaveQuest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available)
                .OnTriggerAreaEnter(Zone.BastokMarkets, Area, q => q.Player.SetLocalVar("P9_ENTER", q.TriggerAreaId))
                .OnTriggerAreaLeave(Zone.BastokMarkets, Area, q => q.TriggerAreaId == Area ? QuestReply.ProgressEvent(9994) : null)
                .OnTriggerAreaLeave(Zone.BastokMarkets, TextArea, q => QuestReply.MessageText("ITEM_OBTAINED"));
        }
    }

    private static void RunP9TriggerAreaLeave()
    {
        Console.WriteLine("quest rework P9: onTriggerAreaLeave");
        zone.TestQuestScripts.Add(new P9LeaveQuest());
        Lua("InteractionGlobal.reload(true)");
        uint id = CharIds[0];
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        Check("leave: the lookup holds the C# leave under onTriggerAreaLeave[903] and [904], none under onTriggerAreaEnter[904]", "1|1|0",
              Lua($@"
                local d = InteractionGlobal.lookup.data[xi.zone.BASTOK_MARKETS]
                local function n(t) local c = 0 for _, e in ipairs(t or {{}}) do if e.container and e.container.id == '{FixtureContainer}' then c = c + 1 end end return c end
                report(n(d.onTriggerAreaLeave and d.onTriggerAreaLeave[903]) .. '|' .. n(d.onTriggerAreaLeave and d.onTriggerAreaLeave[904]) .. '|' ..
                       n(d.onTriggerAreaEnter and d.onTriggerAreaEnter[904]))"));
        Lua("local z = GetZone(xi.zone.BASTOK_MARKETS) " +
            $"z:registerCuboidTriggerArea({P9LeaveQuest.Area}, -282, -18, -139, -278, -14, -135) " +
            $"z:registerCuboidTriggerArea({P9LeaveQuest.TextArea}, -282, -18, -129, -278, -14, -125)");
        // past xi.player.onGameIn's 2.5 s ZoningIn: no trigger area is entered while it is set
        Tick(3000);
        Collect();
        pending_.Clear();
        WalkTo(-280.0f, -16.0f, -137.0f);
        Check("  walked into 903: the enter ran (its q.TriggerAreaId 903), no event", "903/none",
              Lua("report(tostring(player:getLocalVar('P9_ENTER')))") + $"/{EvId()}");
        int mark = log.Count;
        WalkTo(-296.0f, -16.0f, -137.0f);
        Check("  walked out: the leave's reply, event 9994, the char's own (no NPC); no quests: line", $"{id}:9994|",
              $"{EvId()}|" + string.Join(" | ", log.Skip(mark).Where(l => l.StartsWith("quests: "))));
        Finish();
        WalkTo(-280.0f, -16.0f, -127.0f);
        Collect();
        pending_.Clear();
        mark = log.Count;
        WalkTo(-296.0f, -16.0f, -127.0f);
        Check("  a leave's reply with a text the NPC shows is refused: no event, and logged", "none/True",
              $"{EvId()}/{log.Skip(mark).Any(l => l.Contains("a MessageText reply needs an NPC to show it, and a trigger area has none"))}");
        Lua("player:setLocalVar('P9_ENTER', 0)");
        Logout();
        Cleanup(db, id);
        zone.TestQuestScripts.Clear();
        Lua("InteractionGlobal.reload(true)");
    }
}
