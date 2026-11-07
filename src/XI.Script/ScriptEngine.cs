using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using XI.Common;
using static XI.Script.LuaNative;

namespace XI.Script
{
    // A zone as PopulateIDLookups sees it: its zone_settings name (scripts/zones/<Name>/IDs.lua), the data/zones folders
    // of itself and its instance overlays (their npcs.yaml / mobs.yaml feed GetFirstID)
    public sealed class ScriptZone
    {
        public int Id;
        public string Name;
        public List<string> DataFolders = new List<string>();
    }

    // The map server's Lua state: lua_init (src/common/lua.cpp), settings::init and luautils::init. Scripts run with the
    // server root as the working directory, as under xi_map ("./scripts/..." requires and chunk names).
    public sealed partial class ScriptEngine : IDisposable
    {
        public readonly LuaState Lua;
        public readonly string Root;
        // the scripts folder the engine runs (<root>/scripts unless given): its parent is the working directory, so every
        // "./scripts/..." path (the engine's, the zone host's, a require, an io.open) reads it; settings/ and data/ stay
        // at the root (a test records a quest from another commit's Lua through a scripts folder of its own)
        public readonly string ScriptsDir;
        private readonly Action<string> log_, error_;
        private Random rng_ = new Random();

        // math.random's source from here on seeded: the verify suites that compare two runs draw the same numbers in each
        internal void SeedRandom(int seed) => rng_ = new Random(seed);

        // safe_script_file failures while loading (each one is logged as it happens)
        public int LoadErrors;
        public int ScriptsLoaded;

        // the tables xi.roe hands to RoeParseRecords / RoeParseTimed
        public LuaRef RoeRecords, RoeTimedSchedule;
        public Action<LuaState> OnRoeParseRecords, OnRoeParseTimed; // the table is argument 1

        // every luautils global, each a stub (calling one raises "binding not ported") until the host binds it
        public static readonly string[] LuautilsGlobals =
        {
            "GarbageCollectStep", "GarbageCollectFull", "GetZone", "GetItemByID", "GetItemFlagsByID",
            "GetItemLevelRequirementsByID", "GetNPCByID", "GetMobByID", "GetEntityByID", "WeekUpdateConquest",
            "GetRegionOwner", "GetRegionInfluence", "GetNationRank", "GetConquestBalance", "IsConquestAlliance",
            "SpawnMob", "DespawnMob", "GetPlayerByName", "GetPlayerByID", "PlayerHasValidSession", "GetPlayerIDByName",
            "SendToJailOffline", "DrawIn", "GetSystemTime", "LoadLinkshellConciergeSlots", "SetLinkshellConciergeSlot",
            "DeleteLinkshellConciergeSlot", "DecrementLinkshellConciergeMembersGoal", "LoadChocoboRaceResult",
            "SaveChocoboRaceResult", "GetChocoboRaceQuills", "AddChocoboRaceQuills", "JstMidnight", "JstDayOfTheYear",
            "JstDayOfTheMonth", "JstDayOfTheWeek", "JstYear", "JstMonth", "JstHour", "NextConquestTally",
            "NextGameTime", "NextJstDay", "NextJstWeek", "VanadielTime", "VanadielTOTD", "VanadielHour",
            "VanadielMinute", "VanadielDayOfTheWeek", "VanadielDayOfTheMonth", "VanadielDayOfTheYear", "VanadielYear",
            "VanadielMonth", "VanadielUniqueDay", "VanadielDayElement", "VanadielMoonPhase", "VanadielMoonDirection",
            "VanadielRSERace", "VanadielRSELocation", "SetTimeOffset", "RunElevator", "GetElevatorState",
            "GetServerVariable", "SetServerVariable", "GetVolatileServerVariable", "SetVolatileServerVariable",
            "GetCharVar", "SetCharVar", "ClearCharVarFromAll", "SendEntityVisualPacket", "GetMobRespawnTime",
            "DisallowRespawn", "GetRecentFishers", "NearLocation", "GetFurthestValidPosition", "Terminate",
            "GetReadOnlyItem", "GetAbility", "GetSpell", "SelectDailyItem", "GetContainerFilenamesList",
            "GetCachedInstanceScript", "GetItemIDByName", "SendItemToDeliveryBox", "SendLuaFuncStringToZone",
            "RoeParseRecords", "RoeParseTimed", "GetSynergyRecipeByID", "GetSynergyRecipeByTrade",
            "ReloadSynthRecipes", "LoadExpDifficultyCurves", "ReloadExperienceData", "GetFishingContest",
            "InitNewFishingContest", "SetContestParameters", "ProgressFishingContest",
            "InitializeFishingContestSystem", "ForceCrash", "BuildString",
        };

        // the SOL_USERTYPE names (src/map/lua/lua_*.cpp)
        public static readonly string[] UserTypes =
        {
            "CAbility", "CAction", "CAttack", "CBaseEntity", "CBattlefield", "CInstance", "CItem", "CLootContainer",
            "CMobSkill", "CPetSkill", "CSpell", "CStatusEffect", "CTradeContainer", "CTrait", "CTreasurePool",
            "ITriggerArea", "CWeaponSkill", "CZone",
        };

        public ScriptEngine(string root, Action<string> log, Action<string> error, string scriptsDir = null)
        {
            Root = Path.GetFullPath(root);
            ScriptsDir = Path.GetFullPath(Path.TrimEndingDirectorySeparator(scriptsDir ?? Path.Combine(Root, "scripts")));
            if (Path.GetFileName(ScriptsDir) != "scripts")
                throw new ArgumentException($"a scripts folder is named scripts: {ScriptsDir}", nameof(scriptsDir));
            log_ = log;
            error_ = error;
            LuaNative.LibraryDirectory = Root;
            Directory.SetCurrentDirectory(Path.GetDirectoryName(ScriptsDir));
            Lua = new LuaState { Log = log };
        }

        // the scripts folder is not the root's: the working directory is not the root
        private bool OwnScriptsDir => !string.Equals(Path.TrimEndingDirectorySeparator(ScriptsDir),
            Path.TrimEndingDirectorySeparator(Path.Combine(Root, "scripts")), StringComparison.OrdinalIgnoreCase);

        public void Dispose() => Lua.Dispose();

        // ShowLua: where print() writes; XI.Host sends it to the lua logger (logging.LOG_LUA). Null: the log line
        // "[lua] <text>"
        public Action<string> LuaPrint;

        // ---- lua_init ------------------------------------------------------------------------------------------

        public void LuaInit()
        {
            // a require the working directory lacks (settings/...) from the root
            if (OwnScriptsDir)
                Lua.DoString("package.path = package.path .. ';' .. ...", "=lua_init", 0, () =>
                {
                    Lua.PushString(Path.Combine(Root, "?.lua").Replace('\\', '/'));
                    return 1;
                });
            Lua.DoString("if not bit then bit = require('bit') end", "=lua_init");
            Lua.DoString(
                "function __FILE__() return debug.getinfo(2, 'S').source end\n" +
                "function __LINE__() return debug.getinfo(2, 'l').currentline end\n" +
                "function __FUNC__() return debug.getinfo(2, 'n').name end\n", "=lua_init");

            Lua.SetGlobal("print", l =>
            {
                string text = ToStringArgs(l);
                if (LuaPrint != null) LuaPrint(text);
                else log_("[lua] " + text);
                return 0;
            });

            Lua.GetGlobal("tostring");
            Lua.SetField(LUA_GLOBALSINDEX, "_tostring");
            Lua.SetGlobal("tostring", l =>
            {
                l.PushString(ToStringArgs(l));
                return 1;
            });

            Lua.SetGlobal("fmt", l =>
            {
                l.PushString(LuaFmt.Format(l.ToStringArg(1), l, 2, (st, i) => ToStringDepth(st, i, 0)));
                return 1;
            });

            Lua.SetGlobal("sleep", l =>
            {
                double sec = l.ToNumberArg(1);
                log_($"Blocking main thread for {sec} seconds!");
                Thread.Sleep((int)(float)(sec * 1000));
                return 0;
            });
            // lldebugger is optional and not shipped
        }

        // lua_to_string: the arguments joined by spaces; an integral number is printed as a uint32
        private static string ToStringArgs(LuaState l)
        {
            int n = l.Top;
            var parts = new string[n];
            for (int i = 1; i <= n; i++)
            {
                if (l.IsNumber(i) && l.ToNumber(i) == Math.Floor(l.ToNumber(i)))
                    parts[i - 1] = ((uint)l.ToIntegral(i)).ToString(CultureInfo.InvariantCulture);
                else
                    parts[i - 1] = ToStringDepth(l, i, 0);
            }
            return string.Join(" ", parts);
        }

        // lua_to_string_depth
        internal static string ToStringDepth(LuaState l, int idx, int depth)
        {
            if (idx < 0 && idx > LUA_REGISTRYINDEX) idx = l.Top + idx + 1;
            switch (l.Type(idx))
            {
                case LUA_TNONE:
                case LUA_TNIL:
                    return "nil";
                case LUA_TSTRING:
                    return depth > 0 ? "\"" + l.ToStringValue(idx) + "\"" : l.ToStringValue(idx);
                case LUA_TNUMBER:
                    return LuaFmt.FormatG(l.ToNumber(idx));
                case LUA_TTHREAD:
                    return "thread";
                case LUA_TBOOLEAN:
                    return l.ToBoolean(idx) ? "true" : "false";
                case LUA_TFUNCTION:
                    return "function";
                case LUA_TUSERDATA:
                {
                    // the original tostring, saved as _tostring
                    l.GetGlobal("_tostring");
                    l.PushValue(idx);
                    l.Call(1, 1);
                    string s = l.ToStringValue(-1);
                    l.Pop(1);
                    return s;
                }
                case LUA_TLIGHTUSERDATA:
                    return "lightuserdata";
                case LUA_TTABLE:
                {
                    string indent = new string(' ', 4 * (depth + 1));
                    string unindent = indent.Substring(4);
                    var items = new List<string>();
                    l.PushNil();
                    while (l.Next(idx))
                    {
                        // key at -2, value at -1
                        if (l.Type(-2) == LUA_TSTRING)
                            items.Add($"{indent}{ToStringDepth(l, -2, 0)}: {ToStringDepth(l, -1, depth + 1)}");
                        else
                            items.Add($"{indent}{ToStringDepth(l, -1, depth + 1)}");
                        l.Pop(1);
                    }
                    return "\n" + unindent + "{" + (items.Count == 0 ? "" : "\n") + string.Join(",\n", items) +
                           (items.Count == 0 ? "" : "\n") + unindent + "}";
                }
                default:
                    return "UNKNOWN";
            }
        }

        // ---- settings::init ------------------------------------------------------------------------------------

        // settings/default/*.lua then settings/*.lua into xi.settings (each run, then require()d so a later require
        // is a no-op), fillMissingKeys from the defaults, then XI_<FILE>_<NAME> environment overrides
        public void SettingsInit()
        {
            Lua.DoString(@"
                local defaultFiles, userFiles = ...
                local function run(files)
                    for _, f in ipairs(files) do
                        local chunk, err = loadfile(f.file)
                        if not chunk then error('Error parsing settings file: ' .. f.file .. '\n' .. err) end
                        chunk()
                        package.loaded[f.key] = nil
                        require(f.key)
                    end
                end
                local function deepCopy(t)
                    local c = {}
                    for k, v in pairs(t) do c[k] = type(v) == 'table' and deepCopy(v) or v end
                    return c
                end
                local function isSequence(t)
                    for k in pairs(t) do if type(k) ~= 'number' then return false end end
                    return true
                end
                local function fillMissingKeys(defaults, target, depth)
                    for k, v in pairs(defaults) do
                        local isTable = type(v) == 'table'
                        if isTable or depth > 1 then
                            local existing = target[k]
                            if existing == nil then
                                target[k] = v
                            elseif isTable and type(existing) == 'table' and not isSequence(v) then
                                fillMissingKeys(v, existing, depth + 1)
                            end
                        end
                    end
                end
                local function scalar(v) local t = type(v) return t == 'boolean' or t == 'number' or t == 'string' end

                run(defaultFiles)
                local defaults = deepCopy(xi.settings)
                local map = {}
                for outer, t in pairs(xi.settings) do
                    for inner, v in pairs(t) do if scalar(v) then map[outer .. '.' .. inner] = v end end
                end
                run(userFiles)
                fillMissingKeys(defaults, xi.settings, 0)
                for outer, t in pairs(xi.settings) do
                    for inner, v in pairs(t) do
                        if scalar(v) then map[outer .. '.' .. inner] = v end
                        local env = os.getenv('XI_' .. string.upper(outer) .. '_' .. string.upper(inner))
                        if env then
                            env = env:match('^%s*(.-)%s*$')
                            map[outer .. '.' .. inner] = tonumber(env) or env
                        end
                    end
                end
                for key, v in pairs(map) do
                    local outer, inner = key:match('^([^.]*)%.(.*)$')
                    outer = string.lower(outer)
                    xi.settings[outer] = xi.settings[outer] or {}
                    xi.settings[outer][string.upper(inner)] = v
                end", "=settings::init", 0, () =>
            {
                PushFileList(Path.Combine("settings", "default"));
                if (XI.Common.Settings.DefaultsOnly) Lua.NewTable(); // no local overrides (Settings.DefaultsOnly)
                else PushFileList("settings");
                return 2;
            });
        }

        // { {file = './settings/x.lua', key = 'settings/x'}, ... } in path order: the root's (file a full path when the
        // working directory is not the root)
        private void PushFileList(string dir)
        {
            Lua.NewTable();
            int i = 1;
            foreach (var file in Directory.GetFiles(Path.Combine(Root, dir)).OrderBy(f => f, StringComparer.Ordinal))
            {
                if (Path.GetExtension(file) != ".lua") continue;
                string rel = Path.GetRelativePath(Root, file).Replace('\\', '/');
                Lua.NewTable();
                Lua.PushString(OwnScriptsDir ? file.Replace('\\', '/') : "./" + rel);
                Lua.SetField(-2, "file");
                Lua.PushString(rel.Substring(0, rel.Length - 4));
                Lua.SetField(-2, "key");
                Lua.RawSetI(-2, i++);
            }
        }

        // ---- luautils::init ------------------------------------------------------------------------------------

        public void LuautilsInit()
        {
            // math.random: sol::overload of (), (upper), (lower, upper)
            Lua.GetGlobal("math");
            int math = Lua.Top;
            Lua.PushFunction(l =>
            {
                int n = l.Top;
                if (n == 0) l.Push(rng_.NextDouble());
                else if (n == 1) l.Push((double)RandomInt(1, LuaState.LlRound(l.ToNumberArg(1)) + 1));
                else l.Push((double)RandomInt(LuaState.LlRound(l.ToNumberArg(1)), LuaState.LlRound(l.ToNumberArg(2)) + 1));
                return 1;
            });
            Lua.SetField(math, "random");
            Lua.PushFunction(l =>
            {
                l.Push((double)RandomInt(LuaState.LlRound(l.ToNumberArg(1)), LuaState.LlRound(l.ToNumberArg(2)) + 1));
                return 1;
            });
            Lua.SetField(math, "randomInt");
            Lua.PushFunction(l =>
            {
                double lo = l.ToNumberArg(1), hi = l.ToNumberArg(2);
                l.Push(lo >= hi ? lo : lo + rng_.NextDouble() * (hi - lo));
                return 1;
            });
            Lua.SetField(math, "randomFloat");
            Lua.PushFunction(l =>
            {
                double lower = l.OptNumber(3) ?? double.NegativeInfinity, upper = l.OptNumber(4) ?? double.PositiveInfinity;
                l.Push(NormalNumber(l.ToNumberArg(1), l.ToNumberArg(2), lower, upper));
                return 1;
            });
            Lua.SetField(math, "randomNormal");
            Lua.Pop(1);

            foreach (var name in LuautilsGlobals) SetStub(name);
            RegisterTimeGlobals();

            // scripts/{battlefields,missions,quests}/<dir>/<file>.lua (not helpers.lua) as require names, path order
            Lua.SetGlobal("GetContainerFilenamesList", l =>
            {
                var names = new List<string>();
                foreach (var sub in new[] { "scripts/battlefields", "scripts/missions", "scripts/quests" })
                {
                    foreach (var file in SortedRecursive(sub))
                    {
                        if (file.Count(c => c == '/') != 3 || !file.EndsWith(".lua", StringComparison.Ordinal) ||
                            file.EndsWith("/helpers.lua", StringComparison.Ordinal)) continue;
                        if (HiddenContainerFiles.Contains(file.Substring(0, file.Length - 4))) continue;
                        names.Add(file.Substring(0, file.Length - 4));
                    }
                }
                // test only: the extra files, each in its sorted place (empty in the shipped server)
                foreach (var extra in extraContainerFiles_.Keys)
                {
                    if (HiddenContainerFiles.Contains(extra)) continue;
                    int at = names.FindIndex(n => CompareContainerFiles(n, extra) > 0);
                    names.Insert(at < 0 ? names.Count : at, extra);
                }
                l.NewTable();
                for (int i = 0; i < names.Count; i++)
                {
                    l.PushString(names[i]);
                    l.RawSetI(-2, i + 1);
                }
                return 1;
            });

            // roeutils::ParseRecords / ParseTimedSchedule: the tables are kept, and the zone's RoE system parses them
            // at the call, as the C++
            Lua.SetGlobal("RoeParseRecords", l =>
            {
                l.PushValue(1);
                RoeRecords = l.Ref();
                OnRoeParseRecords?.Invoke(l);
                return 0;
            });
            Lua.SetGlobal("RoeParseTimed", l =>
            {
                l.PushValue(1);
                RoeTimedSchedule = l.Ref();
                OnRoeParseTimed?.Invoke(l);
                return 0;
            });

            foreach (var type in UserTypes)
            {
                Lua.RegisterType(type, new Dictionary<string, LuaFunction>());
            }
        }

        // a global that logs "binding not ported" once and raises when called
        public void SetStub(string name)
        {
            Lua.DoString(@"
                local name = ...
                _G[name] = function()
                    __xi_missing('_G', name)
                    error('binding not ported: ' .. name, 2)
                end", "=xi_stub", 0, () =>
            {
                Lua.PushString(name);
                return 1;
            });
        }

        // the Vana'diel clock the Vanadiel* globals read (EarthTime's, the time offset in it); injectable for the
        // harness
        public Func<long> VanaTicksNow = EarthTime.VanaTicks;

        // the wall clock (unix milliseconds) GetSystemTime, VanadielTime, NextGameTime and the Jst* globals read;
        // injectable for the harness
        public Func<long> UnixNowMs = EarthTime.UnixMs;

        private void RegisterTimeGlobals()
        {
            long VanaTicks() => VanaTicksNow();
            long Now() => UnixNowMs() / 1000;

            Lua.SetGlobal("GetSystemTime", l => { l.Push((double)(uint)Now()); return 1; });
            Lua.SetGlobal("VanadielTime", l => { l.Push((double)(uint)(Now() - VanaTime.EpochUnixSec)); return 1; });
            Lua.SetGlobal("VanadielTOTD", l => { l.Push((double)(byte)VanaTime.GetTotd(VanaTicks())); return 1; });
            Lua.SetGlobal("VanadielHour", l => { l.Push((double)VanaTime.GetHour(VanaTicks())); return 1; });
            Lua.SetGlobal("VanadielMinute", l => { l.Push((double)VanaTime.GetMinute(VanaTicks())); return 1; });
            Lua.SetGlobal("VanadielDayOfTheWeek", l => { l.Push((double)VanaTime.GetWeekDay(VanaTicks())); return 1; });
            Lua.SetGlobal("VanadielDayOfTheMonth", l => { l.Push((double)VanaTime.GetMonthDay(VanaTicks())); return 1; });
            Lua.SetGlobal("VanadielDayOfTheYear", l => { l.Push((double)VanaTime.GetYearDay(VanaTicks())); return 1; });
            Lua.SetGlobal("VanadielYear", l => { l.Push((double)(uint)VanaTime.GetYear(VanaTicks())); return 1; });
            Lua.SetGlobal("VanadielMonth", l => { l.Push((double)VanaTime.GetMonth(VanaTicks())); return 1; });
            Lua.SetGlobal("VanadielUniqueDay", l => { l.Push((double)VanaTime.CountDays(VanaTicks())); return 1; });
            Lua.SetGlobal("VanadielMoonPhase", l => { l.Push((double)VanaTime.MoonPhase(VanaTicks())); return 1; });
            Lua.SetGlobal("VanadielMoonDirection", l => { l.Push((double)VanaTime.MoonDirection(VanaTicks())); return 1; });
            Lua.SetGlobal("VanadielRSERace", l => { l.Push((double)VanaTime.RseRace(VanaTicks())); return 1; });
            Lua.SetGlobal("VanadielRSELocation", l => { l.Push((double)VanaTime.RseLocation(VanaTicks())); return 1; });
            // battleutils::GetDayElement: Firesday..Darksday -> ELEMENT fire 1, earth 4, water 6, wind 3, ice 2,
            // thunder 5, light 7, dark 8
            Lua.SetGlobal("VanadielDayElement", l =>
            {
                byte[] elements = { 1, 4, 6, 3, 2, 5, 7, 8 };
                l.Push((double)elements[VanaTime.GetWeekDay(VanaTicks()) % 8]);
                return 1;
            });
            Lua.SetGlobal("NextGameTime", l =>
            {
                uint interval = l.ToUIntArg(1);
                uint vana = (uint)(Now() - VanaTime.EpochUnixSec);
                l.Push((double)(uint)(vana - vana % interval + interval + VanaTime.EpochUnixSec));
                return 1;
            });
            RegisterJstGlobals();
        }

        // luautils::JstMidnight / JstDayOfTheYear / JstDayOfTheMonth / JstDayOfTheWeek / JstYear / JstMonth / JstHour /
        // NextJstWeek, with NextJstDay bound to JstMidnight and NextConquestTally to NextJstWeek (earth_time::jst: the
        // UTC clock plus 9 hours). Times are taken to the millisecond (the C++ clock is finer).
        private void RegisterJstGlobals()
        {
            const long jstOffsetMs = 9 * 3600 * 1000L, dayMs = 86400 * 1000L;
            long NowMs() => UnixNowMs();
            DateTime JstDate() => DateTimeOffset.FromUnixTimeMilliseconds(NowMs() + jstOffsetMs).UtcDateTime;
            // earth_time::jst::get_next_midnight: utc::get_next_midnight(tp + 9h) - 9h, a ceil to the day
            static long NextMidnightMs(long nowMs) => (long)Math.Ceiling((nowMs + jstOffsetMs) / (double)dayMs) * dayMs - jstOffsetMs;
            // earth_time::get_next_game_week: the next JST midnight plus the days to Monday (get_game_weekday: Monday 0)
            long NextGameWeekMs(long nowMs) => NextMidnightMs(nowMs) + (6 - ((int)JstDate().DayOfWeek + 6) % 7) * dayMs;

            Lua.SetGlobal("JstMidnight", l => { l.Push((double)(uint)(NextMidnightMs(NowMs()) / 1000)); return 1; });
            Lua.SetGlobal("NextJstDay", l => { l.Push((double)(uint)(NextMidnightMs(NowMs()) / 1000)); return 1; });
            // earth_time::jst::get_yearday floors to std::chrono::years, the 365.2425-day average year counted from the
            // Unix epoch, not to the calendar year; ported as written
            Lua.SetGlobal("JstDayOfTheYear", l =>
            {
                const long yearMs = 31556952 * 1000L;
                l.Push((double)(uint)((NowMs() + jstOffsetMs) % yearMs / dayMs));
                return 1;
            });
            Lua.SetGlobal("JstDayOfTheMonth", l => { l.Push((double)JstDate().Day); return 1; });
            Lua.SetGlobal("JstDayOfTheWeek", l => { l.Push((double)(int)JstDate().DayOfWeek); return 1; }); // Sunday 0
            Lua.SetGlobal("JstYear", l => { l.Push((double)JstDate().Year); return 1; });
            Lua.SetGlobal("JstMonth", l => { l.Push((double)JstDate().Month); return 1; }); // 1..12
            Lua.SetGlobal("JstHour", l => { l.Push((double)JstDate().Hour); return 1; });
            Lua.SetGlobal("NextJstWeek", l => { l.Push((double)(uint)(NextGameWeekMs(NowMs()) / 1000)); return 1; });
            Lua.SetGlobal("NextConquestTally", l => { l.Push((double)(uint)(NextGameWeekMs(NowMs()) / 1000)); return 1; });
        }

        // xirand::GetRandomNumber<int64>(min, max): [min, max)
        private long RandomInt(long min, long max) => max <= min ? min : rng_.NextInt64(min, max);

        // xirand::GetNormalNumber(mean, stddev, lower, upper): a truncated normal by inverse CDF
        private double NormalNumber(double mean, double stddev, double lower, double upper)
        {
            if (lower >= upper) return lower;
            if (!(stddev > 0.0)) return Math.Clamp(mean, lower, upper);
            double pLo = NormalCdf((lower - mean) / stddev), pHi = NormalCdf((upper - mean) / stddev);
            double p = Math.Clamp(pLo + rng_.NextDouble() * (pHi - pLo), Math.Pow(2, -53), Math.BitDecrement(1.0));
            return Math.Clamp(mean + stddev * InverseNormalCdf(p), lower, upper);
        }

        private static double NormalCdf(double z) => double.IsNegativeInfinity(z) ? 0 : double.IsPositiveInfinity(z) ? 1 : 0.5 * Erfc(-z / Math.Sqrt(2));

        // W. J. Cody's rational approximation, as in Numerical Recipes erfc
        private static double Erfc(double x)
        {
            double z = Math.Abs(x), t = 1.0 / (1.0 + 0.5 * z);
            double r = t * Math.Exp(-z * z - 1.26551223 + t * (1.00002368 + t * (0.37409196 + t * (0.09678418 +
                       t * (-0.18628806 + t * (0.27886807 + t * (-1.13520398 + t * (1.48851587 +
                       t * (-0.82215223 + t * 0.17087277)))))))));
            return x >= 0 ? r : 2.0 - r;
        }

        // Acklam's inverse normal CDF
        private static double InverseNormalCdf(double p)
        {
            double[] a = { -3.969683028665376e+01, 2.209460984245205e+02, -2.759285104469687e+02, 1.383577518672690e+02, -3.066479806614716e+01, 2.506628277459239e+00 };
            double[] b = { -5.447609879822406e+01, 1.615858368580409e+02, -1.556989798598866e+02, 6.680131188771972e+01, -1.328068155288572e+01 };
            double[] c = { -7.784894002430293e-03, -3.223964580411365e-01, -2.400758277161838e+00, -2.549732539343734e+00, 4.374664141464968e+00, 2.938163982698783e+00 };
            double[] d = { 7.784695709041462e-03, 3.224671290700398e-01, 2.445134137142996e+00, 3.754408661907416e+00 };
            const double low = 0.02425, high = 1 - low;
            double q, r;
            if (p < low)
            {
                q = Math.Sqrt(-2 * Math.Log(p));
                return (((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) / ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
            }
            if (p > high)
            {
                q = Math.Sqrt(-2 * Math.Log(1 - p));
                return -(((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) / ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
            }
            q = p - 0.5;
            r = q * q;
            return (((((a[0] * r + a[1]) * r + a[2]) * r + a[3]) * r + a[4]) * r + a[5]) * q / (((((b[0] * r + b[1]) * r + b[2]) * r + b[3]) * r + b[4]) * r + 1);
        }

        // ---- global scripts ------------------------------------------------------------------------------------

        // the Lua module loader (moduleutils.cpp; platform round P11), made with the engine
        public LuaModules Modules { get; private set; }
        // the scripts/globals files' path parts in load order (luautils.cpp:500-520), for TryApplyLuaModules after
        // the load
        private readonly List<List<string>> globalsParts_ = new List<List<string>>();

        // luautils::init's load order: enum, utils, data, combat/basic, combat (each file once), the zone IDs, then
        // scripts/globals recursively, then scripts/commands (each an xi.commands.<name> object). The modules come
        // after (LoadLuaModules, luautils.cpp:569-576), which the zone host calls next.
        public void LoadGlobalScripts(IEnumerable<ScriptZone> zones)
        {
            var loaded = new HashSet<string>(StringComparer.Ordinal);
            foreach (var dir in new[] { "./scripts/enum", "./scripts/utils", "./scripts/data", "./scripts/combat/basic", "./scripts/combat" })
            {
                foreach (var file in SortedRecursive(dir))
                {
                    if (!file.EndsWith(".lua", StringComparison.Ordinal) || !loaded.Add(file)) continue;
                    SafeScriptFile(file);
                }
            }

            scriptZones_ = zones.ToList();
            PopulateIDLookups(scriptZones_);

            foreach (var file in SortedRecursive("./scripts/globals"))
            {
                if (!file.EndsWith(".lua", StringComparison.Ordinal)) continue;
                SafeScriptFile(file);
                // collected whether or not the file loaded, as the C++ does
                globalsParts_.Add(LuaModules.ScriptParts(file));
            }

            // sorted_directory_iterator<directory_iterator>: the folder itself, not below it
            foreach (var file in SortedRecursive("./scripts/commands"))
            {
                if (file.EndsWith(".lua", StringComparison.Ordinal) && file.LastIndexOf('/') == "./scripts/commands".Length)
                    LoadLuaObjectFromFile(file);
            }
        }

        // luautils.cpp:569-576: moduleutils::LoadLuaModules, TryApplyLuaModules for each scripts/globals file in load
        // order, then TryApplyRemainingLuaModules. lines: init.txt's; zonePorts: zone_settings name -> zoneport;
        // currentPort: the map process's port, else network.MAP_PORT (a second call registers the new lines' modules
        // after the first's, as one load of both would not: the golden harness loads a flow's modules once per world)
        public void LoadLuaModules(IEnumerable<string> lines, IReadOnlyDictionary<string, ushort> zonePorts, ushort currentPort)
        {
            Modules ??= new LuaModules(Lua, Root, log_, error_);
            Modules.Load(lines, zonePorts, currentPort);
            foreach (var parts in globalsParts_) Modules.TryApply(parts);
            Modules.TryApplyRemaining();
        }

        // lua.safe_script_file: errors are logged, not thrown
        public bool SafeScriptFile(string relativePath)
        {
            try
            {
                Lua.DoFile(relativePath, "@" + relativePath);
                ScriptsLoaded++;
                return true;
            }
            catch (LuaException e)
            {
                LoadErrors++;
                error_(e.Message);
                return false;
            }
        }

        // sorted_directory_iterator<recursive_directory_iterator>: every entry below dir, as "./a/b.lua", in
        // std::filesystem::path order (element-wise, so "a/b" sorts before "a-b")
        private static List<string> SortedRecursive(string dir)
        {
            var list = new List<string>();
            if (!Directory.Exists(dir)) return list;
            foreach (var f in Directory.EnumerateFileSystemEntries(dir, "*", SearchOption.AllDirectories))
                list.Add(f.Replace('\\', '/'));
            list.Sort(PathCompare);
            return list;
        }

        // the order GetContainerFilenamesList lists require names in (SortedRecursive's, on the files with their .lua)
        public static int CompareContainerFiles(string a, string b) => PathCompare(a + ".lua", b + ".lua");

        // test only: require names GetContainerFilenamesList skips, a stand-in for a deleted Lua file
        internal HashSet<string> HiddenContainerFiles { get; set; } = new HashSet<string>(StringComparer.Ordinal);

        // test only: Lua container files kept outside scripts/, listed as if they were there (QuestScripts.Live's
        // fixture quests): require name (scripts/quests/<dir>/<file>) -> the file, relative to Root or absolute.
        // GetContainerFilenamesList lists each name in its sorted place (HiddenContainerFiles still hides it), and
        // require loads it through package.preload, reading the file again at each load (a reload's
        // package.loaded[name] = nil)
        private IReadOnlyDictionary<string, string> extraContainerFiles_ = new Dictionary<string, string>();
        internal IReadOnlyDictionary<string, string> ExtraContainerFiles
        {
            get => extraContainerFiles_;
            set
            {
                extraContainerFiles_ = value ?? new Dictionary<string, string>();
                foreach (var (name, file) in extraContainerFiles_)
                {
                    string full = Path.GetFullPath(Path.Combine(Root, file)).Replace('\\', '/');
                    if (!File.Exists(full)) throw new FileNotFoundException($"extra container file {name}: {full}", full);
                    Lua.DoString("local name, file = ... package.preload[name] = function() return dofile(file) end",
                                 "=extra_container", 0, () =>
                                 {
                                     Lua.PushString(name);
                                     Lua.PushString(full);
                                     return 2;
                                 });
                }
            }
        }

        internal static int PathCompare(string a, string b)
        {
            var pa = a.Split('/');
            var pb = b.Split('/');
            for (int i = 0; i < Math.Min(pa.Length, pb.Length); i++)
            {
                int c = string.CompareOrdinal(pa[i], pb[i]);
                if (c != 0) return c;
            }
            return pa.Length.CompareTo(pb.Length);
        }

        // PopulateIDLookupsByFilename: for each scripts/zones/<zone>/IDs.lua, GetFirstID / GetTableOfIDs answer from
        // that zone's (and its overlays') mob spawn and npc scripts while the file runs; the result zones[zoneId] is
        // published as package.loaded["scripts/zones/<zone>/IDs"]. onlyZone: that zone folder's alone
        // (PopulateIDLookupsByFilename of one name, the file watcher's reload of its IDs.lua)
        private void PopulateIDLookups(IEnumerable<ScriptZone> zones, string onlyZone = null)
        {
            var byName = zones.ToDictionary(z => z.Name, StringComparer.Ordinal);
            Dictionary<string, List<uint>> lookup = null;
            string zoneName = null;
            var tables = new Dictionary<string, LuaRef>();

            Lua.SetGlobal("GetFirstID", l =>
            {
                string name = l.ToStringArg(1);
                if (lookup != null && lookup.TryGetValue(name, out var ids))
                {
                    l.Push((double)ids[0]);
                    return 1;
                }
                error_($"GetFirstID({name}) in zone {zoneName}: Returning nil");
                l.PushNil();
                return 1;
            });
            Lua.SetGlobal("GetTableOfIDs", l =>
            {
                string name = l.ToStringArg(1);
                if (tables.TryGetValue(name, out var cached))
                {
                    l.PushRef(cached);
                    return 1;
                }
                if (lookup == null || !lookup.TryGetValue(name, out var ids))
                {
                    error_($"GetTableOfIDs({name}) in zone {zoneName}: Returning nil");
                    l.PushNil();
                    return 1;
                }
                l.NewTable(ids.Count);
                for (int i = 0; i < ids.Count; i++)
                {
                    l.Push((double)ids[i]);
                    l.RawSetI(-2, i + 1);
                }
                l.PushValue(-1);
                tables[name] = l.Ref();
                return 1;
            });

            foreach (var zoneDir in SortedDirs("./scripts/zones"))
            {
                if (!File.Exists(zoneDir + "/IDs.lua")) continue;
                zoneName = zoneDir.Substring(zoneDir.LastIndexOf('/') + 1);
                if (zoneName == "286" || (onlyZone != null && zoneName != onlyZone)) continue;
                byName.TryGetValue(zoneName, out var zone);

                lookup = new Dictionary<string, List<uint>>(StringComparer.Ordinal);
                if (zone != null)
                {
                    foreach (var folder in zone.DataFolders)
                    {
                        string dir = Path.GetFullPath(Path.Combine(Root, "data", "zones", folder));
                        var names = new EntityNames();
                        ScanEntityScripts(Path.Combine(dir, "mobs.yaml"), "spawns", lookup, names);
                        ScanEntityScripts(Path.Combine(dir, "npcs.yaml"), "npcs", lookup, names);
                        entityNames_[dir] = names;
                    }
                }
                foreach (var r in tables.Values) Lua.Unref(r);
                tables.Clear();

                SafeScriptFile($"scripts/zones/{zoneName}/IDs.lua");

                Lua.GetGlobal("package");
                Lua.GetField(-1, "loaded");
                Lua.GetGlobal("zones");
                if (Lua.IsTable(-1))
                {
                    Lua.Push((double)(zone?.Id ?? 0));
                    Lua.GetTable(-2);
                    Lua.SetField(-3, $"scripts/zones/{zoneName}/IDs");
                }
                Lua.Pop(3);
            }

            lookup = null;
            foreach (var r in tables.Values) Lua.Unref(r);
            tables.Clear();
            Lua.SetGlobal("GetFirstID", l => { log_("GetFirstID is designed to be used at load/reload-time only!"); return 0; });
            Lua.SetGlobal("GetTableOfIDs", l => { log_("GetTableOfIDs is designed to be used at load/reload-time only!"); return 0; });
        }

        // luautils::GetTextIDVariable: zones[zoneId].text[variable], 0 when there is none
        public int GetTextIdVariable(ushort zoneId, string variable)
        {
            int top = Lua.Top;
            int id = 0;
            Lua.GetGlobal("zones");
            if (Lua.IsTable(-1))
            {
                Lua.Push((double)zoneId);
                Lua.GetTable(-2);
                if (Lua.IsTable(-1))
                {
                    Lua.GetField(-1, "text");
                    if (Lua.IsTable(-1))
                    {
                        Lua.GetField(-1, variable);
                        if (Lua.IsNumber(-1)) id = (int)Lua.ToIntegral(-1); // get_or(0)
                    }
                }
            }
            Lua.SetTop(top);
            return id;
        }

        private static IEnumerable<string> SortedDirs(string dir)
        {
            var list = Directory.GetDirectories(dir).Select(d => d.Replace('\\', '/')).ToList();
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        // the entity names of one data/zones folder as its npcs.yaml and mobs.yaml give them: every npc's script ("" for
        // none), every spawn's script (its template's when it has none), placed (an at / region / path / circuit) or not
        public sealed class EntityNames
        {
            public readonly HashSet<string> Npcs = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> PlacedMobs = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> UnplacedMobs = new HashSet<string>(StringComparer.Ordinal);
        }

        // full folder path -> its names: PopulateIDLookups' scan keeps them, so the quest registration's entity check reads
        // no yaml file a second time
        private readonly Dictionary<string, EntityNames> entityNames_ = new Dictionary<string, EntityNames>(StringComparer.Ordinal);

        // the names of a data/zones folder (zoneDir: its path): the ID scan's, else scanned now (a zone with no IDs.lua)
        public EntityNames EntityNamesOf(string zoneDir)
        {
            string dir = Path.GetFullPath(zoneDir);
            if (entityNames_.TryGetValue(dir, out var names)) return names;
            return entityNames_[dir] = ScanEntityNames(dir);
        }

        // whether EntityNamesOf answers the folder from the ID scan (or an earlier ask) without reading it
        public bool HasEntityNames(string zoneDir) => entityNames_.ContainsKey(Path.GetFullPath(zoneDir));

        // a folder's names by the same line scan, outside PopulateIDLookups
        public static EntityNames ScanEntityNames(string zoneDir)
        {
            var names = new EntityNames();
            var ignored = new Dictionary<string, List<uint>>(StringComparer.Ordinal);
            ScanEntityScripts(Path.Combine(zoneDir, "mobs.yaml"), "spawns", ignored, names);
            ScanEntityScripts(Path.Combine(zoneDir, "npcs.yaml"), "npcs", ignored, names);
            return names;
        }

        // the id -> script pairs of one mobs.yaml ("spawns:", a spawn's script defaulting to its template) or
        // npcs.yaml ("npcs:"), read line by line: two-space keys are ids, four-space script/template fields (and a
        // spawn's at / region / path / circuit, for names). names: each entry's name added, as EntityNames keeps them
        private static void ScanEntityScripts(string path, string section, Dictionary<string, List<uint>> lookup, EntityNames names = null)
        {
            if (!File.Exists(path)) return;
            bool inSection = false;
            uint id = 0;
            string script = null, template = null;
            bool haveId = false, placed = false;
            bool mobs = section == "spawns";

            void Flush()
            {
                if (!haveId) return;
                string name = script ?? template;
                if (name != null)
                {
                    if (!lookup.TryGetValue(name, out var ids)) lookup[name] = ids = new List<uint>();
                    ids.Add(id);
                }
                if (names != null)
                {
                    // MobsFile: an empty script falls back to the template; NpcEntity: no script is ""
                    if (mobs) (placed ? names.PlacedMobs : names.UnplacedMobs).Add(string.IsNullOrEmpty(script) ? template ?? "" : script);
                    else names.Npcs.Add(script ?? "");
                }
                haveId = false;
                placed = false;
                script = template = null;
            }

            foreach (var raw in File.ReadLines(path))
            {
                if (raw.Length == 0 || raw[0] == '#') continue;
                if (raw[0] != ' ')
                {
                    Flush();
                    inSection = raw.StartsWith(section + ":", StringComparison.Ordinal);
                    continue;
                }
                if (!inSection) continue;
                if (raw.Length > 2 && raw[2] != ' ' && raw.StartsWith("  ", StringComparison.Ordinal))
                {
                    Flush();
                    var key = raw.Trim().TrimEnd(':');
                    haveId = uint.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out id);
                    continue;
                }
                if (raw.StartsWith("    ", StringComparison.Ordinal) && raw.Length > 4 && raw[4] != ' ')
                {
                    var line = raw.Substring(4);
                    if (line.StartsWith("script:", StringComparison.Ordinal)) script = YamlScalar(line.Substring(7));
                    else if (line.StartsWith("template:", StringComparison.Ordinal)) template = YamlScalar(line.Substring(9));
                    else if (line.StartsWith("at:", StringComparison.Ordinal) || line.StartsWith("region:", StringComparison.Ordinal) ||
                             line.StartsWith("path:", StringComparison.Ordinal) || line.StartsWith("circuit:", StringComparison.Ordinal)) placed = true;
                }
            }
            Flush();
        }

        private static string YamlScalar(string s)
        {
            s = s.Trim();
            int hash = s.IndexOf(" #", StringComparison.Ordinal);
            if (hash >= 0) s = s.Substring(0, hash).TrimEnd();
            if (s.Length >= 2 && (s[0] == '"' || s[0] == '\'') && s[^1] == s[0]) s = s.Substring(1, s.Length - 2);
            return s;
        }

        // ---- interaction framework / entity scripts ------------------------------------------------------------

        // luautils::InitInteractionGlobal: InteractionGlobal.initZones({ [zoneId] = zoneName }) loads each zone's
        // DefaultActions and every quest/mission/battlefield container
        public bool InitInteractionGlobal(IEnumerable<ScriptZone> zones)
        {
            try
            {
                Lua.DoString("InteractionGlobal.initZones(...)", "=InitInteractionGlobal", 0, () =>
                {
                    Lua.NewTable();
                    foreach (var z in zones)
                    {
                        Lua.PushString(z.Name);
                        Lua.RawSetI(-2, z.Id);
                    }
                    return 1;
                });
                return true;
            }
            catch (LuaException e)
            {
                error_("luautils::InitInteractionGlobal: " + e.Message);
                return false;
            }
        }

        // "./scripts/a/b/c.lua" -> { "a", "b", "c" }: the xi.a.b.c path of a script's object
        private static List<string> ObjectPath(string filename)
        {
            var parts = filename.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
            int scripts = parts.IndexOf("scripts");
            if (scripts < 0) return null;
            parts.RemoveRange(0, scripts + 1);
            if (parts.Count == 0) return null;
            string last = parts[^1];
            int dot = last.LastIndexOf('.');
            if (dot > 0) parts[^1] = last.Substring(0, dot);
            return parts;
        }

        // LoadLuaObjectFromFile for entity/zone scripts: run the file and store what it returns at xi.<path>
        // (only if that slot is empty, unless overwrite)
        public void LoadLuaObjectFromFile(string filename, bool overwrite = false)
        {
            var parts = ObjectPath(filename);
            if (parts == null)
            {
                error_($"luautils::LoadLuaObjectFromFile: Invalid filename: {filename}");
                return;
            }
            if (!File.Exists(filename)) return;

            int top = Lua.Top;
            try
            {
                Lua.DoFile(filename, "@" + filename, 1);
            }
            catch (LuaException e)
            {
                error_($"luautils::LoadLuaObjectFromFile: Load error: {filename}: {e.Message}");
                return;
            }
            if (Lua.IsNil(-1))
            {
                Lua.SetTop(top);
                error_($"luautils::LoadLuaObjectFromFile: No returned object to cache: {filename}");
                return;
            }
            int result = Lua.Top;

            Lua.GetGlobal("xi");
            for (int i = 0; i < parts.Count - 1; i++)
            {
                Lua.GetField(-1, parts[i]);
                if (!Lua.IsTable(-1))
                {
                    Lua.Pop(1);
                    Lua.NewTable();
                    Lua.PushValue(-1);
                    Lua.SetField(-3, parts[i]);
                }
                Lua.Remove(-2);
            }
            Lua.GetField(-1, parts[^1]);
            bool empty = Lua.IsNil(-1);
            Lua.Pop(1);
            if (overwrite || empty)
            {
                Lua.PushValue(result);
                Lua.SetField(-2, parts[^1]);
            }
            Lua.SetTop(top);
            // luautils.cpp:1074: the modules' overrides of this script, stored or not (update_if_empty kept the old)
            if (Modules != null && LuaModules.ScriptParts(filename) is { } moduleParts) Modules.TryApply(moduleParts, overwrite);
        }

        // getCachedFileFunction: push xi.<path>[funcName] if it is a function, else nil (missing tables are not
        // created, unlike GetLuaObjectFromFilename's get_or_create)
        public void PushFileFunction(string filename, string funcName)
        {
            var parts = ObjectPath(filename);
            int top = Lua.Top;
            if (parts == null)
            {
                Lua.PushNil();
                return;
            }
            Lua.GetGlobal("xi");
            foreach (var part in parts)
            {
                if (!Lua.IsTable(-1)) break;
                Lua.GetField(-1, part);
                Lua.Remove(-2);
            }
            if (Lua.IsTable(-1)) Lua.GetField(-1, funcName);
            else Lua.PushNil();
            if (!Lua.IsFunction(-1))
            {
                Lua.SetTop(top);
                Lua.PushNil();
                return;
            }
            Lua.Remove(-2);
        }
    }
}
