// login_helpers.cpp loadZoneLookups: what the login server reads from the zone files at startup.
//   instancedZones(): the zones whose zone.yaml type has instanced (isZoneAtPlayerCap leaves them out)
//   entityNames():    every name a client can see on an npc or a mob (mob_pools.packet_name, npc_list.polutils_name,
//                     each zone's npcs.yaml display_name and mobs.yaml template display_name, else the template's key),
//                     as comparableName; only read when login.DISABLE_MOB_NPC_CHAR_NAMES is on
// The C++ walks every xi::ZoneId; here every zone_settings row, by its folder (ZoneInfo.FolderName), as the world's
// ZoneSettings does.

using System;
using System.Collections.Generic;
using System.IO;
using XI.Login;
using XI.Persistence;

namespace XI.Zone
{
    public static class LoginZoneLookups
    {
        private static List<(ushort Id, string Folder)> Zones(IDatabase db, string zoneDir)
        {
            var zones = new List<(ushort, string)>();
            var r = db.Execute("SELECT zoneid, name FROM zone_settings", Array.Empty<BoundValue>());
            while (r != null && r.Next()) zones.Add((r.Get<ushort>("zoneid"), Path.Combine(zoneDir, ZoneInfo.FolderName(r.Get<string>("name")))));
            return zones;
        }

        public static HashSet<ushort> InstancedZones(IDatabase db, string zoneDir)
        {
            var instanced = new HashSet<ushort>();
            foreach (var (id, folder) in Zones(db, zoneDir))
            {
                var path = Path.Combine(folder, "zone.yaml");
                if (!File.Exists(path)) continue;
                foreach (var t in MiniYaml.Load(path).List("type"))
                    if ((string)t == "instanced") instanced.Add(id);
            }
            return instanced;
        }

        public static HashSet<string> EntityNames(IDatabase db, string zoneDir)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var pools = db.Execute("SELECT packet_name FROM mob_pools WHERE packet_name != ''", Array.Empty<BoundValue>());
            while (pools != null && pools.Next()) names.Add(LoginHelpers.ComparableName(pools.Get<string>("packet_name")));
            var npcs = db.Execute("SELECT polutils_name FROM npc_list WHERE polutils_name != ''", Array.Empty<BoundValue>());
            while (npcs != null && npcs.Next()) names.Add(LoginHelpers.ComparableName(npcs.Get<string>("polutils_name")));

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (_, folder) in Zones(db, zoneDir))
            {
                if (!seen.Add(folder)) continue;
                var npcFile = Path.Combine(folder, "npcs.yaml");
                if (File.Exists(npcFile) && MiniYaml.Load(npcFile).MapOrNull("npcs") is { } list)
                {
                    foreach (var (_, value) in list)
                        if (value is Dictionary<string, object> npc && npc.Str("display_name", "") is { Length: > 0 } shown)
                            names.Add(LoginHelpers.ComparableName(shown));
                }
                var mobFile = Path.Combine(folder, "mobs.yaml");
                if (File.Exists(mobFile) && MiniYaml.Load(mobFile).MapOrNull("templates") is { } templates)
                {
                    foreach (var (key, value) in templates)
                        names.Add(LoginHelpers.ComparableName(value is Dictionary<string, object> t ? t.Str("display_name", key) : key));
                }
            }
            return names;
        }
    }
}
