using System;
using System.Collections.Generic;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // CLuaBaseEntity: the methods the ported interactions reach, for a char (CharData) or a static NPC (NpcEntity).
    // Argument 1 is the entity the method is called on; a method called on the wrong kind of entity warns and returns
    // the C++ default, as the bindings do. Every other method raises "binding not ported".
    public sealed partial class ZoneHost
    {
        private const string BaseEntityType = "CBaseEntity";
        private const int TeleportAbysseaConflux = 5, TeleportHomepoint = 9, TeleportSurvival = 10, TeleportWaypoint = 11, TeleportEschanPortal = 12;
        // CharData.Waypoints: waypoint_t (uint32 access[2], bool confirmation, padding), then the eschan portals at 12
        private const int WaypointConfirmation = 8;
        private const int MaxAbysseaZones = 9;
        private const int MaxQuestArea = 11, MaxQuestId = 256, MaxMissionArea = 15, MaxMissionId = 851, MissionLogCoP = 6;

        private void RegisterBindings()
        {
            lua_.TypeNameOf = o => o switch
            {
                BattleEntity or NpcEntity => BaseEntityType,
                Battlefield => BattlefieldLuaType,
                ActionPacket => ActionType,
                Melee.Swing => AttackLuaType,
                DropList => LootLuaType,
                Item => ItemType,
                MobSkill { IsPetSkill: true } => PetSkillType, MobSkill => MobSkillType, Spell => SpellType, StatusEffect => StatusEffectType, WeaponSkill => WeaponSkillType, Trait => TraitType, AbilityInfo => AbilityType,
                _ => o.GetType().Name,
            };

            RegisterChocoboRaceGlobals();
            RegisterConquestGlobals();
            RegisterZoneLuaGapGlobals();
            RegisterLuautilsGlobals();
            RegisterAdminGlobals();
            RegisterInitGlobals();
            RegisterFishingContestGlobals();
            RegisterQuestScriptGlobals();

            // luautils globals that need the zone or the database
            lua_.SetGlobal("GetServerVariable", l => Ret(l, ServerVar(l.ToStringArg(1))));
            // luautils::DrawIn(entity, { x, y, z, rot }, offset, degrees)
            lua_.SetGlobal("DrawIn", l =>
            {
                if (l.ToObject(1) is BattleEntity b && l.IsTable(2))
                    DrawIn(b, (float)FieldNumber(l, 2, "x", 0), (float)FieldNumber(l, 2, "y", 0), (float)FieldNumber(l, 2, "z", 0),
                           (byte)LuaState.LlRound(FieldNumber(l, 2, "rot", 0)), (float)l.ToNumber(3), (float)l.ToNumber(4));
                return 0;
            });
            // luautils::StartElevator / GetElevatorState (xi.elevator ids; -1 without such a lift)
            lua_.SetGlobal("RunElevator", l =>
            {
                StartElevator((byte)l.ToEnum(1)); // xi::Elevator
                return 0;
            });
            lua_.SetGlobal("GetElevatorState", l =>
            {
                l.Push((double)ElevatorState((byte)l.ToEnum(1)));
                return 1;
            });
            // luautils::NearLocation(pos, radius, theta): nearPosition, radius yalms off the point at theta turned from
            // its rot (x / y / z / rot read as the C++ does: a missing field is 0)
            lua_.SetGlobal("NearLocation", l =>
            {
                float cx = (float)FieldNumber(l, 1, "x", 0), cy = (float)FieldNumber(l, 1, "y", 0), cz = (float)FieldNumber(l, 1, "z", 0);
                byte rot = (byte)LuaState.LlRound(FieldNumber(l, 1, "rot", 0)); // get<uint8>
                float radius = (float)l.ToNumber(2);
                float total = ZoneLine.RotationToRadian(rot) + (float)l.ToNumber(3);
                l.NewTable();
                l.Push((double)(cx + MathF.Cos(2 * MathF.PI - total) * radius)); l.SetField(-2, "x");
                l.Push((double)cy); l.SetField(-2, "y");
                l.Push((double)(cz + MathF.Sin(2 * MathF.PI - total) * radius)); l.SetField(-2, "z");
                return 1;
            });
            // luautils::GetFurthestValidPosition(entity, distance, theta): how far a walk along the navmesh toward the
            // point `distance` off the entity's facing gets, nil when the entity is off the mesh; a zone without a
            // navmesh gives the point itself
            lua_.SetGlobal("GetFurthestValidPosition", l =>
            {
                (float X, float Y, float Z, byte Rot, ushort Zone)? from = l.ToObject(1) switch
                {
                    BattleEntity b => (b.X, b.Y, b.Z, b.Rotation, b.Zone),
                    NpcEntity n => (n.X, n.Y, n.Z, n.Rotation, ZoneOf(n)),
                    _ => null,
                };
                if (from is not { } f)
                {
                    log_("luautils::GetFurthestValidPosition: fromTarget is null or invalid");
                    l.PushNil();
                    return 1;
                }
                float total = ZoneLine.RotationToRadian(f.Rot) + (float)l.ToNumber(3);
                float distance = (float)l.ToNumber(2);
                var target = (X: f.X + MathF.Cos(2 * MathF.PI - total) * distance, Y: f.Y, Z: f.Z + MathF.Sin(2 * MathF.PI - total) * distance);
                (float X, float Y, float Z)? valid = NavMeshOf(f.Zone) is { } nav
                    ? nav.FindFurthestValidPoint(f.X, f.Y, f.Z, target.X, target.Y, target.Z)
                    : target;
                if (valid is not { } v)
                {
                    l.PushNil();
                    return 1;
                }
                l.NewTable();
                l.Push((double)v.X); l.SetField(-2, "x");
                l.Push((double)v.Y); l.SetField(-2, "y");
                l.Push((double)v.Z); l.SetField(-2, "z");
                return 1;
            });
            lua_.SetGlobal("GetNPCByID", l =>
            {
                uint id = l.ToUIntArg(1);
                // GetNPCByID(id, instance): the run's NPC at that targid
                var run = InstanceArg(l, 2);
                var npc = run != null ? InstanceEntity(run, (ushort)(id & 0xFFF), TypeNpc) as NpcEntity
                    : HeldZone((ushort)(id >> 12 & 0xFFF))?.NpcById.TryGetValue(id, out var n) == true ? n : null;
                if (npc == null) log_($"lua: GetNPCByID NPC doesn't exist ({id})");
                l.PushObject(npc);
                return 1;
            });
            lua_.SetGlobal("GetPlayerByID", l =>
            {
                uint id = l.ToUIntArg(1);
                CharData found = null;
                foreach (var c in charSessions_.Keys)
                    if (c.Id == id) found = c;
                l.PushObject(found);
                return 1;
            });

            var methods = new Dictionary<string, LuaFunction>
            {
                ["getID"] = l => Ret(l, IdOf(Entity(l))),
                ["getName"] = l =>
                {
                    l.PushString(NameOf(Entity(l)));
                    return 1;
                },
                ["getZoneID"] = l => Ret(l, ZoneOf(Entity(l))),
                ["getZoneName"] = l =>
                {
                    l.PushString(Zone(ZoneOf(Entity(l))).Info.Name);
                    return 1;
                },
                ["getNation"] = l => Ret(l, Pc(l, "getNation") is CharData c ? c.Nation : 0),
                ["getRace"] = l => Ret(l, Pc(l, "getRace") is CharData c ? c.Look.Race : 0),
                ["getFace"] = l => Ret(l, Pc(l, "getFace") is CharData c ? c.Look.Face : 0),
                // CAMANContainer::hasMentorUnlocked / setMentorUnlocked
                ["getMentor"] = l => Ret(l, Pc(l, "getMentor") is CharData c && c.MentorUnlocked),
                ["setMentor"] = l =>
                {
                    if (Pc(l, "setMentor") is not CharData c) return 0;
                    SetMentorUnlocked(c, l.ToBooleanArg(2));
                    c.UpdateMask |= CharStats.UpdateHp;
                    return 0;
                },

                // char_vars
                ["getCharVar"] = GetCharVar,
                ["getVar"] = GetCharVar,
                ["setCharVar"] = SetCharVar,
                ["setVar"] = SetCharVar,
                ["incrementCharVar"] = l =>
                {
                    if (Entity(l) is not CharData c) return 0;
                    IncrementCharVar(c, l.ToStringArg(2), l.ToIntArg(3));
                    return 0;
                },
                ["setCharVarExpiration"] = l =>
                {
                    if (Entity(l) is not CharData c) return 0;
                    string name = l.ToStringArg(2);
                    SetCharVar(c, name, CharVar(c, name), l.ToUIntArg(3));
                    return 0;
                },
                ["clearVarsWithPrefix"] = l =>
                {
                    if (Entity(l) is not CharData c) return 0;
                    string prefix = l.ToStringArg(2);
                    if (prefix.Length < 5)
                    {
                        log_($"lua: Prefix too short to clear with: '{prefix}'");
                        return 0;
                    }
                    foreach (var name in new List<string>(c.CharVars.Keys))
                        if (name.StartsWith(prefix, StringComparison.Ordinal)) c.CharVars[name] = (0, 0);
                    PersistProgress(c, () => Exec("DELETE FROM char_vars WHERE charid = ? AND varname LIKE ?", BoundValue.Of(c.Id), BoundValue.Of(prefix + "%")) != null,
                                    p => p.ClearVarsWithPrefix(prefix));
                    return 0;
                },

                // m_localVars
                ["getLocalVar"] = l => Ret(l, LocalVars(Entity(l)).TryGetValue(l.ToStringArg(2), out var v) ? v : 0u),
                ["setLocalVar"] = l =>
                {
                    LocalVars(Entity(l))[l.ToStringArg(2)] = l.ToUIntArg(3);
                    return 0;
                },
                // CLuaBaseEntity::getLocalVars: { { varname = ..., value = ... }, ... } in the std::map's (name) order
                ["getLocalVars"] = l =>
                {
                    var vars = LocalVars(Entity(l));
                    var names = new List<string>(vars.Keys);
                    names.Sort(StringComparer.Ordinal);
                    l.NewTable(names.Count, 0);
                    for (int i = 0; i < names.Count; i++)
                    {
                        l.NewTable(0, 2);
                        l.PushString(names[i]);
                        l.SetField(-2, "varname");
                        l.Push((double)vars[names[i]]);
                        l.SetField(-2, "value");
                        l.RawSetI(-2, i + 1);
                    }
                    return 1;
                },
                ["resetLocalVars"] = l =>
                {
                    LocalVars(Entity(l)).Clear();
                    return 0;
                },

                // events
                ["isInEvent"] = l => Ret(l, Pc(l, "isInEvent") is CharData c && c.IsInEvent),
                ["didGetMessage"] = l => Ret(l, Pc(l, "didGetMessage") is CharData c && c.GotMessage),
                ["resetGotMessage"] = l =>
                {
                    if (Pc(l, "resetGotMessage") is CharData c) c.GotMessage = false;
                    return 0;
                },
                ["startEvent"] = l => StartEvent(l, EventType.Normal),
                ["startEventString"] = l => StartEvent(l, EventType.Normal),
                ["startCutscene"] = l => StartEvent(l, EventType.Cutscene),
                ["startOptionalCutscene"] = l => StartEvent(l, EventType.OptionalCutscene),
                ["updateEvent"] = UpdateEvent,
                ["release"] = Release,

                // messages
                ["messageSpecial"] = MessageSpecial,
                ["showText"] = ShowText,
                ["messageText"] = MessageText,

                // gil, key items, quests, missions
                ["getGil"] = l => Ret(l, Entity(l) is CharData c ? Gil(c)?.Quantity ?? 0 : 0u),
                ["delGil"] = DelGil,
                ["hasKeyItem"] = l => Ret(l, Pc(l, "hasKeyItem") is CharData c && HasKeyItem(c, (ushort)l.ToEnumArg(2))),
                ["getQuestStatus"] = l => Ret(l, Pc(l, "getQuestStatus") is CharData c ? QuestStatus(c, l.ToEnumArg(2), l.ToIntArg(3)) : 0),
                ["hasCompletedQuest"] = l => Ret(l, Pc(l, "hasCompletedQuest") is CharData c && QuestStatus(c, l.ToEnumArg(2), l.ToIntArg(3)) == 2),
                ["getCurrentMission"] = GetCurrentMission,
                ["getMissionStatus"] = GetMissionStatus,
                ["hasCompletedMission"] = HasCompletedMission,

                // position, teleports, home point
                ["setPos"] = SetPos,
                ["hasTeleport"] = HasTeleport,
                ["addTeleport"] = AddTeleport,
                ["getTeleport"] = GetTeleport,
                ["getTeleportTable"] = GetTeleportTable,
                ["getTeleportMenu"] = GetTeleportMenu,
                ["setTeleportMenu"] = SetTeleportMenu,
                ["setHomePoint"] = SetHomePoint,
            };
            AddProgressBindings(methods);
            AddJobBindings(methods);
            RegisterShopBindings(methods);
            RegisterGuildBindings(methods);
            RegisterChatBindings(methods);
            RegisterTradeBindings(methods);
            RegisterRoeBindings(methods);
            RegisterTreasurePoolBindings(methods);
            RegisterItemUseBindings(methods);
            RegisterStatusEffectBindings(methods);
            RegisterLatentBindings(methods);
            RegisterMobBindings(methods);
            RegisterLootBindings(methods);
            RegisterRaiseBindings(methods);
            RegisterMobScriptBindings(methods);
            RegisterMobAiBindings(methods);
            RegisterMobRoamBindings(methods);
            RegisterMobFollowBindings(methods);
            RegisterInactiveBindings(methods);
            RegisterPlayerBindings(methods);
            RegisterNpcAiBindings(methods);
            RegisterEntityBindings(methods);
            RegisterUtilityBindings(methods);
            RegisterMiscBindings(methods);
            RegisterBindings12(methods);
            RegisterSmallSystemBindings(methods);
            RegisterFishingContestBindings(methods);
            RegisterMobPetBindings(methods);
            RegisterCharmBindings(methods);
            RegisterTrustBindings(methods);
            RegisterAutomatonBindings(methods);
            RegisterBattlefieldBindings(methods);
            RegisterDynamicEntityBindings(methods);
            RegisterTailBindings(methods);
            RegisterMountBindings(methods);
            RegisterSupportBindings(methods);
            RegisterGmBindings(methods);
            RegisterInstanceBindings(methods);
            RegisterMonstrosityBindings(methods);
            RegisterEnmityBindings(methods);
            RegisterMobSkillBindings(methods);
            RegisterMagicBindings(methods);
            RegisterWeaponSkillBindings(methods);
            RegisterCombatBindings(methods);
            RegisterTraitBindings(methods);
            RegisterWeatherBindings(methods);
            RegisterAbilityBindings(methods);
            RegisterZoneLuaBindings(methods);
            RegisterCasketBindings(methods);
            RegisterConquestBindings(methods);
            RegisterCharCreateBindings(methods);
            RegisterZoneLuaGapBindings(methods);
            RegisterInitBindings(methods);
            lua_.RegisterType(BaseEntityType, methods, new Dictionary<string, LuaFunction>
            {
                // operator<<(std::ostream&, const CLuaBaseEntity&), which sol makes the usertype's __tostring:
                // "CLuaBaseEntity(TYPE_MOB | 17187000 | DE_Fafnir | Fafnir)", the packet name only when there is one
                ["__tostring"] = l =>
                {
                    var e = l.ToObject(1);
                    if (e == null)
                    {
                        l.PushString("CLuaBaseEntity(nullptr)");
                        return 1;
                    }
                    string type = ObjTypeOf(e) switch
                    {
                        ObjTypePc => "TYPE_PC", ObjTypeNpc => "TYPE_NPC", ObjTypeMob => "TYPE_MOB",
                        ObjTypePet => "TYPE_PET", ObjTypeTrust => "TYPE_TRUST", _ => "UNKNOWN",
                    };
                    string packetName = e switch { MobEntity m => m.PacketName, NpcEntity n => n.PacketName, _ => "" };
                    l.PushString($"CLuaBaseEntity({type} | {IdOf(e)} | {NameOf(e)}" + (string.IsNullOrEmpty(packetName) ? ")" : $" | {packetName})"));
                    return 1;
                },
            });
        }

        // ---- helpers ----------------------------------------------------------------------------------------------

        private static object Entity(LuaState l) =>
            l.ToObject(1) ?? throw new InvalidOperationException("bad self: expected " + BaseEntityType);

        // the char a PC-only method runs on, or null after the C++ warning
        private CharData Pc(LuaState l, string method)
        {
            if (Entity(l) is CharData c) return c;
            log_($"lua: Invalid entity type calling function ({method} on {NameOf(Entity(l))}).");
            return null;
        }

        // the battle entity (a char or a mob) a battle-entity method runs on, or null after the C++ warning on an NPC
        private BattleEntity Battle(LuaState l)
        {
            if (Entity(l) is BattleEntity b) return b;
            log_($"Invalid Entity (NPC: {NameOf(Entity(l))}) calling function.");
            return null;
        }

        // the mob a mob-only method runs on, or null after the C++ error
        private MobEntity Mob(LuaState l)
        {
            if (Entity(l) is MobEntity m) return m;
            log_($"function call on invalid entity! (name: {NameOf(Entity(l))} type: {ObjTypeOf(Entity(l))})");
            return null;
        }

        private static uint IdOf(object e) => e is BattleEntity b ? b.Id : ((NpcEntity)e).Id;

        private static string NameOf(object e) => e is BattleEntity b ? b.Name : ((NpcEntity)e).Name;

        // CBaseEntity::getZone: loc.zone's id, or loc.destination once a zone change has taken the char out of it
        private static ushort ZoneOf(object e) => e is CharData { ZonedOut: true } c ? c.Destination : e is BattleEntity b ? b.Zone : (ushort)(((NpcEntity)e).Id >> 12 & 0xFFF);

        private static Dictionary<string, uint> LocalVars(object e) => e is BattleEntity b ? b.LocalVars : ((NpcEntity)e).LocalVars;

        private static int Ret(LuaState l, double v)
        {
            l.Push(v);
            return 1;
        }

        private static int Ret(LuaState l, bool v)
        {
            l.Push(v);
            return 1;
        }

        // sol2's uint32 conversion of a number argument (LuaState.LlRound: an event's pos.x * 1000 of 201.80499 is
        // 201805); 0 for anything else
        private static uint OptU32(LuaState l, int idx) => l.IsNumber(idx) ? (uint)l.ToIntegral(idx) : 0;

        // ---- char_vars --------------------------------------------------------------------------------------------

        internal const string SqlCharVar = "SELECT value, expiry FROM char_vars WHERE charid = ? AND varname = ? LIMIT 1";
        // getCharVarsWithSuffix / getCharVarsWithPrefix
        internal const string SqlVarsLike = "SELECT varname, value, expiry FROM char_vars WHERE charid = ? AND varname LIKE ?";

        // CCharEntity::getCharVar: the cache while it is not expired, else charutils::FetchCharVar (which deletes an
        // expired row)
        private int CharVar(CharData c, string name)
        {
            long now = UnixNow();
            if (c.CharVars.TryGetValue(name, out var v) && (v.Expiry == 0 || v.Expiry > now)) return v.Value;

            int value = 0;
            uint expiry = 0;
            var r = Exec(SqlCharVar, BoundValue.Of(c.Id), BoundValue.Of(name));
            if (r != null && r.Next())
            {
                value = r.Get<int>("value");
                expiry = r.Get<uint>("expiry");
                if (expiry > 0 && expiry <= now)
                {
                    value = 0;
                    Exec("DELETE FROM char_vars WHERE charid = ? AND varname = ?", BoundValue.Of(c.Id), BoundValue.Of(name));
                }
            }
            c.CharVars[name] = (value, expiry);
            return value;
        }

        // CCharEntity::setCharVar + charutils::PersistCharVar; a Quest[..] / Mission[..] var's row copy with it
        private void SetCharVar(CharData c, string name, int value, uint expiry)
        {
            if (expiry > 0 && expiry <= UnixNow())
            {
                log_($"lua: Attempting to set variable '{name}' with an expired time: {expiry}");
                return;
            }
            c.CharVars[name] = (value, expiry);
            if (XI.Quests.CharProgress.IsProgressVar(name))
                PersistProgress(c, () => PersistCharVar(c.Id, name, value, expiry), p => p.SetVar(name, value, expiry));
            else
                PersistCharVar(c.Id, name, value, expiry);
        }

        // charutils::PersistCharVar; false when the statement failed
        private bool PersistCharVar(uint charId, string name, int value, uint expiry)
        {
            if (value == 0)
                return Exec("DELETE FROM char_vars WHERE charid = ? AND varname = ? LIMIT 1", BoundValue.Of(charId), BoundValue.Of(name)) != null;
            return Exec("INSERT INTO char_vars SET charid = ?, varname = ?, value = ?, expiry = ? ON DUPLICATE KEY UPDATE value = ?, expiry = ?",
                        BoundValue.Of(charId), BoundValue.Of(name), BoundValue.Of(value), BoundValue.Of(expiry), BoundValue.Of(value), BoundValue.Of(expiry)) != null;
        }

        private int GetCharVar(LuaState l) => Ret(l, Entity(l) is CharData c ? CharVar(c, l.ToStringArg(2)) : 0);

        private int SetCharVar(LuaState l)
        {
            if (Entity(l) is CharData c) SetCharVar(c, l.ToStringArg(2), l.ToIntArg(3), OptU32(l, 4));
            return 0;
        }

        // ---- events -----------------------------------------------------------------------------------------------

        // CLuaBaseEntity::ParseEvent: va[0] is stack index 3 (after self and the event id)
        private static EventInfo ParseEvent(LuaState l, int eventId, EventInfo preparation, EventType type)
        {
            const int va = 3;
            var e = new EventInfo { EventId = eventId, TargetEntity = preparation.TargetEntity, ScriptFile = preparation.ScriptFile, Type = type };
            var cutsceneOptions = new List<int> { 0 };

            if (l.IsTable(va))
            {
                for (int i = 0; i < 8; i++)
                {
                    l.Push((double)i);
                    l.GetTable(va);
                    uint param = OptU32(l, -1);
                    l.Pop(1);
                    if (param != 0) e.Params[i] = param;
                }

                l.GetField(va, "strings");
                if (l.IsTable(-1))
                {
                    int t = l.Top;
                    l.PushNil();
                    while (l.Next(t))
                    {
                        if (l.IsNumber(-2) && l.IsString(-1)) e.Strings[(int)l.ToIntegral(-2)] = l.ToStringValue(-1);
                        l.Pop(1);
                    }
                }
                l.Pop(1);

                e.TextTable = (short)FieldInt(l, va, "text_table", -1);
                e.InterruptText = (short)FieldInt(l, va, "interrupt_text", 0);
                e.EventFlags = (uint)FieldInt(l, va, "flags", 0);
                e.CanSkip = FieldBool(l, va, "canSkip");
                e.IsHidden = FieldBool(l, va, "isHidden");

                l.GetField(va, "cs_option");
                if (l.IsNumber(-1)) cutsceneOptions = new List<int> { (int)l.ToIntegral(-1) };
                else if (l.IsTable(-1))
                {
                    int t = l.Top;
                    l.PushNil();
                    while (l.Next(t))
                    {
                        if (l.IsNumber(-1)) cutsceneOptions.Add((int)l.ToIntegral(-1));
                        l.Pop(1);
                    }
                }
                l.Pop(1);
            }
            else
            {
                int current = 0;
                if (l.IsString(va))
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (l.IsString(va + current)) e.Strings[i] = l.ToStringValue(va + current);
                        current++;
                    }
                }
                for (int i = 0; i < 8; i++)
                {
                    if (l.IsNumber(va + current)) e.Params[i] = (uint)l.ToIntegral(va + current);
                    current++;
                }
                e.TextTable = l.IsNumber(va + 8) ? (short)l.ToIntegral(va + 8) : (short)-1;
            }

            if (type == EventType.OptionalCutscene) e.CutsceneOptions = cutsceneOptions;
            return e;
        }

        private static long FieldInt(LuaState l, int table, string key, long fallback)
        {
            l.GetField(table, key);
            long v = l.IsNumber(-1) ? l.ToIntegral(-1) : fallback;
            l.Pop(1);
            return v;
        }

        private static bool FieldBool(LuaState l, int table, string key)
        {
            l.GetField(table, key);
            bool v = l.Type(-1) == 1 && l.ToBoolean(-1);
            l.Pop(1);
            return v;
        }

        // sol::table::get_or for a bool: the field when it is a boolean, the default otherwise
        private static bool FieldBoolOr(LuaState l, int table, string key, bool def)
        {
            l.GetField(table, key);
            bool v = l.Type(-1) == 1 ? l.ToBoolean(-1) : def;
            l.Pop(1);
            return v;
        }

        // CLuaBaseEntity::StartEventHelper
        private int StartEvent(LuaState l, EventType type)
        {
            int eventId = l.ToIntArg(2);
            if (Entity(l) is not CharData c)
            {
                log_($"lua: Non-PC entity tried to start event {eventId}");
                return 0;
            }
            if (c.CurrentEvent.EventId == eventId)
            {
                log_($"lua: {c.Name} tried to start event {eventId} while already in it");
                return 0;
            }
            c.StatusEffects.DelStatusEffect(EffectId.Boost);
            var s = SessionOf(c);
            if (s == null) return 0;
            QueueEvent(s, c, ParseEvent(l, eventId, c.EventPreparation, type));
            return 0;
        }

        private int UpdateEvent(LuaState l)
        {
            if (Pc(l, "updateEvent") is not CharData c) return 0;
            var values = new List<(int, uint)>();
            if (l.IsTable(2))
            {
                for (int i = 0; i < 8; i++)
                {
                    l.Push((double)i);
                    l.GetTable(2);
                    uint param = OptU32(l, -1);
                    l.Pop(1);
                    if (param != 0) values.Add((i, param));
                }
            }
            else
            {
                for (int i = 0; i < 8; i++)
                    if (l.IsNumber(2 + i)) values.Add((i, (uint)l.ToIntegral(2 + i)));
            }
            PushTo(c, EventPackets.PendingNum(values));
            return 0;
        }

        private int Release(LuaState l)
        {
            if (Pc(l, "release") is not CharData c) return 0;
            var s = SessionOf(c);
            if (s == null) return 0;
            var mode = EventucoffMode.Standard;
            if (c.IsInEvent)
            {
                mode = EventucoffMode.CancelEvent;
                s.PacketList.AddLast(SystemMessage(MsgEventSkipped));
            }
            c.InSequence = false;
            s.PacketList.AddLast(EventPackets.Eventucoff(c, mode));
            s.PacketList.AddLast(EventPackets.Eventucoff(c, EventucoffMode.EventRecvPending));
            EndCurrentEvent(s, c);
            return 0;
        }

        // ---- messages ---------------------------------------------------------------------------------------------

        private static (uint Id, ushort TargId, string Name, bool IsChar) Speaker(object e) =>
            e is CharData c ? (c.Id, c.TargId, c.Name, true)
            : e is MobEntity m ? (m.Id, m.TargId, m.Name, false)
            : (((NpcEntity)e).Id, ((NpcEntity)e).TargId, ((NpcEntity)e).Name, false);

        // what a non-PC entity says reaches every char that has it spawned
        // CZoneEntities::PushPacket(CHAR_INRANGE) from an NPC: every char within 50 yalms, the NPC in its spawn list or
        // not (only an entity update or an action packet checks the spawn lists), so a hidden ??? still speaks (the
        // Pirates' Chart qm4's showText lines while it is DISAPPEAR)
        private void PushInRange(NpcEntity npc, BasicPacket p) => PushCharsNear(npc, p);

        private int MessageSpecial(LuaState l)
        {
            ushort id = (ushort)l.ToIntArg(2);
            if (Entity(l) is not CharData c)
            {
                log_($"lua: messageSpecial called on non-PC entity ({NameOf(Entity(l))})");
                return 0;
            }
            bool showName = l.Type(7) == 1 && l.ToBoolean(7);
            PushTo(c, EventPackets.TalkNumWork(c.Id, c.TargId, c.Name, true, id, OptU32(l, 3), OptU32(l, 4), OptU32(l, 5), OptU32(l, 6), showName));
            return 0;
        }

        private int ShowText(LuaState l)
        {
            var self = Entity(l);
            var target = l.ToObject(2) ?? throw new InvalidOperationException("null entity used on showText()");
            ushort id = (ushort)l.ToIntArg(3);
            uint p0 = l.IsNil(4) ? 0 : l.ToUIntArg(4), p1 = l.IsNil(5) ? 0 : l.ToUIntArg(5);
            uint p2 = l.IsNil(6) ? 0 : l.ToUIntArg(6), p3 = l.IsNil(7) ? 0 : l.ToUIntArg(7);
            bool showName = !l.IsNil(8) && l.ToBoolean(8);
            bool turn = l.IsNil(9) || l.ToBoolean(9);
            byte? type = l.IsNil(10) ? null : (byte)l.ToIntArg(10);

            // the speaker may be any entity: a char, an NPC, or a mob (a boss's lines in its fight)
            if (turn && target is NpcEntity npc)
            {
                var (sx, _, sz, _) = PosOf(self);
                npc.LookAt = TargIdOf(self);
                npc.Rotation = WorldAngle(npc.X, npc.Z, npc.Rotation, sx, sz);
                BroadcastNpcUpdate(npc, EntityUpdatePacket.UpdatePos);
            }

            var (tid, ttarg, tname, tchar) = Speaker(target);
            var packet = EventPackets.TalkNumWork(tid, ttarg, tname, tchar, id, p0, p1, p2, p3, showName, type);
            if (self is CharData c) PushTo(c, packet);
            else if (self is NpcEntity selfNpc) PushInRange(selfNpc, packet);
            else PushCharsNear(self, packet); // CZone::PushPacket(CHAR_INRANGE)
            return 0;
        }

        private int MessageText(LuaState l)
        {
            var self = Entity(l);
            var target = l.ToObject(2) ?? throw new InvalidOperationException("CLuaBaseEntity::messageText() - argument 1 of CLuaBaseEntity* was nullptr");
            ushort id = (ushort)l.ToIntArg(3);
            bool showName = true;
            byte mode = 0;
            if (!l.IsNil(4))
            {
                if (l.Type(4) == 1) showName = l.ToBoolean(4);
                else if (l.IsNumber(4)) mode = (byte)l.ToIntegral(4);
                else if (l.IsTable(4))
                {
                    // the face option turns the target to a given angle
                    l.GetField(4, "face");
                    bool faceGiven = l.IsNumber(-1);
                    byte face = faceGiven ? (byte)l.ToIntegral(-1) : (byte)0;
                    l.Pop(1);
                    l.GetField(4, "showName");
                    showName = l.Type(-1) != 1 || l.ToBoolean(-1);
                    l.Pop(1);
                    mode = (byte)FieldInt(l, 4, "mode", mode);
                    if (faceGiven && target is NpcEntity npc)
                    {
                        npc.LookAt = TargIdOf(self);
                        npc.Rotation = face;
                        BroadcastNpcUpdate(npc, EntityUpdatePacket.UpdatePos);
                    }
                }
            }
            if (!l.IsNil(5)) mode = (byte)l.ToIntArg(5);

            var (tid, ttarg, _, tchar) = Speaker(target);
            var packet = EventPackets.TalkNum(tid, ttarg, tchar, id, showName, mode);
            if (self is CharData c)
            {
                c.GotMessage = true;
                PushTo(c, packet);
            }
            else if (self is NpcEntity selfNpc) PushInRange(selfNpc, packet);
            else PushCharsNear(self, packet); // a mob's line (a battlefield's dialogue): CZone::PushPacket(CHAR_INRANGE)
            return 0;
        }

        // worldAngle: the facing from A to B (y is height, so the plane is x/z)
        internal static byte WorldAngle(float ax, float az, byte aRotation, float bx, float bz)
        {
            float dx = bx - ax, dz = bz - az;
            if (dx * dx + dz * dz <= 0.1f * 0.1f) return aRotation;
            float radians = MathF.Atan2(dz, dx);
            short raw = (short)(radians * -(128.0f / MathF.PI));
            return (byte)((raw % 256 + 256) % 256);
        }

        // ---- gil, key items, quests, missions ---------------------------------------------------------------------

        // the currency stack in inventory slot 0
        private static Item Gil(CharData c) => c.FindItem(Container.Inventory, 0) is { ItemId: 0xFFFF } gil ? gil : null;

        // ItemClaimTransaction::pay: the inventory row, then GP_SERV_COMMAND_ITEM_NUM
        private int DelGil(LuaState l)
        {
            int gil = l.ToIntArg(2);
            if (Pc(l, "delGil") is not CharData c) return Ret(l, false);
            if (gil < 0)
            {
                log_($"lua: delGil : Negative Gil ({gil}) passed to function");
                return Ret(l, false);
            }
            var item = Gil(c);
            if (item == null || (int)item.Quantity < gil)
            {
                log_("lua: delGil : Not enough Gil in currency slot");
                return Ret(l, false);
            }
            uint quantity = item.Quantity - (uint)gil;
            Exec("UPDATE char_inventory SET quantity = ? WHERE charid = ? AND location = ? AND slot = ?",
                 BoundValue.Of(quantity), BoundValue.Of(c.Id), BoundValue.Of((byte)Container.Inventory), BoundValue.Of((byte)0));
            item.Quantity = quantity;
            PushTo(c, EventPackets.ItemNum(Container.Inventory, 0, quantity));
            return Ret(l, true);
        }

        private static bool HasKeyItem(CharData c, int keyItem) => c.HasKeyItem(keyItem);

        // 0 not started, 1 current, 2 complete
        private int QuestStatus(CharData c, int log, int quest)
        {
            if (log < 0 || log >= MaxQuestArea || quest < 0 || quest >= MaxQuestId)
            {
                log_($"lua: getQuestStatus: questLogID {log} or QuestID {quest} is invalid");
                return 0;
            }
            int bit = 1 << quest % 8, off = log * CharData.QuestLogSize + quest / 8;
            if ((c.QuestLog[off + 32] & bit) != 0) return 2;
            return (c.QuestLog[off] & bit) != 0 ? 1 : 0;
        }

        private int GetCurrentMission(LuaState l)
        {
            if (Pc(l, "getCurrentMission") is not CharData c) return Ret(l, 0);
            int log = 0;
            if (l.IsNumber(2)) log = (byte)l.ToIntegral(2);
            else if (l.IsTable(2)) log = (byte)FieldInt(l, 2, "mission_log", 0);
            if (log >= MaxMissionArea)
            {
                log_($"lua: getCurrentMission: missionLogID {log} is invalid");
                return Ret(l, 0);
            }
            return Ret(l, c.MissionCurrent(log));
        }

        private int GetMissionStatus(LuaState l)
        {
            int log = l.ToEnumArg(2); // MissionLog
            if (Pc(l, "getMissionStatus") is not CharData c) return Ret(l, 0);
            if (log < 0 || log >= MaxMissionArea)
            {
                log_($"lua: getMissionStatus: missionLogID {log} is invalid");
                return Ret(l, 0);
            }
            uint status = (uint)c.MissionStatusUpper(log) << 16 | c.MissionStatusLower(log);
            if (!l.IsNumber(3)) return Ret(l, status);
            int pos = (byte)l.ToIntegral(3);
            if (pos > 7)
            {
                log_($"lua: getMissionStatus: position {pos} is invalid");
                return Ret(l, 0);
            }
            return Ret(l, status >> 4 * pos & 0xF);
        }

        private int HasCompletedMission(LuaState l)
        {
            int log = l.ToEnumArg(2), mission = (ushort)l.ToIntArg(3);
            if (Pc(l, "hasCompletedMission") is not CharData c) return Ret(l, false);
            if (log < 0 || log >= MaxMissionArea || mission >= MaxMissionId)
            {
                log_($"lua: hasCompletedMission: missionLogID {log} or Mission {mission} is invalid");
                return Ret(l, false);
            }
            bool complete = log == MissionLogCoP || mission >= 64 ? mission < c.MissionCurrent(log) : c.MissionComplete(log, mission);
            return Ret(l, complete);
        }

        // ---- position, teleports, home point ----------------------------------------------------------------------

        // GP_SERV_COMMAND_WPOS (POSMODE::NORMAL)
        private static BasicPacket Wpos(BattleEntity c)
        {
            var w = new PacketWriter(0x05B, 4 + S2cWpos.SizeOf);
            w.F32(4 + S2cWpos.OffX, c.X);
            w.F32(4 + S2cWpos.OffY, c.Y);
            w.F32(4 + S2cWpos.OffZ, c.Z);
            w.U32(4 + S2cWpos.OffUniqueNo, c.Id);
            w.U16(4 + S2cWpos.OffActIndex, c.TargId);
            w.U8(4 + S2cWpos.OffDir, c.Rotation);
            return w.Packet;
        }

        // CLuaBaseEntity::setPos: a char given a zone goes there on the next zone tick (requestedZoneChange)
        private int SetPos(LuaState l)
        {
            var e = Entity(l);
            float x, y, z;
            byte rot;
            var (cx, cy, cz, crot) = PosOf(e);
            if (l.IsTable(2))
            {
                l.GetField(2, "x");
                bool named = l.IsNumber(-1);
                l.Pop(1);
                if (named)
                {
                    x = (float)FieldNumber(l, 2, "x", cx);
                    y = (float)FieldNumber(l, 2, "y", cy);
                    z = (float)FieldNumber(l, 2, "z", cz);
                    rot = (byte)LuaState.LlRound(FieldNumber(l, 2, "rotation", crot)); // get_or<uint8>
                }
                else
                {
                    x = (float)IndexNumber(l, 2, 1, cx);
                    y = (float)IndexNumber(l, 2, 2, cy);
                    z = (float)IndexNumber(l, 2, 3, cz);
                    rot = (byte)LuaState.LlRound(IndexNumber(l, 2, 4, crot));
                }
            }
            else if (l.IsNumber(2))
            {
                x = (float)l.ToNumber(2);
                y = l.IsNumber(3) ? (float)l.ToNumber(3) : cy;
                z = l.IsNumber(4) ? (float)l.ToNumber(4) : cz;
                rot = l.IsNumber(5) ? (byte)l.ToIntegral(5) : crot;
            }
            else
            {
                log_("lua: CLuaBaseEntity::setPos() - Received non-table or float value for first parameter!");
                return 0;
            }

            if (e is NpcEntity npc)
            {
                (npc.X, npc.Y, npc.Z, npc.Rotation) = (x, y, z, rot);
                BroadcastNpcUpdate(npc, EntityUpdatePacket.UpdatePos);
                return 0;
            }
            if (e is BattleEntity b and not CharData)
            {
                (b.X, b.Y, b.Z, b.Rotation) = (x, y, z, rot);
                b.UpdateMask |= EntityUpdatePacket.UpdatePos;
                return 0;
            }

            var c = (CharData)e;
            var s = SessionOf(c);
            (c.X, c.Y, c.Z, c.Rotation) = (x, y, z, rot);
            // the char's Disappear: a zone change in flight, or a char still loading in (its 0x00A, where the zone's
            // onZoneIn runs); a char already zoning keeps its destination, and gets no 0x05B
            bool disappeared = c.Status == NpcEntity.StatusDisappear || s != null && zoneChanges_.ContainsKey(s);
            if (l.IsNumber(6) && disappeared) return 0;
            if (s == null) return 0;
            if (l.IsNumber(6))
            {
                ushort zone = (ushort)l.ToEnum(6); // as<xi::ZoneId>
                if (ZoneIpp(zone) == 0)
                {
                    log_($"lua: Char {c.Name} requested zone ({zone}) returned IPP of 0");
                    s.PacketList.AddLast(SystemMessage(MsgCouldNotEnter));
                    return 0;
                }
                c.Boundary = 0;
                zoneChanges_[s] = zone;
                SavePetForZoning(c);
            }
            else if (!disappeared)
            {
                s.PacketList.AddLast(Wpos(c));
            }
            return 0;
        }

        private static double FieldNumber(LuaState l, int table, string key, double fallback)
        {
            l.GetField(table, key);
            double v = l.IsNumber(-1) ? l.ToNumber(-1) : fallback;
            l.Pop(1);
            return v;
        }

        private static double IndexNumber(LuaState l, int table, int index, double fallback)
        {
            l.RawGetI(table, index);
            double v = l.IsNumber(-1) ? l.ToNumber(-1) : fallback;
            l.Pop(1);
            return v;
        }

        // telepoint_t: uint32 access[4], int32 menu[10]
        private static byte[] Telepoint(CharData c, int type) => type == TeleportHomepoint ? c.HomepointTele : type == TeleportSurvival ? c.SurvivalTele : null;

        private static uint TeleAccess(byte[] t, int set) => BitConverter.ToUInt32(t, set * 4);

        // charutils::SaveTeleport
        private void SaveTeleport(CharData c, int type)
        {
            if (type < c.TeleportMasks.Length && CharLoader.TeleportMaskColumns[type] is { } column)
                Exec($"UPDATE char_unlocks SET {column} = ? WHERE charid = ? LIMIT 1", BoundValue.Of(c.TeleportMasks[type]), BoundValue.Of(c.Id));
            else if (type == TeleportHomepoint || type == TeleportSurvival)
                Exec(type == TeleportHomepoint ? "UPDATE char_unlocks SET homepoints = ? WHERE charid = ? LIMIT 1" : "UPDATE char_unlocks SET survivals = ? WHERE charid = ? LIMIT 1",
                     BoundValue.Of(Telepoint(c, type)), BoundValue.Of(c.Id));
            else if (type == TeleportAbysseaConflux)
                Exec("UPDATE char_unlocks SET abyssea_conflux = ? WHERE charid = ? LIMIT 1", BoundValue.Of(c.AbysseaConflux), BoundValue.Of(c.Id));
            else if (type == TeleportWaypoint)
                Exec("UPDATE char_unlocks SET waypoints = ? WHERE charid = ? LIMIT 1", BoundValue.Of(c.Waypoints.AsSpan(0, 12).ToArray()), BoundValue.Of(c.Id));
            else if (type == TeleportEschanPortal)
                Exec("UPDATE char_unlocks SET eschan_portals = ? WHERE charid = ? LIMIT 1", BoundValue.Of(BitConverter.ToUInt32(c.Waypoints, 12)), BoundValue.Of(c.Id));
            else
                log_("charutils:SaveTeleport: Unknown type parameter.");
        }

        // a teleport_t uint32 mask by type: the outposts, runic portals, maws and campaign (the eschan portals after
        // the waypoints in CharData.Waypoints); null for the others
        private static uint? TeleportMask(CharData c, int type) =>
            type < c.TeleportMasks.Length && CharLoader.TeleportMaskColumns[type] != null ? c.TeleportMasks[type]
            : type == TeleportEschanPortal ? BitConverter.ToUInt32(c.Waypoints, 12) : null;

        private int HasTeleport(LuaState l)
        {
            int type = l.ToIntArg(2), bit = (byte)l.ToIntArg(3);
            if (Entity(l) is not CharData c)
            {
                log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                return Ret(l, false);
            }
            if (Telepoint(c, type) is { } t)
            {
                int set = l.IsNil(4) ? 0 : (byte)l.ToIntArg(4);
                if (l.IsNil(4) || set > 3)
                {
                    log_("lua: addTeleport : Attempt to index array out-of-bounds or parameter is nil.");
                    return Ret(l, false);
                }
                return Ret(l, (TeleAccess(t, set) & 1u << bit) != 0);
            }
            // waypoint_t's access[2]: bit / 32 picks the word
            if (type == TeleportWaypoint) return Ret(l, bit / 32 < 2 && (BitConverter.ToUInt32(c.Waypoints, bit / 32 * 4) & 1u << bit % 32) != 0);
            // the eschan portals and the abyssea conflux are not asked here
            if (type != TeleportEschanPortal && TeleportMask(c, type) is { } mask) return Ret(l, (mask & 1u << bit) != 0);
            log_("LuaBaseEntity::hasTeleport : Parameter 1 out of bounds.");
            return Ret(l, false);
        }

        private int AddTeleport(LuaState l)
        {
            int type = l.ToIntArg(2);
            int bitval = (int)l.ToUIntArg(3);
            uint bit = 1u << bitval;
            if (Entity(l) is not CharData c) return 0;
            int set = l.IsNil(4) ? 0 : (byte)l.ToIntArg(4);
            if ((type == TeleportHomepoint || type == TeleportSurvival) && (l.IsNil(4) || set > 3))
            {
                // the C++ logs and carries on into the array
                log_("lua: addteleport : Attempt to index array out-of-bounds or parameter is nil.");
                if (set > 3) return 0;
            }
            else if (type == TeleportAbysseaConflux && (l.IsNil(4) || set >= MaxAbysseaZones))
            {
                log_("Lua::addTeleport : Attempt to add Abyssea Conflux with invalid setval or set variable.");
                return 0;
            }
            if (Telepoint(c, type) is { } t) BitConverter.GetBytes(TeleAccess(t, set) | bit).CopyTo(t, set * 4);
            else if (type == TeleportAbysseaConflux) c.AbysseaConflux[set] |= (byte)bit;
            else if (type == TeleportWaypoint)
            {
                if (bitval / 32 >= 2) return 0;
                int at = bitval / 32 * 4;
                BitConverter.GetBytes(BitConverter.ToUInt32(c.Waypoints, at) | 1u << bitval % 32).CopyTo(c.Waypoints, at);
            }
            else if (type == TeleportEschanPortal) BitConverter.GetBytes(BitConverter.ToUInt32(c.Waypoints, 12) | bit).CopyTo(c.Waypoints, 12);
            else if (TeleportMask(c, type) != null) c.TeleportMasks[type] |= bit;
            else
            {
                log_("LuaBaseEntity::addTeleport : Parameter 1 out of bounds.");
                return 0;
            }
            SaveTeleport(c, type);
            return 0;
        }

        // getTeleport(type, abysseaRegion): the type's mask; the abyssea conflux's for one region (0 out of range)
        private int GetTeleport(LuaState l)
        {
            int type = l.ToIntArg(2);
            if (Entity(l) is not CharData c) return Ret(l, 0);
            if (type == TeleportAbysseaConflux)
            {
                int region = l.IsNumber(3) ? (byte)l.ToIntArg(3) : MaxAbysseaZones;
                return Ret(l, region < MaxAbysseaZones ? c.AbysseaConflux[region] : 0);
            }
            if (TeleportMask(c, type) is { } mask) return Ret(l, mask);
            log_("LuaBaseEntity::getteleport : Parameter 1 out of bounds.");
            return Ret(l, 0);
        }

        // getTeleportTable(type): the access words of the home points and survival guides (4) or the waypoints (2); nil
        // for the other types
        private int GetTeleportTable(LuaState l)
        {
            int type = (byte)l.ToIntArg(2);
            if (Pc(l, "getTeleportTable") is not CharData c)
            {
                l.PushNil();
                return 1;
            }
            int words = Telepoint(c, type) != null ? 4 : type == TeleportWaypoint ? 2 : 0;
            if (words == 0)
            {
                log_("LuaBaseEntity::getteleport : Parameter 1 out of bounds.");
                l.PushNil();
                return 1;
            }
            var access = Telepoint(c, type) ?? c.Waypoints;
            l.NewTable(words);
            for (int i = 0; i < words; i++)
            {
                l.Push((double)TeleAccess(access, i));
                l.RawSetI(-2, i + 1);
            }
            return 1;
        }

        // getTeleportMenu(type): the home points' or survival guides' ten favourites, or the waypoints'
        // { confirmation }; nil for the other types
        private int GetTeleportMenu(LuaState l)
        {
            int type = (byte)l.ToIntArg(2);
            if (Pc(l, "getTeleportMenu") is not CharData c)
            {
                l.PushNil();
                return 1;
            }
            var t = Telepoint(c, type);
            if (t == null && type != TeleportWaypoint)
            {
                log_("LuaBaseEntity::getTeleportMenu : Invalid Teleport Type passed to function.");
                l.PushNil();
                return 1;
            }
            if (t == null)
            {
                // waypoint_t.confirmation, the byte after the two access words
                l.NewTable(1);
                l.Push(c.Waypoints[WaypointConfirmation] != 0);
                l.RawSetI(-2, 1);
                return 1;
            }
            l.NewTable(10);
            for (int i = 0; i < 10; i++)
            {
                l.Push((double)BitConverter.ToInt32(t, 16 + i * 4));
                l.RawSetI(-2, i + 1);
            }
            return 1;
        }

        // setTeleportMenu(type, value): the home points' or survival guides' favourites from a table, or the waypoints'
        // confirmation from a value (as<bool>: nil and false are false), then saved
        private int SetTeleportMenu(LuaState l)
        {
            int type = (ushort)l.ToIntArg(2);
            if (Pc(l, "setTeleportMenu") is not CharData c) return 0;
            var t = Telepoint(c, type);
            if (t == null && type != TeleportWaypoint)
            {
                log_("LuaBaseEntity::setteleportMenu : Invalid Teleport Type passed to function.");
                return 0;
            }
            if (t == null) c.Waypoints[WaypointConfirmation] = (byte)(l.ToBoolean(3) ? 1 : 0);
            else
            {
                if (!l.IsTable(3)) throw new InvalidOperationException("setTeleportMenu: expected a table");
                // sol2 iterates the table with next; the menu fills in that order
                int x = 0;
                l.PushNil();
                while (l.Next(3))
                {
                    if (x < 10) BitConverter.GetBytes((int)l.ToIntegral(-1)).CopyTo(t, 16 + x * 4); // as<int32>
                    x++;
                    l.Pop(1);
                }
            }
            SaveTeleport(c, type);
            return 0;
        }

        private int SetHomePoint(LuaState l)
        {
            if (Pc(l, "setHomePoint") is not CharData c) return 0;
            Exec("UPDATE chars SET home_zone = ?, home_rot = ?, home_x = ?, home_y = ?, home_z = ? WHERE charid = ? LIMIT 1",
                 BoundValue.Of(c.Zone), BoundValue.Of(c.Rotation), BoundValue.Of(c.X), BoundValue.Of(c.Y), BoundValue.Of(c.Z), BoundValue.Of(c.Id));
            return 0;
        }
    }
}
