using System.Collections.Generic;
using System.Linq;
using XI.Ipc;
using XI.Script;
using XI.World;
using LuaFunction = XI.Script.LuaFunction;

namespace XI.Zone
{
    // The map side of conquest (map/conquest_system.cpp) that the zone Lua reaches: conquest::HandleMessage for the
    // world's W2M messages (HandleWeeklyTallyStart, HandleWeeklyTallyEnd, HandleInfluenceUpdate), UpdateConquestGM,
    // luautils::OnConquestUpdate (the zone's onConquestUpdate), luautils::SetRegionalConquestOverseers
    // (xi.conquest.setRegionalConquestOverseers), the luautils globals GetRegionOwner / GetRegionInfluence /
    // GetNationRank / GetConquestBalance / IsConquestAlliance / WeekUpdateConquest, and the CLuaBaseEntity methods
    // getCP / addCP / delCP / gainConquestInfluence / addConquestMobKills. conquest::AddConquestPoints /
    // GainInfluencePoints / GetBalance / IsAlliance keep their ports in ZoneHost.Exp.cs, the 0x05E packet and the
    // formulas in ConquestPackets.cs, and the world side is XI.World.ConquestSystem.
    // conquest::GetConquestData is the map's ConquestData cache (ConquestState), loaded from conquest_system on first
    // use and then kept in step by the world's broadcasts alone: W2M_BroadcastInfluencePoints replaces the influences,
    // W2M_WeeklyUpdateEnd / W2M_BroadcastRegionControls the region controls (a payload that does not decode is
    // dropped, as in the C++). Influence the world has saved shows once it next broadcasts (each Vana'diel hour).
    // ForEachZone is the zones this process has loaded with an IP (the ones whose onInitialize runs); the C++ map
    // loads all of its zones at start.
    public sealed partial class ZoneHost
    {
        // ConquestUpdate (conquest_system.h)
        private const byte ConquestTallyStart = 0, ConquestTallyEnd = 1, ConquestUpdateType = 2;

        // REGION_TYPE::DYNAMIS: the last region ForEachZone's conquest updates reach (the cities have no owner or influence)
        private const byte ConquestRegionDynamis = 23;

        // conquest::conquestData, lazily initialized
        private ConquestState conquestData_;

        // conquest::GetConquestData
        private ConquestState GetConquestData() => conquestData_ ??= ConquestState.Load(db_);

        private void RegisterConquestGlobals()
        {
            // luautils::GetRegionOwner -> conquest::GetRegionOwner
            lua_.SetGlobal("GetRegionOwner", l =>
            {
                byte region = (byte)l.ToUIntArg(1);
                if (!ConquestState.IsValidConquestRegion(region)) log_($"Invalid conquest region passed to function ({region})");
                return Ret(l, GetConquestData().RegionOwner(region));
            });
            // luautils::GetRegionInfluence -> conquest::GetInfluenceGraphics(region)
            lua_.SetGlobal("GetRegionInfluence", l => Ret(l, ConquestInfluenceGraphics(GetConquestData(), (byte)l.ToUIntArg(1))));
            // luautils::GetNationRank
            lua_.SetGlobal("GetNationRank", l => Ret(l, NationRank(GetConquestData(), (byte)l.ToUIntArg(1))));
            // luautils::GetConquestBalance -> conquest::GetBalance
            lua_.SetGlobal("GetConquestBalance", l => Ret(l, ConquestBalance(GetConquestData())));
            // luautils::IsConquestAlliance -> conquest::IsAlliance
            lua_.SetGlobal("IsConquestAlliance", l => Ret(l, IsConquestAlliance(GetConquestData())));
            // luautils::WeekUpdateConquest -> conquest::UpdateConquestGM
            lua_.SetGlobal("WeekUpdateConquest", l =>
            {
                ConquestUpdateGM((byte)l.ToUIntArg(1));
                return 0;
            });
        }

        private void RegisterConquestBindings(Dictionary<string, LuaFunction> m)
        {
            // CLuaBaseEntity::getCP: the char's nation's conquest points
            m["getCP"] = l =>
            {
                if (Pc(l, "getCP") is not CharData c) return Ret(l, 0);
                string column = ConquestPointsColumn(c);
                if (column == null) log_("Invalid nation received, returning nothing.");
                return Ret(l, GetPointsChecked(c, column ?? ""));
            };
            // CLuaBaseEntity::addCP / delCP: charutils::AddPoints, then GP_SERV_COMMAND_CONQUEST
            m["addCP"] = l =>
            {
                int cp = l.ToIntArg(2);
                if (Pc(l, "addCP") is CharData c) ConquestAddCp(c, cp);
                return 0;
            };
            m["delCP"] = l =>
            {
                int cp = l.ToIntArg(2);
                if (Pc(l, "delCP") is CharData c) ConquestAddCp(c, -cp);
                return 0;
            };
            // CLuaBaseEntity::gainConquestInfluence -> conquest::GainInfluencePoints
            m["gainConquestInfluence"] = l =>
            {
                int points = l.ToIntArg(2);
                if (Pc(l, "gainConquestInfluence") is not CharData c) return 0;
                if (points <= 0)
                {
                    log_($"gainConquestInfluence: non-positive amount ({points}) ignored.");
                    return 0;
                }
                GainInfluencePoints(c, (uint)points);
                return 0;
            };
            // CLuaBaseEntity::addConquestMobKills -> conquest::AddMobKills(count, the zone's region)
            m["addConquestMobKills"] = l =>
            {
                int count = l.ToIntArg(2);
                if (Pc(l, "addConquestMobKills") is not CharData c) return 0;
                SendIpc(new ConquestEvent
                {
                    TypeField = (byte)ConquestMessage.M2W_AddMobKills,
                    Payload = new ConquestAddCounter { Count = count, Region = ZoneRegions.Of(c.Zone) }.ToBytes(),
                });
                return 0;
            };
        }

        // charutils::GetConquestPointsName has no column for a char outside the three nations ("" after its error), which
        // AddPoints' GetPoints / SetPoints then log as an invalid type
        private void ConquestAddCp(CharData c, int cp)
        {
            string column = ConquestPointsColumn(c);
            if (column == null) log_("Invalid nation received, returning nothing.");
            AddPoints(c, column ?? "", cp);
            ConquestPushPacket(c, GetConquestData());
        }

        // GP_SERV_COMMAND_CONQUEST for the char's region
        private void ConquestPushPacket(CharData c, ConquestState state) =>
            PushTo(c, ConquestPackets.Conquest(state, ZoneRegions.Of(c.Zone), UnixNow() * 1000,
                GetPoints(c, ConquestPointsColumn(c)), GetPoints(c, "imperial_standing")));

        // conquest::GetInfluenceGraphics(REGION_TYPE)
        private static byte ConquestInfluenceGraphics(ConquestState state, byte region) =>
            ConquestPackets.InfluenceGraphics(state.Influence(region, Nation.Sandoria), state.Influence(region, Nation.Bastok),
                state.Influence(region, Nation.Windurst), state.Influence(region, Nation.Beastmen));

        // conquest::GetBalance()
        private static byte ConquestBalance(ConquestState state) =>
            ConquestPackets.Balance(state.RegionControlCount(Nation.Sandoria), state.RegionControlCount(Nation.Bastok),
                state.RegionControlCount(Nation.Windurst));

        // ---- conquest::HandleMessage ---------------------------------------------------------------------------------

        // conquest::HandleMessage: the world's broadcasts
        private void ConquestHandleMessage(ConquestEvent m)
        {
            switch ((ConquestMessage)m.TypeField)
            {
                case ConquestMessage.W2M_WeeklyUpdateStart:
                    ConquestHandleWeeklyTallyStart();
                    break;
                case ConquestMessage.W2M_WeeklyUpdateEnd:
                    if (ConquestRegionControlUpdate.FromBytes(m.Payload) is { } end) ConquestHandleWeeklyTallyEnd(end.RegionControls);
                    break;
                case ConquestMessage.W2M_BroadcastInfluencePoints:
                    if (ConquestInfluenceUpdate.FromBytes(m.Payload) is { } update) ConquestHandleInfluenceUpdate(update.Influences, update.ShouldUpdateZones);
                    break;
                case ConquestMessage.W2M_BroadcastRegionControls:
                    if (ConquestRegionControlUpdate.FromBytes(m.Payload) is { } controls) GetConquestData().UpdateRegionControls(controls.RegionControls);
                    break;
                default:
                    log_($"Message: unhandled conquest type message received: {m.TypeField}");
                    break;
            }
        }

        // conquest::UpdateConquestGM: the GM's tally start and update go to the world; the tally end reruns the local
        // callbacks with the (cached) region controls
        private void ConquestUpdateGM(byte type)
        {
            if (type == ConquestTallyStart) SendIpc(new ConquestEvent { TypeField = (byte)ConquestMessage.M2W_GM_WeeklyUpdate });
            else if (type == ConquestUpdateType) SendIpc(new ConquestEvent { TypeField = (byte)ConquestMessage.M2W_GM_ConquestUpdate });
            else if (type == ConquestTallyEnd) ConquestHandleWeeklyTallyEnd(GetConquestData().Controls);
        }

        // conquest::HandleWeeklyTallyStart
        private void ConquestHandleWeeklyTallyStart()
        {
            var state = GetConquestData();
            byte ranking = ConquestBalance(state);
            bool isConquestAlliance = IsConquestAlliance(state);
            foreach (var (z, region) in ConquestZones())
            {
                var (influence, owner) = ConquestZoneInfluence(state, region);
                ZoneLuaOnConquestUpdate(z, ConquestTallyStart, influence, owner, ranking, isConquestAlliance);
            }
        }

        // conquest::HandleWeeklyTallyEnd: the cache's region controls, the overseers of the 19 regions, then each zone's
        // onConquestUpdate and, for its chars, GP_SERV_COMMAND_CONQUEST and CheckLatentsZone
        private void ConquestHandleWeeklyTallyEnd(IReadOnlyList<RegionControl> regionControls)
        {
            GetConquestData().UpdateRegionControls(regionControls);
            for (byte i = 0; i <= 18; i++) ConquestSetRegionalConquestOverseers(i);

            var state = GetConquestData();
            byte ranking = ConquestBalance(state);
            bool isConquestAlliance = IsConquestAlliance(state);
            foreach (var (z, region) in ConquestZones())
            {
                var (influence, owner) = ConquestZoneInfluence(state, region);
                ZoneLuaOnConquestUpdate(z, ConquestTallyEnd, influence, owner, ranking, isConquestAlliance);
                foreach (var c in ConquestCharsIn(z.Id))
                {
                    ConquestPushPacket(c, state);
                    c.Latents.CheckLatentsZone();
                }
            }
            log_("Conquest Weekly Update is finished");
        }

        // conquest::HandleInfluenceUpdate: the cache's influences; with shouldUpdateZones, each zone's onConquestUpdate
        // and its chars' CheckLatentsZone
        private void ConquestHandleInfluenceUpdate(IReadOnlyList<Influence> influences, bool shouldUpdateZones)
        {
            GetConquestData().UpdateInfluencePoints(influences);
            if (!shouldUpdateZones) return;
            var state = GetConquestData();
            byte ranking = ConquestBalance(state);
            bool isConquestAlliance = IsConquestAlliance(state);
            foreach (var (z, region) in ConquestZones())
            {
                var (influence, owner) = ConquestZoneInfluence(state, region);
                ZoneLuaOnConquestUpdate(z, ConquestUpdateType, influence, owner, ranking, isConquestAlliance);
                foreach (var c in ConquestCharsIn(z.Id)) c.Latents.CheckLatentsZone();
            }
        }

        // zoneutils::ForEachZone, limited to the regions up to DYNAMIS. A snapshot: the hooks may load more zones.
        private List<(ZoneInfo Zone, byte Region)> ConquestZones() =>
            zones_.Values.Where(z => z.Ip != 0 && ZoneRegions.Of(z.Info.Id) <= ConquestRegionDynamis)
                         .Select(z => (z.Info, ZoneRegions.Of(z.Info.Id))).ToList();

        // the cities and Dynamis have neither influence nor owner (0); the conquest regions their graphics and owner
        private static (byte Influence, byte Owner) ConquestZoneInfluence(ConquestState state, byte region) =>
            region <= Region.Tavnazia ? (ConquestInfluenceGraphics(state, region), state.RegionOwner(region)) : ((byte)0, (byte)0);

        // CZone::ForEachChar
        private List<CharData> ConquestCharsIn(ushort zoneId)
        {
            var list = new List<CharData>();
            foreach (var (c, s) in charSessions_)
                if (s.InZone && s.Char == c && c.Zone == zoneId) list.Add(c);
            return list;
        }

        // luautils::SetRegionalConquestOverseers: callGlobal("xi.conquest.setRegionalConquestOverseers", regionID)
        private void ConquestSetRegionalConquestOverseers(byte region)
        {
            if (lua_ == null) return;
            const string path = "xi.conquest.setRegionalConquestOverseers";
            int top = lua_.Top;
            try
            {
                lua_.GetPath(path);
                if (lua_.IsNil(-1))
                {
                    log_($"luautils::callGlobalFunction: {path}: Function not found");
                    return;
                }
                lua_.Push((double)region);
                lua_.Call(1, 0);
            }
            catch (LuaException e)
            {
                ScriptError(path, e);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::OnConquestUpdate: xi.zones[name].Zone.onConquestUpdate(zone, type, influence, owner, ranking,
        // isConquestAlliance), when the zone has one
        private void ZoneLuaOnConquestUpdate(ZoneInfo z, byte type, byte influence, byte owner, byte ranking, bool isConquestAlliance)
        {
            if (lua_ == null || scripts_ == null || z.Name == "") return;
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction(ZoneLuaFile(z), "onConquestUpdate");
                if (lua_.IsNil(-1)) return;
                lua_.PushObject(z, WeatherZoneLuaType);
                lua_.Push((double)type);
                lua_.Push((double)influence);
                lua_.Push((double)owner);
                lua_.Push((double)ranking);
                lua_.Push(isConquestAlliance);
                lua_.Call(6, 0);
            }
            catch (LuaException e)
            {
                ScriptError("luautils::onConquestUpdate", e);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }
    }
}
