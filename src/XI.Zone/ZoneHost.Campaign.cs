using System;
using System.Collections.Generic;
using System.Linq;
using XI.Map;
using XI.Persistence;

namespace XI.Zone
{
    // campaign_handler.h's CampaignArmy and CampaignControl
    public static class CampaignArmy
    {
        public const byte Sandoria = 0, Bastok = 1, Windurst = 2, Orcish = 3, Quadav = 4, Yagudo = 5, Kindred = 6;
    }

    // campaign_handler.cpp: CCampaignHandler, every zone's (its campaign_map row, all zeros without one) and the setters
    // that write it back. The unions are counts held in memory only.
    public sealed class CampaignHandler
    {
        private readonly Func<string, BoundValue[], ResultSet> exec_;
        private readonly Action<string> log_;
        private readonly ushort zone_;

        public byte CampaignId, BattleStatus, ZoneControl, Heroism;
        public byte InfluenceSandoria, InfluenceBastok, InfluenceWindurst, InfluenceBeastman;
        public ushort Fortification, Resource, MaxFortification, MaxResource;
        private readonly byte[] unions_ = new byte[6]; // CampaignUnion: Adder 1 .. Eland 5

        internal CampaignHandler(ushort zone, Func<string, BoundValue[], ResultSet> exec, Action<string> log)
        {
            zone_ = zone;
            exec_ = exec;
            log_ = log;
            // LoadCampaignZone
            var r = exec_("SELECT id, zoneid, isbattle, nation, heroism, influence_sandoria, influence_bastok, influence_windurst, " +
                          "influence_beastman, current_fortifications, current_resources, max_fortifications, max_resources " +
                          "FROM campaign_map WHERE zoneid = ?", new[] { BoundValue.Of(zone) });
            if (r != null && r.Next())
            {
                CampaignId = (byte)r.Get<uint>("id");
                BattleStatus = (byte)r.Get<uint>("isbattle");
                ZoneControl = (byte)r.Get<uint>("nation");
                Heroism = (byte)r.Get<uint>("heroism");
                InfluenceSandoria = (byte)r.Get<uint>("influence_sandoria");
                InfluenceBastok = (byte)r.Get<uint>("influence_bastok");
                InfluenceWindurst = (byte)r.Get<uint>("influence_windurst");
                InfluenceBeastman = (byte)r.Get<uint>("influence_beastman");
                Fortification = (ushort)r.Get<uint>("current_fortifications");
                Resource = (ushort)r.Get<uint>("current_resources");
                MaxFortification = (ushort)r.Get<uint>("max_fortifications");
                MaxResource = (ushort)r.Get<uint>("max_resources");
            }
        }

        // GetInfluence: the beastmen share one
        public byte GetInfluence(byte army) => army switch
        {
            CampaignArmy.Sandoria => InfluenceSandoria,
            CampaignArmy.Bastok => InfluenceBastok,
            CampaignArmy.Windurst => InfluenceWindurst,
            CampaignArmy.Orcish or CampaignArmy.Quadav or CampaignArmy.Yagudo or CampaignArmy.Kindred => InfluenceBeastman,
            _ => 0,
        };

        public byte GetUnionCount(byte union) => union is >= 1 and <= 5 ? unions_[union] : (byte)0;

        public void SetUnionCount(byte union, byte amount)
        {
            if (union is >= 1 and <= 5) unions_[union] = amount;
        }

        private bool Update(string column, int value, string error)
        {
            if (exec_($"UPDATE campaign_map SET {column} = ? WHERE zoneid = ?", new[] { BoundValue.Of(value), BoundValue.Of(zone_) }) != null) return true;
            log_(error);
            return false;
        }

        public void SetBattleStatus(byte status)
        {
            if (Update("isbattle", status, "Unable to set campaign battle status.")) BattleStatus = status;
        }

        public void SetZoneControl(byte nation)
        {
            if (Update("nation", nation, "Unable to set campaign zone control.")) ZoneControl = nation;
        }

        public void SetHeroism(short amount)
        {
            short current = Math.Clamp(amount, (short)0, (short)200);
            if (Update("heroism", current, "Unable to set campaign region control.")) Heroism = (byte)current;
        }

        public void SetFortification(short amount)
        {
            short current = Math.Clamp(amount, (short)0, (short)MaxFortification);
            if (Update("current_fortifications", current, "Unable to update campaign fortifications.")) Fortification = (ushort)current;
        }

        public void SetResource(short amount)
        {
            short current = Math.Clamp(amount, (short)0, (short)MaxResource);
            if (Update("current_resources", current, "Unable to update campaign resources.")) Resource = (ushort)current;
        }

        public void SetMaxFortification(short amount)
        {
            short max = Math.Clamp(amount, (short)0, (short)1023);
            if (Update("max_fortifications", max, "Unable to update max campaign fortifications.")) MaxFortification = (ushort)max;
        }

        public void SetMaxResource(short amount)
        {
            short max = Math.Clamp(amount, (short)0, (short)1023);
            if (Update("max_resources", max, "Unable to update max campaign resources.")) MaxResource = (ushort)max;
        }

        // SetInfluence, capped at 250. The C++'s San d'Oria statement carries a stray "%d" for its zone, so it fails and
        // that influence never changes; kept.
        public void SetInfluence(byte army, short amount)
        {
            short current = Math.Clamp(amount, (short)0, (short)250);
            bool ok = army switch
            {
                CampaignArmy.Sandoria => false,
                CampaignArmy.Bastok => Updated("influence_bastok", current),
                CampaignArmy.Windurst => Updated("influence_windurst", current),
                CampaignArmy.Orcish or CampaignArmy.Quadav or CampaignArmy.Yagudo or CampaignArmy.Kindred => Updated("influence_beastman", current),
                _ => false,
            };
            if (!ok)
            {
                log_("Unable to update influence.");
                return;
            }
            switch (army)
            {
                case CampaignArmy.Bastok: InfluenceBastok = (byte)current; break;
                case CampaignArmy.Windurst: InfluenceWindurst = (byte)current; break;
                default: InfluenceBeastman = (byte)current; break;
            }
        }

        private bool Updated(string column, int value) =>
            exec_($"UPDATE campaign_map SET {column} = ? WHERE zoneid = ?", new[] { BoundValue.Of(value), BoundValue.Of(zone_) }) != null;
    }

    // campaign_system.cpp: the campaign state (each zone's region, sorted by its campaign id, and the areas each side
    // holds) and the seven armies' standing (campaign_nation), loaded once the zones are (zoneutils::LoadZoneList); the
    // allied notes; SendUpdate's two 0x071 INFLUENCE::CAMPAIGN halves.
    public sealed partial class ZoneHost
    {
        public sealed class CampaignRegion
        {
            public byte CampaignId, Status, Heroism, NationControl;
            public byte InfluenceSandoria, InfluenceBastok, InfluenceWindurst, InfluenceBeastman;
            public ushort CurrentFortifications, CurrentResources, MaxFortifications, MaxResources;
        }

        public sealed class CampaignNation
        {
            public byte Reconnaissance, Morale, Prosperity;
        }

        public sealed class CampaignStateData
        {
            public List<CampaignRegion> Regions = new List<CampaignRegion>();
            public List<CampaignNation> Nations = new List<CampaignNation>();
            public byte ControlSandoria, ControlBastok, ControlWindurst, ControlBeastman;
        }

        private const byte CampaignSandoriaMask = 2, CampaignBastokMask = 4, CampaignWindurstMask = 6;

        private Dictionary<ushort, CampaignHandler> campaignHandlers_;
        private readonly CampaignStateData campaign_ = new CampaignStateData();
        private bool campaignLoaded_;

        public CampaignStateData CampaignState => EnsureCampaign();

        // CZone::campaignHandler: one per zone this process serves (zone_settings at ProcessIpp, or all)
        public CampaignHandler CampaignHandlerOf(ushort zone)
        {
            CampaignHandlers().TryGetValue(zone, out var h);
            return h;
        }

        private Dictionary<ushort, CampaignHandler> CampaignHandlers()
        {
            if (campaignHandlers_ != null) return campaignHandlers_;
            campaignHandlers_ = new Dictionary<ushort, CampaignHandler>();
            string ipString = XI.Map.MapIpp.ToString(ProcessIpp).Split(':')[0];
            var r = Exec("SELECT zoneid FROM zone_settings WHERE IF(? <> 0, ? = zoneip AND ? = zoneport, TRUE) ORDER BY zoneid",
                BoundValue.Of(XI.Map.MapIpp.Ip(ProcessIpp)), BoundValue.Of(ipString), BoundValue.Of(XI.Map.MapIpp.Port(ProcessIpp)));
            var zones = new List<ushort>();
            while (r != null && r.Next()) zones.Add(r.Get<ushort>("zoneid"));
            foreach (var z in zones) campaignHandlers_[z] = new CampaignHandler(z, (q, a) => Exec(q, a), log_);
            return campaignHandlers_;
        }

        // the zone loader's campaign::LoadState / LoadNations; the host calls it at startup, the first use does if not
        public void LoadCampaign()
        {
            LoadCampaignState();
            LoadCampaignNations();
            campaignLoaded_ = true;
        }

        private CampaignStateData EnsureCampaign()
        {
            if (!campaignLoaded_) LoadCampaign();
            return campaign_;
        }

        // campaign::LoadState: each zone's region and the areas each side holds (nation (control + 1) * 2: San d'Oria 2,
        // Bastok 4, Windurst 6, anything else the beastmen's). A zone without a campaign_map row counts too, as in the
        // C++ (every CZone has a handler). The regions sort by campaign id, stably (zone order among equal ids; the C++
        // leaves that order unspecified).
        private void LoadCampaignState()
        {
            campaign_.Regions.Clear();
            campaign_.ControlSandoria = campaign_.ControlBastok = campaign_.ControlWindurst = campaign_.ControlBeastman = 0;
            var regions = new List<CampaignRegion>();
            foreach (var h in CampaignHandlers().Values)
            {
                byte nation = (byte)((h.ZoneControl + 1) * 2);
                switch (nation)
                {
                    case CampaignSandoriaMask: campaign_.ControlSandoria++; break;
                    case CampaignBastokMask: campaign_.ControlBastok++; break;
                    case CampaignWindurstMask: campaign_.ControlWindurst++; break;
                    default: campaign_.ControlBeastman++; break;
                }
                regions.Add(new CampaignRegion
                {
                    CampaignId = h.CampaignId,
                    Status = h.BattleStatus,
                    Heroism = h.Heroism,
                    InfluenceSandoria = h.GetInfluence(CampaignArmy.Sandoria),
                    InfluenceBastok = h.GetInfluence(CampaignArmy.Bastok),
                    InfluenceWindurst = h.GetInfluence(CampaignArmy.Windurst),
                    InfluenceBeastman = h.GetInfluence(CampaignArmy.Orcish),
                    CurrentFortifications = h.Fortification,
                    CurrentResources = h.Resource,
                    MaxFortifications = h.MaxFortification,
                    MaxResources = h.MaxResource,
                    NationControl = nation,
                });
            }
            campaign_.Regions.AddRange(regions.OrderBy(x => x.CampaignId));
        }

        // campaign::LoadNations: campaign_nation in id order
        private void LoadCampaignNations()
        {
            campaign_.Nations.Clear();
            var r = Exec("SELECT id, reconnaissance, morale, prosperity FROM campaign_nation ORDER BY id ASC");
            while (r != null && r.Next())
                campaign_.Nations.Add(new CampaignNation
                {
                    Reconnaissance = (byte)r.Get<uint>("reconnaissance"),
                    Morale = (byte)r.Get<uint>("morale"),
                    Prosperity = (byte)r.Get<uint>("prosperity"),
                });
        }

        public byte CampaignReconnaissance(byte army) => EnsureCampaign().Nations[army].Reconnaissance;
        public byte CampaignMorale(byte army) => EnsureCampaign().Nations[army].Morale;
        public byte CampaignProsperity(byte army) => EnsureCampaign().Nations[army].Prosperity;

        // SetReconnaissance (0-10), SetMorale and SetProsperity (0-100): written to campaign_nation, then held
        public void SetCampaignReconnaissance(byte army, sbyte amount) => SetCampaignNation(army, "reconnaissance", Math.Clamp((int)amount, 0, 10), "Unable to update nation reconnaissance.", (n, v) => n.Reconnaissance = v);
        public void SetCampaignMorale(byte army, sbyte amount) => SetCampaignNation(army, "morale", Math.Clamp((int)amount, 0, 100), "Unable to update nation morale.", (n, v) => n.Morale = v);
        public void SetCampaignProsperity(byte army, sbyte amount) => SetCampaignNation(army, "prosperity", Math.Clamp((int)amount, 0, 100), "Unable to update nation prosperity.", (n, v) => n.Prosperity = v);

        private void SetCampaignNation(byte army, string column, int current, string error, Action<CampaignNation, byte> set)
        {
            var state = EnsureCampaign();
            if (Exec($"UPDATE campaign_nation SET {column} = ? WHERE id = ?", BoundValue.Of(current), BoundValue.Of((int)army)) == null)
            {
                log_(error);
                return;
            }
            set(state.Nations[army], (byte)current);
        }

        // GetAlliedNotes / SetAlliedNotes: char_points.allied_notes
        public int CampaignAlliedNotes(CharData c) => GetPoints(c, "allied_notes");
        public void SetCampaignAlliedNotes(CharData c, int amount) => SetPoints(c, "allied_notes", amount);

        // campaign::SendUpdate: both halves of the map, regions 0-12 and 13-25
        public void SendCampaignUpdate(CharData c)
        {
            var state = EnsureCampaign();
            PushTo(c, CampaignPacket(c, state, 0));
            PushTo(c, CampaignPacket(c, state, 1));
        }

        // 0x071 GP_SERV_COMMAND_INFLUENCE::CAMPAIGN: the allied notes, the areas held, the seven armies, then thirteen
        // regions from the half's offset (the C++ reads past a short region list; here a missing one stays empty)
        private BasicPacket CampaignPacket(CharData c, CampaignStateData state, byte number)
        {
            var w = new PacketWriter(0x071, (uint)(4 + S2cInfluenceCampaign.SizeOf));
            var body = new byte[S2cInfluenceCampaign.SizeOf];
            S2cInfluenceCampaign.SetMode(body, InfluenceMode.Campaign);
            S2cInfluenceCampaign.SetLength(body, 0xC4);
            S2cInfluenceCampaign.SetAlliedNotes(body, CampaignAlliedNotes(c));
            S2cInfluenceCampaign.SetCASandoria(body, state.ControlSandoria);
            S2cInfluenceCampaign.SetCABastok(body, state.ControlBastok);
            S2cInfluenceCampaign.SetCAWindurst(body, state.ControlWindurst);
            S2cInfluenceCampaign.SetCABeastman(body, state.ControlBeastman);
            for (int i = 0; i < S2cInfluenceCampaign.NationsCount && i < state.Nations.Count; i++)
            {
                S2cInfluenceCampaign.SetNationReconnaissance(body, i, state.Nations[i].Reconnaissance);
                S2cInfluenceCampaign.SetNationMorale(body, i, state.Nations[i].Morale);
                S2cInfluenceCampaign.SetNationProsperity(body, i, state.Nations[i].Prosperity);
            }
            int start = number == 0 ? 0 : 13;
            S2cInfluenceCampaign.SetZoneOffset(body, (byte)start);
            for (int i = start; i < start + S2cInfluenceCampaign.ZonesCount && i < state.Regions.Count; i++)
            {
                var region = state.Regions[i];
                int idx = i - start;
                S2cInfluenceCampaign.SetZoneOwner(body, idx, region.NationControl);
                S2cInfluenceCampaign.SetZoneCurrentFortifications(body, idx, region.CurrentFortifications);
                S2cInfluenceCampaign.SetZoneCurrentResources(body, idx, region.CurrentResources);
                S2cInfluenceCampaign.SetZoneHeroism(body, idx, region.Heroism);
                S2cInfluenceCampaign.SetZoneInfluenceSandoria(body, idx, region.InfluenceSandoria);
                S2cInfluenceCampaign.SetZoneInfluenceBastok(body, idx, region.InfluenceBastok);
                S2cInfluenceCampaign.SetZoneInfluenceWindurst(body, idx, region.InfluenceWindurst);
                S2cInfluenceCampaign.SetZoneInfluenceBeastman(body, idx, region.InfluenceBeastman);
                S2cInfluenceCampaign.SetZoneMaxFortifications(body, idx, region.MaxFortifications);
                S2cInfluenceCampaign.SetZoneMaxResources(body, idx, region.MaxResources);
            }
            w.Bytes(4, body);
            return w.Packet;
        }
    }
}
