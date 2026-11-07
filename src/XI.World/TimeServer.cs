// Port of world/time_server.cpp: the world's 2400ms tick. Earth-based hourly/daily/weekly ticks (JST) drive
// conquest + daily tally; the Vana'diel-hour tick drives the vana-hourly conquest broadcast.
//
// Like the C++ coroutine, the "next tick" markers are persistent state (function statics there, fields here), and
// a tick can be missed if ticks are spaced further apart than the period they watch.

using System;
using XI.Common;
using XI.Persistence;

namespace XI.World
{
    public sealed class TimeServer
    {
        private const long MsPerHour = 3_600_000L;
        private const long MsPerDay = 24 * MsPerHour;
        private const long JstOffsetMs = 9 * MsPerHour;

        private readonly ConquestSystem _conquest;
        private readonly IDatabase _db;
        private readonly DailyTallySettings _dailyTally;
        private readonly Func<long> _unixMs;
        private readonly Action<string> _log;

        private int _tickNum;
        private bool _started;
        private long _nextHourlyTick;       // unix ms
        private long _nextVHourlyUpdate;    // vana ticks
        private Totd _prevTotd;

        public TimeServer(ConquestSystem conquest, IDatabase db, DailyTallySettings dailyTally, Func<long> unixMs = null, Action<string> log = null)
        {
            _conquest = conquest;
            _db = db;
            _dailyTally = dailyTally;
            _unixMs = unixMs ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            _log = log ?? (_ => { });
        }

        private static long FloorDiv(long a, long b)
        {
            long q = a / b;
            if (a % b != 0 && ((a < 0) != (b < 0))) q--;
            return q;
        }

        private static long CeilDiv(long a, long b)
        {
            long q = a / b;
            if (a % b != 0 && ((a < 0) == (b < 0))) q++;
            return q;
        }

        // earth_time::jst::get_hour / get_weekday (0 = Sunday) of a UTC unix-ms time point.
        public static uint JstHour(long unixMs) => (uint)(FloorDiv(unixMs + JstOffsetMs, MsPerHour) % 24 + 24) % 24;
        public static uint JstWeekday(long unixMs) => (uint)(((FloorDiv(unixMs + JstOffsetMs, MsPerDay) + 4) % 7 + 7) % 7); // 1970-01-01 = Thursday

        // vanadiel_time::from_earth_time at ms precision (25 vana ticks per earth millisecond).
        public static long VanaTicks(long unixMs) => (unixMs - VanaTime.EpochUnixSec * 1000L) * (VanaTime.TicksPerEarthSecond / 1000L);

        public void Tick()
        {
            ++_tickNum;

            long jstTime = _unixMs();
            uint jstHour = JstHour(jstTime);
            uint jstWeekday = JstWeekday(jstTime);

            if (!_started) _nextHourlyTick = CeilDiv(jstTime, MsPerHour) * MsPerHour;

            // Hourly ticks
            if (jstTime >= _nextHourlyTick)
            {
                _log($"1-hour tick... (current tick: {_tickNum})");
                if (jstHour == 0)
                {
                    _log($"Daily tick... (current tick: {_tickNum})");
                    if (jstWeekday == 1)
                    {
                        _log($"Weekly tick... (current tick: {_tickNum})");
                        _conquest.UpdateWeekConquest();
                    }
                    else
                    {
                        _conquest.UpdateHourlyConquest();
                    }
                    DailyTally.UpdateDailyTallyPoints(_db, _dailyTally, _log);
                }
                else
                {
                    _conquest.UpdateHourlyConquest();
                }

                if (jstHour % 2 == 0)
                {
                    _log($"2-hour tick... (current tick: {_tickNum})");
                    if (jstHour % 4 == 0) _log($"4-hour tick... (current tick: {_tickNum})");
                }

                _nextHourlyTick = CeilDiv(jstTime, MsPerHour) * MsPerHour;
            }

            // Vana'diel-based ticks.
            long vanaTime = VanaTicks(jstTime);
            var vanaTotd = VanaTime.GetTotd(vanaTime);
            uint vanaHour = VanaTime.GetHour(vanaTime);

            if (!_started)
            {
                _nextVHourlyUpdate = CeilDiv(vanaTime, VanaTime.HOUR) * VanaTime.HOUR;
                _prevTotd = vanaTotd;
                _started = true;
            }

            if (vanaTime >= _nextVHourlyUpdate)
            {
                _conquest.UpdateVanaHourlyConquest();

                if (vanaHour == 0) _log($"Vana'diel day tick... (current tick: {_tickNum})");

                if (vanaTotd != _prevTotd) _prevTotd = vanaTotd;

                _nextVHourlyUpdate = CeilDiv(vanaTime, VanaTime.HOUR) * VanaTime.HOUR;
            }
        }
    }
}
