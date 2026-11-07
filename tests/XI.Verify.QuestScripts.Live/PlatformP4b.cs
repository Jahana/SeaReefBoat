using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;

// quest rework P4b: every shipped C# quest module's condition and name against its Lua module. Each QuestModule of
// XI.QuestContent (found by reflection, as the registration finds them) ports a Lua file that registers one Module;
// the file is run as moduleutils.cpp's LoadLuaModules runs it (modules/module_utils.lua run, the registries emptied,
// the file run, its Module read from xi.module.registry; the overrides are not applied here) under
// every cell of a settings matrix, and in each cell Module:new's name must be the C# Name and m.enabled the C# Condition.
//
// The matrix: RESTRICT_CONTENT 0/1 x ENABLE_<tag> 0/1 for every expansion of xi.expansion (module_utils.lua's own table,
// so an expansion added there is varied too; it must name the same expansions as QuestExpansion), every combination;
// a setting of xi.settings.main that either side reads beyond those is varied 0/1 as well (the reads are recorded on the
// Lua side through a proxy xi.settings, on the C# side through the IQuestSettings given). A module whose condition
// cannot be checked this way fails with its name: its file does not load or registers no Module or more than one, it
// reads xi.settings beyond main (IQuestSettings reads only main), its C# Condition throws, or it reads too many settings.
internal static partial class Program
{
    // the settings varied beyond RESTRICT_CONTENT and the ENABLE_<tag>s, at most (each doubles the cells)
    private const int P4bMaxExtraSettings = 4;

    private static void RunP4bConditions()
    {
        string root = Path.GetDirectoryName(dataDir_);
        string utils = Path.Combine(root, "modules", "module_utils.lua").Replace('\\', '/');
        var shipped = ShippedQuestModuleTypes();
        Check("p4b: the shipped quest modules are found by reflection, The Gustaberg Tour's among them", "True",
              shipped.Any(t => t.Module?.LuaFile == GustabergModule).ToString());
        foreach (var (type, _, error) in shipped.Where(t => t.Error != null))
            Check($"p4b: {type.FullName}: its constructor runs", "", error);

        Lua(P4bSave);
        try
        {
            // xi.expansion as module_utils.lua declares it, NAME=value
            var luaExpansions = Lua($"assert(loadfile({P4bStr(utils)}))() local t = {{}} for k, v in pairs(xi.expansion) do t[#t + 1] = k .. '=' .. v end table.sort(t) report(table.concat(t, ' '))");
            var csExpansions = string.Join(" ", Enum.GetValues<QuestExpansion>().Select(e => $"{QuestModule.Tag(e)}={(int)e}").OrderBy(x => x, StringComparer.Ordinal));
            Check("p4b: xi.expansion (module_utils.lua) and QuestExpansion name the same expansions", csExpansions, luaExpansions);
            var baseVars = new List<string> { "RESTRICT_CONTENT" };
            baseVars.AddRange(luaExpansions.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(kv => kv.Split('=')).OrderBy(kv => int.Parse(kv[1])).Select(kv => "ENABLE_" + kv[0]));

            foreach (var (_, m, _) in shipped.Where(t => t.Module != null))
                Check($"p4b: {m.Name} ({m.LuaFile}): Module:new's name and m.enabled equal the C# Name and Condition in every settings cell", "",
                      string.Join(" | ", P4bModuleProblems(m, root, utils, baseVars)));
        }
        finally
        {
            Lua(P4bRestore);
        }
    }

    // the shipped QuestModule types, each with its instance or its constructor's error
    private static List<(Type Type, QuestModule Module, string Error)> ShippedQuestModuleTypes()
    {
        var list = new List<(Type, QuestModule, string)>();
        foreach (var t in typeof(XI.QuestContent.Zone).Assembly.GetTypes()
                     .Where(t => t.IsClass && !t.IsAbstract && typeof(QuestModule).IsAssignableFrom(t))
                     .OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            try
            {
                list.Add((t, (QuestModule)Activator.CreateInstance(t), null));
            }
            catch (Exception e)
            {
                list.Add((t, null, (e is System.Reflection.TargetInvocationException { InnerException: { } inner } ? inner : e).Message));
            }
        }
        return list;
    }

    // the module's disagreements with its Lua file over the matrix (none: it agrees in every cell)
    private static List<string> P4bModuleProblems(QuestModule m, string root, string utils, List<string> baseVars)
    {
        string file = Path.Combine(root, m.LuaFile).Replace('\\', '/');
        if (!File.Exists(file)) return new List<string> { $"its Lua file {m.LuaFile} is not there" };
        var vars = new List<string>(baseVars);
        while (true)
        {
            var problems = new List<string>();
            int cells = 1 << vars.Count;
            // the Lua side: one character a cell ('1' / '0' m.enabled, 'E' the file errs, 'N' not one Module, 'B' enabled
            // not a boolean), the names, the xi.settings.main settings read, the other xi.settings tables read, the first error
            Lua(P4bMatrix(utils, file, vars));
            var res = got.ToList();
            string Field(string tag) => res.FirstOrDefault(r => r.StartsWith(tag + ":", StringComparison.Ordinal))?.Substring(tag.Length + 1) ?? "";
            string luaCells = Field("cells"), luaError = Field("error");
            var names = Field("names").Split('\t', StringSplitOptions.RemoveEmptyEntries);
            var reads = new HashSet<string>(Field("reads").Split('\t', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
            var foreign = Field("foreign").Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (luaCells.Length != cells) return new List<string> { $"its Lua file did not run over the matrix: {luaError}" };
            if (luaCells.Contains('E')) problems.Add($"its Lua file errs in {luaCells.Count(ch => ch == 'E')} of {cells} cells: {luaError}");
            if (luaCells.Contains('N')) problems.Add($"its Lua file registers {(names.Length == 0 ? "no Module" : "more than one Module")} (a C# module ports one Module)");
            if (luaCells.Contains('B')) problems.Add("its Lua m.enabled is not a boolean");
            if (foreign.Length > 0) problems.Add($"its Lua file reads xi.settings.{string.Join(", xi.settings.", foreign)}, which a C# Condition cannot (IQuestSettings reads xi.settings.main)");
            if (names.Length > 0 && !(names.Length == 1 && names[0] == m.Name))
                problems.Add($"Module:new's name is {string.Join(" / ", names)}, the C# Name {m.Name}");

            // the C# side, the same cells
            var csReads = new HashSet<string>(StringComparer.Ordinal);
            var cs = new char[cells];
            string csError = null;
            for (int i = 0; i < cells; i++)
            {
                var settings = new P4bSettings(vars, i, csReads);
                try
                {
                    cs[i] = m.Condition(settings) ? '1' : '0';
                }
                catch (Exception e)
                {
                    cs[i] = 'T';
                    csError ??= e.Message;
                }
            }
            if (csError != null) problems.Add($"its C# Condition throws: {csError}");

            var extras = reads.Concat(csReads).Distinct().Where(r => !vars.Contains(r)).OrderBy(r => r, StringComparer.Ordinal).ToList();
            if (extras.Count > 0 && problems.Count == 0)
            {
                if (vars.Count - baseVars.Count + extras.Count > P4bMaxExtraSettings)
                    return new List<string> { $"its condition reads more settings than the matrix varies ({string.Join(", ", vars.Skip(baseVars.Count).Concat(extras))}; at most {P4bMaxExtraSettings} beyond RESTRICT_CONTENT and the ENABLE_<tag>s)" };
                vars.AddRange(extras);
                continue;
            }
            if (problems.Count > 0) return problems;

            var differ = Enumerable.Range(0, cells).Where(i => luaCells[i] != cs[i]).ToList();
            if (differ.Count > 0)
                problems.Add($"m.enabled and Condition differ in {differ.Count} of {cells} cells, e.g. " +
                             string.Join("; ", differ.Take(3).Select(i => $"{P4bCell(vars, i)}: Lua {luaCells[i] == '1'}, C# {cs[i] == '1'}")));
            Console.WriteLine($"  p4b: {m.Name}: {vars.Count} settings varied ({cells} cells; read: {string.Join(" ", vars.Where(v => reads.Contains(v) || csReads.Contains(v)))}), enabled in {luaCells.Count(ch => ch == '1')}");
            return problems;
        }
    }

    // a cell as the settings at 1 ("[RESTRICT_CONTENT=1; ENABLE_<tag>=1: ROTZ COP; the rest 0]")
    private static string P4bCell(List<string> vars, int cell)
    {
        var on = vars.Where((v, j) => (cell >> j & 1) == 1).ToList();
        var enable = on.Where(v => v.StartsWith("ENABLE_", StringComparison.Ordinal)).Select(v => v.Substring(7)).ToList();
        var rest = on.Where(v => !v.StartsWith("ENABLE_", StringComparison.Ordinal)).Select(v => v + "=1").ToList();
        rest.Add($"ENABLE_<tag>=1: {(enable.Count == 0 ? "none" : string.Join(" ", enable))}");
        return $"[{string.Join("; ", rest)}; the rest 0]";
    }

    // xi.settings.main as the C# Condition reads it in one cell: each varied setting its bit, any other 0 (unset, as the
    // server's settings read an absent one); the names read are recorded
    private sealed class P4bSettings : IQuestSettings
    {
        private readonly List<string> vars_;
        private readonly int cell_;
        private readonly HashSet<string> reads_;

        public P4bSettings(List<string> vars, int cell, HashSet<string> reads)
        {
            vars_ = vars;
            cell_ = cell;
            reads_ = reads;
        }

        public int Main(string name)
        {
            reads_.Add(name);
            int j = vars_.IndexOf(name);
            return j < 0 ? 0 : cell_ >> j & 1;
        }

        // the cells vary xi.settings.main only: a condition that read xi.settings.map would not be varied, so it fails here
        public int Map(string name) => throw new NotSupportedException($"P4b's cells vary xi.settings.main only, not map.{name}");
        public bool MapFlag(string name) => throw new NotSupportedException($"P4b's cells vary xi.settings.main only, not map.{name}");
    }

    private static string P4bStr(string s) => "[==[" + s + "]==]";

    // the globals module_utils.lua sets (and the module files' require of it), kept to be put back after the checks: the
    // matrix runs the module files over and over, which the world's own Lua modules (P11, from init.txt) must not see
    private const string P4bSave = @"
        rawset(_G, 'P4bSaved', { module = xi.module, pre = xi.pre, expansion = xi.expansion, settings = xi.settings,
                     Module = rawget(_G, 'Module'), Override = rawget(_G, 'Override'), applyOverride = rawget(_G, 'applyOverride'),
                     loaded = package.loaded['modules/module_utils'] })";

    private const string P4bRestore = @"
        local s = rawget(_G, 'P4bSaved')
        if s then
            xi.module, xi.pre, xi.expansion, xi.settings = s.module, s.pre, s.expansion, s.settings
            rawset(_G, 'Module', s.Module) rawset(_G, 'Override', s.Override) rawset(_G, 'applyOverride', s.applyOverride)
            package.loaded['modules/module_utils'] = s.loaded
        end
        rawset(_G, 'P4bSaved', nil)";

    // the Lua side of one module over the matrix: module_utils.lua run and the registries emptied (LuaModuleLoader), then
    // in each cell (cell i: vars[j] = bit j - 1 of i, every other xi.settings.main setting unset) the registries emptied
    // and the file run; xi.settings is a proxy for the run (its main the cell, any other table the real one, read noted)
    private static string P4bMatrix(string utils, string file, List<string> vars) => $@"
        local vars = {{ {string.Join(", ", vars.Select(v => "'" + v + "'"))} }}
        assert(loadfile({P4bStr(utils)}))()
        local chunk, loadErr = loadfile({P4bStr(file)})
        if not chunk then report('error:' .. tostring(loadErr)) return end
        local function clear()
            for i = #xi.module.registry, 1, -1 do xi.module.registry[i] = nil end
            for i = #xi.module.commandRegistry, 1, -1 do xi.module.commandRegistry[i] = nil end
        end
        local real = xi.settings
        local cell, reads, foreign = {{}}, {{}}, {{}}
        local main = setmetatable({{}}, {{
            __index = function(_, k) reads[k] = true return cell[k] end,
            __newindex = function(_, k) error('the module writes xi.settings.main.' .. tostring(k)) end }})
        xi.settings = setmetatable({{}}, {{
            __index = function(_, k) if k == 'main' then return main end foreign[k] = true return real and real[k] end,
            __newindex = function(_, k) error('the module writes xi.settings.' .. tostring(k)) end }})
        local out, names, firstErr = {{}}, {{}}, nil
        local ok, err = pcall(function()
            for i = 0, 2 ^ #vars - 1 do
                for j = 1, #vars do cell[vars[j]] = math.floor(i / 2 ^ (j - 1)) % 2 end
                clear()
                local ran, e = pcall(chunk)
                local reg = xi.module.registry
                if not ran then
                    out[#out + 1] = 'E'
                    firstErr = firstErr or tostring(e)
                elseif #reg ~= 1 then
                    out[#out + 1] = 'N'
                    for _, m in ipairs(reg) do names[tostring(m.name)] = true end
                else
                    names[tostring(reg[1].name)] = true
                    local en = reg[1].enabled
                    out[#out + 1] = en == true and '1' or en == false and '0' or 'B'
                end
            end
        end)
        xi.settings = real
        clear()
        local function keys(t) local r = {{}} for k in pairs(t) do r[#r + 1] = tostring(k) end table.sort(r) return table.concat(r, '\t') end
        report('cells:' .. table.concat(out))
        report('names:' .. keys(names))
        report('reads:' .. keys(reads))
        report('foreign:' .. keys(foreign))
        report('error:' .. tostring(ok and firstErr or err))";
}
