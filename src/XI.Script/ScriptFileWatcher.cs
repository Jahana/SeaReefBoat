using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace XI.Script
{
    // common/filewatcher.cpp's Filewatcher: each folder watched recursively (efsw's FileWatcher, here a
    // System.IO.FileSystemWatcher), every change queued from the watcher's thread with its path as the C++ has it
    // ("scripts/zones/Bastok_Markets/npcs/Reet.lua": the watched folder's name, then the path below it), and
    // PopChangedLuaFiles drained on the main thread. luautils::init makes one on "scripts", "modules" and "settings"
    // (luautils.cpp:578); the C# map host makes it only when asked (XI.Host map watch=1, ZoneHost.EnableFileWatcher).
    public sealed class ScriptFileWatcher : IDisposable
    {
        // Filewatcher::Action, efsw's actions
        public enum FileAction
        {
            Add = 1,
            Delete = 2,
            Modified = 3,
            Moved = 4,
        }

        private readonly List<FileSystemWatcher> watchers_ = new List<FileSystemWatcher>();
        private readonly ConcurrentQueue<(string Path, FileAction Action)> queue_ =
            new ConcurrentQueue<(string, FileAction)>();
        private readonly Action<string> log_;

        // folders: (name, dir): the dir watched, its changes queued as "<name>/<path below dir>". A dir that is not
        // there is logged and not watched (efsw's addWatch returns an error for it)
        public ScriptFileWatcher(IEnumerable<(string Name, string Dir)> folders, Action<string> log)
        {
            log_ = log ?? (_ => { });
            foreach (var (name, dir) in folders)
            {
                if (!Directory.Exists(dir))
                {
                    log_($"[FileWatcher] not watched, no such folder: {dir}");
                    continue;
                }
                var w = new FileSystemWatcher(dir)
                {
                    IncludeSubdirectories = true,
                    // efsw's Windows filter: creation, last write, file name, dir name, size
                    NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.FileName |
                                   NotifyFilters.DirectoryName | NotifyFilters.Size,
                    // efsw's WinBufferSize default is 63 KB; the most a FileSystemWatcher takes is 64 KB
                    InternalBufferSize = 64 * 1024,
                };
                string prefix = name;
                w.Created += (_, e) => Enqueue(Join(prefix, e.Name), FileAction.Add);
                w.Deleted += (_, e) => Enqueue(Join(prefix, e.Name), FileAction.Delete);
                w.Changed += (_, e) => Enqueue(Join(prefix, e.Name), FileAction.Modified);
                // efsw's FILE_ACTION_RENAMED_NEW_NAME: Moved, with the new name
                w.Renamed += (_, e) => Enqueue(Join(prefix, e.Name), FileAction.Moved);
                w.Error += (_, e) => log_($"[FileWatcher] {prefix}: {e.GetException()?.Message}");
                w.EnableRaisingEvents = true;
                watchers_.Add(w);
            }
        }

        // the folders being watched
        public int Watching => watchers_.Count;

        private static string Join(string prefix, string name) => prefix + "/" + (name ?? "").Replace('\\', '/');

        // Filewatcher::handleFileAction's enqueue (a test puts a change in by hand with it)
        public void Enqueue(string path, FileAction action) => queue_.Enqueue((path, action));

        // std::set<std::pair<path, Action>>'s order: the path (element-wise), then the action
        private static readonly IComparer<(string Path, FileAction Action)> ChangeOrder =
            Comparer<(string Path, FileAction Action)>.Create((a, b) =>
            {
                int c = ScriptEngine.PathCompare(a.Path, b.Path);
                return c != 0 ? c : a.Action.CompareTo(b.Action);
            });

        // Filewatcher::popChangedLuaFilesList: everything queued so far taken out; the .lua files kept, each
        // (path, action) once, in std::set order (the path element-wise, then the action)
        public List<(string Path, FileAction Action)> PopChangedLuaFiles()
        {
            var set = new SortedSet<(string Path, FileAction Action)>(ChangeOrder);
            while (queue_.TryDequeue(out var change))
            {
                string path = change.Path;
                int slash = path.LastIndexOf('/');
                if (LuaModules.Extension(slash < 0 ? path : path.Substring(slash + 1)) == ".lua") set.Add(change);
            }
            return new List<(string Path, FileAction Action)>(set);
        }

        public void Dispose()
        {
            foreach (var w in watchers_)
            {
                w.EnableRaisingEvents = false;
                w.Dispose();
            }
            watchers_.Clear();
        }
    }
}
