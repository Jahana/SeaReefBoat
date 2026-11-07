using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using XI.Script;

// XI.Verify.Lua <server root>: the LuaJIT bridge (C# functions, errors both ways, userdata identity, lua_init's
// tostring/fmt), then luautils::init's global script load over the real scripts/ tree.
internal static class Program
{
    private static int failures_;

    private static void Check(bool ok, string what)
    {
        if (!ok) failures_++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {what}");
    }

    // the csharp folder (settings/, scripts/, data/ ... copied from the C++ server), found above the test's bin folder
    private static string FindServerRoot()
    {
        for (var d = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory); d != null; d = d.Parent)
            if (System.IO.File.Exists(System.IO.Path.Combine(d.FullName, "settings", "default", "main.lua"))) return d.FullName;
        throw new System.IO.DirectoryNotFoundException("settings/default/main.lua not found above " + System.AppContext.BaseDirectory);
    }

    private static int Main(string[] args)
    {
        string root = args.Length > 0 ? args[0] : FindServerRoot();
        var errors = new List<string>();
        using var engine = new ScriptEngine(root, s => { }, s => errors.Add(s));
        var lua = engine.Lua;

        // ---- bridge ----
        lua.SetGlobal("csAdd", l => { l.Push(l.ToNumberArg(1) + l.ToNumberArg(2)); return 1; });
        lua.SetGlobal("csThrow", l => throw new InvalidOperationException("boom"));
        lua.SetGlobal("csCallback", l =>
        {
            l.PushValue(1);
            l.Push(41.0);
            l.Call(1, 1);
            return 1;
        });
        lua.DoString("return csAdd(2, 3)", "=t", 1);
        Check(lua.ToNumber(-1) == 5, "C# function returns a value");
        lua.Pop(1);
        lua.DoString("local ok, err = pcall(csThrow) return ok, err", "=t", 2);
        Check(!lua.ToBoolean(-2) && lua.ToStringValue(-1).Contains("boom"), "C# exception becomes a catchable Lua error: " + lua.ToStringValue(-1).Split('\n')[0]);
        lua.Pop(2);
        lua.DoString("local ok, err = pcall(csAdd, 'x') return ok, err", "=t", 2);
        Check(!lua.ToBoolean(-2) && lua.ToStringValue(-1).Contains("bad argument #1"), "argument type error: " + lua.ToStringValue(-1));
        lua.Pop(2);
        lua.DoString("return csCallback(function(x) return x + 1 end)", "=t", 1);
        Check(lua.ToNumber(-1) == 42, "Lua -> C# -> Lua re-entry");
        lua.Pop(1);
        lua.DoString("local ok, err = pcall(csCallback, function() error('inner') end) return ok, err", "=t", 2);
        Check(!lua.ToBoolean(-2) && lua.ToStringValue(-1).Contains("inner"), "Lua error inside a C# callback propagates");
        lua.Pop(2);
        // A Lua error raised inside a Lua C API call that a C# function makes (here lua_gettable running an __index
        // that errors) is thrown by LuaJIT's unwinder through the C# frames. It arrives as an SEHException; the C#
        // finally blocks run, the trampoline's catch turns it into a Lua error for the Lua pcall, and the VM keeps
        // working (the stack is balanced, a later call and a full GC run).
        int finallies = 0;
        lua.SetGlobal("csIndex", l =>
        {
            try
            {
                l.PushValue(2);
                l.GetTable(1);
                return 1;
            }
            finally
            {
                finallies++;
            }
        });
        int topBefore = lua.Top;
        lua.DoString("local t = setmetatable({}, { __index = function(_, k) error('meta ' .. k) end }) " +
                     "local bad = 0 for i = 1, 3 do if not pcall(csIndex, t, 'x') then bad = bad + 1 end end " +
                     "local ok, v = pcall(csIndex, { x = 7 }, 'x') collectgarbage() return bad, ok, v", "=t", 3);
        Check(lua.ToNumber(-3) == 3 && lua.ToBoolean(-2) && lua.ToNumber(-1) == 7 && finallies == 4,
              "Lua error unwinding through a C# function's Lua C API call is caught, the C# finally runs and the VM " +
              $"goes on ({lua.ToNumber(-3)} of 3 caught, {finallies} of 4 finally blocks)");
        lua.Pop(3);
        Check(lua.Top == topBefore, "the Lua stack is balanced after those errors");

        // LuaJIT #827 (fixed upstream by 1cdff194, 2022-04; the libluajit_64.dll of 2022-03-05 has it): a hot
        // pairs() loop compiled to ITERN that is later despecialized to ITERC (blacklisted, or run with an iterator
        // that is not next) still runs traces compiled from the ITERN form, and they misread the loop's control slot.
        // These are the two reduced cases from the LuaJIT issue: the first ends the outer traversal after one key
        // (after about 40,000 rounds on the old DLL), the second raises "bad argument #2 to '(for generator)'".
        lua.DoString(@"
            jit.flush()
            local t = { 1, 2, 3, 4, 5 }
            local tab = { a = t, b = t, c = t, d = t, e = t }
            local function f(...) for j = 1, 100 do end end
            for i = 1, 100000 do
                f()
                local c = 0
                for k, v in pairs(tab) do
                    for _ in pairs(v) do end
                    c = c + 1
                end
                if c ~= 5 then return string.format('round %d walked %d of 5 keys', i, c) end
            end
            return 'ok'", "=iter827a", 1);
        string case1 = lua.ToStringValue(-1);
        lua.Pop(1);
        Check(case1 == "ok", "a hot pairs() loop that is blacklisted walks every key (LuaJIT #827 case 1): " + case1);
        lua.DoString(@"
            jit.flush()
            local tab = {}
            for i = 1, 100 do tab[i] = i end
            -- the parameter must be named pairs: the parser compiles only a call named pairs (or next, t) to ITERN
            local function f(pairs)
                local n = 0
                for _ in pairs(tab) do
                    for j = 1, 100 do end
                    n = n + 1
                end
                return n
            end
            local first = f(pairs)
            local ok, n = pcall(f, ipairs)
            if not ok then return tostring(n) end
            if first == 100 and n == 100 then return 'ok' end
            return string.format('%d and %d of 100 keys', first, n)", "=iter827b", 1);
        string case2 = lua.ToStringValue(-1);
        lua.Pop(1);
        Check(case2 == "ok", "a compiled pairs() loop run again with ipairs walks every key (LuaJIT #827 case 2): " +
              case2);
        // This LuaJIT (since 7152e154) rounds an exact decimal tie to even when it formats a number with a limited
        // precision, as C's printf does; the C++ server's 2022 build rounds it away from zero (-348.13, ...563): a
        // deliberate difference (user ruling 2026-11-05). It reaches .. (%.14g), string.format's %f / %g / %e and %s
        // of a number, table.concat and lua_tolstring of a number from C#; tostring, print and fmt are C# here
        // (LuaFmt.FormatG). A player could see it only in the GM text of commands/pos.lua (%.4f of a coordinate that
        // is an odd multiple of 1/32).
        Check(Eval(lua, "string.format('%.2f %.0f %.17g', -348.125, 2.5, 1.73246002197265625)") ==
              "-348.12 2 1.7324600219726562", "string.format rounds an exact tie to even");
        lua.DoString("local co = coroutine.create(function(a) local b = coroutine.yield(csAdd(a, 1)) return csAdd(b, 10) end) " +
                     "local _, x = coroutine.resume(co, 1) local _, y = coroutine.resume(co, 5) return x, y", "=t", 2);
        Check(lua.ToNumber(-2) == 2 && lua.ToNumber(-1) == 15, "C# functions called from a coroutine");
        lua.Pop(2);

        lua.RegisterType("TestEntity", new Dictionary<string, LuaFunction>
        {
            ["getName"] = l => { l.PushString(((TestEntity)l.ToObject(1)).Name); return 1; },
        });
        lua.TypeNameOf = o => o is TestEntity ? "TestEntity" : o.GetType().Name;
        var ent = new TestEntity { Name = "Kohlo-Lakolo" };
        lua.SetGlobal("csEntity", l => { l.PushObject(ent); return 1; });
        lua.DoString("local a, b = csEntity(), csEntity() return a:getName(), a == b, pcall(function() return a:getFameLevel(1) end)", "=t", 4);
        Check(lua.ToStringValue(-4) == "Kohlo-Lakolo", "usertype method");
        Check(lua.ToBoolean(-3), "same object -> same userdata");
        Check(!lua.ToBoolean(-2) && lua.ToStringValue(-1).Contains("binding not ported: TestEntity:getFameLevel"), "unported method raises");
        Check(new List<string>(lua.MissingBindings).Contains("TestEntity:getFameLevel"), "unported method is recorded");
        lua.Pop(4);

        // ---- lua_init ----
        engine.LuaInit();
        Check(Eval(lua, "tostring(-1)") == "4294967295", "tostring prints integers as uint32");
        Check(Eval(lua, "tostring(1.5, 'a', nil, true)") == "1.5 a nil true", "tostring joins its arguments");
        Check(Eval(lua, "tostring({})") == "\n{}", "tostring of an empty table");
        Check(Eval(lua, "tostring({ 'x' })") == "\n{\n    \"x\"\n}", "tostring of a list");
        Check(Eval(lua, "fmt('{:02}:{:02}', 5, 12)") == "05:12", "fmt {:02}");
        Check(Eval(lua, "fmt('{} {} {}', 'a', 1.5, 3)") == "a 1.5 3", "fmt {}");
        Check(Eval(lua, "type(bit.band)") == "function", "bit is loaded");

        // ---- settings + luautils::init ----
        engine.SettingsInit();
        Check(Eval(lua, "type(xi.settings.main)") == "table", "xi.settings.main loaded");
        Check(Eval(lua, "tostring(xi.settings.network.MAP_PORT)") == "54230", "settings network.MAP_PORT");
        engine.LuautilsInit();
        Check(Eval(lua, "tostring(math.random(5, 5))") == "5", "math.random(lower, upper)");
        // the map host binds the DB-backed globals; no DB here
        lua.SetGlobal("GetServerVariable", l => { l.Push(0.0); return 1; });

        var sw = System.Diagnostics.Stopwatch.StartNew();
        engine.LoadGlobalScripts(Zones(root));
        sw.Stop();
        Console.WriteLine($"loaded {engine.ScriptsLoaded} global scripts in {sw.ElapsedMilliseconds} ms, {engine.LoadErrors} load errors, {lua.MemoryKb} KB");
        foreach (var e in errors) Console.WriteLine("  error: " + e.Split('\n')[0]);

        Check(Eval(lua, "tostring(xi.zone.PORT_WINDURST)") == "240", "xi.zone enum");
        Check(Eval(lua, "type(package.loaded['scripts/zones/Port_Windurst/IDs'])") == "table", "Port_Windurst IDs published");
        Check(Eval(lua, "tostring(zones[xi.zone.PORT_WINDURST].npc ~= nil)") == "true", "Port_Windurst IDs has npc ids");
        Check(Eval(lua, "type(InteractionGlobal)") == "table", "InteractionGlobal loaded");
        Check(engine.LoadErrors == 0, "no global script load errors");

        // InteractionGlobal.initZones + one zone's NPC objects (luautils::OnEntityLoad)
        sw.Restart();
        Check(engine.InitInteractionGlobal(Zones(root)), "InteractionGlobal.initZones");
        Console.WriteLine($"interaction containers loaded in {sw.ElapsedMilliseconds} ms, {lua.MemoryKb} KB");
        // every scripts/{battlefields,missions,quests}/<dir>/<file>.lua but helpers.lua (the count falls as quests move to C#)
        int containerFiles = new[] { "battlefields", "missions", "quests" }
            .SelectMany(sub => Directory.GetDirectories(Path.Combine(root, "scripts", sub)))
            .SelectMany(dir => Directory.GetFiles(dir, "*.lua"))
            .Count(f => !Path.GetFileName(f).Equals("helpers.lua", StringComparison.Ordinal));
        string listed = Eval(lua, "#GetContainerFilenamesList()");
        Check(listed == containerFiles.ToString(), $"GetContainerFilenamesList: {listed} containers (files on disk {containerFiles})");
        int before = errors.Count;
        int npcs = 0;
        foreach (var file in Directory.GetFiles(Path.Combine(root, "scripts", "zones", "Port_Windurst", "npcs"), "*.lua"))
        {
            engine.LoadLuaObjectFromFile($"./scripts/zones/Port_Windurst/npcs/{Path.GetFileName(file)}");
            npcs++;
        }
        Check(errors.Count == before, $"{npcs} Port_Windurst NPC scripts load");
        engine.PushFileFunction("./scripts/zones/Port_Windurst/npcs/HomePoint#2.lua", "onTrigger");
        Check(lua.IsFunction(-1), "xi.zones.Port_Windurst.npcs['HomePoint#2'].onTrigger resolved");
        lua.Pop(1);
        engine.PushFileFunction("./scripts/zones/Port_Windurst/npcs/Nobody.lua", "onTrigger");
        Check(lua.IsNil(-1), "missing NPC script -> nil function");
        lua.Pop(1);
        foreach (var e in errors) Console.WriteLine("  error: " + e.Split('\n')[0]);

        // LuaJIT #827 in the framework's own code (the libluajit_64.dll of 2022-03-05 predates its fix 1cdff194): once
        // a loop compiled to ITERN (pairs(t), or for ... in next, t) is despecialized to ITERC (blacklisted after its
        // traces abort, or run with an iterator that is not next), the traces compiled while it was ITERN still run it,
        // and they read the key the interpreter left in the loop's control slot as an index: the traversal ends early
        // or starts over. That dropped Michea's handlers on !reloadquest Father_Figure. InteractionLookup builds and
        // clears its lookup with while loops over next(), so this can't drop, keep or duplicate a handler. Here every
        // pairs() loop addDefaultHandlers and addContainer run is despecialized on purpose (one round through a
        // pairs() whose iterator is not next), with the traces of the warm-up rounds still in place; then the same
        // lookup gets the zone's defaults and the container again, each time removing what it had first (as
        // !reloadquest removes, then adds), and must hold exactly one set of each.
        string iternCheck;
        try
        {
            lua.DoString(@"
                local zone = 235
                local function make(nkeys)
                    local q = Quest:new(1, 9000 + nkeys)
                    local z1, z2 = { onEventFinish = { [240] = function() end } }, { onEventFinish = { [241] = function() end } }
                    local defaults = {}
                    for k = 1, nkeys do
                        z1['Npc' .. k] = q:progressEvent(240 + k)
                        z2['Npc' .. k] = { onTrade = function() end }
                        defaults['Npc' .. k] = { event = 500 + k }
                    end
                    q.sections = { { check = function() return true end, [zone] = z1 }, { check = function() return true end, [zone] = z2 } }
                    return q, defaults, 2 * nkeys + 2, nkeys
                end
                -- the zone's defaults and the container again, into the same lookup; counts the entries of each (while
                -- loops over next(): no ITERN here)
                local function round(lookup, q, defaults)
                    lookup:addDefaultHandlers(zone, defaults)
                    lookup:addContainer(q)
                    local nq, nd = 0, 0
                    local second = lookup.data[zone]
                    local k2, third = next(second)
                    while k2 ~= nil do
                        local k3, list = next(third)
                        while k3 ~= nil do
                            for _, e in ipairs(list) do
                                if rawequal(e.container, q) then nq = nq + 1
                                elseif e.container == nil and e.check == nil then nd = nd + 1 end
                            end
                            k3, list = next(third, k3)
                        end
                        k2, third = next(second, k2)
                    end
                    return nq, nd
                end
                jit.off(round)
                local path = 'scripts/globals/interaction/interaction_lookup'
                local savedClass, savedModule, realPairs = InteractionLookup, package.loaded[path], pairs
                local bad = {}
                local ok, err = pcall(function()
                    for _, nkeys in ipairs({ 2, 3, 4, 6, 8, 12, 16 }) do
                        for _, warm in ipairs({ 30, 300, 3000 }) do
                            -- the lookup's functions fresh (no bytecode despecialized by an earlier round), no traces
                            jit.flush()
                            package.loaded[path] = nil
                            require(path)
                            local lookup = InteractionLookup:new()
                            local q, defaults, expect, expectDefaults = make(nkeys)
                            for i = 1, warm do round(lookup, q, defaults) end
                            pairs = function(t) return function(s, k) return next(s, k) end, t, nil end
                            round(lookup, q, defaults)
                            pairs = realPairs
                            for i = 1, 200 do
                                local nq, nd = round(lookup, q, defaults)
                                if nq ~= expect or nd ~= expectDefaults then
                                    bad[#bad + 1] = string.format('%d keys, %d warm-up rounds: %d of %d handlers, %d of %d defaults',
                                                                  nkeys, warm, nq, expect, nd, expectDefaults)
                                    break
                                end
                            end
                        end
                    end
                end)
                pairs = realPairs
                InteractionLookup, package.loaded[path] = savedClass, savedModule
                jit.flush()
                if not ok then error(err, 0) end
                return #bad == 0 and 'ok' or table.concat(bad, '; ')", "=iterncheck", 1);
            iternCheck = lua.ToStringValue(-1);
            lua.Pop(1);
        }
        catch (LuaException e)
        {
            iternCheck = "error: " + e.Message.Split('\n')[0];
        }
        Check(iternCheck == "ok", "InteractionLookup re-adds defaults and a container after its pairs() loops are despecialized (LuaJIT #827): " + iternCheck);

        Console.WriteLine($"missing bindings reached during load: {lua.MissingBindings.Count}");
        foreach (var m in lua.MissingBindings) Console.WriteLine("  " + m);
        Console.WriteLine(failures_ == 0 ? "ALL PASS" : $"{failures_} FAILED");
        return failures_ == 0 ? 0 : 1;
    }

    private static string Eval(LuaState lua, string expr)
    {
        try
        {
            lua.DoString("return " + expr, "=eval", 1);
            string s = lua.Describe(-1);
            lua.Pop(1);
            return s;
        }
        catch (LuaException e)
        {
            return "error: " + e.Message;
        }
    }

    // zone ids from scripts/enum/zone.lua (the map host takes them from zone_settings)
    private static List<ScriptZone> Zones(string root)
    {
        var ids = new Dictionary<string, int>();
        foreach (Match m in Regex.Matches(File.ReadAllText(Path.Combine(root, "scripts", "enum", "zone.codegen.lua")), @"^\s*(\w+)\s*=\s*(\d+)", RegexOptions.Multiline))
            ids[m.Groups[1].Value] = int.Parse(m.Groups[2].Value);
        var zones = new List<ScriptZone>();
        foreach (var dir in Directory.GetDirectories(Path.Combine(root, "scripts", "zones")))
        {
            string name = Path.GetFileName(dir);
            string folder = FolderName(name);
            if (!ids.TryGetValue(folder.ToUpperInvariant(), out int id)) continue;
            zones.Add(new ScriptZone { Id = id, Name = name, DataFolders = { folder } });
        }
        return zones;
    }

    private static string FolderName(string name)
    {
        var sb = new StringBuilder();
        foreach (char ch in name.ToLowerInvariant())
        {
            if (ch == '\'') continue;
            if (char.IsAsciiLetterOrDigit(ch)) sb.Append(ch);
            else if (sb.Length > 0 && sb[^1] != '_') sb.Append('_');
        }
        return sb.ToString().TrimEnd('_');
    }

    private sealed class TestEntity
    {
        public string Name;
    }
}
