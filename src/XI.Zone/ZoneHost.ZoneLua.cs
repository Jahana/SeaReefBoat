using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // The zone Lua lifecycle: luautils::OnZoneInitialize (each zone's onInitialize as it loads, zoneutils::LoadZoneList),
    // luautils::OnZoneIn (the zone's onZoneIn at the 0x00A login; a returned cutscene becomes the char's currentEvent,
    // sent in the GP_SERV_COMMAND_LOGIN and finished through the event engine's 0x05B), luautils::OnGameIn
    // (xi.player.onGameIn, with charutils::LoadChar's arrivedByZoning / gameLogin from char_stats.zoning),
    // luautils::AfterZoneIn (zoneutils::AfterZoneIn's afterZoneIn), luautils::OnZoneOut and CZone::CharZoneOut's
    // trigger-area leave (on a zone change, a logout and a timeout), CZone::CharZoneIn's clearTriggerAreas.
    // The trigger areas: CZone::InsertTriggerArea, CZone::CheckTriggerAreas on its 200 ms interval,
    // luautils::OnTriggerAreaEnter / OnTriggerAreaLeave and the char's charTriggerAreaIDs. A char in an instance runs
    // its instance's script with the instance, and its zone-out leave runs before it leaves the instance.
    // The zone's local vars: CZone::GetLocalVar / SetLocalVar / ResetLocalVars.
    // The Lua bindings: CLuaZone::getLocalVar / getLocalVars / setLocalVar / resetLocalVars,
    // registerCuboidTriggerArea / registerCylindricalTriggerArea / registerSphericalTriggerArea, levelRestriction,
    // getPlayers / getNPCs / getMobs / getRegionID / getTypeMask / get*Music / set*Music,
    // and CLuaTriggerArea (ITriggerArea).
    // The CLuaBaseEntity methods xi.player.onGameIn calls that had no binding: getCharVarsWithSuffix,
    // getTraverserEpoch / setTraverserEpoch, addGearSetMod / clearGearSetMods.
    // Not ported: m_isPCHidden (the HideFlag of the char packets: an OnZoneIn cutscene does not hide the char from the
    // others), ReportErrorToPlayer (errors go to the log only),
    // the isTestServer switch that leaves the trigger-area timer out (there is no such config here),
    // CLuaZone::reloadNavmesh / rebuildNavmesh (the navmesh builder is not ported;
    // getBattlefieldByInitiator and insertDynamicEntity are in ZoneHost.Battlefield.cs / DynamicEntities.cs). The mog
    // house enter / leave runs through the cities' onZoneIn (xi.moghouse.onMoghouseZoneEvent; getMoghouseFlag /
    // setMoghouseFlag / getPreviousZoneLineID / findItem are in ZoneHost.ZoneLuaGaps.cs).
    // CZone::CharZoneOut's other steps keep their own ports (the seal timer and moduleutils are not ported, the level
    // restriction is the party's, the treasure pool is LeaveTreasurePool).
    // The Lua hooks run with ZoneLuaEnabled, on by default; a verify suite may turn it off. The C++ has no such switch;
    // charutils::LoadChar's zoning flag is read either way.
    public sealed partial class ZoneHost
    {
        // run the zone Lua lifecycle hooks (luautils::OnZoneInitialize / OnZoneIn / OnGameIn / AfterZoneIn / OnZoneOut,
        // CZone::CheckTriggerAreas)
        public bool ZoneLuaEnabled = true;

        private const string ZoneLuaTriggerAreaType = "ITriggerArea";
        private const long ZoneLuaTriggerAreaIntervalMs = 200; // kTriggerAreaInterval: 1000 / (2.5 * 2)

        // CZone's localVars_ and m_triggerAreaList, by zone id
        private sealed class ZoneLuaZoneState
        {
            public readonly Dictionary<string, uint> LocalVars = new Dictionary<string, uint>();
            public readonly List<TriggerArea> TriggerAreas = new List<TriggerArea>();
        }

        // CCharEntity's arrivedByZoning, charTriggerAreaIDs and m_GearSetMods
        private sealed class ZoneLuaCharState
        {
            public bool ArrivedByZoning;
            public readonly HashSet<uint> TriggerAreaIds = new HashSet<uint>();
            public readonly List<(byte SetId, Mod ModId, short ModValue)> GearSetMods = new List<(byte, Mod, short)>();
        }

        private readonly Dictionary<ushort, ZoneLuaZoneState> zoneLuaZones_ = new Dictionary<ushort, ZoneLuaZoneState>();
        private readonly ConditionalWeakTable<CharData, ZoneLuaCharState> zoneLuaChars_ = new ConditionalWeakTable<CharData, ZoneLuaCharState>();
        private long zoneLuaNextTriggerCheckMs_;

        private ZoneLuaZoneState ZoneLuaZone(ushort zoneId)
        {
            if (!zoneLuaZones_.TryGetValue(zoneId, out var z)) zoneLuaZones_[zoneId] = z = new ZoneLuaZoneState();
            return z;
        }

        private ZoneLuaCharState ZoneLuaChar(CharData c) => zoneLuaChars_.GetValue(c, _ => new ZoneLuaCharState());

        // CZone::m_triggerAreaList, for the verify suites
        public IReadOnlyList<TriggerArea> TriggerAreasOf(ushort zoneId)
        {
            Zone(zoneId);
            return ZoneLuaZone(zoneId).TriggerAreas;
        }

        // CCharEntity::isInTriggerArea, for the verify suites
        public bool IsInTriggerArea(CharData c, uint triggerAreaId) => ZoneLuaChar(c).TriggerAreaIds.Contains(triggerAreaId);

        private static string ZoneLuaFile(ZoneInfo z) => $"./scripts/zones/{z.Name}/Zone.lua";

        // ---- charutils::LoadChar ------------------------------------------------------------------------------------

        internal const string SqlZoning = "SELECT zoning FROM char_stats WHERE charid = ? LIMIT 1";

        // charutils::LoadChar after LoadFromCharStatsSQL: the zoning flag the last zone-out left (1 a zone change, 2 a
        // login from the lobby) is read and cleared; a zone change is arrivedByZoning, a lobby login sets gameLogin
        private void ZoneLuaOnCharLoad(CharData c)
        {
            byte zoning = 0;
            var r = Exec(SqlZoning, BoundValue.Of(c.Id));
            if (r != null && r.Next()) zoning = r.Get<byte>("zoning");

            Exec("UPDATE char_stats SET zoning = 0 WHERE charid = ? LIMIT 1", BoundValue.Of(c.Id));
            ZoneLuaChar(c).ArrivedByZoning = zoning == 1;
            if (zoning == 2) c.LocalVars["gameLogin"] = 1;
        }

        // ---- luautils::OnZoneInitialize ------------------------------------------------------------------------------

        // zoneutils::LoadZoneList: a zone this server runs (IP != 0) runs its onInitialize once its NPCs and mobs are in.
        // The LoadLuaObjectFromFile of Zone.lua is LoadZoneScripts', which has run by now
        private void ZoneLuaOnZoneInitialize(ZoneInfo z, uint ip)
        {
            if (!ZoneLuaEnabled || ip == 0 || lua_ == null || scripts_ == null || z.Name == "") return;

            string filename = ZoneLuaFile(z);
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction(filename, "onInitialize");
                if (lua_.IsNil(-1)) return;
                lua_.PushObject(z, WeatherZoneLuaType);
                lua_.Call(1, 0);
            }
            catch (LuaException e)
            {
                ScriptError("luautils::onInitialize", e);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // ---- luautils::OnZoneIn / OnGameIn / AfterZoneIn / OnZoneOut -------------------------------------------------

        // luautils::OnZoneIn: InteractionGlobal.onZoneIn(player, prevZone, the zone's onZoneIn); a cutscene id (or
        // { id, textTable, flags }) becomes the char's currentEvent, a cutscene that locks the char.
        // Not ported: the invalid-zone warning (every zone id loads here), m_isPCHidden.
        private void ZoneLuaOnZoneIn(CharData c)
        {
            if (!ZoneLuaEnabled || lua_ == null) return;
            var z = AreaOf(c);
            string filename = ZoneLuaFile(z);

            int top = lua_.Top;
            try
            {
                lua_.GetPath("InteractionGlobal.onZoneIn");
                lua_.PushObject(c);
                lua_.Push((double)c.PrevZone);
                scripts_.PushFileFunction(filename, "onZoneIn");
                lua_.Call(3, 1);

                if (lua_.IsTable(-1))
                {
                    c.CurrentEvent.EventId = ZoneLuaTableInt(-1, 1, -1);
                    c.CurrentEvent.TextTable = (short)ZoneLuaTableInt(-1, 2, -1);
                    c.CurrentEvent.EventFlags = (uint)ZoneLuaTableInt(-1, 3, 0);
                }
                else if (lua_.IsNumber(-1))
                {
                    c.CurrentEvent.EventId = (int)lua_.ToIntegral(-1);
                }
            }
            catch (LuaException e)
            {
                ScriptError("luautils::onZoneIn", e);
                return;
            }
            finally
            {
                lua_.SetTop(top);
            }

            // On zone-in events
            if (c.CurrentEvent.EventId >= 0)
            {
                c.CurrentEvent.Type = EventType.Cutscene;
                ZoneLuaSetLocked(c);
            }
        }

        // sol::table::get_or(i, fallback) for a number entry
        private int ZoneLuaTableInt(int table, int i, int fallback)
        {
            lua_.RawGetI(table, i);
            int v = lua_.IsNumber(-1) ? (int)lua_.ToIntegral(-1) : fallback;
            lua_.Pop(1);
            return v;
        }

        // CCharEntity::setLocked(true): the char and its pet disengage and the char gives up its claim
        private void ZoneLuaSetLocked(CharData c)
        {
            c.Locked = true;
            Disengage(c);
            if (c.PPet is MobEntity pet && pet.Engaged) MobDisengage(pet, NowMs());
            RelinquishClaim(c, NowMs());
        }

        // luautils::OnGameIn: xi.player.onGameIn(player, needsFirstLogin, zoning); a char without the NEW_ADVENTURER
        // title (206, which charCreate adds) gets the first-login setup
        private void ZoneLuaOnGameIn(CharData c)
        {
            if (!ZoneLuaEnabled || lua_ == null) return;
            const string path = "xi.player.onGameIn";
            bool needsFirstLogin = !HasBit(c.Titles, 206);
            bool zoning = ZoneLuaChar(c).ArrivedByZoning;

            int top = lua_.Top;
            try
            {
                lua_.GetPath(path);
                if (lua_.IsNil(-1))
                {
                    log_($"luautils::callGlobalFunction: {path}: Function not found");
                    return;
                }
                lua_.PushObject(c);
                lua_.Push(needsFirstLogin);
                lua_.Push(zoning);
                lua_.Call(3, 0);
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

        // luautils::AfterZoneIn: InteractionGlobal.afterZoneIn(player, the zone's afterZoneIn)
        private void ZoneLuaAfterZoneIn(CharData c) => ZoneLuaCallZoneHook(c, "afterZoneIn", "luautils::afterZoneIn");

        // luautils::OnZoneOut: InteractionGlobal.onZoneOut(player, the zone's onZoneOut)
        private void ZoneLuaOnZoneOut(CharData c) => ZoneLuaCallZoneHook(c, "onZoneOut", "luautils::onZoneOut");

        private void ZoneLuaCallZoneHook(CharData c, string name, string where)
        {
            if (!ZoneLuaEnabled || lua_ == null) return;
            string filename = ZoneLuaFile(AreaOf(c));

            int top = lua_.Top;
            try
            {
                lua_.GetPath("InteractionGlobal." + name);
                lua_.PushObject(c);
                scripts_.PushFileFunction(filename, name);
                lua_.Call(2, 0);
            }
            catch (LuaException e)
            {
                ScriptError(where, e);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // ---- CZone::CharZoneIn / CharZoneOut ------------------------------------------------------------------------

        // CZone::CharZoneIn: CCharEntity::clearTriggerAreas
        private void ZoneLuaCharZoneIn(CharData c) => ZoneLuaChar(c).TriggerAreaIds.Clear();

        // CZone::CharZoneOut, its Lua steps: the first trigger area the char is in sees it leave (the char's cache keeps
        // it until the next CharZoneIn), then luautils::OnZoneOut
        private void ZoneLuaCharZoneOut(CharData c)
        {
            if (!ZoneLuaEnabled) return;
            var state = ZoneLuaChar(c);
            foreach (var area in ZoneLuaZone(c.Zone).TriggerAreas.ToArray())
            {
                if (state.TriggerAreaIds.Contains(area.Id))
                {
                    ZoneLuaOnTriggerAreaLeave(c, area);
                    break;
                }
            }
            ZoneLuaOnZoneOut(c);
        }

        // ---- CZone::CheckTriggerAreas ---------------------------------------------------------------------------------

        // CZone::CheckTriggerAreas on its kTriggerAreaInterval timer: a char not loading in (ZoningIn, set by
        // xi.player.onGameIn) enters an area its position is now inside and leaves one it is no longer inside
        private void ZoneLuaCheckTriggerAreas()
        {
            if (!ZoneLuaEnabled) return;
            long now = NowMs();
            if (now < zoneLuaNextTriggerCheckMs_) return;
            zoneLuaNextTriggerCheckMs_ = now + ZoneLuaTriggerAreaIntervalMs;

            foreach (var (c, s) in new List<KeyValuePair<CharData, MapSession>>(charSessions_))
            {
                if (!s.InZone || s.Char != c) continue;
                if (!zoneLuaZones_.TryGetValue(c.Zone, out var zone) || zone.TriggerAreas.Count == 0) continue;
                // Do not enter trigger areas while loading in. Set in xi.player.onGameIn
                if (c.LocalVars.TryGetValue("ZoningIn", out uint zoningIn) && zoningIn > 0) continue;

                var state = ZoneLuaChar(c);
                foreach (var area in zone.TriggerAreas.ToArray())
                {
                    if (area.IsPointInside(c.X, c.Y, c.Z))
                    {
                        if (!state.TriggerAreaIds.Contains(area.Id))
                        {
                            // Add the TriggerArea to the players cache of current TriggerAreas
                            state.TriggerAreaIds.Add(area.Id);
                            ZoneLuaOnTriggerAreaEnter(c, area);
                        }
                    }
                    else if (state.TriggerAreaIds.Contains(area.Id))
                    {
                        // Remove the TriggerArea from the players cache of current TriggerAreas
                        state.TriggerAreaIds.Remove(area.Id);
                        ZoneLuaOnTriggerAreaLeave(c, area);
                    }
                }
            }
        }

        // luautils::OnTriggerAreaEnter: the zone script, or for a char in an instance the instance's script, becomes
        // the event file (unless an event is running). Then the framework runs, fn being the instance's
        // onTriggerAreaEnter (xi.zones[zone].instances[name]) or the zone's:
        // InteractionGlobal.onTriggerAreaEnter(player, triggerArea, instance, fn)
        private void ZoneLuaOnTriggerAreaEnter(CharData c, TriggerArea area)
        {
            string zone = AreaOf(c).Name;
            var instance = c.Instance;
            string filename = instance != null
                ? $"./scripts/zones/{zone}/instances/{instance.Data.Name}.lua"
                : $"./scripts/zones/{zone}/Zone.lua";
            ZoneLuaTriggerAreaHook(c, area, filename, filename, "onTriggerAreaEnter", "luautils::onTriggerAreaEnter");
        }

        // luautils::OnTriggerAreaLeave: as the enter, with its own (unprefixed) file name; the instance's function only
        // while the char is in the instance's zone
        private void ZoneLuaOnTriggerAreaLeave(CharData c, TriggerArea area)
        {
            string zone = AreaOf(c).Name;
            var instance = c.Instance;
            string filename = instance != null
                ? $"scripts/zones/{zone}/instances/{instance.Data.Name}.lua"
                : $"scripts/zones/{zone}/Zone.lua";
            string fnFile = instance != null && c.Zone == instance.Zone ? filename : $"scripts/zones/{zone}/Zone.lua";
            ZoneLuaTriggerAreaHook(c, area, filename, fnFile, "onTriggerAreaLeave", "luautils::onTriggerAreaLeave");
        }

        // filename becomes the event file, fnFile names the script whose function InteractionGlobal falls back to
        private void ZoneLuaTriggerAreaHook(CharData c, TriggerArea area, string filename, string fnFile, string name,
                                            string where)
        {
            if (!ZoneLuaEnabled || lua_ == null) return;
            // player may be entering / leaving because of an earlier event (event that changes position)
            if (!c.IsInEvent) c.EventPreparation.ScriptFile = filename;

            int top = lua_.Top;
            try
            {
                lua_.GetPath("InteractionGlobal." + name);
                lua_.PushObject(c);
                lua_.PushObject(area, ZoneLuaTriggerAreaType);
                // PChar->PInstance
                if (c.Instance != null) PushInstance(c.Instance);
                else lua_.PushNil();
                scripts_.PushFileFunction(fnFile, name);
                lua_.Call(4, 0);
            }
            catch (LuaException e)
            {
                ScriptError(where, e);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // ---- bindings -------------------------------------------------------------------------------------------------

        // the CZone methods beside the weather file's getID / getName / getWeather
        private void ZoneLuaZoneMethods(Dictionary<string, LuaFunction> m)
        {
            static ZoneInfo Self(LuaState l) => WeatherLuaZone(l);

            // CLuaZone::getLocalVar: CZone::GetLocalVar (operator[]: a missing var reads, and is kept as, 0)
            m["getLocalVar"] = l =>
            {
                var vars = ZoneLuaZone(Self(l).Id).LocalVars;
                string key = l.ToStringArg(2);
                if (!vars.TryGetValue(key, out uint v)) vars[key] = v = 0;
                return Ret(l, v);
            };
            // CLuaZone::getLocalVars: { { varname, value }, ... }
            m["getLocalVars"] = l =>
            {
                var vars = ZoneLuaZone(Self(l).Id).LocalVars;
                l.NewTable(vars.Count);
                int i = 0;
                foreach (var (name, value) in vars)
                {
                    l.NewTable(0, 2);
                    l.PushString(name);
                    l.SetField(-2, "varname");
                    l.Push((double)value);
                    l.SetField(-2, "value");
                    l.RawSetI(-2, ++i);
                }
                return 1;
            };
            // CLuaZone::setLocalVar / resetLocalVars
            m["setLocalVar"] = l =>
            {
                ZoneLuaZone(Self(l).Id).LocalVars[l.ToStringArg(2)] = OptU32(l, 3);
                return 0;
            };
            m["resetLocalVars"] = l =>
            {
                ZoneLuaZone(Self(l).Id).LocalVars.Clear();
                return 0;
            };

            // CLuaZone::registerCuboidTriggerArea / registerCylindricalTriggerArea / registerSphericalTriggerArea:
            // CZone::InsertTriggerArea
            m["registerCuboidTriggerArea"] = l =>
            {
                uint id = ZoneLuaTriggerAreaId(l);
                ZoneLuaZone(Self(l).Id).TriggerAreas.Add(new CuboidTriggerArea(id, F(l, 3), F(l, 4), F(l, 5), F(l, 6), F(l, 7), F(l, 8),
                                                                               l.IsNumber(9) ? (float)l.ToNumber(9) : 0.0f));
                return 0;
            };
            m["registerCylindricalTriggerArea"] = l =>
            {
                uint id = ZoneLuaTriggerAreaId(l);
                ZoneLuaZone(Self(l).Id).TriggerAreas.Add(new CylindricalTriggerArea(id, F(l, 3), F(l, 4), F(l, 5)));
                return 0;
            };
            m["registerSphericalTriggerArea"] = l =>
            {
                uint id = ZoneLuaTriggerAreaId(l);
                ZoneLuaZone(Self(l).Id).TriggerAreas.Add(new SphericalTriggerArea(id, F(l, 3), F(l, 4), F(l, 5), F(l, 6)));
                return 0;
            };

            // CLuaZone::levelRestriction: always nil
            m["levelRestriction"] = l =>
            {
                l.PushNil();
                return 1;
            };

            // CLuaZone::getPlayers / getNPCs / getMobs: CZone::ForEachChar / ForEachNpc / ForEachMob
            m["getPlayers"] = l =>
            {
                ushort id = Self(l).Id;
                var list = new List<object>();
                foreach (var (c, s) in charSessions_)
                    if (s.InZone && s.Char == c && c.Zone == id) list.Add(c);
                return ZoneLuaPushArray(l, list);
            };
            m["getNPCs"] = l => ZoneLuaPushArray(l, Self(l).Npcs);
            m["getMobs"] = l => ZoneLuaPushArray(l, Self(l).Mobs);

            // CLuaZone::queryEntitiesByName: CZone::queryEntitiesByName's matches, warning when there are none
            m["queryEntitiesByName"] = l =>
            {
                var info = Self(l);
                string name = l.ToStringArg(2);
                var entities = QueryEntitiesByName(info, name);
                if (entities.Count == 0) log_($"Query for entity name: {name} in zone: {info.Name} returned no results");
                return ZoneLuaPushArray(l, entities);
            };

            // CLuaZone::getRegionID: zoneutils::GetCurrentRegion; getTypeMask: the zone.yaml types as xi::ZoneType bits
            m["getRegionID"] = l => Ret(l, ZoneRegions.Of(Self(l).Id));
            // CLuaZone::isNavigablePoint: on the zone's navmesh (anywhere, without one)
            m["isNavigablePoint"] = l =>
            {
                var nav = NavMeshOf(Self(l).Id);
                bool valid = nav == null || nav.ValidPosition((float)FieldNumber(l, 2, "x", 0), (float)FieldNumber(l, 2, "y", 0), (float)FieldNumber(l, 2, "z", 0));
                return Ret(l, valid);
            };
            // CLuaZone::getTerrainType / getFloorId(position): the collision mesh under the point (None / 0 without one)
            m["getTerrainType"] = l => Ret(l, (int)(XiMeshOf(Self(l).Id)?.GetTerrainAt((float)FieldNumber(l, 2, "x", 0),
                (float)FieldNumber(l, 2, "y", 0), (float)FieldNumber(l, 2, "z", 0)) ?? TerrainType.None));
            m["getFloorId"] = l => Ret(l, (int)(XiMeshOf(Self(l).Id)?.GetFloorId((float)FieldNumber(l, 2, "x", 0),
                (float)FieldNumber(l, 2, "y", 0), (float)FieldNumber(l, 2, "z", 0)) ?? 0));
            // CLuaZone::getUptime: the process's uptime in seconds (timer::get_uptime, the GM command !uptime)
            m["getUptime"] = l => Ret(l, (uint)(System.DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime).TotalSeconds);
            m["getTypeMask"] = l =>
            {
                int mask = 0;
                foreach (var t in Self(l).Types) mask |= enums_.Resolve("zone_type", t);
                return Ret(l, mask);
            };

            // CLuaZone::get / set SoloBattleMusic, PartyBattleMusic, BackgroundMusicDay, BackgroundMusicNight
            m["getSoloBattleMusic"] = l => Ret(l, Self(l).BattleSolo);
            m["getPartyBattleMusic"] = l => Ret(l, Self(l).BattleParty);
            m["getBackgroundMusicDay"] = l => Ret(l, Self(l).MusicDay);
            m["getBackgroundMusicNight"] = l => Ret(l, Self(l).MusicNight);
            m["setSoloBattleMusic"] = l =>
            {
                Self(l).BattleSolo = (ushort)l.ToUIntArg(2);
                return 0;
            };
            m["setPartyBattleMusic"] = l =>
            {
                Self(l).BattleParty = (ushort)l.ToUIntArg(2);
                return 0;
            };
            m["setBackgroundMusicDay"] = l =>
            {
                Self(l).MusicDay = (ushort)l.ToUIntArg(2);
                return 0;
            };
            m["setBackgroundMusicNight"] = l =>
            {
                Self(l).MusicNight = (ushort)l.ToUIntArg(2);
                return 0;
            };

            static float F(LuaState l, int i) => (float)l.ToNumberArg(i);
        }

        // the ITriggerArea constructor's warning for ID zero
        private uint ZoneLuaTriggerAreaId(LuaState l)
        {
            uint id = l.ToUIntArg(2);
            if (id == 0) log_("TriggerArea ID cannot be zero");
            return id;
        }

        // CZone::queryEntitiesByName: the NPCs, then the mobs, whose name is a full regex match of the pattern
        // (utils' matches, std::regex_match), memoized per pattern except for dynamic entities ("DE_"); like the C++, the
        // memo is never invalidated. Instances are not searched (TODO in C++ too).
        private readonly Dictionary<(ushort, string), List<object>> queryByNameResults_ = new Dictionary<(ushort, string), List<object>>();

        internal List<object> QueryEntitiesByName(ZoneInfo info, string pattern)
        {
            bool memo = !pattern.StartsWith("DE_", StringComparison.Ordinal);
            if (memo && queryByNameResults_.TryGetValue((info.Id, pattern), out var cached)) return cached;

            var regex = new System.Text.RegularExpressions.Regex("^(?:" + pattern + ")$");
            var entities = new List<object>();
            foreach (var npc in info.Npcs)
                if (regex.IsMatch(npc.Name)) entities.Add(npc);
            foreach (var mob in info.Mobs)
                if (regex.IsMatch(mob.Name)) entities.Add(mob);
            if (memo) queryByNameResults_[(info.Id, pattern)] = entities;
            return entities;
        }

        private static int ZoneLuaPushArray<T>(LuaState l, IReadOnlyList<T> items)
        {
            l.NewTable(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                l.PushObject(items[i]);
                l.RawSetI(-2, i + 1);
            }
            return 1;
        }

        // the ITriggerArea usertype and the CBaseEntity methods of this file
        private void RegisterZoneLuaBindings(Dictionary<string, LuaFunction> m)
        {
            static TriggerArea Area(LuaState l) =>
                l.ToObject(1) as TriggerArea ?? throw new InvalidOperationException("bad self: expected " + ZoneLuaTriggerAreaType);

            // CLuaTriggerArea
            lua_.RegisterType(ZoneLuaTriggerAreaType, new Dictionary<string, LuaFunction>
            {
                ["getTriggerAreaID"] = l => Ret(l, Area(l).Id),
                ["getCount"] = l => Ret(l, Area(l).Count),
                ["addCount"] = l => Ret(l, Area(l).AddCount((short)l.ToIntArg(2))),
                ["delCount"] = l => Ret(l, Area(l).DelCount((short)l.ToIntArg(2))),
            });

            // CLuaBaseEntity::getCharVarsWithSuffix: CCharEntity::getCharVarsWithSuffix, the unexpired char_vars whose
            // name ends in the suffix (cached as they are read), as { [varname] = value }
            m["getCharVarsWithSuffix"] = l =>
            {
                l.NewTable();
                if (Entity(l) is not CharData c) return 1;
                long now = UnixNow();
                var r = Exec(SqlVarsLike, BoundValue.Of(c.Id), BoundValue.Of("%" + l.ToStringArg(2)));
                while (r != null && r.Next())
                {
                    string varname = r.Get<string>("varname");
                    int value = r.Get<int>("value");
                    uint expiry = r.Get<uint>("expiry");
                    if (expiry == 0 || expiry > now)
                    {
                        c.CharVars[varname] = (value, expiry);
                        l.Push((double)value);
                        l.SetField(-2, varname);
                    }
                }
                return 1;
            };

            // CLuaBaseEntity::getTraverserEpoch: charutils::getTraverserEpoch, char_unlocks.traverser_start as a unix time
            m["getTraverserEpoch"] = l =>
            {
                if (Entity(l) is not CharData c) return Ret(l, 0);
                uint start = 0;
                var r = Exec("SELECT UNIX_TIMESTAMP(traverser_start) AS start FROM char_unlocks WHERE charid = ? LIMIT 1", BoundValue.Of(c.Id));
                if (r != null && r.Next() && !r.IsNull("start")) start = (uint)r.Get<long>("start");
                return Ret(l, start);
            };
            // CLuaBaseEntity::setTraverserEpoch: charutils::setTraverserEpoch (its CURRENT_TIMESTAMP()). The host's
            // clock, not the database's: getAvailableTraverserStones counts from UnixNow, so the two must be one clock
            // (the same time live; a golden run pins UnixNow, and the database's clock made the stone count drift by
            // one every 20 real hours)
            m["setTraverserEpoch"] = l =>
            {
                if (Entity(l) is CharData c)
                    Exec("UPDATE char_unlocks SET traverser_start = FROM_UNIXTIME(?) WHERE charid = ?",
                         BoundValue.Of(UnixNow()), BoundValue.Of(c.Id));
                return 0;
            };

            // CLuaBaseEntity::addGearSetMod: the set's mod is kept on the char (m_GearSetMods) and added
            m["addGearSetMod"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("CLuaBaseEntity::addGearSetMod() - m_pBaseEntity is not of type Char.");
                    return 0;
                }
                var mod = (Mod)(ushort)l.ToEnumArg(3); // xi::Mod
                short value = (short)(ushort)l.ToUIntArg(4);
                ZoneLuaChar(c).GearSetMods.Add(((byte)l.ToUIntArg(2), mod, value));
                c.AddModifier(mod, value);
                return 0;
            };
            // CLuaBaseEntity::clearGearSetMods: every gear set mod comes off
            m["clearGearSetMods"] = l =>
            {
                if (Entity(l) is CharData c)
                {
                    var mods = ZoneLuaChar(c).GearSetMods;
                    foreach (var (_, mod, value) in mods) c.DelModifier(mod, value);
                    mods.Clear();
                }
                return 0;
            };
        }
    }
}
