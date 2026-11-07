using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using XI.Map;
using XI.Persistence;
using XI.QuestContent;
using XI.Quests;
using XI.Quests.Authoring;

// XI.Verify.QuestScripts.Live -- live gate for the quest framework (docs/superpowers/specs/2026-10-21-csharp-quests-live-design.md)
// on the real MariaDB (xidb), chars 996141-996152: the registration of the C# quests (XI.QuestContent) and of test-only
// ones (ZoneHost.TestQuestScripts), the switches (CSHARP_QUESTS, CSHARP_QUESTS_OFF), a broken definition refused with its
// reason, a registration that throws left to Lua, InteractionGlobal.reload keeping the C# quests, !reloadquest, the
// generated constants, the container order, the NPC-call quests, the facets and the var cache.
//
// The six B2 quests' flows (bastok: The Elvaan Goldsmith ... The Curse Collector) are pinned by XI.Verify.QuestGolden.Live's
// traces since their Lua files were deleted (B3a task 6); the Lua-versus-C# twin runs that lived here went with the files.
// The switch, reload and refusal checks run on the Lua fixture, a test-only Lua quest file (Fixtures/QSL_Fixture.lua,
// listed as scripts/quests/jeuno/QSL_Fixture), through a test-only C# quest of its key. Real quests were the fixture
// until each was ported (Father Figure, Promotion: Private First Class, Embers of His Past, Scattered into Shadow);
// since B3b Deferred 1 the fixture is a file of its own on a key no quest has (TestKeysFree). A quest whose Lua file is
// gone (The Elvaan Goldsmith) gets the CSHARP_QUESTS_OFF line, the order and the !reloadquest checks that fit it.
//
// Arguments: [<port>] [--db <name>], the MariaDB port (3307) and the database (xidb; a golden lane's, tools/golden_lane_db.sh).
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static partial class Program
{
    private static int _pass, _fail;

    private static readonly uint[] CharIds = { 996141, 996142, 996143, 996144, 996145, 996146, 996147, 996148, 996149, 996150, 996151, 996152 };
    private static int nextChar_;
    private const int TitleNewAdventurer = 206;
    private const string ElvaanKey = "bastok.the_elvaan_goldsmith";
    private const string ElvaanLuaFile = "scripts/quests/bastok/The_Elvaan_Goldsmith";
    private const string ElvaanCsFile = "csharp:" + ElvaanKey;
    // the Lua fixture: Fixtures/QSL_Fixture.lua, a test-only Lua quest on jeuno MYSTERY_OF_FIRE (a key with no Lua
    // file, no Lua draft, no C# quest and no quest-drafts entry: TestKeysFree), listed in every world's containers as
    // the Lua quest file FixtureLuaFile (ScriptEngine.ExtraContainerFiles, Harness.cs NewWorld), so its container, slot
    // and lookup order are a Lua quest's. The switch, reload and refusal checks run on it; the test-only probes (Samia
    // in Bastok Markets) take its key too. Its Lua side, as (log, id) arguments: FixtureLuaIds. Brutus's onTrigger has
    // FixtureTriggers entries of it (the sections 1, 2 and 3), Tebhi's onTrade one (section 2), and Upper Jeuno's
    // onEventFinish the csids 141 / 143 / 135 / 144
    private const QuestLog FixtureLog = QuestLog.Jeuno;
    private const int FixtureLogId = 3;
    private const string FixtureConst = "MYSTERY_OF_FIRE";
    private const string FixtureKey = "jeuno.mystery_of_fire";
    private const string FixtureFile = "QSL_Fixture";
    private const string FixtureLuaFile = "scripts/quests/jeuno/" + FixtureFile;
    private const string FixtureSourceFile = "tests/XI.Verify.QuestScripts.Live/Fixtures/QSL_Fixture.lua";
    private const string FixtureCsFile = "csharp:" + FixtureKey;
    private const string FixtureContainer = "Quest[3][33]";
    private const int FixtureQuestId = 33;
    private const string FixtureLuaIds = "xi.questLog.JEUNO, xi.quest.id.jeuno." + FixtureConst;
    // the hidden fixture (PlatformP9.cs RunP9Hidden): Fixtures/QSL_Hidden_Fixture.lua,
    // HiddenQuest:new('qslHiddenFixture')
    private const string HiddenFixtureName = "qslHiddenFixture";
    private const string HiddenFixtureKey = "hiddenQuests.qsl_hidden_fixture";
    private const string HiddenFixtureLuaFile = "scripts/quests/hiddenQuests/QSL_Hidden_Fixture";
    private const string HiddenFixtureSourceFile = "tests/XI.Verify.QuestScripts.Live/Fixtures/QSL_Hidden_Fixture.lua";
    private const string HiddenFixtureContainer = "HQuest[" + HiddenFixtureName + "]";
    // the Lua files every world lists beside scripts/'s (require name -> the file under the repo root)
    private static readonly IReadOnlyDictionary<string, string> FixtureContainerFiles = new Dictionary<string, string>
    {
        [FixtureLuaFile] = FixtureSourceFile,
        [HiddenFixtureLuaFile] = HiddenFixtureSourceFile,
    };
    // PlatformP10's other quest key (P10ForeignQuest, P10DoubleExportQuest): free as the fixture's is (TestKeysFree),
    // after the fixture's in key order (the registration's order for test quests with no LegacyPath)
    private const QuestLog P10OtherLog = QuestLog.Jeuno;
    private const string P10OtherConst = "YET_ANOTHER_TRIAL_IN_TANDEM";
    private const string P10OtherKey = "jeuno.yet_another_trial_in_tandem";
    private const string P10OtherContainer = "Quest[3][162]";    private const ushort FixtureZone = Zone.UpperJeuno;
    private const string FixtureZoneLua = "xi.zone.UPPER_JEUNO";
    private const string FixtureNpc = "Brutus";
    private const ushort FixtureTradeZone = Zone.CastleOztroja;
    private const string FixtureTradeNpc = "Tebhi";
    private const int FixtureTriggers = 3;
    private const string FixtureBegin = "141", FixtureFinish = "144";
    // the fixture's file named once per onTrigger entry, as Owners joins them
    private static string Entries(string file) => string.Join(";", Enumerable.Repeat(file, FixtureTriggers));
    // the C# quests XI.QuestContent declares, counted as the registration finds them (ZoneHost.DiscoverQuestScripts: every
    // class of the assembly that is a QuestScript and not abstract). Every one must register: a quest the registration
    // drops (it throws, its validation refuses it) makes the registered line's count smaller, and the checks fail
    private static readonly int RegisteredCount = typeof(XI.QuestContent.Zone).Assembly.GetTypes()
        .Count(t => t.IsClass && !t.IsAbstract && typeof(QuestScript).IsAssignableFrom(t));

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    // every char table (the new ones included), the chars row and the account
    private static void Cleanup(IDatabase database)
    {
        foreach (var id in CharIds) Cleanup(database, id);
    }

    private static void Cleanup(IDatabase database, uint id)
    {
        var tables = new List<string>();
        var r = Sql(database, "SELECT table_name AS t FROM information_schema.columns WHERE table_schema = DATABASE() AND column_name = 'charid' AND table_name <> 'chars'");
        while (r != null && r.Next()) tables.Add(r.Get<string>("t"));
        foreach (var t in tables) Sql(database, $"DELETE FROM `{t}` WHERE charid = ?", BoundValue.Of(id));
        Sql(database, "DELETE FROM chars WHERE charid = ?", BoundValue.Of(id));
        Sql(database, "DELETE FROM accounts WHERE id = ?", BoundValue.Of(id));
    }

    private static int Main(string[] argv)
    {
        // [<port>] [--db <name>]: the port (3307) and the database (xidb; a golden lane's copy, tools/golden_lane_db.sh, when
        // another worktree runs on xidb)
        var args = argv.ToList();
        string dbName = "xidb";
        int at = args.IndexOf("--db");
        if (at >= 0)
        {
            if (at + 1 >= args.Count || !System.Text.RegularExpressions.Regex.IsMatch(args[at + 1], "^[A-Za-z0-9_]{1,48}$"))
            {
                Console.WriteLine("--db takes a database name (letters, digits, _)");
                return 1;
            }
            dbName = args[at + 1];
            args.RemoveRange(at, 2);
        }
        int dbPort = args.Count > 0 ? int.Parse(args[0]) : 3307;
        var settings = new NetworkSettings { SqlHost = "127.0.0.1", SqlPort = dbPort, SqlLogin = "root", SqlPassword = "root", SqlDatabase = dbName };
        using var database = new MySqlDatabase(settings);
        try
        {
            var probe = database.Execute("SELECT 1", Array.Empty<BoundValue>());
            if (probe == null || !probe.Next()) { Console.WriteLine("DB not reachable"); return 3; }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DB not reachable ({ex.GetType().Name}: {ex.Message})");
            return 3;
        }

        try
        {
            Cleanup(database);
            Setup(database);
            RunConstants();
            RunRegistration();
            RunEntityNames();
            RunOrder();
            NewWorld();
            RunTestKeysFree();
            RunSwitches();
            Check("no script errors", "", string.Join(" | ", ScriptErrors()));
            Check("no C# quest handler failed", "", string.Join(" | ", log.Where(l => l.StartsWith("quests: ") && l.Contains(": ") &&
                                                                                      !IsRegistered(l) && !l.EndsWith("quest keys") &&
                                                                                      !l.Contains("has no Lua file") && !l.Contains("has no Lua fallback") &&
                                                                                      // CSHARP_QUESTS off names the call-only quests it left out (M6)
                                                                                      !l.StartsWith(M6Prefix) &&
                                                                                      // the shipped NPC scripts call quests the switches turned off
                                                                                      !(l.StartsWith("quests: npc call: zones/") && l.Contains(": unknown key ")))));
            RunRemoveContainer();
            RunFailures();
            RunRefusals();
            int mark = log.Count;
            RunCheckVarCache();
            RunEventOptions();
            RunReplyModifiers();
            RunFacetsLive();
            RunReloadQuest();
            RunNpcCalls();
            RunNpcCalls2();
            RunCallKinds();
            RunAnyStatusAndLocals();
            RunTriggerAreaReply();
            RunPlatformP1();
            RunPlatformP2();
            RunPlatformP4();
            RunPlatformP5();
            RunPlatformP6();
            RunPlatformP7();
            RunPlatformP8();
            RunPlatformP9();
            RunPlatformP10();
            RunWindurstGroup5();
            RunJeunoGroup15();
            RunPlatformP11();
            RunPlatformP11Watch();
            RunCarryHarness2();
            // quest rework C2 Task 3: the mission kind (C2.cs)
            RunC2();
            RunEngineInfra1();
            RunEngineSmall2();
            Check("no script errors in the last sections", "", string.Join(" | ", log.Skip(mark).Where(l => ScriptErrors().Contains(l))));
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FAIL exception: {ex}");
        }
        finally
        {
            Cleanup(database);
            scripts?.Dispose();
        }

        Console.WriteLine($"{_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    // ---- the generated constants (quest rework B3a) ---------------------------------------------------------------------

    // the C# name tools/gen_content_ids.py gives a Lua key
    private static string GenPascal(string key)
    {
        var name = string.Concat(key.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1).ToLowerInvariant()));
        return name.Length > 0 && char.IsDigit(name[0]) ? "_" + name : name;
    }

    private static void RunConstants()
    {
        Console.WriteLine("\n== constants: the generated ids equal the Lua values ==");
        string overridesFile = null;
        for (var d = new System.IO.DirectoryInfo(AppContext.BaseDirectory); d != null && overridesFile == null; d = d.Parent)
            if (System.IO.File.Exists(System.IO.Path.Combine(d.FullName, "tools", "content_id_overrides.txt"))) overridesFile = System.IO.Path.Combine(d.FullName, "tools", "content_id_overrides.txt");
        if (overridesFile == null) { Check("constants: tools/content_id_overrides.txt found", "True", "False"); return; }
        // (class, Lua key) -> C# name
        var overrides = new Dictionary<(string, string), string>();
        foreach (var raw in System.IO.File.ReadAllLines(overridesFile))
        {
            var parts = raw.Split('#')[0].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 3) overrides[(parts[0], parts[1])] = parts[2];
        }

        var mismatches = new List<string>();
        int checkedCount = 0;
        foreach (var (type, table) in new[] { (typeof(Zone), "zone"), (typeof(Item), "item"), (typeof(KeyItem), "keyItem"),
                                              (typeof(FameArea), "fameArea"), (typeof(Title), "title"), (typeof(Slot), "slot") })
        {
            // every integer in xi.<table>, as KEY=value
            var luaValues = new Dictionary<string, long>();
            foreach (var kv in Lua($"local t = {{}} for k, v in pairs(xi.{table}) do if type(v) == 'number' then t[#t + 1] = k .. '=' .. string.format('%d', v) end end report(table.concat(t, ';'))")
                         .Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = kv.LastIndexOf('=');
                luaValues[kv.Substring(0, eq)] = long.Parse(kv.Substring(eq + 1));
            }
            var byName = new Dictionary<string, long>();
            foreach (var kv in luaValues)
                byName[overrides.TryGetValue((type.Name, kv.Key), out var o) ? o : GenPascal(kv.Key)] = kv.Value;
            foreach (var f in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Where(f => f.IsLiteral))
            {
                checkedCount++;
                long want = Convert.ToInt64(f.GetRawConstantValue());
                if (!byName.TryGetValue(f.Name, out var got)) mismatches.Add($"{type.Name}.{f.Name}: no Lua key");
                else if (got != want) mismatches.Add($"{type.Name}.{f.Name}: {want} vs Lua {got}");
            }
        }
        Check("constants: every generated id equals its Lua value", "", string.Join(", ", mismatches.Take(20)));
        Check("constants:   ... and there are many", "True", (checkedCount > 15000).ToString());

        // QuestKey: KeyFor(area, CONST) is the constant, and xi.quest.id[area][CONST] exists
        var keyMismatches = new List<string>();
        int keys = 0;
        foreach (var area in typeof(QuestKey).GetNestedTypes())
        {
            string areaName = char.ToLowerInvariant(area.Name[0]) + area.Name.Substring(1);
            var luaConsts = Lua($"local t = {{}} for k, v in pairs(xi.quest.id.{areaName}) do t[#t + 1] = k end report(table.concat(t, ';'))")
                .Split(';', StringSplitOptions.RemoveEmptyEntries).ToDictionary(k => overrides.TryGetValue(("QuestKey", k), out var o) ? o : GenPascal(k));
            foreach (var f in area.GetFields().Where(f => f.IsLiteral))
            {
                keys++;
                if (!luaConsts.TryGetValue(f.Name, out var luaConst)) keyMismatches.Add($"{area.Name}.{f.Name}: no xi.quest.id key");
                else if (QuestKeyRegistry.KeyFor(areaName, luaConst) != (string)f.GetRawConstantValue()) keyMismatches.Add($"{area.Name}.{f.Name}: {f.GetRawConstantValue()}");
            }
        }
        Check("constants: every QuestKey is KeyFor(area, CONST) of an xi.quest.id entry", "", string.Join(", ", keyMismatches.Take(20)));
        Check("constants:   ... all 1084 of them", "1084", keys.ToString());

        // Text: zones[zone].text[NAME] is set once the zone's IDs table is loaded
        var texts = new List<(ushort Zone, string Name)>();
        foreach (var z in typeof(Text).GetNestedTypes())
            foreach (var f in z.GetFields().Where(f => f.IsStatic && f.FieldType == typeof(ZoneText)))
                texts.Add((((ZoneText)f.GetValue(null)).Zone, ((ZoneText)f.GetValue(null)).Name));
        var sb = new StringBuilder("local missing = {} for _, e in ipairs({");
        foreach (var (zone, name) in texts) sb.Append($"{{{zone},'{name}'}},");
        sb.Append("}) do local z = zones[e[1]] if not (z and type(z.text) == 'table' and z.text[e[2]] ~= nil) then missing[#missing + 1] = e[1] .. ':' .. e[2] end end report(table.concat(missing, ', '))");
        var missingText = Lua(sb.ToString());
        Check("constants: every Text constant is in its zone's IDs.lua text table", "", missingText.Length > 400 ? missingText.Substring(0, 400) : missingText);
        Check("constants:   ... and there are many", "True", (texts.Count > 5000).ToString());
    }

    // ---- the fixture -------------------------------------------------------------------------------------------------

    // a Hume Bastokan WAR 10 past its first login (NEW_ADVENTURER) at x / y / z in the zone, with the Bastok fame, 1000
    // gil and the items from inventory slot 1 on. Its mission log is a new char's (CCharEntity's, as on the C++) past
    // the two missions whose zone-ins play in the starting cities before any quest's. Rhapsodies of Vana'diel (RoV 1-1,
    // current by default: 30035 at a level 3 char's zone-in) is completed and Resonance (RoV 1-2, id 2) current, as
    // that event's finish leaves them. Rumors from the West (SoA 1-1, current by default) has its three optional
    // cutscenes seen (status bits 0 to 2: Bastok Markets' 22, Northern San d'Oria's 878, Windurst Woods' 839). Else the
    // first login played 30035 and the first relog 22, whatever a quest's zone-in returned
    private static void Fixture(uint charId, ushort zoneId, float x, float y, float z, int fame, params (int Item, uint Qty)[] items)
    {
        CharId = charId;
        var titles = new byte[143];
        titles[TitleNewAdventurer >> 3] |= 1 << (TitleNewAdventurer & 7);
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(charId), BoundValue.Of($"qscripts{charId % 1000}"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,titles) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(charId), BoundValue.Of(charId), BoundValue.Of(CharName(charId)), BoundValue.Of(zoneId), BoundValue.Of(zoneId),
            BoundValue.Of(x), BoundValue.Of(y), BoundValue.Of(z), BoundValue.Of((byte)1), BoundValue.Of((byte)0), BoundValue.Of(titles));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(charId), BoundValue.Of(charId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        var missions = new XI.Zone.CharData();
        missions.SetMissionComplete(XI.QuestContent.MissionLog.Rov, Mission.Rov.RhapsodiesOfVanadiel, true);
        missions.SetMissionCurrent(XI.QuestContent.MissionLog.Rov, Mission.Rov.Resonance);
        missions.SetMissionStatus(XI.QuestContent.MissionLog.Soa, 0b111);
        Sql(db, "UPDATE chars SET missions = ? WHERE charid = ?", BoundValue.Of(missions.MissionLog),
            BoundValue.Of(charId));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 300, 50, 1, 10)", BoundValue.Of(charId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 10)", BoundValue.Of(charId));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(charId));
        Sql(db, "REPLACE INTO char_profile(charid, fame_bastok) VALUES (?, ?)", BoundValue.Of(charId), BoundValue.Of(fame));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(charId));
        Sql(db, "INSERT IGNORE INTO char_unlocks(charid) VALUES (?)", BoundValue.Of(charId));
        Sql(db, "REPLACE INTO char_inventory(charid,location,slot,itemId,quantity) VALUES (?,0,0,65535,1000)", BoundValue.Of(charId));
        for (int i = 0; i < items.Length; i++)
            Sql(db, "REPLACE INTO char_inventory(charid,location,slot,itemId,quantity) VALUES (?,0,?,?,?)",
                BoundValue.Of(charId), BoundValue.Of((byte)(i + 1)), BoundValue.Of((ushort)items[i].Item), BoundValue.Of(items[i].Qty));
    }

    // the same length for every char, so a name in a packet sits at the same offsets in both runs
    private static string CharName(uint charId) => $"Qscript{charId % 1000}";

    // the fixture's WAR at another level (its HP filled up at the login)
    private static void Level(uint charId, int level)
    {
        Sql(db, "UPDATE char_stats SET mlvl = ?, hp = 3000 WHERE charid = ?", BoundValue.Of((byte)level), BoundValue.Of(charId));
        Sql(db, "UPDATE char_jobs SET war = ? WHERE charid = ?", BoundValue.Of((byte)level), BoundValue.Of(charId));
    }

    // ---- the twin runner ---------------------------------------------------------------------------------------------

    private static List<(string Label, string Record)> steps_;
    private static string run_;

    // InteractionGlobal's entry points keep what each call returned to the engine (type and value) until the next Step
    private static void InstallReturnRecorder()
    {
        lua.DoString(@"
            __verifyRets = {}
            local function pack(...) return { n = select('#', ...), ... } end
            for _, name in ipairs({ 'onTrigger', 'onTrade', 'onEventFinish', 'onEventUpdate', 'onZoneIn', 'onZoneOut',
                                    'afterZoneIn', 'onMobDeath', 'onTriggerAreaEnter', 'onTriggerAreaLeave' }) do
                local f = InteractionGlobal[name]
                InteractionGlobal[name] = function(...)
                    local r = pack(f(...))
                    local parts = {}
                    for i = 1, r.n do
                        local v = r[i]
                        parts[#parts + 1] = type(v) == 'table' and 'table' or (type(v) .. ':' .. tostring(v))
                    end
                    __verifyRets[#__verifyRets + 1] = name .. '=' .. table.concat(parts, '/')
                    return unpack(r, 1, r.n)
                end
            end", "=verify_returns");
    }

    private static string TakeReturns()
    {
        got.Clear();
        lua.DoString("report(table.concat(__verifyRets, ',')) __verifyRets = {}", "=verify_returns");
        return got.Count > 0 ? got[0] : "";
    }

    // which file the lookup's handlers for the container id come from, per handler list given as zone/second/third level
    private static string Owners(ushort zoneId, string second, string third, string containerId)
    {
        string thirdKey = int.TryParse(third, out _) ? third : $"'{third}'";
        return Lua($@"
            local seen = {{}}
            local z = InteractionGlobal.lookup.data[{zoneId}]
            local list = z and z['{second}'] and z['{second}'][{thirdKey}] or {{}}
            for _, e in ipairs(list) do
                if e.container and e.container.id == '{containerId}' then seen[#seen + 1] = tostring(e.container.filename) end
            end
            report(table.concat(seen, ';'))");
    }

    private static void Expect(string what, object expected, object actual) => Check($"{run_}: {what}", expected, actual);

    // the step's record: the packets the char got since the last client packet, chars.quests, the char's char_vars and
    // char_quest_state rows, and what InteractionGlobal returned to the engine
    private static void Step(string label)
    {
        var sb = new StringBuilder();
        Collect();
        sb.Append("packets=").Append(string.Join(" ", pending_.Select(Normalise)));
        pending_.Clear();
        sb.Append(" | quests=").Append(Convert.ToHexString(DbBlob("quests")));
        var vars = new List<string>();
        var r = Sql(db, "SELECT varname, value, expiry FROM char_vars WHERE charid = ? ORDER BY varname", BoundValue.Of(CharId));
        while (r != null && r.Next()) vars.Add($"{r.Get<string>("varname")}={r.Get<string>("value")}@{r.Get<string>("expiry")}");
        sb.Append(" | vars=").Append(string.Join(",", vars));
        var rows = new List<string>();
        r = Sql(db, "SELECT quest_key, area, quest_id, status, step, progress, repeat_by FROM char_quest_state WHERE charid = ? ORDER BY quest_key",
                BoundValue.Of(CharId));
        while (r != null && r.Next())
        {
            var cols = new List<string>();
            for (uint i = 0; i < r.ColumnCount(); i++) cols.Add(r.Get<string>(i) ?? "null");
            rows.Add(string.Join("|", cols));
        }
        sb.Append(" | rows=").Append(string.Join(",", rows));
        sb.Append(" | ret=").Append(TakeReturns());
        steps_.Add((label, sb.ToString()));
        // QUESTSCRIPTS_DUMP=1 prints every record
        if (Environment.GetEnvironmentVariable("QUESTSCRIPTS_DUMP") is { Length: > 0 }) Console.WriteLine($"  {run_} {label}: {sb}");
    }

    // a packet as hex, the char's own id, targid and name replaced by placeholders (they differ between the two chars)
    private static string Normalise(BasicPacket p)
    {
        var b = (byte[])p.Buffer.Clone();
        int size = (b[1] >> 1) * 4;
        if (size <= 0 || size > b.Length) size = b.Length;
        string hex = Convert.ToHexString(b, 0, size);
        hex = ReplaceAligned(hex, Convert.ToHexString(BitConverter.GetBytes(CharId)), "<CHARID>");
        hex = ReplaceAligned(hex, Convert.ToHexString(Encoding.ASCII.GetBytes(CharName(CharId))), "<CHARNAME>");
        return $"{Opcode(p):X3}:{hex}";
    }

    // replaces the hex pattern only where it starts on a byte boundary
    private static string ReplaceAligned(string hex, string pattern, string with)
    {
        var sb = new StringBuilder();
        int i = 0;
        while (i < hex.Length)
        {
            if (i % 2 == 0 && i + pattern.Length <= hex.Length && string.CompareOrdinal(hex, i, pattern, 0, pattern.Length) == 0)
            {
                sb.Append(with);
                i += pattern.Length;
            }
            else sb.Append(hex[i++]);
        }
        return sb.ToString();
    }

    // the container ids of a lookup list in its order ('default' for a zone's DefaultActions entry): which handler the
    // lookup tries first, and which one its cycling picks next
    private static string Order(ushort zoneId, string second, string third)
    {
        string thirdKey = int.TryParse(third, out _) ? third : $"'{third}'";
        return Lua($@"
            local ids = {{}}
            local z = InteractionGlobal.lookup.data[{zoneId}]
            local list = z and z['{second}'] and z['{second}'][{thirdKey}] or {{}}
            for _, e in ipairs(list) do ids[#ids + 1] = e.container and e.container.id or 'default' end
            report(table.concat(ids, ','))");
    }

    // ---- the container order ---------------------------------------------------------------------------------------

    // the containers list keeps the order of the Lua files' load whether the Lua files are there or not (Task 3 of B3a):
    // a C# quest takes its slot from its LegacyPath. The six B2 quests' Lua files are really gone (Task 6), so the list is
    // checked against the recorded order as it is (the fixtures' containers, which only these worlds list, left out);
    // ScriptEngine.HiddenContainerFiles is proved on a Lua file that exists (the first mission file) and on the fixture
    private static List<string> WithoutFixtures(IEnumerable<string> ids) =>
        ids.Where(id => id != FixtureContainer && id != HiddenFixtureContainer).ToList();

    private static void RunOrder()
    {
        Console.WriteLine("the container order");
        string goldenPath = Path.Combine(Path.GetDirectoryName(dataDir_), "tests", "golden", "container-order.txt");
        string golden = string.Join("\n", File.ReadAllLines(goldenPath));

        // a fresh world's list against the recorded order, with the six files gone, is XI.Verify.QuestGolden.Live's startup gate
        int mark = log.Count;
        NewWorld(ElvaanKey);
        Check("(c) OFF with no Lua file: logged", $"quests: {ElvaanKey} in CSHARP_QUESTS_OFF has no Lua file; it stays in C#",
              string.Join(" | ", log.Skip(mark).Where(l => l.Contains("has no Lua file"))));
        Check("  it is registered", ElvaanKey, Lua("report(tostring(CSharpQuests.live['Quest[1][13]']))"));
        Check("  the list is the same (the fixtures' containers left out)", golden,
              string.Join("\n", WithoutFixtures(zone.ContainerOrder(lua))));

        mark = log.Count;
        NewWorld(null, true);
        zone.TestQuestScripts.Add(new BrokenQuest());
        Lua("InteractionGlobal.reload(true)");
        Check("(d) a quest that fails with no Lua file: the no-fallback line",
              $"quests: C# quest {ElvaanKey} failed to load and has no Lua fallback: no NPC or mob named Nobody_Here in zone 235",
              string.Join(" | ", log.Skip(mark).Where(l => l.Contains("no Lua fallback"))));
        Check("  QuestLoadFailures has the key", ElvaanKey, string.Join(",", zone.QuestLoadFailures));
        Check("  no container has its id", "false", zone.ContainerOrder(lua).Contains("Quest[1][13]").ToString().ToLowerInvariant());
        zone.TestQuestScripts.Clear();

        // (e) is what proves ScriptEngine.HiddenContainerFiles: a Lua file that exists, hidden: its container is not in the
        // list, the rest keep their order (the first mission file, a Lua container for long); the Lua fixture the same
        NewWorld();
        string root = Path.GetDirectoryName(dataDir_);
        string mission = Directory.EnumerateFiles(Path.Combine(root, "scripts", "missions"), "*.lua", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Where(f => f.Count(c => c == '/') == 3 && !f.EndsWith("/helpers.lua", StringComparison.Ordinal))
            .OrderBy(f => f, Comparer<string>.Create(XI.Script.ScriptEngine.PathCompare))
            .Select(f => f.Substring(0, f.Length - 4)).First();
        string missionId = Lua($"report(tostring(package.loaded['{mission}'] and package.loaded['{mission}'].id))");
        Check($"(e) {mission}: a Lua container of the recorded list", "True", golden.Split('\n').Contains(missionId).ToString());
        var full = zone.ContainerOrder(lua);
        NewWorld(new[] { mission }, false);
        Check("  hidden: its container leaves the list, the rest keep their order",
              string.Join("\n", full.Where(id => id != missionId)), string.Join("\n", zone.ContainerOrder(lua)));
        NewWorld(new[] { FixtureLuaFile }, false);
        Check("(e) the Lua fixture hidden: its container leaves the list, the rest keep their order",
              string.Join("\n", full.Where(id => id != FixtureContainer)), string.Join("\n", zone.ContainerOrder(lua)));
        NewWorld();
    }

    // ---- the test keys --------------------------------------------------------------------------------------------

    // the test-only keys (the fixtures', PlatformP10's other one) belong to no quest: no C# quest of XI.QuestContent
    // has one, no Lua file or Lua draft (*.lua.todo) under scripts/ or modules/ names one, and the containers list
    // holds the fixtures' own once and the other key's none. A quest ported, drafted or written on one of them fails
    // here, before the checks it would quietly change (a second C# quest of a key is refused at registration, a second
    // Lua container doubles the lookup's entries)
    private static void RunTestKeysFree()
    {
        Console.WriteLine("the test keys");
        bool Taken(QuestScript s) =>
            (s.Log == FixtureLog && s.ConstName == FixtureConst) ||
            (s.Log == P10OtherLog && s.ConstName == P10OtherConst) ||
            s.HiddenName == HiddenFixtureName ||
            (s.HiddenName != null && s.LegacyPath != null && QuestScript.HiddenKey(s.LegacyPath) == HiddenFixtureKey);
        var taken = typeof(XI.QuestContent.Zone).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(QuestScript).IsAssignableFrom(t) &&
                        t.GetConstructor(Type.EmptyTypes) != null)
            .Select(t => (QuestScript)Activator.CreateInstance(t))
            .Where(Taken).Select(s => s.GetType().Name).ToList();
        Check("test keys: no C# quest has a fixture's key or P10's other key", "", string.Join(",", taken));
        string root = Path.GetDirectoryName(dataDir_);
        string[] names = { $"xi.quest.id.jeuno.{FixtureConst}", $"xi.quest.id.jeuno.{P10OtherConst}", $"'{HiddenFixtureName}'",
                           $"\"{HiddenFixtureName}\"" };
        var luaFiles = new[] { "scripts", "modules" }
            .SelectMany(d => new[] { "*.lua", "*.lua.todo" }
                .SelectMany(p => Directory.EnumerateFiles(Path.Combine(root, d), p, SearchOption.AllDirectories)))
            .Where(f => { string s = File.ReadAllText(f); return names.Any(s.Contains); })
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/')).ToList();
        Check("test keys: no Lua file or Lua draft under scripts/ or modules/ names one", "",
              string.Join(",", luaFiles));
        var order = zone.ContainerOrder(lua).ToList();
        Check("test keys: the containers list holds each fixture's container once, P10's other key's none", "1|1|0",
              $"{order.Count(id => id == FixtureContainer)}|{order.Count(id => id == HiddenFixtureContainer)}|" +
              $"{order.Count(id => id == P10OtherContainer)}");
        Check("  the fixtures' containers are their files' (Lua quests, not C#)", $"{FixtureLuaFile}|{HiddenFixtureLuaFile}",
              Lua($"report(tostring(InteractionGlobal.lookup.containers['{FixtureContainer}'] and package.loaded['{FixtureLuaFile}'].filename) .. '|' .. " +
                  $"tostring(package.loaded['{HiddenFixtureLuaFile}'] and package.loaded['{HiddenFixtureLuaFile}'].filename))"));
    }

    // ---- the registration ------------------------------------------------------------------------------------------

    // the registered line (quests: <n> C# quests registered in <ms> ms) and the same without its time, compared by count
    private static readonly System.Text.RegularExpressions.Regex RegisteredTime = new(@"^(quests: \d+ C# quests registered) in \d+ ms$");
    private static bool IsRegistered(string l) => l != null && RegisteredTime.IsMatch(l);
    private static string Untimed(string l) => l == null ? null : RegisteredTime.Replace(l, "$1");

    private static void RunRegistration()
    {
        Console.WriteLine("the registration");
        Check($"XI.QuestContent declares C# quests ({RegisteredCount})", true, RegisteredCount > 0);
        Check("the host registered the C# quests", "True", log.Select(Untimed).Contains($"quests: {RegisteredCount} C# quests registered"));
        Check("no quest refused", "", string.Join(" | ", log.Where(l => l.Contains("not registered"))));
    }

    // ---- the registration's entity names (B3b start, Task 4) ------------------------------------------------------------

    // the registration's entity check reads each zone's names from the script engine's ID scan (ScriptEngine.EntityNamesOf),
    // not from a second yaml parse: for every zone in zone_settings, the scan's NPC names and its placed and unplaced spawn
    // names equal the yaml parse's (NpcEntity.Load, whatever content; MobsFile.Load's spawns and their Placed), and the
    // host's name set is the NPCs and the placed spawns
    private static void RunEntityNames()
    {
        Console.WriteLine("the registration's entity names");
        var enums = new XI.Zone.DataEnums(dataDir_);
        var zones = new List<(ushort Id, string Name)>();
        var r = Sql(db, "SELECT zoneid, name FROM zone_settings ORDER BY zoneid");
        while (r != null && r.Next()) zones.Add((r.Get<ushort>("zoneid"), r.Get<string>("name")));
        int fromScan = 0, compared = 0, npcs = 0, placed = 0, unplaced = 0;
        var diffs = new List<string>();
        string Diff(string what, ushort id, HashSet<string> want, HashSet<string> got) =>
            want.SetEquals(got) ? null
                : $"zone {id} {what}: only the yaml parse [{string.Join(",", want.Except(got).Take(5))}], only the scan [{string.Join(",", got.Except(want).Take(5))}]";
        foreach (var (id, name) in zones)
        {
            string dir = Path.Combine(dataDir_, "zones", XI.Zone.ZoneInfo.FolderName(name));
            if (scripts.HasEntityNames(dir)) fromScan++;
            var oldNpcs = new HashSet<string>(StringComparer.Ordinal);
            var oldPlaced = new HashSet<string>(StringComparer.Ordinal);
            var oldUnplaced = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                foreach (var n in XI.Zone.NpcEntity.Load(Path.Combine(dir, "npcs.yaml"), enums, _ => true)) oldNpcs.Add(n.Name);
                foreach (var m in XI.Zone.MobsFile.Load(Path.Combine(dir, "mobs.yaml"), enums).Spawns) (m.Placed ? oldPlaced : oldUnplaced).Add(m.Script);
            }
            catch (Exception e)
            {
                diffs.Add($"zone {id}: the yaml parse threw {e.Message}");
                continue;
            }
            var scan = scripts.EntityNamesOf(dir);
            var host = zone.ZoneEntityNames(id);
            var oldNames = new HashSet<string>(oldNpcs.Concat(oldPlaced), StringComparer.Ordinal);
            foreach (var d in new[]
                     {
                         Diff("npcs", id, oldNpcs, scan.Npcs), Diff("placed spawns", id, oldPlaced, scan.PlacedMobs),
                         Diff("unplaced spawns", id, oldUnplaced, scan.UnplacedMobs),
                         host == null ? $"zone {id}: the host has no names" : Diff("the host's names", id, oldNames, host),
                     })
                if (d != null) diffs.Add(d);
            compared++;
            npcs += oldNpcs.Count;
            placed += oldPlaced.Count;
            unplaced += oldUnplaced.Count;
        }
        Console.WriteLine($"  {compared} of {zones.Count} zones compared ({fromScan} from the ID scan): {npcs} NPC names, {placed} placed and " +
                          $"{unplaced} unplaced spawn names");
        Check("entity names: every zone's scan equals its yaml parse", "", string.Join(" | ", diffs.Take(10)) + (diffs.Count > 10 ? $" (+{diffs.Count - 10})" : ""));
        Check("entity names: every zone compared", zones.Count, compared);
        Check("entity names: a zone zone_settings does not have has none", "True", zone.ZoneEntityNames(65000) == null);

        // the Python yaml reader (tools/gen_content_ids.py yaml_entity_names, a port of ScanEntityScripts) against the
        // C# one on the real data: every Npc.<Zone> constant is a script file's name (scripts/zones/<zone_settings
        // name>/npcs or mobs) or one of the scan's names (data/zones/<folder>), so a yaml name the two read apart (a
        // quote, an escape, a field the port drops) fails here
        string scriptsZones = Path.Combine(Directory.GetParent(dataDir_).FullName, "scripts", "zones");
        var byName = zones.ToDictionary(z => z.Id, z => z.Name);
        var strays = new List<string>();
        int constants = 0;
        foreach (var cls in typeof(XI.QuestContent.Npc).GetNestedTypes().OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            if (typeof(XI.QuestContent.Zone).GetField(cls.Name)?.GetValue(null) is not ushort zid ||
                !byName.ContainsKey(zid))
            {
                strays.Add($"Npc.{cls.Name}: no zone of that name in zone_settings");
                continue;
            }
            string folder = XI.Zone.ZoneInfo.FolderName(byName[zid]);
            var scan = scripts.EntityNamesOf(Path.Combine(dataDir_, "zones", folder));
            foreach (var f in cls.GetFields().Where(f => f.IsLiteral))
            {
                string value = (string)f.GetRawConstantValue();
                constants++;
                bool file = new[] { "npcs", "mobs" }
                    .Any(s => File.Exists(Path.Combine(scriptsZones, byName[zid], s, value + ".lua")));
                bool scanned = scan.Npcs.Contains(value) || scan.PlacedMobs.Contains(value) ||
                               scan.UnplacedMobs.Contains(value);
                if (!file && !scanned) strays.Add($"Npc.{cls.Name}.{f.Name} = {value}");
            }
        }
        Console.WriteLine($"  {constants} Npc constants compared");
        Check("entity names: every Npc constant is a script file's or the C# scan's name (the Python yaml reader " +
              "agrees)", "",
              string.Join(" | ", strays.Take(10)) + (strays.Count > 10 ? $" (+{strays.Count - 10})" : ""));
    }

    // ---- the switches and InteractionGlobal.reload ---------------------------------------------------------------------

    // the switches run on the Lua fixture (Fixtures/QSL_Fixture.lua), a Lua quest file, through a test-only C# quest
    // of its key; The Elvaan Goldsmith, whose Lua file is gone, gets the one OFF check that fits it. Brutus's onTrigger
    // has FixtureTriggers entries of the fixture (one per section that talks to him), in Lua and in the test quest
    // alike
    private static void RunSwitches()
    {
        Console.WriteLine("the switches");
        CharId = 0;
        string Owner(string third) => third == "onTrade"
            ? Owners(FixtureTradeZone, FixtureTradeNpc, third, FixtureContainer)
            : Owners(FixtureZone, FixtureNpc, third, FixtureContainer);
        string Finish(string csid) => Owners(FixtureZone, "onEventFinish", csid, FixtureContainer);

        // the fixture's Lua file is in charge until a C# quest of its key is added
        Check("a Lua quest: its file's handlers", $"{Entries(FixtureLuaFile)}/{FixtureLuaFile}/{FixtureLuaFile}",
              $"{Owner("onTrigger")}/{Owner("onTrade")}/{Finish(FixtureFinish)}");
        zone.TestQuestScripts.Add(new FixtureSwitchQuest());
        int before = log.Count(l => Untimed(l) == $"quests: {RegisteredCount + 1} C# quests registered");
        Lua("InteractionGlobal.reload(true)");
        Check("InteractionGlobal.reload(true) registers it", before + 1, log.Count(l => Untimed(l) == $"quests: {RegisteredCount + 1} C# quests registered"));
        Check($"  the lookup's {FixtureNpc} onTrigger / {FixtureTradeNpc} onTrade are the bridge's",
              $"{Entries(FixtureCsFile)}/{FixtureCsFile}", $"{Owner("onTrigger")}/{Owner("onTrade")}");
        Check($"  onEventFinish {FixtureBegin} / {FixtureFinish} are the bridge's", $"{FixtureCsFile}/{FixtureCsFile}",
              $"{Finish(FixtureBegin)}/{Finish(FixtureFinish)}");
        Check("  lookup.containers holds the id", "true", Lua($"report(tostring(InteractionGlobal.lookup.containers['{FixtureContainer}']))"));
        Lua("InteractionGlobal.reload()");
        Check("InteractionGlobal.reload() keeps it", Entries(FixtureCsFile), Owner("onTrigger"));

        zone.CSharpQuestsOff.Add(FixtureKey);
        Lua("InteractionGlobal.reload(true)");
        Check("CSHARP_QUESTS_OFF naming it: the others registered", $"quests: {RegisteredCount} C# quests registered", Untimed(log.Last(IsRegistered)));
        Check("  the Lua file's handlers are back", $"{Entries(FixtureLuaFile)}/{FixtureLuaFile}/{FixtureLuaFile}",
              $"{Owner("onTrigger")}/{Owner("onTrade")}/{Finish(FixtureFinish)}");
        zone.CSharpQuestsOff.Remove(FixtureKey);

        zone.CSharpQuestsEnabled = false;
        Lua("InteractionGlobal.reload(true)");
        Check("CSHARP_QUESTS off: none registered, the Lua file in charge", $"quests: 0 C# quests registered/{Entries(FixtureLuaFile)}",
              $"{Untimed(log.Last(IsRegistered))}/{Owner("onTrigger")}");
        Check("  the call-only quests it left out are named (M6)", "True", log.Any(l => l.StartsWith(M6Prefix) && l.Contains(QuestKey.Sandoria.ThickShells)).ToString());
        zone.CSharpQuestsEnabled = true;
        Lua("InteractionGlobal.reload(true)");
        Check("CSHARP_QUESTS back on: the bridge's again", $"quests: {RegisteredCount + 1} C# quests registered/{Entries(FixtureCsFile)}",
              $"{Untimed(log.Last(IsRegistered))}/{Owner("onTrigger")}");
        zone.TestQuestScripts.Clear();
        Lua("InteractionGlobal.reload(true)");
        Check("the test quest removed: the Lua file in charge again", Entries(FixtureLuaFile), Owner("onTrigger"));

        // a quest whose Lua file is gone: OFF is logged, it stays in C#
        int mark = log.Count;
        zone.CSharpQuestsOff.Add(ElvaanKey);
        Lua("InteractionGlobal.reload(true)");
        Check("CSHARP_QUESTS_OFF naming a quest with no Lua file: logged, it stays in C#",
              $"quests: {ElvaanKey} in CSHARP_QUESTS_OFF has no Lua file; it stays in C#/{RegisteredCount}",
              $"{string.Join(" | ", log.Skip(mark).Where(l => l.Contains("has no Lua file")))}/{Untimed(log.Last(IsRegistered)).Split(' ')[1]}");
        Check("  its handlers are the bridge's", $"{ElvaanCsFile};{ElvaanCsFile}", Owners(Zone.BastokMarkets, "Michea", "onTrigger", "Quest[1][13]"));
        zone.CSharpQuestsOff.Remove(ElvaanKey);
        Lua("InteractionGlobal.reload(true)");

        // a definition naming an NPC Bastok Markets does not have
        var problems = zone.ValidateQuestScript(new BrokenQuest().Compile());
        Check("a broken definition is refused with its reason", "no NPC or mob named Nobody_Here in zone 235", string.Join(" | ", problems));
        Check("the real one validates clean", "", string.Join(" | ", zone.ValidateQuestScript(new XI.QuestContent.Bastok.TheElvaanGoldsmith().Compile())));
    }

    // InteractionLookup:removeContainer takes out every entry of the container, adjacent ones included (this guards removal
    // of adjacent entries; which entries sit together follows pairs() order)
    private static void RunRemoveContainer()
    {
        Console.WriteLine("removeContainer");
        Check("two adjacent entries of one container are both removed", "2/0", Lua(@"
            local lk = InteractionLookup:new()
            local c = { id = 'RemoveProbe', sections = {
                { check = function() return true end, [235] = { RemoveProbeNpc = function() end } },
                { check = function() return true end, [235] = { RemoveProbeNpc = function() end } },
            } }
            lk:addContainer(c)
            local before = #lk.data[235].RemoveProbeNpc.onTrigger
            lk:removeContainer(c)
            report(before .. '/' .. #lk.data[235].RemoveProbeNpc.onTrigger)"));
    }

    // a C# handler or check that throws: logged with its quest and handler, nothing (or false) returned to Lua, and the
    // Lua stack as it was (a closure that left values behind would return them)
    private static void RunFailures()
    {
        Console.WriteLine("failing handlers");
        int mark = log.Count;
        string got = Lua(@"
            for _, e in ipairs(InteractionGlobal.lookup.data[235]['Michea']['onTrade']) do
                if e.container and e.container.id == 'Quest[1][13]' then
                    -- no trade: npcUtil.tradeMatches(nil, ...) raises
                    report(select('#', e.handler(nil, nil, nil)))
                    -- completed: the guard reads the fame of a player there is not
                    local n = select('#', e.check(nil, xi.questStatus.QUEST_COMPLETED))
                    report(n .. '/' .. tostring(e.check(nil, xi.questStatus.QUEST_COMPLETED)))
                    -- accepted: matched before the guard
                    report(tostring(e.check(nil, xi.questStatus.QUEST_ACCEPTED)))
                end
            end");
        Check("a throwing handler returns nothing; a throwing check returns false; a plain check true", "0,1/false,true", got);
        var lines = log.Skip(mark).Where(l => l.StartsWith("quests: ")).Select(l => l.Split(':')[0] + ":" + l.Split(':')[1]).ToList();
        Check("  each logged as quests: <key> <handler>: <message>",
              $"quests: {ElvaanKey} Michea onTrade,quests: {ElvaanKey} check 2,quests: {ElvaanKey} check 2", string.Join(",", lines));
        Check("  the check's message", $"quests: {ElvaanKey} check 2: no player in this handler",
              log.Skip(mark).FirstOrDefault(l => l.Contains("check 2")));
        Check("  the Lua stack is balanced", "0", lua.Top.ToString());
    }

    // through the real registration: a test-only quest naming an NPC Bastok Markets does not have is refused with its
    // reason and its Lua file stays in charge; a key in CSHARP_QUESTS_OFF that no C# quest has is reported
    private static void RunRefusals()
    {
        Console.WriteLine("refusals");
        int mark = log.Count;
        zone.TestQuestScripts.Add(new BrokenFixtureQuest());
        Lua("InteractionGlobal.reload(true)");
        var lines = log.Skip(mark).Where(l => l.StartsWith("quests: ")).ToList();
        Check("an unknown NPC refuses the quest at registration", $"quests: {FixtureKey} not registered: no NPC or mob named Nobody_Here in zone 235",
              string.Join(" | ", lines.Where(l => l.Contains(FixtureKey))));
        Check("  the others registered", $"quests: {RegisteredCount} C# quests registered", Untimed(lines.LastOrDefault()));
        Check("  its Lua file stays in charge", Entries(FixtureLuaFile),
              Owners(FixtureZone, FixtureNpc, "onTrigger", FixtureContainer));
        zone.TestQuestScripts.Clear();

        // a mobs.yaml name with no placed spawn is no mob a quest can hang a handler on
        mark = log.Count;
        zone.TestQuestScripts.Add(new UnplacedMobQuest());
        Lua("InteractionGlobal.reload(true)");
        Check("a mob with no placed spawn refuses the quest at registration",
              $"quests: {FixtureKey} not registered: no NPC or mob named Mighrah in zone {Zone.AbysseaMisareaux}",
              string.Join(" | ", log.Skip(mark).Where(l => l.Contains(FixtureKey))));
        zone.TestQuestScripts.Clear();

        // a quest whose registration throws before its build (here its key: no constant name) is logged and left out;
        // the others are registered, and loadContainers goes on
        mark = log.Count;
        zone.TestQuestScripts.Add(new ThrowingQuest());
        Lua("InteractionGlobal.reload(true)");
        lines = log.Skip(mark).Where(l => l.StartsWith("quests: ")).ToList();
        Check("a quest whose registration throws is logged with its type and left out",
              $"quests: {typeof(ThrowingQuest).FullName} not registered: Object reference not set to an instance of an object.",
              string.Join(" | ", lines.Where(l => l.Contains(nameof(ThrowingQuest)))));
        Check("  the others registered", $"quests: {RegisteredCount} C# quests registered", Untimed(lines.LastOrDefault()));
        Check("  the containers loaded (Michea's handlers there)", $"{ElvaanCsFile};{ElvaanCsFile}",
              Owners(Zone.BastokMarkets, "Michea", "onTrigger", "Quest[1][13]"));
        zone.TestQuestScripts.Clear();

        mark = log.Count;
        zone.CSharpQuestsOff.Add("bastok.no_such_quest");
        Lua("InteractionGlobal.reload(true)");
        Check("an unknown key in CSHARP_QUESTS_OFF is reported", "quests: bastok.no_such_quest in CSHARP_QUESTS_OFF is not a C# quest",
              string.Join(" | ", log.Skip(mark).Where(l => l.Contains("CSHARP_QUESTS_OFF"))));
        zone.CSharpQuestsOff.Remove("bastok.no_such_quest");
        Lua("InteractionGlobal.reload(true)");
        Check("  the C# quest still registered", $"quests: {RegisteredCount} C# quests registered/{ElvaanCsFile};{ElvaanCsFile}",
              $"{Untimed(log.Last(IsRegistered))}/{Owners(Zone.BastokMarkets, "Michea", "onTrigger", "Quest[1][13]")}");
    }

    // a check reads the quest's vars through the lookup's per-dispatch cache: one onEventFinish dispatch over two states,
    // A (Prog 0) whose handler sets Prog 1 and B (a guard wanting Prog 1), runs A only, as the same quest written in Lua
    // does. Both containers are dispatched alike, each alone in a new InteractionLookup.
    private static void RunCheckVarCache()
    {
        Console.WriteLine("checks read the vars through the dispatch's cache");
        zone.TestQuestScripts.Add(new CacheProbeQuest());
        Lua("InteractionGlobal.reload(true)");
        uint id = CharIds[0];
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        Lua($"player:addQuest({FixtureLuaIds})");
        const string prepare = @"
            local q = Quest:new(" + FixtureLuaIds + @")
            q.sections =
            {
                {
                    check = function(player, status, vars)
                        return status == xi.questStatus.QUEST_ACCEPTED and vars.Prog == 0
                    end,
                    [xi.zone.BASTOK_MARKETS] = { onEventFinish = { [9999] = function(player, csid, option, npc) q:setVar(player, 'Prog', 1) end } },
                },
                {
                    check = function(player, status, vars)
                        return status == xi.questStatus.QUEST_ACCEPTED and vars.Prog == 1
                    end,
                    [xi.zone.BASTOK_MARKETS] = { onEventFinish = { [9999] = function(player, csid, option, npc) q:setVar(player, 'B', 1) end } },
                },
            }
            __verifyLuaProbe = q
            for _, e in ipairs(InteractionGlobal.lookup.data[xi.zone.BASTOK_MARKETS].onEventFinish[9999] or {}) do
                if e.container and e.container.id == '" + FixtureContainer + @"' then __verifyCsProbe = e.container end
            end";
        Lua(prepare);
        string Dispatch(string container) => Lua($@"
            player:setCharVar('{FixtureContainer}Prog', 0)
            player:setCharVar('{FixtureContainer}B', 0)
            local lookup = InteractionLookup:new()
            lookup:addContainer({container})
            lookup:onEventFinish(player, 9999, 0, nil)
            report(tostring({container}.filename) .. ':' .. player:getCharVar('{FixtureContainer}Prog') .. '/' .. player:getCharVar('{FixtureContainer}B'))");
        Check("the Lua quest: A ran (Prog 1), B's check saw Prog 0 (B not run)", "nil:1/0", Dispatch("__verifyLuaProbe"));
        Check("the C# quest: the same", $"{FixtureCsFile}:1/0", Dispatch("__verifyCsProbe"));
        Logout();
        Cleanup(db, id);
        zone.TestQuestScripts.Clear();
        Lua("InteractionGlobal.reload(true)");
    }

    // an event reply's Options reach player:startEvent as the table a Lua quest writes: Event(n).Option(2, 7) puts 7 in
    // the event's parameter 2 (nothing in a proof quest uses Options yet)
    private static void RunEventOptions()
    {
        Console.WriteLine("event options");
        zone.TestQuestScripts.Add(new OptionsProbeQuest());
        Lua("InteractionGlobal.reload(true)");
        uint id = CharIds[0];
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        uint michea = TalkTo("Samia");
        Check("Samia's event is the probe's 9998, from the reply", $"{michea}:9998", EvId());
        Check("  its parameter 2 is the option's 7, parameter 0 the positional 5", "7/5",
              $"{c.CurrentEvent.Params.GetValueOrDefault(2)}/{c.CurrentEvent.Params.GetValueOrDefault(0)}");
        Logout();
        Cleanup(db, id);
        zone.TestQuestScripts.Clear();
        Lua("InteractionGlobal.reload(true)");
    }

    // the facets on the engine: a check that calls an effect throws and counts as false (logged); a sequence with Face and
    // Wait steps renders and sends its messages; a ZoneText reply resolves in its own zone's table (Metalworks' id from a
    // Bastok Markets handler)
    private static void RunFacetsLive()
    {
        Console.WriteLine("facets");
        uint id = CharIds[0];

        void Probe(QuestScript quest, Action body)
        {
            zone.TestQuestScripts.Add(quest);
            Lua("InteractionGlobal.reload(true)");
            Cleanup(db, id);
            Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
            Login();
            body();
            Logout();
            Cleanup(db, id);
            zone.TestQuestScripts.Clear();
            Lua("InteractionGlobal.reload(true)");
        }

        // the message ids sent so far: 0x02A (messageSpecial) at 26, 0x036 (messageText) at 10
        List<int> Said(int opcode)
        {
            Collect();
            return pending_.Where(p => Opcode(p) == opcode).Select(p => BitConverter.ToUInt16(p.Buffer, opcode == 0x02A ? 26 : 10) & 0x7FFF).ToList();
        }

        int mark = log.Count;
        Probe(new EffectCheckQuest(), () =>
        {
            TalkTo("Samia");
            Check("a check that adds fame counts as false: the state's event 9998 is not started", "False", EvId().EndsWith(":9998").ToString());
            Check("  the log says why", $"quests: {FixtureKey} check 1: a check cannot add fame",
                  string.Join(" | ", log.Skip(mark).Where(l => l.Contains("a check cannot"))));
        });

        Probe(new SequenceProbeQuest(), () =>
        {
            Collect();
            TalkTo("Samia");
            for (int i = 0; i < 4; i++) Tick(500);
            var said = Said(0x036);
            int first = said.IndexOf(TextId(Zone.BastokMarkets, "ITEM_OBTAINED")), after = said.IndexOf(TextId(Zone.BastokMarkets, "GIL_OBTAINED"));
            Check("a sequence of text, Face, Wait, text sends both messages, in order", "True", (first >= 0 && after > first).ToString());
        });

        Probe(new OtherZoneTextQuest(), () =>
        {
            Collect();
            TalkTo("Samia");
            var said = Said(0x02A);
            Check("a ZoneText reply resolves in its own zone: Metalworks' YOU_ACCEPT_THE_MISSION (9), not Bastok Markets' (6532)",
                  "True/False", $"{said.Contains(9)}/{said.Contains(6532)}");
        });

        // a top-level Face is refused when the quest registers, as is a sequence of steps with no text
        mark = log.Count;
        zone.TestQuestScripts.Add(new TopLevelFaceQuest());
        Lua("InteractionGlobal.reload(true)");
        Check("a fixed top-level Face refuses the quest", $"quests: {FixtureKey} not registered: Samia OnTrigger: a Face reply is a sequence step only",
              string.Join(" | ", log.Skip(mark).Where(l => l.Contains(FixtureKey))));
        zone.TestQuestScripts.Clear();
        mark = log.Count;
        zone.TestQuestScripts.Add(new FaceOnlySequenceQuest());
        Lua("InteractionGlobal.reload(true)");
        Check("a sequence with no text step refuses the quest", $"quests: {FixtureKey} not registered: Samia OnTrigger: a sequence with no message step",
              string.Join(" | ", log.Skip(mark).Where(l => l.Contains(FixtureKey))));
        zone.TestQuestScripts.Clear();

        // a returned top-level Wait is logged and treated as no reply
        mark = log.Count;
        Probe(new ReturnedWaitQuest(), () =>
        {
            TalkTo("Samia");
            Check("a returned top-level Wait is logged and starts no event of the quest", "False/1",
                  $"{EvId().EndsWith(":9998")}/{log.Skip(mark).Count(l => l.Contains("a Wait reply is a sequence step only"))}");
        });

        // a name only a dynamic entity has (Quasilumin: insertDynamicEntity in Grand_Palace_of_HuXzoi's Cermet_Alcove.lua)
        mark = log.Count;
        zone.TestQuestScripts.Add(new DynamicEntityQuest());
        Lua("InteractionGlobal.reload(true)");
        Check("a dynamic-entity-only mob name is accepted", "",
              string.Join(" | ", log.Skip(mark).Where(l => l.Contains(FixtureKey) && l.Contains("not registered"))));
        Check("  and the quest registered", $"quests: {RegisteredCount + 1} C# quests registered", Untimed(log.Skip(mark).LastOrDefault(IsRegistered)));
        zone.TestQuestScripts.Clear();
        Lua("InteractionGlobal.reload(true)");
    }

    private static int TextId(ushort zoneId, string name) => int.Parse(Lua($"report(tostring(zones[{zoneId}].text.{name}))"));

    // !reloadquest: a quest that runs in C# is refused with a message (its handlers stay the bridge's), whether its Lua file
    // exists (the Lua fixture with a test-only C# quest) or is gone (The Elvaan Goldsmith, found by CSharpQuests.legacy); a Lua
    // quest is reloaded from its file as before
    private static void RunReloadQuest()
    {
        Console.WriteLine("!reloadquest");
        // the command looks for scripts/quests/<area>/<name>.lua on disk: the fixture's file is found where its require
        // name says (io.open mapped for the call), as a Lua quest file of scripts/quests would be
        string Run(string quest) => Lua($@"
            local cmd = dofile('scripts/commands/reloadquest.lua')
            local said = {{}}
            local open = io.open
            io.open = function(path, mode)
                if path == '{FixtureLuaFile}.lua' then path = '{FixtureSourceFile}' end
                return open(path, mode)
            end
            local ok, err = pcall(cmd.onTrigger, {{ printToPlayer = function(self, msg) said[#said + 1] = msg end }}, '{quest}')
            io.open = open
            if not ok then error(err) end
            report(table.concat(said, ' | '))");
        // the fixture's Lua file exists: a C# test quest of its key takes its slot
        zone.TestQuestScripts.Add(new FixtureSwitchQuest());
        Lua("InteractionGlobal.reload(true)");
        Check("a quest in C#: refused, the GM told how to reload it",
              $"Quest '{FixtureFile}' runs in C# ({FixtureKey}); !reloadinteraction 1 (InteractionGlobal.reload(true)) reloads it.",
              Run(FixtureFile));
        Check("  its handlers are still the bridge's", Entries(FixtureCsFile),
              Owners(FixtureZone, FixtureNpc, "onTrigger", FixtureContainer));
        zone.TestQuestScripts.Clear();
        Lua("InteractionGlobal.reload(true)");
        // a quest whose Lua file is gone has no file for the command to find
        Check("a quest in C# with no Lua file: refused too (found by its LegacyPath)",
              $"Quest 'The_Elvaan_Goldsmith' runs in C# ({ElvaanKey}); !reloadinteraction 1 (InteractionGlobal.reload(true)) reloads it.",
              Run("The_Elvaan_Goldsmith"));
        Check("  a name that is no quest: not found", "Unable to find quest 'No_Such_Quest' to reload. | !reloadquest <lua-file-name>",
              Run("No_Such_Quest"));
        Check("a Lua quest: reloaded as before", $"Quest '{FixtureFile}' at '{FixtureLuaFile}' has been reloaded.", Run(FixtureFile));
        // the reloaded container has no filename (reloadquest never set one), so it is matched by identity
        Check("  its handlers are the reloaded file's", "true", Lua(@"
            local file = package.loaded['" + FixtureLuaFile + @"']
            local all, n = true, 0
            for _, e in ipairs(InteractionGlobal.lookup.data[" + FixtureZoneLua + @"]
                    ['" + FixtureNpc + @"'].onTrigger) do
                if e.container and e.container.id == '" + FixtureContainer + @"' then
                    n = n + 1
                    if not rawequal(e.container, file) then all = false end
                end
            end
            report(tostring(all and n > 0))"));
        Lua("InteractionGlobal.reload(true)");
    }

    // ---- NPC-call quests (quest rework B3a, task 4) ---------------------------------------------------------------------

    private const string CallKey = QuestKey.Bastok.ADiscerningEye;

    private static void RunNpcCalls()
    {
        Console.WriteLine("NPC-call quests");
        uint id = CharIds[0];
        zone.TestQuestScripts.Add(new CallProbeQuest());
        int mark = log.Count;
        Lua("InteractionGlobal.reload(true)");
        Check("an NPC-call quest registers (it counts) and is not refused", $"quests: {RegisteredCount + 1} C# quests registered/",
              Untimed(log.Skip(mark).LastOrDefault(IsRegistered)) + "/" + string.Join(" | ", log.Skip(mark).Where(l => l.Contains("not registered"))));
        Check("  and it has no container in the lookup", "", Lua("report(tostring(CSharpQuests.live['Quest[1][71]'] or ''))"));
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        uint umberto = NpcId("Umberto");
        string Call(string key, string kind = "onTrigger") =>
            Lua($"report(tostring(xi.cs.quest(player, GetNPCByID({umberto}), '{key}', '{kind}')))");
        string Status() => Lua("report(tostring(player:getQuestStatus(xi.questLog.BASTOK, xi.quest.id.bastok.A_DISCERNING_EYE)))");

        // (a) Available: the handler's Event(473) is started through player:startEvent
        Collect();
        Check("(a) xi.cs.quest in the Available state returns true", "true", Call(CallKey));
        Check("  and event 473 was sent from Umberto", $"{id}:473", EvId());

        // (b) the event ends: OnEventFinish begins the quest through player:addQuest
        Collect();
        Lua($"xi.cs.questEvent(player, GetNPCByID({umberto}), '{CallKey}', 473, 0)");
        Check("(b) xi.cs.questEvent ran the Begin: the quest is Accepted", "1", Status());

        // (c) Accepted needs B3A_PROBE = 5
        Collect();
        pending_.Clear();
        Check("(c) the guard fails (char var 0): false", "false", Call(CallKey));
        Collect();
        Check("  and no packet was sent", "0", pending_.Count(p => Opcode(p) is 0x032 or 0x034 or 0x02A).ToString());
        Lua("player:setCharVar('B3A_PROBE', 5)");
        pending_.Clear();
        Check("  the guard holds (char var 5): true", "true", Call(CallKey));
        Collect();
        Check("  and ITEM_OBTAINED was sent by player:messageSpecial", $"{TextId(Zone.BastokMarkets, "ITEM_OBTAINED")}",
              string.Join(",", pending_.Where(p => Opcode(p) == 0x02A).Select(p => BitConverter.ToUInt16(p.Buffer, 26) & 0x7FFF)));
        Check("  onTrade has no handler: false", "false", Call(CallKey, "onTrade"));

        // (d) an unknown key: false, and the line once for two calls in the same second
        mark = log.Count;
        string first = Call("bastok.nope"), second = Call("bastok.nope");
        Check("(d) an unknown key: false twice", "false/false", first + "/" + second);
        Check("  the line is logged once", "quests: xi.cs.quest: unknown key bastok.nope",
              string.Join(" | ", log.Skip(mark).Where(l => l.Contains("unknown key"))));
        now += 61000;
        mark = log.Count;
        Call("bastok.nope");
        Check("  and again after 60 s of NowMs", "1", log.Skip(mark).Count(l => l.Contains("unknown key bastok.nope")).ToString());
        Check("xi still has its own tables (xi.cs did not replace xi)", "table/table", Lua("report(type(xi.quest) .. '/' .. type(xi.cs))"));
        Logout();
        Cleanup(db, id);

        // (e) the call sites in a scripts tree
        string root = Path.Combine(Path.GetTempPath(), "b3a_calls_" + Guid.NewGuid().ToString("N"));
        try
        {
            string npcs = Path.Combine(root, "zones", "Bastok_Markets", "npcs");
            Directory.CreateDirectory(npcs);
            File.WriteAllText(Path.Combine(npcs, "Umberto.lua"), $"local ok = xi.cs.quest(player, npc, '{CallKey}', 'onTrigger')\n");
            File.WriteAllText(Path.Combine(npcs, "Other.lua"), "xi.cs.quest(player, npc, 'bastok.nope', 'onTrigger')\n");
            mark = log.Count;
            Check("(e) a call with a key no quest has: that unknown key, nothing else", "zones/Bastok_Markets/npcs/Other.lua: unknown key bastok.nope",
                  string.Join(" | ", OwnCalls(zone.ValidateNpcCalls(root))));
            Check("  and it is logged", "quests: npc call: zones/Bastok_Markets/npcs/Other.lua: unknown key bastok.nope",
                  string.Join(" | ", log.Skip(mark).Where(l => l.Contains("npc call") && !IsShippedCallGap(l))));
            File.Delete(Path.Combine(npcs, "Umberto.lua"));
            File.Delete(Path.Combine(npcs, "Other.lua"));
            Check("  no Umberto script: the quest's Called() NPC is never called", $"{CallKey}: Bastok_Markets/Umberto has handlers but its script never calls the quest",
                  string.Join(" | ", OwnCalls(zone.ValidateNpcCalls(root))));
            File.WriteAllText(Path.Combine(npcs, "Umberto.lua"), "xi.cs.quest(player, npc, key, 'onTrigger')\n");
            Check("  a variable key", "zones/Bastok_Markets/npcs/Umberto.lua: the key must be a string literal | " +
                  $"{CallKey}: Bastok_Markets/Umberto has handlers but its script never calls the quest", string.Join(" | ", OwnCalls(zone.ValidateNpcCalls(root))));
            File.WriteAllText(Path.Combine(npcs, "Umberto.lua"), "xi.cs.quest(player, npc, 'a' .. 'b', 'onTrigger')\n");
            Check("  a concatenation of two literals is not a literal", "zones/Bastok_Markets/npcs/Umberto.lua: the key must be a string literal | " +
                  $"{CallKey}: Bastok_Markets/Umberto has handlers but its script never calls the quest", string.Join(" | ", OwnCalls(zone.ValidateNpcCalls(root))));
            File.WriteAllText(Path.Combine(npcs, "Umberto.lua"), $"-- xi.cs.quest(player, npc, 'bastok.nope', 'onTrigger')\nxi.cs.questEvent(player, npc, \"{CallKey}\", csid, option)\n");
            Check("  a commented call is skipped, a double-quoted key and questEvent count", "", string.Join(" | ", OwnCalls(zone.ValidateNpcCalls(root))));
            File.WriteAllText(Path.Combine(npcs, "Umberto.lua"), $"xi.cs.quest(player, npc, '{CallKey}', 'onTrigger')\n");
            File.WriteAllText(Path.Combine(npcs, "Samia.lua"), $"xi.cs.quest(player, npc, '{CallKey}', 'onTrigger')\n");
            Check("  a quest called from an NPC with no Called() handler", $"zones/Bastok_Markets/npcs/Samia.lua: {CallKey} has no handlers for Samia",
                  string.Join(" | ", OwnCalls(zone.ValidateNpcCalls(root))));
            // the narrow scan (the startup's) reads only the called NPCs' scripts
            File.WriteAllText(Path.Combine(npcs, "Samia.lua"), "xi.cs.quest(player, npc, 'bastok.nope', 'onTrigger')\n");
            Check("  narrow: a bad key in an unrelated script is not read", "", string.Join(" | ", OwnCalls(zone.ValidateNpcCalls(root, narrow: true))));
            File.WriteAllText(Path.Combine(npcs, "Umberto.lua"), "xi.cs.quest(player, npc, 'bastok.nope', 'onTrigger')\n");
            Check("  narrow: a bad key in a called NPC's script is found", "zones/Bastok_Markets/npcs/Umberto.lua: unknown key bastok.nope | " +
                  $"{CallKey}: Bastok_Markets/Umberto has handlers but its script never calls the quest", string.Join(" | ", OwnCalls(zone.ValidateNpcCalls(root, narrow: true))));
            // an unreadable called script must not throw out of the startup's call: it is logged and returned as a problem
            File.WriteAllText(Path.Combine(npcs, "Umberto.lua"), "-- unreadable while held open\nxi.cs.quest(player, npc, 'bastok.nope', 'onTrigger')\n");
            IReadOnlyList<string> held = null;
            Exception heldEx = null;
            mark = log.Count;
            using (new FileStream(Path.Combine(npcs, "Umberto.lua"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                try { held = zone.ValidateNpcCallsGuarded(root, narrow: true); }
                catch (Exception ex) { heldEx = ex; }
            }
            Check("  guarded narrow scan of an unreadable called script: no throw", "null", heldEx == null ? "null" : heldEx.GetType().Name);
            Check("  and the problem entry is 'validation failed: ...'", "True", (held != null && held.Count == 1 && held[0].StartsWith("validation failed: ", StringComparison.Ordinal)).ToString());
            Check("  and it is logged as quests: npc call: validation failed", "1",
                  log.Skip(mark).Count(l => l.StartsWith("quests: npc call: validation failed: ", StringComparison.Ordinal)).ToString());
        }
        finally
        {
            Directory.Delete(root, true);
        }
        Check("the real scripts folder has no NPC-call problems", "", string.Join(" | ", zone.NpcCallProblems));
        // the scan's cost on the real folder: warm, median of 3 (informational)
        string realScripts = Path.Combine(Directory.GetParent(FindDataDir()).FullName, "scripts");
        var scanMs = new List<double>();
        for (int i = 0; i < 4; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            zone.ValidateNpcCalls(realScripts);
            sw.Stop();
            if (i > 0) scanMs.Add(sw.Elapsed.TotalMilliseconds);
        }
        scanMs.Sort();
        Console.WriteLine($"  ValidateNpcCalls on the real scripts folder: median {scanMs[1]:F1} ms (warm, of 3, file cache primed)");

        // (f) the validator
        zone.TestQuestScripts.Clear();
        string Problems(QuestScript q) => string.Join(" | ", zone.ValidateQuestScript(q.Compile()));
        Check("(f) an NPC-call quest matching a step is refused", "step 1 is matched but never set | state 1: an NPC-call quest cannot match a step",
              Problems(new CallStepQuest()));
        Check("  a KeyItem reply is refused", "Umberto OnTrigger: a KeyItem reply is not allowed in an NPC call", Problems(new CallKeyItemQuest()));
        Check("  a Called() and a container handler for one NPC are refused", "Umberto in zone 235 has both Called() and container handlers",
              Problems(new CallBothQuest()));
        mark = log.Count;
        zone.TestQuestScripts.Add(new CallStepQuest());
        Lua("InteractionGlobal.reload(true)");
        Check("  the registration refuses it with the reason", "quests: C# quest bastok.a_discerning_eye failed to load and has no Lua fallback: step 1 is matched but never set; state 1: an NPC-call quest cannot match a step",
              string.Join(" | ", log.Skip(mark).Where(l => l.Contains("a_discerning_eye"))));
        zone.TestQuestScripts.Clear();

        // a quest with both kinds: a container for the non-called handlers, the call for the rest; Complete in an NPC call
        zone.TestQuestScripts.Add(new CallMixedQuest());
        zone.TestQuestScripts.Add(new CallRewardQuest());
        Lua("InteractionGlobal.reload(true)");
        Check("a mixed quest keeps a container for Samia only", "csharp:bastok.bait_and_switch/",
              Owners(Zone.BastokMarkets, "Samia", "onTrigger", "Quest[1][83]") + "/" + Owners(Zone.BastokMarkets, "Umberto", "onTrigger", "Quest[1][83]"));
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        umberto = NpcId("Umberto");
        Collect();
        Check("  its NPC call works", "true", Call("bastok.bait_and_switch"));
        Check("  the event is its Called() one", $"{id}:473", EvId());
        Lua("player:addQuest(xi.questLog.BASTOK, xi.quest.id.bastok.THE_NAMING_GAME)");
        string gil = Gil();
        Collect();
        Check("Complete() with a reward: npcUtil.completeQuest ran", "true", Call("bastok.the_naming_game"));
        Check("  the quest is complete", "2", Lua("report(tostring(player:getQuestStatus(xi.questLog.BASTOK, xi.quest.id.bastok.THE_NAMING_GAME)))"));
        Check("  the reward gil came", (int.Parse(gil) + 250).ToString(), Gil());
        Logout();
        Cleanup(db, id);
        zone.TestQuestScripts.Clear();
        Lua("InteractionGlobal.reload(true)");
    }

    // A Discerning Eye (bastok 71), called from Umberto's script
    private sealed class CallProbeQuest : QuestScript
    {
        public CallProbeQuest() : base(QuestLog.Bastok, "A_DISCERNING_EYE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available)
                .Npc(Zone.BastokMarkets, "Umberto").Called().OnTrigger(q => QuestReply.Event(473))
                .OnEventFinish(Zone.BastokMarkets, 473, q => q.Quest.Begin());
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar("B3A_PROBE") == 5)
                .Npc(Zone.BastokMarkets, "Umberto").Called().OnTrigger(q => QuestReply.MessageSpecial(Text.BastokMarkets.ITEM_OBTAINED, 4));
        }
    }

    private sealed class CallStepQuest : QuestScript
    {
        public CallStepQuest() : base(QuestLog.Bastok, "A_DISCERNING_EYE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Accepted, step: 1).Npc(Zone.BastokMarkets, "Umberto").Called().OnTrigger(QuestReply.Event(473));
        }
    }

    private sealed class CallKeyItemQuest : QuestScript
    {
        public CallKeyItemQuest() : base(QuestLog.Bastok, "A_DISCERNING_EYE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available).Npc(Zone.BastokMarkets, "Umberto").Called().OnTrigger(QuestReply.KeyItem(1));
        }
    }

    private sealed class CallBothQuest : QuestScript
    {
        public CallBothQuest() : base(QuestLog.Bastok, "A_DISCERNING_EYE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available).Npc(Zone.BastokMarkets, "Umberto").Called().OnTrigger(QuestReply.Event(473));
            b.State(QuestStatus.Accepted).Npc(Zone.BastokMarkets, "Umberto").OnTrigger(QuestReply.Event(474));
        }
    }

    // Bait and Switch (bastok 83): Umberto is called, Samia is the container's
    private sealed class CallMixedQuest : QuestScript
    {
        public CallMixedQuest() : base(QuestLog.Bastok, "BAIT_AND_SWITCH") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available)
                .Npc(Zone.BastokMarkets, "Umberto").Called().OnTrigger(q => QuestReply.Event(473))
                .Npc(Zone.BastokMarkets, "Samia").OnTrigger(QuestReply.Event(9998));
        }
    }

    // The Naming Game (bastok 81): Complete() with a reward from an NPC call
    private sealed class CallRewardQuest : QuestScript
    {
        public CallRewardQuest() : base(QuestLog.Bastok, "THE_NAMING_GAME") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Gil = 250 });
            b.State(QuestStatus.Accepted).Npc(Zone.BastokMarkets, "Umberto").Called().OnTrigger(q => q.Quest.Complete() ? QuestReply.NoAction() : null);
        }
    }

    // ---- NPC-call quests, fix round 1: rendering, options, trades, failures, reload, ordering, the mixed quest's events ----

    // the shipped NPC-call quests (Personal Hygiene, Thick Shells, ...) have no call in a temp scripts tree: their "never calls
    // the quest" problems are not what these probes look at
    private static bool IsShippedCallGap(string problem) =>
        problem.EndsWith("has handlers but its script never calls the quest") && !problem.Contains(CallKey + ":") ||
        // quest rework C2: a shipped mission's call gaps likewise, but the test mission's (C2.cs RunC2Calls)
        problem.EndsWith("has handlers but its script never calls the mission") && !problem.Contains(SampleKey + ":");
    private static IEnumerable<string> OwnCalls(IEnumerable<string> problems) => problems.Where(p => !IsShippedCallGap(p));

    private static void RunNpcCalls2()
    {
        Console.WriteLine("NPC-call quests (2)");
        uint id = CharIds[0];
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        uint umberto = NpcId("Umberto");
        const string Key = CallKey;
        string Call(string key, string kind = "onTrigger") =>
            Lua($"report(tostring(xi.cs.quest(player, GetNPCByID({umberto}), '{key}', '{kind}')))");
        string Trade(int qty) => Lua($"local t = {{ getSlotCount = function() return 1 end, getItemId = function() return 640 end, " +
                                      $"getItemQty = function() return {qty} end }}; report(tostring(xi.cs.quest(player, GetNPCByID({umberto}), '{Key}', 'onTrade', t)))");
        string Status() => Lua("report(tostring(player:getQuestStatus(xi.questLog.BASTOK, xi.quest.id.bastok.A_DISCERNING_EYE)))");
        string Lines(int from, string part) => string.Join(" | ", log.Skip(from).Where(l => l.Contains(part)));
        int textId = TextId(Zone.BastokMarkets, "ITEM_OBTAINED");
        void WithQuest(QuestScript quest, Action body)
        {
            zone.TestQuestScripts.Clear();
            zone.TestQuestScripts.Add(quest);
            Lua("InteractionGlobal.reload(true)");
            body();
            zone.TestQuestScripts.Clear();
            Lua("InteractionGlobal.reload(true)");
        }
        List<BasicPacket> Sent(int opcode)
        {
            Collect();
            return pending_.Where(p => Opcode(p) == opcode).ToList();
        }

        WithQuest(new CallKindsQuest(), () =>
        {
            // MessageText: player:messageText(npc, id): 0x036 from the NPC (id at 4), message at 10
            Lua("player:setCharVar('B3A_MODE', 1)");
            pending_.Clear();
            Check("MessageText: the call returns true", "true", Call(Key));
            var said = Sent(0x036);
            Check("  one 0x036 from Umberto with ITEM_OBTAINED", $"1/{umberto}/{textId}",
                  $"{said.Count}/{(said.Count > 0 ? BitConverter.ToUInt32(said[0].Buffer, 4) : 0)}/{(said.Count > 0 ? BitConverter.ToUInt16(said[0].Buffer, 10) & 0x7FFF : 0)}");

            // MessageName: player:messageName(id, player): 0x027, the speaker the char, the name field the player's
            Lua("player:setCharVar('B3A_MODE', 2)");
            pending_.Clear();
            Check("MessageName: the call returns true", "true", Call(Key));
            var named = Sent(0x027);
            Check("  one 0x027 with ITEM_OBTAINED, the char as speaker, the player's name in the name field", $"1/{textId}/{id}/{CharName(id)}",
                  named.Count == 0 ? "none" : $"{named.Count}/{BitConverter.ToUInt16(named[0].Buffer, 10) & 0x7FFF}/{BitConverter.ToUInt32(named[0].Buffer, 4)}/" +
                                              Encoding.ASCII.GetString(named[0].Buffer, 32, 32).TrimEnd('\0'));

            // an event with options: one table replaces the positional args
            Lua("player:setCharVar('B3A_MODE', 3)");
            pending_.Clear();
            Check("Event with options: the call returns true", "true", Call(Key));
            Check("  event 9998 from the char, parameter 0 the positional 5, parameter 2 the option's 7", $"{id}:9998/5/7",
                  $"{EvId()}/{c.CurrentEvent.Params.GetValueOrDefault(0)}/{c.CurrentEvent.Params.GetValueOrDefault(2)}");
            Finish();

            // a reply the renderer refuses at run time: false, logged, nothing sent
            Lua("player:setCharVar('B3A_MODE', 4)");
            pending_.Clear();
            int mark = log.Count;
            Check("a KeyItem returned from a handler: false", "false", Call(Key));
            Check("  logged with the handler's name", "quests: bastok.a_discerning_eye Umberto onTrigger: a KeyItem reply is not allowed in an NPC call",
                  Lines(mark, "KeyItem reply"));
            Collect();
            Check("  and nothing was sent", "0", pending_.Count(p => Opcode(p) is 0x032 or 0x034 or 0x036 or 0x027 or 0x02A).ToString());

            // a handler that throws: false and logged; the NPC's script falls through
            Lua("player:setCharVar('B3A_MODE', 5)");
            pending_.Clear();
            mark = log.Count;
            Check("a throwing handler: the script falls through", "fell through",
                  Lua($"local npc = GetNPCByID({umberto}); if not xi.cs.quest(player, npc, '{Key}', 'onTrigger') then report('fell through') else report('handled') end"));
            Check("  the line names the handler and the message", "quests: bastok.a_discerning_eye Umberto onTrigger: boom", Lines(mark, "boom"));

            // onTrade with a trade argument: one that matches, one that doesn't
            pending_.Clear();
            Check("onTrade with the trade the handler wants (640 x2): true", "true", Trade(2));
            Check("  event 9996 was started", $"{id}:9996", EvId());
            Finish();
            pending_.Clear();
            Check("onTrade with another quantity: false", "false", Trade(1));
            Collect();
            Check("  and no event was started", "0", pending_.Count(p => Opcode(p) is 0x032 or 0x034).ToString());
        });

        // questEvent with an unknown key: the line, no error
        int m2 = log.Count;
        Check("questEvent with an unknown key does not error", "ok",
              Lua($"xi.cs.questEvent(player, GetNPCByID({umberto}), 'bastok.nope_event', 473, 0); report('ok')"));
        Check("  and logs the unknown key", "quests: xi.cs.quest: unknown key bastok.nope_event", Lines(m2, "unknown key"));

        // Complete() without a reward: player:completeQuest, no reward side effects
        WithQuest(new CallNoRewardQuest(), () =>
        {
            Lua("player:addQuest(xi.questLog.BASTOK, xi.quest.id.bastok.A_DISCERNING_EYE)");
            string gil = Gil(), fame = Fame();
            Check("Complete() with no reward: the call returns true", "true", Call(Key));
            Check("  the quest is Completed (2)", "2", Status());
            Check("  and no gil or fame came", $"{gil}/{fame}", $"{Gil()}/{Fame()}");
            Lua("player:delQuest(xi.questLog.BASTOK, xi.quest.id.bastok.A_DISCERNING_EYE)");
        });

        // Complete() in an NPC call cleans up like quest:complete: the quest's own Quest[log][id]* vars are deleted
        WithQuest(new CallCleanupQuest(), () =>
        {
            Lua("player:addQuest(xi.questLog.BASTOK, xi.quest.id.bastok.A_DISCERNING_EYE)");
            Lua("player:setCharVar('Quest[1][71]Other', 9)");
            Check("Complete() in a call: the call returns true", "true", Call(Key));
            Check("  the quest is Completed (2)", "2", Status());
            Check("  and the var the handler set is gone", "0/0", Lua("report(player:getCharVar('Quest[1][71]Probe') .. '/' .. player:getCharVar('Quest[1][71]Other'))"));
            Lua("player:delQuest(xi.questLog.BASTOK, xi.quest.id.bastok.A_DISCERNING_EYE)");
        });

        // two states for one NPC: declaration order decides
        WithQuest(new CallTwoStatesQuest(), () =>
        {
            Lua("player:delQuest(xi.questLog.BASTOK, xi.quest.id.bastok.A_DISCERNING_EYE)");
            pending_.Clear();
            Call(Key);
            Check("two matching states: the first declared wins (9998, not 9999)", $"{id}:9998", EvId());
            Finish();
            Lua("player:addQuest(xi.questLog.BASTOK, xi.quest.id.bastok.A_DISCERNING_EYE)");
            pending_.Clear();
            Call(Key);
            Check("  a first state that does not match is skipped for the Accepted one (9990)", $"{id}:9990", EvId());
            Finish();
            Lua("player:delQuest(xi.questLog.BASTOK, xi.quest.id.bastok.A_DISCERNING_EYE)");
        });

        // a mixed quest whose called NPC's event finish goes through questEvent, not the container
        WithQuest(new CallMixedEventQuest(), () =>
        {
            string container = $"csharp:{Key}";
            Check("mixed: the Called() NPC's OnEventFinish is not in the container", "",
                  Owners(Zone.BastokMarkets, "onEventFinish", "9998", "Quest[1][71]"));
            Check("  a state-level OnEventFinish is", container, Owners(Zone.BastokMarkets, "onEventFinish", "9997", "Quest[1][71]"));
            Lua("player:delQuest(xi.questLog.BASTOK, xi.quest.id.bastok.A_DISCERNING_EYE)");
            Lua($"xi.cs.questEvent(player, GetNPCByID({umberto}), '{Key}', 9998, 0)");
            Check("  xi.cs.questEvent runs the called handler (the quest is Accepted)", "1", Status());
            Lua("player:delQuest(xi.questLog.BASTOK, xi.quest.id.bastok.A_DISCERNING_EYE)");
        });

        // a call-only quest with a state-level OnEventFinish: no container, its event finish runs through questEvent
        WithQuest(new CallOnlyStateEventQuest(), () =>
        {
            Check("call-only with a state-level OnEventFinish: no container in the lookup", "",
                  Lua("report(tostring(CSharpQuests.live['Quest[1][71]'] or ''))"));
            Check("  and no onEventFinish owner", "", Owners(Zone.BastokMarkets, "onEventFinish", "9998", "Quest[1][71]"));
            Lua("player:delQuest(xi.questLog.BASTOK, xi.quest.id.bastok.A_DISCERNING_EYE)");
            Lua($"xi.cs.questEvent(player, GetNPCByID({umberto}), '{Key}', 9998, 0)");
            Check("  xi.cs.questEvent runs it (the quest is Accepted)", "1", Status());
            Lua("player:delQuest(xi.questLog.BASTOK, xi.quest.id.bastok.A_DISCERNING_EYE)");
        });

        // reload: the quest is gone, so its key is unknown again
        now += 61000;
        m2 = log.Count;
        Check("after a reload without the quest the call is false", "false", Call(Key));
        Check("  and logs the unknown key", $"quests: xi.cs.quest: unknown key {Key}", Lines(m2, "unknown key"));
        Logout();
        Cleanup(db, id);
    }

    // ---- call kinds: mob deaths, zone-in, event updates, bool returns; kind literals; M6 (quest rework B3b prep) -----

    private const string M6Prefix = "quests: call-only quests not registered (CSHARP_QUESTS / CSHARP_QUESTS_OFF): ";

    private static void RunCallKinds()
    {
        Console.WriteLine("call kinds: mob deaths, zone-in, event updates");
        uint id = CharIds[0];
        const string Key = CallKey;
        int mark = log.Count;
        zone.TestQuestScripts.Add(new MobZoneCallQuest());
        Lua("InteractionGlobal.reload(true)");
        Check("a call-only quest with a mob death, a zone-in and zone events registers", "",
              string.Join(" | ", log.Skip(mark).Where(l => l.Contains("not registered") || l.Contains("failed to load"))));
        Check("  no M6 line while nothing is switched off", "0", log.Skip(mark).Count(l => l.StartsWith(M6Prefix)).ToString());
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        uint umberto = NpcId("Umberto");
        var labels = new List<string>();
        zone.HandlerCalled = (k, label) => { if (k == Key) labels.Add(label); };
        string Lines(int from, string part) => string.Join(" | ", log.Skip(from).Where(l => l.Contains(part)));
        // a mob as the call sees it: mob:getZoneID(), mob:getName()
        string Mob(string name) => $"{{ getZoneID = function() return {Zone.AltarRoom} end, getName = function() return '{name}' end }}";
        string Death(string player, string name = "Yagudo_Avatar") =>
            Lua($"report(tostring(xi.cs.quest({player}, {Mob(name)}, '{Key}', 'onMobDeath')))");
        string Event(string npc, int csid, int option, string kind = null) =>
            Lua($"report(tostring(xi.cs.questEvent(player, {npc}, '{Key}', {csid}, {option}{(kind == null ? "" : ", " + kind)})))");
        string ZoneIn(string key = Key) => Lua($"report(tostring(xi.cs.questZoneIn(player, '{key}', {Zone.BastokMines})))");

        // onMobDeath: the mob is the entity, the handler gets no npc
        mark = log.Count;
        Check("onMobDeath: the call returns true", "true", Death("player"));
        Check("  the handler ran, with no npc (Npc null)", "1", Var("B3B_KILL"));
        Check("  through RunHandler (coverage counted it)", "1/Yagudo_Avatar/onMobDeath", string.Join(",", labels));
        Check("  a death with no killer (player nil): false", "false", Death("nil"));
        Check("  another mob: false", "false", Death("player", "Yagudos_Elemental"));
        Check("  and no line was logged", "", Lines(mark, "quests: "));

        // questEvent(..., 'update'): OnEventUpdate, true when a handler ran
        labels.Clear();
        Check("questEvent 'update': true", "true", Event($"GetNPCByID({umberto})", 9997, 3, "'update'"));
        Check("  OnEventUpdate ran with the option and the npc", $"3/{umberto}", Var("B3B_UPD") + "/" + Var("B3B_UNPC"));
        Check("  through RunHandler", "1/-/onEventUpdate:9997", string.Join(",", labels));
        Check("  a csid with no update handler: false", "false", Event($"GetNPCByID({umberto})", 9990, 0, "'update'"));
        Check("  the finish handler did not run for the update", "-", Var("B3B_ZFIN"));
        // questEvent with npc nil: a zone event; OnEventFinish
        labels.Clear();
        Check("questEvent with npc nil: true", "true", Event("nil", 9997, 0));
        Check("  OnEventFinish ran with no npc", "-1", Var("B3B_ZFIN"));
        Check("  through RunHandler", "1/-/onEventFinish:9997", string.Join(",", labels));
        Check("  a csid with no finish handler: false", "false", Event("nil", 9990, 0));
        mark = log.Count;
        Check("questEvent with an unknown sixth argument: false", "false", Event("nil", 9997, 0, "'updat'"));
        Check("  the unknown-kind line", $"quests: xi.cs.quest {Key}: unknown kind updat", Lines(mark, "unknown kind"));

        // questZoneIn: the reply's event id, nil when no state matches or the reply has none
        labels.Clear();
        Lua("player:setCharVar('B3B_ZI', 1)");
        Collect();
        pending_.Clear();
        Check("questZoneIn: the reply's cutscene id", "9997", ZoneIn());
        Check("  through RunHandler", "1/-/onZoneIn", string.Join(",", labels));
        Collect();
        Check("  and it started nothing itself", "0", pending_.Count(p => Opcode(p) is 0x032 or 0x034).ToString());
        Lua("player:setCharVar('B3B_ZI', 0)");
        Check("  the handler returns no reply: nil", "nil", ZoneIn());
        Lua("player:setCharVar('B3B_ZI', 2)");
        mark = log.Count;
        Check("  an event with arguments: nil", "nil", ZoneIn());
        Check("    logged, not sent without them", $"quests: {Key} onZoneIn: a cutscene id takes no arguments here (1 given)", Lines(mark, Key));
        Lua("player:setCharVar('B3B_ZI', 3)");
        mark = log.Count;
        Check("  a message reply: nil", "nil", ZoneIn());
        Check("    logged", $"quests: {Key} onZoneIn: a MessageSpecial reply is not a cutscene id", Lines(mark, Key));
        Lua("player:setCharVar('B3B_ZI', 4)");
        mark = log.Count;
        Check("  a cutscene with a reply modifier: nil", "nil", ZoneIn());
        Check("    logged, not sent without it", $"quests: {Key} onZoneIn: a cutscene id takes no reply modifiers (oncePerZone()): onZoneIn builds no action", Lines(mark, Key));
        Lua("player:setCharVar('B3B_ZI', 1)");
        Lua("player:addQuest(xi.questLog.BASTOK, xi.quest.id.bastok.A_DISCERNING_EYE)");
        Check("  no state matches (Accepted): nil", "nil", ZoneIn());
        Lua("player:delQuest(xi.questLog.BASTOK, xi.quest.id.bastok.A_DISCERNING_EYE)");
        now += 61000;
        mark = log.Count;
        Check("  an unknown key: nil", "nil", ZoneIn("bastok.nope_zone"));
        Check("    and the unknown-key line", "quests: xi.cs.quest: unknown key bastok.nope_zone", Lines(mark, "unknown key"));
        Check("the B3a form still works: questEvent from an NPC returns a value the script may ignore", "ok",
              Lua($"xi.cs.questEvent(player, GetNPCByID({umberto}), '{Key}', 9997, 0); report('ok')"));
        zone.HandlerCalled = null;
        Lua("player:setCharVar('B3B_ZI', 0) player:setCharVar('B3B_KILL', 0) player:setCharVar('B3B_UPD', 0) player:setCharVar('B3B_UNPC', 0) player:setCharVar('B3B_ZFIN', 0)");
        Logout();
        Cleanup(db, id);

        // the call sites: a mob script, Zone.lua and an NPC script in a temp scripts tree, full and narrow
        string root = Path.Combine(Path.GetTempPath(), "b3b_calls_" + Guid.NewGuid().ToString("N"));
        try
        {
            string mobs = Path.Combine(root, "zones", "Altar_Room", "mobs");
            string markets = Path.Combine(root, "zones", "Bastok_Markets");
            string npcs = Path.Combine(markets, "npcs");
            Directory.CreateDirectory(mobs);
            Directory.CreateDirectory(npcs);
            string avatar = Path.Combine(mobs, "Yagudo_Avatar.lua"), zoneLua = Path.Combine(markets, "Zone.lua"), other = Path.Combine(npcs, "Umberto.lua");
            string Scan(bool narrow) => string.Join(" | ", OwnCalls(zone.ValidateNpcCalls(root, narrow)));
            void Both(string what, string expected)
            {
                Check($"{what} (full)", expected, Scan(false));
                Check($"{what} (narrow)", expected, Scan(true));
            }
            string missingMob = $"{Key}: Altar_Room/Yagudo_Avatar has handlers but its script never calls the quest";
            string missingZone = $"{Key}: Bastok_Markets/Zone has handlers but its script never calls the quest";
            string missingUpdate = $"{Key}: Bastok_Markets/onEventUpdate has handlers but its script never calls the quest";
            Both("no script calls: the mob, the zone-in and the update are each missing", $"{missingMob} | {missingZone} | {missingUpdate}");

            File.WriteAllText(avatar, $"entity.onMobDeath = function(mob, player, optParams)\n    xi.cs.quest(player, mob, '{Key}', 'onMobDeath')\nend\n");
            File.WriteAllText(zoneLua, $"cs = xi.cs.questZoneIn(player, '{Key}', prevZone) or cs\nxi.cs.questEvent(player, nil, '{Key}', csid, option)\n");
            File.WriteAllText(other, $"xi.cs.questEvent(player, npc, '{Key}', csid, option, 'update')\n");
            Both("every call in its place: clean (narrow reads the zone for the update)", "");

            File.WriteAllText(avatar, $"xi.cs.quest(player, mob, '{Key}', 'onTrigger')\n");
            Both("a mob script calling with another kind: its onMobDeath is missing", missingMob);
            File.WriteAllText(avatar, $"xi.cs.quest(player, mob, '{Key}', 'onMobDeth')\n");
            Both("a misspelt kind literal (M4)", $"zones/Altar_Room/mobs/Yagudo_Avatar.lua: unknown kind 'onMobDeth' | {missingMob}");
            File.WriteAllText(avatar, $"xi.cs.quest(player, mob, '{Key}', kind)\n");
            Both("a variable kind is refused as one (and does not count as the onMobDeath call)",
                 $"zones/Altar_Room/mobs/Yagudo_Avatar.lua: the kind must be a string literal (kind) | {missingMob}");
            File.WriteAllText(avatar, $"xi.cs.quest(player, mob, '{Key}', 'onMobDeath')\n");

            File.WriteAllText(zoneLua, $"xi.cs.questEvent(player, nil, '{Key}', csid, option)\n");
            Both("a Zone.lua without questZoneIn", missingZone);
            File.WriteAllText(zoneLua, "cs = xi.cs.questZoneIn(player, 'bastok.nope', prevZone) or cs\n");
            Both("questZoneIn with an unknown key", $"zones/Bastok_Markets/Zone.lua: unknown key bastok.nope | {missingZone}");
            File.WriteAllText(zoneLua, "cs = xi.cs.questZoneIn(player, key, prevZone) or cs\n");
            Both("questZoneIn with a variable key", $"zones/Bastok_Markets/Zone.lua: the key must be a string literal | {missingZone}");
            File.WriteAllText(zoneLua, $"cs = xi.cs.questZoneIn(player, '{Key}', prevZone) or cs\n");

            File.WriteAllText(other, $"cs = xi.cs.questZoneIn(player, '{Key}', prevZone) or cs\n" +
                                     $"xi.cs.questEvent(player, npc, '{Key}', csid, option, 'update')\n");
            Both("questZoneIn in an NPC script is refused (only Zone.lua's onZoneIn runs at a zone-in)",
                 "zones/Bastok_Markets/npcs/Umberto.lua: xi.cs.questZoneIn outside Zone.lua " +
                 "(its onZoneIn is the only zone-in)");
            File.WriteAllText(other, $"xi.cs.questEvent(player, npc, '{Key}', csid, option)\n");
            Both("no questEvent(..., 'update') in the zone", missingUpdate);
            File.WriteAllText(other, $"xi.cs.questEvent(player, npc, '{Key}', csid, option, 'updat')\n");
            Both("a misspelt update literal (M4)", $"zones/Bastok_Markets/npcs/Umberto.lua: unknown kind 'updat' | {missingUpdate}");
            File.WriteAllText(other, $"xi.cs.quest(player, npc, '{Key}', 'onTalk')\nxi.cs.questEvent(player, npc, '{Key}', csid, option, 'update')\n");
            Check("an unknown xi.cs.quest kind in an NPC script (full)", "zones/Bastok_Markets/npcs/Umberto.lua: unknown kind 'onTalk'", Scan(false));
            mark = log.Count;
            Scan(false);
            Check("  logged with the npc call prefix", "quests: npc call: zones/Bastok_Markets/npcs/Umberto.lua: unknown kind 'onTalk'",
                  string.Join(" | ", log.Skip(mark).Where(l => l.Contains("unknown kind"))));
        }
        finally
        {
            Directory.Delete(root, true);
        }

        // M6: a call-only quest left out by the switches is named after the registration
        mark = log.Count;
        zone.CSharpQuestsOff.Add(Key);
        Lua("InteractionGlobal.reload(true)");
        Check("M6: a call-only quest in CSHARP_QUESTS_OFF is named", M6Prefix + Key, Lines(mark, M6Prefix));
        mark = log.Count;
        zone.CSharpQuestsOff.Add(QuestKey.Sandoria.ThickShells);
        Lua("InteractionGlobal.reload(true)");
        Check("  two of them, sorted and joined by a comma", $"{M6Prefix}{Key}, {QuestKey.Sandoria.ThickShells}", Lines(mark, M6Prefix));
        zone.CSharpQuestsOff.Clear();
        mark = log.Count;
        zone.CSharpQuestsEnabled = false;
        Lua("InteractionGlobal.reload(true)");
        string all = Lines(mark, M6Prefix);
        // the names sorted ordinally, so a shipped one may come before the probe (adoulin.children_of_the_rune)
        var names = all.StartsWith(M6Prefix) ? all.Substring(M6Prefix.Length).Split(", ") : Array.Empty<string>();
        Check("  CSHARP_QUESTS off: every call-only quest is named, the probe and the shipped ones, sorted",
              "True/True/True", $"{names.Contains(Key)}/{names.Contains(QuestKey.Sandoria.ThickShells)}/" +
                                $"{names.SequenceEqual(names.OrderBy(n => n, StringComparer.Ordinal))}");
        zone.CSharpQuestsEnabled = true;
        mark = log.Count;
        Lua("InteractionGlobal.reload(true)");
        Check("  back on: no M6 line", "", Lines(mark, M6Prefix));
        zone.TestQuestScripts.Clear();
        Lua("InteractionGlobal.reload(true)");
    }

    // A Discerning Eye (bastok 71) as a call-only quest with a called mob death (Yagudo Avatar in Altar Room) and
    // state-level zone-in, update and finish handlers in Bastok Markets: each records what it got in a char var
    private sealed class MobZoneCallQuest : QuestScript
    {
        public MobZoneCallQuest() : base(QuestLog.Bastok, "A_DISCERNING_EYE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available)
                .Npc(Zone.AltarRoom, XI.QuestContent.Npc.AltarRoom.YagudoAvatar).Called()
                .OnMobDeath(q => q.Player.SetCharVar("B3B_KILL", q.Npc == null ? 1 : 2))
                .OnZoneIn(Zone.BastokMarkets, q => q.Player.CharVar("B3B_ZI") switch
                {
                    1 => QuestReply.Cutscene(9997),
                    2 => QuestReply.Event(9996, 5),
                    3 => QuestReply.MessageSpecial(Text.BastokMarkets.ITEM_OBTAINED, 4),
                    4 => QuestReply.Cutscene(9997).OncePerZone(),
                    _ => null,
                })
                .OnEventUpdate(Zone.BastokMarkets, 9997, q =>
                {
                    q.Player.SetCharVar("B3B_UPD", q.Option);
                    q.Player.SetCharVar("B3B_UNPC", q.Npc?.Id ?? -1);
                })
                .OnEventFinish(Zone.BastokMarkets, 9997, q => q.Player.SetCharVar("B3B_ZFIN", q.Npc?.Id ?? -1));
        }
    }

    // ---- the reply modifiers (quest rework B3b Bastok, task 1b) ----------------------------------------------------------

    // the Lua calls ModifierProbeQuest's replies chain, by the player's B3B_MOD local var (1-based); the last is the plain
    // event, the baseline each modified action must differ from
    private static readonly string[] ModifierCalls =
    {
        ":replaceDefault()", ":oncePerZone()", ":importantEvent()", ":importantOnce()", ":progress()", ":setPriority(50)",
        ":openDoor()", ":importantEvent():setPriority(20)", "",
    };

    // the Lua calls of ModifierProbeQuest's Message (B3B_MOD 21 ...) and Sequence (31 ...) replies, the plain one last
    private static readonly string[] MessageModifierCalls =
    {
        "messageSpecial(7000):replaceDefault()", "messageSpecial(7000):oncePerZone()",
        "messageSpecial(7000):importantEvent():setPriority(20)", "messageSpecial(7000)",
    };
    private static readonly string[] SequenceModifierCalls =
    {
        "sequence({ text = 7000 }):replaceDefault()", "sequence({ text = 7000 }):oncePerZone()",
        "sequence({ text = 7000 })",
    };

    // a trigger area handler with a reply (DRK AF3 Blade of Evil: return quest:progressEvent(14)): the lookup performs the
    // C# reply as the action, with no NPC; a reply that needs an NPC (a text it shows) is refused and logged. The areas are
    // registered on Bastok Markets for this check (its Zone.lua's own onTriggerAreaEnter does nothing), away from the NPCs
    // the other checks use; it runs last, as they stay registered
    private static void RunTriggerAreaReply()
    {
        Console.WriteLine("trigger area replies");
        zone.TestQuestScripts.Add(new TriggerAreaReplyQuest());
        Lua("InteractionGlobal.reload(true)");
        uint id = CharIds[0];
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        Lua("local z = GetZone(xi.zone.BASTOK_MARKETS) " +
            $"z:registerCuboidTriggerArea({TriggerAreaReplyQuest.Area}, -282, -18, -159, -278, -14, -155) " +
            $"z:registerCuboidTriggerArea({TriggerAreaReplyQuest.TextArea}, -282, -18, -149, -278, -14, -145)");
        // past xi.player.onGameIn's 2.5 s ZoningIn: no trigger area is entered while it is set
        Tick(3000);
        Collect();
        pending_.Clear();
        int mark = log.Count;
        WalkTo(-280.0f, -16.0f, -157.0f);
        Check("walking into the area starts the reply's event 9995 (the char's own: no NPC)", $"{id}:9995", EvId());
        Check("  the C# handler ran, no quests: line", "", string.Join(" | ", log.Skip(mark).Where(l => l.StartsWith("quests: "))));
        Finish();
        WalkTo(-296.0f, -16.0f, -157.0f);
        Collect();
        pending_.Clear();
        mark = log.Count;
        WalkTo(-280.0f, -16.0f, -147.0f);
        Check("a reply with a text the NPC shows is refused: no event, and logged", "none/True",
              $"{EvId()}/{log.Skip(mark).Any(l => l.Contains("a MessageText reply needs an NPC to show it, and a trigger area has none"))}");
        Logout();
        Cleanup(db, id);
        zone.TestQuestScripts.Clear();
        Lua("InteractionGlobal.reload(true)");
    }

    // each modifier: the action the C# handler's reply builds against the action of the same Lua call
    // (quest:event(9998):oncePerZone()), field by field (priority, secondaryPriority, returnValue, id); then OncePerZone in
    // play: Samia's 9998 the first time, her own answer the second, with the action's local var at Action.Priority.Ignore
    private static void RunReplyModifiers()
    {
        Console.WriteLine("reply modifiers");
        zone.TestQuestScripts.Add(new ModifierProbeQuest());
        Lua("InteractionGlobal.reload(true)");
        uint id = CharIds[0];
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        uint samia = NpcId("Samia");
        string Fields(int i) => Lua($@"
            player:setLocalVar('B3B_MOD', {i + 1})
            local function fields(a)
                return tostring(a.priority) .. '/' .. tostring(a.secondaryPriority) .. '/' .. tostring(a.returnValue) .. '/' .. tostring(a.id)
            end
            local cs
            for _, e in ipairs(InteractionGlobal.lookup.data[xi.zone.BASTOK_MARKETS]['Samia'].onTrigger) do
                if e.container and e.container.id == '{FixtureContainer}' then cs = e.handler(player, GetNPCByID({samia})) end
            end
            local q = Quest:new({FixtureLuaIds})
            local lua = q:event(9998){ModifierCalls[i]}
            report(fields(cs) .. '|' .. fields(lua))");
        string plain = Fields(ModifierCalls.Length - 1);
        Check("the plain reply: the C# action is the Lua one", "True", (plain.Split('|')[0] == plain.Split('|')[1]).ToString());
        for (int i = 0; i < ModifierCalls.Length - 1; i++)
        {
            string got = Fields(i);
            var halves = got.Split('|');
            Check($"event(9998){ModifierCalls[i]}: the C# action's fields are the Lua action's, and not the plain one's",
                  $"True/True", $"{halves[0] == halves[1]}/{halves[0] != plain.Split('|')[0]}");
        }
        // the order matters: importantEvent() then setPriority(20) is priority 20, secondary 100 (Event); the reverse
        // order would be 1000 / 100
        string ordered = Fields(ModifierCalls.Length - 2).Split('|')[0];
        Check("event(9998):importantEvent():setPriority(20): applied in the order written (priority 20, secondary 100)", "20/100",
              string.Join("/", ordered.Split('/').Take(2)));

        // carry-quests-1 (Bastok branch 1 minors): a Message and a Sequence reply take the modifiers too, on the action
        // the Lua call makes (quest:messageSpecial(7000):replaceDefault(),
        // quest:sequence({ text = 7000 }):oncePerZone(), ...): the same fields, and not the plain action's. The last of
        // each list is the plain one
        foreach (var (first, calls) in new[] { (21, MessageModifierCalls), (31, SequenceModifierCalls) })
        {
            string Same(int i) => Lua($@"
                player:setLocalVar('B3B_MOD', {first + i})
                local function fields(a)
                    return tostring(a.priority) .. '/' .. tostring(a.secondaryPriority) .. '/' ..
                        tostring(a.returnValue) .. '/' .. tostring(a.id)
                end
                local cs
                for _, e in ipairs(InteractionGlobal.lookup.data[xi.zone.BASTOK_MARKETS]['Samia'].onTrigger) do
                    if e.container and e.container.id == '{FixtureContainer}' then
                        cs = e.handler(player, GetNPCByID({samia}))
                    end
                end
                local q = Quest:new({FixtureLuaIds})
                local lua = q:{calls[i]}
                report(fields(cs) .. '|' .. fields(lua))");
            var plainOne = Same(calls.Length - 1).Split('|');
            Check($"{calls[^1]}: the C# action is the Lua one", "True", (plainOne[0] == plainOne[1]).ToString());
            for (int i = 0; i < calls.Length - 1; i++)
            {
                var halves = Same(i).Split('|');
                Check($"{calls[i]}: the C# action's fields are the Lua action's, and not the plain one's",
                      "True/True", $"{halves[0] == halves[1]}/{halves[0] != plainOne[0]}");
            }
        }

        Lua("player:setLocalVar('B3B_MOD', 2)");
        TalkTo("Samia");
        Check("OncePerZone in play: the first talk is 9998", $"{samia}:9998", EvId());
        FinishIfEvent();
        Check("  then the action's local var is Action.Priority.Ignore, as Lua's oncePerZone leaves it", "1",
              Lua("report(player:getLocalVar(actionUtil.getActionVarName('Samia', 'onTrigger', 9998)))"));
        TalkTo("Samia");
        Check("  the second talk this zone-in is not 9998", "False", EvId().EndsWith(":9998").ToString());
        FinishIfEvent();
        Lua("player:setLocalVar('B3B_MOD', 0)");
        Logout();
        Cleanup(db, id);
        zone.TestQuestScripts.Clear();
        Lua("InteractionGlobal.reload(true)");
    }

    // the fixture's key, any status while B3B_MOD is set: Samia's 9998 with the modifiers ModifierCalls names
    private sealed class ModifierProbeQuest : QuestScript
    {
        public ModifierProbeQuest() : base(FixtureLog, FixtureConst) { }

        private static QuestReply Reply(int i) => i switch
        {
            1 => QuestReply.Event(9998).ReplaceDefault(),
            2 => QuestReply.Event(9998).OncePerZone(),
            3 => QuestReply.Event(9998).ImportantEvent(),
            4 => QuestReply.Event(9998).ImportantOnce(),
            5 => QuestReply.Event(9998).Progress(),
            6 => QuestReply.Event(9998).SetPriority(50),
            7 => QuestReply.Event(9998).OpenDoor(),
            8 => QuestReply.Event(9998).ImportantEvent().SetPriority(20),
            // MessageModifierCalls, SequenceModifierCalls
            21 => QuestReply.MessageSpecial(7000).ReplaceDefault(),
            22 => QuestReply.MessageSpecial(7000).OncePerZone(),
            23 => QuestReply.MessageSpecial(7000).ImportantEvent().SetPriority(20),
            24 => QuestReply.MessageSpecial(7000),
            31 => QuestReply.Sequence(QuestReply.Message(7000)).ReplaceDefault(),
            32 => QuestReply.Sequence(QuestReply.Message(7000)).OncePerZone(),
            33 => QuestReply.Sequence(QuestReply.Message(7000)),
            _ => QuestReply.Event(9998),
        };

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q => q.Player.LocalVar("B3B_MOD") > 0)
                .Npc(Zone.BastokMarkets, "Samia").OnTrigger(q => Reply(q.Player.LocalVar("B3B_MOD")));
        }
    }

    // ---- any-status states, local vars, the npc and settings facets, char-var expiry (quest rework B3b prep) ---------

    // a container quest (the Lua fixture's key) and an NPC-call one (A Discerning Eye) whose any-status states are gated on the
    // player's local var (a check gets no npc); their handlers write each kind of var through the new members, and the Lua side reads them back
    // with the Lua calls (player:getLocalVar, quest:getLocalVar, npc:getLocalVar, char_vars.expiry, xi.settings.main)
    private static void RunAnyStatusAndLocals()
    {
        Console.WriteLine("any-status states, local vars, npc and settings");
        uint id = CharIds[0];
        uint expiry = (uint)(unixNow + 3600);
        int mark = log.Count;
        zone.TestQuestScripts.Add(new LocalsProbeQuest(expiry));
        zone.TestQuestScripts.Add(new CallLocalsQuest());
        Lua("InteractionGlobal.reload(true)");
        Check("both probes register", "", string.Join(" | ", log.Skip(mark).Where(l => l.Contains("not registered") || l.Contains("failed to load"))));
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        Lua("xi.settings.main.B3B_TRUE = true xi.settings.main.B3B_FALSE = false");
        uint samia = NpcId("Samia");
        const string FixtureQuest = "Quest:new(" + FixtureLuaIds + ")";

        // the container quest: the check reads the player's local var
        TalkTo("Samia");
        Check("(container) the gate unset: the any-status state does not match", "False", EvId().EndsWith(":9998").ToString());
        FinishIfEvent();
        Lua("player:setLocalVar('B3B_GATE', 1)");
        string Locals() => Lua($"report(player:getLocalVar('B3B_P') .. '/' .. player:getLocalVar('{FixtureContainer}B3B_Q') .. '/' .. " +
                               $"{FixtureQuest}:getLocalVar(player, 'B3B_Q') .. '/' .. GetNPCByID({samia}):getLocalVar('B3B_N'))");
        void Reset() => Lua($"player:setLocalVar('B3B_P', 0) player:setLocalVar('{FixtureContainer}B3B_Q', 0) GetNPCByID({samia}):setLocalVar('B3B_N', 0) " +
                            "player:setCharVar('B3B_FIN', 0)");
        string[] statuses = { "Available", "Accepted", "Completed" };
        string[] setup = { "", $"player:addQuest({FixtureLuaIds})", $"player:completeQuest({FixtureLuaIds})" };
        for (int i = 0; i < 3; i++)
        {
            if (setup[i] != "") Lua(setup[i]);
            Reset();
            TalkTo("Samia");
            Check($"(container) {statuses[i]}: the any-status state matches, Samia starts 9998", $"{samia}:9998", EvId());
            Check($"  {statuses[i]}: player / quest (raw and quest:getLocalVar) / npc local vars as Lua reads them", "7/8/8/15", Locals());
            Finish();
            Check($"  {statuses[i]}: the event's finish handler has the event's npc (Samia)", samia.ToString(), Var("B3B_FIN"));
        }
        Check("(container) SetCharVar with an expiry: value and char_vars.expiry", $"3 {expiry}/3",
              DbRow("value, expiry", "FROM char_vars WHERE charid = ? AND varname = 'B3B_EXP'") + "/" + Lua("report(player:getCharVar('B3B_EXP'))"));
        Check("  SetCharVar without one: expiry 0", "1 0", DbRow("value, expiry", "FROM char_vars WHERE charid = ? AND varname = 'B3B_NOEXP'"));
        Check("  Npc.Id is npc:getID()", samia.ToString(), Var("B3B_NPC"));
        Check("  Settings.Main: ENABLE_WOTG as xi.settings.main has it, then true 1, false 0, unset 0",
              Lua("report(tostring(xi.settings.main.ENABLE_WOTG))") + "100", Var("B3B_SET"));
        Check("  a solo char: PartySize 1, Alliance the char alone (its main level, distance 0)",
              "1/1/" + Lua("report(player:getMainLvl())") + "/0",
              Lua("report(player:getCharVar('B3B_PARTY') .. '/' .. player:getCharVar('B3B_ALLY') .. '/' .. " +
                  "player:getCharVar('B3B_ALLY_LVL') .. '/' .. player:getCharVar('B3B_ALLY_DIST'))"));

        // the NPC-call quest: the handlers get the call's npc argument
        uint umberto = NpcId("Umberto");
        const string Eye = "Quest:new(xi.questLog.BASTOK, xi.quest.id.bastok.A_DISCERNING_EYE)";
        string Call() => Lua($"report(tostring(xi.cs.quest(player, GetNPCByID({umberto}), 'bastok.a_discerning_eye', 'onTrigger')))");
        Lua("player:setLocalVar('B3B_GATE', 0)");
        Check("(call) the gate unset: false", "false", Call());
        Lua("player:setLocalVar('B3B_GATE', 1)");
        string[] eyeSetup = { "", "player:addQuest(xi.questLog.BASTOK, xi.quest.id.bastok.A_DISCERNING_EYE)", "player:completeQuest(xi.questLog.BASTOK, xi.quest.id.bastok.A_DISCERNING_EYE)" };
        for (int i = 0; i < 3; i++)
        {
            if (eyeSetup[i] != "") Lua(eyeSetup[i]);
            Lua($"player:setLocalVar('Quest[1][71]B3B_Q', 0) GetNPCByID({umberto}):setLocalVar('B3B_N', 0) player:setCharVar('B3B_CFIN', 0)");
            Collect();
            Check($"(call) {statuses[i]}: true, event 473", $"true/{id}:473", Call() + "/" + EvId());
            Check($"  {statuses[i]}: quest (raw and quest:getLocalVar) / npc local vars", "6/6/5",
                  Lua($"report(player:getLocalVar('Quest[1][71]B3B_Q') .. '/' .. {Eye}:getLocalVar(player, 'B3B_Q') .. '/' .. GetNPCByID({umberto}):getLocalVar('B3B_N'))"));
            Lua($"xi.cs.questEvent(player, GetNPCByID({umberto}), 'bastok.a_discerning_eye', 473, 0)");
            Check($"  {statuses[i]}: xi.cs.questEvent's handler has Umberto", umberto.ToString(), Var("B3B_CFIN"));
            // the client ends the event (Umberto's onEventFinish calls only Too Many Chefs, which does not act)
            FinishIfEvent();
        }
        Check("the probes logged no failure", "", string.Join(" | ", log.Skip(mark).Where(l => l.StartsWith("quests: bastok."))));

        Lua("xi.settings.main.B3B_TRUE = nil xi.settings.main.B3B_FALSE = nil player:setLocalVar('B3B_GATE', 0)");
        Logout();
        Cleanup(db, id);
        zone.TestQuestScripts.Clear();
        Lua("InteractionGlobal.reload(true)");
    }

    // the fixture's key: any status while the player's B3B_GATE local var is 1, Samia's trigger writes every kind of
    // var; event 9998's finish (any status) records the handler's npc
    private sealed class LocalsProbeQuest : QuestScript
    {
        private readonly uint expiry_;
        public LocalsProbeQuest(uint expiry) : base(FixtureLog, FixtureConst) { expiry_ = expiry; }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q => q.Player.LocalVar("B3B_GATE") == 1)
                .Npc(Zone.BastokMarkets, "Samia").OnTrigger(q =>
                {
                    q.Player.SetLocalVar("B3B_P", 7);
                    q.Quest.SetLocalVar("B3B_Q", 8);
                    q.Npc.SetLocalVar("B3B_N", q.Player.LocalVar("B3B_P") + q.Quest.LocalVar("B3B_Q"));
                    q.Player.SetCharVar("B3B_NPC", q.Npc.Id);
                    q.Player.SetCharVar("B3B_EXP", 3, expiry_);
                    q.Player.SetCharVar("B3B_NOEXP", 1);
                    q.Player.SetCharVar("B3B_SET", q.Settings.Main("ENABLE_WOTG") * 1000 + q.Settings.Main("B3B_TRUE") * 100 +
                                                   q.Settings.Main("B3B_FALSE") * 10 + q.Settings.Main("B3B_UNSET"));
                    // a solo char's party and alliance (group 4 review): the char alone, its own level, distance 0
                    var alliance = q.Player.Alliance;
                    q.Player.SetCharVar("B3B_PARTY", q.Player.PartySize);
                    q.Player.SetCharVar("B3B_ALLY", alliance.Count);
                    q.Player.SetCharVar("B3B_ALLY_LVL", alliance.Count > 0 ? alliance[0].MainLevel : -1);
                    q.Player.SetCharVar("B3B_ALLY_DIST", alliance.Count > 0 ? (int)System.Math.Round(alliance[0].Distance * 1000) : -1);
                    return QuestReply.Event(9998);
                });
            b.AnyState()
                .OnEventFinish(Zone.BastokMarkets, 9998, q => q.Player.SetCharVar("B3B_FIN", q.Npc?.Id ?? -1));
        }
    }

    // A Discerning Eye (bastok 71), called from Umberto: the same player gate; the event's finish records the handler's npc
    private sealed class CallLocalsQuest : QuestScript
    {
        public CallLocalsQuest() : base(QuestLog.Bastok, "A_DISCERNING_EYE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q => q.Player.LocalVar("B3B_GATE") == 1)
                .Npc(Zone.BastokMarkets, "Umberto").Called().OnTrigger(q =>
                {
                    q.Quest.SetLocalVar("B3B_Q", 6);
                    q.Npc.SetLocalVar("B3B_N", 5);
                    return QuestReply.Event(473);
                });
            b.AnyState()
                .Npc(Zone.BastokMarkets, "Umberto").Called()
                .OnEventFinish(Zone.BastokMarkets, 473, q => q.Player.SetCharVar("B3B_CFIN", q.Npc?.Id ?? -1));
        }
    }

    // A Discerning Eye (bastok 71), Available, Umberto called; the reply is chosen by the char var B3A_MODE
    private sealed class CallKindsQuest : QuestScript
    {
        public CallKindsQuest() : base(QuestLog.Bastok, "A_DISCERNING_EYE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available)
                .Npc(Zone.BastokMarkets, "Umberto").Called()
                .OnTrigger(q => q.Player.CharVar("B3A_MODE") switch
                {
                    1 => QuestReply.MessageText(Text.BastokMarkets.ITEM_OBTAINED, 4),
                    2 => QuestReply.MessageName(Text.BastokMarkets.ITEM_OBTAINED, 4),
                    3 => QuestReply.Event(9998, 5).Option(2, 7),
                    4 => QuestReply.KeyItem(1),
                    5 => throw new InvalidOperationException("boom"),
                    _ => null,
                })
                .OnTrade(q => q.Trade.Matches((640, 2)) ? QuestReply.Event(9996) : null);
        }
    }

    private sealed class CallOnlyStateEventQuest : QuestScript
    {
        public CallOnlyStateEventQuest() : base(QuestLog.Bastok, "A_DISCERNING_EYE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            var st = b.State(QuestStatus.Available);
            st.Npc(Zone.BastokMarkets, "Umberto").Called().OnTrigger(q => QuestReply.Event(9998));
            st.OnEventFinish(Zone.BastokMarkets, 9998, q => q.Quest.Begin());
        }
    }

    private sealed class CallNoRewardQuest : QuestScript
    {
        public CallNoRewardQuest() : base(QuestLog.Bastok, "A_DISCERNING_EYE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Accepted).Npc(Zone.BastokMarkets, "Umberto").Called().OnTrigger(q => q.Quest.Complete() ? QuestReply.NoAction() : null);
        }
    }

    private sealed class CallTwoStatesQuest : QuestScript
    {
        public CallTwoStatesQuest() : base(QuestLog.Bastok, "A_DISCERNING_EYE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Accepted).Npc(Zone.BastokMarkets, "Umberto").Called().OnTrigger(QuestReply.Event(9990));
            b.State(QuestStatus.Available).Npc(Zone.BastokMarkets, "Umberto").Called().OnTrigger(QuestReply.Event(9998));
            b.State(QuestStatus.Available).Npc(Zone.BastokMarkets, "Umberto").Called().OnTrigger(QuestReply.Event(9999));
        }
    }

    // Umberto called with its event finish through the NPC builder, Samia in the container, a state-level event finish too
    private sealed class CallMixedEventQuest : QuestScript
    {
        public CallMixedEventQuest() : base(QuestLog.Bastok, "A_DISCERNING_EYE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available)
                .Npc(Zone.BastokMarkets, "Umberto").Called().OnTrigger(q => QuestReply.Event(9998))
                .OnEventFinish(Zone.BastokMarkets, 9998, q => q.Quest.Begin());
            // a state-level event handler in a state with Called() NPCs is refused, so the container's go in their own state
            b.State(QuestStatus.Available)
                .Npc(Zone.BastokMarkets, "Samia").OnTrigger(QuestReply.Event(9995))
                .OnEventFinish(Zone.BastokMarkets, 9997, q => { });
        }
    }

    // a call-only quest: sets a quest var (Quest[1][71]Probe), completes; the var must be gone
    private sealed class CallCleanupQuest : QuestScript
    {
        public CallCleanupQuest() : base(QuestLog.Bastok, "A_DISCERNING_EYE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Accepted).Npc(Zone.BastokMarkets, "Umberto").Called()
                .OnTrigger(q => { q.Quest.SetVar("Probe", 3); return q.Quest.Complete() ? QuestReply.NoAction() : null; });
        }
    }

    // the fixture's key as two states on one zone-wide handler: A (Prog 0) sets Prog 1, B's guard wants Prog 1
    private sealed class CacheProbeQuest : QuestScript
    {
        public CacheProbeQuest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Accepted, step: 0)
                .OnEventFinish(Zone.BastokMarkets, 9999, q => q.Quest.SetStep(1));
            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 1)
                .OnEventFinish(Zone.BastokMarkets, 9999, q => q.Quest.SetVar("B", 1));
        }
    }

    // the Lua fixture (Fixtures/QSL_Fixture.lua) as its Lua file has it at Brutus and Tebhi, in C#: the switch and
    // reload checks run on it, its Lua file existing. One state per Lua section: Brutus's onTrigger in each
    // (FixtureTriggers entries), Tebhi's onTrade in the accepted one, and the Upper Jeuno
    // finishes 141 / 143 / 135 / 144
    private sealed class FixtureSwitchQuest : QuestScript
    {
        public FixtureSwitchQuest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 4, 5);
            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedQuest(QuestLog.Jeuno, "WINGS_OF_GOLD"))
                .Npc(FixtureZone, FixtureNpc).OnTrigger(QuestReply.ProgressEvent(141))
                .OnEventFinish(FixtureZone, 141, q => q.Quest.Begin())
                .OnEventFinish(FixtureZone, 143, q => q.Quest.Begin());
            b.State(QuestStatus.Accepted)
                .Npc(FixtureZone, FixtureNpc).OnTrigger(QuestReply.Event(142))
                .Npc(FixtureTradeZone, FixtureTradeNpc)
                    .OnTrade(q => q.Trade.HasExactly(Item.BeastCollar) ? QuestReply.MessageSpecial(7000) : null)
                .OnEventFinish(FixtureZone, 135, q => q.Quest.Complete())
                .OnEventFinish(FixtureZone, 144, q => q.Quest.SetStep(4));
            b.State(QuestStatus.Completed)
                .Npc(FixtureZone, FixtureNpc).OnTrigger(QuestReply.Event(151));
        }
    }

    // the fixture's key, Available: a Bastok Markets trigger area answering with an event, another with a text (refused)
    private sealed class TriggerAreaReplyQuest : QuestScript
    {
        public const int Area = 901, TextArea = 902;
        public TriggerAreaReplyQuest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available)
                .OnTriggerAreaEnter(Zone.BastokMarkets, Area, q => q.TriggerAreaId == Area ? QuestReply.ProgressEvent(9995) : null)
                .OnTriggerAreaEnter(Zone.BastokMarkets, TextArea, q => QuestReply.MessageText("ITEM_OBTAINED"));
        }
    }

    // the fixture's key, Available: Samia starts event 9998 with 5 and the option { [2] = 7 }
    private sealed class OptionsProbeQuest : QuestScript
    {
        public OptionsProbeQuest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available)
                .Npc(Zone.BastokMarkets, "Samia").OnTrigger(QuestReply.Event(9998, 5).Option(2, 7));
        }
    }

    // the fixture's key, Available with a check that tries an effect
    private sealed class EffectCheckQuest : QuestScript
    {
        public EffectCheckQuest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available, when: q => { ((IQuestPlayer)q.Player).AddFame(1, 10); return true; })
                .Npc(Zone.BastokMarkets, "Samia").OnTrigger(QuestReply.Event(9998));
        }
    }

    private sealed class SequenceProbeQuest : QuestScript
    {
        public SequenceProbeQuest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available).Npc(Zone.BastokMarkets, "Samia").OnTrigger(QuestReply.Sequence(
                QuestReply.MessageText(Text.BastokMarkets.ITEM_OBTAINED), QuestReply.Face(64), QuestReply.Wait(100),
                QuestReply.MessageText(Text.BastokMarkets.GIL_OBTAINED)));
        }
    }

    private sealed class OtherZoneTextQuest : QuestScript
    {
        public OtherZoneTextQuest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available).Npc(Zone.BastokMarkets, "Samia").OnTrigger(QuestReply.MessageSpecial(Text.Metalworks.YOU_ACCEPT_THE_MISSION));
        }
    }

    private sealed class TopLevelFaceQuest : QuestScript
    {
        public TopLevelFaceQuest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available).Npc(Zone.BastokMarkets, "Samia").OnTrigger(QuestReply.Face(64));
        }
    }

    private sealed class FaceOnlySequenceQuest : QuestScript
    {
        public FaceOnlySequenceQuest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available).Npc(Zone.BastokMarkets, "Samia").OnTrigger(QuestReply.Sequence(QuestReply.Face(64), QuestReply.Wait(100)));
        }
    }

    private sealed class ReturnedWaitQuest : QuestScript
    {
        public ReturnedWaitQuest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available).Npc(Zone.BastokMarkets, "Samia").OnTrigger(q => QuestReply.Wait(100));
        }
    }

    private sealed class DynamicEntityQuest : QuestScript
    {
        public DynamicEntityQuest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Accepted).Npc(Zone.GrandPalaceOfHuxzoi, "Quasilumin").OnMobDeath(q => q.Quest.SetStep(1));
        }
    }

    // a test-only quest naming a mobs.yaml spawn that has no placement (Mighrah in Abyssea - Misareaux)
    private sealed class UnplacedMobQuest : QuestScript
    {
        public UnplacedMobQuest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Accepted)
                .Npc(Zone.AbysseaMisareaux, "Mighrah").OnMobDeath(q => q.Quest.SetStep(1));
        }
    }

    // the fixture's key with an NPC Bastok Markets does not have: registered through ZoneHost.TestQuestScripts
    private sealed class BrokenFixtureQuest : QuestScript
    {
        public BrokenFixtureQuest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available)
                .Npc(Zone.BastokMarkets, "Nobody_Here").OnTrigger(QuestReply.ProgressEvent(240));
        }
    }

    // a quest with no constant name: QuestKeyRegistry.KeyFor throws on it, before the registration's own checks
    private sealed class ThrowingQuest : QuestScript
    {
        public ThrowingQuest() : base(QuestLog.Bastok, null) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available)
                .Npc(Zone.BastokMarkets, "Michea").OnTrigger(QuestReply.Event(215));
        }
    }

    // a test-only quest (not in XI.QuestContent, so never discovered): its NPC does not exist
    private sealed class BrokenQuest : QuestScript
    {
        public BrokenQuest() : base(QuestLog.Bastok, "THE_ELVAAN_GOLDSMITH") { }
        public override string LegacyPath => ElvaanLuaFile;

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available)
                .Npc(Zone.BastokMarkets, "Nobody_Here").OnTrigger(QuestReply.Event(215));
        }
    }
}
