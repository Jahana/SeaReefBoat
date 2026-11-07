// Port of the parts of map/conquest_data.h and common/regional_event.h the world server uses:
// influence_t / region_control_t, ConquestData::CalculateBeastmenInfluence, the ConquestMessage enum and the
// glaze-CBOR payload structs carried inside ipc::ConquestEvent.payload (definite-length maps keyed by member
// name, in declaration order; vectors are arrays).

using System;
using System.Collections.Generic;
using System.Formats.Cbor;

namespace XI.World
{
    // NATION_TYPE (map/zone.h)
    public static class Nation
    {
        public const byte Sandoria = 0, Bastok = 1, Windurst = 2, Beastmen = 3, Neutral = 5;
    }

    // REGION_TYPE (map/zone.h)
    public static class Region
    {
        public const byte Ronfaure = 0, Zulkheim = 1, Norvallen = 2, Gustaberg = 3, Derfland = 4, Sarutabaruta = 5,
            Kolshushu = 6, Aragoneu = 7, Fauregandi = 8, Valdeaunia = 9, Qufimisland = 10, Litelor = 11, Kuzotz = 12,
            Vollbow = 13, ElshimoLowlands = 14, ElshimoUplands = 15, Tulia = 16, Movalpolos = 17, Tavnazia = 18,
            Unknown = 255;
    }

    // struct influence_t
    public struct Influence
    {
        public int SandoriaInfluence, BastokInfluence, WindurstInfluence, MobKills, PlayerHomepoints;
    }

    // struct region_control_t
    public struct RegionControl
    {
        public byte Current, Prev;
    }

    public static class ConquestData
    {
        public const int MaxBeastmenInfluence = int.MaxValue / 100; // Packet multiplies by 100

        // ConquestData::CalculateBeastmenInfluence
        public static int CalculateBeastmenInfluence(byte region, in Influence influence)
        {
            if (influence.PlayerHomepoints == 0) return 0;

            // No mob kills means beastmen own 100% if a homepoint has happened
            if (influence.MobKills == 0) return MaxBeastmenInfluence;

            long nationTotal = unchecked(influence.SandoriaInfluence + influence.BastokInfluence + influence.WindurstInfluence);
            if (nationTotal == 0) return MaxBeastmenInfluence;

            long weight;
            switch (region)
            {
                case Region.Ronfaure: case Region.Zulkheim: case Region.Gustaberg: case Region.Sarutabaruta: case Region.Kolshushu:
                    weight = 10; break; // General
                case Region.Fauregandi: case Region.Qufimisland: case Region.Litelor: case Region.Kuzotz: case Region.Vollbow:
                case Region.ElshimoLowlands: case Region.ElshimoUplands: case Region.Tulia: case Region.Tavnazia:
                    weight = 25; break; // Semi-stronghold
                case Region.Norvallen: case Region.Derfland: case Region.Aragoneu: case Region.Valdeaunia: case Region.Movalpolos:
                    weight = 50; break; // Stronghold
                default:
                    weight = 10; break;
            }

            long beastmenWeight = weight * influence.PlayerHomepoints;
            return (int)Math.Min(beastmenWeight * nationTotal / influence.MobKills, MaxBeastmenInfluence);
        }
    }

    internal static class CborUtil
    {
        public static bool IsDecodeError(Exception e) =>
            e is InvalidOperationException || e is CborContentException || e is FormatException || e is OverflowException;
    }

    // M2W_AddInfluencePoints: struct ConquestAddInfluencePoints { int32 points; uint32 nation; uint8 region; }
    public sealed class ConquestAddInfluencePoints
    {
        public int Points;
        public uint Nation;
        public byte Region;

        public static ConquestAddInfluencePoints FromBytes(byte[] payload)
        {
            try
            {
                var r = new CborReader(payload);
                r.ReadStartMap();
                var o = new ConquestAddInfluencePoints();
                r.ReadTextString(); o.Points = r.ReadInt32();
                r.ReadTextString(); o.Nation = r.ReadUInt32();
                r.ReadTextString(); o.Region = checked((byte)r.ReadUInt32());
                r.ReadEndMap();
                return o;
            }
            catch (Exception e) when (CborUtil.IsDecodeError(e)) { return null; }
        }

        public byte[] ToBytes()
        {
            var w = new CborWriter();
            w.WriteStartMap(3);
            w.WriteTextString("points"); w.WriteInt32(Points);
            w.WriteTextString("nation"); w.WriteUInt32(Nation);
            w.WriteTextString("region"); w.WriteUInt32(Region);
            w.WriteEndMap();
            return w.Encode();
        }
    }

    // M2W_AddMobKills, M2W_AddPlayerHomepoints: struct ConquestAddCounter { int32 count; uint8 region; }
    public sealed class ConquestAddCounter
    {
        public int Count;
        public byte Region;

        public static ConquestAddCounter FromBytes(byte[] payload)
        {
            try
            {
                var r = new CborReader(payload);
                r.ReadStartMap();
                var o = new ConquestAddCounter();
                r.ReadTextString(); o.Count = r.ReadInt32();
                r.ReadTextString(); o.Region = checked((byte)r.ReadUInt32());
                r.ReadEndMap();
                return o;
            }
            catch (Exception e) when (CborUtil.IsDecodeError(e)) { return null; }
        }

        public byte[] ToBytes()
        {
            var w = new CborWriter();
            w.WriteStartMap(2);
            w.WriteTextString("count"); w.WriteInt32(Count);
            w.WriteTextString("region"); w.WriteUInt32(Region);
            w.WriteEndMap();
            return w.Encode();
        }
    }

    // W2M_BroadcastInfluencePoints: struct ConquestInfluenceUpdate { bool shouldUpdateZones; vector<influence_t> influences; }
    public sealed class ConquestInfluenceUpdate
    {
        public bool ShouldUpdateZones;
        public List<Influence> Influences = new();

        public byte[] ToBytes()
        {
            var w = new CborWriter();
            w.WriteStartMap(2);
            w.WriteTextString("shouldUpdateZones"); w.WriteBoolean(ShouldUpdateZones);
            w.WriteTextString("influences");
            w.WriteStartArray(Influences.Count);
            foreach (var i in Influences)
            {
                w.WriteStartMap(5);
                w.WriteTextString("sandoria_influence"); w.WriteInt32(i.SandoriaInfluence);
                w.WriteTextString("bastok_influence"); w.WriteInt32(i.BastokInfluence);
                w.WriteTextString("windurst_influence"); w.WriteInt32(i.WindurstInfluence);
                w.WriteTextString("mob_kills"); w.WriteInt32(i.MobKills);
                w.WriteTextString("player_homepoints"); w.WriteInt32(i.PlayerHomepoints);
                w.WriteEndMap();
            }
            w.WriteEndArray();
            w.WriteEndMap();
            return w.Encode();
        }

        public static ConquestInfluenceUpdate FromBytes(byte[] payload)
        {
            try
            {
                var r = new CborReader(payload);
                r.ReadStartMap();
                var o = new ConquestInfluenceUpdate();
                r.ReadTextString(); o.ShouldUpdateZones = r.ReadBoolean();
                r.ReadTextString();
                int n = r.ReadStartArray().Value;
                for (int k = 0; k < n; k++)
                {
                    r.ReadStartMap();
                    var i = new Influence();
                    r.ReadTextString(); i.SandoriaInfluence = r.ReadInt32();
                    r.ReadTextString(); i.BastokInfluence = r.ReadInt32();
                    r.ReadTextString(); i.WindurstInfluence = r.ReadInt32();
                    r.ReadTextString(); i.MobKills = r.ReadInt32();
                    r.ReadTextString(); i.PlayerHomepoints = r.ReadInt32();
                    r.ReadEndMap();
                    o.Influences.Add(i);
                }
                r.ReadEndArray();
                r.ReadEndMap();
                return o;
            }
            catch (Exception e) when (CborUtil.IsDecodeError(e)) { return null; }
        }
    }

    // W2M_BroadcastRegionControls / W2M_WeeklyUpdateEnd: struct ConquestRegionControlUpdate { vector<region_control_t> regionControls; }
    public sealed class ConquestRegionControlUpdate
    {
        public List<RegionControl> RegionControls = new();

        public byte[] ToBytes()
        {
            var w = new CborWriter();
            w.WriteStartMap(1);
            w.WriteTextString("regionControls");
            w.WriteStartArray(RegionControls.Count);
            foreach (var c in RegionControls)
            {
                w.WriteStartMap(2);
                w.WriteTextString("current"); w.WriteUInt32(c.Current);
                w.WriteTextString("prev"); w.WriteUInt32(c.Prev);
                w.WriteEndMap();
            }
            w.WriteEndArray();
            w.WriteEndMap();
            return w.Encode();
        }

        public static ConquestRegionControlUpdate FromBytes(byte[] payload)
        {
            try
            {
                var r = new CborReader(payload);
                r.ReadStartMap();
                var o = new ConquestRegionControlUpdate();
                r.ReadTextString();
                int n = r.ReadStartArray().Value;
                for (int k = 0; k < n; k++)
                {
                    r.ReadStartMap();
                    var c = new RegionControl();
                    r.ReadTextString(); c.Current = checked((byte)r.ReadUInt32());
                    r.ReadTextString(); c.Prev = checked((byte)r.ReadUInt32());
                    r.ReadEndMap();
                    o.RegionControls.Add(c);
                }
                r.ReadEndArray();
                r.ReadEndMap();
                return o;
            }
            catch (Exception e) when (CborUtil.IsDecodeError(e)) { return null; }
        }
    }
}
