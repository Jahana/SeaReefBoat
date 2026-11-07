using System.Collections.Generic;

namespace XI.Zone
{
    // ZoneWeather: a day's three weathers, picked by chance (normal 50%, common 35%, rare 15%)
    public readonly struct ZoneWeather
    {
        public readonly ushort Normal, Common, Rare;

        public ZoneWeather(ushort normal, ushort common, ushort rare)
        {
            Normal = normal;
            Common = common;
            Rare = rare;
        }
    }

    // WeatherContainer: a zone's zone_weather table, keyed by day of the 2160 day cycle. The current weather and the
    // vanadiel timestamp it changed at (current_ / changeTime_) are ZoneInfo.Weather / WeatherChangeTime, which the
    // zone-in packet and the spawn handler already read. NextUpdateMs is the earth time CZone::UpdateWeather's posted
    // task runs at (0 while none is pending).
    public sealed class WeatherContainer
    {
        private readonly SortedDictionary<ushort, ZoneWeather> probabilities_ = new SortedDictionary<ushort, ZoneWeather>();

        public long NextUpdateMs;

        public int Count => probabilities_.Count;

        // WeatherContainer::isStatic
        public bool IsStatic => probabilities_.Count == 0 || probabilities_.Count == 1;

        // WeatherContainer::addEntry: std::map::insert keeps the first entry for a day
        public void AddEntry(ushort day, ZoneWeather entry) => probabilities_.TryAdd(day, entry);

        // WeatherContainer::entryForDay: the last entry on or before the day, else none
        public ZoneWeather EntryForDay(ushort day)
        {
            var entry = new ZoneWeather(0, 0, 0);
            foreach (var (entryDay, probabilities) in probabilities_)
            {
                if (entryDay > day) break;
                entry = probabilities;
            }
            return entry;
        }
    }
}
