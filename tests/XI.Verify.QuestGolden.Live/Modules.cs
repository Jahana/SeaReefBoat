using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Zone;

// quest rework P4, platform round P11: a flow's `module modules/<path>.lua` lines. The world loads the modules as the
// server does at its start, once the setup is written, so the flow's settings (setting RESTRICT_CONTENT 1, setting
// ENABLE_ROV 0) are in effect for their conditions, on both sides alike, in the C++ server's order (luautils.cpp
// applies the Lua modules before zoneutils.cpp builds the interaction containers, and xi.server.onServerStart runs
// after both):
//   1. the flow's lines are the world's module list (ZoneHost.ModuleInitLines, read as modules/init.txt) and the
//      server's own Lua module loader runs them (ZoneHost.LoadLuaModules: moduleutils.cpp's LoadLuaModules, then
//      TryApply for the globals and TryApplyRemaining, P11). The C# modules that port the files (P4) are enabled by the
//      same lines at the next registration pass: on the Lua side every quest they patch is in the record's
//      CSHARP_QUESTS_OFF (RecordOff), so they patch nothing there and the Lua module patches the Lua container; on the
//      C# side the Lua module's modifyInteractionEntry of a C# quest is skipped (logged; ZoneHost.SkippedLuaPatches)
//      and the C# module patches it;
//   2. the containers built again under the overrides (ContainerReload): every quest container file dropped from
//      package.loaded (InteractionGlobal.reload(true) drops only the interaction_containers path, so a cached quest
//      file would keep the container its first load built: a module that overrides a helper the quest files call when
//      they load, GobbiebagQuest.new, would report enabled and patch nothing), then InteractionGlobal.reload(true): the
//      lookup built again and the C# quests registered again (on the C# side the quests the modules patch built from
//      their patched definitions);
//   3. xi.server.onServerStart(): the Lua modules' overrides (super() first, then their modifyInteractionEntry).
// The run fails when a flow module is not in effect: its file logs a loader error (moduleutils:: errors), registers no
// module or one not enabled with the flow's settings; its C# port is not enabled (C# side); or (C# side) one of its
// modifyInteractionEntry calls was skipped and no enabled C# module ports the file. Such a flow would prove nothing.
internal static partial class Program
{
    // the coverage key of a module's overrides (ZoneHost.ModuleCoverageKey: "module <lua file>")
    internal static string ModuleKey(string luaFile) => ZoneHost.ModuleCoverageKey(luaFile);

    // the init.txt lines of the flow's modules (the paths without modules/)
    internal static List<string> ModuleInitLinesOf(Flow f) => f.Modules.Select(p => p.Substring("modules/".Length)).ToList();

    private static void LoadModules(Flow f, bool luaSide, List<string> problems)
    {
        if (f.Modules.Count == 0) return;
        zone.ModuleInitLines = ModuleInitLinesOf(f);
        var loader = zone.LuaModules;
        int errorsBefore = loader?.Errors.Count ?? 0, modulesBefore = loader?.Modules.Count ?? 0;
        zone.LoadLuaModules();
        // a module that requires interaction_global ran it again (DoFile, no package.loaded): the plain entry points
        // are back, so the return recorder goes on them again (an entry point still wrapped is left alone)
        InstallReturnRecorder();
        loader = zone.LuaModules;
        if (loader == null) problems.Add("module lines: the world has no Lua module loader");
        else
        {
            problems.AddRange(loader.Errors.Skip(errorsBefore).Distinct());
            var registered = loader.Modules.Skip(modulesBefore).ToList();
            foreach (var path in f.Modules)
            {
                var mine = registered.Where(m => ZoneHost.ModuleFileOf("@" + m.Path) == path).ToList();
                if (mine.Count == 0) problems.Add($"module {path}: the file registered no module");
                foreach (var m in mine)
                    if (!m.Enabled || m.EnabledCount == 0)
                        problems.Add($"module {path}: {m.Name} is not enabled in this flow (its condition is false with the flow's settings)");
            }
        }
        // the containers after the modules, as the C++ server builds them
        Lua(ContainerReload);
        if (!luaSide)
        {
            foreach (var path in f.Modules)
                if (CSharpModules().Any(m => m.LuaFile == path) && !zone.EnabledQuestModules.Any(m => m.LuaFile == path))
                    problems.Add($"module {path}: its C# module is not enabled in this flow (its condition is false with the flow's settings, or it was refused: see the modules: lines)");
        }
        int skippedBefore = zone.SkippedLuaPatches.Count;
        Lua("xi.server.onServerStart()");
        if (!luaSide)
            foreach (var s in zone.SkippedLuaPatches.Skip(skippedBefore).Where(s => s.PortedBy == null && f.Modules.Contains(s.ModuleFile)))
                problems.Add($"module {s.ModuleFile}: its modifyInteractionEntry('{s.File}') was skipped ({s.Key} is a C# quest) and no enabled C# module ports the file");
        // the modules: lines of the run, for the record (they name the module enabled, refused or off)
        foreach (var line in log.Where(l => l.StartsWith("modules: ", StringComparison.Ordinal) && l.Contains(" refused: ")).Distinct())
            problems.Add(line);
    }

    // the interaction containers built again: each quest container file (scripts/quests/<area>/<file>) dropped from
    // package.loaded, so its next require runs it under the modules' overrides, then InteractionGlobal.reload(true).
    // The mission and battlefield files stay loaded: the quest modules' load-time target is a quest helper
    // (xi.jeuno.helpers.GobbiebagQuest.new); a module flow for one that overrides a helper a mission or battlefield
    // file calls when it loads would drop those files too
    internal const string ContainerReload = @"
        local files = GetContainerFilenamesList()
        for i = 1, #files do
            if string.sub(files[i], 1, 15) == 'scripts/quests/' then package.loaded[files[i]] = nil end
        end
        InteractionGlobal.reload(true)";
}
