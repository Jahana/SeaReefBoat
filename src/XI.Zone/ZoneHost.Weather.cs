using System;
using System.Collections.Generic;
using XI.Common;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // Zone weather: CZone::LoadZoneWeather (the zone_weather blob), zoneutils::InitializeWeather (run as each zone
    // loads, unless map_config controlledWeather), CZone::UpdateWeather (the day's roll, the 2-7 AM fog and the next
    // change 3 to 30 minutes on, run from the zone tick), CZone::SetWeather (the change and its 0x057
    // GP_SERV_COMMAND_WEATHER to the zone's chars; the zone-in sends the weather in the 0x00A login packet only),
    // CZoneEntities::WeatherChange (the mobs' and chars' WEATHER_CHANGE listeners and CheckLatentsWeather),
    // SpawnHandler::onWeatherChange (an elemental or fog mob out of its weather despawns), luautils::OnZoneWeatherChange,
    // fishingutils::GetWeatherModifier, and the Lua bindings CLuaBaseEntity::setWeather / getZone, luautils::GetZone and
    // CLuaZone::getWeather / getID / getName (CLuaBaseEntity::getWeather is the combat bindings'). WeatherChange's
    // m_disableScent is MobEntity.DisableScent (read by the mob AI's CanTrackByScent); the magic and enspell weather
    // bonuses are the spells' Lua's and ZoneHost.Enspell.cs's. Not ported: getZone's zoning branch (a char's zone is
    // always set here). The other CLuaZone methods are the zone Lua file's (ZoneLuaZoneMethods).
    public sealed partial class ZoneHost
    {
        private const string WeatherZoneLuaType = "CZone";
        private const int WeatherCycle = 2160;
        private const ushort WeatherFog = 3, WeatherSquall = 7, WeatherBlizzards = 13, WeatherLast = 19; // xi::Weather

        // map_config controlledWeather: no automated weather, a zone stays clear until a script sets it
        public bool ControlledWeather;

        // xirand::GetRandomNumber(min, max): [min, max)
        public Func<int, int, int> WeatherRandom = (min, max) => Random.Shared.Next(min, max);

        private readonly Dictionary<ushort, WeatherContainer> weatherZones_ = new Dictionary<ushort, WeatherContainer>();

        // CZone::weather(), for the verify suites
        public WeatherContainer WeatherOf(ushort zoneId)
        {
            Zone(zoneId);
            return weatherZones_[zoneId];
        }

        // CZone::LoadZoneWeather: 2160 little-endian uint16, one a day, each the normal, common and rare weather in
        // 5 bit fields (0nnnnncccccrrrrr); a day of 0 has no entry
        private WeatherContainer WeatherLoadZoneWeather(ushort zoneId)
        {
            var w = new WeatherContainer();
            var r = Exec("SELECT weather FROM zone_weather WHERE zone = ? LIMIT 1", BoundValue.Of(zoneId));
            if (r == null || !r.Next() || r.IsNull("weather")) return w;
            var blob = r.GetBlobBytes("weather");
            // db::extractFromBlob: a short blob leaves the rest of the days zeroed
            for (int i = 0; i < WeatherCycle && i * 2 + 1 < blob.Length; i++)
            {
                ushort v = (ushort)(blob[i * 2] | blob[i * 2 + 1] << 8);
                if (v == 0) continue;
                w.AddEntry((ushort)i, new ZoneWeather((ushort)(v >> 10), (ushort)(v >> 5 & 0x1F), (ushort)(v & 0x1F)));
            }
            return w;
        }

        // the zone's LoadZoneWeather, then zoneutils::InitializeWeather for it: a changing zone rolls its weather,
        // a static one takes day 0's common weather
        private void WeatherInitialize(ZoneInfo info)
        {
            var w = WeatherLoadZoneWeather(info.Id);
            weatherZones_[info.Id] = w;
            if (ControlledWeather) return;
            if (!w.IsStatic) WeatherUpdate(info, w);
            else WeatherSet(info, w.EntryForDay(0).Common);
        }

        // CZone::UpdateWeather
        private void WeatherUpdate(ZoneInfo info, WeatherContainer w)
        {
            long now = VanaTicksNow();
            long nextMidnight = VanaTime.NextMidnightTicks(now);
            long startFog = nextMidnight - VanaTime.DAY + 2 * VanaTime.HOUR; // 2 AM
            long endFog = startFog + 5 * VanaTime.HOUR;                      // 7 AM

            // a random time between 3 and 30 minutes for the next change
            long nextUpdateTicks = WeatherRandom(180, 1801) * VanaTime.TicksPerEarthSecond;
            // the weather starts over every 2160 days
            ushort day = (ushort)(VanaTime.CountDays(now) % WeatherCycle);
            int chance = WeatherRandom(0, 100);
            var entry = w.EntryForDay(day);

            // 15% rare, 35% common, 50% normal
            ushort selected;
            if (chance < 15) selected = entry.Rare;
            else if (chance < 50) selected = entry.Common;
            else selected = entry.Normal;

            if (now >= startFog && now < endFog && selected < WeatherHotSpell && !info.Types.Contains("city"))
            {
                selected = WeatherFog;
                // the weather changes by 7 AM
                nextUpdateTicks = endFog - now;
            }

            WeatherSet(info, selected);
            WeatherOnZoneWeatherChange(info, selected);

            // scheduler_.postToMainThread / yieldFor: the next update on the earth clock (25 ticks a millisecond)
            w.NextUpdateMs = NowMs() + nextUpdateTicks / (VanaTime.TicksPerEarthSecond / 1000);
        }

        // the posted UpdateWeather tasks that are due: a zone that has turned static stops
        private void TickWeather()
        {
            long now = NowMs();
            foreach (var (id, w) in new List<KeyValuePair<ushort, WeatherContainer>>(weatherZones_))
            {
                if (w.NextUpdateMs == 0 || now < w.NextUpdateMs) continue;
                w.NextUpdateMs = 0;
                if (!w.IsStatic) WeatherUpdate(Zone(id).Info, w);
            }
        }

        // CZone::SetWeather, for the verify suites
        public void SetWeather(ushort zoneId, ushort weather) => WeatherSet(Zone(zoneId).Info, weather);

        // CZone::SetWeather: the zone's entities hear of it before the weather changes, then the zone's chars get the
        // 0x057
        private void WeatherSet(ZoneInfo info, ushort weather)
        {
            if (weather > WeatherLast)
            {
                log_($"zone: Weather value ({weather}) invalid.");
                return;
            }
            if (info.Weather == weather) return;

            WeatherChange(info, weather);

            uint changeTime = LoginPackets.VanadielTimestamp(UnixNow());
            info.Weather = weather;
            info.WeatherChangeTime = changeTime;

            // CZoneEntities::PushPacket(CHAR_INZONE): every char in the zone but one in a mog house
            var packet = WeatherPacket(changeTime, weather, (ushort)WeatherRandom(4, 28));
            foreach (var (c, s) in charSessions_)
                if (s.InZone && c.Zone == info.Id && c.MoghouseId == 0) Push(s, new BasicPacket(packet.Buffer));
        }

        // GP_SERV_COMMAND_WEATHER: StartTime, WeatherNumber, WeatherOffsetTime
        public static BasicPacket WeatherPacket(uint startTime, ushort weather, ushort offsetTime)
        {
            var w = new PacketWriter((ushort)PacketS2C.GP_SERV_COMMAND_WEATHER, 12);
            w.U32(4, startTime);
            w.U16(8, weather);
            w.U16(10, offsetTime);
            return w.Packet;
        }

        // CZoneEntities::WeatherChange
        private void WeatherChange(ZoneInfo info, ushort weather)
        {
            byte element = MobSpawnHandler.WeatherElement(weather);
            foreach (var m in new List<MobEntity>(info.Mobs))
            {
                TriggerListener(m, "WEATHER_CHANGE", m, (int)weather, (int)element);
                if ((m.GetMobMod(MobMod.Detection) & DetectsScent) != 0)
                    m.DisableScent = weather == WeatherRain || weather == WeatherSquall || weather == WeatherBlizzards;
            }

            WeatherSpawnHandlerOnWeatherChange(info, weather);

            foreach (var (c, s) in new List<KeyValuePair<CharData, MapSession>>(charSessions_))
            {
                if (!s.InZone || c.Zone != info.Id) continue;
                c.Latents.CheckLatentsWeather(weather);
                TriggerListener(c, "WEATHER_CHANGE", c, (int)weather, (int)element);
            }
        }

        // SpawnHandler::onWeatherChange: an elemental (without a master) out of its element, or a fog mob out of fog,
        // despawns in 1 s
        private void WeatherSpawnHandlerOnWeatherChange(ZoneInfo info, ushort weather)
        {
            byte element = MobSpawnHandler.WeatherElement(weather);
            long now = NowMs();
            foreach (var m in info.Mobs)
            {
                if (m.Ecosystem == Ecosystem.Elemental && m.PMaster == null && (m.SpawnType & SpawnType.Weather) != 0)
                {
                    if (m.Element != element) m.DespawnAtMs = now + 1000;
                }
                else if ((m.SpawnType & SpawnType.Fog) != 0)
                {
                    if (weather != WeatherFog) m.DespawnAtMs = now + 1000;
                }
            }
        }

        // luautils::OnZoneWeatherChange: xi.zones[name].Zone.onZoneWeatherChange(weather)
        private void WeatherOnZoneWeatherChange(ZoneInfo info, ushort weather)
        {
            if (lua_ == null || scripts_ == null) return;
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction($"./scripts/zones/{info.Name}/Zone.lua", "onZoneWeatherChange");
                if (lua_.IsNil(-1)) return;
                lua_.Push((double)weather);
                lua_.Call(1, 0);
            }
            catch (LuaException e)
            {
                ScriptError("luautils::onZoneWeatherChange", e);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // fishingutils::GetWeatherModifier
        private float WeatherFishingModifier(CharData c)
        {
            ushort weather = AreaOf(c).Weather;
            if (weather == WeatherRain) return 1.1f;
            if (weather == WeatherSquall) return 1.2f;
            return 1.0f;
        }

        // ---- Lua --------------------------------------------------------------------------------------------------

        private void RegisterWeatherBindings(Dictionary<string, LuaFunction> m)
        {
            // CLuaBaseEntity::setWeather: the zone's script hears of it even when the weather is unchanged
            m["setWeather"] = l =>
            {
                int weather = l.ToEnumArg(2); // xi::Weather
                if (weather >= 0 && weather <= WeatherLast)
                {
                    var info = Zone(ZoneOf(Entity(l))).Info;
                    WeatherSet(info, (ushort)weather);
                    WeatherOnZoneWeatherChange(info, (ushort)weather);
                }
                return 0;
            };
            // CLuaBaseEntity::getZone
            m["getZone"] = l =>
            {
                l.PushObject(Zone(ZoneOf(Entity(l))).Info, WeatherZoneLuaType);
                return 1;
            };

            // luautils::GetZone: nil for a zone that does not exist
            lua_.SetGlobal("GetZone", l =>
            {
                ushort id = (ushort)l.ToEnumArg(1); // xi::ZoneId
                bool exists = zones_.ContainsKey(id);
                if (!exists)
                {
                    var r = Exec("SELECT zoneid FROM zone_settings WHERE zoneid = ? LIMIT 1", BoundValue.Of(id));
                    exists = r != null && r.Next();
                }
                // zoneutils::GetZone: nil for a zone another map process holds
                l.PushObject(exists && IsZoneOnThisProcess(id) ? Zone(id).Info : null, WeatherZoneLuaType);
                return 1;
            });

            // CLuaZone
            var zoneMethods = new Dictionary<string, LuaFunction>
            {
                ["getID"] = l => Ret(l, WeatherLuaZone(l).Id),
                ["getName"] = l =>
                {
                    l.PushString(WeatherLuaZone(l).Name);
                    return 1;
                },
                ["getWeather"] = l => Ret(l, WeatherLuaZone(l).Weather),
            };
            ZoneLuaZoneMethods(zoneMethods);
            BattlefieldZoneMethods(zoneMethods);
            DynamicEntityZoneMethods(zoneMethods);
            lua_.RegisterType(WeatherZoneLuaType, zoneMethods);
        }

        private static ZoneInfo WeatherLuaZone(LuaState l) =>
            l.ToObject(1) as ZoneInfo ?? throw new InvalidOperationException("bad self: expected " + WeatherZoneLuaType);
    }
}
