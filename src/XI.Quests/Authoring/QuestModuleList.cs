// XI.Quests -- which modules modules/init.txt enables, by the rules the C++ server reads it with (moduleutils.cpp
// LoadLuaModules; quest rework P4). The C# modules (QuestModule) are enabled by the same lines as the Lua files they port.
using System;
using System.Collections.Generic;
using System.Linq;

namespace XI.Quests.Authoring
{
    public static class QuestModuleList
    {
        // init.txt's entries as LoadLuaModules reads them: one per line (split at '\n'); a line that is empty, a lone "\r" or
        // starts with '#' is skipped (a '#' after anything, a space included, is no comment); the rest is
        // "./modules/" + line trimmed of spaces, tabs, CR and LF at both ends. So trailing whitespace goes, a leading space
        // stays (and names no file), and a line of whitespace alone is "./modules/", every module
        public static IReadOnlyList<string> Entries(string text)
        {
            var entries = new List<string>();
            if (text == null) return entries;
            foreach (var line in text.Split('\n'))
            {
                if (line.Length == 0 || line[0] == '#' || line == "\r") continue;
                entries.Add(("./modules/" + line).Trim(' ', '\t', '\r', '\n'));
            }
            return entries;
        }

        // the entries of the lines given as init.txt holds them (the golden harness's module lines)
        public static IReadOnlyList<string> Entries(IEnumerable<string> lines) => Entries(lines == null ? null : string.Join("\n", lines));

        // whether names compare without case, as the file system the server runs on does: the C++ server on Windows finds
        // ./modules/Era/ (NTFS), on Linux it does not
        public static bool IgnoreCase { get; } = OperatingSystem.IsWindows();

        // whether an entry enables the module's Lua file (modules/<path>.lua): the entry is the file, or a folder above it
        // (a trailing '/' or none; "." segments, doubled separators and ".." folded as the file system would). Names compare
        // as the file system does (IgnoreCase: without case on Windows, with it elsewhere)
        public static bool Enables(string entry, string luaFile) => Enables(entry, luaFile, IgnoreCase);

        public static bool Enables(string entry, string luaFile, bool ignoreCase)
        {
            var e = Segments(entry);
            var f = Segments(luaFile);
            var cmp = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (e == null || f == null || e.Count == 0 || !string.Equals(e[0], "modules", cmp) || e.Count > f.Count) return false;
            for (int i = 0; i < e.Count; i++)
                if (!string.Equals(e[i], f[i], cmp)) return false;
            return true;
        }

        // the Lua files of the modules the entries enable, in the order LoadLuaModules loads them: each file entry in line
        // order, then each folder entry's files (in path order) in line order; a module enabled twice is loaded once, at its
        // first place (the C++ runs such a file twice; a Lua module that patches a quest gives the same result either way)
        public static IReadOnlyList<string> LoadOrder(IReadOnlyList<string> entries, IEnumerable<string> luaFiles) =>
            LoadOrder(entries, luaFiles, IgnoreCase);

        public static IReadOnlyList<string> LoadOrder(IReadOnlyList<string> entries, IEnumerable<string> luaFiles, bool ignoreCase)
        {
            var files = luaFiles.Distinct(StringComparer.Ordinal).OrderBy(f => f, StringComparer.Ordinal).ToList();
            var order = new List<string>();
            // the file entries first (expandedList starts as the list itself; its folders are skipped there)
            foreach (var entry in entries)
                foreach (var f in files)
                    if (Segments(entry) is { } e && Segments(f) is { } fs && e.Count == fs.Count && Enables(entry, f, ignoreCase) && !order.Contains(f))
                        order.Add(f);
            // then each folder's files, appended in line order
            foreach (var entry in entries)
                foreach (var f in files)
                    if (Enables(entry, f, ignoreCase) && !order.Contains(f))
                        order.Add(f);
            return order;
        }

        // the path's segments from the csharp folder: "./" and "." dropped, empty segments (a doubled or trailing '/') dropped,
        // ".." takes the one before (null when it climbs out); '\' is a separator too
        static List<string> Segments(string path)
        {
            if (path == null) return null;
            var parts = new List<string>();
            foreach (var p in path.Replace('\\', '/').Split('/'))
            {
                if (p.Length == 0 || p == ".") continue;
                if (p == "..")
                {
                    if (parts.Count == 0) return null;
                    parts.RemoveAt(parts.Count - 1);
                    continue;
                }
                parts.Add(p);
            }
            return parts;
        }
    }
}
