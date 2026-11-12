// XI.Verify.Quests -- every C# quest's LegacyPath (carry-quests-1). A LegacyPath is the require name of the Lua file
// the quest replaced: the registration slots the quest by it in the containers list (ZoneHost.QuestScripts.cs
// SortedSlot, in any order: InLoadOrder) and maps it to the key in CSharpQuests.legacy, where !reloadquest finds a
// quest whose Lua file is gone (scripts/commands/reloadquest.lua). Two quests with one LegacyPath would both slot
// there, and the second would take the legacy entry from the first ("Unable to find quest" for it); a LegacyPath
// naming another quest's file slots the quest beside that quest. So: no two quests share one; each lies in its log's
// folder of scripts/quests (xi.quest.area, from scripts/globals/quests.lua; hiddenQuests for a hidden quest); each
// names a Lua file that is in the tree or that git's history deleted; and that file's last text names the quest's
// constant (a hidden quest has none: its key is its LegacyPath's file name). Quest rework C2: every C# container
// kind, a mission's in its log's folder of scripts/missions and a battlefield's in its zone's of scripts/battlefields.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    // the folder above the test's binaries that holds scripts/globals/quests.lua (the csharp folder); null when none
    static string QuestsLuaRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "scripts", "globals", "quests.lua"))) return d.FullName;
        return null;
    }

    // xi.quest.area of <root>/scripts/globals/quests.lua: [xi.questLog.BASTOK] = 'bastok', by the log's number
    static Dictionary<QuestLog, string> LuaQuestAreas(string root)
    {
        var logNumbers = new Dictionary<string, int>
        {
            ["SANDORIA"] = 0, ["BASTOK"] = 1, ["WINDURST"] = 2, ["JEUNO"] = 3, ["OTHER_AREAS"] = 4, ["OUTLANDS"] = 5,
            ["AHT_URHGAN"] = 6, ["CRYSTAL_WAR"] = 7, ["ABYSSEA"] = 8, ["ADOULIN"] = 9, ["COALITION"] = 10,
        };
        var areas = new Dictionary<QuestLog, string>();
        foreach (var line in File.ReadAllLines(Path.Combine(root, "scripts", "globals", "quests.lua")))
        {
            var an = Regex.Match(line, @"^\s*\[xi\.questLog\.(\w+)\]\s*=\s*'(\w+)'");
            if (an.Success && logNumbers.TryGetValue(an.Groups[1].Value, out int n))
                areas[(QuestLog)n] = an.Groups[2].Value;
        }
        return areas;
    }

    // Quest rework C2: the C# missions and battlefields the same way, each kind against its own folder:
    // scripts/missions/<its log's folder>/ (the folder whose files make their Mission:new with that log: zilart's is
    // rotz) and scripts/battlefields/<its zone's folder>/ (the battlefield's zone in sql/bcnm_info.sql, that zone's
    // folder by ContainerFolders.BattlefieldDir); no two containers of any kind share a LegacyPath, each names a Lua
    // file of its kind (in the tree or deleted in git's history) that names its constant
    static void RunLegacyPathGate()
    {
        Console.WriteLine("\n== legacy paths: unique, in the kind's folder, a Lua file naming the container ==");
        string root = QuestsLuaRoot();
        if (root == null)
        {
            Bad("legacy: scripts/globals/quests.lua not found above " + AppContext.BaseDirectory);
            return;
        }
        // the public snapshot (SeaReefBoat) has one commit, so no history of the deleted Lua files this gate reads. Its
        // SNAPSHOT file is written by the publish script and never exists in the dev repo.
        if (File.Exists(Path.Combine(root, "SNAPSHOT")))
        {
            Console.WriteLine("  skipped: a snapshot holds no history of the deleted Lua files");
            return;
        }

        var containers = typeof(QC.Zone).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ContainerScript).IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .Select(t => (Type: t.Name, Script: (ContainerScript)Activator.CreateInstance(t)))
            .Where(q => q.Script.LegacyPath != null)
            .ToList();
        Check("legacy:the C# quests with a LegacyPath are found", "True",
              (containers.Count(c => c.Script is QuestScript) > 500).ToString());

        Check("legacy:no two C# containers share a LegacyPath", "",
              LegacyPathDuplicates(containers.Select(q => (q.Type, q.Script.LegacyPath))));

        var folders = new LegacyFolders { Quests = LuaQuestAreas(root) };
        folders.Quests[QuestScript.HiddenLog] = QuestScript.HiddenArea;
        Check("legacy:xi.quest.area read for every log", "12", folders.Quests.Count.ToString());
        folders.Missions = LuaMissionFolders(root, out string missionProblems);
        Check("legacy:each mission log's files lie in one folder of scripts/missions, one log to a folder", "",
              missionProblems);
        // (ToAU, Zilart, CoP, WoTG and the three nations have none: their last mission files went with C3 T4, C6, C4,
        // T8 and N5/N6. The ToAU, Zilart and nation folders are gone; CoP's and WoTG's keep only helpers.lua, which
        // makes no Mission:new: see PortedLogFolders)
        Check("legacy:the mission logs' folders (those with files)",
              "ACP acp, AMK amk, ASA asa, SoA soa, RoV rov, TVR tvr",
              string.Join(", ", folders.Missions.OrderBy(e => e.Key).Select(e => $"{e.Key} {e.Value}")));
        folders.Battlefields = LuaBattlefieldFolders(root);
        // (a battlefield with no bcnm_records row, the two Aman Troves of Horlais Peak, has no folder and no file)
        Check("legacy:each battlefield file lies in its zone's folder of scripts/battlefields", "",
              BattlefieldFileProblems(root, folders.Battlefields));

        Check("legacy:each LegacyPath is in its kind's folder (scripts/quests/<log's folder>, " +
              "scripts/missions/<log's folder>, scripts/battlefields/<zone's folder>)", "",
              LegacyPathFolderProblems(containers.Select(q => (q.Type, q.Script.LegacyPath, q.Script)), folders));

        // the Lua file's last text: the tree's, else the parent of the commit that last deleted it
        var texts = LuaQuestTexts(root, containers.Select(q => q.Script.LegacyPath + ".lua"), out string gitError);
        if (gitError != null)
        {
            Bad("legacy: " + gitError);
            return;
        }
        Check("legacy:each LegacyPath names a Lua container file, in the tree or deleted in git's history", "",
              string.Join(" ; ", containers.Where(q => !texts.ContainsKey(q.Script.LegacyPath + ".lua"))
                  .Select(q => $"{q.Type}: {q.Script.LegacyPath}")));
        Check("legacy:each LegacyPath's Lua file names the container's constant", "",
              LegacyPathConstantProblems(containers.Where(q => texts.ContainsKey(q.Script.LegacyPath + ".lua"))
                  .Select(q => (q.Type, HiddenNameOf(q.Script), ConstNameOf(q.Script),
                                texts[q.Script.LegacyPath + ".lua"]))));

        // the checks catch what they are for: a LegacyPath moved to another quest's (A Good Pair of Crocs' to A Shot in
        // the Dark's, the hidden-1 review's mutant cr66), a folder of another log, a file that names another quest only
        // (Shady Business's; A Shot in the Dark's names Crocs, its prerequisite)
        var crocs = new QC.Adoulin.AGoodPairOfCrocs();
        var shot = new QC.Adoulin.AShotInTheDark();
        var shady = new QC.Bastok.ShadyBusiness();
        string elsewhere = crocs.LegacyPath.Replace("/adoulin/", "/bastok/");
        Check("legacy:  a LegacyPath moved to another quest's is a duplicate",
              $"AGoodPairOfCrocs, AShotInTheDark: {shot.LegacyPath}",
              LegacyPathDuplicates(new[]
              {
                  ("AGoodPairOfCrocs", shot.LegacyPath), ("AShotInTheDark", shot.LegacyPath),
              }));
        Check("legacy:  a LegacyPath in another log's folder is refused",
              $"AGoodPairOfCrocs: {elsewhere} is not under scripts/quests/adoulin/",
              LegacyPathFolderProblems(new[] { ("AGoodPairOfCrocs", elsewhere, (ContainerScript)crocs) }, folders));
        Check("legacy:  a Lua file that names another quest's constant is refused",
              "AGoodPairOfCrocs: the file does not name A_GOOD_PAIR_OF_CROCS",
              LegacyPathConstantProblems(new[]
              {
                  ("AGoodPairOfCrocs", (string)null, crocs.ConstName, texts[shady.LegacyPath + ".lua"]),
              }));

        // quest rework C2: a test mission and a test battlefield (never in XI.QuestContent) at their Lua files pass;
        // moved under scripts/quests, into another log's or zone's folder, they are refused; a mission and a quest
        // with one LegacyPath are a duplicate
        var arks = new LegacyTestMission();
        var eye = new LegacyTestBattlefield();
        var tests = new (string, string, ContainerScript)[]
        {
            ("LegacyTestMission", arks.LegacyPath, arks), ("LegacyTestBattlefield", eye.LegacyPath, eye),
        };
        Check("legacy:  a mission and a battlefield at their own Lua files pass", "",
              LegacyPathFolderProblems(tests, folders));
        var testTexts = LuaQuestTexts(root, tests.Select(t => t.Item2 + ".lua"), out _);
        Check("legacy:  ... their files name their constants", "",
              LegacyPathConstantProblems(tests.Where(t => testTexts.ContainsKey(t.Item2 + ".lua"))
                  .Select(t => (t.Item1, (string)null, ConstNameOf(t.Item3), testTexts[t.Item2 + ".lua"]))) +
              (testTexts.Count == 2 ? "" : "a file not read"));
        string asQuest = "scripts/quests/cop/1_2_Below_the_Arks";
        string otherLog = "scripts/missions/rotz/1_2_Below_the_Arks";
        string otherZone = "scripts/battlefields/Waughroon_Shrine/eye_of_the_storm";
        Check("legacy:  a mission LegacyPath under scripts/quests, in another log's folder, a battlefield's in " +
              "another zone's folder: refused",
              $"LegacyTestMission: {asQuest} is not under scripts/missions/tvr/ ; " +
              $"LegacyTestMission: {otherLog} is not under scripts/missions/tvr/ ; " +
              $"LegacyTestBattlefield: {otherZone} is not under scripts/battlefields/Chamber_of_Oracles/",
              LegacyPathFolderProblems(new (string, string, ContainerScript)[]
              {
                  ("LegacyTestMission", asQuest, arks), ("LegacyTestMission", otherLog, arks),
                  ("LegacyTestBattlefield", otherZone, eye),
              }, folders));
        Check("legacy:  a mission sharing a quest's LegacyPath is a duplicate",
              $"AGoodPairOfCrocs, LegacyTestMission: {crocs.LegacyPath}",
              LegacyPathDuplicates(new[]
              {
                  ("AGoodPairOfCrocs", crocs.LegacyPath), ("LegacyTestMission", crocs.LegacyPath),
              }));
        Check("legacy:  the Riverne zones' folder: '-' as '_'", "Riverne_Site_A01",
              folders.Battlefields.TryGetValue("battlefield.ouryu_cometh", out var riverne) ? riverne : "(none)");
    }

    // Voracious Resurgence 10-1 at its Lua file, for the gate's own checks (never in XI.QuestContent; no C3 lane ports
    // the TVR log; CoP 1-2's file is gone since quest rework C3 C1, as CoP 1-1's is since C2 Task 7)
    sealed class LegacyTestMission : MissionScript
    {
        public LegacyTestMission() : base(MissionLog.TVR, "TO_MOVALPOLOS") { }
        public override string LegacyPath => "scripts/missions/tvr/10_1_To_Movalpolos";
        protected override void Build(MissionScriptBuilder b) { }
    }

    // Eye of the Storm (Chamber of Oracles) at its Lua file: a container of the battlefield kind (Cactuar Suave until
    // quest rework C2 Task 8, which put Cactuar Suave in C#)
    sealed class LegacyTestBattlefield : ContainerScript
    {
        public override ContainerKind Kind => ContainerKind.Battlefield;
        public override string Key => ContainerKeys.Battlefield("EYE_OF_THE_STORM");
        public override string LegacyPath => "scripts/battlefields/Chamber_of_Oracles/eye_of_the_storm";
    }

    // the folders a LegacyPath of each kind lies in: the quest logs' (xi.quest.area), the mission logs'
    // (LuaMissionFolders) and the battlefield keys' (LuaBattlefieldFolders)
    sealed class LegacyFolders
    {
        public Dictionary<QuestLog, string> Quests;
        public Dictionary<MissionLog, string> Missions;
        public Dictionary<string, string> Battlefields;
    }

    // a hidden quest's file name (its key's), else null: only a quest can be hidden
    static string HiddenNameOf(ContainerScript c) => (c as QuestScript)?.HiddenName;

    // the constant the container's Lua file names: a quest's or a mission's ConstName, a battlefield's
    // xi.battlefield.id constant (its key's, upper case)
    static string ConstNameOf(ContainerScript c) => c switch
    {
        QuestScript q => q.ConstName,
        MissionScript m => m.ConstName,
        _ when c.Kind == ContainerKind.Battlefield => c.Key.Substring(c.Key.IndexOf('.') + 1).ToUpperInvariant(),
        _ => throw new InvalidOperationException($"{c.Key}: no constant for a container of kind {c.Kind}"),
    };

    // "<type>, <type>: <path>" for each LegacyPath two or more containers have, " ; " between
    static string LegacyPathDuplicates(IEnumerable<(string Type, string Path)> quests) =>
        string.Join(" ; ", quests.GroupBy(q => q.Path, StringComparer.Ordinal).Where(g => g.Count() > 1)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"{string.Join(", ", g.Select(q => q.Type))}: {g.Key}"));

    // the mission logs whose every Lua file is ported (C3 T4 ToAU, C4 CoP, C6 Zilart, T8 WoTG, N5/N6 the three
    // nations), so no Mission:new names their folder: each one's LegacyPaths must lie in the folder it had, named here
    static readonly Dictionary<MissionLog, string> PortedLogFolders = new()
    {
        [MissionLog.ToAU] = "scripts/missions/toau/",
        [MissionLog.CoP] = "scripts/missions/cop/",
        [MissionLog.Zilart] = "scripts/missions/rotz/",
        [MissionLog.WoTG] = "scripts/missions/wotg/",
        [MissionLog.Sandoria] = "scripts/missions/sandoria/",
        [MissionLog.Bastok] = "scripts/missions/bastok/",
        [MissionLog.Windurst] = "scripts/missions/windurst/",
    };

    // the folder the container's LegacyPath must lie in ("scripts/<kind>/<folder>/"), by its kind; null with why when
    // there is none
    static string LegacyFolder(ContainerScript c, LegacyFolders folders, out string none)
    {
        none = null;
        switch (c)
        {
            case QuestScript q:
                if (folders.Quests.TryGetValue(q.Log, out var area)) return "scripts/quests/" + area + "/";
                none = $"no folder for log {(int)q.Log}";
                return null;
            case MissionScript m:
                if (folders.Missions.TryGetValue(m.Log, out var log)) return "scripts/missions/" + log + "/";
                // a log with no Lua file left (every one ported) has no folder to read: its LegacyPaths stand for it
                // (ToAU since C3 T4; CoP since C4 and WoTG since T8, whose folders keep only helpers.lua, no
                // Mission:new; Zilart since C6; the three nations since N5/N6)
                if (PortedLogFolders.TryGetValue(m.Log, out var gone) && m.LegacyPath != null
                    && m.LegacyPath.StartsWith(gone, StringComparison.Ordinal))
                    return gone;
                none = $"no folder of scripts/missions for mission log {m.Log}";
                return null;
            default:
                if (c.Kind == ContainerKind.Battlefield && folders.Battlefields.TryGetValue(c.Key, out var zone))
                    return "scripts/battlefields/" + zone + "/";
                none = c.Kind == ContainerKind.Battlefield ? $"no zone's folder for {c.Key}" : $"unknown kind {c.Kind}";
                return null;
        }
    }

    static string LegacyPathFolderProblems(IEnumerable<(string Type, string Path, ContainerScript Script)> containers,
        LegacyFolders folders)
    {
        var bad = new List<string>();
        foreach (var (type, path, script) in containers)
        {
            string prefix = LegacyFolder(script, folders, out string none);
            if (prefix == null)
            {
                bad.Add($"{type}: {none}");
                continue;
            }
            string file = path.StartsWith(prefix, StringComparison.Ordinal) ? path.Substring(prefix.Length) : null;
            if (file == null || file.Length == 0 || file.Contains('/') ||
                file.EndsWith(".lua", StringComparison.Ordinal))
                bad.Add($"{type}: {path} is not under {prefix}");
        }
        return string.Join(" ; ", bad);
    }

    // Mission:new(xi.mission.log_id.<LOG>, ...) in a mission file
    static readonly Regex MissionNew = new Regex(@"\bMission:new\(\s*xi\.mission\.log_id\.(\w+)");

    // each mission log's folder under <root>/scripts/missions: the folder whose files make their Mission:new with that
    // log (a log with no file has none); problems: a log made in two folders, a folder making two logs, an unknown log
    static Dictionary<MissionLog, string> LuaMissionFolders(string root, out string problems)
    {
        var seen = new Dictionary<MissionLog, SortedSet<string>>();
        var logsOf = new Dictionary<string, SortedSet<MissionLog>>(StringComparer.Ordinal);
        var bad = new List<string>();
        string dir = Path.Combine(root, "scripts", "missions");
        foreach (var folderPath in Directory.GetDirectories(dir).OrderBy(d => d, StringComparer.Ordinal))
        {
            string folder = Path.GetFileName(folderPath);
            foreach (var file in Directory.GetFiles(folderPath, "*.lua").OrderBy(f => f, StringComparer.Ordinal))
                foreach (Match m in MissionNew.Matches(File.ReadAllText(file)))
                {
                    if (!Enum.TryParse(m.Groups[1].Value, true, out MissionLog log))
                    {
                        bad.Add($"{folder}/{Path.GetFileName(file)}: unknown log {m.Groups[1].Value}");
                        continue;
                    }
                    (seen.TryGetValue(log, out var fs) ? fs : seen[log] = new SortedSet<string>(StringComparer.Ordinal))
                        .Add(folder);
                    (logsOf.TryGetValue(folder, out var ls) ? ls : logsOf[folder] = new SortedSet<MissionLog>())
                        .Add(log);
                }
        }
        bad.AddRange(seen.Where(e => e.Value.Count > 1).Select(e => $"{e.Key} in {string.Join(", ", e.Value)}"));
        bad.AddRange(logsOf.Where(e => e.Value.Count > 1).Select(e => $"{e.Key} makes {string.Join(", ", e.Value)}"));
        problems = string.Join(" ; ", bad);
        return seen.ToDictionary(e => e.Key, e => e.Value.Min);
    }

    // battlefield key -> its zone's folder under scripts/battlefields: the key's xi.battlefield.id (the generated
    // Battlefield constant of its BattlefieldKey member's name), that battlefield's zone (bcnm_records of
    // sql/bcnm_info.sql), the zone's name (sql/zone_settings.sql), and that name's folder
    // (ContainerFolders.BattlefieldDir, as the call scan finds it). A key whose battlefield or zone has no row has none
    static Dictionary<string, string> LuaBattlefieldFolders(string root)
    {
        var zoneOf = new Dictionary<int, int>();
        foreach (var line in File.ReadLines(Path.Combine(root, "sql", "bcnm_info.sql")))
            if (Regex.Match(line, @"^INSERT INTO `bcnm_records` VALUES \((\d+),(\d+),") is { Success: true } m)
                zoneOf[int.Parse(m.Groups[1].Value)] = int.Parse(m.Groups[2].Value);
        var zoneName = new Dictionary<int, string>();
        foreach (var line in File.ReadLines(Path.Combine(root, "sql", "zone_settings.sql")))
            if (Regex.Match(line, @"^INSERT INTO `zone_settings` VALUES \((\d+),'[^']*',\d+,'([^']*)'\)") is
                { Success: true } m)
                zoneName[int.Parse(m.Groups[1].Value)] = m.Groups[2].Value;
        var folders = new Dictionary<string, string>(StringComparer.Ordinal);
        string battlefieldsDir = Path.Combine(root, "scripts", "battlefields");
        foreach (var f in typeof(QC.BattlefieldKey).GetFields())
        {
            if (typeof(QC.Battlefield).GetField(f.Name) is not { } id) continue;
            if (zoneOf.TryGetValue((int)id.GetRawConstantValue(), out int zone) &&
                zoneName.TryGetValue(zone, out var name))
                folders[(string)f.GetRawConstantValue()] =
                    Path.GetFileName(ContainerFolders.BattlefieldDir(battlefieldsDir, name));
        }
        return folders;
    }

    // each battlefield file of <root>/scripts/battlefields (battlefieldId = xi.battlefield.id.<CONST>) in the folder
    // LuaBattlefieldFolders gives its key: the rule's proof over the whole tree
    static string BattlefieldFileProblems(string root, IReadOnlyDictionary<string, string> folders)
    {
        var bad = new List<string>();
        string dir = Path.Combine(root, "scripts", "battlefields");
        var files = Directory.GetFiles(dir, "*.lua", SearchOption.AllDirectories);
        foreach (var file in files.OrderBy(f => f, StringComparer.Ordinal))
        {
            string rel = Path.GetRelativePath(dir, file).Replace('\\', '/');
            var ids = Regex.Matches(File.ReadAllText(file), @"\bbattlefieldId\s*=\s*xi\.battlefield\.id\.(\w+)");
            if (ids.Count != 1)
            {
                bad.Add($"{rel}: {ids.Count} battlefieldId lines");
                continue;
            }
            string key = ContainerKeys.Battlefield(ids[0].Groups[1].Value);
            string folder = rel.Substring(0, rel.IndexOf('/'));
            if (!folders.TryGetValue(key, out var want) || want != folder)
                bad.Add($"{rel}: {key}'s zone's folder is {want ?? "(none)"}");
        }
        return string.Join(" ; ", bad);
    }

    // a quest's constant named in its file's text as a whole word (xi.quest.id.<area>.<CONST>, or a header's
    // "Quest ID: <CONST>"); a hidden quest has no constant
    static string LegacyPathConstantProblems(
        IEnumerable<(string Type, string HiddenName, string ConstName, string Text)> quests) =>
        string.Join(" ; ", quests
            .Where(q => q.HiddenName == null &&
                        !Regex.IsMatch(q.Text, @"(?<![A-Za-z0-9_])" + Regex.Escape(q.ConstName) + @"(?![A-Za-z0-9_])"))
            .Select(q => $"{q.Type}: the file does not name {q.ConstName}"));

    // rel path -> the file's last text, for each of the paths in the tree or deleted in git's history (one git log
    // and one git cat-file --batch) under scripts/quests, scripts/missions and scripts/battlefields (quest rework C2);
    // error: why git could not be read
    static Dictionary<string, string> LuaQuestTexts(string root, IEnumerable<string> paths, out string error)
    {
        error = null;
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        var wanted = new HashSet<string>(paths, StringComparer.Ordinal);
        foreach (var rel in wanted)
            if (File.Exists(Path.Combine(root, rel))) texts[rel] = File.ReadAllText(Path.Combine(root, rel));
        var gone = wanted.Where(rel => !texts.ContainsKey(rel)).ToHashSet(StringComparer.Ordinal);
        if (gone.Count == 0) return texts;

        string Git(string args, string input, out int code)
        {
            var psi = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = root, RedirectStandardInput = input != null, RedirectStandardOutput = true,
                RedirectStandardError = true, UseShellExecute = false, StandardOutputEncoding = Encoding.Latin1,
            };
            using var p = Process.Start(psi);
            var stdout = p.StandardOutput.ReadToEndAsync();
            var stderr = p.StandardError.ReadToEndAsync();
            if (input != null)
            {
                p.StandardInput.Write(input);
                p.StandardInput.Close();
            }
            p.WaitForExit();
            code = p.ExitCode;
            return code == 0 ? stdout.Result : stderr.Result;
        }

        // the newest deletion of each path first: "C <hash>" lines, then the paths that commit deleted
        string log = Git("log --diff-filter=D --no-renames --name-only --format=C%x20%H -- " +
                         "scripts/quests scripts/missions scripts/battlefields", null, out int lc);
        if (lc != 0)
        {
            error = "git log failed: " + log.Trim();
            return texts;
        }
        var deletedAt = new Dictionary<string, string>(StringComparer.Ordinal);
        string commit = null;
        foreach (var line in log.Split('\n'))
        {
            if (line.StartsWith("C ", StringComparison.Ordinal)) commit = line.Substring(2).Trim();
            else if (line.Length > 0 && commit != null && gone.Contains(line) && !deletedAt.ContainsKey(line))
                deletedAt[line] = commit;
        }
        if (deletedAt.Count == 0) return texts;

        // git cat-file --batch: "<hash> blob <size>\n<bytes>\n" per object, in the request order ("<name> missing")
        var order = deletedAt.OrderBy(e => e.Key, StringComparer.Ordinal).ToList();
        string batch = Git("cat-file --batch", string.Concat(order.Select(e => $"{e.Value}^:{e.Key}\n")), out int bc);
        if (bc != 0)
        {
            error = "git cat-file failed: " + batch.Trim();
            return texts;
        }
        int at = 0;
        foreach (var (rel, _) in order)
        {
            int nl = batch.IndexOf('\n', at);
            var head = batch.Substring(at, nl - at).Split(' ');
            at = nl + 1;
            if (head.Length != 3 || head[1] != "blob") continue;
            int size = int.Parse(head[2]);
            texts[rel] = batch.Substring(at, size);
            at += size + 1;
        }
        return texts;
    }
}
