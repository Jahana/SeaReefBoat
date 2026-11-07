using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace XI.Zone
{
    // The per-zone settings a zone-in packet reads: data/zones/<name>/zone.yaml's music and misc flags, plus the
    // weather the zone is under. Weather is the zone's weather().current() and the vanadiel timestamp it last changed
    // at, which ZoneHost.Weather (CZone::SetWeather) sets.
    public sealed class ZoneInfo
    {
        public ushort Id;
        public string Name = "";
        public ushort MusicDay, MusicNight, BattleSolo, BattleParty;
        public HashSet<string> Misc = new HashSet<string>();
        public HashSet<string> Types = new HashSet<string>();
        public ushort Weather;
        public uint WeatherChangeTime;
        public ushort Tax; // m_tax: the bazaar tax, zone.yaml's percentage in hundredths (500 = 5%)

        public Dictionary<uint, ZoneLine> ZoneLines = new Dictionary<uint, ZoneLine>();

        // npcs.yaml, ordered by id; none in an instanced zone
        public List<NpcEntity> Npcs = new List<NpcEntity>();
        public Dictionary<uint, NpcEntity> NpcById = new Dictionary<uint, NpcEntity>();

        // mobs.yaml as zoneutils::InsertMobs builds it, regions.yaml's roam regions and the zone's spawn handler;
        // none in an instanced zone
        public List<MobEntity> Mobs = new List<MobEntity>();
        public Dictionary<uint, MobEntity> MobById = new Dictionary<uint, MobEntity>();
        public Dictionary<string, RoamRegion> Regions = new Dictionary<string, RoamRegion>();
        // navMesh_: navmeshes/<name>.nav, loaded on first use (ZoneHost.NavMesh.cs); null when the zone has none
        public DetourNavMesh NavMesh;
        public bool NavMeshLoaded;
        // xiMesh_: ximeshes/<name>.ximesh, the collision mesh, loaded on first use (ZoneHost.XiMesh.cs)
        public XiMesh XiMesh;
        public bool XiMeshLoaded;
        public MobSpawnHandler Spawner;
        // m_BattlefieldHandler: a zone with a bcnm_records row holds battlefields (ZoneHost.Battlefield.cs)
        public BattlefieldHandler BattlefieldHandler;

        // m_petList: the chars' pets, each under a dynamic targid
        public List<PetEntity> Pets = new List<PetEntity>();
        // m_trustList: the chars' trusts, each under a dynamic targid
        public List<TrustEntity> Trusts = new List<TrustEntity>();

        // ---- dynamic targids (CZoneEntities::AssignDynamicTargIDandLongID / EraseStaleDynamicTargIDs) ----

        public const ushort DynamicTargIdStart = 0x700, DynamicTargIdMax = 0x8FF;
        private const long DynamicTargIdHoldMs = 60000;
        private readonly HashSet<ushort> dynamicTargIds_ = new HashSet<ushort>();
        private readonly List<(ushort TargId, long At)> dynamicTargIdsToDelete_ = new List<(ushort, long)>();
        private ushort nextDynamicTargId_ = DynamicTargIdStart;

        // the next free targid from 0x700 to 0x8FF (wrapping), and its id 0x01000000 | zone << 12 | targid + 0x100;
        // 0x900 when all are taken
        public void AssignDynamicTargId(BattleEntity e, long nowMs)
        {
            ushort targid = NextDynamicTargId();
            e.TargId = targid;
            e.Id = DynamicId(targid);
            e.Zone = Id;
        }

        // a dynamic NPC (luautils::GenerateDynamicEntity)
        public void AssignDynamicTargId(NpcEntity n)
        {
            ushort targid = NextDynamicTargId();
            n.TargId = targid;
            n.Id = DynamicId(targid);
        }

        public uint DynamicId(ushort targid) => 0x01000000u | (uint)Id << 12 | (uint)(targid + 0x100);

        private ushort NextDynamicTargId()
        {
            ushort targid = nextDynamicTargId_;
            if (targid > DynamicTargIdMax) targid = DynamicTargIdStart;
            int counter = 0;
            while (dynamicTargIds_.Contains(targid))
            {
                targid++;
                if (targid > DynamicTargIdMax) targid = DynamicTargIdStart;
                if (counter > 0x1FF)
                {
                    targid = 0x900;
                    break;
                }
                counter++;
            }
            nextDynamicTargId_ = (ushort)(targid + 1);
            dynamicTargIds_.Add(targid);
            return targid;
        }

        // a removed entity's targid stays taken for 60 s
        public void ReleaseDynamicTargId(ushort targid, long nowMs) => dynamicTargIdsToDelete_.Add((targid, nowMs));

        public void EraseStaleDynamicTargIds(long nowMs)
        {
            for (int i = dynamicTargIdsToDelete_.Count - 1; i >= 0; i--)
            {
                if (nowMs - dynamicTargIdsToDelete_[i].At <= DynamicTargIdHoldMs) continue;
                dynamicTargIds_.Remove(dynamicTargIdsToDelete_[i].TargId);
                dynamicTargIdsToDelete_.RemoveAt(i);
            }
        }

        public bool DynamicTargIdTaken(ushort targid) => dynamicTargIds_.Contains(targid);

        public bool CanUseMisc(string flag) => Misc.Contains(flag);

        // xi::ZoneMisc's bits (data/enums/zone_misc.yaml) by their yaml names; CZone::CanUseMisc wants them all
        private static readonly string[] MiscBits =
        {
            "escape", "fellow", "mount", "mazurka", "tractor", "mogmenu", "costume", "pet", "treasure", "auction_house",
            "yell", "trust", "los_player_block", "los_off", "assist",
        };

        public bool CanUseMisc(ushort mask)
        {
            for (int bit = 0; bit < MiscBits.Length; bit++)
                if ((mask & (1 << bit)) != 0 && !Misc.Contains(MiscBits[bit])) return false;
            return true;
        }

        // the world's ZoneSettings: xi::data::loadZoneFile<settings::Dataset>(zoneid)->Misc, zone.yaml's misc as
        // xi::ZoneMisc bits by the zone_settings name; null for a zone without a file (its misc stays 0)
        public static Func<ushort, uint?> MiscFlagsLookup(XI.Persistence.IDatabase db, string zoneDir)
        {
            var names = new Dictionary<ushort, string>();
            var r = db.Execute("SELECT zoneid, name FROM zone_settings", Array.Empty<XI.Persistence.BoundValue>());
            while (r != null && r.Next()) names[r.Get<ushort>("zoneid")] = r.Get<string>("name");
            return id =>
            {
                if (!names.TryGetValue(id, out var name)) return null;
                var path = Path.Combine(zoneDir, FolderName(name), "zone.yaml");
                if (!File.Exists(path)) return null;
                uint mask = 0;
                foreach (var m in MiniYaml.Load(path).List("misc"))
                {
                    int bit = Array.IndexOf(MiscBits, (string)m);
                    if (bit >= 0) mask |= 1u << bit;
                }
                return mask;
            };
        }

        // the folder (and xi::ZoneId yaml name) of a zone_settings name: "Port_Windurst" -> port_windurst,
        // "Abyssea-Konschtat" -> abyssea_konschtat
        public static string FolderName(string name)
        {
            var sb = new StringBuilder();
            foreach (char ch in name.ToLowerInvariant())
            {
                if (ch == '\'') continue;
                if (char.IsAsciiLetterOrDigit(ch)) sb.Append(ch);
                else if (sb.Length > 0 && sb[^1] != '_') sb.Append('_');
            }
            return sb.ToString().TrimEnd('_');
        }

        // zoneDir is data/zones; name is zone_settings.name ("Port_Windurst"). zoneIdByName resolves a zone line's
        // `to` (a folder name); without it the zone has no zone lines.
        public static ZoneInfo Load(string zoneDir, ushort id, string name, Func<string, ushort?> zoneIdByName = null)
        {
            var info = new ZoneInfo { Id = id, Name = name };
            var path = Path.Combine(zoneDir, FolderName(name), "zone.yaml");
            if (!File.Exists(path)) return info;
            var doc = MiniYaml.Load(path);
            foreach (var m in doc.List("misc")) info.Misc.Add((string)m);
            foreach (var t in doc.List("type")) info.Types.Add((string)t);
            var music = doc.MapOrNull("music");
            if (music != null)
            {
                info.MusicDay = U16(music.Str("day"));
                info.MusicNight = U16(music.Str("night"));
                info.BattleSolo = U16(music.Str("battle_solo"));
                info.BattleParty = U16(music.Str("battle_party"));
            }
            if (doc.Str("tax") is string tax) info.Tax = (ushort)(float.Parse(tax, CultureInfo.InvariantCulture) * 100);

            // convertZoneLines
            var lines = doc.MapOrNull("zonelines");
            if (lines != null && zoneIdByName != null)
            {
                foreach (var (key, value) in lines)
                {
                    if (value is not Dictionary<string, object> zl) continue;
                    ushort? dest = zoneIdByName(zl.Str("to") ?? "");
                    if (dest == null) throw new FormatException($"zone line '{key}' in {path}: unknown zone '{zl.Str("to")}'");
                    var from = Floats(zl.List("from"));
                    var at = Floats(zl.List("at"));
                    var scale = Floats(zl.List("scale"));
                    var line = new ZoneLine
                    {
                        Id = ZoneLine.PackId(key),
                        OriginX = from[0], OriginY = from[1], OriginZ = from[2],
                        DestinationZone = dest.Value,
                        DestX = at[0], DestY = at[1], DestZ = at[2],
                        DestRotation = at.Length > 3 ? ZoneLine.RadianToRotation(at[3]) : (byte)0,
                        ScaleX = scale.Length > 0 ? scale[0] : 1, ScaleZ = scale.Length > 1 ? scale[1] : 1,
                    };
                    if (!info.ZoneLines.TryAdd(line.Id, line)) throw new FormatException($"duplicate zone line '{key}' in {path}");
                }
            }
            return info;
        }

        // zoneutils::InsertNPCs: npcs.yaml beside zone.yaml, skipped for an instanced zone
        public void LoadNpcs(string zoneDir, DataEnums enums, Func<string, bool> contentEnabled)
        {
            if (Types.Contains("instanced")) return;
            Npcs = NpcEntity.Load(Path.Combine(zoneDir, FolderName(Name), "npcs.yaml"), enums, contentEnabled);
            NpcById = new Dictionary<uint, NpcEntity>();
            foreach (var npc in Npcs)
            {
                if ((npc.Id >> 12 & 0xFFF) != Id) throw new FormatException($"npc 0x{npc.Id:X8} belongs to zone {npc.Id >> 12 & 0xFFF}, not {Id}");
                NpcById[npc.Id] = npc;
            }
        }

        private static float[] Floats(List<object> l) =>
            l.ConvertAll(o => float.Parse((string)o, CultureInfo.InvariantCulture)).ToArray();

        private static ushort U16(string s) => s == null ? (ushort)0 : ushort.Parse(s, CultureInfo.InvariantCulture);
    }

    // zoneLine_t: a rect in this zone that sends the char to DestinationZone. Arrivals are spread over eight slots
    // across the destination's spawn area.
    public sealed class ZoneLine
    {
        public uint Id;                       // the fourcc, first character in the low byte (packZoneLineId)
        public float OriginX, OriginY, OriginZ;
        public ushort DestinationZone;
        public float DestX, DestY, DestZ;
        public byte DestRotation;
        public float ScaleX = 1, ScaleZ = 1;
        private int spawnSlot_;

        public static uint PackId(string key)
        {
            if (key.Length != 4) throw new FormatException($"zone line '{key}' is not four characters");
            uint id = 0;
            for (int k = 0; k < 4; k++) id |= (uint)(byte)key[k] << (k * 8);
            return id;
        }

        public static string IdText(uint id) =>
            new string(new[] { (char)(byte)id, (char)(byte)(id >> 8), (char)(byte)(id >> 16), (char)(byte)(id >> 24) });

        // radianToRotation / rotationToRadian: a full turn is 256
        public static byte RadianToRotation(float radian) => (byte)(int)(radian / (2 * Math.PI) * 256);
        public static float RotationToRadian(byte rotation) => (float)(rotation / 256.0f * 2 * Math.PI);

        // the arrival slot back to the first: the verify suites take a zone line from the same slot in each run of a twin
        internal void ResetSpawnSlot() => spawnSlot_ = 0;

        // zoneLine_t::nextSpawnPosition
        public (float X, float Y, float Z, byte Rotation) NextSpawnPosition()
        {
            float scale = MathF.Max(ScaleX, ScaleZ);
            float spacing = (scale - 1.0f) / 8.0f;
            float offset = (spawnSlot_ - 4) * spacing;
            float rotation = RotationToRadian(DestRotation);
            spawnSlot_ = (spawnSlot_ + 1) % 8;
            return (DestX + offset * MathF.Sin(rotation), DestY, DestZ + offset * MathF.Cos(rotation), DestRotation);
        }
    }
}
