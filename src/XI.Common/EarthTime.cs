using System;
using System.Threading;

namespace XI.Common
{
    // earth_time::now (src/common/earth_time.h): the system clock plus the process's time_offset, which luautils
    // SetTimeOffset (the GM command !addtime) resets and then adds to. The map process's wall clocks default to it
    // (ZoneHost.UnixNow, ScriptEngine.UnixNowMs, the packet header timestamp), and its Vana'diel clock is counted from
    // it, so an offset moves Earth and Vana'diel time together, as in the C++. The offset is per process: the world,
    // login and search processes keep the system clock, as the C++ executables do.
    public static class EarthTime
    {
        private static long offsetMs_;

        // time_offset in milliseconds
        public static long OffsetMs => Interlocked.Read(ref offsetMs_);

        // earth_time::reset_offset
        public static void ResetOffset() => Interlocked.Exchange(ref offsetMs_, 0);

        // earth_time::add_offset
        public static void AddOffset(long ms) => Interlocked.Add(ref offsetMs_, ms);

        // earth_time::now as unix milliseconds
        public static long UnixMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + OffsetMs;

        // earth_time::timestamp: whole unix seconds
        public static long UnixSec() => Math.DivRem(UnixMs(), 1000, out long rem) - (rem < 0 ? 1 : 0);

        // vanadiel_clock::now as ticks since the Vana'diel epoch (VanaTime's unit)
        public static long VanaTicks() =>
            (UnixMs() - VanaTime.EpochUnixSec * 1000) * (VanaTime.TicksPerEarthSecond / 1000);
    }
}
