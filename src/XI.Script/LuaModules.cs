using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XI.Script
{
    // moduleutils' Lua module loader (src/map/utils/moduleutils.cpp; platform round P11, design note
    // docs/superpowers/specs/2026-11-03-lua-module-loader-design.md): LoadLuaModules runs modules/module_utils.lua and
    // every *.lua module init.txt lists, keeps each Module's overrides by their parent path, and applyOverride puts an
    // override in its dotted target (module_utils.lua's applyOverride gives it its super). TryApply is called for each
    // global script after the load (luautils.cpp:571-574) and at the end of every LoadLuaObjectFromFile (:1074),
    // TryApplyRemaining once after the globals (:576), NotApplied for MapEngine::do_init's ReportLuaModuleUsage. The
    // messages are the C++ ones, each after the name of the C++ function that logs it; the debug lines
    // (DebugModulesFmt) only while xi.settings.logging.DEBUG_MODULES is true.
    public sealed class LuaModules
    {
        // moduleutils.cpp's Override: one m:addOverride / addOverrideByEra case
        public sealed class Override
        {
            public string Filename;     // the module file's name (path.filename(): "unlocking_a_myth.lua")
            public string ModuleName;   // Module:new's name
            public string Name;         // the dotted target
            public List<string> Parts;  // split(Name, "."), empty parts kept
            public LuaRef Func;
            public bool Enabled = true;
            public bool Applied;
        }

        // a Module the load registered (what registerModule read), for the harness and the verify suites
        public sealed class Registered
        {
            public string Path;         // expanded entry: "./modules/era/lua/globals/equipment/unlocking_a_myth.lua"
            public string Name;
            public bool Enabled;
            public int Declared, EnabledCount;
        }

        private readonly LuaState lua_;
        private readonly string root_;
        private readonly Action<string> log_, error_;

        // overrides by the target's parent path, each key's list in declaration order (the super chain); keys_ in the
        // order of each key's first declaration, the walk of TryApplyRemaining and the report (the C++ HashMap's order
        // cannot be had; it changes the log order only)
        private readonly Dictionary<string, List<Override>> overrides_ = new Dictionary<string, List<Override>>(StringComparer.Ordinal);
        private readonly List<string> keys_ = new List<string>();

        // the module files run by the loads, in run order (a file run twice is here twice), and the Modules they
        // registered
        public List<string> FilesRun { get; } = new List<string>();
        public List<Registered> Modules { get; } = new List<Registered>();
        // every error line the loader logged (also logged through error)
        public List<string> Errors { get; } = new List<string>();

        public LuaModules(LuaState lua, string root, Action<string> log, Action<string> error)
        {
            lua_ = lua;
            root_ = root;
            log_ = log;
            error_ = error;
        }

        // every override in key order, each key's in declaration order
        public IEnumerable<Override> Overrides => keys_.SelectMany(k => overrides_[k]);

        // ShowErrorFmt: kept in Errors and logged through error (the host's ReportLuaModuleUsage logs through it too)
        public void Error(string line)
        {
            Errors.Add(line);
            error_(line);
        }

        // DebugModulesFmt: LOGGER_BODY_CONDITIONAL on logging.DEBUG_MODULES
        private void Debug(string line)
        {
            int top = lua_.Top;
            try
            {
                lua_.GetPath("xi.settings.logging.DEBUG_MODULES");
                if (lua_.IsBoolean(-1) && lua_.ToBoolean(-1)) log_(line);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // ---- init.txt (moduleutils.cpp:304-316) ------------------------------------------------------------------

        // the list: one entry per line (std::getline: split at '\n'); a line that is empty, starts with '#', or is a
        // lone "\r" is skipped (a '#' after anything, a space included, is no comment); the rest is "./modules/" + line
        // trimmed of ' ', '\t', '\r', '\n' at both ends (so a leading space stays, and a line of blanks is
        // "./modules/")
        public static List<string> Entries(IEnumerable<string> lines)
        {
            var list = new List<string>();
            if (lines == null) return list;
            foreach (var line in string.Join("\n", lines).Split('\n'))
            {
                if (line.Length == 0 || line[0] == '#' || line == "\n" || line == "\r" || line == "\r\n") continue;
                list.Add(("./modules/" + line).Trim(' ', '\t', '\r', '\n'));
            }
            return list;
        }

        // the entry's path on disk: the C++ runs with the server root as its working directory
        private string OnDisk(string entry) => Path.Combine(root_, entry);

        // moduleutils.cpp:318-337: expandedList starts as the list; each directory entry's
        // sorted_directory_iterator<recursive_directory_iterator> paths (directories too, element-wise path order) are
        // appended in the list's order, so every listed file comes before any folder's contents; an entry that is
        // neither a directory nor there is logged
        public List<string> Expand(IReadOnlyList<string> list)
        {
            var expanded = new List<string>(list);
            foreach (var entry in list)
            {
                string disk = OnDisk(entry);
                if (Directory.Exists(disk))
                {
                    string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(disk));
                    var inner = new List<string>();
                    foreach (var p in Directory.EnumerateFileSystemEntries(full, "*", SearchOption.AllDirectories))
                        inner.Add(p.Substring(full.Length).TrimStart('\\', '/').Replace('\\', '/'));
                    inner.Sort(ScriptEngine.PathCompare);
                    // path operator/: no second separator after a trailing one
                    string prefix = entry.EndsWith("/", StringComparison.Ordinal) || entry.EndsWith("\\", StringComparison.Ordinal) ? entry : entry + "/";
                    expanded.AddRange(inner.Select(r => (prefix + r).Replace('\\', '/')));
                    continue;
                }
                // A stale init.txt line used to load silently as if it asked for nothing.
                if (!File.Exists(disk)) Error($"moduleutils::LoadLuaModules: init.txt entry does not exist: {entry}");
            }
            return expanded;
        }

        // std::filesystem::path::filename / extension of a generic path
        private static string FileName(string path)
        {
            int slash = path.LastIndexOfAny(new[] { '/', '\\' });
            return slash < 0 ? path : path.Substring(slash + 1);
        }

        internal static string Extension(string fileName)
        {
            if (fileName == "." || fileName == "..") return "";
            int dot = fileName.LastIndexOf('.');
            return dot <= 0 ? "" : fileName.Substring(dot);
        }

        // ---- LoadLuaModules (moduleutils.cpp:300-438) ------------------------------------------------------------

        // zonePorts: zone_settings name -> zoneport; currentPort: the map IPP's port, else network.MAP_PORT
        public void Load(IEnumerable<string> initLines, IReadOnlyDictionary<string, ushort> zonePorts, ushort currentPort)
        {
            // lua.safe_script_file("./modules/module_utils.lua"): its result is not looked at
            RunFile("./modules/module_utils.lua", 0, out _);

            var expanded = Expand(Entries(initLines));

            // Module files self-register; their return values are ignored.
            foreach (var entry in expanded)
            {
                // std::filesystem::path(entry).relative_path(): the entries are relative already
                string path = entry;
                string filename = FileName(path);
                if (filename == "module_utils.lua" || Directory.Exists(OnDisk(path)) || Extension(filename) != ".lua") continue;

                // Per file, so a file that errors cannot leak state into the next one.
                ClearRegistries();

                int top = lua_.Top;
                try
                {
                    if (!RunFile(path, 1, out string err))
                    {
                        Error($"moduleutils::LoadLuaModules: Failed to load module: {filename}");
                        Error(err);
                        continue;
                    }
                    FilesRun.Add(path);
                    int result = lua_.Top;

                    int modules = RegistrySize("registry"), commands = RegistrySize("commandRegistry");
                    if (modules == 0 && commands == 0)
                    {
                        // The old interface returned the command table instead of registering it, and would otherwise
                        // vanish silently.
                        if (lua_.IsTable(result) && HasField(result, "cmdprops") && HasField(result, "onTrigger"))
                        {
                            Error($"moduleutils::LoadLuaModules: Command module returns a table instead of calling xi.module.registerCommand: {filename}");
                            continue;
                        }
                        // Anything else registering nothing is a data or helper file.
                        Debug($"moduleutils::LoadLuaModules: File registered no modules or commands (library/data file): {filename}");
                        continue;
                    }

                    for (int i = 1; i <= commands; i++) PublishCommand(i, filename);
                    for (int i = 1; i <= modules; i++)
                    {
                        lua_.GetPath("xi.module.registry");
                        lua_.RawGetI(-1, i);
                        if (!lua_.IsTable(-1))
                        {
                            Error($"moduleutils::LoadLuaModules: Malformed entry in module registry: {filename}");
                            lua_.SetTop(result);
                            continue;
                        }
                        RegisterModule(lua_.Top, path, filename, zonePorts, currentPort);
                        lua_.SetTop(result);
                    }
                }
                finally
                {
                    lua_.SetTop(top);
                }
            }

            ClearRegistries();
        }

        // safe_script_file of an entry: false with the error when it fails; nresults values left on the stack
        private bool RunFile(string entry, int nresults, out string error)
        {
            error = null;
            try
            {
                lua_.DoFile(OnDisk(entry), "@" + entry, nresults);
                return true;
            }
            catch (Exception e) when (e is LuaException || e is IOException || e is UnauthorizedAccessException)
            {
                error = e.Message;
                return false;
            }
        }

        private int RegistrySize(string name)
        {
            int top = lua_.Top;
            try
            {
                lua_.GetPath("xi.module." + name);
                return lua_.IsTable(-1) ? lua_.ObjLen(-1) : 0;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        private bool HasField(int table, string key)
        {
            lua_.GetField(table, key);
            bool has = !lua_.IsNil(-1);
            lua_.Pop(1);
            return has;
        }

        // moduleutils.cpp:404-421: xi.commands[name] = entry.command (xi and xi.commands made when nil)
        private void PublishCommand(int i, string filename)
        {
            int top = lua_.Top;
            try
            {
                lua_.GetPath("xi.module.commandRegistry");
                lua_.RawGetI(-1, i);
                int entry = lua_.Top;
                if (!lua_.IsTable(entry))
                {
                    Error($"moduleutils::LoadLuaModules: Malformed entry in command registry: {filename}");
                    return;
                }
                lua_.GetField(entry, "name");
                if (!lua_.IsString(-1))
                {
                    Error($"moduleutils::LoadLuaModules: Command registered with no name: {filename}");
                    return;
                }
                string name = lua_.ToStringValue(-1);
                log_($"moduleutils::LoadLuaModules: Registering module command: !{name}");
                lua_.GetGlobal("xi");
                if (!lua_.IsTable(-1))
                {
                    lua_.Pop(1);
                    lua_.NewTable();
                    lua_.PushValue(-1);
                    lua_.SetGlobal("xi");
                }
                lua_.GetField(-1, "commands");
                if (!lua_.IsTable(-1))
                {
                    lua_.Pop(1);
                    lua_.NewTable();
                    lua_.PushValue(-1);
                    lua_.SetField(-3, "commands");
                }
                lua_.GetField(entry, "command");
                lua_.SetField(-2, name);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // ---- registerModule (moduleutils.cpp:122-218) ------------------------------------------------------------

        private void RegisterModule(int module, string path, string filename, IReadOnlyDictionary<string, ushort> zonePorts, ushort currentPort)
        {
            lua_.GetField(module, "name");
            string moduleName = lua_.IsString(-1) ? lua_.ToStringValue(-1) : "";
            lua_.Pop(1);

            // A non-boolean must not silently leave the module enabled.
            lua_.GetField(module, "enabled");
            bool isBool = lua_.IsBoolean(-1);
            bool moduleEnabled = isBool && lua_.ToBoolean(-1);
            lua_.Pop(1);
            if (!isBool)
            {
                Error($"moduleutils::registerModule: Module {moduleName} has a non-boolean 'enabled' field, skipping module ({filename})");
                return;
            }

            log_($"moduleutils::registerModule: === Module: {moduleName}{(moduleEnabled ? "" : " (disabled)")} ===");
            var reg = new Registered { Path = path, Name = moduleName, Enabled = moduleEnabled };
            Modules.Add(reg);

            lua_.GetField(module, "overrides");
            int list = lua_.Top;
            int count = lua_.IsTable(list) ? lua_.ObjLen(list) : 0;
            for (int i = 1; i <= count; i++)
            {
                lua_.RawGetI(list, i);
                int o = lua_.Top;
                try
                {
                    if (!lua_.IsTable(o)) continue;
                    lua_.GetField(o, "name");
                    string name = lua_.IsString(-1) ? lua_.ToStringValue(-1) : null;
                    lua_.Pop(1);
                    if (string.IsNullOrEmpty(name))
                    {
                        Error($"moduleutils::registerModule: Module {moduleName} declared an override with no target ({filename})");
                        continue;
                    }
                    lua_.GetField(o, "func");
                    var func = lua_.Ref();
                    // get_or("enabled", true): the default for anything not a boolean
                    lua_.GetField(o, "enabled");
                    bool caseEnabled = !lua_.IsBoolean(-1) || lua_.ToBoolean(-1);
                    lua_.Pop(1);
                    var parts = name.Split('.').ToList();

                    reg.Declared++;
                    bool enabled = moduleEnabled && caseEnabled;
                    if (!moduleEnabled)
                        Debug($"moduleutils::registerModule: Declared override (skipped, module condition not met): {name}");
                    else if (!caseEnabled)
                    {
                        // Lua supplies the reason; it is logged, not kept.
                        lua_.GetField(o, "reason");
                        string reason = lua_.IsString(-1) ? lua_.ToStringValue(-1) : "condition not met";
                        lua_.Pop(1);
                        Debug($"moduleutils::registerModule: Declared override (skipped, {reason}): {name}");
                    }

                    // Multi-process: skip overrides targeting zones on a different port
                    if (enabled && parts.Count >= 3 && parts[0] == "xi" && parts[1] == "zones" &&
                        zonePorts != null && zonePorts.TryGetValue(parts[2], out var port) && port != currentPort)
                    {
                        Debug($"moduleutils::registerModule: Declared override (skipped, {parts[2]} runs on port {port}): {name}");
                        enabled = false;
                    }

                    if (enabled)
                    {
                        Debug($"moduleutils::registerModule: Declared override: {name}");
                        reg.EnabledCount++;
                    }

                    // name.substr(0, name.rfind('.')): the whole name when it has no '.'
                    int dot = name.LastIndexOf('.');
                    string key = dot < 0 ? name : name.Substring(0, dot);
                    if (!overrides_.TryGetValue(key, out var keyList))
                    {
                        overrides_[key] = keyList = new List<Override>();
                        keys_.Add(key);
                    }
                    keyList.Add(new Override
                    {
                        Filename = filename, ModuleName = moduleName, Name = name, Parts = parts, Func = func, Enabled = enabled,
                    });
                }
                finally
                {
                    lua_.SetTop(o - 1);
                }
            }
            lua_.SetTop(list - 1);

            // Declaring no overrides is malformed whatever the condition evaluated to. Declaring some and applying none
            // is a normal configuration.
            if (reg.Declared == 0)
                Error($"moduleutils::registerModule: No overrides declared in module: {moduleName} ({filename})");
            else if (reg.EnabledCount == 0)
                log_($"moduleutils::registerModule: Module {moduleName}: {reg.Declared} overrides declared, none applied for this configuration");
        }

        // ---- applyOverride (moduleutils.cpp:70-120) --------------------------------------------------------------

        private bool ApplyOverride(Override o, bool silent)
        {
            if (o.Parts.Count == 0) return false;
            int top = lua_.Top;
            try
            {
                lua_.GetGlobal("_G");
                for (int i = 0; i < o.Parts.Count; i++)
                {
                    string part = o.Parts[i];
                    int table = lua_.Top;
                    if (i == o.Parts.Count - 1)
                    {
                        Debug($"moduleutils::applyOverride: Applying override: {o.Name}");
                        lua_.GetField(table, part);
                        bool nil = lua_.IsNil(-1);
                        lua_.Pop(1);
                        if (nil)
                        {
                            Debug($"moduleutils::applyOverride: Inserting empty function to override for: {o.Name} ({o.Filename})");
                            lua_.DoString("return function() end", "=moduleutils", 1);
                            lua_.SetField(table, part);
                        }
                        else Debug($"moduleutils::applyOverride: Override target exists for: {o.Name} ({o.Filename})");

                        try
                        {
                            lua_.GetGlobal("applyOverride");
                            lua_.PushValue(table);
                            lua_.PushString(part);
                            lua_.PushRef(o.Func);
                            lua_.PushString(o.Name);
                            lua_.PushString(o.Filename);
                            lua_.Call(5, 0);
                        }
                        catch (LuaException e)
                        {
                            Error($"moduleutils::applyOverride: applyOverride failed for {o.Name}: {e.Message}");
                            return false;
                        }
                        o.Applied = true;
                        return true;
                    }

                    lua_.GetField(table, part);
                    if (!lua_.IsTable(-1))
                    {
                        if (!silent) Error($"moduleutils::applyOverride: Cannot navigate to override path: {o.Name} (missing {part})");
                        return false;
                    }
                    lua_.Remove(table);
                }
                return false;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // ---- TryApplyLuaModules (moduleutils.cpp:497-552) --------------------------------------------------------

        // parts: the script's path after scripts/, each extension dropped ({"globals", "equipment"},
        // {"zones", "GM_Home", "npcs", "Moogle"}); isReload: every override of the key applied again
        public void TryApply(IReadOnlyList<string> parts, bool isReload = false)
        {
            if (parts == null || parts.Count == 0 || overrides_.Count == 0) return;
            int start = parts[0] == "globals" ? 1 : 0;
            string lookupKey = "xi." + string.Join(".", parts.Skip(start));
            // Also try the bare filename stem for non-xi globals (e.g. utils, npcUtil convention files)
            string bareKey = parts[^1];

            void ApplyRange(string key)
            {
                if (!overrides_.TryGetValue(key, out var list)) return;
                foreach (var o in list)
                {
                    if (!o.Enabled) continue;
                    if (isReload) o.Applied = false;
                    if (!o.Applied) ApplyOverride(o, silent: false);
                }
            }

            ApplyRange(lookupKey);
            if (bareKey != lookupKey) ApplyRange(bareKey);
        }

        // TryApplyRemainingLuaModules (:554-567): every enabled override not applied yet, silently
        public void TryApplyRemaining()
        {
            foreach (var o in Overrides.ToList())
                if (o.Enabled && !o.Applied) ApplyOverride(o, silent: true);
        }

        // ReportLuaModuleUsage's list (:569-582): enabled and not applied
        public List<Override> NotApplied() => Overrides.Where(o => o.Enabled && !o.Applied).ToList();

        // ClearLuaModuleRegistries (:440-450): both registries emptied in place, so any handle held elsewhere stays
        // valid
        public void ClearRegistries()
        {
            try
            {
                lua_.DoString("for i = #xi.module.registry, 1, -1 do xi.module.registry[i] = nil end " +
                              "for i = #xi.module.commandRegistry, 1, -1 do xi.module.commandRegistry[i] = nil end", "=moduleutils");
            }
            catch (LuaException e)
            {
                Error($"moduleutils::ClearLuaModuleRegistries: Failed to clear module registries: {e.Message}");
            }
        }

        // the path parts the C++ hands TryApplyLuaModules for a script (luautils.cpp:500-520, :896-955): every
        // element's extension dropped (path::replace_extension), everything up to and including "scripts" stripped;
        // null without one
        public static List<string> ScriptParts(string filename)
        {
            var parts = new List<string>();
            foreach (var element in filename.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                string ext = Extension(element);
                parts.Add(ext.Length == 0 ? element : element.Substring(0, element.Length - ext.Length));
            }
            int scripts = parts.IndexOf("scripts");
            if (scripts < 0) return null;
            parts.RemoveRange(0, scripts + 1);
            return parts;
        }
    }
}
