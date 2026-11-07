using System;
using System.IO;
using XI.Script;

namespace XI.Zone
{
    // P11 item 2: the file watcher (luautils.cpp:578 makes it at the end of luautils::init on "scripts", "modules" and
    // "settings"; time_server.cpp:187 drains it with TryReloadFilewatchList each 2400 ms time server tick, right after
    // OnTimeServerTick). USER RULING 2026-11-05: opt in, XI.Host only (map watch=1): without EnableFileWatcher nothing
    // is watched and nothing reloads, so the verify suites and the golden worlds run as before.
    public sealed partial class ZoneHost
    {
        // the watcher, null while off
        public ScriptFileWatcher FileWatcher { get; private set; }

        // luautils.cpp:578: scripts/ (the engine's scripts folder), modules/ and settings/ under the root watched from
        // now on. watcher: one made by the caller (a test's); null: the three folders
        public void EnableFileWatcher(ScriptFileWatcher watcher = null)
        {
            if (scripts_ == null) return;
            FileWatcher?.Dispose();
            FileWatcher = watcher ?? new ScriptFileWatcher(new[]
            {
                ("scripts", scripts_.ScriptsDir),
                ("modules", Path.Combine(scripts_.Root, "modules")),
                ("settings", Path.Combine(scripts_.Root, "settings")),
            }, log_);
            // a module_utils.lua run again puts its own modifyInteractionEntry back: the guard goes on again
            scripts_.ModuleFileRerun = InstallPatchGuard;
        }

        public void DisableFileWatcher()
        {
            FileWatcher?.Dispose();
            FileWatcher = null;
            if (scripts_ != null) scripts_.ModuleFileRerun = null;
        }

        // luautils::TryReloadFilewatchList: the changes since the last tick; nothing to do without any. The caches go
        // first. The C++ clears luaCache; this host resolves every script function at its call, so it clears its
        // memos of a script's contents instead: zoneHasTick_ (the zones whose Zone.lua had no onZoneTick) and
        // scriptHasMixins_ (the mob scripts that name mixins, which the C++ reads again at each spawn). Then each
        // added or modified file is reloaded (LoadLuaObjectFromFile(file, true)). A deleted or moved file is not
        // handled, as in the C++. A reload that throws (a file another program still holds open, or one deleted
        // before it was read) is logged and the next file goes on: the C++ reads through Lua, which reports such a
        // file as a load error.
        public void TryReloadFilewatchList()
        {
            if (FileWatcher == null || scripts_ == null) return;
            var changed = FileWatcher.PopChangedLuaFiles();
            if (changed.Count == 0) return;

            zoneHasTick_.Clear();
            scriptHasMixins_.Clear();
            foreach (var (file, action) in changed)
            {
                // moved and deleted files: a TODO in the C++
                if (action is not (ScriptFileWatcher.FileAction.Add or ScriptFileWatcher.FileAction.Modified)) continue;
                log_($"[FileWatcher] {file}");
                try
                {
                    scripts_.ReloadLuaObjectFromFile(file);
                }
                catch (Exception e)
                {
                    log_($"[FileWatcher] reload of {file} failed: {e.GetType().Name}: {e.Message}");
                }
            }
        }
    }
}
