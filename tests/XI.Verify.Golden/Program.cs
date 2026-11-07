using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using XI.Common;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Golden -- golden-value combat parity: golden/combat.tsv holds what the real C++ server (xi_test.exe) computes
// for every case of lua/golden_probe.lua (capture/capture.sh regenerates it). This suite runs the same cases through
// the C# ZoneHost in West Ronfaure against the real MariaDB (xidb) and checks
//   * every probe value, by running the same Lua probe over the C# bindings (stats, the Lua formulas, damage taken,
//     enmity), exactly as text;
//   * the C# native ports of the formulas battleutils calls in Lua (Melee: hit rate, fSTR, pDIF, TP return and gain,
//     the ATT / ACC / DEF / EVA getters), against the golden values of the same keys, under the same scripted draws,
//     for the char and for a pet case's pet.
// The cases: jobs / levels / gear / modifier variants, effects (Signet, Innin, Yonin), pets (avatars, spirits, wyverns,
// jug pets, automatons, luopans), merit / job point chars (golden.chars 9+, raised with the 0x0BE / 0x0BF packets in
// their Mog House), job abilities, spells, cure enmity through another char and weapon skill damage taken; and (golden 3)
// what only golden_hooks reaches -- the capture's xi_test is built with capture/xi_test_hooks.patch
// (capture/build_xi_test.sh), this check registers the same table over the C# ports (RegisterHooks): the native
// auto-attack swing with Sneak / Trick Attack, GetCritHitRate and the native hit rate, MagicDmgTaken, cast / recast
// times, area spells with their real target count, the blood pact scripts with a real pet skill, the weapon skill
// scripts with merits past their unlock and Flash's penalty by the time left.
// GOLDEN_ONLY=<Lua pattern> runs only the matching cases, GOLDEN_FILE=<file> reads another golden file (a partial
// capture's partial.tsv), GOLDEN_SHOW=<text> prints every failure whose key has it, GOLDEN_LOG prints the server log,
// GOLDEN_DUMP=<file> appends the C# values of every case run to the file.
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail, _printed;
    private static readonly SortedDictionary<string, int> FailsByKey = new SortedDictionary<string, int>(StringComparer.Ordinal);

    // golden.chars: 1-8 one per race, as the capture spawns one per race; 9+ the merit / job point chars
    private const uint CharBase = 995981;
    private const uint ExtraCharBase = 995991;
    private const int MaxChars = 17;
    private const ushort WestRonfaure = 100;
    private const int MaxPrinted = 300;

    // The same value: the same text, or two '%.17g' texts of the same double. golden.fmt prints a fraction with
    // string.format('%.17g'), and a float widened to double often ends in an exact decimal tie at the 17th digit
    // (1.73246002197265625): the C++ server's LuaJIT (2022) rounds that tie up (...563), the C# host's newer one
    // rounds it to even (...562, LuaJIT 7152e154), as C's printf does. Both texts are the one double. Any other
    // mismatch fails: a shorter text (an integer, "5.0", a tie rounded to a stripped 0), spaces, a sign or leading
    // zero %.17g does not print, nan and inf.
    internal static bool SameValue(string expected, string actual)
    {
        if (expected == actual) return true;
        return Full17(expected, out double e) && Full17(actual, out double a) &&
               BitConverter.DoubleToInt64Bits(e) == BitConverter.DoubleToInt64Bits(a);
    }

    // a finite number in the form %.17g prints it with all 17 significant digits ("-457.23013305664062",
    // "1.2345678901234567e-05")
    private static readonly System.Text.RegularExpressions.Regex G17 =
        new(@"^-?(0|[1-9][0-9]*)\.[0-9]+(e[+-][0-9]{2,3})?$|^-?[1-9][0-9]{16}$");

    private static bool Full17(string text, out double value)
    {
        value = 0;
        if (!G17.IsMatch(text)) return false;
        string mantissa = text.Split('e')[0].TrimStart('-').Replace(".", "").TrimStart('0');
        return mantissa.Length == 17 &&
               double.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint |
                               NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out value) &&
               double.IsFinite(value);
    }

    private static void Check(string caseName, string key, string expected, string actual)
    {
        if (SameValue(expected, actual))
        {
            _pass++;
            return;
        }

        _fail++;
        // a key family: the key with its numeric and draw-mode parts dropped
        string family = string.Join(".", key.Split('.').Where(p => !int.TryParse(p, out _)).Take(3));
        FailsByKey[family] = FailsByKey.GetValueOrDefault(family) + 1;
        // GOLDEN_SHOW=<text>: print every failure whose key contains it (else the first few of each key family, up to
        // MaxPrinted)
        var show = Environment.GetEnvironmentVariable("GOLDEN_SHOW");
        if (show != null ? key.Contains(show, StringComparison.Ordinal) : FailsByKey[family] <= 4 && _printed++ < MaxPrinted)
            Console.WriteLine($"FAIL {caseName} {key}  expected=[{expected}] actual=[{actual}]");
    }

    private static ResultSet Sql(IDatabase db, string q, params BoundValue[] a) => db.Execute(q, a);

    // golden.chars[i]'s char id
    private static uint CharId(int i) => i <= 8 ? CharBase + (uint)i - 1 : ExtraCharBase + (uint)i - 9;

    private static void Cleanup(IDatabase db)
    {
        foreach (uint id in Enumerable.Range(1, MaxChars).Select(CharId))
        {
            foreach (var t in new[] { "accounts_sessions", "char_vars", "char_storage", "char_stats", "char_jobs", "char_exp", "char_profile",
                                      "char_points", "char_inventory", "char_equip", "char_look", "char_spells", "char_history", "char_skills",
                                      "char_merit", "char_job_points", "char_effects", "char_pet" })
                Sql(db, $"DELETE FROM {t} WHERE charid = ?", BoundValue.Of(id));
            Sql(db, "DELETE FROM chars WHERE charid = ?", BoundValue.Of(id));
            Sql(db, "DELETE FROM accounts WHERE id = ?", BoundValue.Of(id));
        }
    }

    private static string FindDataDir()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var cand = Path.Combine(d.FullName, "data");
            if (File.Exists(Path.Combine(cand, "grades.yaml"))) return cand;
        }
        throw new DirectoryNotFoundException("data (grades.yaml) not found above " + AppContext.BaseDirectory);
    }

    private static string FindGoldenDir()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var cand = Path.Combine(d.FullName, "tests", "XI.Verify.Golden");
            if (File.Exists(Path.Combine(cand, "lua", "golden_probe.lua"))) return cand;
            if (File.Exists(Path.Combine(d.FullName, "lua", "golden_probe.lua"))) return d.FullName;
        }
        throw new DirectoryNotFoundException("tests/XI.Verify.Golden not found above " + AppContext.BaseDirectory);
    }

    private static int Main(string[] argv)
    {
        int dbPort = argv.Length > 0 ? int.Parse(argv[0]) : 3307;
        // SameValue takes the two roundings of one tie, and nothing a bit apart, a shorter or padded text, nan, nil
        // or a boolean
        if (!SameValue("1.7324600219726563", "1.7324600219726562") ||
            !SameValue("-457.23013305664063", "-457.23013305664062") ||
            !Full17("1.2345678901234567e-05", out _) || !Full17("0.00012345678901234567", out _) ||
            SameValue("1.7324600219726563", "1.7324600219726565") || SameValue("1", "1.0000000000000002") ||
            SameValue("5", "5.0") || SameValue("5", " 5") || SameValue("5", "+5") || SameValue("1e2", "100") ||
            SameValue(" 1.7324600219726563", "1.7324600219726562") ||
            SameValue("01.7324600219726563", "1.7324600219726562") || SameValue("5.0", "5.00") ||
            SameValue("1.0000000000000000e+999", "1.0000000000000001e+999") ||
            SameValue("nan", "-nan") || SameValue("inf", "Infinity") || SameValue("nil", "0") ||
            SameValue("true", "1"))
        {
            Console.WriteLine("FAIL SameValue self-check");
            return 1;
        }
        var settings = new NetworkSettings { SqlHost = "127.0.0.1", SqlPort = dbPort, SqlLogin = "root", SqlPassword = "root", SqlDatabase = "xidb" };
        using var db = new MySqlDatabase(settings);

        try
        {
            var probe = db.Execute("SELECT 1", Array.Empty<BoundValue>());
            if (probe == null || !probe.Next()) { Console.WriteLine("DB not reachable"); return 3; }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DB not reachable ({ex.GetType().Name}: {ex.Message})");
            return 3;
        }

        try
        {
            Cleanup(db);
            Run(db);
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FAIL exception: {ex}");
        }
        finally
        {
            Cleanup(db);
        }

        if (FailsByKey.Count > 0)
        {
            Console.WriteLine("failures by key:");
            foreach (var (k, n) in FailsByKey) Console.WriteLine($"  {n,6} {k}");
        }
        Console.WriteLine($"{_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    // golden/combat.tsv: case -> its key / value lines in order
    private static Dictionary<string, List<(string Key, string Value)>> LoadGolden(string path)
    {
        var cases = new Dictionary<string, List<(string, string)>>();
        foreach (var line in File.ReadLines(path))
        {
            if (line.Length == 0) continue;
            var parts = line.Split('\t');
            if (!cases.TryGetValue(parts[0], out var list)) cases[parts[0]] = list = new List<(string, string)>();
            list.Add((parts[1], parts.Length > 2 ? parts[2] : ""));
        }
        return cases;
    }

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var goldenDir = FindGoldenDir();
        // GOLDEN_FILE: another file under golden/ (a partial capture's partial.tsv)
        var golden = LoadGolden(Path.Combine(goldenDir, "golden", Environment.GetEnvironmentVariable("GOLDEN_FILE") ?? "combat.tsv"));
        Console.WriteLine($"golden: {golden.Count} cases, {golden.Values.Sum(l => l.Count)} values");

        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, AbilityTable.Load(db, _ => true, 1.0), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        // Firesday 12:00, no weather: the capture's clock (setVanaDay / setVanaTime) and xi_test's controlledWeather
        zone.VanaTicksNow = () => 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        zone.ControlledWeather = true;
        zone.MobRng = new Random(7);
        zone.SendIpc = _ => { };
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        scripts.VanaTicksNow = zone.VanaTicksNow;
        zone.InitScripts(scripts);
        var info = zone.ZoneInfoOf(WestRonfaure);

        var lua = scripts.Lua;
        RegisterHooks(lua, zone);
        var emitted = new List<(string Key, string Value)>();
        lua.SetGlobal("golden_emit", l => { emitted.Add((l.ToStringValue(1), l.ToStringValue(2))); return 0; });
        var probePath = Path.Combine(goldenDir, "lua", "golden_probe.lua").Replace('\\', '/');
        lua.DoString($"GOLDEN = dofile([[{probePath}]]) GOLDEN_CASES = GOLDEN.cases() golden_emit('count', #GOLDEN_CASES) " +
                     "for i, spec in ipairs(GOLDEN.chars) do golden_emit(tostring(i), spec.race) end " +
                     $"GOLDEN.serverRoot = [[{Path.GetDirectoryName(dataDir).Replace('\\', '/')}]]", "=golden");
        int count = int.Parse(emitted[0].Value);
        var races = emitted.Skip(1).Select(e => byte.Parse(e.Value)).ToArray();
        if (races.Length > MaxChars) throw new InvalidOperationException($"golden.chars has {races.Length} chars, the check makes {MaxChars}");

        // the capture's chars: TestChar::create (a WAR, San d'Oria, the given race) with the NEW_ADVENTURER title
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        for (int i = 1; i <= races.Length; i++)
        {
            uint id = CharId(i);
            byte race = races[i - 1];
            Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of($"golden{i}"), BoundValue.Of("x"), BoundValue.Of((byte)1));
            Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,titles) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of($"Golden{(char)('a' + i)}"), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
                BoundValue.Of(-126.0f), BoundValue.Of(-62.0f), BoundValue.Of(273.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0), BoundValue.Of(titles));
            Sql(db, "REPLACE INTO char_look(charid, race, face, size) VALUES (?, ?, 0, 0)", BoundValue.Of(id), BoundValue.Of(race));
            Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(0u), BoundValue.Of((ushort)0));
            Sql(db, "REPLACE INTO char_stats(charid, mjob, mlvl) VALUES (?, 1, 1)", BoundValue.Of(id));
            Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 1)", BoundValue.Of(id));
            Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(id));
        }

        var chars = new Dictionary<int, CharData>();
        var sessions = new Dictionary<uint, MapSession>();
        void Send(MapSession s, ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(s, opcode, new BasicPacket(buf));
            zone.AfterParse(s);
        }
        for (int i = 1; i <= races.Length; i++)
        {
            uint id = CharId(i);
            var s = new MapSession { CharId = id, AccountId = id };
            zone.OnLogin(s);
            Send(s, 0x00A, 92, buf => BitConverter.GetBytes(id).CopyTo(buf, 12));
            Send(s, 0x00C, 12);
            chars[i] = (CharData)s.Char;
            sessions[id] = s;
        }
        Check("setup", "races", string.Join(" ", races), string.Join(" ", chars.Values.Select(c => c.Look.Race)));

        // the merit / job point chars raise theirs with the edit packets inside their own Mog House, as the capture
        // does (golden_send: the char id, the opcode, the size and the "offset:byte" pairs)
        lua.SetGlobal("golden_send", l =>
        {
            var s = sessions[(uint)l.ToIntArg(1)];
            Send(s, (ushort)l.ToIntArg(2), l.ToIntArg(3), buf =>
            {
                foreach (var pair in l.ToStringValue(4).Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = pair.Split(':');
                    buf[int.Parse(kv[0])] = byte.Parse(kv[1]);
                }
            });
            return 0;
        });
        lua.DoString("GOLDEN_IDS = {} " + string.Concat(Enumerable.Range(1, races.Length).Select(i => $"GOLDEN_IDS[{i}] = {CharId(i)} ")) +
                     "GOLDEN_CHAR_OF = function(i) return GetPlayerByID(GOLDEN_IDS[i]) end", "=golden");
        for (int i = 1; i <= races.Length; i++)
        {
            emitted.Clear();
            lua.DoString($"local spec = GOLDEN.chars[{i}] golden_emit('raise', tostring(spec.merits ~= nil or spec.jobPoints ~= nil))", "=golden");
            if (emitted[0].Value != "true") continue;
            var c = chars[i];
            c.MoghouseId = c.Id;
            lua.DoString($"local id = GOLDEN_IDS[{i}] GOLDEN.setupChar(GetPlayerByID(id), GOLDEN.chars[{i}], function(opcode, size, bytes) " +
                         "local t = {} for k, v in pairs(bytes) do t[#t + 1] = k .. ':' .. v end golden_send(id, opcode, size, table.concat(t, ',')) end)", "=golden");
            c.MoghouseId = 0;
        }

        // GOLDEN_ONLY=<Lua pattern>: only the matching cases (the case count is not checked then)
        var only = Environment.GetEnvironmentVariable("GOLDEN_ONLY");
        if (only == null) Check("setup", "case count", golden.Count.ToString(), count.ToString());

        var melee = zone.MeleeEngine;
        for (int i = 1; i <= count; i++)
        {
            emitted.Clear();
            lua.DoString($"local c = GOLDEN_CASES[{i}] golden_emit(c.name, c.char or c.race) golden_emit('mob', c.mob)", "=golden");
            string name = emitted[0].Key;
            int charIndex = int.Parse(emitted[0].Value);
            uint mobId = uint.Parse(emitted[1].Value);
            if (only != null)
            {
                emitted.Clear();
                lua.DoString($"golden_emit('match', tostring(string.find([==[{name}]==], [==[{only}]==]) ~= nil))", "=golden");
                if (emitted[0].Value != "true") continue;
            }
            // GOLDEN_DUMP=<file>: every run case's C# values go to the file too (a case missing from the golden file
            // runs anyway)
            var dump = Environment.GetEnvironmentVariable("GOLDEN_DUMP");
            if (!golden.TryGetValue(name, out var expected))
            {
                Check(name, "golden case", "present", "missing");
                if (dump == null) continue;
                expected = new List<(string Key, string Value)>();
            }

            // the probe over the C# bindings
            emitted.Clear();
            lua.DoString($"local c = GOLDEN_CASES[{i}] local p = GOLDEN_CHAR_OF({charIndex}) " +
                         "local ok, err = pcall(GOLDEN.run, p, c, golden_emit, GOLDEN_CHAR_OF) if not ok then golden_emit('!error', tostring(err)) end", "=golden");
            // GOLDEN_LOG: the server log lines each case wrote
            if (Environment.GetEnvironmentVariable("GOLDEN_LOG") != null)
                foreach (var line in log) Console.WriteLine($"LOG {name} {line}");
            log.Clear();
            if (dump != null) File.AppendAllLines(dump, emitted.Select(e => $"{name}	{e.Key}	{e.Value}"));
            var actual = new Dictionary<string, string>();
            foreach (var (k, v) in emitted) actual.TryAdd(k, v);
            if (actual.TryGetValue("!error", out var err)) Check(name, "!error", "", err);
            foreach (var (k, v) in expected) Check(name, k, v, actual.GetValueOrDefault(k, "<missing>"));

            // the C# native ports against the same golden values
            var gv = expected.ToDictionary(e => e.Key, e => e.Value);
            var mob = info.MobById[mobId];
            if (gv.ContainsKey("p.wtype")) NativeChecks(name, gv, melee, zone, chars[charIndex], mob);
            // the pet cases' pet (left out until the next case of its master)
            if (chars[charIndex].PPet is MobEntity pet)
            {
                if (gv.ContainsKey("pet.wtype")) PetNativeChecks(name, gv, "", melee, zone, pet, mob);
                if (gv.ContainsKey("withpet.pet.wtype")) PetNativeChecks(name, gv, "withpet.", melee, zone, pet, mob);
            }
        }
    }

    // ---- golden_hooks ------------------------------------------------------------------------------------------

    // the ZoneHost method of that name and argument count (the ports the hooks reach are private to the zone)
    private static object Call(ZoneHost zone, string name, params object[] args)
    {
        var method = typeof(ZoneHost).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Single(m => m.Name == name && m.GetParameters().Length == args.Length);
        return method.Invoke(zone, args);
    }

    // golden_hooks: the table the capture's xi_test gets from capture/xi_test_hooks.patch, over the C# ports of the
    // same C++ (Melee's native swing, crit and hit rates, ZoneHost's cast / recast times, MagicDmgTaken, the cast
    // finish's target search, the pet skill table, the effects' start)
    private static void RegisterHooks(LuaState lua, ZoneHost zone)
    {
        var melee = zone.MeleeEngine;
        BattleEntity B(LuaState l, int i) => l.ToObject(i) as BattleEntity ?? throw new InvalidOperationException($"golden_hooks: argument {i} is not a battle entity");

        lua.DoString("golden_hooks = {}", "=golden");
        void Hook(string name, LuaFunction fn)
        {
            lua.SetGlobal("__golden_hook", fn);
            lua.DoString($"golden_hooks.{name} = __golden_hook __golden_hook = nil", "=golden");
        }

        // battleutils::GetCritHitRate(attacker, defender, ignoreSneakTrickAttack, slot)
        Hook("critHitRate", l =>
        {
            l.Push((double)melee.CritHitRate(B(l, 1), B(l, 2), l.ToIntArg(4), l.ToBoolean(3)));
            return 1;
        });

        // battleutils::GetHitRate(attacker, defender, attackNumber, offsetAccuracy)
        Hook("hitRate", l =>
        {
            l.Push((double)melee.HitRate(B(l, 1), B(l, 2), l.ToIntArg(3), (short)l.ToIntArg(4)));
            return 1;
        });

        // the first swing of a round: the damage ratio under the given draw sequence (the Lua side scripts the C++
        // capture's), then ProcessDamage
        Hook("swing", l =>
        {
            var a = B(l, 1);
            var d = B(l, 2);
            bool critical = l.ToBoolean(3);
            var fractions = new double[l.ObjLen(4)];
            for (int i = 0; i < fractions.Length; i++)
            {
                l.RawGetI(4, i + 1);
                fractions[i] = l.ToNumber(-1);
                l.Pop(1);
            }

            // as the capture's hook: no round for a Trick Attack without a battle target
            bool noTaTarget = a.StatusEffects.HasStatusEffect(EffectId.TrickAttack) &&
                              (a is CharData ac ? ac.BattleTarget : (a as MobEntity)?.BattleTarget) == null;
            var swings = noTaTarget ? new List<Melee.Swing>() : melee.BuildRound(a);
            if (swings.Count == 0)
            {
                l.Push(0.0);
                l.Push(false);
                l.Push(false);
                l.Push(0.0);
                l.Push(0.0);
                return 5;
            }

            var swing = swings[0];
            var saved = zone.MobRng;
            zone.MobRng = new ScriptedRandom(fractions);
            float ratio = melee.SwingDamageRatio(a, d, swing, critical);
            zone.MobRng = saved;
            int damage = melee.ProcessDamage(a, d, swing, ratio);
            l.Push((double)damage);
            l.Push(swing.SneakAttack);
            l.Push(swing.TrickAttack);
            l.Push((double)ratio);
            l.Push((double)swings.Count);
            return 5;
        });

        // CalculateSpellCastTime on a cast state holding a copy of the spell: milliseconds, the instant cast
        Hook("castTime", l =>
        {
            var e = B(l, 1);
            var spell = (Spell)Call(zone, "GetSpell", l.ToIntArg(2));
            var st = new MagicCastState { Spell = spell.Clone() };
            long ms = (long)Call(zone, "CalculateSpellCastTime", e, st);
            l.Push((double)ms);
            l.Push(st.InstantCast);
            return 2;
        });

        // CalculateSpellRecastTime, in milliseconds
        Hook("recastTime", l =>
        {
            var spell = (Spell)Call(zone, "GetSpell", l.ToIntArg(2));
            l.Push((double)(long)Call(zone, "CalculateSpellRecastTime", B(l, 1), spell));
            return 1;
        });

        // battleutils::MagicDmgTaken(defender, damage, element)
        Hook("magicDmgTaken", l =>
        {
            l.Push((double)(int)Call(zone, "MagicDmgTaken", B(l, 1), l.ToIntArg(2), l.ToIntArg(3)));
            return 1;
        });

        // a copy of the spell with the cast finish's target count around the target: the spell, the count
        Hook("spellTargets", l =>
        {
            var caster = B(l, 1);
            var target = B(l, 2);
            var spell = ((Spell)Call(zone, "GetSpell", l.ToIntArg(3))).Clone();
            var (aoeType, radius) = ((byte, float))Call(zone, "MagicAoE", caster, spell);
            var targets = (List<BattleEntity>)Call(zone, "MagicTargets", caster, target, spell, aoeType, radius);
            spell.TotalTargets = (ushort)targets.Count;
            spell.PrimaryTargetId = target.Id;
            l.Push(spell);
            l.Push((double)spell.TotalTargets);
            return 2;
        });

        // a copy of the pet skill carrying the given TP
        Hook("petSkill", l =>
        {
            var skill = ((MobSkill)Call(zone, "GetPetSkill", l.ToIntArg(1))).Clone();
            skill.Tp = (short)l.ToIntArg(2);
            l.Push(skill);
            return 1;
        });

        // puts the mob in (or takes it out of) the char's list of visible mobs
        Hook("seeMob", l =>
        {
            if (l.ToObject(1) is not CharData c || l.ToObject(2) is not MobEntity mob) throw new InvalidOperationException("golden_hooks.seeMob: needs a char and a mob");
            if (l.ToBoolean(3)) c.SpawnMobList.Add(mob.Id);
            else c.SpawnMobList.Remove(mob.Id);
            l.Push(true);
            return 1;
        });

        // starts the entity's effect the given milliseconds ago
        Hook("setEffectElapsed", l =>
        {
            var effect = B(l, 1).StatusEffects.GetStatusEffect((ushort)l.ToIntArg(2));
            if (effect != null) effect.StartMs = zone.NowMs() - l.ToIntArg(3);
            l.Push(effect != null);
            return 1;
        });
    }

    // ---- the native ports ---------------------------------------------------------------------------------------

    // a System.Random drawing the probe's scripted fractions: [min, max) maps to min + floor(f * (max - min))
    private sealed class ScriptedRandom : Random
    {
        private readonly double[] fractions_;
        private int n_;

        public ScriptedRandom(double[] fractions) => fractions_ = fractions;

        private double NextFraction() => fractions_[n_++ % fractions_.Length];

        public override long NextInt64(long minValue, long maxValue)
        {
            if (maxValue <= minValue) return minValue;
            long size = maxValue - minValue;
            return minValue + Math.Min((long)Math.Floor(NextFraction() * size), size - 1);
        }

        public override int Next(int minValue, int maxValue) => (int)NextInt64(minValue, maxValue);
        public override int Next(int maxValue) => (int)NextInt64(0, maxValue);
        public override int Next() => (int)NextInt64(0, int.MaxValue);
        public override double NextDouble() => NextFraction();
        protected override double Sample() => NextFraction();
    }

    private static readonly Dictionary<string, double[]> RngModes = new Dictionary<string, double[]>
    {
        ["low"] = new[] { 0.0 },
        ["high"] = new[] { 0.999999 },
        ["mid"] = new[] { 0.5 },
        ["mix1"] = new[] { 0.999999, 0, 0.5, 0.5 },
        ["mix2"] = new[] { 0.999999, 0.999999, 0.25, 0 },
        ["mix3"] = new[] { 0.75, 0.2, 0.9, 0.6 },
    };

    private static string Fmt(double v) => v == Math.Floor(v) && Math.Abs(v) < 4503599627370496.0
        ? ((long)v).ToString(CultureInfo.InvariantCulture)
        : v.ToString("R", CultureInfo.InvariantCulture);

    private static double Num(string s) => double.Parse(s, CultureInfo.InvariantCulture);

    // sol2's get<int32> of a Lua number: llround (half away from zero)
    private static string LuaInt(string s) => ((int)Math.Round(Num(s), MidpointRounding.AwayFromZero)).ToString();

    // the pet cases: the pet as attacker (petm) and as target (mpet), its stats and TP
    private static void PetNativeChecks(string name, Dictionary<string, string> g, string prefix, Melee melee, ZoneHost zone, MobEntity pet, MobEntity m)
    {
        void Same(string key, string actual)
        {
            if (g.TryGetValue(prefix + key, out var exp)) Check(name, "native " + prefix + key, exp, actual);
        }

        Same("pet.STR", Melee.Str(pet).ToString());
        Same("pet.DEX", Melee.Dex(pet).ToString());
        Same("pet.VIT", Melee.Vit(pet).ToString());
        Same("pet.AGI", Melee.Agi(pet).ToString());
        Same("pet.INT", Melee.Int(pet).ToString());
        Same("pet.MND", Melee.Mnd(pet).ToString());
        Same("pet.CHR", Melee.Chr(pet).ToString());
        Same("pet.ATT", melee.Att(pet, Melee.SlotMain).ToString());
        Same("pet.ACC", melee.Acc(pet, 0).ToString());
        Same("pet.DEF", melee.Def(pet).ToString());
        Same("pet.EVA", Melee.Eva(pet).ToString());
        Same("pet.delay", Melee.BaseDelay(pet).ToString());

        string Pct(string key) => g.TryGetValue(prefix + key, out var v) ? ((byte)(Num(v) * 100)).ToString() : null;
        void SameHit(string key, int actual)
        {
            if (Pct(key) is { } exp) Check(name, "native " + prefix + key, exp, actual.ToString());
        }
        SameHit("hit.petm.0", melee.HitRate(pet, m, 0));
        SameHit("hit.petm.1", melee.HitRate(pet, m, 1));
        SameHit("hit.petm.0.acc20", melee.HitRate(pet, m, 0, 20));
        SameHit("hit.mpet.0", melee.HitRate(m, pet, 0));

        string Rounded(string key) => g.TryGetValue(prefix + key, out var v) ? LuaInt(v) : null;
        if (Rounded("fstr.petm") is { } f1) Check(name, "native " + prefix + "fstr.petm", f1, Melee.FStr(pet, m).ToString());
        if (Rounded("fstr.mpet") is { } f2) Check(name, "native " + prefix + "fstr.mpet", f2, Melee.FStr(m, pet).ToString());

        byte wtype = (byte)Num(g[prefix + "pet.wtype"]);
        foreach (var (mode, fractions) in RngModes)
        {
            foreach (bool crit in new[] { false, true })
            {
                string ck = crit ? "crit" : "norm";
                zone.MobRng = new ScriptedRandom(fractions);
                float pm = melee.DamageRatio(pet, m, crit, wtype, Melee.SlotMain);
                zone.MobRng = new ScriptedRandom(fractions);
                float mp = melee.DamageRatio(m, pet, crit, 0, Melee.SlotMain);
                foreach (var (key, actual) in new[] { ($"pdif.petm.{ck}.{mode}", pm), ($"pdif.mpet.{ck}.{mode}", mp) })
                    if (g.TryGetValue(prefix + key, out var exp))
                        Check(name, "native " + prefix + key, Fmt(Math.Max((float)Num(exp), 0f)), Fmt(actual));
            }
        }
        zone.MobRng = new Random(7);

        foreach (int delay in new[] { 180, 240, 480, 600 })
            Same($"tp.ret.pet.{delay}", Melee.TpReturn(pet, delay).ToString());
        Same("tp.hit.pet", Melee.SingleMeleeHitTpReturn(pet).ToString());
        foreach (int dmg in new[] { 0, 1, 50, 400 })
        {
            Same($"tp.taken.bypet.{dmg}", Melee.TpGainOnPhysicalDamage(pet, m, dmg, Melee.BaseDelay(pet)).ToString());
            Same($"tp.taken.pet.{dmg}", Melee.TpGainOnPhysicalDamage(m, pet, dmg, Melee.BaseDelay(m)).ToString());
        }
    }

    private static void NativeChecks(string name, Dictionary<string, string> g, Melee melee, ZoneHost zone, CharData p, MobEntity m)
    {
        void Same(string key, string actual)
        {
            if (g.TryGetValue(key, out var exp)) Check(name, "native " + key, exp, actual);
        }

        foreach (var (who, e) in new[] { ("p", (BattleEntity)p), ("m", (BattleEntity)m) })
        {
            Same(who + ".STR", Melee.Str(e).ToString());
            Same(who + ".DEX", Melee.Dex(e).ToString());
            Same(who + ".VIT", Melee.Vit(e).ToString());
            Same(who + ".AGI", Melee.Agi(e).ToString());
            Same(who + ".INT", Melee.Int(e).ToString());
            Same(who + ".MND", Melee.Mnd(e).ToString());
            Same(who + ".CHR", Melee.Chr(e).ToString());
            Same(who + ".ATT", melee.Att(e, Melee.SlotMain).ToString());
            Same(who + ".ATT.sub", melee.Att(e, Melee.SlotSub).ToString());
            Same(who + ".ACC", melee.Acc(e, 0).ToString());
            Same(who + ".ACC.1", melee.Acc(e, 1).ToString());
            Same(who + ".ACC.2", melee.Acc(e, 2).ToString());
            Same(who + ".DEF", melee.Def(e).ToString());
            Same(who + ".EVA", Melee.Eva(e).ToString());
            Same(who + ".delay", Melee.BaseDelay(e).ToString());
        }
        Same("p.wdmg", Melee.MainWeaponDmg(p).ToString());
        Same("p.subdmg", Melee.SubWeaponDmg(p).ToString());
        Same("p.wrank", Melee.MainWeaponRank(p).ToString());
        Same("lc.p", melee.IsLevelCorrected(p) ? "true" : "false");

        // GetHitRate: the Lua rate floored to a percent (std::floor<uint8>(luaHitRate * 100))
        string Pct(string key) => g.TryGetValue(key, out var v) ? ((byte)(Num(v) * 100)).ToString() : null;
        void SameHit(string key, int actual)
        {
            if (Pct(key) is { } exp) Check(name, "native " + key, exp, actual.ToString());
        }
        SameHit("hit.pm.0", melee.HitRate(p, m, 0));
        SameHit("hit.pm.1", melee.HitRate(p, m, 1));
        SameHit("hit.pm.0.acc20", melee.HitRate(p, m, 0, 20));
        SameHit("hit.mp.0", melee.HitRate(m, p, 0));

        // GetFSTR: callGlobal<int32> of the Lua number (sol2's llround)
        string Rounded(string key) => g.TryGetValue(key, out var v) ? LuaInt(v) : null;
        if (Rounded("fstr.pm") is { } fpm) Check(name, "native fstr.pm", fpm, Melee.FStr(p, m).ToString());
        if (Rounded("fstr.mp") is { } fmp) Check(name, "native fstr.mp", fmp, Melee.FStr(m, p).ToString());

        // GetDamageRatio: the Lua pDIF as a float, floored at 0, under each draw sequence
        byte wtype = (byte)Num(g["p.wtype"]);
        foreach (var (mode, fractions) in RngModes)
        {
            foreach (bool crit in new[] { false, true })
            {
                string ck = crit ? "crit" : "norm";
                zone.MobRng = new ScriptedRandom(fractions);
                float pm = melee.DamageRatio(p, m, crit, wtype, Melee.SlotMain);
                zone.MobRng = new ScriptedRandom(fractions);
                float mp = melee.DamageRatio(m, p, crit, 0, Melee.SlotMain);
                foreach (var (key, actual) in new[] { ($"pdif.pm.{ck}.{mode}", pm), ($"pdif.mp.{ck}.{mode}", mp) })
                    if (g.TryGetValue(key, out var exp))
                        Check(name, "native " + key, Fmt(Math.Max((float)Num(exp), 0f)), Fmt(actual));
            }
        }
        zone.MobRng = new Random(7);

        // TP: xi.combat.tp
        foreach (int delay in new[] { 96, 180, 240, 300, 360, 480, 500, 540, 600, 700, 800, 960 })
        {
            Same($"tp.ret.p.{delay}", Melee.TpReturn(p, delay).ToString());
            Same($"tp.ret.m.{delay}", Melee.TpReturn(m, delay).ToString());
        }
        Same("tp.moddelay.p", Melee.ModifiedDelay(p, Melee.BaseDelay(p)).ToString());
        Same("tp.canzanshin.p", Melee.CanZanshin(p) ? "true" : "false");
        Same("tp.hit.p", Melee.SingleMeleeHitTpReturn(p).ToString());
        Same("tp.hit.p.zanshin", Melee.SingleMeleeHitTpReturn(p, true).ToString());
        Same("tp.hit.m", Melee.SingleMeleeHitTpReturn(m).ToString());
        foreach (int dmg in new[] { 0, 1, 50, 400 })
        {
            Same($"tp.taken.m.{dmg}", Melee.TpGainOnPhysicalDamage(p, m, dmg, Melee.BaseDelay(p)).ToString());
            Same($"tp.taken.p.{dmg}", Melee.TpGainOnPhysicalDamage(m, p, dmg, Melee.BaseDelay(m)).ToString());
        }
    }
}
