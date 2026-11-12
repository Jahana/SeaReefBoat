using System;
using System.Collections.Generic;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

// quest rework C3 PR1: modules/era/lua/globals/testimony_single_use.lua overrides Battlefield.onBattlefieldEnter (the
// framework's method, through module_utils.lua's applyOverride). A Lua content reaches the override through its
// metatable; a C# content's proxy runs the C# hook, and the C# framework's OnBattlefieldEnter runs the Lua override
// chain with the C# framework as its last super (IBattlefieldEngine.ModuleOverride). The same entry on Shattering
// Stars (MNK), the Lua file and a test-only C# copy of its fields, with the module loaded and without it, over a fake
// player and battlefield that log each call: the C# content makes the Lua content's calls in both worlds, and under
// the module the Monk's Testimony is fully worn (2 uses the module's, the third the framework's: 1 left shown, 0 left)
internal static partial class Program
{
    private const string TestimonyModule = "era/lua/globals/testimony_single_use.lua";

    // Shattering Stars (MNK)'s :new fields as the Lua file gives them, its groups and entryRequirement left out
    private sealed class MnkTestimonyBattlefield : BattlefieldScript
    {
        public MnkTestimonyBattlefield() : base("SHATTERING_STARS_MNK") { }

        public override BattlefieldData Data { get; } = new BattlefieldData
        {
            ZoneId = QC.Zone.BalgasDais, BattlefieldId = QC.Battlefield.ShatteringStarsMnk, MaxPlayers = 1,
            LevelCapSetting = "MAX_LEVEL", AllowSubjob = false, CleanupDebuffs = true, TimeLimit = 10 * 60,
            Index = 5, EntryNpcs = new[] { "BC_Entrance" }, ExitNpcs = new[] { "Burning_Circle" },
            RequiredItems = new BattlefieldRequiredItems
            {
                Items = new[] { QC.Item.MonksTestimony },
                WearMessage = QC.Text.BalgasDais.TESTIMONY_WEARS,
                WornMessage = QC.Text.BalgasDais.TESTIMONY_IS_TORN,
            },
        };
    }

    // contents[101]:onBattlefieldEnter(player, battlefield) over fakes: the player (id 9001, holding the testimony at
    // wear 0) and the battlefield (initiator 9001) log each method called with its arguments
    private static string TestimonyEntry() => Lua($@"
        local said = {{}}
        local wear = 0
        local function args(...)
            local t = {{}}
            for i = 1, select('#', ...) do t[#t + 1] = tostring((select(i, ...))) end
            return table.concat(t, ',')
        end
        local answers = {{
            getID = function() return 9001 end,
            hasItem = function() return true end,
            getWornUses = function() return wear end,
            incrementItemWear = function() wear = wear + 1 return wear end,
            getInitiator = function() return 9001, 'Initiator' end,
        }}
        local function fake(kind)
            return setmetatable({{}}, {{ __index = function(t, name)
                return function(self, ...)
                    said[#said + 1] = kind .. ':' .. name .. '(' .. args(...) .. ')'
                    local answer = answers[name]
                    if answer then return answer() end
                end
            end }})
        end
        local c = xi.battlefield.contents[{QC.Battlefield.ShatteringStarsMnk}]
        c:onBattlefieldEnter(fake('player'), fake('battlefield'))
        report(tostring(c.filename) .. '|' .. wear .. '|' .. table.concat(said, ' '))");

    // under the module: the innermost override's super (the Lua framework's own method) wrapped to count its calls in
    // pr1LuaFramework (a C# content's seam keeps the wrapper as the super of any other content)
    private const string CountLuaFramework = @"
        local f = Battlefield.onBattlefieldEnter
        local inner = nil
        while type(f) == 'function' do
            local env = getfenv(f)
            local s = type(env) == 'table' and rawget(env, 'super') or nil
            if type(s) ~= 'function' then break end
            inner = env
            f = s
        end
        local own = rawget(inner, 'super')
        pr1LuaFramework = 0
        rawset(inner, 'super', function(...)
            pr1LuaFramework = pr1LuaFramework + 1
            return own(...)
        end)";

    // the four entries: the Lua content and the C# one, without the module and with it
    private static void RunC3Testimony()
    {
        Console.WriteLine("quest rework C3 PR1: testimony_single_use on a C# battlefield");
        var got = new Dictionary<string, string>();
        foreach (bool csharp in new[] { false, true })
            foreach (bool module in new[] { false, true })
            {
                worldModuleLines_ = Array.Empty<string>();
                NewWorld();
                if (module)
                {
                    Lua("xi.settings.main.RESTRICT_CONTENT = 1 xi.settings.main.ENABLE_ABYSSEA = 0");
                    zone.ModuleInitLines = new[] { TestimonyModule };
                    zone.LoadLuaModules();
                }
                if (csharp) zone.TestQuestScripts.Add(new MnkTestimonyBattlefield());
                Lua("InteractionGlobal.reload(true)");
                string key = $"{(csharp ? "csharp" : "lua")} {(module ? "module" : "plain")}";
                if (module) Lua(CountLuaFramework);
                got[key] = TestimonyEntry();
                if (module)
                {
                    Check($"c3 PR1: {key}: the module is applied (Battlefield.onBattlefieldEnter has a super)", "true",
                          Lua("report(tostring(type(rawget(getfenv(Battlefield.onBattlefieldEnter), 'super')) == " +
                              "'function'))"));
                    // the Lua content's last super is the Lua framework's method; the C# content's is the C# one
                    Check($"c3 PR1: {key}: the Lua framework's onBattlefieldEnter ran {(csharp ? 0 : 1)} time(s)",
                          csharp ? "0" : "1", Lua("report(tostring(pr1LuaFramework))"));
                }
                zone.TestQuestScripts.Clear();
            }
        worldModuleLines_ = CommittedModuleLines;
        NewWorld();

        string lua = "scripts/battlefields/Balgas_Dais/shattering_stars_mnk";
        string cs = $"csharp:{QC.BattlefieldKey.ShatteringStarsMnk}";
        int wears = TextId(QC.Zone.BalgasDais, "TESTIMONY_WEARS");
        string wearMessage(int shown, int left) =>
            $"player:messageSpecial({wears},{QC.Item.MonksTestimony},{shown},{left})";
        Check("c3 PR1: the Lua content without the module: one use (3 shown, 2 left)", $"{lua}|1|True",
              Head(got["lua plain"]) + "|" + got["lua plain"].Contains(wearMessage(3, 2)));
        Check("c3 PR1: the Lua content under the module: fully worn (1 shown, 0 left)", $"{lua}|3|True",
              Head(got["lua module"]) + "|" + got["lua module"].Contains(wearMessage(1, 0)));
        Check("c3 PR1: the C# content without the module: the Lua content's calls", $"{cs}|{Tail(got["lua plain"])}",
              got["csharp plain"]);
        Check("c3 PR1: the C# content under the module: the Lua content's calls (the module's, then the framework's)",
              $"{cs}|{Tail(got["lua module"])}", got["csharp module"]);
    }

    // a TestimonyEntry result's file and wear count; the rest after the file
    private static string Head(string entry)
    {
        var parts = entry.Split('|');
        return parts.Length < 2 ? entry : $"{parts[0]}|{parts[1]}";
    }

    private static string Tail(string entry)
    {
        int at = entry.IndexOf('|');
        return at < 0 ? entry : entry.Substring(at + 1);
    }
}
