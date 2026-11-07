using System;
using System.Collections.Generic;
using System.Linq;
using XI.Common;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // time_server.cpp's calls into the scripts and the zones: luautils::OnZoneTick each logic tick for every zone this
    // process runs (CZoneEntities::ZoneServer), the zone onGameHour / onGameDay / onTOTDChange on the Vana'diel hour,
    // day and time of day (TickGameHour drives them, with zoneutils::SavePlayTime on the day), xi.server.onJSTMidnight
    // with xi.znm.UpdateSanrakusMobs at JST midnight, xi.znm.ZNMPopPriceDecay each second JST hour,
    // xi.server.onTimeServerTick every 2400 ms; and MapEngine::do_init's tail (ServerStart): the TOTD change that
    // spawns time-of-day content, the expired char / server variables deleted, xi.server.onServerStart.
    public sealed partial class ZoneHost
    {
        private const long TimeServerTickMs = 2400; // kTimeServerTickInterval

        private readonly Dictionary<ushort, bool> zoneHasTick_ = new Dictionary<ushort, bool>();
        private long nextJstHourUnix_ = -1, nextTimeServerTickMs_ = -1;

        // the zones this process runs (zoneutils::ForEachZone); a snapshot, since a hook may load more
        private List<ZoneInfo> RunZones() => zones_.Values.Where(z => z.Ip != 0).Select(z => z.Info).ToList();

        // map_config's isTestServer (the C++ xi_test sets it): ServerStart leaves ReportLuaModuleUsage out. No host
        // here sets it; a verify suite may
        public bool IsTestServer;

        // MapEngine::do_init, after the zones and the scripts are up
        public void ServerStart()
        {
            ZonesOnTotdChange((int)VanaTime.GetTotd(VanaTicksNow()));
            long now = UnixNow();
            Exec("DELETE FROM char_vars WHERE expiry > 0 AND expiry <= ?", BoundValue.Of((uint)now));
            Exec("DELETE FROM server_variables WHERE expiry > 0 AND expiry <= ?", BoundValue.Of((uint)now));
            CallServerGlobal("xi.server.onServerStart");
            // map_engine.cpp:276-279: not on a test server
            if (!IsTestServer) ReportLuaModuleUsage();
        }

        // a Zone.lua function of the zone's, with its arguments; false when the zone has none
        private bool CallZoneFunction(ZoneInfo z, string fn, Action<LuaState> args, int nargs)
        {
            if (lua_ == null || scripts_ == null || z.Name == "") return false;
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction(ZoneLuaFile(z), fn);
                if (lua_.IsNil(-1)) return false;
                args?.Invoke(lua_);
                lua_.Call(nargs, 0);
                return true;
            }
            catch (LuaException e)
            {
                ScriptError("luautils::" + fn, e);
                return true;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::OnZoneTick(zone), for the zones whose script has one
        private void TickZoneScripts()
        {
            if (!ZoneLuaEnabled || lua_ == null) return;
            foreach (var z in RunZones())
            {
                if (zoneHasTick_.TryGetValue(z.Id, out bool has) && !has) continue;
                zoneHasTick_[z.Id] = CallZoneFunction(z, "onZoneTick", l => l.PushObject(z, WeatherZoneLuaType), 1);
            }
        }

        // luautils::OnGameHour(zone) for each zone
        private void ZonesOnGameHour()
        {
            if (!ZoneLuaEnabled) return;
            foreach (var z in RunZones()) CallZoneFunction(z, "onGameHour", l => l.PushObject(z, WeatherZoneLuaType), 1);
        }

        // the Vana'diel day: luautils::OnGameDay() for each zone, then zoneutils::SavePlayTime
        private void ZonesOnGameDay()
        {
            if (ZoneLuaEnabled)
                foreach (var z in RunZones()) CallZoneFunction(z, "onGameDay", null, 0);
            foreach (var c in new List<CharData>(charSessions_.Keys)) SavePlayTime(c);
        }

        // zoneutils::TOTDChange: luautils::OnTOTDChange(zone, totd) for each zone
        private void ZonesOnTotdChange(int totd)
        {
            if (!ZoneLuaEnabled) return;
            foreach (var z in RunZones()) CallZoneFunction(z, "onTOTDChange", l => l.Push((double)totd), 1);
        }

        // the time server's JST hour (OnJSTMidnight, UpdateSanrakusMobs, ZNMPopPriceDecay) and its 2400 ms tick
        // (OnTimeServerTick)
        private void TickTimeServer()
        {
            long nowMs = NowMs();
            if (nextTimeServerTickMs_ < 0 || nowMs >= nextTimeServerTickMs_)
            {
                if (nextTimeServerTickMs_ >= 0)
                {
                    CallServerGlobal("xi.server.onTimeServerTick");
                    // time_server.cpp:186-187: the watcher's changes right after OnTimeServerTick (off: no watcher)
                    TryReloadFilewatchList();
                }
                nextTimeServerTickMs_ = nowMs + TimeServerTickMs;
            }

            long now = UnixNow();
            if (nextJstHourUnix_ < 0) nextJstHourUnix_ = FloorDiv(now + 3599, 3600) * 3600;
            if (now < nextJstHourUnix_) return;
            int hour = JstHour(now);
            if (hour == 0)
            {
                CallServerGlobal("xi.server.onJSTMidnight");
                CallServerGlobal("xi.znm.UpdateSanrakusMobs");
            }
            if (hour % 2 == 0) CallServerGlobal("xi.znm.ZNMPopPriceDecay");
            nextJstHourUnix_ = (FloorDiv(now, 3600) + 1) * 3600;
        }

        // luautils::callGlobal<void>(path)
        private void CallServerGlobal(string path)
        {
            if (lua_ == null) return;
            int top = lua_.Top;
            try
            {
                lua_.GetPath(path);
                if (lua_.IsNil(-1))
                {
                    log_($"luautils::callGlobalFunction: {path}: Function not found");
                    return;
                }
                lua_.Call(0, 0);
            }
            catch (LuaException e)
            {
                ScriptError(path, e);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }
    }
}
