using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Persistence;
using XI.QuestContent;
using XI.Script;

// Platform round P11: the Lua module loader (moduleutils.cpp LoadLuaModules; XI.Script.LuaModules, ZoneHost.LuaModules;
// docs/superpowers/specs/2026-11-03-lua-module-loader-design.md) on the live host. A world starts with the test module
// set tests/XI.Verify.QuestScripts.Live/P11Modules (init lines "../tests/...", so "./modules/../tests/..."):
// - the init.txt rules: a comment, a blank line, a lone CR and a trailing blank skipped; a leading blank line names
//   nothing; a missing file;
// - the order: the file lines first, then each folder in line order (path order, recursive). A file in both runs twice.
// - the overrides:
//   - of a global (xi.equipment.vigilWeaponRequiredWsPoints): three wraps in load order (a + 1, a + 1, b * 2);
//   - of an NPC script function (Reet's onTrigger), applied when the zone loads its scripts;
//   - of an item script function (Treat Staff's onItemEquip), applied at its first use, an equip;
//   - again after an overwriting reload of the NPC's script (TryApplyLuaModules' isReload);
// - the targets missing: a table (reported), and a bareKey one (logged as it is tried);
// - a module whose condition is false, and an era case whose content is on;
// - a file that errors (no Module of it registered, and none of it leaks into the next file);
// - a command module, the old command interface, a data file, a module_utils.lua and a .txt (both never run);
// - the modifyInteractionEntry guard: a C# quest's container skipped and logged, a Lua quest's patched;
// - the multi-process filter and the debug lines (a loader of its own on the world's Lua), and through the host's
//   loader: its port the map IPP's, else network.MAP_PORT; the guard put back after a second load;
// - a Module whose enabled is not a boolean (skipped), an applyOverride that raises (not applied);
// - ServerStart's ReportLuaModuleUsage: not on a test server (IsTestServer), else at the error level;
// - the era module modules/era/lua/globals/equipment/unlocking_a_myth.lua through the real loader, off and on (the TODO
//   row's case): Player.VigilWeaponRequiredWsPoints follows it.
internal static partial class Program
{
    private const string P11Base = "../tests/XI.Verify.QuestScripts.Live/P11Modules";

    private static readonly string[] P11Lines =
    {
        "# the P11 test modules",
        "   # a leading blank: not a comment (names nothing)",
        "",
        "\r",
        P11Base + "/order/a.lua \t ",
        P11Base + "/order",
        P11Base + "/misc/",
        P11Base + "/gone.lua",
        P11Base + "/bare.lua",
    };

    private static void RunPlatformP11()
    {
        Console.WriteLine("platform round P11: the Lua module loader");
        try
        {
            RunP11Loader();
            RunP11Ports();
            RunP11RealPorts();
            RunP11Failures();
            RunP11ServerStart();
            RunP11Era();
        }
        finally
        {
            worldModuleLines_ = CommittedModuleLines;
            NewWorld();
        }
    }

    // a run file as the set names it ("order/b.lua")
    private static string P11Short(string entry)
    {
        string prefix = "./modules/" + P11Base + "/";
        return entry.StartsWith(prefix, StringComparison.Ordinal) ? entry.Substring(prefix.Length) : entry;
    }

    private static void RunP11Loader()
    {
        uint id = CharIds[0];
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -237.0f, -12.0f, -41.0f, 0, (17566, 1));
        worldModuleLines_ = P11Lines;
        int mark = log.Count;
        NewWorld();
        var modules = zone.LuaModules;
        Check("p11: the files run in LoadLuaModules' order: the file lines, then each folder's files (path order, recursive); a file in both twice",
              "order/a.lua,bare.lua,order/a.lua,order/b.lua,order/z/c.lua,misc/b_after_boom.lua,misc/cases.lua,misc/command.lua," +
              "misc/data.lua,misc/disabled.lua,misc/item.lua,misc/newfn.lua,misc/npc.lua,misc/oldcmd.lua,misc/patch.lua",
              string.Join(",", modules.FilesRun.Select(P11Short)));
        Check("p11:   ... each file's own code ran in that order (P11Order)", "a,a,b,c", Lua("report(table.concat(P11Order, ','))"));
        Check("p11: the loader's errors: the leading-blank line and the missing file named, the file that errors, the old command, the bareKey target",
              string.Join("|",
                  "moduleutils::LoadLuaModules: init.txt entry does not exist: ./modules/   # a leading blank: not a comment (names nothing)",
                  $"moduleutils::LoadLuaModules: init.txt entry does not exist: ./modules/{P11Base}/gone.lua",
                  "moduleutils::LoadLuaModules: Failed to load module: gone.lua",
                  "moduleutils::LoadLuaModules: Failed to load module: a_boom.lua",
                  "moduleutils::LoadLuaModules: Command module returns a table instead of calling xi.module.registerCommand: oldcmd.lua",
                  "moduleutils::applyOverride: Cannot navigate to override path: xi.canusespell_override.p11 (missing canusespell_override)",
                  "moduleutils::applyOverride: Cannot navigate to override path: equipment.p11 (missing equipment)"),
              string.Join("|", modules.Errors.Where(e => e.StartsWith("moduleutils::", StringComparison.Ordinal))));
        Check("p11:   ... the failed file's error follows its line (a_boom.lua:7: p11 boom)", "True",
              modules.Errors.Any(e => e.Contains("a_boom.lua:7: p11 boom")).ToString());
        Check("p11: the Modules registered, in order: none of the file that errored (its registry emptied before the next file)",
              "p11_order_a,p11_bare,p11_order_a,p11_order_b,p11_order_c,p11_after_boom,p11_cases,p11_disabled,p11_item,p11_newfn,p11_npc,p11_patch",
              string.Join(",", modules.Modules.Select(m => m.Name)));
        var logged = log.Skip(mark).ToList();
        Check("p11: a module whose condition is false: '(disabled)', declared, none applied", "True|True",
              $"{logged.Contains("moduleutils::registerModule: === Module: p11_disabled (disabled) ===")}|" +
              $"{logged.Contains("moduleutils::registerModule: Module p11_disabled: 1 overrides declared, none applied for this configuration")}");
        Check("p11: an era case whose content is on (RESTRICT_CONTENT 0): declared, none applied", "True",
              logged.Contains("moduleutils::registerModule: Module p11_cases: 1 overrides declared, none applied for this configuration").ToString());
        Check("p11: the debug lines are off (logging.DEBUG_MODULES false)", "False",
              logged.Any(l => l.Contains("Declared override")).ToString());
        Check("p11: a command module: xi.commands.p11cmd published (Registering module command: !p11cmd)", "table|True",
              Lua("report(type(xi.commands.p11cmd.cmdprops))") + "|" + logged.Contains("moduleutils::LoadLuaModules: Registering module command: !p11cmd"));
        Check("p11: a data file ran (P11Data), registering nothing; module_utils.lua and the .txt never ran", "loaded",
              Lua("report(tostring(P11Data))"));

        // the global: base 250, then a (+ 1), a (+ 1), b (* 2) in load order; the disabled ones not applied
        Check("p11: a global override, three wraps in load order (a + 1, a + 1, b * 2 over 250), the disabled ones not applied", "504",
              Lua("report(tostring(xi.equipment.vigilWeaponRequiredWsPoints({ getCharVar = function() return 0 end })))"));
        // moduleutils.cpp:85-89 (applyOverride): a nil target gets an empty function first, the override's super
        Check("p11: an override of a function that is not there: its super an empty function, the override run", "function,new",
              Lua("report(type(getfenv(xi.equipment.p11new).super)) report(tostring(xi.equipment.p11new()))"));

        // the report before any zone or item script loaded
        mark = log.Count;
        int errorMark = errorLog.Count;
        zone.ReportLuaModuleUsage();
        Check("p11: ReportLuaModuleUsage: the missing targets; the NPC's and the item's wait for their scripts",
              string.Join("|",
                  "moduleutils::ReportLuaModuleUsage: Override not applied: equipment.p11 (module p11_bare, bare.lua)",
                  "moduleutils::ReportLuaModuleUsage: Override not applied: xi.canusespell_override.p11 (module p11_bare, bare.lua)",
                  "moduleutils::ReportLuaModuleUsage: Override not applied: xi.p11missing.fn (module p11_order_c, c.lua)",
                  "moduleutils::ReportLuaModuleUsage: Override not applied: xi.p11afterboom.fn (module p11_after_boom, b_after_boom.lua)",
                  "moduleutils::ReportLuaModuleUsage: 2 overrides wait for their script's first load"),
              string.Join("|", log.Skip(mark)));
        // ShowErrorFmt: the not-applied lines through the engine's error action (the waiting count is info)
        Check("p11:   ... at the C++ level: the four not-applied lines are errors, the waiting count is not", "4|4|False",
              $"{errorLog.Skip(errorMark).Count(l => l.Contains("Override not applied"))}|{errorLog.Count - errorMark}|" +
              errorLog.Skip(errorMark).Any(l => l.Contains("wait for")).ToString());

        Login();
        try
        {
            Check("p11: an NPC script's function: applied when the zone loaded its scripts (onTrigger wrapped, its super the script's)",
                  "function,function", Lua("local f = xi.zones.Bastok_Markets.npcs.Reet.onTrigger report(type(f)) report(type(getfenv(f).super))"));
            TalkTo("Reet");
            Check("p11:   ... live: talking to Reet runs the override, then his own onTrigger (event 5)", "module;|5",
                  Lua("report(tostring(P11Npc))") + "|" + (c.IsInEvent ? c.CurrentEvent.EventId.ToString() : "-"));
            FinishIfEvent();
            // an overwriting reload of the script (LoadLuaObjectFromFile(file, true)): its new table gets the override
            // again
            Lua("P11Npc = nil");
            scripts.LoadLuaObjectFromFile("./scripts/zones/Bastok_Markets/npcs/Reet.lua", true);
            TalkTo("Reet");
            Check("p11:   ... reloaded with overwrite (isReload): the new table's onTrigger is wrapped again", "module;|5",
                  Lua("report(tostring(P11Npc))") + "|" + (c.IsInEvent ? c.CurrentEvent.EventId.ToString() : "-"));
            FinishIfEvent();

            Check("p11: an item script's function: not applied before the item's first use", "nil", Lua("report(type(xi.items.treat_staff))"));
            Equip(1, 0);
            Collect();
            Check("p11:   ... live: equipping Treat Staff loads its script and runs the override (its super the script's)", "module;,function",
                  Lua("report(tostring(P11Item)) report(type(getfenv(xi.items.treat_staff.onItemEquip).super))"));

            mark = log.Count;
            zone.ReportLuaModuleUsage();
            Check("p11: ReportLuaModuleUsage after: only the missing targets", 4, log.Skip(mark).Count(l => l.Contains("Override not applied")));
            Check("p11:   ... and nothing waits", "False", log.Skip(mark).Any(l => l.Contains("wait for")).ToString());

            // the guard: Elvaan's container is a C# quest (skipped, logged), the shared fixture's a Lua quest (patched;
            // patch.lua reads its file from P11FixtureLuaFile, so a fixture move is the constants' change alone)
            mark = log.Count;
            int skipped = zone.SkippedLuaPatches.Count;
            Lua($"P11FixtureLuaFile = '{FixtureLuaFile}'");
            Lua("xi.server.onServerStart()");
            Check("p11: modifyInteractionEntry of a C# quest's container: skipped, logged; of a Lua quest's: patched", "nil,true",
                  Lua("report(tostring(P11Patch.elvaan)) report(tostring(P11Patch.fixture))"));
            Check("p11:   ... the log line names the module file, the container, the quest and that no C# module ports it",
                  $"modules: tests/XI.Verify.QuestScripts.Live/P11Modules/misc/patch.lua: modifyInteractionEntry('{ElvaanLuaFile}') skipped: " +
                  $"{ElvaanKey} is a C# quest (no enabled C# module ports the file: this Lua patch does not apply)",
                  string.Join("|", log.Skip(mark).Where(l => l.Contains("modifyInteractionEntry"))));
            Check("p11:   ... SkippedLuaPatches holds it", $"1|{ElvaanKey}|",
                  $"{zone.SkippedLuaPatches.Count - skipped}|{zone.SkippedLuaPatches.LastOrDefault()?.Key}|{zone.SkippedLuaPatches.LastOrDefault()?.PortedBy}");
            Check($"p11:   ... {FixtureFile.Replace('_', ' ')} is still in the lookup (removed and added back)", "True",
                  Lua($"report(tostring(InteractionGlobal.lookup.containers['{FixtureContainer}'] == true))").Equals("true").ToString());
        }
        finally
        {
            Logout();
            Cleanup(db, id);
        }
    }

    // the multi-process filter (moduleutils.cpp:180-190) and the debug lines, on a loader of its own: Bastok Markets on
    // another port than this process's, Lower Jeuno on it, a zone zone_settings does not have
    private static void RunP11Ports()
    {
        worldModuleLines_ = Array.Empty<string>();
        NewWorld();
        Lua("xi.settings.logging = xi.settings.logging or {} xi.settings.logging.DEBUG_MODULES = true");
        var lines = new List<string>();
        var own = new LuaModules(lua, Path.GetDirectoryName(dataDir_), lines.Add, lines.Add);
        own.Load(new[] { P11Base + "/ports/zones.lua" },
                 new Dictionary<string, ushort> { ["Bastok_Markets"] = 1, ["Lower_Jeuno"] = 54230 }, 54230);
        Lua("xi.settings.logging.DEBUG_MODULES = false");
        Check("p11: the multi-process filter: an xi.zones.<zone> target on another port is off, on this port or unknown on",
              "xi.zones.Bastok_Markets.npcs.Reet.onTrade=False,xi.zones.Lower_Jeuno.npcs.Vingijard.onTrade=True,xi.zones.P11_Nowhere.npcs.X.onTrade=True",
              string.Join(",", own.Overrides.Select(o => $"{o.Name}={o.Enabled}")));
        Check("p11:   ... the debug lines (logging.DEBUG_MODULES true): the port's and the declared ones",
              "moduleutils::registerModule: Declared override (skipped, Bastok_Markets runs on port 1): xi.zones.Bastok_Markets.npcs.Reet.onTrade|" +
              "moduleutils::registerModule: Declared override: xi.zones.Lower_Jeuno.npcs.Vingijard.onTrade|" +
              "moduleutils::registerModule: Declared override: xi.zones.P11_Nowhere.npcs.X.onTrade",
              string.Join("|", lines.Where(l => l.Contains("Declared override"))));
    }

    // the multi-process filter through the host's own LoadLuaModules (its currentPort): the map IPP's port, else
    // xi.settings.network.MAP_PORT; then the patch guard after that second load (module_utils.lua ran again and put its
    // own modifyInteractionEntry back)
    private static void RunP11RealPorts()
    {
        worldModuleLines_ = Array.Empty<string>();
        NewWorld();
        ushort Port(string name)
        {
            var r = Sql(db, "SELECT zoneport FROM zone_settings WHERE name = ?", BoundValue.Of(name));
            return r != null && r.Next() ? r.Get<ushort>("zoneport") : (ushort)0;
        }
        ushort bm = Port("Bastok_Markets"), lj = Port("Lower_Jeuno");
        string Load(ushort mapPort, ushort ippPort)
        {
            Lua($"xi.settings.network.MAP_PORT = {mapPort}");
            zone.ProcessIpp = (ulong)ippPort << 32;
            // a key's list grows in place: the new overrides are the ones not there before
            var before = new HashSet<LuaModules.Override>(zone.LuaModules.Overrides);
            try
            {
                zone.ModuleInitLines = new[] { P11Base + "/ports/zones.lua" };
                zone.LoadLuaModules();
            }
            finally
            {
                zone.ProcessIpp = 0;
            }
            return string.Join(",", zone.LuaModules.Overrides.Where(o => !before.Contains(o)).Select(o => o.Enabled.ToString()));
        }
        Check("p11: the host's loader, no map IPP: network.MAP_PORT is the port (Lower_Jeuno's port: on; one more: off)",
              $"{bm == lj},True,True|{bm == lj + 1},False,True", $"{Load(lj, 0)}|{Load((ushort)(lj + 1), 0)}");
        Check("p11:   ... a map IPP: its port, not network.MAP_PORT", $"{bm == lj},True,True", Load((ushort)(lj + 1), lj));
        Lua("xi.settings.network.MAP_PORT = 54230");

        int skipped = zone.SkippedLuaPatches.Count;
        Check("p11: the patch guard put back after a second load (module_utils.lua ran again)", "true",
              Lua("report(tostring(xi.module.modifyInteractionEntry == CSharpQuests.patchGuard))"));
        Lua($"pcall(xi.module.modifyInteractionEntry, '{ElvaanLuaFile}', function() end)");
        Check("p11:   ... and it skips a C# quest's container", $"1|{ElvaanKey}",
              $"{zone.SkippedLuaPatches.Count - skipped}|{zone.SkippedLuaPatches.LastOrDefault()?.Key}");
    }

    // registerModule's non-boolean enabled, and applyOverride raising (module_utils' applyOverride on a func that is
    // not a function), on a loader of its own
    private static void RunP11Failures()
    {
        worldModuleLines_ = Array.Empty<string>();
        NewWorld();
        var lines = new List<string>();
        var own = new LuaModules(lua, Path.GetDirectoryName(dataDir_), lines.Add, lines.Add);
        own.Load(new[] { P11Base + "/fail/notbool.lua", P11Base + "/fail/applyfail.lua" }, null, 0);
        own.TryApplyRemaining();
        Check("p11: a Module whose enabled is not a boolean: an error, the module skipped (no Module, no override)",
              "moduleutils::registerModule: Module p11_notbool has a non-boolean 'enabled' field, skipping module (notbool.lua)|" +
              "p11_applyfail|xi.p11applyfail.fn",
              $"{own.Errors.FirstOrDefault(e => e.Contains("p11_notbool"))}|{string.Join(",", own.Modules.Select(m => m.Name))}|" +
              string.Join(",", own.Overrides.Select(o => o.Name)));
        var failed = own.Overrides.Single(o => o.Name == "xi.p11applyfail.fn");
        Check("p11: applyOverride raising: 'applyOverride failed' logged, the override not applied (and reported)",
              "True|False|1",
              $"{own.Errors.Any(e => e.StartsWith("moduleutils::applyOverride: applyOverride failed for xi.p11applyfail.fn: ", StringComparison.Ordinal))}|" +
              $"{failed.Applied}|{own.NotApplied().Count}");
    }

    // ServerStart's ReportLuaModuleUsage (map_engine.cpp:276-279): left out on a test server, else after
    // xi.server.onServerStart at the error level
    private static void RunP11ServerStart()
    {
        worldModuleLines_ = new[] { P11Base + "/bare.lua" };
        NewWorld();
        int mark = log.Count;
        zone.IsTestServer = true;
        zone.ServerStart();
        Check("p11: ServerStart on a test server (IsTestServer): no ReportLuaModuleUsage", "0",
              log.Skip(mark).Count(l => l.Contains("ReportLuaModuleUsage")).ToString());
        int errorMark = errorLog.Count;
        zone.IsTestServer = false;
        zone.ServerStart();
        Check("p11: ServerStart: ReportLuaModuleUsage, its two not-applied lines errors",
              "moduleutils::ReportLuaModuleUsage: Override not applied: equipment.p11 (module p11_bare, bare.lua)|" +
              "moduleutils::ReportLuaModuleUsage: Override not applied: xi.canusespell_override.p11 (module p11_bare, bare.lua)",
              string.Join("|", errorLog.Skip(errorMark).Where(l => l.Contains("ReportLuaModuleUsage"))));
    }

    // the era module through the real loader (ZoneHost.LoadLuaModules after the settings, as the golden harness's
    // module lines): off with the content on (RESTRICT_CONTENT 0), on with ABYSSEA off; the member and the Lua global
    // by NyzulFloorProgress, and the twenty Vigil weapons' onItemEquip
    private static void RunP11Era()
    {
        const string era = "era/lua/globals/equipment/unlocking_a_myth.lua";
        uint id = CharIds[0];
        foreach (bool on in new[] { false, true })
        {
            worldModuleLines_ = Array.Empty<string>();
            NewWorld();
            Lua(on ? "xi.settings.main.RESTRICT_CONTENT = 1 xi.settings.main.ENABLE_ABYSSEA = 0" : "xi.settings.main.RESTRICT_CONTENT = 0");
            zone.ModuleInitLines = new[] { era };
            zone.LoadLuaModules();
            var m = zone.LuaModules.Modules.Single(x => x.Name == "unlocking_a_myth");
            zone.TestQuestScripts.Add(new JG15Quest());
            Lua("InteractionGlobal.reload(true)");
            Cleanup(db, id);
            Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
            Login();
            try
            {
                string Read(int floor)
                {
                    Lua($"player:setCharVar('NyzulFloorProgress', {floor})");
                    Lua("player:setLocalVar('JG15', 1)");
                    TalkTo("Samia");
                    FinishIfEvent();
                    Lua("player:setLocalVar('JG15', 0)");
                    return $"{JG15Quest.Points}/{Lua("report(tostring(xi.equipment.vigilWeaponRequiredWsPoints(player)))")}";
                }
                string values = $"{Read(0)},{Read(80)},{Read(100)}";
                int applied = zone.LuaModules.Overrides.Count(o => o.ModuleName == "unlocking_a_myth" && o.Applied);
                if (!on)
                {
                    Check("p11: unlocking_a_myth with the content on: '(disabled)', no override applied", "False|21|0",
                          $"{m.Enabled}|{m.Declared}|{applied}");
                    Check("p11:   ... the member and the global the base 250 (NyzulFloorProgress 0, 80, 100)", "250/250,250/250,250/250", values);
                }
                else
                {
                    Check("p11: unlocking_a_myth under RESTRICT_CONTENT 1, ENABLE_ABYSSEA 0: all 21 overrides applied", "True|21|21",
                          $"{m.Enabled}|{m.Declared}|{applied}");
                    Check("p11:   ... the member follows the era global (NyzulFloorProgress 0: 16000, 80: 880, 100: 250)",
                          "16000/16000,880/880,250/250", values);
                    Check("p11:   ... the Vigil weapons' onItemEquip (ensureTable's tables, applied at the start): Sturdy Axe's a function",
                          "function", Lua("report(type(xi.items.sturdy_axe.onItemEquip))"));
                }
            }
            finally
            {
                Lua("player:setCharVar('NyzulFloorProgress', 0) player:setLocalVar('JG15', 0)");
                Logout();
                Cleanup(db, id);
                zone.TestQuestScripts.Clear();
            }
        }
    }
}
