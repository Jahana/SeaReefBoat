using System;
using System.Collections.Generic;
using System.Linq;
using XI.Common;
using XI.Persistence;
using XI.QuestContent;
using XI.Zone;

// lane engine-small-2: the luautils globals the GM commands reach (SetTimeOffset, ReloadSynthRecipes,
// ReloadExperienceData, GetSynergyRecipeByID / ByTrade, Terminate, ForceCrash) and the slow query lines
// (logging.SQL_SLOW_QUERY_*) on the lane's database
internal static partial class Program
{
    private static void RunEngineSmall2()
    {
        Console.WriteLine("\n== engine-small-2: luautils admin globals, the slow query log ==");
        RunSetTimeOffset();
        RunOffsetLeavesSteadyTimes();
        RunJoinTimeOnScriptClock();
        RunReloadSynthRecipes();
        RunReloadExperienceData();
        RunSynergyRecipes();
        RunTerminateAndForceCrash();
        RunSlowQueryLog();
    }

    // 1. SetTimeOffset(s): earth_time::reset_offset then add_offset. The process's Earth clock (EarthTime) and the
    // clocks that default to it (the lifts' UnixNowMs here; the world's are pinned) move by s seconds; a second call
    // replaces the first offset, 0 takes it away
    private static void RunSetTimeOffset()
    {
        try
        {
            Lua("SetTimeOffset(3600)");
            Check("SetTimeOffset(3600): the Earth clock's offset is an hour", "3600000", EarthTime.OffsetMs.ToString());
            long ahead = EarthTime.UnixSec() - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Check("  EarthTime reads an hour ahead of the system clock", "True",
                  (ahead >= 3599 && ahead <= 3601).ToString());
            long liftAhead = zone.UnixNowMs() - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Check("  a host clock left at its default (the lifts') too", "True",
                  (liftAhead >= 3599000 && liftAhead <= 3601000).ToString());
            // a host of its own (its own loader: a host takes over its loader's hooks), the 0x020 hook given back
            var attrOverride = GameOkPackets.AttrOverride;
            var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
            var fresh = new ZoneHost(db, new CharLoader(db, Grades.Load(dataDir_),
                AbilityTable.Load(db, _ => true, 1.0), stats), dataDir_, true, _ => { });
            GameOkPackets.AttrOverride = attrOverride;
            long hostAhead = fresh.UnixNow() - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Check("  ZoneHost.UnixNow's default too (EarthTime)", "True",
                  (hostAhead >= 3599 && hostAhead <= 3601).ToString());
            Lua("SetTimeOffset(-120)");
            Check("SetTimeOffset(-120) after it: reset first, so -120 s", "-120000", EarthTime.OffsetMs.ToString());
            Lua("SetTimeOffset(0)");
            Check("SetTimeOffset(0): no offset", "0", EarthTime.OffsetMs.ToString());
        }
        finally
        {
            EarthTime.ResetOffset();
        }
    }

    // 1b. SetTimeOffset moves the Earth clock only: the play time (GetPlayTime counts on timer::now()) and the time
    // since a member last joined the party (m_LeaderCreatedPartyTime, a timer::time_point read through to_utc) stay
    // as they were. The host's Earth clocks are left at their EarthTime defaults for this, so an offset reaches them
    private static void RunOffsetLeavesSteadyTimes()
    {
        uint id = CharIds[2], other = CharIds[3];
        Cleanup(db, id);
        Cleanup(db, other);
        Fixture(other, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        var (unixNow, unixNowMs, scriptMs) = (zone.UnixNow, zone.UnixNowMs, scripts.UnixNowMs);
        zone.UnixNow = EarthTime.UnixSec;
        zone.UnixNowMs = EarthTime.UnixMs;
        scripts.UnixNowMs = EarthTime.UnixMs;
        Login();
        var s2 = LoginOther(other);
        var party = new ZoneHost.Party(zone, c);
        try
        {
            party.AddMember((CharData)s2.Char);
            Tick(3000);
            string Times() => Lua("report(player:getPlaytime()) " +
                                  "report(GetSystemTime() - player:getPartyLastMemberJoinedTime())");
            string before = Times();
            Check("SetTimeOffset and the steady times: 3 s after the login and the join", "3,3", before);
            Lua("SetTimeOffset(-7200)");
            Check("  SetTimeOffset(-7200): the play time and the time since the join unchanged", before, Times());
            Lua("SetTimeOffset(7200)");
            Check("  SetTimeOffset(7200): unchanged", before, Times());
        }
        finally
        {
            EarthTime.ResetOffset();
            party.DisbandParty();
            zone.OnCharTimeout(s2, false);
            Logout();
            (zone.UnixNow, zone.UnixNowMs, scripts.UnixNowMs) = (unixNow, unixNowMs, scriptMs);
            Cleanup(db, id);
            Cleanup(db, other);
        }
    }

    // 1c. getPartyLastMemberJoinedTime converts on the wall clock GetSystemTime reads: with the zone's and the
    // script engine's Earth clocks pinned a day back (as the harness pins them) and the lift clock
    // (ZoneHost.UnixNowMs) left real, the time since the join is still the 3 s that passed
    private static void RunJoinTimeOnScriptClock()
    {
        uint id = CharIds[2], other = CharIds[3];
        Cleanup(db, id);
        Cleanup(db, other);
        Fixture(other, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        var (unixNow, unixNowMs, scriptMs) = (zone.UnixNow, zone.UnixNowMs, scripts.UnixNowMs);
        long pinned = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 86400;
        zone.UnixNow = () => pinned;
        scripts.UnixNowMs = () => pinned * 1000;
        zone.UnixNowMs = EarthTime.UnixMs;
        Login();
        var s2 = LoginOther(other);
        var party = new ZoneHost.Party(zone, c);
        try
        {
            party.AddMember((CharData)s2.Char);
            Tick(3000);
            Check("getPartyLastMemberJoinedTime on GetSystemTime's clock (the lift clock real): 3 s since the join",
                  "3", Lua("report(GetSystemTime() - player:getPartyLastMemberJoinedTime())"));
        }
        finally
        {
            party.DisbandParty();
            zone.OnCharTimeout(s2, false);
            Logout();
            (zone.UnixNow, zone.UnixNowMs, scripts.UnixNowMs) = (unixNow, unixNowMs, scriptMs);
            Cleanup(db, id);
            Cleanup(db, other);
        }
    }

    // a synth_recipes row copied from the first one, its id and Ingredient8 changed so its ingredient key is new
    private const uint ExtraRecipeId = 999901;

    // 2. ReloadSynthRecipes: synth_recipes read again. A row added to the table is in the reloaded recipes, a row
    // taken away is gone; "Loading synth recipes" logged each time
    private static void RunReloadSynthRecipes()
    {
        var cols = new List<string>();
        var r = Sql(db, "SELECT column_name AS c FROM information_schema.columns WHERE table_schema = DATABASE() " +
                        "AND table_name = 'synth_recipes' ORDER BY ordinal_position");
        while (r != null && r.Next()) cols.Add(r.Get<string>("c"));
        string select = string.Join(", ", cols.Select(col => col switch
        {
            "ID" => ExtraRecipeId.ToString(),
            "Ingredient8" => "4095",
            _ => $"`{col}`",
        }));
        Sql(db, $"DELETE FROM synth_recipes WHERE ID = {ExtraRecipeId}");
        try
        {
            int before = zone.SynthRecipeCount;
            Sql(db, $"INSERT INTO synth_recipes ({string.Join(", ", cols.Select(col => $"`{col}`"))}) " +
                    $"SELECT {select} FROM synth_recipes ORDER BY ID LIMIT 1");
            Check("ReloadSynthRecipes: before it, the added row is not loaded", before, zone.SynthRecipeCount);
            int mark = log.Count;
            Lua("ReloadSynthRecipes()");
            Check("  after it, one recipe more", before + 1, zone.SynthRecipeCount);
            Check("  \"Loading synth recipes\" logged", "Loading synth recipes",
                  string.Join(" | ", log.Skip(mark).Where(l => l.Contains("synth recipes"))));
            Sql(db, $"DELETE FROM synth_recipes WHERE ID = {ExtraRecipeId}");
            Lua("ReloadSynthRecipes()");
            Check("  the row taken away and reloaded: the count as before", before, zone.SynthRecipeCount);
        }
        finally
        {
            Sql(db, $"DELETE FROM synth_recipes WHERE ID = {ExtraRecipeId}");
            Lua("ReloadSynthRecipes()");
        }
    }

    // 3. ReloadExperienceData: charutils::LoadExpTable again, exp_base's experience to the next level read again
    private static void RunReloadExperienceData()
    {
        var growth = loader_.Stats.Growth;
        string Exp5() => DbRow("exp", "FROM exp_base WHERE level = 6 AND ? IS NOT NULL");
        string old = Exp5();
        try
        {
            Check("ReloadExperienceData: level 5's experience to the next level is exp_base's", old,
                  growth.GetExpNextLevel(5).ToString());
            Sql(db, "UPDATE exp_base SET exp = exp + 7 WHERE level = 6");
            Check("  exp_base changed: not seen before the reload", old, growth.GetExpNextLevel(5).ToString());
            Lua("ReloadExperienceData()");
            Check("  after it, the new value", (uint.Parse(old) + 7).ToString(), growth.GetExpNextLevel(5).ToString());
        }
        finally
        {
            Sql(db, "UPDATE exp_base SET exp = ? WHERE level = 6", BoundValue.Of(uint.Parse(old)));
            Lua("ReloadExperienceData()");
        }
        Check("  restored", old, growth.GetExpNextLevel(5).ToString());
    }

    // the Galley Kitchen recipe (synergy_recipes id 3): one each of items 9079, 9080 and 9081
    private const int SynergyRecipe = 3, SynergyA = 9079, SynergyB = 9080, SynergyC = 9081;

    // 4. GetSynergyRecipeByID(id): the synergy_recipes row as a table, nil for an id without one.
    // GetSynergyRecipeByTrade(trade): the trade's item ids sorted (here offered in another order) against
    // ingredient1..8
    private static void RunSynergyRecipes()
    {
        var row = Sql(db, "SELECT result, resultName, ingredient1, cost_fire_fewell, primary_skill " +
                          $"FROM synergy_recipes WHERE id = {SynergyRecipe}");
        string expected = row != null && row.Next()
            ? $"{SynergyRecipe}/{row.Get<uint>("result")}/{row.Get<string>("resultName")}/" +
              $"{row.Get<uint>("ingredient1")}/{row.Get<uint>("cost_fire_fewell")}/{row.Get<uint>("primary_skill")}"
            : "no row";
        Check("GetSynergyRecipeByID(3): the row's fields", expected,
              Lua($"local r = GetSynergyRecipeByID({SynergyRecipe}) report(string.format('%d/%d/%s/%d/%d/%d', r.id, " +
                  "r.result, r.resultName, r.ingredient1, r.cost_fire_fewell, r.primary_skill))"));
        Check("  an id without a row: nil", "nil", Lua("report(tostring(GetSynergyRecipeByID(999999)))"));

        uint id = CharIds[2];
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0, (SynergyC, 1), (SynergyA, 1), (SynergyB, 1));
        Login();
        var npc = Here.Npcs.Where(n => n.Status == NpcEntity.StatusNormal && n.Name.Length > 0)
            .OrderBy(n => (n.X - c.X) * (n.X - c.X) + (n.Z - c.Z) * (n.Z - c.Z)).First();
        try
        {
            Lua(@"
                __es2found = 'none'
                __es2onTrade = InteractionGlobal.onTrade
                InteractionGlobal.onTrade = function(player, npc, trade, fallback)
                    local r = GetSynergyRecipeByTrade(trade)
                    __es2found = r and tostring(r.id) or 'nil'
                end");
            MoveTo(npc.X + 1.5f, npc.Y, npc.Z);
            TradeItems(npc.Id, npc.TargId, (1, 1), (2, 1), (3, 1));
            Check("GetSynergyRecipeByTrade: 9081, 9079 and 9080 offered: recipe 3", SynergyRecipe.ToString(),
                  Lua("report(__es2found)"));
            TradeItems(npc.Id, npc.TargId, (1, 1), (2, 1));
            Check("  two of the three: nil", "nil", Lua("report(__es2found)"));
        }
        finally
        {
            Lua("if __es2onTrade then InteractionGlobal.onTrade = __es2onTrade end " +
                "__es2onTrade = nil __es2found = nil");
            Logout();
            Cleanup(db, id);
        }
    }

    // GP_CLI_COMMAND_ITEM_TRANSFER with several items: each (inventory slot, quantity) in the next trade slot
    private static void TradeItems(uint id, ushort targId, params (byte Slot, uint Qty)[] items)
    {
        Collect();
        Send(0x036, 64, b =>
        {
            BitConverter.GetBytes(id).CopyTo(b, 4);
            for (int i = 0; i < items.Length; i++)
            {
                BitConverter.GetBytes(items[i].Qty).CopyTo(b, 8 + 4 * i);
                b[48 + i] = items[i].Slot;
            }
            BitConverter.GetBytes(targId).CopyTo(b, 58);
            b[60] = (byte)items.Length;
        });
    }

    // 5. Terminate: each char of the process written (here its hp, by SaveCharStats), then the exit hook with 1
    // (XI.Host ends the process there). ForceCrash: the crash hook; a host without one raises a Lua error
    private static void RunTerminateAndForceCrash()
    {
        uint id = CharIds[2];
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        var exits = new List<int>();
        int crashes = 0;
        try
        {
            c.Hp = 123;
            Check("Terminate: before it, char_stats hp is the fixture's", "300",
                  DbRow("hp", "FROM char_stats WHERE charid = ?"));
            zone.ExitProcess = code => exits.Add(code);
            Lua("Terminate()");
            Check("  the char's stats written", "123", DbRow("hp", "FROM char_stats WHERE charid = ?"));
            Check("  the exit hook called with 1 (std::exit(1))", "1", string.Join(",", exits));
            exits.Clear();
            // a char without an effects table (persist::flush writes the pending position all the same)
            var table = c.StatusEffects.Table;
            c.StatusEffects.Table = null;
            c.X = -290.5f;
            c.Persist |= CharPersist.Position;
            Lua("Terminate()");
            c.StatusEffects.Table = table;
            Check("  a char without an effects table: its pending position written", "-290.5",
                  DbRow("pos_x", "FROM chars WHERE charid = ?"));
            zone.ExitProcess = null;
            int mark = log.Count;
            Lua("Terminate()");
            Check("  with no exit hook: logged, the process goes on", "True",
                  log.Skip(mark).Any(l => l.Contains("Terminate: the chars are saved; this host has no exit hook"))
                      .ToString());

            zone.ForceCrash = () => crashes++;
            Lua("ForceCrash()");
            Check("ForceCrash: the crash hook called", "1", crashes.ToString());
            zone.ForceCrash = null;
            string err = Lua("local ok, err = pcall(ForceCrash) report(tostring(ok) .. ' ' .. tostring(err))");
            Check("  with no crash hook: a Lua error", "True",
                  (err.StartsWith("false ") && err.Contains("ForceCrash: this host has no crash handler")).ToString());
        }
        finally
        {
            zone.ExitProcess = null;
            zone.ForceCrash = null;
            Logout();
            Cleanup(db, id);
        }
    }

    // 6. makeQueryTimer: with the timers on, a statement over SQL_SLOW_QUERY_ERROR_TIME logs an error line, one over
    // the warning time a warning, a quick one nothing; with them off (before db::enableTimers) nothing
    private static void RunSlowQueryLog()
    {
        if (db is not MySqlDatabase my)
        {
            Check("slow query log: the suite's database is a MySqlDatabase", "True", "False");
            return;
        }
        var warnings = new List<string>();
        var errors = new List<string>();
        my.LogWarning = warnings.Add;
        my.LogError = errors.Add;
        // margins wide enough for a loaded database: 0.3 s against 50 ms (and 10 s), a quick one against 10 s
        my.SlowQueryWarningMs = 50;
        my.SlowQueryErrorMs = 100;
        try
        {
            Sql(db, "SELECT SLEEP(0.3)");
            Check("slow query log: timers off: nothing", "0/0", $"{warnings.Count}/{errors.Count}");
            my.QueryTimers = true;
            Sql(db, "SELECT SLEEP(0.3)");
            Check("  0.3 s over the error time (100 ms): an error line", "True",
                  errors.Count == 1 && errors[0].StartsWith("SQL query took ") &&
                  errors[0].EndsWith("ms: SELECT SLEEP(0.3)") && warnings.Count == 0 ? "True"
                      : $"{string.Join(" | ", errors)} / {string.Join(" | ", warnings)}");
            my.SlowQueryErrorMs = 10000;
            Sql(db, "SELECT SLEEP(0.3)");
            Check("  0.3 s over the warning time (50 ms), under the error time (10 s): a warning line", "True",
                  warnings.Count == 1 && warnings[0].EndsWith("ms: SELECT SLEEP(0.3)") && errors.Count == 1 ? "True"
                      : $"{string.Join(" | ", errors)} / {string.Join(" | ", warnings)}");
            my.SlowQueryWarningMs = 10000;
            my.SlowQueryErrorMs = 20000;
            Sql(db, "SELECT 1");
            Check("  a quick one (under 10 s): nothing", "1/1", $"{warnings.Count}/{errors.Count}");
            my.SlowQueryWarningMs = 50;
            my.SlowQueryErrorMs = 100;
            my.SlowQueryLog = false;
            Sql(db, "SELECT SLEEP(0.3)");
            Check("  SQL_SLOW_QUERY_LOG_ENABLE off: nothing", "1/1", $"{warnings.Count}/{errors.Count}");
        }
        finally
        {
            my.QueryTimers = false;
            my.SlowQueryLog = true;
            my.LogWarning = null;
            my.LogError = null;
        }
    }
}
