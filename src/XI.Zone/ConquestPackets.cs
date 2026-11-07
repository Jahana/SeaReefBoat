using System;
using System.Collections.Generic;
using System.Linq;
using XI.Common;
using XI.Map;
using XI.Persistence;
using XI.World;

namespace XI.Zone
{
    // map/conquest_data.cpp's ConquestData: the map's cache of the region controls and influences, loaded from the
    // conquest_system rows of the 19 conquest regions (RONFAURE..TAVNAZIA) and then replaced by the world's broadcasts
    public sealed class ConquestState
    {
        public const int RegionCount = Region.Tavnazia + 1;

        public RegionControl[] Controls { get; private set; } = new RegionControl[RegionCount];
        public Influence[] Influences { get; private set; } = new Influence[RegionCount];

        // ConquestData::ConquestData
        public static ConquestState Load(IDatabase db)
        {
            var state = new ConquestState();
            var r = db?.Execute("SELECT region_id, region_control, region_control_prev, sandoria_influence, bastok_influence, " +
                                "windurst_influence, mob_kills, player_homepoints FROM conquest_system", Array.Empty<BoundValue>());
            while (r != null && r.Next())
            {
                int id = r.Get<int>("region_id");
                if (id < 0 || id >= RegionCount) continue;
                state.Controls[id] = new RegionControl { Current = r.Get<byte>("region_control"), Prev = r.Get<byte>("region_control_prev") };
                state.Influences[id] = new Influence
                {
                    SandoriaInfluence = r.Get<int>("sandoria_influence"),
                    BastokInfluence = r.Get<int>("bastok_influence"),
                    WindurstInfluence = r.Get<int>("windurst_influence"),
                    MobKills = r.Get<int>("mob_kills"),
                    PlayerHomepoints = r.Get<int>("player_homepoints"),
                };
            }
            return state;
        }

        // ConquestData::updateInfluencePoints: the world's list replaces the cache, whatever its length
        public void UpdateInfluencePoints(IReadOnlyList<Influence> influences) => Influences = influences.ToArray();

        // ConquestData::updateRegionControls
        public void UpdateRegionControls(IReadOnlyList<RegionControl> regionControls) => Controls = regionControls.ToArray();

        // ConquestData::getRegionOwner: the cities and the other regions without conquest count as the beastmen's; any
        // other region reads 0 (the C++ also logs "Invalid conquest region passed to function"; see IsValidConquestRegion)
        public byte RegionOwner(byte region)
        {
            if (region >= 19 && region <= 27) return Nation.Beastmen; // SANDORIA..LIMBUS
            return region < Controls.Length ? Controls[region].Current : (byte)0;
        }

        // the regions getRegionOwner answers without its "Invalid conquest region" error: RONFAURE..LIMBUS
        public static bool IsValidConquestRegion(byte region) => region <= 27;

        // ConquestData::getRegionControlCount
        public byte RegionControlCount(byte nation)
        {
            byte n = 0;
            foreach (var c in Controls)
                if (c.Current == nation) n++;
            return n;
        }

        // ConquestData::getInfluence: 0 outside the conquest regions (the C++ also logs "Invalid influence region
        // requested")
        public int Influence(byte region, byte nation)
        {
            if (region >= Influences.Length) return 0;
            ref readonly var inf = ref Influences[region];
            switch (nation)
            {
                case Nation.Sandoria: return inf.SandoriaInfluence;
                case Nation.Bastok: return inf.BastokInfluence;
                case Nation.Windurst: return inf.WindurstInfluence;
                case Nation.Beastmen: return ConquestData.CalculateBeastmenInfluence(region, inf);
                default: return 0;
            }
        }
    }

    // The replies to the client's conquest request (c2s/0x05a): 0x05E CONQUEST and 0x071 INFLUENCE::COLONIZATION, and
    // the conquest_system.cpp formulas they are built from
    public static class ConquestPackets
    {
        // conquest::GetInfluenceGraphics
        public static byte InfluenceGraphics(int san, int bas, int win, int bst)
        {
            if (san == 0 && bas == 0 && win == 0 && bst == 0) return 0;
            if (san == bas && san == win && san == bst) return 0;
            if (bst > san && bst > win && bst > bas) return 64;

            long total = unchecked(san + bas + win);
            static int Step(int inf, long total, int unit) =>
                inf >= total * 0.65 ? 3 * unit : inf >= total * 0.5 ? 2 * unit : inf >= total * 0.25 ? unit : 0;
            return (byte)(Step(san, total, 1) + Step(bas, total, 4) + Step(win, total, 16));
        }

        // conquest::GetInfluenceRanking: the beastmen's influence is passed but, as in the C++, never compared
        public static byte InfluenceRanking(int san, int bas, int win, int bst = 0)
        {
            int ranking = 63;
            if (san >= bas) ranking -= 1;
            if (san >= win) ranking -= 1;
            if (bas >= san) ranking -= 4;
            if (bas >= win) ranking -= 4;
            if (win >= san) ranking -= 16;
            if (win >= bas) ranking -= 16;
            return (byte)ranking;
        }

        // conquest::GetBalance: two bits of place (1st..3rd) per nation, San d'Oria lowest
        public static byte Balance(byte sandoria, byte bastok, byte windurst)
        {
            static int Rank(int nation, int a, int b) => 1 + (nation < a ? 1 : 0) + (nation < b ? 1 : 0);
            return (byte)(Rank(sandoria, bastok, windurst) + (Rank(bastok, sandoria, windurst) << 2) + (Rank(windurst, bastok, sandoria) << 4));
        }

        // conquest::GetAlliance: the other two ally against a nation holding more than both together
        public static byte Alliance(byte sandoria, byte bastok, byte windurst)
        {
            if (sandoria > bastok + windurst) return 0b011;
            if (bastok > sandoria + windurst) return 0b101;
            if (windurst > sandoria + bastok) return 0b110;
            return 0;
        }

        private const long JstOffsetSec = 9 * 3600, EarthDaySec = 86400;
        private const long VanaDayMs = VanaTime.DAY * 1000 / VanaTime.TicksPerEarthSecond;

        // conquest::GetNextTally: Vana'diel days, rounded up, until earth_time::get_next_game_week (midnight Monday JST)
        public static byte NextTally(long unixNowMs)
        {
            long jstSec = Math.DivRem(unixNowMs, 1000L).Quotient + JstOffsetSec;
            long jstDay = Math.DivRem(jstSec, EarthDaySec).Quotient;
            int weekday = (int)((jstDay + 4) % 7);   // c_encoding: 1970-01-01 was a Thursday
            int gameWeekday = (weekday + 6) % 7;     // iso_encoding - 1: Monday 0
            long nextMidnightMs = ((unixNowMs + JstOffsetSec * 1000 + EarthDaySec * 1000 - 1) / (EarthDaySec * 1000) * EarthDaySec - JstOffsetSec) * 1000;
            long untilMs = nextMidnightMs + (6 - gameWeekday) * EarthDaySec * 1000 - unixNowMs;
            return (byte)((untilMs + VanaDayMs - 1) / VanaDayMs);
        }

        private const int ConquestAt = 4, RegionsAt = ConquestAt + 22, Besieged = ConquestAt + 156;

        // besieged_system.cpp's fixed state: Al Zahbi defending, each stronghold at level 1 defending with 95 forces
        private const uint OrdersAlZahbiDefense = 0, OrdersDefend = 4, StrongholdLevel1 = 1, StrongholdForces = 95;

        // 0x05E GP_SERV_COMMAND_CONQUEST. `region` is the char's zone's (zoneutils::GetCurrentRegion); its influence
        // shares fill the current-region fields.
        public static BasicPacket Conquest(ConquestState state, byte region, long unixNowMs, int conquestPoints, int imperialStanding)
        {
            var w = new PacketWriter(0x05E, 180);
            byte sandoriaRegions = state.RegionControlCount(Nation.Sandoria);
            byte bastokRegions = state.RegionControlCount(Nation.Bastok);
            byte windurstRegions = state.RegionControlCount(Nation.Windurst);

            for (byte id = Region.Ronfaure; id <= Region.Tavnazia; id++)
            {
                byte owner = state.RegionOwner(id);
                int san = state.Influence(id, Nation.Sandoria), bas = state.Influence(id, Nation.Bastok);
                int win = state.Influence(id, Nation.Windurst), bst = state.Influence(id, Nation.Beastmen);
                int at = RegionsAt + id * 4;
                w.U8(at + 0, InfluenceRanking(san, bas, win, bst));
                w.U8(at + 1, InfluenceRanking(san, bas, win));
                w.U8(at + 2, InfluenceGraphics(san, bas, win, bst));
                w.U8(at + 3, owner == Nation.Neutral ? 0 : owner + 1);

                if (id != region) continue;
                long total = unchecked(san + bas + win);
                long totalBeastmen = total + bst;
                long divB = totalBeastmen == 0 ? 1 : totalBeastmen, div = total == 0 ? 1 : total;
                w.U8(ConquestAt + 0x82, (byte)(san * 100L / divB));
                w.U8(ConquestAt + 0x83, (byte)(bas * 100L / divB));
                w.U8(ConquestAt + 0x84, (byte)(win * 100L / divB));
                w.U8(ConquestAt + 0x85, (byte)(san * 100L / div));
                w.U8(ConquestAt + 0x86, (byte)(bas * 100L / div));
                w.U8(ConquestAt + 0x87, (byte)(win * 100L / div));
                w.U8(ConquestAt + 0x90, (byte)(bst * 100L / divB));
            }

            w.U8(ConquestAt + 0, Balance(sandoriaRegions, bastokRegions, windurstRegions));
            w.U8(ConquestAt + 1, Alliance(sandoriaRegions, bastokRegions, windurstRegions));
            w.U8(ConquestAt + 0x88, NextTally(unixNowMs));
            w.U32(ConquestAt + 0x8C, (uint)conquestPoints);
            w.U8(ConquestAt + 0x98, 1); // Unknown9C

            // besiegedoverview_t: AstralCandescence:2 AlZahbiOrders:2 Mamook/Halvung/ArrapagoLevel:4 x3
            // Mamook/Halvung/ArrapagoOrders:3 x3 Unknown:1
            uint overview = 0 /* ALZAHBI */ | OrdersAlZahbiDefense << 2
                | StrongholdLevel1 << 4 | StrongholdLevel1 << 8 | StrongholdLevel1 << 12
                | OrdersDefend << 16 | OrdersDefend << 19 | OrdersDefend << 22 | 1u << 25;
            w.U32(Besieged, overview);
            // besiegedstronghold_t: Orders:3 Forces:8 Level:4 MirrorDestroyed:1 Mirrors:4 (mirrors / 2) Prisoners:4
            uint stronghold = OrdersDefend | StrongholdForces << 3 | StrongholdLevel1 << 11;
            for (int i = 0; i < 3; i++) w.U32(Besieged + 4 + i * 4, stronghold);
            w.U32(Besieged + 16, (uint)imperialStanding);
            return w.Packet;
        }

        // the colonization zones' MaxBivouacs, Yahse Hunting Grounds..Ra'Kaznar (slot 0 is unused)
        private static readonly uint[] MaxBivouacs = { 3, 3, 4, 3, 5, 4, 4, 0 };

        // 0x071 GP_SERV_COMMAND_INFLUENCE::COLONIZATION: every coalition at rank 1 and no colonization (the C++ fills in
        // fixed values)
        public static BasicPacket Colonization(int bayld)
        {
            var w = new PacketWriter(0x071, 204);
            const int o = 4;
            w.U8(o + S2cInfluenceColonization.OffMode, (byte)InfluenceMode.Colonization);
            w.U16(o + S2cInfluenceColonization.OffLength, 0x34);
            w.U32(o + S2cInfluenceColonization.OffRanks, 0x111111); // Pioneers..Mummers, 4 bits each
            for (int i = 0; i < MaxBivouacs.Length; i++)
                w.U32(o + S2cInfluenceColonization.ZoneWord(i + 1), MaxBivouacs[i] << ColonizationZoneEntry.MaxBivouacsBit);
            w.I32(o + S2cInfluenceColonization.OffBayld, bayld);
            return w.Packet;
        }
    }
}
