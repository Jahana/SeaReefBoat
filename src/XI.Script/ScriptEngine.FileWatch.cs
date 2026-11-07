using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XI.Script
{
    // The file watcher's reload of one file (luautils::TryReloadFilewatchList -> LoadLuaObjectFromFile(file, true),
    // luautils.cpp:887-1075): what the C++ LoadLuaObjectFromFile does for each kind of file under scripts/, modules/
    // and settings/. Only the watcher calls it: the C# LoadLuaObjectFromFile (ScriptEngine.cs) keeps its own callers'
    // object path unchanged.
    public sealed partial class ScriptEngine
    {
        // the zones LoadGlobalScripts was given, for a reload of one zone's IDs.lua
        private List<ScriptZone> scriptZones_ = new List<ScriptZone>();

        // after a modules/ file ran again: module_utils.lua puts its own xi.module functions back, so the host
        // installs its modifyInteractionEntry guard again (ZoneHost.InstallPatchGuard)
        public Action ModuleFileRerun { get; set; }

        // luautils::LoadLuaObjectFromFile(filename, true) for a path the watcher reported ("scripts/...",
        // "modules/...", "settings/...", relative to the server root):
        // - a modules/ file runs again; the module registries its run filled are emptied (it registers nothing);
        // - a settings/ file runs, then every settings file (settings::init);
        // - a scripts/globals file is required again, then its module overrides applied again (isReload);
        // - a zone's IDs.lua runs again with its zone's GetFirstID lookups;
        // - a quests / missions / battlefields container is required again and put back in the lookup (a helpers
        //   file only required again);
        // - any other script is stored over its xi.<path> object, its module overrides applied again (isReload).
        public void ReloadLuaObjectFromFile(string filename)
        {
            if (string.IsNullOrEmpty(filename)) return;
            var elements = filename.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (elements.Count == 0 || LuaModules.Extension(elements[^1]).Length == 0) return;
            // every element's extension dropped (path::replace_extension)
            var parts = elements.Select(e =>
            {
                string ext = LuaModules.Extension(e);
                return ext.Length == 0 ? e : e.Substring(0, e.Length - ext.Length);
            }).ToList();

            if (parts[0] == "modules")
            {
                if (!RunRootFile(filename, out string err))
                {
                    error_($"luautils::LoadLuaObjectFromFile: Load module error: {filename}: {err}");
                    return;
                }
                // Modules self-register on execution, and the registries were drained at startup, so this re-run's
                // entries are dropped.
                (Modules ??= new LuaModules(Lua, Root, log_, error_)).ClearRegistries();
                ModuleFileRerun?.Invoke();
                log_($"[FileWatcher] RE-RUNNING MODULE FILE {filename}");
                return;
            }

            if (parts[0] == "settings")
            {
                if (!RunRootFile(filename, out string err))
                {
                    error_($"luautils::LoadLuaObjectFromFile: Load settings error: {filename}: {err}");
                    return;
                }
                log_("[FileWatcher] RELOADING ALL LUA SETTINGS FILES");
                try
                {
                    SettingsInit();
                }
                catch (LuaException e)
                {
                    // settings::init throws here (ShowCritical); the C# host logs it and runs on
                    error_(e.Message);
                }
                return;
            }

            int scripts = parts.IndexOf("scripts");
            if (scripts < 0)
            {
                error_($"luautils::LoadLuaObjectFromFile: Invalid filename: {filename}");
                return;
            }
            parts.RemoveRange(0, scripts + 1);

            if (parts.Count > 0 && parts[0] == "globals" && LuaModules.Extension(elements[^1]) == ".lua")
            {
                string requireName = "scripts/globals/" + string.Join("/", parts.Skip(1));
                try
                {
                    Lua.DoString("local name = ... package.loaded[name] = nil require(name)", "=LoadLuaObjectFromFile",
                                 0, () =>
                                 {
                                     Lua.PushString(requireName);
                                     return 1;
                                 });
                }
                catch (LuaException e)
                {
                    error_($"luautils::LoadLuaObjectFromFile: Load global error: {filename}: {e.Message}");
                    return;
                }
                Modules?.TryApply(parts, true);
                log_($"[FileWatcher] GLOBAL {filename} -> \"{requireName}\"");
                return;
            }

            if (parts.Count == 3 && parts[2] == "IDs")
            {
                // PopulateIDLookupsByFilename(path.parent_path().stem()): the zone folder's name
                PopulateIDLookups(scriptZones_, elements[^2]);
                log_($"[FileWatcher] IDs {filename}");
                return;
            }

            if (parts.Count == 3 && (parts[0] == "quests" || parts[0] == "missions" || parts[0] == "battlefields"))
            {
                ReloadInteractionFile(filename, parts);
                return;
            }

            LoadLuaObjectFromFile(filename, true);
        }

        // a module or settings file run from the root (safe_script_file with the server root as the working
        // directory), its chunk named by the path as given
        private bool RunRootFile(string filename, out string error)
        {
            error = null;
            try
            {
                Lua.DoFile(Path.Combine(Root, filename), "@" + filename);
                return true;
            }
            catch (Exception e) when (e is LuaException || e is IOException || e is UnauthorizedAccessException)
            {
                error = e.Message;
                return false;
            }
        }

        // luautils.cpp:986-1031: a helpers file required again; a container file required again, its old container
        // taken out of InteractionGlobal.lookup and the new one put in. A container that runs as a C# quest
        // (CSharpQuests.live of its id, or CSharpQuests.legacy of its file, as !reloadquest finds one) is left alone
        // and logged: adding its Lua container would run it beside the C# quest that took its place. A new file is
        // checked by the id of what it returns (the old one is nil): required, but not added
        private void ReloadInteractionFile(string filename, List<string> parts)
        {
            string requireName = $"scripts/{parts[0]}/{parts[1]}/{parts[2]}";
            if (parts[2] == "helpers")
            {
                try
                {
                    Lua.DoString("local name = ... package.loaded[name] = nil utils.prequire(name)",
                                 "=LoadLuaObjectFromFile", 0, () =>
                                 {
                                     Lua.PushString(requireName);
                                     return 1;
                                 });
                }
                catch (LuaException e)
                {
                    // the C++ does not look at this result
                    log_($"luautils::LoadLuaObjectFromFile: {filename}: {e.Message}");
                }
                log_($"[FileWatcher] INTERACTION HELPERS {parts[1]}");
                return;
            }

            int top = Lua.Top;
            try
            {
                Lua.DoString(
                    "local name = ...\n" +
                    "local old = package.loaded[name]\n" +
                    "if CSharpQuests then\n" +
                    "    local key = CSharpQuests.legacy and CSharpQuests.legacy[name]\n" +
                    "    if not key and CSharpQuests.live and type(old) == 'table' then\n" +
                    "        key = CSharpQuests.live[old.id]\n" +
                    "    end\n" +
                    "    if key then return key end\n" +
                    "end\n" +
                    "if old then\n" +
                    "    package.loaded[name] = nil\n" +
                    "    if InteractionGlobal then InteractionGlobal.lookup:removeContainer(old) end\n" +
                    "end\n" +
                    "local res = utils.prequire(name)\n" +
                    "local live = CSharpQuests and CSharpQuests.live and type(res) == 'table'\n" +
                    "if live then live = CSharpQuests.live[res.id] end\n" +
                    "if live then return live end\n" +
                    "if InteractionGlobal and res then InteractionGlobal.lookup:addContainer(res) end\n" +
                    "return nil", "=LoadLuaObjectFromFile", 1, () =>
                    {
                        Lua.PushString(requireName);
                        return 1;
                    });
                if (Lua.IsString(-1))
                {
                    string key = Lua.ToStringValue(-1);
                    log_($"[FileWatcher] INTERACTION {requireName} not reloaded: it runs in C# ({key})");
                    return;
                }
            }
            catch (LuaException e)
            {
                error_($"luautils::LoadLuaObjectFromFile: Load interaction error: {filename}: {e.Message}");
                return;
            }
            finally
            {
                Lua.SetTop(top);
            }
            log_($"[FileWatcher] INTERACTION {requireName} -> {parts[2]}");
        }
    }
}
