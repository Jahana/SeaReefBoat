using System;
using System.Collections.Generic;
using System.Linq;
using XI.Map;
using XI.Script;

namespace XI.Zone
{
    // Platform round P11: the Lua modules modules/init.txt lists, loaded as the C++ server loads them (moduleutils.cpp
    // LoadLuaModules, the port in XI.Script/LuaModules.cs; design note
    // docs/superpowers/specs/2026-11-03-lua-module-loader-design.md). InitScripts loads them right after the global
    // scripts and before the interaction containers (luautils.cpp:569-576, before zoneutils builds the zones); every
    // script loaded later applies its own overrides (ScriptEngine.LoadLuaObjectFromFile, luautils.cpp:1074);
    // ServerStart reports the overrides left unapplied after xi.server.onServerStart, unless IsTestServer
    // (map_engine.cpp:274-279). The C# quest modules (P4, ZoneHost.QuestModules) read the same lines at each
    // registration pass.
    //
    // A Lua module's xi.module.modifyInteractionEntry of a container that is a C# quest (its Lua file gone, or replaced
    // by the C# container) is skipped and logged, as the C++ skips an override whose target is missing: the C# module
    // that ports the file (P4) patches the C# quest; module_utils' own function would raise on the missing file inside
    // xi.server.onServerStart and stop the rest of its super chain. Every other file goes to module_utils' function.
    public sealed partial class ZoneHost
    {
        // a Lua patch the guard skipped: the module file ("modules/era/lua/quests/bastok/the_gustaberg_tour.lua"), the
        // container file it named, the C# quest's key, and the enabled C# module that ports the file (null: none)
        public sealed class SkippedLuaPatch
        {
            public string ModuleFile, File, Key, PortedBy;
        }

        public List<SkippedLuaPatch> SkippedLuaPatches { get; } = new List<SkippedLuaPatch>();

        // the loader's state (null before InitScripts)
        public LuaModules LuaModules => scripts_?.Modules;

        // moduleutils::LoadLuaModules with ModuleInitLines (else modules/init.txt), then the globals' TryApply and
        // TryApplyRemaining; the patch guard installed after. InitScripts runs it once; the golden harness again with a
        // flow's module lines (its world's first run had none)
        public void LoadLuaModules()
        {
            if (scripts_ == null) return;
            var ports = new Dictionary<string, ushort>(StringComparer.Ordinal);
            var r = Exec("SELECT name, zoneport FROM zone_settings");
            while (r != null && r.Next()) ports[r.Get<string>("name")] = r.Get<ushort>("zoneport");
            scripts_.LoadLuaModules(ModuleInitLines ?? ReadModuleInit(), ports, CurrentMapPort());
            InstallPatchGuard();
        }

        // LoadLuaModules' currentPort: the map IPP's port, else settings network.MAP_PORT
        private ushort CurrentMapPort()
        {
            ushort port = MapIpp.Port(ProcessIpp);
            if (port != 0) return port;
            int top = lua_.Top;
            try
            {
                lua_.GetPath("xi.settings.network.MAP_PORT");
                return lua_.IsNumber(-1) ? (ushort)lua_.ToNumber(-1) : (ushort)0;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // xi.module.modifyInteractionEntry wrapped (again after module_utils.lua ran again, which puts its own back)
        private void InstallPatchGuard()
        {
            try
            {
                lua_.DoString(
                    "if CSharpQuests == nil or xi.module == nil or xi.module.modifyInteractionEntry == nil then return end\n" +
                    "if xi.module.modifyInteractionEntry == CSharpQuests.patchGuard then return end\n" +
                    "local modify = xi.module.modifyInteractionEntry\n" +
                    "CSharpQuests.patchGuard = function(filename, modifyFunc)\n" +
                    "    local info = debug and debug.getinfo(2, 'S')\n" +
                    "    if CSharpQuests.skipPatch(filename, info and info.source or '?') then return end\n" +
                    "    return modify(filename, modifyFunc)\n" +
                    "end\n" +
                    "xi.module.modifyInteractionEntry = CSharpQuests.patchGuard", "=moduleutils");
            }
            catch (LuaException e)
            {
                log_($"modules: the modifyInteractionEntry guard not installed: {e.Message}");
            }
        }

        // CSharpQuests.skipPatch(filename, source): true (logged) when the file is a C# quest's container in the last
        // pass
        private int SkipLuaPatch(LuaState l)
        {
            string file = l.IsString(1) ? l.ToStringValue(1) : null;
            string source = l.IsString(2) ? l.ToStringValue(2) : "?";
            string key = file == null ? null : legacyPaths_.Where(p => p.Value == file).Select(p => p.Key).FirstOrDefault();
            if (key == null)
            {
                l.Push(false);
                return 1;
            }
            string moduleFile = ModuleFileOf(source);
            string portedBy = modulePatches_.TryGetValue(key, out var patch) && patch.Module.LuaFile == moduleFile ? patch.Module.Name : null;
            SkippedLuaPatches.Add(new SkippedLuaPatch { ModuleFile = moduleFile, File = file, Key = key, PortedBy = portedBy });
            log_($"modules: {moduleFile}: modifyInteractionEntry('{file}') skipped: {key} is a C# quest " +
                 (portedBy != null ? $"(the C# module {portedBy} ports the file)" : "(no enabled C# module ports the file: this Lua patch does not apply)"));
            l.Push(true);
            return 1;
        }

        // a chunk's source "@./modules/era/x.lua" as a QuestModule names its file, "modules/era/x.lua" ("." and empty
        // segments dropped, ".." folded); anything else as it is
        internal static string ModuleFileOf(string source)
        {
            if (source == null || !source.StartsWith("@", StringComparison.Ordinal)) return source;
            var parts = new List<string>();
            foreach (var p in source.Substring(1).Replace('\\', '/').Split('/'))
            {
                if (p.Length == 0 || p == ".") continue;
                if (p == "..")
                {
                    if (parts.Count > 0) parts.RemoveAt(parts.Count - 1);
                    continue;
                }
                parts.Add(p);
            }
            return string.Join("/", parts);
        }

        // MapEngine::do_init's ReportLuaModuleUsage: each enabled override not applied, an error line (ShowErrorFmt:
        // LuaModules.Error). One whose key names a script outside scripts/globals that exists (scripts/<key>.lua: an
        // NPC, mob, zone, item, action or effect script) is counted instead, in one info line: this host loads such a
        // script on first use, not at its start, and that load applies it (its TryApply is not silent, so a missing
        // table is logged then)
        public void ReportLuaModuleUsage()
        {
            var modules = LuaModules;
            if (modules == null) return;
            int waiting = 0;
            foreach (var o in modules.NotApplied())
            {
                if (WaitsForScript(o))
                {
                    waiting++;
                    continue;
                }
                modules.Error($"moduleutils::ReportLuaModuleUsage: Override not applied: {o.Name} (module {o.ModuleName}, {o.Filename})");
            }
            if (waiting > 0) log_($"moduleutils::ReportLuaModuleUsage: {waiting} overrides wait for their script's first load");
        }

        private bool WaitsForScript(LuaModules.Override o)
        {
            int dot = o.Name.LastIndexOf('.');
            if (dot < 0 || !o.Name.StartsWith("xi.", StringComparison.Ordinal)) return false;
            var key = o.Name.Substring(3, dot - 3).Split('.');
            if (key.Length == 0 || key[0] == "globals") return false;
            return System.IO.File.Exists(System.IO.Path.Combine(scripts_.ScriptsDir, string.Join("/", key) + ".lua"));
        }
    }
}
