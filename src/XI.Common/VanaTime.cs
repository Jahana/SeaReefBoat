using System;

namespace XI.Common
{
    // Port of the Vana'diel clock (src/common/vanadiel_clock.h + vana_time.h).
    //
    // The engine's xi::vanadiel_clock uses non-integer std::ratio durations, so it is unsafe
    // to hand-transcribe: it is verified against compiled chrono golden vectors
    // (csharp/oracle/vana_golden.txt), which are the engine's own arithmetic.
    //
    // Model: the clock's base duration ("milliseconds") ticks at 1/25000 of an Earth second
    // (millisecond_ratio = ratio<1,25000>). One Vana'diel minute = 2.4 Earth seconds, one day
    // = 3456 Earth seconds (cross-checked against VANADAY_SECONDS in gardenutils.cpp). A
    // Vana'diel time_point is expressed as a signed tick count since the Vana'diel epoch.
    public static class VanaTime
    {
        // earth_time::vanadiel_epoch = Unix 1009810800
        public const long EpochUnixSec = 1009810800L;

        // Base tick rate of vanadiel_clock::duration (ratio<1, 25000>).
        public const long TicksPerEarthSecond = 25000L;

        // Unit sizes in ticks (derived from the ratio chain in vanadiel_clock.h).
        public const long SEC   = 1000L;                 // second_ratio = milli * 1000
        public const long MIN   = SEC * 60;              // 60 vana-seconds
        public const long HOUR  = MIN * 60;              // 60 vana-minutes
        public const long DAY   = HOUR * 24;             // 24 vana-hours  (== 3456 earth-s)
        public const long WEEK  = DAY * 8;               // 8 vana-days
        public const long MONTH = DAY * 30;              // 30 vana-days
        public const long YEAR  = DAY * 360;             // 360 vana-days

        // ---- chrono helpers (floor/ceil toward -inf/+inf; '%' uses C# trunc-toward-zero,
        //      which matches std::chrono's x % y = x - y*(x/y)) ----
        private static long FloorDiv(long a, long b)
        {
            long q = a / b;
            if ((a % b != 0) && ((a < 0) != (b < 0))) q--;
            return q;
        }

        private static long CeilDiv(long a, long b)
        {
            long q = a / b;
            if ((a % b != 0) && ((a < 0) == (b < 0))) q++;
            return q;
        }

        // floor<D>(ticks).count() and ceil<D>(ticks).count() in D's units.
        private static long Floor(long ticks, long unitTicks) => FloorDiv(ticks, unitTicks);
        private static long Ceil(long ticks, long unitTicks) => CeilDiv(ticks, unitTicks);

        // std::chrono duration modulus: ac % bc, both already counted in the common unit.
        private static long Mod(long ac, long bc) => ac % bc;

        public static long EarthUnixSecToTicks(long earthUnixSec)
        {
            return (earthUnixSec - EpochUnixSec) * TicksPerEarthSecond;
        }

        // count_days / count_weeks (floor of the tick duration into whole vana-days / weeks).
        public static uint CountDays(long ticks) => (uint)Floor(ticks, DAY);
        public static uint CountWeeks(long ticks) => (uint)Floor(ticks, WEEK);

        // get_second: floor<sec> % (floor<min> * 60), common unit = SEC.
        public static uint GetSecond(long ticks)
        {
            long secCount = Floor(ticks, SEC);
            long minCount = Floor(ticks, MIN);
            return (uint)Mod(secCount, minCount * 60);
        }

        // get_minute: floor<min> % (floor<hr> * 60), common unit = MIN.
        public static uint GetMinute(long ticks)
        {
            long minCount = Floor(ticks, MIN);
            long hrCount = Floor(ticks, HOUR);
            return (uint)Mod(minCount, hrCount * 60);
        }

        // get_hour: floor<hr> % (floor<day> * 24), common unit = HOUR.
        public static uint GetHour(long ticks)
        {
            long hrCount = Floor(ticks, HOUR);
            long dayCount = Floor(ticks, DAY);
            return (uint)Mod(hrCount, dayCount * 24);
        }

        // get_monthday: ceil<day> % (floor<month> * 30), common unit = DAY.
        public static uint GetMonthDay(long ticks)
        {
            long dayCeil = Ceil(ticks, DAY);
            long monthFloor = Floor(ticks, MONTH);
            return (uint)Mod(dayCeil, monthFloor * 30);
        }

        // get_month: ceil<month> % (floor<year> * 12), common unit = MONTH.
        public static uint GetMonth(long ticks)
        {
            long monthCeil = Ceil(ticks, MONTH);
            long yearFloor = Floor(ticks, YEAR);
            return (uint)Mod(monthCeil, yearFloor * 12);
        }

        // get_year: floor<years>.count()  (years since the Vana'diel epoch, matches engine)
        public static int GetYear(long ticks)
        {
            return (int)Floor(ticks, YEAR);
        }

        // get_weekday: floor<day> % (floor<week> * 8), common unit = DAY.
        public static uint GetWeekDay(long ticks)
        {
            long dayCount = Floor(ticks, DAY);
            long weekCount = Floor(ticks, WEEK);
            return (uint)Mod(dayCount, weekCount * 8);
        }

        // get_yearday: floor<day> % (floor<year> * 360), common unit = DAY.
        public static uint GetYearDay(long ticks)
        {
            long dayCount = Floor(ticks, DAY);
            long yearCount = Floor(ticks, YEAR);
            return (uint)Mod(dayCount, yearCount * 360);
        }

        // get_next_midnight: ceil<days>(tp); returned as Earth unix seconds
        // (to_earth_time(ceil_days) = epoch + ceilDayCount * 3456, since DAY/TICKS_PER_EARTH_SEC = 3456).
        public static long GetNextMidnightUnixSec(long ticks)
        {
            long ceilDays = Ceil(ticks, DAY);
            long earthSinceEpochSec = ceilDays * (DAY / TicksPerEarthSecond);
            return EpochUnixSec + earthSinceEpochSec;
        }

        // The next-midnight boundary expressed back in vana TICKS (the runtime's native
        // domain for the daily-repeat window). Composes the two oracle-verified functions
        // instead of re-deriving the ceil: EarthUnixSecToTicks(GetNextMidnightUnixSec(t))
        //   = (Ceil(t,DAY) * 3456) * 25000 = Ceil(t,DAY) * DAY.
        // A tick already sitting exactly on midnight maps to that same boundary.
        public static long NextMidnightTicks(long ticks)
        {
            return EarthUnixSecToTicks(GetNextMidnightUnixSec(ticks));
        }

        // get_totd: bucketed by vana hour (vana_time.h switch).
        public static Totd GetTotd(long ticks)
        {
            switch (GetHour(ticks))
            {
                case 0:
                case 1:
                case 2:
                case 3:
                    return Totd.Midnight;
                case 4:
                case 5:
                    return Totd.NewDay;
                case 6:
                    return Totd.Dawn;
                case 7:
                case 8:
                case 9:
                case 10:
                case 11:
                case 12:
                case 13:
                case 14:
                case 15:
                case 16:
                    return Totd.Day;
                case 17:
                    return Totd.Dusk;
                case 18:
                case 19:
                    return Totd.Evening;
                default: // 20..23
                    return Totd.Night;
            }
        }

        // Moon ramp value: int32((count_days(ticks + 886*YEAR) + 26) % 84). The % here is
        // a plain integer modulo on the uint32 day count (not chrono).
        private static long MoonDaysmod(long ticks)
        {
            uint cd = CountDays(ticks + 886L * YEAR);
            return (long)((cd + 26u) % 84u);
        }

        // moon::get_phase. NOTE: `daysmod` is a double in the engine, so both /42 divisions
        // are floating-point; the result is then truncated to int32.
        public static uint MoonPhase(long ticks)
        {
            double daysmod = (double)(int)MoonDaysmod(ticks);

            int phase;
            if (daysmod >= 42)
            {
                phase = (int)(100 * ((daysmod - 42) / 42) + 0.5);
            }
            else
            {
                phase = (int)(100 * (1 - (daysmod / 42)) + 0.5);
            }

            return (uint)phase;
        }

        // moon::get_direction: 0 neither, 1 waning, 2 waxing. (double comparison, integer-valued.)
        public static byte MoonDirection(long ticks)
        {
            double daysmod = (double)(int)MoonDaysmod(ticks);
            if (daysmod == 42.0 || daysmod == 0.0)
            {
                return 0;
            }

            return daysmod < 42.0 ? (byte)1 : (byte)2;
        }

        // rse::get_race: count_weeks % 8 + 1.
        public static byte RseRace(long ticks)
        {
            return (byte)(CountWeeks(ticks) % 8 + 1);
        }

        // rse::get_location: count_weeks % 3.
        public static byte RseLocation(long ticks)
        {
            return (byte)(CountWeeks(ticks) % 3);
        }
    }

    // vanadiel_time::TOTD
    public enum Totd : byte
    {
        None = 0,
        Midnight = 1,
        NewDay = 2,
        Dawn = 3,
        Day = 4,
        Dusk = 5,
        Evening = 6,
        Night = 7,
    }
}
