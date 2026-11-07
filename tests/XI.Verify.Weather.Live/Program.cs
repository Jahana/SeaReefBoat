using System;
using System.Collections.Generic;
using System.IO;
using XI.Common;
using XI.Map;
using XI.Ipc;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Weather.Live -- live gate for the zone weather (ZoneHost.Weather: CZone::LoadZoneWeather, zoneutils::
// InitializeWeather, CZone::UpdateWeather on the zone tick, CZone::SetWeather and its 0x057, CZoneEntities::
// WeatherChange's listeners and CheckLatentsWeather, SpawnHandler::onWeatherChange, luautils::OnZoneWeatherChange and
// the setWeather / getWeather / getZone / GetZone bindings) in Jugner Forest, whose zone_weather mixes sunshine,
// clouds, rain and thunder, with a rain hat (item_latents: water weather, REGEN+1) against the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 991350;
    private const ushort Jugner = 104, SouthernSandoria = 230;
    private const ushort RainHat = 15220;
    private const byte SlotHead = 4;
    private const ushort Sunshine = 1, Fog = 3, HotSpell = 4, Rain = 6;
    private const int Cycle = 2160;

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static ResultSet Sql(IDatabase db, string q, params BoundValue[] a) => db.Execute(q, a);

    // every table with a charid column, read from the schema at run time: chars' char_insert trigger fills char_equip, char_exp,
    // char_history, char_inventory, char_jobs, char_pet, char_points, char_profile, char_storage and char_unlocks and only
    // char_delete clears them, so a run aborted with one of them left behind would make the next run's INSERT INTO chars collide
    private static List<string> CharidTables(IDatabase db)
    {
        var tables = new List<string>();
        var r = Sql(db, "SELECT table_name AS t FROM information_schema.columns WHERE table_schema = DATABASE() AND column_name = 'charid' AND table_name <> 'chars'");
        while (r != null && r.Next()) tables.Add(r.Get<string>("t"));
        foreach (var t in new[] { "char_equip", "char_exp", "char_history", "char_inventory", "char_jobs", "char_pet", "char_points", "char_profile",
                                  "char_storage", "char_unlocks" })
            if (!tables.Contains(t)) tables.Add(t);
        return tables;
    }

    private static void Cleanup(IDatabase db)
    {
        foreach (var t in CharidTables(db))
            Sql(db, $"DELETE FROM `{t}` WHERE charid = ?", BoundValue.Of(CharId));
        Sql(db, "DELETE FROM chars WHERE charid = ?", BoundValue.Of(CharId));
        Sql(db, "DELETE FROM accounts WHERE id = ?", BoundValue.Of(CharId));
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

    private static int Main(string[] argv)
    {
        int dbPort = argv.Length > 0 ? int.Parse(argv[0]) : 3307;
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

        Console.WriteLine($"{_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    private static int Opcode(BasicPacket p) => p.Buffer[0] | (p.Buffer[1] & 1) << 8;

    // the zone's zone_weather blob as the test reads it: the raw day values
    private static ushort[] Blob(IDatabase db, ushort zone)
    {
        var r = Sql(db, "SELECT weather FROM zone_weather WHERE zone = ? LIMIT 1", BoundValue.Of(zone));
        var days = new ushort[Cycle];
        if (r == null || !r.Next()) return days;
        var b = r.GetBlobBytes("weather");
        for (int i = 0; i < Cycle && i * 2 + 1 < b.Length; i++) days[i] = (ushort)(b[i * 2] | b[i * 2 + 1] << 8);
        return days;
    }

    // normal/common/rare of the last nonzero day on or before the day
    private static (int Normal, int Common, int Rare) Entry(ushort[] days, int day)
    {
        (int, int, int) e = (0, 0, 0);
        for (int i = 0; i <= day; i++)
            if (days[i] != 0) e = (days[i] >> 10, days[i] >> 5 & 0x1F, days[i] & 0x1F);
        return e;
    }

    private static string Show(ZoneWeather w) => $"{w.Normal}/{w.Common}/{w.Rare}";

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        Console.WriteLine("The zone_weather blob");
        var jugner = Blob(db, Jugner);
        int nonzero = 0;
        foreach (var v in jugner) if (v != 0) nonzero++;
        // a day whose weathers (the rare is the common in Jugner) take in rain and sunshine or clouds
        int day = -1, rainChance = -1, clearChance = -1;
        ushort clear = 0;
        for (int d = 0; d < Cycle && day < 0; d++)
        {
            var (n, cm, ra) = Entry(jugner, d);
            var picks = new[] { (ra, 0), (cm, 20), (n, 60) }; // the chance rolls that pick rare, common, normal
            int rc = -1, cc = -1;
            ushort cw = 0;
            foreach (var (w, chance) in picks)
            {
                if (w == Rain) rc = chance;
                else if (w != 0 && w < HotSpell && cc < 0) { cc = chance; cw = (ushort)w; }
            }
            if (rc < 0 || cc < 0) continue;
            day = d;
            rainChance = rc;
            clearChance = cc;
            clear = cw;
        }
        Check("Jugner Forest has a day with rain and clear weather", true, day >= 0);
        if (day < 0) return;
        var entry = Entry(jugner, day);
        Console.WriteLine($"  day {day} of the cycle: normal/common/rare {entry.Normal}/{entry.Common}/{entry.Rare}");

        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("weatherverify"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Weathery"), BoundValue.Of(Jugner), BoundValue.Of((ushort)0),
            BoundValue.Of(2000.0f), BoundValue.Of(0.0f), BoundValue.Of(2000.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 300, 50, 1, 75)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 75)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));
        Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?, 0, 1, ?, 1, ?)",
            BoundValue.Of(CharId), BoundValue.Of(RainHat), BoundValue.Of(new byte[24]));

        // noon of that day, a few cycles in
        long dayStart = (Cycle * 5L + day) * VanaTime.DAY;
        long vana = dayStart + 12 * VanaTime.HOUR;
        const long unix = 1_700_000_000;
        uint stamp = (uint)(unix - VanaTime.EpochUnixSec);

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        zone.UnixNow = () => unix;
        zone.VanaTicksNow = () => vana;
        zone.MobRng = new Random(7);
        zone.SendIpc = _ => { };
        // xirand::GetRandomNumber: the rolls the test queues, recorded
        var rolls = new Queue<int>();
        var calls = new List<string>();
        zone.WeatherRandom = (min, max) =>
        {
            calls.Add($"{min}-{max}");
            return rolls.Count > 0 ? rolls.Dequeue() : min;
        };
        string Calls()
        {
            var r = string.Join(",", calls);
            calls.Clear();
            return r;
        }
        void Roll(params int[] values)
        {
            rolls.Clear();
            foreach (var v in values) rolls.Enqueue(v);
        }
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);

        Console.WriteLine("The load: LoadZoneWeather and InitializeWeather's first UpdateWeather");
        Roll(600, clearChance, 9);
        var info = zone.ZoneInfoOf(Jugner);
        var weather = zone.WeatherOf(Jugner);
        Check("an entry for each nonzero day, so not static", $"{nonzero}/False", $"{weather.Count}/{weather.IsStatic}");
        Check("entryForDay(day) is the blob's", $"{entry.Normal}/{entry.Common}/{entry.Rare}", Show(weather.EntryForDay((ushort)day)));
        bool allMatch = true;
        for (int d = 0; d < Cycle; d += 37)
        {
            var e = Entry(jugner, d);
            if (Show(weather.EntryForDay((ushort)d)) != $"{e.Normal}/{e.Common}/{e.Rare}") allMatch = false;
        }
        Check("entryForDay matches the blob across the cycle", true, allMatch);
        Check("the rolls: next update, chance, then the packet's offset", "180-1801,0-100,4-28", Calls());
        Check("the clear weather picked, changed now", $"{clear}/{stamp}", $"{info.Weather}/{info.WeatherChangeTime}");
        Check("the next update 600 s on", now + 600_000, weather.NextUpdateMs);
        var mobs = new List<MobEntity>(info.Mobs);
        var waterElemental = mobs.Find(m => m.Name == "Water_Elemental");
        var thunderElemental = mobs.Find(m => m.Name == "Thunder_Elemental");
        var wisp = mobs.Find(m => m.Name == "Will-o-the-Wisp");
        var tree = mobs.Find(m => m.Name == "Walking_Tree");
        Check("the weather mobs are there", "True/True/True/True", $"{waterElemental != null}/{thunderElemental != null}/{wisp != null}/{tree != null}");
        if (waterElemental == null || thunderElemental == null || wisp == null || tree == null) return;
        Check("the water elemental's element is rain's", 6, waterElemental.Element);
        Check("clear weather: the elementals and the wisp despawn in 1 s, the tree is left alone",
              $"{now + 1000}/{now + 1000}/{now + 1000}/0",
              $"{waterElemental.DespawnAtMs}/{thunderElemental.DespawnAtMs}/{wisp.DespawnAtMs}/{tree.DespawnAtMs}");

        Console.WriteLine("Zone-in: the 0x00A carries the weather, no 0x057");
        var s = new MapSession { CharId = CharId, AccountId = CharId };
        zone.OnLogin(s);
        var c = (CharData)s.Char;
        void Send(ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(s, opcode, new BasicPacket(buf));
            zone.AfterParse(s);
        }
        List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);
        string Weather057()
        {
            var l = Of(0x057);
            return string.Join(" ", l.ConvertAll(p =>
                $"{(p.Buffer[1] >> 1) * 4}:{BitConverter.ToUInt32(p.Buffer, 4)}:{BitConverter.ToUInt16(p.Buffer, 8)}:{BitConverter.ToUInt16(p.Buffer, 10)}"));
        }
        Send(0x00A, 92, buf => BitConverter.GetBytes(CharId).CopyTo(buf, 12));
        var login = Of(0x00A);
        Check("one login packet", 1, login.Count);
        if (login.Count == 1)
            Check("WeatherNumber / WeatherTime", $"{clear}/{stamp}",
                  $"{BitConverter.ToUInt16(login[0].Buffer, 104)}/{BitConverter.ToUInt32(login[0].Buffer, 108)}");
        Check("no 0x057 at zone-in", "", Weather057());
        Send(0x00C, 12);
        s.PacketList.Clear();

        Console.WriteLine("The rain hat: a water weather latent");
        List<LatentEffect> Head() => new List<LatentEffect>(c.Latents.All).FindAll(l => l.Slot == SlotHead);
        string Active() => string.Join(" ", Head().ConvertAll(l => $"{(ushort)l.ModValue}:{l.ConditionsId}:{l.ConditionsValue}:{(l.Activated ? 1 : 0)}"));
        short regenBare = c.GetMod(Mod.REGEN);
        Send(0x050, 8, b => { b[4] = 1; b[5] = SlotHead; b[6] = 0; });
        Check("the rain hat on", RainHat, c.Equipped[SlotHead]?.ItemId ?? 0);
        Check("its latent off in clear weather", "370:52:6:0/0", $"{Active()}/{c.GetMod(Mod.REGEN) - regenBare}");
        s.PacketList.Clear();

        Console.WriteLine("The scheduled change on the zone tick");
        now += 599_999;
        zone.Tick();
        Check("not due yet: no roll, no change", $"/{clear}/", $"{Calls()}/{info.Weather}/{Weather057()}");
        now += 1;
        Roll(900, rainChance, 17);
        zone.Tick();
        Check("due: the rolls", "180-1801,0-100,4-28", Calls());
        Check("rain", Rain, info.Weather);
        Check("the 0x057 to the char: size, StartTime, WeatherNumber, WeatherOffsetTime", $"12:{stamp}:{Rain}:17", Weather057());
        Check("the next update 900 s on", now + 900_000, weather.NextUpdateMs);
        Check("CheckLatentsWeather: the rain hat's REGEN on", "370:52:6:1/1", $"{Active()}/{c.GetMod(Mod.REGEN) - regenBare}");
        Check("rain: the thunder elemental and the wisp despawn, the water elemental stays",
              $"{now + 1000}/{now + 1000}/True",
              $"{thunderElemental.DespawnAtMs}/{wisp.DespawnAtMs}/{waterElemental.DespawnAtMs != now + 1000}");
        // the spawn handler brings the water elementals up in the rain
        for (int i = 0; i < 4; i++)
        {
            now += 10_000;
            zone.Tick();
        }
        var upWater = mobs.FindAll(m => m.Name == "Water_Elemental" && m.IsSpawned);
        Check("a water elemental up in the rain", true, upWater.Count > 0);
        s.PacketList.Clear();

        Console.WriteLine("Lua: setWeather / getWeather / getZone / GetZone and the listeners");
        var lua = scripts.Lua;
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local player = GetPlayerByID({CharId})\n" + code, "=verify_weather");
            return string.Join(",", got);
        }
        Check("getWeather, getZone():getWeather(), GetZone(104):getWeather()", $"{Rain},{Rain},{Rain}",
              Lua("report(player:getWeather()) report(player:getZone():getWeather()) report(GetZone(104):getWeather())"));
        Check("getZone():getID() / getName(), GetZone of no zone", "104,Jugner_Forest,nil",
              Lua("report(player:getZone():getID()) report(player:getZone():getName()) report(tostring(GetZone(4000)))"));
        Lua("player:addListener('WEATHER_CHANGE', 'VERIFY_WEATHER', function(entity, weather, element) " +
            "report(string.format('%s:%d:%d:%d', entity:getName(), weather, element, entity:getZone():getWeather())) end)");
        Lua("xi.zones.Jugner_Forest.Zone.onZoneWeatherChange = function(weather) report('zone:' .. weather) end");
        // the listener runs before the weather is set; the zone's script after
        Check("setWeather(SUNSHINE): the listener (on the old weather), then onZoneWeatherChange", "Weathery:1:0:6,zone:1",
              Lua("player:setWeather(xi.weather.SUNSHINE)"));
        Check("sunshine, changed now", $"{Sunshine}/{stamp}", $"{info.Weather}/{info.WeatherChangeTime}");
        Check("the 0x057 to the char", $"12:{stamp}:{Sunshine}:4", Weather057());
        Check("the rain hat's REGEN off", "370:52:6:0/0", $"{Active()}/{c.GetMod(Mod.REGEN) - regenBare}");
        Check("sunshine: the water elementals despawn in 1 s", true, upWater.TrueForAll(m => m.DespawnAtMs == now + 1000));
        s.PacketList.Clear();
        Check("setWeather to the same weather: no change, but the zone's script still hears it", "zone:1",
              Lua("player:setWeather(xi.weather.SUNSHINE)"));
        Check("no 0x057", "", Weather057());
        Check("setWeather(25): not a weather, nothing", $"/{Sunshine}", $"{Lua("player:setWeather(25)")}/{info.Weather}");
        zone.SetWeather(Jugner, 20);
        Check("SetWeather(20) warns", true, log.Contains("zone: Weather value (20) invalid."));
        Check("and changes nothing", $"{Sunshine}/", $"{info.Weather}/{Weather057()}");
        // the despawn runs on the mob tick: CDespawnState, then the fade-out 3 s later
        now += 1001;
        zone.Tick();
        now += 3001;
        zone.Tick();
        Check("the water elementals gone", true, upWater.TrueForAll(m => !m.IsSpawned));
        s.PacketList.Clear();

        Console.WriteLine("The fog: 2 to 7 AM outside a city");
        vana = dayStart + 3 * VanaTime.HOUR;
        now = weather.NextUpdateMs;
        got.Clear();
        Roll(700, clearChance, 5);
        zone.Tick();
        Check("a clear pick at 3 AM is fog", Fog, info.Weather);
        Check("the 0x057", $"12:{stamp}:{Fog}:5", Weather057());
        Check("the next update at 7 AM: 4 vana hours", now + 4 * VanaTime.HOUR / 25, weather.NextUpdateMs);
        Check("the listener and onZoneWeatherChange", $"Weathery:{Fog}:0:1,zone:{Fog}", string.Join(",", got));
        Check("fog: the wisp stays, the elementals despawn", $"True/{now + 1000}",
              $"{wisp.DespawnAtMs != now + 1000}/{thunderElemental.DespawnAtMs}");
        s.PacketList.Clear();
        now = weather.NextUpdateMs;
        Roll(800, rainChance, 6);
        zone.Tick();
        Check("rain at 3 AM stays rain, the next update 800 s on", $"{Rain}/{now + 800_000}", $"{info.Weather}/{weather.NextUpdateMs}");
        Calls();

        Console.WriteLine("A city has no fog");
        var sandoria = Blob(db, SouthernSandoria);
        var cityEntry = Entry(sandoria, day);
        Roll(300, 60, 4);
        var cityInfo = zone.ZoneInfoOf(SouthernSandoria);
        Check("Southern San d'Oria is a city with a clear normal weather that day", "True/True",
              $"{cityInfo.Types.Contains("city")}/{cityEntry.Normal < HotSpell}");
        Check("its normal weather at 3 AM, not fog", cityEntry.Normal, cityInfo.Weather);
        Check("the next update 300 s on", now + 300_000, zone.WeatherOf(SouthernSandoria).NextUpdateMs);

        Console.WriteLine("controlledWeather: no automated weather");
        var controlled = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, _ => { });
        controlled.ControlledWeather = true;
        controlled.NowMs = () => now;
        controlled.SendIpc = _ => { };
        int controlledRolls = 0;
        controlled.WeatherRandom = (min, max) =>
        {
            controlledRolls++;
            return min;
        };
        var controlledInfo = controlled.ZoneInfoOf(Jugner);
        Check("the table loaded, the zone clear, no roll, nothing scheduled", $"{nonzero}/0/0/0/0",
              $"{controlled.WeatherOf(Jugner).Count}/{controlledInfo.Weather}/{controlledInfo.WeatherChangeTime}/{controlledRolls}/{controlled.WeatherOf(Jugner).NextUpdateMs}");
        controlled.SetWeather(Jugner, Rain);
        Check("a set weather still changes it", Rain, controlledInfo.Weather);

        // Jugner's mob scripts reach bindings not ported yet (DisallowRespawn, hideHP): only the weather's count here
        var errors = log.FindAll(l => l.StartsWith("lua:") &&
                                      (l.Contains("eather") || l.Contains("CZone") || l.Contains("GetZone") || l.Contains("verify_weather")));
        Check("no weather script errors", "", string.Join(" | ", errors));
    }
}
