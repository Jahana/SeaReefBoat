using System;
using System.IO;
using System.Linq;
using System.Threading;
using XI.QuestContent;
using XI.Script;

// P11 item 2: the file watcher (common/filewatcher.cpp, luautils::TryReloadFilewatchList; XI.Script.ScriptFileWatcher,
// ScriptEngine.ReloadLuaObjectFromFile, ZoneHost.FileWatch). USER RULING 2026-11-05: opt in, XI.Host only; this world
// turns it on for itself (ZoneHost.EnableFileWatcher), on the worktree's real scripts/, modules/ and settings/.
// Probe files the check writes and deletes again (never a tracked file): scripts/globals/p11w_global.lua (a global,
// loaded at the world's start), scripts/items/p11w_probe.lua (an item script, loaded by hand as at its first use) and
// modules/p11w_probe.lua (a module file, not in init.txt). The test module P11Modules/watch/watch.lua overrides a
// function of each probe.
// - On: an edited global and an edited item script are reloaded on the next 2400 ms tick, their override applied
//   again over the new function (isReload); a new module file runs, its registry entries dropped, nothing
//   registered; then, on a watcher over no folder, the changes put in by hand (Enqueue): a reload that throws (a file
//   held open) logged and the next drain unharmed; the other kinds: module_utils.lua (the patch guard put
//   back), a settings file (every settings file run again), a zone's IDs.lua (its zone alone), a Lua quest container
//   (removed and added back), a C# quest's (left alone), a helpers file; the drain's de-duplication and order; the
//   function cache (zoneHasTick_) cleared by a drain.
// - Off (the default): an edited file is not reloaded, no watcher exists, nothing is logged.
internal static partial class Program
{
    private const string P11wGlobal = "scripts/globals/p11w_global.lua";
    private const string P11wItem = "scripts/items/p11w_probe.lua";
    private const string P11wModule = "modules/p11w_probe.lua";

    private static string P11wRoot => Path.GetDirectoryName(dataDir_);
    private static string P11wPath(string rel) => Path.Combine(P11wRoot, rel);

    private static void P11wWriteGlobal(string v) => File.WriteAllText(P11wPath(P11wGlobal),
        "xi = xi or {}\nxi.p11w_global = xi.p11w_global or {}\n" +
        $"xi.p11w_global.fn = function() return '{v}' end\n");

    private static void P11wWriteItem(string v) => File.WriteAllText(P11wPath(P11wItem),
        $"local itemObject = {{}}\nitemObject.onProbe = function() return '{v}' end\nreturn itemObject\n");

    private static void P11wDeleteProbes()
    {
        foreach (var f in new[] { P11wGlobal, P11wItem, P11wModule })
            if (File.Exists(P11wPath(f))) File.Delete(P11wPath(f));
    }

    // the 2400 ms ticks until done holds (the watcher's events come in on its own thread), at most about 5 s
    private static bool P11wTicksUntil(Func<bool> done)
    {
        for (int i = 0; i < 100; i++)
        {
            Thread.Sleep(50);
            Tick(2400);
            if (done()) return true;
        }
        return false;
    }

    private static void RunPlatformP11Watch()
    {
        Console.WriteLine("P11 item 2: the file watcher (opt in)");
        uint id = CharIds[0];
        P11wDeleteProbes();
        try
        {
            RunP11wOn(id);
            RunP11wOff();
        }
        finally
        {
            zone.DisableFileWatcher();
            P11wDeleteProbes();
            Logout();
            Cleanup(db, id);
            worldModuleLines_ = CommittedModuleLines;
            NewWorld();
        }
    }

    private static void RunP11wOn(uint id)
    {
        P11wWriteGlobal("v1");
        P11wWriteItem("v1");
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        worldModuleLines_ = new[] { P11Base + "/watch/watch.lua" };
        NewWorld();
        zone.EnableFileWatcher();
        Login();
        Check("p11w: EnableFileWatcher watches scripts/, modules/ and settings/", 3, zone.FileWatcher.Watching);
        scripts.LoadLuaObjectFromFile("./" + P11wItem);
        Check("p11w: at the start the global's and the item's overrides are on v1", "mod(v1)|mod(v1)",
              Lua("report(xi.p11w_global.fn()) report('|') report(xi.items.p11w_probe.onProbe())").Replace(",", ""));
        int overrides = zone.LuaModules.Overrides.Count();

        // a global and an item script edited on disk
        int mark = log.Count;
        P11wWriteGlobal("v2");
        P11wWriteItem("v2");
        bool seen = P11wTicksUntil(() =>
            log.Skip(mark).Any(l => l.StartsWith("[FileWatcher] GLOBAL ", StringComparison.Ordinal)) &&
            log.Skip(mark).Contains("[FileWatcher] " + P11wItem));
        Check("p11w: both edits reloaded on a 2400 ms tick", "True", seen.ToString());
        Check("p11w:   ... the global required again, its override applied again (isReload) over the new function",
              "mod(v2)", Lua("report(xi.p11w_global.fn())"));
        Check("p11w:   ... the item script stored over its object, its override applied again (isReload)", "mod(v2)",
              Lua("report(xi.items.p11w_probe.onProbe())"));
        Check("p11w:   ... the lines: the file, then the GLOBAL line with its require name", "True|True",
              $"{log.Skip(mark).Contains("[FileWatcher] " + P11wGlobal)}|" +
              log.Skip(mark).Contains($"[FileWatcher] GLOBAL {P11wGlobal} -> \"scripts/globals/p11w_global\""));

        // a module file added: run, its registry entries dropped, nothing registered
        mark = log.Count;
        File.WriteAllText(P11wPath(P11wModule),
            "require('modules/module_utils')\n_G.P11WModuleRuns = (_G.P11WModuleRuns or 0) + 1\n" +
            "local m = Module:new('p11w_rerun')\n" +
            "m:addOverride('xi.p11w_global.fn', function() return 'rerun(' .. super() .. ')' end)\n");
        seen = P11wTicksUntil(() => log.Skip(mark).Contains($"[FileWatcher] RE-RUNNING MODULE FILE {P11wModule}"));
        Check("p11w: a module file added: run again (RE-RUNNING MODULE FILE)", "True", seen.ToString());
        Check("p11w:   ... its registry entries dropped; no override registered or applied (no re-register in the C++)",
              $"0|{overrides}|mod(v2)",
              $"{Lua("report(tostring(#xi.module.registry))")}|{zone.LuaModules.Overrides.Count()}|" +
              Lua("report(xi.p11w_global.fn())"));
        Check("p11w:   ... the file ran (P11WModuleRuns at least 1)", "True",
              (int.Parse(Lua("report(tostring(P11WModuleRuns or 0))")) >= 1).ToString());

        // from here on the changes are put in by hand, on a watcher over no folder: a late event of the writes above
        // cannot land in a drain below
        zone.EnableFileWatcher(new ScriptFileWatcher(Array.Empty<(string, string)>(), log.Add));

        // the other kinds, put in by hand
        string Drain(string path, ScriptFileWatcher.FileAction action = ScriptFileWatcher.FileAction.Modified)
        {
            int at = log.Count;
            zone.FileWatcher.Enqueue(path, action);
            zone.TryReloadFilewatchList();
            return string.Join("|", log.Skip(at).Where(l => l.StartsWith("[FileWatcher]", StringComparison.Ordinal)));
        }

        // a file another program holds open (FileShare.None) cannot be read: the reload's IOException is logged, the
        // drain goes on, and the next drain reloads the file
        P11wWriteItem("v3");
        string lines;
        using (new FileStream(P11wPath(P11wItem), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            lines = Drain(P11wItem);
        Check("p11w: a reload that throws (the file held open): logged, the old object kept", "True|2|mod(v2)",
              lines.StartsWith($"[FileWatcher] {P11wItem}|[FileWatcher] reload of {P11wItem} failed: IOException: ",
                               StringComparison.Ordinal) + "|" + lines.Split('|').Length + "|" +
              Lua("report(xi.items.p11w_probe.onProbe())"));
        lines = Drain(P11wItem);
        Check("p11w:   ... the next drain reloads it", $"[FileWatcher] {P11wItem}|mod(v3)",
              lines + "|" + Lua("report(xi.items.p11w_probe.onProbe())"));

        Lua("xi.module.modifyInteractionEntry = function() end");
        lines = Drain("modules/module_utils.lua");
        Check("p11w: module_utils.lua run again: the modifyInteractionEntry guard put back", "true",
              Lua("report(tostring(xi.module.modifyInteractionEntry == CSharpQuests.patchGuard))"));
        Check("p11w:   ... its lines", "[FileWatcher] modules/module_utils.lua|" +
              "[FileWatcher] RE-RUNNING MODULE FILE modules/module_utils.lua", lines);

        // main.lua's own run resets xi.settings.main; map.lua runs again only in settings::init
        Lua("xi.settings.main.P11W_MARK = 1 xi.settings.map.P11W_MARK = 1 xi.settings.main.SERVER_NAME = 'p11w'");
        lines = Drain("settings/default/main.lua");
        Check("p11w: a settings file: it runs, then every settings file again (settings::init)", "nil|nil|True",
              Lua("report(tostring(xi.settings.main.P11W_MARK)) report('|') " +
                  "report(tostring(xi.settings.map.P11W_MARK))").Replace(",", "") + "|" +
              (Lua("report(tostring(xi.settings.main.SERVER_NAME))") != "p11w"));
        Check("p11w:   ... its lines", "[FileWatcher] settings/default/main.lua|" +
              "[FileWatcher] RELOADING ALL LUA SETTINGS FILES", lines);

        string aquillina = Lua("report(tostring(zones[xi.zone.BASTOK_MARKETS].npc.AQUILLINA))");
        Lua("zones[xi.zone.BASTOK_MARKETS].p11w = true zones[xi.zone.LOWER_JEUNO].p11w = true");
        lines = Drain("scripts/zones/Bastok_Markets/IDs.lua");
        Check("p11w: a zone's IDs.lua: its table made again (GetFirstID answered), package.loaded is it, " +
              "another zone's left", $"nil|{aquillina}|true|true",
              Lua("local t = zones[xi.zone.BASTOK_MARKETS] report(tostring(t.p11w)) report('|') " +
                  "report(tostring(t.npc.AQUILLINA)) report('|') " +
                  "report(tostring(package.loaded['scripts/zones/Bastok_Markets/IDs'] == t)) report('|') " +
                  "report(tostring(zones[xi.zone.LOWER_JEUNO].p11w))").Replace(",", ""));
        Check("p11w:   ... AQUILLINA is a number", "True", int.TryParse(aquillina, out _).ToString());
        Check("p11w:   ... its lines", "[FileWatcher] scripts/zones/Bastok_Markets/IDs.lua|" +
              "[FileWatcher] IDs scripts/zones/Bastok_Markets/IDs.lua", lines);

        // the fixture's file is read through its package.preload loader: counted while the reload runs
        Lua($"local name = '{FixtureLuaFile}' local load = package.preload[name] P11wLoader = load P11wLoads = 0 " +
            "package.preload[name] = function(...) P11wLoads = P11wLoads + 1 return load(...) end " +
            "P11wWasLoaded = type(package.loaded[name])");
        lines = Drain(FixtureLuaFile + ".lua");
        Check("p11w: a Lua quest container: was loaded, its file read again once, required again, in the lookup again",
              "table|1|table|true",
              Lua($"report(P11wWasLoaded) report('|') report(tostring(P11wLoads)) report('|') " +
                  $"report(type(package.loaded['{FixtureLuaFile}'])) report('|') " +
                  $"report(tostring(InteractionGlobal.lookup.containers['{FixtureContainer}'] == true))")
                  .Replace(",", ""));
        Check("p11w:   ... its lines", $"[FileWatcher] {FixtureLuaFile}.lua|" +
              $"[FileWatcher] INTERACTION {FixtureLuaFile} -> {FixtureFile}", lines);
        Lua($"package.preload['{FixtureLuaFile}'] = P11wLoader P11wLoader = nil");

        lines = Drain(ElvaanLuaFile + ".lua");
        Check("p11w: a C# quest's container (Lua file gone): not reloaded, logged (C# only)",
              $"[FileWatcher] {ElvaanLuaFile}.lua|" +
              $"[FileWatcher] INTERACTION {ElvaanLuaFile} not reloaded: it runs in C# ({ElvaanKey})", lines);

        lines = Drain("scripts/quests/jeuno/helpers.lua");
        Check("p11w: a helpers file: required again (INTERACTION HELPERS)",
              "[FileWatcher] scripts/quests/jeuno/helpers.lua|[FileWatcher] INTERACTION HELPERS jeuno", lines);

        // popChangedLuaFilesList: each (path, action) once, path order then action; only .lua; deletes and moves
        // taken out but not reloaded
        int before = log.Count;
        var w = zone.FileWatcher;
        w.Enqueue(P11wItem, ScriptFileWatcher.FileAction.Modified);
        w.Enqueue(P11wGlobal, ScriptFileWatcher.FileAction.Modified);
        w.Enqueue(P11wGlobal, ScriptFileWatcher.FileAction.Modified);
        w.Enqueue(P11wGlobal, ScriptFileWatcher.FileAction.Add);
        w.Enqueue("scripts/globals/p11w_notes.txt", ScriptFileWatcher.FileAction.Modified);
        w.Enqueue(P11wItem, ScriptFileWatcher.FileAction.Delete);
        w.Enqueue(P11wItem, ScriptFileWatcher.FileAction.Moved);
        zone.TryReloadFilewatchList();
        Check("p11w: one drain: each (path, action) once, path then action order; no .txt, no delete or move reloaded",
              $"{P11wGlobal},{P11wGlobal},{P11wItem}",
              string.Join(",", log.Skip(before)
                                  .Where(l => l.StartsWith("[FileWatcher] scripts/", StringComparison.Ordinal))
                                  .Select(l => l.Substring("[FileWatcher] ".Length))));
        Check("p11w:   ... and the queue is empty after it", "0", w.PopChangedLuaFiles().Count.ToString());

        // luaCache.clear(): Bastok Markets' Zone.lua has no onZoneTick, which the host remembers; a function put in
        // afterwards runs only after a drain
        Tick(400);
        Lua("P11wTick = 0 xi.zones.Bastok_Markets.Zone.onZoneTick = function() P11wTick = P11wTick + 1 end");
        Tick(400);
        string held = Lua("report(tostring(P11wTick))");
        w.Enqueue(P11wGlobal, ScriptFileWatcher.FileAction.Modified);
        zone.TryReloadFilewatchList();
        Tick(400);
        Check("p11w: the zone's no-onZoneTick memo held until a drain cleared it (luaCache.clear)", "0|True",
              held + "|" + (int.Parse(Lua("report(tostring(P11wTick))")) >= 1));
        Lua("xi.zones.Bastok_Markets.Zone.onZoneTick = nil");
        Logout();
        zone.DisableFileWatcher();
    }

    // the switch off (no EnableFileWatcher, every world's default): an edit on disk is not reloaded
    private static void RunP11wOff()
    {
        P11wWriteGlobal("v2");
        worldModuleLines_ = new[] { P11Base + "/watch/watch.lua" };
        NewWorld();
        int mark = log.Count;
        Check("p11w: off: no watcher", "True", (zone.FileWatcher == null).ToString());
        P11wWriteGlobal("v3");
        Thread.Sleep(500);
        for (int i = 0; i < 4; i++) Tick(2400);
        Check("p11w: off: the edited global not reloaded (still v2), nothing logged", "mod(v2)|0",
              Lua("report(xi.p11w_global.fn())") + "|" + log.Skip(mark).Count(l => l.Contains("[FileWatcher]")));
    }
}
