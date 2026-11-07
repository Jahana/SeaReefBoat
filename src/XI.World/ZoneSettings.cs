// Port of world/zone_settings.h: zone id -> map process endpoint, plus the de-duplicated endpoint lists
// (all / yell / assist) built from zone_settings joined with each zone's Misc flags.

using System;
using System.Collections.Generic;
using XI.Login;
using XI.Persistence;

namespace XI.World
{
    // xi::ZoneMisc bits (data/enums/zone_misc.yaml)
    public static class ZoneMisc
    {
        public const uint Yell = 0x0400;
        public const uint Assist = 0x4000;
    }

    public sealed class ZoneSettingsEntry
    {
        public ushort ZoneId;
        public Ipp Ipp;
        public uint Misc;
    }

    public sealed class ZoneSettings
    {
        public Dictionary<ushort, ZoneSettingsEntry> ZoneSettingsMap = new();
        public List<Ipp> MapEndpoints = new();
        public List<Ipp> YellMapEndpoints = new();
        public List<Ipp> AssistMapEndpoints = new();

        public ZoneSettings() { }

        // zoneMisc(zoneId) stands in for loadZoneFile<settings::Dataset>(zoneid)->Misc; null when the zone has no file.
        public ZoneSettings(IDatabase db, Func<ushort, uint?> zoneMisc = null)
        {
            var rset = LoginDb.PreparedStmt(db, "SELECT zoneid, zoneip, zoneport FROM zone_settings");
            if (rset == null) throw new InvalidOperationException("Message Server: Failed to load zone settings from database");

            var mapSet = new SortedSet<Ipp>();
            var yellSet = new SortedSet<Ipp>();
            var assistSet = new SortedSet<Ipp>();

            while (rset.Next())
            {
                var entry = new ZoneSettingsEntry
                {
                    ZoneId = rset.Get<ushort>("zoneid"),
                    Ipp = new Ipp(IpText.Str2Ip(rset.Get<string>("zoneip")), (ushort)rset.Get<ulong>("zoneport")),
                };
                var misc = zoneMisc?.Invoke(entry.ZoneId);
                if (misc.HasValue) entry.Misc = misc.Value;

                mapSet.Add(entry.Ipp);
                if ((entry.Misc & ZoneMisc.Yell) != 0) yellSet.Add(entry.Ipp);
                if ((entry.Misc & ZoneMisc.Assist) != 0) assistSet.Add(entry.Ipp);

                ZoneSettingsMap[entry.ZoneId] = entry;
            }

            MapEndpoints.AddRange(mapSet);
            YellMapEndpoints.AddRange(yellSet);
            AssistMapEndpoints.AddRange(assistSet);
        }
    }
}
